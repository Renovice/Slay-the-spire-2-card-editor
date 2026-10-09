using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Monsters.Mocks;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Quality;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2Mod.CardEditor;
using System.Reflection;
using System.Runtime.CompilerServices;
using GamePowerCmd = MegaCrit.Sts2.Core.Commands.PowerCmd;

namespace CardEditor.TestHarness;

internal static class Program
{
	private sealed record Fixture(RunState Run, CombatState Combat, Player Player, BlockingPlayerChoiceContext Choices);
	private sealed record TestCase(string Name, Func<Task> Body);

	private static readonly ResourceInfo _zeroResources = new()
	{
		EnergySpent = 0,
		EnergyValue = 0,
		StarsSpent = 0,
		StarValue = 0
	};

	private static int _fixtureNumber;

	private static async Task<int> Main()
	{
		try
		{
			InitializeHeadlessRuntime();
		}
		catch (Exception exception)
		{
			Console.Error.WriteLine("FAIL headless beta runtime initialization");
			Console.Error.WriteLine(exception);
			return 1;
		}

		TestCase[] tests =
		[
			new("Move cards between piles: complete pile/position matrix", TestMoveCardsBetweenPiles),
			new("Move cards between piles: final positional trigger only", TestMoveCardsFinalPositionTrigger),
			new("Manual Exhaust: headless Choose selection", TestManualExhaust),
			new("Transform cards: type filter", TestTransformByType),
			new("Transform cards: vanilla tag filter", TestTransformByTag),
			new("Reduce Cost -> This Card -> all public Whenever dispatches", TestTriggeredThisCardCostReduction),
			new("Reduce Cost -> This Card -> installed Whenever event power", TestTriggeredThisCardCostReductionWheneverPower),
			new("Production game hook -> Whenever event cost reduction", TestProductionHookWheneverCostReduction),
			new("Resources -> After Death -> player/enemy power hosts", TestAfterDeathPowerTrigger),
			new("Production game hook -> After Death power dispatch", TestProductionHookAfterDeath),
			new("Run Effect Source -> Regent block and Osty cards", TestRunEffectSourceRegentCards),
			new("Production game hook -> reported Effect Source cards", TestProductionHookRunEffectSourceCards),
			new("Run Effect Source lifecycle -> result locations", TestRunEffectSourceResultLocations),
			new("Run Effect Source lifecycle -> draw and exhaust", TestRunEffectSourceDrawAndExhaust),
			new("Run Effect Source lifecycle -> card-play and phase hooks", TestRunEffectSourceCardPlayAndPhaseHooks),
			new("Copy Debuffs: selected source to another enemy", TestCopyDebuffs),
			new("Selection bus: Select Row draws exactly those cards", TestSelectedRowDraw),
			new("Grant -> Hits All Enemies safety block", TestHitsAllGrantIsBlocked),
			new("Grant -> Auto Action transfers wrapper and payload", TestAutoActionGrantPackage),
			new("Card Action -> custom keyword grants definition package", TestCustomKeywordGrantPackage),
			new("Damage source: cardless Thorns does not inherit card bonus", TestCardlessDamageDoesNotInheritCardBonus),
			new("Amount source: vanilla total damage uses actual AoE results", TestVanillaTotalDamageUsesActualResults),
			new("Power lifetime: expiry removes mirrors and reapply starts clean", TestPowerExpiryAndCleanReapply),
			new("Power merge: one listener preserves per-stack potency", TestPowerMergeContract),
			new("Power payloads: delayed damage and block stay unpowered", TestPowerPayloadsStayUnpowered),
			new("Custom status: passive stats track stacks and restore", TestCustomStatusPassiveStatLifetime),
			new("Effect registry: selection contracts are self-audited", TestEffectRegistrySelectionContracts),
			new("Parity: current turn uses the owning player's turn", TestCurrentTurnUsesPlayerTurn),
			new("Parity: amountless targeted powers request a target", TestAmountlessPowerTargetAcquisition),
			new("Parity: Stars Spent uses the public game hook", TestStarsSpentPublicHook),
			new("Parity: enemy death relic trigger includes indirect deaths", TestEnemyDiedRelicTrigger),
			new("Power trigger: bearer hit includes fully blocked attacks", TestBearerHitByAttack),
			new("Effect registry: Apply Poison supports Whenever power", TestApplyPoisonPowerSupport),
			new("Cosmetic VFX: target preset binds to authoritative hit loop", TestAttackHitVfxBinding),
			new("Cosmetic VFX: direct damage branches keep hit hooks", TestDirectDamageVfxHooks),
			new("Multiplayer client Ready reaches vanilla network dispatch", TestMultiplayerClientReady),
			new("UI source contract: Reset requires confirmation", TestResetRequiresConfirmation),
			new("UI source contract: Result Pile saves selected destination", TestResultPileDestinationIsSerialized),
			new("Generated text: Match Energy qualifier appears once", TestMatchEnergyQualifierIsUnique),
			new("UI source contract: card description uses auto-size", TestCardDescriptionUsesAutoSize)
		];

		int passed = 0;
		foreach (TestCase test in tests)
		{
			try
			{
				await test.Body();
				passed++;
				Console.WriteLine($"PASS {test.Name}");
			}
			catch (Exception exception)
			{
				Console.Error.WriteLine($"FAIL {test.Name}");
				Console.Error.WriteLine(exception);
			}
		}

		Console.WriteLine($"RESULT {passed}/{tests.Length} tests passed; {ModelDb.All.Count()} beta and mod models loaded.");
		return passed == tests.Length ? 0 : 1;
	}

	private static void InitializeHeadlessRuntime()
	{
		TestMode.IsOn = true;
		InstallHeadlessLoggerPatch();
		typeof(ModManager).GetProperty(nameof(ModManager.State))!.SetValue(null, ModManagerState.Skipped);
		MegaCrit.Sts2.Core.Modding.AssemblyInfo.Init();

		Type[] modModelTypes = typeof(CardExtraEffect).Assembly.GetTypes()
			.Where(type => !type.IsAbstract && typeof(AbstractModel).IsAssignableFrom(type))
			.ToArray();
		ModelDb.Init(AbstractModelSubtypes.All.Concat(modModelTypes).Distinct().ToArray());
		InitializeModelIdMapsWithoutGodot();
		ModelDb.InitIds();
		InstallHeadlessSaveManager();
		DisableGodotBackedPerformanceSettingsLoading();
		InitializeDefinitionStoreWithoutGodot();
		InstallProductionIntegrationPatches();
	}

	private static void InstallProductionIntegrationPatches()
	{
		const string harmonyId = "card-editor.headless-production-integration";
		Harmony harmony = new(harmonyId);
		harmony.CreateClassProcessor(typeof(Hook_AfterCardPlayed_Patch)).Patch();
		harmony.CreateClassProcessor(typeof(Hook_AfterCardPlayed_AutoPlayDrawSelfFromPile_Patch)).Patch();
		harmony.CreateClassProcessor(typeof(CombatHistory_DamageReceived_CardEditorVanillaDamage_Patch)).Patch();
		harmony.CreateClassProcessor(typeof(Hook_AfterStarsSpent_CardEditorPowerCountEvent_Patch)).Patch();

		MethodInfo target = AccessTools.Method(typeof(Hook), nameof(Hook.AfterCardPlayed))
			?? throw new MissingMethodException(typeof(Hook).FullName, nameof(Hook.AfterCardPlayed));
		Patches? patches = Harmony.GetPatchInfo(target);
		if (patches == null || !patches.Owners.Contains(harmonyId, StringComparer.Ordinal))
		{
			throw new InvalidOperationException("Production Hook.AfterCardPlayed patches were not installed in the integration harness.");
		}
	}

