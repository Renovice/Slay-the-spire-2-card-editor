Card Editor Custom Shader Foils
================================

Status
------
This folder is the drop-in location for user shader foils.
The custom shader loader treats these as a separate "Custom Foil" lane.
They should not replace or modify built-in Card Editor finishes, and they should
not use the base-game/vanilla finish pipeline.

Put files here
--------------
Drop user foils into:

  Slay the Spire 2/mods/Card_editor/custom_foils/

Each foil should normally have:

  my_foil.gdshader
  my_foil.json        optional manifest, same base filename

Example:

  custom_foils/starfield.gdshader
  custom_foils/starfield.json

The finish dropdown should display that as:

  Custom: Starfield

In the editor, use:

  Cosmetics -> Custom Foil

Shader requirements
-------------------
Use Godot 4 canvas-item shaders:

  shader_type canvas_item;

Use UV coordinates for card-relative placement. UV is normalized from 0.0 to 1.0:

  UV.x = left to right
  UV.y = top to bottom

Do not hardcode the user's monitor resolution. Prefer UV over SCREEN_UV unless
the effect intentionally depends on screen-space movement.

The card art/foil rectangle uses roughly the STS2 card ratio:

  width:height ~= 320:446
  aspect ~= 0.7175

If your shader needs aspect correction, use a uniform like:

  uniform vec2 foil_size = vec2(320.0, 446.0);

Then correct UV like:

  vec2 centered = UV - 0.5;
  centered.x *= foil_size.x / foil_size.y;

Good shader uniforms
--------------------
The loader can safely expose these as editor knobs:

  uniform float intensity : hint_range(0.0, 3.0) = 1.0;
  uniform float speed : hint_range(0.0, 5.0) = 1.0;
  uniform bool invert = false;
  uniform vec4 tint : source_color = vec4(1.0, 1.0, 1.0, 1.0);
  uniform vec2 offset = vec2(0.0, 0.0);

Recommended names for common knobs:

  intensity
  opacity
  brightness
  contrast
  saturation
  hue_shift
  speed
  scale
  pattern_scale
  time_offset
  tint

Avoid for first-pass compatibility:

  viewport-only SCREEN_TEXTURE assumptions
  spatial shaders
  particle shaders
  scripts inside .tscn files

Texture uniforms are supported when the manifest marks the knob as "texture".
Texture paths must stay inside custom_foils/ or a subfolder such as
custom_foils/assets/.

JSON manifest
-------------
The JSON file is optional. If absent, the loader can inspect shader uniforms and
auto-create basic controls.

Use a manifest when you want a clean display name, better knob labels, default
values, min/max ranges, or hidden/internal uniforms.

Minimal manifest:

{
  "id": "starfield",
  "name": "Starfield",
  "shader": "starfield.gdshader"
}

Fuller manifest:

{
  "id": "starfield",
  "name": "Starfield",
  "shader": "starfield.gdshader",
  "description": "Animated star glints over the card art.",
  "version": 1,
  "defaults": {
    "intensity": 1.0,
    "speed": 1.0,
    "tint": "#FFFFFFFF"
  },
  "knobs": [
    {
      "key": "intensity",
      "label": "Intensity",
      "type": "float",
      "min": 0.0,
      "max": 3.0,
      "step": 0.05,
      "default": 1.0
    },
    {
      "key": "speed",
      "label": "Speed",
      "type": "float",
      "min": 0.0,
      "max": 5.0,
      "step": 0.05,
      "default": 1.0
    },
    {
      "key": "tint",
      "label": "Tint",
      "type": "color",
      "default": "#FFFFFFFF"
    }
  ]
}

Manifest fields
---------------
id:
  Stable save id. Use lowercase letters, numbers, underscores, or hyphens.
  Do not rename it after publishing a foil, or old card saves may not find it.

name:
  Display name in the Finish dropdown.

shader:
  Filename of the .gdshader in this same folder.

description:
  Optional human-readable note.

version:
  Optional number for your own tracking.

defaults:
  Optional object of shader parameter defaults.

knobs:
  Optional list of UI controls. Supported types:
    float
    int
    bool
    color
    vec2
    vec3
    vec4
    texture
    string

The loader also injects these uniforms when present, so they normally should be
hidden/internal:

  rect_size
  foil_size
  full_art

Safe failure behavior
---------------------
Custom shader foils should fail closed:

  If a shader is missing, invalid, or fails to compile, the card should render
  with no custom foil instead of breaking built-in finishes or vanilla finishes.

Template files
--------------
Use these as starting points:

  template_card_foil.gdshader
  template_card_foil.json
