# Result Pile Destination Bug and 10.1.1 Release

Date: 2026-08-30

## Hypothesis 1: The runtime ignores the configured Result Pile

**Result: FALSE.**

`Hook_ModifyCardPlayResultLocation_CardEditorExtraEffects_Patch` asks
`TryGetCardPlayResultPileOverride` for the saved destination and replaces the vanilla
`CardLocation`. The runtime applies the value present in the serialized effect.

## Hypothesis 2: The editor always saves Hand

**Result: TRUE.**

Both `BuildOverrideFromUi` and `BuildUpgradeOverrideFromUiDeltas` initialized
`moveToPile` to `Hand`, but their pile-configuration gates omitted
`CardExtraEffectKind.ResultPileOverride`. The visible destination controls therefore
never updated the values written to `CardExtraEffect.MoveToPile` and
`CardExtraEffect.MoveToPosition`.

## Fix

- Added `ResultPileOverride` to both pile-action configuration gates in the base-card
  editor save path.
- Added the same coverage to the upgraded-card delta save path.
- Added a headless source-contract regression that requires both gates in both paths.
- Bumped all release manifests from `10.1.0` to `10.1.1`.

## Verification

- `tools/CardEditor.TestHarness/run-tests.ps1`: `RESULT 13/13 tests passed; 2187 beta and mod models loaded.`
- Release build: `0 Warning(s)`, `0 Error(s)`.
- Release DLL SHA-256: `3FE7D8CF820D8D92CB7B0E269566A8C0E50DD5C60A3A1CBAF967DDA06B8DECDD`.
- Release PDB SHA-256: `CA7B089511EC6F9D7FC8E3B634A86CC108D1D2468A2262EFF3CA236D364AC249`.
- DLL/PDB hashes match in build output, `built cfiles`, the pack mirror, and the live
  Steam mod folder.

## Boundary

The automated test proves that both editor builders route Result Pile through the
selected destination controls. It does not automate a full rendered click-through in
Godot. The existing runtime patch is separately source-verified, and the complete
Hand/Draw/Discard/Exhaust movement matrix passes in the headless beta harness.
