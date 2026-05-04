using System;
using System.Globalization;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorPresetPanelTuning
{
	public const bool TunerEnabled = false;

	public static float PopupTop = 24f;
	public static float PopupRightInset = 62f;
	public static float PopupWidth = 346f;
	public static float PopupHeight = 220f;
	public static bool LinkButtonRows;

	public static int MarginLeft = 18;
	public static int MarginTop = 34;
	public static int MarginRight = -82;
	public static int MarginBottom = 44;
	public static int ContentSeparation = 10;

	public static int DropdownInsetLeft = 0;
	public static int DropdownInsetRight = 110;
	public static int DropdownOffsetY = -20;
	public static int DropdownHeight = 30;

	public static int TopButtonsInsetLeft = 0;
	public static int TopButtonsInsetRight = 8;
	public static int TopButtonsOffsetY = -10;
	public static int TopButtonsSeparation = 6;
	public static int TopButtonWidth = 143;
	public static int TopButtonHeight = 54;
	public static int LoadButtonOffsetX = 0;
	public static int LoadButtonOffsetY = 0;
	public static int LoadButtonTextSize = 20;
	public static int LoadButtonTextOffsetX = 4;
	public static int LoadButtonTextOffsetY = 0;
	public static int DeleteButtonOffsetX = 0;
	public static int DeleteButtonOffsetY = 0;
	public static int DeleteButtonTextSize = 18;
	public static int DeleteButtonTextOffsetX = 0;
	public static int DeleteButtonTextOffsetY = 0;

	public static int CheckboxInsetLeft = 0;
	public static int CheckboxInsetRight = 2;
	public static int CheckboxOffsetY = 0;
	public static int CheckboxHeight = 24;
	public static int CheckboxSeparation = 4;
	public static int CheckboxVisualScale = 92;

	public static int NameInsetLeft = 0;
	public static int NameInsetRight = 110;
	public static int NameOffsetY = -20;
	public static int NameHeight = 30;

	public static int SlotSpinnerInsetLeft = 0;
	public static int SlotSpinnerInsetRight = 0;
	public static int SlotSpinnerOffsetY = 0;
	public static int SlotSpinnerSeparation = 8;
	public static int SlotFieldOffsetX = -90;
	public static int SlotFieldOffsetY = 0;
	public static int SlotFieldWidth = 96;
	public static int SlotFieldHeight = 44;
	public static int SlotArrowsOffsetX = -12;
	public static int SlotArrowsOffsetY = -3;
	public static int SlotArrowColumnWidth = 34;
	public static int SlotArrowColumnHeight = 44;
	public static int SlotArrowSeparation = 0;
	public static int SlotArrowButtonWidth = 34;
	public static int SlotArrowButtonHeight = 20;

	public static int BottomButtonsInsetLeft = -2;
	public static int BottomButtonsInsetRight = 10;
	public static int BottomButtonsOffsetY = -10;
	public static int BottomButtonsSeparation = 10;
	public static int SaveButtonWidth = 118;
	public static int RevertButtonWidth = 196;
	public static int BottomButtonHeight = 54;
	public static int SaveButtonOffsetX = 0;
	public static int SaveButtonOffsetY = 0;
	public static int SaveButtonTextSize = 19;
	public static int SaveButtonTextOffsetX = 20;
	public static int SaveButtonTextOffsetY = 0;
	public static int RevertButtonOffsetX = 0;
	public static int RevertButtonOffsetY = 0;
	public static int RevertButtonTextSize = 19;
	public static int RevertButtonTextOffsetX = 0;
	public static int RevertButtonTextOffsetY = 0;

	public static float TunerOffsetX = 24f;
	public static float TunerOffsetY = 120f;

	public static void Reset()
	{
		PopupTop = 24f;
		PopupRightInset = 62f;
		PopupWidth = 346f;
		PopupHeight = 220f;
		LinkButtonRows = false;

		MarginLeft = 18;
		MarginTop = 34;
		MarginRight = -82;
		MarginBottom = 44;
		ContentSeparation = 10;

		DropdownInsetLeft = 0;
		DropdownInsetRight = 110;
		DropdownOffsetY = -20;
		DropdownHeight = 30;

		TopButtonsInsetLeft = 0;
		TopButtonsInsetRight = 8;
		TopButtonsOffsetY = -10;
		TopButtonsSeparation = 6;
		TopButtonWidth = 143;
		TopButtonHeight = 54;
		LoadButtonOffsetX = 0;
		LoadButtonOffsetY = 0;
		LoadButtonTextSize = 20;
		LoadButtonTextOffsetX = 4;
		LoadButtonTextOffsetY = 0;
		DeleteButtonOffsetX = 0;
		DeleteButtonOffsetY = 0;
		DeleteButtonTextSize = 18;
		DeleteButtonTextOffsetX = 0;
		DeleteButtonTextOffsetY = 0;

		CheckboxInsetLeft = 0;
		CheckboxInsetRight = 2;
		CheckboxOffsetY = 0;
		CheckboxHeight = 24;
		CheckboxSeparation = 4;
		CheckboxVisualScale = 92;

		NameInsetLeft = 0;
		NameInsetRight = 110;
		NameOffsetY = -20;
		NameHeight = 30;

		SlotSpinnerInsetLeft = 0;
		SlotSpinnerInsetRight = 0;
		SlotSpinnerOffsetY = 0;
		SlotSpinnerSeparation = 8;
		SlotFieldOffsetX = -90;
		SlotFieldOffsetY = 0;
		SlotFieldWidth = 96;
		SlotFieldHeight = 44;
		SlotArrowsOffsetX = -12;
		SlotArrowsOffsetY = -3;
		SlotArrowColumnWidth = 34;
		SlotArrowColumnHeight = 44;
		SlotArrowSeparation = 0;
		SlotArrowButtonWidth = 34;
		SlotArrowButtonHeight = 20;

		BottomButtonsInsetLeft = -2;
		BottomButtonsInsetRight = 10;
		BottomButtonsOffsetY = -10;
		BottomButtonsSeparation = 10;
		SaveButtonWidth = 118;
		RevertButtonWidth = 196;
		BottomButtonHeight = 54;
		SaveButtonOffsetX = 0;
		SaveButtonOffsetY = 0;
		SaveButtonTextSize = 19;
		SaveButtonTextOffsetX = 20;
		SaveButtonTextOffsetY = 0;
		RevertButtonOffsetX = 0;
		RevertButtonOffsetY = 0;
		RevertButtonTextSize = 19;
		RevertButtonTextOffsetX = 0;
		RevertButtonTextOffsetY = 0;
	}

	public static void SyncBottomActionRowFromTop()
	{
		BottomButtonsInsetLeft = TopButtonsInsetLeft;
		BottomButtonsInsetRight = TopButtonsInsetRight;
		BottomButtonsOffsetY = TopButtonsOffsetY;
		BottomButtonsSeparation = TopButtonsSeparation;
	}

	public static void SyncTopActionRowFromBottom()
	{
		TopButtonsInsetLeft = BottomButtonsInsetLeft;
		TopButtonsInsetRight = BottomButtonsInsetRight;
		TopButtonsOffsetY = BottomButtonsOffsetY;
		TopButtonsSeparation = BottomButtonsSeparation;
	}
}

