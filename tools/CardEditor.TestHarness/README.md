# Card Editor Headless Test Harness

This project runs Card Editor regression tests against the installed Slay the Spire 2 beta assemblies without launching Godot or restarting the game.

Run it from anywhere:

```powershell
& "C:\Users\Bartek\OneDrive\Dokumenter\REPOS\slay-the-spire-2\slay-the-spire-2\tools\CardEditor.TestHarness\run-tests.ps1"
```

The suite exits `0` only when every test passes. It treats C# warnings as errors.

## What It Exercises

- Move cards across the complete Hand/Draw/Discard/Exhaust source-destination matrix, including top and bottom placement.
- Ensure a Discard/Exhaust reorder fires only its final positional trigger, not the vanilla pipeline's intermediate Bottom position.
- Manually choose and Exhaust a card through the beta `TestCardSelector` path.
- Transform cards filtered by card type.
- Transform cards filtered by vanilla card tag.
- Trigger This Card cost reduction through every public lifecycle/event dispatcher (24 paths).
- Store After Death effects on player and enemy power hosts, ignore prevented/unrelated deaths, and dispatch a real death.
- Copy Weak stacks from one selected enemy to another enemy.
- Keep Grant -> Hits All Enemies blocked.
- Construct the beta `StartRunLobby` with a recording client service and prove Ready updates local state, notifies the UI, and sends `LobbyPlayerSetReadyMessage`.
- Assert that both base and upgraded card editors save the selected Result Pile destination.
- Assert that Match Energy is registered once in the editor popup source.
- Assert that the current beta card description path still calls `SetTextAutoSize`.

## Headless Runtime

The harness loads the installed beta `sts2.dll`, all vanilla model types, and Card Editor's custom model types. It creates a real `RunState`, `CombatState`, player, piles, cards, enemies, powers, and choice context. Harmony replaces only Godot-dependent log output so game and mod code can report diagnostics through the console.

The initial `SentryGodotInitializer` message is expected: `Sentry.Godot` cannot load its GDExtension in a plain .NET process. Seeing that line in the harness is not a test warning or failure.

## Boundary

The editor serialization, Match Energy, and auto-size checks are source contracts, not visual assertions. They catch save-path or registration regressions, but only an automated Godot scene or an in-game screenshot can prove final pixel layout and long-text fitting.

For those engine-backed checks, use `tools/CardEditor.EngineTests/run-engine-tests.ps1`. It launches the installed game through Steam with an explicit self-test argument, measures the real popup and card controls, writes `user://card_editor/engine_ui_selftest_report.txt`, and exits. Steam must be signed in because the game blocks before mod loading otherwise.
