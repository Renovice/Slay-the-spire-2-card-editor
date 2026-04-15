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
	private const float ExpandedOffsetLeft = -460f;
	private const float ExpandedOffsetTop = 20f;
	private const float ExpandedOffsetRight = -20f;
	private const float ExpandedOffsetBottom = 400f;
	private const float CollapsedOffsetLeft = -92f;
	private const float CollapsedOffsetTop = 20f;
	private const float CollapsedOffsetRight = -20f;
	private const float CollapsedOffsetBottom = 92f;

	private static Font? _headerFont;
	private static Font? _bodyFont;

	private NCardLibrary _library = null!;
	private OptionButton _presetSelect = null!;
	private LineEdit _presetNameField = null!;
	private CheckBox _startupCheckbox = null!;
	private CheckBox? _sortByCharacterCheckbox;
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
			BgColor = new Color(0.08f, 0.07f, 0.06f, 0.92f),
			BorderColor = StsColors.gold,
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomLeft = 10,
			CornerRadiusBottomRight = 10
		};
		AddThemeStyleboxOverride("panel", panelStyle);
	}

	private void BuildUi()
	{
		_headerFont ??= TryLoadFont(_headerFontPath);
		_bodyFont ??= TryLoadFont(_bodyFontPath);

		_margin = new MarginContainer();
		_margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_margin.AddThemeConstantOverride("margin_left", 14);
		_margin.AddThemeConstantOverride("margin_top", 14);
		_margin.AddThemeConstantOverride("margin_right", 14);
		_margin.AddThemeConstantOverride("margin_bottom", 14);
		AddChild(_margin);

		VBoxContainer root = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		root.AddThemeConstantOverride("separation", 10);
		_margin.AddChild(root);

		_headerRow = new HBoxContainer();
		_headerRow.AddThemeConstantOverride("separation", 10);
		_headerRow.Alignment = BoxContainer.AlignmentMode.Begin;
		root.AddChild(_headerRow);

		_titleLabel = new Label { Text = _isCreatorMode ? "Preset Creator" : "Preset Editor" };
		StyleSectionLabel(_titleLabel);
		_headerRow.AddChild(_titleLabel);

		_headerSpacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_headerRow.AddChild(_headerSpacer);

		_toggleButton = new Button
		{
			Text = "\u25B2",
			Flat = true,
			FocusMode = FocusModeEnum.None,
			CustomMinimumSize = new Vector2(40, 40),
			MouseFilter = MouseFilterEnum.Stop
		};
		StyleToggleButton(_toggleButton);
		_toggleButton.Pressed += ToggleCollapsed;
		_headerRow.AddChild(_toggleButton);

		_content = new VBoxContainer();
		_content.AddThemeConstantOverride("separation", 10);
		root.AddChild(_content);

		HBoxContainer loadRow = new HBoxContainer();
		loadRow.AddThemeConstantOverride("separation", 10);
		_content.AddChild(loadRow);

		_presetSelect = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 44) };
		StyleInput(_presetSelect);
		_presetSelect.ItemSelected += _ => OnPresetSelected();
		loadRow.AddChild(_presetSelect);

		Button load = new Button { Text = CardEditorLoc.T("button.load", "Load"), CustomMinimumSize = new Vector2(92, 44) };
		load.Pressed += OnLoadPressed;
		loadRow.AddChild(load);

		Button delete = new Button { Text = CardEditorLoc.T("button.delete", "Delete"), CustomMinimumSize = new Vector2(92, 44) };
		delete.Pressed += OnDeletePressed;
		loadRow.AddChild(delete);

		HBoxContainer startupRow = new HBoxContainer();
		startupRow.AddThemeConstantOverride("separation", 10);
		_content.AddChild(startupRow);

		_startupCheckbox = new CheckBox
		{
			Text = CardEditorLoc.T("presets.runAtStartup", "Run at startup"),
			FocusMode = FocusModeEnum.All,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Stop
		};
		StyleInput(_startupCheckbox);
		_startupCheckbox.Toggled += OnStartupToggled;
		startupRow.AddChild(_startupCheckbox);

		_slotCountRow = BuildSlotCountRow();
		_slotCountRow.Visible = _isCreatorMode;
		_content.AddChild(_slotCountRow);

		_sortByCharacterCheckbox = new CheckBox
		{
			Text = CardEditorLoc.T("presets.sortByCharacter", "Sort by Character"),
			FocusMode = FocusModeEnum.All,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Stop,
			Visible = _isCreatorMode
		};
		StyleInput(_sortByCharacterCheckbox);
		_sortByCharacterCheckbox.Toggled += OnSortByCharacterToggled;
		_content.AddChild(_sortByCharacterCheckbox);

		HBoxContainer saveRow = new HBoxContainer();
		saveRow.AddThemeConstantOverride("separation", 10);
		_content.AddChild(saveRow);

		_presetNameField = new NMegaLineEdit
		{
			PlaceholderText = CardEditorLoc.T("presets.namePlaceholder", "Preset name…"),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 44)
		};
		StyleInput(_presetNameField);
		saveRow.AddChild(_presetNameField);

		Button save = new Button { Text = CardEditorLoc.T("button.save", "Save"), CustomMinimumSize = new Vector2(92, 44) };
		save.Pressed += OnSavePressed;
		saveRow.AddChild(save);

		Button vanilla = new Button { Text = CardEditorLoc.T("button.revertToVanilla", "Revert to Vanilla"), CustomMinimumSize = new Vector2(0, 44) };
		vanilla.Pressed += OnVanillaPressed;
		_content.AddChild(vanilla);

		SetCollapsed(collapsed: true);
	}

	private HBoxContainer BuildSlotCountRow()
	{
		HBoxContainer row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		row.CustomMinimumSize = new Vector2(0, 44);

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
		spinner.CustomMinimumSize = new Vector2(160, 44);
		row.AddChild(spinner);

		_slotCountField = new NMegaLineEdit
		{
			Text = configured.ToString(CultureInfo.InvariantCulture),
			CustomMinimumSize = new Vector2(90, 44),
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
		arrows.CustomMinimumSize = new Vector2(44, 44);
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
			Flat = true,
			FocusMode = FocusModeEnum.None,
			CustomMinimumSize = new Vector2(44, 20),
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
		button.AddThemeConstantOverride("outline_size", 0);

		StyleBoxFlat normal = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0) };
		StyleBoxFlat hover = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.06f) };
		StyleBoxFlat pressed = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.10f) };
		StyleBoxFlat disabled = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0) };
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
			bool ok = CardEditorPresetStore.TrySavePreset(name, snapshot);
			Log.Info(ok
				? $"[CardEditor] Saved preset '{name}' ({snapshot.Count.ToString(CultureInfo.InvariantCulture)} cards)"
				: $"[CardEditor] Failed saving preset '{name}'");
		}

		RefreshPresetList();
		SelectPresetByName(name);
	}

	private void OnLoadPressed()
	{
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
		}
		else
		{
			HashSet<ModelId> previousIds = CardEditorOverrides.AllOverrides.Keys.ToHashSet();
			if (!CardEditorPresetStore.TryLoadPreset(name, out Dictionary<ModelId, CardOverride> loaded))
			{
				Log.Info($"[CardEditor] Failed loading preset '{name}'");
				return;
			}

			CardEditorOverrides.ReplaceAll(loaded);

			HashSet<ModelId> loadedIds = loaded.Keys.ToHashSet();
			HashSet<ModelId> idsToReset = previousIds;
			idsToReset.ExceptWith(loadedIds);
			CardEditorOverrides.ResetExistingCardsForIds(idsToReset);
			CardEditorOverrides.ApplyAllToExistingCards();

			CardEditorUiState.RefreshLibrary(_library);
			Log.Info($"[CardEditor] Loaded preset '{name}' ({loaded.Count.ToString(CultureInfo.InvariantCulture)} cards)");
		}
	}

	private void OnDeletePressed()
	{
		string name = GetSelectedPresetName() ?? (_presetNameField?.Text?.Trim() ?? string.Empty);
		if (string.IsNullOrWhiteSpace(name))
		{
			Log.Info("[CardEditor] Delete preset: no selection");
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
			CardEditorOverrides.ResetExistingCardsForIds(idsToReset);
			CardEditorUiState.RefreshLibrary(_library);
			Log.Info("[CardEditor] Reverted to vanilla (overrides cleared)");
		}
	}

	private void RefreshStartupCheckbox()
	{
		if (_startupCheckbox == null || !GodotObject.IsInstanceValid(_startupCheckbox))
		{
			return;
		}

		string? selected = GetSelectedPresetName();
		_startupCheckbox.Disabled = string.IsNullOrWhiteSpace(selected);
		_startupCheckbox.TooltipText = _startupCheckbox.Disabled
			? CardEditorLoc.T("tooltip.selectPresetFirst", "Select a preset first.")
			: string.Empty;

		string? startup = _isCreatorMode
			? CardEditorCreatorPresetStore.GetStartupPresetName()
			: CardEditorPresetStore.GetStartupPresetName();

		bool isStartup = !string.IsNullOrWhiteSpace(selected)
			&& !string.IsNullOrWhiteSpace(startup)
			&& string.Equals(selected, startup, StringComparison.OrdinalIgnoreCase);

		_startupCheckbox.SetPressedNoSignal(isStartup);
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
		_sortByCharacterCheckbox.SetPressedNoSignal(CardEditorCreatorPresetStore.GetSortByCharacter());
	}

	private void OnStartupToggled(bool enabled)
	{
		string? selected = GetSelectedPresetName();
		if (string.IsNullOrWhiteSpace(selected))
		{
			if (_startupCheckbox != null && GodotObject.IsInstanceValid(_startupCheckbox))
			{
				_startupCheckbox.SetPressedNoSignal(false);
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
		label.AddThemeFontSizeOverride("font_size", 26);
		label.AddThemeColorOverride("font_color", StsColors.cream);
		label.AddThemeColorOverride("font_outline_color", StsColors.transparentBlack);
		label.AddThemeConstantOverride("outline_size", 12);
	}

	private void StyleInput(Control control)
	{
		if (_bodyFont != null)
		{
			control.AddThemeFontOverride("font", _bodyFont);
		}
		control.AddThemeFontSizeOverride("font_size", 20);
		control.AddThemeColorOverride("font_color", StsColors.cream);
		control.AddThemeConstantOverride("outline_size", 0);
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
			int m = collapsed ? 8 : 14;
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
