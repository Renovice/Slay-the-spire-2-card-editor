# Card Editor User Report Triage - 2026-09-19

## Scope

This report deduplicates the 12 supplied Steam comment screenshots, checks each
claim against the Card Editor 10.1.4 source at commit `356dea1`, and records the
implementation and offline verification work completed afterward in the same
working tree.

The repository targets Slay the Spire 2 beta `v0.111.0`. The locally installed
game is stable `v0.107.1` (`59260271`), so a valid target-beta in-game or
two-player reproduction could not be run today. The repository's headless beta
harness uses the tracked `v0.111.0` assemblies and now passes 31/31 tests with
2,187 beta and mod models loaded.

## Evidence Rules

- `TRUE - DEFECT`: current source directly supports the reported incorrect behavior.
- `TRUE - LIMITATION`: the requested behavior is not implemented in the current model.
- `SOURCE FIX PRESENT`: current source has an implementation intended to address the report, but no live target-beta proof was obtained here.
- `FALSE IN CURRENT SOURCE`: current source exposes the requested path; a live UI check may still be warranted.
- `UNCONFIRMED`: source inspection cannot establish the report or its root cause; a focused reproduction is required.
- `REQUEST`: a feature request rather than a defect.

Source proof, headless harness proof, real Godot/UI proof, and live multiplayer
proof are separate evidence levels throughout this report.

## Executive Inventory

| ID | Deduplicated user report | Verdict | Priority |
|---|---|---|---|
| CE-101 | Potion that adds a card to hand causes multiplayer desync | SOURCE FIX + HEADLESS READY-GATE PASS; two-client live proof pending | P1 |
| CE-102 | Reset preset needs confirmation and is adjacent to Save | FIXED + SOURCE-CONTRACT PASS | P1 |
| CE-103 | Old Outbreak cannot be built because Whenever lacks Apply Poison | HEADLESS REGISTRY PASS; live menu visibility pending | P3 |
| CE-104 | Card-level VFX preset appears only once on a multi-hit attack | SOURCE FIX + COMMAND-BINDING PASS; live visual proof pending | P2 |
| CE-105 | Damage from an additional effect has no VFX | SOURCE FIX + ALL DIRECT-BRANCH CONTRACT PASS; live visual proof pending | P2 |
| CE-106 | Card Damage Bonus source changes enemy Thorns damage | FIXED + HEADLESS PASS | P1 |
| CE-107 | Expired power remains or revives when the same power is reapplied | NOT REPRODUCED; expiry/reapply pass and wording clarified | P2 |
| CE-108 | Merge combines duration but treats potency as separate stacks | CONTRACT VERIFIED + TOOLTIP CLARIFIED | P2 |
| CE-109 | Non-Applied source modes cannot use vanilla card values/actual total damage | FIXED + HEADLESS PASS | P1 |
| CE-110 | Cannot model Enfeebling Touch-style passive negative Strength | TRUE - LIMITATION / workflow ambiguity | P2 |
| CE-111 | Draw Cards with Selected Row never works | HEADLESS PRODUCER/CONSUMER PASS; live UI proof pending | P2 |
| CE-112 | Auto Action cannot be granted and custom keywords cannot be granted | TRUE - LIMITATION | P2 |
| CE-113 | AoE Vanilla Damage source misses Vigor/Vulnerable adjustments | FIXED + REAL BETA COMMAND PASS | P1 |
| CE-114 | Mad Science is not editable | TRUE - deliberate current exclusion | P3 |
| CE-115 | No exact trigger for "when bearer is hit by an attack" | IMPLEMENTED + FULLY-BLOCKED RECEIVER PASS | P2 |
| CE-116 | Add custom characters and a custom enchantment creator | REQUEST | Backlog |

The screenshots contain 16 distinct reports after removing the duplicated final
screenshot. Fifteen are bug/behavior/capability reports and one is a wishlist
request. The implementation pass fixed or added focused coverage for CE-101
through CE-109 where applicable, CE-111, CE-113, and CE-115. CE-110, CE-112,
CE-114, and CE-116 remain deliberate capability work rather than regressions.
CE-109 and CE-113 were two symptoms of the same source-value architecture gap.

