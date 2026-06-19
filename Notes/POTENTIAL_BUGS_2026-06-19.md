# Potential Bugs - STS2 Card Editor Review - 2026-06-19

Scope: repo-wide review focused on the recent `mods/card_editor` multiplayer sync and relic-effect changes.

No code changes were made during the review.

## P1 - Client edit requests can mutate shared state during an active multiplayer run

`mods/card_editor/CardEditorMultiplayerSync.cs`

The normal host snapshot path explicitly freezes definition changes during an active run, but the client edit-request path does not.

Relevant code:

- `CanEditSharedState()` blocks editing while `IsRunActive()` is true.
- The host update loop also skips normal broadcasts while `IsRunActive()` is true.
- `OnSnapshotReceived()` ignores host snapshots received during an active run.
- `OnEditRequestReceived()` accepts a client-authored `StateJson`, calls `ApplyState(...)`, captures a revision checkpoint, and broadcasts a new snapshot without checking `IsRunActive()`.

Impact:

A stale or racing client edit request can apply card/relic/created-card definition changes on the host after a multiplayer run has started, then broadcast that state to ready peers. That defeats the intended "definitions are frozen for the run" rule and can create lockstep/desync behavior.

Suggested verification/fix direction:

Add the same active-run guard to `OnEditRequestReceived()` before deserializing/applying the client state.

## P1 - Root solution build is broken by scratch/decompiler files

Root project / solution:

- `Slay the spire 2.sln`
- `Slay the spire 2.csproj`
- `.tmp_cardcmd.cs`
- `_tmp_NCardHolderHitbox.cs`
- `_tmp_NGridCardHolder.cs`

`dotnet build "Slay the spire 2.sln"` fails because the root SDK project compiles root-level `*.cs` scratch files. Two tracked files contain invalid/generated/decompiler output, and `.tmp_cardcmd.cs` is ignored by git but still present locally and included by the project.

Examples:

- `_tmp_NCardHolderHitbox.cs` has plain tool-warning text after the class body.
- `_tmp_NGridCardHolder.cs` contains invalid generated type syntax like `global::<>z__ReadOnlySingleElementList<string>`.
- `.tmp_cardcmd.cs` is ignored by `.gitignore`, but because it exists in the root it is still included by the project compile glob.

Impact:

The focused card-editor mod project builds, but the root solution does not build from this workspace. Anyone using the solution-level build path will hit compile errors unrelated to the mod changes.

Suggested verification/fix direction:

Either remove/exclude scratch files from the root project compile items, move them outside the project directory, or add explicit `<Compile Remove="...">` exclusions for these temp/decompiler files.

## P2 - Relic effects depend on a proxy card model that may not be registered

Files:

- `mods/card_editor/CardEditorRelicEffects.cs`
- `mods/card_editor/NRelicEditorPopup.cs`
- `mods/card_editor/CardEditorMod.cs`

Relic effect execution creates a transient `CardEditorRelicProxyCard` through `ModelDb`:

```csharp
CardModel canonical = ModelDb.GetById<CardModel>(ModelDb.GetId<CardEditorRelicProxyCard>());
```

The relic editor also refuses to add an effect trigger group if the proxy card cannot resolve from `ModelDb`.

The only explicit created-card registration path found in `CardEditorMod.RegisterCreatedCardsInPools()` filters by the `CardEditorCreatedCardNN` naming pattern. That excludes `CardEditorRelicProxyCard`.

Impact:

If `ModelDb` does not automatically discover/register every `CardModel` subclass, relic effect authoring and runtime execution both fail closed:

- The relic editor logs that the proxy card is not registered and cannot add an effect trigger.
- Runtime relic triggers log that proxy creation failed and skip relic effects.

Suggested verification/fix direction:

Verify in-game whether `ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId<CardEditorRelicProxyCard>())` resolves. If not, explicitly register/inject the proxy card model during mod startup.

## Risk - End-of-turn relic trigger semantics in multiplayer

Files:

- `mods/card_editor/CardEditorRelicEffects.cs`
- `mods/card_editor/NRelicEditorPopup.cs`
- `mods/card_editor/CardEditorExtraEffectScheduler.cs`

The relic editor label says "At the end of your turn", but `Hook_AfterTurnEnd_CardEditorRelicEffects_Patch` dispatches all players' relic effects whenever `CombatSide.Player` ends.

Existing scheduled card effects use a similar side-wide iteration and then filter by owner, so this may be correct for STS2's actual turn model. It should still be playtested in multiplayer if turns can end per player rather than for the whole player side.

Impact if wrong:

Each player's end-turn relic effects could fire on every player-side turn end instead of only that player's own turn end.

## Risk - Desync-protection escape hatch is intentionally broad

File:

- `mods/card_editor/CardEditorMultiplayerSync.cs`

The `Disable Desync Protection` setting suppresses `ChecksumTracker.CompareChecksums` while enabled. The code comments and settings description call this unsafe, so this appears intentional.

Residual risk:

Because the setting is persistent and broad, leaving it enabled disables the host's game-state divergence safety net beyond the immediate mismatch being debugged.

## Verification Run

Passed:

```powershell
dotnet build mods\card_editor\card_editor.csproj
```

Result: build succeeded with 0 warnings and 0 errors.

Failed:

```powershell
dotnet build "Slay the spire 2.sln"
```

Result: failed with 66 compile errors from root-level scratch/decompiler files plus root reference warnings for `0Harmony` and `sts2`.

Other checks:

```powershell
git diff --check -- mods/card_editor/CardEditorMultiplayerSettings.cs mods/card_editor/CardEditorMultiplayerSync.cs mods/card_editor/CardEditorRelicEffects.cs mods/card_editor/CardEditorRelicOverrides.cs mods/card_editor/NRelicEditorPopup.cs mods/card_editor/NCardEditorPopup.cs
```

Result: clean.
