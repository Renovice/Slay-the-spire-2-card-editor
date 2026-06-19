using System;
using System.Globalization;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace SlayTheSpire2Mod.CardEditor;

public partial class NCardEditorBaseDeckPanel : Control
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
	private static readonly Color AddButtonTint = new(0.68f, 0.90f, 0.54f, 1f);
	private static readonly Color DeleteButtonTint = new(0.94f, 0.53f, 0.47f, 1f);
	private static readonly Color CardListButtonTint = Colors.White;
	private static readonly Color ResetButtonTint = new(0.98f, 0.84f, 0.50f, 1f);
	private static readonly Color PresetButtonTint = Colors.White;

	private NCardLibrary _library = null!;
	private CardEditorBaseDeckActionButton _addButton = null!;
	private CardEditorBaseDeckActionButton _deleteButton = null!;
	private CardEditorBaseDeckActionButton _cardListButton = null!;
	private CardEditorBaseDeckActionButton _resetButton = null!;
	private CardEditorBaseDeckActionButton _presetButton = null!;

	public static NCardEditorBaseDeckPanel Create(NCardLibrary library)
	{
		NCardEditorBaseDeckPanel panel = new NCardEditorBaseDeckPanel();
		panel.Initialize(library);
		panel.BuildUi();
		panel.RefreshState();
		return panel;
	}

	private void Initialize(NCardLibrary library)
	{
		_library = library;
		Name = "CardEditorBaseDeckPanel";
		ZIndex = 60;
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

		_addButton = CreateActionButton(CardEditorLoc.T("button.add", "Add"), AddButtonTint, OnAddPressed);
		AddChild(_addButton);

		_deleteButton = CreateActionButton(CardEditorLoc.T("button.delete", "Delete"), DeleteButtonTint, OnDeletePressed);
		AddChild(_deleteButton);

		_cardListButton = CreateActionButton(CardEditorLoc.T("button.cardList", "Card List"), CardListButtonTint, OnCardListPressed);
		AddChild(_cardListButton);

		_resetButton = CreateActionButton(CardEditorLoc.T("button.reset", "Reset"), ResetButtonTint, OnResetPressed);
		AddChild(_resetButton);

		_presetButton = CreateActionButton(CardEditorLoc.T("button.presetEditor", "Preset Editor"), PresetButtonTint, OnPresetPressed);
		AddChild(_presetButton);
	}

	public void RefreshState()
	{
		bool baseDeckMode = CardEditorUiState.IsBaseDeckActive || CardEditorUiState.IsBaseDeckAddActive;
		bool presetOnlyMode = CardEditorUiState.IsEditorActive || CardEditorUiState.IsCreatorActive;
		bool modalOpen = CardEditorBaseDeckPanelHooks.IsBlockingModalOpen();
		if (modalOpen)
		{
			CardEditorBaseDeckPanelHooks.SyncDeferred(_library);
		}
		Visible = (baseDeckMode || presetOnlyMode) && !modalOpen;
		if (!Visible)
		{
			CardEditorBaseDeckActionBarTunerHooks.Sync(this);
			return;
		}

		_addButton.Position = CardEditorBaseDeckActionBarTuning.GetAddButtonPosition();
		_deleteButton.Position = CardEditorBaseDeckActionBarTuning.GetDeleteButtonPosition();
		_cardListButton.Position = CardEditorBaseDeckActionBarTuning.GetCardListButtonPosition();
		_resetButton.Position = CardEditorBaseDeckActionBarTuning.GetResetButtonPosition();
		_presetButton.Position = CardEditorPresetButtonTuning.GetBaseDeckButtonPosition();
		_addButton.SetButtonSize(CardEditorBaseDeckActionBarTuning.ButtonWidth, CardEditorBaseDeckActionBarTuning.ButtonHeight);
		_deleteButton.SetButtonSize(CardEditorBaseDeckActionBarTuning.ButtonWidth, CardEditorBaseDeckActionBarTuning.ButtonHeight);
		_cardListButton.SetButtonSize(CardEditorBaseDeckActionBarTuning.ButtonWidth, CardEditorBaseDeckActionBarTuning.ButtonHeight);
		_resetButton.SetButtonSize(CardEditorBaseDeckActionBarTuning.ButtonWidth, CardEditorBaseDeckActionBarTuning.ButtonHeight);
		_presetButton.SetButtonSize(CardEditorBaseDeckActionBarTuning.ButtonWidth, CardEditorBaseDeckActionBarTuning.ButtonHeight);
		_addButton.SetTextSize(CardEditorBaseDeckActionBarTuning.ButtonFontSize);
		_deleteButton.SetTextSize(CardEditorBaseDeckActionBarTuning.ButtonFontSize);
		_cardListButton.SetTextSize(CardEditorBaseDeckActionBarTuning.ButtonFontSize);
		_resetButton.SetTextSize(CardEditorBaseDeckActionBarTuning.ButtonFontSize);
		_presetButton.SetTextSize(CardEditorBaseDeckActionBarTuning.ButtonFontSize);
		_addButton.SetTextOffsets(
			CardEditorBaseDeckActionBarTuning.BarTextOffsetX,
			CardEditorBaseDeckActionBarTuning.BarTextOffsetY,
			CardEditorBaseDeckActionBarTuning.AddTextOffsetX,
			CardEditorBaseDeckActionBarTuning.AddTextOffsetY);
		_deleteButton.SetTextOffsets(
			CardEditorBaseDeckActionBarTuning.BarTextOffsetX,
			CardEditorBaseDeckActionBarTuning.BarTextOffsetY,
			CardEditorBaseDeckActionBarTuning.DeleteTextOffsetX,
			CardEditorBaseDeckActionBarTuning.DeleteTextOffsetY);
		_cardListButton.SetTextOffsets(
			CardEditorBaseDeckActionBarTuning.BarTextOffsetX,
			CardEditorBaseDeckActionBarTuning.BarTextOffsetY,
			CardEditorBaseDeckActionBarTuning.CardListTextOffsetX,
			CardEditorBaseDeckActionBarTuning.CardListTextOffsetY);
		_resetButton.SetTextOffsets(
			CardEditorBaseDeckActionBarTuning.BarTextOffsetX,
			CardEditorBaseDeckActionBarTuning.BarTextOffsetY,
			CardEditorBaseDeckActionBarTuning.ResetTextOffsetX,
			CardEditorBaseDeckActionBarTuning.ResetTextOffsetY);
		_presetButton.SetTextOffsets(
			CardEditorBaseDeckActionBarTuning.BarTextOffsetX,
			CardEditorBaseDeckActionBarTuning.BarTextOffsetY,
			CardEditorBaseDeckActionBarTuning.ResetTextOffsetX,
			CardEditorBaseDeckActionBarTuning.ResetTextOffsetY);
		_presetButton.SetText(CardEditorLoc.T("button.presetEditor", "Preset Editor"));

		bool hasSelection = CardEditorBaseDeckUiState.HasSelection;
		_addButton.SetButtonEnabled(hasSelection);
		_deleteButton.SetButtonEnabled(CardEditorUiState.IsBaseDeckActive && hasSelection);
		_cardListButton.SetButtonEnabled(true);
		_resetButton.SetButtonEnabled(true);
		_presetButton.SetButtonEnabled(true);
		_addButton.SetEmphasized(hasSelection);
		_deleteButton.SetEmphasized(CardEditorUiState.IsBaseDeckActive && hasSelection);
		_cardListButton.SetEmphasized(true);
		_resetButton.SetEmphasized(false);
		_presetButton.SetEmphasized(false);
		_cardListButton.SetSelected(CardEditorUiState.IsBaseDeckAddActive);
		_resetButton.SetSelected(false);
		_presetButton.SetSelected(CardEditorPresetPanelHooks.IsOpen(_library));
		_addButton.Visible = baseDeckMode;
		_deleteButton.Visible = baseDeckMode;
		_cardListButton.Visible = baseDeckMode;
		_resetButton.Visible = baseDeckMode;
		_presetButton.Visible = true;

		CardEditorBaseDeckActionBarTunerHooks.Sync(this);
	}

	private static CardEditorBaseDeckActionButton CreateActionButton(string text, Color tint, Action onTriggered)
	{
		CardEditorBaseDeckActionButton button = new CardEditorBaseDeckActionButton();
		button.Initialize(text, tint, _buttonTexture!, _buttonOutlineTexture!, _labelFont, _labelTheme, _outlineMaterial, _hsvShader);
		button.Triggered += onTriggered;
		return button;
	}

	private void OnAddPressed()
	{
		if (!CardEditorMultiplayerSync.CanEditSharedState())
		{
			Log.Info($"[CardEditor][MultiplayerSync] Base-deck add blocked: {CardEditorMultiplayerSync.GetSharedStateLockReason()}");
			return;
		}

		CardEditorBaseDeckUiState.AddSelectedToDeck();
		CardEditorMultiplayerSync.NotifySharedStateMutatedLocally();
	}

	private void OnDeletePressed()
	{
		if (!CardEditorMultiplayerSync.CanEditSharedState())
		{
			Log.Info($"[CardEditor][MultiplayerSync] Base-deck delete blocked: {CardEditorMultiplayerSync.GetSharedStateLockReason()}");
			return;
		}

		CardEditorBaseDeckUiState.DeleteSelectedFromDeck();
		CardEditorMultiplayerSync.NotifySharedStateMutatedLocally();
	}

	private void OnCardListPressed()
	{
		if (CardEditorUiState.IsBaseDeckAddActive)
		{
			CardEditorBaseDeckUiState.ExitAddModeToDeck();
			return;
		}

		CardEditorBaseDeckUiState.EnterAddMode();
	}

	private async void OnResetPressed()
	{
		if (!CardEditorMultiplayerSync.CanEditSharedState())
		{
			Log.Info($"[CardEditor][MultiplayerSync] Base-deck reset blocked: {CardEditorMultiplayerSync.GetSharedStateLockReason()}");
			return;
		}

		CardEditorMod.VerboseLog($"[CardEditor][ConfirmPopup] Reset button pressed mode={CardEditorUiState.Mode} panelVisible={Visible} resetButtonVisible={_resetButton?.Visible}");
		bool confirmed = await CardEditorConfirmPopup.ShowConfirmation(
			CardEditorLoc.T("confirm.resetBaseDeck.title", "Reset Base Deck?"),
			CardEditorLoc.T("confirm.resetBaseDeck.body", "Revert this character's base deck to the vanilla starter deck?"));
		CardEditorMod.VerboseLog($"[CardEditor][ConfirmPopup] Reset button confirmation result={confirmed}");
		if (!confirmed)
		{
			return;
		}

		CardEditorMod.VerboseLog("[CardEditor][ConfirmPopup] Reset confirmed; invoking ResetEditedDeck().");
		CardEditorBaseDeckUiState.ResetEditedDeck();
		CardEditorMultiplayerSync.NotifySharedStateMutatedLocally();
	}

	private void OnPresetPressed()
	{
		CardEditorPresetPanelHooks.ToggleOpen(_library);
		RefreshState();
	}
}

