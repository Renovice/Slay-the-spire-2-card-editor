using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;

namespace SlayTheSpire2Mod.CardEditor;

internal readonly struct CardEditorHardcodedPowerAmountSpec
{
	public ModelId PowerId { get; }
	public int DefaultAmount { get; }
	public string? LabelOverride { get; }

	public CardEditorHardcodedPowerAmountSpec(ModelId powerId, int defaultAmount, string? labelOverride = null)
	{
		PowerId = powerId;
		DefaultAmount = defaultAmount;
		LabelOverride = labelOverride;
	}
}

internal static class CardEditorHardcodedPowerAmounts
{
	private static readonly IReadOnlyDictionary<ModelId, CardEditorHardcodedPowerAmountSpec[]> _map =
		new Dictionary<ModelId, CardEditorHardcodedPowerAmountSpec[]>
		{
			{ ModelDb.GetId<Aggression>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<AggressionPower>(), 1) } },
			{ ModelDb.GetId<Barricade>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<BarricadePower>(), 1) } },
			{ ModelDb.GetId<BeaconOfHope>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<BeaconOfHopePower>(), 1) } },
			{ ModelDb.GetId<Calamity>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<CalamityPower>(), 1) } },
			{ ModelDb.GetId<DarkEmbrace>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<DarkEmbracePower>(), 1) } },
			{ ModelDb.GetId<Eidolon>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<IntangiblePower>(), 1, "Intangible") } },
			{ ModelDb.GetId<FanOfKnives>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<FanOfKnivesPower>(), 1) } },
			{ ModelDb.GetId<Flanking>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<FlankingPower>(), 2) } },
			{ ModelDb.GetId<ForbiddenGrimoire>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<ForbiddenGrimoirePower>(), 1) } },
			{ ModelDb.GetId<HammerTime>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<HammerTimePower>(), 1) } },
			{ ModelDb.GetId<Hellraiser>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<HellraiserPower>(), 1) } },
			{ ModelDb.GetId<HelloWorld>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<HelloWorldPower>(), 1) } },
			{ ModelDb.GetId<InfiniteBlades>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<InfiniteBladesPower>(), 1) } },
			{ ModelDb.GetId<Intercept>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<CoveredPower>(), 1) } },
			{ ModelDb.GetId<Juggling>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<JugglingPower>(), 1) } },
			{ ModelDb.GetId<MadScience>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<ImprovementPower>(), 1) } },
			{ ModelDb.GetId<MasterPlanner>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<MasterPlannerPower>(), 1) } },
			{ ModelDb.GetId<Mayhem>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<MayhemPower>(), 1) } },
			{ ModelDb.GetId<Monologue>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<MonologuePower>(), 1) } },
			{ ModelDb.GetId<NecroMastery>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<NecroMasteryPower>(), 1) } },
			{ ModelDb.GetId<Nightmare>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<NightmarePower>(), 3, "Nightmare Copies") } },
			{ ModelDb.GetId<Nostalgia>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<NostalgiaPower>(), 1) } },
			{ ModelDb.GetId<Pounce>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<FreeSkillPower>(), 1) } },
			{ ModelDb.GetId<Predator>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<DrawCardsNextTurnPower>(), 2, "Draw Next Turn") } },
			{ ModelDb.GetId<ReaperForm>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<ReaperFormPower>(), 1) } },
			{ ModelDb.GetId<Rebound>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<ReboundPower>(), 1) } },
			{ ModelDb.GetId<SeekingEdge>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<SeekingEdgePower>(), 1) } },
			{ ModelDb.GetId<ShadowStep>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<ShadowStepPower>(), 1) } },
			{ ModelDb.GetId<Stratagem>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<StratagemPower>(), 1) } },
			{ ModelDb.GetId<Subroutine>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<SubroutinePower>(), 1) } },
			{ ModelDb.GetId<Synthesis>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<FreePowerPower>(), 1) } },
			{ ModelDb.GetId<Tank>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<TankPower>(), 1) } },
			{ ModelDb.GetId<TagTeam>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<TagTeamPower>(), 1) } },
			{ ModelDb.GetId<TheGambit>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<TheGambitPower>(), 1) } },
			{ ModelDb.GetId<TheHunt>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<TheHuntPower>(), 1) } },
			{ ModelDb.GetId<ToolsOfTheTrade>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<ToolsOfTheTradePower>(), 1) } },
			{ ModelDb.GetId<TrashToTreasure>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<TrashToTreasurePower>(), 1) } },
			{ ModelDb.GetId<Tyranny>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<TyrannyPower>(), 1) } },
			{ ModelDb.GetId<Unmovable>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<UnmovablePower>(), 1) } },
			{ ModelDb.GetId<Unrelenting>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<FreeAttackPower>(), 1) } },
			{ ModelDb.GetId<Veilpiercer>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<VeilpiercerPower>(), 1) } },

			{ ModelDb.GetId<FranticEscape>(), new[] { new CardEditorHardcodedPowerAmountSpec(ModelDb.GetId<SandpitPower>(), 1, "Sandpit Reduction") } }
		};

	public static IReadOnlyList<CardEditorHardcodedPowerAmountSpec> Get(ModelId cardId)
	{
		if (_map.TryGetValue(cardId, out CardEditorHardcodedPowerAmountSpec[]? specs) && specs != null)
		{
			return specs;
		}
		return Array.Empty<CardEditorHardcodedPowerAmountSpec>();
	}
}
