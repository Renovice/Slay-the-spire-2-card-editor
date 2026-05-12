using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;

namespace SlayTheSpire2Mod.CardEditor;

public partial class NRelicEditorPopup : Control
{
	private const string PopupName = "CardEditorRelicEditorPopup";

	private readonly Dictionary<string, SpinBox> _numberFields = new(StringComparer.Ordinal);
	private readonly Dictionary<string, CheckBox> _poolFields = new(StringComparer.Ordinal);

	private ModelId _relicId = ModelId.none;
	private TextureRect? _icon;
	private Label? _titleLabel;
	private Label? _descriptionLabel;
	private bool _built;

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

		Node? host = GetPopupHost();
		if (host != null && GodotObject.IsInstanceValid(host))
		{
			foreach (NRelicEditorPopup popup in host.GetChildren().OfType<NRelicEditorPopup>().ToArray())
			{
				popup.QueueFree();
			}
			host.AddChild(editor);
			host.MoveChild(editor, host.GetChildCount() - 1);
			Log.Info($"[CardEditor][RelicEditor] Popup added relic={relic.Id} host={host.Name}");
			return;
		}

		Log.Warn($"[CardEditor][RelicEditor] Could not find a valid host for relic editor popup relic={relic.Id}");
	}

	private static Node? GetPopupHost()
	{
		NGame? game = NGame.Instance;
		if (game == null || !GodotObject.IsInstanceValid(game))
		{
			return null;
		}

		try
		{
			return game.GetNodeOrNull<Control>("%InspectionContainer") ?? game;
		}
		catch
		{
			return game;
		}
	}

	public override void _Ready()
	{
		Build();
	}

	private void Build()
	{
		if (_built)
		{
			return;
		}
		_built = true;

		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		ZIndex = 2000;
		ZAsRelative = false;

		ColorRect dim = new()
		{
			Color = new Color(0f, 0f, 0f, 0.66f),
			MouseFilter = MouseFilterEnum.Stop,
			ZIndex = 0,
			ZAsRelative = false
		};
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dim);

		CenterContainer center = new()
		{
			MouseFilter = MouseFilterEnum.Ignore,
			ZIndex = 10,
			ZAsRelative = false
		};
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);

		Vector2 panelSize = new(980f, 660f);
		PanelContainer panel = new()
		{
			CustomMinimumSize = panelSize,
			Size = panelSize,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			SizeFlagsVertical = SizeFlags.ShrinkCenter
		};
		panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
		center.AddChild(panel);

		VBoxContainer root = new()
		{
			CustomMinimumSize = new Vector2(940f, 620f)
		};
		root.AddThemeConstantOverride("separation", 14);
		panel.AddChild(root);

		Label heading = CreateHeading("Relic Editor", 34);
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
		AddPoolSection(settings);

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

		RefreshPreviewFromUi();
	}

	private void AddNumberSection(VBoxContainer parent)
	{
		parent.AddChild(CreateHeading("Vanilla Numbers", 25));

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

			SpinBox field = new()
			{
				MinValue = -999999d,
				MaxValue = 999999d,
				Step = 1d,
				Value = (double)dynamicVar.BaseValue,
				CustomMinimumSize = new Vector2(150f, 44f),
				AllowGreater = true,
				AllowLesser = true
			};
			field.ValueChanged += _ => RefreshPreviewFromUi();
			row.AddChild(field);
			_numberFields[key] = field;
		}

		if (_numberFields.Count == 0)
		{
			parent.AddChild(CreateMutedLabel("This relic only exposes text variables, not editable numeric values."));
		}
	}

	private void AddPoolSection(VBoxContainer parent)
	{
		parent.AddChild(CreateHeading("Drop Pools", 25));

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
			CheckBox check = new()
			{
				Text = CardEditorRelicOverrides.GetPoolLabel(pool),
				ButtonPressed = selected.Contains(key),
				CustomMinimumSize = new Vector2(240f, 38f)
			};
			check.Toggled += _ => RefreshPreviewFromUi();
			grid.AddChild(check);
			_poolFields[key] = check;
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

		foreach ((string key, SpinBox field) in _numberFields)
		{
			if (preview.DynamicVars.TryGetValue(key, out DynamicVar? dynamicVar))
			{
				dynamicVar.BaseValue = (decimal)field.Value;
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
				_titleLabel.Text = preview.Title.GetFormattedText();
			}
			if (_descriptionLabel != null)
			{
				string rarity = preview.Rarity.ToString();
				string pools = string.Join(", ", GetSelectedPoolKeys(preview)
					.Select(key => CardEditorRelicOverrides.EditablePools().FirstOrDefault(pool => CardEditorRelicOverrides.GetPoolKey(pool) == key))
					.Where(pool => pool != null)
					.Select(pool => CardEditorRelicOverrides.GetPoolLabel(pool!))
					.DefaultIfEmpty("No pool"));
				_descriptionLabel.Text = $"{rarity}\n{pools}\n\n{preview.DynamicDescription.GetFormattedText()}";
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][RelicEditor] Failed refreshing relic preview: {ex}");
		}
	}

	private void ApplyAndClose()
	{
		RelicModel? canonical = GetCanonicalRelic();
		if (canonical == null)
		{
			Close();
			return;
		}

		RelicOverride overrideData = new();

		Dictionary<string, decimal> numbers = new(StringComparer.Ordinal);
		foreach ((string key, SpinBox field) in _numberFields)
		{
			if (!canonical.DynamicVars.TryGetValue(key, out DynamicVar? vanillaVar))
			{
				continue;
			}

			decimal value = (decimal)field.Value;
			if (value != vanillaVar.BaseValue)
			{
				numbers[key] = value;
			}
		}
		if (numbers.Count > 0)
		{
			overrideData.DynamicVarBaseValues = numbers;
		}

		HashSet<string> selectedPools = GetSelectedPoolKeys(canonical);
		HashSet<string> vanillaPools = CardEditorRelicOverrides.GetVanillaPoolKeys(canonical);
		if (!selectedPools.SetEquals(vanillaPools))
		{
			overrideData.PoolKeys = selectedPools;
		}

		CardEditorRelicOverrides.SetAndSave(canonical.Id, overrideData);
		Close();
	}

	private void ResetRelicAndClose()
	{
		RelicModel? canonical = GetCanonicalRelic();
		if (canonical != null)
		{
			CardEditorRelicOverrides.SetAndSave(canonical.Id, null);
		}
		Close();
	}

	private void Close()
	{
		QueueFree();
	}

	private HashSet<string> GetSelectedPoolKeys(RelicModel relic)
	{
		if (_poolFields.Count == 0)
		{
			return CardEditorRelicOverrides.GetEffectivePoolKeys(relic);
		}

		return _poolFields
			.Where(kvp => kvp.Value.ButtonPressed)
			.Select(kvp => kvp.Key)
			.ToHashSet(StringComparer.Ordinal);
	}

	private static Label CreateHeading(string text, int size)
	{
		Label label = new()
		{
			Text = text,
			Modulate = new Color(1f, 0.84f, 0.28f, 1f)
		};
		label.AddThemeFontSizeOverride("font_size", size);
		return label;
	}

	private static Label CreateBodyLabel(string text)
	{
		Label label = new()
		{
			Text = text,
			Modulate = Colors.White
		};
		label.AddThemeFontSizeOverride("font_size", 20);
		return label;
	}

	private static Label CreateMutedLabel(string text)
	{
		Label label = CreateBodyLabel(text);
		label.Modulate = new Color(0.75f, 0.75f, 0.75f, 1f);
		return label;
	}

	private static Button CreateButton(string text)
	{
		Button button = new()
		{
			Text = text,
			CustomMinimumSize = new Vector2(150f, 48f)
		};
		button.AddThemeFontSizeOverride("font_size", 20);
		return button;
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
}