## Implementation Update - 2026-09-19

### Implemented

- Reset reuses `CardEditorConfirmPopup`; no state mutation occurs before acceptance.
- Card Damage Bonus requires a real card source, so cardless retaliation cannot
  inherit the ambient played card.
- Completed vanilla `DamageResult` values are captured per `CardPlay`, sealed
  before extra rows, and exposed to HP/blocked/total/overkill/instance/kill modes.
- Client Ready is retained until the first authoritative host snapshot applies;
  the client requests that snapshot immediately and then replays vanilla Ready once.
- Target VFX presets bind to `AttackCommand`'s actual per-hit loop. The four
  direct `CreatureCmd.Damage` branches used by shared/dynamic extra damage also
  play the configured VFX at their real hit seam.
- `Bearer Hit by Attack` is a new serialized trigger for power and card-hosted
  rows. It checks completed receiver identity, including fully blocked attacks.
- Multiplayer sync protocol is now v4 because the new trigger has serialized
  receiver-specific semantics.
- Power Uses/Turns and Merge tooltips now state the actual listener/stack contract.

### Offline Verification

```text
RESULT 32/32 tests passed; 2187 beta and mod models loaded.
```

The focused tests cover cardless Thorns attribution, actual AoE damage with
target-specific Vulnerable and cast-time Vigor, Selected Row chaining, power
expiry/reapply, Merge potency, Apply Poison power capability, fully blocked
bearer hits, VFX command binding/direct branch hooks, reset ordering, and the
deferred Ready -> vanilla network dispatch transition.

### Evidence Boundary

- The VFX tests prove authoritative command/path binding, not rendered pixels.
- The Ready test uses the real beta lobby and network message types, but simulates
  the successful-snapshot state transition because full snapshot application
  touches Godot `user://`. It is not a two-client lockstep run.
- The installed stable game cannot run the beta-gated Godot engine test. Live
  popup layout, Apply Poison menu visibility, VFX rendering, and two-client
  potion identity remain acceptance gates.

## Initial Detailed Findings (Pre-Implementation)

The sections below preserve the evidence and conclusions that led to the work.
Their original `Result` paragraphs describe commit `356dea1` before the changes;
the implementation table and evidence boundary above are the current status.

### CE-101 - Potion-generated card multiplayer desync

**Hypothesis:** potion card generation diverges because peers do not have the
same Card Editor definitions or candidate set when the synchronized game action
runs.

**Evidence:**

- `mods/card_editor/CardEditorMultiplayerSync.cs` synchronizes editor definitions,
  created cards, keywords, statuses, and relics. It does not synchronize every
  potion's runtime outcome as a separate Card Editor message.
- The current Ready path allows the player to ready before the host snapshot is
  applied. It requests the snapshot and logs that definitions may differ until
  it arrives (`CardEditorMultiplayerSync.cs:296-331`).
- The headless test proves that Ready reaches the vanilla network dispatcher. It
  does not prove that both peers have applied identical editor data before card
  generation.
- The optional desync-protection bypass suppresses checksum comparison. That can
  hide evidence of divergent state and must not be treated as a fix.

**Result:** UNCONFIRMED, but credible. The strongest current lead is the
fail-open Ready-before-snapshot window, mixed mod versions, or different content
snapshots. A two-peer target-beta reproduction must record mod version, snapshot
hash, ready timing, potion model, RNG/action sequence, and both resulting card
IDs. The authoritative fix should gate readiness/use on synchronized data or
make the generated outcome part of the vanilla synchronized action; it should
not add a polling or correction loop.

### CE-102 - Destructive Reset has no confirmation

**Hypothesis:** clicking Reset immediately destroys unsaved preset/editor state.

**Evidence:**

- Save and Reset are adjacent in `mods/card_editor/NCardEditorPresetPanel.cs:619-640`.
- Delete already uses `CardEditorConfirmPopup` at lines 1148-1168.
- `OnVanillaPressed` at lines 1184-1223 immediately clears relic, creator, card,
  and base-deck state without a confirmation step.

