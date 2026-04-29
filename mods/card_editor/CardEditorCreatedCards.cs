using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

public abstract class CardEditorCreatedCardBase : CardModel
{
	protected CardEditorCreatedCardBase()
		: base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, shouldShowInCardLibrary: false)
	{
	}

	public override CardPoolModel Pool => CardEditorCreatedCardsStore.GetPoolForCard(base.Id);

	public override CardPoolModel VisualCardPool => Pool;

	public override string PortraitPath => CardEditorCreatedCardsStore.GetPortraitPathForCard(base.Id) ?? CardModel.MissingPortraitPath;

	public override string BetaPortraitPath => PortraitPath;

	public override CardType Type => CardEditorCreatedCardsStore.GetCardTypeForCard(base.Id);

	public override CardRarity Rarity => CardEditorCreatedCardsStore.GetRarityForCard(base.Id);

	public override TargetType TargetType => CardEditorCreatedCardsStore.GetTargetTypeForCard(base.Id);

	public override IEnumerable<CardKeyword> CanonicalKeywords
	{
		get
		{
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