internal static class CardEditorPresetPanelTunerHooks
{
	private const string ToggleName = "CardEditorPresetPanelTunerToggle";
	private const string PanelName = "CardEditorPresetPanelTunerPanel";
	private const string OpenMetaKey = "card_editor_preset_panel_tuner_open";
	private const string CoordsOpenMetaKey = "card_editor_preset_panel_tuner_coords_open";
	private const string PositionMetaKey = "card_editor_preset_panel_tuner_position";
	private const string DragHandleName = "CardEditorPresetPanelTunerDragHandle";
	private const string DragActiveMetaKey = "card_editor_preset_panel_tuner_drag_active";
	private const string DragOffsetMetaKey = "card_editor_preset_panel_tuner_drag_offset";
	private static string _liveStatus = "Active=None";

	public static void Sync(NCardLibrary library, bool shouldShow)
	{
		if (!CardEditorPresetPanelTuning.TunerEnabled || library == null)
		{
			return;
		}

		Button? toggleButton = library.GetNodeOrNull<Button>(ToggleName);
		PanelContainer? panel = library.GetNodeOrNull<PanelContainer>(PanelName);
		if (shouldShow)
		{
			if (toggleButton == null)
			{
				toggleButton = CreateToggleButton(library);
				library.AddChild(toggleButton);
			}

			if (panel == null)
			{
				panel = CreatePanel(library);
				library.AddChild(panel);
			}

			toggleButton.Visible = true;
			panel.Visible = IsOpen(library);
			RefreshPanel(panel);
		}
		else
		{
			if (toggleButton != null)
			{
				toggleButton.Visible = false;
			}

			if (panel != null)
			{
				panel.Visible = false;
			}
		}
	}

	private static Button CreateToggleButton(NCardLibrary library)
	{
		Button button = new Button
		{
			Name = ToggleName,
			Text = CardEditorLoc.T("tuner.presetPopup.title", "Preset Popup Tuner"),
			FocusMode = Control.FocusModeEnum.None,
			MouseFilter = Control.MouseFilterEnum.Stop,
			ZIndex = 66
		};
		button.AnchorLeft = 0f;
		button.AnchorTop = 0f;
		button.AnchorRight = 0f;
		button.AnchorBottom = 0f;
		button.OffsetLeft = CardEditorPresetPanelTuning.TunerOffsetX;
		button.OffsetTop = CardEditorPresetPanelTuning.TunerOffsetY - 46f;
		button.OffsetRight = CardEditorPresetPanelTuning.TunerOffsetX + 220f;
		button.OffsetBottom = CardEditorPresetPanelTuning.TunerOffsetY - 8f;
		button.Pressed += () =>
		{
			library.SetMeta(OpenMetaKey, !IsOpen(library));
			Sync(library, shouldShow: true);
		};
		return button;
	}

