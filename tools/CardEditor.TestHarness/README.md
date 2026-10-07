# Card Editor Headless Test Harness

This project runs Card Editor regression tests against the repository's tracked Slay the Spire 2 beta assemblies without launching Godot or restarting the game. It falls back to the installed game assemblies only when the tracked references are absent.

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
- Reproduce borrowed `Run Effect Source` lifecycle behavior for result piles, multiplayer transfer, draw/exhaust reactions, combat-long source state, cost scaling, return-to-hand thresholds, and auto-play eligibility.
- Install Card Editor's production Harmony patches and invoke the beta game's real `Hook.AfterCardPlayed` path for Whenever cost reduction.
- Install an After Death power through the real `Hook.AfterCardPlayed` path and invoke the beta game's real `Hook.AfterDeath` dispatcher.
- Run Particle Wall, I Am Invincible, and Right Hand Hand through the production Run Effect Source patch and the real `Hook.AfterCardPlayed` dispatcher.
- Copy Weak stacks from one selected enemy to another enemy.
- Publish a Select Row result and prove Draw Cards draws exactly that card through the selection bus.
- Keep Grant -> Hits All Enemies blocked.
- Preserve explicit card damage bonuses without leaking them into cardless retaliation such as Thorns.
- Capture completed vanilla multi-target damage for HP/blocked/total/overkill/instance/kill amount sources without including later extra-effect damage.
- Expire max-fire and max-turn power listeners, remove their visible mirrors, and prove reapplication starts from one clean entry.
- Merge two matching power applications into one listener while preserving one payload activation per stack.
- Keep delayed damage and Block unpowered across nested power branches and custom-status behaviors, including a runtime Dexterity check.
- Trigger a receiver-specific Bearer Hit by Attack power only for its owner, including a fully blocked hit.
- Keep Apply Poison registered as an effect that can run from a Whenever power trigger.
- Bind a selected target VFX preset to the beta `AttackCommand` hit loop used by multi-hit and additional damage.
- Keep VFX hooks on all four direct `CreatureCmd.Damage` executors used by shared/dynamic extra-damage rows.
- Construct the beta `StartRunLobby` with a recording client service and prove Ready is held until a host snapshot is applied, then updates local state, notifies the UI, and sends exactly one `LobbyPlayerSetReadyMessage`.
- Assert that Reset reuses the confirmation popup and performs no editor-state mutation before confirmation.
- Assert that both base and upgraded card editors save the selected Result Pile destination.
- Assert that Match Energy is registered once in the editor popup source.
- Assert that the current beta card description path still calls `SetTextAutoSize`.

## Headless Runtime

The harness loads the tracked beta `sts2.dll`, all vanilla model types, and Card Editor's custom model types. It creates a real `RunState`, `CombatState`, player, piles, cards, enemies, powers, and choice context. The production-entry tests install the same Card Editor Harmony patch classes used in the game and drive the beta game's public hook dispatchers. Harmony replaces only Godot-dependent log output so game and mod code can report diagnostics through the console.

The initial `SentryGodotInitializer` message is expected: `Sentry.Godot` cannot load its GDExtension in a plain .NET process. Seeing that line in the harness is not a test warning or failure.

## Boundary

The editor serialization, Match Energy, and auto-size checks are source contracts, not visual assertions. They catch save-path or registration regressions, but only an automated Godot scene or an in-game screenshot can prove final pixel layout and long-text fitting.

For those engine-backed checks, use `tools/CardEditor.EngineTests/run-engine-tests.ps1`. It refuses to run unless the installed game is beta `v0.111.0`, temporarily installs a Debug DLL and `steam_appid.txt`, and launches the installed executable headlessly with an opt-in child-process environment variable. The test measures the real popup and card controls, writes `user://card_editor/engine_ui_selftest_report.txt`, and exits. The original live DLL/PDB, app-ID file state, and caller environment are restored in a `finally` block whether the test passes, fails, or times out. Steam must already be running and signed in because the installed game blocks before mod loading otherwise.
