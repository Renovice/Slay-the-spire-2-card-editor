using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

// Suppresses the override-OnPlay postfix while the mod itself invokes a card's OnPlay
// reflectively (auto-action "vanilla OnPlay" payloads, borrowed effect sources): the Harmony
// detour runs for reflective invocations too, and composing rows onto those would re-run the
// card's own override rows against the wrong CardPlay. ThreadStatic on purpose (NOT AsyncLocal):
// the postfix gates execute synchronously inside MethodInfo.Invoke, and the flag must not flow
// into the returned task's awaited continuations.
internal static class CardEditorReflectiveOnPlayGuard
{
	[ThreadStatic]
	private static int _depth;

	internal static bool IsActive => _depth > 0;

	// Dispose BEFORE awaiting the invoked task — ThreadStatic state held across an await would
	// linger on whichever thread resumes first.
	internal static IDisposable PushScoped() => new Scope();

	private sealed class Scope : IDisposable
	{
		public Scope()
		{
			_depth++;
		}

		public void Dispose()
		{
			_depth--;
		}
	}
}

// Runs an overridden vanilla card's immediate extra-effect rows WITH the play instead of after
// it: a postfix on the card type's most-derived OnPlay declaration composes the rows onto
// OnPlay's returned (hot) task — appending to a hot task is legal, and the vanilla wrapper
// awaits OnPlay before Enchantment/Affliction OnPlay, Hook.AfterCardPlayed and result-pile
// routing, so the rows land exactly where created cards' rows already run today. The
// AfterCardPlayed hook postfix then runs only the reactions phase for marked plays.
// Patching is LAZY (per overridden card type) — patching all ~550 vanilla OnPlay declarations
// at boot would cost seconds; a play already in flight when a patch lands simply lacks the
// marker and falls back to the legacy after-play timing for that one play.
internal static class CardEditorOverrideOnPlayPatcher
{
	private static readonly object _lock = new object();
	private static readonly HashSet<Type> _checkedTypes = new HashSet<Type>();
	// Keyed by declared method, NOT by Harmony owner id: several vanilla OnPlay methods
	// (Acrobatics/Survivor/DaggerThrow) already carry this mod's targeted-discard prefixes, so
	// an Owners-based check would silently refuse to add this postfix to them.
	private static readonly HashSet<MethodBase> _patchedMethods = new HashSet<MethodBase>();
	private static readonly ConcurrentDictionary<Type, MethodInfo?> _effectiveOnPlayByType = new ConcurrentDictionary<Type, MethodInfo?>();
	private static readonly Type[] _onPlaySignature = { typeof(PlayerChoiceContext), typeof(CardPlay) };
	private static Harmony? _harmony;

