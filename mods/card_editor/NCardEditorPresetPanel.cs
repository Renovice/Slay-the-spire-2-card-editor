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
	private static readonly string _popupShellTexturePath = "res://images/ui/reward_screen/reward_panel.png";
	private static readonly Vector2 _slotCountSpinButtonMinSize = new(34f, 20f);
	private static readonly Vector2 _slotCountSpinContainerMinSize = new(34f, 44f);
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
	private static Texture2D? _popupShellTexture;

	private NCardLibrary _library = null!;
	private OptionButton _presetSelect = null!;
	private LineEdit _presetNameField = null!;
	private PresetVanillaTickbox _startupCheckbox = null!;
	private PresetVanillaTickbox? _sortByCharacterCheckbox;
	private HBoxContainer? _slotCountRow;
	private LineEdit? _slotCountField;
	private MarginContainer _margin = null!;
	private Control? _popupShell;
	private HBoxContainer _headerRow = null!;
	private Label _titleLabel = null!;
	private Control _headerSpacer = null!;
	private VBoxContainer _content = null!;
	private MarginContainer _presetSelectWrapper = null!;
	private MarginContainer _topActionsWrapper = null!;
	private MarginContainer _loadButtonWrapper = null!;
	private MarginContainer _deleteButtonWrapper = null!;
	private MarginContainer _startupCheckboxWrapper = null!;
	private MarginContainer? _slotCountRowWrapper;
	private MarginContainer? _sortByCharacterCheckboxWrapper;
	private MarginContainer _presetNameWrapper = null!;
	private MarginContainer _bottomActionsWrapper = null!;
	private MarginContainer _saveButtonWrapper = null!;
	private MarginContainer _revertButtonWrapper = null!;
	private MarginContainer? _slotCountSpinnerWrapper;
	private MarginContainer? _slotCountFieldWrapper;
	private MarginContainer? _slotCountArrowsWrapper;
	private HBoxContainer _topActions = null!;
	private HBoxContainer _bottomActions = null!;
	private HBoxContainer? _slotCountSpinner;
	private VBoxContainer? _slotCountArrows;
	private CardEditorBaseDeckActionButton _loadButton = null!;
	private CardEditorBaseDeckActionButton _deleteButton = null!;
	private CardEditorBaseDeckActionButton _saveButton = null!;
	private CardEditorBaseDeckActionButton _revertButton = null!;
	private Button? _slotCountUpButton;
	private Button? _slotCountDownButton;
	private Button _toggleButton = null!;
	private bool _isCollapsed;
	private bool _isCreatorMode;
	private float _scrollbarAlignOffsetX;
	private int _alignRetriesRemaining = 12;
	private bool _isDirectManipulating;
	private bool _isResizeManipulation;
	private Vector2 _dragStartMousePosition;
	private PresetPanelTuningTarget _dragTarget = PresetPanelTuningTarget.None;
	private PresetPanelTuningSnapshot _dragSnapshot;

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
		ApplyLayoutTuning();
		Callable.From(() =>
		{
			ApplyLayoutTuning();
			RefreshAlignmentToScrollbar();
		}).CallDeferred();
		Connect(CanvasItem.SignalName.VisibilityChanged, Callable.From(OnVisibilityChanged));
	}

	public override void _Input(InputEvent @event)
	{
		base._Input(@event);
		if (!Visible || !IsNodeReady() || _library == null || !CardEditorPresetPanelTunerHooks.IsOpenFor(_library))
		{
			return;
		}

		switch (@event)
		{
			case InputEventMouseButton mouseButton when mouseButton.ButtonIndex == MouseButton.Left:
				if (mouseButton.Pressed)
				{
					TryBeginDirectManipulation(mouseButton);
					if (_isDirectManipulating)
					{
						GetViewport().SetInputAsHandled();
					}
				}
				else if (_isDirectManipulating)
				{
					EndDirectManipulation();
					GetViewport().SetInputAsHandled();
				}
				break;

			case InputEventMouseMotion mouseMotion when _isDirectManipulating:
				UpdateDirectManipulation(mouseMotion.Position);
				GetViewport().SetInputAsHandled();
				break;
		}
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
		ApplyLayoutTuning();
		_alignRetriesRemaining = 12;
		Callable.From(() =>
		{
			ApplyLayoutTuning();
			RefreshAlignmentToScrollbar();
			CardEditorPresetPanelTunerHooks.RefreshFor(_library);
		}).CallDeferred();
	}

	private void TryBeginDirectManipulation(InputEventMouseButton mouseButton)
	{
		PresetPanelTuningTarget target = HitTestTuningTarget(mouseButton.Position);
		if (target == PresetPanelTuningTarget.None)
		{
			return;
		}

		_dragTarget = target;
		_isResizeManipulation = mouseButton.ShiftPressed;
		_isDirectManipulating = true;
		_dragStartMousePosition = mouseButton.Position;
		_dragSnapshot = PresetPanelTuningSnapshot.Capture();
		CardEditorPresetPanelTunerHooks.RefreshFor(_library, $"Active={GetTuningTargetName(target)} Mode={(_isResizeManipulation ? "Resize" : "Move")}");
	}

	private void UpdateDirectManipulation(Vector2 mousePosition)
	{
		Vector2 delta = mousePosition - _dragStartMousePosition;
		int dx = Mathf.RoundToInt(delta.X);
		int dy = Mathf.RoundToInt(delta.Y);

		switch (_dragTarget)
		{
			case PresetPanelTuningTarget.Popup:
				if (_isResizeManipulation)
				{
					CardEditorPresetPanelTuning.PopupWidth = Mathf.Max(240f, _dragSnapshot.PopupWidth + dx);
					CardEditorPresetPanelTuning.PopupHeight = Mathf.Max(220f, _dragSnapshot.PopupHeight + dy);
				}
				else
				{
					CardEditorPresetPanelTuning.PopupRightInset = _dragSnapshot.PopupRightInset - dx;
					CardEditorPresetPanelTuning.PopupTop = _dragSnapshot.PopupTop + dy;
				}
				break;

			case PresetPanelTuningTarget.Dropdown:
				if (_isResizeManipulation)
				{
					CardEditorPresetPanelTuning.DropdownInsetRight = _dragSnapshot.DropdownInsetRight - dx;
					CardEditorPresetPanelTuning.DropdownHeight = Math.Max(30, _dragSnapshot.DropdownHeight + dy);
				}
				else
				{
					CardEditorPresetPanelTuning.DropdownInsetLeft = _dragSnapshot.DropdownInsetLeft + dx;
					CardEditorPresetPanelTuning.DropdownInsetRight = _dragSnapshot.DropdownInsetRight - dx;
					CardEditorPresetPanelTuning.DropdownOffsetY = _dragSnapshot.DropdownOffsetY + dy;
				}
				break;

			case PresetPanelTuningTarget.TopButtons:
				if (_isResizeManipulation)
				{
					CardEditorPresetPanelTuning.TopButtonWidth = Math.Max(80, _dragSnapshot.TopButtonWidth + dx);
					CardEditorPresetPanelTuning.TopButtonHeight = Math.Max(36, _dragSnapshot.TopButtonHeight + dy);
				}
				else
				{
					CardEditorPresetPanelTuning.TopButtonsInsetLeft = _dragSnapshot.TopButtonsInsetLeft + dx;
					CardEditorPresetPanelTuning.TopButtonsInsetRight = _dragSnapshot.TopButtonsInsetRight - dx;
					CardEditorPresetPanelTuning.TopButtonsOffsetY = _dragSnapshot.TopButtonsOffsetY + dy;
					if (CardEditorPresetPanelTuning.LinkButtonRows)
					{
						CardEditorPresetPanelTuning.SyncBottomActionRowFromTop();
					}
				}
				break;

			case PresetPanelTuningTarget.LoadButton:
				if (_isResizeManipulation)
				{
					CardEditorPresetPanelTuning.TopButtonWidth = Math.Max(80, _dragSnapshot.TopButtonWidth + dx);
					CardEditorPresetPanelTuning.TopButtonHeight = Math.Max(36, _dragSnapshot.TopButtonHeight + dy);
				}
				else
				{
					CardEditorPresetPanelTuning.LoadButtonOffsetX = _dragSnapshot.LoadButtonOffsetX + dx;
					CardEditorPresetPanelTuning.LoadButtonOffsetY = _dragSnapshot.LoadButtonOffsetY + dy;
				}
				break;

			case PresetPanelTuningTarget.DeleteButton:
				if (_isResizeManipulation)
				{
					CardEditorPresetPanelTuning.TopButtonWidth = Math.Max(80, _dragSnapshot.TopButtonWidth + dx);
					CardEditorPresetPanelTuning.TopButtonHeight = Math.Max(36, _dragSnapshot.TopButtonHeight + dy);
				}
				else
				{
					CardEditorPresetPanelTuning.DeleteButtonOffsetX = _dragSnapshot.DeleteButtonOffsetX + dx;
					CardEditorPresetPanelTuning.DeleteButtonOffsetY = _dragSnapshot.DeleteButtonOffsetY + dy;
				}
				break;

			case PresetPanelTuningTarget.Checkbox:
				if (_isResizeManipulation)
				{
					CardEditorPresetPanelTuning.CheckboxInsetRight = _dragSnapshot.CheckboxInsetRight - dx;
					CardEditorPresetPanelTuning.CheckboxHeight = Math.Max(24, _dragSnapshot.CheckboxHeight + dy);
					CardEditorPresetPanelTuning.CheckboxVisualScale = Math.Max(20, _dragSnapshot.CheckboxVisualScale + Mathf.RoundToInt(delta.X * 0.5f));
				}
				else
				{
					CardEditorPresetPanelTuning.CheckboxInsetLeft = _dragSnapshot.CheckboxInsetLeft + dx;
					CardEditorPresetPanelTuning.CheckboxInsetRight = _dragSnapshot.CheckboxInsetRight - dx;
					CardEditorPresetPanelTuning.CheckboxOffsetY = _dragSnapshot.CheckboxOffsetY + dy;
				}
				break;

			case PresetPanelTuningTarget.NameField:
				if (_isResizeManipulation)
				{
					CardEditorPresetPanelTuning.NameInsetRight = _dragSnapshot.NameInsetRight - dx;
					CardEditorPresetPanelTuning.NameHeight = Math.Max(30, _dragSnapshot.NameHeight + dy);
				}
				else
				{
					CardEditorPresetPanelTuning.NameInsetLeft = _dragSnapshot.NameInsetLeft + dx;
					CardEditorPresetPanelTuning.NameInsetRight = _dragSnapshot.NameInsetRight - dx;
					CardEditorPresetPanelTuning.NameOffsetY = _dragSnapshot.NameOffsetY + dy;
				}
				break;

			case PresetPanelTuningTarget.SlotSpinner:
				if (_isResizeManipulation)
				{
					CardEditorPresetPanelTuning.SlotFieldWidth = Math.Max(48, _dragSnapshot.SlotFieldWidth + dx);
					CardEditorPresetPanelTuning.SlotFieldHeight = Math.Max(24, _dragSnapshot.SlotFieldHeight + dy);
					CardEditorPresetPanelTuning.SlotArrowColumnHeight = Math.Max(24, _dragSnapshot.SlotArrowColumnHeight + dy);
				}
				else
				{
					CardEditorPresetPanelTuning.SlotSpinnerInsetLeft = _dragSnapshot.SlotSpinnerInsetLeft + dx;
					CardEditorPresetPanelTuning.SlotSpinnerInsetRight = _dragSnapshot.SlotSpinnerInsetRight - dx;
					CardEditorPresetPanelTuning.SlotSpinnerOffsetY = _dragSnapshot.SlotSpinnerOffsetY + dy;
				}
				break;

			case PresetPanelTuningTarget.SlotField:
				if (_isResizeManipulation)
				{
					CardEditorPresetPanelTuning.SlotFieldWidth = Math.Max(48, _dragSnapshot.SlotFieldWidth + dx);
					CardEditorPresetPanelTuning.SlotFieldHeight = Math.Max(24, _dragSnapshot.SlotFieldHeight + dy);
				}
				else
				{
					CardEditorPresetPanelTuning.SlotFieldOffsetX = _dragSnapshot.SlotFieldOffsetX + dx;
					CardEditorPresetPanelTuning.SlotFieldOffsetY = _dragSnapshot.SlotFieldOffsetY + dy;
				}
				break;

			case PresetPanelTuningTarget.SlotArrows:
				if (_isResizeManipulation)
				{
					CardEditorPresetPanelTuning.SlotArrowColumnWidth = Math.Max(20, _dragSnapshot.SlotArrowColumnWidth + dx);
					CardEditorPresetPanelTuning.SlotArrowColumnHeight = Math.Max(24, _dragSnapshot.SlotArrowColumnHeight + dy);
					CardEditorPresetPanelTuning.SlotArrowButtonWidth = Math.Max(16, _dragSnapshot.SlotArrowButtonWidth + dx);
					CardEditorPresetPanelTuning.SlotArrowButtonHeight = Math.Max(12, _dragSnapshot.SlotArrowButtonHeight + Mathf.RoundToInt(dy * 0.5f));
				}
				else
				{
					CardEditorPresetPanelTuning.SlotArrowsOffsetX = _dragSnapshot.SlotArrowsOffsetX + dx;
					CardEditorPresetPanelTuning.SlotArrowsOffsetY = _dragSnapshot.SlotArrowsOffsetY + dy;
				}
				break;

			case PresetPanelTuningTarget.SaveButton:
				if (_isResizeManipulation)
				{
					CardEditorPresetPanelTuning.SaveButtonWidth = Math.Max(60, _dragSnapshot.SaveButtonWidth + dx);
					CardEditorPresetPanelTuning.BottomButtonHeight = Math.Max(36, _dragSnapshot.BottomButtonHeight + dy);
				}
				else
				{
					CardEditorPresetPanelTuning.SaveButtonOffsetX = _dragSnapshot.SaveButtonOffsetX + dx;
					CardEditorPresetPanelTuning.SaveButtonOffsetY = _dragSnapshot.SaveButtonOffsetY + dy;
				}
				break;

			case PresetPanelTuningTarget.RevertButton:
				if (_isResizeManipulation)
				{
					CardEditorPresetPanelTuning.RevertButtonWidth = Math.Max(120, _dragSnapshot.RevertButtonWidth + dx);
					CardEditorPresetPanelTuning.BottomButtonHeight = Math.Max(36, _dragSnapshot.BottomButtonHeight + dy);
				}
				else
				{
					CardEditorPresetPanelTuning.RevertButtonOffsetX = _dragSnapshot.RevertButtonOffsetX + dx;
					CardEditorPresetPanelTuning.RevertButtonOffsetY = _dragSnapshot.RevertButtonOffsetY + dy;
				}
				break;
		}

		ApplyLayoutTuning();
		CardEditorPresetPanelTunerHooks.RefreshFor(_library, $"Active={GetTuningTargetName(_dragTarget)} Mode={(_isResizeManipulation ? "Resize" : "Move")}");
	}

	private void EndDirectManipulation()
	{
		_isDirectManipulating = false;
		_isResizeManipulation = false;
		_dragTarget = PresetPanelTuningTarget.None;
		CardEditorPresetPanelTunerHooks.RefreshFor(_library, "Active=None");
	}

	private PresetPanelTuningTarget HitTestTuningTarget(Vector2 mousePosition)
	{
		if (_saveButton != null && GodotObject.IsInstanceValid(_saveButton) && _saveButton.GetGlobalRect().HasPoint(mousePosition))
		{
			return PresetPanelTuningTarget.SaveButton;
		}
		if (_revertButton != null && GodotObject.IsInstanceValid(_revertButton) && _revertButton.GetGlobalRect().HasPoint(mousePosition))
		{
			return PresetPanelTuningTarget.RevertButton;
		}
		if (_loadButton != null && GodotObject.IsInstanceValid(_loadButton) && _loadButton.GetGlobalRect().HasPoint(mousePosition))
		{
			return PresetPanelTuningTarget.LoadButton;
		}
		if (_deleteButton != null && GodotObject.IsInstanceValid(_deleteButton) && _deleteButton.GetGlobalRect().HasPoint(mousePosition))
		{
			return PresetPanelTuningTarget.DeleteButton;
		}
		if (_presetSelect != null && GodotObject.IsInstanceValid(_presetSelect) && _presetSelect.GetGlobalRect().HasPoint(mousePosition))
		{
			return PresetPanelTuningTarget.Dropdown;
		}
		if (_startupCheckbox != null && GodotObject.IsInstanceValid(_startupCheckbox) && _startupCheckbox.GetGlobalRect().HasPoint(mousePosition))
		{
			return PresetPanelTuningTarget.Checkbox;
		}
		if (_sortByCharacterCheckbox != null && GodotObject.IsInstanceValid(_sortByCharacterCheckbox) && _sortByCharacterCheckbox.Visible && _sortByCharacterCheckbox.GetGlobalRect().HasPoint(mousePosition))
		{
			return PresetPanelTuningTarget.Checkbox;
		}
		if (_presetNameField != null && GodotObject.IsInstanceValid(_presetNameField) && _presetNameField.GetGlobalRect().HasPoint(mousePosition))
		{
			return PresetPanelTuningTarget.NameField;
		}
		if (_slotCountField != null && GodotObject.IsInstanceValid(_slotCountField) && _slotCountField.Visible && _slotCountField.GetGlobalRect().HasPoint(mousePosition))
		{
			return PresetPanelTuningTarget.SlotField;
		}
		if (_slotCountArrows != null && GodotObject.IsInstanceValid(_slotCountArrows) && _slotCountArrows.Visible && _slotCountArrows.GetGlobalRect().HasPoint(mousePosition))
		{
			return PresetPanelTuningTarget.SlotArrows;
		}
		if (_slotCountSpinner != null && GodotObject.IsInstanceValid(_slotCountSpinner) && _slotCountSpinner.Visible && _slotCountSpinner.GetGlobalRect().HasPoint(mousePosition))
		{
			return PresetPanelTuningTarget.SlotSpinner;
		}
		if (GetGlobalRect().HasPoint(mousePosition))
		{
			return PresetPanelTuningTarget.Popup;
		}
		return PresetPanelTuningTarget.None;
	}

	private static string GetTuningTargetName(PresetPanelTuningTarget target)
	{
		return target switch
		{
			PresetPanelTuningTarget.Popup => "Popup",
			PresetPanelTuningTarget.Dropdown => "Dropdown",
			PresetPanelTuningTarget.TopButtons => "TopButtons",
			PresetPanelTuningTarget.LoadButton => "LoadButton",
			PresetPanelTuningTarget.DeleteButton => "DeleteButton",
			PresetPanelTuningTarget.Checkbox => "Checkbox",
			PresetPanelTuningTarget.NameField => "NameField",
			PresetPanelTuningTarget.SlotSpinner => "SlotSpinner",
			PresetPanelTuningTarget.SlotField => "SlotField",
			PresetPanelTuningTarget.SlotArrows => "SlotArrows",
			PresetPanelTuningTarget.SaveButton => "SaveButton",
			PresetPanelTuningTarget.RevertButton => "ResetButton",
			_ => "None"
		};
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
		ApplyOffsetsForState();

		AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
	}

	private void BuildUi()
	{
		CardEditorGodotResourceCache.TryLoad(ref _headerFont, _headerFontPath);
		CardEditorGodotResourceCache.TryLoad(ref _bodyFont, _bodyFontPath);
		CardEditorGodotResourceCache.Load(ref _actionButtonTexture, _actionButtonTexturePath);
		CardEditorGodotResourceCache.Load(ref _actionButtonOutlineTexture, _actionButtonOutlineTexturePath);
		CardEditorGodotResourceCache.TryLoad(ref _actionButtonFont, _actionButtonFontPath);
		CardEditorGodotResourceCache.Load(ref _actionButtonTheme, _actionButtonThemePath);
		CardEditorGodotResourceCache.Load(ref _actionButtonOutlineMaterial, _actionButtonOutlineMaterialPath);
		CardEditorGodotResourceCache.Load(ref _actionButtonShader, _actionButtonShaderPath);
		CardEditorGodotResourceCache.Load(ref _tickboxScene, _tickboxScenePath);
		_popupShell = CreatePopupShell();
		if (_popupShell != null)
		{
			AddChild(_popupShell);
		}
		else
		{
			ApplyFallbackPanelStyle();
		}

		_margin = new MarginContainer();
		_margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(_margin);

		VBoxContainer root = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		root.AddThemeConstantOverride("separation", 8);
		_margin.AddChild(root);

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
		_presetSelectWrapper = CreateTunableWrapper(_presetSelect);
		_content.AddChild(_presetSelectWrapper);

		_topActions = new HBoxContainer();
		_topActions.AddThemeConstantOverride("separation", 10);
		_loadButton = CreateActionButton(
			CardEditorLoc.T("button.load", "Load"),
			LoadButtonTint,
			OnLoadPressed,
			193f,
			54f,
			18);
		_loadButtonWrapper = CreateTunableWrapper(_loadButton, expandFill: false);
		_topActions.AddChild(_loadButtonWrapper);
		_deleteButton = CreateActionButton(
			CardEditorLoc.T("button.delete", "Delete"),
			DeleteButtonTint,
			OnDeletePressed,
			193f,
			54f,
			18);
		_deleteButtonWrapper = CreateTunableWrapper(_deleteButton, expandFill: false);
		_topActions.AddChild(_deleteButtonWrapper);
		_topActionsWrapper = CreateTunableWrapper(_topActions);
		_content.AddChild(_topActionsWrapper);

		_startupCheckbox = CreateTickbox(CardEditorLoc.T("presets.runAtStartup", "Run at startup"));
		_startupCheckbox.Toggled += OnStartupToggled;
		_startupCheckboxWrapper = CreateTunableWrapper(_startupCheckbox);
		_content.AddChild(_startupCheckboxWrapper);

		_slotCountRow = BuildSlotCountRow();
		_slotCountRow.Visible = _isCreatorMode;
		_slotCountRowWrapper = CreateTunableWrapper(_slotCountRow);
		_content.AddChild(_slotCountRowWrapper);

		_sortByCharacterCheckbox = CreateTickbox(CardEditorLoc.T("presets.sortByCharacter", "Sort by Character"));
		_sortByCharacterCheckbox.Toggled += OnSortByCharacterToggled;
		_sortByCharacterCheckbox.Visible = _isCreatorMode;
		_sortByCharacterCheckboxWrapper = CreateTunableWrapper(_sortByCharacterCheckbox);
		_content.AddChild(_sortByCharacterCheckboxWrapper);

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
		_presetNameWrapper = CreateTunableWrapper(_presetNameField);
		_content.AddChild(_presetNameWrapper);

		_bottomActions = new HBoxContainer();
		_bottomActions.AddThemeConstantOverride("separation", 10);
		_saveButton = CreateActionButton(
			CardEditorLoc.T("button.save", "Save"),
			SaveButtonTint,
			OnSavePressed,
			118f,
			54f,
			18);
		_saveButtonWrapper = CreateTunableWrapper(_saveButton, expandFill: false);
		_bottomActions.AddChild(_saveButtonWrapper);

		_revertButton = CreateActionButton(
			CardEditorLoc.T("button.reset", "Reset"),
			VanillaButtonTint,
			OnVanillaPressed,
			276f,
			54f,
			15);
		_revertButton.TooltipText = CardEditorLoc.T("button.revertToVanilla", "Revert to Vanilla");
		_revertButtonWrapper = CreateTunableWrapper(_revertButton, expandFill: false);
		_bottomActions.AddChild(_revertButtonWrapper);
		_bottomActionsWrapper = CreateTunableWrapper(_bottomActions);
		_content.AddChild(_bottomActionsWrapper);

		SetCollapsed(collapsed: false);
		ApplyLayoutTuning();
	}

	private void ApplyFallbackPanelStyle()
	{
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

	private Control? CreatePopupShell()
	{
		try
		{
			CardEditorGodotResourceCache.Load(ref _popupShellTexture, _popupShellTexturePath);
			if (_popupShellTexture == null)
			{
				return null;
			}

			TextureRect shell = new TextureRect
			{
				Name = "PresetPopupShell",
				Texture = _popupShellTexture,
				ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional
			};
			shell.MouseFilter = MouseFilterEnum.Ignore;
			shell.ZIndex = -1;
			shell.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			return shell;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][PresetPanel] Failed creating popup shell: {ex}");
			return null;
		}
	}

	private static MarginContainer CreateTunableWrapper(Control child, bool expandFill = true)
	{
		MarginContainer wrapper = new MarginContainer();
		if (expandFill)
		{
			wrapper.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		}
		wrapper.AddChild(child);
		return wrapper;
	}

	public void ApplyLayoutTuning()
	{
		if (!IsNodeReady())
		{
			return;
		}

		if (_margin != null && GodotObject.IsInstanceValid(_margin) && !_isCollapsed)
		{
			_margin.AddThemeConstantOverride("margin_left", CardEditorPresetPanelTuning.MarginLeft);
			_margin.AddThemeConstantOverride("margin_top", CardEditorPresetPanelTuning.MarginTop);
			_margin.AddThemeConstantOverride("margin_right", CardEditorPresetPanelTuning.MarginRight);
			_margin.AddThemeConstantOverride("margin_bottom", CardEditorPresetPanelTuning.MarginBottom);
		}

		if (_content != null && GodotObject.IsInstanceValid(_content))
		{
			_content.AddThemeConstantOverride("separation", CardEditorPresetPanelTuning.ContentSeparation);
		}

		ApplyWrapperMargins(_presetSelectWrapper, CardEditorPresetPanelTuning.DropdownInsetLeft, CardEditorPresetPanelTuning.DropdownOffsetY, CardEditorPresetPanelTuning.DropdownInsetRight, 0);
		if (_presetSelect != null && GodotObject.IsInstanceValid(_presetSelect))
		{
			_presetSelect.CustomMinimumSize = new Vector2(0f, CardEditorPresetPanelTuning.DropdownHeight);
		}

		ApplyWrapperMargins(_topActionsWrapper, CardEditorPresetPanelTuning.TopButtonsInsetLeft, CardEditorPresetPanelTuning.TopButtonsOffsetY, CardEditorPresetPanelTuning.TopButtonsInsetRight, 0);
		if (_topActions != null && GodotObject.IsInstanceValid(_topActions))
		{
			_topActions.AddThemeConstantOverride("separation", CardEditorPresetPanelTuning.TopButtonsSeparation);
		}
		if (_loadButton != null && GodotObject.IsInstanceValid(_loadButton))
		{
			_loadButton.SetButtonSize(CardEditorPresetPanelTuning.TopButtonWidth, CardEditorPresetPanelTuning.TopButtonHeight);
			_loadButton.SetTextSize(CardEditorPresetPanelTuning.LoadButtonTextSize);
			_loadButton.SetTextOffsets(0f, 0f, CardEditorPresetPanelTuning.LoadButtonTextOffsetX, CardEditorPresetPanelTuning.LoadButtonTextOffsetY);
		}
		ApplyWrapperMargins(_loadButtonWrapper, CardEditorPresetPanelTuning.LoadButtonOffsetX, CardEditorPresetPanelTuning.LoadButtonOffsetY, 0, 0);
		if (_deleteButton != null && GodotObject.IsInstanceValid(_deleteButton))
		{
			_deleteButton.SetButtonSize(CardEditorPresetPanelTuning.TopButtonWidth, CardEditorPresetPanelTuning.TopButtonHeight);
			_deleteButton.SetTextSize(CardEditorPresetPanelTuning.DeleteButtonTextSize);
			_deleteButton.SetTextOffsets(0f, 0f, CardEditorPresetPanelTuning.DeleteButtonTextOffsetX, CardEditorPresetPanelTuning.DeleteButtonTextOffsetY);
		}
		ApplyWrapperMargins(_deleteButtonWrapper, CardEditorPresetPanelTuning.DeleteButtonOffsetX, CardEditorPresetPanelTuning.DeleteButtonOffsetY, 0, 0);

		ApplyWrapperMargins(_startupCheckboxWrapper, CardEditorPresetPanelTuning.CheckboxInsetLeft, CardEditorPresetPanelTuning.CheckboxOffsetY, CardEditorPresetPanelTuning.CheckboxInsetRight, 0);
		_startupCheckbox?.ApplyLayoutTuning(
			CardEditorPresetPanelTuning.CheckboxVisualScale / 100f,
			CardEditorPresetPanelTuning.CheckboxHeight,
			CardEditorPresetPanelTuning.CheckboxSeparation);
		_sortByCharacterCheckbox?.ApplyLayoutTuning(
			CardEditorPresetPanelTuning.CheckboxVisualScale / 100f,
			CardEditorPresetPanelTuning.CheckboxHeight,
			CardEditorPresetPanelTuning.CheckboxSeparation);

		ApplyWrapperMargins(_presetNameWrapper, CardEditorPresetPanelTuning.NameInsetLeft, CardEditorPresetPanelTuning.NameOffsetY, CardEditorPresetPanelTuning.NameInsetRight, 0);
		if (_presetNameField != null && GodotObject.IsInstanceValid(_presetNameField))
		{
			_presetNameField.CustomMinimumSize = new Vector2(0f, CardEditorPresetPanelTuning.NameHeight);
		}
		if (_slotCountField != null && GodotObject.IsInstanceValid(_slotCountField))
		{
			_slotCountField.CustomMinimumSize = new Vector2(CardEditorPresetPanelTuning.SlotFieldWidth, CardEditorPresetPanelTuning.SlotFieldHeight);
		}
		ApplyWrapperMargins(_slotCountSpinnerWrapper, CardEditorPresetPanelTuning.SlotSpinnerInsetLeft, CardEditorPresetPanelTuning.SlotSpinnerOffsetY, CardEditorPresetPanelTuning.SlotSpinnerInsetRight, 0);
		ApplyWrapperMargins(_slotCountFieldWrapper, CardEditorPresetPanelTuning.SlotFieldOffsetX, CardEditorPresetPanelTuning.SlotFieldOffsetY, 0, 0);
		ApplyWrapperMargins(_slotCountArrowsWrapper, CardEditorPresetPanelTuning.SlotArrowsOffsetX, CardEditorPresetPanelTuning.SlotArrowsOffsetY, 0, 0);
		if (_slotCountSpinner != null && GodotObject.IsInstanceValid(_slotCountSpinner))
		{
			_slotCountSpinner.AddThemeConstantOverride("separation", CardEditorPresetPanelTuning.SlotSpinnerSeparation);
		}
		if (_slotCountArrows != null && GodotObject.IsInstanceValid(_slotCountArrows))
		{
			_slotCountArrows.AddThemeConstantOverride("separation", CardEditorPresetPanelTuning.SlotArrowSeparation);
			_slotCountArrows.CustomMinimumSize = new Vector2(CardEditorPresetPanelTuning.SlotArrowColumnWidth, CardEditorPresetPanelTuning.SlotArrowColumnHeight);
		}
		if (_slotCountUpButton != null && GodotObject.IsInstanceValid(_slotCountUpButton))
		{
			_slotCountUpButton.CustomMinimumSize = new Vector2(CardEditorPresetPanelTuning.SlotArrowButtonWidth, CardEditorPresetPanelTuning.SlotArrowButtonHeight);
		}
		if (_slotCountDownButton != null && GodotObject.IsInstanceValid(_slotCountDownButton))
		{
			_slotCountDownButton.CustomMinimumSize = new Vector2(CardEditorPresetPanelTuning.SlotArrowButtonWidth, CardEditorPresetPanelTuning.SlotArrowButtonHeight);
		}

		ApplyWrapperMargins(_bottomActionsWrapper, CardEditorPresetPanelTuning.BottomButtonsInsetLeft, CardEditorPresetPanelTuning.BottomButtonsOffsetY, CardEditorPresetPanelTuning.BottomButtonsInsetRight, 0);
		if (_bottomActions != null && GodotObject.IsInstanceValid(_bottomActions))
		{
			_bottomActions.AddThemeConstantOverride("separation", CardEditorPresetPanelTuning.BottomButtonsSeparation);
		}
		if (_saveButton != null && GodotObject.IsInstanceValid(_saveButton))
		{
			_saveButton.SetButtonSize(CardEditorPresetPanelTuning.SaveButtonWidth, CardEditorPresetPanelTuning.BottomButtonHeight);
			_saveButton.SetTextSize(CardEditorPresetPanelTuning.SaveButtonTextSize);
			_saveButton.SetTextOffsets(0f, 0f, CardEditorPresetPanelTuning.SaveButtonTextOffsetX, CardEditorPresetPanelTuning.SaveButtonTextOffsetY);
		}
		ApplyWrapperMargins(_saveButtonWrapper, CardEditorPresetPanelTuning.SaveButtonOffsetX, CardEditorPresetPanelTuning.SaveButtonOffsetY, 0, 0);
		if (_revertButton != null && GodotObject.IsInstanceValid(_revertButton))
		{
			_revertButton.SetButtonSize(CardEditorPresetPanelTuning.RevertButtonWidth, CardEditorPresetPanelTuning.BottomButtonHeight);
			_revertButton.SetTextSize(CardEditorPresetPanelTuning.RevertButtonTextSize);
			_revertButton.SetTextOffsets(0f, 0f, CardEditorPresetPanelTuning.RevertButtonTextOffsetX, CardEditorPresetPanelTuning.RevertButtonTextOffsetY);
		}
		ApplyWrapperMargins(_revertButtonWrapper, CardEditorPresetPanelTuning.RevertButtonOffsetX, CardEditorPresetPanelTuning.RevertButtonOffsetY, 0, 0);

		ApplyOffsetsForState();
		QueueSort();
		QueueRedraw();
	}

	private static void ApplyWrapperMargins(MarginContainer? wrapper, int left, int top, int right, int bottom)
	{
		if (wrapper == null || !GodotObject.IsInstanceValid(wrapper))
		{
			return;
		}

		wrapper.AddThemeConstantOverride("margin_left", left);
		wrapper.AddThemeConstantOverride("margin_top", top);
		wrapper.AddThemeConstantOverride("margin_right", right);
		wrapper.AddThemeConstantOverride("margin_bottom", bottom);
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

		_slotCountSpinner = new HBoxContainer();
		_slotCountSpinner.AddThemeConstantOverride("separation", CardEditorPresetPanelTuning.SlotSpinnerSeparation);
		_slotCountSpinner.CustomMinimumSize = new Vector2(138, 44);
		_slotCountSpinnerWrapper = CreateTunableWrapper(_slotCountSpinner, expandFill: false);
		row.AddChild(_slotCountSpinnerWrapper);

		_slotCountField = new NMegaLineEdit
		{
			Text = configured.ToString(CultureInfo.InvariantCulture),
			CustomMinimumSize = new Vector2(CardEditorPresetPanelTuning.SlotFieldWidth, CardEditorPresetPanelTuning.SlotFieldHeight),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			FocusMode = FocusModeEnum.All,
			MouseFilter = MouseFilterEnum.Stop,
			Alignment = HorizontalAlignment.Center
		};
		_slotCountField.TooltipText = label.TooltipText;
		StyleInput(_slotCountField);
		_slotCountField.TextSubmitted += _ => CommitSlotCountFromField();
		_slotCountField.FocusExited += CommitSlotCountFromField;
		_slotCountFieldWrapper = CreateTunableWrapper(_slotCountField, expandFill: false);
		_slotCountSpinner.AddChild(_slotCountFieldWrapper);

		_slotCountArrows = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(CardEditorPresetPanelTuning.SlotArrowColumnWidth, CardEditorPresetPanelTuning.SlotArrowColumnHeight)
		};
		_slotCountArrows.AddThemeConstantOverride("separation", CardEditorPresetPanelTuning.SlotArrowSeparation);
		_slotCountArrowsWrapper = CreateTunableWrapper(_slotCountArrows, expandFill: false);
		_slotCountSpinner.AddChild(_slotCountArrowsWrapper);

		_slotCountUpButton = CreateSpinnerButton("\u25B2");
		_slotCountUpButton.Pressed += () => AdjustSlotCount(+1);
		_slotCountArrows.AddChild(_slotCountUpButton);

		_slotCountDownButton = CreateSpinnerButton("\u25BC");
		_slotCountDownButton.Pressed += () => AdjustSlotCount(-1);
		_slotCountArrows.AddChild(_slotCountDownButton);

		return row;
	}

	private Button CreateSpinnerButton(string text)
	{
		Button button = new Button
		{
			Text = text,
			Flat = true,
			FocusMode = FocusModeEnum.None,
			CustomMinimumSize = _slotCountSpinButtonMinSize,
			MouseFilter = MouseFilterEnum.Stop,
			Alignment = HorizontalAlignment.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
		};
		StyleSpinnerButton(button);
		return button;
	}

	private void StyleSpinnerButton(Button button)
	{
		CardEditorGodotResourceCache.TryLoad(ref _headerFont, _headerFontPath);
		if (_headerFont != null)
		{
			button.AddThemeFontOverride("font", _headerFont);
		}
		button.AddThemeFontSizeOverride("font_size", 18);
		button.AddThemeColorOverride("font_color", StsColors.gold);
		button.AddThemeColorOverride("font_outline_color", StsColors.transparentBlack);
		button.AddThemeConstantOverride("outline_size", 10);

		StyleBoxFlat normal = new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0f) };
		StyleBoxFlat hover = new StyleBoxFlat { BgColor = new Color(1f, 1f, 1f, 0.06f) };
		StyleBoxFlat pressed = new StyleBoxFlat { BgColor = new Color(1f, 1f, 1f, 0.10f) };
		StyleBoxFlat disabled = new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0f) };
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
			_titleLabel.Text = creatorMode
				? CardEditorLoc.T("button.presetCreator", "Preset Creator")
				: CardEditorLoc.T("button.presetEditor", "Preset Editor");
		}

		if (IsNodeReady())
		{
			RefreshPresetList();
		}

		if (_slotCountRow != null && GodotObject.IsInstanceValid(_slotCountRow))
		{
			_slotCountRow.Visible = _isCreatorMode;
		}
		if (_slotCountRowWrapper != null && GodotObject.IsInstanceValid(_slotCountRowWrapper))
		{
			_slotCountRowWrapper.Visible = _isCreatorMode;
		}

		if (_sortByCharacterCheckbox != null && GodotObject.IsInstanceValid(_sortByCharacterCheckbox))
		{
			_sortByCharacterCheckbox.Visible = _isCreatorMode;
		}
		if (_sortByCharacterCheckboxWrapper != null && GodotObject.IsInstanceValid(_sortByCharacterCheckboxWrapper))
		{
			_sortByCharacterCheckboxWrapper.Visible = _isCreatorMode;
		}

		if (IsNodeReady() && !_isCollapsed)
		{
			ApplyLayoutTuning();
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

		CardEditorMod.VerboseLog($"[CardEditor][ConfirmPopup] Delete preset requested for '{name}'");
		bool confirmed = await CardEditorConfirmPopup.ShowConfirmation(
			"Delete Preset?",
			$"Delete preset \"{name}\"?\n\nThis cannot be undone.");
		CardEditorMod.VerboseLog($"[CardEditor][ConfirmPopup] Delete preset confirmation result={confirmed} preset='{name}'");
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
		button.SetTextOffsets(0f, 0f, 0f, 0f);
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
			if (collapsed)
			{
				_margin.AddThemeConstantOverride("margin_left", 16);
				_margin.AddThemeConstantOverride("margin_top", 14);
				_margin.AddThemeConstantOverride("margin_right", 16);
				_margin.AddThemeConstantOverride("margin_bottom", 14);
			}
			else
			{
				_margin.AddThemeConstantOverride("margin_left", CardEditorPresetPanelTuning.MarginLeft);
				_margin.AddThemeConstantOverride("margin_top", CardEditorPresetPanelTuning.MarginTop);
				_margin.AddThemeConstantOverride("margin_right", CardEditorPresetPanelTuning.MarginRight);
				_margin.AddThemeConstantOverride("margin_bottom", CardEditorPresetPanelTuning.MarginBottom);
			}
		}

		ApplyOffsetsForState();
		if (!collapsed)
		{
			ApplyLayoutTuning();
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
			float right = -CardEditorPresetPanelTuning.PopupRightInset + _scrollbarAlignOffsetX;
			OffsetRight = right;
			OffsetLeft = right - CardEditorPresetPanelTuning.PopupWidth;
			OffsetTop = CardEditorPresetPanelTuning.PopupTop;
			OffsetBottom = CardEditorPresetPanelTuning.PopupTop + CardEditorPresetPanelTuning.PopupHeight;
		}
	}

	private enum PresetPanelTuningTarget
	{
		None,
		Popup,
		Dropdown,
		TopButtons,
		LoadButton,
		DeleteButton,
		Checkbox,
		NameField,
		SlotSpinner,
		SlotField,
		SlotArrows,
		SaveButton,
		RevertButton
	}

	private readonly struct PresetPanelTuningSnapshot
	{
		public readonly float PopupTop;
		public readonly float PopupRightInset;
		public readonly float PopupWidth;
		public readonly float PopupHeight;
		public readonly int DropdownInsetLeft;
		public readonly int DropdownInsetRight;
		public readonly int DropdownOffsetY;
		public readonly int DropdownHeight;
		public readonly int TopButtonsInsetLeft;
		public readonly int TopButtonsInsetRight;
		public readonly int TopButtonsOffsetY;
		public readonly int TopButtonWidth;
		public readonly int TopButtonHeight;
		public readonly int LoadButtonOffsetX;
		public readonly int LoadButtonOffsetY;
		public readonly int DeleteButtonOffsetX;
		public readonly int DeleteButtonOffsetY;
		public readonly int CheckboxInsetLeft;
		public readonly int CheckboxInsetRight;
		public readonly int CheckboxOffsetY;
		public readonly int CheckboxHeight;
		public readonly int CheckboxVisualScale;
		public readonly int NameInsetLeft;
		public readonly int NameInsetRight;
		public readonly int NameOffsetY;
		public readonly int NameHeight;
		public readonly int SlotSpinnerInsetLeft;
		public readonly int SlotSpinnerInsetRight;
		public readonly int SlotSpinnerOffsetY;
		public readonly int SlotFieldOffsetX;
		public readonly int SlotFieldOffsetY;
		public readonly int SlotFieldWidth;
		public readonly int SlotFieldHeight;
		public readonly int SlotArrowsOffsetX;
		public readonly int SlotArrowsOffsetY;
		public readonly int SlotArrowColumnWidth;
		public readonly int SlotArrowColumnHeight;
		public readonly int SlotArrowButtonWidth;
		public readonly int SlotArrowButtonHeight;
		public readonly int BottomButtonsInsetLeft;
		public readonly int BottomButtonsInsetRight;
		public readonly int BottomButtonsOffsetY;
		public readonly int SaveButtonWidth;
		public readonly int RevertButtonWidth;
		public readonly int BottomButtonHeight;
		public readonly int SaveButtonOffsetX;
		public readonly int SaveButtonOffsetY;
		public readonly int RevertButtonOffsetX;
		public readonly int RevertButtonOffsetY;

		private PresetPanelTuningSnapshot(
			float popupTop,
			float popupRightInset,
			float popupWidth,
			float popupHeight,
			int dropdownInsetLeft,
			int dropdownInsetRight,
			int dropdownOffsetY,
			int dropdownHeight,
			int topButtonsInsetLeft,
			int topButtonsInsetRight,
			int topButtonsOffsetY,
			int topButtonWidth,
			int topButtonHeight,
			int loadButtonOffsetX,
			int loadButtonOffsetY,
			int deleteButtonOffsetX,
			int deleteButtonOffsetY,
			int checkboxInsetLeft,
			int checkboxInsetRight,
			int checkboxOffsetY,
			int checkboxHeight,
			int checkboxVisualScale,
			int nameInsetLeft,
			int nameInsetRight,
			int nameOffsetY,
			int nameHeight,
			int slotSpinnerInsetLeft,
			int slotSpinnerInsetRight,
			int slotSpinnerOffsetY,
			int slotFieldOffsetX,
			int slotFieldOffsetY,
			int slotFieldWidth,
			int slotFieldHeight,
			int slotArrowsOffsetX,
			int slotArrowsOffsetY,
			int slotArrowColumnWidth,
			int slotArrowColumnHeight,
			int slotArrowButtonWidth,
			int slotArrowButtonHeight,
			int bottomButtonsInsetLeft,
			int bottomButtonsInsetRight,
			int bottomButtonsOffsetY,
			int saveButtonWidth,
			int revertButtonWidth,
			int bottomButtonHeight,
			int saveButtonOffsetX,
			int saveButtonOffsetY,
			int revertButtonOffsetX,
			int revertButtonOffsetY)
		{
			PopupTop = popupTop;
			PopupRightInset = popupRightInset;
			PopupWidth = popupWidth;
			PopupHeight = popupHeight;
			DropdownInsetLeft = dropdownInsetLeft;
			DropdownInsetRight = dropdownInsetRight;
			DropdownOffsetY = dropdownOffsetY;
			DropdownHeight = dropdownHeight;
			TopButtonsInsetLeft = topButtonsInsetLeft;
			TopButtonsInsetRight = topButtonsInsetRight;
			TopButtonsOffsetY = topButtonsOffsetY;
			TopButtonWidth = topButtonWidth;
			TopButtonHeight = topButtonHeight;
			LoadButtonOffsetX = loadButtonOffsetX;
			LoadButtonOffsetY = loadButtonOffsetY;
			DeleteButtonOffsetX = deleteButtonOffsetX;
			DeleteButtonOffsetY = deleteButtonOffsetY;
			CheckboxInsetLeft = checkboxInsetLeft;
			CheckboxInsetRight = checkboxInsetRight;
			CheckboxOffsetY = checkboxOffsetY;
			CheckboxHeight = checkboxHeight;
			CheckboxVisualScale = checkboxVisualScale;
			NameInsetLeft = nameInsetLeft;
			NameInsetRight = nameInsetRight;
			NameOffsetY = nameOffsetY;
			NameHeight = nameHeight;
			SlotSpinnerInsetLeft = slotSpinnerInsetLeft;
			SlotSpinnerInsetRight = slotSpinnerInsetRight;
			SlotSpinnerOffsetY = slotSpinnerOffsetY;
			SlotFieldOffsetX = slotFieldOffsetX;
			SlotFieldOffsetY = slotFieldOffsetY;
			SlotFieldWidth = slotFieldWidth;
			SlotFieldHeight = slotFieldHeight;
			SlotArrowsOffsetX = slotArrowsOffsetX;
			SlotArrowsOffsetY = slotArrowsOffsetY;
			SlotArrowColumnWidth = slotArrowColumnWidth;
			SlotArrowColumnHeight = slotArrowColumnHeight;
			SlotArrowButtonWidth = slotArrowButtonWidth;
			SlotArrowButtonHeight = slotArrowButtonHeight;
			BottomButtonsInsetLeft = bottomButtonsInsetLeft;
			BottomButtonsInsetRight = bottomButtonsInsetRight;
			BottomButtonsOffsetY = bottomButtonsOffsetY;
			SaveButtonWidth = saveButtonWidth;
			RevertButtonWidth = revertButtonWidth;
			BottomButtonHeight = bottomButtonHeight;
			SaveButtonOffsetX = saveButtonOffsetX;
			SaveButtonOffsetY = saveButtonOffsetY;
			RevertButtonOffsetX = revertButtonOffsetX;
			RevertButtonOffsetY = revertButtonOffsetY;
		}

		public static PresetPanelTuningSnapshot Capture()
		{
			return new PresetPanelTuningSnapshot(
				CardEditorPresetPanelTuning.PopupTop,
				CardEditorPresetPanelTuning.PopupRightInset,
				CardEditorPresetPanelTuning.PopupWidth,
				CardEditorPresetPanelTuning.PopupHeight,
				CardEditorPresetPanelTuning.DropdownInsetLeft,
				CardEditorPresetPanelTuning.DropdownInsetRight,
				CardEditorPresetPanelTuning.DropdownOffsetY,
				CardEditorPresetPanelTuning.DropdownHeight,
				CardEditorPresetPanelTuning.TopButtonsInsetLeft,
				CardEditorPresetPanelTuning.TopButtonsInsetRight,
				CardEditorPresetPanelTuning.TopButtonsOffsetY,
				CardEditorPresetPanelTuning.TopButtonWidth,
				CardEditorPresetPanelTuning.TopButtonHeight,
				CardEditorPresetPanelTuning.LoadButtonOffsetX,
				CardEditorPresetPanelTuning.LoadButtonOffsetY,
				CardEditorPresetPanelTuning.DeleteButtonOffsetX,
				CardEditorPresetPanelTuning.DeleteButtonOffsetY,
				CardEditorPresetPanelTuning.CheckboxInsetLeft,
				CardEditorPresetPanelTuning.CheckboxInsetRight,
				CardEditorPresetPanelTuning.CheckboxOffsetY,
				CardEditorPresetPanelTuning.CheckboxHeight,
				CardEditorPresetPanelTuning.CheckboxVisualScale,
				CardEditorPresetPanelTuning.NameInsetLeft,
				CardEditorPresetPanelTuning.NameInsetRight,
				CardEditorPresetPanelTuning.NameOffsetY,
				CardEditorPresetPanelTuning.NameHeight,
				CardEditorPresetPanelTuning.SlotSpinnerInsetLeft,
				CardEditorPresetPanelTuning.SlotSpinnerInsetRight,
				CardEditorPresetPanelTuning.SlotSpinnerOffsetY,
				CardEditorPresetPanelTuning.SlotFieldOffsetX,
				CardEditorPresetPanelTuning.SlotFieldOffsetY,
				CardEditorPresetPanelTuning.SlotFieldWidth,
				CardEditorPresetPanelTuning.SlotFieldHeight,
				CardEditorPresetPanelTuning.SlotArrowsOffsetX,
				CardEditorPresetPanelTuning.SlotArrowsOffsetY,
				CardEditorPresetPanelTuning.SlotArrowColumnWidth,
				CardEditorPresetPanelTuning.SlotArrowColumnHeight,
				CardEditorPresetPanelTuning.SlotArrowButtonWidth,
				CardEditorPresetPanelTuning.SlotArrowButtonHeight,
				CardEditorPresetPanelTuning.BottomButtonsInsetLeft,
				CardEditorPresetPanelTuning.BottomButtonsInsetRight,
				CardEditorPresetPanelTuning.BottomButtonsOffsetY,
				CardEditorPresetPanelTuning.SaveButtonWidth,
				CardEditorPresetPanelTuning.RevertButtonWidth,
				CardEditorPresetPanelTuning.BottomButtonHeight,
				CardEditorPresetPanelTuning.SaveButtonOffsetX,
				CardEditorPresetPanelTuning.SaveButtonOffsetY,
				CardEditorPresetPanelTuning.RevertButtonOffsetX,
				CardEditorPresetPanelTuning.RevertButtonOffsetY);
		}
	}
}

