using System;
using System.IO;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorBaseDeckBookmarkHooks
{
	private const string BookmarkNodeName = "CardEditorBaseDeckBookmark";
	private const string HoverMetaKey = "card_editor_hovered";
	private const string PendingDeferredSyncMetaKey = "card_editor_pending_deferred_sync";
	private const string LabelNodeName = "Label";
	private const string OutlineNodeName = "Outline";
	private const string OutlineFadeDriverNodeName = "OutlineFadeDriver";
	private const string MoveDriverNodeName = "MoveDriver";
	private const string ImageNodeName = "Image";
	private const string HitboxNodeName = "Hitbox";
	private const string BackButtonTexturePath = "res://images/atlases/ui_atlas.sprites/back_button.tres";
	private const string BackButtonOutlineTexturePath = "res://images/atlases/compressed.sprites/back_button_outline.tres";
	private const string LooseAssetsFolderName = "assets";
	private const string CustomBaseTextureOverrideFileName = "base_deck_button_texture_override.png";
	private const string LegacyRedTailFileName = "RedTail.png";
	private const string CustomOutlineTextureOverrideFileName = "base_deck_button_outline_override.png";
	private const string LegacyWhiteBackgroundFileName = "White background.png";
	private const string SharedAdditiveMaterialPath = "res://themes/canvas_item_material_additive_shared.tres";
	private const string HsvShaderPath = "res://shaders/hsv.gdshader";
	private const string LabelFontPath = "res://themes/kreon_bold_glyph_space_two.tres";

	private static Texture2D? _buttonTexture;
	private static Texture2D? _outlineTexture;
	private static Material? _outlineMaterial;
	private static Shader? _hsvShader;
	private static Font? _labelFont;
	private static readonly Vector2 BookmarkHideOffset = new(-180f, 0f);
	private static readonly FieldInfo? BackButtonShowPositionField = typeof(NBackButton).GetField("_showPos", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly float VisibleTopExtent = MathF.Min(CardEditorBaseDeckBookmarkLayout.ShadowTop, MathF.Min(CardEditorBaseDeckBookmarkLayout.OutlineTop, CardEditorBaseDeckBookmarkLayout.ImageTop));
	private static readonly float VisibleBottomExtent = MathF.Max(CardEditorBaseDeckBookmarkLayout.ShadowBottom, MathF.Max(CardEditorBaseDeckBookmarkLayout.OutlineBottom, CardEditorBaseDeckBookmarkLayout.ImageBottom));

	private static string FormatVector(Vector2 value)
	{
		return $"({value.X:0.##},{value.Y:0.##})";
	}

	private static string DescribeBookmark(Control? bookmark)
	{
		if (bookmark == null || !GodotObject.IsInstanceValid(bookmark))
		{
			return "bookmark=null";
		}

		CardEditorBookmarkMoveDriver? moveDriver = bookmark.GetNodeOrNull<CardEditorBookmarkMoveDriver>(MoveDriverNodeName);
		bool hovered = bookmark.HasMeta(HoverMetaKey) && bookmark.GetMeta(HoverMetaKey).AsBool();
		return $"bookmarkVisible={bookmark.Visible}/{bookmark.IsVisibleInTree()} hovered={hovered} scale={FormatVector(bookmark.Scale)} pos={FormatVector(bookmark.GlobalPosition)} move={moveDriver?.DescribeState() ?? "none"}";
	}

	private static string DescribeBackButton(NBackButton? backButton)
	{
		if (backButton == null || !GodotObject.IsInstanceValid(backButton))
		{
			return "backButton=null";
		}

		Vector2 mousePos = backButton.GetViewport()?.GetMousePosition() ?? Vector2.Zero;
		bool mouseOver = backButton.GetGlobalRect().HasPoint(mousePos);
		return $"backVisible={backButton.Visible}/{backButton.IsVisibleInTree()} backPos={FormatVector(backButton.GlobalPosition)} mouse={FormatVector(mousePos)} mouseOver={mouseOver}";
	}

	private static void DebugLog(string eventName, NCardLibrary? library = null, Control? bookmark = null, NBackButton? backButton = null)
	{
		string libraryText = library == null || !GodotObject.IsInstanceValid(library)
			? "library=null"
			: $"libraryVisible={library.Visible}/{library.IsVisibleInTree()} mode={CardEditorUiState.Mode}";
		Log.Info($"[CardEditor][BaseDeckBookmarkDebug] {eventName} {libraryText} {DescribeBackButton(backButton)} {DescribeBookmark(bookmark)}");
	}

	public static void Sync(NCardLibrary library)
	{
		if (library == null)
		{
			return;
		}

		Control? bookmark = library.FindChild(BookmarkNodeName, recursive: true, owned: false) as Control;
		bool shouldShow = CardEditorUiState.IsEditorActive || CardEditorUiState.IsBaseDeckActive || CardEditorUiState.IsBaseDeckAddActive;
		Log.Info($"[CardEditor][BaseDeckBookmark] Sync mode={CardEditorUiState.Mode} shouldShow={shouldShow} bookmarkExists={bookmark != null}");
		if (!shouldShow)
		{
			if (bookmark != null)
			{
				HideBookmark(bookmark);
				DebugLog("Sync:hidden", library, bookmark);
			}
			CardEditorBaseDeckBookmarkTunerHooks.Sync(library);
			return;
		}

		Control? backButton = library.GetNodeOrNull<Control>("%BackButton");
		if (backButton == null)
		{
			return;
		}

		if (bookmark == null)
		{
			bookmark = Create(library);
			library.AddChild(bookmark);
		}
		else if (bookmark.GetParent() != library)
		{
			Node? previousParent = bookmark.GetParent();
			previousParent?.RemoveChild(bookmark);
			library.AddChild(bookmark);
		}

		bookmark.SetMeta(PendingDeferredSyncMetaKey, false);
		ApplyRuntimeLayout(bookmark);
		UpdateState(library, bookmark);
		DebugLog("Sync:shown", library, bookmark, backButton as NBackButton);
		CardEditorBaseDeckBookmarkTunerHooks.Sync(library);
	}

	public static void RefreshLastLibrary()
	{
		if (CardEditorUiState.TryGetLastLibrary(out NCardLibrary? library) && library != null)
		{
			Sync(library);
		}
	}

	public static void ForceReset(NCardLibrary library)
	{
		if (library == null)
		{
			return;
		}

		Control? bookmark = library.FindChild(BookmarkNodeName, recursive: true, owned: false) as Control;
		if (bookmark == null)
		{
			return;
		}

		bookmark.SetMeta(HoverMetaKey, false);
		ApplyImmediateVisualState(bookmark, selected: false, hovered: false);
		CardEditorBookmarkMoveDriver? moveDriver = bookmark.GetNodeOrNull<CardEditorBookmarkMoveDriver>(MoveDriverNodeName);
		moveDriver?.ForceResetHidden();
		DebugLog("ForceReset", library, bookmark);
	}

	public static void SyncDeferred(NCardLibrary library)
	{
		if (library == null || !GodotObject.IsInstanceValid(library))
		{
			return;
		}

		Callable.From(() =>
		{
			if (!GodotObject.IsInstanceValid(library))
			{
				return;
			}

			Sync(library);
		}).CallDeferred();
	}

	public static void NotifyBackButtonEnabled(NBackButton backButton)
	{
		if (backButton == null || !TryGetOwningLibrary(backButton, out NCardLibrary? library) || library == null)
		{
			return;
		}

		Control? bookmark = library.FindChild(BookmarkNodeName, recursive: true, owned: false) as Control;
		if (bookmark != null)
		{
			ResetVisualStateForMenuEnable(bookmark);
		}

		CardEditorBookmarkMoveDriver? moveDriver = bookmark?.GetNodeOrNull<CardEditorBookmarkMoveDriver>(MoveDriverNodeName);
		moveDriver?.NotifyBackButtonEnabled();
		DebugLog("NotifyBackButtonEnabled", library, bookmark, backButton);
	}

	public static void NotifyBackButtonDisabled(NBackButton backButton)
	{
		if (backButton == null || !TryGetOwningLibrary(backButton, out NCardLibrary? library) || library == null)
		{
			return;
		}

		Control? bookmark = library.FindChild(BookmarkNodeName, recursive: true, owned: false) as Control;
		if (bookmark != null)
		{
			ApplyVisualStateForMenuDisable(bookmark);
		}

		CardEditorBookmarkMoveDriver? moveDriver = bookmark?.GetNodeOrNull<CardEditorBookmarkMoveDriver>(MoveDriverNodeName);
		moveDriver?.NotifyBackButtonDisabled();
		DebugLog("NotifyBackButtonDisabled", library, bookmark, backButton);
	}

	public static void LogBackButtonPressed(NBackButton backButton)
	{
		if (backButton == null || !TryGetOwningLibrary(backButton, out NCardLibrary? library) || library == null)
		{
			return;
		}

		Control? bookmark = library.FindChild(BookmarkNodeName, recursive: true, owned: false) as Control;
		DebugLog("BackButtonPressed", library, bookmark, backButton);
	}

	public static void LogLibraryLifecycle(string eventName, NCardLibrary library)
	{
		Control? bookmark = library?.FindChild(BookmarkNodeName, recursive: true, owned: false) as Control;
		NBackButton? backButton = library?.GetNodeOrNull<NBackButton>("%BackButton");
		DebugLog(eventName, library, bookmark, backButton);
	}

	private static Control Create(NCardLibrary library)
	{
		_buttonTexture ??= TryLoadLooseTextureOverride("base deck texture",
		[
			CustomBaseTextureOverrideFileName,
			LegacyRedTailFileName
		]) ?? GD.Load<Texture2D>(BackButtonTexturePath);
		_outlineTexture ??= TryLoadLooseTextureOverride("base deck outline",
		[
			CustomOutlineTextureOverrideFileName,
			LegacyWhiteBackgroundFileName
		]) ?? GD.Load<Texture2D>(BackButtonOutlineTexturePath);
		_outlineMaterial ??= GD.Load<Material>(SharedAdditiveMaterialPath);
		_hsvShader ??= GD.Load<Shader>(HsvShaderPath);
		_labelFont ??= GD.Load<Font>(LabelFontPath);

		Control root = new()
		{
			Name = BookmarkNodeName,
			TopLevel = true,
			ZAsRelative = false,
			ZIndex = 55,
			MouseFilter = Control.MouseFilterEnum.Stop,
			FocusMode = Control.FocusModeEnum.None,
			CustomMinimumSize = new Vector2(CardEditorBaseDeckBookmarkTuning.Width, CardEditorBaseDeckBookmarkTuning.Height),
			Size = new Vector2(CardEditorBaseDeckBookmarkTuning.Width, CardEditorBaseDeckBookmarkTuning.Height),
			PivotOffset = new Vector2(CardEditorBaseDeckBookmarkTuning.Width * 0.5f, CardEditorBaseDeckBookmarkTuning.Height * 0.5f)
		};

		TextureRect shadow = new()
		{
			Name = "Shadow",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Texture = _buttonTexture,
			ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
		};
		shadow.Modulate = new Color(0f, 0f, 0f, 0.25f);
		shadow.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		shadow.OffsetLeft = CardEditorBaseDeckBookmarkLayout.ShadowLeft;
		shadow.OffsetTop = CardEditorBaseDeckBookmarkLayout.ShadowTop;
		shadow.OffsetRight = CardEditorBaseDeckBookmarkLayout.ShadowRight;
		shadow.OffsetBottom = CardEditorBaseDeckBookmarkLayout.ShadowBottom;
		root.AddChild(shadow);

		TextureRect outline = new()
		{
			Name = OutlineNodeName,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Texture = _outlineTexture,
			ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Visible = true,
			Material = _outlineMaterial
		};
		outline.Modulate = new Color(0.52f, 0.95f, 0.95f, 0f);
		outline.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		outline.OffsetLeft = CardEditorBaseDeckBookmarkLayout.OutlineLeft;
		outline.OffsetTop = CardEditorBaseDeckBookmarkLayout.OutlineTop;
		outline.OffsetRight = CardEditorBaseDeckBookmarkLayout.OutlineRight;
		outline.OffsetBottom = CardEditorBaseDeckBookmarkLayout.OutlineBottom;
		root.AddChild(outline);

		CardEditorBookmarkOutlineFadeDriver outlineFadeDriver = new()
		{
			Name = OutlineFadeDriverNodeName
		};
		outlineFadeDriver.Bind(root, outline);
		root.AddChild(outlineFadeDriver);

		CardEditorBookmarkMoveDriver moveDriver = new()
		{
			Name = MoveDriverNodeName
		};
		moveDriver.Bind(root, library);
		root.AddChild(moveDriver);

		TextureRect image = new()
		{
			Name = ImageNodeName,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Texture = _buttonTexture,
			ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Material = CreateBookmarkImageMaterial()
		};
		image.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		image.OffsetLeft = CardEditorBaseDeckBookmarkLayout.ImageLeft;
		image.OffsetTop = CardEditorBaseDeckBookmarkLayout.ImageTop;
		image.OffsetRight = CardEditorBaseDeckBookmarkLayout.ImageRight;
		image.OffsetBottom = CardEditorBaseDeckBookmarkLayout.ImageBottom;
		root.AddChild(image);

		Label label = new()
		{
			Name = LabelNodeName,
			Text = CardEditorLoc.T("button.baseDeck", "Base Deck"),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		label.OffsetLeft = CardEditorBaseDeckBookmarkLayout.LabelLeft;
		label.OffsetTop = CardEditorBaseDeckBookmarkLayout.LabelTop;
		label.OffsetRight = CardEditorBaseDeckBookmarkLayout.LabelRight;
		label.OffsetBottom = CardEditorBaseDeckBookmarkLayout.LabelBottom;
		if (_labelFont != null)
		{
			label.AddThemeFontOverride("font", _labelFont);
		}
		label.AddThemeFontSizeOverride("font_size", CardEditorBaseDeckBookmarkLayout.LabelFontSize);
		label.AddThemeColorOverride("font_color", StsColors.cream);
		label.AddThemeColorOverride("font_outline_color", StsColors.transparentBlack);
		label.AddThemeConstantOverride("outline_size", 10);
		root.AddChild(label);

		Button hitbox = new()
		{
			Name = HitboxNodeName,
			Flat = true,
			FocusMode = Control.FocusModeEnum.None,
			MouseFilter = Control.MouseFilterEnum.Stop,
			TooltipText = CardEditorLoc.T("baseDeck.bookmarkTooltip", "Open starter deck editor")
		};
		hitbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		hitbox.OffsetLeft = CardEditorBaseDeckBookmarkLayout.HitboxLeft;
		hitbox.OffsetTop = CardEditorBaseDeckBookmarkLayout.HitboxTop;
		hitbox.OffsetRight = CardEditorBaseDeckBookmarkLayout.HitboxRight;
		hitbox.OffsetBottom = CardEditorBaseDeckBookmarkLayout.HitboxBottom;
		MakeButtonInvisible(hitbox);
		hitbox.MouseEntered += () => SetHovered(root, hovered: true);
		hitbox.MouseExited += () => SetHovered(root, hovered: false);
		hitbox.Pressed += () => OnPressed(library);
		root.AddChild(hitbox);

		root.SetMeta(HoverMetaKey, false);
		root.Visible = false;
		ApplyRuntimeLayout(root);
		ApplyVisualState(root, selected: false, hovered: false);
		return root;
	}

	private static void ApplyRuntimeLayout(Control bookmark)
	{
		float width = CardEditorBaseDeckBookmarkTuning.Width;
		float height = CardEditorBaseDeckBookmarkTuning.Height;
		bookmark.CustomMinimumSize = new Vector2(width, height);
		bookmark.Size = new Vector2(width, height);
		bookmark.PivotOffset = new Vector2(width * 0.5f, height * 0.5f);
		ApplyRuntimeOutlineLayout(bookmark);
		ApplyRuntimeLabelLayout(bookmark);
	}

	private static void ApplyRuntimeOutlineLayout(Control bookmark)
	{
		TextureRect? outline = bookmark.GetNodeOrNull<TextureRect>(OutlineNodeName);
		if (outline == null)
		{
			return;
		}

		outline.OffsetLeft = CardEditorBaseDeckBookmarkTuning.OutlineLeft;
		outline.OffsetTop = CardEditorBaseDeckBookmarkTuning.OutlineTop;
		outline.OffsetRight = CardEditorBaseDeckBookmarkTuning.OutlineRight;
		outline.OffsetBottom = CardEditorBaseDeckBookmarkTuning.OutlineBottom;
	}

	private static void ApplyRuntimeLabelLayout(Control bookmark)
	{
		Label? label = bookmark.GetNodeOrNull<Label>(LabelNodeName);
		if (label == null)
		{
			return;
		}

		label.OffsetLeft = CardEditorBaseDeckBookmarkLayout.LabelLeft + CardEditorBaseDeckBookmarkTuning.LabelOffsetX;
		label.OffsetTop = CardEditorBaseDeckBookmarkLayout.LabelTop + CardEditorBaseDeckBookmarkTuning.LabelOffsetY;
		label.OffsetRight = CardEditorBaseDeckBookmarkLayout.LabelRight + CardEditorBaseDeckBookmarkTuning.LabelOffsetX;
		label.OffsetBottom = CardEditorBaseDeckBookmarkLayout.LabelBottom + CardEditorBaseDeckBookmarkTuning.LabelOffsetY;
	}

	private static void MakeButtonInvisible(Button button)
	{
		StyleBoxEmpty empty = new();
		button.AddThemeStyleboxOverride("normal", empty);
		button.AddThemeStyleboxOverride("hover", empty);
		button.AddThemeStyleboxOverride("pressed", empty);
		button.AddThemeStyleboxOverride("disabled", empty);
		button.AddThemeStyleboxOverride("focus", empty);
	}

	private static Texture2D? TryLoadLooseTextureOverride(string label, string[] fileNames)
	{
		try
		{
			string? modDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			if (string.IsNullOrWhiteSpace(modDir))
			{
				return null;
			}

			string assetsDir = Path.Combine(modDir, LooseAssetsFolderName);
			foreach (string fileName in fileNames)
			{
				string candidate = Path.Combine(assetsDir, fileName);
				if (!File.Exists(candidate))
				{
					continue;
				}

				Image? image = TryLoadImageFromFile(candidate);
				if (image == null || image.IsEmpty())
				{
					Log.Warn($"[CardEditor] Failed loading {label} override '{candidate}'.");
					continue;
				}

				Log.Info($"[CardEditor] Loaded {label} override: {candidate}");
				return ImageTexture.CreateFromImage(image);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed resolving {label} override: {ex}");
		}

		return null;
	}

	private static Image? TryLoadImageFromFile(string absolutePath)
	{
		try
		{
			string normalizedPath = absolutePath.Replace('\\', '/');
			return Image.LoadFromFile(normalizedPath);
		}
		catch
		{
			return null;
		}
	}

	private static ShaderMaterial CreateBookmarkImageMaterial()
	{
		ShaderMaterial imageMaterial = new()
		{
			Shader = _hsvShader
		};
		imageMaterial.SetShaderParameter("h", 0.44f);
		imageMaterial.SetShaderParameter("s", 1.1f);
		imageMaterial.SetShaderParameter("v", 0.92f);
		return imageMaterial;
	}

	private static void SetHovered(Control root, bool hovered)
	{
		root.SetMeta(HoverMetaKey, hovered);
		bool selected = CardEditorUiState.IsBaseDeckActive || CardEditorUiState.IsBaseDeckAddActive;
		ApplyVisualState(root, selected, hovered);
	}

	private static void OnPressed(NCardLibrary library)
	{
		if (CardEditorUiState.IsBaseDeckActive || CardEditorUiState.IsBaseDeckAddActive)
		{
			return;
		}

		if (CardEditorBaseDeckLibraryHelper.TryGetSelectedCharacterFromLibrary(library, out ModelId selected)
			&& selected != null
			&& selected != ModelId.none)
		{
			CardEditorBaseDeckUiState.SetEditingCharacter(selected);
		}

		CardEditorBaseDeckUiState.EnterBaseDeckMode();
	}

	private static void UpdateState(NCardLibrary library, Control bookmark)
	{
		if (!TryResolvePositions(library, out Vector2 showPosition, out Vector2 hidePosition))
		{
			bookmark.Visible = false;
			ScheduleDeferredSync(library, bookmark);
			return;
		}
		ApplyMoveState(bookmark, showPosition, hidePosition, shouldShow: true);
		bool selected = CardEditorUiState.IsBaseDeckActive || CardEditorUiState.IsBaseDeckAddActive;
		bool hovered = bookmark.HasMeta(HoverMetaKey) && bookmark.GetMeta(HoverMetaKey).AsBool();
		ApplyVisualState(bookmark, selected, hovered);
	}

	private static void ResetVisualStateForMenuEnable(Control root)
	{
		root.SetMeta(HoverMetaKey, false);
		bool selected = CardEditorUiState.IsBaseDeckActive || CardEditorUiState.IsBaseDeckAddActive;
		ApplyImmediateVisualState(root, selected, hovered: false);
	}

	private static void ApplyVisualStateForMenuDisable(Control root)
	{
		root.SetMeta(HoverMetaKey, false);
		bool selected = CardEditorUiState.IsBaseDeckActive || CardEditorUiState.IsBaseDeckAddActive;
		ApplyVisualState(root, selected, hovered: false);
	}

	private static void ApplyImmediateVisualState(Control root, bool selected, bool hovered)
	{
		TextureRect? outline = root.GetNodeOrNull<TextureRect>(OutlineNodeName);
		CardEditorBookmarkOutlineFadeDriver? outlineFadeDriver = root.GetNodeOrNull<CardEditorBookmarkOutlineFadeDriver>(OutlineFadeDriverNodeName);
		Label? label = root.GetNodeOrNull<Label>(LabelNodeName);
		if (outline == null || label == null)
		{
			return;
		}

		Color outlineColor = selected ? StsColors.gold : new Color(0.52f, 0.95f, 0.95f, 0.82f);
		bool shouldShowOutline = selected || hovered;
		if (outlineFadeDriver != null)
		{
			outlineFadeDriver.Reset(outlineColor, shouldShowOutline);
		}
		else
		{
			root.Scale = Vector2.One;
			outline.Modulate = shouldShowOutline
				? outlineColor
				: new Color(outlineColor.R, outlineColor.G, outlineColor.B, 0f);
		}

		label.Modulate = selected
			? new Color(1f, 0.97f, 0.90f, 1f)
			: hovered
				? new Color(1f, 0.95f, 0.82f, 1f)
				: new Color(0.98f, 0.92f, 0.82f, 1f);
	}

	private static void ApplyVisualState(Control root, bool selected, bool hovered)
	{
		TextureRect? outline = root.GetNodeOrNull<TextureRect>(OutlineNodeName);
		CardEditorBookmarkOutlineFadeDriver? outlineFadeDriver = root.GetNodeOrNull<CardEditorBookmarkOutlineFadeDriver>(OutlineFadeDriverNodeName);
		TextureRect? image = root.GetNodeOrNull<TextureRect>(ImageNodeName);
		Label? label = root.GetNodeOrNull<Label>(LabelNodeName);
		if (outline == null || image == null || label == null)
		{
			return;
		}

		bool tunerOpen = TryGetOwningLibrary(root, out NCardLibrary? library) && library != null && CardEditorBaseDeckBookmarkTunerHooks.IsOpen(library);
		bool shouldShowOutline = selected || hovered || tunerOpen;
		Color outlineColor = selected ? StsColors.gold : new Color(0.52f, 0.95f, 0.95f, 0.82f);
		if (outlineFadeDriver != null)
		{
			outlineFadeDriver.SetState(outlineColor, shouldShowOutline, hovered);
		}
		else
		{
			outline.Visible = shouldShowOutline;
			outline.Modulate = shouldShowOutline ? outlineColor : new Color(outlineColor.R, outlineColor.G, outlineColor.B, 0f);
		}
		label.Modulate = selected
			? new Color(1f, 0.97f, 0.90f, 1f)
			: hovered
				? new Color(1f, 0.95f, 0.82f, 1f)
				: new Color(0.98f, 0.92f, 0.82f, 1f);
	}

	private static bool TryGetOwningLibrary(Node root, out NCardLibrary? library)
	{
		Node? current = root;
		while (current != null)
		{
			if (current is NCardLibrary currentLibrary)
			{
				library = currentLibrary;
				return true;
			}

			current = current.GetParent();
		}

		library = null;
		return false;
	}

	private static void ScheduleDeferredSync(NCardLibrary library, Control bookmark)
	{
		if (library == null || bookmark == null || !GodotObject.IsInstanceValid(library) || !GodotObject.IsInstanceValid(bookmark))
		{
			return;
		}

		if (bookmark.HasMeta(PendingDeferredSyncMetaKey) && bookmark.GetMeta(PendingDeferredSyncMetaKey).AsBool())
		{
			return;
		}

		bookmark.SetMeta(PendingDeferredSyncMetaKey, true);
		Callable.From(() =>
		{
			if (!GodotObject.IsInstanceValid(library))
			{
				return;
			}

			Sync(library);
		}).CallDeferred();
	}

	private static void ApplyMoveState(Control bookmark, Vector2 showPosition, Vector2 hidePosition, bool shouldShow)
	{
		CardEditorBookmarkMoveDriver? moveDriver = bookmark.GetNodeOrNull<CardEditorBookmarkMoveDriver>(MoveDriverNodeName);
		if (moveDriver != null)
		{
			moveDriver.SetState(showPosition, hidePosition, shouldShow);
			return;
		}

		bookmark.Visible = shouldShow;
		bookmark.GlobalPosition = shouldShow ? showPosition : hidePosition;
	}

	private static void HideBookmark(Control bookmark)
	{
		CardEditorBookmarkMoveDriver? moveDriver = bookmark.GetNodeOrNull<CardEditorBookmarkMoveDriver>(MoveDriverNodeName);
		if (moveDriver != null)
		{
			moveDriver.Hide();
			return;
		}

		bookmark.Visible = false;
	}

	private static bool TryResolvePositions(NCardLibrary library, out Vector2 showPosition, out Vector2 hidePosition)
	{
		try
		{
			Control? backButton = library.GetNodeOrNull<Control>("%BackButton");
			if (backButton == null)
			{
				showPosition = Vector2.Zero;
				hidePosition = Vector2.Zero;
				return false;
			}

			Rect2 backRect = backButton.GetGlobalRect();
			Vector2 backSize = backButton.Size;
			if (backRect.Size.Y <= 0f || backSize.Y <= 0f)
			{
				showPosition = Vector2.Zero;
				hidePosition = Vector2.Zero;
				return false;
			}

			Vector2 backAnchorPosition = ResolveBackButtonShowGlobalPosition(backButton, backRect);
			float desiredLeft = CardEditorBaseDeckBookmarkTuning.PositionXOffset;
			float desiredTop = VisibleBottomExtent + CardEditorBaseDeckBookmarkTuning.PositionYOffset - VisibleTopExtent;
			showPosition = backAnchorPosition + new Vector2(desiredLeft, desiredTop);
			hidePosition = showPosition + BookmarkHideOffset;
			return true;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed positioning base deck bookmark: {ex}");
			showPosition = Vector2.Zero;
			hidePosition = Vector2.Zero;
			return false;
		}
	}

	private static Vector2 ResolveBackButtonShowGlobalPosition(Control backButton, Rect2 backRect)
	{
		if (backButton is NBackButton vanillaBackButton
			&& BackButtonShowPositionField != null
			&& BackButtonShowPositionField.GetValue(vanillaBackButton) is Vector2 showLocalPosition)
		{
			if (vanillaBackButton.GetParent() is Control parentControl)
			{
				return parentControl.GlobalPosition + showLocalPosition;
			}

			return showLocalPosition;
		}

		return backRect.Position;
	}
}

internal partial class CardEditorBookmarkOutlineFadeDriver : Node
{
	private static readonly Vector2 HoverScale = Vector2.One * 1.05f;
	private const double FadeInDurationSeconds = 0.05;
	private const double FadeOutDurationSeconds = 0.5;

	private Control? _root;
	private TextureRect? _outline;
	private Tween? _fadeTween;

	public void Bind(Control root, TextureRect outline)
	{
		_root = root;
		_outline = outline;
	}

	public void Reset(Color targetColor, bool shouldShow)
	{
		if (_root == null || !GodotObject.IsInstanceValid(_root) || _outline == null || !GodotObject.IsInstanceValid(_outline))
		{
			return;
		}

		_fadeTween?.Kill();
		_fadeTween = null;
		_root.Scale = Vector2.One;
		_outline.Modulate = shouldShow
			? targetColor
			: new Color(targetColor.R, targetColor.G, targetColor.B, 0f);
	}

	public void SetState(Color targetColor, bool shouldShow, bool hovered)
	{
		if (_root == null || !GodotObject.IsInstanceValid(_root) || _outline == null || !GodotObject.IsInstanceValid(_outline))
		{
			return;
		}

		_fadeTween?.Kill();
		_fadeTween = null;

		if (hovered)
		{
			_fadeTween = CreateTween().SetParallel();
			_fadeTween.TweenProperty(_root, "scale", HoverScale, FadeInDurationSeconds);
			_fadeTween.TweenProperty(_outline, "modulate", targetColor, FadeInDurationSeconds);
			return;
		}

		if (shouldShow)
		{
			_fadeTween = CreateTween().SetParallel();
			_fadeTween.TweenProperty(_root, "scale", HoverScale, FadeInDurationSeconds);
			_fadeTween.TweenProperty(_outline, "modulate", targetColor, FadeInDurationSeconds);
			return;
		}

		Color transparentTarget = new(targetColor.R, targetColor.G, targetColor.B, 0f);
		_fadeTween = CreateTween().SetParallel();
		_fadeTween.TweenProperty(_root, "scale", HoverScale, FadeOutDurationSeconds)
			.SetTrans(Tween.TransitionType.Expo)
			.SetEase(Tween.EaseType.Out);
		_fadeTween.TweenProperty(_outline, "modulate", transparentTarget, FadeOutDurationSeconds);
	}
}

internal partial class CardEditorBookmarkMoveDriver : Node
{
	private const double MoveDurationSeconds = 0.35;

	private Control? _root;
	private Tween? _moveTween;
	private bool _backButtonEnabled;
	private bool _ownerVisible;
	private bool _logicalShouldShow;
	private bool _isShown;
	private bool _hasResolvedPositions;
	private Vector2 _showPosition;
	private Vector2 _hidePosition;

	private static string FormatVector(Vector2 value)
	{
		return $"({value.X:0.##},{value.Y:0.##})";
	}

	public string DescribeState()
	{
		return $"logical={_logicalShouldShow} enabled={_backButtonEnabled} ownerVisible={_ownerVisible} shown={_isShown} hasPos={_hasResolvedPositions} show={FormatVector(_showPosition)} hide={FormatVector(_hidePosition)}";
	}

	private void DebugLog(string eventName)
	{
		if (_root == null || !GodotObject.IsInstanceValid(_root))
		{
			Log.Info($"[CardEditor][BaseDeckBookmarkDebug] MoveDriver:{eventName} root=null state={DescribeState()}");
			return;
		}

		Log.Info($"[CardEditor][BaseDeckBookmarkDebug] MoveDriver:{eventName} rootVisible={_root.Visible}/{_root.IsVisibleInTree()} rootPos={FormatVector(_root.GlobalPosition)} {DescribeState()}");
	}

	public void Bind(Control root, CanvasItem owner)
	{
		_root = root;
		_ownerVisible = owner.Visible;
		owner.Connect(CanvasItem.SignalName.VisibilityChanged, Callable.From(() => OnOwnerVisibilityChanged(owner.Visible)));
		DebugLog("Bind");
	}

	public void SetState(Vector2 showPosition, Vector2 hidePosition, bool shouldShow)
	{
		if (_root == null || !GodotObject.IsInstanceValid(_root))
		{
			return;
		}

		bool positionsChanged = !_hasResolvedPositions
			|| !_showPosition.IsEqualApprox(showPosition)
			|| !_hidePosition.IsEqualApprox(hidePosition);
		_showPosition = showPosition;
		_hidePosition = hidePosition;
		_hasResolvedPositions = true;
		_logicalShouldShow = shouldShow;
		DebugLog($"SetState:start positionsChanged={positionsChanged} shouldShow={shouldShow}");

		if (shouldShow)
		{
			if (!_ownerVisible || !_backButtonEnabled)
			{
				_moveTween?.Kill();
				_root.GlobalPosition = hidePosition;
				_root.Visible = false;
				_isShown = false;
				DebugLog("SetState:blockedHide");
				return;
			}

			if (!_isShown)
			{
				StartShowAnimation();
				return;
			}

			if (positionsChanged)
			{
				_moveTween?.Kill();
				_root.Visible = true;
				_root.GlobalPosition = showPosition;
				DebugLog("SetState:snapShow");
			}

			return;
		}

		if (!_ownerVisible || !_isShown)
		{
			_moveTween?.Kill();
			if (positionsChanged)
			{
				_root.GlobalPosition = hidePosition;
			}
			_root.Visible = false;
			_isShown = false;
			DebugLog("SetState:hideImmediate");
			return;
		}

		_moveTween?.Kill();
		_root.Visible = true;
		_moveTween = CreateTween();
		_moveTween.TweenProperty(_root, "global_position", hidePosition, MoveDurationSeconds)
			.SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Expo);
		_moveTween.TweenCallback(Callable.From(() =>
		{
			if (_root == null || !GodotObject.IsInstanceValid(_root) || _isShown)
			{
				return;
			}

			_root.Visible = false;
			DebugLog("SetState:hideTweenComplete");
		}));
		_isShown = false;
		DebugLog("SetState:hideTweenStarted");
	}

	public void Hide()
	{
		if (_root == null || !GodotObject.IsInstanceValid(_root))
		{
			return;
		}

		if (!_hasResolvedPositions)
		{
			_moveTween?.Kill();
			_root.Visible = false;
			_isShown = false;
			DebugLog("Hide:noPositions");
			return;
		}

		SetState(_showPosition, _hidePosition, shouldShow: false);
	}

	public void NotifyBackButtonEnabled()
	{
		_backButtonEnabled = true;
		DebugLog("NotifyBackButtonEnabled");
		if (_logicalShouldShow && _hasResolvedPositions && _ownerVisible)
		{
			StartShowAnimation();
		}
	}

	public void NotifyBackButtonDisabled()
	{
		_backButtonEnabled = false;
		DebugLog("NotifyBackButtonDisabled");
		if (_root == null || !GodotObject.IsInstanceValid(_root))
		{
			return;
		}

		if (!_hasResolvedPositions)
		{
			_moveTween?.Kill();
			_root.Visible = false;
			_isShown = false;
			DebugLog("NotifyBackButtonDisabled:noPositions");
			return;
		}

		StartHideAnimation();
	}

	public void ForceResetHidden()
	{
		if (_root == null || !GodotObject.IsInstanceValid(_root))
		{
			return;
		}

		_moveTween?.Kill();
		_backButtonEnabled = false;
		_logicalShouldShow = false;
		if (_hasResolvedPositions)
		{
			_root.GlobalPosition = _hidePosition;
		}

		_root.Visible = false;
		_isShown = false;
		DebugLog("ForceResetHidden");
	}

	private void OnOwnerVisibilityChanged(bool ownerVisible)
	{
		_ownerVisible = ownerVisible;
		DebugLog($"OnOwnerVisibilityChanged ownerVisible={ownerVisible}");
		if (_root == null || !GodotObject.IsInstanceValid(_root))
		{
			return;
		}

		if (!ownerVisible)
		{
			_moveTween?.Kill();
			if (_hasResolvedPositions)
			{
				_root.GlobalPosition = _hidePosition;
			}
			_root.Visible = false;
			_isShown = false;
			DebugLog("OnOwnerVisibilityChanged:hidden");
			return;
		}

		if (_logicalShouldShow && _hasResolvedPositions && _backButtonEnabled)
		{
			StartShowAnimation();
		}
	}

	private void StartShowAnimation()
	{
		if (_root == null || !GodotObject.IsInstanceValid(_root))
		{
			return;
		}

		_moveTween?.Kill();
		_root.Visible = true;
		_root.GlobalPosition = _hidePosition;
		_moveTween = CreateTween();
		_moveTween.TweenProperty(_root, "global_position", _showPosition, MoveDurationSeconds)
			.SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Back);
		_isShown = true;
		DebugLog("StartShowAnimation");
	}

	private void StartHideAnimation()
	{
		if (_root == null || !GodotObject.IsInstanceValid(_root))
		{
			return;
		}

		_moveTween?.Kill();
		_root.Visible = true;
		_moveTween = CreateTween();
		_moveTween.TweenProperty(_root, "global_position", _hidePosition, MoveDurationSeconds)
			.SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Expo);
		_moveTween.TweenCallback(Callable.From(() =>
		{
			if (_root == null || !GodotObject.IsInstanceValid(_root) || _backButtonEnabled)
			{
				return;
			}

			_root.Visible = false;
			DebugLog("StartHideAnimation:complete");
		}));
		_isShown = false;
		DebugLog("StartHideAnimation");
	}
}