	private static void InstallHeadlessLoggerPatch()
	{
		Type loggerType = typeof(CombatManager).Assembly.GetType("MegaCrit.Sts2.Core.Logging.Logger")
			?? throw new TypeLoadException("Current beta no longer contains Logger.");
		MethodInfo target = loggerType.GetMethod("GetIsRunningFromGodotEditor", BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new MissingMethodException(loggerType.FullName, "GetIsRunningFromGodotEditor");
		MethodInfo prefix = typeof(Program).GetMethod(nameof(ForceConsoleLogger), BindingFlags.Static | BindingFlags.NonPublic)!;
		Harmony harmony = new("card-editor.headless-test-harness");
		harmony.Patch(target, prefix: new HarmonyMethod(prefix));
		MethodInfo print = typeof(ConsoleLogPrinter).GetMethod(nameof(ConsoleLogPrinter.Print))!;
		MethodInfo printPrefix = typeof(Program).GetMethod(nameof(PrintWithoutGodot), BindingFlags.Static | BindingFlags.NonPublic)!;
		harmony.Patch(print, prefix: new HarmonyMethod(printPrefix));
	}

	private static bool ForceConsoleLogger(ref bool __result)
	{
		__result = false;
		return false;
	}

	private static bool PrintWithoutGodot(LogLevel logLevel, string text)
	{
		TextWriter writer = logLevel >= LogLevel.Warn ? Console.Error : Console.Out;
		writer.WriteLine($"[GAME {logLevel.ToString().ToUpperInvariant()}] {text}");
		return false;
	}

	private static Fixture CreateFixture(IReadOnlyList<Player>? players = null)
	{
		RunState run = RunState.CreateForTest(players, seed: $"CARD-EDITOR-TEST-{Interlocked.Increment(ref _fixtureNumber)}");
		CombatState combat = new(runState: run);
		foreach (Player player in run.Players)
		{
			player.ActivateHooks();
			player.ResetCombatState();
			combat.AddPlayer(player);
			player.PopulateCombatState(run.Rng.Shuffle, combat);
		}
		Creature baselineEnemy = combat.CreateCreature(
			ModelDb.Monster<MockAttackMonster>().ToMutable(),
			CombatSide.Enemy,
			"fixture-primary");
		combat.AddCreature(baselineEnemy);
		ActivateCombatForHeadlessCommands(combat);

		return new Fixture(run, combat, run.Players[0], new BlockingPlayerChoiceContext());
	}

	private static Fixture CreateMultiplayerFixture()
	{
		Player first = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
		Player second = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
		return CreateFixture([first, second]);
	}

	private static void ActivateCombatForHeadlessCommands(CombatState combat)
	{
		CombatManager manager = CombatManager.Instance;
		Type turnStateType = typeof(CombatManager).Assembly.GetType("MegaCrit.Sts2.Core.Combat.CombatTurnState")
			?? throw new TypeLoadException("Current beta no longer contains CombatTurnState.");
		object turnState = Activator.CreateInstance(
			turnStateType,
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
			binder: null,
			args: [combat],
			culture: null) ?? throw new InvalidOperationException("Could not construct a headless CombatTurnState.");
		turnStateType.GetProperty("IsInProgress")!.SetValue(turnState, true);
		turnStateType.GetProperty("IsStarting")!.SetValue(turnState, false);
		typeof(CombatManager).GetField("_turnState", BindingFlags.Instance | BindingFlags.NonPublic)!
			.SetValue(manager, turnState);
		manager.StateTracker.SetState(combat);
	}

	private static CardModel CreateSourceCard(Fixture fixture)
	{
		CardModel canonical = ModelDb.AllCards.First(card => card.IsTransformable && card.Type != CardType.Quest);
		return fixture.Combat.CreateCard(canonical, fixture.Player);
	}

	private static CardModel CreateCostlyHost(Fixture fixture)
	{
		return fixture.Combat.CreateCard(
			ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.Bludgeon>(), fixture.Player);
	}

	private static CardExtraEffect GrantEffectSource(Fixture fixture, CardModel host, Type sourceType)
	{
		CardModel source = ModelDb.AllCards.Single(card => card.GetType() == sourceType);
		CardExtraEffect effect = ImmediateOnPlay(CardExtraEffectKind.RunEffectSourceCard);
		effect.Amount = 0;
		effect.SpecificCardId = source.Id.ToString();
		CardEditorTemporaryExtraEffectController.Grant(
			fixture.Combat, host, effect, CardExtraEffectCardGrantDuration.ThisCombat, turns: 1);
		return effect;
	}

	private static CardPlay CreateCardPlay(Fixture fixture, CardModel card, Creature? target = null)
	{
		return new CardPlay
		{
			Card = card,
			Player = fixture.Player,
			Target = target,
			ResultPile = card.Pile?.Type ?? PileType.None,
			Resources = _zeroResources,
			IsAutoPlay = true,
			PlayIndex = 0,
			PlayCount = 1
		};
	}

	private static CardExtraEffect ImmediateOnPlay(CardExtraEffectKind kind, int amount = 1)
	{
		return new CardExtraEffect
		{
			Kind = kind,
			Amount = amount,
			Target = CardExtraEffectTarget.Self,
			Trigger = CardExtraEffectTrigger.OnPlay,
			Timing = CardExtraEffectTiming.Immediate
		};
	}

	private static async Task RunOnPlay(Fixture fixture, CardModel source, params CardExtraEffect[] effects)
	{
		await CardEditorExtraEffects.RunResolvedOnPlayEffectsDuringCardPlay(
			fixture.Combat,
			fixture.Choices,
			CreateCardPlay(fixture, source),
			effects);
	}

	private static Task PutInPile(CardModel card, PileType pile, CardPilePosition position = CardPilePosition.Bottom)
	{
		return CardPileCmd.Add(card, pile, position, skipVisuals: true);
	}

	private static async Task TestMoveCardsBetweenPiles()
	{
		(CardExtraEffectCardPile Editor, PileType Runtime)[] piles =
		[
			(CardExtraEffectCardPile.Hand, PileType.Hand),
			(CardExtraEffectCardPile.DrawPile, PileType.Draw),
			(CardExtraEffectCardPile.DiscardPile, PileType.Discard),
			(CardExtraEffectCardPile.ExhaustPile, PileType.Exhaust)
		];

		foreach ((CardExtraEffectCardPile fromEditor, PileType fromRuntime) in piles)
		{
			foreach ((CardExtraEffectCardPile toEditor, PileType toRuntime) in piles)
			{
				if (fromRuntime == toRuntime)
				{
					continue;
				}

				foreach (CardExtraEffectCardPilePosition requestedPosition in new[]
					{
						CardExtraEffectCardPilePosition.Top,
						CardExtraEffectCardPilePosition.Bottom
					})
				{
					Fixture fixture = CreateFixture();
					CardModel source = CreateSourceCard(fixture);
					CardModel candidate = fixture.Combat.CreateCard(
						ModelDb.AllCards.First(card => card.IsTransformable && card.Type == CardType.Skill), fixture.Player);
					CardModel sentinel = fixture.Combat.CreateCard(
						ModelDb.AllCards.First(card => card.IsTransformable && card.Type == CardType.Attack), fixture.Player);
					await PutInPile(sentinel, toRuntime, CardPilePosition.Bottom);
					await PutInPile(candidate, fromRuntime, CardPilePosition.Top);

					CardExtraEffect move = ImmediateOnPlay(CardExtraEffectKind.MoveCardsBetweenPiles);
					move.CardSelectionPile = fromEditor;
					move.CardSelectionMode = CardExtraEffectCardSelectionMode.Top;
					move.MoveToPile = toEditor;
					move.MoveToPosition = requestedPosition;
					await RunOnPlay(fixture, source, move);

					string scenario = $"{fromRuntime} -> {toRuntime} {requestedPosition}";
					AssertSame(toRuntime, candidate.Pile?.Type, $"{scenario}: selected card entered the wrong pile");
					IReadOnlyList<CardModel> destination = toRuntime.GetPile(fixture.Player).Cards;
					CardModel actual = requestedPosition == CardExtraEffectCardPilePosition.Top
						? destination[0]
						: destination[^1];
					AssertSame(candidate, actual, $"{scenario}: selected card ignored the requested position");
				}
			}
		}
	}

	private static async Task TestManualExhaust()
	{
		Fixture fixture = CreateFixture();
		CardModel source = CreateSourceCard(fixture);
		CardModel selected = fixture.Combat.CreateCard(
			ModelDb.AllCards.First(card => card.IsTransformable && card.Type == CardType.Skill), fixture.Player);
		await PutInPile(selected, PileType.Hand);

		TestCardSelector selector = new();
		selector.PrepareToSelect([selected]);
		using IDisposable _ = CardSelectCmd.UseSelector(selector);

		CardExtraEffect effect = ImmediateOnPlay(CardExtraEffectKind.ExhaustCards);
		effect.CardSelectionPile = CardExtraEffectCardPile.Hand;
		effect.CardSelectionMode = CardExtraEffectCardSelectionMode.Choose;
		await RunOnPlay(fixture, source, effect);

		AssertSame(PileType.Exhaust, selected.Pile?.Type, "chosen hand card did not enter the exhaust pile");
	}

	private static async Task TestMoveCardsFinalPositionTrigger()
	{
		Fixture fixture = CreateFixture();
		CardModel source = CreateSourceCard(fixture);
		CardModel candidate = fixture.Combat.CreateCard(
			ModelDb.AllCards.First(card => card.IsTransformable && card.Type == CardType.Skill), fixture.Player);
		CardModel sentinel = fixture.Combat.CreateCard(
			ModelDb.AllCards.First(card => card.IsTransformable && card.Type == CardType.Attack), fixture.Player);
		await PutInPile(candidate, PileType.Hand, CardPilePosition.Top);
		await PutInPile(sentinel, PileType.Discard, CardPilePosition.Bottom);

		CardExtraEffect topReaction = ImmediateOnPlay(CardExtraEffectKind.GainEnergy, amount: 1);
		topReaction.Trigger = CardExtraEffectTrigger.OnMovedToTopOfPile;
		topReaction.CardSelectionPile = CardExtraEffectCardPile.DiscardPile;
		CardExtraEffect bottomReaction = ImmediateOnPlay(CardExtraEffectKind.GainEnergy, amount: 10);
		bottomReaction.Trigger = CardExtraEffectTrigger.OnMovedToBottomOfPile;
		bottomReaction.CardSelectionPile = CardExtraEffectCardPile.DiscardPile;
		CardEditorTemporaryExtraEffectController.Grant(
			fixture.Combat, candidate, topReaction, CardExtraEffectCardGrantDuration.ThisCombat, turns: 1);
		CardEditorTemporaryExtraEffectController.Grant(
			fixture.Combat, candidate, bottomReaction, CardExtraEffectCardGrantDuration.ThisCombat, turns: 1);

		CardExtraEffect move = ImmediateOnPlay(CardExtraEffectKind.MoveCardsBetweenPiles);
		move.CardSelectionPile = CardExtraEffectCardPile.Hand;
		move.CardSelectionMode = CardExtraEffectCardSelectionMode.Top;
		move.MoveToPile = CardExtraEffectCardPile.DiscardPile;
		move.MoveToPosition = CardExtraEffectCardPilePosition.Top;
		int before = fixture.Player.PlayerCombatState!.Energy;
		await RunOnPlay(fixture, source, move);

		AssertSame(before + 1, fixture.Player.PlayerCombatState.Energy,
			"Discard Top fired the intermediate Bottom trigger or failed to fire the final Top trigger");
	}

	private static async Task TestTransformByType()
	{
		Fixture fixture = CreateFixture();
		CardModel source = CreateSourceCard(fixture);
		CardModel attack = fixture.Combat.CreateCard(
			ModelDb.AllCards.First(card => card.IsTransformable && card.Type == CardType.Attack), fixture.Player);
		CardModel skill = fixture.Combat.CreateCard(
			ModelDb.AllCards.First(card => card.IsTransformable && card.Type == CardType.Skill), fixture.Player);
		CardModel replacement = ModelDb.AllCards.First(card => card.IsTransformable && card.Type == CardType.Power);
		await PutInPile(attack, PileType.Hand);
		await PutInPile(skill, PileType.Hand);

		CardExtraEffect effect = CreateSpecificTransform(replacement);
		effect.CardSelectionType = CardGeneratedCardType.Attack;
		await RunOnPlay(fixture, source, effect);

		IReadOnlyList<CardModel> hand = PileType.Hand.GetPile(fixture.Player).Cards;
		Assert(!hand.Contains(attack), "attack filter left the selected attack untransformed");
		Assert(hand.Contains(skill), "attack filter incorrectly transformed a skill");
		Assert(hand.Any(card => card.Id == replacement.Id), "specific replacement card was not created in hand");
	}

	private static async Task TestTransformByTag()
	{
		Fixture fixture = CreateFixture();
		CardModel source = CreateSourceCard(fixture);
		CardModel taggedCanonical = ModelDb.AllCards.First(card =>
			card.IsTransformable && card.Type != CardType.Quest && card.Tags.Any(tag => tag != CardTag.None));
		CardTag tag = taggedCanonical.Tags.First(value => value != CardTag.None);
		CardModel tagged = fixture.Combat.CreateCard(taggedCanonical, fixture.Player);
		CardModel untagged = fixture.Combat.CreateCard(
			ModelDb.AllCards.First(card => card.IsTransformable && card.Type != CardType.Quest && !card.Tags.Contains(tag)), fixture.Player);
		CardModel replacement = ModelDb.AllCards.First(card => card.IsTransformable && card.Id != tagged.Id && card.Id != untagged.Id);
		await PutInPile(tagged, PileType.Hand);
		await PutInPile(untagged, PileType.Hand);

		CardExtraEffect effect = CreateSpecificTransform(replacement);
		effect.CardMatchMode = CardExtraEffectCardMatchMode.Tag;
		effect.MatchTagKind = CardExtraEffectCardMatchTagKind.Vanilla;
		effect.MatchVanillaTag = tag;
		await RunOnPlay(fixture, source, effect);

		IReadOnlyList<CardModel> hand = PileType.Hand.GetPile(fixture.Player).Cards;
		Assert(!hand.Contains(tagged), $"{tag} filter left the tagged card untransformed");
		Assert(hand.Contains(untagged), $"{tag} filter incorrectly transformed an untagged card");
		Assert(hand.Any(card => card.Id == replacement.Id), "tag transform did not create its specific replacement");
	}

	private static CardExtraEffect CreateSpecificTransform(CardModel replacement)
	{
		CardExtraEffect effect = ImmediateOnPlay(CardExtraEffectKind.TransformCards, amount: 99);
		effect.CardSelectionPile = CardExtraEffectCardPile.Hand;
		effect.CardSelectionMode = CardExtraEffectCardSelectionMode.All;
		effect.TransformMode = CardExtraEffectTransformMode.SpecificCard;
		effect.SpecificCardId = replacement.Id.ToString();
		return effect;
	}

	private static async Task TestTriggeredThisCardCostReduction()
	{
		(CardExtraEffectTrigger Trigger, Func<Fixture, CardModel, Task> Dispatch)[] cases =
		[
			(CardExtraEffectTrigger.OnPlay, (fixture, card) => CardEditorExtraEffects.RunAfterCardPlayed(fixture.Combat, fixture.Choices, CreateCardPlay(fixture, card))),
			(CardExtraEffectTrigger.OnDraw, (fixture, card) => CardEditorExtraEffects.RunAfterCardDrawn(fixture.Combat, fixture.Choices, card)),
			(CardExtraEffectTrigger.OnDiscard, (fixture, card) => CardEditorExtraEffects.RunAfterCardDiscarded(fixture.Combat, fixture.Choices, card)),
			(CardExtraEffectTrigger.OnExhaust, (fixture, card) => CardEditorExtraEffects.RunAfterCardExhausted(fixture.Combat, fixture.Choices, card)),
			(CardExtraEffectTrigger.EndOfTurnInHand, (fixture, card) => CardEditorExtraEffects.RunEndOfTurnInHand(fixture.Combat, fixture.Choices, card)),
			(CardExtraEffectTrigger.StartOfTurn, (fixture, card) => CardEditorExtraEffects.RunStartOfTurn(fixture.Combat, fixture.Choices, card)),
			(CardExtraEffectTrigger.EndOfTurn, (fixture, card) => CardEditorExtraEffects.RunEndOfTurn(fixture.Combat, fixture.Choices, card)),
			(CardExtraEffectTrigger.StartOfEnemyTurn, (fixture, card) => CardEditorExtraEffects.RunStartOfEnemyTurn(fixture.Combat, fixture.Choices, card)),
			(CardExtraEffectTrigger.EndOfEnemyTurn, (fixture, card) => CardEditorExtraEffects.RunEndOfEnemyTurn(fixture.Combat, fixture.Choices, card)),
			(CardExtraEffectTrigger.OstyDealDamage, (fixture, card) => CardEditorExtraEffects.RunAfterOstyDealDamage(fixture.Combat, fixture.Choices, card)),
			(CardExtraEffectTrigger.AfterCombat, (fixture, card) => CardEditorExtraEffects.RunAfterCombat(fixture.Combat, fixture.Choices, card)),
			(CardExtraEffectTrigger.OnChannel, (fixture, card) => CardEditorExtraEffects.RunAfterOrbChanneled(fixture.Combat, fixture.Choices, card)),
			(CardExtraEffectTrigger.OnEvoke, (fixture, card) => CardEditorExtraEffects.RunAfterOrbEvoked(fixture.Combat, fixture.Choices, card)),
			(CardExtraEffectTrigger.TurnBoundary, DispatchTurnBoundary),
			(CardExtraEffectTrigger.DeckPassiveCombatStart, (fixture, card) => CardEditorExtraEffects.RunDeckPassiveCombatStart(fixture.Combat, fixture.Choices, card)),
			(CardExtraEffectTrigger.DeckPassiveCombatEnd, (fixture, card) => CardEditorExtraEffects.RunDeckPassiveCombatEnd(fixture.Combat, fixture.Choices, card)),
			(CardExtraEffectTrigger.OnMovedToTopOfPile, (fixture, card) => CardEditorExtraEffects.RunAfterCardMovedToTopOfPile(fixture.Combat, fixture.Choices, card)),
			(CardExtraEffectTrigger.OnMovedToBottomOfPile, (fixture, card) => CardEditorExtraEffects.RunAfterCardMovedToBottomOfPile(fixture.Combat, fixture.Choices, card)),
			(CardExtraEffectTrigger.AfterCardEnteredCombat, (fixture, card) => CardEditorExtraEffects.RunAfterCardEnteredCombat(fixture.Combat, fixture.Choices, card)),
			(CardExtraEffectTrigger.BeforeHandDraw, (fixture, card) => CardEditorExtraEffects.RunBeforeHandDraw(fixture.Combat, fixture.Choices, card)),
			(CardExtraEffectTrigger.AfterAttack, (fixture, card) => CardEditorExtraEffects.RunAfterAttack(fixture.Combat, fixture.Choices, card, fixture.Combat.Enemies.First())),
			(CardExtraEffectTrigger.AfterDeath, (fixture, card) => CardEditorExtraEffects.RunAfterDeath(fixture.Combat, fixture.Choices, card, fixture.Combat.Enemies.First())),
			(CardExtraEffectTrigger.AfterCombatEnd, (fixture, card) => CardEditorExtraEffects.RunAfterCombatEnd(fixture.Combat, fixture.Choices, card)),
			(CardExtraEffectTrigger.OnChosen, (fixture, card) => CardEditorExtraEffects.RunOnChosen(fixture.Combat, fixture.Choices, card))
		];

		foreach ((CardExtraEffectTrigger trigger, Func<Fixture, CardModel, Task> dispatch) in cases)
		{
			Fixture fixture = CreateFixture();
			CardModel card = CreateSourceCard(fixture);
			if (trigger == CardExtraEffectTrigger.OnMovedToTopOfPile)
			{
				await PutInPile(card, PileType.Hand, CardPilePosition.Top);
			}
			else if (trigger == CardExtraEffectTrigger.OnMovedToBottomOfPile)
			{
				await PutInPile(card, PileType.Hand, CardPilePosition.Bottom);
			}
			CardExtraEffect effect = ImmediateOnPlay(CardExtraEffectKind.CardCostsLess);
			effect.Trigger = trigger;
			effect.CardCostsLessMode = CardExtraEffectCardCostsLessMode.Triggered;
			effect.CardCostsLessDuration = CardExtraEffectCardCostsLessDuration.ThisCombat;
			effect.CardCostsLessModifier = CardExtraEffectCostModifier.Reduce;
			if (trigger == CardExtraEffectTrigger.TurnBoundary)
			{
				effect.TurnBoundary = CardExtraEffectTurnBoundary.Start;
				effect.TurnBoundarySide = CardExtraEffectTurnBoundarySide.YourTurn;
			}
			CardEditorTemporaryExtraEffectController.Grant(
				fixture.Combat, card, effect, CardExtraEffectCardGrantDuration.ThisCombat, turns: 1);

			AssertSame(0, CardEditorExtraEffects.GetCardCostsLessReduction(fixture.Combat, card), $"{trigger} reduction became passive before its event");
			await dispatch(fixture, card);
			AssertSame(1, CardEditorExtraEffects.GetCardCostsLessReduction(fixture.Combat, card), $"{trigger} did not apply the This Card cost reduction");
			Assert(CardEditorExtraEffects.DoesTriggerMatch(effect, trigger, card), $"{trigger} did not match its own event contract");
		}
	}

	private static Task DispatchTurnBoundary(Fixture fixture, CardModel card)
	{
		return CardEditorExtraEffects.RunTurnBoundary(
			fixture.Combat,
			fixture.Choices,
			card,
			CardExtraEffectTurnBoundary.Start,
			CardExtraEffectTurnBoundarySide.YourTurn);
	}

	private static async Task TestTriggeredThisCardCostReductionWheneverPower()
	{
		foreach (CardExtraEffectCountEvent countEvent in CardEditorExtraEffects.PowerTriggerCountEvents)
		{
			Fixture fixture = CreateFixture();
			CardModel source = CreateSourceCard(fixture);
			CardExtraEffect effect = ImmediateOnPlay(CardExtraEffectKind.CardCostsLess);
			effect.Trigger = CardExtraEffectTrigger.OnCountEvent;
			effect.PowerTriggerCountEvent = countEvent;
			effect.AsPower = true;
			effect.CardCostsLessMode = CardExtraEffectCardCostsLessMode.Triggered;
			effect.CardCostsLessDuration = CardExtraEffectCardCostsLessDuration.ThisCombat;
			effect.CardCostsLessModifier = CardExtraEffectCostModifier.Reduce;
			CardEditorTemporaryExtraEffectController.Grant(
				fixture.Combat, source, effect, CardExtraEffectCardGrantDuration.ThisCombat, turns: 1);

			await CardEditorExtraEffects.RunAfterCardPlayed(
				fixture.Combat, fixture.Choices, CreateCardPlay(fixture, source));
			CardEditorExtraEffectPower? power = fixture.Player.Creature.GetPower<CardEditorExtraEffectPower>();
			Assert(power != null, $"{countEvent}: playing the source card did not install the Whenever power");

			PowerModel? triggeringPower = null;
			if (countEvent is CardExtraEffectCountEvent.StatusGained or CardExtraEffectCountEvent.StatusLost)
			{
				triggeringPower = await GamePowerCmd.Apply<WeakPower>(
					fixture.Choices, fixture.Player.Creature, 1, fixture.Player.Creature, source, silent: true);
			}

			AssertSame(0, CardEditorExtraEffects.GetCardCostsLessReduction(fixture.Combat, source), $"{countEvent}: reduction activated before its Whenever event");
			await power!.TriggerCountEvent(
				fixture.Choices,
				countEvent,
				triggeringCard: source,
				triggeringPower: triggeringPower,
				eventActor: fixture.Player.Creature,
				amount: 1);
			AssertSame(1, CardEditorExtraEffects.GetCardCostsLessReduction(fixture.Combat, source), $"{countEvent}: installed Whenever power did not reduce this card's cost");
		}
	}

	private static async Task TestProductionHookWheneverCostReduction()
	{
		Fixture fixture = CreateFixture();
		CardModel source = CreateSourceCard(fixture);
		CardExtraEffect effect = ImmediateOnPlay(CardExtraEffectKind.CardCostsLess);
		effect.Trigger = CardExtraEffectTrigger.OnCountEvent;
		effect.PowerTriggerCountEvent = CardExtraEffectCountEvent.Played;
		effect.AsPower = true;
		effect.CardCostsLessMode = CardExtraEffectCardCostsLessMode.Triggered;
		effect.CardCostsLessDuration = CardExtraEffectCardCostsLessDuration.ThisCombat;
		effect.CardCostsLessModifier = CardExtraEffectCostModifier.Reduce;
		CardEditorTemporaryExtraEffectController.Grant(
			fixture.Combat, source, effect, CardExtraEffectCardGrantDuration.ThisCombat, turns: 1);

		await Hook.AfterCardPlayed(fixture.Combat, fixture.Choices, CreateCardPlay(fixture, source));
		CardEditorExtraEffectPower? power = fixture.Player.Creature.GetPower<CardEditorExtraEffectPower>();
		Assert(power != null, "production Hook.AfterCardPlayed did not install the Whenever power");
		AssertSame(0, CardEditorExtraEffects.GetCardCostsLessReduction(fixture.Combat, source),
			"the Whenever reduction incorrectly counted the card play that installed it");

		CardModel triggerCard = CreateSourceCard(fixture);
		await Hook.AfterCardPlayed(fixture.Combat, fixture.Choices, CreateCardPlay(fixture, triggerCard));
		AssertSame(1, CardEditorExtraEffects.GetCardCostsLessReduction(fixture.Combat, source),
			"the real beta AfterCardPlayed listener chain did not activate This Card cost reduction");
	}

	private static async Task TestAfterDeathPowerTrigger()
	{
		Fixture fixture = CreateFixture();
		CardModel source = CreateSourceCard(fixture);
		Creature enemy = AddMockEnemy(fixture, "after-death-target");
		CardExtraEffect effect = ImmediateOnPlay(CardExtraEffectKind.GainEnergy);
		effect.Trigger = CardExtraEffectTrigger.AfterDeath;
		effect.AsPower = true;
		// Reproduce the editor's default: card-owner power + Self. Runtime normalization must
		// make the generated "sees a creature die" behavior match what the player configured.
		effect.PowerTriggerFrom = CardExtraEffectPowerTriggerFrom.Self;
		effect.PowerTargeting = CardExtraEffectPowerTargeting.TriggerTarget;
		AssertSame(CardExtraEffectPowerTriggerFrom.Anyone, CardEditorExtraEffects.GetEffectivePowerTriggerFrom(effect),
			"card-owner After Death default did not normalize to watching creature deaths");
		CardEditorTemporaryExtraEffectController.Grant(
			fixture.Combat, source, effect, CardExtraEffectCardGrantDuration.ThisCombat, turns: 1);

		await CardEditorExtraEffects.RunAfterCardPlayed(
			fixture.Combat, fixture.Choices, CreateCardPlay(fixture, source, enemy));
		CardEditorExtraEffectPower? power = fixture.Player.Creature.GetPower<CardEditorExtraEffectPower>();
		Assert(power != null, "playing the card did not install CardEditorExtraEffectPower");

		int before = fixture.Player.PlayerCombatState!.Energy;
		await power!.AfterDeath(fixture.Choices, enemy, wasRemovalPrevented: true, deathAnimLength: 0f);
		AssertSame(before, fixture.Player.PlayerCombatState.Energy, "prevented enemy death incorrectly dispatched Gain Energy");
		await power!.AfterDeath(fixture.Choices, enemy, wasRemovalPrevented: false, deathAnimLength: 0f);
		AssertSame(before + 1, fixture.Player.PlayerCombatState.Energy, "enemy death did not dispatch Gain Energy from the stored power");

		Fixture enemyHostFixture = CreateFixture();
		CardModel enemyHostSource = CreateSourceCard(enemyHostFixture);
		Creature enemyHost = AddMockEnemy(enemyHostFixture, "after-death-enemy-host");
		Creature unrelatedEnemy = AddMockEnemy(enemyHostFixture, "after-death-unrelated");
		CardExtraEffect enemyHostedEffect = ImmediateOnPlay(CardExtraEffectKind.GainEnergy);
		enemyHostedEffect.Trigger = CardExtraEffectTrigger.AfterDeath;
		enemyHostedEffect.AsPower = true;
		enemyHostedEffect.Target = CardExtraEffectTarget.AllEnemies;
		enemyHostedEffect.PowerHost = CardExtraEffectPowerHost.EffectTargets;
		enemyHostedEffect.PowerTriggerFrom = CardExtraEffectPowerTriggerFrom.Self;
		enemyHostedEffect.PowerTargeting = CardExtraEffectPowerTargeting.TriggerTarget;
		CardEditorTemporaryExtraEffectController.Grant(
			enemyHostFixture.Combat, enemyHostSource, enemyHostedEffect, CardExtraEffectCardGrantDuration.ThisCombat, turns: 1);
		await CardEditorExtraEffects.RunAfterCardPlayed(
			enemyHostFixture.Combat,
			enemyHostFixture.Choices,
			CreateCardPlay(enemyHostFixture, enemyHostSource, enemyHost));
		CardEditorExtraEffectPower? enemyPower = enemyHost.GetPower<CardEditorExtraEffectPower>();
		Assert(enemyPower != null, "Trigger Target did not install the After Death power on the enemy");

		int enemyHostBefore = enemyHostFixture.Player.PlayerCombatState!.Energy;
		await enemyPower!.AfterDeath(enemyHostFixture.Choices, unrelatedEnemy, wasRemovalPrevented: false, deathAnimLength: 0f);
		AssertSame(enemyHostBefore, enemyHostFixture.Player.PlayerCombatState.Energy, "Self-hosted death power reacted to an unrelated enemy");
		await enemyPower.AfterDeath(enemyHostFixture.Choices, enemyHost, wasRemovalPrevented: true, deathAnimLength: 0f);
		AssertSame(enemyHostBefore, enemyHostFixture.Player.PlayerCombatState.Energy, "prevented host death incorrectly fired the enemy-hosted power");
		await enemyPower.AfterDeath(enemyHostFixture.Choices, enemyHost, wasRemovalPrevented: false, deathAnimLength: 0f);
		AssertSame(enemyHostBefore + 1, enemyHostFixture.Player.PlayerCombatState.Energy, "enemy-hosted Self death did not pay its source-card owner");
	}

	private static async Task TestProductionHookAfterDeath()
	{
		Fixture fixture = CreateFixture();
		CardModel source = CreateSourceCard(fixture);
		Creature enemy = AddMockEnemy(fixture, "production-after-death-target");
		CardExtraEffect effect = ImmediateOnPlay(CardExtraEffectKind.GainEnergy);
		effect.Trigger = CardExtraEffectTrigger.AfterDeath;
		effect.AsPower = true;
		effect.PowerTriggerFrom = CardExtraEffectPowerTriggerFrom.Self;
		effect.PowerTargeting = CardExtraEffectPowerTargeting.TriggerTarget;
		CardEditorTemporaryExtraEffectController.Grant(
			fixture.Combat, source, effect, CardExtraEffectCardGrantDuration.ThisCombat, turns: 1);

		await Hook.AfterCardPlayed(fixture.Combat, fixture.Choices, CreateCardPlay(fixture, source, enemy));
		Assert(fixture.Player.Creature.GetPower<CardEditorExtraEffectPower>() != null,
			"production Hook.AfterCardPlayed did not install the After Death power");

		int before = fixture.Player.PlayerCombatState!.Energy;
		ulong? previousNetId = LocalContext.NetId;
		LocalContext.NetId = fixture.Player.NetId;
		try
		{
			await Hook.AfterDeath(fixture.Run, fixture.Combat, enemy, wasRemovalPrevented: false, deathAnimLength: 0f);
		}
		finally
		{
			LocalContext.NetId = previousNetId;
		}
		AssertSame(before + 1, fixture.Player.PlayerCombatState.Energy,
			"the real beta Hook.AfterDeath listener chain did not dispatch Gain Energy");
	}

	private static async Task TestRunEffectSourceRegentCards()
	{
		Fixture fixture = CreateFixture();
		CardModel host = CreateSourceCard(fixture);
		Creature enemy = fixture.Combat.Enemies.First();

		foreach (Type sourceType in new[]
		{
			typeof(MegaCrit.Sts2.Core.Models.Cards.ParticleWall),
			typeof(MegaCrit.Sts2.Core.Models.Cards.IAmInvincible)
		})
		{
			CardModel canonical = ModelDb.AllCards.Single(card => card.GetType() == sourceType);
			int expectedBlock = canonical.DynamicVars.Block.IntValue;
			int beforeBlock = fixture.Player.Creature.Block;
			CardExtraEffect effect = ImmediateOnPlay(CardExtraEffectKind.RunEffectSourceCard);
			effect.Amount = 0;
			effect.SpecificCardId = canonical.Id.ToString();

			await CardEditorExtraEffects.RunResolvedOnPlayEffectsDuringCardPlay(
				fixture.Combat,
				fixture.Choices,
				CreateCardPlay(fixture, host),
				[effect]);

			AssertSame(beforeBlock + expectedBlock, fixture.Player.Creature.Block,
				$"{sourceType.Name}: Run Effect Source did not apply the source card's block");
		}

		await PlayerCmd.AddPet<MegaCrit.Sts2.Core.Models.Monsters.Osty>(fixture.Player);
		CardModel rightHandHand = ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.RightHandHand>();
		int beforeHp = enemy.CurrentHp;
		CardExtraEffect attack = ImmediateOnPlay(CardExtraEffectKind.RunEffectSourceCard);
		attack.Amount = 0;
		attack.SpecificCardId = rightHandHand.Id.ToString();
		await CardEditorExtraEffects.RunResolvedOnPlayEffectsDuringCardPlay(
			fixture.Combat,
			fixture.Choices,
			CreateCardPlay(fixture, host, enemy),
			[attack]);

		Assert(enemy.CurrentHp < beforeHp, "RightHandHand: Run Effect Source did not damage the selected enemy");

		CardModel particleHost = CreateSourceCard(fixture);
		CardExtraEffect particleSource = ImmediateOnPlay(CardExtraEffectKind.RunEffectSourceCard);
		particleSource.Amount = 0;
		particleSource.SpecificCardId = ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.ParticleWall>().Id.ToString();
		CardEditorTemporaryExtraEffectController.Grant(
			fixture.Combat, particleHost, particleSource, CardExtraEffectCardGrantDuration.ThisCombat, turns: 1);
		Assert(CardEditorExtraEffects.TryGetBorrowedEffectSourceResultPileOverride(
			particleHost, out PileType particleResultPile, out CardPilePosition particleResultPosition),
			"ParticleWall: borrowed result-pile behavior was not detected");
		AssertSame(PileType.Hand, particleResultPile, "ParticleWall: borrowed result pile was not Hand");
		AssertSame(CardPilePosition.Bottom, particleResultPosition, "ParticleWall: borrowed Hand position was not Bottom");

		CardModel rightHandHost = CreateSourceCard(fixture);
		CardExtraEffect rightHandSource = ImmediateOnPlay(CardExtraEffectKind.RunEffectSourceCard);
		rightHandSource.Amount = 0;
		rightHandSource.SpecificCardId = rightHandHand.Id.ToString();
		CardEditorTemporaryExtraEffectController.Grant(
			fixture.Combat, rightHandHost, rightHandSource, CardExtraEffectCardGrantDuration.ThisCombat, turns: 1);
		await PutInPile(rightHandHost, PileType.Discard);
		CardModel energyCard = CreateSourceCard(fixture);
		CardPlay energyPlay = new()
		{
			Card = energyCard,
			Player = fixture.Player,
			Target = null,
			ResultPile = PileType.Discard,
			Resources = new ResourceInfo { EnergySpent = 2, EnergyValue = 2, StarsSpent = 0, StarValue = 0 },
			IsAutoPlay = false,
			PlayIndex = 0,
			PlayCount = 1
		};
		await CardEditorExtraEffects.RunBorrowedEffectSourceAfterCardPlayedLate(fixture.Combat, fixture.Choices, energyPlay);
		AssertSame(PileType.Hand, rightHandHost.Pile?.Type ?? PileType.None,
			"RightHandHand: borrowed late hook did not return the host card from Discard to Hand");

		CardModel invincibleHost = fixture.Combat.CreateCard(
			ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.DefendIronclad>(), fixture.Player);
		CardExtraEffect invincibleSource = ImmediateOnPlay(CardExtraEffectKind.RunEffectSourceCard);
		invincibleSource.Amount = 0;
		invincibleSource.SpecificCardId = ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.IAmInvincible>().Id.ToString();
		CardEditorTemporaryExtraEffectController.Grant(
			fixture.Combat, invincibleHost, invincibleSource, CardExtraEffectCardGrantDuration.ThisCombat, turns: 1);
		await PutInPile(invincibleHost, PileType.Draw, CardPilePosition.Top);
		Assert(CardEditorExtraEffects.ShouldAutoPlayBorrowedIAmInvincible(fixture.Player, out CardModel? detectedTopCard),
			"IAmInvincible: borrowed auto-post-play hook did not recognize the top Draw Pile card");
		Assert(ReferenceEquals(invincibleHost, detectedTopCard),
			"IAmInvincible: borrowed auto-post-play hook selected the wrong Draw Pile card");
	}

	private static async Task TestProductionHookRunEffectSourceCards()
	{
		foreach (Type sourceType in new[]
		{
			typeof(MegaCrit.Sts2.Core.Models.Cards.ParticleWall),
			typeof(MegaCrit.Sts2.Core.Models.Cards.IAmInvincible)
		})
		{
			Fixture fixture = CreateFixture();
			CardModel host = CreateSourceCard(fixture);
			CardModel canonical = ModelDb.AllCards.Single(card => card.GetType() == sourceType);
			CardExtraEffect effect = ImmediateOnPlay(CardExtraEffectKind.RunEffectSourceCard);
			effect.Amount = 0;
			effect.SpecificCardId = canonical.Id.ToString();
			CardEditorTemporaryExtraEffectController.Grant(
				fixture.Combat, host, effect, CardExtraEffectCardGrantDuration.ThisCombat, turns: 1);

			int beforeBlock = fixture.Player.Creature.Block;
			await Hook.AfterCardPlayed(fixture.Combat, fixture.Choices, CreateCardPlay(fixture, host));
			AssertSame(beforeBlock + canonical.DynamicVars.Block.IntValue, fixture.Player.Creature.Block,
				$"{sourceType.Name}: production AfterCardPlayed hook did not execute borrowed block");
		}

		Fixture ostyFixture = CreateFixture();
		await PlayerCmd.AddPet<MegaCrit.Sts2.Core.Models.Monsters.Osty>(ostyFixture.Player);
		CardModel ostyHost = CreateSourceCard(ostyFixture);
		CardModel rightHandHand = ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.RightHandHand>();
		CardExtraEffect attack = ImmediateOnPlay(CardExtraEffectKind.RunEffectSourceCard);
		attack.Amount = 0;
		attack.SpecificCardId = rightHandHand.Id.ToString();
		CardEditorTemporaryExtraEffectController.Grant(
			ostyFixture.Combat, ostyHost, attack, CardExtraEffectCardGrantDuration.ThisCombat, turns: 1);

		Creature target = ostyFixture.Combat.Enemies.First();
		int beforeHp = target.CurrentHp;
		await Hook.AfterCardPlayed(ostyFixture.Combat, ostyFixture.Choices, CreateCardPlay(ostyFixture, ostyHost, target));
		Assert(target.CurrentHp < beforeHp,
			"RightHandHand: production AfterCardPlayed hook did not execute borrowed damage");
	}

	private static Task TestRunEffectSourceResultLocations()
	{
		Fixture shiningFixture = CreateFixture();
		CardModel shiningHost = CreateSourceCard(shiningFixture);
		GrantEffectSource(shiningFixture, shiningHost, typeof(MegaCrit.Sts2.Core.Models.Cards.ShiningStrike));
		CardLocation discard = new(shiningFixture.Player, PileType.Discard, CardPilePosition.Bottom);
		Assert(CardEditorExtraEffects.TryGetBorrowedEffectSourceResultLocationOverride(shiningHost, discard, out CardLocation shiningResult),
			"ShiningStrike: borrowed result location was not detected");
		AssertSame(PileType.Draw, shiningResult.pileType, "ShiningStrike: host did not go to Draw Pile");
		AssertSame(CardPilePosition.Top, shiningResult.position, "ShiningStrike: host did not go to the top of Draw Pile");

		CardLocation exhaust = new(shiningFixture.Player, PileType.Exhaust, CardPilePosition.Bottom);
		Assert(!CardEditorExtraEffects.TryGetBorrowedEffectSourceResultLocationOverride(shiningHost, exhaust, out _),
			"ShiningStrike: borrowed result location incorrectly replaced Exhaust");

		Fixture ballFixture = CreateMultiplayerFixture();
		CardModel ballHost = CreateSourceCard(ballFixture);
		GrantEffectSource(ballFixture, ballHost, typeof(MegaCrit.Sts2.Core.Models.Cards.TheBall));
		CardLocation ballDiscard = new(ballFixture.Player, PileType.Discard, CardPilePosition.Bottom);
		Assert(CardEditorExtraEffects.TryGetBorrowedEffectSourceResultLocationOverride(ballHost, ballDiscard, out CardLocation ballResult),
			"TheBall: multiplayer transfer result was not detected");
		AssertSame(ballFixture.Run.Players[1], ballResult.player, "TheBall: host was not passed to the teammate");
		AssertSame(PileType.Draw, ballResult.pileType, "TheBall: host did not enter teammate Draw Pile");
		AssertSame(CardPilePosition.Random, ballResult.position, "TheBall: host did not use random Draw Pile position");
		return Task.CompletedTask;
	}

	private static async Task TestRunEffectSourceDrawAndExhaust()
	{
		Fixture kickFixture = CreateFixture();
		CardModel kickHost = CreateCostlyHost(kickFixture);
		GrantEffectSource(kickFixture, kickHost, typeof(MegaCrit.Sts2.Core.Models.Cards.KinglyKick));
		int kickCost = kickHost.EnergyCost.GetWithModifiers(CostModifiers.Local);
		await CardEditorExtraEffects.RunBorrowedEffectSourceAfterCardDrawn(kickFixture.Combat, kickFixture.Choices, kickHost);
		AssertSame(kickCost - 1, kickHost.EnergyCost.GetWithModifiers(CostModifiers.Local),
			"KinglyKick: drawing the host did not reduce its cost");

		Fixture punchFixture = CreateFixture();
		CardModel punchHost = CreateSourceCard(punchFixture);
		Creature punchEnemy = punchFixture.Combat.Enemies.First();
		CardExtraEffect punchEffect = GrantEffectSource(punchFixture, punchHost, typeof(MegaCrit.Sts2.Core.Models.Cards.KinglyPunch));
		int beforeFirstPunch = punchEnemy.CurrentHp;
		await CardEditorExtraEffects.RunResolvedOnPlayEffectsDuringCardPlay(
			punchFixture.Combat, punchFixture.Choices, CreateCardPlay(punchFixture, punchHost, punchEnemy), [punchEffect]);
		int firstPunchDamage = beforeFirstPunch - punchEnemy.CurrentHp;
		await CardEditorExtraEffects.RunBorrowedEffectSourceAfterCardDrawn(punchFixture.Combat, punchFixture.Choices, punchHost);
		int beforeSecondPunch = punchEnemy.CurrentHp;
		await CardEditorExtraEffects.RunResolvedOnPlayEffectsDuringCardPlay(
			punchFixture.Combat, punchFixture.Choices, CreateCardPlay(punchFixture, punchHost, punchEnemy), [punchEffect]);
		int secondPunchDamage = beforeSecondPunch - punchEnemy.CurrentHp;
		Assert(secondPunchDamage > firstPunchDamage,
			$"KinglyPunch: drawing the host did not increase borrowed damage ({firstPunchDamage} -> {secondPunchDamage})");

		Fixture voidFixture = CreateFixture();
		CardModel voidHost = CreateSourceCard(voidFixture);
		GrantEffectSource(voidFixture, voidHost, typeof(MegaCrit.Sts2.Core.Models.Cards.Void));
		await PlayerCmd.GainEnergy(3, voidFixture.Player);
		int energyBeforeVoid = voidFixture.Player.PlayerCombatState!.Energy;
		await CardEditorExtraEffects.RunBorrowedEffectSourceAfterCardDrawn(voidFixture.Combat, voidFixture.Choices, voidHost);
		AssertSame(energyBeforeVoid - 1, voidFixture.Player.PlayerCombatState.Energy,
			"Void: drawing the host did not lose Energy");

		Fixture drumFixture = CreateFixture();
		CardModel drumHost = CreateSourceCard(drumFixture);
		GrantEffectSource(drumFixture, drumHost, typeof(MegaCrit.Sts2.Core.Models.Cards.DrumOfBattle));
		await PutInPile(drumHost, PileType.Exhaust);
		int energyBeforeDrum = drumFixture.Player.PlayerCombatState!.Energy;
		await CardEditorExtraEffects.RunBorrowedEffectSourceAfterCardExhausted(drumFixture.Combat, drumFixture.Choices, drumHost);
		AssertSame(energyBeforeDrum + 2, drumFixture.Player.PlayerCombatState.Energy,
			"DrumOfBattle: exhausting the host did not gain Energy");

		Fixture midnightFixture = CreateFixture();
		CardModel midnightHost = CreateCostlyHost(midnightFixture);
		GrantEffectSource(midnightFixture, midnightHost, typeof(MegaCrit.Sts2.Core.Models.Cards.Midnight));
		await PutInPile(midnightHost, PileType.Draw);
		Assert(midnightFixture.Player.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards).Contains(midnightHost),
			"Midnight: host was not present in the combat pile snapshot");
		Assert(CardEditorExtraEffects.CardHasRuntimeEffectKind(midnightHost, CardExtraEffectKind.RunEffectSourceCard),
			"Midnight: host lost its Run Effect Source row before the exhaust event");
		CardModel exhausted = CreateSourceCard(midnightFixture);
		int midnightCost = midnightHost.EnergyCost.GetWithModifiers(CostModifiers.Local);
		await CardEditorExtraEffects.RunBorrowedEffectSourceAfterCardExhausted(midnightFixture.Combat, midnightFixture.Choices, exhausted);
		AssertSame(midnightCost - 1, midnightHost.EnergyCost.GetWithModifiers(CostModifiers.Local),
			"Midnight: another exhausted card did not reduce host cost");
	}

