using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace SlayTheSpire2Mod.CardEditor;

public partial class NCardEditorPresetPanel : PanelContainer
{
	private static readonly string _headerFontPath = "res://themes/kreon_bold_glyph_space_one.tres";
	private static readonly string _bodyFontPath = "res://themes/kreon_regular_glyph_space_one.tres";
	private static readonly string _actionButtonTexturePath = "res://images/packed/common_ui/settings_tab_selected.png";
	private static readonly string _actionButtonOutlineTexturePath = "res://images/packed/common_ui/settings_tab_stroke.png";
	private static readonly string _actionButtonFontPath = "res://themes/kreon_bold_glyph_space_two.tres";
	private static readonly string _actionButtonThemePath = "res://themes/settings_screen_tab.tres";
	private static readonly string _actionButtonOutlineMaterialPath = "res://themes/canvas_item_material_additive_shared.tres";
	private static readonly string _actionButtonShaderPath = "res://shaders/hsv.gdshader";
	private static readonly string _tickboxScenePath = "res://scenes/ui/tickbox.tscn";
	private const float ExpandedOffsetLeft = -520f;
	private const float ExpandedOffsetTop = 114f;
	private const float ExpandedOffsetRight = -24f;
	private const float ExpandedOffsetBottom = 618f;
	private const float CollapsedOffsetLeft = -92f;
	private const float CollapsedOffsetTop = 20f;
	private const float CollapsedOffsetRight = -20f;
	private const float CollapsedOffsetBottom = 92f;
	private static readonly Color LoadButtonTint = new(0.68f, 0.90f, 0.54f, 1f);
	private static readonly Color DeleteButtonTint = new(0.94f, 0.53f, 0.47f, 1f);
	private static readonly Color SaveButtonTint = new(0.68f, 0.90f, 0.54f, 1f);
	private static readonly Color VanillaButtonTint = new(0.98f, 0.84f, 0.50f, 1f);

	private static Font? _headerFont;
	private static Font? _bodyFont;
	private static Texture2D? _actionButtonTexture;
	private static Texture2D? _actionButtonOutlineTexture;
	private static Font? _actionButtonFont;
	private static Theme? _actionButtonTheme;
	private static Material? _actionButtonOutlineMaterial;
	private static Shader? _actionButtonShader;
	private static PackedScene? _tickboxScene;

	private NCardLibrary _library = null!;
	private OptionButton _presetSelect = null!;
	private LineEdit _presetNameField = null!;
	private PresetVanillaTickbox _startupCheckbox = null!;
	private PresetVanillaTickbox? _sortByCharacterCheckbox;
	private HBoxContainer? _slotCountRow;
	private LineEdit? _slotCountField;
	private MarginContainer _margin = null!;
	private HBoxContainer _headerRow = null!;
	private Label _titleLabel = null!;
	private Control _headerSpacer = null!;
	private VBoxContainer _content = null!;
	private Button _toggleButton = null!;
	private bool _isCollapsed;
	private bool _isCreatorMode;
	private float _scrollbarAlignOffsetX;
	private int _alignRetriesRemaining = 12;

	public static NCardEditorPresetPanel Create(NCardLibrary library, bool creatorMode)
	{
		NCardEditorPresetPanel panel = new NCardEditorPresetPanel();
		panel.Initialize(library);
		panel.SetCreatorMode(creatorMode);
		panel.BuildUi();
		panel.RefreshPresetList();
		return panel;
	}

	public override void _Ready()
	{
		Callable.From(RefreshAlignmentToScrollbar).CallDeferred();
		Connect(CanvasItem.SignalName.VisibilityChanged, Callable.From(OnVisibilityChanged));
	}

	public override void _Notification(int what)
	{
		base._Notification(what);
		if (what == NotificationResized && IsNodeReady())
		{
			RefreshAlignmentToScrollbar();
		}
	}

	private void OnVisibilityChanged()
	{
		if (!IsNodeReady() || !Visible)
		{
			return;
		}
		_alignRetriesRemaining = 12;
		Callable.From(RefreshAlignmentToScrollbar).CallDeferred();
	}

