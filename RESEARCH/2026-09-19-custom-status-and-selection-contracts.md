# Custom Status Passive Modifiers and Selection Contracts

Date: 2026-09-19

## Scope

Investigated three reports:

1. A custom status cannot model Enfeebling Touch-style passive Strength loss because every behavior row requires a `Whenever` trigger.
2. Runtime card-selection publishers are not universally available as `Selected Row` sources, and publisher/consumer flags are not fully audited.
3. Quest rewards support fewer effect kinds outside combat than they do during combat.

## Hypothesis 1: passive custom-power stats were missing

**Result: TRUE.**

Vanilla `EnfeeblingTouchPower` derives from `TemporaryStrengthPower`. The vanilla power applies a signed `StrengthPower` contribution when it is applied, adjusts that contribution when its amount changes, and restores the contribution when it expires. Card Editor custom statuses only installed their behavior rows into `CardEditorExtraEffectPower`, which is an event listener. There was no lifecycle-owned passive stat contribution.

Implemented:

- Added the serialized-compatible trigger `WhilePowerActive = 27` without renumbering existing trigger values.
- Limited it to fixed Strength, Dexterity, and Focus gain/loss behavior rows in the custom Status Behavior editor.
- The custom status now tracks the exact stat totals it contributed, scales them with status stacks, adjusts them when stacks change, and reverses them when removed.
- Passive rows are excluded from the normal event-listener carrier, preventing duplicate or dead `Whenever` entries.
- The UI forces Power mode and Self/bearer targeting, disables unsupported amount/scaling/branch/grant controls, and serializes the passive trigger.
- Generated text describes the bearer as having a signed stat modifier while the power is active rather than adding a `Whenever` clause.

## Hypothesis 2: selection capabilities could drift silently

**Result: TRUE.**

The registry had separate UI and runtime publisher flags. The editor had already started compensating by checking either flag, but the registry audit checked neither selection publishing nor `Selected Row` consumption against an independent runtime contract. A newly added runtime publisher could therefore be omitted from the UI without failing the startup audit.

Implemented:

- A publisher is now one combined capability: every declared runtime publisher is also a UI source.
- UI source discovery and Chainboard auto-wiring use `CanPublishCards` and `CanConsumeCards` instead of reconstructing flag logic.
- Independent executor-side publisher and consumer contracts are audited against every enum member at startup.
- The audit also rejects any UI/runtime publisher disagreement.

## Hypothesis 3: restricted out-of-combat quest effects were an accidental bug

**Result: FALSE. No code change.**

During combat, quest rewards have a `CombatState`, creatures, card piles, orb queues, and card-play context, so the generalized effect executor is valid. A quest can also finish in a rest site, event, map, or other run context where those systems do not exist. The explicit out-of-combat handlers use legitimate run commands or reward screens for gold, potion/card/relic rewards, healing, max HP, and deck upgrade/remove/transform operations. Unsupported combat-only effects intentionally warn and do nothing. Expanding them without defining targets and combat state would create invalid or misleading behavior, so this restriction was left unchanged.

## Verification

- `dotnet build mods\card_editor\card_editor.csproj -c Release`
  - PASS: 0 warnings, 0 errors.
- `dotnet run --project tools\CardEditor.TestHarness\CardEditor.TestHarness.csproj -c Release`
  - PASS: 40/40 tests; 2,187 beta and mod models loaded.
  - New runtime regression verifies initial `-2 Strength`, two stacks at `-4`, stack reduction back to `-2`, full restoration to `0`, and absence of an event-listener carrier for a passive-only status.
  - New registry regression checks every effect kind against independent publisher and consumer contracts.
- `tools\CardEditor.EngineTests\run-engine-tests.ps1`
  - PASS in the real Godot/game runtime.
  - New UI regression verifies the trigger is available in Status Behavior, Power mode is forced, Self/bearer is the only target, and serialization preserves the configuration.

## Evidence boundary

The automated tests establish model/runtime lifecycle behavior and actual Godot editor authoring behavior. They do not constitute a manual combat playthrough against every Artifact, cleanse, multiplayer, or mod-interaction combination. No Steam Workshop upload or Steam mod-folder deployment was performed as part of this change.
