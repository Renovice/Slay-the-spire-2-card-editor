using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;

namespace SlayTheSpire2Mod.CardEditor;

public partial class NCardEditorPopup : Control, IScreenContext
{
	private static readonly Vector2 _panelSize = new Vector2(1240, 760);
	private static readonly string _headerFontPath = "res://themes/kreon_bold_glyph_space_one.tres";
	private static readonly string _bodyFontPath = "res://themes/kreon_regular_glyph_space_one.tres";
	private const string _cardGridScenePath = "res://scenes/cards/card_grid.tscn";
	private static readonly Vector2 _fieldMinSize = new Vector2(0, 44);
	private static readonly Vector2 _numericFieldMinSize = new Vector2(340, 44);
	private static readonly Vector2 _amountFieldMinSize = new Vector2(90, 44);
	private static readonly Vector2 _spinButtonMinSize = new Vector2(34, 20);
	private static readonly Vector2 _spinContainerMinSize = new Vector2(34, 44);
	private const float _labelWidth = 210f;
	private const double _holdInitialDelaySeconds = 0.35;
	private const double _holdRepeatSlowSeconds = 0.12;
	private const double _holdRepeatFastSeconds = 0.04;
	private const double _holdAccelerationSeconds = 2.0;

	private static Font? _headerFont;
	private static Font? _bodyFont;

	private CardModel _previewCard = null!;
	private ModelId _cardId;
	private Action? _onApplied;
	private bool _useModalContainer;
	private bool _isUpgradeEditor;
	private bool _isCreatedCard;
	private bool _uiBuilt;
	private bool _advancedMode;

	private PanelContainer _panel = null!;
	private NCard? _cardPreviewNode;
	private bool _previewReadyConnected;
	private bool _previewUpdateQueued;
	private bool _suppressPreviewUpdate;
	private Control? _specificCardPickerOverlay;
	private Label? _cardNameLabel;
	private OptionButton _cardTypeSelect = null!;
	private OptionButton? _targetTypeSelect;
	private LineEdit _energyCostField = null!;
	private KeywordTickbox? _energyCostXTickbox;
	private LineEdit? _starCostField;
	private KeywordTickbox? _starCostXTickbox;
	private LineEdit _replayField = null!;
	private LineEdit? _sealedThroneStarsGainedField;
	private LineEdit? _drawCostReductionField;
	private LineEdit? _resonanceEnemyStrengthLossField;
	private LineEdit? _retainHandTurnsField;
	private OptionButton? _noDrawDurationSelect;
	private LineEdit? _noDrawTurnsField;
	private HBoxContainer? _noDrawTurnsRow;
	private OptionButton? _conquerorDurationSelect;
	private LineEdit? _conquerorTurnsField;
	private HBoxContainer? _conquerorTurnsRow;
	private OptionButton? _reflectDurationSelect;
	private LineEdit? _reflectTurnsField;
	private HBoxContainer? _reflectTurnsRow;
	private OptionButton? _slyGrantDurationSelect;
	private LineEdit? _slyGrantTurnsField;
	private HBoxContainer? _slyGrantTurnsRow;
	private OptionButton? _tempStrengthDurationSelect;
	private LineEdit? _tempStrengthTurnsField;
	private HBoxContainer? _tempStrengthTurnsRow;
	private OptionButton? _tempDexterityDurationSelect;
	private LineEdit? _tempDexterityTurnsField;
	private HBoxContainer? _tempDexterityTurnsRow;
	private OptionButton? _tempFocusDurationSelect;
	private LineEdit? _tempFocusTurnsField;
	private HBoxContainer? _tempFocusTurnsRow;
	private OptionButton _enchantmentSelect = null!;
	private LineEdit _enchantmentAmountField = null!;
	private OptionButton _afflictionSelect = null!;
	private LineEdit _afflictionAmountField = null!;
	private VBoxContainer _extraEffectsContainer = null!;
	private VBoxContainer? _cardSmithContainer;
	private Control? _defaultFocus;
	private KeywordTickbox? _advancedModeTickbox;

	private KeywordTickbox? _createdEnabledTickbox;
	private LineEdit? _createdTitleField;
	private OptionButton? _createdPoolSelect;
	private readonly List<CardEditorCreatedCardPool> _createdPoolOptions = new();
	private OptionButton? _createdRaritySelect;
	private readonly List<CardRarity> _createdRarityOptions = new();
	private OptionButton? _createdTargetSelect;
	private readonly List<TargetType> _createdTargetOptions = new();
	private LineEdit? _createdArtSearchField;
	private OptionButton? _createdPortraitSourceSelect;
	private readonly List<(ModelId? CardId, string? CustomFile)> _createdPortraitSourceOptions = new();
	private readonly List<(ModelId? CardId, string? CustomFile, string Label)> _createdPortraitSourceCatalog = new();
	private VBoxContainer? _createdEffectSourceListContainer;
	private readonly List<ModelId> _createdEffectSourceIds = new();
	private VBoxContainer? _createdEffectValueContainer;
	private KeywordTickbox? _createdFullArtTickbox;
	private OptionButton? _createdFinishSelect;
	private readonly List<CardEditorVisualFinish> _createdFinishOptions = new();
	private Button? _createdFinishEditorButton;
	private VBoxContainer? _createdFinishEditorContainer;
	private Dictionary<string, float> _createdFinishParams = new();
	private readonly Dictionary<string, HSlider> _createdFinishSliders = new();
	private readonly Dictionary<string, Label> _createdFinishValueLabels = new();
	private KeywordTickbox? _createdCustomTextTickbox;
	private TextEdit? _createdCustomTextField;
	private KeywordTickbox? _createdCustomTextUpgradedTickbox;
	private TextEdit? _createdCustomTextUpgradedField;

	private KeywordTickbox? _vanillaEnabledTickbox;
	private LineEdit? _vanillaTitleField;
	private KeywordTickbox? _vanillaFullArtTickbox;
	private OptionButton? _vanillaFinishSelect;
	private readonly List<CardEditorVisualFinish> _vanillaFinishOptions = new();
	private Button? _vanillaFinishEditorButton;
	private VBoxContainer? _vanillaFinishEditorContainer;
	private Dictionary<string, float> _vanillaFinishParams = new();
	private readonly Dictionary<string, HSlider> _vanillaFinishSliders = new();
	private readonly Dictionary<string, Label> _vanillaFinishValueLabels = new();
	private OptionButton? _vanillaPoolSelect;
	private readonly List<string?> _vanillaPoolOptions = new();
	private OptionButton? _vanillaRaritySelect;
	private readonly List<CardRarity?> _vanillaRarityOptions = new();
	private LineEdit? _vanillaArtSearchField;
	private OptionButton? _vanillaPortraitSourceSelect;
	private readonly List<(ModelId? CardId, string? CustomFile)> _vanillaPortraitSourceOptions = new();
	private readonly List<(ModelId? CardId, string? CustomFile, string Label)> _vanillaPortraitSourceCatalog = new();

	private readonly List<CardType> _cardTypes = new();
	private readonly List<TargetType?> _targetTypeOptions = new();
	private readonly List<ModelId?> _enchantmentIds = new();
	private readonly List<ModelId?> _afflictionIds = new();
	private readonly Dictionary<CardKeyword, KeywordTickbox> _keywordChecks = new();
	private readonly Dictionary<string, LineEdit> _dynamicFields = new();
	private readonly Dictionary<ModelId, LineEdit> _hardcodedPowerAmountFields = new();
	private readonly Dictionary<ModelId, int> _hardcodedPowerAmountDefaults = new();
	private readonly Dictionary<LineEdit, SpinButtons> _spinButtons = new();
	private readonly List<ExtraEffectRow> _extraEffectRows = new();
	private readonly List<CardSmithRow> _cardSmithRows = new();
	private HoldSpinState? _holdSpinState;
	private UpgradeBaseline? _upgradeBaseline;

	public Control? DefaultFocusedControl => _defaultFocus;

	private sealed class UpgradeBaseline
	{
		public int BaseEnergyCost { get; init; }
		public bool BaseEnergyCostsX { get; init; }
		public int VanillaUpgradedEnergyCost { get; init; }
		public int VanillaEnergyDelta { get; init; }

		public int BaseStarCost { get; init; }
		public bool BaseHasStarCostX { get; init; }
		public int VanillaUpgradedStarCost { get; init; }
		public int VanillaStarDelta { get; init; }

		public int BaseReplayCount { get; init; }
		public int VanillaUpgradedReplayCount { get; init; }
		public int VanillaReplayDelta { get; init; }

		public Dictionary<string, decimal> BaseVars { get; init; } = new Dictionary<string, decimal>(StringComparer.Ordinal);
		public Dictionary<string, decimal> VanillaVarDeltas { get; init; } = new Dictionary<string, decimal>(StringComparer.Ordinal);

		public HashSet<CardKeyword> VanillaUpgradedKeywords { get; init; } = new HashSet<CardKeyword>();
	}

	public static NCardEditorPopup Create(CardModel previewCard, Action onApplied, bool useModalContainer = true, bool isUpgradeEditor = false)
	{
		NCardEditorPopup popup = new NCardEditorPopup();
		popup.Initialize(previewCard, onApplied, useModalContainer, isUpgradeEditor);
		popup.EnsureUiBuilt();
		return popup;
	}

	private void Initialize(CardModel previewCard, Action onApplied, bool useModalContainer, bool isUpgradeEditor)
	{
		_previewCard = previewCard;
		_cardId = previewCard.Id;
		_isCreatedCard = CardEditorCreatedCardsStore.IsCreatedCardId(_cardId);
		_onApplied = onApplied;
		_useModalContainer = useModalContainer;
		_isUpgradeEditor = isUpgradeEditor;

		bool storedAdvancedMode = CardEditorUiSettingsStore.GetAdvancedMode();
		_advancedMode = storedAdvancedMode || CardUsesAdvancedEffectUi(previewCard.Id);
		_upgradeBaseline = null;
		Name = "CardEditorPopup";
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		ZIndex = 100;
		Visible = true;
		MouseFilter = MouseFilterEnum.Stop;
		Log.Info("[CardEditor] Popup Initialize");
	}

	private static bool CardUsesAdvancedEffectUi(ModelId cardId)
	{
		try
		{
			CardOverride? stored = CardEditorOverrides.Get(cardId);
			if (stored == null)
			{
				return false;
			}

			static bool UsesAdvancedFields(CardExtraEffect? effect)
			{
				if (effect == null)
				{
					return false;
				}

				bool timingIsAdvanced = effect.Timing != CardExtraEffectTiming.Immediate
					&& (effect.AsPower || effect.Trigger != CardExtraEffectTrigger.OnPlay);
				if (timingIsAdvanced)
				{
					return true;
				}

				return false;
			}

			if (stored.ExtraEffects != null && stored.ExtraEffects.Any(UsesAdvancedFields))
			{
				return true;
			}

			if (stored.Upgrade?.ExtraEffects != null && stored.Upgrade.ExtraEffects.Any(UsesAdvancedFields))
			{
				return true;
			}

			return false;
		}
		catch
		{
			return false;
		}
	}

	public override void _Ready()
	{
		EnsureUiBuilt();

		// NCard.UpdateVisuals() is a no-op until the node is Ready. When the popup is first opened
		// after launching the game, the preview NCard can still be mid-initialization, which leaves
		// the default scene text ("Broken Card") visible until the next UI change. Force a refresh
		// once the preview node reports Ready.
		RefreshPreviewSoon();
		QueuePreviewUpdate();

		SceneTree? tree = GetTree();
		if (tree != null)
		{
			void Refresh()
			{
				ForcePreviewReload();
				QueuePreviewUpdate();
			}

			SceneTreeTimer t0 = tree.CreateTimer(0.0);
			t0.Timeout += Refresh;

			SceneTreeTimer t1 = tree.CreateTimer(0.05);
			t1.Timeout += Refresh;
		}
	}

	private void RefreshPreviewSoon()
	{
		if (_cardPreviewNode == null || !GodotObject.IsInstanceValid(_cardPreviewNode))
		{
			return;
		}

		if (_cardPreviewNode.IsNodeReady())
		{
			Callable.From(() =>
			{
				ForcePreviewReload();
				QueuePreviewUpdate();
			}).CallDeferred();
			return;
		}

		if (_previewReadyConnected)
		{
			return;
		}
		_previewReadyConnected = true;

		_cardPreviewNode.Connect(Node.SignalName.Ready, Callable.From(OnPreviewNodeReady));
	}

	private void OnPreviewNodeReady()
	{
		Callable.From(() =>
		{
			ForcePreviewReload();
			QueuePreviewUpdate();
		}).CallDeferred();
	}

	private void ForcePreviewReload()
	{
		if (_cardPreviewNode == null || !GodotObject.IsInstanceValid(_cardPreviewNode))
		{
			return;
		}
		if (!_cardPreviewNode.IsNodeReady())
		{
			return;
		}

		try
		{
			CardModel model = _previewCard;
			_cardPreviewNode.Model = null;
			_cardPreviewNode.Model = model;
			_cardPreviewNode.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] ForcePreviewReload failed: {ex}");
		}
	}

	public override void _Input(InputEvent inputEvent)
	{
		if (_holdSpinState != null && inputEvent is InputEventMouseButton inputEventMouseButton && inputEventMouseButton.ButtonIndex == MouseButton.Left && !inputEventMouseButton.Pressed)
		{
			StopSpinHold();
		}
	}

	public override void _ExitTree()
	{
		StopSpinHold();
	}

	public override void _Process(double delta)
	{
		if (_holdSpinState == null)
		{
			return;
		}
		if (!GodotObject.IsInstanceValid(_holdSpinState.Field) || _holdSpinState.Field.IsQueuedForDeletion())
		{
			StopSpinHold();
			return;
		}
		_holdSpinState.HeldSeconds += delta;
		_holdSpinState.RepeatCountdownSeconds -= delta;
		if (_holdSpinState.RepeatCountdownSeconds > 0)
		{
			return;
		}
		SpinStepOnce(_holdSpinState.Target, _holdSpinState.Direction);
		_holdSpinState.RepeatCountdownSeconds = GetHoldRepeatInterval(_holdSpinState.HeldSeconds);
	}

	private void EnsureUiBuilt()
	{
		if (_uiBuilt)
		{
			return;
		}
		_uiBuilt = true;
		Log.Info("[CardEditor] Popup BuildUi");
		BuildUi();
	}

	private void BuildUi()
	{
		_suppressPreviewUpdate = true;
		try
		{
			_headerFont ??= TryLoadFont(_headerFontPath);
			_bodyFont ??= TryLoadFont(_bodyFontPath);
			UpgradeBaseline? upgradeBaseline = _isUpgradeEditor ? GetUpgradeBaseline() : null;

			if (!_useModalContainer)
			{
				ColorRect backstop = new ColorRect
				{
					Color = StsColors.screenBackdrop,
					AnchorLeft = 0f,
					AnchorTop = 0f,
					AnchorRight = 1f,
					AnchorBottom = 1f,
					OffsetLeft = 0f,
					OffsetTop = 0f,
					OffsetRight = 0f,
					OffsetBottom = 0f,
					MouseFilter = MouseFilterEnum.Stop
				};
				AddChild(backstop);
			}
			_panel = new PanelContainer
			{
				CustomMinimumSize = _panelSize,
				ZIndex = 20
			};
			_panel.AnchorLeft = 0.5f;
			_panel.AnchorTop = 0.5f;
			_panel.AnchorRight = 0.5f;
			_panel.AnchorBottom = 0.5f;
			_panel.OffsetLeft = -_panelSize.X * 0.5f;
			_panel.OffsetTop = -_panelSize.Y * 0.5f;
			_panel.OffsetRight = _panelSize.X * 0.5f;
			_panel.OffsetBottom = _panelSize.Y * 0.5f;
			_panel.MouseFilter = MouseFilterEnum.Stop;
		StyleBoxFlat panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.07f, 0.06f, 0.96f),
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
		_panel.AddThemeStyleboxOverride("panel", panelStyle);
		AddChild(_panel);

		MarginContainer margin = new MarginContainer();
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 16);
		margin.AddThemeConstantOverride("margin_top", 16);
		margin.AddThemeConstantOverride("margin_right", 16);
		margin.AddThemeConstantOverride("margin_bottom", 16);
		_panel.AddChild(margin);

		VBoxContainer root = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		root.AddThemeConstantOverride("separation", 10);
		margin.AddChild(root);

		Label title = new Label
		{
			Text = _isUpgradeEditor
				? CardEditorLoc.T("popup.upgradeEditorTitle", "Upgrade Editor")
				: CardEditorLoc.T("popup.editorTitle", "Card Editor")
		};
		StyleHeaderLabel(title);
		root.AddChild(title);

		if (_isUpgradeEditor)
		{
			Label upgradeHelp = new Label
			{
				Text = CardEditorLoc.T(
					"popup.upgradeHelp",
					"These values change what the upgrade adds (+/-). They do not edit the base card."),
				AutowrapMode = TextServer.AutowrapMode.WordSmart
			};
			StyleHintLabel(upgradeHelp);
			root.AddChild(upgradeHelp);
		}

		HBoxContainer contentRow = new HBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		contentRow.AddThemeConstantOverride("separation", 24);
		root.AddChild(contentRow);

		VBoxContainer leftColumn = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(500, 0),
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		contentRow.AddChild(leftColumn);

		_cardNameLabel = new Label { Text = _previewCard.Title };
		StyleSectionLabel(_cardNameLabel);
		leftColumn.AddChild(_cardNameLabel);

		CenterContainer cardCenter = new CenterContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			ClipContents = true
		};
		leftColumn.AddChild(cardCenter);

		_cardPreviewNode = NCard.Create(_previewCard, ModelVisibility.Visible);
		if (_cardPreviewNode != null)
		{
			_cardPreviewNode.Scale = Vector2.One * 1.6f;
			_cardPreviewNode.MouseFilter = MouseFilterEnum.Ignore;
			cardCenter.AddChild(_cardPreviewNode);
			RefreshPreviewSoon();
		}

		ScrollContainer rightScroll = new ScrollContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		rightScroll.ZIndex = 40;
		rightScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		rightScroll.MouseFilter = MouseFilterEnum.Pass;
		contentRow.AddChild(rightScroll);

		MarginContainer rightScrollMargin = new MarginContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		rightScrollMargin.AddThemeConstantOverride("margin_left", 0);
		rightScrollMargin.AddThemeConstantOverride("margin_top", 0);
		rightScrollMargin.AddThemeConstantOverride("margin_bottom", 0);
		rightScrollMargin.AddThemeConstantOverride("margin_right", 40);
		rightScroll.AddChild(rightScrollMargin);

		VBoxContainer rightColumn = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		rightColumn.AddThemeConstantOverride("separation", 10);
		rightScrollMargin.AddChild(rightColumn);

		rightColumn.AddChild(CreateCardTypeRow());
		if (_isUpgradeEditor)
		{
			_cardTypeSelect.Disabled = true;
			_cardTypeSelect.SelfModulate = StsColors.gray;
		}

		if (!_isCreatedCard && !_isUpgradeEditor && ShouldShowVanillaTargetTypeRow())
		{
			rightColumn.AddChild(CreateTargetTypeRow());
		}

		if (!_isCreatedCard && !_isUpgradeEditor)
		{
			BuildVanillaAppearanceUi(rightColumn);
		}

		if (_isCreatedCard && !_isUpgradeEditor)
		{
			BuildCreatedCardMetaUi(rightColumn);
		}

		if (_isUpgradeEditor && upgradeBaseline != null)
		{
			CardUpgradeOverride? storedUpgrade = CardEditorOverrides.Get(_cardId)?.Upgrade;

			int desiredEnergyDelta = storedUpgrade?.EnergyCostDelta ?? upgradeBaseline.VanillaEnergyDelta;
			rightColumn.AddChild(CreateNumericRow(
				CardEditorLoc.T("field.energyCostChange", "Energy Cost Change"),
				desiredEnergyDelta.ToString(CultureInfo.InvariantCulture),
				out _energyCostField,
				minValue: -99,
				maxValue: 99,
				onChanged: QueuePreviewUpdate));
			if (upgradeBaseline.BaseEnergyCostsX)
			{
				_energyCostField.Editable = false;
				SetSpinEnabled(_energyCostField, enabled: false);
				_energyCostField.SelfModulate = StsColors.gray;
			}

			int desiredStarDelta = storedUpgrade?.StarCostDelta ?? upgradeBaseline.VanillaStarDelta;
			rightColumn.AddChild(CreateNumericRow(
				CardEditorLoc.T("field.starCostChange", "Star Cost Change"),
				desiredStarDelta.ToString(CultureInfo.InvariantCulture),
				out LineEdit starCostField,
				minValue: -99,
				maxValue: 99,
				onChanged: QueuePreviewUpdate));
			_starCostField = starCostField;
			starCostField.PlaceholderText = "0";
			if (upgradeBaseline.BaseHasStarCostX)
			{
				starCostField.Editable = false;
				SetSpinEnabled(starCostField, enabled: false);
				starCostField.SelfModulate = StsColors.gray;
			}

			int desiredReplayDelta = storedUpgrade?.ReplayCountDelta ?? upgradeBaseline.VanillaReplayDelta;
			rightColumn.AddChild(CreateNumericRow(
				CardEditorLoc.T("field.replayCountChange", "Replay Count Change"),
				desiredReplayDelta.ToString(CultureInfo.InvariantCulture),
				out _replayField,
				minValue: -999,
				maxValue: 999,
				onChanged: QueuePreviewUpdate));

			if (_isCreatedCard)
			{
				BuildCreatedCardUpgradeTextUi(rightColumn);
			}
		}
		else
		{
			bool energyX = _previewCard.EnergyCost.CostsX;
			string energyCostText;
			if (energyX)
			{
				energyCostText = "X";
			}
			else
			{
				int baseEnergyCost = _previewCard.EnergyCost.GetWithModifiers(CostModifiers.None);
				energyCostText = baseEnergyCost < 0 ? string.Empty : baseEnergyCost.ToString(CultureInfo.InvariantCulture);
			}
			rightColumn.AddChild(CreateCostRowWithXToggle(
				CardEditorLoc.T("field.energyCost", "Energy Cost"),
				energyCostText,
				energyX,
				out _energyCostField,
				out KeywordTickbox energyXTick,
				minValue: -1,
				maxValue: 99,
				onChanged: QueuePreviewUpdate));
			_energyCostXTickbox = energyXTick;
			_energyCostField.PlaceholderText = CardEditorLoc.T("value.none", "None");
			_energyCostXTickbox.Toggled += () =>
			{
				ApplyCostXUiState(_energyCostField, _energyCostXTickbox, metaKeyPreviousText: "card_editor_prev_energy_cost", placeholderWhenNonX: CardEditorLoc.T("value.none", "None"));
				QueuePreviewUpdate();
			};
			ApplyCostXUiState(_energyCostField, _energyCostXTickbox, metaKeyPreviousText: "card_editor_prev_energy_cost", placeholderWhenNonX: CardEditorLoc.T("value.none", "None"));

			bool starX = _previewCard.HasStarCostX;
			string starCostText;
			if (starX)
			{
				starCostText = "X";
			}
			else
			{
				int baseStarCost = _previewCard.BaseStarCost;
				starCostText = baseStarCost < 0 ? string.Empty : baseStarCost.ToString(CultureInfo.InvariantCulture);
			}
			rightColumn.AddChild(CreateCostRowWithXToggle(
				CardEditorLoc.T("field.starCost", "Star Cost"),
				starCostText,
				starX,
				out LineEdit starCostField,
				out KeywordTickbox starXTick,
				minValue: -1,
				maxValue: 99,
				onChanged: QueuePreviewUpdate));
			_starCostField = starCostField;
			_starCostXTickbox = starXTick;
			_starCostField.PlaceholderText = CardEditorLoc.T("value.none", "None");
			_starCostXTickbox.Toggled += () =>
			{
				ApplyCostXUiState(_starCostField, _starCostXTickbox, metaKeyPreviousText: "card_editor_prev_star_cost", placeholderWhenNonX: CardEditorLoc.T("value.none", "None"));
				QueuePreviewUpdate();
			};
			ApplyCostXUiState(_starCostField, _starCostXTickbox, metaKeyPreviousText: "card_editor_prev_star_cost", placeholderWhenNonX: CardEditorLoc.T("value.none", "None"));

			rightColumn.AddChild(CreateNumericRow(
				CardEditorLoc.T("field.replayCount", "Replay Count"),
				_previewCard.BaseReplayCount.ToString(CultureInfo.InvariantCulture),
				out _replayField,
				minValue: 0,
				maxValue: 999,
				onChanged: QueuePreviewUpdate));
		}
		_defaultFocus = _energyCostField;

		rightColumn.AddChild(CreateModelRow(
			CardEditorLoc.T("field.enchantment", "Enchantment"),
			out _enchantmentSelect,
			out _enchantmentAmountField));
		PopulateEnchantments();
		rightColumn.AddChild(CreateModelRow(
			CardEditorLoc.T("field.affliction", "Affliction"),
			out _afflictionSelect,
			out _afflictionAmountField));
		PopulateAfflictions();
		if (_isUpgradeEditor)
		{
			_enchantmentSelect.Disabled = true;
			_enchantmentSelect.SelfModulate = StsColors.gray;
			_enchantmentAmountField.Editable = false;
			SetSpinEnabled(_enchantmentAmountField, enabled: false);
			_enchantmentAmountField.SelfModulate = StsColors.gray;

			_afflictionSelect.Disabled = true;
			_afflictionSelect.SelfModulate = StsColors.gray;
			_afflictionAmountField.Editable = false;
			SetSpinEnabled(_afflictionAmountField, enabled: false);
			_afflictionAmountField.SelfModulate = StsColors.gray;
		}

		Label keywordLabel = new Label { Text = CardEditorLoc.T("section.keywords", "Keywords") };
		StyleSectionLabel(keywordLabel);
		rightColumn.AddChild(keywordLabel);

		GridContainer keywordGrid = new GridContainer { Columns = 3 };
		keywordGrid.AddThemeConstantOverride("h_separation", 6);
		keywordGrid.AddThemeConstantOverride("v_separation", 6);
		PackedScene tickboxScene = GD.Load<PackedScene>("res://scenes/ui/tickbox.tscn");
		foreach (CardKeyword keyword in Enum.GetValues<CardKeyword>())
		{
			if (keyword == CardKeyword.None)
			{
				continue;
			}

			Control tickboxVisuals = tickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
			Label label = new Label { Text = GetKeywordDisplayName(keyword) };
			StyleBodyLabel(label);
			KeywordTickbox tickbox = new KeywordTickbox(tickboxVisuals, label, _previewCard.Keywords.Contains(keyword));
			tickbox.Toggled += QueuePreviewUpdate;
			_keywordChecks[keyword] = tickbox;
			keywordGrid.AddChild(tickbox);
		}
		rightColumn.AddChild(keywordGrid);

		BuildVanillaEffectsUi(rightColumn);
		BuildExtraEffectsUi(rightColumn);

		Label varLabel = new Label
		{
			Text = _isUpgradeEditor
				? CardEditorLoc.T("section.numberChanges", "Number Changes")
				: CardEditorLoc.T("section.numbers", "Numbers")
		};
		StyleSectionLabel(varLabel);
		rightColumn.AddChild(varLabel);

		if (!_isUpgradeEditor && _cardId == ModelDb.GetId<TheSealedThrone>())
		{
			ModelId powerId = ModelDb.GetId<TheSealedThronePower>();
			decimal starsGained = 1m;
			if (CardEditorOverrides.TryGet(_cardId, out CardOverride existingOverride)
				&& existingOverride.PowerAmounts != null
				&& existingOverride.PowerAmounts.TryGetValue(powerId, out decimal overriddenAmount))
			{
				starsGained = overriddenAmount;
			}
			rightColumn.AddChild(CreateNumericRow(
				CardEditorLoc.T("field.starsGained", "Stars Gained"),
				((int)starsGained).ToString(CultureInfo.InvariantCulture),
				out LineEdit starsField,
				minValue: 0,
				maxValue: 99,
				onChanged: QueuePreviewUpdate));
			_sealedThroneStarsGainedField = starsField;
		}

		if (!_isUpgradeEditor && _cardId == ModelDb.GetId<KinglyKick>())
		{
			int reduction = 1;
			if (CardEditorOverrides.TryGet(_cardId, out CardOverride existingOverride) && existingOverride.DrawCostReduction.HasValue)
			{
				reduction = Math.Max(0, existingOverride.DrawCostReduction.Value);
			}
			rightColumn.AddChild(CreateNumericRow(
				CardEditorLoc.T("field.costReductionOnDraw", "Cost Reduction on Draw"),
				reduction.ToString(CultureInfo.InvariantCulture),
				out LineEdit reductionField,
				minValue: 0,
				maxValue: 99,
				onChanged: QueuePreviewUpdate));
			_drawCostReductionField = reductionField;
		}

		if (!_isUpgradeEditor && _cardId == ModelDb.GetId<Resonance>())
		{
			int loss = 1;
			if (CardEditorOverrides.TryGet(_cardId, out CardOverride existingOverride)
				&& existingOverride.DynamicVarBaseValues != null
				&& existingOverride.DynamicVarBaseValues.TryGetValue(CardEditorOverrideKeys.ResonanceEnemyStrengthLoss, out decimal overriddenLoss))
			{
				loss = Math.Clamp((int)overriddenLoss, 0, 99);
			}
			rightColumn.AddChild(CreateNumericRow(
				CardEditorLoc.T("field.enemyStrengthLoss", "Enemy Strength Loss"),
				loss.ToString(CultureInfo.InvariantCulture),
				out LineEdit lossField,
				minValue: 0,
				maxValue: 99,
				onChanged: QueuePreviewUpdate));
			_resonanceEnemyStrengthLossField = lossField;
		}

		if (_isCreatedCard && !_isUpgradeEditor)
		{
			_createdEffectValueContainer = new VBoxContainer
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			_createdEffectValueContainer.AddThemeConstantOverride("separation", 10);
			rightColumn.AddChild(_createdEffectValueContainer);
			RebuildCreatedEffectValueRows();
		}
		else
		{
			if (!_isUpgradeEditor)
			{
				BuildHardcodedPowerAmountsUi(rightColumn);
			}

			IEnumerable<KeyValuePair<string, decimal>> dynamicKeys = _previewCard.DynamicVars
				.OrderBy(p => p.Key)
				.Select(p => new KeyValuePair<string, decimal>(p.Key, p.Value.BaseValue));
			if (_isUpgradeEditor && upgradeBaseline != null)
			{
				dynamicKeys = upgradeBaseline.BaseVars.OrderBy(p => p.Key);
			}

			foreach ((string key, decimal baseValue) in dynamicKeys)
			{
				HBoxContainer row = new HBoxContainer();
				row.AddThemeConstantOverride("separation", 10);
				Label name = new Label { Text = key, CustomMinimumSize = new Vector2(_labelWidth, 0) };
				StyleBodyLabel(name);

				string fieldText = baseValue.ToString(CultureInfo.InvariantCulture);
				if (_isUpgradeEditor && upgradeBaseline != null)
				{
					CardUpgradeOverride? storedUpgrade = CardEditorOverrides.Get(_cardId)?.Upgrade;
					decimal vanillaDelta = upgradeBaseline.VanillaVarDeltas.TryGetValue(key, out decimal delta) ? delta : 0m;
					decimal desiredDelta = vanillaDelta;
					if (storedUpgrade?.DynamicVarDeltas != null && storedUpgrade.DynamicVarDeltas.TryGetValue(key, out decimal overriddenDelta))
					{
						desiredDelta = overriddenDelta;
					}
					fieldText = desiredDelta.ToString(CultureInfo.InvariantCulture);
				}

				NMegaLineEdit field = new NMegaLineEdit
				{
					Text = fieldText,
					SizeFlagsHorizontal = Control.SizeFlags.Fill,
					CustomMinimumSize = _numericFieldMinSize
				};
				StyleInput(field);
				field.TextChanged += _ => QueuePreviewUpdate();
				_dynamicFields[key] = field;
				Control spinButtons = CreateSpinButtons(field, step: 1m, minValue: null, maxValue: null);
				row.AddChild(name);
				row.AddChild(spinButtons);
				row.AddChild(field);
				row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
				rightColumn.AddChild(row);
			}
		}

		HBoxContainer buttons = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };

		if (_isUpgradeEditor)
		{
			Button editBase = new Button { Text = CardEditorLoc.T("button.editBase", "Edit Base") };
			editBase.Pressed += OpenBaseEditor;
			buttons.AddChild(editBase);
		}
		else
		{
			Button editUpgrade = new Button { Text = CardEditorLoc.T("button.editUpgrade", "Edit Upgrade") };
			editUpgrade.Pressed += OpenUpgradeEditor;
			buttons.AddChild(editUpgrade);
		}

		buttons.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
		buttons.Alignment = BoxContainer.AlignmentMode.End;
		Button apply = new Button { Text = CardEditorLoc.T("button.apply", "Apply") };
		Button reset = new Button { Text = CardEditorLoc.T("button.reset", "Reset") };
		Button cancel = new Button { Text = CardEditorLoc.T("button.cancel", "Cancel") };
		apply.Pressed += OnApplyPressed;
		reset.Pressed += OnResetPressed;
		cancel.Pressed += Close;
		buttons.AddChild(apply);
		buttons.AddChild(reset);
		buttons.AddChild(cancel);
			root.AddChild(buttons);
		}
		finally
		{
			_suppressPreviewUpdate = false;
		}
	}

	private sealed class KeywordTickbox : HBoxContainer
	{
		private readonly Control _tickedImage;
		private readonly Control _notTickedImage;

		public bool IsTicked { get; private set; }

		public event Action? Toggled;

		public KeywordTickbox(Control tickboxVisuals, Label label, bool initialTicked)
		{
			MouseFilter = MouseFilterEnum.Stop;
			FocusMode = FocusModeEnum.None;
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			AddThemeConstantOverride("separation", 6);

			tickboxVisuals.MouseFilter = MouseFilterEnum.Ignore;
			tickboxVisuals.CustomMinimumSize = new Vector2(48, 48);
			tickboxVisuals.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
			tickboxVisuals.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
			tickboxVisuals.Scale = Vector2.One * 0.66f;
			tickboxVisuals.PivotOffset = new Vector2(24, 24);

			label.MouseFilter = MouseFilterEnum.Ignore;
			label.VerticalAlignment = VerticalAlignment.Center;
			label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

			AddChild(tickboxVisuals);
			AddChild(label);

			_tickedImage = tickboxVisuals.GetNode<Control>("Ticked");
			_notTickedImage = tickboxVisuals.GetNode<Control>("NotTicked");
			SetTicked(initialTicked, notify: false);

			GuiInput += OnGuiInput;
		}

		private void OnGuiInput(InputEvent inputEvent)
		{
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
				Toggled?.Invoke();
			}
		}

		public void SetTickedSilent(bool value)
		{
			SetTicked(value, notify: false);
		}
	}

	private sealed class ExtraEffectRow
	{
		public bool IsUpgradeDeltaRow { get; init; }
		public Control Container { get; init; } = null!;
		public Button RemoveButton { get; init; } = null!;
		public OptionButton KindSelect { get; init; } = null!;
		public List<int> KindDefinitionIndices { get; init; } = new();
		public OptionButton TriggerSelect { get; init; } = null!;
		public OptionButton TargetSelect { get; init; } = null!;
		public OptionButton DurationSelect { get; init; } = null!;
		public OptionButton TimingSelect { get; init; } = null!;
		public Control TimingRow { get; init; } = null!;
		public OptionButton TimingModeSelect { get; init; } = null!;
		public OptionButton TimingBoundaryEdgeSelect { get; init; } = null!;
		public OptionButton TimingBoundarySideSelect { get; init; } = null!;
		public OptionButton TimingBoundaryOffsetSelect { get; init; } = null!;
		public LineEdit TurnsField { get; init; } = null!;
		public KeywordTickbox AmountXTickbox { get; init; } = null!;
		public LineEdit AmountField { get; init; } = null!;
		public KeywordTickbox DisableOnUpgradeTickbox { get; init; } = null!;
		public List<CardExtraEffectTarget> AllowedTargets { get; } = new List<CardExtraEffectTarget>();

		public Control PowerConditionRow { get; init; } = null!;
		public Control PowerTimingRow { get; init; } = null!;
		public Control PowerCountEventRow { get; init; } = null!;
		public OptionButton PowerCountEventSelect { get; init; } = null!;
		public Control PowerFilterRow { get; init; } = null!;
		public OptionButton TriggerCardPoolSelect { get; init; } = null!;
		public OptionButton TriggerCardTypeSelect { get; init; } = null!;
		public OptionButton TriggerCardFilterSelect { get; init; } = null!;
		public OptionButton DrawnFromPileSelect { get; init; } = null!;
		public LineEdit TriggerEveryNField { get; init; } = null!;
		public LineEdit TriggerMaxFiresField { get; init; } = null!;
		public LineEdit TriggerMaxTurnsField { get; init; } = null!;

		public Control RepeatRow { get; init; } = null!;
		public KeywordTickbox RepeatXTickbox { get; init; } = null!;
		public LineEdit RepeatCountField { get; init; } = null!;

		public KeywordTickbox GrantTickbox { get; init; } = null!;
		public Control GrantRow { get; init; } = null!;
		public OptionButton GrantPileSelect { get; init; } = null!;
		public OptionButton GrantModeSelect { get; init; } = null!;
		public Control GrantCountRow { get; init; } = null!;
		public KeywordTickbox GrantCountXTickbox { get; init; } = null!;
		public LineEdit GrantCountField { get; init; } = null!;
		public Control GrantFilterRow { get; init; } = null!;
		public OptionButton GrantCountPoolSelect { get; init; } = null!;
		public OptionButton GrantCountTypeSelect { get; init; } = null!;
		public OptionButton GrantCountFilterSelect { get; init; } = null!;
		public Control GrantDurationRow { get; init; } = null!;
		public OptionButton GrantDurationSelect { get; init; } = null!;
		public Control GrantTurnsRow { get; init; } = null!;
		public LineEdit GrantTurnsField { get; init; } = null!;

		public Control EnchantmentRow { get; init; } = null!;
		public OptionButton EnchantmentSelect { get; init; } = null!;
		public OptionButton EnchantmentDurationSelect { get; init; } = null!;
		public Control EnchantmentTurnsRow { get; init; } = null!;
		public LineEdit EnchantmentTurnsField { get; init; } = null!;

		public Control MoveCardsRow { get; init; } = null!;
		public Control MoveCardsRowTop { get; init; } = null!;
		public Control MoveCardsRowBottom { get; init; } = null!;
		public OptionButton MoveFromPileSelect { get; init; } = null!;
		public OptionButton MoveSelectionModeSelect { get; init; } = null!;
		public OptionButton MoveToPileSelect { get; init; } = null!;
		public OptionButton MoveToPositionSelect { get; init; } = null!;
		public Control CostFilterRow { get; init; } = null!;
		public KeywordTickbox CostFilterTickbox { get; init; } = null!;
		public LineEdit CostFilterField { get; init; } = null!;
		public Control DrawCostRow { get; init; } = null!;
		public KeywordTickbox DrawCostTickbox { get; init; } = null!;
		public LineEdit DrawCostField { get; init; } = null!;
		public Control IgnoreVariantRow { get; init; } = null!;
		public OptionButton IgnoreVariantSelect { get; init; } = null!;
		public Control CardGenerationVariantRow { get; init; } = null!;
		public OptionButton CardGenerationVariantSelect { get; init; } = null!;
		public Control TurnBoundaryRow { get; init; } = null!;
		public OptionButton TurnBoundaryEdgeSelect { get; init; } = null!;
		public OptionButton TurnBoundarySideSelect { get; init; } = null!;
		public OptionButton TurnBoundaryLocationSelect { get; init; } = null!;

		public Control SpecificCardRow { get; init; } = null!;
		public LineEdit SpecificCardIdField { get; init; } = null!;

		public Control OrbRow { get; init; } = null!;
		public OptionButton OrbActionSelect { get; init; } = null!;
		public OptionButton OrbScopeSelect { get; init; } = null!;
		public OptionButton OrbTypeSelect { get; init; } = null!;
		public OptionButton OrbSelectionSelect { get; init; } = null!;
		public OptionButton OrbFollowUpSelect { get; init; } = null!;

		public Control OstyActionRow { get; init; } = null!;
		public OptionButton OstyActionSelect { get; init; } = null!;

		public Control GrantedKeywordRow { get; init; } = null!;
		public OptionButton GrantedKeywordSelect { get; init; } = null!;

		public Control MultiplyStatRow { get; init; } = null!;
		public OptionButton MultiplyStatSelect { get; init; } = null!;

		public Control CreatedCostRow { get; init; } = null!;
		public OptionButton CreatedCostDurationSelect { get; init; } = null!;
		public LineEdit CreatedCostTurnsField { get; init; } = null!;

		public Control UpgradeRow { get; init; } = null!;
		public OptionButton UpgradeVariantSelect { get; init; } = null!;
		public OptionButton UpgradePileSelect { get; init; } = null!;

		public Control CardCostsLessRow { get; init; } = null!;
		public OptionButton CardCostsLessKindSelect { get; init; } = null!;
		public OptionButton CardCostsLessModeSelect { get; init; } = null!;
		public Label CardCostsLessLabel { get; init; } = null!;
		public OptionButton CardCostsLessDurationSelect { get; init; } = null!;
		public LineEdit CardCostsLessTurnsField { get; init; } = null!;

		public Control GeneratedCardRow { get; init; } = null!;
		public OptionButton GeneratedPoolSelect { get; init; } = null!;
		public OptionButton GeneratedTypeSelect { get; init; } = null!;

		public KeywordTickbox ScalingTickbox { get; init; } = null!;
		public KeywordTickbox PowerTickbox { get; init; } = null!;
		public Control ScalingToggleRow { get; init; } = null!;
		public Control ScalingRow { get; init; } = null!;
		public OptionButton CountModeSelect { get; init; } = null!;
		public OptionButton CountEventSelect { get; init; } = null!;
		public OptionButton CountWindowSelect { get; init; } = null!;
		public OptionButton CountPileSelect { get; init; } = null!;
		public Control CountTurnsRow { get; init; } = null!;
		public LineEdit CountTurnsField { get; init; } = null!;
		public Control CountWindowInclusionRow { get; init; } = null!;
		public OptionButton CountWindowInclusionSelect { get; init; } = null!;
		public Control BlockLostCountingModeRow { get; init; } = null!;
		public OptionButton BlockLostCountingModeSelect { get; init; } = null!;
		public Control CountConditionRow { get; init; } = null!;
		public OptionButton CountComparisonSelect { get; init; } = null!;
		public LineEdit CountConditionField { get; init; } = null!;
		public Control CountCardFilterRow { get; init; } = null!;
		public OptionButton CountPoolSelect { get; init; } = null!;
		public OptionButton CountTypeSelect { get; init; } = null!;
		public OptionButton CountFilterSelect { get; init; } = null!;
		public Control CountOrbFilterRow { get; init; } = null!;
		public OptionButton CountOrbTypeSelect { get; init; } = null!;
		public OptionButton CountOrbSelectionSelect { get; init; } = null!;
		public Control CountEnemyStatusRow { get; init; } = null!;
		public OptionButton CountEnemyStatusSelect { get; init; } = null!;
		public Control CountEnemyIntentRow { get; init; } = null!;
		public OptionButton CountEnemyIntentSelect { get; init; } = null!;
		public KeywordTickbox ScalingBaseTickbox { get; init; } = null!;
	}

	private sealed class CardSmithRow
	{
		public Control Container { get; init; } = null!;
		public OptionButton KindSelect { get; init; } = null!;
		public OptionButton TriggerSelect { get; init; } = null!;
		public Control TurnBoundaryRow { get; init; } = null!;
		public OptionButton TurnBoundaryEdgeSelect { get; init; } = null!;
		public OptionButton TurnBoundarySideSelect { get; init; } = null!;
		public OptionButton TurnBoundaryLocationSelect { get; init; } = null!;
		public OptionButton TargetSelect { get; init; } = null!;
		public OptionButton DurationSelect { get; init; } = null!;
		public OptionButton TimingSelect { get; init; } = null!;
		public Control TimingRow { get; init; } = null!;
		public OptionButton TimingModeSelect { get; init; } = null!;
		public OptionButton TimingBoundaryEdgeSelect { get; init; } = null!;
		public OptionButton TimingBoundarySideSelect { get; init; } = null!;
		public OptionButton TimingBoundaryOffsetSelect { get; init; } = null!;
		public LineEdit TurnsField { get; init; } = null!;
		public LineEdit AmountField { get; init; } = null!;
		public List<CardExtraEffectTarget> AllowedTargets { get; } = new List<CardExtraEffectTarget>();

		public OptionButton CountEventSelect { get; init; } = null!;
		public OptionButton CountWindowSelect { get; init; } = null!;
		public Control CountTurnsRow { get; init; } = null!;
		public LineEdit CountTurnsField { get; init; } = null!;
		public Control CountWindowInclusionRow { get; init; } = null!;
		public OptionButton CountWindowInclusionSelect { get; init; } = null!;
		public Control BlockLostCountingModeRow { get; init; } = null!;
		public OptionButton BlockLostCountingModeSelect { get; init; } = null!;
		public OptionButton CountPoolSelect { get; init; } = null!;
		public OptionButton CountTypeSelect { get; init; } = null!;
		public Control GeneratedCardRow { get; init; } = null!;
		public OptionButton GeneratedPoolSelect { get; init; } = null!;
		public OptionButton GeneratedTypeSelect { get; init; } = null!;
		public KeywordTickbox BlockOnlyTickbox { get; init; } = null!;
	}

	private sealed class SpinButtons
	{
		public LineEdit Field { get; init; } = null!;
		public Control Container { get; init; } = null!;
		public Button Up { get; init; } = null!;
		public Button Down { get; init; } = null!;
		public decimal Step { get; init; }
		public decimal? MinValue { get; init; }
		public decimal? MaxValue { get; init; }
		public bool IsInteger { get; init; }
	}

	private sealed class HoldSpinState
	{
		public LineEdit Field { get; init; } = null!;
		public SpinButtons Target { get; init; } = null!;
		public int Direction { get; init; }
		public double HeldSeconds { get; set; }
		public double RepeatCountdownSeconds { get; set; }
	}

	private enum UnifiedCardCostEffectVariant
	{
		ThisCardEnergy = 0,
		ThisCardStars = 1,
		MatchingCardsEnergy = 2,
		MatchingCardsStars = 3,
		DrawnCards = 4,
		CreatedCards = 5
	}

	private enum UnifiedUpgradeEffectVariant
	{
		CreatedByThisCard = 0,
		CreatedCardsAura = 1,
		CardsInPilesAura = 2
	}

	private enum UnifiedCardGenerationVariant
	{
		RandomCardToHand = 0,
		ChooseOneOfThree = 1,
		CopyOfThisCard = 2,
		AddSpecificCard = 3,
		FetchSpecificCard = 4,
		CreatedCardsCostLess = 5,
		CreatedCardsUpgraded = 6
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

	private void StyleHeaderLabel(Label label)
	{
		_headerFont ??= TryLoadFont(_headerFontPath);
		if (_headerFont != null)
		{
			label.AddThemeFontOverride("font", _headerFont);
		}
		label.AddThemeFontSizeOverride("font_size", 46);
		label.AddThemeColorOverride("font_color", StsColors.gold);
		label.AddThemeColorOverride("font_outline_color", StsColors.transparentBlack);
		label.AddThemeConstantOverride("outline_size", 16);
	}

	private void StyleSectionLabel(Label label)
	{
		_headerFont ??= TryLoadFont(_headerFontPath);
		if (_headerFont != null)
		{
			label.AddThemeFontOverride("font", _headerFont);
		}
		label.AddThemeFontSizeOverride("font_size", 30);
		label.AddThemeColorOverride("font_color", StsColors.cream);
		label.AddThemeColorOverride("font_outline_color", StsColors.transparentBlack);
		label.AddThemeConstantOverride("outline_size", 12);
	}

	private void StyleBodyLabel(Control control)
	{
		_bodyFont ??= TryLoadFont(_bodyFontPath);
		if (_bodyFont != null)
		{
			control.AddThemeFontOverride("font", _bodyFont);
		}
		control.AddThemeFontSizeOverride("font_size", 20);
		control.AddThemeColorOverride("font_color", StsColors.cream);
		control.AddThemeColorOverride("font_outline_color", StsColors.transparentBlack);
		control.AddThemeConstantOverride("outline_size", 10);
	}

	private void StyleHintLabel(Label label)
	{
		_bodyFont ??= TryLoadFont(_bodyFontPath);
		if (_bodyFont != null)
		{
			label.AddThemeFontOverride("font", _bodyFont);
		}
		label.AddThemeFontSizeOverride("font_size", 18);
		label.AddThemeColorOverride("font_color", StsColors.gray);
		label.AddThemeColorOverride("font_outline_color", StsColors.transparentBlack);
		label.AddThemeConstantOverride("outline_size", 8);
	}

	private void StyleInput(Control control)
	{
		_bodyFont ??= TryLoadFont(_bodyFontPath);
		if (_bodyFont != null)
		{
			control.AddThemeFontOverride("font", _bodyFont);
		}
		control.AddThemeFontSizeOverride("font_size", 20);
		control.AddThemeColorOverride("font_color", StsColors.cream);
		control.AddThemeConstantOverride("outline_size", 0);
	}

	private static string GetKeywordDisplayName(CardKeyword keyword)
	{
		try
		{
			string upper = keyword.ToString().ToUpperInvariant();
			LocString? title = LocString.GetIfExists("card_keywords", upper + ".title");
			if (title != null)
			{
				return title.GetFormattedText();
			}
		}
		catch
		{
		}

		return keyword.ToString();
	}

	private void ConstrainOptionButtonPopup(OptionButton optionButton)
	{
		if (optionButton == null)
		{
			return;
		}

		const string constrainedMetaKey = "card_editor_popup_constrained";
		if (optionButton.HasMeta(constrainedMetaKey))
		{
			return;
		}
		optionButton.SetMeta(constrainedMetaKey, true);

		PopupMenu popup = optionButton.GetPopup();
		if (popup == null)
		{
			return;
		}

		void Clamp()
		{
			if (_panel == null || !GodotObject.IsInstanceValid(_panel))
			{
				return;
			}
			if (!GodotObject.IsInstanceValid(optionButton))
			{
				return;
			}
			if (!GodotObject.IsInstanceValid(popup))
			{
				return;
			}
			ClampPopupMenuToPanel(optionButton, popup);
		}

		popup.AboutToPopup += () =>
		{
			Clamp();
			SceneTree? tree = GetTree();
			if (tree != null)
			{
				SceneTreeTimer timer = tree.CreateTimer(0.0);
				timer.Timeout += Clamp;
			}
		};

		popup.VisibilityChanged += () =>
		{
			if (popup.Visible)
			{
				Clamp();
			}
		};
	}

	private void ClampPopupMenuToPanel(Control anchor, PopupMenu popup)
	{
		if (anchor == null || popup == null || _panel == null)
		{
			return;
		}

		Rect2 panelRect = _panel.GetGlobalRect();
		const float padding = 8f;
		Rect2 inner = new Rect2(
			panelRect.Position + new Vector2(padding, padding),
			panelRect.Size - new Vector2(padding * 2f, padding * 2f));

		Rect2 anchorRect = anchor.GetGlobalRect();
		float below = inner.End.Y - anchorRect.End.Y;
		float above = anchorRect.Position.Y - inner.Position.Y;
		below = Math.Max(0f, below);
		above = Math.Max(0f, above);

		// Prefer opening downward (like the base editor dropdowns) and rely on scroll if needed.
		// Only open upward if there is absolutely no room below.
		bool openDown = below > 0f;
		float available = openDown ? below : above;
		float cap = Math.Min(520f, inner.Size.Y * 0.6f);
		float maxHeight = Math.Min(available, cap);
		maxHeight = Math.Max(0f, maxHeight);

		float width = Math.Min(anchorRect.Size.X, inner.Size.X);
		width = Math.Max(240f, width);
		width = Math.Min(width, inner.Size.X);

		Vector2I maxSize = new Vector2I((int)MathF.Round(width), (int)MathF.Round(maxHeight));

		// In Godot 4, PopupMenu is a Window and should become scrollable once its height is capped via MaxSize.
		// Use the contents' minimum size to avoid carrying over a huge old Window.Size between different dropdowns.
		popup.ResetSize();
		Vector2 minContent = popup.GetContentsMinimumSize();
		int desiredHeight = (int)MathF.Round(minContent.Y);
		if (maxSize.Y > 0 && desiredHeight > maxSize.Y)
		{
			desiredHeight = maxSize.Y;
		}
		Vector2I contentSize = new Vector2I(maxSize.X, Math.Max(0, desiredHeight));
		popup.MinSize = new Vector2I(maxSize.X, 0);
		popup.MaxSize = maxSize.Y > 0 ? maxSize : new Vector2I(maxSize.X, int.MaxValue);

		float x = Mathf.Clamp(anchorRect.Position.X, inner.Position.X, inner.End.X - width);
		float y = openDown ? anchorRect.End.Y : anchorRect.Position.Y - contentSize.Y;
		y = Mathf.Clamp(y, inner.Position.Y, inner.End.Y - contentSize.Y);

		Vector2I pos = new Vector2I((int)MathF.Round(x), (int)MathF.Round(y));
		popup.Position = pos;
		popup.Size = contentSize;
	}

	private HBoxContainer CreateNumericRow(string labelText, string valueText, out LineEdit field, decimal? minValue, decimal? maxValue, Action? onChanged = null)
	{
		HBoxContainer row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		row.AddThemeConstantOverride("separation", 10);
		Label label = new Label { Text = labelText, CustomMinimumSize = new Vector2(_labelWidth, 0) };
		StyleBodyLabel(label);
		NMegaLineEdit lineEdit = new NMegaLineEdit { Text = valueText, SizeFlagsHorizontal = Control.SizeFlags.Fill, CustomMinimumSize = _numericFieldMinSize };
		StyleInput(lineEdit);
		if (onChanged != null)
		{
			lineEdit.TextChanged += _ => onChanged();
		}
		field = lineEdit;
		Control spinButtons = CreateSpinButtons(field, step: 1m, minValue: minValue, maxValue: maxValue, isInteger: true);
		row.AddChild(label);
		row.AddChild(spinButtons);
		row.AddChild(field);
		row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
		return row;
	}

	private HBoxContainer CreateCostRowWithXToggle(
		string labelText,
		string valueText,
		bool initialX,
		out LineEdit field,
		out KeywordTickbox xTickbox,
		decimal? minValue,
		decimal? maxValue,
		Action? onChanged = null)
	{
		HBoxContainer row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		row.AddThemeConstantOverride("separation", 10);
		Label label = new Label { Text = labelText, CustomMinimumSize = new Vector2(_labelWidth, 0) };
		StyleBodyLabel(label);

		PackedScene tickboxScene = GD.Load<PackedScene>("res://scenes/ui/tickbox.tscn");
		Control tickboxVisuals = tickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
		Label tickLabel = new Label { Text = "X" };
		StyleBodyLabel(tickLabel);
		xTickbox = new KeywordTickbox(tickboxVisuals, tickLabel, initialX);
		xTickbox.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;

		NMegaLineEdit lineEdit = new NMegaLineEdit { Text = valueText, SizeFlagsHorizontal = Control.SizeFlags.Fill, CustomMinimumSize = _numericFieldMinSize };
		StyleInput(lineEdit);
		if (onChanged != null)
		{
			lineEdit.TextChanged += _ => onChanged();
		}
		field = lineEdit;
		Control spinButtons = CreateSpinButtons(field, step: 1m, minValue: minValue, maxValue: maxValue, isInteger: true);
		row.AddChild(label);
		row.AddChild(xTickbox);
		row.AddChild(spinButtons);
		row.AddChild(field);
		row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
		return row;
	}

	private void ApplyCostXUiState(LineEdit field, KeywordTickbox? xTickbox, string metaKeyPreviousText, string? placeholderWhenNonX)
	{
		if (field == null || xTickbox == null)
		{
			return;
		}

		bool isX = xTickbox.IsTicked;
		if (isX)
		{
			if (!string.Equals(field.Text, "X", StringComparison.Ordinal))
			{
				field.SetMeta(metaKeyPreviousText, field.Text ?? string.Empty);
			}
			field.Text = "X";
			field.Editable = false;
			SetSpinEnabled(field, enabled: false);
		}
		else
		{
			field.Editable = true;
			SetSpinEnabled(field, enabled: true);
			if (placeholderWhenNonX != null)
			{
				field.PlaceholderText = placeholderWhenNonX;
			}

			string restored = string.Empty;
			if (field.HasMeta(metaKeyPreviousText))
			{
				Variant meta = field.GetMeta(metaKeyPreviousText);
				if (meta.VariantType != Variant.Type.Nil)
				{
					restored = meta.ToString();
				}
			}
			if (!string.IsNullOrWhiteSpace(restored) && !string.Equals(restored.Trim(), "X", StringComparison.OrdinalIgnoreCase))
			{
				field.Text = restored.Trim();
			}
			else if (string.Equals(field.Text, "X", StringComparison.OrdinalIgnoreCase))
			{
				field.Text = string.Empty;
			}
		}
	}

	private void ApplyEffectXPlusUiState(LineEdit field, KeywordTickbox? xTickbox, string metaKeyPreviousNonXText, string metaKeyPreviousXPlusText)
	{
		if (field == null || xTickbox == null)
		{
			return;
		}

		if (xTickbox.IsTicked)
		{
			string plus = "0";
			if (field.HasMeta(metaKeyPreviousXPlusText))
			{
				Variant meta = field.GetMeta(metaKeyPreviousXPlusText);
				if (meta.VariantType != Variant.Type.Nil)
				{
					string raw = meta.ToString();
					if (!string.IsNullOrWhiteSpace(raw))
					{
						plus = raw.Trim();
					}
				}
			}

			if (!string.Equals((field.Text ?? string.Empty).Trim(), plus, StringComparison.Ordinal))
			{
				field.SetMeta(metaKeyPreviousNonXText, field.Text ?? string.Empty);
			}

			field.Text = plus;
			field.Editable = true;
			SetSpinEnabled(field, enabled: true);
		}
		else
		{
			field.SetMeta(metaKeyPreviousXPlusText, field.Text ?? string.Empty);

			string restored = string.Empty;
			if (field.HasMeta(metaKeyPreviousNonXText))
			{
				Variant meta = field.GetMeta(metaKeyPreviousNonXText);
				if (meta.VariantType != Variant.Type.Nil)
				{
					restored = meta.ToString();
				}
			}

			field.Text = restored.Trim();
			field.Editable = true;
			SetSpinEnabled(field, enabled: true);
		}
	}

	private HBoxContainer CreateTextRow(string labelText, string valueText, out LineEdit field, Action? onChanged = null)
	{
		HBoxContainer row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		row.AddThemeConstantOverride("separation", 10);
		Label label = new Label { Text = labelText, CustomMinimumSize = new Vector2(_labelWidth, 0) };
		StyleBodyLabel(label);
		NMegaLineEdit lineEdit = new NMegaLineEdit
		{
			Text = valueText,
			SizeFlagsHorizontal = Control.SizeFlags.Fill,
			CustomMinimumSize = _numericFieldMinSize
		};
		StyleInput(lineEdit);
		if (onChanged != null)
		{
			lineEdit.TextChanged += _ => onChanged();
		}
		field = lineEdit;
		row.AddChild(label);
		row.AddChild(field);
		row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
		return row;
	}

	private HBoxContainer CreateDropdownRow(string labelText, out OptionButton select)
	{
		HBoxContainer row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		row.AddThemeConstantOverride("separation", 10);
		Label label = new Label { Text = labelText, CustomMinimumSize = new Vector2(_labelWidth, 0) };
		StyleBodyLabel(label);
		select = new OptionButton
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = _fieldMinSize
		};
		StyleInput(select);
		ConstrainOptionButtonPopup(select);
		row.AddChild(label);
		row.AddChild(select);
		return row;
	}

	private void PopulateFinishDropdown(OptionButton select, List<CardEditorVisualFinish> options, CardEditorVisualFinish? selectedFinish, Action onChanged)
	{
		select.Clear();
		options.Clear();

		AddFinishOption(select, options, CardEditorVisualFinish.None, CardEditorLoc.T("finish.none", "None"));
		AddFinishOption(select, options, CardEditorVisualFinish.RainbowRareFoil, CardEditorLoc.T("finish.rainbowRareFoil", "Rainbow Rare Foil"));
		AddFinishOption(select, options, CardEditorVisualFinish.RainbowGlitterArt, CardEditorLoc.T("finish.rainbowGlitterArt", "Rainbow Glitter (Art)"));
		AddFinishOption(select, options, CardEditorVisualFinish.PrismaticBandGlare, CardEditorLoc.T("finish.prismaticBandGlare", "Prismatic Band Glare"));
		AddFinishOption(select, options, CardEditorVisualFinish.PurpleWavesOcean, CardEditorLoc.T("finish.purpleWavesOcean", "Ocean Waves"));

		CardEditorVisualFinish desired = selectedFinish ?? CardEditorVisualFinish.None;
		int selectedIndex = options.IndexOf(desired);
		select.Selected = selectedIndex >= 0 ? selectedIndex : 0;
		select.ItemSelected += _ => onChanged();
	}

	private static void AddFinishOption(OptionButton select, List<CardEditorVisualFinish> options, CardEditorVisualFinish finish, string label)
	{
		select.AddItem(label);
		options.Add(finish);
	}

	private CardEditorVisualFinish GetSelectedCreatedFinish()
	{
		if (_createdFinishSelect != null
			&& _createdFinishSelect.Selected >= 0
			&& _createdFinishSelect.Selected < _createdFinishOptions.Count)
		{
			return _createdFinishOptions[_createdFinishSelect.Selected];
		}

		return CardEditorVisualFinish.None;
	}

	private CardEditorVisualFinish GetSelectedVanillaFinish()
	{
		if (_vanillaFinishSelect != null
			&& _vanillaFinishSelect.Selected >= 0
			&& _vanillaFinishSelect.Selected < _vanillaFinishOptions.Count)
		{
			return _vanillaFinishOptions[_vanillaFinishSelect.Selected];
		}

		return CardEditorVisualFinish.None;
	}

	private record FinishSliderDef(string Key, string Label, float Min, float Max, float Step, float Default);

	private static FinishSliderDef[] GetFinishSliderDefs(CardEditorVisualFinish finish) => finish switch
	{
		CardEditorVisualFinish.PrismaticBandGlare => new FinishSliderDef[]
		{
			new("strength", "Intensity", 0f, 1f, 0.01f, 0.58f),
			new("glareStrength", "Glare", 0f, 1f, 0.01f, 0.34f),
			new("metallicStrength", "Metallic", 0f, 1f, 0.01f, 0.42f),
			new("saturation", "Saturation", 0f, 1f, 0.01f, 1.0f),
			new("speed", "Speed", 0f, 3f, 0.01f, 1.0f),
			new("timeOffset", "Freeze Frame", 0f, 10f, 0.01f, 0f),
			new("hueShift", "Hue Shift", 0f, 1f, 0.01f, 0f),
			new("tintR", "Tint R", 0f, 1f, 0.01f, 1.0f),
			new("tintG", "Tint G", 0f, 1f, 0.01f, 1.0f),
			new("tintB", "Tint B", 0f, 1f, 0.01f, 1.0f),
			new("tintStrength", "Tint Strength", 0f, 1f, 0.01f, 0f),
		},
		CardEditorVisualFinish.RainbowGlitterArt => new FinishSliderDef[]
		{
			new("strength", "Intensity", 0f, 1f, 0.01f, 0.76f),
			new("brightness", "Brightness", 0.5f, 2f, 0.01f, 1.22f),
			new("pastel", "Pastel", 0f, 1f, 0.01f, 0.30f),
			new("saturation", "Saturation", 0f, 1f, 0.01f, 0.60f),
			new("speed", "Speed", 0f, 3f, 0.01f, 1.0f),
			new("timeOffset", "Freeze Frame", 0f, 10f, 0.01f, 0f),
			new("hueShift", "Hue Shift", 0f, 1f, 0.01f, 0f),
			new("tintR", "Tint R", 0f, 1f, 0.01f, 1.0f),
			new("tintG", "Tint G", 0f, 1f, 0.01f, 1.0f),
			new("tintB", "Tint B", 0f, 1f, 0.01f, 1.0f),
			new("tintStrength", "Tint Strength", 0f, 1f, 0.01f, 0f),
		},
		CardEditorVisualFinish.PurpleWavesOcean => new FinishSliderDef[]
		{
			new("strength", "Intensity", 0f, 1f, 0.01f, 0.60f),
			new("brightness", "Brightness", 0.5f, 2f, 0.01f, 1.0f),
			new("pastel", "Pastel", 0f, 1f, 0.01f, 0.15f),
			new("saturation", "Saturation", 0f, 1f, 0.01f, 1.0f),
			new("patternScale", "Zoom", 0.10f, 4f, 0.01f, 1.0f),
			new("horizonOffset", "Horizon", -2.5f, 2.5f, 0.01f, 0.25f),
			new("speed", "Speed", 0f, 3f, 0.01f, 1.0f),
			new("timeOffset", "Freeze Frame", 0f, 10f, 0.01f, 0f),
			new("hueShift", "Hue Shift", 0f, 1f, 0.01f, 0f),
			new("tintR", "Tint R", 0f, 1f, 0.01f, 1.0f),
			new("tintG", "Tint G", 0f, 1f, 0.01f, 1.0f),
			new("tintB", "Tint B", 0f, 1f, 0.01f, 1.0f),
			new("tintStrength", "Tint Strength", 0f, 1f, 0.01f, 0f),
		},
		_ => Array.Empty<FinishSliderDef>()
	};

	private void BuildFinishEditorSliders(
		VBoxContainer container,
		Dictionary<string, HSlider> sliders,
		Dictionary<string, Label> valueLabels,
		Dictionary<string, float> finishParams,
		CardEditorVisualFinish finish,
		Action onChanged)
	{
		foreach (Node child in container.GetChildren())
		{
			child.QueueFree();
		}
		sliders.Clear();
		valueLabels.Clear();

		FinishSliderDef[] defs = GetFinishSliderDefs(finish);
		if (defs.Length == 0)
		{
			Label noParams = new Label { Text = CardEditorLoc.T("finishEditor.noParams", "No editable parameters for this finish.") };
			StyleBodyLabel(noParams);
			container.AddChild(noParams);
			return;
		}

		foreach (FinishSliderDef def in defs)
		{
			float initialValue = finishParams.TryGetValue(def.Key, out float existing) ? existing : def.Default;

			HBoxContainer row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			row.AddThemeConstantOverride("separation", 8);

			Label label = new Label { Text = def.Label, CustomMinimumSize = new Vector2(_labelWidth * 0.6f, 0) };
			StyleBodyLabel(label);

			HSlider slider = new HSlider
			{
				MinValue = def.Min,
				MaxValue = def.Max,
				Step = def.Step,
				Value = initialValue,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				CustomMinimumSize = new Vector2(140, 0)
			};

			Label valLabel = new Label { Text = initialValue.ToString("F2"), CustomMinimumSize = new Vector2(50, 0) };
			StyleBodyLabel(valLabel);

			Button resetBtn = new Button { Text = "R", CustomMinimumSize = new Vector2(28, 0), TooltipText = "Reset to default" };
			StyleInput(resetBtn);
			float defVal = def.Default;
			string defKey = def.Key;
			resetBtn.Pressed += () =>
			{
				slider.Value = defVal;
				valLabel.Text = defVal.ToString("F2");
				finishParams[defKey] = defVal;
				onChanged();
			};

			slider.ValueChanged += (double val) =>
			{
				valLabel.Text = ((float)val).ToString("F2");
				finishParams[defKey] = (float)val;
				onChanged();
			};

			sliders[def.Key] = slider;
			valueLabels[def.Key] = valLabel;

			row.AddChild(label);
			row.AddChild(slider);
			row.AddChild(valLabel);
			row.AddChild(resetBtn);
			container.AddChild(row);
		}
	}

	private void OnCreatedFinishChanged()
	{
		CardEditorVisualFinish finish = GetSelectedCreatedFinish();
		bool hasSliders = GetFinishSliderDefs(finish).Length > 0;
		if (_createdFinishEditorButton != null)
			_createdFinishEditorButton.Visible = hasSliders;
		if (_createdFinishEditorContainer != null)
		{
			_createdFinishEditorContainer.Visible = false;
			if (hasSliders)
			{
				_createdFinishParams.Clear();
				BuildFinishEditorSliders(_createdFinishEditorContainer, _createdFinishSliders, _createdFinishValueLabels, _createdFinishParams, finish, OnCreatedCardMetaChanged);
			}
		}
		OnCreatedCardMetaChanged();
	}

	private void OnVanillaFinishChanged()
	{
		CardEditorVisualFinish finish = GetSelectedVanillaFinish();
		bool hasSliders = GetFinishSliderDefs(finish).Length > 0;
		if (_vanillaFinishEditorButton != null)
			_vanillaFinishEditorButton.Visible = hasSliders;
		if (_vanillaFinishEditorContainer != null)
		{
			_vanillaFinishEditorContainer.Visible = false;
			if (hasSliders)
			{
				_vanillaFinishParams.Clear();
				BuildFinishEditorSliders(_vanillaFinishEditorContainer, _vanillaFinishSliders, _vanillaFinishValueLabels, _vanillaFinishParams, finish, QueuePreviewUpdate);
			}
		}
		QueuePreviewUpdate();
	}

	private HBoxContainer CreateTickboxRow(string labelText, bool initialValue, out KeywordTickbox tickbox, Action? onToggled = null)
	{
		HBoxContainer row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		row.AddThemeConstantOverride("separation", 10);
		Label label = new Label { Text = labelText, CustomMinimumSize = new Vector2(_labelWidth, 0) };
		StyleBodyLabel(label);

		PackedScene tickboxScene = GD.Load<PackedScene>("res://scenes/ui/tickbox.tscn");
		Control tickboxVisuals = tickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
		Label emptyLabel = new Label { Text = string.Empty };
		StyleBodyLabel(emptyLabel);
		tickbox = new KeywordTickbox(tickboxVisuals, emptyLabel, initialValue);
		tickbox.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
		if (onToggled != null)
		{
			tickbox.Toggled += onToggled;
		}

		row.AddChild(label);
		row.AddChild(tickbox);
		row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
		return row;
	}

	private HBoxContainer CreateCardTypeRow()
	{
		HBoxContainer row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		row.AddThemeConstantOverride("separation", 10);
		Label label = new Label { Text = CardEditorLoc.T("field.cardType", "Card Type"), CustomMinimumSize = new Vector2(_labelWidth, 0) };
		StyleBodyLabel(label);
		OptionButton select = new OptionButton
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = _fieldMinSize
		};
		StyleInput(select);
		ConstrainOptionButtonPopup(select);
		_cardTypeSelect = select;
		_cardTypes.Clear();
		CardType[] supportedTypes =
		{
			CardType.Attack,
			CardType.Skill,
			CardType.Power,
			CardType.Status,
			CardType.Curse,
			CardType.Quest
		};

		foreach (CardType type in supportedTypes)
		{
			select.AddItem(CardEditorLoc.Enum("cardType", type, type.ToString()));
			_cardTypes.Add(type);
		}
		int index = _cardTypes.IndexOf(_previewCard.Type);
		if (index < 0)
		{
			index = _cardTypes.IndexOf(CardType.Attack);
			if (index < 0)
			{
				index = 0;
			}
		}
		select.Select(index);
		select.ItemSelected += _ =>
		{
			if (_isCreatedCard && !_isUpgradeEditor)
			{
				OnCreatedCardMetaChanged();
				return;
			}
			QueuePreviewUpdate();
		};
		row.AddChild(label);
		row.AddChild(select);
		return row;
	}

	private static bool IsSupportedVanillaTargetType(TargetType targetType)
	{
		return targetType == TargetType.AnyEnemy
			|| targetType == TargetType.RandomEnemy
			|| targetType == TargetType.AllEnemies;
	}

	private bool ShouldShowVanillaTargetTypeRow()
	{
		if (_isCreatedCard || _isUpgradeEditor)
		{
			return false;
		}

		try
		{
			CardModel canonical = ModelDb.GetById<CardModel>(_cardId);
			return IsSupportedVanillaTargetType(canonical.TargetType);
		}
		catch
		{
			return false;
		}
	}

	private HBoxContainer CreateTargetTypeRow()
	{
		HBoxContainer row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		row.AddThemeConstantOverride("separation", 10);

		Label label = new Label { Text = CardEditorLoc.T("field.targeting", "Targeting"), CustomMinimumSize = new Vector2(_labelWidth, 0) };
		StyleBodyLabel(label);

		OptionButton select = new OptionButton
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = _fieldMinSize
		};
		StyleInput(select);
		ConstrainOptionButtonPopup(select);

		_targetTypeSelect = select;
		_targetTypeOptions.Clear();

		select.AddItem(CardEditorLoc.T("targetType.Default", "Default"));
		_targetTypeOptions.Add(null);

		TargetType[] supportedTargets =
		{
			TargetType.AnyEnemy,
			TargetType.RandomEnemy,
			TargetType.AllEnemies
		};

		foreach (TargetType target in supportedTargets)
		{
			select.AddItem(CardEditorLoc.Enum("targetType", target, target.ToString()));
			_targetTypeOptions.Add(target);
		}

		int selectedIndex = 0;
		if (CardEditorOverrides.TryGet(_cardId, out CardOverride existingOverride) && existingOverride.TargetType.HasValue)
		{
			int index = _targetTypeOptions.IndexOf(existingOverride.TargetType.Value);
			if (index >= 0)
			{
				selectedIndex = index;
			}
		}
		select.Select(selectedIndex);
		select.ItemSelected += _ => QueuePreviewUpdate();

		row.AddChild(label);
		row.AddChild(select);
		return row;
	}

	private void BuildVanillaAppearanceUi(VBoxContainer rightColumn)
	{
		if (_isCreatedCard || _isUpgradeEditor || rightColumn == null)
		{
			return;
		}

		CardOverride? existing = CardEditorOverrides.Get(_cardId);

		// Parity fields with creator (cosmetic / dangerous toggles).
		Label header = new Label { Text = CardEditorLoc.T("section.appearance", "Appearance") };
		StyleSectionLabel(header);
		rightColumn.AddChild(header);

		rightColumn.AddChild(CreateTickboxRow(
			CardEditorLoc.T("field.enabledDanger", "Enabled (Dangerous for vanilla)"),
			existing?.Enabled != false,
			out KeywordTickbox enabledTickbox,
			QueuePreviewUpdate));
		_vanillaEnabledTickbox = enabledTickbox;

		string vanillaTitle = string.Empty;
		bool prevSuppress = CardEditorOverrides.SuppressAllOverrides;
		CardEditorOverrides.SuppressAllOverrides = true;
		try
		{
			vanillaTitle = ModelDb.GetById<CardModel>(_cardId)?.Title ?? string.Empty;
		}
		catch
		{
			vanillaTitle = string.Empty;
		}
		finally
		{
			CardEditorOverrides.SuppressAllOverrides = prevSuppress;
		}

		rightColumn.AddChild(CreateTextRow(
			CardEditorLoc.T("field.titleCosmetic", "Title (Cosmetic)"),
			existing?.TitleOverride ?? vanillaTitle,
			out LineEdit titleField,
			QueuePreviewUpdate));
		_vanillaTitleField = titleField;

		rightColumn.AddChild(CreateTickboxRow(
			CardEditorLoc.T("field.fullArtCosmetic", "Full Art (Cosmetic)"),
			existing?.FullArt == true,
			out KeywordTickbox fullArtTickbox,
			QueuePreviewUpdate));
		_vanillaFullArtTickbox = fullArtTickbox;

		rightColumn.AddChild(CreateDropdownRow(CardEditorLoc.T("field.finishCosmetic", "Finish (Cosmetic)"), out OptionButton finishSelect));
		_vanillaFinishSelect = finishSelect;
		PopulateFinishDropdown(finishSelect, _vanillaFinishOptions, existing?.Finish, OnVanillaFinishChanged);

		CardEditorVisualFinish existingFinish = existing?.Finish ?? CardEditorVisualFinish.None;
		_vanillaFinishEditorButton = new Button
		{
			Text = CardEditorLoc.T("finishEditor.editButton", "Edit Finish Settings"),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(_vanillaFinishEditorButton);
		_vanillaFinishEditorButton.Visible = GetFinishSliderDefs(existingFinish).Length > 0;
		_vanillaFinishEditorButton.Pressed += () =>
		{
			if (_vanillaFinishEditorContainer != null)
				_vanillaFinishEditorContainer.Visible = !_vanillaFinishEditorContainer.Visible;
		};
		rightColumn.AddChild(_vanillaFinishEditorButton);

		_vanillaFinishEditorContainer = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			Visible = false
		};
		_vanillaFinishEditorContainer.AddThemeConstantOverride("separation", 4);
		rightColumn.AddChild(_vanillaFinishEditorContainer);
		if (existing?.FinishParams != null)
			_vanillaFinishParams = new Dictionary<string, float>(existing.FinishParams);
		else
			_vanillaFinishParams.Clear();
		BuildFinishEditorSliders(_vanillaFinishEditorContainer, _vanillaFinishSliders, _vanillaFinishValueLabels, _vanillaFinishParams, existingFinish, QueuePreviewUpdate);

		rightColumn.AddChild(CreateDropdownRow(CardEditorLoc.T("field.class", "Class"), out OptionButton poolSelect));
		_vanillaPoolSelect = poolSelect;
		_vanillaPoolOptions.Clear();

		string defaultLabel = CardEditorLoc.T("value.default", "Default");
		poolSelect.AddItem(defaultLabel);
		_vanillaPoolOptions.Add(null);

		IEnumerable<CardPoolModel> pools = ModelDb.AllCardPools
			.Where(p => p != null && !string.IsNullOrWhiteSpace(p.Title))
			.GroupBy(p => p.Title.Trim(), StringComparer.OrdinalIgnoreCase)
			.Select(g => g.First())
			.OrderBy(p => p.Title, StringComparer.OrdinalIgnoreCase);

		foreach (CardPoolModel pool in pools)
		{
			string title = pool.Title.Trim();
			poolSelect.AddItem(ToDisplayTitle(title));
			_vanillaPoolOptions.Add(title);
		}

		int poolIndex = 0;
		if (existing != null && !string.IsNullOrWhiteSpace(existing.PoolTitle))
		{
			string desired = existing.PoolTitle.Trim();
			int found = _vanillaPoolOptions.FindIndex(s => !string.IsNullOrWhiteSpace(s) && string.Equals(s, desired, StringComparison.OrdinalIgnoreCase));
			if (found >= 0)
			{
				poolIndex = found;
			}
		}
		poolSelect.Select(poolIndex);
		poolSelect.ItemSelected += _ => QueuePreviewUpdate();

		rightColumn.AddChild(CreateDropdownRow(CardEditorLoc.T("field.rarity", "Rarity"), out OptionButton raritySelect));
		_vanillaRaritySelect = raritySelect;
		_vanillaRarityOptions.Clear();

		raritySelect.AddItem(defaultLabel);
		_vanillaRarityOptions.Add(null);
		foreach (CardRarity rarity in Enum.GetValues<CardRarity>())
		{
			if (rarity == CardRarity.None)
			{
				continue;
			}
			raritySelect.AddItem(CardEditorLoc.Enum("rarity", rarity, rarity.ToString()));
			_vanillaRarityOptions.Add(rarity);
		}

		int rarityIndex = 0;
		if (existing != null && existing.Rarity.HasValue && existing.Rarity.Value != CardRarity.None)
		{
			int found = _vanillaRarityOptions.IndexOf(existing.Rarity.Value);
			if (found >= 0)
			{
				rarityIndex = found;
			}
		}
		raritySelect.Select(rarityIndex);
		raritySelect.ItemSelected += _ => QueuePreviewUpdate();

		_vanillaPortraitSourceCatalog.Clear();
		foreach (CardModel card in ModelDb.AllCards)
		{
			if (card == null || card.Id == null)
			{
				continue;
			}
			if (card.Id == _cardId)
			{
				continue;
			}
			string label = $"{card.Title} ({ToDisplayTitle(card.Pool.Title)})";
			_vanillaPortraitSourceCatalog.Add((card.Id, null, label));
		}
		foreach (string file in CardEditorCreatedCardsStore.ListCustomPortraitFiles())
		{
			string prefix = CardEditorLoc.T("value.customPrefix", "Custom");
			string shortFile = file.Length > 30 ? file[..30] + "\u2026" : file;
			_vanillaPortraitSourceCatalog.Add((null, file, $"{prefix}: {shortFile}"));
		}
		_vanillaPortraitSourceCatalog.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));

		rightColumn.AddChild(CreateTextRow(CardEditorLoc.T("field.artSearch", "Art Search"), string.Empty, out LineEdit artSearchField, OnVanillaArtSearchChanged));
		_vanillaArtSearchField = artSearchField;

		rightColumn.AddChild(CreateDropdownRow(CardEditorLoc.T("field.art", "Art"), out OptionButton portraitSelect));
		_vanillaPortraitSourceSelect = portraitSelect;

		(ModelId? CardId, string? CustomFile) selectedPortrait = !string.IsNullOrWhiteSpace(existing?.CustomPortraitFile)
			? (null, existing!.CustomPortraitFile)
			: (existing?.PortraitSourceCardId ?? ModelId.none, null);
		RebuildVanillaArtDropdown(_vanillaArtSearchField.Text, selectedPortrait);
		portraitSelect.ItemSelected += _ => QueuePreviewUpdate();
	}

	private void OnVanillaArtSearchChanged()
	{
		if (_isCreatedCard || _isUpgradeEditor || _suppressPreviewUpdate)
		{
			return;
		}

		(ModelId? CardId, string? CustomFile) selected = GetSelectedVanillaPortraitSourceId();
		RebuildVanillaArtDropdown(_vanillaArtSearchField?.Text ?? string.Empty, selected);
		QueuePreviewUpdate();
	}

	private (ModelId? CardId, string? CustomFile) GetSelectedVanillaPortraitSourceId()
	{
		if (_vanillaPortraitSourceSelect != null
			&& _vanillaPortraitSourceSelect.Selected >= 0
			&& _vanillaPortraitSourceSelect.Selected < _vanillaPortraitSourceOptions.Count)
		{
			return _vanillaPortraitSourceOptions[_vanillaPortraitSourceSelect.Selected];
		}
		return (ModelId.none, null);
	}

	private void RebuildVanillaArtDropdown(string query, (ModelId? CardId, string? CustomFile) selectionToKeep)
	{
		if (_vanillaPortraitSourceSelect == null)
		{
			return;
		}

		string trimmed = query?.Trim() ?? string.Empty;
		List<(ModelId? CardId, string? CustomFile, string Label)> filtered;
		if (string.IsNullOrWhiteSpace(trimmed))
		{
			filtered = _vanillaPortraitSourceCatalog.ToList();
		}
		else
		{
			filtered = _vanillaPortraitSourceCatalog
				.Where(item => item.Label.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
				.ToList();
		}

		bool wantsKeep = (selectionToKeep.CardId != null && selectionToKeep.CardId != ModelId.none)
			|| !string.IsNullOrWhiteSpace(selectionToKeep.CustomFile);

		if (wantsKeep
			&& filtered.All(item => item.CardId != selectionToKeep.CardId || !string.Equals(item.CustomFile, selectionToKeep.CustomFile, StringComparison.OrdinalIgnoreCase)))
		{
			int index = _vanillaPortraitSourceCatalog.FindIndex(item =>
				item.CardId == selectionToKeep.CardId
				&& string.Equals(item.CustomFile, selectionToKeep.CustomFile, StringComparison.OrdinalIgnoreCase));
			if (index >= 0)
			{
				filtered.Insert(0, _vanillaPortraitSourceCatalog[index]);
			}
		}

		bool prevSuppress = _suppressPreviewUpdate;
		_suppressPreviewUpdate = true;
		try
		{
			_vanillaPortraitSourceSelect.Clear();
			_vanillaPortraitSourceOptions.Clear();

			_vanillaPortraitSourceSelect.AddItem(CardEditorLoc.T("value.none", "None"));
			_vanillaPortraitSourceOptions.Add((ModelId.none, null));

			foreach ((ModelId? cardId, string? customFile, string label) in filtered)
			{
				_vanillaPortraitSourceSelect.AddItem(label);
				_vanillaPortraitSourceOptions.Add((cardId, customFile));
			}

			int selectedIndex = _vanillaPortraitSourceOptions.FindIndex(item =>
				item.CardId == selectionToKeep.CardId
				&& string.Equals(item.CustomFile, selectionToKeep.CustomFile, StringComparison.OrdinalIgnoreCase));
			if (selectedIndex < 0)
			{
				selectedIndex = 0;
			}
			_vanillaPortraitSourceSelect.Select(selectedIndex);
		}
		finally
		{
			_suppressPreviewUpdate = prevSuppress;
		}
	}

	private void BuildCreatedCardMetaUi(VBoxContainer rightColumn)
	{
		if (!_isCreatedCard || _isUpgradeEditor || rightColumn == null)
		{
			return;
		}

		if (!CardEditorCreatedCardsStore.TryGetDefinition(_cardId, out CardEditorCreatedCardDefinition def))
		{
			def = new CardEditorCreatedCardDefinition();
		}

		Label header = new Label { Text = CardEditorLoc.T("section.creator", "Creator") };
		StyleSectionLabel(header);
		rightColumn.AddChild(header);

		rightColumn.AddChild(CreateTickboxRow(
			CardEditorLoc.T("field.enabled", "Enabled"),
			def.Enabled,
			out KeywordTickbox enabledTickbox,
			OnCreatedCardMetaChanged));
		_createdEnabledTickbox = enabledTickbox;

		rightColumn.AddChild(CreateTextRow(
			CardEditorLoc.T("field.title", "Title"),
			CardEditorCreatedCardsStore.GetTitleForCard(_cardId),
			out LineEdit titleField,
			OnCreatedCardMetaChanged));
		_createdTitleField = titleField;

		rightColumn.AddChild(CreateDropdownRow(CardEditorLoc.T("field.class", "Class"), out OptionButton poolSelect));
		_createdPoolSelect = poolSelect;
		_createdPoolOptions.Clear();
		foreach (CardEditorCreatedCardPool pool in Enum.GetValues<CardEditorCreatedCardPool>())
		{
			poolSelect.AddItem(CardEditorLoc.Enum("creatorPool", pool, pool.ToString()));
			_createdPoolOptions.Add(pool);
		}
		int poolIndex = _createdPoolOptions.IndexOf(def.Pool);
		if (poolIndex < 0)
		{
			poolIndex = 0;
		}
		poolSelect.Select(poolIndex);
		poolSelect.ItemSelected += _ => OnCreatedCardMetaChanged();

		rightColumn.AddChild(CreateDropdownRow(CardEditorLoc.T("field.rarity", "Rarity"), out OptionButton raritySelect));
		_createdRaritySelect = raritySelect;
		_createdRarityOptions.Clear();
		foreach (CardRarity rarity in Enum.GetValues<CardRarity>())
		{
			if (rarity == CardRarity.None)
			{
				continue;
			}
			raritySelect.AddItem(CardEditorLoc.Enum("rarity", rarity, rarity.ToString()));
			_createdRarityOptions.Add(rarity);
		}
		int rarityIndex = _createdRarityOptions.IndexOf(def.Rarity);
		if (rarityIndex < 0)
		{
			rarityIndex = 0;
		}
		raritySelect.Select(rarityIndex);
		raritySelect.ItemSelected += _ => OnCreatedCardMetaChanged();

		rightColumn.AddChild(CreateDropdownRow(CardEditorLoc.T("field.target", "Target"), out OptionButton targetSelect));
		_createdTargetSelect = targetSelect;
		_createdTargetOptions.Clear();
		foreach (TargetType target in Enum.GetValues<TargetType>())
		{
			targetSelect.AddItem(CardEditorLoc.Enum("targetType", target, target.ToString()));
			_createdTargetOptions.Add(target);
		}
		int targetIndex = _createdTargetOptions.IndexOf(def.TargetType);
		if (targetIndex < 0)
		{
			targetIndex = 0;
		}
		targetSelect.Select(targetIndex);
		targetSelect.ItemSelected += _ => OnCreatedCardMetaChanged();

		_createdPortraitSourceCatalog.Clear();
		foreach (CardModel card in ModelDb.AllCards)
		{
			if (card is CardEditorCreatedCardBase)
			{
				continue;
			}
			string label = $"{card.Title} ({ToDisplayTitle(card.Pool.Title)})";
			_createdPortraitSourceCatalog.Add((card.Id, null, label));
		}
		foreach (string file in CardEditorCreatedCardsStore.ListCustomPortraitFiles())
		{
			string prefix = CardEditorLoc.T("value.customPrefix", "Custom");
			string shortFile = file.Length > 30 ? file[..30] + "\u2026" : file;
			_createdPortraitSourceCatalog.Add((null, file, $"{prefix}: {shortFile}"));
		}
		_createdPortraitSourceCatalog.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));

		rightColumn.AddChild(CreateTextRow(CardEditorLoc.T("field.artSearch", "Art Search"), string.Empty, out LineEdit artSearchField, OnCreatedArtSearchChanged));
		_createdArtSearchField = artSearchField;

		rightColumn.AddChild(CreateDropdownRow(CardEditorLoc.T("field.art", "Art"), out OptionButton portraitSelect));
		_createdPortraitSourceSelect = portraitSelect;
		(ModelId? CardId, string? CustomFile) selectedPortrait = !string.IsNullOrWhiteSpace(def.CustomPortraitFile)
			? (null, def.CustomPortraitFile)
			: (def.PortraitSourceCardId ?? ModelId.none, null);
		RebuildCreatedArtDropdown(_createdArtSearchField.Text, selectedPortrait);
		portraitSelect.ItemSelected += _ => OnCreatedCardMetaChanged();

		// Effect Sources section — card picker + list display
		Label effectSourceSectionLabel = new Label { Text = CardEditorLoc.T("field.effectSources", "Effect Sources") };
		StyleSectionLabel(effectSourceSectionLabel);
		rightColumn.AddChild(effectSourceSectionLabel);

		_createdEffectSourceIds.Clear();
		if (def.EffectSourceCardIds != null)
		{
			_createdEffectSourceIds.AddRange(def.EffectSourceCardIds.Where(id => id != ModelId.none));
		}

		_createdEffectSourceListContainer = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_createdEffectSourceListContainer.AddThemeConstantOverride("separation", 6);
		rightColumn.AddChild(_createdEffectSourceListContainer);
		RebuildEffectSourceListUi();

		Button addEffectSourceButton = new Button
		{
			Text = CardEditorLoc.T("ui.addEffectSource", "+ Add Effect Source"),
			CustomMinimumSize = new Vector2(200, _fieldMinSize.Y)
		};
		StyleInput(addEffectSourceButton);
		addEffectSourceButton.Pressed += () =>
		{
			OpenSpecificCardPicker(selectedId =>
			{
				if (selectedId != ModelId.none
					&& selectedId != _cardId
					&& !_createdEffectSourceIds.Contains(selectedId))
				{
					_createdEffectSourceIds.Add(selectedId);
					RebuildEffectSourceListUi();
					OnCreatedCardMetaChanged();
				}
			});
		};
		rightColumn.AddChild(addEffectSourceButton);

		rightColumn.AddChild(CreateTickboxRow(
			CardEditorLoc.T("field.fullArt", "Full Art"),
			def.FullArt,
			out KeywordTickbox fullArtTickbox,
			OnCreatedCardMetaChanged));
		_createdFullArtTickbox = fullArtTickbox;

		rightColumn.AddChild(CreateDropdownRow(CardEditorLoc.T("field.finish", "Finish"), out OptionButton finishSelect));
		_createdFinishSelect = finishSelect;
		PopulateFinishDropdown(finishSelect, _createdFinishOptions, def.Finish, OnCreatedFinishChanged);

		_createdFinishEditorButton = new Button
		{
			Text = CardEditorLoc.T("finishEditor.editButton", "Edit Finish Settings"),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(_createdFinishEditorButton);
		_createdFinishEditorButton.Visible = GetFinishSliderDefs(def.Finish).Length > 0;
		_createdFinishEditorButton.Pressed += () =>
		{
			if (_createdFinishEditorContainer != null)
				_createdFinishEditorContainer.Visible = !_createdFinishEditorContainer.Visible;
		};
		rightColumn.AddChild(_createdFinishEditorButton);

		_createdFinishEditorContainer = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			Visible = false
		};
		_createdFinishEditorContainer.AddThemeConstantOverride("separation", 4);
		rightColumn.AddChild(_createdFinishEditorContainer);
		if (def.FinishParams != null)
			_createdFinishParams = new Dictionary<string, float>(def.FinishParams);
		else
			_createdFinishParams.Clear();
		BuildFinishEditorSliders(_createdFinishEditorContainer, _createdFinishSliders, _createdFinishValueLabels, _createdFinishParams, def.Finish, OnCreatedCardMetaChanged);

		// Custom text override — checkbox + text area
		bool hasCustomText = !string.IsNullOrWhiteSpace(def.CustomText);
		rightColumn.AddChild(CreateTickboxRow(
			CardEditorLoc.T("field.customText", "Custom Card Text"),
			hasCustomText,
			out KeywordTickbox customTextTickbox,
			OnCustomTextTickboxChanged));
		_createdCustomTextTickbox = customTextTickbox;

		_createdCustomTextField = new TextEdit
		{
			Text = def.CustomText ?? string.Empty,
			CustomMinimumSize = new Vector2(0, 100),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			PlaceholderText = CardEditorLoc.T("field.customTextPlaceholder", "Type your card text here..."),
			WrapMode = TextEdit.LineWrappingMode.Boundary,
			Visible = hasCustomText
		};
		StyleInput(_createdCustomTextField);
		_createdCustomTextField.TextChanged += OnCustomTextChanged;
		rightColumn.AddChild(_createdCustomTextField);
	}

	private void OnCreatedArtSearchChanged()
	{
		if (!_isCreatedCard || _isUpgradeEditor || _suppressPreviewUpdate)
		{
			return;
		}

		(ModelId? CardId, string? CustomFile) selected = GetSelectedPortraitSourceId();
		RebuildCreatedArtDropdown(_createdArtSearchField?.Text ?? string.Empty, selected);
		OnCreatedCardMetaChanged();
	}

	private void OnCustomTextTickboxChanged()
	{
		bool enabled = _createdCustomTextTickbox?.IsTicked ?? false;
		if (_createdCustomTextField != null)
		{
			_createdCustomTextField.Visible = enabled;
			if (!enabled)
			{
				_createdCustomTextField.Text = string.Empty;
			}
		}
		OnCreatedCardMetaChanged();
	}

	private void OnCustomTextChanged()
	{
		OnCreatedCardMetaChanged();
	}

	private void BuildCreatedCardUpgradeTextUi(VBoxContainer rightColumn)
	{
		if (!_isCreatedCard || !_isUpgradeEditor || rightColumn == null)
		{
			return;
		}

		string? existing = CardEditorCreatedCardsStore.GetCustomTextUpgraded(_cardId);
		bool hasCustomText = !string.IsNullOrWhiteSpace(existing);

		rightColumn.AddChild(CreateTickboxRow(
			CardEditorLoc.T("field.customTextUpgraded", "Custom Card Text (Upgraded)"),
			hasCustomText,
			out KeywordTickbox customTextTickbox,
			OnUpgradeCustomTextTickboxChanged));
		_createdCustomTextUpgradedTickbox = customTextTickbox;

		_createdCustomTextUpgradedField = new TextEdit
		{
			Text = existing ?? string.Empty,
			CustomMinimumSize = new Vector2(0, 100),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			PlaceholderText = CardEditorLoc.T("field.customTextPlaceholder", "Type your card text here..."),
			WrapMode = TextEdit.LineWrappingMode.Boundary,
			Visible = hasCustomText
		};
		StyleInput(_createdCustomTextUpgradedField);
		_createdCustomTextUpgradedField.TextChanged += OnUpgradeCustomTextChanged;
		rightColumn.AddChild(_createdCustomTextUpgradedField);
	}

	private void OnUpgradeCustomTextTickboxChanged()
	{
		if (!_isCreatedCard || !_isUpgradeEditor)
		{
			return;
		}

		bool enabled = _createdCustomTextUpgradedTickbox?.IsTicked ?? false;
		if (_createdCustomTextUpgradedField != null)
		{
			_createdCustomTextUpgradedField.Visible = enabled;
			if (!enabled)
			{
				_createdCustomTextUpgradedField.Text = string.Empty;
			}
		}

		string? customText = enabled ? _createdCustomTextUpgradedField?.Text : null;
		CardEditorCreatedCardsStore.SetDraftCustomTextUpgraded(_cardId, customText);
		QueuePreviewUpdate();
	}

	private void OnUpgradeCustomTextChanged()
	{
		if (!_isCreatedCard || !_isUpgradeEditor || _suppressPreviewUpdate)
		{
			return;
		}

		string? customText = (_createdCustomTextUpgradedTickbox?.IsTicked ?? false) ? _createdCustomTextUpgradedField?.Text : null;
		CardEditorCreatedCardsStore.SetDraftCustomTextUpgraded(_cardId, customText);
		QueuePreviewUpdate();
	}

	private (ModelId? CardId, string? CustomFile) GetSelectedPortraitSourceId()
	{
		if (_createdPortraitSourceSelect != null
			&& _createdPortraitSourceSelect.Selected >= 0
			&& _createdPortraitSourceSelect.Selected < _createdPortraitSourceOptions.Count)
		{
			return _createdPortraitSourceOptions[_createdPortraitSourceSelect.Selected];
		}
		return (ModelId.none, null);
	}

	private void RebuildCreatedArtDropdown(string query, (ModelId? CardId, string? CustomFile) selectionToKeep)
	{
		if (_createdPortraitSourceSelect == null)
		{
			return;
		}

		string trimmed = query?.Trim() ?? string.Empty;
		List<(ModelId? CardId, string? CustomFile, string Label)> filtered;
		if (string.IsNullOrWhiteSpace(trimmed))
		{
			filtered = _createdPortraitSourceCatalog.ToList();
		}
		else
		{
			filtered = _createdPortraitSourceCatalog
				.Where(item => item.Label.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
				.ToList();
		}

		bool wantsKeep = (selectionToKeep.CardId != null && selectionToKeep.CardId != ModelId.none)
			|| !string.IsNullOrWhiteSpace(selectionToKeep.CustomFile);

		if (wantsKeep
			&& filtered.All(item => item.CardId != selectionToKeep.CardId || !string.Equals(item.CustomFile, selectionToKeep.CustomFile, StringComparison.OrdinalIgnoreCase)))
		{
			int index = _createdPortraitSourceCatalog.FindIndex(item =>
				item.CardId == selectionToKeep.CardId
				&& string.Equals(item.CustomFile, selectionToKeep.CustomFile, StringComparison.OrdinalIgnoreCase));
			if (index >= 0)
			{
				filtered.Insert(0, _createdPortraitSourceCatalog[index]);
			}
		}

		bool prevSuppress = _suppressPreviewUpdate;
		_suppressPreviewUpdate = true;
		try
		{
			_createdPortraitSourceSelect.Clear();
			_createdPortraitSourceOptions.Clear();

			_createdPortraitSourceSelect.AddItem(CardEditorLoc.T("value.none", "None"));
			_createdPortraitSourceOptions.Add((ModelId.none, null));

			foreach ((ModelId? cardId, string? customFile, string label) in filtered)
			{
				_createdPortraitSourceSelect.AddItem(label);
				_createdPortraitSourceOptions.Add((cardId, customFile));
			}

			int selectedIndex = _createdPortraitSourceOptions.FindIndex(item =>
				item.CardId == selectionToKeep.CardId
				&& string.Equals(item.CustomFile, selectionToKeep.CustomFile, StringComparison.OrdinalIgnoreCase));
			if (selectedIndex < 0)
			{
				selectedIndex = 0;
			}
			_createdPortraitSourceSelect.Select(selectedIndex);
		}
		finally
		{
			_suppressPreviewUpdate = prevSuppress;
		}
	}

	private void RebuildEffectSourceListUi()
	{
		if (_createdEffectSourceListContainer == null)
		{
			return;
		}

		foreach (Node child in _createdEffectSourceListContainer.GetChildren())
		{
			child.QueueFree();
		}

		if (_createdEffectSourceIds.Count == 0)
		{
			Label noSourceLabel = new Label { Text = CardEditorLoc.T("ui.noEffectSources", "No effect sources added.") };
			StyleBodyLabel(noSourceLabel);
			noSourceLabel.Modulate = new Color(0.6f, 0.6f, 0.6f, 1f);
			_createdEffectSourceListContainer.AddChild(noSourceLabel);
			return;
		}

		for (int i = 0; i < _createdEffectSourceIds.Count; i++)
		{
			int index = i;
			ModelId sourceId = _createdEffectSourceIds[i];
			string title = sourceId.ToString();
			try
			{
				CardModel? card = ModelDb.GetByIdOrNull<CardModel>(sourceId);
				if (card != null)
				{
					title = $"{card.Title} ({ToDisplayTitle(card.Pool.Title)})";
				}
			}
			catch { }

			HBoxContainer row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			row.AddThemeConstantOverride("separation", 10);

			Label cardLabel = new Label
			{
				Text = $"{i + 1}. {title}",
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				CustomMinimumSize = new Vector2(200, _fieldMinSize.Y)
			};
			StyleBodyLabel(cardLabel);

			Button removeButton = new Button
			{
				Text = CardEditorLoc.T("ui.remove", "X"),
				CustomMinimumSize = new Vector2(40, _fieldMinSize.Y)
			};
			StyleInput(removeButton);
			removeButton.Pressed += () =>
			{
				if (index >= 0 && index < _createdEffectSourceIds.Count)
				{
					_createdEffectSourceIds.RemoveAt(index);
					RebuildEffectSourceListUi();
					OnCreatedCardMetaChanged();
				}
			};

			row.AddChild(cardLabel);
			row.AddChild(removeButton);
			_createdEffectSourceListContainer.AddChild(row);
		}
	}

	private void RebuildCreatedEffectValueRows()
	{
		if (!_isCreatedCard || _isUpgradeEditor || _createdEffectValueContainer == null)
		{
			return;
		}

		foreach (Node child in _createdEffectValueContainer.GetChildren())
		{
			child.QueueFree();
		}

		_dynamicFields.Clear();

		CardModel preview = CardEditorOverrides.BuildPreview(ModelDb.GetById<CardModel>(_cardId));
		CardOverride draft = BuildOverrideFromUi();
		CardEditorUiState.SetDraftOverride(_cardId, draft);
		CardEditorOverrides.ApplyOverrideToCard(preview, draft);

		foreach ((string key, decimal baseValue) in preview.DynamicVars
			.OrderBy(p => p.Key)
			.Select(p => new KeyValuePair<string, decimal>(p.Key, p.Value.BaseValue)))
		{
			HBoxContainer row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 10);
			Label name = new Label { Text = key, CustomMinimumSize = new Vector2(_labelWidth, 0) };
			StyleBodyLabel(name);

			NMegaLineEdit field = new NMegaLineEdit
			{
				Text = baseValue.ToString(CultureInfo.InvariantCulture),
				SizeFlagsHorizontal = Control.SizeFlags.Fill,
				CustomMinimumSize = _numericFieldMinSize
			};
			StyleInput(field);
			field.TextChanged += _ => QueuePreviewUpdate();
			_dynamicFields[key] = field;
			Control spinButtons = CreateSpinButtons(field, step: 1m, minValue: null, maxValue: null);
			row.AddChild(name);
			row.AddChild(spinButtons);
			row.AddChild(field);
			row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
			_createdEffectValueContainer.AddChild(row);
		}
	}

	private void OnCreatedCardMetaChanged()
	{
		if (!_isCreatedCard || _isUpgradeEditor || _suppressPreviewUpdate)
		{
			return;
		}

		bool enabled = _createdEnabledTickbox?.IsTicked ?? false;
		string? title = _createdTitleField?.Text;

		CardEditorCreatedCardPool pool = CardEditorCreatedCardPool.Ironclad;
		if (_createdPoolSelect != null && _createdPoolSelect.Selected >= 0 && _createdPoolSelect.Selected < _createdPoolOptions.Count)
		{
			pool = _createdPoolOptions[_createdPoolSelect.Selected];
		}

		CardRarity rarity = CardRarity.Common;
		if (_createdRaritySelect != null && _createdRaritySelect.Selected >= 0 && _createdRaritySelect.Selected < _createdRarityOptions.Count)
		{
			rarity = _createdRarityOptions[_createdRaritySelect.Selected];
		}

		CardType type = _previewCard.Type;
		if (_cardTypeSelect.Selected >= 0 && _cardTypeSelect.Selected < _cardTypes.Count)
		{
			type = _cardTypes[_cardTypeSelect.Selected];
		}
		if (type == CardType.None)
		{
			type = CardType.Attack;
		}

		TargetType target = TargetType.AnyEnemy;
		if (_createdTargetSelect != null && _createdTargetSelect.Selected >= 0 && _createdTargetSelect.Selected < _createdTargetOptions.Count)
		{
			target = _createdTargetOptions[_createdTargetSelect.Selected];
		}

		List<ModelId> effectSourceIds = new List<ModelId>(_createdEffectSourceIds);

		ModelId? portraitSourceId = null;
		string? customPortraitFile = null;
		if (_createdPortraitSourceSelect != null && _createdPortraitSourceSelect.Selected >= 0 && _createdPortraitSourceSelect.Selected < _createdPortraitSourceOptions.Count)
		{
			(ModelId? cardId, string? customFile) = _createdPortraitSourceOptions[_createdPortraitSourceSelect.Selected];
			if (!string.IsNullOrWhiteSpace(customFile))
			{
				customPortraitFile = customFile;
				portraitSourceId = null;
			}
			else
			{
				portraitSourceId = cardId;
				customPortraitFile = null;
			}
		}

		bool fullArt = _createdFullArtTickbox?.IsTicked ?? false;
		CardEditorVisualFinish finish = GetSelectedCreatedFinish();
		string? customText = (_createdCustomTextTickbox?.IsTicked ?? false) ? _createdCustomTextField?.Text : null;

		Dictionary<string, float>? fp = _createdFinishParams.Count > 0 ? new Dictionary<string, float>(_createdFinishParams) : null;

		CardEditorCreatedCardsStore.SetDraftMeta(_cardId, enabled, title, pool, rarity, type, target, effectSourceIds, portraitSourceId, customPortraitFile, fullArt, finish, customText, fp);
		if (_cardNameLabel != null && GodotObject.IsInstanceValid(_cardNameLabel))
		{
			_cardNameLabel.Text = CardEditorCreatedCardsStore.GetTitleForCard(_cardId);
		}
		RebuildCreatedEffectValueRows();
		QueuePreviewUpdate();
	}

	private HBoxContainer CreateModelRow(string labelText, out OptionButton select, out LineEdit amountField)
	{
		HBoxContainer row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		Label label = new Label { Text = labelText, CustomMinimumSize = new Vector2(_labelWidth, 0) };
		StyleBodyLabel(label);
		select = new OptionButton
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = _fieldMinSize
		};
		StyleInput(select);
		ConstrainOptionButtonPopup(select);
		NMegaLineEdit amount = new NMegaLineEdit
		{
			Text = "1",
			CustomMinimumSize = _amountFieldMinSize
		};
		amount.Alignment = HorizontalAlignment.Center;
		StyleInput(amount);
		amountField = amount;
		amountField.Editable = true;

		Control spinButtons = CreateSpinButtons(amountField, step: 1m, minValue: 1m, maxValue: 999m, isInteger: true);

		row.AddChild(label);
		row.AddChild(select);
		row.AddChild(spinButtons);
		row.AddChild(amountField);
		return row;
	}

	private void BuildHardcodedPowerAmountsUi(VBoxContainer rightColumn)
	{
		IReadOnlyList<CardEditorHardcodedPowerAmountSpec> specs = CardEditorHardcodedPowerAmounts.Get(_cardId);
		if (specs == null || specs.Count == 0)
		{
			return;
		}

		CardEditorOverrides.TryGet(_cardId, out CardOverride? existing);

		ModelId noDrawPowerId = ModelDb.GetId<NoDrawPower>();
		ModelId conquerorPowerId = ModelDb.GetId<ConquerorPower>();
		ModelId reflectPowerId = ModelDb.GetId<ReflectPower>();
		ModelId retainHandPowerId = ModelDb.GetId<RetainHandPower>();
		ModelId sealedThronePowerId = ModelDb.GetId<TheSealedThronePower>();

		foreach (CardEditorHardcodedPowerAmountSpec spec in specs)
		{
			ModelId powerId = spec.PowerId;

			if (powerId == noDrawPowerId
				|| powerId == conquerorPowerId
				|| powerId == reflectPowerId
				|| powerId == retainHandPowerId
				|| powerId == sealedThronePowerId)
			{
				continue;
			}

			PowerModel? power = ModelDb.GetByIdOrNull<PowerModel>(powerId);
			if (power == null || power.StackType != PowerStackType.Counter)
			{
				continue;
			}

			int value = spec.DefaultAmount;
			if (existing?.PowerAmounts != null && existing.PowerAmounts.TryGetValue(powerId, out decimal overridden))
			{
				value = (int)overridden;
			}

			string labelText = spec.LabelOverride;
			if (string.IsNullOrWhiteSpace(labelText))
			{
				string title = power.Title.GetFormattedText();
				labelText = string.IsNullOrWhiteSpace(title) ? power.Id.Entry : title;
			}

			rightColumn.AddChild(CreateNumericRow(labelText, value.ToString(CultureInfo.InvariantCulture), out LineEdit field, minValue: -99, maxValue: 999, onChanged: QueuePreviewUpdate));
			_hardcodedPowerAmountFields[powerId] = field;
			_hardcodedPowerAmountDefaults[powerId] = spec.DefaultAmount;
		}
	}

	private void BuildVanillaEffectsUi(VBoxContainer rightColumn)
	{
		if (_isUpgradeEditor)
		{
			return;
		}

		CardEditorOverrides.TryGet(_cardId, out CardOverride? existing);

		ModelId cardId = _cardId;

		bool showTempStrength = CardEditorVanillaEffectCards.UsesTemporaryStrength(cardId)
			|| existing?.TemporaryStrengthDuration != null
			|| existing?.TemporaryStrengthTurns != null;
		bool showTempDexterity = CardEditorVanillaEffectCards.UsesTemporaryDexterity(cardId)
			|| existing?.TemporaryDexterityDuration != null
			|| existing?.TemporaryDexterityTurns != null;
		bool showTempFocus = CardEditorVanillaEffectCards.UsesTemporaryFocus(cardId)
			|| existing?.TemporaryFocusDuration != null
			|| existing?.TemporaryFocusTurns != null;

		bool showNoDraw = cardId == ModelDb.GetId<BattleTrance>() || cardId == ModelDb.GetId<BulletTime>();
		bool showConqueror = cardId == ModelDb.GetId<Conqueror>();
		bool showReflect = cardId == ModelDb.GetId<Reflect>();
		bool showSly = cardId == ModelDb.GetId<HandTrick>();
		bool showRetainHand = cardId == ModelDb.GetId<Convergence>() || cardId == ModelDb.GetId<Equilibrium>() || cardId == ModelDb.GetId<Salvo>();

		if (!(showTempStrength || showTempDexterity || showTempFocus || showNoDraw || showConqueror || showReflect || showSly || showRetainHand))
		{
			return;
		}

		Label label = new Label { Text = CardEditorLoc.T("section.vanillaEffects", "Vanilla Effects") };
		StyleSectionLabel(label);
		rightColumn.AddChild(label);

		if (showTempStrength || showTempDexterity || showTempFocus)
		{
			BuildTemporaryStatDurationUi(rightColumn, existing, showTempStrength, showTempDexterity, showTempFocus);
		}

		if (showNoDraw)
		{
			BuildPowerDurationUiRow(
				rightColumn,
				"No Draw",
				ModelDb.GetId<NoDrawPower>(),
				existing,
				out _noDrawDurationSelect,
				out _noDrawTurnsRow,
				out _noDrawTurnsField);
		}

		if (showConqueror)
		{
			BuildPowerDurationUiRow(
				rightColumn,
				"Conqueror",
				ModelDb.GetId<ConquerorPower>(),
				existing,
				out _conquerorDurationSelect,
				out _conquerorTurnsRow,
				out _conquerorTurnsField);
		}

		if (showReflect)
		{
			BuildPowerDurationUiRow(
				rightColumn,
				"Reflect",
				ModelDb.GetId<ReflectPower>(),
				existing,
				out _reflectDurationSelect,
				out _reflectTurnsRow,
				out _reflectTurnsField);
		}
		if (showSly)
		{
			int selectedIndex = 0;
			int turnsValue = 2;
			if (existing != null)
			{
				if (existing.SlyGrantDuration == CardKeywordGrantDuration.ThisCombat)
				{
					selectedIndex = 1;
				}
				else if (existing.SlyGrantDuration == CardKeywordGrantDuration.Turns)
				{
					selectedIndex = 2;
					turnsValue = Math.Clamp(existing.SlyGrantTurns.GetValueOrDefault(2), 1, 99);
				}
			}

			BuildDurationUiRow(
				rightColumn,
				"Sly Duration",
				"Sly Turns",
				selectedIndex,
				turnsValue,
				minTurnsValue: 1,
				out _slyGrantDurationSelect,
				out _slyGrantTurnsRow,
				out _slyGrantTurnsField);
		}

		if (showRetainHand)
		{
			ModelId retainPowerId = ModelDb.GetId<RetainHandPower>();
			int turns = 1;
			if (existing != null
				&& existing.PowerAmounts != null
				&& existing.PowerAmounts.TryGetValue(retainPowerId, out decimal overridden))
			{
				turns = Math.Clamp((int)overridden, 0, 99);
			}
			else if (cardId == ModelDb.GetId<Equilibrium>())
			{
				if (existing?.DynamicVarBaseValues != null && existing.DynamicVarBaseValues.TryGetValue("Equilibrium", out decimal dvTurns))
				{
					turns = Math.Max(1, (int)dvTurns);
				}
				else if (_previewCard?.DynamicVars != null && _previewCard.DynamicVars.TryGetValue("Equilibrium", out var dv))
				{
					turns = Math.Max(1, (int)dv.BaseValue);
				}
			}

			rightColumn.AddChild(CreateNumericRow("Retain Hand Turns", turns.ToString(CultureInfo.InvariantCulture), out LineEdit field, minValue: 0, maxValue: 99, onChanged: QueuePreviewUpdate));
			_retainHandTurnsField = field;
		}
	}

	private void BuildPowerDurationUiRow(
		VBoxContainer rightColumn,
		string labelText,
		ModelId powerId,
		CardOverride? existing,
		out OptionButton? durationSelect,
		out HBoxContainer? turnsRow,
		out LineEdit? turnsField)
	{
		int selectedIndex = 0;
		int turnsValue = 2;

		int existingTurns = 1;
		if (existing?.PowerAmounts != null && existing.PowerAmounts.TryGetValue(powerId, out decimal overriddenAmount))
		{
			existingTurns = Math.Clamp((int)overriddenAmount, 0, 99);
		}

		if (existingTurns >= 99)
		{
			selectedIndex = 1;
		}
		else if (existingTurns == 0)
		{
			selectedIndex = 2;
			turnsValue = 0;
		}
		else if (existingTurns > 1)
		{
			selectedIndex = 2;
			turnsValue = Math.Clamp(existingTurns, 1, 99);
		}

		BuildDurationUiRow(
			rightColumn,
			$"{labelText} Duration",
			$"{labelText} Turns",
			selectedIndex,
			turnsValue,
			minTurnsValue: 0,
			out durationSelect,
			out turnsRow,
			out turnsField);
	}

	private void UpdateSlyGrantTurnsEnabled()
	{
		UpdateDurationTurnsEnabled(_slyGrantDurationSelect, _slyGrantTurnsField, _slyGrantTurnsRow);
	}

	private OptionButton CreateDurationSelect()
	{
		OptionButton select = new OptionButton
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = _fieldMinSize
		};
		StyleInput(select);
		ConstrainOptionButtonPopup(select);
		select.AddItem(CardEditorLoc.T("duration.thisTurn", "This Turn"));
		select.AddItem(CardEditorLoc.T("duration.thisCombat", "This Combat"));
		select.AddItem(CardEditorLoc.T("duration.xTurns", "X Turns"));
		return select;
	}

	private void UpdateDurationTurnsEnabled(OptionButton? durationSelect, LineEdit? turnsField, HBoxContainer? turnsRow)
	{
		if (durationSelect == null || turnsField == null || turnsRow == null)
		{
			return;
		}

		bool enableTurns = durationSelect.Selected == 2;
		turnsRow.Visible = enableTurns;
		turnsField.Editable = enableTurns;
		SetSpinEnabled(turnsField, enabled: enableTurns);
		turnsField.SelfModulate = enableTurns ? StsColors.cream : StsColors.gray;
	}

	private void BuildTemporaryStatDurationUi(VBoxContainer rightColumn, CardOverride? existing)
	{
		BuildTemporaryStatDurationUi(rightColumn, existing, showTempStrength: true, showTempDexterity: true, showTempFocus: true);
	}

	private void BuildTemporaryStatDurationUi(
		VBoxContainer rightColumn,
		CardOverride? existing,
		bool showTempStrength,
		bool showTempDexterity,
		bool showTempFocus)
	{
		_tempStrengthDurationSelect = null;
		_tempStrengthTurnsRow = null;
		_tempStrengthTurnsField = null;
		_tempDexterityDurationSelect = null;
		_tempDexterityTurnsRow = null;
		_tempDexterityTurnsField = null;
		_tempFocusDurationSelect = null;
		_tempFocusTurnsRow = null;
		_tempFocusTurnsField = null;

		if (showTempStrength)
		{
			BuildTemporaryStatDurationUiRow(
				rightColumn,
				"Temp Strength",
				existing?.TemporaryStrengthDuration,
				existing?.TemporaryStrengthTurns,
				out _tempStrengthDurationSelect,
				out _tempStrengthTurnsRow,
				out _tempStrengthTurnsField);
		}

		if (showTempDexterity)
		{
			BuildTemporaryStatDurationUiRow(
				rightColumn,
				"Temp Dexterity",
				existing?.TemporaryDexterityDuration,
				existing?.TemporaryDexterityTurns,
				out _tempDexterityDurationSelect,
				out _tempDexterityTurnsRow,
				out _tempDexterityTurnsField);
		}

		if (showTempFocus)
		{
			BuildTemporaryStatDurationUiRow(
				rightColumn,
				"Temp Focus",
				existing?.TemporaryFocusDuration,
				existing?.TemporaryFocusTurns,
				out _tempFocusDurationSelect,
				out _tempFocusTurnsRow,
				out _tempFocusTurnsField);
		}
	}

	private void BuildTemporaryStatDurationUiRow(
		VBoxContainer rightColumn,
		string labelText,
		CardKeywordGrantDuration? duration,
		int? turns,
		out OptionButton? durationSelect,
		out HBoxContainer? turnsRow,
		out LineEdit? turnsField)
	{
		int selectedIndex = 0;
		int turnsValue = 2;
		if (duration == CardKeywordGrantDuration.ThisCombat)
		{
			selectedIndex = 1;
		}
		else if (duration == CardKeywordGrantDuration.Turns)
		{
			selectedIndex = 2;
			turnsValue = Math.Clamp(turns.GetValueOrDefault(2), 1, 99);
		}

		BuildDurationUiRow(
			rightColumn,
			labelText,
			$"{labelText} Turns",
			selectedIndex,
			turnsValue,
			minTurnsValue: 1,
			out durationSelect,
			out turnsRow,
			out turnsField);
	}

	private void BuildDurationUiRow(
		VBoxContainer rightColumn,
		string durationLabelText,
		string turnsLabelText,
		int selectedIndex,
		int turnsValue,
		int minTurnsValue,
		out OptionButton? durationSelect,
		out HBoxContainer? turnsRow,
		out LineEdit? turnsField)
	{
		HBoxContainer durationRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		durationRow.AddThemeConstantOverride("separation", 10);
		Label durationLabel = new Label { Text = durationLabelText, CustomMinimumSize = new Vector2(_labelWidth, 0) };
		StyleBodyLabel(durationLabel);

		OptionButton select = CreateDurationSelect();
		select.Select(selectedIndex);

		durationRow.AddChild(durationLabel);
		durationRow.AddChild(select);
		rightColumn.AddChild(durationRow);

		HBoxContainer localTurnsRow = CreateNumericRow(turnsLabelText, turnsValue.ToString(CultureInfo.InvariantCulture), out LineEdit localTurnsField, minValue: minTurnsValue, maxValue: 99, onChanged: QueuePreviewUpdate);
		rightColumn.AddChild(localTurnsRow);

		select.ItemSelected += _ =>
		{
			UpdateDurationTurnsEnabled(select, localTurnsField, localTurnsRow);
			QueuePreviewUpdate();
		};

		UpdateDurationTurnsEnabled(select, localTurnsField, localTurnsRow);

		durationSelect = select;
		turnsRow = localTurnsRow;
		turnsField = localTurnsField;
	}

	private void BuildExtraEffectsUi(VBoxContainer rightColumn)
	{
		Label label = new Label { Text = CardEditorLoc.T("section.extraEffects", "Extra Effects") };
		StyleSectionLabel(label);
		rightColumn.AddChild(label);

		_extraEffectsContainer = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_extraEffectsContainer.AddThemeConstantOverride("separation", 8);

		Button add = new Button { Text = CardEditorLoc.T("button.addEffect", "Add Effect"), CustomMinimumSize = _fieldMinSize, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		StyleInput(add);
		add.Pressed += () => AddExtraEffectRow(effect: null);

		VBoxContainer effectSection = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		effectSection.AddThemeConstantOverride("separation", 8);

		PackedScene tickboxScene = GD.Load<PackedScene>("res://scenes/ui/tickbox.tscn");
		Control advancedTickboxVisuals = tickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
		Label advancedLabel = new Label { Text = CardEditorLoc.T("ui.advancedMode", "Advanced Mode") };
		StyleBodyLabel(advancedLabel);
		KeywordTickbox advancedTickbox = new KeywordTickbox(advancedTickboxVisuals, advancedLabel, _advancedMode);
		advancedTickbox.TooltipText = CardEditorLoc.T(
			"tooltip.advancedMode",
			"Shows advanced effect composition options (extra Timing combos, power delays, and more).");
		advancedTickbox.Toggled += () =>
		{
			_advancedMode = advancedTickbox.IsTicked;
			CardEditorUiSettingsStore.SetAdvancedMode(_advancedMode);
			RefreshAdvancedModeVisibility();
			QueuePreviewUpdate();
		};
		_advancedModeTickbox = advancedTickbox;

		effectSection.AddChild(advancedTickbox);
		effectSection.AddChild(_extraEffectsContainer);
		effectSection.AddChild(add);

		MarginContainer effectMargin = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		effectMargin.AddThemeConstantOverride("margin_left", 24);
		effectMargin.AddThemeConstantOverride("margin_top", 0);
		effectMargin.AddThemeConstantOverride("margin_right", 0);
		effectMargin.AddThemeConstantOverride("margin_bottom", 0);
		effectMargin.AddChild(effectSection);
		rightColumn.AddChild(effectMargin);

		_extraEffectRows.Clear();

		CardOverride? storedBase = CardEditorOverrides.Get(_cardId);
		List<CardExtraEffect>? baseEffects = storedBase?.ExtraEffects;
		List<CardExtraEffect>? upgradeEffects = storedBase?.Upgrade?.ExtraEffects;

		if (_isUpgradeEditor)
		{
			if (baseEffects != null && baseEffects.Count > 0)
			{
				bool numericFieldsAreDeltas = storedBase?.Upgrade?.ExtraEffectNumericFieldsAreDeltas ?? false;
				int baseCount = baseEffects.Count;
				for (int i = 0; i < baseCount; i++)
				{
					CardExtraEffect baseEffect = baseEffects[i];
					CardExtraEffect? upgradeEffect = null;

					if (upgradeEffects != null && i < upgradeEffects.Count)
					{
						CardExtraEffect? candidate = upgradeEffects[i];
						if (candidate != null && candidate.Kind == baseEffect.Kind)
						{
							upgradeEffect = candidate;
						}
					}

					AddExtraEffectRow(BuildUpgradeDeltaRowEffect(baseEffect, upgradeEffect, numericFieldsAreDeltas), isUpgradeDeltaRow: true);
				}

				if (upgradeEffects != null && upgradeEffects.Count > baseCount)
				{
					for (int i = baseCount; i < upgradeEffects.Count; i++)
					{
						CardExtraEffect? upgradeEffect = upgradeEffects[i];
						if (upgradeEffect != null && CardEditorExtraEffects.IsValidEffectAmount(upgradeEffect.Kind, upgradeEffect.Amount))
						{
							AddExtraEffectRow(upgradeEffect);
						}
					}
				}
			}
			else if (upgradeEffects != null && upgradeEffects.Count > 0)
			{
				foreach (CardExtraEffect? effect in upgradeEffects)
				{
					if (effect != null && CardEditorExtraEffects.IsValidEffectAmount(effect.Kind, effect.Amount))
					{
						AddExtraEffectRow(effect);
					}
				}
			}
			else
			{
				AddExtraEffectRow(effect: null);
			}

			return;
		}

		if (baseEffects != null && baseEffects.Count > 0)
		{
			foreach (CardExtraEffect effect in baseEffects)
			{
				if (effect != null && CardEditorExtraEffects.IsValidEffectAmount(effect.Kind, effect.Amount))
				{
					AddExtraEffectRow(effect);
				}
			}
		}
		else
		{
			AddExtraEffectRow(effect: null);
		}
	}

	private void RefreshAdvancedModeVisibility()
	{
		if (_advancedModeTickbox != null && GodotObject.IsInstanceValid(_advancedModeTickbox))
		{
			_advancedMode = _advancedModeTickbox.IsTicked;
		}

		foreach (ExtraEffectRow row in _extraEffectRows)
		{
			if (row == null)
			{
				continue;
			}

			UpdateExtraEffectCustomRows(row);

			SyncUnifiedTimingControlsFromTiming(
				GetSelectedTiming(row),
				row.TimingModeSelect,
				row.TimingBoundaryEdgeSelect,
				row.TimingBoundarySideSelect,
				row.TimingBoundaryOffsetSelect);
			UpdateExtraEffectTurnsEnabled(row);
		}
	}

	private void BuildCardSmithUi(VBoxContainer rightColumn)
	{
		if (rightColumn == null || _isUpgradeEditor)
		{
			return;
		}

		Label label = new Label { Text = CardEditorLoc.T("section.scalingEffects", "Scaling Effects") };
		StyleSectionLabel(label);
		rightColumn.AddChild(label);

		_cardSmithContainer = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_cardSmithContainer.AddThemeConstantOverride("separation", 8);

		Button add = new Button { Text = CardEditorLoc.T("button.addScalingEffect", "Add Scaling Effect"), CustomMinimumSize = _fieldMinSize, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		StyleInput(add);
		add.Pressed += () => AddCardSmithRow(effect: null);

		VBoxContainer section = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		section.AddThemeConstantOverride("separation", 8);
		section.AddChild(_cardSmithContainer);
		section.AddChild(add);

		MarginContainer margin = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		margin.AddThemeConstantOverride("margin_left", 24);
		margin.AddThemeConstantOverride("margin_top", 0);
		margin.AddThemeConstantOverride("margin_right", 0);
		margin.AddThemeConstantOverride("margin_bottom", 0);
		margin.AddChild(section);
		rightColumn.AddChild(margin);

		_cardSmithRows.Clear();

		CardOverride? stored = CardEditorOverrides.Get(_cardId);
		List<CardExtraEffect>? storedEffects = stored?.ExtraEffects;
		if (storedEffects != null && storedEffects.Count > 0)
		{
			foreach (CardExtraEffect effect in storedEffects)
			{
				if (effect != null
					&& effect.ScaleMode == CardExtraEffectScaleMode.PerHistoryCount
					&& CardEditorExtraEffects.IsValidEffectAmount(effect.Kind, effect.Amount))
				{
					AddCardSmithRow(effect);
				}
			}
		}

		if (_cardSmithRows.Count == 0)
		{
			AddCardSmithRow(effect: null);
		}
	}

	private void AddCardSmithRow(CardExtraEffect? effect)
	{
		if (_cardSmithContainer == null || !GodotObject.IsInstanceValid(_cardSmithContainer))
		{
			return;
		}

		effect = CardEditorExtraEffects.NormalizeLegacyOrbSlotEffect(effect);

		List<CardExtraEffectDefinition> allowedDefinitions = CardEditorExtraEffects.Definitions
			.Where(d => d.Kind is not CardExtraEffectKind.CreatedCardsCostLess
				and not CardExtraEffectKind.CreatedCardsUpgraded
				and not CardExtraEffectKind.GeneratedCardsUpgraded
				and not CardExtraEffectKind.CardsInPileUpgradedAura
				&& !CardEditorExtraEffects.IsLegacyOrbSlotKind(d.Kind))
			.ToList();

		VBoxContainer wrapper = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		wrapper.AddThemeConstantOverride("separation", 6);

		HBoxContainer rowTop = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		rowTop.AddThemeConstantOverride("separation", 10);

		OptionButton kindSelect = new OptionButton
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = _fieldMinSize
		};
		StyleInput(kindSelect);
		ConstrainOptionButtonPopup(kindSelect);
		foreach (CardExtraEffectDefinition def in allowedDefinitions)
		{
			kindSelect.AddItem(CardEditorLoc.Enum("effectKind", def.Kind, def.Label));
		}

		int kindIndex = 0;
		if (effect != null)
		{
			for (int i = 0; i < allowedDefinitions.Count; i++)
			{
				if (allowedDefinitions[i].Kind == effect.Kind)
				{
					kindIndex = i;
					break;
				}
			}
		}
		kindSelect.Select(kindIndex);

		OptionButton triggerSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(200, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
		};
		StyleInput(triggerSelect);
		ConstrainOptionButtonPopup(triggerSelect);
		bool initialAsPower = effect?.AsPower ?? false;
		foreach (CardExtraEffectTrigger trigger in Enum.GetValues<CardExtraEffectTrigger>())
		{
			if (IsHiddenUnifiedTurnBoundaryTrigger(trigger))
			{
				continue;
			}
			triggerSelect.AddItem(CardEditorExtraEffects.TriggerLabel(trigger, initialAsPower), (int)trigger);
		}
		CardExtraEffectTrigger desiredTrigger = effect != null ? GetVisibleExtraEffectTrigger(effect.Trigger) : CardExtraEffectTrigger.OnPlay;
		int desiredTriggerIndex = triggerSelect.GetItemIndex((int)desiredTrigger);
		if (desiredTriggerIndex < 0)
		{
			desiredTriggerIndex = 0;
		}
		triggerSelect.Select(desiredTriggerIndex);

		OptionButton targetSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(180, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
		};
		StyleInput(targetSelect);
		ConstrainOptionButtonPopup(targetSelect);

		HBoxContainer turnBoundaryRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		turnBoundaryRow.AddThemeConstantOverride("separation", 10);
		Label turnBoundaryLabel = new Label { Text = CardEditorLoc.T("turnBoundary.label", "Turn Boundary"), CustomMinimumSize = new Vector2(120, 0) };
		StyleBodyLabel(turnBoundaryLabel);

		OptionButton turnBoundaryEdgeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(140, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(turnBoundaryEdgeSelect);
		ConstrainOptionButtonPopup(turnBoundaryEdgeSelect);
		turnBoundaryEdgeSelect.TooltipText = CardEditorLoc.T("tooltip.turnBoundary.edge", "Start or end of the turn.");
		turnBoundaryEdgeSelect.AddItem(CardEditorLoc.T("turnBoundary.edge.start", "Start"));
		turnBoundaryEdgeSelect.AddItem(CardEditorLoc.T("turnBoundary.edge.end", "End"));

		OptionButton turnBoundarySideSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(170, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(turnBoundarySideSelect);
		ConstrainOptionButtonPopup(turnBoundarySideSelect);
		turnBoundarySideSelect.TooltipText = CardEditorLoc.T("tooltip.turnBoundary.side", "Whose turn boundary should trigger this effect.");
		turnBoundarySideSelect.AddItem(CardEditorLoc.T("turnBoundary.side.your", "Your Turn"));
		turnBoundarySideSelect.AddItem(CardEditorLoc.T("turnBoundary.side.enemy", "Enemy Turn"));
		turnBoundarySideSelect.AddItem(CardEditorLoc.T("turnBoundary.side.both", "Both"));

		OptionButton turnBoundaryLocationSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(170, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(turnBoundaryLocationSelect);
		ConstrainOptionButtonPopup(turnBoundaryLocationSelect);
		turnBoundaryLocationSelect.TooltipText = CardEditorLoc.T("tooltip.turnBoundary.location", "Optional gate: only trigger if this card is currently in the selected location.");
		turnBoundaryLocationSelect.AddItem(CardEditorLoc.T("turnBoundary.location.any", "Any Location"));
		turnBoundaryLocationSelect.AddItem(CardEditorLoc.T("turnBoundary.location.hand", "In Hand"));
		turnBoundaryLocationSelect.AddItem(CardEditorLoc.T("turnBoundary.location.draw", "In Draw Pile"));
		turnBoundaryLocationSelect.AddItem(CardEditorLoc.T("turnBoundary.location.discard", "In Discard Pile"));
		turnBoundaryLocationSelect.AddItem(CardEditorLoc.T("turnBoundary.location.exhaust", "In Exhaust Pile"));

		CardExtraEffectTurnBoundary initialEdge = effect?.Trigger switch
		{
			CardExtraEffectTrigger.StartOfTurn or CardExtraEffectTrigger.StartOfEnemyTurn => CardExtraEffectTurnBoundary.Start,
			CardExtraEffectTrigger.TurnBoundary => effect.TurnBoundary,
			_ => CardExtraEffectTurnBoundary.End
		};
		CardExtraEffectTurnBoundarySide initialSide = effect?.Trigger switch
		{
			CardExtraEffectTrigger.StartOfEnemyTurn or CardExtraEffectTrigger.EndOfEnemyTurn => CardExtraEffectTurnBoundarySide.EnemyTurn,
			CardExtraEffectTrigger.TurnBoundary => effect.TurnBoundarySide,
			_ => CardExtraEffectTurnBoundarySide.YourTurn
		};
		CardExtraEffectTurnBoundaryCardLocation initialLoc = effect?.Trigger switch
		{
			CardExtraEffectTrigger.EndOfTurnInHand => CardExtraEffectTurnBoundaryCardLocation.Hand,
			CardExtraEffectTrigger.TurnBoundary => effect.TurnBoundaryCardLocation,
			_ => CardExtraEffectTurnBoundaryCardLocation.Any
		};

		turnBoundaryEdgeSelect.Select(initialEdge == CardExtraEffectTurnBoundary.Start ? 0 : 1);
		turnBoundarySideSelect.Select(initialSide switch
		{
			CardExtraEffectTurnBoundarySide.EnemyTurn => 1,
			CardExtraEffectTurnBoundarySide.Both => 2,
			_ => 0
		});
		turnBoundaryLocationSelect.Select(initialLoc switch
		{
			CardExtraEffectTurnBoundaryCardLocation.Hand => 1,
			CardExtraEffectTurnBoundaryCardLocation.DrawPile => 2,
			CardExtraEffectTurnBoundaryCardLocation.DiscardPile => 3,
			CardExtraEffectTurnBoundaryCardLocation.ExhaustPile => 4,
			_ => 0
		});

		turnBoundaryRow.AddChild(turnBoundaryLabel);
		turnBoundaryRow.AddChild(turnBoundaryEdgeSelect);
		turnBoundaryRow.AddChild(turnBoundarySideSelect);
		turnBoundaryRow.AddChild(turnBoundaryLocationSelect);
		turnBoundaryRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		OptionButton durationSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(160, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
		};
		durationSelect.TooltipText = CardEditorLoc.T("tooltip.duration", "Duration (Permanent / This Turn)");
		StyleInput(durationSelect);
		ConstrainOptionButtonPopup(durationSelect);
		durationSelect.AddItem(CardEditorExtraEffects.DurationLabel(CardExtraEffectDuration.Permanent));
		durationSelect.AddItem(CardEditorExtraEffects.DurationLabel(CardExtraEffectDuration.ThisTurn));
		int durationIndex = effect != null ? (int)effect.Duration : 0;
		if (durationIndex < 0 || durationIndex > 1)
		{
			durationIndex = 0;
		}
		durationSelect.Select(durationIndex);

		OptionButton timingSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(timingSelect);
		ConstrainOptionButtonPopup(timingSelect);
		timingSelect.AddItem(CardEditorExtraEffects.TimingLabel(CardExtraEffectTiming.Immediate));
		timingSelect.AddItem(CardEditorExtraEffects.TimingLabel(CardExtraEffectTiming.StartOfTurn));
		timingSelect.AddItem(CardEditorExtraEffects.TimingLabel(CardExtraEffectTiming.EndOfTurn));
		timingSelect.AddItem(CardEditorExtraEffects.TimingLabel(CardExtraEffectTiming.EndOfThisTurn));
		timingSelect.AddItem(CardEditorExtraEffects.TimingLabel(CardExtraEffectTiming.StartOfEnemyTurn));
		timingSelect.AddItem(CardEditorExtraEffects.TimingLabel(CardExtraEffectTiming.EndOfEnemyTurn));
		timingSelect.AddItem(CardEditorExtraEffects.TimingLabel(CardExtraEffectTiming.StartOfAnyTurn));
		timingSelect.AddItem(CardEditorExtraEffects.TimingLabel(CardExtraEffectTiming.EndOfAnyTurn));
		timingSelect.AddItem(CardEditorExtraEffects.TimingLabel(CardExtraEffectTiming.EndOfThisAnyTurn));
		int timingIndex = effect != null ? (int)effect.Timing : 0;
		int timingCount = Enum.GetValues<CardExtraEffectTiming>().Length;
		if (timingIndex < 0 || timingIndex >= timingCount)
		{
			timingIndex = 0;
		}
		timingSelect.Select(timingIndex);

		int turns = effect?.Turns ?? 1;
		if (turns < 0)
		{
			turns = 1;
		}
		NMegaLineEdit turnsField = new NMegaLineEdit
		{
			Text = turns.ToString(CultureInfo.InvariantCulture),
			CustomMinimumSize = _amountFieldMinSize
		};
		turnsField.Alignment = HorizontalAlignment.Center;
		turnsField.TooltipText = "0 = this combat";
		StyleInput(turnsField);
		turnsField.TextChanged += _ => QueuePreviewUpdate();

		Control turnsSpin = CreateSpinButtons(turnsField, step: 1m, minValue: 0m, maxValue: 99m, isInteger: true);

		bool initialAmountIsX = effect?.AmountIsX ?? false;
		NMegaLineEdit amountField = new NMegaLineEdit
		{
			Text = (initialAmountIsX ? (effect?.AmountXPlus ?? 0) : (effect?.Amount ?? 0)).ToString(CultureInfo.InvariantCulture),
			CustomMinimumSize = _amountFieldMinSize
		};
		amountField.Alignment = HorizontalAlignment.Center;
		StyleInput(amountField);
		amountField.TextChanged += _ => QueuePreviewUpdate();

		decimal amountMin = _isUpgradeEditor ? -999m : 0m;
		Control spinButtons = CreateSpinButtons(amountField, step: 1m, minValue: amountMin, maxValue: 999m, isInteger: true);

		PackedScene amountTickboxScene = GD.Load<PackedScene>("res://scenes/ui/tickbox.tscn");
		Control amountTickboxVisuals = amountTickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
		Label amountTickLabel = new Label { Text = "X+" };
		StyleBodyLabel(amountTickLabel);
		KeywordTickbox amountXTickbox = new KeywordTickbox(amountTickboxVisuals, amountTickLabel, initialAmountIsX);
		amountXTickbox.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
		amountXTickbox.Toggled += () =>
		{
			ApplyEffectXPlusUiState(amountField, amountXTickbox, metaKeyPreviousNonXText: "card_editor_prev_effect_amount_nonx", metaKeyPreviousXPlusText: "card_editor_prev_effect_amount_xplus");
			QueuePreviewUpdate();
		};
		if (initialAmountIsX)
		{
			amountField.SetMeta("card_editor_prev_effect_amount_nonx", (effect?.Amount ?? 0).ToString(CultureInfo.InvariantCulture));
			amountField.SetMeta("card_editor_prev_effect_amount_xplus", (effect?.AmountXPlus ?? 0).ToString(CultureInfo.InvariantCulture));
		}
		else
		{
			// Seed the nonX meta so ApplyEffectXPlusUiState restores the correct amount instead of clearing the field.
			amountField.SetMeta("card_editor_prev_effect_amount_nonx", amountField.Text);
		}
		ApplyEffectXPlusUiState(amountField, amountXTickbox, metaKeyPreviousNonXText: "card_editor_prev_effect_amount_nonx", metaKeyPreviousXPlusText: "card_editor_prev_effect_amount_xplus");

		Button remove = new Button
		{
			Text = "\u2715",
			Flat = true,
			FocusMode = FocusModeEnum.None,
			CustomMinimumSize = new Vector2(44, _fieldMinSize.Y),
			MouseFilter = MouseFilterEnum.Stop
		};
		StyleInput(remove);
		remove.AddThemeColorOverride("font_color", StsColors.gold);

		HBoxContainer configRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		configRow.AddThemeConstantOverride("separation", 10);
		configRow.AddChild(triggerSelect);
		configRow.AddChild(targetSelect);
		configRow.AddChild(durationSelect);

		VBoxContainer moveCardsRow = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		moveCardsRow.AddThemeConstantOverride("separation", 6);
		moveCardsRow.Visible = false;

		HBoxContainer moveCardsRowTop = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		moveCardsRowTop.AddThemeConstantOverride("separation", 10);

		OptionButton moveFromPileSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(moveFromPileSelect);
		ConstrainOptionButtonPopup(moveFromPileSelect);
		moveFromPileSelect.TooltipText = CardEditorLoc.T("tooltip.moveFromPile", "Move from this pile");
		foreach (CardExtraEffectCardPile pile in Enum.GetValues<CardExtraEffectCardPile>())
		{
			moveFromPileSelect.AddItem(CardEditorExtraEffects.CardPileLabel(pile));
		}
		moveFromPileSelect.SetItemDisabled((int)CardExtraEffectCardPile.AllPiles, true);
		int moveFromIndex = effect != null ? (int)effect.CardSelectionPile : (int)CardExtraEffectCardPile.DiscardPile;
		if (moveFromIndex < 0 || moveFromIndex >= Enum.GetValues<CardExtraEffectCardPile>().Length)
		{
			moveFromIndex = (int)CardExtraEffectCardPile.DiscardPile;
		}
		moveFromPileSelect.Select(moveFromIndex);

		OptionButton moveSelectionModeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(200, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
		};
		StyleInput(moveSelectionModeSelect);
		ConstrainOptionButtonPopup(moveSelectionModeSelect);
		moveSelectionModeSelect.TooltipText = "Card selection mode";
		foreach (CardExtraEffectCardSelectionMode mode in Enum.GetValues<CardExtraEffectCardSelectionMode>())
		{
			moveSelectionModeSelect.AddItem(CardEditorExtraEffects.CardSelectionModeLabel(mode));
		}
		int moveModeIndex = effect != null ? (int)effect.CardSelectionMode : (int)CardExtraEffectCardSelectionMode.Choose;
		if (moveModeIndex < 0 || moveModeIndex >= Enum.GetValues<CardExtraEffectCardSelectionMode>().Length)
		{
			moveModeIndex = 0;
		}
		moveSelectionModeSelect.Select(moveModeIndex);

		moveCardsRowTop.AddChild(moveFromPileSelect);
		moveCardsRowTop.AddChild(moveSelectionModeSelect);
		moveCardsRowTop.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer moveCardsRowBottom = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		moveCardsRowBottom.AddThemeConstantOverride("separation", 10);

		OptionButton moveToPileSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(moveToPileSelect);
		ConstrainOptionButtonPopup(moveToPileSelect);
		moveToPileSelect.TooltipText = "Move to this pile";
		foreach (CardExtraEffectCardPile pile in Enum.GetValues<CardExtraEffectCardPile>())
		{
			moveToPileSelect.AddItem(CardEditorExtraEffects.CardPileLabel(pile));
		}
		moveToPileSelect.SetItemDisabled((int)CardExtraEffectCardPile.AllPiles, true);
		int moveToIndex = effect != null ? (int)effect.MoveToPile : (int)CardExtraEffectCardPile.DrawPile;
		if (moveToIndex < 0 || moveToIndex >= Enum.GetValues<CardExtraEffectCardPile>().Length)
		{
			moveToIndex = (int)CardExtraEffectCardPile.DrawPile;
		}
		moveToPileSelect.Select(moveToIndex);

		OptionButton moveToPositionSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(200, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
		};
		StyleInput(moveToPositionSelect);
		ConstrainOptionButtonPopup(moveToPositionSelect);
		moveToPositionSelect.TooltipText = "Draw pile position";
		foreach (CardExtraEffectCardPilePosition position in Enum.GetValues<CardExtraEffectCardPilePosition>())
		{
			moveToPositionSelect.AddItem(CardEditorExtraEffects.CardPilePositionLabel(position));
		}
		int movePosIndex = effect != null ? (int)effect.MoveToPosition : (int)CardExtraEffectCardPilePosition.Top;
		if (movePosIndex < 0 || movePosIndex >= Enum.GetValues<CardExtraEffectCardPilePosition>().Length)
		{
			movePosIndex = 0;
		}
		moveToPositionSelect.Select(movePosIndex);

		moveCardsRowBottom.AddChild(moveToPileSelect);
		moveCardsRowBottom.AddChild(moveToPositionSelect);
		moveCardsRowBottom.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		moveCardsRow.AddChild(moveCardsRowTop);
		moveCardsRow.AddChild(moveCardsRowBottom);

		VBoxContainer grantRow = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		grantRow.AddThemeConstantOverride("separation", 6);
		grantRow.Visible = false;

		HBoxContainer grantSelectRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		grantSelectRow.AddThemeConstantOverride("separation", 10);

		OptionButton grantPileSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(grantPileSelect);
		ConstrainOptionButtonPopup(grantPileSelect);
		grantPileSelect.TooltipText = "Grant to a card from this pile";
		foreach (CardExtraEffectCardPile pile in Enum.GetValues<CardExtraEffectCardPile>())
		{
			grantPileSelect.AddItem(CardEditorExtraEffects.CardPileLabel(pile));
		}
		int grantPileIndex = effect != null ? (int)effect.CardSelectionPile : (int)CardExtraEffectCardPile.Hand;
		if (grantPileIndex < 0 || grantPileIndex >= Enum.GetValues<CardExtraEffectCardPile>().Length)
		{
			grantPileIndex = 0;
		}
		grantPileSelect.Select(grantPileIndex);

		OptionButton grantModeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(200, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
		};
		StyleInput(grantModeSelect);
		ConstrainOptionButtonPopup(grantModeSelect);
		grantModeSelect.TooltipText = "Card selection mode";
		foreach (CardExtraEffectCardSelectionMode mode in Enum.GetValues<CardExtraEffectCardSelectionMode>())
		{
			grantModeSelect.AddItem(CardEditorExtraEffects.CardSelectionModeLabel(mode));
		}
		int grantModeIndex = effect != null ? (int)effect.CardSelectionMode : (int)CardExtraEffectCardSelectionMode.Choose;
		if (grantModeIndex < 0 || grantModeIndex >= Enum.GetValues<CardExtraEffectCardSelectionMode>().Length)
		{
			grantModeIndex = 0;
		}
		grantModeSelect.Select(grantModeIndex);

		grantSelectRow.AddChild(grantPileSelect);
		grantSelectRow.AddChild(grantModeSelect);
		grantSelectRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer grantCountRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		grantCountRow.AddThemeConstantOverride("separation", 10);

		Label grantCountLabel = new Label { Text = CardEditorLoc.T("ui.count", "Count"), CustomMinimumSize = new Vector2(120, 0) };
		StyleBodyLabel(grantCountLabel);

		PackedScene grantCountTickboxScene = GD.Load<PackedScene>("res://scenes/ui/tickbox.tscn");
		Control grantCountXVisuals = grantCountTickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
		Label grantCountXLabel = new Label { Text = "X" };
		StyleBodyLabel(grantCountXLabel);
		KeywordTickbox grantCountXTickbox = new KeywordTickbox(grantCountXVisuals, grantCountXLabel, effect?.CardSelectionCountIsX ?? false);
		grantCountXTickbox.TooltipText = CardEditorLoc.T("tooltip.grantCountX", "Use X as the number of selected cards (based on Energy/Stars spent).");

		int grantCount = effect?.CardSelectionCount ?? 1;
		if (grantCount < 0)
		{
			grantCount = 0;
		}
		NMegaLineEdit grantCountField = new NMegaLineEdit
		{
			Text = grantCount.ToString(CultureInfo.InvariantCulture),
			CustomMinimumSize = _amountFieldMinSize
		};
		grantCountField.Alignment = HorizontalAlignment.Center;
		grantCountField.TooltipText = CardEditorLoc.T("tooltip.grantCount", "How many cards to select (0 = none).");
		StyleInput(grantCountField);
		grantCountField.TextChanged += _ => QueuePreviewUpdate();

		Control grantCountSpin = CreateSpinButtons(grantCountField, step: 1m, minValue: 0m, maxValue: 99m, isInteger: true);

		grantCountRow.AddChild(grantCountLabel);
		grantCountRow.AddChild(grantCountXTickbox);
		grantCountRow.AddChild(grantCountSpin);
		grantCountRow.AddChild(grantCountField);
		grantCountRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer grantFilterRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		grantFilterRow.AddThemeConstantOverride("separation", 10);

		OptionButton grantCountPoolSelect = CreateGeneratedPoolSelect(
			effect?.CardSelectionPool ?? CardGeneratedCardPool.All,
			CardGeneratedCardPool.All,
			CardEditorLoc.T("tooltip.grantPool", "Only select cards from this pool (color/class)."));

		OptionButton grantCountTypeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(grantCountTypeSelect);
		ConstrainOptionButtonPopup(grantCountTypeSelect);
		grantCountTypeSelect.TooltipText = CardEditorLoc.T("tooltip.grantType", "Only select cards of this card type.");
		foreach (CardGeneratedCardType type in Enum.GetValues<CardGeneratedCardType>())
		{
			grantCountTypeSelect.AddItem(CardEditorExtraEffects.GeneratedCardTypeLabel(type));
		}
		int grantTypeIndex = effect != null ? (int)effect.CardSelectionType : (int)CardGeneratedCardType.Any;
		if (grantTypeIndex < 0 || grantTypeIndex >= Enum.GetValues<CardGeneratedCardType>().Length)
		{
			grantTypeIndex = (int)CardGeneratedCardType.Any;
		}
		grantCountTypeSelect.Select(grantTypeIndex);

		OptionButton grantCountFilterSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(grantCountFilterSelect);
		ConstrainOptionButtonPopup(grantCountFilterSelect);
		grantCountFilterSelect.TooltipText = CardEditorLoc.T("tooltip.grantFilter", "Only select cards that match this extra-effect filter.");
		foreach (CardExtraEffectCountCardFilter filter in Enum.GetValues<CardExtraEffectCountCardFilter>())
		{
			grantCountFilterSelect.AddItem(CardEditorExtraEffects.CountCardFilterLabel(filter));
		}
		int grantFilterIndex = effect != null ? (int)effect.CardSelectionFilter : (int)CardExtraEffectCountCardFilter.Any;
		if (grantFilterIndex < 0 || grantFilterIndex >= Enum.GetValues<CardExtraEffectCountCardFilter>().Length)
		{
			grantFilterIndex = (int)CardExtraEffectCountCardFilter.Any;
		}
		grantCountFilterSelect.Select(grantFilterIndex);

		grantCountPoolSelect.ItemSelected += _ => QueuePreviewUpdate();
		grantCountTypeSelect.ItemSelected += _ => QueuePreviewUpdate();
		grantCountFilterSelect.ItemSelected += _ => QueuePreviewUpdate();

		grantFilterRow.AddChild(grantCountPoolSelect);
		grantFilterRow.AddChild(grantCountTypeSelect);
		grantFilterRow.AddChild(grantCountFilterSelect);
		grantFilterRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer grantDurationOuterRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		grantDurationOuterRow.AddThemeConstantOverride("separation", 10);

		(OptionButton grantDurationSelect, NMegaLineEdit grantTurnsField, Control grantTurnsSpin) = CreateExtraEffectDurationControls(
			effect?.CardGrantDuration ?? CardExtraEffectCardGrantDuration.ThisTurn,
			CardExtraEffectCardGrantDuration.ThisTurn,
			effect?.CardGrantTurns ?? 2,
			minTurns: 1,
			allowNegativeTurns: false,
			durationTooltipText: "How long the granted effect lasts",
			turnsTooltipText: "How many turns the grant lasts (when Duration = X Turns)",
			CardEditorExtraEffects.CardGrantDurationLabel);

		HBoxContainer grantTurnsRow = new HBoxContainer();
		grantTurnsRow.AddThemeConstantOverride("separation", 10);
		grantTurnsRow.AddChild(grantTurnsSpin);
		grantTurnsRow.AddChild(grantTurnsField);

		grantDurationOuterRow.AddChild(grantDurationSelect);
		grantDurationOuterRow.AddChild(grantTurnsRow);
		grantDurationOuterRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		grantRow.AddChild(grantSelectRow);
		grantRow.AddChild(grantCountRow);
		grantRow.AddChild(grantFilterRow);
		grantRow.AddChild(grantDurationOuterRow);

		HBoxContainer timingRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		timingRow.AddThemeConstantOverride("separation", 10);
		Label timingLabel = new Label { Text = CardEditorLoc.T("timing.label", "Timing"), CustomMinimumSize = new Vector2(120, 0) };
		timingLabel.TooltipText = CardEditorLoc.T("tooltip.timing", "When this effect resolves after the trigger.");
		StyleBodyLabel(timingLabel);
		timingRow.AddChild(timingLabel);

		OptionButton timingModeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(140, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(timingModeSelect);
		ConstrainOptionButtonPopup(timingModeSelect);
		timingModeSelect.TooltipText = CardEditorLoc.T("tooltip.timingMode", "Resolve now, or at a start/end of turn boundary.");
		timingModeSelect.AddItem(CardEditorExtraEffects.TimingLabel(CardExtraEffectTiming.Immediate));
		timingModeSelect.AddItem(CardEditorLoc.T("timing.mode.turnBoundary", "Turn Boundary"));

		OptionButton timingEdgeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(timingEdgeSelect);
		ConstrainOptionButtonPopup(timingEdgeSelect);
		timingEdgeSelect.TooltipText = CardEditorLoc.T("tooltip.timingEdge", "Start or end of the turn.");
		timingEdgeSelect.AddItem(CardEditorLoc.T("turnBoundary.edge.start", "Start"));
		timingEdgeSelect.AddItem(CardEditorLoc.T("turnBoundary.edge.end", "End"));

		OptionButton timingSideSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(150, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(timingSideSelect);
		ConstrainOptionButtonPopup(timingSideSelect);
		timingSideSelect.TooltipText = CardEditorLoc.T("tooltip.timingSide", "Resolve on your turn, the enemy turn, or both.");
		timingSideSelect.AddItem(CardEditorLoc.T("turnBoundary.side.your", "Your Turn"));
		timingSideSelect.AddItem(CardEditorLoc.T("turnBoundary.side.enemy", "Enemy Turn"));
		timingSideSelect.AddItem(CardEditorLoc.T("turnBoundary.side.both", "Both"));

		OptionButton timingOffsetSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(130, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(timingOffsetSelect);
		ConstrainOptionButtonPopup(timingOffsetSelect);
		timingOffsetSelect.TooltipText = CardEditorLoc.T("tooltip.timingOffset", "Whether to include this turn (end edge only).");
		timingOffsetSelect.AddItem(CardEditorLoc.T("timing.offset.thisTurn", "This Turn"));
		timingOffsetSelect.AddItem(CardEditorLoc.T("timing.offset.nextTurn", "Next Turn"));

		CardExtraEffectTiming initialTimingForUi = timingIndex < 0 || timingIndex >= Enum.GetValues<CardExtraEffectTiming>().Length
			? CardExtraEffectTiming.Immediate
			: (CardExtraEffectTiming)timingIndex;
		bool initialIsNow = initialTimingForUi == CardExtraEffectTiming.Immediate;
		timingModeSelect.Select(initialIsNow ? 0 : 1);
		bool initialBoth = initialTimingForUi is CardExtraEffectTiming.StartOfAnyTurn or CardExtraEffectTiming.EndOfAnyTurn or CardExtraEffectTiming.EndOfThisAnyTurn;
		bool initialEnemy = initialTimingForUi is CardExtraEffectTiming.StartOfEnemyTurn or CardExtraEffectTiming.EndOfEnemyTurn;
		bool initialStart = initialTimingForUi is CardExtraEffectTiming.StartOfTurn or CardExtraEffectTiming.StartOfEnemyTurn or CardExtraEffectTiming.StartOfAnyTurn;
		bool initialThisTurn = initialTimingForUi is CardExtraEffectTiming.EndOfThisTurn or CardExtraEffectTiming.EndOfThisAnyTurn;
		timingEdgeSelect.Select(initialStart ? 0 : 1);
		timingSideSelect.Select(initialEnemy ? 1 : (initialBoth ? 2 : 0));
		timingOffsetSelect.Select(initialThisTurn ? 0 : 1);
		timingEdgeSelect.Visible = !initialIsNow;
		timingSideSelect.Visible = !initialIsNow;
		timingOffsetSelect.Visible = !initialIsNow && timingEdgeSelect.Selected == 1 && timingSideSelect.Selected != 1;

		timingRow.AddChild(timingModeSelect);
		timingRow.AddChild(timingEdgeSelect);
		timingRow.AddChild(timingSideSelect);
		timingRow.AddChild(timingOffsetSelect);
		timingRow.AddChild(turnsSpin);
		timingRow.AddChild(turnsField);
		timingRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer countRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		countRow.AddThemeConstantOverride("separation", 10);

		OptionButton countEventSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(200, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(countEventSelect);
		ConstrainOptionButtonPopup(countEventSelect);
		for (int eventIndex = 0; eventIndex < CardEditorExtraEffects.CardSmithCountEvents.Count; eventIndex++)
		{
			countEventSelect.AddItem(CardEditorExtraEffects.CountEventLabel(CardEditorExtraEffects.CardSmithCountEvents[eventIndex]));
		}
		int countEventIndex = 0;
		if (effect != null)
		{
			for (int eventIndex = 0; eventIndex < CardEditorExtraEffects.CardSmithCountEvents.Count; eventIndex++)
			{
				if (CardEditorExtraEffects.CardSmithCountEvents[eventIndex] == effect.CountEvent)
				{
					countEventIndex = eventIndex;
					break;
				}
			}
		}
		if (countEventIndex < 0 || countEventIndex >= CardEditorExtraEffects.CardSmithCountEvents.Count)
		{
			countEventIndex = 0;
		}
		countEventSelect.Select(countEventIndex);

		OptionButton countWindowSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(countWindowSelect);
		ConstrainOptionButtonPopup(countWindowSelect);
		foreach (CardExtraEffectCountWindow window in Enum.GetValues<CardExtraEffectCountWindow>())
		{
			countWindowSelect.AddItem(CardEditorExtraEffects.CountWindowLabel(window));
		}
		int countWindowIndex = effect != null ? (int)effect.CountWindow : (int)CardExtraEffectCountWindow.ThisCombat;
		if (countWindowIndex < 0 || countWindowIndex >= Enum.GetValues<CardExtraEffectCountWindow>().Length)
		{
			countWindowIndex = 0;
		}
		countWindowSelect.Select(countWindowIndex);

		int countTurns = effect?.CountTurns ?? 2;
		if (countTurns <= 0)
		{
			countTurns = 1;
		}
		NMegaLineEdit countTurnsField = new NMegaLineEdit
		{
			Text = countTurns.ToString(CultureInfo.InvariantCulture),
			CustomMinimumSize = _amountFieldMinSize
		};
		countTurnsField.Alignment = HorizontalAlignment.Center;
		StyleInput(countTurnsField);
		countTurnsField.TextChanged += _ => QueuePreviewUpdate();
		Control countTurnsSpin = CreateSpinButtons(countTurnsField, step: 1m, minValue: 1m, maxValue: 99m, isInteger: true);

		HBoxContainer countTurnsRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		countTurnsRow.AddThemeConstantOverride("separation", 10);
		Label countTurnsLabel = new Label { Text = "Last Turns", CustomMinimumSize = new Vector2(120, 0) };
		StyleBodyLabel(countTurnsLabel);
		countTurnsRow.AddChild(countTurnsLabel);
		countTurnsRow.AddChild(countTurnsSpin);
		countTurnsRow.AddChild(countTurnsField);
		countTurnsRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer countWindowInclusionRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		countWindowInclusionRow.AddThemeConstantOverride("separation", 10);
		countWindowInclusionRow.Visible = false;
		Label countWindowInclusionLabel = new Label
		{
			Text = CardEditorLoc.T("ui.turnWindowMode", "Turn Window"),
			CustomMinimumSize = new Vector2(120, 0)
		};
		StyleBodyLabel(countWindowInclusionLabel);

		OptionButton countWindowInclusionSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(240, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(countWindowInclusionSelect);
		ConstrainOptionButtonPopup(countWindowInclusionSelect);
		countWindowInclusionSelect.AddItem(CardEditorLoc.T("ui.turnWindow.includingThisTurn", "Including this turn"), (int)CardExtraEffectCountWindowInclusion.IncludeThisTurn);
		countWindowInclusionSelect.AddItem(CardEditorLoc.T("ui.turnWindow.previousOnly", "Previous turns only"), (int)CardExtraEffectCountWindowInclusion.ExcludeThisTurn);
		int initialInclusion = effect != null && effect.CountWindowInclusion == CardExtraEffectCountWindowInclusion.ExcludeThisTurn
			? (int)CardExtraEffectCountWindowInclusion.ExcludeThisTurn
			: (int)CardExtraEffectCountWindowInclusion.IncludeThisTurn;
		countWindowInclusionSelect.Select(initialInclusion == (int)CardExtraEffectCountWindowInclusion.ExcludeThisTurn ? 1 : 0);
		countWindowInclusionSelect.TooltipText = "Controls whether 'Last X Turns' includes this current turn or only previous turns.";
		countWindowInclusionSelect.ItemSelected += _ => QueuePreviewUpdate();

		countWindowInclusionRow.AddChild(countWindowInclusionLabel);
		countWindowInclusionRow.AddChild(countWindowInclusionSelect);
		countWindowInclusionRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer blockLostCountingModeRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		blockLostCountingModeRow.AddThemeConstantOverride("separation", 10);
		blockLostCountingModeRow.Visible = false;
		Label blockLostCountingModeLabel = new Label
		{
			Text = CardEditorLoc.T("ui.blockLostCountingMode", "Block Loss"),
			CustomMinimumSize = new Vector2(120, 0)
		};
		StyleBodyLabel(blockLostCountingModeLabel);

		OptionButton blockLostCountingModeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(240, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(blockLostCountingModeSelect);
		ConstrainOptionButtonPopup(blockLostCountingModeSelect);
		blockLostCountingModeSelect.AddItem(
			CardEditorLoc.T("ui.blockLostCountingMode.damageAndEffects", "Damage / effects"),
			(int)CardExtraEffectBlockLostCountingMode.DamageAndEffects);
		blockLostCountingModeSelect.AddItem(
			CardEditorLoc.T("ui.blockLostCountingMode.includeBetweenTurns", "Including between turns"),
			(int)CardExtraEffectBlockLostCountingMode.IncludeBetweenTurns);
		int initialBlockLostMode = effect != null && effect.BlockLostCountingMode == CardExtraEffectBlockLostCountingMode.IncludeBetweenTurns
			? (int)CardExtraEffectBlockLostCountingMode.IncludeBetweenTurns
			: (int)CardExtraEffectBlockLostCountingMode.DamageAndEffects;
		blockLostCountingModeSelect.Select(initialBlockLostMode == (int)CardExtraEffectBlockLostCountingMode.IncludeBetweenTurns ? 1 : 0);
		blockLostCountingModeSelect.TooltipText = "Controls whether 'Block Lost' includes block cleared between turns.";
		blockLostCountingModeSelect.ItemSelected += _ => QueuePreviewUpdate();

		blockLostCountingModeRow.AddChild(blockLostCountingModeLabel);
		blockLostCountingModeRow.AddChild(blockLostCountingModeSelect);
		blockLostCountingModeRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		countRow.AddChild(countEventSelect);
		countRow.AddChild(countWindowSelect);
		countRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer filterRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		filterRow.AddThemeConstantOverride("separation", 10);

		OptionButton countPoolSelect = CreateGeneratedPoolSelect(
			effect?.CountCardPool ?? CardGeneratedCardPool.All,
			CardGeneratedCardPool.All,
			string.Empty);

		OptionButton countTypeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(countTypeSelect);
		ConstrainOptionButtonPopup(countTypeSelect);
		foreach (CardGeneratedCardType type in Enum.GetValues<CardGeneratedCardType>())
		{
			countTypeSelect.AddItem(CardEditorExtraEffects.GeneratedCardTypeLabel(type));
		}
		int countTypeIndex = effect != null ? (int)effect.CountCardType : 0;
		if (countTypeIndex < 0 || countTypeIndex >= Enum.GetValues<CardGeneratedCardType>().Length)
		{
			countTypeIndex = 0;
		}
		countTypeSelect.Select(countTypeIndex);

		PackedScene tickboxScene = GD.Load<PackedScene>("res://scenes/ui/tickbox.tscn");
		Control tickboxVisuals = tickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
		Label blockLabel = new Label { Text = "Block Only" };
		StyleBodyLabel(blockLabel);
		KeywordTickbox blockTickbox = new KeywordTickbox(tickboxVisuals, blockLabel, effect?.CountOnlyBlockCards ?? false);
		blockTickbox.Toggled += QueuePreviewUpdate;

		filterRow.AddChild(countPoolSelect);
		filterRow.AddChild(countTypeSelect);
		filterRow.AddChild(blockTickbox);
		filterRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer generatedCardRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		generatedCardRow.AddThemeConstantOverride("separation", 10);

		OptionButton generatedPoolSelect = CreateGeneratedPoolSelect(
			effect?.GeneratedCardPool ?? CardGeneratedCardPool.Default,
			CardGeneratedCardPool.Default,
			"Card pool (class) to generate from");

		OptionButton generatedTypeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(generatedTypeSelect);
		ConstrainOptionButtonPopup(generatedTypeSelect);
		generatedTypeSelect.TooltipText = "Card type to generate";
		foreach (CardGeneratedCardType type in Enum.GetValues<CardGeneratedCardType>())
		{
			generatedTypeSelect.AddItem(CardEditorExtraEffects.GeneratedCardTypeLabel(type));
		}
		int generatedTypeIndex = effect != null ? (int)effect.GeneratedCardType : 0;
		if (generatedTypeIndex < 0 || generatedTypeIndex >= Enum.GetValues<CardGeneratedCardType>().Length)
		{
			generatedTypeIndex = 0;
		}
		generatedTypeSelect.Select(generatedTypeIndex);

		generatedCardRow.AddChild(generatedPoolSelect);
		generatedCardRow.AddChild(generatedTypeSelect);
		generatedCardRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		CardSmithRow row = new CardSmithRow
		{
			Container = wrapper,
			KindSelect = kindSelect,
			TriggerSelect = triggerSelect,
			TurnBoundaryRow = turnBoundaryRow,
			TurnBoundaryEdgeSelect = turnBoundaryEdgeSelect,
			TurnBoundarySideSelect = turnBoundarySideSelect,
			TurnBoundaryLocationSelect = turnBoundaryLocationSelect,
			TargetSelect = targetSelect,
			DurationSelect = durationSelect,
			TimingSelect = timingSelect,
			TimingRow = timingRow,
			TimingModeSelect = timingModeSelect,
			TimingBoundaryEdgeSelect = timingEdgeSelect,
			TimingBoundarySideSelect = timingSideSelect,
			TimingBoundaryOffsetSelect = timingOffsetSelect,
			TurnsField = turnsField,
			AmountField = amountField,
			CountEventSelect = countEventSelect,
			CountWindowSelect = countWindowSelect,
			CountTurnsRow = countTurnsRow,
			CountTurnsField = countTurnsField,
			CountWindowInclusionRow = countWindowInclusionRow,
			CountWindowInclusionSelect = countWindowInclusionSelect,
			BlockLostCountingModeRow = blockLostCountingModeRow,
			BlockLostCountingModeSelect = blockLostCountingModeSelect,
			CountPoolSelect = countPoolSelect,
			CountTypeSelect = countTypeSelect,
			GeneratedCardRow = generatedCardRow,
			GeneratedPoolSelect = generatedPoolSelect,
			GeneratedTypeSelect = generatedTypeSelect,
			BlockOnlyTickbox = blockTickbox
		};
		_cardSmithRows.Add(row);

		kindSelect.ItemSelected += _ =>
		{
			ConfigureCardSmithTargets(row, desiredTarget: null, allowedDefinitions);
			UpdateCardSmithDurationEnabled(row, desiredDuration: null, allowedDefinitions);
			UpdateCardSmithCustomRows(row, allowedDefinitions);
			// Reset amount to a valid default if the current value is invalid for the new Kind.
			int ki = row.KindSelect.Selected;
			if (ki >= 0 && ki < allowedDefinitions.Count)
			{
				CardExtraEffectDefinition newDef = allowedDefinitions[ki];
				if (int.TryParse(row.AmountField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int curAmt))
				{
					if (!CardEditorExtraEffects.IsValidEffectAmount(newDef.Kind, curAmt))
					{
						row.AmountField.Text = newDef.DefaultAmount.ToString(CultureInfo.InvariantCulture);
					}
				}
				else
				{
					row.AmountField.Text = newDef.DefaultAmount.ToString(CultureInfo.InvariantCulture);
				}
			}
			QueuePreviewUpdate();
		};
		triggerSelect.ItemSelected += _ =>
		{
			ConfigureCardSmithTargets(row, desiredTarget: null, allowedDefinitions);
			UpdateCardSmithCustomRows(row, allowedDefinitions);
			UpdateCardSmithTimingRowVisibility(row);
			QueuePreviewUpdate();
		};
		targetSelect.ItemSelected += _ => QueuePreviewUpdate();
		durationSelect.ItemSelected += _ => QueuePreviewUpdate();
		timingSelect.ItemSelected += _ =>
		{
			UpdateCardSmithTurnsEnabled(row);
			QueuePreviewUpdate();
		};
		Action updateTimingFromUnifiedControls = () =>
		{
			CardExtraEffectTiming timing = GetTimingFromUnifiedTimingControls(timingModeSelect, timingEdgeSelect, timingSideSelect, timingOffsetSelect);
			timingSelect.Select((int)timing);
			UpdateUnifiedTimingControlsVisibility(timingModeSelect, timingEdgeSelect, timingSideSelect, timingOffsetSelect);
			UpdateCardSmithTurnsEnabled(row);
			QueuePreviewUpdate();
		};
		timingModeSelect.ItemSelected += _ => updateTimingFromUnifiedControls();
		timingEdgeSelect.ItemSelected += _ => updateTimingFromUnifiedControls();
		timingSideSelect.ItemSelected += _ => updateTimingFromUnifiedControls();
		timingOffsetSelect.ItemSelected += _ => updateTimingFromUnifiedControls();
		countWindowSelect.ItemSelected += _ =>
		{
			UpdateCardSmithCountTurnsEnabled(row);
			QueuePreviewUpdate();
		};
		countEventSelect.ItemSelected += _ =>
		{
			UpdateCardSmithCountTurnsEnabled(row);
			QueuePreviewUpdate();
		};
		countPoolSelect.ItemSelected += _ => QueuePreviewUpdate();
		countTypeSelect.ItemSelected += _ => QueuePreviewUpdate();
		turnBoundaryEdgeSelect.ItemSelected += _ => QueuePreviewUpdate();
		turnBoundarySideSelect.ItemSelected += _ => QueuePreviewUpdate();
		turnBoundaryLocationSelect.ItemSelected += _ => QueuePreviewUpdate();
		generatedPoolSelect.ItemSelected += _ => QueuePreviewUpdate();
		generatedTypeSelect.ItemSelected += _ => QueuePreviewUpdate();
		remove.Pressed += () =>
		{
			if (_cardSmithContainer != null && GodotObject.IsInstanceValid(_cardSmithContainer))
			{
				_cardSmithContainer.RemoveChild(wrapper);
			}
			_cardSmithRows.Remove(row);
			wrapper.QueueFreeSafely();
			QueuePreviewUpdate();
		};

		rowTop.AddChild(kindSelect);
		rowTop.AddChild(amountXTickbox);
		rowTop.AddChild(spinButtons);
		rowTop.AddChild(amountField);
		rowTop.AddChild(remove);

		wrapper.AddChild(rowTop);
		wrapper.AddChild(configRow);
		wrapper.AddChild(turnBoundaryRow);
		wrapper.AddChild(countRow);
		wrapper.AddChild(countTurnsRow);
		wrapper.AddChild(countWindowInclusionRow);
		wrapper.AddChild(blockLostCountingModeRow);
		wrapper.AddChild(filterRow);
		wrapper.AddChild(generatedCardRow);
		wrapper.AddChild(timingRow);

		_cardSmithContainer.AddChild(wrapper);
		ConfigureCardSmithTargets(row, effect?.Target, allowedDefinitions);
		UpdateCardSmithDurationEnabled(row, effect?.Duration, allowedDefinitions);
		UpdateCardSmithCustomRows(row, allowedDefinitions);
		UpdateCardSmithTimingRowVisibility(row);
		UpdateCardSmithCountTurnsEnabled(row);
	}

	private void UpdateCardSmithTimingRowVisibility(CardSmithRow row)
	{
		bool isOnPlay = GetSelectedTrigger(row) == CardExtraEffectTrigger.OnPlay;
		row.TimingRow.Visible = isOnPlay;

		if (!isOnPlay)
		{
			row.TimingSelect.Select((int)CardExtraEffectTiming.Immediate);
			row.TurnsField.Text = "1";
		}

		SyncUnifiedTimingControlsFromTiming(
			GetSelectedTiming(row),
			row.TimingModeSelect,
			row.TimingBoundaryEdgeSelect,
			row.TimingBoundarySideSelect,
			row.TimingBoundaryOffsetSelect);
		UpdateCardSmithTurnsEnabled(row);
	}

	private void UpdateCardSmithTurnsEnabled(CardSmithRow row)
	{
		CardExtraEffectTiming timing = GetSelectedTiming(row);
		bool enableTurns = timing != CardExtraEffectTiming.Immediate;
		row.TurnsField.Editable = enableTurns;
		row.TurnsField.SelfModulate = enableTurns ? Colors.White : StsColors.gray;
		SetSpinEnabled(row.TurnsField, enableTurns);
	}

	private void UpdateCardSmithCountTurnsEnabled(CardSmithRow row)
	{
		CardExtraEffectCountEvent ev = GetSelectedCountEvent(row);
		CardExtraEffectCountWindow window = GetSelectedCountWindow(row);
		row.CountTurnsRow.Visible = window == CardExtraEffectCountWindow.LastTurns;
		row.CountWindowInclusionRow.Visible = row.CountTurnsRow.Visible;
		row.BlockLostCountingModeRow.Visible = ev == CardExtraEffectCountEvent.BlockLost;
	}

	private void UpdateCardSmithCustomRows(CardSmithRow row, IReadOnlyList<CardExtraEffectDefinition> defs)
	{
		row.TurnBoundaryRow.Visible = GetSelectedTrigger(row) == CardExtraEffectTrigger.TurnBoundary;

		int kindIndex = row.KindSelect.Selected;
		if (kindIndex < 0 || kindIndex >= defs.Count)
		{
			kindIndex = 0;
		}

		CardExtraEffectKind kind = defs[kindIndex].Kind;
		bool isGeneratedCard = kind is CardExtraEffectKind.AddRandomCardToHand or CardExtraEffectKind.ChooseOneOfThreeCardsToHand;
		row.GeneratedCardRow.Visible = isGeneratedCard;
	}

	private void UpdateCardSmithDurationEnabled(CardSmithRow row, CardExtraEffectDuration? desiredDuration, IReadOnlyList<CardExtraEffectDefinition> defs)
	{
		int kindIndex = row.KindSelect.Selected;
		if (kindIndex < 0 || kindIndex >= defs.Count)
		{
			kindIndex = 0;
		}
		CardExtraEffectDefinition def = defs[kindIndex];
		bool supported = CardEditorExtraEffects.SupportsDuration(def.Kind);

		if (!supported)
		{
			row.DurationSelect.Select((int)CardExtraEffectDuration.Permanent);
		}
		else if (desiredDuration.HasValue)
		{
			int index = (int)desiredDuration.Value;
			if (index < 0 || index > 1)
			{
				index = 0;
			}
			row.DurationSelect.Select(index);
		}

		row.DurationSelect.Disabled = !supported;
		row.DurationSelect.SelfModulate = supported ? Colors.White : StsColors.gray;
	}

	private void ConfigureCardSmithTargets(CardSmithRow row, CardExtraEffectTarget? desiredTarget, IReadOnlyList<CardExtraEffectDefinition> defs)
	{
		int kindIndex = row.KindSelect.Selected;
		if (kindIndex < 0 || kindIndex >= defs.Count)
		{
			kindIndex = 0;
		}
		CardExtraEffectDefinition def = defs[kindIndex];

		row.AllowedTargets.Clear();
		IReadOnlyList<CardExtraEffectTarget> allowed = def.AllowedTargets;
		if (GetSelectedTrigger(row) != CardExtraEffectTrigger.OnPlay)
		{
			allowed = allowed.Where(t => t != CardExtraEffectTarget.Target).ToArray();
		}
		row.AllowedTargets.AddRange(allowed);

		row.TargetSelect.Clear();
		foreach (CardExtraEffectTarget target in row.AllowedTargets)
		{
			row.TargetSelect.AddItem(CardEditorExtraEffects.TargetLabel(target));
		}

		CardExtraEffectTarget wanted = desiredTarget ?? def.DefaultTarget;
		if (GetSelectedTrigger(row) != CardExtraEffectTrigger.OnPlay && wanted == CardExtraEffectTarget.Target)
		{
			wanted = row.AllowedTargets.Contains(CardExtraEffectTarget.RandomEnemy) ? CardExtraEffectTarget.RandomEnemy : CardExtraEffectTarget.Self;
		}
		int selectIndex = 0;
		for (int i = 0; i < row.AllowedTargets.Count; i++)
		{
			if (row.AllowedTargets[i] == wanted)
			{
				selectIndex = i;
				break;
			}
		}
		row.TargetSelect.Select(selectIndex);

		bool enabled = row.AllowedTargets.Count > 1;
		row.TargetSelect.Disabled = !enabled;
		row.TargetSelect.SelfModulate = enabled ? Colors.White : StsColors.gray;
	}

	private static int GetValidEnumIndex<TEnum>(TEnum selectedValue, TEnum fallbackValue)
		where TEnum : struct, Enum
	{
		int selectedIndex = Convert.ToInt32(selectedValue, CultureInfo.InvariantCulture);
		int enumCount = Enum.GetValues<TEnum>().Length;
		if (selectedIndex < 0 || selectedIndex >= enumCount)
		{
			selectedIndex = Convert.ToInt32(fallbackValue, CultureInfo.InvariantCulture);
		}
		if (selectedIndex < 0 || selectedIndex >= enumCount)
		{
			selectedIndex = 0;
		}
		return selectedIndex;
	}

	private OptionButton CreateGeneratedPoolSelect(CardGeneratedCardPool selectedPool, CardGeneratedCardPool fallbackPool, string tooltipText)
	{
		OptionButton select = new OptionButton
		{
			CustomMinimumSize = new Vector2(140, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(select);
		ConstrainOptionButtonPopup(select);
		select.TooltipText = tooltipText;
		foreach (CardGeneratedCardPool pool in Enum.GetValues<CardGeneratedCardPool>())
		{
			select.AddItem(CardEditorExtraEffects.GeneratedCardPoolLabel(pool));
		}
		select.Select(GetValidEnumIndex(selectedPool, fallbackPool));
		return select;
	}

	private (OptionButton Select, NMegaLineEdit TurnsField, Control TurnsSpin) CreateExtraEffectDurationControls<TEnum>(
		TEnum selectedDuration,
		TEnum fallbackDuration,
		int turnsValue,
		int minTurns,
		bool allowNegativeTurns,
		string durationTooltipText,
		string turnsTooltipText,
		Func<TEnum, string> labelFactory)
		where TEnum : struct, Enum
	{
		OptionButton select = new OptionButton
		{
			CustomMinimumSize = new Vector2(140, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(select);
		ConstrainOptionButtonPopup(select);
		select.TooltipText = durationTooltipText;
		foreach (TEnum duration in Enum.GetValues<TEnum>())
		{
			select.AddItem(labelFactory(duration));
		}
		select.Select(GetValidEnumIndex(selectedDuration, fallbackDuration));

		int normalizedTurns = allowNegativeTurns
			? Math.Clamp(turnsValue, -99, 99)
			: Math.Clamp(turnsValue < minTurns ? 2 : turnsValue, minTurns, 99);
		NMegaLineEdit turnsField = new NMegaLineEdit
		{
			Text = normalizedTurns.ToString(CultureInfo.InvariantCulture),
			CustomMinimumSize = _amountFieldMinSize
		};
		turnsField.Alignment = HorizontalAlignment.Center;
		turnsField.TooltipText = turnsTooltipText;
		StyleInput(turnsField);
		turnsField.TextChanged += _ => QueuePreviewUpdate();

		decimal turnsMin = allowNegativeTurns ? -99m : minTurns;
		Control turnsSpin = CreateSpinButtons(turnsField, step: 1m, minValue: turnsMin, maxValue: 99m, isInteger: true);
		return (select, turnsField, turnsSpin);
	}

	private static int GetUpgradeDisplayNumericValue(CardExtraEffect baseEffect, CardExtraEffect? upgradeEffect, Func<CardExtraEffect, int> selector, bool numericFieldsAreDeltas)
	{
		if (upgradeEffect == null)
		{
			return 0;
		}

		int storedValue = selector(upgradeEffect);
		return numericFieldsAreDeltas ? storedValue : storedValue - selector(baseEffect);
	}

	private static CardExtraEffect BuildUpgradeDeltaRowEffect(CardExtraEffect baseEffect, CardExtraEffect? upgradeEffect, bool numericFieldsAreDeltas)
	{
		CardExtraEffect display = CardEditorExtraEffects.CloneEffect(baseEffect);
		int amountDelta = 0;

		if (upgradeEffect != null && CardEditorExtraEffects.IsValidUpgradeDeltaAmount(upgradeEffect.Kind, upgradeEffect.Amount))
		{
			amountDelta = upgradeEffect.Amount;
		}

		display.DisableOnUpgrade = upgradeEffect?.DisableOnUpgrade ?? false;
		display.Amount = amountDelta;
		if (display.AmountIsX)
		{
			display.AmountXPlus = amountDelta;
		}

		display.Turns = GetUpgradeDisplayNumericValue(baseEffect, upgradeEffect, e => e.Turns, numericFieldsAreDeltas);
		display.RepeatCount = GetUpgradeDisplayNumericValue(baseEffect, upgradeEffect, e => e.RepeatCount, numericFieldsAreDeltas);
		display.TriggerEveryN = GetUpgradeDisplayNumericValue(baseEffect, upgradeEffect, e => e.TriggerEveryN, numericFieldsAreDeltas);
		display.TriggerMaxFires = GetUpgradeDisplayNumericValue(baseEffect, upgradeEffect, e => e.TriggerMaxFires, numericFieldsAreDeltas);
		display.TriggerMaxTurns = GetUpgradeDisplayNumericValue(baseEffect, upgradeEffect, e => e.TriggerMaxTurns, numericFieldsAreDeltas);
		display.CreatedCardsCostTurns = GetUpgradeDisplayNumericValue(baseEffect, upgradeEffect, e => e.CreatedCardsCostTurns, numericFieldsAreDeltas);
		display.CardCostsLessTurns = GetUpgradeDisplayNumericValue(baseEffect, upgradeEffect, e => e.CardCostsLessTurns, numericFieldsAreDeltas);
		display.CountTurns = GetUpgradeDisplayNumericValue(baseEffect, upgradeEffect, e => e.CountTurns, numericFieldsAreDeltas);
		display.CountConditionAmount = GetUpgradeDisplayNumericValue(baseEffect, upgradeEffect, e => e.CountConditionAmount, numericFieldsAreDeltas);
		display.CardGrantTurns = GetUpgradeDisplayNumericValue(baseEffect, upgradeEffect, e => e.CardGrantTurns, numericFieldsAreDeltas);
		display.CardSelectionCount = GetUpgradeDisplayNumericValue(baseEffect, upgradeEffect, e => e.CardSelectionCount, numericFieldsAreDeltas);

		return display;
	}

	private static int ParseExtraEffectNumericField(LineEdit? field, int absoluteDefault, bool isDeltaRow, int minAbsolute, int maxAbsolute)
	{
		int fallback = isDeltaRow ? 0 : absoluteDefault;
		int value = ParseIntOrDefault(field?.Text, fallback);
		return isDeltaRow ? value : Math.Clamp(value, minAbsolute, maxAbsolute);
	}

	private void AddExtraEffectRow(CardExtraEffect? effect, bool isUpgradeDeltaRow = false)
	{
		effect = CardEditorExtraEffects.NormalizeLegacyOrbChannelEffect(effect);
		effect = CardEditorExtraEffects.NormalizeLegacyEvokeOrbsEffect(effect);
		effect = CardEditorExtraEffects.NormalizeLegacyOrbSlotEffect(effect);
		effect = CardEditorExtraEffects.NormalizeSelfPileAutoEffect(effect);

		VBoxContainer wrapper = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		wrapper.AddThemeConstantOverride("separation", 6);

		HBoxContainer rowTop = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		rowTop.AddThemeConstantOverride("separation", 10);

		OptionButton kindSelect = new OptionButton
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = _fieldMinSize
		};
		StyleInput(kindSelect);
		ConstrainOptionButtonPopup(kindSelect);

		List<int> kindDefinitionIndices = new List<int>();
		for (int definitionIndex = 0; definitionIndex < CardEditorExtraEffects.Definitions.Count; definitionIndex++)
		{
			CardExtraEffectDefinition def = CardEditorExtraEffects.Definitions[definitionIndex];
			if (CardEditorExtraEffects.IsLegacyOrbChannelKind(def.Kind)
				|| CardEditorExtraEffects.IsLegacyEvokeOrbsKind(def.Kind)
				|| CardEditorExtraEffects.IsLegacyOrbSlotKind(def.Kind)
				|| CardEditorExtraEffects.IsLegacySelfPileAutoKind(def.Kind)
				|| IsHiddenUnifiedCardCostKind(def.Kind)
				|| IsHiddenUnifiedDrawKind(def.Kind)
				|| IsHiddenUnifiedCardGenerationKind(def.Kind)
				|| IsHiddenUnifiedIgnoreKind(def.Kind)
				|| IsHiddenUnifiedUpgradeKind(def.Kind))
			{
				continue;
			}

			kindDefinitionIndices.Add(definitionIndex);
			kindSelect.AddItem(CardEditorLoc.Enum("effectKind", def.Kind, def.Label));
		}

		int kindIndex = 0;
		if (effect != null)
		{
			CardExtraEffectKind visibleKind = GetVisibleExtraEffectKind(effect.Kind);
			for (int i = 0; i < kindDefinitionIndices.Count; i++)
			{
				if (CardEditorExtraEffects.Definitions[kindDefinitionIndices[i]].Kind == visibleKind)
				{
					kindIndex = i;
					break;
				}
			}
		}
		kindSelect.Select(kindIndex);

		OptionButton triggerSelect = new OptionButton
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = _fieldMinSize
		};
		StyleInput(triggerSelect);
		ConstrainOptionButtonPopup(triggerSelect);
		bool initialAsPower = effect?.AsPower ?? false;
		foreach (CardExtraEffectTrigger trigger in Enum.GetValues<CardExtraEffectTrigger>())
		{
			if (IsHiddenUnifiedTurnBoundaryTrigger(trigger))
			{
				continue;
			}
			triggerSelect.AddItem(CardEditorExtraEffects.TriggerLabel(trigger, initialAsPower), (int)trigger);
		}
		CardExtraEffectTrigger desiredTrigger = effect != null ? GetVisibleExtraEffectTrigger(effect.Trigger) : CardExtraEffectTrigger.OnPlay;
		int triggerIndex = triggerSelect.GetItemIndex((int)desiredTrigger);
		if (triggerIndex < 0)
		{
			triggerIndex = 0;
		}
		triggerSelect.Select(triggerIndex);

		OptionButton targetSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(180, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
		};
		StyleInput(targetSelect);
		ConstrainOptionButtonPopup(targetSelect);

		OptionButton durationSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(160, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
		};
		durationSelect.TooltipText = CardEditorLoc.T("tooltip.duration", "Duration (Permanent / This Turn)");
		StyleInput(durationSelect);
		ConstrainOptionButtonPopup(durationSelect);
		durationSelect.AddItem(CardEditorExtraEffects.DurationLabel(CardExtraEffectDuration.Permanent));
		durationSelect.AddItem(CardEditorExtraEffects.DurationLabel(CardExtraEffectDuration.ThisTurn));
		int durationIndex = effect != null ? (int)effect.Duration : 0;
		if (durationIndex < 0 || durationIndex > 1)
		{
			durationIndex = 0;
		}
		durationSelect.Select(durationIndex);

		OptionButton timingSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(timingSelect);
		ConstrainOptionButtonPopup(timingSelect);
		timingSelect.AddItem(CardEditorExtraEffects.TimingLabel(CardExtraEffectTiming.Immediate));
		timingSelect.AddItem(CardEditorExtraEffects.TimingLabel(CardExtraEffectTiming.StartOfTurn));
		timingSelect.AddItem(CardEditorExtraEffects.TimingLabel(CardExtraEffectTiming.EndOfTurn));
		timingSelect.AddItem(CardEditorExtraEffects.TimingLabel(CardExtraEffectTiming.EndOfThisTurn));
		timingSelect.AddItem(CardEditorExtraEffects.TimingLabel(CardExtraEffectTiming.StartOfEnemyTurn));
		timingSelect.AddItem(CardEditorExtraEffects.TimingLabel(CardExtraEffectTiming.EndOfEnemyTurn));
		timingSelect.AddItem(CardEditorExtraEffects.TimingLabel(CardExtraEffectTiming.StartOfAnyTurn));
		timingSelect.AddItem(CardEditorExtraEffects.TimingLabel(CardExtraEffectTiming.EndOfAnyTurn));
		timingSelect.AddItem(CardEditorExtraEffects.TimingLabel(CardExtraEffectTiming.EndOfThisAnyTurn));

		int timingIndex = effect != null ? (int)effect.Timing : 0;
		int timingCount = Enum.GetValues<CardExtraEffectTiming>().Length;
		if (timingIndex < 0 || timingIndex >= timingCount)
		{
			timingIndex = 0;
		}
		timingSelect.Select(timingIndex);

		int turns = effect?.Turns ?? 1;
		if (turns < 0)
		{
			turns = 1;
		}
		NMegaLineEdit turnsField = new NMegaLineEdit
		{
			Text = turns.ToString(CultureInfo.InvariantCulture),
			CustomMinimumSize = _amountFieldMinSize
		};
		turnsField.Alignment = HorizontalAlignment.Center;
		turnsField.TooltipText = "0 = this combat";
		StyleInput(turnsField);
		turnsField.TextChanged += _ => QueuePreviewUpdate();

		Control turnsSpin = CreateSpinButtons(turnsField, step: 1m, minValue: 0m, maxValue: 99m, isInteger: true);

		bool initialAmountIsX = effect?.AmountIsX ?? false;
		NMegaLineEdit amountField = new NMegaLineEdit
		{
			Text = (initialAmountIsX ? (effect?.AmountXPlus ?? 0) : (effect?.Amount ?? 0)).ToString(CultureInfo.InvariantCulture),
			CustomMinimumSize = _amountFieldMinSize
		};
		amountField.Alignment = HorizontalAlignment.Center;
		if (isUpgradeDeltaRow)
		{
			amountField.TooltipText = "Upgrade delta for this base slot. Use Disable to hide and deactivate it on the upgraded card.";
		}
		StyleInput(amountField);
		amountField.TextChanged += _ => QueuePreviewUpdate();

		decimal amountMin = isUpgradeDeltaRow ? -999m : 0m;
		Control spinButtons = CreateSpinButtons(amountField, step: 1m, minValue: amountMin, maxValue: 999m, isInteger: true);

		PackedScene amountTickboxScene = GD.Load<PackedScene>("res://scenes/ui/tickbox.tscn");
		Control amountTickboxVisuals = amountTickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
		Label amountTickLabel = new Label { Text = "X+" };
		StyleBodyLabel(amountTickLabel);
		KeywordTickbox amountXTickbox = new KeywordTickbox(amountTickboxVisuals, amountTickLabel, initialAmountIsX);
		amountXTickbox.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
		amountXTickbox.Visible = !_isUpgradeEditor;
		amountXTickbox.Toggled += () =>
		{
			ApplyEffectXPlusUiState(amountField, amountXTickbox, metaKeyPreviousNonXText: "card_editor_prev_extra_effect_amount_nonx", metaKeyPreviousXPlusText: "card_editor_prev_extra_effect_amount_xplus");
			QueuePreviewUpdate();
		};
		if (initialAmountIsX)
		{
			amountField.SetMeta("card_editor_prev_extra_effect_amount_nonx", (effect?.Amount ?? 0).ToString(CultureInfo.InvariantCulture));
			amountField.SetMeta("card_editor_prev_extra_effect_amount_xplus", (effect?.AmountXPlus ?? 0).ToString(CultureInfo.InvariantCulture));
		}
		else
		{
			// Seed the nonX meta so ApplyEffectXPlusUiState restores the correct amount instead of clearing the field.
			amountField.SetMeta("card_editor_prev_extra_effect_amount_nonx", amountField.Text);
		}
		ApplyEffectXPlusUiState(amountField, amountXTickbox, metaKeyPreviousNonXText: "card_editor_prev_extra_effect_amount_nonx", metaKeyPreviousXPlusText: "card_editor_prev_extra_effect_amount_xplus");

		Control disableTickboxVisuals = amountTickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
		Label disableTickLabel = new Label { Text = CardEditorLoc.T("effect.disableOnUpgrade", "Disable") };
		StyleBodyLabel(disableTickLabel);
		KeywordTickbox disableOnUpgradeTickbox = new KeywordTickbox(disableTickboxVisuals, disableTickLabel, isUpgradeDeltaRow && (effect?.DisableOnUpgrade ?? false));
		disableOnUpgradeTickbox.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
		disableOnUpgradeTickbox.Visible = isUpgradeDeltaRow;
		disableOnUpgradeTickbox.TooltipText = CardEditorLoc.T("tooltip.disableOnUpgrade", "Hide this effect and make it inactive on the upgraded card only.");
		disableOnUpgradeTickbox.Toggled += QueuePreviewUpdate;

		Button remove = new Button
		{
			Text = "\u2715",
			Flat = true,
			FocusMode = FocusModeEnum.None,
			CustomMinimumSize = new Vector2(44, _fieldMinSize.Y),
			MouseFilter = MouseFilterEnum.Stop
		};
		StyleInput(remove);
		remove.AddThemeColorOverride("font_color", StsColors.gold);
		remove.Visible = !isUpgradeDeltaRow;

		HBoxContainer configRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		configRow.AddThemeConstantOverride("separation", 10);
		configRow.AddChild(triggerSelect);
		configRow.AddChild(targetSelect);
		configRow.AddChild(durationSelect);

		VBoxContainer moveCardsRow = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		moveCardsRow.AddThemeConstantOverride("separation", 6);
		moveCardsRow.Visible = false;

		HBoxContainer moveCardsRowTop = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		moveCardsRowTop.AddThemeConstantOverride("separation", 10);

		OptionButton moveFromPileSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(moveFromPileSelect);
		ConstrainOptionButtonPopup(moveFromPileSelect);
		moveFromPileSelect.TooltipText = CardEditorLoc.T("tooltip.moveFromPile", "Move from this pile");
		foreach (CardExtraEffectCardPile pile in Enum.GetValues<CardExtraEffectCardPile>())
		{
			moveFromPileSelect.AddItem(CardEditorExtraEffects.CardPileLabel(pile));
		}
		int moveFromIndex = effect != null ? (int)effect.CardSelectionPile : (int)CardExtraEffectCardPile.DiscardPile;
		if (moveFromIndex < 0 || moveFromIndex >= Enum.GetValues<CardExtraEffectCardPile>().Length)
		{
			moveFromIndex = (int)CardExtraEffectCardPile.DiscardPile;
		}
		moveFromPileSelect.Select(moveFromIndex);

		OptionButton moveSelectionModeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(200, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
		};
		StyleInput(moveSelectionModeSelect);
		ConstrainOptionButtonPopup(moveSelectionModeSelect);
		moveSelectionModeSelect.TooltipText = "Card selection mode";
		foreach (CardExtraEffectCardSelectionMode mode in Enum.GetValues<CardExtraEffectCardSelectionMode>())
		{
			moveSelectionModeSelect.AddItem(CardEditorExtraEffects.CardSelectionModeLabel(mode));
		}
		int moveModeIndex = effect != null ? (int)effect.CardSelectionMode : (int)CardExtraEffectCardSelectionMode.Choose;
		if (moveModeIndex < 0 || moveModeIndex >= Enum.GetValues<CardExtraEffectCardSelectionMode>().Length)
		{
			moveModeIndex = 0;
		}
		moveSelectionModeSelect.Select(moveModeIndex);

		moveCardsRowTop.AddChild(moveFromPileSelect);
		moveCardsRowTop.AddChild(moveSelectionModeSelect);
		moveCardsRowTop.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer moveCardsRowBottom = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		moveCardsRowBottom.AddThemeConstantOverride("separation", 10);

		OptionButton moveToPileSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(moveToPileSelect);
		ConstrainOptionButtonPopup(moveToPileSelect);
		moveToPileSelect.TooltipText = "Move to this pile";
		foreach (CardExtraEffectCardPile pile in Enum.GetValues<CardExtraEffectCardPile>())
		{
			moveToPileSelect.AddItem(CardEditorExtraEffects.CardPileLabel(pile));
		}
		int moveToIndex = effect != null ? (int)effect.MoveToPile : (int)CardExtraEffectCardPile.DrawPile;
		if (moveToIndex < 0 || moveToIndex >= Enum.GetValues<CardExtraEffectCardPile>().Length)
		{
			moveToIndex = (int)CardExtraEffectCardPile.DrawPile;
		}
		moveToPileSelect.Select(moveToIndex);

		OptionButton moveToPositionSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(200, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
		};
		StyleInput(moveToPositionSelect);
		ConstrainOptionButtonPopup(moveToPositionSelect);
		moveToPositionSelect.TooltipText = "Draw pile position";
		foreach (CardExtraEffectCardPilePosition position in Enum.GetValues<CardExtraEffectCardPilePosition>())
		{
			moveToPositionSelect.AddItem(CardEditorExtraEffects.CardPilePositionLabel(position));
		}
		int movePosIndex = effect != null ? (int)effect.MoveToPosition : (int)CardExtraEffectCardPilePosition.Top;
		if (movePosIndex < 0 || movePosIndex >= Enum.GetValues<CardExtraEffectCardPilePosition>().Length)
		{
			movePosIndex = 0;
		}
		moveToPositionSelect.Select(movePosIndex);

		moveCardsRowBottom.AddChild(moveToPileSelect);
		moveCardsRowBottom.AddChild(moveToPositionSelect);
		moveCardsRowBottom.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		moveCardsRow.AddChild(moveCardsRowTop);
		moveCardsRow.AddChild(moveCardsRowBottom);

		HBoxContainer costFilterRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		costFilterRow.AddThemeConstantOverride("separation", 10);
		costFilterRow.Visible = false;

		Label costFilterLabel = new Label { Text = CardEditorLoc.T("costFilter.label", "Max Cost"), CustomMinimumSize = new Vector2(120, 0) };
		StyleBodyLabel(costFilterLabel);

		PackedScene costFilterTickboxScene = GD.Load<PackedScene>("res://scenes/ui/tickbox.tscn");
		Control costFilterTickboxVisuals = costFilterTickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
		Label costFilterTickboxLabel = new Label { Text = string.Empty };
		StyleBodyLabel(costFilterTickboxLabel);
		KeywordTickbox costFilterTickbox = new KeywordTickbox(costFilterTickboxVisuals, costFilterTickboxLabel, effect?.CostFilterEnabled ?? false);
		costFilterTickbox.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
		costFilterTickbox.TooltipText = CardEditorLoc.T("tooltip.costFilter", "Only affect cards costing this much or less. 0 = free cards only. X-cost cards are always excluded.");

		int costFilterMaxVal = Math.Max(0, effect?.CostFilterMax ?? 0);
		NMegaLineEdit costFilterField = new NMegaLineEdit
		{
			Text = costFilterMaxVal.ToString(CultureInfo.InvariantCulture),
			CustomMinimumSize = _amountFieldMinSize
		};
		costFilterField.Alignment = HorizontalAlignment.Center;
		costFilterField.TooltipText = CardEditorLoc.T("tooltip.costFilterMax", "Cards costing more than this will not be affected (0 = free cards only).");
		StyleInput(costFilterField);
		costFilterField.TextChanged += _ => QueuePreviewUpdate();
		costFilterTickbox.Toggled += () => QueuePreviewUpdate();

		Control costFilterSpin = CreateSpinButtons(costFilterField, step: 1m, minValue: 0m, maxValue: 9m, isInteger: true);

		costFilterRow.AddChild(costFilterLabel);
		costFilterRow.AddChild(costFilterTickbox);
		costFilterRow.AddChild(costFilterSpin);
		costFilterRow.AddChild(costFilterField);
		costFilterRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		moveCardsRow.AddChild(costFilterRow);

		HBoxContainer drawCostRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		drawCostRow.AddThemeConstantOverride("separation", 10);
		drawCostRow.Visible = false;

		Label drawCostLabel = new Label { Text = CardEditorLoc.T("drawCost.label", "Draw Modifier"), CustomMinimumSize = new Vector2(120, 0) };
		StyleBodyLabel(drawCostLabel);

		Control drawCostTickboxVisuals = costFilterTickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
		Label drawCostTickboxLabel = new Label { Text = CardEditorLoc.T("drawCost.less", "Cost Less") };
		StyleBodyLabel(drawCostTickboxLabel);
		bool initialDrawCostEnabled = effect?.Kind == CardExtraEffectKind.DrawCardsThatCostLess;
		KeywordTickbox drawCostTickbox = new KeywordTickbox(drawCostTickboxVisuals, drawCostTickboxLabel, initialDrawCostEnabled);
		drawCostTickbox.TooltipText = CardEditorLoc.T("tooltip.drawCostLess", "After drawing, the drawn cards cost this much less for the chosen duration.");

		int initialDrawCostLess = initialDrawCostEnabled ? (effect?.CardSelectionCount ?? 1) : 1;
		if (initialDrawCostLess <= 0)
		{
			initialDrawCostLess = 1;
		}
		NMegaLineEdit drawCostField = new NMegaLineEdit
		{
			Text = Math.Clamp(initialDrawCostLess, 1, 99).ToString(CultureInfo.InvariantCulture),
			CustomMinimumSize = _amountFieldMinSize
		};
		drawCostField.Alignment = HorizontalAlignment.Center;
		drawCostField.TooltipText = CardEditorLoc.T("tooltip.drawCostLessAmount", "How much less the drawn cards cost (minimum 1).");
		StyleInput(drawCostField);

		Control drawCostSpin = CreateSpinButtons(drawCostField, step: 1m, minValue: 1m, maxValue: 99m, isInteger: true);

		drawCostRow.AddChild(drawCostLabel);
		drawCostRow.AddChild(drawCostTickbox);
		drawCostRow.AddChild(drawCostSpin);
		drawCostRow.AddChild(drawCostField);
		drawCostRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		VBoxContainer grantRow = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		grantRow.AddThemeConstantOverride("separation", 6);
		grantRow.Visible = false;

		HBoxContainer grantSelectRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		grantSelectRow.AddThemeConstantOverride("separation", 10);

		OptionButton grantPileSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(grantPileSelect);
		ConstrainOptionButtonPopup(grantPileSelect);
		grantPileSelect.TooltipText = "Grant to a card from this pile";
		foreach (CardExtraEffectCardPile pile in Enum.GetValues<CardExtraEffectCardPile>())
		{
			grantPileSelect.AddItem(CardEditorExtraEffects.CardPileLabel(pile));
		}
		int grantPileIndex = effect != null ? (int)effect.CardSelectionPile : (int)CardExtraEffectCardPile.Hand;
		if (grantPileIndex < 0 || grantPileIndex >= Enum.GetValues<CardExtraEffectCardPile>().Length)
		{
			grantPileIndex = 0;
		}
		grantPileSelect.Select(grantPileIndex);

		OptionButton grantModeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(200, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
		};
		StyleInput(grantModeSelect);
		ConstrainOptionButtonPopup(grantModeSelect);
		grantModeSelect.TooltipText = "Card selection mode";
		foreach (CardExtraEffectCardSelectionMode mode in Enum.GetValues<CardExtraEffectCardSelectionMode>())
		{
			grantModeSelect.AddItem(CardEditorExtraEffects.CardSelectionModeLabel(mode));
		}
		int grantModeIndex = effect != null ? (int)effect.CardSelectionMode : (int)CardExtraEffectCardSelectionMode.Choose;
		if (grantModeIndex < 0 || grantModeIndex >= Enum.GetValues<CardExtraEffectCardSelectionMode>().Length)
		{
			grantModeIndex = 0;
		}
		grantModeSelect.Select(grantModeIndex);

		grantSelectRow.AddChild(grantPileSelect);
		grantSelectRow.AddChild(grantModeSelect);
		grantSelectRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer grantCountRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		grantCountRow.AddThemeConstantOverride("separation", 10);

		Label grantCountLabel = new Label { Text = CardEditorLoc.T("ui.count", "Count"), CustomMinimumSize = new Vector2(120, 0) };
		StyleBodyLabel(grantCountLabel);

		PackedScene grantCountTickboxScene = GD.Load<PackedScene>("res://scenes/ui/tickbox.tscn");
		Control grantCountXVisuals = grantCountTickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
		Label grantCountXLabel = new Label { Text = "X" };
		StyleBodyLabel(grantCountXLabel);
		KeywordTickbox grantCountXTickbox = new KeywordTickbox(grantCountXVisuals, grantCountXLabel, effect?.CardSelectionCountIsX ?? false);
		grantCountXTickbox.TooltipText = CardEditorLoc.T("tooltip.grantCountX", "Use X as the number of selected cards (based on Energy/Stars spent).");

		int grantCount = effect?.CardSelectionCount ?? 1;
		if (grantCount < 0)
		{
			grantCount = 0;
		}
		NMegaLineEdit grantCountField = new NMegaLineEdit
		{
			Text = grantCount.ToString(CultureInfo.InvariantCulture),
			CustomMinimumSize = _amountFieldMinSize
		};
		grantCountField.Alignment = HorizontalAlignment.Center;
		grantCountField.TooltipText = CardEditorLoc.T("tooltip.grantCount", "How many cards to select (0 = none).");
		StyleInput(grantCountField);
		grantCountField.TextChanged += _ => QueuePreviewUpdate();

		Control grantCountSpin = CreateSpinButtons(grantCountField, step: 1m, minValue: 0m, maxValue: 99m, isInteger: true);

		grantCountRow.AddChild(grantCountLabel);
		grantCountRow.AddChild(grantCountXTickbox);
		grantCountRow.AddChild(grantCountSpin);
		grantCountRow.AddChild(grantCountField);
		grantCountRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer grantFilterRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		grantFilterRow.AddThemeConstantOverride("separation", 10);

		OptionButton grantCountPoolSelect = CreateGeneratedPoolSelect(
			effect?.CardSelectionPool ?? CardGeneratedCardPool.All,
			CardGeneratedCardPool.All,
			CardEditorLoc.T("tooltip.grantPool", "Only select cards from this pool (color/class)."));

		OptionButton grantCountTypeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(grantCountTypeSelect);
		ConstrainOptionButtonPopup(grantCountTypeSelect);
		grantCountTypeSelect.TooltipText = CardEditorLoc.T("tooltip.grantType", "Only select cards of this card type.");
		foreach (CardGeneratedCardType type in Enum.GetValues<CardGeneratedCardType>())
		{
			grantCountTypeSelect.AddItem(CardEditorExtraEffects.GeneratedCardTypeLabel(type));
		}
		int grantTypeIndex = effect != null ? (int)effect.CardSelectionType : (int)CardGeneratedCardType.Any;
		if (grantTypeIndex < 0 || grantTypeIndex >= Enum.GetValues<CardGeneratedCardType>().Length)
		{
			grantTypeIndex = (int)CardGeneratedCardType.Any;
		}
		grantCountTypeSelect.Select(grantTypeIndex);

		OptionButton grantCountFilterSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(grantCountFilterSelect);
		ConstrainOptionButtonPopup(grantCountFilterSelect);
		grantCountFilterSelect.TooltipText = CardEditorLoc.T("tooltip.grantFilter", "Only select cards that match this extra-effect filter.");
		foreach (CardExtraEffectCountCardFilter filter in Enum.GetValues<CardExtraEffectCountCardFilter>())
		{
			grantCountFilterSelect.AddItem(CardEditorExtraEffects.CountCardFilterLabel(filter));
		}
		int grantFilterIndex = effect != null ? (int)effect.CardSelectionFilter : (int)CardExtraEffectCountCardFilter.Any;
		if (grantFilterIndex < 0 || grantFilterIndex >= Enum.GetValues<CardExtraEffectCountCardFilter>().Length)
		{
			grantFilterIndex = (int)CardExtraEffectCountCardFilter.Any;
		}
		grantCountFilterSelect.Select(grantFilterIndex);

		grantCountPoolSelect.ItemSelected += _ => QueuePreviewUpdate();
		grantCountTypeSelect.ItemSelected += _ => QueuePreviewUpdate();
		grantCountFilterSelect.ItemSelected += _ => QueuePreviewUpdate();

		grantFilterRow.AddChild(grantCountPoolSelect);
		grantFilterRow.AddChild(grantCountTypeSelect);
		grantFilterRow.AddChild(grantCountFilterSelect);
		grantFilterRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer grantDurationOuterRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		grantDurationOuterRow.AddThemeConstantOverride("separation", 10);

		(OptionButton grantDurationSelect, NMegaLineEdit grantTurnsField, Control grantTurnsSpin) = CreateExtraEffectDurationControls(
			effect?.CardGrantDuration ?? CardExtraEffectCardGrantDuration.ThisTurn,
			CardExtraEffectCardGrantDuration.ThisTurn,
			effect?.CardGrantTurns ?? 2,
			minTurns: 1,
			allowNegativeTurns: isUpgradeDeltaRow,
			durationTooltipText: "How long the granted effect lasts",
			turnsTooltipText: "How many turns the grant lasts (when Duration = X Turns)",
			CardEditorExtraEffects.CardGrantDurationLabel);

		HBoxContainer grantTurnsRow = new HBoxContainer();
		grantTurnsRow.AddThemeConstantOverride("separation", 10);
		grantTurnsRow.AddChild(grantTurnsSpin);
		grantTurnsRow.AddChild(grantTurnsField);

		grantDurationOuterRow.AddChild(grantDurationSelect);
		grantDurationOuterRow.AddChild(grantTurnsRow);
		grantDurationOuterRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		grantRow.AddChild(grantSelectRow);
		grantRow.AddChild(grantCountRow);
		grantRow.AddChild(grantFilterRow);
		grantRow.AddChild(grantDurationOuterRow);

		HBoxContainer enchantmentRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		enchantmentRow.AddThemeConstantOverride("separation", 10);

		OptionButton enchantmentSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(220, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(enchantmentSelect);
		ConstrainOptionButtonPopup(enchantmentSelect);
		enchantmentSelect.TooltipText = "Which enchantment this effect applies.";
		PopulateExtraEffectEnchantmentSelect(enchantmentSelect, effect?.EnchantmentId);

		(OptionButton enchantmentDurationSelect, NMegaLineEdit enchantmentTurnsField, Control enchantmentTurnsSpin) = CreateExtraEffectDurationControls(
			effect?.EnchantmentDuration ?? CardExtraEffectEnchantmentDuration.ThisCombat,
			CardExtraEffectEnchantmentDuration.ThisCombat,
			effect?.EnchantmentTurns ?? 2,
			minTurns: 1,
			allowNegativeTurns: isUpgradeDeltaRow,
			durationTooltipText: "How long the enchantment lasts",
			turnsTooltipText: "How many turns the enchantment lasts (when Duration = X Turns)",
			CardEditorExtraEffects.EnchantmentDurationLabel);

		HBoxContainer enchantmentTurnsRow = new HBoxContainer();
		enchantmentTurnsRow.AddThemeConstantOverride("separation", 10);
		enchantmentTurnsRow.AddChild(enchantmentTurnsSpin);
		enchantmentTurnsRow.AddChild(enchantmentTurnsField);

		enchantmentRow.AddChild(enchantmentSelect);
		enchantmentRow.AddChild(enchantmentDurationSelect);
		enchantmentRow.AddChild(enchantmentTurnsRow);
		enchantmentRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer timingRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		timingRow.AddThemeConstantOverride("separation", 10);
		Label timingLabel = new Label { Text = CardEditorLoc.T("timing.label", "Timing"), CustomMinimumSize = new Vector2(120, 0) };
		timingLabel.TooltipText = CardEditorLoc.T("tooltip.timing", "When this effect resolves after the trigger.");
		StyleBodyLabel(timingLabel);
		timingRow.AddChild(timingLabel);

		OptionButton timingModeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(140, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(timingModeSelect);
		ConstrainOptionButtonPopup(timingModeSelect);
		timingModeSelect.TooltipText = CardEditorLoc.T("tooltip.timingMode", "Resolve now, or at a start/end of turn boundary.");
		timingModeSelect.AddItem(CardEditorExtraEffects.TimingLabel(CardExtraEffectTiming.Immediate));
		timingModeSelect.AddItem(CardEditorLoc.T("timing.mode.turnBoundary", "Turn Boundary"));

		OptionButton timingEdgeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(timingEdgeSelect);
		ConstrainOptionButtonPopup(timingEdgeSelect);
		timingEdgeSelect.TooltipText = CardEditorLoc.T("tooltip.timingEdge", "Start or end of the turn.");
		timingEdgeSelect.AddItem(CardEditorLoc.T("turnBoundary.edge.start", "Start"));
		timingEdgeSelect.AddItem(CardEditorLoc.T("turnBoundary.edge.end", "End"));

		OptionButton timingSideSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(150, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(timingSideSelect);
		ConstrainOptionButtonPopup(timingSideSelect);
		timingSideSelect.TooltipText = CardEditorLoc.T("tooltip.timingSide", "Resolve on your turn, the enemy turn, or both.");
		timingSideSelect.AddItem(CardEditorLoc.T("turnBoundary.side.your", "Your Turn"));
		timingSideSelect.AddItem(CardEditorLoc.T("turnBoundary.side.enemy", "Enemy Turn"));
		timingSideSelect.AddItem(CardEditorLoc.T("turnBoundary.side.both", "Both"));

		OptionButton timingOffsetSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(130, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(timingOffsetSelect);
		ConstrainOptionButtonPopup(timingOffsetSelect);
		timingOffsetSelect.TooltipText = CardEditorLoc.T("tooltip.timingOffset", "Whether to include this turn (end edge only).");
		timingOffsetSelect.AddItem(CardEditorLoc.T("timing.offset.thisTurn", "This Turn"));
		timingOffsetSelect.AddItem(CardEditorLoc.T("timing.offset.nextTurn", "Next Turn"));

		CardExtraEffectTiming initialTimingForUi = timingIndex < 0 || timingIndex >= Enum.GetValues<CardExtraEffectTiming>().Length
			? CardExtraEffectTiming.Immediate
			: (CardExtraEffectTiming)timingIndex;
		bool initialIsNow = initialTimingForUi == CardExtraEffectTiming.Immediate;
		timingModeSelect.Select(initialIsNow ? 0 : 1);
		bool initialBoth = initialTimingForUi is CardExtraEffectTiming.StartOfAnyTurn or CardExtraEffectTiming.EndOfAnyTurn or CardExtraEffectTiming.EndOfThisAnyTurn;
		bool initialEnemy = initialTimingForUi is CardExtraEffectTiming.StartOfEnemyTurn or CardExtraEffectTiming.EndOfEnemyTurn;
		bool initialStart = initialTimingForUi is CardExtraEffectTiming.StartOfTurn or CardExtraEffectTiming.StartOfEnemyTurn or CardExtraEffectTiming.StartOfAnyTurn;
		bool initialThisTurn = initialTimingForUi is CardExtraEffectTiming.EndOfThisTurn or CardExtraEffectTiming.EndOfThisAnyTurn;
		timingEdgeSelect.Select(initialStart ? 0 : 1);
		timingSideSelect.Select(initialEnemy ? 1 : (initialBoth ? 2 : 0));
		timingOffsetSelect.Select(initialThisTurn ? 0 : 1);
		timingEdgeSelect.Visible = !initialIsNow;
		timingSideSelect.Visible = !initialIsNow;
		timingOffsetSelect.Visible = !initialIsNow && timingEdgeSelect.Selected == 1 && timingSideSelect.Selected != 1;

		timingRow.AddChild(timingModeSelect);
		timingRow.AddChild(timingEdgeSelect);
		timingRow.AddChild(timingSideSelect);
		timingRow.AddChild(timingOffsetSelect);
		timingRow.AddChild(turnsSpin);
		timingRow.AddChild(turnsField);
		timingRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer createdCostRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		createdCostRow.AddThemeConstantOverride("separation", 10);

		(OptionButton createdCostDurationSelect, NMegaLineEdit createdCostTurnsField, Control createdCostTurnsSpin) = CreateExtraEffectDurationControls(
			effect?.CreatedCardsCostDuration ?? CardCreatedCardsCostDuration.ThisTurn,
			CardCreatedCardsCostDuration.ThisTurn,
			effect?.CreatedCardsCostTurns ?? 2,
			minTurns: 1,
			allowNegativeTurns: isUpgradeDeltaRow,
			durationTooltipText: "How long the created-card cost discount lasts",
			turnsTooltipText: "How many turns the cost discount lasts (when Duration = X Turns)",
			CardEditorExtraEffects.CreatedCardsCostDurationLabel);

		createdCostRow.AddChild(createdCostDurationSelect);
		createdCostRow.AddChild(createdCostTurnsSpin);
		createdCostRow.AddChild(createdCostTurnsField);
		createdCostRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer upgradeRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		upgradeRow.AddThemeConstantOverride("separation", 10);
		upgradeRow.Visible = false;

		OptionButton upgradeVariantSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(260, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(upgradeVariantSelect);
		ConstrainOptionButtonPopup(upgradeVariantSelect);
		upgradeVariantSelect.TooltipText = "Choose what gets upgraded.";
		upgradeVariantSelect.AddItem("Created by This Card");
		upgradeVariantSelect.AddItem("Created Cards (Aura)");
		upgradeVariantSelect.AddItem("Cards in Pile(s) (Aura)");
		int upgradeVariantIndex = effect != null ? (int)GetUnifiedUpgradeEffectVariant(effect.Kind) : 0;
		if (upgradeVariantIndex < 0 || upgradeVariantIndex >= upgradeVariantSelect.ItemCount)
		{
			upgradeVariantIndex = 0;
		}
		upgradeVariantSelect.Select(upgradeVariantIndex);

		OptionButton upgradePileSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(190, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(upgradePileSelect);
		ConstrainOptionButtonPopup(upgradePileSelect);
		upgradePileSelect.TooltipText = "Which pile(s) the upgrade aura applies to.";
		foreach (CardExtraEffectCardPile pile in Enum.GetValues<CardExtraEffectCardPile>())
		{
			upgradePileSelect.AddItem(CardEditorExtraEffects.CardPileLabel(pile));
		}
		int upgradePileIndex = effect != null ? (int)effect.CardSelectionPile : (int)CardExtraEffectCardPile.AllPiles;
		if (upgradePileIndex < 0 || upgradePileIndex >= Enum.GetValues<CardExtraEffectCardPile>().Length)
		{
			upgradePileIndex = (int)CardExtraEffectCardPile.AllPiles;
		}
		upgradePileSelect.Select(upgradePileIndex);

		upgradeRow.AddChild(upgradeVariantSelect);
		upgradeRow.AddChild(upgradePileSelect);
		upgradeRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer cardCostsLessRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		cardCostsLessRow.AddThemeConstantOverride("separation", 10);

		OptionButton cardCostsLessKindSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(220, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(cardCostsLessKindSelect);
		ConstrainOptionButtonPopup(cardCostsLessKindSelect);
		cardCostsLessKindSelect.TooltipText = "Choose which cards get the cost change and which resource it affects.";
		cardCostsLessKindSelect.AddItem("This Card (Energy)");
		cardCostsLessKindSelect.AddItem("This Card (Stars)");
		cardCostsLessKindSelect.AddItem("Matching Cards (Energy)");
		cardCostsLessKindSelect.AddItem("Matching Cards (Stars)");
		cardCostsLessKindSelect.AddItem("Drawn Cards");
		cardCostsLessKindSelect.AddItem("Created Cards");
		int cardCostsLessKindIndex = effect != null ? (int)GetUnifiedCardCostEffectVariant(effect.Kind) : 0;
		if (cardCostsLessKindIndex < 0 || cardCostsLessKindIndex >= cardCostsLessKindSelect.ItemCount)
		{
			cardCostsLessKindIndex = 0;
		}
		cardCostsLessKindSelect.Select(cardCostsLessKindIndex);

		OptionButton cardCostsLessModeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(180, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
		};
		StyleInput(cardCostsLessModeSelect);
		ConstrainOptionButtonPopup(cardCostsLessModeSelect);
		cardCostsLessModeSelect.TooltipText = "Passive: always applies. Triggered: activates when the chosen trigger happens.";
		cardCostsLessModeSelect.AddItem(CardEditorExtraEffects.CardCostsLessModeLabel(CardExtraEffectCardCostsLessMode.Passive));
		cardCostsLessModeSelect.AddItem(CardEditorExtraEffects.CardCostsLessModeLabel(CardExtraEffectCardCostsLessMode.Triggered));
		int cardCostsLessModeIndex = 0;
		if (effect != null)
		{
			bool triggered = effect.CardCostsLessMode == CardExtraEffectCardCostsLessMode.Triggered
				|| (effect.CardCostsLessMode == CardExtraEffectCardCostsLessMode.Legacy && effect.Trigger != CardExtraEffectTrigger.OnPlay);
			cardCostsLessModeIndex = triggered ? 1 : 0;
		}
		cardCostsLessModeSelect.Select(cardCostsLessModeIndex);

		(OptionButton cardCostsLessDurationSelect, NMegaLineEdit cardCostsLessTurnsField, Control cardCostsLessTurnsSpin) = CreateExtraEffectDurationControls(
			effect?.CardCostsLessDuration ?? CardExtraEffectCardCostsLessDuration.Permanent,
			CardExtraEffectCardCostsLessDuration.Permanent,
			effect?.CardCostsLessTurns ?? 2,
			minTurns: 1,
			allowNegativeTurns: isUpgradeDeltaRow,
			durationTooltipText: "How long this card's cost reduction lasts (in combat)",
			turnsTooltipText: "How many turns the cost reduction lasts (when Duration = X Turns)",
			CardEditorExtraEffects.CardCostsLessDurationLabel);

		Label cardCostsLessLabel = new Label { Text = "Duration:", CustomMinimumSize = new Vector2(80, 0) };
		StyleBodyLabel(cardCostsLessLabel);
		cardCostsLessRow.AddChild(cardCostsLessKindSelect);
		cardCostsLessRow.AddChild(cardCostsLessModeSelect);
		cardCostsLessRow.AddChild(cardCostsLessLabel);
		cardCostsLessRow.AddChild(cardCostsLessDurationSelect);
		cardCostsLessRow.AddChild(cardCostsLessTurnsSpin);
		cardCostsLessRow.AddChild(cardCostsLessTurnsField);
		cardCostsLessRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer generatedCardRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		generatedCardRow.AddThemeConstantOverride("separation", 10);

		OptionButton generatedPoolSelect = CreateGeneratedPoolSelect(
			effect?.GeneratedCardPool ?? CardGeneratedCardPool.Default,
			CardGeneratedCardPool.Default,
			"Card pool (class) to generate from");

		OptionButton generatedTypeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(generatedTypeSelect);
		ConstrainOptionButtonPopup(generatedTypeSelect);
		generatedTypeSelect.TooltipText = "Card type to generate";
		foreach (CardGeneratedCardType type in Enum.GetValues<CardGeneratedCardType>())
		{
			generatedTypeSelect.AddItem(CardEditorExtraEffects.GeneratedCardTypeLabel(type));
		}
		int typeIndex = effect != null ? (int)effect.GeneratedCardType : 0;
		if (typeIndex < 0 || typeIndex >= Enum.GetValues<CardGeneratedCardType>().Length)
		{
			typeIndex = 0;
		}
		generatedTypeSelect.Select(typeIndex);

		generatedCardRow.AddChild(generatedPoolSelect);
		generatedCardRow.AddChild(generatedTypeSelect);
		generatedCardRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer specificCardRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		specificCardRow.AddThemeConstantOverride("separation", 10);
		specificCardRow.Visible = false;

		HBoxContainer orbRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Visible = false };
		orbRow.AddThemeConstantOverride("separation", 10);

		HBoxContainer multiplyStatRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Visible = false };
		multiplyStatRow.AddThemeConstantOverride("separation", 10);

		OptionButton orbActionSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(140, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(orbActionSelect);
		ConstrainOptionButtonPopup(orbActionSelect);
		orbActionSelect.TooltipText = "Choose whether to evoke or lose the selected orb.";
		foreach (CardExtraEffectOrbAction action in Enum.GetValues<CardExtraEffectOrbAction>())
		{
			orbActionSelect.AddItem(CardEditorExtraEffects.OrbActionLabel(action));
		}
		int orbActionIndex = effect != null ? (int)effect.OrbAction : (int)CardExtraEffectOrbAction.Evoke;
		if (orbActionIndex < 0 || orbActionIndex >= Enum.GetValues<CardExtraEffectOrbAction>().Length)
		{
			orbActionIndex = (int)CardExtraEffectOrbAction.Evoke;
		}
		orbActionSelect.Select(orbActionIndex);

		OptionButton orbScopeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(100, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(orbScopeSelect);
		ConstrainOptionButtonPopup(orbScopeSelect);
		orbScopeSelect.TooltipText = "Choose One to use the Amount, or All to target every matching orb.";
		foreach (CardExtraEffectOrbScope orbScope in Enum.GetValues<CardExtraEffectOrbScope>())
		{
			orbScopeSelect.AddItem(CardEditorExtraEffects.OrbScopeLabel(orbScope));
		}
		int orbScopeIndex = effect != null ? (int)effect.OrbScope : (int)CardExtraEffectOrbScope.Fixed;
		if (orbScopeIndex < 0 || orbScopeIndex >= Enum.GetValues<CardExtraEffectOrbScope>().Length)
		{
			orbScopeIndex = (int)CardExtraEffectOrbScope.Fixed;
		}
		orbScopeSelect.Select(orbScopeIndex);

		OptionButton orbTypeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(150, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(orbTypeSelect);
		ConstrainOptionButtonPopup(orbTypeSelect);
		orbTypeSelect.TooltipText = "Optionally filter to a specific orb type.";
		foreach (CardExtraEffectOrbType orbType in Enum.GetValues<CardExtraEffectOrbType>())
		{
			orbTypeSelect.AddItem(CardEditorExtraEffects.OrbTypeLabel(orbType));
		}
		int orbTypeIndex = effect != null ? (int)effect.OrbType : (int)CardExtraEffectOrbType.Any;
		if (orbTypeIndex < 0 || orbTypeIndex >= Enum.GetValues<CardExtraEffectOrbType>().Length)
		{
			orbTypeIndex = (int)CardExtraEffectOrbType.Any;
		}
		orbTypeSelect.Select(orbTypeIndex);

		OptionButton orbSelectionSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(150, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(orbSelectionSelect);
		ConstrainOptionButtonPopup(orbSelectionSelect);
		orbSelectionSelect.TooltipText = "Choose which matching orb to use.\nMiddle: if even number of orbs, biases to the middle-right (closer to evoke).";
		foreach (CardExtraEffectOrbSelection orbSelection in Enum.GetValues<CardExtraEffectOrbSelection>())
		{
			orbSelectionSelect.AddItem(CardEditorExtraEffects.OrbSelectionLabel(orbSelection));
		}
		int orbSelectionIndex = effect != null ? (int)effect.OrbSelection : (int)CardExtraEffectOrbSelection.Leftmost;
		if (orbSelectionIndex < 0 || orbSelectionIndex >= Enum.GetValues<CardExtraEffectOrbSelection>().Length)
		{
			orbSelectionIndex = (int)CardExtraEffectOrbSelection.Leftmost;
		}
		orbSelectionSelect.Select(orbSelectionIndex);

		OptionButton orbFollowUpSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(190, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(orbFollowUpSelect);
		ConstrainOptionButtonPopup(orbFollowUpSelect);
		orbFollowUpSelect.TooltipText = "After evoking, optionally channel the same orb type.";
		foreach (CardExtraEffectOrbFollowUp followUp in Enum.GetValues<CardExtraEffectOrbFollowUp>())
		{
			orbFollowUpSelect.AddItem(CardEditorExtraEffects.OrbFollowUpLabel(followUp));
		}
		int orbFollowUpIndex = effect != null ? (int)effect.OrbFollowUp : (int)CardExtraEffectOrbFollowUp.None;
		if (orbFollowUpIndex < 0 || orbFollowUpIndex >= Enum.GetValues<CardExtraEffectOrbFollowUp>().Length)
		{
			orbFollowUpIndex = (int)CardExtraEffectOrbFollowUp.None;
		}
		orbFollowUpSelect.Select(orbFollowUpIndex);

		orbRow.AddChild(orbActionSelect);
		orbRow.AddChild(orbScopeSelect);
		orbRow.AddChild(orbTypeSelect);
		orbRow.AddChild(orbSelectionSelect);
		orbRow.AddChild(orbFollowUpSelect);
		orbRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer ostyActionRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Visible = false };
		ostyActionRow.AddThemeConstantOverride("separation", 10);

		OptionButton ostyActionSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(160, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(ostyActionSelect);
		ConstrainOptionButtonPopup(ostyActionSelect);
		ostyActionSelect.TooltipText = "Choose what Osty will do.";
		foreach (CardExtraEffectOstyAction action in Enum.GetValues<CardExtraEffectOstyAction>())
		{
			ostyActionSelect.AddItem(CardEditorExtraEffects.OstyActionLabel(action));
		}
		int ostyActionIndex = effect != null ? (int)effect.OstyAction : (int)CardExtraEffectOstyAction.Attack;
		if (ostyActionIndex < 0 || ostyActionIndex >= Enum.GetValues<CardExtraEffectOstyAction>().Length)
		{
			ostyActionIndex = (int)CardExtraEffectOstyAction.Attack;
		}
		ostyActionSelect.Select(ostyActionIndex);

		ostyActionRow.AddChild(ostyActionSelect);
		ostyActionRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer grantedKeywordRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Visible = false };
		grantedKeywordRow.AddThemeConstantOverride("separation", 10);

		Label grantedKeywordLabel = new Label { Text = "Keyword", CustomMinimumSize = new Vector2(120, 0) };
		StyleBodyLabel(grantedKeywordLabel);

		OptionButton grantedKeywordSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(160, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(grantedKeywordSelect);
		ConstrainOptionButtonPopup(grantedKeywordSelect);
		grantedKeywordSelect.TooltipText = "Choose which keyword to grant to the selected cards.";
		foreach (CardKeyword kw in Enum.GetValues<CardKeyword>())
		{
			if (kw == CardKeyword.None) continue;
			grantedKeywordSelect.AddItem(CardEditorExtraEffects.GrantedKeywordLabel(kw), (int)kw);
		}
		CardKeyword initialGrantedKeyword = effect?.GrantedKeyword ?? CardKeyword.Exhaust;
		int grantedKeywordId = (int)initialGrantedKeyword;
		int grantedKeywordIdx = grantedKeywordSelect.GetItemIndex(grantedKeywordId);
		if (grantedKeywordIdx < 0) grantedKeywordIdx = 0;
		grantedKeywordSelect.Select(grantedKeywordIdx);
		grantedKeywordSelect.ItemSelected += _ => QueuePreviewUpdate();

		grantedKeywordRow.AddChild(grantedKeywordLabel);
		grantedKeywordRow.AddChild(grantedKeywordSelect);
		grantedKeywordRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer ignoreVariantRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Visible = false };
		ignoreVariantRow.AddThemeConstantOverride("separation", 10);

		Label ignoreVariantLabel = new Label { Text = CardEditorLoc.T("ignoreEffects.label", "Ignore"), CustomMinimumSize = new Vector2(120, 0) };
		StyleBodyLabel(ignoreVariantLabel);

		OptionButton ignoreVariantSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(260, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(ignoreVariantSelect);
		ConstrainOptionButtonPopup(ignoreVariantSelect);
		ignoreVariantSelect.TooltipText = CardEditorLoc.T("tooltip.ignoreEffects", "Choose which damage rule to ignore for this card (or for the granted card).");
		ignoreVariantSelect.AddItem("Ignore Effect");
		ignoreVariantSelect.AddItem("Ignore Damage Modifiers");
		ignoreVariantSelect.AddItem("Ignore Damage Caps");
		ignoreVariantSelect.AddItem("Ignore Damage Negation");
		ignoreVariantSelect.AddItem("Ignore Enemy Damage Reductions");
		int ignoreVariantIndex = effect?.Kind switch
		{
			CardExtraEffectKind.IgnoreDamageModifiers => 1,
			CardExtraEffectKind.IgnoreDamageCaps => 2,
			CardExtraEffectKind.IgnoreDamageNegation => 3,
			CardExtraEffectKind.IgnoreEnemyDamageReductions => 4,
			_ => 0
		};
		ignoreVariantSelect.Select(ignoreVariantIndex);

		ignoreVariantRow.AddChild(ignoreVariantLabel);
		ignoreVariantRow.AddChild(ignoreVariantSelect);
		ignoreVariantRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer cardGenerationVariantRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Visible = false };
		cardGenerationVariantRow.AddThemeConstantOverride("separation", 10);

		Label cardGenerationVariantLabel = new Label { Text = CardEditorLoc.T("cardGeneration.label", "Generation"), CustomMinimumSize = new Vector2(120, 0) };
		StyleBodyLabel(cardGenerationVariantLabel);

		OptionButton cardGenerationVariantSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(260, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(cardGenerationVariantSelect);
		ConstrainOptionButtonPopup(cardGenerationVariantSelect);
		cardGenerationVariantSelect.TooltipText = CardEditorLoc.T("tooltip.cardGeneration", "Choose what kind of card generation or created-card modifier this effect uses.");
		cardGenerationVariantSelect.AddItem("Add Random Card to Hand");
		cardGenerationVariantSelect.AddItem("Choose 1 of 3 Cards");
		cardGenerationVariantSelect.AddItem("Add Copy of This Card");
		cardGenerationVariantSelect.AddItem("Add Specific Card");
		cardGenerationVariantSelect.AddItem("Fetch Specific Card (Preserves State)");
		cardGenerationVariantSelect.AddItem("Created Cards Cost Less");
		cardGenerationVariantSelect.AddItem("Created Cards Are Upgraded");
		int cardGenerationVariantIndex = effect?.Kind switch
		{
			CardExtraEffectKind.ChooseOneOfThreeCardsToHand => (int)UnifiedCardGenerationVariant.ChooseOneOfThree,
			CardExtraEffectKind.AddCopyOfThisCard => (int)UnifiedCardGenerationVariant.CopyOfThisCard,
			CardExtraEffectKind.AddSpecificCardToHand => (int)UnifiedCardGenerationVariant.AddSpecificCard,
			CardExtraEffectKind.FetchSpecificCardToHand => (int)UnifiedCardGenerationVariant.FetchSpecificCard,
			CardExtraEffectKind.CreatedCardsCostLess => (int)UnifiedCardGenerationVariant.CreatedCardsCostLess,
			CardExtraEffectKind.CreatedCardsUpgraded
			or CardExtraEffectKind.GeneratedCardsUpgraded
			or CardExtraEffectKind.CardsInPileUpgradedAura => (int)UnifiedCardGenerationVariant.CreatedCardsUpgraded,
			_ => (int)UnifiedCardGenerationVariant.RandomCardToHand
		};
		if (cardGenerationVariantIndex < 0 || cardGenerationVariantIndex >= cardGenerationVariantSelect.ItemCount)
		{
			cardGenerationVariantIndex = 0;
		}
		cardGenerationVariantSelect.Select(cardGenerationVariantIndex);

		cardGenerationVariantRow.AddChild(cardGenerationVariantLabel);
		cardGenerationVariantRow.AddChild(cardGenerationVariantSelect);
		cardGenerationVariantRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer turnBoundaryRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Visible = false };
		turnBoundaryRow.AddThemeConstantOverride("separation", 10);

		Label turnBoundaryLabel = new Label { Text = CardEditorLoc.T("turnBoundary.label", "Turn"), CustomMinimumSize = new Vector2(120, 0) };
		StyleBodyLabel(turnBoundaryLabel);

		OptionButton turnBoundaryEdgeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(140, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(turnBoundaryEdgeSelect);
		ConstrainOptionButtonPopup(turnBoundaryEdgeSelect);
		turnBoundaryEdgeSelect.TooltipText = CardEditorLoc.T("tooltip.turnBoundary.edge", "Start or end of the turn.");
		turnBoundaryEdgeSelect.AddItem(CardEditorLoc.T("turnBoundary.edge.start", "Start"));
		turnBoundaryEdgeSelect.AddItem(CardEditorLoc.T("turnBoundary.edge.end", "End"));

		OptionButton turnBoundarySideSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(170, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(turnBoundarySideSelect);
		ConstrainOptionButtonPopup(turnBoundarySideSelect);
		turnBoundarySideSelect.TooltipText = CardEditorLoc.T("tooltip.turnBoundary.side", "Whose turn boundary should trigger this effect.");
		turnBoundarySideSelect.AddItem(CardEditorLoc.T("turnBoundary.side.your", "Your Turn"));
		turnBoundarySideSelect.AddItem(CardEditorLoc.T("turnBoundary.side.enemy", "Enemy Turn"));
		turnBoundarySideSelect.AddItem(CardEditorLoc.T("turnBoundary.side.both", "Both"));

		OptionButton turnBoundaryLocationSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(170, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(turnBoundaryLocationSelect);
		ConstrainOptionButtonPopup(turnBoundaryLocationSelect);
		turnBoundaryLocationSelect.TooltipText = CardEditorLoc.T("tooltip.turnBoundary.location", "Optional gate: only trigger if this card is currently in the selected location.");
		turnBoundaryLocationSelect.AddItem(CardEditorLoc.T("turnBoundary.location.any", "Any Location"));
		turnBoundaryLocationSelect.AddItem(CardEditorLoc.T("turnBoundary.location.hand", "In Hand"));
		turnBoundaryLocationSelect.AddItem(CardEditorLoc.T("turnBoundary.location.draw", "In Draw Pile"));
		turnBoundaryLocationSelect.AddItem(CardEditorLoc.T("turnBoundary.location.discard", "In Discard Pile"));
		turnBoundaryLocationSelect.AddItem(CardEditorLoc.T("turnBoundary.location.exhaust", "In Exhaust Pile"));

		CardExtraEffectTurnBoundary initialEdge = effect?.Trigger switch
		{
			CardExtraEffectTrigger.StartOfTurn or CardExtraEffectTrigger.StartOfEnemyTurn => CardExtraEffectTurnBoundary.Start,
			CardExtraEffectTrigger.TurnBoundary => effect.TurnBoundary,
			_ => CardExtraEffectTurnBoundary.End
		};
		CardExtraEffectTurnBoundarySide initialSide = effect?.Trigger switch
		{
			CardExtraEffectTrigger.StartOfEnemyTurn or CardExtraEffectTrigger.EndOfEnemyTurn => CardExtraEffectTurnBoundarySide.EnemyTurn,
			CardExtraEffectTrigger.TurnBoundary => effect.TurnBoundarySide,
			_ => CardExtraEffectTurnBoundarySide.YourTurn
		};
		CardExtraEffectTurnBoundaryCardLocation initialLoc = effect?.Trigger switch
		{
			CardExtraEffectTrigger.EndOfTurnInHand => CardExtraEffectTurnBoundaryCardLocation.Hand,
			CardExtraEffectTrigger.TurnBoundary => effect.TurnBoundaryCardLocation,
			_ => CardExtraEffectTurnBoundaryCardLocation.Any
		};

		turnBoundaryEdgeSelect.Select(initialEdge == CardExtraEffectTurnBoundary.Start ? 0 : 1);
		turnBoundarySideSelect.Select(initialSide switch
		{
			CardExtraEffectTurnBoundarySide.EnemyTurn => 1,
			CardExtraEffectTurnBoundarySide.Both => 2,
			_ => 0
		});
		turnBoundaryLocationSelect.Select(initialLoc switch
		{
			CardExtraEffectTurnBoundaryCardLocation.Hand => 1,
			CardExtraEffectTurnBoundaryCardLocation.DrawPile => 2,
			CardExtraEffectTurnBoundaryCardLocation.DiscardPile => 3,
			CardExtraEffectTurnBoundaryCardLocation.ExhaustPile => 4,
			_ => 0
		});

		turnBoundaryRow.AddChild(turnBoundaryLabel);
		turnBoundaryRow.AddChild(turnBoundaryEdgeSelect);
		turnBoundaryRow.AddChild(turnBoundarySideSelect);
		turnBoundaryRow.AddChild(turnBoundaryLocationSelect);
		turnBoundaryRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		Label specificCardLabel = new Label { Text = "Card Id", CustomMinimumSize = new Vector2(120, 0) };
		StyleBodyLabel(specificCardLabel);

		NMegaLineEdit specificCardIdField = new NMegaLineEdit
		{
			Text = effect?.SpecificCardId ?? string.Empty,
			CustomMinimumSize = _fieldMinSize
		};
		specificCardIdField.PlaceholderText = "cards.shiv";
		specificCardIdField.TooltipText = "Card model id to add (e.g. cards.shiv). For custom cards, use the full id shown in the Creator.";
		StyleInput(specificCardIdField);
		specificCardIdField.TextChanged += _ => QueuePreviewUpdate();

		Button pickSpecificCardButton = new Button
		{
			Text = CardEditorLoc.T("ui.cardPicker.button", "Pick"),
			CustomMinimumSize = new Vector2(90, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
		};
		pickSpecificCardButton.TooltipText = CardEditorLoc.T("ui.cardPicker.tooltip", "Pick a card from the full card library and fill in its id.");
		StyleInput(pickSpecificCardButton);
		pickSpecificCardButton.Pressed += () =>
		{
			OpenSpecificCardPicker(selectedId =>
			{
				specificCardIdField.Text = selectedId.ToString();
				QueuePreviewUpdate();
			});
		};

		specificCardRow.AddChild(specificCardLabel);
		specificCardRow.AddChild(specificCardIdField);
		specificCardRow.AddChild(pickSpecificCardButton);
		specificCardRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		Label multiplyStatLabel = new Label { Text = "Stat / Status", CustomMinimumSize = new Vector2(120, 0) };
		StyleBodyLabel(multiplyStatLabel);

		OptionButton multiplyStatSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(220, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(multiplyStatSelect);
		ConstrainOptionButtonPopup(multiplyStatSelect);
		multiplyStatSelect.TooltipText = "Choose which current stat or status should be multiplied.";
		foreach (CardExtraEffectMultiplierStat stat in Enum.GetValues<CardExtraEffectMultiplierStat>())
		{
			multiplyStatSelect.AddItem(CardEditorExtraEffects.MultiplierStatLabel(stat));
		}
		int multiplyStatIndex = effect != null ? (int)effect.MultiplierStat : (int)CardExtraEffectMultiplierStat.Strength;
		if (multiplyStatIndex < 0 || multiplyStatIndex >= Enum.GetValues<CardExtraEffectMultiplierStat>().Length)
		{
			multiplyStatIndex = (int)CardExtraEffectMultiplierStat.Strength;
		}
		multiplyStatSelect.Select(multiplyStatIndex);

		multiplyStatRow.AddChild(multiplyStatLabel);
		multiplyStatRow.AddChild(multiplyStatSelect);
		multiplyStatRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		PackedScene tickboxScene = GD.Load<PackedScene>("res://scenes/ui/tickbox.tscn");

		HBoxContainer repeatRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		repeatRow.AddThemeConstantOverride("separation", 10);
		repeatRow.Visible = false;

		Label repeatLabel = new Label { Text = CardEditorLoc.T("ui.repeat", "Repeat"), CustomMinimumSize = new Vector2(120, 0) };
		StyleBodyLabel(repeatLabel);

		Control repeatXVisuals = tickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
		Label repeatXLabel = new Label { Text = "X" };
		StyleBodyLabel(repeatXLabel);
		KeywordTickbox repeatXTickbox = new KeywordTickbox(repeatXVisuals, repeatXLabel, effect?.RepeatIsX ?? false);
		repeatXTickbox.TooltipText = CardEditorLoc.T("tooltip.repeatX", "Repeat X times (based on Energy/Stars spent).");

		int repeatCount = effect?.RepeatCount ?? (isUpgradeDeltaRow ? 0 : 1);
		if (!isUpgradeDeltaRow && repeatCount <= 0)
		{
			repeatCount = 1;
		}
		NMegaLineEdit repeatCountField = new NMegaLineEdit
		{
			Text = repeatCount.ToString(CultureInfo.InvariantCulture),
			CustomMinimumSize = _amountFieldMinSize
		};
		repeatCountField.Alignment = HorizontalAlignment.Center;
		repeatCountField.TooltipText = CardEditorLoc.T("tooltip.repeatCount", "How many times to repeat this effect (minimum 1).");
		StyleInput(repeatCountField);
		repeatCountField.TextChanged += _ => QueuePreviewUpdate();

		decimal repeatCountMin = isUpgradeDeltaRow ? -99m : 1m;
		Control repeatCountSpin = CreateSpinButtons(repeatCountField, step: 1m, minValue: repeatCountMin, maxValue: 99m, isInteger: true);

		repeatRow.AddChild(repeatLabel);
		repeatRow.AddChild(repeatXTickbox);
		repeatRow.AddChild(repeatCountSpin);
		repeatRow.AddChild(repeatCountField);
		repeatRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		Control scalingTickboxVisuals = tickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
		Label scalingLabel = new Label { Text = CardEditorLoc.T("ui.scaling", "Count Logic") };
		StyleBodyLabel(scalingLabel);
		KeywordTickbox scalingTickbox = new KeywordTickbox(scalingTickboxVisuals, scalingLabel, effect != null && effect.ScaleMode != CardExtraEffectScaleMode.None);

		Control powerTickboxVisuals = tickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
		Label powerLabel = new Label { Text = CardEditorLoc.T("ui.power", "Power") };
		StyleBodyLabel(powerLabel);
		KeywordTickbox powerTickbox = new KeywordTickbox(powerTickboxVisuals, powerLabel, effect?.AsPower ?? false);
		powerTickbox.TooltipText = CardEditorLoc.T("tooltip.power",
			"When enabled, this effect is routed through a persistent Power. This changes many triggers from “this card” to “any card” (global). Turn Boundary triggers usually require Power to fire at turn edges.");

		Control grantTickboxVisuals = tickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
		Label grantLabel = new Label { Text = CardEditorLoc.T("ui.grant", "Grant") };
		StyleBodyLabel(grantLabel);
		KeywordTickbox grantTickbox = new KeywordTickbox(grantTickboxVisuals, grantLabel, effect?.GrantToCard ?? false);
		grantTickbox.TooltipText = CardEditorLoc.T("tooltip.grant", "When enabled, this effect is granted to another card instead of resolving immediately.");

		HBoxContainer scalingToggleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		scalingToggleRow.AddThemeConstantOverride("separation", 10);
		scalingToggleRow.AddChild(scalingTickbox);
		scalingToggleRow.AddChild(powerTickbox);
		scalingToggleRow.AddChild(grantTickbox);
		scalingToggleRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		VBoxContainer scalingRow = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		scalingRow.AddThemeConstantOverride("separation", 8);
		scalingRow.Visible = scalingTickbox.IsTicked;

		HBoxContainer countRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		countRow.AddThemeConstantOverride("separation", 10);

		OptionButton countModeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(170, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(countModeSelect);
		ConstrainOptionButtonPopup(countModeSelect);
		countModeSelect.TooltipText = "Choose whether this count source scales the amount or only gates the effect.";
		foreach (CardExtraEffectScaleMode mode in new[] { CardExtraEffectScaleMode.PerHistoryCount, CardExtraEffectScaleMode.ConditionOnly })
		{
			countModeSelect.AddItem(CardEditorExtraEffects.ScaleModeLabel(mode));
		}
		CardExtraEffectScaleMode selectedCountMode = effect?.ScaleMode is CardExtraEffectScaleMode.PerHistoryCount or CardExtraEffectScaleMode.ConditionOnly
			? effect.ScaleMode
			: CardExtraEffectScaleMode.PerHistoryCount;
		countModeSelect.Select(selectedCountMode == CardExtraEffectScaleMode.ConditionOnly ? 1 : 0);

		OptionButton countEventSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(200, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(countEventSelect);
		ConstrainOptionButtonPopup(countEventSelect);
		countEventSelect.TooltipText = "Choose what this effect should count. Some options unlock extra selectors below, like Status/Power or Orb Type.";
		foreach (CardExtraEffectCountEvent ev in Enum.GetValues<CardExtraEffectCountEvent>())
		{
			countEventSelect.AddItem(CardEditorExtraEffects.CountEventLabel(ev));
		}
		int countEventIndex = effect != null ? (int)effect.CountEvent : (int)CardExtraEffectCountEvent.Played;
		if (countEventIndex < 0 || countEventIndex >= Enum.GetValues<CardExtraEffectCountEvent>().Length)
		{
			countEventIndex = 0;
		}
		countEventSelect.Select(countEventIndex);

		OptionButton countWindowSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(countWindowSelect);
		ConstrainOptionButtonPopup(countWindowSelect);
		foreach (CardExtraEffectCountWindow window in Enum.GetValues<CardExtraEffectCountWindow>())
		{
			countWindowSelect.AddItem(CardEditorExtraEffects.CountWindowLabel(window));
		}
		int countWindowIndex = effect != null ? (int)effect.CountWindow : (int)CardExtraEffectCountWindow.ThisCombat;
		if (countWindowIndex < 0 || countWindowIndex >= Enum.GetValues<CardExtraEffectCountWindow>().Length)
		{
			countWindowIndex = 0;
		}
		countWindowSelect.Select(countWindowIndex);

		OptionButton countPileSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			Visible = false
		};
		StyleInput(countPileSelect);
		ConstrainOptionButtonPopup(countPileSelect);
		countPileSelect.TooltipText = "Count cards currently in this pile";
		foreach (CardExtraEffectCardPile pile in Enum.GetValues<CardExtraEffectCardPile>())
		{
			countPileSelect.AddItem(CardEditorExtraEffects.CardPileLabel(pile));
		}
		int countPileIndex = effect != null ? (int)effect.CountCardPile : (int)CardExtraEffectCardPile.Hand;
		if (countPileIndex < 0 || countPileIndex >= Enum.GetValues<CardExtraEffectCardPile>().Length)
		{
			countPileIndex = (int)CardExtraEffectCardPile.Hand;
		}
		countPileSelect.Select(countPileIndex);

		countRow.AddChild(countModeSelect);
		countRow.AddChild(countEventSelect);
		countRow.AddChild(countWindowSelect);
		countRow.AddChild(countPileSelect);
		countRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		int countTurns = effect?.CountTurns ?? (isUpgradeDeltaRow ? 0 : 2);
		if (!isUpgradeDeltaRow && countTurns <= 0)
		{
			countTurns = 1;
		}
		NMegaLineEdit countTurnsField = new NMegaLineEdit
		{
			Text = countTurns.ToString(CultureInfo.InvariantCulture),
			CustomMinimumSize = _amountFieldMinSize
		};
		countTurnsField.Alignment = HorizontalAlignment.Center;
		StyleInput(countTurnsField);
		countTurnsField.TextChanged += _ => QueuePreviewUpdate();
		decimal countTurnsMin = isUpgradeDeltaRow ? -99m : 1m;
		Control countTurnsSpin = CreateSpinButtons(countTurnsField, step: 1m, minValue: countTurnsMin, maxValue: 99m, isInteger: true);

		HBoxContainer countTurnsRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		countTurnsRow.AddThemeConstantOverride("separation", 10);
		Label countTurnsLabel = new Label { Text = "Last Turns", CustomMinimumSize = new Vector2(120, 0) };
		StyleBodyLabel(countTurnsLabel);
		countTurnsRow.AddChild(countTurnsLabel);
		countTurnsRow.AddChild(countTurnsSpin);
		countTurnsRow.AddChild(countTurnsField);
		countTurnsRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer countWindowInclusionRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		countWindowInclusionRow.AddThemeConstantOverride("separation", 10);
		countWindowInclusionRow.Visible = false;
		Label countWindowInclusionLabel = new Label
		{
			Text = CardEditorLoc.T("ui.turnWindowMode", "Turn Window"),
			CustomMinimumSize = new Vector2(120, 0)
		};
		StyleBodyLabel(countWindowInclusionLabel);

		OptionButton countWindowInclusionSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(240, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(countWindowInclusionSelect);
		ConstrainOptionButtonPopup(countWindowInclusionSelect);
		countWindowInclusionSelect.AddItem(CardEditorLoc.T("ui.turnWindow.includingThisTurn", "Including this turn"), (int)CardExtraEffectCountWindowInclusion.IncludeThisTurn);
		countWindowInclusionSelect.AddItem(CardEditorLoc.T("ui.turnWindow.previousOnly", "Previous turns only"), (int)CardExtraEffectCountWindowInclusion.ExcludeThisTurn);
		int initialInclusion = effect != null && effect.CountWindowInclusion == CardExtraEffectCountWindowInclusion.ExcludeThisTurn
			? (int)CardExtraEffectCountWindowInclusion.ExcludeThisTurn
			: (int)CardExtraEffectCountWindowInclusion.IncludeThisTurn;
		countWindowInclusionSelect.Select(initialInclusion == (int)CardExtraEffectCountWindowInclusion.ExcludeThisTurn ? 1 : 0);
		countWindowInclusionSelect.TooltipText = "Controls whether 'Last X Turns' includes this current turn or only previous turns.";
		countWindowInclusionSelect.ItemSelected += _ => QueuePreviewUpdate();

		countWindowInclusionRow.AddChild(countWindowInclusionLabel);
		countWindowInclusionRow.AddChild(countWindowInclusionSelect);
		countWindowInclusionRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer blockLostCountingModeRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		blockLostCountingModeRow.AddThemeConstantOverride("separation", 10);
		blockLostCountingModeRow.Visible = false;
		Label blockLostCountingModeLabel = new Label
		{
			Text = CardEditorLoc.T("ui.blockLostCountingMode", "Block Loss"),
			CustomMinimumSize = new Vector2(120, 0)
		};
		StyleBodyLabel(blockLostCountingModeLabel);

		OptionButton blockLostCountingModeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(240, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(blockLostCountingModeSelect);
		ConstrainOptionButtonPopup(blockLostCountingModeSelect);
		blockLostCountingModeSelect.AddItem(
			CardEditorLoc.T("ui.blockLostCountingMode.damageAndEffects", "Damage / effects"),
			(int)CardExtraEffectBlockLostCountingMode.DamageAndEffects);
		blockLostCountingModeSelect.AddItem(
			CardEditorLoc.T("ui.blockLostCountingMode.includeBetweenTurns", "Including between turns"),
			(int)CardExtraEffectBlockLostCountingMode.IncludeBetweenTurns);
		int initialBlockLostMode = effect != null && effect.BlockLostCountingMode == CardExtraEffectBlockLostCountingMode.IncludeBetweenTurns
			? (int)CardExtraEffectBlockLostCountingMode.IncludeBetweenTurns
			: (int)CardExtraEffectBlockLostCountingMode.DamageAndEffects;
		blockLostCountingModeSelect.Select(initialBlockLostMode == (int)CardExtraEffectBlockLostCountingMode.IncludeBetweenTurns ? 1 : 0);
		blockLostCountingModeSelect.TooltipText = "Controls whether 'Block Lost' includes block cleared between turns.";
		blockLostCountingModeSelect.ItemSelected += _ => QueuePreviewUpdate();

		blockLostCountingModeRow.AddChild(blockLostCountingModeLabel);
		blockLostCountingModeRow.AddChild(blockLostCountingModeSelect);
		blockLostCountingModeRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer countConditionRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		countConditionRow.AddThemeConstantOverride("separation", 10);

		OptionButton countComparisonSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(170, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(countComparisonSelect);
		ConstrainOptionButtonPopup(countComparisonSelect);
		countComparisonSelect.TooltipText = "Optional threshold to require before the effect applies.";
		foreach (CardExtraEffectCountComparison comparison in Enum.GetValues<CardExtraEffectCountComparison>())
		{
			countComparisonSelect.AddItem(CardEditorExtraEffects.CountComparisonLabel(comparison));
		}
		int countComparisonIndex = effect != null ? (int)effect.CountComparison : (int)CardExtraEffectCountComparison.None;
		if (countComparisonIndex < 0 || countComparisonIndex >= Enum.GetValues<CardExtraEffectCountComparison>().Length)
		{
			countComparisonIndex = (int)CardExtraEffectCountComparison.None;
		}
		countComparisonSelect.Select(countComparisonIndex);

		int countConditionAmount = isUpgradeDeltaRow
			? (effect?.CountConditionAmount ?? 0)
			: Math.Max(0, effect?.CountConditionAmount ?? 1);
		NMegaLineEdit countConditionField = new NMegaLineEdit
		{
			Text = countConditionAmount.ToString(CultureInfo.InvariantCulture),
			CustomMinimumSize = _amountFieldMinSize
		};
		countConditionField.Alignment = HorizontalAlignment.Center;
		countConditionField.TooltipText = "Threshold amount used by the selected comparison.";
		StyleInput(countConditionField);
		countConditionField.TextChanged += _ => QueuePreviewUpdate();
		decimal countConditionMin = isUpgradeDeltaRow ? -99m : 0m;
		Control countConditionSpin = CreateSpinButtons(countConditionField, step: 1m, minValue: countConditionMin, maxValue: 99m, isInteger: true);

		countConditionRow.AddChild(countComparisonSelect);
		countConditionRow.AddChild(countConditionSpin);
		countConditionRow.AddChild(countConditionField);
		countConditionRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer filterRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		filterRow.AddThemeConstantOverride("separation", 10);

		OptionButton countPoolSelect = CreateGeneratedPoolSelect(
			effect?.CountCardPool ?? CardGeneratedCardPool.All,
			CardGeneratedCardPool.All,
			string.Empty);

		OptionButton countTypeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(countTypeSelect);
		ConstrainOptionButtonPopup(countTypeSelect);
		foreach (CardGeneratedCardType type in Enum.GetValues<CardGeneratedCardType>())
		{
			countTypeSelect.AddItem(CardEditorExtraEffects.GeneratedCardTypeLabel(type));
		}
		int countTypeIndex = effect != null ? (int)effect.CountCardType : 0;
		if (countTypeIndex < 0 || countTypeIndex >= Enum.GetValues<CardGeneratedCardType>().Length)
		{
			countTypeIndex = 0;
		}
		countTypeSelect.Select(countTypeIndex);

		OptionButton countFilterSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(countFilterSelect);
		ConstrainOptionButtonPopup(countFilterSelect);
		countFilterSelect.TooltipText = "Only count cards that match this effect (for Scaling).";
		foreach (CardExtraEffectCountCardFilter filter in Enum.GetValues<CardExtraEffectCountCardFilter>())
		{
			countFilterSelect.AddItem(CardEditorExtraEffects.CountCardFilterLabel(filter));
		}
		int countFilterIndex = effect != null ? (int)effect.CountCardFilter : (int)CardExtraEffectCountCardFilter.Any;
		if (countFilterIndex == (int)CardExtraEffectCountCardFilter.Any && (effect?.CountOnlyBlockCards ?? false))
		{
			countFilterIndex = (int)CardExtraEffectCountCardFilter.GainBlock;
		}
		if (countFilterIndex < 0 || countFilterIndex >= Enum.GetValues<CardExtraEffectCountCardFilter>().Length)
		{
			countFilterIndex = 0;
		}
		countFilterSelect.Select(countFilterIndex);

		filterRow.AddChild(countPoolSelect);
		filterRow.AddChild(countTypeSelect);
		filterRow.AddChild(countFilterSelect);
		filterRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer orbCountFilterRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		orbCountFilterRow.AddThemeConstantOverride("separation", 10);

		OptionButton countOrbTypeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(150, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(countOrbTypeSelect);
		ConstrainOptionButtonPopup(countOrbTypeSelect);
		countOrbTypeSelect.TooltipText = "Filter orb-based count events to a specific orb type, like Lightning or Frost.";
		foreach (CardExtraEffectOrbType orbType in Enum.GetValues<CardExtraEffectOrbType>())
		{
			countOrbTypeSelect.AddItem(CardEditorExtraEffects.OrbTypeLabel(orbType));
		}
		int countOrbTypeIndex = effect != null ? (int)effect.CountOrbType : (int)CardExtraEffectOrbType.Any;
		if (countOrbTypeIndex < 0 || countOrbTypeIndex >= Enum.GetValues<CardExtraEffectOrbType>().Length)
		{
			countOrbTypeIndex = (int)CardExtraEffectOrbType.Any;
		}
		countOrbTypeSelect.Select(countOrbTypeIndex);

		OptionButton countOrbSelectionSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(150, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(countOrbSelectionSelect);
		ConstrainOptionButtonPopup(countOrbSelectionSelect);
		countOrbSelectionSelect.TooltipText = "Choose which orb position to check when using a position-based orb condition.\nMiddle: if even number of orbs, biases to the middle-right (closer to evoke).";
		foreach (CardExtraEffectOrbSelection orbSelection in Enum.GetValues<CardExtraEffectOrbSelection>())
		{
			countOrbSelectionSelect.AddItem(CardEditorExtraEffects.OrbSelectionLabel(orbSelection));
		}
		int countOrbSelectionIndex = effect != null ? (int)effect.CountOrbSelection : (int)CardExtraEffectOrbSelection.Leftmost;
		if (countOrbSelectionIndex < 0 || countOrbSelectionIndex >= Enum.GetValues<CardExtraEffectOrbSelection>().Length)
		{
			countOrbSelectionIndex = (int)CardExtraEffectOrbSelection.Leftmost;
		}
		countOrbSelectionSelect.Select(countOrbSelectionIndex);

		orbCountFilterRow.AddChild(countOrbTypeSelect);
		orbCountFilterRow.AddChild(countOrbSelectionSelect);
		orbCountFilterRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer enemyStatusRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		enemyStatusRow.AddThemeConstantOverride("separation", 10);

		OptionButton countEnemyStatusSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(190, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(countEnemyStatusSelect);
		ConstrainOptionButtonPopup(countEnemyStatusSelect);
		countEnemyStatusSelect.TooltipText = "Choose which status or power this count event should track. When it matches the effect you're editing, this selector will auto-fill for you.";
		foreach (CardExtraEffectEnemyStatus status in Enum.GetValues<CardExtraEffectEnemyStatus>())
		{
			countEnemyStatusSelect.AddItem(CardEditorExtraEffects.EnemyStatusLabel(status));
		}
		int countEnemyStatusIndex = effect != null ? (int)effect.CountEnemyStatus : (int)CardExtraEffectEnemyStatus.AnyPowerStatus;
		if (countEnemyStatusIndex < 0 || countEnemyStatusIndex >= Enum.GetValues<CardExtraEffectEnemyStatus>().Length)
		{
			countEnemyStatusIndex = (int)CardExtraEffectEnemyStatus.AnyPowerStatus;
		}
		countEnemyStatusSelect.Select(countEnemyStatusIndex);

		enemyStatusRow.AddChild(countEnemyStatusSelect);
		enemyStatusRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer enemyIntentRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		enemyIntentRow.AddThemeConstantOverride("separation", 10);

		OptionButton countEnemyIntentSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(190, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(countEnemyIntentSelect);
		ConstrainOptionButtonPopup(countEnemyIntentSelect);
		countEnemyIntentSelect.TooltipText = "Count enemies with this current intent.";
		foreach (CardExtraEffectEnemyIntent intent in Enum.GetValues<CardExtraEffectEnemyIntent>())
		{
			countEnemyIntentSelect.AddItem(CardEditorExtraEffects.EnemyIntentLabel(intent));
		}
		int countEnemyIntentIndex = effect != null ? (int)effect.CountEnemyIntent : (int)CardExtraEffectEnemyIntent.Attack;
		if (countEnemyIntentIndex < 0 || countEnemyIntentIndex >= Enum.GetValues<CardExtraEffectEnemyIntent>().Length)
		{
			countEnemyIntentIndex = (int)CardExtraEffectEnemyIntent.Attack;
		}
		countEnemyIntentSelect.Select(countEnemyIntentIndex);

		enemyIntentRow.AddChild(countEnemyIntentSelect);
		enemyIntentRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		Control scalingBaseTickboxVisuals = tickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
		Label scalingBaseLabel = new Label { Text = "Include Base" };
		StyleBodyLabel(scalingBaseLabel);
		KeywordTickbox scalingBaseTickbox = new KeywordTickbox(scalingBaseTickboxVisuals, scalingBaseLabel, effect?.HistoryScalingIncludesBase ?? false);
		scalingBaseTickbox.TooltipText = "When Scaling is enabled, also apply the base amount even if the count is zero (base + base*count).";
		scalingBaseTickbox.Toggled += QueuePreviewUpdate;
		scalingBaseTickbox.Visible = false;

		scalingRow.AddChild(countRow);
		scalingRow.AddChild(countTurnsRow);
		scalingRow.AddChild(countWindowInclusionRow);
		scalingRow.AddChild(blockLostCountingModeRow);
		scalingRow.AddChild(countConditionRow);
		scalingRow.AddChild(filterRow);
		scalingRow.AddChild(orbCountFilterRow);
		scalingRow.AddChild(enemyStatusRow);
		scalingRow.AddChild(enemyIntentRow);
		scalingRow.AddChild(scalingBaseTickbox);

		VBoxContainer powerConditionRow = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		powerConditionRow.AddThemeConstantOverride("separation", 6);

		HBoxContainer powerTimingRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		powerTimingRow.AddThemeConstantOverride("separation", 10);

		HBoxContainer powerCountEventRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		powerCountEventRow.AddThemeConstantOverride("separation", 10);
		powerCountEventRow.Visible = false;

		Label powerCountEventLabel = new Label { Text = CardEditorLoc.T("ui.powerCountEvent", "Whenever"), CustomMinimumSize = new Vector2(120, 0) };
		StyleBodyLabel(powerCountEventLabel);

		OptionButton powerCountEventSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(240, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(powerCountEventSelect);
		ConstrainOptionButtonPopup(powerCountEventSelect);
		powerCountEventSelect.TooltipText = "Power trigger: which event should trigger this effect.";
		foreach (CardExtraEffectCountEvent ev in CardEditorExtraEffects.PowerTriggerCountEvents)
		{
			powerCountEventSelect.AddItem(CardEditorExtraEffects.CountEventLabel(ev), (int)ev);
		}
		int initialPowerCountEvent = effect != null && CardEditorExtraEffects.PowerTriggerCountEvents.Contains(effect.PowerTriggerCountEvent)
			? (int)effect.PowerTriggerCountEvent
			: (int)CardExtraEffectCountEvent.BlockLost;
		powerCountEventSelect.Select(powerCountEventSelect.GetItemIndex(initialPowerCountEvent));
		powerCountEventSelect.ItemSelected += _ => QueuePreviewUpdate();

		powerCountEventRow.AddChild(powerCountEventLabel);
		powerCountEventRow.AddChild(powerCountEventSelect);
		powerCountEventRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer powerFilterRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		powerFilterRow.AddThemeConstantOverride("separation", 10);

		OptionButton triggerPoolSelect = CreateGeneratedPoolSelect(
			effect?.TriggerCardPool ?? CardGeneratedCardPool.All,
			CardGeneratedCardPool.All,
			"Power trigger condition: only trigger when the card matches this pool (color/class).");

		OptionButton triggerTypeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(triggerTypeSelect);
		ConstrainOptionButtonPopup(triggerTypeSelect);
		triggerTypeSelect.TooltipText = "Power trigger condition: only trigger when the card matches this card type.";
		foreach (CardGeneratedCardType type in Enum.GetValues<CardGeneratedCardType>())
		{
			triggerTypeSelect.AddItem(CardEditorExtraEffects.GeneratedCardTypeLabel(type));
		}
		int triggerTypeIndex = effect != null ? (int)effect.TriggerCardType : (int)CardGeneratedCardType.Any;
		if (triggerTypeIndex < 0 || triggerTypeIndex >= Enum.GetValues<CardGeneratedCardType>().Length)
		{
			triggerTypeIndex = (int)CardGeneratedCardType.Any;
		}
		triggerTypeSelect.Select(triggerTypeIndex);

		OptionButton triggerFilterSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(triggerFilterSelect);
		ConstrainOptionButtonPopup(triggerFilterSelect);
		triggerFilterSelect.TooltipText = "Power trigger condition: only trigger when the card matches this effect filter.";
		foreach (CardExtraEffectCountCardFilter filter in Enum.GetValues<CardExtraEffectCountCardFilter>())
		{
			triggerFilterSelect.AddItem(CardEditorExtraEffects.CountCardFilterLabel(filter));
		}
		int triggerFilterIndex = effect != null ? (int)effect.TriggerCardFilter : (int)CardExtraEffectCountCardFilter.Any;
		if (triggerFilterIndex < 0 || triggerFilterIndex >= Enum.GetValues<CardExtraEffectCountCardFilter>().Length)
		{
			triggerFilterIndex = (int)CardExtraEffectCountCardFilter.Any;
		}
		triggerFilterSelect.Select(triggerFilterIndex);

		triggerPoolSelect.ItemSelected += _ => QueuePreviewUpdate();
		triggerTypeSelect.ItemSelected += _ => QueuePreviewUpdate();
		triggerFilterSelect.ItemSelected += _ => QueuePreviewUpdate();

		OptionButton drawnFromPileSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(220, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
			Visible = false
		};
		StyleInput(drawnFromPileSelect);
		ConstrainOptionButtonPopup(drawnFromPileSelect);
		drawnFromPileSelect.TooltipText = "Source pile: only apply cost reduction to cards drawn from this pile.";
		drawnFromPileSelect.AddItem("Any Pile");
		drawnFromPileSelect.AddItem("Draw Pile");
		drawnFromPileSelect.AddItem("Discard Pile");
		drawnFromPileSelect.AddItem("Exhaust Pile");
		int drawnFromPileIndex = 0;
		if (effect != null)
		{
			drawnFromPileIndex = effect.DrawnFromPile switch
			{
				CardExtraEffectCardPile.DrawPile => 1,
				CardExtraEffectCardPile.DiscardPile => 2,
				CardExtraEffectCardPile.ExhaustPile => 3,
				_ => 0
			};
		}
		drawnFromPileSelect.Select(drawnFromPileIndex);
		drawnFromPileSelect.ItemSelected += _ => QueuePreviewUpdate();

		Label everyNLabel = new Label { Text = "Every" };
		StyleBodyLabel(everyNLabel);
		int triggerEveryNValue = isUpgradeDeltaRow
			? (effect?.TriggerEveryN ?? 0)
			: Math.Max(1, effect?.TriggerEveryN ?? 1);
		NMegaLineEdit triggerEveryNField = new NMegaLineEdit
		{
			Text = triggerEveryNValue.ToString(CultureInfo.InvariantCulture),
			CustomMinimumSize = _amountFieldMinSize,
			Alignment = HorizontalAlignment.Center
		};
		triggerEveryNField.TooltipText = "Trigger this effect every N matching events. 1 = every event.";
		StyleInput(triggerEveryNField);
		triggerEveryNField.TextChanged += _ => QueuePreviewUpdate();

		Label maxFiresLabel = new Label { Text = "Uses" };
		StyleBodyLabel(maxFiresLabel);
		int triggerMaxFiresValue = isUpgradeDeltaRow
			? (effect?.TriggerMaxFires ?? 0)
			: Math.Max(0, effect?.TriggerMaxFires ?? 0);
		NMegaLineEdit triggerMaxFiresField = new NMegaLineEdit
		{
			Text = triggerMaxFiresValue.ToString(CultureInfo.InvariantCulture),
			CustomMinimumSize = _amountFieldMinSize,
			Alignment = HorizontalAlignment.Center
		};
		triggerMaxFiresField.TooltipText = "Maximum number of times this effect can trigger before it expires. 0 = unlimited.";
		StyleInput(triggerMaxFiresField);
		triggerMaxFiresField.TextChanged += _ => QueuePreviewUpdate();

		Label maxTurnsLabel = new Label { Text = "Turns" };
		StyleBodyLabel(maxTurnsLabel);
		int triggerMaxTurnsValue = isUpgradeDeltaRow
			? (effect?.TriggerMaxTurns ?? 0)
			: Math.Max(0, effect?.TriggerMaxTurns ?? 0);
		NMegaLineEdit triggerMaxTurnsField = new NMegaLineEdit
		{
			Text = triggerMaxTurnsValue.ToString(CultureInfo.InvariantCulture),
			CustomMinimumSize = _amountFieldMinSize,
			Alignment = HorizontalAlignment.Center
		};
		triggerMaxTurnsField.TooltipText = "Maximum number of turns this effect lasts. 0 = unlimited. If both Uses and Turns are set, whichever limit is reached first expires it.";
		StyleInput(triggerMaxTurnsField);
		triggerMaxTurnsField.TextChanged += _ => QueuePreviewUpdate();

		powerTimingRow.AddChild(everyNLabel);
		powerTimingRow.AddChild(triggerEveryNField);
		powerTimingRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
		powerTimingRow.AddChild(maxFiresLabel);
		powerTimingRow.AddChild(triggerMaxFiresField);
		powerTimingRow.AddChild(maxTurnsLabel);
		powerTimingRow.AddChild(triggerMaxTurnsField);
		powerFilterRow.AddChild(triggerPoolSelect);
		powerFilterRow.AddChild(triggerTypeSelect);
		powerFilterRow.AddChild(triggerFilterSelect);
		powerFilterRow.AddChild(drawnFromPileSelect);
		powerConditionRow.AddChild(powerTimingRow);
		powerConditionRow.AddChild(powerCountEventRow);
		powerConditionRow.AddChild(powerFilterRow);

		ExtraEffectRow effectRow = new ExtraEffectRow
		{
			IsUpgradeDeltaRow = isUpgradeDeltaRow,
			Container = wrapper,
			RemoveButton = remove,
			KindSelect = kindSelect,
			TriggerSelect = triggerSelect,
			TargetSelect = targetSelect,
			DurationSelect = durationSelect,
			TimingSelect = timingSelect,
			TimingRow = timingRow,
			TimingModeSelect = timingModeSelect,
			TimingBoundaryEdgeSelect = timingEdgeSelect,
			TimingBoundarySideSelect = timingSideSelect,
			TimingBoundaryOffsetSelect = timingOffsetSelect,
			TurnsField = turnsField,
			AmountXTickbox = amountXTickbox,
			AmountField = amountField,
			DisableOnUpgradeTickbox = disableOnUpgradeTickbox,
			CreatedCostRow = createdCostRow,
			CreatedCostDurationSelect = createdCostDurationSelect,
			CreatedCostTurnsField = createdCostTurnsField,
			UpgradeRow = upgradeRow,
			UpgradeVariantSelect = upgradeVariantSelect,
			UpgradePileSelect = upgradePileSelect,
			CardCostsLessRow = cardCostsLessRow,
			CardCostsLessKindSelect = cardCostsLessKindSelect,
			CardCostsLessModeSelect = cardCostsLessModeSelect,
			CardCostsLessLabel = cardCostsLessLabel,
			CardCostsLessDurationSelect = cardCostsLessDurationSelect,
			CardCostsLessTurnsField = cardCostsLessTurnsField,
			GeneratedCardRow = generatedCardRow,
			GeneratedPoolSelect = generatedPoolSelect,
			GeneratedTypeSelect = generatedTypeSelect,
			ScalingTickbox = scalingTickbox,
			PowerTickbox = powerTickbox,
			GrantTickbox = grantTickbox,
			RepeatRow = repeatRow,
			RepeatXTickbox = repeatXTickbox,
			RepeatCountField = repeatCountField,
			PowerConditionRow = powerConditionRow,
			PowerTimingRow = powerTimingRow,
			PowerCountEventRow = powerCountEventRow,
			PowerCountEventSelect = powerCountEventSelect,
			PowerFilterRow = powerFilterRow,
			TriggerCardPoolSelect = triggerPoolSelect,
			TriggerCardTypeSelect = triggerTypeSelect,
			TriggerCardFilterSelect = triggerFilterSelect,
			DrawnFromPileSelect = drawnFromPileSelect,
			TriggerEveryNField = triggerEveryNField,
			TriggerMaxFiresField = triggerMaxFiresField,
			TriggerMaxTurnsField = triggerMaxTurnsField,
			GrantRow = grantRow,
			GrantPileSelect = grantPileSelect,
			GrantModeSelect = grantModeSelect,
			GrantCountRow = grantCountRow,
			GrantCountXTickbox = grantCountXTickbox,
			GrantCountField = grantCountField,
			GrantFilterRow = grantFilterRow,
			GrantCountPoolSelect = grantCountPoolSelect,
			GrantCountTypeSelect = grantCountTypeSelect,
			GrantCountFilterSelect = grantCountFilterSelect,
			GrantDurationRow = grantDurationOuterRow,
			GrantDurationSelect = grantDurationSelect,
			GrantTurnsRow = grantTurnsRow,
			GrantTurnsField = grantTurnsField,
			EnchantmentRow = enchantmentRow,
			EnchantmentSelect = enchantmentSelect,
			EnchantmentDurationSelect = enchantmentDurationSelect,
			EnchantmentTurnsRow = enchantmentTurnsRow,
			EnchantmentTurnsField = enchantmentTurnsField,
			MoveCardsRow = moveCardsRow,
			MoveCardsRowTop = moveCardsRowTop,
			MoveCardsRowBottom = moveCardsRowBottom,
			MoveFromPileSelect = moveFromPileSelect,
			MoveSelectionModeSelect = moveSelectionModeSelect,
			MoveToPileSelect = moveToPileSelect,
			MoveToPositionSelect = moveToPositionSelect,
			CostFilterRow = costFilterRow,
			CostFilterTickbox = costFilterTickbox,
			CostFilterField = costFilterField,
			DrawCostRow = drawCostRow,
			DrawCostTickbox = drawCostTickbox,
			DrawCostField = drawCostField,
			IgnoreVariantRow = ignoreVariantRow,
			IgnoreVariantSelect = ignoreVariantSelect,
			CardGenerationVariantRow = cardGenerationVariantRow,
			CardGenerationVariantSelect = cardGenerationVariantSelect,
			TurnBoundaryRow = turnBoundaryRow,
			TurnBoundaryEdgeSelect = turnBoundaryEdgeSelect,
			TurnBoundarySideSelect = turnBoundarySideSelect,
			TurnBoundaryLocationSelect = turnBoundaryLocationSelect,
			SpecificCardRow = specificCardRow,
			SpecificCardIdField = specificCardIdField,
			OrbRow = orbRow,
			OrbActionSelect = orbActionSelect,
			OrbScopeSelect = orbScopeSelect,
			OrbTypeSelect = orbTypeSelect,
			OrbSelectionSelect = orbSelectionSelect,
			OrbFollowUpSelect = orbFollowUpSelect,
			OstyActionRow = ostyActionRow,
			OstyActionSelect = ostyActionSelect,
			GrantedKeywordRow = grantedKeywordRow,
			GrantedKeywordSelect = grantedKeywordSelect,
			MultiplyStatRow = multiplyStatRow,
			MultiplyStatSelect = multiplyStatSelect,
			KindDefinitionIndices = kindDefinitionIndices,
			ScalingToggleRow = scalingToggleRow,
			ScalingRow = scalingRow,
			CountModeSelect = countModeSelect,
			CountEventSelect = countEventSelect,
			CountWindowSelect = countWindowSelect,
			CountPileSelect = countPileSelect,
			CountTurnsRow = countTurnsRow,
			CountTurnsField = countTurnsField,
			CountWindowInclusionRow = countWindowInclusionRow,
			CountWindowInclusionSelect = countWindowInclusionSelect,
			BlockLostCountingModeRow = blockLostCountingModeRow,
			BlockLostCountingModeSelect = blockLostCountingModeSelect,
			CountConditionRow = countConditionRow,
			CountComparisonSelect = countComparisonSelect,
			CountConditionField = countConditionField,
			CountCardFilterRow = filterRow,
			CountPoolSelect = countPoolSelect,
			CountTypeSelect = countTypeSelect,
			CountFilterSelect = countFilterSelect,
			CountOrbFilterRow = orbCountFilterRow,
			CountOrbTypeSelect = countOrbTypeSelect,
			CountOrbSelectionSelect = countOrbSelectionSelect,
			CountEnemyStatusRow = enemyStatusRow,
			CountEnemyStatusSelect = countEnemyStatusSelect,
			CountEnemyIntentRow = enemyIntentRow,
			CountEnemyIntentSelect = countEnemyIntentSelect,
			ScalingBaseTickbox = scalingBaseTickbox
		};
		_extraEffectRows.Add(effectRow);

		kindSelect.ItemSelected += _ =>
		{
			ConfigureExtraEffectTargets(effectRow, desiredTarget: null);
			UpdateExtraEffectDurationEnabled(effectRow, desiredDuration: null);
			UpdateExtraEffectCustomRows(effectRow);
			ApplySuggestedCountSelectorDefaults(effectRow);
			// Reset amount to a valid default if the current value is invalid for the new Kind.
			int definitionIndex = GetSelectedExtraEffectDefinitionIndex(effectRow);
			if (definitionIndex >= 0 && definitionIndex < CardEditorExtraEffects.Definitions.Count)
			{
				CardExtraEffectDefinition newDef = CardEditorExtraEffects.Definitions[definitionIndex];
				if (int.TryParse(effectRow.AmountField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int curAmt))
				{
					if (!CardEditorExtraEffects.IsValidEffectAmount(newDef.Kind, curAmt))
					{
						effectRow.AmountField.Text = newDef.DefaultAmount.ToString(CultureInfo.InvariantCulture);
					}
				}
				else
				{
					effectRow.AmountField.Text = newDef.DefaultAmount.ToString(CultureInfo.InvariantCulture);
				}
			}
			QueuePreviewUpdate();
		};
		triggerSelect.ItemSelected += _ =>
		{
			ConfigureExtraEffectTargets(effectRow, desiredTarget: null);
			UpdateExtraEffectCustomRows(effectRow);
			UpdateExtraEffectDurationEnabled(effectRow, desiredDuration: null);
			QueuePreviewUpdate();
		};
		targetSelect.ItemSelected += _ => QueuePreviewUpdate();
		multiplyStatSelect.ItemSelected += _ => QueuePreviewUpdate();
		durationSelect.ItemSelected += _ => QueuePreviewUpdate();
		timingSelect.ItemSelected += _ =>
		{
			UpdateExtraEffectTurnsEnabled(effectRow);
			QueuePreviewUpdate();
		};
		Action updateTimingFromUnifiedControls = () =>
		{
			CardExtraEffectTiming timing = GetTimingFromUnifiedTimingControls(timingModeSelect, timingEdgeSelect, timingSideSelect, timingOffsetSelect);
			timingSelect.Select((int)timing);
			UpdateUnifiedTimingControlsVisibility(timingModeSelect, timingEdgeSelect, timingSideSelect, timingOffsetSelect);
			UpdateExtraEffectTurnsEnabled(effectRow);
			QueuePreviewUpdate();
		};
		timingModeSelect.ItemSelected += _ => updateTimingFromUnifiedControls();
		timingEdgeSelect.ItemSelected += _ => updateTimingFromUnifiedControls();
		timingSideSelect.ItemSelected += _ => updateTimingFromUnifiedControls();
		timingOffsetSelect.ItemSelected += _ => updateTimingFromUnifiedControls();
		powerTickbox.Toggled += () =>
		{
			UpdateExtraEffectCustomRows(effectRow);
			UpdateExtraEffectDurationEnabled(effectRow, desiredDuration: null);
			QueuePreviewUpdate();
		};
		repeatXTickbox.Toggled += () =>
		{
			UpdateExtraEffectCustomRows(effectRow);
			QueuePreviewUpdate();
		};
		grantTickbox.Toggled += () =>
		{
			UpdateExtraEffectCustomRows(effectRow);
			QueuePreviewUpdate();
		};
		scalingTickbox.Toggled += () =>
		{
			UpdateExtraEffectScalingRowVisibility(effectRow);
			UpdateExtraEffectCountTurnsEnabled(effectRow);
			QueuePreviewUpdate();
		};
		countModeSelect.ItemSelected += _ =>
		{
			UpdateExtraEffectScalingRowVisibility(effectRow);
			UpdateExtraEffectCountTurnsEnabled(effectRow);
			QueuePreviewUpdate();
		};
		countWindowSelect.ItemSelected += _ =>
		{
			UpdateExtraEffectCountTurnsEnabled(effectRow);
			QueuePreviewUpdate();
		};
		countEventSelect.ItemSelected += _ =>
		{
			ApplySuggestedCountSelectorDefaults(effectRow);
			UpdateExtraEffectScalingRowVisibility(effectRow);
			UpdateExtraEffectCountTurnsEnabled(effectRow);
			QueuePreviewUpdate();
		};
		countComparisonSelect.ItemSelected += _ =>
		{
			UpdateExtraEffectCountTurnsEnabled(effectRow);
			QueuePreviewUpdate();
		};
		countPoolSelect.ItemSelected += _ => QueuePreviewUpdate();
		countTypeSelect.ItemSelected += _ => QueuePreviewUpdate();
		countFilterSelect.ItemSelected += _ => QueuePreviewUpdate();
		countPileSelect.ItemSelected += _ => QueuePreviewUpdate();
		countOrbTypeSelect.ItemSelected += _ => QueuePreviewUpdate();
		countOrbSelectionSelect.ItemSelected += _ => QueuePreviewUpdate();
		countEnemyStatusSelect.ItemSelected += _ => QueuePreviewUpdate();
		countEnemyIntentSelect.ItemSelected += _ => QueuePreviewUpdate();
		createdCostDurationSelect.ItemSelected += _ =>
		{
			UpdateCreatedCardsCostTurnsEnabled(effectRow);
			QueuePreviewUpdate();
		};
		cardCostsLessModeSelect.ItemSelected += _ =>
		{
			UpdateExtraEffectCustomRows(effectRow);
			QueuePreviewUpdate();
		};
		cardCostsLessKindSelect.ItemSelected += _ =>
		{
			UpdateExtraEffectCustomRows(effectRow);
			UpdateExtraEffectDurationEnabled(effectRow, desiredDuration: null);
			QueuePreviewUpdate();
		};
		cardCostsLessDurationSelect.ItemSelected += _ =>
		{
			UpdateCardCostsLessTurnsEnabled(effectRow);
			QueuePreviewUpdate();
		};
		grantDurationSelect.ItemSelected += _ =>
		{
			UpdateExtraEffectGrantTurnsEnabled(effectRow);
			QueuePreviewUpdate();
		};
		enchantmentSelect.ItemSelected += _ => QueuePreviewUpdate();
		enchantmentDurationSelect.ItemSelected += _ =>
		{
			UpdateExtraEffectEnchantmentTurnsEnabled(effectRow);
			QueuePreviewUpdate();
		};
		grantPileSelect.ItemSelected += _ => QueuePreviewUpdate();
		grantModeSelect.ItemSelected += _ =>
		{
			UpdateExtraEffectGrantTurnsEnabled(effectRow);
			QueuePreviewUpdate();
		};
		grantCountXTickbox.Toggled += () =>
		{
			UpdateExtraEffectGrantTurnsEnabled(effectRow);
			QueuePreviewUpdate();
		};
		moveFromPileSelect.ItemSelected += _ => QueuePreviewUpdate();
		moveSelectionModeSelect.ItemSelected += _ => QueuePreviewUpdate();
		moveToPileSelect.ItemSelected += _ =>
		{
			UpdateExtraEffectMovePositionEnabled(effectRow);
			QueuePreviewUpdate();
		};
		moveToPositionSelect.ItemSelected += _ => QueuePreviewUpdate();
		drawCostTickbox.Toggled += () =>
		{
			UpdateExtraEffectCustomRows(effectRow);
			QueuePreviewUpdate();
		};
		drawCostField.TextChanged += _ =>
		{
			UpdateExtraEffectCustomRows(effectRow);
			QueuePreviewUpdate();
		};
		ignoreVariantSelect.ItemSelected += _ =>
		{
			ConfigureExtraEffectTargets(effectRow, desiredTarget: null);
			UpdateExtraEffectDurationEnabled(effectRow, desiredDuration: null);
			UpdateExtraEffectCustomRows(effectRow);
			QueuePreviewUpdate();
		};
		cardGenerationVariantSelect.ItemSelected += _ =>
		{
			ConfigureExtraEffectTargets(effectRow, desiredTarget: null);
			UpdateExtraEffectDurationEnabled(effectRow, desiredDuration: null);
			UpdateExtraEffectCustomRows(effectRow);
			QueuePreviewUpdate();
		};
		turnBoundaryEdgeSelect.ItemSelected += _ =>
		{
			UpdateExtraEffectCustomRows(effectRow);
			QueuePreviewUpdate();
		};
		turnBoundarySideSelect.ItemSelected += _ =>
		{
			UpdateExtraEffectCustomRows(effectRow);
			QueuePreviewUpdate();
		};
		turnBoundaryLocationSelect.ItemSelected += _ =>
		{
			UpdateExtraEffectCustomRows(effectRow);
			QueuePreviewUpdate();
		};
		orbActionSelect.ItemSelected += _ =>
		{
			UpdateExtraEffectCustomRows(effectRow);
			ApplySuggestedCountSelectorDefaults(effectRow);
			QueuePreviewUpdate();
		};
		orbScopeSelect.ItemSelected += _ =>
		{
			UpdateExtraEffectCustomRows(effectRow);
			QueuePreviewUpdate();
		};
		orbTypeSelect.ItemSelected += _ =>
		{
			ApplySuggestedCountSelectorDefaults(effectRow);
			QueuePreviewUpdate();
		};
		orbSelectionSelect.ItemSelected += _ => QueuePreviewUpdate();
		orbFollowUpSelect.ItemSelected += _ => QueuePreviewUpdate();
		ostyActionSelect.ItemSelected += _ =>
		{
			ConfigureExtraEffectTargets(effectRow, null);
			QueuePreviewUpdate();
		};
		generatedPoolSelect.ItemSelected += _ => QueuePreviewUpdate();
		generatedTypeSelect.ItemSelected += _ => QueuePreviewUpdate();
		upgradeVariantSelect.ItemSelected += _ =>
		{
			UpdateExtraEffectCustomRows(effectRow);
			QueuePreviewUpdate();
		};
		upgradePileSelect.ItemSelected += _ => QueuePreviewUpdate();
		remove.Pressed += () =>
		{
			if (effectRow.IsUpgradeDeltaRow)
			{
				return;
			}
			if (_extraEffectsContainer != null && GodotObject.IsInstanceValid(_extraEffectsContainer))
			{
				_extraEffectsContainer.RemoveChild(wrapper);
			}
			_extraEffectRows.Remove(effectRow);
			wrapper.QueueFreeSafely();
			QueuePreviewUpdate();
		};

		rowTop.AddChild(kindSelect);
		rowTop.AddChild(amountXTickbox);
		rowTop.AddChild(spinButtons);
		rowTop.AddChild(amountField);
		rowTop.AddChild(disableOnUpgradeTickbox);
		rowTop.AddChild(remove);

		wrapper.AddChild(rowTop);
		wrapper.AddChild(repeatRow);
		wrapper.AddChild(configRow);
		wrapper.AddChild(turnBoundaryRow);
		wrapper.AddChild(specificCardRow);
		wrapper.AddChild(moveCardsRow);
		wrapper.AddChild(drawCostRow);
		wrapper.AddChild(ignoreVariantRow);
		wrapper.AddChild(cardGenerationVariantRow);
		wrapper.AddChild(orbRow);
		wrapper.AddChild(ostyActionRow);
		wrapper.AddChild(grantedKeywordRow);
		wrapper.AddChild(multiplyStatRow);
		wrapper.AddChild(enchantmentRow);
		wrapper.AddChild(scalingToggleRow);
		wrapper.AddChild(grantRow);
		wrapper.AddChild(powerConditionRow);
		wrapper.AddChild(scalingRow);
		wrapper.AddChild(generatedCardRow);
		wrapper.AddChild(upgradeRow);
		wrapper.AddChild(createdCostRow);
		wrapper.AddChild(cardCostsLessRow);
		wrapper.AddChild(timingRow);

		_extraEffectsContainer.AddChild(wrapper);
		ConfigureExtraEffectTargets(effectRow, effect?.Target);
		UpdateExtraEffectDurationEnabled(effectRow, effect?.Duration);
		UpdateExtraEffectTurnsEnabled(effectRow);
		UpdateExtraEffectCustomRows(effectRow);
		UpdateExtraEffectScalingRowVisibility(effectRow);
		UpdateExtraEffectCountTurnsEnabled(effectRow);
		UpdateExtraEffectGrantTurnsEnabled(effectRow);
		UpdateExtraEffectEnchantmentTurnsEnabled(effectRow);
		UpdateExtraEffectMovePositionEnabled(effectRow);
		ApplyUpgradeDeltaRowLocks(effectRow);
	}

	private static void DisableOptionButton(OptionButton optionButton)
	{
		if (optionButton == null || !GodotObject.IsInstanceValid(optionButton))
		{
			return;
		}
		optionButton.Disabled = true;
		optionButton.SelfModulate = StsColors.gray;
	}

	private static void DisableLineEdit(LineEdit lineEdit)
	{
		if (lineEdit == null || !GodotObject.IsInstanceValid(lineEdit))
		{
			return;
		}
		lineEdit.Editable = false;
		lineEdit.SelfModulate = StsColors.gray;
	}

	private static void DisableTickbox(KeywordTickbox tickbox)
	{
		if (tickbox == null || !GodotObject.IsInstanceValid(tickbox))
		{
			return;
		}
		tickbox.MouseFilter = MouseFilterEnum.Ignore;
		tickbox.SelfModulate = StsColors.gray;
	}

	private static bool IsHiddenUnifiedCardCostKind(CardExtraEffectKind kind)
	{
		return kind is CardExtraEffectKind.CardStarCostsLess
			or CardExtraEffectKind.CardTypeCostsLess
			or CardExtraEffectKind.CardTypeStarCostsLess
			or CardExtraEffectKind.DrawnCardsCostLess
			or CardExtraEffectKind.GeneratedCardsCostLess;
	}

	private static bool IsHiddenUnifiedDrawKind(CardExtraEffectKind kind)
	{
		// Hide specialized draw variants; expose them through the base DrawCards effect via extra knobs.
		return kind is CardExtraEffectKind.DrawCardsThatCostLess;
	}

	private static bool IsHiddenUnifiedTurnBoundaryTrigger(CardExtraEffectTrigger trigger)
	{
		// Hide specialized turn-boundary triggers; expose them through TurnBoundary via extra knobs.
		return trigger is CardExtraEffectTrigger.EndOfTurnInHand
			or CardExtraEffectTrigger.StartOfTurn
			or CardExtraEffectTrigger.EndOfTurn
			or CardExtraEffectTrigger.StartOfEnemyTurn
			or CardExtraEffectTrigger.EndOfEnemyTurn;
	}

	private static CardExtraEffectTrigger GetVisibleExtraEffectTrigger(CardExtraEffectTrigger trigger)
	{
		return IsHiddenUnifiedTurnBoundaryTrigger(trigger) ? CardExtraEffectTrigger.TurnBoundary : trigger;
	}

	private static bool IsHiddenUnifiedCardGenerationKind(CardExtraEffectKind kind)
	{
		// Hide specialized card-generation variants; expose them through AddRandomCardToHand (renamed "Card Generation") via a variant selector.
		return kind is CardExtraEffectKind.CreatedCardsCostLess
			or CardExtraEffectKind.CreatedCardsUpgraded
			or CardExtraEffectKind.ChooseOneOfThreeCardsToHand
			or CardExtraEffectKind.AddCopyOfThisCard
			or CardExtraEffectKind.AddSpecificCardToHand
			or CardExtraEffectKind.FetchSpecificCardToHand;
	}

	private static bool IsHiddenUnifiedIgnoreKind(CardExtraEffectKind kind)
	{
		// Hide specialized ignore variants; expose them through IgnoreBlock (renamed "Ignore Effect") via a variant selector.
		return kind is CardExtraEffectKind.IgnoreDamageModifiers
			or CardExtraEffectKind.IgnoreDamageCaps
			or CardExtraEffectKind.IgnoreDamageNegation
			or CardExtraEffectKind.IgnoreEnemyDamageReductions;
	}

	private static bool IsAmountlessExtraEffectKind(CardExtraEffectKind kind)
	{
		return kind == CardExtraEffectKind.EndTurn;
	}

	private void UpdateExtraEffectAmountControls(ExtraEffectRow row, CardExtraEffectDefinition definition, CardExtraEffectKind kind)
	{
		if (row?.AmountField == null || !GodotObject.IsInstanceValid(row.AmountField))
		{
			return;
		}

		Control? spinButtons = null;
		Node parent = row.AmountField.GetParent();
		if (parent != null)
		{
			int amountFieldIndex = row.AmountField.GetIndex();
			if (amountFieldIndex > 0)
			{
				spinButtons = parent.GetChildOrNull<Control>(amountFieldIndex - 1);
			}
		}

		bool hideAmountControls = IsAmountlessExtraEffectKind(kind);
		if (hideAmountControls)
		{
			row.AmountField.Text = definition.DefaultAmount.ToString(CultureInfo.InvariantCulture);
			row.AmountField.Visible = false;
			row.AmountField.Editable = false;
			row.AmountField.SelfModulate = StsColors.gray;
			SetSpinEnabled(row.AmountField, enabled: false);
			if (spinButtons != null && GodotObject.IsInstanceValid(spinButtons))
			{
				spinButtons.Visible = false;
			}
			if (row.AmountXTickbox != null && GodotObject.IsInstanceValid(row.AmountXTickbox))
			{
				row.AmountXTickbox.Visible = false;
			}
			return;
		}

		row.AmountField.Visible = true;
		row.AmountField.SelfModulate = Colors.White;
		if (spinButtons != null && GodotObject.IsInstanceValid(spinButtons))
		{
			spinButtons.Visible = true;
		}
		if (row.AmountXTickbox != null && GodotObject.IsInstanceValid(row.AmountXTickbox))
		{
			row.AmountXTickbox.Visible = !_isUpgradeEditor;
		}
		ApplyEffectXPlusUiState(row.AmountField, row.AmountXTickbox, metaKeyPreviousNonXText: "card_editor_prev_extra_effect_amount_nonx", metaKeyPreviousXPlusText: "card_editor_prev_extra_effect_amount_xplus");
	}

	private static bool IsHiddenUnifiedUpgradeKind(CardExtraEffectKind kind)
	{
		return kind is CardExtraEffectKind.GeneratedCardsUpgraded
			or CardExtraEffectKind.CardsInPileUpgradedAura;
	}

	private static CardExtraEffectKind GetVisibleExtraEffectKind(CardExtraEffectKind kind)
	{
		return kind switch
		{
			CardExtraEffectKind.CreatedCardsCostLess
			or CardExtraEffectKind.CreatedCardsUpgraded
			or CardExtraEffectKind.GeneratedCardsUpgraded
			or CardExtraEffectKind.CardsInPileUpgradedAura
			or CardExtraEffectKind.ChooseOneOfThreeCardsToHand
			or CardExtraEffectKind.AddCopyOfThisCard
			or CardExtraEffectKind.AddSpecificCardToHand
			or CardExtraEffectKind.FetchSpecificCardToHand => CardExtraEffectKind.AddRandomCardToHand,
			CardExtraEffectKind.DrawCardsThatCostLess => CardExtraEffectKind.DrawCards,
			CardExtraEffectKind.IgnoreDamageModifiers
			or CardExtraEffectKind.IgnoreDamageCaps
			or CardExtraEffectKind.IgnoreDamageNegation
			or CardExtraEffectKind.IgnoreEnemyDamageReductions => CardExtraEffectKind.IgnoreBlock,
			CardExtraEffectKind.CardStarCostsLess
			or CardExtraEffectKind.CardTypeCostsLess
			or CardExtraEffectKind.CardTypeStarCostsLess
			or CardExtraEffectKind.DrawnCardsCostLess
			or CardExtraEffectKind.GeneratedCardsCostLess => CardExtraEffectKind.CardCostsLess,
			CardExtraEffectKind.EvokeOrbs => CardExtraEffectKind.OrbAction,
			_ => kind
		};
	}

	private static UnifiedCardCostEffectVariant GetUnifiedCardCostEffectVariant(CardExtraEffectKind kind)
	{
		return kind switch
		{
			CardExtraEffectKind.CardStarCostsLess => UnifiedCardCostEffectVariant.ThisCardStars,
			CardExtraEffectKind.CardTypeCostsLess => UnifiedCardCostEffectVariant.MatchingCardsEnergy,
			CardExtraEffectKind.CardTypeStarCostsLess => UnifiedCardCostEffectVariant.MatchingCardsStars,
			CardExtraEffectKind.DrawnCardsCostLess => UnifiedCardCostEffectVariant.DrawnCards,
			CardExtraEffectKind.GeneratedCardsCostLess => UnifiedCardCostEffectVariant.CreatedCards,
			_ => UnifiedCardCostEffectVariant.ThisCardEnergy
		};
	}

	private static CardExtraEffectKind GetUnifiedCardCostEffectKind(UnifiedCardCostEffectVariant variant)
	{
		return variant switch
		{
			UnifiedCardCostEffectVariant.ThisCardStars => CardExtraEffectKind.CardStarCostsLess,
			UnifiedCardCostEffectVariant.MatchingCardsEnergy => CardExtraEffectKind.CardTypeCostsLess,
			UnifiedCardCostEffectVariant.MatchingCardsStars => CardExtraEffectKind.CardTypeStarCostsLess,
			UnifiedCardCostEffectVariant.DrawnCards => CardExtraEffectKind.DrawnCardsCostLess,
			UnifiedCardCostEffectVariant.CreatedCards => CardExtraEffectKind.GeneratedCardsCostLess,
			_ => CardExtraEffectKind.CardCostsLess
		};
	}

	private static UnifiedUpgradeEffectVariant GetUnifiedUpgradeEffectVariant(CardExtraEffectKind kind)
	{
		return kind switch
		{
			CardExtraEffectKind.GeneratedCardsUpgraded => UnifiedUpgradeEffectVariant.CreatedCardsAura,
			CardExtraEffectKind.CardsInPileUpgradedAura => UnifiedUpgradeEffectVariant.CardsInPilesAura,
			_ => UnifiedUpgradeEffectVariant.CreatedByThisCard
		};
	}

	private static CardExtraEffectKind GetUnifiedUpgradeEffectKind(UnifiedUpgradeEffectVariant variant)
	{
		return variant switch
		{
			UnifiedUpgradeEffectVariant.CreatedCardsAura => CardExtraEffectKind.GeneratedCardsUpgraded,
			UnifiedUpgradeEffectVariant.CardsInPilesAura => CardExtraEffectKind.CardsInPileUpgradedAura,
			_ => CardExtraEffectKind.CreatedCardsUpgraded
		};
	}

	private static bool IsSelfCardCostModifierKind(CardExtraEffectKind kind)
	{
		return kind is CardExtraEffectKind.CardCostsLess or CardExtraEffectKind.CardStarCostsLess;
	}

	private static bool IsCardTypeCostModifierKind(CardExtraEffectKind kind)
	{
		return kind is CardExtraEffectKind.CardTypeCostsLess or CardExtraEffectKind.CardTypeStarCostsLess;
	}

	private static bool IsDrawnGeneratedCostModifierKind(CardExtraEffectKind kind)
	{
		return kind is CardExtraEffectKind.DrawnCardsCostLess or CardExtraEffectKind.GeneratedCardsCostLess;
	}

	private static bool IsUnifiedCardCostModifierKind(CardExtraEffectKind kind)
	{
		return IsSelfCardCostModifierKind(kind)
			|| IsCardTypeCostModifierKind(kind)
			|| IsDrawnGeneratedCostModifierKind(kind);
	}

	private static CardExtraEffectKind GetResolvedExtraEffectKind(ExtraEffectRow row, CardExtraEffectKind kind)
	{
		if (kind == CardExtraEffectKind.AddRandomCardToHand
			&& row?.CardGenerationVariantSelect != null
			&& GodotObject.IsInstanceValid(row.CardGenerationVariantSelect))
		{
			int generationSelected = row.CardGenerationVariantSelect.Selected;
			if (generationSelected < 0 || generationSelected >= Enum.GetValues<UnifiedCardGenerationVariant>().Length)
			{
				generationSelected = 0;
			}

			kind = (UnifiedCardGenerationVariant)generationSelected switch
			{
				UnifiedCardGenerationVariant.ChooseOneOfThree => CardExtraEffectKind.ChooseOneOfThreeCardsToHand,
				UnifiedCardGenerationVariant.CopyOfThisCard => CardExtraEffectKind.AddCopyOfThisCard,
				UnifiedCardGenerationVariant.AddSpecificCard => CardExtraEffectKind.AddSpecificCardToHand,
				UnifiedCardGenerationVariant.FetchSpecificCard => CardExtraEffectKind.FetchSpecificCardToHand,
				UnifiedCardGenerationVariant.CreatedCardsCostLess => CardExtraEffectKind.CreatedCardsCostLess,
				UnifiedCardGenerationVariant.CreatedCardsUpgraded => CardExtraEffectKind.CreatedCardsUpgraded,
				_ => CardExtraEffectKind.AddRandomCardToHand
			};
		}

		if (kind == CardExtraEffectKind.DrawCards
			&& row?.DrawCostTickbox != null
			&& GodotObject.IsInstanceValid(row.DrawCostTickbox)
			&& row.DrawCostTickbox.IsTicked)
		{
			return CardExtraEffectKind.DrawCardsThatCostLess;
		}

		if (kind == CardExtraEffectKind.IgnoreBlock
			&& row?.IgnoreVariantSelect != null
			&& GodotObject.IsInstanceValid(row.IgnoreVariantSelect))
		{
			int ignoreSelected = row.IgnoreVariantSelect.Selected;
			return ignoreSelected switch
			{
				1 => CardExtraEffectKind.IgnoreDamageModifiers,
				2 => CardExtraEffectKind.IgnoreDamageCaps,
				3 => CardExtraEffectKind.IgnoreDamageNegation,
				4 => CardExtraEffectKind.IgnoreEnemyDamageReductions,
				_ => CardExtraEffectKind.IgnoreBlock
			};
		}

		if (kind == CardExtraEffectKind.CreatedCardsUpgraded
			&& row?.UpgradeVariantSelect != null
			&& GodotObject.IsInstanceValid(row.UpgradeVariantSelect))
		{
			int upgradeSelected = row.UpgradeVariantSelect.Selected;
			if (upgradeSelected < 0 || upgradeSelected >= Enum.GetValues<UnifiedUpgradeEffectVariant>().Length)
			{
				upgradeSelected = 0;
			}

			return GetUnifiedUpgradeEffectKind((UnifiedUpgradeEffectVariant)upgradeSelected);
		}

		if (kind != CardExtraEffectKind.CardCostsLess
			|| row?.CardCostsLessKindSelect == null
			|| !GodotObject.IsInstanceValid(row.CardCostsLessKindSelect))
		{
			return kind;
		}

		int selected = row.CardCostsLessKindSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<UnifiedCardCostEffectVariant>().Length)
		{
			selected = 0;
		}

		return GetUnifiedCardCostEffectKind((UnifiedCardCostEffectVariant)selected);
	}

	private void ApplyUpgradeDeltaRowLocks(ExtraEffectRow row)
	{
		if (row == null || !row.IsUpgradeDeltaRow)
		{
			return;
		}

		DisableOptionButton(row.KindSelect);
		DisableOptionButton(row.TriggerSelect);
		DisableOptionButton(row.TargetSelect);
		DisableOptionButton(row.DurationSelect);
		DisableOptionButton(row.TimingSelect);
		DisableOptionButton(row.TimingModeSelect);
		DisableOptionButton(row.TimingBoundaryEdgeSelect);
		DisableOptionButton(row.TimingBoundarySideSelect);
		DisableOptionButton(row.TimingBoundaryOffsetSelect);

		DisableTickbox(row.AmountXTickbox);
		DisableTickbox(row.GrantTickbox);
		DisableTickbox(row.PowerTickbox);
		DisableTickbox(row.ScalingTickbox);
		DisableTickbox(row.ScalingBaseTickbox);

		DisableOptionButton(row.UpgradeVariantSelect);
		DisableOptionButton(row.UpgradePileSelect);

		DisableOptionButton(row.TriggerCardPoolSelect);
		DisableOptionButton(row.TriggerCardTypeSelect);
		DisableOptionButton(row.TriggerCardFilterSelect);

		DisableOptionButton(row.GrantPileSelect);
		DisableOptionButton(row.GrantModeSelect);
		DisableTickbox(row.GrantCountXTickbox);
		DisableOptionButton(row.GrantCountPoolSelect);
		DisableOptionButton(row.GrantCountTypeSelect);
		DisableOptionButton(row.GrantCountFilterSelect);
		DisableOptionButton(row.GrantDurationSelect);
		DisableOptionButton(row.EnchantmentSelect);
		DisableOptionButton(row.EnchantmentDurationSelect);

		DisableTickbox(row.RepeatXTickbox);

		DisableOptionButton(row.MoveFromPileSelect);
		DisableOptionButton(row.MoveSelectionModeSelect);
		DisableOptionButton(row.MoveToPileSelect);
		DisableOptionButton(row.MoveToPositionSelect);
		DisableTickbox(row.DrawCostTickbox);
		DisableLineEdit(row.DrawCostField);
		DisableOptionButton(row.IgnoreVariantSelect);
		DisableOptionButton(row.CardGenerationVariantSelect);
		DisableOptionButton(row.TurnBoundaryEdgeSelect);
		DisableOptionButton(row.TurnBoundarySideSelect);
		DisableOptionButton(row.TurnBoundaryLocationSelect);
		DisableOptionButton(row.OrbActionSelect);
		DisableOptionButton(row.OrbTypeSelect);
		DisableOptionButton(row.OrbSelectionSelect);
		DisableOptionButton(row.OrbFollowUpSelect);
		DisableOptionButton(row.MultiplyStatSelect);

		DisableLineEdit(row.SpecificCardIdField);
		DisableLineEdit(row.EnchantmentTurnsField);

		DisableOptionButton(row.CreatedCostDurationSelect);

		DisableOptionButton(row.CardCostsLessKindSelect);
		DisableOptionButton(row.CardCostsLessModeSelect);
		DisableOptionButton(row.CardCostsLessDurationSelect);

		DisableOptionButton(row.GeneratedPoolSelect);
		DisableOptionButton(row.GeneratedTypeSelect);

		DisableOptionButton(row.CountEventSelect);
		DisableOptionButton(row.CountModeSelect);
		DisableOptionButton(row.CountWindowSelect);
		DisableOptionButton(row.CountWindowInclusionSelect);
		DisableOptionButton(row.PowerCountEventSelect);
		DisableOptionButton(row.BlockLostCountingModeSelect);
		DisableOptionButton(row.CountPileSelect);
		DisableOptionButton(row.CountComparisonSelect);
		DisableOptionButton(row.CountPoolSelect);
		DisableOptionButton(row.CountTypeSelect);
		DisableOptionButton(row.CountFilterSelect);
		DisableOptionButton(row.CountOrbTypeSelect);
		DisableOptionButton(row.CountOrbSelectionSelect);

		if (row.RemoveButton != null && GodotObject.IsInstanceValid(row.RemoveButton))
		{
			row.RemoveButton.Disabled = true;
			row.RemoveButton.SelfModulate = StsColors.gray;
		}
	}

	private void UpdateExtraEffectCustomRows(ExtraEffectRow row)
	{
		int kindIndex = GetSelectedExtraEffectDefinitionIndex(row);
		CardExtraEffectDefinition definition = CardEditorExtraEffects.Definitions[kindIndex];
		CardExtraEffectKind baseKind = definition.Kind;
		CardExtraEffectKind kind = GetResolvedExtraEffectKind(row, baseKind);
		CardExtraEffectTrigger trigger = GetSelectedTrigger(row);
		bool usesUnifiedCardCostSelector = baseKind == CardExtraEffectKind.CardCostsLess;
		bool usesUnifiedUpgradeSelector = baseKind == CardExtraEffectKind.CreatedCardsUpgraded
			|| kind is CardExtraEffectKind.CreatedCardsUpgraded
				or CardExtraEffectKind.GeneratedCardsUpgraded
				or CardExtraEffectKind.CardsInPileUpgradedAura;
		bool supportsAsPower = CardEditorExtraEffects.SupportsAsPower(kind) && trigger != CardExtraEffectTrigger.Fatal;
		row.PowerTickbox.Visible = supportsAsPower;
		bool asPower = supportsAsPower && row.PowerTickbox.IsTicked;
		UpdateExtraEffectAmountControls(row, definition, kind);

		if (!asPower && trigger == CardExtraEffectTrigger.OnCountEvent)
		{
			int onPlayIndex = row.TriggerSelect.GetItemIndex((int)CardExtraEffectTrigger.OnPlay);
			if (onPlayIndex >= 0)
			{
				row.TriggerSelect.Select(onPlayIndex);
			}
			trigger = CardExtraEffectTrigger.OnPlay;
		}

		bool isUpgradeUnifiedKind = kind is CardExtraEffectKind.CreatedCardsUpgraded
			or CardExtraEffectKind.GeneratedCardsUpgraded
			or CardExtraEffectKind.CardsInPileUpgradedAura;
		bool isUpgradeAura = kind is CardExtraEffectKind.GeneratedCardsUpgraded or CardExtraEffectKind.CardsInPileUpgradedAura or CardExtraEffectKind.UpgradeCardsInPile;

		row.UpgradeRow.Visible = usesUnifiedUpgradeSelector;
		if (row.UpgradeVariantSelect != null && GodotObject.IsInstanceValid(row.UpgradeVariantSelect))
		{
			row.UpgradeVariantSelect.Visible = row.UpgradeRow.Visible;
		}
		if (row.UpgradePileSelect != null && GodotObject.IsInstanceValid(row.UpgradePileSelect))
		{
			row.UpgradePileSelect.Visible = row.UpgradeRow.Visible && kind == CardExtraEffectKind.CardsInPileUpgradedAura;
		}

		bool isCreatedCardModifier = kind is CardExtraEffectKind.CreatedCardsCostLess
			or CardExtraEffectKind.CreatedCardsUpgraded
			or CardExtraEffectKind.GeneratedCardsUpgraded
			or CardExtraEffectKind.CardsInPileUpgradedAura;
		bool isCreatedCost = kind == CardExtraEffectKind.CreatedCardsCostLess;
		bool isGeneratedCard = kind is CardExtraEffectKind.AddRandomCardToHand or CardExtraEffectKind.ChooseOneOfThreeCardsToHand;
		bool isMoveCards = kind == CardExtraEffectKind.MoveCardsBetweenPiles;
		bool isUpgradeCardsInPile = kind == CardExtraEffectKind.UpgradeCardsInPile;
		bool isCopyThisCard = kind == CardExtraEffectKind.AddCopyOfThisCard;
		bool isPlayFromPile = kind == CardExtraEffectKind.PlayCardFromPile;
		bool isAutoPlaySelfFromPile = kind == CardExtraEffectKind.AutoPlaySelfFromPile;
		bool isAutoDrawSelfFromPile = kind == CardExtraEffectKind.AutoDrawSelfFromPile;
		bool isConditionalAutoFromPile = kind is CardExtraEffectKind.ConditionalAutoPlayFromPile or CardExtraEffectKind.ConditionalAutoDrawFromPile;
		bool isDrawCardsThatCostLess = kind == CardExtraEffectKind.DrawCardsThatCostLess;
		bool isSpecificCard = kind is CardExtraEffectKind.AddSpecificCardToHand or CardExtraEffectKind.FetchSpecificCardToHand;
		bool isOrbAction = kind == CardExtraEffectKind.OrbAction;
		bool isOstyAction = kind == CardExtraEffectKind.OstyAction;
		bool isMultiplyStatStatus = kind == CardExtraEffectKind.MultiplyStatStatus;
		bool isEnchantCard = kind == CardExtraEffectKind.EnchantCard;
		bool isDiscardCards = kind == CardExtraEffectKind.DiscardCards;
		bool isExhaustCards = kind == CardExtraEffectKind.ExhaustCards;
		bool isDrawCards = baseKind == CardExtraEffectKind.DrawCards;
		bool isIgnoreEffects = baseKind == CardExtraEffectKind.IgnoreBlock;
		bool isCardGeneration = baseKind == CardExtraEffectKind.AddRandomCardToHand;
		bool isGrantKeywordToPile = kind == CardExtraEffectKind.GrantKeywordToPile;
		bool isUpgradeDeckCards = kind == CardExtraEffectKind.UpgradeDeckCards;
		bool isAmountlessEffect = IsAmountlessExtraEffectKind(kind);
		bool isSelfCardCostsLess = IsSelfCardCostModifierKind(kind);
		bool isCardTypeCostAura = IsCardTypeCostModifierKind(kind);
		bool isDrawnGeneratedCost = IsDrawnGeneratedCostModifierKind(kind);
		bool isAnyCostChange = isSelfCardCostsLess || isCardTypeCostAura || isDrawnGeneratedCost || isDrawCardsThatCostLess;

		bool canGrantToCard = !isCreatedCardModifier
			&& !isMoveCards
			&& !isUpgradeCardsInPile
			&& !isPlayFromPile
			&& !isAutoPlaySelfFromPile
			&& !isAutoDrawSelfFromPile
			&& !isConditionalAutoFromPile
			&& !isDrawCardsThatCostLess
			&& !isDiscardCards
			&& !isExhaustCards
			&& !isAmountlessEffect
			&& !isGrantKeywordToPile
			&& !isUpgradeDeckCards;
		row.GrantTickbox.Visible = canGrantToCard;
		if (!canGrantToCard)
		{
			row.GrantRow.Visible = false;
		}

		bool grantToCard = row.GrantTickbox.Visible && row.GrantTickbox.IsTicked;

		bool showRepeat = CardEditorExtraEffects.SupportsRepeat(kind) && !grantToCard;
		row.RepeatRow.Visible = showRepeat;
		if (row.RepeatCountField != null && GodotObject.IsInstanceValid(row.RepeatCountField))
		{
			bool repeatUsesX = showRepeat && row.RepeatXTickbox != null && GodotObject.IsInstanceValid(row.RepeatXTickbox) && row.RepeatXTickbox.IsTicked;
			bool enableRepeatCount = showRepeat && !repeatUsesX;
			row.RepeatCountField.Editable = enableRepeatCount;
			row.RepeatCountField.SelfModulate = enableRepeatCount ? Colors.White : StsColors.gray;
			SetSpinEnabled(row.RepeatCountField, enableRepeatCount);
		}

		UpdateExtraEffectTriggerLabelTexts(row, asPower);

		bool isOnPlayTrigger = trigger == CardExtraEffectTrigger.OnPlay;
		bool isTimedTrigger = trigger is CardExtraEffectTrigger.TurnBoundary
			or CardExtraEffectTrigger.StartOfTurn or CardExtraEffectTrigger.EndOfTurn or CardExtraEffectTrigger.EndOfTurnInHand
			or CardExtraEffectTrigger.StartOfEnemyTurn or CardExtraEffectTrigger.EndOfEnemyTurn;
		bool isOrbTrigger = trigger is CardExtraEffectTrigger.OnChannel or CardExtraEffectTrigger.OnEvoke;
		bool isCountEventTrigger = trigger == CardExtraEffectTrigger.OnCountEvent;
		bool showAffectedCardFilters = isCardTypeCostAura || isDrawnGeneratedCost || isUpgradeUnifiedKind;
		row.PowerConditionRow.Visible = asPower || isTimedTrigger || showAffectedCardFilters;
		row.PowerTimingRow.Visible = asPower || isTimedTrigger;
		row.PowerCountEventRow.Visible = asPower && isCountEventTrigger;
		row.PowerFilterRow.Visible = ((asPower && !isTimedTrigger && !isOrbTrigger && !isCountEventTrigger) || showAffectedCardFilters) && !isCountEventTrigger;
		if (isCardTypeCostAura || isDrawnGeneratedCost)
		{
			row.TriggerCardPoolSelect.TooltipText = "Affected cards: only apply to cards from this pool.";
			row.TriggerCardTypeSelect.TooltipText = "Affected cards: only apply to cards of this type.";
			row.TriggerCardFilterSelect.Visible = false;
			row.DrawnFromPileSelect.Visible = kind == CardExtraEffectKind.DrawnCardsCostLess;
		}
		else if (isUpgradeUnifiedKind)
		{
			row.TriggerCardPoolSelect.TooltipText = "Affected cards: only apply to cards from this pool.";
			row.TriggerCardTypeSelect.TooltipText = "Affected cards: only apply to cards of this type.";
			row.TriggerCardFilterSelect.TooltipText = "Affected cards: only apply to cards matching this effect filter.";
			row.TriggerCardFilterSelect.Visible = true;
			row.DrawnFromPileSelect.Visible = false;
		}
		else
		{
			row.TriggerCardFilterSelect.Visible = true;
			row.DrawnFromPileSelect.Visible = false;
		}

		bool isCardCostsLessPassive = isSelfCardCostsLess
			&& row.CardCostsLessModeSelect != null
			&& GodotObject.IsInstanceValid(row.CardCostsLessModeSelect)
			&& row.CardCostsLessModeSelect.Selected == 0;

		if (isCreatedCardModifier)
		{
			int onPlayIndex = row.TriggerSelect.GetItemIndex((int)CardExtraEffectTrigger.OnPlay);
			if (onPlayIndex >= 0)
			{
				row.TriggerSelect.Select(onPlayIndex);
			}
		}
		if (isSelfCardCostsLess && isCardCostsLessPassive)
		{
			int onPlayIndex = row.TriggerSelect.GetItemIndex((int)CardExtraEffectTrigger.OnPlay);
			if (onPlayIndex >= 0)
			{
				row.TriggerSelect.Select(onPlayIndex);
			}
			trigger = CardExtraEffectTrigger.OnPlay;
			isOnPlayTrigger = true;
		}
		bool disableTrigger = isCreatedCardModifier || (isSelfCardCostsLess && isCardCostsLessPassive);
		row.TriggerSelect.Disabled = disableTrigger;
		row.TriggerSelect.SelfModulate = disableTrigger ? StsColors.gray : Colors.White;

		row.CreatedCostRow.Visible = isCreatedCost;
		row.CardCostsLessRow.Visible = isAnyCostChange || isUpgradeAura;
		if (isUpgradeAura
			&& row.CardCostsLessDurationSelect != null
			&& GodotObject.IsInstanceValid(row.CardCostsLessDurationSelect)
			&& row.CardCostsLessTurnsField != null
			&& GodotObject.IsInstanceValid(row.CardCostsLessTurnsField))
		{
			if (kind == CardExtraEffectKind.UpgradeCardsInPile)
			{
				row.CardCostsLessDurationSelect.TooltipText = "How long the upgrade lasts";
				row.CardCostsLessTurnsField.TooltipText = "How many turns the upgrade lasts (when Duration = X Turns)";
			}
			else
			{
				row.CardCostsLessDurationSelect.TooltipText = "How long the upgrade aura lasts (in combat)";
				row.CardCostsLessTurnsField.TooltipText = "How many turns the upgrade aura lasts (when Duration = X Turns)";
			}
		}
		if (row.CardCostsLessKindSelect != null && GodotObject.IsInstanceValid(row.CardCostsLessKindSelect))
		{
			row.CardCostsLessKindSelect.Visible = row.CardCostsLessRow.Visible && usesUnifiedCardCostSelector;
		}
		if (row.CardCostsLessModeSelect != null && GodotObject.IsInstanceValid(row.CardCostsLessModeSelect))
		{
			row.CardCostsLessModeSelect.Visible = row.CardCostsLessRow.Visible && usesUnifiedCardCostSelector && isSelfCardCostsLess;
		}
		if (row.CardCostsLessLabel != null && GodotObject.IsInstanceValid(row.CardCostsLessLabel))
		{
			row.CardCostsLessLabel.Visible = row.CardCostsLessRow.Visible;
		}
		row.GeneratedCardRow.Visible = isGeneratedCard || kind == CardExtraEffectKind.GeneratedCardsCostLess;
		row.SpecificCardRow.Visible = isSpecificCard;
		row.OrbRow.Visible = isOrbAction;
		if (row.OstyActionRow != null && GodotObject.IsInstanceValid(row.OstyActionRow))
		{
			row.OstyActionRow.Visible = isOstyAction;
		}
		if (row.GrantedKeywordRow != null && GodotObject.IsInstanceValid(row.GrantedKeywordRow))
		{
			row.GrantedKeywordRow.Visible = kind == CardExtraEffectKind.GrantKeywordToPile;
		}
		row.MultiplyStatRow.Visible = isMultiplyStatStatus;
		row.EnchantmentRow.Visible = isEnchantCard;
		if (row.OrbScopeSelect != null && GodotObject.IsInstanceValid(row.OrbScopeSelect))
		{
			CardExtraEffectOrbAction orbActionForScope = isOrbAction ? GetSelectedOrbAction(row) : CardExtraEffectOrbAction.Evoke;
			bool isSlotAction = orbActionForScope is CardExtraEffectOrbAction.AddSlots or CardExtraEffectOrbAction.RemoveSlots;
			row.OrbScopeSelect.Visible = isOrbAction && !isSlotAction && orbActionForScope != CardExtraEffectOrbAction.Channel;
		}
		if (row.OrbTypeSelect != null && GodotObject.IsInstanceValid(row.OrbTypeSelect))
		{
			CardExtraEffectOrbAction orbActionForType = isOrbAction ? GetSelectedOrbAction(row) : CardExtraEffectOrbAction.Evoke;
			row.OrbTypeSelect.Visible = isOrbAction && orbActionForType is not (CardExtraEffectOrbAction.AddSlots or CardExtraEffectOrbAction.RemoveSlots);
		}
		if (row.OrbSelectionSelect != null && GodotObject.IsInstanceValid(row.OrbSelectionSelect))
		{
			CardExtraEffectOrbAction orbActionForSel = isOrbAction ? GetSelectedOrbAction(row) : CardExtraEffectOrbAction.Evoke;
			bool isSlotActionForSel = orbActionForSel is CardExtraEffectOrbAction.AddSlots or CardExtraEffectOrbAction.RemoveSlots;
			bool scopeIsAll = isOrbAction && !isSlotActionForSel && GetSelectedOrbScope(row) == CardExtraEffectOrbScope.All;
			row.OrbSelectionSelect.Visible = isOrbAction && !isSlotActionForSel && orbActionForSel != CardExtraEffectOrbAction.Channel && !scopeIsAll;
		}
		if (row.OrbFollowUpSelect != null && GodotObject.IsInstanceValid(row.OrbFollowUpSelect))
		{
			row.OrbFollowUpSelect.Visible = isOrbAction && GetSelectedOrbAction(row) == CardExtraEffectOrbAction.Evoke;
		}
		bool allowTiming = !isCreatedCardModifier && !isAnyCostChange && !isTimedTrigger;
		bool showTiming = allowTiming && (isOnPlayTrigger || _advancedMode) && (!asPower || _advancedMode);
		row.TimingRow.Visible = showTiming;

		row.MoveCardsRow.Visible = isMoveCards || isUpgradeCardsInPile || isCopyThisCard || isPlayFromPile || isAutoPlaySelfFromPile || isAutoDrawSelfFromPile || isConditionalAutoFromPile || isSpecificCard || isDiscardCards || isExhaustCards || isGrantKeywordToPile || isUpgradeDeckCards;
		if (row.MoveCardsRow.Visible)
		{
			// MoveCardsBetweenPiles: show both rows.
			// UpgradeCardsInPile: only needs the "from" selectors + selection mode.
			// AddCopyOfThisCard: only needs the "to" selectors.
			// PlayCardFromPile: only needs the "from" selectors + selection mode.
			// AutoPlaySelfFromPile / AutoDrawSelfFromPile: only needs the "from" pile (which pile this card must be in).
			// GrantKeywordToPile: needs from pile + selection mode.
			// UpgradeDeckCards: needs only selection mode (deck is always the pile).
			row.MoveCardsRowTop.Visible = isMoveCards || isUpgradeCardsInPile || isPlayFromPile || isAutoPlaySelfFromPile || isAutoDrawSelfFromPile || isConditionalAutoFromPile || isDiscardCards || isExhaustCards || isGrantKeywordToPile || isUpgradeDeckCards;
			row.MoveCardsRowBottom.Visible = isMoveCards || isCopyThisCard || isSpecificCard;

			row.MoveFromPileSelect.Visible = isMoveCards || isUpgradeCardsInPile || isPlayFromPile || isAutoPlaySelfFromPile || isAutoDrawSelfFromPile || isConditionalAutoFromPile || isDiscardCards || isExhaustCards || isGrantKeywordToPile;
			row.MoveSelectionModeSelect.Visible = isMoveCards || isUpgradeCardsInPile || isPlayFromPile || isDiscardCards || isExhaustCards || isGrantKeywordToPile || isUpgradeDeckCards || (kind == CardExtraEffectKind.FetchSpecificCardToHand);
			row.MoveToPileSelect.Visible = isMoveCards || isCopyThisCard || isSpecificCard;
			row.MoveToPositionSelect.Visible = isMoveCards || isCopyThisCard || isSpecificCard;
		}

		if (row.MoveCardsRow.Visible)
		{
			const string metaSpecificToHand = "card_editor_default_specific_to_hand";
			if (isSpecificCard
				&& row.MoveToPileSelect != null
				&& GodotObject.IsInstanceValid(row.MoveToPileSelect)
				&& (row.SpecificCardIdField == null || string.IsNullOrWhiteSpace(row.SpecificCardIdField.Text))
				&& !row.MoveToPileSelect.HasMeta(metaSpecificToHand))
			{
				row.MoveToPileSelect.Select((int)CardExtraEffectCardPile.Hand);
				row.MoveToPileSelect.SetMeta(metaSpecificToHand, true);
			}

			// Keep the user's selection stable. Previously this forced defaults (e.g. Play-from-pile ? Hand),
			// which overwrote loaded values on open and caused the UI to disagree with the saved effect.
			if (row.MoveFromPileSelect != null && GodotObject.IsInstanceValid(row.MoveFromPileSelect))
			{
				row.MoveFromPileSelect.Disabled = false;
				row.MoveFromPileSelect.SelfModulate = Colors.White;
			}
		}

		if (row.CostFilterRow != null && GodotObject.IsInstanceValid(row.CostFilterRow))
		{
			row.CostFilterRow.Visible = isMoveCards || isPlayFromPile || isDiscardCards || isExhaustCards;
		}

		if (row.DrawCostRow != null && GodotObject.IsInstanceValid(row.DrawCostRow))
		{
			row.DrawCostRow.Visible = isDrawCards;
		}

		if (row.IgnoreVariantRow != null && GodotObject.IsInstanceValid(row.IgnoreVariantRow))
		{
			row.IgnoreVariantRow.Visible = isIgnoreEffects;
		}

		if (row.CardGenerationVariantRow != null && GodotObject.IsInstanceValid(row.CardGenerationVariantRow))
		{
			row.CardGenerationVariantRow.Visible = isCardGeneration;
		}

		if (row.TurnBoundaryRow != null && GodotObject.IsInstanceValid(row.TurnBoundaryRow))
		{
			row.TurnBoundaryRow.Visible = trigger == CardExtraEffectTrigger.TurnBoundary;
		}

		if (asPower || isCreatedCardModifier || !isOnPlayTrigger || isAnyCostChange)
		{
			row.TimingSelect.Select((int)CardExtraEffectTiming.Immediate);
			row.TurnsField.Text = "1";
		}

		SyncUnifiedTimingControlsFromTiming(
			GetSelectedTiming(row),
			row.TimingModeSelect,
			row.TimingBoundaryEdgeSelect,
			row.TimingBoundarySideSelect,
			row.TimingBoundaryOffsetSelect);

		UpdateCreatedCardsCostTurnsEnabled(row);
		UpdateCardCostsLessTurnsEnabled(row);
		UpdateExtraEffectGrantTurnsEnabled(row);
		UpdateExtraEffectEnchantmentTurnsEnabled(row);
		UpdateExtraEffectMovePositionEnabled(row);
		UpdateExtraEffectScalingRowVisibility(row);
		UpdateExtraEffectCountTurnsEnabled(row);
		UpdateExtraEffectTurnsEnabled(row);
	}

	private void UpdateExtraEffectGrantTurnsEnabled(ExtraEffectRow row)
	{
		if (row?.GrantTurnsField == null
			|| row.GrantRow == null
			|| row.GrantModeSelect == null
			|| row.GrantCountRow == null
			|| row.GrantCountField == null
			|| row.GrantFilterRow == null)
		{
			return;
		}

		bool enabled = row.GrantTickbox.Visible && row.GrantTickbox.IsTicked;
		row.GrantRow.Visible = enabled;
		if (!enabled)
		{
			if (row.GrantDurationRow != null && GodotObject.IsInstanceValid(row.GrantDurationRow))
			{
				row.GrantDurationRow.Visible = false;
			}
			return;
		}

		int definitionIndex = GetSelectedExtraEffectDefinitionIndex(row);
		CardExtraEffectKind baseKind = CardEditorExtraEffects.Definitions[definitionIndex].Kind;
		CardExtraEffectKind kind = GetResolvedExtraEffectKind(row, baseKind);
		bool isEnchantCard = kind == CardExtraEffectKind.EnchantCard;

		CardExtraEffectCardSelectionMode selectionMode = GetSelectedCardSelectionMode(row.GrantModeSelect, CardExtraEffectCardSelectionMode.Choose);
		bool showCount = selectionMode != CardExtraEffectCardSelectionMode.All;
		row.GrantCountRow.Visible = showCount;
		row.GrantFilterRow.Visible = true;

		bool countUsesX = showCount && row.GrantCountXTickbox != null && GodotObject.IsInstanceValid(row.GrantCountXTickbox) && row.GrantCountXTickbox.IsTicked;
		bool enableCountField = showCount && !countUsesX;
		row.GrantCountField.Editable = enableCountField;
		row.GrantCountField.SelfModulate = enableCountField ? Colors.White : StsColors.gray;
		SetSpinEnabled(row.GrantCountField, enableCountField);

		int selected = row.GrantDurationSelect.Selected;
		CardExtraEffectCardGrantDuration duration = selected < 0 || selected >= Enum.GetValues<CardExtraEffectCardGrantDuration>().Length
			? CardExtraEffectCardGrantDuration.ThisTurn
			: (CardExtraEffectCardGrantDuration)selected;

		if (row.GrantDurationRow != null && GodotObject.IsInstanceValid(row.GrantDurationRow))
		{
			row.GrantDurationRow.Visible = !isEnchantCard;
		}

		bool showTurns = !isEnchantCard && duration == CardExtraEffectCardGrantDuration.Turns;
		row.GrantTurnsRow.Visible = showTurns;
		row.GrantTurnsField.Editable = showTurns;
		row.GrantTurnsField.SelfModulate = showTurns ? Colors.White : StsColors.gray;
		SetSpinEnabled(row.GrantTurnsField, showTurns);
	}

	private void UpdateExtraEffectEnchantmentTurnsEnabled(ExtraEffectRow row)
	{
		if (row?.EnchantmentRow == null
			|| row.EnchantmentSelect == null
			|| row.EnchantmentDurationSelect == null
			|| row.EnchantmentTurnsField == null
			|| row.EnchantmentTurnsRow == null)
		{
			return;
		}

		int definitionIndex = GetSelectedExtraEffectDefinitionIndex(row);
		CardExtraEffectKind baseKind = CardEditorExtraEffects.Definitions[definitionIndex].Kind;
		CardExtraEffectKind kind = GetResolvedExtraEffectKind(row, baseKind);
		if (kind != CardExtraEffectKind.EnchantCard)
		{
			row.EnchantmentTurnsRow.Visible = false;
			return;
		}

		bool hasEnchantment = row.EnchantmentSelect.Selected > 0;
		row.EnchantmentDurationSelect.Disabled = !hasEnchantment;
		row.EnchantmentDurationSelect.SelfModulate = hasEnchantment ? Colors.White : StsColors.gray;

		CardExtraEffectEnchantmentDuration duration = GetSelectedEnchantmentDuration(row.EnchantmentDurationSelect, CardExtraEffectEnchantmentDuration.ThisCombat);
		bool showTurns = hasEnchantment && duration == CardExtraEffectEnchantmentDuration.Turns;
		row.EnchantmentTurnsRow.Visible = showTurns;
		row.EnchantmentTurnsField.Editable = showTurns;
		row.EnchantmentTurnsField.SelfModulate = showTurns ? Colors.White : StsColors.gray;
		SetSpinEnabled(row.EnchantmentTurnsField, showTurns);
	}

	private void UpdateExtraEffectMovePositionEnabled(ExtraEffectRow row)
	{
		if (row?.MoveToPileSelect == null || row.MoveToPositionSelect == null || row.MoveCardsRow == null)
		{
			return;
		}

		if (!row.MoveCardsRow.Visible)
		{
			return;
		}

		int selected = row.MoveToPileSelect.Selected;
		CardExtraEffectCardPile toPile = selected < 0 || selected >= Enum.GetValues<CardExtraEffectCardPile>().Length
			? CardExtraEffectCardPile.DrawPile
			: (CardExtraEffectCardPile)selected;

		bool enablePosition = toPile == CardExtraEffectCardPile.DrawPile;
		row.MoveToPositionSelect.Disabled = !enablePosition;
		row.MoveToPositionSelect.SelfModulate = enablePosition ? Colors.White : StsColors.gray;
	}

	private void UpdateExtraEffectTriggerLabelTexts(ExtraEffectRow row, bool asPower)
	{
		if (row?.TriggerSelect == null || !GodotObject.IsInstanceValid(row.TriggerSelect))
		{
			return;
		}

		PopupMenu? popup = row.TriggerSelect.GetPopup();
		if (popup == null || !GodotObject.IsInstanceValid(popup))
		{
			return;
		}

		const string pendingMetaKey = "card_editor_trigger_label_pending_is_power";
		const string hookMetaKey = "card_editor_trigger_label_pending_hooked";

		// Avoid mutating the popup while it's visible (this can corrupt the live dropdown until the editor is reopened).
		// If we get called while it's open (e.g. from ItemSelected), queue an update for when it closes.
		if (popup.Visible)
		{
			row.TriggerSelect.SetMeta(pendingMetaKey, asPower);

			if (!popup.HasMeta(hookMetaKey))
			{
				popup.SetMeta(hookMetaKey, true);
				popup.VisibilityChanged += () =>
				{
					if (!GodotObject.IsInstanceValid(row.TriggerSelect) || !GodotObject.IsInstanceValid(popup))
					{
						return;
					}
					if (popup.Visible)
					{
						return;
					}
					if (!row.TriggerSelect.HasMeta(pendingMetaKey))
					{
						return;
					}

					bool desiredAsPower = false;
					try
					{
						desiredAsPower = (bool)row.TriggerSelect.GetMeta(pendingMetaKey);
					}
					catch
					{
					}

					try
					{
						row.TriggerSelect.RemoveMeta(pendingMetaKey);
					}
					catch
					{
					}

					UpdateExtraEffectTriggerLabelTexts(row, desiredAsPower);
				};
			}

			return;
		}

		for (int i = 0; i < popup.ItemCount; i++)
		{
			int id = popup.GetItemId(i);
			if (!Enum.IsDefined(typeof(CardExtraEffectTrigger), id))
			{
				continue;
			}
			CardExtraEffectTrigger trigger = (CardExtraEffectTrigger)id;
			popup.SetItemText(i, CardEditorExtraEffects.TriggerLabel(trigger, asPower));
			popup.SetItemDisabled(i, asPower && trigger == CardExtraEffectTrigger.Fatal);
		}
	}

	private void UpdateExtraEffectScalingRowVisibility(ExtraEffectRow row)
	{
		int kindIndex = GetSelectedExtraEffectDefinitionIndex(row);
		CardExtraEffectKind kind = GetResolvedExtraEffectKind(row, CardEditorExtraEffects.Definitions[kindIndex].Kind);

		bool isConditionalAutoFromPile = kind is CardExtraEffectKind.ConditionalAutoPlayFromPile or CardExtraEffectKind.ConditionalAutoDrawFromPile;
		bool supportsScaling = kind is not CardExtraEffectKind.CreatedCardsCostLess
			and not CardExtraEffectKind.CreatedCardsUpgraded
			and not CardExtraEffectKind.GeneratedCardsUpgraded
			and not CardExtraEffectKind.CardsInPileUpgradedAura
			and not CardExtraEffectKind.EndTurn
			and not CardExtraEffectKind.MultiplyStatStatus;

		row.ScalingToggleRow.Visible = supportsScaling;
		row.ScalingRow.Visible = supportsScaling && row.ScalingTickbox.IsTicked;
		if (row.CountModeSelect != null && GodotObject.IsInstanceValid(row.CountModeSelect))
		{
			row.CountModeSelect.Visible = row.ScalingRow.Visible && !isConditionalAutoFromPile;
		}

		bool showScalingBase = row.ScalingRow.Visible
			&& GetSelectedScaleMode(row, kind) == CardExtraEffectScaleMode.PerHistoryCount
			&& kind is CardExtraEffectKind.DealDamage or CardExtraEffectKind.GainBlock;
		row.ScalingBaseTickbox.Visible = showScalingBase;

		if (!supportsScaling)
		{
			row.ScalingTickbox.MouseFilter = MouseFilterEnum.Ignore;
		}
		else
		{
			row.ScalingTickbox.MouseFilter = MouseFilterEnum.Stop;
		}
	}

	private void ApplySuggestedCountSelectorDefaults(ExtraEffectRow row)
	{
		if (row == null)
		{
			return;
		}

		int kindIndex = GetSelectedExtraEffectDefinitionIndex(row);
		CardExtraEffectKind kind = GetResolvedExtraEffectKind(row, CardEditorExtraEffects.Definitions[kindIndex].Kind);
		CardExtraEffectCountEvent countEvent = GetSelectedCountEvent(row);

		if (CardEditorExtraEffects.CountEventUsesEnemyStatus(countEvent)
			&& CardEditorExtraEffects.TryGetSuggestedCountEnemyStatus(kind, out CardExtraEffectEnemyStatus suggestedStatus))
		{
			int selectedStatus = row.CountEnemyStatusSelect.Selected;
			if (selectedStatus < 0
				|| selectedStatus >= Enum.GetValues<CardExtraEffectEnemyStatus>().Length
				|| selectedStatus == (int)CardExtraEffectEnemyStatus.AnyPowerStatus)
			{
				row.CountEnemyStatusSelect.Select((int)suggestedStatus);
			}
		}

		if (CardEditorExtraEffects.CountEventUsesOrbType(countEvent)
			&& CardEditorExtraEffects.TryGetSuggestedCountOrbType(kind, GetSelectedOrbAction(row), GetSelectedOrbType(row), out CardExtraEffectOrbType suggestedOrbType))
		{
			int selectedOrbType = row.CountOrbTypeSelect.Selected;
			if (selectedOrbType < 0
				|| selectedOrbType >= Enum.GetValues<CardExtraEffectOrbType>().Length
				|| selectedOrbType == (int)CardExtraEffectOrbType.Any)
			{
				row.CountOrbTypeSelect.Select((int)suggestedOrbType);
			}
		}
	}

	private void UpdateExtraEffectCountTurnsEnabled(ExtraEffectRow row)
	{
		bool enabled = row.ScalingRow.Visible;
		CardExtraEffectCountEvent ev = GetSelectedCountEvent(row);
		bool usesWindow = CardEditorExtraEffects.CountEventUsesWindow(ev);
		bool usesCardPile = CardEditorExtraEffects.CountEventUsesCardPile(ev);
		bool usesCardFilters = CardEditorExtraEffects.CountEventUsesCardFilters(ev);
		bool usesOrbType = CardEditorExtraEffects.CountEventUsesOrbType(ev);
		bool usesOrbSelection = CardEditorExtraEffects.CountEventUsesOrbSelection(ev);
		bool usesEnemyStatus = CardEditorExtraEffects.CountEventUsesEnemyStatus(ev);
		bool usesEnemyIntent = CardEditorExtraEffects.CountEventUsesEnemyIntent(ev);

		row.CountWindowSelect.Visible = enabled && usesWindow;
		row.CountPileSelect.Visible = enabled && usesCardPile;
		row.CountCardFilterRow.Visible = enabled && usesCardFilters;
		row.CountOrbFilterRow.Visible = enabled && (usesOrbType || usesOrbSelection);
		row.CountOrbTypeSelect.Visible = enabled && usesOrbType;
		row.CountOrbSelectionSelect.Visible = enabled && usesOrbSelection;
		row.CountEnemyStatusRow.Visible = enabled && usesEnemyStatus;
		row.CountEnemyIntentRow.Visible = enabled && usesEnemyIntent;
		row.CountConditionRow.Visible = enabled && ev != CardExtraEffectCountEvent.OrbInPosition;
		row.BlockLostCountingModeRow.Visible = enabled && ev == CardExtraEffectCountEvent.BlockLost;

		if (!usesWindow)
		{
			row.CountTurnsRow.Visible = false;
			row.CountWindowInclusionRow.Visible = false;
		}
		else
		{
			int selected = row.CountWindowSelect.Selected;
			CardExtraEffectCountWindow window = selected < 0 || selected >= Enum.GetValues<CardExtraEffectCountWindow>().Length
				? CardExtraEffectCountWindow.ThisCombat
				: (CardExtraEffectCountWindow)selected;
			row.CountTurnsRow.Visible = enabled && window == CardExtraEffectCountWindow.LastTurns;
			row.CountWindowInclusionRow.Visible = row.CountTurnsRow.Visible;
		}

		bool showConditionAmount = row.CountConditionRow.Visible
			&& row.CountComparisonSelect != null
			&& GodotObject.IsInstanceValid(row.CountComparisonSelect)
			&& GetSelectedCountComparison(row) != CardExtraEffectCountComparison.None;
		if (row.CountConditionField != null && GodotObject.IsInstanceValid(row.CountConditionField))
		{
			row.CountConditionField.Visible = showConditionAmount;
			row.CountConditionField.Editable = showConditionAmount;
			row.CountConditionField.SelfModulate = showConditionAmount ? Colors.White : StsColors.gray;
			SetSpinEnabled(row.CountConditionField, showConditionAmount);
		}
	}

	private void UpdateExtraEffectTurnsEnabled(ExtraEffectRow row)
	{
		CardExtraEffectTiming timing = GetSelectedTiming(row);
		bool enableTurns = timing != CardExtraEffectTiming.Immediate;
		row.TurnsField.Editable = enableTurns;
		row.TurnsField.SelfModulate = enableTurns ? Colors.White : StsColors.gray;
		SetSpinEnabled(row.TurnsField, enableTurns);
	}

	private static CardExtraEffectTiming GetTimingFromUnifiedTimingControls(
		OptionButton timingModeSelect,
		OptionButton timingEdgeSelect,
		OptionButton timingSideSelect,
		OptionButton timingOffsetSelect)
	{
		if (timingModeSelect == null || !GodotObject.IsInstanceValid(timingModeSelect) || timingModeSelect.Selected == 0)
		{
			return CardExtraEffectTiming.Immediate;
		}

		bool isStart = timingEdgeSelect != null && GodotObject.IsInstanceValid(timingEdgeSelect) && timingEdgeSelect.Selected == 0;
		int selectedSide = timingSideSelect != null && GodotObject.IsInstanceValid(timingSideSelect)
			? timingSideSelect.Selected
			: 0;
		bool isEnemy = selectedSide == 1;
		bool isBoth = selectedSide == 2;

		if (isStart)
		{
			if (isBoth)
			{
				return CardExtraEffectTiming.StartOfAnyTurn;
			}
			return isEnemy ? CardExtraEffectTiming.StartOfEnemyTurn : CardExtraEffectTiming.StartOfTurn;
		}

		if (isEnemy)
		{
			return CardExtraEffectTiming.EndOfEnemyTurn;
		}

		if (isBoth)
		{
			bool isThisTurnForBoth = timingOffsetSelect != null && GodotObject.IsInstanceValid(timingOffsetSelect) && timingOffsetSelect.Selected == 0;
			return isThisTurnForBoth ? CardExtraEffectTiming.EndOfThisAnyTurn : CardExtraEffectTiming.EndOfAnyTurn;
		}

		bool isThisTurn = timingOffsetSelect != null && GodotObject.IsInstanceValid(timingOffsetSelect) && timingOffsetSelect.Selected == 0;
		return isThisTurn ? CardExtraEffectTiming.EndOfThisTurn : CardExtraEffectTiming.EndOfTurn;
	}

	private static void UpdateUnifiedTimingControlsVisibility(
		OptionButton timingModeSelect,
		OptionButton timingEdgeSelect,
		OptionButton timingSideSelect,
		OptionButton timingOffsetSelect)
	{
		bool isBoundary = timingModeSelect != null
			&& GodotObject.IsInstanceValid(timingModeSelect)
			&& timingModeSelect.Selected == 1;

		if (timingEdgeSelect != null && GodotObject.IsInstanceValid(timingEdgeSelect))
		{
			timingEdgeSelect.Visible = isBoundary;
		}

		if (timingSideSelect != null && GodotObject.IsInstanceValid(timingSideSelect))
		{
			timingSideSelect.Visible = isBoundary;
		}

		bool showOffset = isBoundary
			&& timingEdgeSelect != null
			&& GodotObject.IsInstanceValid(timingEdgeSelect)
			&& timingEdgeSelect.Selected == 1
			&& timingSideSelect != null
			&& GodotObject.IsInstanceValid(timingSideSelect)
			&& timingSideSelect.Selected != 1;

		if (timingOffsetSelect != null && GodotObject.IsInstanceValid(timingOffsetSelect))
		{
			timingOffsetSelect.Visible = showOffset;
		}
	}

	private static void SyncUnifiedTimingControlsFromTiming(
		CardExtraEffectTiming timing,
		OptionButton timingModeSelect,
		OptionButton timingEdgeSelect,
		OptionButton timingSideSelect,
		OptionButton timingOffsetSelect)
	{
		if (timingModeSelect == null || !GodotObject.IsInstanceValid(timingModeSelect))
		{
			return;
		}

		bool isNow = timing == CardExtraEffectTiming.Immediate;
		timingModeSelect.Select(isNow ? 0 : 1);

		bool isBoth = !isNow && timing is CardExtraEffectTiming.StartOfAnyTurn or CardExtraEffectTiming.EndOfAnyTurn or CardExtraEffectTiming.EndOfThisAnyTurn;
		bool isEnemy = !isNow && timing is CardExtraEffectTiming.StartOfEnemyTurn or CardExtraEffectTiming.EndOfEnemyTurn;
		bool isStart = isNow || timing is CardExtraEffectTiming.StartOfTurn or CardExtraEffectTiming.StartOfEnemyTurn or CardExtraEffectTiming.StartOfAnyTurn;
		bool isThisTurn = !isNow && timing is CardExtraEffectTiming.EndOfThisTurn or CardExtraEffectTiming.EndOfThisAnyTurn;

		if (timingEdgeSelect != null && GodotObject.IsInstanceValid(timingEdgeSelect))
		{
			timingEdgeSelect.Select(isStart ? 0 : 1);
		}

		if (timingSideSelect != null && GodotObject.IsInstanceValid(timingSideSelect))
		{
			timingSideSelect.Select(isEnemy ? 1 : (isBoth ? 2 : 0));
		}

		if (timingOffsetSelect != null && GodotObject.IsInstanceValid(timingOffsetSelect))
		{
			timingOffsetSelect.Select(isThisTurn ? 0 : 1);
		}

		UpdateUnifiedTimingControlsVisibility(timingModeSelect, timingEdgeSelect, timingSideSelect, timingOffsetSelect);
	}

	private static CardExtraEffectTiming GetSelectedTiming(ExtraEffectRow row)
	{
		int selected = row.TimingSelect.Selected;
		int timingCount = Enum.GetValues<CardExtraEffectTiming>().Length;
		if (selected < 0 || selected >= timingCount)
		{
			return CardExtraEffectTiming.Immediate;
		}
		return (CardExtraEffectTiming)selected;
	}

	private static CardExtraEffectDuration GetSelectedDuration(ExtraEffectRow row)
	{
		int selected = row.DurationSelect.Selected;
		if (selected < 0 || selected > 1)
		{
			return CardExtraEffectDuration.Permanent;
		}
		return (CardExtraEffectDuration)selected;
	}

	private static CardExtraEffectTrigger GetSelectedTrigger(ExtraEffectRow row)
	{
		int selected = row.TriggerSelect.Selected;
		if (selected < 0 || row.TriggerSelect == null || !GodotObject.IsInstanceValid(row.TriggerSelect) || selected >= row.TriggerSelect.ItemCount)
		{
			return CardExtraEffectTrigger.OnPlay;
		}

		int id = row.TriggerSelect.GetItemId(selected);
		return Enum.IsDefined(typeof(CardExtraEffectTrigger), id) ? (CardExtraEffectTrigger)id : CardExtraEffectTrigger.OnPlay;
	}

	private static CardExtraEffectScaleMode GetSelectedScaleMode(ExtraEffectRow row, CardExtraEffectKind kind)
	{
		bool supportsScaling = kind is not CardExtraEffectKind.CreatedCardsCostLess
			and not CardExtraEffectKind.CreatedCardsUpgraded
			and not CardExtraEffectKind.GeneratedCardsUpgraded
			and not CardExtraEffectKind.CardsInPileUpgradedAura;
		supportsScaling = supportsScaling && kind != CardExtraEffectKind.MultiplyStatStatus;
		if (!supportsScaling || !row.ScalingTickbox.IsTicked)
		{
			return CardExtraEffectScaleMode.None;
		}

		if (kind is CardExtraEffectKind.ConditionalAutoPlayFromPile or CardExtraEffectKind.ConditionalAutoDrawFromPile)
		{
			return CardExtraEffectScaleMode.ConditionOnly;
		}

		int selected = row.CountModeSelect.Selected;
		return selected == 1 ? CardExtraEffectScaleMode.ConditionOnly : CardExtraEffectScaleMode.PerHistoryCount;
	}

	private static CardExtraEffectCountEvent GetSelectedCountEvent(ExtraEffectRow row)
	{
		int selected = row.CountEventSelect.Selected;

		// ExtraEffectRow.CountEventSelect is populated from the full CardExtraEffectCountEvent enum
		// (unlike CardSmithRow which uses CardSmithCountEvents), so map directly by index.
		CardExtraEffectCountEvent[] values = Enum.GetValues<CardExtraEffectCountEvent>();
		if (selected < 0 || selected >= values.Length)
		{
			return CardExtraEffectCountEvent.Played;
		}
		return values[selected];
	}

	private static CardExtraEffectCountWindow GetSelectedCountWindow(ExtraEffectRow row)
	{
		int selected = row.CountWindowSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardExtraEffectCountWindow>().Length)
		{
			return CardExtraEffectCountWindow.ThisCombat;
		}
		return (CardExtraEffectCountWindow)selected;
	}

	private static CardExtraEffectCountWindowInclusion GetSelectedCountWindowInclusion(ExtraEffectRow row)
	{
		if (row.CountWindowInclusionSelect == null || !GodotObject.IsInstanceValid(row.CountWindowInclusionSelect))
		{
			return CardExtraEffectCountWindowInclusion.IncludeThisTurn;
		}

		int selected = row.CountWindowInclusionSelect.Selected;
		if (selected < 0 || selected >= row.CountWindowInclusionSelect.ItemCount)
		{
			return CardExtraEffectCountWindowInclusion.IncludeThisTurn;
		}

		int id = row.CountWindowInclusionSelect.GetItemId(selected);
		return Enum.IsDefined(typeof(CardExtraEffectCountWindowInclusion), id)
			? (CardExtraEffectCountWindowInclusion)id
			: CardExtraEffectCountWindowInclusion.IncludeThisTurn;
	}

	private static CardExtraEffectCountEvent GetSelectedPowerTriggerCountEvent(ExtraEffectRow row)
	{
		if (row.PowerCountEventSelect == null || !GodotObject.IsInstanceValid(row.PowerCountEventSelect))
		{
			return CardExtraEffectCountEvent.BlockLost;
		}

		int selected = row.PowerCountEventSelect.Selected;
		if (selected < 0 || selected >= row.PowerCountEventSelect.ItemCount)
		{
			return CardExtraEffectCountEvent.BlockLost;
		}

		int id = row.PowerCountEventSelect.GetItemId(selected);
		return Enum.IsDefined(typeof(CardExtraEffectCountEvent), id)
			? (CardExtraEffectCountEvent)id
			: CardExtraEffectCountEvent.BlockLost;
	}

	private static CardExtraEffectBlockLostCountingMode GetSelectedBlockLostCountingMode(ExtraEffectRow row)
	{
		if (row.BlockLostCountingModeSelect == null || !GodotObject.IsInstanceValid(row.BlockLostCountingModeSelect))
		{
			return CardExtraEffectBlockLostCountingMode.DamageAndEffects;
		}

		int selected = row.BlockLostCountingModeSelect.Selected;
		if (selected < 0 || selected >= row.BlockLostCountingModeSelect.ItemCount)
		{
			return CardExtraEffectBlockLostCountingMode.DamageAndEffects;
		}

		int id = row.BlockLostCountingModeSelect.GetItemId(selected);
		return Enum.IsDefined(typeof(CardExtraEffectBlockLostCountingMode), id)
			? (CardExtraEffectBlockLostCountingMode)id
			: CardExtraEffectBlockLostCountingMode.DamageAndEffects;
	}

	private static CardExtraEffectCountComparison GetSelectedCountComparison(ExtraEffectRow row)
	{
		int selected = row.CountComparisonSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardExtraEffectCountComparison>().Length)
		{
			return CardExtraEffectCountComparison.None;
		}
		return (CardExtraEffectCountComparison)selected;
	}

	private static int GetSelectedCountConditionAmount(ExtraEffectRow row)
	{
		int value = ParseIntOrDefault(row.CountConditionField.Text, 1);
		return Math.Clamp(value, 0, 99);
	}

	private static int GetSelectedCountTurns(ExtraEffectRow row)
	{
		int value = ParseIntOrDefault(row.CountTurnsField.Text, 2);
		return Math.Clamp(value, 1, 99);
	}

	private static CardGeneratedCardPool GetSelectedCountPool(ExtraEffectRow row)
	{
		int selected = row.CountPoolSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardGeneratedCardPool>().Length)
		{
			return CardGeneratedCardPool.All;
		}
		return (CardGeneratedCardPool)selected;
	}

	private static CardGeneratedCardType GetSelectedCountType(ExtraEffectRow row)
	{
		int selected = row.CountTypeSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardGeneratedCardType>().Length)
		{
			return CardGeneratedCardType.Any;
		}
		return (CardGeneratedCardType)selected;
	}

	private static CardExtraEffectCountCardFilter GetSelectedCountFilter(ExtraEffectRow row)
	{
		int selected = row.CountFilterSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardExtraEffectCountCardFilter>().Length)
		{
			return CardExtraEffectCountCardFilter.Any;
		}
		return (CardExtraEffectCountCardFilter)selected;
	}

	private static CardExtraEffectOrbType GetSelectedCountOrbType(ExtraEffectRow row)
	{
		int selected = row.CountOrbTypeSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardExtraEffectOrbType>().Length)
		{
			return GetSuggestedCountOrbTypeOrDefault(row);
		}

		CardExtraEffectOrbType orbType = (CardExtraEffectOrbType)selected;
		if (orbType == CardExtraEffectOrbType.Any
			&& CardEditorExtraEffects.TryGetSuggestedCountOrbType(GetCurrentResolvedExtraEffectKind(row), GetSelectedOrbAction(row), GetSelectedOrbType(row), out CardExtraEffectOrbType suggestedOrbType)
			&& suggestedOrbType != CardExtraEffectOrbType.Any)
		{
			return suggestedOrbType;
		}

		return orbType;
	}

	private static CardExtraEffectOrbSelection GetSelectedCountOrbSelection(ExtraEffectRow row)
	{
		int selected = row.CountOrbSelectionSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardExtraEffectOrbSelection>().Length)
		{
			return CardExtraEffectOrbSelection.Leftmost;
		}
		return (CardExtraEffectOrbSelection)selected;
	}

	private static CardExtraEffectEnemyStatus GetSelectedCountEnemyStatus(ExtraEffectRow row)
	{
		int selected = row.CountEnemyStatusSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardExtraEffectEnemyStatus>().Length)
		{
			return GetSuggestedCountEnemyStatusOrDefault(row);
		}

		return (CardExtraEffectEnemyStatus)selected;
	}

	private static CardExtraEffectEnemyIntent GetSelectedCountEnemyIntent(ExtraEffectRow row)
	{
		int selected = row.CountEnemyIntentSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardExtraEffectEnemyIntent>().Length)
		{
			return CardExtraEffectEnemyIntent.Attack;
		}
		return (CardExtraEffectEnemyIntent)selected;
	}

	private static CardExtraEffectMultiplierStat GetSelectedMultiplierStat(ExtraEffectRow row)
	{
		int selected = row.MultiplyStatSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardExtraEffectMultiplierStat>().Length)
		{
			return CardExtraEffectMultiplierStat.Strength;
		}
		return (CardExtraEffectMultiplierStat)selected;
	}

	private static CardExtraEffectEnemyStatus GetSuggestedCountEnemyStatusOrDefault(ExtraEffectRow row)
	{
		return CardEditorExtraEffects.TryGetSuggestedCountEnemyStatus(GetCurrentResolvedExtraEffectKind(row), out CardExtraEffectEnemyStatus suggestedStatus)
			? suggestedStatus
			: CardExtraEffectEnemyStatus.Weak;
	}

	private static CardExtraEffectOrbType GetSuggestedCountOrbTypeOrDefault(ExtraEffectRow row)
	{
		return CardEditorExtraEffects.TryGetSuggestedCountOrbType(GetCurrentResolvedExtraEffectKind(row), GetSelectedOrbAction(row), GetSelectedOrbType(row), out CardExtraEffectOrbType suggestedOrbType)
			? suggestedOrbType
			: CardExtraEffectOrbType.Any;
	}

	private static CardExtraEffectKind GetCurrentResolvedExtraEffectKind(ExtraEffectRow row)
	{
		int kindIndex = GetSelectedExtraEffectDefinitionIndex(row);
		return GetResolvedExtraEffectKind(row, CardEditorExtraEffects.Definitions[kindIndex].Kind);
	}

	private static CardExtraEffectCardPile GetSelectedCardPile(OptionButton select, CardExtraEffectCardPile fallback)
	{
		if (select == null || !GodotObject.IsInstanceValid(select))
		{
			return fallback;
		}

		int selected = select.Selected;
		if (selected < 0 || selected >= select.ItemCount)
		{
			return fallback;
		}

		int id = select.GetItemId(selected);
		if (!Enum.IsDefined(typeof(CardExtraEffectCardPile), id))
		{
			return fallback;
		}

		return (CardExtraEffectCardPile)id;
	}

	private static CardExtraEffectCardPilePosition GetSelectedCardPilePosition(OptionButton select, CardExtraEffectCardPilePosition fallback)
	{
		if (select == null || !GodotObject.IsInstanceValid(select))
		{
			return fallback;
		}

		int selected = select.Selected;
		if (selected < 0 || selected >= select.ItemCount)
		{
			return fallback;
		}

		int id = select.GetItemId(selected);
		if (!Enum.IsDefined(typeof(CardExtraEffectCardPilePosition), id))
		{
			return fallback;
		}

		return (CardExtraEffectCardPilePosition)id;
	}

	private static CardExtraEffectCardSelectionMode GetSelectedCardSelectionMode(OptionButton select, CardExtraEffectCardSelectionMode fallback)
	{
		if (select == null || !GodotObject.IsInstanceValid(select))
		{
			return fallback;
		}

		int selected = select.Selected;
		if (selected < 0 || selected >= select.ItemCount)
		{
			return fallback;
		}

		int id = select.GetItemId(selected);
		if (!Enum.IsDefined(typeof(CardExtraEffectCardSelectionMode), id))
		{
			return fallback;
		}

		return (CardExtraEffectCardSelectionMode)id;
	}

	private static CardExtraEffectCardGrantDuration GetSelectedCardGrantDuration(OptionButton select, CardExtraEffectCardGrantDuration fallback)
	{
		if (select == null || !GodotObject.IsInstanceValid(select))
		{
			return fallback;
		}

		int selected = select.Selected;
		if (selected < 0 || selected >= select.ItemCount)
		{
			return fallback;
		}

		int id = select.GetItemId(selected);
		if (!Enum.IsDefined(typeof(CardExtraEffectCardGrantDuration), id))
		{
			return fallback;
		}

		return (CardExtraEffectCardGrantDuration)id;
	}

	private CardExtraEffectEnchantmentDuration GetSelectedEnchantmentDuration(OptionButton select, CardExtraEffectEnchantmentDuration fallback)
	{
		int selected = select.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardExtraEffectEnchantmentDuration>().Length)
		{
			return fallback;
		}
		return (CardExtraEffectEnchantmentDuration)selected;
	}

	private string? GetSelectedExtraEffectEnchantmentId(ExtraEffectRow row)
	{
		if (row?.EnchantmentSelect == null || !GodotObject.IsInstanceValid(row.EnchantmentSelect))
		{
			return null;
		}

		int selected = row.EnchantmentSelect.Selected;
		if (selected < 0 || selected >= _enchantmentIds.Count)
		{
			return null;
		}

		ModelId? enchantmentId = _enchantmentIds[selected];
		if (enchantmentId == null || enchantmentId == ModelId.none)
		{
			return null;
		}

		return enchantmentId.ToString();
	}

	private static CardExtraEffectTiming GetSelectedTiming(CardSmithRow row)
	{
		int selected = row.TimingSelect.Selected;
		int timingCount = Enum.GetValues<CardExtraEffectTiming>().Length;
		if (selected < 0 || selected >= timingCount)
		{
			return CardExtraEffectTiming.Immediate;
		}
		return (CardExtraEffectTiming)selected;
	}

	private static CardExtraEffectDuration GetSelectedDuration(CardSmithRow row)
	{
		int selected = row.DurationSelect.Selected;
		if (selected < 0 || selected > 1)
		{
			return CardExtraEffectDuration.Permanent;
		}
		return (CardExtraEffectDuration)selected;
	}

	private static CardExtraEffectTrigger GetSelectedTrigger(CardSmithRow row)
	{
		int selected = row.TriggerSelect.Selected;
		if (selected < 0 || row.TriggerSelect == null || !GodotObject.IsInstanceValid(row.TriggerSelect) || selected >= row.TriggerSelect.ItemCount)
		{
			return CardExtraEffectTrigger.OnPlay;
		}

		int id = row.TriggerSelect.GetItemId(selected);
		return Enum.IsDefined(typeof(CardExtraEffectTrigger), id) ? (CardExtraEffectTrigger)id : CardExtraEffectTrigger.OnPlay;
	}

	private static CardExtraEffectCountEvent GetSelectedCountEvent(CardSmithRow row)
	{
		int selected = row.CountEventSelect.Selected;
		if (selected < 0 || selected >= CardEditorExtraEffects.CardSmithCountEvents.Count)
		{
			return CardExtraEffectCountEvent.Played;
		}
		return CardEditorExtraEffects.CardSmithCountEvents[selected];
	}

	private static CardExtraEffectCountWindow GetSelectedCountWindow(CardSmithRow row)
	{
		int selected = row.CountWindowSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardExtraEffectCountWindow>().Length)
		{
			return CardExtraEffectCountWindow.ThisCombat;
		}
		return (CardExtraEffectCountWindow)selected;
	}

	private static CardExtraEffectCountWindowInclusion GetSelectedCountWindowInclusion(CardSmithRow row)
	{
		if (row.CountWindowInclusionSelect == null || !GodotObject.IsInstanceValid(row.CountWindowInclusionSelect))
		{
			return CardExtraEffectCountWindowInclusion.IncludeThisTurn;
		}

		int selected = row.CountWindowInclusionSelect.Selected;
		if (selected < 0 || selected >= row.CountWindowInclusionSelect.ItemCount)
		{
			return CardExtraEffectCountWindowInclusion.IncludeThisTurn;
		}

		int id = row.CountWindowInclusionSelect.GetItemId(selected);
		return Enum.IsDefined(typeof(CardExtraEffectCountWindowInclusion), id)
			? (CardExtraEffectCountWindowInclusion)id
			: CardExtraEffectCountWindowInclusion.IncludeThisTurn;
	}

	private static CardExtraEffectBlockLostCountingMode GetSelectedBlockLostCountingMode(CardSmithRow row)
	{
		if (row.BlockLostCountingModeSelect == null || !GodotObject.IsInstanceValid(row.BlockLostCountingModeSelect))
		{
			return CardExtraEffectBlockLostCountingMode.DamageAndEffects;
		}

		int selected = row.BlockLostCountingModeSelect.Selected;
		if (selected < 0 || selected >= row.BlockLostCountingModeSelect.ItemCount)
		{
			return CardExtraEffectBlockLostCountingMode.DamageAndEffects;
		}

		int id = row.BlockLostCountingModeSelect.GetItemId(selected);
		return Enum.IsDefined(typeof(CardExtraEffectBlockLostCountingMode), id)
			? (CardExtraEffectBlockLostCountingMode)id
			: CardExtraEffectBlockLostCountingMode.DamageAndEffects;
	}

	private static CardGeneratedCardPool GetSelectedCountPool(CardSmithRow row)
	{
		int selected = row.CountPoolSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardGeneratedCardPool>().Length)
		{
			return CardGeneratedCardPool.All;
		}
		return (CardGeneratedCardPool)selected;
	}

	private static CardGeneratedCardType GetSelectedCountType(CardSmithRow row)
	{
		int selected = row.CountTypeSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardGeneratedCardType>().Length)
		{
			return CardGeneratedCardType.Any;
		}
		return (CardGeneratedCardType)selected;
	}

	private static CardGeneratedCardPool GetSelectedGeneratedCardPool(CardSmithRow row)
	{
		int selected = row.GeneratedPoolSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardGeneratedCardPool>().Length)
		{
			return CardGeneratedCardPool.Default;
		}
		return (CardGeneratedCardPool)selected;
	}

	private static CardGeneratedCardType GetSelectedGeneratedCardType(CardSmithRow row)
	{
		int selected = row.GeneratedTypeSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardGeneratedCardType>().Length)
		{
			return CardGeneratedCardType.Any;
		}
		return (CardGeneratedCardType)selected;
	}

	private static int ParseIntOrDefault(string? text, int defaultValue)
	{
		if (!string.IsNullOrWhiteSpace(text)
			&& int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
		{
			return value;
		}
		return defaultValue;
	}

	private static CardCreatedCardsCostDuration GetSelectedCreatedCardsCostDuration(ExtraEffectRow row)
	{
		int selected = row.CreatedCostDurationSelect.Selected;
		if (selected < 0 || selected > 3)
		{
			return CardCreatedCardsCostDuration.ThisTurn;
		}
		return (CardCreatedCardsCostDuration)selected;
	}

	private static CardExtraEffectCardCostsLessDuration GetSelectedCardCostsLessDuration(ExtraEffectRow row)
	{
		int selected = row.CardCostsLessDurationSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardExtraEffectCardCostsLessDuration>().Length)
		{
			return CardExtraEffectCardCostsLessDuration.Permanent;
		}
		return (CardExtraEffectCardCostsLessDuration)selected;
	}

	private static CardExtraEffectCardCostsLessMode GetSelectedCardCostsLessMode(ExtraEffectRow row)
	{
		int selected = row.CardCostsLessModeSelect.Selected;
		return selected == 1
			? CardExtraEffectCardCostsLessMode.Triggered
			: CardExtraEffectCardCostsLessMode.Passive;
	}

	private static CardGeneratedCardPool GetSelectedGeneratedCardPool(ExtraEffectRow row)
	{
		int selected = row.GeneratedPoolSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardGeneratedCardPool>().Length)
		{
			return CardGeneratedCardPool.Default;
		}
		return (CardGeneratedCardPool)selected;
	}

	private static CardGeneratedCardType GetSelectedGeneratedCardType(ExtraEffectRow row)
	{
		int selected = row.GeneratedTypeSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardGeneratedCardType>().Length)
		{
			return CardGeneratedCardType.Any;
		}
		return (CardGeneratedCardType)selected;
	}

	private static CardGeneratedCardPool GetSelectedTriggerCardPool(ExtraEffectRow row)
	{
		int selected = row.TriggerCardPoolSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardGeneratedCardPool>().Length)
		{
			return CardGeneratedCardPool.All;
		}
		return (CardGeneratedCardPool)selected;
	}

	private static CardGeneratedCardType GetSelectedTriggerCardType(ExtraEffectRow row)
	{
		int selected = row.TriggerCardTypeSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardGeneratedCardType>().Length)
		{
			return CardGeneratedCardType.Any;
		}
		return (CardGeneratedCardType)selected;
	}

	private static CardExtraEffectCountCardFilter GetSelectedTriggerCardFilter(ExtraEffectRow row)
	{
		int selected = row.TriggerCardFilterSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardExtraEffectCountCardFilter>().Length)
		{
			return CardExtraEffectCountCardFilter.Any;
		}
		return (CardExtraEffectCountCardFilter)selected;
	}

	private static CardGeneratedCardPool GetSelectedGrantCountPool(ExtraEffectRow row)
	{
		if (row.GrantCountPoolSelect == null)
		{
			return CardGeneratedCardPool.All;
		}
		int selected = row.GrantCountPoolSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardGeneratedCardPool>().Length)
		{
			return CardGeneratedCardPool.All;
		}
		return (CardGeneratedCardPool)selected;
	}

	private static CardGeneratedCardType GetSelectedGrantCountType(ExtraEffectRow row)
	{
		if (row.GrantCountTypeSelect == null)
		{
			return CardGeneratedCardType.Any;
		}
		int selected = row.GrantCountTypeSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardGeneratedCardType>().Length)
		{
			return CardGeneratedCardType.Any;
		}
		return (CardGeneratedCardType)selected;
	}

	private static CardExtraEffectCountCardFilter GetSelectedGrantCountFilter(ExtraEffectRow row)
	{
		if (row.GrantCountFilterSelect == null)
		{
			return CardExtraEffectCountCardFilter.Any;
		}
		int selected = row.GrantCountFilterSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardExtraEffectCountCardFilter>().Length)
		{
			return CardExtraEffectCountCardFilter.Any;
		}
		return (CardExtraEffectCountCardFilter)selected;
	}

	private static CardExtraEffectOrbAction GetSelectedOrbAction(ExtraEffectRow row)
	{
		int selected = row.OrbActionSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardExtraEffectOrbAction>().Length)
		{
			return CardExtraEffectOrbAction.Evoke;
		}
		return (CardExtraEffectOrbAction)selected;
	}

	private static CardExtraEffectOrbScope GetSelectedOrbScope(ExtraEffectRow row)
	{
		if (row.OrbScopeSelect == null || !GodotObject.IsInstanceValid(row.OrbScopeSelect))
		{
			return CardExtraEffectOrbScope.Fixed;
		}
		int selected = row.OrbScopeSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardExtraEffectOrbScope>().Length)
		{
			return CardExtraEffectOrbScope.Fixed;
		}
		return (CardExtraEffectOrbScope)selected;
	}

	private static CardExtraEffectOstyAction GetSelectedOstyAction(ExtraEffectRow row)
	{
		int selected = row.OstyActionSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardExtraEffectOstyAction>().Length)
		{
			return CardExtraEffectOstyAction.Attack;
		}
		return (CardExtraEffectOstyAction)selected;
	}

	private static CardKeyword GetSelectedGrantedKeyword(ExtraEffectRow row)
	{
		if (row.GrantedKeywordSelect == null || !GodotObject.IsInstanceValid(row.GrantedKeywordSelect))
		{
			return CardKeyword.Exhaust;
		}
		int idx = row.GrantedKeywordSelect.Selected;
		if (idx < 0 || idx >= row.GrantedKeywordSelect.ItemCount)
		{
			return CardKeyword.Exhaust;
		}
		int id = row.GrantedKeywordSelect.GetItemId(idx);
		if (!Enum.IsDefined(typeof(CardKeyword), id))
		{
			return CardKeyword.Exhaust;
		}
		return (CardKeyword)id;
	}

	private static CardExtraEffectOrbType GetSelectedOrbType(ExtraEffectRow row)
	{
		int selected = row.OrbTypeSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardExtraEffectOrbType>().Length)
		{
			return CardExtraEffectOrbType.Any;
		}
		return (CardExtraEffectOrbType)selected;
	}

	private static CardExtraEffectOrbSelection GetSelectedOrbSelection(ExtraEffectRow row)
	{
		int selected = row.OrbSelectionSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardExtraEffectOrbSelection>().Length)
		{
			return CardExtraEffectOrbSelection.Leftmost;
		}
		return (CardExtraEffectOrbSelection)selected;
	}

	private static CardExtraEffectOrbFollowUp GetSelectedOrbFollowUp(ExtraEffectRow row)
	{
		int selected = row.OrbFollowUpSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardExtraEffectOrbFollowUp>().Length)
		{
			return CardExtraEffectOrbFollowUp.None;
		}
		return (CardExtraEffectOrbFollowUp)selected;
	}

	private static CardExtraEffectTurnBoundary GetSelectedTurnBoundaryEdge(ExtraEffectRow row)
	{
		int selected = row.TurnBoundaryEdgeSelect.Selected;
		return selected == 0 ? CardExtraEffectTurnBoundary.Start : CardExtraEffectTurnBoundary.End;
	}

	private static CardExtraEffectTurnBoundarySide GetSelectedTurnBoundarySide(ExtraEffectRow row)
	{
		int selected = row.TurnBoundarySideSelect.Selected;
		return selected switch
		{
			1 => CardExtraEffectTurnBoundarySide.EnemyTurn,
			2 => CardExtraEffectTurnBoundarySide.Both,
			_ => CardExtraEffectTurnBoundarySide.YourTurn
		};
	}

	private static CardExtraEffectTurnBoundaryCardLocation GetSelectedTurnBoundaryLocation(ExtraEffectRow row)
	{
		int selected = row.TurnBoundaryLocationSelect.Selected;
		return selected switch
		{
			1 => CardExtraEffectTurnBoundaryCardLocation.Hand,
			2 => CardExtraEffectTurnBoundaryCardLocation.DrawPile,
			3 => CardExtraEffectTurnBoundaryCardLocation.DiscardPile,
			4 => CardExtraEffectTurnBoundaryCardLocation.ExhaustPile,
			_ => CardExtraEffectTurnBoundaryCardLocation.Any
		};
	}

	private static int GetSelectedExtraEffectDefinitionIndex(ExtraEffectRow row)
	{
		if (row == null || row.KindDefinitionIndices == null || row.KindDefinitionIndices.Count == 0)
		{
			return 0;
		}

		int selected = row.KindSelect.Selected;
		if (selected < 0 || selected >= row.KindDefinitionIndices.Count)
		{
			return row.KindDefinitionIndices[0];
		}

		int definitionIndex = row.KindDefinitionIndices[selected];
		if (definitionIndex < 0 || definitionIndex >= CardEditorExtraEffects.Definitions.Count)
		{
			return row.KindDefinitionIndices[0];
		}

		return definitionIndex;
	}

	private void UpdateCreatedCardsCostTurnsEnabled(ExtraEffectRow row)
	{
		bool enableTurns = GetSelectedCreatedCardsCostDuration(row) == CardCreatedCardsCostDuration.Turns;
		row.CreatedCostTurnsField.Editable = enableTurns;
		row.CreatedCostTurnsField.SelfModulate = enableTurns ? Colors.White : StsColors.gray;
		SetSpinEnabled(row.CreatedCostTurnsField, enableTurns);
	}

	private void UpdateCardCostsLessTurnsEnabled(ExtraEffectRow row)
	{
		if (row?.CardCostsLessRow == null || row.CardCostsLessTurnsField == null)
		{
			return;
		}
		if (!row.CardCostsLessRow.Visible)
		{
			return;
		}

		bool enableTurns = GetSelectedCardCostsLessDuration(row) == CardExtraEffectCardCostsLessDuration.Turns;
		row.CardCostsLessTurnsField.Editable = enableTurns;
		row.CardCostsLessTurnsField.SelfModulate = enableTurns ? Colors.White : StsColors.gray;
		SetSpinEnabled(row.CardCostsLessTurnsField, enableTurns);
	}

	private void UpdateExtraEffectDurationEnabled(ExtraEffectRow row, CardExtraEffectDuration? desiredDuration)
	{
		int kindIndex = GetSelectedExtraEffectDefinitionIndex(row);
		CardExtraEffectDefinition def = CardEditorExtraEffects.Definitions[kindIndex];
		bool asPower = row.PowerTickbox.Visible && row.PowerTickbox.IsTicked;
		bool supported = asPower || CardEditorExtraEffects.SupportsDuration(def.Kind);

		if (!supported)
		{
			row.DurationSelect.Select((int)CardExtraEffectDuration.Permanent);
		}
		else if (desiredDuration.HasValue)
		{
			int index = (int)desiredDuration.Value;
			if (index < 0 || index > 1)
			{
				index = 0;
			}
			row.DurationSelect.Select(index);
		}

		row.DurationSelect.Disabled = !supported;
		row.DurationSelect.SelfModulate = supported ? Colors.White : StsColors.gray;
	}

	private void ConfigureExtraEffectTargets(ExtraEffectRow row, CardExtraEffectTarget? desiredTarget)
	{
		int kindIndex = GetSelectedExtraEffectDefinitionIndex(row);
		CardExtraEffectDefinition def = CardEditorExtraEffects.Definitions[kindIndex];

		row.AllowedTargets.Clear();
		IReadOnlyList<CardExtraEffectTarget> allowed = def.AllowedTargets;
		if (GetSelectedTrigger(row) != CardExtraEffectTrigger.OnPlay)
		{
			allowed = allowed.Where(t => t != CardExtraEffectTarget.Target).ToArray();
		}

		// For OstyAction, restrict targets based on which action is selected
		if (def.Kind == CardExtraEffectKind.OstyAction)
		{
			CardExtraEffectOstyAction ostyAction = GetSelectedOstyAction(row);
			if (ostyAction == CardExtraEffectOstyAction.Attack)
			{
				allowed = allowed.Where(t => t != CardExtraEffectTarget.AllEnemies).ToArray();
			}
			else if (ostyAction == CardExtraEffectOstyAction.AttackAll)
			{
				allowed = new[] { CardExtraEffectTarget.AllEnemies };
			}
			else
			{
				// Heal and Kill only affect Osty directly, no target needed
				allowed = new[] { CardExtraEffectTarget.Self };
			}
		}

		row.AllowedTargets.AddRange(allowed);

		row.TargetSelect.Clear();
		foreach (CardExtraEffectTarget target in row.AllowedTargets)
		{
			row.TargetSelect.AddItem(CardEditorExtraEffects.TargetLabel(target));
		}

		CardExtraEffectTarget wanted = desiredTarget ?? def.DefaultTarget;
		if (GetSelectedTrigger(row) != CardExtraEffectTrigger.OnPlay && wanted == CardExtraEffectTarget.Target)
		{
			wanted = row.AllowedTargets.Contains(CardExtraEffectTarget.RandomEnemy) ? CardExtraEffectTarget.RandomEnemy : CardExtraEffectTarget.Self;
		}
		int selectIndex = 0;
		for (int i = 0; i < row.AllowedTargets.Count; i++)
		{
			if (row.AllowedTargets[i] == wanted)
			{
				selectIndex = i;
				break;
		}
		}

		bool enabled = row.AllowedTargets.Count > 1;
		row.TargetSelect.Select(selectIndex);
		row.TargetSelect.Disabled = !enabled;
		row.TargetSelect.SelfModulate = enabled ? Colors.White : StsColors.gray;
}

	private Control CreateSpinButtons(LineEdit field, decimal step, decimal? minValue, decimal? maxValue, bool isInteger = false)
	{
		VBoxContainer container = new VBoxContainer();
		container.CustomMinimumSize = _spinContainerMinSize;
		container.AddThemeConstantOverride("separation", 2);

		Button up = CreateSpinButton("\u25B2");
		Button down = CreateSpinButton("\u25BC");
		container.AddChild(up);
		container.AddChild(down);

		SpinButtons spin = new SpinButtons
		{
			Field = field,
			Container = container,
			Up = up,
			Down = down,
			Step = step,
			MinValue = minValue,
			MaxValue = maxValue,
			IsInteger = isInteger
		};
		_spinButtons[field] = spin;

		up.Connect(BaseButton.SignalName.ButtonDown, Callable.From(() => StartSpinHold(spin, +1)));
		down.Connect(BaseButton.SignalName.ButtonDown, Callable.From(() => StartSpinHold(spin, -1)));
		up.Connect(BaseButton.SignalName.ButtonUp, Callable.From(StopSpinHold));
		down.Connect(BaseButton.SignalName.ButtonUp, Callable.From(StopSpinHold));

		return container;
	}

	private Button CreateSpinButton(string glyph)
	{
		Button button = new Button
		{
			Text = glyph,
			Flat = true,
			FocusMode = FocusModeEnum.None,
			CustomMinimumSize = _spinButtonMinSize,
			MouseFilter = MouseFilterEnum.Stop
		};
		_headerFont ??= TryLoadFont(_headerFontPath);
		if (_headerFont != null)
		{
			button.AddThemeFontOverride("font", _headerFont);
		}
		button.AddThemeFontSizeOverride("font_size", 18);
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

		return button;
	}

	private void StartSpinHold(SpinButtons target, int direction)
	{
		if (direction == 0)
		{
			return;
		}
		if (target.Up.Disabled || target.Down.Disabled)
		{
			return;
		}
		SpinStepOnce(target, direction);
		_holdSpinState = new HoldSpinState
		{
			Field = target.Field,
			Target = target,
			Direction = direction,
			HeldSeconds = 0,
			RepeatCountdownSeconds = _holdInitialDelaySeconds
		};
		SetProcess(true);
	}

	private void StopSpinHold()
	{
		if (_holdSpinState == null)
		{
			return;
		}
		_holdSpinState = null;
		SetProcess(false);
	}

	private static double GetHoldRepeatInterval(double heldSeconds)
	{
		double t = Math.Clamp((heldSeconds - _holdInitialDelaySeconds) / _holdAccelerationSeconds, 0.0, 1.0);
		return _holdRepeatSlowSeconds + (_holdRepeatFastSeconds - _holdRepeatSlowSeconds) * t;
	}

	private void SpinStepOnce(SpinButtons target, int direction)
	{
		if (direction == 0)
		{
			return;
		}
		LineEdit field = target.Field;
		if (!GodotObject.IsInstanceValid(field) || field.IsQueuedForDeletion())
		{
			return;
		}
		if (!field.Editable)
		{
			return;
		}

		decimal? minValue = target.MinValue;
		decimal? maxValue = target.MaxValue;
		if (target.IsInteger && IsCreatedCardsCostLessAmountField(field))
		{
			minValue = -1m;
		}
		if (target.IsInteger && IsCardCostsDeltaAmountField(field))
		{
			minValue = -999m;
		}

		if (target.IsInteger)
		{
			int current = ParseInt(field.Text);
			decimal next = (decimal)current + target.Step * direction;
			next = Clamp(next, minValue, maxValue);
			field.Text = ((int)next).ToString(CultureInfo.InvariantCulture);
		}
		else
		{
			decimal current = ParseDecimal(field.Text);
			decimal next = current + target.Step * direction;
			next = Clamp(next, minValue, maxValue);
			field.Text = next.ToString(CultureInfo.InvariantCulture);
		}

		QueuePreviewUpdate();
	}

	private bool IsCreatedCardsCostLessAmountField(LineEdit field)
	{
		if (field == null || _extraEffectRows.Count == 0)
		{
			return false;
		}

		foreach (ExtraEffectRow row in _extraEffectRows)
		{
			if (row == null || !ReferenceEquals(row.AmountField, field))
			{
				continue;
			}

			return GetCurrentResolvedExtraEffectKind(row) == CardExtraEffectKind.CreatedCardsCostLess;
		}

		return false;
	}

	private bool IsCardCostsDeltaAmountField(LineEdit field)
	{
		if (field == null || _extraEffectRows.Count == 0)
		{
			return false;
		}

		foreach (ExtraEffectRow row in _extraEffectRows)
		{
			if (row == null || !ReferenceEquals(row.AmountField, field))
			{
				continue;
			}

			int kindIndex = GetSelectedExtraEffectDefinitionIndex(row);
			if (kindIndex < 0 || kindIndex >= CardEditorExtraEffects.Definitions.Count)
			{
				return false;
			}

			CardExtraEffectKind kind = GetResolvedExtraEffectKind(row, CardEditorExtraEffects.Definitions[kindIndex].Kind);
			return IsUnifiedCardCostModifierKind(kind);
		}

		return false;
	}

	private static int ParseInt(string text)
	{
		if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
		{
			return value;
		}
		return 0;
	}

	private static decimal ParseDecimal(string text)
	{
		if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal value))
		{
			return value;
		}
		return 0m;
	}

	private static decimal Clamp(decimal value, decimal? minValue, decimal? maxValue)
	{
		if (minValue.HasValue)
		{
			value = Math.Max(value, minValue.Value);
		}
		if (maxValue.HasValue)
		{
			value = Math.Min(value, maxValue.Value);
		}
		return value;
	}

	private void SetSpinEnabled(LineEdit field, bool enabled)
	{
		if (!_spinButtons.TryGetValue(field, out SpinButtons spin))
		{
			return;
		}
		spin.Up.Disabled = !enabled;
		spin.Down.Disabled = !enabled;
		spin.Container.SelfModulate = enabled ? Colors.White : StsColors.gray;
	}

	private void PopulateEnchantments()
	{
		_enchantmentSelect.Clear();
		_enchantmentIds.Clear();

		_enchantmentSelect.AddItem(CardEditorLoc.T("value.none", "None"));
		_enchantmentIds.Add(ModelId.none);
		foreach (EnchantmentModel enchantment in ModelDb.DebugEnchantments.OrderBy(e => e.Title.GetFormattedText()))
		{
			_enchantmentSelect.AddItem(enchantment.Title.GetFormattedText());
			_enchantmentIds.Add(enchantment.Id);
		}

		ModelId selectedId = _previewCard.Enchantment?.Id ?? ModelId.none;
		int index = _enchantmentIds.FindIndex(id => id == selectedId);
		if (index < 0)
		{
			index = 0;
		}
		_enchantmentSelect.Select(index);
		_enchantmentAmountField.Text = Math.Max(1, _previewCard.Enchantment?.Amount ?? 1).ToString(CultureInfo.InvariantCulture);
		_enchantmentSelect.ItemSelected += _ =>
		{
			UpdateAmountEnabled(_enchantmentSelect, _enchantmentAmountField, _enchantmentIds);
			QueuePreviewUpdate();
		};
		_enchantmentAmountField.TextChanged += _ => QueuePreviewUpdate();
		UpdateAmountEnabled(_enchantmentSelect, _enchantmentAmountField, _enchantmentIds);
	}

	private void PopulateExtraEffectEnchantmentSelect(OptionButton select, string? selectedIdText)
	{
		if (select == null || !GodotObject.IsInstanceValid(select))
		{
			return;
		}

		select.Clear();
		foreach (ModelId? enchantmentId in _enchantmentIds)
		{
			if (enchantmentId == null || enchantmentId == ModelId.none)
			{
				select.AddItem(CardEditorLoc.T("value.none", "None"));
				continue;
			}

			EnchantmentModel? enchantment = ModelDb.GetByIdOrNull<EnchantmentModel>(enchantmentId);
			select.AddItem(enchantment?.Title.GetFormattedText() ?? enchantmentId.ToString());
		}

		ModelId desiredId = ModelId.none;
		if (!string.IsNullOrWhiteSpace(selectedIdText))
		{
			try
			{
				desiredId = ModelId.Deserialize(selectedIdText.Trim());
			}
			catch
			{
				desiredId = ModelId.none;
			}
		}

		int index = _enchantmentIds.FindIndex(id => id == desiredId);
		if (index < 0)
		{
			index = 0;
		}
		select.Select(index);
	}

	private void PopulateAfflictions()
	{
		_afflictionSelect.Clear();
		_afflictionIds.Clear();

		_afflictionSelect.AddItem(CardEditorLoc.T("value.none", "None"));
		_afflictionIds.Add(ModelId.none);
		foreach (AfflictionModel affliction in ModelDb.DebugAfflictions.OrderBy(a => a.Title.GetFormattedText()))
		{
			_afflictionSelect.AddItem(affliction.Title.GetFormattedText());
			_afflictionIds.Add(affliction.Id);
		}

		ModelId selectedId = _previewCard.Affliction?.Id ?? ModelId.none;
		int index = _afflictionIds.FindIndex(id => id == selectedId);
		if (index < 0)
		{
			index = 0;
		}
		_afflictionSelect.Select(index);
		_afflictionAmountField.Text = Math.Max(1, _previewCard.Affliction?.Amount ?? 1).ToString(CultureInfo.InvariantCulture);
		_afflictionSelect.ItemSelected += _ =>
		{
			UpdateAmountEnabled(_afflictionSelect, _afflictionAmountField, _afflictionIds);
			QueuePreviewUpdate();
		};
		_afflictionAmountField.TextChanged += _ => QueuePreviewUpdate();
		UpdateAmountEnabled(_afflictionSelect, _afflictionAmountField, _afflictionIds);
	}

	private void UpdateAmountEnabled(OptionButton select, LineEdit amountField, List<ModelId?> ids)
	{
		int index = select.Selected;
		ModelId? modelId = (index >= 0 && index < ids.Count) ? ids[index] : null;
		bool enableAmount = modelId != null && modelId != ModelId.none;
		amountField.Editable = enableAmount;
		amountField.SelfModulate = enableAmount ? Colors.White : StsColors.gray;
		SetSpinEnabled(amountField, enableAmount);
	}

	private void QueuePreviewUpdate()
	{
		if (_suppressPreviewUpdate)
		{
			return;
		}
		if (_previewUpdateQueued)
		{
			return;
		}
		_previewUpdateQueued = true;
		Callable.From(UpdatePreviewFromUi).CallDeferred();
	}

	private void UpdatePreviewFromUi()
	{
		_previewUpdateQueued = false;
		if (_cardPreviewNode == null)
		{
			return;
		}
		CardModel canonical = ModelDb.GetById<CardModel>(_cardId);

		if (_isUpgradeEditor)
		{
			UpgradeBaseline baseline = GetUpgradeBaseline();
			CardUpgradeOverride draftUpgrade = BuildUpgradeOverrideFromUiDeltas(baseline);

			CardModel upgradedPreview = CardEditorOverrides.BuildPreview(canonical);
			bool prevSuppress = CardEditorOverrides.SuppressUpgradeOverrides;
			try
			{
				CardEditorOverrides.SuppressUpgradeOverrides = true;
				TryUpgradeForPreview(upgradedPreview);
			}
			finally
			{
				CardEditorOverrides.SuppressUpgradeOverrides = prevSuppress;
			}

			ApplyUpgradeOverridePreview(upgradedPreview, draftUpgrade, baseline);

			CardOverride storedBase = CardEditorOverrides.Get(_cardId) ?? new CardOverride();
			CardOverride draftForDescription = new CardOverride
			{
				EnergyCost = storedBase.EnergyCost,
				StarCost = storedBase.StarCost,
				ReplayCount = storedBase.ReplayCount,
				DynamicVarBaseValues = storedBase.DynamicVarBaseValues != null
					? new Dictionary<string, decimal>(storedBase.DynamicVarBaseValues, StringComparer.Ordinal)
					: null,
				FullArt = storedBase.FullArt,
				Finish = storedBase.Finish,
				ExtraEffects = storedBase.ExtraEffects,
				Upgrade = draftUpgrade
			};
			CardEditorUiState.SetDraftOverride(_cardId, draftForDescription);

			_previewCard = upgradedPreview;
			_cardPreviewNode.Model = upgradedPreview;
			_cardPreviewNode.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
			if (_cardNameLabel != null && GodotObject.IsInstanceValid(_cardNameLabel))
			{
				_cardNameLabel.Text = upgradedPreview.Title;
			}
			return;
		}

		CardModel preview = CardEditorOverrides.BuildPreview(canonical);
		CardOverride draft = BuildOverrideFromUi();
		CardEditorUiState.SetDraftOverride(_cardId, draft);
		CardEditorOverrides.ApplyOverrideToCard(preview, draft);
		_previewCard = preview;
		_cardPreviewNode.Model = preview;
		_cardPreviewNode.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
		if (_cardNameLabel != null && GodotObject.IsInstanceValid(_cardNameLabel))
		{
			_cardNameLabel.Text = preview.Title;
		}
	}

	private CardOverride BuildOverrideFromUi()
	{
		CardOverride overrideData = new CardOverride
		{
			Keywords = _keywordChecks.Where(kvp => kvp.Value.IsTicked).Select(kvp => kvp.Key).ToHashSet(),
			DynamicVarBaseValues = new Dictionary<string, decimal>()
		};

		if (!_isCreatedCard && _cardTypeSelect.Selected >= 0 && _cardTypeSelect.Selected < _cardTypes.Count)
		{
			CardType selectedType = _cardTypes[_cardTypeSelect.Selected];
			if (selectedType != CardType.None)
			{
				overrideData.CardType = selectedType;
			}
		}

		if (!_isCreatedCard
			&& !_isUpgradeEditor
			&& _targetTypeSelect != null
			&& _targetTypeSelect.Selected >= 0
			&& _targetTypeSelect.Selected < _targetTypeOptions.Count)
		{
			TargetType? selectedTarget = _targetTypeOptions[_targetTypeSelect.Selected];
			if (selectedTarget.HasValue && selectedTarget.Value != TargetType.None)
			{
				try
				{
					TargetType vanillaTargetType = ModelDb.GetById<CardModel>(_cardId).TargetType;
					if (selectedTarget.Value != vanillaTargetType)
					{
						overrideData.TargetType = selectedTarget.Value;
					}
				}
				catch
				{
					overrideData.TargetType = selectedTarget.Value;
				}
			}
		}

		// Compare to vanilla (no overrides/draft) so X-cost toggles persist even when the preview draft is active.
		bool vanillaEnergyX = false;
		bool vanillaStarX = false;
		string vanillaPoolTitle = string.Empty;
		CardRarity vanillaRarity = CardRarity.Common;
		string vanillaTitle = string.Empty;
		bool prevSuppress = CardEditorOverrides.SuppressAllOverrides;
		CardEditorOverrides.SuppressAllOverrides = true;
		try
		{
			CardModel canonicalForCost = ModelDb.GetById<CardModel>(_cardId);
			vanillaEnergyX = canonicalForCost.EnergyCost.CostsX;
			vanillaStarX = canonicalForCost.HasStarCostX;
			vanillaPoolTitle = canonicalForCost.Pool?.Title ?? string.Empty;
			vanillaRarity = canonicalForCost.Rarity;
			vanillaTitle = canonicalForCost.Title ?? string.Empty;
		}
		catch
		{
			vanillaEnergyX = false;
			vanillaStarX = false;
			vanillaPoolTitle = string.Empty;
			vanillaRarity = CardRarity.Common;
			vanillaTitle = string.Empty;
		}
		finally
		{
			CardEditorOverrides.SuppressAllOverrides = prevSuppress;
		}

		if (!_isCreatedCard && !_isUpgradeEditor)
		{
			if (_vanillaEnabledTickbox != null && !_vanillaEnabledTickbox.IsTicked)
			{
				overrideData.Enabled = false;
			}

			string desiredTitle = _vanillaTitleField?.Text?.Trim() ?? string.Empty;
			if (!string.IsNullOrWhiteSpace(desiredTitle)
				&& !string.Equals(desiredTitle, vanillaTitle, StringComparison.Ordinal))
			{
				overrideData.TitleOverride = desiredTitle;
			}

			if (_vanillaFullArtTickbox?.IsTicked == true)
			{
				overrideData.FullArt = true;
			}

			CardEditorVisualFinish finish = GetSelectedVanillaFinish();
			if (finish != CardEditorVisualFinish.None)
			{
				overrideData.Finish = finish;
			}

			if (_vanillaFinishParams.Count > 0)
			{
				overrideData.FinishParams = new Dictionary<string, float>(_vanillaFinishParams);
			}
		}

		if (!_isCreatedCard
			&& !_isUpgradeEditor
			&& _vanillaPoolSelect != null
			&& _vanillaPoolSelect.Selected >= 0
			&& _vanillaPoolSelect.Selected < _vanillaPoolOptions.Count)
		{
			string? selectedPoolTitle = _vanillaPoolOptions[_vanillaPoolSelect.Selected];
			if (!string.IsNullOrWhiteSpace(selectedPoolTitle)
				&& !string.Equals(selectedPoolTitle.Trim(), vanillaPoolTitle, StringComparison.OrdinalIgnoreCase))
			{
				overrideData.PoolTitle = selectedPoolTitle.Trim();
			}
		}

		if (!_isCreatedCard
			&& !_isUpgradeEditor
			&& _vanillaRaritySelect != null
			&& _vanillaRaritySelect.Selected >= 0
			&& _vanillaRaritySelect.Selected < _vanillaRarityOptions.Count)
		{
			CardRarity? selectedRarity = _vanillaRarityOptions[_vanillaRaritySelect.Selected];
			if (selectedRarity.HasValue
				&& selectedRarity.Value != CardRarity.None
				&& selectedRarity.Value != vanillaRarity)
			{
				overrideData.Rarity = selectedRarity.Value;
			}
		}

		if (!_isCreatedCard && !_isUpgradeEditor)
		{
			(ModelId? CardId, string? CustomFile) portraitSelection = GetSelectedVanillaPortraitSourceId();
			if (!string.IsNullOrWhiteSpace(portraitSelection.CustomFile))
			{
				overrideData.CustomPortraitFile = portraitSelection.CustomFile!.Trim();
				overrideData.PortraitSourceCardId = null;
			}
			else if (portraitSelection.CardId != null && portraitSelection.CardId != ModelId.none)
			{
				overrideData.PortraitSourceCardId = portraitSelection.CardId;
				overrideData.CustomPortraitFile = null;
			}
		}

		bool desiredEnergyX = _energyCostXTickbox?.IsTicked ?? false;
		if (desiredEnergyX != vanillaEnergyX)
		{
			overrideData.EnergyCostX = desiredEnergyX;
		}
		if (!desiredEnergyX)
		{
			string text = _energyCostField.Text ?? string.Empty;
			if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int energy))
			{
				overrideData.EnergyCost = energy;
			}
			else if (string.IsNullOrWhiteSpace(text))
			{
				overrideData.EnergyCost = -1;
			}
		}
		if (_starCostField != null)
		{
			bool desiredStarX = _starCostXTickbox?.IsTicked ?? false;
			if (desiredStarX != vanillaStarX)
			{
				overrideData.StarCostX = desiredStarX;
			}

			if (!desiredStarX)
			{
				string text = _starCostField.Text ?? string.Empty;
				if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int stars))
				{
					overrideData.StarCost = stars;
				}
				else if (string.IsNullOrWhiteSpace(text))
				{
					overrideData.StarCost = -1;
				}
			}
		}
		if (int.TryParse(_replayField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int replay))
		{
			overrideData.ReplayCount = replay;
		}
		if (_drawCostReductionField != null && int.TryParse(_drawCostReductionField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int reduction))
		{
			reduction = Math.Max(0, reduction);
			overrideData.DrawCostReduction = reduction == 1 ? null : reduction;
		}
		if (_resonanceEnemyStrengthLossField != null
			&& int.TryParse(_resonanceEnemyStrengthLossField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int resonanceLoss))
		{
			resonanceLoss = Math.Clamp(resonanceLoss, 0, 99);
			if (resonanceLoss != 1)
			{
				overrideData.DynamicVarBaseValues![CardEditorOverrideKeys.ResonanceEnemyStrengthLoss] = resonanceLoss;
			}
		}
		if (_sealedThroneStarsGainedField != null && int.TryParse(_sealedThroneStarsGainedField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sealedThroneStars))
		{
			overrideData.PowerAmounts ??= new Dictionary<ModelId, decimal>();
			overrideData.PowerAmounts[ModelDb.GetId<TheSealedThronePower>()] = sealedThroneStars;
		}
		if (_retainHandTurnsField != null && int.TryParse(_retainHandTurnsField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int retainHandTurns))
		{
			retainHandTurns = Math.Clamp(retainHandTurns, 0, 99);
			if (retainHandTurns != 1)
			{
				overrideData.PowerAmounts ??= new Dictionary<ModelId, decimal>();
				overrideData.PowerAmounts[ModelDb.GetId<RetainHandPower>()] = retainHandTurns;
			}
		}

		ApplyPowerDurationToOverride(_noDrawDurationSelect, _noDrawTurnsField, ModelDb.GetId<NoDrawPower>(), overrideData);
		ApplyPowerDurationToOverride(_conquerorDurationSelect, _conquerorTurnsField, ModelDb.GetId<ConquerorPower>(), overrideData);
		ApplyPowerDurationToOverride(_reflectDurationSelect, _reflectTurnsField, ModelDb.GetId<ReflectPower>(), overrideData);

		if (_hardcodedPowerAmountFields.Count > 0)
		{
			foreach ((ModelId powerId, LineEdit field) in _hardcodedPowerAmountFields)
			{
				if (!_hardcodedPowerAmountDefaults.TryGetValue(powerId, out int defaultValue))
				{
					continue;
				}
				if (!int.TryParse(field.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
				{
					continue;
				}

				if (value != defaultValue)
				{
					overrideData.PowerAmounts ??= new Dictionary<ModelId, decimal>();
					overrideData.PowerAmounts[powerId] = value;
				}
			}
		}

		foreach ((string key, LineEdit field) in _dynamicFields)
		{
			if (decimal.TryParse(field.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal value))
			{
				overrideData.DynamicVarBaseValues![key] = value;
			}
		}

		if (_enchantmentSelect.Selected >= 0 && _enchantmentSelect.Selected < _enchantmentIds.Count && _enchantmentIds[_enchantmentSelect.Selected] != null)
		{
			overrideData.EnchantmentId = _enchantmentIds[_enchantmentSelect.Selected];
			if (overrideData.EnchantmentId != ModelId.none && int.TryParse(_enchantmentAmountField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount))
			{
				overrideData.EnchantmentAmount = amount;
			}
		}

		if (_afflictionSelect.Selected >= 0 && _afflictionSelect.Selected < _afflictionIds.Count && _afflictionIds[_afflictionSelect.Selected] != null)
		{
			overrideData.AfflictionId = _afflictionIds[_afflictionSelect.Selected];
			if (overrideData.AfflictionId != ModelId.none && int.TryParse(_afflictionAmountField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount))
			{
				overrideData.AfflictionAmount = amount;
			}
		}

		if (_extraEffectRows.Count > 0)
		{
			List<CardExtraEffect> effects = new List<CardExtraEffect>();
			for (int rowIndex = 0; rowIndex < _extraEffectRows.Count; rowIndex++)
			{
				ExtraEffectRow row = _extraEffectRows[rowIndex];
				int kindIndex = GetSelectedExtraEffectDefinitionIndex(row);
				if (kindIndex < 0 || kindIndex >= CardEditorExtraEffects.Definitions.Count)
				{
					if (_isUpgradeEditor)
					{
						effects.Add(null!);
					}
					continue;
				}

				CardExtraEffectDefinition def = CardEditorExtraEffects.Definitions[kindIndex];
				CardExtraEffectKind resolvedKind = GetResolvedExtraEffectKind(row, def.Kind);
				bool amountIsX = row.AmountXTickbox != null && row.AmountXTickbox.Visible && row.AmountXTickbox.IsTicked;
				int amount = def.DefaultAmount;
				int amountXPlus = 0;
				if (amountIsX)
				{
					amountXPlus = ParseIntOrDefault(row.AmountField.Text, 0);
					amountXPlus = Math.Clamp(amountXPlus, 0, 999);

					string nonXText = row.AmountField.HasMeta("card_editor_prev_extra_effect_amount_nonx")
						? row.AmountField.GetMeta("card_editor_prev_extra_effect_amount_nonx").ToString()
						: amount.ToString(CultureInfo.InvariantCulture);

					if (!int.TryParse(nonXText, NumberStyles.Integer, CultureInfo.InvariantCulture, out amount)
						|| !CardEditorExtraEffects.IsValidEffectAmount(resolvedKind, amount))
					{
						amount = def.DefaultAmount;
					}

					if (!CardEditorExtraEffects.IsValidEffectAmount(resolvedKind, amount))
					{
						if (_isUpgradeEditor)
						{
							effects.Add(null!);
						}
						continue;
					}
				}
				else
				{
					if (!int.TryParse(row.AmountField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out amount)
						|| !CardEditorExtraEffects.IsValidEffectAmount(resolvedKind, amount))
					{
						if (_isUpgradeEditor)
						{
							effects.Add(null!);
						}
						continue;
					}
				}
				CardExtraEffectTarget target = def.DefaultTarget;
				if (row.AllowedTargets.Count > 0 && row.TargetSelect.Selected >= 0 && row.TargetSelect.Selected < row.AllowedTargets.Count)
				{
					target = row.AllowedTargets[row.TargetSelect.Selected];
				}

				bool asPower = row.PowerTickbox.Visible && row.PowerTickbox.IsTicked;
				CardExtraEffectTiming timing = asPower ? CardExtraEffectTiming.Immediate : GetSelectedTiming(row);
				int turns = 0;
				if (!asPower && timing != CardExtraEffectTiming.Immediate)
				{
					if (!int.TryParse(row.TurnsField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out turns) || turns < 0)
					{
						turns = 1;
					}
				}

				CardExtraEffectTrigger trigger = GetSelectedTrigger(row);
				if (resolvedKind is CardExtraEffectKind.CreatedCardsCostLess
					or CardExtraEffectKind.CreatedCardsUpgraded
					or CardExtraEffectKind.GeneratedCardsUpgraded
					or CardExtraEffectKind.CardsInPileUpgradedAura)
				{
					trigger = CardExtraEffectTrigger.OnPlay;
				}
				CardExtraEffectCardCostsLessMode cardCostsLessMode = IsSelfCardCostModifierKind(resolvedKind)
					? GetSelectedCardCostsLessMode(row)
					: CardExtraEffectCardCostsLessMode.Legacy;
				if (IsSelfCardCostModifierKind(resolvedKind)
					&& cardCostsLessMode == CardExtraEffectCardCostsLessMode.Passive)
				{
					trigger = CardExtraEffectTrigger.OnPlay;
				}

				// "For N turns" on a timed trigger auto-enables power routing � no Power tickbox required
				int triggerMaxFiresValue = Math.Max(0, ParseIntOrDefault(row.TriggerMaxFiresField?.Text, 0));
				int triggerMaxTurnsValue = Math.Max(0, ParseIntOrDefault(row.TriggerMaxTurnsField?.Text, 0));
				if (!asPower && triggerMaxFiresValue > 0 && trigger is CardExtraEffectTrigger.TurnBoundary
					or CardExtraEffectTrigger.StartOfTurn or CardExtraEffectTrigger.EndOfTurn or CardExtraEffectTrigger.EndOfTurnInHand
					or CardExtraEffectTrigger.StartOfEnemyTurn or CardExtraEffectTrigger.EndOfEnemyTurn)
				{
					asPower = true;
					timing = CardExtraEffectTiming.Immediate;
					turns = 0;
				}

				bool grantToCard = row.GrantTickbox.Visible && row.GrantTickbox.IsTicked;
				CardExtraEffectCardSelectionMode selectionMode = CardExtraEffectCardSelectionMode.Choose;
				CardExtraEffectCardPile selectionPile = CardExtraEffectCardPile.Hand;
				CardExtraEffectCardGrantDuration grantDuration = CardExtraEffectCardGrantDuration.ThisTurn;
				int grantTurns = 2;
				CardExtraEffectCardPile moveToPile = CardExtraEffectCardPile.DrawPile;
				CardExtraEffectCardPilePosition moveToPosition = CardExtraEffectCardPilePosition.Top;
				bool repeatIsX = row.RepeatRow != null && row.RepeatRow.Visible && row.RepeatXTickbox != null && row.RepeatXTickbox.IsTicked;
				int repeatCount = ParseIntOrDefault(row.RepeatCountField?.Text, 1);
				bool selectionCountIsX = grantToCard && row.GrantCountRow != null && row.GrantCountRow.Visible && row.GrantCountXTickbox != null && row.GrantCountXTickbox.IsTicked;
				int selectionCount = grantToCard && row.GrantCountRow != null && row.GrantCountRow.Visible
					? ParseIntOrDefault(row.GrantCountField?.Text, 1)
					: 1;
				CardGeneratedCardPool selectionPool = grantToCard ? GetSelectedGrantCountPool(row) : CardGeneratedCardPool.All;
				CardGeneratedCardType selectionType = grantToCard ? GetSelectedGrantCountType(row) : CardGeneratedCardType.Any;
				CardExtraEffectCountCardFilter selectionFilter = grantToCard ? GetSelectedGrantCountFilter(row) : CardExtraEffectCountCardFilter.Any;
				int drawCostLessDelta = 0;
				if (resolvedKind == CardExtraEffectKind.DrawCardsThatCostLess)
				{
					drawCostLessDelta = row.DrawCostField != null && GodotObject.IsInstanceValid(row.DrawCostField)
						? ParseIntOrDefault(row.DrawCostField.Text, 1)
						: 1;
					drawCostLessDelta = Math.Clamp(drawCostLessDelta, 1, 99);
				}

				if (def.Kind is CardExtraEffectKind.MoveCardsBetweenPiles
					or CardExtraEffectKind.UpgradeCardsInPile
					or CardExtraEffectKind.AddCopyOfThisCard
					or CardExtraEffectKind.PlayCardFromPile
					or CardExtraEffectKind.AddSpecificCardToHand
					or CardExtraEffectKind.DiscardCards
					or CardExtraEffectKind.ExhaustCards
					or CardExtraEffectKind.GrantKeywordToPile
					or CardExtraEffectKind.AutoPlaySelfFromPile
					or CardExtraEffectKind.AutoDrawSelfFromPile
					or CardExtraEffectKind.ConditionalAutoPlayFromPile
					or CardExtraEffectKind.ConditionalAutoDrawFromPile)
				{
					if (def.Kind is CardExtraEffectKind.MoveCardsBetweenPiles
						or CardExtraEffectKind.AddCopyOfThisCard
						or CardExtraEffectKind.AddSpecificCardToHand)
					{
						moveToPile = GetSelectedCardPile(row.MoveToPileSelect, CardExtraEffectCardPile.DrawPile);
						moveToPosition = GetSelectedCardPilePosition(row.MoveToPositionSelect, CardExtraEffectCardPilePosition.Top);
					}

					if (def.Kind is CardExtraEffectKind.MoveCardsBetweenPiles
						or CardExtraEffectKind.UpgradeCardsInPile
						or CardExtraEffectKind.PlayCardFromPile
						or CardExtraEffectKind.DiscardCards
						or CardExtraEffectKind.ExhaustCards
						or CardExtraEffectKind.GrantKeywordToPile
						or CardExtraEffectKind.AutoPlaySelfFromPile
						or CardExtraEffectKind.AutoDrawSelfFromPile
						or CardExtraEffectKind.ConditionalAutoPlayFromPile
						or CardExtraEffectKind.ConditionalAutoDrawFromPile)
					{
						selectionPile = GetSelectedCardPile(row.MoveFromPileSelect, CardExtraEffectCardPile.Hand);
						selectionMode = GetSelectedCardSelectionMode(row.MoveSelectionModeSelect, CardExtraEffectCardSelectionMode.Choose);
					}
				}
				else if (grantToCard)
				{
					selectionPile = GetSelectedCardPile(row.GrantPileSelect, CardExtraEffectCardPile.Hand);
					selectionMode = GetSelectedCardSelectionMode(row.GrantModeSelect, CardExtraEffectCardSelectionMode.Choose);
					grantDuration = GetSelectedCardGrantDuration(row.GrantDurationSelect, CardExtraEffectCardGrantDuration.ThisTurn);
					grantTurns = Math.Clamp(ParseIntOrDefault(row.GrantTurnsField.Text, 2), 1, 99);
				}

				string? enchantmentId = resolvedKind == CardExtraEffectKind.EnchantCard
					? GetSelectedExtraEffectEnchantmentId(row)
					: null;
				if (resolvedKind == CardExtraEffectKind.EnchantCard && string.IsNullOrWhiteSpace(enchantmentId))
				{
					continue;
				}
				CardExtraEffectEnchantmentDuration enchantmentDuration = resolvedKind == CardExtraEffectKind.EnchantCard
					? GetSelectedEnchantmentDuration(row.EnchantmentDurationSelect, CardExtraEffectEnchantmentDuration.ThisCombat)
					: CardExtraEffectEnchantmentDuration.ThisCombat;
				int enchantmentTurns = resolvedKind == CardExtraEffectKind.EnchantCard
					? Math.Clamp(ParseIntOrDefault(row.EnchantmentTurnsField.Text, 2), 1, 99)
					: 1;

				if (resolvedKind == CardExtraEffectKind.CardsInPileUpgradedAura)
				{
					selectionPile = GetSelectedCardPile(row.UpgradePileSelect, CardExtraEffectCardPile.AllPiles);
				}

				bool isCardTypeCostAura = IsCardTypeCostModifierKind(resolvedKind);
				bool isDrawnGeneratedCost = IsDrawnGeneratedCostModifierKind(resolvedKind);
				bool isUnifiedUpgrade = resolvedKind is CardExtraEffectKind.CreatedCardsUpgraded
					or CardExtraEffectKind.GeneratedCardsUpgraded
					or CardExtraEffectKind.CardsInPileUpgradedAura;

				CardExtraEffectOrbAction orbAction = resolvedKind == CardExtraEffectKind.OrbAction ? GetSelectedOrbAction(row) : CardExtraEffectOrbAction.Evoke;
				CardExtraEffectOrbScope orbScope = resolvedKind == CardExtraEffectKind.OrbAction ? GetSelectedOrbScope(row) : CardExtraEffectOrbScope.Fixed;
				CardExtraEffectOrbType orbType = resolvedKind == CardExtraEffectKind.OrbAction ? GetSelectedOrbType(row) : CardExtraEffectOrbType.Any;
				CardExtraEffectOrbSelection orbSelection = resolvedKind == CardExtraEffectKind.OrbAction ? GetSelectedOrbSelection(row) : CardExtraEffectOrbSelection.Leftmost;
				CardExtraEffectOrbFollowUp orbFollowUp = resolvedKind == CardExtraEffectKind.OrbAction && orbAction == CardExtraEffectOrbAction.Evoke
					? GetSelectedOrbFollowUp(row)
					: CardExtraEffectOrbFollowUp.None;
				CardExtraEffectMultiplierStat multiplierStat = resolvedKind == CardExtraEffectKind.MultiplyStatStatus
					? GetSelectedMultiplierStat(row)
					: CardExtraEffectMultiplierStat.Strength;
				var drawnFromPile = (resolvedKind == CardExtraEffectKind.DrawnCardsCostLess && row.DrawnFromPileSelect != null && GodotObject.IsInstanceValid(row.DrawnFromPileSelect))
					? row.DrawnFromPileSelect.Selected switch { 1 => CardExtraEffectCardPile.DrawPile, 2 => CardExtraEffectCardPile.DiscardPile, 3 => CardExtraEffectCardPile.ExhaustPile, _ => CardExtraEffectCardPile.AllPiles }
					: CardExtraEffectCardPile.AllPiles;

				effects.Add(new CardExtraEffect
				{
					Kind = resolvedKind,
					Target = target,
					Amount = amount,
					AmountIsX = amountIsX,
					AmountXPlus = amountIsX ? amountXPlus : 0,
					Trigger = trigger,
					PowerTriggerCountEvent = GetSelectedPowerTriggerCountEvent(row),
					TurnBoundary = trigger == CardExtraEffectTrigger.TurnBoundary ? GetSelectedTurnBoundaryEdge(row) : CardExtraEffectTurnBoundary.End,
					TurnBoundarySide = trigger == CardExtraEffectTrigger.TurnBoundary ? GetSelectedTurnBoundarySide(row) : CardExtraEffectTurnBoundarySide.YourTurn,
					TurnBoundaryCardLocation = trigger == CardExtraEffectTrigger.TurnBoundary ? GetSelectedTurnBoundaryLocation(row) : CardExtraEffectTurnBoundaryCardLocation.Any,
					Timing = timing,
					Turns = turns,
					Duration = GetSelectedDuration(row),
					AsPower = asPower,
					TriggerCardPool = (asPower || isCardTypeCostAura || isDrawnGeneratedCost || isUnifiedUpgrade) ? GetSelectedTriggerCardPool(row) : CardGeneratedCardPool.All,
					TriggerCardType = (asPower || isCardTypeCostAura || isDrawnGeneratedCost || isUnifiedUpgrade) ? GetSelectedTriggerCardType(row) : CardGeneratedCardType.Any,
					TriggerCardFilter = (asPower || isUnifiedUpgrade) ? GetSelectedTriggerCardFilter(row) : CardExtraEffectCountCardFilter.Any,
					TriggerEveryN = asPower ? Math.Max(1, ParseIntOrDefault(row.TriggerEveryNField?.Text, 1)) : 0,
					TriggerMaxFires = asPower ? triggerMaxFiresValue : 0,
					TriggerMaxTurns = asPower ? triggerMaxTurnsValue : 0,
					CreatedCardsCostDuration = GetSelectedCreatedCardsCostDuration(row),
					CreatedCardsCostTurns = ParseIntOrDefault(row.CreatedCostTurnsField.Text, 1),
					CardCostsLessDuration = GetSelectedCardCostsLessDuration(row),
					CardCostsLessTurns = ParseIntOrDefault(row.CardCostsLessTurnsField.Text, 1),
					CardCostsLessMode = cardCostsLessMode,
					GeneratedCardPool = GetSelectedGeneratedCardPool(row),
					GeneratedCardType = GetSelectedGeneratedCardType(row),
					ScaleMode = GetSelectedScaleMode(row, resolvedKind),
					CountEvent = GetSelectedCountEvent(row),
					CountWindow = GetSelectedCountWindow(row),
					CountWindowInclusion = GetSelectedCountWindowInclusion(row),
					BlockLostCountingMode = GetSelectedBlockLostCountingMode(row),
					CountTurns = GetSelectedCountTurns(row),
					CountComparison = GetSelectedCountComparison(row),
					CountConditionAmount = GetSelectedCountConditionAmount(row),
					CountCardPile = row.CountPileSelect != null && row.CountPileSelect.Visible
						? GetSelectedCardPile(row.CountPileSelect, CardExtraEffectCardPile.Hand)
						: CardExtraEffectCardPile.Hand,
					CountCardPool = GetSelectedCountPool(row),
					CountCardType = GetSelectedCountType(row),
					CountCardFilter = GetSelectedCountFilter(row),
					CountOrbType = GetSelectedCountOrbType(row),
					CountOrbSelection = GetSelectedCountOrbSelection(row),
					CountEnemyStatus = GetSelectedCountEnemyStatus(row),
					CountEnemyIntent = GetSelectedCountEnemyIntent(row),
					CountOnlyBlockCards = GetSelectedCountFilter(row) == CardExtraEffectCountCardFilter.GainBlock,
					HistoryScalingIncludesBase = row.ScalingBaseTickbox != null && row.ScalingBaseTickbox.Visible && row.ScalingBaseTickbox.IsTicked,
					RepeatIsX = repeatIsX,
					RepeatCount = repeatCount,
					GrantToCard = grantToCard,
					CardSelectionMode = selectionMode,
					CardSelectionPile = selectionPile,
					CardGrantDuration = grantDuration,
					CardGrantTurns = grantTurns,
					EnchantmentId = enchantmentId,
					EnchantmentDuration = enchantmentDuration,
					EnchantmentTurns = enchantmentTurns,
					CardSelectionCountIsX = selectionCountIsX,
					CardSelectionCount = resolvedKind == CardExtraEffectKind.DrawCardsThatCostLess ? drawCostLessDelta : selectionCount,
					CardSelectionPool = selectionPool,
					CardSelectionType = selectionType,
					CardSelectionFilter = selectionFilter,
					MoveToPile = moveToPile,
					MoveToPosition = moveToPosition,
					OrbAction = orbAction,
					OrbScope = orbScope,
					OrbType = orbType,
					OrbSelection = orbSelection,
					OrbFollowUp = orbFollowUp,
					MultiplierStat = multiplierStat,
					DrawnFromPile = drawnFromPile,
					SpecificCardId = (resolvedKind is CardExtraEffectKind.AddSpecificCardToHand or CardExtraEffectKind.FetchSpecificCardToHand)
						? (string.IsNullOrWhiteSpace(row.SpecificCardIdField?.Text) ? null : row.SpecificCardIdField.Text.Trim())
						: null,
					OstyAction = resolvedKind == CardExtraEffectKind.OstyAction ? GetSelectedOstyAction(row) : default,
					GrantedKeyword = resolvedKind == CardExtraEffectKind.GrantKeywordToPile ? GetSelectedGrantedKeyword(row) : default,
					CostFilterEnabled = row.CostFilterTickbox != null && GodotObject.IsInstanceValid(row.CostFilterTickbox) && row.CostFilterTickbox.IsTicked,
					CostFilterMax = row.CostFilterField != null && GodotObject.IsInstanceValid(row.CostFilterField) ? ParseIntOrDefault(row.CostFilterField.Text, 0) : 0
				});
			}
			if (effects.Any(e => e != null))
			{
				overrideData.ExtraEffects = effects;
			}
		}

		if (_slyGrantDurationSelect != null)
		{
			int selected = _slyGrantDurationSelect.Selected;
			if (selected == 1)
			{
				overrideData.SlyGrantDuration = CardKeywordGrantDuration.ThisCombat;
				overrideData.SlyGrantTurns = null;
			}
			else if (selected == 2)
			{
				overrideData.SlyGrantDuration = CardKeywordGrantDuration.Turns;
				int turns = 2;
				if (_slyGrantTurnsField != null && int.TryParse(_slyGrantTurnsField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedTurns))
				{
					turns = Math.Clamp(parsedTurns, 1, 99);
				}
				overrideData.SlyGrantTurns = turns;
			}
			else
			{
				overrideData.SlyGrantDuration = null;
				overrideData.SlyGrantTurns = null;
			}
		}

		ApplyTemporaryStatDurationToOverride(_tempStrengthDurationSelect, _tempStrengthTurnsField, out CardKeywordGrantDuration? tempStrDuration, out int? tempStrTurns);
		overrideData.TemporaryStrengthDuration = tempStrDuration;
		overrideData.TemporaryStrengthTurns = tempStrTurns;

		ApplyTemporaryStatDurationToOverride(_tempDexterityDurationSelect, _tempDexterityTurnsField, out CardKeywordGrantDuration? tempDexDuration, out int? tempDexTurns);
		overrideData.TemporaryDexterityDuration = tempDexDuration;
		overrideData.TemporaryDexterityTurns = tempDexTurns;

		ApplyTemporaryStatDurationToOverride(_tempFocusDurationSelect, _tempFocusTurnsField, out CardKeywordGrantDuration? tempFocusDuration, out int? tempFocusTurns);
		overrideData.TemporaryFocusDuration = tempFocusDuration;
		overrideData.TemporaryFocusTurns = tempFocusTurns;

		return overrideData;
	}

	private void ApplyPowerDurationToOverride(OptionButton? durationSelect, LineEdit? turnsField, ModelId powerId, CardOverride overrideData)
	{
		if (durationSelect == null)
		{
			return;
		}
		if (overrideData == null)
		{
			return;
		}

		int selected = durationSelect.Selected;
		if (selected == 0)
		{
			return;
		}

		int turns = 99;
		if (selected == 2)
		{
			if (!int.TryParse(turnsField?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out turns))
			{
				turns = 2;
			}
			turns = Math.Clamp(turns, 0, 99);
			if (turns == 0)
			{
				overrideData.PowerAmounts ??= new Dictionary<ModelId, decimal>();
				overrideData.PowerAmounts[powerId] = 0;
				return;
			}
			if (turns <= 1)
			{
				return;
			}
		}

		overrideData.PowerAmounts ??= new Dictionary<ModelId, decimal>();
		overrideData.PowerAmounts[powerId] = turns;
	}

	private static void ApplyTemporaryStatDurationToOverride(
		OptionButton? durationSelect,
		LineEdit? turnsField,
		out CardKeywordGrantDuration? duration,
		out int? turns)
	{
		duration = null;
		turns = null;

		if (durationSelect == null)
		{
			return;
		}

		int selected = durationSelect.Selected;
		if (selected == 1)
		{
			duration = CardKeywordGrantDuration.ThisCombat;
			return;
		}
		if (selected != 2)
		{
			return;
		}

		duration = CardKeywordGrantDuration.Turns;
		int parsed = 2;
		if (turnsField != null && int.TryParse(turnsField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedTurns))
		{
			parsed = Math.Clamp(parsedTurns, 1, 99);
		}
		turns = parsed;
	}

	private UpgradeBaseline GetUpgradeBaseline()
	{
		if (_upgradeBaseline != null)
		{
			return _upgradeBaseline;
		}

		CardModel canonical = ModelDb.GetById<CardModel>(_cardId);

		CardModel baseCard = canonical.ToMutable();
		if (_isCreatedCard)
		{
			CardEditorCreatedCardEffectSourceSupport.EnsureEffectSourceDynamicVars(baseCard, isUpgradePreview: false);
		}
		int baseEnergyCost = baseCard.EnergyCost.GetWithModifiers(CostModifiers.None);
		bool baseEnergyCostsX = baseCard.EnergyCost.CostsX;
		int baseStarCost = baseCard.BaseStarCost;
		bool baseHasStarCostX = baseCard.HasStarCostX;
		int baseReplayCount = baseCard.BaseReplayCount;
		Dictionary<string, decimal> baseVars = baseCard.DynamicVars.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.BaseValue, StringComparer.Ordinal);

		CardModel vanillaUpgraded = canonical.ToMutable();
		bool prevSuppress = CardEditorOverrides.SuppressUpgradeOverrides;
		try
		{
			CardEditorOverrides.SuppressUpgradeOverrides = true;
			TryUpgradeForPreview(vanillaUpgraded);
		}
		finally
		{
			CardEditorOverrides.SuppressUpgradeOverrides = prevSuppress;
		}

		if (_isCreatedCard)
		{
			CardEditorCreatedCardEffectSourceSupport.EnsureEffectSourceDynamicVars(vanillaUpgraded, isUpgradePreview: true);
		}

		int vanillaEnergyCost = vanillaUpgraded.EnergyCost.GetWithModifiers(CostModifiers.None);
		int vanillaStarCost = vanillaUpgraded.BaseStarCost;
		int vanillaReplayCount = vanillaUpgraded.BaseReplayCount;
		Dictionary<string, decimal> vanillaVars = vanillaUpgraded.DynamicVars.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.BaseValue, StringComparer.Ordinal);

		Dictionary<string, decimal> vanillaVarDeltas = new Dictionary<string, decimal>(StringComparer.Ordinal);
		foreach ((string key, decimal baseValue) in baseVars)
		{
			if (vanillaVars.TryGetValue(key, out decimal upgradedValue))
			{
				vanillaVarDeltas[key] = upgradedValue - baseValue;
			}
		}

		_upgradeBaseline = new UpgradeBaseline
		{
			BaseEnergyCost = baseEnergyCost,
			BaseEnergyCostsX = baseEnergyCostsX,
			VanillaUpgradedEnergyCost = vanillaEnergyCost,
			VanillaEnergyDelta = vanillaEnergyCost - baseEnergyCost,

			BaseStarCost = baseStarCost,
			BaseHasStarCostX = baseHasStarCostX,
			VanillaUpgradedStarCost = vanillaStarCost,
			VanillaStarDelta = vanillaStarCost - baseStarCost,

			BaseReplayCount = baseReplayCount,
			VanillaUpgradedReplayCount = vanillaReplayCount,
			VanillaReplayDelta = vanillaReplayCount - baseReplayCount,

			BaseVars = baseVars,
			VanillaVarDeltas = vanillaVarDeltas,
			VanillaUpgradedKeywords = new HashSet<CardKeyword>(vanillaUpgraded.Keywords)
		};

		return _upgradeBaseline;
	}

	private CardUpgradeOverride BuildUpgradeOverrideFromUiDeltas(UpgradeBaseline baseline)
	{
		CardUpgradeOverride upgrade = new CardUpgradeOverride();
		int baseExtraEffectCount = CardEditorOverrides.Get(_cardId)?.ExtraEffects?.Count ?? 0;

		if (!baseline.BaseEnergyCostsX && int.TryParse(_energyCostField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int desiredEnergyDelta))
		{
			if (desiredEnergyDelta != baseline.VanillaEnergyDelta)
			{
				upgrade.EnergyCostDelta = desiredEnergyDelta;
			}
		}

		if (_starCostField != null
			&& !baseline.BaseHasStarCostX
			&& int.TryParse(_starCostField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int desiredStarDelta))
		{
			if (desiredStarDelta != baseline.VanillaStarDelta)
			{
				upgrade.StarCostDelta = desiredStarDelta;
			}
		}

		if (int.TryParse(_replayField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int desiredReplayDelta))
		{
			if (desiredReplayDelta != baseline.VanillaReplayDelta)
			{
				upgrade.ReplayCountDelta = desiredReplayDelta;
			}
		}

		if (_dynamicFields.Count > 0)
		{
			Dictionary<string, decimal> deltas = new Dictionary<string, decimal>(StringComparer.Ordinal);
			foreach ((string key, LineEdit field) in _dynamicFields)
			{
				if (!decimal.TryParse(field.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal desiredDelta))
				{
					continue;
				}
				decimal vanillaDelta = baseline.VanillaVarDeltas.TryGetValue(key, out decimal delta) ? delta : 0m;
				if (desiredDelta != vanillaDelta)
				{
					deltas[key] = desiredDelta;
				}
			}
			if (deltas.Count > 0)
			{
				upgrade.DynamicVarDeltas = deltas;
			}
		}

		HashSet<CardKeyword> desiredKeywords = _keywordChecks.Where(kvp => kvp.Value.IsTicked).Select(kvp => kvp.Key).ToHashSet();
		HashSet<CardKeyword> toRemove = new HashSet<CardKeyword>(baseline.VanillaUpgradedKeywords);
		toRemove.ExceptWith(desiredKeywords);
		HashSet<CardKeyword> toAdd = new HashSet<CardKeyword>(desiredKeywords);
		toAdd.ExceptWith(baseline.VanillaUpgradedKeywords);

		if (toRemove.Count > 0)
		{
			upgrade.KeywordsToRemove = toRemove;
		}
		if (toAdd.Count > 0)
		{
			upgrade.KeywordsToAdd = toAdd;
		}

		if (_extraEffectRows.Count > 0)
		{
			List<CardExtraEffect> effects = new List<CardExtraEffect>();
			for (int rowIndex = 0; rowIndex < _extraEffectRows.Count; rowIndex++)
			{
				ExtraEffectRow row = _extraEffectRows[rowIndex];
				int kindIndex = GetSelectedExtraEffectDefinitionIndex(row);
				if (kindIndex < 0 || kindIndex >= CardEditorExtraEffects.Definitions.Count)
				{
					effects.Add(null!);
					continue;
				}
				CardExtraEffectDefinition def = CardEditorExtraEffects.Definitions[kindIndex];
				CardExtraEffectKind resolvedKind = GetResolvedExtraEffectKind(row, def.Kind);
				bool isDeltaRow = _isUpgradeEditor && rowIndex < baseExtraEffectCount;
				bool disableOnUpgrade = isDeltaRow
					&& row.DisableOnUpgradeTickbox != null
					&& row.DisableOnUpgradeTickbox.Visible
					&& row.DisableOnUpgradeTickbox.IsTicked;
				bool amountIsX = row.AmountXTickbox != null && row.AmountXTickbox.Visible && row.AmountXTickbox.IsTicked;
				int amount = def.DefaultAmount;
				bool hasValidAmount = false;
				if (!amountIsX)
				{
					if (isDeltaRow)
					{
						amount = ParseIntOrDefault(row.AmountField?.Text, 0);
						hasValidAmount = true;
					}
					else
					{
						bool parsedAmount = int.TryParse(row.AmountField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out amount);
						hasValidAmount = parsedAmount && CardEditorExtraEffects.IsValidEffectAmount(resolvedKind, amount);
					}
					if (disableOnUpgrade)
					{
						amount = 0;
						hasValidAmount = true;
					}
				}
				else if (!isDeltaRow)
				{
					if (!CardEditorExtraEffects.IsValidEffectAmount(resolvedKind, amount))
					{
						amount = 1;
					}
					hasValidAmount = CardEditorExtraEffects.IsValidEffectAmount(resolvedKind, amount);
				}
				if (!hasValidAmount)
				{
					effects.Add(null!);
					continue;
				}
				CardExtraEffectTarget target = def.DefaultTarget;
				if (row.AllowedTargets.Count > 0 && row.TargetSelect.Selected >= 0 && row.TargetSelect.Selected < row.AllowedTargets.Count)
				{
					target = row.AllowedTargets[row.TargetSelect.Selected];
				}

				bool asPower = row.PowerTickbox.Visible && row.PowerTickbox.IsTicked;
				CardExtraEffectTiming timing = asPower ? CardExtraEffectTiming.Immediate : GetSelectedTiming(row);
				int turns = 0;
				if (!asPower && timing != CardExtraEffectTiming.Immediate)
				{
					if (isDeltaRow)
					{
						turns = ParseExtraEffectNumericField(row.TurnsField, absoluteDefault: 1, isDeltaRow: true, minAbsolute: 0, maxAbsolute: 99);
					}
					else if (!int.TryParse(row.TurnsField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out turns) || turns < 0)
					{
						turns = 1;
					}
				}

				CardExtraEffectTrigger trigger = GetSelectedTrigger(row);
				if (resolvedKind is CardExtraEffectKind.CreatedCardsCostLess
					or CardExtraEffectKind.CreatedCardsUpgraded
					or CardExtraEffectKind.GeneratedCardsUpgraded
					or CardExtraEffectKind.CardsInPileUpgradedAura)
				{
					trigger = CardExtraEffectTrigger.OnPlay;
				}

				CardExtraEffectCardCostsLessMode cardCostsLessMode = IsSelfCardCostModifierKind(resolvedKind)
					? GetSelectedCardCostsLessMode(row)
					: CardExtraEffectCardCostsLessMode.Legacy;
				if (IsSelfCardCostModifierKind(resolvedKind)
					&& cardCostsLessMode == CardExtraEffectCardCostsLessMode.Passive)
				{
					trigger = CardExtraEffectTrigger.OnPlay;
				}

				// "For N turns" on a timed trigger auto-enables power routing � no Power tickbox required
				int triggerMaxFiresValue = ParseExtraEffectNumericField(row.TriggerMaxFiresField, absoluteDefault: 0, isDeltaRow, minAbsolute: 0, maxAbsolute: 999);
				int triggerMaxTurnsValue = ParseExtraEffectNumericField(row.TriggerMaxTurnsField, absoluteDefault: 0, isDeltaRow, minAbsolute: 0, maxAbsolute: 999);
				if (!asPower && triggerMaxFiresValue > 0 && trigger is CardExtraEffectTrigger.TurnBoundary
					or CardExtraEffectTrigger.StartOfTurn or CardExtraEffectTrigger.EndOfTurn or CardExtraEffectTrigger.EndOfTurnInHand
					or CardExtraEffectTrigger.StartOfEnemyTurn or CardExtraEffectTrigger.EndOfEnemyTurn)
				{
					asPower = true;
					timing = CardExtraEffectTiming.Immediate;
					turns = 0;
				}

				bool grantToCard = row.GrantTickbox.Visible && row.GrantTickbox.IsTicked;
				CardExtraEffectCardSelectionMode selectionMode = CardExtraEffectCardSelectionMode.Choose;
				CardExtraEffectCardPile selectionPile = CardExtraEffectCardPile.Hand;
				CardExtraEffectCardGrantDuration grantDuration = CardExtraEffectCardGrantDuration.ThisTurn;
				int grantTurns = isDeltaRow ? 0 : 2;
				CardExtraEffectCardPile moveToPile = CardExtraEffectCardPile.DrawPile;
				CardExtraEffectCardPilePosition moveToPosition = CardExtraEffectCardPilePosition.Top;
				bool repeatIsX = row.RepeatRow != null && row.RepeatRow.Visible && row.RepeatXTickbox != null && row.RepeatXTickbox.IsTicked;
				int repeatCount = ParseExtraEffectNumericField(row.RepeatCountField, absoluteDefault: 1, isDeltaRow, minAbsolute: 1, maxAbsolute: 99);
				bool selectionCountIsX = grantToCard && row.GrantCountRow != null && row.GrantCountRow.Visible && row.GrantCountXTickbox != null && row.GrantCountXTickbox.IsTicked;
				int selectionCount = grantToCard && row.GrantCountRow != null && row.GrantCountRow.Visible
					? ParseExtraEffectNumericField(row.GrantCountField, absoluteDefault: 1, isDeltaRow, minAbsolute: 0, maxAbsolute: 99)
					: (isDeltaRow ? 0 : 1);
				CardGeneratedCardPool selectionPool = grantToCard ? GetSelectedGrantCountPool(row) : CardGeneratedCardPool.All;
				CardGeneratedCardType selectionType = grantToCard ? GetSelectedGrantCountType(row) : CardGeneratedCardType.Any;
				CardExtraEffectCountCardFilter selectionFilter = grantToCard ? GetSelectedGrantCountFilter(row) : CardExtraEffectCountCardFilter.Any;
				int drawCostLessDelta = 0;
				if (resolvedKind == CardExtraEffectKind.DrawCardsThatCostLess)
				{
					drawCostLessDelta = row.DrawCostField != null && GodotObject.IsInstanceValid(row.DrawCostField)
						? ParseIntOrDefault(row.DrawCostField.Text, 1)
						: 1;
					drawCostLessDelta = Math.Clamp(drawCostLessDelta, 1, 99);
				}

				if (def.Kind is CardExtraEffectKind.MoveCardsBetweenPiles
					or CardExtraEffectKind.UpgradeCardsInPile
					or CardExtraEffectKind.AddCopyOfThisCard
					or CardExtraEffectKind.AddSpecificCardToHand
					or CardExtraEffectKind.PlayCardFromPile
					or CardExtraEffectKind.DiscardCards
					or CardExtraEffectKind.ExhaustCards
					or CardExtraEffectKind.GrantKeywordToPile
					or CardExtraEffectKind.AutoPlaySelfFromPile
					or CardExtraEffectKind.AutoDrawSelfFromPile
					or CardExtraEffectKind.ConditionalAutoPlayFromPile
					or CardExtraEffectKind.ConditionalAutoDrawFromPile)
				{
					if (def.Kind is CardExtraEffectKind.MoveCardsBetweenPiles
						or CardExtraEffectKind.AddCopyOfThisCard
						or CardExtraEffectKind.AddSpecificCardToHand)
					{
						moveToPile = GetSelectedCardPile(row.MoveToPileSelect, CardExtraEffectCardPile.DrawPile);
						moveToPosition = GetSelectedCardPilePosition(row.MoveToPositionSelect, CardExtraEffectCardPilePosition.Top);
					}

					if (def.Kind is CardExtraEffectKind.MoveCardsBetweenPiles
						or CardExtraEffectKind.UpgradeCardsInPile
						or CardExtraEffectKind.PlayCardFromPile
						or CardExtraEffectKind.DiscardCards
						or CardExtraEffectKind.ExhaustCards
						or CardExtraEffectKind.GrantKeywordToPile
						or CardExtraEffectKind.AutoPlaySelfFromPile
						or CardExtraEffectKind.AutoDrawSelfFromPile
						or CardExtraEffectKind.ConditionalAutoPlayFromPile
						or CardExtraEffectKind.ConditionalAutoDrawFromPile)
					{
						selectionPile = GetSelectedCardPile(row.MoveFromPileSelect, CardExtraEffectCardPile.Hand);
						selectionMode = GetSelectedCardSelectionMode(row.MoveSelectionModeSelect, CardExtraEffectCardSelectionMode.Choose);
					}
				}
				else if (grantToCard)
				{
					selectionPile = GetSelectedCardPile(row.GrantPileSelect, CardExtraEffectCardPile.Hand);
					selectionMode = GetSelectedCardSelectionMode(row.GrantModeSelect, CardExtraEffectCardSelectionMode.Choose);
					grantDuration = GetSelectedCardGrantDuration(row.GrantDurationSelect, CardExtraEffectCardGrantDuration.ThisTurn);
					grantTurns = ParseExtraEffectNumericField(row.GrantTurnsField, absoluteDefault: 2, isDeltaRow, minAbsolute: 1, maxAbsolute: 99);
				}

				string? enchantmentId = resolvedKind == CardExtraEffectKind.EnchantCard
					? GetSelectedExtraEffectEnchantmentId(row)
					: null;
				if (resolvedKind == CardExtraEffectKind.EnchantCard && string.IsNullOrWhiteSpace(enchantmentId))
				{
					effects.Add(null!);
					continue;
				}
				CardExtraEffectEnchantmentDuration enchantmentDuration = resolvedKind == CardExtraEffectKind.EnchantCard
					? GetSelectedEnchantmentDuration(row.EnchantmentDurationSelect, CardExtraEffectEnchantmentDuration.ThisCombat)
					: CardExtraEffectEnchantmentDuration.ThisCombat;
				int enchantmentTurns = resolvedKind == CardExtraEffectKind.EnchantCard
					? ParseExtraEffectNumericField(row.EnchantmentTurnsField, absoluteDefault: 2, isDeltaRow, minAbsolute: 1, maxAbsolute: 99)
					: 1;

				if (resolvedKind == CardExtraEffectKind.CardsInPileUpgradedAura)
				{
					selectionPile = GetSelectedCardPile(row.UpgradePileSelect, CardExtraEffectCardPile.AllPiles);
				}

				bool isCardTypeCostAura = IsCardTypeCostModifierKind(resolvedKind);
				bool isDrawnGeneratedCost = IsDrawnGeneratedCostModifierKind(resolvedKind);
				bool isUnifiedUpgrade = resolvedKind is CardExtraEffectKind.CreatedCardsUpgraded
					or CardExtraEffectKind.GeneratedCardsUpgraded
					or CardExtraEffectKind.CardsInPileUpgradedAura;
				CardExtraEffectOrbAction orbAction = resolvedKind == CardExtraEffectKind.OrbAction ? GetSelectedOrbAction(row) : CardExtraEffectOrbAction.Evoke;
				CardExtraEffectOrbScope orbScope = resolvedKind == CardExtraEffectKind.OrbAction ? GetSelectedOrbScope(row) : CardExtraEffectOrbScope.Fixed;
				CardExtraEffectOrbType orbType = resolvedKind == CardExtraEffectKind.OrbAction ? GetSelectedOrbType(row) : CardExtraEffectOrbType.Any;
				CardExtraEffectOrbSelection orbSelection = resolvedKind == CardExtraEffectKind.OrbAction ? GetSelectedOrbSelection(row) : CardExtraEffectOrbSelection.Leftmost;
				CardExtraEffectOrbFollowUp orbFollowUp = resolvedKind == CardExtraEffectKind.OrbAction && orbAction == CardExtraEffectOrbAction.Evoke
					? GetSelectedOrbFollowUp(row)
					: CardExtraEffectOrbFollowUp.None;
				CardExtraEffectMultiplierStat multiplierStat = resolvedKind == CardExtraEffectKind.MultiplyStatStatus
					? GetSelectedMultiplierStat(row)
					: CardExtraEffectMultiplierStat.Strength;
				var drawnFromPile = (resolvedKind == CardExtraEffectKind.DrawnCardsCostLess && row.DrawnFromPileSelect != null && GodotObject.IsInstanceValid(row.DrawnFromPileSelect))
					? row.DrawnFromPileSelect.Selected switch { 1 => CardExtraEffectCardPile.DrawPile, 2 => CardExtraEffectCardPile.DiscardPile, 3 => CardExtraEffectCardPile.ExhaustPile, _ => CardExtraEffectCardPile.AllPiles }
					: CardExtraEffectCardPile.AllPiles;
				bool isMergedSelfPileAuto = def.Kind is CardExtraEffectKind.ConditionalAutoPlayFromPile or CardExtraEffectKind.ConditionalAutoDrawFromPile;
				CardExtraEffectScaleMode scaleMode = GetSelectedScaleMode(row, resolvedKind);
				CardExtraEffectCountComparison countComparison = GetSelectedCountComparison(row);
				int countConditionAmount = isDeltaRow
					? ParseExtraEffectNumericField(row.CountConditionField, absoluteDefault: 0, isDeltaRow: true, minAbsolute: 0, maxAbsolute: 999)
					: GetSelectedCountConditionAmount(row);
				int savedAmount = amount;
				bool savedAmountIsX = amountIsX;
				int savedAmountXPlus = 0;
				if (isMergedSelfPileAuto)
				{
					savedAmount = 1;
					savedAmountIsX = false;
					savedAmountXPlus = 0;
					if (scaleMode == CardExtraEffectScaleMode.None)
					{
						countComparison = CardExtraEffectCountComparison.None;
						countConditionAmount = 0;
					}
				}

				CardExtraEffect upgradeEffect = new CardExtraEffect
				{
					Kind = resolvedKind,
					Target = target,
					Amount = savedAmount,
					AmountIsX = savedAmountIsX,
					DisableOnUpgrade = disableOnUpgrade,
					AmountXPlus = savedAmountXPlus,
					Trigger = trigger,
					PowerTriggerCountEvent = GetSelectedPowerTriggerCountEvent(row),
					TurnBoundary = trigger == CardExtraEffectTrigger.TurnBoundary ? GetSelectedTurnBoundaryEdge(row) : CardExtraEffectTurnBoundary.End,
					TurnBoundarySide = trigger == CardExtraEffectTrigger.TurnBoundary ? GetSelectedTurnBoundarySide(row) : CardExtraEffectTurnBoundarySide.YourTurn,
					TurnBoundaryCardLocation = trigger == CardExtraEffectTrigger.TurnBoundary ? GetSelectedTurnBoundaryLocation(row) : CardExtraEffectTurnBoundaryCardLocation.Any,
					Timing = timing,
					Turns = turns,
					Duration = GetSelectedDuration(row),
					AsPower = asPower,
					TriggerCardPool = (asPower || isCardTypeCostAura || isDrawnGeneratedCost || isUnifiedUpgrade) ? GetSelectedTriggerCardPool(row) : CardGeneratedCardPool.All,
					TriggerCardType = (asPower || isCardTypeCostAura || isDrawnGeneratedCost || isUnifiedUpgrade) ? GetSelectedTriggerCardType(row) : CardGeneratedCardType.Any,
					TriggerCardFilter = (asPower || isUnifiedUpgrade) ? GetSelectedTriggerCardFilter(row) : CardExtraEffectCountCardFilter.Any,
					TriggerEveryN = asPower ? ParseExtraEffectNumericField(row.TriggerEveryNField, absoluteDefault: 1, isDeltaRow, minAbsolute: 1, maxAbsolute: 999) : 0,
					TriggerMaxFires = asPower ? triggerMaxFiresValue : 0,
					TriggerMaxTurns = asPower ? triggerMaxTurnsValue : 0,
					CreatedCardsCostDuration = GetSelectedCreatedCardsCostDuration(row),
					CreatedCardsCostTurns = ParseExtraEffectNumericField(row.CreatedCostTurnsField, absoluteDefault: 1, isDeltaRow, minAbsolute: 1, maxAbsolute: 99),
					CardCostsLessDuration = GetSelectedCardCostsLessDuration(row),
					CardCostsLessTurns = ParseExtraEffectNumericField(row.CardCostsLessTurnsField, absoluteDefault: 1, isDeltaRow, minAbsolute: 1, maxAbsolute: 99),
					CardCostsLessMode = cardCostsLessMode,
					GeneratedCardPool = GetSelectedGeneratedCardPool(row),
					GeneratedCardType = GetSelectedGeneratedCardType(row),
					ScaleMode = scaleMode,
					CountEvent = GetSelectedCountEvent(row),
					CountWindow = GetSelectedCountWindow(row),
					CountWindowInclusion = GetSelectedCountWindowInclusion(row),
					BlockLostCountingMode = GetSelectedBlockLostCountingMode(row),
					CountTurns = isDeltaRow
						? ParseExtraEffectNumericField(row.CountTurnsField, absoluteDefault: 1, isDeltaRow: true, minAbsolute: 1, maxAbsolute: 99)
						: GetSelectedCountTurns(row),
					CountComparison = countComparison,
					CountConditionAmount = countConditionAmount,
					CountCardPile = row.CountPileSelect != null && row.CountPileSelect.Visible
						? GetSelectedCardPile(row.CountPileSelect, CardExtraEffectCardPile.Hand)
						: CardExtraEffectCardPile.Hand,
					CountCardPool = GetSelectedCountPool(row),
					CountCardType = GetSelectedCountType(row),
					CountCardFilter = GetSelectedCountFilter(row),
					CountOrbType = GetSelectedCountOrbType(row),
					CountOrbSelection = GetSelectedCountOrbSelection(row),
					CountEnemyStatus = GetSelectedCountEnemyStatus(row),
					CountEnemyIntent = GetSelectedCountEnemyIntent(row),
					CountOnlyBlockCards = GetSelectedCountFilter(row) == CardExtraEffectCountCardFilter.GainBlock,
					HistoryScalingIncludesBase = row.ScalingBaseTickbox != null && row.ScalingBaseTickbox.Visible && row.ScalingBaseTickbox.IsTicked,
					RepeatIsX = repeatIsX,
					RepeatCount = repeatCount,
					GrantToCard = grantToCard,
					CardSelectionMode = selectionMode,
					CardSelectionPile = selectionPile,
					CardGrantDuration = grantDuration,
					CardGrantTurns = grantTurns,
					EnchantmentId = enchantmentId,
					EnchantmentDuration = enchantmentDuration,
					EnchantmentTurns = enchantmentTurns,
					CardSelectionCountIsX = selectionCountIsX,
					CardSelectionCount = resolvedKind == CardExtraEffectKind.DrawCardsThatCostLess ? drawCostLessDelta : selectionCount,
					CardSelectionPool = selectionPool,
					CardSelectionType = selectionType,
					CardSelectionFilter = selectionFilter,
					MoveToPile = moveToPile,
					MoveToPosition = moveToPosition,
					OrbAction = orbAction,
					OrbScope = orbScope,
					OrbType = orbType,
					OrbSelection = orbSelection,
					OrbFollowUp = orbFollowUp,
					MultiplierStat = multiplierStat,
					DrawnFromPile = drawnFromPile,
					SpecificCardId = (resolvedKind is CardExtraEffectKind.AddSpecificCardToHand or CardExtraEffectKind.FetchSpecificCardToHand)
						? (string.IsNullOrWhiteSpace(row.SpecificCardIdField?.Text) ? null : row.SpecificCardIdField.Text.Trim())
						: null,
					OstyAction = resolvedKind == CardExtraEffectKind.OstyAction ? GetSelectedOstyAction(row) : default,
					GrantedKeyword = resolvedKind == CardExtraEffectKind.GrantKeywordToPile ? GetSelectedGrantedKeyword(row) : default,
					CostFilterEnabled = row.CostFilterTickbox != null && GodotObject.IsInstanceValid(row.CostFilterTickbox) && row.CostFilterTickbox.IsTicked,
					CostFilterMax = row.CostFilterField != null && GodotObject.IsInstanceValid(row.CostFilterField) ? ParseIntOrDefault(row.CostFilterField.Text, 0) : 0
				};

				if (isDeltaRow && !CardEditorExtraEffects.HasMeaningfulUpgradeBaseSlotDelta(upgradeEffect, secondaryNumericFieldsAreDeltas: true))
				{
					effects.Add(null!);
					continue;
				}

				effects.Add(upgradeEffect);
			}
			if (effects.Any(e => e != null))
			{
				upgrade.ExtraEffectNumericFieldsAreDeltas = true;
				upgrade.ExtraEffects = effects;
			}
		}

		return upgrade;
	}

	private static void ApplyUpgradeOverridePreview(CardModel card, CardUpgradeOverride upgrade, UpgradeBaseline baseline)
	{
		if (card == null || !card.IsMutable || upgrade == null)
		{
			return;
		}

		if (upgrade.EnergyCostDelta.HasValue && !card.EnergyCost.CostsX && !baseline.BaseEnergyCostsX)
		{
			int vanillaDelta = baseline.VanillaEnergyDelta;
			int desiredDelta = upgrade.EnergyCostDelta.Value;
			int adjust = desiredDelta - vanillaDelta;
			if (adjust != 0)
			{
				try
				{
					int vanillaUpgraded = card.EnergyCost.GetWithModifiers(CostModifiers.None);
					card.EnergyCost.SetCustomBaseCost(Math.Max(-1, vanillaUpgraded + adjust));
					card.InvokeEnergyCostChanged();
				}
				catch
				{
				}
			}
		}

		if (upgrade.StarCostDelta.HasValue
			&& !baseline.BaseHasStarCostX
			&& !card.HasStarCostX)
		{
			int vanillaDelta = baseline.VanillaStarDelta;
			int desiredDelta = upgrade.StarCostDelta.Value;
			int adjust = desiredDelta - vanillaDelta;
			if (adjust != 0)
			{
				CardEditorOverrides.SetBaseStarCostUnsafe(card, Math.Max(-1, baseline.VanillaUpgradedStarCost + adjust));
			}
		}

		if (upgrade.ReplayCountDelta.HasValue)
		{
			int vanillaDelta = baseline.VanillaReplayDelta;
			int desiredDelta = upgrade.ReplayCountDelta.Value;
			int adjust = desiredDelta - vanillaDelta;
			if (adjust != 0)
			{
				card.BaseReplayCount = Math.Max(0, baseline.VanillaUpgradedReplayCount + adjust);
			}
		}

		if (upgrade.DynamicVarDeltas != null && upgrade.DynamicVarDeltas.Count > 0)
		{
			foreach ((string key, decimal desiredDelta) in upgrade.DynamicVarDeltas)
			{
				if (!card.DynamicVars.TryGetValue(key, out var dynamicVar))
				{
					continue;
				}
				decimal vanillaDelta = baseline.VanillaVarDeltas.TryGetValue(key, out decimal delta) ? delta : 0m;
				decimal adjust = desiredDelta - vanillaDelta;
				if (adjust != 0m)
				{
					try
					{
						dynamicVar.UpgradeValueBy(adjust);
					}
					catch
					{
					}
				}
			}
		}

		if (upgrade.KeywordsToRemove != null)
		{
			foreach (CardKeyword keyword in upgrade.KeywordsToRemove)
			{
				if (keyword != CardKeyword.None && card.Keywords.Contains(keyword))
				{
					card.RemoveKeyword(keyword);
				}
			}
		}

		if (upgrade.KeywordsToAdd != null)
		{
			foreach (CardKeyword keyword in upgrade.KeywordsToAdd)
			{
				if (keyword != CardKeyword.None && !card.Keywords.Contains(keyword))
				{
					card.AddKeyword(keyword);
				}
			}
		}

		try
		{
			card.DynamicVars.RecalculateForUpgradeOrEnchant();
		}
		catch
		{
		}
	}

	private CardUpgradeOverride BuildUpgradeOverrideFromUpgradedAbsolute(CardOverride desiredUpgradedAbsolute)
	{
		CardModel canonical = ModelDb.GetById<CardModel>(_cardId);

		CardModel baseCard = canonical.ToMutable();
		int baseEnergyCost = baseCard.EnergyCost.GetWithModifiers(CostModifiers.None);
		int baseStarCost = baseCard.BaseStarCost;
		int baseReplayCount = baseCard.BaseReplayCount;
		Dictionary<string, decimal> baseVars = baseCard.DynamicVars.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.BaseValue, StringComparer.Ordinal);

		CardModel vanillaUpgraded = canonical.ToMutable();
		try
		{
			CardEditorOverrides.SuppressUpgradeOverrides = true;
			TryUpgradeForPreview(vanillaUpgraded);
		}
		finally
		{
			CardEditorOverrides.SuppressUpgradeOverrides = false;
		}
		int vanillaEnergyCost = vanillaUpgraded.EnergyCost.GetWithModifiers(CostModifiers.None);
		int vanillaStarCost = vanillaUpgraded.BaseStarCost;
		int vanillaReplayCount = vanillaUpgraded.BaseReplayCount;
		Dictionary<string, decimal> vanillaVars = vanillaUpgraded.DynamicVars.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.BaseValue, StringComparer.Ordinal);
		HashSet<CardKeyword> vanillaKeywords = new HashSet<CardKeyword>(vanillaUpgraded.Keywords);

		CardUpgradeOverride upgrade = new CardUpgradeOverride();

		if (!baseCard.EnergyCost.CostsX)
		{
			int desiredEnergy = desiredUpgradedAbsolute.EnergyCost ?? vanillaEnergyCost;
			int desiredDelta = desiredEnergy - baseEnergyCost;
			int vanillaDelta = vanillaEnergyCost - baseEnergyCost;
			if (desiredDelta != vanillaDelta)
			{
				upgrade.EnergyCostDelta = desiredDelta;
			}
		}

		if (!baseCard.HasStarCostX)
		{
			int desiredStar = desiredUpgradedAbsolute.StarCost ?? vanillaStarCost;
			if (desiredStar >= -1)
			{
				int desiredDelta = desiredStar - baseStarCost;
				int vanillaDelta = vanillaStarCost - baseStarCost;
				if (desiredDelta != vanillaDelta)
				{
					upgrade.StarCostDelta = desiredDelta;
				}
			}
		}

		{
			int desiredReplay = desiredUpgradedAbsolute.ReplayCount ?? vanillaReplayCount;
			int desiredDelta = desiredReplay - baseReplayCount;
			int vanillaDelta = vanillaReplayCount - baseReplayCount;
			if (desiredDelta != vanillaDelta)
			{
				upgrade.ReplayCountDelta = desiredDelta;
			}
		}

		if (desiredUpgradedAbsolute.DynamicVarBaseValues != null && desiredUpgradedAbsolute.DynamicVarBaseValues.Count > 0)
		{
			Dictionary<string, decimal> deltas = new Dictionary<string, decimal>(StringComparer.Ordinal);
			foreach ((string key, decimal baseValue) in baseVars)
			{
				if (!desiredUpgradedAbsolute.DynamicVarBaseValues.TryGetValue(key, out decimal desiredValue))
				{
					continue;
				}
				if (!vanillaVars.TryGetValue(key, out decimal vanillaValue))
				{
					continue;
				}
				decimal desiredDelta = desiredValue - baseValue;
				decimal vanillaDelta = vanillaValue - baseValue;
				if (desiredDelta != vanillaDelta)
				{
					deltas[key] = desiredDelta;
				}
			}
			if (deltas.Count > 0)
			{
				upgrade.DynamicVarDeltas = deltas;
			}
		}

		if (desiredUpgradedAbsolute.Keywords != null)
		{
			HashSet<CardKeyword> desiredKeywords = desiredUpgradedAbsolute.Keywords;
			HashSet<CardKeyword> toRemove = new HashSet<CardKeyword>(vanillaKeywords);
			toRemove.ExceptWith(desiredKeywords);
			HashSet<CardKeyword> toAdd = new HashSet<CardKeyword>(desiredKeywords);
			toAdd.ExceptWith(vanillaKeywords);

			if (toRemove.Count > 0)
			{
				upgrade.KeywordsToRemove = toRemove;
			}
			if (toAdd.Count > 0)
			{
				upgrade.KeywordsToAdd = toAdd;
			}
		}

		if (desiredUpgradedAbsolute.ExtraEffects != null && desiredUpgradedAbsolute.ExtraEffects.Count > 0)
		{
			upgrade.ExtraEffects = desiredUpgradedAbsolute.ExtraEffects;
		}

		return upgrade;
	}

	private void OnApplyPressed()
	{
		if (_isCreatedCard && !_isUpgradeEditor)
		{
			CommitCreatedCardMetaFromUi();
		}

		if (_isUpgradeEditor)
		{
			if (_isCreatedCard)
			{
				string? customTextUpgraded = (_createdCustomTextUpgradedTickbox?.IsTicked ?? false) ? _createdCustomTextUpgradedField?.Text : null;
				CardEditorCreatedCardsStore.SetCustomTextUpgraded(_cardId, customTextUpgraded);
			}

			UpgradeBaseline baseline = GetUpgradeBaseline();
			CardUpgradeOverride upgradeOverride = BuildUpgradeOverrideFromUiDeltas(baseline);

			CardOverride baseOverride = CardEditorOverrides.Get(_cardId) ?? new CardOverride();
			baseOverride.Upgrade = upgradeOverride.IsEmpty() ? null : upgradeOverride;
			if (_isCreatedCard)
			{
				CardEditorCreatedCardsStore.SetOverride(_cardId, baseOverride);
			}
			else
			{
				CardEditorOverrides.Set(_cardId, baseOverride);
			}
		}
		else
		{
			CardOverride overrideData = BuildOverrideFromUi();
			if (CardEditorOverrides.TryGet(_cardId, out CardOverride existing) && existing.Upgrade != null && !existing.Upgrade.IsEmpty())
			{
				overrideData.Upgrade = existing.Upgrade;
			}
			if (_isCreatedCard)
			{
				CardEditorCreatedCardsStore.SetOverride(_cardId, overrideData);
			}
			else
			{
				CardEditorOverrides.Set(_cardId, overrideData);
			}
		}

		if (_isCreatedCard)
		{
			CardEditorCreatedCardsStore.ClearDraftMeta(_cardId);
		}
		CardEditorOverrides.ApplyToExistingCards(_cardId);
		_onApplied?.Invoke();
		Close();
	}

	private void OnResetPressed()
	{
		if (_isUpgradeEditor)
		{
			CardOverride? existing = CardEditorOverrides.Get(_cardId);
			if (existing != null)
			{
				existing.Upgrade = null;
				if (_isCreatedCard)
				{
					CardEditorCreatedCardsStore.SetOverride(_cardId, existing.IsEmpty() ? new CardOverride() : existing);
				}
				else if (existing.IsEmpty())
				{
					CardEditorOverrides.Clear(_cardId);
				}
				else
				{
					CardEditorOverrides.Set(_cardId, existing);
				}
			}
			CardEditorOverrides.ResetExistingCardsForIds(new[] { _cardId });
			CardEditorOverrides.ApplyToExistingCards(_cardId);
		}
		else
		{
			if (_isCreatedCard)
			{
				CardEditorCreatedCardsStore.SetOverride(_cardId, new CardOverride());
			}
			else
			{
				CardEditorOverrides.Clear(_cardId);
			}
			CardEditorOverrides.ResetExistingCards(_cardId);
		}

		if (_isCreatedCard)
		{
			CardEditorCreatedCardsStore.ClearDraftMeta(_cardId);
		}
		_onApplied?.Invoke();
		Close();
	}

	private static void TryUpgradeForPreview(CardModel card)
	{
		if (card == null || !card.IsMutable)
		{
			return;
		}
		try
		{
			if (card.IsUpgradable)
			{
				card.UpgradeInternal();
			}
			card.FinalizeUpgradeInternal();
		}
		catch
		{
		}
	}

	private void OpenUpgradeEditor()
	{
		CardModel canonical = ModelDb.GetById<CardModel>(_cardId);
		CardModel upgraded = canonical.ToMutable();
		TryUpgradeForPreview(upgraded);
		NCardEditorPopup popup = Create(upgraded, _onApplied ?? (() => { }), useModalContainer: _useModalContainer, isUpgradeEditor: true);
		Callable.From(() => NGame.Instance?.AddChildSafely(popup)).CallDeferred();
		Close();
	}

	private void OpenBaseEditor()
	{
		CardModel canonical = ModelDb.GetById<CardModel>(_cardId);
		CardModel basePreview = canonical.ToMutable();
		NCardEditorPopup popup = Create(basePreview, _onApplied ?? (() => { }), useModalContainer: _useModalContainer, isUpgradeEditor: false);
		Callable.From(() => NGame.Instance?.AddChildSafely(popup)).CallDeferred();
		Close();
	}

	private void Close()
	{
		CardEditorUiState.ClearDraftOverride(_cardId);
		if (_isCreatedCard)
		{
			CardEditorCreatedCardsStore.ClearDraftMeta(_cardId);
		}
		if (_cardPreviewNode != null && GodotObject.IsInstanceValid(_cardPreviewNode))
		{
			_cardPreviewNode.QueueFreeSafely();
			_cardPreviewNode = null;
		}
		if (_useModalContainer)
		{
			NModalContainer.Instance?.Clear();
		}
		else
		{
			this.QueueFreeSafely();
		}
	}

	private void OpenSpecificCardPicker(Action<ModelId> onPicked)
	{
		if (onPicked == null)
		{
			return;
		}

		if (_specificCardPickerOverlay != null && GodotObject.IsInstanceValid(_specificCardPickerOverlay))
		{
			return;
		}

		Control overlay = new Control
		{
			Name = "SpecificCardPickerOverlay",
			MouseFilter = MouseFilterEnum.Stop,
			ZIndex = 250,
			Visible = true
		};
		overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		ColorRect backstop = new ColorRect
		{
			Name = "Backstop",
			Color = new Color(0, 0, 0, 0.85f),
			MouseFilter = MouseFilterEnum.Stop
		};
		backstop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		overlay.AddChild(backstop);

		PanelContainer panel = new PanelContainer
		{
			Name = "Panel",
			MouseFilter = MouseFilterEnum.Stop
		};
		panel.AnchorLeft = 0.02f;
		panel.AnchorTop = 0.02f;
		panel.AnchorRight = 0.98f;
		panel.AnchorBottom = 0.98f;
		panel.OffsetLeft = 0f;
		panel.OffsetTop = 0f;
		panel.OffsetRight = 0f;
		panel.OffsetBottom = 0f;

		StyleBoxFlat panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.05f, 0.05f, 0.97f),
			BorderWidthLeft = 2,
			BorderWidthRight = 2,
			BorderWidthTop = 2,
			BorderWidthBottom = 2,
			BorderColor = new Color(0.9f, 0.75f, 0.2f, 1f),
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
			ContentMarginLeft = 16,
			ContentMarginRight = 16,
			ContentMarginTop = 16,
			ContentMarginBottom = 16
		};
		panel.AddThemeStyleboxOverride("panel", panelStyle);

		VBoxContainer root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		root.AddThemeConstantOverride("separation", 10);
		panel.AddChild(root);

		HBoxContainer headerRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		headerRow.AddThemeConstantOverride("separation", 10);
		root.AddChild(headerRow);

		Label title = new Label { Text = CardEditorLoc.T("ui.cardPicker.title", "Select Card") };
		StyleSectionLabel(title);
		title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		headerRow.AddChild(title);

		Button closeButton = new Button
		{
			Text = CardEditorLoc.T("ui.close", "Close"),
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y)
		};
		StyleInput(closeButton);
		headerRow.AddChild(closeButton);

		HBoxContainer searchRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		searchRow.AddThemeConstantOverride("separation", 10);
		root.AddChild(searchRow);

		Label searchLabel = new Label { Text = CardEditorLoc.T("ui.search", "Search") + ":" };
		StyleBodyLabel(searchLabel);
		searchLabel.CustomMinimumSize = new Vector2(120, 0);
		searchRow.AddChild(searchLabel);

		NMegaLineEdit searchField = new NMegaLineEdit { CustomMinimumSize = _fieldMinSize, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		searchField.PlaceholderText = CardEditorLoc.T("ui.search.placeholder", "Type a card name...");
		StyleInput(searchField);
		searchRow.AddChild(searchField);

		Label sortLabel = new Label { Text = CardEditorLoc.T("ui.sort", "Sort") + ":" };
		StyleBodyLabel(sortLabel);
		sortLabel.CustomMinimumSize = new Vector2(80, 0);
		searchRow.AddChild(sortLabel);

		OptionButton sortSelect = new OptionButton { CustomMinimumSize = new Vector2(240, _fieldMinSize.Y) };
		StyleInput(sortSelect);
		var sortOptions = new (SortingOrders Order, string Key, string Fallback)[]
		{
			(SortingOrders.AlphabetAscending, "ui.sort.alphaAsc", "A ? Z"),
			(SortingOrders.AlphabetDescending, "ui.sort.alphaDesc", "Z ? A"),
			(SortingOrders.CostAscending, "ui.sort.costAsc", "Cost ?"),
			(SortingOrders.CostDescending, "ui.sort.costDesc", "Cost ?"),
			(SortingOrders.RarityAscending, "ui.sort.rarityAsc", "Rarity ?"),
			(SortingOrders.RarityDescending, "ui.sort.rarityDesc", "Rarity ?"),
			(SortingOrders.TypeAscending, "ui.sort.typeAsc", "Type ?"),
			(SortingOrders.TypeDescending, "ui.sort.typeDesc", "Type ?")
		};
		for (int i = 0; i < sortOptions.Length; i++)
		{
			(_, string key, string fallback) = sortOptions[i];
			sortSelect.AddItem(CardEditorLoc.T(key, fallback));
		}
		sortSelect.Selected = 0;
		searchRow.AddChild(sortSelect);

		PackedScene gridScene = ResourceLoader.Load<PackedScene>(_cardGridScenePath);
		NCardGrid grid = gridScene.Instantiate<NCardGrid>(PackedScene.GenEditState.Disabled);
		grid.Name = "CardGrid";
		grid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		grid.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

		Control gridClip = new Control
		{
			Name = "CardGridClip",
			ClipContents = true,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		root.AddChild(gridClip);
		grid.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		gridClip.AddChild(grid);

		List<CardModel> allCards;
		try
		{
			allCards = ModelDb.AllCards
				.Where(c => c != null && c.Id != null)
				.Select(CardEditorOverrides.BuildPreview)
				.Where(c => !string.IsNullOrWhiteSpace(c.Title))
				.ToList();
		}
		catch
		{
			allCards = new List<CardModel>();
		}

		void ApplyFilter()
		{
			string query = (searchField.Text ?? string.Empty).Trim();
			List<CardModel> filtered = string.IsNullOrWhiteSpace(query)
				? allCards
				: allCards.Where(c =>
					(c.Title?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
					|| c.Id.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)
					|| (c.Id.Entry?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
				.ToList();

			SortingOrders order = sortOptions[Math.Clamp(sortSelect.Selected, 0, sortOptions.Length - 1)].Order;
			// NCardGrid's built-in Type sorting can behave oddly for our preview-built cards (empty grid for Type ?/? on some versions).
			// Pre-sort ourselves and ask the grid to preserve the incoming order.
			if (order is SortingOrders.TypeAscending or SortingOrders.TypeDescending)
			{
				bool desc = order == SortingOrders.TypeDescending;
				filtered.Sort((a, b) =>
				{
					int cmp = a.Type.CompareTo(b.Type);
					if (cmp != 0)
					{
						return desc ? -cmp : cmp;
					}

					cmp = string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase);
					if (cmp != 0)
					{
						return desc ? -cmp : cmp;
					}

					return desc ? b.Id.CompareTo(a.Id) : a.Id.CompareTo(b.Id);
				});

				grid.SetCards(filtered, PileType.None, new List<SortingOrders> { SortingOrders.Ascending });
				return;
			}

			grid.SetCards(filtered, PileType.None, new List<SortingOrders> { order });
		}

		searchField.TextChanged += _ => ApplyFilter();
		sortSelect.ItemSelected += _ => ApplyFilter();

		void CloseOverlay()
		{
			if (_specificCardPickerOverlay != null && GodotObject.IsInstanceValid(_specificCardPickerOverlay))
			{
				_specificCardPickerOverlay.QueueFreeSafely();
			}
			_specificCardPickerOverlay = null;
		}

		closeButton.Pressed += CloseOverlay;
		backstop.GuiInput += input =>
		{
			if (input is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
			{
				CloseOverlay();
				overlay.AcceptEvent();
			}
		};

		grid.Connect(NCardGrid.SignalName.HolderPressed, Callable.From<NCardHolder>(holder =>
		{
			CardModel card = holder?.CardModel;
			if (card == null)
			{
				return;
			}
			onPicked(card.Id);
			CloseOverlay();
		}));
		grid.Connect(NCardGrid.SignalName.HolderAltPressed, Callable.From<NCardHolder>(holder =>
		{
			CardModel card = holder?.CardModel;
			if (card == null)
			{
				return;
			}
			onPicked(card.Id);
			CloseOverlay();
		}));

		overlay.AddChild(panel);
		AddChild(overlay);
		_specificCardPickerOverlay = overlay;

		// Ensure the grid is inside the tree before we populate it.
		Callable.From(ApplyFilter).CallDeferred();
		Callable.From(() => searchField.GrabFocus()).CallDeferred();
	}

	private void CommitCreatedCardMetaFromUi()
	{
		if (!_isCreatedCard || _isUpgradeEditor)
		{
			return;
		}

		bool enabled = _createdEnabledTickbox?.IsTicked ?? false;
		string? title = _createdTitleField?.Text;

		CardEditorCreatedCardPool pool = CardEditorCreatedCardPool.Ironclad;
		if (_createdPoolSelect != null && _createdPoolSelect.Selected >= 0 && _createdPoolSelect.Selected < _createdPoolOptions.Count)
		{
			pool = _createdPoolOptions[_createdPoolSelect.Selected];
		}

		CardRarity rarity = CardRarity.Common;
		if (_createdRaritySelect != null && _createdRaritySelect.Selected >= 0 && _createdRaritySelect.Selected < _createdRarityOptions.Count)
		{
			rarity = _createdRarityOptions[_createdRaritySelect.Selected];
		}

		CardType type = _previewCard.Type;
		if (_cardTypeSelect.Selected >= 0 && _cardTypeSelect.Selected < _cardTypes.Count)
		{
			type = _cardTypes[_cardTypeSelect.Selected];
		}

		TargetType targetType = TargetType.AnyEnemy;
		if (_createdTargetSelect != null && _createdTargetSelect.Selected >= 0 && _createdTargetSelect.Selected < _createdTargetOptions.Count)
		{
			targetType = _createdTargetOptions[_createdTargetSelect.Selected];
		}

		List<ModelId> effectSourceIds = new List<ModelId>(_createdEffectSourceIds);

		ModelId? portraitSourceId = null;
		string? customPortraitFile = null;
		if (_createdPortraitSourceSelect != null && _createdPortraitSourceSelect.Selected >= 0 && _createdPortraitSourceSelect.Selected < _createdPortraitSourceOptions.Count)
		{
			(ModelId? cardId, string? customFile) = _createdPortraitSourceOptions[_createdPortraitSourceSelect.Selected];
			if (!string.IsNullOrWhiteSpace(customFile))
			{
				customPortraitFile = customFile;
				portraitSourceId = null;
			}
			else
			{
				portraitSourceId = cardId;
				customPortraitFile = null;
			}
		}

		bool fullArt = _createdFullArtTickbox?.IsTicked ?? false;
		CardEditorVisualFinish finish = GetSelectedCreatedFinish();
		string? customText = (_createdCustomTextTickbox?.IsTicked ?? false) ? _createdCustomTextField?.Text : null;

		Dictionary<string, float>? fp = _createdFinishParams.Count > 0 ? new Dictionary<string, float>(_createdFinishParams) : null;

		CardEditorCreatedCardsStore.SetMeta(_cardId, title, pool, rarity, type, targetType, effectSourceIds, portraitSourceId, customPortraitFile, fullArt, finish, customText, fp);
		CardEditorCreatedCardsStore.SetEnabled(_cardId, enabled);
	}

	private static string ToDisplayTitle(string? rawTitle)
	{
		if (string.IsNullOrWhiteSpace(rawTitle))
		{
			return string.Empty;
		}

		string title = rawTitle.Trim();
		return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(title.ToLowerInvariant());
	}
}
