# Card Engine Plan — universal composition + always-correct card text

*2026-07-17. Synthesized from a 9-agent audit/design/critique pass over the actual codebase (every claim below carries file:line evidence from that pass; see FINDINGS.md entry of the same date).*

## What we actually have (audit ground truth)

- **The model**: `CardExtraEffect` is one flat 254-field record (78 fields are kind-specific one-offs), a 147-kind enum, executed by a 115-case switch plus ~25 dedicated executors (`CardEditorExtraEffects.cs:1303-1656`, `:27209-29345`). The editor's "unified groups" already hide ~90 kinds behind ~10 dropdown entries — a proto-taxonomy, but UI-only.
- **The text problem, precisely**: 33 hand-written target-switches exist. 16 are fully target-aware (fixed 2026-07-17), **13 are partial** — the whole "equal to {reference}" family falls back to "the target" (one site at `:16053` feeds ~30 kinds), and **~9 formatters are completely target-blind** (Draw/Energy/Stars/Gold — runtime honors ally/player targets via `ResolveTargetPlayers :38460`, text never mentions them). Root cause: `ExpandMultiplayerTargets` (NCardEditorPopup.cs:14330) adds ally/player options to EVERY dropdown; formatters were only ever retrofitted site-by-site. Worse: some kinds' **runtime** ignores Target entirely (GainMaxHp, Summon, Forge, Discard/Exhaust `:28507-29122`) — picking "Any Ally" silently behaves as Self.
- **Composability**: the buses already exist — per-play selection publish/consume, 11 amount-source modes, recursive branches that are **already persisted, MP-synced, and executed** (`:27285-27296`). The walls are ~10 hand-maintained whitelists that disagree with each other and one literal bug: `FetchSpecificCardToHand` is offered as a selection source but never publishes (`:31764-31799`). Top walls: branch payloads UI-locked to a helper card; generators publish selections the UI never offers; DrawCards can't consume selections; 37 kinds can't be granted; damage/kill metrics only tracked for DealDamage rows.

## The verdict from the design bake-off

Three architectures were designed and adversarially reviewed:

1. **Registry + wall-removal** (keep 147 kinds as truth) — survives review nearly intact. **Adopted as the runtime strategy.**
2. **Full primitive-graph interpreter** (~19 ops, everything compiled) — rejected as the committed path: the "one seam" claim is false (the trigger layer is ~25 entry points, not 1), the compile cache breaks on in-place self-scaling row mutation, and text provenance for the upgrade green-diff would need a rebuild. We steal its best ideas (symmetric publish/consume, per-field text provenance) without the interpreter rewrite.
3. **Sentence composer + step-block UI** — the composer survives review and is the single highest-value item; the step UI is real but later. **Adopted as the text strategy + long-term UX.**

Non-negotiables that came out of critique:
- **Golden text snapshots before any text edit** — dump every description (all presets/created cards + a synthesized kind × 8-targets × upgrade-preview matrix), commit the snapshot, diff at boot in dev builds.
- **The custom-text live-number system consumes generated text as matching keys** (line-matching in CardEditorDescriptionNumberHighlighter). Any wording change must ship with a re-match/alias pass + a boot audit counting orphaned custom-text lines, or users' custom descriptions silently freeze/misassign numbers.
- **Don't hook the composer naively at TryFormatLine:16146** — extract the ~130-line shared preamble (amount validation/suppression, X/plural grammar, 4-channel upgrade green-diff) into one `AmountRenderContext` used by BOTH paths.
- **MP version guard first**: bump `CardEditorMultiplayerStateDto.Version`, include the mod version in the sync handshake, refuse mismatched syncs with a clear error. New behavior keys off field combos an old DLL parses cleanly but executes with old semantics — today that's a silent desync; the guard makes it a visible join-time error.

## The phases (each independently shippable; stop/reorder freely)

### Phase 0 — Safety rails (small, zero behavior change)
1. Golden text-snapshot harness (as above).
2. MP sync version guard.
3. **Capability registry**: one `EffectKindProfile` per kind — `[Flags] EffectCaps { Grantable, AsPower, Repeatable, Branchable, DynamicAmount, PublishesCards, ConsumesCards, Schedulable, HistoryScalable, Passive, MetaWrapper }` + `TargetSemantics { IgnoresTarget, ResolvesCreatures, ResolvesPlayers, ResolvesBoth }` + optional `PhraseSpec`. Transcribed 1:1 from the existing predicates; boot-time parity audit vs the legacy lists + coverage audit (all 147 kinds). Predicates become delegating one-liners after one clean release. From then on, every capability is ONE profile row instead of ten scattered lists, and the UI can never disagree with runtime again.

