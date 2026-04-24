# Card Editor Mod – Developer Notes

## Rule: Understand before implementing

Before coding any new feature, always first understand how **both** the base game (STS2) and the mod currently handle the relevant mechanic. Examples:

- **Damage calculations**: STS2 applies Strength, Weak, Vulnerable, Block etc. through its own pipeline. Our `ExecuteEffect` calls into that pipeline via `DealDamageCmd` – don't re-implement damage math manually.
- **Card variations / upgrades**: STS2 cards track upgrade level on `CardModel`. The editor stores base + upgrade delta effects separately and resolves them via `GetEffectiveExtraEffects`. Always check how an effect differs between base and upgraded before assuming it "just works".
- **Powers**: STS2 powers are persistent `PowerModel` instances attached to creatures. Our `CardEditorExtraEffectPower` is a single invisible power that stores a list of entries. New entries are added when a card is played; entries are removed on expiry. Never bypass this – scheduling deferred effects yourself will fight the power system.
- **Scheduling**: One-shot deferred effects (e.g. "at the start of your next turn, deal damage") go through `CardEditorExtraEffectScheduler`. Repeating effects MUST use the power system (`AsPower = true` + a trigger).

---

## Rule: Do not touch the original compendium by default

- Do **not** patch the vanilla compendium / card library rendering path unless there is no safer alternative.
- Canonical library cards are immutable preview models. Never assume they have combat ownership, mutable state, or a valid `Owner` / `CombatState`.
- Any shared text/targeting/helper patch must explicitly bail out for non-mutable canonical cards unless the feature is intentionally supposed to affect the vanilla library.
- Prefer Card Editor popups, Card Editor-created previews, or Card Editor-owned cards over hooks that run on the base compendium.

---

## Power compatibility traps

- Do **not** assume every vanilla `PowerModel` is safe on both players and enemies.
- Some powers are really enemy encounter scripting wrapped in a power. They depend on `Owner.Monster`, monster move-state machines, enemy-specific classes, or `CreatureCmd.Stun(...)`.
- Vanilla stun is **monster-only**. `Creature.StunInternal(...)` throws if `Monster == null`, so a player cannot be stunned through the vanilla stun pipeline.
- If a created card grants one of these powers to the player, the result is not a "normal debuff". It can break combat flow or freeze the turn.

### Confirmed unsafe on players

These powers should be treated as player-unsafe unless we add a dedicated compatibility layer:

| Power | Why it is unsafe |
|---|---|
| `FlutterPower` | Uses `Owner.Monster.MoveStateMachine`, calls `CreatureCmd.Stun(...)`, casts to `ThievingHopper` |
| `AsleepPower` | Casts `Owner.Monster` to `LagavulinMatriarch`, calls `CreatureCmd.Stun(...)` |
| `SlumberPower` | Casts `Owner.Monster` to `SlumberingBeetle`, calls `CreatureCmd.Stun(...)` |
| `BurrowedPower` | Calls `CreatureCmd.Stun(...)` after enemy-specific animation flow |
| `ImbalancedPower` | Checks `Owner.Monster is BowlbugRock`, calls `CreatureCmd.Stun(...)` |
| `PlowPower` | Casts `Owner.Monster` to `CeremonialBeast`, calls `CreatureCmd.Stun(...)` |
| `RavenousPower` | Casts `Owner.Monster` to `CorpseSlug`, calls `CreatureCmd.Stun(...)` |
| `ShriekPower` | Casts `Owner.Monster` to `TerrorEel`, calls `CreatureCmd.Stun(...)` |

### Likely unsafe on players

These also depend on monster-only state and should be treated carefully:

| Power | Why it is suspicious |
|---|---|
| `IllusionPower` | Uses `Owner.Monster.MoveStateMachine` and `SetMoveImmediate(...)` |
| `ReattachPower` | Uses `Owner.Monster.SetMoveImmediate(...)` and segment-specific logic |
| `CurlUpPower` | References monster-specific classes (`LouseProgenitor`) |

### Mirror problem: player-only powers on enemies

- There is also an opposite class of bugs: powers that assume `Owner.Player` / `Owner.Player.PlayerCombatState`.
- Example candidates seen during investigation: `CoolantPower`, `HexPower`, `TangledPower`.
- If we ever add a safety filter, it should work in **both** directions:
  - monster-only powers blocked on players
  - player-only powers blocked on enemies

### Current recommendation

- Leave vanilla behavior alone for now.
- Do not try to make monster-only powers "work" on players one by one unless there is a strong reason.
- Preferred future fix path:
  1. Add an editor/runtime compatibility filter for unsafe powers.
  2. Optionally add a mod-side "unstick combat" recovery tool for bad states.
  3. Only build custom replacements if we truly want player-side versions of monster mechanics.

---

## Power trigger system (CardEditorExtraEffectPower)

| Field | Meaning |
|---|---|
| `TriggerEveryN` | Fire only every Nth hit of the trigger condition (0/1 = every time) |
| `TriggerCounter` | Running count of times trigger condition was met (used for EveryN) |
| `TriggerMaxFires` | Remove power entry after this many fires (0 = unlimited / permanent) |
| `TriggerFireCount` | Running count of actual fires (used for MaxFires check) |

### Duration semantics in power mode

| Duration | Behaviour |
|---|---|
| Permanent (0) | Entry stays until combat ends |
| This Turn (1) | Entry is removed at the end of the turn it was created |
| *(MaxFires > 0)* | Entry is removed after firing N times (independent of Duration) |

### Text generation

- `StartOfTurn` / `EndOfTurn` + `TriggerMaxFires = 3` → "For the next 3 turns, at the start of your turn, …"
- `OnPlay` + `TriggerMaxFires = 3` → "The next 3 times you play a card, …"
- `TriggerEveryN = 2` → "Every 2 cards you play/draw/…"
- `Duration = ThisTurn` (non-stat-buff) → "This turn, …" prefix

---

## Key files

| File | Role |
|---|---|
| `CardEditorExtraEffects.cs` | All card data types, text generation, effect execution |
| `CardEditorExtraEffectPower.cs` | Persistent power that fires repeated trigger effects |
| `NCardEditorPopup.cs` | UI – the main card editor popup |
| `CardEditorExtraEffectScheduler.cs` | One-shot deferred effect scheduling |
| `CardEditorTemporaryExtraEffectController.cs` | Temporary per-card effects (e.g. cost reductions) |

---

## Build & deploy

```powershell
cd mods\card_editor
dotnet build card_editor.csproj -c Release
# Then copy build\net9.0\card_editor.dll to:
#   ../../built cfiles/card_editor.dll
#   C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\Card_editor\card_editor.dll
```