internal static class CardEditorBaseDeckPanelHooks
{
	private const string DeferredSyncMetaKey = "card_editor_base_deck_panel_deferred_sync";

	public static bool IsBlockingModalOpen()
	{
		object? openModal = NModalContainer.Instance?.OpenModal;
		if (openModal == null)
		{
			return false;
		}

		if (openModal is CanvasItem modalCanvas)
		{
			return GodotObject.IsInstanceValid(modalCanvas) && modalCanvas.IsInsideTree() && modalCanvas.Visible;
		}

		if (openModal is Node modalNode)
		{
			return GodotObject.IsInstanceValid(modalNode) && modalNode.IsInsideTree();
		}

		Control? focusedControl = NModalContainer.Instance?.OpenModal?.DefaultFocusedControl;
		return focusedControl != null
			&& GodotObject.IsInstanceValid(focusedControl)
			&& focusedControl.IsInsideTree()
			&& focusedControl.Visible;
	}

	public static void SyncDeferred(NCardLibrary library)
	{
		if (library == null || !GodotObject.IsInstanceValid(library))
		{
			return;
		}
		if (library.HasMeta(DeferredSyncMetaKey) && library.GetMeta(DeferredSyncMetaKey).AsBool())
		{
			return;
		}

		library.SetMeta(DeferredSyncMetaKey, true);
		Callable.From(() =>
		{
			if (!GodotObject.IsInstanceValid(library))
			{
				return;
			}

			library.SetMeta(DeferredSyncMetaKey, false);
			Sync(library);
		}).CallDeferred();
	}

	public static void Sync(NCardLibrary library)
	{
		if (library == null)
		{
			return;
		}

		NCardEditorBaseDeckPanel? panel = library.GetNodeOrNull<NCardEditorBaseDeckPanel>("CardEditorBaseDeckPanel");
		bool shouldShow =
			CardEditorUiState.IsBaseDeckActive ||
			CardEditorUiState.IsBaseDeckAddActive ||
			CardEditorUiState.IsEditorActive ||
			CardEditorUiState.IsCreatorActive;
		if (IsBlockingModalOpen())
		{
			SyncDeferred(library);
			shouldShow = false;
		}
		if (shouldShow)
		{
			CardEditorUiState.SetLastLibrary(library);
			if (panel == null)
			{
				panel = NCardEditorBaseDeckPanel.Create(library);
				library.AddChild(panel);
			}

			panel.Visible = true;
			panel.RefreshState();
		}
		else if (panel != null)
		{
			panel.Visible = false;
		}

		SyncPoolVisibility(library, shouldShow && CardEditorUiState.IsBaseDeckActive);
	}

