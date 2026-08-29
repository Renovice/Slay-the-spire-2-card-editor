using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using PowerCmd = SlayTheSpire2Mod.CardEditor.CardEditorPowerCmdCompat;

namespace SlayTheSpire2Mod.CardEditor;

public sealed class CardEditorBuiltTinkerCard : CardModel
{
	private const int AttackDamage = 12;
	private const int SkillBlock = 8;
	private const int SappingWeak = 2;
	private const int SappingVulnerable = 2;
	private const int ViolenceHits = 3;
	private const int ChokingDamage = 6;
	private const int EnergizedEnergy = 2;
	private const int WisdomCards = 3;
	private const int ExpertiseStrength = 2;
	private const int ExpertiseDexterity = 2;
	private const int CuriousReduction = 1;
	private const string BuiltTinkerTitleKey = "CARD_EDITOR_BUILT_TINKER_CARD.title";
	private const string BuiltTinkerDescriptionKey = "CARD_EDITOR_BUILT_TINKER_CARD.description";
	private const string BuiltTinkerTitleFallback = "Custom Experiment";
	private const string BuiltTinkerDescriptionFallback = "Build a custom experiment.";

	private CardType _tinkerTimeType = CardType.Attack;
	private TinkerTime.RiderEffect _tinkerTimeRider;
	private ModelId _tinkerTimeBaseCardId = ModelId.none;
	private ModelId _tinkerTimeRiderCardId = ModelId.none;

	public override string PortraitPath => TryBuildSourceCard(TinkerTimeBaseCardId, out CardModel? source)
		? source.PortraitPath
		: GetPortraitPath(TinkerTimeType);

	public override string BetaPortraitPath => CardModel.MissingPortraitPath;

	public override CardType Type => TinkerTimeType;

	public override CardRarity Rarity => CardRarity.Event;

	public override TargetType TargetType
	{
		get
		{
			if (TinkerTimeType == CardType.Attack || VanillaRiderNeedsEnemyTarget(TinkerTimeRider))
			{
				return TargetType.AnyEnemy;
			}

			return TryBuildSourceCard(TinkerTimeBaseCardId, out CardModel? source)
				? source.TargetType
				: TargetType.Self;
		}
	}

	public override bool GainsBlock => TinkerTimeType == CardType.Skill
		|| (TryBuildSourceCard(TinkerTimeBaseCardId, out CardModel? source) && source.GainsBlock)
		|| (TryBuildSourceCard(TinkerTimeRiderCardId, out CardModel? rider) && rider.GainsBlock);

	public override IEnumerable<CardKeyword> CanonicalKeywords
	{
		get
		{
			HashSet<CardKeyword> keywords = new();
			AddSourceKeywords(TinkerTimeBaseCardId, keywords);
			AddSourceKeywords(TinkerTimeRiderCardId, keywords);
			return keywords;
		}
	}

	protected override int CanonicalEnergyCost => TryBuildSourceCard(TinkerTimeBaseCardId, out CardModel? source)
		? source.EnergyCost.GetWithModifiers(CostModifiers.None)
		: 1;

	protected override bool HasEnergyCostX => TryBuildSourceCard(TinkerTimeBaseCardId, out CardModel? source)
		&& source.EnergyCost.CostsX;

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
	{
		new DamageVar(AttackDamage, ValueProp.Move),
		new BlockVar(SkillBlock, ValueProp.Move),
		new PowerVar<WeakPower>("SappingWeak", SappingWeak),
		new PowerVar<VulnerablePower>("SappingVulnerable", SappingVulnerable),
		new DynamicVar("ViolenceHits", ViolenceHits),
		new PowerVar<StranglePower>("ChokingDamage", ChokingDamage),
		new EnergyVar("EnergizedEnergy", EnergizedEnergy),
		new CardsVar("WisdomCards", WisdomCards),
		new PowerVar<StrengthPower>("ExpertiseStrength", ExpertiseStrength),
		new PowerVar<DexterityPower>("ExpertiseDexterity", ExpertiseDexterity),
		new DynamicVar("CuriousReduction", CuriousReduction)
	};

