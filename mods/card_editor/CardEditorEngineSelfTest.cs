using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.addons.mega_text;

namespace SlayTheSpire2Mod.CardEditor;

// Engine-backed regression runner. It is not patched or instantiated in normal builds unless the
// process explicitly opts in through CARD_EDITOR_ENGINE_SELF_TEST=1.
[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class CardEditorEngineSelfTestMainMenuPatch
{
	private static bool Prepare()
	{
		bool environmentEnabled = string.Equals(
			System.Environment.GetEnvironmentVariable("CARD_EDITOR_ENGINE_SELF_TEST"),
			"1",
			StringComparison.Ordinal);
		return environmentEnabled || OS.GetCmdlineArgs().Any(argument =>
			string.Equals(argument, "--card-editor-engine-self-test", StringComparison.Ordinal));
	}

	private static void Postfix(NMainMenu __instance)
	{
		if (__instance.GetNodeOrNull<CardEditorEngineSelfTestRunner>("CardEditorEngineSelfTestRunner") != null)
		{
			return;
		}

		__instance.AddChild(new CardEditorEngineSelfTestRunner
		{
			Name = "CardEditorEngineSelfTestRunner"
		});
	}
}

internal partial class CardEditorEngineSelfTestRunner : Node
{
	private const string ReportPath = "user://card_editor/engine_ui_selftest_report.txt";
	private const int DescriptionSampleCount = 16;

	public override async void _Ready()
	{
		StringBuilder report = new();
		bool passed = true;
		report.AppendLine("Card Editor Engine UI Self-Test");
		report.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

		try
		{
			await WaitFrames(10);
			passed &= await TestMatchEnergyOptions(report);
			passed &= await TestLongestCardDescriptions(report);
		}
		catch (Exception ex)
		{
			passed = false;
			report.AppendLine("UNHANDLED: FAIL");
			report.AppendLine(ex.ToString());
		}

		report.AppendLine();
		report.AppendLine($"RESULT: {(passed ? "PASS" : "FAIL")}");
		string globalPath = ProjectSettings.GlobalizePath(ReportPath);
		Directory.CreateDirectory(Path.GetDirectoryName(globalPath)!);
		File.WriteAllText(globalPath, report.ToString());
		GD.Print($"[CardEditor][EngineSelfTest] {(passed ? "PASS" : "FAIL")} report={globalPath}");
		GetTree().Quit(passed ? 0 : 1);
	}

	private async System.Threading.Tasks.Task<bool> TestMatchEnergyOptions(StringBuilder report)
	{
		CardModel preview = ModelDb.AllCards.First(card => card.Type != CardType.Quest).ToMutable();
		NCardEditorPopup popup = NCardEditorPopup.Create(preview, static () => { }, useModalContainer: false);
		AddChild(popup);
		await WaitFrames(5);

		List<(string Path, int Index)> matches = new();
		foreach (OptionButton option in Descendants(popup).OfType<OptionButton>())
		{
			for (int i = 0; i < option.ItemCount; i++)
			{
				if (string.Equals(option.GetItemText(i), "Matching Cards (Energy)", StringComparison.Ordinal))
				{
					matches.Add((option.GetPath().ToString(), i));
				}
			}
		}

		bool pass = matches.Count == 1;
		report.AppendLine($"Match Energy option uniqueness: {(pass ? "PASS" : "FAIL")}");
		report.AppendLine($"  Visible option occurrences: {matches.Count}");
		foreach ((string path, int index) in matches)
		{
			report.AppendLine($"  {path} item={index}");
		}

		popup.QueueFree();
		await WaitFrames(2);
		return pass;
	}

	private async System.Threading.Tasks.Task<bool> TestLongestCardDescriptions(StringBuilder report)
	{
		List<(CardModel Card, string Description)> candidates = new();
		foreach (CardModel canonical in ModelDb.AllCards)
		{
			try
			{
				CardModel card = canonical.ToMutable();
				string description = card.GetDescriptionForPile(PileType.None);
				if (!string.IsNullOrWhiteSpace(description))
				{
					candidates.Add((card, description));
				}
			}
			catch
			{
				// Some context-only cards cannot produce a menu preview without a run owner.
			}
		}

		List<(CardModel Card, string Description)> samples = candidates
			.OrderByDescending(item => item.Description.Length)
			.Take(DescriptionSampleCount)
			.ToList();
		bool pass = samples.Count > 0;
		report.AppendLine($"Longest current card descriptions fit NCard bounds: {(pass ? "PASS" : "FAIL")}");
		report.AppendLine($"  Candidates: {candidates.Count}; rendered samples: {samples.Count}");

		FieldInfo descriptionField = typeof(NCard).GetField("_descriptionLabel", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingFieldException(typeof(NCard).FullName, "_descriptionLabel");
		foreach ((CardModel card, string description) in samples)
		{
			NCard node = NCard.Create(card) ?? throw new InvalidOperationException($"NCard.Create returned null for {card.Id}");
			AddChild(node);
			await WaitFrames(3);
			node.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
			await WaitFrames(3);

			MegaRichTextLabel label = (MegaRichTextLabel)descriptionField.GetValue(node)!;
			float contentHeight = label.GetContentHeight();
			float contentWidth = label.GetContentWidth();
			Vector2 bounds = label.Size;
			bool fits = contentHeight <= bounds.Y + 1f && contentWidth <= bounds.X + 1f;
			pass &= fits;
			int fontSize = label.GetThemeFontSize("normal_font_size", "RichTextLabel");
			report.AppendLine(string.Create(
				CultureInfo.InvariantCulture,
				$"  {(fits ? "PASS" : "FAIL")} {card.Id} chars={description.Length} font={fontSize} content=({contentWidth:0.##},{contentHeight:0.##}) bounds=({bounds.X:0.##},{bounds.Y:0.##})"));

			node.QueueFree();
			await WaitFrames(2);
		}

		return pass;
	}

	private async System.Threading.Tasks.Task WaitFrames(int count)
	{
		for (int i = 0; i < count; i++)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
	}

	private static IEnumerable<Node> Descendants(Node root)
	{
		foreach (Node child in root.GetChildren())
		{
			yield return child;
			foreach (Node descendant in Descendants(child))
			{
				yield return descendant;
			}
		}
	}
}