	internal static void EnsureForAllStoredOverrides()
	{
		try
		{
			foreach (ModelId cardId in CardEditorOverrides.AllOverrides.Keys.ToList())
			{
				EnsureForCardId(cardId);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed ensuring OnPlay patches for stored overrides: {ex}");
		}
	}

	internal static void EnsureForCardId(ModelId cardId)
	{
		try
		{
			if (cardId == null || cardId == ModelId.none)
			{
				return;
			}

			CardModel? canonical = ModelDb.GetByIdOrNull<CardModel>(cardId);
			if (canonical == null)
			{
				return;
			}

			EnsureForCardType(canonical.GetType());
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed resolving OnPlay patch target for {cardId}: {ex}");
		}
	}

	internal static void EnsureForCardType(Type? cardType)
	{
		if (cardType == null
			|| !typeof(CardModel).IsAssignableFrom(cardType)
			|| typeof(CardEditorCreatedCardBase).IsAssignableFrom(cardType))
		{
			return;
		}

		lock (_lock)
		{
			if (!_checkedTypes.Add(cardType))
			{
				return;
			}
		}

		try
		{
			// Nearest OnPlay declaration up the chain: the card's own override, or base
			// CardModel.OnPlay for the handful of types that never declare one. The postfix's
			// most-derived gate keeps a base declaration from firing for instances whose
			// effective OnPlay is a deeper override.
			MethodInfo? effective = GetEffectiveOnPlay(cardType);
			if (effective?.DeclaringType == null)
			{
				return;
			}

			MethodInfo? declared = effective.ReflectedType == effective.DeclaringType
				? effective
				: AccessTools.DeclaredMethod(effective.DeclaringType, "OnPlay", _onPlaySignature);
			if (declared == null)
			{
				return;
			}

			lock (_lock)
			{
				if (!_patchedMethods.Add(declared))
				{
					return;
				}
			}

			_harmony ??= new Harmony(CardEditorMod.HarmonyId);
			_harmony.Patch(declared, postfix: new HarmonyMethod(AccessTools.Method(typeof(CardEditorOverrideOnPlayPatcher), nameof(OnPlayPostfix))));
			Log.Info($"[CardEditor] Override rows now run with the play for OnPlay declared on {declared.DeclaringType?.Name}.");
		}
		catch (Exception ex)
		{
			// Patch failure is non-fatal: the AfterCardPlayed hook's Combined phase remains the
			// fallback (legacy after-play timing).
			Log.Warn($"[CardEditor] Failed to patch OnPlay for {cardType.FullName}: {ex}");
		}
	}

	internal static MethodInfo? GetEffectiveOnPlay(Type cardType)
		=> _effectiveOnPlayByType.GetOrAdd(cardType, static t => AccessTools.Method(t, "OnPlay", _onPlaySignature));

	// Harmony binds choiceContext/cardPlay BY NAME — all vanilla CardModel OnPlay declarations
	// use these canonical parameter names.
	public static void OnPlayPostfix(CardModel __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, MethodBase __originalMethod, ref Task __result)
	{
		if (__result == null || __instance == null || cardPlay == null)
		{
			return;
		}
		// Reflective invocations by the mod itself (auto-action "vanilla OnPlay" payloads,
		// borrowed effect sources) must not compose rows — the payload row's own play already
		// runs them, and the borrowed-source path passes the CREATED card's real CardPlay.
		if (CardEditorReflectiveOnPlayGuard.IsActive)
		{
			return;
		}
		// Created cards have their own OnPlay postfix (CardEditorMod); cardPlay.Card check is
		// belt-and-braces against any path invoking a vanilla card's OnPlay under a created
		// card's play.
		if (__instance is CardEditorCreatedCardBase || cardPlay.Card is CardEditorCreatedCardBase)
		{
			return;
		}
		// Most-derived gate: when the patched declaration is a base/shared one, fire only for
		// instances whose EFFECTIVE OnPlay is that declaration (also keeps any future
		// base.OnPlay() call from running rows mid-derived-OnPlay).
		MethodInfo? effective = GetEffectiveOnPlay(__instance.GetType());
		if (effective == null || effective.DeclaringType != __originalMethod.DeclaringType)
		{
			return;
		}

		CombatState? combatState = cardPlay.Card?.GetConcreteCombatState();
		if (combatState == null)
		{
			return;
		}
		if (!CardEditorOverrides.HasAnyOverrides
			&& !CardEditorTemporaryExtraEffectController.HasAny(combatState)
			&& !CardEditorTemporaryEnchantmentController.HasAny(combatState))
		{
			return;
		}

		__result = RunImmediateRowsAfterOnPlay(__result, combatState, choiceContext, cardPlay);
	}

	private static async Task RunImmediateRowsAfterOnPlay(Task original, CombatState combatState, PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await original;
		// Intended parity deltas vs the legacy after-play timing (both match created cards and
		// printed-card semantics): (1) if these rows kill the OWNER, the vanilla wrapper's
		// IsDead gate skips Enchantment/Affliction OnPlay and Hook.AfterCardPlayed, so the
		// reactions phase (power grants, Fatal, expiry) never runs for that play — exactly as
		// vanilla skips all after-play listeners when the owner dies mid-OnPlay. (2) An
		// immediate row granted TO this card by an AfterCardPlayed listener can no longer run
		// same-play (the immediate phase has already passed); it first applies on the next play.
		// Mark BEFORE running rows: if a row throws mid-way, the AfterCardPlayed hook must not
		// re-run the immediate phase — half-executed rows running twice is worse than the
		// remainder being skipped once.
		CardEditorExtraEffects.MarkImmediateRowsRanDuringOnPlay(cardPlay);
		try
		{
			await CardEditorExtraEffects.RunAfterCardPlayed(combatState, choiceContext, cardPlay, CardEditorExtraEffects.CardPlayHookPhase.ImmediateRowsOnly);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Override OnPlay rows failed for {cardPlay?.Card?.Id}: {ex}");
		}
	}
}
