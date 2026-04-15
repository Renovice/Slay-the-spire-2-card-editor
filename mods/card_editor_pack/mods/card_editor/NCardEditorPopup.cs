using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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
	private static readonly Vector2 _panelSize = new Vector2(1440, 820);
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

	private PanelContainer _panel = null!;
	private Vector2 _panelRuntimeSize = _panelSize;
	private Vector2 _panelCenterNudge = Vector2.Zero;
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
	private LineEdit? _handDiscardCountField;
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
	private VBoxContainer? _effectSummaryContainer;
	private VBoxContainer? _cardSmithContainer;
	private Control? _defaultFocus;
	private OptionButton? _cosmeticStylePresetSelect;
	private readonly List<CardEditorCosmeticStylePreset> _cosmeticStylePresetOptions = new();
	private OptionButton? _cosmeticAnimationPresetSelect;
	private readonly List<CardEditorCosmeticAnimationPreset> _cosmeticAnimationPresetOptions = new();
	private OptionButton? _cosmeticVfxPresetSelect;
	private readonly List<CardEditorCosmeticVfxPreset> _cosmeticVfxPresetOptions = new();
	private OptionButton? _cosmeticVfxAttachSelect;
	private readonly List<CardEditorCosmeticAttach> _cosmeticVfxAttachOptions = new();

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
	private OptionButton? _createdEffectSourceOrderSelect;
	private readonly List<CardEditorEffectSourcePlacement> _createdEffectSourceOrderOptions = new();
	private VBoxContainer? _createdEffectValueContainer;
	private string _createdEffectSourceNumbersKey = string.Empty;
	private Label? _effectSourceDynamicVarLabel;
	private VBoxContainer? _effectSourceDynamicVarContainer;
	private readonly Dictionary<string, Control> _effectSourceDynamicVarRowControls = new(StringComparer.Ordinal);
	private readonly Dictionary<string, Control> _effectSourceSpecialNumberRowControls = new(StringComparer.Ordinal);
	private readonly Dictionary<string, LineEdit> _effectSourceSpecialNumberFields = new(StringComparer.Ordinal);
	private readonly Dictionary<string, int> _effectSourceSpecialNumberDefaults = new(StringComparer.Ordinal);
	private string _effectSourceDynamicVarKey = string.Empty;
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
	private readonly List<ModelId?> _powerIds = new();
	private readonly Dictionary<CardKeyword, KeywordTickbox> _keywordChecks = new();
	private readonly Dictionary<CardTag, KeywordTickbox> _tagChecks = new();
	private readonly HashSet<string> _customTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	private HashSet<CardTag>? _baselineTags;
	private VBoxContainer? _customTagsList;
	private LineEdit? _customTagField;
	private readonly Dictionary<string, LineEdit> _dynamicFields = new();
	private readonly Dictionary<ModelId, LineEdit> _hardcodedPowerAmountFields = new();
	private readonly Dictionary<ModelId, int> _hardcodedPowerAmountDefaults = new();
	private readonly Dictionary<LineEdit, SpinButtons> _spinButtons = new();
	private readonly List<ExtraEffectRow> _extraEffectRows = new();
	private readonly List<CardSmithRow> _cardSmithRows = new();
	private HoldSpinState? _holdSpinState;
	private UpgradeBaseline? _upgradeBaseline;

	private const string EffectSourceDrawCostReductionKey = "EffectSource.DrawCostReduction";
	private const string EffectSourceHandDiscardCountKey = "EffectSource.HandDiscardCount";
	private const string EffectSourceResonanceEnemyStrengthLossKey = "EffectSource.ResonanceEnemyStrengthLoss";
	private const string EffectSourcePowerAmountPrefix = "EffectSource.PowerAmount:";

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
		_upgradeBaseline = null;
		Name = "CardEditorPopup";
		TopLevel = true;
		ZAsRelative = false;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		ZIndex = 100;
		Visible = true;
		MouseFilter = MouseFilterEnum.Stop;
		Log.Info("[CardEditor] Popup Initialize");
	}

	public override void _Ready()
	{
		ApplyRootViewportLayout();
		EnsureUiBuilt();
		RecenterPanelToScreen();
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

		Callable.From(() =>
		{
			ApplyRootViewportLayout();
			RecenterPanelToScreen();
		}).CallDeferred();
	}

	public override void _Notification(int what)
	{
		base._Notification(what);
		if (what == NotificationResized)
		{
			ApplyRootViewportLayout();
			RecenterPanelToScreen();
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
			_cardPreviewNode.UpdateVisuals(PileType.None, GetEditorPreviewMode());
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] ForcePreviewReload failed: {ex}");
		}
	}

	private CardPreviewMode GetEditorPreviewMode()
	{
		return _isUpgradeEditor ? CardPreviewMode.Upgrade : CardPreviewMode.Normal;
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
			_baselineTags = !_isUpgradeEditor ? ComputeBaselineTags() : null;

			_customTags.Clear();
			if (!_isUpgradeEditor
				&& CardEditorOverrides.TryGetEffectiveOverride(_cardId, out CardOverride initialOverrideData)
				&& initialOverrideData.CustomTags != null
				&& initialOverrideData.CustomTags.Count > 0)
			{
				_customTags.UnionWith(initialOverrideData.CustomTags);
			}

			Vector2 viewportSize = GetPopupViewportSize();
			Vector2 panelSize = GetDesiredPanelSize(viewportSize);
			_panelRuntimeSize = panelSize;
			_panel = new PanelContainer
			{
				CustomMinimumSize = panelSize,
				ZIndex = 20
			};
			_panel.TopLevel = true;
			_panel.ZAsRelative = false;
			_panelCenterNudge = Vector2.Zero;
			ApplyPanelLayout();
			_panel.ClipContents = true;
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
		margin.AddThemeConstantOverride("margin_left", 14);
		margin.AddThemeConstantOverride("margin_top", 12);
		margin.AddThemeConstantOverride("margin_right", 14);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		_panel.AddChild(margin);

		VBoxContainer root = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		root.AddThemeConstantOverride("separation", 8);
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
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			ClipContents = true
		};
		contentRow.AddThemeConstantOverride("separation", 14);
		root.AddChild(contentRow);

		VBoxContainer leftColumn = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(430, 0),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		leftColumn.AddThemeConstantOverride("separation", 12);
		contentRow.AddChild(leftColumn);

		PanelContainer previewPanel = CreateEditorSectionPanel(CardEditorLoc.T("section.cardPreview", "Card Preview"), out VBoxContainer previewBody, new Vector2(0, 430));
		leftColumn.AddChild(previewPanel);

		_cardNameLabel = new Label { Text = _previewCard.Title };
		StyleSectionLabel(_cardNameLabel);

		Control cardPreviewViewport = new Control
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			ClipContents = true
		};
		previewBody.AddChild(cardPreviewViewport);

		_cardPreviewNode = NCard.Create(_previewCard, ModelVisibility.Visible);
		if (_cardPreviewNode != null)
		{
			const float previewCardScale = 0.76f;
			Vector2 scaledCardSize = NCard.defaultSize * previewCardScale;
			_cardPreviewNode.Scale = Vector2.One * previewCardScale;
			_cardPreviewNode.AnchorLeft = 0.5f;
			_cardPreviewNode.AnchorTop = 0.5f;
			_cardPreviewNode.AnchorRight = 0.5f;
			_cardPreviewNode.AnchorBottom = 0.5f;
			_cardPreviewNode.OffsetLeft = -scaledCardSize.X * 0.5f;
			_cardPreviewNode.OffsetTop = -scaledCardSize.Y * 0.5f + 18f;
			_cardPreviewNode.OffsetRight = scaledCardSize.X * 0.5f;
			_cardPreviewNode.OffsetBottom = scaledCardSize.Y * 0.5f + 18f;
			_cardPreviewNode.MouseFilter = MouseFilterEnum.Ignore;
			cardPreviewViewport.AddChild(_cardPreviewNode);
			RefreshPreviewSoon();
		}

		PanelContainer effectListPanel = CreateEditorSectionPanel(CardEditorLoc.T("section.effectList", "Effect List"), out VBoxContainer effectListBody);
		effectListPanel.CustomMinimumSize = new Vector2(0, 230);
		effectListPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		leftColumn.AddChild(effectListPanel);

		ScrollContainer effectSummaryScroll = new ScrollContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		_effectSummaryContainer = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		_effectSummaryContainer.AddThemeConstantOverride("separation", 6);
		effectSummaryScroll.AddChild(_effectSummaryContainer);
		effectListBody.AddChild(effectSummaryScroll);

		HBoxContainer quickEffectButtons = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		quickEffectButtons.AddThemeConstantOverride("separation", 8);
		Button quickAddEffect = new Button
		{
			Text = CardEditorLoc.T("button.addEffect", "Add Effect"),
			CustomMinimumSize = new Vector2(0, 42),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(quickAddEffect);
		quickAddEffect.Pressed += () => AddExtraEffectRow(effect: null);
		Button quickAddEffectSource = new Button
		{
			Text = CardEditorLoc.T("button.addEffectSourceInline", "Add Effect Source"),
			CustomMinimumSize = new Vector2(0, 42),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			Visible = !_isUpgradeEditor
		};
		StyleInput(quickAddEffectSource);
		quickAddEffectSource.Pressed += () => OpenSpecificCardPicker(selectedId =>
		{
			if (selectedId == ModelId.none || selectedId == _cardId)
			{
				return;
			}

			AddExtraEffectRow(new CardExtraEffect
			{
				Kind = CardExtraEffectKind.RunEffectSourceCard,
				SpecificCardId = selectedId.ToString(),
				Trigger = CardExtraEffectTrigger.OnPlay,
				Target = CardExtraEffectTarget.Self
			});
			QueuePreviewUpdate();
		});
		quickEffectButtons.AddChild(quickAddEffect);
		quickEffectButtons.AddChild(quickAddEffectSource);
		effectListBody.AddChild(quickEffectButtons);

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

		Label configTitle = new Label { Text = CardEditorLoc.T("section.effectConfiguration", "Effect Configuration") };
		StylePanelTitleLabel(configTitle);
		rightColumn.AddChild(configTitle);

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

		if (!_isUpgradeEditor)
		{
			_baselineTags ??= ComputeBaselineTags();

			Label tagLabel = new Label { Text = CardEditorLoc.T("section.tags", "Tags") };
			StyleSectionLabel(tagLabel);
			rightColumn.AddChild(tagLabel);

			GridContainer tagGrid = new GridContainer { Columns = 3 };
			tagGrid.AddThemeConstantOverride("h_separation", 6);
			tagGrid.AddThemeConstantOverride("v_separation", 6);

			HashSet<CardTag> effectiveTags = new HashSet<CardTag>(_previewCard.Tags.Where(t => t != CardTag.None));
			foreach (CardTag tag in Enum.GetValues<CardTag>())
			{
				if (tag == CardTag.None)
				{
					continue;
				}

				Control tickboxVisuals = tickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
				Label label = new Label { Text = GetTagDisplayName(tag) };
				StyleBodyLabel(label);
				KeywordTickbox tickbox = new KeywordTickbox(tickboxVisuals, label, effectiveTags.Contains(tag));
				tickbox.Toggled += QueuePreviewUpdate;
				_tagChecks[tag] = tickbox;
				tagGrid.AddChild(tickbox);
			}
			rightColumn.AddChild(tagGrid);

			Label customTagsLabel = new Label { Text = CardEditorLoc.T("section.customTags", "Custom Tags") };
			StyleBodyLabel(customTagsLabel);
			rightColumn.AddChild(customTagsLabel);

			HBoxContainer customTagAddRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			customTagAddRow.AddThemeConstantOverride("separation", 10);

			NMegaLineEdit customTagField = new NMegaLineEdit
			{
				Text = string.Empty,
				CustomMinimumSize = new Vector2(320, _fieldMinSize.Y),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			customTagField.TooltipText = CardEditorLoc.T("tooltip.customTag", "Custom tags are data-driven and should sync in multiplayer. Use tags to match cards without relying on localized names.");
			StyleInput(customTagField);
			customTagField.TextSubmitted += _ => AddCustomTagFromField();
			_customTagField = customTagField;

			Button addCustomTagButton = new Button { Text = CardEditorLoc.T("button.add", "Add"), CustomMinimumSize = new Vector2(90, _fieldMinSize.Y) };
			StyleInput(addCustomTagButton);
			addCustomTagButton.Pressed += AddCustomTagFromField;

			customTagAddRow.AddChild(customTagField);
			customTagAddRow.AddChild(addCustomTagButton);
			rightColumn.AddChild(customTagAddRow);

			_customTagsList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			_customTagsList.AddThemeConstantOverride("separation", 6);
			rightColumn.AddChild(_customTagsList);
			RebuildCustomTagsList();
		}

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

		if (!_isUpgradeEditor && !_isCreatedCard && CardEditorTargetedDiscardSupport.IsSupportedCard(_cardId))
		{
			int discardCount = 1;
			if (CardEditorOverrides.TryGet(_cardId, out CardOverride existingOverride) && existingOverride.HandDiscardCount.HasValue)
			{
				discardCount = existingOverride.HandDiscardCount.Value;
			}
			discardCount = Math.Clamp(discardCount, 0, 99);

			HBoxContainer discardRow = CreateNumericRow(
				CardEditorLoc.T("field.discardCount", "Discard"),
				discardCount.ToString(CultureInfo.InvariantCulture),
				out LineEdit discardField,
				minValue: 0,
				maxValue: 99,
				onChanged: QueuePreviewUpdate);
			discardField.TooltipText = CardEditorLoc.T("tooltip.discardCountOverride", "Overrides how many cards this card makes you discard from hand.");
			rightColumn.AddChild(discardRow);
			_handDiscardCountField = discardField;
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

			Label effectSourceLabel = new Label { Text = "Effect Source Numbers" };
			StyleBodyLabel(effectSourceLabel);
			effectSourceLabel.Modulate = new Color(0.75f, 0.75f, 0.75f, 1f);
			effectSourceLabel.Visible = false;
			rightColumn.AddChild(effectSourceLabel);
			_effectSourceDynamicVarLabel = effectSourceLabel;

			_effectSourceDynamicVarContainer = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			_effectSourceDynamicVarContainer.AddThemeConstantOverride("separation", 10);
			rightColumn.AddChild(_effectSourceDynamicVarContainer);

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

		if (!_isUpgradeEditor)
		{
			CardOverride? existingForCosmetics = null;
			if (CardEditorOverrides.TryGetEffectiveOverride(_cardId, out CardOverride existingOverride))
			{
				existingForCosmetics = existingOverride;
			}
			BuildCosmeticsUi(rightColumn, existingForCosmetics);
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

	private void BuildCosmeticsUi(VBoxContainer rightColumn, CardOverride? existing)
	{
		if (rightColumn == null)
		{
			return;
		}

		Label cosmeticsLabel = new Label { Text = CardEditorLoc.T("section.cosmetics", "Cosmetics") };
		StyleSectionLabel(cosmeticsLabel);
		rightColumn.AddChild(cosmeticsLabel);

		CardEditorCosmeticStylePreset initialStylePreset = existing?.CosmeticStylePreset ?? CardEditorCosmeticStylePreset.None;
		CardEditorCosmeticAnimationPreset initialAnimationPreset =
			existing?.CosmeticAnimationPreset
			?? ((existing?.CosmeticPlayAttackerAnim ?? false)
				? CardEditorCosmeticAnimationPreset.MatchCardType
				: CardEditorCosmeticAnimationPreset.None);
		CardEditorCosmeticVfxPreset initialVfxPreset = existing?.CosmeticVfxPreset ?? CardEditorCosmeticVfxPreset.None;
		CardEditorCosmeticAttach initialAttach = existing?.CosmeticVfxAttach ?? CardEditorCosmeticAttach.Target;
		if (initialStylePreset != CardEditorCosmeticStylePreset.None)
		{
			if (initialAnimationPreset == CardEditorCosmeticAnimationPreset.None
				&& CardEditorCosmetics.TryGetStyleDefaults(initialStylePreset, out CardEditorCosmeticAnimationPreset styleAnimation, out CardEditorCosmeticVfxPreset styleVfx, out CardEditorCosmeticAttach styleAttach))
			{
				initialAnimationPreset = styleAnimation;
				if (initialVfxPreset == CardEditorCosmeticVfxPreset.None)
				{
					initialVfxPreset = styleVfx;
				}
				if (existing?.CosmeticVfxAttach == null)
				{
					initialAttach = styleAttach;
				}
			}
		}

		HBoxContainer styleRow = CreateDropdownRow(CardEditorLoc.T("field.cosmeticStylePreset", "Visual Archetype"), out OptionButton styleSelect);
		styleRow.TooltipText = CardEditorLoc.T(
			"tooltip.cosmeticStylePreset",
			"Cosmetic only. Applies a ready-made animation/VFX style bundle. You can still override the animation or VFX below.");
		rightColumn.AddChild(styleRow);
		_cosmeticStylePresetSelect = styleSelect;
		PopulateCosmeticStylePresetDropdown(styleSelect, initialStylePreset);

		HBoxContainer animationRow = CreateDropdownRow(CardEditorLoc.T("field.cosmeticAnimationPreset", "Animation Preset"), out OptionButton animationSelect);
		animationRow.TooltipText = CardEditorLoc.T(
			"tooltip.cosmeticAnimationPreset",
			"Cosmetic only. Plays an owner animation preset when the card is played. Owner Attack/Cast uses the current character's own animation, so Regent cards on Regent already use Regent's strike/cast style.");
		rightColumn.AddChild(animationRow);
		_cosmeticAnimationPresetSelect = animationSelect;
		PopulateCosmeticAnimationPresetDropdown(animationSelect, initialAnimationPreset);

		HBoxContainer presetRow = CreateDropdownRow(CardEditorLoc.T("field.cosmeticVfxPreset", "VFX Preset"), out OptionButton presetSelect);
		presetRow.TooltipText = CardEditorLoc.T(
			"tooltip.cosmeticVfxPreset",
			"Cosmetic only. Plays a visual effect when the card is played. Does not change card text.");
		rightColumn.AddChild(presetRow);
		_cosmeticVfxPresetSelect = presetSelect;
		PopulateCosmeticVfxPresetDropdown(presetSelect, initialVfxPreset);

		HBoxContainer attachRow = CreateDropdownRow(CardEditorLoc.T("field.cosmeticVfxAttach", "VFX Attach"), out OptionButton attachSelect);
		attachRow.TooltipText = CardEditorLoc.T(
			"tooltip.cosmeticVfxAttach",
			"Where the VFX spawns (Self/Target/All Enemies/Random Enemy).");
		rightColumn.AddChild(attachRow);
		_cosmeticVfxAttachSelect = attachSelect;
		PopulateCosmeticVfxAttachDropdown(attachSelect, initialAttach);

		void UpdateAttachEnabled()
		{
			CardEditorCosmeticVfxPreset preset = GetSelectedCosmeticVfxPreset();
			bool enabled = preset != CardEditorCosmeticVfxPreset.None;
			if (_cosmeticVfxAttachSelect != null && GodotObject.IsInstanceValid(_cosmeticVfxAttachSelect))
			{
				_cosmeticVfxAttachSelect.Disabled = !enabled;
			}
		}

		styleSelect.ItemSelected += _ =>
		{
			CardEditorCosmeticStylePreset stylePreset = GetSelectedCosmeticStylePreset();
			if (stylePreset != CardEditorCosmeticStylePreset.None
				&& CardEditorCosmetics.TryGetStyleDefaults(stylePreset, out CardEditorCosmeticAnimationPreset styleAnimation, out CardEditorCosmeticVfxPreset styleVfx, out CardEditorCosmeticAttach styleAttach))
			{
				SetCosmeticAnimationPreset(styleAnimation);
				SetCosmeticVfxPreset(styleVfx);
				SetCosmeticVfxAttach(styleAttach);
			}

			UpdateAttachEnabled();
			QueuePreviewUpdate();
		};

		animationSelect.ItemSelected += _ => QueuePreviewUpdate();
		presetSelect.ItemSelected += _ =>
		{
			UpdateAttachEnabled();
			QueuePreviewUpdate();
		};

		attachSelect.ItemSelected += _ => QueuePreviewUpdate();

		UpdateAttachEnabled();
	}

	private void PopulateCosmeticAnimationPresetDropdown(OptionButton select, CardEditorCosmeticAnimationPreset selectedPreset)
	{
		select.Clear();
		_cosmeticAnimationPresetOptions.Clear();

		foreach (CardEditorCosmeticAnimationPreset preset in Enum.GetValues<CardEditorCosmeticAnimationPreset>())
		{
			select.AddItem(CardEditorCosmetics.AnimationPresetLabel(preset));
			_cosmeticAnimationPresetOptions.Add(preset);
		}

		int selectedIndex = _cosmeticAnimationPresetOptions.IndexOf(selectedPreset);
		select.Selected = selectedIndex >= 0 ? selectedIndex : 0;
	}

	private void PopulateCosmeticStylePresetDropdown(OptionButton select, CardEditorCosmeticStylePreset selectedPreset)
	{
		select.Clear();
		_cosmeticStylePresetOptions.Clear();

		foreach (CardEditorCosmeticStylePreset preset in Enum.GetValues<CardEditorCosmeticStylePreset>())
		{
			select.AddItem(CardEditorCosmetics.StylePresetLabel(preset));
			_cosmeticStylePresetOptions.Add(preset);
		}

		int selectedIndex = _cosmeticStylePresetOptions.IndexOf(selectedPreset);
		select.Selected = selectedIndex >= 0 ? selectedIndex : 0;
	}

	private void PopulateCosmeticVfxPresetDropdown(OptionButton select, CardEditorCosmeticVfxPreset? selectedPreset)
	{
		select.Clear();
		_cosmeticVfxPresetOptions.Clear();

		foreach (CardEditorCosmeticVfxPreset preset in Enum.GetValues<CardEditorCosmeticVfxPreset>())
		{
			select.AddItem(CardEditorCosmetics.VfxPresetLabel(preset));
			_cosmeticVfxPresetOptions.Add(preset);
		}

		CardEditorCosmeticVfxPreset desired = selectedPreset ?? CardEditorCosmeticVfxPreset.None;
		int selectedIndex = _cosmeticVfxPresetOptions.IndexOf(desired);
		select.Selected = selectedIndex >= 0 ? selectedIndex : 0;
	}

	private void PopulateCosmeticVfxAttachDropdown(OptionButton select, CardEditorCosmeticAttach? selectedAttach)
	{
		select.Clear();
		_cosmeticVfxAttachOptions.Clear();

		foreach (CardEditorCosmeticAttach attach in Enum.GetValues<CardEditorCosmeticAttach>())
		{
			select.AddItem(CardEditorCosmetics.AttachLabel(attach));
			_cosmeticVfxAttachOptions.Add(attach);
		}

		CardEditorCosmeticAttach desired = selectedAttach ?? CardEditorCosmeticAttach.Target;
		int selectedIndex = _cosmeticVfxAttachOptions.IndexOf(desired);
		select.Selected = selectedIndex >= 0 ? selectedIndex : 0;
	}

	private void SetCosmeticAnimationPreset(CardEditorCosmeticAnimationPreset preset)
	{
		if (_cosmeticAnimationPresetSelect == null
			|| !GodotObject.IsInstanceValid(_cosmeticAnimationPresetSelect))
		{
			return;
		}

		int selectedIndex = _cosmeticAnimationPresetOptions.IndexOf(preset);
		_cosmeticAnimationPresetSelect.Selected = selectedIndex >= 0 ? selectedIndex : 0;
	}

	private void SetCosmeticVfxPreset(CardEditorCosmeticVfxPreset preset)
	{
		if (_cosmeticVfxPresetSelect == null
			|| !GodotObject.IsInstanceValid(_cosmeticVfxPresetSelect))
		{
			return;
		}

		int selectedIndex = _cosmeticVfxPresetOptions.IndexOf(preset);
		_cosmeticVfxPresetSelect.Selected = selectedIndex >= 0 ? selectedIndex : 0;
	}

	private void SetCosmeticVfxAttach(CardEditorCosmeticAttach attach)
	{
		if (_cosmeticVfxAttachSelect == null
			|| !GodotObject.IsInstanceValid(_cosmeticVfxAttachSelect))
		{
			return;
		}

		int selectedIndex = _cosmeticVfxAttachOptions.IndexOf(attach);
		_cosmeticVfxAttachSelect.Selected = selectedIndex >= 0 ? selectedIndex : 0;
	}

	private CardEditorCosmeticStylePreset GetSelectedCosmeticStylePreset()
	{
		if (_cosmeticStylePresetSelect != null
			&& GodotObject.IsInstanceValid(_cosmeticStylePresetSelect)
			&& _cosmeticStylePresetSelect.Selected >= 0
			&& _cosmeticStylePresetSelect.Selected < _cosmeticStylePresetOptions.Count)
		{
			return _cosmeticStylePresetOptions[_cosmeticStylePresetSelect.Selected];
		}

		return CardEditorCosmeticStylePreset.None;
	}

	private CardEditorCosmeticAnimationPreset GetSelectedCosmeticAnimationPreset()
	{
		if (_cosmeticAnimationPresetSelect != null
			&& GodotObject.IsInstanceValid(_cosmeticAnimationPresetSelect)
			&& _cosmeticAnimationPresetSelect.Selected >= 0
			&& _cosmeticAnimationPresetSelect.Selected < _cosmeticAnimationPresetOptions.Count)
		{
			return _cosmeticAnimationPresetOptions[_cosmeticAnimationPresetSelect.Selected];
		}

		return CardEditorCosmeticAnimationPreset.None;
	}

	private CardEditorCosmeticVfxPreset GetSelectedCosmeticVfxPreset()
	{
		if (_cosmeticVfxPresetSelect != null
			&& GodotObject.IsInstanceValid(_cosmeticVfxPresetSelect)
			&& _cosmeticVfxPresetSelect.Selected >= 0
			&& _cosmeticVfxPresetSelect.Selected < _cosmeticVfxPresetOptions.Count)
		{
			return _cosmeticVfxPresetOptions[_cosmeticVfxPresetSelect.Selected];
		}

		return CardEditorCosmeticVfxPreset.None;
	}

	private CardEditorCosmeticAttach GetSelectedCosmeticVfxAttach()
	{
		if (_cosmeticVfxAttachSelect != null
			&& GodotObject.IsInstanceValid(_cosmeticVfxAttachSelect)
			&& _cosmeticVfxAttachSelect.Selected >= 0
			&& _cosmeticVfxAttachSelect.Selected < _cosmeticVfxAttachOptions.Count)
		{
			return _cosmeticVfxAttachOptions[_cosmeticVfxAttachSelect.Selected];
		}

		return CardEditorCosmeticAttach.Target;
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
		public Button MoveUpButton { get; init; } = null!;
		public Button MoveDownButton { get; init; } = null!;
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
		public Control PowerCountEnemyStatusRow { get; init; } = null!;
		public OptionButton PowerCountEnemyStatusSelect { get; init; } = null!;
		public Control PowerFilterRow { get; init; } = null!;
		public OptionButton TriggerCardPoolSelect { get; init; } = null!;
		public OptionButton TriggerCardTypeSelect { get; init; } = null!;
		public OptionButton TriggerCardFilterSelect { get; init; } = null!;
		public Control DrawTargetFilterRow { get; init; } = null!;
		public OptionButton DrawTargetPoolSelect { get; init; } = null!;
		public OptionButton DrawTargetTypeSelect { get; init; } = null!;
		public OptionButton DrawTargetFilterSelect { get; init; } = null!;
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

		public Control PowerRow { get; init; } = null!;
		public OptionButton PowerSelect { get; init; } = null!;

		public Control MoveCardsRow { get; init; } = null!;
		public Control MoveCardsRowTop { get; init; } = null!;
		public Control MoveCardsRowBottom { get; init; } = null!;
		public OptionButton MoveFromPileSelect { get; init; } = null!;
		public OptionButton MoveSelectionModeSelect { get; init; } = null!;
		public OptionButton MoveToPileSelect { get; init; } = null!;
		public OptionButton MoveToPositionSelect { get; init; } = null!;
		public Control AdditionalMoveToRow { get; init; } = null!;
		public KeywordTickbox AdditionalMoveToHandTickbox { get; init; } = null!;
		public KeywordTickbox AdditionalMoveToDrawTickbox { get; init; } = null!;
		public KeywordTickbox AdditionalMoveToDiscardTickbox { get; init; } = null!;
		public KeywordTickbox AdditionalMoveToExhaustTickbox { get; init; } = null!;
		public Control CostFilterRow { get; init; } = null!;
		public KeywordTickbox CostFilterTickbox { get; init; } = null!;
		public LineEdit CostFilterField { get; init; } = null!;
		public Control CardMatchRow { get; init; } = null!;
		public OptionButton CardMatchModeSelect { get; init; } = null!;
		public LineEdit MatchCardIdField { get; init; } = null!;
		public Button MatchCardIdPickButton { get; init; } = null!;
		public OptionButton MatchTagKindSelect { get; init; } = null!;
		public OptionButton MatchVanillaTagSelect { get; init; } = null!;
		public OptionButton MatchCustomTagSelect { get; init; } = null!;
		public List<string> MatchCustomTagOptions { get; set; } = new();
		public Control DrawCostRow { get; init; } = null!;
		public KeywordTickbox DrawCostTickbox { get; init; } = null!;
		public LineEdit DrawCostField { get; init; } = null!;
		public Control KeywordGroupRow { get; init; } = null!;
		public LineEdit KeywordGroupField { get; init; } = null!;
		public Control IgnoreVariantRow { get; init; } = null!;
		public OptionButton IgnoreVariantSelect { get; init; } = null!;
		public Control CardActionVariantRow { get; init; } = null!;
		public OptionButton CardActionVariantSelect { get; init; } = null!;
		public Control CardGenerationVariantRow { get; init; } = null!;
		public OptionButton CardGenerationVariantSelect { get; init; } = null!;
		public Control TurnBoundaryRow { get; init; } = null!;
		public OptionButton TurnBoundaryEdgeSelect { get; init; } = null!;
		public OptionButton TurnBoundarySideSelect { get; init; } = null!;
		public OptionButton TurnBoundaryLocationSelect { get; init; } = null!;

		public Control SpecificCardRow { get; init; } = null!;
		public LineEdit SpecificCardIdField { get; init; } = null!;

		public Control TransformModeRow { get; init; } = null!;
		public OptionButton TransformModeSelect { get; init; } = null!;

		public Control ConditionalBonusRow { get; init; } = null!;
		public OptionButton ConditionalBonusConditionSelect { get; init; } = null!;
		public OptionButton ConditionalBonusEnemyStatusSelect { get; init; } = null!;
		public OptionButton ConditionalBonusEnemyIntentSelect { get; init; } = null!;
		public LineEdit ConditionalBonusAmountField { get; init; } = null!;

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
		public Control CountSourceToggleRow { get; init; } = null!;
		public KeywordTickbox CountExcludeSourceTickbox { get; init; } = null!;
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
		ExactCopyOfThisCardToDeck = 3,
		AddSpecificCard = 4,
		FetchSpecificCard = 5,
		CreatedCardsCostLess = 6,
		CreatedCardsUpgraded = 7
	}

	private enum UnifiedCardActionVariant
	{
		MoveBetweenPiles = 0,
		PlayFromPile = 1,
		Discard = 2,
		Exhaust = 3,
		Transform = 4,
		GrantKeyword = 5,
		GrantExtraEffect = 6,
		UpgradeInPile = 7,
		UpgradeDeck = 8,
		CopyPileToDeck = 9,
		ExactCopyPileToDeck = 10,
		RemoveFromDeck = 11
	}

	private Vector2 GetPopupViewportSize()
	{
		try
		{
			SceneTree? tree = GetTree();
			Viewport? rootViewport = tree?.Root;
			if (rootViewport != null)
			{
				Rect2 rootRect = rootViewport.GetVisibleRect();
				if (rootRect.Size.X > 0f && rootRect.Size.Y > 0f)
				{
					return rootRect.Size;
				}
			}
		}
		catch
		{
		}

		try
		{
			Viewport? viewport = GetViewport();
			if (viewport != null)
			{
				Rect2 visibleRect = viewport.GetVisibleRect();
				if (visibleRect.Size.X > 0f && visibleRect.Size.Y > 0f)
				{
					return visibleRect.Size;
				}
			}
		}
		catch
		{
		}

		return GetViewportRect().Size;
	}

	private Vector2 GetDesiredPanelSize(Vector2 viewportSize)
	{
		return new Vector2(
			Mathf.Min(_panelSize.X, Mathf.Max(900f, viewportSize.X - 160f)),
			Mathf.Min(_panelSize.Y, Mathf.Max(640f, viewportSize.Y - 140f)));
	}

	private void ApplyRootViewportLayout()
	{
		Vector2 viewportSize = GetPopupViewportSize();
		SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
		Position = Vector2.Zero;
		Size = viewportSize;
		CustomMinimumSize = viewportSize;
	}

	private void RecenterPanelToScreen()
	{
		if (_panel == null || !GodotObject.IsInstanceValid(_panel))
		{
			return;
		}

		_panelRuntimeSize = GetDesiredPanelSize(GetPopupViewportSize());
		_panel.CustomMinimumSize = _panelRuntimeSize;
		_panelCenterNudge = Vector2.Zero;
		ApplyPanelLayout();
	}

	private Vector2 GetScreenCenterLocalPosition()
	{
		Vector2 screenSize = GetPopupViewportSize();
		return (screenSize - _panelRuntimeSize) * 0.5f;
	}

	private void ApplyPanelLayout()
	{
		if (_panel == null || !GodotObject.IsInstanceValid(_panel))
		{
			return;
		}

		Vector2 localTopLeft = GetScreenCenterLocalPosition();
		float left = Mathf.Round(localTopLeft.X);
		float top = Mathf.Round(localTopLeft.Y);
		_panel.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
		_panel.Position = new Vector2(left, top);
		_panel.Size = _panelRuntimeSize;
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

	private PanelContainer CreateEditorPanel(float bgAlpha = 0.88f, int borderWidth = 1)
	{
		PanelContainer panel = new PanelContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleBoxFlat style = new StyleBoxFlat
		{
			BgColor = new Color(0.035f, 0.04f, 0.045f, bgAlpha),
			BorderColor = StsColors.gold,
			BorderWidthLeft = borderWidth,
			BorderWidthTop = borderWidth,
			BorderWidthRight = borderWidth,
			BorderWidthBottom = borderWidth,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8
		};
		panel.AddThemeStyleboxOverride("panel", style);
		return panel;
	}

	private PanelContainer CreateEditorSectionPanel(string titleText, out VBoxContainer body, Vector2? minimumSize = null)
	{
		PanelContainer panel = CreateEditorPanel();
		if (minimumSize.HasValue)
		{
			panel.CustomMinimumSize = minimumSize.Value;
		}

		MarginContainer margin = new MarginContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		margin.AddThemeConstantOverride("margin_left", 14);
		margin.AddThemeConstantOverride("margin_top", 12);
		margin.AddThemeConstantOverride("margin_right", 14);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		panel.AddChild(margin);

		body = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		body.AddThemeConstantOverride("separation", 10);
		margin.AddChild(body);

		Label title = new Label { Text = titleText };
		StylePanelTitleLabel(title);
		body.AddChild(title);
		return panel;
	}

	private void StylePanelTitleLabel(Label label)
	{
		_headerFont ??= TryLoadFont(_headerFontPath);
		if (_headerFont != null)
		{
			label.AddThemeFontOverride("font", _headerFont);
		}
		label.AddThemeFontSizeOverride("font_size", 24);
		label.AddThemeColorOverride("font_color", StsColors.cream);
		label.AddThemeColorOverride("font_outline_color", StsColors.transparentBlack);
		label.AddThemeConstantOverride("outline_size", 10);
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

	private static StyleBoxFlat CreateInputStyleBox(Color bgColor, Color borderColor, int borderWidth = 1)
	{
		return new StyleBoxFlat
		{
			BgColor = bgColor,
			BorderColor = borderColor,
			BorderWidthLeft = borderWidth,
			BorderWidthTop = borderWidth,
			BorderWidthRight = borderWidth,
			BorderWidthBottom = borderWidth,
			CornerRadiusTopLeft = 5,
			CornerRadiusTopRight = 5,
			CornerRadiusBottomLeft = 5,
			CornerRadiusBottomRight = 5,
			ContentMarginLeft = 8,
			ContentMarginRight = 8,
			ContentMarginTop = 4,
			ContentMarginBottom = 4
		};
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
		control.AddThemeColorOverride("font_hover_color", Colors.White);
		control.AddThemeColorOverride("font_pressed_color", StsColors.gold);
		control.AddThemeColorOverride("font_focus_color", Colors.White);
		control.AddThemeColorOverride("font_disabled_color", StsColors.gray);
		control.AddThemeConstantOverride("outline_size", 0);
		if (control is Button || control is LineEdit)
		{
			StyleBoxFlat normal = CreateInputStyleBox(new Color(0.055f, 0.065f, 0.075f, 0.95f), new Color(0.20f, 0.23f, 0.26f, 1f));
			StyleBoxFlat hover = CreateInputStyleBox(new Color(0.075f, 0.09f, 0.105f, 0.98f), new Color(0.34f, 0.38f, 0.42f, 1f));
			StyleBoxFlat focus = CreateInputStyleBox(new Color(0.065f, 0.08f, 0.095f, 1f), StsColors.gold, 2);
			StyleBoxFlat disabled = CreateInputStyleBox(new Color(0.035f, 0.035f, 0.04f, 0.72f), new Color(0.12f, 0.13f, 0.14f, 1f));
			control.AddThemeStyleboxOverride("normal", normal);
			control.AddThemeStyleboxOverride("hover", hover);
			control.AddThemeStyleboxOverride("pressed", focus);
			control.AddThemeStyleboxOverride("focus", focus);
			control.AddThemeStyleboxOverride("disabled", disabled);
			control.AddThemeStyleboxOverride("read_only", disabled);
		}
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

	private static string GetTagDisplayName(CardTag tag)
	{
		string raw = tag.ToString();
		if (string.IsNullOrWhiteSpace(raw))
		{
			return raw;
		}

		StringBuilder sb = new StringBuilder(raw.Length + 8);
		sb.Append(raw[0]);
		for (int i = 1; i < raw.Length; i++)
		{
			char c = raw[i];
			char prev = raw[i - 1];
			if (char.IsUpper(c) && !char.IsUpper(prev) && prev != ' ')
			{
				sb.Append(' ');
			}
			sb.Append(c);
		}
		return sb.ToString();
	}

	private void AddCustomTagFromField()
	{
		if (_customTagField == null || !GodotObject.IsInstanceValid(_customTagField))
		{
			return;
		}

		string tag = _customTagField.Text?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(tag))
		{
			return;
		}

		_customTags.Add(tag);
		_customTagField.Text = string.Empty;
		RebuildCustomTagsList();
		QueuePreviewUpdate();
	}

	private void RebuildCustomTagsList()
	{
		if (_customTagsList == null || !GodotObject.IsInstanceValid(_customTagsList))
		{
			return;
		}

		foreach (Node child in _customTagsList.GetChildren())
		{
			child.QueueFree();
		}

		foreach (string tag in _customTags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase))
		{
			string captured = tag;
			HBoxContainer row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			row.AddThemeConstantOverride("separation", 10);

			Label label = new Label { Text = captured, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			StyleBodyLabel(label);

			Button removeButton = new Button { Text = "X", CustomMinimumSize = new Vector2(40, _fieldMinSize.Y) };
			StyleInput(removeButton);
			removeButton.TooltipText = CardEditorLoc.T("tooltip.removeCustomTag", "Remove this custom tag from the card.");
			removeButton.Pressed += () =>
			{
				_customTags.Remove(captured);
				RebuildCustomTagsList();
				QueuePreviewUpdate();
			};

			row.AddChild(label);
			row.AddChild(removeButton);
			_customTagsList.AddChild(row);
		}

		RefreshCustomTagMatchSelectors();
	}

	private List<string> GetAllKnownCustomTags()
	{
		HashSet<string> tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach ((ModelId _, CardOverride ov) in CardEditorOverrides.AllOverrides)
		{
			if (ov?.CustomTags == null || ov.CustomTags.Count == 0)
			{
				continue;
			}
			foreach (string t in ov.CustomTags)
			{
				string trimmed = t?.Trim() ?? string.Empty;
				if (!string.IsNullOrWhiteSpace(trimmed))
				{
					tags.Add(trimmed);
				}
			}
		}

		foreach (string t in _customTags)
		{
			string trimmed = t?.Trim() ?? string.Empty;
			if (!string.IsNullOrWhiteSpace(trimmed))
			{
				tags.Add(trimmed);
			}
		}

		return tags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
	}

	private void RefreshCustomTagMatchSelectors()
	{
		List<string> tags = GetAllKnownCustomTags();
		foreach (ExtraEffectRow row in _extraEffectRows)
		{
			if (row?.MatchCustomTagSelect == null || !GodotObject.IsInstanceValid(row.MatchCustomTagSelect))
			{
				continue;
			}

			string? previousSelection = GetSelectedMatchCustomTag(row);
			row.MatchCustomTagOptions = tags;

			row.MatchCustomTagSelect.Clear();
			row.MatchCustomTagSelect.AddItem(CardEditorLoc.T("cardMatch.tag.any", "Any Tag"), 0);
			for (int i = 0; i < tags.Count; i++)
			{
				row.MatchCustomTagSelect.AddItem(tags[i], i + 1);
			}

			int desiredIndex = 0;
			if (!string.IsNullOrWhiteSpace(previousSelection))
			{
				int idx = tags.FindIndex(t => string.Equals(t, previousSelection, StringComparison.OrdinalIgnoreCase));
				if (idx >= 0)
				{
					desiredIndex = row.MatchCustomTagSelect.GetItemIndex(idx + 1);
				}
			}
			row.MatchCustomTagSelect.Select(Math.Max(0, desiredIndex));
		}
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
		// Effect Sources are managed as inline Extra Effects (RunEffectSourceCard) so they can be freely reordered.
		_createdEffectSourceIds.Clear();

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

			Button moveUpButton = new Button
			{
				Text = "\u25B2",
				Flat = true,
				FocusMode = FocusModeEnum.None,
				CustomMinimumSize = new Vector2(40, _fieldMinSize.Y),
				MouseFilter = MouseFilterEnum.Stop
			};
			StyleInput(moveUpButton);
			moveUpButton.AddThemeColorOverride("font_color", StsColors.gold);
			moveUpButton.TooltipText = "Move effect source up";
			moveUpButton.Disabled = index == 0;
			moveUpButton.Pressed += () =>
			{
				if (index <= 0 || index >= _createdEffectSourceIds.Count)
				{
					return;
				}
				(_createdEffectSourceIds[index - 1], _createdEffectSourceIds[index]) = (_createdEffectSourceIds[index], _createdEffectSourceIds[index - 1]);
				RebuildEffectSourceListUi();
				OnCreatedCardMetaChanged();
			};

			Button moveDownButton = new Button
			{
				Text = "\u25BC",
				Flat = true,
				FocusMode = FocusModeEnum.None,
				CustomMinimumSize = new Vector2(40, _fieldMinSize.Y),
				MouseFilter = MouseFilterEnum.Stop
			};
			StyleInput(moveDownButton);
			moveDownButton.AddThemeColorOverride("font_color", StsColors.gold);
			moveDownButton.TooltipText = "Move effect source down";
			moveDownButton.Disabled = index >= _createdEffectSourceIds.Count - 1;
			moveDownButton.Pressed += () =>
			{
				if (index < 0 || index >= _createdEffectSourceIds.Count - 1)
				{
					return;
				}
				(_createdEffectSourceIds[index], _createdEffectSourceIds[index + 1]) = (_createdEffectSourceIds[index + 1], _createdEffectSourceIds[index]);
				RebuildEffectSourceListUi();
				OnCreatedCardMetaChanged();
			};

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
			row.AddChild(moveUpButton);
			row.AddChild(moveDownButton);
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
		CardEditorCreatedCardEffectSourceSupport.EnsureEffectSourceDynamicVars(preview, isUpgradePreview: false);
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

		// Legacy Effect Sources are deprecated; use inline RunEffectSourceCard extra effects instead.
		List<ModelId> effectSourceIds = new List<ModelId>();
		CardEditorEffectSourcePlacement placement = CardEditorEffectSourcePlacement.BeforeCustomEffects;

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

		CardEditorCreatedCardsStore.SetDraftMeta(_cardId, enabled, title, pool, rarity, type, target, effectSourceIds, placement, portraitSourceId, customPortraitFile, fullArt, finish, customText, fp);
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

	private static string GetEffectSourcePowerAmountKey(ModelId powerId)
	{
		return EffectSourcePowerAmountPrefix + powerId;
	}

	private static bool TryParseEffectSourcePowerAmountKey(string key, out ModelId powerId)
	{
		powerId = ModelId.none;
		if (string.IsNullOrWhiteSpace(key) || !key.StartsWith(EffectSourcePowerAmountPrefix, StringComparison.Ordinal))
		{
			return false;
		}

		string idText = key.Substring(EffectSourcePowerAmountPrefix.Length);
		if (string.IsNullOrWhiteSpace(idText))
		{
			return false;
		}

		try
		{
			powerId = ModelId.Deserialize(idText);
		}
		catch
		{
			powerId = ModelId.none;
		}
		return powerId != ModelId.none;
	}

	private static LineEdit? FindFirstLineEdit(Control rowControl)
	{
		return rowControl?.GetChildren().OfType<LineEdit>().FirstOrDefault();
	}

	private void AddEffectSourceSpecialNumericRow(string key, string labelText, int value, int defaultValue, decimal minValue, decimal maxValue)
	{
		_effectSourceSpecialNumberDefaults[key] = defaultValue;

		if (!_effectSourceSpecialNumberRowControls.TryGetValue(key, out Control? rowControl)
			|| rowControl == null
			|| !GodotObject.IsInstanceValid(rowControl)
			|| rowControl is not HBoxContainer row)
		{
			row = CreateNumericRow(
				labelText,
				value.ToString(CultureInfo.InvariantCulture),
				out LineEdit field,
				minValue,
				maxValue,
				onChanged: QueuePreviewUpdate);
			_effectSourceSpecialNumberRowControls[key] = row;
			_effectSourceSpecialNumberFields[key] = field;
		}
		else if (!_effectSourceSpecialNumberFields.ContainsKey(key) && FindFirstLineEdit(row) is LineEdit existingField)
		{
			_effectSourceSpecialNumberFields[key] = existingField;
		}

		_effectSourceDynamicVarContainer?.AddChild(row);
	}

	private void BuildEffectSourceSpecialNumberRows(ModelId sourceId, CardModel sourceCard, CardOverride? currentOverride)
	{
		CardEditorOverrides.TryGet(sourceId, out CardOverride? sourceOverride);

		if (_drawCostReductionField == null && sourceId == ModelDb.GetId<KinglyKick>())
		{
			int defaultValue = sourceOverride?.DrawCostReduction.HasValue == true
				? Math.Max(0, sourceOverride.DrawCostReduction.Value)
				: 1;
			int value = currentOverride?.DrawCostReduction.HasValue == true
				? Math.Max(0, currentOverride.DrawCostReduction.Value)
				: defaultValue;
			AddEffectSourceSpecialNumericRow(EffectSourceDrawCostReductionKey, CardEditorLoc.T("field.costReductionOnDraw", "Cost Reduction on Draw"), value, defaultValue, 0, 99);
		}

		if (_resonanceEnemyStrengthLossField == null && sourceId == ModelDb.GetId<Resonance>())
		{
			int defaultValue = 1;
			if (sourceOverride?.DynamicVarBaseValues != null
				&& sourceOverride.DynamicVarBaseValues.TryGetValue(CardEditorOverrideKeys.ResonanceEnemyStrengthLoss, out decimal sourceLoss))
			{
				defaultValue = Math.Clamp((int)sourceLoss, 0, 99);
			}

			int value = currentOverride?.DynamicVarBaseValues != null
				&& currentOverride.DynamicVarBaseValues.TryGetValue(CardEditorOverrideKeys.ResonanceEnemyStrengthLoss, out decimal currentLoss)
				? Math.Clamp((int)currentLoss, 0, 99)
				: defaultValue;
			AddEffectSourceSpecialNumericRow(EffectSourceResonanceEnemyStrengthLossKey, CardEditorLoc.T("field.enemyStrengthLoss", "Enemy Strength Loss"), value, defaultValue, 0, 99);
		}

		if (_handDiscardCountField == null && CardEditorTargetedDiscardSupport.IsSupportedCard(sourceId))
		{
			int defaultValue = sourceOverride?.HandDiscardCount.HasValue == true
				? Math.Clamp(sourceOverride.HandDiscardCount.Value, 0, 99)
				: 1;
			int value = currentOverride?.HandDiscardCount.HasValue == true
				? Math.Clamp(currentOverride.HandDiscardCount.Value, 0, 99)
				: defaultValue;
			AddEffectSourceSpecialNumericRow(EffectSourceHandDiscardCountKey, CardEditorLoc.T("field.discardCount", "Discard"), value, defaultValue, 0, 99);
		}

		if (_sealedThroneStarsGainedField == null && sourceId == ModelDb.GetId<TheSealedThrone>())
		{
			ModelId powerId = ModelDb.GetId<TheSealedThronePower>();
			int defaultValue = sourceOverride?.PowerAmounts != null
				&& sourceOverride.PowerAmounts.TryGetValue(powerId, out decimal sourceAmount)
				? (int)sourceAmount
				: 1;
			int value = currentOverride?.PowerAmounts != null
				&& currentOverride.PowerAmounts.TryGetValue(powerId, out decimal currentAmount)
				? (int)currentAmount
				: defaultValue;
			AddEffectSourceSpecialNumericRow(GetEffectSourcePowerAmountKey(powerId), CardEditorLoc.T("field.starsGained", "Stars Gained"), value, defaultValue, 0, 99);
		}

		if (_retainHandTurnsField == null
			&& (sourceId == ModelDb.GetId<Convergence>() || sourceId == ModelDb.GetId<Equilibrium>() || sourceId == ModelDb.GetId<Salvo>()))
		{
			ModelId retainPowerId = ModelDb.GetId<RetainHandPower>();
			int defaultValue = 1;
			if (sourceOverride?.PowerAmounts != null
				&& sourceOverride.PowerAmounts.TryGetValue(retainPowerId, out decimal overridden))
			{
				defaultValue = Math.Clamp((int)overridden, 0, 99);
			}
			else if (sourceId == ModelDb.GetId<Equilibrium>() && sourceCard.DynamicVars.TryGetValue("Equilibrium", out var equilibriumTurns))
			{
				defaultValue = Math.Max(1, (int)equilibriumTurns.BaseValue);
			}

			int value = currentOverride?.PowerAmounts != null
				&& currentOverride.PowerAmounts.TryGetValue(retainPowerId, out decimal currentTurns)
				? Math.Clamp((int)currentTurns, 0, 99)
				: defaultValue;
			AddEffectSourceSpecialNumericRow(GetEffectSourcePowerAmountKey(retainPowerId), "Retain Hand Turns", value, defaultValue, 0, 99);
		}

		IReadOnlyList<CardEditorHardcodedPowerAmountSpec> specs = CardEditorHardcodedPowerAmounts.Get(sourceId);
		if (specs == null || specs.Count == 0)
		{
			return;
		}

		ModelId noDrawPowerId = ModelDb.GetId<NoDrawPower>();
		ModelId conquerorPowerId = ModelDb.GetId<ConquerorPower>();
		ModelId reflectPowerId = ModelDb.GetId<ReflectPower>();
		ModelId retainHandPowerIdFiltered = ModelDb.GetId<RetainHandPower>();
		ModelId sealedThronePowerId = ModelDb.GetId<TheSealedThronePower>();

		foreach (CardEditorHardcodedPowerAmountSpec spec in specs)
		{
			ModelId powerId = spec.PowerId;
			if (_hardcodedPowerAmountFields.ContainsKey(powerId)
				|| powerId == noDrawPowerId
				|| powerId == conquerorPowerId
				|| powerId == reflectPowerId
				|| powerId == retainHandPowerIdFiltered
				|| powerId == sealedThronePowerId)
			{
				continue;
			}

			PowerModel? power = ModelDb.GetByIdOrNull<PowerModel>(powerId);
			if (power == null || power.StackType != PowerStackType.Counter)
			{
				continue;
			}

			int defaultValue = spec.DefaultAmount;
			if (sourceOverride?.PowerAmounts != null && sourceOverride.PowerAmounts.TryGetValue(powerId, out decimal sourceAmount))
			{
				defaultValue = (int)sourceAmount;
			}

			int value = currentOverride?.PowerAmounts != null
				&& currentOverride.PowerAmounts.TryGetValue(powerId, out decimal currentAmount)
				? (int)currentAmount
				: defaultValue;

			string labelText = spec.LabelOverride;
			if (string.IsNullOrWhiteSpace(labelText))
			{
				string title = power.Title.GetFormattedText();
				labelText = string.IsNullOrWhiteSpace(title) ? power.Id.Entry : title;
			}

			AddEffectSourceSpecialNumericRow(GetEffectSourcePowerAmountKey(powerId), labelText, value, defaultValue, -99, 999);
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

		Button addEffectSource = new Button
		{
			Text = CardEditorLoc.T("button.addEffectSourceInline", "Add Effect Source"),
			CustomMinimumSize = _fieldMinSize,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(addEffectSource);
		addEffectSource.Pressed += () =>
		{
			OpenSpecificCardPicker(selectedId =>
			{
				if (selectedId == ModelId.none || selectedId == _cardId)
				{
					return;
				}

				CardExtraEffect effect = new CardExtraEffect
				{
					Kind = CardExtraEffectKind.RunEffectSourceCard,
					SpecificCardId = selectedId.ToString(),
					Trigger = CardExtraEffectTrigger.OnPlay,
					Target = CardExtraEffectTarget.Self
				};
				AddExtraEffectRow(effect);
				QueuePreviewUpdate();
			});
		};
		addEffectSource.Visible = !_isUpgradeEditor;

		HBoxContainer addButtonsRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		addButtonsRow.AddThemeConstantOverride("separation", 10);
		addButtonsRow.AddChild(add);
		addButtonsRow.AddChild(addEffectSource);

		VBoxContainer effectSection = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		effectSection.AddThemeConstantOverride("separation", 8);
		effectSection.AddChild(_extraEffectsContainer);
		effectSection.AddChild(addButtonsRow);

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
				(CardExtraEffect?[] alignedUpgradeEffects, List<CardExtraEffect> absoluteUpgradeEffects) = upgradeEffects != null
					? CardEditorExtraEffects.AlignUpgradeEffectsForEditor(baseEffects, upgradeEffects)
					: (new CardExtraEffect?[baseEffects.Count], new List<CardExtraEffect>());
				int baseCount = baseEffects.Count;
				for (int i = 0; i < baseCount; i++)
				{
					CardExtraEffect baseEffect = baseEffects[i];
					CardExtraEffect? upgradeEffect = i < alignedUpgradeEffects.Length ? alignedUpgradeEffects[i] : null;

					AddExtraEffectRow(BuildUpgradeDeltaRowEffect(baseEffect, upgradeEffect, numericFieldsAreDeltas), isUpgradeDeltaRow: true);
				}

				if (absoluteUpgradeEffects.Count > 0)
				{
					foreach (CardExtraEffect upgradeEffect in absoluteUpgradeEffects)
					{
						AddExtraEffectRow(upgradeEffect);
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

			return;
		}

		(List<CardExtraEffect> legacyBefore, List<CardExtraEffect> legacyAfter) = BuildLegacyCreatedCardEffectSourceExtraEffects(baseEffects);
		foreach (CardExtraEffect effect in legacyBefore)
		{
			AddExtraEffectRow(effect);
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

		foreach (CardExtraEffect effect in legacyAfter)
		{
			AddExtraEffectRow(effect);
		}
	}

	private (List<CardExtraEffect> Before, List<CardExtraEffect> After) BuildLegacyCreatedCardEffectSourceExtraEffects(List<CardExtraEffect>? baseEffects)
	{
		if (!_isCreatedCard || _isUpgradeEditor)
		{
			return (new List<CardExtraEffect>(), new List<CardExtraEffect>());
		}

		IReadOnlyList<ModelId> legacyEffectSourceIds = CardEditorCreatedCardsStore.GetEffectSourceCardIds(_cardId);
		if (legacyEffectSourceIds.Count == 0)
		{
			return (new List<CardExtraEffect>(), new List<CardExtraEffect>());
		}

		bool alreadyUsingInlineSources = baseEffects != null && baseEffects.Any(e => e != null && e.Kind == CardExtraEffectKind.RunEffectSourceCard);
		if (alreadyUsingInlineSources)
		{
			return (new List<CardExtraEffect>(), new List<CardExtraEffect>());
		}

		CardEditorEffectSourcePlacement placement = CardEditorCreatedCardsStore.GetEffectSourcePlacement(_cardId);

		List<CardExtraEffect> effectsToAdd = new List<CardExtraEffect>();
		foreach (ModelId sourceId in legacyEffectSourceIds)
		{
			if (sourceId == ModelId.none || sourceId == _cardId)
			{
				continue;
			}

			string idStr = sourceId.ToString();
			bool exists = baseEffects != null && baseEffects.Any(e =>
				e != null
				&& e.Kind == CardExtraEffectKind.RunEffectSourceCard
				&& string.Equals((e.SpecificCardId ?? string.Empty).Trim(), idStr, StringComparison.OrdinalIgnoreCase));
			if (exists)
			{
				continue;
			}

			effectsToAdd.Add(new CardExtraEffect
			{
				Kind = CardExtraEffectKind.RunEffectSourceCard,
				SpecificCardId = idStr,
				Trigger = CardExtraEffectTrigger.OnPlay,
				Target = CardExtraEffectTarget.Self
			});
		}

		if (effectsToAdd.Count == 0)
		{
			return (new List<CardExtraEffect>(), new List<CardExtraEffect>());
		}

		if (placement == CardEditorEffectSourcePlacement.BeforeCustomEffects)
		{
			return (effectsToAdd, new List<CardExtraEffect>());
		}

		return (new List<CardExtraEffect>(), effectsToAdd);
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
		CardExtraEffectKind initialSelectedKind = allowedDefinitions.Count > 0
			? allowedDefinitions[Math.Clamp(kindIndex, 0, allowedDefinitions.Count - 1)].Kind
			: CardExtraEffectKind.DealDamage;

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
			moveFromPileSelect.AddItem(CardEditorExtraEffects.CardPileLabel(pile), (int)pile);
		}
		int moveFromAllIndex = moveFromPileSelect.GetItemIndex((int)CardExtraEffectCardPile.AllPiles);
		if (moveFromAllIndex >= 0)
		{
			moveFromPileSelect.SetItemDisabled(moveFromAllIndex, true);
		}
		int defaultMoveFromPileId = initialSelectedKind is CardExtraEffectKind.DrawCards or CardExtraEffectKind.DrawCardsThatCostLess
			? (int)CardExtraEffectCardPile.DrawPile
			: (int)CardExtraEffectCardPile.DiscardPile;
		int moveFromId = effect != null ? (int)effect.CardSelectionPile : defaultMoveFromPileId;
		int moveFromIndex = moveFromPileSelect.GetItemIndex(moveFromId);
		if (moveFromIndex < 0)
		{
			moveFromIndex = moveFromPileSelect.GetItemIndex(defaultMoveFromPileId);
		}
		moveFromPileSelect.Select(Math.Max(0, moveFromIndex));

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
			moveSelectionModeSelect.AddItem(CardEditorExtraEffects.CardSelectionModeLabel(mode), (int)mode);
		}
		int moveModeId = effect != null ? (int)effect.CardSelectionMode : (int)CardExtraEffectCardSelectionMode.Choose;
		int moveModeIndex = moveSelectionModeSelect.GetItemIndex(moveModeId);
		if (moveModeIndex < 0)
		{
			moveModeIndex = moveSelectionModeSelect.GetItemIndex((int)CardExtraEffectCardSelectionMode.Choose);
		}
		moveSelectionModeSelect.Select(Math.Max(0, moveModeIndex));

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
			moveToPileSelect.AddItem(CardEditorExtraEffects.CardPileLabel(pile), (int)pile);
		}
		int moveToAllIndex = moveToPileSelect.GetItemIndex((int)CardExtraEffectCardPile.AllPiles);
		if (moveToAllIndex >= 0)
		{
			moveToPileSelect.SetItemDisabled(moveToAllIndex, true);
		}
		CardExtraEffectCardPile initialMoveToPile = effect != null
			? ((effect.Kind is CardExtraEffectKind.AddRandomCardToHand or CardExtraEffectKind.ChooseOneOfThreeCardsToHand) && !effect.UseMoveDestinationForGeneratedCards
				? CardExtraEffectCardPile.Hand
				: effect.MoveToPile)
			: CardExtraEffectCardPile.DrawPile;
		int moveToId = (int)initialMoveToPile;
		int moveToIndex = moveToPileSelect.GetItemIndex(moveToId);
		if (moveToIndex < 0)
		{
			moveToIndex = moveToPileSelect.GetItemIndex((int)CardExtraEffectCardPile.DrawPile);
		}
		moveToPileSelect.Select(Math.Max(0, moveToIndex));

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
			moveToPositionSelect.AddItem(CardEditorExtraEffects.CardPilePositionLabel(position), (int)position);
		}
		CardExtraEffectCardPilePosition initialMoveToPosition = effect != null
			? ((effect.Kind is CardExtraEffectKind.AddRandomCardToHand or CardExtraEffectKind.ChooseOneOfThreeCardsToHand) && !effect.UseMoveDestinationForGeneratedCards
				? CardExtraEffectCardPilePosition.Bottom
				: effect.MoveToPosition)
			: CardExtraEffectCardPilePosition.Top;
		int movePosId = (int)initialMoveToPosition;
		int movePosIndex = moveToPositionSelect.GetItemIndex(movePosId);
		if (movePosIndex < 0)
		{
			movePosIndex = moveToPositionSelect.GetItemIndex((int)CardExtraEffectCardPilePosition.Top);
		}
		moveToPositionSelect.Select(Math.Max(0, movePosIndex));

		moveCardsRowBottom.AddChild(moveToPileSelect);
		moveCardsRowBottom.AddChild(moveToPositionSelect);
		moveCardsRowBottom.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer additionalMoveToRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Visible = false };
		additionalMoveToRow.AddThemeConstantOverride("separation", 10);

		Label additionalMoveToLabel = new Label { Text = CardEditorLoc.T("additionalMoveTo.label", "Also To"), CustomMinimumSize = new Vector2(120, 0) };
		StyleBodyLabel(additionalMoveToLabel);
		additionalMoveToRow.AddChild(additionalMoveToLabel);

		PackedScene additionalMoveTickboxScene = GD.Load<PackedScene>("res://scenes/ui/tickbox.tscn");
		CardExtraEffectAdditionalMoveToPiles initialAdditionalMoveToPiles = effect?.AdditionalMoveToPiles ?? CardExtraEffectAdditionalMoveToPiles.None;

		Label additionalMoveToHandLabel = new Label { Text = CardEditorExtraEffects.CardPileLabel(CardExtraEffectCardPile.Hand) };
		KeywordTickbox additionalMoveToHandTickbox = new KeywordTickbox(
			additionalMoveTickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled),
			additionalMoveToHandLabel,
			initialAdditionalMoveToPiles.HasFlag(CardExtraEffectAdditionalMoveToPiles.Hand));
		StyleBodyLabel(additionalMoveToHandLabel);
		additionalMoveToHandTickbox.TooltipText = "Also create a copy in your Hand.";

		Label additionalMoveToDrawLabel = new Label { Text = CardEditorExtraEffects.CardPileLabel(CardExtraEffectCardPile.DrawPile) };
		KeywordTickbox additionalMoveToDrawTickbox = new KeywordTickbox(
			additionalMoveTickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled),
			additionalMoveToDrawLabel,
			initialAdditionalMoveToPiles.HasFlag(CardExtraEffectAdditionalMoveToPiles.DrawPile));
		StyleBodyLabel(additionalMoveToDrawLabel);
		additionalMoveToDrawTickbox.TooltipText = "Also create a copy in your Draw Pile.";

		Label additionalMoveToDiscardLabel = new Label { Text = CardEditorExtraEffects.CardPileLabel(CardExtraEffectCardPile.DiscardPile) };
		KeywordTickbox additionalMoveToDiscardTickbox = new KeywordTickbox(
			additionalMoveTickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled),
			additionalMoveToDiscardLabel,
			initialAdditionalMoveToPiles.HasFlag(CardExtraEffectAdditionalMoveToPiles.DiscardPile));
		StyleBodyLabel(additionalMoveToDiscardLabel);
		additionalMoveToDiscardTickbox.TooltipText = "Also create a copy in your Discard Pile.";

		Label additionalMoveToExhaustLabel = new Label { Text = CardEditorExtraEffects.CardPileLabel(CardExtraEffectCardPile.ExhaustPile) };
		KeywordTickbox additionalMoveToExhaustTickbox = new KeywordTickbox(
			additionalMoveTickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled),
			additionalMoveToExhaustLabel,
			initialAdditionalMoveToPiles.HasFlag(CardExtraEffectAdditionalMoveToPiles.ExhaustPile));
		StyleBodyLabel(additionalMoveToExhaustLabel);
		additionalMoveToExhaustTickbox.TooltipText = "Also create a copy in your Exhaust Pile.";

		void RefreshAdditionalMoveToTargets()
		{
			UpdateAdditionalMoveToTargets(moveToPileSelect, additionalMoveToHandTickbox, additionalMoveToDrawTickbox, additionalMoveToDiscardTickbox, additionalMoveToExhaustTickbox);
			QueuePreviewUpdate();
		}

		moveToPileSelect.ItemSelected += _ => RefreshAdditionalMoveToTargets();
		additionalMoveToHandTickbox.Toggled += RefreshAdditionalMoveToTargets;
		additionalMoveToDrawTickbox.Toggled += RefreshAdditionalMoveToTargets;
		additionalMoveToDiscardTickbox.Toggled += RefreshAdditionalMoveToTargets;
		additionalMoveToExhaustTickbox.Toggled += RefreshAdditionalMoveToTargets;

		additionalMoveToRow.AddChild(additionalMoveToHandTickbox);
		additionalMoveToRow.AddChild(additionalMoveToDrawTickbox);
		additionalMoveToRow.AddChild(additionalMoveToDiscardTickbox);
		additionalMoveToRow.AddChild(additionalMoveToExhaustTickbox);
		additionalMoveToRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
		UpdateAdditionalMoveToTargets(moveToPileSelect, additionalMoveToHandTickbox, additionalMoveToDrawTickbox, additionalMoveToDiscardTickbox, additionalMoveToExhaustTickbox);

		moveCardsRow.AddChild(moveCardsRowTop);
		moveCardsRow.AddChild(moveCardsRowBottom);
		moveCardsRow.AddChild(additionalMoveToRow);

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

		// Some upgrade base-slot fields are absolute overrides (not deltas). Mirror the stored upgrade effect so the UI
		// reflects what will actually happen in-game.
		if (upgradeEffect != null && baseEffect.Kind == CardExtraEffectKind.OrbAction && upgradeEffect.Kind == CardExtraEffectKind.OrbAction)
		{
			display.OrbAction = upgradeEffect.OrbAction;
			display.OrbScope = upgradeEffect.OrbScope;
			display.OrbType = upgradeEffect.OrbType;
			display.OrbSelection = upgradeEffect.OrbSelection;
			display.OrbFollowUp = upgradeEffect.OrbFollowUp;
		}

		if (upgradeEffect != null
			&& !string.Equals(
				baseEffect.CustomKeywordName?.Trim() ?? string.Empty,
				upgradeEffect.CustomKeywordName?.Trim() ?? string.Empty,
				StringComparison.Ordinal))
		{
			display.CustomKeywordName = string.IsNullOrWhiteSpace(upgradeEffect.CustomKeywordName)
				? null
				: upgradeEffect.CustomKeywordName.Trim();
		}

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
				|| IsHiddenUnifiedCardActionKind(def.Kind)
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
		CardExtraEffectKind initialSelectedKind = kindDefinitionIndices.Count > 0
			? CardEditorExtraEffects.Definitions[kindDefinitionIndices[Math.Clamp(kindIndex, 0, kindDefinitionIndices.Count - 1)]].Kind
			: CardExtraEffectKind.DealDamage;

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

		Button moveUp = new Button
		{
			Text = "\u25B2",
			Flat = true,
			FocusMode = FocusModeEnum.None,
			CustomMinimumSize = new Vector2(44, _fieldMinSize.Y),
			MouseFilter = MouseFilterEnum.Stop
		};
		StyleInput(moveUp);
		moveUp.AddThemeColorOverride("font_color", StsColors.gold);
		moveUp.TooltipText = "Move effect up";

		Button moveDown = new Button
		{
			Text = "\u25BC",
			Flat = true,
			FocusMode = FocusModeEnum.None,
			CustomMinimumSize = new Vector2(44, _fieldMinSize.Y),
			MouseFilter = MouseFilterEnum.Stop
		};
		StyleInput(moveDown);
		moveDown.AddThemeColorOverride("font_color", StsColors.gold);
		moveDown.TooltipText = "Move effect down";

		bool allowReorder = !_isUpgradeEditor && !isUpgradeDeltaRow;
		moveUp.Visible = allowReorder;
		moveDown.Visible = allowReorder;

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
			moveFromPileSelect.AddItem(CardEditorExtraEffects.CardPileLabel(pile), (int)pile);
		}
		int defaultMoveFromPileId = initialSelectedKind is CardExtraEffectKind.DrawCards or CardExtraEffectKind.DrawCardsThatCostLess
			? (int)CardExtraEffectCardPile.DrawPile
			: (int)CardExtraEffectCardPile.DiscardPile;
		int moveFromId = effect != null ? (int)effect.CardSelectionPile : defaultMoveFromPileId;
		int moveFromIndex = moveFromPileSelect.GetItemIndex(moveFromId);
		if (moveFromIndex < 0)
		{
			moveFromIndex = moveFromPileSelect.GetItemIndex(defaultMoveFromPileId);
		}
		moveFromPileSelect.Select(Math.Max(0, moveFromIndex));

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
			moveSelectionModeSelect.AddItem(CardEditorExtraEffects.CardSelectionModeLabel(mode), (int)mode);
		}
		int moveModeId = effect != null ? (int)effect.CardSelectionMode : (int)CardExtraEffectCardSelectionMode.Choose;
		int moveModeIndex = moveSelectionModeSelect.GetItemIndex(moveModeId);
		if (moveModeIndex < 0)
		{
			moveModeIndex = moveSelectionModeSelect.GetItemIndex((int)CardExtraEffectCardSelectionMode.Choose);
		}
		moveSelectionModeSelect.Select(Math.Max(0, moveModeIndex));

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
			moveToPileSelect.AddItem(CardEditorExtraEffects.CardPileLabel(pile), (int)pile);
		}
		CardExtraEffectCardPile initialMoveToPile = effect != null
			? ((effect.Kind is CardExtraEffectKind.AddRandomCardToHand or CardExtraEffectKind.ChooseOneOfThreeCardsToHand) && !effect.UseMoveDestinationForGeneratedCards
				? CardExtraEffectCardPile.Hand
				: effect.MoveToPile)
			: CardExtraEffectCardPile.DrawPile;
		int moveToId = (int)initialMoveToPile;
		int moveToIndex = moveToPileSelect.GetItemIndex(moveToId);
		if (moveToIndex < 0)
		{
			moveToIndex = moveToPileSelect.GetItemIndex((int)CardExtraEffectCardPile.DrawPile);
		}
		moveToPileSelect.Select(Math.Max(0, moveToIndex));

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
			moveToPositionSelect.AddItem(CardEditorExtraEffects.CardPilePositionLabel(position), (int)position);
		}
		CardExtraEffectCardPilePosition initialMoveToPosition = effect != null
			? ((effect.Kind is CardExtraEffectKind.AddRandomCardToHand or CardExtraEffectKind.ChooseOneOfThreeCardsToHand) && !effect.UseMoveDestinationForGeneratedCards
				? CardExtraEffectCardPilePosition.Bottom
				: effect.MoveToPosition)
			: CardExtraEffectCardPilePosition.Top;
		int movePosId = (int)initialMoveToPosition;
		int movePosIndex = moveToPositionSelect.GetItemIndex(movePosId);
		if (movePosIndex < 0)
		{
			movePosIndex = moveToPositionSelect.GetItemIndex((int)CardExtraEffectCardPilePosition.Top);
		}
		moveToPositionSelect.Select(Math.Max(0, movePosIndex));

		moveCardsRowBottom.AddChild(moveToPileSelect);
		moveCardsRowBottom.AddChild(moveToPositionSelect);
		moveCardsRowBottom.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer additionalMoveToRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Visible = false };
		additionalMoveToRow.AddThemeConstantOverride("separation", 10);

		Label additionalMoveToLabel = new Label { Text = CardEditorLoc.T("additionalMoveTo.label", "Also To"), CustomMinimumSize = new Vector2(120, 0) };
		StyleBodyLabel(additionalMoveToLabel);
		additionalMoveToRow.AddChild(additionalMoveToLabel);

		PackedScene additionalMoveTickboxScene = GD.Load<PackedScene>("res://scenes/ui/tickbox.tscn");
		CardExtraEffectAdditionalMoveToPiles initialAdditionalMoveToPiles = effect?.AdditionalMoveToPiles ?? CardExtraEffectAdditionalMoveToPiles.None;

		Label additionalMoveToHandLabel = new Label { Text = CardEditorExtraEffects.CardPileLabel(CardExtraEffectCardPile.Hand) };
		KeywordTickbox additionalMoveToHandTickbox = new KeywordTickbox(
			additionalMoveTickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled),
			additionalMoveToHandLabel,
			initialAdditionalMoveToPiles.HasFlag(CardExtraEffectAdditionalMoveToPiles.Hand));
		StyleBodyLabel(additionalMoveToHandLabel);
		additionalMoveToHandTickbox.TooltipText = "Also create a copy in your Hand.";

		Label additionalMoveToDrawLabel = new Label { Text = CardEditorExtraEffects.CardPileLabel(CardExtraEffectCardPile.DrawPile) };
		KeywordTickbox additionalMoveToDrawTickbox = new KeywordTickbox(
			additionalMoveTickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled),
			additionalMoveToDrawLabel,
			initialAdditionalMoveToPiles.HasFlag(CardExtraEffectAdditionalMoveToPiles.DrawPile));
		StyleBodyLabel(additionalMoveToDrawLabel);
		additionalMoveToDrawTickbox.TooltipText = "Also create a copy in your Draw Pile.";

		Label additionalMoveToDiscardLabel = new Label { Text = CardEditorExtraEffects.CardPileLabel(CardExtraEffectCardPile.DiscardPile) };
		KeywordTickbox additionalMoveToDiscardTickbox = new KeywordTickbox(
			additionalMoveTickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled),
			additionalMoveToDiscardLabel,
			initialAdditionalMoveToPiles.HasFlag(CardExtraEffectAdditionalMoveToPiles.DiscardPile));
		StyleBodyLabel(additionalMoveToDiscardLabel);
		additionalMoveToDiscardTickbox.TooltipText = "Also create a copy in your Discard Pile.";

		Label additionalMoveToExhaustLabel = new Label { Text = CardEditorExtraEffects.CardPileLabel(CardExtraEffectCardPile.ExhaustPile) };
		KeywordTickbox additionalMoveToExhaustTickbox = new KeywordTickbox(
			additionalMoveTickboxScene.Instantiate<Control>(PackedScene.GenEditState.Disabled),
			additionalMoveToExhaustLabel,
			initialAdditionalMoveToPiles.HasFlag(CardExtraEffectAdditionalMoveToPiles.ExhaustPile));
		StyleBodyLabel(additionalMoveToExhaustLabel);
		additionalMoveToExhaustTickbox.TooltipText = "Also create a copy in your Exhaust Pile.";

		void RefreshAdditionalMoveToTargets()
		{
			UpdateAdditionalMoveToTargets(moveToPileSelect, additionalMoveToHandTickbox, additionalMoveToDrawTickbox, additionalMoveToDiscardTickbox, additionalMoveToExhaustTickbox);
			QueuePreviewUpdate();
		}

		moveToPileSelect.ItemSelected += _ => RefreshAdditionalMoveToTargets();
		additionalMoveToHandTickbox.Toggled += RefreshAdditionalMoveToTargets;
		additionalMoveToDrawTickbox.Toggled += RefreshAdditionalMoveToTargets;
		additionalMoveToDiscardTickbox.Toggled += RefreshAdditionalMoveToTargets;
		additionalMoveToExhaustTickbox.Toggled += RefreshAdditionalMoveToTargets;

		additionalMoveToRow.AddChild(additionalMoveToHandTickbox);
		additionalMoveToRow.AddChild(additionalMoveToDrawTickbox);
		additionalMoveToRow.AddChild(additionalMoveToDiscardTickbox);
		additionalMoveToRow.AddChild(additionalMoveToExhaustTickbox);
		additionalMoveToRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
		UpdateAdditionalMoveToTargets(moveToPileSelect, additionalMoveToHandTickbox, additionalMoveToDrawTickbox, additionalMoveToDiscardTickbox, additionalMoveToExhaustTickbox);

		moveCardsRow.AddChild(moveCardsRowTop);
		moveCardsRow.AddChild(moveCardsRowBottom);
		moveCardsRow.AddChild(additionalMoveToRow);

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

		HBoxContainer cardMatchRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		cardMatchRow.AddThemeConstantOverride("separation", 10);
		cardMatchRow.Visible = false;

		Label cardMatchLabel = new Label { Text = CardEditorLoc.T("cardMatch.label", "Match"), CustomMinimumSize = new Vector2(120, 0) };
		StyleBodyLabel(cardMatchLabel);

		OptionButton cardMatchModeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(160, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
		};
		StyleInput(cardMatchModeSelect);
		ConstrainOptionButtonPopup(cardMatchModeSelect);
		cardMatchModeSelect.TooltipText = CardEditorLoc.T("tooltip.cardMatchMode", "Optional: only affect cards that match this filter.");
		cardMatchModeSelect.AddItem(CardEditorLoc.T("cardMatch.any", "Any"), (int)CardExtraEffectCardMatchMode.Any);
		cardMatchModeSelect.AddItem(CardEditorLoc.T("cardMatch.cardId", "Card Id"), (int)CardExtraEffectCardMatchMode.CardId);
		cardMatchModeSelect.AddItem(CardEditorLoc.T("cardMatch.tag", "Tag"), (int)CardExtraEffectCardMatchMode.Tag);
		int initialMatchModeId = effect != null ? (int)effect.CardMatchMode : (int)CardExtraEffectCardMatchMode.Any;
		int initialMatchModeIndex = cardMatchModeSelect.GetItemIndex(initialMatchModeId);
		if (initialMatchModeIndex < 0) initialMatchModeIndex = 0;
		cardMatchModeSelect.Select(initialMatchModeIndex);

		NMegaLineEdit matchCardIdField = new NMegaLineEdit
		{
			Text = effect?.MatchCardId ?? string.Empty,
			CustomMinimumSize = _fieldMinSize
		};
		matchCardIdField.PlaceholderText = "cards.shiv";
		matchCardIdField.TooltipText = CardEditorLoc.T("tooltip.matchCardId", "Only affect cards with this card model id (e.g. cards.shiv).");
		StyleInput(matchCardIdField);
		matchCardIdField.TextChanged += _ => QueuePreviewUpdate();

		Button pickMatchCardIdButton = new Button
		{
			Text = CardEditorLoc.T("ui.cardPicker.button", "Pick"),
			CustomMinimumSize = new Vector2(90, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
		};
		pickMatchCardIdButton.TooltipText = CardEditorLoc.T("ui.cardPicker.tooltip", "Pick a card from the full card library and fill in its id.");
		StyleInput(pickMatchCardIdButton);
		pickMatchCardIdButton.Pressed += () =>
		{
			OpenSpecificCardPicker(selectedId =>
			{
				matchCardIdField.Text = selectedId.ToString();
				QueuePreviewUpdate();
			});
		};

		OptionButton matchTagKindSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(140, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
		};
		StyleInput(matchTagKindSelect);
		ConstrainOptionButtonPopup(matchTagKindSelect);
		matchTagKindSelect.TooltipText = CardEditorLoc.T("tooltip.matchTagKind", "Choose whether to match a vanilla tag or a custom tag.");
		matchTagKindSelect.AddItem(CardEditorLoc.T("cardMatch.tagKind.vanilla", "Vanilla"), (int)CardExtraEffectCardMatchTagKind.Vanilla);
		matchTagKindSelect.AddItem(CardEditorLoc.T("cardMatch.tagKind.custom", "Custom"), (int)CardExtraEffectCardMatchTagKind.Custom);
		int initialTagKindId = effect != null ? (int)effect.MatchTagKind : (int)CardExtraEffectCardMatchTagKind.Vanilla;
		int initialTagKindIndex = matchTagKindSelect.GetItemIndex(initialTagKindId);
		if (initialTagKindIndex < 0) initialTagKindIndex = 0;
		matchTagKindSelect.Select(initialTagKindIndex);

		OptionButton matchVanillaTagSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(200, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(matchVanillaTagSelect);
		ConstrainOptionButtonPopup(matchVanillaTagSelect);
		matchVanillaTagSelect.TooltipText = CardEditorLoc.T("tooltip.matchVanillaTag", "Only affect cards that have this vanilla tag.");
		matchVanillaTagSelect.AddItem(CardEditorLoc.T("cardMatch.tag.any", "Any Tag"), (int)CardTag.None);
		foreach (CardTag tag in Enum.GetValues<CardTag>())
		{
			if (tag == CardTag.None) continue;
			matchVanillaTagSelect.AddItem(GetTagDisplayName(tag), (int)tag);
		}
		CardTag initialVanillaTag = effect?.MatchVanillaTag ?? CardTag.None;
		int initialVanillaTagIndex = matchVanillaTagSelect.GetItemIndex((int)initialVanillaTag);
		if (initialVanillaTagIndex < 0) initialVanillaTagIndex = 0;
		matchVanillaTagSelect.Select(initialVanillaTagIndex);
		matchVanillaTagSelect.ItemSelected += _ => QueuePreviewUpdate();

		List<string> customTagOptions = GetAllKnownCustomTags();
		OptionButton matchCustomTagSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(220, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(matchCustomTagSelect);
		ConstrainOptionButtonPopup(matchCustomTagSelect);
		matchCustomTagSelect.TooltipText = CardEditorLoc.T("tooltip.matchCustomTag", "Only affect cards that have this custom tag.");
		matchCustomTagSelect.AddItem(CardEditorLoc.T("cardMatch.tag.any", "Any Tag"), 0);
		for (int i = 0; i < customTagOptions.Count; i++)
		{
			matchCustomTagSelect.AddItem(customTagOptions[i], i + 1);
		}
		string initialCustomTag = effect?.MatchCustomTag?.Trim() ?? string.Empty;
		int initialCustomTagIndex = 0;
		if (!string.IsNullOrWhiteSpace(initialCustomTag))
		{
			int idx = customTagOptions.FindIndex(t => string.Equals(t, initialCustomTag, StringComparison.OrdinalIgnoreCase));
			if (idx >= 0)
			{
				initialCustomTagIndex = matchCustomTagSelect.GetItemIndex(idx + 1);
			}
		}
		matchCustomTagSelect.Select(Math.Max(0, initialCustomTagIndex));
		matchCustomTagSelect.ItemSelected += _ => QueuePreviewUpdate();

		void SyncCardMatchRowControls()
		{
			CardExtraEffectCardMatchMode mode = CardExtraEffectCardMatchMode.Any;
			if (cardMatchModeSelect.Selected >= 0 && cardMatchModeSelect.Selected < cardMatchModeSelect.ItemCount)
			{
				int id = cardMatchModeSelect.GetItemId(cardMatchModeSelect.Selected);
				if (Enum.IsDefined(typeof(CardExtraEffectCardMatchMode), id))
				{
					mode = (CardExtraEffectCardMatchMode)id;
				}
			}

			bool isCardId = mode == CardExtraEffectCardMatchMode.CardId;
			bool isTag = mode == CardExtraEffectCardMatchMode.Tag;

			matchCardIdField.Visible = isCardId;
			pickMatchCardIdButton.Visible = isCardId;

			matchTagKindSelect.Visible = isTag;
			matchVanillaTagSelect.Visible = isTag && matchTagKindSelect.Selected == 0;
			matchCustomTagSelect.Visible = isTag && matchTagKindSelect.Selected == 1;
		}

		cardMatchModeSelect.ItemSelected += _ =>
		{
			SyncCardMatchRowControls();
			QueuePreviewUpdate();
		};
		matchTagKindSelect.ItemSelected += _ =>
		{
			SyncCardMatchRowControls();
			QueuePreviewUpdate();
		};

		SyncCardMatchRowControls();

		cardMatchRow.AddChild(cardMatchLabel);
		cardMatchRow.AddChild(cardMatchModeSelect);
		cardMatchRow.AddChild(matchCardIdField);
		cardMatchRow.AddChild(pickMatchCardIdButton);
		cardMatchRow.AddChild(matchTagKindSelect);
		cardMatchRow.AddChild(matchVanillaTagSelect);
		cardMatchRow.AddChild(matchCustomTagSelect);
		cardMatchRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		// CardMatchRow is added at the wrapper-level so it can be used both for pile operations
		// (e.g. Exhaust Cards) and as an optional filter for scaling count events.

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

		int initialDrawCostLess = initialDrawCostEnabled
			? (effect?.CardSelectionCount ?? (isUpgradeDeltaRow ? 0 : 1))
			: (isUpgradeDeltaRow ? 0 : 1);
		if (!isUpgradeDeltaRow && initialDrawCostLess <= 0)
		{
			initialDrawCostLess = 1;
		}
		NMegaLineEdit drawCostField = new NMegaLineEdit
		{
			Text = (isUpgradeDeltaRow
				? Math.Clamp(initialDrawCostLess, -99, 99)
				: Math.Clamp(initialDrawCostLess, 1, 99)).ToString(CultureInfo.InvariantCulture),
			CustomMinimumSize = _amountFieldMinSize
		};
		drawCostField.Alignment = HorizontalAlignment.Center;
		drawCostField.TooltipText = isUpgradeDeltaRow
			? CardEditorLoc.T("tooltip.drawCostLessAmountUpgrade", "Upgrade delta for how much less the drawn cards cost.")
			: CardEditorLoc.T("tooltip.drawCostLessAmount", "How much less the drawn cards cost (minimum 1).");
		StyleInput(drawCostField);

		Control drawCostSpin = CreateSpinButtons(drawCostField, step: 1m, minValue: isUpgradeDeltaRow ? -99m : 1m, maxValue: 99m, isInteger: true);

		drawCostRow.AddChild(drawCostLabel);
		drawCostRow.AddChild(drawCostTickbox);
		drawCostRow.AddChild(drawCostSpin);
		drawCostRow.AddChild(drawCostField);
		drawCostRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer keywordGroupRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		keywordGroupRow.AddThemeConstantOverride("separation", 10);

		Label keywordGroupLabel = new Label
		{
			Text = CardEditorLoc.T("keywordGroup.label", "Keyword"),
			CustomMinimumSize = new Vector2(120, 0)
		};
		StyleBodyLabel(keywordGroupLabel);

		NMegaLineEdit keywordGroupField = new NMegaLineEdit
		{
			Text = effect?.CustomKeywordName?.Trim() ?? string.Empty,
			CustomMinimumSize = _fieldMinSize,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		keywordGroupField.PlaceholderText = CardEditorLoc.T("keywordGroup.placeholder", "Optional packaged keyword");
		keywordGroupField.TooltipText = CardEditorLoc.T("tooltip.keywordGroup", "Optional: effects sharing this name collapse into one gold keyword line on the card. Hovering the keyword shows the packaged effects.");
		StyleInput(keywordGroupField);
		keywordGroupField.TextChanged += _ => QueuePreviewUpdate();

		keywordGroupRow.AddChild(keywordGroupLabel);
		keywordGroupRow.AddChild(keywordGroupField);

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

		HBoxContainer applyPowerRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		applyPowerRow.AddThemeConstantOverride("separation", 10);
		applyPowerRow.Visible = false;

		Label applyPowerLabel = new Label { Text = "Power", CustomMinimumSize = new Vector2(120, 0) };
		StyleBodyLabel(applyPowerLabel);

		OptionButton applyPowerSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(320, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(applyPowerSelect);
		ConstrainOptionButtonPopup(applyPowerSelect);
		applyPowerSelect.TooltipText = "Which power/status this effect applies.";
		PopulateExtraEffectPowerSelect(applyPowerSelect, effect?.PowerId);
		applyPowerSelect.ItemSelected += _ => QueuePreviewUpdate();

		applyPowerRow.AddChild(applyPowerLabel);
		applyPowerRow.AddChild(applyPowerSelect);
		applyPowerRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

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

		HBoxContainer transformModeRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Visible = false };
		transformModeRow.AddThemeConstantOverride("separation", 10);

		HBoxContainer conditionalBonusRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Visible = false };
		conditionalBonusRow.AddThemeConstantOverride("separation", 10);

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
		orbSelectionSelect.TooltipText = "Choose which matching orb to use.\nMiddle: if there is an even number, it defaults to the middle orb closest to the right.";
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

		Label ignoreVariantLabel = new Label { Text = CardEditorLoc.T("ignoreEffects.label", "Rule"), CustomMinimumSize = new Vector2(120, 0) };
		StyleBodyLabel(ignoreVariantLabel);

		OptionButton ignoreVariantSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(260, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(ignoreVariantSelect);
		ConstrainOptionButtonPopup(ignoreVariantSelect);
		ignoreVariantSelect.TooltipText = CardEditorLoc.T("tooltip.ignoreEffects", "Choose which damage rule this card modifies.");
		ignoreVariantSelect.AddItem("Ignore Block");
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

		HBoxContainer cardActionVariantRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Visible = false };
		cardActionVariantRow.AddThemeConstantOverride("separation", 10);

		Label cardActionVariantLabel = new Label { Text = CardEditorLoc.T("cardAction.label", "Action"), CustomMinimumSize = new Vector2(120, 0) };
		StyleBodyLabel(cardActionVariantLabel);

		OptionButton cardActionVariantSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(260, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(cardActionVariantSelect);
		ConstrainOptionButtonPopup(cardActionVariantSelect);
		cardActionVariantSelect.TooltipText = CardEditorLoc.T("tooltip.cardAction", "Choose which pile action this effect uses. The selector and filter knobs below are shared across these actions.");
		cardActionVariantSelect.AddItem(CardEditorLoc.T("cardAction.variant.move", "Move Between Piles"));
		cardActionVariantSelect.AddItem(CardEditorLoc.T("cardAction.variant.play", "Play From Pile"));
		cardActionVariantSelect.AddItem(CardEditorLoc.T("cardAction.variant.discard", "Discard"));
		cardActionVariantSelect.AddItem(CardEditorLoc.T("cardAction.variant.exhaust", "Exhaust"));
		cardActionVariantSelect.AddItem(CardEditorLoc.T("cardAction.variant.transform", "Transform"));
		cardActionVariantSelect.AddItem(CardEditorLoc.T("cardAction.variant.keyword", "Grant Keyword"));
		cardActionVariantSelect.AddItem(CardEditorLoc.T("cardAction.variant.extraEffect", "Grant Extra Effect"));
		cardActionVariantSelect.AddItem(CardEditorLoc.T("cardAction.variant.upgradePile", "Upgrade in Pile"));
		cardActionVariantSelect.AddItem(CardEditorLoc.T("cardAction.variant.upgradeDeck", "Upgrade Deck"));
		cardActionVariantSelect.AddItem(CardEditorLoc.T("cardAction.variant.copyPileToDeck", "Copy Pile to Deck"));
		cardActionVariantSelect.AddItem(CardEditorLoc.T("cardAction.variant.exactCopyPileToDeck", "Exact Copy Pile to Deck"));
		cardActionVariantSelect.AddItem(CardEditorLoc.T("cardAction.variant.removeDeck", "Remove From Deck"));
		int cardActionVariantIndex = effect != null ? (int)GetUnifiedCardActionVariant(effect.Kind) : (int)UnifiedCardActionVariant.MoveBetweenPiles;
		if (cardActionVariantIndex < 0 || cardActionVariantIndex >= cardActionVariantSelect.ItemCount)
		{
			cardActionVariantIndex = 0;
		}
		cardActionVariantSelect.Select(cardActionVariantIndex);

		cardActionVariantRow.AddChild(cardActionVariantLabel);
		cardActionVariantRow.AddChild(cardActionVariantSelect);
		cardActionVariantRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

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
		cardGenerationVariantSelect.AddItem("Add Random Card");
		cardGenerationVariantSelect.AddItem("Choose 1 of 3 Cards");
		cardGenerationVariantSelect.AddItem("Add Copy of This Card");
		cardGenerationVariantSelect.AddItem("Add Exact Copy to Deck");
		cardGenerationVariantSelect.AddItem("Add Specific Card");
		cardGenerationVariantSelect.AddItem("Fetch Specific Card (Preserves State)");
		cardGenerationVariantSelect.AddItem("Created Cards Cost Less");
		cardGenerationVariantSelect.AddItem("Created Cards Are Upgraded");
		int cardGenerationVariantIndex = effect?.Kind switch
		{
			CardExtraEffectKind.ChooseOneOfThreeCardsToHand => (int)UnifiedCardGenerationVariant.ChooseOneOfThree,
			CardExtraEffectKind.AddCopyOfThisCard => (int)UnifiedCardGenerationVariant.CopyOfThisCard,
			CardExtraEffectKind.AddExactCopyOfThisCardToDeck => (int)UnifiedCardGenerationVariant.ExactCopyOfThisCardToDeck,
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

		Label transformModeLabel = new Label { Text = "Transform Into", CustomMinimumSize = new Vector2(120, 0) };
		StyleBodyLabel(transformModeLabel);

		OptionButton transformModeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(260, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(transformModeSelect);
		ConstrainOptionButtonPopup(transformModeSelect);
		transformModeSelect.TooltipText = "Choose whether to transform into random cards, or into a specific card id.";
		foreach (CardExtraEffectTransformMode mode in Enum.GetValues<CardExtraEffectTransformMode>())
		{
			transformModeSelect.AddItem(CardEditorExtraEffects.TransformModeLabel(mode));
		}
		int transformModeIndex = effect != null ? (int)effect.TransformMode : (int)CardExtraEffectTransformMode.Random;
		if (transformModeIndex < 0 || transformModeIndex >= Enum.GetValues<CardExtraEffectTransformMode>().Length)
		{
			transformModeIndex = (int)CardExtraEffectTransformMode.Random;
		}
		transformModeSelect.Select(transformModeIndex);

		transformModeRow.AddChild(transformModeLabel);
		transformModeRow.AddChild(transformModeSelect);
		transformModeRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		Label conditionalBonusLabel = new Label { Text = CardEditorLoc.T("ui.conditionalBonus", "Conditional Bonus"), CustomMinimumSize = new Vector2(120, 0) };
		StyleBodyLabel(conditionalBonusLabel);

		OptionButton conditionalBonusConditionSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(240, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(conditionalBonusConditionSelect);
		ConstrainOptionButtonPopup(conditionalBonusConditionSelect);
		conditionalBonusConditionSelect.TooltipText = "Optional: add a bonus to this effect's amount when the condition is true.";
		foreach (CardExtraEffectConditionalBonusCondition cond in Enum.GetValues<CardExtraEffectConditionalBonusCondition>())
		{
			conditionalBonusConditionSelect.AddItem(CardEditorExtraEffects.ConditionalBonusConditionLabel(cond));
		}
		int conditionalBonusConditionIndex = effect != null ? (int)effect.ConditionalBonusCondition : (int)CardExtraEffectConditionalBonusCondition.None;
		if (conditionalBonusConditionIndex < 0 || conditionalBonusConditionIndex >= Enum.GetValues<CardExtraEffectConditionalBonusCondition>().Length)
		{
			conditionalBonusConditionIndex = (int)CardExtraEffectConditionalBonusCondition.None;
		}
		conditionalBonusConditionSelect.Select(conditionalBonusConditionIndex);

		OptionButton conditionalBonusEnemyStatusSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(220, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(conditionalBonusEnemyStatusSelect);
		ConstrainOptionButtonPopup(conditionalBonusEnemyStatusSelect);
		conditionalBonusEnemyStatusSelect.TooltipText = "Enemy status to check for the conditional bonus.";
		foreach (CardExtraEffectEnemyStatus status in Enum.GetValues<CardExtraEffectEnemyStatus>())
		{
			conditionalBonusEnemyStatusSelect.AddItem(CardEditorExtraEffects.EnemyStatusLabel(status));
		}
		int conditionalBonusEnemyStatusIndex = effect != null ? (int)effect.ConditionalBonusEnemyStatus : (int)CardExtraEffectEnemyStatus.Weak;
		if (conditionalBonusEnemyStatusIndex < 0 || conditionalBonusEnemyStatusIndex >= Enum.GetValues<CardExtraEffectEnemyStatus>().Length)
		{
			conditionalBonusEnemyStatusIndex = (int)CardExtraEffectEnemyStatus.Weak;
		}
		conditionalBonusEnemyStatusSelect.Select(conditionalBonusEnemyStatusIndex);

		OptionButton conditionalBonusEnemyIntentSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(220, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(conditionalBonusEnemyIntentSelect);
		ConstrainOptionButtonPopup(conditionalBonusEnemyIntentSelect);
		conditionalBonusEnemyIntentSelect.TooltipText = "Enemy intent to check for the conditional bonus.";
		foreach (CardExtraEffectEnemyIntent intent in Enum.GetValues<CardExtraEffectEnemyIntent>())
		{
			conditionalBonusEnemyIntentSelect.AddItem(CardEditorExtraEffects.EnemyIntentLabel(intent));
		}
		int conditionalBonusEnemyIntentIndex = effect != null ? (int)effect.ConditionalBonusEnemyIntent : (int)CardExtraEffectEnemyIntent.Attack;
		if (conditionalBonusEnemyIntentIndex < 0 || conditionalBonusEnemyIntentIndex >= Enum.GetValues<CardExtraEffectEnemyIntent>().Length)
		{
			conditionalBonusEnemyIntentIndex = (int)CardExtraEffectEnemyIntent.Attack;
		}
		conditionalBonusEnemyIntentSelect.Select(conditionalBonusEnemyIntentIndex);

		NMegaLineEdit conditionalBonusAmountField = new NMegaLineEdit
		{
			Text = (effect?.ConditionalBonusAmount ?? 0).ToString(CultureInfo.InvariantCulture),
			CustomMinimumSize = _amountFieldMinSize,
			Alignment = HorizontalAlignment.Center
		};
		conditionalBonusAmountField.TooltipText = "Amount added when the conditional bonus passes (can be negative).";
		StyleInput(conditionalBonusAmountField);
		conditionalBonusAmountField.TextChanged += _ => QueuePreviewUpdate();
		Control conditionalBonusSpin = CreateSpinButtons(conditionalBonusAmountField, step: 1m, minValue: -99m, maxValue: 99m, isInteger: true);

		conditionalBonusRow.AddChild(conditionalBonusLabel);
		conditionalBonusRow.AddChild(conditionalBonusConditionSelect);
		conditionalBonusRow.AddChild(conditionalBonusEnemyStatusSelect);
		conditionalBonusRow.AddChild(conditionalBonusEnemyIntentSelect);
		conditionalBonusRow.AddChild(conditionalBonusSpin);
		conditionalBonusRow.AddChild(conditionalBonusAmountField);
		conditionalBonusRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

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

		HBoxContainer countSourceToggleRow = CreateTickboxRow(
			CardEditorLoc.T("ui.count.excludeSource", "Count Source"),
			effect?.CountExcludeSourceCard ?? false,
			out KeywordTickbox countExcludeSourceTickbox,
			onToggled: QueuePreviewUpdate);
		countSourceToggleRow.Visible = false;
		countExcludeSourceTickbox.TooltipText = "Exclude this card itself from pile counting. Useful for 'other cards in hand' style scaling.";

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
		countOrbSelectionSelect.TooltipText = "Choose which orb position to check when using a position-based orb condition.\nMiddle: if there is an even number, it defaults to the middle orb closest to the right.";
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
		scalingRow.AddChild(countSourceToggleRow);
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
		powerCountEventRow.AddChild(powerCountEventLabel);
		powerCountEventRow.AddChild(powerCountEventSelect);
		powerCountEventRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer powerCountEnemyStatusRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		powerCountEnemyStatusRow.AddThemeConstantOverride("separation", 10);
		powerCountEnemyStatusRow.Visible = false;

		Label powerCountEnemyStatusLabel = new Label { Text = CardEditorLoc.T("ui.powerCountEnemyStatus", "Status"), CustomMinimumSize = new Vector2(120, 0) };
		StyleBodyLabel(powerCountEnemyStatusLabel);

		OptionButton powerCountEnemyStatusSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(240, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(powerCountEnemyStatusSelect);
		ConstrainOptionButtonPopup(powerCountEnemyStatusSelect);
		powerCountEnemyStatusSelect.TooltipText = "Power trigger: only trigger when this gained/lost power or status matches.";
		foreach (CardExtraEffectEnemyStatus status in Enum.GetValues<CardExtraEffectEnemyStatus>())
		{
			powerCountEnemyStatusSelect.AddItem(CardEditorExtraEffects.EnemyStatusLabel(status));
		}
		int powerCountEnemyStatusIndex = effect != null ? (int)effect.PowerTriggerEnemyStatus : (int)CardExtraEffectEnemyStatus.AnyPowerStatus;
		if (powerCountEnemyStatusIndex < 0 || powerCountEnemyStatusIndex >= Enum.GetValues<CardExtraEffectEnemyStatus>().Length)
		{
			powerCountEnemyStatusIndex = (int)CardExtraEffectEnemyStatus.AnyPowerStatus;
		}
		powerCountEnemyStatusSelect.Select(powerCountEnemyStatusIndex);
		powerCountEnemyStatusRow.AddChild(powerCountEnemyStatusLabel);
		powerCountEnemyStatusRow.AddChild(powerCountEnemyStatusSelect);
		powerCountEnemyStatusRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		HBoxContainer powerFilterRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		powerFilterRow.AddThemeConstantOverride("separation", 10);

		bool usesCardSelectionFilterControls = UsesCardSelectionFilterControls(initialSelectedKind);
		bool usesDedicatedDrawTargetFilters = initialSelectedKind is CardExtraEffectKind.DrawCards or CardExtraEffectKind.DrawCardsThatCostLess;
		OptionButton triggerPoolSelect = CreateGeneratedPoolSelect(
			(!usesCardSelectionFilterControls || usesDedicatedDrawTargetFilters) ? (effect?.TriggerCardPool ?? CardGeneratedCardPool.All) : (effect?.CardSelectionPool ?? CardGeneratedCardPool.All),
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
		int triggerTypeIndex = effect != null
			? ((!usesCardSelectionFilterControls || usesDedicatedDrawTargetFilters) ? (int)effect.TriggerCardType : (int)effect.CardSelectionType)
			: (int)CardGeneratedCardType.Any;
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
		int triggerFilterIndex = effect != null
			? ((!usesCardSelectionFilterControls || usesDedicatedDrawTargetFilters) ? (int)effect.TriggerCardFilter : (int)effect.CardSelectionFilter)
			: (int)CardExtraEffectCountCardFilter.Any;
		if (triggerFilterIndex < 0 || triggerFilterIndex >= Enum.GetValues<CardExtraEffectCountCardFilter>().Length)
		{
			triggerFilterIndex = (int)CardExtraEffectCountCardFilter.Any;
		}
		triggerFilterSelect.Select(triggerFilterIndex);

		triggerPoolSelect.ItemSelected += _ => QueuePreviewUpdate();
		triggerTypeSelect.ItemSelected += _ => QueuePreviewUpdate();
		triggerFilterSelect.ItemSelected += _ => QueuePreviewUpdate();

		HBoxContainer drawTargetFilterRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		drawTargetFilterRow.AddThemeConstantOverride("separation", 10);
		drawTargetFilterRow.Visible = false;

		Label drawTargetLabel = new Label
		{
			Text = CardEditorLoc.T("ui.drawTarget", "Draw"),
			CustomMinimumSize = new Vector2(120, 0)
		};
		StyleBodyLabel(drawTargetLabel);

		OptionButton drawTargetPoolSelect = CreateGeneratedPoolSelect(
			effect?.CardSelectionPool ?? CardGeneratedCardPool.All,
			CardGeneratedCardPool.All,
			"Draw target: only draw cards from this pool (color/class).");

		OptionButton drawTargetTypeSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(drawTargetTypeSelect);
		ConstrainOptionButtonPopup(drawTargetTypeSelect);
		drawTargetTypeSelect.TooltipText = "Draw target: only draw cards of this type.";
		foreach (CardGeneratedCardType type in Enum.GetValues<CardGeneratedCardType>())
		{
			drawTargetTypeSelect.AddItem(CardEditorExtraEffects.GeneratedCardTypeLabel(type));
		}
		int drawTargetTypeIndex = effect != null ? (int)effect.CardSelectionType : (int)CardGeneratedCardType.Any;
		if (drawTargetTypeIndex < 0 || drawTargetTypeIndex >= Enum.GetValues<CardGeneratedCardType>().Length)
		{
			drawTargetTypeIndex = (int)CardGeneratedCardType.Any;
		}
		drawTargetTypeSelect.Select(drawTargetTypeIndex);

		OptionButton drawTargetFilterSelect = new OptionButton
		{
			CustomMinimumSize = new Vector2(120, _fieldMinSize.Y),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		StyleInput(drawTargetFilterSelect);
		ConstrainOptionButtonPopup(drawTargetFilterSelect);
		drawTargetFilterSelect.TooltipText = "Draw target: only draw cards matching this effect filter.";
		foreach (CardExtraEffectCountCardFilter filter in Enum.GetValues<CardExtraEffectCountCardFilter>())
		{
			drawTargetFilterSelect.AddItem(CardEditorExtraEffects.CountCardFilterLabel(filter));
		}
		int drawTargetFilterIndex = effect != null ? (int)effect.CardSelectionFilter : (int)CardExtraEffectCountCardFilter.Any;
		if (drawTargetFilterIndex < 0 || drawTargetFilterIndex >= Enum.GetValues<CardExtraEffectCountCardFilter>().Length)
		{
			drawTargetFilterIndex = (int)CardExtraEffectCountCardFilter.Any;
		}
		drawTargetFilterSelect.Select(drawTargetFilterIndex);

		drawTargetPoolSelect.ItemSelected += _ => QueuePreviewUpdate();
		drawTargetTypeSelect.ItemSelected += _ => QueuePreviewUpdate();
		drawTargetFilterSelect.ItemSelected += _ => QueuePreviewUpdate();

		drawTargetFilterRow.AddChild(drawTargetLabel);
		drawTargetFilterRow.AddChild(drawTargetPoolSelect);
		drawTargetFilterRow.AddChild(drawTargetTypeSelect);
		drawTargetFilterRow.AddChild(drawTargetFilterSelect);
		drawTargetFilterRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

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
		triggerMaxFiresField.TooltipText = "Maximum number of times this effect can trigger before it expires. For Power effects, 0 = unlimited. For non-Power Turn Boundary effects, 0 = once.";
		StyleInput(triggerMaxFiresField);
		triggerMaxFiresField.TextChanged += _ => QueuePreviewUpdate();

		Label maxTurnsLabel = new Label { Text = "Power Turns" };
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
		triggerMaxTurnsField.TooltipText = "Power mode only: maximum number of your turns this passive trigger lasts. 0 = unlimited. If both Uses and Power Turns are set, whichever limit is reached first expires it.";
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
		powerConditionRow.AddChild(powerCountEnemyStatusRow);
		powerConditionRow.AddChild(powerFilterRow);
		powerConditionRow.AddChild(drawTargetFilterRow);

		PanelContainer effectPanel = CreateEditorPanel(bgAlpha: 0.72f);
		MarginContainer effectPanelMargin = new MarginContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		effectPanelMargin.AddThemeConstantOverride("margin_left", 12);
		effectPanelMargin.AddThemeConstantOverride("margin_top", 10);
		effectPanelMargin.AddThemeConstantOverride("margin_right", 12);
		effectPanelMargin.AddThemeConstantOverride("margin_bottom", 10);
		effectPanel.AddChild(effectPanelMargin);
		effectPanelMargin.AddChild(wrapper);

		ExtraEffectRow effectRow = new ExtraEffectRow
		{
			IsUpgradeDeltaRow = isUpgradeDeltaRow,
			Container = effectPanel,
			MoveUpButton = moveUp,
			MoveDownButton = moveDown,
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
			PowerCountEnemyStatusRow = powerCountEnemyStatusRow,
			PowerCountEnemyStatusSelect = powerCountEnemyStatusSelect,
			PowerFilterRow = powerFilterRow,
			TriggerCardPoolSelect = triggerPoolSelect,
			TriggerCardTypeSelect = triggerTypeSelect,
			TriggerCardFilterSelect = triggerFilterSelect,
			DrawTargetFilterRow = drawTargetFilterRow,
			DrawTargetPoolSelect = drawTargetPoolSelect,
			DrawTargetTypeSelect = drawTargetTypeSelect,
			DrawTargetFilterSelect = drawTargetFilterSelect,
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
			PowerRow = applyPowerRow,
			PowerSelect = applyPowerSelect,
			MoveCardsRow = moveCardsRow,
			MoveCardsRowTop = moveCardsRowTop,
			MoveCardsRowBottom = moveCardsRowBottom,
			MoveFromPileSelect = moveFromPileSelect,
			MoveSelectionModeSelect = moveSelectionModeSelect,
			MoveToPileSelect = moveToPileSelect,
			MoveToPositionSelect = moveToPositionSelect,
			AdditionalMoveToRow = additionalMoveToRow,
			AdditionalMoveToHandTickbox = additionalMoveToHandTickbox,
			AdditionalMoveToDrawTickbox = additionalMoveToDrawTickbox,
			AdditionalMoveToDiscardTickbox = additionalMoveToDiscardTickbox,
			AdditionalMoveToExhaustTickbox = additionalMoveToExhaustTickbox,
			CostFilterRow = costFilterRow,
			CostFilterTickbox = costFilterTickbox,
			CostFilterField = costFilterField,
			CardMatchRow = cardMatchRow,
			CardMatchModeSelect = cardMatchModeSelect,
			MatchCardIdField = matchCardIdField,
			MatchCardIdPickButton = pickMatchCardIdButton,
			MatchTagKindSelect = matchTagKindSelect,
			MatchVanillaTagSelect = matchVanillaTagSelect,
			MatchCustomTagSelect = matchCustomTagSelect,
			MatchCustomTagOptions = customTagOptions,
			DrawCostRow = drawCostRow,
			DrawCostTickbox = drawCostTickbox,
			DrawCostField = drawCostField,
			KeywordGroupRow = keywordGroupRow,
			KeywordGroupField = keywordGroupField,
			IgnoreVariantRow = ignoreVariantRow,
			IgnoreVariantSelect = ignoreVariantSelect,
			CardActionVariantRow = cardActionVariantRow,
			CardActionVariantSelect = cardActionVariantSelect,
			CardGenerationVariantRow = cardGenerationVariantRow,
			CardGenerationVariantSelect = cardGenerationVariantSelect,
			TurnBoundaryRow = turnBoundaryRow,
			TurnBoundaryEdgeSelect = turnBoundaryEdgeSelect,
			TurnBoundarySideSelect = turnBoundarySideSelect,
			TurnBoundaryLocationSelect = turnBoundaryLocationSelect,
			TransformModeRow = transformModeRow,
			TransformModeSelect = transformModeSelect,
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
			ConditionalBonusRow = conditionalBonusRow,
			ConditionalBonusConditionSelect = conditionalBonusConditionSelect,
			ConditionalBonusEnemyStatusSelect = conditionalBonusEnemyStatusSelect,
			ConditionalBonusEnemyIntentSelect = conditionalBonusEnemyIntentSelect,
			ConditionalBonusAmountField = conditionalBonusAmountField,
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
			CountSourceToggleRow = countSourceToggleRow,
			CountExcludeSourceTickbox = countExcludeSourceTickbox,
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
		moveUp.Pressed += () => MoveExtraEffectRow(effectRow, direction: -1);
		moveDown.Pressed += () => MoveExtraEffectRow(effectRow, direction: 1);

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
		transformModeSelect.ItemSelected += _ =>
		{
			UpdateExtraEffectCustomRows(effectRow);
			QueuePreviewUpdate();
		};
		conditionalBonusConditionSelect.ItemSelected += _ =>
		{
			UpdateExtraEffectCustomRows(effectRow);
			QueuePreviewUpdate();
		};
		conditionalBonusEnemyStatusSelect.ItemSelected += _ => QueuePreviewUpdate();
		conditionalBonusEnemyIntentSelect.ItemSelected += _ => QueuePreviewUpdate();
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
		powerCountEventSelect.ItemSelected += _ =>
		{
			ApplySuggestedCountSelectorDefaults(effectRow);
			UpdateExtraEffectCustomRows(effectRow);
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
		powerCountEnemyStatusSelect.ItemSelected += _ => QueuePreviewUpdate();
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
		cardActionVariantSelect.ItemSelected += _ =>
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
				_extraEffectsContainer.RemoveChild(effectPanel);
			}
			_extraEffectRows.Remove(effectRow);
			effectPanel.QueueFreeSafely();
			UpdateExtraEffectReorderButtons();
			RefreshEffectSummaryList();
			QueuePreviewUpdate();
		};

		rowTop.AddChild(kindSelect);
		rowTop.AddChild(amountXTickbox);
		rowTop.AddChild(spinButtons);
		rowTop.AddChild(amountField);
		rowTop.AddChild(disableOnUpgradeTickbox);
		rowTop.AddChild(moveUp);
		rowTop.AddChild(moveDown);
		rowTop.AddChild(remove);

		Label coreHeaderLabel = new Label { Text = CardEditorLoc.T("section.coreEffect", "Core Effect") };
		StylePanelTitleLabel(coreHeaderLabel);
		Label timingHeaderLabel = new Label { Text = CardEditorLoc.T("section.timingTargeting", "Timing & Targeting") };
		StylePanelTitleLabel(timingHeaderLabel);
		Label advancedHeaderLabel = new Label { Text = CardEditorLoc.T("section.advancedProperties", "Advanced Properties") };
		StylePanelTitleLabel(advancedHeaderLabel);

		wrapper.AddChild(coreHeaderLabel);
		wrapper.AddChild(rowTop);
		wrapper.AddChild(repeatRow);
		wrapper.AddChild(timingHeaderLabel);
		wrapper.AddChild(configRow);
		wrapper.AddChild(conditionalBonusRow);
		wrapper.AddChild(turnBoundaryRow);
		wrapper.AddChild(transformModeRow);
		wrapper.AddChild(specificCardRow);
		wrapper.AddChild(moveCardsRow);
		wrapper.AddChild(advancedHeaderLabel);
		wrapper.AddChild(cardMatchRow);
		wrapper.AddChild(drawCostRow);
		wrapper.AddChild(keywordGroupRow);
		wrapper.AddChild(ignoreVariantRow);
		wrapper.AddChild(cardActionVariantRow);
		wrapper.AddChild(cardGenerationVariantRow);
		wrapper.AddChild(orbRow);
		wrapper.AddChild(ostyActionRow);
		wrapper.AddChild(grantedKeywordRow);
		wrapper.AddChild(multiplyStatRow);
		wrapper.AddChild(enchantmentRow);
		wrapper.AddChild(applyPowerRow);
		wrapper.AddChild(scalingToggleRow);
		wrapper.AddChild(grantRow);
		wrapper.AddChild(powerConditionRow);
		wrapper.AddChild(scalingRow);
		wrapper.AddChild(generatedCardRow);
		wrapper.AddChild(upgradeRow);
		wrapper.AddChild(createdCostRow);
		wrapper.AddChild(cardCostsLessRow);
		wrapper.AddChild(timingRow);

		_extraEffectsContainer.AddChild(effectPanel);
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
		UpdateExtraEffectReorderButtons();
		RefreshEffectSummaryList();
	}

	private void MoveExtraEffectRow(ExtraEffectRow row, int direction)
	{
		if (_isUpgradeEditor || row.IsUpgradeDeltaRow)
		{
			return;
		}

		int currentIndex = _extraEffectRows.IndexOf(row);
		if (currentIndex < 0)
		{
			return;
		}

		int newIndex = currentIndex + direction;
		if (newIndex < 0 || newIndex >= _extraEffectRows.Count)
		{
			return;
		}

		(_extraEffectRows[currentIndex], _extraEffectRows[newIndex]) = (_extraEffectRows[newIndex], _extraEffectRows[currentIndex]);

		if (_extraEffectsContainer != null
			&& GodotObject.IsInstanceValid(_extraEffectsContainer)
			&& row.Container != null
			&& GodotObject.IsInstanceValid(row.Container))
		{
			_extraEffectsContainer.MoveChild(row.Container, newIndex);
		}

		UpdateExtraEffectReorderButtons();
		RefreshEffectSummaryList();
		QueuePreviewUpdate();
	}

	private void RefreshEffectSummaryList()
	{
		if (_effectSummaryContainer == null || !GodotObject.IsInstanceValid(_effectSummaryContainer))
		{
			return;
		}

		foreach (Node child in _effectSummaryContainer.GetChildren().Cast<Node>().ToList())
		{
			_effectSummaryContainer.RemoveChild(child);
			child.QueueFreeSafely();
		}

		if (_extraEffectRows.Count == 0)
		{
			Label empty = new Label
			{
				Text = CardEditorLoc.T("effectList.empty", "No extra effects yet."),
				AutowrapMode = TextServer.AutowrapMode.WordSmart
			};
			StyleHintLabel(empty);
			_effectSummaryContainer.AddChild(empty);
			return;
		}

		for (int i = 0; i < _extraEffectRows.Count; i++)
		{
			ExtraEffectRow row = _extraEffectRows[i];
			PanelContainer itemPanel = CreateEditorPanel(bgAlpha: 0.62f);
			MarginContainer margin = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			margin.AddThemeConstantOverride("margin_left", 10);
			margin.AddThemeConstantOverride("margin_top", 8);
			margin.AddThemeConstantOverride("margin_right", 10);
			margin.AddThemeConstantOverride("margin_bottom", 8);
			itemPanel.AddChild(margin);

			VBoxContainer itemBody = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			itemBody.AddThemeConstantOverride("separation", 2);
			margin.AddChild(itemBody);

			string kindText = GetSelectedItemText(row.KindSelect, CardEditorLoc.T("effectList.unknownEffect", "Effect"));
			string amountText = GetEffectSummaryAmountText(row);
			string disabledPrefix = row.IsUpgradeDeltaRow && row.DisableOnUpgradeTickbox.IsTicked
				? CardEditorLoc.T("effectList.disabledPrefix", "Disabled: ")
				: string.Empty;
			Label title = new Label
			{
				Text = $"{i + 1}. {disabledPrefix}{kindText}{amountText}",
				ClipText = true
			};
			StyleBodyLabel(title);
			itemBody.AddChild(title);

			string triggerText = GetSelectedItemText(row.TriggerSelect, CardEditorLoc.T("effectList.noTrigger", "No trigger"));
			string targetText = GetSelectedItemText(row.TargetSelect, string.Empty);
			Label details = new Label
			{
				Text = string.IsNullOrWhiteSpace(targetText) ? triggerText : $"{triggerText} \u2022 {targetText}",
				ClipText = true
			};
			StyleHintLabel(details);
			itemBody.AddChild(details);

			_effectSummaryContainer.AddChild(itemPanel);
		}
	}

	private static string GetSelectedItemText(OptionButton? select, string fallback)
	{
		if (select == null || !GodotObject.IsInstanceValid(select) || select.Selected < 0 || select.Selected >= select.ItemCount)
		{
			return fallback;
		}

		string text = select.GetItemText(select.Selected);
		return string.IsNullOrWhiteSpace(text) ? fallback : text;
	}

	private static string GetEffectSummaryAmountText(ExtraEffectRow row)
	{
		if (row.AmountField == null || !GodotObject.IsInstanceValid(row.AmountField) || !row.AmountField.Visible)
		{
			return string.Empty;
		}

		string amount = row.AmountField.Text?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(amount))
		{
			return string.Empty;
		}

		return row.AmountXTickbox != null && GodotObject.IsInstanceValid(row.AmountXTickbox) && row.AmountXTickbox.IsTicked
			? $" X+{amount}"
			: $" {amount}";
	}

	private void UpdateExtraEffectReorderButtons()
	{
		for (int i = 0; i < _extraEffectRows.Count; i++)
		{
			ExtraEffectRow row = _extraEffectRows[i];
			if (row == null)
			{
				continue;
			}

			bool allowReorder = !_isUpgradeEditor && !row.IsUpgradeDeltaRow;

			if (row.MoveUpButton != null && GodotObject.IsInstanceValid(row.MoveUpButton))
			{
				row.MoveUpButton.Visible = allowReorder;
				row.MoveUpButton.Disabled = !allowReorder || i == 0;
			}

			if (row.MoveDownButton != null && GodotObject.IsInstanceValid(row.MoveDownButton))
			{
				row.MoveDownButton.Visible = allowReorder;
				row.MoveDownButton.Disabled = !allowReorder || i == _extraEffectRows.Count - 1;
			}
		}
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
			or CardExtraEffectKind.AddExactCopyOfThisCardToDeck
			or CardExtraEffectKind.AddSpecificCardToHand
			or CardExtraEffectKind.FetchSpecificCardToHand;
	}

	private static bool IsHiddenUnifiedIgnoreKind(CardExtraEffectKind kind)
	{
		// Hide specialized ignore variants; expose them through IgnoreBlock (renamed "Damage Rule Modifier") via a variant selector.
		return kind is CardExtraEffectKind.IgnoreDamageModifiers
			or CardExtraEffectKind.IgnoreDamageCaps
			or CardExtraEffectKind.IgnoreDamageNegation
			or CardExtraEffectKind.IgnoreEnemyDamageReductions;
	}

	private static bool IsHiddenUnifiedCardActionKind(CardExtraEffectKind kind)
	{
		// Hide specialized pile-action variants; expose them through MoveCardsBetweenPiles (renamed "Card Action") via a variant selector.
		return kind is CardExtraEffectKind.PlayCardFromPile
			or CardExtraEffectKind.DiscardCards
			or CardExtraEffectKind.ExhaustCards
			or CardExtraEffectKind.TransformCards
			or CardExtraEffectKind.GrantKeywordToPile
			or CardExtraEffectKind.UpgradeCardsInPile
			or CardExtraEffectKind.UpgradeDeckCards
			or CardExtraEffectKind.CopyCardsFromPileToDeck
			or CardExtraEffectKind.CopyExactCardsFromPileToDeck
			or CardExtraEffectKind.RemoveCardsFromDeck;
	}

	private static bool IsAmountlessExtraEffectKind(CardExtraEffectKind kind)
	{
		return kind is CardExtraEffectKind.EndTurn
			or CardExtraEffectKind.RunEffectSourceCard;
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
			or CardExtraEffectKind.AddExactCopyOfThisCardToDeck
			or CardExtraEffectKind.AddSpecificCardToHand
			or CardExtraEffectKind.FetchSpecificCardToHand => CardExtraEffectKind.AddRandomCardToHand,
			CardExtraEffectKind.DrawCardsThatCostLess => CardExtraEffectKind.DrawCards,
			CardExtraEffectKind.IgnoreDamageModifiers
			or CardExtraEffectKind.IgnoreDamageCaps
			or CardExtraEffectKind.IgnoreDamageNegation
			or CardExtraEffectKind.IgnoreEnemyDamageReductions => CardExtraEffectKind.IgnoreBlock,
			CardExtraEffectKind.PlayCardFromPile
			or CardExtraEffectKind.DiscardCards
			or CardExtraEffectKind.ExhaustCards
			or CardExtraEffectKind.TransformCards
			or CardExtraEffectKind.GrantKeywordToPile
			or CardExtraEffectKind.UpgradeCardsInPile
			or CardExtraEffectKind.UpgradeDeckCards
			or CardExtraEffectKind.CopyCardsFromPileToDeck
			or CardExtraEffectKind.CopyExactCardsFromPileToDeck
			or CardExtraEffectKind.RemoveCardsFromDeck => CardExtraEffectKind.MoveCardsBetweenPiles,
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

	private static UnifiedCardActionVariant GetUnifiedCardActionVariant(CardExtraEffectKind kind)
	{
		return kind switch
		{
			CardExtraEffectKind.PlayCardFromPile => UnifiedCardActionVariant.PlayFromPile,
			CardExtraEffectKind.DiscardCards => UnifiedCardActionVariant.Discard,
			CardExtraEffectKind.ExhaustCards => UnifiedCardActionVariant.Exhaust,
			CardExtraEffectKind.TransformCards => UnifiedCardActionVariant.Transform,
			CardExtraEffectKind.GrantKeywordToPile => UnifiedCardActionVariant.GrantKeyword,
			CardExtraEffectKind.RunEffectSourceCard => UnifiedCardActionVariant.GrantExtraEffect,
			CardExtraEffectKind.UpgradeCardsInPile => UnifiedCardActionVariant.UpgradeInPile,
			CardExtraEffectKind.UpgradeDeckCards => UnifiedCardActionVariant.UpgradeDeck,
			CardExtraEffectKind.CopyCardsFromPileToDeck => UnifiedCardActionVariant.CopyPileToDeck,
			CardExtraEffectKind.CopyExactCardsFromPileToDeck => UnifiedCardActionVariant.ExactCopyPileToDeck,
			CardExtraEffectKind.RemoveCardsFromDeck => UnifiedCardActionVariant.RemoveFromDeck,
			_ => UnifiedCardActionVariant.MoveBetweenPiles
		};
	}

	private static CardExtraEffectKind GetUnifiedCardActionKind(UnifiedCardActionVariant variant)
	{
		return variant switch
		{
			UnifiedCardActionVariant.PlayFromPile => CardExtraEffectKind.PlayCardFromPile,
			UnifiedCardActionVariant.Discard => CardExtraEffectKind.DiscardCards,
			UnifiedCardActionVariant.Exhaust => CardExtraEffectKind.ExhaustCards,
			UnifiedCardActionVariant.Transform => CardExtraEffectKind.TransformCards,
			UnifiedCardActionVariant.GrantKeyword => CardExtraEffectKind.GrantKeywordToPile,
			UnifiedCardActionVariant.GrantExtraEffect => CardExtraEffectKind.RunEffectSourceCard,
			UnifiedCardActionVariant.UpgradeInPile => CardExtraEffectKind.UpgradeCardsInPile,
			UnifiedCardActionVariant.UpgradeDeck => CardExtraEffectKind.UpgradeDeckCards,
			UnifiedCardActionVariant.CopyPileToDeck => CardExtraEffectKind.CopyCardsFromPileToDeck,
			UnifiedCardActionVariant.ExactCopyPileToDeck => CardExtraEffectKind.CopyExactCardsFromPileToDeck,
			UnifiedCardActionVariant.RemoveFromDeck => CardExtraEffectKind.RemoveCardsFromDeck,
			_ => CardExtraEffectKind.MoveCardsBetweenPiles
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
				UnifiedCardGenerationVariant.ExactCopyOfThisCardToDeck => CardExtraEffectKind.AddExactCopyOfThisCardToDeck,
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

		if (kind == CardExtraEffectKind.MoveCardsBetweenPiles
			&& row?.CardActionVariantSelect != null
			&& GodotObject.IsInstanceValid(row.CardActionVariantSelect))
		{
			int actionSelected = row.CardActionVariantSelect.Selected;
			if (actionSelected < 0 || actionSelected >= Enum.GetValues<UnifiedCardActionVariant>().Length)
			{
				actionSelected = 0;
			}

			return GetUnifiedCardActionKind((UnifiedCardActionVariant)actionSelected);
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
		DisableOptionButton(row.DrawTargetPoolSelect);
		DisableOptionButton(row.DrawTargetTypeSelect);
		DisableOptionButton(row.DrawTargetFilterSelect);

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
		DisableTickbox(row.AdditionalMoveToHandTickbox);
		DisableTickbox(row.AdditionalMoveToDrawTickbox);
		DisableTickbox(row.AdditionalMoveToDiscardTickbox);
		DisableTickbox(row.AdditionalMoveToExhaustTickbox);
		// Allow enabling/disabling and tuning Draw->Cost Less in upgrade delta rows.
		// This is a numeric modifier like other upgrade-adjustable knobs, and locking it prevents setting deltas.
		DisableOptionButton(row.IgnoreVariantSelect);
		DisableOptionButton(row.CardActionVariantSelect);
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
		DisableOptionButton(row.PowerCountEnemyStatusSelect);
		DisableOptionButton(row.BlockLostCountingModeSelect);
		DisableOptionButton(row.CountPileSelect);
		DisableOptionButton(row.CountComparisonSelect);
		DisableOptionButton(row.CountPoolSelect);
		DisableOptionButton(row.CountTypeSelect);
		DisableOptionButton(row.CountFilterSelect);
		DisableTickbox(row.CountExcludeSourceTickbox);
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
		bool isExactCopyThisCardToDeck = kind == CardExtraEffectKind.AddExactCopyOfThisCardToDeck;
		bool isCopyPileToDeck = kind == CardExtraEffectKind.CopyCardsFromPileToDeck;
		bool isExactCopyPileToDeck = kind == CardExtraEffectKind.CopyExactCardsFromPileToDeck;
		bool isRemoveCardsFromDeck = kind == CardExtraEffectKind.RemoveCardsFromDeck;
		bool isPlayFromPile = kind == CardExtraEffectKind.PlayCardFromPile;
		bool isAutoPlaySelfFromPile = kind == CardExtraEffectKind.AutoPlaySelfFromPile;
		bool isAutoDrawSelfFromPile = kind == CardExtraEffectKind.AutoDrawSelfFromPile;
		bool isConditionalAutoFromPile = kind is CardExtraEffectKind.ConditionalAutoPlayFromPile or CardExtraEffectKind.ConditionalAutoDrawFromPile;
		bool isDrawCardsThatCostLess = kind == CardExtraEffectKind.DrawCardsThatCostLess;
		bool isTransformCards = kind == CardExtraEffectKind.TransformCards;
		bool isGrantExtraEffect = baseKind == CardExtraEffectKind.MoveCardsBetweenPiles && kind == CardExtraEffectKind.RunEffectSourceCard;
		bool transformIntoSpecific = isTransformCards && GetSelectedTransformMode(row) == CardExtraEffectTransformMode.SpecificCard;
		bool isSpecificCard = kind is CardExtraEffectKind.AddSpecificCardToHand
			or CardExtraEffectKind.FetchSpecificCardToHand
			or CardExtraEffectKind.RunEffectSourceCard
			|| transformIntoSpecific;
		bool isSpecificCardMove = kind is CardExtraEffectKind.AddSpecificCardToHand
			or CardExtraEffectKind.FetchSpecificCardToHand;
		bool isOrbAction = kind == CardExtraEffectKind.OrbAction;
		bool isOstyAction = kind == CardExtraEffectKind.OstyAction;
		bool isMultiplyStatStatus = kind == CardExtraEffectKind.MultiplyStatStatus;
		bool isEnchantCard = kind == CardExtraEffectKind.EnchantCard;
		bool isApplyPower = kind == CardExtraEffectKind.ApplyPower;
		bool isDiscardCards = kind == CardExtraEffectKind.DiscardCards;
		bool isExhaustCards = kind == CardExtraEffectKind.ExhaustCards;
		bool isDrawCards = baseKind == CardExtraEffectKind.DrawCards;
		bool isIgnoreEffects = baseKind == CardExtraEffectKind.IgnoreBlock;
		bool isCardAction = baseKind == CardExtraEffectKind.MoveCardsBetweenPiles;
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
			&& !isExactCopyThisCardToDeck
			&& !isCopyPileToDeck
			&& !isExactCopyPileToDeck
			&& !isRemoveCardsFromDeck
			&& !isDrawCardsThatCostLess
			&& !isDiscardCards
			&& !isExhaustCards
			&& !isAmountlessEffect
			&& !isGrantKeywordToPile
			&& !isUpgradeDeckCards
			&& !isGrantExtraEffect;
		row.GrantTickbox.Visible = canGrantToCard;
		if (!canGrantToCard)
		{
			row.GrantRow.Visible = false;
		}

		if (row.GrantTickbox.Visible && kind == CardExtraEffectKind.GrantReplay && !row.GrantTickbox.IsTicked)
		{
			row.GrantTickbox.SetTickedSilent(true);
		}

		bool grantToCard = isGrantExtraEffect || (row.GrantTickbox.Visible && row.GrantTickbox.IsTicked);
		row.GrantRow.SetMeta("card_editor_force_grant_row", isGrantExtraEffect);

		bool showRepeat = kind != CardExtraEffectKind.RunEffectSourceCard && CardEditorExtraEffects.SupportsRepeat(kind) && !grantToCard;
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
		bool showSelectionCardFilters = UsesCardSelectionFilterControls(kind);
		bool usesDedicatedDrawTargetFilters = kind is CardExtraEffectKind.DrawCards or CardExtraEffectKind.DrawCardsThatCostLess;
		bool showLegacySelectionCardFilters = showSelectionCardFilters && !usesDedicatedDrawTargetFilters;
		CardExtraEffectCountEvent selectedPowerCountEvent = isCountEventTrigger ? GetSelectedPowerTriggerCountEvent(row) : CardExtraEffectCountEvent.BlockLost;
		bool showPowerTriggerCardFiltersForCountEvent = asPower
			&& isCountEventTrigger
			&& CardEditorExtraEffects.CountEventUsesCardFilters(selectedPowerCountEvent);
		bool showPowerTriggerEnemyStatusForCountEvent = asPower
			&& isCountEventTrigger
			&& CardEditorExtraEffects.CountEventUsesEnemyStatus(selectedPowerCountEvent);
		bool showTriggerCardFilters = (asPower && !isTimedTrigger && !isOrbTrigger && !isCountEventTrigger)
			|| showAffectedCardFilters
			|| showLegacySelectionCardFilters
			|| showPowerTriggerCardFiltersForCountEvent;
		row.PowerConditionRow.Visible = asPower || isTimedTrigger || showAffectedCardFilters || showSelectionCardFilters;
		row.PowerTimingRow.Visible = asPower || isTimedTrigger;
		row.PowerCountEventRow.Visible = asPower && isCountEventTrigger;
		row.PowerCountEnemyStatusRow.Visible = showPowerTriggerEnemyStatusForCountEvent;
		row.PowerFilterRow.Visible = showTriggerCardFilters;
		row.DrawTargetFilterRow.Visible = usesDedicatedDrawTargetFilters;

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
		else if (showLegacySelectionCardFilters)
		{
			row.TriggerCardPoolSelect.TooltipText = "Selected cards: only affect cards from this pool.";
			row.TriggerCardTypeSelect.TooltipText = "Selected cards: only affect cards of this type.";
			row.TriggerCardFilterSelect.TooltipText = "Selected cards: only affect cards matching this effect filter.";
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

		if (kind == CardExtraEffectKind.RunEffectSourceCard)
		{
			int onPlayIndex = row.TriggerSelect.GetItemIndex((int)CardExtraEffectTrigger.OnPlay);
			if (onPlayIndex >= 0)
			{
				row.TriggerSelect.Select(onPlayIndex);
			}
			row.TriggerSelect.Disabled = true;
			row.TriggerSelect.SelfModulate = StsColors.gray;
		}

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
		if (row.TransformModeRow != null && GodotObject.IsInstanceValid(row.TransformModeRow))
		{
			row.TransformModeRow.Visible = isTransformCards;
		}
		row.SpecificCardRow.Visible = isSpecificCard;
		if (row.ConditionalBonusRow != null && GodotObject.IsInstanceValid(row.ConditionalBonusRow))
		{
			bool showRow = !isAmountlessEffect && row.AmountField != null && GodotObject.IsInstanceValid(row.AmountField) && row.AmountField.Visible;
			row.ConditionalBonusRow.Visible = showRow;

			CardExtraEffectConditionalBonusCondition cond = GetSelectedConditionalBonusCondition(row);
			bool needsStatus = cond == CardExtraEffectConditionalBonusCondition.TargetHasStatus;
			bool needsIntent = cond == CardExtraEffectConditionalBonusCondition.TargetHasIntent;

			if (row.ConditionalBonusEnemyStatusSelect != null && GodotObject.IsInstanceValid(row.ConditionalBonusEnemyStatusSelect))
			{
				row.ConditionalBonusEnemyStatusSelect.Visible = showRow && needsStatus;
			}
			if (row.ConditionalBonusEnemyIntentSelect != null && GodotObject.IsInstanceValid(row.ConditionalBonusEnemyIntentSelect))
			{
				row.ConditionalBonusEnemyIntentSelect.Visible = showRow && needsIntent;
			}

			bool showBonusAmount = showRow && cond != CardExtraEffectConditionalBonusCondition.None;
			Control? bonusSpin = null;
			if (row.ConditionalBonusAmountField != null && GodotObject.IsInstanceValid(row.ConditionalBonusAmountField))
			{
				Node parent = row.ConditionalBonusAmountField.GetParent();
				if (parent != null)
				{
					int fieldIndex = row.ConditionalBonusAmountField.GetIndex();
					if (fieldIndex > 0)
					{
						bonusSpin = parent.GetChildOrNull<Control>(fieldIndex - 1);
					}
				}

				row.ConditionalBonusAmountField.Visible = showBonusAmount;
			}
			if (bonusSpin != null && GodotObject.IsInstanceValid(bonusSpin))
			{
				bonusSpin.Visible = showBonusAmount;
			}
		}
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
		if (row.PowerRow != null && GodotObject.IsInstanceValid(row.PowerRow))
		{
			row.PowerRow.Visible = isApplyPower;
		}
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
		bool allowTiming = kind != CardExtraEffectKind.RunEffectSourceCard && !isCreatedCardModifier && !isAnyCostChange && !isTimedTrigger;
		bool showTiming = allowTiming && (isOnPlayTrigger || asPower);
		row.TimingRow.Visible = showTiming;

		row.MoveCardsRow.Visible = isMoveCards || isUpgradeCardsInPile || isCopyThisCard || isGeneratedCard || isPlayFromPile || isAutoPlaySelfFromPile || isAutoDrawSelfFromPile || isConditionalAutoFromPile || isSpecificCardMove || isDiscardCards || isExhaustCards || isTransformCards || isGrantKeywordToPile || isUpgradeDeckCards || isDrawCards || isCopyPileToDeck || isExactCopyPileToDeck || isRemoveCardsFromDeck;
		if (row.MoveCardsRow.Visible)
		{
			// MoveCardsBetweenPiles: show both rows.
			// UpgradeCardsInPile: only needs the "from" selectors + selection mode.
			// AddCopyOfThisCard: only needs the "to" selectors.
			// PlayCardFromPile: only needs the "from" selectors + selection mode.
			// AutoPlaySelfFromPile / AutoDrawSelfFromPile: only needs the "from" pile (which pile this card must be in).
			// GrantKeywordToPile: needs from pile + selection mode.
			// UpgradeDeckCards: needs only selection mode (deck is always the pile).
			row.MoveCardsRowTop.Visible = isMoveCards || isUpgradeCardsInPile || isPlayFromPile || isAutoPlaySelfFromPile || isAutoDrawSelfFromPile || isConditionalAutoFromPile || isDiscardCards || isExhaustCards || isTransformCards || isGrantKeywordToPile || isUpgradeDeckCards || isDrawCards || isCopyPileToDeck || isExactCopyPileToDeck || isRemoveCardsFromDeck || kind == CardExtraEffectKind.FetchSpecificCardToHand;
			row.MoveCardsRowBottom.Visible = isMoveCards || isCopyThisCard || isGeneratedCard || isSpecificCardMove;

			row.MoveFromPileSelect.Visible = isMoveCards || isUpgradeCardsInPile || isPlayFromPile || isAutoPlaySelfFromPile || isAutoDrawSelfFromPile || isConditionalAutoFromPile || isDiscardCards || isExhaustCards || isTransformCards || isGrantKeywordToPile || isDrawCards || isCopyPileToDeck || isExactCopyPileToDeck || isRemoveCardsFromDeck;
			row.MoveSelectionModeSelect.Visible = isMoveCards || isUpgradeCardsInPile || isPlayFromPile || isDiscardCards || isExhaustCards || isTransformCards || isGrantKeywordToPile || isUpgradeDeckCards || isCopyPileToDeck || isExactCopyPileToDeck || isRemoveCardsFromDeck || (kind == CardExtraEffectKind.FetchSpecificCardToHand);
			row.MoveToPileSelect.Visible = isMoveCards || isCopyThisCard || isGeneratedCard || isSpecificCardMove;
			row.MoveToPositionSelect.Visible = isMoveCards || isCopyThisCard || isGeneratedCard || isSpecificCardMove;
			if (row.AdditionalMoveToRow != null && GodotObject.IsInstanceValid(row.AdditionalMoveToRow))
			{
				row.AdditionalMoveToRow.Visible = isGeneratedCard || isCopyThisCard || kind == CardExtraEffectKind.AddSpecificCardToHand;
			}
		}

		if (row.MoveCardsRow.Visible)
		{
			const string metaSpecificToHand = "card_editor_default_specific_to_hand";
			if ((isSpecificCard || isGeneratedCard)
				&& row.MoveToPileSelect != null
				&& GodotObject.IsInstanceValid(row.MoveToPileSelect)
				&& !row.MoveToPileSelect.HasMeta(metaSpecificToHand))
			{
				row.MoveToPileSelect.Select((int)CardExtraEffectCardPile.Hand);
				if (row.MoveToPositionSelect != null && GodotObject.IsInstanceValid(row.MoveToPositionSelect))
				{
					row.MoveToPositionSelect.Select((int)CardExtraEffectCardPilePosition.Bottom);
				}
				row.MoveToPileSelect.SetMeta(metaSpecificToHand, true);
			}

			// Keep the user's selection stable. Previously this forced defaults (e.g. Play-from-pile ? Hand),
			// which overwrote loaded values on open and caused the UI to disagree with the saved effect.
			if (row.MoveFromPileSelect != null && GodotObject.IsInstanceValid(row.MoveFromPileSelect))
			{
				row.MoveFromPileSelect.Disabled = false;
				row.MoveFromPileSelect.SelfModulate = Colors.White;
			}
			if (row.AdditionalMoveToRow != null && GodotObject.IsInstanceValid(row.AdditionalMoveToRow) && row.AdditionalMoveToRow.Visible)
			{
				UpdateAdditionalMoveToTargets(row.MoveToPileSelect, row.AdditionalMoveToHandTickbox, row.AdditionalMoveToDrawTickbox, row.AdditionalMoveToDiscardTickbox, row.AdditionalMoveToExhaustTickbox);
			}
		}

		if (row.CostFilterRow != null && GodotObject.IsInstanceValid(row.CostFilterRow))
		{
			row.CostFilterRow.Visible = isMoveCards || isPlayFromPile || isDiscardCards || isExhaustCards || isTransformCards;
		}

		if (row.CardMatchRow != null && GodotObject.IsInstanceValid(row.CardMatchRow))
		{
			// Set later (after scaling visibility is computed) so we can also show it for scaling count events.
			row.CardMatchRow.Visible = false;
		}

		if (row.DrawCostRow != null && GodotObject.IsInstanceValid(row.DrawCostRow))
		{
			row.DrawCostRow.Visible = isDrawCards;
			bool enableDrawCostAmount = isDrawCards
				&& row.DrawCostTickbox != null
				&& GodotObject.IsInstanceValid(row.DrawCostTickbox)
				&& row.DrawCostTickbox.IsTicked
				&& row.DrawCostField != null
				&& GodotObject.IsInstanceValid(row.DrawCostField);
			if (row.DrawCostField != null && GodotObject.IsInstanceValid(row.DrawCostField))
			{
				row.DrawCostField.Editable = enableDrawCostAmount;
				row.DrawCostField.SelfModulate = enableDrawCostAmount ? Colors.White : StsColors.gray;
				SetSpinEnabled(row.DrawCostField, enableDrawCostAmount);
			}
		}

		if (row.IgnoreVariantRow != null && GodotObject.IsInstanceValid(row.IgnoreVariantRow))
		{
			row.IgnoreVariantRow.Visible = isIgnoreEffects;
		}

		if (row.CardActionVariantRow != null && GodotObject.IsInstanceValid(row.CardActionVariantRow))
		{
			row.CardActionVariantRow.Visible = isCardAction;
		}

		if (row.CardGenerationVariantRow != null && GodotObject.IsInstanceValid(row.CardGenerationVariantRow))
		{
			row.CardGenerationVariantRow.Visible = isCardGeneration;
		}

		if (row.TurnBoundaryRow != null && GodotObject.IsInstanceValid(row.TurnBoundaryRow))
		{
			row.TurnBoundaryRow.Visible = trigger == CardExtraEffectTrigger.TurnBoundary;
		}

		if (!showTiming)
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

		if (row.CardMatchRow != null && GodotObject.IsInstanceValid(row.CardMatchRow))
		{
			bool showForPileOps = isMoveCards
				|| isUpgradeCardsInPile
				|| isPlayFromPile
				|| isDiscardCards
				|| isExhaustCards
				|| isTransformCards
				|| isGrantKeywordToPile
				|| isUpgradeDeckCards
				|| isDrawCards;

			bool showForScaling = row.ScalingRow.Visible && CardEditorExtraEffects.CountEventUsesCardFilters(GetSelectedCountEvent(row));
			row.CardMatchRow.Visible = showForPileOps || showForScaling;
		}
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

		bool forcedVisible = row.GrantRow.HasMeta("card_editor_force_grant_row") && (bool)row.GrantRow.GetMeta("card_editor_force_grant_row");
		bool enabled = forcedVisible || (row.GrantTickbox.Visible && row.GrantTickbox.IsTicked);
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

		CardExtraEffectCardPile toPile = GetSelectedCardPile(row.MoveToPileSelect, CardExtraEffectCardPile.DrawPile);

		bool enablePosition = toPile == CardExtraEffectCardPile.DrawPile
			|| (row.AdditionalMoveToDrawTickbox != null && GodotObject.IsInstanceValid(row.AdditionalMoveToDrawTickbox) && row.AdditionalMoveToDrawTickbox.IsTicked);
		row.MoveToPositionSelect.Disabled = !enablePosition;
		row.MoveToPositionSelect.SelfModulate = enablePosition ? Colors.White : StsColors.gray;
	}

	private static void UpdateAdditionalMoveToTargets(OptionButton moveToPileSelect, params KeywordTickbox[] tickboxes)
	{
		if (moveToPileSelect == null || !GodotObject.IsInstanceValid(moveToPileSelect) || tickboxes == null || tickboxes.Length < 4)
		{
			return;
		}

		CardExtraEffectCardPile primaryPile = (CardExtraEffectCardPile)moveToPileSelect.GetSelectedId();
		(bool matchesPrimary, KeywordTickbox tickbox)[] map =
		{
			(primaryPile == CardExtraEffectCardPile.Hand, tickboxes[0]),
			(primaryPile == CardExtraEffectCardPile.DrawPile, tickboxes[1]),
			(primaryPile == CardExtraEffectCardPile.DiscardPile, tickboxes[2]),
			(primaryPile == CardExtraEffectCardPile.ExhaustPile, tickboxes[3])
		};

		foreach ((bool matchesPrimary, KeywordTickbox tickbox) in map)
		{
			if (tickbox == null || !GodotObject.IsInstanceValid(tickbox))
			{
				continue;
			}

			if (matchesPrimary && tickbox.IsTicked)
			{
				tickbox.SetTickedSilent(false);
			}

			tickbox.MouseFilter = matchesPrimary ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;
			tickbox.SelfModulate = matchesPrimary ? StsColors.gray : Colors.White;
		}
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
			and not CardExtraEffectKind.MultiplyStatStatus
			and not CardExtraEffectKind.RunEffectSourceCard;

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

		CardExtraEffectCountEvent powerCountEvent = GetSelectedPowerTriggerCountEvent(row);
		if (CardEditorExtraEffects.CountEventUsesEnemyStatus(powerCountEvent)
			&& CardEditorExtraEffects.TryGetSuggestedCountEnemyStatus(kind, out CardExtraEffectEnemyStatus suggestedPowerStatus))
		{
			int selectedPowerStatus = row.PowerCountEnemyStatusSelect.Selected;
			if (selectedPowerStatus < 0
				|| selectedPowerStatus >= Enum.GetValues<CardExtraEffectEnemyStatus>().Length
				|| selectedPowerStatus == (int)CardExtraEffectEnemyStatus.AnyPowerStatus)
			{
				row.PowerCountEnemyStatusSelect.Select((int)suggestedPowerStatus);
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
		bool usesExcludeSource = ev == CardExtraEffectCountEvent.InPile;
		bool usesOrbType = CardEditorExtraEffects.CountEventUsesOrbType(ev);
		bool usesOrbSelection = CardEditorExtraEffects.CountEventUsesOrbSelection(ev);
		bool usesEnemyStatus = CardEditorExtraEffects.CountEventUsesEnemyStatus(ev);
		bool usesEnemyIntent = CardEditorExtraEffects.CountEventUsesEnemyIntent(ev);

		row.CountWindowSelect.Visible = enabled && usesWindow;
		row.CountPileSelect.Visible = enabled && usesCardPile;
		row.CountCardFilterRow.Visible = enabled && usesCardFilters;
		row.CountSourceToggleRow.Visible = enabled && usesExcludeSource;
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

	private static CardExtraEffectEnemyStatus GetSelectedPowerTriggerEnemyStatus(ExtraEffectRow row)
	{
		if (row.PowerCountEnemyStatusSelect == null || !GodotObject.IsInstanceValid(row.PowerCountEnemyStatusSelect))
		{
			return CardExtraEffectEnemyStatus.AnyPowerStatus;
		}

		int selected = row.PowerCountEnemyStatusSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardExtraEffectEnemyStatus>().Length)
		{
			return CardExtraEffectEnemyStatus.AnyPowerStatus;
		}

		return (CardExtraEffectEnemyStatus)selected;
	}

	private static bool UsesCardSelectionFilterControls(CardExtraEffectKind kind)
	{
		return kind is CardExtraEffectKind.MoveCardsBetweenPiles
			or CardExtraEffectKind.UpgradeCardsInPile
			or CardExtraEffectKind.PlayCardFromPile
			or CardExtraEffectKind.DiscardCards
			or CardExtraEffectKind.ExhaustCards
			or CardExtraEffectKind.TransformCards
			or CardExtraEffectKind.GrantKeywordToPile
			or CardExtraEffectKind.UpgradeDeckCards
			or CardExtraEffectKind.CopyCardsFromPileToDeck
			or CardExtraEffectKind.CopyExactCardsFromPileToDeck
			or CardExtraEffectKind.RemoveCardsFromDeck
			or CardExtraEffectKind.DrawCards
			or CardExtraEffectKind.DrawCardsThatCostLess;
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

	private static CardExtraEffectAdditionalMoveToPiles GetSelectedAdditionalMoveToPiles(ExtraEffectRow row)
	{
		if (row == null)
		{
			return CardExtraEffectAdditionalMoveToPiles.None;
		}

		CardExtraEffectAdditionalMoveToPiles result = CardExtraEffectAdditionalMoveToPiles.None;
		if (row.AdditionalMoveToHandTickbox != null && GodotObject.IsInstanceValid(row.AdditionalMoveToHandTickbox) && row.AdditionalMoveToHandTickbox.IsTicked)
		{
			result |= CardExtraEffectAdditionalMoveToPiles.Hand;
		}
		if (row.AdditionalMoveToDrawTickbox != null && GodotObject.IsInstanceValid(row.AdditionalMoveToDrawTickbox) && row.AdditionalMoveToDrawTickbox.IsTicked)
		{
			result |= CardExtraEffectAdditionalMoveToPiles.DrawPile;
		}
		if (row.AdditionalMoveToDiscardTickbox != null && GodotObject.IsInstanceValid(row.AdditionalMoveToDiscardTickbox) && row.AdditionalMoveToDiscardTickbox.IsTicked)
		{
			result |= CardExtraEffectAdditionalMoveToPiles.DiscardPile;
		}
		if (row.AdditionalMoveToExhaustTickbox != null && GodotObject.IsInstanceValid(row.AdditionalMoveToExhaustTickbox) && row.AdditionalMoveToExhaustTickbox.IsTicked)
		{
			result |= CardExtraEffectAdditionalMoveToPiles.ExhaustPile;
		}

		return result;
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

	private static CardGeneratedCardPool GetSelectedDrawTargetPool(ExtraEffectRow row)
	{
		int selected = row.DrawTargetPoolSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardGeneratedCardPool>().Length)
		{
			return CardGeneratedCardPool.All;
		}
		return (CardGeneratedCardPool)selected;
	}

	private static CardGeneratedCardType GetSelectedDrawTargetType(ExtraEffectRow row)
	{
		int selected = row.DrawTargetTypeSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardGeneratedCardType>().Length)
		{
			return CardGeneratedCardType.Any;
		}
		return (CardGeneratedCardType)selected;
	}

	private static CardExtraEffectCountCardFilter GetSelectedDrawTargetFilter(ExtraEffectRow row)
	{
		int selected = row.DrawTargetFilterSelect.Selected;
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

	private static CardExtraEffectTransformMode GetSelectedTransformMode(ExtraEffectRow row)
	{
		if (row.TransformModeSelect == null || !GodotObject.IsInstanceValid(row.TransformModeSelect))
		{
			return CardExtraEffectTransformMode.Random;
		}
		int selected = row.TransformModeSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardExtraEffectTransformMode>().Length)
		{
			return CardExtraEffectTransformMode.Random;
		}
		return (CardExtraEffectTransformMode)selected;
	}

	private static CardExtraEffectConditionalBonusCondition GetSelectedConditionalBonusCondition(ExtraEffectRow row)
	{
		if (row.ConditionalBonusConditionSelect == null || !GodotObject.IsInstanceValid(row.ConditionalBonusConditionSelect))
		{
			return CardExtraEffectConditionalBonusCondition.None;
		}
		int selected = row.ConditionalBonusConditionSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardExtraEffectConditionalBonusCondition>().Length)
		{
			return CardExtraEffectConditionalBonusCondition.None;
		}
		return (CardExtraEffectConditionalBonusCondition)selected;
	}

	private static CardExtraEffectEnemyStatus GetSelectedConditionalBonusEnemyStatus(ExtraEffectRow row)
	{
		if (row.ConditionalBonusEnemyStatusSelect == null || !GodotObject.IsInstanceValid(row.ConditionalBonusEnemyStatusSelect))
		{
			return CardExtraEffectEnemyStatus.Weak;
		}
		int selected = row.ConditionalBonusEnemyStatusSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardExtraEffectEnemyStatus>().Length)
		{
			return CardExtraEffectEnemyStatus.Weak;
		}
		return (CardExtraEffectEnemyStatus)selected;
	}

	private static CardExtraEffectEnemyIntent GetSelectedConditionalBonusEnemyIntent(ExtraEffectRow row)
	{
		if (row.ConditionalBonusEnemyIntentSelect == null || !GodotObject.IsInstanceValid(row.ConditionalBonusEnemyIntentSelect))
		{
			return CardExtraEffectEnemyIntent.Attack;
		}
		int selected = row.ConditionalBonusEnemyIntentSelect.Selected;
		if (selected < 0 || selected >= Enum.GetValues<CardExtraEffectEnemyIntent>().Length)
		{
			return CardExtraEffectEnemyIntent.Attack;
		}
		return (CardExtraEffectEnemyIntent)selected;
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

	private static CardExtraEffectCardMatchMode GetSelectedCardMatchMode(ExtraEffectRow row)
	{
		if (row.CardMatchModeSelect == null || !GodotObject.IsInstanceValid(row.CardMatchModeSelect))
		{
			return CardExtraEffectCardMatchMode.Any;
		}
		int selected = row.CardMatchModeSelect.Selected;
		if (selected < 0 || selected >= row.CardMatchModeSelect.ItemCount)
		{
			return CardExtraEffectCardMatchMode.Any;
		}
		int id = row.CardMatchModeSelect.GetItemId(selected);
		return Enum.IsDefined(typeof(CardExtraEffectCardMatchMode), id)
			? (CardExtraEffectCardMatchMode)id
			: CardExtraEffectCardMatchMode.Any;
	}

	private static CardExtraEffectCardMatchTagKind GetSelectedCardMatchTagKind(ExtraEffectRow row)
	{
		if (row.MatchTagKindSelect == null || !GodotObject.IsInstanceValid(row.MatchTagKindSelect))
		{
			return CardExtraEffectCardMatchTagKind.Vanilla;
		}
		int selected = row.MatchTagKindSelect.Selected;
		if (selected < 0 || selected >= row.MatchTagKindSelect.ItemCount)
		{
			return CardExtraEffectCardMatchTagKind.Vanilla;
		}
		int id = row.MatchTagKindSelect.GetItemId(selected);
		return Enum.IsDefined(typeof(CardExtraEffectCardMatchTagKind), id)
			? (CardExtraEffectCardMatchTagKind)id
			: CardExtraEffectCardMatchTagKind.Vanilla;
	}

	private static CardTag GetSelectedMatchVanillaTag(ExtraEffectRow row)
	{
		if (row.MatchVanillaTagSelect == null || !GodotObject.IsInstanceValid(row.MatchVanillaTagSelect))
		{
			return CardTag.None;
		}
		int selected = row.MatchVanillaTagSelect.Selected;
		if (selected < 0 || selected >= row.MatchVanillaTagSelect.ItemCount)
		{
			return CardTag.None;
		}
		int id = row.MatchVanillaTagSelect.GetItemId(selected);
		return Enum.IsDefined(typeof(CardTag), id) ? (CardTag)id : CardTag.None;
	}

	private static string? GetSelectedMatchCustomTag(ExtraEffectRow row)
	{
		if (row.MatchCustomTagSelect == null || !GodotObject.IsInstanceValid(row.MatchCustomTagSelect))
		{
			return null;
		}
		int selected = row.MatchCustomTagSelect.Selected;
		if (selected < 0 || selected >= row.MatchCustomTagSelect.ItemCount)
		{
			return null;
		}
		int id = row.MatchCustomTagSelect.GetItemId(selected);
		if (id <= 0)
		{
			return null;
		}
		int idx = id - 1;
		return (row.MatchCustomTagOptions != null && idx >= 0 && idx < row.MatchCustomTagOptions.Count)
			? row.MatchCustomTagOptions[idx]
			: null;
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

		_enchantmentSelect.AddItem(CardEditorLoc.T("value.noOverride", "No Override"));
		_enchantmentIds.Add(null);
		_enchantmentSelect.AddItem(CardEditorLoc.T("value.none", "None"));
		_enchantmentIds.Add(ModelId.none);
		foreach (EnchantmentModel enchantment in ModelDb.DebugEnchantments.OrderBy(e => e.Title.GetFormattedText()))
		{
			_enchantmentSelect.AddItem(enchantment.Title.GetFormattedText());
			_enchantmentIds.Add(enchantment.Id);
		}

		ModelId? selectedId = null;
		if (!_isUpgradeEditor
			&& CardEditorOverrides.TryGetEffectiveOverride(_cardId, out CardOverride existingOverride)
			&& existingOverride.EnchantmentId != null)
		{
			selectedId = existingOverride.EnchantmentId;
		}
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

	private void EnsurePowerIdsLoaded()
	{
		// If the first load happened too early (before ModelDb is fully initialized), we may have cached only "None".
		// Treat that as "not loaded" so the list can recover later.
		if (_powerIds.Count > 1)
		{
			return;
		}

		_powerIds.Clear();
		_powerIds.Add(ModelId.none);
		try
		{
			// Avoid ModelDb.AllPowers: it can throw if any PowerModel subtype exists but isn't registered in ModelDb.
			HashSet<ModelId> ids = new HashSet<ModelId>();
			foreach (Type t in ModelDb.AllAbstractModelSubtypes)
			{
				if (t == null || !t.IsSubclassOf(typeof(PowerModel)))
				{
					continue;
				}

				ModelId id;
				try
				{
					id = ModelDb.GetId(t);
				}
				catch
				{
					continue;
				}

				if (id == ModelId.none)
				{
					continue;
				}

				PowerModel? power = null;
				try
				{
					power = ModelDb.GetByIdOrNull<PowerModel>(id);
				}
				catch
				{
					power = null;
				}

				if (power != null)
				{
					ids.Add(id);
				}
			}

			foreach (ModelId id in ids.OrderBy(id => id.Entry, StringComparer.OrdinalIgnoreCase))
			{
				_powerIds.Add(id);
			}
		}
		catch
		{
		}
	}

	private void PopulateExtraEffectPowerSelect(OptionButton select, string? selectedIdText)
	{
		if (select == null || !GodotObject.IsInstanceValid(select))
		{
			return;
		}

		EnsurePowerIdsLoaded();

		select.Clear();
		foreach (ModelId? powerId in _powerIds)
		{
			if (powerId == null || powerId == ModelId.none)
			{
				select.AddItem(CardEditorLoc.T("value.none", "None"));
				continue;
			}

			string label = powerId.ToString();
			try
			{
				PowerModel? power = ModelDb.GetByIdOrNull<PowerModel>(powerId);
				string? title = power?.Title?.GetFormattedText();
				if (!string.IsNullOrWhiteSpace(title))
				{
					label = title;
				}
			}
			catch
			{
			}
			select.AddItem(label);
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

		int index = _powerIds.FindIndex(id => id == desiredId);
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

		_afflictionSelect.AddItem(CardEditorLoc.T("value.noOverride", "No Override"));
		_afflictionIds.Add(null);
		_afflictionSelect.AddItem(CardEditorLoc.T("value.none", "None"));
		_afflictionIds.Add(ModelId.none);
		foreach (AfflictionModel affliction in ModelDb.DebugAfflictions.OrderBy(a => a.Title.GetFormattedText()))
		{
			_afflictionSelect.AddItem(affliction.Title.GetFormattedText());
			_afflictionIds.Add(affliction.Id);
		}

		ModelId? selectedId = null;
		if (!_isUpgradeEditor
			&& CardEditorOverrides.TryGetEffectiveOverride(_cardId, out CardOverride existingOverride)
			&& existingOverride.AfflictionId != null)
		{
			selectedId = existingOverride.AfflictionId;
		}
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
		RefreshEffectSummaryList();
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
				EnergyCostX = storedBase.EnergyCostX,
				EnergyCost = storedBase.EnergyCost,
				StarCostX = storedBase.StarCostX,
				StarCost = storedBase.StarCost,
				ReplayCount = storedBase.ReplayCount,
				HandDiscardCount = storedBase.HandDiscardCount,
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
			_cardPreviewNode.UpdateVisuals(PileType.None, GetEditorPreviewMode());
			if (_cardNameLabel != null && GodotObject.IsInstanceValid(_cardNameLabel))
			{
				_cardNameLabel.Text = upgradedPreview.Title;
			}
			return;
		}

		CardModel preview = CardEditorOverrides.BuildPreview(canonical);
		CardOverride draft = BuildOverrideFromUi();
		CardEditorUiState.SetDraftOverride(_cardId, draft);
		if (_isCreatedCard)
		{
			CardEditorCreatedCardEffectSourceSupport.EnsureEffectSourceDynamicVars(preview, isUpgradePreview: false);
		}
		CardEditorOverrides.ApplyOverrideToCard(preview, draft);
		_previewCard = preview;
		_cardPreviewNode.Model = preview;
		_cardPreviewNode.UpdateVisuals(PileType.None, GetEditorPreviewMode());
		if (_cardNameLabel != null && GodotObject.IsInstanceValid(_cardNameLabel))
		{
			_cardNameLabel.Text = preview.Title;
		}

		MaybeRebuildCreatedEffectValueRowsForEffectSources();
		MaybeRebuildEffectSourceDynamicVarRows();
	}

	private void MaybeRebuildCreatedEffectValueRowsForEffectSources()
	{
		if (!_isCreatedCard || _isUpgradeEditor || _createdEffectValueContainer == null)
		{
			return;
		}

		string key = BuildCreatedEffectSourceNumbersKeyFromUi();
		if (string.Equals(_createdEffectSourceNumbersKey, key, StringComparison.Ordinal))
		{
			return;
		}

		_createdEffectSourceNumbersKey = key;
		RebuildCreatedEffectValueRows();
	}

	private void MaybeRebuildEffectSourceDynamicVarRows()
	{
		if (_isCreatedCard || _effectSourceDynamicVarContainer == null)
		{
			return;
		}

		string key = BuildEffectSourceNumbersKeyFromUi();
		if (string.Equals(_effectSourceDynamicVarKey, key, StringComparison.Ordinal))
		{
			return;
		}

		_effectSourceDynamicVarKey = key;
		RebuildEffectSourceDynamicVarRowsFromUi();
	}

	private void RebuildEffectSourceDynamicVarRowsFromUi()
	{
		if (_effectSourceDynamicVarContainer == null || !GodotObject.IsInstanceValid(_effectSourceDynamicVarContainer))
		{
			return;
		}

		List<Node> existingChildren = _effectSourceDynamicVarContainer.GetChildren().ToList();
		foreach (Node child in existingChildren)
		{
			_effectSourceDynamicVarContainer.RemoveChild(child);
		}

		List<ModelId> sourceIds = GetInlineEffectSourceIdsFromUi();
		if (sourceIds.Count == 0)
		{
			foreach (string key in _effectSourceDynamicVarRowControls.Keys.ToList())
			{
				_dynamicFields.Remove(key);
			}
			_effectSourceSpecialNumberFields.Clear();
			_effectSourceSpecialNumberDefaults.Clear();
			if (_effectSourceDynamicVarLabel != null && GodotObject.IsInstanceValid(_effectSourceDynamicVarLabel))
			{
				_effectSourceDynamicVarLabel.Visible = false;
			}
			return;
		}

		CardOverride? existingOverride = null;
		if (CardEditorOverrides.TryGetEffectiveOverride(_cardId, out CardOverride existing))
		{
			existingOverride = existing;
		}

		UpgradeBaseline? baseline = _isUpgradeEditor ? GetUpgradeBaseline() : null;
		CardUpgradeOverride? storedUpgrade = _isUpgradeEditor ? CardEditorOverrides.Get(_cardId)?.Upgrade : null;

		HashSet<string> added = new HashSet<string>(StringComparer.Ordinal);
		HashSet<string> addedSpecial = new HashSet<string>(StringComparer.Ordinal);
		bool prevSuppress = _suppressPreviewUpdate;
		_suppressPreviewUpdate = true;
		foreach (ModelId sourceId in sourceIds)
		{
			CardModel? sourceCardCanonical = ModelDb.GetByIdOrNull<CardModel>(sourceId);
			if (sourceCardCanonical == null)
			{
				continue;
			}

			CardModel sourceCard;
			try
			{
				// Use a mutable clone so any overrides on the source card (e.g. Furnace Forge 10)
				// are reflected in the defaults we seed into this card's Effect Source Numbers.
				sourceCard = sourceCardCanonical.ToMutable();
				CardEditorOverrides.ApplyTo(sourceCard);
			}
			catch
			{
				continue;
			}

			if (!_isUpgradeEditor)
			{
				int specialCountBefore = _effectSourceSpecialNumberFields.Count;
				BuildEffectSourceSpecialNumberRows(sourceId, sourceCard, existingOverride);
				foreach (string key in _effectSourceSpecialNumberFields.Keys.Skip(specialCountBefore).ToArray())
				{
					addedSpecial.Add(key);
				}
				foreach (string key in _effectSourceSpecialNumberFields.Keys)
				{
					if (_effectSourceDynamicVarContainer.GetChildren().Contains(_effectSourceSpecialNumberRowControls.GetValueOrDefault(key)))
					{
						addedSpecial.Add(key);
					}
				}
			}

			foreach ((string varKey, var dynamicVar) in sourceCard.DynamicVars)
			{
				if (string.IsNullOrWhiteSpace(varKey))
				{
					continue;
				}

				if (added.Contains(varKey))
				{
					continue;
				}

				// If the base card already exposes this var, don't duplicate it.
				if (_dynamicFields.ContainsKey(varKey))
				{
					added.Add(varKey);
					continue;
				}

				if (!_effectSourceDynamicVarRowControls.TryGetValue(varKey, out Control? rowControl)
					|| rowControl == null
					|| !GodotObject.IsInstanceValid(rowControl)
					|| rowControl is not HBoxContainer row)
				{
					decimal baseValue = dynamicVar.BaseValue;
					if (existingOverride?.DynamicVarBaseValues != null
						&& existingOverride.DynamicVarBaseValues.TryGetValue(varKey, out decimal overridden))
					{
						baseValue = overridden;
					}

					row = new HBoxContainer();
					row.AddThemeConstantOverride("separation", 10);
					Label name = new Label { Text = varKey, CustomMinimumSize = new Vector2(_labelWidth, 0) };
					StyleBodyLabel(name);

					string fieldText;
					if (_isUpgradeEditor)
					{
						decimal vanillaDelta = 0m;
						try
						{
							CardModel deltaCard = sourceCardCanonical.ToMutable();
							if (deltaCard.DynamicVars.TryGetValue(varKey, out var deltaVar))
							{
								deltaVar.BaseValue = baseValue;
							}

							bool prevSuppressUpgrade = CardEditorOverrides.SuppressUpgradeOverrides;
							CardEditorOverrides.SuppressUpgradeOverrides = true;
							try
							{
								TryUpgradeForPreview(deltaCard);
							}
							finally
							{
								CardEditorOverrides.SuppressUpgradeOverrides = prevSuppressUpgrade;
							}

							if (deltaCard.DynamicVars.TryGetValue(varKey, out var upgradedVar))
							{
								vanillaDelta = upgradedVar.BaseValue - baseValue;
							}
						}
						catch
						{
						}

						if (baseline != null)
						{
							baseline.VanillaVarDeltas[varKey] = vanillaDelta;
						}

						decimal desiredDelta = vanillaDelta;
						if (storedUpgrade?.DynamicVarDeltas != null && storedUpgrade.DynamicVarDeltas.TryGetValue(varKey, out decimal overriddenDelta))
						{
							desiredDelta = overriddenDelta;
						}

						fieldText = desiredDelta.ToString(CultureInfo.InvariantCulture);
					}
					else
					{
						fieldText = baseValue.ToString(CultureInfo.InvariantCulture);
					}

					NMegaLineEdit field = new NMegaLineEdit
					{
						Text = fieldText,
						SizeFlagsHorizontal = Control.SizeFlags.Fill,
						CustomMinimumSize = _numericFieldMinSize,
						Alignment = HorizontalAlignment.Center
					};
					StyleInput(field);
					field.TextChanged += _ => QueuePreviewUpdate();

					_dynamicFields[varKey] = field;

					Control spinButtons = CreateSpinButtons(field, step: 1m, minValue: null, maxValue: null);
					row.AddChild(name);
					row.AddChild(spinButtons);
					row.AddChild(field);
					row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

					_effectSourceDynamicVarRowControls[varKey] = row;
				}
				else if (!_dynamicFields.ContainsKey(varKey) && FindFirstLineEdit(row) is LineEdit existingField)
				{
					_dynamicFields[varKey] = existingField;
				}

				_effectSourceDynamicVarContainer.AddChild(row);
				added.Add(varKey);
			}
		}
		_suppressPreviewUpdate = prevSuppress;

		foreach (string staleKey in _effectSourceDynamicVarRowControls.Keys.Except(added).ToList())
		{
			_dynamicFields.Remove(staleKey);
		}

		foreach (string staleKey in _effectSourceSpecialNumberFields.Keys.Except(addedSpecial).ToList())
		{
			_effectSourceSpecialNumberFields.Remove(staleKey);
			_effectSourceSpecialNumberDefaults.Remove(staleKey);
		}

		bool hasRows = _effectSourceDynamicVarContainer.GetChildCount() > 0;
		if (_effectSourceDynamicVarLabel != null && GodotObject.IsInstanceValid(_effectSourceDynamicVarLabel))
		{
			_effectSourceDynamicVarLabel.Visible = hasRows;
		}
	}

	private string BuildEffectSourceNumbersKeyFromUi()
	{
		if (_extraEffectRows == null || _extraEffectRows.Count == 0)
		{
			return string.Empty;
		}

		List<string> ids = new();
		foreach (ExtraEffectRow row in _extraEffectRows)
		{
			if (row == null)
			{
				continue;
			}

			int definitionIndex = GetSelectedExtraEffectDefinitionIndex(row);
			if (definitionIndex < 0 || definitionIndex >= CardEditorExtraEffects.Definitions.Count)
			{
				continue;
			}

			CardExtraEffectKind kind = GetResolvedExtraEffectKind(row, CardEditorExtraEffects.Definitions[definitionIndex].Kind);
			if (kind != CardExtraEffectKind.RunEffectSourceCard)
			{
				continue;
			}

			string? idStr = row.SpecificCardIdField?.Text?.Trim();
			if (string.IsNullOrWhiteSpace(idStr))
			{
				continue;
			}

			try
			{
				ModelId id = ModelId.Deserialize(idStr);
				if (id == ModelId.none || id == _cardId)
				{
					continue;
				}
				ids.Add(id.ToString());
			}
			catch
			{
			}
		}

		return ids.Count == 0 ? string.Empty : string.Join(";", ids);
	}

	private List<ModelId> GetInlineEffectSourceIdsFromUi()
	{
		List<ModelId> ids = new();
		if (_extraEffectRows == null || _extraEffectRows.Count == 0)
		{
			return ids;
		}

		foreach (ExtraEffectRow row in _extraEffectRows)
		{
			if (row == null)
			{
				continue;
			}

			int definitionIndex = GetSelectedExtraEffectDefinitionIndex(row);
			if (definitionIndex < 0 || definitionIndex >= CardEditorExtraEffects.Definitions.Count)
			{
				continue;
			}

			CardExtraEffectKind kind = GetResolvedExtraEffectKind(row, CardEditorExtraEffects.Definitions[definitionIndex].Kind);
			if (kind != CardExtraEffectKind.RunEffectSourceCard)
			{
				continue;
			}

			string? idStr = row.SpecificCardIdField?.Text?.Trim();
			if (string.IsNullOrWhiteSpace(idStr))
			{
				continue;
			}

			try
			{
				ModelId id = ModelId.Deserialize(idStr);
				if (id == ModelId.none || id == _cardId)
				{
					continue;
				}
				if (!ids.Contains(id))
				{
					ids.Add(id);
				}
			}
			catch
			{
			}
		}

		return ids;
	}

	private string BuildCreatedEffectSourceNumbersKeyFromUi()
	{
		if (_extraEffectRows == null || _extraEffectRows.Count == 0)
		{
			return string.Empty;
		}

		List<string> ids = new();
		foreach (ExtraEffectRow row in _extraEffectRows)
		{
			if (row == null)
			{
				continue;
			}

			int definitionIndex = GetSelectedExtraEffectDefinitionIndex(row);
			if (definitionIndex < 0 || definitionIndex >= CardEditorExtraEffects.Definitions.Count)
			{
				continue;
			}

			CardExtraEffectKind kind = GetResolvedExtraEffectKind(row, CardEditorExtraEffects.Definitions[definitionIndex].Kind);
			if (kind != CardExtraEffectKind.RunEffectSourceCard)
			{
				continue;
			}

			string? idStr = row.SpecificCardIdField?.Text?.Trim();
			if (string.IsNullOrWhiteSpace(idStr))
			{
				continue;
			}

			try
			{
				ModelId id = ModelId.Deserialize(idStr);
				if (id == ModelId.none || id == _cardId)
				{
					continue;
				}
				ids.Add(id.ToString());
			}
			catch
			{
				// Ignore invalid ids while typing.
			}
		}

		return ids.Count == 0 ? string.Empty : string.Join(";", ids);
	}

	private CardOverride BuildOverrideFromUi()
	{
		CardOverride overrideData = new CardOverride
		{
			Keywords = _keywordChecks.Where(kvp => kvp.Value.IsTicked).Select(kvp => kvp.Key).ToHashSet(),
			DynamicVarBaseValues = new Dictionary<string, decimal>()
		};

		if (!_isUpgradeEditor && _tagChecks.Count > 0)
		{
			HashSet<CardTag> desiredTags = _tagChecks
				.Where(kvp => kvp.Value.IsTicked)
				.Select(kvp => kvp.Key)
				.Where(t => t != CardTag.None)
				.ToHashSet();
			HashSet<CardTag> baselineTags = _baselineTags ?? ComputeBaselineTags();

			HashSet<CardTag> toRemove = new HashSet<CardTag>(baselineTags);
			toRemove.ExceptWith(desiredTags);
			HashSet<CardTag> toAdd = new HashSet<CardTag>(desiredTags);
			toAdd.ExceptWith(baselineTags);

			if (toRemove.Count > 0)
			{
				overrideData.TagsToRemove = toRemove;
			}
			if (toAdd.Count > 0)
			{
				overrideData.TagsToAdd = toAdd;
			}
			if (_customTags.Count > 0)
			{
				overrideData.CustomTags = new HashSet<string>(_customTags, StringComparer.OrdinalIgnoreCase);
			}
		}

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
		if (_handDiscardCountField != null && int.TryParse(_handDiscardCountField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int discardCount))
		{
			discardCount = Math.Clamp(discardCount, 0, 99);
			overrideData.HandDiscardCount = discardCount == 1 ? null : discardCount;
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

		if (_drawCostReductionField == null
			&& _effectSourceSpecialNumberFields.TryGetValue(EffectSourceDrawCostReductionKey, out LineEdit? effectSourceDrawReductionField)
			&& int.TryParse(effectSourceDrawReductionField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int effectSourceReduction))
		{
			effectSourceReduction = Math.Max(0, effectSourceReduction);
			if (!_effectSourceSpecialNumberDefaults.TryGetValue(EffectSourceDrawCostReductionKey, out int defaultReduction))
			{
				defaultReduction = 1;
			}
			overrideData.DrawCostReduction = effectSourceReduction == defaultReduction ? null : effectSourceReduction;
		}

		if (_handDiscardCountField == null
			&& _effectSourceSpecialNumberFields.TryGetValue(EffectSourceHandDiscardCountKey, out LineEdit? effectSourceDiscardField)
			&& int.TryParse(effectSourceDiscardField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int effectSourceDiscardCount))
		{
			effectSourceDiscardCount = Math.Clamp(effectSourceDiscardCount, 0, 99);
			if (!_effectSourceSpecialNumberDefaults.TryGetValue(EffectSourceHandDiscardCountKey, out int defaultDiscardCount))
			{
				defaultDiscardCount = 1;
			}
			overrideData.HandDiscardCount = effectSourceDiscardCount == defaultDiscardCount ? null : effectSourceDiscardCount;
		}

		if (_resonanceEnemyStrengthLossField == null
			&& _effectSourceSpecialNumberFields.TryGetValue(EffectSourceResonanceEnemyStrengthLossKey, out LineEdit? effectSourceResonanceField)
			&& int.TryParse(effectSourceResonanceField.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int effectSourceResonanceLoss))
		{
			effectSourceResonanceLoss = Math.Clamp(effectSourceResonanceLoss, 0, 99);
			if (!_effectSourceSpecialNumberDefaults.TryGetValue(EffectSourceResonanceEnemyStrengthLossKey, out int defaultResonanceLoss))
			{
				defaultResonanceLoss = 1;
			}
			if (effectSourceResonanceLoss != defaultResonanceLoss)
			{
				overrideData.DynamicVarBaseValues![CardEditorOverrideKeys.ResonanceEnemyStrengthLoss] = effectSourceResonanceLoss;
			}
		}

		foreach ((string key, LineEdit field) in _effectSourceSpecialNumberFields)
		{
			if (!TryParseEffectSourcePowerAmountKey(key, out ModelId powerId))
			{
				continue;
			}
			if (!int.TryParse(field.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
			{
				continue;
			}

			if (!_effectSourceSpecialNumberDefaults.TryGetValue(key, out int defaultValue))
			{
				defaultValue = 1;
			}

			if (value != defaultValue)
			{
				overrideData.PowerAmounts ??= new Dictionary<ModelId, decimal>();
				overrideData.PowerAmounts[powerId] = value;
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
				CardExtraEffectTiming timing = GetSelectedTiming(row);
				int turns = 0;
				if (timing != CardExtraEffectTiming.Immediate)
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
				// Power cards shouldn't fire non-OnPlay triggers while sitting in a pile (e.g. Innate in hand).
				// If the user picked a non-OnPlay trigger for a Power card, route it through the Power system by default.
				if (!asPower
					&& trigger != CardExtraEffectTrigger.OnPlay
					&& row.PowerTickbox.Visible
					&& CardEditorExtraEffects.SupportsAsPower(resolvedKind))
				{
					CardType selectedCardType = _previewCard.Type;
					if (_cardTypeSelect.Selected >= 0 && _cardTypeSelect.Selected < _cardTypes.Count)
					{
						selectedCardType = _cardTypes[_cardTypeSelect.Selected];
					}
					if (selectedCardType == CardType.Power)
					{
						asPower = true;
					}
				}

				int triggerMaxTurnsValue = Math.Max(0, ParseIntOrDefault(row.TriggerMaxTurnsField?.Text, 0));
				int triggerMaxFiresValue = Math.Max(0, ParseIntOrDefault(row.TriggerMaxFiresField?.Text, 0));

				bool grantToCard = (resolvedKind == CardExtraEffectKind.RunEffectSourceCard && def.Kind == CardExtraEffectKind.MoveCardsBetweenPiles)
					|| (row.GrantTickbox.Visible && row.GrantTickbox.IsTicked);
				CardExtraEffectCardSelectionMode selectionMode = CardExtraEffectCardSelectionMode.Choose;
				CardExtraEffectCardPile selectionPile = CardExtraEffectCardPile.Hand;
				CardExtraEffectCardGrantDuration grantDuration = CardExtraEffectCardGrantDuration.ThisTurn;
				int grantTurns = 2;
				CardExtraEffectCardPile moveToPile = CardExtraEffectCardPile.Hand;
				CardExtraEffectCardPilePosition moveToPosition = CardExtraEffectCardPilePosition.Bottom;
				bool useMoveDestinationForGeneratedCards = false;
				CardExtraEffectAdditionalMoveToPiles additionalMoveToPiles = CardExtraEffectAdditionalMoveToPiles.None;
				bool repeatIsX = row.RepeatRow != null && row.RepeatRow.Visible && row.RepeatXTickbox != null && row.RepeatXTickbox.IsTicked;
				int repeatCount = ParseIntOrDefault(row.RepeatCountField?.Text, 1);
				bool selectionCountIsX = grantToCard && row.GrantCountRow != null && row.GrantCountRow.Visible && row.GrantCountXTickbox != null && row.GrantCountXTickbox.IsTicked;
				int selectionCount = grantToCard && row.GrantCountRow != null && row.GrantCountRow.Visible
					? ParseIntOrDefault(row.GrantCountField?.Text, 1)
					: 1;
				bool usesSelectionFilters = UsesCardSelectionFilterControls(resolvedKind);
				bool usesDedicatedDrawTargetFilters = resolvedKind is CardExtraEffectKind.DrawCards or CardExtraEffectKind.DrawCardsThatCostLess;
				bool usesSharedSelectionFilters = usesSelectionFilters && !usesDedicatedDrawTargetFilters;
				CardGeneratedCardPool selectionPool = grantToCard
					? GetSelectedGrantCountPool(row)
					: (usesDedicatedDrawTargetFilters ? GetSelectedDrawTargetPool(row) : (usesSharedSelectionFilters ? GetSelectedTriggerCardPool(row) : CardGeneratedCardPool.All));
				CardGeneratedCardType selectionType = grantToCard
					? GetSelectedGrantCountType(row)
					: (usesDedicatedDrawTargetFilters ? GetSelectedDrawTargetType(row) : (usesSharedSelectionFilters ? GetSelectedTriggerCardType(row) : CardGeneratedCardType.Any));
				CardExtraEffectCountCardFilter selectionFilter = grantToCard
					? GetSelectedGrantCountFilter(row)
					: (usesDedicatedDrawTargetFilters ? GetSelectedDrawTargetFilter(row) : (usesSharedSelectionFilters ? GetSelectedTriggerCardFilter(row) : CardExtraEffectCountCardFilter.Any));
				int drawCostLessDelta = 0;
				if (resolvedKind == CardExtraEffectKind.DrawCardsThatCostLess)
				{
					drawCostLessDelta = ParseExtraEffectNumericField(row.DrawCostField, absoluteDefault: 1, row.IsUpgradeDeltaRow, minAbsolute: 1, maxAbsolute: 99);
				}

				if (resolvedKind is CardExtraEffectKind.MoveCardsBetweenPiles
					or CardExtraEffectKind.UpgradeCardsInPile
					or CardExtraEffectKind.AddCopyOfThisCard
					or CardExtraEffectKind.AddRandomCardToHand
					or CardExtraEffectKind.ChooseOneOfThreeCardsToHand
					or CardExtraEffectKind.PlayCardFromPile
					or CardExtraEffectKind.TransformCards
					or CardExtraEffectKind.AddSpecificCardToHand
					or CardExtraEffectKind.FetchSpecificCardToHand
					or CardExtraEffectKind.DiscardCards
					or CardExtraEffectKind.ExhaustCards
					or CardExtraEffectKind.GrantKeywordToPile
					or CardExtraEffectKind.CopyCardsFromPileToDeck
					or CardExtraEffectKind.CopyExactCardsFromPileToDeck
					or CardExtraEffectKind.RemoveCardsFromDeck
					or CardExtraEffectKind.AutoPlaySelfFromPile
					or CardExtraEffectKind.AutoDrawSelfFromPile
					or CardExtraEffectKind.ConditionalAutoPlayFromPile
					or CardExtraEffectKind.ConditionalAutoDrawFromPile)
				{
					if (resolvedKind is CardExtraEffectKind.MoveCardsBetweenPiles
						or CardExtraEffectKind.AddCopyOfThisCard
						or CardExtraEffectKind.AddRandomCardToHand
						or CardExtraEffectKind.ChooseOneOfThreeCardsToHand
						or CardExtraEffectKind.AddSpecificCardToHand
						or CardExtraEffectKind.FetchSpecificCardToHand)
					{
						CardExtraEffectCardPile moveToDefaultPile = resolvedKind is CardExtraEffectKind.AddRandomCardToHand or CardExtraEffectKind.ChooseOneOfThreeCardsToHand
							? CardExtraEffectCardPile.Hand
							: CardExtraEffectCardPile.DrawPile;
						CardExtraEffectCardPilePosition moveToDefaultPosition = resolvedKind is CardExtraEffectKind.AddRandomCardToHand or CardExtraEffectKind.ChooseOneOfThreeCardsToHand
							? CardExtraEffectCardPilePosition.Bottom
							: CardExtraEffectCardPilePosition.Top;
						moveToPile = GetSelectedCardPile(row.MoveToPileSelect, moveToDefaultPile);
						moveToPosition = GetSelectedCardPilePosition(row.MoveToPositionSelect, moveToDefaultPosition);
						useMoveDestinationForGeneratedCards = resolvedKind is CardExtraEffectKind.AddRandomCardToHand or CardExtraEffectKind.ChooseOneOfThreeCardsToHand;
						additionalMoveToPiles = GetSelectedAdditionalMoveToPiles(row);
					}

					if (resolvedKind == CardExtraEffectKind.FetchSpecificCardToHand)
					{
						selectionMode = GetSelectedCardSelectionMode(row.MoveSelectionModeSelect, CardExtraEffectCardSelectionMode.UpTo);
					}
					if (resolvedKind is CardExtraEffectKind.MoveCardsBetweenPiles
						or CardExtraEffectKind.UpgradeCardsInPile
						or CardExtraEffectKind.PlayCardFromPile
						or CardExtraEffectKind.DiscardCards
						or CardExtraEffectKind.ExhaustCards
						or CardExtraEffectKind.TransformCards
						or CardExtraEffectKind.GrantKeywordToPile
						or CardExtraEffectKind.CopyCardsFromPileToDeck
						or CardExtraEffectKind.CopyExactCardsFromPileToDeck
						or CardExtraEffectKind.RemoveCardsFromDeck
						or CardExtraEffectKind.AutoPlaySelfFromPile
						or CardExtraEffectKind.AutoDrawSelfFromPile
						or CardExtraEffectKind.ConditionalAutoPlayFromPile
						or CardExtraEffectKind.ConditionalAutoDrawFromPile)
					{
						selectionPile = GetSelectedCardPile(row.MoveFromPileSelect, CardExtraEffectCardPile.Hand);
						selectionMode = GetSelectedCardSelectionMode(row.MoveSelectionModeSelect, CardExtraEffectCardSelectionMode.Choose);
					}
					else if (resolvedKind is CardExtraEffectKind.DrawCards or CardExtraEffectKind.DrawCardsThatCostLess)
					{
						selectionPile = GetSelectedCardPile(row.MoveFromPileSelect, CardExtraEffectCardPile.DrawPile);
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

				CardExtraEffectTransformMode transformMode = resolvedKind == CardExtraEffectKind.TransformCards
					? GetSelectedTransformMode(row)
					: CardExtraEffectTransformMode.Random;

				bool allowConditionalBonus = row.ConditionalBonusRow != null && GodotObject.IsInstanceValid(row.ConditionalBonusRow) && row.ConditionalBonusRow.Visible;
				CardExtraEffectConditionalBonusCondition conditionalBonusCondition = allowConditionalBonus
					? GetSelectedConditionalBonusCondition(row)
					: CardExtraEffectConditionalBonusCondition.None;
				int conditionalBonusAmount = allowConditionalBonus && row.ConditionalBonusAmountField != null && GodotObject.IsInstanceValid(row.ConditionalBonusAmountField)
					? ParseIntOrDefault(row.ConditionalBonusAmountField.Text, 0)
					: 0;
				if (conditionalBonusCondition == CardExtraEffectConditionalBonusCondition.None)
				{
					conditionalBonusAmount = 0;
				}
				CardExtraEffectEnemyStatus conditionalBonusEnemyStatus = conditionalBonusCondition == CardExtraEffectConditionalBonusCondition.TargetHasStatus
					? GetSelectedConditionalBonusEnemyStatus(row)
					: CardExtraEffectEnemyStatus.Weak;
				CardExtraEffectEnemyIntent conditionalBonusEnemyIntent = conditionalBonusCondition == CardExtraEffectConditionalBonusCondition.TargetHasIntent
					? GetSelectedConditionalBonusEnemyIntent(row)
					: CardExtraEffectEnemyIntent.Attack;

				bool usesSpecificCardId = resolvedKind is CardExtraEffectKind.AddSpecificCardToHand
					or CardExtraEffectKind.FetchSpecificCardToHand
					or CardExtraEffectKind.RunEffectSourceCard
					|| (resolvedKind == CardExtraEffectKind.TransformCards && transformMode == CardExtraEffectTransformMode.SpecificCard);

				effects.Add(new CardExtraEffect
				{
					Kind = resolvedKind,
					Target = target,
					Amount = amount,
					AmountIsX = amountIsX,
					AmountXPlus = amountIsX ? amountXPlus : 0,
					Trigger = trigger,
					PowerTriggerCountEvent = GetSelectedPowerTriggerCountEvent(row),
					PowerTriggerEnemyStatus = GetSelectedPowerTriggerEnemyStatus(row),
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
					CountExcludeSourceCard = row.CountSourceToggleRow != null && row.CountSourceToggleRow.Visible
						&& row.CountExcludeSourceTickbox != null && row.CountExcludeSourceTickbox.IsTicked,
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
					PowerId = (resolvedKind == CardExtraEffectKind.ApplyPower
						&& row.PowerSelect != null
						&& GodotObject.IsInstanceValid(row.PowerSelect)
						&& row.PowerSelect.Selected >= 0
						&& row.PowerSelect.Selected < _powerIds.Count
						&& _powerIds[row.PowerSelect.Selected] != null
						&& _powerIds[row.PowerSelect.Selected] != ModelId.none)
						? _powerIds[row.PowerSelect.Selected]!.ToString()
						: null,
					CardSelectionCountIsX = selectionCountIsX,
					CardSelectionCount = resolvedKind == CardExtraEffectKind.DrawCardsThatCostLess ? drawCostLessDelta : selectionCount,
					CardSelectionPool = selectionPool,
					CardSelectionType = selectionType,
					CardSelectionFilter = selectionFilter,
					MoveToPile = moveToPile,
					MoveToPosition = moveToPosition,
					UseMoveDestinationForGeneratedCards = useMoveDestinationForGeneratedCards,
					AdditionalMoveToPiles = additionalMoveToPiles,
					OrbAction = orbAction,
					OrbScope = orbScope,
					OrbType = orbType,
					OrbSelection = orbSelection,
					OrbFollowUp = orbFollowUp,
					MultiplierStat = multiplierStat,
					DrawnFromPile = drawnFromPile,
					SpecificCardId = usesSpecificCardId
						? (string.IsNullOrWhiteSpace(row.SpecificCardIdField?.Text) ? null : row.SpecificCardIdField.Text.Trim())
						: null,
					TransformMode = transformMode,
					ConditionalBonusAmount = conditionalBonusAmount,
					ConditionalBonusCondition = conditionalBonusCondition,
					ConditionalBonusEnemyStatus = conditionalBonusEnemyStatus,
					ConditionalBonusEnemyIntent = conditionalBonusEnemyIntent,
					OstyAction = resolvedKind == CardExtraEffectKind.OstyAction ? GetSelectedOstyAction(row) : default,
					GrantedKeyword = resolvedKind == CardExtraEffectKind.GrantKeywordToPile ? GetSelectedGrantedKeyword(row) : default,
					CardMatchMode = GetSelectedCardMatchMode(row),
					MatchCardId = row.MatchCardIdField != null && GodotObject.IsInstanceValid(row.MatchCardIdField)
						? (string.IsNullOrWhiteSpace(row.MatchCardIdField.Text) ? null : row.MatchCardIdField.Text.Trim())
						: null,
					MatchTagKind = GetSelectedCardMatchTagKind(row),
					MatchVanillaTag = GetSelectedMatchVanillaTag(row),
					MatchCustomTag = GetSelectedMatchCustomTag(row),
					CustomKeywordName = row.KeywordGroupField != null && GodotObject.IsInstanceValid(row.KeywordGroupField)
						? (string.IsNullOrWhiteSpace(row.KeywordGroupField.Text) ? null : row.KeywordGroupField.Text.Trim())
						: null,
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

		if (!_isUpgradeEditor)
		{
			CardEditorCosmeticStylePreset stylePreset = GetSelectedCosmeticStylePreset();
			if (stylePreset != CardEditorCosmeticStylePreset.None)
			{
				overrideData.CosmeticStylePreset = stylePreset;
			}

			CardEditorCosmeticAnimationPreset animationPreset = GetSelectedCosmeticAnimationPreset();
			if (animationPreset != CardEditorCosmeticAnimationPreset.None)
			{
				overrideData.CosmeticAnimationPreset = animationPreset;
			}

			CardEditorCosmeticVfxPreset preset = GetSelectedCosmeticVfxPreset();
			if (preset != CardEditorCosmeticVfxPreset.None)
			{
				overrideData.CosmeticVfxPreset = preset;
				overrideData.CosmeticVfxAttach = GetSelectedCosmeticVfxAttach();
			}
		}

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

		CardModel baseCard = CardEditorOverrides.BuildPreview(canonical);
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

		CardModel vanillaUpgraded = CardEditorOverrides.BuildPreview(canonical);
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

	private HashSet<CardTag> ComputeBaselineTags()
	{
		bool prevSuppress = CardEditorOverrides.SuppressAllOverrides;
		try
		{
			CardEditorOverrides.SuppressAllOverrides = true;
			return _previewCard?.Tags?.Where(t => t != CardTag.None).ToHashSet() ?? new HashSet<CardTag>();
		}
		finally
		{
			CardEditorOverrides.SuppressAllOverrides = prevSuppress;
		}
	}

	private CardUpgradeOverride BuildUpgradeOverrideFromUiDeltas(UpgradeBaseline baseline)
	{
		CardUpgradeOverride upgrade = new CardUpgradeOverride();
		List<CardExtraEffect>? baseExtraEffects = CardEditorOverrides.Get(_cardId)?.ExtraEffects;
		int baseExtraEffectCount = baseExtraEffects?.Count ?? 0;

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
				CardExtraEffectTiming timing = GetSelectedTiming(row);
				int turns = 0;
				if (timing != CardExtraEffectTiming.Immediate)
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
				// Power cards shouldn't fire non-OnPlay triggers while sitting in a pile (e.g. Innate in hand).
				// If the user picked a non-OnPlay trigger for a Power card, route it through the Power system by default.
				if (!asPower
					&& trigger != CardExtraEffectTrigger.OnPlay
					&& row.PowerTickbox.Visible
					&& CardEditorExtraEffects.SupportsAsPower(resolvedKind))
				{
					CardType selectedCardType = _previewCard.Type;
					if (_cardTypeSelect.Selected >= 0 && _cardTypeSelect.Selected < _cardTypes.Count)
					{
						selectedCardType = _cardTypes[_cardTypeSelect.Selected];
					}
					if (selectedCardType == CardType.Power)
					{
						asPower = true;
					}
				}

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

				bool grantToCard = (resolvedKind == CardExtraEffectKind.RunEffectSourceCard && def.Kind == CardExtraEffectKind.MoveCardsBetweenPiles)
					|| (row.GrantTickbox.Visible && row.GrantTickbox.IsTicked);
				CardExtraEffectCardSelectionMode selectionMode = CardExtraEffectCardSelectionMode.Choose;
				CardExtraEffectCardPile selectionPile = CardExtraEffectCardPile.Hand;
				CardExtraEffectCardGrantDuration grantDuration = CardExtraEffectCardGrantDuration.ThisTurn;
				int grantTurns = isDeltaRow ? 0 : 2;
				CardExtraEffectCardPile moveToPile = CardExtraEffectCardPile.Hand;
				CardExtraEffectCardPilePosition moveToPosition = CardExtraEffectCardPilePosition.Bottom;
				bool useMoveDestinationForGeneratedCards = false;
				CardExtraEffectAdditionalMoveToPiles additionalMoveToPiles = CardExtraEffectAdditionalMoveToPiles.None;
				bool repeatIsX = row.RepeatRow != null && row.RepeatRow.Visible && row.RepeatXTickbox != null && row.RepeatXTickbox.IsTicked;
				int repeatCount = ParseExtraEffectNumericField(row.RepeatCountField, absoluteDefault: 1, isDeltaRow, minAbsolute: 1, maxAbsolute: 99);
				bool selectionCountIsX = grantToCard && row.GrantCountRow != null && row.GrantCountRow.Visible && row.GrantCountXTickbox != null && row.GrantCountXTickbox.IsTicked;
				int selectionCount = grantToCard && row.GrantCountRow != null && row.GrantCountRow.Visible
					? ParseExtraEffectNumericField(row.GrantCountField, absoluteDefault: 1, isDeltaRow, minAbsolute: 0, maxAbsolute: 99)
					: (isDeltaRow ? 0 : 1);
				bool usesSelectionFilters = UsesCardSelectionFilterControls(resolvedKind);
				bool usesDedicatedDrawTargetFilters = resolvedKind is CardExtraEffectKind.DrawCards or CardExtraEffectKind.DrawCardsThatCostLess;
				bool usesSharedSelectionFilters = usesSelectionFilters && !usesDedicatedDrawTargetFilters;
				CardGeneratedCardPool selectionPool = grantToCard
					? GetSelectedGrantCountPool(row)
					: (usesDedicatedDrawTargetFilters ? GetSelectedDrawTargetPool(row) : (usesSharedSelectionFilters ? GetSelectedTriggerCardPool(row) : CardGeneratedCardPool.All));
				CardGeneratedCardType selectionType = grantToCard
					? GetSelectedGrantCountType(row)
					: (usesDedicatedDrawTargetFilters ? GetSelectedDrawTargetType(row) : (usesSharedSelectionFilters ? GetSelectedTriggerCardType(row) : CardGeneratedCardType.Any));
				CardExtraEffectCountCardFilter selectionFilter = grantToCard
					? GetSelectedGrantCountFilter(row)
					: (usesDedicatedDrawTargetFilters ? GetSelectedDrawTargetFilter(row) : (usesSharedSelectionFilters ? GetSelectedTriggerCardFilter(row) : CardExtraEffectCountCardFilter.Any));
				int drawCostLessDelta = 0;
				if (resolvedKind == CardExtraEffectKind.DrawCardsThatCostLess)
				{
					drawCostLessDelta = ParseExtraEffectNumericField(row.DrawCostField, absoluteDefault: 1, isDeltaRow, minAbsolute: 1, maxAbsolute: 99);
				}

				if (resolvedKind is CardExtraEffectKind.MoveCardsBetweenPiles
					or CardExtraEffectKind.UpgradeCardsInPile
					or CardExtraEffectKind.AddCopyOfThisCard
					or CardExtraEffectKind.AddRandomCardToHand
					or CardExtraEffectKind.ChooseOneOfThreeCardsToHand
					or CardExtraEffectKind.AddSpecificCardToHand
					or CardExtraEffectKind.FetchSpecificCardToHand
					or CardExtraEffectKind.PlayCardFromPile
					or CardExtraEffectKind.TransformCards
					or CardExtraEffectKind.DiscardCards
					or CardExtraEffectKind.ExhaustCards
					or CardExtraEffectKind.GrantKeywordToPile
					or CardExtraEffectKind.CopyCardsFromPileToDeck
					or CardExtraEffectKind.CopyExactCardsFromPileToDeck
					or CardExtraEffectKind.RemoveCardsFromDeck
					or CardExtraEffectKind.AutoPlaySelfFromPile
					or CardExtraEffectKind.AutoDrawSelfFromPile
					or CardExtraEffectKind.ConditionalAutoPlayFromPile
					or CardExtraEffectKind.ConditionalAutoDrawFromPile)
				{
					if (resolvedKind is CardExtraEffectKind.MoveCardsBetweenPiles
						or CardExtraEffectKind.AddCopyOfThisCard
						or CardExtraEffectKind.AddRandomCardToHand
						or CardExtraEffectKind.ChooseOneOfThreeCardsToHand
						or CardExtraEffectKind.AddSpecificCardToHand
						or CardExtraEffectKind.FetchSpecificCardToHand)
					{
						CardExtraEffectCardPile moveToDefaultPile = resolvedKind is CardExtraEffectKind.AddRandomCardToHand or CardExtraEffectKind.ChooseOneOfThreeCardsToHand
							? CardExtraEffectCardPile.Hand
							: CardExtraEffectCardPile.DrawPile;
						CardExtraEffectCardPilePosition moveToDefaultPosition = resolvedKind is CardExtraEffectKind.AddRandomCardToHand or CardExtraEffectKind.ChooseOneOfThreeCardsToHand
							? CardExtraEffectCardPilePosition.Bottom
							: CardExtraEffectCardPilePosition.Top;
						moveToPile = GetSelectedCardPile(row.MoveToPileSelect, moveToDefaultPile);
						moveToPosition = GetSelectedCardPilePosition(row.MoveToPositionSelect, moveToDefaultPosition);
						useMoveDestinationForGeneratedCards = resolvedKind is CardExtraEffectKind.AddRandomCardToHand or CardExtraEffectKind.ChooseOneOfThreeCardsToHand;
						additionalMoveToPiles = GetSelectedAdditionalMoveToPiles(row);
					}

					if (resolvedKind == CardExtraEffectKind.FetchSpecificCardToHand)
					{
						selectionMode = GetSelectedCardSelectionMode(row.MoveSelectionModeSelect, CardExtraEffectCardSelectionMode.UpTo);
					}
					if (resolvedKind is CardExtraEffectKind.MoveCardsBetweenPiles
						or CardExtraEffectKind.UpgradeCardsInPile
						or CardExtraEffectKind.PlayCardFromPile
						or CardExtraEffectKind.TransformCards
						or CardExtraEffectKind.DiscardCards
						or CardExtraEffectKind.ExhaustCards
						or CardExtraEffectKind.GrantKeywordToPile
						or CardExtraEffectKind.CopyCardsFromPileToDeck
						or CardExtraEffectKind.CopyExactCardsFromPileToDeck
						or CardExtraEffectKind.RemoveCardsFromDeck
						or CardExtraEffectKind.AutoPlaySelfFromPile
						or CardExtraEffectKind.AutoDrawSelfFromPile
						or CardExtraEffectKind.ConditionalAutoPlayFromPile
						or CardExtraEffectKind.ConditionalAutoDrawFromPile)
					{
						selectionPile = GetSelectedCardPile(row.MoveFromPileSelect, CardExtraEffectCardPile.Hand);
						selectionMode = GetSelectedCardSelectionMode(row.MoveSelectionModeSelect, CardExtraEffectCardSelectionMode.Choose);
					}
					else if (resolvedKind is CardExtraEffectKind.DrawCards or CardExtraEffectKind.DrawCardsThatCostLess)
					{
						selectionPile = GetSelectedCardPile(row.MoveFromPileSelect, CardExtraEffectCardPile.DrawPile);
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

				CardExtraEffectTransformMode transformMode = resolvedKind == CardExtraEffectKind.TransformCards
					? GetSelectedTransformMode(row)
					: CardExtraEffectTransformMode.Random;

				bool allowConditionalBonus = row.ConditionalBonusRow != null && GodotObject.IsInstanceValid(row.ConditionalBonusRow) && row.ConditionalBonusRow.Visible;
				CardExtraEffectConditionalBonusCondition conditionalBonusCondition = allowConditionalBonus
					? GetSelectedConditionalBonusCondition(row)
					: CardExtraEffectConditionalBonusCondition.None;
				int conditionalBonusAmount = allowConditionalBonus && row.ConditionalBonusAmountField != null && GodotObject.IsInstanceValid(row.ConditionalBonusAmountField)
					? ParseIntOrDefault(row.ConditionalBonusAmountField.Text, 0)
					: 0;
				if (conditionalBonusCondition == CardExtraEffectConditionalBonusCondition.None)
				{
					conditionalBonusAmount = 0;
				}
				CardExtraEffectEnemyStatus conditionalBonusEnemyStatus = conditionalBonusCondition == CardExtraEffectConditionalBonusCondition.TargetHasStatus
					? GetSelectedConditionalBonusEnemyStatus(row)
					: CardExtraEffectEnemyStatus.Weak;
				CardExtraEffectEnemyIntent conditionalBonusEnemyIntent = conditionalBonusCondition == CardExtraEffectConditionalBonusCondition.TargetHasIntent
					? GetSelectedConditionalBonusEnemyIntent(row)
					: CardExtraEffectEnemyIntent.Attack;

				bool usesSpecificCardId = resolvedKind is CardExtraEffectKind.AddSpecificCardToHand
					or CardExtraEffectKind.FetchSpecificCardToHand
					or CardExtraEffectKind.RunEffectSourceCard
					|| (resolvedKind == CardExtraEffectKind.TransformCards && transformMode == CardExtraEffectTransformMode.SpecificCard);

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
					PowerTriggerEnemyStatus = GetSelectedPowerTriggerEnemyStatus(row),
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
					CountExcludeSourceCard = row.CountSourceToggleRow != null && row.CountSourceToggleRow.Visible
						&& row.CountExcludeSourceTickbox != null && row.CountExcludeSourceTickbox.IsTicked,
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
					PowerId = (resolvedKind == CardExtraEffectKind.ApplyPower
						&& row.PowerSelect != null
						&& GodotObject.IsInstanceValid(row.PowerSelect)
						&& row.PowerSelect.Selected >= 0
						&& row.PowerSelect.Selected < _powerIds.Count
						&& _powerIds[row.PowerSelect.Selected] != null
						&& _powerIds[row.PowerSelect.Selected] != ModelId.none)
						? _powerIds[row.PowerSelect.Selected]!.ToString()
						: null,
					CardSelectionCountIsX = selectionCountIsX,
					CardSelectionCount = resolvedKind == CardExtraEffectKind.DrawCardsThatCostLess ? drawCostLessDelta : selectionCount,
					CardSelectionPool = selectionPool,
					CardSelectionType = selectionType,
					CardSelectionFilter = selectionFilter,
					MoveToPile = moveToPile,
					MoveToPosition = moveToPosition,
					UseMoveDestinationForGeneratedCards = useMoveDestinationForGeneratedCards,
					AdditionalMoveToPiles = additionalMoveToPiles,
					OrbAction = orbAction,
					OrbScope = orbScope,
					OrbType = orbType,
					OrbSelection = orbSelection,
					OrbFollowUp = orbFollowUp,
					MultiplierStat = multiplierStat,
					DrawnFromPile = drawnFromPile,
					SpecificCardId = usesSpecificCardId
						? (string.IsNullOrWhiteSpace(row.SpecificCardIdField?.Text) ? null : row.SpecificCardIdField.Text.Trim())
						: null,
					TransformMode = transformMode,
					ConditionalBonusAmount = conditionalBonusAmount,
					ConditionalBonusCondition = conditionalBonusCondition,
					ConditionalBonusEnemyStatus = conditionalBonusEnemyStatus,
					ConditionalBonusEnemyIntent = conditionalBonusEnemyIntent,
					OstyAction = resolvedKind == CardExtraEffectKind.OstyAction ? GetSelectedOstyAction(row) : default,
					GrantedKeyword = resolvedKind == CardExtraEffectKind.GrantKeywordToPile ? GetSelectedGrantedKeyword(row) : default,
					CardMatchMode = GetSelectedCardMatchMode(row),
					MatchCardId = row.MatchCardIdField != null && GodotObject.IsInstanceValid(row.MatchCardIdField)
						? (string.IsNullOrWhiteSpace(row.MatchCardIdField.Text) ? null : row.MatchCardIdField.Text.Trim())
						: null,
					MatchTagKind = GetSelectedCardMatchTagKind(row),
					MatchVanillaTag = GetSelectedMatchVanillaTag(row),
					MatchCustomTag = GetSelectedMatchCustomTag(row),
					CustomKeywordName = row.KeywordGroupField != null && GodotObject.IsInstanceValid(row.KeywordGroupField)
						? (string.IsNullOrWhiteSpace(row.KeywordGroupField.Text) ? null : row.KeywordGroupField.Text.Trim())
						: null,
					CostFilterEnabled = row.CostFilterTickbox != null && GodotObject.IsInstanceValid(row.CostFilterTickbox) && row.CostFilterTickbox.IsTicked,
					CostFilterMax = row.CostFilterField != null && GodotObject.IsInstanceValid(row.CostFilterField) ? ParseIntOrDefault(row.CostFilterField.Text, 0) : 0
				};

				if (isDeltaRow)
				{
					CardExtraEffect? baseEffect = (baseExtraEffects != null && rowIndex >= 0 && rowIndex < baseExtraEffects.Count)
						? baseExtraEffects[rowIndex]
						: null;

					bool meaningful = baseEffect != null && baseEffect.Kind == upgradeEffect.Kind
						? CardEditorExtraEffects.HasMeaningfulUpgradeBaseSlotDelta(baseEffect, upgradeEffect, secondaryNumericFieldsAreDeltas: true)
						: CardEditorExtraEffects.HasMeaningfulUpgradeBaseSlotDelta(upgradeEffect, secondaryNumericFieldsAreDeltas: true);

					if (!meaningful)
					{
						effects.Add(null!);
						continue;
					}
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
					CardEditorOverrides.MarkEnergyCostJustUpgraded(card);
					card.EnergyCost.SetCustomBaseCost(Math.Max(-1, vanillaUpgraded + adjust));
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
				CardEditorOverrides.MarkStarCostJustUpgraded(card);
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

		CardModel baseCard = CardEditorOverrides.BuildPreview(canonical);
		int baseEnergyCost = baseCard.EnergyCost.GetWithModifiers(CostModifiers.None);
		int baseStarCost = baseCard.BaseStarCost;
		int baseReplayCount = baseCard.BaseReplayCount;
		Dictionary<string, decimal> baseVars = baseCard.DynamicVars.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.BaseValue, StringComparer.Ordinal);

		CardModel vanillaUpgraded = CardEditorOverrides.BuildPreview(canonical);
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
				overrideData.Upgrade.ExtraEffects = CardEditorExtraEffects.RebaseUpgradeEffectsAfterBaseEdit(
					existing.ExtraEffects,
					overrideData.ExtraEffects,
					existing.Upgrade.ExtraEffects);
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
		}
		catch
		{
		}
	}

	private void OpenUpgradeEditor()
	{
		CardModel canonical = ModelDb.GetById<CardModel>(_cardId);
		CardModel upgraded = CardEditorOverrides.BuildPreview(canonical);
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

		// Legacy Effect Sources are deprecated; use inline RunEffectSourceCard extra effects instead.
		List<ModelId> effectSourceIds = new List<ModelId>();

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
