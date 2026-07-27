using System;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorBaseDeckBookmarkTuning
{
	private const float MinWidth = 120f;
	private const float MaxWidth = 360f;
	private const float MinHeight = 10f;
	private const float MaxHeight = 220f;

	public static float PositionXOffset { get; private set; } = CardEditorBaseDeckBookmarkLayout.PositionXOffsetFromBackButton;
	public static float PositionYOffset { get; private set; } = CardEditorBaseDeckBookmarkLayout.PositionYOffsetFromBackButton;
	public static float Width { get; private set; } = CardEditorBaseDeckBookmarkLayout.Width;
	public static float Height { get; private set; } = CardEditorBaseDeckBookmarkLayout.Height;
	public static float LabelOffsetX { get; private set; } = CardEditorBaseDeckBookmarkLayout.LabelOffsetXFromBookmark;
	public static float LabelOffsetY { get; private set; } = CardEditorBaseDeckBookmarkLayout.LabelOffsetYFromBookmark;
	public static float OutlineOffsetX { get; private set; } = CardEditorBaseDeckBookmarkLayout.OutlineOffsetXFromBookmark;
	public static float OutlineOffsetY { get; private set; } = CardEditorBaseDeckBookmarkLayout.OutlineOffsetYFromBookmark;
	public static float OutlineWidthAdjust { get; private set; } = CardEditorBaseDeckBookmarkLayout.OutlineWidthAdjustFromBookmark;
	public static float OutlineHeightAdjust { get; private set; } = CardEditorBaseDeckBookmarkLayout.OutlineHeightAdjustFromBookmark;
	public static float OutlineLeft => CardEditorBaseDeckBookmarkLayout.OutlineLeft + OutlineOffsetX - OutlineWidthAdjust * 0.5f;
	public static float OutlineTop => CardEditorBaseDeckBookmarkLayout.OutlineTop + OutlineOffsetY - OutlineHeightAdjust * 0.5f;
	public static float OutlineRight => CardEditorBaseDeckBookmarkLayout.OutlineRight + OutlineOffsetX + OutlineWidthAdjust * 0.5f;
	public static float OutlineBottom => CardEditorBaseDeckBookmarkLayout.OutlineBottom + OutlineOffsetY + OutlineHeightAdjust * 0.5f;

	public static void NudgePosition(float deltaX, float deltaY)
	{
		PositionXOffset += deltaX;
		PositionYOffset += deltaY;
		ApplyChange();
	}

	public static void NudgeSize(float deltaWidth, float deltaHeight)
	{
		Width = Mathf.Clamp(Width + deltaWidth, MinWidth, MaxWidth);
		Height = Mathf.Clamp(Height + deltaHeight, MinHeight, MaxHeight);
		ApplyChange();
	}

	public static void Reset()
	{
		PositionXOffset = CardEditorBaseDeckBookmarkLayout.PositionXOffsetFromBackButton;
		PositionYOffset = CardEditorBaseDeckBookmarkLayout.PositionYOffsetFromBackButton;
		Width = CardEditorBaseDeckBookmarkLayout.Width;
		Height = CardEditorBaseDeckBookmarkLayout.Height;
		LabelOffsetX = CardEditorBaseDeckBookmarkLayout.LabelOffsetXFromBookmark;
		LabelOffsetY = CardEditorBaseDeckBookmarkLayout.LabelOffsetYFromBookmark;
		OutlineOffsetX = CardEditorBaseDeckBookmarkLayout.OutlineOffsetXFromBookmark;
		OutlineOffsetY = CardEditorBaseDeckBookmarkLayout.OutlineOffsetYFromBookmark;
		OutlineWidthAdjust = CardEditorBaseDeckBookmarkLayout.OutlineWidthAdjustFromBookmark;
		OutlineHeightAdjust = CardEditorBaseDeckBookmarkLayout.OutlineHeightAdjustFromBookmark;
		ApplyChange();
	}

	public static void NudgeLabel(float deltaX, float deltaY)
	{
		LabelOffsetX += deltaX;
		LabelOffsetY += deltaY;
		ApplyChange();
	}

	public static void NudgeOutlinePosition(float deltaX, float deltaY)
	{
		OutlineOffsetX += deltaX;
		OutlineOffsetY += deltaY;
		ApplyChange();
	}

	public static void NudgeOutlineSize(float deltaWidth, float deltaHeight)
	{
		OutlineWidthAdjust += deltaWidth;
		OutlineHeightAdjust += deltaHeight;
		ApplyChange();
	}

	public static string Describe()
	{
		return $"X={PositionXOffset:0}  Y={PositionYOffset:0}  W={Width:0}  H={Height:0}  TX={LabelOffsetX:0}  TY={LabelOffsetY:0}  OX={OutlineOffsetX:0}  OY={OutlineOffsetY:0}  OW={OutlineWidthAdjust:0}  OH={OutlineHeightAdjust:0}";
	}

	public static string DescribeMultiline()
	{
		return $"X={PositionXOffset:0}  Y={PositionYOffset:0}  W={Width:0}  H={Height:0}\n"
			+ $"TX={LabelOffsetX:0}  TY={LabelOffsetY:0}\n"
			+ $"OX={OutlineOffsetX:0}  OY={OutlineOffsetY:0}  OW={OutlineWidthAdjust:0}  OH={OutlineHeightAdjust:0}";
	}

	private static void ApplyChange()
	{
		CardEditorMod.VerboseLog($"[CardEditor][BaseDeckBookmarkTuner] {Describe()}");
		CardEditorBaseDeckBookmarkHooks.RefreshLastLibrary();
	}
}

