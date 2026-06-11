using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorAutoPlayLoopGuard
{
	private sealed class CombatGuardState
	{
		public int RoundNumber { get; set; } = -1;
		public CombatSide CurrentSide { get; set; }
		public Dictionary<string, int> PlaysBySource { get; } = new(StringComparer.Ordinal);
		public Dictionary<string, int> CombatUsesBySource { get; } = new(StringComparer.Ordinal);
		public HashSet<string> WarnedUseLimitKeys { get; } = new(StringComparer.Ordinal);
	}

	private sealed class RunGuardState
	{
		public Dictionary<string, int> UsesBySource { get; } = new(StringComparer.Ordinal);
		public HashSet<string> WarnedUseLimitKeys { get; } = new(StringComparer.Ordinal);
	}

	private sealed class LoopToken
	{
		public required string SourceKey { get; init; }
		public required string SourceCardId { get; init; }
		public required string SourceEffectId { get; init; }
		public required string SourceEffectSignature { get; init; }
		public required string SourceLabel { get; init; }
		public bool SuppressSelfLoops { get; init; }
		public bool PrecountedNestedBatch { get; init; }
	}

	private sealed class Scope : IDisposable
	{
		private readonly LoopToken? _token;
		private readonly ChainNode? _chainNode;
		private bool _disposed;

		public Scope(LoopToken? token, ChainNode? chainNode)
		{
			_token = token;
			_chainNode = chainNode;
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}
			_disposed = true;
			if (_token != null)
			{
				PopToken(_token);
			}
			if (_chainNode != null)
			{
				// Dispose runs in the same async frame as the TryEnter call, so this write flows
				// forward in that frame: sequential SIBLING activations see the parent again
				// instead of inheriting each other's path. (Values written deeper in the awaited
				// subtree never flowed back up, so _chain.Value here is still our node.)
				_chain.Value = _chainNode.Parent;
			}
		}
	}

	private sealed class EmptyScope : IDisposable
	{
		public static EmptyScope Instance { get; } = new();
		public void Dispose()
		{
		}
	}

	private sealed class UseLimitSourceScope : IDisposable
	{
		private readonly object _sourceInstance;
		private bool _disposed;

		public UseLimitSourceScope(object sourceInstance)
		{
			_sourceInstance = sourceInstance;
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}
			_disposed = true;
			PopUseLimitSourceInstance(_sourceInstance);
		}
	}

	private static readonly ConditionalWeakTable<CombatState, CombatGuardState> _states = new();
	private static readonly ConditionalWeakTable<object, RunGuardState> _runUseStates = new();
	private static readonly AsyncLocal<Stack<LoopToken>?> _activeTokens = new();
	private static readonly AsyncLocal<Stack<object>?> _useLimitSourceInstances = new();

	// ----- Chain guard: bounds auto-action recursion (Whenever-driven and otherwise) -----
	// One ChainNode per auto-effect ACTIVATION (the inner duplicate guard entry of an already-
	// counted activation passes consumeActivation:false and creates no node). The AsyncLocal
	// write happens synchronously on the calling async method's ExecutionContext, so it flows
	// DOWN into the awaited execution subtree (the played card's hooks, its whenever reactions,
	// their nested auto actions) and never back up — each manual action therefore starts a
	// fresh chain, and N independent trigger firings are N independent chains (no false
	// positives for legitimate repeated triggers). The PATH (parent links) is immutable; the
	// TOTALS holder is shared by reference chain-wide so breadth is bounded too.
	private sealed class ChainTotals
	{
		public int Activations;
	}

	private sealed class ChainNode
	{
		public required ChainTotals Totals { get; init; }
		public ChainNode? Parent { get; init; }
		public required string EffectKey { get; init; }
		public required int Depth { get; init; }
	}

	private static readonly AsyncLocal<ChainNode?> _chain = new();
	private static readonly HashSet<string> _warnedChainKeys = new(StringComparer.Ordinal);

	// Same effect at most 3 times on any root-to-leaf activation path (stops "whenever a card
	// is played, play a card" self-amplification and A->B->A ping-pong); at most 12 nested
	// activations on one path (cross-effect cycles — generous because domino decks of DISTINCT
	// chained cards are core mod audience); at most 64 activations total under one root
	// (breadth-times-depth explosion). Legitimate finite chains stay far below all three.
	private const int MaxSameEffectPerChainPath = 3;
	private const int MaxChainDepth = 12;
	private const int MaxChainActivations = 64;

	private static bool TryCreateChainNode(string chainKey, CardModel? sourceCard, CardExtraEffect sourceEffect, out ChainNode? node)
	{
		node = null;
		ChainNode? parent = _chain.Value;
		ChainTotals totals = parent?.Totals ?? new ChainTotals();
		int depth = (parent?.Depth ?? 0) + 1;

		if (depth > MaxChainDepth)
		{
			WarnChainStop(chainKey, sourceCard, sourceEffect, $"chain depth would exceed {MaxChainDepth}");
			return false;
		}

		if (totals.Activations >= MaxChainActivations)
		{
			WarnChainStop(chainKey, sourceCard, sourceEffect, $"chain activation budget {MaxChainActivations} exhausted");
			return false;
		}

		int sameKeyOnPath = 0;
		for (ChainNode? walk = parent; walk != null; walk = walk.Parent)
		{
			if (string.Equals(walk.EffectKey, chainKey, StringComparison.Ordinal)
				&& ++sameKeyOnPath >= MaxSameEffectPerChainPath)
			{
				WarnChainStop(chainKey, sourceCard, sourceEffect, $"same effect {MaxSameEffectPerChainPath}+ times on one chain path");
				return false;
			}
		}

		totals.Activations++;
		node = new ChainNode { Totals = totals, Parent = parent, EffectKey = chainKey, Depth = depth };
		return true;
	}

	private static void WarnChainStop(string chainKey, CardModel? sourceCard, CardExtraEffect sourceEffect, string reason)
	{
		if (_warnedChainKeys.Add(chainKey + "|" + reason))
		{
			Log.Warn($"[CardEditor][LoopGuard] Auto-action chain stopped ({reason}). Source={BuildSourceLabel(sourceCard, sourceEffect)}");
		}
	}

	public static string BuildPowerUseLimitInstanceKey(long entryId)
	{
		return "power|" + entryId.ToString(CultureInfo.InvariantCulture);
	}

	public static IDisposable PushUseLimitSourceInstance(object? sourceInstance)
	{
		if (sourceInstance == null)
		{
			return EmptyScope.Instance;
		}

		Stack<object> stack = _useLimitSourceInstances.Value ??= new Stack<object>();
		stack.Push(sourceInstance);
		return new UseLimitSourceScope(sourceInstance);
	}

	public static bool TryEnterAutoPlay(
		CombatState? combatState,
		Player? owner,
		CardModel? sourceCard,
		CardExtraEffect? sourceEffect,
		CardModel? playedCard,
		out IDisposable scope,
		bool consumeActivation = true)
	{
		if (!TryEnterAutoPlayEffect(combatState, owner ?? playedCard?.Owner ?? sourceCard?.Owner, sourceCard, sourceEffect, out scope, consumeActivation: consumeActivation))
		{
			return false;
		}

		if (!TryEnterAutoPlayedCard(combatState, owner, sourceCard, sourceEffect, playedCard))
		{
			scope.Dispose();
			scope = EmptyScope.Instance;
			return false;
		}

		return true;
	}

	// consumeActivation:false marks the INNER duplicate guard entry of an activation the caller
	// already counted (precounted nested batch) — it pushes the suppression token but creates no
	// chain node, so caps aren't double-counted per activation. Pass it as
	// !IsAutoPlayEffectPrecounted(...) at inner action sites.
	public static bool TryEnterAutoPlayEffect(
		CombatState? combatState,
		Player? owner,
		CardModel? sourceCard,
		CardExtraEffect? sourceEffect,
		out IDisposable scope,
		bool consumeActivation = true,
		bool markPrecountedNestedBatch = false)
	{
		scope = EmptyScope.Instance;

		if (sourceEffect == null)
		{
			return true;
		}

		Player? effectiveOwner = owner ?? sourceCard?.Owner;
		string sourceKey = effectiveOwner != null ? BuildSourceKey(effectiveOwner, sourceCard, sourceEffect) : string.Empty;

		ChainNode? chainNode = null;
		if (consumeActivation)
		{
			string chainKey = string.IsNullOrWhiteSpace(sourceKey) ? BuildEffectSignature(sourceEffect) : sourceKey;
			if (!TryCreateChainNode(chainKey, sourceCard, sourceEffect, out chainNode))
			{
				return false;
			}
		}

		bool suppressSelfLoops = !sourceEffect.AutoPlayAllowSelfTrigger;
		LoopToken? token = null;
		if (suppressSelfLoops || !string.IsNullOrWhiteSpace(sourceKey))
		{
			token = new LoopToken
			{
				SourceKey = sourceKey,
				SourceCardId = GetCardIdText(sourceCard),
				SourceEffectId = Normalize(sourceEffect.EffectId),
				SourceEffectSignature = BuildEffectSignature(sourceEffect),
				SourceLabel = BuildSourceLabel(sourceCard, sourceEffect),
				SuppressSelfLoops = suppressSelfLoops,
				PrecountedNestedBatch = markPrecountedNestedBatch
			};
			PushToken(token);
		}

		if (chainNode != null)
		{
			_chain.Value = chainNode;
		}

		if (token != null || chainNode != null)
		{
			scope = new Scope(token, chainNode);
		}
		return true;
	}

	public static bool IsAutoPlayEffectPrecounted(
		CombatState? combatState,
		Player? owner,
		CardModel? sourceCard,
		CardExtraEffect? sourceEffect)
	{
		if (sourceEffect == null)
		{
			return false;
		}

		Player? effectiveOwner = owner ?? sourceCard?.Owner;
		if (effectiveOwner == null)
		{
			return false;
		}

		string sourceKey = BuildSourceKey(effectiveOwner, sourceCard, sourceEffect);
		if (string.IsNullOrWhiteSpace(sourceKey))
		{
			return false;
		}

		Stack<LoopToken>? stack = _activeTokens.Value;
		return stack != null && stack.Any(token => token.PrecountedNestedBatch && string.Equals(token.SourceKey, sourceKey, StringComparison.Ordinal));
	}

	public static int GetRemainingAutoPlayActivations(
		CombatState? combatState,
		Player? owner,
		CardModel? sourceCard,
		CardExtraEffect? sourceEffect)
	{
		if (sourceEffect == null || !CardEditorExtraEffects.SupportsAutoPlayLoopControls(sourceEffect.Kind))
		{
			return -1;
		}

		return GetRemainingEffectUses(combatState, owner, sourceCard, sourceEffect);
	}

	public static bool TryConsumeEffectUseLimit(
		CombatState? combatState,
		Player? owner,
		CardModel? sourceCard,
		CardExtraEffect? sourceEffect,
		object? sourceInstance = null)
	{
		if (sourceEffect == null)
		{
			return true;
		}

		int useLimit = Math.Clamp(sourceEffect.AutoPlayLoopLimit, 0, 999);
		if (useLimit <= 0)
		{
			return true;
		}

		Player? effectiveOwner = owner ?? sourceCard?.Owner;
		if (effectiveOwner == null)
		{
			return true;
		}

		Dictionary<string, int>? counters = GetUseCounters(combatState, effectiveOwner, sourceEffect, out HashSet<string>? warnedKeys);
		if (counters == null)
		{
			return true;
		}

		object? effectiveSourceInstance = sourceInstance ?? CurrentUseLimitSourceInstance() ?? sourceCard;
		string sourceKey = BuildUseLimitKey(effectiveOwner, sourceCard, sourceEffect, effectiveSourceInstance);
		counters.TryGetValue(sourceKey, out int used);
		if (used >= useLimit)
		{
			string warnKey = $"{sourceEffect.UseLimitWindow}|{RuntimeHelpers.GetHashCode(effectiveOwner)}|{sourceKey}";
			if (warnedKeys?.Add(warnKey) == true)
			{
				Log.Warn(
					$"[CardEditor][UseLimit] Effect use limit reached for player={effectiveOwner} " +
					$"window={sourceEffect.UseLimitWindow} limit={useLimit}. " +
					$"Skipped source={BuildSourceLabel(sourceCard, sourceEffect)}");
			}
			return false;
		}

		counters[sourceKey] = used + 1;
		RefreshUseLimitText(effectiveOwner, sourceCard, sourceEffect, sourceKey);
		return true;
	}

	public static int GetRemainingEffectUses(
		CombatState? combatState,
		Player? owner,
		CardModel? sourceCard,
		CardExtraEffect? sourceEffect,
		object? sourceInstance = null)
	{
		if (sourceEffect == null)
		{
			return -1;
		}

		int useLimit = Math.Clamp(sourceEffect.AutoPlayLoopLimit, 0, 999);
		if (useLimit <= 0)
		{
			return -1;
		}

		Player? effectiveOwner = owner ?? sourceCard?.Owner;
		if (effectiveOwner == null)
		{
			return useLimit;
		}

		Dictionary<string, int>? counters = GetUseCounters(combatState, effectiveOwner, sourceEffect, out _);
		if (counters == null)
		{
			return -1;
		}

		object? effectiveSourceInstance = sourceInstance ?? CurrentUseLimitSourceInstance() ?? sourceCard;
		string sourceKey = BuildUseLimitKey(effectiveOwner, sourceCard, sourceEffect, effectiveSourceInstance);
		counters.TryGetValue(sourceKey, out int used);
		return Math.Clamp(useLimit - used, 0, useLimit);
	}

	public static bool TryEnterAutoPlayedCard(
		CombatState? combatState,
		Player? owner,
		CardModel? sourceCard,
		CardExtraEffect? sourceEffect,
		CardModel? playedCard)
	{
		return true;
	}

	public static bool ShouldSuppressEffect(CardModel? effectOwnerCard, CardExtraEffect? effect)
	{
		if (effect == null)
		{
			return false;
		}

		Stack<LoopToken>? stack = _activeTokens.Value;
		if (stack == null || stack.Count == 0)
		{
			return false;
		}

		string effectId = Normalize(effect.EffectId);
		string signature = BuildEffectSignature(effect);
		foreach (LoopToken token in stack)
		{
			if (!token.SuppressSelfLoops)
			{
				continue;
			}

			if (!string.IsNullOrWhiteSpace(effectId)
				&& string.Equals(effectId, token.SourceEffectId, StringComparison.Ordinal))
			{
				CardEditorMod.VerboseLog($"[CardEditor][LoopGuard] Suppressed self-loop effect by id card={GetCardIdText(effectOwnerCard)} effect={effectId} source={token.SourceLabel}");
				return true;
			}

			if (!string.IsNullOrWhiteSpace(signature)
				&& string.Equals(signature, token.SourceEffectSignature, StringComparison.Ordinal))
			{
				CardEditorMod.VerboseLog($"[CardEditor][LoopGuard] Suppressed self-loop effect by signature card={GetCardIdText(effectOwnerCard)} source={token.SourceLabel}");
				return true;
			}
		}

		return false;
	}

	private static CombatGuardState GetTurnState(CombatState combatState)
	{
		CombatGuardState state = _states.GetOrCreateValue(combatState);
		if (state.RoundNumber != combatState.RoundNumber || state.CurrentSide != combatState.CurrentSide)
		{
			state.RoundNumber = combatState.RoundNumber;
			state.CurrentSide = combatState.CurrentSide;
			state.PlaysBySource.Clear();
			state.WarnedUseLimitKeys.Clear();
		}

		return state;
	}

	private static Dictionary<string, int>? GetUseCounters(
		CombatState? combatState,
		Player owner,
		CardExtraEffect sourceEffect,
		out HashSet<string>? warnedKeys)
	{
		warnedKeys = null;
		if (owner == null || sourceEffect == null)
		{
			return null;
		}

		switch (sourceEffect.UseLimitWindow)
		{
			case CardExtraEffectUseLimitWindow.Combat:
				if (combatState == null)
				{
					return null;
				}
				CombatGuardState combatStateData = _states.GetOrCreateValue(combatState);
				warnedKeys = combatStateData.WarnedUseLimitKeys;
				return combatStateData.CombatUsesBySource;
			case CardExtraEffectUseLimitWindow.Run:
				RunGuardState runStateData = _runUseStates.GetOrCreateValue(GetRunUseLimitOwner(owner));
				warnedKeys = runStateData.WarnedUseLimitKeys;
				return runStateData.UsesBySource;
			default:
				if (combatState == null)
				{
					return null;
				}
				CombatGuardState turnStateData = GetTurnState(combatState);
				warnedKeys = turnStateData.WarnedUseLimitKeys;
				return turnStateData.PlaysBySource;
		}
	}

	private static object GetRunUseLimitOwner(Player owner)
	{
		try
		{
			object? runState = owner.RunState;
			return runState ?? (object)owner;
		}
		catch
		{
			return owner;
		}
	}

	private static void RefreshUseLimitText(Player owner, CardModel? sourceCard, CardExtraEffect sourceEffect, string sourceKey)
	{
		if (string.IsNullOrWhiteSpace(sourceKey))
		{
			RefreshCardText(sourceCard);
			return;
		}

		try
		{
			if (sourceEffect.AutoPlayLoopScope == CardExtraEffectAutoPlayLoopScope.ThisCard)
			{
				RefreshCardText(sourceCard);
				return;
			}

			foreach (var pile in owner.Piles)
			{
				foreach (CardModel card in pile.Cards)
				{
					if (ShouldRefreshUseLimitCard(owner, card, sourceCard, sourceEffect, sourceKey))
					{
						RefreshCardText(card);
					}
				}
			}
		}
		catch
		{
			RefreshCardText(sourceCard);
		}
	}

	private static bool ShouldRefreshUseLimitCard(
		Player owner,
		CardModel card,
		CardModel? sourceCard,
		CardExtraEffect sourceEffect,
		string sourceKey)
	{
		if (card == null)
		{
			return false;
		}

		if (sourceEffect.AutoPlayLoopScope == CardExtraEffectAutoPlayLoopScope.AllCopies)
		{
			return string.Equals(GetCardIdText(card), GetCardIdText(sourceCard), StringComparison.Ordinal);
		}

		if (sourceEffect.AutoPlayLoopScope == CardExtraEffectAutoPlayLoopScope.Global)
		{
			return true;
		}

		if (sourceEffect.AutoPlayLoopScope != CardExtraEffectAutoPlayLoopScope.EquivalentEffect)
		{
			return ReferenceEquals(card, sourceCard);
		}

		try
		{
			foreach (CardExtraEffect effect in CardEditorExtraEffects.GetEffectsForDescription(card, isUpgradePreview: false))
			{
				if (effect == null
					|| effect.AutoPlayLoopLimit <= 0)
				{
					continue;
				}

				string candidateKey = BuildUseLimitKey(owner, card, effect, card);
				if (string.Equals(candidateKey, sourceKey, StringComparison.Ordinal))
				{
					return true;
				}
			}
		}
		catch
		{
		}

		return false;
	}

	private static void RefreshCardText(CardModel? card)
	{
		if (card == null)
		{
			return;
		}

		try
		{
			card.DynamicVars.RecalculateForUpgradeOrEnchant();
		}
		catch
		{
		}

		try
		{
			card.InvokeEnergyCostChanged();
		}
		catch
		{
		}
	}

	private static string BuildSourceKey(Player owner, CardModel? sourceCard, CardExtraEffect sourceEffect)
	{
		string ownerKey = RuntimeHelpers.GetHashCode(owner).ToString(CultureInfo.InvariantCulture);
		string effectSignature = BuildEffectSignature(sourceEffect);
		return sourceEffect.AutoPlayLoopScope switch
		{
			CardExtraEffectAutoPlayLoopScope.Global => string.Join("|", new[]
			{
				ownerKey,
				"global",
				effectSignature
			}),
			CardExtraEffectAutoPlayLoopScope.EquivalentEffect => string.Join("|", new[]
			{
				ownerKey,
				"equivalent",
				Int(Math.Clamp(sourceEffect.AutoPlayLoopLimit, 0, 999)),
				effectSignature
			}),
			CardExtraEffectAutoPlayLoopScope.AllCopies => string.Join("|", new[]
			{
				ownerKey,
				"copies",
				GetCardIdText(sourceCard),
				Normalize(sourceEffect.EffectId),
				Int(Math.Clamp(sourceEffect.AutoPlayLoopLimit, 0, 999)),
				effectSignature
			}),
			_ => string.Join("|", new[]
			{
				ownerKey,
				"card",
				sourceCard == null ? "" : RuntimeHelpers.GetHashCode(sourceCard).ToString(CultureInfo.InvariantCulture),
				GetCardIdText(sourceCard),
				Normalize(sourceEffect.EffectId),
				Int(Math.Clamp(sourceEffect.AutoPlayLoopLimit, 0, 999)),
				effectSignature
			})
		};
	}

	private static string BuildUseLimitKey(Player owner, CardModel? sourceCard, CardExtraEffect sourceEffect, object? sourceInstance)
	{
		string ownerKey = RuntimeHelpers.GetHashCode(owner).ToString(CultureInfo.InvariantCulture);
		string effectSignature = BuildEffectSignature(sourceEffect);
		string groupedEffectSignature = BuildGroupedUseLimitSignature(sourceEffect);
		return sourceEffect.AutoPlayLoopScope switch
		{
			CardExtraEffectAutoPlayLoopScope.Global => string.Join("|", new[]
			{
				ownerKey,
				"global"
			}),
			CardExtraEffectAutoPlayLoopScope.EquivalentEffect => string.Join("|", new[]
			{
				ownerKey,
				"equivalent",
				effectSignature
			}),
			CardExtraEffectAutoPlayLoopScope.AllCopies => string.Join("|", new[]
			{
				ownerKey,
				"copies",
				GetCardIdText(sourceCard),
				groupedEffectSignature
			}),
			_ => string.Join("|", new[]
			{
				ownerKey,
				"instance",
				GetSourceInstanceKey(sourceInstance ?? sourceCard),
				GetCardIdText(sourceCard),
				groupedEffectSignature
			})
		};
	}

	private static string BuildGroupedUseLimitSignature(CardExtraEffect effect)
	{
		string groupId = Normalize(effect.UseLimitGroupId);
		return string.IsNullOrWhiteSpace(groupId)
			? string.Join("|", new[] { Normalize(effect.EffectId), BuildEffectSignature(effect) })
			: string.Join("|", new[] { "group", groupId });
	}

	private static string GetSourceInstanceKey(object? sourceInstance)
	{
		if (sourceInstance == null)
		{
			return "";
		}

		if (sourceInstance is string text)
		{
			return Normalize(text);
		}

		return RuntimeHelpers.GetHashCode(sourceInstance).ToString(CultureInfo.InvariantCulture);
	}

	private static object? CurrentUseLimitSourceInstance()
	{
		Stack<object>? stack = _useLimitSourceInstances.Value;
		return stack != null && stack.Count > 0 ? stack.Peek() : null;
	}

	private static void PopUseLimitSourceInstance(object sourceInstance)
	{
		Stack<object>? stack = _useLimitSourceInstances.Value;
		if (stack == null || stack.Count == 0 || sourceInstance == null)
		{
			return;
		}

		if (ReferenceEquals(stack.Peek(), sourceInstance) || Equals(stack.Peek(), sourceInstance))
		{
			stack.Pop();
			return;
		}

		object[] snapshot = stack.ToArray();
		stack.Clear();
		bool removed = false;
		for (int i = snapshot.Length - 1; i >= 0; i--)
		{
			object item = snapshot[i];
			if (!removed && (ReferenceEquals(item, sourceInstance) || Equals(item, sourceInstance)))
			{
				removed = true;
				continue;
			}
			stack.Push(item);
		}
	}

	private static void PushToken(LoopToken token)
	{
		Stack<LoopToken> stack = _activeTokens.Value ??= new Stack<LoopToken>();
		stack.Push(token);
	}

	private static void PopToken(LoopToken token)
	{
		Stack<LoopToken>? stack = _activeTokens.Value;
		if (stack == null || stack.Count == 0)
		{
			return;
		}

		if (ReferenceEquals(stack.Peek(), token))
		{
			stack.Pop();
			return;
		}

		LoopToken[] snapshot = stack.ToArray();
		stack.Clear();
		bool removed = false;
		for (int i = snapshot.Length - 1; i >= 0; i--)
		{
			LoopToken item = snapshot[i];
			if (!removed && ReferenceEquals(item, token))
			{
				removed = true;
				continue;
			}
			stack.Push(item);
		}
	}

	private static string BuildSourceLabel(CardModel? sourceCard, CardExtraEffect? sourceEffect)
	{
		return $"{GetCardIdText(sourceCard)}:{sourceEffect?.Kind.ToString() ?? "Unknown"}:{Normalize(sourceEffect?.EffectId)}";
	}

	private static string BuildEffectSignature(CardExtraEffect effect)
	{
		return string.Join("|", new[]
		{
			Int((int)effect.Kind),
			Int((int)effect.Trigger),
			Int((int)effect.Timing),
			Bool(effect.AsPower),
			Int(effect.Amount),
			Int((int)effect.Target),
			Int((int)effect.PowerTriggerFrom),
			Int((int)effect.PowerTargeting),
			Int((int)effect.PowerTriggerCountEvent),
			Int((int)effect.TriggerCardPool),
			Int((int)effect.TriggerCardType),
			Int((int)effect.TriggerCardFilter),
			Int((int)effect.GeneratedCardPool),
			Int((int)effect.GeneratedCardType),
			Normalize(effect.GeneratedCardCustomTag),
			Int((int)effect.CardSelectionPool),
			Int((int)effect.CardSelectionType),
			Int((int)effect.CardSelectionFilter),
			Int((int)effect.CardSelectionPile),
			Int((int)effect.CardSelectionMode),
			Int((int)effect.CardMatchMode),
			Normalize(effect.MatchCardId),
			Int((int)effect.MatchTagKind),
			Int((int)effect.MatchVanillaTag),
			Normalize(effect.MatchCustomTag),
			Normalize(effect.MatchCustomKeyword),
			Bool(effect.NameFilterEnabled),
			Normalize(effect.NameFilterText),
			Bool(effect.CostFilterEnabled),
			Int((int)effect.CostFilterMode),
			Int(effect.CostFilterMax),
			Normalize(effect.AutoActionEffectIds)
		});
	}

	private static string GetCardIdText(CardModel? card)
	{
		try
		{
			return card?.Id.ToString() ?? "";
		}
		catch
		{
			return "";
		}
	}

	private static string Normalize(string? value)
	{
		return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
	}

	private static string Int(int value)
	{
		return value.ToString(CultureInfo.InvariantCulture);
	}

	private static string Bool(bool value)
	{
		return value ? "1" : "0";
	}
}