	private static async Task TestRunEffectSourceCardPlayAndPhaseHooks()
	{
		Fixture bansheeFixture = CreateFixture();
		CardModel bansheeHost = CreateCostlyHost(bansheeFixture);
		GrantEffectSource(bansheeFixture, bansheeHost, typeof(MegaCrit.Sts2.Core.Models.Cards.BansheesCry));
		await PutInPile(bansheeHost, PileType.Draw);
		CardModel ethereal = bansheeFixture.Combat.CreateCard(
			ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.Apparition>(), bansheeFixture.Player);
		int bansheeCost = bansheeHost.EnergyCost.GetWithModifiers(CostModifiers.Local);
		await CardEditorExtraEffects.RunBorrowedEffectSourceAfterCardPlayedLate(
			bansheeFixture.Combat, bansheeFixture.Choices, CreateCardPlay(bansheeFixture, ethereal));
		AssertSame(Math.Max(0, bansheeCost - 2), bansheeHost.EnergyCost.GetWithModifiers(CostModifiers.Local),
			"BansheesCry: playing an Ethereal card did not reduce host cost");

		Fixture pinpointFixture = CreateFixture();
		CardModel pinpointHost = CreateCostlyHost(pinpointFixture);
		GrantEffectSource(pinpointFixture, pinpointHost, typeof(MegaCrit.Sts2.Core.Models.Cards.Pinpoint));
		await PutInPile(pinpointHost, PileType.Draw);
		CardModel skill = pinpointFixture.Combat.CreateCard(
			ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.DefendIronclad>(), pinpointFixture.Player);
		int pinpointCost = pinpointHost.EnergyCost.GetWithModifiers(CostModifiers.Local);
		await CardEditorExtraEffects.RunBorrowedEffectSourceAfterCardPlayedLate(
			pinpointFixture.Combat, pinpointFixture.Choices, CreateCardPlay(pinpointFixture, skill));
		AssertSame(pinpointCost - 1, pinpointHost.EnergyCost.GetWithModifiers(CostModifiers.Local),
			"Pinpoint: playing a Skill did not reduce host cost");

		Fixture makeItSoFixture = CreateFixture();
		CardModel makeItSoHost = CreateSourceCard(makeItSoFixture);
		GrantEffectSource(makeItSoFixture, makeItSoHost, typeof(MegaCrit.Sts2.Core.Models.Cards.MakeItSo));
		await PutInPile(makeItSoHost, PileType.Discard);
		CardModel playedSkill = makeItSoFixture.Combat.CreateCard(
			ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.DefendIronclad>(), makeItSoFixture.Player);
		CardPlay thirdSkillPlay = CreateCardPlay(makeItSoFixture, playedSkill);
		CombatManager.Instance.History.CardPlayFinished(makeItSoFixture.Combat, CreateCardPlay(makeItSoFixture, playedSkill));
		CombatManager.Instance.History.CardPlayFinished(makeItSoFixture.Combat, CreateCardPlay(makeItSoFixture, playedSkill));
		CombatManager.Instance.History.CardPlayFinished(makeItSoFixture.Combat, thirdSkillPlay);
		await CardEditorExtraEffects.RunBorrowedEffectSourceAfterCardPlayedLate(
			makeItSoFixture.Combat, makeItSoFixture.Choices, thirdSkillPlay);
		AssertSame(PileType.Hand, makeItSoHost.Pile?.Type ?? PileType.None,
			"MakeItSo: third Skill did not return the host to Hand");

		Fixture howlFixture = CreateFixture();
		CardModel howlHost = howlFixture.Combat.CreateCard(
			ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.DefendIronclad>(), howlFixture.Player);
		CardExtraEffect howlEffect = GrantEffectSource(howlFixture, howlHost, typeof(MegaCrit.Sts2.Core.Models.Cards.HowlFromBeyond));
		Creature howlEnemy = howlFixture.Combat.Enemies.First();
		int beforeHowl = howlEnemy.CurrentHp;
		await CardEditorExtraEffects.RunResolvedOnPlayEffectsDuringCardPlay(
			howlFixture.Combat, howlFixture.Choices, CreateCardPlay(howlFixture, howlHost), [howlEffect]);
		Assert(howlEnemy.CurrentHp < beforeHowl, "HowlFromBeyond: borrowed OnPlay did not damage all enemies");
		await PutInPile(howlHost, PileType.Exhaust);
		Assert(CardEditorExtraEffects.ShouldAutoPlayBorrowedHowlFromBeyond(howlFixture.Player, howlHost),
			"HowlFromBeyond: Exhaust-pile host was not eligible for auto-play");
	}