**Result:** TRUE. This is a high-risk UX defect with a direct data-loss path.
The future fix should put the existing confirmation popup at the Reset handler,
with no state mutation before acceptance.

### CE-103 - Apply Poison unavailable for Whenever / old Outbreak

**Hypothesis:** Apply Poison cannot be used as a Whenever power effect.

**Evidence:**

- `CardEditorEffectKindRegistry.cs` registers `ApplyPoison` with power/repeat
  capabilities.
- `SupportsAsPower` in `CardEditorExtraEffects.cs:5579-5606` does not exclude
  `ApplyPoison`.
- `Whenever (Event)` is present in the trigger model and is available to power
  effects.

**Result:** FALSE IN CURRENT SOURCE. The capability exists in 10.1.4. The report
may describe an older build or a UI discoverability problem. This still needs a
live editor check to ensure the current menu actually exposes Apply Poison after
Power is enabled.

### CE-104 - Multi-hit attack VFX plays only once

**Hypothesis:** the cosmetic preset is bound to the card play, not each damage hit.

**Evidence:**

- `CardEditorCosmetics.cs:185-228` executes cosmetics once per card play.
- `TryPlayVfxPreset` at lines 363-470 spawns one preset for each resolved target,
  not for each hit in the damage command.
- There is no hit-index callback or `WithHitVfx` binding in this path.

**Result:** TRUE - LIMITATION. The observed one-VFX-per-card/target behavior is
what the current architecture implements. A per-hit option needs to bind to the
authoritative damage command's hit lifecycle, not replay a timer-based cosmetic.

### CE-105 - Additional-effect damage has no VFX

**Hypothesis:** extra damage effects do not inherit the card-level cosmetic preset.

**Evidence:**

- Additional `DealDamage` uses `DamageCmd.Attack(...).WithHitCount(repeats)` in
  `CardEditorExtraEffects.cs:29346`.
- That command is not associated with `CardEditorCosmetics` or a per-effect VFX
  setting.
- Broad empty catches at `CardEditorCosmetics.cs:357-360` and 467-470 can also
  hide cosmetic failures instead of producing actionable diagnostics.

**Result:** TRUE - LIMITATION. Additional-effect damage currently has no defined
VFX inheritance contract. When implemented, VFX selection should belong to the
effect/damage command, with failures logged rather than silently swallowed.

### CE-106 - Card Damage Bonus changes enemy Thorns

**Hypothesis:** ambient card-play context makes cardless retaliatory damage look
like damage from the currently played card.

**Evidence:**

- Vanilla `ThornsPower.cs:17-23` calls `CreatureCmd.Damage` with
  `cardSource=null` and `cardPlay=null`.
- `CardEditorCardDamageBonusPatches.cs:21-46` patches global damage modification
  and asks for an effective source card.
- `CardEditorIgnoreCapsAndNegationPatches.cs:99-119` falls back from a null
  `cardSource` to `CardEditorCardPlayContext.Current.Card`.
- Thorns can execute while that ambient attack-card context is still active, so
  its independent retaliation inherits the attack card's source multiplier.

**Result:** TRUE. This is a source-attribution defect. The fix must require a
real card-origin damage source/card play or authoritative powered-attack
relationship; unrelated damage must not inherit ambient card context.

### CE-107 - Power does not expire and old value returns on reapply

**Hypothesis:** expired listener entries or their visible mirror powers remain
and are merged back into a later application.

**Evidence:**

- `CardEditorExtraEffectPower.cs:1176-1187` removes entries that reach their
  maximum fire count.
- Lines 1769-1823 increment turn lifetime and remove entries at maximum turns.
- `SyncVisibleMirrorPowers` at lines 617-783 contains stale-mirror removal.
- Power Turns and Trigger Uses govern the Card Editor listener. They do not undo
  persistent vanilla effects, such as Strength already granted by an earlier
  trigger. A surviving Strength value is therefore not by itself proof that the
  listener failed to expire.