	private static PanelContainer CreatePanel(NCardLibrary library)
	{
		PanelContainer panel = new PanelContainer
		{
			Name = PanelName,
			ZIndex = 66,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		panel.AnchorLeft = 0f;
		panel.AnchorTop = 0f;
		panel.AnchorRight = 0f;
		panel.AnchorBottom = 0f;

		StyleBoxFlat style = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.07f, 0.06f, 0.95f),
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
		panel.AddThemeStyleboxOverride("panel", style);

		MarginContainer margin = new MarginContainer();
		margin.Name = "Margin";
		margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_top", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		panel.AddChild(margin);

		VBoxContainer root = new VBoxContainer();
		root.Name = "Root";
		root.AddThemeConstantOverride("separation", 10);
		margin.AddChild(root);

		Control dragHandle = new Control
		{
			Name = DragHandleName,
			CustomMinimumSize = new Vector2(0f, 36f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		dragHandle.GuiInput += @event => HandlePanelDragInput(library, panel, @event);
		root.AddChild(dragHandle);

		Label title = CreateInfoLabel(CardEditorLoc.T("tuner.presetPopup.title", "Preset Popup Tuner"), 24);
		title.Name = "Title";
		title.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		title.MouseFilter = Control.MouseFilterEnum.Ignore;
		dragHandle.AddChild(title);

		CheckBox linkRowsCheck = new CheckBox
		{
			Name = "LinkRowsCheck",
			Text = CardEditorLoc.T("tuner.presetPopup.linkRows", "Link button rows"),
			FocusMode = Control.FocusModeEnum.None,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		linkRowsCheck.AddThemeFontSizeOverride("font_size", 18);
		linkRowsCheck.AddThemeColorOverride("font_color", StsColors.cream);
		linkRowsCheck.AddThemeColorOverride("font_outline_color", StsColors.transparentBlack);
		linkRowsCheck.AddThemeConstantOverride("outline_size", 6);
		linkRowsCheck.Toggled += enabled =>
		{
			CardEditorPresetPanelTuning.LinkButtonRows = enabled;
			if (enabled)
			{
				CardEditorPresetPanelTuning.SyncBottomActionRowFromTop();
			}
			RefreshLibrary(library);
		};
		root.AddChild(linkRowsCheck);

		Label copyHint = CreateInfoLabel(CardEditorLoc.T("tuner.copyCoordinateLines", "Copy the coordinate lines below and send them back."), 15);
		copyHint.Name = "CopyHint";
		copyHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		root.AddChild(copyHint);

		Label readout = CreateInfoLabel(string.Empty, 17);
		readout.Name = "Readout";
		readout.AutowrapMode = TextServer.AutowrapMode.Off;
		root.AddChild(readout);

		Button coordsToggle = new Button
		{
			Name = "CoordsToggle",
			Text = CardEditorLoc.T("button.showCoordinates", "Show Coordinates"),
			FocusMode = Control.FocusModeEnum.None,
			MouseFilter = Control.MouseFilterEnum.Stop,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		coordsToggle.Pressed += () =>
		{
			library.SetMeta(CoordsOpenMetaKey, !IsCoordsOpen(library));
			RefreshPanel(panel);
		};
		root.AddChild(coordsToggle);

		VBoxContainer coordsBox = new VBoxContainer
		{
			Name = "CoordsBox",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		coordsBox.AddThemeConstantOverride("separation", 6);
		coordsBox.AddChild(CreateReadoutField("RectReadout"));
		coordsBox.AddChild(CreateReadoutField("PopupReadout"));
		coordsBox.AddChild(CreateReadoutField("MarginsReadout"));
		coordsBox.AddChild(CreateReadoutField("MiddleReadout"));
		coordsBox.AddChild(CreateReadoutField("BottomReadout"));
		coordsBox.AddChild(CreateReadoutField("SlotReadout"));
		coordsBox.AddChild(CreateReadoutField("TextTopReadout"));
		coordsBox.AddChild(CreateReadoutField("TextBottomReadout"));
		root.AddChild(coordsBox);

		ScrollContainer scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0f, 360f)
		};
		root.AddChild(scroll);

		VBoxContainer controls = new VBoxContainer();
		controls.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(controls);

		AddSection(controls, "Popup");
		controls.AddChild(CreateAdjustRow("Top",
			() => { CardEditorPresetPanelTuning.PopupTop -= 10f; RefreshLibrary(library); },
			() => { CardEditorPresetPanelTuning.PopupTop -= 1f; RefreshLibrary(library); },
			() => { CardEditorPresetPanelTuning.PopupTop += 1f; RefreshLibrary(library); },
			() => { CardEditorPresetPanelTuning.PopupTop += 10f; RefreshLibrary(library); }));
		controls.AddChild(CreateAdjustRow("Right",
			() => { CardEditorPresetPanelTuning.PopupRightInset -= 10f; RefreshLibrary(library); },
			() => { CardEditorPresetPanelTuning.PopupRightInset -= 1f; RefreshLibrary(library); },
			() => { CardEditorPresetPanelTuning.PopupRightInset += 1f; RefreshLibrary(library); },
			() => { CardEditorPresetPanelTuning.PopupRightInset += 10f; RefreshLibrary(library); }));
		controls.AddChild(CreateAdjustRow("Width",
			() => { CardEditorPresetPanelTuning.PopupWidth = Mathf.Max(240f, CardEditorPresetPanelTuning.PopupWidth - 10f); RefreshLibrary(library); },
			() => { CardEditorPresetPanelTuning.PopupWidth = Mathf.Max(240f, CardEditorPresetPanelTuning.PopupWidth - 1f); RefreshLibrary(library); },
			() => { CardEditorPresetPanelTuning.PopupWidth += 1f; RefreshLibrary(library); },
			() => { CardEditorPresetPanelTuning.PopupWidth += 10f; RefreshLibrary(library); }));
		controls.AddChild(CreateAdjustRow("Height",
			() => { CardEditorPresetPanelTuning.PopupHeight = Mathf.Max(220f, CardEditorPresetPanelTuning.PopupHeight - 10f); RefreshLibrary(library); },
			() => { CardEditorPresetPanelTuning.PopupHeight = Mathf.Max(220f, CardEditorPresetPanelTuning.PopupHeight - 1f); RefreshLibrary(library); },
			() => { CardEditorPresetPanelTuning.PopupHeight += 1f; RefreshLibrary(library); },
			() => { CardEditorPresetPanelTuning.PopupHeight += 10f; RefreshLibrary(library); }));

		AddSection(controls, "Margins");
		AddIntAdjustRow(controls, "ML", () => ref CardEditorPresetPanelTuning.MarginLeft, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "MT", () => ref CardEditorPresetPanelTuning.MarginTop, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "MR", () => ref CardEditorPresetPanelTuning.MarginRight, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "MB", () => ref CardEditorPresetPanelTuning.MarginBottom, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "Gap", () => ref CardEditorPresetPanelTuning.ContentSeparation, library, -2, -1, +1, +2, 0);

		AddSection(controls, "Dropdown");
		AddIntAdjustRow(controls, "DL", () => ref CardEditorPresetPanelTuning.DropdownInsetLeft, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "DR", () => ref CardEditorPresetPanelTuning.DropdownInsetRight, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "DY", () => ref CardEditorPresetPanelTuning.DropdownOffsetY, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "DH", () => ref CardEditorPresetPanelTuning.DropdownHeight, library, -10, -1, +1, +10, 30);

		AddSection(controls, "Top Buttons");
		AddIntAdjustRow(controls, "TL", () => ref CardEditorPresetPanelTuning.TopButtonsInsetLeft, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "TR", () => ref CardEditorPresetPanelTuning.TopButtonsInsetRight, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "TY", () => ref CardEditorPresetPanelTuning.TopButtonsOffsetY, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "TS", () => ref CardEditorPresetPanelTuning.TopButtonsSeparation, library, -4, -1, +1, +4, 0);
		AddIntAdjustRow(controls, "TW", () => ref CardEditorPresetPanelTuning.TopButtonWidth, library, -10, -1, +1, +10, 80);
		AddIntAdjustRow(controls, "TH", () => ref CardEditorPresetPanelTuning.TopButtonHeight, library, -10, -1, +1, +10, 36);
		AddIntAdjustRow(controls, "LFS", () => ref CardEditorPresetPanelTuning.LoadButtonTextSize, library, -4, -1, +1, +4, 8);
		AddIntAdjustRow(controls, "LTX", () => ref CardEditorPresetPanelTuning.LoadButtonTextOffsetX, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "LTY", () => ref CardEditorPresetPanelTuning.LoadButtonTextOffsetY, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "DFS", () => ref CardEditorPresetPanelTuning.DeleteButtonTextSize, library, -4, -1, +1, +4, 8);
		AddIntAdjustRow(controls, "DTX", () => ref CardEditorPresetPanelTuning.DeleteButtonTextOffsetX, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "DTY", () => ref CardEditorPresetPanelTuning.DeleteButtonTextOffsetY, library, -10, -1, +1, +10);

		AddSection(controls, "Checkbox");
		AddIntAdjustRow(controls, "CL", () => ref CardEditorPresetPanelTuning.CheckboxInsetLeft, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "CR", () => ref CardEditorPresetPanelTuning.CheckboxInsetRight, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "CY", () => ref CardEditorPresetPanelTuning.CheckboxOffsetY, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "CH", () => ref CardEditorPresetPanelTuning.CheckboxHeight, library, -10, -1, +1, +10, 24);
		AddIntAdjustRow(controls, "CS", () => ref CardEditorPresetPanelTuning.CheckboxSeparation, library, -2, -1, +1, +2, 0);
		AddIntAdjustRow(controls, "CZ", () => ref CardEditorPresetPanelTuning.CheckboxVisualScale, library, -10, -1, +1, +10, 20);

		AddSection(controls, "Name Field");
		AddIntAdjustRow(controls, "NL", () => ref CardEditorPresetPanelTuning.NameInsetLeft, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "NR", () => ref CardEditorPresetPanelTuning.NameInsetRight, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "NY", () => ref CardEditorPresetPanelTuning.NameOffsetY, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "NH", () => ref CardEditorPresetPanelTuning.NameHeight, library, -10, -1, +1, +10, 30);

		AddSection(controls, "Creator Slot Count");
		AddIntAdjustRow(controls, "SCL", () => ref CardEditorPresetPanelTuning.SlotSpinnerInsetLeft, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "SCR", () => ref CardEditorPresetPanelTuning.SlotSpinnerInsetRight, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "SCY", () => ref CardEditorPresetPanelTuning.SlotSpinnerOffsetY, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "SCS", () => ref CardEditorPresetPanelTuning.SlotSpinnerSeparation, library, -4, -1, +1, +4, 0);
		AddIntAdjustRow(controls, "SFX", () => ref CardEditorPresetPanelTuning.SlotFieldOffsetX, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "SFY", () => ref CardEditorPresetPanelTuning.SlotFieldOffsetY, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "SFW", () => ref CardEditorPresetPanelTuning.SlotFieldWidth, library, -10, -1, +1, +10, 48);
		AddIntAdjustRow(controls, "SFH", () => ref CardEditorPresetPanelTuning.SlotFieldHeight, library, -10, -1, +1, +10, 24);
		AddIntAdjustRow(controls, "SAX", () => ref CardEditorPresetPanelTuning.SlotArrowsOffsetX, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "SAY", () => ref CardEditorPresetPanelTuning.SlotArrowsOffsetY, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "SAW", () => ref CardEditorPresetPanelTuning.SlotArrowColumnWidth, library, -10, -1, +1, +10, 20);
		AddIntAdjustRow(controls, "SAH", () => ref CardEditorPresetPanelTuning.SlotArrowColumnHeight, library, -10, -1, +1, +10, 24);
		AddIntAdjustRow(controls, "SAS", () => ref CardEditorPresetPanelTuning.SlotArrowSeparation, library, -4, -1, +1, +4, 0);
		AddIntAdjustRow(controls, "SBW", () => ref CardEditorPresetPanelTuning.SlotArrowButtonWidth, library, -10, -1, +1, +10, 16);
		AddIntAdjustRow(controls, "SBH", () => ref CardEditorPresetPanelTuning.SlotArrowButtonHeight, library, -10, -1, +1, +10, 12);

		AddSection(controls, "Bottom Buttons");
		AddIntAdjustRow(controls, "BL", () => ref CardEditorPresetPanelTuning.BottomButtonsInsetLeft, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "BR", () => ref CardEditorPresetPanelTuning.BottomButtonsInsetRight, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "BY", () => ref CardEditorPresetPanelTuning.BottomButtonsOffsetY, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "BS", () => ref CardEditorPresetPanelTuning.BottomButtonsSeparation, library, -4, -1, +1, +4, 0);
		AddIntAdjustRow(controls, "SW", () => ref CardEditorPresetPanelTuning.SaveButtonWidth, library, -10, -1, +1, +10, 60);
		AddIntAdjustRow(controls, "RW", () => ref CardEditorPresetPanelTuning.RevertButtonWidth, library, -10, -1, +1, +10, 120);
		AddIntAdjustRow(controls, "BH", () => ref CardEditorPresetPanelTuning.BottomButtonHeight, library, -10, -1, +1, +10, 36);
		AddIntAdjustRow(controls, "SFS", () => ref CardEditorPresetPanelTuning.SaveButtonTextSize, library, -4, -1, +1, +4, 8);
		AddIntAdjustRow(controls, "STX", () => ref CardEditorPresetPanelTuning.SaveButtonTextOffsetX, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "STY", () => ref CardEditorPresetPanelTuning.SaveButtonTextOffsetY, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "RFS", () => ref CardEditorPresetPanelTuning.RevertButtonTextSize, library, -4, -1, +1, +4, 8);
		AddIntAdjustRow(controls, "RTX", () => ref CardEditorPresetPanelTuning.RevertButtonTextOffsetX, library, -10, -1, +1, +10);
		AddIntAdjustRow(controls, "RTY", () => ref CardEditorPresetPanelTuning.RevertButtonTextOffsetY, library, -10, -1, +1, +10);

		HBoxContainer actions = new HBoxContainer();
		actions.AddThemeConstantOverride("separation", 8);
		root.AddChild(actions);

		Button centerText = new Button { Text = CardEditorLoc.T("button.centerText", "Center Text"), FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		centerText.Pressed += () =>
		{
			CenterButtonTextOffsets();
			RefreshLibrary(library);
		};
		actions.AddChild(centerText);

		Button reset = new Button { Text = CardEditorLoc.T("button.reset", "Reset"), FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		reset.Pressed += () =>
		{
			CardEditorPresetPanelTuning.Reset();
			RefreshLibrary(library);
		};
		actions.AddChild(reset);

		Button close = new Button { Text = CardEditorLoc.T("button.close", "Close"), FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		close.Pressed += () =>
		{
			library.SetMeta(OpenMetaKey, false);
			Sync(library, shouldShow: true);
		};
		actions.AddChild(close);

		return panel;
	}

	private static void AddSection(VBoxContainer root, string title)
	{
		Label label = CreateInfoLabel(title, 20);
		root.AddChild(label);
	}

	private static Label CreateInfoLabel(string text, int size)
	{
		Label label = new Label { Text = text };
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", StsColors.cream);
		label.AddThemeColorOverride("font_outline_color", StsColors.transparentBlack);
		label.AddThemeConstantOverride("outline_size", 8);
		return label;
	}

	private static LineEdit CreateReadoutField(string name)
	{
		LineEdit field = new LineEdit
		{
			Name = name,
			Editable = false,
			FocusMode = Control.FocusModeEnum.Click,
			MouseFilter = Control.MouseFilterEnum.Stop,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SelectAllOnFocus = true,
			CustomMinimumSize = new Vector2(0f, 34f)
		};
		field.AddThemeFontSizeOverride("font_size", 16);
		field.AddThemeColorOverride("font_color", StsColors.cream);
		field.AddThemeColorOverride("font_outline_color", StsColors.transparentBlack);
		field.AddThemeConstantOverride("outline_size", 6);
		return field;
	}

	private delegate ref int IntRefAccessor();

	private static void AddIntAdjustRow(VBoxContainer root, string axis, IntRefAccessor accessor, NCardLibrary library, int bigMinus, int smallMinus, int smallPlus, int bigPlus, int min = -9999)
	{
		root.AddChild(CreateAdjustRow(
			axis,
			() => { ref int value = ref accessor(); value = Math.Max(min, value + bigMinus); ApplyLinkedRowRule(axis); RefreshLibrary(library); },
			() => { ref int value = ref accessor(); value = Math.Max(min, value + smallMinus); ApplyLinkedRowRule(axis); RefreshLibrary(library); },
			() => { ref int value = ref accessor(); value += smallPlus; ApplyLinkedRowRule(axis); RefreshLibrary(library); },
			() => { ref int value = ref accessor(); value += bigPlus; ApplyLinkedRowRule(axis); RefreshLibrary(library); }));
	}

	private static void ApplyLinkedRowRule(string axis)
	{
		if (!CardEditorPresetPanelTuning.LinkButtonRows)
		{
			return;
		}

		if (axis is "TL" or "TR" or "TY" or "TS")
		{
			CardEditorPresetPanelTuning.SyncBottomActionRowFromTop();
		}
		else if (axis is "BL" or "BR" or "BY" or "BS")
		{
			CardEditorPresetPanelTuning.SyncTopActionRowFromBottom();
		}
	}

	private static HBoxContainer CreateAdjustRow(string axis, Action onBigMinus, Action onSmallMinus, Action onSmallPlus, Action onBigPlus)
	{
		HBoxContainer row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);

		Label label = new Label
		{
			Text = axis,
			CustomMinimumSize = new Vector2(54f, 32f),
			VerticalAlignment = VerticalAlignment.Center
		};
		label.AddThemeFontSizeOverride("font_size", 18);
		label.AddThemeColorOverride("font_color", StsColors.cream);
		row.AddChild(label);

		row.AddChild(CreateAdjustButton("-10", onBigMinus));
		row.AddChild(CreateAdjustButton("-1", onSmallMinus));
		row.AddChild(CreateAdjustButton("+1", onSmallPlus));
		row.AddChild(CreateAdjustButton("+10", onBigPlus));
		return row;
	}

	private static Button CreateAdjustButton(string text, Action onPressed)
	{
		Button button = new Button
		{
			Text = text,
			FocusMode = Control.FocusModeEnum.None,
			CustomMinimumSize = new Vector2(58f, 32f)
		};
		button.Pressed += onPressed;
		return button;
	}

	private static void RefreshPanel(PanelContainer panel)
	{
		NCardLibrary? library = panel.GetParent() as NCardLibrary;
		Vector2 position = library != null ? GetPanelPosition(library) : new Vector2(CardEditorPresetPanelTuning.TunerOffsetX, CardEditorPresetPanelTuning.TunerOffsetY);
		panel.OffsetLeft = position.X;
		panel.OffsetTop = position.Y;
		panel.OffsetRight = position.X + 470f;
		panel.OffsetBottom = position.Y + 860f;

		Label? readout = panel.GetNodeOrNull<Label>("Margin/Root/Readout");
		Button? coordsToggle = panel.GetNodeOrNull<Button>("Margin/Root/CoordsToggle");
		VBoxContainer? coordsBox = panel.GetNodeOrNull<VBoxContainer>("Margin/Root/CoordsBox");
		CheckBox? linkRowsCheck = panel.GetNodeOrNull<CheckBox>("Margin/Root/LinkRowsCheck");
		LineEdit? rectReadout = panel.GetNodeOrNull<LineEdit>("Margin/Root/CoordsBox/RectReadout");
		LineEdit? popupReadout = panel.GetNodeOrNull<LineEdit>("Margin/Root/CoordsBox/PopupReadout");
		LineEdit? marginsReadout = panel.GetNodeOrNull<LineEdit>("Margin/Root/CoordsBox/MarginsReadout");
		LineEdit? middleReadout = panel.GetNodeOrNull<LineEdit>("Margin/Root/CoordsBox/MiddleReadout");
		LineEdit? bottomReadout = panel.GetNodeOrNull<LineEdit>("Margin/Root/CoordsBox/BottomReadout");
		LineEdit? slotReadout = panel.GetNodeOrNull<LineEdit>("Margin/Root/CoordsBox/SlotReadout");
		LineEdit? textTopReadout = panel.GetNodeOrNull<LineEdit>("Margin/Root/CoordsBox/TextTopReadout");
		LineEdit? textBottomReadout = panel.GetNodeOrNull<LineEdit>("Margin/Root/CoordsBox/TextBottomReadout");
		if (readout != null)
		{
			Rect2 actualRect = default;
			NCardEditorPresetPanel? presetPanel = panel.GetParent()?.GetNodeOrNull<NCardEditorPresetPanel>("CardEditorPresetPanel");
			if (presetPanel != null && GodotObject.IsInstanceValid(presetPanel))
			{
				actualRect = presetPanel.GetGlobalRect();
			}

			string rectLine = string.Format(
				CultureInfo.InvariantCulture,
				"ACTUAL X={0} Y={1} W={2} H={3}",
				Mathf.RoundToInt(actualRect.Position.X),
				Mathf.RoundToInt(actualRect.Position.Y),
				Mathf.RoundToInt(actualRect.Size.X),
				Mathf.RoundToInt(actualRect.Size.Y));

			string popupLine = string.Format(
				CultureInfo.InvariantCulture,
				"POPUP Top={0} Right={1} Width={2} Height={3} LinkRows={4}",
				Mathf.RoundToInt(CardEditorPresetPanelTuning.PopupTop),
				Mathf.RoundToInt(CardEditorPresetPanelTuning.PopupRightInset),
				Mathf.RoundToInt(CardEditorPresetPanelTuning.PopupWidth),
				Mathf.RoundToInt(CardEditorPresetPanelTuning.PopupHeight),
				CardEditorPresetPanelTuning.LinkButtonRows ? "On" : "Off");

			string marginsLine = string.Format(
				CultureInfo.InvariantCulture,
				"MARGIN L={0} T={1} R={2} B={3} Gap={4} | DROPDOWN L={5} R={6} Y={7} H={8}",
				CardEditorPresetPanelTuning.MarginLeft,
				CardEditorPresetPanelTuning.MarginTop,
				CardEditorPresetPanelTuning.MarginRight,
				CardEditorPresetPanelTuning.MarginBottom,
				CardEditorPresetPanelTuning.ContentSeparation,
				CardEditorPresetPanelTuning.DropdownInsetLeft,
				CardEditorPresetPanelTuning.DropdownInsetRight,
				CardEditorPresetPanelTuning.DropdownOffsetY,
				CardEditorPresetPanelTuning.DropdownHeight);

			string middleLine = string.Format(
				CultureInfo.InvariantCulture,
				"TOP L={0} R={1} Y={2} S={3} W={4} H={5} LX={6} LY={7} DX={8} DY={9} | CHECKBOX L={10} R={11} Y={12} H={13} S={14} Z={15}",
				CardEditorPresetPanelTuning.TopButtonsInsetLeft,
				CardEditorPresetPanelTuning.TopButtonsInsetRight,
				CardEditorPresetPanelTuning.TopButtonsOffsetY,
				CardEditorPresetPanelTuning.TopButtonsSeparation,
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
				CardEditorPresetPanelTuning.CheckboxSeparation,
				CardEditorPresetPanelTuning.CheckboxVisualScale);

			string bottomLine = string.Format(
				CultureInfo.InvariantCulture,
				"NAME L={0} R={1} Y={2} H={3} | BOTTOM L={4} R={5} Y={6} S={7} SaveW={8} ResetW={9} H={10} SX={11} SY={12} RX={13} RY={14}",
				CardEditorPresetPanelTuning.NameInsetLeft,
				CardEditorPresetPanelTuning.NameInsetRight,
				CardEditorPresetPanelTuning.NameOffsetY,
				CardEditorPresetPanelTuning.NameHeight,
				CardEditorPresetPanelTuning.BottomButtonsInsetLeft,
				CardEditorPresetPanelTuning.BottomButtonsInsetRight,
				CardEditorPresetPanelTuning.BottomButtonsOffsetY,
				CardEditorPresetPanelTuning.BottomButtonsSeparation,
				CardEditorPresetPanelTuning.SaveButtonWidth,
				CardEditorPresetPanelTuning.RevertButtonWidth,
				CardEditorPresetPanelTuning.BottomButtonHeight,
				CardEditorPresetPanelTuning.SaveButtonOffsetX,
				CardEditorPresetPanelTuning.SaveButtonOffsetY,
				CardEditorPresetPanelTuning.RevertButtonOffsetX,
				CardEditorPresetPanelTuning.RevertButtonOffsetY);

			string slotLine = string.Format(
				CultureInfo.InvariantCulture,
				"SLOT GROUP L={0} R={1} Y={2} S={3} | FIELD X={4} Y={5} W={6} H={7} | ARROWS X={8} Y={9} W={10} H={11} S={12} BW={13} BH={14}",
				CardEditorPresetPanelTuning.SlotSpinnerInsetLeft,
				CardEditorPresetPanelTuning.SlotSpinnerInsetRight,
				CardEditorPresetPanelTuning.SlotSpinnerOffsetY,
				CardEditorPresetPanelTuning.SlotSpinnerSeparation,
				CardEditorPresetPanelTuning.SlotFieldOffsetX,
				CardEditorPresetPanelTuning.SlotFieldOffsetY,
				CardEditorPresetPanelTuning.SlotFieldWidth,
				CardEditorPresetPanelTuning.SlotFieldHeight,
				CardEditorPresetPanelTuning.SlotArrowsOffsetX,
				CardEditorPresetPanelTuning.SlotArrowsOffsetY,
				CardEditorPresetPanelTuning.SlotArrowColumnWidth,
				CardEditorPresetPanelTuning.SlotArrowColumnHeight,
				CardEditorPresetPanelTuning.SlotArrowSeparation,
				CardEditorPresetPanelTuning.SlotArrowButtonWidth,
				CardEditorPresetPanelTuning.SlotArrowButtonHeight);

			string textTopLine = string.Format(
				CultureInfo.InvariantCulture,
				"LOAD FS={0} TX={1} TY={2} | DELETE FS={3} TX={4} TY={5}",
				CardEditorPresetPanelTuning.LoadButtonTextSize,
				CardEditorPresetPanelTuning.LoadButtonTextOffsetX,
				CardEditorPresetPanelTuning.LoadButtonTextOffsetY,
				CardEditorPresetPanelTuning.DeleteButtonTextSize,
				CardEditorPresetPanelTuning.DeleteButtonTextOffsetX,
				CardEditorPresetPanelTuning.DeleteButtonTextOffsetY);

			string textBottomLine = string.Format(
				CultureInfo.InvariantCulture,
				"SAVE FS={0} TX={1} TY={2} | RESET FS={3} TX={4} TY={5}",
				CardEditorPresetPanelTuning.SaveButtonTextSize,
				CardEditorPresetPanelTuning.SaveButtonTextOffsetX,
				CardEditorPresetPanelTuning.SaveButtonTextOffsetY,
				CardEditorPresetPanelTuning.RevertButtonTextSize,
				CardEditorPresetPanelTuning.RevertButtonTextOffsetX,
				CardEditorPresetPanelTuning.RevertButtonTextOffsetY);

			readout.Text = _liveStatus;

			if (rectReadout != null)
			{
				rectReadout.Text = rectLine;
			}

			bool coordsOpen = library != null && IsCoordsOpen(library);
			if (coordsToggle != null)
			{
				coordsToggle.Text = coordsOpen
					? CardEditorLoc.T("button.hideCoordinates", "Hide Coordinates")
					: CardEditorLoc.T("button.showCoordinates", "Show Coordinates");
			}
			if (coordsBox != null)
			{
				coordsBox.Visible = coordsOpen;
			}

			if (linkRowsCheck != null && linkRowsCheck.ButtonPressed != CardEditorPresetPanelTuning.LinkButtonRows)
			{
				linkRowsCheck.SetPressedNoSignal(CardEditorPresetPanelTuning.LinkButtonRows);
			}

			if (popupReadout != null)
			{
				popupReadout.Text = popupLine;
			}

			if (marginsReadout != null)
			{
				marginsReadout.Text = marginsLine;
			}

			if (middleReadout != null)
			{
				middleReadout.Text = middleLine;
			}

			if (bottomReadout != null)
			{
				bottomReadout.Text = bottomLine;
			}

			if (slotReadout != null)
			{
				slotReadout.Text = slotLine;
			}

			if (textTopReadout != null)
			{
				textTopReadout.Text = textTopLine;
			}

			if (textBottomReadout != null)
			{
				textBottomReadout.Text = textBottomLine;
			}
		}
	}

	internal static bool IsOpenFor(NCardLibrary library)
	{
		return CardEditorPresetPanelTuning.TunerEnabled
			&& library.HasMeta(OpenMetaKey)
			&& library.GetMeta(OpenMetaKey).AsBool();
	}

	internal static void RefreshFor(NCardLibrary library, string? liveStatus = null)
	{
		if (liveStatus != null)
		{
			_liveStatus = liveStatus;
		}

		PanelContainer? panel = library.GetNodeOrNull<PanelContainer>(PanelName);
		if (panel != null)
		{
			RefreshPanel(panel);
		}
	}

	private static void RefreshLibrary(NCardLibrary library)
	{
		CardEditorPresetPanelHooks.Sync(library);
		if (library.GetNodeOrNull<NCardEditorPresetPanel>("CardEditorPresetPanel") is { } panel)
		{
			panel.ApplyLayoutTuning();
		}
		RefreshFor(library);
		Sync(library, shouldShow: true);
	}

	private static bool IsOpen(NCardLibrary library)
	{
		return IsOpenFor(library);
	}

	private static bool IsCoordsOpen(NCardLibrary library)
	{
		return library.HasMeta(CoordsOpenMetaKey) && library.GetMeta(CoordsOpenMetaKey).AsBool();
	}

	private static void CenterButtonTextOffsets()
	{
		CardEditorPresetPanelTuning.LoadButtonTextOffsetX = 0;
		CardEditorPresetPanelTuning.LoadButtonTextOffsetY = 0;
		CardEditorPresetPanelTuning.DeleteButtonTextOffsetX = 0;
		CardEditorPresetPanelTuning.DeleteButtonTextOffsetY = 0;
		CardEditorPresetPanelTuning.SaveButtonTextOffsetX = 0;
		CardEditorPresetPanelTuning.SaveButtonTextOffsetY = 0;
		CardEditorPresetPanelTuning.RevertButtonTextOffsetX = 0;
		CardEditorPresetPanelTuning.RevertButtonTextOffsetY = 0;
	}

	private static Vector2 GetPanelPosition(NCardLibrary library)
	{
		return library.HasMeta(PositionMetaKey)
			? library.GetMeta(PositionMetaKey).AsVector2()
			: new Vector2(CardEditorPresetPanelTuning.TunerOffsetX, CardEditorPresetPanelTuning.TunerOffsetY);
	}

	private static void SetPanelPosition(NCardLibrary library, PanelContainer panel, Vector2 position)
	{
		Vector2 viewportSize = library.GetViewportRect().Size;
		Vector2 panelSize = new Vector2(470f, 860f);
		float maxX = Mathf.Max(0f, viewportSize.X - panelSize.X);
		float maxY = Mathf.Max(0f, viewportSize.Y - panelSize.Y);
		Vector2 clamped = new Vector2(
			Mathf.Clamp(position.X, 0f, maxX),
			Mathf.Clamp(position.Y, 0f, maxY));
		library.SetMeta(PositionMetaKey, clamped);
		panel.OffsetLeft = clamped.X;
		panel.OffsetTop = clamped.Y;
		panel.OffsetRight = clamped.X + panelSize.X;
		panel.OffsetBottom = clamped.Y + panelSize.Y;
	}

	private static void HandlePanelDragInput(NCardLibrary library, PanelContainer panel, InputEvent @event)
	{
		switch (@event)
		{
			case InputEventMouseButton button when button.ButtonIndex == MouseButton.Left && button.Pressed:
				library.SetMeta(DragActiveMetaKey, true);
				library.SetMeta(DragOffsetMetaKey, button.GlobalPosition - panel.GlobalPosition);
				panel.AcceptEvent();
				break;
			case InputEventMouseButton button when button.ButtonIndex == MouseButton.Left && !button.Pressed:
				library.SetMeta(DragActiveMetaKey, false);
				panel.AcceptEvent();
				break;
			case InputEventMouseMotion motion when library.HasMeta(DragActiveMetaKey) && library.GetMeta(DragActiveMetaKey).AsBool():
				Vector2 offset = library.HasMeta(DragOffsetMetaKey) ? library.GetMeta(DragOffsetMetaKey).AsVector2() : Vector2.Zero;
				SetPanelPosition(library, panel, motion.GlobalPosition - offset);
				panel.AcceptEvent();
				break;
		}
	}
}
