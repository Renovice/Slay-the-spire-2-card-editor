namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorBaseDeckBookmarkLayout
{
	public const float Width = 200f;
	public const float Height = 31f;

	public const float ShadowLeft = -9f;
	public const float ShadowTop = -1f;
	public const float ShadowRight = 58f;
	public const float ShadowBottom = 39f;

	public const float OutlineLeft = -24f;
	public const float OutlineTop = -16f;
	public const float OutlineRight = 49f;
	public const float OutlineBottom = 30f;

	public const float ImageLeft = -21f;
	public const float ImageTop = -13f;
	public const float ImageRight = 46f;
	public const float ImageBottom = 27f;

	public const float LabelLeft = 17f;
	public const float LabelTop = 12f;
	public const float LabelRight = -49f;
	public const float LabelBottom = -14f;
	public const int LabelFontSize = 22;

	public const float HitboxLeft = -21f;
	public const float HitboxTop = -13f;
	public const float HitboxRight = 46f;
	public const float HitboxBottom = 27f;

	// Horizontal placement is relative to the live back button root.
	public const float PositionXOffsetFromBackButton = -16f;
	// Vertical placement is relative to the live back button's visible bottom edge.
	public const float PositionYOffsetFromBackButton = 76f;
	public const float LabelOffsetXFromBookmark = 42f;
	public const float LabelOffsetYFromBookmark = -2f;
	public const float OutlineOffsetXFromBookmark = -1f;
	public const float OutlineOffsetYFromBookmark = 0f;
	public const float OutlineWidthAdjustFromBookmark = -5f;
	public const float OutlineHeightAdjustFromBookmark = -3f;
	public const bool ClampToBottomControls = false;
	public const float MinGapFromBottomControls = 10f;
}