**Result:** UNCONFIRMED. Current source contains removal paths, but there is no
focused test for expire -> inspect carrier/mirror -> reapply. Reproduction must
distinguish the Card Editor listener icon/entry from the lasting effect it
previously applied. If the listener/mirror remains, it is a defect. If only the
previously applied vanilla power remains, that is current action semantics and
the UI needs clearer wording or an explicit reversible-duration feature.

### CE-108 - Merge duration and potency semantics

**Hypothesis:** Merge combines duration but executes most effects as independent
potency stacks.

**Evidence:**

- `CardEditorExtraEffectPower.cs:415-420` increments `StackCount` and sums
  remaining turn/use limits.
- Only fixed Gain Energy/Gain Stars receives special merged amount scaling.
- `ExecuteOrSchedulePowerEffect` at lines 1073-1086 executes most other effects
  once per stack.
- The UI only says "Merge matching power effects into one stacked entry" in
  `NCardEditorPopup.cs:20424-20443`; it does not define how potency should merge.

**Result:** TRUE as a description of current behavior. Whether it is a runtime
bug depends on the intended contract, which is currently not explicit. The
implementation and tooltip must converge on one rule: aggregate amount once, or
run one effect instance per stack.

### CE-109 - Vanilla values unavailable to total/other amount modes

**Hypothesis:** only Applied Effect can read a vanilla card dynamic value, while
Total Damage and related sources only consume Card Editor effect-row results.

**Evidence:**

- `CardEditorExtraEffects.cs:28387-28438` resolves vanilla dynamic variables for
  `AppliedEffectRow`.
- Total Damage and related modes query Card Editor execution-session rows.
- Damage results are reported to that session by Card Editor extra effects. The
  vanilla card's base damage command is not captured as a named source row.
- Therefore actual aggregate vanilla multi-hit/AoE damage cannot currently be
  selected as Total Damage without recreating damage in a separate effect.

**Result:** TRUE - LIMITATION. This is not just a missing dropdown option; the
runtime lacks an authoritative captured result for the vanilla card's completed
damage. A robust implementation must capture the actual command results for the
current card play, including hit/target identity, rather than recomputing text
values after the fact.

### CE-110 - Enfeebling Touch-style passive stat modifier

**Hypothesis:** a custom power cannot continuously modify Strength without an
event trigger.

**Evidence:**

- Generic `As Power` rows install event listeners and therefore require a trigger.
- `LoseStrength` is supported as a triggered effect, but it is not a passive
  stat-calculation hook.
- Applying vanilla Enfeebling Touch or a purpose-built custom status is a
  different path from making a generic no-trigger power effect.

**Result:** TRUE - LIMITATION / workflow ambiguity. The generic effect-power
model cannot express a passive modifier while the status exists. The current
workaround is to apply an existing suitable power/status. A future general
solution should expose authoritative stat-modifier hooks in the Status Editor,
not simulate them with repeated trigger actions.

### CE-111 - Draw Cards with Selected Row

**Hypothesis:** Draw Cards ignores the selection produced by a prior effect row.

**Evidence:**

- Current `DrawMatchingCards` detects `SelectedByEffect` and gets candidates from
  the selection bus (`CardEditorExtraEffects.cs:31391-31515`).
- `GetCandidatesFromSelectedEffectSource` is implemented at lines 32489-32519.
- Fetch publishes its selection at lines 32906-32909.
- The registry declares Draw Cards as a selection consumer.
- No focused Selected Row Draw test exists in the current 21-test harness.

**Result:** SOURCE FIX PRESENT. Current source contains the expected producer to
consumer path, so the old report is not proven against 10.1.4. Add a focused
headless test and then a target-beta in-game chain test before marking it fixed.

### CE-112 - Grant Auto Action and custom keywords

**Hypothesis:** Auto Action is deliberately excluded from Grant, and Grant
Keyword only carries vanilla enum values.

**Evidence:**

- `SupportsGrantToCard` in `CardEditorExtraEffects.cs:5635-5667` explicitly
  excludes Auto Play, Auto Draw, and conditional auto-action kinds.
- These kinds depend on dedicated trigger/runtime machinery and would be inert
  if copied as ordinary rows.
- `GrantKeywordToCards` at lines 32178-32287 uses the vanilla
  `GrantedKeyword` enum. It has no custom-keyword identity payload.