	private static async Task TestCopyDebuffs()
	{
		Fixture fixture = CreateFixture();
		CardModel sourceCard = CreateSourceCard(fixture);
		Creature sourceEnemy = AddMockEnemy(fixture, "copy-source");
		Creature destinationEnemy = AddMockEnemy(fixture, "copy-destination");
		await GamePowerCmd.Apply<WeakPower>(fixture.Choices, sourceEnemy, 2, fixture.Player.Creature, sourceCard, silent: true);

		CardExtraEffect effect = ImmediateOnPlay(CardExtraEffectKind.CopyDebuffs);
		effect.Target = CardExtraEffectTarget.AllEnemies;
		await CardEditorExtraEffects.RunResolvedOnPlayEffectsDuringCardPlay(
			fixture.Combat, fixture.Choices, CreateCardPlay(fixture, sourceCard, sourceEnemy), [effect]);

		AssertSame(2, sourceEnemy.GetPowerAmount<WeakPower>(), "copying changed the source enemy's Weak amount");
		AssertSame(2, destinationEnemy.GetPowerAmount<WeakPower>(), "destination enemy did not receive the copied Weak stacks");
	}

	private static async Task TestSelectedRowDraw()
	{
		Fixture fixture = CreateFixture();
		CardModel source = CreateSourceCard(fixture);
		CardModel selected = CreateSourceCard(fixture);
		CardModel sentinel = CreateSourceCard(fixture);
		await PutInPile(sentinel, PileType.Draw, CardPilePosition.Bottom);
		await PutInPile(selected, PileType.Draw, CardPilePosition.Top);

		CardExtraEffect select = ImmediateOnPlay(CardExtraEffectKind.SelectCardsFromPile);
		select.EffectId = "selected-row-source";
		select.CardSelectionPile = CardExtraEffectCardPile.DrawPile;
		select.CardSelectionMode = CardExtraEffectCardSelectionMode.Top;

		CardExtraEffect draw = ImmediateOnPlay(CardExtraEffectKind.DrawCards);
		draw.CardSelectionMode = CardExtraEffectCardSelectionMode.SelectedByEffect;
		draw.CardSelectionSourceEffectId = select.EffectId;

		await RunOnPlay(fixture, source, select, draw);

		AssertSame(PileType.Hand, selected.Pile?.Type, "Draw Cards did not consume the Selected Row result");
		AssertSame(PileType.Draw, sentinel.Pile?.Type, "Draw Cards ignored Selected Row and drew another card");
	}

