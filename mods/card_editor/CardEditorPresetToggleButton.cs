using System;
using System.Globalization;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorPresetButtonTuning
{
	public static float ButtonOffsetX = 20f;
	public static float ButtonOffsetY = 20f;
	public static float ButtonWidth = 246f;
	public static float ButtonHeight = 80f;
	public static int ButtonFontSize = 14;
	public static float TextOffsetX = -1f;
	public static float TextOffsetY = 22f;

	public static float TunerOffsetX = 28f;
	public static float TunerOffsetY = 150f;

	private const float DefaultButtonOffsetX = 20f;
	private const float DefaultButtonOffsetY = 20f;
	private const float DefaultButtonWidth = 246f;
	private const float DefaultButtonHeight = 80f;
	private const int DefaultButtonFontSize = 14;
	private const float DefaultTextOffsetX = -1f;
	private const float DefaultTextOffsetY = 22f;

	public static Vector2 GetBaseDeckButtonPosition()
	{
		Vector2 resetPosition = CardEditorBaseDeckActionBarTuning.GetResetButtonPosition();
		return new Vector2(
			resetPosition.X + 280f,
			resetPosition.Y);
	}

	public static void Reset()
	{
		ButtonOffsetX = DefaultButtonOffsetX;
		ButtonOffsetY = DefaultButtonOffsetY;
		ButtonWidth = DefaultButtonWidth;
		ButtonHeight = DefaultButtonHeight;
		ButtonFontSize = DefaultButtonFontSize;
		TextOffsetX = DefaultTextOffsetX;
		TextOffsetY = DefaultTextOffsetY;
	}
}

public partial class NCardEditorPresetToggleButton : Control
{
	private static readonly string _buttonTexturePath = "res://images/packed/common_ui/settings_tab_selected.png";
	private static readonly string _buttonOutlineTexturePath = "res://images/packed/common_ui/settings_tab_stroke.png";
	private static readonly string _fontPath = "res://themes/kreon_bold_glyph_space_two.tres";
	private static readonly string _labelThemePath = "res://themes/settings_screen_tab.tres";
	private static readonly string _outlineMaterialPath = "res://themes/canvas_item_material_additive_shared.tres";
	private static readonly string _shaderPath = "res://shaders/hsv.gdshader";

	private static Texture2D? _buttonTexture;
	private static Texture2D? _buttonOutlineTexture;
	private static Font? _labelFont;
	private static Theme? _labelTheme;
	private static Material? _outlineMaterial;
	private static Shader? _hsvShader;

	private NCardLibrary _library = null!;
	private CardEditorBaseDeckActionButton _button = null!;

	public static NCardEditorPresetToggleButton Create(NCardLibrary library)
	{
		NCardEditorPresetToggleButton button = new NCardEditorPresetToggleButton();
		button.Initialize(library);
		button.BuildUi();
		return button;
	}

	private void Initialize(NCardLibrary library)
	{
		_library = library;
		Name = "CardEditorPresetToggleButton";
		ZIndex = 61;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	private void BuildUi()
	{
		CardEditorGodotResourceCache.Load(ref _buttonTexture, _buttonTexturePath);
		CardEditorGodotResourceCache.Load(ref _buttonOutlineTexture, _buttonOutlineTexturePath);
		CardEditorGodotResourceCache.Load(ref _labelFont, _fontPath);
		CardEditorGodotResourceCache.Load(ref _labelTheme, _labelThemePath);
		CardEditorGodotResourceCache.Load(ref _outlineMaterial, _outlineMaterialPath);
		CardEditorGodotResourceCache.Load(ref _hsvShader, _shaderPath);

		_button = new CardEditorBaseDeckActionButton();
		_button.Initialize(CardEditorLoc.T("button.presetEditor", "Preset Editor"), Colors.White, _buttonTexture!, _buttonOutlineTexture!, _labelFont, _labelTheme, _outlineMaterial, _hsvShader);
		_button.Triggered += OnTriggered;
		AddChild(_button);
	}

	public void RefreshState(bool shouldShow, bool creatorMode, bool isPanelOpen)
	{
		Visible = shouldShow;
		if (!shouldShow)
		{
			return;
		}

		string label = creatorMode
			? CardEditorLoc.T("button.presetCreator", "Preset Creator")
			: CardEditorLoc.T("button.presetEditor", "Preset Editor");
		_button.SetText(label);
		_button.TooltipText = isPanelOpen
			? CardEditorLoc.T("tooltip.hidePresets", "Hide Presets")
			: CardEditorLoc.T("tooltip.showPresets", "Show Presets");
		_button.SetButtonSize(CardEditorPresetButtonTuning.ButtonWidth, CardEditorPresetButtonTuning.ButtonHeight);
		_button.SetTextSize(CardEditorPresetButtonTuning.ButtonFontSize);
		_button.SetTextOffsets(0f, 0f, CardEditorPresetButtonTuning.TextOffsetX, CardEditorPresetButtonTuning.TextOffsetY);
		_button.SetSelected(isPanelOpen);
		_button.SetEmphasized(false);
		_button.SetButtonEnabled(true);
		Vector2 viewportSize = GetViewportRect().Size;
		float x = Math.Max(0f, viewportSize.X - CardEditorPresetButtonTuning.ButtonOffsetX - CardEditorPresetButtonTuning.ButtonWidth);
		float y = CardEditorPresetButtonTuning.ButtonOffsetY;
		_button.Position = new Vector2(x, y);
		CardEditorMod.VerboseLog($"[CardEditor][PresetButton] StandalonePosition viewport=({viewportSize.X:0},{viewportSize.Y:0}) pos=({x:0},{y:0}) size=({CardEditorPresetButtonTuning.ButtonWidth:0},{CardEditorPresetButtonTuning.ButtonHeight:0})");
	}

	private void OnTriggered()
	{
		CardEditorPresetPanelHooks.ToggleOpen(_library);
	}
}

internal static class CardEditorPresetButtonTunerHooks
{
	private const bool TunerEnabled = false;
	private const string ToggleName = "CardEditorPresetButtonTunerToggle";
	private const string PanelName = "CardEditorPresetButtonTunerPanel";
	private const string OpenMetaKey = "card_editor_preset_button_tuner_open";