internal sealed class PresetVanillaTickbox : HBoxContainer
{
	private readonly Control _tickedImage;
	private readonly Control _notTickedImage;
	private readonly Control _tickboxVisuals;
	private readonly Label _label;
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

		_tickboxVisuals = tickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
		_tickboxVisuals.MouseFilter = MouseFilterEnum.Ignore;
		_tickboxVisuals.CustomMinimumSize = new Vector2(48f, 48f);
		_tickboxVisuals.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
		_tickboxVisuals.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
		_tickboxVisuals.Scale = Vector2.One * 0.72f;
		_tickboxVisuals.PivotOffset = new Vector2(24f, 24f);

		_label = new Label
		{
			Text = text,
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore
		};
		if (bodyFont != null)
		{
			_label.AddThemeFontOverride("font", bodyFont);
		}
		_label.AddThemeFontSizeOverride("font_size", 20);
		_label.AddThemeColorOverride("font_color", StsColors.cream);
		_label.AddThemeColorOverride("font_outline_color", StsColors.transparentBlack);
		_label.AddThemeConstantOverride("outline_size", 8);

		AddChild(_tickboxVisuals);
		AddChild(_label);

		_tickedImage = _tickboxVisuals.GetNode<Control>("Ticked");
		_notTickedImage = _tickboxVisuals.GetNode<Control>("NotTicked");
		SetTicked(false, notify: false);

		GuiInput += OnGuiInput;
	}

	public void ApplyLayoutTuning(float visualScale, int rowHeight, int separation)
	{
		CustomMinimumSize = new Vector2(0f, rowHeight);
		AddThemeConstantOverride("separation", separation);
		_tickboxVisuals.Scale = Vector2.One * visualScale;
		_tickboxVisuals.PivotOffset = new Vector2(24f, 24f);
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