	private static Creature AddMockEnemy(Fixture fixture, string slot)
	{
		Creature enemy = fixture.Combat.CreateCreature(ModelDb.Monster<MockAttackMonster>().ToMutable(), CombatSide.Enemy, slot);
		fixture.Combat.AddCreature(enemy);
		return enemy;
	}

	private static Task TestHitsAllGrantIsBlocked()
	{
		Assert(!CardEditorExtraEffects.SupportsGrantToCard(CardExtraEffectKind.HitsAllEnemies), "unsafe Hits All Enemies grant is no longer blocked");
		return Task.CompletedTask;
	}

	private static async Task TestAutoActionGrantPackage()
	{
		Assert(CardEditorExtraEffects.SupportsGrantToCard(CardExtraEffectKind.ConditionalAutoPlayFromPile),
			"Auto-Play is still excluded from card grants");
		Assert(CardEditorExtraEffects.SupportsGrantToCard(CardExtraEffectKind.ConditionalAutoDrawFromPile),
			"Auto-Draw is still excluded from card grants");
		Assert(CardEditorExtraEffects.SupportsGrantToCard(CardExtraEffectKind.ConditionalAutoRunEffects),
			"Auto-Run is still excluded from card grants");

		Fixture fixture = CreateFixture();
		CardModel source = CreateSourceCard(fixture);
		CardModel recipient = fixture.Combat.CreateCard(
			ModelDb.AllCards.First(card => card.IsTransformable && card.Type != CardType.Quest && card.Id != source.Id),
			fixture.Player);
		await PutInPile(source, PileType.Hand);
		await PutInPile(recipient, PileType.Hand);

		CardExtraEffect payload = ImmediateOnPlay(CardExtraEffectKind.GainEnergy, amount: 2);
		payload.EffectId = "auto-grant-payload";
		CardExtraEffect wrapper = ImmediateOnPlay(CardExtraEffectKind.ConditionalAutoRunEffects);
		wrapper.EffectId = "auto-grant-wrapper";
		wrapper.AutoActionEffectIds = payload.EffectId;
		wrapper.GrantToCard = true;
		wrapper.CardSelectionPile = CardExtraEffectCardPile.Hand;
		wrapper.CardSelectionMode = CardExtraEffectCardSelectionMode.Choose;
		wrapper.CardSelectionCount = 1;
		wrapper.CardGrantDuration = CardExtraEffectCardGrantDuration.ThisCombat;

		CardEditorOverrides.SetInstanceOverride(source, new CardOverride { ExtraEffects = [wrapper, payload] });
		try
		{
			IReadOnlyList<CardExtraEffect> packagePreview = CardEditorExtraEffects.BuildGrantedEffectPackage(fixture.Combat, source, wrapper);
			AssertSame(2, packagePreview.Count, "Auto-Run grant package did not include its selected payload row");
			TestCardSelector selector = new();
			selector.PrepareToSelect([recipient]);
			using IDisposable _ = CardSelectCmd.UseSelector(selector);
			await RunOnPlay(fixture, source, wrapper, payload);

			IReadOnlyList<CardExtraEffect> granted = CardEditorTemporaryExtraEffectController.GetEffects(fixture.Combat, recipient);
			string grantedKinds = string.Join(", ", granted.Select(effect => effect.Kind));
			CardExtraEffect grantedWrapper = granted.FirstOrDefault(effect => effect.Kind == CardExtraEffectKind.ConditionalAutoRunEffects)
				?? throw new InvalidOperationException($"recipient did not receive the Auto-Run wrapper; stored kinds: {grantedKinds}");
			CardExtraEffect grantedPayload = granted.FirstOrDefault(effect => effect.Kind == CardExtraEffectKind.GainEnergy)
				?? throw new InvalidOperationException($"recipient did not receive the Auto-Run payload; stored kinds: {grantedKinds}");
			Assert(!grantedWrapper.GrantToCard, "granted Auto-Run wrapper recursively retained Grant");
			Assert(grantedPayload.PayloadOnly, "granted Auto-Run payload can fire independently");
			Assert(!string.Equals(grantedWrapper.EffectId, wrapper.EffectId, StringComparison.Ordinal),
				"granted Auto-Run package reused source row IDs");
			AssertSame(grantedPayload.EffectId, grantedWrapper.AutoActionEffectIds,
				"granted Auto-Run wrapper did not point at its remapped payload");
		}
		finally
		{
			CardEditorOverrides.SetInstanceOverride(source, null);
		}
	}

	private static async Task TestCustomKeywordGrantPackage()
	{
		IReadOnlyList<CardEditorCustomKeywordDefinition> originalKeywords = CardEditorDefinitionStore.GetKeywordDefinitions();
		IReadOnlyList<CardEditorCustomStatusDefinition> originalStatuses = CardEditorDefinitionStore.GetStatusDefinitions();
		bool previousPersistence = CardEditorDefinitionStore.PersistenceSuspended;
		CardEditorDefinitionStore.PersistenceSuspended = true;
		const string keywordName = "Harness Retain Burst";
		try
		{
			CardExtraEffect behavior = ImmediateOnPlay(CardExtraEffectKind.GainBlock, amount: 4);
			behavior.EffectId = "custom-keyword-behavior";
			CardEditorDefinitionStore.ReplaceDefinitions(
				originalKeywords.Concat([
					new CardEditorCustomKeywordDefinition
					{
						Id = CardEditorDefinitionStore.BuildKeywordId(keywordName)!,
						Name = keywordName,
						Description = "Gain 4 Block.",
						Effects = [behavior]
					}
				]),
				originalStatuses);

			Fixture fixture = CreateFixture();
			CardModel source = CreateSourceCard(fixture);
			await PutInPile(source, PileType.Hand);
			CardExtraEffect grant = ImmediateOnPlay(CardExtraEffectKind.GrantKeywordToPile);
			grant.CustomKeywordName = keywordName;
			grant.CardSelectionPile = CardExtraEffectCardPile.Hand;
			grant.CardSelectionMode = CardExtraEffectCardSelectionMode.ThisCard;
			grant.IncludeSourceCardInSelection = true;
			grant.CardGrantDuration = CardExtraEffectCardGrantDuration.ThisCombat;
			await RunOnPlay(fixture, source, grant);

			IReadOnlyList<CardExtraEffect> granted = CardEditorTemporaryExtraEffectController.GetEffects(fixture.Combat, source);
			CardExtraEffect stored = granted.Single(effect =>
				string.Equals(effect.CustomKeywordName, keywordName, StringComparison.OrdinalIgnoreCase));
			AssertSame(CardExtraEffectKind.GainBlock, stored.Kind, "custom keyword granted the wrong behavior row");
			AssertSame(4, stored.Amount, "custom keyword lost its configured behavior amount");
			Assert(!string.IsNullOrWhiteSpace(stored.GrantPackageKey), "custom keyword package has no stable duplicate key");
		}
		finally
		{
			CardEditorDefinitionStore.ReplaceDefinitions(originalKeywords, originalStatuses);
			CardEditorDefinitionStore.PersistenceSuspended = previousPersistence;
		}
	}

	private static Task TestCardlessDamageDoesNotInheritCardBonus()
	{
		Fixture fixture = CreateFixture();
		CardModel source = CreateSourceCard(fixture);
		CardExtraEffect bonus = ImmediateOnPlay(CardExtraEffectKind.CardDealsExtraDamage, amount: 7);
		CardEditorTemporaryExtraEffectController.Grant(
			fixture.Combat, source, bonus, CardExtraEffectCardGrantDuration.ThisCombat, turns: 1);

		decimal explicitCardDamage = 3m;
		Hook_ModifyDamageInternal_CardDamageBonus_Patch.Prefix(
			fixture.Combat,
			fixture.Combat.Enemies.First(),
			fixture.Player.Creature,
			ref explicitCardDamage,
			ValueProp.Move,
			source);
		AssertSame(10m, explicitCardDamage, "an explicit card source did not receive its card damage bonus");

		decimal cardlessRetaliation = 3m;
		using (CardEditorCardPlayContext.PushScoped(CreateCardPlay(fixture, source)))
		{
			Hook_ModifyDamageInternal_CardDamageBonus_Patch.Prefix(
				fixture.Combat,
				fixture.Player.Creature,
				fixture.Combat.Enemies.First(),
				ref cardlessRetaliation,
				ValueProp.Move,
				cardSource: null);
		}
		AssertSame(3m, cardlessRetaliation, "cardless retaliation inherited the ambient played card's damage bonus");
		return Task.CompletedTask;
	}

