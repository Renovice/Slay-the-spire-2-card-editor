using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

// Boot-time consistency audit: mechanically cross-checks the per-kind tables that previously had to
// be kept in sync by hand (definitions, save DTO, clone, description text). Every future mismatch —
// a new field missing from the DTO, a kind without text, a clone dropping data — surfaces as a
// Log.Warn at startup instead of shipping silently as a "dead option".
internal static class CardEditorConsistencyAudit
{
	private static bool _descriptionAuditRan;

	// Kinds that intentionally render no description text of their own.
	private static readonly HashSet<CardExtraEffectKind> _intentionallyTextlessKinds = new()
	{
		CardExtraEffectKind.EffectLimit,
		CardExtraEffectKind.HoverPreview,
	};

	public static void RunStartupAudits()
	{
		int issues = 0;
		issues += RunSafely(AuditDefinitionCoverage, "definition coverage");
		issues += RunSafely(AuditCloneFidelity, "clone fidelity");
		issues += RunSafely(AuditDtoRoundTrip, "save round-trip");
		if (issues == 0)
		{
			Log.Info("[CardEditor] Consistency audit passed (definitions, clone, save round-trip).");
		}
	}

	public static void RunDescriptionAuditOnce(CardModel? card)
	{
		if (_descriptionAuditRan || card == null)
		{
			return;
		}

		_descriptionAuditRan = true;
		RunSafely(() => AuditDescriptionCoverage(card), "description coverage");
	}

	private static int RunSafely(Func<int> audit, string name)
	{
		try
		{
			return audit();
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Consistency audit '{name}' failed to run: {ex}");
			return 1;
		}
	}

	private static int AuditDefinitionCoverage()
	{
		HashSet<CardExtraEffectKind> defined = CardEditorExtraEffects.Definitions
			.Select(def => def.Kind)
			.ToHashSet();
		List<string> missing = Enum.GetValues<CardExtraEffectKind>()
			.Where(kind => !defined.Contains(kind))
			.Select(kind => kind.ToString())
			.ToList();
		int duplicates = CardEditorExtraEffects.Definitions.Count - defined.Count;

		if (missing.Count > 0)
		{
			Log.Warn($"[CardEditor] Audit: effect kinds without a definition (invisible in the editor): {string.Join(", ", missing)}");
		}
		if (duplicates > 0)
		{
			Log.Warn($"[CardEditor] Audit: {duplicates} duplicate effect definition entries.");
		}

		return missing.Count + duplicates;
	}

	private static int AuditCloneFidelity()
	{
		CardExtraEffect source = BuildFullyPopulatedEffect();
		CardExtraEffect clone = CardEditorExtraEffects.CloneEffect(source);

		List<string> lost = new();
		List<string> aliased = new();
		foreach (PropertyInfo property in GetAuditableProperties())
		{
			object? sourceValue = property.GetValue(source);
			object? cloneValue = property.GetValue(clone);
			if (!AuditValuesEqual(sourceValue, cloneValue))
			{
				lost.Add(property.Name);
				continue;
			}

			// A shared mutable reference means editing one effect would silently edit its clones.
			if (sourceValue != null
				&& ReferenceEquals(sourceValue, cloneValue)
				&& property.PropertyType.IsClass
				&& property.PropertyType != typeof(string))
			{
				aliased.Add(property.Name);
			}
		}

		if (lost.Count > 0)
		{
			Log.Warn($"[CardEditor] Audit: CloneEffect drops or changes: {string.Join(", ", lost)}");
		}
		if (aliased.Count > 0)
		{
			Log.Warn($"[CardEditor] Audit: CloneEffect shares mutable references for: {string.Join(", ", aliased)}");
		}

		return lost.Count + aliased.Count;
	}

	private static int AuditDtoRoundTrip()
	{
		CardExtraEffect source = BuildFullyPopulatedEffect();
		if (!CardEditorPresetStore.TryRoundTripEffectForAudit(source, out CardExtraEffect roundTripped))
		{
			Log.Warn("[CardEditor] Audit: save round-trip failed entirely (TryToEffect returned false).");
			return 1;
		}

		List<string> changed = new();
		foreach (PropertyInfo property in GetAuditableProperties())
		{
			if (!AuditValuesEqual(property.GetValue(source), property.GetValue(roundTripped)))
			{
				changed.Add(property.Name);
			}
		}

		if (changed.Count > 0)
		{
			Log.Warn($"[CardEditor] Audit: save round-trip loses or changes (field not plumbed through the preset DTO?): {string.Join(", ", changed)}");
		}

		return changed.Count;
	}

