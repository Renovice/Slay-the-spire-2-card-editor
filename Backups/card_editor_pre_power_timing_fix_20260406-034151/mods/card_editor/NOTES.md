# Card Editor Mod – Developer Notes

## Rule: Understand before implementing

Before coding any new feature, always first understand how **both** the base game (STS2) and the mod currently handle the relevant mechanic. Examples:

- **Damage calculations**: STS2 applies Strength, Weak, Vulnerable, Block etc. through its own pipeline. Our `ExecuteEffect` calls into that pipeline via `DealDamageCmd` – don't re-implement damage math manually.
- **Card variations / upgrades**: STS2 cards track upgrade level on `CardModel`. The editor stores base + upgrade delta effects separately and resolves them via `GetEffectiveExtraEffects`. Always check how an effect differs between base and upgraded before assuming it "just works".
- **Powers**: STS2 powers are persistent `PowerModel` instances attached to creatures. Our `CardEditorExtraEffectPower` is a single invisible power that stores a list of entries. New entries are added when a card is played; entries are removed on expiry. Never bypass this – scheduling deferred effects yourself will fight the power system.
- **Scheduling**: One-shot deferred effects (e.g. "at the start of your next turn, deal damage") go through `CardEditorExtraEffectScheduler`. Repeating effects MUST use the power system (`AsPower = true` + a trigger).

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
