using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Orbs;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Formatters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TextEffects;
using MegaCrit.Sts2.Core.ValueProps;

namespace SlayTheSpire2Mod.CardEditor;

public enum CardExtraEffectKind
{
	GainBlock = 0,
	DealDamage = 1,
	DrawCards = 2,
	GainEnergy = 3,
	GainStars = 4,
	Heal = 5,
	GainStrength = 6,
	LoseStrength = 7,
	GainDexterity = 8,
	LoseDexterity = 9,
	GainFocus = 10,
	LoseFocus = 11,
	ApplyWeak = 12,
	ApplyFrail = 13,
	ApplyVulnerable = 14,
	ApplyPoison = 15,
	ApplyDoom = 16,
	GainArtifact = 17,
	GainThorns = 18,
	GainRegen = 19,
	GainPlating = 20,
	GainIntangible = 21,
	GainBuffer = 22,
	GainVigor = 23,
	GainBlur = 24,
	GainRitual = 25,
	ApplyConstrict = 26,
	CreatedCardsCostLess = 27,
	AddRandomCardToHand = 28,
	ChooseOneOfThreeCardsToHand = 29,
	CreatedCardsUpgraded = 30,
	Summon = 31,
	Forge = 32,
	ChannelLightning = 33,
	ChannelFrost = 34,
	ChannelDark = 35,
	ChannelPlasma = 36,
	ChannelGlass = 37,
	ChannelRandomOrb = 38,
	GainOrbSlots = 39,
	IgnoreBlock = 40,
	IgnoreDamageModifiers = 41,
	LoseHp = 42,
	IgnoreDamageCaps = 43,
	IgnoreDamageNegation = 44,
	CardCostsLess = 45,
	MoveCardsBetweenPiles = 46,
	IgnoreEnemyDamageReductions = 47,
	AddCopyOfThisCard = 48,
	CardStarCostsLess = 49,
	AddSpecificCardToHand = 50,
	PlayCardFromPile = 51,
	DiscardCards = 52,
	ExhaustCards = 53,
	EvokeOrbs = 54,
	CardTypeCostsLess = 55,
	CardTypeStarCostsLess = 56,
	DrawnCardsCostLess = 57,
	GeneratedCardsCostLess = 58,
	AutoPlaySelfFromPile = 59,
	DrawCardsThatCostLess = 60,
	AutoDrawSelfFromPile = 61,
	ConditionalAutoPlayFromPile = 62,
	ConditionalAutoDrawFromPile = 63,
	LoseOrbSlots = 64,
	OrbAction = 65,
	UpgradeCardsInPile = 66,
	RemoveBlock = 67,
	RemoveArtifact = 68,
	MultiplyStatStatus = 69,
	GeneratedCardsUpgraded = 70,
	CardsInPileUpgradedAura = 71,
	EndTurn = 72,
	EnchantCard = 73,
	OstyAction = 74,
	GrantKeywordToPile = 75,
	GainGold = 76,
	UpgradeDeckCards = 77,
	FetchSpecificCardToHand = 78
}

public enum CardExtraEffectCardPile
{
	Hand = 0,
	DrawPile = 1,
	DiscardPile = 2,
	ExhaustPile = 3,
	AllPiles = 4,
	Deck = 5
}

public enum CardExtraEffectCardPilePosition
{
	Top = 0,
	Bottom = 1,
	Random = 2
}

public enum CardExtraEffectCardSelectionMode
{
	Choose = 0,
	Random = 1,
	All = 2,
	UpTo = 3
}

public enum CardExtraEffectCardGrantDuration
{
	ThisTurn = 0,
	ThisCombat = 1,
	UntilPlayed = 2,
	Turns = 3
}

public enum CardExtraEffectEnchantmentDuration
{
	Permanent = 0,
	ThisTurn = 1,
	ThisCombat = 2,
	UntilPlayed = 3,
	Turns = 4
}

public enum CardExtraEffectCardCostsLessDuration
{
	Permanent = 0,
	ThisTurn = 1,
	ThisCombat = 2,
	UntilPlayed = 3,
	Turns = 4
}

public enum CardExtraEffectCardCostsLessMode
{
	// Back-compat: if unset in saved overrides/presets, we infer behavior from Trigger like older versions did.
	Legacy = 0,
	Passive = 1,
	Triggered = 2
}

public enum CardExtraEffectTarget
{
	Self = 0,
	Target = 1,
	RandomEnemy = 2,
	AllEnemies = 3
}

public enum CardExtraEffectTrigger
{
	OnPlay = 0,
	OnDraw = 1,
	OnDiscard = 2,
	OnExhaust = 3,
	EndOfTurnInHand = 4,
	StartOfTurn = 5,
	EndOfTurn = 6,
	StartOfEnemyTurn = 7,
	EndOfEnemyTurn = 8,
	Fatal = 9,
	OstyDealDamage = 10,
	AfterCombat = 11,
	OnChannel = 12,
	OnEvoke = 13,
	TurnBoundary = 14,
	OnCountEvent = 15
}

public enum CardExtraEffectTurnBoundary
{
	Start = 0,
	End = 1
}

public enum CardExtraEffectTurnBoundarySide
{
	YourTurn = 0,
	EnemyTurn = 1,
	Both = 2
}

public enum CardExtraEffectTurnBoundaryCardLocation
{
	Any = 0,
	Hand = 1,
	DrawPile = 2,
	DiscardPile = 3,
	ExhaustPile = 4
}

public enum CardExtraEffectTiming
{
	Immediate = 0,
	StartOfTurn = 1,
	EndOfTurn = 2,
	EndOfThisTurn = 3,
	StartOfEnemyTurn = 4,
	EndOfEnemyTurn = 5,
	StartOfAnyTurn = 6,
	EndOfAnyTurn = 7,
	EndOfThisAnyTurn = 8
}

public enum CardExtraEffectDuration
{
	Permanent = 0,
	ThisTurn = 1
}

public enum CardCreatedCardsCostDuration
{
	ThisTurn = 0,
	ThisCombat = 1,
	UntilPlayed = 2,
	Turns = 3
}

public enum CardGeneratedCardPool
{
	Default = 0,
	Colorless = 1,
	Ironclad = 2,
	Silent = 3,
	Defect = 4,
	Regent = 5,
	Necrobinder = 6,
	OtherColors = 7,
	Any = 8,
	Ancient = 9,
	All = 10
}

public enum CardGeneratedCardType
{
	Any = 0,
	Attack = 1,
	Skill = 2,
	Power = 3,
	Playable = 4,
	Status = 5,
	Curse = 6,
	Quest = 7
}

public enum CardExtraEffectScaleMode
{
	None = 0,
	PerHistoryCount = 1,
	ConditionOnly = 2
}

public enum CardExtraEffectCountEvent
{
	Played = 0,
	Drawn = 1,
	Discarded = 2,
	Exhausted = 3,
	Generated = 4,
	InPile = 5,
	OrbChanneled = 6,
	OrbEvoked = 7,
	CurrentOrbs = 8,
	EmptyOrbSlots = 9,
	OrbInPosition = 10,
	EnemyHasStatus = 11,
	EnemyIntent = 12,
	PlayedCardEnergyCost = 13,
	StarsGained = 14,
	StarsLost = 15,
	EnergyGained = 16,
	EnergyLost = 17,
	EnergyUsed = 18,
	BlockGained = 19,
	BlockLost = 20,
	StatusGained = 21,
	StatusLost = 22,
	DamageDealt = 23,
	DamageTaken = 24,
	HealingReceived = 25,
	Summoned = 26,
	TimesLostHp = 27,
	TimesGainedHp = 28,
	OstyAttacked = 29
}

public enum CardExtraEffectCountWindow
{
	ThisTurn = 0,
	ThisCombat = 1,
	LastTurns = 2
}

public enum CardExtraEffectCountWindowInclusion
{
	IncludeThisTurn = 0,
	ExcludeThisTurn = 1
}

public enum CardExtraEffectBlockLostCountingMode
{
	DamageAndEffects = 0,
	IncludeBetweenTurns = 1
}

public enum CardExtraEffectCountCardFilter
{
	Any = 0,
	DealDamage = 1,
	GainBlock = 2,
	DrawCards = 3,
	GainEnergy = 4,
	GainStars = 5,
	Heal = 6,
	Strength = 7,
	Dexterity = 8,
	Focus = 9,
	Weak = 10,
	Frail = 11,
	Vulnerable = 12,
	Poison = 13,
	Doom = 14,
	Constrict = 15,
	Artifact = 16,
	Thorns = 17,
	Regen = 18,
	Plating = 19,
	Intangible = 20,
	Buffer = 21,
	Vigor = 22,
	Blur = 23,
	Ritual = 24,
	Summon = 25,
	Forge = 26,
	LoseHp = 27,
	Exhaust = 28,
	Ethereal = 29,
	Innate = 30,
	Retain = 31,
	Sly = 32,
	Eternal = 33
}

public enum CardExtraEffectMultiplierStat
{
	Block = 0,
	Strength = 1,
	Dexterity = 2,
	Focus = 3,
	Weak = 4,
	Frail = 5,
	Vulnerable = 6,
	Poison = 7,
	Doom = 8,
	Constrict = 9,
	Artifact = 10,
	Thorns = 11,
	Regen = 12,
	Plating = 13,
	Intangible = 14,
	Buffer = 15,
	Vigor = 16,
	Blur = 17,
	Ritual = 18
}

public enum CardExtraEffectOrbAction
{
	Evoke = 0,
	Remove = 1,
	Channel = 2,
	AddSlots = 3,
	RemoveSlots = 4,
}

public enum CardExtraEffectOrbType
{
	Any = 0,
	Lightning = 1,
	Frost = 2,
	Dark = 3,
	Plasma = 4,
	Glass = 5
}

public enum CardExtraEffectOrbSelection
{
	Leftmost = 0,
	Rightmost = 1,
	Middle = 2
}

public enum CardExtraEffectOrbFollowUp
{
	None = 0,
	ChannelSameType = 1
}

public enum CardExtraEffectOrbScope
{
	Fixed = 0,
	All = 1,
}

public enum CardExtraEffectOstyAction
{
	Attack = 0,
	AttackAll = 1,
	Heal = 2,
	Kill = 3
}

public enum CardExtraEffectCountComparison
{
	None = 0,
	AtLeast = 1,
	AtMost = 2,
	Exactly = 3
}

public enum CardExtraEffectEnemyStatus
{
	Weak = 0,
	Frail = 1,
	Vulnerable = 2,
	Poison = 3,
	Doom = 4,
	Constrict = 5,
	Artifact = 6,
	Thorns = 7,
	Regen = 8,
	Plating = 9,
	Intangible = 10,
	Buffer = 11,
	Vigor = 12,
	Blur = 13,
	Ritual = 14,
	Strength = 15,
	Dexterity = 16,
	Focus = 17,
	AnyPowerStatus = 18,
	Buff = 19,
	Debuff = 20
}

public enum CardExtraEffectEnemyIntent
{
	Attack = 0,
	Defense = 1,
	Buff = 2,
	Debuff = 3,
	Heal = 4,
	Escape = 5,
	Summon = 6,
	Sleep = 7,
	Stun = 8
}

public sealed class CardExtraEffect
{
	public CardExtraEffectKind Kind { get; set; }
	public CardExtraEffectTarget Target { get; set; }
	public int Amount { get; set; }

	// When true, treat Amount as "X" (the amount of Energy spent when the effect triggers).
	// The numeric Amount value is preserved so toggling X off in the UI restores the previous number.
	public bool AmountIsX { get; set; }

	// When AmountIsX is true, add this value to X when resolving the effect (i.e. "X+N").
	// This enables vanilla-style "X+1" amounts while still preserving the non-X Amount (used when toggling X off).
	public int AmountXPlus { get; set; }

	// Upgrade-only: keep the base slot synced in the editor, but hide and disable this effect on the upgraded card.
	public bool DisableOnUpgrade { get; set; }

	// When enabled, repeat this effect multiple times.
	// RepeatCount is used unless RepeatIsX is true, in which case the repeat count is derived from the card's X cost.
	public bool RepeatIsX { get; set; }
	public int RepeatCount { get; set; } = 1;

	// Grant-to-card: how many cards to select (ignored for SelectionMode.All).
	public bool CardSelectionCountIsX { get; set; }
	public int CardSelectionCount { get; set; } = 1;

	// Grant-to-card: optional candidate filters.
	public CardGeneratedCardPool CardSelectionPool { get; set; } = CardGeneratedCardPool.All;
	public CardGeneratedCardType CardSelectionType { get; set; } = CardGeneratedCardType.Any;
	public CardExtraEffectCountCardFilter CardSelectionFilter { get; set; } = CardExtraEffectCountCardFilter.Any;
	public CardExtraEffectTrigger Trigger { get; set; }
	public CardExtraEffectCountEvent PowerTriggerCountEvent { get; set; } = CardExtraEffectCountEvent.BlockLost;
	public CardExtraEffectTurnBoundary TurnBoundary { get; set; } = CardExtraEffectTurnBoundary.End;
	public CardExtraEffectTurnBoundarySide TurnBoundarySide { get; set; } = CardExtraEffectTurnBoundarySide.YourTurn;
	public CardExtraEffectTurnBoundaryCardLocation TurnBoundaryCardLocation { get; set; } = CardExtraEffectTurnBoundaryCardLocation.Any;
	public CardExtraEffectTiming Timing { get; set; }
	public int Turns { get; set; }
	public CardExtraEffectDuration Duration { get; set; }
	public bool AsPower { get; set; }

	// Power-only trigger conditions (which card event should actually fire this power effect).
	// Defaults are "any card" to preserve older saves/presets that didn't have these fields.
	public CardGeneratedCardPool TriggerCardPool { get; set; } = CardGeneratedCardPool.All;
	public CardGeneratedCardType TriggerCardType { get; set; } = CardGeneratedCardType.Any;
	public CardExtraEffectCountCardFilter TriggerCardFilter { get; set; } = CardExtraEffectCountCardFilter.Any;

	// Power-only: fire only every N-th time the trigger condition is met. 0 or 1 = every time.
	public int TriggerEveryN { get; set; }

	// Power-only: remove this power entry after it fires this many times. 0 = unlimited.
	public int TriggerMaxFires { get; set; }

	// Power-only: remove this power entry after this many turns elapse. 0 = unlimited.
	// When both TriggerMaxFires and TriggerMaxTurns are set, the entry expires when either limit is reached first.
	public int TriggerMaxTurns { get; set; }

	public CardCreatedCardsCostDuration CreatedCardsCostDuration { get; set; }
	public int CreatedCardsCostTurns { get; set; }

	public CardExtraEffectCardCostsLessDuration CardCostsLessDuration { get; set; }
	public int CardCostsLessTurns { get; set; }
	public CardExtraEffectCardCostsLessMode CardCostsLessMode { get; set; }

	public CardGeneratedCardPool GeneratedCardPool { get; set; }
	public CardGeneratedCardType GeneratedCardType { get; set; }

	public CardExtraEffectScaleMode ScaleMode { get; set; }
	public CardExtraEffectCountEvent CountEvent { get; set; }
	public CardExtraEffectCountWindow CountWindow { get; set; }
	public CardExtraEffectCountWindowInclusion CountWindowInclusion { get; set; } = CardExtraEffectCountWindowInclusion.IncludeThisTurn;
	public CardExtraEffectBlockLostCountingMode BlockLostCountingMode { get; set; } = CardExtraEffectBlockLostCountingMode.DamageAndEffects;
	public int CountTurns { get; set; }
	public CardExtraEffectCardPile CountCardPile { get; set; } = CardExtraEffectCardPile.Hand;
	public CardGeneratedCardPool CountCardPool { get; set; }
	public CardGeneratedCardType CountCardType { get; set; }
	public CardExtraEffectCountCardFilter CountCardFilter { get; set; }
	public bool CountOnlyBlockCards { get; set; }
	public CardExtraEffectOrbType CountOrbType { get; set; } = CardExtraEffectOrbType.Any;
	public CardExtraEffectOrbSelection CountOrbSelection { get; set; } = CardExtraEffectOrbSelection.Leftmost;
	public CardExtraEffectEnemyStatus CountEnemyStatus { get; set; } = CardExtraEffectEnemyStatus.AnyPowerStatus;
	public CardExtraEffectEnemyIntent CountEnemyIntent { get; set; } = CardExtraEffectEnemyIntent.Attack;
	public CardExtraEffectMultiplierStat MultiplierStat { get; set; } = CardExtraEffectMultiplierStat.Strength;
	public CardExtraEffectCountComparison CountComparison { get; set; }
	public int CountConditionAmount { get; set; } = 1;

	// When history scaling is enabled, include the base amount even if the history count is zero.
	// (i.e., total = base + base*count). This is optional and defaults to false for back-compat.
	public bool HistoryScalingIncludesBase { get; set; }

	public bool GrantToCard { get; set; }
	public CardExtraEffectCardSelectionMode CardSelectionMode { get; set; }
	public CardExtraEffectCardPile CardSelectionPile { get; set; }
	public CardExtraEffectCardGrantDuration CardGrantDuration { get; set; }
	public int CardGrantTurns { get; set; }
	public string? EnchantmentId { get; set; }
	public CardExtraEffectEnchantmentDuration EnchantmentDuration { get; set; } = CardExtraEffectEnchantmentDuration.ThisCombat;
	public int EnchantmentTurns { get; set; } = 1;

	public CardExtraEffectCardPile MoveToPile { get; set; }
	public CardExtraEffectCardPilePosition MoveToPosition { get; set; }
	public CardExtraEffectOrbAction OrbAction { get; set; }
	public CardExtraEffectOrbType OrbType { get; set; } = CardExtraEffectOrbType.Any;
	public CardExtraEffectOrbSelection OrbSelection { get; set; } = CardExtraEffectOrbSelection.Leftmost;
	public CardExtraEffectOrbFollowUp OrbFollowUp { get; set; } = CardExtraEffectOrbFollowUp.None;
	public CardExtraEffectOrbScope OrbScope { get; set; } = CardExtraEffectOrbScope.Fixed;
	public CardExtraEffectOstyAction OstyAction { get; set; }

	// DrawnCardsCostLess: which pile the card must have come from for the cost reduction to apply.
	// AllPiles = no filter (any source pile).
	public CardExtraEffectCardPile DrawnFromPile { get; set; } = CardExtraEffectCardPile.AllPiles;

	// For effects that reference a specific card (stored as ModelId string, e.g. "cards.shiv").
	public string? SpecificCardId { get; set; }

	// GrantKeywordToPile: which keyword to grant to the selected cards.
	public CardKeyword GrantedKeyword { get; set; } = CardKeyword.Exhaust;

	// Cost filter for pile operations (PlayCardFromPile, MoveCardsBetweenPiles, DiscardCards, ExhaustCards, UpgradeCardsInPile).
	// When enabled, only cards whose current energy cost is <= CostFilterMax are included (X-cost cards are always excluded).
	// Default: disabled (no filter), preserving existing behavior.
	public bool CostFilterEnabled { get; set; }
	public int CostFilterMax { get; set; }
}

internal sealed class CardExtraEffectDefinition
{
	public required CardExtraEffectKind Kind { get; init; }
	public required string Label { get; init; }
	public required IReadOnlyList<CardExtraEffectTarget> AllowedTargets { get; init; }
	public required int DefaultAmount { get; init; }
	public required CardExtraEffectTarget DefaultTarget { get; init; }
}

internal static class CardEditorExtraEffects
{
	internal static readonly IReadOnlyList<CardExtraEffectCountEvent> PowerTriggerCountEvents = new[]
	{
		CardExtraEffectCountEvent.BlockLost,
		CardExtraEffectCountEvent.BlockGained,
		CardExtraEffectCountEvent.EnergyGained,
		CardExtraEffectCountEvent.EnergyLost,
		CardExtraEffectCountEvent.EnergyUsed,
		CardExtraEffectCountEvent.DamageDealt,
		CardExtraEffectCountEvent.DamageTaken,
		CardExtraEffectCountEvent.HealingReceived,
		CardExtraEffectCountEvent.Summoned
	};

	internal enum ResourceCountSource
	{
		Other = 0,
		BetweenTurnsBlockClear = 1
	}

	private sealed class ManualTargetHolder
	{
		public Creature? Target { get; set; }
	}

	private sealed class OrbEvokeCountEntry
	{
		public required Creature Actor { get; init; }
		public required OrbModel Orb { get; init; }
		public required int RoundNumber { get; init; }
		public required CombatSide CurrentSide { get; init; }
	}

	private sealed class ResourceCountEntry
	{
		public required Creature Actor { get; init; }
		public required CardExtraEffectCountEvent CountEvent { get; init; }
		public required int Amount { get; init; }
		public required int RoundNumber { get; init; }
		public required CombatSide CurrentSide { get; init; }
		public required ResourceCountSource Source { get; init; }
	}

	private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
	{
		public static readonly ReferenceEqualityComparer<T> Instance = new();

		public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
		public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
	}

	private static readonly IReadOnlyList<CardExtraEffectDefinition> _definitions = new List<CardExtraEffectDefinition>
	{
		new()
		{
			Kind = CardExtraEffectKind.DealDamage,
			Label = "Deal Damage",
			AllowedTargets = new [] { CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy, CardExtraEffectTarget.Self },
			DefaultAmount = 6,
			DefaultTarget = CardExtraEffectTarget.Target
		},
		new()
		{
			Kind = CardExtraEffectKind.GainBlock,
			Label = "Gain Block",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 5,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.MultiplyStatStatus,
			Label = "Multiply Stat / Status",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 2,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.DrawCards,
			Label = "Draw Cards",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.GainEnergy,
			Label = "Gain Energy",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.GainStars,
			Label = "Gain Stars",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.Heal,
			Label = "Heal",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 3,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.LoseHp,
			Label = "Lose HP",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 3,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.GainStrength,
			Label = "Gain Strength",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 2,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.LoseStrength,
			Label = "Lose Strength",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 2,
			DefaultTarget = CardExtraEffectTarget.Target
		},
		new()
		{
			Kind = CardExtraEffectKind.GainDexterity,
			Label = "Gain Dexterity",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 2,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.LoseDexterity,
			Label = "Lose Dexterity",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 2,
			DefaultTarget = CardExtraEffectTarget.Target
		},
		new()
		{
			Kind = CardExtraEffectKind.GainFocus,
			Label = "Gain Focus",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.LoseFocus,
			Label = "Lose Focus",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.ApplyWeak,
			Label = "Apply Weak",
			AllowedTargets = new [] { CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy, CardExtraEffectTarget.Self },
			DefaultAmount = 2,
			DefaultTarget = CardExtraEffectTarget.Target
		},
		new()
		{
			Kind = CardExtraEffectKind.ApplyFrail,
			Label = "Apply Frail",
			AllowedTargets = new [] { CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy, CardExtraEffectTarget.Self },
			DefaultAmount = 2,
			DefaultTarget = CardExtraEffectTarget.Target
		},
		new()
		{
			Kind = CardExtraEffectKind.ApplyVulnerable,
			Label = "Apply Vulnerable",
			AllowedTargets = new [] { CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy, CardExtraEffectTarget.Self },
			DefaultAmount = 2,
			DefaultTarget = CardExtraEffectTarget.Target
		},
		new()
		{
			Kind = CardExtraEffectKind.ApplyPoison,
			Label = "Apply Poison",
			AllowedTargets = new [] { CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy, CardExtraEffectTarget.Self },
			DefaultAmount = 3,
			DefaultTarget = CardExtraEffectTarget.Target
		},
		new()
		{
			Kind = CardExtraEffectKind.ApplyDoom,
			Label = "Apply Doom",
			AllowedTargets = new [] { CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy, CardExtraEffectTarget.Self },
			DefaultAmount = 6,
			DefaultTarget = CardExtraEffectTarget.Target
		},
		new()
		{
			Kind = CardExtraEffectKind.GainArtifact,
			Label = "Gain Artifact",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.GainThorns,
			Label = "Gain Thorns",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 2,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.GainRegen,
			Label = "Gain Regen",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 3,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.GainPlating,
			Label = "Gain Plating",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 4,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.GainIntangible,
			Label = "Gain Intangible",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.GainBuffer,
			Label = "Gain Buffer",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.GainVigor,
			Label = "Gain Vigor",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 2,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.GainBlur,
			Label = "Gain Blur",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.GainRitual,
			Label = "Gain Ritual",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.ApplyConstrict,
			Label = "Apply Constrict",
			AllowedTargets = new [] { CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy, CardExtraEffectTarget.Self },
			DefaultAmount = 3,
			DefaultTarget = CardExtraEffectTarget.Target
		},
		new()
		{
			Kind = CardExtraEffectKind.CreatedCardsCostLess,
			Label = "Created Cards Cost Less",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.CreatedCardsUpgraded,
			Label = "Created Cards Are Upgraded",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.GeneratedCardsUpgraded,
			Label = "Created Cards Are Upgraded (Aura)",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.CardsInPileUpgradedAura,
			Label = "Cards In Pile Are Upgraded (Aura)",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.AddRandomCardToHand,
			Label = "Card Generation",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.ChooseOneOfThreeCardsToHand,
			Label = "Choose 1 of 3 Cards",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.Summon,
			Label = "Summon",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 5,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.Forge,
			Label = "Forge",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 3,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.ChannelLightning,
			Label = "Channel Lightning",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.ChannelFrost,
			Label = "Channel Frost",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.ChannelDark,
			Label = "Channel Dark",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.ChannelPlasma,
			Label = "Channel Plasma",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.ChannelGlass,
			Label = "Channel Glass",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.ChannelRandomOrb,
			Label = "Channel Random Orb",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.GainOrbSlots,
			Label = "Gain Orb Slots",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.LoseOrbSlots,
			Label = "Lose Orb Slots",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.OrbAction,
			Label = "Orb Action",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.OstyAction,
			Label = "Osty Action",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.RandomEnemy, CardExtraEffectTarget.AllEnemies },
			DefaultAmount = 5,
			DefaultTarget = CardExtraEffectTarget.Target
		},
		new()
		{
			Kind = CardExtraEffectKind.IgnoreBlock,
			Label = "Ignore Effect",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.IgnoreDamageModifiers,
			Label = "Ignore Damage Modifiers",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.IgnoreDamageCaps,
			Label = "Ignore Damage Caps",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.IgnoreDamageNegation,
			Label = "Ignore Damage Negation",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.IgnoreEnemyDamageReductions,
			Label = "Ignore Enemy Damage Reductions",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.CardCostsLess,
			Label = "Card Cost Changes",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.DiscardCards,
			Label = "Discard Cards",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.ExhaustCards,
			Label = "Exhaust Cards",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.EvokeOrbs,
			Label = "Evoke Orbs",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.EndTurn,
			Label = "End Turn",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.EnchantCard,
			Label = "Enchant Card",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.MoveCardsBetweenPiles,
			Label = "Move Cards Between Piles",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.UpgradeCardsInPile,
			Label = "Upgrade Cards in Pile",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.AddCopyOfThisCard,
			Label = "Add Copy of This Card",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.CardStarCostsLess,
			Label = "Card Star Cost Changes",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.CardTypeCostsLess,
			Label = "Card Type Cost Changes",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.CardTypeStarCostsLess,
			Label = "Card Type Star Cost Changes",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.DrawnCardsCostLess,
			Label = "Drawn Cards Cost Less",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.GeneratedCardsCostLess,
			Label = "Created Cards Cost Less (Global)",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.AddSpecificCardToHand,
			Label = "Add Specific Card",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.FetchSpecificCardToHand,
			Label = "Fetch Specific Card (Preserves State)",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.PlayCardFromPile,
			Label = "Play Card from Pile",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.AutoPlaySelfFromPile,
			Label = "Auto-Play Self from Pile",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.DrawCardsThatCostLess,
			Label = "Draw Cards (They Cost Less)",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 2,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.AutoDrawSelfFromPile,
			Label = "Auto-Draw Self from Pile",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.ConditionalAutoPlayFromPile,
			Label = "Auto-Play Self from Pile",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.ConditionalAutoDrawFromPile,
			Label = "Auto-Draw Self from Pile",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.GrantKeywordToPile,
			Label = "Grant Keyword to Pile",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.GainGold,
			Label = "Gain Gold",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		}
	};

	private static readonly IReadOnlyList<CardExtraEffectCountEvent> _cardSmithCountEvents = new[]
	{
		CardExtraEffectCountEvent.Played,
		CardExtraEffectCountEvent.Drawn,
		CardExtraEffectCountEvent.Discarded,
		CardExtraEffectCountEvent.Exhausted,
		CardExtraEffectCountEvent.Generated,
		CardExtraEffectCountEvent.InPile,
		CardExtraEffectCountEvent.StarsGained,
		CardExtraEffectCountEvent.StarsLost,
		CardExtraEffectCountEvent.EnergyGained,
		CardExtraEffectCountEvent.EnergyLost,
		CardExtraEffectCountEvent.EnergyUsed,
		CardExtraEffectCountEvent.BlockGained,
		CardExtraEffectCountEvent.BlockLost,
		CardExtraEffectCountEvent.StatusGained,
		CardExtraEffectCountEvent.StatusLost,
		CardExtraEffectCountEvent.DamageDealt,
		CardExtraEffectCountEvent.DamageTaken,
		CardExtraEffectCountEvent.HealingReceived,
		CardExtraEffectCountEvent.Summoned
	};

	private static readonly ConditionalWeakTable<CardModel, ManualTargetHolder> _manualTargets = new();
	private static readonly Dictionary<ModelId, Creature> _manualTargetsByCardId = new();
	private static readonly ConditionalWeakTable<CombatState, List<OrbEvokeCountEntry>> _orbEvokeHistory = new();
	private static readonly ConditionalWeakTable<CombatState, List<ResourceCountEntry>> _resourceCountHistory = new();

	public static IReadOnlyList<CardExtraEffectDefinition> Definitions => _definitions;
	public static IReadOnlyList<CardExtraEffectCountEvent> CardSmithCountEvents => _cardSmithCountEvents;

	public static bool RequiresManualEnemyTarget(CardModel card)
	{
		if (card == null)
		{
			return false;
		}

		if (TryGetOverride(card.Id, out CardOverride? overrideData))
		{
			IReadOnlyList<CardExtraEffect> overrideEffects = GetEffectiveExtraEffects(card, overrideData!, card.CurrentUpgradeLevel > 0);
			if (overrideEffects.Any(e => e != null
				&& !IsPowerEffect(e)
				&& !e.GrantToCard
				&& IsValidEffectAmount(e.Kind, e.Amount)
				&& e.Trigger == CardExtraEffectTrigger.OnPlay
				&& e.Target == CardExtraEffectTarget.Target))
			{
				return true;
			}
		}

		CombatState? combatState = card.CombatState;
		if (combatState != null)
		{
			IReadOnlyList<CardExtraEffect> temporaryEffects = CardEditorTemporaryExtraEffectController.GetEffects(combatState, card);
			if (temporaryEffects.Any(e => e != null
				&& !IsPowerEffect(e)
				&& !e.GrantToCard
				&& IsValidEffectAmount(e.Kind, e.Amount)
				&& e.Trigger == CardExtraEffectTrigger.OnPlay
				&& e.Target == CardExtraEffectTarget.Target))
			{
				return true;
			}
		}

		Creature? ownerCreature = card.Owner?.Creature;
		if (ownerCreature == null)
		{
			return false;
		}

		CardEditorExtraEffectPower? power = ownerCreature.GetPower<CardEditorExtraEffectPower>();
		return power != null && power.HasOnPlayTargetEffects();
	}

	public static void SetManualTarget(CardModel card, Creature target)
	{
		if (card == null || target == null)
		{
			return;
		}
		ManualTargetHolder holder = _manualTargets.GetOrCreateValue(card);
		holder.Target = target;
		_manualTargetsByCardId[card.Id] = target;
		Log.Info($"[CardEditor] Manual target: {card.Id.Entry} -> {target.Name}");
	}

	public static bool TryGetManualTarget(CardModel card, out Creature? target)
	{
		target = null;
		CardModel? cursor = card;
		for (int i = 0; i < 4 && cursor != null; i++)
		{
			if (_manualTargets.TryGetValue(cursor, out ManualTargetHolder? holder) && holder?.Target != null)
			{
				target = holder.Target;
				return true;
			}
			cursor = cursor.CloneOf;
		}
		if (_manualTargetsByCardId.TryGetValue(card.Id, out Creature? byId) && byId != null)
		{
			target = byId;
			return true;
		}
		return false;
	}

	public static void ClearManualTarget(CardModel card)
	{
		if (card == null)
		{
			return;
		}
		_manualTargets.Remove(card);
		_manualTargetsByCardId.Remove(card.Id);
	}

	public static string TargetLabel(CardExtraEffectTarget target)
	{
		string fallback = target switch
		{
			CardExtraEffectTarget.Self => "Self",
			CardExtraEffectTarget.Target => "Target",
			CardExtraEffectTarget.RandomEnemy => "Random Enemy",
			CardExtraEffectTarget.AllEnemies => "All Enemies",
			_ => target.ToString()
		};
		return CardEditorLoc.Enum("extraEffectTarget", target, fallback);
	}

	public static string TriggerLabel(CardExtraEffectTrigger trigger)
	{
		string fallback = trigger switch
		{
			CardExtraEffectTrigger.OnPlay => "On Play",
			CardExtraEffectTrigger.OnDraw => "On Draw",
			CardExtraEffectTrigger.OnDiscard => "On Discard",
			CardExtraEffectTrigger.OnExhaust => "On Exhaust",
			CardExtraEffectTrigger.EndOfTurnInHand => "End of YOUR Turn (in hand)",
			CardExtraEffectTrigger.StartOfTurn => "Start of Your Turn",
			CardExtraEffectTrigger.EndOfTurn => "End of Your Turn",
			CardExtraEffectTrigger.StartOfEnemyTurn => "Start of Enemy Turn",
			CardExtraEffectTrigger.EndOfEnemyTurn => "End of Enemy Turn",
			CardExtraEffectTrigger.TurnBoundary => "Turn Boundary",
			CardExtraEffectTrigger.OnCountEvent => "Whenever (Event)",
			CardExtraEffectTrigger.Fatal => "Fatal",
			CardExtraEffectTrigger.OstyDealDamage => "Osty Deals Damage",
			CardExtraEffectTrigger.AfterCombat => "End of Combat",
			CardExtraEffectTrigger.OnChannel => "On Channel",
			CardExtraEffectTrigger.OnEvoke => "On Evoke",
			_ => trigger.ToString()
		};
		return CardEditorLoc.Enum("extraEffectTrigger", trigger, fallback);
	}

	public static string TriggerLabel(CardExtraEffectTrigger trigger, bool asPower)
	{
		// Keep trigger dropdown labels stable to avoid the UI appearing to "replace" options when the Power toggle changes.
		// The Power toggle still changes semantics (this-card trigger vs global trigger), but that is communicated via the
		// Power tooltip + generated card text rather than re-labelling the dropdown items live.
		_ = asPower;
		return TriggerLabel(trigger);
	}

	public static string CountEventLabel(CardExtraEffectCountEvent ev)
	{
		string fallback = ev switch
		{
			CardExtraEffectCountEvent.Played => "Played",
			CardExtraEffectCountEvent.Drawn => "Drawn",
			CardExtraEffectCountEvent.Discarded => "Discarded",
			CardExtraEffectCountEvent.Exhausted => "Exhausted",
			CardExtraEffectCountEvent.Generated => "Created",
			CardExtraEffectCountEvent.InPile => "In Pile",
			CardExtraEffectCountEvent.OrbChanneled => "Orb Channeled",
			CardExtraEffectCountEvent.OrbEvoked => "Orb Evoked",
			CardExtraEffectCountEvent.CurrentOrbs => "Current Orbs",
			CardExtraEffectCountEvent.EmptyOrbSlots => "Empty Orb Slots",
			CardExtraEffectCountEvent.OrbInPosition => "Orb In Position",
			CardExtraEffectCountEvent.EnemyHasStatus => "Enemy Has Status/Power",
			CardExtraEffectCountEvent.EnemyIntent => "Enemy Intent",
			CardExtraEffectCountEvent.PlayedCardEnergyCost => "Played Card Cost",
			CardExtraEffectCountEvent.StarsGained => "Stars Gained",
			CardExtraEffectCountEvent.StarsLost => "Stars Lost",
			CardExtraEffectCountEvent.EnergyGained => "Energy Gained",
			CardExtraEffectCountEvent.EnergyLost => "Energy Lost",
			CardExtraEffectCountEvent.EnergyUsed => "Energy Used",
			CardExtraEffectCountEvent.BlockGained => "Block Gained",
			CardExtraEffectCountEvent.BlockLost => "Block Lost",
			CardExtraEffectCountEvent.StatusGained => "Gained Power/Status",
			CardExtraEffectCountEvent.StatusLost => "Lost Power/Status",
			CardExtraEffectCountEvent.DamageDealt => "Damage Dealt",
			CardExtraEffectCountEvent.DamageTaken => "Damage Taken",
			CardExtraEffectCountEvent.HealingReceived => "HP Recovered",
			CardExtraEffectCountEvent.Summoned => "Summoned",
			CardExtraEffectCountEvent.TimesLostHp => "Times Lost HP",
			CardExtraEffectCountEvent.TimesGainedHp => "Times Gained HP",
			CardExtraEffectCountEvent.OstyAttacked => "Times Osty Attacked",
			_ => ev.ToString()
		};
		return CardEditorLoc.Enum("countEvent", ev, fallback);
	}

	public static string ScaleModeLabel(CardExtraEffectScaleMode mode)
	{
		string fallback = mode switch
		{
			CardExtraEffectScaleMode.PerHistoryCount => "Scale by Count",
			CardExtraEffectScaleMode.ConditionOnly => "Condition Only",
			_ => "None"
		};
		return CardEditorLoc.Enum("scaleMode", mode, fallback);
	}

	public static string CountComparisonLabel(CardExtraEffectCountComparison comparison)
	{
		string fallback = comparison switch
		{
			CardExtraEffectCountComparison.None => "No Threshold",
			CardExtraEffectCountComparison.AtLeast => "At Least",
			CardExtraEffectCountComparison.AtMost => "At Most",
			CardExtraEffectCountComparison.Exactly => "Exactly",
			_ => comparison.ToString()
		};
		return CardEditorLoc.Enum("countComparison", comparison, fallback);
	}

	public static string CountWindowLabel(CardExtraEffectCountWindow window)
	{
		string fallback = window switch
		{
			CardExtraEffectCountWindow.ThisTurn => "This Turn",
			CardExtraEffectCountWindow.ThisCombat => "This Combat",
			CardExtraEffectCountWindow.LastTurns => "Last X Turns",
			_ => window.ToString()
		};
		return CardEditorLoc.Enum("countWindow", window, fallback);
	}

	public static bool CountEventUsesWindow(CardExtraEffectCountEvent ev)
	{
		return ev is not CardExtraEffectCountEvent.InPile
			and not CardExtraEffectCountEvent.CurrentOrbs
			and not CardExtraEffectCountEvent.EmptyOrbSlots
			and not CardExtraEffectCountEvent.OrbInPosition
			and not CardExtraEffectCountEvent.EnemyHasStatus
			and not CardExtraEffectCountEvent.EnemyIntent
			and not CardExtraEffectCountEvent.PlayedCardEnergyCost;
	}

	public static bool CountEventUsesCardPile(CardExtraEffectCountEvent ev)
	{
		return ev == CardExtraEffectCountEvent.InPile;
	}

	public static bool CountEventUsesCardFilters(CardExtraEffectCountEvent ev)
	{
		return ev is CardExtraEffectCountEvent.Played
			or CardExtraEffectCountEvent.Drawn
			or CardExtraEffectCountEvent.Discarded
			or CardExtraEffectCountEvent.Exhausted
			or CardExtraEffectCountEvent.Generated
			or CardExtraEffectCountEvent.InPile;
	}

	public static bool CountEventUsesOrbType(CardExtraEffectCountEvent ev)
	{
		return ev is CardExtraEffectCountEvent.OrbChanneled
			or CardExtraEffectCountEvent.OrbEvoked
			or CardExtraEffectCountEvent.CurrentOrbs
			or CardExtraEffectCountEvent.OrbInPosition;
	}

	public static bool CountEventUsesOrbSelection(CardExtraEffectCountEvent ev)
	{
		return ev == CardExtraEffectCountEvent.OrbInPosition;
	}

	public static bool CountEventUsesEnemyStatus(CardExtraEffectCountEvent ev)
	{
		return ev is CardExtraEffectCountEvent.EnemyHasStatus
			or CardExtraEffectCountEvent.StatusGained
			or CardExtraEffectCountEvent.StatusLost;
	}

	public static bool CountEventUsesEnemyIntent(CardExtraEffectCountEvent ev)
	{
		return ev == CardExtraEffectCountEvent.EnemyIntent;
	}

	private static bool TryGetAmountCountText(CardModel? card, CardExtraEffectCountEvent ev, out string singularResource, out string pluralResource, out string presentVerb, out string simplePastVerb, out string perfectVerb)
	{
		switch (ev)
		{
			case CardExtraEffectCountEvent.StarsGained:
				singularResource = BuildStarIcons(1);
				pluralResource = singularResource;
				presentVerb = CardEditorLoc.Enum("historyVerbPresent", ev, "gain");
				simplePastVerb = CardEditorLoc.Enum("historyVerbPast", ev, "gained");
				perfectVerb = simplePastVerb;
				return true;
			case CardExtraEffectCountEvent.StarsLost:
				singularResource = BuildStarIcons(1);
				pluralResource = singularResource;
				presentVerb = CardEditorLoc.Enum("historyVerbPresent", ev, "lose");
				simplePastVerb = CardEditorLoc.Enum("historyVerbPast", ev, "lost");
				perfectVerb = simplePastVerb;
				return true;
			case CardExtraEffectCountEvent.EnergyGained:
				singularResource = BuildEnergyIcon(card);
				pluralResource = singularResource;
				presentVerb = CardEditorLoc.Enum("historyVerbPresent", ev, "gain");
				simplePastVerb = CardEditorLoc.Enum("historyVerbPast", ev, "gained");
				perfectVerb = simplePastVerb;
				return true;
			case CardExtraEffectCountEvent.EnergyLost:
				singularResource = BuildEnergyIcon(card);
				pluralResource = singularResource;
				presentVerb = CardEditorLoc.Enum("historyVerbPresent", ev, "lose");
				simplePastVerb = CardEditorLoc.Enum("historyVerbPast", ev, "lost");
				perfectVerb = simplePastVerb;
				return true;
			case CardExtraEffectCountEvent.EnergyUsed:
				singularResource = BuildEnergyIcon(card);
				pluralResource = singularResource;
				presentVerb = CardEditorLoc.Enum("historyVerbPresent", ev, "spend");
				simplePastVerb = CardEditorLoc.Enum("historyVerbPast", ev, "spent");
				perfectVerb = simplePastVerb;
				return true;
			case CardExtraEffectCountEvent.BlockGained:
				singularResource = "[gold]Block[/gold]";
				pluralResource = singularResource;
				presentVerb = CardEditorLoc.Enum("historyVerbPresent", ev, "gain");
				simplePastVerb = CardEditorLoc.Enum("historyVerbPast", ev, "gained");
				perfectVerb = simplePastVerb;
				return true;
			case CardExtraEffectCountEvent.BlockLost:
				singularResource = "[gold]Block[/gold]";
				pluralResource = singularResource;
				presentVerb = CardEditorLoc.Enum("historyVerbPresent", ev, "lose");
				simplePastVerb = CardEditorLoc.Enum("historyVerbPast", ev, "lost");
				perfectVerb = simplePastVerb;
				return true;
			case CardExtraEffectCountEvent.DamageDealt:
				singularResource = CardEditorLoc.T("cardText.resource.damage", "damage");
				pluralResource = singularResource;
				presentVerb = CardEditorLoc.Enum("historyVerbPresent", ev, "deal");
				simplePastVerb = CardEditorLoc.Enum("historyVerbPast", ev, "dealt");
				perfectVerb = simplePastVerb;
				return true;
			case CardExtraEffectCountEvent.DamageTaken:
				singularResource = CardEditorLoc.T("cardText.resource.damage", "damage");
				pluralResource = singularResource;
				presentVerb = CardEditorLoc.Enum("historyVerbPresent", ev, "take");
				simplePastVerb = CardEditorLoc.Enum("historyVerbPast", ev, "took");
				perfectVerb = CardEditorLoc.Enum("historyVerbPerfect", ev, "taken");
				return true;
			case CardExtraEffectCountEvent.HealingReceived:
				singularResource = CardEditorLoc.T("cardText.resource.hp", "HP");
				pluralResource = singularResource;
				presentVerb = CardEditorLoc.Enum("historyVerbPresent", ev, "recover");
				simplePastVerb = CardEditorLoc.Enum("historyVerbPast", ev, "recovered");
				perfectVerb = simplePastVerb;
				return true;
			case CardExtraEffectCountEvent.TimesLostHp:
				singularResource = "time";
				pluralResource = "times";
				presentVerb = "lose HP";
				simplePastVerb = "lost HP";
				perfectVerb = "lost HP";
				return true;
			case CardExtraEffectCountEvent.TimesGainedHp:
				singularResource = "time";
				pluralResource = "times";
				presentVerb = "gain HP";
				simplePastVerb = "gained HP";
				perfectVerb = "gained HP";
				return true;
			case CardExtraEffectCountEvent.OstyAttacked:
				singularResource = "time";
				pluralResource = "times";
				presentVerb = "Osty attacks";
				simplePastVerb = "Osty attacked";
				perfectVerb = "Osty attacked";
				return true;
			default:
				singularResource = string.Empty;
				pluralResource = string.Empty;
				presentVerb = string.Empty;
				simplePastVerb = string.Empty;
				perfectVerb = string.Empty;
				return false;
		}
	}

	private static bool TryGetStatusCountText(CardExtraEffectCountEvent ev, CardExtraEffectEnemyStatus status, out string statusText, out string presentVerb, out string simplePastVerb, out string perfectVerb)
	{
		statusText = EnemyStatusLabel(status);
		switch (ev)
		{
			case CardExtraEffectCountEvent.StatusGained:
				presentVerb = CardEditorLoc.Enum("historyVerbPresent", ev, "gain");
				simplePastVerb = CardEditorLoc.Enum("historyVerbPast", ev, "gained");
				perfectVerb = simplePastVerb;
				return true;
			case CardExtraEffectCountEvent.StatusLost:
				presentVerb = CardEditorLoc.Enum("historyVerbPresent", ev, "lose");
				simplePastVerb = CardEditorLoc.Enum("historyVerbPast", ev, "lost");
				perfectVerb = simplePastVerb;
				return true;
			default:
				statusText = string.Empty;
				presentVerb = string.Empty;
				simplePastVerb = string.Empty;
				perfectVerb = string.Empty;
				return false;
		}
	}

	internal static bool TryGetSuggestedCountEnemyStatus(CardExtraEffectKind kind, out CardExtraEffectEnemyStatus status)
	{
		status = kind switch
		{
			CardExtraEffectKind.GainStrength or CardExtraEffectKind.LoseStrength => CardExtraEffectEnemyStatus.Strength,
			CardExtraEffectKind.GainDexterity or CardExtraEffectKind.LoseDexterity => CardExtraEffectEnemyStatus.Dexterity,
			CardExtraEffectKind.GainFocus or CardExtraEffectKind.LoseFocus => CardExtraEffectEnemyStatus.Focus,
			CardExtraEffectKind.ApplyWeak => CardExtraEffectEnemyStatus.Weak,
			CardExtraEffectKind.ApplyFrail => CardExtraEffectEnemyStatus.Frail,
			CardExtraEffectKind.ApplyVulnerable => CardExtraEffectEnemyStatus.Vulnerable,
			CardExtraEffectKind.ApplyPoison => CardExtraEffectEnemyStatus.Poison,
			CardExtraEffectKind.ApplyDoom => CardExtraEffectEnemyStatus.Doom,
			CardExtraEffectKind.ApplyConstrict => CardExtraEffectEnemyStatus.Constrict,
			CardExtraEffectKind.GainArtifact => CardExtraEffectEnemyStatus.Artifact,
			CardExtraEffectKind.GainThorns => CardExtraEffectEnemyStatus.Thorns,
			CardExtraEffectKind.GainRegen => CardExtraEffectEnemyStatus.Regen,
			CardExtraEffectKind.GainPlating => CardExtraEffectEnemyStatus.Plating,
			CardExtraEffectKind.GainIntangible => CardExtraEffectEnemyStatus.Intangible,
			CardExtraEffectKind.GainBuffer => CardExtraEffectEnemyStatus.Buffer,
			CardExtraEffectKind.GainVigor => CardExtraEffectEnemyStatus.Vigor,
			CardExtraEffectKind.GainBlur => CardExtraEffectEnemyStatus.Blur,
			CardExtraEffectKind.GainRitual => CardExtraEffectEnemyStatus.Ritual,
			_ => default
		};

		return kind is CardExtraEffectKind.GainStrength
			or CardExtraEffectKind.LoseStrength
			or CardExtraEffectKind.GainDexterity
			or CardExtraEffectKind.LoseDexterity
			or CardExtraEffectKind.GainFocus
			or CardExtraEffectKind.LoseFocus
			or CardExtraEffectKind.ApplyWeak
			or CardExtraEffectKind.ApplyFrail
			or CardExtraEffectKind.ApplyVulnerable
			or CardExtraEffectKind.ApplyPoison
			or CardExtraEffectKind.ApplyDoom
			or CardExtraEffectKind.ApplyConstrict
			or CardExtraEffectKind.GainArtifact
			or CardExtraEffectKind.GainThorns
			or CardExtraEffectKind.GainRegen
			or CardExtraEffectKind.GainPlating
			or CardExtraEffectKind.GainIntangible
			or CardExtraEffectKind.GainBuffer
			or CardExtraEffectKind.GainVigor
			or CardExtraEffectKind.GainBlur
			or CardExtraEffectKind.GainRitual;
	}

	internal static bool TryGetSuggestedCountOrbType(CardExtraEffectKind kind, CardExtraEffectOrbAction orbAction, CardExtraEffectOrbType orbType, out CardExtraEffectOrbType suggestedOrbType)
	{
		suggestedOrbType = kind switch
		{
			CardExtraEffectKind.ChannelLightning => CardExtraEffectOrbType.Lightning,
			CardExtraEffectKind.ChannelFrost => CardExtraEffectOrbType.Frost,
			CardExtraEffectKind.ChannelDark => CardExtraEffectOrbType.Dark,
			CardExtraEffectKind.ChannelPlasma => CardExtraEffectOrbType.Plasma,
			CardExtraEffectKind.ChannelGlass => CardExtraEffectOrbType.Glass,
			CardExtraEffectKind.OrbAction when orbAction is CardExtraEffectOrbAction.Channel or CardExtraEffectOrbAction.Evoke or CardExtraEffectOrbAction.Remove
				&& orbType != CardExtraEffectOrbType.Any => orbType,
			_ => CardExtraEffectOrbType.Any
		};

		return kind is CardExtraEffectKind.ChannelLightning
			or CardExtraEffectKind.ChannelFrost
			or CardExtraEffectKind.ChannelDark
			or CardExtraEffectKind.ChannelPlasma
			or CardExtraEffectKind.ChannelGlass
			or CardExtraEffectKind.OrbAction && suggestedOrbType != CardExtraEffectOrbType.Any;
	}

	public static string CountCardFilterLabel(CardExtraEffectCountCardFilter filter)
	{
		string fallback = filter switch
		{
			CardExtraEffectCountCardFilter.Any => "Any",
			CardExtraEffectCountCardFilter.DealDamage => "Deal Damage",
			CardExtraEffectCountCardFilter.GainBlock => "Gain Block",
			CardExtraEffectCountCardFilter.DrawCards => "Draw Cards",
			CardExtraEffectCountCardFilter.GainEnergy => "Gain Energy",
			CardExtraEffectCountCardFilter.GainStars => "Gain Stars",
			CardExtraEffectCountCardFilter.Heal => "Heal",
			CardExtraEffectCountCardFilter.Strength => "Strength",
			CardExtraEffectCountCardFilter.Dexterity => "Dexterity",
			CardExtraEffectCountCardFilter.Focus => "Focus",
			CardExtraEffectCountCardFilter.Weak => "Weak",
			CardExtraEffectCountCardFilter.Frail => "Frail",
			CardExtraEffectCountCardFilter.Vulnerable => "Vulnerable",
			CardExtraEffectCountCardFilter.Poison => "Poison",
			CardExtraEffectCountCardFilter.Doom => "Doom",
			CardExtraEffectCountCardFilter.Constrict => "Constrict",
			CardExtraEffectCountCardFilter.Artifact => "Artifact",
			CardExtraEffectCountCardFilter.Thorns => "Thorns",
			CardExtraEffectCountCardFilter.Regen => "Regen",
			CardExtraEffectCountCardFilter.Plating => "Plating",
			CardExtraEffectCountCardFilter.Intangible => "Intangible",
			CardExtraEffectCountCardFilter.Buffer => "Buffer",
			CardExtraEffectCountCardFilter.Vigor => "Vigor",
			CardExtraEffectCountCardFilter.Blur => "Blur",
			CardExtraEffectCountCardFilter.Ritual => "Ritual",
			CardExtraEffectCountCardFilter.Summon => "Summon",
			CardExtraEffectCountCardFilter.Forge => "Forge",
			CardExtraEffectCountCardFilter.LoseHp => "Lose HP",
			CardExtraEffectCountCardFilter.Exhaust => "Has Exhaust",
			CardExtraEffectCountCardFilter.Ethereal => "Has Ethereal",
			CardExtraEffectCountCardFilter.Innate => "Has Innate",
			CardExtraEffectCountCardFilter.Retain => "Has Retain",
			CardExtraEffectCountCardFilter.Sly => "Has Sly",
			CardExtraEffectCountCardFilter.Eternal => "Has Eternal",
			_ => filter.ToString()
		};
		return CardEditorLoc.Enum("countFilter", filter, fallback);
	}

	public static string GrantedKeywordLabel(CardKeyword keyword)
	{
		string fallback = keyword switch
		{
			CardKeyword.Exhaust => "Exhaust",
			CardKeyword.Ethereal => "Ethereal",
			CardKeyword.Innate => "Innate",
			CardKeyword.Unplayable => "Unplayable",
			CardKeyword.Retain => "Retain",
			CardKeyword.Sly => "Sly",
			CardKeyword.Eternal => "Eternal",
			_ => keyword.ToString()
		};
		return CardEditorLoc.Enum("cardKeyword", keyword, fallback);
	}

	public static string TimingLabel(CardExtraEffectTiming timing)
	{
		string fallback = timing switch
		{
			CardExtraEffectTiming.Immediate => "Now",
			CardExtraEffectTiming.StartOfTurn => "Start of your turn",
			CardExtraEffectTiming.EndOfTurn => "End of Next Turn",
			CardExtraEffectTiming.EndOfThisTurn => "End of This Turn",
			CardExtraEffectTiming.StartOfEnemyTurn => "Start of enemy turn",
			CardExtraEffectTiming.EndOfEnemyTurn => "End of enemy turn",
			CardExtraEffectTiming.StartOfAnyTurn => "Start of next turn",
			CardExtraEffectTiming.EndOfAnyTurn => "End of next turn",
			CardExtraEffectTiming.EndOfThisAnyTurn => "End of this turn",
			_ => timing.ToString()
		};
		return CardEditorLoc.Enum("extraEffectTiming", timing, fallback);
	}

	public static string EnemyStatusLabel(CardExtraEffectEnemyStatus status)
	{
		string fallback = status switch
		{
			CardExtraEffectEnemyStatus.Weak => "Weak",
			CardExtraEffectEnemyStatus.Frail => "Frail",
			CardExtraEffectEnemyStatus.Vulnerable => "Vulnerable",
			CardExtraEffectEnemyStatus.Poison => "Poison",
			CardExtraEffectEnemyStatus.Doom => "Doom",
			CardExtraEffectEnemyStatus.Constrict => "Constrict",
			CardExtraEffectEnemyStatus.Artifact => "Artifact",
			CardExtraEffectEnemyStatus.Thorns => "Thorns",
			CardExtraEffectEnemyStatus.Regen => "Regen",
			CardExtraEffectEnemyStatus.Plating => "Plating",
			CardExtraEffectEnemyStatus.Intangible => "Intangible",
			CardExtraEffectEnemyStatus.Buffer => "Buffer",
			CardExtraEffectEnemyStatus.Vigor => "Vigor",
			CardExtraEffectEnemyStatus.Blur => "Blur",
			CardExtraEffectEnemyStatus.Ritual => "Ritual",
			CardExtraEffectEnemyStatus.Strength => "Strength",
			CardExtraEffectEnemyStatus.Dexterity => "Dexterity",
			CardExtraEffectEnemyStatus.Focus => "Focus",
			CardExtraEffectEnemyStatus.AnyPowerStatus => "Any Power/Status",
			CardExtraEffectEnemyStatus.Buff => "Any Buff",
			CardExtraEffectEnemyStatus.Debuff => "Any Debuff",
			_ => status.ToString()
		};
		return CardEditorLoc.Enum("enemyStatus", status, fallback);
	}

	public static string MultiplierStatLabel(CardExtraEffectMultiplierStat stat)
	{
		string fallback = stat switch
		{
			CardExtraEffectMultiplierStat.Block => "Block",
			CardExtraEffectMultiplierStat.Strength => "Strength",
			CardExtraEffectMultiplierStat.Dexterity => "Dexterity",
			CardExtraEffectMultiplierStat.Focus => "Focus",
			CardExtraEffectMultiplierStat.Weak => "Weak",
			CardExtraEffectMultiplierStat.Frail => "Frail",
			CardExtraEffectMultiplierStat.Vulnerable => "Vulnerable",
			CardExtraEffectMultiplierStat.Poison => "Poison",
			CardExtraEffectMultiplierStat.Doom => "Doom",
			CardExtraEffectMultiplierStat.Constrict => "Constrict",
			CardExtraEffectMultiplierStat.Artifact => "Artifact",
			CardExtraEffectMultiplierStat.Thorns => "Thorns",
			CardExtraEffectMultiplierStat.Regen => "Regen",
			CardExtraEffectMultiplierStat.Plating => "Plating",
			CardExtraEffectMultiplierStat.Intangible => "Intangible",
			CardExtraEffectMultiplierStat.Buffer => "Buffer",
			CardExtraEffectMultiplierStat.Vigor => "Vigor",
			CardExtraEffectMultiplierStat.Blur => "Blur",
			CardExtraEffectMultiplierStat.Ritual => "Ritual",
			_ => stat.ToString()
		};
		return CardEditorLoc.Enum("multiplierStat", stat, fallback);
	}

	public static string EnemyIntentLabel(CardExtraEffectEnemyIntent intent)
	{
		string fallback = intent switch
		{
			CardExtraEffectEnemyIntent.Attack => "Attack",
			CardExtraEffectEnemyIntent.Defense => "Defense",
			CardExtraEffectEnemyIntent.Buff => "Buff",
			CardExtraEffectEnemyIntent.Debuff => "Debuff",
			CardExtraEffectEnemyIntent.Heal => "Heal",
			CardExtraEffectEnemyIntent.Escape => "Escape",
			CardExtraEffectEnemyIntent.Summon => "Summon",
			CardExtraEffectEnemyIntent.Sleep => "Sleep",
			CardExtraEffectEnemyIntent.Stun => "Stun",
			_ => intent.ToString()
		};
		return CardEditorLoc.Enum("enemyIntent", intent, fallback);
	}

	public static string DurationLabel(CardExtraEffectDuration duration)
	{
		string fallback = duration switch
		{
			CardExtraEffectDuration.Permanent => "Permanent",
			CardExtraEffectDuration.ThisTurn => "This Turn",
			_ => duration.ToString()
		};
		return CardEditorLoc.Enum("extraEffectDuration", duration, fallback);
	}

	public static string CreatedCardsCostDurationLabel(CardCreatedCardsCostDuration duration)
	{
		string fallback = duration switch
		{
			CardCreatedCardsCostDuration.ThisTurn => "This Turn",
			CardCreatedCardsCostDuration.ThisCombat => "This Combat",
			CardCreatedCardsCostDuration.UntilPlayed => "Until Played",
			CardCreatedCardsCostDuration.Turns => "X Turns",
			_ => duration.ToString()
		};
		return CardEditorLoc.Enum("createdCostDuration", duration, fallback);
	}

	public static string GeneratedCardPoolLabel(CardGeneratedCardPool pool)
	{
		string fallback = pool switch
		{
			CardGeneratedCardPool.Default => "Your Color",
			CardGeneratedCardPool.Colorless => "Colorless",
			CardGeneratedCardPool.Ironclad => "Ironclad",
			CardGeneratedCardPool.Silent => "Silent",
			CardGeneratedCardPool.Defect => "Defect",
			CardGeneratedCardPool.Regent => "Regent",
			CardGeneratedCardPool.Necrobinder => "Necrobinder",
			CardGeneratedCardPool.OtherColors => "Other Characters",
			CardGeneratedCardPool.Any => "Any Character",
			CardGeneratedCardPool.Ancient => "Ancient",
			CardGeneratedCardPool.All => "All Cards",
			_ => pool.ToString()
		};
		return CardEditorLoc.Enum("generatedPool", pool, fallback);
	}

	public static string GeneratedCardTypeLabel(CardGeneratedCardType type)
	{
		string fallback = type switch
		{
			CardGeneratedCardType.Any => "Any Type",
			CardGeneratedCardType.Attack => "Attack",
			CardGeneratedCardType.Skill => "Skill",
			CardGeneratedCardType.Power => "Power",
			CardGeneratedCardType.Playable => "Playable (Attack/Skill/Power)",
			CardGeneratedCardType.Status => "Status",
			CardGeneratedCardType.Curse => "Curse",
			CardGeneratedCardType.Quest => "Quest",
			_ => type.ToString()
		};
		return CardEditorLoc.Enum("generatedType", type, fallback);
	}

	public static string CardPileLabel(CardExtraEffectCardPile pile)
	{
		string fallback = pile switch
		{
			CardExtraEffectCardPile.Hand => "Hand",
			CardExtraEffectCardPile.DrawPile => "Draw Pile",
			CardExtraEffectCardPile.DiscardPile => "Discard Pile",
			CardExtraEffectCardPile.ExhaustPile => "Exhaust Pile",
			CardExtraEffectCardPile.AllPiles => "All Piles",
			CardExtraEffectCardPile.Deck => "Deck",
			_ => pile.ToString()
		};
		return CardEditorLoc.Enum("cardPile", pile, fallback);
	}

	public static string CardPilePositionLabel(CardExtraEffectCardPilePosition position)
	{
		string fallback = position switch
		{
			CardExtraEffectCardPilePosition.Top => "Top",
			CardExtraEffectCardPilePosition.Bottom => "Bottom",
			CardExtraEffectCardPilePosition.Random => "Random",
			_ => position.ToString()
		};
		return CardEditorLoc.Enum("cardPilePosition", position, fallback);
	}

	public static string CardSelectionModeLabel(CardExtraEffectCardSelectionMode mode)
	{
		string fallback = mode switch
		{
			CardExtraEffectCardSelectionMode.Choose => "Choose",
			CardExtraEffectCardSelectionMode.Random => "Random",
			CardExtraEffectCardSelectionMode.All => "All",
			CardExtraEffectCardSelectionMode.UpTo => "Up To",
			_ => mode.ToString()
		};
		return CardEditorLoc.Enum("cardSelectionMode", mode, fallback);
	}

	public static string CardGrantDurationLabel(CardExtraEffectCardGrantDuration duration)
	{
		string fallback = duration switch
		{
			CardExtraEffectCardGrantDuration.ThisTurn => "This Turn",
			CardExtraEffectCardGrantDuration.ThisCombat => "This Combat",
			CardExtraEffectCardGrantDuration.UntilPlayed => "Until Played",
			CardExtraEffectCardGrantDuration.Turns => "X Turns",
			_ => duration.ToString()
		};
		return CardEditorLoc.Enum("grantDuration", duration, fallback);
	}

	public static string EnchantmentDurationLabel(CardExtraEffectEnchantmentDuration duration)
	{
		string fallback = duration switch
		{
			CardExtraEffectEnchantmentDuration.Permanent => "Permanent",
			CardExtraEffectEnchantmentDuration.ThisTurn => "This Turn",
			CardExtraEffectEnchantmentDuration.ThisCombat => "This Combat",
			CardExtraEffectEnchantmentDuration.UntilPlayed => "Until Played",
			CardExtraEffectEnchantmentDuration.Turns => "X Turns",
			_ => duration.ToString()
		};
		return CardEditorLoc.Enum("enchantmentDuration", duration, fallback);
	}

	public static string CardCostsLessModeLabel(CardExtraEffectCardCostsLessMode mode)
	{
		string fallback = mode switch
		{
			CardExtraEffectCardCostsLessMode.Legacy => "Legacy",
			CardExtraEffectCardCostsLessMode.Passive => "Passive",
			CardExtraEffectCardCostsLessMode.Triggered => "Triggered",
			_ => mode.ToString()
		};
		return CardEditorLoc.Enum("cardCostsLessMode", mode, fallback);
	}

	public static string CardCostsLessDurationLabel(CardExtraEffectCardCostsLessDuration duration)
	{
		string fallback = duration switch
		{
			CardExtraEffectCardCostsLessDuration.Permanent => "Permanent",
			CardExtraEffectCardCostsLessDuration.ThisTurn => "This Turn",
			CardExtraEffectCardCostsLessDuration.ThisCombat => "This Combat",
			CardExtraEffectCardCostsLessDuration.UntilPlayed => "Until Played",
			CardExtraEffectCardCostsLessDuration.Turns => "X Turns",
			_ => duration.ToString()
		};
		return CardEditorLoc.Enum("cardCostsLessDuration", duration, fallback);
	}

	public static string OrbActionLabel(CardExtraEffectOrbAction action)
	{
		string fallback = action switch
		{
			CardExtraEffectOrbAction.Evoke => "Evoke",
			CardExtraEffectOrbAction.Remove => "Lose",
			CardExtraEffectOrbAction.Channel => "Channel",
			CardExtraEffectOrbAction.AddSlots => "Add Slots",
			CardExtraEffectOrbAction.RemoveSlots => "Remove Slots",
			_ => action.ToString()
		};
		return CardEditorLoc.Enum("orbAction", action, fallback);
	}

	public static string OrbScopeLabel(CardExtraEffectOrbScope scope)
	{
		string fallback = scope switch
		{
			CardExtraEffectOrbScope.All => "All",
			_ => "One"
		};
		return CardEditorLoc.Enum("orbScope", scope, fallback);
	}

	public static string OstyActionLabel(CardExtraEffectOstyAction action)
	{
		string fallback = action switch
		{
			CardExtraEffectOstyAction.Attack => "Attack",
			CardExtraEffectOstyAction.AttackAll => "Attack All",
			CardExtraEffectOstyAction.Heal => "Heal Osty",
			CardExtraEffectOstyAction.Kill => "Kill Osty",
			_ => action.ToString()
		};
		return CardEditorLoc.Enum("ostyAction", action, fallback);
	}

	public static string OrbTypeLabel(CardExtraEffectOrbType type)
	{
		string fallback = type switch
		{
			CardExtraEffectOrbType.Any => "Any Orb",
			CardExtraEffectOrbType.Lightning => "Lightning",
			CardExtraEffectOrbType.Frost => "Frost",
			CardExtraEffectOrbType.Dark => "Dark",
			CardExtraEffectOrbType.Plasma => "Plasma",
			CardExtraEffectOrbType.Glass => "Glass",
			_ => type.ToString()
		};
		return CardEditorLoc.Enum("orbType", type, fallback);
	}

	public static string OrbSelectionLabel(CardExtraEffectOrbSelection selection)
	{
		string fallback = selection switch
		{
			CardExtraEffectOrbSelection.Leftmost => "Leftmost",
			CardExtraEffectOrbSelection.Rightmost => "Rightmost",
			CardExtraEffectOrbSelection.Middle => "Middle",
			_ => selection.ToString()
		};
		return CardEditorLoc.Enum("orbSelection", selection, fallback);
	}

	public static string OrbFollowUpLabel(CardExtraEffectOrbFollowUp followUp)
	{
		string fallback = followUp switch
		{
			CardExtraEffectOrbFollowUp.None => "No Follow-up",
			CardExtraEffectOrbFollowUp.ChannelSameType => "Channel Same Type",
			_ => followUp.ToString()
		};
		return CardEditorLoc.Enum("orbFollowUp", followUp, fallback);
	}

	public static bool SupportsDuration(CardExtraEffectKind kind)
	{
		return kind is CardExtraEffectKind.GainStrength
			or CardExtraEffectKind.LoseStrength
			or CardExtraEffectKind.GainDexterity
			or CardExtraEffectKind.LoseDexterity
			or CardExtraEffectKind.GainFocus
			or CardExtraEffectKind.LoseFocus
			or CardExtraEffectKind.ApplyWeak
			or CardExtraEffectKind.ApplyFrail
			or CardExtraEffectKind.ApplyVulnerable
			or CardExtraEffectKind.ApplyPoison
			or CardExtraEffectKind.ApplyDoom
			or CardExtraEffectKind.GainArtifact
			or CardExtraEffectKind.GainThorns
			or CardExtraEffectKind.GainRegen
			or CardExtraEffectKind.GainPlating
			or CardExtraEffectKind.GainIntangible
			or CardExtraEffectKind.GainBuffer
			or CardExtraEffectKind.GainVigor
			or CardExtraEffectKind.GainBlur
			or CardExtraEffectKind.GainRitual
			or CardExtraEffectKind.ApplyConstrict;
	}

	public static bool SupportsAsPower(CardExtraEffectKind kind)
	{
		return kind is not CardExtraEffectKind.CreatedCardsCostLess
			and not CardExtraEffectKind.CreatedCardsUpgraded
			and not CardExtraEffectKind.GeneratedCardsUpgraded
			and not CardExtraEffectKind.CardsInPileUpgradedAura
			and not CardExtraEffectKind.EndTurn
			and not CardExtraEffectKind.EnchantCard
			and not CardExtraEffectKind.IgnoreBlock
			and not CardExtraEffectKind.IgnoreDamageModifiers
			and not CardExtraEffectKind.IgnoreDamageCaps
			and not CardExtraEffectKind.IgnoreDamageNegation
			and not CardExtraEffectKind.IgnoreEnemyDamageReductions
			and not CardExtraEffectKind.CardCostsLess
			and not CardExtraEffectKind.CardStarCostsLess
			and not CardExtraEffectKind.AutoPlaySelfFromPile
			and not CardExtraEffectKind.DrawCardsThatCostLess
			and not CardExtraEffectKind.AutoDrawSelfFromPile
			and not CardExtraEffectKind.ConditionalAutoPlayFromPile
			and not CardExtraEffectKind.ConditionalAutoDrawFromPile
			;
	}

	private static bool IsPowerEffect(CardExtraEffect? effect)
		=> effect != null && effect.AsPower && SupportsAsPower(effect.Kind);

	internal static bool IsLegacyOrbSlotKind(CardExtraEffectKind kind)
		=> kind is CardExtraEffectKind.GainOrbSlots or CardExtraEffectKind.LoseOrbSlots;

	internal static CardExtraEffect? NormalizeLegacyOrbSlotEffect(CardExtraEffect? effect)
	{
		if (effect == null || !IsLegacyOrbSlotKind(effect.Kind))
		{
			return effect;
		}

		int amount = effect.Amount <= 0 ? 1 : effect.Amount;
		if (effect.Kind == CardExtraEffectKind.GainOrbSlots)
		{
			effect.Kind = CardExtraEffectKind.OrbAction;
			effect.OrbAction = CardExtraEffectOrbAction.AddSlots;
			effect.OrbScope = CardExtraEffectOrbScope.Fixed;
			effect.Amount = amount;
			return effect;
		}

		// LoseOrbSlots
		effect.Kind = CardExtraEffectKind.OrbAction;
		effect.OrbAction = CardExtraEffectOrbAction.RemoveSlots;
		effect.OrbScope = amount >= 99 ? CardExtraEffectOrbScope.All : CardExtraEffectOrbScope.Fixed;
		effect.Amount = amount >= 99 ? 1 : amount;
		return effect;
	}

	internal static bool IsValidEffectAmount(CardExtraEffectKind kind, int amount)
	{
		return kind switch
		{
			CardExtraEffectKind.DealDamage => amount >= 0,
			CardExtraEffectKind.GainBlock => amount >= 0,
			CardExtraEffectKind.MultiplyStatStatus => amount >= 1,
			CardExtraEffectKind.CreatedCardsCostLess => amount == -1 || amount > 0,
			CardExtraEffectKind.CardCostsLess => amount != 0,
			CardExtraEffectKind.CardStarCostsLess => amount != 0,
			CardExtraEffectKind.CardTypeCostsLess => amount != 0,
			CardExtraEffectKind.CardTypeStarCostsLess => amount != 0,
			CardExtraEffectKind.DrawnCardsCostLess => amount != 0,
			CardExtraEffectKind.GeneratedCardsCostLess => amount != 0,
			_ => amount >= 0
		};
	}

	// Upgrade "extra effects" store deltas for the base slots (index-based). Those deltas can be negative to cancel/remove
	// a base effect on the upgraded version (e.g. base 6 + delta -6 => removed).
	internal static bool IsValidUpgradeDeltaAmount(CardExtraEffectKind kind, int amount)
	{
		return amount != 0;
	}

	internal static bool HasMeaningfulUpgradeBaseSlotDelta(CardExtraEffect effect, bool secondaryNumericFieldsAreDeltas)
	{
		if (effect == null)
		{
			return false;
		}

		if (effect.DisableOnUpgrade || IsValidUpgradeDeltaAmount(effect.Kind, effect.Amount))
		{
			return true;
		}

		if (!secondaryNumericFieldsAreDeltas)
		{
			return false;
		}

		return effect.Turns != 0
			|| effect.RepeatCount != 0
			|| effect.TriggerEveryN != 0
			|| effect.TriggerMaxFires != 0
			|| effect.TriggerMaxTurns != 0
			|| effect.CreatedCardsCostTurns != 0
			|| effect.CardCostsLessTurns != 0
			|| effect.CountTurns != 0
			|| effect.CountConditionAmount != 0
			|| effect.CardGrantTurns != 0
			|| effect.CardSelectionCount != 0
			|| effect.EnchantmentTurns != 0;
	}

	public static IReadOnlyList<CardExtraEffect> GetEffectsForDescription(CardModel card, bool isUpgradePreview)
	{
		if (card == null)
		{
			return Array.Empty<CardExtraEffect>();
		}

		bool considerUpgrade = isUpgradePreview || card.CurrentUpgradeLevel > 0;
		IReadOnlyList<CardExtraEffect> baseEffects = Array.Empty<CardExtraEffect>();

		if (CardEditorUiState.TryGetDraftOverride(card.Id, out CardOverride draft))
		{
			// Draft is authoritative — if the editor is open, don't fall back to stored even if the draft has no valid effects.
			baseEffects = GetEffectiveExtraEffects(card, draft, considerUpgrade);
		}
		else if (CardEditorOverrides.TryGet(card.Id, out CardOverride stored))
		{
			baseEffects = GetEffectiveExtraEffects(card, stored, considerUpgrade);
		}

		CombatState? combatState = card.CombatState;
		IReadOnlyList<CardExtraEffect> temporaryEffects = combatState != null
			? CardEditorTemporaryExtraEffectController.GetEffects(combatState, card)
			: Array.Empty<CardExtraEffect>();

		if (temporaryEffects.Count == 0)
		{
			return baseEffects;
		}

		if (baseEffects.Count == 0)
		{
			return temporaryEffects;
		}

		List<CardExtraEffect> combined = new List<CardExtraEffect>(baseEffects.Count + temporaryEffects.Count);
		combined.AddRange(baseEffects);
		combined.AddRange(temporaryEffects);
		return combined;
	}

	private static bool TryGetOverride(ModelId cardId, out CardOverride? overrideData)
	{
		if (CardEditorUiState.TryGetDraftOverride(cardId, out CardOverride draft))
		{
			overrideData = draft;
			return true;
		}
		if (CardEditorOverrides.TryGet(cardId, out CardOverride stored))
		{
			overrideData = stored;
			return true;
		}
		overrideData = null;
		return false;
	}

	public static bool TryAppendDescription(CardModel card, ref string description, Creature? target = null, bool isUpgradePreview = false)
	{
		if (card == null)
		{
			return false;
		}

		bool considerUpgrade = isUpgradePreview || card.CurrentUpgradeLevel > 0;

		bool hasOverride = TryGetOverrideForDescription(card, considerUpgrade, out CardOverride? overrideData, out IReadOnlyList<CardExtraEffect> effectiveEffects);
		CombatState? combatState = card.CombatState;
		IReadOnlyList<CardExtraEffect> temporaryEffects = combatState != null
			? CardEditorTemporaryExtraEffectController.GetEffects(combatState, card)
			: Array.Empty<CardExtraEffect>();

		List<(CardExtraEffect Effect, bool WasJustUpgraded, bool IsTemporary)> toRender = new List<(CardExtraEffect, bool, bool)>();
		if (hasOverride)
		{
			List<(CardExtraEffect Effect, bool WasJustUpgraded)> baseList = isUpgradePreview
				? BuildUpgradePreviewEffects(overrideData!, effectiveEffects)
				: effectiveEffects.Select(e => (e, false)).ToList();
			if (baseList.Count > 0)
			{
				toRender.AddRange(baseList.Select(t => (t.Effect, t.WasJustUpgraded, false)));
			}
		}
		if (temporaryEffects.Count > 0)
		{
			toRender.AddRange(temporaryEffects.Select(e => (e, false, true)));
		}

		if (toRender.Count == 0)
		{
			return false;
		}

		List<string> lines = new List<string>();
		foreach ((CardExtraEffect effect, bool wasJustUpgraded, bool isTemporary) in toRender)
		{
			if (effect == null || !IsValidEffectAmount(effect.Kind, effect.Amount))
			{
				continue;
			}
			if (card.CombatState != null
				&& isTemporary
				&& effect.Kind == CardExtraEffectKind.CardCostsLess
				&& !IsPowerEffect(effect)
				&& !effect.GrantToCard)
			{
				// Prefer rendering the "definition" line (base override) over the active (temporary) line so the card text
				// stays stable like vanilla cards (the cost gem/green color communicates the active discount).
				bool hasMatchingBase = toRender.Any(e =>
					!e.IsTemporary
					&& e.Effect != null
					&& IsEquivalentTimedCardCostsLess(e.Effect, effect));
				if (hasMatchingBase)
				{
					continue;
				}
			}
			string? line = TryFormatLine(card, effect, target, forceUpgradeHighlight: wasJustUpgraded);
			if (!string.IsNullOrWhiteSpace(line))
			{
				lines.Add(line!);
			}
		}
		if (lines.Count == 0)
		{
			return false;
		}

		description = string.IsNullOrEmpty(description) ? string.Join('\n', lines) : description + "\n" + string.Join('\n', lines);
		return true;
	}

	private static bool TryGetOverrideForDescription(CardModel card, bool considerUpgrade, out CardOverride? overrideData, out IReadOnlyList<CardExtraEffect> effects)
	{
		overrideData = null;
		effects = Array.Empty<CardExtraEffect>();

		if (CardEditorUiState.TryGetDraftOverride(card.Id, out CardOverride draft))
		{
			// Draft is authoritative — if the editor is open, don't fall back to stored even if the draft has no valid effects.
			IReadOnlyList<CardExtraEffect> draftEffects = GetEffectiveExtraEffects(card, draft, considerUpgrade);
			if (draftEffects.Count > 0)
			{
				overrideData = draft;
				effects = draftEffects;
				return true;
			}
			return false;
		}

		if (CardEditorOverrides.TryGet(card.Id, out CardOverride stored))
		{
			IReadOnlyList<CardExtraEffect> storedEffects = GetEffectiveExtraEffects(card, stored, considerUpgrade);
			if (storedEffects.Count > 0)
			{
				overrideData = stored;
				effects = storedEffects;
				return true;
			}
		}

		return false;
	}

	private static List<(CardExtraEffect Effect, bool WasJustUpgraded)> BuildUpgradePreviewEffects(CardOverride overrideData, IReadOnlyList<CardExtraEffect> effectiveEffects)
	{
		if (overrideData == null || effectiveEffects == null || effectiveEffects.Count == 0)
		{
			return new List<(CardExtraEffect, bool)>();
		}

		IReadOnlyList<CardExtraEffect>? baseEffects = overrideData.ExtraEffects;
		IReadOnlyList<CardExtraEffect>? upgradeEffects = overrideData.Upgrade?.ExtraEffects;

		if (upgradeEffects == null || upgradeEffects.Count == 0)
		{
			return effectiveEffects.Select(e => (e, false)).ToList();
		}

		if (baseEffects == null || baseEffects.Count == 0)
		{
			return effectiveEffects.Select(e => (e, true)).ToList();
		}

		List<(CardExtraEffect Effect, bool WasJustUpgraded)> result = baseEffects.Select(e => (CloneEffect(e), false)).ToList();
		int baseCount = baseEffects.Count;
		bool secondaryNumericFieldsAreDeltas = overrideData.Upgrade?.ExtraEffectNumericFieldsAreDeltas ?? false;

		for (int i = 0; i < upgradeEffects.Count; i++)
		{
			CardExtraEffect? upgradeEffect = upgradeEffects[i];
			if (upgradeEffect == null)
			{
				continue;
			}

			if (i < baseCount)
			{
				if (upgradeEffect.DisableOnUpgrade)
				{
					result[i] = (null!, true);
					continue;
				}

				CardExtraEffect baseEffect = baseEffects[i];
				if (baseEffect.Kind == upgradeEffect.Kind)
				{
					if (!HasMeaningfulUpgradeBaseSlotDelta(upgradeEffect, secondaryNumericFieldsAreDeltas))
					{
						continue;
					}
					result[i] = (MergeUpgradeBaseSlotEffect(baseEffect, upgradeEffect, secondaryNumericFieldsAreDeltas), true);
					continue;
				}
			}

			// Past the base slots, upgrade effects are absolute (not deltas).
			if (!upgradeEffect.DisableOnUpgrade && IsValidEffectAmount(upgradeEffect.Kind, upgradeEffect.Amount))
			{
				result.Add((CloneEffect(upgradeEffect), true));
			}
		}

		return result;
	}

	public static async Task RunAfterCardPlayed(CombatState combatState, PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		CardModel card = cardPlay.Card;
		if (card == null)
		{
			return;
		}
		using IDisposable _ = CardEditorCardPlayContext.PushScoped(cardPlay);
		List<CardExtraEffect>? triggeredCardCostsLessToApplyAfter = null;
		bool isCreatedCard = card is CardEditorCreatedCardBase;
		try
		{
			List<CardExtraEffect> effects = new List<CardExtraEffect>();
			if (CardEditorOverrides.TryGet(card.Id, out CardOverride overrideData))
			{
				IReadOnlyList<CardExtraEffect> overrideEffects = GetEffectiveExtraEffects(card, overrideData, card.CurrentUpgradeLevel > 0);
				if (overrideEffects.Count > 0)
				{
					effects.AddRange(overrideEffects);
				}
			}

			IReadOnlyList<CardExtraEffect> temporaryEffects = CardEditorTemporaryExtraEffectController.GetEffects(combatState, card);
			if (temporaryEffects.Count > 0)
			{
				effects.AddRange(temporaryEffects);
			}

			if (effects.Count == 0)
			{
				return;
			}

			List<CardExtraEffect>? powerEffectsToAdd = null;
			foreach (CardExtraEffect effect in effects)
			{
				if (effect == null || !IsValidEffectAmount(effect.Kind, effect.Amount))
				{
					continue;
				}

				if (IsPowerEffect(effect))
				{
					(powerEffectsToAdd ??= new List<CardExtraEffect>()).Add(effect);
					continue;
				}

				if (effect.Trigger != CardExtraEffectTrigger.OnPlay)
				{
					continue;
				}

				// Created cards execute their non-power OnPlay effects during CardModel.OnPlay so they resolve before
				// AfterCardPlayed powers (e.g. "Whenever you play a card, gain Vigor") trigger.
				if (isCreatedCard)
				{
					if (effect.Kind is (CardExtraEffectKind.CardCostsLess or CardExtraEffectKind.CardStarCostsLess) && !effect.GrantToCard)
					{
						if (IsTriggeredCardCostsLessDefinition(effect))
						{
							(triggeredCardCostsLessToApplyAfter ??= new List<CardExtraEffect>()).Add(effect);
						}
					}
					continue;
				}

				if (effect.Kind is (CardExtraEffectKind.CardCostsLess or CardExtraEffectKind.CardStarCostsLess) && !effect.GrantToCard)
				{
					if (IsTriggeredCardCostsLessDefinition(effect))
					{
						(triggeredCardCostsLessToApplyAfter ??= new List<CardExtraEffect>()).Add(effect);
					}
					continue;
				}

				if (effect.Timing == CardExtraEffectTiming.Immediate)
				{
					await ExecuteEffect(combatState, choiceContext, cardPlay, effect);
				}
				else
				{
					Creature? lockedTarget = null;
					Player? owner = cardPlay.Card.Owner;
					if (effect.Target == CardExtraEffectTarget.Target && owner?.Creature != null)
					{
						lockedTarget = ResolveSingleTarget(combatState, owner.Creature, cardPlay);
					}
					CardEditorExtraEffectScheduler.Schedule(combatState, cardPlay, effect, lockedTarget);
				}
			}

			bool didTriggerFatal = DidCardPlayTriggerFatal(cardPlay);
			if (didTriggerFatal)
			{
				await RunForCardPlayTrigger(combatState, choiceContext, cardPlay, effects, CardExtraEffectTrigger.Fatal);
			}

			if (powerEffectsToAdd != null && powerEffectsToAdd.Count > 0)
			{
				Creature? ownerCreature = card.Owner?.Creature;
				if (ownerCreature != null)
				{
					CardEditorExtraEffectPower? power = ownerCreature.GetPower<CardEditorExtraEffectPower>();
					if (power == null)
					{
						power = await PowerCmd.Apply<CardEditorExtraEffectPower>(ownerCreature, 1, ownerCreature, cardPlay.Card);
					}
					power?.AddPowerEffects(cardPlay.Card, powerEffectsToAdd);
				}
			}
		}
		finally
		{
			if (cardPlay.IsLastInSeries)
			{
				ClearManualTarget(card);
				CardEditorTemporaryExtraEffectController.OnAfterCardPlayed(combatState, card);
				CardEditorTemporaryEnchantmentController.OnAfterCardPlayed(combatState, card);
				if (triggeredCardCostsLessToApplyAfter != null)
				{
					foreach (CardExtraEffect effect in triggeredCardCostsLessToApplyAfter)
					{
						ApplyTriggeredCardCostsLess(combatState, card, effect);
					}
				}
			}
		}
	}

	/// <summary>
	/// Runs created-card OnPlay effects during <c>CardModel.OnPlay</c> so they resolve before base-game AfterCardPlayed triggers.
	/// This avoids custom cards gaining Vigor (or similar on-play power triggers) before their own damage/effects resolve.
	/// </summary>
	internal static async Task RunCreatedCardOnPlayDuringCardPlay(CombatState combatState, PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		CardModel? card = cardPlay?.Card;
		if (combatState == null || choiceContext == null || card == null)
		{
			return;
		}

		try
		{
			List<CardExtraEffect> effects = new List<CardExtraEffect>();
			if (CardEditorOverrides.TryGet(card.Id, out CardOverride overrideData))
			{
				IReadOnlyList<CardExtraEffect> overrideEffects = GetEffectiveExtraEffects(card, overrideData, card.CurrentUpgradeLevel > 0);
				if (overrideEffects.Count > 0)
				{
					effects.AddRange(overrideEffects);
				}
			}

			IReadOnlyList<CardExtraEffect> temporaryEffects = CardEditorTemporaryExtraEffectController.GetEffects(combatState, card);
			if (temporaryEffects.Count > 0)
			{
				effects.AddRange(temporaryEffects);
			}

			if (effects.Count == 0)
			{
				return;
			}

			Creature? ownerCreature = card.Owner?.Creature;
			VigorPreserver? vigor = null;
			int remainingImmediateDamageEffects = 0;
			if (ownerCreature != null)
			{
				remainingImmediateDamageEffects = effects.Count(e =>
					e != null
					&& !IsPowerEffect(e)
					&& e.Trigger == CardExtraEffectTrigger.OnPlay
					&& e.Timing == CardExtraEffectTiming.Immediate
					&& e.Kind == CardExtraEffectKind.DealDamage
					&& IsValidEffectAmount(e.Kind, e.Amount));

				if (remainingImmediateDamageEffects > 1)
				{
					vigor = VigorPreserver.TryCreate(ownerCreature, cardPlay.Card);
				}
			}

			foreach (CardExtraEffect effect in effects)
			{
				if (effect == null
					|| IsPowerEffect(effect)
					|| !IsValidEffectAmount(effect.Kind, effect.Amount)
					|| effect.Trigger != CardExtraEffectTrigger.OnPlay)
				{
					continue;
				}

				// Triggered CardCostsLess definitions intentionally apply after the play finishes.
				if (effect.Kind is (CardExtraEffectKind.CardCostsLess or CardExtraEffectKind.CardStarCostsLess) && !effect.GrantToCard)
				{
					continue;
				}

				if (effect.Timing == CardExtraEffectTiming.Immediate)
				{
					await ExecuteEffect(combatState, choiceContext, cardPlay, effect);

					if (vigor != null
						&& effect.Kind == CardExtraEffectKind.DealDamage
						&& effect.Timing == CardExtraEffectTiming.Immediate)
					{
						remainingImmediateDamageEffects = Math.Max(0, remainingImmediateDamageEffects - 1);
						if (remainingImmediateDamageEffects > 0)
						{
							await vigor.RestoreIfConsumed();
						}
					}
				}
				else
				{
					Creature? lockedTarget = null;
					Player? owner = cardPlay.Card.Owner;
					if (effect.Target == CardExtraEffectTarget.Target && owner?.Creature != null)
					{
						lockedTarget = ResolveSingleTarget(combatState, owner.Creature, cardPlay);
					}
					CardEditorExtraEffectScheduler.Schedule(combatState, cardPlay, effect, lockedTarget);
				}
			}

			if (vigor != null)
			{
				await vigor.FinalizeRestores();
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Created card OnPlay extra effects failed: {ex}");
		}
	}

	private sealed class VigorPreserver
	{
		private readonly Creature _target;
		private readonly Creature _applier;
		private readonly CardModel _cardSource;
		private readonly int _startAmount;
		private int _restoredAmount;

		private VigorPreserver(Creature target, CardModel cardSource, int startAmount)
		{
			_target = target;
			_applier = target;
			_cardSource = cardSource;
			_startAmount = startAmount;
		}

		public static VigorPreserver? TryCreate(Creature target, CardModel cardSource)
		{
			if (target == null || cardSource == null)
			{
				return null;
			}
			VigorPower? power = target.GetPower<VigorPower>();
			int amount = power?.Amount ?? 0;
			return amount > 0 ? new VigorPreserver(target, cardSource, amount) : null;
		}

		public async Task RestoreIfConsumed()
		{
			VigorPower? current = _target.GetPower<VigorPower>();
			int currentAmount = current?.Amount ?? 0;
			if (currentAmount >= _startAmount)
			{
				return;
			}

			int need = _startAmount - currentAmount;
			if (need <= 0)
			{
				return;
			}

			try
			{
				_ = await PowerCmd.Apply<VigorPower>(_target, need, _applier, _cardSource);
				_restoredAmount += need;
			}
			catch
			{
			}
		}

		public async Task FinalizeRestores()
		{
			if (_restoredAmount <= 0)
			{
				return;
			}

			VigorPower? power = _target.GetPower<VigorPower>();
			if (power == null)
			{
				return;
			}

			try
			{
				if (power.Amount <= _restoredAmount)
				{
					await PowerCmd.Remove(power);
				}
				else
				{
					await PowerCmd.ModifyAmount(power, -_restoredAmount, _applier, _cardSource);
				}
			}
			catch
			{
			}
		}
	}

	public static Task RunAfterCardDrawn(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card)
	{
		return RunForTrigger(combatState, choiceContext, card, CardExtraEffectTrigger.OnDraw);
	}

	public static Task RunAfterCardDiscarded(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card)
	{
		return RunForTrigger(combatState, choiceContext, card, CardExtraEffectTrigger.OnDiscard);
	}

	public static Task RunAfterCardExhausted(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card)
	{
		return RunForTrigger(combatState, choiceContext, card, CardExtraEffectTrigger.OnExhaust);
	}

	public static Task RunEndOfTurnInHand(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card)
	{
		return RunForTrigger(combatState, choiceContext, card, CardExtraEffectTrigger.EndOfTurnInHand);
	}

	public static Task RunStartOfTurn(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card)
	{
		return RunForTrigger(combatState, choiceContext, card, CardExtraEffectTrigger.StartOfTurn);
	}

	public static Task RunEndOfTurn(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card)
	{
		return RunForTrigger(combatState, choiceContext, card, CardExtraEffectTrigger.EndOfTurn);
	}

	public static Task RunStartOfEnemyTurn(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card)
	{
		return RunForTrigger(combatState, choiceContext, card, CardExtraEffectTrigger.StartOfEnemyTurn);
	}

	public static Task RunEndOfEnemyTurn(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card)
	{
		return RunForTrigger(combatState, choiceContext, card, CardExtraEffectTrigger.EndOfEnemyTurn);
	}

	public static Task RunAfterCombat(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card)
	{
		return RunForTrigger(combatState, choiceContext, card, CardExtraEffectTrigger.AfterCombat);
	}

	public static Task RunAfterOrbChanneled(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card)
	{
		return RunForTrigger(combatState, choiceContext, card, CardExtraEffectTrigger.OnChannel);
	}

	public static Task RunAfterOrbEvoked(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card)
	{
		return RunForTrigger(combatState, choiceContext, card, CardExtraEffectTrigger.OnEvoke);
	}

	public static Task RunAfterOstyDealDamage(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card)
	{
		return RunForTrigger(combatState, choiceContext, card, CardExtraEffectTrigger.OstyDealDamage);
	}

	private static async Task RunForCardPlayTrigger(CombatState combatState, PlayerChoiceContext choiceContext, CardPlay cardPlay, IReadOnlyList<CardExtraEffect> effects, CardExtraEffectTrigger trigger)
	{
		if (combatState == null || choiceContext == null || cardPlay?.Card == null || effects == null || effects.Count == 0)
		{
			return;
		}

		foreach (CardExtraEffect effect in effects)
		{
			if (effect == null || IsPowerEffect(effect) || !IsValidEffectAmount(effect.Kind, effect.Amount) || effect.Trigger != trigger)
			{
				continue;
			}

			if (effect.Kind is (CardExtraEffectKind.CardCostsLess or CardExtraEffectKind.CardStarCostsLess) && !effect.GrantToCard)
			{
				if (IsTriggeredCardCostsLessDefinition(effect))
				{
					ApplyTriggeredCardCostsLess(combatState, cardPlay.Card, effect);
				}
				continue;
			}

			if (effect.Timing == CardExtraEffectTiming.Immediate)
			{
				await ExecuteEffect(combatState, choiceContext, cardPlay, effect);
			}
			else
			{
				Creature? lockedTarget = null;
				Player? owner = cardPlay.Card.Owner;
				if (effect.Target == CardExtraEffectTarget.Target && owner?.Creature != null)
				{
					lockedTarget = ResolveSingleTarget(combatState, owner.Creature, cardPlay);
				}
				CardEditorExtraEffectScheduler.Schedule(combatState, cardPlay, effect, lockedTarget);
			}
		}
	}

	private static bool DidCardPlayTriggerFatal(CardPlay cardPlay)
	{
		if (cardPlay?.Card?.Owner?.Creature == null)
		{
			return false;
		}

		CombatHistory? history = CombatManager.Instance?.History;
		if (history == null)
		{
			return false;
		}

		CombatSide ownerSide = cardPlay.Card.Owner.Creature.Side;
		foreach (CombatHistoryEntry entry in history.Entries.Reverse())
		{
			if (entry is CardPlayStartedEntry startedEntry)
			{
				return ReferenceEquals(startedEntry.CardPlay, cardPlay);
			}

			if (entry is not DamageReceivedEntry damageEntry)
			{
				continue;
			}

			if (!ReferenceEquals(damageEntry.CardSource, cardPlay.Card)
				|| damageEntry.Result == null
				|| !damageEntry.Result.WasTargetKilled
				|| damageEntry.Receiver == null
				|| damageEntry.Receiver.Side == ownerSide)
			{
				continue;
			}

			if (damageEntry.Receiver.Powers.All(p => p.ShouldOwnerDeathTriggerFatal()))
			{
				return true;
			}
		}

		return false;
	}

	public static async Task RunAutoPlaySelfFromPile(CombatState combatState, PlayerChoiceContext choiceContext, Player player, CardExtraEffectTrigger trigger)
	{
		if (combatState == null || choiceContext == null || player == null)
		{
			return;
		}

		// Check all relevant non-hand piles
		PileType[] pilesToCheck = { PileType.Exhaust, PileType.Discard, PileType.Draw };
		foreach (PileType pileType in pilesToCheck)
		{
			CardPile? pile = pileType.GetPile(player);
			if (pile == null || pile.Cards.Count == 0)
			{
				continue;
			}

			List<CardModel> snapshot = pile.Cards.Where(c => c != null).ToList();
			foreach (CardModel card in snapshot)
			{
				await TryAutoPlaySelfFromPile(combatState, choiceContext, card, trigger);
			}
		}
	}

	private static async Task TryAutoPlaySelfFromPile(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card, CardExtraEffectTrigger trigger)
	{
		// Card may have been moved by a previous auto-play
		CardPile? cardPile = card.Pile;
		if (cardPile == null || cardPile.Type == PileType.Hand || cardPile.Type == PileType.Play)
		{
			return;
		}

		List<CardExtraEffect> effects = new List<CardExtraEffect>();
		if (CardEditorOverrides.TryGet(card.Id, out CardOverride overrideData))
		{
			IReadOnlyList<CardExtraEffect> overrideEffects = GetEffectiveExtraEffects(card, overrideData, card.CurrentUpgradeLevel > 0);
			foreach (CardExtraEffect e in overrideEffects)
			{
				CardExtraEffect? normalized = NormalizeSelfPileAutoEffect(e);
				if (normalized?.Kind == CardExtraEffectKind.ConditionalAutoPlayFromPile)
				{
					effects.Add(normalized);
				}
			}
		}

		IReadOnlyList<CardExtraEffect> temporaryEffects = CardEditorTemporaryExtraEffectController.GetEffects(combatState, card);
		foreach (CardExtraEffect e in temporaryEffects)
		{
			CardExtraEffect? normalized = NormalizeSelfPileAutoEffect(e);
			if (normalized?.Kind == CardExtraEffectKind.ConditionalAutoPlayFromPile)
			{
				effects.Add(normalized);
			}
		}

		foreach (CardExtraEffect effect in effects)
		{
			if (effect.Trigger != trigger)
			{
				continue;
			}

			// Verify the card is still in the required pile
			CardPile? currentPile = card.Pile;
			if (currentPile == null)
			{
				continue;
			}

			if (effect.CardSelectionPile != CardExtraEffectCardPile.AllPiles)
			{
				PileType requiredType = ResolvePileType(effect.CardSelectionPile);
				if (currentPile.Type != requiredType)
				{
					continue;
				}
			}

			if (effect.ScaleMode != CardExtraEffectScaleMode.None)
			{
				if (CardEditorConditionalFromPileFiredTracker.HasFired(combatState, card))
				{
					continue;
				}

				int count = GetHistoryCountMultiplier(combatState, card.Owner?.Creature, null, effect);
				if (!DoesCountConditionPass(count, effect))
				{
					continue;
				}

				CardEditorConditionalFromPileFiredTracker.MarkFired(combatState, card);
			}

			await CardCmd.AutoPlay(choiceContext, card, null);
			return; // Only auto-play once per card per trigger
		}
	}

	public static async Task RunAutoDrawSelfFromPile(CombatState combatState, PlayerChoiceContext choiceContext, Player player, CardExtraEffectTrigger trigger)
	{
		if (combatState == null || choiceContext == null || player == null)
		{
			return;
		}

		PileType[] pilesToCheck = { PileType.Exhaust, PileType.Discard, PileType.Draw };
		foreach (PileType pileType in pilesToCheck)
		{
			CardPile? pile = pileType.GetPile(player);
			if (pile == null || pile.Cards.Count == 0)
			{
				continue;
			}

			List<CardModel> snapshot = pile.Cards.Where(c => c != null).ToList();
			foreach (CardModel card in snapshot)
			{
				await TryAutoDrawSelfFromPile(combatState, choiceContext, card, trigger);
			}
		}
	}

	private static async Task TryAutoDrawSelfFromPile(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card, CardExtraEffectTrigger trigger)
	{
		CardPile? cardPile = card.Pile;
		if (cardPile == null || cardPile.Type == PileType.Hand || cardPile.Type == PileType.Play)
		{
			return;
		}

		List<CardExtraEffect> effects = new List<CardExtraEffect>();
		if (CardEditorOverrides.TryGet(card.Id, out CardOverride overrideData))
		{
			IReadOnlyList<CardExtraEffect> overrideEffects = GetEffectiveExtraEffects(card, overrideData, card.CurrentUpgradeLevel > 0);
			foreach (CardExtraEffect e in overrideEffects)
			{
				CardExtraEffect? normalized = NormalizeSelfPileAutoEffect(e);
				if (normalized?.Kind == CardExtraEffectKind.ConditionalAutoDrawFromPile)
				{
					effects.Add(normalized);
				}
			}
		}

		IReadOnlyList<CardExtraEffect> temporaryEffects = CardEditorTemporaryExtraEffectController.GetEffects(combatState, card);
		foreach (CardExtraEffect e in temporaryEffects)
		{
			CardExtraEffect? normalized = NormalizeSelfPileAutoEffect(e);
			if (normalized?.Kind == CardExtraEffectKind.ConditionalAutoDrawFromPile)
			{
				effects.Add(normalized);
			}
		}

		foreach (CardExtraEffect effect in effects)
		{
			if (effect.Trigger != trigger)
			{
				continue;
			}

			CardPile? currentPile = card.Pile;
			if (currentPile == null || currentPile.Type == PileType.Hand || currentPile.Type == PileType.Play)
			{
				continue;
			}

			if (effect.CardSelectionPile != CardExtraEffectCardPile.AllPiles)
			{
				PileType requiredType = ResolvePileType(effect.CardSelectionPile);
				if (currentPile.Type != requiredType)
				{
					continue;
				}
			}

			if (effect.ScaleMode != CardExtraEffectScaleMode.None)
			{
				if (CardEditorConditionalFromPileFiredTracker.HasFired(combatState, card))
				{
					continue;
				}

				int count = GetHistoryCountMultiplier(combatState, card.Owner?.Creature, null, effect);
				if (!DoesCountConditionPass(count, effect))
				{
					continue;
				}

				CardEditorConditionalFromPileFiredTracker.MarkFired(combatState, card);
			}

			await CardPileCmd.Add(card, PileType.Hand);
			return; // Only draw once per card per trigger
		}
	}

	public static async Task RunConditionalAutoFromPile(CombatState combatState, PlayerChoiceContext choiceContext, Player player)
	{
		if (combatState == null || choiceContext == null || player == null)
		{
			return;
		}

		PileType[] pilesToCheck = { PileType.Exhaust, PileType.Discard, PileType.Draw };
		foreach (PileType pileType in pilesToCheck)
		{
			CardPile? pile = pileType.GetPile(player);
			if (pile == null || pile.Cards.Count == 0)
			{
				continue;
			}

			List<CardModel> snapshot = pile.Cards.Where(c => c != null).ToList();
			foreach (CardModel card in snapshot)
			{
				await TryConditionalAutoFromPile(combatState, choiceContext, card);
			}
		}
	}

	private static async Task TryConditionalAutoFromPile(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card)
	{
		CardPile? cardPile = card.Pile;
		if (cardPile == null || cardPile.Type == PileType.Hand || cardPile.Type == PileType.Play)
		{
			return;
		}

		if (CardEditorConditionalFromPileFiredTracker.HasFired(combatState, card))
		{
			return;
		}

		List<CardExtraEffect> effects = new List<CardExtraEffect>();
		if (CardEditorOverrides.TryGet(card.Id, out CardOverride overrideData))
		{
			IReadOnlyList<CardExtraEffect> overrideEffects = GetEffectiveExtraEffects(card, overrideData, card.CurrentUpgradeLevel > 0);
			foreach (CardExtraEffect e in overrideEffects)
			{
				if (e?.Kind is CardExtraEffectKind.ConditionalAutoPlayFromPile or CardExtraEffectKind.ConditionalAutoDrawFromPile)
				{
					effects.Add(e);
				}
			}
		}

		IReadOnlyList<CardExtraEffect> temporaryEffects = CardEditorTemporaryExtraEffectController.GetEffects(combatState, card);
		foreach (CardExtraEffect e in temporaryEffects)
		{
			if (e?.Kind is CardExtraEffectKind.ConditionalAutoPlayFromPile or CardExtraEffectKind.ConditionalAutoDrawFromPile)
			{
				effects.Add(e);
			}
		}

		foreach (CardExtraEffect effect in effects)
		{
			CardPile? currentPile = card.Pile;
			if (currentPile == null || currentPile.Type == PileType.Hand || currentPile.Type == PileType.Play)
			{
				continue;
			}

			if (effect.CardSelectionPile != CardExtraEffectCardPile.AllPiles)
			{
				PileType requiredType = ResolvePileType(effect.CardSelectionPile);
				if (currentPile.Type != requiredType)
				{
					continue;
				}
			}

			int threshold = Math.Max(1, effect.Amount);
			int count = GetHistoryCountMultiplier(combatState, card.Owner?.Creature, null, effect);
			if (count < threshold)
			{
				continue;
			}

			CardEditorConditionalFromPileFiredTracker.MarkFired(combatState, card);

			if (effect.Kind == CardExtraEffectKind.ConditionalAutoPlayFromPile)
			{
				await CardCmd.AutoPlay(choiceContext, card, null);
			}
			else
			{
				await CardPileCmd.Add(card, PileType.Hand);
			}
			return;
		}
	}

	private static async Task RunForTrigger(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card, CardExtraEffectTrigger trigger)
	{
		if (combatState == null || choiceContext == null || card == null)
		{
			return;
		}

		using IDisposable _ = CardEditorCardPlayContext.PushScoped(new CardPlay
		{
			Card = card,
			Target = null,
			ResultPile = card.Pile?.Type ?? PileType.None,
			Resources = new ResourceInfo
			{
				EnergySpent = 0,
				EnergyValue = 0,
				StarsSpent = 0,
				StarValue = 0
			},
			IsAutoPlay = true,
			PlayIndex = 0,
			PlayCount = 1
		});

		try
		{
			List<CardExtraEffect> effects = new List<CardExtraEffect>();
			if (CardEditorOverrides.TryGet(card.Id, out CardOverride overrideData))
			{
				IReadOnlyList<CardExtraEffect> overrideEffects = GetEffectiveExtraEffects(card, overrideData, card.CurrentUpgradeLevel > 0);
				if (overrideEffects.Count > 0)
				{
					effects.AddRange(overrideEffects);
				}
			}

			IReadOnlyList<CardExtraEffect> temporaryEffects = CardEditorTemporaryExtraEffectController.GetEffects(combatState, card);
			if (temporaryEffects.Count > 0)
			{
				// Never "execute" CardCostsLess; it is a passive modifier and is handled by the cost patches.
				foreach (CardExtraEffect effect in temporaryEffects)
				{
					if (effect == null || effect.Kind == CardExtraEffectKind.CardCostsLess)
					{
						continue;
					}
					effects.Add(effect);
				}
			}

			if (effects.Count == 0)
			{
				return;
			}

			CardPlay syntheticPlay = new CardPlay
			{
				Card = card,
				Target = null,
				ResultPile = card.Pile?.Type ?? PileType.None,
				Resources = new ResourceInfo
				{
					EnergySpent = 0,
					EnergyValue = 0,
					StarsSpent = 0,
					StarValue = 0
				},
				IsAutoPlay = true,
				PlayIndex = 0,
				PlayCount = 1
			};

			foreach (CardExtraEffect effect in effects)
			{
				if (effect == null
					|| IsPowerEffect(effect)
					|| !IsValidEffectAmount(effect.Kind, effect.Amount)
					|| !DoesTriggerMatch(effect, trigger, card))
				{
					continue;
				}

				if (effect.Kind is (CardExtraEffectKind.CardCostsLess or CardExtraEffectKind.CardStarCostsLess) && !effect.GrantToCard)
				{
					if (IsTriggeredCardCostsLessDefinition(effect))
					{
						ApplyTriggeredCardCostsLess(combatState, card, effect);
					}
					continue;
				}

				if (effect.Timing == CardExtraEffectTiming.Immediate)
				{
					await ExecuteEffect(combatState, choiceContext, syntheticPlay, effect);
				}
				else
				{
					Creature? lockedTarget = null;
					Player? owner = card.Owner;
					if (effect.Target == CardExtraEffectTarget.Target && owner?.Creature != null)
					{
						lockedTarget = ResolveSingleTarget(combatState, owner.Creature, syntheticPlay);
					}
					CardEditorExtraEffectScheduler.Schedule(combatState, syntheticPlay, effect, lockedTarget);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Extra effects (trigger={trigger}) failed: {ex}");
		}
	}

	internal static bool DoesTriggerMatch(CardExtraEffect effect, CardExtraEffectTrigger observedTrigger, CardModel card)
	{
		if (effect == null)
		{
			return false;
		}
		if (effect.Trigger == observedTrigger
			|| (observedTrigger == CardExtraEffectTrigger.EndOfTurnInHand && effect.Trigger == CardExtraEffectTrigger.EndOfTurn))
		{
			return true;
		}
		if (effect.Trigger != CardExtraEffectTrigger.TurnBoundary)
		{
			return false;
		}
		if (!TryMapObservedTurnBoundaryTrigger(observedTrigger, out CardExtraEffectTurnBoundary boundary, out CardExtraEffectTurnBoundarySide side))
		{
			return false;
		}
		if (effect.TurnBoundary != boundary)
		{
			return false;
		}
		if (effect.TurnBoundarySide != CardExtraEffectTurnBoundarySide.Both && effect.TurnBoundarySide != side)
		{
			return false;
		}
		if (effect.TurnBoundaryCardLocation != CardExtraEffectTurnBoundaryCardLocation.Any
			&& !IsCardInTurnBoundaryLocation(card, effect.TurnBoundaryCardLocation))
		{
			return false;
		}
		return true;
	}

	private static bool TryMapObservedTurnBoundaryTrigger(CardExtraEffectTrigger observedTrigger, out CardExtraEffectTurnBoundary boundary, out CardExtraEffectTurnBoundarySide side)
	{
		switch (observedTrigger)
		{
			case CardExtraEffectTrigger.StartOfTurn:
				boundary = CardExtraEffectTurnBoundary.Start;
				side = CardExtraEffectTurnBoundarySide.YourTurn;
				return true;
			case CardExtraEffectTrigger.EndOfTurn:
			case CardExtraEffectTrigger.EndOfTurnInHand:
				boundary = CardExtraEffectTurnBoundary.End;
				side = CardExtraEffectTurnBoundarySide.YourTurn;
				return true;
			case CardExtraEffectTrigger.StartOfEnemyTurn:
				boundary = CardExtraEffectTurnBoundary.Start;
				side = CardExtraEffectTurnBoundarySide.EnemyTurn;
				return true;
			case CardExtraEffectTrigger.EndOfEnemyTurn:
				boundary = CardExtraEffectTurnBoundary.End;
				side = CardExtraEffectTurnBoundarySide.EnemyTurn;
				return true;
			default:
				boundary = default;
				side = default;
				return false;
		}
	}

	private static bool IsCardInTurnBoundaryLocation(CardModel card, CardExtraEffectTurnBoundaryCardLocation location)
	{
		if (card?.Pile == null)
		{
			return false;
		}
		return location switch
		{
			CardExtraEffectTurnBoundaryCardLocation.Hand => card.Pile.Type == PileType.Hand,
			CardExtraEffectTurnBoundaryCardLocation.DrawPile => card.Pile.Type == PileType.Draw,
			CardExtraEffectTurnBoundaryCardLocation.DiscardPile => card.Pile.Type == PileType.Discard,
			CardExtraEffectTurnBoundaryCardLocation.ExhaustPile => card.Pile.Type == PileType.Exhaust,
			_ => true
		};
	}

	private static void ApplyTriggeredCardCostsLess(CombatState combatState, CardModel card, CardExtraEffect effect)
	{
		if (combatState == null || card == null || effect == null)
		{
			return;
		}
		if (!IsValidEffectAmount(effect.Kind, effect.Amount))
		{
			return;
		}

		CardExtraEffectCardGrantDuration duration = effect.CardCostsLessDuration switch
		{
			CardExtraEffectCardCostsLessDuration.ThisTurn => CardExtraEffectCardGrantDuration.ThisTurn,
			CardExtraEffectCardCostsLessDuration.ThisCombat => CardExtraEffectCardGrantDuration.ThisCombat,
			CardExtraEffectCardCostsLessDuration.UntilPlayed => CardExtraEffectCardGrantDuration.UntilPlayed,
			CardExtraEffectCardCostsLessDuration.Turns => CardExtraEffectCardGrantDuration.Turns,
			// "Permanent" means "for the rest of combat" once it has triggered.
			_ => CardExtraEffectCardGrantDuration.ThisCombat
		};
		int turns = duration == CardExtraEffectCardGrantDuration.Turns
			? Math.Clamp(effect.CardCostsLessTurns, 1, 99)
			: 1;

		CardExtraEffect active = CloneEffect(effect);
		if (active.AmountIsX)
		{
			int x = active.Kind == CardExtraEffectKind.CardStarCostsLess
				? ResolveStarXValueForCostEffect(card, CardEditorCardPlayContext.Current)
				: ResolveEnergyXValueForCostEffect(card, CardEditorCardPlayContext.Current);
			x = Math.Max(0, x + active.AmountXPlus);
			int sign = active.Amount < 0 ? -1 : 1;
			active.Amount = sign * x;
			active.AmountIsX = false;
		}
		active.Trigger = CardExtraEffectTrigger.OnPlay;
		active.Timing = CardExtraEffectTiming.Immediate;
		active.Turns = 1;
		active.GrantToCard = false;
		active.CardCostsLessMode = CardExtraEffectCardCostsLessMode.Passive;

		// Stack repeated triggers (e.g. "Costs 1 less each time it's played") by merging into an equivalent existing grant when possible.
		if (!CardEditorTemporaryExtraEffectController.TryStackTimedCardCostsLess(combatState, card, active, duration, turns))
		{
		CardEditorTemporaryExtraEffectController.Grant(combatState, card, active, duration, turns);
		}
		card.InvokeEnergyCostChanged();
	}

	private static string? TryFormatLine(CardModel card, CardExtraEffect effect, Creature? target, bool forceUpgradeHighlight)
	{
		bool amountIsX = effect.AmountIsX;
		int baseAmount = effect.Amount;
		if (!amountIsX && !IsValidEffectAmount(effect.Kind, baseAmount))
		{
			return null;
		}
		if (amountIsX && !IsValidEffectAmount(effect.Kind, 1))
		{
			return null;
		}

		int grammarAmount = amountIsX ? 2 : baseAmount;

		int historyMultiplier = 0;
		if (!amountIsX && effect.ScaleMode == CardExtraEffectScaleMode.PerHistoryCount
			&& SupportsHistoryScaling(effect.Kind)
			&& card.CombatState != null
			&& card.Owner?.Creature != null)
		{
			historyMultiplier = Math.Max(0, GetHistoryCountMultiplier(card.CombatState, card.Owner.Creature, cardPlay: null, effect));
		}

		string amountText = amountIsX
			? FormatXPlusText(effect.AmountXPlus)
			: (effect.Kind == CardExtraEffectKind.CreatedCardsCostLess && baseAmount == -1)
				? "0"
				: baseAmount.ToString(CultureInfo.InvariantCulture);
		if (forceUpgradeHighlight)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, 1);
		}

		string? powerDurationSuffix = (effect.Duration == CardExtraEffectDuration.ThisTurn && SupportsDuration(effect.Kind))
			? CardEditorLoc.T("cardText.duration.thisTurn", "this turn")
			: null;

		string? cardCostsLessDurationSuffix = null;
		if (effect.Kind is CardExtraEffectKind.CardCostsLess
			or CardExtraEffectKind.CardStarCostsLess
			or CardExtraEffectKind.CardTypeCostsLess
			or CardExtraEffectKind.CardTypeStarCostsLess
			or CardExtraEffectKind.DrawnCardsCostLess
			or CardExtraEffectKind.GeneratedCardsCostLess
			or CardExtraEffectKind.GeneratedCardsUpgraded
			or CardExtraEffectKind.CardsInPileUpgradedAura
			or CardExtraEffectKind.DrawCardsThatCostLess
			or CardExtraEffectKind.UpgradeCardsInPile)
		{
			cardCostsLessDurationSuffix = effect.CardCostsLessDuration switch
			{
				CardExtraEffectCardCostsLessDuration.ThisTurn => CardEditorLoc.T("cardText.duration.thisTurn", "this turn"),
				CardExtraEffectCardCostsLessDuration.ThisCombat => CardEditorLoc.T("cardText.duration.thisCombat", "this combat"),
				CardExtraEffectCardCostsLessDuration.UntilPlayed => CardEditorLoc.T("cardText.duration.untilPlayed", "until played"),
				CardExtraEffectCardCostsLessDuration.Turns => CardEditorLoc.F(
					"cardText.duration.forTurns",
					$"for {Math.Max(1, effect.CardCostsLessTurns).ToString(CultureInfo.InvariantCulture)} turns",
					("Turns", Math.Max(1, effect.CardCostsLessTurns))),
				_ => null
			};
		}

		bool usesHistoryScalingWording = !amountIsX && effect.ScaleMode == CardExtraEffectScaleMode.PerHistoryCount
			&& SupportsHistoryScaling(effect.Kind)
			&& effect.Kind is CardExtraEffectKind.DealDamage or CardExtraEffectKind.GainBlock;

		bool usesTwoLineHistoryScalingPreview = !amountIsX
			&& !usesHistoryScalingWording
			&& effect.ScaleMode == CardExtraEffectScaleMode.PerHistoryCount
			&& SupportsHistoryScaling(effect.Kind)
			&& card.CombatState != null
			&& card.Owner?.Creature != null;

		string? FormatLineForAmount(int baseAmountForLine, string amountTextForLine, int grammarAmountForLine, bool allowDamageBlockPreview, bool forceNumericEnergyStars)
		{
			int baseAmount = baseAmountForLine;
			int grammarAmount = grammarAmountForLine;
			string amountText = amountTextForLine;

			if (allowDamageBlockPreview && !amountIsX && effect.Kind is CardExtraEffectKind.DealDamage or CardExtraEffectKind.GainBlock)
			{
				TryGetScaledAmountText(card, effect, baseAmount, target, forceUpgradeHighlight, out amountText);
			}

			string energyText = (amountIsX || forceNumericEnergyStars)
				? amountText + BuildEnergyIcons(card, 1)
				: BuildEnergyIcons(card, baseAmount);
			string starText = (amountIsX || forceNumericEnergyStars)
				? amountText + BuildStarIcons(1)
				: BuildStarIcons(baseAmount);

			return effect.Kind switch
			{
				CardExtraEffectKind.GainBlock => FormatGainBlock(effect.Target, amountText),
				CardExtraEffectKind.DealDamage => FormatDealDamage(effect.Target, amountText),
				CardExtraEffectKind.DrawCards => CardEditorLoc.F("cardText.drawCards", $"Draw {amountText} {(grammarAmount == 1 ? "card" : "cards")}.", ("Amount", amountText)),
				CardExtraEffectKind.GainEnergy => CardEditorLoc.F("cardText.gainEnergy", $"Gain {energyText}.", ("Amount", energyText)),
				CardExtraEffectKind.GainStars => CardEditorLoc.F("cardText.gainStars", $"Gain {starText}.", ("Amount", starText)),
				CardExtraEffectKind.Heal => effect.Target switch
				{
					CardExtraEffectTarget.AllEnemies => CardEditorLoc.F("cardText.heal.allEnemies", $"ALL enemies heal {amountText} HP.", ("Amount", amountText)),
					CardExtraEffectTarget.RandomEnemy => CardEditorLoc.F("cardText.heal.randomEnemy", $"A random enemy heals {amountText} HP.", ("Amount", amountText)),
					CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.heal.self", $"Heal {amountText} HP.", ("Amount", amountText)),
					_ => CardEditorLoc.F("cardText.heal.target", $"Enemy heals {amountText} HP.", ("Amount", amountText))
				},
				CardExtraEffectKind.LoseHp => effect.Target switch
				{
					CardExtraEffectTarget.AllEnemies => CardEditorLoc.F("cardText.loseHp.allEnemies", $"ALL enemies lose {amountText} HP.", ("Amount", amountText)),
					CardExtraEffectTarget.RandomEnemy => CardEditorLoc.F("cardText.loseHp.randomEnemy", $"A random enemy loses {amountText} HP.", ("Amount", amountText)),
					CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.loseHp.self", $"Lose {amountText} HP.", ("Amount", amountText)),
					_ => CardEditorLoc.F("cardText.loseHp.target", $"Enemy loses {amountText} HP.", ("Amount", amountText))
				},
				CardExtraEffectKind.GainStrength => amountIsX
					? FormatSignedPowerText(effect.Target, gain: true, amountText, "Strength", powerDurationSuffix)
					: FormatSignedPower(effect.Target, baseAmount, "Strength", powerDurationSuffix),
				CardExtraEffectKind.LoseStrength => amountIsX
					? FormatSignedPowerText(effect.Target, gain: false, amountText, "Strength", powerDurationSuffix)
					: FormatSignedPower(effect.Target, -baseAmount, "Strength", powerDurationSuffix),
				CardExtraEffectKind.GainDexterity => amountIsX
					? FormatSignedPowerText(effect.Target, gain: true, amountText, "Dexterity", powerDurationSuffix)
					: FormatSignedPower(effect.Target, baseAmount, "Dexterity", powerDurationSuffix),
				CardExtraEffectKind.LoseDexterity => amountIsX
					? FormatSignedPowerText(effect.Target, gain: false, amountText, "Dexterity", powerDurationSuffix)
					: FormatSignedPower(effect.Target, -baseAmount, "Dexterity", powerDurationSuffix),
				CardExtraEffectKind.GainFocus => amountIsX
					? FormatSignedPowerText(effect.Target, gain: true, amountText, "Focus", powerDurationSuffix)
					: FormatSignedPower(effect.Target, baseAmount, "Focus", powerDurationSuffix),
				CardExtraEffectKind.LoseFocus => amountIsX
					? FormatSignedPowerText(effect.Target, gain: false, amountText, "Focus", powerDurationSuffix)
					: FormatSignedPower(effect.Target, -baseAmount, "Focus", powerDurationSuffix),
				CardExtraEffectKind.ApplyWeak => amountIsX ? FormatApplyDebuffText(effect.Target, amountText, "Weak", powerDurationSuffix) : FormatApplyDebuff(effect.Target, baseAmount, "Weak", powerDurationSuffix),
				CardExtraEffectKind.ApplyFrail => amountIsX ? FormatApplyDebuffText(effect.Target, amountText, "Frail", powerDurationSuffix) : FormatApplyDebuff(effect.Target, baseAmount, "Frail", powerDurationSuffix),
				CardExtraEffectKind.ApplyVulnerable => amountIsX ? FormatApplyDebuffText(effect.Target, amountText, "Vulnerable", powerDurationSuffix) : FormatApplyDebuff(effect.Target, baseAmount, "Vulnerable", powerDurationSuffix),
				CardExtraEffectKind.ApplyPoison => amountIsX ? FormatApplyDebuffText(effect.Target, amountText, "Poison", powerDurationSuffix) : FormatApplyDebuff(effect.Target, baseAmount, "Poison", powerDurationSuffix),
				CardExtraEffectKind.ApplyDoom => amountIsX ? FormatApplyDebuffText(effect.Target, amountText, "Doom", powerDurationSuffix) : FormatApplyDebuff(effect.Target, baseAmount, "Doom", powerDurationSuffix),
				CardExtraEffectKind.GainArtifact => FormatGainPower(effect.Target, amountText, "Artifact", powerDurationSuffix),
				CardExtraEffectKind.GainThorns => FormatGainPower(effect.Target, amountText, "Thorns", powerDurationSuffix),
				CardExtraEffectKind.GainRegen => FormatGainPower(effect.Target, amountText, "Regen", powerDurationSuffix),
				CardExtraEffectKind.GainPlating => FormatGainPower(effect.Target, amountText, "Plating", powerDurationSuffix),
				CardExtraEffectKind.GainIntangible => FormatGainPower(effect.Target, amountText, "Intangible", powerDurationSuffix),
				CardExtraEffectKind.GainBuffer => FormatGainPower(effect.Target, amountText, "Buffer", powerDurationSuffix),
				CardExtraEffectKind.GainVigor => FormatGainPower(effect.Target, amountText, "Vigor", powerDurationSuffix),
				CardExtraEffectKind.GainBlur => FormatGainPower(effect.Target, amountText, "Blur", powerDurationSuffix),
				CardExtraEffectKind.GainRitual => FormatGainPower(effect.Target, amountText, "Ritual", powerDurationSuffix),
				CardExtraEffectKind.ApplyConstrict => amountIsX ? FormatApplyDebuffText(effect.Target, amountText, "Constrict", powerDurationSuffix) : FormatApplyDebuff(effect.Target, baseAmount, "Constrict", powerDurationSuffix),
				CardExtraEffectKind.CreatedCardsCostLess => FormatCreatedCardsCostLess(effect, amountText),
				CardExtraEffectKind.CreatedCardsUpgraded => FormatCreatedCardsUpgraded(grammarAmount, amountText),
				CardExtraEffectKind.GeneratedCardsUpgraded => FormatGeneratedCardsUpgraded(effect, grammarAmount, amountText, cardCostsLessDurationSuffix),
				CardExtraEffectKind.CardsInPileUpgradedAura => FormatCardsInPileUpgradedAura(effect, grammarAmount, amountText, cardCostsLessDurationSuffix),
				CardExtraEffectKind.AddRandomCardToHand => FormatAddRandomCardToHand(effect, grammarAmount, amountText),
				CardExtraEffectKind.ChooseOneOfThreeCardsToHand => FormatChooseOneOfThreeToHand(effect, grammarAmount, amountText),
				CardExtraEffectKind.Summon => CardEditorLoc.F("cardText.summon", $"[gold]Summon[/gold] {amountText}.", ("Amount", amountText)),
				CardExtraEffectKind.Forge => CardEditorLoc.F("cardText.forge", $"[gold]Forge[/gold] {amountText}.", ("Amount", amountText)),
				CardExtraEffectKind.ChannelLightning => FormatChannelOrb(amountText, grammarAmount, "Lightning"),
				CardExtraEffectKind.ChannelFrost => FormatChannelOrb(amountText, grammarAmount, "Frost"),
				CardExtraEffectKind.ChannelDark => FormatChannelOrb(amountText, grammarAmount, "Dark"),
				CardExtraEffectKind.ChannelPlasma => FormatChannelOrb(amountText, grammarAmount, "Plasma"),
				CardExtraEffectKind.ChannelGlass => FormatChannelOrb(amountText, grammarAmount, "Glass"),
				CardExtraEffectKind.ChannelRandomOrb => FormatChannelRandomOrb(amountText, grammarAmount),
				CardExtraEffectKind.GainOrbSlots => CardEditorLoc.F("cardText.gainOrbSlots", $"Gain {amountText} Orb Slots.", ("Amount", amountText)),
				CardExtraEffectKind.LoseOrbSlots => CardEditorLoc.F("cardText.loseOrbSlots", $"Lose {amountText} Orb Slots.", ("Amount", amountText)),
				CardExtraEffectKind.EndTurn => CardEditorLoc.T("cardText.endTurn", "End your turn."),
				CardExtraEffectKind.EnchantCard => FormatEnchantCard(effect, amountText),
				CardExtraEffectKind.IgnoreBlock => CardEditorLoc.T("cardText.ignoreBlock", "This card's damage ignores Block."),
				CardExtraEffectKind.IgnoreDamageModifiers => CardEditorLoc.T("cardText.ignoreDamageModifiers", "This card's damage ignores damage modifiers."),
				CardExtraEffectKind.IgnoreDamageCaps => CardEditorLoc.T("cardText.ignoreDamageCaps", "This card's damage ignores damage caps."),
				CardExtraEffectKind.IgnoreDamageNegation => CardEditorLoc.T("cardText.ignoreDamageNegation", "This card's damage ignores damage negation."),
				CardExtraEffectKind.IgnoreEnemyDamageReductions => CardEditorLoc.T("cardText.ignoreEnemyDamageReductions", "This card's damage ignores enemy damage reduction effects."),
				CardExtraEffectKind.RemoveBlock => FormatRemoveBlock(effect.Target, amountText),
				CardExtraEffectKind.RemoveArtifact => FormatRemoveArtifact(effect.Target, amountText),
				CardExtraEffectKind.MultiplyStatStatus => FormatMultiplyStatStatus(effect.Target, amountText, effect.MultiplierStat),
				CardExtraEffectKind.CardTypeCostsLess => amountIsX
					? FormatCardTypeCostDeltaText(effect, amountText + BuildEnergyIcons(card, 1), isLess: baseAmount > 0, cardCostsLessDurationSuffix, forceUpgradeHighlight)
					: FormatCardTypeCostDelta(card, effect, baseAmount, cardCostsLessDurationSuffix, forceUpgradeHighlight),
				CardExtraEffectKind.CardTypeStarCostsLess => amountIsX
					? FormatCardTypeStarCostDeltaText(effect, amountText + BuildStarIcons(1), isLess: baseAmount > 0, cardCostsLessDurationSuffix, forceUpgradeHighlight)
					: FormatCardTypeStarCostDelta(effect, baseAmount, cardCostsLessDurationSuffix, forceUpgradeHighlight),
				CardExtraEffectKind.CardCostsLess => amountIsX
					? FormatCardCostDeltaText(amountText, isLess: baseAmount > 0, cardCostsLessDurationSuffix, forceUpgradeHighlight)
					: FormatCardCostDelta(baseAmount, cardCostsLessDurationSuffix, forceUpgradeHighlight),
				CardExtraEffectKind.CardStarCostsLess => amountIsX
					? FormatCardStarCostDeltaText(amountText + BuildStarIcons(1), isLess: baseAmount > 0, cardCostsLessDurationSuffix, forceUpgradeHighlight)
					: FormatCardStarCostDelta(baseAmount, cardCostsLessDurationSuffix, forceUpgradeHighlight),
				CardExtraEffectKind.DrawnCardsCostLess => amountIsX
					? FormatDrawnCardsCostDeltaText(effect, amountText + BuildEnergyIcons(card, 1), isLess: baseAmount > 0, cardCostsLessDurationSuffix, forceUpgradeHighlight)
					: FormatDrawnCardsCostDelta(card, effect, baseAmount, cardCostsLessDurationSuffix, forceUpgradeHighlight),
				CardExtraEffectKind.GeneratedCardsCostLess => amountIsX
					? FormatGeneratedCardsCostDeltaText(effect, amountText + BuildEnergyIcons(card, 1), isLess: baseAmount > 0, cardCostsLessDurationSuffix, forceUpgradeHighlight)
					: FormatGeneratedCardsCostDelta(card, effect, baseAmount, cardCostsLessDurationSuffix, forceUpgradeHighlight),
				CardExtraEffectKind.DiscardCards => FormatDiscardCards(effect, grammarAmount, amountText),
				CardExtraEffectKind.ExhaustCards => FormatExhaustCards(effect, grammarAmount, amountText),
				CardExtraEffectKind.EvokeOrbs => FormatEvokeOrbs(grammarAmount, amountText),
				CardExtraEffectKind.OrbAction => FormatOrbAction(effect, grammarAmount, amountText),
				CardExtraEffectKind.OstyAction => FormatOstyAction(effect, grammarAmount, amountText),
				CardExtraEffectKind.MoveCardsBetweenPiles => FormatMoveCardsBetweenPiles(effect, grammarAmount, amountText),
				CardExtraEffectKind.UpgradeCardsInPile => FormatUpgradeCardsInPile(effect, grammarAmount, amountText, cardCostsLessDurationSuffix),
				CardExtraEffectKind.AddCopyOfThisCard => FormatAddCopyOfThisCard(effect, grammarAmount, amountText),
				CardExtraEffectKind.AddSpecificCardToHand => FormatAddSpecificCardToHand(effect, grammarAmount, amountText),
				CardExtraEffectKind.PlayCardFromPile => FormatPlayCardFromPile(effect, grammarAmount, amountText),
				CardExtraEffectKind.AutoPlaySelfFromPile => FormatAutoPlaySelfFromPile(effect),
				CardExtraEffectKind.DrawCardsThatCostLess => FormatDrawCardsThatCostLess(effect, grammarAmount, amountText, cardCostsLessDurationSuffix),
				CardExtraEffectKind.AutoDrawSelfFromPile => FormatAutoDrawSelfFromPile(effect),
				CardExtraEffectKind.ConditionalAutoPlayFromPile => FormatAutoPlaySelfFromPile(NormalizeSelfPileAutoEffect(effect) ?? effect),
				CardExtraEffectKind.ConditionalAutoDrawFromPile => FormatAutoDrawSelfFromPile(NormalizeSelfPileAutoEffect(effect) ?? effect),
				CardExtraEffectKind.GrantKeywordToPile => FormatGrantKeywordToPile(effect, grammarAmount, amountText),
				CardExtraEffectKind.GainGold => $"Gain {amountText} Gold.",
				CardExtraEffectKind.UpgradeDeckCards => FormatUpgradeDeckCards(effect, grammarAmount, amountText),
				CardExtraEffectKind.FetchSpecificCardToHand => FormatFetchSpecificCardToHand(effect, grammarAmount, amountText),
				_ => null
			};
		}

		string? line = null;
		if (usesHistoryScalingWording)
		{
			// For history-scaled damage/block, render like vanilla scaling cards:
			// - A "base" line that shows the current total (so it can preview Vulnerable/etc and update as history changes).
			// - A definition line that shows the per-trigger coefficient (not colored by combat modifiers).
			int totalBaseAmount = effect.HistoryScalingIncludesBase
				? (int)Math.Clamp((long)baseAmount * (1L + historyMultiplier), 0L, int.MaxValue)
				: (int)Math.Clamp((long)baseAmount * historyMultiplier, 0L, int.MaxValue);

			string totalAmountText = totalBaseAmount.ToString(CultureInfo.InvariantCulture);
			TryGetScaledAmountText(card, effect, totalBaseAmount, target, forceUpgradeHighlight, out totalAmountText);

			string coefficientText = baseAmount.ToString(CultureInfo.InvariantCulture);
			if (forceUpgradeHighlight)
			{
				coefficientText = StsTextUtilities.HighlightChangeText(coefficientText, 1);
			}

			string definitionBase = effect.Kind == CardExtraEffectKind.DealDamage
				? CardEditorLoc.F("cardText.scaling.additionalDamage", $"Deals {coefficientText} additional damage.", ("Amount", coefficientText))
				: CardEditorLoc.F("cardText.scaling.additionalBlock", $"Gains {coefficientText} additional [gold]Block[/gold].", ("Amount", coefficientText));
			string definitionLine = ApplyHistoryScalingSuffix(card, definitionBase, effect);

			string baseLine = effect.Kind == CardExtraEffectKind.GainBlock
				? FormatGainBlock(effect.Target, totalAmountText)
				: FormatDealDamage(effect.Target, totalAmountText);

			line = baseLine + "\n" + definitionLine;
		}
		else if (usesTwoLineHistoryScalingPreview)
		{
			string? definitionCoreLine = FormatLineForAmount(baseAmount, amountText, grammarAmount, allowDamageBlockPreview: false, forceNumericEnergyStars: false);
			if (string.IsNullOrWhiteSpace(definitionCoreLine))
			{
				return null;
			}

			int totalAmount = 0;
			if (DoesCountConditionPass(historyMultiplier, effect))
			{
				if (historyMultiplier > 0 || effect.HistoryScalingIncludesBase)
				{
					long scaled = (long)baseAmount * Math.Max(0, historyMultiplier);
					long total = effect.HistoryScalingIncludesBase ? (long)baseAmount + scaled : scaled;
					totalAmount = total >= int.MaxValue ? int.MaxValue : total <= 0 ? 0 : (int)total;
				}
			}

			int comparison = forceUpgradeHighlight ? 1 : totalAmount.CompareTo(baseAmount);
			string totalAmountText = StsTextUtilities.HighlightChangeText(totalAmount.ToString(CultureInfo.InvariantCulture), comparison);

			string? baseCoreLine = FormatLineForAmount(totalAmount, totalAmountText, totalAmount, allowDamageBlockPreview: false, forceNumericEnergyStars: true);
			string definitionLine = ApplyHistoryScalingSuffix(card, definitionCoreLine, effect);

			line = string.IsNullOrWhiteSpace(baseCoreLine) ? definitionLine : baseCoreLine + "\n" + definitionLine;
		}
		else
		{
			line = FormatLineForAmount(baseAmount, amountText, grammarAmount, allowDamageBlockPreview: true, forceNumericEnergyStars: false);
		}

		if (string.IsNullOrWhiteSpace(line))
		{
			return null;
		}

		string scaledLine = line!;
		if (!usesHistoryScalingWording && !usesTwoLineHistoryScalingPreview && effect.ScaleMode != CardExtraEffectScaleMode.None)
		{
			scaledLine = effect.ScaleMode == CardExtraEffectScaleMode.PerHistoryCount && SupportsHistoryScaling(effect.Kind)
				? ApplyHistoryScalingSuffix(card, scaledLine, effect)
				: ApplyHistoryConditionPrefix(card, scaledLine.TrimEnd().TrimEnd('.'), effect);
		}
		scaledLine = ApplyRepeatSuffix(scaledLine, effect);

		string rendered = effect.Kind is CardExtraEffectKind.CreatedCardsCostLess or CardExtraEffectKind.CreatedCardsUpgraded
				or CardExtraEffectKind.GeneratedCardsUpgraded or CardExtraEffectKind.CardsInPileUpgradedAura
				or CardExtraEffectKind.DrawnCardsCostLess or CardExtraEffectKind.GeneratedCardsCostLess
				or CardExtraEffectKind.AutoPlaySelfFromPile
				or CardExtraEffectKind.ConditionalAutoPlayFromPile
				or CardExtraEffectKind.ConditionalAutoDrawFromPile
			? scaledLine
			: IsPowerEffect(effect)
				? scaledLine
				: ApplyTiming(scaledLine, effect.Timing, effect.Turns);

		if (effect.GrantToCard && effect.Kind != CardExtraEffectKind.EnchantCard)
		{
			rendered = WrapGrantToCard(rendered, effect);
		}

		if (IsPowerEffect(effect))
		{
			rendered = ApplyPowerTriggerPrefix(rendered, effect);
		}
		else if (effect.Trigger == CardExtraEffectTrigger.TurnBoundary)
		{
			rendered = FormatTurnBoundaryTrigger(effect, LowercaseFirst(rendered));
		}
		else if (effect.Trigger != CardExtraEffectTrigger.OnPlay
			|| (effect.Kind == CardExtraEffectKind.CardCostsLess && IsTriggeredCardCostsLessDefinition(effect))
			|| effect.Kind is CardExtraEffectKind.CardTypeCostsLess or CardExtraEffectKind.CardTypeStarCostsLess
				or CardExtraEffectKind.DrawnCardsCostLess or CardExtraEffectKind.GeneratedCardsCostLess)
		{
			rendered = ApplyTriggerPrefix(rendered, effect.Trigger);
		}

		return rendered;
	}

	private static void TryGetHistoryScalingTotalAmountText(CardModel card, CardExtraEffect effect, int baseAmount, Creature? target, bool forceUpgradeHighlight, out string amountText)
	{
		amountText = baseAmount.ToString(CultureInfo.InvariantCulture);

		try
		{
			Player? owner = card.Owner;
			Creature? dealer = owner?.Creature;
			CombatState? combatState = card.CombatState;

			bool isOstyAttack = effect.Kind == CardExtraEffectKind.OstyAction
				&& effect.OstyAction is CardExtraEffectOstyAction.Attack or CardExtraEffectOstyAction.AttackAll;

			ValueProp props = ValueProp.Move;

			decimal preview = baseAmount;
			if (ShouldRunGlobalHooks(card) && dealer != null && combatState != null && owner?.RunState != null)
			{
				if (effect.Kind == CardExtraEffectKind.DealDamage || isOstyAttack)
				{
					Creature? effectiveDealer = isOstyAttack ? owner.Osty : dealer;
					Creature? damageTarget = effect.Target switch
					{
						CardExtraEffectTarget.Self => isOstyAttack ? owner.Osty : dealer,
						CardExtraEffectTarget.Target => target,
						_ => null
					};
					CardPreviewMode previewMode = (effect.Target == CardExtraEffectTarget.AllEnemies || effect.OstyAction == CardExtraEffectOstyAction.AttackAll)
						? CardPreviewMode.MultiCreatureTargeting
						: CardPreviewMode.Normal;

					if (effectiveDealer != null)
					{
						preview = Hook.ModifyDamage(owner.RunState, combatState, damageTarget, effectiveDealer, baseAmount, props, card, ModifyDamageHookType.All, previewMode, out IEnumerable<AbstractModel> _);
					}
				}
				else if (effect.Kind == CardExtraEffectKind.GainBlock)
				{
					Creature? blockTarget = effect.Target switch
					{
						CardExtraEffectTarget.Self => dealer,
						CardExtraEffectTarget.Target => target,
						_ => null
					};

					if (blockTarget != null)
					{
						preview = Hook.ModifyBlock(combatState, blockTarget, baseAmount, props, card, cardPlay: null, out IEnumerable<AbstractModel> _);
					}
				}
			}

			int previewInt = (int)preview;
			int comparison = forceUpgradeHighlight ? 1 : previewInt.CompareTo(0);
			amountText = StsTextUtilities.HighlightChangeText(previewInt.ToString(CultureInfo.InvariantCulture), comparison);
		}
		catch
		{
			amountText = baseAmount.ToString(CultureInfo.InvariantCulture);
		}
	}

	private static string ApplyPowerTriggerPrefix(string line, CardExtraEffect effect)
	{
		string payload = LowercaseFirst(line);

		bool allowDelay = effect != null
			&& effect.Timing != CardExtraEffectTiming.Immediate
			&& effect.Trigger is CardExtraEffectTrigger.OnPlay
				or CardExtraEffectTrigger.OnDraw
				or CardExtraEffectTrigger.OnDiscard
				or CardExtraEffectTrigger.OnExhaust
				or CardExtraEffectTrigger.OnCountEvent
				or CardExtraEffectTrigger.Fatal
				or CardExtraEffectTrigger.OstyDealDamage
				or CardExtraEffectTrigger.AfterCombat
				or CardExtraEffectTrigger.OnChannel
				or CardExtraEffectTrigger.OnEvoke;
		if (allowDelay)
		{
			payload = ApplyTiming(payload, effect.Timing, effect.Turns);
		}
		string when = BuildPowerTriggerWhenClause(effect);

		string result = effect.Trigger switch
		{
			CardExtraEffectTrigger.OnPlay => CardEditorLoc.F("cardText.powerTrigger.prefix", $"{when}, {payload}", ("When", when), ("Payload", payload)),
			CardExtraEffectTrigger.OnDraw => CardEditorLoc.F("cardText.powerTrigger.prefix", $"{when}, {payload}", ("When", when), ("Payload", payload)),
			CardExtraEffectTrigger.OnDiscard => CardEditorLoc.F("cardText.powerTrigger.prefix", $"{when}, {payload}", ("When", when), ("Payload", payload)),
			CardExtraEffectTrigger.OnExhaust => CardEditorLoc.F("cardText.powerTrigger.prefix", $"{when}, {payload}", ("When", when), ("Payload", payload)),
			CardExtraEffectTrigger.OnCountEvent => CardEditorLoc.F("cardText.powerTrigger.prefix", $"{when}, {payload}", ("When", when), ("Payload", payload)),
			CardExtraEffectTrigger.Fatal => CardEditorLoc.F("cardText.powerTrigger.prefix", $"{when}, {payload}", ("When", when), ("Payload", payload)),
			CardExtraEffectTrigger.OstyDealDamage => CardEditorLoc.F("cardText.powerTrigger.prefix", $"{when}, {payload}", ("When", when), ("Payload", payload)),
			CardExtraEffectTrigger.AfterCombat => CardEditorLoc.F("cardText.powerTrigger.prefix", $"{when}, {payload}", ("When", when), ("Payload", payload)),
			CardExtraEffectTrigger.OnChannel => CardEditorLoc.F("cardText.powerTrigger.prefix", $"{when}, {payload}", ("When", when), ("Payload", payload)),
			CardExtraEffectTrigger.OnEvoke => CardEditorLoc.F("cardText.powerTrigger.prefix", $"{when}, {payload}", ("When", when), ("Payload", payload)),
			CardExtraEffectTrigger.EndOfTurnInHand => CardEditorLoc.F("cardText.powerTrigger.endOfTurn", $"At the end of your turn, {payload}", ("Payload", payload)),
			CardExtraEffectTrigger.StartOfTurn => CardEditorLoc.F("cardText.powerTrigger.startOfTurn", $"At the start of your turn, {payload}", ("Payload", payload)),
			CardExtraEffectTrigger.EndOfTurn => CardEditorLoc.F("cardText.powerTrigger.endOfTurn", $"At the end of your turn, {payload}", ("Payload", payload)),
			CardExtraEffectTrigger.StartOfEnemyTurn => CardEditorLoc.F("cardText.powerTrigger.startOfEnemyTurn", $"At the start of the enemy turn, {payload}", ("Payload", payload)),
			CardExtraEffectTrigger.EndOfEnemyTurn => CardEditorLoc.F("cardText.powerTrigger.endOfEnemyTurn", $"At the end of the enemy turn, {payload}", ("Payload", payload)),
			CardExtraEffectTrigger.TurnBoundary => FormatTurnBoundaryTrigger(effect, payload),
			_ => payload
		};

		// For non-stat-buff kinds with Duration=ThisTurn in power mode, the power entry expires at end of turn.
		if (!SupportsDuration(effect.Kind) && effect.Duration == CardExtraEffectDuration.ThisTurn)
		{
			result = CardEditorLoc.F("cardText.powerTrigger.thisTurnScope", $"This turn, {LowercaseFirst(result)}", ("Payload", result));
		}

		// Wrap with "For the next N turns/times" when a max-fire limit is set.
		if (effect.TriggerMaxFires >= 1)
		{
			string nStr = effect.TriggerMaxFires.ToString(CultureInfo.InvariantCulture);
			bool isTurnBased = effect.Trigger is CardExtraEffectTrigger.StartOfTurn
				or CardExtraEffectTrigger.EndOfTurn
				or CardExtraEffectTrigger.EndOfTurnInHand
				or CardExtraEffectTrigger.StartOfEnemyTurn
				or CardExtraEffectTrigger.EndOfEnemyTurn
				or CardExtraEffectTrigger.TurnBoundary;
			if (isTurnBased)
			{
				result = CardEditorLoc.F("cardText.powerTrigger.forNTurns", $"For the next {nStr} turns, {LowercaseFirst(result)}", ("N", effect.TriggerMaxFires), ("Payload", result));
			}
			else
			{
				result = CardEditorLoc.F("cardText.powerTrigger.forNTimes", $"The next {nStr} times, {LowercaseFirst(result)}", ("N", effect.TriggerMaxFires), ("Payload", result));
			}
		}

		// Wrap with "For the next N turns" when a turn duration limit is set (separate from fire limit).
		if (effect.TriggerMaxTurns >= 1)
		{
			string nStr = effect.TriggerMaxTurns.ToString(CultureInfo.InvariantCulture);
			result = CardEditorLoc.F("cardText.powerTrigger.forNTurnsDuration", $"For the next {nStr} turns, {LowercaseFirst(result)}", ("N", effect.TriggerMaxTurns), ("Payload", result));
		}

		return result;
	}

	private static string FormatTurnBoundaryTrigger(CardExtraEffect effect, string payload)
	{
		string edgeText = effect.TurnBoundary == CardExtraEffectTurnBoundary.Start ? "start" : "end";
		string sideText = effect.TurnBoundarySide switch
		{
			CardExtraEffectTurnBoundarySide.EnemyTurn => "the enemy turn",
			CardExtraEffectTurnBoundarySide.Both => "each turn",
			_ => "your turn"
		};

		string locationText = effect.TurnBoundaryCardLocation switch
		{
			CardExtraEffectTurnBoundaryCardLocation.Hand => " while this card is in your hand",
			CardExtraEffectTurnBoundaryCardLocation.DrawPile => " while this card is in your draw pile",
			CardExtraEffectTurnBoundaryCardLocation.DiscardPile => " while this card is in your discard pile",
			CardExtraEffectTurnBoundaryCardLocation.ExhaustPile => " while this card is in your exhaust pile",
			_ => string.Empty
		};

		string prefix = $"At the {edgeText} of {sideText}{locationText}, {payload}";
		return CardEditorLoc.F("cardText.powerTrigger.turnBoundary", prefix,
			("Edge", edgeText),
			("Side", sideText),
			("Payload", payload));
	}

	private static string BuildPowerTriggerWhenClause(CardExtraEffect effect)
	{
		string descriptor = BuildPowerTriggerCardDescriptor(effect);
		int everyN = effect.TriggerEveryN;

		if (everyN >= 2)
		{
			string nStr = everyN.ToString(CultureInfo.InvariantCulture);
			return effect.Trigger switch
			{
				CardExtraEffectTrigger.OnPlay => string.IsNullOrEmpty(descriptor)
					? CardEditorLoc.F("cardText.powerTrigger.everyNPlayAny", $"Every {nStr} cards you play", ("N", everyN))
					: CardEditorLoc.F("cardText.powerTrigger.everyNPlay", $"Every {nStr} {descriptor} cards you play", ("N", everyN), ("Descriptor", descriptor)),
				CardExtraEffectTrigger.OnDraw => string.IsNullOrEmpty(descriptor)
					? CardEditorLoc.F("cardText.powerTrigger.everyNDrawAny", $"Every {nStr} cards you draw", ("N", everyN))
					: CardEditorLoc.F("cardText.powerTrigger.everyNDraw", $"Every {nStr} {descriptor} cards you draw", ("N", everyN), ("Descriptor", descriptor)),
				CardExtraEffectTrigger.OnDiscard => string.IsNullOrEmpty(descriptor)
					? CardEditorLoc.F("cardText.powerTrigger.everyNDiscardAny", $"Every {nStr} cards you discard", ("N", everyN))
					: CardEditorLoc.F("cardText.powerTrigger.everyNDiscard", $"Every {nStr} {descriptor} cards you discard", ("N", everyN), ("Descriptor", descriptor)),
				CardExtraEffectTrigger.OnExhaust => string.IsNullOrEmpty(descriptor)
					? CardEditorLoc.F("cardText.powerTrigger.everyNExhaustAny", $"Every {nStr} cards you exhaust", ("N", everyN))
					: CardEditorLoc.F("cardText.powerTrigger.everyNExhaust", $"Every {nStr} {descriptor} cards you exhaust", ("N", everyN), ("Descriptor", descriptor)),
				CardExtraEffectTrigger.Fatal => CardEditorLoc.F("cardText.powerTrigger.everyNFatalAny", $"Every {nStr} killing blows you land", ("N", everyN)),
				CardExtraEffectTrigger.OstyDealDamage => CardEditorLoc.F("cardText.powerTrigger.everyNOstyDealDamage", $"Every {nStr} times Osty attacks", ("N", everyN)),
				CardExtraEffectTrigger.AfterCombat => CardEditorLoc.F("cardText.powerTrigger.everyNAfterCombat", $"After every {nStr} combats you win", ("N", everyN)),
				CardExtraEffectTrigger.OnChannel => CardEditorLoc.F("cardText.powerTrigger.everyNChannel", $"Every {nStr} orbs you channel", ("N", everyN)),
				CardExtraEffectTrigger.OnEvoke => CardEditorLoc.F("cardText.powerTrigger.everyNEvoke", $"Every {nStr} orbs you evoke", ("N", everyN)),
				CardExtraEffectTrigger.OnCountEvent => CardEditorLoc.F(
					"cardText.powerTrigger.everyNCountEvent",
					$"Every {nStr} times you {BuildPowerCountEventVerbPhrase(effect)}",
					("N", everyN),
					("Event", BuildPowerCountEventVerbPhrase(effect))),
				_ => CardEditorLoc.T("cardText.powerTrigger.whenPlayAny", "Whenever you play a card")
			};
		}

		return effect.Trigger switch
		{
			CardExtraEffectTrigger.OnPlay => string.IsNullOrEmpty(descriptor)
				? CardEditorLoc.T("cardText.powerTrigger.whenPlayAny", "Whenever you play a card")
				: CardEditorLoc.F("cardText.powerTrigger.whenPlay", $"Whenever you play {descriptor}", ("Descriptor", descriptor)),
			CardExtraEffectTrigger.OnDraw => string.IsNullOrEmpty(descriptor)
				? CardEditorLoc.T("cardText.powerTrigger.whenDrawAny", "Whenever you draw a card")
				: CardEditorLoc.F("cardText.powerTrigger.whenDraw", $"Whenever you draw {descriptor}", ("Descriptor", descriptor)),
			CardExtraEffectTrigger.OnDiscard => string.IsNullOrEmpty(descriptor)
				? CardEditorLoc.T("cardText.powerTrigger.whenDiscardAny", "Whenever you discard a card")
				: CardEditorLoc.F("cardText.powerTrigger.whenDiscard", $"Whenever you discard {descriptor}", ("Descriptor", descriptor)),
			CardExtraEffectTrigger.OnExhaust => string.IsNullOrEmpty(descriptor)
				? CardEditorLoc.T("cardText.powerTrigger.whenExhaustAny", "Whenever you exhaust a card")
				: CardEditorLoc.F("cardText.powerTrigger.whenExhaust", $"Whenever you exhaust {descriptor}", ("Descriptor", descriptor)),
			CardExtraEffectTrigger.Fatal => CardEditorLoc.T("cardText.powerTrigger.whenFatalAny", "Whenever this card lands a killing blow"),
			CardExtraEffectTrigger.OstyDealDamage => CardEditorLoc.T("cardText.powerTrigger.whenOstyDealDamage", "Whenever Osty attacks"),
			CardExtraEffectTrigger.AfterCombat => CardEditorLoc.T("cardText.powerTrigger.whenAfterCombat", "After combat victory"),
			CardExtraEffectTrigger.OnChannel => CardEditorLoc.T("cardText.powerTrigger.whenChannel", "Whenever you channel an orb"),
			CardExtraEffectTrigger.OnEvoke => CardEditorLoc.T("cardText.powerTrigger.whenEvoke", "Whenever you evoke an orb"),
			CardExtraEffectTrigger.OnCountEvent => CardEditorLoc.F(
				"cardText.powerTrigger.whenCountEvent",
				$"Whenever you {BuildPowerCountEventVerbPhrase(effect)}",
				("Event", BuildPowerCountEventVerbPhrase(effect))),
			_ => CardEditorLoc.T("cardText.powerTrigger.whenPlayAny", "Whenever you play a card")
		};
	}

	private static string BuildPowerCountEventVerbPhrase(CardExtraEffect effect)
	{
		CardExtraEffectCountEvent ev = effect?.PowerTriggerCountEvent ?? CardExtraEffectCountEvent.BlockLost;
		if (!TryGetAmountCountText(card: null, ev, out string singularResource, out _, out string presentVerb, out _, out _))
		{
			return ev switch
			{
				CardExtraEffectCountEvent.Summoned => "summon",
				_ => "trigger"
			};
		}

		return $"{presentVerb} {singularResource}";
	}

	private static string BuildPowerTriggerCardDescriptor(CardExtraEffect effect)
	{
		if (effect == null)
		{
			return string.Empty;
		}

		CardGeneratedCardPool pool = effect.TriggerCardPool;
		CardGeneratedCardType type = effect.TriggerCardType;
		CardExtraEffectCountCardFilter filter = effect.TriggerCardFilter;

		bool poolAny = pool is CardGeneratedCardPool.Any or CardGeneratedCardPool.All;
		bool typeAny = type == CardGeneratedCardType.Any;
		bool filterAny = filter == CardExtraEffectCountCardFilter.Any;

		if (poolAny && typeAny && filterAny)
		{
			return string.Empty;
		}

		string poolText = pool switch
		{
			CardGeneratedCardPool.Default => CardEditorLoc.T("cardText.poolDescriptor.yourColor", "of your color"),
			CardGeneratedCardPool.OtherColors => CardEditorLoc.T("cardText.poolDescriptor.otherColors", "from other characters"),
			CardGeneratedCardPool.Any => CardEditorLoc.T("cardText.poolDescriptor.allColors", "from any character"),
			CardGeneratedCardPool.All => CardEditorLoc.T("cardText.poolDescriptor.allPools", "from all pools"),
			CardGeneratedCardPool.Colorless => GeneratedCardPoolLabel(CardGeneratedCardPool.Colorless),
			CardGeneratedCardPool.Ancient => GeneratedCardPoolLabel(CardGeneratedCardPool.Ancient),
			CardGeneratedCardPool.Ironclad => GeneratedCardPoolLabel(CardGeneratedCardPool.Ironclad),
			CardGeneratedCardPool.Silent => GeneratedCardPoolLabel(CardGeneratedCardPool.Silent),
			CardGeneratedCardPool.Defect => GeneratedCardPoolLabel(CardGeneratedCardPool.Defect),
			CardGeneratedCardPool.Regent => GeneratedCardPoolLabel(CardGeneratedCardPool.Regent),
			CardGeneratedCardPool.Necrobinder => GeneratedCardPoolLabel(CardGeneratedCardPool.Necrobinder),
			_ => string.Empty
		};

		string typeText = type switch
		{
			CardGeneratedCardType.Attack => GeneratedCardTypeLabel(CardGeneratedCardType.Attack),
			CardGeneratedCardType.Skill => GeneratedCardTypeLabel(CardGeneratedCardType.Skill),
			CardGeneratedCardType.Power => GeneratedCardTypeLabel(CardGeneratedCardType.Power),
			CardGeneratedCardType.Playable => GeneratedCardTypeLabel(CardGeneratedCardType.Playable),
			CardGeneratedCardType.Status => GeneratedCardTypeLabel(CardGeneratedCardType.Status),
			CardGeneratedCardType.Curse => GeneratedCardTypeLabel(CardGeneratedCardType.Curse),
			CardGeneratedCardType.Quest => GeneratedCardTypeLabel(CardGeneratedCardType.Quest),
			_ => string.Empty
		};

		string filterText = filter switch
		{
			CardExtraEffectCountCardFilter.Any => string.Empty,
			_ => CountCardFilterLabel(filter)
		};

		string cardWord = CardEditorLoc.T("cardText.word.card", "card");
		bool poolIsPrefix = pool is CardGeneratedCardPool.Colorless or CardGeneratedCardPool.Ancient
			or CardGeneratedCardPool.Ironclad or CardGeneratedCardPool.Silent or CardGeneratedCardPool.Defect
			or CardGeneratedCardPool.Regent or CardGeneratedCardPool.Necrobinder;

		string? keywordSuffix = filter switch
		{
			CardExtraEffectCountCardFilter.Exhaust => "Exhaust",
			CardExtraEffectCountCardFilter.Ethereal => "Ethereal",
			CardExtraEffectCountCardFilter.Innate => "Innate",
			CardExtraEffectCountCardFilter.Retain => "Retain",
			CardExtraEffectCountCardFilter.Sly => "Sly",
			CardExtraEffectCountCardFilter.Eternal => "Eternal",
			_ => null
		};

		string prefix;
		if (!string.IsNullOrWhiteSpace(keywordSuffix))
		{
			string cardPhrase = string.Join(" ", new[] { typeText, cardWord }.Where(s => !string.IsNullOrWhiteSpace(s)));
			if (!string.IsNullOrWhiteSpace(poolText))
			{
				cardPhrase = poolIsPrefix
					? string.Join(" ", new[] { poolText, cardPhrase }.Where(s => !string.IsNullOrWhiteSpace(s)))
					: $"{cardPhrase} {poolText}".Trim();
			}
			prefix = string.Join(" ", new[] { cardPhrase, "with", keywordSuffix }.Where(s => !string.IsNullOrWhiteSpace(s)));
		}
		else
		{
			prefix = string.Join(" ", new[] { filterText, typeText, cardWord }.Where(s => !string.IsNullOrWhiteSpace(s)));
			if (!string.IsNullOrWhiteSpace(poolText))
			{
				prefix = poolIsPrefix
					? string.Join(" ", new[] { poolText, prefix }.Where(s => !string.IsNullOrWhiteSpace(s)))
					: $"{prefix} {poolText}".Trim();
			}
		}

		if (string.IsNullOrWhiteSpace(prefix))
		{
			return string.Empty;
		}

		string trimmedPrefix = prefix.TrimStart();
		bool useAn = trimmedPrefix.Length > 0 && "aeiou".Contains(char.ToLowerInvariant(trimmedPrefix[0]));
		return useAn
			? CardEditorLoc.F("cardText.descriptor.anCard", "an {Prefix}", ("Prefix", prefix))
			: CardEditorLoc.F("cardText.descriptor.aCard", "a {Prefix}", ("Prefix", prefix));
	}

	private static string LowercaseFirst(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}
		char first = text[0];
		if (!char.IsUpper(first))
		{
			return text;
		}
		return char.ToLowerInvariant(first) + text.Substring(1);
	}

	private static string ApplyHistoryScalingSuffix(CardModel? card, string line, CardExtraEffect effect)
	{
		if (string.IsNullOrWhiteSpace(line) || effect == null)
		{
			return line;
		}

		string trimmed = line.TrimEnd();
		if (trimmed.EndsWith(".", StringComparison.Ordinal))
		{
			trimmed = trimmed.Substring(0, trimmed.Length - 1);
		}

		if (effect.Kind is CardExtraEffectKind.IgnoreBlock
			or CardExtraEffectKind.IgnoreDamageModifiers
			or CardExtraEffectKind.IgnoreDamageCaps
			or CardExtraEffectKind.IgnoreDamageNegation
			or CardExtraEffectKind.IgnoreEnemyDamageReductions)
		{
			return ApplyHistoryConditionPrefix(card, trimmed, effect);
		}

		if (effect.CountEvent == CardExtraEffectCountEvent.CurrentOrbs)
		{
			string orbDescriptor = GetOrbCountDescriptor(effect.CountOrbType, plural: true);
			return CardEditorLoc.F(
				"cardText.orbScalingSuffix",
				$"{trimmed} for each {orbDescriptor} you have.",
				("Effect", trimmed),
				("OrbDescriptor", orbDescriptor));
		}

		if (effect.CountEvent == CardExtraEffectCountEvent.EmptyOrbSlots)
		{
			return CardEditorLoc.F(
				"cardText.emptyOrbSlotScalingSuffix",
				$"{trimmed} for each empty orb slot you have.",
				("Effect", trimmed));
		}

		if (effect.CountEvent == CardExtraEffectCountEvent.OrbInPosition)
		{
			return ApplyHistoryConditionPrefix(card, trimmed, effect);
		}

		if (effect.CountEvent == CardExtraEffectCountEvent.EnemyHasStatus)
		{
			string status = EnemyStatusLabel(effect.CountEnemyStatus);
			return CardEditorLoc.F(
				"cardText.enemyStatusScalingSuffix",
				$"{trimmed} for each enemy with {status}.",
				("Effect", trimmed),
				("Status", status));
		}

		if (effect.CountEvent == CardExtraEffectCountEvent.EnemyIntent)
		{
			string intent = LowercaseFirst(EnemyIntentLabel(effect.CountEnemyIntent));
			return CardEditorLoc.F(
				"cardText.enemyIntentScalingSuffix",
				$"{trimmed} for each enemy intending to {intent}.",
				("Effect", trimmed),
				("Intent", intent));
		}

		if (effect.CountEvent == CardExtraEffectCountEvent.PlayedCardEnergyCost)
		{
			string energyIcon = BuildEnergyIcon(card);
			return CardEditorLoc.F(
				"cardText.playedCardCostScalingSuffix",
				$"{trimmed} for each {energyIcon} in the played card's cost.",
				("Effect", trimmed),
				("Energy", energyIcon));
		}

		if (TryGetStatusCountText(effect.CountEvent, effect.CountEnemyStatus, out string statusText, out _, out string pastStatusVerb, out _))
		{
			string statusWindowText = GetCountWindowText(effect);
			return CardEditorLoc.F(
				"cardText.statusScalingSuffix",
				$"{trimmed} for each {statusText} {pastStatusVerb} {statusWindowText}.",
				("Effect", trimmed),
				("Status", statusText),
				("Verb", pastStatusVerb),
				("Window", statusWindowText));
		}

		if (effect.CountEvent == CardExtraEffectCountEvent.Summoned)
		{
			string summonWindowText = GetCountWindowText(effect);
			return CardEditorLoc.F(
				"cardText.summonScalingSuffix",
				$"{trimmed} for each summon you made {summonWindowText}.",
				("Effect", trimmed),
				("Window", summonWindowText));
		}

		if (TryGetAmountCountText(card, effect.CountEvent, out string singularResource, out _, out _, out string pastResourceVerb, out _))
		{
			string resourceWindowText = GetCountWindowText(effect);
			return CardEditorLoc.F(
				"cardText.resourceScalingSuffix",
				$"{trimmed} for each {singularResource} {pastResourceVerb} {resourceWindowText}.",
				("Effect", trimmed),
				("Resource", singularResource),
				("Verb", pastResourceVerb),
				("Window", resourceWindowText));
		}

		if (effect.CountEvent is CardExtraEffectCountEvent.OrbChanneled or CardExtraEffectCountEvent.OrbEvoked)
		{
			string orbDescriptor = GetOrbCountDescriptor(effect.CountOrbType, plural: true);
			string orbVerb = effect.CountEvent == CardExtraEffectCountEvent.OrbChanneled
				? CardEditorLoc.Enum("historyVerbPast", effect.CountEvent, "channeled")
				: CardEditorLoc.Enum("historyVerbPast", effect.CountEvent, "evoked");
			string orbWindowText = GetCountWindowText(effect);
			return CardEditorLoc.F(
				"cardText.orbHistoryScalingSuffix",
				$"{trimmed} for each {orbDescriptor} you {orbVerb} {orbWindowText}.",
				("Effect", trimmed),
				("OrbDescriptor", orbDescriptor),
				("Verb", orbVerb),
				("Window", orbWindowText));
		}

		string filter = BuildCountCardFilter(effect);
		string poolSuffix = BuildCountScalingPoolSuffix(effect.CountCardPool);

		string prefix = string.IsNullOrEmpty(filter) ? string.Empty : filter;
		if (effect.CountEvent == CardExtraEffectCountEvent.InPile)
		{
			string where = GetCardPileLocation(effect.CountCardPile);
			return CardEditorLoc.F(
				"cardText.pileScalingSuffix",
				$"{trimmed} for each {prefix}card{poolSuffix} {where}.",
				("Effect", trimmed),
				("Prefix", prefix),
				("PoolSuffix", poolSuffix),
				("Where", where));
		}

		string verbFallback = effect.CountEvent switch
		{
			CardExtraEffectCountEvent.Drawn => "drew",
			CardExtraEffectCountEvent.Discarded => "discarded",
			CardExtraEffectCountEvent.Exhausted => "exhausted",
			CardExtraEffectCountEvent.Generated => "created",
			_ => "played"
		};
		string verb = CardEditorLoc.Enum("historyVerbPast", effect.CountEvent, verbFallback);

		string windowText = GetCountWindowText(effect);
		return CardEditorLoc.F(
			"cardText.historyScalingSuffix",
			$"{trimmed} for each {prefix}card{poolSuffix} you {verb} {windowText}.",
			("Effect", trimmed),
			("Prefix", prefix),
			("PoolSuffix", poolSuffix),
			("Verb", verb),
			("Window", windowText));
	}

	private static string ApplyHistoryConditionPrefix(CardModel? card, string trimmedLineWithoutPeriod, CardExtraEffect effect)
	{
		if (effect == null)
		{
			return trimmedLineWithoutPeriod;
		}

		string clause = BuildCountConditionClause(card, effect);
		if (!string.IsNullOrWhiteSpace(clause))
		{
			return CardEditorLoc.F(
				"cardText.genericConditionPrefix",
				$"If {clause}, {trimmedLineWithoutPeriod}.",
				("Clause", clause),
				("Effect", trimmedLineWithoutPeriod));
		}

		if (effect.CountEvent == CardExtraEffectCountEvent.InPile)
		{
			string pileFilter = BuildCountCardFilter(effect);
			string pilePoolSuffix = BuildCountScalingPoolSuffix(effect.CountCardPool);
			string pilePrefix = string.IsNullOrEmpty(pileFilter) ? string.Empty : pileFilter;
			string where = GetCardPileLocation(effect.CountCardPile);
			bool explicitComparison = effect.CountComparison != CardExtraEffectCountComparison.None;
			int threshold = Math.Max(0, effect.CountConditionAmount);
			if (explicitComparison)
			{
				return CardEditorLoc.F(
					"cardText.pileConditionCountPrefix",
					$"If you have {FormatComparisonPhrase(effect.CountComparison, threshold)} {pilePrefix}card{(threshold == 1 ? string.Empty : "s")}{pilePoolSuffix} {where}, {trimmedLineWithoutPeriod}.",
					("Comparison", FormatComparisonPhrase(effect.CountComparison, threshold)),
					("Prefix", pilePrefix),
					("Plural", threshold == 1 ? string.Empty : "s"),
					("PoolSuffix", pilePoolSuffix),
					("Where", where),
					("Effect", trimmedLineWithoutPeriod));
			}

			return CardEditorLoc.F(
				"cardText.pileConditionPrefix",
				$"If you have a {pilePrefix}card{pilePoolSuffix} {where}, {trimmedLineWithoutPeriod}.",
				("Prefix", pilePrefix),
				("PoolSuffix", pilePoolSuffix),
				("Where", where),
				("Effect", trimmedLineWithoutPeriod));
		}

		bool explicitHistoryComparison = effect.CountComparison != CardExtraEffectCountComparison.None;
		int historyThreshold = Math.Max(0, effect.CountConditionAmount);
		string verbFallback = effect.CountEvent switch
		{
			CardExtraEffectCountEvent.Drawn => "draw",
			CardExtraEffectCountEvent.Discarded => "discard",
			CardExtraEffectCountEvent.Exhausted => "exhaust",
			CardExtraEffectCountEvent.Generated => "create",
			_ => "play"
		};
		string verb = CardEditorLoc.Enum("historyVerbPresent", effect.CountEvent, verbFallback);

		string windowText = GetCountWindowText(effect);

		string filter = BuildCountCardFilter(effect);
		string poolSuffix = BuildCountScalingPoolSuffix(effect.CountCardPool);
		string prefix = string.IsNullOrEmpty(filter) ? string.Empty : filter;
		if (explicitHistoryComparison)
		{
			string pastVerbFallback = effect.CountEvent switch
			{
				CardExtraEffectCountEvent.Drawn => "drew",
				CardExtraEffectCountEvent.Discarded => "discarded",
				CardExtraEffectCountEvent.Exhausted => "exhausted",
				CardExtraEffectCountEvent.Generated => "created",
				_ => "played"
			};
			string pastVerb = CardEditorLoc.Enum("historyVerbPast", effect.CountEvent, pastVerbFallback);

			return CardEditorLoc.F(
				"cardText.historyConditionCountPrefix",
				$"If you've {pastVerb} {FormatComparisonPhrase(effect.CountComparison, historyThreshold)} {prefix}card{(historyThreshold == 1 ? string.Empty : "s")}{poolSuffix} {windowText}, {trimmedLineWithoutPeriod}.",
				("Verb", pastVerb),
				("Comparison", FormatComparisonPhrase(effect.CountComparison, historyThreshold)),
				("Prefix", prefix),
				("Plural", historyThreshold == 1 ? string.Empty : "s"),
				("PoolSuffix", poolSuffix),
				("Window", windowText),
				("Effect", trimmedLineWithoutPeriod));
		}

		return CardEditorLoc.F(
			"cardText.historyConditionPrefix",
			$"If you {verb} a {prefix}card{poolSuffix} {windowText}, {trimmedLineWithoutPeriod}.",
			("Verb", verb),
			("Prefix", prefix),
			("PoolSuffix", poolSuffix),
			("Window", windowText),
			("Effect", trimmedLineWithoutPeriod));
	}

	private static string BuildCountConditionClause(CardModel? card, CardExtraEffect effect)
	{
		if (effect == null)
		{
			return string.Empty;
		}

		bool explicitComparison = effect.CountComparison != CardExtraEffectCountComparison.None;
		int threshold = Math.Max(0, effect.CountConditionAmount);

		if (effect.CountEvent == CardExtraEffectCountEvent.CurrentOrbs)
		{
			if (!explicitComparison)
			{
				return effect.CountOrbType == CardExtraEffectOrbType.Any
					? CardEditorLoc.T("cardText.condition.anyOrb", "you have an orb")
					: CardEditorLoc.F("cardText.condition.haveOrbType", $"you have a {GetOrbCountDescriptor(effect.CountOrbType, plural: false)}", ("OrbDescriptor", GetOrbCountDescriptor(effect.CountOrbType, plural: false)));
			}

			return CardEditorLoc.F(
				"cardText.condition.haveOrbTypeCount",
				$"you have {FormatComparisonPhrase(effect.CountComparison, threshold)} {GetOrbCountDescriptor(effect.CountOrbType, plural: threshold != 1)}",
				("Comparison", FormatComparisonPhrase(effect.CountComparison, threshold)),
				("OrbDescriptor", GetOrbCountDescriptor(effect.CountOrbType, plural: threshold != 1)));
		}

		if (effect.CountEvent == CardExtraEffectCountEvent.EmptyOrbSlots)
		{
			if (explicitComparison && effect.CountComparison == CardExtraEffectCountComparison.Exactly && threshold == 0)
			{
				return CardEditorLoc.T("cardText.condition.orbSlotsFull", "your orb slots are full");
			}

			if (!explicitComparison)
			{
				return CardEditorLoc.T("cardText.condition.haveEmptyOrbSlot", "you have an empty orb slot");
			}

			return CardEditorLoc.F(
				"cardText.condition.emptyOrbSlotsCount",
				$"you have {FormatComparisonPhrase(effect.CountComparison, threshold)} empty orb slot{(threshold == 1 ? string.Empty : "s")}",
				("Comparison", FormatComparisonPhrase(effect.CountComparison, threshold)),
				("Plural", threshold == 1 ? string.Empty : "s"));
		}

		if (effect.CountEvent == CardExtraEffectCountEvent.OrbInPosition)
		{
			string selectionText = LowercaseFirst(OrbSelectionLabel(effect.CountOrbSelection));
			if (effect.CountOrbType == CardExtraEffectOrbType.Any)
			{
				return CardEditorLoc.F("cardText.condition.orbPositionAny", $"your {selectionText} orb slot is occupied", ("Selection", selectionText));
			}

			return CardEditorLoc.F(
				"cardText.condition.orbPositionType",
				$"your {selectionText} orb is {GetOrbCountDescriptor(effect.CountOrbType, plural: false)}",
				("Selection", selectionText),
				("OrbDescriptor", GetOrbCountDescriptor(effect.CountOrbType, plural: false)));
		}

		if (effect.CountEvent is CardExtraEffectCountEvent.OrbChanneled or CardExtraEffectCountEvent.OrbEvoked)
		{
			string verb = effect.CountEvent == CardExtraEffectCountEvent.OrbChanneled
				? CardEditorLoc.Enum("historyVerbPresent", effect.CountEvent, "channel")
				: CardEditorLoc.Enum("historyVerbPresent", effect.CountEvent, "evoke");
			string orbDescriptor = GetOrbCountDescriptor(effect.CountOrbType, plural: explicitComparison ? threshold != 1 : false);
			string windowText = GetCountWindowText(effect);
			if (!explicitComparison)
			{
				string article = effect.CountOrbType == CardExtraEffectOrbType.Any ? "an" : "a";
				return CardEditorLoc.F(
					"cardText.condition.orbHistorySingle",
					$"you {verb} {article} {orbDescriptor} {windowText}",
					("Verb", verb),
					("OrbDescriptor", orbDescriptor),
					("Window", windowText));
			}

			return CardEditorLoc.F(
				"cardText.condition.orbHistoryCount",
				$"you {verb} {FormatComparisonPhrase(effect.CountComparison, threshold)} {orbDescriptor} {windowText}",
				("Verb", verb),
				("Comparison", FormatComparisonPhrase(effect.CountComparison, threshold)),
				("OrbDescriptor", orbDescriptor),
				("Window", windowText));
		}

		if (effect.CountEvent == CardExtraEffectCountEvent.EnemyHasStatus)
		{
			string status = EnemyStatusLabel(effect.CountEnemyStatus);
			if (explicitComparison && effect.CountComparison == CardExtraEffectCountComparison.Exactly && threshold == 0)
			{
				return CardEditorLoc.F("cardText.condition.enemyStatusNone", $"no enemy has {status}", ("Status", status));
			}

			if (!explicitComparison)
			{
				return CardEditorLoc.F("cardText.condition.enemyHasStatus", $"an enemy has {status}", ("Status", status));
			}

			return CardEditorLoc.F(
				"cardText.condition.enemyHasStatusCount",
				$"there are {FormatComparisonPhrase(effect.CountComparison, threshold)} enemies with {status}",
				("Comparison", FormatComparisonPhrase(effect.CountComparison, threshold)),
				("Status", status));
		}

		if (effect.CountEvent == CardExtraEffectCountEvent.EnemyIntent)
		{
			string intent = LowercaseFirst(EnemyIntentLabel(effect.CountEnemyIntent));
			if (explicitComparison && effect.CountComparison == CardExtraEffectCountComparison.Exactly && threshold == 0)
			{
				return CardEditorLoc.F("cardText.condition.enemyIntentNone", $"no enemy intends to {intent}", ("Intent", intent));
			}

			if (!explicitComparison)
			{
				return CardEditorLoc.F("cardText.condition.enemyIntentAny", $"an enemy intends to {intent}", ("Intent", intent));
			}

			return CardEditorLoc.F(
				"cardText.condition.enemyIntentCount",
				$"there are {FormatComparisonPhrase(effect.CountComparison, threshold)} enemies intending to {intent}",
				("Comparison", FormatComparisonPhrase(effect.CountComparison, threshold)),
				("Intent", intent));
		}

		if (effect.CountEvent == CardExtraEffectCountEvent.PlayedCardEnergyCost)
		{
			string energyIcon = BuildEnergyIcon(card);
			if (!explicitComparison)
			{
				return CardEditorLoc.F("cardText.condition.playedCardCostsEnergy", $"the played card costs {energyIcon}", ("Energy", energyIcon));
			}

			return CardEditorLoc.F(
				"cardText.condition.playedCardCostCount",
				$"the played card costs {FormatComparisonPhrase(effect.CountComparison, threshold)} {energyIcon}",
				("Comparison", FormatComparisonPhrase(effect.CountComparison, threshold)),
				("Energy", energyIcon));
		}

		if (TryGetStatusCountText(effect.CountEvent, effect.CountEnemyStatus, out string statusText, out string presentStatusVerb, out _, out string perfectStatusVerb))
		{
			string statusWindowText = GetCountWindowText(effect);
			if (!explicitComparison)
			{
				return CardEditorLoc.F(
					"cardText.condition.statusHistorySingle",
					$"you {presentStatusVerb} {statusText} {statusWindowText}",
					("Verb", presentStatusVerb),
					("Status", statusText),
					("Window", statusWindowText));
			}

			return CardEditorLoc.F(
				"cardText.condition.statusHistoryCount",
				$"you've {perfectStatusVerb} {FormatComparisonPhrase(effect.CountComparison, threshold)} {statusText} {statusWindowText}",
				("Verb", perfectStatusVerb),
				("Comparison", FormatComparisonPhrase(effect.CountComparison, threshold)),
				("Status", statusText),
				("Window", statusWindowText));
		}

		if (effect.CountEvent == CardExtraEffectCountEvent.Summoned)
		{
			string summonWindowText = GetCountWindowText(effect);
			if (!explicitComparison)
			{
				return CardEditorLoc.F(
					"cardText.condition.summonHistorySingle",
					$"you made a summon {summonWindowText}",
					("Window", summonWindowText));
			}

			return CardEditorLoc.F(
				"cardText.condition.summonHistoryCount",
				$"you've made {FormatComparisonPhrase(effect.CountComparison, threshold)} summon{(threshold == 1 ? string.Empty : "s")} {summonWindowText}",
				("Comparison", FormatComparisonPhrase(effect.CountComparison, threshold)),
				("Plural", threshold == 1 ? string.Empty : "s"),
				("Window", summonWindowText));
		}

		if (TryGetAmountCountText(card, effect.CountEvent, out string singularResource, out string pluralResource, out string presentResourceVerb, out _, out string perfectResourceVerb))
		{
			string resourceWindowText = GetCountWindowText(effect);
			if (!explicitComparison)
			{
				return CardEditorLoc.F(
					"cardText.condition.resourceHistorySingle",
					$"you {presentResourceVerb} {singularResource} {resourceWindowText}",
					("Verb", presentResourceVerb),
					("Resource", singularResource),
					("Window", resourceWindowText));
			}

			string countedResourceText = threshold == 1 ? singularResource : pluralResource;
			return CardEditorLoc.F(
				"cardText.condition.resourceHistoryCount",
				$"you've {perfectResourceVerb} {FormatComparisonPhrase(effect.CountComparison, threshold)} {countedResourceText} {resourceWindowText}",
				("Verb", perfectResourceVerb),
				("Comparison", FormatComparisonPhrase(effect.CountComparison, threshold)),
				("Resource", countedResourceText),
				("Window", resourceWindowText));
		}

		return string.Empty;
	}

	private static string BuildEnergyIcon(CardModel? card)
	{
		string prefix = string.Empty;
		if (card != null)
		{
			prefix = EnergyIconHelper.GetPrefix(card);
		}

		if (string.IsNullOrEmpty(prefix) || prefix == "colorless")
		{
			prefix = RunManager.Instance?.GetLocalCharacterEnergyIconPrefix() ?? "colorless";
		}

		return "[img]res://images/packed/sprite_fonts/" + prefix + "_energy_icon.png[/img]";
	}

	private static string GetCountWindowText(CardExtraEffect effect)
	{
		int turns = Math.Max(1, effect.CountTurns);
		return effect.CountWindow switch
		{
			CardExtraEffectCountWindow.ThisTurn => CardEditorLoc.T("cardText.window.thisTurn", "this turn"),
			CardExtraEffectCountWindow.LastTurns => turns == 1
				? effect.CountWindowInclusion == CardExtraEffectCountWindowInclusion.ExcludeThisTurn
					? CardEditorLoc.T("cardText.window.lastTurn", "last turn")
					: CardEditorLoc.T("cardText.window.thisTurn", "this turn")
				: effect.CountWindowInclusion == CardExtraEffectCountWindowInclusion.ExcludeThisTurn
					? CardEditorLoc.F("cardText.window.previousTurns", $"in the previous {turns.ToString(CultureInfo.InvariantCulture)} turns", ("Turns", turns))
					: CardEditorLoc.F("cardText.window.lastTurnsIncludingThisTurn", $"in the last {turns.ToString(CultureInfo.InvariantCulture)} turns (including this turn)", ("Turns", turns)),
			_ => CardEditorLoc.T("cardText.window.thisCombat", "this combat")
		};
	}

	private static string GetOrbCountDescriptor(CardExtraEffectOrbType orbType, bool plural)
	{
		if (orbType == CardExtraEffectOrbType.Any)
		{
			return plural ? "orbs" : "orb";
		}

		string orbName = OrbTypeLabel(orbType);
		return plural ? $"{orbName} orbs" : $"{orbName} orb";
	}

	private static string FormatComparisonPhrase(CardExtraEffectCountComparison comparison, int amount)
	{
		string amountText = amount.ToString(CultureInfo.InvariantCulture);
		return comparison switch
		{
			CardExtraEffectCountComparison.AtLeast => CardEditorLoc.F("cardText.condition.atLeast", $"at least {amountText}", ("Amount", amountText)),
			CardExtraEffectCountComparison.AtMost => CardEditorLoc.F("cardText.condition.atMost", $"at most {amountText}", ("Amount", amountText)),
			CardExtraEffectCountComparison.Exactly => CardEditorLoc.F("cardText.condition.exactly", $"exactly {amountText}", ("Amount", amountText)),
			_ => amountText
		};
	}

	private static IEnumerable<Creature> GetRelevantEnemyConditionTargets(CombatState combatState, CardPlay? cardPlay)
	{
		if (cardPlay?.Target is Creature targetedEnemy && targetedEnemy.IsEnemy && targetedEnemy.IsAlive)
		{
			yield return targetedEnemy;
			yield break;
		}

		if (combatState?.Enemies == null)
		{
			yield break;
		}

		foreach (Creature enemy in combatState.Enemies)
		{
			if (enemy != null && enemy.IsEnemy && enemy.IsAlive)
			{
				yield return enemy;
			}
		}
	}

	private static bool PowerMatchesStatus(PowerModel power, CardExtraEffectEnemyStatus status)
	{
		if (power == null)
		{
			return false;
		}

		return status switch
		{
			CardExtraEffectEnemyStatus.Weak => power is WeakPower,
			CardExtraEffectEnemyStatus.Frail => power is FrailPower,
			CardExtraEffectEnemyStatus.Vulnerable => power is VulnerablePower,
			CardExtraEffectEnemyStatus.Poison => power is PoisonPower,
			CardExtraEffectEnemyStatus.Doom => power is DoomPower,
			CardExtraEffectEnemyStatus.Constrict => power is ConstrictPower,
			CardExtraEffectEnemyStatus.Artifact => power is ArtifactPower,
			CardExtraEffectEnemyStatus.Thorns => power is ThornsPower,
			CardExtraEffectEnemyStatus.Regen => power is RegenPower,
			CardExtraEffectEnemyStatus.Plating => power is PlatingPower,
			CardExtraEffectEnemyStatus.Intangible => power is IntangiblePower,
			CardExtraEffectEnemyStatus.Buffer => power is BufferPower,
			CardExtraEffectEnemyStatus.Vigor => power is VigorPower,
			CardExtraEffectEnemyStatus.Blur => power is BlurPower,
			CardExtraEffectEnemyStatus.Ritual => power is RitualPower,
			CardExtraEffectEnemyStatus.Strength => power is StrengthPower,
			CardExtraEffectEnemyStatus.Dexterity => power is DexterityPower,
			CardExtraEffectEnemyStatus.Focus => power is FocusPower,
			CardExtraEffectEnemyStatus.AnyPowerStatus => true,
			CardExtraEffectEnemyStatus.Buff => power.Type == PowerType.Buff,
			CardExtraEffectEnemyStatus.Debuff => power.Type == PowerType.Debuff,
			_ => false
		};
	}

	private static bool EnemyHasStatus(Creature enemy, CardExtraEffectEnemyStatus status)
	{
		if (enemy == null || !enemy.IsEnemy || !enemy.IsAlive)
		{
			return false;
		}

		if (status is CardExtraEffectEnemyStatus.AnyPowerStatus or CardExtraEffectEnemyStatus.Buff or CardExtraEffectEnemyStatus.Debuff)
		{
			return enemy.Powers.Any(power => power != null && power.Amount > 0 && PowerMatchesStatus(power, status));
		}

		return status switch
		{
			CardExtraEffectEnemyStatus.Weak => enemy.GetPowerAmount<WeakPower>() > 0,
			CardExtraEffectEnemyStatus.Frail => enemy.GetPowerAmount<FrailPower>() > 0,
			CardExtraEffectEnemyStatus.Vulnerable => enemy.GetPowerAmount<VulnerablePower>() > 0,
			CardExtraEffectEnemyStatus.Poison => enemy.GetPowerAmount<PoisonPower>() > 0,
			CardExtraEffectEnemyStatus.Doom => enemy.GetPowerAmount<DoomPower>() > 0,
			CardExtraEffectEnemyStatus.Constrict => enemy.GetPowerAmount<ConstrictPower>() > 0,
			CardExtraEffectEnemyStatus.Artifact => enemy.GetPowerAmount<ArtifactPower>() > 0,
			CardExtraEffectEnemyStatus.Thorns => enemy.GetPowerAmount<ThornsPower>() > 0,
			CardExtraEffectEnemyStatus.Regen => enemy.GetPowerAmount<RegenPower>() > 0,
			CardExtraEffectEnemyStatus.Plating => enemy.GetPowerAmount<PlatingPower>() > 0,
			CardExtraEffectEnemyStatus.Intangible => enemy.GetPowerAmount<IntangiblePower>() > 0,
			CardExtraEffectEnemyStatus.Buffer => enemy.GetPowerAmount<BufferPower>() > 0,
			CardExtraEffectEnemyStatus.Vigor => enemy.GetPowerAmount<VigorPower>() > 0,
			CardExtraEffectEnemyStatus.Blur => enemy.GetPowerAmount<BlurPower>() > 0,
			CardExtraEffectEnemyStatus.Ritual => enemy.GetPowerAmount<RitualPower>() > 0,
			CardExtraEffectEnemyStatus.Strength => enemy.GetPowerAmount<StrengthPower>() > 0,
			CardExtraEffectEnemyStatus.Dexterity => enemy.GetPowerAmount<DexterityPower>() > 0,
			CardExtraEffectEnemyStatus.Focus => enemy.GetPowerAmount<FocusPower>() > 0,
			_ => false
		};
	}

	private static bool EnemyHasIntent(Creature enemy, CardExtraEffectEnemyIntent desiredIntent)
	{
		if (enemy == null || !enemy.IsEnemy || !enemy.IsAlive)
		{
			return false;
		}

		if (desiredIntent == CardExtraEffectEnemyIntent.Stun && enemy.IsStunned)
		{
			return true;
		}

		IReadOnlyList<AbstractIntent>? intents = enemy.Monster?.NextMove?.Intents;
		if (intents == null)
		{
			return false;
		}

		return intents.Any(intent => intent != null && EnemyIntentMatches(intent.IntentType, desiredIntent));
	}

	private static bool EnemyIntentMatches(IntentType intentType, CardExtraEffectEnemyIntent desiredIntent)
	{
		return desiredIntent switch
		{
			CardExtraEffectEnemyIntent.Attack => intentType is IntentType.Attack or IntentType.DeathBlow,
			CardExtraEffectEnemyIntent.Defense => intentType == IntentType.Defend,
			CardExtraEffectEnemyIntent.Buff => intentType == IntentType.Buff,
			CardExtraEffectEnemyIntent.Debuff => intentType is IntentType.Debuff or IntentType.DebuffStrong or IntentType.CardDebuff or IntentType.StatusCard,
			CardExtraEffectEnemyIntent.Heal => intentType == IntentType.Heal,
			CardExtraEffectEnemyIntent.Escape => intentType == IntentType.Escape,
			CardExtraEffectEnemyIntent.Summon => intentType == IntentType.Summon,
			CardExtraEffectEnemyIntent.Sleep => intentType == IntentType.Sleep,
			CardExtraEffectEnemyIntent.Stun => intentType == IntentType.Stun,
			_ => false
		};
	}

	private static string BuildCountCardFilter(CardExtraEffect effect)
	{
		string poolAdj = effect.CountCardPool switch
		{
			CardGeneratedCardPool.Colorless => GeneratedCardPoolLabel(CardGeneratedCardPool.Colorless) + " ",
			CardGeneratedCardPool.Ironclad => GeneratedCardPoolLabel(CardGeneratedCardPool.Ironclad) + " ",
			CardGeneratedCardPool.Silent => GeneratedCardPoolLabel(CardGeneratedCardPool.Silent) + " ",
			CardGeneratedCardPool.Defect => GeneratedCardPoolLabel(CardGeneratedCardPool.Defect) + " ",
			CardGeneratedCardPool.Regent => GeneratedCardPoolLabel(CardGeneratedCardPool.Regent) + " ",
			CardGeneratedCardPool.Necrobinder => GeneratedCardPoolLabel(CardGeneratedCardPool.Necrobinder) + " ",
			CardGeneratedCardPool.Ancient => GeneratedCardPoolLabel(CardGeneratedCardPool.Ancient) + " ",
			_ => string.Empty
		};

		string typeAdj = effect.CountCardType switch
		{
			CardGeneratedCardType.Attack => GeneratedCardTypeLabel(CardGeneratedCardType.Attack) + " ",
			CardGeneratedCardType.Skill => GeneratedCardTypeLabel(CardGeneratedCardType.Skill) + " ",
			CardGeneratedCardType.Power => GeneratedCardTypeLabel(CardGeneratedCardType.Power) + " ",
			CardGeneratedCardType.Playable => CardEditorLoc.T("cardText.value.playable", "playable") + " ",
			CardGeneratedCardType.Status => GeneratedCardTypeLabel(CardGeneratedCardType.Status) + " ",
			CardGeneratedCardType.Curse => GeneratedCardTypeLabel(CardGeneratedCardType.Curse) + " ",
			CardGeneratedCardType.Quest => GeneratedCardTypeLabel(CardGeneratedCardType.Quest) + " ",
			_ => string.Empty
		};

		CardExtraEffectCountCardFilter filter = effect.CountCardFilter;
		if (filter == CardExtraEffectCountCardFilter.Any && effect.CountOnlyBlockCards)
		{
			filter = CardExtraEffectCountCardFilter.GainBlock;
		}

		string effectAdj = filter == CardExtraEffectCountCardFilter.Any ? string.Empty : CountCardFilterLabel(filter) + " ";

		string prefix = (effectAdj + poolAdj + typeAdj).Trim();
		return string.IsNullOrEmpty(prefix) ? string.Empty : prefix + " ";
	}

	private static string ApplyTriggerPrefix(string line, CardExtraEffectTrigger trigger)
	{
		string fallback = trigger switch
		{
			CardExtraEffectTrigger.OnPlay => "When played: ",
			CardExtraEffectTrigger.OnDraw => "When drawn: ",
			CardExtraEffectTrigger.OnDiscard => "When discarded: ",
			CardExtraEffectTrigger.OnExhaust => "When exhausted: ",
			CardExtraEffectTrigger.Fatal => "Fatal: ",
			CardExtraEffectTrigger.EndOfTurnInHand => "End of your turn (in hand): ",
			CardExtraEffectTrigger.EndOfTurn => string.Empty,
			CardExtraEffectTrigger.OstyDealDamage => "Whenever Osty attacks: ",
			_ => string.Empty
		};

		string prefix = CardEditorLoc.Enum("triggerPrefix", trigger, fallback);

		return string.IsNullOrEmpty(prefix) ? line : prefix + line;
	}

	private static string WrapGrantToCard(string line, CardExtraEffect effect)
	{
		string pileLocation = GetCardPileLocation(effect.CardSelectionPile);
		string selectionText = BuildGrantSelectionText(effect);

		string durationText = effect.CardGrantDuration switch
		{
			CardExtraEffectCardGrantDuration.ThisCombat => CardEditorLoc.T("cardText.duration.thisCombat", "this combat"),
			CardExtraEffectCardGrantDuration.UntilPlayed => CardEditorLoc.T("cardText.duration.untilPlayed", "until played"),
			CardExtraEffectCardGrantDuration.Turns => effect.CardGrantTurns <= 1
				? CardEditorLoc.T("cardText.duration.thisTurn", "this turn")
				: CardEditorLoc.F("cardText.duration.nextTurns", $"for the next {Math.Max(1, effect.CardGrantTurns).ToString(CultureInfo.InvariantCulture)} turns", ("Turns", Math.Max(1, effect.CardGrantTurns))),
			_ => CardEditorLoc.T("cardText.duration.thisTurn", "this turn")
		};

		return CardEditorLoc.F(
			"cardText.grant.wrap",
			$"Give {selectionText} {pileLocation} {durationText}: {line}",
			("Selection", selectionText),
			("Pile", pileLocation),
			("Duration", durationText),
			("Line", line));
	}

	private static string FormatEnchantCard(CardExtraEffect effect, string amountText)
	{
		if (!TryResolveEffectEnchantmentId(effect, out ModelId enchantmentId))
		{
			return string.Empty;
		}

		EnchantmentModel? enchantment = ModelDb.GetByIdOrNull<EnchantmentModel>(enchantmentId);
		if (enchantment == null)
		{
			return string.Empty;
		}

		string enchantmentTitle = enchantment.Title.GetFormattedText();
		string durationSuffix = BuildEnchantmentDurationSuffix(effect);
		if (effect.GrantToCard)
		{
			string pileLocation = GetCardPileLocation(effect.CardSelectionPile);
			string selectionText = BuildGrantSelectionText(effect);
			return CardEditorLoc.F(
				"cardText.enchantCard.grant",
				$"Enchant {selectionText} {pileLocation} with {amountText} {enchantmentTitle}{durationSuffix}.",
				("Selection", selectionText),
				("Pile", pileLocation),
				("Amount", amountText),
				("Enchantment", enchantmentTitle),
				("Duration", durationSuffix));
		}

		return CardEditorLoc.F(
			"cardText.enchantCard.self",
			$"This card gains {amountText} {enchantmentTitle}{durationSuffix}.",
			("Amount", amountText),
			("Enchantment", enchantmentTitle),
			("Duration", durationSuffix));
	}

	private static string BuildEnchantmentDurationSuffix(CardExtraEffect effect)
	{
		string suffix = effect.EnchantmentDuration switch
		{
			CardExtraEffectEnchantmentDuration.Permanent => string.Empty,
			CardExtraEffectEnchantmentDuration.ThisTurn => CardEditorLoc.T("cardText.duration.thisTurn", "this turn"),
			CardExtraEffectEnchantmentDuration.ThisCombat => CardEditorLoc.T("cardText.duration.thisCombat", "this combat"),
			CardExtraEffectEnchantmentDuration.UntilPlayed => CardEditorLoc.T("cardText.duration.untilPlayed", "until played"),
			CardExtraEffectEnchantmentDuration.Turns => effect.EnchantmentTurns <= 1
				? CardEditorLoc.T("cardText.duration.thisTurn", "this turn")
				: CardEditorLoc.F("cardText.duration.nextTurns", $"for the next {Math.Max(1, effect.EnchantmentTurns).ToString(CultureInfo.InvariantCulture)} turns", ("Turns", Math.Max(1, effect.EnchantmentTurns))),
			_ => CardEditorLoc.T("cardText.duration.thisCombat", "this combat")
		};

		return string.IsNullOrEmpty(suffix) ? string.Empty : " " + suffix;
	}

	private static bool TryResolveEffectEnchantmentId(CardExtraEffect effect, out ModelId enchantmentId)
	{
		enchantmentId = ModelId.none;
		if (effect == null || string.IsNullOrWhiteSpace(effect.EnchantmentId))
		{
			return false;
		}

		try
		{
			enchantmentId = ModelId.Deserialize(effect.EnchantmentId.Trim());
		}
		catch
		{
			enchantmentId = ModelId.none;
			return false;
		}

		return enchantmentId != ModelId.none && ModelDb.GetByIdOrNull<EnchantmentModel>(enchantmentId) != null;
	}

	private static string BuildGrantSelectionText(CardExtraEffect effect)
	{
		string typeAdj = effect.CardSelectionType switch
		{
			CardGeneratedCardType.Attack => GeneratedCardTypeLabel(CardGeneratedCardType.Attack) + " ",
			CardGeneratedCardType.Skill => GeneratedCardTypeLabel(CardGeneratedCardType.Skill) + " ",
			CardGeneratedCardType.Power => GeneratedCardTypeLabel(CardGeneratedCardType.Power) + " ",
			CardGeneratedCardType.Playable => GeneratedCardTypeLabel(CardGeneratedCardType.Playable) + " ",
			CardGeneratedCardType.Status => GeneratedCardTypeLabel(CardGeneratedCardType.Status) + " ",
			CardGeneratedCardType.Curse => GeneratedCardTypeLabel(CardGeneratedCardType.Curse) + " ",
			CardGeneratedCardType.Quest => GeneratedCardTypeLabel(CardGeneratedCardType.Quest) + " ",
			_ => string.Empty
		};

		string poolSuffix = BuildCountScalingPoolSuffix(effect.CardSelectionPool);
		string filterSuffix = effect.CardSelectionFilter == CardExtraEffectCountCardFilter.Any
			? string.Empty
			: $" ({CountCardFilterLabel(effect.CardSelectionFilter)})";
		string suffix = poolSuffix + filterSuffix;

		if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All)
		{
			if (string.IsNullOrEmpty(typeAdj))
				return CardEditorLoc.T("cardText.selection.allCards", "all cards") + suffix;
			return "all " + typeAdj + CardEditorLoc.T("cardText.cards", "cards") + suffix;
		}

		bool countIsX = effect.CardSelectionCountIsX;
		int count = Math.Max(0, effect.CardSelectionCount);
		string countText = countIsX ? "X" : count.ToString(CultureInfo.InvariantCulture);
		bool singular = !countIsX && count == 1;

		if (string.IsNullOrEmpty(typeAdj))
		{
			return (effect.CardSelectionMode switch
			{
				CardExtraEffectCardSelectionMode.Random => singular
					? CardEditorLoc.T("cardText.selection.randomCard", "a random card")
					: CardEditorLoc.F("cardText.selection.randomCards", $"{countText} random cards", ("Count", countText)),
				CardExtraEffectCardSelectionMode.UpTo => singular
					? CardEditorLoc.T("cardText.selection.upToOneCard", "up to 1 card")
					: CardEditorLoc.F("cardText.selection.upToCards", $"up to {countText} cards", ("Count", countText)),
				_ => singular
					? CardEditorLoc.T("cardText.selection.aCard", "a card")
					: CardEditorLoc.F("cardText.selection.cards", $"{countText} cards", ("Count", countText))
			}) + suffix;
		}

		string cardsText = CardEditorLoc.T("cardText.cards", "cards");
		string trimmedType = typeAdj.TrimStart();
		bool typeUsesAn = trimmedType.Length > 0 && "aeiou".Contains(char.ToLowerInvariant(trimmedType[0]));
		string typeArticle = typeUsesAn
			? CardEditorLoc.T("cardText.article.an", "an")
			: CardEditorLoc.T("cardText.article.a", "a");
		return (effect.CardSelectionMode switch
		{
			CardExtraEffectCardSelectionMode.Random => singular
				? $"a random {typeAdj}card"
				: $"{countText} random {typeAdj}{cardsText}",
			CardExtraEffectCardSelectionMode.UpTo => singular
				? $"up to 1 {typeAdj}card"
				: $"up to {countText} {typeAdj}{cardsText}",
			_ => singular
				? $"{typeArticle} {typeAdj}card"
				: $"{countText} {typeAdj}{cardsText}",
		}) + suffix;
	}

	private static string FormatCreatedCardsUpgraded(int amount, string amountText)
	{
		return amount <= 1
			? CardEditorLoc.T("cardText.createdCards.upgraded", "Cards created by this card are [gold]Upgraded[/gold].")
			: CardEditorLoc.F("cardText.createdCards.upgraded.times", $"Cards created by this card are [gold]Upgraded[/gold]. ({amountText} times)", ("Times", amountText));
	}

	private static string FormatGeneratedCardsUpgraded(CardExtraEffect effect, int amount, string amountText, string? durationSuffix)
	{
		string affected = BuildAffectedCardTypeText(effect);
		string created = CardEditorLoc.T("cardText.created", "created");

		string line = amount <= 1
			? CardEditorLoc.F(
				"cardText.generatedCards.upgraded",
				$"{affected} {created} are [gold]Upgraded[/gold].",
				("Affected", affected),
				("Created", created))
			: CardEditorLoc.F(
				"cardText.generatedCards.upgraded.times",
				$"{affected} {created} are [gold]Upgraded[/gold]. ({amountText} times)",
				("Affected", affected),
				("Created", created),
				("Times", amountText));

		if (durationSuffix == null)
		{
			return line;
		}

		return CardEditorLoc.F(
			"cardText.generatedCards.upgraded.duration",
			$"{line} {durationSuffix}.",
			("Line", line),
			("Duration", durationSuffix));
	}

	private static string FormatCardsInPileUpgradedAura(CardExtraEffect effect, int amount, string amountText, string? durationSuffix)
	{
		string affected = BuildAffectedCardTypeText(effect);
		string pile = GetCardPileLocation(effect.CardSelectionPile);

		string line = amount <= 1
			? CardEditorLoc.F(
				"cardText.cardsInPile.upgraded",
				$"{affected} {pile} are [gold]Upgraded[/gold].",
				("Affected", affected),
				("Pile", pile))
			: CardEditorLoc.F(
				"cardText.cardsInPile.upgraded.times",
				$"{affected} {pile} are [gold]Upgraded[/gold]. ({amountText} times)",
				("Affected", affected),
				("Pile", pile),
				("Times", amountText));

		if (durationSuffix == null)
		{
			return line;
		}

		return CardEditorLoc.F(
			"cardText.cardsInPile.upgraded.duration",
			$"{line} {durationSuffix}.",
			("Line", line),
			("Duration", durationSuffix));
	}

	private static string FormatCreatedCardsCostLess(CardExtraEffect effect, string amountText)
	{
		int turns = Math.Max(1, effect.CreatedCardsCostTurns);
		string suffix = effect.CreatedCardsCostDuration switch
		{
			CardCreatedCardsCostDuration.ThisCombat => CardEditorLoc.T("cardText.duration.thisCombat", "this combat"),
			CardCreatedCardsCostDuration.UntilPlayed => CardEditorLoc.T("cardText.duration.untilPlayed", "until played"),
			CardCreatedCardsCostDuration.Turns => turns <= 1
				? CardEditorLoc.T("cardText.duration.thisTurn", "this turn")
				: CardEditorLoc.F("cardText.duration.nextTurns", $"for the next {turns.ToString(CultureInfo.InvariantCulture)} turns", ("Turns", turns)),
			_ => CardEditorLoc.T("cardText.duration.thisTurn", "this turn")
		};

		return effect.Amount == -1
			? CardEditorLoc.F("cardText.createdCards.cost", $"Cards created by this card cost {amountText} {suffix}.", ("Amount", amountText), ("Duration", suffix))
			: CardEditorLoc.F("cardText.createdCards.costLess", $"Cards created by this card cost {amountText} less {suffix}.", ("Amount", amountText), ("Duration", suffix));
	}

	private static string FormatAddRandomCardToHand(CardExtraEffect effect, int baseAmount, string amountText)
	{
		string filter = BuildGeneratedCardFilter(effect, plural: baseAmount != 1);
		string filterTrim = filter.Trim();
		string suffix = BuildGeneratedCardPoolSuffix(effect.GeneratedCardPool);

		if (baseAmount == 1)
		{
			return string.IsNullOrEmpty(filterTrim)
				? CardEditorLoc.F("cardText.generate.addRandomCard", $"Add a random card{suffix} to your hand.", ("PoolSuffix", suffix))
				: CardEditorLoc.F("cardText.generate.addRandomFilteredCard", $"Add a random {filterTrim} card{suffix} to your hand.", ("Filter", filterTrim), ("PoolSuffix", suffix));
		}

		return string.IsNullOrEmpty(filterTrim)
			? CardEditorLoc.F("cardText.generate.addRandomCards", $"Add {amountText} random cards{suffix} to your hand.", ("Amount", amountText), ("PoolSuffix", suffix))
			: CardEditorLoc.F("cardText.generate.addRandomFilteredCards", $"Add {amountText} random {filterTrim} cards{suffix} to your hand.", ("Amount", amountText), ("Filter", filterTrim), ("PoolSuffix", suffix));
	}

	private static string FormatChooseOneOfThreeToHand(CardExtraEffect effect, int baseAmount, string amountText)
	{
		string filter = BuildGeneratedCardFilter(effect, plural: true);
		string filterTrim = filter.Trim();
		string suffix = BuildGeneratedCardPoolSuffix(effect.GeneratedCardPool);
		string line = string.IsNullOrEmpty(filterTrim)
			? CardEditorLoc.F("cardText.generate.chooseOneOfThree", $"Choose 1 of 3 random cards{suffix} to add to your hand.", ("PoolSuffix", suffix))
			: CardEditorLoc.F("cardText.generate.chooseOneOfThreeFiltered", $"Choose 1 of 3 random {filterTrim} cards{suffix} to add to your hand.", ("Filter", filterTrim), ("PoolSuffix", suffix));

		if (baseAmount <= 1)
		{
			return line;
		}

		return CardEditorLoc.F("cardText.generate.chooseOneOfThree.times", $"{line} ({amountText} times)", ("Line", line), ("Times", amountText));
	}

	private static string FormatChannelOrb(string amountText, int amount, string orbName)
	{
		string localizedOrbName = OrbTitle(orbName, orbName);
		string fallback = $"[gold]Channel[/gold] {amountText} [gold]{localizedOrbName}[/gold].";
		return CardEditorLoc.F("cardText.channelOrb", fallback, ("Amount", amountText), ("Orb", localizedOrbName));
	}

	private static string FormatChannelRandomOrb(string amountText, int amount)
	{
		string fallback = amount == 1
			? $"[gold]Channel[/gold] {amountText} random Orb."
			: $"[gold]Channel[/gold] {amountText} random Orbs.";
		return CardEditorLoc.F("cardText.channelRandomOrb", fallback, ("Amount", amountText));
	}

	private static string FormatMoveCardsBetweenPiles(CardExtraEffect effect, int amount, string amountText)
	{
		if (effect == null)
		{
			return string.Empty;
		}

		// Prefer vanilla wording for common movement patterns (discard/exhaust) so it reads naturally and matches synergies.
		if (effect.CardSelectionPile == CardExtraEffectCardPile.Hand && effect.MoveToPile == CardExtraEffectCardPile.DiscardPile)
		{
			bool random = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Random;
			bool all = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All;
			bool upTo = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.UpTo;
			if (all)
			{
				return CardEditorLoc.T("cardText.discard.hand", "Discard your hand.");
			}
			if (upTo)
			{
				return CardEditorLoc.F("cardText.discard.upTo", $"Discard up to {amountText} cards.", ("Amount", amountText));
			}
			if (amount == 1)
			{
				return random
					? CardEditorLoc.T("cardText.discard.randomOne", "Discard a random card.")
					: CardEditorLoc.T("cardText.discard.one", "Discard a card.");
			}

			return random
				? CardEditorLoc.F("cardText.discard.randomMany", $"Discard {amountText} random cards.", ("Amount", amountText))
				: CardEditorLoc.F("cardText.discard.many", $"Discard {amountText} cards.", ("Amount", amountText));
		}
		if (effect.CardSelectionPile == CardExtraEffectCardPile.Hand && effect.MoveToPile == CardExtraEffectCardPile.ExhaustPile)
		{
			bool random = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Random;
			bool all = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All;
			bool upTo = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.UpTo;
			if (all)
			{
				return CardEditorLoc.T("cardText.exhaust.hand", "Exhaust your hand.");
			}
			if (upTo)
			{
				return CardEditorLoc.F("cardText.exhaust.upTo", $"Exhaust up to {amountText} cards.", ("Amount", amountText));
			}
			if (amount == 1)
			{
				return random
					? CardEditorLoc.T("cardText.exhaust.randomOne", "Exhaust a random card.")
					: CardEditorLoc.T("cardText.exhaust.one", "Exhaust a card.");
			}

			return random
				? CardEditorLoc.F("cardText.exhaust.randomMany", $"Exhaust {amountText} random cards.", ("Amount", amountText))
				: CardEditorLoc.F("cardText.exhaust.many", $"Exhaust {amountText} cards.", ("Amount", amountText));
		}

		string from = effect.CardSelectionPile == CardExtraEffectCardPile.AllPiles
			? CardEditorLoc.T("cardText.pile.anywhere", "anywhere")
			: GetCardPilePossessive(effect.CardSelectionPile);
		string to = GetCardPileDestination(effect.MoveToPile, effect.MoveToPosition);

		string moveResult;
		if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All)
		{
			moveResult = CardEditorLoc.F("cardText.moveCards.all", $"Move all cards from {from} to {to}.", ("From", from), ("To", to));
		}
		else if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.UpTo)
		{
			moveResult = CardEditorLoc.F("cardText.moveCards.upTo", $"Move up to {amountText} cards from {from} to {to}.", ("Amount", amountText), ("From", from), ("To", to));
		}
		else if (effect.CardSelectionPile == CardExtraEffectCardPile.AllPiles)
		{
			bool random = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Random;
			if (random)
			{
				moveResult = amount == 1
					? CardEditorLoc.F("cardText.moveCards.anywhere.random.one", $"Move a random card from any pile to {to}.", ("To", to))
					: CardEditorLoc.F("cardText.moveCards.anywhere.random.many", $"Move {amountText} random cards from any pile to {to}.", ("Amount", amountText), ("To", to));
			}
			else
			{
				moveResult = amount == 1
					? CardEditorLoc.F("cardText.moveCards.anywhere.one", $"Choose a card from any pile and move it to {to}.", ("To", to))
					: CardEditorLoc.F("cardText.moveCards.anywhere.many", $"Choose {amountText} cards from any pile and move them to {to}.", ("Amount", amountText), ("To", to));
			}
		}
		else
		{
			bool random = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Random;
			if (random)
			{
				moveResult = amount == 1
					? CardEditorLoc.F("cardText.moveCards.random.one", $"Move a random card from {from} to {to}.", ("From", from), ("To", to))
					: CardEditorLoc.F("cardText.moveCards.random.many", $"Move {amountText} random cards from {from} to {to}.", ("Amount", amountText), ("From", from), ("To", to));
			}
			else
			{
				moveResult = amount == 1
					? CardEditorLoc.F("cardText.moveCards.one", $"Move a card from {from} to {to}.", ("From", from), ("To", to))
					: CardEditorLoc.F("cardText.moveCards.many", $"Move {amountText} cards from {from} to {to}.", ("Amount", amountText), ("From", from), ("To", to));
			}
		}
		return BuildCostFilteredText(moveResult, effect);
	}

	private static string FormatUpgradeCardsInPile(CardExtraEffect effect, int amount, string amountText, string? durationSuffix = null)
	{
		string pile = GetCardPileLocation(effect.CardSelectionPile);
		bool random = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Random;
		bool all = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All;
		bool upTo = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.UpTo;

		string result;
		if (all)
		{
			result = CardEditorLoc.F("cardText.upgradeFromPile.all", $"Upgrade all upgradable cards {pile}.", ("Pile", pile));
		}
		else if (upTo)
		{
			result = CardEditorLoc.F("cardText.upgradeFromPile.upTo", $"Upgrade up to {amountText} cards {pile}.", ("Amount", amountText), ("Pile", pile));
		}
		else if (amount == 1)
		{
			result = random
				? CardEditorLoc.F("cardText.upgradeFromPile.random.one", $"Upgrade a random card {pile}.", ("Pile", pile))
				: CardEditorLoc.F("cardText.upgradeFromPile.choose.one", $"Upgrade a card {pile}.", ("Pile", pile));
		}
		else
		{
			result = random
				? CardEditorLoc.F("cardText.upgradeFromPile.random.many", $"Upgrade {amountText} random cards {pile}.", ("Amount", amountText), ("Pile", pile))
				: CardEditorLoc.F("cardText.upgradeFromPile.choose.many", $"Upgrade {amountText} cards {pile}.", ("Amount", amountText), ("Pile", pile));
		}

		if (durationSuffix == null)
		{
			return result;
		}

		return CardEditorLoc.F(
			"cardText.upgradeFromPile.duration",
			$"{result} {durationSuffix}.",
			("Line", result),
			("Duration", durationSuffix));
	}

	private static string FormatGrantKeywordToPile(CardExtraEffect effect, int amount, string amountText)
	{
		string keyword = GrantedKeywordLabel(effect.GrantedKeyword);
		string pile = GetCardPileLocation(effect.CardSelectionPile);
		bool all = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All;
		bool random = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Random;
		bool upTo = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.UpTo;

		if (all)
		{
			return CardEditorLoc.F("cardText.grantKeyword.all", $"All cards {pile} gain {keyword} this combat.", ("Pile", pile), ("Keyword", keyword));
		}
		if (upTo)
		{
			return CardEditorLoc.F("cardText.grantKeyword.upTo", $"Choose up to {amountText} cards {pile}. They gain {keyword} this combat.", ("Amount", amountText), ("Pile", pile), ("Keyword", keyword));
		}
		if (amount == 1)
		{
			return random
				? CardEditorLoc.F("cardText.grantKeyword.random.one", $"A random card {pile} gains {keyword} this combat.", ("Pile", pile), ("Keyword", keyword))
				: CardEditorLoc.F("cardText.grantKeyword.one", $"Choose a card {pile}. It gains {keyword} this combat.", ("Pile", pile), ("Keyword", keyword));
		}

		return random
			? CardEditorLoc.F("cardText.grantKeyword.random.many", $"{amountText} random cards {pile} gain {keyword} this combat.", ("Amount", amountText), ("Pile", pile), ("Keyword", keyword))
			: CardEditorLoc.F("cardText.grantKeyword.many", $"Choose {amountText} cards {pile}. They gain {keyword} this combat.", ("Amount", amountText), ("Pile", pile), ("Keyword", keyword));
	}

	private static string FormatUpgradeDeckCards(CardExtraEffect effect, int amount, string amountText)
	{
		bool all = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All;
		if (all)
		{
			return CardEditorLoc.T("cardText.upgradeDeck.all", "Upgrade all upgradable cards in your deck.");
		}
		if (amount == 1)
		{
			return CardEditorLoc.T("cardText.upgradeDeck.random.one", "Upgrade a random card in your deck.");
		}
		return CardEditorLoc.F("cardText.upgradeDeck.random.many", $"Upgrade {amountText} random cards in your deck.", ("Amount", amountText));
	}

	private static string FormatRemoveBlock(CardExtraEffectTarget target, string amountText)
	{
		return target switch
		{
			CardExtraEffectTarget.AllEnemies => CardEditorLoc.F("cardText.removeBlock.allEnemies", $"ALL enemies lose {amountText} Block.", ("Amount", amountText)),
			CardExtraEffectTarget.RandomEnemy => CardEditorLoc.F("cardText.removeBlock.randomEnemy", $"A random enemy loses {amountText} Block.", ("Amount", amountText)),
			CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.removeBlock.self", $"Lose {amountText} Block.", ("Amount", amountText)),
			_ => CardEditorLoc.F("cardText.removeBlock.target", $"Enemy loses {amountText} Block.", ("Amount", amountText))
		};
	}

	private static string FormatRemoveArtifact(CardExtraEffectTarget target, string amountText)
	{
		return target switch
		{
			CardExtraEffectTarget.AllEnemies => CardEditorLoc.F("cardText.removeArtifact.allEnemies", $"ALL enemies lose {amountText} Artifact.", ("Amount", amountText)),
			CardExtraEffectTarget.RandomEnemy => CardEditorLoc.F("cardText.removeArtifact.randomEnemy", $"A random enemy loses {amountText} Artifact.", ("Amount", amountText)),
			CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.removeArtifact.self", $"Lose {amountText} Artifact.", ("Amount", amountText)),
			_ => CardEditorLoc.F("cardText.removeArtifact.target", $"Enemy loses {amountText} Artifact.", ("Amount", amountText))
		};
	}

	private static string FormatDiscardCards(CardExtraEffect effect, int amount, string amountText)
	{
		if (effect == null)
		{
			return string.Empty;
		}

		bool random = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Random;
		bool all = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All;
		bool upTo = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.UpTo;
		string suffix = string.Empty;
		if (effect.CardSelectionPile != CardExtraEffectCardPile.Hand)
		{
			string pile = GetCardPilePossessive(effect.CardSelectionPile);
			suffix = CardEditorLoc.F("cardText.discard.fromPile", $" from {pile}", ("Pile", pile));
		}

		string discardResult;
		if (all)
		{
			discardResult = CardEditorLoc.F("cardText.discard.all.suffix", $"Discard all cards{suffix}.", ("Suffix", suffix));
		}
		else if (upTo)
		{
			discardResult = CardEditorLoc.F("cardText.discard.upTo.suffix", $"Discard up to {amountText} cards{suffix}.", ("Amount", amountText), ("Suffix", suffix));
		}
		else if (amount == 1)
		{
			discardResult = random
				? CardEditorLoc.F("cardText.discard.randomOne.suffix", $"Discard a random card{suffix}.", ("Suffix", suffix))
				: CardEditorLoc.F("cardText.discard.one.suffix", $"Discard a card{suffix}.", ("Suffix", suffix));
		}
		else
		{
			discardResult = random
				? CardEditorLoc.F("cardText.discard.randomMany.suffix", $"Discard {amountText} random cards{suffix}.", ("Amount", amountText), ("Suffix", suffix))
				: CardEditorLoc.F("cardText.discard.many.suffix", $"Discard {amountText} cards{suffix}.", ("Amount", amountText), ("Suffix", suffix));
		}
		return BuildCostFilteredText(discardResult, effect);
	}

	private static string FormatExhaustCards(CardExtraEffect effect, int amount, string amountText)
	{
		if (effect == null)
		{
			return string.Empty;
		}

		bool random = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Random;
		bool all = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All;
		bool upTo = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.UpTo;
		string suffix = string.Empty;
		if (effect.CardSelectionPile != CardExtraEffectCardPile.Hand)
		{
			string pile = GetCardPilePossessive(effect.CardSelectionPile);
			suffix = CardEditorLoc.F("cardText.exhaust.fromPile", $" from {pile}", ("Pile", pile));
		}

		string exhaustResult;
		if (all)
		{
			exhaustResult = CardEditorLoc.F("cardText.exhaust.all.suffix", $"Exhaust all cards{suffix}.", ("Suffix", suffix));
		}
		else if (upTo)
		{
			exhaustResult = CardEditorLoc.F("cardText.exhaust.upTo.suffix", $"Exhaust up to {amountText} cards{suffix}.", ("Amount", amountText), ("Suffix", suffix));
		}
		else if (amount == 1)
		{
			exhaustResult = random
				? CardEditorLoc.F("cardText.exhaust.randomOne.suffix", $"Exhaust a random card{suffix}.", ("Suffix", suffix))
				: CardEditorLoc.F("cardText.exhaust.one.suffix", $"Exhaust a card{suffix}.", ("Suffix", suffix));
		}
		else
		{
			exhaustResult = random
				? CardEditorLoc.F("cardText.exhaust.randomMany.suffix", $"Exhaust {amountText} random cards{suffix}.", ("Amount", amountText), ("Suffix", suffix))
				: CardEditorLoc.F("cardText.exhaust.many.suffix", $"Exhaust {amountText} cards{suffix}.", ("Amount", amountText), ("Suffix", suffix));
		}
		return BuildCostFilteredText(exhaustResult, effect);
	}

	private static string FormatEvokeOrbs(int amount, string amountText)
	{
		if (amount == 1)
		{
			return CardEditorLoc.T("cardText.evoke.one", "Evoke your next Orb.");
		}

		return CardEditorLoc.F("cardText.evoke.many", $"Evoke {amountText} Orbs.", ("Amount", amountText));
	}

	private static string FormatOrbAction(CardExtraEffect effect, int amount, string amountText)
	{
		if (effect == null)
		{
			return string.Empty;
		}

		if (effect.OrbAction == CardExtraEffectOrbAction.AddSlots)
		{
			return amount == 1 ? "Gain 1 Orb Slot." : $"Gain {amountText} Orb Slots.";
		}

		if (effect.OrbAction == CardExtraEffectOrbAction.RemoveSlots)
		{
			if (effect.OrbScope == CardExtraEffectOrbScope.All)
				return "Lose all Orb Slots.";
			return amount == 1 ? "Lose 1 Orb Slot." : $"Lose {amountText} Orb Slots.";
		}

		if (effect.OrbAction == CardExtraEffectOrbAction.Channel)
		{
			return effect.OrbType == CardExtraEffectOrbType.Any
				? FormatChannelRandomOrb(amountText, amount)
				: FormatChannelOrb(amountText, amount, effect.OrbType.ToString());
		}

		string actionText = effect.OrbAction == CardExtraEffectOrbAction.Remove ? "Lose" : "Evoke";
		string orbTypeText = GetOrbTypeTitle(effect.OrbType);
		string orbPrefix = string.IsNullOrWhiteSpace(orbTypeText) ? string.Empty : orbTypeText + " ";

		if (effect.OrbScope == CardExtraEffectOrbScope.All)
		{
			string allLine = $"{actionText} all {orbPrefix}Orbs.";
			if (effect.OrbAction == CardExtraEffectOrbAction.Evoke && effect.OrbFollowUp == CardExtraEffectOrbFollowUp.ChannelSameType)
			{
				allLine = allLine.TrimEnd('.') + ", then [gold]Channel[/gold] the same Orb type.";
			}
			return allLine;
		}

		string selectionText = effect.OrbSelection switch
		{
			CardExtraEffectOrbSelection.Rightmost => "rightmost",
			CardExtraEffectOrbSelection.Middle => "middle",
			_ => "leftmost"
		};
		string orbWord = amount == 1 ? "Orb" : "Orbs";

		string line = amount == 1
			? $"{actionText} your {selectionText} {orbPrefix}{orbWord}."
			: $"{actionText} {amountText} {selectionText} {orbPrefix}{orbWord}.";

		if (effect.OrbAction == CardExtraEffectOrbAction.Evoke && effect.OrbFollowUp == CardExtraEffectOrbFollowUp.ChannelSameType)
		{
			line = line.TrimEnd('.') + ", then [gold]Channel[/gold] the same Orb type.";
		}

		return line;
	}

	private static string FormatOstyAction(CardExtraEffect effect, int amount, string amountText)
	{
		if (effect == null)
		{
			return string.Empty;
		}
		return effect.OstyAction switch
		{
			CardExtraEffectOstyAction.Attack => $"Osty attacks for {amountText} damage.",
			CardExtraEffectOstyAction.AttackAll => $"Osty attacks ALL enemies for {amountText} damage.",
			CardExtraEffectOstyAction.Heal => $"Heal Osty {amountText} HP.",
			CardExtraEffectOstyAction.Kill => "Kill Osty.",
			_ => string.Empty
		};
	}

	private static string FormatAddCopyOfThisCard(CardExtraEffect effect, int amount, string amountText)
	{
		if (effect == null)
		{
			return string.Empty;
		}

		string to = GetCardPileDestination(effect.MoveToPile, effect.MoveToPosition);

		return amount == 1
			? CardEditorLoc.F("cardText.copyThisCard.one", $"Put a copy of this card into {to}.", ("To", to))
			: CardEditorLoc.F("cardText.copyThisCard.many", $"Put {amountText} copies of this card into {to}.", ("Amount", amountText), ("To", to));
	}

	private static string FormatCardStarCostDelta(int signedAmount, string? durationSuffix, bool forceUpgradeHighlight)
	{
		int magnitude = Math.Abs(signedAmount);
		string amountText = BuildStarIcons(magnitude);
		if (forceUpgradeHighlight)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, 1);
		}

		bool isLess = signedAmount > 0;
		string lessOrMore = isLess
			? CardEditorLoc.T("cardText.costDelta.less", "less")
			: CardEditorLoc.T("cardText.costDelta.more", "more");

		if (durationSuffix != null)
		{
			return CardEditorLoc.F(
				"cardText.cardStarCostDelta.duration",
				$"This card costs {amountText} {lessOrMore} {durationSuffix}.",
				("Amount", amountText),
				("LessOrMore", lessOrMore),
				("Stars", string.Empty),
				("Duration", durationSuffix));
		}

		return CardEditorLoc.F(
			"cardText.cardStarCostDelta",
			$"This card costs {amountText} {lessOrMore}.",
			("Amount", amountText),
			("LessOrMore", lessOrMore),
			("Stars", string.Empty));
	}

	private static string FormatCardStarCostDeltaText(string amountText, bool isLess, string? durationSuffix, bool forceUpgradeHighlight)
	{
		if (forceUpgradeHighlight)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, 1);
		}

		string lessOrMore = isLess
			? CardEditorLoc.T("cardText.costDelta.less", "less")
			: CardEditorLoc.T("cardText.costDelta.more", "more");

		if (durationSuffix != null)
		{
			return CardEditorLoc.F(
				"cardText.cardStarCostDelta.duration",
				$"This card costs {amountText} {lessOrMore} {durationSuffix}.",
				("Amount", amountText),
				("LessOrMore", lessOrMore),
				("Stars", string.Empty),
				("Duration", durationSuffix));
		}

		return CardEditorLoc.F(
			"cardText.cardStarCostDelta",
			$"This card costs {amountText} {lessOrMore}.",
			("Amount", amountText),
			("LessOrMore", lessOrMore),
			("Stars", string.Empty));
	}

	private static string FormatAddSpecificCardToHand(CardExtraEffect effect, int amount, string amountText)
	{
		string cardName = ResolveSpecificCardTitle(effect?.SpecificCardId) ?? CardEditorLoc.T("cardText.specificCard.unknown", "Unknown Card");
		string destination = GetCardPileDestination(effect.MoveToPile, effect.MoveToPosition);
		return amount == 1
			? CardEditorLoc.F("cardText.addSpecificCard.one", $"Add a {cardName} to {destination}.", ("Card", cardName), ("Destination", destination))
			: CardEditorLoc.F("cardText.addSpecificCard.many", $"Add {amountText} copies of {cardName} to {destination}.", ("Amount", amountText), ("Card", cardName), ("Destination", destination));
	}

	private static string FormatFetchSpecificCardToHand(CardExtraEffect effect, int amount, string amountText)
	{
		string cardName = ResolveSpecificCardTitle(effect?.SpecificCardId) ?? CardEditorLoc.T("cardText.specificCard.unknown", "Unknown Card");
		string destination = GetCardPileDestination(effect.MoveToPile, effect.MoveToPosition);
		bool all = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All;
		if (all)
			return CardEditorLoc.F("cardText.fetchSpecificCard.all", $"Put all copies of {cardName} into your {destination} from anywhere.", ("Card", cardName), ("Destination", destination));
		return amount == 1
			? CardEditorLoc.F("cardText.fetchSpecificCard.one", $"Put {cardName} into your {destination} from anywhere.", ("Card", cardName), ("Destination", destination))
			: CardEditorLoc.F("cardText.fetchSpecificCard.many", $"Put up to {amountText} copies of {cardName} into your {destination} from anywhere.", ("Amount", amountText), ("Card", cardName), ("Destination", destination));
	}

	private static string FormatPlayCardFromPile(CardExtraEffect effect, int amount, string amountText)
	{
		string pile = GetCardPilePossessive(effect.CardSelectionPile);
		bool random = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Random;
		bool all = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All;
		bool upTo = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.UpTo;
		string result;
		if (all)
		{
			result = CardEditorLoc.F("cardText.playFromPile.all", $"Play all cards from {pile}.", ("Pile", pile));
		}
		else if (upTo)
		{
			result = CardEditorLoc.F("cardText.playFromPile.upTo.many", $"Play up to {amountText} cards from {pile}.", ("Amount", amountText), ("Pile", pile));
		}
		else if (amount == 1)
		{
			result = random
				? CardEditorLoc.F("cardText.playFromPile.random.one", $"Play a random card from {pile}.", ("Pile", pile))
				: CardEditorLoc.F("cardText.playFromPile.choose.one", $"Play a card from {pile}.", ("Pile", pile));
		}
		else
		{
			result = random
				? CardEditorLoc.F("cardText.playFromPile.random.many", $"Play {amountText} random cards from {pile}.", ("Amount", amountText), ("Pile", pile))
				: CardEditorLoc.F("cardText.playFromPile.choose.many", $"Play {amountText} cards from {pile}.", ("Amount", amountText), ("Pile", pile));
		}
		return BuildCostFilteredText(result, effect);
	}

	private static string FormatAutoPlaySelfFromPile(CardExtraEffect effect)
	{
		string pile = GetCardPileLocation(effect.CardSelectionPile);
		return effect.Trigger switch
		{
			CardExtraEffectTrigger.OnDraw => CardEditorLoc.F(
				"cardText.autoPlaySelf.onDraw",
				$"Whenever a card is drawn, if {pile}, play this.",
				("Pile", pile)),
			CardExtraEffectTrigger.OnDiscard => CardEditorLoc.F(
				"cardText.autoPlaySelf.onDiscard",
				$"Whenever a card is discarded, if {pile}, play this.",
				("Pile", pile)),
			CardExtraEffectTrigger.OnExhaust => CardEditorLoc.F(
				"cardText.autoPlaySelf.onExhaust",
				$"Whenever a card is exhausted, if {pile}, play this.",
				("Pile", pile)),
			CardExtraEffectTrigger.EndOfTurn => CardEditorLoc.F(
				"cardText.autoPlaySelf.endOfTurn",
				$"At the end of your turn, if {pile}, play this.",
				("Pile", pile)),
			CardExtraEffectTrigger.StartOfEnemyTurn => CardEditorLoc.F(
				"cardText.autoPlaySelf.startOfEnemyTurn",
				$"At the start of the enemy's turn, if {pile}, play this.",
				("Pile", pile)),
			CardExtraEffectTrigger.OnPlay => CardEditorLoc.F(
				"cardText.autoPlaySelf.onPlay",
				$"Whenever a card is played, if {pile}, play this.",
				("Pile", pile)),
			_ => CardEditorLoc.F(
				"cardText.autoPlaySelf.startOfTurn",
				$"At the start of your turn, if {pile}, play this.",
				("Pile", pile))
		};
	}

	private static string FormatDrawCardsThatCostLess(CardExtraEffect effect, int grammarAmount, string amountText, string? durationSuffix)
	{
		int costLess = effect.CardSelectionCount > 0 ? effect.CardSelectionCount : 1;
		string costLessText = costLess.ToString(CultureInfo.InvariantCulture);
		string plural = grammarAmount == 1 ? "it" : "they";
		string cardOrCards = grammarAmount == 1 ? "card" : "cards";
		if (durationSuffix != null)
		{
			return CardEditorLoc.F(
				"cardText.drawCostLess.duration",
				$"Draw {amountText} {cardOrCards}. {char.ToUpper(plural[0])}{plural[1..]} cost {costLessText} less {durationSuffix}.",
				("Amount", amountText), ("CostLess", costLessText), ("Duration", durationSuffix));
		}
		return CardEditorLoc.F(
			"cardText.drawCostLess",
			$"Draw {amountText} {cardOrCards}. {char.ToUpper(plural[0])}{plural[1..]} cost {costLessText} less.",
			("Amount", amountText), ("CostLess", costLessText));
	}

	private static string FormatAutoDrawSelfFromPile(CardExtraEffect effect)
	{
		string pile = GetCardPileLocation(effect.CardSelectionPile);
		return effect.Trigger switch
		{
			CardExtraEffectTrigger.OnDraw => CardEditorLoc.F(
				"cardText.autoDrawSelf.onDraw",
				$"Whenever a card is drawn, if {pile}, draw this.",
				("Pile", pile)),
			CardExtraEffectTrigger.OnDiscard => CardEditorLoc.F(
				"cardText.autoDrawSelf.onDiscard",
				$"Whenever a card is discarded, if {pile}, draw this.",
				("Pile", pile)),
			CardExtraEffectTrigger.OnExhaust => CardEditorLoc.F(
				"cardText.autoDrawSelf.onExhaust",
				$"Whenever a card is exhausted, if {pile}, draw this.",
				("Pile", pile)),
			CardExtraEffectTrigger.EndOfTurn => CardEditorLoc.F(
				"cardText.autoDrawSelf.endOfTurn",
				$"At the end of your turn, if {pile}, draw this.",
				("Pile", pile)),
			CardExtraEffectTrigger.StartOfEnemyTurn => CardEditorLoc.F(
				"cardText.autoDrawSelf.startOfEnemyTurn",
				$"At the start of the enemy's turn, if {pile}, draw this.",
				("Pile", pile)),
			CardExtraEffectTrigger.OnPlay => CardEditorLoc.F(
				"cardText.autoDrawSelf.onPlay",
				$"Whenever a card is played, if {pile}, draw this.",
				("Pile", pile)),
			_ => CardEditorLoc.F(
				"cardText.autoDrawSelf.startOfTurn",
				$"At the start of your turn, if {pile}, draw this.",
				("Pile", pile))
		};
	}

	private static string FormatConditionalAutoFromPile(CardExtraEffect effect, string amountText, bool isPlay)
	{
		string pile = GetCardPileLocation(effect.CardSelectionPile);

		string verbFallback = effect.CountEvent switch
		{
			CardExtraEffectCountEvent.Drawn => "drew",
			CardExtraEffectCountEvent.Discarded => "discarded",
			CardExtraEffectCountEvent.Exhausted => "exhausted",
			CardExtraEffectCountEvent.Generated => "created",
			_ => "played"
		};
		string verb = CardEditorLoc.Enum("historyVerbPast", effect.CountEvent, verbFallback);

		string window = GetCountWindowText(effect);

		string filter = BuildCountCardFilter(effect);
		string poolSuffix = BuildCountScalingPoolSuffix(effect.CountCardPool);
		string prefix = string.IsNullOrEmpty(filter) ? string.Empty : filter;

		string key = isPlay ? "cardText.conditionalAutoPlaySelf" : "cardText.conditionalAutoDrawSelf";
		string fallback = isPlay
			? $"If you've {verb} {amountText}+ {prefix}cards{poolSuffix} {window}, if {pile}, play this."
			: $"If you've {verb} {amountText}+ {prefix}cards{poolSuffix} {window}, if {pile}, draw this.";

		return CardEditorLoc.F(key, fallback,
			("Amount", amountText),
			("Verb", verb),
			("Prefix", prefix),
			("PoolSuffix", poolSuffix),
			("Window", window),
			("Pile", pile));
	}

	private static string? ResolveSpecificCardTitle(string? specificCardId)
	{
		if (string.IsNullOrWhiteSpace(specificCardId))
		{
			return null;
		}

		if (!TryParseSpecificCardId(specificCardId, out ModelId id))
		{
			return specificCardId.Trim();
		}

		try
		{
			if (CardEditorCreatedCardsStore.IsCreatedCardId(id))
			{
				return CardEditorCreatedCardsStore.GetTitleForCard(id);
			}

			CardModel? canonical = ModelDb.GetByIdOrNull<CardModel>(id);
			return canonical?.Title ?? specificCardId.Trim();
		}
		catch
		{
			return specificCardId.Trim();
		}
	}

	private static bool TryParseSpecificCardId(string specificCardId, out ModelId id)
	{
		id = ModelId.none;
		if (string.IsNullOrWhiteSpace(specificCardId))
		{
			return false;
		}

		string trimmed = specificCardId.Trim();
		try
		{
			if (int.TryParse(trimmed, System.Globalization.NumberStyles.None, CultureInfo.InvariantCulture, out int createdSlot)
				&& CardEditorCreatedCardsStore.TryGetCreatedCardIdForSlot(createdSlot, out ModelId createdCardId))
			{
				id = createdCardId;
				return true;
			}

			if (trimmed.Contains('.', StringComparison.Ordinal))
			{
				id = ModelId.Deserialize(trimmed);
				return true;
			}

			// Convenience: allow users to type just the entry ("shiv") and assume the "cards" category.
			id = new ModelId("cards", trimmed);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static string FormatCardCostDelta(int signedAmount, string? durationSuffix, bool forceUpgradeHighlight)
	{
		int magnitude = Math.Abs(signedAmount);
		string amountText = magnitude.ToString(CultureInfo.InvariantCulture);
		if (forceUpgradeHighlight)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, 1);
		}

		bool isLess = signedAmount > 0;
		string lessOrMore = isLess
			? CardEditorLoc.T("cardText.costDelta.less", "less")
			: CardEditorLoc.T("cardText.costDelta.more", "more");

		if (durationSuffix != null)
		{
			return CardEditorLoc.F(
				"cardText.cardCostDelta.duration",
				$"This card costs {amountText} {lessOrMore} {durationSuffix}.",
				("Amount", amountText),
				("LessOrMore", lessOrMore),
				("Duration", durationSuffix));
		}

		return CardEditorLoc.F(
			"cardText.cardCostDelta",
			$"This card costs {amountText} {lessOrMore}.",
			("Amount", amountText),
			("LessOrMore", lessOrMore));
	}

	private static string FormatCardCostDeltaText(string amountText, bool isLess, string? durationSuffix, bool forceUpgradeHighlight)
	{
		if (forceUpgradeHighlight)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, 1);
		}

		string lessOrMore = isLess
			? CardEditorLoc.T("cardText.costDelta.less", "less")
			: CardEditorLoc.T("cardText.costDelta.more", "more");

		if (durationSuffix != null)
		{
			return CardEditorLoc.F(
				"cardText.cardCostDelta.duration",
				$"This card costs {amountText} {lessOrMore} {durationSuffix}.",
				("Amount", amountText),
				("LessOrMore", lessOrMore),
				("Duration", durationSuffix));
		}

		return CardEditorLoc.F(
			"cardText.cardCostDelta",
			$"This card costs {amountText} {lessOrMore}.",
			("Amount", amountText),
			("LessOrMore", lessOrMore));
	}

	private static string BuildAffectedCardTypeText(CardExtraEffect effect)
	{
		if (effect == null)
		{
			return CardEditorLoc.T("cardText.cards", "cards");
		}

		CardGeneratedCardPool pool = effect.TriggerCardPool;
		CardGeneratedCardType type = effect.TriggerCardType;

		string poolAdj = pool switch
		{
			CardGeneratedCardPool.Colorless => GeneratedCardPoolLabel(CardGeneratedCardPool.Colorless) + " ",
			CardGeneratedCardPool.Ironclad => GeneratedCardPoolLabel(CardGeneratedCardPool.Ironclad) + " ",
			CardGeneratedCardPool.Silent => GeneratedCardPoolLabel(CardGeneratedCardPool.Silent) + " ",
			CardGeneratedCardPool.Defect => GeneratedCardPoolLabel(CardGeneratedCardPool.Defect) + " ",
			CardGeneratedCardPool.Regent => GeneratedCardPoolLabel(CardGeneratedCardPool.Regent) + " ",
			CardGeneratedCardPool.Necrobinder => GeneratedCardPoolLabel(CardGeneratedCardPool.Necrobinder) + " ",
			CardGeneratedCardPool.Ancient => GeneratedCardPoolLabel(CardGeneratedCardPool.Ancient) + " ",
			_ => string.Empty
		};

		string typeAdj = type switch
		{
			CardGeneratedCardType.Attack => GeneratedCardTypeLabel(CardGeneratedCardType.Attack) + " ",
			CardGeneratedCardType.Skill => GeneratedCardTypeLabel(CardGeneratedCardType.Skill) + " ",
			CardGeneratedCardType.Power => GeneratedCardTypeLabel(CardGeneratedCardType.Power) + " ",
			CardGeneratedCardType.Playable => CardEditorLoc.T("cardText.value.playable", "playable") + " ",
			CardGeneratedCardType.Status => GeneratedCardTypeLabel(CardGeneratedCardType.Status) + " ",
			CardGeneratedCardType.Curse => GeneratedCardTypeLabel(CardGeneratedCardType.Curse) + " ",
			CardGeneratedCardType.Quest => GeneratedCardTypeLabel(CardGeneratedCardType.Quest) + " ",
			_ => string.Empty
		};

		string prefix = (poolAdj + typeAdj).Trim();
		string cards = string.IsNullOrEmpty(prefix)
			? CardEditorLoc.T("cardText.cards", "cards")
			: CardEditorLoc.F("cardText.cards.prefixed", $"{prefix} cards", ("Prefix", prefix));

		string suffix = pool == CardGeneratedCardPool.Default ? string.Empty : BuildCountScalingPoolSuffix(pool);
		string result = cards + suffix;
		return result.Length > 0 ? char.ToUpper(result[0]) + result[1..] : result;
	}

	private static string FormatCardTypeCostDelta(CardModel card, CardExtraEffect effect, int signedAmount, string? durationSuffix, bool forceUpgradeHighlight)
	{
		string affected = BuildAffectedCardTypeText(effect);

		int magnitude = Math.Abs(signedAmount);
		string amountText = BuildEnergyIcons(card, magnitude);
		if (forceUpgradeHighlight)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, 1);
		}

		bool isLess = signedAmount > 0;
		string lessOrMore = isLess
			? CardEditorLoc.T("cardText.costDelta.less", "less")
			: CardEditorLoc.T("cardText.costDelta.more", "more");

		if (durationSuffix != null)
		{
			return CardEditorLoc.F(
				"cardText.cardTypeCostDelta.duration",
				$"{affected} cost {amountText} {lessOrMore} {durationSuffix}.",
				("Affected", affected),
				("Amount", amountText),
				("LessOrMore", lessOrMore),
				("Duration", durationSuffix));
		}

		return CardEditorLoc.F(
			"cardText.cardTypeCostDelta",
			$"{affected} cost {amountText} {lessOrMore}.",
			("Affected", affected),
			("Amount", amountText),
			("LessOrMore", lessOrMore));
	}

	private static string FormatCardTypeCostDeltaText(CardExtraEffect effect, string amountText, bool isLess, string? durationSuffix, bool forceUpgradeHighlight)
	{
		string affected = BuildAffectedCardTypeText(effect);
		if (forceUpgradeHighlight)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, 1);
		}

		string lessOrMore = isLess
			? CardEditorLoc.T("cardText.costDelta.less", "less")
			: CardEditorLoc.T("cardText.costDelta.more", "more");

		if (durationSuffix != null)
		{
			return CardEditorLoc.F(
				"cardText.cardTypeCostDelta.duration",
				$"{affected} cost {amountText} {lessOrMore} {durationSuffix}.",
				("Affected", affected),
				("Amount", amountText),
				("LessOrMore", lessOrMore),
				("Duration", durationSuffix));
		}

		return CardEditorLoc.F(
			"cardText.cardTypeCostDelta",
			$"{affected} cost {amountText} {lessOrMore}.",
			("Affected", affected),
			("Amount", amountText),
			("LessOrMore", lessOrMore));
	}

	private static string FormatCardTypeStarCostDelta(CardExtraEffect effect, int signedAmount, string? durationSuffix, bool forceUpgradeHighlight)
	{
		string affected = BuildAffectedCardTypeText(effect);

		int magnitude = Math.Abs(signedAmount);
		string amountText = BuildStarIcons(magnitude);
		if (forceUpgradeHighlight)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, 1);
		}

		bool isLess = signedAmount > 0;
		string lessOrMore = isLess
			? CardEditorLoc.T("cardText.costDelta.less", "less")
			: CardEditorLoc.T("cardText.costDelta.more", "more");

		if (durationSuffix != null)
		{
			return CardEditorLoc.F(
				"cardText.cardTypeStarCostDelta.duration",
				$"{affected} cost {amountText} {lessOrMore} {durationSuffix}.",
				("Affected", affected),
				("Amount", amountText),
				("LessOrMore", lessOrMore),
				("Stars", string.Empty),
				("Duration", durationSuffix));
		}

		return CardEditorLoc.F(
			"cardText.cardTypeStarCostDelta",
			$"{affected} cost {amountText} {lessOrMore}.",
			("Affected", affected),
			("Amount", amountText),
			("LessOrMore", lessOrMore),
			("Stars", string.Empty));
	}

	private static string FormatCardTypeStarCostDeltaText(CardExtraEffect effect, string amountText, bool isLess, string? durationSuffix, bool forceUpgradeHighlight)
	{
		string affected = BuildAffectedCardTypeText(effect);
		if (forceUpgradeHighlight)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, 1);
		}

		string lessOrMore = isLess
			? CardEditorLoc.T("cardText.costDelta.less", "less")
			: CardEditorLoc.T("cardText.costDelta.more", "more");

		if (durationSuffix != null)
		{
			return CardEditorLoc.F(
				"cardText.cardTypeStarCostDelta.duration",
				$"{affected} cost {amountText} {lessOrMore} {durationSuffix}.",
				("Affected", affected),
				("Amount", amountText),
				("LessOrMore", lessOrMore),
				("Stars", string.Empty),
				("Duration", durationSuffix));
		}

		return CardEditorLoc.F(
			"cardText.cardTypeStarCostDelta",
			$"{affected} cost {amountText} {lessOrMore}.",
			("Affected", affected),
			("Amount", amountText),
			("LessOrMore", lessOrMore),
			("Stars", string.Empty));
	}

	private static string BuildDrawnFromPileSuffix(CardExtraEffect effect)
	{
		if (effect.DrawnFromPile == CardExtraEffectCardPile.AllPiles || effect.DrawnFromPile == CardExtraEffectCardPile.Hand)
		{
			return string.Empty;
		}

		string pile = CardPileLabel(effect.DrawnFromPile);
		return " " + CardEditorLoc.F("cardText.drawnFromPile", $"from your {pile}", ("Pile", pile));
	}

	private static string FormatDrawnCardsCostDelta(CardModel card, CardExtraEffect effect, int signedAmount, string? durationSuffix, bool forceUpgradeHighlight)
	{
		string affected = BuildAffectedCardTypeText(effect);
		string fromPile = BuildDrawnFromPileSuffix(effect);

		int magnitude = Math.Abs(signedAmount);
		string amountText = BuildEnergyIcons(card, magnitude);
		if (forceUpgradeHighlight)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, 1);
		}

		bool isLess = signedAmount > 0;
		string lessOrMore = isLess
			? CardEditorLoc.T("cardText.costDelta.less", "less")
			: CardEditorLoc.T("cardText.costDelta.more", "more");

		string drawn = CardEditorLoc.T("cardText.drawn", "drawn");

		if (durationSuffix != null)
		{
			return CardEditorLoc.F(
				"cardText.drawnCardsCostDelta.duration",
				$"{affected} {drawn}{fromPile} cost {amountText} {lessOrMore} {durationSuffix}.",
				("Affected", affected),
				("Drawn", drawn),
				("FromPile", fromPile),
				("Amount", amountText),
				("LessOrMore", lessOrMore),
				("Duration", durationSuffix));
		}

		return CardEditorLoc.F(
			"cardText.drawnCardsCostDelta",
			$"{affected} {drawn}{fromPile} cost {amountText} {lessOrMore}.",
			("Affected", affected),
			("Drawn", drawn),
			("FromPile", fromPile),
			("Amount", amountText),
			("LessOrMore", lessOrMore));
	}

	private static string FormatDrawnCardsCostDeltaText(CardExtraEffect effect, string amountText, bool isLess, string? durationSuffix, bool forceUpgradeHighlight)
	{
		string affected = BuildAffectedCardTypeText(effect);
		string fromPile = BuildDrawnFromPileSuffix(effect);
		if (forceUpgradeHighlight)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, 1);
		}

		string lessOrMore = isLess
			? CardEditorLoc.T("cardText.costDelta.less", "less")
			: CardEditorLoc.T("cardText.costDelta.more", "more");

		string drawn = CardEditorLoc.T("cardText.drawn", "drawn");

		if (durationSuffix != null)
		{
			return CardEditorLoc.F(
				"cardText.drawnCardsCostDelta.duration",
				$"{affected} {drawn}{fromPile} cost {amountText} {lessOrMore} {durationSuffix}.",
				("Affected", affected),
				("Drawn", drawn),
				("FromPile", fromPile),
				("Amount", amountText),
				("LessOrMore", lessOrMore),
				("Duration", durationSuffix));
		}

		return CardEditorLoc.F(
			"cardText.drawnCardsCostDelta",
			$"{affected} {drawn}{fromPile} cost {amountText} {lessOrMore}.",
			("Affected", affected),
			("Drawn", drawn),
			("FromPile", fromPile),
			("Amount", amountText),
			("LessOrMore", lessOrMore));
	}

	private static string FormatGeneratedCardsCostDelta(CardModel card, CardExtraEffect effect, int signedAmount, string? durationSuffix, bool forceUpgradeHighlight)
	{
		string affected = BuildAffectedCardTypeText(effect);

		int magnitude = Math.Abs(signedAmount);
		string amountText = BuildEnergyIcons(card, magnitude);
		if (forceUpgradeHighlight)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, 1);
		}

		bool isLess = signedAmount > 0;
		string lessOrMore = isLess
			? CardEditorLoc.T("cardText.costDelta.less", "less")
			: CardEditorLoc.T("cardText.costDelta.more", "more");

		string created = CardEditorLoc.T("cardText.created", "created");

		if (durationSuffix != null)
		{
			return CardEditorLoc.F(
				"cardText.generatedCardsCostDelta.duration",
				$"{affected} {created} cost {amountText} {lessOrMore} {durationSuffix}.",
				("Affected", affected),
				("Created", created),
				("Amount", amountText),
				("LessOrMore", lessOrMore),
				("Duration", durationSuffix));
		}

		return CardEditorLoc.F(
			"cardText.generatedCardsCostDelta",
			$"{affected} {created} cost {amountText} {lessOrMore}.",
			("Affected", affected),
			("Created", created),
			("Amount", amountText),
			("LessOrMore", lessOrMore));
	}

	private static string FormatGeneratedCardsCostDeltaText(CardExtraEffect effect, string amountText, bool isLess, string? durationSuffix, bool forceUpgradeHighlight)
	{
		string affected = BuildAffectedCardTypeText(effect);
		if (forceUpgradeHighlight)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, 1);
		}

		string lessOrMore = isLess
			? CardEditorLoc.T("cardText.costDelta.less", "less")
			: CardEditorLoc.T("cardText.costDelta.more", "more");

		string created = CardEditorLoc.T("cardText.created", "created");

		if (durationSuffix != null)
		{
			return CardEditorLoc.F(
				"cardText.generatedCardsCostDelta.duration",
				$"{affected} {created} cost {amountText} {lessOrMore} {durationSuffix}.",
				("Affected", affected),
				("Created", created),
				("Amount", amountText),
				("LessOrMore", lessOrMore),
				("Duration", durationSuffix));
		}

		return CardEditorLoc.F(
			"cardText.generatedCardsCostDelta",
			$"{affected} {created} cost {amountText} {lessOrMore}.",
			("Affected", affected),
			("Created", created),
			("Amount", amountText),
			("LessOrMore", lessOrMore));
	}

	private static string GetCardPilePossessive(CardExtraEffectCardPile pile)
	{
		string label = CardPileLabel(pile);
		return CardEditorLoc.F("cardText.pile.your", $"your {label}", ("Pile", label));
	}

	private static string GetCardPileDestination(CardExtraEffectCardPile pile, CardExtraEffectCardPilePosition position)
	{
		if (pile == CardExtraEffectCardPile.DrawPile)
		{
			string pileLabel = CardPileLabel(pile);
			return position switch
			{
				CardExtraEffectCardPilePosition.Top => CardEditorLoc.F("cardText.pile.topOf", $"the top of your {pileLabel}", ("Pile", pileLabel)),
				CardExtraEffectCardPilePosition.Bottom => CardEditorLoc.F("cardText.pile.bottomOf", $"the bottom of your {pileLabel}", ("Pile", pileLabel)),
				_ => GetCardPilePossessive(pile)
			};
		}

		return GetCardPilePossessive(pile);
	}

	private static string BuildGeneratedCardFilter(CardExtraEffect effect, bool plural)
	{
		string poolAdj = effect.GeneratedCardPool switch
		{
			CardGeneratedCardPool.Colorless => GeneratedCardPoolLabel(CardGeneratedCardPool.Colorless) + " ",
			CardGeneratedCardPool.Ironclad => GeneratedCardPoolLabel(CardGeneratedCardPool.Ironclad) + " ",
			CardGeneratedCardPool.Silent => GeneratedCardPoolLabel(CardGeneratedCardPool.Silent) + " ",
			CardGeneratedCardPool.Defect => GeneratedCardPoolLabel(CardGeneratedCardPool.Defect) + " ",
			CardGeneratedCardPool.Regent => GeneratedCardPoolLabel(CardGeneratedCardPool.Regent) + " ",
			CardGeneratedCardPool.Necrobinder => GeneratedCardPoolLabel(CardGeneratedCardPool.Necrobinder) + " ",
			CardGeneratedCardPool.Ancient => GeneratedCardPoolLabel(CardGeneratedCardPool.Ancient) + " ",
			_ => string.Empty
		};

		string typeAdj = effect.GeneratedCardType switch
		{
			CardGeneratedCardType.Attack => GeneratedCardTypeLabel(CardGeneratedCardType.Attack) + " ",
			CardGeneratedCardType.Skill => GeneratedCardTypeLabel(CardGeneratedCardType.Skill) + " ",
			CardGeneratedCardType.Power => GeneratedCardTypeLabel(CardGeneratedCardType.Power) + " ",
			CardGeneratedCardType.Playable => CardEditorLoc.T("cardText.value.playable", "playable") + " ",
			CardGeneratedCardType.Status => GeneratedCardTypeLabel(CardGeneratedCardType.Status) + " ",
			CardGeneratedCardType.Curse => GeneratedCardTypeLabel(CardGeneratedCardType.Curse) + " ",
			CardGeneratedCardType.Quest => GeneratedCardTypeLabel(CardGeneratedCardType.Quest) + " ",
			_ => string.Empty
		};

		string prefix = (poolAdj + typeAdj).Trim();
		return string.IsNullOrEmpty(prefix) ? string.Empty : prefix + " ";
	}

	private static string BuildGeneratedCardPoolSuffix(CardGeneratedCardPool pool)
	{
		return pool switch
		{
			CardGeneratedCardPool.OtherColors => CardEditorLoc.T("cardText.poolSuffix.otherColors", " from other characters"),
			CardGeneratedCardPool.Any => CardEditorLoc.T("cardText.poolSuffix.allColors", " from any character"),
			CardGeneratedCardPool.All => CardEditorLoc.T("cardText.poolSuffix.allPools", " from all pools"),
			_ => string.Empty
		};
	}

	private static string BuildCountScalingPoolSuffix(CardGeneratedCardPool pool)
	{
		// For non-generation text, generic "cards" already means literally all cards.
		// Keep the explicit suffix for "any character" because it excludes Colorless/Ancient.
		if (pool == CardGeneratedCardPool.All)
		{
			return string.Empty;
		}

		return pool switch
		{
			CardGeneratedCardPool.Default => CardEditorLoc.T("cardText.poolSuffix.yourColor", " of your color"),
			CardGeneratedCardPool.OtherColors => CardEditorLoc.T("cardText.poolSuffix.otherColors", " from other characters"),
			CardGeneratedCardPool.Any => CardEditorLoc.T("cardText.poolSuffix.allColors", " from any character"),
			_ => string.Empty
		};
	}

	private static string GetCardPileLocation(CardExtraEffectCardPile pile)
	{
		return pile switch
		{
			CardExtraEffectCardPile.Hand => CardEditorLoc.T("cardText.pile.inHand", "in your hand"),
			CardExtraEffectCardPile.DrawPile => CardEditorLoc.T("cardText.pile.inDrawPile", "in your draw pile"),
			CardExtraEffectCardPile.DiscardPile => CardEditorLoc.T("cardText.pile.inDiscardPile", "in your discard pile"),
			CardExtraEffectCardPile.ExhaustPile => CardEditorLoc.T("cardText.pile.inExhaustPile", "in your exhaust pile"),
			CardExtraEffectCardPile.AllPiles => CardEditorLoc.T("cardText.pile.inAnyPile", "in any pile"),
			CardExtraEffectCardPile.Deck => CardEditorLoc.T("cardText.pile.inDeck", "in your deck"),
			_ => CardEditorLoc.T("cardText.pile.inHand", "in your hand")
		};
	}

	private static void TryGetScaledAmountText(CardModel card, CardExtraEffect effect, int baseAmount, Creature? target, bool forceUpgradeHighlight, out string amountText)
	{
		amountText = baseAmount.ToString(CultureInfo.InvariantCulture);

		try
		{
			Player? owner = card.Owner;
			Creature? dealer = owner?.Creature;
			CombatState? combatState = card.CombatState;

			bool isOstyAttack = effect.Kind == CardExtraEffectKind.OstyAction
				&& effect.OstyAction is CardExtraEffectOstyAction.Attack or CardExtraEffectOstyAction.AttackAll;

			ValueProp props = ValueProp.Move;

			decimal enchanted = baseAmount;
			if (card.Enchantment != null)
			{
				if (effect.Kind == CardExtraEffectKind.DealDamage)
				{
					enchanted += card.Enchantment.EnchantDamageAdditive(enchanted, props);
					enchanted *= card.Enchantment.EnchantDamageMultiplicative(enchanted, props);
				}
				else if (effect.Kind == CardExtraEffectKind.GainBlock)
				{
					enchanted += card.Enchantment.EnchantBlockAdditive(enchanted, props);
					enchanted *= card.Enchantment.EnchantBlockMultiplicative(enchanted, props);
				}
			}

			decimal preview = enchanted;
			if (ShouldRunGlobalHooks(card) && dealer != null && combatState != null && owner?.RunState != null)
			{
				if (effect.Kind == CardExtraEffectKind.DealDamage || isOstyAttack)
				{
					Creature? effectiveDealer = isOstyAttack ? owner.Osty : dealer;
					Creature? damageTarget = effect.Target switch
					{
						CardExtraEffectTarget.Self => isOstyAttack ? owner.Osty : dealer,
						CardExtraEffectTarget.Target => target,
						_ => null
					};
					CardPreviewMode previewMode = (effect.Target == CardExtraEffectTarget.AllEnemies || effect.OstyAction == CardExtraEffectOstyAction.AttackAll)
						? CardPreviewMode.MultiCreatureTargeting
						: CardPreviewMode.Normal;

					if (effectiveDealer != null)
					{
						preview = Hook.ModifyDamage(owner.RunState, combatState, damageTarget, effectiveDealer, baseAmount, props, card, ModifyDamageHookType.All, previewMode, out IEnumerable<AbstractModel> _);
					}
				}
				else if (effect.Kind == CardExtraEffectKind.GainBlock)
				{
					Creature? blockTarget = effect.Target switch
					{
						CardExtraEffectTarget.Self => dealer,
						CardExtraEffectTarget.Target => target,
						_ => null
					};

					if (blockTarget != null)
					{
						preview = Hook.ModifyBlock(combatState, blockTarget, baseAmount, props, card, cardPlay: null, out IEnumerable<AbstractModel> _);
					}
				}
			}

			int baseInt = (int)enchanted;
			int previewInt = (int)preview;
			int comparison = forceUpgradeHighlight ? 1 : previewInt.CompareTo(baseInt);
			amountText = StsTextUtilities.HighlightChangeText(previewInt.ToString(CultureInfo.InvariantCulture), comparison);
		}
		catch
		{
			amountText = baseAmount.ToString(CultureInfo.InvariantCulture);
		}
	}

	private static bool ShouldRunGlobalHooks(CardModel card)
	{
		if (card == null || card.CombatState == null)
		{
			return false;
		}

		PileType? pileType = card.Pile?.Type;
		if (pileType == PileType.Hand || pileType == PileType.Play)
		{
			return true;
		}

		return card.UpgradePreviewType == CardUpgradePreviewType.Combat;
	}

	private static string ApplyTiming(string line, CardExtraEffectTiming timing, int turns)
	{
		if (timing == CardExtraEffectTiming.Immediate || turns < 0)
		{
			return line;
		}

		string prefix = timing switch
		{
			CardExtraEffectTiming.StartOfTurn => turns == 0
				? CardEditorLoc.T("cardText.timing.start.eachTurnCombatPrefix", "Start of each turn this combat: ")
				: (turns == 1
					? CardEditorLoc.T("cardText.timing.start.nextTurnPrefix", "Start of next turn: ")
					: CardEditorLoc.F("cardText.timing.start.nextTurnsPrefix", $"Start of the next {turns.ToString(CultureInfo.InvariantCulture)} turns: ", ("Turns", turns))),
			CardExtraEffectTiming.EndOfTurn => turns == 0
				? CardEditorLoc.T("cardText.timing.end.startingNextTurnEachCombatPrefix", "Starting next turn, end of each turn this combat: ")
				: (turns == 1
					? CardEditorLoc.T("cardText.timing.end.nextTurnPrefix", "End of next turn: ")
					: CardEditorLoc.F("cardText.timing.end.nextTurnsPrefix", $"End of the next {turns.ToString(CultureInfo.InvariantCulture)} turns: ", ("Turns", turns))),
			CardExtraEffectTiming.EndOfThisTurn => turns == 0
				? CardEditorLoc.T("cardText.timing.endEachCombatPrefix", "End of each turn this combat: ")
				: (turns == 1
					? CardEditorLoc.T("cardText.timing.endThisTurnPrefix", "End of this turn: ")
					: (turns == 2
						? CardEditorLoc.T("cardText.timing.endThisTurnAndNextPrefix", "End of this turn and next turn: ")
						: CardEditorLoc.F("cardText.timing.endThisTurnAndNextTurnsPrefix", $"End of this turn and the next {(turns - 1).ToString(CultureInfo.InvariantCulture)} turns: ", ("Turns", turns - 1)))),
			CardExtraEffectTiming.StartOfEnemyTurn => turns == 0
				? CardEditorLoc.T("cardText.timing.enemyStart.eachTurnCombatPrefix", "Start of each enemy turn this combat: ")
				: (turns == 1
					? CardEditorLoc.T("cardText.timing.enemyStart.nextTurnPrefix", "Start of next enemy turn: ")
					: CardEditorLoc.F("cardText.timing.enemyStart.nextTurnsPrefix", $"Start of the next {turns.ToString(CultureInfo.InvariantCulture)} enemy turns: ", ("Turns", turns))),
			CardExtraEffectTiming.EndOfEnemyTurn => turns == 0
				? CardEditorLoc.T("cardText.timing.enemyEnd.startingNextTurnEachCombatPrefix", "Starting next enemy turn, end of each enemy turn this combat: ")
				: (turns == 1
					? CardEditorLoc.T("cardText.timing.enemyEnd.nextTurnPrefix", "End of next enemy turn: ")
					: CardEditorLoc.F("cardText.timing.enemyEnd.nextTurnsPrefix", $"End of the next {turns.ToString(CultureInfo.InvariantCulture)} enemy turns: ", ("Turns", turns))),
			CardExtraEffectTiming.StartOfAnyTurn => turns == 0
				? CardEditorLoc.T("cardText.timing.anyStart.eachTurnCombatPrefix", "Start of each turn this combat: ")
				: (turns == 1
					? CardEditorLoc.T("cardText.timing.anyStart.nextTurnPrefix", "Start of next turn: ")
					: CardEditorLoc.F("cardText.timing.anyStart.nextTurnsPrefix", $"Start of the next {turns.ToString(CultureInfo.InvariantCulture)} turns: ", ("Turns", turns))),
			CardExtraEffectTiming.EndOfAnyTurn => turns == 0
				? CardEditorLoc.T("cardText.timing.anyEnd.startingNextTurnEachCombatPrefix", "Starting next turn, end of each turn this combat: ")
				: (turns == 1
					? CardEditorLoc.T("cardText.timing.anyEnd.nextTurnPrefix", "End of next turn: ")
					: CardEditorLoc.F("cardText.timing.anyEnd.nextTurnsPrefix", $"End of the next {turns.ToString(CultureInfo.InvariantCulture)} turns: ", ("Turns", turns))),
			CardExtraEffectTiming.EndOfThisAnyTurn => turns == 0
				? CardEditorLoc.T("cardText.timing.anyEndEachCombatPrefix", "End of each turn this combat: ")
				: (turns == 1
					? CardEditorLoc.T("cardText.timing.anyEndThisTurnPrefix", "End of this turn: ")
					: (turns == 2
						? CardEditorLoc.T("cardText.timing.anyEndThisTurnAndNextPrefix", "End of this turn and next turn: ")
						: CardEditorLoc.F("cardText.timing.anyEndThisTurnAndNextTurnsPrefix", $"End of this turn and the next {(turns - 1).ToString(CultureInfo.InvariantCulture)} turns: ", ("Turns", turns - 1)))),
			_ => string.Empty
		};

		return string.IsNullOrEmpty(prefix) ? line : prefix + line;
	}

	private static string BuildEnergyIcons(CardModel card, int amount)
	{
		string icon = BuildEnergyIcon(card);
		if (amount <= 0 || amount >= 4)
		{
			return amount.ToString(CultureInfo.InvariantCulture) + icon;
		}
		return string.Concat(Enumerable.Repeat(icon, amount));
	}

	private static string BuildStarIcons(int amount)
	{
		if (amount <= 0)
		{
			return "0 " + StarIconsFormatter.starIconSprite;
		}
		if (amount == 1)
		{
			return StarIconsFormatter.starIconSprite;
		}
		StringBuilder sb = new StringBuilder(StarIconsFormatter.starIconSprite.Length * amount);
		for (int i = 0; i < amount; i++)
		{
			sb.Append(StarIconsFormatter.starIconSprite);
		}
		return sb.ToString();
	}

	private static string PowerTitle(string powerName, string fallback)
	{
		if (string.IsNullOrWhiteSpace(powerName))
		{
			return fallback;
		}

		try
		{
			string key = powerName.Trim().ToUpperInvariant() + "_POWER.title";
			if (LocString.Exists("powers", key))
			{
				string? title = new LocString("powers", key).GetFormattedText();
				if (!string.IsNullOrWhiteSpace(title))
				{
					return title!;
				}
			}
		}
		catch
		{
		}

		return fallback;
	}

	private static string OrbTitle(string orbName, string fallback)
	{
		if (string.IsNullOrWhiteSpace(orbName))
		{
			return fallback;
		}

		try
		{
			string key = orbName.Trim().ToUpperInvariant() + "_ORB.title";
			if (LocString.Exists("orbs", key))
			{
				string? title = new LocString("orbs", key).GetFormattedText();
				if (!string.IsNullOrWhiteSpace(title))
				{
					return title!;
				}
			}
		}
		catch
		{
		}

		return fallback;
	}

	private static string GetOrbTypeTitle(CardExtraEffectOrbType type)
	{
		return type switch
		{
			CardExtraEffectOrbType.Lightning => OrbTitle("Lightning", "Lightning"),
			CardExtraEffectOrbType.Frost => OrbTitle("Frost", "Frost"),
			CardExtraEffectOrbType.Dark => OrbTitle("Dark", "Dark"),
			CardExtraEffectOrbType.Plasma => OrbTitle("Plasma", "Plasma"),
			CardExtraEffectOrbType.Glass => OrbTitle("Glass", "Glass"),
			_ => string.Empty
		};
	}

	internal static bool IsLegacyOrbChannelKind(CardExtraEffectKind kind)
	{
		return kind is CardExtraEffectKind.ChannelLightning
			or CardExtraEffectKind.ChannelFrost
			or CardExtraEffectKind.ChannelDark
			or CardExtraEffectKind.ChannelPlasma
			or CardExtraEffectKind.ChannelGlass
			or CardExtraEffectKind.ChannelRandomOrb;
	}

	internal static CardExtraEffect? NormalizeLegacyOrbChannelEffect(CardExtraEffect? effect)
	{
		if (effect == null || !IsLegacyOrbChannelKind(effect.Kind))
		{
			return effect;
		}

		CardExtraEffect normalized = CloneEffect(effect);
		normalized.Kind = CardExtraEffectKind.OrbAction;
		normalized.OrbAction = CardExtraEffectOrbAction.Channel;
		normalized.OrbSelection = CardExtraEffectOrbSelection.Leftmost;
		normalized.OrbFollowUp = CardExtraEffectOrbFollowUp.None;
		normalized.OrbType = effect.Kind switch
		{
			CardExtraEffectKind.ChannelLightning => CardExtraEffectOrbType.Lightning,
			CardExtraEffectKind.ChannelFrost => CardExtraEffectOrbType.Frost,
			CardExtraEffectKind.ChannelDark => CardExtraEffectOrbType.Dark,
			CardExtraEffectKind.ChannelPlasma => CardExtraEffectOrbType.Plasma,
			CardExtraEffectKind.ChannelGlass => CardExtraEffectOrbType.Glass,
			_ => CardExtraEffectOrbType.Any
		};
		return normalized;
	}

	internal static bool IsLegacyEvokeOrbsKind(CardExtraEffectKind kind)
	{
		return kind == CardExtraEffectKind.EvokeOrbs;
	}

	internal static CardExtraEffect? NormalizeLegacyEvokeOrbsEffect(CardExtraEffect? effect)
	{
		if (effect == null || !IsLegacyEvokeOrbsKind(effect.Kind))
		{
			return effect;
		}

		CardExtraEffect normalized = CloneEffect(effect);
		normalized.Kind = CardExtraEffectKind.OrbAction;
		normalized.OrbAction = CardExtraEffectOrbAction.Evoke;
		normalized.OrbType = CardExtraEffectOrbType.Any;
		normalized.OrbSelection = CardExtraEffectOrbSelection.Leftmost;
		normalized.OrbFollowUp = CardExtraEffectOrbFollowUp.None;
		return normalized;
	}

	internal static bool IsLegacySelfPileAutoKind(CardExtraEffectKind kind)
	{
		return kind is CardExtraEffectKind.AutoPlaySelfFromPile or CardExtraEffectKind.AutoDrawSelfFromPile;
	}

	internal static CardExtraEffect? NormalizeSelfPileAutoEffect(CardExtraEffect? effect)
	{
		if (effect == null)
		{
			return null;
		}

		if (effect.Kind is not CardExtraEffectKind.AutoPlaySelfFromPile
			and not CardExtraEffectKind.AutoDrawSelfFromPile
			and not CardExtraEffectKind.ConditionalAutoPlayFromPile
			and not CardExtraEffectKind.ConditionalAutoDrawFromPile)
		{
			return effect;
		}

		CardExtraEffect normalized = CloneEffect(effect);
		if (effect.Kind == CardExtraEffectKind.AutoPlaySelfFromPile)
		{
			normalized.Kind = CardExtraEffectKind.ConditionalAutoPlayFromPile;
			normalized.ScaleMode = CardExtraEffectScaleMode.None;
			normalized.CountComparison = CardExtraEffectCountComparison.None;
			normalized.CountConditionAmount = 0;
			normalized.Amount = 1;
			normalized.AmountIsX = false;
			normalized.AmountXPlus = 0;
			return normalized;
		}

		if (effect.Kind == CardExtraEffectKind.AutoDrawSelfFromPile)
		{
			normalized.Kind = CardExtraEffectKind.ConditionalAutoDrawFromPile;
			normalized.ScaleMode = CardExtraEffectScaleMode.None;
			normalized.CountComparison = CardExtraEffectCountComparison.None;
			normalized.CountConditionAmount = 0;
			normalized.Amount = 1;
			normalized.AmountIsX = false;
			normalized.AmountXPlus = 0;
			return normalized;
		}

		if (normalized.ScaleMode != CardExtraEffectScaleMode.None
			&& normalized.CountComparison == CardExtraEffectCountComparison.None
			&& normalized.CountConditionAmount <= 0)
		{
			normalized.ScaleMode = CardExtraEffectScaleMode.ConditionOnly;
			normalized.CountComparison = CardExtraEffectCountComparison.AtLeast;
			normalized.CountConditionAmount = Math.Max(1, normalized.Amount);
			normalized.Amount = 1;
			normalized.AmountIsX = false;
			normalized.AmountXPlus = 0;
		}

		return normalized;
	}

	private static string BuildSuffixPart(string? suffix)
	{
		string period = CardEditorLoc.T("cardText.punctuation.period", ".");

		if (string.IsNullOrWhiteSpace(suffix))
		{
			return CardEditorLoc.F("cardText.suffix.noSuffix", period, ("Period", period));
		}

		return CardEditorLoc.F("cardText.suffix.withPeriod", $" {suffix}{period}", ("Suffix", suffix), ("Period", period));
	}

	private static string FormatDealDamage(CardExtraEffectTarget target, string amountText)
	{
		return target switch
		{
			CardExtraEffectTarget.AllEnemies => CardEditorLoc.F("cardText.dealDamage.allEnemies", $"Deal {amountText} damage to ALL enemies.", ("Amount", amountText)),
			CardExtraEffectTarget.RandomEnemy => CardEditorLoc.F("cardText.dealDamage.randomEnemy", $"Deal {amountText} damage to a random enemy.", ("Amount", amountText)),
			CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.dealDamage.self", $"Deal {amountText} damage to yourself.", ("Amount", amountText)),
			_ => CardEditorLoc.F("cardText.dealDamage.target", $"Deal {amountText} damage.", ("Amount", amountText))
		};
	}

	private static string FormatGainBlock(CardExtraEffectTarget target, string amountText)
	{
		return target switch
		{
			CardExtraEffectTarget.AllEnemies => CardEditorLoc.F("cardText.gainBlock.allEnemies", $"ALL enemies gain {amountText} [gold]Block[/gold].", ("Amount", amountText)),
			CardExtraEffectTarget.RandomEnemy => CardEditorLoc.F("cardText.gainBlock.randomEnemy", $"A random enemy gains {amountText} [gold]Block[/gold].", ("Amount", amountText)),
			CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.gainBlock.self", $"Gain {amountText} [gold]Block[/gold].", ("Amount", amountText)),
			_ => CardEditorLoc.F("cardText.gainBlock.target", $"Enemy gains {amountText} [gold]Block[/gold].", ("Amount", amountText))
		};
	}

	private static string FormatApplyDebuff(CardExtraEffectTarget target, int amount, string debuffName, string? suffix = null)
	{
		string title = PowerTitle(debuffName, debuffName);
		string payload = $"{amount.ToString(CultureInfo.InvariantCulture)} [gold]{title}[/gold]";
		string suffixPart = BuildSuffixPart(suffix);
		return target switch
		{
			CardExtraEffectTarget.AllEnemies => CardEditorLoc.F("cardText.applyDebuff.allEnemies", $"Apply {payload} to ALL enemies{suffixPart}", ("Payload", payload), ("Suffix", suffixPart)),
			CardExtraEffectTarget.RandomEnemy => CardEditorLoc.F("cardText.applyDebuff.randomEnemy", $"Apply {payload} to a random enemy{suffixPart}", ("Payload", payload), ("Suffix", suffixPart)),
			CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.applyDebuff.self", $"Apply {payload} to yourself{suffixPart}", ("Payload", payload), ("Suffix", suffixPart)),
			_ => CardEditorLoc.F("cardText.applyDebuff.target", $"Apply {payload}{suffixPart}", ("Payload", payload), ("Suffix", suffixPart))
		};
	}

	private static string FormatApplyDebuffText(CardExtraEffectTarget target, string amountText, string debuffName, string? suffix = null)
	{
		string title = PowerTitle(debuffName, debuffName);
		string payload = $"{amountText} [gold]{title}[/gold]";
		string suffixPart = BuildSuffixPart(suffix);
		return target switch
		{
			CardExtraEffectTarget.AllEnemies => CardEditorLoc.F("cardText.applyDebuff.allEnemies", $"Apply {payload} to ALL enemies{suffixPart}", ("Payload", payload), ("Suffix", suffixPart)),
			CardExtraEffectTarget.RandomEnemy => CardEditorLoc.F("cardText.applyDebuff.randomEnemy", $"Apply {payload} to a random enemy{suffixPart}", ("Payload", payload), ("Suffix", suffixPart)),
			CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.applyDebuff.self", $"Apply {payload} to yourself{suffixPart}", ("Payload", payload), ("Suffix", suffixPart)),
			_ => CardEditorLoc.F("cardText.applyDebuff.target", $"Apply {payload}{suffixPart}", ("Payload", payload), ("Suffix", suffixPart))
		};
	}

	private static string FormatSignedPowerText(CardExtraEffectTarget target, bool gain, string amountText, string powerName, string? suffix = null)
	{
		string wordSelf = gain
			? CardEditorLoc.T("cardText.word.gainSelf", "Gain")
			: CardEditorLoc.T("cardText.word.loseSelf", "Lose");
		string verbPlural = gain
			? CardEditorLoc.T("cardText.word.gain", "gain")
			: CardEditorLoc.T("cardText.word.lose", "lose");
		string verbSingular = gain
			? CardEditorLoc.T("cardText.word.gains", "gains")
			: CardEditorLoc.T("cardText.word.loses", "loses");

		string title = PowerTitle(powerName, powerName);
		string payload = $"{amountText} [gold]{title}[/gold]";
		string suffixPart = BuildSuffixPart(suffix);

		return target switch
		{
			CardExtraEffectTarget.AllEnemies => CardEditorLoc.F("cardText.signedPower.allEnemies", $"ALL enemies {verbPlural} {payload}{suffixPart}", ("Verb", verbPlural), ("Payload", payload), ("Suffix", suffixPart)),
			CardExtraEffectTarget.RandomEnemy => CardEditorLoc.F("cardText.signedPower.randomEnemy", $"A random enemy {verbSingular} {payload}{suffixPart}", ("Verb", verbSingular), ("Payload", payload), ("Suffix", suffixPart)),
			CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.signedPower.self", $"{wordSelf} {payload}{suffixPart}", ("Verb", wordSelf), ("Payload", payload), ("Suffix", suffixPart)),
			_ => CardEditorLoc.F("cardText.signedPower.target", $"Enemy {verbSingular} {payload}{suffixPart}", ("Verb", verbSingular), ("Payload", payload), ("Suffix", suffixPart))
		};
	}

	private static string FormatSignedPower(CardExtraEffectTarget target, int signedAmount, string powerName, string? suffix = null)
	{
		int abs = Math.Abs(signedAmount);
		bool gain = signedAmount >= 0;
		string wordSelf = gain
			? CardEditorLoc.T("cardText.word.gainSelf", "Gain")
			: CardEditorLoc.T("cardText.word.loseSelf", "Lose");
		string verbPlural = gain
			? CardEditorLoc.T("cardText.word.gain", "gain")
			: CardEditorLoc.T("cardText.word.lose", "lose");
		string verbSingular = gain
			? CardEditorLoc.T("cardText.word.gains", "gains")
			: CardEditorLoc.T("cardText.word.loses", "loses");

		string title = PowerTitle(powerName, powerName);
		string payload = $"{abs.ToString(CultureInfo.InvariantCulture)} [gold]{title}[/gold]";
		string suffixPart = BuildSuffixPart(suffix);

		return target switch
		{
			CardExtraEffectTarget.AllEnemies => CardEditorLoc.F("cardText.signedPower.allEnemies", $"ALL enemies {verbPlural} {payload}{suffixPart}", ("Verb", verbPlural), ("Payload", payload), ("Suffix", suffixPart)),
			CardExtraEffectTarget.RandomEnemy => CardEditorLoc.F("cardText.signedPower.randomEnemy", $"A random enemy {verbSingular} {payload}{suffixPart}", ("Verb", verbSingular), ("Payload", payload), ("Suffix", suffixPart)),
			CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.signedPower.self", $"{wordSelf} {payload}{suffixPart}", ("Verb", wordSelf), ("Payload", payload), ("Suffix", suffixPart)),
			_ => CardEditorLoc.F("cardText.signedPower.target", $"Enemy {verbSingular} {payload}{suffixPart}", ("Verb", verbSingular), ("Payload", payload), ("Suffix", suffixPart))
		};
	}

	private static string FormatGainPower(CardExtraEffectTarget target, string amountText, string powerName, string? suffix = null)
	{
		string title = PowerTitle(powerName, powerName);
		string payload = $"{amountText} [gold]{title}[/gold]";
		string suffixPart = BuildSuffixPart(suffix);

		return target switch
		{
			CardExtraEffectTarget.AllEnemies => CardEditorLoc.F("cardText.gainPower.allEnemies", $"ALL enemies gain {payload}{suffixPart}", ("Payload", payload), ("Suffix", suffixPart)),
			CardExtraEffectTarget.RandomEnemy => CardEditorLoc.F("cardText.gainPower.randomEnemy", $"A random enemy gains {payload}{suffixPart}", ("Payload", payload), ("Suffix", suffixPart)),
			CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.gainPower.self", $"Gain {payload}{suffixPart}", ("Payload", payload), ("Suffix", suffixPart)),
			_ => CardEditorLoc.F("cardText.gainPower.target", $"Enemy gains {payload}{suffixPart}", ("Payload", payload), ("Suffix", suffixPart))
		};
	}

	private static string GetMultiplierStatRichText(CardExtraEffectMultiplierStat stat)
	{
		return stat switch
		{
			CardExtraEffectMultiplierStat.Block => "[gold]Block[/gold]",
			CardExtraEffectMultiplierStat.Strength => $"[gold]{PowerTitle("Strength", "Strength")}[/gold]",
			CardExtraEffectMultiplierStat.Dexterity => $"[gold]{PowerTitle("Dexterity", "Dexterity")}[/gold]",
			CardExtraEffectMultiplierStat.Focus => $"[gold]{PowerTitle("Focus", "Focus")}[/gold]",
			CardExtraEffectMultiplierStat.Weak => $"[gold]{PowerTitle("Weak", "Weak")}[/gold]",
			CardExtraEffectMultiplierStat.Frail => $"[gold]{PowerTitle("Frail", "Frail")}[/gold]",
			CardExtraEffectMultiplierStat.Vulnerable => $"[gold]{PowerTitle("Vulnerable", "Vulnerable")}[/gold]",
			CardExtraEffectMultiplierStat.Poison => $"[gold]{PowerTitle("Poison", "Poison")}[/gold]",
			CardExtraEffectMultiplierStat.Doom => $"[gold]{PowerTitle("Doom", "Doom")}[/gold]",
			CardExtraEffectMultiplierStat.Constrict => $"[gold]{PowerTitle("Constrict", "Constrict")}[/gold]",
			CardExtraEffectMultiplierStat.Artifact => $"[gold]{PowerTitle("Artifact", "Artifact")}[/gold]",
			CardExtraEffectMultiplierStat.Thorns => $"[gold]{PowerTitle("Thorns", "Thorns")}[/gold]",
			CardExtraEffectMultiplierStat.Regen => $"[gold]{PowerTitle("Regen", "Regen")}[/gold]",
			CardExtraEffectMultiplierStat.Plating => $"[gold]{PowerTitle("Plating", "Plating")}[/gold]",
			CardExtraEffectMultiplierStat.Intangible => $"[gold]{PowerTitle("Intangible", "Intangible")}[/gold]",
			CardExtraEffectMultiplierStat.Buffer => $"[gold]{PowerTitle("Buffer", "Buffer")}[/gold]",
			CardExtraEffectMultiplierStat.Vigor => $"[gold]{PowerTitle("Vigor", "Vigor")}[/gold]",
			CardExtraEffectMultiplierStat.Blur => $"[gold]{PowerTitle("Blur", "Blur")}[/gold]",
			CardExtraEffectMultiplierStat.Ritual => $"[gold]{PowerTitle("Ritual", "Ritual")}[/gold]",
			_ => $"[gold]{MultiplierStatLabel(stat)}[/gold]"
		};
	}

	private static string FormatMultiplyStatStatus(CardExtraEffectTarget target, string factorText, CardExtraEffectMultiplierStat stat)
	{
		string statText = GetMultiplierStatRichText(stat);
		return target switch
		{
			CardExtraEffectTarget.AllEnemies => CardEditorLoc.F("cardText.multiplyStat.allEnemies", $"Multiply current {statText} of ALL enemies by {factorText}.", ("Stat", statText), ("Amount", factorText)),
			CardExtraEffectTarget.RandomEnemy => CardEditorLoc.F("cardText.multiplyStat.randomEnemy", $"Multiply current {statText} of a random enemy by {factorText}.", ("Stat", statText), ("Amount", factorText)),
			CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.multiplyStat.self", $"Multiply your current {statText} by {factorText}.", ("Stat", statText), ("Amount", factorText)),
			_ => CardEditorLoc.F("cardText.multiplyStat.target", $"Multiply current {statText} of an enemy by {factorText}.", ("Stat", statText), ("Amount", factorText))
		};
	}

	private static int ResolveXAmount(CardPlay cardPlay)
	{
		try
		{
			ResourceInfo resources = cardPlay.Resources;

			int energy = Math.Max(0, resources.EnergySpent);
			if (energy <= 0)
			{
				energy = Math.Max(0, resources.EnergyValue);
			}
			if (energy > 0)
			{
				return energy;
			}

			int stars = Math.Max(0, resources.StarsSpent);
			if (stars <= 0)
			{
				stars = Math.Max(0, resources.StarValue);
			}
			return stars;
		}
		catch
		{
			return 0;
		}
	}

	internal static bool SupportsRepeat(CardExtraEffectKind kind)
	{
		// Repeat is intended for "simple" effects; avoid repeating multi-step UX flows (card pickers / pile ops / generation / cost modifiers).
		return kind switch
		{
			CardExtraEffectKind.EndTurn => false,
			CardExtraEffectKind.EnchantCard => false,
			CardExtraEffectKind.MultiplyStatStatus => false,
			CardExtraEffectKind.CreatedCardsCostLess => false,
			CardExtraEffectKind.CreatedCardsUpgraded => false,
			CardExtraEffectKind.GeneratedCardsUpgraded => false,
			CardExtraEffectKind.CardsInPileUpgradedAura => false,
			CardExtraEffectKind.AddRandomCardToHand => false,
			CardExtraEffectKind.ChooseOneOfThreeCardsToHand => false,
			CardExtraEffectKind.MoveCardsBetweenPiles => false,
			CardExtraEffectKind.UpgradeCardsInPile => false,
			CardExtraEffectKind.AddCopyOfThisCard => false,
			CardExtraEffectKind.AddSpecificCardToHand => false,
			CardExtraEffectKind.PlayCardFromPile => false,
			CardExtraEffectKind.DiscardCards => false,
			CardExtraEffectKind.ExhaustCards => false,
			CardExtraEffectKind.AutoPlaySelfFromPile => false,
			CardExtraEffectKind.DrawCardsThatCostLess => false,
			CardExtraEffectKind.AutoDrawSelfFromPile => false,
			CardExtraEffectKind.ConditionalAutoPlayFromPile => false,
			CardExtraEffectKind.ConditionalAutoDrawFromPile => false,
			CardExtraEffectKind.GrantKeywordToPile => false,
			CardExtraEffectKind.GainGold => true,
			CardExtraEffectKind.UpgradeDeckCards => false,
			CardExtraEffectKind.FetchSpecificCardToHand => false,
			_ => true
		};
	}

	private static int ResolveRepeatCount(CardPlay cardPlay, CardExtraEffect effect)
	{
		if (effect == null || effect.GrantToCard || !SupportsRepeat(effect.Kind))
		{
			return 1;
		}

		if (effect.RepeatIsX)
		{
			return Math.Clamp(ResolveXAmount(cardPlay), 0, 99);
		}

		int count = effect.RepeatCount <= 0 ? 1 : effect.RepeatCount;
		return Math.Clamp(count, 1, 99);
	}

	private static string ApplyRepeatSuffix(string baseLine, CardExtraEffect effect)
	{
		if (string.IsNullOrWhiteSpace(baseLine) || effect == null || effect.GrantToCard || !SupportsRepeat(effect.Kind))
		{
			return baseLine;
		}

		bool hasRepeat = effect.RepeatIsX || effect.RepeatCount > 1;
		if (!hasRepeat)
		{
			return baseLine;
		}

		string repeatSuffix = effect.RepeatIsX
			? CardEditorLoc.T("cardText.repeat.xTimes", "X times")
			: effect.RepeatCount == 2
				? CardEditorLoc.T("cardText.repeat.twice", "twice")
				: CardEditorLoc.F("cardText.repeat.times", $"{effect.RepeatCount.ToString(CultureInfo.InvariantCulture)} times", ("Times", effect.RepeatCount));

		string[] split = baseLine.Split('\n', 2);
		string first = split[0];

		int lastPeriod = first.LastIndexOf('.');
		first = lastPeriod >= 0
			? first.Insert(lastPeriod, " " + repeatSuffix)
			: first + " " + repeatSuffix;

		return split.Length == 2 ? first + "\n" + split[1] : first;
	}

	private static int GetMultipliedAmount(int currentAmount, int factor)
	{
		if (factor <= 1)
		{
			return currentAmount;
		}

		long multiplied = (long)currentAmount * factor;
		if (multiplied > int.MaxValue)
		{
			return int.MaxValue;
		}
		if (multiplied < int.MinValue)
		{
			return int.MinValue;
		}

		return (int)multiplied;
	}

	private static int GetMultiplierDelta(int currentAmount, int factor)
	{
		return GetMultipliedAmount(currentAmount, factor) - currentAmount;
	}

	private static async Task MultiplyMatchingTemporaryPowers(Creature target, ModelId underlyingPowerId, Type trackerType, int factor, Creature applier, CardModel? cardSource)
	{
		List<PowerModel> temporaryPowers = target.Powers
			.Where(power => power != null && ((power is ITemporaryPower temporary && Equals(temporary.InternallyAppliedPower.Id, underlyingPowerId)) || trackerType.IsInstanceOfType(power)))
			.ToList();

		foreach (PowerModel power in temporaryPowers)
		{
			int delta = GetMultiplierDelta(power.Amount, factor);
			if (delta == 0)
			{
				continue;
			}

			if (power is ITemporaryPower temporary)
			{
				temporary.IgnoreNextInstance();
			}

			await PowerCmd.ModifyAmount(power, delta, applier, cardSource);
		}
	}

	private static async Task MultiplyPowerWithTemporaryTracking<TPower>(Creature target, Type trackerType, int factor, Creature applier, CardModel? cardSource)
		where TPower : PowerModel
	{
		TPower? power = target.GetPower<TPower>();
		if (power == null || power.Amount == 0)
		{
			return;
		}

		int delta = GetMultiplierDelta(power.Amount, factor);
		if (delta == 0)
		{
			return;
		}

		await PowerCmd.ModifyAmount(power, delta, applier, cardSource);
		await MultiplyMatchingTemporaryPowers(target, power.Id, trackerType, factor, applier, cardSource);
	}

	private static async Task MultiplyBlock(Creature target, int factor, CardPlay cardPlay)
	{
		int currentBlock = target.Block;
		if (currentBlock <= 0)
		{
			return;
		}

		int delta = GetMultiplierDelta(currentBlock, factor);
		if (delta > 0)
		{
			await CreatureCmd.GainBlock(target, delta, ValueProp.Move, cardPlay);
		}
	}

	private static async Task MultiplyStatStatus(Creature target, CardExtraEffectMultiplierStat stat, int factor, Creature applier, CardModel? cardSource, CardPlay cardPlay)
	{
		if (target == null || factor <= 1)
		{
			return;
		}

		switch (stat)
		{
			case CardExtraEffectMultiplierStat.Block:
				await MultiplyBlock(target, factor, cardPlay);
				break;
			case CardExtraEffectMultiplierStat.Strength:
				await MultiplyPowerWithTemporaryTracking<StrengthPower>(target, typeof(CardEditorTempStrengthTrackerPower), factor, applier, cardSource);
				break;
			case CardExtraEffectMultiplierStat.Dexterity:
				await MultiplyPowerWithTemporaryTracking<DexterityPower>(target, typeof(CardEditorTempDexterityTrackerPower), factor, applier, cardSource);
				break;
			case CardExtraEffectMultiplierStat.Focus:
				await MultiplyPowerWithTemporaryTracking<FocusPower>(target, typeof(CardEditorTempFocusTrackerPower), factor, applier, cardSource);
				break;
			case CardExtraEffectMultiplierStat.Weak:
				await MultiplyPowerWithTemporaryTracking<WeakPower>(target, typeof(CardEditorTempWeakTrackerPower), factor, applier, cardSource);
				break;
			case CardExtraEffectMultiplierStat.Frail:
				await MultiplyPowerWithTemporaryTracking<FrailPower>(target, typeof(CardEditorTempFrailTrackerPower), factor, applier, cardSource);
				break;
			case CardExtraEffectMultiplierStat.Vulnerable:
				await MultiplyPowerWithTemporaryTracking<VulnerablePower>(target, typeof(CardEditorTempVulnerableTrackerPower), factor, applier, cardSource);
				break;
			case CardExtraEffectMultiplierStat.Poison:
				await MultiplyPowerWithTemporaryTracking<PoisonPower>(target, typeof(CardEditorTempPoisonTrackerPower), factor, applier, cardSource);
				break;
			case CardExtraEffectMultiplierStat.Doom:
				await MultiplyPowerWithTemporaryTracking<DoomPower>(target, typeof(CardEditorTempDoomTrackerPower), factor, applier, cardSource);
				break;
			case CardExtraEffectMultiplierStat.Constrict:
				await MultiplyPowerWithTemporaryTracking<ConstrictPower>(target, typeof(CardEditorTempConstrictTrackerPower), factor, applier, cardSource);
				break;
			case CardExtraEffectMultiplierStat.Artifact:
				await MultiplyPowerWithTemporaryTracking<ArtifactPower>(target, typeof(CardEditorTempArtifactTrackerPower), factor, applier, cardSource);
				break;
			case CardExtraEffectMultiplierStat.Thorns:
				await MultiplyPowerWithTemporaryTracking<ThornsPower>(target, typeof(CardEditorTempThornsTrackerPower), factor, applier, cardSource);
				break;
			case CardExtraEffectMultiplierStat.Regen:
				await MultiplyPowerWithTemporaryTracking<RegenPower>(target, typeof(CardEditorTempRegenTrackerPower), factor, applier, cardSource);
				break;
			case CardExtraEffectMultiplierStat.Plating:
				await MultiplyPowerWithTemporaryTracking<PlatingPower>(target, typeof(CardEditorTempPlatingTrackerPower), factor, applier, cardSource);
				break;
			case CardExtraEffectMultiplierStat.Intangible:
				await MultiplyPowerWithTemporaryTracking<IntangiblePower>(target, typeof(CardEditorTempIntangibleTrackerPower), factor, applier, cardSource);
				break;
			case CardExtraEffectMultiplierStat.Buffer:
				await MultiplyPowerWithTemporaryTracking<BufferPower>(target, typeof(CardEditorTempBufferTrackerPower), factor, applier, cardSource);
				break;
			case CardExtraEffectMultiplierStat.Vigor:
				await MultiplyPowerWithTemporaryTracking<VigorPower>(target, typeof(CardEditorTempVigorTrackerPower), factor, applier, cardSource);
				break;
			case CardExtraEffectMultiplierStat.Blur:
				await MultiplyPowerWithTemporaryTracking<BlurPower>(target, typeof(CardEditorTempBlurTrackerPower), factor, applier, cardSource);
				break;
			case CardExtraEffectMultiplierStat.Ritual:
				await MultiplyPowerWithTemporaryTracking<RitualPower>(target, typeof(CardEditorTempRitualTrackerPower), factor, applier, cardSource);
				break;
		}
	}

	internal static async Task ExecuteEffect(CombatState combatState, PlayerChoiceContext choiceContext, CardPlay cardPlay, CardExtraEffect effect)
	{
		CardModel card = cardPlay.Card;
		Player? owner = card.Owner;
		if (owner == null)
		{
			return;
		}
		Creature ownerCreature = owner.Creature;
		if (ownerCreature == null)
		{
			return;
		}

		if (effect.GrantToCard)
		{
			if (effect.Kind == CardExtraEffectKind.EnchantCard)
			{
				await EnchantSelectedCards(combatState, choiceContext, cardPlay, effect);
				return;
			}

			await GrantEffectToCard(combatState, choiceContext, cardPlay, effect);
			return;
		}

		if (effect.Kind == CardExtraEffectKind.EnchantCard)
		{
			EnchantSourceCard(combatState, cardPlay, effect);
			return;
		}

		if (effect.Kind is CardExtraEffectKind.CardTypeCostsLess or CardExtraEffectKind.CardTypeStarCostsLess)
		{
			if (!TryResolveSignedAmountForCostEffect(combatState, ownerCreature, cardPlay, effect, out int signedAmount))
			{
				return;
			}

			CardExtraEffect resolved = CloneEffect(effect);
			resolved.Amount = signedAmount;
			resolved.AmountIsX = false;
			resolved.AmountXPlus = 0;
			CardEditorCardTypeCostAuras.Apply(combatState, owner, resolved);
			card.InvokeEnergyCostChanged();
			return;
		}

		if (effect.Kind is CardExtraEffectKind.DrawnCardsCostLess or CardExtraEffectKind.GeneratedCardsCostLess)
		{
			if (!TryResolveSignedAmountForCostEffect(combatState, ownerCreature, cardPlay, effect, out int signedAmount))
			{
				return;
			}

			CardExtraEffect resolved = CloneEffect(effect);
			resolved.Amount = signedAmount;
			resolved.AmountIsX = false;
			resolved.AmountXPlus = 0;
			CardEditorDrawnGeneratedCostController.Apply(combatState, owner, resolved);
			return;
		}

		if (effect.Kind == CardExtraEffectKind.EndTurn)
		{
			PlayerCmd.EndTurn(owner, canBackOut: false);
			return;
		}

		int amount = effect.AmountIsX
			? ResolveXAmountWithPlus(cardPlay, effect.AmountXPlus)
			: effect.Amount;
		if (amount <= 0)
		{
			return;
		}

		if (effect.ScaleMode != CardExtraEffectScaleMode.None)
		{
			int multiplier = GetHistoryCountMultiplier(combatState, ownerCreature, cardPlay, effect);
			if (!DoesCountConditionPass(multiplier, effect))
			{
				return;
			}

			if (effect.ScaleMode == CardExtraEffectScaleMode.ConditionOnly)
			{
				multiplier = 1;
			}

			if (effect.ScaleMode == CardExtraEffectScaleMode.PerHistoryCount && multiplier <= 0 && !effect.HistoryScalingIncludesBase)
			{
				return;
			}

			if (effect.ScaleMode == CardExtraEffectScaleMode.PerHistoryCount && SupportsHistoryScaling(effect.Kind))
			{
				long scaled = (long)amount * Math.Max(0, multiplier);
				long total = effect.HistoryScalingIncludesBase ? (long)amount + scaled : scaled;
				amount = total >= int.MaxValue ? int.MaxValue : (int)total;
				if (amount <= 0)
				{
					return;
				}
			}
		}

		int repeats = ResolveRepeatCount(cardPlay, effect);
		if (repeats <= 0)
		{
			return;
		}

		// For damage, use a single AttackCommand with a hit-count so on-attack hooks (e.g. Vigor) behave like vanilla multi-hit cards.
		// Doing separate AttackCommands per repeat would consume "next attack" bonuses on the first hit only.
		if (effect.Kind == CardExtraEffectKind.DealDamage)
		{
			bool playAttackerAnim = card is CardEditorCreatedCardBase
				&& !IsPowerEffect(effect)
				&& !cardPlay.IsAutoPlay;

			var attack = DamageCmd.Attack(amount).FromCard(cardPlay.Card).WithHitCount(repeats);
			if (!playAttackerAnim)
			{
				// Most vanilla Attack cards already play their own animation. Suppress animation here to avoid double-attacks.
				// For created/custom cards (which have no built-in OnPlay), allow the attack animation so the card feels "real".
				attack.WithNoAttackerAnim();
			}

			switch (effect.Target)
			{
				case CardExtraEffectTarget.AllEnemies:
					attack.TargetingAllOpponents(combatState);
					break;
				case CardExtraEffectTarget.RandomEnemy:
					attack.TargetingRandomOpponents(combatState);
					break;
				case CardExtraEffectTarget.Self:
					attack.Targeting(ownerCreature);
					break;
				default:
				{
					Creature? resolvedTarget = ResolveSingleTarget(combatState, ownerCreature, cardPlay);
					if (resolvedTarget == null)
					{
						return;
					}
					attack.Targeting(resolvedTarget);
					break;
				}
			}

			if (attack.IsSingleTargeted || attack.IsMultiTargeted)
			{
				await attack.Execute(choiceContext);
			}
			return;
		}

		for (int repeatIndex = 0; repeatIndex < repeats; repeatIndex++)
		{
		switch (effect.Kind)
		{
			case CardExtraEffectKind.GainBlock:
			{
				foreach (Creature target in ResolveTargets(combatState, ownerCreature, cardPlay, effect.Target))
				{
					await CreatureCmd.GainBlock(target, amount, ValueProp.Move, cardPlay);
				}
				break;
			}
			case CardExtraEffectKind.MultiplyStatStatus:
			{
				foreach (Creature target in ResolveTargets(combatState, ownerCreature, cardPlay, effect.Target))
				{
					await MultiplyStatStatus(target, effect.MultiplierStat, amount, ownerCreature, cardPlay.Card, cardPlay);
				}
				break;
			}
			case CardExtraEffectKind.LoseHp:
			{
				List<Creature> targets = ResolveTargets(combatState, ownerCreature, cardPlay, effect.Target).Where(c => c != null).ToList();
				if (targets.Count > 0)
				{
					await CreatureCmd.Damage(choiceContext, targets, amount, DamageProps.cardHpLoss, ownerCreature, cardPlay.Card);
				}
				break;
			}
			case CardExtraEffectKind.DrawCards:
			{
				await CardPileCmd.Draw(choiceContext, amount, owner);
				break;
			}
			case CardExtraEffectKind.DrawCardsThatCostLess:
			{
				// Snapshot hand before draw so we can identify exactly which cards were drawn by this effect.
				CardPile? handPile = PileType.Hand.GetPile(owner);
				HashSet<CardModel> cardsBefore = handPile?.Cards != null
					? new HashSet<CardModel>(handPile.Cards, ReferenceEqualityComparer<CardModel>.Instance)
					: new HashSet<CardModel>(ReferenceEqualityComparer<CardModel>.Instance);

				await CardPileCmd.Draw(choiceContext, amount, owner);

				int costLess = effect.CardSelectionCount > 0 ? effect.CardSelectionCount : 1;
				if (handPile != null && costLess != 0)
				{
					List<CardModel> drawnCards = handPile.Cards
						.Where(c => c != null && !cardsBefore.Contains(c))
						.ToList();
					foreach (CardModel drawnCard in drawnCards)
					{
						CardEditorDrawnGeneratedCostController.StampCostReductionForCard(combatState, drawnCard, effect, costLess);
						drawnCard.InvokeEnergyCostChanged();
					}
				}
				break;
			}
			case CardExtraEffectKind.GainEnergy:
			{
				await PlayerCmd.GainEnergy(amount, owner);
				break;
			}
			case CardExtraEffectKind.GainStars:
			{
				await PlayerCmd.GainStars(amount, owner);
				break;
			}
			case CardExtraEffectKind.Heal:
			{
				foreach (Creature target in ResolveTargets(combatState, ownerCreature, cardPlay, effect.Target))
				{
					await CreatureCmd.Heal(target, amount);
				}
				break;
			}
			case CardExtraEffectKind.GainStrength:
			{
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
				{
					await ApplyPower<StrengthPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
					await ApplyPower<CardEditorTempStrengthTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				}
				else
				{
					await ApplyPower<StrengthPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				}
				break;
			}
			case CardExtraEffectKind.LoseStrength:
			{
				int delta = -amount;
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
				{
					await ApplyPower<StrengthPower>(combatState, ownerCreature, cardPlay, effect.Target, delta);
					await ApplyPower<CardEditorTempStrengthTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, delta);
				}
				else
				{
					await ApplyPower<StrengthPower>(combatState, ownerCreature, cardPlay, effect.Target, delta);
				}
				break;
			}
			case CardExtraEffectKind.GainDexterity:
			{
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
				{
					await ApplyPower<DexterityPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
					await ApplyPower<CardEditorTempDexterityTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				}
				else
				{
					await ApplyPower<DexterityPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				}
				break;
			}
			case CardExtraEffectKind.LoseDexterity:
			{
				int delta = -amount;
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
				{
					await ApplyPower<DexterityPower>(combatState, ownerCreature, cardPlay, effect.Target, delta);
					await ApplyPower<CardEditorTempDexterityTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, delta);
				}
				else
				{
					await ApplyPower<DexterityPower>(combatState, ownerCreature, cardPlay, effect.Target, delta);
				}
				break;
			}
			case CardExtraEffectKind.GainFocus:
			{
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
				{
					await ApplyPower<FocusPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
					await ApplyPower<CardEditorTempFocusTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				}
				else
				{
					await ApplyPower<FocusPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				}
				break;
			}
			case CardExtraEffectKind.LoseFocus:
			{
				int delta = -amount;
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
				{
					await ApplyPower<FocusPower>(combatState, ownerCreature, cardPlay, effect.Target, delta);
					await ApplyPower<CardEditorTempFocusTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, delta);
				}
				else
				{
					await ApplyPower<FocusPower>(combatState, ownerCreature, cardPlay, effect.Target, delta);
				}
				break;
			}
			case CardExtraEffectKind.ApplyWeak:
			{
				await ApplyPower<WeakPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempWeakTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.ApplyFrail:
			{
				await ApplyPower<FrailPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempFrailTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.ApplyVulnerable:
			{
				await ApplyPower<VulnerablePower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				// STS2 sets SkipNextDurationTick on debuffs applied to players so enemy-applied debuffs
				// last through the player's next turn. For self-applied debuffs during the player's own
				// turn, this makes 1-stack debuffs linger an extra round (e.g. Vulnerable lasting to Turn 3).
				// Clear the skip in this specific case so the duration matches vanilla expectations for
				// self-inflicted debuffs played on your turn.
				if (effect.Target == CardExtraEffectTarget.Self
					&& ownerCreature.Side == CombatSide.Player
					&& combatState.CurrentSide == CombatSide.Player)
				{
					try
					{
						VulnerablePower? power = ownerCreature.GetPower<VulnerablePower>();
						if (power != null)
						{
							power.SkipNextDurationTick = false;
						}
					}
					catch
					{
						// ignored
					}
				}
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempVulnerableTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.ApplyPoison:
			{
				await ApplyPower<PoisonPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempPoisonTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.ApplyDoom:
			{
				await ApplyPower<DoomPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempDoomTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.GainArtifact:
			{
				await ApplyPower<ArtifactPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempArtifactTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.GainThorns:
			{
				await ApplyPower<ThornsPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempThornsTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.GainRegen:
			{
				await ApplyPower<RegenPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempRegenTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.GainPlating:
			{
				await ApplyPower<PlatingPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempPlatingTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.GainIntangible:
			{
				await ApplyPower<IntangiblePower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempIntangibleTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.GainBuffer:
			{
				await ApplyPower<BufferPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempBufferTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.GainVigor:
			{
				await ApplyPower<VigorPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempVigorTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.GainBlur:
			{
				await ApplyPower<BlurPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempBlurTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.GainRitual:
			{
				await ApplyPower<RitualPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempRitualTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.ApplyConstrict:
			{
				await ApplyPower<ConstrictPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempConstrictTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.AddRandomCardToHand:
			{
				await AddRandomCardsToHand(combatState, owner, amount, effect.GeneratedCardPool, effect.GeneratedCardType);
				break;
			}
			case CardExtraEffectKind.ChooseOneOfThreeCardsToHand:
			{
				await ChooseOneOfThreeCardsToHand(combatState, choiceContext, owner, amount, effect.GeneratedCardPool, effect.GeneratedCardType);
				break;
			}
			case CardExtraEffectKind.Summon:
			{
				await OstyCmd.Summon(choiceContext, owner, amount, cardPlay.Card);
				break;
			}
			case CardExtraEffectKind.OstyAction:
			{
				if (!Osty.CheckMissingWithAnim(owner))
				{
					switch (effect.OstyAction)
					{
						case CardExtraEffectOstyAction.Attack:
						{
							Creature? attackTarget = ResolveSingleTarget(combatState, ownerCreature, cardPlay);
							if (attackTarget != null)
							{
								await DamageCmd.Attack(amount).FromOsty(owner.Osty, cardPlay.Card).Targeting(attackTarget).Execute(choiceContext);
							}
							break;
						}
						case CardExtraEffectOstyAction.AttackAll:
							await DamageCmd.Attack(amount).FromOsty(owner.Osty, cardPlay.Card).TargetingAllOpponents(combatState).Execute(choiceContext);
							break;
						case CardExtraEffectOstyAction.Heal:
							await CreatureCmd.Heal(owner.Osty, amount);
							break;
						case CardExtraEffectOstyAction.Kill:
							if (owner.IsOstyAlive)
							{
								await CreatureCmd.Kill(owner.Osty);
							}
							break;
					}
				}
				break;
			}
			case CardExtraEffectKind.Forge:
			{
				await ForgeCmd.Forge(amount, owner, cardPlay.Card);
				break;
			}
			case CardExtraEffectKind.ChannelLightning:
			{
				for (int i = 0; i < amount; i++)
				{
					await OrbCmd.Channel<LightningOrb>(choiceContext, owner);
				}
				break;
			}
			case CardExtraEffectKind.ChannelFrost:
			{
				for (int i = 0; i < amount; i++)
				{
					await OrbCmd.Channel<FrostOrb>(choiceContext, owner);
				}
				break;
			}
			case CardExtraEffectKind.ChannelDark:
			{
				for (int i = 0; i < amount; i++)
				{
					await OrbCmd.Channel<DarkOrb>(choiceContext, owner);
				}
				break;
			}
			case CardExtraEffectKind.ChannelPlasma:
			{
				for (int i = 0; i < amount; i++)
				{
					await OrbCmd.Channel<PlasmaOrb>(choiceContext, owner);
				}
				break;
			}
			case CardExtraEffectKind.ChannelGlass:
			{
				for (int i = 0; i < amount; i++)
				{
					await OrbCmd.Channel<GlassOrb>(choiceContext, owner);
				}
				break;
			}
			case CardExtraEffectKind.ChannelRandomOrb:
			{
				for (int i = 0; i < amount; i++)
				{
					OrbModel orb = OrbModel.GetRandomOrb(owner.RunState.Rng.CombatOrbGeneration).ToMutable();
					await OrbCmd.Channel(choiceContext, orb, owner);
				}
				break;
			}
			case CardExtraEffectKind.GainOrbSlots:
			{
				await OrbCmd.AddSlots(owner, amount);
				break;
			}
			case CardExtraEffectKind.LoseOrbSlots:
			{
				OrbCmd.RemoveSlots(owner, amount);
				break;
			}
			case CardExtraEffectKind.RemoveBlock:
			{
				foreach (Creature target in ResolveTargets(combatState, ownerCreature, cardPlay, effect.Target))
				{
					if (target == null || target.Block <= 0)
					{
						continue;
					}

					await CreatureCmd.LoseBlock(target, Math.Min((decimal)amount, target.Block));
				}
				break;
			}
			case CardExtraEffectKind.RemoveArtifact:
			{
				foreach (Creature target in ResolveTargets(combatState, ownerCreature, cardPlay, effect.Target))
				{
					ArtifactPower? artifact = target?.GetPower<ArtifactPower>();
					if (artifact == null)
					{
						continue;
					}

					if (amount >= artifact.Amount)
					{
						await PowerCmd.Remove(artifact);
					}
					else
					{
						await PowerCmd.ModifyAmount(artifact, -amount, ownerCreature, cardPlay.Card);
					}
				}
				break;
			}
			case CardExtraEffectKind.MoveCardsBetweenPiles:
			{
				await MoveCardsBetweenPiles(choiceContext, owner, amount, effect, sourceCard: cardPlay?.Card);
				break;
			}
			case CardExtraEffectKind.UpgradeCardsInPile:
			{
				await UpgradeCardsInPile(choiceContext, combatState, owner, amount, effect, sourceCard: cardPlay?.Card);
				break;
			}
			case CardExtraEffectKind.DiscardCards:
			{
				await DiscardCards(choiceContext, owner, amount, effect, sourceCard: cardPlay?.Card);
				break;
			}
			case CardExtraEffectKind.ExhaustCards:
			{
				await ExhaustCards(choiceContext, owner, amount, effect, sourceCard: cardPlay?.Card);
				break;
			}
			case CardExtraEffectKind.EvokeOrbs:
			{
				for (int i = 0; i < amount; i++)
				{
					await OrbCmd.EvokeNext(choiceContext, owner);
				}
				break;
			}
			case CardExtraEffectKind.OrbAction:
			{
				if (effect.OrbAction == CardExtraEffectOrbAction.AddSlots)
				{
					await OrbCmd.AddSlots(owner, amount);
					break;
				}
				if (effect.OrbAction == CardExtraEffectOrbAction.RemoveSlots)
				{
					if (effect.OrbScope == CardExtraEffectOrbScope.All)
						OrbCmd.RemoveSlots(owner, owner.PlayerCombatState.OrbQueue.Capacity);
					else
						OrbCmd.RemoveSlots(owner, amount);
					break;
				}
				if (effect.OrbScope == CardExtraEffectOrbScope.All
					&& (effect.OrbAction == CardExtraEffectOrbAction.Evoke || effect.OrbAction == CardExtraEffectOrbAction.Remove))
				{
					await ExecuteOrbActionAll(choiceContext, owner, effect);
					break;
				}
				for (int i = 0; i < amount; i++)
				{
					if (effect.OrbAction == CardExtraEffectOrbAction.Channel)
					{
						await ChannelOrb(choiceContext, owner, effect);
						continue;
					}

					OrbModel? operatedOrb = await ExecuteOrbAction(choiceContext, owner, effect);
					if (operatedOrb == null)
					{
						break;
					}

					if (effect.OrbAction == CardExtraEffectOrbAction.Evoke
						&& effect.OrbFollowUp == CardExtraEffectOrbFollowUp.ChannelSameType)
					{
						OrbModel? followUpOrb = CreateOrbModelLike(operatedOrb);
						if (followUpOrb != null)
						{
							await OrbCmd.Channel(choiceContext, followUpOrb, owner);
						}
					}
				}
				break;
			}
			case CardExtraEffectKind.AddCopyOfThisCard:
			{
				await AddCopiesOfThisCard(cardPlay, amount, effect);
				break;
			}
			case CardExtraEffectKind.AddSpecificCardToHand:
			{
				await AddSpecificCardsToHand(combatState, owner, amount, effect);
				break;
			}
			case CardExtraEffectKind.FetchSpecificCardToHand:
			{
				await FetchSpecificCardsToHand(combatState, owner, amount, effect);
				break;
			}
			case CardExtraEffectKind.PlayCardFromPile:
			{
				await PlayCardsFromPile(choiceContext, owner, amount, effect, sourceCard: cardPlay?.Card);
				break;
			}
			case CardExtraEffectKind.CardTypeCostsLess:
			case CardExtraEffectKind.CardTypeStarCostsLess:
			{
				CardEditorCardTypeCostAuras.Apply(combatState, owner, effect);
				break;
			}
			case CardExtraEffectKind.DrawnCardsCostLess:
			case CardExtraEffectKind.GeneratedCardsCostLess:
			{
				CardEditorDrawnGeneratedCostController.Apply(combatState, owner, effect);
				break;
			}
			case CardExtraEffectKind.GeneratedCardsUpgraded:
			case CardExtraEffectKind.CardsInPileUpgradedAura:
			{
				CardEditorUpgradeAuraController.Apply(combatState, owner, effect);
				break;
			}
			case CardExtraEffectKind.CardCostsLess:
			case CardExtraEffectKind.CardStarCostsLess:
			case CardExtraEffectKind.IgnoreBlock:
			case CardExtraEffectKind.IgnoreDamageModifiers:
			case CardExtraEffectKind.IgnoreDamageCaps:
			case CardExtraEffectKind.IgnoreDamageNegation:
			{
				break;
			}
			case CardExtraEffectKind.GrantKeywordToPile:
			{
				await GrantKeywordToCards(choiceContext, combatState, owner, amount, effect);
				break;
			}
			case CardExtraEffectKind.GainGold:
			{
				PlayerCmd.GainGold(amount, owner);
				break;
			}
			case CardExtraEffectKind.UpgradeDeckCards:
			{
				await UpgradeDeckCards(owner, amount, effect);
				break;
			}
		}
		}
	}

	private static async Task ChannelOrb(PlayerChoiceContext choiceContext, Player player, CardExtraEffect effect)
	{
		switch (effect.OrbType)
		{
			case CardExtraEffectOrbType.Lightning:
				await OrbCmd.Channel<LightningOrb>(choiceContext, player);
				break;
			case CardExtraEffectOrbType.Frost:
				await OrbCmd.Channel<FrostOrb>(choiceContext, player);
				break;
			case CardExtraEffectOrbType.Dark:
				await OrbCmd.Channel<DarkOrb>(choiceContext, player);
				break;
			case CardExtraEffectOrbType.Plasma:
				await OrbCmd.Channel<PlasmaOrb>(choiceContext, player);
				break;
			case CardExtraEffectOrbType.Glass:
				await OrbCmd.Channel<GlassOrb>(choiceContext, player);
				break;
			default:
			{
				OrbModel orb = OrbModel.GetRandomOrb(player.RunState.Rng.CombatOrbGeneration).ToMutable();
				await OrbCmd.Channel(choiceContext, orb, player);
				break;
			}
		}
	}

	private static async Task<OrbModel?> ExecuteOrbAction(PlayerChoiceContext choiceContext, Player player, CardExtraEffect effect)
	{
		OrbModel? orb = SelectOrb(player, effect);
		if (orb == null)
		{
			return null;
		}

		if (effect.OrbAction == CardExtraEffectOrbAction.Remove)
		{
			RemoveOrb(player, orb);
			return orb;
		}

		await EvokeOrb(choiceContext, player, orb);
		return orb;
	}

	private static OrbModel? SelectOrb(Player player, CardExtraEffect effect)
	{
		if (player?.PlayerCombatState?.OrbQueue?.Orbs == null)
		{
			return null;
		}

		List<OrbModel> matchingOrbs = player.PlayerCombatState.OrbQueue.Orbs
			.Where(orb => orb != null && OrbMatchesType(orb, effect.OrbType))
			.ToList();

		if (matchingOrbs.Count == 0)
		{
			return null;
		}

		return effect.OrbSelection switch
		{
			CardExtraEffectOrbSelection.Rightmost => matchingOrbs[0],
			CardExtraEffectOrbSelection.Middle => matchingOrbs[(matchingOrbs.Count - 1) / 2],
			_ => matchingOrbs[matchingOrbs.Count - 1]
		};
	}

	private static bool OrbMatchesType(OrbModel orb, CardExtraEffectOrbType type)
	{
		return type switch
		{
			CardExtraEffectOrbType.Lightning => orb is LightningOrb,
			CardExtraEffectOrbType.Frost => orb is FrostOrb,
			CardExtraEffectOrbType.Dark => orb is DarkOrb,
			CardExtraEffectOrbType.Plasma => orb is PlasmaOrb,
			CardExtraEffectOrbType.Glass => orb is GlassOrb,
			_ => true
		};
	}

	private static async Task EvokeOrb(PlayerChoiceContext choiceContext, Player player, OrbModel orb)
	{
		if (CombatManager.Instance.IsOverOrEnding)
		{
			return;
		}

		var orbQueue = player.PlayerCombatState.OrbQueue;
		if (!orbQueue.Remove(orb))
		{
			return;
		}

		NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.OrbManager?.EvokeOrbAnim(orb);
		choiceContext.PushModel(orb);
		IEnumerable<Creature> targets = await orb.Evoke(choiceContext);
		choiceContext.PopModel(orb);
		await Hook.AfterOrbEvoked(choiceContext, player.Creature.CombatState, orb, targets);
		orb.RemoveInternal();
	}

	private static void RemoveOrb(Player player, OrbModel orb)
	{
		if (CombatManager.Instance.IsOverOrEnding)
		{
			return;
		}

		var orbQueue = player.PlayerCombatState.OrbQueue;
		if (!orbQueue.Remove(orb))
		{
			return;
		}

		NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.OrbManager?.EvokeOrbAnim(orb);
		orb.RemoveInternal();
	}

	private static async Task ExecuteOrbActionAll(PlayerChoiceContext choiceContext, Player player, CardExtraEffect effect)
	{
		List<OrbModel> orbs = player.PlayerCombatState.OrbQueue.Orbs
			.Where(orb => orb != null && OrbMatchesType(orb, effect.OrbType))
			.ToList();

		foreach (OrbModel orb in orbs)
		{
			if (effect.OrbAction == CardExtraEffectOrbAction.Remove)
			{
				RemoveOrb(player, orb);
			}
			else
			{
				await EvokeOrb(choiceContext, player, orb);
				if (effect.OrbFollowUp == CardExtraEffectOrbFollowUp.ChannelSameType)
				{
					OrbModel? followUpOrb = CreateOrbModelLike(orb);
					if (followUpOrb != null)
					{
						await OrbCmd.Channel(choiceContext, followUpOrb, player);
					}
				}
			}
		}
	}

	private static OrbModel? CreateOrbModelLike(OrbModel orb)
	{
		return orb switch
		{
			LightningOrb => ModelDb.Orb<LightningOrb>().ToMutable(),
			FrostOrb => ModelDb.Orb<FrostOrb>().ToMutable(),
			DarkOrb => ModelDb.Orb<DarkOrb>().ToMutable(),
			PlasmaOrb => ModelDb.Orb<PlasmaOrb>().ToMutable(),
			GlassOrb => ModelDb.Orb<GlassOrb>().ToMutable(),
			_ => null
		};
	}

	private static void EnchantSourceCard(CombatState combatState, CardPlay cardPlay, CardExtraEffect effect)
	{
		CardModel? card = cardPlay?.Card;
		if (combatState == null || card == null || effect == null)
		{
			return;
		}
		if (!TryResolveEffectEnchantmentId(effect, out ModelId enchantmentId))
		{
			return;
		}

		int amount = effect.AmountIsX
			? ResolveXAmountWithPlus(cardPlay, effect.AmountXPlus)
			: effect.Amount;
		if (amount <= 0)
		{
			return;
		}
		if (!CanEffectEnchantmentApplyToCard(enchantmentId, card))
		{
			return;
		}

		Creature? ownerCreature = card.Owner?.Creature;
		if (ownerCreature == null)
		{
			return;
		}

		if (effect.ScaleMode != CardExtraEffectScaleMode.None)
		{
			int multiplier = GetHistoryCountMultiplier(combatState, ownerCreature, cardPlay, effect);
			if (!DoesCountConditionPass(multiplier, effect))
			{
				return;
			}

			if (effect.ScaleMode == CardExtraEffectScaleMode.ConditionOnly)
			{
				multiplier = 1;
			}

			if (effect.ScaleMode == CardExtraEffectScaleMode.PerHistoryCount && multiplier <= 0 && !effect.HistoryScalingIncludesBase)
			{
				return;
			}

			if (effect.ScaleMode == CardExtraEffectScaleMode.PerHistoryCount)
			{
				long scaled = (long)amount * Math.Max(0, multiplier);
				long total = effect.HistoryScalingIncludesBase ? (long)amount + scaled : scaled;
				amount = total >= int.MaxValue ? int.MaxValue : (int)total;
				if (amount <= 0)
				{
					return;
				}
			}
		}

		CardEditorTemporaryEnchantmentController.Apply(combatState, card, enchantmentId, amount, effect.EnchantmentDuration, effect.EnchantmentTurns);
	}

	private static async Task EnchantSelectedCards(CombatState combatState, PlayerChoiceContext choiceContext, CardPlay cardPlay, CardExtraEffect effect)
	{
		if (combatState == null || choiceContext == null || cardPlay?.Card?.Owner == null || effect == null)
		{
			return;
		}
		if (!TryResolveEffectEnchantmentId(effect, out ModelId enchantmentId))
		{
			return;
		}

		CardModel sourceCard = cardPlay.Card;
		Player owner = sourceCard.Owner;

		List<CardModel> candidates;
		if (effect.CardSelectionPile == CardExtraEffectCardPile.AllPiles)
		{
			HashSet<CardModel> unique = new HashSet<CardModel>(ReferenceEqualityComparer<CardModel>.Instance);
			foreach (PileType pileType in new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust })
			{
				CardPile? pile = pileType.GetPile(owner);
				if (pile == null)
				{
					continue;
				}
				foreach (CardModel card in pile.Cards)
				{
					if (card == null || ReferenceEquals(card, sourceCard))
					{
						continue;
					}
					if (!MatchesGrantCardFilters(owner, card, effect))
					{
						continue;
					}
					if (!CanEffectEnchantmentApplyToCard(enchantmentId, card))
					{
						continue;
					}
					unique.Add(card);
				}
			}
			candidates = unique.ToList();
		}
		else
		{
			PileType fromPileType = ResolvePileType(effect.CardSelectionPile);
			CardPile fromPile = fromPileType.GetPile(owner);
			if (fromPile == null)
			{
				return;
			}

			candidates = fromPile.Cards
				.Where(c => c != null && !ReferenceEquals(c, sourceCard))
				.Where(c => MatchesGrantCardFilters(owner, c, effect))
				.Where(c => CanEffectEnchantmentApplyToCard(enchantmentId, c))
				.ToList();
		}
		if (candidates.Count == 0)
		{
			return;
		}

		int desiredCount = effect.CardSelectionCountIsX ? ResolveXAmount(cardPlay) : effect.CardSelectionCount;
		desiredCount = Math.Clamp(desiredCount, 0, 99);

		List<CardModel> selected;
		if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All)
		{
			selected = candidates;
		}
		else if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Random)
		{
			if (desiredCount <= 0)
			{
				return;
			}
			selected = PickRandomDistinct(candidates, Math.Min(desiredCount, candidates.Count), owner.RunState?.Rng?.Shuffle);
		}
		else if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.UpTo)
		{
			if (desiredCount <= 0)
			{
				return;
			}
			int maxCount = Math.Min(desiredCount, candidates.Count);
			CardSelectorPrefs prefs = new CardSelectorPrefs(new LocString("gameplay_ui", "CHOOSE_CARD_HEADER"), 0, maxCount) { Cancelable = true };
			selected = (await CardSelectCmd.FromSimpleGrid(choiceContext, candidates, owner, prefs)).OfType<CardModel>().ToList();
		}
		else
		{
			if (desiredCount <= 0)
			{
				return;
			}
			CardSelectorPrefs prefs = new CardSelectorPrefs(new LocString("gameplay_ui", "CHOOSE_CARD_HEADER"), Math.Min(desiredCount, candidates.Count));
			selected = (await CardSelectCmd.FromSimpleGrid(choiceContext, candidates, owner, prefs)).OfType<CardModel>().ToList();
		}

		if (selected.Count == 0)
		{
			return;
		}

		int amount = effect.AmountIsX
			? ResolveXAmountWithPlus(cardPlay, effect.AmountXPlus)
			: effect.Amount;
		if (amount <= 0)
		{
			return;
		}

		Creature? ownerCreature = owner.Creature;
		if (ownerCreature == null)
		{
			return;
		}

		if (effect.ScaleMode != CardExtraEffectScaleMode.None)
		{
			int multiplier = GetHistoryCountMultiplier(combatState, ownerCreature, cardPlay, effect);
			if (!DoesCountConditionPass(multiplier, effect))
			{
				return;
			}

			if (effect.ScaleMode == CardExtraEffectScaleMode.ConditionOnly)
			{
				multiplier = 1;
			}

			if (effect.ScaleMode == CardExtraEffectScaleMode.PerHistoryCount && multiplier <= 0 && !effect.HistoryScalingIncludesBase)
			{
				return;
			}

			if (effect.ScaleMode == CardExtraEffectScaleMode.PerHistoryCount)
			{
				long scaled = (long)amount * Math.Max(0, multiplier);
				long total = effect.HistoryScalingIncludesBase ? (long)amount + scaled : scaled;
				amount = total >= int.MaxValue ? int.MaxValue : (int)total;
				if (amount <= 0)
				{
					return;
				}
			}
		}

		foreach (CardModel card in selected)
		{
			if (card == null)
			{
				continue;
			}
			if (!CanEffectEnchantmentApplyToCard(enchantmentId, card))
			{
				continue;
			}

			CardEditorTemporaryEnchantmentController.Apply(combatState, card, enchantmentId, amount, effect.EnchantmentDuration, effect.EnchantmentTurns);
		}
	}

	private static bool CanEffectEnchantmentApplyToCard(ModelId enchantmentId, CardModel card)
	{
		if (card == null || enchantmentId == ModelId.none)
		{
			return false;
		}

		EnchantmentModel? enchantment = ModelDb.GetByIdOrNull<EnchantmentModel>(enchantmentId);
		return enchantment != null && enchantment.CanEnchant(card);
	}

	private static async Task GrantEffectToCard(CombatState combatState, PlayerChoiceContext choiceContext, CardPlay cardPlay, CardExtraEffect effect)
	{
		if (combatState == null || choiceContext == null || cardPlay?.Card?.Owner == null || effect == null)
		{
			return;
		}

		CardModel sourceCard = cardPlay.Card;
		Player owner = sourceCard.Owner;

		List<CardModel> candidates;
		if (effect.CardSelectionPile == CardExtraEffectCardPile.AllPiles)
		{
			HashSet<CardModel> unique = new HashSet<CardModel>(ReferenceEqualityComparer<CardModel>.Instance);
			foreach (PileType pileType in new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust })
			{
				CardPile? pile = pileType.GetPile(owner);
				if (pile == null)
				{
					continue;
				}
				foreach (CardModel card in pile.Cards)
				{
					if (card == null || ReferenceEquals(card, sourceCard))
					{
						continue;
					}
					if (!MatchesGrantCardFilters(owner, card, effect))
					{
						continue;
					}
					unique.Add(card);
				}
			}
			candidates = unique.ToList();
		}
		else
		{
			PileType fromPileType = ResolvePileType(effect.CardSelectionPile);
			CardPile fromPile = fromPileType.GetPile(owner);
			if (fromPile == null)
			{
				return;
			}

			candidates = fromPile.Cards
				.Where(c => c != null && !ReferenceEquals(c, sourceCard))
				.Where(c => MatchesGrantCardFilters(owner, c, effect))
				.ToList();
		}
		if (candidates.Count == 0)
		{
			return;
		}

		int desiredCount = effect.CardSelectionCountIsX ? ResolveXAmount(cardPlay) : effect.CardSelectionCount;
		desiredCount = Math.Clamp(desiredCount, 0, 99);

		List<CardModel> selected;
		if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All)
		{
			selected = candidates;
		}
		else if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Random)
		{
			if (desiredCount <= 0)
			{
				return;
			}
			selected = PickRandomDistinct(candidates, Math.Min(desiredCount, candidates.Count), owner.RunState?.Rng?.Shuffle);
		}
		else if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.UpTo)
		{
			if (desiredCount <= 0)
			{
				return;
			}
			int maxCount = Math.Min(desiredCount, candidates.Count);
			CardSelectorPrefs prefs = new CardSelectorPrefs(new LocString("gameplay_ui", "CHOOSE_CARD_HEADER"), 0, maxCount) { Cancelable = true };
			selected = (await CardSelectCmd.FromSimpleGrid(choiceContext, candidates, owner, prefs)).OfType<CardModel>().ToList();
		}
		else
		{
			if (desiredCount <= 0)
			{
				return;
			}
			CardSelectorPrefs prefs = new CardSelectorPrefs(new LocString("gameplay_ui", "CHOOSE_CARD_HEADER"), Math.Min(desiredCount, candidates.Count));
			selected = (await CardSelectCmd.FromSimpleGrid(choiceContext, candidates, owner, prefs)).OfType<CardModel>().ToList();
		}

		if (selected.Count == 0)
		{
			return;
		}

		foreach (CardModel card in selected)
		{
			if (card == null)
			{
				continue;
			}
			CardEditorTemporaryExtraEffectController.Grant(
				combatState,
				card,
				effect,
				effect.CardGrantDuration,
				effect.CardGrantTurns);
			try
			{
				card.InvokeEnergyCostChanged();

				NCard? node = NCard.FindOnTable(card);
				if (node != null)
				{
					node.UpdateVisuals(node.DisplayingPile, CardPreviewMode.Normal);
				}
			}
			catch
			{
				// ignored
			}
		}
	}

	private static async Task MoveCardsBetweenPiles(PlayerChoiceContext choiceContext, Player owner, int amount, CardExtraEffect effect, CardModel? sourceCard)
	{
		if (choiceContext == null || owner == null || effect == null)
		{
			return;
		}

		PileType toPileType = ResolvePileType(effect.MoveToPile);

		List<CardModel> candidates;
		if (effect.CardSelectionPile == CardExtraEffectCardPile.AllPiles)
		{
			HashSet<CardModel> unique = new HashSet<CardModel>(ReferenceEqualityComparer<CardModel>.Instance);
			foreach (PileType pileType in new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust })
			{
				CardPile? pile = pileType.GetPile(owner);
				if (pile == null) continue;
				foreach (CardModel c in pile.Cards)
				{
					if (c != null && !ReferenceEquals(c, sourceCard) && PassesCostFilter(c, effect)) unique.Add(c);
				}
			}
			candidates = unique.ToList();
		}
		else
		{
			PileType fromPileType = ResolvePileType(effect.CardSelectionPile);
			CardPile? fromPile = fromPileType.GetPile(owner);
			if (fromPile == null) return;
			candidates = fromPile.Cards.Where(c => c != null && !ReferenceEquals(c, sourceCard) && PassesCostFilter(c, effect)).ToList();
		}

		if (candidates.Count == 0)
		{
			return;
		}

		int count = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All
			? candidates.Count
			: Math.Max(0, Math.Min(amount, candidates.Count));
		if (count <= 0)
		{
			return;
		}

		List<CardModel> selected;
		if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All)
		{
			selected = candidates;
		}
		else if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Random)
		{
			selected = PickRandomDistinct(candidates, count, owner.RunState?.Rng?.Shuffle);
		}
		else if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.UpTo)
		{
			CardSelectorPrefs prefs = new CardSelectorPrefs(new LocString("gameplay_ui", "CHOOSE_CARD_HEADER"), 0, count) { Cancelable = true };
			selected = (await CardSelectCmd.FromSimpleGrid(choiceContext, candidates, owner, prefs)).OfType<CardModel>().ToList();
		}
		else
		{
			CardSelectorPrefs prefs = new CardSelectorPrefs(new LocString("gameplay_ui", "CHOOSE_CARD_HEADER"), count);
			selected = (await CardSelectCmd.FromSimpleGrid(choiceContext, candidates, owner, prefs)).OfType<CardModel>().ToList();
		}

		if (selected.Count == 0)
		{
			return;
		}

		// Use vanilla pipelines where possible so discard/exhaust synergies work (history + hooks).
		if (toPileType == PileType.Discard)
		{
			await CardCmd.Discard(choiceContext, selected);
			return;
		}
		if (toPileType == PileType.Exhaust)
		{
			foreach (CardModel card in selected)
			{
				if (card != null)
				{
					await CardCmd.Exhaust(choiceContext, card);
				}
			}
			return;
		}

		CardPilePosition position = ResolvePilePosition(effect.MoveToPosition);
		foreach (CardModel card in selected)
		{
			if (card != null)
			{
				await CardPileCmd.Add(card, toPileType, position);
			}
		}
	}

	private static async Task UpgradeCardsInPile(PlayerChoiceContext choiceContext, CombatState? combatState, Player owner, int amount, CardExtraEffect effect, CardModel? sourceCard)
	{
		if (choiceContext == null || owner == null || effect == null)
		{
			return;
		}

		List<CardModel> candidates;
		if (effect.CardSelectionPile == CardExtraEffectCardPile.AllPiles)
		{
			HashSet<CardModel> unique = new HashSet<CardModel>(ReferenceEqualityComparer<CardModel>.Instance);
			foreach (PileType pileType in new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust })
			{
				CardPile? pile = pileType.GetPile(owner);
				if (pile == null)
				{
					continue;
				}

				foreach (CardModel card in pile.Cards)
				{
					if (card != null && !ReferenceEquals(card, sourceCard) && card.IsUpgradable)
					{
						unique.Add(card);
					}
				}
			}

			candidates = unique.ToList();
		}
		else if (effect.CardSelectionPile == CardExtraEffectCardPile.Deck)
		{
			CardPile? deckPile = PileType.Deck.GetPile(owner);
			if (deckPile == null)
			{
				return;
			}

			candidates = deckPile.Cards.Where(c => c != null && !ReferenceEquals(c, sourceCard) && c.IsUpgradable).ToList();
		}
		else
		{
			PileType fromPileType = ResolvePileType(effect.CardSelectionPile);
			CardPile fromPile = fromPileType.GetPile(owner);
			if (fromPile == null)
			{
				return;
			}

			candidates = fromPile.Cards.Where(c => c != null && !ReferenceEquals(c, sourceCard) && c.IsUpgradable).ToList();
		}

		if (candidates.Count == 0)
		{
			return;
		}

		int count = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All
			? candidates.Count
			: Math.Max(0, Math.Min(amount, candidates.Count));
		if (count <= 0)
		{
			return;
		}

		List<CardModel> selected;
		if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All)
		{
			selected = candidates;
		}
		else if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Random)
		{
			selected = PickRandomDistinct(candidates, count, owner.RunState?.Rng?.Shuffle);
		}
		else if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.UpTo)
		{
			CardSelectorPrefs prefs = new CardSelectorPrefs(new LocString("gameplay_ui", "CHOOSE_CARD_HEADER"), 0, count) { Cancelable = true };
			selected = (await CardSelectCmd.FromSimpleGrid(choiceContext, candidates, owner, prefs)).OfType<CardModel>().ToList();
		}
		else
		{
			CardSelectorPrefs prefs = new CardSelectorPrefs(new LocString("gameplay_ui", "CHOOSE_CARD_HEADER"), count);
			selected = (await CardSelectCmd.FromSimpleGrid(choiceContext, candidates, owner, prefs)).OfType<CardModel>().ToList();
		}

		if (selected.Count == 0)
		{
			return;
		}

		CardCmd.Upgrade(selected, CardPreviewStyle.None);

		var duration = effect.CardCostsLessDuration;
		if (duration != CardExtraEffectCardCostsLessDuration.Permanent && combatState != null)
		{
			CardEditorTemporaryUpgradeController.Apply(combatState, selected, duration, effect.CardCostsLessTurns);
		}
	}

	private static async Task DiscardCards(PlayerChoiceContext choiceContext, Player owner, int amount, CardExtraEffect effect, CardModel? sourceCard)
	{
		if (choiceContext == null || owner == null || effect == null || amount <= 0)
		{
			return;
		}

		PileType fromPileType = ResolvePileType(effect.CardSelectionPile);
		CardPile fromPile = fromPileType.GetPile(owner);
		if (fromPile == null)
		{
			return;
		}

		List<CardModel> candidates = fromPile.Cards
			.Where(c => c != null && !ReferenceEquals(c, sourceCard) && PassesCostFilter(c, effect))
			.ToList();
		if (candidates.Count == 0)
		{
			return;
		}

		int count = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All
			? candidates.Count
			: Math.Max(0, Math.Min(amount, candidates.Count));
		if (count <= 0)
		{
			return;
		}

		List<CardModel> selected;
		if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All)
		{
			selected = candidates;
		}
		else if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Random)
		{
			selected = PickRandomDistinct(candidates, count, owner.RunState?.Rng?.Shuffle);
		}
		else if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.UpTo)
		{
			CardSelectorPrefs prefs = new CardSelectorPrefs(new LocString("gameplay_ui", "CHOOSE_CARD_HEADER"), 0, count) { Cancelable = true };
			selected = (await CardSelectCmd.FromSimpleGrid(choiceContext, candidates, owner, prefs)).OfType<CardModel>().ToList();
		}
		else
		{
			CardSelectorPrefs prefs = new CardSelectorPrefs(new LocString("gameplay_ui", "CHOOSE_CARD_HEADER"), count);
			selected = (await CardSelectCmd.FromSimpleGrid(choiceContext, candidates, owner, prefs)).OfType<CardModel>().ToList();
		}

		if (selected.Count == 0)
		{
			return;
		}

		await CardCmd.Discard(choiceContext, selected);
	}

	private static async Task ExhaustCards(PlayerChoiceContext choiceContext, Player owner, int amount, CardExtraEffect effect, CardModel? sourceCard)
	{
		if (choiceContext == null || owner == null || effect == null || amount <= 0)
		{
			return;
		}

		PileType fromPileType = ResolvePileType(effect.CardSelectionPile);
		CardPile fromPile = fromPileType.GetPile(owner);
		if (fromPile == null)
		{
			return;
		}

		List<CardModel> candidates = fromPile.Cards
			.Where(c => c != null && !ReferenceEquals(c, sourceCard) && PassesCostFilter(c, effect))
			.ToList();
		if (candidates.Count == 0)
		{
			return;
		}

		int count = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All
			? candidates.Count
			: Math.Max(0, Math.Min(amount, candidates.Count));
		if (count <= 0)
		{
			return;
		}

		List<CardModel> selected;
		if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All)
		{
			selected = candidates;
		}
		else if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Random)
		{
			selected = PickRandomDistinct(candidates, count, owner.RunState?.Rng?.Shuffle);
		}
		else if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.UpTo)
		{
			CardSelectorPrefs prefs = new CardSelectorPrefs(new LocString("gameplay_ui", "CHOOSE_CARD_HEADER"), 0, count) { Cancelable = true };
			selected = (await CardSelectCmd.FromSimpleGrid(choiceContext, candidates, owner, prefs)).OfType<CardModel>().ToList();
		}
		else
		{
			CardSelectorPrefs prefs = new CardSelectorPrefs(new LocString("gameplay_ui", "CHOOSE_CARD_HEADER"), count);
			selected = (await CardSelectCmd.FromSimpleGrid(choiceContext, candidates, owner, prefs)).OfType<CardModel>().ToList();
		}

		if (selected.Count == 0)
		{
			return;
		}

		foreach (CardModel card in selected)
		{
			if (card != null)
			{
				await CardCmd.Exhaust(choiceContext, card);
			}
		}
	}

	private static async Task GrantKeywordToCards(PlayerChoiceContext choiceContext, CombatState combatState, Player owner, int amount, CardExtraEffect effect)
	{
		if (owner == null || combatState == null || effect == null)
		{
			return;
		}

		List<CardModel> candidates;
		if (effect.CardSelectionPile == CardExtraEffectCardPile.AllPiles)
		{
			HashSet<CardModel> unique = new HashSet<CardModel>(ReferenceEqualityComparer<CardModel>.Instance);
			foreach (PileType pileType in new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust })
			{
				CardPile? pile = pileType.GetPile(owner);
				if (pile != null)
				{
					foreach (CardModel card in pile.Cards)
					{
						if (card != null) unique.Add(card);
					}
				}
			}
			candidates = unique.ToList();
		}
		else
		{
			PileType fromPileType = ResolvePileType(effect.CardSelectionPile);
			CardPile? fromPile = fromPileType.GetPile(owner);
			if (fromPile == null) return;
			candidates = fromPile.Cards.Where(c => c != null).ToList();
		}

		if (candidates.Count == 0) return;

		int count = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All
			? candidates.Count
			: Math.Clamp(amount, 0, candidates.Count);

		List<CardModel> selected;
		if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All)
		{
			selected = candidates;
		}
		else if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.UpTo)
		{
			CardSelectorPrefs prefs = new CardSelectorPrefs(new LocString("gameplay_ui", "CHOOSE_CARD_HEADER"), 0, count) { Cancelable = true };
			selected = (await CardSelectCmd.FromSimpleGrid(choiceContext, candidates, owner, prefs)).OfType<CardModel>().ToList();
		}
		else if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Choose)
		{
			CardSelectorPrefs prefs = new CardSelectorPrefs(new LocString("gameplay_ui", "CHOOSE_CARD_HEADER"), count);
			selected = (await CardSelectCmd.FromSimpleGrid(choiceContext, candidates, owner, prefs)).OfType<CardModel>().ToList();
		}
		else
		{
			selected = PickRandomDistinct(candidates, count, owner.RunState?.Rng?.Shuffle);
		}

		foreach (CardModel card in selected)
		{
			if (card != null)
			{
				CardEditorTemporaryKeywordController.ApplyThisCombat(combatState, card, effect.GrantedKeyword);
			}
		}
	}

	private static async Task UpgradeDeckCards(Player owner, int amount, CardExtraEffect effect)
	{
		if (owner == null || effect == null)
		{
			return;
		}

		CardPile? deckPile = PileType.Deck.GetPile(owner);
		if (deckPile == null)
		{
			return;
		}

		List<CardModel> candidates = deckPile.Cards.Where(c => c != null && c.IsUpgradable).ToList();
		if (candidates.Count == 0)
		{
			return;
		}

		int count = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All
			? candidates.Count
			: Math.Clamp(amount, 0, candidates.Count);

		List<CardModel> selected;
		if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All)
		{
			selected = candidates;
		}
		else
		{
			selected = PickRandomDistinct(candidates, count, owner.RunState?.Rng?.Shuffle);
		}

		CardCmd.Upgrade(selected, CardPreviewStyle.None);
		await Task.CompletedTask;
	}

	private static async Task AddCopiesOfThisCard(CardPlay cardPlay, int amount, CardExtraEffect effect)
	{
		if (cardPlay?.Card?.Owner == null || effect == null)
		{
			return;
		}
		if (amount <= 0)
		{
			return;
		}
		if (!CombatManager.Instance.IsInProgress)
		{
			return;
		}

		CardModel source = cardPlay.Card;
		PileType toPileType = ResolvePileType(effect.MoveToPile);
		CardPilePosition position = ResolvePilePosition(effect.MoveToPosition);

		int copies = Math.Clamp(amount, 1, 99);
		for (int i = 0; i < copies; i++)
		{
			CardModel clone = source.CreateClone();
			await CardPileCmd.AddGeneratedCardToCombat(clone, toPileType, addedByPlayer: true, position);
		}
	}

	private static async Task AddSpecificCardsToHand(CombatState combatState, Player owner, int amount, CardExtraEffect effect)
	{
		if (combatState == null || owner == null || effect == null || amount <= 0)
		{
			return;
		}

		if (!TryParseSpecificCardId(effect.SpecificCardId ?? string.Empty, out ModelId id))
		{
			return;
		}

		CardModel? canonical = ModelDb.GetByIdOrNull<CardModel>(id);
		if (canonical == null)
		{
			return;
		}

		PileType toPileType = ResolvePileType(effect.MoveToPile);
		CardPilePosition position = ResolvePilePosition(effect.MoveToPosition);
		for (int i = 0; i < amount; i++)
		{
			CardModel generated = combatState.CreateCard(canonical, owner);
			await CardPileCmd.AddGeneratedCardToCombat(generated, toPileType, addedByPlayer: true, position);
		}
	}

	private static async Task FetchSpecificCardsToHand(CombatState combatState, Player owner, int amount, CardExtraEffect effect)
	{
		if (combatState == null || owner == null || effect == null || amount <= 0)
		{
			return;
		}

		if (!TryParseSpecificCardId(effect.SpecificCardId ?? string.Empty, out ModelId id))
		{
			return;
		}

		CardModel? canonical = ModelDb.GetByIdOrNull<CardModel>(id);
		if (canonical == null)
		{
			return;
		}

		PileType toPileType = ResolvePileType(effect.MoveToPile);
		bool fetchAll = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All;
		IEnumerable<CardModel> query = owner.PlayerCombatState.AllCards
			.Where(c => c != null && c.Id == canonical.Id && c.Pile?.Type != toPileType);
		if (!fetchAll)
			query = query.Take(amount);
		List<CardModel> candidates = query.ToList();

		foreach (CardModel card in candidates)
		{
			await CardPileCmd.Add(card, toPileType);
		}
	}

	private static async Task PlayCardsFromPile(PlayerChoiceContext choiceContext, Player owner, int amount, CardExtraEffect effect, CardModel? sourceCard)
	{
		if (choiceContext == null || owner == null || effect == null || amount <= 0)
		{
			return;
		}

		PileType fromPileType = ResolvePileType(effect.CardSelectionPile);
		CardPile fromPile = fromPileType.GetPile(owner);
		if (fromPile == null)
		{
			return;
		}

		List<CardModel> candidates = fromPile.Cards.Where(c => c != null && !ReferenceEquals(c, sourceCard) && PassesCostFilter(c, effect)).ToList();
		if (candidates.Count == 0)
		{
			return;
		}

		int count = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All
			? candidates.Count
			: Math.Max(0, Math.Min(amount, candidates.Count));
		if (count <= 0)
		{
			return;
		}

		List<CardModel> selected;
		if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All)
		{
			selected = candidates;
		}
		else if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Random)
		{
			selected = PickRandomDistinct(candidates, count, owner.RunState?.Rng?.Shuffle);
		}
		else if (effect.CardSelectionMode == CardExtraEffectCardSelectionMode.UpTo)
		{
			CardSelectorPrefs prefs = new CardSelectorPrefs(new LocString("gameplay_ui", "CHOOSE_CARD_HEADER"), 0, count) { Cancelable = true };
			selected = (await CardSelectCmd.FromSimpleGrid(choiceContext, candidates, owner, prefs)).OfType<CardModel>().ToList();
		}
		else
		{
			CardSelectorPrefs prefs = new CardSelectorPrefs(new LocString("gameplay_ui", "CHOOSE_CARD_HEADER"), count);
			selected = (await CardSelectCmd.FromSimpleGrid(choiceContext, candidates, owner, prefs)).OfType<CardModel>().ToList();
		}

		if (selected.Count == 0)
		{
			return;
		}

		foreach (CardModel card in selected)
		{
			if (card == null)
			{
				continue;
			}

			await CardPileCmd.Add(card, PileType.Play);
			await CardCmd.AutoPlay(choiceContext, card, target: null);
		}
	}

	private static List<CardModel> PickRandomDistinct(IReadOnlyList<CardModel> cards, int count, MegaCrit.Sts2.Core.Random.Rng? rng)
	{
		if (cards == null || cards.Count == 0 || count <= 0)
		{
			return new List<CardModel>();
		}

		int take = Math.Min(count, cards.Count);
		List<CardModel> pool = cards.Where(c => c != null).ToList();
		if (pool.Count <= take)
		{
			return pool;
		}

		for (int i = 0; i < take; i++)
		{
			int j = rng == null ? i : i + rng.NextInt(pool.Count - i);
			(pool[i], pool[j]) = (pool[j], pool[i]);
		}
		return pool.Take(take).ToList();
	}

	// Returns false if the card fails the cost filter: X-cost cards always fail when the filter is enabled,
	// and non-X cards must have a current cost (with all modifiers) <= CostFilterMax.
	private static bool PassesCostFilter(CardModel card, CardExtraEffect effect)
	{
		if (effect == null || !effect.CostFilterEnabled)
		{
			return true;
		}
		if (card == null || card.EnergyCost.CostsX)
		{
			return false;
		}
		return card.EnergyCost.GetWithModifiers(CostModifiers.All) <= effect.CostFilterMax;
	}

	private static string BuildCostFilteredText(string text, CardExtraEffect effect)
	{
		if (effect == null || !effect.CostFilterEnabled)
		{
			return text;
		}
		string note = effect.CostFilterMax == 0
			? CardEditorLoc.T("cardText.costFilter.freeOnly", " (free only)")
			: CardEditorLoc.F("cardText.costFilter.maxOnly", $" (cost {effect.CostFilterMax} or less)", ("Max", effect.CostFilterMax.ToString(CultureInfo.InvariantCulture)));
		if (text.EndsWith('.') && text.Length > 1)
		{
			return text[..^1] + note + ".";
		}
		return text + note;
	}

	private static PileType ResolvePileType(CardExtraEffectCardPile pile)
	{
		return pile switch
		{
			CardExtraEffectCardPile.Hand => PileType.Hand,
			CardExtraEffectCardPile.DrawPile => PileType.Draw,
			CardExtraEffectCardPile.AllPiles => PileType.None,
			CardExtraEffectCardPile.ExhaustPile => PileType.Exhaust,
			CardExtraEffectCardPile.Deck => PileType.Deck,
			_ => PileType.Discard
		};
	}

	private static CardPilePosition ResolvePilePosition(CardExtraEffectCardPilePosition position)
	{
		return position switch
		{
			CardExtraEffectCardPilePosition.Top => CardPilePosition.Top,
			CardExtraEffectCardPilePosition.Random => CardPilePosition.Random,
			_ => CardPilePosition.Bottom
		};
	}

	private static bool SupportsHistoryScaling(CardExtraEffectKind kind)
	{
		return kind is not CardExtraEffectKind.CreatedCardsCostLess
			and not CardExtraEffectKind.CreatedCardsUpgraded
			and not CardExtraEffectKind.DrawnCardsCostLess
			and not CardExtraEffectKind.GeneratedCardsCostLess
			and not CardExtraEffectKind.EndTurn
			and not CardExtraEffectKind.AutoPlaySelfFromPile
			and not CardExtraEffectKind.DrawCardsThatCostLess
			and not CardExtraEffectKind.AutoDrawSelfFromPile
			and not CardExtraEffectKind.ConditionalAutoPlayFromPile
			and not CardExtraEffectKind.ConditionalAutoDrawFromPile;
	}

	private static bool DoesCountConditionPass(int count, CardExtraEffect effect)
	{
		if (effect == null)
		{
			return true;
		}

		if (effect.CountComparison == CardExtraEffectCountComparison.None)
		{
			return count > 0 || (effect.ScaleMode == CardExtraEffectScaleMode.PerHistoryCount && effect.HistoryScalingIncludesBase);
		}

		int threshold = Math.Max(0, effect.CountConditionAmount);
		return effect.CountComparison switch
		{
			CardExtraEffectCountComparison.AtLeast => count >= threshold,
			CardExtraEffectCountComparison.AtMost => count <= threshold,
			CardExtraEffectCountComparison.Exactly => count == threshold,
			_ => count > 0
		};
	}

	internal static int GetHistoryCountMultiplierForCardPlay(CombatState combatState, Creature ownerCreature, CardPlay cardPlay, CardExtraEffect effect)
	{
		return GetHistoryCountMultiplier(combatState, ownerCreature, cardPlay, effect);
	}

	private static bool IsPassiveCardCostsLessDefinition(CardExtraEffect effect)
	{
		if (effect == null || effect.Kind is not (CardExtraEffectKind.CardCostsLess or CardExtraEffectKind.CardStarCostsLess))
		{
			return false;
		}

		return effect.CardCostsLessMode switch
		{
			CardExtraEffectCardCostsLessMode.Passive => true,
			CardExtraEffectCardCostsLessMode.Triggered => false,
			_ => effect.Trigger == CardExtraEffectTrigger.OnPlay // legacy behavior
		};
	}

	private static bool IsTriggeredCardCostsLessDefinition(CardExtraEffect effect)
	{
		if (effect == null || effect.Kind is not (CardExtraEffectKind.CardCostsLess or CardExtraEffectKind.CardStarCostsLess))
		{
			return false;
		}

		return effect.CardCostsLessMode switch
		{
			CardExtraEffectCardCostsLessMode.Triggered => true,
			CardExtraEffectCardCostsLessMode.Passive => false,
			_ => effect.Trigger != CardExtraEffectTrigger.OnPlay // legacy behavior
		};
	}

	internal static void ApplyIntrinsicTimedCardCostsLessOnEnterHand(CombatState combatState, CardModel card)
	{
		if (combatState == null || card == null)
		{
			return;
		}

		if (!TryGetOverride(card.Id, out CardOverride? overrideData))
		{
			return;
		}

		IReadOnlyList<CardExtraEffect> effects;
		try
		{
			effects = GetEffectiveExtraEffects(card, overrideData!, card.CurrentUpgradeLevel > 0);
		}
		catch
		{
			return;
		}

		if (effects == null || effects.Count == 0)
		{
			return;
		}

		IReadOnlyList<CardExtraEffect> existingTemp = CardEditorTemporaryExtraEffectController.GetEffects(combatState, card);

		bool changed = false;
		foreach (CardExtraEffect effect in effects)
		{
			if (effect == null
				|| IsPowerEffect(effect)
				|| effect.GrantToCard
				|| effect.Kind is not (CardExtraEffectKind.CardCostsLess or CardExtraEffectKind.CardStarCostsLess)
				|| !IsPassiveCardCostsLessDefinition(effect)
				|| !IsValidEffectAmount(effect.Kind, effect.Amount)
				|| effect.CardCostsLessDuration == CardExtraEffectCardCostsLessDuration.Permanent)
			{
				continue;
			}

			if (existingTemp.Any(e => IsEquivalentTimedCardCostsLess(e, effect)))
			{
				continue;
			}

			(CardExtraEffectCardGrantDuration duration, int turns) = effect.CardCostsLessDuration switch
			{
				CardExtraEffectCardCostsLessDuration.ThisTurn => (CardExtraEffectCardGrantDuration.ThisTurn, 1),
				CardExtraEffectCardCostsLessDuration.ThisCombat => (CardExtraEffectCardGrantDuration.ThisCombat, 1),
				CardExtraEffectCardCostsLessDuration.UntilPlayed => (CardExtraEffectCardGrantDuration.UntilPlayed, 1),
				CardExtraEffectCardCostsLessDuration.Turns => (CardExtraEffectCardGrantDuration.Turns, Math.Clamp(effect.CardCostsLessTurns, 1, 99)),
				_ => (CardExtraEffectCardGrantDuration.ThisCombat, 1)
			};

			CardEditorTemporaryExtraEffectController.Grant(combatState, card, effect, duration, turns);
			changed = true;
		}

		if (changed)
		{
			card.InvokeEnergyCostChanged();
		}
	}

	private static bool IsEquivalentTimedCardCostsLess(CardExtraEffect? existing, CardExtraEffect? candidate)
	{
		if (existing == null || candidate == null)
		{
			return false;
		}
		if (existing.Kind != CardExtraEffectKind.CardCostsLess || candidate.Kind != CardExtraEffectKind.CardCostsLess)
		{
			return false;
		}

		return existing.Amount == candidate.Amount
			&& existing.ScaleMode == candidate.ScaleMode
			&& existing.CountEvent == candidate.CountEvent
			&& existing.CountWindow == candidate.CountWindow
			&& existing.CountWindowInclusion == candidate.CountWindowInclusion
			&& existing.BlockLostCountingMode == candidate.BlockLostCountingMode
			&& existing.CountTurns == candidate.CountTurns
			&& existing.CountCardPool == candidate.CountCardPool
			&& existing.CountCardType == candidate.CountCardType
			&& existing.CountCardFilter == candidate.CountCardFilter
			&& existing.CountOnlyBlockCards == candidate.CountOnlyBlockCards
			&& existing.CardCostsLessDuration == candidate.CardCostsLessDuration
			&& existing.CardCostsLessTurns == candidate.CardCostsLessTurns;
	}

	// Signed delta: positive reduces cost, negative increases cost.
	internal static int GetCardCostsLessReduction(CombatState combatState, CardModel card)
	{
		if (combatState == null || card == null)
		{
			return 0;
		}

		Player? owner = card.Owner;
		Creature? ownerCreature = owner?.Creature;
		if (ownerCreature == null)
		{
			return 0;
		}

		CardPlay? currentPlay = CardEditorCardPlayContext.Current;
		CardPlay playForHistory = (currentPlay != null && ReferenceEquals(currentPlay.Card, card))
			? currentPlay
			: new CardPlay
			{
				Card = card,
				Target = null,
				ResultPile = card.Pile?.Type ?? PileType.None,
				Resources = new ResourceInfo
				{
					EnergySpent = 0,
					EnergyValue = 0,
					StarsSpent = 0,
					StarValue = 0
				},
				IsAutoPlay = true,
				PlayIndex = 0,
				PlayCount = 1
			};

		long totalDelta = 0;

		void Accumulate(IReadOnlyList<CardExtraEffect> effects, bool includeTimedIntrinsic)
		{
			if (effects == null || effects.Count == 0)
			{
				return;
			}

			foreach (CardExtraEffect effect in effects)
			{
				if (effect == null
					|| IsPowerEffect(effect)
					|| effect.GrantToCard
					|| effect.Kind != CardExtraEffectKind.CardCostsLess
					|| !IsValidEffectAmount(effect.Kind, effect.Amount))
				{
					continue;
				}
				if (!includeTimedIntrinsic && !IsPassiveCardCostsLessDefinition(effect))
				{
					continue;
				}
				if (!includeTimedIntrinsic && effect.CardCostsLessDuration != CardExtraEffectCardCostsLessDuration.Permanent)
				{
					continue;
				}

				int delta = effect.AmountIsX
					? (effect.Amount < 0 ? -1 : 1) * Math.Max(0, ResolveEnergyXValueForCostEffect(card, playForHistory) + effect.AmountXPlus)
					: effect.Amount;
				if (effect.ScaleMode != CardExtraEffectScaleMode.None)
				{
					int multiplier = GetHistoryCountMultiplier(combatState, ownerCreature, playForHistory, effect);
					if (!DoesCountConditionPass(multiplier, effect))
					{
						continue;
					}
					if (effect.ScaleMode == CardExtraEffectScaleMode.PerHistoryCount)
					{
						long scaled = (long)delta * multiplier;
						if (scaled >= int.MaxValue)
						{
							delta = int.MaxValue;
						}
						else if (scaled <= -int.MaxValue)
						{
							delta = -int.MaxValue;
						}
						else
						{
							delta = (int)scaled;
						}
					}
				}

				int repeats = ResolveRepeatCount(playForHistory, effect);
				if (repeats <= 0)
				{
					continue;
				}
				if (repeats > 1)
				{
					long repeated = (long)delta * repeats;
					if (repeated >= int.MaxValue)
					{
						delta = int.MaxValue;
					}
					else if (repeated <= -int.MaxValue)
					{
						delta = -int.MaxValue;
					}
					else
					{
						delta = (int)repeated;
					}
				}

				if (delta == 0)
				{
					continue;
				}

				totalDelta += delta;
				if (totalDelta >= int.MaxValue)
				{
					totalDelta = int.MaxValue;
					return;
				}
				if (totalDelta <= -int.MaxValue)
				{
					totalDelta = -int.MaxValue;
					return;
				}
			}
		}

		if (CardEditorOverrides.TryGet(card.Id, out CardOverride overrideData))
		{
			Accumulate(GetEffectiveExtraEffects(card, overrideData, card.CurrentUpgradeLevel > 0), includeTimedIntrinsic: false);
		}

		Accumulate(CardEditorTemporaryExtraEffectController.GetEffects(combatState, card), includeTimedIntrinsic: true);

		if (totalDelta >= int.MaxValue)
		{
			return int.MaxValue;
		}
		if (totalDelta <= -int.MaxValue)
		{
			return -int.MaxValue;
		}
		return (int)totalDelta;
	}

	// Signed delta: positive reduces Star cost, negative increases Star cost.
	internal static int GetCardStarCostsLessReduction(CombatState combatState, CardModel card)
	{
		if (combatState == null || card == null)
		{
			return 0;
		}

		Player? owner = card.Owner;
		Creature? ownerCreature = owner?.Creature;
		if (ownerCreature == null)
		{
			return 0;
		}

		CardPlay? currentPlay = CardEditorCardPlayContext.Current;
		CardPlay playForHistory = (currentPlay != null && ReferenceEquals(currentPlay.Card, card))
			? currentPlay
			: new CardPlay
			{
				Card = card,
				Target = null,
				ResultPile = card.Pile?.Type ?? PileType.None,
				Resources = new ResourceInfo
				{
					EnergySpent = 0,
					EnergyValue = 0,
					StarsSpent = 0,
					StarValue = 0
				},
				IsAutoPlay = true,
				PlayIndex = 0,
				PlayCount = 1
			};

		long totalDelta = 0;

		void Accumulate(IReadOnlyList<CardExtraEffect> effects, bool includeTimedIntrinsic)
		{
			if (effects == null || effects.Count == 0)
			{
				return;
			}

			foreach (CardExtraEffect effect in effects)
			{
				if (effect == null
					|| IsPowerEffect(effect)
					|| effect.GrantToCard
					|| effect.Kind != CardExtraEffectKind.CardStarCostsLess
					|| !IsValidEffectAmount(effect.Kind, effect.Amount))
				{
					continue;
				}
				if (!includeTimedIntrinsic && !IsPassiveCardCostsLessDefinition(effect))
				{
					continue;
				}
				if (!includeTimedIntrinsic && effect.CardCostsLessDuration != CardExtraEffectCardCostsLessDuration.Permanent)
				{
					continue;
				}

				int delta = effect.AmountIsX
					? (effect.Amount < 0 ? -1 : 1) * Math.Max(0, ResolveStarXValueForCostEffect(card, playForHistory) + effect.AmountXPlus)
					: effect.Amount;
				if (effect.ScaleMode != CardExtraEffectScaleMode.None)
				{
					int multiplier = GetHistoryCountMultiplier(combatState, ownerCreature, playForHistory, effect);
					if (!DoesCountConditionPass(multiplier, effect))
					{
						continue;
					}
					if (effect.ScaleMode == CardExtraEffectScaleMode.PerHistoryCount)
					{
						long scaled = (long)delta * multiplier;
						if (scaled >= int.MaxValue)
						{
							delta = int.MaxValue;
						}
						else if (scaled <= -int.MaxValue)
						{
							delta = -int.MaxValue;
						}
						else
						{
							delta = (int)scaled;
						}
					}
				}

				int repeats = ResolveRepeatCount(playForHistory, effect);
				if (repeats <= 0)
				{
					continue;
				}
				if (repeats > 1)
				{
					long repeated = (long)delta * repeats;
					if (repeated >= int.MaxValue)
					{
						delta = int.MaxValue;
					}
					else if (repeated <= -int.MaxValue)
					{
						delta = -int.MaxValue;
					}
					else
					{
						delta = (int)repeated;
					}
				}

				if (delta == 0)
				{
					continue;
				}

				totalDelta += delta;
				if (totalDelta >= int.MaxValue)
				{
					totalDelta = int.MaxValue;
					return;
				}
				if (totalDelta <= -int.MaxValue)
				{
					totalDelta = -int.MaxValue;
					return;
				}
			}
		}

		if (CardEditorOverrides.TryGet(card.Id, out CardOverride overrideData))
		{
			Accumulate(GetEffectiveExtraEffects(card, overrideData, card.CurrentUpgradeLevel > 0), includeTimedIntrinsic: false);
		}

		Accumulate(CardEditorTemporaryExtraEffectController.GetEffects(combatState, card), includeTimedIntrinsic: true);

		if (totalDelta >= int.MaxValue)
		{
			return int.MaxValue;
		}
		if (totalDelta <= -int.MaxValue)
		{
			return -int.MaxValue;
		}
		return (int)totalDelta;
	}

	private static int ResolveEnergyXValueForCostEffect(CardModel card, CardPlay? play)
	{
		try
		{
			if (play != null && card != null && ReferenceEquals(play.Card, card))
			{
				return Math.Max(0, play.Resources.EnergyValue);
			}
		}
		catch
		{
			// ignored
		}

		try
		{
			return Math.Max(0, card?.EnergyCost?.Canonical ?? 0);
		}
		catch
		{
			return 0;
		}
	}

	private static bool TryResolveSignedAmountForCostEffect(CombatState combatState, Creature ownerCreature, CardPlay cardPlay, CardExtraEffect effect, out int signedAmount)
	{
		signedAmount = 0;
		if (combatState == null || ownerCreature == null || cardPlay == null || effect == null)
		{
			return false;
		}

		int baseAmount;
		if (effect.AmountIsX)
		{
			int x = ResolveXAmountWithPlus(cardPlay, effect.AmountXPlus);
			x = Math.Max(0, x);
			int sign = effect.Amount < 0 ? -1 : 1;
			baseAmount = sign * x;
		}
		else
		{
			baseAmount = effect.Amount;
		}

		if (baseAmount == 0)
		{
			return false;
		}

		int resolved = baseAmount;
		if (effect.ScaleMode != CardExtraEffectScaleMode.None)
		{
			int multiplier = GetHistoryCountMultiplier(combatState, ownerCreature, cardPlay, effect);
			if (!DoesCountConditionPass(multiplier, effect))
			{
				return false;
			}

			if (effect.ScaleMode == CardExtraEffectScaleMode.ConditionOnly)
			{
				multiplier = 1;
			}

			if (effect.ScaleMode == CardExtraEffectScaleMode.PerHistoryCount)
			{
				long scaled = (long)resolved * Math.Max(0, multiplier);
				long total = effect.HistoryScalingIncludesBase ? (long)resolved + scaled : scaled;
				if (total >= int.MaxValue)
				{
					resolved = int.MaxValue;
				}
				else if (total <= -int.MaxValue)
				{
					resolved = -int.MaxValue;
				}
				else
				{
					resolved = (int)total;
				}
			}
		}

		int repeats = ResolveRepeatCount(cardPlay, effect);
		if (repeats <= 0)
		{
			return false;
		}
		if (repeats > 1)
		{
			long repeated = (long)resolved * repeats;
			if (repeated >= int.MaxValue)
			{
				resolved = int.MaxValue;
			}
			else if (repeated <= -int.MaxValue)
			{
				resolved = -int.MaxValue;
			}
			else
			{
				resolved = (int)repeated;
			}
		}

		if (resolved == 0)
		{
			return false;
		}

		signedAmount = resolved;
		return true;
	}

	private static int ResolveStarXValueForCostEffect(CardModel card, CardPlay? play)
	{
		try
		{
			if (play != null && card != null && ReferenceEquals(play.Card, card))
			{
				return Math.Max(0, play.Resources.StarValue);
			}
		}
		catch
		{
			// ignored
		}

		try
		{
			int cost = card?.CurrentStarCost ?? -1;
			return Math.Max(0, cost);
		}
		catch
		{
			return 0;
		}
	}

	private static int GetHistoryCountMultiplier(CombatState combatState, Creature ownerCreature, CardPlay? cardPlay, CardExtraEffect effect)
	{
		try
		{
			if (combatState == null || ownerCreature == null || effect == null)
			{
				return 0;
			}

			if (effect.CountEvent == CardExtraEffectCountEvent.InPile)
			{
				Player? ownerPlayer = ownerCreature.Player;
				if (ownerPlayer == null)
				{
					return 0;
				}

				int inPileCount = 0;
				if (effect.CountCardPile == CardExtraEffectCardPile.AllPiles)
				{
					foreach (PileType countPileType in new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust })
					{
						CardPile? countPile = countPileType.GetPile(ownerPlayer);
						if (countPile == null)
						{
							continue;
						}

						foreach (CardModel? c in countPile.Cards)
						{
							if (c == null)
							{
								continue;
							}
							if (!MatchesCountCardFilters(ownerPlayer, c, effect))
							{
								continue;
							}
							inPileCount++;
						}
					}
					return inPileCount;
				}

				PileType pileType = ResolvePileType(effect.CountCardPile);
				CardPile? pile = pileType.GetPile(ownerPlayer);
				if (pile == null)
				{
					return 0;
				}

				foreach (CardModel? c in pile.Cards)
				{
					if (c == null)
					{
						continue;
					}
					if (!MatchesCountCardFilters(ownerPlayer, c, effect))
					{
						continue;
					}
					inPileCount++;
				}

				return inPileCount;
			}

			CombatHistory? history = CombatManager.Instance?.History;
			if (history != null && TryGetResourceHistoryCountMultiplier(history, combatState, ownerCreature, effect, out int resourceCount))
			{
				return resourceCount;
			}
			if (history == null
				&& effect.CountEvent is CardExtraEffectCountEvent.EnergyGained
					or CardExtraEffectCountEvent.EnergyLost
					or CardExtraEffectCountEvent.BlockLost
					or CardExtraEffectCountEvent.HealingReceived)
			{
				TryGetRecordedResourceCountMultiplier(combatState, ownerCreature, effect, out int recordedCount);
				return recordedCount;
			}

			Player? owner = ownerCreature.Player;
			if (owner == null)
			{
				return 0;
			}

			if (effect.CountEvent == CardExtraEffectCountEvent.CurrentOrbs)
			{
				return owner.PlayerCombatState?.OrbQueue?.Orbs?.Count(orb => orb != null && OrbMatchesType(orb, effect.CountOrbType)) ?? 0;
			}

			if (effect.CountEvent == CardExtraEffectCountEvent.EnemyHasStatus)
			{
				return GetRelevantEnemyConditionTargets(combatState, cardPlay)
					.Count(enemy => EnemyHasStatus(enemy, effect.CountEnemyStatus));
			}

			if (effect.CountEvent == CardExtraEffectCountEvent.EnemyIntent)
			{
				return GetRelevantEnemyConditionTargets(combatState, cardPlay)
					.Count(enemy => EnemyHasIntent(enemy, effect.CountEnemyIntent));
			}

			if (effect.CountEvent == CardExtraEffectCountEvent.PlayedCardEnergyCost)
			{
				return Math.Max(0, cardPlay?.Resources.EnergyValue ?? 0);
			}

			if (effect.CountEvent == CardExtraEffectCountEvent.EmptyOrbSlots)
			{
				OrbQueue? orbQueue = owner.PlayerCombatState?.OrbQueue;
				if (orbQueue == null)
				{
					return 0;
				}
				return Math.Max(0, orbQueue.Capacity - orbQueue.Orbs.Count);
			}

			if (effect.CountEvent == CardExtraEffectCountEvent.OrbInPosition)
			{
				OrbModel? orb = GetOrbInPosition(owner, effect.CountOrbSelection);
				if (orb == null)
				{
					return 0;
				}
				return OrbMatchesType(orb, effect.CountOrbType) ? 1 : 0;
			}

			if (effect.CountEvent == CardExtraEffectCountEvent.OrbChanneled)
			{
				if (history == null)
				{
					return 0;
				}
				return history.Entries
					.OfType<OrbChanneledEntry>()
					.Count(e => e != null
						&& ReferenceEquals(e.Actor, ownerCreature)
						&& MatchesCountWindow(e, combatState, effect)
						&& e.Orb != null
						&& OrbMatchesType(e.Orb, effect.CountOrbType));
			}

			if (effect.CountEvent == CardExtraEffectCountEvent.OrbEvoked)
			{
				if (!_orbEvokeHistory.TryGetValue(combatState, out List<OrbEvokeCountEntry>? orbEntries))
				{
					return 0;
				}

				return orbEntries.Count(e => e != null
					&& ReferenceEquals(e.Actor, ownerCreature)
					&& MatchesCountWindow(e.RoundNumber, combatState, effect)
					&& e.Orb != null
					&& OrbMatchesType(e.Orb, effect.CountOrbType));
			}

			if (history == null)
			{
				return 0;
			}

			IEnumerable<CardModel> cards = effect.CountEvent switch
			{
				CardExtraEffectCountEvent.Drawn => history.Entries
					.OfType<CardDrawnEntry>()
					.Where(e => e != null && ReferenceEquals(e.Actor, ownerCreature) && MatchesCountWindow(e, combatState, effect))
					.Select(e => e.Card),
				CardExtraEffectCountEvent.Discarded => history.Entries
					.OfType<CardDiscardedEntry>()
					.Where(e => e != null && ReferenceEquals(e.Actor, ownerCreature) && MatchesCountWindow(e, combatState, effect))
					.Select(e => e.Card),
				CardExtraEffectCountEvent.Exhausted => history.Entries
					.OfType<CardExhaustedEntry>()
					.Where(e => e != null && ReferenceEquals(e.Actor, ownerCreature) && MatchesCountWindow(e, combatState, effect))
					.Select(e => e.Card),
				CardExtraEffectCountEvent.Generated => history.Entries
					.OfType<CardGeneratedEntry>()
					.Where(e => e != null && e.GeneratedByPlayer && ReferenceEquals(e.Actor, ownerCreature) && MatchesCountWindow(e, combatState, effect))
					.Select(e => e.Card),
				_ => history.Entries
					.OfType<CardPlayStartedEntry>()
					.Where(e => e != null
						&& (cardPlay == null || !ReferenceEquals(e.CardPlay, cardPlay))
						&& ReferenceEquals(e.Actor, ownerCreature)
						&& MatchesCountWindow(e, combatState, effect))
					.Select(e => e.CardPlay.Card)
			};

			int count = 0;
			foreach (CardModel? historyCard in cards)
			{
				if (historyCard == null)
				{
					continue;
				}
				if (!MatchesCountCardFilters(owner, historyCard, effect))
				{
					continue;
				}
				count++;
			}

			return count;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed computing history scaling multiplier: {ex}");
			return 0;
		}
	}

	private static bool TryGetResourceHistoryCountMultiplier(CombatHistory history, CombatState combatState, Creature ownerCreature, CardExtraEffect effect, out int multiplier)
	{
		multiplier = 0;
		switch (effect.CountEvent)
		{
			case CardExtraEffectCountEvent.StarsGained:
			{
				long gainedStars = history.Entries
					.OfType<StarsModifiedEntry>()
					.Where(e => e != null
						&& ReferenceEquals(e.Actor, ownerCreature)
						&& MatchesCountWindow(e, combatState, effect)
						&& e.Amount > 0)
					.Sum(e => (long)e.Amount);
				multiplier = ClampLongToInt(gainedStars);
				return true;
			}
			case CardExtraEffectCountEvent.StarsLost:
			{
				long lostStars = history.Entries
					.OfType<StarsModifiedEntry>()
					.Where(e => e != null
						&& ReferenceEquals(e.Actor, ownerCreature)
						&& MatchesCountWindow(e, combatState, effect)
						&& e.Amount < 0)
					.Sum(e => -(long)e.Amount);
				multiplier = ClampLongToInt(lostStars);
				return true;
			}
			case CardExtraEffectCountEvent.EnergyUsed:
			{
				long usedEnergy = history.Entries
					.OfType<EnergySpentEntry>()
					.Where(e => e != null
						&& ReferenceEquals(e.Actor, ownerCreature)
						&& MatchesCountWindow(e, combatState, effect)
						&& e.Amount > 0)
					.Sum(e => (long)e.Amount);
				multiplier = ClampLongToInt(usedEnergy);
				return true;
			}
			case CardExtraEffectCountEvent.BlockGained:
			{
				long gainedBlock = history.Entries
					.OfType<BlockGainedEntry>()
					.Where(e => e != null
						&& ReferenceEquals(e.Actor, ownerCreature)
						&& MatchesCountWindow(e, combatState, effect)
						&& e.Amount > 0)
					.Sum(e => (long)e.Amount);
				multiplier = ClampLongToInt(gainedBlock);
				return true;
			}
			case CardExtraEffectCountEvent.StatusGained:
			{
				long gainedStatus = history.Entries
					.OfType<PowerReceivedEntry>()
					.Where(e => e != null
						&& ReferenceEquals(e.Actor, ownerCreature)
						&& MatchesCountWindow(e, combatState, effect)
						&& e.Amount > 0
						&& PowerMatchesStatus(e.Power, effect.CountEnemyStatus))
					.Sum(e => (long)e.Amount);
				multiplier = ClampLongToInt(gainedStatus);
				return true;
			}
			case CardExtraEffectCountEvent.StatusLost:
			{
				long lostStatus = history.Entries
					.OfType<PowerReceivedEntry>()
					.Where(e => e != null
						&& ReferenceEquals(e.Actor, ownerCreature)
						&& MatchesCountWindow(e, combatState, effect)
						&& e.Amount < 0
						&& PowerMatchesStatus(e.Power, effect.CountEnemyStatus))
					.Sum(e => (long)Math.Abs(e.Amount));
				multiplier = ClampLongToInt(lostStatus);
				return true;
			}
			case CardExtraEffectCountEvent.DamageDealt:
			{
				long dealtDamage = history.Entries
					.OfType<DamageReceivedEntry>()
					.Where(e => e != null
						&& ReferenceEquals(e.Dealer, ownerCreature)
						&& MatchesCountWindow(e, combatState, effect))
					.Sum(e => (long)Math.Max(0, e.Result.TotalDamage));
				multiplier = ClampLongToInt(dealtDamage);
				return true;
			}
			case CardExtraEffectCountEvent.DamageTaken:
			{
				long takenDamage = history.Entries
					.OfType<DamageReceivedEntry>()
					.Where(e => e != null
						&& ReferenceEquals(e.Actor, ownerCreature)
						&& MatchesCountWindow(e, combatState, effect))
					.Sum(e => (long)Math.Max(0, e.Result.TotalDamage));
				multiplier = ClampLongToInt(takenDamage);
				return true;
			}
			case CardExtraEffectCountEvent.TimesLostHp:
			{
				long timesHit = history.Entries
					.OfType<DamageReceivedEntry>()
					.Count(e => e != null
						&& ReferenceEquals(e.Actor, ownerCreature)
						&& MatchesCountWindow(e, combatState, effect)
						&& e.Result.TotalDamage > 0);
				multiplier = ClampLongToInt(timesHit);
				return true;
			}
			case CardExtraEffectCountEvent.TimesGainedHp:
			{
				if (!_resourceCountHistory.TryGetValue(combatState, out List<ResourceCountEntry>? rcEntries))
				{
					multiplier = 0;
					return true;
				}
				long timesHealed = rcEntries
					.Count(e => e != null
						&& e.CountEvent == CardExtraEffectCountEvent.HealingReceived
						&& ReferenceEquals(e.Actor, ownerCreature)
						&& MatchesCountWindow(e.RoundNumber, combatState, effect)
						&& e.Amount > 0);
				multiplier = ClampLongToInt(timesHealed);
				return true;
			}
			case CardExtraEffectCountEvent.OstyAttacked:
			{
				Creature? osty = ownerCreature.Player?.Osty;
				if (osty == null)
				{
					multiplier = 0;
					return true;
				}
				long timesOstyAttacked = history.Entries
					.OfType<CreatureAttackedEntry>()
					.Count(e => e != null
						&& ReferenceEquals(e.Actor, osty)
						&& MatchesCountWindow(e, combatState, effect));
				multiplier = ClampLongToInt(timesOstyAttacked);
				return true;
			}
			case CardExtraEffectCountEvent.Summoned:
			{
				long summons = history.Entries
					.OfType<SummonedEntry>()
					.Where(e => e != null
						&& ReferenceEquals(e.Actor, ownerCreature)
						&& MatchesCountWindow(e, combatState, effect)
						&& e.Amount > 0)
					.Sum(e => (long)e.Amount);
				multiplier = ClampLongToInt(summons);
				return true;
			}
			case CardExtraEffectCountEvent.EnergyGained:
			case CardExtraEffectCountEvent.EnergyLost:
			case CardExtraEffectCountEvent.BlockLost:
			case CardExtraEffectCountEvent.HealingReceived:
				return TryGetRecordedResourceCountMultiplier(combatState, ownerCreature, effect, out multiplier);
			default:
				return false;
		}
	}

	private static bool TryGetRecordedResourceCountMultiplier(CombatState combatState, Creature ownerCreature, CardExtraEffect effect, out int multiplier)
	{
		multiplier = 0;
		if (!_resourceCountHistory.TryGetValue(combatState, out List<ResourceCountEntry>? resourceEntries))
		{
			return true;
		}

		bool includeBetweenTurns = effect.CountEvent != CardExtraEffectCountEvent.BlockLost
			|| effect.BlockLostCountingMode == CardExtraEffectBlockLostCountingMode.IncludeBetweenTurns;

		long total = resourceEntries
			.Where(e => e != null
				&& e.CountEvent == effect.CountEvent
				&& ReferenceEquals(e.Actor, ownerCreature)
				&& MatchesCountWindow(e.RoundNumber, combatState, effect)
				&& (includeBetweenTurns || e.Source != ResourceCountSource.BetweenTurnsBlockClear))
			.Sum(e => (long)e.Amount);
		multiplier = ClampLongToInt(total);
		return true;
	}

	private static int ClampLongToInt(long value)
	{
		if (value <= 0)
		{
			return 0;
		}
		return value >= int.MaxValue ? int.MaxValue : (int)value;
	}

	private static bool MatchesCountWindow(CombatHistoryEntry entry, CombatState combatState, CardExtraEffect effect)
	{
		if (!CountEventUsesWindow(effect.CountEvent))
		{
			return true;
		}

		return MatchesCountWindow(entry.RoundNumber, combatState, effect);
	}

	private static bool MatchesCountWindow(int roundNumber, CombatState combatState, CardExtraEffect effect)
	{
		if (!CountEventUsesWindow(effect.CountEvent))
		{
			return true;
		}
		int turns = Math.Max(1, effect.CountTurns);
		return effect.CountWindow switch
		{
			CardExtraEffectCountWindow.ThisTurn => roundNumber == combatState.RoundNumber,
			CardExtraEffectCountWindow.LastTurns => effect.CountWindowInclusion == CardExtraEffectCountWindowInclusion.ExcludeThisTurn
				? roundNumber < combatState.RoundNumber && roundNumber >= combatState.RoundNumber - turns
				: roundNumber >= combatState.RoundNumber - (turns - 1),
			_ => true
		};
	}

	private static bool MatchesCountCardFilters(Player owner, CardModel card, CardExtraEffect effect)
	{
		if (card == null || owner == null || effect == null)
		{
			return false;
		}

		CardExtraEffectCountCardFilter filter = effect.CountCardFilter;
		if (filter == CardExtraEffectCountCardFilter.Any && effect.CountOnlyBlockCards)
		{
			filter = CardExtraEffectCountCardFilter.GainBlock;
		}

		if (filter != CardExtraEffectCountCardFilter.Any && !MatchesCountCardEffectFilter(card, filter))
		{
			return false;
		}

		if (!MatchesCountPool(owner, card, effect.CountCardPool))
		{
			return false;
		}

		if (effect.CountCardType != CardGeneratedCardType.Any)
		{
			CardType effectiveType = GetEffectiveCardType(card);
			if (effect.CountCardType == CardGeneratedCardType.Playable)
			{
				if (effectiveType is not (CardType.Attack or CardType.Skill or CardType.Power))
				{
					return false;
				}
			}
			else
			{
				CardType desired = effect.CountCardType switch
				{
					CardGeneratedCardType.Attack => CardType.Attack,
					CardGeneratedCardType.Skill => CardType.Skill,
					CardGeneratedCardType.Power => CardType.Power,
					CardGeneratedCardType.Status => CardType.Status,
					CardGeneratedCardType.Curse => CardType.Curse,
					CardGeneratedCardType.Quest => CardType.Quest,
					_ => CardType.Attack
				};

				if (effectiveType != desired)
				{
					return false;
				}
			}
		}

		return true;
	}

	private static OrbModel? GetOrbInPosition(Player player, CardExtraEffectOrbSelection selection)
	{
		IReadOnlyList<OrbModel>? orbs = player?.PlayerCombatState?.OrbQueue?.Orbs;
		if (orbs == null || orbs.Count == 0)
		{
			return null;
		}

		return selection switch
		{
			CardExtraEffectOrbSelection.Rightmost => orbs[orbs.Count - 1],
			CardExtraEffectOrbSelection.Middle => orbs[(orbs.Count - 1) / 2],
			_ => orbs[0]
		};
	}

	internal static void RecordOrbEvoked(CombatState combatState, OrbModel orb)
	{
		if (combatState == null || orb?.Owner?.Creature == null)
		{
			return;
		}

		List<OrbEvokeCountEntry> entries = _orbEvokeHistory.GetOrCreateValue(combatState);
		entries.Add(new OrbEvokeCountEntry
		{
			Actor = orb.Owner.Creature,
			Orb = orb,
			RoundNumber = combatState.RoundNumber,
			CurrentSide = combatState.CurrentSide
		});
	}

	internal static void RecordResourceCount(CombatState combatState, Creature actor, CardExtraEffectCountEvent countEvent, int amount)
	{
		RecordResourceCount(combatState, actor, countEvent, amount, ResourceCountSource.Other);
	}

	internal static void RecordBetweenTurnsBlockClear(CombatState combatState, Creature actor, int amount)
	{
		RecordResourceCount(combatState, actor, CardExtraEffectCountEvent.BlockLost, amount, ResourceCountSource.BetweenTurnsBlockClear);
	}

	internal static Task TriggerPowerCountEventAsync(CombatState combatState, Creature actor, CardExtraEffectCountEvent countEvent, ResourceCountSource source = ResourceCountSource.Other)
	{
		if (combatState == null || actor == null || !actor.IsPlayer)
		{
			return Task.CompletedTask;
		}

		CardEditorExtraEffectPower? power = actor.GetPower<CardEditorExtraEffectPower>();
		if (power == null)
		{
			return Task.CompletedTask;
		}

		ulong? netId = LocalContext.NetId;
		if (!netId.HasValue)
		{
			return Task.CompletedTask;
		}

		HookPlayerChoiceContext choiceContext = new HookPlayerChoiceContext(actor.Player, netId.Value, GameActionType.Combat);
		return power.TriggerCountEvent(choiceContext, countEvent, source);
	}

	internal static void TriggerPowerCountEvent(CombatState combatState, Creature actor, CardExtraEffectCountEvent countEvent, ResourceCountSource source = ResourceCountSource.Other)
	{
		TaskHelper.RunSafely(TriggerPowerCountEventAsync(combatState, actor, countEvent, source));
	}

	private static void RecordResourceCount(CombatState combatState, Creature actor, CardExtraEffectCountEvent countEvent, int amount, ResourceCountSource source)
	{
		if (combatState == null || actor == null || amount <= 0)
		{
			return;
		}

		List<ResourceCountEntry> entries = _resourceCountHistory.GetOrCreateValue(combatState);
		entries.Add(new ResourceCountEntry
		{
			Actor = actor,
			CountEvent = countEvent,
			Amount = amount,
			RoundNumber = combatState.RoundNumber,
			CurrentSide = combatState.CurrentSide,
			Source = source
		});
	}

	internal static void ClearOrbCountHistory(CombatState combatState)
	{
		if (combatState == null)
		{
			return;
		}

		_orbEvokeHistory.Remove(combatState);
	}

	internal static void ClearResourceCountHistory(CombatState combatState)
	{
		if (combatState == null)
		{
			return;
		}

		_resourceCountHistory.Remove(combatState);
	}

	private static bool MatchesGrantCardFilters(Player owner, CardModel card, CardExtraEffect effect)
	{
		if (card == null || owner == null || effect == null)
		{
			return false;
		}

		CardExtraEffectCountCardFilter filter = effect.CardSelectionFilter;
		if (filter != CardExtraEffectCountCardFilter.Any && !MatchesCountCardEffectFilter(card, filter))
		{
			return false;
		}

		if (!MatchesCountPool(owner, card, effect.CardSelectionPool))
		{
			return false;
		}

		if (effect.CardSelectionType != CardGeneratedCardType.Any)
		{
			CardType effectiveType = GetEffectiveCardType(card);
			if (effect.CardSelectionType == CardGeneratedCardType.Playable)
			{
				if (effectiveType is not (CardType.Attack or CardType.Skill or CardType.Power))
				{
					return false;
				}
			}
			else
			{
				CardType desired = effect.CardSelectionType switch
				{
					CardGeneratedCardType.Attack => CardType.Attack,
					CardGeneratedCardType.Skill => CardType.Skill,
					CardGeneratedCardType.Power => CardType.Power,
					CardGeneratedCardType.Status => CardType.Status,
					CardGeneratedCardType.Curse => CardType.Curse,
					CardGeneratedCardType.Quest => CardType.Quest,
					_ => CardType.Attack
				};

				if (effectiveType != desired)
				{
					return false;
				}
			}
		}

		return true;
	}

	internal static bool MatchesPowerTriggerCardFilters(Player owner, CardModel triggeringCard, CardExtraEffect effect)
	{
		if (owner == null || triggeringCard == null || effect == null)
		{
			return true;
		}

		if (!IsPowerEffect(effect))
		{
			return true;
		}

		// Timed powers don't have a "triggering card" filter.
		if (effect.Trigger is CardExtraEffectTrigger.TurnBoundary
			or CardExtraEffectTrigger.EndOfTurnInHand
			or CardExtraEffectTrigger.StartOfTurn
			or CardExtraEffectTrigger.EndOfTurn
			or CardExtraEffectTrigger.StartOfEnemyTurn
			or CardExtraEffectTrigger.EndOfEnemyTurn)
		{
			return true;
		}

		CardExtraEffectCountCardFilter filter = effect.TriggerCardFilter;
		if (filter != CardExtraEffectCountCardFilter.Any && !MatchesCountCardEffectFilter(triggeringCard, filter))
		{
			return false;
		}

		if (!MatchesCountPool(owner, triggeringCard, effect.TriggerCardPool))
		{
			return false;
		}

		if (effect.TriggerCardType != CardGeneratedCardType.Any)
		{
			CardType effectiveType = GetEffectiveCardType(triggeringCard);
			if (effect.TriggerCardType == CardGeneratedCardType.Playable)
			{
				if (effectiveType is not (CardType.Attack or CardType.Skill or CardType.Power))
				{
					return false;
				}
			}
			else
			{
				CardType desired = effect.TriggerCardType switch
				{
					CardGeneratedCardType.Attack => CardType.Attack,
					CardGeneratedCardType.Skill => CardType.Skill,
					CardGeneratedCardType.Power => CardType.Power,
					CardGeneratedCardType.Status => CardType.Status,
					CardGeneratedCardType.Curse => CardType.Curse,
					CardGeneratedCardType.Quest => CardType.Quest,
					_ => CardType.Attack
				};

				if (effectiveType != desired)
				{
					return false;
				}
			}
		}

		return true;
	}

	internal static bool MatchesAffectedCardFilters(Player owner, CardModel card, CardExtraEffect effect)
	{
		if (owner == null || card == null || effect == null)
		{
			return true;
		}

		CardExtraEffectCountCardFilter filter = effect.TriggerCardFilter;
		if (filter != CardExtraEffectCountCardFilter.Any && !MatchesCountCardEffectFilter(card, filter))
		{
			return false;
		}

		if (!MatchesCountPool(owner, card, effect.TriggerCardPool))
		{
			return false;
		}

		CardGeneratedCardType type = effect.TriggerCardType;
		if (type != CardGeneratedCardType.Any)
		{
			CardType effectiveType = GetEffectiveCardType(card);
			if (type == CardGeneratedCardType.Playable)
			{
				if (effectiveType is not (CardType.Attack or CardType.Skill or CardType.Power))
				{
					return false;
				}
			}
			else
			{
				CardType desired = type switch
				{
					CardGeneratedCardType.Attack => CardType.Attack,
					CardGeneratedCardType.Skill => CardType.Skill,
					CardGeneratedCardType.Power => CardType.Power,
					CardGeneratedCardType.Status => CardType.Status,
					CardGeneratedCardType.Curse => CardType.Curse,
					CardGeneratedCardType.Quest => CardType.Quest,
					_ => CardType.Attack
				};

				if (effectiveType != desired)
				{
					return false;
				}
			}
		}

		return true;
	}

	private static CardType GetEffectiveCardType(CardModel card)
	{
		if (card == null)
		{
			return CardType.None;
		}

		try
		{
			CardType type = card.Type;
			if (type != CardType.None)
			{
				return type;
			}
		}
		catch
		{
			// ignored
		}

		try
		{
			// Some special cards can report Type=None but still have a meaningful rarity bucket.
			// Use rarity as a best-effort fallback so type-based filters work for Status/Curse/Quest cards.
			CardType fromRarity = card.Rarity switch
			{
				CardRarity.Status => CardType.Status,
				CardRarity.Curse => CardType.Curse,
				CardRarity.Quest => CardType.Quest,
				_ => CardType.None
			};
			if (fromRarity != CardType.None)
			{
				return fromRarity;
			}
		}
		catch
		{
			// ignored
		}

		try
		{
			CardModel? cursor = card.CloneOf;
			for (int i = 0; i < 8 && cursor != null; i++)
			{
				CardType type = cursor.Type;
				if (type != CardType.None)
				{
					return type;
				}
				cursor = cursor.CloneOf;
			}
		}
		catch
		{
			// ignored
		}

		try
		{
			return ModelDb.GetById<CardModel>(card.Id).Type;
		}
		catch
		{
			return CardType.None;
		}
	}

	private static bool MatchesCountCardEffectFilter(CardModel card, CardExtraEffectCountCardFilter filter)
	{
		bool HasExtraEffectKind(CardExtraEffectKind kind)
		{
			try
			{
				foreach (CardExtraEffect effect in GetEffectsForDescription(card, isUpgradePreview: false))
				{
					if (effect.Kind == kind)
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

		bool HasAnyExtraEffectKind(params CardExtraEffectKind[] kinds)
		{
			try
			{
				foreach (CardExtraEffect effect in GetEffectsForDescription(card, isUpgradePreview: false))
				{
					for (int i = 0; i < kinds.Length; i++)
					{
						if (effect.Kind == kinds[i])
						{
							return true;
						}
					}
				}
			}
			catch
			{
			}
			return false;
		}

		switch (filter)
		{
			case CardExtraEffectCountCardFilter.GainBlock:
				return CardGainsBlock(card) || HasExtraEffectKind(CardExtraEffectKind.GainBlock);
			case CardExtraEffectCountCardFilter.DealDamage:
				return HasAnyDamageVar(card) || HasExtraEffectKind(CardExtraEffectKind.DealDamage);
			case CardExtraEffectCountCardFilter.DrawCards:
				return HasDynamicVar(card, "Cards") || HasExtraEffectKind(CardExtraEffectKind.DrawCards);
			case CardExtraEffectCountCardFilter.GainEnergy:
				return HasDynamicVar(card, "Energy") || HasExtraEffectKind(CardExtraEffectKind.GainEnergy);
			case CardExtraEffectCountCardFilter.GainStars:
				return HasDynamicVar(card, "Stars") || HasExtraEffectKind(CardExtraEffectKind.GainStars);
			case CardExtraEffectCountCardFilter.Heal:
				return HasDynamicVar(card, "Heal") || HasExtraEffectKind(CardExtraEffectKind.Heal);
			case CardExtraEffectCountCardFilter.LoseHp:
				return HasDynamicVar(card, "HpLoss") || HasExtraEffectKind(CardExtraEffectKind.LoseHp);
			case CardExtraEffectCountCardFilter.Strength:
				return HasDynamicVar(card, "StrengthPower")
					|| HasPowerHoverTip(card, "StrengthPower")
					|| HasAnyExtraEffectKind(CardExtraEffectKind.GainStrength, CardExtraEffectKind.LoseStrength);
			case CardExtraEffectCountCardFilter.Dexterity:
				return HasDynamicVar(card, "DexterityPower")
					|| HasPowerHoverTip(card, "DexterityPower")
					|| HasAnyExtraEffectKind(CardExtraEffectKind.GainDexterity, CardExtraEffectKind.LoseDexterity);
			case CardExtraEffectCountCardFilter.Focus:
				return HasDynamicVar(card, "FocusPower")
					|| HasPowerHoverTip(card, "FocusPower")
					|| HasAnyExtraEffectKind(CardExtraEffectKind.GainFocus, CardExtraEffectKind.LoseFocus);
			case CardExtraEffectCountCardFilter.Weak:
				return HasDynamicVar(card, "WeakPower") || HasPowerHoverTip(card, "WeakPower") || HasExtraEffectKind(CardExtraEffectKind.ApplyWeak);
			case CardExtraEffectCountCardFilter.Frail:
				return HasDynamicVar(card, "FrailPower") || HasPowerHoverTip(card, "FrailPower") || HasExtraEffectKind(CardExtraEffectKind.ApplyFrail);
			case CardExtraEffectCountCardFilter.Vulnerable:
				return HasDynamicVar(card, "VulnerablePower") || HasPowerHoverTip(card, "VulnerablePower") || HasExtraEffectKind(CardExtraEffectKind.ApplyVulnerable);
			case CardExtraEffectCountCardFilter.Poison:
				return HasDynamicVar(card, "PoisonPower") || HasPowerHoverTip(card, "PoisonPower") || HasExtraEffectKind(CardExtraEffectKind.ApplyPoison);
			case CardExtraEffectCountCardFilter.Doom:
				return HasDynamicVar(card, "DoomPower") || HasPowerHoverTip(card, "DoomPower") || HasExtraEffectKind(CardExtraEffectKind.ApplyDoom);
			case CardExtraEffectCountCardFilter.Constrict:
				return HasDynamicVar(card, "ConstrictPower") || HasPowerHoverTip(card, "ConstrictPower") || HasExtraEffectKind(CardExtraEffectKind.ApplyConstrict);
			case CardExtraEffectCountCardFilter.Artifact:
				return HasDynamicVar(card, "ArtifactPower") || HasPowerHoverTip(card, "ArtifactPower") || HasExtraEffectKind(CardExtraEffectKind.GainArtifact);
			case CardExtraEffectCountCardFilter.Thorns:
				return HasDynamicVar(card, "ThornsPower") || HasPowerHoverTip(card, "ThornsPower") || HasExtraEffectKind(CardExtraEffectKind.GainThorns);
			case CardExtraEffectCountCardFilter.Regen:
				return HasDynamicVar(card, "RegenPower") || HasPowerHoverTip(card, "RegenPower") || HasExtraEffectKind(CardExtraEffectKind.GainRegen);
			case CardExtraEffectCountCardFilter.Plating:
				return HasDynamicVar(card, "PlatingPower") || HasPowerHoverTip(card, "PlatingPower") || HasExtraEffectKind(CardExtraEffectKind.GainPlating);
			case CardExtraEffectCountCardFilter.Intangible:
				return HasDynamicVar(card, "IntangiblePower") || HasPowerHoverTip(card, "IntangiblePower") || HasExtraEffectKind(CardExtraEffectKind.GainIntangible);
			case CardExtraEffectCountCardFilter.Buffer:
				return HasDynamicVar(card, "BufferPower") || HasPowerHoverTip(card, "BufferPower") || HasExtraEffectKind(CardExtraEffectKind.GainBuffer);
			case CardExtraEffectCountCardFilter.Vigor:
				return HasDynamicVar(card, "VigorPower") || HasPowerHoverTip(card, "VigorPower") || HasExtraEffectKind(CardExtraEffectKind.GainVigor);
			case CardExtraEffectCountCardFilter.Blur:
				return HasDynamicVar(card, "BlurPower") || HasPowerHoverTip(card, "BlurPower") || HasExtraEffectKind(CardExtraEffectKind.GainBlur);
			case CardExtraEffectCountCardFilter.Ritual:
				return HasDynamicVar(card, "RitualPower") || HasPowerHoverTip(card, "RitualPower") || HasExtraEffectKind(CardExtraEffectKind.GainRitual);
			case CardExtraEffectCountCardFilter.Summon:
				return HasDynamicVar(card, "Summon") || HasExtraEffectKind(CardExtraEffectKind.Summon);
			case CardExtraEffectCountCardFilter.Forge:
				return HasDynamicVar(card, "Forge") || HasExtraEffectKind(CardExtraEffectKind.Forge);
			case CardExtraEffectCountCardFilter.Exhaust:
				return card.Keywords.Contains(CardKeyword.Exhaust);
			case CardExtraEffectCountCardFilter.Ethereal:
				return card.Keywords.Contains(CardKeyword.Ethereal);
			case CardExtraEffectCountCardFilter.Innate:
				return card.Keywords.Contains(CardKeyword.Innate);
			case CardExtraEffectCountCardFilter.Retain:
				return card.Keywords.Contains(CardKeyword.Retain);
			case CardExtraEffectCountCardFilter.Sly:
				return card.Keywords.Contains(CardKeyword.Sly);
			case CardExtraEffectCountCardFilter.Eternal:
				return card.Keywords.Contains(CardKeyword.Eternal);
			default:
				return true;
		}
	}

	private static bool HasDynamicVar(CardModel card, string varName)
	{
		CardModel? cursor = card;
		for (int i = 0; i < 8 && cursor != null; i++)
		{
			try
			{
				if (cursor.DynamicVars.ContainsKey(varName))
				{
					return true;
				}
			}
			catch
			{
				// ignored
			}
			try
			{
				cursor = cursor.CloneOf;
			}
			catch
			{
				break;
			}
		}

		try
		{
			return ModelDb.GetById<CardModel>(card.Id).DynamicVars.ContainsKey(varName);
		}
		catch
		{
			return false;
		}
	}

	private static bool HasAnyDamageVar(CardModel card)
	{
		if (HasDynamicVar(card, "Damage")
			|| HasDynamicVar(card, "CalculatedDamage")
			|| HasDynamicVar(card, "ExtraDamage")
			|| HasDynamicVar(card, "OstyDamage"))
		{
			return true;
		}

		CardModel? cursor = card;
		for (int i = 0; i < 8 && cursor != null; i++)
		{
			try
			{
				foreach (string key in cursor.DynamicVars.Keys)
				{
					if (!string.IsNullOrEmpty(key) && key.Contains("Damage", StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
				}
			}
			catch
			{
				// ignored
			}

			try
			{
				cursor = cursor.CloneOf;
			}
			catch
			{
				break;
			}
		}

		try
		{
			foreach (string key in ModelDb.GetById<CardModel>(card.Id).DynamicVars.Keys)
			{
				if (!string.IsNullOrEmpty(key) && key.Contains("Damage", StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
		}
		catch
		{
			// ignored
		}

		return false;
	}

	private static bool HasPowerHoverTip(CardModel card, string powerTypeName)
	{
		CardModel? cursor = card;
		for (int i = 0; i < 8 && cursor != null; i++)
		{
			try
			{
				foreach (var tip in cursor.HoverTips)
				{
					string? canonicalName = tip?.CanonicalModel?.GetType().Name;
					if (!string.IsNullOrEmpty(canonicalName) && string.Equals(canonicalName, powerTypeName, StringComparison.Ordinal))
					{
						return true;
					}
				}
			}
			catch
			{
				// ignored
			}

			try
			{
				cursor = cursor.CloneOf;
			}
			catch
			{
				break;
			}
		}

		return false;
	}

	private static bool CardGainsBlock(CardModel card)
	{
		CardModel? cursor = card;
		for (int i = 0; i < 8 && cursor != null; i++)
		{
			try
			{
				if (cursor.GainsBlock)
				{
					return true;
				}
			}
			catch
			{
				// ignored
			}

			try
			{
				cursor = cursor.CloneOf;
			}
			catch
			{
				break;
			}
		}

		try
		{
			return ModelDb.GetById<CardModel>(card.Id).GainsBlock;
		}
		catch
		{
			return false;
		}
	}

	internal static bool MatchesCountPool(Player owner, CardModel card, CardGeneratedCardPool pool)
	{
		switch (pool)
		{
			case CardGeneratedCardPool.Any:
			case CardGeneratedCardPool.All:
				return true;
			case CardGeneratedCardPool.Ancient:
				return card.Rarity == CardRarity.Ancient;
			case CardGeneratedCardPool.Colorless:
				// Base game generally treats "colorless" as a property of the (visual) pool, which includes Tokens.
				return card.VisualCardPool != null && card.VisualCardPool.IsColorless;
			case CardGeneratedCardPool.Ironclad:
				return ReferenceEquals(card.Pool, ModelDb.CardPool<IroncladCardPool>());
			case CardGeneratedCardPool.Silent:
				return ReferenceEquals(card.Pool, ModelDb.CardPool<SilentCardPool>());
			case CardGeneratedCardPool.Defect:
				return ReferenceEquals(card.Pool, ModelDb.CardPool<DefectCardPool>());
			case CardGeneratedCardPool.Regent:
				return ReferenceEquals(card.Pool, ModelDb.CardPool<RegentCardPool>());
			case CardGeneratedCardPool.Necrobinder:
				return ReferenceEquals(card.Pool, ModelDb.CardPool<NecrobinderCardPool>());
			case CardGeneratedCardPool.OtherColors:
				return IsAnyClassPool(card.Pool) && !ReferenceEquals(card.Pool, owner.Character.CardPool);
			default:
				return ReferenceEquals(card.Pool, owner.Character.CardPool);
		}
	}

	private static bool IsAnyClassPool(CardPoolModel pool)
	{
		return pool != null
			&& (ReferenceEquals(pool, ModelDb.CardPool<IroncladCardPool>())
				|| ReferenceEquals(pool, ModelDb.CardPool<SilentCardPool>())
				|| ReferenceEquals(pool, ModelDb.CardPool<DefectCardPool>())
				|| ReferenceEquals(pool, ModelDb.CardPool<RegentCardPool>())
				|| ReferenceEquals(pool, ModelDb.CardPool<NecrobinderCardPool>()));
	}

	private static async Task AddRandomCardsToHand(CombatState combatState, Player owner, int amount, CardGeneratedCardPool pool, CardGeneratedCardType type)
	{
		if (combatState == null || owner == null || amount <= 0)
		{
			return;
		}

		List<CardModel> candidates = GetCardGenerationCandidates(owner, pool, type);
		if (candidates.Count == 0)
		{
			return;
		}

		for (int i = 0; i < amount; i++)
		{
			CardModel canonical = owner.RunState.Rng.CombatCardGeneration.NextItem(candidates);
			if (canonical == null)
			{
				continue;
			}

			CardModel generated = combatState.CreateCard(canonical, owner);
			await CardPileCmd.AddGeneratedCardToCombat(generated, PileType.Hand, addedByPlayer: true);
		}
	}

	private static async Task ChooseOneOfThreeCardsToHand(CombatState combatState, PlayerChoiceContext choiceContext, Player owner, int times, CardGeneratedCardPool pool, CardGeneratedCardType type)
	{
		if (combatState == null || choiceContext == null || owner == null)
		{
			return;
		}
		times = Math.Max(1, times);

		List<CardModel> candidates = GetCardGenerationCandidates(owner, pool, type);
		if (candidates.Count == 0)
		{
			return;
		}

		for (int i = 0; i < times; i++)
		{
			List<CardModel> options = new List<CardModel>();
			HashSet<ModelId> used = new HashSet<ModelId>();
			int tries = 0;
			while (options.Count < 3 && used.Count < candidates.Count && tries < 50)
			{
				tries++;
				CardModel canonical = owner.RunState.Rng.CombatCardGeneration.NextItem(candidates);
				if (canonical == null || used.Contains(canonical.Id))
				{
					continue;
				}
				used.Add(canonical.Id);
				options.Add(combatState.CreateCard(canonical, owner));
			}

			if (options.Count == 0)
			{
				return;
			}

			CardModel? selected = await CardSelectCmd.FromChooseACardScreen(choiceContext, options, owner, canSkip: true);
			if (selected != null)
			{
				await CardPileCmd.AddGeneratedCardToCombat(selected, PileType.Hand, addedByPlayer: true);
			}
		}
	}

	private static List<CardModel> GetCardGenerationCandidates(Player owner, CardGeneratedCardPool pool, CardGeneratedCardType type)
	{
		if (owner == null)
		{
			return new List<CardModel>();
		}

		IEnumerable<CardModel> baseCards = pool switch
		{
			CardGeneratedCardPool.Colorless => ModelDb.CardPool<ColorlessCardPool>().GetUnlockedCards(owner.UnlockState, owner.RunState.CardMultiplayerConstraint),
			CardGeneratedCardPool.Ironclad => ModelDb.CardPool<IroncladCardPool>().GetUnlockedCards(owner.UnlockState, owner.RunState.CardMultiplayerConstraint),
			CardGeneratedCardPool.Silent => ModelDb.CardPool<SilentCardPool>().GetUnlockedCards(owner.UnlockState, owner.RunState.CardMultiplayerConstraint),
			CardGeneratedCardPool.Defect => ModelDb.CardPool<DefectCardPool>().GetUnlockedCards(owner.UnlockState, owner.RunState.CardMultiplayerConstraint),
			CardGeneratedCardPool.Regent => ModelDb.CardPool<RegentCardPool>().GetUnlockedCards(owner.UnlockState, owner.RunState.CardMultiplayerConstraint),
			CardGeneratedCardPool.Necrobinder => ModelDb.CardPool<NecrobinderCardPool>().GetUnlockedCards(owner.UnlockState, owner.RunState.CardMultiplayerConstraint),
			CardGeneratedCardPool.OtherColors => GetOtherColorCards(owner),
			CardGeneratedCardPool.Any => GetAllClassCards(owner),
			CardGeneratedCardPool.All => GetAllCards(owner),
			CardGeneratedCardPool.Ancient => GetAllCards(owner).Where(c => c.Rarity == CardRarity.Ancient),
			_ => owner.Character.CardPool.GetUnlockedCards(owner.UnlockState, owner.RunState.CardMultiplayerConstraint)
		};

		IEnumerable<CardModel> filtered = baseCards.Where(c => c != null && c.CanBeGeneratedInCombat && c.CanBeGeneratedByModifiers);

		filtered = pool switch
		{
			CardGeneratedCardPool.Ancient => filtered.Where(c => c.Rarity == CardRarity.Ancient),
			CardGeneratedCardPool.All => filtered.Where(c => c.Rarity != CardRarity.Basic && c.Rarity != CardRarity.Event),
			_ => filtered.Where(c => c.Rarity != CardRarity.Basic && c.Rarity != CardRarity.Ancient && c.Rarity != CardRarity.Event)
		};

		if (type != CardGeneratedCardType.Any)
		{
			if (type == CardGeneratedCardType.Playable)
			{
				filtered = filtered.Where(c => c.Type is CardType.Attack or CardType.Skill or CardType.Power);
			}
			else
			{
				CardType desired = type switch
				{
					CardGeneratedCardType.Attack => CardType.Attack,
					CardGeneratedCardType.Skill => CardType.Skill,
					CardGeneratedCardType.Power => CardType.Power,
					CardGeneratedCardType.Status => CardType.Status,
					CardGeneratedCardType.Curse => CardType.Curse,
					CardGeneratedCardType.Quest => CardType.Quest,
					_ => CardType.Skill
				};
				filtered = filtered.Where(c => c.Type == desired);
			}
		}

		return filtered.Distinct().ToList();
	}

	private static IEnumerable<CardModel> GetAllClassCards(Player owner)
	{
		CardMultiplayerConstraint constraint = owner.RunState.CardMultiplayerConstraint;
		var unlockState = owner.UnlockState;

		return ModelDb.CardPool<IroncladCardPool>().GetUnlockedCards(unlockState, constraint)
			.Concat(ModelDb.CardPool<SilentCardPool>().GetUnlockedCards(unlockState, constraint))
			.Concat(ModelDb.CardPool<DefectCardPool>().GetUnlockedCards(unlockState, constraint))
			.Concat(ModelDb.CardPool<RegentCardPool>().GetUnlockedCards(unlockState, constraint))
			.Concat(ModelDb.CardPool<NecrobinderCardPool>().GetUnlockedCards(unlockState, constraint))
			.Distinct();
	}

	private static IEnumerable<CardModel> GetAllCards(Player owner)
	{
		CardMultiplayerConstraint constraint = owner.RunState.CardMultiplayerConstraint;
		var unlockState = owner.UnlockState;

		return GetAllClassCards(owner)
			.Concat(ModelDb.CardPool<ColorlessCardPool>().GetUnlockedCards(unlockState, constraint))
			.Distinct();
	}

	private static IEnumerable<CardModel> GetOtherColorCards(Player owner)
	{
		CardPoolModel myPool = owner.Character.CardPool;
		if (myPool == null)
		{
			return GetAllClassCards(owner);
		}

		return GetAllClassCards(owner).Where(c => c != null && !ReferenceEquals(c.Pool, myPool));
	}

	private static async Task ApplyPower<TPower>(CombatState combatState, Creature ownerCreature, CardPlay cardPlay, CardExtraEffectTarget target, int amount) where TPower : PowerModel
	{
		IReadOnlyList<Creature> targets = ResolveTargets(combatState, ownerCreature, cardPlay, target).ToList();
		if (targets.Count == 0)
		{
			return;
		}
		await PowerCmd.Apply<TPower>(targets, amount, ownerCreature, cardPlay.Card);
	}

	private static IEnumerable<Creature> ResolveTargets(CombatState combatState, Creature ownerCreature, CardPlay cardPlay, CardExtraEffectTarget target)
	{
		switch (target)
		{
			case CardExtraEffectTarget.Self:
				return new[] { ownerCreature };
			case CardExtraEffectTarget.AllEnemies:
				return combatState.GetOpponentsOf(ownerCreature).Where(c => c.IsAlive);
			case CardExtraEffectTarget.RandomEnemy:
			{
				Creature? picked = combatState.RunState.Rng.CombatTargets.NextItem(combatState.GetOpponentsOf(ownerCreature).Where(c => c.IsAlive));
				return picked != null ? new[] { picked } : Array.Empty<Creature>();
			}
			default:
			{
				Creature? picked = ResolveSingleTarget(combatState, ownerCreature, cardPlay);
				return picked != null ? new[] { picked } : Array.Empty<Creature>();
			}
		}
	}

	private static Creature? ResolveSingleTarget(CombatState combatState, Creature ownerCreature, CardPlay cardPlay)
	{
		if (cardPlay.Target != null && cardPlay.Target.IsAlive)
		{
			return cardPlay.Target;
		}
		if (TryGetManualTarget(cardPlay.Card, out Creature? manualTarget) && manualTarget != null && manualTarget.IsAlive)
		{
			return manualTarget;
		}
		if (cardPlay.IsFirstInSeries && RequiresManualEnemyTarget(cardPlay.Card))
		{
			Log.Warn($"[CardEditor] Missing manual target for {cardPlay.Card.Id.Entry}; falling back to random enemy.");
		}
		return combatState.RunState.Rng.CombatTargets.NextItem(combatState.GetOpponentsOf(ownerCreature).Where(c => c.IsAlive));
	}

	private static IReadOnlyList<CardExtraEffect> GetEffectiveExtraEffects(CardModel card, CardOverride? overrideData, bool considerUpgrade)
	{
		if (overrideData == null)
		{
			return Array.Empty<CardExtraEffect>();
		}

		IReadOnlyList<CardExtraEffect>? baseEffects = (overrideData.ExtraEffects != null && overrideData.ExtraEffects.Count > 0)
			? overrideData.ExtraEffects
			: null;

		if (!considerUpgrade
			|| overrideData.Upgrade == null
			|| overrideData.Upgrade.ExtraEffects == null
			|| overrideData.Upgrade.ExtraEffects.Count == 0)
		{
			return baseEffects ?? Array.Empty<CardExtraEffect>();
		}

		IReadOnlyList<CardExtraEffect> upgradeEffects = overrideData.Upgrade.ExtraEffects;
		if (baseEffects == null)
		{
			return upgradeEffects;
		}

		List<CardExtraEffect> fused = baseEffects.Select(CloneEffect).ToList();
		int baseCount = baseEffects.Count;
		bool secondaryNumericFieldsAreDeltas = overrideData.Upgrade.ExtraEffectNumericFieldsAreDeltas;

		for (int i = 0; i < upgradeEffects.Count; i++)
		{
			CardExtraEffect? upgradeEffect = upgradeEffects[i];
			if (upgradeEffect == null)
			{
				continue;
			}

			if (i < baseCount)
			{
				if (upgradeEffect.DisableOnUpgrade)
				{
					fused[i] = null!;
					continue;
				}

				CardExtraEffect baseEffect = baseEffects[i];
				if (baseEffect.Kind == upgradeEffect.Kind)
				{
					if (!HasMeaningfulUpgradeBaseSlotDelta(upgradeEffect, secondaryNumericFieldsAreDeltas))
					{
						continue;
					}
					fused[i] = MergeUpgradeBaseSlotEffect(baseEffect, upgradeEffect, secondaryNumericFieldsAreDeltas);
					continue;
				}
			}

			if (!upgradeEffect.DisableOnUpgrade)
			{
				fused.Add(CloneEffect(upgradeEffect));
			}
		}

		return fused;
	}

	private static CardExtraEffect MergeUpgradeBaseSlotEffect(CardExtraEffect baseEffect, CardExtraEffect upgradeEffect, bool secondaryNumericFieldsAreDeltas)
	{
		CardExtraEffect fused = CloneEffect(baseEffect);

		if (baseEffect.AmountIsX)
		{
			fused.AmountXPlus = baseEffect.AmountXPlus + upgradeEffect.Amount;
		}
		else
		{
			int mergedAmount = baseEffect.Kind == CardExtraEffectKind.CreatedCardsCostLess
				&& (baseEffect.Amount == -1 || upgradeEffect.Amount == -1)
					? -1
					: baseEffect.Amount + upgradeEffect.Amount;
			fused.Amount = IsValidEffectAmount(baseEffect.Kind, mergedAmount) ? mergedAmount : 0;
		}

		if (secondaryNumericFieldsAreDeltas)
		{
			fused.Turns = AddUpgradeDelta(baseEffect.Turns, upgradeEffect.Turns, 0, 99);
			fused.RepeatCount = AddUpgradeDelta(baseEffect.RepeatCount, upgradeEffect.RepeatCount, 1, 99);
			fused.TriggerEveryN = AddUpgradeDelta(baseEffect.TriggerEveryN, upgradeEffect.TriggerEveryN, 1, 999);
			fused.TriggerMaxFires = AddUpgradeDelta(baseEffect.TriggerMaxFires, upgradeEffect.TriggerMaxFires, 0, 999);
			fused.TriggerMaxTurns = AddUpgradeDelta(baseEffect.TriggerMaxTurns, upgradeEffect.TriggerMaxTurns, 0, 999);
			fused.CreatedCardsCostTurns = AddUpgradeDelta(baseEffect.CreatedCardsCostTurns, upgradeEffect.CreatedCardsCostTurns, 1, 99);
			fused.CardCostsLessTurns = AddUpgradeDelta(baseEffect.CardCostsLessTurns, upgradeEffect.CardCostsLessTurns, 1, 99);
			fused.CountTurns = AddUpgradeDelta(baseEffect.CountTurns, upgradeEffect.CountTurns, 1, 99);
			fused.CountConditionAmount = AddUpgradeDelta(baseEffect.CountConditionAmount, upgradeEffect.CountConditionAmount, 0, 999);
			fused.CardGrantTurns = AddUpgradeDelta(baseEffect.CardGrantTurns, upgradeEffect.CardGrantTurns, 1, 99);
			fused.CardSelectionCount = AddUpgradeDelta(baseEffect.CardSelectionCount, upgradeEffect.CardSelectionCount, 0, 99);
			fused.EnchantmentTurns = AddUpgradeDelta(baseEffect.EnchantmentTurns, upgradeEffect.EnchantmentTurns, 1, 99);
		}
		else
		{
			fused.Turns = upgradeEffect.Turns;
			fused.RepeatCount = upgradeEffect.RepeatCount;
			fused.TriggerEveryN = upgradeEffect.TriggerEveryN;
			fused.TriggerMaxFires = upgradeEffect.TriggerMaxFires;
			fused.TriggerMaxTurns = upgradeEffect.TriggerMaxTurns;
			fused.CreatedCardsCostTurns = upgradeEffect.CreatedCardsCostTurns;
			fused.CardCostsLessTurns = upgradeEffect.CardCostsLessTurns;
			fused.CountTurns = upgradeEffect.CountTurns;
			fused.CountConditionAmount = upgradeEffect.CountConditionAmount;
			fused.CardGrantTurns = upgradeEffect.CardGrantTurns;
			fused.CardSelectionCount = upgradeEffect.CardSelectionCount;
			fused.EnchantmentTurns = upgradeEffect.EnchantmentTurns;
		}

		return fused;
	}

	private static int AddUpgradeDelta(int baseValue, int deltaValue, int minValue, int maxValue)
	{
		return Math.Clamp(baseValue + deltaValue, minValue, maxValue);
	}

	internal static bool EffectsMatchExceptAmount(CardExtraEffect a, CardExtraEffect b)
	{
		if (a == null || b == null)
		{
			return false;
		}
		return a.Kind == b.Kind
			&& a.Target == b.Target
			&& a.AmountIsX == b.AmountIsX
			&& a.Trigger == b.Trigger
			&& a.PowerTriggerCountEvent == b.PowerTriggerCountEvent
			&& a.TurnBoundary == b.TurnBoundary
			&& a.TurnBoundarySide == b.TurnBoundarySide
			&& a.TurnBoundaryCardLocation == b.TurnBoundaryCardLocation
			&& a.Timing == b.Timing
			&& a.Turns == b.Turns
			&& a.Duration == b.Duration
			&& a.RepeatIsX == b.RepeatIsX
			&& a.RepeatCount == b.RepeatCount
			&& a.AsPower == b.AsPower
			&& a.TriggerCardPool == b.TriggerCardPool
			&& a.TriggerCardType == b.TriggerCardType
			&& a.TriggerCardFilter == b.TriggerCardFilter
			&& a.CreatedCardsCostDuration == b.CreatedCardsCostDuration
			&& a.CreatedCardsCostTurns == b.CreatedCardsCostTurns
			&& a.CardCostsLessDuration == b.CardCostsLessDuration
			&& a.CardCostsLessTurns == b.CardCostsLessTurns
			&& a.CardCostsLessMode == b.CardCostsLessMode
			&& a.GeneratedCardPool == b.GeneratedCardPool
			&& a.GeneratedCardType == b.GeneratedCardType
			&& a.ScaleMode == b.ScaleMode
			&& a.CountEvent == b.CountEvent
			&& a.CountWindow == b.CountWindow
			&& a.CountWindowInclusion == b.CountWindowInclusion
			&& a.BlockLostCountingMode == b.BlockLostCountingMode
			&& a.CountTurns == b.CountTurns
			&& a.CountCardPile == b.CountCardPile
			&& a.CountCardPool == b.CountCardPool
			&& a.CountCardType == b.CountCardType
			&& a.CountCardFilter == b.CountCardFilter
			&& a.CountOnlyBlockCards == b.CountOnlyBlockCards
			&& a.CountOrbType == b.CountOrbType
			&& a.CountOrbSelection == b.CountOrbSelection
			&& a.CountEnemyStatus == b.CountEnemyStatus
			&& a.CountEnemyIntent == b.CountEnemyIntent
			&& a.MultiplierStat == b.MultiplierStat
			&& a.CountComparison == b.CountComparison
			&& a.CountConditionAmount == b.CountConditionAmount
			&& a.HistoryScalingIncludesBase == b.HistoryScalingIncludesBase
			&& a.GrantToCard == b.GrantToCard
			&& a.CardSelectionMode == b.CardSelectionMode
			&& a.CardSelectionCountIsX == b.CardSelectionCountIsX
			&& a.CardSelectionCount == b.CardSelectionCount
			&& a.CardSelectionPool == b.CardSelectionPool
			&& a.CardSelectionType == b.CardSelectionType
			&& a.CardSelectionFilter == b.CardSelectionFilter
			&& a.CardSelectionPile == b.CardSelectionPile
			&& a.CardGrantDuration == b.CardGrantDuration
			&& a.CardGrantTurns == b.CardGrantTurns
			&& string.Equals(a.EnchantmentId ?? string.Empty, b.EnchantmentId ?? string.Empty, StringComparison.Ordinal)
			&& a.EnchantmentDuration == b.EnchantmentDuration
			&& a.EnchantmentTurns == b.EnchantmentTurns
			&& a.MoveToPile == b.MoveToPile
			&& a.MoveToPosition == b.MoveToPosition
			&& a.OrbAction == b.OrbAction
			&& a.OrbType == b.OrbType
			&& a.OrbSelection == b.OrbSelection
			&& a.OrbFollowUp == b.OrbFollowUp
			&& a.OstyAction == b.OstyAction
			&& string.Equals(a.SpecificCardId ?? string.Empty, b.SpecificCardId ?? string.Empty, StringComparison.Ordinal)
			&& a.GrantedKeyword == b.GrantedKeyword;
	}

	internal static CardExtraEffect CloneEffect(CardExtraEffect source)
	{
		return new CardExtraEffect
		{
			Kind = source.Kind,
			Target = source.Target,
			Amount = source.Amount,
			AmountIsX = source.AmountIsX,
			AmountXPlus = source.AmountXPlus,
			DisableOnUpgrade = source.DisableOnUpgrade,
			RepeatIsX = source.RepeatIsX,
			RepeatCount = source.RepeatCount,
			Trigger = source.Trigger,
			PowerTriggerCountEvent = source.PowerTriggerCountEvent,
			TurnBoundary = source.TurnBoundary,
			TurnBoundarySide = source.TurnBoundarySide,
			TurnBoundaryCardLocation = source.TurnBoundaryCardLocation,
			Timing = source.Timing,
			Turns = source.Turns,
			Duration = source.Duration,
			AsPower = source.AsPower,
			TriggerCardPool = source.TriggerCardPool,
			TriggerCardType = source.TriggerCardType,
			TriggerCardFilter = source.TriggerCardFilter,
			TriggerEveryN = source.TriggerEveryN,
			TriggerMaxFires = source.TriggerMaxFires,
			TriggerMaxTurns = source.TriggerMaxTurns,
			CreatedCardsCostDuration = source.CreatedCardsCostDuration,
			CreatedCardsCostTurns = source.CreatedCardsCostTurns,
			CardCostsLessDuration = source.CardCostsLessDuration,
			CardCostsLessTurns = source.CardCostsLessTurns,
			CardCostsLessMode = source.CardCostsLessMode,
			GeneratedCardPool = source.GeneratedCardPool,
			GeneratedCardType = source.GeneratedCardType,
			ScaleMode = source.ScaleMode,
			CountEvent = source.CountEvent,
			CountWindow = source.CountWindow,
			CountWindowInclusion = source.CountWindowInclusion,
			BlockLostCountingMode = source.BlockLostCountingMode,
			CountTurns = source.CountTurns,
			CountCardPile = source.CountCardPile,
			CountCardPool = source.CountCardPool,
			CountCardType = source.CountCardType,
			CountCardFilter = source.CountCardFilter,
			CountOnlyBlockCards = source.CountOnlyBlockCards,
			CountOrbType = source.CountOrbType,
			CountOrbSelection = source.CountOrbSelection,
			CountEnemyStatus = source.CountEnemyStatus,
			CountEnemyIntent = source.CountEnemyIntent,
			MultiplierStat = source.MultiplierStat,
			CountComparison = source.CountComparison,
			CountConditionAmount = source.CountConditionAmount,
			HistoryScalingIncludesBase = source.HistoryScalingIncludesBase,
			GrantToCard = source.GrantToCard,
			CardSelectionMode = source.CardSelectionMode,
			CardSelectionCountIsX = source.CardSelectionCountIsX,
			CardSelectionCount = source.CardSelectionCount,
			CardSelectionPool = source.CardSelectionPool,
			CardSelectionType = source.CardSelectionType,
			CardSelectionFilter = source.CardSelectionFilter,
			CardSelectionPile = source.CardSelectionPile,
			CardGrantDuration = source.CardGrantDuration,
			CardGrantTurns = source.CardGrantTurns,
			EnchantmentId = source.EnchantmentId,
			EnchantmentDuration = source.EnchantmentDuration,
			EnchantmentTurns = source.EnchantmentTurns,
			MoveToPile = source.MoveToPile,
			MoveToPosition = source.MoveToPosition,
			OrbAction = source.OrbAction,
			OrbType = source.OrbType,
			OrbSelection = source.OrbSelection,
			OrbFollowUp = source.OrbFollowUp,
			OrbScope = source.OrbScope,
			OstyAction = source.OstyAction,
			DrawnFromPile = source.DrawnFromPile,
			SpecificCardId = source.SpecificCardId,
			GrantedKeyword = source.GrantedKeyword,
			CostFilterEnabled = source.CostFilterEnabled,
			CostFilterMax = source.CostFilterMax
		};
	}

	private static int ResolveXAmountWithPlus(CardPlay cardPlay, int plus)
	{
		long value = (long)ResolveXAmount(cardPlay) + plus;
		if (value >= int.MaxValue)
		{
			return int.MaxValue;
		}
		if (value <= int.MinValue)
		{
			return int.MinValue;
		}
		return (int)value;
	}

	private static string FormatXPlusText(int plus)
	{
		if (plus == 0)
		{
			return "X";
		}
		return plus > 0
			? "X+" + plus.ToString(CultureInfo.InvariantCulture)
			: "X" + plus.ToString(CultureInfo.InvariantCulture);
	}
}