	private static int AuditDescriptionCoverage(CardModel card)
	{
		List<string> textless = new();
		foreach (CardExtraEffectDefinition definition in CardEditorExtraEffects.Definitions)
		{
			if (_intentionallyTextlessKinds.Contains(definition.Kind))
			{
				continue;
			}

			CardExtraEffect effect = new CardExtraEffect
			{
				Kind = definition.Kind,
				Target = definition.DefaultTarget,
				Amount = definition.DefaultAmount,
			};

			bool hasText;
			try
			{
				hasText = CardEditorExtraEffects.TryFormatLineForAudit(card, effect, out _);
			}
			catch
			{
				hasText = false;
			}

			if (!hasText)
			{
				textless.Add(definition.Kind.ToString());
			}
		}

		if (textless.Count > 0)
		{
			Log.Warn($"[CardEditor] Audit: kinds render no card text for a default-configured effect (possible dead option or missing required setting): {string.Join(", ", textless)}");
		}

		return textless.Count;
	}

	private static IEnumerable<PropertyInfo> GetAuditableProperties()
	{
		return typeof(CardExtraEffect)
			.GetProperties(BindingFlags.Instance | BindingFlags.Public)
			.Where(property => property.CanRead && property.CanWrite);
	}

	// Builds an effect with every property set to a non-default value that survives load-time
	// normalization (positive in-range numbers, valid enum names), so a round-trip mismatch
	// means a missing mapping rather than an intentional clamp.
	private static CardExtraEffect BuildFullyPopulatedEffect()
	{
		CardExtraEffect effect = new CardExtraEffect();
		int seed = 0;
		foreach (PropertyInfo property in GetAuditableProperties())
		{
			seed++;
			object? value = CreateAuditValue(property.PropertyType, seed);
			if (value != null)
			{
				property.SetValue(effect, value);
			}
		}

		// Keep interacting fields coherent so normalization preserves them.
		effect.Timing = CardExtraEffectTiming.StartOfTurn;
		effect.Turns = 2;
		effect.Amount = 2;
		effect.RepeatCount = 2;
		effect.AmountSourceMultiplier = 2m;
		effect.HistoryScalingCountStep = 2;
		effect.RepeatScalingExtraTimes = 2;
		effect.BranchEffect = new CardExtraEffect
		{
			Kind = CardExtraEffectKind.GainBlock,
			Target = CardExtraEffectTarget.Self,
			Amount = 3,
		};
		effect.ChooseOneOption1 = new CardExtraEffectChooseOneOption { CardId = "audit.card1" };
		effect.ChooseOneOption2 = new CardExtraEffectChooseOneOption { CardId = "audit.card2" };
		effect.ChooseOneOption3 = new CardExtraEffectChooseOneOption { CardId = "audit.card3" };
		return effect;
	}

	private static object? CreateAuditValue(Type type, int seed)
	{
		Type effective = Nullable.GetUnderlyingType(type) ?? type;
		if (effective == typeof(bool))
		{
			return true;
		}
		if (effective == typeof(int))
		{
			return 2;
		}
		if (effective == typeof(decimal))
		{
			return 2m;
		}
		if (effective == typeof(float))
		{
			return 2f;
		}
		if (effective == typeof(string))
		{
			return "audit" + seed.ToString(System.Globalization.CultureInfo.InvariantCulture);
		}
		if (effective.IsEnum)
		{
			Array values = Enum.GetValues(effective);
			return values.Length > 1 ? values.GetValue(1) : values.GetValue(0);
		}

		// Reference-typed members (BranchEffect, ChooseOneOption*) are populated explicitly.
		return null;
	}

	private static bool AuditValuesEqual(object? a, object? b)
	{
		if (a == null || b == null)
		{
			return a == null && b == null;
		}

		if (a is CardExtraEffect effectA && b is CardExtraEffect effectB)
		{
			return effectA.Kind == effectB.Kind && effectA.Amount == effectB.Amount;
		}

		if (a is CardExtraEffectChooseOneOption optionA && b is CardExtraEffectChooseOneOption optionB)
		{
			return string.Equals(optionA.CardId, optionB.CardId, StringComparison.Ordinal);
		}

		return a.Equals(b);
	}
}
