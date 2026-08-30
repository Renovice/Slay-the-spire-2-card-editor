using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
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
			new("Resources -> After Death -> player/enemy power hosts", TestAfterDeathPowerTrigger),
			new("Copy Debuffs: selected source to another enemy", TestCopyDebuffs),
			new("Grant -> Hits All Enemies safety block", TestHitsAllGrantIsBlocked),
			new("Multiplayer client Ready reaches vanilla network dispatch", TestMultiplayerClientReady),
			new("UI source contract: Result Pile saves selected destination", TestResultPileDestinationIsSerialized),
			new("UI source contract: Match Energy is defined once", TestMatchEnergyDefinitionIsUnique),
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

	private static Fixture CreateFixture()
	{
		RunState run = RunState.CreateForTest(seed: $"CARD-EDITOR-TEST-{Interlocked.Increment(ref _fixtureNumber)}");
		CombatState combat = new(runState: run);
		foreach (Player player in run.Players)
		{
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

	private static async Task TestAfterDeathPowerTrigger()
	{
		Fixture fixture = CreateFixture();
		CardModel source = CreateSourceCard(fixture);
		Creature enemy = AddMockEnemy(fixture, "after-death-target");
		CardExtraEffect effect = ImmediateOnPlay(CardExtraEffectKind.GainEnergy);
		effect.Trigger = CardExtraEffectTrigger.AfterDeath;
		effect.AsPower = true;
		effect.PowerTriggerFrom = CardExtraEffectPowerTriggerFrom.AnyEnemy;
		effect.PowerTargeting = CardExtraEffectPowerTargeting.TriggerTarget;
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
		SetPrivateStaticField(typeof(CardEditorMultiplayerSync), "_requestedInitialSync", true);
		try
		{
			bool originalAllowed = CardEditorMultiplayerSync.AllowClientReady(() => lobby.SetReady(true), ready: true);
			Assert(originalAllowed, "Card Editor swallowed the client's Ready click before StartRunLobby.SetReady");
			if (originalAllowed)
			{
				lobby.SetReady(true);
			}

			Assert(lobby.LocalPlayer.isReady, "the vanilla lobby did not mark the local client ready");
			Assert(service.SentMessageTypes.Contains(typeof(LobbyPlayerSetReadyMessage)),
				"the vanilla lobby did not send LobbyPlayerSetReadyMessage to the host");
			AssertSame(1, listener.PlayerChangedCount, "the lobby UI listener did not receive the Ready state change");
			Assert(!GetPrivateStaticField<bool>(typeof(CardEditorMultiplayerSync), "_requestedInitialSync"),
				"readying before the snapshot did not re-arm the initial sync request");
		}
		finally
		{
			lobby.CleanUp(disconnectSession: false);
			SetPrivateStaticField(typeof(CardEditorMultiplayerSync), "_netService", null);
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

	private static Task TestMatchEnergyDefinitionIsUnique()
	{
		string root = FindRepoRoot();
		string popupSource = File.ReadAllText(Path.Combine(root, "mods", "card_editor", "NCardEditorPopup.cs"));
		AssertSame(1, CountOccurrences(popupSource, "cardCostsLess.kind.matchingCardsEnergy"), "editor popup registers Match Energy more than once");
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
}