	private void Initialize(NCardLibrary library)
	{
		_library = library;
		Name = "CardEditorPresetPanel";
		ZIndex = 60;
		MouseFilter = MouseFilterEnum.Stop;

		AnchorLeft = 1f;
		AnchorTop = 0f;
		AnchorRight = 1f;
		AnchorBottom = 0f;
		GrowHorizontal = GrowDirection.Begin;
		OffsetLeft = ExpandedOffsetLeft;
		OffsetTop = ExpandedOffsetTop;
		OffsetRight = ExpandedOffsetRight;
		OffsetBottom = ExpandedOffsetBottom;

		StyleBoxFlat panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.035f, 0.04f, 0.045f, 0.88f),
			BorderColor = StsColors.gold,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8
		};
		AddThemeStyleboxOverride("panel", panelStyle);
	}

	private void BuildUi()
	{
		_headerFont ??= TryLoadFont(_headerFontPath);
		_bodyFont ??= TryLoadFont(_bodyFontPath);
		_actionButtonTexture ??= GD.Load<Texture2D>(_actionButtonTexturePath);
		_actionButtonOutlineTexture ??= GD.Load<Texture2D>(_actionButtonOutlineTexturePath);
		_actionButtonFont ??= TryLoadFont(_actionButtonFontPath);
		_actionButtonTheme ??= GD.Load<Theme>(_actionButtonThemePath);
		_actionButtonOutlineMaterial ??= GD.Load<Material>(_actionButtonOutlineMaterialPath);
		_actionButtonShader ??= GD.Load<Shader>(_actionButtonShaderPath);
		_tickboxScene ??= GD.Load<PackedScene>(_tickboxScenePath);

		_margin = new MarginContainer();
		_margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_margin.AddThemeConstantOverride("margin_left", 16);
		_margin.AddThemeConstantOverride("margin_top", 16);
		_margin.AddThemeConstantOverride("margin_right", 16);
		_margin.AddThemeConstantOverride("margin_bottom", 16);
		AddChild(_margin);

		VBoxContainer root = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		root.AddThemeConstantOverride("separation", 8);
		_margin.AddChild(root);

		_headerRow = new HBoxContainer();
		_headerRow.AddThemeConstantOverride("separation", 8);
		_headerRow.Alignment = BoxContainer.AlignmentMode.Begin;
		root.AddChild(_headerRow);

		_titleLabel = new Label { Text = _isCreatorMode ? "Preset Creator" : "Preset Editor" };
		StyleSectionLabel(_titleLabel);
		_headerRow.AddChild(_titleLabel);

		_headerSpacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_headerRow.AddChild(_headerSpacer);

		root.AddChild(CreateDivider());

		_content = new VBoxContainer();
		_content.AddThemeConstantOverride("separation", 10);
		root.AddChild(_content);

		_presetSelect = new OptionButton
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 50),
			FocusMode = FocusModeEnum.All,
			MouseFilter = MouseFilterEnum.Stop
		};
		StyleInput(_presetSelect);
		_presetSelect.ItemSelected += _ => OnPresetSelected();
		_content.AddChild(_presetSelect);

		HBoxContainer loadActions = new HBoxContainer();
		loadActions.AddThemeConstantOverride("separation", 10);
		_content.AddChild(loadActions);

		loadActions.AddChild(CreateActionButton(
			CardEditorLoc.T("button.load", "Load"),
			LoadButtonTint,
			OnLoadPressed,
			193f,
			54f,
			18));
		loadActions.AddChild(CreateActionButton(
			CardEditorLoc.T("button.delete", "Delete"),
			DeleteButtonTint,
			OnDeletePressed,
			193f,
			54f,
			18));

		_startupCheckbox = CreateTickbox(CardEditorLoc.T("presets.runAtStartup", "Run at startup"));
		_startupCheckbox.Toggled += OnStartupToggled;
		_content.AddChild(_startupCheckbox);

		_slotCountRow = BuildSlotCountRow();
		_slotCountRow.Visible = _isCreatorMode;
		_content.AddChild(_slotCountRow);

		_sortByCharacterCheckbox = CreateTickbox(CardEditorLoc.T("presets.sortByCharacter", "Sort by Character"));
		_sortByCharacterCheckbox.Toggled += OnSortByCharacterToggled;
		_sortByCharacterCheckbox.Visible = _isCreatorMode;
		_content.AddChild(_sortByCharacterCheckbox);

		_content.AddChild(CreateDivider());

		_presetNameField = new NMegaLineEdit
		{
			PlaceholderText = CardEditorLoc.T("presets.namePlaceholder", "Preset name…"),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 44)
		};
		StyleInput(_presetNameField);
		_presetNameField.PlaceholderText = CardEditorLoc.T("presets.namePlaceholder", "Preset name...");
		_presetNameField.CustomMinimumSize = new Vector2(0, 50);
		_content.AddChild(_presetNameField);

		HBoxContainer bottomActions = new HBoxContainer();
		bottomActions.AddThemeConstantOverride("separation", 10);
		_content.AddChild(bottomActions);

		bottomActions.AddChild(CreateActionButton(
			CardEditorLoc.T("button.save", "Save"),
			SaveButtonTint,
			OnSavePressed,
			118f,
			54f,
			18));

		CardEditorBaseDeckActionButton revertButton = CreateActionButton(
			CardEditorLoc.T("button.revertToVanilla", "Revert to Vanilla"),
			VanillaButtonTint,
			OnVanillaPressed,
			276f,
			54f,
			15);
		revertButton.TooltipText = CardEditorLoc.T("button.revertToVanilla", "Revert to Vanilla");
		bottomActions.AddChild(revertButton);

		SetCollapsed(collapsed: false);
	}

	private HBoxContainer BuildSlotCountRow()
	{
		HBoxContainer row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		row.CustomMinimumSize = new Vector2(0, 48);

		Label label = new Label
		{
			Text = CardEditorLoc.T("presets.maxCustomCards", "Max Custom Cards (requires restart)"),
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		StyleInput(label);
		label.TooltipText = CardEditorLoc.T(
			"presets.maxCustomCardsTooltip",
			"Sets how many custom card slots exist in Creator mode. Requires restart to apply.");
		row.AddChild(label);

		int configured = Math.Clamp(CardEditorCreatedCardsStore.ConfiguredSlotCount, 1, CardEditorCreatedCardsStore.MaxSlotCount);

		HBoxContainer spinner = new HBoxContainer();
		spinner.AddThemeConstantOverride("separation", 6);
		spinner.CustomMinimumSize = new Vector2(164, 48);
		row.AddChild(spinner);

		_slotCountField = new NMegaLineEdit
		{
			Text = configured.ToString(CultureInfo.InvariantCulture),
			CustomMinimumSize = new Vector2(98, 48),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			FocusMode = FocusModeEnum.All,
			MouseFilter = MouseFilterEnum.Stop
		};
		_slotCountField.TooltipText = label.TooltipText;
		StyleInput(_slotCountField);
		_slotCountField.TextSubmitted += _ => CommitSlotCountFromField();
		_slotCountField.FocusExited += CommitSlotCountFromField;
		spinner.AddChild(_slotCountField);

		VBoxContainer arrows = new VBoxContainer();
		arrows.AddThemeConstantOverride("separation", 4);
		arrows.CustomMinimumSize = new Vector2(50, 48);
		spinner.AddChild(arrows);

		Button up = CreateSpinnerButton("\u25B2");
		up.Pressed += () => AdjustSlotCount(+1);
		arrows.AddChild(up);

		Button down = CreateSpinnerButton("\u25BC");
		down.Pressed += () => AdjustSlotCount(-1);
		arrows.AddChild(down);

		return row;
	}

	private Button CreateSpinnerButton(string text)
	{
		Button button = new Button
		{
			Text = text,
			FocusMode = FocusModeEnum.None,
			CustomMinimumSize = new Vector2(50, 22),
			MouseFilter = MouseFilterEnum.Stop
		};
		StyleSpinnerButton(button);
		return button;
	}

	private void StyleSpinnerButton(Button button)
	{
		if (_bodyFont != null)
		{
			button.AddThemeFontOverride("font", _bodyFont);
		}
		button.AddThemeFontSizeOverride("font_size", 18);
		button.AddThemeColorOverride("font_color", StsColors.gold);
		button.AddThemeColorOverride("font_outline_color", StsColors.transparentBlack);
		button.AddThemeConstantOverride("outline_size", 8);

		StyleBoxFlat normal = CreateFieldStyle(new Color(0.40f, 0.33f, 0.14f, 1f));
		StyleBoxFlat hover = CreateFieldStyle(StsColors.gold);
		StyleBoxFlat pressed = CreateFieldStyle(new Color(0.3648f, 0.9104f, 0.96f, 0.752941f));
		StyleBoxFlat disabled = CreateFieldStyle(StsColors.gray);
		button.AddThemeStyleboxOverride("normal", normal);
		button.AddThemeStyleboxOverride("hover", hover);
		button.AddThemeStyleboxOverride("pressed", pressed);
		button.AddThemeStyleboxOverride("disabled", disabled);
	}

	private void AdjustSlotCount(int delta)
	{
		if (_slotCountField == null || !GodotObject.IsInstanceValid(_slotCountField))
		{
			return;
		}

		int current = ParseSlotCountOrFallback(_slotCountField.Text, CardEditorCreatedCardsStore.ConfiguredSlotCount);
		int next = Math.Clamp(current + delta, 1, CardEditorCreatedCardsStore.MaxSlotCount);
		_slotCountField.Text = next.ToString(CultureInfo.InvariantCulture);
		CommitSlotCount(next);
	}

	private void CommitSlotCountFromField()
	{
		if (_slotCountField == null || !GodotObject.IsInstanceValid(_slotCountField))
		{
			return;
		}

		int parsed = ParseSlotCountOrFallback(_slotCountField.Text, CardEditorCreatedCardsStore.ConfiguredSlotCount);
		int next = Math.Clamp(parsed, 1, CardEditorCreatedCardsStore.MaxSlotCount);
		_slotCountField.Text = next.ToString(CultureInfo.InvariantCulture);
		CommitSlotCount(next);
	}

	private static int ParseSlotCountOrFallback(string? text, int fallback)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return fallback;
		}

		return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
			? parsed
			: fallback;
	}

	private static void CommitSlotCount(int slotCount)
	{
		CardEditorCreatedCardsStore.SetSlotCountForNextRun(slotCount);
	}

	public void SetCreatorMode(bool creatorMode)
	{
		if (_isCreatorMode == creatorMode)
		{
			return;
		}
		_isCreatorMode = creatorMode;
		if (_titleLabel != null && GodotObject.IsInstanceValid(_titleLabel))
		{
			_titleLabel.Text = creatorMode ? "Preset Creator" : "Preset Editor";
		}

		if (IsNodeReady())
		{
			RefreshPresetList();
		}

		if (_slotCountRow != null && GodotObject.IsInstanceValid(_slotCountRow))
		{
			_slotCountRow.Visible = _isCreatorMode;
		}

		if (_sortByCharacterCheckbox != null && GodotObject.IsInstanceValid(_sortByCharacterCheckbox))
		{
			_sortByCharacterCheckbox.Visible = _isCreatorMode;
		}

		if (IsNodeReady() && !_isCollapsed)
		{
			ApplyOffsetsForState();
		}
	}

	public void RefreshPresetList()
	{
		if (_presetSelect == null || !GodotObject.IsInstanceValid(_presetSelect))
		{
			return;
		}

		string? previous = GetSelectedPresetName();
		_presetSelect.Clear();
		_presetSelect.AddItem(CardEditorLoc.T("presets.select", "Select preset…"));

		List<string> presets = _isCreatorMode
			? CardEditorCreatorPresetStore.ListPresetNames()
			: CardEditorPresetStore.ListPresetNames();
		foreach (string name in presets)
		{
			_presetSelect.AddItem(name);
		}

		if (!string.IsNullOrWhiteSpace(previous))
		{
			int index = presets.FindIndex(p => string.Equals(p, previous, StringComparison.OrdinalIgnoreCase));
			if (index >= 0)
			{
				_presetSelect.Select(index + 1);
			}
		}
		else
		{
			string? startup = _isCreatorMode
				? CardEditorCreatorPresetStore.GetStartupPresetName()
				: CardEditorPresetStore.GetStartupPresetName();

			if (!string.IsNullOrWhiteSpace(startup))
			{
				int index = presets.FindIndex(p => string.Equals(p, startup, StringComparison.OrdinalIgnoreCase));
				if (index >= 0)
				{
					_presetSelect.Select(index + 1);
				}
				else if (presets.Count > 0)
				{
					_presetSelect.Select(1);
				}
			}
			else if (presets.Count > 0)
			{
				_presetSelect.Select(1);
			}
		}

		RefreshStartupCheckbox();
		RefreshSortByCharacterCheckbox();

		if (_presetNameField != null && GodotObject.IsInstanceValid(_presetNameField) && string.IsNullOrWhiteSpace(_presetNameField.Text))
		{
			string? startup = _isCreatorMode
				? CardEditorCreatorPresetStore.GetStartupPresetName()
				: CardEditorPresetStore.GetStartupPresetName();
			if (!string.IsNullOrWhiteSpace(startup))
			{
				_presetNameField.Text = startup;
			}
		}
	}

	private void OnPresetSelected()
	{
		string? name = GetSelectedPresetName();
		if (!string.IsNullOrWhiteSpace(name) && _presetNameField != null && GodotObject.IsInstanceValid(_presetNameField))
		{
			_presetNameField.Text = name;
		}
		RefreshStartupCheckbox();
	}

	private void OnSavePressed()
	{
		string name = _presetNameField?.Text?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(name))
		{
			Log.Info("[CardEditor] Save preset: empty name");
			return;
		}

		if (_isCreatorMode)
		{
			Dictionary<ModelId, CardEditorCreatedCardDefinition> snapshot = CardEditorCreatedCardsStore.ExportSnapshot();
			bool ok = CardEditorCreatorPresetStore.TrySavePreset(name, snapshot);
			Log.Info(ok
				? $"[CardEditor] Saved creator preset '{name}' ({snapshot.Count.ToString(CultureInfo.InvariantCulture)} cards)"
				: $"[CardEditor] Failed saving creator preset '{name}'");
		}
		else
		{
			Dictionary<ModelId, CardOverride> snapshot = CardEditorOverrides.ExportSnapshot();
			Dictionary<ModelId, List<ModelId>> baseDeckSnapshot = CardEditorBaseDeckStore.ExportSnapshot();
			bool ok = CardEditorPresetStore.TrySavePreset(name, snapshot, baseDeckSnapshot);
			Log.Info(ok
				? $"[CardEditor] Saved preset '{name}' ({snapshot.Count.ToString(CultureInfo.InvariantCulture)} cards, {baseDeckSnapshot.Count.ToString(CultureInfo.InvariantCulture)} base decks)"
				: $"[CardEditor] Failed saving preset '{name}'");
		}

		RefreshPresetList();
		SelectPresetByName(name);
	}

	private void OnLoadPressed()
	{
		if (!CardEditorMultiplayerSync.CanEditSharedState())
		{
			Log.Info("[CardEditor][MultiplayerSync] Blocked preset load because shared-state editing is host-controlled.");
			return;
		}

		string name = GetSelectedPresetName() ?? (_presetNameField?.Text?.Trim() ?? string.Empty);
		if (string.IsNullOrWhiteSpace(name))
		{
			Log.Info("[CardEditor] Load preset: no selection");
			return;
		}

		if (_isCreatorMode)
		{
			if (!CardEditorCreatorPresetStore.TryLoadPreset(name, out Dictionary<ModelId, CardEditorCreatedCardDefinition> loaded))
			{
				Log.Info($"[CardEditor] Failed loading creator preset '{name}'");
				return;
			}

			CardEditorCreatedCardsStore.ImportSnapshot(loaded);
			CardEditorUiState.RefreshLibrary(_library);
			Log.Info($"[CardEditor] Loaded creator preset '{name}' ({loaded.Count.ToString(CultureInfo.InvariantCulture)} cards)");
			CardEditorMultiplayerSync.NotifySharedStateMutatedLocally();
		}
		else
		{
			HashSet<ModelId> previousIds = CardEditorOverrides.AllOverrides.Keys.ToHashSet();
			if (!CardEditorPresetStore.TryLoadPreset(name, out Dictionary<ModelId, CardOverride> loaded, out Dictionary<ModelId, List<ModelId>> loadedBaseDecks))
			{
				Log.Info($"[CardEditor] Failed loading preset '{name}'");
				return;
			}

			CardEditorOverrides.ReplaceAll(loaded);
			CardEditorBaseDeckStore.ImportSnapshot(loadedBaseDecks);
			CardEditorBaseDeckUiState.ClearTransientState();
			CardEditorBaseDeckUiState.EnsureValidCharacter();

			HashSet<ModelId> loadedIds = loaded.Keys.ToHashSet();
			HashSet<ModelId> idsToReset = previousIds;
			idsToReset.ExceptWith(loadedIds);
			CardEditorOverrides.ResetExistingCardsForIds(idsToReset);
			CardEditorOverrides.ApplyAllToExistingCards();

			CardEditorUiState.RefreshLibrary(_library);
			CardEditorBaseDeckUiState.RefreshAll();
			Log.Info($"[CardEditor] Loaded preset '{name}' ({loaded.Count.ToString(CultureInfo.InvariantCulture)} cards, {loadedBaseDecks.Count.ToString(CultureInfo.InvariantCulture)} base decks)");
			CardEditorMultiplayerSync.NotifySharedStateMutatedLocally();
		}
	}

	private async void OnDeletePressed()
	{
		string name = GetSelectedPresetName() ?? (_presetNameField?.Text?.Trim() ?? string.Empty);
		if (string.IsNullOrWhiteSpace(name))
		{
			Log.Info("[CardEditor] Delete preset: no selection");
			return;
		}

		Log.Info($"[CardEditor][ConfirmPopup] Delete preset requested for '{name}'");
		bool confirmed = await CardEditorConfirmPopup.ShowConfirmation(
			"Delete Preset?",
			$"Delete preset \"{name}\"?\n\nThis cannot be undone.");
		Log.Info($"[CardEditor][ConfirmPopup] Delete preset confirmation result={confirmed} preset='{name}'");
		if (!confirmed)
		{
			Log.Info($"[CardEditor] Delete preset cancelled for '{name}'");
			return;
		}

		bool ok = _isCreatorMode
			? CardEditorCreatorPresetStore.TryDeletePreset(name)
			: CardEditorPresetStore.TryDeletePreset(name);
		Log.Info(ok
			? $"[CardEditor] Deleted preset '{name}'"
			: $"[CardEditor] Failed deleting preset '{name}'");

		RefreshPresetList();
		if (_presetNameField != null && GodotObject.IsInstanceValid(_presetNameField))
		{
			_presetNameField.Text = string.Empty;
		}
		if (_presetSelect != null && GodotObject.IsInstanceValid(_presetSelect))
		{
			_presetSelect.Select(0);
		}
	}

	private void OnVanillaPressed()
	{
		if (!CardEditorMultiplayerSync.CanEditSharedState())
		{
			Log.Info("[CardEditor][MultiplayerSync] Blocked revert-to-vanilla because shared-state editing is host-controlled.");
			return;
		}

		if (_isCreatorMode)
		{
			CardEditorCreatedCardsStore.ResetAllToDefaults();
			CardEditorUiState.RefreshLibrary(_library);
			Log.Info("[CardEditor] Reverted creator cards to defaults");
		}
		else
		{
			HashSet<ModelId> idsToReset = CardEditorOverrides.AllOverrides.Keys.ToHashSet();
			CardEditorOverrides.ClearAll();
			CardEditorBaseDeckStore.ClearAllOverrides();
			CardEditorBaseDeckUiState.ClearTransientState();
			CardEditorOverrides.ResetExistingCardsForIds(idsToReset);
			CardEditorUiState.RefreshLibrary(_library);
			CardEditorBaseDeckUiState.RefreshAll();
			Log.Info("[CardEditor] Reverted to vanilla (overrides cleared)");
		}

		CardEditorMultiplayerSync.NotifySharedStateMutatedLocally();
	}

	private void RefreshStartupCheckbox()
	{
		if (_startupCheckbox == null || !GodotObject.IsInstanceValid(_startupCheckbox))
		{
			return;
		}

		string? selected = GetSelectedPresetName();
		bool hasSelection = !string.IsNullOrWhiteSpace(selected);
		_startupCheckbox.SetInteractable(
			hasSelection,
			hasSelection ? string.Empty : CardEditorLoc.T("tooltip.selectPresetFirst", "Select a preset first."));

		string? startup = _isCreatorMode
			? CardEditorCreatorPresetStore.GetStartupPresetName()
			: CardEditorPresetStore.GetStartupPresetName();

		bool isStartup = !string.IsNullOrWhiteSpace(selected)
			&& !string.IsNullOrWhiteSpace(startup)
			&& string.Equals(selected, startup, StringComparison.OrdinalIgnoreCase);

		_startupCheckbox.SetTickedSilent(isStartup);
	}

	private void OnSortByCharacterToggled(bool enabled)
	{
		CardEditorCreatorPresetStore.SetSortByCharacter(enabled);
		if (_isCreatorMode)
		{
			CardEditorUiState.RefreshLibrary(_library);
		}
	}

	private void RefreshSortByCharacterCheckbox()
	{
		if (_sortByCharacterCheckbox == null || !GodotObject.IsInstanceValid(_sortByCharacterCheckbox))
		{
			return;
		}
		_sortByCharacterCheckbox.SetTickedSilent(CardEditorCreatorPresetStore.GetSortByCharacter());
	}

	private void OnStartupToggled(bool enabled)
	{
		string? selected = GetSelectedPresetName();
		if (string.IsNullOrWhiteSpace(selected))
		{
			if (_startupCheckbox != null && GodotObject.IsInstanceValid(_startupCheckbox))
			{
				_startupCheckbox.SetTickedSilent(false);
			}
			return;
		}

		if (_isCreatorMode)
		{
			CardEditorCreatorPresetStore.SetStartupPresetName(enabled ? selected : null);
		}
		else
		{
			CardEditorPresetStore.SetStartupPresetName(enabled ? selected : null);
		}
	}

	private void RefreshAlignmentToScrollbar()
	{
		try
		{
			NCardLibraryGrid? grid = _library?.GetNodeOrNull<NCardLibraryGrid>("%CardGrid");
			Control? scrollbar = grid?.GetNodeOrNull<Control>("Scrollbar");
			if (scrollbar == null)
			{
				ApplyOffsetsForState();
				if (_alignRetriesRemaining-- > 0)
				{
					Callable.From(RefreshAlignmentToScrollbar).CallDeferred();
				}
				return;
			}

			Control? handle = scrollbar.GetNodeOrNull<Control>("%Handle") ?? scrollbar.GetNodeOrNull<Control>("Handle");
			Rect2 bar = (handle ?? scrollbar).GetGlobalRect();
			float barCenterX = bar.Position.X + bar.Size.X * 0.5f;

			Control? parentControl = GetParent() as Control;
			if (parentControl == null)
			{
				return;
			}

			Rect2 parentRect = parentControl.GetGlobalRect();
			if (parentRect.Size.X <= 0f || parentRect.Size.Y <= 0f)
			{
				ApplyOffsetsForState();
				if (_alignRetriesRemaining-- > 0)
				{
					Callable.From(RefreshAlignmentToScrollbar).CallDeferred();
				}
				return;
			}
			float parentRightX = parentRect.Position.X + parentRect.Size.X;
			float collapsedCenterOffset = (CollapsedOffsetLeft + CollapsedOffsetRight) * 0.5f;
			float desiredAlignOffset = barCenterX - (parentRightX + collapsedCenterOffset);

			float maxRightShift = -CollapsedOffsetRight;
			float newOffset = Mathf.Clamp(desiredAlignOffset, -600f, maxRightShift);
			if (Mathf.Abs(newOffset - _scrollbarAlignOffsetX) > 0.5f)
			{
				_scrollbarAlignOffsetX = newOffset;
				ApplyOffsetsForState();
			}
		}
		catch
		{
		}
	}

	private string? GetSelectedPresetName()
	{
		if (_presetSelect == null || !GodotObject.IsInstanceValid(_presetSelect))
		{
			return null;
		}
		int index = _presetSelect.Selected;
		if (index <= 0)
		{
			return null;
		}
		return _presetSelect.GetItemText(index);
	}

	private void SelectPresetByName(string name)
	{
		if (_presetSelect == null || !GodotObject.IsInstanceValid(_presetSelect))
		{
			return;
		}
		for (int i = 1; i < _presetSelect.ItemCount; i++)
		{
			if (string.Equals(_presetSelect.GetItemText(i), name, StringComparison.OrdinalIgnoreCase))
			{
				_presetSelect.Select(i);
				return;
			}
		}
	}

	private static Font? TryLoadFont(string path)
	{
		try
		{
			return GD.Load<Font>(path);
		}
		catch
		{
			return null;
		}
	}

	private void StyleSectionLabel(Label label)
	{
		if (_headerFont != null)
		{
			label.AddThemeFontOverride("font", _headerFont);
		}
		label.AddThemeFontSizeOverride("font_size", 24);
		label.AddThemeColorOverride("font_color", StsColors.cream);
		label.AddThemeColorOverride("font_outline_color", StsColors.transparentBlack);
		label.AddThemeConstantOverride("outline_size", 10);
	}

	private void StyleInput(Control control)
	{
		if (_bodyFont != null)
		{
			control.AddThemeFontOverride("font", _bodyFont);
		}
		control.AddThemeFontSizeOverride("font_size", 20);
		control.AddThemeColorOverride("font_color", StsColors.cream);
		control.AddThemeColorOverride("font_outline_color", StsColors.transparentBlack);
		control.AddThemeConstantOverride("outline_size", 8);

		if (control is Label label)
		{
			label.AddThemeColorOverride("font_color", StsColors.cream);
			return;
		}

		StyleBoxFlat normal = CreateFieldStyle(new Color(0.20f, 0.23f, 0.26f, 1f));
		StyleBoxFlat hover = CreateFieldStyle(new Color(0.34f, 0.38f, 0.42f, 1f));
		StyleBoxFlat focus = CreateFieldStyle(StsColors.gold);

		control.AddThemeStyleboxOverride("normal", normal);
		control.AddThemeStyleboxOverride("hover", hover);
		control.AddThemeStyleboxOverride("focus", focus);

		if (control is LineEdit lineEdit)
		{
			lineEdit.AddThemeStyleboxOverride("read_only", normal.Duplicate() as StyleBoxFlat ?? normal);
			lineEdit.AddThemeColorOverride("font_placeholder_color", StsColors.halfTransparentCream);
			lineEdit.CaretBlink = true;
		}
	}

	private static StyleBoxFlat CreateFieldStyle(Color borderColor)
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.055f, 0.065f, 0.075f, 0.95f),
			BorderColor = borderColor,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 5,
			CornerRadiusTopRight = 5,
			CornerRadiusBottomLeft = 5,
			CornerRadiusBottomRight = 5,
			ContentMarginLeft = 8,
			ContentMarginTop = 4,
			ContentMarginRight = 8,
			ContentMarginBottom = 4
		};
	}

	private Control CreateDivider()
	{
		return new ColorRect
		{
			Color = new Color(0.20f, 0.23f, 0.26f, 0.95f),
			CustomMinimumSize = new Vector2(0f, 1f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
	}

	private CardEditorBaseDeckActionButton CreateActionButton(string text, Color tint, Action onTriggered, float width, float height, int fontSize)
	{
		CardEditorBaseDeckActionButton button = new CardEditorBaseDeckActionButton();
		button.Initialize(text, tint, _actionButtonTexture!, _actionButtonOutlineTexture!, _actionButtonFont, _actionButtonTheme, _actionButtonOutlineMaterial, _actionButtonShader);
		button.SetButtonSize(width, height);
		button.SetTextSize(fontSize);
		button.SetTextOffsets(0f, 10f, 0f, 0f);
		button.Triggered += onTriggered;
		return button;
	}

	private PresetVanillaTickbox CreateTickbox(string text)
	{
		return new PresetVanillaTickbox(_tickboxScene!, _bodyFont, text);
	}

	private void StyleToggleButton(Button button)
	{
		if (_headerFont != null)
		{
			button.AddThemeFontOverride("font", _headerFont);
		}
		button.AddThemeFontSizeOverride("font_size", 22);
		button.AddThemeColorOverride("font_color", StsColors.gold);
		button.AddThemeColorOverride("font_outline_color", StsColors.transparentBlack);
		button.AddThemeConstantOverride("outline_size", 10);

		StyleBoxFlat normal = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0) };
		StyleBoxFlat hover = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.06f) };
		StyleBoxFlat pressed = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.10f) };
		StyleBoxFlat disabled = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0) };
		button.AddThemeStyleboxOverride("normal", normal);
		button.AddThemeStyleboxOverride("hover", hover);
		button.AddThemeStyleboxOverride("pressed", pressed);
		button.AddThemeStyleboxOverride("disabled", disabled);
	}

	private void ToggleCollapsed()
	{
		SetCollapsed(!_isCollapsed);
	}

	private void SetCollapsed(bool collapsed)
	{
		_isCollapsed = collapsed;

		if (_content != null && GodotObject.IsInstanceValid(_content))
		{
			_content.Visible = !collapsed;
		}
		if (_titleLabel != null && GodotObject.IsInstanceValid(_titleLabel))
		{
			_titleLabel.Visible = !collapsed;
		}
		if (_headerSpacer != null && GodotObject.IsInstanceValid(_headerSpacer))
		{
			_headerSpacer.Visible = !collapsed;
		}
		if (_headerRow != null && GodotObject.IsInstanceValid(_headerRow))
		{
			_headerRow.Alignment = collapsed ? BoxContainer.AlignmentMode.Center : BoxContainer.AlignmentMode.Begin;
		}
		if (_toggleButton != null && GodotObject.IsInstanceValid(_toggleButton))
		{
			_toggleButton.Text = collapsed ? "\u25BC" : "\u25B2";
			_toggleButton.TooltipText = collapsed
				? CardEditorLoc.T("tooltip.showPresets", "Show Presets")
				: CardEditorLoc.T("tooltip.hidePresets", "Hide Presets");
		}

		if (_margin != null && GodotObject.IsInstanceValid(_margin))
		{
			int m = collapsed ? 8 : 16;
			_margin.AddThemeConstantOverride("margin_left", m);
			_margin.AddThemeConstantOverride("margin_top", m);
			_margin.AddThemeConstantOverride("margin_right", m);
			_margin.AddThemeConstantOverride("margin_bottom", m);
		}

		if (collapsed)
		{
			ApplyOffsetsForState();
		}
		else
		{
			ApplyOffsetsForState();
		}

		_alignRetriesRemaining = 12;
		Callable.From(RefreshAlignmentToScrollbar).CallDeferred();
	}

	private void ApplyOffsetsForState()
	{
		if (_isCollapsed)
		{
			OffsetLeft = CollapsedOffsetLeft + _scrollbarAlignOffsetX;
			OffsetTop = CollapsedOffsetTop;
			OffsetRight = CollapsedOffsetRight + _scrollbarAlignOffsetX;
			OffsetBottom = CollapsedOffsetBottom;
		}
		else
		{
			OffsetLeft = ExpandedOffsetLeft + _scrollbarAlignOffsetX;
			OffsetTop = ExpandedOffsetTop;
			OffsetRight = ExpandedOffsetRight + _scrollbarAlignOffsetX;
			OffsetBottom = ExpandedOffsetBottom;
		}
	}
}

