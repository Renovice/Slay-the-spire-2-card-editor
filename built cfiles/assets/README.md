# Base Deck Bookmark Assets

This folder contains the bookmark-related assets pulled together in one place so you can edit them manually.

Files:
- `base_deck_button_texture_override.png`
  - Loose override file that the live mod now prefers for the Base Deck body texture.
  - Current test asset source: `RedTAIL2.png`
- `base_deck_button_outline_override.png`
  - Loose override file that the live mod now prefers for the Base Deck outline/glow shape.
  - Current test asset source: `White background.png`
- `back_button_base_texture.png`
  - Extracted from the game's back button atlas region.
  - Source atlas: `ui_atlas_1.png`
  - Region: `x=630 y=801 w=256 h=131`
  - Atlas margin: `left=0 top=26 right=44 bottom=71`
- `back_button_outline_texture.png`
  - Extracted outline used for the hover/selected glow.
  - Source atlas: `compressed_0.png`
  - Region: `x=824 y=1011 w=259 h=137`
  - Atlas margin: `left=0 top=23 right=41 bottom=65`
- `ui_atlas_1.png`
  - Full UI atlas that contains the base bookmark texture.
- `compressed_0.png`
  - Full compressed atlas that contains the outline texture.
- `hsv.gdshader`
  - Shader used to tint the base texture green in-game.
- `kreon_bold.ttf`
  - Font used by the bookmark label in the local preview scene.
- `base_deck_button_asset.tscn`
  - Small Godot scene that previews the bookmark pieces in isolation.
- `RedTail.png`
  - Original Photoshop export used for the current in-game test.
- `RedTAIL2.png`
  - Alternate Photoshop export that is now active for the current in-game test.
- `White background.png`
  - Reference/helper export from Photoshop.

Runtime usage notes:
- The visible green bookmark body is not a separate texture. It uses `back_button_base_texture.png` plus the HSV shader.
- The shadow is also not a separate texture. It uses the same base texture again, modulated black with alpha `0.25`.
- The outline uses the separate outline texture with the additive shared material.

Current shader values for the green bookmark preview:
- `h = 0.44`
- `s = 1.1`
- `v = 0.92`