	[SavedProperty(SerializationCondition.AlwaysSave, -1)]
	public CardType TinkerTimeType
	{
		get => _tinkerTimeType;
		set
		{
			AssertMutable();
			_tinkerTimeType = value;
		}
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public TinkerTime.RiderEffect TinkerTimeRider
	{
		get => _tinkerTimeRider;
		set
		{
			AssertMutable();
			_tinkerTimeRider = value;
		}
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public ModelId TinkerTimeBaseCardId
	{
		get => _tinkerTimeBaseCardId;
		set
		{
			AssertMutable();
			_tinkerTimeBaseCardId = value ?? ModelId.none;
		}
	}

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public ModelId TinkerTimeRiderCardId
	{
		get => _tinkerTimeRiderCardId;
		set
		{
			AssertMutable();
			_tinkerTimeRiderCardId = value ?? ModelId.none;
		}
	}

	public CardEditorBuiltTinkerCard()
		: base(1, CardType.Attack, CardRarity.Event, TargetType.AnyEnemy, shouldShowInCardLibrary: false)
	{
		EnsureLocalization();
	}

	internal void ConfigureForTinkerTime(
		CardType type,
		TinkerTime.RiderEffect vanillaRider,
		ModelId? baseCardId,
		ModelId? riderCardId)
	{
		TinkerTimeType = type;
		TinkerTimeRider = vanillaRider;
		TinkerTimeBaseCardId = baseCardId ?? ModelId.none;
		TinkerTimeRiderCardId = riderCardId ?? ModelId.none;
		ApplyRecipeOverride();
	}

	internal string ResolveDisplayTitle()
	{
		string title = string.Empty;
		if (!CardEditorOverrides.SuppressAllOverrides
			&& CardEditorOverrides.TryGetEffectiveOverride(this, out CardOverride overrideData)
			&& !string.IsNullOrWhiteSpace(overrideData.TitleOverride))
		{
			title = overrideData.TitleOverride.Trim();
		}

		if (string.IsNullOrWhiteSpace(title))
		{
			try
			{
				title = IsMutable ? BuildTitle() : GetCardLoc(BuiltTinkerTitleKey, BuiltTinkerTitleFallback);
			}
			catch
			{
				title = GetCardLoc(BuiltTinkerTitleKey, BuiltTinkerTitleFallback);
			}
		}

		if (!IsUpgraded)
		{
			return title;
		}
		if (MaxUpgradeLevel > 1)
		{
			return $"{title}+{CurrentUpgradeLevel}";
		}
		return title + "+";
	}

	internal string ResolveDisplayDescription(Creature? target, bool isUpgradePreview)
	{
		string fallback;
		try
		{
			fallback = IsMutable ? BuildDescription() : GetCardLoc(BuiltTinkerDescriptionKey, BuiltTinkerDescriptionFallback);
		}
		catch
		{
			fallback = GetCardLoc(BuiltTinkerDescriptionKey, BuiltTinkerDescriptionFallback);
		}

		if (string.IsNullOrWhiteSpace(fallback))
		{
			fallback = GetCardLoc(BuiltTinkerDescriptionKey, BuiltTinkerDescriptionFallback);
		}
		return CardEditorVanillaKeywordSupport.FormatDescription(this, fallback, target, isUpgradePreview);
	}

	public override void AfterCreated()
	{
		base.AfterCreated();
		ApplyRecipeOverride();
	}

	protected override void AfterDeserialized()
	{
		base.AfterDeserialized();
		ApplyRecipeOverride();
	}

	protected override void AfterCloned()
	{
		base.AfterCloned();
		ApplyRecipeOverride();
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (HasCustomBase)
		{
			int repeats = TinkerTimeRider == TinkerTime.RiderEffect.Violence
				? Math.Max(1, DynamicVars["ViolenceHits"].IntValue)
				: 1;

			for (int i = 0; i < repeats; i++)
			{
				await RunSourceCard(choiceContext, cardPlay, TinkerTimeBaseCardId, $"tinker-base-{i}");
			}
		}
		else
		{
			await ExecuteVanillaBase(choiceContext, cardPlay);
		}

		if (HasCustomRider)
		{
			await RunSourceCard(choiceContext, cardPlay, TinkerTimeRiderCardId, "tinker-rider");
			return;
		}

		await ExecuteVanillaRider(choiceContext, cardPlay);
	}

	protected override void OnUpgrade()
	{
		AddKeyword(CardKeyword.Innate);
	}

	private bool HasCustomBase => TinkerTimeBaseCardId != ModelId.none;

	private bool HasCustomRider => TinkerTimeRiderCardId != ModelId.none;

	private async Task RunSourceCard(PlayerChoiceContext choiceContext, CardPlay cardPlay, ModelId sourceId, string key)
	{
		if (sourceId == null || sourceId == ModelId.none)
		{
			return;
		}

		using IDisposable _ = CardEditorCardPlayContext.PushScoped(cardPlay);
		await CardEditorCreatedCardEffectSourceSupport.RunSingleEffectSourceOnPlay(
			this,
			choiceContext,
			cardPlay,
			sourceId,
			$"{key}:{sourceId}");
	}

	private async Task ExecuteVanillaBase(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		switch (TinkerTimeType)
		{
		case CardType.Attack:
			Creature target = RequireTarget(cardPlay);
			int hits = TinkerTimeRider == TinkerTime.RiderEffect.Violence
				? Math.Max(1, DynamicVars["ViolenceHits"].IntValue)
				: 1;
			for (int i = 0; i < hits; i++)
			{
				await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(target)
					.WithHitFx("vfx/vfx_attack_slash")
					.Execute(choiceContext);
			}
			break;
		case CardType.Skill:
			await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
			break;
		case CardType.Power:
			await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
			break;
		}
	}

	private async Task ExecuteVanillaRider(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		switch (TinkerTimeRider)
		{
		case TinkerTime.RiderEffect.None:
		case TinkerTime.RiderEffect.Violence:
			return;
		case TinkerTime.RiderEffect.Sapping:
			await PowerCmd.Apply<WeakPower>(RequireTarget(cardPlay), DynamicVars["SappingWeak"].BaseValue, Owner.Creature, this);
			await PowerCmd.Apply<VulnerablePower>(RequireTarget(cardPlay), DynamicVars["SappingVulnerable"].BaseValue, Owner.Creature, this);
			return;
		case TinkerTime.RiderEffect.Choking:
			await PowerCmd.Apply<StranglePower>(RequireTarget(cardPlay), DynamicVars["ChokingDamage"].BaseValue, Owner.Creature, this);
			return;
		case TinkerTime.RiderEffect.Energized:
			await PlayerCmd.GainEnergy(DynamicVars["EnergizedEnergy"].IntValue, Owner);
			return;
		case TinkerTime.RiderEffect.Wisdom:
			await CardPileCmd.Draw(choiceContext, DynamicVars["WisdomCards"].IntValue, Owner);
			return;
		case TinkerTime.RiderEffect.Chaos:
			CardModel card = CardFactory.GetDistinctForCombat(
				Owner,
				Owner.Character.CardPool.GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint),
				1,
				Owner.RunState.Rng.CombatCardGeneration).First();
			card.SetToFreeThisTurn();
			await CardPileCmd.Add(card, PileType.Hand);
			return;
		case TinkerTime.RiderEffect.Expertise:
			await PowerCmd.Apply<StrengthPower>(Owner.Creature, DynamicVars["ExpertiseStrength"].BaseValue, Owner.Creature, this);
			await PowerCmd.Apply<DexterityPower>(Owner.Creature, DynamicVars["ExpertiseDexterity"].BaseValue, Owner.Creature, this);
			return;
		case TinkerTime.RiderEffect.Curious:
			await PowerCmd.Apply<CuriousPower>(Owner.Creature, DynamicVars["CuriousReduction"].BaseValue, Owner.Creature, this);
			return;
		case TinkerTime.RiderEffect.Improvement:
			await PowerCmd.Apply<ImprovementPower>(Owner.Creature, 1m, Owner.Creature, this);
			return;
		default:
			throw new ArgumentOutOfRangeException(nameof(TinkerTimeRider), TinkerTimeRider, null);
		}
	}

	private Creature RequireTarget(CardPlay cardPlay)
	{
		if (cardPlay.Target == null)
		{
			ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));
		}
		return cardPlay.Target;
	}

	private void ApplyRecipeOverride()
	{
		EnsureLocalization();
		if (!IsMutable)
		{
			return;
		}

		CardOverride overrideData = new()
		{
			TitleOverride = BuildTitle(),
			ModifiedBaseTextEnabled = true,
			ModifiedBaseText = BuildDescription(),
			Rarity = CardRarity.Event,
			CardType = TinkerTimeType,
			TargetType = TargetType,
			EnergyCost = HasEnergyCostX ? null : CanonicalEnergyCost,
			EnergyCostX = HasEnergyCostX ? true : null
		};

		CardEditorOverrides.ApplyOverrideToCard(this, overrideData);
	}

	private string BuildTitle()
	{
		if (TryBuildSourceCard(TinkerTimeBaseCardId, out CardModel? source))
		{
			return source.Title;
		}

		return CardEditorLoc.T("tinkerTime.madScience.title", "Mad Science");
	}

	private string BuildDescription()
	{
		List<string> lines = new();

		string baseDescription = HasCustomBase
			? DescribeSource(TinkerTimeBaseCardId, "tinker-desc-base")
			: DescribeVanillaBase();
		if (!string.IsNullOrWhiteSpace(baseDescription))
		{
			lines.Add(baseDescription);
		}

		if (HasCustomBase && TinkerTimeRider == TinkerTime.RiderEffect.Violence)
		{
			string count = FormatDynamicVar("ViolenceHits");
			lines.Add(CardEditorLoc.F(
				"tinkerTime.violenceRepeat",
				$"Repeat this base effect {count} times.",
				("Count", count)));
		}

		string riderDescription = HasCustomRider
			? DescribeSource(TinkerTimeRiderCardId, "tinker-desc-rider")
			: DescribeVanillaRider();
		if (!string.IsNullOrWhiteSpace(riderDescription))
		{
			lines.Add(riderDescription);
		}

		return string.Join("\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()));
	}

	private string DescribeSource(ModelId sourceId, string key)
	{
		try
		{
			string? description = CardEditorCreatedCardEffectSourceSupport.GetSingleEffectSourceDescription(
				this,
				CurrentTarget,
				isUpgradePreview: false,
				sourceId,
				key);
			if (!string.IsNullOrWhiteSpace(description))
			{
				return description.Trim();
			}
		}
		catch
		{
		}

		return TryBuildSourceCard(sourceId, out CardModel? source)
			? source.GetDescriptionForPile(PileType.None, CurrentTarget).Trim()
			: string.Empty;
	}

	private string DescribeVanillaBase()
	{
		return TinkerTimeType switch
		{
			CardType.Attack => $"Deal {DynamicVars.Damage.ToHighlightedString(inverse: false)} damage.",
			CardType.Skill => $"Gain {DynamicVars.Block.ToHighlightedString(inverse: false)} Block.",
			CardType.Power => string.Empty,
			_ => string.Empty
		};
	}

	private string DescribeVanillaRider()
	{
		return TinkerTimeRider switch
		{
			TinkerTime.RiderEffect.None => string.Empty,
			TinkerTime.RiderEffect.Sapping => $"Apply {FormatDynamicVar("SappingWeak")} Weak and {FormatDynamicVar("SappingVulnerable")} Vulnerable.",
			TinkerTime.RiderEffect.Violence when !HasCustomBase => $"Deal damage {FormatDynamicVar("ViolenceHits")} times.",
			TinkerTime.RiderEffect.Violence => string.Empty,
			TinkerTime.RiderEffect.Choking => $"Apply {FormatDynamicVar("ChokingDamage")} Strangle.",
			TinkerTime.RiderEffect.Energized => $"Gain {FormatDynamicVar("EnergizedEnergy")} Energy.",
			TinkerTime.RiderEffect.Wisdom => $"Draw {FormatDynamicVar("WisdomCards")} cards.",
			TinkerTime.RiderEffect.Chaos => "Add a random card to your hand. It costs 0 this turn.",
			TinkerTime.RiderEffect.Expertise => $"Gain {FormatDynamicVar("ExpertiseStrength")} Strength and {FormatDynamicVar("ExpertiseDexterity")} Dexterity.",
			TinkerTime.RiderEffect.Curious => $"Apply {FormatDynamicVar("CuriousReduction")} Curious.",
			TinkerTime.RiderEffect.Improvement => "Apply 1 Improvement.",
			_ => string.Empty
		};
	}

	private string FormatDynamicVar(string name)
	{
		return DynamicVars[name].ToHighlightedString(inverse: false);
	}

	private static bool VanillaRiderNeedsEnemyTarget(TinkerTime.RiderEffect rider)
	{
		return rider == TinkerTime.RiderEffect.Sapping
			|| rider == TinkerTime.RiderEffect.Choking;
	}

	private void AddSourceKeywords(ModelId sourceId, HashSet<CardKeyword> keywords)
	{
		if (!TryBuildSourceCard(sourceId, out CardModel? source))
		{
			return;
		}

		foreach (CardKeyword keyword in source.Keywords)
		{
			if (keyword != CardKeyword.None)
			{
				keywords.Add(keyword);
			}
		}
	}

	private bool TryBuildSourceCard(ModelId sourceId, out CardModel source)
	{
		source = null!;
		if (sourceId == null || sourceId == ModelId.none)
		{
			return false;
		}

		CardModel? canonical = ModelDb.GetByIdOrNull<CardModel>(sourceId);
		if (canonical == null)
		{
			return false;
		}

		try
		{
			source = canonical.ToMutable();
			CardEditorOverrides.ApplyTo(source);
			try
			{
				if (IsMutable && Owner != null && source.Owner == null)
				{
					source.Owner = Owner;
				}
			}
			catch
			{
			}
			return true;
		}
		catch
		{
			source = null!;
			return false;
		}
	}

	private static string GetPortraitPath(CardType cardType)
	{
		string filename = cardType switch
		{
			CardType.Attack => "mad_science_attack",
			CardType.Skill => "mad_science_skill",
			CardType.Power => "mad_science_power",
			_ => "mad_science_attack"
		};
		return ImageHelper.GetImagePath("atlases/card_atlas.sprites/event/" + filename + ".tres");
	}

	internal static void EnsureLocalization()
	{
		try
		{
			if (LocManager.Instance == null)
			{
				return;
			}

			Dictionary<string, string> missing = new();
			if (!LocString.Exists("cards", BuiltTinkerTitleKey))
			{
				missing[BuiltTinkerTitleKey] = BuiltTinkerTitleFallback;
			}
			if (!LocString.Exists("cards", BuiltTinkerDescriptionKey))
			{
				missing[BuiltTinkerDescriptionKey] = BuiltTinkerDescriptionFallback;
			}
			if (missing.Count == 0)
			{
				return;
			}

			LocManager.Instance.GetTable("cards").MergeWith(missing);
		}
		catch
		{
		}
	}

	private static string GetCardLoc(string key, string fallback)
	{
		try
		{
			if (LocManager.Instance == null || !LocString.Exists("cards", key))
			{
				return fallback;
			}

			string value = new LocString("cards", key).GetFormattedText();
			return string.IsNullOrWhiteSpace(value) ? fallback : value;
		}
		catch
		{
			return fallback;
		}
	}
}

