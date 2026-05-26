using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using PowerCmd = SlayTheSpire2Mod.CardEditor.CardEditorPowerCmdCompat;

namespace SlayTheSpire2Mod.CardEditor;

internal sealed class CardEditorCountdownEffectPower : PowerModel
{
	private sealed class PendingPayloadScope(PayloadSpec? previous) : IDisposable
	{
		public void Dispose()
		{
			_pendingPayload.Value = previous;
		}
	}

	internal sealed class PayloadEntry(CardExtraEffect effect, Creature? lockedTarget)
	{
		public CardExtraEffect Effect { get; } = effect;
		public Creature? LockedTarget { get; } = lockedTarget;

		public PayloadEntry Clone()
			=> new PayloadEntry(CardEditorExtraEffects.CloneEffect(Effect), LockedTarget);
	}

	internal sealed class PayloadSpec(
		CardModel card,
		CardModel? sourceCardInstance,
		IReadOnlyList<PayloadEntry> payloads,
		IReadOnlyDictionary<string, List<CardModel>> selectedCardsByEffectId,
		ResourceInfo resources,
		PileType resultPile,
		CardExtraEffectTiming timing,
		Creature? executionHost,
		object? useLimitSourceInstance,
		int triggerEventAmount,
		string? payloadDescription)
	{
		public CardModel Card { get; } = card;
		public CardModel? SourceCardInstance { get; } = sourceCardInstance;
		public IReadOnlyList<PayloadEntry> Payloads { get; } = payloads?.Where(p => p?.Effect != null).ToList() ?? new List<PayloadEntry>();
		public Dictionary<string, List<CardModel>> SelectedCardsByEffectId { get; } = CardEditorEffectExecutionAmountContext.CloneSelectedCardsSnapshot(selectedCardsByEffectId, cloneCards: true);
		public ResourceInfo Resources { get; } = resources;
		public PileType ResultPile { get; } = resultPile;
		public CardExtraEffectTiming Timing { get; } = timing;
		public Creature? ExecutionHost { get; } = executionHost;
		public object? UseLimitSourceInstance { get; } = useLimitSourceInstance;
		public int TriggerEventAmount { get; } = Math.Max(0, triggerEventAmount);
		public string? PayloadDescription { get; } = payloadDescription;

		public PayloadSpec Clone()
			=> new PayloadSpec(
				Card,
				SourceCardInstance,
				Payloads.Select(p => p.Clone()).ToList(),
				SelectedCardsByEffectId,
				Resources,
				ResultPile,
				Timing,
				ExecutionHost,
				UseLimitSourceInstance,
				TriggerEventAmount,
				PayloadDescription);
	}

	private static readonly AsyncLocal<PayloadSpec?> _pendingPayload = new();

	private PayloadSpec? _payload;

	public override LocString Title
		=> CardEditorCustomStatusPower.CreateRuntimeLocString("CARD_EDITOR.COUNTDOWN_POWER_TITLE.", CardEditorLoc.T("power.countdownEffect.title", "Countdown Effect"));

	public override LocString Description
		=> CardEditorCustomStatusPower.CreateRuntimeLocString("CARD_EDITOR.COUNTDOWN_POWER_DESCRIPTION.", BuildDescription());

	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

	public override bool ShouldPlayVfx => false;

	public override int DisplayAmount => Math.Max(0, Amount);

	public static IDisposable PushPendingPayload(PayloadSpec payload)
	{
		PayloadSpec? previous = _pendingPayload.Value;
		_pendingPayload.Value = payload;
		return new PendingPayloadScope(previous);
	}

	public override Task BeforeApplied(Creature target, decimal amount, Creature? applier, CardModel? cardSource)
	{
		_payload = _pendingPayload.Value?.Clone();
		return Task.CompletedTask;
	}

	protected override void DeepCloneFields()
	{
		PayloadSpec? payload = _payload?.Clone();
		base.DeepCloneFields();
		_payload = payload;
	}

	public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
	{
		if (!MatchesBoundary(side, isStart: false))
		{
			return;
		}

		await TickOrFire(choiceContext);
	}

	internal async Task RunAfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		CombatSide side = player?.Creature?.Side ?? CombatSide.Player;
		if (choiceContext == null || !MatchesBoundary(side, isStart: true))
		{
			return;
		}

