# Card Editor Vanilla-Parity and Grant Fixes

Date: 2026-09-19

## Scope

This pass investigated and fixed these user reports:

1. A vanilla AOE card's `Vanilla: Damage > Source Effect` did not include target-specific changes such as Vigor and Vulnerable.
2. Auto Action effects did not expose or execute `Grant` correctly.
3. `Card Action > Grant Keyword` could select vanilla keywords but not Card Editor custom keywords.
4. Power, delayed, countdown, quest, and relic payloads could inherit live card Strength/Dexterity-style modifiers after the originating card had already resolved.
5. Nearby count-event, targeting, and death-trigger paths had avoidable departures from current beta APIs or vanilla ownership semantics.

## Hypotheses and Results

### H1: AOE Source Effect was reading a static card value instead of actual per-target results

Result: TRUE in the old path; FIXED in the current source.

- Damage-result sources now consume the actual `DamageResult` values produced by the vanilla play.
- The calculation sums target-specific HP damage, blocked damage, overkill, instances, and kills as requested.
- It therefore includes Vigor, Vulnerable, per-enemy modifiers, and other changes already applied by the game.
- A regression test uses a real vanilla AOE card with two enemies, Vulnerable on one target, and Vigor on the player. The source amount must equal the actual target-specific result sum and must not count later extra-effect damage.

### H2: Auto Action lacked Grant only because of a UI omission

Result: FALSE. It was both a UI capability omission and a runtime dispatch/package problem.

- Conditional Auto-Play, Auto-Draw, and Auto-Run are now grantable capabilities.
- Grant dispatch now runs before Auto Action execution, so a grant row is not swallowed as a local auto action.
- Auto-Run transfers its wrapper and referenced payload rows as one package.
- Package effect IDs are remapped, payload rows are marked payload-only, and duplicate keys remain stable.
- Future-matching-card auras use the same package path.
- The unsafe `Hits All Enemies` grant remains blocked because granting it to a vanilla single-target card can remove the card target and soft-lock resolution.

### H3: Custom keywords could not be selected by Card Action

Result: TRUE; FIXED.

- The keyword picker now combines vanilla enum keywords with explicit and discovered Card Editor custom keywords.
- Selecting a custom keyword persists its name through both normal and upgrade UI serialization.
- Runtime grant resolves the custom definition and transfers all behavior rows as one remapped package.
- The package is tagged with the custom keyword name and receives a stable duplicate key.
- `Remove instead` stays hidden for custom keywords because vanilla removal suppresses an enum flag, while a custom keyword is an effect package. Treating those as the same operation would be misleading.

### H4: Persistent/non-card effects should keep recalculating from current Strength or Dexterity

Result: FALSE for vanilla parity; FIXED.

- Normal card execution and behavior granted directly to a card remain powered and can use current card modifiers.
- Power, scheduler, countdown, recurring quest, and relic-hosted payloads are cloned for non-card execution.
- X is captured once when required, and nested damage/block payloads are marked unpowered.
- A power that was created as `deal 3 damage at end of turn` therefore remains 3 when Strength changes later, unless the authored effect explicitly scales from another source.

### H5: Nearby parity gaps were isolated to the three reports

Result: FALSE. Five additional concrete gaps were corrected.

- `Current Turn Number` now reads the owning player's turn number, with combat round only as a fallback.
- Amountless targeted effects such as Cleanse Debuffs now request a target instead of being discarded by an `Amount > 0` gate.
- Stars Spent uses the current public `Hook.AfterStarsSpent` beta hook and awaits trigger dispatch.
- Relics now distinguish `When an enemy dies` from `When you kill an enemy`; indirect poison/scripted deaths fire only the former.
- All capability profiles still pass the in-game registry consistency audit.

## Validation

### Headless beta regression suite

Command: `tools\CardEditor.TestHarness\run-tests.ps1`

Result: PASS, 38/38 tests; 2,187 beta and mod models loaded.

Relevant passing cases:

- `Grant -> Auto Action transfers wrapper and payload`
- `Card Action -> custom keyword grants definition package`
- `Amount source: vanilla total damage uses actual AoE results`
- `Power payloads: delayed damage and block stay unpowered`
- `Parity: current turn uses the owning player's turn`
- `Parity: amountless targeted powers request a target`
- `Parity: Stars Spent uses the public game hook`
- `Parity: enemy death relic trigger includes indirect deaths`

### Real Godot/game engine self-test

Command: `tools\CardEditor.EngineTests\run-engine-tests.ps1`

Installed beta: `v0.111.0`

Result: PASS.

- Match Energy option occurrences: 1
- Auto Action exposes Grant: PASS
- Custom keyword appears selected: PASS
- Custom keyword survives UI serialization: PASS
- Custom keyword hides vanilla-only removal mode: PASS
- Longest-description render samples: 16/16 fit the card bounds
- Report: `%APPDATA%\SlayTheSpire2\card_editor\engine_ui_selftest_report.txt`
- Report SHA-256: `1E96071F70590C4B5309907BD372AD739DEF76781257296B7A668CD0F8F7D1AB`

The latest game log contains zero Card Editor warning/error/failure lines and reports that the definition, clone, save round-trip, and capability-registry consistency audit passed. Godot still emits `Invalid Task ID` during headless UI construction and renderer/resource-leak messages during process exit. The same categories occur in older ordinary game logs without this self-test, so they are recorded as engine/headless-runner noise rather than evidence that a Card Editor assertion failed.

### Build

Command: `dotnet build mods\card_editor\card_editor.csproj -c Release --nologo`

Result: PASS, 0 warnings, 0 errors.

## Deployment

Manifest version remains `10.1.4`; this was a local test deployment, not a Workshop release.

Release DLL:

- Size: `3,768,320` bytes
- SHA-256: `34A4F1BACFF4FE8C03B6869348BDF37041CEA640ADE7AEA4DA0CCDBF64538C7F`

Release PDB:

- Size: `931,116` bytes
- SHA-256: `B36B5814F21804E06036AA2873641F999D0F531F32D88C109D13FFE0058F65F1`

The DLL and PDB hashes match in all locations:

- `mods\card_editor\build\net9.0`
- `built cfiles`
- `mods\card_editor_pack\mods\card_editor`
- `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\Card Editor2`

Rollback backup:

- `RESEARCH\deployment-backups\20260919-134522-before-vanilla-parity-grants`

## Remaining Boundaries

- No Workshop upload was performed.
- Automated combat-model tests and real-engine UI tests passed, but this pass did not manually play every combination in an interactive combat.
- Host-self Auto-Play/Auto-Draw, patch-driven created-card modifiers, result-pile overrides, and render/hook-only passive markers remain intentionally non-grantable because they do not represent transferable on-play behavior.
- Quest reward execution still has a narrower supported effect subset than normal card play and remains a separate parity project.
- Custom-keyword removal is not exposed as vanilla `Remove instead`; implementing it needs explicit package-removal UX rather than pretending a custom effect package is a vanilla enum keyword.
