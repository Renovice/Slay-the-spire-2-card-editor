namespace SlayTheSpire2Mod.CardEditor;

// P1.5: shared phrase arms for the CardEditorExtraEffects text formatters. Helpers here must stay
// byte-identical in output to the inline arms they replaced; the boot snapshot audit diffs all text.
internal static class CardEditorPhraseComposer
{
	internal static string? NormalizeCustomName(string? value)
	{
		string trimmed = value?.Trim() ?? string.Empty;
		return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
	}

	// Vanilla ally frames for player-resolved lines. Draw-style imperatives ("Draw 2 cards from your
	// [gold]Discard Pile[/gold].") become "Another player draws 2 cards from their ..." (Constellation/
	// Tutor wording); resource arms become "Another player gains {payload}" / "ALL players gain ..."
	// (Believe in You / Energy Surge). Self keeps the untouched imperative line.
	internal static string ApplyPlayerDrawSubject(string line, CardExtraEffectTarget target)
	{
		string? subject = target switch
		{
			CardExtraEffectTarget.AnyAlly => "Another player draws",
			CardExtraEffectTarget.AllAllies => "ALL players draw",
			CardExtraEffectTarget.AnyPlayer => "Any player draws",
			_ => null
		};
		if (subject == null || string.IsNullOrEmpty(line) || !line.StartsWith("Draw ", StringComparison.Ordinal))
		{
			return line;
		}

		return subject + line.Substring("Draw".Length).Replace(" your ", " their ");
	}

	internal static string FormatPlayerResourceArm(CardExtraEffectTarget target, string selfLine, string singularVerbPhrase, string pluralVerbPhrase)
	{
		return target switch
		{
			CardExtraEffectTarget.AnyAlly => $"Another player {singularVerbPhrase}",
			CardExtraEffectTarget.AllAllies => $"ALL players {pluralVerbPhrase}",
			CardExtraEffectTarget.AnyPlayer => $"Any player {singularVerbPhrase}",
			_ => selfLine
		};
	}

	// Vanilla frames for player-resolved resources ("Another player gains ...", "ALL players gain ...",
	// per Believe in You / Energy Surge). Self keeps the imperative; enemy targets are not resolvable
	// for these kinds at runtime, so they keep the imperative default too.
	internal static string FormatEqualToPlayerResourceText(CardExtraEffectTarget target, bool gain, string resourceName, string referenceText)
	{
		string selfVerb = gain ? "Gain" : "Lose";
		string singularVerb = gain ? "gains" : "loses";
		string pluralVerb = gain ? "gain" : "lose";
		return target switch
		{
			CardExtraEffectTarget.AllAllies => $"ALL players {pluralVerb} {resourceName} equal to {referenceText}.",
			CardExtraEffectTarget.AnyAlly => $"Another player {singularVerb} {resourceName} equal to {referenceText}.",
			CardExtraEffectTarget.AnyPlayer => $"Any player {singularVerb} {resourceName} equal to {referenceText}.",
			_ => $"{selfVerb} {resourceName} equal to {referenceText}."
		};
	}

	internal static string FormatApplyEqualToText(CardExtraEffectTarget target, string payload, string referenceText, string suffixPart)
	{
		return target switch
		{
			CardExtraEffectTarget.AllEnemies => $"Apply {payload} equal to {referenceText} to ALL enemies{suffixPart}",
			CardExtraEffectTarget.OtherEnemies => $"Apply {payload} equal to {referenceText} to other enemies{suffixPart}",
			CardExtraEffectTarget.RandomEnemy => $"Apply {payload} equal to {referenceText} to a random enemy{suffixPart}",
			CardExtraEffectTarget.Self => $"Gain {payload} equal to {referenceText}{suffixPart}",
			CardExtraEffectTarget.AllAllies => $"Apply {payload} equal to {referenceText} to ALL players{suffixPart}",
			CardExtraEffectTarget.AnyAlly => $"Apply {payload} equal to {referenceText} to another player{suffixPart}",
			CardExtraEffectTarget.AnyPlayer => $"Apply {payload} equal to {referenceText} to any player{suffixPart}",
			_ => $"Apply {payload} equal to {referenceText}{suffixPart}"
		};
	}