internal sealed class PresetVanillaTickbox : HBoxContainer
{
	private readonly Control _tickedImage;
	private readonly Control _notTickedImage;
	private bool _interactive = true;

	public bool IsTicked { get; private set; }

	public event Action<bool>? Toggled;

	public PresetVanillaTickbox(PackedScene tickboxScene, Font? bodyFont, string text)
	{
		MouseFilter = MouseFilterEnum.Stop;
		FocusMode = FocusModeEnum.None;
		SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		CustomMinimumSize = new Vector2(0f, 42f);
		AddThemeConstantOverride("separation", 8);

		Control tickboxVisuals = tickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
		tickboxVisuals.MouseFilter = MouseFilterEnum.Ignore;
		tickboxVisuals.CustomMinimumSize = new Vector2(48f, 48f);
		tickboxVisuals.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
		tickboxVisuals.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
		tickboxVisuals.Scale = Vector2.One * 0.72f;
		tickboxVisuals.PivotOffset = new Vector2(24f, 24f);

		Label label = new Label
		{
			Text = text,
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore
		};
		if (bodyFont != null)
		{
			label.AddThemeFontOverride("font", bodyFont);
		}
		label.AddThemeFontSizeOverride("font_size", 20);
		label.AddThemeColorOverride("font_color", StsColors.cream);
		label.AddThemeColorOverride("font_outline_color", StsColors.transparentBlack);
		label.AddThemeConstantOverride("outline_size", 8);

		AddChild(tickboxVisuals);
		AddChild(label);

		_tickedImage = tickboxVisuals.GetNode<Control>("Ticked");
		_notTickedImage = tickboxVisuals.GetNode<Control>("NotTicked");
		SetTicked(false, notify: false);

		GuiInput += OnGuiInput;
	}

	public void SetTickedSilent(bool value)
	{
		SetTicked(value, notify: false);
	}

	public void SetInteractable(bool interactive, string tooltipText)
	{
		_interactive = interactive;
		TooltipText = tooltipText;
		MouseFilter = interactive ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
		SelfModulate = interactive ? Colors.White : StsColors.gray;
	}

	private void OnGuiInput(InputEvent inputEvent)
	{
		if (!_interactive)
		{
			return;
		}

		if (inputEvent is InputEventMouseButton mouseButton
			&& mouseButton.ButtonIndex == MouseButton.Left
			&& mouseButton.Pressed)
		{
			SetTicked(!IsTicked, notify: true);
			AcceptEvent();
		}
	}

	private void SetTicked(bool value, bool notify)
	{
		IsTicked = value;
		_tickedImage.Visible = value;
		_notTickedImage.Visible = !value;
		if (notify)
		{
			Toggled?.Invoke(value);
		}
	}
}
