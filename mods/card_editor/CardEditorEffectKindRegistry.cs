using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Logging;

namespace SlayTheSpire2Mod.CardEditor;

// Card Engine P0: ONE declarative capability profile per effect kind, replacing ~10 hand-maintained
// predicate lists that had drifted apart (see CARD_ENGINE_PLAN.md). Until the legacy predicates are
// converted into delegations, this registry is AUDITED against them at boot (AuditRegistryParity):
// any disagreement is a startup warning, so the two sources of truth cannot drift silently.
//
// Capability semantics (transcribed from the live predicates 2026-07-17):
//   Grantable        = SupportsGrantToCard          (CardEditorExtraEffects.cs:5591)
//   AsPower          = SupportsAsPower              (:5535)
//   Repeatable       = SupportsRepeat               (:24825)
//   DynamicAmount    = SupportsAppliedEffectRowAmountSource (:5750)
//   HistoryScalable  = SupportsHistoryScaling       (:33112)
//   PublishesCardsUi = offered as a "Selected Row" source by the editor (CanPublishCardSelection)
//   PublishesCardsRuntime = actually reports selected/created cards into the execution context
//   ConsumesCards    = honors CardSelectionMode.SelectedByEffect via the shared candidate funnel
//   Passive          = no-op in the executor; evaluated by hook/render/hover patches
//   MetaWrapper      = wraps/gates OTHER rows rather than doing work itself
[Flags]
internal enum EffectCaps
{
	None = 0,
	Grantable = 1 << 0,
	AsPower = 1 << 1,
	Repeatable = 1 << 2,
	DynamicAmount = 1 << 3,
	HistoryScalable = 1 << 4,
	PublishesCardsUi = 1 << 5,
	PublishesCardsRuntime = 1 << 6,
	ConsumesCards = 1 << 7,
	Passive = 1 << 8,
	MetaWrapper = 1 << 9,
}

// How the executor actually uses effect.Target - the truth behind the target dropdown.
//   IgnoresTarget     = executes on the owner/host regardless of Target (dropdown options beyond
//                       Self are cosmetic today; candidates for gating or routing in P4).
//   ResolvesCreatures = resolves creatures via ResolveTargets (enemies/self/allies as creatures).
//   ResolvesPlayers   = resolves players via ResolveTargetPlayers (draw/energy/stars/gold).
internal enum EffectTargetSemantics
{
	IgnoresTarget,
	ResolvesCreatures,
	ResolvesPlayers,
}

internal sealed record EffectKindProfile(CardExtraEffectKind Kind, EffectCaps Caps, EffectTargetSemantics Targets);

internal static class CardEditorEffectKindRegistry
{
	// Shorthand for the table below.
	private const EffectCaps G = EffectCaps.Grantable;
	private const EffectCaps P = EffectCaps.AsPower;
	private const EffectCaps R = EffectCaps.Repeatable;
	private const EffectCaps D = EffectCaps.DynamicAmount;
	private const EffectCaps H = EffectCaps.HistoryScalable;
	private const EffectCaps S = EffectCaps.PublishesCardsUi;
	private const EffectCaps s = EffectCaps.PublishesCardsRuntime;
	private const EffectCaps C = EffectCaps.ConsumesCards;
	private const EffectCaps X = EffectCaps.Passive;
	private const EffectCaps M = EffectCaps.MetaWrapper;

	private const EffectTargetSemantics Ignores = EffectTargetSemantics.IgnoresTarget;
	private const EffectTargetSemantics Creatures = EffectTargetSemantics.ResolvesCreatures;
	private const EffectTargetSemantics Players = EffectTargetSemantics.ResolvesPlayers;

	private static readonly Dictionary<CardExtraEffectKind, EffectKindProfile> _table = Build();

	public static EffectKindProfile Get(CardExtraEffectKind kind)
		=> _table.TryGetValue(kind, out EffectKindProfile? profile)
			? profile
			: new EffectKindProfile(kind, EffectCaps.None, Ignores);

	public static bool Has(CardExtraEffectKind kind, EffectCaps caps)
		=> (Get(kind).Caps & caps) == caps;

	public static EffectTargetSemantics TargetSemanticsOf(CardExtraEffectKind kind)
		=> Get(kind).Targets;