**Result:** TRUE - LIMITATION. Exposing the existing Grant checkbox alone would
not work. Grantable Auto Action needs its trigger state serialized and installed
on the destination card; custom keyword grant needs a stable custom keyword ID
and runtime add/remove support.

### CE-113 - AoE vanilla source misses Vigor/Vulnerable

**Hypothesis:** the source system recomputes one dynamic damage value after play
instead of consuming actual per-target cast-time results.

**Evidence:**

- `TryResolveVanillaDynamicAmountSource` in
  `CardEditorExtraEffects.cs:28485-28546` reads a dynamic variable and manually
  calls `Hook.ModifyDamage`.
- It passes `cardPlay.Target` and normal preview mode at lines 28509-28522.
- An AoE play normally has no single `cardPlay.Target`.
- Vanilla `Hook.ModifyDamage` only fans out null-target calculation in
  multi-creature targeting preview mode; Vulnerable requires the actual target
  to be its owner.
- Cast-time resources such as Vigor may already have been consumed before
  after-play extra effects recompute the value.

**Result:** TRUE. This explains both missing target-specific Vulnerable and
missing cast-time Vigor. It shares the root cause of CE-109. Capture the actual
vanilla damage command results during execution; do not attempt to reconstruct
them afterward from a null target and current powers.

### CE-114 - Mad Science not editable

**Hypothesis:** the editor only enumerates cards marked for the card library, and
Mad Science opts out of that library.

**Evidence:**

- `NCardEditorSubmenu.cs:60-70` enumerates only cards where
  `ShouldShowInCardLibrary` is true.
- Vanilla `MadScience.cs:167-169` sets `shouldShowInCardLibrary:false`.

**Result:** TRUE - deliberate current exclusion. This is not a failed load; it
is filtered out by the browser query. Supporting it safely should be a general
hidden/internal-card browser mode with warnings, not a one-card exception.

### CE-115 - Exact "when bearer is hit by an attack" trigger

**Hypothesis:** After Attack does not identify whether this power's owner was one
of the hit receivers.

**Evidence:**

- `AfterAttack` exists in `CardEditorExtraEffects.cs:751-778`.
- `CardEditorExtraEffectPower.AfterAttack` at lines 1638-1683 dispatches from
  the command attacker/results but does not require `Owner` to be among the
  receivers.
- A broad Any Enemy trigger can therefore observe an enemy attack against a
  different player in multiplayer. Damage-received triggers can miss a fully
  blocked hit.

**Result:** TRUE - LIMITATION. Current triggers approximate parts of Thorns but
cannot express "this bearer was targeted and hit by a powered attack, even when
fully blocked." The correct predicate belongs on the completed attack command's
receiver results.

### CE-116 - Custom characters and enchantment creator

**Hypothesis:** this is a request for new editor domains, not a malfunction.

**Evidence:** the comment explicitly describes future desired functionality and
does not identify existing behavior that fails.

**Result:** REQUEST. Track separately from bug fixes so it does not distort the
release-defect count.

## Existing Harness Result

Command:

```powershell
& ".\tools\CardEditor.TestHarness\run-tests.ps1"
```

Result on 2026-09-19:

```text
RESULT 32/32 tests passed; 2187 beta and mod models loaded.
```

Relevant passes include pile movement, manual Exhaust, filtered Transform, all
public Whenever cost-reduction dispatchers, After Death, Run Effect Source,
Selected Row chaining, cardless retaliation isolation, actual vanilla AoE result
capture, power expiry/reapply and Merge, recursively unpowered delayed
damage/Block payloads, Bearer Hit by Attack, Apply Poison power capability, VFX
command/direct-path wiring, deferred Ready reaching the vanilla network
dispatcher, Reset confirmation ordering, Result Pile save, single Match Energy
registration, and the card-text auto-size source contract.

The Sentry GDExtension message emitted by this harness is expected because it is
not a Godot process. It is not a test failure. No other warnings or failures were
reported.

## Remaining Verification Gates