	private static async Task TestVanillaTotalDamageUsesActualResults()
	{
		Fixture fixture = CreateFixture();
		Creature firstEnemy = fixture.Combat.Enemies.First();
		Creature secondEnemy = AddMockEnemy(fixture, "vanilla-total-damage-second-target");
		CardModel source = fixture.Combat.CreateCard(
			ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.AstralPulse>(), fixture.Player);
		await PutInPile(source, PileType.Hand);
		CardPlay cardPlay = CreateCardPlay(fixture, source);

		await GamePowerCmd.Apply<VulnerablePower>(
			fixture.Choices, firstEnemy, 2, fixture.Player.Creature, source, silent: true);
		await GamePowerCmd.Apply<VigorPower>(
			fixture.Choices, fixture.Player.Creature, 3, fixture.Player.Creature, source, silent: true);

		int firstHpBefore = firstEnemy.CurrentHp;
		int secondHpBefore = secondEnemy.CurrentHp;
		using (CardEditorCardPlayContext.PushScoped(cardPlay))
		{
			MethodInfo onPlay = AccessTools.Method(
				source.GetType(),
				"OnPlay",
				[typeof(PlayerChoiceContext), typeof(CardPlay)])
				?? throw new MissingMethodException(source.GetType().FullName, "OnPlay");
			Task task;
			using (CardEditorReflectiveOnPlayGuard.PushScoped())
			{
				task = onPlay.Invoke(source, [fixture.Choices, cardPlay]) as Task
					?? throw new InvalidOperationException("Vanilla card OnPlay did not return a Task.");
			}
			await task;
		}
		int firstDamage = firstHpBefore - firstEnemy.CurrentHp;
		int secondDamage = secondHpBefore - secondEnemy.CurrentHp;
		int actualVanillaTotal = firstDamage + secondDamage;
		Assert(actualVanillaTotal > 0, "the vanilla AoE attack did not deal damage in the beta fixture");
		Assert(firstDamage > secondDamage, "target-specific Vulnerable did not affect the vanilla AoE result");

		CardExtraEffect laterExtraDamage = ImmediateOnPlay(CardExtraEffectKind.DealDamage, amount: 1);
		laterExtraDamage.Target = CardExtraEffectTarget.AllEnemies;
		CardExtraEffect gainBlock = ImmediateOnPlay(CardExtraEffectKind.GainBlock);
		gainBlock.AmountSourceMode = CardExtraEffectAmountSourceMode.AppliedEffectTotalDamage;
		gainBlock.AmountSourceEffectId = CardEditorExtraEffects.EncodeVanillaDynamicAmountSource("Damage");

		int blockBefore = fixture.Player.Creature.Block;
		await CardEditorExtraEffects.RunResolvedOnPlayEffectsDuringCardPlay(
			fixture.Combat, fixture.Choices, cardPlay, [laterExtraDamage, gainBlock]);
		AssertSame(
			actualVanillaTotal,
			fixture.Player.Creature.Block - blockBefore,
			"Total Damage did not use the completed vanilla AoE results, or included a later extra-effect hit");

		CardExtraEffect appliedBlock = ImmediateOnPlay(CardExtraEffectKind.GainBlock);
		appliedBlock.AmountSourceMode = CardExtraEffectAmountSourceMode.AppliedEffectRow;
		appliedBlock.AmountSourceEffectId = CardEditorExtraEffects.EncodeVanillaDynamicAmountSource("Damage");
		blockBefore = fixture.Player.Creature.Block;
		await CardEditorExtraEffects.RunResolvedOnPlayEffectsDuringCardPlay(
			fixture.Combat, fixture.Choices, cardPlay, [appliedBlock]);
		AssertSame(
			actualVanillaTotal,
			fixture.Player.Creature.Block - blockBefore,
			"Applied Effect on Vanilla: Damage recomputed the value instead of using the landed Vigor/Vulnerable hits");
	}

	private static async Task TestPowerExpiryAndCleanReapply()
	{
		Fixture fixture = CreateFixture();
		CardModel source = CreateSourceCard(fixture);
		CardExtraEffect fireLimited = ImmediateOnPlay(CardExtraEffectKind.GainEnergy, amount: 2);
		fireLimited.AsPower = true;
		fireLimited.Trigger = CardExtraEffectTrigger.OnCountEvent;
		fireLimited.PowerTriggerCountEvent = CardExtraEffectCountEvent.Played;
		fireLimited.TriggerMaxFires = 1;
		fireLimited.PowerStackMode = CardExtraEffectPowerStackMode.Merge;

		CardEditorExtraEffectPower power = await GamePowerCmd.Apply<CardEditorExtraEffectPower>(
			fixture.Choices, fixture.Player.Creature, 1, fixture.Player.Creature, source, silent: true)
			?? throw new InvalidOperationException("could not install Card Editor power carrier");
		await power.AddPowerEffects(source, [fireLimited], CreateCardPlay(fixture, source));
		AssertSame(1, GetPowerEntryCount(power), "new fire-limited power did not create exactly one entry");
		AssertSame(1, GetVisiblePowerMirrorCount(fixture.Player.Creature), "new fire-limited power did not create exactly one mirror");

		int energyBefore = fixture.Player.PlayerCombatState!.Energy;
		CardModel trigger = CreateSourceCard(fixture);
		await power.AfterCardPlayed(fixture.Choices, CreateCardPlay(fixture, trigger));
		AssertSame(energyBefore + 2, fixture.Player.PlayerCombatState.Energy, "fire-limited power applied the wrong amount");
		AssertSame(0, GetPowerEntryCount(power), "max-fire expiry left its listener entry installed");
		AssertSame(0, GetVisiblePowerMirrorCount(fixture.Player.Creature), "max-fire expiry left a stale visible mirror");

		await power.AddPowerEffects(source, [fireLimited], CreateCardPlay(fixture, source));
		AssertSame(1, GetPowerEntryCount(power), "reapplying an expired power revived an old entry");
		energyBefore = fixture.Player.PlayerCombatState.Energy;
		await power.AfterCardPlayed(fixture.Choices, CreateCardPlay(fixture, trigger));
		AssertSame(energyBefore + 2, fixture.Player.PlayerCombatState.Energy, "reapplied power included dormant prior potency");
		AssertSame(0, GetPowerEntryCount(power), "reapplied max-fire power did not expire cleanly");

		CardExtraEffect turnLimited = ImmediateOnPlay(CardExtraEffectKind.GainEnergy);
		turnLimited.AsPower = true;
		turnLimited.Trigger = CardExtraEffectTrigger.EndOfTurn;
		turnLimited.TriggerMaxTurns = 1;
		turnLimited.PowerStackMode = CardExtraEffectPowerStackMode.Separate;
		await power.AddPowerEffects(source, [turnLimited], CreateCardPlay(fixture, source));
		AssertSame(1, GetPowerEntryCount(power), "turn-limited power did not create exactly one entry");
		await power.AfterSideTurnEnd(fixture.Choices, fixture.Player.Creature.Side, [fixture.Player.Creature]);
		AssertSame(0, GetPowerEntryCount(power), "turn expiry left its listener entry installed");
		AssertSame(0, GetVisiblePowerMirrorCount(fixture.Player.Creature), "turn expiry left a stale visible mirror");

		await power.AddPowerEffects(source, [turnLimited], CreateCardPlay(fixture, source));
		AssertSame(1, GetPowerEntryCount(power), "reapplying a turn-expired power revived an old entry");
	}

	private static int GetPowerEntryCount(CardEditorExtraEffectPower power)
	{
		object entries = typeof(CardEditorExtraEffectPower)
			.GetProperty("Entries", BindingFlags.Instance | BindingFlags.NonPublic)!
			.GetValue(power)!;
		return ((System.Collections.IEnumerable)entries).Cast<object>().Count();
	}

	private static List<CardExtraEffect> GetStoredPowerEffects(CardEditorExtraEffectPower power)
	{
		object entries = typeof(CardEditorExtraEffectPower)
			.GetProperty("Entries", BindingFlags.Instance | BindingFlags.NonPublic)!
			.GetValue(power)!;
		return ((System.Collections.IEnumerable)entries)
			.Cast<object>()
			.Select(entry => entry.GetType().GetProperty("Effect")?.GetValue(entry) as CardExtraEffect
				?? throw new InvalidOperationException("power entry did not expose its stored effect"))
			.ToList();
	}

	private static int GetVisiblePowerMirrorCount(Creature owner)
		=> owner.Powers.OfType<CardEditorVisibleExtraEffectPower>().Count();

	private static async Task TestPowerMergeContract()
	{
		Fixture fixture = CreateFixture();
		CardModel source = CreateSourceCard(fixture);
		CardExtraEffect merged = ImmediateOnPlay(CardExtraEffectKind.GainBlock, amount: 3);
		merged.AsPower = true;
		merged.Trigger = CardExtraEffectTrigger.OnCountEvent;
		merged.PowerTriggerCountEvent = CardExtraEffectCountEvent.Played;
		merged.PowerStackMode = CardExtraEffectPowerStackMode.Merge;

		CardEditorExtraEffectPower power = await GamePowerCmd.Apply<CardEditorExtraEffectPower>(
			fixture.Choices, fixture.Player.Creature, 1, fixture.Player.Creature, source, silent: true)
			?? throw new InvalidOperationException("could not install Card Editor power carrier");
		await power.AddPowerEffects(source, [merged], CreateCardPlay(fixture, source));
		await power.AddPowerEffects(source, [merged], CreateCardPlay(fixture, source));
		AssertSame(1, GetPowerEntryCount(power), "Merge created multiple listener entries");

		int blockBefore = fixture.Player.Creature.Block;
		await power.AfterCardPlayed(fixture.Choices, CreateCardPlay(fixture, CreateSourceCard(fixture)));
		AssertSame(blockBefore + 6, fixture.Player.Creature.Block,
			"two merged stacks did not contribute two activations of three Block");
	}

	private static async Task TestPowerPayloadsStayUnpowered()
	{
		Fixture fixture = CreateFixture();
		CardModel source = CreateSourceCard(fixture);
		await GamePowerCmd.Apply<DexterityPower>(
			fixture.Choices, fixture.Player.Creature, 5, fixture.Player.Creature, source, silent: true);

		CardExtraEffect branchedPower = ImmediateOnPlay(CardExtraEffectKind.GainEnergy);
		branchedPower.EffectId = "harness-branched-power";
		branchedPower.AsPower = true;
		branchedPower.Trigger = CardExtraEffectTrigger.OnCountEvent;
		branchedPower.PowerTriggerCountEvent = CardExtraEffectCountEvent.Played;
		branchedPower.PowerStackMode = CardExtraEffectPowerStackMode.Separate;
		branchedPower.BranchMode = CardExtraEffectBranchMode.AlsoIf;
		branchedPower.BranchConditionType = CardExtraEffectBranchConditionType.TargetCheck;
		branchedPower.BranchCondition = CardExtraEffectConditionalBonusCondition.SelfHasNoBlock;
		branchedPower.BranchEffect = ImmediateOnPlay(CardExtraEffectKind.GainBlock, amount: 3);

		CardEditorExtraEffectPower power = await GamePowerCmd.Apply<CardEditorExtraEffectPower>(
			fixture.Choices, fixture.Player.Creature, 1, fixture.Player.Creature, source, silent: true)
			?? throw new InvalidOperationException("could not install Card Editor power carrier");
		await power.AddPowerEffects(source, [branchedPower], CreateCardPlay(fixture, source));

		CardExtraEffect storedBranchPower = GetStoredPowerEffects(power)
			.Single(effect => effect.EffectId == "harness-branched-power");
		Assert(storedBranchPower.BranchEffect?.DeferredPowerAmountIsUnpowered == true,
			"nested power Block payload was not marked unpowered");
		Assert(branchedPower.BranchEffect?.DeferredPowerAmountIsUnpowered == false,
			"power installation mutated the configured source effect");

		int blockBefore = fixture.Player.Creature.Block;
		await power.AfterCardPlayed(fixture.Choices, CreateCardPlay(fixture, CreateSourceCard(fixture)));
		AssertSame(blockBefore + 3, fixture.Player.Creature.Block,
			"nested power Block payload inherited live Dexterity instead of keeping its configured amount");

		CardExtraEffect customStatusBehavior = ImmediateOnPlay(CardExtraEffectKind.DealDamage, amount: 2);
		customStatusBehavior.EffectId = "harness-custom-status-damage";
		customStatusBehavior.AsPower = true;
		customStatusBehavior.Trigger = CardExtraEffectTrigger.OnCountEvent;
		customStatusBehavior.PowerTriggerCountEvent = CardExtraEffectCountEvent.Played;
		customStatusBehavior.BranchMode = CardExtraEffectBranchMode.AlsoIf;
		customStatusBehavior.BranchConditionType = CardExtraEffectBranchConditionType.TargetCheck;
		customStatusBehavior.BranchCondition = CardExtraEffectConditionalBonusCondition.SelfHasNoBlock;
		customStatusBehavior.BranchEffect = ImmediateOnPlay(CardExtraEffectKind.GainBlock, amount: 2);

		await power.AddCustomStatusBehaviorEffects(
			source, "harness-custom-status", [customStatusBehavior], stacks: 1);
		CardExtraEffect storedCustomStatusBehavior = GetStoredPowerEffects(power)
			.Single(effect => effect.EffectId == "harness-custom-status-damage");
		Assert(storedCustomStatusBehavior.DeferredPowerAmountIsUnpowered,
			"custom-status damage payload was not marked unpowered");
		Assert(storedCustomStatusBehavior.BranchEffect?.DeferredPowerAmountIsUnpowered == true,
			"nested custom-status Block payload was not marked unpowered");

		CardExtraEffect delayed = ImmediateOnPlay(CardExtraEffectKind.DealDamage, amount: 5);
		delayed.BranchEffect = ImmediateOnPlay(CardExtraEffectKind.GainBlock, amount: 6);
		CardExtraEffect nonCardClone = CardEditorExtraEffects.CloneForNonCardExecution(CreateCardPlay(fixture, source), delayed);
		Assert(nonCardClone.DeferredPowerAmountIsUnpowered,
			"delayed/relic/quest damage payload was not marked unpowered");
		Assert(nonCardClone.BranchEffect?.DeferredPowerAmountIsUnpowered == true,
			"nested delayed/relic/quest Block payload was not marked unpowered");
		Assert(!delayed.DeferredPowerAmountIsUnpowered && delayed.BranchEffect?.DeferredPowerAmountIsUnpowered == false,
			"non-card execution cloning mutated the configured source effect");
	}

