using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.addons.mega_text;

namespace SlayTheSpire2Mod.CardEditor;

// Engine-backed regression runner. It is not instantiated in normal builds unless the process
// explicitly opts in through CARD_EDITOR_ENGINE_SELF_TEST=1 or the command-line switch.
internal static class CardEditorEngineSelfTest
{
	private static bool _started;

	private static bool IsEnabled()
	{
		bool environmentEnabled = string.Equals(
			System.Environment.GetEnvironmentVariable("CARD_EDITOR_ENGINE_SELF_TEST"),
			"1",
			StringComparison.Ordinal);
		return environmentEnabled || OS.GetCmdlineArgs()
			.Concat(OS.GetCmdlineUserArgs())
			.Any(argument => string.Equals(
				argument,
				"--card-editor-engine-self-test",
				StringComparison.Ordinal));
	}

	internal static void TryAttach(NMainMenu mainMenu)
	{
		if (_started || !IsEnabled())
		{
			return;
		}

		_started = true;
		GD.Print("[CardEditor][EngineSelfTest] Starting real-engine UI checks.");
		CardEditorEngineSelfTestRunner.Run(mainMenu);
	}
}

internal static class CardEditorEngineSelfTestRunner
{
	private const string ReportPath = "user://card_editor/engine_ui_selftest_report.txt";
	private const int DescriptionSampleCount = 16;