	public static void RefreshLastLibrary()
	{
		if (CardEditorUiState.TryGetLastLibrary(out NCardLibrary? library) && library != null)
		{
			Sync(library);
		}
	}

	private static void SyncPoolVisibility(NCardLibrary library, bool baseDeckCharacterMode)
	{
		string[] optionalPools = { "%ColorlessPool", "%AncientsPool", "%MiscPool" };
		foreach (string path in optionalPools)
		{
			Control? node = library.GetNodeOrNull<Control>(path);
			if (node != null)
			{
				node.Visible = !baseDeckCharacterMode;
			}
		}
	}
}

internal static class CardEditorBaseDeckActionBarTuning
{
	public const bool TunerEnabled = false;

	private const float DefaultAddButtonX = 1015f;
	private const float DefaultAddButtonY = 108f;
	private const float DefaultDeleteButtonX = 1295f;
	private const float DefaultDeleteButtonY = 107f;
	private const float DefaultCardListButtonX = 1575f;
	private const float DefaultCardListButtonY = 107f;
	private const float DefaultResetButtonX = 1855f;
	private const float DefaultResetButtonY = 107f;
	private const float DefaultBarOffsetX = -640f;
	private const float DefaultBarOffsetY = -154f;
	private const float DefaultButtonWidth = 246f;
	private const float DefaultButtonHeight = 80f;
	private const int DefaultButtonFontSize = 19;
	private const float DefaultBarTextOffsetX = -1f;
	private const float DefaultBarTextOffsetY = 22f;
	private const float DefaultAddTextOffsetX = -1f;
	private const float DefaultAddTextOffsetY = 0f;
	private const float DefaultDeleteTextOffsetX = -1f;
	private const float DefaultDeleteTextOffsetY = 0f;
	private const float DefaultCardListTextOffsetX = 0f;
	private const float DefaultCardListTextOffsetY = 0f;
	private const float DefaultResetTextOffsetX = 0f;
	private const float DefaultResetTextOffsetY = 0f;

	public static float AddButtonX { get; private set; } = DefaultAddButtonX;
	public static float AddButtonY { get; private set; } = DefaultAddButtonY;
	public static float DeleteButtonX { get; private set; } = DefaultDeleteButtonX;
	public static float DeleteButtonY { get; private set; } = DefaultDeleteButtonY;
	public static float CardListButtonX { get; private set; } = DefaultCardListButtonX;
	public static float CardListButtonY { get; private set; } = DefaultCardListButtonY;
	public static float ResetButtonX { get; private set; } = DefaultResetButtonX;
	public static float ResetButtonY { get; private set; } = DefaultResetButtonY;
	public static float BarOffsetX { get; private set; } = DefaultBarOffsetX;
	public static float BarOffsetY { get; private set; } = DefaultBarOffsetY;
	public static float ButtonWidth { get; private set; } = DefaultButtonWidth;
	public static float ButtonHeight { get; private set; } = DefaultButtonHeight;
	public static int ButtonFontSize { get; private set; } = DefaultButtonFontSize;
	public static float BarTextOffsetX { get; private set; } = DefaultBarTextOffsetX;
	public static float BarTextOffsetY { get; private set; } = DefaultBarTextOffsetY;
	public static float AddTextOffsetX { get; private set; } = DefaultAddTextOffsetX;
	public static float AddTextOffsetY { get; private set; } = DefaultAddTextOffsetY;
	public static float DeleteTextOffsetX { get; private set; } = DefaultDeleteTextOffsetX;
	public static float DeleteTextOffsetY { get; private set; } = DefaultDeleteTextOffsetY;
	public static float CardListTextOffsetX { get; private set; } = DefaultCardListTextOffsetX;
	public static float CardListTextOffsetY { get; private set; } = DefaultCardListTextOffsetY;
	public static float ResetTextOffsetX { get; private set; } = DefaultResetTextOffsetX;
	public static float ResetTextOffsetY { get; private set; } = DefaultResetTextOffsetY;

	public static void Reset()
	{
		AddButtonX = DefaultAddButtonX;
		AddButtonY = DefaultAddButtonY;
		DeleteButtonX = DefaultDeleteButtonX;
		DeleteButtonY = DefaultDeleteButtonY;
		CardListButtonX = DefaultCardListButtonX;
		CardListButtonY = DefaultCardListButtonY;
		ResetButtonX = DefaultResetButtonX;
		ResetButtonY = DefaultResetButtonY;
		BarOffsetX = DefaultBarOffsetX;
		BarOffsetY = DefaultBarOffsetY;
		ButtonWidth = DefaultButtonWidth;
		ButtonHeight = DefaultButtonHeight;
		ButtonFontSize = DefaultButtonFontSize;
		BarTextOffsetX = DefaultBarTextOffsetX;
		BarTextOffsetY = DefaultBarTextOffsetY;
		AddTextOffsetX = DefaultAddTextOffsetX;
		AddTextOffsetY = DefaultAddTextOffsetY;
		DeleteTextOffsetX = DefaultDeleteTextOffsetX;
		DeleteTextOffsetY = DefaultDeleteTextOffsetY;
		CardListTextOffsetX = DefaultCardListTextOffsetX;
		CardListTextOffsetY = DefaultCardListTextOffsetY;
		ResetTextOffsetX = DefaultResetTextOffsetX;
		ResetTextOffsetY = DefaultResetTextOffsetY;
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
	}

	public static Vector2 GetAddButtonPosition()
	{
		return new Vector2(AddButtonX + BarOffsetX, AddButtonY + BarOffsetY);
	}

	public static Vector2 GetDeleteButtonPosition()
	{
		return new Vector2(DeleteButtonX + BarOffsetX, DeleteButtonY + BarOffsetY);
	}

	public static Vector2 GetCardListButtonPosition()
	{
		return new Vector2(CardListButtonX + BarOffsetX, CardListButtonY + BarOffsetY);
	}

	public static Vector2 GetResetButtonPosition()
	{
		return new Vector2(ResetButtonX + BarOffsetX, ResetButtonY + BarOffsetY);
	}

	public static void AdjustBarOffsetX(float delta)
	{
		BarOffsetX += delta;
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
	}

	public static void AdjustBarOffsetY(float delta)
	{
		BarOffsetY += delta;
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
	}

	public static void AdjustButtonWidth(float delta)
	{
		ButtonWidth = Mathf.Max(120f, ButtonWidth + delta);
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
	}

