# Card Editor production-hook and real-engine test environment

Date: 2026-08-31  
Game target: beta `v0.111.0`, commit `41cef1ea`  
Mod source version: `10.1.4`

## Scope

This pass tested the four reports that were still described as requiring game validation:

1. Reduce Cost -> This Card -> Trigger -> Whenever event.
2. Match Energy shown twice.
3. Resources -> After Death -> Check on Power.
4. Run Effect Source for Particle Wall, I Am Invincible, and Right Hand Hand.

It also hardened the engine runner so a failed test cannot leave its Debug DLL or temporary Steam app-ID file in the installed mod.

## Hypotheses and results

### H1: production game hook paths can be exercised without a rendered game

**Result: TRUE.**

The headless harness now installs the same Card Editor Harmony patch classes used by the mod and calls the beta game's real `Hook.AfterCardPlayed` and `Hook.AfterDeath` dispatchers with real `RunState`, `CombatState`, cards, creatures, piles, powers, and hook contexts.

Verified through production entry points:

- Whenever cost reduction installs on the source card, ignores the installation play, and reduces cost after the selected event.
- After Death installs as a power and grants energy from the game's death hook.
- Particle Wall and I Am Invincible grant their borrowed block through `Hook.AfterCardPlayed`.
- Right Hand Hand runs its borrowed Osty-dependent damage behavior through `Hook.AfterCardPlayed`.

Harness result: `21/21 tests passed; 2187 beta and mod models loaded.`

### H2: the installed beta can run deterministic UI checks headlessly

**Result: TRUE.**

The engine runner launches the actual installed `SlayTheSpire2.exe` for beta `v0.111.0` in headless mode. Steam authentication succeeds by temporarily supplying `steam_appid.txt` with app ID `2868840`. The Debug mod receives `CARD_EDITOR_ENGINE_SELF_TEST=1` from process start and attaches checks at the real `NMainMenu` callback.

The first runner design used a dynamically instantiated custom C# `Node`. Godot failed while registering that script class (`Invalid Task ID`). Running the asynchronous checks from the existing vanilla `NMainMenu` node removed that failure.

### H3: Match Energy is currently duplicated in the real advanced effect UI

**Result: FALSE for the tested `v0.111.0`/10.1.4 source.**

The engine test builds the real embedded advanced effect UI, loads a `CardCostsLess` row, enumerates the resulting Godot `OptionButton` items, and finds exactly one `Matching Cards (Energy)` item.

Measured result: `Visible option occurrences: 1`.

This verifies option registration in the current build. It does not reproduce every historical preset or every old serialized override that could have produced duplicate generated text.

### H4: current generated card descriptions overflow real card bounds

**Result: FALSE for the tested sample.**

The engine rendered the 16 longest descriptions among 627 current card candidates using real `NCard` and `MegaRichTextLabel` controls. Every sample fit both measured width and height bounds. The sample included Right Hand Hand at 295 characters.

### H5: the engine test leaves the installed mod unchanged

**Result: TRUE.**

Before and after the passing engine run:

- Installed DLL SHA-256: `A5C58BDFED1EE2B2F5F875742AF244370E48480632A3E1B4BED1F90627437909`.
- `steam_appid.txt` absent before and absent after.
- No `SlayTheSpire2` process remained after the test.

The script validates beta `v0.111.0` before changing files and restores the DLL, PDB, app-ID state, process environment, and test process in a `finally` block.

## Evidence boundary

The three behavior reports now have production-Harmony/game-hook integration coverage, which is materially stronger than calling Card Editor helper methods directly. They still do not simulate a full player-driven combat animation/selection sequence frame by frame.

The Match Energy and description checks are real installed-engine UI checks. They boot the actual beta executable, instantiate real Godot controls, wait real frames, and inspect rendered control measurements. Running the decompiled project separately is no longer required for these checks; the decompiled beta source remains useful for mapping hook contracts and call order.

## Commands

```powershell
& .\tools\CardEditor.TestHarness\run-tests.ps1
& .\tools\CardEditor.EngineTests\run-engine-tests.ps1
```