	public static void Sync(NCardLibrary library, bool shouldShow)
	{
		if (!TunerEnabled || library == null)
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
			Text = CardEditorLoc.T("tuner.presetButton.title", "Preset Button Tuner"),
			FocusMode = Control.FocusModeEnum.None,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		button.AnchorLeft = 0f;
		button.AnchorTop = 0f;
		button.AnchorRight = 0f;
		button.AnchorBottom = 0f;
		button.OffsetLeft = CardEditorPresetButtonTuning.TunerOffsetX;
		button.OffsetTop = CardEditorPresetButtonTuning.TunerOffsetY - 46f;
		button.OffsetRight = CardEditorPresetButtonTuning.TunerOffsetX + 220f;
		button.OffsetBottom = CardEditorPresetButtonTuning.TunerOffsetY - 8f;
		button.ZIndex = 65;
		button.Pressed += () =>
		{
			bool open = !IsOpen(library);
			library.SetMeta(OpenMetaKey, open);
			Sync(library, shouldShow: true);
		};
		return button;
	}

	private static PanelContainer CreatePanel(NCardLibrary library)
	{
		PanelContainer panel = new PanelContainer
		{
			Name = PanelName,
			ZIndex = 65,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		panel.AnchorLeft = 0f;
		panel.AnchorTop = 0f;
		panel.AnchorRight = 0f;
		panel.AnchorBottom = 0f;

		StyleBoxFlat style = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.07f, 0.06f, 0.94f),
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

		Label title = new Label
		{
			Text = CardEditorLoc.T("tuner.presetButton.title", "Preset Button Tuner"),
			Name = "Title"
		};
		title.AddThemeFontSizeOverride("font_size", 24);
		title.AddThemeColorOverride("font_color", StsColors.cream);
		title.AddThemeColorOverride("font_outline_color", StsColors.transparentBlack);
		title.AddThemeConstantOverride("outline_size", 10);
		root.AddChild(title);

		Label readout = new Label
		{
			Name = "Readout",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		readout.AddThemeFontSizeOverride("font_size", 18);
		readout.AddThemeColorOverride("font_color", StsColors.cream);
		root.AddChild(readout);

		root.AddChild(CreateAdjustRow("X",
			() => { CardEditorPresetButtonTuning.ButtonOffsetX -= 10f; RefreshLibrary(library); },
			() => { CardEditorPresetButtonTuning.ButtonOffsetX -= 1f; RefreshLibrary(library); },
			() => { CardEditorPresetButtonTuning.ButtonOffsetX += 1f; RefreshLibrary(library); },
			() => { CardEditorPresetButtonTuning.ButtonOffsetX += 10f; RefreshLibrary(library); }));
		root.AddChild(CreateAdjustRow("Y",
			() => { CardEditorPresetButtonTuning.ButtonOffsetY -= 10f; RefreshLibrary(library); },
			() => { CardEditorPresetButtonTuning.ButtonOffsetY -= 1f; RefreshLibrary(library); },
			() => { CardEditorPresetButtonTuning.ButtonOffsetY += 1f; RefreshLibrary(library); },
			() => { CardEditorPresetButtonTuning.ButtonOffsetY += 10f; RefreshLibrary(library); }));
		root.AddChild(CreateAdjustRow("W",
			() => { CardEditorPresetButtonTuning.ButtonWidth = Mathf.Max(140f, CardEditorPresetButtonTuning.ButtonWidth - 10f); RefreshLibrary(library); },
			() => { CardEditorPresetButtonTuning.ButtonWidth = Mathf.Max(140f, CardEditorPresetButtonTuning.ButtonWidth - 1f); RefreshLibrary(library); },
			() => { CardEditorPresetButtonTuning.ButtonWidth += 1f; RefreshLibrary(library); },
			() => { CardEditorPresetButtonTuning.ButtonWidth += 10f; RefreshLibrary(library); }));
		root.AddChild(CreateAdjustRow("H",
			() => { CardEditorPresetButtonTuning.ButtonHeight = Mathf.Max(44f, CardEditorPresetButtonTuning.ButtonHeight - 10f); RefreshLibrary(library); },
			() => { CardEditorPresetButtonTuning.ButtonHeight = Mathf.Max(44f, CardEditorPresetButtonTuning.ButtonHeight - 1f); RefreshLibrary(library); },
			() => { CardEditorPresetButtonTuning.ButtonHeight += 1f; RefreshLibrary(library); },
			() => { CardEditorPresetButtonTuning.ButtonHeight += 10f; RefreshLibrary(library); }));
		root.AddChild(CreateAdjustRow("TX",
			() => { CardEditorPresetButtonTuning.TextOffsetX -= 10f; RefreshLibrary(library); },
			() => { CardEditorPresetButtonTuning.TextOffsetX -= 1f; RefreshLibrary(library); },
			() => { CardEditorPresetButtonTuning.TextOffsetX += 1f; RefreshLibrary(library); },
			() => { CardEditorPresetButtonTuning.TextOffsetX += 10f; RefreshLibrary(library); }));
		root.AddChild(CreateAdjustRow("TY",
			() => { CardEditorPresetButtonTuning.TextOffsetY -= 10f; RefreshLibrary(library); },
			() => { CardEditorPresetButtonTuning.TextOffsetY -= 1f; RefreshLibrary(library); },
			() => { CardEditorPresetButtonTuning.TextOffsetY += 1f; RefreshLibrary(library); },
			() => { CardEditorPresetButtonTuning.TextOffsetY += 10f; RefreshLibrary(library); }));
		root.AddChild(CreateAdjustRow("FS",
			() => { CardEditorPresetButtonTuning.ButtonFontSize = Math.Max(12, CardEditorPresetButtonTuning.ButtonFontSize - 2); RefreshLibrary(library); },
			() => { CardEditorPresetButtonTuning.ButtonFontSize = Math.Max(12, CardEditorPresetButtonTuning.ButtonFontSize - 1); RefreshLibrary(library); },
			() => { CardEditorPresetButtonTuning.ButtonFontSize += 1; RefreshLibrary(library); },
			() => { CardEditorPresetButtonTuning.ButtonFontSize += 2; RefreshLibrary(library); }));

		HBoxContainer actions = new HBoxContainer();
		actions.AddThemeConstantOverride("separation", 8);
		root.AddChild(actions);

		Button reset = new Button { Text = CardEditorLoc.T("button.reset", "Reset"), FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		reset.Pressed += () =>
		{
			CardEditorPresetButtonTuning.Reset();
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

	private static HBoxContainer CreateAdjustRow(string axis, Action onBigMinus, Action onSmallMinus, Action onSmallPlus, Action onBigPlus)
	{
		HBoxContainer row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);

		Label label = new Label
		{
			Text = axis,
			CustomMinimumSize = new Vector2(40f, 32f),
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
		panel.OffsetLeft = CardEditorPresetButtonTuning.TunerOffsetX;
		panel.OffsetTop = CardEditorPresetButtonTuning.TunerOffsetY;
		panel.OffsetRight = CardEditorPresetButtonTuning.TunerOffsetX + 360f;
		panel.OffsetBottom = CardEditorPresetButtonTuning.TunerOffsetY + 370f;

		Label? readout = panel.GetNodeOrNull<Label>("MarginContainer/Root/Readout");
		if (readout != null)
		{
			readout.Text = string.Format(
				CultureInfo.InvariantCulture,
				"X={0}  Y={1}\nW={2}  H={3}\nFS={4}\nTX={5}  TY={6}",
				Mathf.RoundToInt(CardEditorPresetButtonTuning.ButtonOffsetX),
				Mathf.RoundToInt(CardEditorPresetButtonTuning.ButtonOffsetY),
				Mathf.RoundToInt(CardEditorPresetButtonTuning.ButtonWidth),
				Mathf.RoundToInt(CardEditorPresetButtonTuning.ButtonHeight),
				CardEditorPresetButtonTuning.ButtonFontSize,
				Mathf.RoundToInt(CardEditorPresetButtonTuning.TextOffsetX),
				Mathf.RoundToInt(CardEditorPresetButtonTuning.TextOffsetY));
		}
	}

	private static bool IsOpen(NCardLibrary library)
	{
		return library.HasMeta(OpenMetaKey) && library.GetMeta(OpenMetaKey).AsBool();
	}

	private static void RefreshLibrary(NCardLibrary library)
	{
		CardEditorPresetPanelHooks.Sync(library);
	}
}