internal static class CardEditorBaseDeckBookmarkTunerHooks
{
	private const bool TunerEnabled = false;
	private const string ToggleButtonName = "CardEditorBaseDeckBookmarkTunerToggle";
	private const string PanelName = "CardEditorBaseDeckBookmarkTunerPanel";
	private const string ValueLabelName = "Values";
	private const string OpenMetaKey = "card_editor_base_deck_bookmark_tuner_open";
	private const string HeaderFontPath = "res://themes/kreon_bold_glyph_space_one.tres";
	private const string BodyFontPath = "res://themes/kreon_regular_glyph_space_one.tres";

	private static Font? _headerFont;
	private static Font? _bodyFont;

	public static void Sync(NCardLibrary library)
	{
		if (library == null)
		{
			return;
		}

		bool shouldShow = CardEditorUiState.IsEditorActive || CardEditorUiState.IsBaseDeckActive || CardEditorUiState.IsBaseDeckAddActive;
		Button? toggleButton = library.GetNodeOrNull<Button>(ToggleButtonName);
		PanelContainer? panel = library.GetNodeOrNull<PanelContainer>(PanelName);
		if (!TunerEnabled)
		{
			if (toggleButton != null)
			{
				toggleButton.Visible = false;
			}
			if (panel != null)
			{
				panel.Visible = false;
			}
			return;
		}

		#pragma warning disable CS0162 // Unreachable code — TunerEnabled is a const toggle
		if (!shouldShow)
		{
			if (toggleButton != null)
			{
				toggleButton.Visible = false;
			}
			if (panel != null)
			{
				panel.Visible = false;
			}
			return;
		}
		#pragma warning restore CS0162

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
		toggleButton.Text = IsOpen(library)
			? CardEditorLoc.T("button.hideBookmarkTuner", "Hide Bookmark Tuner")
			: CardEditorLoc.T("button.tuneBookmark", "Tune Bookmark");
		panel.Visible = IsOpen(library);
		RefreshPanel(panel);
	}

	public static void RefreshLastLibrary()
	{
		if (CardEditorUiState.TryGetLastLibrary(out NCardLibrary? library) && library != null)
		{
			Sync(library);
		}
	}

	private static Button CreateToggleButton(NCardLibrary library)
	{
		CardEditorGodotResourceCache.TryLoad(ref _bodyFont, BodyFontPath);

		Button button = new()
		{
			Name = ToggleButtonName,
			ZIndex = 120,
			MouseFilter = Control.MouseFilterEnum.Stop,
			FocusMode = Control.FocusModeEnum.None,
			AnchorLeft = 1f,
			AnchorTop = 0f,
			AnchorRight = 1f,
			AnchorBottom = 0f,
			GrowHorizontal = Control.GrowDirection.Begin,
			OffsetLeft = -220f,
			OffsetTop = 18f,
			OffsetRight = -20f,
			OffsetBottom = 56f,
			CustomMinimumSize = new Vector2(200f, 38f),
			Text = CardEditorLoc.T("button.tuneBookmark", "Tune Bookmark")
		};
		if (_bodyFont != null)
		{
			button.AddThemeFontOverride("font", _bodyFont);
		}
		button.AddThemeFontSizeOverride("font_size", 18);
		button.AddThemeColorOverride("font_color", StsColors.cream);
		button.Pressed += () => ToggleOpen(library);
		return button;
	}