	private static async Task TestCustomStatusPassiveStatLifetime()
	{
		IReadOnlyList<CardEditorCustomKeywordDefinition> originalKeywords = CardEditorDefinitionStore.GetKeywordDefinitions();
		IReadOnlyList<CardEditorCustomStatusDefinition> originalStatuses = CardEditorDefinitionStore.GetStatusDefinitions();
		bool previousPersistence = CardEditorDefinitionStore.PersistenceSuspended;
		CardEditorDefinitionStore.PersistenceSuspended = true;
		const string statusName = "Harness Enfeebled";
		string statusId = CardEditorCustomStatusRegistry.BuildId(statusName)
			?? throw new InvalidOperationException("could not build custom status id");

		try
		{
			CardExtraEffect passiveStrength = ImmediateOnPlay(CardExtraEffectKind.LoseStrength, amount: 2);
			passiveStrength.EffectId = "harness-passive-strength";
			passiveStrength.AsPower = true;
			passiveStrength.Trigger = CardExtraEffectTrigger.WhilePowerActive;
			passiveStrength.Target = CardExtraEffectTarget.Self;

			CardEditorDefinitionStore.ReplaceDefinitions(
				originalKeywords,
				originalStatuses.Concat([
					new CardEditorCustomStatusDefinition
					{
						Id = statusId,
						Name = statusName,
						Description = "The bearer has -2 Strength while this Power is active.",
						Type = PowerType.Debuff,
						BehaviorEffects = [passiveStrength]
					}
				]));

			Fixture fixture = CreateFixture();
			CardModel source = CreateSourceCard(fixture);
			await CardEditorExtraEffects.ApplyConfiguredPowerForCardEditor(
				fixture.Player.Creature, statusId, 1, fixture.Player.Creature, source);

			CardEditorCustomStatusPower status = fixture.Player.Creature.Powers
				.OfType<CardEditorCustomStatusPower>()
				.Single(power => power.MatchesConfiguredId(statusId));
			AssertSame(-2, fixture.Player.Creature.GetPower<StrengthPower>()?.Amount ?? 0,
				"initial passive custom status did not reduce Strength");
			Assert(fixture.Player.Creature.GetPower<CardEditorExtraEffectPower>() == null,
				"passive custom status incorrectly installed an event-listener carrier");

			await GamePowerCmd.ModifyAmount(
				fixture.Choices, status, 1, fixture.Player.Creature, source, silent: true);
			AssertSame(2, status.Amount, "custom status did not gain its second stack");
			AssertSame(-4, fixture.Player.Creature.GetPower<StrengthPower>()?.Amount ?? 0,
				"passive Strength did not scale with the custom status stack count");

			await GamePowerCmd.ModifyAmount(
				fixture.Choices, status, -1, fixture.Player.Creature, source, silent: true);
			AssertSame(-2, fixture.Player.Creature.GetPower<StrengthPower>()?.Amount ?? 0,
				"reducing the custom status stack did not restore its Strength contribution");

			await GamePowerCmd.Remove(status);
			AssertSame(0, fixture.Player.Creature.GetPower<StrengthPower>()?.Amount ?? 0,
				"removing the custom status did not restore Strength");
		}
		finally
		{
			CardEditorDefinitionStore.ReplaceDefinitions(originalKeywords, originalStatuses);
			CardEditorDefinitionStore.PersistenceSuspended = previousPersistence;
		}
	}

	private static Task TestEffectRegistrySelectionContracts()
	{
		AssertSame(0, CardEditorEffectKindRegistry.RunAudits(),
			"effect capability registry reported a startup contract mismatch");

		foreach (CardExtraEffectKind kind in Enum.GetValues<CardExtraEffectKind>())
		{
			bool runtimePublisher = CardEditorExtraEffects.SupportsCardSelectionPublishingAtRuntime(kind);
			AssertSame(runtimePublisher, CardEditorEffectKindRegistry.CanPublishCards(kind),
				$"{kind} selection publishing differs between runtime and UI");
			AssertSame(runtimePublisher, CardEditorEffectKindRegistry.Has(kind, EffectCaps.PublishesCardsUi),
				$"{kind} runtime publisher is not exposed by the UI");
			AssertSame(
				CardEditorExtraEffects.SupportsSelectedByEffectAtRuntime(kind),
				CardEditorEffectKindRegistry.CanConsumeCards(kind),
				$"{kind} Selected Row consumer capability differs from runtime");
		}

		return Task.CompletedTask;
	}

	private static Task TestCurrentTurnUsesPlayerTurn()
	{
		Fixture fixture = CreateFixture();
		CardModel source = CreateSourceCard(fixture);
		typeof(PlayerCombatState).GetProperty(nameof(PlayerCombatState.TurnNumber))!.SetValue(
			fixture.Player.PlayerCombatState, 7);
		CardExtraEffect effect = ImmediateOnPlay(CardExtraEffectKind.GainBlock);
		effect.CountEvent = CardExtraEffectCountEvent.CurrentTurnNumber;
		int count = CardEditorExtraEffects.GetHistoryCountMultiplierForCardPlay(
			fixture.Combat,
			fixture.Player.Creature,
			CreateCardPlay(fixture, source),
			effect);
		AssertSame(7, count, "Current Turn Number used the combat round instead of the owner's turn");
		return Task.CompletedTask;
	}

	private static async Task TestAmountlessPowerTargetAcquisition()
	{
		Fixture fixture = CreateFixture();
		CardModel source = CreateSourceCard(fixture);
		CardExtraEffect amountless = ImmediateOnPlay(CardExtraEffectKind.CleanseDebuffs, amount: 0);
		amountless.AsPower = true;
		amountless.Target = CardExtraEffectTarget.Target;
		amountless.PowerTargeting = CardExtraEffectPowerTargeting.TriggerTarget;
		amountless.PowerStackMode = CardExtraEffectPowerStackMode.Separate;

		CardEditorExtraEffectPower power = await GamePowerCmd.Apply<CardEditorExtraEffectPower>(
			fixture.Choices, fixture.Player.Creature, 1, fixture.Player.Creature, source, silent: true)
			?? throw new InvalidOperationException("could not install amountless target test carrier");
		await power.AddPowerEffects(source, [amountless], CreateCardPlay(fixture, source));
		Assert(power.HasOnPlayTargetEffects(),
			"zero-amount Cleanse Debuffs power was filtered out before target acquisition");
	}

	private static async Task TestStarsSpentPublicHook()
	{
		Fixture fixture = CreateFixture();
		CardModel source = CreateSourceCard(fixture);
		CardExtraEffect effect = ImmediateOnPlay(CardExtraEffectKind.GainEnergy, amount: 1);
		effect.AsPower = true;
		effect.Trigger = CardExtraEffectTrigger.OnCountEvent;
		effect.PowerTriggerCountEvent = CardExtraEffectCountEvent.StarsSpent;
		effect.PowerStackMode = CardExtraEffectPowerStackMode.Separate;

		CardEditorExtraEffectPower power = await GamePowerCmd.Apply<CardEditorExtraEffectPower>(
			fixture.Choices, fixture.Player.Creature, 1, fixture.Player.Creature, source, silent: true)
			?? throw new InvalidOperationException("could not install Stars Spent test carrier");
		await power.AddPowerEffects(source, [effect], CreateCardPlay(fixture, source));
		int before = fixture.Player.PlayerCombatState!.Energy;
		ulong? previousNetId = LocalContext.NetId;
		LocalContext.NetId = fixture.Player.NetId;
		try
		{
			await Hook.AfterStarsSpent(fixture.Combat, 2, fixture.Player);
			AssertSame(before + 1, fixture.Player.PlayerCombatState.Energy,
				"public Hook.AfterStarsSpent did not dispatch the configured count event amount");
		}
		finally
		{
			LocalContext.NetId = previousNetId;
		}
	}

	private static async Task TestEnemyDiedRelicTrigger()
	{
		Fixture fixture = CreateFixture();
		RelicModel relic = ModelDb.AllRelics.First().ToMutable();
		fixture.Player.AddRelicInternal(relic, silent: true);
		ModelId relicId = relic.CanonicalInstance?.Id ?? relic.Id;
		RelicOverride? original = CardEditorRelicOverrides.Get(relicId);
		CardExtraEffect gain = ImmediateOnPlay(CardExtraEffectKind.GainEnergy, amount: 3);
		CardEditorRelicOverrides.Set(relicId, new RelicOverride
		{
			ExtraEffects = [new RelicEffectEntry { Trigger = RelicTriggerKind.OnEnemyDied, Effect = gain }],
			EffectTriggers = [RelicTriggerKind.OnEnemyDied]
		});
		try
		{
			int before = fixture.Player.PlayerCombatState!.Energy;
			ulong? previousNetId = LocalContext.NetId;
			LocalContext.NetId = fixture.Player.NetId;
			try
			{
				Task hookResult = Task.CompletedTask;
				Hook_AfterDeath_CardEditorRelicEffects_Patch.Postfix(
					fixture.Combat, fixture.Combat.Enemies.First(), ref hookResult);
				await hookResult;
				AssertSame(before + 3, fixture.Player.PlayerCombatState.Energy,
					"an unattributed enemy death did not fire the all-deaths relic trigger");
			}
			finally
			{
				LocalContext.NetId = previousNetId;
			}
		}
		finally
		{
			CardEditorRelicOverrides.Set(relicId, original);
		}
	}

	private static Task TestApplyPoisonPowerSupport()
	{
		Assert(CardEditorExtraEffects.Definitions.Any(definition => definition.Kind == CardExtraEffectKind.ApplyPoison),
			"Apply Poison is missing from the effect registry");
		Assert(CardEditorExtraEffects.SupportsAsPower(CardExtraEffectKind.ApplyPoison),
			"Apply Poison cannot be enabled as a Whenever power effect");
		return Task.CompletedTask;
	}

	private static async Task TestBearerHitByAttack()
	{
		Fixture fixture = CreateFixture();
		CardModel source = CreateSourceCard(fixture);
		CardExtraEffect effect = ImmediateOnPlay(CardExtraEffectKind.GainEnergy, amount: 2);
		effect.AsPower = true;
		effect.Trigger = CardExtraEffectTrigger.BearerHitByAttack;
		effect.PowerStackMode = CardExtraEffectPowerStackMode.Separate;

		CardEditorExtraEffectPower power = await GamePowerCmd.Apply<CardEditorExtraEffectPower>(
			fixture.Choices, fixture.Player.Creature, 1, fixture.Player.Creature, source, silent: true)
			?? throw new InvalidOperationException("could not install Card Editor power carrier");
		await power.AddPowerEffects(source, [effect], CreateCardPlay(fixture, source));

		Creature enemy = fixture.Combat.Enemies.First();
		int energyBefore = fixture.Player.PlayerCombatState!.Energy;
		var unrelatedAttack = DamageCmd.Attack(1m).FromMonster(enemy.Monster!);
		unrelatedAttack.AddResultsInternal(
		[
			new DamageResult(enemy, ValueProp.Move)
			{
				BlockedDamage = 1,
				UnblockedDamage = 0,
				WasFullyBlocked = true
			}
		]);
		await power.AfterAttack(fixture.Choices, unrelatedAttack);
		AssertSame(energyBefore, fixture.Player.PlayerCombatState.Energy,
			"bearer-hit trigger fired when a different creature received the attack");

		var blockedHit = DamageCmd.Attack(1m).FromMonster(enemy.Monster!);
		blockedHit.AddResultsInternal(
		[
			new DamageResult(fixture.Player.Creature, ValueProp.Move)
			{
				BlockedDamage = 1,
				UnblockedDamage = 0,
				WasFullyBlocked = true
			}
		]);
		await power.AfterAttack(fixture.Choices, blockedHit);
		AssertSame(energyBefore + 2, fixture.Player.PlayerCombatState.Energy,
			"bearer-hit trigger ignored a fully blocked attack received by its owner");
	}

	private static Task TestAttackHitVfxBinding()
	{
		Fixture fixture = CreateFixture();
		CardModel source = fixture.Combat.CreateCard(
			ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.Bludgeon>(), fixture.Player);
		CardExtraEffect extraDamage = ImmediateOnPlay(CardExtraEffectKind.DealDamage, amount: 3);
		CardOverride overrideData = new()
		{
			CosmeticVfxPreset = CardEditorCosmeticVfxPreset.AttackSlash,
			CosmeticVfxAttach = CardEditorCosmeticAttach.Target,
			ExtraEffects = [extraDamage]
		};
		CardEditorOverrides.SetInstanceOverride(source, overrideData);
		try
		{
			Assert(CardEditorCosmetics.ShouldBindVfxToAttack(
				source,
				overrideData,
				CardEditorCosmeticVfxPreset.AttackSlash,
				CardEditorCosmeticAttach.Target),
				"target VFX was not recognized as attack-bound for a damaging card");

			var attack = DamageCmd.Attack(3m)
				.FromCard(source, CreateCardPlay(fixture, source, fixture.Combat.Enemies.First()))
				.WithHitCount(3)
				.Targeting(fixture.Combat.Enemies.First());
			CardEditorCosmetics.ConfigureAttackHitVfx(attack);
			AssertSame(VfxCmd.slashPath, attack.HitVfx,
				"selected VFX was not attached to the beta AttackCommand hit loop");
		}
		finally
		{
			CardEditorOverrides.SetInstanceOverride(source, null);
		}

		return Task.CompletedTask;
	}

	private static Task TestDirectDamageVfxHooks()
	{
		string root = FindRepoRoot();
		string effectsSource = File.ReadAllText(Path.Combine(root, "mods", "card_editor", "CardEditorExtraEffects.cs"));
		AssertSame(4, CountOccurrences(effectsSource, "CardEditorCosmetics.PlayConfiguredHitVfx("),
			"not every direct Deal Damage executor binds VFX at its authoritative hit seam");
		return Task.CompletedTask;
	}

