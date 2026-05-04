# Card Editor Feature Checklist

Use this file as the running feature ledger. When a feature is added, mark the checkbox and strike through the line so old ideas remain visible.

## Branching And Limits

- [x] ~~Standalone Effect Limit extra effect that can limit selected effects or the whole card.~~
- [x] ~~Nested branch effect sources stay nested instead of being flattened into the parent row.~~
- [x] ~~Effect Limit rows do not expose self-trigger controls or history scaling controls that do not apply.~~
- [x] ~~Fatal as a universal branch condition: if this card play dealt a killing blow, run any selected branch effect or effect source.~~
- [ ] Let Fatal optionally evaluate after the current row's own effect resolves, so a damage row can branch off its own kill without relying on earlier card-play damage.
- [ ] Branch trees with named stages, reusable branches, and nested branch editing without losing row identity.
- [ ] Better branch editor UX for long effect-source chains and large conditional trees.

## Use Limits And Scopes

- [ ] Finish global/shared/equivalent/self-copy use-limit semantics for every effect kind.
- [ ] Make power-created effects merge identical active effects instead of creating duplicate powers with separate counters.
- [ ] Display remaining uses live on cards and powers with vanilla-style colored numbers.
- [ ] Support use-limit windows beyond turn/combat where appropriate, including run-scoped limits.
- [ ] Decide and document how base-card counters and power-instance counters interact when a card creates a power.

## Random Cards And Generated Cards

- [ ] Add a true "play random existing card" effect that chooses from real piles/cards instead of only generating temporary cards.
- [x] ~~Add explicit result-pile override controls for played cards where vanilla semantics differ.~~
- [x] ~~Add shuffle-pile actions so moved or generated cards can be followed by vanilla-like pile shuffling.~~
- [ ] Add finer result-pile controls for generated/autoplayed cards where vanilla semantics differ from the source card.
- [ ] Normalize random-card filter UI with the existing field-label and parameter-box style.

## Vanilla Coverage Gaps

- [x] ~~Robust Alchemize-style potion maker with amount/X support, exact database-backed potion picker, pool and rarity filters, combat-only eligibility, duplicate control, text preview, serialization, and upgrade deltas.~~
- [ ] Broader potion procurement/reward variants beyond direct potion creation, such as potion rewards, shop/rest-site potion hooks, or potion manipulation effects.
- [ ] Reward, relic, map, route, and rest-site mutation effects.
- [x] ~~Custom playability gates, including "cannot play this unless..." and "cannot play other cards..." effects.~~
- [x] ~~Actual damage-result value sources for HP damage, blocked damage, total damage, overkill, killed-count, and application/instance count.~~
- [ ] Target-specific damage-result callback identity for cards that need to remember exactly which target was hit/killed by a prior hit chain.
- [x] ~~Bespoke lifecycle hooks that vanilla cards use outside normal play/power/timing rows.~~
- [x] ~~Custom dynamic card identity and hover-preview card tips.~~
- [ ] Visual render overlays, badges, glows, and other bespoke card-face presentation effects.

## Upgrade And Scaling Ideas

- [ ] Custom upgrade stages for endless upgrades instead of only upgraded-version plus infinite delta.
- [ ] Branchable scaling stages, such as "if at least N matching cards were played this combat, run this other effect source."
- [ ] Let stage selectors target any compatible effect/effect source with the same row-selector system used by Effect Limit.

## Vanilla Cards Not Yet Exact With Only Effect Sources

Rechecked 2026-05-04 against vanilla source. "Mechanics yes" means the gameplay behavior can be built in the editor; some entries still lack vanilla-perfect glow, overlay, VFX, or live dynamic wording.

- [x] ~~`Alchemize`~~
- [x] ~~`TheHunt`~~
- [ ] `ByrdonisEgg` - no generic rest-site option injection yet.
- [ ] `LanternKey` - no generic map/event routing mutation yet.
- [ ] `SpoilsMap` - no generic act-map replacement, quest marker, and quest-complete hook yet.
- [x] ~~`Guilty`~~ - mechanics yes via run-scoped delayed deck removal after 5 combats; live remaining-combat text is not vanilla-perfect.
- [x] ~~`Clash`~~ - mechanics yes via play-permission gates, including conditional gold glow.
- [x] ~~`GrandFinale`~~ - mechanics yes via draw-pile count play gate, including conditional gold glow.
- [x] ~~`HighFive`~~ - mechanics yes via Osty-alive play gate plus Osty attack action; vanilla red glow/VFX are not exact.
- [x] ~~`PactsEnd`~~ - mechanics yes via exhaust-pile count play gate.
- [ ] `Enthralled` - manual lockout can be built, but exact autoplay exemption, non-generation flag, and red glow are not generic yet.
- [ ] `Normality` - manual 3-cards-per-turn lockout can be built, but exact autoplay exemption, live remaining count, and red glow are not generic yet.
- [x] ~~`BlightStrike`~~ - mechanics yes via total-damage amount source into Doom.
- [x] ~~`BeatIntoShape`~~ - mechanics yes via damage-history count before current play into Forge.
- [x] ~~`Fisticuffs`~~ - mechanics yes via total+overkill amount source into Block.
- [x] ~~`EchoingSlash`~~
- [x] ~~`Omnislice`~~
- [x] ~~`Misery`~~
- [x] ~~`Reboot`~~ - mechanics yes via move hand to draw, shuffle draw pile, then draw.
- [x] ~~`ParticleWall`~~ - mechanics yes via result-pile override to hand.
- [x] ~~`BansheesCry`~~ - mechanics yes via Ethereal-play count/history cost reduction.
- [x] ~~`MadScience`~~ - mechanics yes via built Tinker Time card support.
- [ ] `Infection` - end-turn-in-hand self damage can be built, but the built-in overlay and bespoke VFX are not generic yet.
- [x] ~~`Melancholy`~~ - mechanics yes via After Death trigger with this-combat cost reduction.
- [ ] `SovereignBlade` - still needs dynamic Seeking Edge targeting, Parry callback, forge-created VFX state, and exact retained token mutation.
