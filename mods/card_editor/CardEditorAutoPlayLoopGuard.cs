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
		public Dictionary<Player, int> PlaysByPlayer { get; } = new();
		public Dictionary<string, int> PlaysBySource { get; } = new(StringComparer.Ordinal);
		public Dictionary<string, int> CombatUsesBySource { get; } = new(StringComparer.Ordinal);
		public HashSet<string> WarnedCapKeys { get; } = new(StringComparer.Ordinal);
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
		private bool _disposed;

		public Scope(LoopToken? token)
		{
			_token = token;
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
		out IDisposable scope)
	{
		if (!TryEnterAutoPlayEffect(combatState, owner ?? playedCard?.Owner ?? sourceCard?.Owner, sourceCard, sourceEffect, out scope))
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

		bool suppressSelfLoops = !sourceEffect.AutoPlayAllowSelfTrigger
			|| CardEditorPerformanceSettings.PreventCardEditorAutoPlaySelfLoops;
		if (!suppressSelfLoops && string.IsNullOrWhiteSpace(sourceKey))
		{
			return true;
		}

		LoopToken token = new LoopToken
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
		scope = new Scope(token);
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
		if (combatState == null || !CardEditorPerformanceSettings.EnableCardEditorAutoPlayLoopCap)
		{
			return true;
		}

		Player? effectiveOwner = owner ?? playedCard?.Owner ?? sourceCard?.Owner;
		return effectiveOwner == null || TryConsumeGlobalPlay(combatState, effectiveOwner, sourceCard, sourceEffect, playedCard);
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
			state.PlaysByPlayer.Clear();
			state.PlaysBySource.Clear();
			state.WarnedCapKeys.Clear();
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

	private static bool TryConsumeGlobalPlay(CombatState combatState, Player owner, CardModel? sourceCard, CardExtraEffect? sourceEffect, CardModel? playedCard)
	{
		CombatGuardState state = GetTurnState(combatState);
		int cap = CardEditorPerformanceSettings.CardEditorAutoPlayLoopCapPerTurn;
		state.PlaysByPlayer.TryGetValue(owner, out int globalCount);
		if (globalCount >= cap)
		{
			string warnKey = $"global|{RuntimeHelpers.GetHashCode(owner)}|{combatState.RoundNumber}|{combatState.CurrentSide}";
			if (state.WarnedCapKeys.Add(warnKey))
			{
				Log.Warn(
					$"[CardEditor][LoopGuard] Card Editor autoplay cap reached for player={owner} " +
					$"round={combatState.RoundNumber} side={combatState.CurrentSide} cap={cap}. " +
					$"Skipped card={GetCardIdText(playedCard)} source={BuildSourceLabel(sourceCard, sourceEffect)}");
			}
			return false;
		}

		state.PlaysByPlayer[owner] = globalCount + 1;
		return true;
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