	private static Task TestMultiplayerClientReady()
	{
		const ulong localId = 2000;
		RecordingClientNetGameService service = new(localId);
		RecordingStartRunLobbyListener listener = new();
		StartRunLobby lobby = new(GameMode.Standard, service, listener, maxPlayers: 2);
		SerializableUnlockState unlocks = new UnlockState(SaveManager.Instance.Progress).ToSerializable();
		CharacterModel character = ModelDb.Character<Ironclad>();
		lobby.Players.Add(new StartRunLobbyPlayer
		{
			id = 1,
			slotId = 0,
			character = character,
			unlockState = unlocks,
			maxMultiplayerAscensionUnlocked = 0,
			isReady = false
		});
		lobby.Players.Add(new StartRunLobbyPlayer
		{
			id = localId,
			slotId = 1,
			character = character,
			unlockState = unlocks,
			maxMultiplayerAscensionUnlocked = 0,
			isReady = false
		});

		SetPrivateStaticField(typeof(CardEditorMultiplayerSettings), "_loaded", true);
		SetPrivateStaticField(typeof(CardEditorMultiplayerSettings), "_data", new CardEditorMultiplayerSettingsData());
		SetPrivateStaticField(typeof(CardEditorMultiplayerSync), "_netService", service);
		SetPrivateStaticField(typeof(CardEditorMultiplayerSync), "_lastAppliedSequence", 0);
		SetPrivateStaticField(typeof(CardEditorMultiplayerSync), "_requestedInitialSync", false);
		SetPrivateStaticField(typeof(CardEditorMultiplayerSync), "_pendingClientReady", false);
		SetPrivateStaticField(typeof(CardEditorMultiplayerSync), "_pendingReadyAction", null);
		try
		{
			bool originalAllowed = CardEditorMultiplayerSync.AllowClientReady(() => lobby.SetReady(true), ready: true);
			Assert(!originalAllowed, "client Ready was allowed before the authoritative editor snapshot arrived");
			Assert(!lobby.LocalPlayer.isReady, "client was marked ready before the authoritative editor snapshot arrived");
			Assert(!service.SentMessageTypes.Contains(typeof(LobbyPlayerSetReadyMessage)),
				"client sent vanilla Ready before the authoritative editor snapshot arrived");
			AssertSame(1, service.SentMessageTypes.Count(type => type == typeof(CardEditorMultiplayerSyncRequestMessage)),
				"held Ready did not immediately request exactly one authoritative snapshot");
			Assert(GetPrivateStaticField<bool>(typeof(CardEditorMultiplayerSync), "_pendingClientReady"),
				"held Ready was not retained for replay after synchronization");

			// Applying a full snapshot touches Godot's user:// filesystem, which is intentionally outside
			// this plain .NET harness. Simulate the exact successful OnSnapshotReceived transition, then
			// invoke the same completion function called by that handler and by Update.
			SetPrivateStaticField(typeof(CardEditorMultiplayerSync), "_lastAppliedSequence", 1);
			MethodInfo firePendingReady = typeof(CardEditorMultiplayerSync).GetMethod(
				"FirePendingReadyIfNeeded",
				BindingFlags.Static | BindingFlags.NonPublic)!;
			firePendingReady.Invoke(null, null);

			Assert(lobby.LocalPlayer.isReady, "the vanilla lobby did not mark the local client ready");
			AssertSame(1, service.SentMessageTypes.Count(type => type == typeof(LobbyPlayerSetReadyMessage)),
				"the deferred Ready did not send exactly one vanilla LobbyPlayerSetReadyMessage to the host");
			AssertSame(1, listener.PlayerChangedCount, "the lobby UI listener did not receive the Ready state change");
			AssertSame(1, GetPrivateStaticField<int>(typeof(CardEditorMultiplayerSync), "_lastAppliedSequence"),
				"the authoritative snapshot was not recorded before Ready replay");
			Assert(!GetPrivateStaticField<bool>(typeof(CardEditorMultiplayerSync), "_pendingClientReady"),
				"the deferred Ready remained pending after it was replayed");
		}
		finally
		{
			lobby.CleanUp(disconnectSession: false);
			SetPrivateStaticField(typeof(CardEditorMultiplayerSync), "_netService", null);
			SetPrivateStaticField(typeof(CardEditorMultiplayerSync), "_lastAppliedSequence", 0);
			SetPrivateStaticField(typeof(CardEditorMultiplayerSync), "_requestedInitialSync", false);
			SetPrivateStaticField(typeof(CardEditorMultiplayerSync), "_pendingClientReady", false);
			SetPrivateStaticField(typeof(CardEditorMultiplayerSync), "_pendingReadyAction", null);
			CardEditorMultiplayerSync.ForceClientReadyGateForTesting = false;
		}

		return Task.CompletedTask;
	}

	private static void SetPrivateStaticField(Type type, string name, object? value)
	{
		type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, value);
	}

	private static T GetPrivateStaticField<T>(Type type, string name)
	{
		return (T)type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
	}

	private sealed class RecordingClientNetGameService(ulong netId) : INetGameService
	{
		public List<Type> SentMessageTypes { get; } = [];
		public ulong NetId { get; } = netId;
		public bool IsConnected => true;
		public bool IsGameLoading { get; private set; }
		public NetGameType Type => NetGameType.Client;
		public PlatformType Platform => PlatformType.None;
		public PeerVersionInfo LocalVersion { get; } = new()
		{
			version = "0.111.0-headless-test",
			branch = default,
			idDatabaseHash = 0,
			gameplayAffectingMods = [],
			otherMods = []
		};
		public event Action<NetErrorInfo>? Disconnected;
		public void SendMessage<T>(T message, ulong playerId) where T : INetMessage => SentMessageTypes.Add(typeof(T));
		public void SendMessage<T>(T message) where T : INetMessage => SentMessageTypes.Add(typeof(T));
		public void RegisterMessageHandler<T>(MessageHandlerDelegate<T> handler) where T : INetMessage { }
		public void UnregisterMessageHandler<T>(MessageHandlerDelegate<T> handler) where T : INetMessage { }
		public void Update() { }
		public void Disconnect(NetError reason, bool now = false) => Disconnected?.Invoke(new NetErrorInfo(reason, selfInitiated: true));
		public ConnectionStats? GetStatsForPeer(ulong peerId) => null;
		public void SetGameLoading(bool isLoading) => IsGameLoading = isLoading;
		public void SetBufferMessages(bool bufferMessages) { }
		public string? GetRawLobbyIdentifier() => null;
	}

	private sealed class RecordingStartRunLobbyListener : IStartRunLobbyListener
	{
		public int PlayerChangedCount { get; private set; }
		public void PlayerConnected(StartRunLobbyPlayer player) { }
		public void PlayerChanged(StartRunLobbyPlayer player, bool isRandomCharacterResolution) => PlayerChangedCount++;
		public void AscensionChanged() { }
		public void SeedChanged() { }
		public void ModifiersChanged() { }
		public void MaxAscensionChanged() { }
		public void RemotePlayerDisconnected(StartRunLobbyPlayer player) { }
		public void BeginRun(string seed, List<ActModel> acts, IReadOnlyList<ModifierModel> modifiers) { }
		public void LocalPlayerDisconnected(NetErrorInfo info) { }
	}

	private static Task TestMatchEnergyQualifierIsUnique()
	{
		Fixture fixture = CreateFixture();
		CardModel source = CreateSourceCard(fixture);
		CardExtraEffect move = ImmediateOnPlay(CardExtraEffectKind.MoveCardsBetweenPiles, amount: 2);
		move.CardSelectionPile = CardExtraEffectCardPile.DrawPile;
		move.MoveToPile = CardExtraEffectCardPile.Hand;
		move.CardSelectionMode = CardExtraEffectCardSelectionMode.Random;
		move.CostFilterEnabled = true;
		move.CostFilterMode = CardExtraEffectCostFilterMode.Exactly;
		move.CostFilterMax = 3;

		Assert(CardEditorExtraEffects.TryFormatLineForAudit(source, move, out string? description),
			"Match Energy effect did not generate description text");
		AssertSame(1, CountOccurrences(description!, "costing exactly"), "Match Energy qualifier was rendered more than once");
		return Task.CompletedTask;
	}

	private static Task TestResultPileDestinationIsSerialized()
	{
		string root = FindRepoRoot();
		string popupSource = File.ReadAllText(Path.Combine(root, "mods", "card_editor", "NCardEditorPopup.cs"));
		string baseOverrideBuilder = SliceSource(
			popupSource,
			"private CardOverride BuildOverrideFromUi()",
			"private void ApplyPowerDurationToOverride(");
		string upgradeOverrideBuilder = SliceSource(
			popupSource,
			"private CardUpgradeOverride BuildUpgradeOverrideFromUiDeltas(UpgradeBaseline baseline)",
			"private static void ApplyUpgradeOverridePreview(");

		AssertSame(2, CountOccurrences(baseOverrideBuilder, "or CardExtraEffectKind.ResultPileOverride"), "base-card editor does not route Result Pile through both pile configuration gates");
		AssertSame(2, CountOccurrences(upgradeOverrideBuilder, "or CardExtraEffectKind.ResultPileOverride"), "upgraded-card editor does not route Result Pile through both pile configuration gates");
		return Task.CompletedTask;
	}

	private static Task TestResetRequiresConfirmation()
	{
		string root = FindRepoRoot();
		string panelSource = File.ReadAllText(Path.Combine(root, "mods", "card_editor", "NCardEditorPresetPanel.cs"));
		string resetHandler = SliceSource(
			panelSource,
			"private async void OnVanillaPressed()",
			"private void RefreshStartupCheckbox()");

		Assert(
			resetHandler.Contains("await CardEditorConfirmPopup.ShowConfirmation(", StringComparison.Ordinal),
			"Reset does not reuse the editor confirmation popup");
		int confirmation = resetHandler.IndexOf("await CardEditorConfirmPopup.ShowConfirmation(", StringComparison.Ordinal);
		int firstMutation = resetHandler.IndexOf("CardEditorRelicOverrides.ReplaceAll", StringComparison.Ordinal);
		Assert(confirmation >= 0 && firstMutation > confirmation,
			"Reset mutates editor state before confirmation completes");
		Assert(resetHandler.Contains("if (!confirmed)", StringComparison.Ordinal),
			"Reset does not return when confirmation is declined");
		return Task.CompletedTask;
	}

	private static Task TestCardDescriptionUsesAutoSize()
	{
		string root = FindRepoRoot();
		string cardSourcePath = Path.Combine(root, "Slay the spire 2 Source", "src", "Core", "Nodes", "Cards", "NCard.cs");
		string cardSource = File.ReadAllText(cardSourcePath);
		Assert(
			cardSource.Contains("_descriptionLabel.SetTextAutoSize(\"[center]\" + text + \"[/center]\");", StringComparison.Ordinal),
			"current beta NCard description no longer uses the auto-size text path");
		return Task.CompletedTask;
	}

	private static string FindRepoRoot()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "mods", "card_editor", "NCardEditorPopup.cs")))
			{
				return directory.FullName;
			}
			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
	}

	private static string SliceSource(string source, string startMarker, string endMarker)
	{
		int start = source.IndexOf(startMarker, StringComparison.Ordinal);
		if (start < 0)
		{
			throw new InvalidOperationException($"Source start marker not found: {startMarker}");
		}

		int end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
		if (end < 0)
		{
			throw new InvalidOperationException($"Source end marker not found: {endMarker}");
		}

		return source[start..end];
	}

	private static int CountOccurrences(string value, string needle)
	{
		int count = 0;
		int offset = 0;
		while ((offset = value.IndexOf(needle, offset, StringComparison.Ordinal)) >= 0)
		{
			count++;
			offset += needle.Length;
		}
		return count;
	}

	private static void Assert(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}

	private static void AssertSame<T>(T expected, T actual, string message)
	{
		if (!EqualityComparer<T>.Default.Equals(expected, actual))
		{
			throw new InvalidOperationException($"{message}. Expected: {expected}; actual: {actual}.");
		}
	}

	private static void InitializeModelIdMapsWithoutGodot()
	{
		const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
		Type cacheType = typeof(ModelIdSerializationCache);
		var categoryToId = (Dictionary<string, int>)cacheType.GetField("_categoryNameToNetIdMap", flags)!.GetValue(null)!;
		var idToCategory = (List<string>)cacheType.GetField("_netIdToCategoryNameMap", flags)!.GetValue(null)!;
		var entryToId = (Dictionary<string, int>)cacheType.GetField("_entryNameToNetIdMap", flags)!.GetValue(null)!;
		var idToEntry = (List<string>)cacheType.GetField("_netIdToEntryNameMap", flags)!.GetValue(null)!;

		foreach (string category in ModelDb.All.Select(model => model.Id.Category).Distinct().Order())
		{
			if (!categoryToId.ContainsKey(category))
			{
				categoryToId.Add(category, idToCategory.Count);
				idToCategory.Add(category);
			}
		}

		foreach (string entry in ModelDb.All.Select(model => model.Id.Entry).Distinct().Order())
		{
			if (!entryToId.ContainsKey(entry))
			{
				entryToId.Add(entry, idToEntry.Count);
				idToEntry.Add(entry);
			}
		}

		cacheType.GetField("_initialized", flags)!.SetValue(null, true);
	}

	private static void InstallHeadlessSaveManager()
	{
		var progressManager = (ProgressSaveManager)RuntimeHelpers.GetUninitializedObject(typeof(ProgressSaveManager));
		progressManager.Progress = ProgressState.CreateDefault();

		var saveManager = (SaveManager)RuntimeHelpers.GetUninitializedObject(typeof(SaveManager));
		typeof(SaveManager).GetField("_progressSaveManager", BindingFlags.Instance | BindingFlags.NonPublic)!
			.SetValue(saveManager, progressManager);
		SaveManager.MockInstanceForTesting(saveManager);
	}

	private static void DisableGodotBackedPerformanceSettingsLoading()
	{
		Type settingsType = typeof(CardExtraEffect).Assembly.GetType("SlayTheSpire2Mod.CardEditor.CardEditorPerformanceSettings")
			?? throw new TypeLoadException("Could not find CardEditorPerformanceSettings.");
		settingsType.GetField("_loaded", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, true);
	}

	private static void InitializeDefinitionStoreWithoutGodot()
	{
		typeof(CardEditorDefinitionStore).GetField("_loaded", BindingFlags.Static | BindingFlags.NonPublic)!
			.SetValue(null, true);
		typeof(CardEditorRelicOverrideStore).GetField("_loaded", BindingFlags.Static | BindingFlags.NonPublic)!
			.SetValue(null, true);
		typeof(CardEditorCreatedCardsStore).GetField("_loaded", BindingFlags.Static | BindingFlags.NonPublic)!
			.SetValue(null, true);
		CardEditorDefinitionStore.PersistenceSuspended = true;
	}
}
