using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace SlayTheSpire2Mod.CardEditor;

public partial class NRelicEditorPopup : Control
{
	private const string PopupName = "CardEditorRelicEditorPopup";
	// Wide enough that the full embedded card-effect rows (kind + amount + reorder/remove columns,
	// ~670px) fit in the settings column without a horizontal scrollbar.
	private static readonly Vector2 PanelSize = new(1180f, 720f);
	private static readonly Vector2 NumericFieldMinSize = new(150f, 44f);
	private static readonly Vector2 SpinButtonMinSize = new(34f, 20f);
	private static readonly Vector2 SpinContainerMinSize = new(34f, 44f);
	private static readonly string HeaderFontPath = "res://themes/kreon_bold_glyph_space_one.tres";
	private static readonly string BodyFontPath = "res://themes/kreon_regular_glyph_space_one.tres";
	private const string TickboxScenePath = "res://scenes/ui/tickbox.tscn";
	private const double HoldInitialDelaySeconds = 0.35;
	private const double HoldRepeatSlowSeconds = 0.12;
	private const double HoldRepeatFastSeconds = 0.04;
	private const double HoldAccelerationSeconds = 2.0;

	private static Font? _headerFont;
	private static Font? _bodyFont;
	private static PackedScene? _tickboxScene;
	private static StyleBoxFlat? _inputNormalStyleBox;
	private static StyleBoxFlat? _inputHoverStyleBox;
	private static StyleBoxFlat? _inputFocusStyleBox;
	private static StyleBoxFlat? _inputDisabledStyleBox;
	private static StyleBoxFlat? _spinButtonNormalStyleBox;
	private static StyleBoxFlat? _spinButtonHoverStyleBox;
	private static StyleBoxFlat? _spinButtonPressedStyleBox;
	private static StyleBoxFlat? _spinButtonDisabledStyleBox;

	private readonly Dictionary<string, NMegaLineEdit> _numberFields = new(StringComparer.Ordinal);
	private readonly Dictionary<LineEdit, RelicSpinButtons> _spinButtons = new();
	private readonly Dictionary<string, RelicEditorTickbox> _poolFields = new(StringComparer.Ordinal);
	private readonly Dictionary<string, RelicEditorTickbox> _fixedSourceFields = new(StringComparer.Ordinal);

	private ModelId _relicId = ModelId.none;
	private ColorRect? _backstop;
	private Control? _center;
	private PanelContainer? _panel;
	private TextureRect? _icon;
	private Label? _titleLabel;
	private Label? _descriptionLabel;
	private RelicEditorTickbox? _customTextTickbox;
	private TextEdit? _customTextField;
	private Control? _defaultFocus;
	private HoldSpinState? _holdSpinState;
	private bool _built;

	// Custom relic effects. Each "group" pairs a relic trigger with an embedded card-effect
	// editor (a hidden NCardEditorPopup driven via its embedded-host API) that builds the full
	// effect UI directly into this relic menu. Committed in ApplyCurrentRelicEditsToStore.
	private sealed class RelicEffectGroup
	{
		public RelicTriggerKind Trigger;
		public OptionButton TriggerSelect = null!;
		public NCardEditorPopup Host = null!;
		public Control Root = null!;
	}

	private readonly List<RelicEffectGroup> _effectGroups = new();
	private VBoxContainer? _effectGroupsContainer;
	private Button? _addEffectTriggerButton;
	private CardModel? _relicProxyCard;

	private static readonly RelicTriggerKind[] ActiveRelicTriggers =
	{
		RelicTriggerKind.OnCombatStart,
		RelicTriggerKind.OnTurnStart,
		RelicTriggerKind.OnTurnEnd,
		RelicTriggerKind.OnCardPlayed,
		RelicTriggerKind.OnDamageTaken,
		RelicTriggerKind.OnCombatEnd,
	};

	public static void Open(RelicModel relic)
	{
		if (relic == null)
		{
			return;
		}

		NRelicEditorPopup editor = new()
		{
			Name = PopupName,
			_relicId = relic.Id
		};

		NModalContainer? modalHost = NModalContainer.Instance;
		if (modalHost != null && GodotObject.IsInstanceValid(modalHost) && modalHost.OpenModal is NRelicEditorPopup)
		{
			Log.Warn($"[CardEditor][RelicEditor] Clearing stale relic modal before opening relic={relic.Id}");
			modalHost.Clear();
		}

		Node? host = GetFallbackPopupHost();
		if (host != null && GodotObject.IsInstanceValid(host))
		{
			try
			{
				foreach (NRelicEditorPopup popup in host.GetChildren().OfType<NRelicEditorPopup>().ToArray())
				{
					popup.QueueFree();
				}
				host.AddChild(editor);
				host.MoveChild(editor, host.GetChildCount() - 1);
				editor.Build();
				editor.ForceLayoutRefreshNow();
				Callable.From(editor.ForceLayoutRefreshNow).CallDeferred();
				Log.Info($"[CardEditor][RelicEditor] Popup added relic={relic.Id} host={host.Name} inTree={editor.IsInsideTree()} " +
					$"visible={editor.Visible} children={editor.GetChildCount()} size={editor.Size}");
			}
			catch (Exception ex)
			{
				Log.Warn($"[CardEditor][RelicEditor] Failed opening relic popup relic={relic.Id}: {ex}");
				if (GodotObject.IsInstanceValid(editor))
				{
					editor.QueueFree();
				}
			}
			return;
		}

		Log.Warn($"[CardEditor][RelicEditor] Could not find a valid host for relic editor popup relic={relic.Id}");
	}

	private static Node? GetFallbackPopupHost()
	{
		NGame? game = NGame.Instance;
		if (game == null || !GodotObject.IsInstanceValid(game))
		{
			return null;
		}

		return game;
	}

	internal static void CloseAnyOpen()
	{
		try
		{
			if (NModalContainer.Instance != null
				&& GodotObject.IsInstanceValid(NModalContainer.Instance)
				&& NModalContainer.Instance.OpenModal is NRelicEditorPopup)
			{
				NModalContainer.Instance.Clear();
			}

			Node? host = GetFallbackPopupHost();
			if (host == null || !GodotObject.IsInstanceValid(host))
			{
				return;
			}

			foreach (NRelicEditorPopup popup in host.GetChildren().OfType<NRelicEditorPopup>().ToArray())
			{
				popup.QueueFree();
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][RelicEditor] Failed closing existing relic editor popup: {ex}");
		}
	}

	public override void _Ready()
	{
		Log.Info($"[CardEditor][RelicEditor] Popup ready relic={_relicId} parent={GetParent()?.Name}");
		Build();
		ForceLayoutRefreshNow();
	}

