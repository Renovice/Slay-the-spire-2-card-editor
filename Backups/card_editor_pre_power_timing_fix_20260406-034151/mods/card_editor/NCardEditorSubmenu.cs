using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace SlayTheSpire2Mod.CardEditor;

public partial class NCardEditorSubmenu : NSubmenu
{
	private const string BackButtonScenePath = "res://scenes/ui/back_button.tscn";
	private const string CardGridScenePath = "res://scenes/cards/card_grid.tscn";

	private NCardGrid _grid = null!;

	private List<CardModel> _displayCards = new();

	private bool _uiBuilt;

	protected override Control? InitialFocusedControl => _grid?.DefaultFocusedControl;

	public static NCardEditorSubmenu? Create()
	{
		NCardEditorSubmenu menu = new NCardEditorSubmenu();
		menu.BuildUi();
		return menu;
	}

	public override void _Ready()
	{
		if (!_uiBuilt)
		{
			BuildUi();
		}
		ConnectSignals();
		if (_grid == null)
		{
			return;
		}
		_grid.Connect(NCardGrid.SignalName.HolderPressed, Callable.From<NCardHolder>(OnCardPressed));
		_grid.Connect(NCardGrid.SignalName.HolderAltPressed, Callable.From<NCardHolder>(OnCardPressed));
	}

	public override void OnSubmenuOpened()
	{
		base.OnSubmenuOpened();
		if (_grid == null)
		{
			Callable.From(RefreshCards).CallDeferred();
			return;
		}
		RefreshCards();
	}

	private void RefreshCards()
	{
		if (_grid == null)
		{
			return;
		}
		_displayCards = ModelDb.AllCards
			.Where((CardModel c) => c.ShouldShowInCardLibrary)
			.Select(CardEditorOverrides.BuildPreview)
			.ToList();
		_grid.SetCards(_displayCards, PileType.None, new List<SortingOrders> { SortingOrders.AlphabetAscending });
	}

	private void OnCardPressed(NCardHolder holder)
	{
		CardModel card = holder.CardModel;
		if (card == null || NModalContainer.Instance == null)
		{
			return;
		}
		NCardEditorPopup popup = NCardEditorPopup.Create(card, RefreshCards);
		NModalContainer.Instance.Add(popup);
	}

	private void BuildUi()
	{
		if (_uiBuilt)
		{
			return;
		}
		_uiBuilt = true;

		Name = "CardEditor";
		AnchorLeft = 0f;
		AnchorTop = 0f;
		AnchorRight = 1f;
		AnchorBottom = 1f;
		OffsetLeft = 0f;
		OffsetTop = 0f;
		OffsetRight = 0f;
		OffsetBottom = 0f;

		PackedScene backScene = ResourceLoader.Load<PackedScene>(BackButtonScenePath);
		if (backScene != null)
		{
			NBackButton backButton = backScene.Instantiate<NBackButton>(PackedScene.GenEditState.Disabled);
			backButton.Name = "BackButton";
			AddChild(backButton);
		}

		Label title = new Label
		{
			Name = "Title",
			Text = CardEditorLoc.T("submenu.title", "EDITOR"),
			HorizontalAlignment = HorizontalAlignment.Center,
			AnchorLeft = 0.5f,
			AnchorTop = 0f,
			AnchorRight = 0.5f,
			AnchorBottom = 0f,
			OffsetLeft = -120f,
			OffsetTop = 20f,
			OffsetRight = 120f,
			OffsetBottom = 60f
		};
		AddChild(title);

		PackedScene gridScene = ResourceLoader.Load<PackedScene>(CardGridScenePath);
		if (gridScene != null)
		{
			_grid = gridScene.Instantiate<NCardGrid>(PackedScene.GenEditState.Disabled);
			_grid.Name = "CardGrid";
			_grid.AnchorLeft = 0f;
			_grid.AnchorTop = 0f;
			_grid.AnchorRight = 1f;
			_grid.AnchorBottom = 1f;
			_grid.OffsetLeft = 0f;
			_grid.OffsetTop = 80f;
			_grid.OffsetRight = 0f;
			_grid.OffsetBottom = 0f;
			AddChild(_grid);
		}
	}
}