[HarmonyPatch(typeof(CardModel), "get_Title")]
internal static class CardModel_get_Title_CardEditorBuiltTinkerCard_Patch
{
	[HarmonyPriority(Priority.First)]
	public static bool Prefix(CardModel __instance, ref string __result)
	{
		if (__instance is not CardEditorBuiltTinkerCard builtTinkerCard)
		{
			return true;
		}

		__result = builtTinkerCard.ResolveDisplayTitle();
		return false;
	}
}

[HarmonyPatch(typeof(TinkerTime), "ChooseCardType")]
internal static class TinkerTime_ChooseCardType_CardEditorPatch
{
	public static bool Prefix(TinkerTime __instance, ref Task __result)
	{
		try
		{
			if (!CardEditorTinkerTimeIntegration.HasCustomTypeChoices(__instance))
			{
				return true;
			}

			__result = CardEditorTinkerTimeIntegration.ShowTypeChoices(__instance);
			return false;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][TinkerTime] Type choice integration failed; falling back to vanilla: {ex}");
			return true;
		}
	}
}

[HarmonyPatch(typeof(TinkerTime), "ChooseRiderEffect")]
internal static class TinkerTime_ChooseRiderEffect_CardEditorPatch
{
	public static bool Prefix(TinkerTime __instance, ref Task __result)
	{
		try
		{
			if (!CardEditorTinkerTimeIntegration.ShouldHandleRiderChoices(__instance))
			{
				return true;
			}

			__result = CardEditorTinkerTimeIntegration.ShowRiderChoices(__instance);
			return false;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][TinkerTime] Rider choice integration failed; falling back to vanilla: {ex}");
			return true;
		}
	}
}

