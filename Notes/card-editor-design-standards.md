# Card Editor Mod — Design Standards

## Safety Standard: Scope changes to the mod first
When adding new behavior, wording rules, formatting helpers, or hover-tip support, target **Card Editor-owned surfaces first**.

- Prefer hooks that apply only to created cards, cards with Card Editor extra effects, Card Editor powers, or Card Editor UI.
- If the base game already has the behavior, prefer reusing the vanilla card/mechanic pipeline instead of recreating it in mod code (for example, call the same command/helper a vanilla card uses when possible).
- Avoid changing shared vanilla rendering or gameplay systems unless there is no safe mod-local extension point.
- If a shared vanilla hook must be used, add a strict eligibility check so untouched vanilla cards continue to behave exactly as before.
- Treat regressions on normal vanilla cards as a design failure, not an acceptable side effect.

## Effect Editor Pattern (established v2.7)
Every new card effect must expose **all variable dimensions** as editable fields in the UI.
No hardcoded behavior — every knob the user could want to tweak gets its own dropdown, spinner, or tickbox.

Example: ConditionalAutoPlayFromPile/ConditionalAutoDrawFromPile (enums 62-63) exposes:
- **Event verb** (played, drawn, discarded, exhausted, created) — via history scaling verb dropdown
- **Threshold** (amount, e.g. 3+) — via scaling amount field
- **Card-type filter** (any, attack, skill, power) — via scaling card filter
- **Time window** (this turn, this combat, last N turns) — via count-turns dropdown
- **Pile source** (draw pile, discard pile, exhaust pile) — via MoveFromPile selector

This is the standard. Future card effects should follow the same "conditional" multi-variable pattern.

### Required Conditional Variable Axes
When a new effect concept depends on combat state, intent state, or played-card properties, expose those as reusable variables instead of hardcoding one narrow case.

Common examples that should be treated as first-class editable variables:
- **Enemy status filter** — e.g. target has Weaken, Vulnerable, Poison, Doom, or another status/power
- **Enemy intent filter** — e.g. target intends to Attack, Defend, Buff, Debuff, or another supported intent category
- **Played card cost filter** — e.g. trigger only when the player uses a card costing 0, 1, 2, 3, etc.

Design rule:
- If the effect logic can reasonably be described as "if target has X", "if target intends Y", or "if played card cost is Z", then X/Y/Z must be an exposed dropdown, spinner, or tickbox-driven variable in the editor.
- Prefer generic selectors over bespoke one-off effects. For example, add a status selector or intent selector instead of adding separate hardcoded effects like "If enemy has Poison" and "If enemy intends Attack".

## UI Declutter: Merging Effects (do not break old cards)
If the Extra Effects dropdown is getting cluttered, **merge** related effects into one overarching entry and expose the differences as additional knobs after selection.

Rules:
- **Never delete or renumber** existing `ExtraEffectKind` values (old presets/cards must keep loading).
- **Hide legacy effect kinds** from the picker instead of removing them, and map them to the new unified UI on load.
- **Keep serialization stable**: the UI can resolve a "base kind + knobs" into the legacy underlying kind when saving/applying.
- **Pick a fitting overarching name** for the merged entry (e.g. `"Ignore Effect"`), and represent specific variants via a secondary selector/tickbox/spinner.

## Standardization Rule: Use normative presentation
Features should be standardized and normative across Card Editor-owned surfaces.

Rules:
- Reuse the same wording, icon conventions, timing phrasing, and visual grammar that equivalent vanilla/card-editor features already use.
- Do not introduce a generic fallback presentation when a class-specific or existing standardized presentation is available.
- Resource text must prefer the proper class/resource icon source used by other equivalent effects, rather than plain digits or generic placeholder icons.
- If two effects communicate the same gameplay idea, they should format their card text the same way unless there is a deliberate, documented gameplay reason not to.

## Key Files
- `CardEditorExtraEffects.cs` — enum, definitions, format, runtime logic
- `CardEditorExtraEffectTriggerPatches.cs` — hook patches + tracker classes
- `NCardEditorPopup.cs` — UI (source: `mods/card_editor/`, pack copy: `mods/card_editor_pack/mods/card_editor/`)
- Localization (6 files): `mods/card_editor_pack/.../localization/{eng,zhs}/extensions.json`, `built cfiles/localization/{eng,zhs}/extensions.json`, `built c files chinese/localization/{eng,zhs}/extensions.json`
- README: `built cfiles/README.txt` (also deployed to game folder)

## Build & Deploy
- Build: `cd mods/card_editor; dotnet build --configuration Release`
- Output: `mods/card_editor/build/net9.0/card_editor.dll`
- Deploy target: `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\Card_editor\`
- Copy: DLL, PDB, localization files, README
