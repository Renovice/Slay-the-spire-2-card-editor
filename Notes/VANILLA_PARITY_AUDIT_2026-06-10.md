# Vanilla-Parity Audit (2026-06-10)

> **STATUS UPDATE (same day, parity fix wave shipped):** Tier 1 items 1-12 and 14-20 FIXED, plus
> Tier 2: start-of-turn asymmetry partially (end-of-turn side fully fixed; start-of-turn left as
> designed), RNG streams, cleanse-by-type, self-debuff tick consistency, Vulnerable persistence,
> empty rewards, pity room type, quest potion filters, and the Tier 3 text conventions
> (Channel/Evoke/Gold/Osty wording) + hover tips for applied powers/custom statuses.
> All fixes adversarially reviewed (22 checks; 3 findings fixed: cleanse IsVisible→type-based
> exclusion, pending-discount creation-round guard, enchant keyword attribution instead of
> absolute baseline).
> **DEFERRED (need design decisions):** #10 Gigantification one-stack-per-row (needs single
> AttackContext per card play or a generic latched-power preserver); #13 override-row timing
> (needs rows moved into the play wrapper like created cards — structural); half-cost snapshot
> double-dip of vanilla auras (deliberate snapshot design, ambiguous either way); co-op
> extra-turn participants filtering; moved-pile triggers during reshuffles; in-hand grid
> selector for self-inclusive discards; multi-evoke same-orb-N-times (missing feature, text is
> honest); scheduled end-of-turn effects still post-flush (power-hosted ones fixed).

10 comparison agents traced each mechanic through BOTH the mod engine and the decompiled vanilla source;
every major finding was adversarially re-verified (23 verified, 0 refuted). Detailed evidence with
file:line on both sides lives in `Notes/parity/*.md`. This file is the ranked synthesis.

The recurring root themes — most findings are instances of these five:

1. **Host asymmetry.** The same effect text behaves differently depending on WHERE it lives
   (card row vs power entry vs scheduled vs override-on-vanilla-card vs created card): different
   timing, different Vigor handling, double-firing.
2. **Postfix-after-vanilla ordering.** The mod applies its modifiers in Harmony postfixes AFTER
   vanilla's own pipeline finished, so it overrides decisions vanilla effects are supposed to win
   (free-cost powers, Rebound) and runs at the wrong combat phase (after hand flush, after
   after-play triggers).
3. **Split custom statuses.** A custom status = visible icon power + invisible behavior power; the
   seams show (Artifact checks the wrong half, behavior outlives the icon, temp trackers go negative).
4. **Hand-rolled loops missing vanilla gates.** Filtered draw skips Hook.ShouldDraw; auto-play skips
   reshuffle; deck ops skip IsRemovable/Quest filters.
5. **Text conventions** drift from STS2 wording (mostly cosmetic).

## TIER 1 — wrong numbers or broken rules, easy to hit