1. Run the Godot engine test against an installed beta `v0.111.0` build and
   inspect the Reset dialog, Apply Poison menu, and generated trigger text.
2. Render a multi-hit vanilla attack and each direct extra-damage branch with a
   target VFX preset; count visible effects against actual hit results.
3. Run two clients with identical v4 builds. Prove Ready stays blocked until the
   host snapshot applies, then use the reported add-card potion and compare the
   generated card IDs plus lockstep checksum on both peers.
4. Exercise the new Bearer Hit by Attack trigger on player and enemy hosts in a
   live combat, including a fully blocked hit.

## Remaining Work Order

1. Complete the four live acceptance gates above on a matching beta install.
2. Design CE-112 as serialized grant state; merely exposing its disabled checkbox
   would create inert Auto Actions and lossy custom-keyword grants.
3. Design CE-110 as an authoritative custom status stat-modifier capability.
4. Add a warned hidden/internal-card browser mode for CE-114 rather than a
   one-card Mad Science exception.
5. Keep CE-116 as separate editor-domain work.

Do not solve CE-101, CE-106, CE-109, CE-113, or CE-115 with polling, ambient
state repair, or per-frame enforcement. Each has an authoritative command,
source attribution, readiness gate, or completed-result hook that should own the
behavior.

## Local Test Deployment

Deployed on 2026-09-19 to:

```text
C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\Card Editor2
```

The game was not running during deployment. The installed game identified
itself as beta `v0.111.0` commit `41cef1ea`, matching the mod manifest minimum.
The C# project was built in Release configuration with stable .NET SDK
`9.0.314`; the build completed with zero warnings and zero errors. No `.pck`,
asset, user-setting, or preset file was changed.

The exact Release DLL/PDB were copied to the live mod folder and both repository
package mirrors (`built cfiles` and `mods/card_editor_pack/mods/card_editor`).
All four copies, including the build output, were hash-identical:

```text
card_editor.dll  5E624C9121633DF38F8347E1C57F5673EA0CF18CEFC5EE96DB9AF11907ED808F
card_editor.pdb  C6D197238FD344031084C5223D3547176CE656D002B06B5F2E9463ABB6E0DFC2
```

The prior live and package binaries plus manifests were preserved at:

```text
RESEARCH/deployment-backups/20260919-123730-before-local-card-editor-test
```

This proves a clean build and exact local deployment. It does not by itself
prove the remaining live combat, rendered VFX, or two-client multiplayer
acceptance gates listed above, and it is not a Steam Workshop upload.

## Power Payload Isolation Fix and Redeployment

**Hypothesis:** the ordinary `As Power` path isolated only the outer damage or
Block row, leaving nested branch rows and custom-status behavior rows able to
inherit live Strength, Dexterity, Vigor, Weak, Vulnerable, Frail, and similar
damage/Block modifiers when they triggered later.

**Result:** TRUE, FIXED. `CloneForPowerExecution` now recursively marks delayed
damage and Block rows throughout the branch tree as unpowered. Custom-status
behavior installation now uses the same power-execution clone path. Immediate
card effects remain powered, while an explicitly selected dynamic Value Source
continues to resolve from current combat state by design.

The focused regression installs +5 Dexterity, triggers a nested power branch
configured to gain 3 Block, and proves that it gains exactly 3 rather than 8.
It also proves that installation does not mutate the configured source effect
and that custom-status damage plus its nested Block branch are stored as
unpowered. Full harness result:

```text
RESULT 32/32 tests passed; 2187 beta and mod models loaded.
```

The stable .NET SDK `9.0.314` Release build completed with zero warnings and
zero errors. Its DLL/PDB were deployed to the live `Card Editor2` folder,
`built cfiles`, and the pack mirror; every destination matched the build output:

```text
card_editor.dll  32B564895C118AF8F671354601CFADDFC026600FB03C279CD43A474D89FFC609
card_editor.pdb  C49CD06007892E24DA01B4DDC20C5A108304FA03AC2F83ADA49328D8D24AE7B7
```

The immediately preceding local test build was preserved at:

```text
RESEARCH/deployment-backups/20260919-124823-before-power-payload-fix
```
