# Card Editor Headless Test Harness

This project runs Card Editor regression tests against the installed Slay the Spire 2 beta assemblies without launching Godot or restarting the game.

Run it from anywhere:

```powershell
& "C:\Users\Bartek\OneDrive\Dokumenter\REPOS\slay-the-spire-2\slay-the-spire-2\tools\CardEditor.TestHarness\run-tests.ps1"
```

The suite exits `0` only when every test passes. It treats C# warnings as errors.

## What It Exercises

- Move cards between draw and discard piles, including top and bottom placement.
- Manually choose and Exhaust a card through the beta `TestCardSelector` path.
- Transform cards filtered by card type.
- Transform cards filtered by vanilla card tag.
- Trigger This Card cost reduction on draw, discard, and exhaust events.
- Store an After Death effect as `CardEditorExtraEffectPower` and dispatch it for an enemy death.
- Copy Weak stacks from one selected enemy to another enemy.
- Keep Grant -> Hits All Enemies blocked.
- Assert that Match Energy is registered once in the editor popup source.
- Assert that the current beta card description path still calls `SetTextAutoSize`.

## Headless Runtime

The harness loads the installed beta `sts2.dll`, all vanilla model types, and Card Editor's custom model types. It creates a real `RunState`, `CombatState`, player, piles, cards, enemies, powers, and choice context. Harmony replaces only Godot-dependent log output so game and mod code can report diagnostics through the console.

The initial `SentryGodotInitializer` message is expected: `Sentry.Godot` cannot load its GDExtension in a plain .NET process. Seeing that line in the harness is not a test warning or failure.

## Boundary

The final two checks are source contracts, not visual assertions. They catch duplicate editor registration and removal of the beta auto-size call, but only an automated Godot scene or an in-game screenshot can prove final pixel layout and long-text fitting.