		await TickOrFire(choiceContext);
	}

	internal async Task RunAfterSideTurnStart(CombatSide side, CombatState combatState)
	{
		if (combatState == null || !MatchesBoundary(side, isStart: true))
		{
			return;
		}

		ulong? netId = LocalContext.NetId;
		if (!netId.HasValue)
		{
			return;
		}

		try
		{
			HookPlayerChoiceContext choiceContext = Owner?.Player != null
				? new HookPlayerChoiceContext(Owner.Player, netId.Value, GameActionType.Combat)
				: CardEditorCombatStateCompat.CreateHookPlayerChoiceContext(this, netId.Value, combatState, GameActionType.Combat);
			Task task = TickOrFire(choiceContext);
			bool completed = await choiceContext.AssignTaskAndWaitForPauseOrCompletion(task);
			if (!completed && choiceContext.GameAction != null)
			{
				await choiceContext.GameAction.CompletionTask;
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Countdown start-of-turn payload failed: {ex}");
		}
	}

	internal Texture2D? ResolveIcon(bool bigIcon)
	{
		return TryResolveBaseGameIcon<TheBombPower>(bigIcon)
			?? TryResolveBaseGameIcon<CountdownPower>(bigIcon)
			?? TryResolveBaseGameIcon<ArtifactPower>(bigIcon);
	}

	private async Task TickOrFire(PlayerChoiceContext choiceContext)
	{
		CombatState? combatState = this.GetConcreteCombatState();
		if (_payload == null || combatState == null)
		{
			await PowerCmd.Remove(this);
			return;
		}

		if (Amount > 1)
		{
			Flash();
			await PowerCmd.ModifyAmount(this, -1, Owner, _payload.SourceCardInstance ?? _payload.Card, silent: true);
			return;
		}

		Flash();
		await CardEditorExtraEffects.ExecuteCountdownPayloads(combatState, choiceContext, _payload);
		await PowerCmd.Remove(this);
	}

	private bool MatchesBoundary(CombatSide side, bool isStart)
	{
		if (_payload == null || side == CombatSide.None)
		{
			return false;
		}

		CombatSide ownerSide = Owner?.Side ?? CombatSide.Player;
		return _payload.Timing switch
		{
			CardExtraEffectTiming.StartOfTurn => isStart && side == ownerSide,
			CardExtraEffectTiming.StartOfAnyTurn => isStart,
			CardExtraEffectTiming.StartOfEnemyTurn => isStart && side != ownerSide,
			CardExtraEffectTiming.EndOfTurn or CardExtraEffectTiming.EndOfThisTurn => !isStart && side == ownerSide,
			CardExtraEffectTiming.EndOfAnyTurn or CardExtraEffectTiming.EndOfThisAnyTurn => !isStart,
			CardExtraEffectTiming.EndOfEnemyTurn => !isStart && side != ownerSide,
			_ => false
		};
	}

	private string BuildDescription()
	{
		string payload = string.IsNullOrWhiteSpace(_payload?.PayloadDescription)
			? CardEditorLoc.T("power.countdownEffect.payload", "perform the selected effects")
			: _payload!.PayloadDescription!;
		return CardEditorLoc.F(
			"power.countdownEffect.description",
			$"When this countdown reaches 0, {payload}.",
			("Payload", payload));
	}

	private static Texture2D? TryResolveBaseGameIcon<T>(bool bigIcon)
		where T : PowerModel
	{
		try
		{
			PowerModel? power = ModelDb.Power<T>();
			return bigIcon ? power?.BigIcon : power?.Icon;
		}
		catch
		{
			return null;
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterPlayerTurnStart))]
internal static class Hook_AfterPlayerTurnStart_CardEditorCountdownEffectPower_Patch
{
	public static void Postfix(CombatState combatState, PlayerChoiceContext choiceContext, Player player, ref Task __result)
	{
		if (__result == null || combatState == null || choiceContext == null || player?.Creature == null)
		{
			return;
		}

		__result = RunAfter(__result, choiceContext, player);
	}

	private static async Task RunAfter(Task original, PlayerChoiceContext choiceContext, Player player)
	{
		await original;

		try
		{
			List<CardEditorCountdownEffectPower> powers = player.Creature.Powers?
				.OfType<CardEditorCountdownEffectPower>()
				.ToList() ?? new List<CardEditorCountdownEffectPower>();
			foreach (CardEditorCountdownEffectPower power in powers)
			{
				await power.RunAfterPlayerTurnStart(choiceContext, player);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Countdown player-start payload failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterSideTurnStart))]
internal static class Hook_AfterSideTurnStart_CardEditorCountdownEffectPower_Patch
{
	public static void Postfix(CombatState combatState, CombatSide side, ref Task __result)
	{
		if (__result == null || combatState == null || side == CombatSide.None || side == CombatSide.Player)
		{
			return;
		}

		__result = RunAfter(__result, combatState, side);
	}

	private static async Task RunAfter(Task original, CombatState combatState, CombatSide side)
	{
		await original;

		try
		{
			foreach (Player player in combatState.Players)
			{
				Creature? creature = player?.Creature;
				if (creature == null)
				{
					continue;
				}

				List<CardEditorCountdownEffectPower> powers = creature.Powers?
					.OfType<CardEditorCountdownEffectPower>()
					.ToList() ?? new List<CardEditorCountdownEffectPower>();
				foreach (CardEditorCountdownEffectPower power in powers)
				{
					await power.RunAfterSideTurnStart(side, combatState);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Countdown side-start payload failed: {ex}");
		}
	}
}

[HarmonyPatch]
internal static class CardEditorCountdownEffectPowerPatches
{
	[HarmonyPatch(typeof(PowerModel), "get_Icon")]
	[HarmonyPrefix]
	private static bool PowerModel_get_Icon_Prefix(PowerModel __instance, ref Texture2D? __result)
	{
		if (__instance is not CardEditorCountdownEffectPower countdown)
		{
			return true;
		}

		__result = countdown.ResolveIcon(bigIcon: false);
		return false;
	}

	[HarmonyPatch(typeof(PowerModel), "get_BigIcon")]
	[HarmonyPrefix]
	private static bool PowerModel_get_BigIcon_Prefix(PowerModel __instance, ref Texture2D? __result)
	{
		if (__instance is not CardEditorCountdownEffectPower countdown)
		{
			return true;
		}

		__result = countdown.ResolveIcon(bigIcon: true);
		return false;
	}
}