internal static class CardEditorTinkerTimeIntegration
{
	private static readonly ConditionalWeakTable<TinkerTime, TinkerBuildState> _state = new();
	private static readonly FieldInfo? _chosenCardTypeField = AccessTools.Field(typeof(TinkerTime), "_chosenCardType");
	private static readonly MethodInfo? _setEventStateMethod = AccessTools.Method(
		typeof(EventModel),
		"SetEventState",
		new[] { typeof(LocString), typeof(IEnumerable<EventOption>) });
	private static readonly MethodInfo? _setEventFinishedMethod = AccessTools.Method(
		typeof(EventModel),
		"SetEventFinished",
		new[] { typeof(LocString) });

	internal static bool HasCustomTypeChoices(TinkerTime tinker)
	{
		return GetCustomTypeChoices(tinker).Count > 0;
	}

	internal static bool ShouldHandleRiderChoices(TinkerTime tinker)
	{
		if (GetStateOrNull(tinker)?.HasCustomBase == true)
		{
			return true;
		}

		return GetCustomRiderChoices(tinker, GetChosenCardType(tinker)).Count > 0;
	}

	internal static Task ShowTypeChoices(TinkerTime tinker)
	{
		ClearState(tinker);
		List<TinkerChoice> vanilla = new()
		{
			TinkerChoice.VanillaType(CardType.Attack),
			TinkerChoice.VanillaType(CardType.Skill),
			TinkerChoice.VanillaType(CardType.Power)
		};
		List<TinkerChoice> custom = GetCustomTypeChoices(tinker);
		List<TinkerChoice> selected = ChooseDisplayed(vanilla, custom, tinker.Rng, 2);
		IEnumerable<EventOption> options = selected.Select(choice => CreateTypeOption(tinker, choice)).ToList();
		SetEventState(tinker, EventLoc("TINKER_TIME.pages.CHOOSE_CARD_TYPE.description"), options);
		return Task.CompletedTask;
	}

