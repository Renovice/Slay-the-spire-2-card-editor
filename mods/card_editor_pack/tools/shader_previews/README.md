# Shader Previews (Not Packed)

These scenes are quick Godot previews for the shaders in `slay-the-spire-2/Holo cards codes/Shader list/*.txt`, rendered on an STS2-style card rect.

## Open in Godot

1. Open the project: `slay-the-spire-2/mods/card_editor_pack/project.godot`
2. In the FileSystem dock, go to: `res://tools/shader_previews/`
3. Open any of:
   - `preview_purple_waves_ocean.tscn`
   - `preview_flame.tscn`
   - `preview_lightning.tscn`

## Notes

- Card size is set to `320x446` via the `rect_size` shader parameter in each scene.
- The preview files live under `res://tools/` so they are excluded by `pack_pck.gd` (only `mods/card_editor` + localization are packed).