	internal static async void Run(Node host)
	{
		StringBuilder report = new();
		bool passed = true;
		report.AppendLine("Card Editor Engine UI Self-Test");
		report.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

		try
		{
			await WaitFrames(host, 10);
			passed &= await TestMatchEnergyOptions(host, report);
			passed &= await TestPassiveStatusModifierUi(host, report);
			passed &= await TestLongestCardDescriptions(host, report);
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
		host.GetTree().Quit(passed ? 0 : 1);
	}

	private static async System.Threading.Tasks.Task<bool> TestMatchEnergyOptions(Node host, StringBuilder report)
	{
		CardModel preview = ModelDb.AllCards.First(card => card.Type != CardType.Quest).ToMutable();
		NCardEditorPopup popup = new();
		popup.InitializeAsEmbeddedEffectHost(preview);
		host.AddChild(popup);
		VBoxContainer effectUi = new() { Name = "EngineSelfTestEffectUi" };
		host.AddChild(effectUi);
		popup.BuildEmbeddedEffectsUi(effectUi);
		popup.LoadEmbeddedEffect(new CardExtraEffect { Kind = CardExtraEffectKind.CardCostsLess });
		await WaitFrames(host, 5);

		List<(string Path, int Index)> matches = new();
		foreach (OptionButton option in Descendants(effectUi).OfType<OptionButton>())
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
		pass &= await TestGrantParityControls(host, popup, report);

		popup.QueueFree();
		effectUi.QueueFree();
		await WaitFrames(host, 2);
		return pass;
	}

	private static async System.Threading.Tasks.Task<bool> TestGrantParityControls(
		Node host,
		NCardEditorPopup popup,
		StringBuilder report)
	{
		const string keywordName = "Engine Self-Test Keyword";
		string customOptionText = $"Custom: {keywordName}";
		IReadOnlyList<CardEditorCustomKeywordDefinition> originalKeywords = CardEditorDefinitionStore.GetKeywordDefinitions();
		IReadOnlyList<CardEditorCustomStatusDefinition> originalStatuses = CardEditorDefinitionStore.GetStatusDefinitions();
		bool previousPersistence = CardEditorDefinitionStore.PersistenceSuspended;
		bool autoActionPass = false;
		bool customOptionPass = false;
		bool customSerializationPass = false;
		bool customRemoveModePass = false;

		CardEditorDefinitionStore.PersistenceSuspended = true;
		try
		{
			popup.LoadEmbeddedEffect(new CardExtraEffect { Kind = CardExtraEffectKind.ConditionalAutoRunEffects });
			await WaitFrames(host, 5);
			object autoActionRow = GetLastEffectRow(popup);
			autoActionPass = GetRowProperty<Control>(autoActionRow, "GrantTickbox").Visible;

			CardEditorDefinitionStore.ReplaceDefinitions(
				originalKeywords.Concat([
					new CardEditorCustomKeywordDefinition
					{
						Id = CardEditorDefinitionStore.BuildKeywordId(keywordName)!,
						Name = keywordName,
						Description = "Gain 1 Block.",
						Effects = [new CardExtraEffect { Kind = CardExtraEffectKind.GainBlock, Amount = 1 }]
					}
				]),
				originalStatuses);

			popup.LoadEmbeddedEffect(
				new CardExtraEffect
				{
					Kind = CardExtraEffectKind.GrantKeywordToPile,
					CustomKeywordName = keywordName
				});
			await WaitFrames(host, 5);
			object keywordRow = GetLastEffectRow(popup);
			OptionButton customKeywordSelect = GetRowProperty<OptionButton>(keywordRow, "GrantedKeywordSelect");
			customOptionPass = customKeywordSelect.Selected >= 0
				&& customKeywordSelect.Selected < customKeywordSelect.ItemCount
				&& string.Equals(customKeywordSelect.GetItemText(customKeywordSelect.Selected), customOptionText, StringComparison.Ordinal);
			customSerializationPass = popup.ReadEmbeddedEffects()
				.Any(effect => effect.Kind == CardExtraEffectKind.GrantKeywordToPile
					&& string.Equals(effect.CustomKeywordName, keywordName, StringComparison.Ordinal));
			customRemoveModePass = !GetRowProperty<Control>(keywordRow, "GrantedKeywordRemoveTickbox").Visible;
		}
		finally
		{
			CardEditorDefinitionStore.ReplaceDefinitions(originalKeywords, originalStatuses);
			CardEditorDefinitionStore.PersistenceSuspended = previousPersistence;
		}

		bool pass = autoActionPass && customOptionPass && customSerializationPass && customRemoveModePass;
		report.AppendLine($"Grant and custom-keyword UI parity: {(pass ? "PASS" : "FAIL")}");
		report.AppendLine($"  Auto Action exposes Grant: {(autoActionPass ? "PASS" : "FAIL")}");
		report.AppendLine($"  Custom keyword appears selected: {(customOptionPass ? "PASS" : "FAIL")}");
		report.AppendLine($"  Custom keyword survives UI serialization: {(customSerializationPass ? "PASS" : "FAIL")}");
		report.AppendLine($"  Custom keyword hides vanilla-only removal mode: {(customRemoveModePass ? "PASS" : "FAIL")}");
		return pass;
	}

	private static object GetLastEffectRow(NCardEditorPopup popup)
	{
		FieldInfo rowsField = typeof(NCardEditorPopup).GetField("_extraEffectRows", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingFieldException(typeof(NCardEditorPopup).FullName, "_extraEffectRows");
		if (rowsField.GetValue(popup) is not IList rows || rows.Count == 0)
		{
			throw new InvalidOperationException("The embedded editor did not create an effect row.");
		}
		return rows[rows.Count - 1]!;
	}

	private static async System.Threading.Tasks.Task<bool> TestPassiveStatusModifierUi(Node host, StringBuilder report)
	{
		CardModel preview = ModelDb.AllCards.First(card => card.Type != CardType.Quest).ToMutable();
		NCardEditorPopup popup = NCardEditorPopup.Create(preview, () => { }, useModalContainer: false);
		host.AddChild(popup);
		await WaitFrames(host, 5);

		bool triggerAvailable = false;
		bool powerForced = false;
		bool bearerTargetForced = false;
		bool serialized = false;
		try
		{
			CardExtraEffect passiveStrength = new()
			{
				Kind = CardExtraEffectKind.LoseStrength,
				Amount = 2,
				AsPower = true,
				Trigger = CardExtraEffectTrigger.WhilePowerActive,
				Target = CardExtraEffectTarget.Self
			};
			MethodInfo openBehaviorEditor = typeof(NCardEditorPopup).GetMethod(
				"OpenDefinitionBehaviorEditor",
				BindingFlags.Instance | BindingFlags.NonPublic)
				?? throw new MissingMethodException(typeof(NCardEditorPopup).FullName, "OpenDefinitionBehaviorEditor");
			openBehaviorEditor.Invoke(
				popup,
				new object?[]
				{
					new[] { passiveStrength },
					(Action<List<CardExtraEffect>>)(_ => { }),
					"Status Behavior",
					null,
					true
				});
			await WaitFrames(host, 5);

			object row = GetLastEffectRow(popup);
			OptionButton triggerSelect = GetRowProperty<OptionButton>(row, "TriggerSelect");
			int triggerIndex = triggerSelect.GetItemIndex((int)CardExtraEffectTrigger.WhilePowerActive);
			triggerAvailable = triggerIndex >= 0
				&& !triggerSelect.GetPopup().IsItemDisabled(triggerIndex)
				&& triggerSelect.GetSelectedId() == (int)CardExtraEffectTrigger.WhilePowerActive;
			object powerTickbox = row.GetType().GetProperty("PowerTickbox", BindingFlags.Instance | BindingFlags.Public)?.GetValue(row)
				?? throw new MissingMemberException(row.GetType().FullName, "PowerTickbox");
			powerForced = powerTickbox.GetType().GetProperty("IsTicked", BindingFlags.Instance | BindingFlags.Public)?.GetValue(powerTickbox) is true;

			OptionButton targetSelect = GetRowProperty<OptionButton>(row, "TargetSelect");
			bearerTargetForced = targetSelect.ItemCount == 1
				&& string.Equals(targetSelect.GetItemText(0), CardEditorExtraEffects.TargetLabel(CardExtraEffectTarget.Self), StringComparison.Ordinal)
				&& targetSelect.Disabled;

			MethodInfo buildEffects = typeof(NCardEditorPopup).GetMethod(
				"BuildDefinitionBehaviorEffectsFromRows",
				BindingFlags.Instance | BindingFlags.NonPublic)
				?? throw new MissingMethodException(typeof(NCardEditorPopup).FullName, "BuildDefinitionBehaviorEffectsFromRows");
			List<CardExtraEffect> effects = (List<CardExtraEffect>)buildEffects.Invoke(popup, null)!;
			serialized = effects.Count == 1
				&& effects[0].Kind == CardExtraEffectKind.LoseStrength
				&& effects[0].AsPower
				&& effects[0].Trigger == CardExtraEffectTrigger.WhilePowerActive
				&& effects[0].Target == CardExtraEffectTarget.Self;
		}
		finally
		{
			popup.QueueFree();
			await WaitFrames(host, 2);
		}

		bool pass = triggerAvailable && powerForced && bearerTargetForced && serialized;
		report.AppendLine($"Passive custom-status modifier UI: {(pass ? "PASS" : "FAIL")}");
		report.AppendLine($"  While Power Active trigger available: {(triggerAvailable ? "PASS" : "FAIL")}");
		report.AppendLine($"  Power mode forced: {(powerForced ? "PASS" : "FAIL")}");
		report.AppendLine($"  Bearer/Self target forced: {(bearerTargetForced ? "PASS" : "FAIL")}");
		report.AppendLine($"  Passive trigger survives UI serialization: {(serialized ? "PASS" : "FAIL")}");
		return pass;
	}

	private static T GetRowProperty<T>(object row, string propertyName) where T : class
	{
		PropertyInfo property = row.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
			?? throw new MissingMemberException(row.GetType().FullName, propertyName);
		return property.GetValue(row) as T
			?? throw new InvalidCastException($"Effect row property {propertyName} is not {typeof(T).Name}.");
	}

	private static async System.Threading.Tasks.Task<bool> TestLongestCardDescriptions(Node host, StringBuilder report)
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
			host.AddChild(node);
			await WaitFrames(host, 3);
			node.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
			await WaitFrames(host, 3);

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
			await WaitFrames(host, 2);
		}

		return pass;
	}

	private static async System.Threading.Tasks.Task WaitFrames(Node host, int count)
	{
		for (int i = 0; i < count; i++)
		{
			await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
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