	private static PanelContainer CreatePanel(NCardLibrary library)
	{
		CardEditorGodotResourceCache.TryLoad(ref _headerFont, HeaderFontPath);
		CardEditorGodotResourceCache.TryLoad(ref _bodyFont, BodyFontPath);

		PanelContainer panel = new()
		{
			Name = PanelName,
			ZIndex = 120,
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Stop,
			FocusMode = Control.FocusModeEnum.None,
			AnchorLeft = 1f,
			AnchorTop = 0f,
			AnchorRight = 1f,
			AnchorBottom = 0f,
			GrowHorizontal = Control.GrowDirection.Begin,
			OffsetLeft = -320f,
			OffsetTop = 64f,
			OffsetRight = -20f,
			OffsetBottom = 620f,
			CustomMinimumSize = new Vector2(300f, 556f)
		};

		StyleBoxFlat panelStyle = new()
		{
			BgColor = new Color(0.08f, 0.07f, 0.06f, 0.94f),
			BorderColor = new Color(0.82f, 0.73f, 0.46f, 1f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomLeft = 10,
			CornerRadiusBottomRight = 10
		};
		panel.AddThemeStyleboxOverride("panel", panelStyle);

		MarginContainer margin = new();
		margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_top", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		panel.AddChild(margin);

		VBoxContainer root = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		root.AddThemeConstantOverride("separation", 8);
		margin.AddChild(root);

		Label title = CreateLabel(CardEditorLoc.T("tuner.bookmark.title", "Bookmark Tuner"), header: true);
		root.AddChild(title);

		Label values = CreateLabel(string.Empty, header: false);
		values.Name = ValueLabelName;
		values.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		root.AddChild(values);

		root.AddChild(CreateAdjustRow("X", () => CardEditorBaseDeckBookmarkTuning.NudgePosition(-10f, 0f), () => CardEditorBaseDeckBookmarkTuning.NudgePosition(-1f, 0f), () => CardEditorBaseDeckBookmarkTuning.NudgePosition(1f, 0f), () => CardEditorBaseDeckBookmarkTuning.NudgePosition(10f, 0f)));
		root.AddChild(CreateAdjustRow("Y", () => CardEditorBaseDeckBookmarkTuning.NudgePosition(0f, -10f), () => CardEditorBaseDeckBookmarkTuning.NudgePosition(0f, -1f), () => CardEditorBaseDeckBookmarkTuning.NudgePosition(0f, 1f), () => CardEditorBaseDeckBookmarkTuning.NudgePosition(0f, 10f)));
		root.AddChild(CreateAdjustRow("W", () => CardEditorBaseDeckBookmarkTuning.NudgeSize(-10f, 0f), () => CardEditorBaseDeckBookmarkTuning.NudgeSize(-1f, 0f), () => CardEditorBaseDeckBookmarkTuning.NudgeSize(1f, 0f), () => CardEditorBaseDeckBookmarkTuning.NudgeSize(10f, 0f)));
		root.AddChild(CreateAdjustRow("H", () => CardEditorBaseDeckBookmarkTuning.NudgeSize(0f, -10f), () => CardEditorBaseDeckBookmarkTuning.NudgeSize(0f, -1f), () => CardEditorBaseDeckBookmarkTuning.NudgeSize(0f, 1f), () => CardEditorBaseDeckBookmarkTuning.NudgeSize(0f, 10f)));
		root.AddChild(CreateAdjustRow("TX", () => CardEditorBaseDeckBookmarkTuning.NudgeLabel(-10f, 0f), () => CardEditorBaseDeckBookmarkTuning.NudgeLabel(-1f, 0f), () => CardEditorBaseDeckBookmarkTuning.NudgeLabel(1f, 0f), () => CardEditorBaseDeckBookmarkTuning.NudgeLabel(10f, 0f)));
		root.AddChild(CreateAdjustRow("TY", () => CardEditorBaseDeckBookmarkTuning.NudgeLabel(0f, -10f), () => CardEditorBaseDeckBookmarkTuning.NudgeLabel(0f, -1f), () => CardEditorBaseDeckBookmarkTuning.NudgeLabel(0f, 1f), () => CardEditorBaseDeckBookmarkTuning.NudgeLabel(0f, 10f)));
		root.AddChild(CreateAdjustRow("OX", () => CardEditorBaseDeckBookmarkTuning.NudgeOutlinePosition(-10f, 0f), () => CardEditorBaseDeckBookmarkTuning.NudgeOutlinePosition(-1f, 0f), () => CardEditorBaseDeckBookmarkTuning.NudgeOutlinePosition(1f, 0f), () => CardEditorBaseDeckBookmarkTuning.NudgeOutlinePosition(10f, 0f)));
		root.AddChild(CreateAdjustRow("OY", () => CardEditorBaseDeckBookmarkTuning.NudgeOutlinePosition(0f, -10f), () => CardEditorBaseDeckBookmarkTuning.NudgeOutlinePosition(0f, -1f), () => CardEditorBaseDeckBookmarkTuning.NudgeOutlinePosition(0f, 1f), () => CardEditorBaseDeckBookmarkTuning.NudgeOutlinePosition(0f, 10f)));
		root.AddChild(CreateAdjustRow("OW", () => CardEditorBaseDeckBookmarkTuning.NudgeOutlineSize(-10f, 0f), () => CardEditorBaseDeckBookmarkTuning.NudgeOutlineSize(-1f, 0f), () => CardEditorBaseDeckBookmarkTuning.NudgeOutlineSize(1f, 0f), () => CardEditorBaseDeckBookmarkTuning.NudgeOutlineSize(10f, 0f)));
		root.AddChild(CreateAdjustRow("OH", () => CardEditorBaseDeckBookmarkTuning.NudgeOutlineSize(0f, -10f), () => CardEditorBaseDeckBookmarkTuning.NudgeOutlineSize(0f, -1f), () => CardEditorBaseDeckBookmarkTuning.NudgeOutlineSize(0f, 1f), () => CardEditorBaseDeckBookmarkTuning.NudgeOutlineSize(0f, 10f)));

		HBoxContainer footer = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		footer.AddThemeConstantOverride("separation", 8);
		footer.AddChild(CreateActionButton(CardEditorLoc.T("button.reset", "Reset"), CardEditorBaseDeckBookmarkTuning.Reset));
		footer.AddChild(CreateActionButton(CardEditorLoc.T("button.close", "Close"), () => SetOpen(library, false)));
		root.AddChild(footer);

		return panel;
	}

	private static HBoxContainer CreateAdjustRow(string axis, Action onBigMinus, Action onSmallMinus, Action onSmallPlus, Action onBigPlus)
	{
		HBoxContainer row = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		row.AddThemeConstantOverride("separation", 6);

		Label axisLabel = CreateLabel(axis, header: false);
		axisLabel.CustomMinimumSize = new Vector2(26f, 32f);
		axisLabel.HorizontalAlignment = HorizontalAlignment.Center;
		axisLabel.VerticalAlignment = VerticalAlignment.Center;
		row.AddChild(axisLabel);

		row.AddChild(CreateActionButton("-10", onBigMinus));
		row.AddChild(CreateActionButton("-1", onSmallMinus));
		row.AddChild(CreateActionButton("+1", onSmallPlus));
		row.AddChild(CreateActionButton("+10", onBigPlus));
		return row;
	}

	private static Button CreateActionButton(string text, Action onPressed)
	{
		CardEditorGodotResourceCache.TryLoad(ref _bodyFont, BodyFontPath);

		Button button = new()
		{
			Text = text,
			CustomMinimumSize = new Vector2(0f, 34f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		if (_bodyFont != null)
		{
			button.AddThemeFontOverride("font", _bodyFont);
		}
		button.AddThemeFontSizeOverride("font_size", 17);
		button.AddThemeColorOverride("font_color", StsColors.cream);
		button.Pressed += onPressed;
		return button;
	}

	private static Label CreateLabel(string text, bool header)
	{
		Label label = new()
		{
			Text = text
		};
		if (header)
		{
			if (_headerFont != null)
			{
				label.AddThemeFontOverride("font", _headerFont);
			}
			label.AddThemeFontSizeOverride("font_size", 24);
		}
		else
		{
			if (_bodyFont != null)
			{
				label.AddThemeFontOverride("font", _bodyFont);
			}
			label.AddThemeFontSizeOverride("font_size", 18);
		}
		label.AddThemeColorOverride("font_color", StsColors.cream);
		return label;
	}

	private static void RefreshPanel(PanelContainer panel)
	{
		Label? values = panel.FindChild(ValueLabelName, recursive: true, owned: false) as Label;
		if (values == null)
		{
			return;
		}

		values.Text = "Adjust the bookmark in-game, then send me these values:\n"
			+ CardEditorBaseDeckBookmarkTuning.DescribeMultiline();
	}

	private static void ToggleOpen(NCardLibrary library)
	{
		SetOpen(library, !IsOpen(library));
	}

	public static bool IsOpen(NCardLibrary library)
	{
		return TunerEnabled && library.HasMeta(OpenMetaKey) && library.GetMeta(OpenMetaKey).AsBool();
	}

	private static void SetOpen(NCardLibrary library, bool open)
	{
		if (!TunerEnabled)
		{
			return;
		}

		#pragma warning disable CS0162 // Unreachable code — TunerEnabled is a const toggle
		library.SetMeta(OpenMetaKey, open);
		Sync(library);
		#pragma warning restore CS0162
	}

}