	internal static Task ShowRiderChoices(TinkerTime tinker)
	{
		CardType type = GetChosenCardType(tinker);
		List<TinkerChoice> vanilla = GetVanillaRiders(type)
			.Select(rider => TinkerChoice.VanillaRider(type, rider))
			.ToList();
		List<TinkerChoice> custom = GetCustomRiderChoices(tinker, type);
		List<TinkerChoice> selected = ChooseDisplayed(vanilla, custom, tinker.Rng, 2);
		IEnumerable<EventOption> options = selected.Select(choice => CreateRiderOption(tinker, choice)).ToList();
		SetEventState(tinker, EventLoc("TINKER_TIME.pages.CHOOSE_RIDER.description"), options);
		return Task.CompletedTask;
	}

	private static EventOption CreateTypeOption(TinkerTime tinker, TinkerChoice choice)
	{
		if (choice.IsVanilla)
		{
			return new EventOption(
				tinker,
				() => ChooseVanillaType(tinker, choice.Type),
				GetVanillaTypeLocKey(choice.Type),
				CreateVanillaTypeHoverTip(tinker, choice.Type));
		}

		CardModel preview = CreateSourcePreview(tinker.Owner!, choice.Template!); // Owner expected non-null for a running TinkerTime event
		return new EventOption(
			tinker,
			() => ChooseCustomType(tinker, choice),
			RuntimeLoc("CARD_EDITOR.tinkerTime.customType.title", "Build {Card}", ("Card", preview.Title)),
			RuntimeLoc("CARD_EDITOR.tinkerTime.customType.description", "Use {Card} as the base experiment.", ("Card", preview.Title)),
			choice.Template!.Id.Entry,
			new IHoverTip[] { new CardHoverTip(preview) });
	}

