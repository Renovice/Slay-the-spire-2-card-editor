using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

// Card Engine P0: golden text-snapshot harness. Renders every effect kind's generated card text
// across the full 8-target matrix into user://card_editor/text_snapshot.current.txt on boot and
// diffs it against text_snapshot.baseline.txt. Any wording change - intended or not - shows up as
// a boot warning with concrete before/after lines instead of shipping silently.
//
// Workflow: the FIRST run (no baseline) copies current -> baseline automatically. Before a text
// change (e.g. a Sentence Composer phase), the baseline is the pre-change truth; after building,
// launch once and read the diff. To accept intended changes, delete the baseline file (it will be
// re-seeded from current on the next launch).
internal static class CardEditorTextSnapshotAudit
{
	private const string CurrentPath = "user://card_editor/text_snapshot.current.txt";
	private const string BaselinePath = "user://card_editor/text_snapshot.baseline.txt";
	private const int MaxReportedDiffs = 12;
	private static readonly Regex EnergyIconPath = new(
		@"(?<=res://images/packed/sprite_fonts/)[^/\]]+_energy_icon\.png",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static bool _ran;

	public static void RunOnce(CardModel host)
	{
		if (_ran || host == null)
		{
			return;
		}

		_ran = true;
		try
		{
			List<string> lines = RenderMatrix(host);

			string currentPath = ProjectSettings.GlobalizePath(CurrentPath);
			string baselinePath = ProjectSettings.GlobalizePath(BaselinePath);
			Directory.CreateDirectory(Path.GetDirectoryName(currentPath)!);
			File.WriteAllLines(currentPath, lines, Encoding.UTF8);

			if (!File.Exists(baselinePath))
			{
				File.Copy(currentPath, baselinePath);
				Log.Info($"[CardEditor] Text snapshot baseline seeded ({lines.Count} lines) at {baselinePath}.");
				return;
			}

			CompareAgainstBaseline(lines, baselinePath);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Text snapshot audit failed: {ex}");
		}
	}

	private static List<string> RenderMatrix(CardModel host)
	{
		List<string> lines = new List<string>(CardEditorExtraEffects.Definitions.Count * 8);
		foreach (CardExtraEffectDefinition definition in CardEditorExtraEffects.Definitions)
		{
			foreach (CardExtraEffectTarget target in Enum.GetValues<CardExtraEffectTarget>())
			{
				string rendered;
				try
				{
					CardExtraEffect effect = new CardExtraEffect
					{
						Kind = definition.Kind,
						Target = target,
						Amount = definition.DefaultAmount,
					};
					rendered = CardEditorExtraEffects.TryFormatLineForAudit(host, effect, out string? line)
						? (line ?? string.Empty).Replace('\n', '↵')
						: "<no text>";
				}
				catch (Exception ex)
				{
					rendered = $"<format threw: {ex.GetType().Name}>";
				}

				lines.Add($"{definition.Kind}|{target}|{rendered}");
			}
		}

		lines.Sort(StringComparer.Ordinal);
		return lines;
	}

	private static void CompareAgainstBaseline(List<string> current, string baselinePath)
	{
		Dictionary<string, string> baselineByKey = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (string line in File.ReadAllLines(baselinePath))
		{
			int secondPipe = IndexOfSecondPipe(line);
			if (secondPipe > 0)
			{
				baselineByKey[line.Substring(0, secondPipe)] = NormalizeContextDependentText(line.Substring(secondPipe + 1));
			}
		}

		int changed = 0;
		int added = 0;
		List<string> examples = new List<string>();
		HashSet<string> seenKeys = new HashSet<string>(StringComparer.Ordinal);
		foreach (string line in current)
		{
			int secondPipe = IndexOfSecondPipe(line);
			if (secondPipe <= 0)
			{
				continue;
			}

			string key = line.Substring(0, secondPipe);
			string text = NormalizeContextDependentText(line.Substring(secondPipe + 1));
			seenKeys.Add(key);
			if (!baselineByKey.TryGetValue(key, out string? baselineText))
			{
				added++;
				if (examples.Count < MaxReportedDiffs)
				{
					examples.Add($"NEW {key}: \"{text}\"");
				}
				continue;
			}

			if (!string.Equals(baselineText, text, StringComparison.Ordinal))
			{
				changed++;
				if (examples.Count < MaxReportedDiffs)
				{
					examples.Add($"CHANGED {key}: \"{baselineText}\" -> \"{text}\"");
				}
			}
		}

		int removed = 0;
		foreach (string key in baselineByKey.Keys)
		{
			if (!seenKeys.Contains(key))
			{
				removed++;
				if (examples.Count < MaxReportedDiffs)
				{
					examples.Add($"REMOVED {key}");
				}
			}
		}

		if (changed == 0 && added == 0 && removed == 0)
		{
			Log.Info("[CardEditor] Text snapshot matches baseline (no wording drift).");
			return;
		}

		Log.Warn(
			$"[CardEditor] Text snapshot DIFFERS from baseline: {changed} changed, {added} new, {removed} removed. "
			+ "If intended, delete text_snapshot.baseline.txt to re-seed. Examples:\n  "
			+ string.Join("\n  ", examples));
	}

	private static int IndexOfSecondPipe(string line)
	{
		int first = line.IndexOf('|');
		return first < 0 ? -1 : line.IndexOf('|', first + 1);
	}

	private static string NormalizeContextDependentText(string text)
	{
		return EnergyIconPath.Replace(text ?? string.Empty, "character_energy_icon.png");
	}
}
