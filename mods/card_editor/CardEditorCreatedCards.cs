using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace SlayTheSpire2Mod.CardEditor;

public abstract class CardEditorCreatedCardBase : CardModel, KnowledgeDemon.IChoosable
{
	private string _cardEditorSelfScalingDiff = string.Empty;

	protected CardEditorCreatedCardBase()
		: base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, shouldShowInCardLibrary: false)
	{
	}

	[SavedProperty]
	public string CardEditorSelfScalingDiff
	{
		get => _cardEditorSelfScalingDiff;
		set
		{
			AssertMutable();
			_cardEditorSelfScalingDiff = value ?? string.Empty;
		}
	}

	public override CardPoolModel Pool => CardEditorExtraEffects.TryGetDynamicIdentitySource(this, out CardModel identitySource)
		? identitySource.Pool
		: CardEditorCreatedCardsStore.GetPoolForCard(base.Id);

	public override CardPoolModel VisualCardPool => CardEditorExtraEffects.TryGetDynamicIdentitySource(this, out CardModel identitySource)
		? identitySource.VisualCardPool
		: Pool;

	public override string PortraitPath => CardEditorExtraEffects.TryGetDynamicIdentitySource(this, out CardModel identitySource)
		? identitySource.PortraitPath
		: CardEditorCreatedCardsStore.GetPortraitPathForCard(base.Id) ?? CardModel.MissingPortraitPath;

	public override string BetaPortraitPath => CardEditorExtraEffects.TryGetDynamicIdentitySource(this, out CardModel identitySource)
		? identitySource.BetaPortraitPath
		: PortraitPath;

	public override CardType Type => CardEditorExtraEffects.TryGetDynamicIdentitySource(this, out CardModel identitySource)
		? identitySource.Type
		: CardEditorCreatedCardsStore.GetCardTypeForCard(base.Id);

	public override CardRarity Rarity => CardEditorExtraEffects.TryGetDynamicIdentitySource(this, out CardModel identitySource)
		? identitySource.Rarity
		: CardEditorCreatedCardsStore.GetRarityForCard(base.Id);

	public override TargetType TargetType => CardEditorExtraEffects.TryGetDynamicIdentitySource(this, out CardModel identitySource)
		? identitySource.TargetType
		: CardEditorCreatedCardsStore.GetTargetTypeForCard(base.Id);

	public override IEnumerable<CardKeyword> CanonicalKeywords
	{
		get
		{
			if (CardEditorExtraEffects.TryGetDynamicIdentitySource(this, out CardModel identitySource))
			{
				return identitySource.CanonicalKeywords;
			}

			HashSet<CardKeyword> keywords = new HashSet<CardKeyword>(base.CanonicalKeywords);

			foreach (CardKeyword keyword in CardEditorCreatedCardEffectSourceSupport.GetEffectSourceKeywords(this, isUpgradePreview: false))
			{
				keywords.Add(keyword);
			}

			return keywords;
		}
	}

	public override bool GainsBlock
	{
		get
		{
			if (CardEditorExtraEffects.TryGetDynamicIdentitySource(this, out CardModel identitySource))
			{
				return identitySource.GainsBlock;
			}

			try
			{
				foreach (ModelId effectSourceId in CardEditorCreatedCardsStore.GetEffectSourceCardIds(base.Id))
				{
					if (effectSourceId == null || effectSourceId == ModelId.none)
					{
						continue;
					}
					if (CardEditorCreatedCardsStore.IsCreatedCardId(effectSourceId))
					{
						continue;
					}
					CardModel? source = ModelDb.GetByIdOrNull<CardModel>(effectSourceId);
					if (source != null && source.GainsBlock)
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
				foreach (CardExtraEffect effect in CardEditorExtraEffects.GetRuntimeEffectsForExecution(null, this))
				{
					if (effect != null && effect.Kind == CardExtraEffectKind.GainBlock && effect.Amount > 0)
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
	}

	public override IEnumerable<CardTag> Tags
	{
		get
		{
			if (CardEditorExtraEffects.TryGetDynamicIdentitySource(this, out CardModel identitySource))
			{
				return identitySource.Tags;
			}

			return GetEffectiveTags();
		}
	}

	private IReadOnlySet<CardTag> GetEffectiveTags()
	{
		HashSet<CardTag> tags = new HashSet<CardTag>();

		try
		{
			foreach (ModelId effectSourceId in CardEditorCreatedCardsStore.GetEffectSourceCardIds(base.Id))
			{
				if (effectSourceId == null || effectSourceId == ModelId.none)
				{
					continue;
				}
				if (CardEditorCreatedCardsStore.IsCreatedCardId(effectSourceId))
				{
					continue;
				}

				CardModel? source = ModelDb.GetByIdOrNull<CardModel>(effectSourceId);
				if (source == null)
				{
					continue;
				}

				foreach (CardTag tag in source.Tags)
				{
					tags.Add(tag);
				}
			}
		}
		catch
		{
			// ignored
		}

		string title = CardEditorCreatedCardsStore.GetTitleForCard(base.Id);
		if (!string.IsNullOrWhiteSpace(title))
		{
			if (title.Contains("strike", StringComparison.OrdinalIgnoreCase))
			{
				tags.Add(CardTag.Strike);
			}
			if (title.Contains("defend", StringComparison.OrdinalIgnoreCase))
			{
				tags.Add(CardTag.Defend);
			}
		}

		if (GainsBlock && Type == CardType.Skill)
		{
			tags.Add(CardTag.Defend);
		}

		if (!CardEditorOverrides.SuppressAllOverrides
			&& CardEditorOverrides.TryGetEffectiveOverride(base.Id, out CardOverride overrideData))
		{
			if (overrideData.TagsToRemove != null && overrideData.TagsToRemove.Count > 0)
			{
				tags.ExceptWith(overrideData.TagsToRemove);
			}
			if (overrideData.TagsToAdd != null && overrideData.TagsToAdd.Count > 0)
			{
				tags.UnionWith(overrideData.TagsToAdd);
			}
		}

		tags.Remove(CardTag.None);
		return tags;
	}

	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		// No default behavior: this card is driven by Card Editor overrides + extra effects.
		return Task.CompletedTask;
	}

	public async Task OnChosen()
	{
		try
		{
			CombatState? combatState = this.GetConcreteCombatState() ?? this.TryGetOwnerCreature().GetConcreteCombatState();
			if (combatState == null)
			{
				return;
			}

			await CardEditorExtraEffects.RunOnChosen(combatState, new BlockingPlayerChoiceContext(), this);
		}
		catch
		{
		}
	}
}

public sealed class CardEditorCreatedCard01 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard02 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard03 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard04 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard05 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard06 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard07 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard08 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard09 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard10 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard11 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard12 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard13 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard14 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard15 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard16 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard17 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard18 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard19 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard20 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard21 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard22 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard23 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard24 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard25 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard26 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard27 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard28 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard29 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard30 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard31 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard32 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard33 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard34 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard35 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard36 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard37 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard38 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard39 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard40 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard41 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard42 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard43 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard44 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard45 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard46 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard47 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard48 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard49 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard50 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard51 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard52 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard53 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard54 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard55 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard56 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard57 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard58 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard59 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard60 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard61 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard62 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard63 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard64 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard65 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard66 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard67 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard68 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard69 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard70 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard71 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard72 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard73 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard74 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard75 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard76 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard77 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard78 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard79 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard80 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard81 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard82 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard83 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard84 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard85 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard86 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard87 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard88 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard89 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard90 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard91 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard92 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard93 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard94 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard95 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard96 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard97 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard98 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard99 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard100 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard101 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard102 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard103 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard104 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard105 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard106 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard107 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard108 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard109 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard110 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard111 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard112 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard113 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard114 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard115 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard116 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard117 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard118 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard119 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard120 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard121 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard122 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard123 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard124 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard125 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard126 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard127 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard128 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard129 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard130 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard131 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard132 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard133 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard134 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard135 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard136 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard137 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard138 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard139 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard140 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard141 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard142 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard143 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard144 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard145 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard146 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard147 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard148 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard149 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard150 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard151 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard152 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard153 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard154 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard155 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard156 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard157 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard158 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard159 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard160 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard161 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard162 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard163 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard164 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard165 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard166 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard167 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard168 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard169 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard170 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard171 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard172 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard173 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard174 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard175 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard176 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard177 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard178 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard179 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard180 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard181 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard182 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard183 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard184 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard185 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard186 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard187 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard188 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard189 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard190 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard191 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard192 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard193 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard194 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard195 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard196 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard197 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard198 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard199 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard200 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard201 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard202 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard203 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard204 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard205 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard206 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard207 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard208 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard209 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard210 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard211 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard212 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard213 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard214 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard215 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard216 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard217 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard218 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard219 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard220 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard221 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard222 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard223 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard224 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard225 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard226 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard227 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard228 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard229 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard230 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard231 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard232 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard233 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard234 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard235 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard236 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard237 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard238 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard239 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard240 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard241 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard242 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard243 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard244 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard245 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard246 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard247 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard248 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard249 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard250 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard251 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard252 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard253 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard254 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard255 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard256 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard257 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard258 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard259 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard260 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard261 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard262 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard263 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard264 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard265 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard266 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard267 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard268 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard269 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard270 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard271 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard272 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard273 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard274 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard275 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard276 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard277 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard278 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard279 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard280 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard281 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard282 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard283 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard284 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard285 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard286 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard287 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard288 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard289 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard290 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard291 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard292 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard293 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard294 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard295 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard296 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard297 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard298 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard299 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard300 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard301 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard302 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard303 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard304 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard305 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard306 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard307 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard308 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard309 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard310 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard311 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard312 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard313 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard314 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard315 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard316 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard317 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard318 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard319 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard320 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard321 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard322 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard323 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard324 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard325 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard326 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard327 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard328 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard329 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard330 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard331 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard332 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard333 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard334 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard335 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard336 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard337 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard338 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard339 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard340 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard341 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard342 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard343 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard344 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard345 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard346 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard347 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard348 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard349 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard350 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard351 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard352 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard353 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard354 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard355 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard356 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard357 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard358 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard359 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard360 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard361 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard362 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard363 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard364 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard365 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard366 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard367 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard368 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard369 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard370 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard371 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard372 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard373 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard374 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard375 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard376 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard377 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard378 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard379 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard380 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard381 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard382 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard383 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard384 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard385 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard386 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard387 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard388 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard389 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard390 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard391 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard392 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard393 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard394 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard395 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard396 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard397 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard398 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard399 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard400 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard401 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard402 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard403 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard404 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard405 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard406 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard407 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard408 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard409 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard410 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard411 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard412 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard413 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard414 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard415 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard416 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard417 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard418 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard419 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard420 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard421 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard422 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard423 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard424 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard425 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard426 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard427 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard428 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard429 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard430 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard431 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard432 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard433 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard434 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard435 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard436 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard437 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard438 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard439 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard440 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard441 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard442 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard443 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard444 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard445 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard446 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard447 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard448 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard449 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard450 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard451 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard452 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard453 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard454 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard455 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard456 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard457 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard458 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard459 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard460 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard461 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard462 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard463 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard464 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard465 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard466 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard467 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard468 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard469 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard470 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard471 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard472 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard473 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard474 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard475 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard476 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard477 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard478 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard479 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard480 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard481 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard482 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard483 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard484 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard485 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard486 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard487 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard488 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard489 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard490 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard491 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard492 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard493 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard494 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard495 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard496 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard497 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard498 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard499 : CardEditorCreatedCardBase { }
public sealed class CardEditorCreatedCard500 : CardEditorCreatedCardBase { }
