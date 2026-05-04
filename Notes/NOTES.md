# Card Editor Mod – Developer Notes

## Rule: Understand before implementing

Before coding any new feature, always first understand how **both** the base game (STS2) and the mod currently handle the relevant mechanic. Examples:

- **Damage calculations**: STS2 applies Strength, Weak, Vulnerable, Block etc. through its own pipeline. Our `ExecuteEffect` calls into that pipeline via `DealDamageCmd` – don't re-implement damage math manually.
- **Existing vanilla card behavior**: if a vanilla card already does the thing you need, reuse the same underlying command/helper/pipeline where possible instead of inventing a Card Editor-only version of the mechanic.
- **Vanilla semantics first**: prefer vanilla-style semantics and wording whenever possible. If a safety knob is needed for mod-created edge cases, default it to vanilla behavior and make any safer deviation explicit in the editor UI/card text.
- **Source semantics**: if a trigger can be raised by cards, powers, relics, or global commands, preserve the real source. Do not make a `null`/non-card source satisfy card-specific filters, and do not invent a fake card source just to make a filter pass.
- **Loop limit semantics**: the per-effect autoplay loop limit counts whole effect activations, not individual cards produced by that activation. Power-mode immediate triggers consume the limit before running so repeated OnPlay events cannot queue recursive autoplay work. The separate global autoplay cap counts individual autoplayed cards and remains the hard softlock safety net.
- **Loop limit scope**: autoplay loop limits can be tracked per card copy, across all copies of the same card/effect row, or across equivalent autoplay effects with the same trigger/filter/generated-card signature.
- **Loop limit descriptions**: in combat, auto-generated extra-effect text shows the remaining loop-limit uses for the current turn and colors the number using the normal card-text increase/decrease highlighting.
- **Card variations / upgrades**: STS2 cards track upgrade level on `CardModel`. The editor stores base + upgrade delta effects separately and resolves them via `GetEffectiveExtraEffects`. Always check how an effect differs between base and upgraded before assuming it "just works".
- **Powers**: STS2 powers are persistent `PowerModel` instances attached to creatures. Our `CardEditorExtraEffectPower` is a single invisible power that stores a list of entries. New entries are added when a card is played; entries are removed on expiry. Never bypass this – scheduling deferred effects yourself will fight the power system.
- **Scheduling**: One-shot deferred effects (e.g. "at the start of your next turn, deal damage") go through `CardEditorExtraEffectScheduler`. Repeating effects MUST use the power system (`AsPower = true` + a trigger).

## Rule: Do not disturb vanilla systems unless necessary

Default to **mod-scoped changes only**.

- Do **not** change vanilla mechanics, vanilla card text rendering, vanilla hover-tip behavior, vanilla cost/icon rendering, or other base-game pipelines unless the task absolutely requires it.
- Prefer patches that only affect Card Editor-owned cards, Card Editor-generated text, Card Editor powers, or Card Editor UI.
- If touching a shared vanilla path is unavoidable, keep the guard clause as narrow as possible and document why the broader hook was necessary.
- Never broaden a helper from "our cards" to "all cards" without an explicit reason and a regression check against normal vanilla cards.

### Original compendium / library is off-limits by default

- Do **not** patch the original vanilla compendium / card library rendering path unless there is no safer alternative.
- Canonical library cards are immutable preview models. Never assume they have combat ownership, mutable state, or a valid `Owner` / `CombatState`.
- Any shared text/targeting/helper patch must explicitly bail out for non-mutable canonical cards unless the feature is intentionally supposed to affect the vanilla library.
- Prefer Card Editor popups, Card Editor-created previews, or Card Editor-owned cards over hooks that run on the base compendium.

## Rule: New editor controls must use the existing grid system

When adding a new UI "box", dropdown, modifier row, or extra field to the card editor:

- Put it into the existing `NCardEditorPopup` form/grid system instead of creating a one-off freeform layout.
- Reuse the same row/slot conventions as the surrounding controls so it participates in the normal compact ordering and reflow rules.
- If a control changes between 1-box / 2-box / 3-box states, integrate that into the existing grid behavior rather than giving it unique positioning logic.
- Avoid custom spacing hacks unless the shared grid cannot express the layout.
- If a new modifier needs extra controls, add them as part of the current grid pattern so they sort/reorder the same way as rows like `Timing`, `Power Filter`, and `Condition Bonus`.

## Rule: New extra effects must include universal modifiers by default

Before adding a new `CardExtraEffectKind`, read `Notes/extra-effect-implementation-baseline.md`. A new effect is not complete just because its core action runs; it should support the shared timing, power, grant-to-card, scaling/count, conditional bonus, branch, use-limit/effect-limit, self-scaling, card-filter, text, upgrade, and UI systems wherever the effect's semantics allow it.

---

