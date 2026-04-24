using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
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
using MegaCrit.Sts2.Core.Extensions;
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
	FetchSpecificCardToHand = 78,
	RunEffectSourceCard = 79,
	GainMaxHp = 80,
	LoseMaxHp = 81,
	ApplyPower = 82,
	TransformCards = 83,
	GrantReplay = 84,
	AddExactCopyOfThisCardToDeck = 85,
	CopyCardsFromPileToDeck = 86,
	RemoveCardsFromDeck = 87,
	CopyExactCardsFromPileToDeck = 88,
	RemoveWeak = 89,
	RemoveFrail = 90,
	RemoveVulnerable = 91,
	RemovePoison = 92,
	RemoveDoom = 93,
	RemoveConstrict = 94,
	RemoveThorns = 95,
	RemoveRegen = 96,
	RemovePlating = 97,
	RemoveIntangible = 98,
	RemoveBuffer = 99,
	RemoveVigor = 100,
	RemoveBlur = 101,
	RemoveRitual = 102,
	CleanseDebuffs = 103,
	CleanseBuffs = 104,
	ChooseOneEffectSource = 105,
	SelfScaling = 106,
	LoseEnergy = 107,
	LoseStars = 108,
	LoseGold = 109,
	PlayRandomGeneratedCard = 110,
	HitsAllEnemies = 111,
	CardDealsExtraDamage = 112,
	GainStatusEqualToStatus = 113,
	DoesNotConsumeVigor = 114,
	ScalingStage = 115,
	PersistentSelfScaling = 116
}

public enum CardExtraEffectSelfScalingOperation
{
	Increase = 0,
	Decrease = 1
}

public enum CardExtraEffectSelfScalingTargetType
{
	BaseDamage = 0,
	BaseBlock = 1,
	EffectRowAmount = 2
}

public enum CardExtraEffectSelfScalingField
{
	Amount = 0,
	Repeat = 1,
	SecondaryAmount = 2,
	Threshold = 3,
	Duration = 4
}

public enum CardExtraEffectAmountSourceMode
{
	Fixed = 0,
	AppliedEffectRow = 1,
	ValueSource = 2
}

public enum CardExtraEffectValueSourceMode
{
	Common = 0,
	PowerStatus = 1
}

public enum CardExtraEffectValueSourceActor
{
	Self = 0,
	Target = 1,
	AllEnemies = 2,
	AllAllies = 3
}

public enum CardExtraEffectValueSourceAggregation
{
	Value = 0,
	Sum = 1,
	Highest = 2,
	Lowest = 3,
	Average = 4
}

public enum CardExtraEffectValueSourceKind
{
	CurrentHp = 0,
	MaxHp = 1,
	MissingHp = 2,
	Block = 3,
	Strength = 4,
	Dexterity = 5,
	Focus = 6,
	Weak = 7,
	Frail = 8,
	Vulnerable = 9,
	Poison = 10,
	Doom = 11,
	Constrict = 12,
	Artifact = 13,
	Thorns = 14,
	Regen = 15,
	Plating = 16,
	Intangible = 17,
	Buffer = 18,
	Vigor = 19,
	Blur = 20,
	Ritual = 21
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

[Flags]
public enum CardExtraEffectAdditionalMoveToPiles
{
	None = 0,
	Hand = 1,
	DrawPile = 2,
	DiscardPile = 4,
	ExhaustPile = 8
}

public enum CardExtraEffectCardSelectionMode
{
	Choose = 0,
	Random = 1,
	All = 2,
	UpTo = 3,
	Top = 4,
	Bottom = 5
}

public enum CardExtraEffectChooseOneOptionMode
{
	ExactCard = 0,
	MatchingCards = 1
}

public enum CardExtraEffectChooseOneQuerySource
{
	Pile = 0,
	Compendium = 1
}

public enum CardExtraEffectTransformMode
{
	Random = 0,
	SpecificCard = 1
}

public enum CardExtraEffectConditionalBonusCondition
{
	None = 0,
	TargetHasBlock = 1,
	TargetHasStatus = 2,
	TargetHasIntent = 3,
	SelfHasBlock = 4,
	SelfHasStatus = 5,
	TargetHasNoBlock = 6,
	SelfHasNoBlock = 7,
	TargetLacksStatus = 8,
	SelfLacksStatus = 9,
	TargetIntentIsNot = 10,
	TargetIsDamaged = 11,
	SelfIsDamaged = 12,
	TargetIsBloodied = 13,
	SelfIsBloodied = 14,
	TargetIsFullHp = 15,
	SelfIsFullHp = 16,
	TargetIsNotBloodied = 17,
	SelfIsNotBloodied = 18,
	TargetHasLessHpThanYou = 19,
	TargetHasMoreHpThanYou = 20,
	TargetHasLessBlockThanYou = 21,
	TargetHasMoreBlockThanYou = 22
}

public enum CardExtraEffectBranchMode
{
	None = 0,
	InsteadIf = 1,
	AlsoIf = 2
}

public enum CardExtraEffectBranchConditionType
{
	None = 0,
	TargetCheck = 1,
	HistoryCount = 2
}

public enum CardExtraEffectCardMatchMode
{
	Any = 0,
	CardId = 1,
	Tag = 2,
	CustomKeyword = 3,
	NameContains = 4
}

public enum CardExtraEffectCardMatchTagKind
{
	Vanilla = 0,
	Custom = 1
}

public enum CardExtraEffectCardReferenceDisplayMode
{
	NameOnly = 0,
	FullText = 1
}

public enum CardExtraEffectResourceConsumptionMode
{
	Vigor = 0,
	SelfHpAndSelfDamage = 1,
	SpecificStatStatus = 2,
	SpecificPowerStatus = 3
}

public enum CardExtraEffectStatusToStatusMode
{
	Gain = 0,
	Lose = 1
}

public sealed class CardExtraEffectChooseOneOption
{
	public CardExtraEffectChooseOneOptionMode Mode { get; set; } = CardExtraEffectChooseOneOptionMode.ExactCard;
	public bool ShowFullText { get; set; }
	public string? CardId { get; set; }
	public CardExtraEffectChooseOneQuerySource QuerySource { get; set; } = CardExtraEffectChooseOneQuerySource.Pile;
	public CardExtraEffectCardPile QueryPile { get; set; } = CardExtraEffectCardPile.Hand;
	public CardGeneratedCardPool QueryPool { get; set; } = CardGeneratedCardPool.All;
	public CardGeneratedCardType QueryType { get; set; } = CardGeneratedCardType.Any;
	public CardExtraEffectCardSelectionMode QuerySelectionMode { get; set; } = CardExtraEffectCardSelectionMode.Choose;
	public int QueryCount { get; set; } = 1;
	public CardExtraEffectCardMatchMode QueryMatchMode { get; set; } = CardExtraEffectCardMatchMode.Any;
	public string? QueryMatchCardId { get; set; }
	public CardExtraEffectCardMatchTagKind QueryMatchTagKind { get; set; } = CardExtraEffectCardMatchTagKind.Vanilla;
	public CardTag QueryMatchVanillaTag { get; set; } = CardTag.None;
	public string? QueryMatchCustomTag { get; set; }
	public string? QueryMatchCustomKeyword { get; set; }
}

public enum CardExtraEffectCostFilterMode
{
	AtMost = 0,
	AtLeast = 1,
	Exactly = 2
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

public enum CardExtraEffectCostModifier
{
	Reduce = 0,
	Free = 1,
	HalfCost = 2,
	FreeToPlay = 3
}

public enum CardExtraEffectStatusIconMode
{
	Auto = 0,
	BaseGame = 1,
	Custom = 2
}

public enum CardExtraEffectPowerHost
{
	CardOwner = 0,
	TriggerTarget = 1,
	CardOwnerWatchOpponents = 2
}

public enum CardExtraEffectPowerTriggerFrom
{
	Self = 0,
	AnyEnemy = 1,
	AnyAlly = 2,
	Anyone = 3
}

public enum CardExtraEffectPowerTargeting
{
	TriggerTarget = 0,
	RememberFirstEnemy = 1,
	RememberLastEnemy = 2,
	RememberEnemyRandomFallback = 3,
	RandomEnemy = 4,
	AllEnemies = 5
}

public enum CardExtraEffectTarget
{
	Self = 0,
	Target = 1,
	RandomEnemy = 2,
	AllEnemies = 3,
	AnyPlayer = 4,
	AnyAlly = 5,
	AllAllies = 6
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
	OnCountEvent = 15,
	DeckPassiveCombatStart = 16,
	DeckPassiveCombatEnd = 17,
	OnMovedToTopOfPile = 18,
	OnMovedToBottomOfPile = 19
}

public enum CardExtraEffectTurnBoundary
{
	Start = 0,
	End = 1,
	StartAfterDraw = 2,
	EndAfterDiscard = 3
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
	Turns = 3,
	Permanent = 4
}

public enum CardCreatedCardsCostResource
{
	Energy = 0,
	Stars = 1
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
	OstyAttacked = 29,
	OstyAlive = 30,
	TimesDealtDamage = 31,
	ThisCardPlayed = 32,
	ThisCardDrawn = 33,
	ThisCardDiscarded = 34,
	ThisCardExhausted = 35,
	ThisCardDamageDealt = 36
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
	Eternal = 33,
	CreatesCards = 34
}

public enum CardExtraEffectCountAggregationMode
{
	CardCount = 0,
	MatchingEffectAmount = 1,
	CurrentEnergyCost = 2,
	BaseEnergyCost = 3,
	CurrentStarCost = 4,
	BaseStarCost = 5
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
	TriggerPassive = 5,
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
	public readonly record struct CardCostAdjustment(int Delta, bool ForceFree, bool HalfCost)
	{
		public bool IsNeutral => Delta == 0 && !ForceFree && !HalfCost;

		public static CardCostAdjustment Combine(CardCostAdjustment left, CardCostAdjustment right)
			=> new(left.Delta + right.Delta, left.ForceFree || right.ForceFree, left.HalfCost || right.HalfCost);
	}

	public CardExtraEffectKind Kind { get; set; }
	public CardExtraEffectTarget Target { get; set; }
	public int Amount { get; set; }

	// When true, treat Amount as "X" (the amount of Energy spent when the effect triggers).
	// The numeric Amount value is preserved so toggling X off in the UI restores the previous number.
	public bool AmountIsX { get; set; }

	// When AmountIsX is true, add this value to X when resolving the effect (i.e. "X+N").
	// This enables vanilla-style "X+1" amounts while still preserving the non-X Amount (used when toggling X off).
	public int AmountXPlus { get; set; }

	// Optional: resolve this effect's amount from a previous effect row's actual applied total.
	public CardExtraEffectAmountSourceMode AmountSourceMode { get; set; } = CardExtraEffectAmountSourceMode.Fixed;
	public string? AmountSourceEffectId { get; set; }
	public CardExtraEffectValueSourceMode ValueSourceMode { get; set; } = CardExtraEffectValueSourceMode.Common;
	public CardExtraEffectValueSourceActor ValueSourceActor { get; set; } = CardExtraEffectValueSourceActor.Self;
	public CardExtraEffectValueSourceAggregation ValueSourceAggregation { get; set; } = CardExtraEffectValueSourceAggregation.Value;
	public CardExtraEffectValueSourceKind ValueSourceKind { get; set; } = CardExtraEffectValueSourceKind.MaxHp;
	public string? ValueSourcePowerId { get; set; }

	// Optional: apply a flat bonus to this effect's resolved amount when the condition passes.
	public int ConditionalBonusAmount { get; set; }

	// Optional: conditional that gates ConditionalBonusAmount.
	public CardExtraEffectBranchConditionType ConditionalBonusConditionType { get; set; } = CardExtraEffectBranchConditionType.None;
	public CardExtraEffectConditionalBonusCondition ConditionalBonusCondition { get; set; } = CardExtraEffectConditionalBonusCondition.None;
	public CardExtraEffectEnemyStatus ConditionalBonusEnemyStatus { get; set; }
	public string? ConditionalBonusPowerId { get; set; }
	public CardExtraEffectEnemyIntent ConditionalBonusEnemyIntent { get; set; }

	// Optional: when the branch condition passes, run BranchEffect instead of or in addition to this effect.
	public CardExtraEffectBranchMode BranchMode { get; set; } = CardExtraEffectBranchMode.None;
	public CardExtraEffectBranchConditionType BranchConditionType { get; set; } = CardExtraEffectBranchConditionType.None;
	public CardExtraEffectConditionalBonusCondition BranchCondition { get; set; } = CardExtraEffectConditionalBonusCondition.None;
	public CardExtraEffectEnemyStatus BranchEnemyStatus { get; set; }
	public string? BranchPowerId { get; set; }
	public CardExtraEffectEnemyIntent BranchEnemyIntent { get; set; }
	public CardExtraEffect? BranchEffect { get; set; }
	public CardExtraEffectCountEvent BranchCountEvent { get; set; } = CardExtraEffectCountEvent.Played;
	public CardExtraEffectCountWindow BranchCountWindow { get; set; } = CardExtraEffectCountWindow.ThisCombat;
	public CardExtraEffectCountWindowInclusion BranchCountWindowInclusion { get; set; } = CardExtraEffectCountWindowInclusion.IncludeThisTurn;
	public CardExtraEffectBlockLostCountingMode BranchBlockLostCountingMode { get; set; } = CardExtraEffectBlockLostCountingMode.DamageAndEffects;
	public int BranchCountTurns { get; set; } = 1;
	public CardExtraEffectCardPile BranchCountCardPile { get; set; } = CardExtraEffectCardPile.Hand;
	public CardGeneratedCardPool BranchCountCardPool { get; set; } = CardGeneratedCardPool.All;
	public CardGeneratedCardType BranchCountCardType { get; set; } = CardGeneratedCardType.Any;
	public CardExtraEffectCountCardFilter BranchCountCardFilter { get; set; } = CardExtraEffectCountCardFilter.Any;
	public CardExtraEffectCountAggregationMode BranchCountAggregationMode { get; set; } = CardExtraEffectCountAggregationMode.CardCount;
	// Legacy back-compat: older saves used a boolean toggle for "sum effect amount".
	public bool BranchCountUsesCardEffectAmount { get; set; }
	public bool BranchCountExcludeSourceCard { get; set; }
	public CardExtraEffectOrbType BranchCountOrbType { get; set; } = CardExtraEffectOrbType.Any;
	public CardExtraEffectOrbSelection BranchCountOrbSelection { get; set; } = CardExtraEffectOrbSelection.Leftmost;
	public CardExtraEffectEnemyStatus BranchCountEnemyStatus { get; set; } = CardExtraEffectEnemyStatus.Weak;
	public string? BranchCountPowerId { get; set; }
	public CardExtraEffectEnemyIntent BranchCountEnemyIntent { get; set; } = CardExtraEffectEnemyIntent.Attack;
	public CardExtraEffectCountComparison BranchCountComparison { get; set; } = CardExtraEffectCountComparison.None;
	public int BranchCountConditionAmount { get; set; } = 1;

	// Transform-specific: choose whether to transform into a random card or a specific card id.
	public CardExtraEffectTransformMode TransformMode { get; set; } = CardExtraEffectTransformMode.Random;

	// Choose-one-specific: optional additional effect source card ids for the second and third options.
	public string? SpecificCardId2 { get; set; }
	public string? SpecificCardId3 { get; set; }
	public CardExtraEffectChooseOneOption? ChooseOneOption1 { get; set; }
	public CardExtraEffectChooseOneOption? ChooseOneOption2 { get; set; }
	public CardExtraEffectChooseOneOption? ChooseOneOption3 { get; set; }

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
	public CardExtraEffectEnemyStatus PowerTriggerEnemyStatus { get; set; } = CardExtraEffectEnemyStatus.AnyPowerStatus;
	public string? PowerTriggerPowerId { get; set; }
	public bool PowerTriggerUsesEventAmount { get; set; }

	// Power-only: fire only every N-th time the trigger condition is met. 0 or 1 = every time.
	public int TriggerEveryN { get; set; }

	// Power-only: remove this power entry after it fires this many times. 0 = unlimited.
	public int TriggerMaxFires { get; set; }

	// Power-only: remove this power entry after this many turns elapse. 0 = unlimited.
	// When both TriggerMaxFires and TriggerMaxTurns are set, the entry expires when either limit is reached first.
	public int TriggerMaxTurns { get; set; }

	public CardCreatedCardsCostDuration CreatedCardsCostDuration { get; set; }
	public int CreatedCardsCostTurns { get; set; }
	public CardCreatedCardsCostResource CreatedCardsCostResource { get; set; }

	public CardExtraEffectCardCostsLessDuration CardCostsLessDuration { get; set; }
	public int CardCostsLessTurns { get; set; }
	public CardExtraEffectCardCostsLessMode CardCostsLessMode { get; set; }
	public CardExtraEffectCostModifier CardCostsLessModifier { get; set; }

	public CardGeneratedCardPool GeneratedCardPool { get; set; }
	public CardGeneratedCardType GeneratedCardType { get; set; }
	public string? GeneratedCardCustomTag { get; set; }

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
	public CardExtraEffectCountAggregationMode CountAggregationMode { get; set; } = CardExtraEffectCountAggregationMode.CardCount;
	// Legacy back-compat: older saves used a boolean toggle for "sum effect amount".
	public bool CountUsesCardEffectAmount { get; set; }
	public bool CountExcludeSourceCard { get; set; }
	public CardExtraEffectOrbType CountOrbType { get; set; } = CardExtraEffectOrbType.Any;
	public CardExtraEffectOrbSelection CountOrbSelection { get; set; } = CardExtraEffectOrbSelection.Leftmost;
	public CardExtraEffectEnemyStatus CountEnemyStatus { get; set; } = CardExtraEffectEnemyStatus.AnyPowerStatus;
	public string? CountPowerId { get; set; }
	public CardExtraEffectEnemyIntent CountEnemyIntent { get; set; } = CardExtraEffectEnemyIntent.Attack;
	public CardExtraEffectMultiplierStat MultiplierStat { get; set; } = CardExtraEffectMultiplierStat.Strength;
	public CardExtraEffectValueSourceMode MultiplierSourceMode { get; set; } = CardExtraEffectValueSourceMode.Common;
	public string? MultiplierPowerId { get; set; }
	public CardExtraEffectCountComparison CountComparison { get; set; }
	public int CountConditionAmount { get; set; } = 1;

	// When history scaling is enabled, include the base amount even if the history count is zero.
	// (i.e., total = base + base*count). This is optional and defaults to false for back-compat.
	public bool HistoryScalingIncludesBase { get; set; }

	public bool GrantToCard { get; set; }
	public CardExtraEffectCardSelectionMode CardSelectionMode { get; set; }
	public CardExtraEffectCardPile CardSelectionPile { get; set; }
	public bool IncludeSourceCardInSelection { get; set; }
	public bool FutureMatchingCards { get; set; }
	public CardExtraEffectCardGrantDuration CardGrantDuration { get; set; }
	public int CardGrantTurns { get; set; }
	public string? EnchantmentId { get; set; }
	public CardExtraEffectEnchantmentDuration EnchantmentDuration { get; set; } = CardExtraEffectEnchantmentDuration.ThisCombat;
	public int EnchantmentTurns { get; set; } = 1;

	public CardExtraEffectCardPile MoveToPile { get; set; }
	public CardExtraEffectCardPilePosition MoveToPosition { get; set; }
	public bool UseMoveDestinationForGeneratedCards { get; set; }
	public CardExtraEffectAdditionalMoveToPiles AdditionalMoveToPiles { get; set; }
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
	public CardExtraEffectCardReferenceDisplayMode CardReferenceDisplayMode { get; set; } = CardExtraEffectCardReferenceDisplayMode.NameOnly;
	public CardExtraEffectResourceConsumptionMode ResourceConsumptionMode { get; set; } = CardExtraEffectResourceConsumptionMode.Vigor;
	public CardExtraEffectMultiplierStat ResourceConsumptionStat { get; set; } = CardExtraEffectMultiplierStat.Vigor;
	public CardExtraEffectStatusToStatusMode StatusToStatusMode { get; set; } = CardExtraEffectStatusToStatusMode.Gain;

	// ApplyPower: which power to apply (stored as ModelId string, e.g. "powers.demise").
	public string? PowerId { get; set; }

	// Persistent power UI: how this effect should surface on the vanilla power bar.
	public CardExtraEffectStatusIconMode StatusIconMode { get; set; } = CardExtraEffectStatusIconMode.Auto;
	public string? StatusIconPowerId { get; set; }
	public string? StatusCustomPackedIconPath { get; set; }
	public string? StatusCustomBigIconPath { get; set; }
	public string? CustomPowerName { get; set; }
	public string? CustomPowerDescription { get; set; }
	public CardExtraEffectPowerHost PowerHost { get; set; } = CardExtraEffectPowerHost.CardOwner;
	public CardExtraEffectPowerTriggerFrom PowerTriggerFrom { get; set; } = CardExtraEffectPowerTriggerFrom.Self;
	public CardExtraEffectPowerTargeting PowerTargeting { get; set; } = CardExtraEffectPowerTargeting.TriggerTarget;

	// GrantKeywordToPile: which keyword to grant to the selected cards.
	public CardKeyword GrantedKeyword { get; set; } = CardKeyword.Exhaust;

	// Optional card match filter for pile operations (MoveCardsBetweenPiles, DiscardCards, ExhaustCards, PlayCardFromPile, UpgradeCardsInPile, GrantKeywordToPile, UpgradeDeckCards).
	public CardExtraEffectCardMatchMode CardMatchMode { get; set; } = CardExtraEffectCardMatchMode.Any;
	public string? MatchCardId { get; set; }
	public CardExtraEffectCardMatchTagKind MatchTagKind { get; set; } = CardExtraEffectCardMatchTagKind.Vanilla;
	public CardTag MatchVanillaTag { get; set; } = CardTag.None;
	public string? MatchCustomTag { get; set; }
	public string? MatchCustomKeyword { get; set; }
	public bool NameFilterEnabled { get; set; }
	public string? NameFilterText { get; set; }
	public string? CustomKeywordName { get; set; }

	// Cost filter for pile operations and random card generation effects.
	// CostFilterMax stores the threshold value for the selected comparison mode.
	// X-cost cards are always excluded while the filter is enabled.
	// Default: disabled (no filter), preserving existing behavior.
	public bool CostFilterEnabled { get; set; }
	public CardExtraEffectCostFilterMode CostFilterMode { get; set; } = CardExtraEffectCostFilterMode.AtMost;
	public int CostFilterMax { get; set; }

	// Stable identity for editor/runtime mutation targeting.
	public string? EffectId { get; set; }

	// Self-scaling: mutate this card's own base vars or a chosen effect row after it is played.
	public CardExtraEffectSelfScalingOperation SelfScalingOperation { get; set; } = CardExtraEffectSelfScalingOperation.Increase;
	public CardExtraEffectSelfScalingTargetType SelfScalingTargetType { get; set; } = CardExtraEffectSelfScalingTargetType.BaseDamage;
	public CardExtraEffectSelfScalingField SelfScalingField { get; set; } = CardExtraEffectSelfScalingField.Amount;
	public string? SelfScalingTargetEffectId { get; set; }
}

internal sealed class CardExtraEffectDefinition
{
	public required CardExtraEffectKind Kind { get; init; }
	public required string Label { get; init; }
	public required IReadOnlyList<CardExtraEffectTarget> AllowedTargets { get; init; }
	public required int DefaultAmount { get; init; }
	public required CardExtraEffectTarget DefaultTarget { get; init; }
}

internal sealed class CardExtraEffectKeywordSummary
{
	public required string Name { get; init; }
	public required string Description { get; init; }
}

internal static class CardEditorExtraEffects
{
	private static readonly Dictionary<Type, bool> _cardCreatesCardsIlCache = new();
	private static readonly object _cardCreatesCardsIlCacheLock = new();
	private static readonly OpCode[] _oneByteOpCodes = BuildOpCodeMap(twoByte: false);
	private static readonly OpCode[] _twoByteOpCodes = BuildOpCodeMap(twoByte: true);

	private sealed class DescriptionEffectLine
	{
		public required string Line { get; init; }
		public string? CustomKeywordName { get; init; }
		public CardExtraEffectTrigger Trigger { get; init; }
	}

	private sealed class UpgradeEffectAlignment
	{
		public required CardExtraEffect?[] BaseSlotEffects { get; init; }
		public required int[] UpgradeIndexToBaseSlot { get; init; }
		public required int LastMatchedUpgradeIndex { get; init; }
	}

	internal static readonly IReadOnlyList<CardExtraEffectCountEvent> PowerTriggerCountEvents = new[]
	{
		CardExtraEffectCountEvent.Played,
		CardExtraEffectCountEvent.Drawn,
		CardExtraEffectCountEvent.Discarded,
		CardExtraEffectCountEvent.Exhausted,
		CardExtraEffectCountEvent.Generated,
		CardExtraEffectCountEvent.OrbChanneled,
		CardExtraEffectCountEvent.OrbEvoked,
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

	internal enum ResourceCountSource
	{
		Other = 0,
		BetweenTurnsBlockClear = 1
	}

	internal static CardExtraEffectPowerHost GetEffectivePowerHost(CardExtraEffect? effect)
	{
		return effect?.PowerHost switch
		{
			CardExtraEffectPowerHost.TriggerTarget => CardExtraEffectPowerHost.TriggerTarget,
			_ => CardExtraEffectPowerHost.CardOwner
		};
	}

	internal static CardExtraEffectPowerTriggerFrom GetEffectivePowerTriggerFrom(CardExtraEffect? effect)
	{
		if (effect == null)
		{
			return CardExtraEffectPowerTriggerFrom.Self;
		}

		if (effect.PowerHost == CardExtraEffectPowerHost.CardOwnerWatchOpponents
			&& effect.PowerTriggerFrom == CardExtraEffectPowerTriggerFrom.Self)
		{
			return CardExtraEffectPowerTriggerFrom.AnyEnemy;
		}

		return effect.PowerTriggerFrom;
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
			Kind = CardExtraEffectKind.CardDealsExtraDamage,
			Label = "Card Damage Bonus",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 3,
			DefaultTarget = CardExtraEffectTarget.Self
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
			Kind = CardExtraEffectKind.RemoveBlock,
			Label = "Remove Block",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 5,
			DefaultTarget = CardExtraEffectTarget.Target
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
			Kind = CardExtraEffectKind.LoseEnergy,
			Label = "Lose Energy",
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
			Kind = CardExtraEffectKind.LoseStars,
			Label = "Lose Stars",
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
			Kind = CardExtraEffectKind.GainMaxHp,
			Label = "Raise Max HP",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 5,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.LoseMaxHp,
			Label = "Lower Max HP",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
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
			Kind = CardExtraEffectKind.ApplyPower,
			Label = "Apply Power / Status",
			AllowedTargets = new [] { CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy, CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Target
		},
		new()
		{
			Kind = CardExtraEffectKind.GainStatusEqualToStatus,
			Label = "Status to Status",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
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
			Kind = CardExtraEffectKind.RemoveWeak,
			Label = "Remove Weak",
			AllowedTargets = new [] { CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy, CardExtraEffectTarget.Self },
			DefaultAmount = 2,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.RemoveFrail,
			Label = "Remove Frail",
			AllowedTargets = new [] { CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy, CardExtraEffectTarget.Self },
			DefaultAmount = 2,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.RemoveVulnerable,
			Label = "Remove Vulnerable",
			AllowedTargets = new [] { CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy, CardExtraEffectTarget.Self },
			DefaultAmount = 2,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.RemovePoison,
			Label = "Remove Poison",
			AllowedTargets = new [] { CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy, CardExtraEffectTarget.Self },
			DefaultAmount = 3,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.RemoveDoom,
			Label = "Remove Doom",
			AllowedTargets = new [] { CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy, CardExtraEffectTarget.Self },
			DefaultAmount = 6,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.RemoveConstrict,
			Label = "Remove Constrict",
			AllowedTargets = new [] { CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy, CardExtraEffectTarget.Self },
			DefaultAmount = 3,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.RemoveArtifact,
			Label = "Remove Artifact",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Target
		},
		new()
		{
			Kind = CardExtraEffectKind.RemoveThorns,
			Label = "Remove Thorns",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 2,
			DefaultTarget = CardExtraEffectTarget.Target
		},
		new()
		{
			Kind = CardExtraEffectKind.RemoveRegen,
			Label = "Remove Regen",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 3,
			DefaultTarget = CardExtraEffectTarget.Target
		},
		new()
		{
			Kind = CardExtraEffectKind.RemovePlating,
			Label = "Remove Plating",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 4,
			DefaultTarget = CardExtraEffectTarget.Target
		},
		new()
		{
			Kind = CardExtraEffectKind.RemoveIntangible,
			Label = "Remove Intangible",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Target
		},
		new()
		{
			Kind = CardExtraEffectKind.RemoveBuffer,
			Label = "Remove Buffer",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Target
		},
		new()
		{
			Kind = CardExtraEffectKind.RemoveVigor,
			Label = "Remove Vigor",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 2,
			DefaultTarget = CardExtraEffectTarget.Target
		},
		new()
		{
			Kind = CardExtraEffectKind.RemoveBlur,
			Label = "Remove Blur",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Target
		},
		new()
		{
			Kind = CardExtraEffectKind.RemoveRitual,
			Label = "Remove Ritual",
			AllowedTargets = new [] { CardExtraEffectTarget.Self, CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Target
		},
		new()
		{
			Kind = CardExtraEffectKind.CleanseDebuffs,
			Label = "Cleanse Debuffs",
			AllowedTargets = new [] { CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy, CardExtraEffectTarget.Self },
			DefaultAmount = 0,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.CleanseBuffs,
			Label = "Cleanse Buffs",
			AllowedTargets = new [] { CardExtraEffectTarget.Target, CardExtraEffectTarget.AllEnemies, CardExtraEffectTarget.RandomEnemy, CardExtraEffectTarget.Self },
			DefaultAmount = 0,
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
			Label = "Choose 1 of 3 cards",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.PlayRandomGeneratedCard,
			Label = "Play Random Card",
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
			Label = "Damage Rule Modifier",
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
			Kind = CardExtraEffectKind.DoesNotConsumeVigor,
			Label = "Resource Protection",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 0,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.HitsAllEnemies,
			Label = "Hits All Enemies",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.CardCostsLess,
			Label = "Reduce Cost",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.SelfScaling,
			Label = "Self Scaling",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.PersistentSelfScaling,
			Label = "Permanent Self Scaling",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.ScalingStage,
			Label = "Scaling Stages",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 0,
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
			Label = "Card Action",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.TransformCards,
			Label = "Transform Cards",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.GrantReplay,
			Label = "Grant Replay",
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
			Kind = CardExtraEffectKind.UpgradeDeckCards,
			Label = "Upgrade Deck Cards",
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
			Kind = CardExtraEffectKind.AddExactCopyOfThisCardToDeck,
			Label = "Add Exact Copy of This Card to Deck",
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
			Kind = CardExtraEffectKind.RunEffectSourceCard,
			Label = "Run Effect Source",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 0,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.ChooseOneEffectSource,
			Label = "Choose One",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 0,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.CopyCardsFromPileToDeck,
			Label = "Copy Cards from Pile",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.CopyExactCardsFromPileToDeck,
			Label = "Copy Exact Cards from Pile",
			AllowedTargets = new [] { CardExtraEffectTarget.Self },
			DefaultAmount = 1,
			DefaultTarget = CardExtraEffectTarget.Self
		},
		new()
		{
			Kind = CardExtraEffectKind.RemoveCardsFromDeck,
			Label = "Remove Cards from Deck",
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
			Label = "Auto Action",
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
		},
		new()
		{
			Kind = CardExtraEffectKind.LoseGold,
			Label = "Lose Gold",
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
		CardExtraEffectCountEvent.Summoned,
		CardExtraEffectCountEvent.OstyAlive
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

		// Only force manual enemy targeting for cards that otherwise don't pick a creature target.
		// If a card already targets something specific (Self/Osty/All/etc), we treat "Target" as that implied target.
		TargetType targetType = card.TargetType;
		if (targetType != TargetType.None && targetType != TargetType.TargetedNoCreature)
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
			IReadOnlyList<CardExtraEffect> grantedEffects = GetActiveGrantedExtraEffects(combatState, card);
			if (grantedEffects.Any(e => e != null
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
			CardExtraEffectTarget.AnyPlayer => "Any Player",
			CardExtraEffectTarget.AnyAlly => "Any Ally",
			CardExtraEffectTarget.AllAllies => "All Allies",
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
			CardExtraEffectTrigger.DeckPassiveCombatStart => "Deck Passive: Combat Start",
			CardExtraEffectTrigger.DeckPassiveCombatEnd => "Deck Passive: Combat End",
			CardExtraEffectTrigger.Fatal => "Fatal",
			CardExtraEffectTrigger.OstyDealDamage => "Osty Deals Damage",
			CardExtraEffectTrigger.AfterCombat => "End of Combat",
			CardExtraEffectTrigger.OnChannel => "On Channel",
			CardExtraEffectTrigger.OnEvoke => "On Evoke",
			CardExtraEffectTrigger.OnMovedToTopOfPile => "Moved To Top Of Pile",
			CardExtraEffectTrigger.OnMovedToBottomOfPile => "Moved To Bottom Of Pile",
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
			CardExtraEffectCountEvent.DamageTaken => "HP Lost",
			CardExtraEffectCountEvent.HealingReceived => "HP Recovered",
			CardExtraEffectCountEvent.Summoned => "Summoned",
			CardExtraEffectCountEvent.TimesLostHp => "Times HP Lost",
			CardExtraEffectCountEvent.TimesGainedHp => "Times Gained HP",
			CardExtraEffectCountEvent.TimesDealtDamage => "Times Dealt Damage",
			CardExtraEffectCountEvent.OstyAttacked => "Times Osty Attacked",
			CardExtraEffectCountEvent.OstyAlive => "Osty Is Alive",
			CardExtraEffectCountEvent.ThisCardPlayed => "This Card: Played",
			CardExtraEffectCountEvent.ThisCardDrawn => "This Card: Drawn",
			CardExtraEffectCountEvent.ThisCardDiscarded => "This Card: Discarded",
			CardExtraEffectCountEvent.ThisCardExhausted => "This Card: Exhausted",
			CardExtraEffectCountEvent.ThisCardDamageDealt => "This Card: Damage Dealt",
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

public static string CountAggregationModeLabel(CardExtraEffectCountAggregationMode mode)
{
	string fallback = mode switch
	{
		CardExtraEffectCountAggregationMode.CardCount => "Cards",
		CardExtraEffectCountAggregationMode.MatchingEffectAmount => "Effect Amount",
		CardExtraEffectCountAggregationMode.CurrentEnergyCost => "Current Energy Cost",
		CardExtraEffectCountAggregationMode.BaseEnergyCost => "Base Energy Cost",
		CardExtraEffectCountAggregationMode.CurrentStarCost => "Current Star Cost",
		CardExtraEffectCountAggregationMode.BaseStarCost => "Base Star Cost",
		_ => mode.ToString()
	};
	return CardEditorLoc.Enum("countAggregationMode", mode, fallback);
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

	public static bool PowerCountEventUsesCardFilters(CardExtraEffectCountEvent ev)
	{
		return CountEventUsesCardFilters(ev)
			|| ev is CardExtraEffectCountEvent.EnergyUsed
				or CardExtraEffectCountEvent.BlockGained
				or CardExtraEffectCountEvent.StatusGained
				or CardExtraEffectCountEvent.StatusLost;
	}

	public static bool CountCardFilterSupportsAmount(CardExtraEffectCountCardFilter filter)
	{
		return filter is CardExtraEffectCountCardFilter.DealDamage
			or CardExtraEffectCountCardFilter.GainBlock
			or CardExtraEffectCountCardFilter.DrawCards
			or CardExtraEffectCountCardFilter.GainEnergy
			or CardExtraEffectCountCardFilter.GainStars
			or CardExtraEffectCountCardFilter.Heal
			or CardExtraEffectCountCardFilter.Strength
			or CardExtraEffectCountCardFilter.Dexterity
			or CardExtraEffectCountCardFilter.Focus
			or CardExtraEffectCountCardFilter.Weak
			or CardExtraEffectCountCardFilter.Frail
			or CardExtraEffectCountCardFilter.Vulnerable
			or CardExtraEffectCountCardFilter.Poison
			or CardExtraEffectCountCardFilter.Doom
			or CardExtraEffectCountCardFilter.Constrict
			or CardExtraEffectCountCardFilter.Artifact
			or CardExtraEffectCountCardFilter.Thorns
			or CardExtraEffectCountCardFilter.Regen
			or CardExtraEffectCountCardFilter.Plating
			or CardExtraEffectCountCardFilter.Intangible
			or CardExtraEffectCountCardFilter.Buffer
			or CardExtraEffectCountCardFilter.Vigor
			or CardExtraEffectCountCardFilter.Blur
			or CardExtraEffectCountCardFilter.Ritual
			or CardExtraEffectCountCardFilter.Summon
			or CardExtraEffectCountCardFilter.Forge
			or CardExtraEffectCountCardFilter.LoseHp;
	}

	private static bool IsThisCardHistoryCountEvent(CardExtraEffectCountEvent ev)
	{
		return ev is CardExtraEffectCountEvent.ThisCardPlayed
			or CardExtraEffectCountEvent.ThisCardDrawn
			or CardExtraEffectCountEvent.ThisCardDiscarded
			or CardExtraEffectCountEvent.ThisCardExhausted;
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
			case CardExtraEffectCountEvent.ThisCardDamageDealt:
				singularResource = CardEditorLoc.T("cardText.resource.damage", "damage");
				pluralResource = singularResource;
				presentVerb = CardEditorLoc.Enum("historyVerbPresent", ev, "deal");
				simplePastVerb = CardEditorLoc.Enum("historyVerbPast", ev, "dealt");
				perfectVerb = simplePastVerb;
				return true;
			case CardExtraEffectCountEvent.DamageTaken:
				singularResource = CardEditorLoc.T("cardText.resource.hp", "HP");
				pluralResource = singularResource;
				presentVerb = CardEditorLoc.Enum("historyVerbPresent", ev, "lose");
				simplePastVerb = CardEditorLoc.Enum("historyVerbPast", ev, "lost");
				perfectVerb = CardEditorLoc.Enum("historyVerbPerfect", ev, "lost");
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
			case CardExtraEffectCountEvent.TimesDealtDamage:
				singularResource = "time";
				pluralResource = "times";
				presentVerb = "deal damage";
				simplePastVerb = "dealt damage";
				perfectVerb = "dealt damage";
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

	private static bool TryGetStatusCountText(CardExtraEffectCountEvent ev, CardExtraEffectEnemyStatus status, out string statusText, out string presentVerb, out string simplePastVerb, out string perfectVerb, string? powerId = null)
	{
		statusText = GetConfiguredStatusLabel(status, powerId);
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
			CardExtraEffectKind.ApplyWeak or CardExtraEffectKind.RemoveWeak => CardExtraEffectEnemyStatus.Weak,
			CardExtraEffectKind.ApplyFrail or CardExtraEffectKind.RemoveFrail => CardExtraEffectEnemyStatus.Frail,
			CardExtraEffectKind.ApplyVulnerable or CardExtraEffectKind.RemoveVulnerable => CardExtraEffectEnemyStatus.Vulnerable,
			CardExtraEffectKind.ApplyPoison or CardExtraEffectKind.RemovePoison => CardExtraEffectEnemyStatus.Poison,
			CardExtraEffectKind.ApplyDoom or CardExtraEffectKind.RemoveDoom => CardExtraEffectEnemyStatus.Doom,
			CardExtraEffectKind.ApplyConstrict or CardExtraEffectKind.RemoveConstrict => CardExtraEffectEnemyStatus.Constrict,
			CardExtraEffectKind.GainArtifact or CardExtraEffectKind.RemoveArtifact => CardExtraEffectEnemyStatus.Artifact,
			CardExtraEffectKind.GainThorns or CardExtraEffectKind.RemoveThorns => CardExtraEffectEnemyStatus.Thorns,
			CardExtraEffectKind.GainRegen or CardExtraEffectKind.RemoveRegen => CardExtraEffectEnemyStatus.Regen,
			CardExtraEffectKind.GainPlating or CardExtraEffectKind.RemovePlating => CardExtraEffectEnemyStatus.Plating,
			CardExtraEffectKind.GainIntangible or CardExtraEffectKind.RemoveIntangible => CardExtraEffectEnemyStatus.Intangible,
			CardExtraEffectKind.GainBuffer or CardExtraEffectKind.RemoveBuffer => CardExtraEffectEnemyStatus.Buffer,
			CardExtraEffectKind.GainVigor or CardExtraEffectKind.RemoveVigor => CardExtraEffectEnemyStatus.Vigor,
			CardExtraEffectKind.GainBlur or CardExtraEffectKind.RemoveBlur => CardExtraEffectEnemyStatus.Blur,
			CardExtraEffectKind.GainRitual or CardExtraEffectKind.RemoveRitual => CardExtraEffectEnemyStatus.Ritual,
			_ => default
		};

		return kind is CardExtraEffectKind.GainStrength
			or CardExtraEffectKind.LoseStrength
			or CardExtraEffectKind.GainDexterity
			or CardExtraEffectKind.LoseDexterity
			or CardExtraEffectKind.GainFocus
			or CardExtraEffectKind.LoseFocus
			or CardExtraEffectKind.ApplyWeak
			or CardExtraEffectKind.RemoveWeak
			or CardExtraEffectKind.ApplyFrail
			or CardExtraEffectKind.RemoveFrail
			or CardExtraEffectKind.ApplyVulnerable
			or CardExtraEffectKind.RemoveVulnerable
			or CardExtraEffectKind.ApplyPoison
			or CardExtraEffectKind.RemovePoison
			or CardExtraEffectKind.ApplyDoom
			or CardExtraEffectKind.RemoveDoom
			or CardExtraEffectKind.ApplyConstrict
			or CardExtraEffectKind.RemoveConstrict
			or CardExtraEffectKind.GainArtifact
			or CardExtraEffectKind.RemoveArtifact
			or CardExtraEffectKind.GainThorns
			or CardExtraEffectKind.RemoveThorns
			or CardExtraEffectKind.GainRegen
			or CardExtraEffectKind.RemoveRegen
			or CardExtraEffectKind.GainPlating
			or CardExtraEffectKind.RemovePlating
			or CardExtraEffectKind.GainIntangible
			or CardExtraEffectKind.RemoveIntangible
			or CardExtraEffectKind.GainBuffer
			or CardExtraEffectKind.RemoveBuffer
			or CardExtraEffectKind.GainVigor
			or CardExtraEffectKind.RemoveVigor
			or CardExtraEffectKind.GainBlur
			or CardExtraEffectKind.RemoveBlur
			or CardExtraEffectKind.GainRitual
			or CardExtraEffectKind.RemoveRitual;
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
			CardExtraEffectCountCardFilter.CreatesCards => "Creates Cards",
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

	private static string CountCardFilterPrefixLabel(CardExtraEffectCountCardFilter filter)
	{
		return filter switch
		{
			CardExtraEffectCountCardFilter.CreatesCards => string.Empty,
			CardExtraEffectCountCardFilter.Exhaust => GrantedKeywordLabel(CardKeyword.Exhaust),
			CardExtraEffectCountCardFilter.Ethereal => GrantedKeywordLabel(CardKeyword.Ethereal),
			CardExtraEffectCountCardFilter.Innate => GrantedKeywordLabel(CardKeyword.Innate),
			CardExtraEffectCountCardFilter.Retain => GrantedKeywordLabel(CardKeyword.Retain),
			CardExtraEffectCountCardFilter.Sly => GrantedKeywordLabel(CardKeyword.Sly),
			CardExtraEffectCountCardFilter.Eternal => GrantedKeywordLabel(CardKeyword.Eternal),
			_ => CountCardFilterLabel(filter)
		};
	}

private static string CountCardFilterAmountLabel(CardExtraEffectCountCardFilter filter)
{
	return filter switch
	{
			CardExtraEffectCountCardFilter.DealDamage => "damage",
			CardExtraEffectCountCardFilter.GainBlock => "block",
			CardExtraEffectCountCardFilter.DrawCards => "cards drawn",
			CardExtraEffectCountCardFilter.GainEnergy => "Energy",
			CardExtraEffectCountCardFilter.GainStars => "Stars",
			CardExtraEffectCountCardFilter.Heal => "HP recovered",
			CardExtraEffectCountCardFilter.LoseHp => "HP lost",
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
		_ => CountCardFilterLabel(filter)
	};
}

internal static CardExtraEffectCountAggregationMode GetEffectiveCountAggregationMode(CardExtraEffect? effect)
{
	return GetEffectiveCountAggregationMode(effect?.CountAggregationMode ?? CardExtraEffectCountAggregationMode.CardCount, effect?.CountUsesCardEffectAmount ?? false, effect?.CountEvent ?? CardExtraEffectCountEvent.Played);
}

internal static CardExtraEffectCountAggregationMode GetEffectiveBranchCountAggregationMode(CardExtraEffect? effect)
{
	return GetEffectiveCountAggregationMode(effect?.BranchCountAggregationMode ?? CardExtraEffectCountAggregationMode.CardCount, effect?.BranchCountUsesCardEffectAmount ?? false, effect?.BranchCountEvent ?? CardExtraEffectCountEvent.Played);
}

private static CardExtraEffectCountAggregationMode GetEffectiveCountAggregationMode(CardExtraEffectCountAggregationMode mode, bool legacyUsesEffectAmount, CardExtraEffectCountEvent countEvent)
{
	if (!CountEventUsesCardFilters(countEvent))
	{
		return CardExtraEffectCountAggregationMode.CardCount;
	}

	return mode switch
	{
		CardExtraEffectCountAggregationMode.MatchingEffectAmount
			or CardExtraEffectCountAggregationMode.CurrentEnergyCost
			or CardExtraEffectCountAggregationMode.BaseEnergyCost
			or CardExtraEffectCountAggregationMode.CurrentStarCost
			or CardExtraEffectCountAggregationMode.BaseStarCost => mode,
		_ => legacyUsesEffectAmount
			? CardExtraEffectCountAggregationMode.MatchingEffectAmount
			: CardExtraEffectCountAggregationMode.CardCount
	};
}

private static bool UsesCountCardAggregateAmount(CardExtraEffect effect)
{
	if (effect == null || !CountEventUsesCardFilters(effect.CountEvent))
	{
		return false;
	}

	return GetEffectiveCountAggregationMode(effect) != CardExtraEffectCountAggregationMode.CardCount;
}

private static CardExtraEffectCountCardFilter GetEffectiveCountCardFilter(CardExtraEffect effect)
{
	CardExtraEffectCountCardFilter filter = effect?.CountCardFilter ?? CardExtraEffectCountCardFilter.Any;
	if (filter == CardExtraEffectCountCardFilter.Any && effect?.CountOnlyBlockCards == true)
	{
			filter = CardExtraEffectCountCardFilter.GainBlock;
		}

		return filter;
	}

private static bool UsesCountCardEffectAmount(CardExtraEffect effect)
{
	if (effect == null || !CountEventUsesCardFilters(effect.CountEvent))
	{
		return false;
	}

	return GetEffectiveCountAggregationMode(effect) == CardExtraEffectCountAggregationMode.MatchingEffectAmount
		&& CountCardFilterSupportsAmount(GetEffectiveCountCardFilter(effect));
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

	private static string GetConfiguredStatusLabel(CardExtraEffectEnemyStatus status, string? powerId)
	{
		if (!string.IsNullOrWhiteSpace(powerId))
		{
			return ResolvePowerTitle(powerId) ?? CardEditorLoc.T("cardText.power.unknown", "Unknown Power");
		}

		return EnemyStatusLabel(status);
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

	public static string ValueSourceActorLabel(CardExtraEffectValueSourceActor actor)
	{
		string fallback = actor switch
		{
			CardExtraEffectValueSourceActor.Self => "Self",
			CardExtraEffectValueSourceActor.Target => "Target",
			CardExtraEffectValueSourceActor.AllEnemies => "All Enemies",
			CardExtraEffectValueSourceActor.AllAllies => "All Allies",
			_ => actor.ToString()
		};
		return CardEditorLoc.Enum("valueSource.actor", actor, fallback);
	}

	public static string ValueSourceModeLabel(CardExtraEffectValueSourceMode mode)
	{
		string fallback = mode switch
		{
			CardExtraEffectValueSourceMode.Common => "Common",
			CardExtraEffectValueSourceMode.PowerStatus => "Power / Status",
			_ => mode.ToString()
		};
		return CardEditorLoc.Enum("valueSource.mode", mode, fallback);
	}

	public static string ValueSourceAggregationLabel(CardExtraEffectValueSourceAggregation aggregation)
	{
		string fallback = aggregation switch
		{
			CardExtraEffectValueSourceAggregation.Value => "Value",
			CardExtraEffectValueSourceAggregation.Sum => "Sum",
			CardExtraEffectValueSourceAggregation.Highest => "Highest",
			CardExtraEffectValueSourceAggregation.Lowest => "Lowest",
			CardExtraEffectValueSourceAggregation.Average => "Average",
			_ => aggregation.ToString()
		};
		return CardEditorLoc.Enum("valueSource.aggregation", aggregation, fallback);
	}

	public static string ValueSourceKindLabel(CardExtraEffectValueSourceKind kind)
	{
		string fallback = kind switch
		{
			CardExtraEffectValueSourceKind.CurrentHp => "Current HP",
			CardExtraEffectValueSourceKind.MaxHp => "Max HP",
			CardExtraEffectValueSourceKind.MissingHp => "Missing HP",
			CardExtraEffectValueSourceKind.Block => "Block",
			CardExtraEffectValueSourceKind.Strength => "Strength",
			CardExtraEffectValueSourceKind.Dexterity => "Dexterity",
			CardExtraEffectValueSourceKind.Focus => "Focus",
			CardExtraEffectValueSourceKind.Weak => "Weak",
			CardExtraEffectValueSourceKind.Frail => "Frail",
			CardExtraEffectValueSourceKind.Vulnerable => "Vulnerable",
			CardExtraEffectValueSourceKind.Poison => "Poison",
			CardExtraEffectValueSourceKind.Doom => "Doom",
			CardExtraEffectValueSourceKind.Constrict => "Constrict",
			CardExtraEffectValueSourceKind.Artifact => "Artifact",
			CardExtraEffectValueSourceKind.Thorns => "Thorns",
			CardExtraEffectValueSourceKind.Regen => "Regen",
			CardExtraEffectValueSourceKind.Plating => "Plating",
			CardExtraEffectValueSourceKind.Intangible => "Intangible",
			CardExtraEffectValueSourceKind.Buffer => "Buffer",
			CardExtraEffectValueSourceKind.Vigor => "Vigor",
			CardExtraEffectValueSourceKind.Blur => "Blur",
			CardExtraEffectValueSourceKind.Ritual => "Ritual",
			_ => kind.ToString()
		};
		return CardEditorLoc.Enum("valueSource.kind", kind, fallback);
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

	public static string TransformModeLabel(CardExtraEffectTransformMode mode)
	{
		string fallback = mode switch
		{
			CardExtraEffectTransformMode.Random => "Random",
			CardExtraEffectTransformMode.SpecificCard => "Specific Card",
			_ => mode.ToString()
		};
		return CardEditorLoc.Enum("transformMode", mode, fallback);
	}

	public static string ConditionalBonusConditionLabel(CardExtraEffectConditionalBonusCondition condition)
	{
		string fallback = condition switch
		{
			CardExtraEffectConditionalBonusCondition.None => "None",
			CardExtraEffectConditionalBonusCondition.TargetHasBlock => "Target Has Block",
			CardExtraEffectConditionalBonusCondition.TargetHasStatus => "Target Has Status",
			CardExtraEffectConditionalBonusCondition.TargetHasIntent => "Target Has Intent",
			CardExtraEffectConditionalBonusCondition.SelfHasBlock => "You Have Block",
			CardExtraEffectConditionalBonusCondition.SelfHasStatus => "You Have Status",
			CardExtraEffectConditionalBonusCondition.TargetHasNoBlock => "Target Has No Block",
			CardExtraEffectConditionalBonusCondition.SelfHasNoBlock => "You Have No Block",
			CardExtraEffectConditionalBonusCondition.TargetLacksStatus => "Target Lacks Status",
			CardExtraEffectConditionalBonusCondition.SelfLacksStatus => "You Lack Status",
			CardExtraEffectConditionalBonusCondition.TargetIntentIsNot => "Target Intent Is Not",
			CardExtraEffectConditionalBonusCondition.TargetIsDamaged => "Target Is Damaged",
			CardExtraEffectConditionalBonusCondition.SelfIsDamaged => "You Are Damaged",
			CardExtraEffectConditionalBonusCondition.TargetIsBloodied => "Target Is Bloodied",
			CardExtraEffectConditionalBonusCondition.SelfIsBloodied => "You Are Bloodied",
			CardExtraEffectConditionalBonusCondition.TargetIsFullHp => "Target Is At Full HP",
			CardExtraEffectConditionalBonusCondition.SelfIsFullHp => "You Are At Full HP",
			CardExtraEffectConditionalBonusCondition.TargetIsNotBloodied => "Target Is Not Bloodied",
			CardExtraEffectConditionalBonusCondition.SelfIsNotBloodied => "You Are Not Bloodied",
			CardExtraEffectConditionalBonusCondition.TargetHasLessHpThanYou => "Target Has Less HP Than You",
			CardExtraEffectConditionalBonusCondition.TargetHasMoreHpThanYou => "Target Has More HP Than You",
			CardExtraEffectConditionalBonusCondition.TargetHasLessBlockThanYou => "Target Has Less Block Than You",
			CardExtraEffectConditionalBonusCondition.TargetHasMoreBlockThanYou => "Target Has More Block Than You",
			_ => condition.ToString()
		};
		return CardEditorLoc.Enum("conditionalBonusCondition", condition, fallback);
	}

	public static string BranchModeLabel(CardExtraEffectBranchMode mode)
	{
		string fallback = mode switch
		{
			CardExtraEffectBranchMode.None => "None",
			CardExtraEffectBranchMode.InsteadIf => "Instead If",
			CardExtraEffectBranchMode.AlsoIf => "Also If",
			_ => mode.ToString()
		};
		return CardEditorLoc.Enum("branchMode", mode, fallback);
	}

	public static string BranchConditionTypeLabel(CardExtraEffectBranchConditionType type)
	{
		string fallback = type switch
		{
			CardExtraEffectBranchConditionType.None => "None",
			CardExtraEffectBranchConditionType.TargetCheck => "Target Check",
			CardExtraEffectBranchConditionType.HistoryCount => "History / Count",
			_ => type.ToString()
		};
		return CardEditorLoc.Enum("branchConditionType", type, fallback);
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
			CardCreatedCardsCostDuration.Permanent => "Permanent",
			_ => duration.ToString()
		};
		return CardEditorLoc.Enum("createdCostDuration", duration, fallback);
	}

	public static string CreatedCardsCostResourceLabel(CardCreatedCardsCostResource resource)
	{
		string fallback = resource switch
		{
			CardCreatedCardsCostResource.Stars => "Stars",
			_ => "Energy"
		};
		return CardEditorLoc.Enum("createdCostResource", resource, fallback);
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
			CardExtraEffectCardSelectionMode.Top => "Top",
			CardExtraEffectCardSelectionMode.Bottom => "Bottom",
			_ => mode.ToString()
		};
		return CardEditorLoc.Enum("cardSelectionMode", mode, fallback);
	}

	public static string ResourceConsumptionModeLabel(CardExtraEffectResourceConsumptionMode mode)
	{
		string fallback = mode switch
		{
			CardExtraEffectResourceConsumptionMode.Vigor => "Vigor",
			CardExtraEffectResourceConsumptionMode.SelfHpAndSelfDamage => "Self HP / Damage",
			CardExtraEffectResourceConsumptionMode.SpecificStatStatus => "Specific Stat / Status",
			CardExtraEffectResourceConsumptionMode.SpecificPowerStatus => "Specific Power / Status",
			_ => mode.ToString()
		};
		return CardEditorLoc.Enum("resourceConsumptionMode", mode, fallback);
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

	internal static bool SupportsFutureMatchingCardAura(CardExtraEffectKind kind, bool grantToCard)
	{
		if (kind == CardExtraEffectKind.GrantKeywordToPile || kind == CardExtraEffectKind.GrantReplay)
		{
			return true;
		}

		return grantToCard && kind != CardExtraEffectKind.EnchantCard;
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

	public static string CardCostsLessModifierLabel(CardExtraEffectCostModifier modifier)
	{
		string fallback = modifier switch
		{
			CardExtraEffectCostModifier.Reduce => "Reduce",
			CardExtraEffectCostModifier.Free => "Set to 0",
			CardExtraEffectCostModifier.HalfCost => "Half Cost",
			CardExtraEffectCostModifier.FreeToPlay => "Free to Play",
			_ => modifier.ToString()
		};
		return CardEditorLoc.Enum("cardCostsLessModifier", modifier, fallback);
	}

	public static string CostFilterModeLabel(CardExtraEffectCostFilterMode mode)
	{
		string fallback = mode switch
		{
			CardExtraEffectCostFilterMode.AtMost => "<=",
			CardExtraEffectCostFilterMode.AtLeast => ">=",
			CardExtraEffectCostFilterMode.Exactly => "=",
			_ => mode.ToString()
		};
		return CardEditorLoc.Enum("costFilterMode", mode, fallback);
	}

	internal static CardExtraEffectCostModifier GetEffectiveCardCostsLessModifier(CardExtraEffect? effect)
	{
		if (effect == null)
		{
			return CardExtraEffectCostModifier.Reduce;
		}

		if (effect.Kind == CardExtraEffectKind.CreatedCardsCostLess && effect.Amount == -1)
		{
			return CardExtraEffectCostModifier.Free;
		}

		if (effect.Kind == CardExtraEffectKind.CreatedCardsCostLess
			&& effect.CreatedCardsCostDuration == CardCreatedCardsCostDuration.Permanent
			&& effect.CardCostsLessModifier == CardExtraEffectCostModifier.FreeToPlay)
		{
			return CardExtraEffectCostModifier.Free;
		}

		return effect.CardCostsLessModifier;
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
			CardExtraEffectOrbAction.TriggerPassive => "Trigger Passive",
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
			and not CardExtraEffectKind.SelfScaling
			and not CardExtraEffectKind.PersistentSelfScaling
			and not CardExtraEffectKind.ScalingStage
			and not CardExtraEffectKind.IgnoreBlock
			and not CardExtraEffectKind.IgnoreDamageModifiers
			and not CardExtraEffectKind.IgnoreDamageCaps
			and not CardExtraEffectKind.IgnoreDamageNegation
			and not CardExtraEffectKind.IgnoreEnemyDamageReductions
			and not CardExtraEffectKind.DoesNotConsumeVigor
			and not CardExtraEffectKind.HitsAllEnemies
			and not CardExtraEffectKind.CardDealsExtraDamage
			and not CardExtraEffectKind.AutoPlaySelfFromPile
			and not CardExtraEffectKind.DrawCardsThatCostLess
			and not CardExtraEffectKind.AutoDrawSelfFromPile
			and not CardExtraEffectKind.ConditionalAutoPlayFromPile
			and not CardExtraEffectKind.ConditionalAutoDrawFromPile
			;
	}

	public static bool SupportsGrantToCard(CardExtraEffectKind kind)
	{
		return kind is not CardExtraEffectKind.CreatedCardsCostLess
			and not CardExtraEffectKind.CreatedCardsUpgraded
			and not CardExtraEffectKind.GeneratedCardsUpgraded
			and not CardExtraEffectKind.CardsInPileUpgradedAura
			and not CardExtraEffectKind.MoveCardsBetweenPiles
			and not CardExtraEffectKind.UpgradeCardsInPile
			and not CardExtraEffectKind.PlayCardFromPile
			and not CardExtraEffectKind.AutoPlaySelfFromPile
			and not CardExtraEffectKind.AutoDrawSelfFromPile
			and not CardExtraEffectKind.ConditionalAutoPlayFromPile
			and not CardExtraEffectKind.ConditionalAutoDrawFromPile
			and not CardExtraEffectKind.AddExactCopyOfThisCardToDeck
			and not CardExtraEffectKind.CopyCardsFromPileToDeck
			and not CardExtraEffectKind.CopyExactCardsFromPileToDeck
			and not CardExtraEffectKind.RemoveCardsFromDeck
			and not CardExtraEffectKind.DrawCardsThatCostLess
			and not CardExtraEffectKind.DiscardCards
			and not CardExtraEffectKind.ExhaustCards
			and not CardExtraEffectKind.SelfScaling
			and not CardExtraEffectKind.PersistentSelfScaling
			and not CardExtraEffectKind.GrantKeywordToPile
			and not CardExtraEffectKind.UpgradeDeckCards;
	}

	internal static bool IsSelfScalingKind(CardExtraEffectKind kind)
		=> kind is CardExtraEffectKind.SelfScaling or CardExtraEffectKind.PersistentSelfScaling;

	internal static bool IsSelfCardCostModifierKind(CardExtraEffectKind kind)
		=> kind is CardExtraEffectKind.CardCostsLess or CardExtraEffectKind.CardStarCostsLess;

	internal static bool UsesSourceCardForImmediatePowerExecution(CardExtraEffect? effect)
	{
		return effect != null
			&& !effect.GrantToCard
			&& (IsSelfCardCostModifierKind(effect.Kind) || IsSelfScalingKind(effect.Kind));
	}

	internal static CardModel ResolveImmediatePowerExecutionCard(CardModel sourceCard, CardPlay? triggerPlay, CardExtraEffect? effect)
	{
		if (sourceCard == null)
		{
			return triggerPlay?.Card!;
		}

		return UsesSourceCardForImmediatePowerExecution(effect)
			? sourceCard
			: triggerPlay?.Card ?? sourceCard;
	}

	public static bool SupportsSelfScalingEffectRowField(CardExtraEffect candidate, CardExtraEffectSelfScalingField field)
	{
		if (candidate == null || IsSelfScalingKind(candidate.Kind))
		{
			return false;
		}

		return field switch
		{
			CardExtraEffectSelfScalingField.Amount => candidate.Kind is not (
				CardExtraEffectKind.EndTurn
				or CardExtraEffectKind.RunEffectSourceCard
				or CardExtraEffectKind.ChooseOneEffectSource
				or CardExtraEffectKind.ScalingStage
				or CardExtraEffectKind.CleanseDebuffs
				or CardExtraEffectKind.CleanseBuffs
				or CardExtraEffectKind.DoesNotConsumeVigor
				or CardExtraEffectKind.HitsAllEnemies),
			CardExtraEffectSelfScalingField.Repeat => SupportsRepeat(candidate.Kind),
			CardExtraEffectSelfScalingField.SecondaryAmount => candidate.ConditionalBonusAmount != 0
				|| candidate.CountComparison != CardExtraEffectCountComparison.None
				|| candidate.ScaleMode == CardExtraEffectScaleMode.PerHistoryCount,
			CardExtraEffectSelfScalingField.Threshold => candidate.CountComparison != CardExtraEffectCountComparison.None,
			CardExtraEffectSelfScalingField.Duration => SupportsSelfScalingDurationField(candidate),
			_ => false
		};
	}

	private static bool SupportsSelfScalingDurationField(CardExtraEffect candidate)
	{
		if (candidate == null)
		{
			return false;
		}

		if (IsPowerEffect(candidate) && candidate.TriggerMaxTurns > 0)
		{
			return true;
		}

		return candidate.CardGrantDuration == CardExtraEffectCardGrantDuration.Turns
			|| candidate.EnchantmentDuration == CardExtraEffectEnchantmentDuration.Turns
			|| candidate.CardCostsLessDuration == CardExtraEffectCardCostsLessDuration.Turns
			|| candidate.CreatedCardsCostDuration == CardCreatedCardsCostDuration.Turns
			|| candidate.Timing != CardExtraEffectTiming.Immediate;
	}

	internal static bool SupportsAppliedEffectRowAmountSource(CardExtraEffectKind kind)
	{
		return kind is CardExtraEffectKind.GainBlock
			or CardExtraEffectKind.DealDamage
			or CardExtraEffectKind.CardDealsExtraDamage
			or CardExtraEffectKind.Heal
			or CardExtraEffectKind.LoseHp
			or CardExtraEffectKind.GainMaxHp
			or CardExtraEffectKind.LoseMaxHp
			or CardExtraEffectKind.GainStrength
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
			or CardExtraEffectKind.RemoveWeak
			or CardExtraEffectKind.RemoveFrail
			or CardExtraEffectKind.RemoveVulnerable
			or CardExtraEffectKind.RemovePoison
			or CardExtraEffectKind.RemoveDoom
			or CardExtraEffectKind.GainArtifact
			or CardExtraEffectKind.GainThorns
			or CardExtraEffectKind.GainRegen
			or CardExtraEffectKind.GainPlating
			or CardExtraEffectKind.GainIntangible
			or CardExtraEffectKind.GainBuffer
			or CardExtraEffectKind.GainVigor
			or CardExtraEffectKind.GainBlur
			or CardExtraEffectKind.GainRitual
			or CardExtraEffectKind.RemoveArtifact
			or CardExtraEffectKind.RemoveThorns
			or CardExtraEffectKind.RemoveRegen
			or CardExtraEffectKind.RemovePlating
			or CardExtraEffectKind.RemoveIntangible
			or CardExtraEffectKind.RemoveBuffer
			or CardExtraEffectKind.RemoveVigor
			or CardExtraEffectKind.RemoveBlur
			or CardExtraEffectKind.RemoveRitual
			or CardExtraEffectKind.ApplyConstrict
			or CardExtraEffectKind.RemoveConstrict
			or CardExtraEffectKind.ApplyPower
			or CardExtraEffectKind.GainStatusEqualToStatus
			or CardExtraEffectKind.Summon
			or CardExtraEffectKind.Forge
			or CardExtraEffectKind.GainEnergy
			or CardExtraEffectKind.LoseEnergy
			or CardExtraEffectKind.GainStars
			or CardExtraEffectKind.LoseStars
			or CardExtraEffectKind.RemoveBlock
			or CardExtraEffectKind.GainGold
			or CardExtraEffectKind.LoseGold;
	}

	internal static bool SupportsValueSourceAmountSource(CardExtraEffectKind kind)
		=> SupportsAppliedEffectRowAmountSource(kind);

	internal static bool TryGetEffectPowerMatchStatus(CardExtraEffectKind kind, out CardExtraEffectEnemyStatus status)
	{
		switch (kind)
		{
			case CardExtraEffectKind.ApplyWeak:
			case CardExtraEffectKind.RemoveWeak:
				status = CardExtraEffectEnemyStatus.Weak;
				return true;
			case CardExtraEffectKind.ApplyFrail:
			case CardExtraEffectKind.RemoveFrail:
				status = CardExtraEffectEnemyStatus.Frail;
				return true;
			case CardExtraEffectKind.ApplyVulnerable:
			case CardExtraEffectKind.RemoveVulnerable:
				status = CardExtraEffectEnemyStatus.Vulnerable;
				return true;
			case CardExtraEffectKind.ApplyPoison:
			case CardExtraEffectKind.RemovePoison:
				status = CardExtraEffectEnemyStatus.Poison;
				return true;
			case CardExtraEffectKind.ApplyDoom:
			case CardExtraEffectKind.RemoveDoom:
				status = CardExtraEffectEnemyStatus.Doom;
				return true;
			case CardExtraEffectKind.ApplyConstrict:
			case CardExtraEffectKind.RemoveConstrict:
				status = CardExtraEffectEnemyStatus.Constrict;
				return true;
			case CardExtraEffectKind.GainArtifact:
			case CardExtraEffectKind.RemoveArtifact:
				status = CardExtraEffectEnemyStatus.Artifact;
				return true;
			case CardExtraEffectKind.GainThorns:
			case CardExtraEffectKind.RemoveThorns:
				status = CardExtraEffectEnemyStatus.Thorns;
				return true;
			case CardExtraEffectKind.GainRegen:
			case CardExtraEffectKind.RemoveRegen:
				status = CardExtraEffectEnemyStatus.Regen;
				return true;
			case CardExtraEffectKind.GainPlating:
			case CardExtraEffectKind.RemovePlating:
				status = CardExtraEffectEnemyStatus.Plating;
				return true;
			case CardExtraEffectKind.GainIntangible:
			case CardExtraEffectKind.RemoveIntangible:
				status = CardExtraEffectEnemyStatus.Intangible;
				return true;
			case CardExtraEffectKind.GainBuffer:
			case CardExtraEffectKind.RemoveBuffer:
				status = CardExtraEffectEnemyStatus.Buffer;
				return true;
			case CardExtraEffectKind.GainVigor:
			case CardExtraEffectKind.RemoveVigor:
				status = CardExtraEffectEnemyStatus.Vigor;
				return true;
			case CardExtraEffectKind.GainBlur:
			case CardExtraEffectKind.RemoveBlur:
				status = CardExtraEffectEnemyStatus.Blur;
				return true;
			case CardExtraEffectKind.GainRitual:
			case CardExtraEffectKind.RemoveRitual:
				status = CardExtraEffectEnemyStatus.Ritual;
				return true;
			case CardExtraEffectKind.GainStrength:
			case CardExtraEffectKind.LoseStrength:
				status = CardExtraEffectEnemyStatus.Strength;
				return true;
			case CardExtraEffectKind.GainDexterity:
			case CardExtraEffectKind.LoseDexterity:
				status = CardExtraEffectEnemyStatus.Dexterity;
				return true;
			case CardExtraEffectKind.GainFocus:
			case CardExtraEffectKind.LoseFocus:
				status = CardExtraEffectEnemyStatus.Focus;
				return true;
			default:
				status = default;
				return false;
		}
	}

	private static bool UsesAppliedEffectRowAmountSource(CardExtraEffect effect)
	{
		return effect != null
			&& SupportsAppliedEffectRowAmountSource(effect.Kind)
			&& effect.AmountSourceMode == CardExtraEffectAmountSourceMode.AppliedEffectRow;
	}

	private static bool UsesValueSourceAmountSource(CardExtraEffect effect)
	{
		return effect != null
			&& SupportsValueSourceAmountSource(effect.Kind)
			&& effect.AmountSourceMode == CardExtraEffectAmountSourceMode.ValueSource;
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
			CardExtraEffectKind.GainStatusEqualToStatus => amount >= 1,
			CardExtraEffectKind.SelfScaling => amount > 0,
			CardExtraEffectKind.PersistentSelfScaling => amount > 0,
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
		// Back-compat helper for callers that don't have the base effect available.
		// This is intentionally conservative and only checks numeric/delta fields.
		if (effect == null)
		{
			return false;
		}

		if (effect.DisableOnUpgrade || IsValidUpgradeDeltaAmount(effect.Kind, effect.Amount))
		{
			return true;
		}

		if (effect.IncludeSourceCardInSelection
			|| effect.FutureMatchingCards
			|| effect.AmountSourceMode != CardExtraEffectAmountSourceMode.Fixed
			|| effect.ValueSourceMode != CardExtraEffectValueSourceMode.Common
			|| effect.ValueSourceActor != CardExtraEffectValueSourceActor.Self
			|| effect.ValueSourceAggregation != CardExtraEffectValueSourceAggregation.Value
			|| effect.ValueSourceKind != CardExtraEffectValueSourceKind.MaxHp
			|| !string.IsNullOrWhiteSpace(effect.ValueSourcePowerId)
			|| effect.ResourceConsumptionMode != CardExtraEffectResourceConsumptionMode.Vigor
			|| (effect.ResourceConsumptionMode == CardExtraEffectResourceConsumptionMode.SpecificStatStatus
				&& effect.ResourceConsumptionStat != CardExtraEffectMultiplierStat.Vigor)
			|| (effect.ResourceConsumptionMode == CardExtraEffectResourceConsumptionMode.SpecificPowerStatus
				&& !string.IsNullOrWhiteSpace(effect.PowerId))
			|| effect.MultiplierSourceMode != CardExtraEffectValueSourceMode.Common
			|| !string.IsNullOrWhiteSpace(effect.MultiplierPowerId)
			|| effect.StatusToStatusMode != CardExtraEffectStatusToStatusMode.Gain)
		{
			return true;
		}

		if (!secondaryNumericFieldsAreDeltas)
		{
			return false;
		}

		return effect.Turns != 0
			|| effect.RepeatIsX
			|| effect.RepeatCount != 0
			|| effect.TriggerEveryN != 0
			|| effect.TriggerMaxFires != 0
			|| effect.TriggerMaxTurns != 0
			|| effect.CreatedCardsCostTurns != 0
			|| effect.CreatedCardsCostResource != CardCreatedCardsCostResource.Energy
			|| effect.CardCostsLessTurns != 0
			|| effect.CountTurns != 0
			|| effect.CountConditionAmount != 0
			|| GetEffectiveCountAggregationMode(effect) != CardExtraEffectCountAggregationMode.CardCount
			|| effect.CardGrantTurns != 0
			|| effect.CardSelectionCount != 0
			|| effect.EnchantmentTurns != 0
			|| effect.ConditionalBonusAmount != 0
			|| effect.ConditionalBonusConditionType != CardExtraEffectBranchConditionType.None
			|| effect.ConditionalBonusCondition != CardExtraEffectConditionalBonusCondition.None
			|| !string.IsNullOrWhiteSpace(effect.ConditionalBonusPowerId)
			|| effect.BranchMode != CardExtraEffectBranchMode.None
			|| effect.BranchConditionType != CardExtraEffectBranchConditionType.None
			|| effect.BranchCondition != CardExtraEffectConditionalBonusCondition.None
			|| !string.IsNullOrWhiteSpace(effect.BranchPowerId)
			|| effect.BranchEffect != null
			|| effect.BranchCountTurns != 0
			|| effect.BranchCountCardPool != CardGeneratedCardPool.All
			|| effect.BranchCountCardType != CardGeneratedCardType.Any
			|| effect.BranchCountCardFilter != CardExtraEffectCountCardFilter.Any
			|| GetEffectiveBranchCountAggregationMode(effect) != CardExtraEffectCountAggregationMode.CardCount
			|| effect.BranchCountExcludeSourceCard
			|| effect.BranchCountOrbType != CardExtraEffectOrbType.Any
			|| effect.BranchCountOrbSelection != CardExtraEffectOrbSelection.Leftmost
			|| effect.BranchCountEnemyStatus != CardExtraEffectEnemyStatus.Weak
			|| !string.IsNullOrWhiteSpace(effect.BranchCountPowerId)
			|| effect.BranchCountEnemyIntent != CardExtraEffectEnemyIntent.Attack
			|| effect.BranchCountComparison != CardExtraEffectCountComparison.None
			|| effect.BranchCountConditionAmount != 0
			|| effect.PowerTriggerEnemyStatus != CardExtraEffectEnemyStatus.AnyPowerStatus
			|| !string.IsNullOrWhiteSpace(effect.PowerTriggerPowerId)
			|| !string.IsNullOrWhiteSpace(effect.CountPowerId)
			|| !string.IsNullOrWhiteSpace(effect.MatchCustomKeyword)
			|| !string.IsNullOrWhiteSpace(effect.CustomKeywordName)
			|| !string.IsNullOrWhiteSpace(effect.CustomPowerName)
			|| !string.IsNullOrWhiteSpace(effect.CustomPowerDescription)
			|| (effect.Kind == CardExtraEffectKind.ChooseOneEffectSource
				&& GetChooseOneOptions(effect).Count > 0)
			|| (effect.Kind == CardExtraEffectKind.TransformCards
				&& (effect.TransformMode != CardExtraEffectTransformMode.Random || !string.IsNullOrWhiteSpace(effect.SpecificCardId)));
	}

	internal static bool HasMeaningfulUpgradeBaseSlotDelta(CardExtraEffect baseEffect, CardExtraEffect upgradeEffect, bool secondaryNumericFieldsAreDeltas)
	{
		if (upgradeEffect == null)
		{
			return false;
		}

		if (upgradeEffect.DisableOnUpgrade || IsValidUpgradeDeltaAmount(upgradeEffect.Kind, upgradeEffect.Amount))
		{
			return true;
		}

		if (upgradeEffect.IncludeSourceCardInSelection
			|| upgradeEffect.FutureMatchingCards
			|| upgradeEffect.AmountSourceMode != CardExtraEffectAmountSourceMode.Fixed
			|| upgradeEffect.ValueSourceMode != CardExtraEffectValueSourceMode.Common
			|| upgradeEffect.ValueSourceActor != CardExtraEffectValueSourceActor.Self
			|| upgradeEffect.ValueSourceAggregation != CardExtraEffectValueSourceAggregation.Value
			|| upgradeEffect.ValueSourceKind != CardExtraEffectValueSourceKind.MaxHp
			|| !string.IsNullOrWhiteSpace(upgradeEffect.ValueSourcePowerId)
			|| upgradeEffect.ResourceConsumptionMode != CardExtraEffectResourceConsumptionMode.Vigor
			|| (upgradeEffect.ResourceConsumptionMode == CardExtraEffectResourceConsumptionMode.SpecificStatStatus
				&& upgradeEffect.ResourceConsumptionStat != CardExtraEffectMultiplierStat.Vigor)
			|| (upgradeEffect.ResourceConsumptionMode == CardExtraEffectResourceConsumptionMode.SpecificPowerStatus
				&& !string.IsNullOrWhiteSpace(upgradeEffect.PowerId))
			|| upgradeEffect.MultiplierSourceMode != CardExtraEffectValueSourceMode.Common
			|| !string.IsNullOrWhiteSpace(upgradeEffect.MultiplierPowerId)
			|| upgradeEffect.StatusToStatusMode != CardExtraEffectStatusToStatusMode.Gain)
		{
			return true;
		}

		if (secondaryNumericFieldsAreDeltas)
		{
			if (upgradeEffect.Turns != 0
				|| upgradeEffect.RepeatCount != 0
				|| upgradeEffect.TriggerEveryN != 0
				|| upgradeEffect.TriggerMaxFires != 0
				|| upgradeEffect.TriggerMaxTurns != 0
				|| upgradeEffect.CreatedCardsCostTurns != 0
				|| upgradeEffect.CreatedCardsCostResource != CardCreatedCardsCostResource.Energy
				|| upgradeEffect.CardCostsLessTurns != 0
				|| upgradeEffect.CountTurns != 0
				|| upgradeEffect.CountConditionAmount != 0
				|| upgradeEffect.CardGrantTurns != 0
				|| upgradeEffect.CardSelectionCount != 0
				|| upgradeEffect.EnchantmentTurns != 0
				|| upgradeEffect.ConditionalBonusAmount != 0
				|| upgradeEffect.ConditionalBonusConditionType != CardExtraEffectBranchConditionType.None
				|| !string.IsNullOrWhiteSpace(upgradeEffect.ConditionalBonusPowerId)
				|| upgradeEffect.BranchMode != CardExtraEffectBranchMode.None
				|| upgradeEffect.BranchConditionType != CardExtraEffectBranchConditionType.None
				|| upgradeEffect.BranchCondition != CardExtraEffectConditionalBonusCondition.None
				|| !string.IsNullOrWhiteSpace(upgradeEffect.BranchPowerId)
				|| !string.IsNullOrWhiteSpace(upgradeEffect.BranchCountPowerId)
				|| !string.IsNullOrWhiteSpace(upgradeEffect.PowerTriggerPowerId)
				|| !string.IsNullOrWhiteSpace(upgradeEffect.CountPowerId)
				|| upgradeEffect.BranchEffect != null)
			{
				return true;
			}
		}
		else
		{
			// If numeric fields are absolute, any non-default value is meaningful. This matches
			// the legacy behavior where upgrades stored absolute values instead of deltas.
			if (upgradeEffect.Turns != 0
				|| upgradeEffect.RepeatCount != 0
				|| upgradeEffect.TriggerEveryN != 0
				|| upgradeEffect.TriggerMaxFires != 0
				|| upgradeEffect.TriggerMaxTurns != 0
				|| upgradeEffect.CreatedCardsCostTurns != 0
				|| upgradeEffect.CreatedCardsCostResource != CardCreatedCardsCostResource.Energy
				|| upgradeEffect.CardCostsLessTurns != 0
				|| upgradeEffect.CountTurns != 0
				|| upgradeEffect.CountConditionAmount != 0
				|| upgradeEffect.CardGrantTurns != 0
				|| upgradeEffect.CardSelectionCount != 0
				|| upgradeEffect.EnchantmentTurns != 0
				|| upgradeEffect.ConditionalBonusAmount != 0
				|| upgradeEffect.ConditionalBonusConditionType != CardExtraEffectBranchConditionType.None
				|| upgradeEffect.ConditionalBonusCondition != CardExtraEffectConditionalBonusCondition.None
				|| !string.IsNullOrWhiteSpace(upgradeEffect.ConditionalBonusPowerId)
				|| upgradeEffect.BranchMode != CardExtraEffectBranchMode.None
				|| upgradeEffect.BranchConditionType != CardExtraEffectBranchConditionType.None
				|| upgradeEffect.BranchCondition != CardExtraEffectConditionalBonusCondition.None
				|| !string.IsNullOrWhiteSpace(upgradeEffect.BranchPowerId)
				|| !string.IsNullOrWhiteSpace(upgradeEffect.BranchCountPowerId)
				|| !string.IsNullOrWhiteSpace(upgradeEffect.PowerTriggerPowerId)
				|| !string.IsNullOrWhiteSpace(upgradeEffect.CountPowerId)
				|| upgradeEffect.BranchEffect != null)
			{
				return true;
			}
		}

		// Support non-numeric overrides for specific kinds where the upgrade UI exposes meaningful changes.
		// This fixes cases like OrbAction: Trigger Passive (One -> All) not persisting on upgrade.
		if (baseEffect != null && baseEffect.Kind == CardExtraEffectKind.OrbAction && upgradeEffect.Kind == CardExtraEffectKind.OrbAction)
		{
			return baseEffect.OrbAction != upgradeEffect.OrbAction
				|| baseEffect.OrbScope != upgradeEffect.OrbScope
				|| baseEffect.OrbType != upgradeEffect.OrbType
				|| baseEffect.OrbSelection != upgradeEffect.OrbSelection
				|| baseEffect.OrbFollowUp != upgradeEffect.OrbFollowUp;
		}

		if (baseEffect != null)
		{
			if (baseEffect.RepeatIsX != upgradeEffect.RepeatIsX)
			{
				return true;
			}

			if (baseEffect.IncludeSourceCardInSelection != upgradeEffect.IncludeSourceCardInSelection)
			{
				return true;
			}

			if (baseEffect.FutureMatchingCards != upgradeEffect.FutureMatchingCards)
			{
				return true;
			}

			if (baseEffect.AmountSourceMode != upgradeEffect.AmountSourceMode)
			{
				return true;
			}

			if (baseEffect.ValueSourceMode != upgradeEffect.ValueSourceMode
				|| baseEffect.ValueSourceActor != upgradeEffect.ValueSourceActor
				|| baseEffect.ValueSourceAggregation != upgradeEffect.ValueSourceAggregation
				|| baseEffect.ValueSourceKind != upgradeEffect.ValueSourceKind
				|| !string.Equals(baseEffect.ValueSourcePowerId ?? string.Empty, upgradeEffect.ValueSourcePowerId ?? string.Empty, StringComparison.Ordinal))
			{
				return true;
			}

			if (baseEffect.ResourceConsumptionMode != upgradeEffect.ResourceConsumptionMode)
			{
				return true;
			}

			if (baseEffect.MultiplierSourceMode != upgradeEffect.MultiplierSourceMode
				|| !string.Equals(baseEffect.MultiplierPowerId ?? string.Empty, upgradeEffect.MultiplierPowerId ?? string.Empty, StringComparison.Ordinal))
			{
				return true;
			}

			if (baseEffect.ResourceConsumptionStat != upgradeEffect.ResourceConsumptionStat)
			{
				return true;
			}

			if (!string.Equals(baseEffect.PowerId ?? string.Empty, upgradeEffect.PowerId ?? string.Empty, StringComparison.Ordinal))
			{
				return true;
			}

			if (baseEffect.StatusToStatusMode != upgradeEffect.StatusToStatusMode)
			{
				return true;
			}

			if (baseEffect.CountExcludeSourceCard != upgradeEffect.CountExcludeSourceCard)
			{
				return true;
			}

			if (GetEffectiveCountAggregationMode(baseEffect) != GetEffectiveCountAggregationMode(upgradeEffect))
			{
				return true;
			}

			if (baseEffect.PowerTriggerEnemyStatus != upgradeEffect.PowerTriggerEnemyStatus
				|| !string.Equals(baseEffect.PowerTriggerPowerId ?? string.Empty, upgradeEffect.PowerTriggerPowerId ?? string.Empty, StringComparison.Ordinal)
				|| baseEffect.CountEnemyStatus != upgradeEffect.CountEnemyStatus
				|| !string.Equals(baseEffect.CountPowerId ?? string.Empty, upgradeEffect.CountPowerId ?? string.Empty, StringComparison.Ordinal))
			{
				return true;
			}

			if (baseEffect.UseMoveDestinationForGeneratedCards != upgradeEffect.UseMoveDestinationForGeneratedCards)
			{
				return true;
			}
			if (baseEffect.CreatedCardsCostResource != upgradeEffect.CreatedCardsCostResource)
			{
				return true;
			}
			if (baseEffect.AdditionalMoveToPiles != upgradeEffect.AdditionalMoveToPiles)
			{
				return true;
			}

			// Treat CustomKeywordName as "inherit from base" unless explicitly set on the upgrade effect.
			// This prevents upgrades from accidentally "un-fusing" keyword-grouped effects when the delta
			// only changes numbers.
			string? baseKeyword = string.IsNullOrWhiteSpace(baseEffect.CustomKeywordName) ? null : baseEffect.CustomKeywordName.Trim();
			string? upgradeKeyword = string.IsNullOrWhiteSpace(upgradeEffect.CustomKeywordName) ? null : upgradeEffect.CustomKeywordName.Trim();
			if (upgradeKeyword != null && !string.Equals(baseKeyword ?? string.Empty, upgradeKeyword, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			string? basePowerName = string.IsNullOrWhiteSpace(baseEffect.CustomPowerName) ? null : baseEffect.CustomPowerName.Trim();
			string? upgradePowerName = string.IsNullOrWhiteSpace(upgradeEffect.CustomPowerName) ? null : upgradeEffect.CustomPowerName.Trim();
			if (upgradePowerName != null && !string.Equals(basePowerName ?? string.Empty, upgradePowerName, StringComparison.Ordinal))
			{
				return true;
			}

			string? basePowerDescription = string.IsNullOrWhiteSpace(baseEffect.CustomPowerDescription) ? null : baseEffect.CustomPowerDescription.Trim();
			string? upgradePowerDescription = string.IsNullOrWhiteSpace(upgradeEffect.CustomPowerDescription) ? null : upgradeEffect.CustomPowerDescription.Trim();
			if (upgradePowerDescription != null && !string.Equals(basePowerDescription ?? string.Empty, upgradePowerDescription, StringComparison.Ordinal))
			{
				return true;
			}

			if (baseEffect.ConditionalBonusConditionType != upgradeEffect.ConditionalBonusConditionType
				|| baseEffect.ConditionalBonusCondition != upgradeEffect.ConditionalBonusCondition
				|| baseEffect.ConditionalBonusEnemyStatus != upgradeEffect.ConditionalBonusEnemyStatus
				|| !string.Equals(baseEffect.ConditionalBonusPowerId ?? string.Empty, upgradeEffect.ConditionalBonusPowerId ?? string.Empty, StringComparison.Ordinal)
				|| baseEffect.ConditionalBonusEnemyIntent != upgradeEffect.ConditionalBonusEnemyIntent)
			{
				return true;
			}

			if (baseEffect.Kind == CardExtraEffectKind.ChooseOneEffectSource && upgradeEffect.Kind == CardExtraEffectKind.ChooseOneEffectSource)
			{
				if (!ChooseOneOptionsEqual(NormalizeChooseOneOption(baseEffect.ChooseOneOption1, baseEffect.SpecificCardId), NormalizeChooseOneOption(upgradeEffect.ChooseOneOption1, upgradeEffect.SpecificCardId))
					|| !ChooseOneOptionsEqual(NormalizeChooseOneOption(baseEffect.ChooseOneOption2, baseEffect.SpecificCardId2), NormalizeChooseOneOption(upgradeEffect.ChooseOneOption2, upgradeEffect.SpecificCardId2))
					|| !ChooseOneOptionsEqual(NormalizeChooseOneOption(baseEffect.ChooseOneOption3, baseEffect.SpecificCardId3), NormalizeChooseOneOption(upgradeEffect.ChooseOneOption3, upgradeEffect.SpecificCardId3)))
				{
					return true;
				}
			}

			if (baseEffect.BranchMode != upgradeEffect.BranchMode
				|| baseEffect.BranchConditionType != upgradeEffect.BranchConditionType
				|| baseEffect.BranchCondition != upgradeEffect.BranchCondition
				|| baseEffect.BranchEnemyStatus != upgradeEffect.BranchEnemyStatus
				|| !string.Equals(baseEffect.BranchPowerId ?? string.Empty, upgradeEffect.BranchPowerId ?? string.Empty, StringComparison.Ordinal)
				|| baseEffect.BranchEnemyIntent != upgradeEffect.BranchEnemyIntent
				|| baseEffect.BranchCountEvent != upgradeEffect.BranchCountEvent
				|| baseEffect.BranchCountWindow != upgradeEffect.BranchCountWindow
				|| baseEffect.BranchCountWindowInclusion != upgradeEffect.BranchCountWindowInclusion
				|| baseEffect.BranchBlockLostCountingMode != upgradeEffect.BranchBlockLostCountingMode
				|| baseEffect.BranchCountTurns != upgradeEffect.BranchCountTurns
				|| baseEffect.BranchCountCardPile != upgradeEffect.BranchCountCardPile
				|| baseEffect.BranchCountCardPool != upgradeEffect.BranchCountCardPool
				|| baseEffect.BranchCountCardType != upgradeEffect.BranchCountCardType
				|| baseEffect.BranchCountCardFilter != upgradeEffect.BranchCountCardFilter
				|| GetEffectiveBranchCountAggregationMode(baseEffect) != GetEffectiveBranchCountAggregationMode(upgradeEffect)
				|| baseEffect.BranchCountExcludeSourceCard != upgradeEffect.BranchCountExcludeSourceCard
				|| baseEffect.BranchCountOrbType != upgradeEffect.BranchCountOrbType
				|| baseEffect.BranchCountOrbSelection != upgradeEffect.BranchCountOrbSelection
				|| baseEffect.BranchCountEnemyStatus != upgradeEffect.BranchCountEnemyStatus
				|| !string.Equals(baseEffect.BranchCountPowerId ?? string.Empty, upgradeEffect.BranchCountPowerId ?? string.Empty, StringComparison.Ordinal)
				|| baseEffect.BranchCountEnemyIntent != upgradeEffect.BranchCountEnemyIntent
				|| baseEffect.BranchCountComparison != upgradeEffect.BranchCountComparison
				|| baseEffect.BranchCountConditionAmount != upgradeEffect.BranchCountConditionAmount
				|| !BranchEffectsMatch(baseEffect.BranchEffect, upgradeEffect.BranchEffect))
			{
				return true;
			}

			if (baseEffect.Kind == CardExtraEffectKind.TransformCards && upgradeEffect.Kind == CardExtraEffectKind.TransformCards)
			{
				return baseEffect.TransformMode != upgradeEffect.TransformMode
					|| !string.Equals(baseEffect.SpecificCardId ?? string.Empty, upgradeEffect.SpecificCardId ?? string.Empty, StringComparison.Ordinal);
			}
		}

		return false;
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
		else if (CardEditorOverrides.TryGetEffectiveOverride(card, out CardOverride stored))
		{
			baseEffects = GetEffectiveExtraEffects(card, stored, considerUpgrade);
		}

		CombatState? combatState = card.CombatState;
		IReadOnlyList<CardExtraEffect> temporaryEffects = GetActiveGrantedExtraEffects(combatState, card);

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

	private static IReadOnlyList<CardExtraEffect> GetActiveGrantedExtraEffects(CombatState? combatState, CardModel card)
	{
		if (combatState == null || card == null)
		{
			return Array.Empty<CardExtraEffect>();
		}

		IReadOnlyList<CardExtraEffect> temporaryEffects = CardEditorTemporaryExtraEffectController.GetEffects(combatState, card);
		IReadOnlyList<CardExtraEffect> auraEffects = CardEditorMatchingCardAuraController.GetVirtualExtraEffects(combatState, card);
		if (temporaryEffects.Count == 0)
		{
			return auraEffects;
		}
		if (auraEffects.Count == 0)
		{
			return temporaryEffects;
		}

		List<CardExtraEffect> combined = new List<CardExtraEffect>(temporaryEffects.Count + auraEffects.Count);
		combined.AddRange(temporaryEffects);
		combined.AddRange(auraEffects);
		return combined;
	}

	internal static bool ShouldGlowGoldForConditionalEffects(CardModel card)
	{
		if (card == null)
		{
			return false;
		}

		CombatState? combatState = ResolveCardCombatState(card);
		Creature? ownerCreature = card.Owner?.Creature;
		if (combatState == null || ownerCreature == null)
		{
			return false;
		}

		CardPlay playForConditions = GetPreviewConditionCardPlay(card);
		foreach (CardExtraEffect effect in GetRuntimeEffectsIncludingBorrowedSources(combatState, card))
		{
			if (ShouldGlowGoldForConditionalEffect(card, combatState, ownerCreature, playForConditions, effect))
			{
				return true;
			}
		}

		return false;
	}

	private static CardPlay GetPreviewConditionCardPlay(CardModel card)
	{
		CardPlay? currentPlay = CardEditorCardPlayContext.Current;
		if (currentPlay != null && ReferenceEquals(currentPlay.Card, card))
		{
			return currentPlay;
		}

		return new CardPlay
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
	}

	private static bool ShouldGlowGoldForConditionalEffect(
		CardModel card,
		CombatState combatState,
		Creature ownerCreature,
		CardPlay playForConditions,
		CardExtraEffect effect)
	{
		if (card == null || effect == null)
		{
			return false;
		}

		if (effect.ScaleMode != CardExtraEffectScaleMode.None)
		{
			int count = GetHistoryCountMultiplier(combatState, ownerCreature, playForConditions, effect, card);
			if (DoesCountConditionPass(count, effect))
			{
				if (effect.ScaleMode == CardExtraEffectScaleMode.ConditionOnly)
				{
					return true;
				}

				if (effect.ScaleMode == CardExtraEffectScaleMode.PerHistoryCount)
				{
					if (effect.CountComparison != CardExtraEffectCountComparison.None)
					{
						return true;
					}

					if (count > 0)
					{
						return true;
					}
				}
			}
		}

		if (GetEffectiveConditionalBonusConditionType(effect) != CardExtraEffectBranchConditionType.None
			&& effect.ConditionalBonusAmount != 0
			&& DoesConditionalBonusPass(combatState, ownerCreature, playForConditions, effect))
		{
			return true;
		}

		return GetUsableBranchEffect(effect) != null
			&& DoesBranchConditionPass(combatState, ownerCreature, playForConditions, effect);
	}

	private static CombatState? ResolveCardCombatState(CardModel? card)
	{
		if (card?.CombatState != null)
		{
			return card.CombatState;
		}

		// Canonical/library cards are immutable and throw if we probe combat ownership while rendering compendium text.
		if (card == null || !card.IsMutable)
		{
			return null;
		}

		return card.Owner?.Creature?.CombatState;
	}

	internal static bool HasActiveRuleModifier(CardExtraEffectKind kind, CardModel? card, Creature? target = null)
	{
		if (card == null)
		{
			return false;
		}

		CombatState? combatState = ResolveCardCombatState(card);
		if (combatState != null)
		{
			Creature? ownerCreature = card.IsMutable ? card.Owner?.Creature : null;
			return CardEditorIgnoreEffectHelpers.HasActiveIgnoreEffect(kind, card, ownerCreature, combatState, target);
		}

		return GetEffectsForDescription(card, isUpgradePreview: false).Any(effect =>
			effect != null
			&& effect.Kind == kind
			&& !IsPowerEffect(effect)
			&& IsValidEffectAmount(effect.Kind, effect.Amount));
	}

	internal static bool TryGetRuleAdjustedTargetType(CardModel? card, TargetType currentTargetType, out TargetType adjustedTargetType)
	{
		adjustedTargetType = currentTargetType;
		if (card == null)
		{
			return false;
		}

		if (currentTargetType is not (TargetType.AnyEnemy or TargetType.RandomEnemy))
		{
			return false;
		}

		if (!HasActiveRuleModifier(CardExtraEffectKind.HitsAllEnemies, card))
		{
			return false;
		}

		adjustedTargetType = TargetType.AllEnemies;
		return true;
	}

	// Count/history-based glow conditions often change after another card resolves, so repaint the hand explicitly.
	private static void RefreshHandCardVisuals(Player? player)
	{
		CardPile? hand = player?.PlayerCombatState?.Hand;
		if (hand?.Cards == null)
		{
			return;
		}

		foreach (CardModel? handCard in hand.Cards)
		{
			if (handCard == null)
			{
				continue;
			}

			try
			{
				NCard? node = NCard.FindOnTable(handCard);
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

	private static CardOverride CreateSelfScalingSnapshot(CardModel card)
	{
		CardOverride snapshot = CardEditorOverrides.TryGetEffectiveOverride(card, out CardOverride currentOverride)
			? CardEditorOverrides.Clone(currentOverride)
			: new CardOverride();

		try
		{
			snapshot.DynamicVarBaseValues = card.DynamicVars.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.BaseValue, StringComparer.Ordinal);
		}
		catch
		{
			snapshot.DynamicVarBaseValues ??= new Dictionary<string, decimal>(StringComparer.Ordinal);
		}

		bool considerUpgrade = card.CurrentUpgradeLevel > 0;
		IReadOnlyList<CardExtraEffect> currentEffects = Array.Empty<CardExtraEffect>();
		if (CardEditorUiState.TryGetDraftOverride(card.Id, out CardOverride draftOverride))
		{
			currentEffects = GetEffectiveExtraEffects(card, draftOverride, considerUpgrade);
		}
		else if (CardEditorOverrides.TryGetEffectiveOverride(card, out CardOverride effectiveOverride))
		{
			currentEffects = GetEffectiveExtraEffects(card, effectiveOverride, considerUpgrade);
		}

		snapshot.ExtraEffects = currentEffects.Select(CloneEffect).ToList();
		snapshot.Upgrade = null;
		return snapshot;
	}

	private static string? TryResolveSelfScalingDynamicVarKey(CardModel card, string requestedKey)
	{
		if (card?.DynamicVars == null)
		{
			return null;
		}

		foreach (string key in card.DynamicVars.Keys)
		{
			if (string.Equals(key, requestedKey, StringComparison.OrdinalIgnoreCase))
			{
				return key;
			}
		}

		return null;
	}

	private static int GetSelfScalingDelta(CardExtraEffect effect)
	{
		if (effect == null || !IsValidEffectAmount(effect.Kind, effect.Amount))
		{
			return 0;
		}

		return effect.SelfScalingOperation == CardExtraEffectSelfScalingOperation.Decrease
			? -Math.Abs(effect.Amount)
			: Math.Abs(effect.Amount);
	}

	internal static CardModel? ResolveSelfScalingMutationTargetCard(CardModel? card)
	{
		if (card == null)
		{
			return null;
		}

		try
		{
			CardModel? cursor = card;
			for (int i = 0; i < 8 && cursor != null; i++)
			{
				if (!cursor.IsClone || cursor.CloneOf == null)
				{
					return cursor;
				}

				// Self-scaling often fires from a transient play clone. Walk back to the live mutable combat card
				// so the mutation sticks to the card that actually remains in the combat piles.
				if (!cursor.CloneOf.IsMutable)
				{
					return cursor;
				}

				cursor = cursor.CloneOf;
			}
		}
		catch
		{
			// ignored
		}

		return card;
	}

	private static bool TryApplySelfScalingMutationToSnapshot(CardModel card, CardOverride snapshot, CardExtraEffect effect, int delta)
	{
		if (card == null || snapshot == null || effect == null || delta == 0)
		{
			return false;
		}

		bool mutated = false;

		switch (effect.SelfScalingTargetType)
		{
			case CardExtraEffectSelfScalingTargetType.BaseDamage:
			case CardExtraEffectSelfScalingTargetType.BaseBlock:
			{
				string requestedKey = effect.SelfScalingTargetType == CardExtraEffectSelfScalingTargetType.BaseDamage ? "Damage" : "Block";
				string? resolvedKey = TryResolveSelfScalingDynamicVarKey(card, requestedKey);
				if (!string.IsNullOrWhiteSpace(resolvedKey))
				{
					snapshot.DynamicVarBaseValues ??= new Dictionary<string, decimal>(StringComparer.Ordinal);
					decimal currentValue = snapshot.DynamicVarBaseValues.TryGetValue(resolvedKey, out decimal existingValue)
						? existingValue
						: (card.DynamicVars.TryGetValue(resolvedKey, out var dynamicVar) ? dynamicVar.BaseValue : 0m);
					snapshot.DynamicVarBaseValues[resolvedKey] = currentValue + delta;
					mutated = true;
				}
				break;
			}
			case CardExtraEffectSelfScalingTargetType.EffectRowAmount:
			{
				if (!string.IsNullOrWhiteSpace(effect.SelfScalingTargetEffectId) && snapshot.ExtraEffects != null)
				{
					foreach (CardExtraEffect candidate in snapshot.ExtraEffects)
					{
						if (candidate == null
							|| string.IsNullOrWhiteSpace(candidate.EffectId)
							|| !string.Equals(candidate.EffectId, effect.SelfScalingTargetEffectId, StringComparison.Ordinal))
						{
							continue;
						}

						if (TryApplySelfScalingEffectRowMutation(candidate, effect.SelfScalingField, delta))
						{
							mutated = true;
						}
						return mutated;
					}
				}
				break;
			}
		}

		return mutated;
	}

	private static void RecalculateSelfScalingCard(CardModel card)
	{
		try
		{
			card.DynamicVars.RecalculateForUpgradeOrEnchant();
		}
		catch
		{
		}
	}

	private static bool ApplySelfScalingMutation(CardModel? card, CardExtraEffect effect)
	{
		if (card == null || effect == null || !card.IsMutable || !IsValidEffectAmount(effect.Kind, effect.Amount))
		{
			return false;
		}

		int delta = GetSelfScalingDelta(effect);
		if (delta == 0)
		{
			return false;
		}

		CardModel targetCard = ResolveSelfScalingMutationTargetCard(card) ?? card;
		if (!targetCard.IsMutable)
		{
			return false;
		}

		CardOverride snapshot = CreateSelfScalingSnapshot(targetCard);
		if (!TryApplySelfScalingMutationToSnapshot(targetCard, snapshot, effect, delta))
		{
			return false;
		}

		CardEditorOverrides.ApplyOverrideToCard(targetCard, snapshot);
		RecalculateSelfScalingCard(targetCard);
		return true;
	}

	private static bool ApplyPersistentSelfScalingMutation(CardModel? card, CardExtraEffect effect)
	{
		if (card == null || effect == null || !card.IsMutable || !IsValidEffectAmount(effect.Kind, effect.Amount))
		{
			return false;
		}

		CardModel persistentCard = card.DeckVersion is CardModel deckCard && deckCard.IsMutable
			? deckCard
			: card;
		if (persistentCard.Id == null || persistentCard.Id == ModelId.none)
		{
			return ApplySelfScalingMutation(card, effect);
		}

		int delta = GetSelfScalingDelta(effect);
		if (delta == 0)
		{
			return false;
		}

		CardOverride persistentSnapshot = CreateSelfScalingSnapshot(persistentCard);
		CardOverride? runtimeSnapshot = !ReferenceEquals(card, persistentCard)
			? CreateSelfScalingSnapshot(card)
			: null;
		if (!TryApplySelfScalingMutationToSnapshot(persistentCard, persistentSnapshot, effect, delta))
		{
			return false;
		}

		CardEditorOverrides.Set(persistentCard.Id, persistentSnapshot);
		CardEditorOverrides.ApplyOverrideToCard(persistentCard, persistentSnapshot);
		RecalculateSelfScalingCard(persistentCard);

		if (runtimeSnapshot != null && TryApplySelfScalingMutationToSnapshot(card, runtimeSnapshot, effect, delta))
		{
			CardEditorOverrides.ApplyOverrideToCard(card, runtimeSnapshot);
			RecalculateSelfScalingCard(card);
		}

		return true;
	}

	private static bool TryApplyTriggeredPermanentCardCostsLessToSnapshot(CardModel card, CardOverride snapshot, CardExtraEffect effect)
	{
		if (card == null || snapshot == null || effect == null)
		{
			return false;
		}

		CardExtraEffectCostModifier modifier = GetEffectiveCardCostsLessModifier(effect);
		switch (effect.Kind)
		{
			case CardExtraEffectKind.CardCostsLess:
			{
				if (snapshot.EnergyCostX == true || card.EnergyCost.CostsX)
				{
					return false;
				}

				int currentBaseCost;
				try
				{
					currentBaseCost = snapshot.EnergyCost ?? card.EnergyCost.GetWithModifiers(CostModifiers.None);
				}
				catch
				{
					return false;
				}

				if (!TryGetTriggeredPermanentCardCostsLessBaseCost(currentBaseCost, modifier, effect.Amount, out int mutatedBaseCost))
				{
					return false;
				}

				snapshot.EnergyCostX = false;
				snapshot.EnergyCost = mutatedBaseCost;
				return true;
			}

			case CardExtraEffectKind.CardStarCostsLess:
			{
				if (snapshot.StarCostX == true || card.HasStarCostX)
				{
					return false;
				}

				int currentBaseCost;
				try
				{
					currentBaseCost = snapshot.StarCost ?? card.BaseStarCost;
				}
				catch
				{
					return false;
				}

				if (!TryGetTriggeredPermanentCardCostsLessBaseCost(currentBaseCost, modifier, effect.Amount, out int mutatedBaseCost))
				{
					return false;
				}

				snapshot.StarCostX = false;
				snapshot.StarCost = mutatedBaseCost;
				return true;
			}

			default:
				return false;
		}
	}

	private static bool TryGetTriggeredPermanentCardCostsLessBaseCost(int currentBaseCost, CardExtraEffectCostModifier modifier, int amount, out int mutatedBaseCost)
	{
		mutatedBaseCost = currentBaseCost;
		if (currentBaseCost < 0)
		{
			return false;
		}

		long nextBaseCost = modifier switch
		{
			CardExtraEffectCostModifier.Reduce => (long)currentBaseCost - amount,
			CardExtraEffectCostModifier.Free => 0,
			CardExtraEffectCostModifier.FreeToPlay => 0,
			CardExtraEffectCostModifier.HalfCost => (long)Math.Floor(Math.Max(0, currentBaseCost) / 2m),
			_ => currentBaseCost
		};

		if (modifier is not (CardExtraEffectCostModifier.Reduce
			or CardExtraEffectCostModifier.Free
			or CardExtraEffectCostModifier.FreeToPlay
			or CardExtraEffectCostModifier.HalfCost))
		{
			return false;
		}

		if (nextBaseCost < 0)
		{
			nextBaseCost = 0;
		}
		else if (nextBaseCost > int.MaxValue)
		{
			nextBaseCost = int.MaxValue;
		}

		mutatedBaseCost = (int)nextBaseCost;
		return true;
	}

	private static void NotifyTriggeredPermanentCardCostsLessChanged(CardModel card, CardExtraEffectKind kind)
	{
		if (card == null)
		{
			return;
		}

		try
		{
			if (kind == CardExtraEffectKind.CardStarCostsLess)
			{
				CardEditorOverrides.NotifyStarCostChanged(card);
			}
			else
			{
				card.InvokeEnergyCostChanged();
			}
		}
		catch
		{
			// ignored
		}
	}

	private static bool ApplyPersistentTriggeredCardCostsLessMutation(CardModel? card, CardExtraEffect effect)
	{
		if (card == null || effect == null || !card.IsMutable)
		{
			return false;
		}

		CardModel runtimeTargetCard = ResolveSelfScalingMutationTargetCard(card) ?? card;
		if (!runtimeTargetCard.IsMutable)
		{
			return false;
		}

		CardModel persistentCard = runtimeTargetCard.DeckVersion is CardModel deckCard && deckCard.IsMutable
			? deckCard
			: runtimeTargetCard;
		if (persistentCard.Id == null || persistentCard.Id == ModelId.none)
		{
			CardOverride runtimeOnlySnapshot = CreateSelfScalingSnapshot(runtimeTargetCard);
			if (!TryApplyTriggeredPermanentCardCostsLessToSnapshot(runtimeTargetCard, runtimeOnlySnapshot, effect))
			{
				return false;
			}

			CardEditorOverrides.ApplyOverrideToCard(runtimeTargetCard, runtimeOnlySnapshot);
			NotifyTriggeredPermanentCardCostsLessChanged(runtimeTargetCard, effect.Kind);
			return true;
		}

		CardOverride persistentSnapshot = CreateSelfScalingSnapshot(persistentCard);
		CardOverride? runtimeSnapshot = !ReferenceEquals(runtimeTargetCard, persistentCard)
			? CreateSelfScalingSnapshot(runtimeTargetCard)
			: null;
		if (!TryApplyTriggeredPermanentCardCostsLessToSnapshot(persistentCard, persistentSnapshot, effect))
		{
			return false;
		}

		CardEditorOverrides.Set(persistentCard.Id, persistentSnapshot);
		CardEditorOverrides.ApplyOverrideToCard(persistentCard, persistentSnapshot);
		NotifyTriggeredPermanentCardCostsLessChanged(persistentCard, effect.Kind);

		if (runtimeSnapshot != null && TryApplyTriggeredPermanentCardCostsLessToSnapshot(runtimeTargetCard, runtimeSnapshot, effect))
		{
			CardEditorOverrides.ApplyOverrideToCard(runtimeTargetCard, runtimeSnapshot);
			NotifyTriggeredPermanentCardCostsLessChanged(runtimeTargetCard, effect.Kind);
		}

		return true;
	}

	private static IReadOnlyList<CardExtraEffect> GetRuntimeEffectsIncludingBorrowedSources(CombatState? combatState, CardModel card)
	{
		if (card == null)
		{
			return Array.Empty<CardExtraEffect>();
		}

		List<CardExtraEffect> effects = new List<CardExtraEffect>();
		HashSet<ModelId> effectSourceGuard = new HashSet<ModelId>();
		if (card.Id != null && card.Id != ModelId.none)
		{
			effectSourceGuard.Add(card.Id);
		}

		AppendRuntimeEffectsIncludingBorrowedSources(effects, combatState, card, includeTemporaryEffects: true, effectSourceGuard);
		return effects;
	}

	internal static bool CardHasRuntimeEffectKind(CardModel? card, CardExtraEffectKind kind)
	{
		if (card == null)
		{
			return false;
		}

		return GetRuntimeEffectsIncludingBorrowedSources(card.CombatState, card).Any(effect =>
			effect != null
			&& effect.Kind == kind
			&& IsValidEffectAmount(effect.Kind, effect.Amount));
	}

	internal static bool CardHasRuntimeResourceConsumptionMode(CardModel? card, CardExtraEffectResourceConsumptionMode mode)
	{
		if (card == null)
		{
			return false;
		}

		return GetRuntimeEffectsIncludingBorrowedSources(card.CombatState, card).Any(effect =>
			effect != null
			&& effect.Kind == CardExtraEffectKind.DoesNotConsumeVigor
			&& effect.ResourceConsumptionMode == mode
			&& IsValidEffectAmount(effect.Kind, effect.Amount));
	}

internal static bool CardHasRuntimeProtectedMultiplierStat(CardModel? card, CardExtraEffectMultiplierStat stat)
{
	if (card == null)
	{
		return false;
		}

		return GetRuntimeEffectsIncludingBorrowedSources(card.CombatState, card).Any(effect =>
			effect != null
			&& effect.Kind == CardExtraEffectKind.DoesNotConsumeVigor
			&& IsValidEffectAmount(effect.Kind, effect.Amount)
			&& ((effect.ResourceConsumptionMode == CardExtraEffectResourceConsumptionMode.Vigor
					&& stat == CardExtraEffectMultiplierStat.Vigor)
				|| (effect.ResourceConsumptionMode == CardExtraEffectResourceConsumptionMode.SpecificStatStatus
					&& effect.ResourceConsumptionStat == stat)));
}

internal static bool CardHasRuntimeProtectedConfiguredPower(CardModel? card, ModelId powerId)
{
	if (card == null || powerId == ModelId.none)
	{
		return false;
	}

	return GetRuntimeEffectsIncludingBorrowedSources(card.CombatState, card).Any(effect =>
	{
		if (effect == null
			|| effect.Kind != CardExtraEffectKind.DoesNotConsumeVigor
			|| !IsValidEffectAmount(effect.Kind, effect.Amount)
			|| effect.ResourceConsumptionMode != CardExtraEffectResourceConsumptionMode.SpecificPowerStatus
			|| string.IsNullOrWhiteSpace(effect.PowerId))
		{
			return false;
		}

		try
		{
			return ModelId.Deserialize(effect.PowerId.Trim()) == powerId;
		}
		catch
		{
			return false;
		}
	});
}

	private static bool TryGetProtectedMultiplierStat(Type? powerType, out CardExtraEffectMultiplierStat stat)
	{
		stat = CardExtraEffectMultiplierStat.Vigor;
		if (powerType == null)
		{
			return false;
		}

		if (powerType == typeof(StrengthPower)) { stat = CardExtraEffectMultiplierStat.Strength; return true; }
		if (powerType == typeof(DexterityPower)) { stat = CardExtraEffectMultiplierStat.Dexterity; return true; }
		if (powerType == typeof(FocusPower)) { stat = CardExtraEffectMultiplierStat.Focus; return true; }
		if (powerType == typeof(WeakPower)) { stat = CardExtraEffectMultiplierStat.Weak; return true; }
		if (powerType == typeof(FrailPower)) { stat = CardExtraEffectMultiplierStat.Frail; return true; }
		if (powerType == typeof(VulnerablePower)) { stat = CardExtraEffectMultiplierStat.Vulnerable; return true; }
		if (powerType == typeof(PoisonPower)) { stat = CardExtraEffectMultiplierStat.Poison; return true; }
		if (powerType == typeof(DoomPower)) { stat = CardExtraEffectMultiplierStat.Doom; return true; }
		if (powerType == typeof(ConstrictPower)) { stat = CardExtraEffectMultiplierStat.Constrict; return true; }
		if (powerType == typeof(ArtifactPower)) { stat = CardExtraEffectMultiplierStat.Artifact; return true; }
		if (powerType == typeof(ThornsPower)) { stat = CardExtraEffectMultiplierStat.Thorns; return true; }
		if (powerType == typeof(RegenPower)) { stat = CardExtraEffectMultiplierStat.Regen; return true; }
		if (powerType == typeof(PlatingPower)) { stat = CardExtraEffectMultiplierStat.Plating; return true; }
		if (powerType == typeof(IntangiblePower)) { stat = CardExtraEffectMultiplierStat.Intangible; return true; }
		if (powerType == typeof(BufferPower)) { stat = CardExtraEffectMultiplierStat.Buffer; return true; }
		if (powerType == typeof(VigorPower)) { stat = CardExtraEffectMultiplierStat.Vigor; return true; }
		if (powerType == typeof(BlurPower)) { stat = CardExtraEffectMultiplierStat.Blur; return true; }
		if (powerType == typeof(RitualPower)) { stat = CardExtraEffectMultiplierStat.Ritual; return true; }

		return false;
	}

private static bool ShouldPreserveSelfProtectedStat(CardPlay? cardPlay, Creature? ownerCreature, CardExtraEffectTarget target, CardExtraEffectMultiplierStat stat)
{
	return cardPlay?.Card != null
		&& ownerCreature != null
		&& target == CardExtraEffectTarget.Self
		&& CardHasRuntimeProtectedMultiplierStat(cardPlay.Card, stat);
}

private static bool ShouldPreserveSelfProtectedConfiguredPower(CardPlay? cardPlay, Creature? ownerCreature, CardExtraEffectTarget target, ModelId powerId)
{
	return cardPlay?.Card != null
		&& ownerCreature != null
		&& target == CardExtraEffectTarget.Self
		&& CardHasRuntimeProtectedConfiguredPower(cardPlay.Card, powerId);
}

private static bool ShouldPreserveSelfProtectedPower(CardPlay? cardPlay, Creature? ownerCreature, CardExtraEffectTarget target, PowerModel? powerModel)
{
	if (powerModel == null)
	{
		return false;
	}

	if (ShouldPreserveSelfProtectedConfiguredPower(cardPlay, ownerCreature, target, powerModel.Id))
	{
		return true;
	}

	return TryGetProtectedMultiplierStat(powerModel.GetType(), out CardExtraEffectMultiplierStat stat)
		&& ShouldPreserveSelfProtectedStat(cardPlay, ownerCreature, target, stat);
}

	private static void AppendRuntimeEffectsIncludingBorrowedSources(
		List<CardExtraEffect> destination,
		CombatState? combatState,
		CardModel templateCard,
		bool includeTemporaryEffects,
		HashSet<ModelId> effectSourceGuard,
		string? keywordFilter = null)
	{
		if (destination == null || templateCard == null)
		{
			return;
		}

		IReadOnlyList<CardExtraEffect> overrideEffects = Array.Empty<CardExtraEffect>();
		if (CardEditorOverrides.TryGetEffectiveOverride(templateCard, out CardOverride overrideData))
		{
			overrideEffects = GetEffectiveExtraEffects(templateCard, overrideData, templateCard.CurrentUpgradeLevel > 0);
		}

		bool hasInlineEffectSources = false;
		foreach (CardExtraEffect effect in overrideEffects)
		{
			if (effect == null)
			{
				continue;
			}
			if (!MatchesCustomKeywordFilter(effect, keywordFilter))
			{
				continue;
			}

			destination.Add(effect);
			if (effect.Kind == CardExtraEffectKind.RunEffectSourceCard)
			{
				// Keep runtime execution single-sourced through ExecuteEffect -> RunSingleEffectSourceOnPlay.
				// If we eagerly splice the source card's runtime effects into the host list here, the host card
				// executes the borrowed source once inline and then a second time through the effect-source runner.
				hasInlineEffectSources = true;
			}
		}

		if (templateCard is CardEditorCreatedCardBase && !hasInlineEffectSources)
		{
			List<CardExtraEffect> borrowedEffects = BuildLegacyBorrowedRuntimeEffects(combatState, templateCard, effectSourceGuard, keywordFilter);
			if (borrowedEffects.Count > 0)
			{
				if (CardEditorCreatedCardsStore.GetEffectSourcePlacement(templateCard.Id) == CardEditorEffectSourcePlacement.BeforeCustomEffects)
				{
					destination.InsertRange(0, borrowedEffects);
				}
				else
				{
					destination.AddRange(borrowedEffects);
				}
			}
		}

		if (!includeTemporaryEffects || combatState == null)
		{
			return;
		}

		IReadOnlyList<CardExtraEffect> temporaryEffects = GetActiveGrantedExtraEffects(combatState, templateCard);
		if (temporaryEffects.Count > 0)
		{
			foreach (CardExtraEffect effect in temporaryEffects)
			{
				if (effect == null)
				{
					continue;
				}
				if (!MatchesCustomKeywordFilter(effect, keywordFilter))
				{
					continue;
				}

				destination.Add(effect);
				if (effect.Kind == CardExtraEffectKind.RunEffectSourceCard)
				{
					AppendBorrowedInspectionEffectsFromSourceId(
						destination,
						combatState,
						templateCard,
						effect.SpecificCardId,
						effectSourceGuard,
						effect.CustomKeywordName);
				}
			}
		}
	}

	private static List<CardExtraEffect> BuildLegacyBorrowedRuntimeEffects(CombatState? combatState, CardModel templateCard, HashSet<ModelId> effectSourceGuard, string? keywordFilter = null)
	{
		List<CardExtraEffect> borrowedEffects = new List<CardExtraEffect>();
		foreach (ModelId sourceId in CardEditorCreatedCardEffectSourceSupport.GetRuntimeEffectSourceIds(templateCard, isUpgradePreview: false))
		{
			AppendBorrowedRuntimeEffectsFromSourceId(borrowedEffects, combatState, templateCard, sourceId, effectSourceGuard, keywordFilter);
		}

		return borrowedEffects;
	}

	internal static IReadOnlyList<CardExtraEffect> GetRuntimeEffectsForBorrowedSource(CombatState? combatState, CardModel hostCard, ModelId effectSourceId, string? keywordFilter = null)
	{
		List<CardExtraEffect> borrowedEffects = new List<CardExtraEffect>();
		AppendBorrowedRuntimeEffectsFromSourceId(
			borrowedEffects,
			combatState,
			hostCard,
			effectSourceId,
			new HashSet<ModelId>(),
			keywordFilter);
		return borrowedEffects;
	}

	internal static IReadOnlyList<CardExtraEffect> GetRuntimeEffectsForExecution(CombatState? combatState, CardModel card)
	{
		if (card == null)
		{
			return Array.Empty<CardExtraEffect>();
		}

		if (combatState != null)
		{
			return GetRuntimeEffectsForModifierInspection(combatState, card);
		}

		return GetEffectsForDescription(card, isUpgradePreview: false);
	}

	private static IReadOnlyList<CardExtraEffect> GetRuntimeEffectsForModifierInspection(CombatState combatState, CardModel card)
	{
		if (card == null)
		{
			return Array.Empty<CardExtraEffect>();
		}

		List<CardExtraEffect> effects = new List<CardExtraEffect>();
		HashSet<ModelId> effectSourceGuard = new HashSet<ModelId>();
		if (card.Id != null && card.Id != ModelId.none)
		{
			effectSourceGuard.Add(card.Id);
		}

		AppendRuntimeEffectsForModifierInspection(effects, combatState, card, effectSourceGuard);
		return effects;
	}

	private static void AppendRuntimeEffectsForModifierInspection(
		List<CardExtraEffect> destination,
		CombatState combatState,
		CardModel templateCard,
		HashSet<ModelId> effectSourceGuard,
		string? keywordFilter = null)
	{
		if (destination == null || templateCard == null)
		{
			return;
		}

		IReadOnlyList<CardExtraEffect> overrideEffects = Array.Empty<CardExtraEffect>();
		if (CardEditorOverrides.TryGetEffectiveOverride(templateCard, out CardOverride overrideData))
		{
			overrideEffects = GetEffectiveExtraEffects(templateCard, overrideData, templateCard.CurrentUpgradeLevel > 0);
		}

		bool hasInlineEffectSources = false;
		foreach (CardExtraEffect effect in overrideEffects)
		{
			if (effect == null)
			{
				continue;
			}
			if (!MatchesCustomKeywordFilter(effect, keywordFilter))
			{
				continue;
			}

			destination.Add(effect);
			if (effect.Kind == CardExtraEffectKind.RunEffectSourceCard)
			{
				hasInlineEffectSources = true;
				AppendBorrowedInspectionEffectsFromSourceId(destination, combatState, templateCard, effect.SpecificCardId, effectSourceGuard, effect.CustomKeywordName);
			}
		}

		if (templateCard is CardEditorCreatedCardBase && !hasInlineEffectSources)
		{
			List<CardExtraEffect> borrowedEffects = new List<CardExtraEffect>();
			foreach (ModelId sourceId in CardEditorCreatedCardEffectSourceSupport.GetRuntimeEffectSourceIds(templateCard, isUpgradePreview: false))
			{
				AppendBorrowedInspectionEffectsFromSourceId(borrowedEffects, combatState, templateCard, sourceId, effectSourceGuard, keywordFilter);
			}

			if (borrowedEffects.Count > 0)
			{
				if (CardEditorCreatedCardsStore.GetEffectSourcePlacement(templateCard.Id) == CardEditorEffectSourcePlacement.BeforeCustomEffects)
				{
					destination.InsertRange(0, borrowedEffects);
				}
				else
				{
					destination.AddRange(borrowedEffects);
				}
			}
		}

		IReadOnlyList<CardExtraEffect> temporaryEffects = GetActiveGrantedExtraEffects(combatState, templateCard);
		if (temporaryEffects.Count > 0)
		{
			destination.AddRange(temporaryEffects);
		}
	}

	private static void AppendBorrowedInspectionEffectsFromSourceId(
		List<CardExtraEffect> destination,
		CombatState? combatState,
		CardModel hostCard,
		string? effectSourceIdText,
		HashSet<ModelId> effectSourceGuard,
		string? keywordFilter)
	{
		if (!TryParseEffectSourceModelId(effectSourceIdText, out ModelId effectSourceId))
		{
			return;
		}

		AppendBorrowedInspectionEffectsFromSourceId(destination, combatState, hostCard, effectSourceId, effectSourceGuard, keywordFilter);
	}

	private static void AppendBorrowedInspectionEffectsFromSourceId(
		List<CardExtraEffect> destination,
		CombatState? combatState,
		CardModel hostCard,
		ModelId effectSourceId,
		HashSet<ModelId> effectSourceGuard,
		string? keywordFilter)
	{
		if (destination == null || hostCard == null || combatState == null || effectSourceId == null || effectSourceId == ModelId.none)
		{
			return;
		}

		if (!effectSourceGuard.Add(effectSourceId))
		{
			return;
		}

		try
		{
			CardModel? sourceCard = CardEditorCreatedCardEffectSourceSupport.BuildRuntimeEffectSourceCard(hostCard, effectSourceId, isUpgradePreview: false);
			if (sourceCard == null)
			{
				return;
			}

			AppendRuntimeEffectsForModifierInspection(destination, combatState, sourceCard, effectSourceGuard, keywordFilter);
		}
		finally
		{
			effectSourceGuard.Remove(effectSourceId);
		}
	}

	private static void AppendBorrowedRuntimeEffectsFromSourceId(
		List<CardExtraEffect> destination,
		CombatState? combatState,
		CardModel hostCard,
		string? effectSourceIdText,
		HashSet<ModelId> effectSourceGuard,
		string? keywordFilter)
	{
		if (!TryParseEffectSourceModelId(effectSourceIdText, out ModelId effectSourceId))
		{
			return;
		}

		AppendBorrowedRuntimeEffectsFromSourceId(destination, combatState, hostCard, effectSourceId, effectSourceGuard, keywordFilter);
	}

	private static void AppendBorrowedRuntimeEffectsFromSourceId(
		List<CardExtraEffect> destination,
		CombatState? combatState,
		CardModel hostCard,
		ModelId effectSourceId,
		HashSet<ModelId> effectSourceGuard,
		string? keywordFilter)
	{
		if (destination == null || hostCard == null || effectSourceId == null || effectSourceId == ModelId.none)
		{
			return;
		}

		if (!effectSourceGuard.Add(effectSourceId))
		{
			return;
		}

		try
		{
			CardModel? sourceCard = CardEditorCreatedCardEffectSourceSupport.BuildRuntimeEffectSourceCard(hostCard, effectSourceId, isUpgradePreview: false);
			if (sourceCard == null)
			{
				return;
			}

			AppendRuntimeEffectsIncludingBorrowedSources(destination, combatState, sourceCard, includeTemporaryEffects: false, effectSourceGuard, keywordFilter);
		}
		finally
		{
			effectSourceGuard.Remove(effectSourceId);
		}
	}

	private static bool TryParseEffectSourceModelId(string? text, out ModelId id)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				id = ModelId.none;
				return false;
			}

			id = ModelId.Deserialize(text.Trim());
			return id != null && id != ModelId.none;
		}
		catch
		{
			id = ModelId.none;
			return false;
		}
	}

	private static bool MatchesCustomKeywordFilter(CardExtraEffect? effect, string? keywordFilter)
	{
		string? normalizedFilter = NormalizeCustomKeywordName(keywordFilter);
		if (normalizedFilter == null)
		{
			return true;
		}

		return string.Equals(NormalizeCustomKeywordName(effect?.CustomKeywordName), normalizedFilter, StringComparison.OrdinalIgnoreCase);
	}

	private static string? NormalizeCustomKeywordName(string? value)
	{
		string trimmed = value?.Trim() ?? string.Empty;
		return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
	}

	private static List<DescriptionEffectLine> BuildDescriptionEffectLines(CardModel card, Creature? target, bool isUpgradePreview)
	{
		if (card == null)
		{
			return new List<DescriptionEffectLine>();
		}

		bool considerUpgrade = isUpgradePreview || card.CurrentUpgradeLevel > 0;

		bool hasOverride = TryGetOverrideForDescription(card, considerUpgrade, out CardOverride? overrideData, out IReadOnlyList<CardExtraEffect> effectiveEffects);
		CombatState? combatState = card.CombatState;
		IReadOnlyList<CardExtraEffect> temporaryEffects = GetActiveGrantedExtraEffects(combatState, card);

		List<(CardExtraEffect Effect, int UpgradeHighlightComparison, bool IsTemporary)> toRender = new List<(CardExtraEffect, int, bool)>();
		if (hasOverride)
		{
			bool wantsUpgradeDiffHighlight = isUpgradePreview;
			List<(CardExtraEffect Effect, int UpgradeHighlightComparison)> baseList = wantsUpgradeDiffHighlight
				? BuildUpgradePreviewEffects(overrideData!, effectiveEffects)
				: effectiveEffects.Select(e => (e, 0)).ToList();
			if (baseList.Count > 0)
			{
				toRender.AddRange(baseList.Select(t => (t.Effect, t.UpgradeHighlightComparison, false)));
			}
		}
		if (temporaryEffects.Count > 0)
		{
			toRender.AddRange(temporaryEffects.Select(e => (e, 0, true)));
		}

		List<DescriptionEffectLine> lines = new List<DescriptionEffectLine>();
		foreach ((CardExtraEffect effect, int upgradeHighlightComparison, bool isTemporary) in toRender)
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
				bool hasMatchingBase = toRender.Any(e =>
					!e.IsTemporary
					&& e.Effect != null
					&& IsEquivalentTimedCardCostsLess(e.Effect, effect));
				if (hasMatchingBase)
				{
					continue;
				}
			}

			string? line = TryFormatLine(card, effect, target, upgradeHighlightComparison, isUpgradePreview: isUpgradePreview);
			if (string.IsNullOrWhiteSpace(line))
			{
				continue;
			}

			lines.Add(new DescriptionEffectLine
			{
				Line = line!,
				CustomKeywordName = NormalizeCustomKeywordName(effect.CustomKeywordName),
				Trigger = effect.Trigger
			});
		}

		return MergeDescriptionEffectLines(lines);
	}

	internal static string? FormatSingleEffectLine(CardModel card, CardExtraEffect effect, Creature? target = null, bool isUpgradePreview = false)
	{
		if (card == null || effect == null || !IsValidEffectAmount(effect.Kind, effect.Amount))
		{
			return null;
		}

		string? line = TryFormatLine(card, effect, target, 0, isUpgradePreview);
		if (string.IsNullOrWhiteSpace(line))
		{
			return null;
		}

		return CardEditorVanillaKeywordSupport.FormatDescription(card, line!, target, isUpgradePreview);
	}

	private enum DescriptionLineMergeMode
	{
		None = 0,
		SharedPrefix = 1,
		SharedSuffix = 2
	}

	private static List<DescriptionEffectLine> MergeDescriptionEffectLines(List<DescriptionEffectLine> lines)
	{
		if (lines == null || lines.Count <= 1)
		{
			return lines ?? new List<DescriptionEffectLine>();
		}

		List<DescriptionEffectLine> merged = new List<DescriptionEffectLine>(lines.Count);
		for (int index = 0; index < lines.Count; index++)
		{
			DescriptionEffectLine current = lines[index];
			if (!TryGetMergeParts(current.Line, out DescriptionLineMergeMode mode, out string sharedText, out string payload))
			{
				merged.Add(current);
				continue;
			}

			List<string> payloads = new List<string> { payload };
			int lastMergedIndex = index;
			for (int nextIndex = index + 1; nextIndex < lines.Count; nextIndex++)
			{
				DescriptionEffectLine next = lines[nextIndex];
				if (!string.IsNullOrWhiteSpace(next.CustomKeywordName)
					|| !string.IsNullOrWhiteSpace(current.CustomKeywordName)
					|| next.Trigger != current.Trigger
					|| !TryGetMergeParts(next.Line, out DescriptionLineMergeMode nextMode, out string nextSharedText, out string nextPayload)
					|| nextMode != mode
					|| !string.Equals(nextSharedText, sharedText, StringComparison.Ordinal))
				{
					break;
				}

				payloads.Add(nextPayload);
				lastMergedIndex = nextIndex;
			}

			if (payloads.Count == 1)
			{
				merged.Add(current);
				continue;
			}

			string joinedPayload = JoinDescriptionPayloads(payloads);
			string mergedLine = mode == DescriptionLineMergeMode.SharedPrefix
				? sharedText + joinedPayload + "."
				: joinedPayload + sharedText;

			merged.Add(new DescriptionEffectLine
			{
				Line = mergedLine,
				CustomKeywordName = current.CustomKeywordName,
				Trigger = current.Trigger
			});

			index = lastMergedIndex;
		}

		return merged;
	}

	private static bool TryGetMergeParts(string line, out DescriptionLineMergeMode mode, out string sharedText, out string payload)
	{
		mode = DescriptionLineMergeMode.None;
		sharedText = string.Empty;
		payload = string.Empty;

		if (string.IsNullOrWhiteSpace(line) || line.Contains('\n'))
		{
			return false;
		}

		string trimmed = line.Trim();
		foreach (string marker in GetMergeSuffixMarkers())
		{
			int suffixIndex = trimmed.IndexOf(marker, StringComparison.Ordinal);
			if (suffixIndex > 0)
			{
				mode = DescriptionLineMergeMode.SharedSuffix;
				sharedText = trimmed[suffixIndex..];
				payload = TrimTrailingPeriod(trimmed[..suffixIndex]);
				return !string.IsNullOrWhiteSpace(payload);
			}
		}

		foreach (string startOfCombatPrefix in GetCombatStartPrefixes())
		{
			if (trimmed.StartsWith(startOfCombatPrefix, StringComparison.Ordinal))
			{
				mode = DescriptionLineMergeMode.SharedPrefix;
				sharedText = startOfCombatPrefix;
				payload = TrimTrailingPeriod(trimmed[sharedText.Length..]);
				return !string.IsNullOrWhiteSpace(payload);
			}
		}

		if (StartsWithAny(trimmed, GetPrefixMergeMarkers()))
		{
			foreach (string separator in GetPrefixMergeSeparators())
			{
				int separatorIndex = trimmed.IndexOf(separator, StringComparison.Ordinal);
				if (separatorIndex > 0)
				{
					mode = DescriptionLineMergeMode.SharedPrefix;
					sharedText = trimmed[..(separatorIndex + separator.Length)];
					payload = TrimTrailingPeriod(trimmed[(separatorIndex + separator.Length)..]);
					return !string.IsNullOrWhiteSpace(payload);
				}
			}
		}

		return false;
	}

	private static string JoinDescriptionPayloads(IReadOnlyList<string> payloads)
	{
		if (payloads == null || payloads.Count == 0)
		{
			return string.Empty;
		}
		if (payloads.Count == 1)
		{
			return payloads[0];
		}
		if (payloads.Count == 2)
		{
			if (IsChineseLocalizationActive())
			{
				return payloads[0] + "和" + payloads[1];
			}
			return payloads[0] + " and " + LowercaseFirst(payloads[1]);
		}

		if (IsChineseLocalizationActive())
		{
			return string.Join("、", payloads.Take(payloads.Count - 1))
				+ "和"
				+ payloads[^1];
		}

		return string.Join(", ", payloads.Take(payloads.Count - 1))
			+ ", and "
			+ LowercaseFirst(payloads[^1]);
	}

	private static string TrimTrailingPeriod(string text)
	{
		string trimmed = text?.Trim() ?? string.Empty;
		if (trimmed.EndsWith(".", StringComparison.Ordinal) || trimmed.EndsWith("。", StringComparison.Ordinal))
		{
			return trimmed[..^1];
		}

		return trimmed;
	}

	private static bool IsChineseLocalizationActive()
	{
		string language = LocManager.Instance?.Language ?? string.Empty;
		return language.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
	}

	private static bool StartsWithAny(string text, IReadOnlyList<string> prefixes)
	{
		foreach (string prefix in prefixes)
		{
			if (text.StartsWith(prefix, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	private static IReadOnlyList<string> GetMergeSuffixMarkers()
	{
		return IsChineseLocalizationActive()
			? new[] { "。你在", "（你在", "，你在" }
			: new[] { " for each " };
	}

	private static IReadOnlyList<string> GetCombatStartPrefixes()
	{
		return IsChineseLocalizationActive()
			? new[] { "[gold]战斗开始时[/gold]：" }
			: new[] { "[gold]Start Of Combat[/gold]: " };
	}

	private static IReadOnlyList<string> GetPrefixMergeMarkers()
	{
		return IsChineseLocalizationActive()
			? new[] { "如果", "每当", "在", "每", "本场战斗中", "从下个", "从你的下个", "战斗开始时" }
			: new[] { "If ", "Whenever ", "At the ", "After ", "Every " };
	}

	private static IReadOnlyList<string> GetPrefixMergeSeparators()
	{
		return IsChineseLocalizationActive()
			? new[] { "，", "：" }
			: new[] { ", ", ": " };
	}

	internal static IReadOnlyList<CardExtraEffectKeywordSummary> GetCustomKeywordSummaries(CardModel card, Creature? target = null, bool isUpgradePreview = false)
	{
		List<DescriptionEffectLine> lines = BuildDescriptionEffectLines(card, target, isUpgradePreview);
		if (lines.Count == 0)
		{
			return Array.Empty<CardExtraEffectKeywordSummary>();
		}

		List<string> orderedNames = new List<string>();
		Dictionary<string, List<string>> groupedLines = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
		foreach (DescriptionEffectLine line in lines)
		{
			string? keywordName = line.CustomKeywordName;
			if (string.IsNullOrWhiteSpace(keywordName))
			{
				continue;
			}

			if (!groupedLines.TryGetValue(keywordName, out List<string>? group))
			{
				group = new List<string>();
				groupedLines[keywordName] = group;
				orderedNames.Add(keywordName);
			}

			group.Add(line.Line);
		}

		if (orderedNames.Count == 0)
		{
			return Array.Empty<CardExtraEffectKeywordSummary>();
		}

		List<CardExtraEffectKeywordSummary> summaries = new List<CardExtraEffectKeywordSummary>(orderedNames.Count);
		foreach (string name in orderedNames)
		{
			if (!groupedLines.TryGetValue(name, out List<string>? group) || group.Count == 0)
			{
				continue;
			}

			summaries.Add(new CardExtraEffectKeywordSummary
			{
				Name = name,
				Description = string.Join('\n', group)
			});
		}

		if (lines.Any(line => line.Trigger == CardExtraEffectTrigger.DeckPassiveCombatStart))
		{
			summaries.Add(new CardExtraEffectKeywordSummary
			{
				Name = GetDeckPassiveCombatStartKeywordTitle(),
				Description = GetDeckPassiveCombatStartKeywordDescription()
			});
		}

		if (lines.Any(line => line.Trigger == CardExtraEffectTrigger.DeckPassiveCombatEnd))
		{
			summaries.Add(new CardExtraEffectKeywordSummary
			{
				Name = GetDeckPassiveCombatEndKeywordTitle(),
				Description = GetDeckPassiveCombatEndKeywordDescription()
			});
		}

		if (lines.Any(line => line.Line.Contains("[gold]" + GetPlayableKeywordTitle() + "[/gold]", StringComparison.Ordinal)))
		{
			summaries.Add(new CardExtraEffectKeywordSummary
			{
				Name = GetPlayableKeywordTitle(),
				Description = GetPlayableKeywordDescription()
			});
		}

		if (lines.Any(line => line.Line.Contains("[gold]" + GetAllCardsKeywordTitle() + "[/gold]", StringComparison.Ordinal)))
		{
			summaries.Add(new CardExtraEffectKeywordSummary
			{
				Name = GetAllCardsKeywordTitle(),
				Description = GetAllCardsKeywordDescription()
			});
		}

		return summaries;
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
		List<DescriptionEffectLine> renderedLines = BuildDescriptionEffectLines(card, target, isUpgradePreview);
		if (renderedLines.Count == 0)
		{
			return false;
		}

		List<string> lines = new List<string>();
		HashSet<string> emittedKeywordGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (DescriptionEffectLine renderedLine in renderedLines)
		{
			if (!string.IsNullOrWhiteSpace(renderedLine.CustomKeywordName))
			{
				if (emittedKeywordGroups.Add(renderedLine.CustomKeywordName))
				{
					lines.Add("[gold]" + renderedLine.CustomKeywordName + "[/gold].");
				}
				continue;
			}

			lines.Add(renderedLine.Line);
		}
		if (lines.Count == 0)
		{
			return false;
		}

		string appendBlock = string.Join('\n', lines);
		string formattedAppendBlock = CardEditorVanillaKeywordSupport.FormatDescription(appendBlock);
		if (string.IsNullOrEmpty(description))
		{
			description = appendBlock;
			return true;
		}

		// Some mod combinations re-enter public description APIs after the private path has
		// already appended our extra-effect block. Only suppress an exact duplicate block at
		// the end of the description so intentional repeated effect lines remain untouched.
		string comparisonDescription = description.TrimEnd('\r', '\n');
		bool alreadyEndsWithRawBlock = comparisonDescription.Equals(appendBlock, StringComparison.Ordinal)
			|| comparisonDescription.EndsWith("\n" + appendBlock, StringComparison.Ordinal);
		bool alreadyEndsWithFormattedBlock = !string.Equals(formattedAppendBlock, appendBlock, StringComparison.Ordinal)
			&& (comparisonDescription.Equals(formattedAppendBlock, StringComparison.Ordinal)
				|| comparisonDescription.EndsWith("\n" + formattedAppendBlock, StringComparison.Ordinal));
		if (alreadyEndsWithRawBlock || alreadyEndsWithFormattedBlock)
		{
			return false;
		}

		description += "\n" + appendBlock;
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

		if (CardEditorOverrides.TryGetEffectiveOverride(card, out CardOverride stored))
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

	private static List<(CardExtraEffect Effect, int UpgradeHighlightComparison)> BuildUpgradePreviewEffects(CardOverride overrideData, IReadOnlyList<CardExtraEffect> effectiveEffects)
	{
		if (overrideData == null || effectiveEffects == null || effectiveEffects.Count == 0)
		{
			return new List<(CardExtraEffect, int)>();
		}

		IReadOnlyList<CardExtraEffect>? baseEffects = overrideData.ExtraEffects;
		IReadOnlyList<CardExtraEffect>? upgradeEffects = overrideData.Upgrade?.ExtraEffects;

		if (upgradeEffects == null || upgradeEffects.Count == 0)
		{
			return effectiveEffects.Select(e => (e, 0)).ToList();
		}

		if (baseEffects == null || baseEffects.Count == 0)
		{
			return effectiveEffects.Select(e => (e, GetUpgradeHighlightComparison(baseEffect: null, upgradedEffect: e))).ToList();
		}

		List<(CardExtraEffect Effect, int UpgradeHighlightComparison)> result = baseEffects.Select(e => (CloneEffect(e), 0)).ToList();
		bool secondaryNumericFieldsAreDeltas = overrideData.Upgrade?.ExtraEffectNumericFieldsAreDeltas ?? false;
		UpgradeEffectAlignment alignment = AlignUpgradeEffectsToBaseSlots(baseEffects, upgradeEffects);

		for (int baseSlotIndex = 0; baseSlotIndex < alignment.BaseSlotEffects.Length; baseSlotIndex++)
		{
			CardExtraEffect? upgradeEffect = alignment.BaseSlotEffects[baseSlotIndex];
			if (upgradeEffect == null)
			{
				continue;
			}

			if (upgradeEffect.DisableOnUpgrade)
			{
				result[baseSlotIndex] = (null!, 0);
				continue;
			}

			CardExtraEffect baseEffect = baseEffects[baseSlotIndex];
			if (!HasMeaningfulUpgradeBaseSlotDelta(baseEffect, upgradeEffect, secondaryNumericFieldsAreDeltas))
			{
				continue;
			}

			CardExtraEffect merged = MergeUpgradeBaseSlotEffect(baseEffect, upgradeEffect, secondaryNumericFieldsAreDeltas);
			result[baseSlotIndex] = (merged, GetUpgradeHighlightComparison(baseEffect, merged));
		}

		for (int i = 0; i < upgradeEffects.Count; i++)
		{
			CardExtraEffect? upgradeEffect = upgradeEffects[i];
			if (upgradeEffect == null || alignment.UpgradeIndexToBaseSlot[i] >= 0)
			{
				continue;
			}

			if (!ShouldTreatUnmatchedUpgradeEffectAsAbsolute(baseEffects.Count, i, alignment.LastMatchedUpgradeIndex))
			{
				continue;
			}

			if (!upgradeEffect.DisableOnUpgrade && IsValidEffectAmount(upgradeEffect.Kind, upgradeEffect.Amount))
			{
				CardExtraEffect cloned = CloneEffect(upgradeEffect);
				result.Add((cloned, GetUpgradeHighlightComparison(baseEffect: null, upgradedEffect: cloned)));
			}
		}

		return result;
	}

	private static UpgradeEffectAlignment AlignUpgradeEffectsToBaseSlots(IReadOnlyList<CardExtraEffect> baseEffects, IReadOnlyList<CardExtraEffect> upgradeEffects)
	{
		int baseCount = baseEffects?.Count ?? 0;
		int upgradeCount = upgradeEffects?.Count ?? 0;
		CardExtraEffect?[] baseSlotEffects = new CardExtraEffect?[baseCount];
		int[] upgradeIndexToBaseSlot = Enumerable.Repeat(-1, upgradeCount).ToArray();

		if (baseCount == 0 || upgradeCount == 0)
		{
			return new UpgradeEffectAlignment
			{
				BaseSlotEffects = baseSlotEffects,
				UpgradeIndexToBaseSlot = upgradeIndexToBaseSlot,
				LastMatchedUpgradeIndex = -1
			};
		}

		bool[] usedBaseSlots = new bool[baseCount];
		int lastMatchedUpgradeIndex = -1;
		for (int i = 0; i < upgradeCount; i++)
		{
			CardExtraEffect? upgradeEffect = upgradeEffects[i];
			if (upgradeEffect == null)
			{
				continue;
			}

			int baseSlotIndex = ResolveUpgradeBaseSlotIndex(baseEffects, upgradeEffect, preferredIndex: i, usedBaseSlots);
			if (baseSlotIndex < 0)
			{
				continue;
			}

			usedBaseSlots[baseSlotIndex] = true;
			baseSlotEffects[baseSlotIndex] = upgradeEffect;
			upgradeIndexToBaseSlot[i] = baseSlotIndex;
			lastMatchedUpgradeIndex = i;
		}

		return new UpgradeEffectAlignment
		{
			BaseSlotEffects = baseSlotEffects,
			UpgradeIndexToBaseSlot = upgradeIndexToBaseSlot,
			LastMatchedUpgradeIndex = lastMatchedUpgradeIndex
		};
	}

	internal static (CardExtraEffect?[] BaseSlotEffects, List<CardExtraEffect> AbsoluteEffects) AlignUpgradeEffectsForEditor(IReadOnlyList<CardExtraEffect> baseEffects, IReadOnlyList<CardExtraEffect> upgradeEffects)
	{
		UpgradeEffectAlignment alignment = AlignUpgradeEffectsToBaseSlots(baseEffects, upgradeEffects);
		List<CardExtraEffect> absoluteEffects = new List<CardExtraEffect>();
		for (int i = 0; i < (upgradeEffects?.Count ?? 0); i++)
		{
			CardExtraEffect? upgradeEffect = upgradeEffects[i];
			if (upgradeEffect == null || alignment.UpgradeIndexToBaseSlot[i] >= 0)
			{
				continue;
			}

			if (!ShouldTreatUnmatchedUpgradeEffectAsAbsolute(baseEffects?.Count ?? 0, i, alignment.LastMatchedUpgradeIndex))
			{
				continue;
			}

			if (!upgradeEffect.DisableOnUpgrade && IsValidEffectAmount(upgradeEffect.Kind, upgradeEffect.Amount))
			{
				absoluteEffects.Add(CloneEffect(upgradeEffect));
			}
		}

		return (alignment.BaseSlotEffects, absoluteEffects);
	}

	internal static List<CardExtraEffect>? RebaseUpgradeEffectsAfterBaseEdit(
		IReadOnlyList<CardExtraEffect>? oldBaseEffects,
		IReadOnlyList<CardExtraEffect>? newBaseEffects,
		IReadOnlyList<CardExtraEffect>? existingUpgradeEffects)
	{
		if (existingUpgradeEffects == null || existingUpgradeEffects.Count == 0)
		{
			return null;
		}

		int oldBaseCount = oldBaseEffects?.Count ?? 0;
		int newBaseCount = newBaseEffects?.Count ?? 0;
		List<CardExtraEffect> rebased = Enumerable.Repeat<CardExtraEffect>(null!, newBaseCount).ToList();
		List<CardExtraEffect> absoluteEffects = new List<CardExtraEffect>();
		bool[] usedNewBaseSlots = new bool[newBaseCount];

		for (int i = 0; i < existingUpgradeEffects.Count; i++)
		{
			CardExtraEffect? existingUpgradeEffect = existingUpgradeEffects[i];
			if (existingUpgradeEffect == null)
			{
				continue;
			}

			if (i < oldBaseCount)
			{
				CardExtraEffect? oldBaseEffect = oldBaseEffects?[i];
				if (oldBaseEffect == null || newBaseCount == 0)
				{
					continue;
				}

				int newBaseSlotIndex = ResolveEquivalentBaseSlotIndex(newBaseEffects!, oldBaseEffect, preferredIndex: i, usedNewBaseSlots);
				if (newBaseSlotIndex < 0)
				{
					continue;
				}

				usedNewBaseSlots[newBaseSlotIndex] = true;
				rebased[newBaseSlotIndex] = CloneEffect(existingUpgradeEffect);
				continue;
			}

			if (!existingUpgradeEffect.DisableOnUpgrade && IsValidEffectAmount(existingUpgradeEffect.Kind, existingUpgradeEffect.Amount))
			{
				absoluteEffects.Add(CloneEffect(existingUpgradeEffect));
			}
		}

		if (absoluteEffects.Count > 0)
		{
			rebased.AddRange(absoluteEffects);
		}

		return rebased.Any(effect => effect != null) ? rebased : null;
	}

	internal static bool ShouldTreatUnmatchedUpgradeEffectAsAbsolute(int baseCount, int upgradeIndex, int lastMatchedUpgradeIndex)
	{
		if (upgradeIndex < 0)
		{
			return false;
		}

		if (lastMatchedUpgradeIndex >= 0)
		{
			return upgradeIndex > lastMatchedUpgradeIndex;
		}

		return upgradeIndex >= baseCount;
	}

	private static int ResolveUpgradeBaseSlotIndex(IReadOnlyList<CardExtraEffect> baseEffects, CardExtraEffect upgradeEffect, int preferredIndex, bool[] usedBaseSlots)
	{
		if (baseEffects == null || upgradeEffect == null || baseEffects.Count == 0)
		{
			return -1;
		}

		if (preferredIndex >= 0
			&& preferredIndex < baseEffects.Count
			&& (usedBaseSlots == null || preferredIndex >= usedBaseSlots.Length || !usedBaseSlots[preferredIndex])
			&& CanUsePreferredUpgradeBaseSlot(baseEffects[preferredIndex], upgradeEffect))
		{
			return preferredIndex;
		}

		for (int i = 0; i < baseEffects.Count; i++)
		{
			if (usedBaseSlots != null && i < usedBaseSlots.Length && usedBaseSlots[i])
			{
				continue;
			}
			if (LooksLikeSameUpgradeBaseSlot(baseEffects[i], upgradeEffect))
			{
				return i;
			}
		}

		for (int i = 0; i < baseEffects.Count; i++)
		{
			if (usedBaseSlots != null && i < usedBaseSlots.Length && usedBaseSlots[i])
			{
				continue;
			}
			if (CanUsePreferredUpgradeBaseSlot(baseEffects[i], upgradeEffect))
			{
				return i;
			}
		}

		return -1;
	}

	private static int ResolveEquivalentBaseSlotIndex(IReadOnlyList<CardExtraEffect> candidateBaseEffects, CardExtraEffect referenceBaseEffect, int preferredIndex, bool[] usedBaseSlots)
	{
		if (candidateBaseEffects == null || referenceBaseEffect == null || candidateBaseEffects.Count == 0)
		{
			return -1;
		}

		if (preferredIndex >= 0
			&& preferredIndex < candidateBaseEffects.Count
			&& (usedBaseSlots == null || preferredIndex >= usedBaseSlots.Length || !usedBaseSlots[preferredIndex])
			&& LooksLikeSameUpgradeBaseSlot(candidateBaseEffects[preferredIndex], referenceBaseEffect))
		{
			return preferredIndex;
		}

		for (int i = 0; i < candidateBaseEffects.Count; i++)
		{
			if (usedBaseSlots != null && i < usedBaseSlots.Length && usedBaseSlots[i])
			{
				continue;
			}
			if (LooksLikeSameUpgradeBaseSlot(candidateBaseEffects[i], referenceBaseEffect))
			{
				return i;
			}
		}

		for (int i = 0; i < candidateBaseEffects.Count; i++)
		{
			if (usedBaseSlots != null && i < usedBaseSlots.Length && usedBaseSlots[i])
			{
				continue;
			}
			if (CanUsePreferredUpgradeBaseSlot(candidateBaseEffects[i], referenceBaseEffect))
			{
				return i;
			}
		}

		return -1;
	}

	private static bool CanUsePreferredUpgradeBaseSlot(CardExtraEffect baseEffect, CardExtraEffect upgradeEffect)
	{
		if (baseEffect == null || upgradeEffect == null)
		{
			return false;
		}

		if (baseEffect.Kind != upgradeEffect.Kind)
		{
			return false;
		}

		string? upgradeKeyword = NormalizeCustomKeywordName(upgradeEffect.CustomKeywordName);
		if (upgradeKeyword != null)
		{
			string? baseKeyword = NormalizeCustomKeywordName(baseEffect.CustomKeywordName);
			if (!string.Equals(baseKeyword ?? string.Empty, upgradeKeyword, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
		}

		string? upgradePowerName = string.IsNullOrWhiteSpace(upgradeEffect.CustomPowerName) ? null : upgradeEffect.CustomPowerName.Trim();
		if (upgradePowerName != null)
		{
			string? basePowerName = string.IsNullOrWhiteSpace(baseEffect.CustomPowerName) ? null : baseEffect.CustomPowerName.Trim();
			if (!string.Equals(basePowerName ?? string.Empty, upgradePowerName, StringComparison.Ordinal))
			{
				return false;
			}
		}

		string? upgradePowerDescription = string.IsNullOrWhiteSpace(upgradeEffect.CustomPowerDescription) ? null : upgradeEffect.CustomPowerDescription.Trim();
		if (upgradePowerDescription != null)
		{
			string? basePowerDescription = string.IsNullOrWhiteSpace(baseEffect.CustomPowerDescription) ? null : baseEffect.CustomPowerDescription.Trim();
			if (!string.Equals(basePowerDescription ?? string.Empty, upgradePowerDescription, StringComparison.Ordinal))
			{
				return false;
			}
		}

		return true;
	}

	private static bool LooksLikeSameUpgradeBaseSlot(CardExtraEffect baseEffect, CardExtraEffect upgradeEffect)
	{
		if (baseEffect == null || upgradeEffect == null)
		{
			return false;
		}

		// CustomKeywordName is presentation/grouping only. Treat it as inherited from base unless the
		// upgrade explicitly sets it, otherwise keyword-fused effects fail to match and get duplicated.
		string? upgradeKeyword = NormalizeCustomKeywordName(upgradeEffect.CustomKeywordName);
		if (upgradeKeyword != null)
		{
			string? baseKeyword = NormalizeCustomKeywordName(baseEffect.CustomKeywordName);
			if (!string.Equals(baseKeyword ?? string.Empty, upgradeKeyword, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
		}

		string? upgradePowerName = string.IsNullOrWhiteSpace(upgradeEffect.CustomPowerName) ? null : upgradeEffect.CustomPowerName.Trim();
		if (upgradePowerName != null)
		{
			string? basePowerName = string.IsNullOrWhiteSpace(baseEffect.CustomPowerName) ? null : baseEffect.CustomPowerName.Trim();
			if (!string.Equals(basePowerName ?? string.Empty, upgradePowerName, StringComparison.Ordinal))
			{
				return false;
			}
		}

		string? upgradePowerDescription = string.IsNullOrWhiteSpace(upgradeEffect.CustomPowerDescription) ? null : upgradeEffect.CustomPowerDescription.Trim();
		if (upgradePowerDescription != null)
		{
			string? basePowerDescription = string.IsNullOrWhiteSpace(baseEffect.CustomPowerDescription) ? null : baseEffect.CustomPowerDescription.Trim();
			if (!string.Equals(basePowerDescription ?? string.Empty, upgradePowerDescription, StringComparison.Ordinal))
			{
				return false;
			}
		}

		return baseEffect.Kind == upgradeEffect.Kind
			&& baseEffect.Target == upgradeEffect.Target
			&& baseEffect.AmountIsX == upgradeEffect.AmountIsX
			&& baseEffect.Trigger == upgradeEffect.Trigger
			&& baseEffect.PowerTriggerCountEvent == upgradeEffect.PowerTriggerCountEvent
			&& baseEffect.PowerTriggerEnemyStatus == upgradeEffect.PowerTriggerEnemyStatus
			&& string.Equals(baseEffect.PowerTriggerPowerId ?? string.Empty, upgradeEffect.PowerTriggerPowerId ?? string.Empty, StringComparison.Ordinal)
			&& baseEffect.TurnBoundary == upgradeEffect.TurnBoundary
			&& baseEffect.TurnBoundarySide == upgradeEffect.TurnBoundarySide
			&& baseEffect.TurnBoundaryCardLocation == upgradeEffect.TurnBoundaryCardLocation
			&& baseEffect.Timing == upgradeEffect.Timing
			&& baseEffect.Duration == upgradeEffect.Duration
			&& baseEffect.AsPower == upgradeEffect.AsPower
			&& baseEffect.TriggerCardPool == upgradeEffect.TriggerCardPool
			&& baseEffect.TriggerCardType == upgradeEffect.TriggerCardType
			&& baseEffect.TriggerCardFilter == upgradeEffect.TriggerCardFilter
			&& baseEffect.CardCostsLessDuration == upgradeEffect.CardCostsLessDuration
			&& baseEffect.CardCostsLessMode == upgradeEffect.CardCostsLessMode
			&& baseEffect.CardCostsLessModifier == upgradeEffect.CardCostsLessModifier
			&& baseEffect.GeneratedCardPool == upgradeEffect.GeneratedCardPool
			&& baseEffect.GeneratedCardType == upgradeEffect.GeneratedCardType
			&& baseEffect.ScaleMode == upgradeEffect.ScaleMode
			&& baseEffect.CountEvent == upgradeEffect.CountEvent
			&& baseEffect.CountWindow == upgradeEffect.CountWindow
			&& baseEffect.CountWindowInclusion == upgradeEffect.CountWindowInclusion
			&& baseEffect.BlockLostCountingMode == upgradeEffect.BlockLostCountingMode
			&& baseEffect.CountCardPile == upgradeEffect.CountCardPile
			&& baseEffect.CountCardPool == upgradeEffect.CountCardPool
			&& baseEffect.CountCardType == upgradeEffect.CountCardType
			&& baseEffect.CountCardFilter == upgradeEffect.CountCardFilter
			&& baseEffect.CountOnlyBlockCards == upgradeEffect.CountOnlyBlockCards
			&& GetEffectiveCountAggregationMode(baseEffect) == GetEffectiveCountAggregationMode(upgradeEffect)
			&& baseEffect.CountOrbType == upgradeEffect.CountOrbType
			&& baseEffect.CountOrbSelection == upgradeEffect.CountOrbSelection
			&& baseEffect.CountEnemyStatus == upgradeEffect.CountEnemyStatus
			&& string.Equals(baseEffect.CountPowerId ?? string.Empty, upgradeEffect.CountPowerId ?? string.Empty, StringComparison.Ordinal)
			&& baseEffect.CountEnemyIntent == upgradeEffect.CountEnemyIntent
			&& baseEffect.CountComparison == upgradeEffect.CountComparison
			&& baseEffect.HistoryScalingIncludesBase == upgradeEffect.HistoryScalingIncludesBase
			&& baseEffect.RepeatIsX == upgradeEffect.RepeatIsX
			&& baseEffect.GrantToCard == upgradeEffect.GrantToCard
			&& baseEffect.CardSelectionMode == upgradeEffect.CardSelectionMode
			&& baseEffect.CardSelectionCountIsX == upgradeEffect.CardSelectionCountIsX
			&& baseEffect.CardSelectionPile == upgradeEffect.CardSelectionPile
			&& baseEffect.CardGrantDuration == upgradeEffect.CardGrantDuration
			&& string.Equals(baseEffect.EnchantmentId ?? string.Empty, upgradeEffect.EnchantmentId ?? string.Empty, StringComparison.Ordinal)
			&& baseEffect.EnchantmentDuration == upgradeEffect.EnchantmentDuration
			&& baseEffect.MoveToPile == upgradeEffect.MoveToPile
			&& baseEffect.MoveToPosition == upgradeEffect.MoveToPosition
			&& baseEffect.UseMoveDestinationForGeneratedCards == upgradeEffect.UseMoveDestinationForGeneratedCards
			&& baseEffect.AdditionalMoveToPiles == upgradeEffect.AdditionalMoveToPiles
			&& baseEffect.DrawnFromPile == upgradeEffect.DrawnFromPile
			&& string.Equals(baseEffect.SpecificCardId ?? string.Empty, upgradeEffect.SpecificCardId ?? string.Empty, StringComparison.Ordinal)
			&& baseEffect.TransformMode == upgradeEffect.TransformMode
			&& baseEffect.ConditionalBonusConditionType == upgradeEffect.ConditionalBonusConditionType
			&& baseEffect.ConditionalBonusCondition == upgradeEffect.ConditionalBonusCondition
			&& baseEffect.ConditionalBonusEnemyStatus == upgradeEffect.ConditionalBonusEnemyStatus
			&& string.Equals(baseEffect.ConditionalBonusPowerId ?? string.Empty, upgradeEffect.ConditionalBonusPowerId ?? string.Empty, StringComparison.Ordinal)
			&& baseEffect.ConditionalBonusEnemyIntent == upgradeEffect.ConditionalBonusEnemyIntent
			&& baseEffect.BranchMode == upgradeEffect.BranchMode
			&& baseEffect.BranchCondition == upgradeEffect.BranchCondition
			&& baseEffect.BranchEnemyStatus == upgradeEffect.BranchEnemyStatus
			&& string.Equals(baseEffect.BranchPowerId ?? string.Empty, upgradeEffect.BranchPowerId ?? string.Empty, StringComparison.Ordinal)
			&& baseEffect.BranchEnemyIntent == upgradeEffect.BranchEnemyIntent
			&& BranchEffectsMatch(baseEffect.BranchEffect, upgradeEffect.BranchEffect)
			&& string.Equals(baseEffect.PowerId ?? string.Empty, upgradeEffect.PowerId ?? string.Empty, StringComparison.Ordinal)
			&& baseEffect.OrbAction == upgradeEffect.OrbAction
			&& baseEffect.OrbType == upgradeEffect.OrbType
			&& baseEffect.OrbSelection == upgradeEffect.OrbSelection
			&& baseEffect.OrbFollowUp == upgradeEffect.OrbFollowUp
			&& baseEffect.OrbScope == upgradeEffect.OrbScope
			&& baseEffect.OstyAction == upgradeEffect.OstyAction
			&& baseEffect.MultiplierStat == upgradeEffect.MultiplierStat
			&& baseEffect.MultiplierSourceMode == upgradeEffect.MultiplierSourceMode
			&& string.Equals(baseEffect.MultiplierPowerId ?? string.Empty, upgradeEffect.MultiplierPowerId ?? string.Empty, StringComparison.Ordinal)
			&& baseEffect.GrantedKeyword == upgradeEffect.GrantedKeyword
			&& baseEffect.CardMatchMode == upgradeEffect.CardMatchMode
			&& string.Equals(baseEffect.MatchCardId ?? string.Empty, upgradeEffect.MatchCardId ?? string.Empty, StringComparison.Ordinal)
			&& baseEffect.MatchTagKind == upgradeEffect.MatchTagKind
			&& baseEffect.MatchVanillaTag == upgradeEffect.MatchVanillaTag
			&& string.Equals(baseEffect.MatchCustomTag ?? string.Empty, upgradeEffect.MatchCustomTag ?? string.Empty, StringComparison.Ordinal)
			&& string.Equals(baseEffect.MatchCustomKeyword ?? string.Empty, upgradeEffect.MatchCustomKeyword ?? string.Empty, StringComparison.Ordinal)
			&& baseEffect.NameFilterEnabled == upgradeEffect.NameFilterEnabled
			&& string.Equals(baseEffect.NameFilterText ?? string.Empty, upgradeEffect.NameFilterText ?? string.Empty, StringComparison.Ordinal)
			&& baseEffect.CostFilterEnabled == upgradeEffect.CostFilterEnabled
			&& baseEffect.CostFilterMode == upgradeEffect.CostFilterMode
			&& baseEffect.CostFilterMax == upgradeEffect.CostFilterMax;
	}

	private static bool IsInverseUpgradeDiff(CardExtraEffect effect)
	{
		if (effect == null)
		{
			return false;
		}

		// Mirror vanilla's "inverseDiff" idea for numeric values where lower is "better"/highlight-green.
		// Keep this conservative: only invert clear self-penalty values.
		if (effect.Target != CardExtraEffectTarget.Self)
		{
			return false;
		}

		return effect.Kind is CardExtraEffectKind.LoseStrength
			or CardExtraEffectKind.LoseDexterity
			or CardExtraEffectKind.LoseFocus
			or CardExtraEffectKind.LoseHp
			or CardExtraEffectKind.LoseMaxHp;
	}

	private static int NormalizeUpgradeDiffAmount(CardExtraEffect effect)
	{
		if (effect == null)
		{
			return 0;
		}

		// CreatedCardsCostLess used -1 as a sentinel that means "free"; preserve that in upgrade comparisons.
		if (effect.Kind == CardExtraEffectKind.CreatedCardsCostLess
			&& GetEffectiveCardCostsLessModifier(effect) == CardExtraEffectCostModifier.Free)
		{
			return 0;
		}

		return effect.Amount;
	}

	private static int GetUpgradeHighlightComparison(CardExtraEffect? baseEffect, CardExtraEffect? upgradedEffect)
	{
		if (upgradedEffect == null)
		{
			return 0;
		}

		if (upgradedEffect.AmountIsX)
		{
			int basePlus = baseEffect?.AmountXPlus ?? 0;
			int upgradedPlus = upgradedEffect.AmountXPlus;
			return upgradedPlus.CompareTo(basePlus);
		}

		int upgradedAmount = NormalizeUpgradeDiffAmount(upgradedEffect);
		int baseAmount = baseEffect != null ? NormalizeUpgradeDiffAmount(baseEffect) : 0;

		bool inverse = baseEffect != null && IsInverseUpgradeDiff(baseEffect);
		return inverse
			? baseAmount.CompareTo(upgradedAmount)
			: upgradedAmount.CompareTo(baseAmount);
	}

	public static async Task RunAfterCardPlayed(CombatState combatState, PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		CardModel card = cardPlay.Card;
		if (card == null)
		{
			return;
		}
		using IDisposable _ = CardEditorCardPlayContext.PushScoped(cardPlay);
		using IDisposable __ = CardEditorEffectExecutionAmountContext.PushSessionScoped();
		List<CardExtraEffect>? triggeredCardCostsLessToApplyAfter = null;
		List<CardExtraEffect>? deferredSelfScaling = null;
		bool isCreatedCard = card is CardEditorCreatedCardBase;
		try
		{
			IReadOnlyList<CardExtraEffect> effects = GetRuntimeEffectsIncludingBorrowedSources(combatState, card);
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

				if (IsSelfScalingKind(effect.Kind))
				{
					(deferredSelfScaling ??= new List<CardExtraEffect>()).Add(effect);
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

			if (deferredSelfScaling != null)
			{
				foreach (CardExtraEffect effect in deferredSelfScaling)
				{
					await ExecuteEffect(combatState, choiceContext, cardPlay, effect);
				}
			}

			if (powerEffectsToAdd != null && powerEffectsToAdd.Count > 0)
			{
				Dictionary<Creature, List<CardExtraEffect>> powerEffectsByHost = new Dictionary<Creature, List<CardExtraEffect>>();
				foreach (CardExtraEffect powerEffect in powerEffectsToAdd)
				{
					Creature? hostCreature = ResolvePowerHostCreature(combatState, cardPlay, powerEffect);
					if (hostCreature == null)
					{
						continue;
					}

					if (!powerEffectsByHost.TryGetValue(hostCreature, out List<CardExtraEffect>? hostEffects))
					{
						hostEffects = new List<CardExtraEffect>();
						powerEffectsByHost[hostCreature] = hostEffects;
					}

					hostEffects.Add(powerEffect);
				}

				foreach ((Creature hostCreature, List<CardExtraEffect> hostEffects) in powerEffectsByHost)
				{
					CardEditorExtraEffectPower? power = hostCreature.GetPower<CardEditorExtraEffectPower>();
					if (power == null)
					{
						power = await PowerCmd.Apply<CardEditorExtraEffectPower>(hostCreature, 1, hostCreature, cardPlay.Card);
					}
					if (power != null)
					{
						await power.AddPowerEffects(cardPlay.Card, hostEffects);
					}
				}
			}
		}
		finally
		{
			if (cardPlay.IsLastInSeries)
			{
				ClearManualTarget(card);
				CardEditorTemporaryExtraEffectController.OnAfterCardPlayed(combatState, card);
				CardEditorTemporaryKeywordController.OnAfterCardPlayed(combatState, card);
				CardEditorTemporaryEnchantmentController.OnAfterCardPlayed(combatState, card);
				CardEditorTemporaryReplayController.OnAfterCardPlayed(combatState, card);
				CardEditorMatchingCardAuraController.OnAfterCardPlayed(combatState, card);
				RefreshHandCardVisuals(card.Owner);
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
			IReadOnlyList<CardExtraEffect> effects = GetRuntimeEffectsIncludingBorrowedSources(combatState, card);
			await RunResolvedOnPlayEffectsDuringCardPlay(combatState, choiceContext, cardPlay, effects);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Created card OnPlay extra effects failed: {ex}");
		}
	}

	internal static async Task RunResolvedOnPlayEffectsDuringCardPlay(CombatState combatState, PlayerChoiceContext choiceContext, CardPlay cardPlay, IReadOnlyList<CardExtraEffect> effects)
	{
		CardModel? card = cardPlay?.Card;
		if (combatState == null || choiceContext == null || card == null || effects == null || effects.Count == 0)
		{
			return;
		}

		Creature? ownerCreature = card.Owner?.Creature;
		VigorPreserver? vigor = null;
		int remainingImmediateDamageEffects = 0;
		List<CardExtraEffect>? deferredSelfScaling = null;
		using IDisposable _ = CardEditorEffectExecutionAmountContext.PushSessionScoped();
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

			if (IsSelfScalingKind(effect.Kind))
			{
				(deferredSelfScaling ??= new List<CardExtraEffect>()).Add(effect);
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

		if (deferredSelfScaling != null)
		{
			foreach (CardExtraEffect effect in deferredSelfScaling)
			{
				await ExecuteEffect(combatState, choiceContext, cardPlay, effect);
			}
		}

		if (vigor != null)
		{
			await vigor.FinalizeRestores();
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

	public static Task RunTurnBoundary(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card, CardExtraEffectTurnBoundary boundary, CardExtraEffectTurnBoundarySide side)
	{
		return RunForTurnBoundary(combatState, choiceContext, card, boundary, side);
	}

	public static Task RunAfterCombat(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card)
	{
		return RunForTrigger(combatState, choiceContext, card, CardExtraEffectTrigger.AfterCombat);
	}

	public static Task RunDeckPassiveCombatStart(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card)
	{
		return RunForTrigger(combatState, choiceContext, card, CardExtraEffectTrigger.DeckPassiveCombatStart);
	}

	public static Task RunDeckPassiveCombatEnd(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card)
	{
		return RunForTrigger(combatState, choiceContext, card, CardExtraEffectTrigger.DeckPassiveCombatEnd);
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

	public static Task RunAfterCardMovedToTopOfPile(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card)
	{
		return RunForTrigger(combatState, choiceContext, card, CardExtraEffectTrigger.OnMovedToTopOfPile);
	}

	public static Task RunAfterCardMovedToBottomOfPile(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card)
	{
		return RunForTrigger(combatState, choiceContext, card, CardExtraEffectTrigger.OnMovedToBottomOfPile);
	}

	private static async Task RunForTurnBoundary(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card, CardExtraEffectTurnBoundary boundary, CardExtraEffectTurnBoundarySide side)
	{
		if (combatState == null || choiceContext == null || card == null)
		{
			return;
		}

		try
		{
			using IDisposable _ = CardEditorEffectExecutionAmountContext.PushSessionScoped();
			List<CardExtraEffect> effects = GetRuntimeEffectsIncludingBorrowedSources(combatState, card).ToList();
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
					|| !DoesTurnBoundaryMatch(effect, boundary, side, card))
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
			Log.Warn($"[CardEditor] Extra turn-boundary effects (boundary={boundary}, side={side}) failed: {ex}");
		}
	}

	private static async Task RunForCardPlayTrigger(CombatState combatState, PlayerChoiceContext choiceContext, CardPlay cardPlay, IReadOnlyList<CardExtraEffect> effects, CardExtraEffectTrigger trigger)
	{
		if (combatState == null || choiceContext == null || cardPlay?.Card == null || effects == null || effects.Count == 0)
		{
			return;
		}

		using IDisposable _ = CardEditorEffectExecutionAmountContext.PushSessionScoped();
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
				// Only consider damage entries that occurred during this specific CardPlay.
				// Once we hit the start marker for this play, we stop scanning history.
				if (ReferenceEquals(startedEntry.CardPlay, cardPlay))
				{
					break;
				}
				continue;
			}

			if (entry is not DamageReceivedEntry damageEntry)
			{
				continue;
			}

			bool matchesSource = ReferenceEquals(damageEntry.CardSource, cardPlay.Card)
				|| (damageEntry.CardSource == null && ReferenceEquals(damageEntry.Dealer, cardPlay.Card.Owner.Creature));
			if (!matchesSource
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

		Creature? target = cardPlay.Target;
		if (target != null
			&& target.Side != ownerSide
			&& target.IsDead
			&& target.Powers.All(p => p.ShouldOwnerDeathTriggerFatal()))
		{
			return true;
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
		PileType[] pilesToCheck = { PileType.Exhaust, PileType.Discard, PileType.Draw, PileType.Deck };
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
		foreach (CardExtraEffect e in GetRuntimeEffectsIncludingBorrowedSources(combatState, card))
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

			if (!IsCardAtConfiguredPilePosition(card, effect.CardSelectionMode))
			{
				continue;
			}

			if (effect.ScaleMode != CardExtraEffectScaleMode.None)
			{
				if (CardEditorConditionalFromPileFiredTracker.HasFired(combatState, card))
				{
					continue;
				}

				int count = GetHistoryCountMultiplier(combatState, card.Owner?.Creature, null, effect, card);
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

		PileType[] pilesToCheck = { PileType.Exhaust, PileType.Discard, PileType.Draw, PileType.Deck };
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
		foreach (CardExtraEffect e in GetRuntimeEffectsIncludingBorrowedSources(combatState, card))
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

			if (!IsCardAtConfiguredPilePosition(card, effect.CardSelectionMode))
			{
				continue;
			}

			if (effect.ScaleMode != CardExtraEffectScaleMode.None)
			{
				if (CardEditorConditionalFromPileFiredTracker.HasFired(combatState, card))
				{
					continue;
				}

				int count = GetHistoryCountMultiplier(combatState, card.Owner?.Creature, null, effect, card);
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
		foreach (CardExtraEffect e in GetRuntimeEffectsIncludingBorrowedSources(combatState, card))
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
			int count = GetHistoryCountMultiplier(combatState, card.Owner?.Creature, null, effect, card);
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
		using IDisposable __ = CardEditorEffectExecutionAmountContext.PushSessionScoped();

		try
		{
			List<CardExtraEffect> effects = GetRuntimeEffectsIncludingBorrowedSources(combatState, card).ToList();
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
						Log.Info($"[CardEditor][CostLessTrigger] observed={trigger} card={card?.Id} pile={card?.Pile?.Type} amount={effect.Amount} amountIsX={effect.AmountIsX} duration={effect.CardCostsLessDuration} mode={effect.CardCostsLessMode} boundary={effect.TurnBoundary}/{effect.TurnBoundarySide}/{effect.TurnBoundaryCardLocation}");
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
		if (IsMovedToPileTrigger(effect.Trigger) || IsMovedToPileTrigger(observedTrigger))
		{
			return DoesMovedPileTriggerMatch(effect, observedTrigger, card);
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

	private static bool DoesMovedPileTriggerMatch(CardExtraEffect effect, CardExtraEffectTrigger observedTrigger, CardModel card)
	{
		if (effect == null || effect.Trigger != observedTrigger || !IsMovedToPileTrigger(effect.Trigger) || card?.Pile == null)
		{
			return false;
		}
		if (effect.CardSelectionPile != CardExtraEffectCardPile.AllPiles
			&& card.Pile.Type != ResolvePileType(effect.CardSelectionPile))
		{
			return false;
		}

		return effect.Trigger switch
		{
			CardExtraEffectTrigger.OnMovedToTopOfPile => IsCardAtTopOfPile(card),
			CardExtraEffectTrigger.OnMovedToBottomOfPile => IsCardAtBottomOfPile(card),
			_ => false
		};
	}

	internal static bool DoesTurnBoundaryMatch(CardExtraEffect effect, CardExtraEffectTurnBoundary boundary, CardExtraEffectTurnBoundarySide side, CardModel card)
	{
		if (effect == null || effect.Trigger != CardExtraEffectTrigger.TurnBoundary)
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

		if (active.CardCostsLessDuration == CardExtraEffectCardCostsLessDuration.Permanent)
		{
			if (ApplyPersistentTriggeredCardCostsLessMutation(card, active))
			{
				Log.Info($"[CardEditor][ApplyTriggeredCostLess] card={card.Id} pile={card.Pile?.Type} mutable={card.IsMutable} clone={card.IsClone} cloneOf={card.CloneOf?.Id} duration=Permanent amount={active.Amount} modifier={GetEffectiveCardCostsLessModifier(active)} persistent=True");
				return;
			}

			Log.Warn($"[CardEditor][ApplyTriggeredCostLess] Permanent trigger could not persist on {card?.Id}; falling back to combat-only grant.");
		}

		CardExtraEffectCardGrantDuration duration = effect.CardCostsLessDuration switch
		{
			CardExtraEffectCardCostsLessDuration.ThisTurn => CardExtraEffectCardGrantDuration.ThisTurn,
			CardExtraEffectCardCostsLessDuration.ThisCombat => CardExtraEffectCardGrantDuration.ThisCombat,
			CardExtraEffectCardCostsLessDuration.UntilPlayed => CardExtraEffectCardGrantDuration.UntilPlayed,
			CardExtraEffectCardCostsLessDuration.Turns => CardExtraEffectCardGrantDuration.Turns,
			_ => CardExtraEffectCardGrantDuration.ThisCombat
		};
		int turns = duration == CardExtraEffectCardGrantDuration.Turns
			? Math.Clamp(effect.CardCostsLessTurns, 1, 99)
			: 1;
		// Stack repeated triggers (e.g. "Costs 1 less each time it's played") by merging into an equivalent existing grant when possible.
		if (!CardEditorTemporaryExtraEffectController.TryStackTimedCardCostsLess(combatState, card, active, duration, turns))
		{
			CardEditorTemporaryExtraEffectController.Grant(combatState, card, active, duration, turns);
		}
		Log.Info($"[CardEditor][ApplyTriggeredCostLess] card={card.Id} pile={card.Pile?.Type} mutable={card.IsMutable} clone={card.IsClone} cloneOf={card.CloneOf?.Id} duration={duration} turns={turns} amount={active.Amount} modifier={GetEffectiveCardCostsLessModifier(active)}");
		NotifyTriggeredPermanentCardCostsLessChanged(card, active.Kind);
	}

	private static string GetSelfScalingTargetLabel(CardModel card, CardExtraEffect effect, bool isUpgradePreview)
	{
		return effect.SelfScalingTargetType switch
		{
			CardExtraEffectSelfScalingTargetType.BaseDamage => CardEditorLoc.T("cardText.selfScaling.baseDamage", "this card's damage"),
			CardExtraEffectSelfScalingTargetType.BaseBlock => CardEditorLoc.T("cardText.selfScaling.baseBlock", "this card's block"),
			CardExtraEffectSelfScalingTargetType.EffectRowAmount => BuildSelfScalingEffectRowFieldLabel(card, effect, isUpgradePreview),
			_ => CardEditorLoc.T("cardText.selfScaling.baseDamage", "this card's damage")
		};
	}

	private static bool TryGetEffectRowForDescription(CardModel card, string? effectId, bool isUpgradePreview, out CardExtraEffect matched)
	{
		matched = null!;
		if (card == null || string.IsNullOrWhiteSpace(effectId))
		{
			return false;
		}

		string trimmedEffectId = effectId.Trim();
		foreach (CardExtraEffect candidate in GetEffectsForDescription(card, isUpgradePreview))
		{
			if (candidate == null
				|| string.IsNullOrWhiteSpace(candidate.EffectId)
				|| !string.Equals(candidate.EffectId, trimmedEffectId, StringComparison.Ordinal))
			{
				continue;
			}

			matched = candidate;
			return true;
		}

		return false;
	}

	private static string ResolveSelfScalingEffectRowLabel(CardModel card, CardExtraEffect effect, bool isUpgradePreview)
	{
		if (!TryGetEffectRowForDescription(card, effect.SelfScalingTargetEffectId, isUpgradePreview, out CardExtraEffect candidate))
		{
			return CardEditorLoc.T("cardText.selfScaling.effectRow", "this effect");
		}

		return candidate.Kind switch
		{
			CardExtraEffectKind.DealDamage => CardEditorLoc.T("cardText.selfScaling.damageEffect", "this card's damage effect"),
			CardExtraEffectKind.GainBlock => CardEditorLoc.T("cardText.selfScaling.blockEffect", "this card's block effect"),
			_ => CardEditorLoc.Enum(
				"effectKind",
				candidate.Kind,
				Definitions.FirstOrDefault(def => def.Kind == candidate.Kind)?.Label ?? candidate.Kind.ToString())
		};
	}

	private static string GetSelfScalingFieldLabel(CardExtraEffectSelfScalingField field)
	{
		return field switch
		{
			CardExtraEffectSelfScalingField.Repeat => CardEditorLoc.T("cardText.selfScaling.field.repeat", "repeat count"),
			CardExtraEffectSelfScalingField.SecondaryAmount => CardEditorLoc.T("cardText.selfScaling.field.secondaryAmount", "secondary amount"),
			CardExtraEffectSelfScalingField.Threshold => CardEditorLoc.T("cardText.selfScaling.field.threshold", "threshold"),
			CardExtraEffectSelfScalingField.Duration => CardEditorLoc.T("cardText.selfScaling.field.duration", "duration"),
			_ => CardEditorLoc.T("cardText.selfScaling.field.amount", "amount")
		};
	}

	private static string BuildSelfScalingEffectRowFieldLabel(CardModel card, CardExtraEffect effect, bool isUpgradePreview)
	{
		string rowLabel = ResolveSelfScalingEffectRowLabel(card, effect, isUpgradePreview);
		if (effect.SelfScalingField == CardExtraEffectSelfScalingField.Amount)
		{
			return rowLabel;
		}

		string fieldLabel = GetSelfScalingFieldLabel(effect.SelfScalingField);
		return CardEditorLoc.F(
			"cardText.selfScaling.effectRowField",
			$"{rowLabel}'s {fieldLabel}",
			("Row", rowLabel),
			("Field", fieldLabel));
	}

	private static string FormatSelfScalingLine(CardModel card, CardExtraEffect effect, bool isUpgradePreview)
	{
		string verb = effect.SelfScalingOperation == CardExtraEffectSelfScalingOperation.Decrease ? "decrease" : "increase";
		string targetLabel = GetSelfScalingTargetLabel(card, effect, isUpgradePreview);
		string amountText = effect.Amount.ToString(CultureInfo.InvariantCulture);
		string durationSuffix = effect.Kind == CardExtraEffectKind.PersistentSelfScaling ? " for the rest of the run" : " for this combat";
		string action = $"{verb} {targetLabel} by {amountText}{durationSuffix}";
		return effect.Trigger == CardExtraEffectTrigger.OnPlay
			? $"When played, {action}."
			: $"{UppercaseFirst(action)}.";
	}

	private static string? FormatScalingStageLine(CardModel card, Creature? target, CardExtraEffect effect, bool isUpgradePreview)
	{
		if (card == null || effect == null)
		{
			return null;
		}

		string conditionText = BuildCountConditionClause(card, effect);
		if (string.IsNullOrWhiteSpace(conditionText))
		{
			return null;
		}

		string? payloadLine = BuildEffectSourceReferenceLine(card, target, isUpgradePreview, effect);
		if (string.IsNullOrWhiteSpace(payloadLine))
		{
			return null;
		}

		if (!payloadLine.Contains('\n'))
		{
			string inlinePayload = LowercaseFirst(TrimTrailingSentencePunctuation(payloadLine));
			return CardEditorLoc.F(
				"cardText.scalingStage.inline",
				$"If {conditionText}, {inlinePayload}.",
				("Condition", conditionText),
				("Effect", inlinePayload));
		}

			return CardEditorLoc.F(
				"cardText.scalingStage",
				$"If {conditionText},\n{payloadLine}",
				("Condition", conditionText),
				("Effect", payloadLine));
		}

	private static string GetAppliedAmountSourcePlaceholder(CardExtraEffectKind kind)
	{
		return kind is CardExtraEffectKind.Summon or CardExtraEffectKind.Forge
			? CardEditorLoc.T("cardText.amountSource.thatMany", "that many")
			: CardEditorLoc.T("cardText.amountSource.thatMuch", "that much");
	}

	private static string GetAmountSourceStatusName(CardExtraEffectEnemyStatus status)
	{
		return status switch
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
			_ => "status"
		};
	}

	private static string ResolveAppliedAmountReferenceText(CardModel card, CardExtraEffect effect, bool isUpgradePreview)
	{
		if (!TryGetEffectRowForDescription(card, effect.AmountSourceEffectId, isUpgradePreview, out CardExtraEffect candidate))
		{
			return CardEditorLoc.T("cardText.amountSource.selectedEffect", "the amount from that effect");
		}

		if (TryGetEffectPowerMatchStatus(candidate.Kind, out CardExtraEffectEnemyStatus status))
		{
			string statusName = GetAmountSourceStatusName(status);
			string verb = candidate.Kind switch
			{
				CardExtraEffectKind.RemoveWeak
					or CardExtraEffectKind.RemoveFrail
					or CardExtraEffectKind.RemoveVulnerable
					or CardExtraEffectKind.RemovePoison
					or CardExtraEffectKind.RemoveDoom
					or CardExtraEffectKind.RemoveConstrict
					or CardExtraEffectKind.RemoveArtifact
					or CardExtraEffectKind.RemoveThorns
					or CardExtraEffectKind.RemoveRegen
					or CardExtraEffectKind.RemovePlating
					or CardExtraEffectKind.RemoveIntangible
					or CardExtraEffectKind.RemoveBuffer
					or CardExtraEffectKind.RemoveVigor
					or CardExtraEffectKind.RemoveBlur
					or CardExtraEffectKind.RemoveRitual => "removed",
				CardExtraEffectKind.LoseStrength
					or CardExtraEffectKind.LoseDexterity
					or CardExtraEffectKind.LoseFocus => "lost",
				CardExtraEffectKind.GainStrength
					or CardExtraEffectKind.GainDexterity
					or CardExtraEffectKind.GainFocus
					or CardExtraEffectKind.GainArtifact
					or CardExtraEffectKind.GainThorns
					or CardExtraEffectKind.GainRegen
					or CardExtraEffectKind.GainPlating
					or CardExtraEffectKind.GainIntangible
					or CardExtraEffectKind.GainBuffer
					or CardExtraEffectKind.GainVigor
					or CardExtraEffectKind.GainBlur
					or CardExtraEffectKind.GainRitual => "gained",
				_ => "applied"
			};
			return $"the {statusName} {verb}";
		}

		string fallbackRowLabel = ResolveSelfScalingEffectRowLabel(card, new CardExtraEffect { SelfScalingTargetEffectId = candidate.EffectId }, isUpgradePreview);
		return candidate.Kind switch
		{
			CardExtraEffectKind.RunEffectSourceCard => CardEditorLoc.F(
				"cardText.amountSource.effectSourceApplied",
				$"the amount from {ResolveSpecificCardTitle(candidate.SpecificCardId) ?? candidate.SpecificCardId?.Trim() ?? CardEditorLoc.T("cardText.amountSource.selectedEffect", "the amount from that effect")}",
				("Card", ResolveSpecificCardTitle(candidate.SpecificCardId) ?? candidate.SpecificCardId?.Trim() ?? CardEditorLoc.T("cardText.amountSource.selectedEffect", "the amount from that effect"))),
				CardExtraEffectKind.DealDamage => CardEditorLoc.T("cardText.amountSource.damageDealt", "the damage dealt"),
				CardExtraEffectKind.GainBlock => CardEditorLoc.T("cardText.amountSource.blockGained", "the Block gained"),
			CardExtraEffectKind.Heal => CardEditorLoc.T("cardText.amountSource.hpHealed", "the HP healed"),
			CardExtraEffectKind.LoseHp => CardEditorLoc.T("cardText.amountSource.hpLost", "the HP lost"),
			CardExtraEffectKind.GainMaxHp => CardEditorLoc.T("cardText.amountSource.maxHpGained", "the Max HP gained"),
			CardExtraEffectKind.LoseMaxHp => CardEditorLoc.T("cardText.amountSource.maxHpLost", "the Max HP lost"),
			CardExtraEffectKind.GainEnergy => CardEditorLoc.T("cardText.amountSource.energyGained", "the Energy gained"),
			CardExtraEffectKind.LoseEnergy => CardEditorLoc.T("cardText.amountSource.energyLost", "the Energy lost"),
			CardExtraEffectKind.GainStars => CardEditorLoc.T("cardText.amountSource.starsGained", "the Stars gained"),
			CardExtraEffectKind.LoseStars => CardEditorLoc.T("cardText.amountSource.starsLost", "the Stars lost"),
			CardExtraEffectKind.GainGold => CardEditorLoc.T("cardText.amountSource.goldGained", "the Gold gained"),
			CardExtraEffectKind.LoseGold => CardEditorLoc.T("cardText.amountSource.goldLost", "the Gold lost"),
			CardExtraEffectKind.Summon => CardEditorLoc.T("cardText.amountSource.amountSummoned", "the amount summoned"),
			CardExtraEffectKind.Forge => CardEditorLoc.T("cardText.amountSource.amountForged", "the amount forged"),
			CardExtraEffectKind.RemoveBlock => CardEditorLoc.T("cardText.amountSource.blockRemoved", "the Block removed"),
			CardExtraEffectKind.ApplyPower => CardEditorLoc.T("cardText.amountSource.powerApplied", "the amount from that effect"),
			_ => CardEditorLoc.F(
				"cardText.amountSource.effectRowAmount",
				$"the amount from {fallbackRowLabel}",
				("Row", fallbackRowLabel))
		};
	}

	private static string BuildAppliedAmountSourceSuffix(CardModel card, CardExtraEffect effect, bool isUpgradePreview)
	{
		string referenceText = ResolveAppliedAmountReferenceText(card, effect, isUpgradePreview);
		return CardEditorLoc.F(
			"cardText.amountSource.suffix",
			$"Equal to {referenceText}.",
			("Amount", referenceText));
	}

	private static string ResolveValueSourceReferenceText(CardExtraEffect effect)
	{
		string valueLabel = effect.ValueSourceMode == CardExtraEffectValueSourceMode.PowerStatus
			? (ResolvePowerTitle(effect.ValueSourcePowerId) ?? CardEditorLoc.T("cardText.power.unknown", "Unknown Power"))
			: ValueSourceKindLabel(effect.ValueSourceKind);
		return effect.ValueSourceActor switch
		{
			CardExtraEffectValueSourceActor.Self => CardEditorLoc.F(
				"cardText.valueSource.self",
				$"your {valueLabel}",
				("Value", valueLabel)),
			CardExtraEffectValueSourceActor.Target => CardEditorLoc.F(
				"cardText.valueSource.target",
				$"the target's {valueLabel}",
				("Value", valueLabel)),
			CardExtraEffectValueSourceActor.AllEnemies => ResolveGroupedValueSourceReferenceText(effect.ValueSourceAggregation, valueLabel, CardEditorLoc.T("cardText.valueSource.allEnemies", "ALL enemies")),
			CardExtraEffectValueSourceActor.AllAllies => ResolveGroupedValueSourceReferenceText(effect.ValueSourceAggregation, valueLabel, CardEditorLoc.T("cardText.valueSource.allAllies", "ALL allies")),
			_ => CardEditorLoc.F(
				"cardText.valueSource.self",
				$"your {valueLabel}",
				("Value", valueLabel))
		};
	}

	private static string ResolveGroupedValueSourceReferenceText(CardExtraEffectValueSourceAggregation aggregation, string valueLabel, string actorLabel)
	{
		CardExtraEffectValueSourceAggregation effectiveAggregation = aggregation == CardExtraEffectValueSourceAggregation.Value
			? CardExtraEffectValueSourceAggregation.Sum
			: aggregation;
		return effectiveAggregation switch
		{
			CardExtraEffectValueSourceAggregation.Highest => CardEditorLoc.F(
				"cardText.valueSource.group.highest",
				$"the highest {valueLabel} among {actorLabel}",
				("Value", valueLabel),
				("Actor", actorLabel)),
			CardExtraEffectValueSourceAggregation.Lowest => CardEditorLoc.F(
				"cardText.valueSource.group.lowest",
				$"the lowest {valueLabel} among {actorLabel}",
				("Value", valueLabel),
				("Actor", actorLabel)),
			CardExtraEffectValueSourceAggregation.Average => CardEditorLoc.F(
				"cardText.valueSource.group.average",
				$"the average {valueLabel} among {actorLabel}",
				("Value", valueLabel),
				("Actor", actorLabel)),
				_ => CardEditorLoc.F(
					"cardText.valueSource.group.sum",
					$"the total {valueLabel} on {actorLabel}",
					("Value", valueLabel),
					("Actor", actorLabel))
			};
		}

	private static string BuildValueSourceAmountSuffix(CardExtraEffect effect)
	{
		string referenceText = ResolveValueSourceReferenceText(effect);
		return CardEditorLoc.F(
			"cardText.valueSource.suffix",
			$"Equal to {referenceText}.",
			("Amount", referenceText));
	}

	private static string? TryFormatDirectDynamicAmountSourceLine(CardModel card, CardExtraEffect effect, string referenceText, string? powerDurationSuffix)
	{
		if (card == null || effect == null || string.IsNullOrWhiteSpace(referenceText))
		{
			return null;
		}

		string suffixPart = BuildSuffixPart(powerDurationSuffix);

		return effect.Kind switch
		{
			CardExtraEffectKind.DealDamage => effect.Target switch
			{
				CardExtraEffectTarget.AllEnemies => $"Deal damage equal to {referenceText} to ALL enemies.",
				CardExtraEffectTarget.RandomEnemy => $"Deal damage equal to {referenceText} to a random enemy.",
				CardExtraEffectTarget.Self => $"Take damage equal to {referenceText}.",
				_ => $"Deal damage equal to {referenceText}."
			},
				CardExtraEffectKind.CardDealsExtraDamage => $"This card deals bonus damage equal to {referenceText}.",
			CardExtraEffectKind.GainBlock => effect.Target switch
			{
				CardExtraEffectTarget.AllEnemies => $"ALL enemies gain [gold]Block[/gold] equal to {referenceText}.",
				CardExtraEffectTarget.RandomEnemy => $"A random enemy gains [gold]Block[/gold] equal to {referenceText}.",
				CardExtraEffectTarget.Self => $"Gain [gold]Block[/gold] equal to {referenceText}.",
				_ => $"The target gains [gold]Block[/gold] equal to {referenceText}."
			},
			CardExtraEffectKind.RemoveBlock => effect.Target switch
			{
				CardExtraEffectTarget.AllEnemies => $"ALL enemies lose [gold]Block[/gold] equal to {referenceText}.",
				CardExtraEffectTarget.RandomEnemy => $"A random enemy loses [gold]Block[/gold] equal to {referenceText}.",
				CardExtraEffectTarget.Self => $"Lose [gold]Block[/gold] equal to {referenceText}.",
				_ => $"The target loses [gold]Block[/gold] equal to {referenceText}."
			},
			CardExtraEffectKind.Heal => effect.Target switch
			{
				CardExtraEffectTarget.AllEnemies => $"ALL enemies heal HP equal to {referenceText}.",
				CardExtraEffectTarget.RandomEnemy => $"A random enemy heals HP equal to {referenceText}.",
				CardExtraEffectTarget.Self => $"Heal HP equal to {referenceText}.",
				_ => $"The target heals HP equal to {referenceText}."
			},
			CardExtraEffectKind.LoseHp => effect.Target switch
			{
				CardExtraEffectTarget.AllEnemies => $"ALL enemies lose HP equal to {referenceText}.",
				CardExtraEffectTarget.RandomEnemy => $"A random enemy loses HP equal to {referenceText}.",
				CardExtraEffectTarget.Self => $"Lose HP equal to {referenceText}.",
				_ => $"The target loses HP equal to {referenceText}."
			},
			CardExtraEffectKind.GainMaxHp => $"Gain Max HP equal to {referenceText}.",
			CardExtraEffectKind.LoseMaxHp => $"Lose Max HP equal to {referenceText}.",
				CardExtraEffectKind.GainEnergy => $"Gain Energy equal to {referenceText}.",
				CardExtraEffectKind.LoseEnergy => $"Lose Energy equal to {referenceText}.",
				CardExtraEffectKind.GainStars => $"Gain Stars equal to {referenceText}.",
				CardExtraEffectKind.LoseStars => $"Lose Stars equal to {referenceText}.",
			CardExtraEffectKind.GainGold => $"Gain Gold equal to {referenceText}.",
			CardExtraEffectKind.LoseGold => $"Lose Gold equal to {referenceText}.",
			CardExtraEffectKind.GainStrength => FormatEqualToSignedPowerText(effect.Target, gain: true, "Strength", referenceText, powerDurationSuffix),
			CardExtraEffectKind.LoseStrength => FormatEqualToSignedPowerText(effect.Target, gain: false, "Strength", referenceText, powerDurationSuffix),
			CardExtraEffectKind.GainDexterity => FormatEqualToSignedPowerText(effect.Target, gain: true, "Dexterity", referenceText, powerDurationSuffix),
			CardExtraEffectKind.LoseDexterity => FormatEqualToSignedPowerText(effect.Target, gain: false, "Dexterity", referenceText, powerDurationSuffix),
			CardExtraEffectKind.GainFocus => FormatEqualToSignedPowerText(effect.Target, gain: true, "Focus", referenceText, powerDurationSuffix),
			CardExtraEffectKind.LoseFocus => FormatEqualToSignedPowerText(effect.Target, gain: false, "Focus", referenceText, powerDurationSuffix),
			CardExtraEffectKind.GainArtifact => FormatEqualToGainPowerText(effect.Target, "Artifact", referenceText, powerDurationSuffix),
			CardExtraEffectKind.GainThorns => FormatEqualToGainPowerText(effect.Target, "Thorns", referenceText, powerDurationSuffix),
			CardExtraEffectKind.GainRegen => FormatEqualToGainPowerText(effect.Target, "Regen", referenceText, powerDurationSuffix),
			CardExtraEffectKind.GainPlating => FormatEqualToGainPowerText(effect.Target, "Plating", referenceText, powerDurationSuffix),
			CardExtraEffectKind.GainIntangible => FormatEqualToGainPowerText(effect.Target, "Intangible", referenceText, powerDurationSuffix),
			CardExtraEffectKind.GainBuffer => FormatEqualToGainPowerText(effect.Target, "Buffer", referenceText, powerDurationSuffix),
			CardExtraEffectKind.GainVigor => FormatEqualToGainPowerText(effect.Target, "Vigor", referenceText, powerDurationSuffix),
			CardExtraEffectKind.GainBlur => FormatEqualToGainPowerText(effect.Target, "Blur", referenceText, powerDurationSuffix),
			CardExtraEffectKind.GainRitual => FormatEqualToGainPowerText(effect.Target, "Ritual", referenceText, powerDurationSuffix),
			CardExtraEffectKind.RemoveArtifact => FormatEqualToLosePowerText(effect.Target, "Artifact", referenceText),
			CardExtraEffectKind.RemoveThorns => FormatEqualToLosePowerText(effect.Target, "Thorns", referenceText),
			CardExtraEffectKind.RemoveRegen => FormatEqualToLosePowerText(effect.Target, "Regen", referenceText),
			CardExtraEffectKind.RemovePlating => FormatEqualToLosePowerText(effect.Target, "Plating", referenceText),
			CardExtraEffectKind.RemoveIntangible => FormatEqualToLosePowerText(effect.Target, "Intangible", referenceText),
			CardExtraEffectKind.RemoveBuffer => FormatEqualToLosePowerText(effect.Target, "Buffer", referenceText),
			CardExtraEffectKind.RemoveVigor => FormatEqualToLosePowerText(effect.Target, "Vigor", referenceText),
			CardExtraEffectKind.RemoveBlur => FormatEqualToLosePowerText(effect.Target, "Blur", referenceText),
			CardExtraEffectKind.RemoveRitual => FormatEqualToLosePowerText(effect.Target, "Ritual", referenceText),
			CardExtraEffectKind.ApplyWeak => FormatEqualToDebuffText(effect.Target, "Weak", referenceText, powerDurationSuffix),
			CardExtraEffectKind.ApplyFrail => FormatEqualToDebuffText(effect.Target, "Frail", referenceText, powerDurationSuffix),
			CardExtraEffectKind.ApplyVulnerable => FormatEqualToDebuffText(effect.Target, "Vulnerable", referenceText, powerDurationSuffix),
			CardExtraEffectKind.ApplyPoison => FormatEqualToDebuffText(effect.Target, "Poison", referenceText, powerDurationSuffix),
			CardExtraEffectKind.ApplyDoom => FormatEqualToDebuffText(effect.Target, "Doom", referenceText, powerDurationSuffix),
			CardExtraEffectKind.ApplyConstrict => FormatEqualToDebuffText(effect.Target, "Constrict", referenceText, powerDurationSuffix),
			CardExtraEffectKind.RemoveWeak => FormatEqualToLosePowerText(effect.Target, "Weak", referenceText),
			CardExtraEffectKind.RemoveFrail => FormatEqualToLosePowerText(effect.Target, "Frail", referenceText),
			CardExtraEffectKind.RemoveVulnerable => FormatEqualToLosePowerText(effect.Target, "Vulnerable", referenceText),
			CardExtraEffectKind.RemovePoison => FormatEqualToLosePowerText(effect.Target, "Poison", referenceText),
			CardExtraEffectKind.RemoveDoom => FormatEqualToLosePowerText(effect.Target, "Doom", referenceText),
			CardExtraEffectKind.RemoveConstrict => FormatEqualToLosePowerText(effect.Target, "Constrict", referenceText),
			CardExtraEffectKind.ApplyPower => FormatEqualToApplyPowerText(effect, referenceText, powerDurationSuffix),
			CardExtraEffectKind.Summon => $"[gold]Summon[/gold] equal to {referenceText}.",
			CardExtraEffectKind.Forge => $"[gold]Forge[/gold] equal to {referenceText}.",
			_ => null
		};
	}

	private static string FormatEqualToDebuffText(CardExtraEffectTarget target, string debuffName, string referenceText, string? suffix = null)
	{
		string title = PowerTitle(debuffName, debuffName);
		string payload = $"[gold]{title}[/gold]";
		string suffixPart = BuildSuffixPart(suffix);
		return target switch
		{
			CardExtraEffectTarget.AllEnemies => $"Apply {payload} equal to {referenceText} to ALL enemies{suffixPart}",
			CardExtraEffectTarget.RandomEnemy => $"Apply {payload} equal to {referenceText} to a random enemy{suffixPart}",
			CardExtraEffectTarget.Self => $"Gain {payload} equal to {referenceText}{suffixPart}",
			_ => $"Apply {payload} equal to {referenceText}{suffixPart}"
		};
	}

	private static string FormatEqualToSignedPowerText(CardExtraEffectTarget target, bool gain, string powerName, string referenceText, string? suffix = null)
	{
		string title = PowerTitle(powerName, powerName);
		string payload = $"[gold]{title}[/gold]";
		string suffixPart = BuildSuffixPart(suffix);
		string selfVerb = gain
			? CardEditorLoc.T("cardText.word.gainSelf", "Gain")
			: CardEditorLoc.T("cardText.word.loseSelf", "Lose");
		string pluralVerb = gain
			? CardEditorLoc.T("cardText.word.gain", "gain")
			: CardEditorLoc.T("cardText.word.lose", "lose");
		string singularVerb = gain
			? CardEditorLoc.T("cardText.word.gains", "gains")
			: CardEditorLoc.T("cardText.word.loses", "loses");

		return target switch
		{
			CardExtraEffectTarget.AllEnemies => $"ALL enemies {pluralVerb} {payload} equal to {referenceText}{suffixPart}",
			CardExtraEffectTarget.RandomEnemy => $"A random enemy {singularVerb} {payload} equal to {referenceText}{suffixPart}",
			CardExtraEffectTarget.Self => $"{selfVerb} {payload} equal to {referenceText}{suffixPart}",
			_ => $"The target {singularVerb} {payload} equal to {referenceText}{suffixPart}"
		};
	}

	private static string FormatEqualToGainPowerText(CardExtraEffectTarget target, string powerName, string referenceText, string? suffix = null)
	{
		return FormatEqualToSignedPowerText(target, gain: true, powerName, referenceText, suffix);
	}

	private static string FormatEqualToLosePowerText(CardExtraEffectTarget target, string powerName, string referenceText, string? suffix = null)
	{
		return FormatEqualToSignedPowerText(target, gain: false, powerName, referenceText, suffix);
	}

	private static string FormatEqualToApplyPowerText(CardExtraEffect effect, string referenceText, string? suffix = null)
	{
		string title = ResolvePowerTitle(effect?.PowerId) ?? CardEditorLoc.T("cardText.power.unknown", "Unknown Power");
		string payload = $"[gold]{title}[/gold]";
		string suffixPart = BuildSuffixPart(suffix);
		return effect.Target switch
		{
			CardExtraEffectTarget.AllEnemies => $"Apply {payload} equal to {referenceText} to ALL enemies{suffixPart}",
			CardExtraEffectTarget.RandomEnemy => $"Apply {payload} equal to {referenceText} to a random enemy{suffixPart}",
			CardExtraEffectTarget.Self => $"Gain {payload} equal to {referenceText}{suffixPart}",
			_ => $"Apply {payload} equal to {referenceText}{suffixPart}"
		};
	}

	private static string FinalizeSpecialEffectSourceLine(
		CardModel card,
		Creature? target,
		CardExtraEffect effect,
		string line,
		int upgradeHighlightComparison,
		bool isUpgradePreview)
	{
		if (string.IsNullOrWhiteSpace(line))
		{
			return string.Empty;
		}

		string scaledLine = line;
		if (effect.Kind == CardExtraEffectKind.RunEffectSourceCard
			&& effect.ScaleMode != CardExtraEffectScaleMode.None)
		{
			scaledLine = effect.ScaleMode == CardExtraEffectScaleMode.PerHistoryCount && SupportsHistoryScaling(effect.Kind)
				? ApplyHistoryScalingSuffix(card, scaledLine, effect)
				: ApplyHistoryConditionPrefix(card, scaledLine.TrimEnd().TrimEnd('.'), effect);
		}

		string rendered = IsPowerEffect(effect)
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
		else if (effect.Trigger != CardExtraEffectTrigger.OnPlay)
		{
			rendered = ApplyTriggerPrefix(rendered, effect);
		}

		return ApplyConditionalBranchSuffix(card, target, rendered, effect, upgradeHighlightComparison, isUpgradePreview);
	}

	private static string? TryFormatLine(CardModel card, CardExtraEffect effect, Creature? target, int upgradeHighlightComparison, bool isUpgradePreview)
	{
	if (effect.Kind == CardExtraEffectKind.RunEffectSourceCard)
	{
		string? sourceLine = BuildEffectSourceReferenceLine(card, target, isUpgradePreview, effect);
		return string.IsNullOrWhiteSpace(sourceLine)
			? null
			: FinalizeSpecialEffectSourceLine(card, target, effect, sourceLine, upgradeHighlightComparison, isUpgradePreview);
	}

		if (effect.Kind == CardExtraEffectKind.ScalingStage)
		{
			string? stageLine = FormatScalingStageLine(card, target, effect, isUpgradePreview);
			return string.IsNullOrWhiteSpace(stageLine)
				? null
				: FinalizeSpecialEffectSourceLine(card, target, effect, stageLine, upgradeHighlightComparison, isUpgradePreview);
		}

		if (effect.Kind == CardExtraEffectKind.ChooseOneEffectSource)
		{
			string? chooseOneLine = FormatChooseOneEffectSource(card, target, isUpgradePreview, effect);
			return string.IsNullOrWhiteSpace(chooseOneLine)
				? null
				: FinalizeSpecialEffectSourceLine(card, target, effect, chooseOneLine, upgradeHighlightComparison, isUpgradePreview);
		}

		if (IsSelfScalingKind(effect.Kind))
		{
			return FormatSelfScalingLine(card, effect, isUpgradePreview);
		}

		bool usesAppliedEffectRowAmount = UsesAppliedEffectRowAmountSource(effect);
		bool usesValueSourceAmount = UsesValueSourceAmountSource(effect);
		bool usesPowerTriggerEventAmount = UsesPowerTriggerEventAmount(effect);
		bool amountIsX = effect.AmountIsX && !usesPowerTriggerEventAmount && !usesAppliedEffectRowAmount && !usesValueSourceAmount;
		int baseAmount = effect.Amount;
		if (usesAppliedEffectRowAmount || usesValueSourceAmount)
		{
			if ((!usesAppliedEffectRowAmount && !SupportsValueSourceAmountSource(effect.Kind))
				|| (!usesValueSourceAmount && !SupportsAppliedEffectRowAmountSource(effect.Kind))
				|| !IsValidEffectAmount(effect.Kind, Math.Max(1, baseAmount)))
			{
				return null;
			}
		}
		else if (!amountIsX && !IsValidEffectAmount(effect.Kind, baseAmount))
		{
			return null;
		}
		if (amountIsX && !IsValidEffectAmount(effect.Kind, 1))
		{
			return null;
		}

		int grammarAmount = usesAppliedEffectRowAmount || usesValueSourceAmount || usesPowerTriggerEventAmount
			? 2
			: amountIsX ? 2 : baseAmount;

		int historyMultiplier = 0;
		if (!usesAppliedEffectRowAmount
			&& !usesValueSourceAmount
			&& !amountIsX && effect.ScaleMode == CardExtraEffectScaleMode.PerHistoryCount
			&& SupportsHistoryScaling(effect.Kind)
			&& card.CombatState != null
			&& card.Owner?.Creature != null)
		{
			historyMultiplier = Math.Max(0, GetHistoryCountMultiplier(card.CombatState, card.Owner.Creature, cardPlay: null, effect, card));
		}

		string amountText = usesAppliedEffectRowAmount
			? GetAppliedAmountSourcePlaceholder(effect.Kind)
			: usesValueSourceAmount
				? GetAppliedAmountSourcePlaceholder(effect.Kind)
			: usesPowerTriggerEventAmount
			? GetPowerTriggerEventAmountText(effect)
			: amountIsX
				? FormatXPlusText(effect.AmountXPlus)
				: (effect.Kind == CardExtraEffectKind.CreatedCardsCostLess && baseAmount == -1)
					? "0"
					: baseAmount.ToString(CultureInfo.InvariantCulture);
		if (upgradeHighlightComparison != 0)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, upgradeHighlightComparison);
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

		string? directDynamicAmountLine = usesAppliedEffectRowAmount
			? TryFormatDirectDynamicAmountSourceLine(card, effect, ResolveAppliedAmountReferenceText(card, effect, isUpgradePreview), powerDurationSuffix)
			: usesValueSourceAmount
				? TryFormatDirectDynamicAmountSourceLine(card, effect, ResolveValueSourceReferenceText(effect), powerDurationSuffix)
				: null;
		bool usedDirectDynamicAmountLine = !string.IsNullOrWhiteSpace(directDynamicAmountLine);

		bool usesHistoryScalingWording = !usesAppliedEffectRowAmount
			&& !usesValueSourceAmount
			&& !amountIsX && effect.ScaleMode == CardExtraEffectScaleMode.PerHistoryCount
			&& SupportsHistoryScaling(effect.Kind)
			&& effect.Kind is CardExtraEffectKind.DealDamage or CardExtraEffectKind.GainBlock or CardExtraEffectKind.CardDealsExtraDamage;

		bool usesTwoLineHistoryScalingPreview = !usesAppliedEffectRowAmount
			&& !usesValueSourceAmount
			&& !amountIsX
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
				TryGetScaledAmountText(card, effect, baseAmount, target, upgradeHighlightComparison, out amountText);
			}

			string FormatEnergyText()
			{
				return usesPowerTriggerEventAmount
					? amountText + BuildEnergyIcons(card, 1)
					: (amountIsX || usesValueSourceAmount || forceNumericEnergyStars)
						? amountText + BuildEnergyIcons(card, 1)
						: BuildEnergyIcons(card, baseAmount);
			}

			string FormatStarText()
			{
				return usesPowerTriggerEventAmount
					? amountText + BuildStarIcons(1)
					: (amountIsX || usesValueSourceAmount || forceNumericEnergyStars)
						? amountText + BuildStarIcons(1)
						: BuildStarIcons(baseAmount);
			}

			return effect.Kind switch
			{
				CardExtraEffectKind.GainBlock => FormatGainBlock(effect.Target, amountText),
				CardExtraEffectKind.DealDamage => FormatDealDamage(effect.Target, amountText),
				CardExtraEffectKind.CardDealsExtraDamage => FormatCardDealsExtraDamage(amountText),
				CardExtraEffectKind.DrawCards => FormatDrawCards(effect, grammarAmount, amountText),
				CardExtraEffectKind.GainEnergy => CardEditorLoc.F("cardText.gainEnergy", $"Gain {FormatEnergyText()}.", ("Amount", FormatEnergyText())),
				CardExtraEffectKind.LoseEnergy => CardEditorLoc.F("cardText.loseEnergy", $"Lose {FormatEnergyText()}.", ("Amount", FormatEnergyText())),
				CardExtraEffectKind.GainStars => CardEditorLoc.F("cardText.gainStars", $"Gain {FormatStarText()}.", ("Amount", FormatStarText())),
				CardExtraEffectKind.LoseStars => CardEditorLoc.F("cardText.loseStars", $"Lose {FormatStarText()}.", ("Amount", FormatStarText())),
				CardExtraEffectKind.Heal => effect.Target switch
				{
					CardExtraEffectTarget.AllEnemies => CardEditorLoc.F("cardText.heal.allEnemies", $"ALL enemies heal {amountText} HP.", ("Amount", amountText)),
					CardExtraEffectTarget.RandomEnemy => CardEditorLoc.F("cardText.heal.randomEnemy", $"A random enemy heals {amountText} HP.", ("Amount", amountText)),
					CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.heal.self", $"Heal {amountText} HP.", ("Amount", amountText)),
					_ => CardEditorLoc.F("cardText.heal.target", $"The target heals {amountText} HP.", ("Amount", amountText))
				},
				CardExtraEffectKind.LoseHp => effect.Target switch
				{
					CardExtraEffectTarget.AllEnemies => CardEditorLoc.F("cardText.loseHp.allEnemies", $"ALL enemies lose {amountText} HP.", ("Amount", amountText)),
					CardExtraEffectTarget.RandomEnemy => CardEditorLoc.F("cardText.loseHp.randomEnemy", $"A random enemy loses {amountText} HP.", ("Amount", amountText)),
					CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.loseHp.self", $"Lose {amountText} HP.", ("Amount", amountText)),
					_ => CardEditorLoc.F("cardText.loseHp.target", $"The target loses {amountText} HP.", ("Amount", amountText))
				},
				CardExtraEffectKind.GainMaxHp => CardEditorLoc.F("cardText.gainMaxHp", $"Gain {amountText} Max HP.", ("Amount", amountText)),
				CardExtraEffectKind.LoseMaxHp => CardEditorLoc.F("cardText.loseMaxHp", $"Lose {amountText} Max HP.", ("Amount", amountText)),
				CardExtraEffectKind.GainStrength => FormatSignedPowerText(effect.Target, gain: true, amountText, "Strength", powerDurationSuffix),
				CardExtraEffectKind.LoseStrength => FormatSignedPowerText(effect.Target, gain: false, amountText, "Strength", powerDurationSuffix),
				CardExtraEffectKind.GainDexterity => FormatSignedPowerText(effect.Target, gain: true, amountText, "Dexterity", powerDurationSuffix),
				CardExtraEffectKind.LoseDexterity => FormatSignedPowerText(effect.Target, gain: false, amountText, "Dexterity", powerDurationSuffix),
				CardExtraEffectKind.GainFocus => FormatSignedPowerText(effect.Target, gain: true, amountText, "Focus", powerDurationSuffix),
				CardExtraEffectKind.LoseFocus => FormatSignedPowerText(effect.Target, gain: false, amountText, "Focus", powerDurationSuffix),
				CardExtraEffectKind.ApplyWeak => FormatApplyDebuffText(effect.Target, amountText, "Weak", powerDurationSuffix),
				CardExtraEffectKind.ApplyFrail => FormatApplyDebuffText(effect.Target, amountText, "Frail", powerDurationSuffix),
				CardExtraEffectKind.ApplyVulnerable => FormatApplyDebuffText(effect.Target, amountText, "Vulnerable", powerDurationSuffix),
				CardExtraEffectKind.ApplyPoison => FormatApplyDebuffText(effect.Target, amountText, "Poison", powerDurationSuffix),
				CardExtraEffectKind.ApplyDoom => amountIsX ? FormatApplyDebuffText(effect.Target, amountText, "Doom", powerDurationSuffix) : FormatApplyDebuff(effect.Target, baseAmount, "Doom", powerDurationSuffix),
				CardExtraEffectKind.RemoveWeak => FormatRemovePowerText(effect.Target, amountText, "Weak"),
				CardExtraEffectKind.RemoveFrail => FormatRemovePowerText(effect.Target, amountText, "Frail"),
				CardExtraEffectKind.RemoveVulnerable => FormatRemovePowerText(effect.Target, amountText, "Vulnerable"),
				CardExtraEffectKind.RemovePoison => FormatRemovePowerText(effect.Target, amountText, "Poison"),
				CardExtraEffectKind.RemoveDoom => FormatRemovePowerText(effect.Target, amountText, "Doom"),
				CardExtraEffectKind.RemoveConstrict => FormatRemovePowerText(effect.Target, amountText, "Constrict"),
				CardExtraEffectKind.ApplyPower => FormatApplyPower(effect, amountText),
				CardExtraEffectKind.GainStatusEqualToStatus => FormatStatusToStatus(effect, amountText),
				CardExtraEffectKind.GrantReplay => FormatGrantReplay(amountText),
				CardExtraEffectKind.GainArtifact => FormatGainPower(effect.Target, amountText, "Artifact", powerDurationSuffix),
				CardExtraEffectKind.GainThorns => FormatGainPower(effect.Target, amountText, "Thorns", powerDurationSuffix),
				CardExtraEffectKind.GainRegen => FormatGainPower(effect.Target, amountText, "Regen", powerDurationSuffix),
				CardExtraEffectKind.GainPlating => FormatGainPower(effect.Target, amountText, "Plating", powerDurationSuffix),
				CardExtraEffectKind.GainIntangible => FormatGainPower(effect.Target, amountText, "Intangible", powerDurationSuffix),
				CardExtraEffectKind.GainBuffer => FormatGainPower(effect.Target, amountText, "Buffer", powerDurationSuffix),
				CardExtraEffectKind.GainVigor => FormatGainPower(effect.Target, amountText, "Vigor", powerDurationSuffix),
				CardExtraEffectKind.GainBlur => FormatGainPower(effect.Target, amountText, "Blur", powerDurationSuffix),
				CardExtraEffectKind.GainRitual => FormatGainPower(effect.Target, amountText, "Ritual", powerDurationSuffix),
				CardExtraEffectKind.RemoveArtifact => FormatRemovePowerText(effect.Target, amountText, "Artifact"),
				CardExtraEffectKind.RemoveThorns => FormatRemovePowerText(effect.Target, amountText, "Thorns"),
				CardExtraEffectKind.RemoveRegen => FormatRemovePowerText(effect.Target, amountText, "Regen"),
				CardExtraEffectKind.RemovePlating => FormatRemovePowerText(effect.Target, amountText, "Plating"),
				CardExtraEffectKind.RemoveIntangible => FormatRemovePowerText(effect.Target, amountText, "Intangible"),
				CardExtraEffectKind.RemoveBuffer => FormatRemovePowerText(effect.Target, amountText, "Buffer"),
				CardExtraEffectKind.RemoveVigor => FormatRemovePowerText(effect.Target, amountText, "Vigor"),
				CardExtraEffectKind.RemoveBlur => FormatRemovePowerText(effect.Target, amountText, "Blur"),
				CardExtraEffectKind.RemoveRitual => FormatRemovePowerText(effect.Target, amountText, "Ritual"),
				CardExtraEffectKind.CleanseDebuffs => FormatCleansePowers(effect.Target, "debuffs"),
				CardExtraEffectKind.CleanseBuffs => FormatCleansePowers(effect.Target, "buffs"),
				CardExtraEffectKind.ApplyConstrict => FormatApplyDebuffText(effect.Target, amountText, "Constrict", powerDurationSuffix),
				CardExtraEffectKind.CreatedCardsCostLess => FormatCreatedCardsCostLess(card, effect, baseAmount, amountText, amountIsX, upgradeHighlightComparison),
				CardExtraEffectKind.CreatedCardsUpgraded => FormatCreatedCardsUpgraded(grammarAmount, amountText),
				CardExtraEffectKind.GeneratedCardsUpgraded => FormatGeneratedCardsUpgraded(effect, grammarAmount, amountText, cardCostsLessDurationSuffix),
				CardExtraEffectKind.CardsInPileUpgradedAura => FormatCardsInPileUpgradedAura(effect, grammarAmount, amountText, cardCostsLessDurationSuffix),
				CardExtraEffectKind.AddRandomCardToHand => FormatAddRandomCardToHand(effect, grammarAmount, amountText),
				CardExtraEffectKind.ChooseOneOfThreeCardsToHand => FormatChooseOneOfThreeToHand(effect, grammarAmount, amountText),
				CardExtraEffectKind.PlayRandomGeneratedCard => FormatPlayRandomGeneratedCard(effect, grammarAmount, amountText),
				CardExtraEffectKind.Summon => CardEditorLoc.F("cardText.summon", $"[gold]Summon[/gold] {amountText}.", ("Amount", amountText)),
				CardExtraEffectKind.Forge => CardEditorLoc.F("cardText.forge", $"[gold]Forge[/gold] {amountText}.", ("Amount", amountText)),
				CardExtraEffectKind.ChannelLightning => FormatChannelOrb(amountText, grammarAmount, "Lightning"),
				CardExtraEffectKind.ChannelFrost => FormatChannelOrb(amountText, grammarAmount, "Frost"),
				CardExtraEffectKind.ChannelDark => FormatChannelOrb(amountText, grammarAmount, "Dark"),
				CardExtraEffectKind.ChannelPlasma => FormatChannelOrb(amountText, grammarAmount, "Plasma"),
				CardExtraEffectKind.ChannelGlass => FormatChannelOrb(amountText, grammarAmount, "Glass"),
				CardExtraEffectKind.ChannelRandomOrb => FormatChannelRandomOrb(amountText, grammarAmount),
				CardExtraEffectKind.GainOrbSlots => grammarAmount == 1
					? CardEditorLoc.F("cardText.gainOrbSlots.one", $"Gain {amountText} Orb Slot.", ("Amount", amountText))
					: CardEditorLoc.F("cardText.gainOrbSlots.many", $"Gain {amountText} Orb Slots.", ("Amount", amountText)),
				CardExtraEffectKind.LoseOrbSlots => grammarAmount == 1
					? CardEditorLoc.F("cardText.loseOrbSlots.one", $"Lose {amountText} Orb Slot.", ("Amount", amountText))
					: CardEditorLoc.F("cardText.loseOrbSlots.many", $"Lose {amountText} Orb Slots.", ("Amount", amountText)),
				CardExtraEffectKind.EndTurn => CardEditorLoc.T("cardText.endTurn", "End your turn."),
				CardExtraEffectKind.EnchantCard => FormatEnchantCard(effect, amountText),
				CardExtraEffectKind.IgnoreBlock => CardEditorLoc.T("cardText.ignoreBlock", "This card's damage ignores Block."),
				CardExtraEffectKind.IgnoreDamageModifiers => CardEditorLoc.T("cardText.ignoreDamageModifiers", "This card's damage ignores damage modifiers."),
				CardExtraEffectKind.IgnoreDamageCaps => CardEditorLoc.T("cardText.ignoreDamageCaps", "This card's damage ignores damage caps."),
				CardExtraEffectKind.IgnoreDamageNegation => CardEditorLoc.T("cardText.ignoreDamageNegation", "This card's damage ignores damage negation."),
				CardExtraEffectKind.IgnoreEnemyDamageReductions => CardEditorLoc.T("cardText.ignoreEnemyDamageReductions", "This card's damage ignores enemy damage reduction effects."),
				CardExtraEffectKind.DoesNotConsumeVigor => effect.ResourceConsumptionMode switch
				{
					CardExtraEffectResourceConsumptionMode.SelfHpAndSelfDamage => CardEditorLoc.T("cardText.doesNotConsumeSelfHp", "This card does not consume your [gold]HP[/gold]."),
					CardExtraEffectResourceConsumptionMode.SpecificPowerStatus => CardEditorLoc.F(
						"cardText.doesNotConsumePowerStatus",
						"This card does not consume your {Resource}.",
						("Resource", $"[gold]{ResolvePowerTitle(effect?.PowerId) ?? CardEditorLoc.T("cardText.power.unknown", "Unknown Power")}[/gold]")),
					CardExtraEffectResourceConsumptionMode.SpecificStatStatus => CardEditorLoc.F(
						"cardText.doesNotConsumeStatStatus",
						"This card does not consume your {Resource}.",
					("Resource", GetMultiplierStatRichText(effect.ResourceConsumptionStat))),
				_ => CardEditorLoc.T("cardText.doesNotConsumeVigor", "This card does not consume [gold]Vigor[/gold].")
			},
				CardExtraEffectKind.HitsAllEnemies => CardEditorLoc.T("cardText.hitsAllEnemies", "This card hits ALL enemies."),
				CardExtraEffectKind.RemoveBlock => FormatRemoveBlock(effect.Target, amountText),
				CardExtraEffectKind.MultiplyStatStatus => FormatMultiplyStatStatus(effect, amountText),
				CardExtraEffectKind.CardTypeCostsLess => GetEffectiveCardCostsLessModifier(effect) != CardExtraEffectCostModifier.Reduce
					? FormatCardTypeCostModifier(card, effect, GetEffectiveCardCostsLessModifier(effect), cardCostsLessDurationSuffix, upgradeHighlightComparison)
					: amountIsX
						? FormatCardTypeCostDeltaText(effect, amountText + BuildEnergyIcons(card, 1), isLess: baseAmount > 0, cardCostsLessDurationSuffix, upgradeHighlightComparison)
						: FormatCardTypeCostDelta(card, effect, baseAmount, cardCostsLessDurationSuffix, upgradeHighlightComparison),
				CardExtraEffectKind.CardTypeStarCostsLess => GetEffectiveCardCostsLessModifier(effect) != CardExtraEffectCostModifier.Reduce
					? FormatCardTypeStarCostModifier(effect, GetEffectiveCardCostsLessModifier(effect), cardCostsLessDurationSuffix, upgradeHighlightComparison)
					: amountIsX
						? FormatCardTypeStarCostDeltaText(effect, amountText + BuildStarIcons(1), isLess: baseAmount > 0, cardCostsLessDurationSuffix, upgradeHighlightComparison)
						: FormatCardTypeStarCostDelta(effect, baseAmount, cardCostsLessDurationSuffix, upgradeHighlightComparison),
				CardExtraEffectKind.CardCostsLess => GetEffectiveCardCostsLessModifier(effect) != CardExtraEffectCostModifier.Reduce
					? FormatCardCostModifier(card, GetEffectiveCardCostsLessModifier(effect), cardCostsLessDurationSuffix, upgradeHighlightComparison)
					: amountIsX
						? FormatCardCostDeltaText(amountText + BuildEnergyIcons(card, 1), isLess: baseAmount > 0, cardCostsLessDurationSuffix, upgradeHighlightComparison)
						: FormatCardCostDelta(card, baseAmount, cardCostsLessDurationSuffix, upgradeHighlightComparison),
				CardExtraEffectKind.CardStarCostsLess => GetEffectiveCardCostsLessModifier(effect) != CardExtraEffectCostModifier.Reduce
					? FormatCardStarCostModifier(GetEffectiveCardCostsLessModifier(effect), cardCostsLessDurationSuffix, upgradeHighlightComparison)
					: amountIsX
						? FormatCardStarCostDeltaText(amountText + BuildStarIcons(1), isLess: baseAmount > 0, cardCostsLessDurationSuffix, upgradeHighlightComparison)
						: FormatCardStarCostDelta(baseAmount, cardCostsLessDurationSuffix, upgradeHighlightComparison),
				CardExtraEffectKind.DrawnCardsCostLess => GetEffectiveCardCostsLessModifier(effect) != CardExtraEffectCostModifier.Reduce
					? FormatDrawnCardsCostModifier(card, effect, GetEffectiveCardCostsLessModifier(effect), cardCostsLessDurationSuffix, upgradeHighlightComparison)
					: amountIsX
						? FormatDrawnCardsCostDeltaText(effect, amountText + BuildEnergyIcons(card, 1), isLess: baseAmount > 0, cardCostsLessDurationSuffix, upgradeHighlightComparison)
						: FormatDrawnCardsCostDelta(card, effect, baseAmount, cardCostsLessDurationSuffix, upgradeHighlightComparison),
				CardExtraEffectKind.GeneratedCardsCostLess => GetEffectiveCardCostsLessModifier(effect) != CardExtraEffectCostModifier.Reduce
					? FormatGeneratedCardsCostModifier(card, effect, GetEffectiveCardCostsLessModifier(effect), cardCostsLessDurationSuffix, upgradeHighlightComparison)
					: amountIsX
						? FormatGeneratedCardsCostDeltaText(effect, amountText + BuildEnergyIcons(card, 1), isLess: baseAmount > 0, cardCostsLessDurationSuffix, upgradeHighlightComparison)
						: FormatGeneratedCardsCostDelta(card, effect, baseAmount, cardCostsLessDurationSuffix, upgradeHighlightComparison),
				CardExtraEffectKind.DiscardCards => FormatDiscardCards(card, effect, grammarAmount, amountText),
				CardExtraEffectKind.ExhaustCards => FormatExhaustCards(card, effect, grammarAmount, amountText),
				CardExtraEffectKind.TransformCards => FormatTransformCards(card, effect, grammarAmount, amountText),
				CardExtraEffectKind.EvokeOrbs => FormatEvokeOrbs(grammarAmount, amountText),
				CardExtraEffectKind.OrbAction => FormatOrbAction(effect, grammarAmount, amountText),
				CardExtraEffectKind.OstyAction => FormatOstyAction(effect, grammarAmount, amountText),
				CardExtraEffectKind.MoveCardsBetweenPiles => FormatMoveCardsBetweenPiles(card, effect, grammarAmount, amountText),
				CardExtraEffectKind.UpgradeCardsInPile => FormatUpgradeCardsInPile(card, effect, grammarAmount, amountText, cardCostsLessDurationSuffix),
				CardExtraEffectKind.AddCopyOfThisCard => FormatAddCopyOfThisCard(effect, grammarAmount, amountText),
				CardExtraEffectKind.AddExactCopyOfThisCardToDeck => FormatAddExactCopyOfThisCardToDeck(grammarAmount, amountText),
				CardExtraEffectKind.CopyCardsFromPileToDeck => FormatCopyCardsFromPileToDeck(card, effect, grammarAmount, amountText),
				CardExtraEffectKind.CopyExactCardsFromPileToDeck => FormatCopyCardsFromPileToDeck(card, effect, grammarAmount, amountText, exact: true),
				CardExtraEffectKind.RemoveCardsFromDeck => FormatRemoveCardsFromDeck(card, effect, grammarAmount, amountText),
				CardExtraEffectKind.AddSpecificCardToHand => FormatAddSpecificCardToHand(effect, grammarAmount, amountText),
				CardExtraEffectKind.PlayCardFromPile => FormatPlayCardFromPile(card, effect, grammarAmount, amountText),
				CardExtraEffectKind.AutoPlaySelfFromPile => FormatAutoPlaySelfFromPile(effect),
				CardExtraEffectKind.DrawCardsThatCostLess => FormatDrawCardsThatCostLess(card, effect, grammarAmount, amountText, cardCostsLessDurationSuffix),
				CardExtraEffectKind.AutoDrawSelfFromPile => FormatAutoDrawSelfFromPile(effect),
				CardExtraEffectKind.ConditionalAutoPlayFromPile => FormatAutoPlaySelfFromPile(NormalizeSelfPileAutoEffect(effect) ?? effect),
				CardExtraEffectKind.ConditionalAutoDrawFromPile => FormatAutoDrawSelfFromPile(NormalizeSelfPileAutoEffect(effect) ?? effect),
				CardExtraEffectKind.GrantKeywordToPile => FormatGrantKeywordToPile(effect, grammarAmount, amountText),
				CardExtraEffectKind.GainGold => $"Gain {amountText} Gold.",
				CardExtraEffectKind.LoseGold => $"Lose {amountText} Gold.",
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
			TryGetScaledAmountText(card, effect, totalBaseAmount, target, upgradeHighlightComparison, out totalAmountText);

			string coefficientText = baseAmount.ToString(CultureInfo.InvariantCulture);
			if (upgradeHighlightComparison != 0)
			{
				coefficientText = StsTextUtilities.HighlightChangeText(coefficientText, upgradeHighlightComparison);
			}

			string definitionBase = effect.Kind switch
			{
				CardExtraEffectKind.DealDamage => CardEditorLoc.F("cardText.scaling.additionalDamage", $"Deal {coefficientText} bonus damage.", ("Amount", coefficientText)),
				CardExtraEffectKind.CardDealsExtraDamage => FormatCardDealsExtraDamage(coefficientText),
				_ => CardEditorLoc.F("cardText.scaling.additionalBlock", $"Gain {coefficientText} more [gold]Block[/gold].", ("Amount", coefficientText))
			};
			string definitionLine = ApplyHistoryScalingSuffix(card, definitionBase, effect);

			string baseLine = effect.Kind switch
			{
				CardExtraEffectKind.GainBlock => FormatGainBlock(effect.Target, totalAmountText),
				CardExtraEffectKind.CardDealsExtraDamage => FormatCardDealsExtraDamage(totalAmountText),
				_ => FormatDealDamage(effect.Target, totalAmountText)
			};

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

			int comparison = upgradeHighlightComparison != 0 ? upgradeHighlightComparison : totalAmount.CompareTo(baseAmount);
			string totalAmountText = StsTextUtilities.HighlightChangeText(totalAmount.ToString(CultureInfo.InvariantCulture), comparison);

			string? baseCoreLine = FormatLineForAmount(totalAmount, totalAmountText, totalAmount, allowDamageBlockPreview: false, forceNumericEnergyStars: true);
			string definitionLine = ApplyHistoryScalingSuffix(card, definitionCoreLine, effect);

			line = string.IsNullOrWhiteSpace(baseCoreLine) ? definitionLine : baseCoreLine + "\n" + definitionLine;
		}
		else if (usedDirectDynamicAmountLine)
		{
			line = directDynamicAmountLine;
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
		scaledLine = ApplyConditionalBonusSuffix(card, scaledLine, effect);
		if (usesAppliedEffectRowAmount && !usedDirectDynamicAmountLine)
		{
			scaledLine += " " + BuildAppliedAmountSourceSuffix(card, effect, isUpgradePreview);
		}
		else if (usesValueSourceAmount && !usedDirectDynamicAmountLine)
		{
			scaledLine += " " + BuildValueSourceAmountSuffix(effect);
		}

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
		else if (effect.Kind is CardExtraEffectKind.AutoPlaySelfFromPile
			or CardExtraEffectKind.AutoDrawSelfFromPile
			or CardExtraEffectKind.ConditionalAutoPlayFromPile
			or CardExtraEffectKind.ConditionalAutoDrawFromPile)
		{
			// These effect kinds already include their trigger wording (e.g. "At the start of your turn..."),
			// so applying another trigger prefix would duplicate it. Still allow conditional branch text.
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
			rendered = ApplyTriggerPrefix(rendered, effect);
		}

		rendered = ApplyConditionalBranchSuffix(card, target, rendered, effect, upgradeHighlightComparison, isUpgradePreview);
		return rendered;
	}

	private static void TryGetHistoryScalingTotalAmountText(CardModel card, CardExtraEffect effect, int baseAmount, Creature? target, int upgradeHighlightComparison, out string amountText)
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
			int comparison = upgradeHighlightComparison != 0 ? upgradeHighlightComparison : previewInt.CompareTo(baseAmount);
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
			CardExtraEffectTrigger.EndOfTurnInHand => CardEditorLoc.F("cardText.powerTrigger.prefix", $"{when}, {payload}", ("When", when), ("Payload", payload)),
			CardExtraEffectTrigger.StartOfTurn => CardEditorLoc.F("cardText.powerTrigger.prefix", $"{when}, {payload}", ("When", when), ("Payload", payload)),
			CardExtraEffectTrigger.EndOfTurn => CardEditorLoc.F("cardText.powerTrigger.prefix", $"{when}, {payload}", ("When", when), ("Payload", payload)),
			CardExtraEffectTrigger.StartOfEnemyTurn => CardEditorLoc.F("cardText.powerTrigger.prefix", $"{when}, {payload}", ("When", when), ("Payload", payload)),
			CardExtraEffectTrigger.EndOfEnemyTurn => CardEditorLoc.F("cardText.powerTrigger.prefix", $"{when}, {payload}", ("When", when), ("Payload", payload)),
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
		string phaseText = effect.TurnBoundary switch
		{
			CardExtraEffectTurnBoundary.Start => CardEditorLoc.T("cardText.turnBoundary.edge.startBeforeDraw", "start (before draw)"),
			CardExtraEffectTurnBoundary.StartAfterDraw => CardEditorLoc.T("cardText.turnBoundary.edge.startAfterDraw", "start (after draw)"),
			CardExtraEffectTurnBoundary.EndAfterDiscard => CardEditorLoc.T("cardText.turnBoundary.edge.endAfterDiscard", "end (after discard)"),
			_ => CardEditorLoc.T("cardText.turnBoundary.edge.endBeforeDiscard", "end (before discard)")
		};

		string sideText = effect.TurnBoundarySide switch
		{
			CardExtraEffectTurnBoundarySide.EnemyTurn => CardEditorLoc.T("cardText.turnBoundary.side.enemyTurn", "the enemy turn"),
			CardExtraEffectTurnBoundarySide.Both => CardEditorLoc.T("cardText.turnBoundary.side.eachTurn", "each turn"),
			_ => CardEditorLoc.T("cardText.turnBoundary.side.yourTurn", "your turn")
		};

		string locationText = effect.TurnBoundaryCardLocation switch
		{
			CardExtraEffectTurnBoundaryCardLocation.Hand => CardEditorLoc.T("cardText.turnBoundary.location.hand", " while this card is in your hand"),
			CardExtraEffectTurnBoundaryCardLocation.DrawPile => CardEditorLoc.T("cardText.turnBoundary.location.drawPile", " while this card is in your draw pile"),
			CardExtraEffectTurnBoundaryCardLocation.DiscardPile => CardEditorLoc.T("cardText.turnBoundary.location.discardPile", " while this card is in your discard pile"),
			CardExtraEffectTurnBoundaryCardLocation.ExhaustPile => CardEditorLoc.T("cardText.turnBoundary.location.exhaustPile", " while this card is in your exhaust pile"),
			_ => string.Empty
		};

		string fallback = $"At the {phaseText} of {sideText}{locationText}, {payload}";
		return CardEditorLoc.F("cardText.powerTrigger.turnBoundary", fallback,
			("Edge", phaseText),
			("Side", sideText),
			("Location", locationText),
			("Payload", payload));
	}

	private static string BuildPowerTriggerWhenClause(CardExtraEffect effect)
	{
		if (effect.Trigger is CardExtraEffectTrigger.StartOfTurn
			or CardExtraEffectTrigger.EndOfTurn
			or CardExtraEffectTrigger.EndOfTurnInHand
			or CardExtraEffectTrigger.StartOfEnemyTurn
			or CardExtraEffectTrigger.EndOfEnemyTurn)
		{
			return BuildPowerTurnTriggerWhenClause(effect);
		}

		string descriptor = BuildPowerTriggerCardDescriptor(effect);
		CardExtraEffectPowerTriggerFrom triggerFrom = GetEffectivePowerTriggerFrom(effect);
		bool usesActorSource = triggerFrom != CardExtraEffectPowerTriggerFrom.Self;
		string actorPhrase = BuildPowerTriggerActorPhrase(effect);
		int everyN = effect.TriggerEveryN;

		if (usesActorSource)
		{
			string action = BuildPowerTriggerActionPhrase(effect, descriptor, thirdPerson: true);
			if (everyN >= 2)
			{
				return CardEditorLoc.F(
					"cardText.powerTrigger.everyNActorAction",
					$"Every {everyN} times {actorPhrase} {action}",
					("N", everyN),
					("Actor", actorPhrase),
					("Action", action));
			}

			return CardEditorLoc.F(
				"cardText.powerTrigger.whenActorAction",
				$"Whenever {actorPhrase} {action}",
				("Actor", actorPhrase),
				("Action", action));
		}

		if (everyN >= 2)
		{
			string nStr = everyN.ToString(CultureInfo.InvariantCulture);
			return effect.Trigger switch
			{
				CardExtraEffectTrigger.OnPlay => string.IsNullOrEmpty(descriptor)
					? CardEditorLoc.F("cardText.powerTrigger.everyNPlayAny", $"Every time you play {nStr} cards", ("N", everyN))
					: CardEditorLoc.F("cardText.powerTrigger.everyNPlay", $"Every time you play {nStr} {descriptor} cards", ("N", everyN), ("Descriptor", descriptor)),
				CardExtraEffectTrigger.OnDraw => string.IsNullOrEmpty(descriptor)
					? CardEditorLoc.F("cardText.powerTrigger.everyNDrawAny", $"Every time you draw {nStr} cards", ("N", everyN))
					: CardEditorLoc.F("cardText.powerTrigger.everyNDraw", $"Every time you draw {nStr} {descriptor} cards", ("N", everyN), ("Descriptor", descriptor)),
				CardExtraEffectTrigger.OnDiscard => string.IsNullOrEmpty(descriptor)
					? CardEditorLoc.F("cardText.powerTrigger.everyNDiscardAny", $"Every time you discard {nStr} cards", ("N", everyN))
					: CardEditorLoc.F("cardText.powerTrigger.everyNDiscard", $"Every time you discard {nStr} {descriptor} cards", ("N", everyN), ("Descriptor", descriptor)),
				CardExtraEffectTrigger.OnExhaust => string.IsNullOrEmpty(descriptor)
					? CardEditorLoc.F("cardText.powerTrigger.everyNExhaustAny", $"Every time you exhaust {nStr} cards", ("N", everyN))
					: CardEditorLoc.F("cardText.powerTrigger.everyNExhaust", $"Every time you exhaust {nStr} {descriptor} cards", ("N", everyN), ("Descriptor", descriptor)),
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
			CardExtraEffectTrigger.Fatal => CardEditorLoc.T("cardText.powerTrigger.whenFatalAny", "Whenever this card deals a killing blow"),
			CardExtraEffectTrigger.OstyDealDamage => CardEditorLoc.T("cardText.powerTrigger.whenOstyDealDamage", "Whenever Osty attacks"),
			CardExtraEffectTrigger.AfterCombat => CardEditorLoc.T("cardText.powerTrigger.whenAfterCombat", "After winning combat"),
			CardExtraEffectTrigger.OnChannel => CardEditorLoc.T("cardText.powerTrigger.whenChannel", "Whenever you channel an orb"),
			CardExtraEffectTrigger.OnEvoke => CardEditorLoc.T("cardText.powerTrigger.whenEvoke", "Whenever you evoke an orb"),
			CardExtraEffectTrigger.OnCountEvent => CardEditorLoc.F(
				"cardText.powerTrigger.whenCountEvent",
				$"Whenever you {BuildPowerCountEventVerbPhrase(effect)}",
				("Event", BuildPowerCountEventVerbPhrase(effect))),
			_ => CardEditorLoc.T("cardText.powerTrigger.whenPlayAny", "Whenever you play a card")
		};
	}

	private static string BuildPowerTurnTriggerWhenClause(CardExtraEffect effect)
	{
		int everyN = Math.Max(1, effect?.TriggerEveryN ?? 1);
		string nStr = everyN.ToString(CultureInfo.InvariantCulture);

		return effect.Trigger switch
		{
			CardExtraEffectTrigger.StartOfTurn when everyN >= 2 => CardEditorLoc.F(
				"cardText.powerTrigger.everyNStartOfTurn",
				$"Every {nStr} turns at the start of your turn",
				("N", everyN)),
			CardExtraEffectTrigger.EndOfTurn when everyN >= 2 => CardEditorLoc.F(
				"cardText.powerTrigger.everyNEndOfTurn",
				$"Every {nStr} turns at the end of your turn",
				("N", everyN)),
			CardExtraEffectTrigger.EndOfTurnInHand when everyN >= 2 => CardEditorLoc.F(
				"cardText.powerTrigger.everyNEndOfTurnInHand",
				$"Every {nStr} turns at the end of your turn",
				("N", everyN)),
			CardExtraEffectTrigger.StartOfEnemyTurn when everyN >= 2 => CardEditorLoc.F(
				"cardText.powerTrigger.everyNStartOfEnemyTurn",
				$"Every {nStr} enemy turns at the start of the enemy turn",
				("N", everyN)),
			CardExtraEffectTrigger.EndOfEnemyTurn when everyN >= 2 => CardEditorLoc.F(
				"cardText.powerTrigger.everyNEndOfEnemyTurn",
				$"Every {nStr} enemy turns at the end of the enemy turn",
				("N", everyN)),
			CardExtraEffectTrigger.StartOfTurn => CardEditorLoc.T("cardText.powerTrigger.whenStartOfTurn", "At the start of your turn"),
			CardExtraEffectTrigger.EndOfTurn => CardEditorLoc.T("cardText.powerTrigger.whenEndOfTurn", "At the end of your turn"),
			CardExtraEffectTrigger.EndOfTurnInHand => CardEditorLoc.T("cardText.powerTrigger.whenEndOfTurnInHand", "At the end of your turn"),
			CardExtraEffectTrigger.StartOfEnemyTurn => CardEditorLoc.T("cardText.powerTrigger.whenStartOfEnemyTurn", "At the start of the enemy turn"),
			CardExtraEffectTrigger.EndOfEnemyTurn => CardEditorLoc.T("cardText.powerTrigger.whenEndOfEnemyTurn", "At the end of the enemy turn"),
			_ => CardEditorLoc.T("cardText.powerTrigger.whenPlayAny", "Whenever you play a card")
		};
	}

	private static string BuildPowerCountEventVerbPhrase(CardExtraEffect effect)
	{
		CardExtraEffectCountEvent ev = effect?.PowerTriggerCountEvent ?? CardExtraEffectCountEvent.BlockLost;
		if (CountEventUsesEnemyStatus(ev)
			&& TryGetStatusCountText(ev, effect?.PowerTriggerEnemyStatus ?? CardExtraEffectEnemyStatus.AnyPowerStatus, out string statusText, out string statusPresentVerb, out _, out _, effect?.PowerTriggerPowerId))
		{
			return $"{statusPresentVerb} {statusText}";
		}

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

	private static bool UsesPowerTriggerEventAmount(CardExtraEffect? effect)
	{
		return effect != null
			&& effect.AsPower
			&& effect.Trigger == CardExtraEffectTrigger.OnCountEvent
			&& effect.PowerTriggerUsesEventAmount;
	}

	private static string GetPowerTriggerEventAmountText(CardExtraEffect effect)
	{
		return UsesCountLikeEventAmountText(effect.Kind)
			? CardEditorLoc.T("cardText.powerTrigger.eventAmount.many", "that many")
			: CardEditorLoc.T("cardText.powerTrigger.eventAmount.much", "that much");
	}

	private static bool UsesCountLikeEventAmountText(CardExtraEffectKind kind)
	{
		return kind is CardExtraEffectKind.DrawCards
			or CardExtraEffectKind.GainEnergy
			or CardExtraEffectKind.LoseEnergy
			or CardExtraEffectKind.GainStars
			or CardExtraEffectKind.LoseStars
			or CardExtraEffectKind.CreatedCardsUpgraded
			or CardExtraEffectKind.AddRandomCardToHand
			or CardExtraEffectKind.ChooseOneOfThreeCardsToHand
			or CardExtraEffectKind.PlayRandomGeneratedCard
			or CardExtraEffectKind.Summon
			or CardExtraEffectKind.Forge
			or CardExtraEffectKind.ChannelLightning
			or CardExtraEffectKind.ChannelFrost
			or CardExtraEffectKind.ChannelDark
			or CardExtraEffectKind.ChannelPlasma
			or CardExtraEffectKind.ChannelGlass
			or CardExtraEffectKind.ChannelRandomOrb
			or CardExtraEffectKind.GainOrbSlots
			or CardExtraEffectKind.LoseOrbSlots
			or CardExtraEffectKind.DiscardCards
			or CardExtraEffectKind.ExhaustCards
			or CardExtraEffectKind.TransformCards
			or CardExtraEffectKind.EvokeOrbs
			or CardExtraEffectKind.OrbAction
			or CardExtraEffectKind.OstyAction
			or CardExtraEffectKind.MoveCardsBetweenPiles
			or CardExtraEffectKind.UpgradeCardsInPile
			or CardExtraEffectKind.AddCopyOfThisCard
			or CardExtraEffectKind.AddExactCopyOfThisCardToDeck
			or CardExtraEffectKind.CopyCardsFromPileToDeck
			or CardExtraEffectKind.CopyExactCardsFromPileToDeck
			or CardExtraEffectKind.RemoveCardsFromDeck
			or CardExtraEffectKind.AddSpecificCardToHand
			or CardExtraEffectKind.DrawCardsThatCostLess
			or CardExtraEffectKind.GrantKeywordToPile
			or CardExtraEffectKind.UpgradeDeckCards
			or CardExtraEffectKind.FetchSpecificCardToHand;
	}

	private static string BuildPowerTriggerActorPhrase(CardExtraEffect effect)
	{
		return GetEffectivePowerTriggerFrom(effect) switch
		{
			CardExtraEffectPowerTriggerFrom.AnyEnemy => CardEditorLoc.T("cardText.powerTrigger.actor.anyEnemy", "an enemy"),
			CardExtraEffectPowerTriggerFrom.AnyAlly => CardEditorLoc.T("cardText.powerTrigger.actor.anyAlly", "an ally"),
			CardExtraEffectPowerTriggerFrom.Anyone => CardEditorLoc.T("cardText.powerTrigger.actor.anyone", "anyone"),
			_ => CardEditorLoc.T("cardText.powerTrigger.actor.self", "you")
		};
	}

	private static string BuildPowerTriggerActionPhrase(CardExtraEffect effect, string descriptor, bool thirdPerson)
	{
		string PlayPhrase(string anyCard, string withDescriptor)
			=> string.IsNullOrEmpty(descriptor) ? anyCard : string.Format(CultureInfo.InvariantCulture, withDescriptor, descriptor);

		return effect.Trigger switch
		{
			CardExtraEffectTrigger.OnPlay => PlayPhrase(
				thirdPerson ? "plays a card" : "play a card",
				thirdPerson ? "plays {0}" : "play {0}"),
			CardExtraEffectTrigger.OnDraw => PlayPhrase(
				thirdPerson ? "draws a card" : "draw a card",
				thirdPerson ? "draws {0}" : "draw {0}"),
			CardExtraEffectTrigger.OnDiscard => PlayPhrase(
				thirdPerson ? "discards a card" : "discard a card",
				thirdPerson ? "discards {0}" : "discard {0}"),
			CardExtraEffectTrigger.OnExhaust => PlayPhrase(
				thirdPerson ? "exhausts a card" : "exhaust a card",
				thirdPerson ? "exhausts {0}" : "exhaust {0}"),
			CardExtraEffectTrigger.Fatal => thirdPerson ? "deals a killing blow" : "deal a killing blow",
			CardExtraEffectTrigger.OstyDealDamage => thirdPerson ? "has Osty attack" : "have Osty attack",
			CardExtraEffectTrigger.AfterCombat => thirdPerson ? "wins combat" : "win combat",
			CardExtraEffectTrigger.OnChannel => thirdPerson ? "channels an orb" : "channel an orb",
			CardExtraEffectTrigger.OnEvoke => thirdPerson ? "evokes an orb" : "evoke an orb",
			CardExtraEffectTrigger.OnCountEvent => BuildPowerCountEventVerbPhraseForSubject(effect, thirdPerson),
			_ => thirdPerson ? "plays a card" : "play a card"
		};
	}

	private static string BuildPowerCountEventVerbPhraseForSubject(CardExtraEffect effect, bool thirdPerson)
	{
		string phrase = BuildPowerCountEventVerbPhrase(effect);
		return thirdPerson ? ConjugatePresentVerbPhrase(phrase) : phrase;
	}

	private static string ConjugatePresentVerbPhrase(string phrase)
	{
		if (string.IsNullOrWhiteSpace(phrase))
		{
			return phrase;
		}

		if (phrase.StartsWith("Osty ", StringComparison.Ordinal))
		{
			return phrase;
		}

		string[] parts = phrase.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0)
		{
			return phrase;
		}

		string conjugated = ConjugateSimplePresentVerb(parts[0]);
		return parts.Length == 1 ? conjugated : $"{conjugated} {parts[1]}";
	}

	private static string ConjugateSimplePresentVerb(string verb)
	{
		if (string.IsNullOrWhiteSpace(verb))
		{
			return verb;
		}

		string lower = verb.ToLowerInvariant();
		string conjugatedLower = lower switch
		{
			"have" => "has",
			"do" => "does",
			"go" => "goes",
			"lose" => "loses",
			_ when lower.EndsWith("y", StringComparison.Ordinal) && lower.Length > 1 && !"aeiou".Contains(lower[^2]) => $"{lower[..^1]}ies",
			_ when lower.EndsWith("s", StringComparison.Ordinal)
				|| lower.EndsWith("sh", StringComparison.Ordinal)
				|| lower.EndsWith("ch", StringComparison.Ordinal)
				|| lower.EndsWith("x", StringComparison.Ordinal)
				|| lower.EndsWith("z", StringComparison.Ordinal)
				|| lower.EndsWith("o", StringComparison.Ordinal) => $"{lower}es",
			_ => $"{lower}s"
		};

		if (char.IsUpper(verb[0]))
		{
			return char.ToUpperInvariant(conjugatedLower[0]) + conjugatedLower[1..];
		}

		return conjugatedLower;
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
		string matchSuffix = BuildCountCardMatchSuffix(effect);

		if (poolAny && typeAny && filterAny && !effect.CostFilterEnabled)
		{
			return string.IsNullOrWhiteSpace(matchSuffix)
				? string.Empty
				: $"{CardEditorLoc.T("cardText.word.card", "card")}{matchSuffix}";
		}

		string poolText = pool switch
		{
			CardGeneratedCardPool.Default => CardEditorLoc.T("cardText.poolDescriptor.yourColor", "of your color"),
			CardGeneratedCardPool.OtherColors => CardEditorLoc.T("cardText.poolDescriptor.otherColors", "from other characters"),
			CardGeneratedCardPool.Any => CardEditorLoc.T("cardText.poolDescriptor.allColors", "from any character"),
			CardGeneratedCardPool.All => string.Empty,
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
			_ => CountCardFilterPrefixLabel(filter)
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

		if (!string.IsNullOrWhiteSpace(matchSuffix))
		{
			prefix = $"{prefix}{matchSuffix}";
		}

		if (filter == CardExtraEffectCountCardFilter.CreatesCards)
		{
			string outputDescriptor = BuildCreatesCardsOutputDescriptor(pool, type, effect, plural: true);
			string clause = string.IsNullOrWhiteSpace(outputDescriptor)
				? CardEditorLoc.T("cardText.countFilter.createsCards.singular", "that creates cards")
				: CardEditorLoc.F("cardText.countFilter.createsCards.output.singular", $"that creates {outputDescriptor}", ("Output", outputDescriptor));
			prefix = string.IsNullOrWhiteSpace(prefix)
				? $"{cardWord} {clause}"
				: $"{prefix} {clause}";
			return prefix.Trim();
		}

		prefix = BuildCostFilteredText(card: null, prefix, effect);

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

	private static string TrimTrailingSentencePunctuation(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return string.Empty;
		}

		return text.TrimEnd().TrimEnd('.', '!', '?').TrimEnd();
	}

	private static string UppercaseFirst(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}
		char first = text[0];
		if (!char.IsLower(first))
		{
			return text;
		}
		return char.ToUpperInvariant(first) + text.Substring(1);
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
			or CardExtraEffectKind.IgnoreEnemyDamageReductions
			or CardExtraEffectKind.HitsAllEnemies)
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
			string status = GetConfiguredStatusLabel(effect.CountEnemyStatus, effect.CountPowerId);
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

		if (TryGetStatusCountText(effect.CountEvent, effect.CountEnemyStatus, out string statusText, out _, out string pastStatusVerb, out _, effect.CountPowerId))
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

		if (effect.CountEvent == CardExtraEffectCountEvent.ThisCardDamageDealt)
		{
			string thisCardDamageWindowText = GetCountWindowText(effect);
			return CardEditorLoc.F(
				"cardText.thisCardDamageScalingSuffix",
				$"{trimmed} for each damage this card dealt {thisCardDamageWindowText}.",
				("Effect", trimmed),
				("Window", thisCardDamageWindowText));
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

		if (IsThisCardHistoryCountEvent(effect.CountEvent))
		{
			string thisCardVerb = GetThisCardHistoryPastVerb(effect.CountEvent);
			string thisCardWindowText = GetCountWindowText(effect);
			return CardEditorLoc.F(
				"cardText.thisCardHistoryScalingSuffix",
				$"{trimmed} for each time you {thisCardVerb} this card {thisCardWindowText}.",
				("Effect", trimmed),
				("Verb", thisCardVerb),
				("Window", thisCardWindowText));
		}

		if (UsesCountCardAggregateAmount(effect))
		{
			string metric = BuildCountAmountMetricLabel(effect, plural: true, includeOtherPrefix: effect.CountExcludeSourceCard);
			if (effect.CountEvent == CardExtraEffectCountEvent.InPile)
			{
				string where = GetCardPileLocation(effect.CountCardPile);
				return CardEditorLoc.F(
					"cardText.pileScalingSuffix.totalAmount",
					$"{trimmed} based on the total {metric} {where}.",
					("Effect", trimmed),
					("Metric", metric),
					("Where", where));
			}

			string amountVerbFallback = effect.CountEvent switch
			{
				CardExtraEffectCountEvent.Drawn => "drew",
				CardExtraEffectCountEvent.Discarded => "discarded",
				CardExtraEffectCountEvent.Exhausted => "exhausted",
				CardExtraEffectCountEvent.Generated => "created",
				_ => "played"
			};
			string amountVerb = CardEditorLoc.Enum("historyVerbPast", effect.CountEvent, amountVerbFallback);
			string amountWindowText = GetCountWindowText(effect);
			return CardEditorLoc.F(
				"cardText.historyScalingSuffix.totalAmount",
				$"{trimmed} based on the total {metric} you {amountVerb} {amountWindowText}.",
				("Effect", trimmed),
				("Metric", metric),
				("Verb", amountVerb),
				("Window", amountWindowText));
		}

		string descriptor = BuildCountCardDescriptor(effect, plural: false, includeOtherPrefix: effect.CountExcludeSourceCard);

		if (effect.CountEvent == CardExtraEffectCountEvent.InPile)
		{
			string where = GetCardPileLocation(effect.CountCardPile);
			return CardEditorLoc.F(
				"cardText.pileScalingSuffix.descriptor",
				$"{trimmed} for each {descriptor} {where}.",
				("Effect", trimmed),
				("Descriptor", descriptor),
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
			"cardText.historyScalingSuffix.descriptor",
			$"{trimmed} for each {descriptor} you've {verb} {windowText}.",
			("Effect", trimmed),
			("Descriptor", descriptor),
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
			string where = GetCardPileLocation(effect.CountCardPile);
			bool explicitComparison = effect.CountComparison != CardExtraEffectCountComparison.None;
			int threshold = Math.Max(0, effect.CountConditionAmount);
			string pileDescriptorPlural = BuildCountCardDescriptor(effect, plural: true, includeOtherPrefix: effect.CountExcludeSourceCard);
			if (explicitComparison)
			{
				return CardEditorLoc.F(
					"cardText.pileConditionCountPrefix.descriptor",
					$"If you have {FormatComparisonPhrase(effect.CountComparison, threshold)} {pileDescriptorPlural} {where}, {trimmedLineWithoutPeriod}.",
					("Comparison", FormatComparisonPhrase(effect.CountComparison, threshold)),
					("Descriptor", pileDescriptorPlural),
					("Where", where),
					("Effect", trimmedLineWithoutPeriod));
			}

			string reference = BuildCountCardReference(effect, useAnother: effect.CountExcludeSourceCard);
			return CardEditorLoc.F(
				"cardText.pileConditionPrefix.descriptor",
				$"If you have {reference} {where}, {trimmedLineWithoutPeriod}.",
				("Reference", reference),
				("Where", where),
				("Effect", trimmedLineWithoutPeriod));
		}

		bool explicitHistoryComparison = effect.CountComparison != CardExtraEffectCountComparison.None;
		int historyThreshold = Math.Max(0, effect.CountConditionAmount);
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

		string descriptorPlural = BuildCountCardDescriptor(effect, plural: true, includeOtherPrefix: effect.CountExcludeSourceCard);
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
				"cardText.historyConditionCountPrefix.descriptor",
				$"If you've {pastVerb} {FormatComparisonPhrase(effect.CountComparison, historyThreshold)} {descriptorPlural} {windowText}, {trimmedLineWithoutPeriod}.",
				("Verb", pastVerb),
				("Comparison", FormatComparisonPhrase(effect.CountComparison, historyThreshold)),
				("Descriptor", descriptorPlural),
				("Window", windowText),
				("Effect", trimmedLineWithoutPeriod));
		}

		string referenceHistory = BuildCountCardReference(effect, useAnother: effect.CountExcludeSourceCard);
		return CardEditorLoc.F(
			"cardText.historyConditionPrefix.descriptor",
			$"If you've {verb} {referenceHistory} {windowText}, {trimmedLineWithoutPeriod}.",
			("Verb", verb),
			("Reference", referenceHistory),
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
				? CardEditorLoc.Enum("historyVerbPerfect", effect.CountEvent, "channeled")
				: CardEditorLoc.Enum("historyVerbPerfect", effect.CountEvent, "evoked");
			string orbDescriptor = GetOrbCountDescriptor(effect.CountOrbType, plural: explicitComparison ? threshold != 1 : false);
			string windowText = GetCountWindowText(effect);
			if (!explicitComparison)
			{
				string article = effect.CountOrbType == CardExtraEffectOrbType.Any ? "an" : "a";
				return CardEditorLoc.F(
					"cardText.condition.orbHistorySingle",
					$"you've {verb} {article} {orbDescriptor} {windowText}",
					("Verb", verb),
					("OrbDescriptor", orbDescriptor),
					("Window", windowText));
			}

			return CardEditorLoc.F(
				"cardText.condition.orbHistoryCount",
				$"you've {verb} {FormatComparisonPhrase(effect.CountComparison, threshold)} {orbDescriptor} {windowText}",
				("Verb", verb),
				("Comparison", FormatComparisonPhrase(effect.CountComparison, threshold)),
				("OrbDescriptor", orbDescriptor),
				("Window", windowText));
		}

		if (effect.CountEvent == CardExtraEffectCountEvent.EnemyHasStatus)
		{
			string status = GetConfiguredStatusLabel(effect.CountEnemyStatus, effect.CountPowerId);
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

		if (TryGetStatusCountText(effect.CountEvent, effect.CountEnemyStatus, out string statusText, out string presentStatusVerb, out _, out string perfectStatusVerb, effect.CountPowerId))
		{
			string statusWindowText = GetCountWindowText(effect);
			if (!explicitComparison)
			{
				return CardEditorLoc.F(
					"cardText.condition.statusHistorySingle",
					$"you've {perfectStatusVerb} {statusText} {statusWindowText}",
					("Verb", perfectStatusVerb),
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
			if (effect.CountEvent == CardExtraEffectCountEvent.ThisCardDamageDealt)
			{
				string thisCardDamageWindowText = GetCountWindowText(effect);
				if (!explicitComparison)
				{
					return CardEditorLoc.F(
						"cardText.condition.thisCardDamageSingle",
						$"this card has dealt damage {thisCardDamageWindowText}",
						("Window", thisCardDamageWindowText));
				}

				return CardEditorLoc.F(
					"cardText.condition.thisCardDamageCount",
					$"this card has dealt {FormatComparisonPhrase(effect.CountComparison, threshold)} {pluralResource} {thisCardDamageWindowText}",
					("Comparison", FormatComparisonPhrase(effect.CountComparison, threshold)),
					("Resource", pluralResource),
					("Window", thisCardDamageWindowText));
			}

			string resourceWindowText = GetCountWindowText(effect);
			if (!explicitComparison)
			{
				return CardEditorLoc.F(
					"cardText.condition.resourceHistorySingle",
					$"you've {perfectResourceVerb} {singularResource} {resourceWindowText}",
					("Verb", perfectResourceVerb),
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

		if (IsThisCardHistoryCountEvent(effect.CountEvent))
		{
			string windowText = GetCountWindowText(effect);
			string presentVerb = GetThisCardHistoryPresentVerb(effect.CountEvent);
			string perfectVerb = GetThisCardHistoryPerfectVerb(effect.CountEvent);
			if (!explicitComparison)
			{
				return CardEditorLoc.F(
					"cardText.condition.thisCardHistorySingle",
					$"you've {perfectVerb} this card {windowText}",
					("Verb", perfectVerb),
					("Window", windowText));
			}

			return CardEditorLoc.F(
				"cardText.condition.thisCardHistoryCount",
				$"you've {perfectVerb} this card {FormatComparisonPhrase(effect.CountComparison, threshold)} times {windowText}",
				("Verb", perfectVerb),
				("Comparison", FormatComparisonPhrase(effect.CountComparison, threshold)),
				("Window", windowText));
		}

		if (UsesCountCardAggregateAmount(effect))
		{
			string metric = BuildCountAmountMetricLabel(effect, plural: true, includeOtherPrefix: effect.CountExcludeSourceCard);
			string comparisonPhrase = explicitComparison
				? FormatComparisonPhrase(effect.CountComparison, threshold)
				: CardEditorLoc.T("cardText.condition.totalAmount.any", "greater than 0");

			if (effect.CountEvent == CardExtraEffectCountEvent.InPile)
			{
				string where = GetCardPileLocation(effect.CountCardPile);
				return CardEditorLoc.F(
					"cardText.condition.totalAmountInPile",
					$"the total {metric} {where} is {comparisonPhrase}",
					("Metric", metric),
					("Where", where),
					("Comparison", comparisonPhrase));
			}

			string pastVerbFallback = effect.CountEvent switch
			{
				CardExtraEffectCountEvent.Drawn => "drawn",
				CardExtraEffectCountEvent.Discarded => "discarded",
				CardExtraEffectCountEvent.Exhausted => "exhausted",
				CardExtraEffectCountEvent.Generated => "created",
				_ => "played"
			};
			string perfectVerb = CardEditorLoc.Enum("historyVerbPerfect", effect.CountEvent, pastVerbFallback);
			string windowText = GetCountWindowText(effect);
			return CardEditorLoc.F(
				"cardText.condition.totalAmountHistory",
				$"the total {metric} you've {perfectVerb} {windowText} is {comparisonPhrase}",
				("Metric", metric),
				("Verb", perfectVerb),
				("Window", windowText),
				("Comparison", comparisonPhrase));
		}

		if (effect.CountEvent == CardExtraEffectCountEvent.InPile)
		{
			string where = GetCardPileLocation(effect.CountCardPile);
			string descriptorPlural = BuildCountCardDescriptor(effect, plural: true, includeOtherPrefix: effect.CountExcludeSourceCard);
			if (explicitComparison)
			{
				return CardEditorLoc.F(
					"cardText.condition.pileCountGeneric",
					$"you have {FormatComparisonPhrase(effect.CountComparison, threshold)} {descriptorPlural} {where}",
					("Comparison", FormatComparisonPhrase(effect.CountComparison, threshold)),
					("Descriptor", descriptorPlural),
					("Where", where));
			}

			string reference = BuildCountCardReference(effect, useAnother: effect.CountExcludeSourceCard);
			return CardEditorLoc.F(
				"cardText.condition.pileGeneric",
				$"you have {reference} {where}",
				("Reference", reference),
				("Where", where));
		}

		string verbFallback = effect.CountEvent switch
		{
			CardExtraEffectCountEvent.Drawn => "drawn",
			CardExtraEffectCountEvent.Discarded => "discarded",
			CardExtraEffectCountEvent.Exhausted => "exhausted",
			CardExtraEffectCountEvent.Generated => "created",
			_ => "played"
		};
		string historyVerb = CardEditorLoc.Enum("historyVerbPerfect", effect.CountEvent, verbFallback);
		string windowTextGeneric = GetCountWindowText(effect);
		string descriptorPluralGeneric = BuildCountCardDescriptor(effect, plural: true, includeOtherPrefix: effect.CountExcludeSourceCard);
		if (explicitComparison)
		{
			string pastVerbFallback = effect.CountEvent switch
			{
				CardExtraEffectCountEvent.Drawn => "drawn",
				CardExtraEffectCountEvent.Discarded => "discarded",
				CardExtraEffectCountEvent.Exhausted => "exhausted",
				CardExtraEffectCountEvent.Generated => "created",
				_ => "played"
			};
			string perfectVerb = CardEditorLoc.Enum("historyVerbPerfect", effect.CountEvent, pastVerbFallback);
			return CardEditorLoc.F(
				"cardText.condition.historyGenericCount",
				$"you've {perfectVerb} {FormatComparisonPhrase(effect.CountComparison, threshold)} {descriptorPluralGeneric} {windowTextGeneric}",
				("Verb", perfectVerb),
				("Comparison", FormatComparisonPhrase(effect.CountComparison, threshold)),
				("Descriptor", descriptorPluralGeneric),
				("Window", windowTextGeneric));
		}

		string referenceGeneric = BuildCountCardReference(effect, useAnother: effect.CountExcludeSourceCard);
		return CardEditorLoc.F(
			"cardText.condition.historyGeneric",
			$"you've {historyVerb} {referenceGeneric} {windowTextGeneric}",
			("Verb", historyVerb),
			("Reference", referenceGeneric),
			("Window", windowTextGeneric));
	}

	private static string GetThisCardHistoryPastVerb(CardExtraEffectCountEvent ev)
	{
		return ev switch
		{
			CardExtraEffectCountEvent.ThisCardDrawn => CardEditorLoc.T("cardText.thisCard.verbPast.drawn", "drew"),
			CardExtraEffectCountEvent.ThisCardDiscarded => CardEditorLoc.T("cardText.thisCard.verbPast.discarded", "discarded"),
			CardExtraEffectCountEvent.ThisCardExhausted => CardEditorLoc.T("cardText.thisCard.verbPast.exhausted", "exhausted"),
			_ => CardEditorLoc.T("cardText.thisCard.verbPast.played", "played")
		};
	}

	private static string GetThisCardHistoryPresentVerb(CardExtraEffectCountEvent ev)
	{
		return ev switch
		{
			CardExtraEffectCountEvent.ThisCardDrawn => CardEditorLoc.T("cardText.thisCard.verbPresent.drawn", "draw"),
			CardExtraEffectCountEvent.ThisCardDiscarded => CardEditorLoc.T("cardText.thisCard.verbPresent.discarded", "discard"),
			CardExtraEffectCountEvent.ThisCardExhausted => CardEditorLoc.T("cardText.thisCard.verbPresent.exhausted", "exhaust"),
			_ => CardEditorLoc.T("cardText.thisCard.verbPresent.played", "play")
		};
	}

	private static string GetThisCardHistoryPerfectVerb(CardExtraEffectCountEvent ev)
	{
		return ev switch
		{
			CardExtraEffectCountEvent.ThisCardDrawn => CardEditorLoc.T("cardText.thisCard.verbPerfect.drawn", "drawn"),
			CardExtraEffectCountEvent.ThisCardDiscarded => CardEditorLoc.T("cardText.thisCard.verbPerfect.discarded", "discarded"),
			CardExtraEffectCountEvent.ThisCardExhausted => CardEditorLoc.T("cardText.thisCard.verbPerfect.exhausted", "exhausted"),
			_ => CardEditorLoc.T("cardText.thisCard.verbPerfect.played", "played")
		};
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

	internal static bool PowerMatchesStatus(PowerModel power, CardExtraEffectEnemyStatus status)
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

	internal static bool PowerMatchesConfiguredStatus(PowerModel? power, CardExtraEffectEnemyStatus status, string? powerId)
	{
		if (power == null)
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(powerId))
		{
			if (!TryResolveConfiguredPowerModel(powerId, out PowerModel? canonical) || canonical?.Id == null || canonical.Id == ModelId.none)
			{
				return false;
			}

			return power.Id != null && power.Id == canonical.Id;
		}

		return PowerMatchesStatus(power, status);
	}

	private static bool CreatureHasStatus(Creature creature, CardExtraEffectEnemyStatus status)
	{
		if (creature == null || !creature.IsAlive)
		{
			return false;
		}

		if (status is CardExtraEffectEnemyStatus.AnyPowerStatus or CardExtraEffectEnemyStatus.Buff or CardExtraEffectEnemyStatus.Debuff)
		{
			return creature.Powers.Any(power => power != null && power.Amount > 0 && PowerMatchesStatus(power, status));
		}

		return status switch
		{
			CardExtraEffectEnemyStatus.Weak => creature.GetPowerAmount<WeakPower>() > 0,
			CardExtraEffectEnemyStatus.Frail => creature.GetPowerAmount<FrailPower>() > 0,
			CardExtraEffectEnemyStatus.Vulnerable => creature.GetPowerAmount<VulnerablePower>() > 0,
			CardExtraEffectEnemyStatus.Poison => creature.GetPowerAmount<PoisonPower>() > 0,
			CardExtraEffectEnemyStatus.Doom => creature.GetPowerAmount<DoomPower>() > 0,
			CardExtraEffectEnemyStatus.Constrict => creature.GetPowerAmount<ConstrictPower>() > 0,
			CardExtraEffectEnemyStatus.Artifact => creature.GetPowerAmount<ArtifactPower>() > 0,
			CardExtraEffectEnemyStatus.Thorns => creature.GetPowerAmount<ThornsPower>() > 0,
			CardExtraEffectEnemyStatus.Regen => creature.GetPowerAmount<RegenPower>() > 0,
			CardExtraEffectEnemyStatus.Plating => creature.GetPowerAmount<PlatingPower>() > 0,
			CardExtraEffectEnemyStatus.Intangible => creature.GetPowerAmount<IntangiblePower>() > 0,
			CardExtraEffectEnemyStatus.Buffer => creature.GetPowerAmount<BufferPower>() > 0,
			CardExtraEffectEnemyStatus.Vigor => creature.GetPowerAmount<VigorPower>() > 0,
			CardExtraEffectEnemyStatus.Blur => creature.GetPowerAmount<BlurPower>() > 0,
			CardExtraEffectEnemyStatus.Ritual => creature.GetPowerAmount<RitualPower>() > 0,
			CardExtraEffectEnemyStatus.Strength => creature.GetPowerAmount<StrengthPower>() > 0,
			CardExtraEffectEnemyStatus.Dexterity => creature.GetPowerAmount<DexterityPower>() > 0,
			CardExtraEffectEnemyStatus.Focus => creature.GetPowerAmount<FocusPower>() > 0,
			_ => false
		};
	}

	private static bool EnemyHasStatus(Creature enemy, CardExtraEffectEnemyStatus status)
	{
		if (enemy == null || !enemy.IsEnemy || !enemy.IsAlive)
		{
			return false;
		}

		return CreatureHasStatus(enemy, status);
	}

	private static bool CreatureHasConfiguredStatus(Creature creature, CardExtraEffectEnemyStatus status, string? powerId)
	{
		if (!string.IsNullOrWhiteSpace(powerId))
		{
			return GetSpecificPowerValueSourceAmount(creature, powerId) > 0;
		}

		return CreatureHasStatus(creature, status);
	}

	private static bool EnemyHasConfiguredStatus(Creature enemy, CardExtraEffectEnemyStatus status, string? powerId)
	{
		if (enemy == null || !enemy.IsEnemy || !enemy.IsAlive)
		{
			return false;
		}

		return CreatureHasConfiguredStatus(enemy, status, powerId);
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
		if (GetEffectiveCountCardFilter(effect) == CardExtraEffectCountCardFilter.CreatesCards)
		{
			return string.Empty;
		}

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
			CardGeneratedCardType.Playable => "[gold]" + GetPlayableKeywordTitle() + "[/gold] ",
			CardGeneratedCardType.Status => GeneratedCardTypeLabel(CardGeneratedCardType.Status) + " ",
			CardGeneratedCardType.Curse => GeneratedCardTypeLabel(CardGeneratedCardType.Curse) + " ",
			CardGeneratedCardType.Quest => GeneratedCardTypeLabel(CardGeneratedCardType.Quest) + " ",
			_ => string.Empty
		};

		CardExtraEffectCountCardFilter filter = GetEffectiveCountCardFilter(effect);

		string effectAdj = filter == CardExtraEffectCountCardFilter.Any ? string.Empty : CountCardFilterPrefixLabel(filter) + " ";

		string prefix = (effectAdj + poolAdj + typeAdj).Trim();
		return string.IsNullOrEmpty(prefix) ? string.Empty : prefix + " ";
	}

	private static string BuildCreatesCardsOutputDescriptor(CardGeneratedCardPool pool, CardGeneratedCardType type, CardExtraEffect effect, bool plural)
	{
		string cardWord = CardEditorLoc.T(plural ? "cardText.cards" : "cardText.word.card", plural ? "cards" : "card");
		string? poolPrefix = GetCardPoolQualifierPrefix(pool, plural);
		string? poolSuffix = GetCardPoolQualifierSuffix(pool, plural);
		string? typePrefix = type switch
		{
			CardGeneratedCardType.Attack => GeneratedCardTypeLabel(CardGeneratedCardType.Attack),
			CardGeneratedCardType.Skill => GeneratedCardTypeLabel(CardGeneratedCardType.Skill),
			CardGeneratedCardType.Power => GeneratedCardTypeLabel(CardGeneratedCardType.Power),
			CardGeneratedCardType.Playable => "[gold]" + GetPlayableKeywordTitle() + "[/gold]",
			CardGeneratedCardType.Status => GeneratedCardTypeLabel(CardGeneratedCardType.Status),
			CardGeneratedCardType.Curse => GeneratedCardTypeLabel(CardGeneratedCardType.Curse),
			CardGeneratedCardType.Quest => GeneratedCardTypeLabel(CardGeneratedCardType.Quest),
			_ => null
		};

		string descriptor = string.Join(" ", new[] { poolPrefix, typePrefix, cardWord }.Where(s => !string.IsNullOrWhiteSpace(s)));
		if (!string.IsNullOrWhiteSpace(poolSuffix))
		{
			descriptor = string.Join(" ", new[] { descriptor, poolSuffix }.Where(s => !string.IsNullOrWhiteSpace(s)));
		}

		return BuildCostFilteredText(card: null, descriptor.Trim(), effect).Trim();
	}

private static string BuildCountAmountMetricLabel(CardExtraEffect effect, bool plural, bool includeOtherPrefix)
{
	CardExtraEffectCountAggregationMode aggregationMode = GetEffectiveCountAggregationMode(effect);
	string descriptor = BuildCountCardDescriptor(effect, plural, includeOtherPrefix).Trim();

	string AttachDescriptor(string metric)
	{
		return string.IsNullOrWhiteSpace(descriptor)
			? metric
			: $"{metric} of {descriptor}";
	}

	if (aggregationMode == CardExtraEffectCountAggregationMode.CurrentEnergyCost)
	{
		return AttachDescriptor(CardEditorLoc.T("cardText.countAggregationMetric.currentEnergyCost", "Energy cost"));
	}

	if (aggregationMode == CardExtraEffectCountAggregationMode.BaseEnergyCost)
	{
		return AttachDescriptor(CardEditorLoc.T("cardText.countAggregationMetric.baseEnergyCost", "base Energy cost"));
	}

	if (aggregationMode == CardExtraEffectCountAggregationMode.CurrentStarCost)
	{
		return AttachDescriptor(CardEditorLoc.T("cardText.countAggregationMetric.currentStarCost", "Star cost"));
	}

	if (aggregationMode == CardExtraEffectCountAggregationMode.BaseStarCost)
	{
		return AttachDescriptor(CardEditorLoc.T("cardText.countAggregationMetric.baseStarCost", "base Star cost"));
	}

	CardExtraEffectCountCardFilter filter = GetEffectiveCountCardFilter(effect);
	string filterPrefix = CountCardFilterPrefixLabel(filter).Trim();
	if (!string.IsNullOrWhiteSpace(filterPrefix)
		&& descriptor.StartsWith(filterPrefix + " ", StringComparison.Ordinal))
	{
			descriptor = descriptor.Substring(filterPrefix.Length + 1).Trim();
		}

		string amountLabel = CountCardFilterAmountLabel(filter);
		return string.IsNullOrWhiteSpace(descriptor)
			? amountLabel
			: $"{amountLabel} on {descriptor}";
	}

	private static string BuildCountExcludeSourcePrefix(CardExtraEffect effect)
	{
		return effect != null && effect.CountExcludeSourceCard
			? CardEditorLoc.T("cardText.count.otherPrefix", "other ")
			: string.Empty;
	}

	private static string BuildCountCardDescriptor(CardExtraEffect effect, bool plural, bool includeOtherPrefix)
	{
		string cardWord = CardEditorLoc.T(plural ? "cardText.cards" : "cardText.word.card", plural ? "cards" : "card");
		string? otherPrefix = includeOtherPrefix ? BuildCountExcludeSourcePrefix(effect).Trim() : null;
		CardExtraEffectCountCardFilter filter = GetEffectiveCountCardFilter(effect);
		string filterPrefix = BuildCountCardFilter(effect).Trim();
		string matchSuffix = BuildCountCardMatchSuffix(effect).Trim();
		string poolSuffix = filter == CardExtraEffectCountCardFilter.CreatesCards
			? string.Empty
			: BuildCountScalingPoolSuffix(effect.CountCardPool).Trim();

		string descriptor = string.Join(" ", new[] { otherPrefix, filterPrefix, cardWord }.Where(s => !string.IsNullOrWhiteSpace(s)));
		if (!string.IsNullOrWhiteSpace(matchSuffix))
		{
			descriptor += " " + matchSuffix;
		}
		if (!string.IsNullOrWhiteSpace(poolSuffix))
		{
			descriptor += " " + poolSuffix;
		}

		if (filter == CardExtraEffectCountCardFilter.CreatesCards)
		{
			string outputDescriptor = BuildCreatesCardsOutputDescriptor(effect.CountCardPool, effect.CountCardType, effect, plural: true);
			string clause = string.IsNullOrWhiteSpace(outputDescriptor)
				? CardEditorLoc.T(
					plural ? "cardText.countFilter.createsCards.plural" : "cardText.countFilter.createsCards.singular",
					plural ? "that create cards" : "that creates cards")
				: CardEditorLoc.F(
					plural ? "cardText.countFilter.createsCards.output.plural" : "cardText.countFilter.createsCards.output.singular",
					plural ? $"that create {outputDescriptor}" : $"that creates {outputDescriptor}",
					("Output", outputDescriptor));
			descriptor = string.IsNullOrWhiteSpace(descriptor)
				? $"{cardWord} {clause}"
				: $"{descriptor} {clause}";
			return descriptor.Trim();
		}

		return BuildCostFilteredText(card: null, descriptor.Trim(), effect);
	}

	private static string StripBbCodeTags(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return string.Empty;
		}

		StringBuilder sb = new StringBuilder(text.Length);
		bool insideTag = false;
		foreach (char ch in text)
		{
			if (ch == '[')
			{
				insideTag = true;
				continue;
			}

			if (ch == ']')
			{
				insideTag = false;
				continue;
			}

			if (!insideTag)
			{
				sb.Append(ch);
			}
		}

		return sb.ToString();
	}

	private static string BuildCountCardReference(CardExtraEffect effect, bool useAnother)
	{
		string descriptor = BuildCountCardDescriptor(effect, plural: false, includeOtherPrefix: false);
		if (useAnother)
		{
			return CardEditorLoc.F("cardText.count.reference.another", $"another {descriptor}", ("Descriptor", descriptor));
		}

		string stripped = StripBbCodeTags(descriptor).TrimStart();
		char first = stripped.Length > 0 ? char.ToLowerInvariant(stripped[0]) : 'a';
		string article = "aeiou".IndexOf(first) >= 0 ? "an " : "a ";
		return CardEditorLoc.F("cardText.count.reference.one", $"{article}{descriptor}", ("Article", article), ("Descriptor", descriptor));
	}

	private static string BuildCountCardMatchSuffix(CardExtraEffect effect)
	{
		if (effect == null || effect.CardMatchMode == CardExtraEffectCardMatchMode.Any)
		{
			return string.Empty;
		}

		if (effect.CardMatchMode == CardExtraEffectCardMatchMode.CardId)
		{
			string idStr = effect.MatchCardId?.Trim() ?? string.Empty;
			if (string.IsNullOrWhiteSpace(idStr))
			{
				return string.Empty;
			}

			string cardName = BuildSpecificCardReferenceText(idStr, GetCardReferenceDisplayMode(effect));
			return CardEditorLoc.F("cardText.countMatch.cardText", $" matching {cardName}", ("Card", cardName));
		}

		if (effect.CardMatchMode == CardExtraEffectCardMatchMode.Tag)
		{
			if (effect.MatchTagKind == CardExtraEffectCardMatchTagKind.Custom)
			{
				string tag = effect.MatchCustomTag?.Trim() ?? string.Empty;
				if (string.IsNullOrWhiteSpace(tag))
				{
					return string.Empty;
				}

				return CardEditorLoc.F("cardText.countMatch.customTag", $" with tag {tag}", ("Tag", tag));
			}

			CardTag vanillaTag = effect.MatchVanillaTag;
			if (vanillaTag == CardTag.None)
			{
				return string.Empty;
			}

			string tagLabel = CardEditorLoc.Enum("cardTag", vanillaTag, vanillaTag.ToString());
			return CardEditorLoc.F("cardText.countMatch.vanillaTag", $" with {tagLabel}", ("Tag", tagLabel));
		}

		if (effect.CardMatchMode == CardExtraEffectCardMatchMode.CustomKeyword)
		{
			string keyword = effect.MatchCustomKeyword?.Trim() ?? string.Empty;
			if (string.IsNullOrWhiteSpace(keyword))
			{
				return string.Empty;
			}

			return CardEditorLoc.F("cardText.countMatch.customKeyword", $" with keyword {keyword}", ("Keyword", keyword));
		}

		if (effect.CardMatchMode == CardExtraEffectCardMatchMode.NameContains)
		{
			string text = effect.MatchCardId?.Trim() ?? string.Empty;
			if (string.IsNullOrWhiteSpace(text))
			{
				return string.Empty;
			}

			return CardEditorLoc.F("cardText.countMatch.nameContains", $" with \"{text}\" in its name", ("Text", text));
		}

		return string.Empty;
	}

	private static string ApplyTriggerPrefix(string line, CardExtraEffect effect)
	{
		CardExtraEffectTrigger trigger = effect?.Trigger ?? CardExtraEffectTrigger.OnPlay;
		string fallback = trigger switch
		{
			// For most effects, "On Play" is just the default card text behavior (i.e. what happens when you play this card),
			// so adding a "When played:" header is redundant and reads awkwardly compared to vanilla.
			CardExtraEffectTrigger.OnPlay => string.Empty,
			CardExtraEffectTrigger.OnDraw => "When drawn: ",
			CardExtraEffectTrigger.OnDiscard => "When discarded: ",
			CardExtraEffectTrigger.OnExhaust => "When exhausted: ",
			CardExtraEffectTrigger.DeckPassiveCombatStart => $"[gold]{GetDeckPassiveCombatStartKeywordTitle()}[/gold]: ",
			CardExtraEffectTrigger.DeckPassiveCombatEnd => $"[gold]{GetDeckPassiveCombatEndKeywordTitle()}[/gold]: ",
			CardExtraEffectTrigger.Fatal => "Fatal: ",
			CardExtraEffectTrigger.EndOfTurnInHand => "End of your turn (in hand): ",
			CardExtraEffectTrigger.EndOfTurn => string.Empty,
			CardExtraEffectTrigger.OstyDealDamage => "Whenever Osty attacks: ",
			CardExtraEffectTrigger.OnMovedToTopOfPile => $"When moved to {GetCardPileBoundary(effect?.CardSelectionPile ?? CardExtraEffectCardPile.AllPiles, top: true)}: ",
			CardExtraEffectTrigger.OnMovedToBottomOfPile => $"When moved to {GetCardPileBoundary(effect?.CardSelectionPile ?? CardExtraEffectCardPile.AllPiles, top: false)}: ",
			_ => string.Empty
		};

		string prefix = CardEditorLoc.Enum("triggerPrefix", trigger, fallback);

		return string.IsNullOrEmpty(prefix) ? line : prefix + line;
	}

	private static string GetDeckPassiveCombatStartKeywordTitle()
	{
		return CardEditorLoc.T("cardText.deckPassiveCombatStart.title", "Start Of Combat");
	}

	private static string GetDeckPassiveCombatStartKeywordDescription()
	{
		return CardEditorLoc.T(
			"cardText.deckPassiveCombatStart.description",
			"If this card is in your deck when combat starts, this effect triggers automatically.");
	}

	private static string GetDeckPassiveCombatEndKeywordTitle()
	{
		return CardEditorLoc.T("cardText.deckPassiveCombatEnd.title", "End Of Combat");
	}

	private static string GetDeckPassiveCombatEndKeywordDescription()
	{
		return CardEditorLoc.T(
			"cardText.deckPassiveCombatEnd.description",
			"If this card is in your deck when combat ends, this effect triggers automatically.");
	}

	private static string WrapGrantToCard(string line, CardExtraEffect effect)
	{
		string pileLocation = GetCardPileLocation(effect.CardSelectionPile);
		string selectionText = BuildGrantSelectionText(effect);
		if (effect.CardMatchMode == CardExtraEffectCardMatchMode.CardId
			&& !string.IsNullOrWhiteSpace(effect.MatchCardId))
		{
			string cardText = BuildSpecificCardReferenceText(effect.MatchCardId, GetCardReferenceDisplayMode(effect));
			selectionText = CardEditorLoc.F("cardText.grant.selection.cardId", $"{selectionText}: {cardText}", ("Selection", selectionText), ("Card", cardText));
		}
		else
		{
			selectionText += BuildCountCardMatchSuffix(effect);
		}
		string durationText = GetCardGrantDurationText(effect);
		string futureText = effect.FutureMatchingCards
			? CardEditorLoc.T("cardText.grant.futureMatching", ", including future matching cards")
			: string.Empty;

		return CardEditorLoc.F(
			"cardText.grant.wrap",
			$"Give {selectionText} {pileLocation} {durationText}{futureText}: {line}",
			("Selection", selectionText),
			("Pile", pileLocation),
			("Duration", durationText),
			("Future", futureText),
			("Line", line));
	}

	private static string GetCardGrantDurationText(CardExtraEffect effect)
	{
		return effect.CardGrantDuration switch
		{
			CardExtraEffectCardGrantDuration.ThisCombat => CardEditorLoc.T("cardText.duration.thisCombat", "this combat"),
			CardExtraEffectCardGrantDuration.UntilPlayed => CardEditorLoc.T("cardText.duration.untilPlayed", "until played"),
			CardExtraEffectCardGrantDuration.Turns => effect.CardGrantTurns <= 1
				? CardEditorLoc.T("cardText.duration.thisTurn", "this turn")
				: CardEditorLoc.F("cardText.duration.nextTurns", $"for the next {Math.Max(1, effect.CardGrantTurns).ToString(CultureInfo.InvariantCulture)} turns", ("Turns", Math.Max(1, effect.CardGrantTurns))),
			_ => CardEditorLoc.T("cardText.duration.thisTurn", "this turn")
		};
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
		string countText = countIsX ? FormatXPlusText(count) : count.ToString(CultureInfo.InvariantCulture);
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
				CardExtraEffectCardSelectionMode.Top => singular
					? CardEditorLoc.T("cardText.selection.topCard", "the top card")
					: CardEditorLoc.F("cardText.selection.topCards", $"the top {countText} cards", ("Count", countText)),
				CardExtraEffectCardSelectionMode.Bottom => singular
					? CardEditorLoc.T("cardText.selection.bottomCard", "the bottom card")
					: CardEditorLoc.F("cardText.selection.bottomCards", $"the bottom {countText} cards", ("Count", countText)),
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
			CardExtraEffectCardSelectionMode.Top => singular
				? $"the top {typeAdj}card"
				: $"the top {countText} {typeAdj}{cardsText}",
			CardExtraEffectCardSelectionMode.Bottom => singular
				? $"the bottom {typeAdj}card"
				: $"the bottom {countText} {typeAdj}{cardsText}",
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

	private static string FormatCreatedCardsCostLess(CardModel? card, CardExtraEffect effect, int baseAmount, string amountText, bool amountIsX, int upgradeHighlightComparison)
	{
		int turns = Math.Max(1, effect.CreatedCardsCostTurns);
		bool isPermanent = effect.CreatedCardsCostDuration == CardCreatedCardsCostDuration.Permanent;
		string? suffix = effect.CreatedCardsCostDuration switch
		{
			CardCreatedCardsCostDuration.ThisCombat => CardEditorLoc.T("cardText.duration.thisCombat", "this combat"),
			CardCreatedCardsCostDuration.UntilPlayed => CardEditorLoc.T("cardText.duration.untilPlayed", "until played"),
			CardCreatedCardsCostDuration.Turns => turns <= 1
				? CardEditorLoc.T("cardText.duration.thisTurn", "this turn")
				: CardEditorLoc.F("cardText.duration.nextTurns", $"for the next {turns.ToString(CultureInfo.InvariantCulture)} turns", ("Turns", turns)),
			CardCreatedCardsCostDuration.Permanent => null,
			_ => CardEditorLoc.T("cardText.duration.thisTurn", "this turn")
		};

		bool usesStars = effect.CreatedCardsCostResource == CardCreatedCardsCostResource.Stars;
		CardExtraEffectCostModifier modifier = GetEffectiveCardCostsLessModifier(effect);
		if (modifier == CardExtraEffectCostModifier.FreeToPlay)
		{
			string freeText = HighlightCostModifierText(CardEditorLoc.T("cardText.costModifier.freeToPlay", "free to play"), upgradeHighlightComparison);

			return CardEditorLoc.F("cardText.createdCards.freeToPlay", $"Cards created by this card are {freeText} {suffix}.", ("Free", freeText), ("Duration", suffix!));
		}
		if (modifier == CardExtraEffectCostModifier.Free)
		{
			string freeText = HighlightCostModifierText(usesStars ? BuildStarIcons(0) : BuildEnergyIcons(card, 0), upgradeHighlightComparison);

			return isPermanent
				? CardEditorLoc.F("cardText.createdCards.free.permanent", $"Cards created by this card cost {freeText} for the rest of the run.", ("Free", freeText))
				: CardEditorLoc.F("cardText.createdCards.free", $"Cards created by this card cost {freeText} {suffix}.", ("Free", freeText), ("Duration", suffix!));
		}
		if (modifier == CardExtraEffectCostModifier.HalfCost)
		{
			string halfText = HighlightCostModifierText(usesStars ? BuildHalfStarCostText() : BuildHalfEnergyCostText(card), upgradeHighlightComparison);

			return isPermanent
				? CardEditorLoc.F("cardText.createdCards.halfCost.permanent", $"Cards created by this card cost {halfText} for the rest of the run.", ("Half", halfText))
				: CardEditorLoc.F("cardText.createdCards.halfCost", $"Cards created by this card cost {halfText} {suffix}.", ("Half", halfText), ("Duration", suffix!));
		}

		string costText;
		if (amountIsX)
		{
			costText = amountText + (usesStars ? BuildStarIcons(1) : BuildEnergyIcons(card, 1));
		}
		else if (effect.Amount == -1)
		{
			costText = usesStars ? BuildStarIcons(0) : BuildEnergyIcons(card, 0);
		}
		else
		{
			costText = usesStars ? BuildStarIcons(Math.Abs(baseAmount)) : BuildEnergyIcons(card, Math.Abs(baseAmount));
		}

		if (upgradeHighlightComparison != 0)
		{
			costText = StsTextUtilities.HighlightChangeText(costText, upgradeHighlightComparison);
		}

		return isPermanent
			? CardEditorLoc.F("cardText.createdCards.costLess.permanent", $"Cards created by this card cost {costText} less for the rest of the run.", ("Amount", costText))
			: CardEditorLoc.F("cardText.createdCards.costLess", $"Cards created by this card cost {costText} less {suffix}.", ("Amount", costText), ("Duration", suffix!));
	}

	private static string FormatAddRandomCardToHand(CardExtraEffect effect, int baseAmount, string amountText)
	{
		string filter = BuildGeneratedCardFilter(effect, plural: baseAmount != 1);
		string filterTrim = filter.Trim();
		string suffix = BuildGeneratedCardPoolSuffix(effect.GeneratedCardPool);
		string tagSuffix = BuildGeneratedCardCustomTagSuffix(effect);
		string destination = GetGeneratedCardDestination(effect);
		bool goesToHand = GeneratedCardsGoToHand(effect);

		if (baseAmount == 1)
		{
			string line = string.IsNullOrEmpty(filterTrim)
				? (goesToHand
					? CardEditorLoc.F("cardText.generate.addRandomCard", $"Add a random card{suffix}{tagSuffix} into your hand.", ("PoolSuffix", suffix), ("TagSuffix", tagSuffix))
					: CardEditorLoc.F("cardText.generate.addRandomCardToPile", $"Add a random card{suffix}{tagSuffix} into {destination}.", ("PoolSuffix", suffix), ("TagSuffix", tagSuffix), ("To", destination)))
				: (goesToHand
					? CardEditorLoc.F("cardText.generate.addRandomFilteredCard", $"Add a random {filterTrim} card{suffix}{tagSuffix} into your hand.", ("Filter", filterTrim), ("PoolSuffix", suffix), ("TagSuffix", tagSuffix))
					: CardEditorLoc.F("cardText.generate.addRandomFilteredCardToPile", $"Add a random {filterTrim} card{suffix}{tagSuffix} into {destination}.", ("Filter", filterTrim), ("PoolSuffix", suffix), ("TagSuffix", tagSuffix), ("To", destination)));
			return BuildCostFilteredText(null, line, effect);
		}

		string multiLine = string.IsNullOrEmpty(filterTrim)
			? (goesToHand
				? CardEditorLoc.F("cardText.generate.addRandomCards", $"Add {amountText} random cards{suffix}{tagSuffix} into your hand.", ("Amount", amountText), ("PoolSuffix", suffix), ("TagSuffix", tagSuffix))
				: CardEditorLoc.F("cardText.generate.addRandomCardsToPile", $"Add {amountText} random cards{suffix}{tagSuffix} into {destination}.", ("Amount", amountText), ("PoolSuffix", suffix), ("TagSuffix", tagSuffix), ("To", destination)))
			: (goesToHand
				? CardEditorLoc.F("cardText.generate.addRandomFilteredCards", $"Add {amountText} random {filterTrim} cards{suffix}{tagSuffix} into your hand.", ("Amount", amountText), ("Filter", filterTrim), ("PoolSuffix", suffix), ("TagSuffix", tagSuffix))
				: CardEditorLoc.F("cardText.generate.addRandomFilteredCardsToPile", $"Add {amountText} random {filterTrim} cards{suffix}{tagSuffix} into {destination}.", ("Amount", amountText), ("Filter", filterTrim), ("PoolSuffix", suffix), ("TagSuffix", tagSuffix), ("To", destination)));
		return BuildCostFilteredText(null, multiLine, effect);
	}

	private static string FormatChooseOneOfThreeToHand(CardExtraEffect effect, int baseAmount, string amountText)
	{
		string filter = BuildGeneratedCardFilter(effect, plural: true);
		string filterTrim = filter.Trim();
		string suffix = BuildGeneratedCardPoolSuffix(effect.GeneratedCardPool);
		string tagSuffix = BuildGeneratedCardCustomTagSuffix(effect);
		string destination = GetGeneratedCardDestination(effect);
		bool goesToHand = GeneratedCardsGoToHand(effect);
		string line = string.IsNullOrEmpty(filterTrim)
			? (goesToHand
				? CardEditorLoc.F("cardText.generate.chooseOneOfThree", $"Choose 1 of 3 random cards{suffix}{tagSuffix} to add to your hand.", ("PoolSuffix", suffix), ("TagSuffix", tagSuffix))
				: CardEditorLoc.F("cardText.generate.chooseOneOfThreeToPile", $"Choose 1 of 3 random cards{suffix}{tagSuffix} to add into {destination}.", ("PoolSuffix", suffix), ("TagSuffix", tagSuffix), ("To", destination)))
			: (goesToHand
				? CardEditorLoc.F("cardText.generate.chooseOneOfThreeFiltered", $"Choose 1 of 3 random {filterTrim} cards{suffix}{tagSuffix} to add to your hand.", ("Filter", filterTrim), ("PoolSuffix", suffix), ("TagSuffix", tagSuffix))
				: CardEditorLoc.F("cardText.generate.chooseOneOfThreeFilteredToPile", $"Choose 1 of 3 random {filterTrim} cards{suffix}{tagSuffix} to add into {destination}.", ("Filter", filterTrim), ("PoolSuffix", suffix), ("TagSuffix", tagSuffix), ("To", destination)));

		if (baseAmount == 1)
		{
			return BuildCostFilteredText(null, line, effect);
		}

		return BuildCostFilteredText(null, CardEditorLoc.F("cardText.generate.chooseOneOfThree.times", $"{line} ({amountText} times)", ("Line", line), ("Times", amountText)), effect);
	}

	private static string FormatPlayRandomGeneratedCard(CardExtraEffect effect, int baseAmount, string amountText)
	{
		string filter = BuildGeneratedCardFilter(effect, plural: baseAmount != 1);
		string filterTrim = filter.Trim();
		string suffix = BuildGeneratedCardPoolSuffix(effect.GeneratedCardPool);
		string tagSuffix = BuildGeneratedCardCustomTagSuffix(effect);

		if (baseAmount == 1)
		{
			string line = string.IsNullOrEmpty(filterTrim)
				? CardEditorLoc.F("cardText.generate.playRandomCard", $"Play a random card{suffix}{tagSuffix}.", ("PoolSuffix", suffix), ("TagSuffix", tagSuffix))
				: CardEditorLoc.F("cardText.generate.playRandomFilteredCard", $"Play a random {filterTrim} card{suffix}{tagSuffix}.", ("Filter", filterTrim), ("PoolSuffix", suffix), ("TagSuffix", tagSuffix));
			return BuildCostFilteredText(null, line, effect);
		}

		string multiLine = string.IsNullOrEmpty(filterTrim)
			? CardEditorLoc.F("cardText.generate.playRandomCards", $"Play {amountText} random cards{suffix}{tagSuffix}.", ("Amount", amountText), ("PoolSuffix", suffix), ("TagSuffix", tagSuffix))
			: CardEditorLoc.F("cardText.generate.playRandomFilteredCards", $"Play {amountText} random {filterTrim} cards{suffix}{tagSuffix}.", ("Amount", amountText), ("Filter", filterTrim), ("PoolSuffix", suffix), ("TagSuffix", tagSuffix));
		return BuildCostFilteredText(null, multiLine, effect);
	}

private static string? FormatChooseOneEffectSource(CardModel card, Creature? target, bool isUpgradePreview, CardExtraEffect effect)
{
	List<CardExtraEffectChooseOneOption> options = GetChooseOneOptions(effect);
	if (options.Count == 0)
	{
		return null;
	}

	List<string> optionSummaries = new();
	foreach (CardExtraEffectChooseOneOption option in options)
	{
		optionSummaries.Add(BuildChooseOneOptionSummary(card, target, isUpgradePreview, option));
	}

	string optionText = JoinChooseOneOptionSummaries(optionSummaries);
	return CardEditorLoc.F("cardText.chooseOne.effectSource", $"Choose one: {optionText}.", ("Options", optionText));
}

	private static string FormatChannelOrb(string amountText, int amount, string orbName)
	{
		string localizedOrbName = OrbTitle(orbName, orbName);
		string fallback = amount == 1
			? $"[gold]Channel[/gold] {amountText} [gold]{localizedOrbName}[/gold] Orb."
			: $"[gold]Channel[/gold] {amountText} [gold]{localizedOrbName}[/gold] Orbs.";
		return amount == 1
			? CardEditorLoc.F("cardText.channelOrb.one", fallback, ("Amount", amountText), ("Orb", localizedOrbName))
			: CardEditorLoc.F("cardText.channelOrb.many", fallback, ("Amount", amountText), ("Orb", localizedOrbName));
	}

	private static string FormatChannelRandomOrb(string amountText, int amount)
	{
		string fallback = amount == 1
			? $"[gold]Channel[/gold] {amountText} random Orb."
			: $"[gold]Channel[/gold] {amountText} random Orbs.";
		return amount == 1
			? CardEditorLoc.F("cardText.channelRandomOrb.one", fallback, ("Amount", amountText))
			: CardEditorLoc.F("cardText.channelRandomOrb.many", fallback, ("Amount", amountText));
	}

	private static string FormatMoveCardsBetweenPiles(CardModel? card, CardExtraEffect effect, int amount, string amountText)
	{
		if (effect == null)
		{
			return string.Empty;
		}
		bool hasSelectionCriteria = HasCardSelectionCriteria(effect);

		// Prefer vanilla wording for common movement patterns (discard/exhaust) so it reads naturally and matches synergies.
		if (!hasSelectionCriteria && effect.CardSelectionPile == CardExtraEffectCardPile.Hand && effect.MoveToPile == CardExtraEffectCardPile.DiscardPile)
		{
			bool random = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Random;
			bool all = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All;
			bool upTo = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.UpTo;
			string discardResult;
			if (all)
			{
				discardResult = CardEditorLoc.T("cardText.discard.hand", "Discard your hand.");
			}
			else if (upTo)
			{
				discardResult = CardEditorLoc.F("cardText.discard.upTo", $"Discard up to {amountText} cards.", ("Amount", amountText));
			}
			else if (amount == 1)
			{
				discardResult = random
					? CardEditorLoc.T("cardText.discard.randomOne", "Discard a random card.")
					: CardEditorLoc.T("cardText.discard.one", "Discard a card.");
			}
			else
			{
				discardResult = random
					? CardEditorLoc.F("cardText.discard.randomMany", $"Discard {amountText} random cards.", ("Amount", amountText))
					: CardEditorLoc.F("cardText.discard.many", $"Discard {amountText} cards.", ("Amount", amountText));
			}

			discardResult = AppendCardSelectionNote(discardResult, effect);
			return BuildCostFilteredText(card, discardResult, effect);
		}
		if (!hasSelectionCriteria && effect.CardSelectionPile == CardExtraEffectCardPile.Hand && effect.MoveToPile == CardExtraEffectCardPile.ExhaustPile)
		{
			bool random = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Random;
			bool all = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All;
			bool upTo = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.UpTo;
			string exhaustResult;
			if (all)
			{
				exhaustResult = CardEditorLoc.T("cardText.exhaust.hand", "Exhaust your hand.");
			}
			else if (upTo)
			{
				exhaustResult = CardEditorLoc.F("cardText.exhaust.upTo", $"Exhaust up to {amountText} cards.", ("Amount", amountText));
			}
			else if (amount == 1)
			{
				exhaustResult = random
					? CardEditorLoc.T("cardText.exhaust.randomOne", "Exhaust a random card.")
					: CardEditorLoc.T("cardText.exhaust.one", "Exhaust a card.");
			}
			else
			{
				exhaustResult = random
					? CardEditorLoc.F("cardText.exhaust.randomMany", $"Exhaust {amountText} random cards.", ("Amount", amountText))
					: CardEditorLoc.F("cardText.exhaust.many", $"Exhaust {amountText} cards.", ("Amount", amountText));
			}

			exhaustResult = AppendCardSelectionNote(exhaustResult, effect);
			return BuildCostFilteredText(card, exhaustResult, effect);
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
		moveResult = AppendCardSelectionNote(moveResult, effect);
		return BuildCostFilteredText(card, moveResult, effect);
	}

	private static string FormatUpgradeCardsInPile(CardModel? card, CardExtraEffect effect, int amount, string amountText, string? durationSuffix = null)
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

		result = AppendCardSelectionNote(result, effect);
		result = BuildCostFilteredText(card, result, effect);
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
		string durationText = GetCardGrantDurationText(effect);
		string futureText = effect.FutureMatchingCards
			? CardEditorLoc.T("cardText.grant.futureMatching", ", including future matching cards")
			: string.Empty;
		bool all = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All;
		bool random = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Random;
		bool upTo = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.UpTo;

		string result;
		if (all)
		{
			result = CardEditorLoc.F("cardText.grantKeyword.all", $"All cards {pile} gain {keyword} {durationText}{futureText}.", ("Pile", pile), ("Keyword", keyword), ("Duration", durationText), ("Future", futureText));
		}
		else if (upTo)
		{
			result = CardEditorLoc.F("cardText.grantKeyword.upTo", $"Choose up to {amountText} cards {pile}. They gain {keyword} {durationText}{futureText}.", ("Amount", amountText), ("Pile", pile), ("Keyword", keyword), ("Duration", durationText), ("Future", futureText));
		}
		else if (amount == 1)
		{
			result = random
				? CardEditorLoc.F("cardText.grantKeyword.random.one", $"A random card {pile} gains {keyword} {durationText}{futureText}.", ("Pile", pile), ("Keyword", keyword), ("Duration", durationText), ("Future", futureText))
				: CardEditorLoc.F("cardText.grantKeyword.one", $"Choose a card {pile}. It gains {keyword} {durationText}{futureText}.", ("Pile", pile), ("Keyword", keyword), ("Duration", durationText), ("Future", futureText));
		}
		else
		{
			result = random
				? CardEditorLoc.F("cardText.grantKeyword.random.many", $"{amountText} random cards {pile} gain {keyword} {durationText}{futureText}.", ("Amount", amountText), ("Pile", pile), ("Keyword", keyword), ("Duration", durationText), ("Future", futureText))
				: CardEditorLoc.F("cardText.grantKeyword.many", $"Choose {amountText} cards {pile}. They gain {keyword} {durationText}{futureText}.", ("Amount", amountText), ("Pile", pile), ("Keyword", keyword), ("Duration", durationText), ("Future", futureText));
		}

		return AppendCardSelectionNote(result, effect);
	}

	private static string FormatUpgradeDeckCards(CardExtraEffect effect, int amount, string amountText)
	{
		string result;
		bool all = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All;
		if (all)
		{
			result = CardEditorLoc.T("cardText.upgradeDeck.all", "Upgrade all upgradable cards in your deck.");
		}
		else if (amount == 1)
		{
			result = CardEditorLoc.T("cardText.upgradeDeck.random.one", "Upgrade a random card in your deck.");
		}
		else
		{
			result = CardEditorLoc.F("cardText.upgradeDeck.random.many", $"Upgrade {amountText} random cards in your deck.", ("Amount", amountText));
		}

		return AppendCardSelectionNote(result, effect);
	}

	private static string FormatRemoveBlock(CardExtraEffectTarget target, string amountText)
	{
		return target switch
		{
			CardExtraEffectTarget.AllEnemies => CardEditorLoc.F("cardText.removeBlock.allEnemies", $"ALL enemies lose {amountText} Block.", ("Amount", amountText)),
			CardExtraEffectTarget.RandomEnemy => CardEditorLoc.F("cardText.removeBlock.randomEnemy", $"A random enemy loses {amountText} Block.", ("Amount", amountText)),
			CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.removeBlock.self", $"Lose {amountText} Block.", ("Amount", amountText)),
			_ => CardEditorLoc.F("cardText.removeBlock.target", $"The target loses {amountText} Block.", ("Amount", amountText))
		};
	}

	private static string FormatRemoveArtifact(CardExtraEffectTarget target, string amountText)
	{
		return target switch
		{
			CardExtraEffectTarget.AllEnemies => CardEditorLoc.F("cardText.removeArtifact.allEnemies", $"ALL enemies lose {amountText} Artifact.", ("Amount", amountText)),
			CardExtraEffectTarget.RandomEnemy => CardEditorLoc.F("cardText.removeArtifact.randomEnemy", $"A random enemy loses {amountText} Artifact.", ("Amount", amountText)),
			CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.removeArtifact.self", $"Lose {amountText} Artifact.", ("Amount", amountText)),
			_ => CardEditorLoc.F("cardText.removeArtifact.target", $"The target loses {amountText} Artifact.", ("Amount", amountText))
		};
	}

	private static string FormatDiscardCards(CardModel? card, CardExtraEffect effect, int amount, string amountText)
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
		discardResult = AppendCardSelectionNote(discardResult, effect);
		return BuildCostFilteredText(card, discardResult, effect);
	}

	private static string FormatExhaustCards(CardModel? card, CardExtraEffect effect, int amount, string amountText)
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
		exhaustResult = AppendCardSelectionNote(exhaustResult, effect);
		return BuildCostFilteredText(card, exhaustResult, effect);
	}

	private static string FormatTransformCards(CardModel? card, CardExtraEffect effect, int amount, string amountText)
	{
		if (effect == null)
		{
			return string.Empty;
		}

		bool randomPick = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Random;
		bool all = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All;
		bool upTo = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.UpTo;

		string pileSuffix = string.Empty;
		if (effect.CardSelectionPile != CardExtraEffectCardPile.Hand)
		{
			string pile = GetCardPilePossessive(effect.CardSelectionPile);
			pileSuffix = CardEditorLoc.F("cardText.transform.fromPile", $" from {pile}", ("Pile", pile));
		}

		string intoText;
		if (effect.TransformMode == CardExtraEffectTransformMode.SpecificCard)
		{
			string cardName = BuildSpecificCardReferenceText(effect.SpecificCardId, GetCardReferenceDisplayMode(effect));
			intoText = CardEditorLoc.F("cardText.transform.intoSpecific", $" into {cardName}", ("Card", cardName));
		}
		else
		{
			intoText = CardEditorLoc.T("cardText.transform.intoRandom", " into random cards");
		}

		string result;
		if (all)
		{
			result = CardEditorLoc.F("cardText.transform.all", $"Transform all cards{pileSuffix}{intoText}.", ("PileSuffix", pileSuffix), ("Into", intoText));
		}
		else if (upTo)
		{
			result = CardEditorLoc.F("cardText.transform.upTo", $"Transform up to {amountText} cards{pileSuffix}{intoText}.", ("Amount", amountText), ("PileSuffix", pileSuffix), ("Into", intoText));
		}
		else if (amount == 1)
		{
			result = randomPick
				? CardEditorLoc.F("cardText.transform.randomOne", $"Transform a random card{pileSuffix}{intoText}.", ("PileSuffix", pileSuffix), ("Into", intoText))
				: CardEditorLoc.F("cardText.transform.one", $"Transform a card{pileSuffix}{intoText}.", ("PileSuffix", pileSuffix), ("Into", intoText));
		}
		else
		{
			result = randomPick
				? CardEditorLoc.F("cardText.transform.randomMany", $"Transform {amountText} random cards{pileSuffix}{intoText}.", ("Amount", amountText), ("PileSuffix", pileSuffix), ("Into", intoText))
				: CardEditorLoc.F("cardText.transform.many", $"Transform {amountText} cards{pileSuffix}{intoText}.", ("Amount", amountText), ("PileSuffix", pileSuffix), ("Into", intoText));
		}

		result = AppendCardSelectionNote(result, effect);
		return BuildCostFilteredText(card, result, effect);
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

		if (effect.OrbAction == CardExtraEffectOrbAction.TriggerPassive)
		{
			string orbTypeTitle = GetOrbTypeTitle(effect.OrbType);
			string orbTypePrefix = string.IsNullOrWhiteSpace(orbTypeTitle) ? string.Empty : orbTypeTitle + " ";

			if (effect.OrbScope == CardExtraEffectOrbScope.All)
			{
				return $"Trigger the passive effect of all {orbTypePrefix}Orbs.";
			}

			string selectionWord = effect.OrbSelection switch
			{
				CardExtraEffectOrbSelection.Rightmost => "rightmost",
				CardExtraEffectOrbSelection.Middle => "middle",
				_ => "leftmost"
			};

			return amount == 1
				? $"Trigger the passive effect of your {selectionWord} {orbTypePrefix}Orb."
				: $"Trigger the passive effect of your {selectionWord} {orbTypePrefix}Orb {amountText} times.";
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

		string to = BuildGeneratedDestinationText(GetConfiguredCardCreationDestinations(effect, CardExtraEffectCardPile.DrawPile, CardExtraEffectCardPilePosition.Top));

		return amount == 1
			? CardEditorLoc.F("cardText.copyThisCard.one", $"Add a copy of this card into {to}.", ("To", to))
			: CardEditorLoc.F("cardText.copyThisCard.many", $"Add {amountText} copies of this card into {to}.", ("Amount", amountText), ("To", to));
	}

	private static string FormatAddExactCopyOfThisCardToDeck(int amount, string amountText)
	{
		return amount == 1
			? CardEditorLoc.T("cardText.copyThisCardToDeck.exact.one", "Add an exact copy of this card to your deck.")
			: CardEditorLoc.F("cardText.copyThisCardToDeck.exact.many", $"Add {amountText} exact copies of this card to your deck.", ("Amount", amountText));
	}

	private static string FormatCopyCardsFromPileToDeck(CardModel? card, CardExtraEffect effect, int amount, string amountText, bool exact = false)
	{
		if (effect == null)
		{
			return string.Empty;
		}

		string from = effect.CardSelectionPile == CardExtraEffectCardPile.Deck
			? CardEditorLoc.T("cardText.deck", "your deck")
			: GetCardPilePossessive(effect.CardSelectionPile);
		string to = BuildGeneratedDestinationText(GetCopyCardDestinations(effect));
		bool all = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All;
		bool upTo = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.UpTo;
		bool random = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Random;

		string result;
		if (all)
		{
			result = exact
				? CardEditorLoc.F("cardText.copyPileToDeck.exact.all", $"Add exact copies of all cards from {from} to {to}.", ("From", from), ("To", to))
				: CardEditorLoc.F("cardText.copyPileToDeck.all", $"Add copies of all cards from {from} to {to}.", ("From", from), ("To", to));
		}
		else if (upTo)
		{
			result = exact
				? CardEditorLoc.F("cardText.copyPileToDeck.exact.upTo", $"Add exact copies of up to {amountText} cards from {from} to {to}.", ("Amount", amountText), ("From", from), ("To", to))
				: CardEditorLoc.F("cardText.copyPileToDeck.upTo", $"Add copies of up to {amountText} cards from {from} to {to}.", ("Amount", amountText), ("From", from), ("To", to));
		}
		else if (amount == 1)
		{
			result = exact
				? (random
					? CardEditorLoc.F("cardText.copyPileToDeck.exact.random.one", $"Add an exact copy of a random card from {from} to {to}.", ("From", from), ("To", to))
					: CardEditorLoc.F("cardText.copyPileToDeck.exact.choose.one", $"Choose a card from {from}. Add an exact copy to {to}.", ("From", from), ("To", to)))
				: (random
					? CardEditorLoc.F("cardText.copyPileToDeck.random.one", $"Add a copy of a random card from {from} to {to}.", ("From", from), ("To", to))
					: CardEditorLoc.F("cardText.copyPileToDeck.choose.one", $"Choose a card from {from}. Add a copy to {to}.", ("From", from), ("To", to)));
		}
		else
		{
			result = exact
				? (random
					? CardEditorLoc.F("cardText.copyPileToDeck.exact.random.many", $"Add exact copies of {amountText} random cards from {from} to {to}.", ("Amount", amountText), ("From", from), ("To", to))
					: CardEditorLoc.F("cardText.copyPileToDeck.exact.choose.many", $"Choose {amountText} cards from {from}. Add exact copies to {to}.", ("Amount", amountText), ("From", from), ("To", to)))
				: (random
					? CardEditorLoc.F("cardText.copyPileToDeck.random.many", $"Add copies of {amountText} random cards from {from} to {to}.", ("Amount", amountText), ("From", from), ("To", to))
					: CardEditorLoc.F("cardText.copyPileToDeck.choose.many", $"Choose {amountText} cards from {from}. Add copies to {to}.", ("Amount", amountText), ("From", from), ("To", to)));
		}

		result = AppendCardSelectionNote(result, effect);
		return BuildCostFilteredText(card, result, effect);
	}

	private static string FormatRemoveCardsFromDeck(CardModel? card, CardExtraEffect effect, int amount, string amountText)
	{
		if (effect == null)
		{
			return string.Empty;
		}

		bool fromDeck = effect.CardSelectionPile == CardExtraEffectCardPile.Deck;
		string source = fromDeck ? CardEditorLoc.T("cardText.deck", "your deck") : GetCardPilePossessive(effect.CardSelectionPile);
		bool all = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All;
		bool upTo = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.UpTo;
		bool random = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Random;

		string result;
		if (fromDeck)
		{
			if (all)
			{
				result = CardEditorLoc.T("cardText.removeDeck.all", "Remove all matching cards from your deck.");
			}
			else if (upTo)
			{
				result = CardEditorLoc.F("cardText.removeDeck.upTo", $"Remove up to {amountText} cards from your deck.", ("Amount", amountText));
			}
			else if (amount == 1)
			{
				result = random
					? CardEditorLoc.T("cardText.removeDeck.random.one", "Remove a random card from your deck.")
					: CardEditorLoc.T("cardText.removeDeck.choose.one", "Choose a card to remove from your deck.");
			}
			else
			{
				result = random
					? CardEditorLoc.F("cardText.removeDeck.random.many", $"Remove {amountText} random cards from your deck.", ("Amount", amountText))
					: CardEditorLoc.F("cardText.removeDeck.choose.many", $"Choose {amountText} cards to remove from your deck.", ("Amount", amountText));
			}
		}
		else if (all)
		{
			result = CardEditorLoc.F("cardText.removeDeckFromPile.all", $"Remove the deck versions of all cards from {source}.", ("Source", source));
		}
		else if (upTo)
		{
			result = CardEditorLoc.F("cardText.removeDeckFromPile.upTo", $"Remove the deck versions of up to {amountText} cards from {source}.", ("Amount", amountText), ("Source", source));
		}
		else if (amount == 1)
		{
			result = random
				? CardEditorLoc.F("cardText.removeDeckFromPile.random.one", $"Remove the deck version of a random card from {source}.", ("Source", source))
				: CardEditorLoc.F("cardText.removeDeckFromPile.choose.one", $"Choose a card from {source}. Remove its deck version.", ("Source", source));
		}
		else
		{
			result = random
				? CardEditorLoc.F("cardText.removeDeckFromPile.random.many", $"Remove the deck versions of {amountText} random cards from {source}.", ("Amount", amountText), ("Source", source))
				: CardEditorLoc.F("cardText.removeDeckFromPile.choose.many", $"Choose {amountText} cards from {source}. Remove their deck versions.", ("Amount", amountText), ("Source", source));
		}

		result = AppendCardSelectionNote(result, effect);
		return BuildCostFilteredText(card, result, effect);
	}

	private static bool GeneratedCardsGoToHand(CardExtraEffect? effect)
	{
		List<(CardExtraEffectCardPile Pile, CardExtraEffectCardPilePosition Position)> destinations = GetGeneratedCardDestinations(effect, CardExtraEffectCardPile.Hand, CardExtraEffectCardPilePosition.Bottom);
		return destinations.Count == 1 && destinations[0].Pile == CardExtraEffectCardPile.Hand;
	}

	private static string GetGeneratedCardDestination(CardExtraEffect? effect)
	{
		return BuildGeneratedDestinationText(GetGeneratedCardDestinations(effect, CardExtraEffectCardPile.Hand, CardExtraEffectCardPilePosition.Bottom));
	}

	private static List<(CardExtraEffectCardPile Pile, CardExtraEffectCardPilePosition Position)> GetConfiguredCardCreationDestinations(CardExtraEffect? effect, CardExtraEffectCardPile defaultPile, CardExtraEffectCardPilePosition defaultPosition)
	{
		CardExtraEffectCardPile primaryPile = effect?.MoveToPile ?? defaultPile;
		CardExtraEffectCardPilePosition primaryPosition = effect?.MoveToPosition ?? defaultPosition;

		List<(CardExtraEffectCardPile Pile, CardExtraEffectCardPilePosition Position)> destinations = new()
		{
			(primaryPile, primaryPosition)
		};

		CardExtraEffectAdditionalMoveToPiles extras = effect?.AdditionalMoveToPiles ?? CardExtraEffectAdditionalMoveToPiles.None;
		foreach (CardExtraEffectCardPile pile in new[] { CardExtraEffectCardPile.Hand, CardExtraEffectCardPile.DrawPile, CardExtraEffectCardPile.DiscardPile, CardExtraEffectCardPile.ExhaustPile })
		{
			CardExtraEffectAdditionalMoveToPiles flag = AdditionalMoveToPileFlag(pile);
			if (flag == CardExtraEffectAdditionalMoveToPiles.None || !extras.HasFlag(flag) || pile == primaryPile)
			{
				continue;
			}

			CardExtraEffectCardPilePosition position = pile == CardExtraEffectCardPile.DrawPile
				? primaryPosition
				: CardExtraEffectCardPilePosition.Bottom;
			destinations.Add((pile, position));
		}

		return destinations;
	}

	private static List<(CardExtraEffectCardPile Pile, CardExtraEffectCardPilePosition Position)> GetCopyCardDestinations(CardExtraEffect? effect)
	{
		// Legacy copy-from-pile effects were always deck-bound and never saved an explicit destination.
		// Keep that behavior until the effect is re-saved with an opted-in configured destination.
		return effect != null && effect.UseMoveDestinationForGeneratedCards
			? GetConfiguredCardCreationDestinations(effect, CardExtraEffectCardPile.Deck, CardExtraEffectCardPilePosition.Top)
			: new List<(CardExtraEffectCardPile Pile, CardExtraEffectCardPilePosition Position)>
			{
				(CardExtraEffectCardPile.Deck, CardExtraEffectCardPilePosition.Top)
			};
	}

	private static CardExtraEffectAdditionalMoveToPiles AdditionalMoveToPileFlag(CardExtraEffectCardPile pile)
	{
		return pile switch
		{
			CardExtraEffectCardPile.Hand => CardExtraEffectAdditionalMoveToPiles.Hand,
			CardExtraEffectCardPile.DrawPile => CardExtraEffectAdditionalMoveToPiles.DrawPile,
			CardExtraEffectCardPile.DiscardPile => CardExtraEffectAdditionalMoveToPiles.DiscardPile,
			CardExtraEffectCardPile.ExhaustPile => CardExtraEffectAdditionalMoveToPiles.ExhaustPile,
			_ => CardExtraEffectAdditionalMoveToPiles.None
		};
	}

	private static List<(CardExtraEffectCardPile Pile, CardExtraEffectCardPilePosition Position)> GetGeneratedCardDestinations(CardExtraEffect? effect, CardExtraEffectCardPile defaultPile, CardExtraEffectCardPilePosition defaultPosition)
	{
		CardExtraEffectCardPile primaryPile = effect == null || !effect.UseMoveDestinationForGeneratedCards
			? defaultPile
			: effect.MoveToPile;
		CardExtraEffectCardPilePosition primaryPosition = effect == null || !effect.UseMoveDestinationForGeneratedCards
			? defaultPosition
			: effect.MoveToPosition;

		List<(CardExtraEffectCardPile Pile, CardExtraEffectCardPilePosition Position)> destinations = new()
		{
			(primaryPile, primaryPosition)
		};

		CardExtraEffectAdditionalMoveToPiles extras = effect?.AdditionalMoveToPiles ?? CardExtraEffectAdditionalMoveToPiles.None;
		foreach (CardExtraEffectCardPile pile in new[] { CardExtraEffectCardPile.Hand, CardExtraEffectCardPile.DrawPile, CardExtraEffectCardPile.DiscardPile, CardExtraEffectCardPile.ExhaustPile })
		{
			CardExtraEffectAdditionalMoveToPiles flag = AdditionalMoveToPileFlag(pile);
			if (flag == CardExtraEffectAdditionalMoveToPiles.None || !extras.HasFlag(flag) || pile == primaryPile)
			{
				continue;
			}

			CardExtraEffectCardPilePosition position = pile == CardExtraEffectCardPile.DrawPile
				? primaryPosition
				: CardExtraEffectCardPilePosition.Bottom;
			destinations.Add((pile, position));
		}

		return destinations;
	}

	private static string BuildGeneratedDestinationText(IReadOnlyList<(CardExtraEffectCardPile Pile, CardExtraEffectCardPilePosition Position)> destinations)
	{
		if (destinations == null || destinations.Count == 0)
		{
			return GetCardPileDestination(CardExtraEffectCardPile.Hand, CardExtraEffectCardPilePosition.Bottom);
		}

		List<string> labels = destinations
			.Select(dest => GetCardPileDestination(dest.Pile, dest.Position))
			.Distinct(StringComparer.Ordinal)
			.ToList();
		if (labels.Count == 1)
		{
			return labels[0];
		}
		if (labels.Count == 2)
		{
			return labels[0] + " and " + labels[1];
		}

		return string.Join(", ", labels.Take(labels.Count - 1)) + ", and " + labels[^1];
	}

	private static string FormatCardStarCostDelta(int signedAmount, string? durationSuffix, int upgradeHighlightComparison)
	{
		int magnitude = Math.Abs(signedAmount);
		string amountText = BuildStarIcons(magnitude);
		if (upgradeHighlightComparison != 0)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, upgradeHighlightComparison);
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

	private static string FormatCardStarCostDeltaText(string amountText, bool isLess, string? durationSuffix, int upgradeHighlightComparison)
	{
		if (upgradeHighlightComparison != 0)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, upgradeHighlightComparison);
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
		string cardName = BuildSpecificCardReferenceText(effect?.SpecificCardId, GetCardReferenceDisplayMode(effect));
		string destination = BuildGeneratedDestinationText(GetConfiguredCardCreationDestinations(effect, CardExtraEffectCardPile.DrawPile, CardExtraEffectCardPilePosition.Top));
		return amount == 1
			? CardEditorLoc.F("cardText.addSpecificCard.one", $"Add a {cardName} into {destination}.", ("Card", cardName), ("Destination", destination))
			: CardEditorLoc.F("cardText.addSpecificCard.many", $"Add {amountText} copies of {cardName} into {destination}.", ("Amount", amountText), ("Card", cardName), ("Destination", destination));
	}

	private static string FormatFetchSpecificCardToHand(CardExtraEffect effect, int amount, string amountText)
	{
		string cardName = BuildSpecificCardReferenceText(effect?.SpecificCardId, GetCardReferenceDisplayMode(effect));
		string destination = GetCardPileDestination(effect.MoveToPile, effect.MoveToPosition);
		bool all = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All;
		if (all)
			return CardEditorLoc.F("cardText.fetchSpecificCard.all", $"Put all copies of {cardName} into your {destination} from any pile.", ("Card", cardName), ("Destination", destination));
		return amount == 1
			? CardEditorLoc.F("cardText.fetchSpecificCard.one", $"Put {cardName} into your {destination} from any pile.", ("Card", cardName), ("Destination", destination))
			: CardEditorLoc.F("cardText.fetchSpecificCard.many", $"Put up to {amountText} copies of {cardName} into your {destination} from any pile.", ("Amount", amountText), ("Card", cardName), ("Destination", destination));
	}

	private static string FormatPlayCardFromPile(CardModel? card, CardExtraEffect effect, int amount, string amountText)
	{
		string pile = GetCardPilePossessive(effect.CardSelectionPile);
		bool random = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Random;
		bool all = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All;
		bool upTo = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.UpTo;
		bool top = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Top;
		bool bottom = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.Bottom;
		string result;
		if (all)
		{
			result = CardEditorLoc.F("cardText.playFromPile.all", $"Play all cards from {pile}.", ("Pile", pile));
		}
		else if (upTo)
		{
			result = CardEditorLoc.F("cardText.playFromPile.upTo.many", $"Play up to {amountText} cards from {pile}.", ("Amount", amountText), ("Pile", pile));
		}
		else if (top)
		{
			result = amount == 1
				? CardEditorLoc.F("cardText.playFromPile.top.one", $"Play the top card of {pile}.", ("Pile", pile))
				: CardEditorLoc.F("cardText.playFromPile.top.many", $"Play the top {amountText} cards of {pile}.", ("Amount", amountText), ("Pile", pile));
		}
		else if (bottom)
		{
			result = amount == 1
				? CardEditorLoc.F("cardText.playFromPile.bottom.one", $"Play the bottom card of {pile}.", ("Pile", pile))
				: CardEditorLoc.F("cardText.playFromPile.bottom.many", $"Play the bottom {amountText} cards of {pile}.", ("Amount", amountText), ("Pile", pile));
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
		result = AppendCardSelectionNote(result, effect);
		return BuildCostFilteredText(card, result, effect);
	}

	private static string FormatAutoPlaySelfFromPile(CardExtraEffect effect)
	{
		string pile = GetCardPileLocationWithSelectionMode(effect.CardSelectionPile, effect.CardSelectionMode);
		return effect.Trigger switch
		{
			CardExtraEffectTrigger.OnDraw => CardEditorLoc.F(
				"cardText.autoPlaySelf.onDraw",
				$"Whenever a card is drawn, if this is {pile}, play it.",
				("Pile", pile)),
			CardExtraEffectTrigger.OnDiscard => CardEditorLoc.F(
				"cardText.autoPlaySelf.onDiscard",
				$"Whenever a card is discarded, if this is {pile}, play it.",
				("Pile", pile)),
			CardExtraEffectTrigger.OnExhaust => CardEditorLoc.F(
				"cardText.autoPlaySelf.onExhaust",
				$"Whenever a card is exhausted, if this is {pile}, play it.",
				("Pile", pile)),
			CardExtraEffectTrigger.EndOfTurn => CardEditorLoc.F(
				"cardText.autoPlaySelf.endOfTurn",
				$"At the end of your turn, if this is {pile}, play it.",
				("Pile", pile)),
			CardExtraEffectTrigger.StartOfEnemyTurn => CardEditorLoc.F(
				"cardText.autoPlaySelf.startOfEnemyTurn",
				$"At the start of the enemy turn, if this is {pile}, play it.",
				("Pile", pile)),
			CardExtraEffectTrigger.OnPlay => CardEditorLoc.F(
				"cardText.autoPlaySelf.onPlay",
				$"Whenever a card is played, if this is {pile}, play it.",
				("Pile", pile)),
			CardExtraEffectTrigger.OnMovedToTopOfPile => CardEditorLoc.F(
				"cardText.autoPlaySelf.onMovedToTop",
				$"Whenever this is moved to {GetCardPileBoundary(effect.CardSelectionPile, top: true)}, play it.",
				("Pile", GetCardPileBoundary(effect.CardSelectionPile, top: true))),
			CardExtraEffectTrigger.OnMovedToBottomOfPile => CardEditorLoc.F(
				"cardText.autoPlaySelf.onMovedToBottom",
				$"Whenever this is moved to {GetCardPileBoundary(effect.CardSelectionPile, top: false)}, play it.",
				("Pile", GetCardPileBoundary(effect.CardSelectionPile, top: false))),
			_ => CardEditorLoc.F(
				"cardText.autoPlaySelf.startOfTurn",
				$"At the start of your turn, if this is {pile}, play it.",
				("Pile", pile))
		};
	}

	private static string FormatDrawCards(CardExtraEffect effect, int grammarAmount, string amountText)
	{
		CardExtraEffectCardPile sourcePile = GetEffectiveDrawSourcePile(effect);
		bool hasSelectionCriteria = effect != null && HasCardSelectionCriteria(effect);
		if (hasSelectionCriteria || sourcePile != CardExtraEffectCardPile.DrawPile)
		{
			string descriptor = hasSelectionCriteria
				? BuildCardSelectionDescriptor(effect, plural: grammarAmount != 1)
				: (grammarAmount == 1 ? "card" : "cards");
			string sourceText = sourcePile == CardExtraEffectCardPile.AllPiles
				? CardEditorLoc.T("cardText.pile.anywhere", "any pile")
				: GetCardPilePossessive(sourcePile);
			return CardEditorLoc.F(
				"cardText.drawCards.filtered",
				$"Draw {amountText} {descriptor} from {sourceText}.",
				("Amount", amountText),
				("Descriptor", descriptor),
				("Source", sourceText));
		}

		string cardOrCards = grammarAmount == 1 ? "card" : "cards";
		return CardEditorLoc.F(
			"cardText.drawCards",
			$"Draw {amountText} {cardOrCards}.",
			("Amount", amountText),
			("CardWord", cardOrCards));
	}

	private static string FormatDrawCardsThatCostLess(CardModel card, CardExtraEffect effect, int grammarAmount, string amountText, string? durationSuffix)
	{
		int costLess = effect.CardSelectionCount > 0 ? effect.CardSelectionCount : 1;
		string costLessText = BuildEnergyIcons(card, costLess);
		string subject = grammarAmount == 1 ? "it" : "they";
		string subjectCap = char.ToUpperInvariant(subject[0]) + subject[1..];
		string cardOrCards = grammarAmount == 1 ? "card" : "cards";
		CardExtraEffectCardPile sourcePile = GetEffectiveDrawSourcePile(effect);
		if ((effect != null && HasCardSelectionCriteria(effect)) || sourcePile != CardExtraEffectCardPile.DrawPile)
		{
			string descriptor = effect != null && HasCardSelectionCriteria(effect)
				? BuildCardSelectionDescriptor(effect, plural: grammarAmount != 1)
				: cardOrCards;
			string sourceText = sourcePile == CardExtraEffectCardPile.AllPiles
				? CardEditorLoc.T("cardText.pile.anywhere", "any pile")
				: GetCardPilePossessive(sourcePile);
			if (durationSuffix != null)
			{
				return CardEditorLoc.F(
					"cardText.drawCostLess.filtered.duration",
					$"Draw {amountText} {descriptor} from {sourceText}. {subjectCap} cost {costLessText} less {durationSuffix}.",
					("Amount", amountText),
					("Descriptor", descriptor),
					("Source", sourceText),
					("CostLess", costLessText),
					("SubjectCap", subjectCap),
					("Duration", durationSuffix));
			}

			return CardEditorLoc.F(
				"cardText.drawCostLess.filtered",
				$"Draw {amountText} {descriptor} from {sourceText}. {subjectCap} cost {costLessText} less.",
				("Amount", amountText),
				("Descriptor", descriptor),
				("Source", sourceText),
				("CostLess", costLessText),
				("SubjectCap", subjectCap));
		}
		if (durationSuffix != null)
		{
			return CardEditorLoc.F(
				"cardText.drawCostLess.duration",
				$"Draw {amountText} {cardOrCards}. {subjectCap} cost {costLessText} less {durationSuffix}.",
				("Amount", amountText),
				("CardWord", cardOrCards),
				("CostLess", costLessText),
				("SubjectCap", subjectCap),
				("Duration", durationSuffix));
		}
		return CardEditorLoc.F(
			"cardText.drawCostLess",
			$"Draw {amountText} {cardOrCards}. {subjectCap} cost {costLessText} less.",
			("Amount", amountText),
			("CardWord", cardOrCards),
			("CostLess", costLessText),
			("SubjectCap", subjectCap));
	}

	private static string FormatAutoDrawSelfFromPile(CardExtraEffect effect)
	{
		string pile = GetCardPileLocationWithSelectionMode(effect.CardSelectionPile, effect.CardSelectionMode);
		return effect.Trigger switch
		{
			CardExtraEffectTrigger.OnDraw => CardEditorLoc.F(
				"cardText.autoDrawSelf.onDraw",
				$"Whenever a card is drawn, if this is {pile}, draw it.",
				("Pile", pile)),
			CardExtraEffectTrigger.OnDiscard => CardEditorLoc.F(
				"cardText.autoDrawSelf.onDiscard",
				$"Whenever a card is discarded, if this is {pile}, draw it.",
				("Pile", pile)),
			CardExtraEffectTrigger.OnExhaust => CardEditorLoc.F(
				"cardText.autoDrawSelf.onExhaust",
				$"Whenever a card is exhausted, if this is {pile}, draw it.",
				("Pile", pile)),
			CardExtraEffectTrigger.EndOfTurn => CardEditorLoc.F(
				"cardText.autoDrawSelf.endOfTurn",
				$"At the end of your turn, if this is {pile}, draw it.",
				("Pile", pile)),
			CardExtraEffectTrigger.StartOfEnemyTurn => CardEditorLoc.F(
				"cardText.autoDrawSelf.startOfEnemyTurn",
				$"At the start of the enemy turn, if this is {pile}, draw it.",
				("Pile", pile)),
			CardExtraEffectTrigger.OnPlay => CardEditorLoc.F(
				"cardText.autoDrawSelf.onPlay",
				$"Whenever a card is played, if this is {pile}, draw it.",
				("Pile", pile)),
			CardExtraEffectTrigger.OnMovedToTopOfPile => CardEditorLoc.F(
				"cardText.autoDrawSelf.onMovedToTop",
				$"Whenever this is moved to {GetCardPileBoundary(effect.CardSelectionPile, top: true)}, draw it.",
				("Pile", GetCardPileBoundary(effect.CardSelectionPile, top: true))),
			CardExtraEffectTrigger.OnMovedToBottomOfPile => CardEditorLoc.F(
				"cardText.autoDrawSelf.onMovedToBottom",
				$"Whenever this is moved to {GetCardPileBoundary(effect.CardSelectionPile, top: false)}, draw it.",
				("Pile", GetCardPileBoundary(effect.CardSelectionPile, top: false))),
			_ => CardEditorLoc.F(
				"cardText.autoDrawSelf.startOfTurn",
				$"At the start of your turn, if this is {pile}, draw it.",
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

		string descriptor = BuildCountCardDescriptor(effect, plural: true, includeOtherPrefix: effect.CountExcludeSourceCard);

		string key = isPlay ? "cardText.conditionalAutoPlaySelf.descriptor" : "cardText.conditionalAutoDrawSelf.descriptor";
		string fallback = isPlay
			? $"If you've {verb} {amountText}+ {descriptor} {window} and this is {pile}, play it."
			: $"If you've {verb} {amountText}+ {descriptor} {window} and this is {pile}, draw it.";

		return CardEditorLoc.F(key, fallback,
			("Amount", amountText),
			("Verb", verb),
			("Descriptor", descriptor),
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

	private static string? ResolvePowerTitle(string? powerId)
	{
		if (string.IsNullOrWhiteSpace(powerId))
		{
			return null;
		}

		ModelId id;
		try
		{
			id = ModelId.Deserialize(powerId.Trim());
		}
		catch
		{
			return powerId.Trim();
		}

		try
		{
			PowerModel? canonical = ModelDb.GetByIdOrNull<PowerModel>(id);
			return canonical?.Title?.GetFormattedText() ?? powerId.Trim();
		}
		catch
		{
			return powerId.Trim();
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

internal static CardExtraEffectChooseOneOption? CloneChooseOneOption(CardExtraEffectChooseOneOption? source)
{
	if (source == null)
	{
		return null;
	}

	return new CardExtraEffectChooseOneOption
	{
		Mode = source.Mode,
		ShowFullText = source.ShowFullText,
		CardId = source.CardId,
		QuerySource = source.QuerySource,
		QueryPile = source.QueryPile,
		QueryPool = source.QueryPool,
		QueryType = source.QueryType,
		QuerySelectionMode = source.QuerySelectionMode,
		QueryCount = source.QueryCount,
		QueryMatchMode = source.QueryMatchMode,
		QueryMatchCardId = source.QueryMatchCardId,
		QueryMatchTagKind = source.QueryMatchTagKind,
		QueryMatchVanillaTag = source.QueryMatchVanillaTag,
		QueryMatchCustomTag = source.QueryMatchCustomTag,
		QueryMatchCustomKeyword = source.QueryMatchCustomKeyword
	};
}

internal static CardExtraEffectChooseOneOption NormalizeChooseOneOption(CardExtraEffectChooseOneOption? option, string? legacyCardId)
{
	CardExtraEffectChooseOneOption normalized = CloneChooseOneOption(option) ?? new CardExtraEffectChooseOneOption();
	if (string.IsNullOrWhiteSpace(normalized.CardId) && !string.IsNullOrWhiteSpace(legacyCardId))
	{
		normalized.CardId = legacyCardId.Trim();
	}

	normalized.QueryCount = Math.Clamp(normalized.QueryCount <= 0 ? 1 : normalized.QueryCount, 1, 99);
	return normalized;
}

private static CardExtraEffectCardReferenceDisplayMode GetCardReferenceDisplayMode(CardExtraEffect effect)
{
	return effect?.CardReferenceDisplayMode ?? CardExtraEffectCardReferenceDisplayMode.NameOnly;
}

private static CardExtraEffectCardReferenceDisplayMode GetChooseOneReferenceDisplayMode(CardExtraEffectChooseOneOption? option)
{
	return option != null && option.ShowFullText
		? CardExtraEffectCardReferenceDisplayMode.FullText
		: CardExtraEffectCardReferenceDisplayMode.NameOnly;
}

private static string? ResolveSpecificCardSummaryText(string? specificCardId)
{
	if (!TryParseSpecificCardId(specificCardId ?? string.Empty, out ModelId id))
	{
		return null;
	}

	try
	{
		CardModel? canonical = ModelDb.GetByIdOrNull<CardModel>(id);
		if (canonical == null)
		{
			return null;
		}

		string? description = canonical.GetDescriptionForUpgradePreview();
		if (string.IsNullOrWhiteSpace(description))
		{
			return null;
		}

		string stripped = StripBbCodeTags(description);
		return NormalizeChooseOneSummaryText(stripped);
	}
	catch
	{
		return null;
	}
}

private static string BuildSpecificCardReferenceText(string? specificCardId, CardExtraEffectCardReferenceDisplayMode displayMode)
{
	string fallback = CardEditorLoc.T("cardText.specificCard.unknown", "Unknown Card");
	string idStr = specificCardId?.Trim() ?? string.Empty;
	string title = ResolveSpecificCardTitle(idStr) ?? fallback;
	if (displayMode != CardExtraEffectCardReferenceDisplayMode.FullText)
	{
		return title;
	}

	string? summary = ResolveSpecificCardSummaryText(idStr);
	if (string.IsNullOrWhiteSpace(summary) || string.Equals(summary, title, StringComparison.OrdinalIgnoreCase))
	{
		return title;
	}

	return CardEditorLoc.F(
		"cardText.specificCard.withSummary",
		$"{title} ({summary})",
		("Card", title),
		("Summary", summary));
}

private static string? BuildEffectSourceReferenceLine(
	CardModel card,
	Creature? target,
	bool isUpgradePreview,
	CardExtraEffect effect)
{
	string? idStr = effect?.SpecificCardId;
	if (string.IsNullOrWhiteSpace(idStr))
	{
		return null;
	}

	if (GetCardReferenceDisplayMode(effect) != CardExtraEffectCardReferenceDisplayMode.FullText)
	{
		string title = ResolveSpecificCardTitle(idStr) ?? idStr.Trim();
		return string.IsNullOrWhiteSpace(title) ? null : title.Trim() + ".";
	}

	ModelId sourceId = ModelId.Deserialize(idStr.Trim());
	if (sourceId == ModelId.none)
	{
		return null;
	}

	string? sourceLine = CardEditorCreatedCardEffectSourceSupport.GetSingleEffectSourceDescription(
		card,
		target,
		isUpgradePreview,
		sourceId,
		GetEffectSourceRuntimeInstanceKey(card, effect),
		effect.CustomKeywordName);
	return string.IsNullOrWhiteSpace(sourceLine) ? null : sourceLine;
}

private static bool IsMeaningfulChooseOneOption(CardExtraEffectChooseOneOption? option)
{
	if (option == null)
	{
		return false;
	}

	return option.Mode == CardExtraEffectChooseOneOptionMode.MatchingCards
		|| !string.IsNullOrWhiteSpace(option.CardId);
}

internal static List<CardExtraEffectChooseOneOption> GetChooseOneOptions(CardExtraEffect? effect)
{
	List<CardExtraEffectChooseOneOption> options = new();
	if (effect == null)
	{
		return options;
	}

	CardExtraEffectChooseOneOption[] normalized =
	{
		NormalizeChooseOneOption(effect.ChooseOneOption1, effect.SpecificCardId),
		NormalizeChooseOneOption(effect.ChooseOneOption2, effect.SpecificCardId2),
		NormalizeChooseOneOption(effect.ChooseOneOption3, effect.SpecificCardId3)
	};

	foreach (CardExtraEffectChooseOneOption option in normalized)
	{
		if (IsMeaningfulChooseOneOption(option))
		{
			options.Add(option);
		}
	}

	return options;
}

private static bool ChooseOneOptionsEqual(CardExtraEffectChooseOneOption? a, CardExtraEffectChooseOneOption? b)
{
	CardExtraEffectChooseOneOption left = NormalizeChooseOneOption(a, legacyCardId: null);
	CardExtraEffectChooseOneOption right = NormalizeChooseOneOption(b, legacyCardId: null);
	if (!IsMeaningfulChooseOneOption(left) && !IsMeaningfulChooseOneOption(right))
	{
		return true;
	}

	return left.Mode == right.Mode
		&& left.ShowFullText == right.ShowFullText
		&& string.Equals(left.CardId ?? string.Empty, right.CardId ?? string.Empty, StringComparison.Ordinal)
		&& left.QuerySource == right.QuerySource
		&& left.QueryPile == right.QueryPile
		&& left.QueryPool == right.QueryPool
		&& left.QueryType == right.QueryType
		&& left.QuerySelectionMode == right.QuerySelectionMode
		&& left.QueryCount == right.QueryCount
		&& left.QueryMatchMode == right.QueryMatchMode
		&& string.Equals(left.QueryMatchCardId ?? string.Empty, right.QueryMatchCardId ?? string.Empty, StringComparison.Ordinal)
		&& left.QueryMatchTagKind == right.QueryMatchTagKind
		&& left.QueryMatchVanillaTag == right.QueryMatchVanillaTag
		&& string.Equals(left.QueryMatchCustomTag ?? string.Empty, right.QueryMatchCustomTag ?? string.Empty, StringComparison.Ordinal)
		&& string.Equals(left.QueryMatchCustomKeyword ?? string.Empty, right.QueryMatchCustomKeyword ?? string.Empty, StringComparison.Ordinal);
}

private static bool TryGetChooseOneExactSourceId(CardExtraEffectChooseOneOption? option, out ModelId sourceId)
{
	sourceId = ModelId.none;
	if (option == null || option.Mode != CardExtraEffectChooseOneOptionMode.ExactCard || string.IsNullOrWhiteSpace(option.CardId))
	{
		return false;
	}

	return TryParseSpecificCardId(option.CardId, out sourceId) && sourceId != ModelId.none;
}

private static List<ModelId> GetChooseOneEffectSourceIds(CardExtraEffect? effect)
{
	List<ModelId> result = new();
	foreach (CardExtraEffectChooseOneOption option in GetChooseOneOptions(effect))
	{
		if (!TryGetChooseOneExactSourceId(option, out ModelId sourceId) || result.Contains(sourceId))
		{
			continue;
		}

		result.Add(sourceId);
	}

	return result;
}

private static CardExtraEffect BuildChooseOneQueryEffect(CardExtraEffectChooseOneOption option)
{
	return new CardExtraEffect
	{
		CardReferenceDisplayMode = GetChooseOneReferenceDisplayMode(option),
		CardSelectionMode = option.QuerySelectionMode,
		CardSelectionCount = Math.Clamp(option.QueryCount <= 0 ? 1 : option.QueryCount, 1, 99),
		CardSelectionPile = option.QueryPile,
		CardSelectionPool = option.QueryPool,
		CardSelectionType = option.QueryType,
		CardMatchMode = option.QueryMatchMode,
		MatchCardId = option.QueryMatchCardId?.Trim(),
		MatchTagKind = option.QueryMatchTagKind,
		MatchVanillaTag = option.QueryMatchVanillaTag,
		MatchCustomTag = option.QueryMatchCustomTag?.Trim(),
		MatchCustomKeyword = option.QueryMatchCustomKeyword?.Trim()
	};
}

private static string NormalizeChooseOneSummaryText(string text)
{
	if (string.IsNullOrWhiteSpace(text))
	{
		return CardEditorLoc.T("cardText.chooseOne.unknownOption", "Unknown Option");
	}

	string compact = Regex.Replace(text.Trim(), "\\s+", " ");
	return compact.Trim().TrimEnd('.', ' ');
}

internal static string DescribeChooseOneQueryOption(CardExtraEffectChooseOneOption? option, bool fullText)
{
	if (option == null)
	{
		return CardEditorLoc.T("cardText.chooseOne.unknownOption", "Unknown Option");
	}

	CardExtraEffect queryEffect = BuildChooseOneQueryEffect(option);
	int count = Math.Clamp(option.QueryCount <= 0 ? 1 : option.QueryCount, 1, 99);
	string amountText = count.ToString(CultureInfo.InvariantCulture);
	string summary = option.QuerySource switch
	{
		CardExtraEffectChooseOneQuerySource.Compendium => FormatChooseOneCompendiumQuery(queryEffect, count, amountText, longForm: fullText),
		_ => FormatChooseOnePileQuery(queryEffect, count, amountText, longForm: fullText)
	};

	return NormalizeChooseOneSummaryText(summary);
}

private static string FormatChooseOnePileQuery(CardExtraEffect effect, int amount, string amountText, bool longForm)
{
	string summary = FormatPlayCardFromPile(null, effect, amount, amountText);
	if (!longForm && summary.StartsWith("Play ", StringComparison.OrdinalIgnoreCase))
	{
		summary = summary[5..];
	}

	return summary;
}

private static string FormatChooseOneCompendiumQuery(CardExtraEffect effect, int amount, string amountText, bool longForm)
{
	string descriptor = BuildCardSelectionDescriptor(effect, plural: amount != 1, preferAllCardsKeyword: true);
	string summary = effect.CardSelectionMode switch
	{
		CardExtraEffectCardSelectionMode.All => CardEditorLoc.F(
			"cardText.chooseOne.query.compendium.all",
			$"Play all {descriptor}.",
			("Descriptor", descriptor)),
		CardExtraEffectCardSelectionMode.UpTo => CardEditorLoc.F(
			"cardText.chooseOne.query.compendium.upTo",
			$"Play up to {amountText} {descriptor}.",
			("Amount", amountText),
			("Descriptor", descriptor)),
		CardExtraEffectCardSelectionMode.Random when amount == 1 => CardEditorLoc.F(
			"cardText.chooseOne.query.compendium.random.one",
			$"Play a random {descriptor}.",
			("Descriptor", descriptor)),
		CardExtraEffectCardSelectionMode.Random => CardEditorLoc.F(
			"cardText.chooseOne.query.compendium.random.many",
			$"Play {amountText} random {descriptor}.",
			("Amount", amountText),
			("Descriptor", descriptor)),
		_ when amount == 1 => CardEditorLoc.F(
			"cardText.chooseOne.query.compendium.choose.one",
			$"Play a {descriptor}.",
			("Descriptor", descriptor)),
		_ => CardEditorLoc.F(
			"cardText.chooseOne.query.compendium.choose.many",
			$"Play {amountText} {descriptor}.",
			("Amount", amountText),
			("Descriptor", descriptor))
	};

	if (!longForm && summary.StartsWith("Play ", StringComparison.OrdinalIgnoreCase))
	{
		summary = summary[5..];
	}

	return summary;
}

private static string BuildChooseOneOptionSummary(CardModel card, Creature? target, bool isUpgradePreview, CardExtraEffectChooseOneOption option)
{
	if (option.Mode == CardExtraEffectChooseOneOptionMode.MatchingCards)
	{
		return DescribeChooseOneQueryOption(option, option.ShowFullText);
	}

	if (TryGetChooseOneExactSourceId(option, out ModelId sourceId))
	{
		if (!option.ShowFullText)
		{
			string? title = ResolveSpecificCardTitle(sourceId.ToString());
			if (!string.IsNullOrWhiteSpace(title))
			{
				return title.Trim();
			}
		}

		string? description = CardEditorCreatedCardEffectSourceSupport.GetSingleEffectSourceDescription(card, target, isUpgradePreview, sourceId);
		if (!string.IsNullOrWhiteSpace(description))
		{
			return NormalizeChooseOneSummaryText(description);
		}

		string? fallbackTitle = ResolveSpecificCardTitle(sourceId.ToString());
		if (!string.IsNullOrWhiteSpace(fallbackTitle))
		{
			return fallbackTitle.Trim();
		}
	}

	return CardEditorLoc.T("cardText.chooseOne.unknownOption", "Unknown Option");
}

	private static string JoinChooseOneOptionSummaries(IReadOnlyList<string> options)
	{
		return options.Count switch
		{
			0 => CardEditorLoc.T("cardText.chooseOne.none", "no options"),
			1 => options[0],
			2 => $"{options[0]} or {options[1]}",
			_ => $"{options[0]}, {options[1]}, or {options[2]}"
		};
	}

	private static string FormatCardCostDelta(CardModel card, int signedAmount, string? durationSuffix, int upgradeHighlightComparison)
	{
		int magnitude = Math.Abs(signedAmount);
		string amountText = BuildEnergyIcons(card, magnitude);
		if (upgradeHighlightComparison != 0)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, upgradeHighlightComparison);
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

	private static string FormatCardCostDeltaText(string amountText, bool isLess, string? durationSuffix, int upgradeHighlightComparison)
	{
		if (upgradeHighlightComparison != 0)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, upgradeHighlightComparison);
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

	private static string BuildHalfEnergyCostText(CardModel? card)
	{
		return CardEditorLoc.T("cardText.costModifier.half", "half") + " " + BuildEnergyIcons(card, 1);
	}

	private static string BuildHalfStarCostText()
	{
		return CardEditorLoc.T("cardText.costModifier.half", "half") + " " + BuildStarIcons(1);
	}

	private static string HighlightCostModifierText(string text, int upgradeHighlightComparison)
	{
		return upgradeHighlightComparison != 0
			? StsTextUtilities.HighlightChangeText(text, upgradeHighlightComparison)
			: text;
	}

	private static string FormatCardCostModifier(CardModel card, CardExtraEffectCostModifier modifier, string? durationSuffix, int upgradeHighlightComparison)
	{
		if (modifier == CardExtraEffectCostModifier.FreeToPlay)
		{
			string freeText = HighlightCostModifierText(CardEditorLoc.T("cardText.costModifier.freeToPlay", "free to play"), upgradeHighlightComparison);

			return durationSuffix != null
				? CardEditorLoc.F("cardText.cardCostDelta.freeToPlay.duration", $"This card is {freeText} {durationSuffix}.", ("Free", freeText), ("Duration", durationSuffix))
				: CardEditorLoc.F("cardText.cardCostDelta.freeToPlay", $"This card is {freeText}.", ("Free", freeText));
		}
		if (modifier == CardExtraEffectCostModifier.Free)
		{
			string freeText = HighlightCostModifierText(BuildEnergyIcons(card, 0), upgradeHighlightComparison);

			return durationSuffix != null
				? CardEditorLoc.F("cardText.cardCostDelta.free.duration", $"This card costs {freeText} {durationSuffix}.", ("Free", freeText), ("Duration", durationSuffix))
				: CardEditorLoc.F("cardText.cardCostDelta.free", $"This card costs {freeText}.", ("Free", freeText));
		}

		string halfText = HighlightCostModifierText(BuildHalfEnergyCostText(card), upgradeHighlightComparison);

		return durationSuffix != null
			? CardEditorLoc.F("cardText.cardCostDelta.half.duration", $"This card costs {halfText} {durationSuffix}.", ("Half", halfText), ("Duration", durationSuffix))
			: CardEditorLoc.F("cardText.cardCostDelta.half", $"This card costs {halfText}.", ("Half", halfText));
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

	private static string FormatCardTypeCostDelta(CardModel card, CardExtraEffect effect, int signedAmount, string? durationSuffix, int upgradeHighlightComparison)
	{
		string affected = BuildAffectedCardTypeText(effect);

		int magnitude = Math.Abs(signedAmount);
		string amountText = BuildEnergyIcons(card, magnitude);
		if (upgradeHighlightComparison != 0)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, upgradeHighlightComparison);
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

	private static string FormatCardTypeCostDeltaText(CardExtraEffect effect, string amountText, bool isLess, string? durationSuffix, int upgradeHighlightComparison)
	{
		string affected = BuildAffectedCardTypeText(effect);
		if (upgradeHighlightComparison != 0)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, upgradeHighlightComparison);
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

	private static string FormatCardTypeCostModifier(CardModel card, CardExtraEffect effect, CardExtraEffectCostModifier modifier, string? durationSuffix, int upgradeHighlightComparison)
	{
		string affected = BuildAffectedCardTypeText(effect);
		if (modifier == CardExtraEffectCostModifier.FreeToPlay)
		{
			string freeText = HighlightCostModifierText(CardEditorLoc.T("cardText.costModifier.freeToPlay", "free to play"), upgradeHighlightComparison);

			return durationSuffix != null
				? CardEditorLoc.F("cardText.cardTypeCostDelta.freeToPlay.duration", $"{affected} are {freeText} {durationSuffix}.", ("Affected", affected), ("Free", freeText), ("Duration", durationSuffix))
				: CardEditorLoc.F("cardText.cardTypeCostDelta.freeToPlay", $"{affected} are {freeText}.", ("Affected", affected), ("Free", freeText));
		}
		if (modifier == CardExtraEffectCostModifier.Free)
		{
			string freeText = HighlightCostModifierText(BuildEnergyIcons(card, 0), upgradeHighlightComparison);

			return durationSuffix != null
				? CardEditorLoc.F("cardText.cardTypeCostDelta.free.duration", $"{affected} cost {freeText} {durationSuffix}.", ("Affected", affected), ("Free", freeText), ("Duration", durationSuffix))
				: CardEditorLoc.F("cardText.cardTypeCostDelta.free", $"{affected} cost {freeText}.", ("Affected", affected), ("Free", freeText));
		}

		string halfText = HighlightCostModifierText(BuildHalfEnergyCostText(card), upgradeHighlightComparison);

		return durationSuffix != null
			? CardEditorLoc.F("cardText.cardTypeCostDelta.half.duration", $"{affected} cost {halfText} {durationSuffix}.", ("Affected", affected), ("Half", halfText), ("Duration", durationSuffix))
			: CardEditorLoc.F("cardText.cardTypeCostDelta.half", $"{affected} cost {halfText}.", ("Affected", affected), ("Half", halfText));
	}

	private static string FormatCardTypeStarCostDelta(CardExtraEffect effect, int signedAmount, string? durationSuffix, int upgradeHighlightComparison)
	{
		string affected = BuildAffectedCardTypeText(effect);

		int magnitude = Math.Abs(signedAmount);
		string amountText = BuildStarIcons(magnitude);
		if (upgradeHighlightComparison != 0)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, upgradeHighlightComparison);
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

	private static string FormatCardTypeStarCostDeltaText(CardExtraEffect effect, string amountText, bool isLess, string? durationSuffix, int upgradeHighlightComparison)
	{
		string affected = BuildAffectedCardTypeText(effect);
		if (upgradeHighlightComparison != 0)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, upgradeHighlightComparison);
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

	private static string FormatCardTypeStarCostModifier(CardExtraEffect effect, CardExtraEffectCostModifier modifier, string? durationSuffix, int upgradeHighlightComparison)
	{
		string affected = BuildAffectedCardTypeText(effect);
		if (modifier == CardExtraEffectCostModifier.FreeToPlay)
		{
			string freeText = HighlightCostModifierText(CardEditorLoc.T("cardText.costModifier.freeToPlay", "free to play"), upgradeHighlightComparison);

			return durationSuffix != null
				? CardEditorLoc.F("cardText.cardTypeStarCostDelta.freeToPlay.duration", $"{affected} are {freeText} {durationSuffix}.", ("Affected", affected), ("Free", freeText), ("Duration", durationSuffix))
				: CardEditorLoc.F("cardText.cardTypeStarCostDelta.freeToPlay", $"{affected} are {freeText}.", ("Affected", affected), ("Free", freeText));
		}
		if (modifier == CardExtraEffectCostModifier.Free)
		{
			string noStarsText = HighlightCostModifierText(BuildStarIcons(0), upgradeHighlightComparison);

			return durationSuffix != null
				? CardEditorLoc.F("cardText.cardTypeStarCostDelta.free.duration", $"{affected} cost {noStarsText} {durationSuffix}.", ("Affected", affected), ("NoStars", noStarsText), ("Duration", durationSuffix))
				: CardEditorLoc.F("cardText.cardTypeStarCostDelta.free", $"{affected} cost {noStarsText}.", ("Affected", affected), ("NoStars", noStarsText));
		}

		string halfStarsText = HighlightCostModifierText(BuildHalfStarCostText(), upgradeHighlightComparison);

		return durationSuffix != null
			? CardEditorLoc.F("cardText.cardTypeStarCostDelta.half.duration", $"{affected} cost {halfStarsText} {durationSuffix}.", ("Affected", affected), ("HalfStars", halfStarsText), ("Duration", durationSuffix))
			: CardEditorLoc.F("cardText.cardTypeStarCostDelta.half", $"{affected} cost {halfStarsText}.", ("Affected", affected), ("HalfStars", halfStarsText));
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

	private static string FormatDrawnCardsCostDelta(CardModel card, CardExtraEffect effect, int signedAmount, string? durationSuffix, int upgradeHighlightComparison)
	{
		string affected = BuildAffectedCardTypeText(effect);
		string fromPile = BuildDrawnFromPileSuffix(effect);

		int magnitude = Math.Abs(signedAmount);
		string amountText = BuildEnergyIcons(card, magnitude);
		if (upgradeHighlightComparison != 0)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, upgradeHighlightComparison);
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

	private static string FormatDrawnCardsCostDeltaText(CardExtraEffect effect, string amountText, bool isLess, string? durationSuffix, int upgradeHighlightComparison)
	{
		string affected = BuildAffectedCardTypeText(effect);
		string fromPile = BuildDrawnFromPileSuffix(effect);
		if (upgradeHighlightComparison != 0)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, upgradeHighlightComparison);
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

	private static string FormatDrawnCardsCostModifier(CardModel card, CardExtraEffect effect, CardExtraEffectCostModifier modifier, string? durationSuffix, int upgradeHighlightComparison)
	{
		string affected = BuildAffectedCardTypeText(effect);
		string drawn = CardEditorLoc.T("cardText.drawn", "drawn");
		string fromPile = BuildDrawnFromPileSuffix(effect);
		if (modifier == CardExtraEffectCostModifier.FreeToPlay)
		{
			string freeText = HighlightCostModifierText(CardEditorLoc.T("cardText.costModifier.freeToPlay", "free to play"), upgradeHighlightComparison);

			return durationSuffix != null
				? CardEditorLoc.F("cardText.drawnCardsCostDelta.freeToPlay.duration", $"{affected} {drawn}{fromPile} are {freeText} {durationSuffix}.", ("Affected", affected), ("Drawn", drawn), ("FromPile", fromPile), ("Free", freeText), ("Duration", durationSuffix))
				: CardEditorLoc.F("cardText.drawnCardsCostDelta.freeToPlay", $"{affected} {drawn}{fromPile} are {freeText}.", ("Affected", affected), ("Drawn", drawn), ("FromPile", fromPile), ("Free", freeText));
		}
		if (modifier == CardExtraEffectCostModifier.Free)
		{
			string freeText = HighlightCostModifierText(BuildEnergyIcons(card, 0), upgradeHighlightComparison);

			return durationSuffix != null
				? CardEditorLoc.F("cardText.drawnCardsCostDelta.free.duration", $"{affected} {drawn}{fromPile} cost {freeText} {durationSuffix}.", ("Affected", affected), ("Drawn", drawn), ("FromPile", fromPile), ("Free", freeText), ("Duration", durationSuffix))
				: CardEditorLoc.F("cardText.drawnCardsCostDelta.free", $"{affected} {drawn}{fromPile} cost {freeText}.", ("Affected", affected), ("Drawn", drawn), ("FromPile", fromPile), ("Free", freeText));
		}

		string halfText = HighlightCostModifierText(BuildHalfEnergyCostText(card), upgradeHighlightComparison);

		return durationSuffix != null
			? CardEditorLoc.F("cardText.drawnCardsCostDelta.half.duration", $"{affected} {drawn}{fromPile} cost {halfText} {durationSuffix}.", ("Affected", affected), ("Drawn", drawn), ("FromPile", fromPile), ("Half", halfText), ("Duration", durationSuffix))
			: CardEditorLoc.F("cardText.drawnCardsCostDelta.half", $"{affected} {drawn}{fromPile} cost {halfText}.", ("Affected", affected), ("Drawn", drawn), ("FromPile", fromPile), ("Half", halfText));
	}

	private static string FormatGeneratedCardsCostDelta(CardModel card, CardExtraEffect effect, int signedAmount, string? durationSuffix, int upgradeHighlightComparison)
	{
		string affected = BuildAffectedCardTypeText(effect);

		int magnitude = Math.Abs(signedAmount);
		string amountText = BuildEnergyIcons(card, magnitude);
		if (upgradeHighlightComparison != 0)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, upgradeHighlightComparison);
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

	private static string FormatGeneratedCardsCostDeltaText(CardExtraEffect effect, string amountText, bool isLess, string? durationSuffix, int upgradeHighlightComparison)
	{
		string affected = BuildAffectedCardTypeText(effect);
		if (upgradeHighlightComparison != 0)
		{
			amountText = StsTextUtilities.HighlightChangeText(amountText, upgradeHighlightComparison);
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

	private static string FormatGeneratedCardsCostModifier(CardModel card, CardExtraEffect effect, CardExtraEffectCostModifier modifier, string? durationSuffix, int upgradeHighlightComparison)
	{
		string affected = BuildAffectedCardTypeText(effect);
		string created = CardEditorLoc.T("cardText.created", "created");
		if (modifier == CardExtraEffectCostModifier.FreeToPlay)
		{
			string freeText = HighlightCostModifierText(CardEditorLoc.T("cardText.costModifier.freeToPlay", "free to play"), upgradeHighlightComparison);

			return durationSuffix != null
				? CardEditorLoc.F("cardText.generatedCardsCostDelta.freeToPlay.duration", $"{affected} {created} are {freeText} {durationSuffix}.", ("Affected", affected), ("Created", created), ("Free", freeText), ("Duration", durationSuffix))
				: CardEditorLoc.F("cardText.generatedCardsCostDelta.freeToPlay", $"{affected} {created} are {freeText}.", ("Affected", affected), ("Created", created), ("Free", freeText));
		}
		if (modifier == CardExtraEffectCostModifier.Free)
		{
			string freeText = HighlightCostModifierText(BuildEnergyIcons(card, 0), upgradeHighlightComparison);

			return durationSuffix != null
				? CardEditorLoc.F("cardText.generatedCardsCostDelta.free.duration", $"{affected} {created} cost {freeText} {durationSuffix}.", ("Affected", affected), ("Created", created), ("Free", freeText), ("Duration", durationSuffix))
				: CardEditorLoc.F("cardText.generatedCardsCostDelta.free", $"{affected} {created} cost {freeText}.", ("Affected", affected), ("Created", created), ("Free", freeText));
		}

		string halfText = HighlightCostModifierText(BuildHalfEnergyCostText(card), upgradeHighlightComparison);

		return durationSuffix != null
			? CardEditorLoc.F("cardText.generatedCardsCostDelta.half.duration", $"{affected} {created} cost {halfText} {durationSuffix}.", ("Affected", affected), ("Created", created), ("Half", halfText), ("Duration", durationSuffix))
			: CardEditorLoc.F("cardText.generatedCardsCostDelta.half", $"{affected} {created} cost {halfText}.", ("Affected", affected), ("Created", created), ("Half", halfText));
	}

	private static string FormatCardStarCostModifier(CardExtraEffectCostModifier modifier, string? durationSuffix, int upgradeHighlightComparison)
	{
		if (modifier == CardExtraEffectCostModifier.FreeToPlay)
		{
			string freeText = HighlightCostModifierText(CardEditorLoc.T("cardText.costModifier.freeToPlay", "free to play"), upgradeHighlightComparison);

			return durationSuffix != null
				? CardEditorLoc.F("cardText.cardStarCostDelta.freeToPlay.duration", $"This card is {freeText} {durationSuffix}.", ("Free", freeText), ("Duration", durationSuffix))
				: CardEditorLoc.F("cardText.cardStarCostDelta.freeToPlay", $"This card is {freeText}.", ("Free", freeText));
		}
		if (modifier == CardExtraEffectCostModifier.Free)
		{
			string noStarsText = HighlightCostModifierText(BuildStarIcons(0), upgradeHighlightComparison);

			return durationSuffix != null
				? CardEditorLoc.F("cardText.cardStarCostDelta.free.duration", $"This card costs {noStarsText} {durationSuffix}.", ("NoStars", noStarsText), ("Duration", durationSuffix))
				: CardEditorLoc.F("cardText.cardStarCostDelta.free", $"This card costs {noStarsText}.", ("NoStars", noStarsText));
		}

		string halfStarsText = HighlightCostModifierText(BuildHalfStarCostText(), upgradeHighlightComparison);

		return durationSuffix != null
			? CardEditorLoc.F("cardText.cardStarCostDelta.half.duration", $"This card costs {halfStarsText} {durationSuffix}.", ("HalfStars", halfStarsText), ("Duration", durationSuffix))
			: CardEditorLoc.F("cardText.cardStarCostDelta.half", $"This card costs {halfStarsText}.", ("HalfStars", halfStarsText));
	}

	private static string GetCardPilePossessive(CardExtraEffectCardPile pile)
	{
		if (pile == CardExtraEffectCardPile.AllPiles)
		{
			return CardEditorLoc.T("cardText.pile.all", "all of your piles");
		}
		string label = CardPileLabel(pile);
		return CardEditorLoc.F("cardText.pile.your", $"your {label}", ("Pile", label));
	}

	private static string GetCardPileBoundary(CardExtraEffectCardPile pile, bool top)
	{
		if (pile == CardExtraEffectCardPile.AllPiles)
		{
			return top
				? CardEditorLoc.T("cardText.pile.topOfAny", "the top of any pile")
				: CardEditorLoc.T("cardText.pile.bottomOfAny", "the bottom of any pile");
		}

		string possessive = GetCardPilePossessive(pile);
		return top
			? CardEditorLoc.F("cardText.pile.topOfSelected", $"the top of {possessive}", ("Pile", possessive))
			: CardEditorLoc.F("cardText.pile.bottomOfSelected", $"the bottom of {possessive}", ("Pile", possessive));
	}

	private static string GetCardPileLocationWithSelectionMode(CardExtraEffectCardPile pile, CardExtraEffectCardSelectionMode selectionMode)
	{
		return selectionMode switch
		{
			CardExtraEffectCardSelectionMode.Top => CardEditorLoc.F(
				"cardText.pile.atTopOfSelected",
				$"at {GetCardPileBoundary(pile, top: true)}",
				("Pile", GetCardPileBoundary(pile, top: true))),
			CardExtraEffectCardSelectionMode.Bottom => CardEditorLoc.F(
				"cardText.pile.atBottomOfSelected",
				$"at {GetCardPileBoundary(pile, top: false)}",
				("Pile", GetCardPileBoundary(pile, top: false))),
			_ => GetCardPileLocation(pile)
		};
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
			CardGeneratedCardType.Playable => "[gold]" + GetPlayableKeywordTitle() + "[/gold] ",
			CardGeneratedCardType.Status => GeneratedCardTypeLabel(CardGeneratedCardType.Status) + " ",
			CardGeneratedCardType.Curse => GeneratedCardTypeLabel(CardGeneratedCardType.Curse) + " ",
			CardGeneratedCardType.Quest => GeneratedCardTypeLabel(CardGeneratedCardType.Quest) + " ",
			_ => string.Empty
		};

		string prefix = (poolAdj + typeAdj).Trim();
		return string.IsNullOrEmpty(prefix) ? string.Empty : prefix + " ";
	}

	private static string BuildGeneratedCardCustomTagSuffix(CardExtraEffect effect)
	{
		string tag = effect.GeneratedCardCustomTag?.Trim() ?? string.Empty;
		return string.IsNullOrWhiteSpace(tag)
			? string.Empty
			: CardEditorLoc.F("cardText.generate.customTagSuffix", " with \"{Tag}\" tag", ("Tag", tag));
	}

	private static string BuildGeneratedCardPoolSuffix(CardGeneratedCardPool pool)
	{
		return pool switch
		{
			CardGeneratedCardPool.OtherColors => CardEditorLoc.T("cardText.poolSuffix.otherColors", " from other characters"),
			CardGeneratedCardPool.Any => CardEditorLoc.T("cardText.poolSuffix.allColors", " from any character"),
			CardGeneratedCardPool.All => CardEditorLoc.T("cardText.poolSuffix.allPools", " from all cards"),
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

	private static void TryGetScaledAmountText(CardModel card, CardExtraEffect effect, int baseAmount, Creature? target, int upgradeHighlightComparison, out string amountText)
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
			int comparison = upgradeHighlightComparison != 0 ? upgradeHighlightComparison : previewInt.CompareTo(baseInt);
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
				? CardEditorLoc.T("cardText.timing.start.eachTurnCombatPrefix", "At the start of each turn this combat: ")
				: (turns == 1
					? CardEditorLoc.T("cardText.timing.start.nextTurnPrefix", "At the start of your next turn: ")
					: CardEditorLoc.F("cardText.timing.start.nextTurnsPrefix", $"At the start of your next {turns.ToString(CultureInfo.InvariantCulture)} turns: ", ("Turns", turns))),
			CardExtraEffectTiming.EndOfTurn => turns == 0
				? CardEditorLoc.T("cardText.timing.end.startingNextTurnEachCombatPrefix", "Starting next turn, at the end of each turn this combat: ")
				: (turns == 1
					? CardEditorLoc.T("cardText.timing.end.nextTurnPrefix", "At the end of your next turn: ")
					: CardEditorLoc.F("cardText.timing.end.nextTurnsPrefix", $"At the end of your next {turns.ToString(CultureInfo.InvariantCulture)} turns: ", ("Turns", turns))),
			CardExtraEffectTiming.EndOfThisTurn => turns == 0
				? CardEditorLoc.T("cardText.timing.endEachCombatPrefix", "At the end of each turn this combat: ")
				: (turns == 1
					? CardEditorLoc.T("cardText.timing.endThisTurnPrefix", "At the end of this turn: ")
					: (turns == 2
						? CardEditorLoc.T("cardText.timing.endThisTurnAndNextPrefix", "At the end of this turn and your next turn: ")
						: CardEditorLoc.F("cardText.timing.endThisTurnAndNextTurnsPrefix", $"At the end of this turn and your next {(turns - 1).ToString(CultureInfo.InvariantCulture)} turns: ", ("Turns", turns - 1)))),
			CardExtraEffectTiming.StartOfEnemyTurn => turns == 0
				? CardEditorLoc.T("cardText.timing.enemyStart.eachTurnCombatPrefix", "At the start of each enemy turn this combat: ")
				: (turns == 1
					? CardEditorLoc.T("cardText.timing.enemyStart.nextTurnPrefix", "At the start of the next enemy turn: ")
					: CardEditorLoc.F("cardText.timing.enemyStart.nextTurnsPrefix", $"At the start of the next {turns.ToString(CultureInfo.InvariantCulture)} enemy turns: ", ("Turns", turns))),
			CardExtraEffectTiming.EndOfEnemyTurn => turns == 0
				? CardEditorLoc.T("cardText.timing.enemyEnd.startingNextTurnEachCombatPrefix", "Starting next enemy turn, at the end of each enemy turn this combat: ")
				: (turns == 1
					? CardEditorLoc.T("cardText.timing.enemyEnd.nextTurnPrefix", "At the end of the next enemy turn: ")
					: CardEditorLoc.F("cardText.timing.enemyEnd.nextTurnsPrefix", $"At the end of the next {turns.ToString(CultureInfo.InvariantCulture)} enemy turns: ", ("Turns", turns))),
			CardExtraEffectTiming.StartOfAnyTurn => turns == 0
				? CardEditorLoc.T("cardText.timing.anyStart.eachTurnCombatPrefix", "At the start of each turn this combat: ")
				: (turns == 1
					? CardEditorLoc.T("cardText.timing.anyStart.nextTurnPrefix", "At the start of the next turn: ")
					: CardEditorLoc.F("cardText.timing.anyStart.nextTurnsPrefix", $"At the start of the next {turns.ToString(CultureInfo.InvariantCulture)} turns: ", ("Turns", turns))),
			CardExtraEffectTiming.EndOfAnyTurn => turns == 0
				? CardEditorLoc.T("cardText.timing.anyEnd.startingNextTurnEachCombatPrefix", "Starting next turn, at the end of each turn this combat: ")
				: (turns == 1
					? CardEditorLoc.T("cardText.timing.anyEnd.nextTurnPrefix", "At the end of the next turn: ")
					: CardEditorLoc.F("cardText.timing.anyEnd.nextTurnsPrefix", $"At the end of the next {turns.ToString(CultureInfo.InvariantCulture)} turns: ", ("Turns", turns))),
			CardExtraEffectTiming.EndOfThisAnyTurn => turns == 0
				? CardEditorLoc.T("cardText.timing.anyEndEachCombatPrefix", "At the end of each turn this combat: ")
				: (turns == 1
					? CardEditorLoc.T("cardText.timing.anyEndThisTurnPrefix", "At the end of this turn: ")
					: (turns == 2
						? CardEditorLoc.T("cardText.timing.anyEndThisTurnAndNextPrefix", "At the end of this turn and the next turn: ")
						: CardEditorLoc.F("cardText.timing.anyEndThisTurnAndNextTurnsPrefix", $"At the end of this turn and the next {(turns - 1).ToString(CultureInfo.InvariantCulture)} turns: ", ("Turns", turns - 1)))),
			_ => string.Empty
		};

		return string.IsNullOrEmpty(prefix) ? line : prefix + line;
	}

	private static string BuildEnergyIcons(CardModel? card, int amount)
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
		string icon = StarIconsFormatter.starIconSprite;
		if (amount <= 0 || amount >= 4)
		{
			return amount.ToString(CultureInfo.InvariantCulture) + icon;
		}
		return string.Concat(Enumerable.Repeat(icon, amount));
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
			CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.dealDamage.self", $"Take {amountText} damage.", ("Amount", amountText)),
			_ => CardEditorLoc.F("cardText.dealDamage.target", $"Deal {amountText} damage.", ("Amount", amountText))
		};
	}

	private static string FormatCardDealsExtraDamage(string amountText)
	{
		return CardEditorLoc.F(
			"cardText.cardDealsExtraDamage",
			$"This card deals {amountText} bonus damage.",
			("Amount", amountText));
	}

private static string FormatStatusToStatus(CardExtraEffect effect, string amountText)
{
	string destination = ResolvePowerTitle(effect?.PowerId) ?? CardEditorLoc.T("cardText.power.unknown", "Unknown Power");
	string source = GetConfiguredMultiplierSourceLabel(effect);
	string payload = $"[gold]{destination}[/gold]";
	bool lose = effect?.StatusToStatusMode == CardExtraEffectStatusToStatusMode.Lose;
	return effect.Target switch
	{
		CardExtraEffectTarget.AllEnemies => FormatStatusToStatusLine(
			lose ? "cardText.loseStatusEqual.allEnemies" : "cardText.gainStatusEqual.allEnemies",
			lose ? "ALL enemies lose {Payload} equal to {Factor}." : "ALL enemies gain {Payload} equal to {Factor}.",
			payload,
			amountText,
			source,
			lose,
			referenceSelf: false,
			referencePlural: true),
		CardExtraEffectTarget.RandomEnemy => FormatStatusToStatusLine(
			lose ? "cardText.loseStatusEqual.randomEnemy" : "cardText.gainStatusEqual.randomEnemy",
			lose ? "A random enemy loses {Payload} equal to {Factor}." : "A random enemy gains {Payload} equal to {Factor}.",
			payload,
			amountText,
			source,
			lose,
			referenceSelf: false,
			referencePlural: false),
		CardExtraEffectTarget.Self => FormatStatusToStatusLine(
			lose ? "cardText.loseStatusEqual.self" : "cardText.gainStatusEqual.self",
			lose ? "Lose {Payload} equal to {Factor}." : "Gain {Payload} equal to {Factor}.",
			payload,
			amountText,
			source,
			lose,
			referenceSelf: true,
			referencePlural: false),
		_ => FormatStatusToStatusLine(
			lose ? "cardText.loseStatusEqual.target" : "cardText.gainStatusEqual.target",
			lose ? "The target loses {Payload} equal to {Factor}." : "The target gains {Payload} equal to {Factor}.",
			payload,
			amountText,
			source,
			lose,
			referenceSelf: false,
			referencePlural: false)
	};
}

private static string FormatStatusToStatusLine(
	string key,
	string template,
	string payload,
	string amountText,
	string source,
	bool lose,
	bool referenceSelf,
	bool referencePlural)
{
	string sourceText = referenceSelf
		? CardEditorLoc.F(
			lose ? "cardText.loseStatusEqual.source.self" : "cardText.gainStatusEqual.source.self",
			"your {Source}",
			("Source", source))
		: referencePlural
			? CardEditorLoc.F(
				lose ? "cardText.loseStatusEqual.source.their" : "cardText.gainStatusEqual.source.their",
				"their {Source}",
				("Source", source))
			: CardEditorLoc.F(
				lose ? "cardText.loseStatusEqual.source.its" : "cardText.gainStatusEqual.source.its",
				"its {Source}",
				("Source", source));

	string factorText = amountText == "1"
		? sourceText
		: CardEditorLoc.F(
			lose ? "cardText.loseStatusEqual.factor.multiple" : "cardText.gainStatusEqual.factor.multiple",
			$"{amountText} times {sourceText}",
			("Amount", amountText),
			("SourceText", sourceText));

	return CardEditorLoc.F(
		key,
		template.Replace("{Payload}", payload).Replace("{Factor}", factorText),
		("Payload", payload),
		("Factor", factorText));
}

private static string GetConfiguredMultiplierSourceLabel(CardExtraEffect? effect)
{
	if (effect?.MultiplierSourceMode == CardExtraEffectValueSourceMode.PowerStatus)
	{
		return ResolvePowerTitle(effect.MultiplierPowerId) ?? CardEditorLoc.T("cardText.power.unknown", "Unknown Power");
	}

	return MultiplierStatLabel(effect?.MultiplierStat ?? CardExtraEffectMultiplierStat.Strength);
}

	private static string GetConfiguredMultiplierSourceRichText(CardExtraEffect? effect)
	{
		if (effect?.MultiplierSourceMode == CardExtraEffectValueSourceMode.PowerStatus)
		{
			string title = ResolvePowerTitle(effect.MultiplierPowerId) ?? CardEditorLoc.T("cardText.power.unknown", "Unknown Power");
			return $"[gold]{title}[/gold]";
		}

		return GetMultiplierStatRichText(effect?.MultiplierStat ?? CardExtraEffectMultiplierStat.Strength);
	}

	private static string FormatGainBlock(CardExtraEffectTarget target, string amountText)
	{
		return target switch
		{
			CardExtraEffectTarget.AllEnemies => CardEditorLoc.F("cardText.gainBlock.allEnemies", $"ALL enemies gain {amountText} [gold]Block[/gold].", ("Amount", amountText)),
			CardExtraEffectTarget.RandomEnemy => CardEditorLoc.F("cardText.gainBlock.randomEnemy", $"A random enemy gains {amountText} [gold]Block[/gold].", ("Amount", amountText)),
			CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.gainBlock.self", $"Gain {amountText} [gold]Block[/gold].", ("Amount", amountText)),
			_ => CardEditorLoc.F("cardText.gainBlock.target", $"The target gains {amountText} [gold]Block[/gold].", ("Amount", amountText))
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
			CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.applyDebuff.self", $"Gain {payload}{suffixPart}", ("Payload", payload), ("Suffix", suffixPart)),
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
			CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.applyDebuff.self", $"Gain {payload}{suffixPart}", ("Payload", payload), ("Suffix", suffixPart)),
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
			_ => CardEditorLoc.F("cardText.signedPower.target", $"The target {verbSingular} {payload}{suffixPart}", ("Verb", verbSingular), ("Payload", payload), ("Suffix", suffixPart))
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
			_ => CardEditorLoc.F("cardText.signedPower.target", $"The target {verbSingular} {payload}{suffixPart}", ("Verb", verbSingular), ("Payload", payload), ("Suffix", suffixPart))
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
			_ => CardEditorLoc.F("cardText.gainPower.target", $"The target gains {payload}{suffixPart}", ("Payload", payload), ("Suffix", suffixPart))
		};
	}

	private static string FormatRemovePowerText(CardExtraEffectTarget target, string amountText, string powerName, string? suffix = null)
	{
		string title = PowerTitle(powerName, powerName);
		string payload = $"{amountText} [gold]{title}[/gold]";
		string suffixPart = BuildSuffixPart(suffix);
		return target switch
		{
			CardExtraEffectTarget.AllEnemies => CardEditorLoc.F("cardText.removePower.allEnemies", $"Remove {payload} from ALL enemies{suffixPart}", ("Payload", payload), ("Suffix", suffixPart)),
			CardExtraEffectTarget.RandomEnemy => CardEditorLoc.F("cardText.removePower.randomEnemy", $"Remove {payload} from a random enemy{suffixPart}", ("Payload", payload), ("Suffix", suffixPart)),
			CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.removePower.self", $"Remove {payload}{suffixPart}", ("Payload", payload), ("Suffix", suffixPart)),
			_ => CardEditorLoc.F("cardText.removePower.target", $"Remove {payload} from the target{suffixPart}", ("Payload", payload), ("Suffix", suffixPart))
		};
	}

	private static string FormatCleansePowers(CardExtraEffectTarget target, string family)
	{
		return target switch
		{
			CardExtraEffectTarget.AllEnemies => CardEditorLoc.F("cardText.cleansePowers.allEnemies", $"Cleanse all {family} from ALL enemies.", ("Family", family)),
			CardExtraEffectTarget.RandomEnemy => CardEditorLoc.F("cardText.cleansePowers.randomEnemy", $"Cleanse all {family} from a random enemy.", ("Family", family)),
			CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.cleansePowers.self", $"Cleanse all {family}.", ("Family", family)),
			_ => CardEditorLoc.F("cardText.cleansePowers.target", $"Cleanse all {family} from the target.", ("Family", family))
		};
	}

	private static string FormatApplyPower(CardExtraEffect effect, string amountText)
	{
		string powerName = ResolvePowerTitle(effect?.PowerId) ?? CardEditorLoc.T("cardText.power.unknown", "Unknown Power");
		string payload = $"{amountText} [gold]{powerName}[/gold]";

		return effect.Target switch
		{
			CardExtraEffectTarget.AllEnemies => CardEditorLoc.F("cardText.applyPower.allEnemies", $"Apply {payload} to ALL enemies.", ("Payload", payload)),
			CardExtraEffectTarget.RandomEnemy => CardEditorLoc.F("cardText.applyPower.randomEnemy", $"Apply {payload} to a random enemy.", ("Payload", payload)),
			CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.applyPower.self", $"Gain {payload}.", ("Payload", payload)),
			_ => CardEditorLoc.F("cardText.applyPower.target", $"Apply {payload}.", ("Payload", payload))
		};
	}

	private static string FormatGrantReplay(string amountText)
	{
		string replay = CardEditorLoc.T("cardText.replay", "Replay");
		string payload = $"{amountText} [gold]{replay}[/gold]";
		return CardEditorLoc.F("cardText.grantReplay", $"Gain {payload}.", ("Payload", payload));
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

	private static string FormatMultiplyStatStatus(CardExtraEffect effect, string factorText)
	{
		string statText = GetConfiguredMultiplierSourceRichText(effect);
		return effect.Target switch
		{
			CardExtraEffectTarget.AllEnemies => CardEditorLoc.F("cardText.multiplyStat.allEnemies", $"Multiply ALL enemies' {statText} by {factorText}.", ("Stat", statText), ("Amount", factorText)),
			CardExtraEffectTarget.RandomEnemy => CardEditorLoc.F("cardText.multiplyStat.randomEnemy", $"Multiply a random enemy's {statText} by {factorText}.", ("Stat", statText), ("Amount", factorText)),
			CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.multiplyStat.self", $"Multiply your {statText} by {factorText}.", ("Stat", statText), ("Amount", factorText)),
			_ => CardEditorLoc.F("cardText.multiplyStat.target", $"Multiply the target's {statText} by {factorText}.", ("Stat", statText), ("Amount", factorText))
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
			CardExtraEffectKind.PlayRandomGeneratedCard => false,
			CardExtraEffectKind.ChooseOneEffectSource => false,
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
			CardExtraEffectKind.DoesNotConsumeVigor => false,
			CardExtraEffectKind.HitsAllEnemies => false,
			CardExtraEffectKind.CardDealsExtraDamage => false,
			CardExtraEffectKind.SelfScaling => false,
			CardExtraEffectKind.PersistentSelfScaling => false,
			CardExtraEffectKind.ScalingStage => false,
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
			return Math.Clamp(ResolveXAmountWithPlus(cardPlay, effect.RepeatCount), 0, 99);
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
			? CardEditorLoc.F("cardText.repeat.xPlusTimes", $"{FormatXPlusText(effect.RepeatCount)} times", ("Times", FormatXPlusText(effect.RepeatCount)))
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

	private static string? GetConditionalBonusPowerText(CardExtraEffect effect)
	{
		string? powerName = effect?.Kind switch
		{
			CardExtraEffectKind.GainStrength or CardExtraEffectKind.LoseStrength => "Strength",
			CardExtraEffectKind.GainDexterity or CardExtraEffectKind.LoseDexterity => "Dexterity",
			CardExtraEffectKind.GainFocus or CardExtraEffectKind.LoseFocus => "Focus",
			CardExtraEffectKind.ApplyWeak or CardExtraEffectKind.RemoveWeak => "Weak",
			CardExtraEffectKind.ApplyFrail or CardExtraEffectKind.RemoveFrail => "Frail",
			CardExtraEffectKind.ApplyVulnerable or CardExtraEffectKind.RemoveVulnerable => "Vulnerable",
			CardExtraEffectKind.ApplyPoison or CardExtraEffectKind.RemovePoison => "Poison",
			CardExtraEffectKind.ApplyDoom or CardExtraEffectKind.RemoveDoom => "Doom",
			CardExtraEffectKind.ApplyConstrict or CardExtraEffectKind.RemoveConstrict => "Constrict",
			CardExtraEffectKind.GainArtifact or CardExtraEffectKind.RemoveArtifact => "Artifact",
			CardExtraEffectKind.GainThorns or CardExtraEffectKind.RemoveThorns => "Thorns",
			CardExtraEffectKind.GainRegen or CardExtraEffectKind.RemoveRegen => "Regen",
			CardExtraEffectKind.GainPlating or CardExtraEffectKind.RemovePlating => "Plating",
			CardExtraEffectKind.GainIntangible or CardExtraEffectKind.RemoveIntangible => "Intangible",
			CardExtraEffectKind.GainBuffer or CardExtraEffectKind.RemoveBuffer => "Buffer",
			CardExtraEffectKind.GainVigor or CardExtraEffectKind.RemoveVigor => "Vigor",
			CardExtraEffectKind.GainBlur or CardExtraEffectKind.RemoveBlur => "Blur",
			CardExtraEffectKind.GainRitual or CardExtraEffectKind.RemoveRitual => "Ritual",
			CardExtraEffectKind.ApplyPower or CardExtraEffectKind.GainStatusEqualToStatus => ResolvePowerTitle(effect.PowerId),
			_ => null
		};

		return string.IsNullOrWhiteSpace(powerName) ? null : $"[gold]{powerName}[/gold]";
	}

	private static string BuildConditionalBonusActionText(CardModel? card, CardExtraEffect effect, int bonus)
	{
		int magnitude = Math.Abs(bonus);
		string amountText = magnitude.ToString(CultureInfo.InvariantCulture);
		string moreOrLess = bonus > 0
			? CardEditorLoc.T("cardText.conditional.more", "more")
			: CardEditorLoc.T("cardText.conditional.less", "less");

		string? powerText = GetConditionalBonusPowerText(effect);
		return effect.Kind switch
		{
			CardExtraEffectKind.DealDamage or CardExtraEffectKind.CardDealsExtraDamage => $"deal {amountText} {moreOrLess} damage",
			CardExtraEffectKind.GainBlock => $"gain {amountText} {moreOrLess} [gold]Block[/gold]",
			CardExtraEffectKind.RemoveBlock => $"remove {amountText} {moreOrLess} [gold]Block[/gold]",
			CardExtraEffectKind.Heal => $"heal {amountText} {moreOrLess} HP",
			CardExtraEffectKind.LoseHp => $"lose {amountText} {moreOrLess} HP",
			CardExtraEffectKind.GainMaxHp => $"gain {amountText} {moreOrLess} Max HP",
			CardExtraEffectKind.LoseMaxHp => $"lose {amountText} {moreOrLess} Max HP",
			CardExtraEffectKind.GainEnergy => $"gain {BuildEnergyIcons(card, magnitude)} {moreOrLess}",
			CardExtraEffectKind.LoseEnergy => $"lose {BuildEnergyIcons(card, magnitude)} {moreOrLess}",
			CardExtraEffectKind.GainStars => $"gain {BuildStarIcons(magnitude)} {moreOrLess}",
			CardExtraEffectKind.LoseStars => $"lose {BuildStarIcons(magnitude)} {moreOrLess}",
			CardExtraEffectKind.GainGold => $"gain {amountText} {moreOrLess} Gold",
			CardExtraEffectKind.LoseGold => $"lose {amountText} {moreOrLess} Gold",
			CardExtraEffectKind.ApplyPower or CardExtraEffectKind.ApplyWeak or CardExtraEffectKind.ApplyFrail
				or CardExtraEffectKind.ApplyVulnerable or CardExtraEffectKind.ApplyPoison
				or CardExtraEffectKind.ApplyDoom or CardExtraEffectKind.ApplyConstrict
					when powerText != null => $"apply {amountText} {moreOrLess} {powerText}",
			CardExtraEffectKind.GainStrength or CardExtraEffectKind.GainDexterity or CardExtraEffectKind.GainFocus
				or CardExtraEffectKind.GainArtifact or CardExtraEffectKind.GainThorns or CardExtraEffectKind.GainRegen
				or CardExtraEffectKind.GainPlating or CardExtraEffectKind.GainIntangible or CardExtraEffectKind.GainBuffer
				or CardExtraEffectKind.GainVigor or CardExtraEffectKind.GainBlur or CardExtraEffectKind.GainRitual
					when powerText != null => $"gain {amountText} {moreOrLess} {powerText}",
			CardExtraEffectKind.GainStatusEqualToStatus when powerText != null => effect.StatusToStatusMode == CardExtraEffectStatusToStatusMode.Lose
				? $"lose {amountText} {moreOrLess} {powerText}"
				: $"gain {amountText} {moreOrLess} {powerText}",
			CardExtraEffectKind.LoseStrength or CardExtraEffectKind.LoseDexterity or CardExtraEffectKind.LoseFocus
				or CardExtraEffectKind.RemoveWeak or CardExtraEffectKind.RemoveFrail or CardExtraEffectKind.RemoveVulnerable
				or CardExtraEffectKind.RemovePoison or CardExtraEffectKind.RemoveDoom or CardExtraEffectKind.RemoveConstrict
				or CardExtraEffectKind.RemoveArtifact or CardExtraEffectKind.RemoveThorns or CardExtraEffectKind.RemoveRegen
				or CardExtraEffectKind.RemovePlating or CardExtraEffectKind.RemoveIntangible or CardExtraEffectKind.RemoveBuffer
				or CardExtraEffectKind.RemoveVigor or CardExtraEffectKind.RemoveBlur or CardExtraEffectKind.RemoveRitual
					when powerText != null => $"lose {amountText} {moreOrLess} {powerText}",
			CardExtraEffectKind.MultiplyStatStatus => bonus > 0
				? $"increase this multiplier by {amountText}"
				: $"reduce this multiplier by {amountText}",
			_ => bonus > 0
				? $"increase this effect by {amountText}"
				: $"reduce this effect by {amountText}"
		};
	}

	private static string ApplyConditionalBonusSuffix(CardModel? card, string baseLine, CardExtraEffect effect)
	{
		if (string.IsNullOrWhiteSpace(baseLine) || effect == null)
		{
			return baseLine;
		}

		int bonus = effect.ConditionalBonusAmount;
		if (bonus == 0)
		{
			return baseLine;
		}

		string conditionText = BuildConditionalBonusConditionText(null, effect);
		if (string.IsNullOrWhiteSpace(conditionText))
		{
			return baseLine;
		}

		string bonusText = BuildConditionalBonusActionText(card, effect, bonus);

		string suffix = CardEditorLoc.F(
			"cardText.conditional.bonus",
			$"If {conditionText}, {bonusText}.",
			("Condition", conditionText),
			("Bonus", bonusText));

		string[] split = baseLine.Split('\n', 2);
		string first = split[0].TrimEnd();
		first = first.EndsWith('.') ? first + " " + suffix : first + ". " + suffix;
		return split.Length == 2 ? first + "\n" + split[1] : first;
	}

	private static CardExtraEffectBranchConditionType GetEffectiveConditionalBonusConditionType(CardExtraEffect? effect)
	{
		if (effect == null)
		{
			return CardExtraEffectBranchConditionType.None;
		}

		if (effect.ConditionalBonusConditionType != CardExtraEffectBranchConditionType.None)
		{
			return effect.ConditionalBonusConditionType;
		}

		return effect.ConditionalBonusCondition != CardExtraEffectConditionalBonusCondition.None
			? CardExtraEffectBranchConditionType.TargetCheck
			: CardExtraEffectBranchConditionType.None;
	}

	private static string BuildConditionalBonusConditionText(CardModel? card, CardExtraEffect effect)
	{
		return GetEffectiveConditionalBonusConditionType(effect) switch
		{
			CardExtraEffectBranchConditionType.TargetCheck => BuildConditionalConditionText(
				effect.ConditionalBonusCondition,
				effect.ConditionalBonusEnemyStatus,
				effect.ConditionalBonusEnemyIntent,
				effect.ConditionalBonusPowerId),
			CardExtraEffectBranchConditionType.HistoryCount => BuildCountConditionClause(card, effect),
			_ => string.Empty
		};
	}

	private static bool DoesConditionalBonusPass(CombatState combatState, Creature ownerCreature, CardPlay cardPlay, CardExtraEffect effect)
	{
		return GetEffectiveConditionalBonusConditionType(effect) switch
		{
			CardExtraEffectBranchConditionType.TargetCheck => DoesConditionalConditionPass(
				combatState,
				ownerCreature,
				cardPlay,
				effect.ConditionalBonusCondition,
				effect.ConditionalBonusEnemyStatus,
				effect.ConditionalBonusEnemyIntent,
				effect.ConditionalBonusPowerId),
			CardExtraEffectBranchConditionType.HistoryCount => DoesCountConditionPass(
				GetHistoryCountMultiplier(combatState, ownerCreature, cardPlay, effect),
				effect),
			_ => false
		};
	}

	private static string ApplyConditionalBranchSuffix(CardModel card, Creature? target, string baseLine, CardExtraEffect effect, int upgradeHighlightComparison, bool isUpgradePreview)
	{
		if (string.IsNullOrWhiteSpace(baseLine) || effect == null)
		{
			return baseLine;
		}

		CardExtraEffect? branchEffect = GetUsableBranchEffect(effect);
		if (branchEffect == null)
		{
			return baseLine;
		}

		string conditionText = BuildBranchConditionText(card, effect);
		if (string.IsNullOrWhiteSpace(conditionText))
		{
			return baseLine;
		}

		string? branchLine = TryFormatLine(card, branchEffect, target, upgradeHighlightComparison, isUpgradePreview);
		if (string.IsNullOrWhiteSpace(branchLine))
		{
			branchLine = BuildBranchFallbackLine(branchEffect);
			if (string.IsNullOrWhiteSpace(branchLine))
			{
				return baseLine;
			}
		}

		string suffix = effect.BranchMode switch
		{
			CardExtraEffectBranchMode.InsteadIf => CardEditorLoc.F(
				"cardText.branch.instead",
				branchLine.Contains('\n')
					? $"If {conditionText}, instead:\n{branchLine}"
					: $"If {conditionText}, {LowercaseFirst(TrimTrailingSentencePunctuation(branchLine))} instead.",
				("Condition", conditionText),
				("Effect", branchLine)),
			CardExtraEffectBranchMode.AlsoIf => CardEditorLoc.F(
				"cardText.branch.also",
				branchLine.Contains('\n')
					? $"If {conditionText}, also:\n{branchLine}"
					: $"If {conditionText}, also {LowercaseFirst(TrimTrailingSentencePunctuation(branchLine))}.",
				("Condition", conditionText),
				("Effect", branchLine)),
			_ => string.Empty
		};

		return string.IsNullOrWhiteSpace(suffix) ? baseLine : baseLine + "\n" + suffix;
	}

	private static string? BuildBranchFallbackLine(CardExtraEffect branchEffect)
	{
		if (branchEffect == null)
		{
			return null;
		}

		if (branchEffect.Kind == CardExtraEffectKind.RunEffectSourceCard
			&& !string.IsNullOrWhiteSpace(branchEffect.SpecificCardId))
		{
			string cardName = GetCardReferenceDisplayMode(branchEffect) == CardExtraEffectCardReferenceDisplayMode.FullText
				? BuildSpecificCardReferenceText(branchEffect.SpecificCardId, CardExtraEffectCardReferenceDisplayMode.FullText)
				: (ResolveSpecificCardTitle(branchEffect.SpecificCardId) ?? branchEffect.SpecificCardId.Trim());
			return CardEditorLoc.F(
				"cardText.branch.effectSourceFallback",
				$"run {cardName}",
				("Card", cardName));
		}

		string fallbackLabel = Definitions.FirstOrDefault(def => def.Kind == branchEffect.Kind)?.Label
			?? branchEffect.Kind.ToString();
		string localizedLabel = CardEditorLoc.T($"effectKind.{branchEffect.Kind}", fallbackLabel);
		return CardEditorLoc.F(
			"cardText.branch.effectKindFallback",
			$"{localizedLabel}",
			("Effect", localizedLabel));
	}

	private static string BuildBranchConditionText(CardModel? card, CardExtraEffect effect)
	{
		if (effect == null)
		{
			return string.Empty;
		}

		return effect.BranchConditionType switch
		{
			CardExtraEffectBranchConditionType.TargetCheck => BuildConditionalConditionText(effect.BranchCondition, effect.BranchEnemyStatus, effect.BranchEnemyIntent, effect.BranchPowerId),
			CardExtraEffectBranchConditionType.HistoryCount => TryBuildBranchCountConditionEffect(effect, out CardExtraEffect branchCountEffect)
				? BuildCountConditionClause(card, branchCountEffect)
				: string.Empty,
			_ => string.Empty
		};
	}

	private static string BuildConditionalConditionText(
		CardExtraEffectConditionalBonusCondition condition,
		CardExtraEffectEnemyStatus enemyStatus,
		CardExtraEffectEnemyIntent enemyIntent,
		string? powerId = null)
	{
		string statusLabel = !string.IsNullOrWhiteSpace(powerId)
			? (ResolvePowerTitle(powerId) ?? CardEditorLoc.T("cardText.power.unknown", "Unknown Power"))
			: EnemyStatusLabel(enemyStatus);

		return condition switch
		{
			CardExtraEffectConditionalBonusCondition.TargetHasBlock => CardEditorLoc.T("cardText.conditional.targetHasBlock", "the target has Block"),
			CardExtraEffectConditionalBonusCondition.TargetHasStatus => CardEditorLoc.F(
				"cardText.conditional.targetHasStatus",
				$"the target has {statusLabel}",
				("Status", statusLabel)),
			CardExtraEffectConditionalBonusCondition.TargetHasIntent => CardEditorLoc.F(
				"cardText.conditional.targetHasIntent",
				$"the target intends to {EnemyIntentLabel(enemyIntent)}",
				("Intent", EnemyIntentLabel(enemyIntent))),
			CardExtraEffectConditionalBonusCondition.SelfHasBlock => CardEditorLoc.T("cardText.conditional.selfHasBlock", "you have Block"),
			CardExtraEffectConditionalBonusCondition.SelfHasStatus => CardEditorLoc.F(
				"cardText.conditional.selfHasStatus",
				$"you have {statusLabel}",
				("Status", statusLabel)),
			CardExtraEffectConditionalBonusCondition.TargetHasNoBlock => CardEditorLoc.T("cardText.conditional.targetHasNoBlock", "the target has no Block"),
			CardExtraEffectConditionalBonusCondition.SelfHasNoBlock => CardEditorLoc.T("cardText.conditional.selfHasNoBlock", "you have no Block"),
			CardExtraEffectConditionalBonusCondition.TargetLacksStatus => CardEditorLoc.F(
				"cardText.conditional.targetLacksStatus",
				$"the target does not have {statusLabel}",
				("Status", statusLabel)),
			CardExtraEffectConditionalBonusCondition.SelfLacksStatus => CardEditorLoc.F(
				"cardText.conditional.selfLacksStatus",
				$"you do not have {statusLabel}",
				("Status", statusLabel)),
			CardExtraEffectConditionalBonusCondition.TargetIntentIsNot => CardEditorLoc.F(
				"cardText.conditional.targetIntentIsNot",
				$"the target does not intend to {EnemyIntentLabel(enemyIntent)}",
				("Intent", EnemyIntentLabel(enemyIntent))),
			CardExtraEffectConditionalBonusCondition.TargetIsDamaged => CardEditorLoc.T("cardText.conditional.targetIsDamaged", "the target is damaged"),
			CardExtraEffectConditionalBonusCondition.SelfIsDamaged => CardEditorLoc.T("cardText.conditional.selfIsDamaged", "you are damaged"),
			CardExtraEffectConditionalBonusCondition.TargetIsBloodied => CardEditorLoc.T("cardText.conditional.targetIsBloodied", "the target is bloodied"),
			CardExtraEffectConditionalBonusCondition.SelfIsBloodied => CardEditorLoc.T("cardText.conditional.selfIsBloodied", "you are bloodied"),
			CardExtraEffectConditionalBonusCondition.TargetIsFullHp => CardEditorLoc.T("cardText.conditional.targetIsFullHp", "the target is at full HP"),
			CardExtraEffectConditionalBonusCondition.SelfIsFullHp => CardEditorLoc.T("cardText.conditional.selfIsFullHp", "you are at full HP"),
			CardExtraEffectConditionalBonusCondition.TargetIsNotBloodied => CardEditorLoc.T("cardText.conditional.targetIsNotBloodied", "the target is not bloodied"),
			CardExtraEffectConditionalBonusCondition.SelfIsNotBloodied => CardEditorLoc.T("cardText.conditional.selfIsNotBloodied", "you are not bloodied"),
			CardExtraEffectConditionalBonusCondition.TargetHasLessHpThanYou => CardEditorLoc.T("cardText.conditional.targetHasLessHpThanYou", "the target has less HP than you"),
			CardExtraEffectConditionalBonusCondition.TargetHasMoreHpThanYou => CardEditorLoc.T("cardText.conditional.targetHasMoreHpThanYou", "the target has more HP than you"),
			CardExtraEffectConditionalBonusCondition.TargetHasLessBlockThanYou => CardEditorLoc.T("cardText.conditional.targetHasLessBlockThanYou", "the target has less Block than you"),
			CardExtraEffectConditionalBonusCondition.TargetHasMoreBlockThanYou => CardEditorLoc.T("cardText.conditional.targetHasMoreBlockThanYou", "the target has more Block than you"),
			_ => string.Empty
		};
	}

	private static bool HasUsableBranchCondition(CardExtraEffect effect)
	{
		if (effect == null)
		{
			return false;
		}

		return effect.BranchConditionType switch
		{
			CardExtraEffectBranchConditionType.TargetCheck => effect.BranchCondition != CardExtraEffectConditionalBonusCondition.None,
			CardExtraEffectBranchConditionType.HistoryCount => true,
			_ => false
		};
	}

	private static bool TryBuildBranchCountConditionEffect(CardExtraEffect effect, out CardExtraEffect branchCountEffect)
	{
		branchCountEffect = null!;
		if (effect == null || effect.BranchConditionType != CardExtraEffectBranchConditionType.HistoryCount)
		{
			return false;
		}

		branchCountEffect = new CardExtraEffect
		{
			ScaleMode = CardExtraEffectScaleMode.ConditionOnly,
			CountEvent = effect.BranchCountEvent,
			CountWindow = effect.BranchCountWindow,
			CountWindowInclusion = effect.BranchCountWindowInclusion,
			BlockLostCountingMode = effect.BranchBlockLostCountingMode,
			CountTurns = effect.BranchCountTurns,
			CountCardPile = effect.BranchCountCardPile,
			CountCardPool = effect.BranchCountCardPool,
			CountCardType = effect.BranchCountCardType,
			CountCardFilter = effect.BranchCountCardFilter,
			CountAggregationMode = GetEffectiveBranchCountAggregationMode(effect),
			CountExcludeSourceCard = effect.BranchCountExcludeSourceCard,
			CountOrbType = effect.BranchCountOrbType,
			CountOrbSelection = effect.BranchCountOrbSelection,
			CountEnemyStatus = effect.BranchCountEnemyStatus,
			CountPowerId = effect.BranchCountPowerId,
			CountEnemyIntent = effect.BranchCountEnemyIntent,
			CountComparison = effect.BranchCountComparison,
			CountConditionAmount = effect.BranchCountConditionAmount,
			CostFilterEnabled = effect.CostFilterEnabled,
			CostFilterMode = effect.CostFilterMode,
			CostFilterMax = effect.CostFilterMax,
			CountOnlyBlockCards = effect.BranchCountCardFilter == CardExtraEffectCountCardFilter.GainBlock,
			CountUsesCardEffectAmount = GetEffectiveBranchCountAggregationMode(effect) == CardExtraEffectCountAggregationMode.MatchingEffectAmount
		};
		return true;
	}

	private static bool DoesConditionalConditionPass(
		CombatState combatState,
		Creature ownerCreature,
		CardPlay cardPlay,
		CardExtraEffectConditionalBonusCondition condition,
		CardExtraEffectEnemyStatus enemyStatus,
		CardExtraEffectEnemyIntent enemyIntent,
		string? powerId = null)
	{
		if (combatState == null || ownerCreature == null || cardPlay == null || condition == CardExtraEffectConditionalBonusCondition.None)
		{
			return false;
		}

		return condition switch
		{
			CardExtraEffectConditionalBonusCondition.TargetHasBlock => GetRelevantEnemyConditionTargets(combatState, cardPlay)
				.Any(enemy => enemy != null && enemy.IsAlive && enemy.Block > 0),
			CardExtraEffectConditionalBonusCondition.TargetHasStatus => GetRelevantEnemyConditionTargets(combatState, cardPlay)
				.Any(enemy => enemy != null && enemy.IsAlive && EnemyHasConfiguredStatus(enemy, enemyStatus, powerId)),
			CardExtraEffectConditionalBonusCondition.TargetHasIntent => GetRelevantEnemyConditionTargets(combatState, cardPlay)
				.Any(enemy => enemy != null && enemy.IsAlive && EnemyHasIntent(enemy, enemyIntent)),
			CardExtraEffectConditionalBonusCondition.SelfHasBlock => ownerCreature.Block > 0,
			CardExtraEffectConditionalBonusCondition.SelfHasStatus => CreatureHasConfiguredStatus(ownerCreature, enemyStatus, powerId),
			CardExtraEffectConditionalBonusCondition.TargetHasNoBlock => GetRelevantEnemyConditionTargets(combatState, cardPlay)
				.Any(enemy => enemy != null && enemy.IsAlive && enemy.Block <= 0),
			CardExtraEffectConditionalBonusCondition.SelfHasNoBlock => ownerCreature.Block <= 0,
			CardExtraEffectConditionalBonusCondition.TargetLacksStatus => GetRelevantEnemyConditionTargets(combatState, cardPlay)
				.Any(enemy => enemy != null && enemy.IsAlive && !EnemyHasConfiguredStatus(enemy, enemyStatus, powerId)),
			CardExtraEffectConditionalBonusCondition.SelfLacksStatus => !CreatureHasConfiguredStatus(ownerCreature, enemyStatus, powerId),
			CardExtraEffectConditionalBonusCondition.TargetIntentIsNot => GetRelevantEnemyConditionTargets(combatState, cardPlay)
				.Any(enemy => enemy != null && enemy.IsAlive && !EnemyHasIntent(enemy, enemyIntent)),
			CardExtraEffectConditionalBonusCondition.TargetIsDamaged => GetRelevantEnemyConditionTargets(combatState, cardPlay)
				.Any(enemy => enemy != null && enemy.IsAlive && enemy.CurrentHp < enemy.MaxHp),
			CardExtraEffectConditionalBonusCondition.SelfIsDamaged => ownerCreature.CurrentHp < ownerCreature.MaxHp,
			CardExtraEffectConditionalBonusCondition.TargetIsBloodied => GetRelevantEnemyConditionTargets(combatState, cardPlay)
				.Any(enemy => enemy != null && enemy.IsAlive && enemy.CurrentHp * 2 <= enemy.MaxHp),
			CardExtraEffectConditionalBonusCondition.SelfIsBloodied => ownerCreature.CurrentHp * 2 <= ownerCreature.MaxHp,
			CardExtraEffectConditionalBonusCondition.TargetIsFullHp => GetRelevantEnemyConditionTargets(combatState, cardPlay)
				.Any(enemy => enemy != null && enemy.IsAlive && enemy.CurrentHp >= enemy.MaxHp),
			CardExtraEffectConditionalBonusCondition.SelfIsFullHp => ownerCreature.CurrentHp >= ownerCreature.MaxHp,
			CardExtraEffectConditionalBonusCondition.TargetIsNotBloodied => GetRelevantEnemyConditionTargets(combatState, cardPlay)
				.Any(enemy => enemy != null && enemy.IsAlive && enemy.CurrentHp * 2 > enemy.MaxHp),
			CardExtraEffectConditionalBonusCondition.SelfIsNotBloodied => ownerCreature.CurrentHp * 2 > ownerCreature.MaxHp,
			CardExtraEffectConditionalBonusCondition.TargetHasLessHpThanYou => GetRelevantEnemyConditionTargets(combatState, cardPlay)
				.Any(enemy => enemy != null && enemy.IsAlive && enemy.CurrentHp < ownerCreature.CurrentHp),
			CardExtraEffectConditionalBonusCondition.TargetHasMoreHpThanYou => GetRelevantEnemyConditionTargets(combatState, cardPlay)
				.Any(enemy => enemy != null && enemy.IsAlive && enemy.CurrentHp > ownerCreature.CurrentHp),
			CardExtraEffectConditionalBonusCondition.TargetHasLessBlockThanYou => GetRelevantEnemyConditionTargets(combatState, cardPlay)
				.Any(enemy => enemy != null && enemy.IsAlive && enemy.Block < ownerCreature.Block),
			CardExtraEffectConditionalBonusCondition.TargetHasMoreBlockThanYou => GetRelevantEnemyConditionTargets(combatState, cardPlay)
				.Any(enemy => enemy != null && enemy.IsAlive && enemy.Block > ownerCreature.Block),
			_ => false
		};
	}

	private static bool DoesBranchConditionPass(
		CombatState combatState,
		Creature ownerCreature,
		CardPlay cardPlay,
		CardExtraEffect effect)
	{
		if (effect == null || combatState == null || ownerCreature == null || cardPlay == null)
		{
			return false;
		}

		return effect.BranchConditionType switch
		{
			CardExtraEffectBranchConditionType.TargetCheck => DoesConditionalConditionPass(
				combatState,
				ownerCreature,
				cardPlay,
				effect.BranchCondition,
				effect.BranchEnemyStatus,
				effect.BranchEnemyIntent,
				effect.BranchPowerId),
			CardExtraEffectBranchConditionType.HistoryCount => TryBuildBranchCountConditionEffect(effect, out CardExtraEffect branchCountEffect)
				&& DoesCountConditionPass(GetHistoryCountMultiplier(combatState, ownerCreature, cardPlay, branchCountEffect), branchCountEffect),
			_ => false
		};
	}

	private static CardExtraEffect? GetUsableBranchEffect(CardExtraEffect effect)
	{
		if (effect == null
			|| effect.BranchMode == CardExtraEffectBranchMode.None
			|| !HasUsableBranchCondition(effect)
			|| effect.BranchEffect == null)
		{
			return null;
		}

		if (effect.BranchEffect.Kind == CardExtraEffectKind.RunEffectSourceCard
			&& string.IsNullOrWhiteSpace(effect.BranchEffect.SpecificCardId))
		{
			return null;
		}

		if (effect.BranchEffect.BranchMode == CardExtraEffectBranchMode.None && effect.BranchEffect.BranchEffect == null)
		{
			return effect.BranchEffect;
		}

		CardExtraEffect sanitized = CloneEffect(effect.BranchEffect);
		sanitized.BranchMode = CardExtraEffectBranchMode.None;
		sanitized.BranchConditionType = CardExtraEffectBranchConditionType.None;
		sanitized.BranchCondition = CardExtraEffectConditionalBonusCondition.None;
		sanitized.BranchEnemyStatus = default;
		sanitized.BranchPowerId = null;
		sanitized.BranchEnemyIntent = default;
		sanitized.BranchCountEvent = CardExtraEffectCountEvent.Played;
		sanitized.BranchCountWindow = CardExtraEffectCountWindow.ThisCombat;
		sanitized.BranchCountWindowInclusion = CardExtraEffectCountWindowInclusion.IncludeThisTurn;
		sanitized.BranchBlockLostCountingMode = CardExtraEffectBlockLostCountingMode.DamageAndEffects;
		sanitized.BranchCountTurns = 1;
		sanitized.BranchCountCardPile = CardExtraEffectCardPile.Hand;
		sanitized.BranchCountCardPool = CardGeneratedCardPool.All;
		sanitized.BranchCountCardType = CardGeneratedCardType.Any;
		sanitized.BranchCountCardFilter = CardExtraEffectCountCardFilter.Any;
		sanitized.BranchCountAggregationMode = CardExtraEffectCountAggregationMode.CardCount;
		sanitized.BranchCountUsesCardEffectAmount = false;
		sanitized.BranchCountExcludeSourceCard = false;
		sanitized.BranchCountOrbType = CardExtraEffectOrbType.Any;
		sanitized.BranchCountOrbSelection = CardExtraEffectOrbSelection.Leftmost;
		sanitized.BranchCountEnemyStatus = CardExtraEffectEnemyStatus.Weak;
		sanitized.BranchCountPowerId = null;
		sanitized.BranchCountEnemyIntent = CardExtraEffectEnemyIntent.Attack;
		sanitized.BranchCountComparison = CardExtraEffectCountComparison.None;
		sanitized.BranchCountConditionAmount = 1;
		sanitized.BranchEffect = null;
		return sanitized;
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

	private static async Task ReducePowerWithTemporaryTracking<TPower, TTracker>(Creature target, int amount, Creature applier, CardModel? cardSource)
		where TPower : PowerModel
		where TTracker : PowerModel
	{
		if (target == null || amount <= 0)
		{
			return;
		}

		if (cardSource?.Owner?.Creature != null
			&& ReferenceEquals(target, cardSource.Owner.Creature)
			&& ((TryGetProtectedMultiplierStat(typeof(TPower), out CardExtraEffectMultiplierStat protectedStat)
					&& CardHasRuntimeProtectedMultiplierStat(cardSource, protectedStat))
				|| CardHasRuntimeProtectedConfiguredPower(cardSource, ModelDb.GetId<TPower>())))
		{
			return;
		}

		TPower? power = target.GetPower<TPower>();
		TTracker? tracker = target.GetPower<TTracker>();
		int currentAmount = power?.Amount ?? 0;
		if (currentAmount <= 0)
		{
			if (tracker != null)
			{
				await PowerCmd.Remove(tracker);
			}
			return;
		}

		int removedAmount = Math.Min(amount, currentAmount);
		if (removedAmount >= currentAmount)
		{
			if (power != null)
			{
				await PowerCmd.Remove(power);
			}
			if (tracker != null)
			{
				await PowerCmd.Remove(tracker);
			}
			return;
		}

		if (power != null)
		{
			await PowerCmd.ModifyAmount(power, -removedAmount, applier, cardSource);
		}

		if (tracker == null)
		{
			return;
		}

		if (tracker.Amount <= removedAmount)
		{
			await PowerCmd.Remove(tracker);
		}
		else
		{
			await PowerCmd.ModifyAmount(tracker, -removedAmount, applier, cardSource);
		}
	}

	private static async Task RemovePowerFromTargets<TPower, TTracker>(CombatState combatState, Creature ownerCreature, CardPlay cardPlay, CardExtraEffectTarget target, int amount)
		where TPower : PowerModel
		where TTracker : PowerModel
	{
		foreach (Creature creature in ResolveTargets(combatState, ownerCreature, cardPlay, target))
		{
			await ReducePowerWithTemporaryTracking<TPower, TTracker>(creature, amount, ownerCreature, cardPlay.Card);
		}
	}

	private static async Task CleanseDebuffs(Creature target, Creature applier, CardModel? cardSource)
	{
		await ReducePowerWithTemporaryTracking<WeakPower, CardEditorTempWeakTrackerPower>(target, int.MaxValue, applier, cardSource);
		await ReducePowerWithTemporaryTracking<FrailPower, CardEditorTempFrailTrackerPower>(target, int.MaxValue, applier, cardSource);
		await ReducePowerWithTemporaryTracking<VulnerablePower, CardEditorTempVulnerableTrackerPower>(target, int.MaxValue, applier, cardSource);
		await ReducePowerWithTemporaryTracking<PoisonPower, CardEditorTempPoisonTrackerPower>(target, int.MaxValue, applier, cardSource);
		await ReducePowerWithTemporaryTracking<DoomPower, CardEditorTempDoomTrackerPower>(target, int.MaxValue, applier, cardSource);
		await ReducePowerWithTemporaryTracking<ConstrictPower, CardEditorTempConstrictTrackerPower>(target, int.MaxValue, applier, cardSource);
	}

	private static async Task CleanseBuffs(Creature target, Creature applier, CardModel? cardSource)
	{
		await ReducePowerWithTemporaryTracking<ArtifactPower, CardEditorTempArtifactTrackerPower>(target, int.MaxValue, applier, cardSource);
		await ReducePowerWithTemporaryTracking<ThornsPower, CardEditorTempThornsTrackerPower>(target, int.MaxValue, applier, cardSource);
		await ReducePowerWithTemporaryTracking<RegenPower, CardEditorTempRegenTrackerPower>(target, int.MaxValue, applier, cardSource);
		await ReducePowerWithTemporaryTracking<PlatingPower, CardEditorTempPlatingTrackerPower>(target, int.MaxValue, applier, cardSource);
		await ReducePowerWithTemporaryTracking<IntangiblePower, CardEditorTempIntangibleTrackerPower>(target, int.MaxValue, applier, cardSource);
		await ReducePowerWithTemporaryTracking<BufferPower, CardEditorTempBufferTrackerPower>(target, int.MaxValue, applier, cardSource);
		await ReducePowerWithTemporaryTracking<VigorPower, CardEditorTempVigorTrackerPower>(target, int.MaxValue, applier, cardSource);
		await ReducePowerWithTemporaryTracking<BlurPower, CardEditorTempBlurTrackerPower>(target, int.MaxValue, applier, cardSource);
		await ReducePowerWithTemporaryTracking<RitualPower, CardEditorTempRitualTrackerPower>(target, int.MaxValue, applier, cardSource);
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

	private static async Task MultiplyConfiguredPowerStatus(Creature target, CardExtraEffect effect, int factor, Creature applier, CardModel? cardSource, CardPlay cardPlay)
	{
		if (target == null || effect == null || factor <= 1)
		{
			return;
		}

		if (!TryResolveConfiguredPowerModel(effect.MultiplierPowerId, out PowerModel? canonical) || canonical == null)
		{
			return;
		}

		if (ShouldPreserveSelfProtectedPower(cardPlay, applier, effect.Target, canonical))
		{
			return;
		}

		if (TryGetProtectedMultiplierStat(canonical.GetType(), out CardExtraEffectMultiplierStat builtInStat))
		{
			await MultiplyStatStatus(target, builtInStat, factor, applier, cardSource, cardPlay);
			return;
		}

		PowerModel? active = target.Powers?.FirstOrDefault(power => power != null
			&& (Equals(power.Id, canonical.Id)
				|| (power is ITemporaryPower temporary && Equals(temporary.InternallyAppliedPower?.Id, canonical.Id))));
		if (active == null || active.Amount == 0)
		{
			return;
		}

		int delta = GetMultiplierDelta(active.Amount, factor);
		if (delta == 0)
		{
			return;
		}

		if (active is ITemporaryPower temporaryPower)
		{
			temporaryPower.IgnoreNextInstance();
		}

		await PowerCmd.ModifyAmount(active, delta, applier, cardSource);
	}

	private static async Task MultiplyStatStatus(Creature target, CardExtraEffect effect, int factor, Creature applier, CardModel? cardSource, CardPlay cardPlay)
	{
		if (target == null || effect == null || factor <= 1)
		{
			return;
		}

		if (effect.MultiplierSourceMode == CardExtraEffectValueSourceMode.PowerStatus)
		{
			await MultiplyConfiguredPowerStatus(target, effect, factor, applier, cardSource, cardPlay);
			return;
		}

		await MultiplyStatStatus(target, effect.MultiplierStat, factor, applier, cardSource, cardPlay);
	}

	private static int GetMultiplierStatAmount(Creature target, CardExtraEffectMultiplierStat stat)
	{
		if (target == null)
		{
			return 0;
		}

		return stat switch
		{
			CardExtraEffectMultiplierStat.Block => (int)Math.Max(0, target.Block),
			CardExtraEffectMultiplierStat.Strength => Math.Max(0, target.GetPowerAmount<StrengthPower>()),
			CardExtraEffectMultiplierStat.Dexterity => Math.Max(0, target.GetPowerAmount<DexterityPower>()),
			CardExtraEffectMultiplierStat.Focus => Math.Max(0, target.GetPowerAmount<FocusPower>()),
			CardExtraEffectMultiplierStat.Weak => Math.Max(0, target.GetPowerAmount<WeakPower>()),
			CardExtraEffectMultiplierStat.Frail => Math.Max(0, target.GetPowerAmount<FrailPower>()),
			CardExtraEffectMultiplierStat.Vulnerable => Math.Max(0, target.GetPowerAmount<VulnerablePower>()),
			CardExtraEffectMultiplierStat.Poison => Math.Max(0, target.GetPowerAmount<PoisonPower>()),
			CardExtraEffectMultiplierStat.Doom => Math.Max(0, target.GetPowerAmount<DoomPower>()),
			CardExtraEffectMultiplierStat.Constrict => Math.Max(0, target.GetPowerAmount<ConstrictPower>()),
			CardExtraEffectMultiplierStat.Artifact => Math.Max(0, target.GetPowerAmount<ArtifactPower>()),
			CardExtraEffectMultiplierStat.Thorns => Math.Max(0, target.GetPowerAmount<ThornsPower>()),
			CardExtraEffectMultiplierStat.Regen => Math.Max(0, target.GetPowerAmount<RegenPower>()),
			CardExtraEffectMultiplierStat.Plating => Math.Max(0, target.GetPowerAmount<PlatingPower>()),
			CardExtraEffectMultiplierStat.Intangible => Math.Max(0, target.GetPowerAmount<IntangiblePower>()),
			CardExtraEffectMultiplierStat.Buffer => Math.Max(0, target.GetPowerAmount<BufferPower>()),
			CardExtraEffectMultiplierStat.Vigor => Math.Max(0, target.GetPowerAmount<VigorPower>()),
			CardExtraEffectMultiplierStat.Blur => Math.Max(0, target.GetPowerAmount<BlurPower>()),
			CardExtraEffectMultiplierStat.Ritual => Math.Max(0, target.GetPowerAmount<RitualPower>()),
			_ => 0
		};
	}

	private static bool TryResolveConfiguredPowerModel(string? powerId, out PowerModel? canonical)
	{
		canonical = null;
		if (string.IsNullOrWhiteSpace(powerId))
		{
			return false;
		}

		try
		{
			ModelId parsed = ModelId.Deserialize(powerId.Trim());
			canonical = ModelDb.GetByIdOrNull<PowerModel>(parsed);
		}
		catch
		{
			canonical = null;
		}

		return canonical != null;
	}

private static async Task GainStatusEqualToStatus(CombatState combatState, Creature ownerCreature, CardPlay cardPlay, CardExtraEffect effect, int factor)
{
	if (effect == null || factor <= 0)
	{
			return;
		}

		if (!TryResolveConfiguredPowerModel(effect.PowerId, out PowerModel? canonical) || canonical == null)
		{
			return;
		}

		foreach (Creature target in ResolveTargets(combatState, ownerCreature, cardPlay, effect.Target))
		{
			if (target == null)
			{
				continue;
			}

			int sourceAmount = GetConfiguredMultiplierSourceAmount(target, effect);
			if (sourceAmount <= 0)
			{
				continue;
			}

			long total = (long)sourceAmount * factor;
			if (total <= 0)
			{
				continue;
			}

			PowerModel power = canonical.ToMutable();
			int appliedAmount = total >= int.MaxValue ? int.MaxValue : (int)total;
			if (effect.StatusToStatusMode == CardExtraEffectStatusToStatusMode.Lose)
			{
				if (ShouldPreserveSelfProtectedPower(cardPlay, ownerCreature, effect.Target, canonical))
				{
					continue;
				}

				appliedAmount = -appliedAmount;
			}

			await PowerCmd.Apply(power, target, appliedAmount, ownerCreature, cardPlay.Card);
		}
	}

	internal static Task ExecuteEffect(CombatState combatState, PlayerChoiceContext choiceContext, CardPlay cardPlay, CardExtraEffect effect, int triggerEventAmount = 1)
		=> ExecuteEffect(combatState, choiceContext, cardPlay, effect, branchDepth: 0, triggerEventAmount: triggerEventAmount);

	private static async Task ExecuteEffect(CombatState combatState, PlayerChoiceContext choiceContext, CardPlay cardPlay, CardExtraEffect effect, int branchDepth, int triggerEventAmount)
	{
		if (effect == null || branchDepth > 4)
		{
			return;
		}

		CardModel? card = cardPlay?.Card;
		Player? owner = card?.Owner;
		Creature? ownerCreature = owner?.Creature;
		CardExtraEffect? branchEffect = GetUsableBranchEffect(effect);
		bool shouldRunBranch = branchEffect != null
			&& cardPlay != null
			&& ownerCreature != null
			&& DoesBranchConditionPass(
				combatState,
				ownerCreature,
				cardPlay,
				effect);

		if (shouldRunBranch && effect.BranchMode == CardExtraEffectBranchMode.InsteadIf)
		{
			await ExecuteEffect(combatState, choiceContext, cardPlay, branchEffect!, branchDepth + 1, triggerEventAmount);
			return;
		}

		await ExecuteEffectCore(combatState, choiceContext, cardPlay, effect, triggerEventAmount);

		if (shouldRunBranch && effect.BranchMode == CardExtraEffectBranchMode.AlsoIf)
		{
			await ExecuteEffect(combatState, choiceContext, cardPlay, branchEffect!, branchDepth + 1, triggerEventAmount);
		}
	}

	private static IEnumerable<Creature> ResolveValueSourceCreatures(CombatState combatState, Creature ownerCreature, CardPlay cardPlay, CardExtraEffect effect)
	{
		if (combatState == null || ownerCreature == null || cardPlay == null || effect == null)
		{
			return Array.Empty<Creature>();
		}

		return effect.ValueSourceActor switch
		{
			CardExtraEffectValueSourceActor.Self => new[] { ownerCreature },
			CardExtraEffectValueSourceActor.Target => ResolveSingleTarget(combatState, ownerCreature, cardPlay) is Creature target && target != null
				? new[] { target }
				: Array.Empty<Creature>(),
			CardExtraEffectValueSourceActor.AllEnemies => combatState.GetOpponentsOf(ownerCreature).Where(c => c != null && c.IsAlive),
			CardExtraEffectValueSourceActor.AllAllies => ResolveFriendlyGroupTargets(combatState, ownerCreature, includeSelf: true),
			_ => new[] { ownerCreature }
		};
	}

	private static CardExtraEffectMultiplierStat? TryMapValueSourceKindToMultiplierStat(CardExtraEffectValueSourceKind kind)
	{
		return kind switch
		{
			CardExtraEffectValueSourceKind.Block => CardExtraEffectMultiplierStat.Block,
			CardExtraEffectValueSourceKind.Strength => CardExtraEffectMultiplierStat.Strength,
			CardExtraEffectValueSourceKind.Dexterity => CardExtraEffectMultiplierStat.Dexterity,
			CardExtraEffectValueSourceKind.Focus => CardExtraEffectMultiplierStat.Focus,
			CardExtraEffectValueSourceKind.Weak => CardExtraEffectMultiplierStat.Weak,
			CardExtraEffectValueSourceKind.Frail => CardExtraEffectMultiplierStat.Frail,
			CardExtraEffectValueSourceKind.Vulnerable => CardExtraEffectMultiplierStat.Vulnerable,
			CardExtraEffectValueSourceKind.Poison => CardExtraEffectMultiplierStat.Poison,
			CardExtraEffectValueSourceKind.Doom => CardExtraEffectMultiplierStat.Doom,
			CardExtraEffectValueSourceKind.Constrict => CardExtraEffectMultiplierStat.Constrict,
			CardExtraEffectValueSourceKind.Artifact => CardExtraEffectMultiplierStat.Artifact,
			CardExtraEffectValueSourceKind.Thorns => CardExtraEffectMultiplierStat.Thorns,
			CardExtraEffectValueSourceKind.Regen => CardExtraEffectMultiplierStat.Regen,
			CardExtraEffectValueSourceKind.Plating => CardExtraEffectMultiplierStat.Plating,
			CardExtraEffectValueSourceKind.Intangible => CardExtraEffectMultiplierStat.Intangible,
			CardExtraEffectValueSourceKind.Buffer => CardExtraEffectMultiplierStat.Buffer,
			CardExtraEffectValueSourceKind.Vigor => CardExtraEffectMultiplierStat.Vigor,
			CardExtraEffectValueSourceKind.Blur => CardExtraEffectMultiplierStat.Blur,
			CardExtraEffectValueSourceKind.Ritual => CardExtraEffectMultiplierStat.Ritual,
			_ => null
		};
	}

	private static int GetSpecificPowerValueSourceAmount(Creature creature, string? powerId)
	{
		if (creature == null || !TryResolveConfiguredPowerModel(powerId, out PowerModel? canonical) || canonical == null)
		{
			return 0;
		}

		PowerModel? active = creature.Powers?.FirstOrDefault(power => power != null && Equals(power.Id, canonical.Id));
		if (active == null)
		{
			return 0;
		}

		int amount = Math.Max(0, active.Amount);
		if (amount > 0)
		{
			return amount;
		}

		return active.StackType == PowerStackType.Single ? 1 : 0;
	}

	private static int GetConfiguredMultiplierSourceAmount(Creature target, CardExtraEffect effect)
	{
		if (target == null || effect == null)
		{
			return 0;
		}

		return effect.MultiplierSourceMode == CardExtraEffectValueSourceMode.PowerStatus
			? GetSpecificPowerValueSourceAmount(target, effect.MultiplierPowerId)
			: GetMultiplierStatAmount(target, effect.MultiplierStat);
	}

	private static int GetValueSourceAmount(Creature creature, CardExtraEffectValueSourceKind kind)
	{
		if (creature == null)
		{
			return 0;
		}

		return kind switch
		{
			CardExtraEffectValueSourceKind.CurrentHp => Math.Max(0, creature.CurrentHp),
			CardExtraEffectValueSourceKind.MaxHp => Math.Max(0, creature.MaxHp),
			CardExtraEffectValueSourceKind.MissingHp => Math.Max(0, creature.MaxHp - creature.CurrentHp),
			_ => TryMapValueSourceKindToMultiplierStat(kind) is CardExtraEffectMultiplierStat stat
				? GetMultiplierStatAmount(creature, stat)
				: 0
		};
	}

	private static int GetValueSourceAmount(Creature creature, CardExtraEffect effect)
	{
		if (effect == null || creature == null)
		{
			return 0;
		}

		return effect.ValueSourceMode == CardExtraEffectValueSourceMode.PowerStatus
			? GetSpecificPowerValueSourceAmount(creature, effect.ValueSourcePowerId)
			: GetValueSourceAmount(creature, effect.ValueSourceKind);
	}

	private static int AggregateValueSourceAmounts(IReadOnlyList<int> values, CardExtraEffectValueSourceAggregation aggregation)
	{
		if (values == null || values.Count == 0)
		{
			return 0;
		}

		return aggregation switch
		{
			CardExtraEffectValueSourceAggregation.Sum => ClampLongToInt(values.Aggregate(0L, (sum, value) => sum + Math.Max(0, value))),
			CardExtraEffectValueSourceAggregation.Highest => Math.Max(0, values.Max()),
			CardExtraEffectValueSourceAggregation.Lowest => Math.Max(0, values.Min()),
			CardExtraEffectValueSourceAggregation.Average => Math.Max(0, (int)Math.Round(values.Average(value => Math.Max(0, value)), MidpointRounding.AwayFromZero)),
			_ => Math.Max(0, values[0])
		};
	}

	private static int ResolveValueSourceAmount(CombatState combatState, Creature ownerCreature, CardPlay cardPlay, CardExtraEffect effect)
	{
		List<int> values = ResolveValueSourceCreatures(combatState, ownerCreature, cardPlay, effect)
			.Where(creature => creature != null)
			.Select(creature => GetValueSourceAmount(creature, effect))
			.ToList();

		if (values.Count == 0)
		{
			return 0;
		}

		bool usesGroupAggregation = effect.ValueSourceActor is CardExtraEffectValueSourceActor.AllEnemies or CardExtraEffectValueSourceActor.AllAllies;
		CardExtraEffectValueSourceAggregation aggregation = usesGroupAggregation
			? (effect.ValueSourceAggregation == CardExtraEffectValueSourceAggregation.Value
				? CardExtraEffectValueSourceAggregation.Sum
				: effect.ValueSourceAggregation)
			: CardExtraEffectValueSourceAggregation.Value;
		return AggregateValueSourceAmounts(values, aggregation);
	}

	private static int ResolveConfiguredEffectAmount(CombatState combatState, Creature ownerCreature, CardPlay cardPlay, CardExtraEffect effect)
	{
		if (UsesAppliedEffectRowAmountSource(effect))
		{
			return CardEditorEffectExecutionAmountContext.TryGetAppliedAmount(effect.AmountSourceEffectId, out int appliedAmount)
				? Math.Max(0, appliedAmount)
				: 0;
		}

		if (UsesValueSourceAmountSource(effect))
		{
			return ResolveValueSourceAmount(combatState, ownerCreature, cardPlay, effect);
		}

		return effect.AmountIsX
			? ResolveXAmountWithPlus(cardPlay, effect.AmountXPlus)
			: effect.Amount;
	}

	private static bool EffectUsesTargetCountForAppliedAmount(CardExtraEffectKind kind)
	{
		return kind is CardExtraEffectKind.GainBlock
			or CardExtraEffectKind.DealDamage
			or CardExtraEffectKind.Heal
			or CardExtraEffectKind.LoseHp
			or CardExtraEffectKind.GainStrength
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
			or CardExtraEffectKind.RemoveWeak
			or CardExtraEffectKind.RemoveFrail
			or CardExtraEffectKind.RemoveVulnerable
			or CardExtraEffectKind.RemovePoison
			or CardExtraEffectKind.RemoveDoom
			or CardExtraEffectKind.GainArtifact
			or CardExtraEffectKind.GainThorns
			or CardExtraEffectKind.GainRegen
			or CardExtraEffectKind.GainPlating
			or CardExtraEffectKind.GainIntangible
			or CardExtraEffectKind.GainBuffer
			or CardExtraEffectKind.GainVigor
			or CardExtraEffectKind.GainBlur
			or CardExtraEffectKind.GainRitual
			or CardExtraEffectKind.RemoveArtifact
			or CardExtraEffectKind.RemoveThorns
			or CardExtraEffectKind.RemoveRegen
			or CardExtraEffectKind.RemovePlating
			or CardExtraEffectKind.RemoveIntangible
			or CardExtraEffectKind.RemoveBuffer
			or CardExtraEffectKind.RemoveVigor
			or CardExtraEffectKind.RemoveBlur
			or CardExtraEffectKind.RemoveRitual
			or CardExtraEffectKind.ApplyConstrict
			or CardExtraEffectKind.RemoveConstrict
			or CardExtraEffectKind.ApplyPower
			or CardExtraEffectKind.GainStatusEqualToStatus
			or CardExtraEffectKind.RemoveBlock;
	}

	private static int EstimateAppliedTargetCount(CombatState combatState, Creature ownerCreature, CardPlay cardPlay, CardExtraEffectTarget requestedTarget)
	{
		CardExtraEffectTarget target = GetEffectiveResolvedTarget(cardPlay, requestedTarget);
		return target switch
		{
			CardExtraEffectTarget.Self => 1,
			CardExtraEffectTarget.AllAllies => ResolveFriendlyGroupTargets(combatState, ownerCreature, includeSelf: true).Count(),
			CardExtraEffectTarget.AnyPlayer => ResolveFriendlySingleTarget(combatState, ownerCreature, cardPlay, includeSelf: true) != null ? 1 : 0,
			CardExtraEffectTarget.AnyAlly => ResolveFriendlySingleTarget(combatState, ownerCreature, cardPlay, includeSelf: false) != null ? 1 : 0,
			CardExtraEffectTarget.AllEnemies => combatState.GetOpponentsOf(ownerCreature).Count(c => c.IsAlive),
			CardExtraEffectTarget.RandomEnemy => combatState.GetOpponentsOf(ownerCreature).Any(c => c.IsAlive) ? 1 : 0,
			_ => ResolveSingleTarget(combatState, ownerCreature, cardPlay) != null ? 1 : 0
		};
	}

	private static int EstimateFallbackAppliedAmount(CombatState combatState, Creature ownerCreature, CardPlay cardPlay, CardExtraEffect effect, int amount, int repeats)
	{
		if (effect == null || amount <= 0 || repeats <= 0)
		{
			return 0;
		}

		long total = amount;
		if (EffectUsesTargetCountForAppliedAmount(effect.Kind))
		{
			total *= Math.Max(0, EstimateAppliedTargetCount(combatState, ownerCreature, cardPlay, effect.Target));
		}

		total *= Math.Max(1, repeats);
		if (total >= int.MaxValue)
		{
			return int.MaxValue;
		}

		return total <= 0 ? 0 : (int)total;
	}

	private static int ResolveRunEffectSourceExecutionCount(CombatState combatState, Creature ownerCreature, CardPlay cardPlay, CardExtraEffect effect)
	{
		if (effect == null)
		{
			return 0;
		}

		int executionCount = 1;
		if (effect.ScaleMode != CardExtraEffectScaleMode.None)
		{
			int multiplier = GetHistoryCountMultiplier(combatState, ownerCreature, cardPlay, effect);
			if (!DoesCountConditionPass(multiplier, effect))
			{
				return 0;
			}

			if (effect.ScaleMode == CardExtraEffectScaleMode.PerHistoryCount)
			{
				if (multiplier <= 0 && !effect.HistoryScalingIncludesBase)
				{
					return 0;
				}

				long totalExecutions = effect.HistoryScalingIncludesBase
					? 1L + Math.Max(0, multiplier)
					: Math.Max(0, multiplier);
				executionCount = totalExecutions >= 99 ? 99 : (int)totalExecutions;
			}
		}

		int repeats = ResolveRepeatCount(cardPlay, effect);
		if (repeats <= 0)
		{
			return 0;
		}

		long combined = (long)executionCount * repeats;
		return combined >= 99 ? 99 : combined <= 0 ? 0 : (int)combined;
	}

	private static async Task ExecuteEffectCore(CombatState combatState, PlayerChoiceContext choiceContext, CardPlay cardPlay, CardExtraEffect effect, int triggerEventAmount)
	{
		CardModel card = cardPlay.Card;
		Player? owner = card.Owner;
		if (owner == null)
		{
			return;
		}
		Creature ownerCreature = CardEditorPowerExecutionHostContext.Current ?? owner.Creature;
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

			if (effect.Kind == CardExtraEffectKind.GrantReplay)
			{
				await GrantReplayToSelectedCards(combatState, choiceContext, cardPlay, effect);
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

		if (effect.Kind is (CardExtraEffectKind.CardCostsLess or CardExtraEffectKind.CardStarCostsLess)
			&& !effect.GrantToCard
			&& IsTriggeredCardCostsLessDefinition(effect))
		{
			ApplyTriggeredCardCostsLess(combatState, card, effect);
			return;
		}

		if (effect.Kind == CardExtraEffectKind.SelfScaling)
		{
			ApplySelfScalingMutation(card, effect);
			return;
		}

		if (effect.Kind == CardExtraEffectKind.PersistentSelfScaling)
		{
			ApplyPersistentSelfScalingMutation(card, effect);
			return;
		}

		if (effect.Kind == CardExtraEffectKind.ScalingStage)
		{
			if (!TryParseSpecificCardId(effect.SpecificCardId ?? string.Empty, out ModelId stageSourceId) || stageSourceId == ModelId.none)
			{
				return;
			}

			int stageCount = GetHistoryCountMultiplier(combatState, ownerCreature, cardPlay, effect);
			if (!DoesCountConditionPass(stageCount, effect))
			{
				return;
			}

			await CardEditorCreatedCardEffectSourceSupport.RunSingleEffectSourceOnPlay(
				card,
				choiceContext,
				cardPlay,
				stageSourceId,
				GetScalingStageRuntimeInstanceKey(card, effect),
				effect.CustomKeywordName);
			return;
		}

		if (effect.Kind == CardExtraEffectKind.EndTurn)
		{
			PlayerCmd.EndTurn(owner, canBackOut: false);
			return;
		}

		if (effect.Kind == CardExtraEffectKind.RunEffectSourceCard)
		{
			string? idStr = effect.SpecificCardId;
			if (string.IsNullOrWhiteSpace(idStr))
			{
				return;
			}

			ModelId sourceId = ModelId.Deserialize(idStr.Trim());
			if (sourceId == ModelId.none)
			{
				return;
			}

			int executionCount = ResolveRunEffectSourceExecutionCount(combatState, ownerCreature, cardPlay, effect);
			if (executionCount <= 0)
			{
				return;
			}

			string runtimeKeyBase = GetRunEffectSourceRuntimeInstanceKey(card, effect);
			for (int i = 0; i < executionCount; i++)
			{
				string runtimeKey = executionCount == 1
					? runtimeKeyBase
					: $"{runtimeKeyBase}|grant{i.ToString(CultureInfo.InvariantCulture)}";
				await CardEditorCreatedCardEffectSourceSupport.RunSingleEffectSourceOnPlay(
					card,
					choiceContext,
					cardPlay,
					sourceId,
					runtimeKey,
					effect.CustomKeywordName);
			}
			return;
		}

		if (effect.Kind == CardExtraEffectKind.ChooseOneEffectSource)
		{
			await ChooseOneEffectSourceCard(choiceContext, owner, card, cardPlay, effect);
			return;
		}

		int amount = ResolveConfiguredEffectAmount(combatState, ownerCreature, cardPlay, effect);

		amount = ApplyConditionalBonusAmount(combatState, ownerCreature, cardPlay, effect, amount);
		if (amount <= 0 && !AllowsZeroAmountWhenSelectingAll(effect))
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

		if (effect.PowerTriggerUsesEventAmount)
		{
			int eventAmount = Math.Max(0, triggerEventAmount);
			if (eventAmount <= 0)
			{
				if (!AllowsZeroAmountWhenSelectingAll(effect))
				{
					return;
				}

				amount = 0;
			}
			else if (eventAmount != 1)
			{
				long scaledByTriggerAmount = (long)amount * eventAmount;
				if (scaledByTriggerAmount >= int.MaxValue)
				{
					amount = int.MaxValue;
				}
				else if (scaledByTriggerAmount <= int.MinValue)
				{
					amount = int.MinValue;
				}
				else
				{
					amount = (int)scaledByTriggerAmount;
				}

				if (amount <= 0 && !AllowsZeroAmountWhenSelectingAll(effect))
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

		int fallbackAppliedAmount = EstimateFallbackAppliedAmount(combatState, ownerCreature, cardPlay, effect, amount, repeats);
		using IDisposable amountScope = CardEditorEffectExecutionAmountContext.PushEffectScoped(effect, fallbackAppliedAmount);

		// For damage, use a single AttackCommand with a hit-count so on-attack hooks (e.g. Vigor) behave like vanilla multi-hit cards.
		// Doing separate AttackCommands per repeat would consume "next attack" bonuses on the first hit only.
		if (effect.Kind == CardExtraEffectKind.DealDamage)
		{
			bool preserveSelfHp = CardHasRuntimeResourceConsumptionMode(cardPlay.Card, CardExtraEffectResourceConsumptionMode.SelfHpAndSelfDamage);
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

			switch (GetEffectiveResolvedTarget(cardPlay, effect.Target))
			{
				case CardExtraEffectTarget.AllEnemies:
					attack.TargetingAllOpponents(combatState);
					break;
				case CardExtraEffectTarget.RandomEnemy:
					attack.TargetingRandomOpponents(combatState);
					break;
				case CardExtraEffectTarget.Self:
					if (preserveSelfHp)
					{
						return;
					}
					attack.Targeting(ownerCreature);
					break;
				default:
				{
					Creature? resolvedTarget = ResolveSingleTarget(combatState, ownerCreature, cardPlay);
					if (resolvedTarget == null)
					{
						return;
					}
					if (preserveSelfHp && ReferenceEquals(resolvedTarget, ownerCreature))
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
					await MultiplyStatStatus(target, effect, amount, ownerCreature, cardPlay.Card, cardPlay);
				}
				break;
			}
			case CardExtraEffectKind.LoseHp:
			{
				List<Creature> targets = ResolveTargets(combatState, ownerCreature, cardPlay, effect.Target).Where(c => c != null).ToList();
				if (CardHasRuntimeResourceConsumptionMode(cardPlay.Card, CardExtraEffectResourceConsumptionMode.SelfHpAndSelfDamage))
				{
					targets = targets.Where(target => !ReferenceEquals(target, ownerCreature)).ToList();
				}
				if (targets.Count > 0)
				{
					await CreatureCmd.Damage(choiceContext, targets, amount, DamageProps.cardHpLoss, ownerCreature, cardPlay.Card);
				}
				break;
			}
			case CardExtraEffectKind.GainMaxHp:
			{
				await CreatureCmd.GainMaxHp(ownerCreature, amount);
				break;
			}
			case CardExtraEffectKind.LoseMaxHp:
			{
				await CreatureCmd.LoseMaxHp(choiceContext, ownerCreature, amount, isFromCard: true);
				break;
			}
			case CardExtraEffectKind.ApplyPower:
			{
				if (!TryResolveConfiguredPowerModel(effect.PowerId, out PowerModel? canonical) || canonical == null)
				{
					break;
				}

				foreach (Creature target in ResolveTargets(combatState, ownerCreature, cardPlay, effect.Target))
				{
					if (target == null)
					{
						continue;
					}
					PowerModel power = canonical.ToMutable();
					await PowerCmd.Apply(power, target, amount, ownerCreature, cardPlay.Card);
				}
				break;
			}
			case CardExtraEffectKind.GainStatusEqualToStatus:
			{
				await GainStatusEqualToStatus(combatState, ownerCreature, cardPlay, effect, amount);
				break;
			}
			case CardExtraEffectKind.DrawCards:
			{
				await DrawMatchingCards(choiceContext, owner, amount, effect);
				break;
			}
			case CardExtraEffectKind.DrawCardsThatCostLess:
			{
				List<CardModel> drawnCards = await DrawMatchingCards(choiceContext, owner, amount, effect);

				int costLess = effect.CardSelectionCount > 0 ? effect.CardSelectionCount : 1;
				if (costLess != 0)
				{
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
			case CardExtraEffectKind.LoseEnergy:
			{
				await PlayerCmd.LoseEnergy(amount, owner);
				break;
			}
			case CardExtraEffectKind.GainStars:
			{
				await PlayerCmd.GainStars(amount, owner);
				break;
			}
			case CardExtraEffectKind.LoseStars:
			{
				await PlayerCmd.LoseStars(amount, owner);
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
				if (ShouldPreserveSelfProtectedStat(cardPlay, ownerCreature, effect.Target, CardExtraEffectMultiplierStat.Strength)
					|| ShouldPreserveSelfProtectedConfiguredPower(cardPlay, ownerCreature, effect.Target, ModelDb.GetId<StrengthPower>()))
				{
					break;
				}
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
				if (ShouldPreserveSelfProtectedStat(cardPlay, ownerCreature, effect.Target, CardExtraEffectMultiplierStat.Dexterity)
					|| ShouldPreserveSelfProtectedConfiguredPower(cardPlay, ownerCreature, effect.Target, ModelDb.GetId<DexterityPower>()))
				{
					break;
				}
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
				if (ShouldPreserveSelfProtectedStat(cardPlay, ownerCreature, effect.Target, CardExtraEffectMultiplierStat.Focus)
					|| ShouldPreserveSelfProtectedConfiguredPower(cardPlay, ownerCreature, effect.Target, ModelDb.GetId<FocusPower>()))
				{
					break;
				}
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
			case CardExtraEffectKind.RemoveWeak:
			{
				await RemovePowerFromTargets<WeakPower, CardEditorTempWeakTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.ApplyFrail:
			{
				await ApplyPower<FrailPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempFrailTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.RemoveFrail:
			{
				await RemovePowerFromTargets<FrailPower, CardEditorTempFrailTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
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
			case CardExtraEffectKind.RemoveVulnerable:
			{
				await RemovePowerFromTargets<VulnerablePower, CardEditorTempVulnerableTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.ApplyPoison:
			{
				await ApplyPower<PoisonPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempPoisonTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.RemovePoison:
			{
				await RemovePowerFromTargets<PoisonPower, CardEditorTempPoisonTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.ApplyDoom:
			{
				await ApplyPower<DoomPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempDoomTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.RemoveDoom:
			{
				await RemovePowerFromTargets<DoomPower, CardEditorTempDoomTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.GainArtifact:
			{
				await ApplyPower<ArtifactPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempArtifactTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.RemoveArtifact:
			{
				await RemovePowerFromTargets<ArtifactPower, CardEditorTempArtifactTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.GainThorns:
			{
				await ApplyPower<ThornsPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempThornsTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.RemoveThorns:
			{
				await RemovePowerFromTargets<ThornsPower, CardEditorTempThornsTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.GainRegen:
			{
				await ApplyPower<RegenPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempRegenTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.RemoveRegen:
			{
				await RemovePowerFromTargets<RegenPower, CardEditorTempRegenTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.GainPlating:
			{
				await ApplyPower<PlatingPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempPlatingTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.RemovePlating:
			{
				await RemovePowerFromTargets<PlatingPower, CardEditorTempPlatingTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.GainIntangible:
			{
				await ApplyPower<IntangiblePower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempIntangibleTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.RemoveIntangible:
			{
				await RemovePowerFromTargets<IntangiblePower, CardEditorTempIntangibleTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.GainBuffer:
			{
				await ApplyPower<BufferPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempBufferTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.RemoveBuffer:
			{
				await RemovePowerFromTargets<BufferPower, CardEditorTempBufferTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.GainVigor:
			{
				await ApplyPower<VigorPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempVigorTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.RemoveVigor:
			{
				await RemovePowerFromTargets<VigorPower, CardEditorTempVigorTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.GainBlur:
			{
				await ApplyPower<BlurPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempBlurTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.RemoveBlur:
			{
				await RemovePowerFromTargets<BlurPower, CardEditorTempBlurTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.GainRitual:
			{
				await ApplyPower<RitualPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempRitualTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.RemoveRitual:
			{
				await RemovePowerFromTargets<RitualPower, CardEditorTempRitualTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.ApplyConstrict:
			{
				await ApplyPower<ConstrictPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				if (effect.Duration == CardExtraEffectDuration.ThisTurn)
					await ApplyPower<CardEditorTempConstrictTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.RemoveConstrict:
			{
				await RemovePowerFromTargets<ConstrictPower, CardEditorTempConstrictTrackerPower>(combatState, ownerCreature, cardPlay, effect.Target, amount);
				break;
			}
			case CardExtraEffectKind.CleanseDebuffs:
			{
				foreach (Creature target in ResolveTargets(combatState, ownerCreature, cardPlay, effect.Target))
				{
					await CleanseDebuffs(target, ownerCreature, cardPlay.Card);
				}
				break;
			}
			case CardExtraEffectKind.CleanseBuffs:
			{
				foreach (Creature target in ResolveTargets(combatState, ownerCreature, cardPlay, effect.Target))
				{
					await CleanseBuffs(target, ownerCreature, cardPlay.Card);
				}
				break;
			}
			case CardExtraEffectKind.AddRandomCardToHand:
			{
				await AddRandomCardsToHand(combatState, owner, amount, effect.GeneratedCardPool, effect.GeneratedCardType, effect);
				break;
			}
			case CardExtraEffectKind.ChooseOneOfThreeCardsToHand:
			{
				await ChooseOneOfThreeCardsToHand(combatState, choiceContext, owner, amount, effect.GeneratedCardPool, effect.GeneratedCardType, effect);
				break;
			}
			case CardExtraEffectKind.PlayRandomGeneratedCard:
			{
				await PlayRandomGeneratedCards(combatState, choiceContext, owner, amount, effect.GeneratedCardPool, effect.GeneratedCardType, effect);
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
					if (cardPlay?.Card != null
						&& ownerCreature != null
						&& ReferenceEquals(target, ownerCreature)
						&& CardHasRuntimeProtectedMultiplierStat(cardPlay.Card, CardExtraEffectMultiplierStat.Block))
					{
						continue;
					}

					await CreatureCmd.LoseBlock(target, Math.Min((decimal)amount, target.Block));
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
			case CardExtraEffectKind.TransformCards:
			{
				await TransformCards(choiceContext, owner, amount, effect, sourceCard: cardPlay?.Card);
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

				Creature? orbPassiveTarget = null;
				if (effect.OrbAction == CardExtraEffectOrbAction.TriggerPassive
					&& effect.Target == CardExtraEffectTarget.Target
					&& ownerCreature != null)
				{
					orbPassiveTarget = ResolveSingleTarget(combatState, ownerCreature, cardPlay);
				}

				if (effect.OrbScope == CardExtraEffectOrbScope.All
					&& (effect.OrbAction == CardExtraEffectOrbAction.Evoke
						|| effect.OrbAction == CardExtraEffectOrbAction.Remove
						|| effect.OrbAction == CardExtraEffectOrbAction.TriggerPassive))
				{
					await ExecuteOrbActionAll(choiceContext, owner, effect, orbPassiveTarget);
					break;
				}
				for (int i = 0; i < amount; i++)
				{
					if (effect.OrbAction == CardExtraEffectOrbAction.Channel)
					{
						await ChannelOrb(choiceContext, owner, effect);
						continue;
					}

					OrbModel? operatedOrb = await ExecuteOrbAction(choiceContext, owner, effect, orbPassiveTarget);
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
			case CardExtraEffectKind.AddExactCopyOfThisCardToDeck:
			{
				await AddExactCopiesOfThisCardToDeck(cardPlay, amount);
				break;
			}
			case CardExtraEffectKind.CopyCardsFromPileToDeck:
			{
				await CopyCardsFromPileToDeck(choiceContext, owner, amount, effect, sourceCard: cardPlay?.Card);
				break;
			}
			case CardExtraEffectKind.CopyExactCardsFromPileToDeck:
			{
				await CopyCardsFromPileToDeck(choiceContext, owner, amount, effect, sourceCard: cardPlay?.Card, exact: true);
				break;
			}
			case CardExtraEffectKind.RemoveCardsFromDeck:
			{
				await RemoveCardsFromDeck(choiceContext, owner, amount, effect, sourceCard: cardPlay?.Card);
				break;
			}
			case CardExtraEffectKind.AddSpecificCardToHand:
			{
				await AddSpecificCardsToHand(combatState, owner, amount, effect);
				break;
			}
			case CardExtraEffectKind.FetchSpecificCardToHand:
			{
				await FetchSpecificCardsToHand(combatState, owner, amount, effect, cardPlay?.Card);
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
			case CardExtraEffectKind.IgnoreEnemyDamageReductions:
			case CardExtraEffectKind.HitsAllEnemies:
			{
				break;
			}
			case CardExtraEffectKind.GrantKeywordToPile:
			{
				await GrantKeywordToCards(choiceContext, combatState, owner, amount, effect, sourceCard: cardPlay?.Card);
				break;
			}
			case CardExtraEffectKind.GainGold:
			{
				PlayerCmd.GainGold(amount, owner);
				break;
			}
			case CardExtraEffectKind.LoseGold:
			{
				await PlayerCmd.LoseGold(amount, owner);
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
		=> await ExecuteOrbAction(choiceContext, player, effect, passiveTarget: null);

	private static async Task<OrbModel?> ExecuteOrbAction(PlayerChoiceContext choiceContext, Player player, CardExtraEffect effect, Creature? passiveTarget)
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
		if (effect.OrbAction == CardExtraEffectOrbAction.TriggerPassive)
		{
			await TriggerOrbPassive(choiceContext, orb, passiveTarget);
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

	private static async Task TriggerOrbPassive(PlayerChoiceContext choiceContext, OrbModel orb, Creature? target)
	{
		if (CombatManager.Instance.IsOverOrEnding)
		{
			return;
		}

		if (choiceContext == null || orb == null)
		{
			return;
		}

		// Most orb passives cannot target a creature; Lightning is the exception.
		if (orb is not LightningOrb)
		{
			target = null;
		}

		await OrbCmd.Passive(choiceContext, orb, target);
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

	private static async Task ExecuteOrbActionAll(PlayerChoiceContext choiceContext, Player player, CardExtraEffect effect, Creature? passiveTarget)
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
			else if (effect.OrbAction == CardExtraEffectOrbAction.TriggerPassive)
			{
				await TriggerOrbPassive(choiceContext, orb, passiveTarget);
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
					if (!MatchesCardSelectionFilters(owner, card, effect))
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
				.Where(c => MatchesCardSelectionFilters(owner, c, effect))
				.Where(c => CanEffectEnchantmentApplyToCard(enchantmentId, c))
				.ToList();
		}
		if (candidates.Count == 0)
		{
			return;
		}

		int desiredCount = effect.CardSelectionCountIsX ? ResolveXAmountWithPlus(cardPlay, effect.CardSelectionCount) : effect.CardSelectionCount;
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

	private static async Task GrantReplayToSelectedCards(CombatState combatState, PlayerChoiceContext choiceContext, CardPlay cardPlay, CardExtraEffect effect)
	{
		if (combatState == null || choiceContext == null || cardPlay?.Card?.Owner == null || effect == null)
		{
			return;
		}

		CardModel sourceCard = cardPlay.Card;
		Player owner = sourceCard.Owner;
		bool useFutureAura = effect.FutureMatchingCards && effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All;

		List<CardModel> candidates = GetCandidatesFromConfiguredPile(
			owner,
			effect,
			sourceCard,
			includeDeck: false,
			requireDeckVersion: false,
			includeCostFilter: false);
		if (candidates.Count == 0 && !useFutureAura)
		{
			return;
		}

		int desiredCount = effect.CardSelectionCountIsX ? ResolveXAmountWithPlus(cardPlay, effect.CardSelectionCount) : effect.CardSelectionCount;
		desiredCount = Math.Clamp(desiredCount, 0, 99);

		PileType fromPileType = effect.CardSelectionPile == CardExtraEffectCardPile.AllPiles
			? PileType.None
			: ResolvePileType(effect.CardSelectionPile);
		List<CardModel> selected = candidates.Count == 0
			? new List<CardModel>()
			: await SelectCardsFromCandidates(
				choiceContext,
				owner,
				candidates,
				Math.Min(desiredCount, candidates.Count),
				effect.CardSelectionMode,
				fromPileType,
				new LocString("gameplay_ui", "CHOOSE_CARD_HEADER"),
				sourceCard,
				ShouldExcludeSourceCardFromSelection(effect),
				preferHandDiscardSelector: false,
				owner.RunState?.Rng?.Shuffle);

		if (selected.Count == 0 && !useFutureAura)
		{
			return;
		}

		int amount = effect.AmountIsX
			? ResolveXAmountWithPlus(cardPlay, effect.AmountXPlus)
			: effect.Amount;
		if (amount == 0)
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
				if (amount == 0)
				{
					return;
				}
			}
		}

		if (useFutureAura)
		{
			CardEditorMatchingCardAuraController.ApplyReplayAura(combatState, owner, effect, sourceCard, selected, amount);
			return;
		}

		foreach (CardModel card in selected)
		{
			if (card == null)
			{
				continue;
			}

			CardEditorTemporaryReplayController.Apply(combatState, card, amount, effect.CardGrantDuration, effect.CardGrantTurns);

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
		bool useFutureAura = effect.FutureMatchingCards && effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All;

		List<CardModel> candidates = GetCandidatesFromConfiguredPile(
			owner,
			effect,
			sourceCard,
			includeDeck: false,
			requireDeckVersion: false,
			includeCostFilter: false);
		if (candidates.Count == 0 && !useFutureAura)
		{
			return;
		}

		int desiredCount = effect.CardSelectionCountIsX ? ResolveXAmountWithPlus(cardPlay, effect.CardSelectionCount) : effect.CardSelectionCount;
		desiredCount = Math.Clamp(desiredCount, 0, 99);

		PileType fromPileType = effect.CardSelectionPile == CardExtraEffectCardPile.AllPiles
			? PileType.None
			: ResolvePileType(effect.CardSelectionPile);
		List<CardModel> selected = candidates.Count == 0
			? new List<CardModel>()
			: await SelectCardsFromCandidates(
				choiceContext,
				owner,
				candidates,
				Math.Min(desiredCount, candidates.Count),
				effect.CardSelectionMode,
				fromPileType,
				new LocString("gameplay_ui", "CHOOSE_CARD_HEADER"),
				sourceCard,
				ShouldExcludeSourceCardFromSelection(effect),
				preferHandDiscardSelector: false,
				owner.RunState?.Rng?.Shuffle);

		if (selected.Count == 0 && !useFutureAura)
		{
			return;
		}

		if (useFutureAura)
		{
			CardEditorMatchingCardAuraController.ApplyExtraEffectAura(combatState, owner, effect, sourceCard, selected);
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

	internal static bool SupportsIncludingSourceCardInSelection(CardExtraEffectKind kind)
	{
		return kind is CardExtraEffectKind.MoveCardsBetweenPiles
			or CardExtraEffectKind.UpgradeCardsInPile
			or CardExtraEffectKind.DiscardCards
			or CardExtraEffectKind.ExhaustCards
			or CardExtraEffectKind.TransformCards
			or CardExtraEffectKind.CopyCardsFromPileToDeck
			or CardExtraEffectKind.CopyExactCardsFromPileToDeck
			or CardExtraEffectKind.RemoveCardsFromDeck
			or CardExtraEffectKind.PlayCardFromPile
			or CardExtraEffectKind.FetchSpecificCardToHand
			or CardExtraEffectKind.GrantKeywordToPile
			or CardExtraEffectKind.GrantReplay
			or CardExtraEffectKind.EnchantCard
			or CardExtraEffectKind.RunEffectSourceCard;
	}

	private static bool ShouldIncludeSourceCardInSelection(CardExtraEffect? effect)
	{
		return effect != null
			&& effect.IncludeSourceCardInSelection
			&& SupportsIncludingSourceCardInSelection(effect.Kind);
	}

	private static bool ShouldExcludeSourceCardFromSelection(CardExtraEffect? effect)
	{
		return !ShouldIncludeSourceCardInSelection(effect);
	}

	private static CardModel? ResolveSourceCardSelectionCandidate(CardExtraEffect effect, CardModel? sourceCard, bool requireDeckVersion)
	{
		if (!ShouldIncludeSourceCardInSelection(effect) || sourceCard == null)
		{
			return null;
		}

		if (requireDeckVersion || effect.CardSelectionPile == CardExtraEffectCardPile.Deck)
		{
			return sourceCard.DeckVersion;
		}

		return sourceCard;
	}

	private static bool CanIncludeSourceCardCandidate(Player owner, CardExtraEffect effect, CardModel? sourceCard, bool requireDeckVersion, bool includeCostFilter)
	{
		CardModel? candidate = ResolveSourceCardSelectionCandidate(effect, sourceCard, requireDeckVersion);
		return candidate != null && MatchesCardSelectionFilters(owner, candidate, effect, includeCostFilter);
	}

	private static bool AllowsZeroAmountWhenSelectingAll(CardExtraEffect effect)
	{
		if (effect == null || effect.CardSelectionMode != CardExtraEffectCardSelectionMode.All)
		{
			return false;
		}

		return effect.Kind is CardExtraEffectKind.MoveCardsBetweenPiles
			or CardExtraEffectKind.UpgradeCardsInPile
			or CardExtraEffectKind.UpgradeDeckCards
			or CardExtraEffectKind.DiscardCards
			or CardExtraEffectKind.ExhaustCards
			or CardExtraEffectKind.TransformCards
			or CardExtraEffectKind.CopyCardsFromPileToDeck
			or CardExtraEffectKind.CopyExactCardsFromPileToDeck
			or CardExtraEffectKind.RemoveCardsFromDeck
			or CardExtraEffectKind.FetchSpecificCardToHand
			or CardExtraEffectKind.PlayCardFromPile;
	}

	private static async Task<List<CardModel>> SelectCardsFromCandidates(
		PlayerChoiceContext choiceContext,
		Player owner,
		IReadOnlyList<CardModel> candidates,
		int count,
		CardExtraEffectCardSelectionMode selectionMode,
		PileType fromPileType,
		LocString prompt,
		CardModel? sourceCard,
		bool excludeSourceCardInHandSelector,
		bool preferHandDiscardSelector,
		MegaCrit.Sts2.Core.Random.Rng? shuffleRng)
	{
		if (choiceContext == null || owner == null || candidates == null || candidates.Count == 0 || count <= 0)
		{
			return new List<CardModel>();
		}

		if (selectionMode == CardExtraEffectCardSelectionMode.All)
		{
			return candidates.Where(c => c != null).ToList();
		}

		if (selectionMode == CardExtraEffectCardSelectionMode.Random)
		{
			return PickRandomDistinct(candidates, count, shuffleRng);
		}

		if (selectionMode == CardExtraEffectCardSelectionMode.Top)
		{
			return candidates.Where(c => c != null).Take(count).ToList();
		}

		if (selectionMode == CardExtraEffectCardSelectionMode.Bottom)
		{
			return candidates.Where(c => c != null).Reverse().Take(count).Reverse().ToList();
		}

		bool isUpTo = selectionMode == CardExtraEffectCardSelectionMode.UpTo;
		CardSelectorPrefs prefs = isUpTo
			? new CardSelectorPrefs(prompt, 0, count) { Cancelable = true }
			: new CardSelectorPrefs(prompt, count);

		bool canUseHandSelector = fromPileType == PileType.Hand
			&& (sourceCard == null || excludeSourceCardInHandSelector);
		if (canUseHandSelector)
		{
			CardModel? source = excludeSourceCardInHandSelector ? sourceCard : null;
			HashSet<CardModel> candidateSet = new HashSet<CardModel>(candidates.Where(c => c != null), ReferenceEqualityComparer<CardModel>.Instance);
			Func<CardModel, bool>? filter = candidateSet.Count == 0 ? null : (CardModel c) => c != null && candidateSet.Contains(c);

			IEnumerable<CardModel> selected = preferHandDiscardSelector
				? await CardSelectCmd.FromHandForDiscard(choiceContext, owner, prefs, filter, source)
				: await CardSelectCmd.FromHand(choiceContext, owner, prefs, filter, source);

			return selected.Where(c => c != null).ToList();
		}

		return (await CardSelectCmd.FromSimpleGrid(choiceContext, candidates, owner, prefs)).OfType<CardModel>().ToList();
	}

	private static CardExtraEffectCardPile GetEffectiveDrawSourcePile(CardExtraEffect effect)
	{
		if (effect == null || effect.CardSelectionPile == CardExtraEffectCardPile.Hand)
		{
			return CardExtraEffectCardPile.DrawPile;
		}

		return effect.CardSelectionPile;
	}

	private static async Task<List<CardModel>> DrawMatchingCards(PlayerChoiceContext choiceContext, Player owner, int amount, CardExtraEffect effect)
	{
		if (choiceContext == null || owner == null || effect == null || amount <= 0)
		{
			return new List<CardModel>();
		}

		CardExtraEffectCardPile sourcePile = GetEffectiveDrawSourcePile(effect);
		bool useCustomDrawRouting = HasCardSelectionCriteria(effect) || sourcePile != CardExtraEffectCardPile.DrawPile;
		if (!useCustomDrawRouting)
		{
			return (await CardPileCmd.Draw(choiceContext, amount, owner)).Where(c => c != null).ToList();
		}

		List<CardModel> drawnCards = new List<CardModel>();
		CardPile? handPile = PileType.Hand.GetPile(owner);
		CardPile? drawPile = PileType.Draw.GetPile(owner);
		CardPile? discardPile = PileType.Discard.GetPile(owner);
		CardPile? exhaustPile = PileType.Exhaust.GetPile(owner);
		CardPile? deckPile = PileType.Deck.GetPile(owner);
		if (handPile == null || drawPile == null)
		{
			return drawnCards;
		}

		int drawsRequested = Math.Max(0, amount);
		for (int i = 0; i < drawsRequested; i++)
		{
			if (handPile.Cards.Count >= 10)
			{
				ThinkCmd.Play(new LocString("combat_messages", "HAND_FULL"), owner.Creature, 2.0);
				break;
			}

			if (sourcePile == CardExtraEffectCardPile.DrawPile)
			{
				await CardPileCmd.ShuffleIfNecessary(choiceContext, owner);
			}

			CardModel? matchingCard = sourcePile switch
			{
				CardExtraEffectCardPile.DrawPile => drawPile.Cards.FirstOrDefault(card => card != null && MatchesCardSelectionFilters(owner, card, effect)),
				CardExtraEffectCardPile.DiscardPile => discardPile?.Cards.FirstOrDefault(card => card != null && MatchesCardSelectionFilters(owner, card, effect)),
				CardExtraEffectCardPile.ExhaustPile => exhaustPile?.Cards.FirstOrDefault(card => card != null && MatchesCardSelectionFilters(owner, card, effect)),
				CardExtraEffectCardPile.Deck => deckPile?.Cards.FirstOrDefault(card => card != null && MatchesCardSelectionFilters(owner, card, effect)),
				CardExtraEffectCardPile.AllPiles => new[] { drawPile, discardPile, exhaustPile, deckPile }
					.Where(pile => pile != null)
					.SelectMany(pile => pile!.Cards)
					.FirstOrDefault(card => card != null && MatchesCardSelectionFilters(owner, card, effect)),
				_ => drawPile.Cards.FirstOrDefault(card => card != null && MatchesCardSelectionFilters(owner, card, effect))
			};
			if (matchingCard == null)
			{
				bool anyCardsRemaining = new[] { drawPile, discardPile, exhaustPile, deckPile }
					.Where(pile => pile != null)
					.SelectMany(pile => pile!.Cards)
					.Any();
				if (!anyCardsRemaining)
				{
					ThinkCmd.Play(new LocString("combat_messages", "NO_DRAW"), owner.Creature, 2.0);
				}
				break;
			}

			drawnCards.Add(matchingCard);
			await CardPileCmd.Add(matchingCard, PileType.Hand);
			CombatManager.Instance.History.CardDrawn(owner.Creature.CombatState, matchingCard, fromHandDraw: false);
			await Hook.AfterCardDrawn(owner.Creature.CombatState, choiceContext, matchingCard, fromHandDraw: false);
			matchingCard.InvokeDrawn();
		}

		return drawnCards;
	}

	private static async Task MoveCardsBetweenPiles(PlayerChoiceContext choiceContext, Player owner, int amount, CardExtraEffect effect, CardModel? sourceCard)
	{
		if (choiceContext == null || owner == null || effect == null)
		{
			return;
		}

		PileType fromPileType = effect.CardSelectionPile == CardExtraEffectCardPile.AllPiles
			? PileType.None
			: ResolvePileType(effect.CardSelectionPile);
		PileType toPileType = ResolvePileType(effect.MoveToPile);

		List<CardModel> candidates = GetCandidatesFromConfiguredPile(
			owner,
			effect,
			sourceCard,
			includeDeck: false,
			requireDeckVersion: false,
			includeCostFilter: true);

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

		LocString prompt = toPileType switch
		{
			PileType.Discard => CardSelectorPrefs.DiscardSelectionPrompt,
			PileType.Exhaust => CardSelectorPrefs.ExhaustSelectionPrompt,
			_ => new LocString("gameplay_ui", "CHOOSE_CARD_HEADER")
		};
		List<CardModel> selected = await SelectCardsFromCandidates(
			choiceContext,
			owner,
			candidates,
			count,
			effect.CardSelectionMode,
			fromPileType,
			prompt,
			sourceCard,
			ShouldExcludeSourceCardFromSelection(effect),
			preferHandDiscardSelector: toPileType == PileType.Discard,
			owner.RunState?.Rng?.Shuffle);

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

		PileType fromPileType = effect.CardSelectionPile == CardExtraEffectCardPile.AllPiles
			? PileType.None
			: ResolvePileType(effect.CardSelectionPile);

		List<CardModel> candidates = GetCandidatesFromConfiguredPile(
			owner,
			effect,
			sourceCard,
			includeDeck: false,
			requireDeckVersion: false,
			includeCostFilter: false)
			.Where(card => card != null && card.IsUpgradable)
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

		List<CardModel> selected = await SelectCardsFromCandidates(
			choiceContext,
			owner,
			candidates,
			count,
			effect.CardSelectionMode,
			fromPileType,
			CardSelectorPrefs.UpgradeSelectionPrompt,
			sourceCard,
			ShouldExcludeSourceCardFromSelection(effect),
			preferHandDiscardSelector: false,
			owner.RunState?.Rng?.Shuffle);

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

		PileType fromPileType = effect.CardSelectionPile == CardExtraEffectCardPile.AllPiles
			? PileType.None
			: ResolvePileType(effect.CardSelectionPile);

		List<CardModel> candidates = GetCandidatesFromConfiguredPile(
			owner,
			effect,
			sourceCard,
			includeDeck: false,
			requireDeckVersion: false,
			includeCostFilter: true);
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

		List<CardModel> selected = await SelectCardsFromCandidates(
			choiceContext,
			owner,
			candidates,
			count,
			effect.CardSelectionMode,
			fromPileType,
			CardSelectorPrefs.DiscardSelectionPrompt,
			sourceCard,
			ShouldExcludeSourceCardFromSelection(effect),
			preferHandDiscardSelector: true,
			owner.RunState?.Rng?.Shuffle);

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

		PileType fromPileType = effect.CardSelectionPile == CardExtraEffectCardPile.AllPiles
			? PileType.None
			: ResolvePileType(effect.CardSelectionPile);

		List<CardModel> candidates = GetCandidatesFromConfiguredPile(
			owner,
			effect,
			sourceCard,
			includeDeck: false,
			requireDeckVersion: false,
			includeCostFilter: true);
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

		List<CardModel> selected = await SelectCardsFromCandidates(
			choiceContext,
			owner,
			candidates,
			count,
			effect.CardSelectionMode,
			fromPileType,
			CardSelectorPrefs.ExhaustSelectionPrompt,
			sourceCard,
			ShouldExcludeSourceCardFromSelection(effect),
			preferHandDiscardSelector: false,
			owner.RunState?.Rng?.Shuffle);

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

	private static async Task TransformCards(PlayerChoiceContext choiceContext, Player owner, int amount, CardExtraEffect effect, CardModel? sourceCard)
	{
		if (choiceContext == null || owner == null || effect == null || amount <= 0)
		{
			return;
		}

		PileType fromPileType = effect.CardSelectionPile == CardExtraEffectCardPile.AllPiles
			? PileType.None
			: ResolvePileType(effect.CardSelectionPile);

		List<CardModel> candidates = GetCandidatesFromConfiguredPile(
			owner,
			effect,
			sourceCard,
			includeDeck: false,
			requireDeckVersion: false,
			includeCostFilter: true)
			.Where(card => card != null && card.IsTransformable)
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

		List<CardModel> selected = await SelectCardsFromCandidates(
			choiceContext,
			owner,
			candidates,
			count,
			effect.CardSelectionMode,
			fromPileType,
			CardSelectorPrefs.TransformSelectionPrompt,
			sourceCard,
			ShouldExcludeSourceCardFromSelection(effect),
			preferHandDiscardSelector: false,
			owner.RunState?.Rng?.Shuffle);

		if (selected.Count == 0)
		{
			return;
		}

		if (effect.TransformMode == CardExtraEffectTransformMode.SpecificCard)
		{
			if (!TryParseSpecificCardId(effect.SpecificCardId ?? string.Empty, out ModelId id))
			{
				return;
			}

			CardModel? canonical = ModelDb.GetByIdOrNull<CardModel>(id);
			if (canonical == null)
			{
				return;
			}

			List<CardTransformation> transformations = new List<CardTransformation>();
			foreach (CardModel card in selected)
			{
				if (card == null)
				{
					continue;
				}
				CardModel replacement = card.CombatState != null
					? card.CombatState.CreateCard(canonical, owner)
					: owner.RunState.CreateCard(canonical, owner);
				transformations.Add(new CardTransformation(card, replacement));
			}

			if (transformations.Count == 0)
			{
				return;
			}

			await CardCmd.Transform(transformations, rng: null, CardPreviewStyle.HorizontalLayout);
			return;
		}

		// Random transform
		List<CardTransformation> randomTransformations = selected
			.Where(c => c != null)
			.Select(c => new CardTransformation(c))
			.ToList();
		if (randomTransformations.Count == 0)
		{
			return;
		}
		await CardCmd.Transform(randomTransformations, owner.PlayerRng.Transformations, CardPreviewStyle.HorizontalLayout);
	}

	private static async Task GrantKeywordToCards(PlayerChoiceContext choiceContext, CombatState combatState, Player owner, int amount, CardExtraEffect effect, CardModel? sourceCard)
	{
		if (owner == null || combatState == null || effect == null)
		{
			return;
		}

		bool useFutureAura = effect.FutureMatchingCards && effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All;

		static bool IsPlayerEndBoundary(CombatState combatState, CardExtraEffect effect)
		{
			if (combatState.CurrentSide != CombatSide.Player)
			{
				return false;
			}

			if (effect.Trigger is CardExtraEffectTrigger.EndOfTurn or CardExtraEffectTrigger.EndOfTurnInHand)
			{
				return true;
			}

			return effect.Trigger == CardExtraEffectTrigger.TurnBoundary
				&& effect.TurnBoundary == CardExtraEffectTurnBoundary.End
				&& effect.TurnBoundarySide is CardExtraEffectTurnBoundarySide.YourTurn or CardExtraEffectTurnBoundarySide.Both;
		}

		PileType fromPileType = effect.CardSelectionPile == CardExtraEffectCardPile.AllPiles
			? PileType.None
			: ResolvePileType(effect.CardSelectionPile);

		List<CardModel> candidates = GetCandidatesFromConfiguredPile(
			owner,
			effect,
			sourceCard,
			includeDeck: false,
			requireDeckVersion: false,
			includeCostFilter: false);

		if (candidates.Count == 0 && !useFutureAura) return;

		int count = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All
			? candidates.Count
			: Math.Clamp(amount, 0, candidates.Count);

		List<CardModel> selected = candidates.Count == 0
			? new List<CardModel>()
			: await SelectCardsFromCandidates(
				choiceContext,
				owner,
				candidates,
				count,
				effect.CardSelectionMode,
				fromPileType,
				new LocString("gameplay_ui", "CHOOSE_CARD_HEADER"),
				sourceCard,
				ShouldExcludeSourceCardFromSelection(effect),
				preferHandDiscardSelector: false,
				owner.RunState?.Rng?.Shuffle);

		if (useFutureAura)
		{
			CardEditorMatchingCardAuraController.ApplyKeywordAura(combatState, owner, effect, sourceCard, selected);
			return;
		}

		foreach (CardModel card in selected)
		{
			if (card != null)
			{
				bool extendForPlayerEndBoundary = IsPlayerEndBoundary(combatState, effect);
				switch (effect.CardGrantDuration)
				{
					case CardExtraEffectCardGrantDuration.ThisTurn:
						CardEditorTemporaryKeywordController.ApplyForTurns(
							combatState,
							card,
							effect.GrantedKeyword,
							extendForPlayerEndBoundary ? 2 : 1);
						break;
					case CardExtraEffectCardGrantDuration.Turns:
						CardEditorTemporaryKeywordController.ApplyForTurns(
							combatState,
							card,
							effect.GrantedKeyword,
							Math.Clamp(effect.CardGrantTurns, 1, 99) + (extendForPlayerEndBoundary ? 1 : 0));
						break;
					case CardExtraEffectCardGrantDuration.UntilPlayed:
						CardEditorTemporaryKeywordController.ApplyUntilPlayed(combatState, card, effect.GrantedKeyword);
						break;
					default:
						CardEditorTemporaryKeywordController.ApplyThisCombat(combatState, card, effect.GrantedKeyword);
						break;
				}
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

		List<CardModel> candidates = deckPile.Cards
			.Where(c => c != null && c.IsUpgradable && MatchesCardSelectionFilters(owner, c, effect))
			.ToList();
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

	private static async Task AddExactCopiesOfThisCardToDeck(CardPlay cardPlay, int amount)
	{
		if (cardPlay?.Card?.Owner == null || amount <= 0)
		{
			return;
		}

		Player owner = cardPlay.Card.Owner;
		List<(CardPileAddResult Result, PileType PileType)> results = new();
		int copies = Math.Clamp(amount, 1, 99);
		for (int i = 0; i < copies; i++)
		{
			await AddDeckCardCopy(owner, cardPlay.Card, exact: true, results);
		}
		PreviewGeneratedPileAdds(results);
	}

	private static List<CardModel> GetCandidatesFromConfiguredPile(Player owner, CardExtraEffect effect, CardModel? sourceCard, bool includeDeck, bool requireDeckVersion, bool includeCostFilter)
	{
		List<CardModel> candidates = new List<CardModel>();
		if (owner == null || effect == null)
		{
			return candidates;
		}

		IEnumerable<PileType> pileTypes;
		if (effect.CardSelectionPile == CardExtraEffectCardPile.AllPiles)
		{
			pileTypes = includeDeck
				? new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust, PileType.Deck }
				: new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust };
		}
		else
		{
			pileTypes = new[] { ResolvePileType(effect.CardSelectionPile) };
		}

		HashSet<CardModel> seen = new HashSet<CardModel>(ReferenceEqualityComparer<CardModel>.Instance);
		foreach (PileType pileType in pileTypes)
		{
			CardPile? pile = pileType.GetPile(owner);
			if (pile == null)
			{
				continue;
			}

			foreach (CardModel card in pile.Cards)
			{
				if (card == null
					|| (ShouldExcludeSourceCardFromSelection(effect) && ReferenceEquals(card, sourceCard))
					|| (requireDeckVersion && card.DeckVersion == null)
					|| !MatchesCardSelectionFilters(owner, card, effect, includeCostFilter))
				{
					continue;
				}

				if (seen.Add(card))
				{
					candidates.Add(card);
				}
			}
		}

		CardModel? sourceCandidate = ResolveSourceCardSelectionCandidate(effect, sourceCard, requireDeckVersion);
		if (sourceCandidate != null
			&& MatchesCardSelectionFilters(owner, sourceCandidate, effect, includeCostFilter)
			&& seen.Add(sourceCandidate))
		{
			candidates.Add(sourceCandidate);
		}
		return candidates;
	}

	private static async Task CopyCardsFromPileToDeck(PlayerChoiceContext choiceContext, Player owner, int amount, CardExtraEffect effect, CardModel? sourceCard, bool exact = false)
	{
		if (choiceContext == null || owner == null || effect == null || amount <= 0)
		{
			return;
		}

		CombatState? combatState = owner.Creature?.CombatState;
		if (combatState == null)
		{
			return;
		}

		List<CardModel> candidates = GetCandidatesFromConfiguredPile(
			owner,
			effect,
			sourceCard,
			includeDeck: true,
			requireDeckVersion: false,
			includeCostFilter: true);
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

		PileType fromPileType = effect.CardSelectionPile == CardExtraEffectCardPile.AllPiles
			? PileType.None
			: ResolvePileType(effect.CardSelectionPile);
		List<CardModel> selected = await SelectCardsFromCandidates(
			choiceContext,
			owner,
			candidates,
			count,
			effect.CardSelectionMode,
			fromPileType,
			new LocString("gameplay_ui", "CHOOSE_CARD_HEADER"),
			sourceCard,
			ShouldExcludeSourceCardFromSelection(effect),
			preferHandDiscardSelector: false,
			owner.RunState?.Rng?.Shuffle);

		if (selected.Count == 0)
		{
			return;
		}

		List<(PileType PileType, CardPilePosition Position)> destinations = GetCopyCardDestinations(effect)
			.Select(dest => (ResolvePileType(dest.Pile), ResolvePilePosition(dest.Position)))
			.ToList();
		List<(CardPileAddResult Result, PileType PileType)> results = new();
		foreach (CardModel selectedCard in selected)
		{
			if (selectedCard != null)
			{
				await AddCardCopyToConfiguredDestinations(combatState, owner, selectedCard, exact, destinations, results);
			}
		}
		PreviewGeneratedPileAdds(results);
	}

	private static async Task RemoveCardsFromDeck(PlayerChoiceContext choiceContext, Player owner, int amount, CardExtraEffect effect, CardModel? sourceCard)
	{
		if (choiceContext == null || owner == null || effect == null || amount <= 0)
		{
			return;
		}

		bool selectingDeckDirectly = effect.CardSelectionPile == CardExtraEffectCardPile.Deck;
		List<CardModel> candidates = selectingDeckDirectly
			? GetCandidatesFromConfiguredPile(owner, effect, sourceCard, includeDeck: true, requireDeckVersion: false, includeCostFilter: true)
			: GetCandidatesFromConfiguredPile(owner, effect, sourceCard, includeDeck: false, requireDeckVersion: true, includeCostFilter: true);
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

		PileType fromPileType = effect.CardSelectionPile == CardExtraEffectCardPile.AllPiles
			? PileType.None
			: ResolvePileType(effect.CardSelectionPile);
		List<CardModel> selected = await SelectCardsFromCandidates(
			choiceContext,
			owner,
			candidates,
			count,
			effect.CardSelectionMode,
			fromPileType,
			CardSelectorPrefs.RemoveSelectionPrompt,
			sourceCard,
			ShouldExcludeSourceCardFromSelection(effect),
			preferHandDiscardSelector: false,
			owner.RunState?.Rng?.Shuffle);

		if (selected.Count == 0)
		{
			return;
		}

		CardPile? deckPile = PileType.Deck.GetPile(owner);
		if (deckPile == null)
		{
			return;
		}

		HashSet<CardModel> deckCards = new HashSet<CardModel>(ReferenceEqualityComparer<CardModel>.Instance);
		foreach (CardModel selectedCard in selected)
		{
			CardModel? deckCard = selectingDeckDirectly ? selectedCard : selectedCard?.DeckVersion;
			if (deckCard != null && deckCard.Pile?.Type == PileType.Deck && deckPile.Cards.Contains(deckCard))
			{
				deckCards.Add(deckCard);
			}
		}

		if (deckCards.Count > 0)
		{
			await CardPileCmd.RemoveFromDeck(deckCards.ToList(), showPreview: true);
		}
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
		List<(PileType PileType, CardPilePosition Position)> destinations = GetConfiguredCardCreationDestinations(effect, CardExtraEffectCardPile.DrawPile, CardExtraEffectCardPilePosition.Top)
			.Select(dest => (ResolvePileType(dest.Pile), ResolvePilePosition(dest.Position)))
			.ToList();

		int copies = Math.Clamp(amount, 1, 99);
		List<(CardPileAddResult Result, PileType PileType)> results = new();
		for (int i = 0; i < copies; i++)
		{
			foreach ((PileType toPileType, CardPilePosition position) in destinations)
			{
				if (toPileType == PileType.Deck)
				{
					await AddDeckCardCopy(cardPlay.Card.Owner, source, exact: true, results);
				}
				else if (toPileType.IsCombatPile())
				{
					CardModel clone = source.CreateClone();
					results.Add((await CardPileCmd.AddGeneratedCardToCombat(clone, toPileType, addedByPlayer: true, position), toPileType));
				}
			}
		}
		PreviewGeneratedPileAdds(results);
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

		List<(PileType PileType, CardPilePosition Position)> destinations = GetConfiguredCardCreationDestinations(effect, CardExtraEffectCardPile.DrawPile, CardExtraEffectCardPilePosition.Top)
			.Select(dest => (ResolvePileType(dest.Pile), ResolvePilePosition(dest.Position)))
			.ToList();
		List<(CardPileAddResult Result, PileType PileType)> results = new();
		for (int i = 0; i < amount; i++)
		{
			await AddCanonicalCardToConfiguredDestinations(combatState, owner, canonical, destinations, results);
		}
		PreviewGeneratedPileAdds(results);
	}

	private static async Task FetchSpecificCardsToHand(CombatState combatState, Player owner, int amount, CardExtraEffect effect, CardModel? sourceCard)
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
		bool fetchAll = effect.CardSelectionMode == CardExtraEffectCardSelectionMode.All;
		IEnumerable<CardModel> query = owner.PlayerCombatState.AllCards
			.Where(c => c != null
				&& c.Id == canonical.Id
				&& c.Pile?.Type != toPileType
				&& (!ShouldExcludeSourceCardFromSelection(effect) || !ReferenceEquals(c, sourceCard)));
		if (!fetchAll)
			query = query.Take(amount);
		List<CardModel> candidates = query.ToList();

		foreach (CardModel card in candidates)
		{
			await CardPileCmd.Add(card, toPileType, position);
		}
	}

private static async Task PlayCardsFromPile(PlayerChoiceContext choiceContext, Player owner, int amount, CardExtraEffect effect, CardModel? sourceCard)
{
	if (choiceContext == null || owner == null || effect == null || amount <= 0)
	{
		return;
	}

	List<CardModel> candidates = GetCandidatesFromConfiguredPile(
		owner,
		effect,
		sourceCard,
		includeDeck: true,
		requireDeckVersion: false,
		includeCostFilter: true);
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

		List<CardModel> selected = await SelectCardsFromCandidates(
			choiceContext,
			owner,
			candidates,
			count,
			effect.CardSelectionMode,
			effect.CardSelectionPile == CardExtraEffectCardPile.AllPiles ? PileType.None : ResolvePileType(effect.CardSelectionPile),
			new LocString("gameplay_ui", "CHOOSE_CARD_HEADER"),
			sourceCard,
			ShouldExcludeSourceCardFromSelection(effect),
			preferHandDiscardSelector: false,
			owner.RunState?.Rng?.Shuffle);

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
	// and non-X cards must satisfy the selected comparison against CostFilterMax.
	private static bool PassesCostFilter(CardModel card, CardExtraEffect effect)
	{
		return PassesCostFilter(card, effect, selectedTypeOverride: null);
	}

	private static bool PassesCostFilter(CardModel card, CardExtraEffect effect, CardGeneratedCardType? selectedTypeOverride)
	{
		if (effect == null || !effect.CostFilterEnabled)
		{
			return true;
		}
		if (card == null || card.EnergyCost.CostsX || card.HasStarCostX || card.CurrentStarCost > 0)
		{
			return false;
		}
		int currentCost = card.EnergyCost.GetWithModifiers(CostModifiers.All);
		bool passesThreshold = effect.CostFilterMode switch
		{
			CardExtraEffectCostFilterMode.AtLeast => currentCost >= effect.CostFilterMax,
			CardExtraEffectCostFilterMode.Exactly => currentCost == effect.CostFilterMax,
			_ => currentCost <= effect.CostFilterMax
		};
		if (!passesThreshold)
		{
			return false;
		}

		CardGeneratedCardType selectedType = selectedTypeOverride ?? (effect.GeneratedCardType != CardGeneratedCardType.Any
			? effect.GeneratedCardType
			: effect.CardSelectionType);
		if (selectedType == CardGeneratedCardType.Playable)
		{
			if (currentCost < 0)
			{
				return false;
			}

			CardType effectiveType = GetEffectiveCardType(card);
			return effectiveType is CardType.Attack or CardType.Skill or CardType.Power;
		}

		return true;
	}

	private static string? GetSelectionFilterDescriptorPrefix(CardExtraEffectCountCardFilter filter)
	{
		return filter switch
		{
			CardExtraEffectCountCardFilter.Summon => "[gold]" + CountCardFilterLabel(filter) + "[/gold]",
			CardExtraEffectCountCardFilter.Forge => "[gold]" + CountCardFilterLabel(filter) + "[/gold]",
			CardExtraEffectCountCardFilter.Any => null,
			CardExtraEffectCountCardFilter.Exhaust => null,
			CardExtraEffectCountCardFilter.Ethereal => null,
			CardExtraEffectCountCardFilter.Innate => null,
			CardExtraEffectCountCardFilter.Retain => null,
			CardExtraEffectCountCardFilter.Sly => null,
			CardExtraEffectCountCardFilter.Eternal => null,
			CardExtraEffectCountCardFilter.CreatesCards => null,
			_ => CountCardFilterPrefixLabel(filter)
		};
	}

	private static bool PassesCardMatchFilter(CardModel card, CardExtraEffect effect)
	{
		if (effect == null || effect.CardMatchMode == CardExtraEffectCardMatchMode.Any)
		{
			return true;
		}
		if (card == null)
		{
			return false;
		}

		if (effect.CardMatchMode == CardExtraEffectCardMatchMode.CardId)
		{
			string idStr = effect.MatchCardId?.Trim() ?? string.Empty;
			if (string.IsNullOrWhiteSpace(idStr))
			{
				return true;
			}
			if (!TryParseSpecificCardId(idStr, out ModelId desiredId))
			{
				return true;
			}
			return card.Id == desiredId;
		}

		if (effect.CardMatchMode == CardExtraEffectCardMatchMode.Tag)
		{
			if (effect.MatchTagKind == CardExtraEffectCardMatchTagKind.Custom)
			{
				string tag = effect.MatchCustomTag?.Trim() ?? string.Empty;
				if (string.IsNullOrWhiteSpace(tag))
				{
					return true;
				}
				if (CardEditorOverrides.TryGetEffectiveOverride(card.Id, out CardOverride overrideData)
					&& overrideData.CustomTags != null
					&& overrideData.CustomTags.Count > 0)
				{
					return overrideData.CustomTags.Contains(tag);
				}
				return false;
			}

			CardTag desiredTag = effect.MatchVanillaTag;
			if (desiredTag == CardTag.None)
			{
				return true;
			}
			return card.Tags != null && card.Tags.Contains(desiredTag);
		}

		if (effect.CardMatchMode == CardExtraEffectCardMatchMode.CustomKeyword)
		{
			string? desiredKeyword = NormalizeCustomKeywordName(effect.MatchCustomKeyword);
			if (desiredKeyword == null)
			{
				return true;
			}

			return CardHasCustomKeyword(card, desiredKeyword);
		}

		if (effect.CardMatchMode == CardExtraEffectCardMatchMode.NameContains)
		{
			string? desiredName = NormalizeNameFilterText(effect.MatchCardId);
			if (desiredName == null)
			{
				return true;
			}

			return GetCardNameForMatching(card).Contains(desiredName, StringComparison.OrdinalIgnoreCase);
		}

		return true;
	}

	private static string? NormalizeNameFilterText(string? text)
	{
		string trimmed = text?.Trim() ?? string.Empty;
		return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
	}

	private static string GetCardNameForMatching(CardModel? card)
	{
		return card?.Title?.Trim() ?? string.Empty;
	}

	private static bool PassesNameFilter(CardModel? card, CardExtraEffect? effect)
	{
		if (effect == null || !effect.NameFilterEnabled)
		{
			return true;
		}

		string? desiredName = NormalizeNameFilterText(effect.NameFilterText);
		if (desiredName == null)
		{
			return true;
		}

		return card != null && GetCardNameForMatching(card).Contains(desiredName, StringComparison.OrdinalIgnoreCase);
	}

	private static bool MatchesGeneratedCardType(CardModel? card, CardGeneratedCardType type)
	{
		if (card == null)
		{
			return false;
		}

		if (type == CardGeneratedCardType.Any)
		{
			return true;
		}

		if (type == CardGeneratedCardType.Playable)
		{
			CardType effectiveType = GetEffectiveCardType(card);
			return effectiveType is CardType.Attack or CardType.Skill or CardType.Power;
		}

		CardType desired = type switch
		{
			CardGeneratedCardType.Attack => CardType.Attack,
			CardGeneratedCardType.Skill => CardType.Skill,
			CardGeneratedCardType.Power => CardType.Power,
			CardGeneratedCardType.Status => CardType.Status,
			CardGeneratedCardType.Curse => CardType.Curse,
			CardGeneratedCardType.Quest => CardType.Quest,
			_ => GetEffectiveCardType(card)
		};

		return GetEffectiveCardType(card) == desired;
	}

	private static bool HasCreatesCardsOutputConstraints(CardGeneratedCardPool pool, CardGeneratedCardType type, CardExtraEffect? effect)
	{
		return pool != CardGeneratedCardPool.All
			|| type != CardGeneratedCardType.Any
			|| (effect != null && effect.CostFilterEnabled)
			|| (effect != null && effect.NameFilterEnabled && NormalizeNameFilterText(effect.NameFilterText) != null);
	}

	private static bool MatchesCreatedCardOutputFilters(Player? owner, CardModel? candidate, CardGeneratedCardPool pool, CardGeneratedCardType type, CardExtraEffect effect)
	{
		if (candidate == null || effect == null)
		{
			return false;
		}

		if (owner != null && !MatchesCountPool(owner, candidate, pool))
		{
			return false;
		}

		if (pool != CardGeneratedCardPool.All && owner == null)
		{
			return false;
		}

		if (!MatchesGeneratedCardType(candidate, type))
		{
			return false;
		}

		if (!PassesCostFilter(candidate, effect, type))
		{
			return false;
		}

		return PassesNameFilter(candidate, effect);
	}

	private static bool CardCreatesMatchingCards(Player? owner, CardModel? card, CardGeneratedCardPool pool, CardGeneratedCardType type, CardExtraEffect effect, HashSet<string>? visitedSourceCardIds = null)
	{
		if (card == null)
		{
			return false;
		}

		visitedSourceCardIds ??= new HashSet<string>(StringComparer.Ordinal);
		string visitKey = card.Id.ToString();
		if (!visitedSourceCardIds.Add(visitKey))
		{
			return false;
		}

		try
		{
			foreach (CardExtraEffect candidateEffect in GetEffectsForDescription(card, isUpgradePreview: false))
			{
				if (EffectCreatesMatchingCards(owner, card, candidateEffect, pool, type, effect, visitedSourceCardIds))
				{
					return true;
				}
			}
		}
		catch
		{
		}

		try
		{
			CombatState? combatState = card.CombatState;
			if (combatState != null)
			{
				foreach (CardExtraEffect candidateEffect in CardEditorTemporaryExtraEffectController.GetEffects(combatState, card))
				{
					if (EffectCreatesMatchingCards(owner, card, candidateEffect, pool, type, effect, visitedSourceCardIds))
					{
						return true;
					}
				}
			}
		}
		catch
		{
		}

		return !HasCreatesCardsOutputConstraints(pool, type, effect)
			&& CardTypeCreatesCardsViaIl(card.GetType());
	}

	private static bool EffectCreatesMatchingCards(Player? owner, CardModel hostCard, CardExtraEffect? effect, CardGeneratedCardPool pool, CardGeneratedCardType type, CardExtraEffect filterEffect, HashSet<string> visitedSourceCardIds)
	{
		if (effect == null || hostCard == null || filterEffect == null)
		{
			return false;
		}

		if (effect.BranchEffect != null && EffectCreatesMatchingCards(owner, hostCard, effect.BranchEffect, pool, type, filterEffect, visitedSourceCardIds))
		{
			return true;
		}

		switch (effect.Kind)
		{
			case CardExtraEffectKind.AddRandomCardToHand:
			case CardExtraEffectKind.ChooseOneOfThreeCardsToHand:
			case CardExtraEffectKind.PlayRandomGeneratedCard:
				if (owner == null)
				{
					return false;
				}

				return GetCardGenerationCandidates(owner, effect.GeneratedCardPool, effect.GeneratedCardType, effect.GeneratedCardCustomTag, effect)
					.Any(candidate => MatchesCreatedCardOutputFilters(owner, candidate, pool, type, filterEffect));
			case CardExtraEffectKind.AddSpecificCardToHand:
				if (!TryParseSpecificCardId(effect.SpecificCardId ?? string.Empty, out ModelId specificId))
				{
					return false;
				}

				return MatchesCreatedCardOutputFilters(owner, ModelDb.GetByIdOrNull<CardModel>(specificId), pool, type, filterEffect);
			case CardExtraEffectKind.AddCopyOfThisCard:
			case CardExtraEffectKind.AddExactCopyOfThisCardToDeck:
				return MatchesCreatedCardOutputFilters(owner, hostCard, pool, type, filterEffect);
			case CardExtraEffectKind.CopyCardsFromPileToDeck:
			case CardExtraEffectKind.CopyExactCardsFromPileToDeck:
				if (owner == null)
				{
					return false;
				}

				return GetCandidatesFromConfiguredPile(
						owner,
						effect,
						hostCard,
						includeDeck: true,
						requireDeckVersion: false,
						includeCostFilter: true)
					.Any(candidate => MatchesCreatedCardOutputFilters(owner, candidate, pool, type, filterEffect));
			case CardExtraEffectKind.RunEffectSourceCard:
				if (!string.IsNullOrWhiteSpace(effect.SpecificCardId)
					&& TryParseSpecificCardId(effect.SpecificCardId.Trim(), out ModelId sourceId))
				{
					try
					{
						CardModel? sourceCard = ModelDb.GetByIdOrNull<CardModel>(sourceId);
						return sourceCard != null && CardCreatesMatchingCards(owner, sourceCard, pool, type, filterEffect, visitedSourceCardIds);
					}
					catch
					{
					}
				}
				return false;
			default:
				return false;
		}
	}

	private static bool CardHasCustomKeyword(CardModel card, string keywordName)
	{
		if (card == null || string.IsNullOrWhiteSpace(keywordName))
		{
			return false;
		}

		string? normalizedKeyword = NormalizeCustomKeywordName(keywordName);
		if (normalizedKeyword == null)
		{
			return false;
		}

		try
		{
			foreach (CardExtraEffect effect in GetEffectsForDescription(card, isUpgradePreview: false))
			{
				if (string.Equals(NormalizeCustomKeywordName(effect?.CustomKeywordName), normalizedKeyword, StringComparison.OrdinalIgnoreCase))
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

	private static bool TryApplySelfScalingEffectRowMutation(CardExtraEffect candidate, CardExtraEffectSelfScalingField field, int delta)
	{
		if (candidate == null || !SupportsSelfScalingEffectRowField(candidate, field))
		{
			return false;
		}

		switch (field)
		{
			case CardExtraEffectSelfScalingField.Amount:
			{
				int nextAmount = candidate.Amount + delta;
				if (!IsValidEffectAmount(candidate.Kind, nextAmount))
				{
					return false;
				}
				candidate.Amount = nextAmount;
				return true;
			}
			case CardExtraEffectSelfScalingField.Repeat:
				candidate.RepeatCount = Math.Clamp(candidate.RepeatCount + delta, 0, 99);
				return true;
			case CardExtraEffectSelfScalingField.SecondaryAmount:
				candidate.ConditionalBonusAmount = Math.Clamp(candidate.ConditionalBonusAmount + delta, -99, 999);
				return true;
			case CardExtraEffectSelfScalingField.Threshold:
				candidate.CountConditionAmount = Math.Clamp(candidate.CountConditionAmount + delta, 0, 999);
				return true;
			case CardExtraEffectSelfScalingField.Duration:
				return TryApplySelfScalingDurationMutation(candidate, delta);
			default:
				return false;
		}
	}

	private static bool TryApplySelfScalingDurationMutation(CardExtraEffect candidate, int delta)
	{
		if (IsPowerEffect(candidate) && candidate.TriggerMaxTurns > 0)
		{
			candidate.TriggerMaxTurns = Math.Clamp(candidate.TriggerMaxTurns + delta, 1, 99);
			return true;
		}

		switch (candidate.CardGrantDuration)
		{
			case CardExtraEffectCardGrantDuration.Turns:
				candidate.CardGrantTurns = Math.Clamp(candidate.CardGrantTurns + delta, 1, 99);
				return true;
		}

		switch (candidate.EnchantmentDuration)
		{
			case CardExtraEffectEnchantmentDuration.Turns:
				candidate.EnchantmentTurns = Math.Clamp(candidate.EnchantmentTurns + delta, 1, 99);
				return true;
		}

		switch (candidate.CardCostsLessDuration)
		{
			case CardExtraEffectCardCostsLessDuration.Turns:
				candidate.CardCostsLessTurns = Math.Clamp(candidate.CardCostsLessTurns + delta, 1, 99);
				return true;
		}

		switch (candidate.CreatedCardsCostDuration)
		{
			case CardCreatedCardsCostDuration.Turns:
				candidate.CreatedCardsCostTurns = Math.Clamp(candidate.CreatedCardsCostTurns + delta, 1, 99);
				return true;
		}

		if (candidate.Timing != CardExtraEffectTiming.Immediate)
		{
			candidate.Turns = Math.Clamp(candidate.Turns + delta, 0, 99);
			return true;
		}

		return false;
	}

	private static bool HasCardSelectionCriteria(CardExtraEffect effect)
	{
		return effect != null
			&& (effect.CardSelectionPool != CardGeneratedCardPool.All
				|| effect.CardSelectionType != CardGeneratedCardType.Any
				|| effect.CardSelectionFilter != CardExtraEffectCountCardFilter.Any
				|| effect.CardMatchMode != CardExtraEffectCardMatchMode.Any
				|| effect.CostFilterEnabled
				|| (effect.NameFilterEnabled && NormalizeNameFilterText(effect.NameFilterText) != null));
	}

internal static bool MatchesCardSelectionFilters(Player owner, CardModel card, CardExtraEffect effect, bool includeCostFilter = false)
{
	if (card == null || owner == null || effect == null)
	{
		return false;
	}

	CardExtraEffectCountCardFilter filter = effect.CardSelectionFilter;
	if (filter == CardExtraEffectCountCardFilter.CreatesCards)
	{
		if (!CardCreatesMatchingCards(owner, card, effect.CardSelectionPool, effect.CardSelectionType, effect))
		{
			return false;
		}
	}
	else
	{
		if (filter != CardExtraEffectCountCardFilter.Any && !MatchesCountCardEffectFilter(card, filter))
		{
			return false;
		}

		if (includeCostFilter && !PassesCostFilter(card, effect, effect.CardSelectionType))
		{
			return false;
		}

		if (!MatchesCountPool(owner, card, effect.CardSelectionPool))
		{
			return false;
		}

		if (!MatchesGeneratedCardType(card, effect.CardSelectionType))
		{
			return false;
		}

		if (!PassesNameFilter(card, effect))
		{
			return false;
		}
	}

	return PassesCardMatchFilter(card, effect);
}

	private static string BuildCardSelectionDescriptor(CardExtraEffect effect, bool plural, bool preferAllCardsKeyword = false)
	{
		string cardWord = CardEditorLoc.T(plural ? "cardText.cards" : "cardText.word.card", plural ? "cards" : "card");
		if (effect == null)
		{
			return cardWord;
		}

		bool useAllCardsKeyword = preferAllCardsKeyword
			&& plural
			&& effect.CardSelectionType == CardGeneratedCardType.Any;

		bool selectionCreatesCards = effect.CardSelectionFilter == CardExtraEffectCountCardFilter.CreatesCards;
		string? poolPrefix = selectionCreatesCards ? null : GetCardPoolQualifierPrefix(effect.CardSelectionPool, plural);
		string? poolSuffix = selectionCreatesCards ? null : GetCardPoolQualifierSuffix(effect.CardSelectionPool, plural);

		string? typePrefix = useAllCardsKeyword
			? null
			: effect.CardSelectionType switch
		{
			CardGeneratedCardType.Attack => GeneratedCardTypeLabel(CardGeneratedCardType.Attack),
			CardGeneratedCardType.Skill => GeneratedCardTypeLabel(CardGeneratedCardType.Skill),
			CardGeneratedCardType.Power => GeneratedCardTypeLabel(CardGeneratedCardType.Power),
			CardGeneratedCardType.Playable => "[gold]" + GetPlayableKeywordTitle() + "[/gold]",
			CardGeneratedCardType.Status => GeneratedCardTypeLabel(CardGeneratedCardType.Status),
			CardGeneratedCardType.Curse => GeneratedCardTypeLabel(CardGeneratedCardType.Curse),
			CardGeneratedCardType.Quest => GeneratedCardTypeLabel(CardGeneratedCardType.Quest),
			_ => null
		};

		if (useAllCardsKeyword)
		{
			cardWord = "[gold]" + GetAllCardsKeywordTitle() + "[/gold]";
		}

		string descriptor = string.Join(" ", new[] { poolPrefix, typePrefix, cardWord }.Where(s => !string.IsNullOrWhiteSpace(s)));
		if (!string.IsNullOrWhiteSpace(poolSuffix))
		{
			descriptor = string.Join(" ", new[] { descriptor, poolSuffix }.Where(s => !string.IsNullOrWhiteSpace(s)));
		}

		string? keywordFilter = effect.CardSelectionFilter switch
		{
			CardExtraEffectCountCardFilter.Exhaust => "Exhaust",
			CardExtraEffectCountCardFilter.Ethereal => "Ethereal",
			CardExtraEffectCountCardFilter.Innate => "Innate",
			CardExtraEffectCountCardFilter.Retain => "Retain",
			CardExtraEffectCountCardFilter.Sly => "Sly",
			CardExtraEffectCountCardFilter.Eternal => "Eternal",
			_ => null
		};

		string? filterPrefix = GetSelectionFilterDescriptorPrefix(effect.CardSelectionFilter);
		if (!string.IsNullOrWhiteSpace(filterPrefix))
		{
			descriptor = string.Join(" ", new[] { filterPrefix, descriptor }.Where(s => !string.IsNullOrWhiteSpace(s)));
		}
		else if (!string.IsNullOrWhiteSpace(keywordFilter))
		{
			descriptor += " " + CardEditorLoc.F("cardText.selectionDescriptor.keyword", $"with {keywordFilter}", ("Keyword", keywordFilter));
		}
		string matchSuffix = BuildCountCardMatchSuffix(effect).Trim();
		if (!string.IsNullOrWhiteSpace(matchSuffix))
		{
			descriptor += " " + matchSuffix;
		}

		if (effect.CardSelectionFilter == CardExtraEffectCountCardFilter.CreatesCards)
		{
			string outputDescriptor = BuildCreatesCardsOutputDescriptor(effect.CardSelectionPool, effect.CardSelectionType, effect, plural: true);
			string clause = string.IsNullOrWhiteSpace(outputDescriptor)
				? CardEditorLoc.T(
					plural ? "cardText.countFilter.createsCards.plural" : "cardText.countFilter.createsCards.singular",
					plural ? "that create cards" : "that creates cards")
				: CardEditorLoc.F(
					plural ? "cardText.countFilter.createsCards.output.plural" : "cardText.countFilter.createsCards.output.singular",
					plural ? $"that create {outputDescriptor}" : $"that creates {outputDescriptor}",
					("Output", outputDescriptor));
			descriptor = string.IsNullOrWhiteSpace(descriptor)
				? $"{cardWord} {clause}"
				: $"{descriptor} {clause}";
			return descriptor.Trim();
		}

		return BuildCostFilteredText(card: null, descriptor.Trim(), effect);
	}

	private static string? GetCardPoolQualifierPrefix(CardGeneratedCardPool pool, bool plural)
	{
		return pool switch
		{
			CardGeneratedCardPool.Colorless => GeneratedCardPoolLabel(CardGeneratedCardPool.Colorless),
			CardGeneratedCardPool.Ancient => GeneratedCardPoolLabel(CardGeneratedCardPool.Ancient),
			CardGeneratedCardPool.Ironclad => GeneratedCardPoolLabel(CardGeneratedCardPool.Ironclad),
			CardGeneratedCardPool.Silent => GeneratedCardPoolLabel(CardGeneratedCardPool.Silent),
			CardGeneratedCardPool.Defect => GeneratedCardPoolLabel(CardGeneratedCardPool.Defect),
			CardGeneratedCardPool.Regent => GeneratedCardPoolLabel(CardGeneratedCardPool.Regent),
			CardGeneratedCardPool.Necrobinder => GeneratedCardPoolLabel(CardGeneratedCardPool.Necrobinder),
			CardGeneratedCardPool.OtherColors when plural => CardEditorLoc.T("cardText.poolPrefix.otherColors", "Other Character"),
			CardGeneratedCardPool.Any when plural => CardEditorLoc.T("cardText.poolPrefix.allColors", "Any Character"),
			_ => null
		};
	}

	private static string? GetCardPoolQualifierSuffix(CardGeneratedCardPool pool, bool plural)
	{
		return pool switch
		{
			CardGeneratedCardPool.Default => CardEditorLoc.T("cardText.poolSuffix.yourColor", "of your color"),
			CardGeneratedCardPool.OtherColors when !plural => CardEditorLoc.T("cardText.poolSuffix.otherColors", "from other characters"),
			CardGeneratedCardPool.Any when !plural => CardEditorLoc.T("cardText.poolSuffix.allColors", "from any character"),
			CardGeneratedCardPool.All => null,
			_ => null
		};
	}

	private static string GetPlayableKeywordTitle()
	{
		return CardEditorLoc.T("cardText.playableKeyword.title", "Playable");
	}

	private static string GetPlayableKeywordDescription()
	{
		return CardEditorLoc.T(
			"cardText.playableKeyword.description",
			"Attack, Skill, or Power cards. This excludes Curses and Statuses.");
	}

	private static string GetAllCardsKeywordTitle()
	{
		return CardEditorLoc.T("cardText.allCardsKeyword.title", "All Cards");
	}

	private static string GetAllCardsKeywordDescription()
	{
		return CardEditorLoc.T(
			"cardText.allCardsKeyword.description",
			"Includes playable cards, Statuses, Curses, and other card types unless further filtered.");
	}

	private static bool TryInlineCardQualifier(ref string text, string noun, string qualifiedNoun)
	{
		if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(noun) || string.IsNullOrWhiteSpace(qualifiedNoun))
		{
			return false;
		}

		string[] patterns =
		{
			"matching " + noun,
			"random " + noun,
			"all " + noun,
			noun + " from ",
			noun + " in ",
			noun + " into ",
			noun + " to ",
			noun + " of ",
			noun + " on ",
			noun + " this",
			noun + ".",
			noun + ",",
			noun + ")",
			noun
		};

		foreach (string pattern in patterns)
		{
			int index = text.IndexOf(pattern, StringComparison.Ordinal);
			if (index < 0)
			{
				continue;
			}

			string replacement = pattern switch
			{
				_ when pattern == "matching " + noun => qualifiedNoun,
				_ when pattern == "random " + noun => "random " + qualifiedNoun,
				_ when pattern == "all " + noun && qualifiedNoun.Contains("[gold]" + GetAllCardsKeywordTitle() + "[/gold]", StringComparison.Ordinal) => qualifiedNoun,
				_ when pattern == "all " + noun => "all " + qualifiedNoun,
				_ when pattern == noun => qualifiedNoun,
				_ => qualifiedNoun + pattern[noun.Length..]
			};

			text = text[..index] + replacement + text[(index + pattern.Length)..];
			return true;
		}

		return false;
	}

	private static string AppendCardSelectionNote(string text, CardExtraEffect effect)
	{
		bool hasCardWord = !string.IsNullOrWhiteSpace(text)
			&& text.Contains("card", StringComparison.OrdinalIgnoreCase);
		bool useAllCardsKeyword = effect != null
			&& effect.CardSelectionType == CardGeneratedCardType.Any
			&& effect.CardSelectionFilter == CardExtraEffectCountCardFilter.Any
			&& effect.CardMatchMode == CardExtraEffectCardMatchMode.Any
			&& hasCardWord;
		bool includeSourceCard = ShouldIncludeSourceCardInSelection(effect);
		bool hasSelectionDescriptor = HasCardSelectionCriteria(effect) || useAllCardsKeyword;
		if (string.IsNullOrWhiteSpace(text) || (!hasSelectionDescriptor && !includeSourceCard))
		{
			return text;
		}

		if (hasSelectionDescriptor)
		{
			string pluralDescriptor = BuildCardSelectionDescriptor(effect, plural: true, preferAllCardsKeyword: useAllCardsKeyword);
			string singularDescriptor = BuildCardSelectionDescriptor(effect, plural: false);
			if (!TryInlineCardQualifier(ref text, "cards", pluralDescriptor)
				&& !TryInlineCardQualifier(ref text, "card", singularDescriptor))
			{
				string note = CardEditorLoc.F("cardText.selectionDescriptor.note", $" ({pluralDescriptor})", ("Descriptor", pluralDescriptor));
				if (text.EndsWith('.') && text.Length > 1)
				{
					text = text[..^1] + note + ".";
				}
				else
				{
					text += note;
				}
			}
		}

		if (!includeSourceCard)
		{
			return text;
		}

		string sourceNote = CardEditorLoc.T("cardText.selectionDescriptor.includeSource", "including this card itself");
		string suffix = CardEditorLoc.F("cardText.selectionDescriptor.includeSource.note", $" ({sourceNote})", ("Note", sourceNote));
		if (text.EndsWith('.') && text.Length > 1)
		{
			return text[..^1] + suffix + ".";
		}

		return text + suffix;
	}

	private static string BuildCostFilteredText(CardModel? card, string text, CardExtraEffect effect)
	{
		if (effect == null)
		{
			return text;
		}

		if (effect.CostFilterEnabled)
		{
			string maxCostText = BuildEnergyIcons(card, effect.CostFilterMax);
			string qualifier = effect.CostFilterMode switch
			{
				CardExtraEffectCostFilterMode.AtLeast => CardEditorLoc.F("cardText.costFilter.inline.min", $"costing {maxCostText} or more", ("Threshold", maxCostText)),
				CardExtraEffectCostFilterMode.Exactly => CardEditorLoc.F("cardText.costFilter.inline.exact", $"costing exactly {maxCostText}", ("Threshold", maxCostText)),
				_ when effect.CostFilterMax == 0 => CardEditorLoc.F("cardText.costFilter.inline.zero", $"costing {maxCostText} only", ("Max", maxCostText)),
				_ => CardEditorLoc.F("cardText.costFilter.inline.max", $"costing {maxCostText} or less", ("Max", maxCostText))
			};
			if (!(TryInlineCardQualifier(ref text, "cards", CardEditorLoc.F("cardText.costFilter.inline.cards", $"cards {qualifier}", ("Qualifier", qualifier)))
				|| TryInlineCardQualifier(ref text, "card", CardEditorLoc.F("cardText.costFilter.inline.card", $"card {qualifier}", ("Qualifier", qualifier)))))
			{
				string note = effect.CostFilterMode switch
				{
					CardExtraEffectCostFilterMode.AtLeast => CardEditorLoc.F("cardText.costFilter.minOnly", $" (cost {maxCostText} or more)", ("Threshold", maxCostText)),
					CardExtraEffectCostFilterMode.Exactly => CardEditorLoc.F("cardText.costFilter.exactOnly", $" (cost exactly {maxCostText})", ("Threshold", maxCostText)),
					_ when effect.CostFilterMax == 0 => CardEditorLoc.F("cardText.costFilter.maxOnlyZeroSymbol", $" ({maxCostText} only)", ("Max", maxCostText)),
					_ => CardEditorLoc.F("cardText.costFilter.maxOnly", $" (cost {maxCostText} or less)", ("Max", maxCostText))
				};
				if (text.EndsWith('.') && text.Length > 1)
				{
					text = text[..^1] + note + ".";
				}
				else
				{
					text += note;
				}
			}
		}

		string? nameFilter = NormalizeNameFilterText(effect.NameFilterText);
		if (effect.NameFilterEnabled && nameFilter != null)
		{
			string note = CardEditorLoc.F("cardText.nameFilter.note", $" (name contains \"{nameFilter}\")", ("Text", nameFilter));
			if (text.EndsWith('.') && text.Length > 1)
			{
				text = text[..^1] + note + ".";
			}
			else
			{
				text += note;
			}
		}

		return text;
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

	internal static bool IsMovedToPileTrigger(CardExtraEffectTrigger trigger)
	{
		return trigger is CardExtraEffectTrigger.OnMovedToTopOfPile or CardExtraEffectTrigger.OnMovedToBottomOfPile;
	}

	internal static bool IsOrderedPileSelectionMode(CardExtraEffectCardSelectionMode mode)
	{
		return mode is CardExtraEffectCardSelectionMode.Top or CardExtraEffectCardSelectionMode.Bottom;
	}

	internal static bool IsCardAtTopOfPile(CardModel? card)
	{
		CardPile? pile = card?.Pile;
		return pile?.Cards != null
			&& pile.Cards.Count > 0
			&& ReferenceEquals(pile.Cards[0], card);
	}

	internal static bool IsCardAtBottomOfPile(CardModel? card)
	{
		CardPile? pile = card?.Pile;
		return pile?.Cards != null
			&& pile.Cards.Count > 0
			&& ReferenceEquals(pile.Cards[^1], card);
	}

	private static bool IsCardAtConfiguredPilePosition(CardModel? card, CardExtraEffectCardSelectionMode selectionMode)
	{
		return selectionMode switch
		{
			CardExtraEffectCardSelectionMode.Top => IsCardAtTopOfPile(card),
			CardExtraEffectCardSelectionMode.Bottom => IsCardAtBottomOfPile(card),
			_ => true
		};
	}

	internal static bool SupportsHistoryScaling(CardExtraEffectKind kind)
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

	private static int ApplyConditionalBonusAmount(CombatState combatState, Creature ownerCreature, CardPlay cardPlay, CardExtraEffect effect, int amount)
	{
		if (combatState == null || ownerCreature == null || cardPlay == null || effect == null)
		{
			return amount;
		}

		int bonus = effect.ConditionalBonusAmount;
		if (bonus == 0)
		{
			return amount;
		}

		bool pass = DoesConditionalBonusPass(combatState, ownerCreature, cardPlay, effect);

		if (!pass)
		{
			return amount;
		}

		long total = (long)amount + bonus;
		if (total >= int.MaxValue)
		{
			return int.MaxValue;
		}
		if (total <= int.MinValue)
		{
			return int.MinValue;
		}
		return (int)total;
	}

	internal static int GetHistoryCountMultiplierForCardPlay(CombatState combatState, Creature ownerCreature, CardPlay cardPlay, CardExtraEffect effect)
	{
		return GetHistoryCountMultiplier(combatState, ownerCreature, cardPlay, effect);
	}

	internal static int GetCardDamageBonusAmount(CombatState combatState, CardModel card, Creature? target, Creature? dealer)
	{
		if (combatState == null || card == null)
		{
			return 0;
		}

		Creature? ownerCreature = card.Owner?.Creature ?? dealer;
		if (ownerCreature == null)
		{
			return 0;
		}

		CardPlay playForHistory = BuildCardDamageBonusPlayContext(card, target);
		long totalBonus = 0L;
		foreach (CardExtraEffect effect in GetRuntimeEffectsForModifierInspection(combatState, card))
		{
			if (effect == null
				|| effect.Kind != CardExtraEffectKind.CardDealsExtraDamage
				|| IsPowerEffect(effect)
				|| effect.GrantToCard
				|| !IsValidEffectAmount(effect.Kind, effect.Amount))
			{
				continue;
			}

			totalBonus += ResolveCardDamageBonusAmount(combatState, ownerCreature, playForHistory, effect);
			if (totalBonus >= int.MaxValue)
			{
				return int.MaxValue;
			}
		}

		return totalBonus <= 0L ? 0 : (int)Math.Min(totalBonus, int.MaxValue);
	}

	private static CardPlay BuildCardDamageBonusPlayContext(CardModel card, Creature? target)
	{
		CardPlay? currentPlay = CardEditorCardPlayContext.Current;
		if (currentPlay != null && ReferenceEquals(currentPlay.Card, card))
		{
			return currentPlay;
		}

		return new CardPlay
		{
			Card = card,
			Target = target,
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
	}

	private static int ResolveCardDamageBonusAmount(CombatState combatState, Creature ownerCreature, CardPlay cardPlay, CardExtraEffect effect)
	{
		if (effect == null || effect.Kind != CardExtraEffectKind.CardDealsExtraDamage)
		{
			return 0;
		}

		CardExtraEffect? branchEffect = GetUsableBranchEffect(effect);
		bool shouldRunBranch = branchEffect != null && DoesBranchConditionPass(combatState, ownerCreature, cardPlay, effect);

		if (shouldRunBranch && effect.BranchMode == CardExtraEffectBranchMode.InsteadIf)
		{
			return branchEffect?.Kind == CardExtraEffectKind.CardDealsExtraDamage
				? ResolveCardDamageBonusAmountCore(combatState, ownerCreature, cardPlay, branchEffect)
				: 0;
		}

		int total = ResolveCardDamageBonusAmountCore(combatState, ownerCreature, cardPlay, effect);
		if (shouldRunBranch
			&& effect.BranchMode == CardExtraEffectBranchMode.AlsoIf
			&& branchEffect?.Kind == CardExtraEffectKind.CardDealsExtraDamage)
		{
			long combined = (long)total + ResolveCardDamageBonusAmountCore(combatState, ownerCreature, cardPlay, branchEffect);
			total = combined >= int.MaxValue ? int.MaxValue : combined <= 0L ? 0 : (int)combined;
		}

		return total;
	}

	private static int ResolveCardDamageBonusAmountCore(CombatState combatState, Creature ownerCreature, CardPlay cardPlay, CardExtraEffect effect)
	{
		int amount = ResolveConfiguredEffectAmount(combatState, ownerCreature, cardPlay, effect);
		amount = ApplyConditionalBonusAmount(combatState, ownerCreature, cardPlay, effect, amount);
		if (amount <= 0)
		{
			return 0;
		}

		if (effect.ScaleMode != CardExtraEffectScaleMode.None)
		{
			int multiplier = GetHistoryCountMultiplier(combatState, ownerCreature, cardPlay, effect);
			if (!DoesCountConditionPass(multiplier, effect))
			{
				return 0;
			}

			if (effect.ScaleMode == CardExtraEffectScaleMode.ConditionOnly)
			{
				multiplier = 1;
			}

			if (effect.ScaleMode == CardExtraEffectScaleMode.PerHistoryCount)
			{
				if (multiplier <= 0 && !effect.HistoryScalingIncludesBase)
				{
					return 0;
				}

				long scaled = (long)amount * Math.Max(0, multiplier);
				long total = effect.HistoryScalingIncludesBase ? (long)amount + scaled : scaled;
				amount = total >= int.MaxValue ? int.MaxValue : total <= 0L ? 0 : (int)total;
				if (amount <= 0)
				{
					return 0;
				}
			}
		}

		int repeats = ResolveRepeatCount(cardPlay, effect);
		if (repeats <= 0)
		{
			return 0;
		}

		if (repeats > 1)
		{
			long repeated = (long)amount * repeats;
			amount = repeated >= int.MaxValue ? int.MaxValue : repeated <= 0L ? 0 : (int)repeated;
		}

		return amount <= 0 ? 0 : amount;
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

		IReadOnlyList<CardExtraEffect> existingTemp = GetActiveGrantedExtraEffects(combatState, card);

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
			&& GetEffectiveCountAggregationMode(existing) == GetEffectiveCountAggregationMode(candidate)
			&& existing.CardCostsLessDuration == candidate.CardCostsLessDuration
			&& existing.CardCostsLessTurns == candidate.CardCostsLessTurns
			&& existing.CardCostsLessModifier == candidate.CardCostsLessModifier;
	}

	// Signed delta: positive reduces cost, negative increases cost.
	internal static int GetCardCostsLessReduction(CombatState combatState, CardModel card)
		=> GetCardCostsLessAdjustment(combatState, card).Delta;

	internal static CardExtraEffect.CardCostAdjustment GetCardCostsLessAdjustment(CombatState combatState, CardModel card)
	{
		if (combatState == null || card == null)
		{
			return default;
		}

		Player? owner = card.Owner;
		Creature? ownerCreature = owner?.Creature;
		if (ownerCreature == null)
		{
			return default;
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
		bool forceFree = false;
		bool halfCost = false;

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

				CardExtraEffectCostModifier modifier = GetEffectiveCardCostsLessModifier(effect);
				if (modifier == CardExtraEffectCostModifier.FreeToPlay)
				{
					forceFree = true;
					continue;
				}
				if (modifier == CardExtraEffectCostModifier.HalfCost)
				{
					halfCost = true;
					continue;
				}
				if (modifier == CardExtraEffectCostModifier.Free)
				{
					totalDelta = int.MaxValue;
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

		Accumulate(GetActiveGrantedExtraEffects(combatState, card), includeTimedIntrinsic: true);

		if (totalDelta >= int.MaxValue)
		{
			return new CardExtraEffect.CardCostAdjustment(int.MaxValue, forceFree, halfCost);
		}
		if (totalDelta <= -int.MaxValue)
		{
			return new CardExtraEffect.CardCostAdjustment(-int.MaxValue, forceFree, halfCost);
		}
		return new CardExtraEffect.CardCostAdjustment((int)totalDelta, forceFree, halfCost);
	}

	// Signed delta: positive reduces Star cost, negative increases Star cost.
	internal static int GetCardStarCostsLessReduction(CombatState combatState, CardModel card)
		=> GetCardStarCostsLessAdjustment(combatState, card).Delta;

	internal static CardExtraEffect.CardCostAdjustment GetCardStarCostsLessAdjustment(CombatState combatState, CardModel card)
	{
		if (combatState == null || card == null)
		{
			return default;
		}

		Player? owner = card.Owner;
		Creature? ownerCreature = owner?.Creature;
		if (ownerCreature == null)
		{
			return default;
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
		bool forceFree = false;
		bool halfCost = false;

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

				CardExtraEffectCostModifier modifier = GetEffectiveCardCostsLessModifier(effect);
				if (modifier == CardExtraEffectCostModifier.FreeToPlay)
				{
					forceFree = true;
					continue;
				}
				if (modifier == CardExtraEffectCostModifier.HalfCost)
				{
					halfCost = true;
					continue;
				}
				if (modifier == CardExtraEffectCostModifier.Free)
				{
					totalDelta = int.MaxValue;
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

		Accumulate(GetActiveGrantedExtraEffects(combatState, card), includeTimedIntrinsic: true);

		if (totalDelta >= int.MaxValue)
		{
			return new CardExtraEffect.CardCostAdjustment(int.MaxValue, forceFree, halfCost);
		}
		if (totalDelta <= -int.MaxValue)
		{
			return new CardExtraEffect.CardCostAdjustment(-int.MaxValue, forceFree, halfCost);
		}
		return new CardExtraEffect.CardCostAdjustment((int)totalDelta, forceFree, halfCost);
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

	private static int GetHistoryCountMultiplier(CombatState combatState, Creature ownerCreature, CardPlay? cardPlay, CardExtraEffect effect, CardModel? sourceCardOverride = null)
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
				CardModel? sourceCard = sourceCardOverride ?? cardPlay?.Card;
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
							if (effect.CountExcludeSourceCard && sourceCard != null && ReferenceEquals(c, sourceCard))
							{
								continue;
							}
							if (!MatchesCountCardFilters(ownerPlayer, c, effect))
							{
								continue;
							}
							inPileCount += UsesCountCardAggregateAmount(effect)
								? GetCountAggregationAmount(c, effect)
								: 1;
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
					if (effect.CountExcludeSourceCard && sourceCard != null && ReferenceEquals(c, sourceCard))
					{
						continue;
					}
					if (!MatchesCountCardFilters(ownerPlayer, c, effect))
					{
						continue;
					}
					inPileCount += UsesCountCardAggregateAmount(effect)
						? GetCountAggregationAmount(c, effect)
						: 1;
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
					.Count(enemy => EnemyHasConfiguredStatus(enemy, effect.CountEnemyStatus, effect.CountPowerId));
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

			if (effect.CountEvent == CardExtraEffectCountEvent.ThisCardDamageDealt)
			{
				CardModel? sourceCard = sourceCardOverride ?? cardPlay?.Card;
				if (sourceCard == null)
				{
					return 0;
				}

				long thisCardDamage = history.Entries
					.OfType<DamageReceivedEntry>()
					.Where(e => e != null
						&& MatchesCountWindow(e, combatState, effect)
						&& ReferenceEquals(e.CardSource, sourceCard))
					.Sum(e => (long)Math.Max(0, e.Result.UnblockedDamage));
				return ClampLongToInt(thisCardDamage);
			}

			if (IsThisCardHistoryCountEvent(effect.CountEvent))
			{
				CardModel? sourceCard = sourceCardOverride ?? cardPlay?.Card;
				if (sourceCard == null)
				{
					return 0;
				}

				return effect.CountEvent switch
				{
					CardExtraEffectCountEvent.ThisCardDrawn => history.Entries
						.OfType<CardDrawnEntry>()
						.Count(e => e != null
							&& ReferenceEquals(e.Actor, ownerCreature)
							&& MatchesCountWindow(e, combatState, effect)
							&& ReferenceEquals(e.Card, sourceCard)),
					CardExtraEffectCountEvent.ThisCardDiscarded => history.Entries
						.OfType<CardDiscardedEntry>()
						.Count(e => e != null
							&& ReferenceEquals(e.Actor, ownerCreature)
							&& MatchesCountWindow(e, combatState, effect)
							&& ReferenceEquals(e.Card, sourceCard)),
					CardExtraEffectCountEvent.ThisCardExhausted => history.Entries
						.OfType<CardExhaustedEntry>()
						.Count(e => e != null
							&& ReferenceEquals(e.Actor, ownerCreature)
							&& MatchesCountWindow(e, combatState, effect)
							&& ReferenceEquals(e.Card, sourceCard)),
					_ => history.Entries
						.OfType<CardPlayStartedEntry>()
						.Count(e => e != null
							&& (cardPlay == null || !ReferenceEquals(e.CardPlay, cardPlay))
							&& ReferenceEquals(e.Actor, ownerCreature)
							&& MatchesCountWindow(e, combatState, effect)
							&& ReferenceEquals(e.CardPlay?.Card, sourceCard))
				};
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
				count += UsesCountCardAggregateAmount(effect)
					? GetCountAggregationAmount(historyCard, effect)
					: 1;
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
						&& PowerMatchesConfiguredStatus(e.Power, effect.CountEnemyStatus, effect.CountPowerId))
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
						&& PowerMatchesConfiguredStatus(e.Power, effect.CountEnemyStatus, effect.CountPowerId))
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
					.Sum(e => (long)Math.Max(0, e.Result.UnblockedDamage));
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
						&& e.Result.UnblockedDamage > 0);
				multiplier = ClampLongToInt(timesHit);
				return true;
			}
			case CardExtraEffectCountEvent.TimesDealtDamage:
			{
				long timesDealtDamage = history.Entries
					.OfType<DamageReceivedEntry>()
					.Count(e => e != null
						&& ReferenceEquals(e.Dealer, ownerCreature)
						&& MatchesCountWindow(e, combatState, effect)
						&& e.Result.TotalDamage > 0);
				multiplier = ClampLongToInt(timesDealtDamage);
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
			case CardExtraEffectCountEvent.OstyAlive:
			{
				multiplier = ownerCreature.Player?.IsOstyAlive == true ? 1 : 0;
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

	CardExtraEffectCountCardFilter filter = GetEffectiveCountCardFilter(effect);

	if (filter == CardExtraEffectCountCardFilter.CreatesCards)
	{
		if (!CardCreatesMatchingCards(owner, card, effect.CountCardPool, effect.CountCardType, effect))
		{
			return false;
		}
	}
	else
	{
		if (filter != CardExtraEffectCountCardFilter.Any && !MatchesCountCardEffectFilter(card, filter))
		{
			return false;
		}

		if (!PassesCostFilter(card, effect, effect.CountCardType))
		{
			return false;
		}

		if (!MatchesCountPool(owner, card, effect.CountCardPool))
		{
			return false;
		}

		if (!MatchesGeneratedCardType(card, effect.CountCardType))
		{
			return false;
		}

		if (!PassesNameFilter(card, effect))
		{
			return false;
		}
	}

	if (!PassesCardMatchFilter(card, effect))
	{
		return false;
		}

		return true;
	}

	private static int GetCountAggregationAmount(CardModel card, CardExtraEffect effect)
	{
		if (card == null || effect == null)
		{
			return 0;
		}

		return GetEffectiveCountAggregationMode(effect) switch
		{
			CardExtraEffectCountAggregationMode.MatchingEffectAmount => GetCountCardFilterAmount(card, effect),
			CardExtraEffectCountAggregationMode.CurrentEnergyCost => GetCardEnergyCostAmount(card, useBaseCost: false),
			CardExtraEffectCountAggregationMode.BaseEnergyCost => GetCardEnergyCostAmount(card, useBaseCost: true),
			CardExtraEffectCountAggregationMode.CurrentStarCost => GetCardStarCostAmount(card, useBaseCost: false),
			CardExtraEffectCountAggregationMode.BaseStarCost => GetCardStarCostAmount(card, useBaseCost: true),
			_ => 1
		};
	}

	private static int GetCardEnergyCostAmount(CardModel card, bool useBaseCost)
	{
		if (card == null)
		{
			return 0;
		}

		try
		{
			if (card.EnergyCost.CostsX)
			{
				return 0;
			}

			int cost = useBaseCost
				? card.EnergyCost.GetWithModifiers(CostModifiers.None)
				: card.EnergyCost.GetWithModifiers(CostModifiers.All);
			return Math.Max(0, cost);
		}
		catch
		{
			return 0;
		}
	}

	private static int GetCardStarCostAmount(CardModel card, bool useBaseCost)
	{
		if (card == null)
		{
			return 0;
		}

		try
		{
			if (card.HasStarCostX)
			{
				return 0;
			}

			int cost = useBaseCost ? card.BaseStarCost : card.CurrentStarCost;
			return Math.Max(0, cost);
		}
		catch
		{
			return 0;
		}
	}

	private static int GetCountCardFilterAmount(CardModel card, CardExtraEffect effect)
	{
		if (card == null || effect == null)
		{
			return 0;
		}

		CardExtraEffectCountCardFilter filter = GetEffectiveCountCardFilter(effect);
		if (!CountCardFilterSupportsAmount(filter))
		{
			return MatchesCountCardEffectFilter(card, filter) ? 1 : 0;
		}

		int currentDynamicAmount = GetCountCardFilterDynamicAmount(card, filter);
		int currentExtraAmount = GetCountCardFilterExtraEffectAmount(card, filter);

		if (!TryBuildCanonicalCountAmountCard(card, out CardModel? canonicalCard) || canonicalCard == null)
		{
			return Math.Max(currentDynamicAmount, currentExtraAmount);
		}

		int canonicalDynamicAmount = GetCountCardFilterDynamicAmount(canonicalCard, filter);
		int canonicalExtraAmount = GetCountCardFilterExtraEffectAmount(canonicalCard, filter);

		int totalAmount = currentDynamicAmount > 0 ? currentDynamicAmount : currentExtraAmount;
		int additionalExtraAmount = Math.Max(0, currentExtraAmount - canonicalExtraAmount);
		int dynamicIncrease = Math.Max(0, currentDynamicAmount - canonicalDynamicAmount);
		totalAmount += Math.Max(0, additionalExtraAmount - dynamicIncrease);

		return Math.Max(0, totalAmount);
	}

	private static bool TryBuildCanonicalCountAmountCard(CardModel card, out CardModel? canonicalCard)
	{
		canonicalCard = null;
		if (card == null)
		{
			return false;
		}

		try
		{
			CardModel canonical = ModelDb.GetById<CardModel>(card.Id);
			CardModel preview = canonical.ToMutable();
			int upgradeLevels = Math.Max(0, card.CurrentUpgradeLevel);
			bool prevSuppressAll = CardEditorOverrides.SuppressAllOverrides;
			bool prevSuppressUpgrade = CardEditorOverrides.SuppressUpgradeOverrides;
			try
			{
				CardEditorOverrides.SuppressAllOverrides = true;
				CardEditorOverrides.SuppressUpgradeOverrides = true;
				for (int i = 0; i < upgradeLevels && preview.IsUpgradable; i++)
				{
					preview.UpgradeInternal();
					preview.FinalizeUpgradeInternal();
				}
			}
			finally
			{
				CardEditorOverrides.SuppressAllOverrides = prevSuppressAll;
				CardEditorOverrides.SuppressUpgradeOverrides = prevSuppressUpgrade;
			}

			canonicalCard = preview;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static int GetCountCardFilterExtraEffectAmount(CardModel card, CardExtraEffectCountCardFilter filter)
	{
		if (card == null || filter == CardExtraEffectCountCardFilter.Any)
		{
			return 0;
		}

		int total = 0;
		try
		{
			foreach (CardExtraEffect effect in GetEffectsForDescription(card, isUpgradePreview: false))
			{
				if (effect == null || !DoesEffectContributeToCountCardFilter(effect, filter))
				{
					continue;
				}

				total += GetConfiguredCountableEffectAmount(effect);
			}
		}
		catch
		{
		}

		return Math.Max(0, total);
	}

	private static int GetConfiguredCountableEffectAmount(CardExtraEffect effect)
	{
		if (effect == null)
		{
			return 0;
		}

		return effect.AmountIsX
			? Math.Max(0, effect.Amount + effect.AmountXPlus)
			: Math.Max(0, effect.Amount);
	}

	private static bool DoesEffectContributeToCountCardFilter(CardExtraEffect effect, CardExtraEffectCountCardFilter filter)
	{
		return filter switch
		{
			CardExtraEffectCountCardFilter.GainBlock => effect.Kind == CardExtraEffectKind.GainBlock,
			CardExtraEffectCountCardFilter.DealDamage => effect.Kind == CardExtraEffectKind.DealDamage,
			CardExtraEffectCountCardFilter.DrawCards => effect.Kind == CardExtraEffectKind.DrawCards,
			CardExtraEffectCountCardFilter.GainEnergy => effect.Kind == CardExtraEffectKind.GainEnergy,
			CardExtraEffectCountCardFilter.GainStars => effect.Kind == CardExtraEffectKind.GainStars,
			CardExtraEffectCountCardFilter.Heal => effect.Kind == CardExtraEffectKind.Heal,
			CardExtraEffectCountCardFilter.LoseHp => effect.Kind == CardExtraEffectKind.LoseHp,
			CardExtraEffectCountCardFilter.Strength => effect.Kind is CardExtraEffectKind.GainStrength or CardExtraEffectKind.LoseStrength,
			CardExtraEffectCountCardFilter.Dexterity => effect.Kind is CardExtraEffectKind.GainDexterity or CardExtraEffectKind.LoseDexterity,
			CardExtraEffectCountCardFilter.Focus => effect.Kind is CardExtraEffectKind.GainFocus or CardExtraEffectKind.LoseFocus,
			CardExtraEffectCountCardFilter.Weak => effect.Kind == CardExtraEffectKind.ApplyWeak,
			CardExtraEffectCountCardFilter.Frail => effect.Kind == CardExtraEffectKind.ApplyFrail,
			CardExtraEffectCountCardFilter.Vulnerable => effect.Kind == CardExtraEffectKind.ApplyVulnerable,
			CardExtraEffectCountCardFilter.Poison => effect.Kind == CardExtraEffectKind.ApplyPoison,
			CardExtraEffectCountCardFilter.Doom => effect.Kind == CardExtraEffectKind.ApplyDoom,
			CardExtraEffectCountCardFilter.Constrict => effect.Kind == CardExtraEffectKind.ApplyConstrict,
			CardExtraEffectCountCardFilter.Artifact => effect.Kind == CardExtraEffectKind.GainArtifact,
			CardExtraEffectCountCardFilter.Thorns => effect.Kind == CardExtraEffectKind.GainThorns,
			CardExtraEffectCountCardFilter.Regen => effect.Kind == CardExtraEffectKind.GainRegen,
			CardExtraEffectCountCardFilter.Plating => effect.Kind == CardExtraEffectKind.GainPlating,
			CardExtraEffectCountCardFilter.Intangible => effect.Kind == CardExtraEffectKind.GainIntangible,
			CardExtraEffectCountCardFilter.Buffer => effect.Kind == CardExtraEffectKind.GainBuffer,
			CardExtraEffectCountCardFilter.Vigor => effect.Kind == CardExtraEffectKind.GainVigor,
			CardExtraEffectCountCardFilter.Blur => effect.Kind == CardExtraEffectKind.GainBlur,
			CardExtraEffectCountCardFilter.Ritual => effect.Kind == CardExtraEffectKind.GainRitual,
			CardExtraEffectCountCardFilter.Summon => effect.Kind == CardExtraEffectKind.Summon,
			CardExtraEffectCountCardFilter.Forge => effect.Kind == CardExtraEffectKind.Forge,
			_ => false
		};
	}

	private static int GetCountCardFilterDynamicAmount(CardModel card, CardExtraEffectCountCardFilter filter)
	{
		return filter switch
		{
			CardExtraEffectCountCardFilter.DealDamage => GetMaxDynamicVarAmount(card, "Damage", "CalculatedDamage", "ExtraDamage", "OstyDamage"),
			CardExtraEffectCountCardFilter.GainBlock => GetMaxDynamicVarAmount(card, "Block"),
			CardExtraEffectCountCardFilter.DrawCards => GetMaxDynamicVarAmount(card, "Cards"),
			CardExtraEffectCountCardFilter.GainEnergy => GetMaxDynamicVarAmount(card, "Energy"),
			CardExtraEffectCountCardFilter.GainStars => GetMaxDynamicVarAmount(card, "Stars"),
			CardExtraEffectCountCardFilter.Heal => GetMaxDynamicVarAmount(card, "Heal"),
			CardExtraEffectCountCardFilter.LoseHp => GetMaxDynamicVarAmount(card, "HpLoss"),
			CardExtraEffectCountCardFilter.Strength => GetMaxDynamicVarAmount(card, "StrengthPower"),
			CardExtraEffectCountCardFilter.Dexterity => GetMaxDynamicVarAmount(card, "DexterityPower"),
			CardExtraEffectCountCardFilter.Focus => GetMaxDynamicVarAmount(card, "FocusPower"),
			CardExtraEffectCountCardFilter.Weak => GetMaxDynamicVarAmount(card, "WeakPower"),
			CardExtraEffectCountCardFilter.Frail => GetMaxDynamicVarAmount(card, "FrailPower"),
			CardExtraEffectCountCardFilter.Vulnerable => GetMaxDynamicVarAmount(card, "VulnerablePower"),
			CardExtraEffectCountCardFilter.Poison => GetMaxDynamicVarAmount(card, "PoisonPower"),
			CardExtraEffectCountCardFilter.Doom => GetMaxDynamicVarAmount(card, "DoomPower"),
			CardExtraEffectCountCardFilter.Constrict => GetMaxDynamicVarAmount(card, "ConstrictPower"),
			CardExtraEffectCountCardFilter.Artifact => GetMaxDynamicVarAmount(card, "ArtifactPower"),
			CardExtraEffectCountCardFilter.Thorns => GetMaxDynamicVarAmount(card, "ThornsPower"),
			CardExtraEffectCountCardFilter.Regen => GetMaxDynamicVarAmount(card, "RegenPower"),
			CardExtraEffectCountCardFilter.Plating => GetMaxDynamicVarAmount(card, "PlatingPower"),
			CardExtraEffectCountCardFilter.Intangible => GetMaxDynamicVarAmount(card, "IntangiblePower"),
			CardExtraEffectCountCardFilter.Buffer => GetMaxDynamicVarAmount(card, "BufferPower"),
			CardExtraEffectCountCardFilter.Vigor => GetMaxDynamicVarAmount(card, "VigorPower"),
			CardExtraEffectCountCardFilter.Blur => GetMaxDynamicVarAmount(card, "BlurPower"),
			CardExtraEffectCountCardFilter.Ritual => GetMaxDynamicVarAmount(card, "RitualPower"),
			CardExtraEffectCountCardFilter.Summon => GetMaxDynamicVarAmount(card, "Summon"),
			CardExtraEffectCountCardFilter.Forge => GetMaxDynamicVarAmount(card, "Forge"),
			_ => 0
		};
	}

	private static int GetMaxDynamicVarAmount(CardModel card, params string[] keys)
	{
		if (card == null || keys == null || keys.Length == 0)
		{
			return 0;
		}

		int best = 0;
		foreach (string key in keys)
		{
			best = Math.Max(best, GetDynamicVarAmount(card, key));
		}

		return best;
	}

	private static int GetDynamicVarAmount(CardModel card, string key)
	{
		if (card == null || string.IsNullOrWhiteSpace(key))
		{
			return 0;
		}

		CardModel? cursor = card;
		for (int i = 0; i < 8 && cursor != null; i++)
		{
			try
			{
				if (cursor.DynamicVars.TryGetValue(key, out var dynamicVar))
				{
					return Math.Max(0, (int)decimal.Round(dynamicVar.BaseValue, 0, MidpointRounding.AwayFromZero));
				}
			}
			catch
			{
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

		return 0;
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

	internal static Task TriggerPowerCountEventAsync(CombatState combatState, Creature actor, CardExtraEffectCountEvent countEvent, ResourceCountSource source = ResourceCountSource.Other, PowerModel? triggeringPower = null, CardModel? triggeringCard = null, int amount = 1)
	{
		if (combatState == null || actor == null)
		{
			return Task.CompletedTask;
		}

		ulong? netId = LocalContext.NetId;
		if (!netId.HasValue)
		{
			return Task.CompletedTask;
		}

		return TriggerPowerCountEventAsync(combatState, actor, actor, countEvent, source, triggeringPower, triggeringCard, netId.Value, amount);
	}

	private static async Task TriggerPowerCountEventAsync(
		CombatState combatState,
		Creature powerOwner,
		Creature eventActor,
		CardExtraEffectCountEvent countEvent,
		ResourceCountSource source,
		PowerModel? triggeringPower,
		CardModel? triggeringCard,
		ulong netId,
		int amount)
	{
		if (combatState == null || powerOwner == null || eventActor == null)
		{
			return;
		}

		HashSet<Creature> listeners = new HashSet<Creature>(ReferenceEqualityComparer<Creature>.Instance);
		if (powerOwner != null)
		{
			listeners.Add(powerOwner);
		}

		foreach (Player player in combatState.Players)
		{
			if (player?.Creature != null)
			{
				listeners.Add(player.Creature);
			}
		}

		foreach (Creature enemy in combatState.Enemies)
		{
			if (enemy != null)
			{
				listeners.Add(enemy);
			}
		}

		foreach (Creature listener in listeners)
		{
			CardEditorExtraEffectPower? power = listener.GetPower<CardEditorExtraEffectPower>();
			if (power == null)
			{
				continue;
			}

			HookPlayerChoiceContext choiceContext = new HookPlayerChoiceContext(power, netId, combatState, GameActionType.Combat);
			await power.TriggerCountEvent(choiceContext, countEvent, source, triggeringCard: triggeringCard, triggeringPower: triggeringPower, eventActor: eventActor, amount: amount);
		}
	}

	internal static void TriggerPowerCountEvent(CombatState combatState, Creature actor, CardExtraEffectCountEvent countEvent, ResourceCountSource source = ResourceCountSource.Other, PowerModel? triggeringPower = null, CardModel? triggeringCard = null, int amount = 1)
	{
		TaskHelper.RunSafely(TriggerPowerCountEventAsync(combatState, actor, countEvent, source, triggeringPower, triggeringCard, amount));
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
	if (filter == CardExtraEffectCountCardFilter.CreatesCards)
	{
		if (!CardCreatesMatchingCards(owner, card, effect.CardSelectionPool, effect.CardSelectionType, effect))
		{
			return false;
		}
	}
	else
	{
		if (filter != CardExtraEffectCountCardFilter.Any && !MatchesCountCardEffectFilter(card, filter))
		{
			return false;
		}

		if (!PassesCostFilter(card, effect, effect.CardSelectionType))
		{
			return false;
		}

		if (!MatchesCountPool(owner, card, effect.CardSelectionPool))
		{
			return false;
		}

		if (!MatchesGeneratedCardType(card, effect.CardSelectionType))
		{
			return false;
		}

		if (!PassesNameFilter(card, effect))
		{
			return false;
		}
	}

	return true;
}

	internal static bool MatchesPowerTriggerCardFilters(Player? owner, CardModel triggeringCard, CardExtraEffect effect)
	{
		if (triggeringCard == null || effect == null)
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
	if (filter == CardExtraEffectCountCardFilter.CreatesCards)
	{
		if (!CardCreatesMatchingCards(owner, triggeringCard, effect.TriggerCardPool, effect.TriggerCardType, effect))
		{
			return false;
		}
	}
	else
	{
		if (filter != CardExtraEffectCountCardFilter.Any && !MatchesCountCardEffectFilter(triggeringCard, filter))
		{
			return false;
		}

		if (!PassesCostFilter(triggeringCard, effect, effect.TriggerCardType))
		{
			return false;
		}

		if (owner != null && !MatchesCountPool(owner, triggeringCard, effect.TriggerCardPool))
		{
			return false;
		}

		if (effect.TriggerCardPool != CardGeneratedCardPool.All && owner == null)
		{
			return false;
		}

		if (!MatchesGeneratedCardType(triggeringCard, effect.TriggerCardType))
		{
			return false;
		}

		if (!PassesNameFilter(triggeringCard, effect))
		{
			return false;
		}
	}

	if (!PassesCardMatchFilter(triggeringCard, effect))
	{
		return false;
		}

		return true;
	}

	internal static Creature? ResolvePowerHostCreature(CombatState combatState, CardPlay cardPlay, CardExtraEffect effect)
	{
		if (combatState == null || cardPlay?.Card?.Owner?.Creature == null || effect == null)
		{
			return null;
		}

		Creature ownerCreature = cardPlay.Card.Owner.Creature;
		if (GetEffectivePowerHost(effect) != CardExtraEffectPowerHost.TriggerTarget)
		{
			return ownerCreature;
		}

		return effect.Target switch
		{
			CardExtraEffectTarget.Self => ownerCreature,
			CardExtraEffectTarget.RandomEnemy => combatState.RunState.Rng.CombatTargets.NextItem(combatState.GetOpponentsOf(ownerCreature).Where(c => c.IsAlive)),
			CardExtraEffectTarget.AllEnemies => ResolveSingleTarget(combatState, ownerCreature, cardPlay),
			_ => ResolveSingleTarget(combatState, ownerCreature, cardPlay)
		};
	}

	internal static bool MatchesAffectedCardFilters(Player owner, CardModel card, CardExtraEffect effect)
	{
		if (owner == null || card == null || effect == null)
		{
			return true;
		}

	CardExtraEffectCountCardFilter filter = effect.TriggerCardFilter;
	if (filter == CardExtraEffectCountCardFilter.CreatesCards)
	{
		if (!CardCreatesMatchingCards(owner, card, effect.TriggerCardPool, effect.TriggerCardType, effect))
		{
			return false;
		}
	}
	else
	{
		if (filter != CardExtraEffectCountCardFilter.Any && !MatchesCountCardEffectFilter(card, filter))
		{
			return false;
		}

		if (!PassesCostFilter(card, effect, effect.TriggerCardType))
		{
			return false;
		}

		if (!MatchesCountPool(owner, card, effect.TriggerCardPool))
		{
			return false;
		}

		if (!MatchesGeneratedCardType(card, effect.TriggerCardType))
		{
			return false;
		}

		if (!PassesNameFilter(card, effect))
		{
			return false;
		}
	}

	if (!PassesCardMatchFilter(card, effect))
	{
		return false;
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
			case CardExtraEffectCountCardFilter.CreatesCards:
				return CardCreatesCards(card);
			default:
				return true;
		}
	}

	private static bool CardCreatesCards(CardModel? card, HashSet<string>? visitedSourceCardIds = null)
	{
		if (card == null)
		{
			return false;
		}

		visitedSourceCardIds ??= new HashSet<string>(StringComparer.Ordinal);
		string visitKey = card.Id.ToString();
		if (!visitedSourceCardIds.Add(visitKey))
		{
			return false;
		}

		try
		{
			foreach (CardExtraEffect effect in GetEffectsForDescription(card, isUpgradePreview: false))
			{
				if (EffectCreatesCards(effect, visitedSourceCardIds))
				{
					return true;
				}
			}
		}
		catch
		{
		}

		try
		{
			CombatState? combatState = card.CombatState;
			if (combatState != null)
			{
				foreach (CardExtraEffect effect in CardEditorTemporaryExtraEffectController.GetEffects(combatState, card))
				{
					if (EffectCreatesCards(effect, visitedSourceCardIds))
					{
						return true;
					}
				}
			}
		}
		catch
		{
		}

		return CardTypeCreatesCardsViaIl(card.GetType());
	}

	private static bool EffectCreatesCards(CardExtraEffect? effect, HashSet<string> visitedSourceCardIds)
	{
		if (effect == null)
		{
			return false;
		}

		if (effect.BranchEffect != null && EffectCreatesCards(effect.BranchEffect, visitedSourceCardIds))
		{
			return true;
		}

		switch (effect.Kind)
		{
			case CardExtraEffectKind.AddRandomCardToHand:
			case CardExtraEffectKind.ChooseOneOfThreeCardsToHand:
			case CardExtraEffectKind.PlayRandomGeneratedCard:
			case CardExtraEffectKind.AddCopyOfThisCard:
			case CardExtraEffectKind.AddExactCopyOfThisCardToDeck:
			case CardExtraEffectKind.AddSpecificCardToHand:
			case CardExtraEffectKind.CopyCardsFromPileToDeck:
			case CardExtraEffectKind.CopyExactCardsFromPileToDeck:
				return true;
			case CardExtraEffectKind.RunEffectSourceCard:
				if (!string.IsNullOrWhiteSpace(effect.SpecificCardId)
					&& TryParseSpecificCardId(effect.SpecificCardId.Trim(), out ModelId sourceId))
				{
					try
					{
						CardModel? sourceCard = ModelDb.GetByIdOrNull<CardModel>(sourceId);
						return sourceCard != null && CardCreatesCards(sourceCard, visitedSourceCardIds);
					}
					catch
					{
					}
				}
				return false;
			default:
				return false;
		}
	}

	private static bool CardTypeCreatesCardsViaIl(Type? cardType)
	{
		if (cardType == null)
		{
			return false;
		}

		lock (_cardCreatesCardsIlCacheLock)
		{
			if (_cardCreatesCardsIlCache.TryGetValue(cardType, out bool cached))
			{
				return cached;
			}
		}

		MethodInfo? onPlay = cardType.GetMethod("OnPlay", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		bool createsCards = MethodCallsGeneratedCardAdd(onPlay, cardType, new HashSet<MethodBase>(), remainingDepth: 2);

		lock (_cardCreatesCardsIlCacheLock)
		{
			_cardCreatesCardsIlCache[cardType] = createsCards;
		}

		return createsCards;
	}

	private static bool MethodCallsGeneratedCardAdd(MethodBase? method, Type ownerType, HashSet<MethodBase> visitedMethods, int remainingDepth)
	{
		if (method == null || remainingDepth < 0 || !visitedMethods.Add(method))
		{
			return false;
		}

		foreach (MethodBase calledMethod in EnumerateCalledMethods(method))
		{
			if (calledMethod.DeclaringType == typeof(CardPileCmd)
				&& string.Equals(calledMethod.Name, nameof(CardPileCmd.AddGeneratedCardToCombat), StringComparison.Ordinal))
			{
				return true;
			}

			if (remainingDepth > 0
				&& calledMethod.DeclaringType == ownerType
				&& MethodCallsGeneratedCardAdd(calledMethod, ownerType, visitedMethods, remainingDepth - 1))
			{
				return true;
			}
		}

		return false;
	}

	private static IEnumerable<MethodBase> EnumerateCalledMethods(MethodBase method)
	{
		MethodBody? body = method.GetMethodBody();
		byte[]? il = body?.GetILAsByteArray();
		if (il == null || il.Length == 0)
		{
			yield break;
		}

		Module module = method.Module;
		Type[] typeArgs = method.DeclaringType?.GetGenericArguments() ?? Type.EmptyTypes;
		Type[] methodArgs = method is MethodInfo methodInfo && methodInfo.IsGenericMethod
			? methodInfo.GetGenericArguments()
			: Type.EmptyTypes;

		int position = 0;
		while (position < il.Length)
		{
			OpCode opCode = ReadNextOpCode(il, ref position);
			if (string.IsNullOrEmpty(opCode.Name))
			{
				yield break;
			}

			int operandStart = position;
			int operandSize = GetOperandSize(opCode.OperandType, il, operandStart);
			if (operandSize < 0 || operandStart + operandSize > il.Length)
			{
				yield break;
			}

			if (opCode.OperandType == OperandType.InlineMethod)
			{
				int metadataToken = BitConverter.ToInt32(il, operandStart);
				MethodBase? resolvedMethod = null;
				try
				{
					resolvedMethod = module.ResolveMethod(metadataToken, typeArgs, methodArgs);
				}
				catch
				{
				}

				if (resolvedMethod != null)
				{
					yield return resolvedMethod;
				}
			}

			position = operandStart + operandSize;
		}
	}

	private static OpCode ReadNextOpCode(byte[] il, ref int position)
	{
		if (position >= il.Length)
		{
			return default;
		}

		byte first = il[position++];
		if (first != 0xFE)
		{
			return _oneByteOpCodes[first];
		}

		if (position >= il.Length)
		{
			return default;
		}

		byte second = il[position++];
		return _twoByteOpCodes[second];
	}

	private static int GetOperandSize(OperandType operandType, byte[] il, int operandStart)
	{
		return operandType switch
		{
			OperandType.InlineNone => 0,
			OperandType.ShortInlineBrTarget => 1,
			OperandType.ShortInlineI => 1,
			OperandType.ShortInlineVar => 1,
			OperandType.InlineVar => 2,
			OperandType.InlineI => 4,
			OperandType.InlineBrTarget => 4,
			OperandType.InlineField => 4,
			OperandType.InlineMethod => 4,
			OperandType.InlineSig => 4,
			OperandType.InlineString => 4,
			OperandType.InlineTok => 4,
			OperandType.InlineType => 4,
			OperandType.ShortInlineR => 4,
			OperandType.InlineI8 => 8,
			OperandType.InlineR => 8,
			OperandType.InlineSwitch => operandStart + 4 > il.Length ? -1 : 4 + (4 * BitConverter.ToInt32(il, operandStart)),
			_ => 0
		};
	}

	private static OpCode[] BuildOpCodeMap(bool twoByte)
	{
		OpCode[] map = new OpCode[256];
		foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
		{
			if (field.FieldType != typeof(OpCode))
			{
				continue;
			}

			OpCode opCode = (OpCode)field.GetValue(null)!;
			ushort value = unchecked((ushort)opCode.Value);
			if (!twoByte)
			{
				if (value < 0x100)
				{
					map[value] = opCode;
				}
			}
			else if ((value & 0xFF00) == 0xFE00)
			{
				map[value & 0xFF] = opCode;
			}
		}

		return map;
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
		CardPoolModel classPool = GetClassFilterPool(card);
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
				return ReferenceEquals(classPool, ModelDb.CardPool<IroncladCardPool>());
			case CardGeneratedCardPool.Silent:
				return ReferenceEquals(classPool, ModelDb.CardPool<SilentCardPool>());
			case CardGeneratedCardPool.Defect:
				return ReferenceEquals(classPool, ModelDb.CardPool<DefectCardPool>());
			case CardGeneratedCardPool.Regent:
				return ReferenceEquals(classPool, ModelDb.CardPool<RegentCardPool>());
			case CardGeneratedCardPool.Necrobinder:
				return ReferenceEquals(classPool, ModelDb.CardPool<NecrobinderCardPool>());
			case CardGeneratedCardPool.OtherColors:
				return IsAnyClassPool(classPool) && !ReferenceEquals(classPool, owner.Character.CardPool);
			default:
				return ReferenceEquals(classPool, owner.Character.CardPool);
		}
	}

	private static CardPoolModel GetClassFilterPool(CardModel card)
	{
		if (card == null)
		{
			return null;
		}

		CardPoolModel visualPool = card.VisualCardPool;
		if (IsAnyClassPool(visualPool))
		{
			return visualPool;
		}

		return card.Pool;
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

	private static CardModel CreateDeckCopyFromSource(Player owner, CardModel source, bool exact)
	{
		if (owner == null || source == null)
		{
			throw new ArgumentNullException();
		}

		if (exact)
		{
			return owner.RunState.CloneCard(source);
		}

		CardModel canonical = source.CanonicalInstance ?? ModelDb.GetById<CardModel>(source.Id);
		CardModel copy = owner.RunState.CreateCard(canonical, owner);
		int upgradeLevels = Math.Clamp(source.CurrentUpgradeLevel, 0, copy.MaxUpgradeLevel);
		for (int i = 0; i < upgradeLevels && copy.IsUpgradable; i++)
		{
			copy.UpgradeInternal();
			copy.FinalizeUpgradeInternal();
		}
		return copy;
	}

	private static CardModel CreateCombatCopyFromSource(CombatState combatState, Player owner, CardModel source, bool exact)
	{
		if (combatState == null || owner == null || source == null)
		{
			throw new ArgumentNullException();
		}

		if (exact)
		{
			return source.CreateClone();
		}

		CardModel canonical = source.CanonicalInstance ?? ModelDb.GetById<CardModel>(source.Id);
		CardModel copy = combatState.CreateCard(canonical, owner);
		int upgradeLevels = Math.Clamp(source.CurrentUpgradeLevel, 0, copy.MaxUpgradeLevel);
		for (int i = 0; i < upgradeLevels && copy.IsUpgradable; i++)
		{
			copy.UpgradeInternal();
			copy.FinalizeUpgradeInternal();
		}
		return copy;
	}

	private static async Task AddDeckCardCopy(Player owner, CardModel source, bool exact, List<(CardPileAddResult Result, PileType PileType)> results)
	{
		CardModel deckCard = CreateDeckCopyFromSource(owner, source, exact);
		results.Add((await CardPileCmd.Add(deckCard, PileType.Deck), PileType.Deck));
	}

	private static async Task AddCardCopyToConfiguredDestinations(CombatState combatState, Player owner, CardModel source, bool exact, IReadOnlyList<(PileType PileType, CardPilePosition Position)> destinations, List<(CardPileAddResult Result, PileType PileType)> results)
	{
		if (combatState == null || owner == null || source == null || destinations == null || results == null)
		{
			return;
		}

		foreach ((PileType toPileType, CardPilePosition position) in destinations)
		{
			if (toPileType == PileType.Deck)
			{
				await AddDeckCardCopy(owner, source, exact, results);
			}
			else if (toPileType.IsCombatPile())
			{
				CardModel copy = CreateCombatCopyFromSource(combatState, owner, source, exact);
				results.Add((await CardPileCmd.AddGeneratedCardToCombat(copy, toPileType, addedByPlayer: true, position), toPileType));
			}
		}
	}

	private static async Task AddCanonicalCardToConfiguredDestinations(CombatState combatState, Player owner, CardModel canonical, IReadOnlyList<(PileType PileType, CardPilePosition Position)> destinations, List<(CardPileAddResult Result, PileType PileType)> results)
	{
		if (combatState == null || owner == null || canonical == null || destinations == null || results == null)
		{
			return;
		}

		foreach ((PileType toPileType, CardPilePosition position) in destinations)
		{
			if (toPileType == PileType.Deck)
			{
				CardModel generated = owner.RunState.CreateCard(canonical, owner);
				results.Add((await CardPileCmd.Add(generated, PileType.Deck), PileType.Deck));
			}
			else if (toPileType.IsCombatPile())
			{
				CardModel generated = combatState.CreateCard(canonical, owner);
				results.Add((await CardPileCmd.AddGeneratedCardToCombat(generated, toPileType, addedByPlayer: true, position), toPileType));
			}
		}
	}

	private static async Task AddRandomCardsToHand(CombatState combatState, Player owner, int amount, CardGeneratedCardPool pool, CardGeneratedCardType type, CardExtraEffect effect)
	{
		if (combatState == null || owner == null || amount <= 0)
		{
			return;
		}

		List<CardModel> candidates = GetCardGenerationCandidates(owner, pool, type, effect.GeneratedCardCustomTag, effect);
		if (candidates.Count == 0)
		{
			return;
		}

		List<(PileType PileType, CardPilePosition Position)> destinations = ResolveGeneratedCombatDestinations(effect, effect?.MoveToPile ?? CardExtraEffectCardPile.Hand, effect?.MoveToPosition ?? CardExtraEffectCardPilePosition.Bottom);
		List<(CardPileAddResult Result, PileType PileType)> results = new();
		for (int i = 0; i < amount; i++)
		{
			CardModel canonical = owner.RunState.Rng.CombatCardGeneration.NextItem(candidates);
			if (canonical == null)
			{
				continue;
			}

			await AddCanonicalCardToConfiguredDestinations(combatState, owner, canonical, destinations, results);
		}
		PreviewGeneratedPileAdds(results);
	}

	private static async Task ChooseOneOfThreeCardsToHand(CombatState combatState, PlayerChoiceContext choiceContext, Player owner, int times, CardGeneratedCardPool pool, CardGeneratedCardType type, CardExtraEffect effect)
	{
		if (combatState == null || choiceContext == null || owner == null)
		{
			return;
		}
		times = Math.Max(1, times);

		List<CardModel> candidates = GetCardGenerationCandidates(owner, pool, type, effect.GeneratedCardCustomTag, effect);
		if (candidates.Count == 0)
		{
			return;
		}

		List<(PileType PileType, CardPilePosition Position)> destinations = ResolveGeneratedCombatDestinations(effect, effect?.MoveToPile ?? CardExtraEffectCardPile.Hand, effect?.MoveToPosition ?? CardExtraEffectCardPilePosition.Bottom);
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
				List<(CardPileAddResult Result, PileType PileType)> results = new();
				bool usedSelectedCombatCard = false;
				for (int destinationIndex = 0; destinationIndex < destinations.Count; destinationIndex++)
				{
					(PileType toPileType, CardPilePosition position) = destinations[destinationIndex];
					if (toPileType == PileType.Deck)
					{
						await AddDeckCardCopy(owner, selected, exact: false, results);
					}
					else if (toPileType.IsCombatPile())
					{
						CardModel cardToAdd = !usedSelectedCombatCard ? selected : selected.CreateClone();
						usedSelectedCombatCard = true;
						results.Add((await CardPileCmd.AddGeneratedCardToCombat(cardToAdd, toPileType, addedByPlayer: true, position), toPileType));
					}
				}
				PreviewGeneratedPileAdds(results);
			}
		}
	}

private static async Task PlayRandomGeneratedCards(CombatState combatState, PlayerChoiceContext choiceContext, Player owner, int amount, CardGeneratedCardPool pool, CardGeneratedCardType type, CardExtraEffect effect)
{
	if (combatState == null || choiceContext == null || owner == null || amount <= 0)
	{
			return;
		}

		List<CardModel> candidates = GetCardGenerationCandidates(owner, pool, type, effect.GeneratedCardCustomTag, effect);
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
			await CardPileCmd.AddGeneratedCardToCombat(generated, PileType.Hand, addedByPlayer: true, CardPilePosition.Bottom);
			await CardCmd.AutoPlay(choiceContext, generated, target: null);
		}
	}

private static CardModel? ResolveChooseOneQueryRepresentativeCard(Player owner, CardExtraEffectChooseOneOption option)
{
	if (owner == null || option == null)
	{
		return null;
	}

	if (option.QueryMatchMode == CardExtraEffectCardMatchMode.CardId
		&& TryParseSpecificCardId(option.QueryMatchCardId ?? string.Empty, out ModelId exactId))
	{
		CardModel? exactCard = ModelDb.GetByIdOrNull<CardModel>(exactId);
		if (exactCard != null)
		{
			return exactCard;
		}
	}

	CardExtraEffect queryEffect = BuildChooseOneQueryEffect(option);
	if (option.QuerySource == CardExtraEffectChooseOneQuerySource.Compendium)
	{
		return GetCardGenerationCandidates(owner, option.QueryPool, option.QueryType, null, queryEffect).FirstOrDefault();
	}

	return GetCandidatesFromConfiguredPile(
		owner,
		queryEffect,
		sourceCard: null,
		includeDeck: true,
		requireDeckVersion: false,
		includeCostFilter: true).FirstOrDefault();
}

private static CardModel? BuildChooseOneQueryDisplayCard(Player owner, CardModel hostCard, CardExtraEffectChooseOneOption option)
{
	CardModel? representative = ResolveChooseOneQueryRepresentativeCard(owner, option) ?? hostCard;
	if (representative == null)
	{
		return null;
	}

	try
	{
		if (hostCard?.CombatState != null)
		{
			return hostCard.CombatState.CreateCard(representative, owner);
		}

		return representative.ToMutable();
	}
	catch
	{
		return representative;
	}
}

private static async Task PlayMatchingGeneratedCards(CombatState? combatState, PlayerChoiceContext choiceContext, Player owner, CardExtraEffect effect, int amount)
{
	if (combatState == null || choiceContext == null || owner == null || effect == null || amount <= 0)
	{
		return;
	}

	List<CardModel> candidates = GetCardGenerationCandidates(owner, effect.CardSelectionPool, effect.CardSelectionType, null, effect);
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

	List<CardModel> selected = await SelectCardsFromCandidates(
		choiceContext,
		owner,
		candidates,
		count,
		effect.CardSelectionMode,
		PileType.None,
		new LocString("gameplay_ui", "CHOOSE_CARD_HEADER"),
		sourceCard: null,
		excludeSourceCardInHandSelector: true,
		preferHandDiscardSelector: false,
		owner.RunState?.Rng?.Shuffle);
	if (selected.Count == 0)
	{
		return;
	}

	foreach (CardModel canonical in selected)
	{
		if (canonical == null)
		{
			continue;
		}

		CardModel generated = combatState.CreateCard(canonical, owner);
		await CardPileCmd.AddGeneratedCardToCombat(generated, PileType.Hand, addedByPlayer: true, CardPilePosition.Bottom);
		await CardCmd.AutoPlay(choiceContext, generated, target: null);
	}
}

private static async Task ExecuteChooseOneQueryOption(PlayerChoiceContext choiceContext, Player owner, CardModel hostCard, CardExtraEffectChooseOneOption option)
{
	if (choiceContext == null || owner == null || option == null)
	{
		return;
	}

	CardExtraEffect queryEffect = BuildChooseOneQueryEffect(option);
	int count = Math.Clamp(option.QueryCount <= 0 ? 1 : option.QueryCount, 1, 99);
	if (option.QuerySource == CardExtraEffectChooseOneQuerySource.Compendium)
	{
		await PlayMatchingGeneratedCards(hostCard?.CombatState ?? owner.Creature?.CombatState, choiceContext, owner, queryEffect, count);
		return;
	}

	await PlayCardsFromPile(choiceContext, owner, count, queryEffect, sourceCard: hostCard);
}

private static async Task ChooseOneEffectSourceCard(PlayerChoiceContext choiceContext, Player owner, CardModel hostCard, CardPlay cardPlay, CardExtraEffect effect)
{
	List<CardExtraEffectChooseOneOption> options = GetChooseOneOptions(effect);
	if (options.Count == 0)
	{
		return;
	}

	if (options.Count == 1)
	{
		CardExtraEffectChooseOneOption onlyOption = options[0];
		if (TryGetChooseOneExactSourceId(onlyOption, out ModelId exactSourceId))
		{
			await CardEditorCreatedCardEffectSourceSupport.RunSingleEffectSourceOnPlay(hostCard, choiceContext, cardPlay, exactSourceId, GetChooseOneEffectSourceRuntimeInstanceKey(hostCard, effect, 0, exactSourceId));
		}
		else
		{
			await ExecuteChooseOneQueryOption(choiceContext, owner, hostCard, onlyOption);
		}

		return;
	}

	List<CardModel> optionCards = new();
	List<CardExtraEffectChooseOneOption> resolvedOptions = new();
	List<ModelId> optionSourceIds = new();
	List<string> optionRuntimeKeys = new();
	for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
	{
		CardExtraEffectChooseOneOption option = options[optionIndex];
		ModelId sourceId = ModelId.none;
		string runtimeKey = string.Empty;
		CardModel? optionCard;
		if (TryGetChooseOneExactSourceId(option, out sourceId))
		{
			runtimeKey = GetChooseOneEffectSourceRuntimeInstanceKey(hostCard, effect, optionIndex, sourceId);
			optionCard = CardEditorCreatedCardEffectSourceSupport.BuildRuntimeEffectSourceCard(hostCard, sourceId, isUpgradePreview: false, runtimeKey);
		}
		else
		{
			optionCard = BuildChooseOneQueryDisplayCard(owner, hostCard, option);
		}

		if (optionCard == null)
		{
			continue;
		}

		optionCards.Add(optionCard);
		resolvedOptions.Add(option);
		optionSourceIds.Add(sourceId);
		optionRuntimeKeys.Add(runtimeKey);
	}

		if (optionCards.Count == 0)
		{
			return;
		}

	if (optionCards.Count == 1)
	{
		if (TryGetChooseOneExactSourceId(resolvedOptions[0], out ModelId singleSourceId))
		{
			await CardEditorCreatedCardEffectSourceSupport.RunSingleEffectSourceOnPlay(hostCard, choiceContext, cardPlay, singleSourceId, optionRuntimeKeys[0]);
		}
		else
		{
			await ExecuteChooseOneQueryOption(choiceContext, owner, hostCard, resolvedOptions[0]);
		}

		return;
	}

		CardModel? selectedCard = await CardSelectCmd.FromChooseACardScreen(choiceContext, optionCards, owner, canSkip: false);
		if (selectedCard == null)
		{
			return;
		}

		int selectedIndex = optionCards.IndexOf(selectedCard);
	if (selectedIndex < 0 || selectedIndex >= resolvedOptions.Count)
	{
		return;
	}

	if (TryGetChooseOneExactSourceId(resolvedOptions[selectedIndex], out ModelId selectedSourceId))
	{
		await CardEditorCreatedCardEffectSourceSupport.RunSingleEffectSourceOnPlay(hostCard, choiceContext, cardPlay, selectedSourceId, optionRuntimeKeys[selectedIndex]);
	}
	else
	{
		await ExecuteChooseOneQueryOption(choiceContext, owner, hostCard, resolvedOptions[selectedIndex]);
	}
}

	private static string GetRunEffectSourceRuntimeInstanceKey(CardModel card, CardExtraEffect effect)
	{
		if (card == null || effect == null || string.IsNullOrWhiteSpace(effect.SpecificCardId) || !TryParseSpecificCardId(effect.SpecificCardId, out ModelId sourceId))
		{
			return CardEditorCreatedCardEffectSourceSupport.CreateRuntimeSourceInstanceKey(ModelId.none, 0, "runEffectSource");
		}

		int occurrence = 0;
		foreach (CardExtraEffect candidate in GetEffectsForDescription(card, isUpgradePreview: false))
		{
			if (candidate == null || candidate.Kind != CardExtraEffectKind.RunEffectSourceCard)
			{
				continue;
			}

			if (!string.Equals(candidate.SpecificCardId?.Trim(), effect.SpecificCardId?.Trim(), StringComparison.Ordinal))
			{
				continue;
			}

			if (ReferenceEquals(candidate, effect))
			{
				break;
			}

			occurrence++;
		}

		return CardEditorCreatedCardEffectSourceSupport.CreateRuntimeSourceInstanceKey(sourceId, occurrence, "runEffectSource");
	}

	private static string GetScalingStageRuntimeInstanceKey(CardModel card, CardExtraEffect effect)
	{
		if (card == null || effect == null || string.IsNullOrWhiteSpace(effect.SpecificCardId) || !TryParseSpecificCardId(effect.SpecificCardId, out ModelId sourceId))
		{
			return CardEditorCreatedCardEffectSourceSupport.CreateRuntimeSourceInstanceKey(ModelId.none, 0, "scalingStage");
		}

		int occurrence = 0;
		foreach (CardExtraEffect candidate in GetEffectsForDescription(card, isUpgradePreview: false))
		{
			if (candidate == null || candidate.Kind != CardExtraEffectKind.ScalingStage)
			{
				continue;
			}

			if (!string.Equals(candidate.SpecificCardId?.Trim(), effect.SpecificCardId?.Trim(), StringComparison.Ordinal))
			{
				continue;
			}

			if (ReferenceEquals(candidate, effect))
			{
				break;
			}

			occurrence++;
		}

		return CardEditorCreatedCardEffectSourceSupport.CreateRuntimeSourceInstanceKey(sourceId, occurrence, "scalingStage");
	}

	private static string GetEffectSourceRuntimeInstanceKey(CardModel card, CardExtraEffect effect)
	{
		return effect?.Kind == CardExtraEffectKind.ScalingStage
			? GetScalingStageRuntimeInstanceKey(card, effect)
			: GetRunEffectSourceRuntimeInstanceKey(card, effect);
	}

	private static string GetChooseOneEffectSourceRuntimeInstanceKey(CardModel hostCard, CardExtraEffect effect, int optionIndex, ModelId sourceId)
	{
		int occurrence = 0;
		foreach (CardExtraEffect candidate in GetEffectsForDescription(hostCard, isUpgradePreview: false))
		{
			if (candidate == null || candidate.Kind != CardExtraEffectKind.ChooseOneEffectSource)
			{
				continue;
			}

			if (ReferenceEquals(candidate, effect))
			{
				break;
			}

			occurrence++;
		}

		return CardEditorCreatedCardEffectSourceSupport.CreateRuntimeSourceInstanceKey(sourceId, Math.Max(0, optionIndex), $"chooseOne:{occurrence}");
	}

	private static List<(PileType PileType, CardPilePosition Position)> ResolveGeneratedCombatDestinations(CardExtraEffect? effect, CardExtraEffectCardPile configuredPile, CardExtraEffectCardPilePosition configuredPosition)
	{
		return GetGeneratedCardDestinations(effect, configuredPile, configuredPosition)
			.Select(dest => (ResolvePileType(dest.Pile), ResolvePilePosition(dest.Position)))
			.ToList();
	}

	private static void PreviewGeneratedPileAdds(IReadOnlyList<(CardPileAddResult Result, PileType PileType)> results)
	{
		if (results == null || results.Count == 0)
		{
			return;
		}

		foreach (IGrouping<PileType, CardPileAddResult> group in results.GroupBy(r => r.PileType, r => r.Result))
		{
			if (group.Key == PileType.Hand)
			{
				continue;
			}

			List<CardPileAddResult> pileResults = group.ToList();
			CardPreviewStyle style = pileResults.Count > 5 ? CardPreviewStyle.MessyLayout : CardPreviewStyle.HorizontalLayout;
			CardCmd.PreviewCardPileAdd(pileResults, 1.2f, style);
		}
	}

	private static List<CardModel> GetCardGenerationCandidates(Player owner, CardGeneratedCardPool pool, CardGeneratedCardType type, string? customTag, CardExtraEffect? effect = null)
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

		string requiredCustomTag = customTag?.Trim() ?? string.Empty;
		if (!string.IsNullOrWhiteSpace(requiredCustomTag))
		{
			filtered = filtered.Where(card =>
				CardEditorOverrides.TryGetEffectiveOverride(card.Id, out CardOverride overrideData)
				&& overrideData.CustomTags != null
				&& overrideData.CustomTags.Contains(requiredCustomTag));
		}

	if (effect != null && effect.CostFilterEnabled)
	{
		filtered = filtered.Where(card => PassesCostFilter(card, effect, type));
	}

	if (effect != null && effect.CardMatchMode != CardExtraEffectCardMatchMode.Any)
	{
		filtered = filtered.Where(card => PassesCardMatchFilter(card, effect));
	}

	if (effect != null && effect.NameFilterEnabled)
	{
		filtered = filtered.Where(card => PassesNameFilter(card, effect));
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
		target = GetEffectiveResolvedTarget(cardPlay, target);

		switch (target)
		{
			case CardExtraEffectTarget.Self:
				return new[] { ownerCreature };
			case CardExtraEffectTarget.AllAllies:
				return ResolveFriendlyGroupTargets(combatState, ownerCreature, includeSelf: true);
			case CardExtraEffectTarget.AnyPlayer:
			{
				Creature? picked = ResolveFriendlySingleTarget(combatState, ownerCreature, cardPlay, includeSelf: true);
				return picked != null ? new[] { picked } : Array.Empty<Creature>();
			}
			case CardExtraEffectTarget.AnyAlly:
			{
				Creature? picked = ResolveFriendlySingleTarget(combatState, ownerCreature, cardPlay, includeSelf: false);
				return picked != null ? new[] { picked } : Array.Empty<Creature>();
			}
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

	private static IEnumerable<Creature> ResolveFriendlyGroupTargets(CombatState combatState, Creature ownerCreature, bool includeSelf)
	{
		IEnumerable<Creature> source = ownerCreature.IsPlayer
			? combatState.PlayerCreatures
			: combatState.GetTeammatesOf(ownerCreature);
		return source.Where(creature => creature != null && creature.IsAlive && (includeSelf || creature != ownerCreature));
	}

	private static Creature? ResolveFriendlySingleTarget(CombatState combatState, Creature ownerCreature, CardPlay cardPlay, bool includeSelf)
	{
		if (cardPlay.Target != null && cardPlay.Target.IsAlive && IsFriendlyTarget(ownerCreature, cardPlay.Target, includeSelf))
		{
			return cardPlay.Target;
		}

		if (TryGetManualTarget(cardPlay.Card, out Creature? manualTarget)
			&& manualTarget != null
			&& manualTarget.IsAlive
			&& IsFriendlyTarget(ownerCreature, manualTarget, includeSelf))
		{
			return manualTarget;
		}

		if (includeSelf && ownerCreature.IsAlive)
		{
			return ownerCreature;
		}

		return ResolveFriendlyGroupTargets(combatState, ownerCreature, includeSelf).FirstOrDefault();
	}

	private static bool IsFriendlyTarget(Creature ownerCreature, Creature candidate, bool includeSelf)
	{
		if (candidate == null || !candidate.IsAlive)
		{
			return false;
		}

		if (!includeSelf && candidate == ownerCreature)
		{
			return false;
		}

		if (ownerCreature.IsPlayer)
		{
			return candidate.IsPlayer;
		}

		return candidate == ownerCreature || candidate.PetOwner == ownerCreature.PetOwner || candidate.IsPlayer == ownerCreature.IsPlayer;
	}

	private static Creature? ResolveSingleTarget(CombatState combatState, Creature ownerCreature, CardPlay cardPlay)
	{
		if (cardPlay.Target != null && cardPlay.Target.IsAlive)
		{
			return cardPlay.Target;
		}
		if (cardPlay?.Card?.TargetType == TargetType.Self)
		{
			return ownerCreature.IsAlive ? ownerCreature : null;
		}
		if (cardPlay?.Card?.TargetType == TargetType.Osty)
		{
			Creature? osty = ownerCreature.Player?.Osty;
			return osty != null && osty.IsAlive ? osty : null;
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

	private static CardExtraEffectTarget GetEffectiveResolvedTarget(CardPlay cardPlay, CardExtraEffectTarget requestedTarget)
	{
		if (requestedTarget != CardExtraEffectTarget.Target || cardPlay?.Card == null)
		{
			return requestedTarget;
		}

		return cardPlay.Card.TargetType switch
		{
			TargetType.Self => CardExtraEffectTarget.Self,
			TargetType.RandomEnemy => CardExtraEffectTarget.RandomEnemy,
			TargetType.AllEnemies => CardExtraEffectTarget.AllEnemies,
			TargetType.AnyAlly => CardExtraEffectTarget.AnyAlly,
			TargetType.AllAllies => CardExtraEffectTarget.AllAllies,
			TargetType.AnyPlayer => CardExtraEffectTarget.AnyPlayer,
			_ => requestedTarget
		};
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
		bool numericFieldsAreDeltas = overrideData.Upgrade.ExtraEffectNumericFieldsAreDeltas;
		UpgradeEffectAlignment alignment = AlignUpgradeEffectsToBaseSlots(baseEffects, upgradeEffects);
		int upgradeApplications = GetExtraEffectUpgradeApplicationCount(card, overrideData);

		for (int baseSlotIndex = 0; baseSlotIndex < alignment.BaseSlotEffects.Length; baseSlotIndex++)
		{
			CardExtraEffect? upgradeEffect = alignment.BaseSlotEffects[baseSlotIndex];
			if (upgradeEffect == null)
			{
				continue;
			}

			if (upgradeEffect.DisableOnUpgrade)
			{
				fused[baseSlotIndex] = null!;
				continue;
			}

			CardExtraEffect baseEffect = baseEffects[baseSlotIndex];
			if (!HasMeaningfulUpgradeBaseSlotDelta(baseEffect, upgradeEffect, numericFieldsAreDeltas))
			{
				continue;
			}

			CardExtraEffect mergedEffect = CloneEffect(baseEffect);
			for (int applicationIndex = 0; applicationIndex < upgradeApplications; applicationIndex++)
			{
				mergedEffect = MergeUpgradeBaseSlotEffect(mergedEffect, upgradeEffect, numericFieldsAreDeltas);
			}

			fused[baseSlotIndex] = mergedEffect;
		}

		for (int i = 0; i < upgradeEffects.Count; i++)
		{
			CardExtraEffect? upgradeEffect = upgradeEffects[i];
			if (upgradeEffect == null || alignment.UpgradeIndexToBaseSlot[i] >= 0)
			{
				continue;
			}

			if (!ShouldTreatUnmatchedUpgradeEffectAsAbsolute(baseEffects.Count, i, alignment.LastMatchedUpgradeIndex))
			{
				continue;
			}

			if (!upgradeEffect.DisableOnUpgrade)
			{
				fused.Add(CloneEffect(upgradeEffect));
			}
		}

		return fused;
	}

	private static int GetExtraEffectUpgradeApplicationCount(CardModel card, CardOverride overrideData)
	{
		if (overrideData?.EndlessUpgrades != true || card == null)
		{
			return 1;
		}

		return Math.Max(1, card.CurrentUpgradeLevel);
	}

	private static CardExtraEffect MergeUpgradeBaseSlotEffect(CardExtraEffect baseEffect, CardExtraEffect upgradeEffect, bool numericFieldsAreDeltas)
	{
		CardExtraEffect fused = CloneEffect(baseEffect);
		bool repeatModeChanged = baseEffect.RepeatIsX != upgradeEffect.RepeatIsX;

		if (baseEffect.AmountIsX)
		{
			fused.AmountXPlus = numericFieldsAreDeltas
				? baseEffect.AmountXPlus + upgradeEffect.Amount
				: upgradeEffect.Amount;
		}
		else
		{
			int mergedAmount = numericFieldsAreDeltas
				? (baseEffect.Kind == CardExtraEffectKind.CreatedCardsCostLess
					&& (baseEffect.Amount == -1 || upgradeEffect.Amount == -1)
						? -1
						: baseEffect.Amount + upgradeEffect.Amount)
				: upgradeEffect.Amount;
			fused.Amount = IsValidEffectAmount(baseEffect.Kind, mergedAmount) ? mergedAmount : 0;
		}

		fused.RepeatIsX = upgradeEffect.RepeatIsX;

		if (numericFieldsAreDeltas)
		{
			fused.Turns = AddUpgradeDelta(baseEffect.Turns, upgradeEffect.Turns, 0, 99);
			if (fused.RepeatIsX)
			{
				fused.RepeatCount = repeatModeChanged
					? Math.Clamp(upgradeEffect.RepeatCount, 0, 99)
					: Math.Clamp(baseEffect.RepeatCount + upgradeEffect.RepeatCount, 0, 99);
			}
			else
			{
				fused.RepeatCount = repeatModeChanged
					? Math.Clamp(upgradeEffect.RepeatCount <= 0 ? 1 : upgradeEffect.RepeatCount, 1, 99)
					: AddUpgradeDelta(baseEffect.RepeatCount, upgradeEffect.RepeatCount, 1, 99);
			}
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
			fused.ConditionalBonusAmount = Math.Clamp(baseEffect.ConditionalBonusAmount + upgradeEffect.ConditionalBonusAmount, -99, 99);
		}
		else
		{
			fused.Turns = upgradeEffect.Turns;
			fused.RepeatCount = Math.Clamp(upgradeEffect.RepeatCount, fused.RepeatIsX ? 0 : 1, 99);
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
			fused.ConditionalBonusAmount = upgradeEffect.ConditionalBonusAmount;
		}

		// Some upgrade deltas are absolute (not additive) even when numeric fields are deltas.
		// Only merge fields that are safe and exposed in the upgrade delta UI.
		if (baseEffect.Kind == CardExtraEffectKind.OrbAction && upgradeEffect.Kind == CardExtraEffectKind.OrbAction)
		{
			fused.OrbAction = upgradeEffect.OrbAction;
			fused.OrbScope = upgradeEffect.OrbScope;
			fused.OrbType = upgradeEffect.OrbType;
			fused.OrbSelection = upgradeEffect.OrbSelection;
			fused.OrbFollowUp = upgradeEffect.OrbFollowUp;
		}

		fused.CountExcludeSourceCard = upgradeEffect.CountExcludeSourceCard;
		fused.CountAggregationMode = GetEffectiveCountAggregationMode(upgradeEffect);
		fused.CountUsesCardEffectAmount = fused.CountAggregationMode == CardExtraEffectCountAggregationMode.MatchingEffectAmount;
		fused.CreatedCardsCostResource = upgradeEffect.CreatedCardsCostResource;
		fused.CardCostsLessModifier = upgradeEffect.CardCostsLessModifier;
		fused.UseMoveDestinationForGeneratedCards = upgradeEffect.UseMoveDestinationForGeneratedCards;
		fused.AdditionalMoveToPiles = upgradeEffect.AdditionalMoveToPiles;
		fused.PowerTriggerEnemyStatus = upgradeEffect.PowerTriggerEnemyStatus;
		fused.PowerTriggerPowerId = upgradeEffect.PowerTriggerPowerId;
		fused.PowerTriggerUsesEventAmount = upgradeEffect.PowerTriggerUsesEventAmount;
		fused.ConditionalBonusConditionType = upgradeEffect.ConditionalBonusConditionType;
		fused.ConditionalBonusCondition = upgradeEffect.ConditionalBonusCondition;
		fused.ConditionalBonusEnemyStatus = upgradeEffect.ConditionalBonusEnemyStatus;
		fused.ConditionalBonusEnemyIntent = upgradeEffect.ConditionalBonusEnemyIntent;
		fused.BranchMode = upgradeEffect.BranchMode;
		fused.BranchConditionType = upgradeEffect.BranchConditionType;
		fused.BranchCondition = upgradeEffect.BranchCondition;
		fused.BranchEnemyStatus = upgradeEffect.BranchEnemyStatus;
		fused.BranchEnemyIntent = upgradeEffect.BranchEnemyIntent;
		fused.BranchCountEvent = upgradeEffect.BranchCountEvent;
		fused.BranchCountWindow = upgradeEffect.BranchCountWindow;
		fused.BranchCountWindowInclusion = upgradeEffect.BranchCountWindowInclusion;
		fused.BranchBlockLostCountingMode = upgradeEffect.BranchBlockLostCountingMode;
		fused.BranchCountTurns = upgradeEffect.BranchCountTurns;
		fused.BranchCountCardPile = upgradeEffect.BranchCountCardPile;
		fused.BranchCountCardPool = upgradeEffect.BranchCountCardPool;
		fused.BranchCountCardType = upgradeEffect.BranchCountCardType;
		fused.BranchCountCardFilter = upgradeEffect.BranchCountCardFilter;
		fused.BranchCountAggregationMode = GetEffectiveBranchCountAggregationMode(upgradeEffect);
		fused.BranchCountUsesCardEffectAmount = fused.BranchCountAggregationMode == CardExtraEffectCountAggregationMode.MatchingEffectAmount;
		fused.BranchCountExcludeSourceCard = upgradeEffect.BranchCountExcludeSourceCard;
		fused.BranchCountOrbType = upgradeEffect.BranchCountOrbType;
		fused.BranchCountOrbSelection = upgradeEffect.BranchCountOrbSelection;
		fused.BranchCountEnemyStatus = upgradeEffect.BranchCountEnemyStatus;
		fused.BranchCountPowerId = upgradeEffect.BranchCountPowerId;
		fused.BranchCountEnemyIntent = upgradeEffect.BranchCountEnemyIntent;
		fused.BranchCountComparison = upgradeEffect.BranchCountComparison;
		fused.BranchCountConditionAmount = upgradeEffect.BranchCountConditionAmount;
		fused.BranchEffect = upgradeEffect.BranchEffect != null ? CloneEffect(upgradeEffect.BranchEffect) : null;
		fused.ConditionalBonusPowerId = upgradeEffect.ConditionalBonusPowerId;
		fused.BranchPowerId = upgradeEffect.BranchPowerId;
		fused.CountPowerId = upgradeEffect.CountPowerId;
		fused.CardMatchMode = upgradeEffect.CardMatchMode;
		fused.MatchCardId = upgradeEffect.MatchCardId;
		fused.MatchTagKind = upgradeEffect.MatchTagKind;
		fused.MatchVanillaTag = upgradeEffect.MatchVanillaTag;
		fused.MatchCustomTag = upgradeEffect.MatchCustomTag;
		fused.MatchCustomKeyword = upgradeEffect.MatchCustomKeyword;
		fused.NameFilterEnabled = upgradeEffect.NameFilterEnabled;
		fused.NameFilterText = upgradeEffect.NameFilterText;
		fused.CostFilterEnabled = upgradeEffect.CostFilterEnabled;
		fused.CostFilterMode = upgradeEffect.CostFilterMode;
		fused.CostFilterMax = upgradeEffect.CostFilterMax;
		string? upgradeKeyword = NormalizeCustomKeywordName(upgradeEffect.CustomKeywordName);
		if (upgradeKeyword != null)
		{
			fused.CustomKeywordName = upgradeKeyword;
		}
		string? upgradePowerName = string.IsNullOrWhiteSpace(upgradeEffect.CustomPowerName) ? null : upgradeEffect.CustomPowerName.Trim();
		if (upgradePowerName != null)
		{
			fused.CustomPowerName = upgradePowerName;
		}
		string? upgradePowerDescription = string.IsNullOrWhiteSpace(upgradeEffect.CustomPowerDescription) ? null : upgradeEffect.CustomPowerDescription.Trim();
		if (upgradePowerDescription != null)
		{
			fused.CustomPowerDescription = upgradePowerDescription;
		}
		fused.StatusIconMode = upgradeEffect.StatusIconMode;
		fused.StatusIconPowerId = upgradeEffect.StatusIconPowerId;
		fused.StatusCustomPackedIconPath = upgradeEffect.StatusCustomPackedIconPath;
		fused.StatusCustomBigIconPath = upgradeEffect.StatusCustomBigIconPath;
		fused.PowerId = upgradeEffect.PowerId;
		fused.PowerHost = GetEffectivePowerHost(upgradeEffect);
		fused.PowerTriggerFrom = GetEffectivePowerTriggerFrom(upgradeEffect);
		fused.PowerTargeting = upgradeEffect.PowerTargeting;
		fused.CardReferenceDisplayMode = upgradeEffect.CardReferenceDisplayMode;
		fused.AmountSourceMode = upgradeEffect.AmountSourceMode;
		fused.AmountSourceEffectId = upgradeEffect.AmountSourceEffectId;
		fused.ValueSourceMode = upgradeEffect.ValueSourceMode;
		fused.ValueSourceActor = upgradeEffect.ValueSourceActor;
		fused.ValueSourceAggregation = upgradeEffect.ValueSourceAggregation;
		fused.ValueSourceKind = upgradeEffect.ValueSourceKind;
		fused.ValueSourcePowerId = upgradeEffect.ValueSourcePowerId;
		fused.MultiplierSourceMode = upgradeEffect.MultiplierSourceMode;
		fused.MultiplierPowerId = upgradeEffect.MultiplierPowerId;
		fused.IncludeSourceCardInSelection = upgradeEffect.IncludeSourceCardInSelection;
		fused.FutureMatchingCards = upgradeEffect.FutureMatchingCards;
		fused.ResourceConsumptionMode = upgradeEffect.ResourceConsumptionMode;
		fused.ResourceConsumptionStat = upgradeEffect.ResourceConsumptionStat;
		fused.StatusToStatusMode = upgradeEffect.StatusToStatusMode;
		fused.SelfScalingOperation = upgradeEffect.SelfScalingOperation;
		fused.SelfScalingTargetType = upgradeEffect.SelfScalingTargetType;
		fused.SelfScalingField = upgradeEffect.SelfScalingField;
		fused.SelfScalingTargetEffectId = upgradeEffect.SelfScalingTargetEffectId;

		if (baseEffect.Kind == CardExtraEffectKind.TransformCards && upgradeEffect.Kind == CardExtraEffectKind.TransformCards)
		{
			fused.TransformMode = upgradeEffect.TransformMode;
			fused.SpecificCardId = upgradeEffect.SpecificCardId;
		}

	if (baseEffect.Kind == CardExtraEffectKind.ChooseOneEffectSource && upgradeEffect.Kind == CardExtraEffectKind.ChooseOneEffectSource)
	{
		fused.SpecificCardId = upgradeEffect.SpecificCardId;
		fused.SpecificCardId2 = upgradeEffect.SpecificCardId2;
		fused.SpecificCardId3 = upgradeEffect.SpecificCardId3;
		fused.ChooseOneOption1 = CloneChooseOneOption(upgradeEffect.ChooseOneOption1);
		fused.ChooseOneOption2 = CloneChooseOneOption(upgradeEffect.ChooseOneOption2);
		fused.ChooseOneOption3 = CloneChooseOneOption(upgradeEffect.ChooseOneOption3);
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
			&& a.AmountSourceMode == b.AmountSourceMode
			&& string.Equals(a.AmountSourceEffectId ?? string.Empty, b.AmountSourceEffectId ?? string.Empty, StringComparison.Ordinal)
			&& a.ValueSourceMode == b.ValueSourceMode
			&& a.ValueSourceActor == b.ValueSourceActor
			&& a.ValueSourceAggregation == b.ValueSourceAggregation
			&& a.ValueSourceKind == b.ValueSourceKind
			&& string.Equals(a.ValueSourcePowerId ?? string.Empty, b.ValueSourcePowerId ?? string.Empty, StringComparison.Ordinal)
			&& a.Trigger == b.Trigger
			&& a.PowerTriggerCountEvent == b.PowerTriggerCountEvent
			&& a.PowerTriggerEnemyStatus == b.PowerTriggerEnemyStatus
			&& string.Equals(a.PowerTriggerPowerId ?? string.Empty, b.PowerTriggerPowerId ?? string.Empty, StringComparison.Ordinal)
			&& a.PowerTriggerUsesEventAmount == b.PowerTriggerUsesEventAmount
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
			&& a.CreatedCardsCostResource == b.CreatedCardsCostResource
			&& a.CardCostsLessDuration == b.CardCostsLessDuration
			&& a.CardCostsLessTurns == b.CardCostsLessTurns
			&& a.CardCostsLessMode == b.CardCostsLessMode
			&& a.CardCostsLessModifier == b.CardCostsLessModifier
			&& a.GeneratedCardPool == b.GeneratedCardPool
			&& a.GeneratedCardType == b.GeneratedCardType
			&& string.Equals(a.GeneratedCardCustomTag ?? string.Empty, b.GeneratedCardCustomTag ?? string.Empty, StringComparison.Ordinal)
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
			&& GetEffectiveCountAggregationMode(a) == GetEffectiveCountAggregationMode(b)
			&& a.CountExcludeSourceCard == b.CountExcludeSourceCard
			&& a.CountOrbType == b.CountOrbType
			&& a.CountOrbSelection == b.CountOrbSelection
			&& a.CountEnemyStatus == b.CountEnemyStatus
			&& string.Equals(a.CountPowerId ?? string.Empty, b.CountPowerId ?? string.Empty, StringComparison.Ordinal)
			&& a.CountEnemyIntent == b.CountEnemyIntent
			&& a.MultiplierStat == b.MultiplierStat
			&& a.MultiplierSourceMode == b.MultiplierSourceMode
			&& string.Equals(a.MultiplierPowerId ?? string.Empty, b.MultiplierPowerId ?? string.Empty, StringComparison.Ordinal)
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
			&& a.IncludeSourceCardInSelection == b.IncludeSourceCardInSelection
			&& a.FutureMatchingCards == b.FutureMatchingCards
			&& a.CardGrantDuration == b.CardGrantDuration
			&& a.CardGrantTurns == b.CardGrantTurns
			&& string.Equals(a.EnchantmentId ?? string.Empty, b.EnchantmentId ?? string.Empty, StringComparison.Ordinal)
			&& a.EnchantmentDuration == b.EnchantmentDuration
			&& a.EnchantmentTurns == b.EnchantmentTurns
			&& a.MoveToPile == b.MoveToPile
			&& a.MoveToPosition == b.MoveToPosition
			&& a.UseMoveDestinationForGeneratedCards == b.UseMoveDestinationForGeneratedCards
			&& a.AdditionalMoveToPiles == b.AdditionalMoveToPiles
			&& a.OrbAction == b.OrbAction
			&& a.OrbType == b.OrbType
			&& a.OrbSelection == b.OrbSelection
			&& a.OrbFollowUp == b.OrbFollowUp
			&& a.OstyAction == b.OstyAction
			&& string.Equals(a.SpecificCardId ?? string.Empty, b.SpecificCardId ?? string.Empty, StringComparison.Ordinal)
			&& string.Equals(a.SpecificCardId2 ?? string.Empty, b.SpecificCardId2 ?? string.Empty, StringComparison.Ordinal)
			&& string.Equals(a.SpecificCardId3 ?? string.Empty, b.SpecificCardId3 ?? string.Empty, StringComparison.Ordinal)
			&& ChooseOneOptionsEqual(NormalizeChooseOneOption(a.ChooseOneOption1, a.SpecificCardId), NormalizeChooseOneOption(b.ChooseOneOption1, b.SpecificCardId))
			&& ChooseOneOptionsEqual(NormalizeChooseOneOption(a.ChooseOneOption2, a.SpecificCardId2), NormalizeChooseOneOption(b.ChooseOneOption2, b.SpecificCardId2))
			&& ChooseOneOptionsEqual(NormalizeChooseOneOption(a.ChooseOneOption3, a.SpecificCardId3), NormalizeChooseOneOption(b.ChooseOneOption3, b.SpecificCardId3))
			&& a.TransformMode == b.TransformMode
			&& a.ConditionalBonusAmount == b.ConditionalBonusAmount
			&& a.ConditionalBonusConditionType == b.ConditionalBonusConditionType
			&& a.ConditionalBonusCondition == b.ConditionalBonusCondition
			&& a.ConditionalBonusEnemyStatus == b.ConditionalBonusEnemyStatus
			&& string.Equals(a.ConditionalBonusPowerId ?? string.Empty, b.ConditionalBonusPowerId ?? string.Empty, StringComparison.Ordinal)
			&& a.ConditionalBonusEnemyIntent == b.ConditionalBonusEnemyIntent
			&& a.BranchMode == b.BranchMode
			&& a.BranchConditionType == b.BranchConditionType
			&& a.BranchCondition == b.BranchCondition
			&& a.BranchEnemyStatus == b.BranchEnemyStatus
			&& string.Equals(a.BranchPowerId ?? string.Empty, b.BranchPowerId ?? string.Empty, StringComparison.Ordinal)
			&& a.BranchEnemyIntent == b.BranchEnemyIntent
			&& a.BranchCountEvent == b.BranchCountEvent
			&& a.BranchCountWindow == b.BranchCountWindow
			&& a.BranchCountWindowInclusion == b.BranchCountWindowInclusion
			&& a.BranchBlockLostCountingMode == b.BranchBlockLostCountingMode
			&& a.BranchCountTurns == b.BranchCountTurns
			&& a.BranchCountCardPile == b.BranchCountCardPile
			&& a.BranchCountCardPool == b.BranchCountCardPool
			&& a.BranchCountCardType == b.BranchCountCardType
			&& a.BranchCountCardFilter == b.BranchCountCardFilter
			&& GetEffectiveBranchCountAggregationMode(a) == GetEffectiveBranchCountAggregationMode(b)
			&& a.BranchCountExcludeSourceCard == b.BranchCountExcludeSourceCard
			&& a.BranchCountOrbType == b.BranchCountOrbType
			&& a.BranchCountOrbSelection == b.BranchCountOrbSelection
			&& a.BranchCountEnemyStatus == b.BranchCountEnemyStatus
			&& string.Equals(a.BranchCountPowerId ?? string.Empty, b.BranchCountPowerId ?? string.Empty, StringComparison.Ordinal)
			&& a.BranchCountEnemyIntent == b.BranchCountEnemyIntent
			&& a.BranchCountComparison == b.BranchCountComparison
			&& a.BranchCountConditionAmount == b.BranchCountConditionAmount
			&& BranchEffectsMatch(a.BranchEffect, b.BranchEffect)
			&& string.Equals(a.PowerId ?? string.Empty, b.PowerId ?? string.Empty, StringComparison.Ordinal)
			&& a.GrantedKeyword == b.GrantedKeyword
			&& a.StatusIconMode == b.StatusIconMode
			&& string.Equals(a.StatusIconPowerId ?? string.Empty, b.StatusIconPowerId ?? string.Empty, StringComparison.Ordinal)
			&& string.Equals(a.StatusCustomPackedIconPath ?? string.Empty, b.StatusCustomPackedIconPath ?? string.Empty, StringComparison.Ordinal)
			&& string.Equals(a.StatusCustomBigIconPath ?? string.Empty, b.StatusCustomBigIconPath ?? string.Empty, StringComparison.Ordinal)
			&& GetEffectivePowerHost(a) == GetEffectivePowerHost(b)
			&& GetEffectivePowerTriggerFrom(a) == GetEffectivePowerTriggerFrom(b)
			&& a.PowerTargeting == b.PowerTargeting
			&& a.CardReferenceDisplayMode == b.CardReferenceDisplayMode
			&& a.CardMatchMode == b.CardMatchMode
			&& string.Equals(a.MatchCardId ?? string.Empty, b.MatchCardId ?? string.Empty, StringComparison.Ordinal)
			&& a.MatchTagKind == b.MatchTagKind
			&& a.MatchVanillaTag == b.MatchVanillaTag
			&& string.Equals(a.MatchCustomTag ?? string.Empty, b.MatchCustomTag ?? string.Empty, StringComparison.Ordinal)
			&& string.Equals(a.MatchCustomKeyword ?? string.Empty, b.MatchCustomKeyword ?? string.Empty, StringComparison.Ordinal)
			&& string.Equals(a.CustomKeywordName ?? string.Empty, b.CustomKeywordName ?? string.Empty, StringComparison.Ordinal)
			&& string.Equals(a.CustomPowerName ?? string.Empty, b.CustomPowerName ?? string.Empty, StringComparison.Ordinal)
			&& string.Equals(a.CustomPowerDescription ?? string.Empty, b.CustomPowerDescription ?? string.Empty, StringComparison.Ordinal)
			&& a.NameFilterEnabled == b.NameFilterEnabled
			&& string.Equals(a.NameFilterText ?? string.Empty, b.NameFilterText ?? string.Empty, StringComparison.Ordinal)
			&& a.CostFilterEnabled == b.CostFilterEnabled
			&& a.CostFilterMode == b.CostFilterMode
			&& a.CostFilterMax == b.CostFilterMax
			&& a.ResourceConsumptionMode == b.ResourceConsumptionMode
			&& a.ResourceConsumptionStat == b.ResourceConsumptionStat
			&& a.StatusToStatusMode == b.StatusToStatusMode
			&& a.SelfScalingOperation == b.SelfScalingOperation
			&& a.SelfScalingTargetType == b.SelfScalingTargetType
			&& a.SelfScalingField == b.SelfScalingField
			&& string.Equals(a.SelfScalingTargetEffectId ?? string.Empty, b.SelfScalingTargetEffectId ?? string.Empty, StringComparison.Ordinal);
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
			AmountSourceMode = source.AmountSourceMode,
			AmountSourceEffectId = source.AmountSourceEffectId,
			ValueSourceMode = source.ValueSourceMode,
			ValueSourceActor = source.ValueSourceActor,
			ValueSourceAggregation = source.ValueSourceAggregation,
			ValueSourceKind = source.ValueSourceKind,
			ValueSourcePowerId = source.ValueSourcePowerId,
			ConditionalBonusAmount = source.ConditionalBonusAmount,
			ConditionalBonusConditionType = source.ConditionalBonusConditionType,
			ConditionalBonusCondition = source.ConditionalBonusCondition,
			ConditionalBonusEnemyStatus = source.ConditionalBonusEnemyStatus,
			ConditionalBonusPowerId = source.ConditionalBonusPowerId,
			ConditionalBonusEnemyIntent = source.ConditionalBonusEnemyIntent,
			BranchMode = source.BranchMode,
			BranchConditionType = source.BranchConditionType,
			BranchCondition = source.BranchCondition,
			BranchEnemyStatus = source.BranchEnemyStatus,
			BranchPowerId = source.BranchPowerId,
			BranchEnemyIntent = source.BranchEnemyIntent,
			BranchCountEvent = source.BranchCountEvent,
			BranchCountWindow = source.BranchCountWindow,
			BranchCountWindowInclusion = source.BranchCountWindowInclusion,
			BranchBlockLostCountingMode = source.BranchBlockLostCountingMode,
			BranchCountTurns = source.BranchCountTurns,
			BranchCountCardPile = source.BranchCountCardPile,
			BranchCountCardPool = source.BranchCountCardPool,
			BranchCountCardType = source.BranchCountCardType,
			BranchCountCardFilter = source.BranchCountCardFilter,
			BranchCountAggregationMode = source.BranchCountAggregationMode,
			BranchCountUsesCardEffectAmount = source.BranchCountUsesCardEffectAmount,
			BranchCountExcludeSourceCard = source.BranchCountExcludeSourceCard,
			BranchCountOrbType = source.BranchCountOrbType,
			BranchCountOrbSelection = source.BranchCountOrbSelection,
			BranchCountEnemyStatus = source.BranchCountEnemyStatus,
			BranchCountPowerId = source.BranchCountPowerId,
			BranchCountEnemyIntent = source.BranchCountEnemyIntent,
			BranchCountComparison = source.BranchCountComparison,
			BranchCountConditionAmount = source.BranchCountConditionAmount,
			BranchEffect = source.BranchEffect != null ? CloneEffect(source.BranchEffect) : null,
			TransformMode = source.TransformMode,
			DisableOnUpgrade = source.DisableOnUpgrade,
			RepeatIsX = source.RepeatIsX,
			RepeatCount = source.RepeatCount,
			Trigger = source.Trigger,
			PowerTriggerCountEvent = source.PowerTriggerCountEvent,
			PowerTriggerEnemyStatus = source.PowerTriggerEnemyStatus,
			PowerTriggerPowerId = source.PowerTriggerPowerId,
			PowerTriggerUsesEventAmount = source.PowerTriggerUsesEventAmount,
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
			CreatedCardsCostResource = source.CreatedCardsCostResource,
			CardCostsLessDuration = source.CardCostsLessDuration,
			CardCostsLessTurns = source.CardCostsLessTurns,
			CardCostsLessMode = source.CardCostsLessMode,
			CardCostsLessModifier = source.CardCostsLessModifier,
			GeneratedCardPool = source.GeneratedCardPool,
			GeneratedCardType = source.GeneratedCardType,
			GeneratedCardCustomTag = source.GeneratedCardCustomTag,
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
			CountAggregationMode = source.CountAggregationMode,
			CountUsesCardEffectAmount = source.CountUsesCardEffectAmount,
			CountExcludeSourceCard = source.CountExcludeSourceCard,
			CountOrbType = source.CountOrbType,
			CountOrbSelection = source.CountOrbSelection,
			CountEnemyStatus = source.CountEnemyStatus,
			CountPowerId = source.CountPowerId,
			CountEnemyIntent = source.CountEnemyIntent,
			MultiplierStat = source.MultiplierStat,
			MultiplierSourceMode = source.MultiplierSourceMode,
			MultiplierPowerId = source.MultiplierPowerId,
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
			UseMoveDestinationForGeneratedCards = source.UseMoveDestinationForGeneratedCards,
			AdditionalMoveToPiles = source.AdditionalMoveToPiles,
			OrbAction = source.OrbAction,
			OrbType = source.OrbType,
			OrbSelection = source.OrbSelection,
			OrbFollowUp = source.OrbFollowUp,
			OrbScope = source.OrbScope,
			OstyAction = source.OstyAction,
			DrawnFromPile = source.DrawnFromPile,
			SpecificCardId = source.SpecificCardId,
			CardReferenceDisplayMode = source.CardReferenceDisplayMode,
			SpecificCardId2 = source.SpecificCardId2,
			SpecificCardId3 = source.SpecificCardId3,
			ChooseOneOption1 = CloneChooseOneOption(source.ChooseOneOption1),
			ChooseOneOption2 = CloneChooseOneOption(source.ChooseOneOption2),
			ChooseOneOption3 = CloneChooseOneOption(source.ChooseOneOption3),
			PowerId = source.PowerId,
			StatusIconMode = source.StatusIconMode,
			StatusIconPowerId = source.StatusIconPowerId,
			StatusCustomPackedIconPath = source.StatusCustomPackedIconPath,
			StatusCustomBigIconPath = source.StatusCustomBigIconPath,
			CustomPowerName = source.CustomPowerName,
			CustomPowerDescription = source.CustomPowerDescription,
			PowerHost = GetEffectivePowerHost(source),
			PowerTriggerFrom = GetEffectivePowerTriggerFrom(source),
			PowerTargeting = source.PowerTargeting,
			GrantedKeyword = source.GrantedKeyword,
			CardMatchMode = source.CardMatchMode,
			MatchCardId = source.MatchCardId,
			MatchTagKind = source.MatchTagKind,
			MatchVanillaTag = source.MatchVanillaTag,
			MatchCustomTag = source.MatchCustomTag,
			MatchCustomKeyword = source.MatchCustomKeyword,
			CustomKeywordName = source.CustomKeywordName,
			NameFilterEnabled = source.NameFilterEnabled,
			NameFilterText = source.NameFilterText,
			CostFilterEnabled = source.CostFilterEnabled,
			CostFilterMode = source.CostFilterMode,
			CostFilterMax = source.CostFilterMax,
			EffectId = source.EffectId,
			IncludeSourceCardInSelection = source.IncludeSourceCardInSelection,
			FutureMatchingCards = source.FutureMatchingCards,
			ResourceConsumptionMode = source.ResourceConsumptionMode,
			ResourceConsumptionStat = source.ResourceConsumptionStat,
			StatusToStatusMode = source.StatusToStatusMode,
			SelfScalingOperation = source.SelfScalingOperation,
			SelfScalingTargetType = source.SelfScalingTargetType,
			SelfScalingField = source.SelfScalingField,
			SelfScalingTargetEffectId = source.SelfScalingTargetEffectId
		};
	}

	private static bool BranchEffectsMatch(CardExtraEffect? a, CardExtraEffect? b)
	{
		if (a == null || b == null)
		{
			return a == b;
		}

		return a.Amount == b.Amount
			&& a.AmountXPlus == b.AmountXPlus
			&& EffectsMatchExceptAmount(a, b);
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