	public static void AdjustButtonHeight(float delta)
	{
		ButtonHeight = Mathf.Max(40f, ButtonHeight + delta);
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
	}

	public static void AdjustButtonFontSize(int delta)
	{
		ButtonFontSize = Math.Max(12, ButtonFontSize + delta);
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
	}

	public static void AdjustBarTextOffsetX(float delta)
	{
		BarTextOffsetX += delta;
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
	}

	public static void AdjustBarTextOffsetY(float delta)
	{
		BarTextOffsetY += delta;
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
	}

	public static void AdjustAddButtonX(float delta)
	{
		AddButtonX += delta;
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
	}

	public static void AdjustAddButtonY(float delta)
	{
		AddButtonY += delta;
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
	}

	public static void AdjustDeleteButtonX(float delta)
	{
		DeleteButtonX += delta;
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
	}

	public static void AdjustDeleteButtonY(float delta)
	{
		DeleteButtonY += delta;
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
	}

	public static void AdjustCardListButtonX(float delta)
	{
		CardListButtonX += delta;
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
	}

	public static void AdjustCardListButtonY(float delta)
	{
		CardListButtonY += delta;
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
	}

	public static void AdjustResetButtonX(float delta)
	{
		ResetButtonX += delta;
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
	}

	public static void AdjustResetButtonY(float delta)
	{
		ResetButtonY += delta;
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
	}

	public static void AdjustAddTextOffsetX(float delta)
	{
		AddTextOffsetX += delta;
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
	}

	public static void AdjustAddTextOffsetY(float delta)
	{
		AddTextOffsetY += delta;
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
	}

	public static void AdjustDeleteTextOffsetX(float delta)
	{
		DeleteTextOffsetX += delta;
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
	}

	public static void AdjustDeleteTextOffsetY(float delta)
	{
		DeleteTextOffsetY += delta;
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
	}

	public static void AdjustCardListTextOffsetX(float delta)
	{
		CardListTextOffsetX += delta;
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
	}

	public static void AdjustCardListTextOffsetY(float delta)
	{
		CardListTextOffsetY += delta;
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
	}

	public static void AdjustResetTextOffsetX(float delta)
	{
		ResetTextOffsetX += delta;
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
	}

	public static void AdjustResetTextOffsetY(float delta)
	{
		ResetTextOffsetY += delta;
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
	}
}

internal static class CardEditorBaseDeckActionBarTunerHooks
{
	private const string TunerToggleNodeName = "CardEditorBaseDeckActionBarTunerToggle";
	private const string TunerPanelNodeName = "CardEditorBaseDeckActionBarTuner";
	private const string TunerReadoutNodeName = "Readout";
	private const string TunerDragHandleNodeName = "DragHandle";
	private const string TunerOpenMetaKey = "card_editor_action_bar_tuner_open";
	private const string TunerPositionMetaKey = "card_editor_action_bar_tuner_position";
	private const string TunerDragActiveMetaKey = "card_editor_action_bar_tuner_drag_active";
	private const string TunerDragOffsetMetaKey = "card_editor_action_bar_tuner_drag_offset";

	private static readonly Vector2 DefaultPanelPosition = new(1451f, 70f);

	public static void Sync(NCardEditorBaseDeckPanel owner)
	{
		if (owner == null)
		{
			return;
		}

		Button? toggleButton = owner.GetNodeOrNull<Button>(TunerToggleNodeName);
		PanelContainer? panel = owner.GetNodeOrNull<PanelContainer>(TunerPanelNodeName);
		if (!CardEditorBaseDeckActionBarTuning.TunerEnabled || !owner.Visible)
		{
			toggleButton?.QueueFree();
			panel?.QueueFree();
			return;
		}

		toggleButton ??= CreateToggleButton(owner);
		panel ??= CreatePanel(owner);

		Vector2 desiredSize = GetPanelSize(owner);
		panel.CustomMinimumSize = desiredSize;
		panel.Size = desiredSize;
		bool isOpen = IsOpen(owner);
		toggleButton.Text = isOpen ? "Hide Button Tuner" : "Tune Button Bar";
		panel.Visible = isOpen;
		SetPanelPosition(owner, panel, GetPanelPosition(owner));
		if (isOpen)
		{
			UpdateReadout(owner, panel);
		}
	}