	internal static string FormatApplyDebuffPayload(CardExtraEffectTarget target, string payload, string suffixPart)
	{
		return target switch
		{
			CardExtraEffectTarget.AllEnemies => CardEditorLoc.F("cardText.applyDebuff.allEnemies", $"Apply {payload} to ALL enemies{suffixPart}", ("Payload", payload), ("Suffix", suffixPart)),
			CardExtraEffectTarget.OtherEnemies => CardEditorLoc.F("cardText.applyDebuff.otherEnemies", $"Apply {payload} to other enemies{suffixPart}", ("Payload", payload), ("Suffix", suffixPart)),
			CardExtraEffectTarget.RandomEnemy => CardEditorLoc.F("cardText.applyDebuff.randomEnemy", $"Apply {payload} to a random enemy{suffixPart}", ("Payload", payload), ("Suffix", suffixPart)),
			CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.applyDebuff.self", $"Gain {payload}{suffixPart}", ("Payload", payload), ("Suffix", suffixPart)),
			CardExtraEffectTarget.AllAllies => CardEditorLoc.F("cardText.applyDebuff.allAllies", $"Apply {payload} to ALL players{suffixPart}", ("Payload", payload), ("Suffix", suffixPart)),
			CardExtraEffectTarget.AnyAlly => CardEditorLoc.F("cardText.applyDebuff.anyAlly", $"Apply {payload} to another player{suffixPart}", ("Payload", payload), ("Suffix", suffixPart)),
			CardExtraEffectTarget.AnyPlayer => CardEditorLoc.F("cardText.applyDebuff.anyPlayer", $"Apply {payload} to any player{suffixPart}", ("Payload", payload), ("Suffix", suffixPart)),
			_ => CardEditorLoc.F("cardText.applyDebuff.target", $"Apply {payload}{suffixPart}", ("Payload", payload), ("Suffix", suffixPart))
		};
	}

	internal static string FormatSignedPowerPayload(CardExtraEffectTarget target, bool gain, string payload, string suffixPart)
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

		return target switch
		{
			CardExtraEffectTarget.AllEnemies => CardEditorLoc.F("cardText.signedPower.allEnemies", $"ALL enemies {verbPlural} {payload}{suffixPart}", ("Verb", verbPlural), ("Payload", payload), ("Suffix", suffixPart)),
			CardExtraEffectTarget.OtherEnemies => CardEditorLoc.F("cardText.signedPower.otherEnemies", $"Other enemies {verbPlural} {payload}{suffixPart}", ("Verb", verbPlural), ("Payload", payload), ("Suffix", suffixPart)),
			CardExtraEffectTarget.RandomEnemy => CardEditorLoc.F("cardText.signedPower.randomEnemy", $"A random enemy {verbSingular} {payload}{suffixPart}", ("Verb", verbSingular), ("Payload", payload), ("Suffix", suffixPart)),
			CardExtraEffectTarget.Self => CardEditorLoc.F("cardText.signedPower.self", $"{wordSelf} {payload}{suffixPart}", ("Verb", wordSelf), ("Payload", payload), ("Suffix", suffixPart)),
			CardExtraEffectTarget.AllAllies => CardEditorLoc.F("cardText.signedPower.allAllies", $"ALL players {verbPlural} {payload}{suffixPart}", ("Verb", verbPlural), ("Payload", payload), ("Suffix", suffixPart)),
			CardExtraEffectTarget.AnyAlly => CardEditorLoc.F("cardText.signedPower.anyAlly", $"Another player {verbSingular} {payload}{suffixPart}", ("Verb", verbSingular), ("Payload", payload), ("Suffix", suffixPart)),
			CardExtraEffectTarget.AnyPlayer => CardEditorLoc.F("cardText.signedPower.anyPlayer", $"Any player {verbSingular} {payload}{suffixPart}", ("Verb", verbSingular), ("Payload", payload), ("Suffix", suffixPart)),
			_ => CardEditorLoc.F("cardText.signedPower.target", $"The target {verbSingular} {payload}{suffixPart}", ("Verb", verbSingular), ("Payload", payload), ("Suffix", suffixPart))
		};
	}
}