	private static EventOption CreateRiderOption(TinkerTime tinker, TinkerChoice choice)
	{
		if (choice.IsVanilla)
		{
			return new EventOption(
				tinker,
				() => ChooseVanillaRider(tinker, choice.Rider),
				GetVanillaRiderLocKey(choice.Rider),
				CreateRiderHoverTip(tinker, choice));
		}

		CardModel preview = CreateBuiltPreview(tinker, choice);
		return new EventOption(
			tinker,
			() => ChooseCustomRider(tinker, choice),
			RuntimeLoc("CARD_EDITOR.tinkerTime.customRider.title", "Add {Card}", ("Card", choice.Template!.Title)),
			RuntimeLoc("CARD_EDITOR.tinkerTime.customRider.description", "Use {Card} as the rider experiment.", ("Card", choice.Template!.Title)),
			choice.Template!.Id.Entry,
			new IHoverTip[] { new CardHoverTip(preview) });
	}

	private static Task ChooseVanillaType(TinkerTime tinker, CardType type)
	{
		ClearState(tinker);
		SetChosenCardType(tinker, type);
		return ShowRiderChoices(tinker);
	}

	private static Task ChooseCustomType(TinkerTime tinker, TinkerChoice choice)
	{
		SetChosenCardType(tinker, choice.Type);
		TinkerBuildState state = GetState(tinker);
		state.BaseCardId = choice.Template!.Id;
		state.BaseType = choice.Type;
		return ShowRiderChoices(tinker);
	}

	private static Task ChooseVanillaRider(TinkerTime tinker, TinkerTime.RiderEffect rider)
	{
		TinkerBuildState? state = GetStateOrNull(tinker);
		return FinishChoice(
			tinker,
			rider,
			state?.BaseCardId ?? ModelId.none,
			ModelId.none);
	}

	private static Task ChooseCustomRider(TinkerTime tinker, TinkerChoice choice)
	{
		TinkerBuildState? state = GetStateOrNull(tinker);
		return FinishChoice(
			tinker,
			TinkerTime.RiderEffect.None,
			state?.BaseCardId ?? ModelId.none,
			choice.Template!.Id);
	}