### Phase 1 — The Sentence Composer (the "the target" killer; biggest user win)
- Extract `AmountRenderContext` (shared preamble), then `PhraseComposer`: `SubjectFor(target)` is an **exhaustive 8-way switch with no default arm** — a new target enum value becomes a compile error, not a silent "the target". Verb conjugation + whole-sentence loc keys (`{Subject} {verb} {Amount} {Payload}{Duration}{Selection}.`), Self keeps vanilla imperative voice so existing correct text is snapshot-locked byte-identical.
- Migrate in order: the 13 partial "equal to" sites (the `:16053` site alone fixes ~30 kinds) → the 9 target-blind resource formatters ("An ally draws 2 cards.") → refactor the 16 full sites onto Compose → CreatureCommand subjects → **PowerHost voice** ("Enemies with this power: at the start of their turn, …" — currently every hosted power reads as if it's yours).
- Custom-text compat: old→new line-key alias map for one transition window; auto-upgrade matched lines to stable `{{e:effectId}}` tokens; boot audit for orphaned lines.
- **Target truthfulness**: kinds with `TargetSemantics.IgnoresTarget` stop offering ally/player options; saved rows keep a preserved "legacy — behaves as Self" entry (never rewrite saved state).
- Branch/countdown/quest payload text re-enters TryFormatLine, so everything above propagates into wrapped effects for free.

### Phase 2 — Selection bus completion ("create X, then act on X")
- Fix the Fetch publish bug (one line: `ReportCurrentSelectedCards(fetched)`).
- Flip `PublishesCards` for the 8 generator kinds — they already publish at runtime; the dropdown just never offered them. "Add a random card to hand, then upgrade/discount/play IT" becomes direct.
- DrawCards consumes selections: `DrawMatchingCards` already implements a complete manual draw (ShouldDraw gate, history, AfterCardDrawn) — swap the candidate source to the existing SelectedByEffect funnel. "Scry 3, draw those" works.
- Grants stop stripping selection chains when the referenced row is granted in the same bundle.

### Phase 3 — Inline branch payloads (no more mandatory helper card)
Model, DTO, wire, executor, and even TEXT already fully support arbitrary recursive `BranchEffect` — only the editor locks payloads to "run another card". Add a UI-only style toggle (Effect-source card | Inline) with a self-contained `NestedEffectRowEditor` control (do NOT grow AddExtraEffectRow). Start with a whitelist of ~15 simple amount-only kinds (damage/block/draw/stat deltas) and widen per release; UI nesting cap 4 (wire cap is 8, runtime 16). "If Fatal: instead draw 2" becomes one card.

### Phase 4 — Grant + target routing (behavior changes, gated per kind)
- `Grantable` flipped per safe pile-op kind with an in-combat checklist each ("give this card: when played, discard 1" without helper cards). Auto-actions/meta/passives stay excluded permanently (the HitsAllEnemies soft-lock class).
- Optionally route runtime-target-blind kinds through `ResolveTargetPlayers` so "An ally draws 2" actually happens — each is an explicit, revertible profile flip.

### Phase 5 — Value bus, honestly scoped
- Widen `DynamicAmount` consumers: orb counts, clamped cost deltas.
- Metrics: ADD report call-sites where metrics should originate (the pattern exists); do NOT remove the DealDamage frame gate (critique-verified: removal delivers nothing and double-counts nested wrappers). DoT/turn-end kill attribution is out of scope (needs a combat-scoped store — separate project).

### Phase 6 — Chainboard step UI (the long-term editor)
Step blocks (Action / Target / Amount / Trigger / Duration / Condition / Limit) each owning a disjoint field slice, live composed sentence as the primary feedback, reference chips + indented child steps making chains visible. Classic-form toggle throughout; both views bind the same object (boot round-trip audit). Pre-commit a retirement criterion so dual maintenance has a scheduled end.

## Invariants (hold for every phase)
- `CardExtraEffect` + `CardExtraEffectDto` stay the disk/wire format, untouched. No kind is ever renamed. New fields are append-only nullable. Old saves load and behave identically — new behavior only activates on field combinations old saves cannot contain.
- Zero new Harmony patches (except, at most, one spike fallback in Phase 2 — abandon if ugly). All engine code is mod-owned: daily beta updates never collide with it.
- Every widening is one profile row + changelog line, revertible in one line. Boot audits (registry parity/coverage, DTO round-trip, clone fidelity, text snapshots, custom-text matching) turn every silent-drift class into a startup warning.

## Rough effort
P0: 1-2 sessions · P1: 4-6 · P2: 2-3 · P3: 3-5 · P4: 2-3 · P5: 1-2 · P6: open-ended, incremental. P1 alone removes the #1 user pain; P2+P3 deliver the composability headline.
