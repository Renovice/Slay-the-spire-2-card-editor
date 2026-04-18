using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace SlayTheSpire2Mod.CardEditor;

public partial class NCardEditorBaseDeckPanel : PanelContainer
{
	private static readonly string _headerFontPath = "res://themes/kreon_bold_glyph_space_one.tres";
	private static readonly string _bodyFontPath = "res://themes/kreon_regular_glyph_space_one.tres";
	private const float OffsetLeftExpanded = -500f;
	private const float OffsetTopExpanded = 20f;
	private const float OffsetRightExpanded = -20f;
	private const float OffsetBottomExpanded = 420f;

	private static Font? _headerFont;
	private static Font? _bodyFont;

	private NCardLibrary _library = null!;
	private MarginContainer _margin = null!;
	private Label _titleLabel = null!;
	private Label _summaryLabel = null!;
	private Label _helpLabel = null!;
	private Button _primaryButton = null!;
	private Button _secondaryButton = null!;
	private Button _tertiaryButton = null!;
	private RichTextLabel _selectionList = null!;
	private float _scrollbarAlignOffsetX;
	private int _alignRetriesRemaining = 12;

	public static NCardEditorBaseDeckPanel Create(NCardLibrary library)
	{
		NCardEditorBaseDeckPanel panel = new NCardEditorBaseDeckPanel();
		panel.Initialize(library);
		panel.BuildUi();
		panel.RefreshState();
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
		Name = "CardEditorBaseDeckPanel";
		ZIndex = 60;
		MouseFilter = MouseFilterEnum.Stop;

		AnchorLeft = 1f;
		AnchorTop = 0f;
		AnchorRight = 1f;
		AnchorBottom = 0f;
		GrowHorizontal = GrowDirection.Begin;
		OffsetLeft = OffsetLeftExpanded;
		OffsetTop = OffsetTopExpanded;
		OffsetRight = OffsetRightExpanded;
		OffsetBottom = OffsetBottomExpanded;

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

		_titleLabel = new Label();
		StyleSectionLabel(_titleLabel);
		root.AddChild(_titleLabel);

		_summaryLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		StyleBodyLabel(_summaryLabel);
		root.AddChild(_summaryLabel);

		_helpLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		StyleBodyLabel(_helpLabel);
		_helpLabel.Modulate = new Color(0.92f, 0.88f, 0.78f, 0.95f);
		root.AddChild(_helpLabel);

		_primaryButton = CreateActionButton();
		_primaryButton.Pressed += OnPrimaryPressed;
		root.AddChild(_primaryButton);

		_secondaryButton = CreateActionButton();
		_secondaryButton.Pressed += OnSecondaryPressed;
		root.AddChild(_secondaryButton);

		_tertiaryButton = CreateActionButton();
		_tertiaryButton.Pressed += OnTertiaryPressed;
		root.AddChild(_tertiaryButton);

		_selectionList = new RichTextLabel
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			BbcodeEnabled = true,
			FitContent = false,
			ScrollActive = true,
			Visible = false
		};
		_selectionList.CustomMinimumSize = new Vector2(0, 140);
		if (_bodyFont != null)
		{
			_selectionList.AddThemeFontOverride("normal_font", _bodyFont);
		}
		_selectionList.AddThemeFontSizeOverride("normal_font_size", 18);
		root.AddChild(_selectionList);
	}

	private Button CreateActionButton()
	{
		Button button = new Button
		{
			CustomMinimumSize = new Vector2(0, 44),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		if (_bodyFont != null)
		{
			button.AddThemeFontOverride("font", _bodyFont);
		}
		button.AddThemeFontSizeOverride("font_size", 20);
		button.AddThemeColorOverride("font_color", StsColors.cream);
		return button;
	}

	public void RefreshState()
	{
		CardEditorBaseDeckUiState.EnsureValidCharacter();
		ModelId characterId = CardEditorBaseDeckUiState.EditingCharacterId;
		string characterTitle = CardEditorBaseDeckStore.GetCharacterTitle(characterId);
		int deckCount = CardEditorBaseDeckStore.GetDeckIds(characterId).Count;

		if (CardEditorUiState.IsBaseDeckAddActive)
		{
			_titleLabel.Text = CardEditorLoc.T("baseDeck.addTitle", "Base Deck Add");
			_summaryLabel.Text = $"{characterTitle} • {CardEditorLoc.T("baseDeck.deckCount", "Deck Cards")}: {deckCount.ToString(CultureInfo.InvariantCulture)}";
			_helpLabel.Text = CardEditorLoc.T("baseDeck.addHelp", "Left click queues one copy. Right click removes one queued copy. Confirm adds all queued cards to this base deck.");
			_primaryButton.Text = CardEditorLoc.T("baseDeck.confirmAdd", "Add Selected");
			_primaryButton.Disabled = CardEditorBaseDeckUiState.PendingAddCards.Count == 0;
			_secondaryButton.Text = CardEditorLoc.T("baseDeck.clearQueue", "Clear Selection");
			_secondaryButton.Disabled = CardEditorBaseDeckUiState.PendingAddCards.Count == 0;
			_tertiaryButton.Text = CardEditorLoc.T("baseDeck.backToDeck", "Back to Deck");
			_selectionList.Visible = true;
			_selectionList.Text = BuildSelectionText();
		}
		else
		{
			_titleLabel.Text = CardEditorLoc.T("baseDeck.title", "Base Deck");
			_summaryLabel.Text = $"{characterTitle} • {CardEditorLoc.T("baseDeck.deckCount", "Deck Cards")}: {deckCount.ToString(CultureInfo.InvariantCulture)}";
			_helpLabel.Text = CardEditorLoc.T("baseDeck.help", "Use the class tabs to switch starter decks. Left click edits a card. Right click removes one copy from the current base deck.");
			_primaryButton.Text = CardEditorLoc.T("baseDeck.addCard", "Add Card");
			_primaryButton.Disabled = false;
			_secondaryButton.Text = CardEditorLoc.T("button.revertToVanilla", "Revert to Vanilla");
			_secondaryButton.Disabled = !CardEditorBaseDeckStore.HasOverride(characterId);
			_tertiaryButton.Text = CardEditorLoc.T("baseDeck.backToEditor", "Back to Editor");
			_selectionList.Visible = false;
			_selectionList.Text = string.Empty;
		}

		_alignRetriesRemaining = 12;
		Callable.From(RefreshAlignmentToScrollbar).CallDeferred();
	}

	private string BuildSelectionText()
	{
		if (CardEditorBaseDeckUiState.PendingAddCards.Count == 0)
		{
			return CardEditorLoc.T("baseDeck.noSelection", "No cards selected yet.");
		}

		IEnumerable<IGrouping<ModelId, ModelId>> groups = CardEditorBaseDeckUiState.PendingAddCards
			.GroupBy(id => id)
			.OrderBy(g =>
			{
				CardModel? card = ModelDb.GetByIdOrNull<CardModel>(g.Key);
				return card?.Title ?? g.Key.ToString();
			}, StringComparer.CurrentCultureIgnoreCase);

		List<string> lines = new();
		foreach (IGrouping<ModelId, ModelId> group in groups)
		{
			CardModel? card = ModelDb.GetByIdOrNull<CardModel>(group.Key);
			string title = card?.Title ?? group.Key.ToString();
			lines.Add($"• {title} × {group.Count().ToString(CultureInfo.InvariantCulture)}");
		}

		return string.Join('\n', lines);
	}

	private void OnPrimaryPressed()
	{
		if (CardEditorUiState.IsBaseDeckAddActive)
		{
			CardEditorBaseDeckUiState.CommitPendingCards();
			return;
		}

		CardEditorBaseDeckUiState.EnterAddMode();
	}

	private void OnSecondaryPressed()
	{
		if (CardEditorUiState.IsBaseDeckAddActive)
		{
			CardEditorBaseDeckUiState.ClearPendingAddCards();
			return;
		}

		CardEditorBaseDeckStore.ResetToVanilla(CardEditorBaseDeckUiState.EditingCharacterId);
		CardEditorBaseDeckUiState.RefreshAll();
	}

	private void OnTertiaryPressed()
	{
		if (CardEditorUiState.IsBaseDeckAddActive)
		{
			CardEditorBaseDeckUiState.ExitAddModeToDeck();
			return;
		}

		CardEditorBaseDeckUiState.ExitToEditor();
	}

	private void RefreshAlignmentToScrollbar()
	{
		try
		{
			NCardLibraryGrid? grid = _library?.GetNodeOrNull<NCardLibraryGrid>("%CardGrid");
			Control? scrollbar = grid?.GetNodeOrNull<Control>("Scrollbar");
			if (scrollbar == null)
			{
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
			if (parentRect.Size.X <= 0f)
			{
				if (_alignRetriesRemaining-- > 0)
				{
					Callable.From(RefreshAlignmentToScrollbar).CallDeferred();
				}
				return;
			}

			float parentRightX = parentRect.Position.X + parentRect.Size.X;
			float desiredAlignOffset = barCenterX - (parentRightX + (OffsetLeftExpanded + OffsetRightExpanded) * 0.5f);
			float newOffset = Mathf.Clamp(desiredAlignOffset, -600f, -OffsetRightExpanded);
			if (Mathf.Abs(newOffset - _scrollbarAlignOffsetX) > 0.5f)
			{
				_scrollbarAlignOffsetX = newOffset;
				OffsetLeft = OffsetLeftExpanded + _scrollbarAlignOffsetX;
				OffsetTop = OffsetTopExpanded;
				OffsetRight = OffsetRightExpanded + _scrollbarAlignOffsetX;
				OffsetBottom = OffsetBottomExpanded;
			}
		}
		catch
		{
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

	private void StyleBodyLabel(Label label)
	{
		if (_bodyFont != null)
		{
			label.AddThemeFontOverride("font", _bodyFont);
		}
		label.AddThemeFontSizeOverride("font_size", 18);
		label.AddThemeColorOverride("font_color", StsColors.cream);
		label.AddThemeConstantOverride("outline_size", 0);
	}
}

internal static class CardEditorBaseDeckPanelHooks
{
	public static void Sync(NCardLibrary library)
	{
		if (library == null)
		{
			return;
		}

		NCardEditorBaseDeckPanel? panel = library.GetNodeOrNull<NCardEditorBaseDeckPanel>("CardEditorBaseDeckPanel");
		bool shouldShow = CardEditorUiState.IsBaseDeckActive || CardEditorUiState.IsBaseDeckAddActive;
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