	private static Button CreateToggleButton(NCardEditorBaseDeckPanel owner)
	{
		Button button = new Button
		{
			Name = TunerToggleNodeName,
			AnchorLeft = 1f,
			AnchorTop = 0f,
			AnchorRight = 1f,
			AnchorBottom = 0f,
			OffsetLeft = -290f,
			OffsetTop = 18f,
			OffsetRight = -18f,
			OffsetBottom = 58f,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		button.Pressed += () => SetOpen(owner, !IsOpen(owner));
		owner.AddChild(button);
		return button;
	}

	private static PanelContainer CreatePanel(NCardEditorBaseDeckPanel owner)
	{
		Vector2 panelSize = GetPanelSize(owner);
		PanelContainer panel = new PanelContainer
		{
			Name = TunerPanelNodeName,
			Position = GetPanelPosition(owner),
			CustomMinimumSize = panelSize,
			Size = panelSize,
			MouseFilter = Control.MouseFilterEnum.Stop
		};

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
		panel.AddThemeStyleboxOverride("panel", panelStyle);

		MarginContainer margin = new MarginContainer();
		margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 14);
		margin.AddThemeConstantOverride("margin_top", 14);
		margin.AddThemeConstantOverride("margin_right", 14);
		margin.AddThemeConstantOverride("margin_bottom", 14);
		panel.AddChild(margin);

		VBoxContainer root = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		root.AddThemeConstantOverride("separation", 10);
		margin.AddChild(root);

		Control dragHandle = new Control
		{
			Name = TunerDragHandleNodeName,
			CustomMinimumSize = new Vector2(0f, 40f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		dragHandle.GuiInput += @event => HandlePanelDragInput(owner, panel, @event);
		root.AddChild(dragHandle);

		Label title = new Label
		{
			Text = CardEditorLoc.T("tuner.buttonBar.title", "Button Bar Tuner"),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		title.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		title.AddThemeFontSizeOverride("font_size", 24);
		title.AddThemeColorOverride("font_color", StsColors.cream);
		dragHandle.AddChild(title);

		Label help = new Label
		{
			Text = CardEditorLoc.T("tuner.buttonBar.help", "Move and resize the buttons and their label text in-game."),
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		help.AddThemeFontSizeOverride("font_size", 18);
		help.AddThemeColorOverride("font_color", StsColors.cream);
		root.AddChild(help);

		Label readout = new Label
		{
			Name = TunerReadoutNodeName,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		readout.AddThemeFontSizeOverride("font_size", 18);
		readout.AddThemeColorOverride("font_color", StsColors.cream);
		root.AddChild(readout);

		ScrollContainer scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0f, 320f)
		};
		root.AddChild(scroll);

		VBoxContainer controls = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		controls.AddThemeConstantOverride("separation", 10);
		scroll.AddChild(controls);

		controls.AddChild(CreateAdjustRow("BX", () => CardEditorBaseDeckActionBarTuning.AdjustBarOffsetX(-10f), () => CardEditorBaseDeckActionBarTuning.AdjustBarOffsetX(-1f), () => CardEditorBaseDeckActionBarTuning.AdjustBarOffsetX(1f), () => CardEditorBaseDeckActionBarTuning.AdjustBarOffsetX(10f)));
		controls.AddChild(CreateAdjustRow("BY", () => CardEditorBaseDeckActionBarTuning.AdjustBarOffsetY(-10f), () => CardEditorBaseDeckActionBarTuning.AdjustBarOffsetY(-1f), () => CardEditorBaseDeckActionBarTuning.AdjustBarOffsetY(1f), () => CardEditorBaseDeckActionBarTuning.AdjustBarOffsetY(10f)));
		controls.AddChild(CreateAdjustRow("BW", () => CardEditorBaseDeckActionBarTuning.AdjustButtonWidth(-10f), () => CardEditorBaseDeckActionBarTuning.AdjustButtonWidth(-1f), () => CardEditorBaseDeckActionBarTuning.AdjustButtonWidth(1f), () => CardEditorBaseDeckActionBarTuning.AdjustButtonWidth(10f)));
		controls.AddChild(CreateAdjustRow("BH", () => CardEditorBaseDeckActionBarTuning.AdjustButtonHeight(-10f), () => CardEditorBaseDeckActionBarTuning.AdjustButtonHeight(-1f), () => CardEditorBaseDeckActionBarTuning.AdjustButtonHeight(1f), () => CardEditorBaseDeckActionBarTuning.AdjustButtonHeight(10f)));
		controls.AddChild(CreateAdjustRow("FS", () => CardEditorBaseDeckActionBarTuning.AdjustButtonFontSize(-10), () => CardEditorBaseDeckActionBarTuning.AdjustButtonFontSize(-1), () => CardEditorBaseDeckActionBarTuning.AdjustButtonFontSize(1), () => CardEditorBaseDeckActionBarTuning.AdjustButtonFontSize(10)));
		controls.AddChild(CreateAdjustRow("BTX", () => CardEditorBaseDeckActionBarTuning.AdjustBarTextOffsetX(-10f), () => CardEditorBaseDeckActionBarTuning.AdjustBarTextOffsetX(-1f), () => CardEditorBaseDeckActionBarTuning.AdjustBarTextOffsetX(1f), () => CardEditorBaseDeckActionBarTuning.AdjustBarTextOffsetX(10f)));
		controls.AddChild(CreateAdjustRow("BTY", () => CardEditorBaseDeckActionBarTuning.AdjustBarTextOffsetY(-10f), () => CardEditorBaseDeckActionBarTuning.AdjustBarTextOffsetY(-1f), () => CardEditorBaseDeckActionBarTuning.AdjustBarTextOffsetY(1f), () => CardEditorBaseDeckActionBarTuning.AdjustBarTextOffsetY(10f)));
		controls.AddChild(CreateAdjustRow("AX", () => CardEditorBaseDeckActionBarTuning.AdjustAddButtonX(-10f), () => CardEditorBaseDeckActionBarTuning.AdjustAddButtonX(-1f), () => CardEditorBaseDeckActionBarTuning.AdjustAddButtonX(1f), () => CardEditorBaseDeckActionBarTuning.AdjustAddButtonX(10f)));
		controls.AddChild(CreateAdjustRow("AY", () => CardEditorBaseDeckActionBarTuning.AdjustAddButtonY(-10f), () => CardEditorBaseDeckActionBarTuning.AdjustAddButtonY(-1f), () => CardEditorBaseDeckActionBarTuning.AdjustAddButtonY(1f), () => CardEditorBaseDeckActionBarTuning.AdjustAddButtonY(10f)));
		controls.AddChild(CreateAdjustRow("ATX", () => CardEditorBaseDeckActionBarTuning.AdjustAddTextOffsetX(-10f), () => CardEditorBaseDeckActionBarTuning.AdjustAddTextOffsetX(-1f), () => CardEditorBaseDeckActionBarTuning.AdjustAddTextOffsetX(1f), () => CardEditorBaseDeckActionBarTuning.AdjustAddTextOffsetX(10f)));
		controls.AddChild(CreateAdjustRow("ATY", () => CardEditorBaseDeckActionBarTuning.AdjustAddTextOffsetY(-10f), () => CardEditorBaseDeckActionBarTuning.AdjustAddTextOffsetY(-1f), () => CardEditorBaseDeckActionBarTuning.AdjustAddTextOffsetY(1f), () => CardEditorBaseDeckActionBarTuning.AdjustAddTextOffsetY(10f)));
		controls.AddChild(CreateAdjustRow("DX", () => CardEditorBaseDeckActionBarTuning.AdjustDeleteButtonX(-10f), () => CardEditorBaseDeckActionBarTuning.AdjustDeleteButtonX(-1f), () => CardEditorBaseDeckActionBarTuning.AdjustDeleteButtonX(1f), () => CardEditorBaseDeckActionBarTuning.AdjustDeleteButtonX(10f)));
		controls.AddChild(CreateAdjustRow("DY", () => CardEditorBaseDeckActionBarTuning.AdjustDeleteButtonY(-10f), () => CardEditorBaseDeckActionBarTuning.AdjustDeleteButtonY(-1f), () => CardEditorBaseDeckActionBarTuning.AdjustDeleteButtonY(1f), () => CardEditorBaseDeckActionBarTuning.AdjustDeleteButtonY(10f)));
		controls.AddChild(CreateAdjustRow("DTX", () => CardEditorBaseDeckActionBarTuning.AdjustDeleteTextOffsetX(-10f), () => CardEditorBaseDeckActionBarTuning.AdjustDeleteTextOffsetX(-1f), () => CardEditorBaseDeckActionBarTuning.AdjustDeleteTextOffsetX(1f), () => CardEditorBaseDeckActionBarTuning.AdjustDeleteTextOffsetX(10f)));
		controls.AddChild(CreateAdjustRow("DTY", () => CardEditorBaseDeckActionBarTuning.AdjustDeleteTextOffsetY(-10f), () => CardEditorBaseDeckActionBarTuning.AdjustDeleteTextOffsetY(-1f), () => CardEditorBaseDeckActionBarTuning.AdjustDeleteTextOffsetY(1f), () => CardEditorBaseDeckActionBarTuning.AdjustDeleteTextOffsetY(10f)));
		controls.AddChild(CreateAdjustRow("LX", () => CardEditorBaseDeckActionBarTuning.AdjustCardListButtonX(-10f), () => CardEditorBaseDeckActionBarTuning.AdjustCardListButtonX(-1f), () => CardEditorBaseDeckActionBarTuning.AdjustCardListButtonX(1f), () => CardEditorBaseDeckActionBarTuning.AdjustCardListButtonX(10f)));
		controls.AddChild(CreateAdjustRow("LY", () => CardEditorBaseDeckActionBarTuning.AdjustCardListButtonY(-10f), () => CardEditorBaseDeckActionBarTuning.AdjustCardListButtonY(-1f), () => CardEditorBaseDeckActionBarTuning.AdjustCardListButtonY(1f), () => CardEditorBaseDeckActionBarTuning.AdjustCardListButtonY(10f)));
		controls.AddChild(CreateAdjustRow("LTX", () => CardEditorBaseDeckActionBarTuning.AdjustCardListTextOffsetX(-10f), () => CardEditorBaseDeckActionBarTuning.AdjustCardListTextOffsetX(-1f), () => CardEditorBaseDeckActionBarTuning.AdjustCardListTextOffsetX(1f), () => CardEditorBaseDeckActionBarTuning.AdjustCardListTextOffsetX(10f)));
		controls.AddChild(CreateAdjustRow("LTY", () => CardEditorBaseDeckActionBarTuning.AdjustCardListTextOffsetY(-10f), () => CardEditorBaseDeckActionBarTuning.AdjustCardListTextOffsetY(-1f), () => CardEditorBaseDeckActionBarTuning.AdjustCardListTextOffsetY(1f), () => CardEditorBaseDeckActionBarTuning.AdjustCardListTextOffsetY(10f)));
		controls.AddChild(CreateAdjustRow("RX", () => CardEditorBaseDeckActionBarTuning.AdjustResetButtonX(-10f), () => CardEditorBaseDeckActionBarTuning.AdjustResetButtonX(-1f), () => CardEditorBaseDeckActionBarTuning.AdjustResetButtonX(1f), () => CardEditorBaseDeckActionBarTuning.AdjustResetButtonX(10f)));
		controls.AddChild(CreateAdjustRow("RY", () => CardEditorBaseDeckActionBarTuning.AdjustResetButtonY(-10f), () => CardEditorBaseDeckActionBarTuning.AdjustResetButtonY(-1f), () => CardEditorBaseDeckActionBarTuning.AdjustResetButtonY(1f), () => CardEditorBaseDeckActionBarTuning.AdjustResetButtonY(10f)));
		controls.AddChild(CreateAdjustRow("RTX", () => CardEditorBaseDeckActionBarTuning.AdjustResetTextOffsetX(-10f), () => CardEditorBaseDeckActionBarTuning.AdjustResetTextOffsetX(-1f), () => CardEditorBaseDeckActionBarTuning.AdjustResetTextOffsetX(1f), () => CardEditorBaseDeckActionBarTuning.AdjustResetTextOffsetX(10f)));
		controls.AddChild(CreateAdjustRow("RTY", () => CardEditorBaseDeckActionBarTuning.AdjustResetTextOffsetY(-10f), () => CardEditorBaseDeckActionBarTuning.AdjustResetTextOffsetY(-1f), () => CardEditorBaseDeckActionBarTuning.AdjustResetTextOffsetY(1f), () => CardEditorBaseDeckActionBarTuning.AdjustResetTextOffsetY(10f)));

		HBoxContainer footer = new HBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		footer.AddThemeConstantOverride("separation", 12);
		controls.AddChild(footer);

		footer.AddChild(CreateFooterButton(CardEditorLoc.T("button.reset", "Reset"), CardEditorBaseDeckActionBarTuning.Reset));
		footer.AddChild(CreateFooterButton(CardEditorLoc.T("button.close", "Close"), () => SetOpen(owner, false)));

		owner.AddChild(panel);
		UpdateReadout(owner, panel);
		return panel;
	}

	private static HBoxContainer CreateAdjustRow(string labelText, Action onBigMinus, Action onSmallMinus, Action onSmallPlus, Action onBigPlus)
	{
		HBoxContainer row = new HBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		row.AddThemeConstantOverride("separation", 8);

		Label label = new Label
		{
			Text = labelText,
			CustomMinimumSize = new Vector2(54f, 0f),
			VerticalAlignment = VerticalAlignment.Center
		};
		label.AddThemeFontSizeOverride("font_size", 18);
		label.AddThemeColorOverride("font_color", StsColors.cream);
		row.AddChild(label);

		row.AddChild(CreateFooterButton("-10", onBigMinus));
		row.AddChild(CreateFooterButton("-1", onSmallMinus));
		row.AddChild(CreateFooterButton("+1", onSmallPlus));
		row.AddChild(CreateFooterButton("+10", onBigPlus));
		return row;
	}

	private static Button CreateFooterButton(string text, Action onPressed)
	{
		Button button = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(80f, 40f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		button.Pressed += onPressed;
		return button;
	}

	private static bool IsOpen(NCardEditorBaseDeckPanel owner)
	{
		return owner.HasMeta(TunerOpenMetaKey) && owner.GetMeta(TunerOpenMetaKey).AsBool();
	}

	private static void SetOpen(NCardEditorBaseDeckPanel owner, bool isOpen)
	{
		owner.SetMeta(TunerOpenMetaKey, isOpen);
		Sync(owner);
	}

	private static void UpdateReadout(NCardEditorBaseDeckPanel owner, PanelContainer panel)
	{
		Label? readout = panel.FindChild(TunerReadoutNodeName, true, false) as Label;
		if (readout == null)
		{
			return;
		}

		Vector2 panelPosition = GetPanelPosition(owner);
		readout.Text =
			$"PX={panelPosition.X.ToString("0", CultureInfo.InvariantCulture)}  " +
			$"PY={panelPosition.Y.ToString("0", CultureInfo.InvariantCulture)}\n" +
			$"BX={CardEditorBaseDeckActionBarTuning.BarOffsetX.ToString("0", CultureInfo.InvariantCulture)}  " +
			$"BY={CardEditorBaseDeckActionBarTuning.BarOffsetY.ToString("0", CultureInfo.InvariantCulture)}\n" +
			$"BW={CardEditorBaseDeckActionBarTuning.ButtonWidth.ToString("0", CultureInfo.InvariantCulture)}  " +
			$"BH={CardEditorBaseDeckActionBarTuning.ButtonHeight.ToString("0", CultureInfo.InvariantCulture)}  " +
			$"FS={CardEditorBaseDeckActionBarTuning.ButtonFontSize.ToString(CultureInfo.InvariantCulture)}\n" +
			$"BTX={CardEditorBaseDeckActionBarTuning.BarTextOffsetX.ToString("0", CultureInfo.InvariantCulture)}  " +
			$"BTY={CardEditorBaseDeckActionBarTuning.BarTextOffsetY.ToString("0", CultureInfo.InvariantCulture)}\n" +
			$"AX={CardEditorBaseDeckActionBarTuning.AddButtonX.ToString("0", CultureInfo.InvariantCulture)}  " +
			$"AY={CardEditorBaseDeckActionBarTuning.AddButtonY.ToString("0", CultureInfo.InvariantCulture)}  " +
			$"ATX={CardEditorBaseDeckActionBarTuning.AddTextOffsetX.ToString("0", CultureInfo.InvariantCulture)}  " +
			$"ATY={CardEditorBaseDeckActionBarTuning.AddTextOffsetY.ToString("0", CultureInfo.InvariantCulture)}\n" +
			$"DX={CardEditorBaseDeckActionBarTuning.DeleteButtonX.ToString("0", CultureInfo.InvariantCulture)}  " +
			$"DY={CardEditorBaseDeckActionBarTuning.DeleteButtonY.ToString("0", CultureInfo.InvariantCulture)}  " +
			$"DTX={CardEditorBaseDeckActionBarTuning.DeleteTextOffsetX.ToString("0", CultureInfo.InvariantCulture)}  " +
			$"DTY={CardEditorBaseDeckActionBarTuning.DeleteTextOffsetY.ToString("0", CultureInfo.InvariantCulture)}\n" +
			$"LX={CardEditorBaseDeckActionBarTuning.CardListButtonX.ToString("0", CultureInfo.InvariantCulture)}  " +
			$"LY={CardEditorBaseDeckActionBarTuning.CardListButtonY.ToString("0", CultureInfo.InvariantCulture)}  " +
			$"LTX={CardEditorBaseDeckActionBarTuning.CardListTextOffsetX.ToString("0", CultureInfo.InvariantCulture)}  " +
			$"LTY={CardEditorBaseDeckActionBarTuning.CardListTextOffsetY.ToString("0", CultureInfo.InvariantCulture)}\n" +
			$"RX={CardEditorBaseDeckActionBarTuning.ResetButtonX.ToString("0", CultureInfo.InvariantCulture)}  " +
			$"RY={CardEditorBaseDeckActionBarTuning.ResetButtonY.ToString("0", CultureInfo.InvariantCulture)}  " +
			$"RTX={CardEditorBaseDeckActionBarTuning.ResetTextOffsetX.ToString("0", CultureInfo.InvariantCulture)}  " +
			$"RTY={CardEditorBaseDeckActionBarTuning.ResetTextOffsetY.ToString("0", CultureInfo.InvariantCulture)}";
	}

	private static Vector2 GetPanelPosition(NCardEditorBaseDeckPanel owner)
	{
		return owner.HasMeta(TunerPositionMetaKey) ? owner.GetMeta(TunerPositionMetaKey).AsVector2() : DefaultPanelPosition;
	}

	private static Vector2 GetPanelSize(NCardEditorBaseDeckPanel owner)
	{
		Vector2 viewportSize = owner.GetViewportRect().Size;
		float height = Mathf.Clamp(viewportSize.Y - 120f, 420f, 760f);
		return new Vector2(432f, height);
	}

	private static void SetPanelPosition(NCardEditorBaseDeckPanel owner, PanelContainer panel, Vector2 position)
	{
		Vector2 viewportSize = owner.GetViewportRect().Size;
		float maxX = Mathf.Max(0f, viewportSize.X - panel.Size.X);
		float maxY = Mathf.Max(0f, viewportSize.Y - panel.Size.Y);
		Vector2 clamped = new Vector2(
			Mathf.Clamp(position.X, 0f, maxX),
			Mathf.Clamp(position.Y, 0f, maxY));
		owner.SetMeta(TunerPositionMetaKey, clamped);
		panel.Position = clamped;
		UpdateReadout(owner, panel);
	}

	private static void HandlePanelDragInput(NCardEditorBaseDeckPanel owner, PanelContainer panel, InputEvent @event)
	{
		switch (@event)
		{
			case InputEventMouseButton button when button.ButtonIndex == MouseButton.Left && button.Pressed:
				owner.SetMeta(TunerDragActiveMetaKey, true);
				owner.SetMeta(TunerDragOffsetMetaKey, button.GlobalPosition - panel.GlobalPosition);
				panel.AcceptEvent();
				break;
			case InputEventMouseButton button when button.ButtonIndex == MouseButton.Left && !button.Pressed:
				owner.SetMeta(TunerDragActiveMetaKey, false);
				panel.AcceptEvent();
				break;
			case InputEventMouseMotion motion when owner.HasMeta(TunerDragActiveMetaKey) && owner.GetMeta(TunerDragActiveMetaKey).AsBool():
				Vector2 offset = owner.HasMeta(TunerDragOffsetMetaKey) ? owner.GetMeta(TunerDragOffsetMetaKey).AsVector2() : Vector2.Zero;
				SetPanelPosition(owner, panel, motion.GlobalPosition - offset);
				panel.AcceptEvent();
				break;
		}
	}
}

public partial class CardEditorBaseDeckActionButton : NButton
{
	private const float DefaultWidth = 256f;
	private const float DefaultHeight = 90f;
	private const float LabelHorizontalPadding = 27f;

	private static readonly StringName ShaderV = new("v");

	private TextureRect _image = null!;
	private Label _label = null!;
	private TextureRect _activeOutline = null!;
	private ShaderMaterial _hsvMaterial = null!;
	private Tween? _tween;
	private bool _selected;
	private bool _emphasized;
	private float _sharedTextOffsetX;
	private float _sharedTextOffsetY;
	private float _localTextOffsetX;
	private float _localTextOffsetY;
	private float _buttonWidth = DefaultWidth;
	private float _buttonHeight = DefaultHeight;
	private Color _imageTint = Colors.White;

	public event Action? Triggered;

	public void Initialize(string text, Color tint, Texture2D texture, Texture2D outlineTexture, Font? font, Theme? labelTheme, Material? outlineMaterial, Shader? hsvShader)
	{
		Name = $"ActionButton{text.Replace(" ", string.Empty, StringComparison.Ordinal)}";
		_imageTint = tint;
		CustomMinimumSize = new Vector2(_buttonWidth, _buttonHeight);
		Size = new Vector2(_buttonWidth, _buttonHeight);
		PivotOffset = new Vector2(_buttonWidth * 0.5f, _buttonHeight * 0.5f);
		FocusMode = FocusModeEnum.All;
		MouseFilter = MouseFilterEnum.Stop;

		_activeOutline = new TextureRect
		{
			Name = "Outline",
			Texture = outlineTexture,
			ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = false,
			Modulate = new Color(0.3648f, 0.9104f, 0.96f, 0.752941f),
			Material = outlineMaterial
		};
		_activeOutline.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_activeOutline.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(_activeOutline);

		_image = new TextureRect
		{
			Name = "Image",
			Texture = texture,
			ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore
		};
		_image.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_hsvMaterial = new ShaderMaterial();
		_hsvMaterial.Shader = hsvShader;
		_hsvMaterial.SetShaderParameter(ShaderV, 0.9f);
		_image.Material = _hsvMaterial;
		AddChild(_image);

		_label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		_label.AnchorLeft = 0.5f;
		_label.AnchorTop = 0f;
		_label.AnchorRight = 0.5f;
		_label.AnchorBottom = 1f;
		if (font != null)
		{
			_label.AddThemeFontOverride("font", font);
		}
		if (labelTheme != null)
		{
			_label.Theme = labelTheme;
		}
		_label.AddThemeFontSizeOverride("font_size", 32);
		AddChild(_label);
		SetButtonSize(_buttonWidth, _buttonHeight);
		ApplyLabelOffsets();
	}

	public override void _Ready()
	{
		ConnectSignals();
		if (IsEnabled)
		{
			ApplyIdleVisual(immediate: true);
			return;
		}

		ApplyDisabledVisual();
	}

	public void SetButtonEnabled(bool enabled)
	{
		if (enabled)
		{
			Enable();
		}
		else
		{
			Disable();
		}
	}

	public void SetSelected(bool selected)
	{
		_selected = selected;
		ApplyIdleVisual(immediate: true);
	}

	public void SetEmphasized(bool emphasized)
	{
		_emphasized = emphasized;
		ApplyIdleVisual(immediate: true);
	}

	public void SetTextOffsets(float sharedX, float sharedY, float localX, float localY)
	{
		_sharedTextOffsetX = sharedX;
		_sharedTextOffsetY = sharedY;
		_localTextOffsetX = localX;
		_localTextOffsetY = localY;
		ApplyLabelOffsets();
	}

	public void SetButtonSize(float width, float height)
	{
		_buttonWidth = width;
		_buttonHeight = height;
		CustomMinimumSize = new Vector2(width, height);
		Size = new Vector2(width, height);
		PivotOffset = new Vector2(width * 0.5f, height * 0.5f);
		ApplyLabelOffsets();
	}

	public void SetTextSize(int fontSize)
	{
		_label.AddThemeFontSizeOverride("font_size", fontSize);
	}

	public void SetText(string text)
	{
		if (_label == null)
		{
			return;
		}

		_label.Text = text;
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		ApplyIdleVisual(immediate: true);
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		ApplyDisabledVisual();
	}

	protected override void OnFocus()
	{
		base.OnFocus();
		if (!IsEnabled)
		{
			return;
		}

		ApplyFocusedVisual(immediate: true);
	}

	protected override void OnUnfocus()
	{
		if (!IsEnabled)
		{
			return;
		}

		ApplyIdleVisual(immediate: false);
	}

	protected override void OnPress()
	{
		base.OnPress();
		if (!IsEnabled)
		{
			return;
		}

		_tween?.Kill();
	}

	protected override void OnRelease()
	{
		base.OnRelease();
		if (!IsEnabled)
		{
			return;
		}

		Triggered?.Invoke();
		if (IsFocused)
		{
			ApplyFocusedVisual(immediate: true);
		}
		else
		{
			ApplyIdleVisual(immediate: false);
		}
	}

	private void ApplyIdleVisual(bool immediate)
	{
		if (!IsNodeReady() || !IsEnabled)
		{
			return;
		}

		_tween?.Kill();
		bool shouldBrighten = _selected || _emphasized;
		float targetV = shouldBrighten ? 1.05f : 0.9f;
		Color targetLabelColor = shouldBrighten ? StsColors.cream : StsColors.halfTransparentCream;
		_activeOutline.Visible = _selected;
		_image.Modulate = _imageTint;
		_label.Modulate = targetLabelColor;
		Modulate = Colors.White;

		if (immediate)
		{
			Scale = Vector2.One;
			SetShaderValue(targetV);
			return;
		}

		_tween = CreateTween().SetParallel();
		_tween.TweenProperty(this, "scale", Vector2.One, 0.5).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
		_tween.TweenProperty(_label, "modulate", targetLabelColor, 0.5).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
		_tween.TweenMethod(Callable.From<float>(SetShaderValue), GetShaderValue(), targetV, 0.5).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
	}

	private void ApplyDisabledVisual()
	{
		if (_image == null || _label == null || _activeOutline == null)
		{
			return;
		}

		_tween?.Kill();
		Scale = Vector2.One;
		_activeOutline.Visible = false;
		_image.Modulate = _imageTint;
		_label.Modulate = StsColors.halfTransparentCream;
		SetShaderValue(0.82f);
		Modulate = new Color(0.68f, 0.68f, 0.68f, 1f);
	}

	private void ApplyFocusedVisual(bool immediate)
	{
		if (!IsNodeReady() || !IsEnabled)
		{
			return;
		}

		_tween?.Kill();
		_activeOutline.Visible = _selected;
		_image.Modulate = _imageTint;
		_label.Modulate = StsColors.gold;
		Modulate = Colors.White;

		if (immediate)
		{
			SetShaderValue(1.2f);
			Scale = Vector2.One * 1.05f;
			return;
		}

		_tween = CreateTween().SetParallel();
		_tween.TweenProperty(this, "scale", Vector2.One * 1.05f, 0.05).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
		_tween.TweenProperty(_label, "modulate", StsColors.gold, 0.05).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
		_tween.TweenMethod(Callable.From<float>(SetShaderValue), GetShaderValue(), 1.2f, 0.05).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
	}

	private float GetShaderValue()
	{
		return _hsvMaterial.GetShaderParameter(ShaderV).AsSingle();
	}

	private void SetShaderValue(float value)
	{
		_hsvMaterial.SetShaderParameter(ShaderV, value);
	}

	private void ApplyLabelOffsets()
	{
		if (_label == null)
		{
			return;
		}

		float totalOffsetX = _sharedTextOffsetX + _localTextOffsetX;
		float totalOffsetY = _sharedTextOffsetY + _localTextOffsetY;
		float halfLabelWidth = Math.Max(20f, _buttonWidth * 0.5f - LabelHorizontalPadding);
		_label.OffsetLeft = -halfLabelWidth + totalOffsetX;
		_label.OffsetTop = totalOffsetY;
		_label.OffsetRight = halfLabelWidth + totalOffsetX;
		_label.OffsetBottom = totalOffsetY;
	}
}
