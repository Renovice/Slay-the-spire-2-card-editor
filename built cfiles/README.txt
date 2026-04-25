Card Editor (Slay the Spire 2 Mod)

Adds an "Editor" button to the main menu that opens a Compendium-style card browser and lets you edit cards in-game.

FEATURES
- Edit: Energy cost, Star cost, Replay count, Card type
- Keywords: Exhaust, Ethereal, Retain, etc.
- Numbers: Damage/Block/etc. (dynamic vars)
- Enchantment / Affliction + amount
- Extra Effects: add reusable effects with trigger/target/timing/duration
- Scaling (per Extra Effect): effects can scale based on play/draw/discard/exhaust/create history
- Conditional Effects: multi-variable effect conditions (event type, threshold, card-type filter, time window, pile) — fully editable
- Presets: Save / Load / Delete + "Revert to Vanilla"
- Hotkey: quick-open Editor (default F11, remappable)
- Creator: custom cards (slots) + optional custom art from disk

INSTALL (works on old + beta/new loader)
1) Create a folder:
   ...\Slay the Spire 2\mods\card_editor\
2) Copy EVERYTHING from this zip into it (including the `localization` folder):
   - card_editor.dll
   - card_editor.pck
   - card_editor.json  (required for the beta/new mod loader)
   - localization\...
3) Launch the game -> Main Menu -> Editor (and Creator)

CUSTOM ART ("card art" folder)
If you use the Creator and want to pick your own portrait art:
1) Create a folder next to card_editor.dll:
   ...\mods\card_editor\card art\
   (also supported: ...\mods\card_editor\card_art\)
2) Drop images in that folder:
   - Supported: .png / .jpg / .jpeg / .webp
   - Recommended portrait size: 1000x760 (same as base game portraits) or higher.
3) In Creator -> Art dropdown, pick: "Custom: <filename>"

LOCALIZATION / TRANSLATION
- Localization is external on purpose; the PCK should not be edited for translations.
- To translate the mod, edit:
  ...\mods\card_editor\localization\<lang>\extensions.loc
  ...\mods\card_editor\localization\<lang>\settings_ui.loc
- Example: Simplified Chinese = `zhs`
- Korean = `kor`
- The game log prints the exact external localization file path loaded by Card Editor.

NOTES
- Linux/Steam Deck is case-sensitive: keep folder/file casing exactly as shipped.
- Presets: user://card_editor/presets/*.json
- Logs: user://logs/godot.log (search for [CardEditor])

CHANGELOG (high level)
- v1.4: Presets + extra effects/scaling, hotkey, beta/new mod-loader manifest support, Creator slots + custom art folder support.
- v2.0: External localization support, more extra effects, true X-cost (energy/stars), misc fixes.
- v2.7: Conditional auto-play/draw from pile (multi-variable editor: event verb, threshold, card-type filter, time window, pile source). Draw-cards-cost-less Permanent wording fix.

DESIGN NOTE
Future card effects should follow the "conditional" pattern: every knob that
can vary (trigger event, threshold, filter, window, pile, etc.) gets its own
dropdown or field in the editor so users can compose behaviour without code.