	private static async Task FinishChoice(TinkerTime tinker, TinkerTime.RiderEffect vanillaRider, ModelId baseCardId, ModelId riderCardId)
	{
		CardModel card;
		if ((baseCardId == null || baseCardId == ModelId.none)
			&& (riderCardId == null || riderCardId == ModelId.none))
		{
			MadScience madScience = tinker.Owner!.RunState.CreateCard<MadScience>(tinker.Owner!); // Owner expected non-null for a running TinkerTime event
			madScience.TinkerTimeType = GetChosenCardType(tinker);
			madScience.TinkerTimeRider = vanillaRider;
			card = madScience;
		}
		else
		{
			CardEditorBuiltTinkerCard built = (CardEditorBuiltTinkerCard)tinker.Owner!.RunState.CreateCard(
				ModelDb.Card<CardEditorBuiltTinkerCard>(),
				tinker.Owner!);
			built.ConfigureForTinkerTime(GetChosenCardType(tinker), vanillaRider, baseCardId, riderCardId);
			card = built;
		}

		CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck), 3f);
		ClearState(tinker);
		SetEventFinished(tinker, EventLoc("TINKER_TIME.pages.DONE.description"));
	}

	private static CardHoverTip CreateVanillaTypeHoverTip(TinkerTime tinker, CardType type)
	{
		MadScience madScience = tinker.Owner!.RunState.CreateCard<MadScience>(tinker.Owner!); // Owner expected non-null for a running TinkerTime event
		madScience.TinkerTimeType = type;
		madScience.TinkerTimeRider = TinkerTime.RiderEffect.None;
		return new CardHoverTip(madScience);
	}

	private static IHoverTip[] CreateRiderHoverTip(TinkerTime tinker, TinkerChoice choice)
	{
		TinkerBuildState? state = GetStateOrNull(tinker);
		if (state?.HasCustomBase == true)
		{
			CardEditorBuiltTinkerCard built = (CardEditorBuiltTinkerCard)tinker.Owner!.RunState.CreateCard(
				ModelDb.Card<CardEditorBuiltTinkerCard>(),
				tinker.Owner!); // Owner expected non-null for a running TinkerTime event
			built.ConfigureForTinkerTime(choice.Type, choice.Rider, state.BaseCardId, ModelId.none);
			return new IHoverTip[] { new CardHoverTip(built) };
		}

		MadScience madScience = tinker.Owner!.RunState.CreateCard<MadScience>(tinker.Owner!);
		madScience.TinkerTimeType = choice.Type;
		madScience.TinkerTimeRider = choice.Rider;
		return new IHoverTip[] { new CardHoverTip(madScience) };
	}

	private static CardModel CreateBuiltPreview(TinkerTime tinker, TinkerChoice riderChoice)
	{
		TinkerBuildState? state = GetStateOrNull(tinker);
		CardEditorBuiltTinkerCard built = (CardEditorBuiltTinkerCard)tinker.Owner!.RunState.CreateCard(
			ModelDb.Card<CardEditorBuiltTinkerCard>(),
			tinker.Owner!); // Owner expected non-null for a running TinkerTime event
		built.ConfigureForTinkerTime(
			GetChosenCardType(tinker),
			TinkerTime.RiderEffect.None,
			state?.BaseCardId ?? ModelId.none,
			riderChoice.Template!.Id);
		return built;
	}

	private static CardModel CreateSourcePreview(Player owner, CardModel template)
	{
		CardModel preview = owner.RunState.CreateCard(template, owner);
		CardEditorOverrides.ApplyTo(preview);
		return preview;
	}

	private static List<TinkerChoice> GetCustomTypeChoices(TinkerTime tinker)
	{
		List<TinkerChoice> choices = new();
		AddCustomChoices(choices, tinker, CardEditorRewardPoolRegistry.TinkerTimeTypeAttackPoolId, CardType.Attack, isRider: false);
		AddCustomChoices(choices, tinker, CardEditorRewardPoolRegistry.TinkerTimeTypeSkillPoolId, CardType.Skill, isRider: false);
		AddCustomChoices(choices, tinker, CardEditorRewardPoolRegistry.TinkerTimeTypePowerPoolId, CardType.Power, isRider: false);
		return choices;
	}

	private static List<TinkerChoice> GetCustomRiderChoices(TinkerTime tinker, CardType type)
	{
		List<TinkerChoice> choices = new();
		string poolId = type switch
		{
			CardType.Attack => CardEditorRewardPoolRegistry.TinkerTimeRiderAttackPoolId,
			CardType.Skill => CardEditorRewardPoolRegistry.TinkerTimeRiderSkillPoolId,
			CardType.Power => CardEditorRewardPoolRegistry.TinkerTimeRiderPowerPoolId,
			_ => string.Empty
		};
		if (!string.IsNullOrWhiteSpace(poolId))
		{
			AddCustomChoices(choices, tinker, poolId, type, isRider: true);
		}
		return choices;
	}

	private static void AddCustomChoices(List<TinkerChoice> choices, TinkerTime tinker, string poolId, CardType type, bool isRider)
	{
		foreach (CardEditorRewardPoolTemplateCandidate candidate in CardEditorRewardPoolRegistry.GetEnabledTemplateCandidates(new[] { poolId }, tinker.Owner))
		{
			if (candidate.Template == null)
			{
				continue;
			}

			choices.Add(isRider
				? TinkerChoice.CustomRider(type, candidate.Template, candidate.Mode)
				: TinkerChoice.CustomType(type, candidate.Template, candidate.Mode));
		}
	}

	private static List<TinkerChoice> ChooseDisplayed(List<TinkerChoice> vanilla, List<TinkerChoice> custom, Rng rng, int count)
	{
		List<TinkerChoice> replacement = custom.Where(c => c.Mode == CardEditorRewardPoolInjectionMode.ReplacePool).ToList();
		if (replacement.Count > 0)
		{
			return TakeRandom(replacement, count, rng);
		}

		List<TinkerChoice> pool = vanilla.Concat(custom).ToList();
		List<TinkerChoice> selected = TakeRandom(pool, count, rng);
		if (custom.Any(c => c.Mode == CardEditorRewardPoolInjectionMode.ForceInclude)
			&& !selected.Any(c => !c.IsVanilla))
		{
			TinkerChoice? forced = rng.NextItem(custom.Where(c => c.Mode == CardEditorRewardPoolInjectionMode.ForceInclude).ToList());
			if (selected.Count < count)
			{
				selected.Add(forced!); // forced non-null: ForceInclude list is non-empty (checked by .Any above)
			}
			else if (selected.Count > 0)
			{
				selected[Math.Clamp(rng.NextInt(selected.Count), 0, selected.Count - 1)] = forced!;
			}
		}

		return selected;
	}

	private static List<TinkerChoice> TakeRandom(List<TinkerChoice> source, int count, Rng rng)
	{
		List<TinkerChoice> pool = source.ToList();
		List<TinkerChoice> result = new();
		while (pool.Count > 0 && result.Count < count)
		{
			int index = Math.Clamp(rng.NextInt(pool.Count), 0, pool.Count - 1);
			result.Add(pool[index]);
			pool.RemoveAt(index);
		}
		return result;
	}

	private static IReadOnlyList<TinkerTime.RiderEffect> GetVanillaRiders(CardType type)
	{
		return type switch
		{
			CardType.Attack => new[]
			{
				TinkerTime.RiderEffect.Sapping,
				TinkerTime.RiderEffect.Violence,
				TinkerTime.RiderEffect.Choking
			},
			CardType.Skill => new[]
			{
				TinkerTime.RiderEffect.Energized,
				TinkerTime.RiderEffect.Wisdom,
				TinkerTime.RiderEffect.Chaos
			},
			CardType.Power => new[]
			{
				TinkerTime.RiderEffect.Expertise,
				TinkerTime.RiderEffect.Curious,
				TinkerTime.RiderEffect.Improvement
			},
			_ => Array.Empty<TinkerTime.RiderEffect>()
		};
	}

	private static string GetVanillaTypeLocKey(CardType type)
	{
		return type switch
		{
			CardType.Attack => "TINKER_TIME.pages.CHOOSE_CARD_TYPE.options.ATTACK",
			CardType.Skill => "TINKER_TIME.pages.CHOOSE_CARD_TYPE.options.SKILL",
			CardType.Power => "TINKER_TIME.pages.CHOOSE_CARD_TYPE.options.POWER",
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
		};
	}

	private static string GetVanillaRiderLocKey(TinkerTime.RiderEffect rider)
	{
		return rider switch
		{
			TinkerTime.RiderEffect.Sapping => "TINKER_TIME.pages.CHOOSE_RIDER.options.SAPPING",
			TinkerTime.RiderEffect.Violence => "TINKER_TIME.pages.CHOOSE_RIDER.options.VIOLENCE",
			TinkerTime.RiderEffect.Choking => "TINKER_TIME.pages.CHOOSE_RIDER.options.CHOKING",
			TinkerTime.RiderEffect.Energized => "TINKER_TIME.pages.CHOOSE_RIDER.options.ENERGIZED",
			TinkerTime.RiderEffect.Wisdom => "TINKER_TIME.pages.CHOOSE_RIDER.options.WISDOM",
			TinkerTime.RiderEffect.Chaos => "TINKER_TIME.pages.CHOOSE_RIDER.options.CHAOS",
			TinkerTime.RiderEffect.Expertise => "TINKER_TIME.pages.CHOOSE_RIDER.options.EXPERTISE",
			TinkerTime.RiderEffect.Curious => "TINKER_TIME.pages.CHOOSE_RIDER.options.CURIOUS",
			TinkerTime.RiderEffect.Improvement => "TINKER_TIME.pages.CHOOSE_RIDER.options.IMPROVEMENT",
			_ => throw new ArgumentOutOfRangeException(nameof(rider), rider, null)
		};
	}

	private static LocString RuntimeLoc(string key, string text, params (string Key, string Value)[] vars)
	{
		string localizedText = CardEditorLoc.T(key, text);
		try
		{
			LocManager.Instance?.GetTable("extensions").MergeWith(new Dictionary<string, string>
			{
				[key] = localizedText
			});
		}
		catch
		{
		}

		LocString loc = new("extensions", key);
		foreach ((string varKey, string value) in vars)
		{
			loc.Add(varKey, value);
		}
		return loc;
	}

	private static LocString EventLoc(string key)
		=> new("events", key);

	private static TinkerBuildState GetState(TinkerTime tinker)
		=> _state.GetOrCreateValue(tinker);

	private static TinkerBuildState? GetStateOrNull(TinkerTime tinker)
	{
		return _state.TryGetValue(tinker, out TinkerBuildState? state) ? state : null;
	}

	private static void ClearState(TinkerTime tinker)
	{
		_state.Remove(tinker);
	}

	private static CardType GetChosenCardType(TinkerTime tinker)
	{
		return _chosenCardTypeField?.GetValue(tinker) is CardType type ? type : CardType.Attack;
	}

	private static void SetChosenCardType(TinkerTime tinker, CardType type)
	{
		_chosenCardTypeField?.SetValue(tinker, type);
	}

	private static void SetEventState(TinkerTime tinker, LocString description, IEnumerable<EventOption> options)
	{
		if (_setEventStateMethod == null)
		{
			throw new MissingMethodException(typeof(EventModel).FullName, "SetEventState");
		}

		_setEventStateMethod.Invoke(tinker, new object[] { description, options });
	}

	private static void SetEventFinished(TinkerTime tinker, LocString description)
	{
		if (_setEventFinishedMethod == null)
		{
			throw new MissingMethodException(typeof(EventModel).FullName, "SetEventFinished");
		}

		_setEventFinishedMethod.Invoke(tinker, new object[] { description });
	}

	private sealed class TinkerBuildState
	{
		public CardType BaseType { get; set; } = CardType.Attack;
		public ModelId BaseCardId { get; set; } = ModelId.none;
		public bool HasCustomBase => BaseCardId != null && BaseCardId != ModelId.none;
	}

	private sealed record TinkerChoice(
		CardType Type,
		TinkerTime.RiderEffect Rider,
		CardModel? Template,
		CardEditorRewardPoolInjectionMode Mode,
		bool IsVanilla)
	{
		public static TinkerChoice VanillaType(CardType type)
			=> new(type, TinkerTime.RiderEffect.None, null, CardEditorRewardPoolInjectionMode.AddToPool, true);

		public static TinkerChoice CustomType(CardType type, CardModel template, CardEditorRewardPoolInjectionMode mode)
			=> new(type, TinkerTime.RiderEffect.None, template, mode, false);

		public static TinkerChoice VanillaRider(CardType type, TinkerTime.RiderEffect rider)
			=> new(type, rider, null, CardEditorRewardPoolInjectionMode.AddToPool, true);

		public static TinkerChoice CustomRider(CardType type, CardModel template, CardEditorRewardPoolInjectionMode mode)
			=> new(type, TinkerTime.RiderEffect.None, template, mode, false);
	}
}