	private static Dictionary<CardExtraEffectKind, EffectKindProfile> Build()
	{
		Dictionary<CardExtraEffectKind, EffectKindProfile> table = new();
		void Add(CardExtraEffectKind kind, EffectCaps caps, EffectTargetSemantics targets)
			=> table[kind] = new EffectKindProfile(kind, caps, targets);

		// Creature-targeted numeric/status effects.
		Add(CardExtraEffectKind.GainBlock, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.DealDamage, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.Heal, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.LoseHp, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.RemoveBlock, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.GainStrength, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.LoseStrength, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.GainDexterity, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.LoseDexterity, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.GainFocus, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.LoseFocus, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.ApplyWeak, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.RemoveWeak, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.ApplyFrail, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.RemoveFrail, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.ApplyVulnerable, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.RemoveVulnerable, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.ApplyPoison, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.RemovePoison, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.ApplyDoom, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.RemoveDoom, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.ApplyConstrict, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.RemoveConstrict, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.GainArtifact, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.RemoveArtifact, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.GainThorns, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.RemoveThorns, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.GainRegen, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.RemoveRegen, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.GainPlating, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.RemovePlating, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.GainIntangible, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.RemoveIntangible, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.GainBuffer, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.RemoveBuffer, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.GainVigor, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.RemoveVigor, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.GainBlur, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.RemoveBlur, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.GainRitual, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.RemoveRitual, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.ApplyPower, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.RemovePower, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.ModifyActivePower, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.GainStatusEqualToStatus, G | P | R | D | H, Creatures);
		Add(CardExtraEffectKind.GainMaxHp, G | P | R | D | H, Ignores);
		Add(CardExtraEffectKind.LoseMaxHp, G | P | R | D | H, Ignores);
		Add(CardExtraEffectKind.MultiplyStatStatus, G | P | H, Creatures);
		Add(CardExtraEffectKind.CleanseDebuffs, G | P | R | H, Creatures);
		Add(CardExtraEffectKind.CleanseBuffs, G | P | R | H, Creatures);
		Add(CardExtraEffectKind.CopyDebuffs, G | P | R | H, Creatures);
		Add(CardExtraEffectKind.CopyBuffs, G | P | R | H, Creatures);
		// ApplyMarked: DynamicAmount missing from the live whitelist (flagged oversight; registry
		// mirrors CURRENT truth - widening it later is a deliberate one-line change here + there).
		Add(CardExtraEffectKind.ApplyMarked, G | P | R | H, Creatures);
		Add(CardExtraEffectKind.CreatureCommand, G | P | R | H, Creatures);
		Add(CardExtraEffectKind.OstyAction, G | P | R | H, Creatures);

		// Player-resolved resource/draw effects.
		Add(CardExtraEffectKind.DrawCards, G | P | R | D | H | S | s, Players);
		Add(CardExtraEffectKind.DrawCardsThatCostLess, D | S | s, Players);
		Add(CardExtraEffectKind.DrawUntilHandSize, G | P | R | H | s, Players);
		Add(CardExtraEffectKind.DrawAndCheck, G | P | R | H | s, Players);
		Add(CardExtraEffectKind.GainEnergy, G | P | R | D | H, Players);
		Add(CardExtraEffectKind.LoseEnergy, G | P | R | D | H, Players);
		Add(CardExtraEffectKind.GainStars, G | P | R | D | H, Players);
		Add(CardExtraEffectKind.LoseStars, G | P | R | D | H, Players);
		Add(CardExtraEffectKind.GainGold, G | P | R | D | H, Players);
		Add(CardExtraEffectKind.LoseGold, G | P | R | D | H, Players);
		Add(CardExtraEffectKind.GrantKeywordToPile, D | H | S | C, Players);
		Add(CardExtraEffectKind.GeneratedCardsUpgraded, H, Players);
		Add(CardExtraEffectKind.CardsInPileUpgradedAura, H, Players);

		// Owner-scoped one-shots (target dropdown is cosmetic today).
		Add(CardExtraEffectKind.Summon, G | P | R | D | H, Ignores);
		Add(CardExtraEffectKind.Forge, G | P | R | D | H, Ignores);
		Add(CardExtraEffectKind.CreateRandomPotion, G | P | R | D | H, Ignores);
		Add(CardExtraEffectKind.AddCardReward, G | P | D | H, Ignores);
		Add(CardExtraEffectKind.AddRelicReward, G | P | D | H, Ignores);
		Add(CardExtraEffectKind.EndTurn, G, Ignores);

		// Orb effects (owner orb queue; OrbAction TriggerPassive+Target is the one exception).
		Add(CardExtraEffectKind.ChannelLightning, G | P | R | H, Ignores);
		Add(CardExtraEffectKind.ChannelFrost, G | P | R | H, Ignores);
		Add(CardExtraEffectKind.ChannelDark, G | P | R | H, Ignores);
		Add(CardExtraEffectKind.ChannelPlasma, G | P | R | H, Ignores);
		Add(CardExtraEffectKind.ChannelGlass, G | P | R | H, Ignores);
		Add(CardExtraEffectKind.ChannelRandomOrb, G | P | R | H, Ignores);
		Add(CardExtraEffectKind.GainOrbSlots, G | P | R | H, Ignores);
		Add(CardExtraEffectKind.LoseOrbSlots, G | P | R | H, Ignores);
		Add(CardExtraEffectKind.OrbAction, G | P | R | H, Ignores);
		Add(CardExtraEffectKind.EvokeOrbs, G | P | R | H, Ignores);

		// Card generation (runtime publishers - the UI does not offer them as sources yet; P2).
		Add(CardExtraEffectKind.AddRandomCardToHand, G | P | D | H | s, Ignores);
		Add(CardExtraEffectKind.ChooseOneOfThreeCardsToHand, G | P | D | H | s, Ignores);
		Add(CardExtraEffectKind.AddSpecificCardToHand, G | P | D | H | s, Ignores);
		Add(CardExtraEffectKind.AddCopyOfThisCard, G | P | D | H | s, Ignores);
		Add(CardExtraEffectKind.AddExactCopyOfThisCardToDeck, P | R | D | H | s, Ignores);
		Add(CardExtraEffectKind.PlayRandomGeneratedCard, G | P | D | H | s, Ignores);
		Add(CardExtraEffectKind.FetchSpecificCardToHand, G | P | D | H | S, Ignores);
		Add(CardExtraEffectKind.LinkedCardAction, P | D | H | s, Ignores);

		// Pile / card actions (selection publishers + consumers; not grantable today).
		Add(CardExtraEffectKind.MoveCardsBetweenPiles, P | D | H | S | C, Ignores);
		Add(CardExtraEffectKind.PlayCardFromPile, P | D | H | S | C, Ignores);
		Add(CardExtraEffectKind.DiscardCards, P | D | H | S | C, Ignores);
		Add(CardExtraEffectKind.ExhaustCards, P | D | H | S | C, Ignores);
		Add(CardExtraEffectKind.UpgradeCardsInPile, P | D | H | S | C, Ignores);
		Add(CardExtraEffectKind.SelectCardsFromPile, P | D | H | S | C, Ignores);
		Add(CardExtraEffectKind.ConsumeCardValue, P | D | H | S | C, Ignores);
		Add(CardExtraEffectKind.DelayedPileAction, P | R | D | H | S | C, Ignores);
		Add(CardExtraEffectKind.TransformCards, G | P | R | D | H | S | C, Ignores);
		Add(CardExtraEffectKind.CopyCardsFromPileToDeck, P | R | D | H | S | C, Ignores);
		Add(CardExtraEffectKind.CopyExactCardsFromPileToDeck, P | R | D | H | S | C, Ignores);
		Add(CardExtraEffectKind.RemoveCardsFromDeck, P | R | D | H | S | C, Ignores);
		Add(CardExtraEffectKind.UpgradeDeckCards, P | D | H | S, Ignores);
		Add(CardExtraEffectKind.GrantReplay, G | P | R | H | S | C, Ignores);
		Add(CardExtraEffectKind.EnchantCard, G | P | H | S, Ignores);
		Add(CardExtraEffectKind.ShuffleDrawPile, P, Ignores);

		// Cost modifiers (owner/host-scoped).
		Add(CardExtraEffectKind.CardCostsLess, G | P | R | H, Ignores);
		Add(CardExtraEffectKind.CardStarCostsLess, G | P | R | H, Ignores);
		Add(CardExtraEffectKind.CardTypeCostsLess, G | P | R | H, Ignores);
		Add(CardExtraEffectKind.CardTypeStarCostsLess, G | P | R | H, Ignores);
		Add(CardExtraEffectKind.DrawnCardsCostLess, G | P | R, Ignores);
		Add(CardExtraEffectKind.GeneratedCardsCostLess, G | P | R, Ignores);
		Add(CardExtraEffectKind.CreatedCardsCostLess, EffectCaps.None, Ignores);
		Add(CardExtraEffectKind.CreatedCardsUpgraded, EffectCaps.None, Ignores);

		// Play modifiers / passive markers (patch-evaluated; executor no-ops).
		Add(CardExtraEffectKind.IgnoreBlock, G | R | H, Ignores);
		Add(CardExtraEffectKind.IgnoreDamageModifiers, G | R | H, Ignores);
		Add(CardExtraEffectKind.IgnoreDamageCaps, G | R | H, Ignores);
		Add(CardExtraEffectKind.IgnoreDamageNegation, G | R | H, Ignores);
		Add(CardExtraEffectKind.IgnoreEnemyDamageReductions, G | R | H, Ignores);
		Add(CardExtraEffectKind.HitsAllEnemies, H, Ignores);
		Add(CardExtraEffectKind.CardDealsExtraDamage, G | D | H, Ignores);
		Add(CardExtraEffectKind.DoesNotConsumeVigor, G | H, Ignores);
		Add(CardExtraEffectKind.ResultPileOverride, X, Ignores);
		Add(CardExtraEffectKind.PlayPermission, H | X, Ignores);
		Add(CardExtraEffectKind.PlayPrevention, H | X, Ignores);
		Add(CardExtraEffectKind.ConditionalGlow, H | X, Ignores);
		Add(CardExtraEffectKind.Quest, H | X, Ignores);
		Add(CardExtraEffectKind.HoverPreview, H | X, Ignores);
		Add(CardExtraEffectKind.DynamicIdentity, H | X, Ignores);
		Add(CardExtraEffectKind.DisplayNumber, H | X, Ignores);

		// Self-scaling / mutation family.
		Add(CardExtraEffectKind.SelfScaling, G | P | D | H | s, Ignores);
		Add(CardExtraEffectKind.PersistentSelfScaling, G | P | D | H | s, Ignores);
		Add(CardExtraEffectKind.TargetCardMutation, G | P | D | H | S | C, Ignores);
		Add(CardExtraEffectKind.PersistentTargetCardMutation, G | P | D | H | S | C, Ignores);
		Add(CardExtraEffectKind.StatefulTransform, G | P | H, Ignores);
		Add(CardExtraEffectKind.ScalingStage, G | H, Ignores);

		// Auto-actions + meta wrappers.
		Add(CardExtraEffectKind.AutoPlaySelfFromPile, P | D, Ignores);
		Add(CardExtraEffectKind.AutoDrawSelfFromPile, P | D, Ignores);
		Add(CardExtraEffectKind.ConditionalAutoPlayFromPile, P | D, Ignores);
		Add(CardExtraEffectKind.ConditionalAutoDrawFromPile, P | D, Ignores);
		Add(CardExtraEffectKind.ConditionalAutoRunEffects, P | D | M, Ignores);
		Add(CardExtraEffectKind.EffectLimit, M, Ignores);
		Add(CardExtraEffectKind.CountdownEffect, R | M, Ignores);
		Add(CardExtraEffectKind.RunEffectSourceCard, G | P | R | H | S, Ignores);
		Add(CardExtraEffectKind.ChooseOneEffectSource, G | P | H | S | s | C, Ignores);

		return table;
	}

