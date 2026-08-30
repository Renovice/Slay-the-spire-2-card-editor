# Yellow Regression and Multiplayer Ready Test Pass

Date: 2026-08-29
Game beta: v0.111.0
Installed game assembly models loaded by harness: 2,187

## Executive Result

- Runtime suite: **PASS, 12/12 test groups**.
- Runtime subcases expanded in this pass: **24 pile routes/positions** and **24 public Whenever dispatch paths**.
- Multiplayer Ready: **PASS** through the real beta `StartRunLobby.SetReady` implementation with a recording client network service.
- Engine UI checks: runner **built successfully**, but execution is **blocked before mod loading because Steam is logged off**. These two checks remain unverified visually.
- New bug found and fixed: Move Between Piles ignored `Top` when the destination was Discard or Exhaust.

## Hypotheses and Results

### H1: Move Between Piles respects top/bottom for every combat pile

**Initial result: FALSE.** `Hand -> Discard Top` failed because `CardCmd.Discard` always inserts at the bottom. Exhaust used the same forced-bottom pattern.

**Fix:** keep the vanilla Discard/Exhaust pipelines for history and hooks, then perform a same-pile reorder for non-bottom destinations and dispatch the resulting positional trigger.

**Final result: TRUE.** All 24 combinations of four source piles, three distinct destinations, and Top/Bottom pass. A separate regression proves Discard Top fires only the final Top reaction and does not leak the vanilla pipeline's temporary Bottom reaction.

### H2: Manual Exhaust can select a card in hand

**Result: TRUE.** A real beta `TestCardSelector` chooses the intended hand card and the card enters Exhaust through the production effect path.

### H3: Transform filtering works for type and tag

**Result: TRUE.** Attack-only filtering leaves a Skill untouched, and a vanilla-tag filter transforms only the tagged card.

### H4: Reduce Cost -> This Card -> Whenever reaches every event dispatcher

**Result: TRUE for all public dispatch paths.** The test proves the reduction is inactive before the event and active after each of 24 dispatchers, including turn boundaries, pile-position events, combat lifecycle events, orb events, attack, death, and chosen-card events.

`Fatal` and `OnCountEvent` are specialized pipelines rather than public one-event dispatch methods and are outside this generic matrix.

### H5: Resources -> After Death -> Check on Power works

**Result: TRUE.** Player-hosted Any Enemy death grants energy once, ignores prevented death, and an enemy-hosted Effect Targets configuration ignores unrelated/prevented deaths before firing on its own death.

### H6: Card Editor allows a multiplayer client to Ready

**Historical result: FALSE.** Before commit `ec0ee76`, `AllowClientReady` returned `false` while waiting for a snapshot. Harmony therefore skipped vanilla `StartRunLobby.SetReady`, the exact method that sets `LocalPlayer.isReady`, sends `LobbyPlayerSetReadyMessage`, and notifies the lobby listener. A lost deferred callback left the player permanently unable to ready.

**Current result: TRUE.** The regression constructs the real beta lobby with a client `INetGameService`. The Card Editor prefix allows the call, local Ready becomes true, one `LobbyPlayerSetReadyMessage` is recorded, the listener is notified, and the missing-snapshot request is re-armed.

### H7: Match Energy is not displayed twice

**Source result: TRUE.** The popup source registers `Matching Cards (Energy)` once.

**Engine/visual result: BLOCKED, not failed.** The opt-in engine runner builds and attempts to count actual `OptionButton` items. Direct launch first failed due missing app ID; a temporary app ID then proved SteamAPI fails at `ConnectToGlobalUser` because Steam is logged off. The temporary file and stalled processes were removed.

### H8: Generated card text stays inside the card text box

**Source result: TRUE.** The beta `NCard` uses `MegaRichTextLabel.SetTextAutoSize` for descriptions.

**Engine/visual result: BLOCKED, not failed.** The runner is ready to render the 16 longest current descriptions and compare content width/height against the real label bounds. It cannot reach main-menu mod loading until Steam is signed in.

## Reproduction Commands

Headless runtime and fake-client networking:

```powershell
& ".\tools\CardEditor.TestHarness\run-tests.ps1"
```

Godot UI controls, after Steam is signed in:

```powershell
& ".\tools\CardEditor.EngineTests\run-engine-tests.ps1"
```