## Power trigger system (CardEditorExtraEffectPower)

| Field | Meaning |
|---|---|
| `TriggerEveryN` | Fire only every Nth hit of the trigger condition (0/1 = every time) |
| `TriggerCounter` | Running count of times trigger condition was met (used for EveryN) |
| `TriggerMaxFires` | Remove power entry after this many fires (0 = unlimited / permanent) |
| `TriggerFireCount` | Running count of actual fires (used for MaxFires check) |

### Duration semantics in power mode

| Duration | Behaviour |
|---|---|
| Permanent (0) | Entry stays until combat ends |
| This Turn (1) | Entry is removed at the end of the turn it was created |
| *(MaxFires > 0)* | Entry is removed after firing N times (independent of Duration) |

### Text generation

- `StartOfTurn` / `EndOfTurn` + `TriggerMaxFires = 3` → "For the next 3 turns, at the start of your turn, …"
- `OnPlay` + `TriggerMaxFires = 3` → "The next 3 times you play a card, …"
- `TriggerEveryN = 2` → "Every 2 cards you play/draw/…"
- `Duration = ThisTurn` (non-stat-buff) → "This turn, …" prefix

---

## Key files

| File | Role |
|---|---|
| `CardEditorExtraEffects.cs` | All card data types, text generation, effect execution |
| `CardEditorExtraEffectPower.cs` | Persistent power that fires repeated trigger effects |
| `NCardEditorPopup.cs` | UI – the main card editor popup |
| `CardEditorExtraEffectScheduler.cs` | One-shot deferred effect scheduling |
| `CardEditorTemporaryExtraEffectController.cs` | Temporary per-card effects (e.g. cost reductions) |

---

## Build & deploy

```powershell
cd mods\card_editor
dotnet build card_editor.csproj -c Release
# Then copy build\net9.0\card_editor.dll to:
#   ../../built cfiles/card_editor.dll
#   C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\Card_editor\card_editor.dll
```

---

## Bug fix verification

- Any bugs should be fixed at their root, not at the level of individual interactions, but all interactions and effects that are affected by said bug.
- After fixing a bug, always test it in the live setup environment after build and deploy, not just by reading code or relying on logs.
- For live verification, launch through Steam rather than the standalone `.exe`, because the Steam run is the real mod environment we ship against.
- When checking runtime logs from a live verification run, inspect `user://logs/godot.log`.
- Reproduce the original bug in the live game setup when possible, then confirm the exact scenario now behaves correctly.
- Treat a bug fix as incomplete until it has been verified in the live setup environment.

---

## Card finish editing parameters

Every shader-based finish supports the **Finish Editor** UI (collapsible sliders). When creating a new finish, always define slider entries in `GetFinishSliderDefs()` (in `NCardEditorPopup.cs`) and wire the corresponding `SetShaderParameter` calls in the controller's `SyncPortrait` method.

### Common parameters (all shader finishes)

| Param key | Uniform name | Description | Default |
|---|---|---|---|
| `speed` | `motion_speed` | Overall animation speed (0 = frozen) | 1.0 |
| `timeOffset` | `time_offset` | Phase offset / freeze-frame scrub (0–10) | 0.0 |
| `hueShift` | `hue_shift` / `hue_offset` | Shift rainbow hue (0–1 = full cycle) | 0.0 |
| `saturation` | `color_saturation` (external) / `saturation` (inline) | Color vividness (0 = greyscale, 1 = full) | 1.0 (0.60 for RainbowGlitter) |
| `tintR` | `color_tint.x` | Red channel of tint colour | 1.0 |
| `tintG` | `color_tint.y` | Green channel of tint colour | 1.0 |
| `tintB` | `color_tint.z` | Blue channel of tint colour | 1.0 |
| `tintStrength` | `tint_strength` | How strongly tint replaces rainbow (0 = off, 1 = full) | 0.0 |

### Per-finish extra parameters

| Finish | Extra params |
|---|---|
| VMax Swirl | `strength` (intensity), `brightness`, `pastel` |
| Cosmos Holo | `strength`, `brightness`, `pastel` |
| Galaxy Holo | `strength`, `brightness`, `pastel`, `maskContrast` |
| Prismatic Band Glare | `strength`, `glareStrength`, `metallicStrength` |
| Rainbow Glitter (Art) | `strength`, `brightness`, `pastel` |

### Checklist for adding a new finish

1. Create/select a shader (external `.gdshader` or inline C# string)
2. Add a `CardEditorVisualFinish` enum value
3. Create a controller class with `Sync()` / `SyncPortrait()` matching existing patterns
4. Wire all common params (`motion_speed`, `time_offset`, `hue_shift`, `color_saturation`, `color_tint`, `tint_strength`)
5. Add a case in `GetFinishSliderDefs()` with all applicable sliders
6. Register the finish in the dropdown + resolver