	public override void _EnterTree()
	{
		Log.Info($"[CardEditor][RelicEditor] Popup enter-tree relic={_relicId} parent={GetParent()?.Name}");
	}

	public override void _Process(double delta)
	{
		if (_holdSpinState == null)
		{
			SetProcess(false);
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

	private void Build()
	{
		if (_built)
		{
			return;
		}
		_built = true;

		Name = PopupName;
		Visible = true;
		TopLevel = false;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Position = Vector2.Zero;
		Size = GetViewportRect().Size;
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		SizeFlagsVertical = SizeFlags.ExpandFill;
		MouseFilter = MouseFilterEnum.Stop;
		MouseDefaultCursorShape = CursorShape.Arrow;
		ZIndex = 1000;
		ZAsRelative = false;

		ColorRect dim = new()
		{
			Color = new Color(0f, 0f, 0f, 0.66f),
			MouseFilter = MouseFilterEnum.Stop,
			ZIndex = 0
		};
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dim);
		_backstop = dim;

		CenterContainer center = new()
		{
			MouseFilter = MouseFilterEnum.Ignore,
			ZIndex = 2
		};
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);
		_center = center;

		PanelContainer panel = new()
		{
			CustomMinimumSize = PanelSize,
			Size = PanelSize,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			SizeFlagsVertical = SizeFlags.ShrinkCenter
		};
		panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
		panel.ZIndex = 3;
		center.AddChild(panel);
		_panel = panel;

		VBoxContainer root = new()
		{
			CustomMinimumSize = new Vector2(940f, 620f)
		};
		root.AddThemeConstantOverride("separation", 14);
		panel.AddChild(root);

		Label heading = CreateHeading("Relic Editor", 46);
		root.AddChild(heading);

		HBoxContainer body = new();
		body.AddThemeConstantOverride("separation", 22);
		body.SizeFlagsVertical = SizeFlags.ExpandFill;
		root.AddChild(body);

		VBoxContainer preview = new()
		{
			CustomMinimumSize = new Vector2(300f, 500f)
		};
		preview.AddThemeConstantOverride("separation", 12);
		body.AddChild(preview);

		PanelContainer previewPanel = new();
		previewPanel.AddThemeStyleboxOverride("panel", CreateInnerStyle());
		previewPanel.CustomMinimumSize = new Vector2(300f, 500f);
		preview.AddChild(previewPanel);

		VBoxContainer previewContent = new();
		previewContent.AddThemeConstantOverride("separation", 10);
		previewPanel.AddChild(previewContent);

		_icon = new TextureRect
		{
			CustomMinimumSize = new Vector2(140f, 140f),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter
		};
		previewContent.AddChild(_icon);

		_titleLabel = CreateHeading("", 24);
		_titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_titleLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		previewContent.AddChild(_titleLabel);

		_descriptionLabel = CreateBodyLabel("");
		_descriptionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_descriptionLabel.SizeFlagsVertical = SizeFlags.ExpandFill;
		previewContent.AddChild(_descriptionLabel);

		ScrollContainer scroll = new()
		{
			CustomMinimumSize = new Vector2(600f, 500f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		body.AddChild(scroll);

		VBoxContainer settings = new();
		settings.AddThemeConstantOverride("separation", 18);
		settings.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scroll.AddChild(settings);

		AddNumberSection(settings);
		AddTextSection(settings);
		AddPoolSection(settings);
		AddEffectsSection(settings);

		HBoxContainer buttons = new()
		{
			Alignment = BoxContainer.AlignmentMode.End
		};
		buttons.AddThemeConstantOverride("separation", 12);
		root.AddChild(buttons);

		Button reset = CreateButton("Reset Relic");
		reset.Pressed += ResetRelicAndClose;
		buttons.AddChild(reset);

		Button cancel = CreateButton("Cancel");
		cancel.Pressed += Close;
		buttons.AddChild(cancel);

		Button apply = CreateButton("Apply");
		apply.Pressed += ApplyAndClose;
		buttons.AddChild(apply);
		_defaultFocus = apply;

		// If shared-state editing is locked (e.g. during an active multiplayer run), grey out the
		// mutating buttons and explain why on hover, instead of letting them silently no-op.
		string? lockReason = CardEditorMultiplayerSync.GetSharedStateLockReason();
		if (lockReason != null)
		{
			apply.Disabled = true;
			apply.TooltipText = lockReason;
			reset.Disabled = true;
			reset.TooltipText = lockReason;
			_defaultFocus = cancel;
		}

		RefreshPreviewFromUi();
		ForceLayoutRefreshNow();
		Callable.From(ForceLayoutRefreshNow).CallDeferred();
	}

	private void LogLayout()
	{
		try
		{
			Control? center = _center;
			PanelContainer? panel = _panel;
			Log.Info($"[CardEditor][RelicEditor] Popup layout relic={_relicId} popupVisible={Visible} popupSize={Size} " +
				$"centerVisible={center?.Visible} centerSize={center?.Size} panelVisible={panel?.Visible} panelPosition={panel?.Position} panelSize={panel?.Size} " +
				$"parent={GetParent()?.Name} children={GetChildCount()}");
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][RelicEditor] Failed logging popup layout: {ex}");
		}
	}

	public void ForceLayoutRefreshNow()
	{
		if (!GodotObject.IsInstanceValid(this))
		{
			return;
		}

		Vector2 availableSize = GetViewportRect().Size;
		if (availableSize.X <= 0f || availableSize.Y <= 0f)
		{
			availableSize = new Vector2(1920f, 1080f);
		}

		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		OffsetLeft = 0f;
		OffsetTop = 0f;
		OffsetRight = 0f;
		OffsetBottom = 0f;
		Position = Vector2.Zero;
		Size = availableSize;

		if (_backstop != null && GodotObject.IsInstanceValid(_backstop))
		{
			_backstop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			_backstop.Position = Vector2.Zero;
			_backstop.Size = availableSize;
		}

		if (_center != null && GodotObject.IsInstanceValid(_center))
		{
			_center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			_center.Position = Vector2.Zero;
			_center.Size = availableSize;
		}

		if (_panel != null && GodotObject.IsInstanceValid(_panel))
		{
			Vector2 clampedSize = new(
				Mathf.Min(PanelSize.X, availableSize.X - 80f),
				Mathf.Min(PanelSize.Y, availableSize.Y - 80f));
			clampedSize.X = Mathf.Max(640f, clampedSize.X);
			clampedSize.Y = Mathf.Max(480f, clampedSize.Y);
			_panel.AnchorLeft = 0f;
			_panel.AnchorTop = 0f;
			_panel.AnchorRight = 0f;
			_panel.AnchorBottom = 0f;
			_panel.OffsetLeft = 0f;
			_panel.OffsetTop = 0f;
			_panel.OffsetRight = 0f;
			_panel.OffsetBottom = 0f;
			_panel.Size = clampedSize;
			_panel.Position = new Vector2(
				Mathf.Round((availableSize.X - clampedSize.X) * 0.5f),
				Mathf.Round((availableSize.Y - clampedSize.Y) * 0.5f));
		}

		LogLayout();
	}

	private void AddNumberSection(VBoxContainer parent)
	{
		parent.AddChild(CreateHeading("Vanilla Numbers", 30));

		RelicModel? preview = BuildCurrentPreview();
		if (preview == null || preview.DynamicVars.Count == 0)
		{
			parent.AddChild(CreateMutedLabel("No editable vanilla numbers found on this relic."));
			return;
		}

		foreach ((string key, DynamicVar dynamicVar) in preview.DynamicVars.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
		{
			if (string.Equals(dynamicVar.GetType().Name, "StringVar", StringComparison.Ordinal))
			{
				continue;
			}

			HBoxContainer row = new();
			row.AddThemeConstantOverride("separation", 10);
			parent.AddChild(row);

			Label label = CreateBodyLabel($"{key}:");
			label.CustomMinimumSize = new Vector2(220f, 0f);
			row.AddChild(label);

			NMegaLineEdit field = new()
			{
				Text = FormatDecimal(dynamicVar.BaseValue),
				CustomMinimumSize = NumericFieldMinSize,
				SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
				FocusMode = FocusModeEnum.All,
				MouseFilter = MouseFilterEnum.Stop,
				Alignment = HorizontalAlignment.Center
			};
			StyleInput(field);
			field.TextChanged += _ => RefreshPreviewFromUi();
			row.AddChild(field);
			row.AddChild(CreateSpinButtons(field, step: 1m, minValue: -999999m, maxValue: 999999m));
			_numberFields[key] = field;
		}

		if (_numberFields.Count == 0)
		{
			parent.AddChild(CreateMutedLabel("This relic only exposes text variables, not editable numeric values."));
		}
	}

	private void AddTextSection(VBoxContainer parent)
	{
		parent.AddChild(CreateHeading("Text", 30));

		RelicModel? canonical = GetCanonicalRelic();
		RelicOverride? existing = canonical != null ? CardEditorRelicOverrides.Get(canonical.Id) : null;
		bool hasCustomText = existing?.CustomDescriptionEnabled == true;

		HBoxContainer customTextRow = CreateTickboxRow("Custom Text", hasCustomText, out RelicEditorTickbox customTextTickbox);
		customTextTickbox.Toggled += () =>
		{
			if (_customTextField != null && GodotObject.IsInstanceValid(_customTextField))
			{
				if (customTextTickbox.IsTicked && string.IsNullOrEmpty(_customTextField.Text))
				{
					_customTextField.Text = BuildCurrentPreview()?.DynamicDescription.GetFormattedText() ?? string.Empty;
				}
				_customTextField.Visible = customTextTickbox.IsTicked;
			}
			RefreshPreviewFromUi();
		};
		parent.AddChild(customTextRow);
		_customTextTickbox = customTextTickbox;

		_customTextField = new TextEdit
		{
			Text = hasCustomText ? (existing?.CustomDescription ?? string.Empty) : string.Empty,
			CustomMinimumSize = new Vector2(0f, 110f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			WrapMode = TextEdit.LineWrappingMode.Boundary,
			Visible = hasCustomText
		};
		StyleInput(_customTextField);
		_customTextField.TextChanged += RefreshPreviewFromUi;
		parent.AddChild(_customTextField);
	}

	private void AddPoolSection(VBoxContainer parent)
	{
		parent.AddChild(CreateHeading("Reward Pools", 30));
		Label help = CreateMutedLabel("These checkboxes affect random relic reward pools. Fixed event and Ancient relic grants are separate event option sources, listed below.");
		help.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		parent.AddChild(help);

		RelicModel? canonical = GetCanonicalRelic();
		if (canonical == null)
		{
			return;
		}

		HashSet<string> selected = CardEditorRelicOverrides.GetEffectivePoolKeys(canonical);
		GridContainer grid = new()
		{
			Columns = 2,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		grid.AddThemeConstantOverride("h_separation", 28);
		grid.AddThemeConstantOverride("v_separation", 8);
		parent.AddChild(grid);

		foreach (RelicPoolModel pool in CardEditorRelicOverrides.EditablePools())
		{
			string key = CardEditorRelicOverrides.GetPoolKey(pool);
			RelicEditorTickbox check = CreateStandaloneTickbox(CardEditorRelicOverrides.GetPoolLabel(pool), selected.Contains(key), RefreshPreviewFromUi);
			check.CustomMinimumSize = new Vector2(240f, 38f);
			check.TooltipText = CardEditorRelicOverrides.GetPoolDescription(pool);
			check.Label.TooltipText = check.TooltipText;
			grid.AddChild(check);
			_poolFields[key] = check;
		}

		AddFixedSourceSection(parent, canonical);
	}

	private void AddFixedSourceSection(VBoxContainer parent, RelicModel canonical)
	{
		parent.AddChild(CreateHeading("Fixed/Event Sources", 24));
		Label help = CreateMutedLabel("Ancient sources are real editable event-option tables. Other fixed event sources are listed for visibility because many of them run custom event scripts.");
		help.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		parent.AddChild(help);

		HashSet<string> selected = CardEditorRelicOverrides.GetEffectiveFixedSourceKeys(canonical);
		List<RelicSourceSummary> allSources = CardEditorRelicOverrides.EditableFixedSources();
		List<RelicSourceSummary> editableSources = allSources.Where(source => source.Editable).ToList();
		if (editableSources.Count > 0)
		{
			GridContainer grid = new()
			{
				Columns = 2,
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			grid.AddThemeConstantOverride("h_separation", 28);
			grid.AddThemeConstantOverride("v_separation", 8);
			parent.AddChild(grid);

			foreach (RelicSourceSummary source in editableSources)
			{
				RelicEditorTickbox check = CreateStandaloneTickbox(source.Label, selected.Contains(source.Key), RefreshPreviewFromUi);
				check.CustomMinimumSize = new Vector2(240f, 38f);
				check.TooltipText = source.Description;
				check.Label.TooltipText = source.Description;
				grid.AddChild(check);
				_fixedSourceFields[source.Key] = check;
			}
		}

		List<RelicSourceSummary> readOnlySources = CardEditorRelicOverrides.GetFixedRelicSourceSummaries(canonical)
			.Where(source => !source.Editable)
			.ToList();
		if (readOnlySources.Count > 0)
		{
			parent.AddChild(CreateMutedLabel("Other fixed sources detected:"));
			VBoxContainer list = new();
			list.AddThemeConstantOverride("separation", 4);
			parent.AddChild(list);

			foreach (RelicSourceSummary source in readOnlySources)
			{
				Label label = CreateBodyLabel(source.Label);
				label.TooltipText = source.Description;
				label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
				list.AddChild(label);
			}
		}
		else if (editableSources.Count == 0)
		{
			parent.AddChild(CreateMutedLabel("No fixed event or Ancient source found for this relic."));
		}
	}

	private RelicModel? GetCanonicalRelic()
	{
		if (_relicId == ModelId.none)
		{
			return null;
		}
		return ModelDb.GetByIdOrNull<RelicModel>(_relicId);
	}

	private RelicModel? BuildCurrentPreview()
	{
		RelicModel? canonical = GetCanonicalRelic();
		if (canonical == null)
		{
			return null;
		}

		RelicModel preview = CardEditorRelicOverrides.BuildPreview(canonical);
		if (_numberFields.Count == 0)
		{
			return preview;
		}

		foreach ((string key, NMegaLineEdit field) in _numberFields)
		{
			if (preview.DynamicVars.TryGetValue(key, out DynamicVar? dynamicVar))
			{
				dynamicVar.BaseValue = ParseDecimalOrFallback(field.Text, dynamicVar.BaseValue);
			}
		}
		return preview;
	}

	private void RefreshPreviewFromUi()
	{
		try
		{
			RelicModel? preview = BuildCurrentPreview();
			if (preview == null)
			{
				return;
			}

			if (_icon != null)
			{
				_icon.Texture = preview.BigIcon;
			}
			if (_titleLabel != null)
			{
				_titleLabel.Text = FormatRelicTextForPreview(preview.Title.GetFormattedText());
			}
			if (_descriptionLabel != null)
			{
				string rarity = preview.Rarity.ToString();
				string pools = string.Join(", ", GetSelectedPoolKeys(preview)
					.Select(key => CardEditorRelicOverrides.EditablePools().FirstOrDefault(pool => CardEditorRelicOverrides.GetPoolKey(pool) == key))
					.Where(pool => pool != null)
					.Select(pool => CardEditorRelicOverrides.GetPoolLabel(pool!))
					.DefaultIfEmpty("No pool"));
				string rawDescription = preview.DynamicDescription.GetFormattedText() ?? string.Empty;
				bool customTextEnabled = _customTextTickbox?.IsTicked ?? false;
				string displayDescription = customTextEnabled && _customTextField != null && GodotObject.IsInstanceValid(_customTextField)
					? _customTextField.Text ?? string.Empty
					: rawDescription;
				_descriptionLabel.Text = $"{rarity}\n{pools}\n\n{FormatRelicTextForPreview(displayDescription)}";
				if (_customTextField != null
					&& GodotObject.IsInstanceValid(_customTextField)
					&& !customTextEnabled
					&& !_customTextField.HasFocus())
				{
					_customTextField.Text = rawDescription;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][RelicEditor] Failed refreshing relic preview: {ex}");
		}
	}

	private void AddEffectsSection(VBoxContainer parent)
	{
		parent.AddChild(CreateHeading("Custom Effects", 30));
		parent.AddChild(CreateMutedLabel(
			"Give this relic card-editor effects. Add a trigger for each moment in combat the relic should act, " +
			"then build its effects with the full effect editor below. Every effect type works exactly as it does on cards."));

		_effectGroupsContainer = new VBoxContainer();
		_effectGroupsContainer.AddThemeConstantOverride("separation", 16);
		_effectGroupsContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		parent.AddChild(_effectGroupsContainer);

		_addEffectTriggerButton = CreateButton("Add Effect Trigger");
		_addEffectTriggerButton.Pressed += () =>
		{
			RelicTriggerKind? next = FirstUnusedTrigger();
			if (next.HasValue)
			{
				AddEffectGroup(next.Value, null);
			}
		};
		parent.AddChild(_addEffectTriggerButton);

		LoadExistingEffectGroups();
		RefreshGroupTriggerOptions();
	}

	private void LoadExistingEffectGroups()
	{
		RelicModel? canonical = GetCanonicalRelic();
		RelicOverride? existing = canonical != null ? CardEditorRelicOverrides.Get(canonical.Id) : null;
		if (existing == null)
		{
			return;
		}

		// Group saved effects by trigger, preserving first-seen order.
		List<RelicTriggerKind> effectOrder = new();
		Dictionary<RelicTriggerKind, List<CardExtraEffect>> byTrigger = new();
		if (existing.ExtraEffects != null)
		{
			foreach (RelicEffectEntry entry in existing.ExtraEffects)
			{
				if (entry?.Effect == null)
				{
					continue;
				}
				if (!byTrigger.TryGetValue(entry.Trigger, out List<CardExtraEffect>? list))
				{
					list = new List<CardExtraEffect>();
					byTrigger[entry.Trigger] = list;
					effectOrder.Add(entry.Trigger);
				}
				list.Add(CardEditorExtraEffects.CloneEffect(entry.Effect));
			}
		}

		// EffectTriggers (when present) is the authoritative group list, so empty groups reload.
		// Legacy overrides saved before EffectTriggers fall back to the effect-derived order.
		List<RelicTriggerKind> groupOrder = new();
		if (existing.EffectTriggers != null)
		{
			foreach (RelicTriggerKind t in existing.EffectTriggers)
			{
				if (!groupOrder.Contains(t))
				{
					groupOrder.Add(t);
				}
			}
		}
		foreach (RelicTriggerKind t in effectOrder)
		{
			if (!groupOrder.Contains(t))
			{
				groupOrder.Add(t);
			}
		}

		foreach (RelicTriggerKind trigger in groupOrder)
		{
			byTrigger.TryGetValue(trigger, out List<CardExtraEffect>? effects);
			AddEffectGroup(trigger, effects);
		}
	}

	private RelicTriggerKind? FirstUnusedTrigger()
	{
		foreach (RelicTriggerKind trigger in ActiveRelicTriggers)
		{
			if (!_effectGroups.Any(g => g.Trigger == trigger))
			{
				return trigger;
			}
		}
		return null;
	}

	// Keeps the trigger model set-shaped: each trigger belongs to at most one group. Disables
	// already-used triggers in every group's dropdown and disables "Add Effect Trigger" when full,
	// so duplicate-trigger groups (which silently merge on save/reload) can never be created.
	private void RefreshGroupTriggerOptions()
	{
		HashSet<RelicTriggerKind> used = new(_effectGroups.Select(g => g.Trigger));
		foreach (RelicEffectGroup g in _effectGroups)
		{
			if (g.TriggerSelect == null || !GodotObject.IsInstanceValid(g.TriggerSelect))
			{
				continue;
			}
			for (int i = 0; i < ActiveRelicTriggers.Length; i++)
			{
				RelicTriggerKind t = ActiveRelicTriggers[i];
				g.TriggerSelect.SetItemDisabled(i, t != g.Trigger && used.Contains(t));
			}
		}
		if (_addEffectTriggerButton != null && GodotObject.IsInstanceValid(_addEffectTriggerButton))
		{
			_addEffectTriggerButton.Disabled = _effectGroups.Count >= ActiveRelicTriggers.Length;
		}
	}

	private void AddEffectGroup(RelicTriggerKind trigger, List<CardExtraEffect>? effects)
	{
		if (_effectGroupsContainer == null || !GodotObject.IsInstanceValid(_effectGroupsContainer))
		{
			return;
		}

		CardModel? proxy = _relicProxyCard ??= TryGetRelicProxyCard();
		if (proxy == null)
		{
			Log.Warn("[CardEditor][RelicEditor] Relic proxy card is not registered; cannot add an effect trigger.");
			return;
		}

		PanelContainer groupPanel = new();
		groupPanel.AddThemeStyleboxOverride("panel", CreateInnerStyle());
		VBoxContainer groupRoot = new();
		groupRoot.AddThemeConstantOverride("separation", 8);
		groupRoot.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		groupPanel.AddChild(groupRoot);

		Label groupTitle = CreateHeading(string.Empty, 24);
		groupRoot.AddChild(groupTitle);

		HBoxContainer headerRow = new();
		headerRow.AddThemeConstantOverride("separation", 10);
		Label whenLabel = CreateBodyLabel("When:");
		whenLabel.CustomMinimumSize = new Vector2(70f, 0f);
		headerRow.AddChild(whenLabel);

		OptionButton triggerSelect = new()
		{
			CustomMinimumSize = new Vector2(300f, 44f),
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin
		};
		StyleInput(triggerSelect);
		foreach (RelicTriggerKind t in ActiveRelicTriggers)
		{
			triggerSelect.AddItem(RelicTriggerLabel(t), (int)t);
		}
		int selIndex = Array.IndexOf(ActiveRelicTriggers, trigger);
		if (selIndex < 0)
		{
			// An unsupported/legacy trigger (e.g. hand-edited JSON) cannot be shown or dispatched;
			// normalize it to the first supported trigger so label and stored value never diverge.
			trigger = ActiveRelicTriggers[0];
			selIndex = 0;
		}
		triggerSelect.Select(selIndex);
		groupTitle.Text = RelicTriggerLabel(trigger);
		headerRow.AddChild(triggerSelect);

		Button removeButton = CreateButton("Remove");
		headerRow.AddChild(removeButton);
		groupRoot.AddChild(headerRow);

		VBoxContainer effectsContainer = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		groupRoot.AddChild(effectsContainer);

		_effectGroupsContainer.AddChild(groupPanel);

		// Hidden host node: lives in this menu's tree so deferred calls work, but only builds
		// its effect-list UI into effectsContainer (no popup chrome).
		NCardEditorPopup host = new();
		host.InitializeAsEmbeddedEffectHost(proxy);
		host.Name = "RelicEffectHost_" + _effectGroups.Count;
		AddChild(host);
		host.BuildEmbeddedEffectsUi(effectsContainer);
		if (effects != null)
		{
			foreach (CardExtraEffect e in effects)
			{
				host.LoadEmbeddedEffect(e);
			}
		}

		RelicEffectGroup group = new()
		{
			Trigger = trigger,
			TriggerSelect = triggerSelect,
			Host = host,
			Root = groupPanel
		};
		triggerSelect.ItemSelected += idx =>
		{
			group.Trigger = (RelicTriggerKind)triggerSelect.GetItemId((int)idx);
			groupTitle.Text = RelicTriggerLabel(group.Trigger);
			RefreshGroupTriggerOptions();
		};
		removeButton.Pressed += () => RemoveEffectGroup(group);

		_effectGroups.Add(group);
		RefreshGroupTriggerOptions();
	}

	private void RemoveEffectGroup(RelicEffectGroup group)
	{
		_effectGroups.Remove(group);
		if (group.Host != null && GodotObject.IsInstanceValid(group.Host))
		{
			group.Host.QueueFreeSafely();
		}
		if (group.Root != null && GodotObject.IsInstanceValid(group.Root))
		{
			group.Root.QueueFreeSafely();
		}
		RefreshGroupTriggerOptions();
	}

	private CardModel? TryGetRelicProxyCard()
	{
		try
		{
			return ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId<CardEditorRelicProxyCard>());
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][RelicEditor] Failed resolving relic proxy card: {ex.Message}");
			return null;
		}
	}

	private static string RelicTriggerLabel(RelicTriggerKind trigger)
	{
		return trigger switch
		{
			RelicTriggerKind.OnCombatStart => "At combat start",
			RelicTriggerKind.OnTurnStart => "At the start of your turn",
			RelicTriggerKind.OnTurnEnd => "At the end of your turn",
			RelicTriggerKind.OnCardPlayed => "When you play a card",
			RelicTriggerKind.OnDamageTaken => "When you take damage",
			RelicTriggerKind.OnCombatEnd => "At combat end",
			_ => trigger.ToString()
		};
	}

	// Reads every group's embedded effect editor back into a flat list of (trigger, effect) entries.
	private List<RelicEffectEntry> CollectEffectGroupEntries()
	{
		List<RelicEffectEntry> entries = new();
		foreach (RelicEffectGroup group in _effectGroups)
		{
			if (group.Host == null || !GodotObject.IsInstanceValid(group.Host))
			{
				continue;
			}
			foreach (CardExtraEffect effect in group.Host.ReadEmbeddedEffects())
			{
				if (effect == null)
				{
					continue;
				}
				entries.Add(new RelicEffectEntry
				{
					Trigger = group.Trigger,
					Effect = effect
				});
			}
		}
		return entries;
	}

	private void ApplyAndClose()
	{
		if (!CardEditorMultiplayerSync.CanEditSharedState())
		{
			Log.Info($"[CardEditor][MultiplayerSync] Relic editor apply blocked: {CardEditorMultiplayerSync.GetSharedStateLockReason()}");
			return;
		}

		if (ApplyCurrentRelicEditsToStore())
		{
			CardEditorMultiplayerSync.NotifySharedStateMutatedLocally();
		}
		Close();
	}

	private bool ApplyCurrentRelicEditsToStore()
	{
		RelicModel? canonical = GetCanonicalRelic();
		if (canonical == null)
		{
			return false;
		}

		RelicOverride overrideData = new();

		Dictionary<string, decimal> numbers = new(StringComparer.Ordinal);
		foreach ((string key, NMegaLineEdit field) in _numberFields)
		{
			if (!canonical.DynamicVars.TryGetValue(key, out DynamicVar? vanillaVar))
			{
				continue;
			}

			decimal value = ParseDecimalOrFallback(field.Text, vanillaVar.BaseValue);
			if (value != vanillaVar.BaseValue)
			{
				numbers[key] = value;
			}
		}
		if (numbers.Count > 0)
		{
			overrideData.DynamicVarBaseValues = numbers;
		}

		if (_customTextTickbox?.IsTicked == true)
		{
			overrideData.CustomDescriptionEnabled = true;
			overrideData.CustomDescription = _customTextField?.Text ?? string.Empty;
		}

		HashSet<string> selectedPools = GetSelectedPoolKeys(canonical);
		HashSet<string> vanillaPools = CardEditorRelicOverrides.GetVanillaPoolKeys(canonical);
		if (!selectedPools.SetEquals(vanillaPools))
		{
			overrideData.PoolKeys = selectedPools;
		}

		HashSet<string> selectedFixedSources = GetSelectedFixedSourceKeys(canonical);
		HashSet<string> vanillaFixedSources = CardEditorRelicOverrides.GetVanillaFixedSourceKeys(canonical);
		if (!selectedFixedSources.SetEquals(vanillaFixedSources))
		{
			overrideData.FixedSourceKeys = selectedFixedSources;
		}

		List<RelicEffectEntry> effects = CollectEffectGroupEntries();
		if (effects.Count > 0)
		{
			overrideData.ExtraEffects = effects;
		}

		// Persist the trigger of every group (even empty ones) so a parked trigger survives reopen.
		List<RelicTriggerKind> groupTriggers = _effectGroups.Select(g => g.Trigger).Distinct().ToList();
		if (groupTriggers.Count > 0)
		{
			overrideData.EffectTriggers = groupTriggers;
		}

		CardEditorRelicOverrides.SetAndSave(canonical.Id, overrideData);
		return true;
	}

	private void ResetRelicAndClose()
	{
		if (!CardEditorMultiplayerSync.CanEditSharedState())
		{
			Log.Info($"[CardEditor][MultiplayerSync] Relic editor reset blocked: {CardEditorMultiplayerSync.GetSharedStateLockReason()}");
			return;
		}

		RelicModel? canonical = GetCanonicalRelic();
		if (canonical != null)
		{
			CardEditorRelicOverrides.SetAndSave(canonical.Id, null);
			CardEditorMultiplayerSync.NotifySharedStateMutatedLocally();
		}
		Close();
	}

	private void Close()
	{
		if (NModalContainer.Instance != null && GetParent() == NModalContainer.Instance)
		{
			NModalContainer.Instance.Clear();
			return;
		}

		QueueFree();
	}

	private HashSet<string> GetSelectedPoolKeys(RelicModel relic)
	{
		if (_poolFields.Count == 0)
		{
			return CardEditorRelicOverrides.GetEffectivePoolKeys(relic);
		}

		return _poolFields
			.Where(kvp => kvp.Value.IsTicked)
			.Select(kvp => kvp.Key)
			.ToHashSet(StringComparer.Ordinal);
	}

	private HashSet<string> GetSelectedFixedSourceKeys(RelicModel relic)
	{
		if (_fixedSourceFields.Count == 0)
		{
			return CardEditorRelicOverrides.GetEffectiveFixedSourceKeys(relic);
		}

		HashSet<string> selected = _fixedSourceFields
			.Where(kvp => kvp.Value.IsTicked)
			.Select(kvp => kvp.Key)
			.ToHashSet(StringComparer.Ordinal);

		foreach (RelicSourceSummary source in CardEditorRelicOverrides.GetFixedRelicSourceSummaries(relic).Where(source => !source.Editable))
		{
			selected.Add(source.Key);
		}
		return selected;
	}

	private static Label CreateHeading(string text, int size)
	{
		Label label = new()
		{
			Text = text
		};
		CardEditorGodotResourceCache.TryLoad(ref _headerFont, HeaderFontPath);
		if (_headerFont != null)
		{
			label.AddThemeFontOverride("font", _headerFont);
		}
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", size >= 34 ? StsColors.gold : StsColors.cream);
		label.AddThemeColorOverride("font_outline_color", StsColors.transparentBlack);
		label.AddThemeConstantOverride("outline_size", size >= 34 ? 16 : 12);
		return label;
	}

	private static Label CreateBodyLabel(string text)
	{
		Label label = new()
		{
			Text = text
		};
		StyleBodyLabel(label);
		return label;
	}

	private static void StyleBodyLabel(Control control)
	{
		CardEditorGodotResourceCache.TryLoad(ref _bodyFont, BodyFontPath);
		if (_bodyFont != null)
		{
			control.AddThemeFontOverride("font", _bodyFont);
		}
		control.AddThemeFontSizeOverride("font_size", 20);
		control.AddThemeColorOverride("font_color", StsColors.cream);
		control.AddThemeColorOverride("font_outline_color", StsColors.transparentBlack);
		control.AddThemeConstantOverride("outline_size", 10);
	}

	private static Label CreateMutedLabel(string text)
	{
		Label label = CreateBodyLabel(text);
		label.AddThemeColorOverride("font_color", StsColors.gray);
		label.AddThemeConstantOverride("outline_size", 8);
		// Muted labels are always help/explanatory text; wrap so long strings never overflow the column.
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		return label;
	}

	private static Button CreateButton(string text)
	{
		Button button = new()
		{
			Text = text,
			CustomMinimumSize = new Vector2(150f, 48f)
		};
		StyleInput(button);
		return button;
	}

	private static string FormatRelicTextForPreview(string? formattedText)
	{
		if (string.IsNullOrWhiteSpace(formattedText))
		{
			return string.Empty;
		}

		string text = formattedText.Replace("\r", string.Empty);
		text = Regex.Replace(
			text,
			@"\[(?:img|image)(?:[^\]]*)\](?<path>.*?)\[/\s*(?:img|image)\]",
			match => " " + ResolveInlineImageDisplay(match.Groups["path"].Value) + " ",
			RegexOptions.IgnoreCase | RegexOptions.Singleline);
		text = Regex.Replace(
			text,
			@"\[(?:img|image)[^\]]*(?<path>res://[^\]\s]+)[^\]]*\]",
			match => " " + ResolveInlineImageDisplay(match.Groups["path"].Value) + " ",
			RegexOptions.IgnoreCase);
		text = Regex.Replace(
			text,
			@"res://images/packed/sprite_fonts/[A-Za-z0-9_\-/]+\.png",
			match => " " + ResolveInlineImageDisplay(match.Value) + " ",
			RegexOptions.IgnoreCase);
		text = Regex.Replace(text, @"<[^>]+>", string.Empty);
		text = CardEditorCustomKeywordLibrary.StripMarkupForDisplay(text);
		text = Regex.Replace(text, @"[ \t]+\n", "\n");
		text = Regex.Replace(text, @"\n{3,}", "\n\n");
		text = Regex.Replace(text, @"[ \t]{2,}", " ");
		text = Regex.Replace(text, @"\s+([.,;:!?])", "$1");
		return text.Trim();
	}

	private static string ResolveInlineImageDisplay(string imageText)
	{
		string lower = (imageText ?? string.Empty).Trim().ToLowerInvariant();
		if (lower.Contains("star_icon"))
		{
			return "\u2726";
		}
		if (lower.Contains("energy"))
		{
			return "\u25CF";
		}
		if (lower.Contains("block"))
		{
			return "\u25A3";
		}
		return "\u25C6";
	}

	private Control CreateSpinButtons(LineEdit field, decimal step, decimal? minValue, decimal? maxValue)
	{
		VBoxContainer container = new()
		{
			CustomMinimumSize = SpinContainerMinSize,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			SizeFlagsVertical = SizeFlags.ShrinkCenter
		};
		container.AddThemeConstantOverride("separation", 2);

		Button up = CreateSpinButton("\u25B2");
		Button down = CreateSpinButton("\u25BC");
		container.AddChild(up);
		container.AddChild(down);

		RelicSpinButtons spin = new()
		{
			Field = field,
			Container = container,
			Up = up,
			Down = down,
			Step = step,
			MinValue = minValue,
			MaxValue = maxValue
		};
		_spinButtons[field] = spin;

		up.Connect(BaseButton.SignalName.ButtonDown, Callable.From(() => StartSpinHold(spin, +1)));
		down.Connect(BaseButton.SignalName.ButtonDown, Callable.From(() => StartSpinHold(spin, -1)));
		up.Connect(BaseButton.SignalName.ButtonUp, Callable.From(StopSpinHold));
		down.Connect(BaseButton.SignalName.ButtonUp, Callable.From(StopSpinHold));

		return container;
	}

	private static Button CreateSpinButton(string glyph)
	{
		Button button = new()
		{
			Text = glyph,
			Flat = true,
			FocusMode = FocusModeEnum.None,
			CustomMinimumSize = SpinButtonMinSize,
			MouseFilter = MouseFilterEnum.Stop,
			Alignment = HorizontalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter
		};
		CardEditorGodotResourceCache.TryLoad(ref _headerFont, HeaderFontPath);
		if (_headerFont != null)
		{
			button.AddThemeFontOverride("font", _headerFont);
		}
		button.AddThemeFontSizeOverride("font_size", 18);
		button.AddThemeColorOverride("font_color", StsColors.gold);
		button.AddThemeColorOverride("font_outline_color", StsColors.transparentBlack);
		button.AddThemeConstantOverride("outline_size", 10);
		button.AddThemeStyleboxOverride("normal", GetSpinButtonNormalStyleBox());
		button.AddThemeStyleboxOverride("hover", GetSpinButtonHoverStyleBox());
		button.AddThemeStyleboxOverride("pressed", GetSpinButtonPressedStyleBox());
		button.AddThemeStyleboxOverride("disabled", GetSpinButtonDisabledStyleBox());
		return button;
	}

	private void StartSpinHold(RelicSpinButtons target, int direction)
	{
		if (direction == 0)
		{
			return;
		}

		Button activeButton = direction > 0 ? target.Up : target.Down;
		if (!GodotObject.IsInstanceValid(activeButton) || activeButton.Disabled)
		{
			return;
		}

		SpinStepOnce(target, direction);
		_holdSpinState = new HoldSpinState
		{
			Target = target,
			Direction = direction,
			HeldSeconds = 0,
			RepeatCountdownSeconds = HoldInitialDelaySeconds
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
		double t = Math.Clamp((heldSeconds - HoldInitialDelaySeconds) / HoldAccelerationSeconds, 0.0, 1.0);
		return HoldRepeatSlowSeconds + (HoldRepeatFastSeconds - HoldRepeatSlowSeconds) * t;
	}

	private void SpinStepOnce(RelicSpinButtons target, int direction)
	{
		if (direction == 0)
		{
			return;
		}

		LineEdit field = target.Field;
		if (!GodotObject.IsInstanceValid(field) || field.IsQueuedForDeletion() || !field.Editable)
		{
			return;
		}

		decimal current = ParseDecimalOrFallback(field.Text, 0m);
		decimal next = Clamp(current + target.Step * direction, target.MinValue, target.MaxValue);
		field.Text = FormatDecimal(next);
		RefreshPreviewFromUi();
	}

	private static decimal ParseDecimalOrFallback(string? text, decimal fallback)
	{
		return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed)
			? parsed
			: fallback;
	}

	private static decimal Clamp(decimal value, decimal? minValue, decimal? maxValue)
	{
		if (minValue.HasValue && value < minValue.Value)
		{
			return minValue.Value;
		}
		if (maxValue.HasValue && value > maxValue.Value)
		{
			return maxValue.Value;
		}
		return value;
	}

	private static string FormatDecimal(decimal value)
	{
		return decimal.Truncate(value) == value
			? decimal.ToInt32(value).ToString(CultureInfo.InvariantCulture)
			: value.ToString(CultureInfo.InvariantCulture);
	}

	private static HBoxContainer CreateTickboxRow(string labelText, bool initialValue, out RelicEditorTickbox tickbox, Action? onToggled = null)
	{
		HBoxContainer row = new();
		row.AddThemeConstantOverride("separation", 10);
		tickbox = CreateStandaloneTickbox(labelText, initialValue, onToggled);
		row.AddChild(tickbox);
		return row;
	}

	private static RelicEditorTickbox CreateStandaloneTickbox(string labelText, bool initialValue, Action? onToggled = null)
	{
		Label label = CreateBodyLabel(labelText);
		Control tickboxVisuals = InstantiateTickboxVisuals();
		RelicEditorTickbox tickbox = new(tickboxVisuals, label, initialValue);
		if (onToggled != null)
		{
			tickbox.Toggled += onToggled;
		}
		return tickbox;
	}

	private static PackedScene GetTickboxScene()
	{
		CardEditorGodotResourceCache.Load(ref _tickboxScene, TickboxScenePath);
		return _tickboxScene!;
	}

	private static Control InstantiateTickboxVisuals()
	{
		return GetTickboxScene().Instantiate<Control>(PackedScene.GenEditState.Disabled);
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

	private static StyleBoxFlat GetInputNormalStyleBox()
	{
		return _inputNormalStyleBox ??= CreateInputStyleBox(
			new Color(0.055f, 0.065f, 0.075f, 0.95f),
			new Color(0.20f, 0.23f, 0.26f, 1f));
	}

	private static StyleBoxFlat GetInputHoverStyleBox()
	{
		return _inputHoverStyleBox ??= CreateInputStyleBox(
			new Color(0.075f, 0.09f, 0.105f, 0.98f),
			new Color(0.34f, 0.38f, 0.42f, 1f));
	}

	private static StyleBoxFlat GetInputFocusStyleBox()
	{
		return _inputFocusStyleBox ??= CreateInputStyleBox(
			new Color(0.065f, 0.08f, 0.095f, 1f),
			StsColors.gold,
			2);
	}

	private static StyleBoxFlat GetInputDisabledStyleBox()
	{
		return _inputDisabledStyleBox ??= CreateInputStyleBox(
			new Color(0.035f, 0.035f, 0.04f, 0.72f),
			new Color(0.12f, 0.13f, 0.14f, 1f));
	}

	private static StyleBoxFlat CreateFlatFillStyleBox(Color bgColor)
	{
		return new StyleBoxFlat
		{
			BgColor = bgColor
		};
	}

	private static StyleBoxFlat GetSpinButtonNormalStyleBox()
	{
		return _spinButtonNormalStyleBox ??= CreateFlatFillStyleBox(new Color(0f, 0f, 0f, 0f));
	}

	private static StyleBoxFlat GetSpinButtonHoverStyleBox()
	{
		return _spinButtonHoverStyleBox ??= CreateFlatFillStyleBox(new Color(1f, 1f, 1f, 0.06f));
	}

	private static StyleBoxFlat GetSpinButtonPressedStyleBox()
	{
		return _spinButtonPressedStyleBox ??= CreateFlatFillStyleBox(new Color(1f, 1f, 1f, 0.10f));
	}

	private static StyleBoxFlat GetSpinButtonDisabledStyleBox()
	{
		return _spinButtonDisabledStyleBox ??= CreateFlatFillStyleBox(new Color(0f, 0f, 0f, 0f));
	}

	private static void StyleInput(Control control)
	{
		CardEditorGodotResourceCache.TryLoad(ref _bodyFont, BodyFontPath);
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
		if (control is OptionButton optionButton)
		{
			optionButton.ClipText = true;
		}
		else if (control is Button button)
		{
			button.ClipText = false;
		}
		if (control is Button || control is LineEdit || control is TextEdit)
		{
			StyleBoxFlat focus = GetInputFocusStyleBox();
			StyleBoxFlat disabled = GetInputDisabledStyleBox();
			control.AddThemeStyleboxOverride("normal", GetInputNormalStyleBox());
			control.AddThemeStyleboxOverride("hover", GetInputHoverStyleBox());
			control.AddThemeStyleboxOverride("pressed", focus);
			control.AddThemeStyleboxOverride("focus", focus);
			control.AddThemeStyleboxOverride("disabled", disabled);
			control.AddThemeStyleboxOverride("read_only", disabled);
		}
	}

	private static StyleBoxFlat CreatePanelStyle()
	{
		StyleBoxFlat style = new()
		{
			BgColor = new Color(0.015f, 0.015f, 0.012f, 0.96f),
			BorderColor = new Color(1f, 0.82f, 0.17f, 1f),
			ContentMarginLeft = 20f,
			ContentMarginRight = 20f,
			ContentMarginTop = 16f,
			ContentMarginBottom = 16f
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(8);
		return style;
	}

	private static StyleBoxFlat CreateInnerStyle()
	{
		StyleBoxFlat style = new()
		{
			BgColor = new Color(0.02f, 0.025f, 0.024f, 0.92f),
			BorderColor = new Color(0.9f, 0.7f, 0.1f, 0.9f),
			ContentMarginLeft = 16f,
			ContentMarginRight = 16f,
			ContentMarginTop = 16f,
			ContentMarginBottom = 16f
		};
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(6);
		return style;
	}

	private sealed class RelicSpinButtons
	{
		public LineEdit Field { get; init; } = null!;
		public VBoxContainer Container { get; init; } = null!;
		public Button Up { get; init; } = null!;
		public Button Down { get; init; } = null!;
		public decimal Step { get; init; } = 1m;
		public decimal? MinValue { get; init; }
		public decimal? MaxValue { get; init; }
	}

	private sealed class HoldSpinState
	{
		public RelicSpinButtons Target { get; init; } = null!;
		public int Direction { get; init; }
		public double HeldSeconds { get; set; }
		public double RepeatCountdownSeconds { get; set; }
	}

	private sealed class RelicEditorTickbox : HBoxContainer
	{
		private readonly Control _tickedImage;
		private readonly Control _notTickedImage;

		public bool IsTicked { get; private set; }
		public Label Label { get; }

		public event Action? Toggled;

		public RelicEditorTickbox(Control tickboxVisuals, Label label, bool initialTicked)
		{
			Label = label;
			MouseFilter = MouseFilterEnum.Stop;
			FocusMode = FocusModeEnum.None;
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
			AddThemeConstantOverride("separation", 6);

			tickboxVisuals.MouseFilter = MouseFilterEnum.Ignore;
			tickboxVisuals.CustomMinimumSize = new Vector2(48f, 48f);
			tickboxVisuals.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
			tickboxVisuals.SizeFlagsVertical = SizeFlags.ShrinkBegin;
			tickboxVisuals.Scale = Vector2.One * 0.66f;
			tickboxVisuals.PivotOffset = new Vector2(24f, 24f);

			label.MouseFilter = MouseFilterEnum.Ignore;
			label.VerticalAlignment = VerticalAlignment.Center;
			label.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;

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
	}
}
