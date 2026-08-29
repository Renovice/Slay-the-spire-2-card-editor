# Card Editor Headless Regression Harness

Date: 2026-08-29

## Objective

Replace repeated game relaunches for the reported Card Editor behaviors with deterministic tests against the currently installed beta assemblies.

## Hypotheses And Results

### Hypothesis 1: The beta game model and combat layers require a running Godot process

Result: **False.**

Evidence: a plain .NET 9 process initializes 2,187 vanilla and Card Editor model instances, creates a `RunState`, activates a real `CombatState`, and runs pile, selection, transform, power, and status commands. Godot-dependent logging is redirected to standard output by a harness-only Harmony patch.

### Hypothesis 2: Move, Exhaust, transform, After Death, and Copy Debuffs can be tested through production dispatch

Result: **True.**

Evidence: the suite invokes `RunResolvedOnPlayEffectsDuringCardPlay`, `RunAfterCardPlayed`, `CardEditorExtraEffectPower.AfterDeath`, vanilla `CardPileCmd`, vanilla `CardCmd`, and beta `TestCardSelector`. All corresponding assertions pass.

### Hypothesis 3: Triggered This Card cost reduction is inactive before its selected event

Initial result: **False.**

Evidence: the first suite run observed reduction `1` before `OnDraw` fired. `GetCardCostsLessAdjustment` passed `includeTimedIntrinsic: true` for temporary grants, and `ShouldIncludeIntrinsicCardCostsLessDefinition` then accepted every row, including `Triggered` definitions.

Fix result: **True.**

Evidence: temporary rows now pass `IsPassiveCardCostsLessDefinition`. The event definition remains inactive; `ApplyTriggeredCardCostsLess` materializes the separate passive grant when On Draw, On Discard, or On Exhaust fires. All three event cases pass and the shared helper also protects star-cost calculations.

### Hypothesis 4: The two UI reports can be fully proved without Godot rendering

Result: **False.**

Evidence: source contracts prove one Match Energy registration and the current beta `NCard` call to `SetTextAutoSize`. They cannot prove final card-text pixel bounds or detect a duplicate introduced dynamically at runtime. Those two reports still need an engine-render integration test for complete visual verification.

## Final Automated Result

Command:

```powershell
& ".\tools\CardEditor.TestHarness\run-tests.ps1"
```

Result: **10/10 passed, process exit code 0.**

Build result: **0 warnings, 0 errors.**

The suite covers:

1. Move between piles.
2. Manual Exhaust selection.
3. Transform by type.
4. Transform by vanilla tag.
5. This Card Whenever cost reduction for draw, discard, and exhaust.
6. After Death power dispatch.
7. Copy Debuffs with two enemies.
8. Hits All Enemies grant safety.
9. Match Energy unique source registration.
10. Beta card-description auto-size source contract.

## Expected Diagnostic

`SentryGodotInitializer` states that its GDExtension is unavailable. This is expected only because the test runner is intentionally a plain .NET process. It remains an error if the same message appears while the game itself is running.

## Deployment Verification

The rebuilt production artifacts were copied to `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\Card Editor2` after confirming the game was not running.

- DLL SHA256: `D9578A09D011DE82223B0812C20B88F96F41CDB6B6F49F9DB9018934325B7D0A`
- PDB SHA256: `0E1A804C941D4DF829A6F93AEF2FA2FA8CB92C199E491984B023DA70D9C9C037`
- Source/deployment hash match: **True** for both files.
