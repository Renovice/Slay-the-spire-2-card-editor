# Chainboard Plan — the box-chain panel (Card Engine P6)

*2026-07-18. Design agreed with Bartek: simple linear boxes, not node graphs. This refines the P6
section of CARD_ENGINE_PLAN.md into a buildable spec.*

## Design principles (from the author)

- **Linear box chains**: `(box) — (box) — (box)`, left to right. No Unreal-style node canvases,
  no wires to drag. Boxes look like the existing UI (dropdown/panel styling).
- **Bottom of the editor**, in its own collapsible "Effect Chains" section, with a **toggle**.
- **Tandem, not replacement**: chains work alongside the classic Extra Effects list. Same
  functionality as the regular editor and more — a pseudo-scripting surface a non-coder can use
  by shuffling boxes.
- Movable steps, conditions in the chain, "act on the previous step's result" as the core verb.

## The load-bearing architectural decision

**Boxes ARE effect rows. Chains ARE the existing links.** A chain is nothing but consecutive
`CardExtraEffect` rows where each card-action step consumes the previous step's published
selection (`CardSelectionMode = SelectedByEffect` + `CardSelectionSourceEffectId`), or reads its
amount (`AmountSourceEffectId`), or is a branch payload. The Chainboard is a *view + authoring
layer* over the same rows:

- Zero new persistence. Zero DTO changes. MP-safe by construction (the wire format is untouched).
- **Tandem for free**: a chain-authored card IS a classic-rows card; open it in either view.
  Classic rows that happen to be linked (authored before the Chainboard existed) render as chains
  automatically.
- The panel's one real job is **auto-wiring**: when you add a step after a card-producing step,
  it defaults to *"acts on the previous step's cards"* — the SelectedByEffect dropdowns the user
  never has to see.

## The author's examples, as they will look

**"Transform a card, then shuffle it into my deck":**

    [ Transform Card ] ──▶ [ Shuffle into Draw Pile ]
      (choose 1 in hand)      (acts on step 1's result)

Internally: Row 1 = TransformCards (Choose/Hand) · Row 2 = MoveCardsBetweenPiles → Draw Pile,
position Random, Selection = SelectedByEffect(Row 1). Both already exist; the panel writes them.

**"Give a Celestial Blade in your hand Pierce":**

    [ Give Keyword: Pierce ] with chips: (in Hand) (only: Celestial Blade)

One box — this is a single effect (GrantKeywordToPile + keyword + pile + card-id match filter).
The "choose keyword / limit to card" sub-boxes from the sketch are that box's **setting chips**,
shown inside the expanded box. Rule of thumb: *a box per action; chips per setting.*

**Conditions** render as small prefix boxes attached to the step they gate:

    [ IF Fatal ] ▶ [ Draw 2 ]        (the step's inline branch / condition fields)

## Auto-wiring rules

1. Adding a step after a card-producing step (registry `PublishesCards*`): if the new kind
   consumes cards (`ConsumesCards`), default Selection = SelectedByEffect(previous step). The
   connector renders solid: `──▶`.
2. If the new kind can't consume cards but takes a dynamic amount, offer "amount = previous
   step's result" as the chip default; connector renders dashed.
3. If neither applies, the boxes are just sequenced (plain `—` connector) — same-play order.
4. Reordering boxes reorders the rows and rewires the source references (references are
   backward-only, matching the runtime).
5. Deleting a step that others reference: downstream steps show a broken-link chip and fall back
   to their kind's default selection (never silently deleted).

## Prerequisite fixes (C0) — found during design verification

- **TransformCards must re-publish the REPLACEMENT cards** after transforming (today it publishes
  the pre-transform originals, which stop existing — a downstream "shuffle THAT into deck" would
  no-op). Same `ReplaceCurrentSelectedCards` pattern generators use. Check the other mutators
  (EnchantCard, UpgradeCardsInPile keep the same instances — fine; StatefulTransform — verify).
- Sweep for any other "result vs input" publish mismatches in kinds the chain view will surface.

## Build phases

- **C0 — publish-result fixes** (S): Transform republishes replacements (+ audit siblings).
- **C1 — read-only chain strip** (M): the bottom "Effect Chains" panel renders existing rows as
  boxes with connectors inferred from their links; each box shows kind + one-line summary (the
  hint pipeline from the Browse picker); clicking a box expands/scrolls to its classic row.
  Collapsible section + settings toggle. *Ships the visual language with zero behavior risk.*
- **C2 — chain authoring** (M-L): "+ Step" button per chain (opens the existing Browse picker),
  auto-wiring per the rules above; ◀▶ move buttons rewiring references; remove-step handling;
  "New Chain" starting from any producing kind.
- **C3 — condition & modifier chips** (M): IF-boxes writing the step's branch/condition fields
  (incl. P3 inline payloads); duration/target/keyword chips inside expanded boxes.
- **C4 — polish** (S-M, ongoing): drag to reorder, live sentence under the strip, chain badges on
  classic rows, collapse-classic-when-chained option.

## UI spec (v1)

- Section header "Effect Chains" + fold arrow + a small "Show chains" settings toggle, placed
  below the Extra Effects list in the right column.
- A chain = one horizontal `HBoxContainer` (h-scroll when long). Box = `PanelContainer` in the
  existing input style: bold kind label, dim one-line summary, `>` marker on the selected box.
  Connector = a fixed-width `Label` ("──▶" / "─ ─" / "—").
- Steps move with ◀ ▶ buttons (drag comes in C4). "+ Step" at the strip's end; "✕" per box.
- Everything writes through the existing row-building/save code paths — the panel never invents
  its own persistence.