| # | Finding | One-line symptom | Detail file |
|---|---------|------------------|-------------|
| 1 | Power-hosted "End of turn" fires TWICE per turn; Every-N counter +2/turn | Metallicize clone gains 6 Block instead of 3; "every 2nd turn" fires every turn | triggers-timing |
| 2 | Power-hosted end-of-turn runs AFTER hand flush (card-hosted runs before) | "Gain 1 Block per card in hand" power always sees an empty hand; end-of-turn Block lands after Burn damage | triggers-timing |
| 3 | Custom Debuff statuses pierce Artifact on FIRST application (blocked on restack) | Enemy Artifact does nothing vs custom debuffs, then eats the second cast | power-application |
| 4 | Custom-status behavior keeps firing after the icon is removed (expiry/stack loss/vanilla removal) | "Burning until next turn" icon disappears but damage ticks all combat from an invisible source | power-application |
| 5 | "This turn" status trackers create phantom NEGATIVE powers | "1 Weak this turn" leaves a visible "Weak -1" that still weakens for an extra round; affects all 17 trackers + status-to-status Lose mode | power-application |
| 6 | Mod cost-INCREASE overrides vanilla free-cost powers | Free Skill charge: card still costs 1, charge consumed anyway | cost-modifiers |
| 7 | "Drawn/generated cost 1 MORE for N turns" makes cards cost 0 (the -1 = Free sentinel); ≥2 increases last 1 turn only; multi-turn discounts DOUBLE on turn 1 (half→quarter) | Cost knobs do the opposite of the text | cost-modifiers |
| 8 | Result-derived damage ("equal to damage dealt") re-adds Strength/Vigor on the main target path | With 5 Str: echo of a 15 hit deals 20; same row aimed at "other enemies" deals 15 — vanilla Omnislice marks echoes Unpowered | damage-pipeline |
| 9 | "Take X damage" (DealDamage→Self) is a POWERED, BLOCKABLE self-attack | Self-damage scales with your own Strength/Vigor and is absorbed by your Block; vanilla self-damage is Unpowered+Unblockable (the mod's LoseHp kind is correct) | damage-pipeline |
| 10 | One Gigantification stack burned PER damage row (Vigor is hack-preserved, siblings aren't) | "Deal 6. Deal 6." consumes 2 Gigantification stacks where a vanilla 2-hit consumes 1 | damage-pipeline |
| 11 | Filtered draws ignore No Draw (Hook.ShouldDraw never called) | "Draw 2 Attacks" works under Battle Trance's No Draw; unfiltered "Draw 2" is correctly blocked | draw-discard-pile-ops |
| 12 | PlayCardFromPile never reshuffles an empty draw pile | Havoc clone silently no-ops with 0 draw / 20 discard; vanilla Havoc reshuffles and plays | draw-discard-pile-ops |
| 13 | Extra rows on OVERRIDDEN vanilla cards run after the entire vanilla play sequence (after enchant/affliction OnPlay + all after-play triggers); created cards were fixed, overrides were not | "After you play a card, gain Strength" powers buff the card's own added rows; Tender-style powers nerf them | energy-xcost-play |
| 14 | Editor-set Hexed never grants Ethereal | Card shows the Hexed overlay + Ethereal tooltip but discards normally at end of turn | enchant-afflict-keywords |
| 15 | Temporary enchantments permanently leak OnEnchant keywords | "Enchant with Steady this turn" leaves Retain on the card forever; Goopy leaves Exhaust; Ember leaves Eternal | enchant-afflict-keywords |
| 16 | OrbInPosition condition reads leftmost/rightmost INVERTED (opposite of the mod's own orb actions) | "If your leftmost orb is Frost" checks the rightmost orb; condition and action halves of one card disagree | orbs-and-channel |
| 17 | Eternal cards removable, Quest cards transformable | Custom remove deletes Ascender's-Bane-style curses; transforming Spoils Map strands its map marker forever | rewards-pools-meta |
| 18 | Out-of-combat quest potion rewards ignore rarity/pool filters | "Create a Rare potion" quest gives a random common when completed on the map | rewards-pools-meta |
| 19 | Galvanized fallback only triggers on Power cards (vanilla is type-agnostic); Tainted has NO fallback at all | Same afflicted Attack self-zaps in one fight and not the next; Tainted is a silent no-op in most fights | enchant-afflict-keywords |
| 20 | ApplyPower / custom statuses get no hover tooltip on the card in hand | "[gold]Metallicize[/gold]" looks vanilla but hovering explains nothing (vanilla always shows the power tip) | text-rendering-rest |

## TIER 2 — real but situational

- **Start of turn timing depends on host**: card-hosted = BEFORE draw (sees ~empty hand), power-hosted
  and scheduled = AFTER draw. Vanilla picks per effect semantics. (triggers-timing)
- **Card EndOfTurnInHand fires one phase early** (before orb passives/Ethereal) and skips the vanilla
  Burn-style reveal + self-discard; scheduled end-of-turn effects fire after flush/Burn. (triggers-timing)
- **Vigor host asymmetry**: created card "Deal 8. Deal 8." = 16+16 with 8 Vigor; identical rows on an
  overridden vanilla card = 16+8. (energy-xcost-play)
- **ResultPileOverride/force-exhaust stomp vanilla pile modifiers** — Corruption can't exhaust modded
  skills; Rebound flashes, loses a stack, and is ignored. (energy-xcost-play, draw-discard-pile-ops)
- **Random picks use the Shuffle RNG stream** instead of CombatCardSelection — seeded runs diverge
  from vanilla and future reshuffle order changes. (draw-discard-pile-ops)
- **Multi-evoke = N different orbs**, never vanilla's same-orb-N-times — Dualcast/Multicast designs
  can't be recreated (text does say what it does). (orbs-and-channel)
- **Self-Vulnerable vs self-Weak/Frail duration inconsistency** (SkipNextDurationTick cleared only for
  Vulnerable); ApplyPower-by-id vs dedicated-kind tick policy differs for fresh applications;
  Cleanse uses a hardcoded 6/9-power list instead of PowerType enumeration; ThisTurn tracker records
  pre-hook amounts (desyncs under amount-modifying relics). (power-application)
- **Cost cosmetics/edge**: half-cost snapshots double-dip vanilla auras; star "this turn" discounts
  survive into the next turn start; type-aura expiry doesn't refresh displayed costs; permanent
  over-reduction sets base to -1 (icon vanishes, card immune to later cost increases; Confused-set
  costs ignore reductions). (cost-modifiers)
- **Reward meta**: out-of-combat card rewards skip the rare-pity timer; spec-filtered rewards
  generate twice and skew global pity; tag-filtered rewards can be offered empty; quest gold is a
  skippable reward where vanilla grants directly. (rewards-pools-meta)
- **Co-op edges**: turn-end triggers fire for ALL players during one player's extra turn; custom
  evoke path can NRE where vanilla null-guards. (triggers-timing, orbs-and-channel)
- **Moved-to-top/bottom triggers fire during ordinary reshuffles** and run un-awaited (race). (draw-discard-pile-ops)
- **Hand-discard with "include this card" uses a full-screen grid** instead of the vanilla in-hand
  selector (no Sly gold-glow). (draw-discard-pile-ops)

## TIER 3 — cosmetic text conventions

- Channel lines append "Orb/Orbs" ("Channel 1 [gold]Frost[/gold] Orb." vs vanilla "Channel 1 [gold]Frost[/gold].")
- "Evoke your next Orb." (STS1 wording) vs STS2 "Evoke your rightmost Orb"; "passive effect" vs "passive ability"
- "Gold" and "Osty" not gold-tagged; Osty verbs differ ("attacks for" vs "deals")
- Self-scaling phrasing ("When played, ... for this combat") has no vanilla analog wording
- Word-vs-digit count conventions differ for discard/exhaust lines

## Suggested fix order

Tier 1 items 1-12 are mostly small, surgical fixes (add a flag, add a guard, move a pass, add a filter):
1, 2 (trigger double-fire + phase) → 3, 4, 5 (custom status seams) → 6, 7 (cost logic) → 8, 9, 10
(damage props: mark result-derived rows Unpowered on ALL paths, route Self-damage through cardHpLoss,
extend the Vigor preserver into a generic next-attack-power preserver or merge rows into one context) →
11, 12 (vanilla gates) → 14, 15, 16, 17, 18, 19 (one-liners with the evidence in hand) → 13 (move
override rows into the play wrapper like created cards — biggest single change) → 20 + Tier 2/3.