	// Boot audits: coverage (every enum member has a profile) + parity (registry flags agree with
	// the live legacy predicates for the five mechanically-checkable capabilities).
	public static int RunAudits()
	{
		int issues = 0;
		List<string> missing = new();
		List<string> mismatches = new();

		foreach (CardExtraEffectKind kind in Enum.GetValues<CardExtraEffectKind>())
		{
			if (!_table.TryGetValue(kind, out EffectKindProfile? profile))
			{
				missing.Add(kind.ToString());
				continue;
			}

			CheckParity(kind, EffectCaps.Grantable, CardEditorExtraEffects.SupportsGrantToCard(kind), mismatches);
			CheckParity(kind, EffectCaps.AsPower, CardEditorExtraEffects.SupportsAsPower(kind), mismatches);
			CheckParity(kind, EffectCaps.Repeatable, CardEditorExtraEffects.SupportsRepeat(kind), mismatches);
			CheckParity(kind, EffectCaps.DynamicAmount, CardEditorExtraEffects.SupportsAppliedEffectRowAmountSource(kind), mismatches);
			CheckParity(kind, EffectCaps.HistoryScalable, CardEditorExtraEffects.SupportsHistoryScaling(kind), mismatches);
		}

		if (missing.Count > 0)
		{
			issues += missing.Count;
			Log.Warn($"[CardEditor] EffectKindRegistry: {missing.Count} kinds have NO profile: {string.Join(", ", missing)}");
		}

		if (mismatches.Count > 0)
		{
			issues += mismatches.Count;
			Log.Warn(
				$"[CardEditor] EffectKindRegistry parity: {mismatches.Count} disagreements with legacy predicates "
				+ "(registry vs live code):\n  " + string.Join("\n  ", mismatches));
		}

		return issues;
	}

	private static void CheckParity(CardExtraEffectKind kind, EffectCaps cap, bool legacy, List<string> mismatches)
	{
		bool registry = Has(kind, cap);
		if (registry != legacy)
		{
			mismatches.Add($"{kind}.{cap}: registry={registry} legacy={legacy}");
		}
	}
}
