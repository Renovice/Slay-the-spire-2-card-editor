using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal sealed class CardEditorCustomKeywordLibraryEntry
{
	public string? DefinitionId { get; init; }
	public string KeywordName { get; init; } = string.Empty;
	public string Description { get; init; } = string.Empty;
	public string PlainDescription { get; init; } = string.Empty;
	public ModelId SourceCardId { get; init; } = ModelId.none;
	public string SourceCardTitle { get; init; } = string.Empty;
	public string SearchText { get; init; } = string.Empty;
	public IReadOnlyList<CardExtraEffect> Effects { get; init; } = Array.Empty<CardExtraEffect>();
	public bool IsExplicitDefinition { get; init; }
}

internal static class CardEditorCustomKeywordLibrary
{
	public static IReadOnlyList<CardEditorCustomKeywordLibraryEntry> BuildEntries()
	{
		List<CardEditorCustomKeywordLibraryEntry> entries = new List<CardEditorCustomKeywordLibraryEntry>();
		HashSet<string> seenEntryKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (CardEditorCustomKeywordDefinition definition in CardEditorDefinitionStore.GetKeywordDefinitions())
		{
			string keywordName = definition.Name?.Trim() ?? string.Empty;
			if (string.IsNullOrWhiteSpace(keywordName))
			{
				continue;
			}

			string entryKey = NormalizeKeywordKey(keywordName);
			if (!seenEntryKeys.Add(entryKey))
			{
				continue;
			}

			string description = definition.Description?.Trim() ?? string.Empty;
			string plainDescription = StripMarkup(description);
			entries.Add(new CardEditorCustomKeywordLibraryEntry
			{
				DefinitionId = definition.Id,
				KeywordName = keywordName,
				Description = description,
				PlainDescription = plainDescription,
				SourceCardId = ModelId.none,
				SourceCardTitle = "Keyword Definition",
				SearchText = string.Join('\n', new[]
				{
					keywordName,
					"Keyword Definition",
					plainDescription,
					definition.Id
				}),
				Effects = definition.Effects.Select(CardEditorExtraEffects.CloneEffect).ToList(),
				IsExplicitDefinition = true
			});
		}

		foreach (ModelId cardId in EnumerateCandidateCardIds())
		{
			if (cardId == null || cardId == ModelId.none)
			{
				continue;
			}

			try
			{
				CardModel canonical = ModelDb.GetById<CardModel>(cardId);
				if (canonical == null)
				{
					continue;
				}

				CardModel preview = CardEditorOverrides.BuildPreview(canonical);
				HashSet<string> explicitKeywordNames = CardEditorExtraEffects.GetEffectsForDescription(preview, isUpgradePreview: false)
					.Where(effect => effect != null && !string.IsNullOrWhiteSpace(effect.CustomKeywordName))
					.Select(effect => effect.CustomKeywordName!.Trim())
					.ToHashSet(StringComparer.OrdinalIgnoreCase);
				if (explicitKeywordNames.Count == 0)
				{
					continue;
				}

				string sourceTitle = string.IsNullOrWhiteSpace(preview.Title)
					? cardId.ToString()
					: preview.Title.Trim();

				foreach (CardExtraEffectKeywordSummary summary in CardEditorExtraEffects.GetCustomKeywordSummaries(preview))
				{
					string keywordName = summary.Name?.Trim() ?? string.Empty;
					if (string.IsNullOrWhiteSpace(keywordName) || !explicitKeywordNames.Contains(keywordName))
					{
						continue;
					}

					string entryKey = NormalizeKeywordKey(keywordName);
					if (!seenEntryKeys.Add(entryKey))
					{
						continue;
					}

					string description = summary.Description?.Trim() ?? string.Empty;
					string plainDescription = StripMarkup(description);
					entries.Add(new CardEditorCustomKeywordLibraryEntry
					{
						KeywordName = keywordName,
						Description = description,
						PlainDescription = plainDescription,
						SourceCardId = cardId,
						SourceCardTitle = sourceTitle,
						SearchText = string.Join('\n', new[]
						{
							keywordName,
							sourceTitle,
							plainDescription,
							cardId.ToString()
						}),
						Effects = CardEditorExtraEffects.GetEffectsForDescription(preview, isUpgradePreview: false)
							.Where(effect => string.Equals(effect?.CustomKeywordName?.Trim(), keywordName, StringComparison.OrdinalIgnoreCase))
							.Select(CardEditorExtraEffects.CloneEffect)
							.ToList()
					});
				}
			}
			catch
			{
			}
		}

		entries.Sort((a, b) =>
		{
			int cmp = string.Compare(a.KeywordName, b.KeywordName, StringComparison.OrdinalIgnoreCase);
			if (cmp != 0)
			{
				return cmp;
			}

			cmp = string.Compare(a.SourceCardTitle, b.SourceCardTitle, StringComparison.OrdinalIgnoreCase);
			if (cmp != 0)
			{
				return cmp;
			}

			return a.SourceCardId.CompareTo(b.SourceCardId);
		});

		return entries;
	}

	private static string NormalizeKeywordKey(string? keywordName)
	{
		return string.IsNullOrWhiteSpace(keywordName) ? string.Empty : keywordName.Trim();
	}

	private static IEnumerable<ModelId> EnumerateCandidateCardIds()
	{
		HashSet<string> seenIds = new HashSet<string>(StringComparer.Ordinal);

		foreach (CardModel card in ModelDb.AllCards)
		{
			if (card?.Id == null)
			{
				continue;
			}

			string rawId = card.Id.ToString();
			if (seenIds.Add(rawId))
			{
				yield return card.Id;
			}
		}

		foreach (ModelId createdId in CardEditorCreatedCardsStore.GetAllCreatedCardIds())
		{
			if (createdId == null)
			{
				continue;
			}

			string rawId = createdId.ToString();
			if (seenIds.Add(rawId))
			{
				yield return createdId;
			}
		}
	}

	internal static string StripMarkupForDisplay(string text)
	{
		return StripMarkup(text);
	}

	private static string StripMarkup(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
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

			if (insideTag)
			{
				if (ch == ']')
				{
					insideTag = false;
				}
				continue;
			}

			sb.Append(ch);
		}

		return sb.ToString().Replace("\r", string.Empty).Trim();
	}
}
