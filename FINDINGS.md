## 2026-07-27 (2) - Transform played its animation but the card never CHANGED: our visual repair could never find the node

User pushed back on the "working as designed" answer and was right: the card visually stayed Rainbow Evolution during combat, which the ThisCombat/combat-end explanation does not cover. Root-caused properly this time.
CHAIN:
1. Vanilla only swaps what a card DISPLAYS inside NCardTransformShineVfx.PlayUntilCardUpdate: `if (!(await WaitAndInterruptIfNecessary(...))) QueueFreeSafely(); else UpdateCard(_cardNode, _endCard);`. UpdateCard is the ONLY thing that assigns cardNode.Model = replacement. WaitAndInterruptIfNecessary bails the moment `!cardNode.IsInsideTree() || _endCard.Pile == null` (NCardTransformShineVfx.cs:104-117, 133-151). At a Turn-End-before-discard transform the hand is being torn down while that ~0.75s animation runs, so the node leaves the tree, the wait aborts, and UpdateCard NEVER RUNS - the animation is seen, the card is not changed.
2. We already had RefreshTransformVisuals (CardEditorExtraEffects.cs:9891) to repair exactly that, but it could never work: it called `NCard.FindOnTable(original)` with NO overridePile. By then CardCmd.Transform has removed the original from state, so original.Pile == null, and FindOnTable's switch falls to its `null => null` arm (game source NCard.cs:360-371) and returns null. We then fell through to RefreshCardVisuals(replacement), which looks the node up by the REPLACEMENT model - but the node still carries the ORIGINAL model precisely because UpdateCard never ran, so that missed too. Both paths missed; nothing ever repainted the card.
FIX: pass the replacement's pile as the override - `NCard.FindOnTable(original, replacement.Pile?.Type)`. The replacement occupies the slot the original had, so its pile is where the stale node lives; the existing repair (set .Model, then NHandCardHolder.UpdateCard or UpdateVisuals) then runs as designed.
NOTE this is a general fix, not specific to turn-end: ANY transform whose vanilla animation gets interrupted (card leaving the tree, pile change mid-animation) hit the same dead repair path.
Separately confirmed earlier and still true: duration ThisCombat legitimately reverts at combat end (use RUN to persist), and the combat-end revert NRE fix from earlier today stands.
Build 0 errors / 278 warnings. Deployed.
Next Step: in game, end a turn with the card in hand - it must now visibly become the target card, and stay it for the rest of the combat.
## 2026-07-27 - "Transform This Card" reverting: mostly BY DESIGN + a real NRE in the combat-end revert

User report: a card with StatefulTransform (Turn End before discard, In Hand, Transform, duration THIS COMBAT, into a created card) plays the transform animation but "goes back into deck as Rainbow Evolution".
ROOT ANSWER (not a bug): duration ThisCombat is combat-scoped by design. RunStatefulTransformsAfterCombatEnd (CardEditorExtraEffects.cs:10541-10547) reverts on the `_ => true` default arm; only Run returns false and Combats decrements. So at combat end the card becomes the original again and the run deck shows Rainbow Evolution. For a permanent transform the duration must be RUN.
REFUTED along the way (two agents, ~300k tokens, worth recording so nobody re-checks): the end-of-turn discard does NOT iterate a pre-transform snapshot - FlushPlayerHand reads the live hand in Phase Two, after BeforeSideTurnEnd (CombatManager.cs:1253/1390/1401); the turn-boundary CardPlay DOES carry the live hand instance, not a clone (CardEditorExtraEffectTriggerPatches.cs:480); the trigger patch chains its task (`__result = RunAfter(__result, ...)`, :431) so the game awaits it - no race; ThisCombat does NOT revert at turn end (`_ => false`, :10492-10499); created-card ids resolve fine.
REAL BUG FOUND (runtime evidence, godot.log): "[CardEditor] Stateful transform combat cleanup failed: System.NullReferenceException at CardCmd_Transform_CardEditorTransformInterop_Patch.UnmarkWhenFinished <- RevertStatefulTransform <- RunStatefulTransformsAfterCombatEnd". Cause: RevertStatefulTransform calls CardCmd.Transform, whose FIRST line dereferences CombatManager.Instance (game source CardCmd.cs:371) - and that singleton is already torn down during combat-end cleanup. The NRE surfaced at our `await originalTask` in the interop postfix. Two consequences: (1) the bookkeeping after the await (UnmarkStatefulTransformReplacement / UnregisterStatefulTransformEntry) never ran, leaking the entry - and _statefulTransformEntries is a STATIC list, so it leaks across combats; (2) the catch sat OUTSIDE the foreach, so the first throwing entry abandoned every remaining entry un-reverted and still registered.
FIX: RevertStatefulTransform now wraps only the visual swap in try/catch and moves the bookkeeping into a finally (the visual swap is meaningless once combat is over; the bookkeeping is not). RunStatefulTransformsAfterCombatEnd got a per-entry try/catch that still unmarks+unregisters the failing entry so one bad entry cannot abort the sweep.
Build 0 errors / 278 warnings. Deployed.
Next Step: tell the user to set duration RUN if they want the transform to persist past combat; re-check the log for the cleanup warning after a combat to confirm the NRE is gone.
## 2026-07-27 - Rainbow Evolution StatefulTransform "animate then revert" - root cause

Hypothesis: Three hypotheses investigated for why a ThisCombat StatefulTransform (Turn End Before Discard, In Hand, Transform mode) animates but the card reverts.

Finding: H-C partially confirmed at COMBAT END, not turn end. H-D and H-E refuted.

Evidence:
- RefreshStatefulTransformDuration (10143-10160): ThisCombat hits `_ => 0` → RemainingTurns=0, RemainingCombats=0.
- RunStatefulTransformsAfterPlayerTurnEnd (10467): switch default `_ => false` → ThisCombat does NOT revert at turn end.
- RunStatefulTransformsAfterCombatEnd (10516): switch default `_ => true` → ThisCombat DOES revert at combat end. This is the actual revert path.
- DoesStatefulTransformConditionPass (10162): checks only ScaleMode count conditions, NOT card location. H-D refuted.
- DoesStatefulTransformRevertConditionPass (10192): returns false unless HasUsableBranchCondition (branch condition set). No location check. H-D refuted.
- CreateDynamicTransformReplacement (9748): TryParseSpecificCardId correctly resolves CARD.CARD_EDIT... ids via CardEditorCreatedCardsStore slot mapping. CardModel_ToMutable_Patch applies TARGET card's overrides. H-E refuted.
- No pile-change handler, no AfterFlush sweep, no EvaluateStatefulTransformConditionStops call runs during the BeforeSideTurnEnd → FlushPlayerHand → AfterSideTurnEnd sequence that could trigger a within-turn revert.
- Turn-end execution order confirmed: BeforeSideTurnEnd (Phase One, transform fires, card in Hand) → DoTurnEnd → FlushPlayerHand (Phase Two, discard) → AfterSideTurnEnd (RunStatefulTransformsAfterPlayerTurnEnd → _ => false).

Reason: The transform fires correctly and persists through the turn. The revert happens at AfterCombatEnd via RunStatefulTransformsAfterCombatEnd's `_ => true` default arm which catches ThisCombat. The user observes "Rainbow Evolution" in the deck/discard AFTER combat ends, which is the correct behavior for ThisCombat. The "animates then reverts" description matches: forward transform animation plays during the turn, silent revert fires at combat end.

Fix: To keep the transform permanently (through the run), use Duration = Run instead of ThisCombat. Run hits `CardExtraEffectStatefulTransformDuration.Run => false` in RunStatefulTransformsAfterCombatEnd and `_ => false` in RunStatefulTransformsAfterPlayerTurnEnd → never reverts.

Side effects of changing to Run: the transform would persist across ALL combats (Run = entire run duration). If the intent is "transform for THIS combat only, revert at end" then ThisCombat is correct and the user must accept the revert. If the intent is "permanently transform", use Run.

Next Step: Clarify with the user whether they want (a) transform that reverts at combat end (ThisCombat is correct, no code change needed) or (b) a permanent transform that persists to the next combat (use Run duration instead).

## 2026-07-25 - MP ready path CONFIRMED end to end (join -> ready -> host start -> run loads)

Added SimulateHostBeginRun: the fake client service now STORES the message handlers StartRunLobby registers on it, so a synthetic LobbyBeginRunMessage can be delivered to the exact handler a real host message would hit (players/seed/act taken from the live lobby). This is simulation, not a shortcut - the game cannot tell the difference.
RESULT (godot.log, no exceptions anywhere):
  [FakeJoin] Delivering fake LobbyBeginRunMessage (seed=951VTXRCBT2V, players=2)
  [StartRunLobby] Received LobbyBeginRunMessage      <- the GAME's own handler log
  [FakeJoin] LobbyBeginRunMessage delivered to 1 handler(s)
The run then began loading and stopped on a black screen. That is the PHANTOM PEER, not a bug: co-op run init waits on the other player (asset preload / input sync / checksum handshake) and nobody answers. Loading at all was the confirmation being sought.
FULL CHAIN NOW VERIFIED SOLO on real game code: join as client -> pick character -> Ready registers (localPlayer.isReady=true) -> LobbyPlayerSetReadyMessage would be sent to the host -> IsAboutToBeginGame TRUE (all three conditions) -> host begin-run accepted -> run loads.
STILL UNVERIFIED (needs a second real machine): actual cross-machine networking and card-definition sync between peers. Everything up to that point is proven.
Clean build (all four debug toggles false) redeployed; distribution refreshed. 0 errors / 278 warnings.
## 2026-07-25 - MP ready fix VERIFIED on the joiner side (fake client lobby)

The ready-gate fix is now proven against real game code, solo, with no networking.
Approach that finally worked (user's idea, simpler than everything before it): do not fake a network PEER - fake the JOIN. A debug entry in the game's own Choose-Friend list builds a client-typed INetGameService plus a ClientLobbyJoinResponseMessage containing an already-ready fake host, then calls the genuine NCharacterSelectScreen.InitializeMultiplayerAsClient and pushes the real character-select screen. From that point every line executed is real game code: same lobby, same SetReady, same gate. ENet is bypassed entirely (the in-process handshake is impossible - proven twice).
EVIDENCE (godot.log, joiner side):
  [FakeJoin] Pushing character select as CLIENT (host id=1 ready=true, local id=2000 ready=false)
  [FakeNetService] SendMessage<LobbyPlayerSetReadyMessage>            <- the message the mod used to swallow
  [FakePeer] SetReady(True) called for netId=2000 -> localPlayer.isReady=True
  [FakePeer] IsAboutToBeginGame -> True | connectingPlayers=0 players=2 allReady=True
All three previously-failing behaviours now pass: the click reaches the game, isReady actually sets, and the set-ready message goes out to the host. IsAboutToBeginGame returns TRUE, i.e. the lobby is fully satisfied.
"It sits there after Ready" is CORRECT, not a failure: a client never begins the run itself, it waits for the host's LobbyBeginRunMessage, and a fake host cannot send one. With a real host that is the moment both players load in.
Also fixed: the debug entry's label used "[debug]", which the BBCode parser read as a tag ("Found end tag center, expected debug") and threw into the log - renamed to "- debug".
STILL UNVERIFIED (needs the friend or the source build): real cross-machine networking and actual definition sync between peers. This test covers the local ready path only.
Clean build (all three debug toggles false) rebuilt, deployed, distribution refreshed. Build 0 errors / 278 warnings.
## 2026-07-25 - Reverted perf items 3,5,6,7,8 (they caused card library/creator loading hitches)

User report: the editor caches and pool/character/boot-save changes made the CARD LIBRARY and CREATOR hitch while loading - i.e. the "optimisations" were a net regression on the screens they touched. Reverted per user instruction; correctness and smoothness beat micro-optimisation.
REVERTED (restored to pre-perf-pass-2 behaviour):
- 3 kind-dropdown cache, 4 summary-LINQ removal, 5 chain-strip fingerprint skip -> NCardEditorPopup.cs restored wholesale to dd034c3~1 (item 4 went with them; it was a tiny invisible win and not worth carrying alone). The strip once again rebuilds every refresh, which is slower but provably never stale - and it removes the whole class of fingerprint-gap bugs (two of which the bug test had just found).
- 6a pool title dictionary in the get_Pool/get_VisualCardPool postfixes -> CardEditorVanillaClassificationPatches.cs restored wholesale.
- 6b GetAvailablePoolTitles cache, 7 SupportedCharacterIds cache + HashSet, 8 conditional boot Save -> surgically removed from CardEditorCreatedCardsStore.cs / CardEditorBaseDeckStore.cs, keeping item 1 in those files.
KEPT (invisible to the UI, genuine wins, no regression reported):
- 1 JsonSerializerOptions hoisted to static readonly across 9 stores + 2 inline sites.
- 2 CardEditorRunPowerState write batching (removes a synchronous disk write per persistent power grant during combat).
ALSO KEPT: the green/enchanted fix in CardEditorOverrides.cs (guard inside ReapplyRuntimeModifiers) - verified still present after the reverts.
Build: 0 errors / 278 warnings (beta baseline). Deployed.
LESSON: caching on UI/loading paths in this codebase has repeatedly cost more than it saved (stale-state bugs plus first-touch build hitches). Future perf work should stay on invisible paths (I/O, serialization, combat hooks) unless there is a measured profile showing the UI path is the bottleneck.
## 2026-07-25 - Bug-test of our own fixes: 2 blockers found and fixed

Two hostile review agents were pointed at the recent fixes (perf pass 2 dd034c3, gate/green ec0ee76, the branch-retarget merges). They found real bugs - both of my earlier fixes were INCOMPLETE:

BLOCKER 1 - the GREEN fix did not work for ENCHANTED cards, i.e. exactly the reported case. RefreshCardAfterUpgradeStateChanged learned to skip FinalizeUpgradeInternal for preview clones, but line 1503 then calls ReapplyRuntimeModifiers, which at :1148-1152 called card.FinalizeUpgradeInternal() UNCONDITIONALLY whenever card.Enchantment != null - re-wiping WasJustUpgraded straight after the guarded loop. Worse, the enchanted preview clone also reaches that same code via the UpgradeInternal postfix, a path the first fix never touched.
FIX: moved the guard INTO ReapplyRuntimeModifiers (capture CardHasJustUpgradedFlag before Enchantment.ModifyCard(), skip Finalize when set). One change now covers both call paths.

BLOCKER 2 - the chain-strip FINGERPRINT omitted 10+ fields that the strip actually renders, so the early-return could show STALE boxes: unified variant/mode pickers, card-generation/ignore/auto-action/card-action/upgrade variants, CardCostsLess kind+duration, the whole AmountValueSource* family and the source multiplier; plus two in-place LABEL mutations that leave the selected index unchanged (As-Power rewrites trigger labels; the amount-source dropdown relabels when another row's kind changes).
FIX: rather than enumerate fields (fragile - we had just proven 10+ were missed), the fingerprint now hashes the RENDERED TEXT itself (GetEffectSummaryKindText + GetEffectSummaryAmountText + the trigger/target/IF-condition/amount-source/selection-source selected texts). Self-maintaining by construction: if the strip can render it, changing it invalidates the fingerprint.

CONFIRMED SAFE by the reviews (no change needed): JSON options hoisting (no caller mutates the shared instance, settings/converters identical); the kind-dropdown cache (keyed correctly on _isEmbeddedEffectHost; Definitions and the filters are static); SupportedCharacterIds order preserved; RunPowerState read paths use the in-memory cache and out-of-combat grants still flush immediately; HasActiveIgnoreEffectPair matches two separate calls incl. the bitmask prefilter; the beta CreatureCmd.Damage 7-arg signature is correct; the ready-gate fix works and cannot double-fire (its deferred machinery is now dead, never re-armed).
Known remaining nits (not fixed, low value): dead FirePendingReadyIfNeeded still ticked from Update(); stale comment above AllowClientReady; Hook.ModifyDamage prefix declares CombatState where beta passes ICombatState (harmless downcast, fallback resolves context); a crash mid-combat can lose one turn of batched power-state writes.
Build: 0 errors / 278 warnings (beta baseline). Deployed.
Next Step: in-game - (1) enchant a card then open its upgrade preview: changed numbers must be GREEN; (2) edit a chained effect's variant/amount-source and confirm the chain strip updates immediately (no stale box).
## 2026-07-24 (late) - Game swapped BACK to the public branch mid-work; retargeted + perf pass 2

BRANCH SWAP: the game updated again at 21:48 (sts2.dll 9,364,480 bytes, CardLocation ABSENT = public build; the beta was 9,609,216 with CardLocation). We had retargeted to BETA on 07-20, so the mod stopped compiling. NOT caused by the perf work - two implementation agents wrongly dismissed the 9 resulting errors as "pre-existing", which they were not (the tree was 0-errors before).
Fix: re-applied the public compat by reverting the beta-revert (git revert 11237f9). 3 conflicts, all resolved as genuine MERGES rather than taking a side - the public ICombatState signatures were kept AND the perf-pass HasActiveIgnoreEffectPair single-fetch optimisation was preserved (CardEditorIgnoreDamagePatches, CardEditorIgnoreCapsAndNegationPatches). Two post-compat regressions also fixed: CardPlay.Player in the quest synthetic play (Quest Reward Persistence was written after the original compat pass) and JoinFlow.MockInfo in the debug dummy lobby (beta-only API - helper stubbed with a warning, restore if ever retargeted to beta).
LESSON: this mod now flips branches regularly. Every branch swap needs this revert-the-revert dance. A conditional-compilation dual build remains the real fix if it keeps happening.

PERF PASS 2 (items 1-8 of the optimisation report), all intended output-identical:
1. JsonSerializerOptions hoisted to static readonly in 9 stores + 2 inline sites (chain-link store, foil registry). A fresh options object per call defeats System.Text.Json's metadata cache.
2. CardEditorRunPowerState now batches disk writes (dirty flag + FlushIfDirty at the counter store's existing seams) instead of a synchronous WriteAllText per persistent power grant during combat.
3. Effect-kind dropdown (205 defs filtered + sorted + ~147 AddItem) built ONCE per popup instead of per row - a 10-effect card paid it 10x on open.
4. RefreshEffectSummaryList: per-keystroke .Select().ToList() + .Distinct().Count() replaced with an early-exit scan; list only materialised when group headers are actually needed.
5. RefreshEffectChainStrip: content fingerprint (40+ per-row render inputs incl. all branch/chip selects, expanded id, manual sequence links) - skips the full teardown/rebuild when nothing it renders changed. Was freeing and recreating every node on EVERY keystroke.
6. Pool title -> model dictionary replaces ModelDb.AllCardPools linear scans in get_Pool/get_VisualCardPool postfixes and the library sort.
7. SupportedCharacterIds cached + HashSet membership (was a full ModelDb LINQ chain per property access, then O(n) Contains).
8. CreatedCardsStore skips its unconditional boot re-save when the load added no default slots.
Deliberately NOT done: lazy-building the 347 Godot nodes per effect row (biggest theoretical win, ~260 fields would become nullable = high regression risk for a one-time cost).
Build: 0 errors / 274 warnings (PUBLIC-branch baseline; 278 was the beta baseline). Deployed + distribution refreshed.
Next Step: perf pass 2 is UNVERIFIED IN GAME - play a combat and open the editor; watch for (a) any stale chain strip after edits (fingerprint gap), (b) quest/power counters persisting correctly across combat end.
## 2026-07-24 - BOTH root causes found: ready gate swallowed clicks; our rebase cleared vanilla green

Prior diagnoses were WRONG on both counts. Corrected:

GATE (MP ready). Old theory: invalid SemVer manifest broke mod parity. WRONG - the game's version/mod parity check runs in JoinFlow.Begin at JOIN time; players already IN a lobby seeing each other have passed it. Real cause: OUR Harmony prefix on StartRunLobby.SetReady (CardEditorStartRunLobbySetReadyPatch) -> AllowClientReady returned FALSE to "hold" the ready until the host snapshot applied. Returning false suppresses the game's SetReady, which is what SENDS LobbyPlayerSetReadyMessage - so the HOST never learned the client was ready and IsAboutToBeginGame() stayed false forever. Any hole in the deferred re-fire (FirePendingReadyIfNeeded ClearPendingReady() on a transient _netService null/rebind; a pump that stopped ticking on scene change) ate the click permanently with zero UI feedback. Decisive argument: the hold ALREADY failed open after 3s regardless of sync state, so it never prevented an unsynced ready - it only DELAYED one. Near-zero safety, catastrophic downside.
FIX: AllowClientReady now ALWAYS returns true (never blocks); when a client readies pre-snapshot it re-arms the sync request and logs a warning. Dead _bypassReadyGate field + empty finally removed.

GREEN (vanilla upgrade preview). Old status: shelved as "intermittent, our postfix exonerated". The postfix WAS innocent (measured greenIn==greenOut) - the culprit was a PREFIX. Chain, fully proven in source:
- DynamicVar.ToHighlightedString (DynamicVar.cs:175) renders green when WasJustUpgraded is true.
- FinalizeUpgradeInternal (CardModel.cs:2143-2148) -> DynamicVars.FinalizeUpgrade() -> DynamicVar.cs:154 sets WasJustUpgraded = false. Finalize CLEARS green.
- Vanilla's campfire preview clones the card and calls UpgradeInternal WITHOUT Finalize, so the flag stays set = green.
- OUR CardEditorOverrides.RefreshCardAfterUpgradeStateChanged (:1449-1460) rebuilt the card with UpgradeInternal() + FinalizeUpgradeInternal() -> flag cleared -> green gone. Reached via the GetDescriptionForUpgradePreview PREFIX -> TryRestoreCard -> RebaseCardToCurrentDefinition, which only does real work when the card has a stored mutation payload - ENCHANTING creates one, hence "sometimes depending on enchantments", and hence greenIn=False in the live trace (vanilla built its string AFTER we wiped the flag).
FIX: capture CardHasJustUpgradedFlag(card) before the rebuild; skip FinalizeUpgradeInternal in the re-upgrade loop when the card arrived already flagged (a preview clone), preserving vanilla's green. Non-preview cards finalize exactly as before. Honors the user constraint: our machinery must never alter vanilla/pre-baked text.

Build: 0 errors / 278 warnings (baseline). Deployed + built cfiles + release zip refreshed.
Next Step: (1) MP - both players retry Ready in a real lobby; it must register immediately now. (2) GREEN - enchant a card, then open a campfire upgrade preview on a vanilla card; changed numbers must be green.
## 2026-07-24 - Debug dummy co-op host toggle (ENet loopback) for solo ready-check testing

Feasibility workflow (wf_2efeadf0) verified the game supports in-process ENet loopback (two ENet sockets coexist; NMultiplayerTest is the reference). Built CardEditorDebugDummyLobby.cs:
- SimulateDummyHost (const, default false): spawns an in-process dummy co-op HOST at main-menu ready (NetHostGameService + StartENetHost(33771,4) + StartRunLobby(Standard, host, stub listener, 4) + AddLocalHostPlayer + SetLocalCharacter(Ironclad) + SetReady(true)), pumped each frame by a dedicated mod Node. Matches NMultiplayerTest.StartHost exactly. Stub IStartRunLobbyListener logs BeginRun ("Ready-check PASSED").
- SimulateVersionMismatch (const, default false): surfaces BuildMismatchedJoinFlow(wrongVersion) helper using the game's JoinFlow MockInfo seam to prove a version mismatch is still REJECTED solo (negative test).
Off-by-default = provably inert: no [HarmonyPatch] in the file, one guarded call at MainMenu ready that returns immediately, no nodes created when off. Build 0 errors / 278 baseline.
FIDELITY/HONESTY: this is ENet loopback (not Steam) and both peers are the SAME build - it proves the fixed manifest does NOT falsely reject a MATCHING peer and the whole ready handshake completes end to end (the JoinFlow parity code we fixed is transport-agnostic, so it IS exercised). It does NOT test Steam matchmaking or genuine cross-version interop. Approach C covers the mismatch-rejection negative case.
USAGE: flip SimulateDummyHost=true, rebuild+deploy, launch; the menu logs "Dummy co-op host listening on 127.0.0.1:33771 (ready)". Then join 127.0.0.1:33771 as a client via the game's Join-by-IP (needs the 'fastmp' launch arg to expose it). Intended solo = join from the SAME instance (two ENet peers, one process); if shared net-singletons conflict, fall back to a second game instance as the client. Watch the host log for BeginRun = ready-check works.
NOT deployed (off by default; deploy when you flip the const).
## 2026-07-24 - Green upgrade-text investigation: our code exonerated; intermittent, shelved

User: vanilla cards (Neutralize etc.) sometimes lose upgrade-preview green, "depending on enchantments"; bisect blamed the perf-pass DLL.
Investigation: 3-agent trace + a deployed diagnostic build logging [green] presence at the description postfix ENTRY vs EXIT (temp instrumentation, since reverted).
Live trace verdict: for EVERY card greenIn == greenOut - our postfix NEVER strips green. Where green was missing (BULLET_TIME) it was greenIn=False, i.e. VANILLA did not produce it before our code ran. On the tested load all unedited cards (NEUTRALIZE/SLICE/SURVIVOR/DEFEND_SILENT) showed greenIn=True greenOut=True = working. User confirmed "worked fine when I loaded it in" -> the fault is INTERMITTENT / run-state-dependent, not a deterministic code break, and the perf pass is provably output-neutral in the description path for unedited cards.
Leading unproven theory for the intermittent case: the upgrade-preview / NCard.UpdateVisuals prefix calls CardEditorRunSelfScalingState.TryRestoreCard, which - once a run accumulates self-scaling/enchant state - RebaseCardToCurrentDefinition rebuilds the card's DynamicVars and clears vanilla's WasJustUpgraded flag (DynamicVar.ToHighlightedString greens on WasJustUpgraded). Fresh load = no state = no rebase = green survives. NOT confirmed with a broken-state trace.
Decision (user): ship clean now, reopen if it recurs. Diagnostic logging reverted; clean 10.0.0 build deployed + built cfiles + zip refreshed.
Next Step if it recurs: reproduce with the diagnostic build still catchable (re-add the GreenTrace line), catch a card that SHOULD be green showing greenIn=False, then guard RebaseCardToCurrentDefinition (or the UpdateVisuals prefix) to preserve WasJustUpgraded during upgrade-preview renders.
## 2026-07-24 - MP ready-check: manifest version was not valid SemVer

godot.log line 16: "[WARN] Mod card_editor declares version 10.0 which is not a valid Semantic Version" - the GAME validates manifest versions as SemVer and uses mod identity/version for the multiplayer mod-parity check ("MODDED (n)" + lobby checkmark). An unparseable version plausibly breaks the between-players mod match, leaving the ready checkmark stuck. ("7.7" was equally invalid, so the weird checkmark may predate us; making it valid is correct regardless.)
Fix: card_editor.json version "10.0" -> "10.0.0" (repo + deployed + built cfiles + release zip). No DLL change needed. BOTH players need the fixed manifest AND the same DLL.
Green-text upgrade-preview regression: perf-pass gate, self-scaling restore reorder, and fuse inherit all examined and provably innocent for untouched cards (gated passes are override-guarded no-ops; old code checked the same marker post-parse). Root cause NOT yet identified - needs a bisect data point (does green break on a fresh un-edited card? which dated DLL backup last showed green?).
Next Step: user retests MP with matched builds; user bisects green via bak-2026-07-19b/20a/20c/20d or reports whether an untouched card also loses green.
## 2026-07-20 - Perf pass shipped: 20/22 audit findings + 6 review fixes (user: "choppy on occasion")

2-agent audit (wf_705c7bbc-8f8, 22 verified findings) then implementation (agent a7b20519) then 2-agent adversarial review (wf_bceb33c5-336, 6 findings, all fixed):
CORE: new CardEditorRuntimeCacheVersion (one Interlocked counter, ~30 bump seams across every store/controller that can change effective effects) + CardEditorRuntimeCaches (per-card mod-touched gate + 16-kind presence bitmask keyed on version+upgrade level; ALWAYS bypassed when the combat has temp/aura grants - over-eager by design, stale reads impossible).
WINS: damage computations no longer rebuild effect lists ~5x (pair-fetch + bitmask gate); TargetType/glow/ShouldPlay/description postfixes early-out for unmodified cards; self-scaling JSON parse reordered AFTER the marker check (was parsing per hover frame); hand refresh no longer O(n^2); quest counters batch disk writes (was full JSON write PER DAMAGE TICK) with in-memory authoritative reads; compiled delegates replace PropertyInfo.GetValue/MethodInfo.Invoke hot spots; settings lock fast path; Marked icon cache; text-snapshot audit moved off the first combat frame to the menu seam.
SKIPPED (unprovable-safe, deliberate): fused-effects list memoization (consumers mutate the fresh clones), description append-block cache (text embeds live combat values), resource-count wrapper guard (would lose history on mid-combat live edits).
REVIEW FIXES: quest-presence deck stamp now includes upgrade level (upgrade-only quest rows kept recording after campfire smith - was a silent blocker); fallback patch seam registers the scope Finalizers with their Prefixes (prefix-only registration would leak the ThreadStatic flags permanently - blocker); SetDraftMeta/ClearDraftMeta bump the cache version (live editor edits); FlushIfDirty keeps the dirty flag on failed saves (OneDrive IO retry); Save returns success; end-of-player-turn flush bounds crash loss to one turn.
Build: 0 errors / 278 warnings (beta baseline). NOT deployed (rides with b469dff upgrade-link fix).
Next Step: deploy on request; feel-test = hover attack cards during a big fight (numbers identical, smoother), live-edit a card mid-combat (updates apply immediately - proves bump seams), quest counters survive quit-after-turn.
## 2026-07-23 - Performance pass: 22-finding audit implemented (version-keyed cache backbone)

Hypothesis: the audited hot paths (5x effects rebuild per damage computation, per-event deck rescans, per-increment JSON disk writes, reflection-per-call) could all be gated by ONE central version counter without any behavior change.
Finding: True for 20/22 findings; 2 skipped as unprovable-safe (GetEffectiveExtraEffects fused-list memoization - callers may mutate returned clones; description append-block cache - text depends on live combat values no counter covers).
Built:
- Backbone: CardEditorRuntimeCacheVersion (Interlocked counter) bumped from EVERY effect-surface mutation seam: CardEditorOverrides (stored+instance), UiState drafts, CreatedCardsStore + DefinitionStore (all Revision++ sites), temporary extra-effect controller, matching-card aura controller (incl. AppliedCards), stateful+dynamic transform markers, self-scaling payload writes. CardEditorRuntimeCaches: per-card HasAnyModSurface gate + per-kind presence bitmask (16 kinds), cached per (version, upgrade level, combat identity); combat grants ALWAYS bypass caches (pile-dependent aura effects are never cached).
- Gated paths: HasActiveIgnoreEffect (+ pair helper: 2 kinds from 1 effects fetch; verbose-debug path untouched), ShouldGlowGold/glow color, ShouldPlay (per-hand-card PlayPermission/PlayPrevention bits), TargetType HitsAllEnemies, card damage bonus, self-pile auto sweeps (per-card bits + per-player candidate pre-scan skipping HookPlayerChoiceContext allocs), dynamic cost-adjust refresh (CountEvent set cache + no-overrides pile-walk skip), vanilla description postfix (front gate; RetainHand/Colossus/Blur fixes + FormatDescription stay outside - they can touch un-overridden cards).
- Self-scaling: marker check now BEFORE JSON deserialize in both restore paths (pure reorder).
- Per-power damage prefixes reuse ModifyDamageInternal's flags via ThreadStatic scopes (Prefix __state + Finalizer, snapshot-nested); hook-listener snapshot fetched once per computation, reused across phases.
- Reflection: compiled Expression getters/invokers replace PropertyInfo.GetValue (GetConcreteCombatState) and MethodInfo.Invoke (IterateHookListeners), reflection-closure fallback.
- Quests: per-player RunProgress presence cache (version + exact deck-reference stamp) early-outs RecordRunProgress/RecordCardProgress; persistent counter store is in-memory authoritative with dirty-flag batching (immediate flush out of combat, flush at combat end + quest completion), run-key/instance-key cached per run/card validated by an exact deck snapshot token (no hashing); JsonSerializerOptions hoisted.
- Small: settings volatile double-checked lock, Marked power icon cache, text-snapshot audit moved to the NMainMenu._Ready seam (one-shot guard keeps the old site as no-op fallback).
Build: 0 errors / 278 warnings (beta baseline, exact). NOT deployed, NOT committed.
Next Step: deploy on request; smoke test = hover an attack with an Ignore-caps override (numbers unchanged), play a quest run (counters persist across relaunch after combat end), live-edit a card mid-combat (text/glow update immediately = bump seams work).

## 2026-07-20 - Upgrade fuse also erased AMOUNT-source links ("equal to Poison" -> "Deal 4")

User: base card "Deal damage equal to the Poison applied" turned into "Deal 4 damage to ALL enemies" once upgraded in gameplay (editor showed the link fine - it hydrates base rows; the runtime fuse lost it).
Hypothesis: same disease as the 07-19 Pierce fix - MergeUpgradeBaseSlotEffect stomps link fields with the upgrade row's defaults.
Finding: True. :39394-39404 unconditionally copied AmountSourceMode/EffectId/Multiplier + ValueSource* + MultiplierSource* + CardSelectionSourceEffectId from the upgrade row; an upgrade row authored as a plain "+4 damage" delta carries Fixed/blank and erased the base link.
Fix: inherit-from-base rules matching the CardMatch*/CustomKeywordName precedent - amount group copied only when the upgrade row actually authors a link (mode != Fixed or id non-blank); multiplier group only when MultiplierSourceMode != default; CardSelectionSourceEffectId only when non-blank.
Also: EndlessUpgrades untick now writes an explicit null in both save builders (was set-true-only; fresh-override path was safe but any cloned-override path could keep a stale true). NOTE for user: the reported "endless upgrades while tag disabled" screenshot actually shows the tag TICKED - the 4->8->12 growth was the flag working on the raw +4 delta after the link was lost. Untick + Apply should stop it post-fix; if not, reopen as a bug.
Build: 0 errors / 278 warnings (beta baseline). NOT deployed. Perf audit running separately (wf_705c7bbc-8f8).
Next Step: deploy on request; test = upgrade the Poison card in a run -> text stays "equal to the Poison applied", damage scales with poison, and unticking Endless Upgrades stops repeat upgrades.
## 2026-07-20 - Quest Reward Persistence: absorbed rows keep their triggers

User: quest rewards flatten the absorbed row ("start of combat: gain 7 block" fired once on completion). Root cause: ResolveQuestRewardEffects:564 hard-set Trigger=OnPlay/Timing=Immediate on every absorbed clone.
Built (agent af275ea9, verified build):
- QuestRewardStyle on quest rows: Instant (old) | Keep trigger - this combat | Keep trigger - rest of run. New enum + CardExtraEffect field + DTO (string, default-safe) + both save builders + hydration; SyncProtocolVersion 2 -> 3.
- Recurring triggers supported: Deck Passive Combat Start, Turn Boundary (all 4 edges x your/enemy incl. legacy Start/EndOfTurn variants).
- ZERO new run storage: run-scoped installs are DERIVED - deck-resident quest cards via the persistent completion counter; removed quest cards (final completion removes them!) via the game's own save-persistent map history (PlayerMapPointHistoryEntry.CompletedQuests + CardsRemoved snapshots rebuilt with CardModel.FromSerializable). Combat scope: ConditionalWeakTable per CombatState.
- Dispatch at 8 sites: combat-start deck-passive loops (x2, idempotent) + six turn-boundary wrappers; fires via the quest synthetic-play pattern with loop guard, per-fire flattened clone, try/catch so a bad passive never crashes combat.
- Quest card text appends "Triggered rewards persist this combat / for the rest of the run."
Known edges (logged, acceptable): completing a KeepTriggerCombat quest OUT of combat installs nothing that fight; a removed card with multiple KeepTriggerRun quest effects derives all of them (history stores card id only); upgrade-fusion equality ignores the new field.
Build: 0 errors / 278 warnings (beta baseline). NOT deployed.
Next Step: deploy on request; in-game test = quest "exhaust 10 cards" + reward row "Combat Start: Gain 7 Block" style=rest-of-run -> complete it, verify block at start of every later combat and after save/reload.
## 2026-07-20 - Cards freeze on beta: retargeted mod back to v0.109 beta APIs

User on public beta v0.109.0 (2026.07.17); cards froze mid-play.
Hypothesis: the deployed DLL targets the 2026-07-18 PUBLIC build's APIs, but the game is now the v0.109 beta with the older signatures.
Finding: True. godot.log: repeated MissingMethodException 'AbstractModel.ModifyDamageCap(Creature, ValueProp, Creature, CardModel)' (my 4-arg public-targeted call) in the damage path; game sts2.dll (9,609,216 bytes) HAS set_Player/CardLocation/BeforeSideTurnEnd + the 5-arg ModifyDamage family = beta. The 07-19 compat pass had retargeted to public.
Decision (user): beta only. C# override signatures (Marked power ModifyDamageMultiplicative) can't be adaptive in one DLL, so one build must pick a branch.
Fix: git revert of the compat commit 556ed3b (code files only; FINDINGS kept via checkout --ours), restoring all beta signatures while keeping every feature commit after it (Chainboard C4/C5, chain-aware text, P1.5). Build vs the beta sts2.dll: 0 errors / 278 warnings = the ORIGINAL beta baseline (compat pass had made it 274 by deleting beta-only code), confirming exact known-good restore.
CHANGELOG compat line changed from "2026-07-18 update" to "public beta branch (v0.109.0)".
Deploy blocked while game running (loaded DLL lock) - user must close the game.
Next Step: deploy to unfreeze; rebuild the 8.0 release zip from the beta DLL; if the public branch ever matters, a conditional-compilation dual build is the real fix.
## 2026-07-19 - P1.5 PhraseComposer consolidation (pre-release hygiene)

Hypothesis: the P1 multiplayer-wording pass left byte-identical phrase arms duplicated across formatters, consolidatable with zero text drift.
Finding: Partially true - 3 switch pairs (applyDebuff, signedPower, apply-equal-to; 8 arms each, character-identical) + 4 self-contained helpers were true duplicates; the bulk of per-target switches differ per-kind (verbs/loc keys, each exists once) and were correctly left alone.
What changed: new CardEditorPhraseComposer.cs holds ApplyPlayerDrawSubject, FormatPlayerResourceArm, FormatEqualToPlayerResourceText, FormatApplyEqualToText, FormatApplyDebuffPayload, FormatSignedPowerPayload, and the custom-name resolver NormalizeCustomName (NormalizeCustomKeywordName stays as a thin wrapper - 16 callers untouched). 24 duplicate arms deduped; CardEditorExtraEffects.cs shrank 132 lines. Deliberately NOT moved: ApplyPowerTriggerPrefix (single copy, deeply coupled), ~20 per-target switches with differing arms, upgrade-merge custom-name ternaries (non-display code).
Invariant: byte-identical output - established at code level (verbatim moves + character-identical merges only; CardEditorLoc.T verified pure). The boot-time text-snapshot audit is the end-to-end check on next game launch: any diff logs loudly.
Build: 0 errors / 274 warnings (baseline, unchanged). Ships in the 8.0 release package.
Next Step: launch the game once before the Nexus upload and confirm no snapshot-audit diff in godot.log.
## 2026-07-19 - Chain-aware card text: 17 formatter gaps closed (user report)

User: "card panel changes dont reflect in the card text - they are standalone and dont say what they do". 2-agent audit (wf_171b2e21-8eb) swept all ConsumesCards/DynamicAmount kinds vs their formatters: 18 gaps, 17 real (ChooseOneEffectSource's SelectedByEffect is unreachable at runtime - skipped).
Implemented (agent ac600bc8, verified build): SelectedByEffect arms with "those cards" vanilla phrasing for DrawCards, DrawCardsThatCostLess ("Draw those cards. They cost [E] less."), GrantKeywordToPile (gain/lose), MoveCardsBetweenPiles (Discard/Exhaust verb shortcuts kept, "from pile" clause dropped), DiscardCards, ExhaustCards, UpgradeCardsInPile, SelectCardsFromPile, ConsumeCardValue ("their" plural + per-action verbs), DelayedPileAction (shared BuildDelayedPileSelectionText arm fixes both), TransformCards, RemoveCardsFromDeck ("deck versions of those cards"), GrantReplay, TargetCardMutation + PersistentTargetCardMutation (shared FormatSelfScalingLine recipient suffix "on those cards"). Amount links: ApplyMarked now renders "Apply Marked equal to <reference>" (was: no trace of the link); EvokeOrbs linked amounts bypass the hardcoded "twice" shortcut -> "that many times" + reference suffix.
30 new loc keys, 2 formatter signatures extended, draw prefixes preserved for ApplyPlayerDrawSubject player rewrites.
Known follow-ups: EvokeOrbs AmountIsX still hits the "twice" shortcut; ChooseOneEffectSource SelectedByEffect plumbing inert.
Build: 0 errors / 274 warnings (baseline). NOT deployed - queued with the 8.0 release package.
Next Step: deploy on request; in-game check = Select->Draw chain card should read "Select those cards. Draw those cards."
## 2026-07-19 - Release prep: version 8.0, changelog, manifest in repo

- card_editor.json brought into the repo (was only in the deployed folder) and bumped 7.7 -> 8.0 with a Chainboard mention in the description.
- csproj gains <Version>8.0.0</Version> - the MP sync guard's ModVersion label now reads a real version (verified: built DLL stamps 8.0.0.0).
- CHANGELOG.md (repo root): Nexus-pasteable 8.0 notes distilled from FINDINGS - Chainboard, Card Engine (selection/value bus, inline branches, grants, truthful targets, vanilla verbiage), QoL sweep, 2026-07-18 game-update compatibility, MP same-build requirement.
Build: 0 errors / 274 warnings (baseline). Deploy = DLL + PDB + card_editor.json this time.
Next Step: deploy on request; P1.5 (PhraseComposer hygiene) stays queued for a fresh session.
## 2026-07-19 - Chainboard C5 polish: live chain sentences + chain badges

- LIVE SENTENCE: every multi-step chain shows its actual rules text under the strip - built by the same BuildOverrideFromUi + TryFormatLineForAudit pipeline the preview/audit use, markup stripped with REAL numbers kept (StripMarkupForHint gained replaceDigits:false). Perf-guarded: skipped while typing in a chip (strip repaints per keystroke) and while lazy row hydration is still pending (the builder would force-complete it at popup-open).
- CHAIN BADGE: classic Effect List rows that participate in a chain (card link, amount link, or board sequence link) carry a chain-link glyph in their title, so chain membership is visible from the classic list too.
Build: 0 errors / 274 warnings (baseline).
Next Step: deployed with d51b74d + 54598ed (wrap/drag + persistence) per user instruction.
## 2026-07-19 - Chain groupings now persist (chain_links.json sidecar)

The drag-built/plus-built sequence links were session-only - chains with no real card/amount link fell apart on reopen. Now:
- New CardEditorChainLinkStore: user://card_editor/chain_links.json, cardKey -> (effectId -> sourceEffectId), System.Text.Json, lazy load + write-through saves. UI sidecar ONLY - card override DTO and MP snapshot untouched.
- Popup loads the card's links on first strip paint per retarget (cached-popup safe via _chainLinksLoadedForCardKey), writes through on drag-attach, "+" fallback links, and prunes links (and dependents) when a row is removed.
Build: 0 errors / 274 warnings (baseline). NOT deployed - queued with the wrap/drag rework (d51b74d).
Next Step: deploy on request; check = build a chain from two unlinkable effects via drag, close + reopen the card, the chain is still one strip.
## 2026-07-19 - Chainboard UX rework: no inner scrollbars, "+" always joins, drag-to-chain

User verdict on deployed C4: inner strip scrollbars "hurt visibility" (main scrollbar exists), "+" spawned the new effect as a separate strip below when the picked kind had no card/amount link, and there was no mouse way to attach steps.
- Strips are now HFlowContainers: long chains WRAP and the Effect Chains section simply grows taller - zero inner scrollbars, the main editor scrollbar is the only one. Height clamps and per-strip ScrollContainers deleted.
- "+" (strip end AND connectors) inserts right after the chain's last/left row AND always joins that chain: real auto-wire when the registry allows, otherwise a board-side sequence link (_chainManualSequenceLinks, session-scoped, plain dash connector; real card/amount links take render precedence and are still the only runtime-meaningful ones).
- Drag & drop via Control.SetDragForwarding: grab any chain card, drop it on another - it attaches to that chain (sequence link) and repositions right after the target through MoveExtraEffectRow (stall-guarded loop). Disabled in the upgrade editor and for delta rows (index-pairing hazard).
Known limit (documented): sequence links are session-scoped - chains held together ONLY by them regroup as singletons after reopening the card; chains with any real link survive. Persisting them needs a UI-sidecar store (candidate for C5).
Build: 0 errors / 274 warnings (baseline). NOT deployed yet.
Next Step: deploy on request (game closed); check = a 3+ box chain wraps instead of scrolling, "+" lands the step in the same strip, dragging a box onto another joins them.
## 2026-07-19 - Chainboard C4 adversarial review: 2 blockers + 3 should-fixes, all fixed

Two-agent hostile review (Godot lifecycle + state consistency) of the board-native authoring code. Confirmed findings, all fixed same day:
- BLOCKER chip typing: every keystroke's classic TextChanged handler queues a same-frame strip rebuild that freed the focused mirror field (one character per click). Fix: rebuild now captures the focused chip (by source-control instance id via metadata) + caret and restores focus on the recreated mirror; the FocusExited repaint hook is gone (each keystroke's deferred repaint keeps everything fresh).
- BLOCKER upgrade editor: insert-between placed a non-delta row inside the delta block, corrupting the upgrade save's absolute-index base/delta pairing. Fix: connector "+" hidden in the upgrade editor + OnAddChainStep ignores insertAfterRowIndex there.
- Expanded card clipped at fixed 260px with scrolling disabled (count-based IF conditions overflow past the action row). Fix: strip height = content min height clamped 120-380, VerticalScrollMode Auto when expanded, expanded box gets ExpandFill so chips wrap at real width.
- Clicks on chip labels/gaps collapsed the card mid-edit (whole panel was the toggle). Fix: collapsed box = whole-card expand target; expanded card collapses only via the title row.
- "+ IF" bypassed the classic branch-availability gate (Quest rows). Fix: offer gated on ScalingToggleRow.Visible, matching the classic toggle.
- Nits: dropped the chip handlers' synchronous summary repaints (the classic deferred pass repaints identically - halves rebuilds per edit); _expandedChainEffectId now cleared on row removal and popup retarget. Refuted by review: EmitSignal write-through (works, long-vs-int marshalling fine), free-during-callback crashes (QueueFreeSafely defers), LineEdit loops.
Build: 0 errors / 274 warnings (baseline). NOT deployed - ships with the compat pass.
Next Step: deploy on request (game closed); board checklist = type a multi-digit amount in a chip, expand a card with a count IF (everything reachable), background click while editing must NOT collapse.
## 2026-07-19 - Game update (2026-07-18 13:57) broke the deployed mod: full compat pass

Hypothesis: the "cant load my library / a bug has occured" popup and dead patches came from the Steam update changing game APIs under the deployed DLL (built 11:08, update landed 13:57).
Finding: True.
Evidence: godot.log - MissingMethodException CardPlay.set_Player at NCardLibraryGrid.InitGrid (the library crash) + 10 "[CardEditor] Skipping incompatible Harmony patch" warnings at boot; sts2.dll mtime 13:57; rebuild against the new DLL surfaced 46 unique compile errors.
API changes handled (new shapes read from the game's fresh sts2.xml + a reflection probe):
- CardPlay lost Player entirely (owner now flows from Card) - 27 initializer sites cleaned via compiler-line-verified script.
- Hook.BeforeSideTurnEnd/AfterSideTurnEnd -> Hook.BeforeTurnEnd/AfterTurnEnd(ICombatState, CombatSide, IEnumerable<Creature>) - 6 patch classes retargeted with is-not-CombatState downcast guards.
- Hook.ModifyCardPlayResultLocation + CardLocation record REMOVED (v0.109 un-reverted) -> Hook.ModifyCardPlayResultPileTypeAndPosition with (PileType, CardPilePosition) tuple result; patch restored from pre-v0.109 git history (b13ee61~1).
- Hook.ModifyDamage / ModifyDamageInternal / AbstractModel.ModifyDamage{Additive,Multiplicative,Cap} dropped the CardPlay param; IgnoreCaps prefix + Marked power override + preview call sites updated; combatState is ICombatState now (GetConcreteCombatState() compat shim).
- CreatureCmd.Damage overloads dropped the trailing CardPlay; AttackCommand.FromCard(card) / FromOsty(osty, card) slimmed; CreatureCmd.LoseBlock(creature, amount); PotionFactory.GetPotionOptions gained a required blacklist param (empty passed).
Build: 0 errors / 274 warnings (NEW baseline, was 278 - four warnings lived in deleted code). NOT deployed yet; C4 board-native authoring rides in the same DLL (adversarial review re-running: wf_0b9fff6b-be2; the first run was interrupted by the session break).
Next Step: deploy on request (game must be CLOSED); post-deploy check = library opens, Pierce ignores Block in combat, turn-start/turn-end triggers fire; then check godot.log for any remaining "Skipping incompatible" warnings.
## 2026-07-18 - Chainboard relocated: full-width board in the main column, movable card boxes

User feedback: the Effect Chains panel was squeezed into the 340px left sidebar under the Effect List - boxes clipped, strips unreadable. It belongs in the MAIN content column (where the Numbers section lives), horizontal left-to-right.
- Left-column panel deleted; the board is now a full-width section in the right column, directly after the Extra Effects list and above Numbers. Strips get the whole main-column width and h-scroll only when a chain is genuinely long.
- Boxes rebuilt as cards (min 180px wide, padded): title + X on top, Trigger/Target line, then a bottom row with move-earlier/move-later arrow buttons wired to the existing MoveExtraEffectRow (which already rewires by StableEffectId and repaints the strip via RefreshEffectSummaryList). Upgrade-delta rows stay pinned (no move/remove).
- Ordering fix: the container is created AFTER BuildExtraEffectsUi now, so an immediate RefreshEffectChainStrip() paints rows hydrated before it existed.
Build: 0 errors / 278 warnings (baseline). NOT deployed yet.
Next Step: deploy on request; C4 polish queue = true drag-and-drop reorder, live sentence under strips, chain badges on classic rows.
## 2026-07-18 - Pierce/Sovereign-Blade bug: upgrade fuse erased the card-match filter

Hypothesis: the grant flow falls back to a null (match-everything) hand filter when the filtered candidate set is empty.
Finding: Partially true - the null-filter fallback exists but is LATENT (unreachable via current callers); the LIVE route is the upgrade fuse.
Evidence: 2-agent trace + created_cards.json (CARD.CARD_EDITOR_CREATED_CARD18): base Pierce grant rows have CardMatchMode=CardId/MatchCardId=CARD.SOVEREIGN_BLADE, but Upgrade.ExtraEffects[2] saved default Any/null; MergeUpgradeBaseSlotEffect (:39288) unconditionally overwrote the fused row with the upgrade values, so the upgraded card's grant row lost the restriction -> candidates = whole hand -> Choose picker over anything. The sibling row that KEPT its filter correctly no-ops when SB is absent (empty-candidate guards are sound).
Reason: the fuse had an inherit rule for CustomKeywordName but none for the CardMatch* fields; two secondary fail-open paths (unparseable MatchCardId -> match-all; empty candidateSet -> null FromHand filter) could produce the same symptom on other cards.
Fix (3 guards in CardEditorExtraEffects.cs): (1) fuse inherits base CardMatch*/Match* fields when the upgrade row's CardMatchMode is Any (mirrors the CustomKeywordName rule) - fixes the user's card with NO data repair needed; (2) PassesCardMatchFilter fails CLOSED on a non-blank unparseable MatchCardId; (3) SelectCardsFromCandidates returns an empty selection instead of passing a null filter to FromHand.
Build: 0 errors / 278 warnings (baseline). NOT deployed yet.
Next Step: deploy on request; in-game check = play upgraded Pierce card with no Sovereign Blade anywhere (expect silent no-op, no picker) and with SB in hand (expect only SB selectable).
## 2026-07-18 - Chainboard C3 SHIPPED (code): chips, IF prefixes, click-to-jump, per-box remove

- BOX CHIPS: each box now shows "N. Kind Amount" + a dim "Trigger • Target" line (same summary helpers as the Effect List, so wording stays in sync).
- IF CHIP: a step whose Branch tickbox is active gets a small "IF <condition type>" prefix chip in the strip - conditions are visible in the chain, click to edit.
- CLICK-TO-JUMP: clicking any box (or IF chip) hydrates pending rows and scrolls the right column to that effect's full editor (new _rightColumnScroll ref + deferred scroll so post-hydration layout is current). Boxes get the pointing-hand cursor + tooltip.
- PER-BOX REMOVE: a compact X on each box calls the same RemoveExtraEffectRow as the classic list (hidden on upgrade-delta rows, matching the summary panel's rule).
Build: 0 errors / 278 warnings (baseline). NOT deployed (undeployed: C3).
Chainboard remaining (C4, polish tier): drag reorder, live sentence under strips, chain badges on classic rows, collapse-classic option.

## 2026-07-18 - Chainboard C2 SHIPPED (code): the strip authors chains

The Effect Chains panel gains authoring:
- "+" AT THE END OF EACH STRIP: opens the categorized/searchable kind picker (full definitions list, hints included) and appends a row PRE-WIRED to the chain: if the new kind ConsumesCards and the last step publishes -> CardSelectionMode=SelectedByEffect + source id set (no dropdowns touched); else if it takes DynamicAmount -> AmountSourceMode=AppliedEffectRow + source id ("deal damage = cards drawn" in two clicks); else plain sequenced row.
- "+ New Chain" (also shown when the card has no effects): picker-driven unlinked row - the classic Add Effect with the better picker.
- FORWARD-LINK WARNING: a consumer reordered ABOVE its source renders its connector as "(warn) #n" with a tooltip (rows execute in order; the link finds nothing until moved back) - reordering can't silently kill a chain anymore.
- Registry correction: DrawCards/DrawCardsThatCostLess gained ConsumesCards (the P2 consume path existed; the flag was missed - auto-wiring reads it).
Auto-wire uses the registry (publishes/consumes/dynamic-amount), so future capability changes flow into the panel automatically.
Build: 0 errors / 278 warnings (baseline). NOT deployed (undeployed: C2).
Next: C3 - IF-boxes + setting chips on the boxes; box click-to-scroll; step removal from the strip.

## 2026-07-18 - Chainboard C0+C1 SHIPPED (code): transform publishes results + read-only chain strips

C0 (the prerequisite gap found in design verification): TransformCardsWithCurrentPlayDeferral now re-publishes the REPLACEMENT cards via ReplaceCurrentSelectedCards after the immediate transforms - "transform a card, then shuffle IT into your deck" chains onto the result instead of the vanished original (generator re-publish pattern). Deferred self-transforms excluded (they resolve after the play).
C1 (per CHAINBOARD_PLAN.md): new "Effect Chains" section under the Effect List (left column) rendering the live rows as linked box strips:
- Rows connected by card links (SelectedByEffect, move OR grant mode) or amount links union into one strip; unlinked rows are singleton boxes. Boxes show "N. Kind" + trigger; strips h-scroll.
- Connectors: "──▶" card link from the previous box, "─ ─▶" amount link, "#n ▶" non-adjacent card link, "= #n ▶" non-adjacent amount link, "—" grouped without a direct neighbor link.
- Pure VIEW over _extraEffectRows (StableEffectId + CardSelectionSourceEffectId + AmountSourceEffectId), rebuilt with RefreshEffectSummaryList, cleared on popup reset. Zero persistence, zero behavior change.
Next: C2 authoring (+ Step with auto-wiring, reorder, New Chain), C3 condition/setting chips.
Build: 0 errors / 278 warnings (baseline). NOT deployed (undeployed: draw-save fix-up + C0 + C1).

## 2026-07-17 - P2 fix-up (user repro): "draw those cards" mode never SAVED for Draw rows

User in-game: no way to wire "add 10 random cards, draw those". Root cause: P2 made the selection-Mode dropdown VISIBLE for DrawCards and the runtime consume path exists, but the SAVE builders' kind list (NCardEditorPopup ~:32656) excluded DrawCards - its "else if" branch (:32738) only persisted the pile, so Mode=Selected By Effect and the source row id were silently dropped on Apply (mode saved as Choose, source null, runtime never consumed).
Fix (both base + upgrade builders): DrawCards/DrawCardsThatCostLess added to the enclosing kind list; the draw branch persists SelectedByEffect (only that mode - other modes keep the classic draw default so nothing else changes). With this, the source-picker row also appears (its visibility keys off the mode) and the chain round-trips.
WORKING RECIPE (after deploy): Row 1 = Card Generation, Amount 10, Destination = Draw Pile; Row 2 = Draw Cards, Amount 10, Mode = Selected By Effect, Selected Row = Row 1.
Build: 0 errors / 278 warnings (baseline). NOT deployed yet.

## 2026-07-17 - Card Engine P5 SHIPPED (code): value bus, honestly scoped

Per the critique's rescoping (the original "remove the metric frame gate" idea was proven near-worthless and double-count-prone):
- DYNAMIC AMOUNTS widened (whitelist + registry D flags, parity-audited): all orb effects (Channel x6, Gain/LoseOrbSlots, EvokeOrbs - "Channel Lightning equal to the damage dealt") plus the three flagged whitelist oversights: ApplyMarked, DrawUntilHandSize, DrawAndCheck. OrbAction stays excluded (per-action amount semantics vary).
- LOSEHP JOINS THE METRIC BUS: its executor now captures and reports DamageResults (totals/instances/blocked/overkill/kills), and the two frame gates (ReportCurrentDamageTotals, ReportCurrentKillCount) allowlist LoseHp - "draw cards equal to Kills from row 1" works off an HP-loss row. Exact-kind allowlist extension, NOT a gate removal, so nested wrappers still cannot double-count.
- DEFERRED with reasons: dynamic amounts for cost-modifier rows (they are PASSIVE - read by the cost pipeline outside any play session, so per-play dynamic sources do not exist at evaluation time; needs a different design); DoT/turn-end kill attribution (needs a combat-scoped metric store - separate project).
Build: 0 errors / 278 warnings (baseline). NOT deployed (wave: P2+P3+P4+P5+protocol v2).
Next Step: deploy the wave + in-game checklists; then P1.5 (composer hygiene) or P6 (step UI) per user pick.

## 2026-07-17 - Card Engine P4 SHIPPED (code): 16 pile/deck actions grantable + truthful target dropdowns

Hypothesis (verified by a 2-agent adversarial pass first): the 16 pile-op grant exclusions were blanket conservatism (only HitsAllEnemies had a stated reason); granted rows execute through the recipient's REAL play pipeline identically to native rows; the one universal hazard is the grant normalizer stripping selection filters ("remove all Curses" would become "remove your whole deck").
Finding: True - all 16 enabled WITH the normalizer fix.
GRANTS ("give this card: when played, discard 1" - no helper cards):
- SupportsGrantToCard exclusion list shrunk by 16: Discard/Exhaust/Move/UpgradeInPile/SelectFromPile/PlayFromPile/GrantKeyword/Consume/DrawThatCostLess/Delayed/CopyToDeck x2/RemoveFromDeck/UpgradeDeck/AddExactCopy/Shuffle. Still excluded: created-card modifiers/auras, self-pile auto-actions, meta wrappers, passives, LinkedCardAction, HitsAllEnemies (documented soft-lock).
- Editor gate: the redundant per-kind !isX chain in canGrantToCard deleted - SupportsGrantToCard (registry-audited) is the single truth.
- Registry: G flag added to all 16 (boot parity audit keeps predicate and registry honest).
- NORMALIZER FIX (the load-bearing piece): NormalizeGrantedPayloadSelection no longer strips selection filters for pile/deck-action payloads (GrantPayloadKeepsSelectionFilters set). Also fixes the GrantKeywordToPile future-aura overreach. Draw kinds keep their dedicated reshaping; simple payloads still clear stale filters.
- Verified safety per kind: real choiceContext/cardPlay in granted execution; auto-play loop guard caps PlayCardFromPile chains; AddExactCopyOfThisCardToDeck retargets "this card" to the RECIPIENT (sensible, documented); deck copies do NOT inherit granted rows (no self-replication).
TARGET TRUTHFULNESS:
- ConfigureExtraEffectTargets + ConfigureCardSmithTargets skip the multiplayer expansion for registry IgnoresTarget kinds - Gain Max HP/Summon/Forge/Channel/etc. no longer offer "Any Ally" that silently behaves as Self.
- AS-POWER EXEMPT: power rows keep the expansion (ResolvePowerHostCreatures genuinely resolves ally/player hosts; the advertised "attach to an ally" configs keep working). Toggling Power rebuilds the target list preserving the current pick.
- PRESERVATION: a saved row with a now-gated target gets its value appended as an extra dropdown item (EventTarget-append pattern) - persisted state is never silently rewritten; self-heals when the user re-picks. OrbAction's hand-edited Target=Target (TriggerPassive) preserved too.
Build: 0 errors / 278 warnings (baseline). NOT deployed (undeployed: P2+P3+P4+protocol v2).
Next Step: in-game checklist for newly-granted kinds (one combat each per the plan discipline); P5 (value bus, honest scope) after.

## 2026-07-17 - Card Engine P3 SHIPPED (code): inline branch payloads - no more helper cards

Hypothesis: branch payloads are UI-locked only; an inline editor writing effect.BranchEffect directly needs zero runtime/DTO/wire changes.
Finding: True (verified: GetUsableBranchEffect passes any non-RunEffectSourceCard BranchEffect; DTO recurses; branch TEXT already re-enters TryFormatLine so inline payloads self-describe).
What shipped (NCardEditorPopup.cs only):
- "Branch Payload" style dropdown on every branch section: [Effect-Source Card | Inline Effect]. Effect-Source path byte-identical to before. Style inferred on load (existing saves with a RunEffectSourceCard branch open as Effect-Source; hand-edited inline JSON opens as Inline).
- Inline editor row: kind (starter whitelist of 16 amount-only kinds: damage/block/draw/heal/lose HP/energy/stars/gold/5 debuffs/strength/dex/thorns), amount, target (all 8, vanilla labels). Saved as BranchEffect { Kind, Amount, Target, OnPlay, Immediate } by BOTH builders (base + upgrade).
- RECIPE: any effect row -> tick Branch -> condition (e.g. Fatal) -> Branch Payload = Inline Effect -> Draw Cards, 2 => "If Fatal: draw 2." on ONE card. Card text renders automatically (branch suffix re-enters the line formatter).
- Whitelist widens per release as configurations get verified; UI nesting stays 1 level (wire cap 8, runtime cap 16 untouched).
Build: 0 errors / 278 warnings (baseline). NOT deployed (undeployed batch: P2 selection bus + protocol v2 + P3 inline branches - both players must update together when this deploys).
Next Step: in-game test branch recipes; P4 (grantable pile ops + target routing) after.

## 2026-07-17 - Card Engine P2 SHIPPED (code): selection bus - "create X, then act on X"

Hypothesis: the selection-bus walls are a missing publish call, a too-narrow UI list, a stripped chain, and a missing consume path - all fixable without touching persistence.
Finding: True. Four fixes + the first protocol bump:
- FETCH PUBLISH BUG (one line): FetchSpecificCardsToHand now reports its fetched cards as the row's selection - it was offered as a "Selected Row" source but always yielded zero candidates downstream.
- GENERATORS AS SOURCES: CanPublishCardSelection now delegates to the capability registry (PublishesCardsUi | PublishesCardsRuntime) - first real registry consumer. Newly wireable sources: AddRandomCardToHand, ChooseOneOfThreeCardsToHand, AddSpecificCardToHand, AddCopyOfThisCard, AddExactCopyOfThisCardToDeck, PlayRandomGeneratedCard, ChooseOneEffectSource, LinkedCardAction, SelfScaling, PersistentSelfScaling, DrawUntilHandSize, DrawAndCheck (all verified runtime publishers). RECIPE: Row 1 = Card Generation, Row 2 = any card action with Selection "Selected By Effect" -> Source Row 1: "add a random Attack to your hand, it costs 1 less this combat".
- DRAWCARDS CONSUMES SELECTIONS: DrawMatchingCards gained a SelectedByEffect path - candidates come from the published selection (draw-able piles only: draw/discard/exhaust; stale entries skipped) and go through the SAME manual-draw pipeline (ShouldDraw gate, history, AfterCardDrawn, InvokeDrawn), so a consumed draw is a real draw. UI: the selection-mode dropdown now shows for DrawCards. RECIPE: Row 1 = Select Cards From Pile (scry-style), Row 2 = Draw Cards with Selection "Selected By Effect" -> "scry 3, draw those". Empty/exhausted chains end quietly (no NO_DRAW nag).
- GRANTS KEEP CHAINS: NormalizeGrantedPayloadSelection no longer strips CardSelectionSourceEffectId from granted draw payloads whose mode is SelectedByEffect - the source row executes in the same recipient play session, so the chain resolves.
- SYNC PROTOCOL v2: first behavior-changing release; the P0 guard now refuses mixed-build sync with the clear both-players-must-update message instead of silently diverging.
Build: 0 errors / 278 warnings (baseline). NOT deployed. Known pre-existing publish gaps left as-is (UpgradeDeckCards/EnchantCard partial runtime publish - were already offered by the old list).
Next Step: in-game test the two recipes; P3 (inline branch payloads) after.

## 2026-07-17 - Card Engine P1: PowerHost voice - hosted powers finally say where they live

The last audited wording gap: a power hosted on the trigger target or on each affected creature rendered EXACTLY like one on yourself ("At the start of your turn, gain 2 Strength." even when the power sits on an enemy). ApplyPowerTriggerPrefix now frames the whole trigger clause as the outermost wrap: "On the target: at the start of your turn, ..." (TriggerTarget host) / "On each affected creature: ..." (EffectTargets host). CardOwner/CardOwnerWatchOpponents hosts unchanged (the watch-opponents actor voice was already handled via the AnyEnemy trigger-from mapping). New loc keys cardText.powerTrigger.hostTarget/.hostEffectTargets.
With this, EVERY user-facing gap from the text audit is closed: 13 partial sites, 9 target-blind resource sites, creature commands, power-trigger actor, host voice. P1's remaining items are pure hygiene (PhraseComposer consolidation of the ~20 complete switches, custom-text line-key alias pass) - tracked as P1.5.
Build: 0 errors / 278 warnings (baseline). NOT deployed (undeployed: creature-command text + host voice).

## 2026-07-17 - Card Engine P1: creature commands now name their target (Stun/Kill/Escape/Heal/HP/Set)

FormatCreatureCommand was binary (AllEnemies or nothing): "Stun." for every non-AllEnemies target, "Heal X HP." with zero target wording. Now full 8-target switches: "[gold]Stun[/gold] a random enemy.", "Kill other enemies.", "Another player escapes.", "ALL players heal X HP.", "Set a random enemy's Max HP to X." - vanilla frames throughout; the historical Target/Self imperatives ("Stun.", "Heal X HP.") preserved so existing card text does not drift. GainBlock keeps delegating to the full FormatGainBlock.
Build: 0 errors / 278 warnings (baseline).
P1 part 2 remaining (next session, fresh context): PhraseComposer consolidation of the now-complete sites, PowerHost voice ("hosted on enemies" phrasing in trigger clauses), custom-text line-key alias pass for the intentionally changed lines (snapshot diff on next launch lists them).

## 2026-07-17 - Card Engine P1 part 1 SHIPPED: vanilla-verbiage target text at every audited gap

Hypothesis: all 13 partial + 9 target-blind formatter sites from the text audit can gain vanilla-corpus ally/player wording without touching Self/enemy lines (snapshot-locked).
Finding: True - every audited site now covered.
What changed (CardEditorExtraEffects.cs, all Self/enemy wording byte-identical):
- "Equal to {reference}" family COMPLETE: the 5 inline switches (DealDamage/GainBlock/RemoveBlock/Heal/LoseHp), FormatEqualToDebuffText, FormatEqualToSignedPowerText (one edit covers ~30 stat/power kinds), FormatEqualToApplyPowerText - all gained AllAllies="ALL players", AnyAlly="Another player/another player", AnyPlayer="Any player/any player" branches.
- Resource equal-to one-liners (Energy/Stars/Gold x gain/lose) -> new FormatEqualToPlayerResourceText with vanilla frames ("Another player gains Gold equal to X.").
- Plain player-resolved arms: GainEnergy/LoseEnergy/GainStars/LoseStars/GainGold/LoseGold -> FormatPlayerResourceArm ("Another player gains {energy}." per Believe in You; "ALL players gain ..." per Energy Surge). Draw family (DrawCards, DrawUntilHandSize, DrawAndCheck, DrawCardsThatCostLess) -> ApplyPlayerDrawSubject wrapper ("Another player draws 2 cards from their [gold]Discard Pile[/gold]." - Constellation/Tutor wording, your->their swap).
- FormatCopyDebuffs/FormatCopyBuffs/FormatMarked/FormatModifyActivePower: player branches added; ModifyActivePower also gained its MISSING OtherEnemies branch; CopyBuffs "to all allies" vanilla-ized to "to ALL players".
- FormatStatusToStatus: 3 player branches with singular-they possessive ("their {Source}").
- Power-trigger actor "an ally" -> "another player" (vanilla Sneaky wording).
Remaining P1 part 2 (hygiene, not user-facing gaps): consolidate the now-19 full sites onto one PhraseComposer, PowerHost voice ("Enemies with this power: ..."), FormatCreatureCommand subjects, custom-text line-key alias pass for the changed wording (snapshot harness will list exactly which lines moved on next launch).
Build: 0 errors / 278 warnings (baseline). NOT deployed.

## 2026-07-17 - Card Engine P0 SHIPPED (safety rails) + vanilla verbiage corpus mined

Hypothesis: P0 (snapshot harness, MP guard, capability registry) can land with zero behavior change, and vanilla has explicit ally-target phrasing to copy.
Finding: True on both; the verbiage research OVERTURNED our earlier ally wording.
P0 landed (all build-clean, 0 errors / 278 baseline, behavior-neutral):
- TEXT SNAPSHOT HARNESS (CardEditorTextSnapshotAudit.cs): boot renders every kind x all 8 targets through the real pipeline into user://card_editor/text_snapshot.current.txt, seeds baseline on first run, warns with before/after examples on drift. Delete baseline to accept intended changes.
- MP SYNC VERSION GUARD (CardEditorMultiplayerSync.cs): SyncProtocolVersion const (stays 1 until a behavior phase ships) + ModVersion label (assembly version + DLL timestamp) stamped into snapshots; mismatch now REFUSES loudly ("Both players must install the SAME card_editor build") instead of the old silent Version==1 drop.
- CAPABILITY REGISTRY (CardEditorEffectKindRegistry.cs): EffectCaps flags + EffectTargetSemantics per all 147 kinds, transcribed from the ~10 legacy predicate lists; boot audits check coverage (every enum member) AND parity against the 5 callable legacy predicates (SupportsGrantToCard/AsPower/Repeat/AppliedEffectRowAmountSource/HistoryScaling) - transcription errors surface as startup warnings on next launch (self-verifying; check the log after first boot!).
VANILLA VERBIAGE CORPUS (from localization/eng/cards.json+powers.json, all ~630 card strings; full conventions in the research output):
- Single ally = "another player" (14/16 vanilla AnyAlly cards; frames: "Another player gains X." / "Give another player X" / "Choose another player."); ally-including-self group = "ALL players" (caps); everyone-but-you = "other players"; possessive = singular "their". "ally/allies" is reserved for passive aura powers (Tank/Covered).
- Apply (enemy debuffs) vs Gain (self, incl. self-debuffs) vs Give (ally grants); "lose HP" vs "take damage" distinction; [gold] on keywords/piles, plain damage/HP; energy always icons; "this turn/combat" suffixes; "Whenever you X," trigger-first comma form; ALL-caps emphasis family (ALL/EVERYONE'S/ANYONE).
- APPLIED NOW: the 16 target-switch sites' ally branches rewritten to vanilla terms (An ally->Another player, ALL allies->ALL players, an ally's->another player's; 33 lines). Two pre-existing partial sites (CopyBuffs :15781, power-trigger actor :17142) intentionally left for the P1 composer migration.
- Registry research also confirmed/expanded the disagreement list (dead ResolveTargetPlayers cases for CardType/Drawn/Generated cost auras; GrantReplay standalone = silent no-op; UpgradeDeckCards/EnchantCard publish gaps; repeat-blacklist intent not enforced for 9 pile-op kinds) - all captured for P2/P4.
Next Step: launch the game once to seed the text baseline + verify registry parity comes back clean, then P1 (Sentence Composer on the vanilla corpus).

## 2026-07-17 - Card Engine rework: 9-agent audit + design bake-off -> CARD_ENGINE_PLAN.md

Hypothesis: universal effect composition requires reshaping the persisted model; rival: every composability wall is a hand-maintained list or UI lock over buses that already exist.
Finding: Rival confirmed (verified at every layer by 3 independent auditors + 3 adversarial critics).
Key audit facts (file:line evidence in the plan doc):
- CardExtraEffect = 254-field flat record (78 kind-specific), 147 kinds, 115-case switch + ~25 executors; UI "unified groups" already hide ~90 kinds behind ~10 entries (proto-taxonomy, UI-only).
- Text: 33 target-switches = 16 full (fixed today) + 13 partial ("equal to" family - one site :16053 feeds ~30 kinds) + ~9 fully target-blind resource formatters whose RUNTIME honors ally/player targets; root cause = ExpandMultiplayerTargets widens every dropdown unconditionally. Some kinds' runtime ignores Target entirely (GainMaxHp/Summon/Forge/Discard/Exhaust) - dropdown lies.
- Composability: branches are ALREADY recursively persisted+synced+executed (UI-locked to helper cards only); generators publish selections the UI never offers; FetchSpecificCardToHand advertised as source but never publishes (bug); DrawCards can't consume; 37 kinds ungrantable; metrics DealDamage-only.
Design bake-off: (1) registry+wall-removal ADOPTED for runtime; (2) full primitive-graph interpreter REJECTED (single-seam claim false - trigger layer is ~25 entry points; compile cache broken by in-place self-scaling mutation; green-diff needs field provenance) - its symmetric publish/consume ideas absorbed; (3) sentence composer ADOPTED for text + step UI as the long-term editor.
Critique-mandated safety rails: golden text snapshots BEFORE any wording change; the custom-text live-number system MATCHES ON generated text (wording changes need alias/re-match pass + boot audit); composer must share the extracted amount/upgrade-diff preamble, not bypass it; MP sync gets a version handshake (refuse mismatch) before any behavior phase.
Output: CARD_ENGINE_PLAN.md (repo root) - 7 phases, each independently shippable: P0 safety rails + capability registry, P1 sentence composer (kills "the target" everywhere), P2 selection bus ("create X then act on X"), P3 inline branch payloads, P4 grant+target routing, P5 value bus (honest scope), P6 step-block UI.
Next Step: user picks a phase to green-light; P0+P1 recommended first.

## 2026-07-17 - UI feedback round: prefix flood reverted, hint cleanup, ally/player target text

User verdict on the deployed #15 UI: WORSE - the "Category / Name" prefix flooded every kind dropdown (truncated in the fixed-width select, redundant under the browser's section headers), hints showed baked numbers ("Summon 5") for configurable amounts, one hint leaked a raw res:// image path, and card text does not react to targeting changes (ally/enemy/player) for most effects.
Hypothesis: prefixes belong ONLY in the browser (as section headers); hints must show placeholders; the target-text failure is formatters missing AnyAlly/AllAllies/AnyPlayer branches (falling into the generic "_" default).
Finding: True on all counts.
Fixes:
- DROPDOWNS RESTORED TO CLASSIC: DefinitionDisplayLabel no longer prefixes the category - every kind select shows the plain label in the original alphabetical order. Categories now exist ONLY inside the Browse overlay as section headers (browser items are plain names, no more "Rules & Meta / X" under a "Rules & Meta" header).
- HINTS: digits are replaced with "X" ("Summon X", "Apply X Poison.") since default amounts are placeholders; [img]...[/img] spans are dropped WHOLE (tag + inner path), fixing the "Gain res://images/..." leak on Gain Energy-style hints.
- TARGET TEXT (the real bug): AnyAlly(5)/AllAllies(6)/AnyPlayer(4) fell into the "_" default of every target switch, so changing targeting produced identical text. Added explicit branches ("An ally gains X Block.", "ALL allies heal X HP.", "Apply X Weak to any player", ...) to 16 formatter switches: DealDamage, GainBlock, Heal, LoseHp, RemoveBlock, RemoveArtifact, ApplyDebuff (x2 overloads), SignedPower (x2 overloads - all stat gains/losses), GainPower, RemovePower, CleansePowers, ApplyPower (plain + duration), MultiplyStat. EventTarget intentionally stays on the "the target" default.
- Known remainder: the "equal to {value source}" reference-text variants (~8 switches around CardEditorExtraEffects 15640-16090) still lack ally branches - lower traffic, same pattern if reported.
Build: 0 errors / 278 warnings (baseline). NOT deployed yet.

## 2026-07-17 - Browse picker v2: self-truthing effect descriptions + Recently Used section

Hypothesis: per-effect descriptions in the browser need 140+ hand-written blurbs (drift-prone); rival: the card-text pipeline can generate them.
Finding: Rival confirmed - descriptions are generated, not authored.
What shipped (NCardEditorPopup.cs):
- DESCRIPTIONS: each browser entry now shows a one-line hint = the effect's DEFAULT-configured card text, produced by the same TryFormatLineForAudit call the startup consistency audit uses (host = the card being edited), with [tags] stripped for plain Button rendering. Self-truthing: the hint is literally what the effect prints, so it can never drift from behavior; kinds with no default text (pure rules/markers) show none. Inline hint truncated at ~90 chars, full text in the tooltip.
- SEARCH NOW MATCHES DESCRIPTIONS: typing "exhaust" finds every effect whose generated text mentions Exhaust, not just label matches.
- RECENTLY USED: session-wide list (static, max 8) of picked entries shown as a top section when the search box is empty; picking anywhere records it.
Build: 0 errors / 278 warnings (baseline). Not deployed; not in-game tested.
Next Step: in-game smoke test of the browser, then deploy the #10-#16 batch on approval.

## 2026-07-17 - Bug list #15: Add Effect overhaul v1 - categories everywhere + searchable Browse picker

Hypothesis: the "confusing AF" Add Effect experience is mostly the flat 140+ item kind dropdown; the definition model already had a Category display path (DefinitionDisplayLabel renders "Category / Label" and pickers sort by it) that was never populated, so categorization + search could ship without touching any save/hydrate/visibility logic ("0 loss").
Finding: True.
What shipped:
- CATEGORIES: CardExtraEffectDefinition.Category is now DERIVED from one central map (CardEditorExtraEffects.GetEffectKindCategory - a switch over all 147 kinds; unmapped/new kinds fall to "Other", so nothing can ship uncategorized). 13 categories: Attack, Defense, Buffs & Stats, Debuffs, Powers, Draw & Hand, Resources, Costs, Create Cards, Cards & Piles, Osty & Orbs, Scaling & Logic, Rules & Meta. Every kind dropdown now reads "Attack / Deal Damage" and clusters by category (existing sort-by-label does the grouping); loc keys effectCategory.* wrap each name.
- BROWSE PICKER: new "Browse" button next to the kind dropdown on BOTH row builders (main extra-effect rows + card-smith rows). Opens a searchable overlay (same overlay pattern as the specific-card picker: backstop, panel, live-filter search field, results count, scrollable category sections, current kind marked with ">"). The option list is taken VERBATIM from that row's dropdown (labels via GetItemText, including unified-group entries), and picking drives kindSelect.Select + EmitSignal(ItemSelected) - so the existing row-reconfiguration, save and hydration paths run completely unchanged. Overlay registered in IsDefinitionEditorBlocked + the picker mutual-exclusion guard + popup close reset.
- Progressive disclosure was already in place (per-kind row visibility switch); the discoverability gap was the picker itself.
Not in-game tested yet (build-verified only). Follow-up ideas if wanted: per-effect one-line descriptions in the browser, favorites/recent section.
Build: 0 errors / 278 warnings (baseline). Bug list COMPLETE (15/15).

## 2026-07-17 - Bug list #14: "Refresh Cards" button - rebuild live card data without restarting

Hypothesis: base/upgrade desyncs on custom cards come from stale live instances (canonical values, upgrade deltas, materialized DynamicVars) that only a restart rebuilt; the existing resync machinery just needed a user-facing trigger + full coverage.
Finding: True.
Evidence: CardEditorOverrides already had the full per-instance rebuild (RefreshCardAfterUpgradeStateChanged: downgrade to level 0 -> canonical values -> override -> re-upgrade, preserving runtime enchantments, then vanilla-parity var invalidation) and ApplyAllToExistingCards - but the sweep only ran on override SAVE, required at least one override to exist, and had no manual trigger. Player.Piles spans the run deck AND in-combat hand/draw/discard/exhaust (Player.cs:218-228), so one sweep covers combat too.
Fix:
- NEW CardEditorOverrides.RefreshAllLiveCardData(): unconditional sweep of every card in every pile of every player - per-instance rebuild + CardEditorExtraEffects.RefreshCardVisuals (now internal) so table nodes redraw; per-card try/catch so one broken instance can't abort the sweep; returns the rebuilt count (verbose-logged).
- NEW "Refresh Cards" button in the editor's bottom action row (next to Status Editor), tooltip explains scope. It follows the SAME multiplayer shared-state lock as Apply/Reset (greyed with reason) - a one-sided rebuild could desync per-action checksums.
Build: 0 errors / 278 warnings (baseline). Next Step: task #15 (Add Effect UI overhaul).

## 2026-07-17 - Bug list #13: literal numbers in custom text - {{=50}} opt-out for the auto number-link

Hypothesis: the custom-text live-number sync auto-links every visible number positionally, so a description constant ("Reduces damage by 50%") gets bound to an unrelated effect value (1-stack buff -> "by 1%"), and there is no way to opt out.
Finding: True - implemented the opt-out.
Evidence: the sync tokenizes raw numbers into {{n1}}/{{l2n1}} live tokens at seed time (ReplaceVisibleNumbersWithTokens) and positionally fills leftover raw numbers at render time (ApplyRenderedNumberTokens) - no literal escape existed; the user's workaround was a dummy "Damage Rule Modifier" effect just to have a value to reference.
Fix (CardEditorDescriptionNumberHighlighter.cs):
- NEW literal token {{=50}}: renders as "50", never auto-links, never consumes a positional slot (resolved in TryResolveSemanticLiveNumberToken; IsSemanticLiveNumberToken deliberately stays false for '=' so seed/counting passes ignore it). Diff-safe: it resolves before the upgrade-preview diff, so an unchanged literal never greens and a changed one greens like any word.
- HARDENING: ApplyRenderedNumberTokens now skips {{...}} blobs entirely - the positional pass could previously consume/replace digits INSIDE an unresolved live token (e.g. a stale {{l2n1}}), corrupting it.
- Help text (editor tooltip) documents the syntax alongside {{n1}}/[[green]].
Recipe: write "Reduces incoming attack damage by {{=50}}%." - the 50 stays 50 forever.
Build: 0 errors / 278 warnings (baseline). Next Step: task #14.

## 2026-07-17 - Bug list #12: upgrade-diff green highlighting + custom upgrade text - ALL FOUR SUB-ASKS ALREADY SHIPPED (verification)

Hypothesis: the three reported highlight defects (single-line shows no green; a prepended upgrade line turns all following lines green; whole lines green instead of changed words) plus the requested manual-control field all need new work.
Finding: False - every sub-ask is already implemented in the current tree (fixed in an earlier session; the report predates the fixes). Verified end-to-end this pass.
Evidence (CardEditorDescriptionNumberHighlighter.cs):
- Single line: HighlightChangedNumbers routes single-line pairs through HighlightChangedWordsInLine (:801-804; comment cites this exact bug - the old path compared only numeric tokens).
- Prepended line (Innate): HighlightChangedNumbersByLine pairs each upgraded line with the most-similar UNUSED base line (exact visible-key, then >0.5 word overlap) instead of by index (:857-931; comment cites the shift-everything-green bug); unmatched new lines green whole, like vanilla {IfUpgraded}.
- Word-level: HighlightChangedWordsInLine does an LCS word diff and greens only changed tokens; bracketed color spans ([gold]X[/gold]) diff as one token so tags never split (:933-987).
- Manual control: [[green]]text[[/green]] renders green ONLY in upgrade previews and disappears in play (ResolvePreviewOnlyHighlightMarkers :810) - the "custom upgrade text" ask is served by the existing upgraded-text fields (created cards: CustomTextUpgraded + enable flag; edited vanilla cards: Upgrade.ModifiedBaseText) combined with these markers; both pipelines diff through the same fixed highlighter (CreatedCardsTextPatches:284, VanillaDescriptionOverrideSupport:118).
Reason: report compiled against an older build.
Next Step: none needed; recipe documented above.

## 2026-07-17 - Bug list #11: multi-hit vs Phantasmal Gardener mid-flurry block - per-repeat AfterAttack leak (Osty attacks)

Hypothesis: the mod's multi-hit attacks run as N separate AttackCommands, so once-per-attack reactions (Skittish) fire between hits - vanilla resolves the whole flurry as ONE attack, then reacts.
Finding: Partially true - the DealDamage paths were ALREADY vanilla-parity (report predates that work); the surviving leak was Osty attack repeats (plus this documents the global rule).
Evidence:
- Vanilla mechanic: SkittishPower (Phantasmal Gardener) gains block in AfterAttack (SkittishPower.cs:56) - a hook AttackCommand.Execute fires ONCE after its whole hit loop (AttackCommand.cs:549-551 BeforeAttack -> hit loop -> AfterAttack). Vanilla multi-hit = one command with WithHitCount(N) (PhantasmalGardener's own Flail uses it), so mid-flurry block gain via Skittish is impossible in vanilla.
- Mod DealDamage already matches: single row -> WithHitCount (CardEditorExtraEffects.cs:28159 + comment), multi-row plays share one AttackContext (:28150), dynamic result-repeats + OtherEnemies + per-target-conditional all group hits under an AttackContextLease (:27647/:27724/:27801) so Before/AfterAttack fire once.
- The LEAK: the generic repeat loop (:28231) wraps the whole kind switch, and OstyAction Attack/AttackAll built a FULL AttackCommand per repeat (:28751/:28756) - each Execute fires its own BeforeAttack/AfterAttack, letting Skittish block interpose after hit 1 and eat hits 2..N (and double-firing any per-attack latched buffs).
Fix: Osty attacks are now hoisted out of the repeat loop into ONE AttackCommand with WithHitCount(repeats) (mirrors the DealDamage pre-loop case; non-attack Osty actions Heal/Kill keep per-repeat semantics). Bonus parity: Hook.ModifyAttackHitCount now applies to modded Osty flurries like vanilla.
Build: 0 errors / 278 warnings (baseline; two new CS8604 silenced with owner.Osty! - non-null guaranteed by Osty.CheckMissingWithAnim).
Next Step: task #12 (upgrade-diff highlights + custom upgrade text).

## 2026-07-17 - Bug list #10: "choose a card in hand at turn start - chosen card never returns to hand" - stale ExecutionFinished attach

Hypothesis: the turn-start hand selection strands the chosen card in the selected-card strip because the release is tied to an event that never fires.
Finding: True - root-caused to the scheduler path.
Evidence (chain, all verified in game source + mod):
- Vanilla NPlayerHand.SelectCards lifts picked card nodes into the selected strip; AfterCardsSelected(source) releases them back to the hand fan EITHER immediately (source==null) OR on source.ExecutionFinished (NPlayerHand.cs:700-707, OnSelectModeSourceFinished :919). Every vanilla caller passes the RESOLVING card/power as source, so the event always fires at play end.
- The mod's single choke point (SelectCardsFromCandidates -> ResolveHandSelectionUiSource, CardEditorExtraEffects.cs:30032) already forced source=null for power effects and non-OnPlay triggers - but TIMED rows (Timing = "Start of your turn" etc.) keep their base Trigger==OnPlay and are not power effects, so the guard passed sourceCard through.
- Timed rows execute via CardEditorExtraEffectScheduler.ExecuteScheduledEffect (scheduler runs from Hook.AfterPlayerTurnStart, i.e. exactly "start of turn, after drawing") with a synthetic CardPlay whose source is the card instance that finished playing LAST turn (or a pile-less snapshot clone). Its ExecutionFinished never fires again -> the chosen card's NODE stays in the strip forever. The MODEL is fine (keyword/transform did apply), which matches the report: effect resolves, card just never comes back down.
- Differentials all explained: mid-turn OnPlay works (source genuinely resolving, event fires); non-hand piles work (grid selector, no node lift); turn-start power/boundary triggers work (IsPowerEffect -> source already null).
Fix: ResolveHandSelectionUiSource now attaches the selection to the source only when the source card is genuinely mid-play RIGHT NOW (sourceCard.Pile?.Type == PileType.Play - the same "currently playing" signal the transform deferral already uses at :9689); otherwise source=null -> vanilla's immediate-release path. Release-early is always safe; attach-to-dead-source is the strand. The mod's vanilla-replica card patches (Acrobatics/Survivor/DaggerThrow in CardEditorTargetedDiscardPatches.cs) pass the truly-resolving card and discard the selection - untouched.
Build: 0 errors / 278 warnings (baseline). NOT in the deploy from earlier today (bak-17e deploy covered #3-#9 only) - needs a fresh deploy to ship.
Next Step: task #11 (multi-hit vs mid-hit block, Phantasmal Gardener).

## 2026-07-17 - Bug list #9: targeted cost reduction (recipe) / lose keywords (implemented) / stars-spent trigger (already exists)

Hypothesis: three separate asks - (a) "reduce a specific card's Energy cost directly" needs a new feature, (b) "make cards LOSE keywords" needs a new feature, (c) "add 'stars spent' to the whenever conditions" needs a new count event.
Finding: Partially true - only (b) was a real gap. (a) already exists via the Grant tickbox (undiscoverable -> more task #15 evidence); (c) already exists end-to-end (the report predates its addition).
(c) Stars Spent: CardExtraEffectCountEvent.StarsSpent=52 is in BOTH whenever/trigger dropdown lists (PowerTriggerCountEvents CardEditorExtraEffects.cs:1710, cardSmith list :2988), labeled "Stars Spent" (:3294), window support (:3543), trigger handling (:3703), history counting (:34579), captured per play from CardPlay.Resources.StarsSpent (:24475). Nothing to add.
(a) Targeted cost reduction RECIPE: Card Cost Modifier (CardCostsLess) is grantable (SupportsGrantToCard :5421 does not exclude it). Add a "Card Cost Modifier" row, set reduction + duration, tick "Grant" ("granted to another card instead of resolving immediately"), then pick targets with the selection controls: Choose 1 in hand for a chosen card; Match: Card Id + the id/pick field for one exact card; All + "future matching cards" for an aura on every copy. Granted rows stack (task #3) and the printed cost refreshes live (task #6).
(b) Lose keywords IMPLEMENTED: "Remove instead" tickbox on the Grant Keyword action (GrantKeywordToPile). New CardExtraEffect.GrantedKeywordRemove; executor branch calls vanilla CardCmd.RemoveKeyword (CardCmd.cs:685 -> CardModel.RemoveKeyword:1337, which edits LocalKeywords - seeded from canonical keywords, so it strips BASE printed keywords too, exactly like vanilla "loses X" effects, plus mod-granted local keywords). Card text renders "loses {keyword}" variants (cardText.removeKeyword.* keys, no duration suffix). Round-trips through the preset DTO (CardEditorPresetStore) and override JSON automatically (CardExtraEffect serializes directly; old saves default false). Both save builders and both effect comparers (upgrade pairing :12515, effect matching :39083) distinguish grant vs remove. Limits: removal lasts the rest of the combat (grant-duration dropdown intentionally ignored); cannot strip GLOBAL power-granted keywords (e.g. Hex's Ethereal) - same as vanilla; the future-matching aura stays grant-only (remove mode falls back to immediate removal).
Build: 0 errors / 278 warnings (baseline). NOT deployed (undeployed batch now spans tasks #3-#9). Next Step: task #10.

## 2026-07-17 - Bug list #8: generation upgrades - rarity + keywords already exist (recipes); random transforms now arrive upgraded (implemented)

Hypothesis: all three generation asks need new features.
Finding: Mixed - two of three already exist, one was a real gap (now fixed).
(1) "Generate 2 Common cards" - EXISTS: the generation effect's rarity filter (CardExtraEffect.CardSelectionRarity, CardExtraEffectCardRarityFilter enum) is fully wired: UI dropdowns (NCardEditorPopup 13657/15740/19994), DTO round-trip, and candidate filtering (PassesRarityFilter at CardEditorExtraEffects 37853-37855 in the generation pool + 32143 in the creates-cards matchers). Recipe: Card Generation -> set the rarity filter dropdown to Common, Amount 2.
(2) Keywords (Exhaust/Ethereal) on generated cards - EXISTS via chaining: every generation executor publishes its generated cards as the row's selection (ReplaceCurrentSelectedCards at 31016/31242/31420/31460/36901/36973/37273/37421/37498). Recipe: Row 1 = Card Generation; Row 2 = Grant Keyword (Exhaust), Selection = Selected By Effect, Source = Row 1; Row 3 = same for Ethereal. Works for any post-processing of generated cards (cost mods, extra effects), not just keywords.
(3) Random transforms arriving upgraded - REAL GAP, implemented: the save path hard-forced SpecificCardUpgradeMode=MatchSource unless TransformMode==SpecificCard (NCardEditorPopup 33135/34605), the upgrade dropdown was hidden for random mode (it lives inside the specific-card row), and the executor never applied a mode (vanilla CardCmd.Transform rolls the replacement internally, so pre-roll application is impossible). Fix: (a) save gates honor the dropdown for ALL TransformCards rows; (b) the specific-card row now shows for random transforms with the id-entry controls hidden (new ExtraEffectRow.SpecificCardPickButton tracks the pick button; row label becomes "Transformed Card"); (c) TransformCardsWithCurrentPlayDeferral takes the effect and applies ApplySpecificCardUpgradeMode(replacement, original, effect) POST-ROLL on each vanilla result - gated to the explicit Upgraded mode only, so stored MatchSource rows keep their legacy no-op behavior (no surprise upgrades for existing configs). Known limit: deferred self-transforms (a card randomly transforming ITSELF mid-play) skip the upgrade (rare; documented).
Builds clean, 0 errors. Next Step: task #9.

## 2026-07-17 - Bug list #7: composite action chains (play+exhaust, discard+play) - already fully supported via Selected By Effect

Hypothesis: chaining two actions on the same chosen card needs a new composite-effect primitive.
Finding: False - the chaining primitive exists end to end and both requested combos are constructible today. Mechanism: any pile-action row can set Selection = SelectedByEffect (CardExtraEffectCardSelectionMode.SelectedByEffect, offered in the UI mode list at NCardEditorPopup.cs:22723-22732) plus a source-effect picker (CardSelectionSourceRow, shown when that mode is selected, 26029-26052); at execution, GetCandidatesFromConfiguredPile routes to GetCandidatesFromSelectedEffectSource which reads the source row's selection from CardEditorEffectExecutionAmountContext (per-play AsyncLocal); EVERY selection mode reports its picked cards (ReportSelectedCards), and rows execute sequentially in row order - so row B acts on exactly the card(s) row A picked, after row A finished. PlayCardFromPile even has dedicated selected-card replay controls (25113-25116).
WORKING RECIPES (relay to users):
- "Play a card from your hand, then Exhaust it": Row 1 = Card Action -> Play From Pile (Hand, Choose/Random, 1). Row 2 = Card Action -> Exhaust, Selection = Selected By Effect, Source = Row 1. (Alternative: single row Play From Pile + Result Pile Override -> Exhaust, variant 11.)
- "Discard and play a card from your hand": Row 1 = Card Action -> Discard (Hand, Choose 1). Row 2 = Card Action -> Play From Pile, Selection = Selected By Effect, Source = Row 1. The discard is a real discard (history entry, discard triggers) and the same instance then plays from wherever it landed.
No code change shipped: nothing was broken or missing. Like #6, this is a discoverability failure - the chaining mode is one unlabeled option inside a generic selection dropdown; folded into task #15's requirements (the Add Effect redesign should surface "act on the card from step N" as a first-class concept).
Next Step: task #8 (generation upgrades).

## 2026-07-17 - Bug list #6: Sneaky Strike / Eviscerate - machinery already existed; fixed the missing live cost refresh

Hypothesis: "cards discarded this turn" needs a new count event + condition plumbing.
Finding: False - everything already exists end to end. CardExtraEffectCountEvent.Discarded (=2) counts CardDiscardedEntry per owner with window support (ThisTurn/ThisCombat/LastTurns); ScaleMode has ConditionOnly ("Only If Count") and PerHistoryCount; the effect UI offers the FULL event enum + all 3 modes + comparisons; SupportsHistoryScaling allows CardCostsLess and GainEnergy; and GetCardCostsLessAdjustment accumulates the card's OWN override rows and applies history scaling + count conditions live inside the cost hook.
WORKING RECIPES (relay to users):
- Sneaky Strike ("if you discarded a card this turn, gain 2 Energy"): Gain Energy 2, Trigger On Play, scaling ON: Mode = Only If Count, Event = Discarded, Window = This Turn, Comparison = At Least, Amount = 1.
- Eviscerate ("costs 1 less for each card discarded this turn"): add a Card Cost Modifier (CardCostsLess, Reduce 1) row on the card itself, scaling ON: Mode = Per Count, Event = Discarded, Window = This Turn.
The REAL defect: nothing refreshed the hand UI when a counted event changed a scaled cost - GetCardCostsLessAdjustment evaluates live, but the printed cost stayed frozen until an unrelated redraw, making a correctly-configured Eviscerate look broken (very likely why the user judged it "impossible").
Fix: new CardEditorScaledCostRefreshPatches - postfixes on CombatHistory.CardDiscarded/CardDrawn/CardExhausted (the single chokepoints that write the counted entries) refresh the event-owner's hand cards that carry a history-scaled CardCostsLess/CardStarCostsLess row (InvokeEnergyCostChanged + NCard.UpdateVisuals; failure-isolated).
Discoverability note: the scaling/conditions UI being invisible to users is the core evidence for task #15's redesign (conditions live behind a generic "scaling" tickbox with jargon labels).
Builds clean, 0 errors. Next Step: task #7 (composite action chains).

## 2026-07-17 - Bug list #5: Knowledge Demon's Disintegration ignores edits - encounter post-creation var writes clobbered overrides

Hypothesis: boss-owned card copies bypass the override chokepoint entirely.
Finding: False on the chokepoint, True on the outcome. KnowledgeDemon creates its Curse-of-Knowledge offers via CombatState.CreateCard(canonical, target.Player) -> canonical.ToMutable() -> the mod's CardModel_ToMutable_Patch postfix DOES apply the stored override (owner-agnostic). The edit is lost one line later: the demon hard-sets cardModel.DynamicVars["DisintegrationPower"].BaseValue to its escalating damage table AFTER CreateCard returns (KnowledgeDemon.cs:176-183), overwriting the user's edited value. Disintegration's whole effect IS that var (OnChosen applies DisintegrationPower with DynamicVars["DisintegrationPower"].BaseValue), so "its effect doesn't change".
Fix: CardEditorOverrides.ReassertDynamicVarBaseValues(card) (mirrors ApplyOverride's var-application block, gated on IsMutable/Suppress/stored override) + new CardEditorEncounterCardOfferPatches: prefix on CardSelectCmd.FromChooseACardScreen re-asserts each offered card's override var values right before the choose screen shows them - user's absolute edit wins over ANY encounter's post-creation var writes (boss-agnostic; covers the whole class, not just Knowledge Demon), and OnChosen executes the same instance, so the applied power uses the edited value.
Scope note: extra-effect ROW edits on Disintegration still cannot execute from OnChosen (it is not a card play - vanilla effect bodies are not replaceable); the realistic edit for this card is its damage var, which is what now works. Text/cost/keyword edits were already applying via the creation chokepoint.
Builds clean, 0 errors. Next Step: task #6 (discarded-this-turn conditions).

## 2026-07-17 - Bug list #4: custom power lifecycle - stacks trigger per-stack, buff color by target, orphaned powers pruned

Hypothesis: apply-power stacks don't reach the behavior execution; color/persistence are registry-level defects.
Finding: True on all three.
(1) STACKS ("apply 5 stacks -> triggers as 1"): custom-status behaviors live as PowerEffectEntry items on the invisible CardEditorExtraEffectPower host, and execution multiplies by entry.StackCount (runCount = stackCount, CardEditorExtraEffectPower.cs ~957) - but AddCustomStatusBehaviorEffects HARD-CODED StackCount = 1 on both create and refresh (and even reset an existing entry back to 1). Direct plays instead accumulate StackCount via MergeIntoEntry, which is why playing the source card 5x worked. Fix: AddCustomStatusBehaviorEffects takes the status's stack count; ApplyCustomStatusPower passes Amount on fresh apply and active.Amount after ModifyAmount; new SyncCustomStatusBehaviorStacks keeps entries in step on reductions (ReduceConfiguredPower, ModifyActivePowers). Behavior now fires once per stack - identical to playing the source N times.
(2) COLOR (red debuff number on a self-buff): CardEditorCustomStatusRegistry.InferPowerType classified the status by WHAT its behavior applies (PowerId=Poison -> Debuff) regardless of target. Fix: classification is now target-aware - only self/ally-facing behaviors (Self/AnyPlayer/AnyAlly/AllAllies) inherit the applied power's debuff type; enemy-facing debuff-appliers ("apply 1 Poison to enemies") classify as Buff on the holder. Explicit icon choice (StatusIconPowerId) keeps authority.
(3) DELETION PERSISTENCE ("deleted card's power still invocable"): CardEditorRunPowerState (user://card_editor/run_power_state.json) re-applies stored powers by name at every combat start for 14 days, even after the defining card/definition is gone; the stale stored definition also shadowed same-name recreations via Resolve()'s stored-first preference. Fix: new CardEditorCustomStatusRegistry.DefinitionExists; ApplyForCombat prunes+persists orphaned custom-status entries instead of resurrecting them. With the orphan gone, recreating a power under the same name resolves fresh.
Note: "buffs split into separate icons" for direct plays is the per-row PowerStackMode choice (Merge vs Separate) working as designed - Merge collapses entries/icons; documented rather than changed.
Builds clean, 0 errors. Next Step: task #5 (enemy/boss copies respect overrides).

## 2026-07-17 - Bug list #3: granted card effects now STACK amounts ("Gain 1 Thorns" twice = Gain 2)

Hypothesis: the second grant is dropped by a dedup rule instead of merging amounts.
Finding: True. CardEditorTemporaryExtraEffectController.Grant() ran IsDuplicateGrantedEffect (CardEditorExtraEffects.cs:39031) on every new grant: equivalent effect + equal Amount + same EffectId (always the case when the same source row grants again) -> "duplicate ignored", only the duration refreshed. The amount-merge pattern existed but ONLY for timed cost-reduction grants (TryStackTimedCardCostsLess).
Fix: new CardEditorExtraEffects.TryStackDuplicateGrantedEffect - matches EffectsMatchExceptAmount, refuses keyword grant PACKAGES (GetCanonicalGrantPackageKey non-empty: re-granting the same keyword stays identity/no-op), X-amounts, and non-magnitude kinds (reuses IsNonStackingGrantedEffectKind = !SupportsRepeat + toggle kinds), then folds candidate.Amount into existing.Amount (signed amounts fold correctly). Grant() tries stacking FIRST (same-duration grants only; different durations coexist as separate entries and already execute additively), falling back to the old duplicate-ignore. grant.Effect is the same instance referenced by state.Effects, so the merged amount is immediately live for execution and card text. All four Grant() call sites (aura controller, 3 ExtraEffects paths) funnel through the fixed method.
Also covers: grant 1 then grant 2 -> 3 (old rule only matched EQUAL amounts, so unequal re-grants previously created ambiguity); a third grant keeps accumulating.
Note: the OTHER half of the original report ("buffs overwrite or split into separate icons") is the custom POWER stacking path - handled in task #4 (power lifecycle) alongside apply-power amounts and buff/debuff color.
Builds clean, 0 errors. Next Step: task #4.

## 2026-07-17 - Bug list #2: Thrash can't read custom-card damage - vanilla DynamicVars parity for created cards

Hypothesis: vanilla readers see created cards as damage-less because created cards never expose vanilla DynamicVars.
Finding: True. Thrash.OnPlay (v0.109 source) reads the exhausted card's DynamicVars by key ("CalculatedDamage" -> "Damage" -> "OstyDamage"), warns and uses 0 if absent; Reap/SeekerStrike/Neutralize/Wither/WroughtInWar/UltimateStrike read the same surface. CardEditorCreatedCardBase never overrode CanonicalVars, so created cards had an empty var set - their damage lives only in DealDamage effect rows.
Fix: CardEditorExtraEffects.BuildVanillaParityVars(card) derives a vanilla DamageVar (first on-play, non-payload, non-X, non-self DealDamage row) and BlockVar (first on-play GainBlock row) from GetEffectsForDescription(card, false) - which already fuses upgrade deltas via CurrentUpgradeLevel and includes combat-granted rows. CardEditorCreatedCardBase now overrides CanonicalVars to return these. Staleness handling (CardModel.DynamicVars materializes lazily ONCE per instance and clones COPY the materialized set, source CardModel.cs:538-552/1202): CardEditorExtraEffects.InvalidateVanillaParityVars nulls the private _dynamicVars cache via reflection at three points - CardEditorCreatedCardsStore.SetOverride (canonical instance, so new run clones derive fresh), CardEditorOverrides.ApplyToExistingCardInstance (live mutable instances after edits), and CardEditorCreatedCardBase.OnUpgrade (re-derive fused values; vanilla re-materializes after OnUpgrade, and downgrade resets from canonical which is level-0 correct).
Recursion audit: the derivation chain (GetEffectsForDescription -> GetEffectiveExtraEffects at 38302 / GetActiveGrantedExtraEffects / temporary+aura controllers) contains zero .DynamicVars accesses, so materializing the set from CanonicalVars cannot re-enter DynamicVars. Builds clean, 0 errors.
Not covered (noted): OstyDamage var for created cards with Osty-attack rows (rare; add OstyDamageVar the same way if reported), and vanilla cards whose OVERRIDE adds damage rows to a non-attack card (would need a CardModel-level patch rather than CanonicalVars - revisit with task #5's instantiation chokepoint).
Next Step: deploy with the next batch; task #3 (grant/buff stacking).

## 2026-07-17 - Bug list #1: "editing a card wipes its custom keywords" - root-caused, fixed, adversarially verified

Hypothesis: a contained store bug replaces the keyword list on save.
Finding: Partially true - worse than a store bug. Custom keywords have NO dedicated store: a keyword grant is CardExtraEffect entries with CustomKeywordName inside CardOverride.ExtraEffects, and the editor rebuilds the ENTIRE override from UI rows on every Apply (BuildOverrideFromUi -> CardEditorOverrides.Set = full replace). Any stored effect that fails to round-trip the row UI is permanently deleted by an Apply that touched only name/art/rarity.
Root cause (3-agent trace): validity-rule asymmetry. Save-side IsValidExtraEffectAmountForSave has an escape hatch persisting CreatedCardsCostLess Free/HalfCost/FreeToPlay rows with Amount=0 (canonical keyword-maker behavior rows); the card RENDERER accepts them (IsRenderableCreatedCardsCostLess); but the popup's LOAD filter used raw IsValidEffectAmount (rejects 0) so those effects never became rows -> next Apply deleted them. Secondary vectors: KeywordGroupField null-out when the widget is freed, batch apply overwriting every card with one card's state, preset-load filter, upgrade rebase drops, stale cached popups.
Fix round 1: unified predicate CardEditorExtraEffects.IsPersistableEffect (AmountIsX + valid amounts + cost-modifier escape) applied at popup hydration (both duplicated paths), preset ToOverride, RebaseUpgradeEffectsAfterBaseEdit; preservation stash (_unrepresentedBaseEffects/_unrepresentedUpgradeEffects) re-appending effects the UI cannot represent; HydratedCustomKeywordName fallback; batch per-target preserve; row-cache round-trip of stash state; stale-cache keyword-group guard (_hydrationSeenKeywordGroups).
Adversarial verification (4 lenses: deletion/duplication/alignment/lifecycle) found 9 real issues in round 1 - 3 critical: (1) deleting a keyword ADDED through the same kept-alive popup instance got resurrected by the stale-guard (seen-set only populated at hydration; Apply keeps the popup as the fresh cache entry with no rehydration) and became undeletable; (2) the definition-behavior editor reuses BuildOverrideFromUi with definition rows swapped in - the card's stash/guard effects leaked INTO keyword/status definitions (and an emptied behavior list could never save empty); (3) tail-appending the stash REORDERED the base list in preview drafts while the attached upgrade list stayed unrebased - OnApplyPressed feeds the draft to RebaseUpgradeEffectsAfterBaseEdit whose POSITIONAL pairing then swaps upgrade slot deltas onto wrong base effects (deterministic corruption). Majors: main-build upgrade no-base hydration branch left unconverted; AlignUpgradeEffectsForEditor still strict-filtered absolutes before the stash could see them; upgrade builder's stash append unreachable with zero rows; batch double-preserve (template stash + per-target merge, compounding); minors: stash resurrection after external ReplaceAll; half-visible keyword groups undeletable.
Fix round 2 (all 9): stash entries carry StoredIndex and are REINSERTED at original positions (order-stable => upgrade slot pairing safe); seen-set unioned with every applied keyword group at the end of base Apply; _suppressPreservedBaseEffects gate (definition-behavior builds + batch template build); IsPersistableEffect + stash in the missed upgrade branch and AlignUpgradeEffectsForEditor; upgrade builder no-rows else-branch; batch EffectId dedup; stash entries skipped when externally deleted (EffectId no longer in stored) or when their keyword group had visible rows at hydration and the user deleted them all.
Builds clean, 0 errors. Custom keywords + keyword maker fully preserved; deliberate deletion honored on every surface.
Next Step: deploy on user confirmation; then task #2 (vanilla DynamicVars parity).

## 2026-07-17 - Game hotfixed to v0.109.0 overnight; mod init crashed (TypeLoadException) - rebuilt

Hypothesis: "Card editor isn't loading at all" = my 00:14 MP-hardening build broke something.
Finding: False - Steam pushed beta v0.109.0 (commit c12f634d, built 02:31Z, installed 05:46; the user's fresh source drop IS v0.109.0). The mod (built vs v0.108.0) died at init: TypeLoadException for MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache at JIT of CardEditorMod.RegisterCreatedCardsInPools - the method-level try/catch cannot catch a JIT-time type-load failure, so Init aborted before ANY Harmony patching (main menu loads, zero editor).
Evidence: godot.log L22-24 (exception), release_info.json v0.109.0, ModelIdSerializationCache line now has "Properties: 50" (new property-name net-id table).
v0.108->v0.109 API changes fixed: (1) SavedPropertiesTypeCache deleted - ModelIdSerializationCache.Init now scans every ModelDb type (mod models included) and caches [SavedProperty] members + property-name net ids itself, so the two InjectTypeIntoCache calls were simply removed (pool registration suffices). (2) Hook.ModifyCardPlayResultPileTypeAndPosition -> Hook.ModifyCardPlayResultLocation returning the new CardLocation record struct (player, pileType, position); patch retargeted, preserves __result.player. (3) CardPlay gained `required Player Player` - inserted `Player = <card>.Owner` into all 27 synthetic CardPlay initializers via scripted transform. (4) CreatureCmd.LoseBlock now (choiceContext, target, amount, remover). (5) The four Hook.ModifyHpLost* statics folded into Hook.ModifyHpLost(..., HpLossHookPhase phases, out modifiers) - the IgnoreDamageNegation patch (skips the AfterOstyLate negation pass, e.g. Buffer) rewritten against the consolidated hook (guards on phases.HasFlag(AfterOsty), mirrors vanilla incl. decimal.Truncate change-detection, still skips AfterOstyLate); manual EnsurePatched registration in CardEditorMod.EnsureIgnoreDamagePatches updated; IterateHookListenersCompat widened to ICombatState?.
Verified still present in v0.109.0 source (string-based targets that fail silently): Hook.ModifyDamageInternal (cardPlay shape unchanged), NCardTransformShineVfx.PlayAnimation + _cardNode/_endCard, ChecksumTracker.CompareChecksums, RunManager.InitializeShared, AttackCommand.ModelSource auto-property, NCardPlayQueue._playQueue, NoDrawPower.AfterSideTurnEnd. Builds clean: 0 errors.
Next Step: deploy (both players again), then the user's fix list.

## 2026-07-17 - MP "ready not registering" = the mod's own ready-gate deadlocking (first live run of L1); hardened to fail-open

Hypothesis: The joiner's eaten Ready click is the game's fault (join gates / host DLL).
Finding: False - it is the mod's L1 client ready-gate (CardEditorMultiplayerSync.AllowClientReady) deadlocking on its first-ever field run. Both players were on v0.108.0 + matching mod (join succeeded, host hash 693432997 = modded), user's multiplayer_settings.json has MultiplayerSyncEnabled=true -> the gate armed and held every SetReady(true), and the fail-open never fired.
Evidence (joiner logs, session 23:46 2026-07-16): zero CardEditorMultiplayerSnapshotMessage receive lines across 6 join attempts (host snapshot never arrived), zero "Readying WITHOUT a confirmed card-editor sync" timeout warns (the FirePendingReadyIfNeeded pump never fired -> runner node _Process dead or Update() early-return), and every "Local player ... is ready" line is explainable as SetReady(false) passes (vanilla logs the same "is ready" text for BOTH values - confirmed in decompiled v0.108.0 StartRunLobby.SetReady). Host therefore only ever received value=False -> checkmark never appeared -> "waiting for players to ready" -> host quits ("Disconnected from host, reason: Quit" x6). Old 7.6MB v0.107.1 log has zero sync traffic despite working MP -> the gate existed in the 22.06 DLL but was inert there; tonight's rebuild was its first armed run. UI aggravator: NCharacterSelectScreen.OnEmbarkPressed switches the joiner's own screen to "Ready and waiting" even when the prefix blocks SetReady, so the joiner looks ready to themselves while the lobby never got it.
Fix applied (CardEditorMultiplayerSync.cs, builds clean): (1) Update() now calls FirePendingReadyIfNeeded() BEFORE the IsConnected early-return; (2) AllowClientReady only holds when the runner is verifiably alive (IsInsideTree), else passes through with a warn; (3) EnsureRunner treats a valid-but-orphaned runner as missing and retries via CallDeferred(AddChild) with a NotifyRunnerEnteredTree/_runnerAddQueued dedupe (BindToNetService can run mid scene setup where sync AddChild is rejected); (4) ReadyGateTimeoutSeconds 8s -> 3s; (5) hold/release/timeout are now Log.Info/Warn (previously VerboseLog-only, and VerboseLogging=false in card_editor_settings.txt hid everything).
Source-verified (fresh v0.108.0 decompiled source drop, 2026-07-17): every game-side link is sound, pinning the dead pump as the SOLE root cause. (1) OneTimeInitialization.ExecuteVeryEarly loads mods BEFORE ExecuteEssential:84 runs MessageTypes.Initialize(), which appends ReflectionHelper.GetSubtypesInMods<INetMessage>() - the mod's 3 sync messages get valid wire ids, and NetTypeCache assigns ids by NAME-SORTED order (deterministic; identical for identically-modded peers; also explains why modded<->vanilla is impossible: mod names interleave and shift vanilla ids). (2) NetMessageBus.TryDeserializeMessage drops unknown ids with a one-time "outside the bounds of our known messages" warn - only fires on mismatched mod sets. (3) NetHostGameService.SendMessage(msg, peerId) (the snapshot reply path) does NOT check readyForBroadcasting - only the broadcast overload does. (4) Bus buffering (ShouldBuffer) is only active during the lobby->run transition (StartRunLobby.BeginRunForAllPlayers:490 on, RunManager.Launch:684 off), not in the idle lobby. (5) v0.108.0 NCharacterSelectScreen.OnEmbarkPressed:394-404 switches the local UI to "Ready and waiting" even when the SetReady prefix blocks the call - the joiner LOOKS ready to themselves while the lobby never got it. So: dead runner _Process => no snapshot request sent (Update() sends it) AND no gate release/timeout (same Update()) => host only ever received False. Both symptoms, one cause.
Next Step: deploy to BOTH players; verify in joiner log: "Holding lobby ready ... (fails open after 3s)" then either "Host snapshot applied; sending the held lobby ready" (host sync on) or the WITHOUT-confirmed warn (host sync off/blocked -> then check host's Multiplayer Sync toggle + host log for the request/response). Note: OnSyncRequestReceived answers only when the HOST's MultiplayerSyncEnabled=true.

## 2026-07-16 - Beta v0.108.0 broke the mod (API changes); fixed + rebuilt. MP blocked by compat gates

Hypothesis: The combat freeze/error spam and dead intents after tonight's Steam beta update come from the mod DLL (built 22.06 vs v0.107.1) referencing APIs that changed in v0.108.0; the multiplayer failure is the game's join gates, not the lobby ready code.
Finding: True (combat/mod). Partially true / Unconfirmed (multiplayer — no post-update MP log exists on this machine; all rotated logs incl. the 7.6MB co-op session are v0.107.1 where join+ready+resume all worked and the session ended in StateDivergence at 17:53).
Evidence: %APPDATA%\SlayTheSpire2\logs\godot.log (v0.108.0 modded session): 9 Harmony patch classes skipped at load + repeating `MissingMethodException: AbstractModel.ModifyDamageCap(Creature, ValueProp, Creature, CardModel)` thrown from Hook_ModifyDamageInternal_IgnoreCaps_Patch.Prefix via MonoMod JIT hook whenever NIntent.UpdateVisuals/UpdateDynamicVarPreview ran (= missing enemy intents + error spam in combat). Decompiled the new sts2.dll (9,571,328 bytes, 2026-07-16 21:58) with .tools\ilspycmd:
- ModifyDamage*/ModifyBlock*/Hook.ModifyDamage(Internal) gained `CardPlay? cardPlay`; CreatureCmd.Damage overloads likewise.
- Hook.BeforeTurnEnd/AfterTurnEnd renamed to BeforeSideTurnEnd/AfterSideTurnEnd (ICombatState param); model-level AfterTurnEnd → AfterSideTurnEnd(PlayerChoiceContext, CombatSide, IEnumerable<Creature>).
- NCardTransformVfx.PlayAnimOnCardInHand removed; on-card transform anim now = NCardTransformShineVfx (fields _cardNode/_endCard, method PlayAnimation).
- AttackCommand.CreateContextAsync now takes CardPlay (not CardModel); FromCard/FromOsty take CardPlay?; PotionFactory.GetPotionOptions(Player) lost blacklist; CardCreationOptions ctor now (IEnumerable<CardPoolModel>, source, odds, Func<CardModel,bool>? filter) — candidate-list ctor gone; ITemporaryPower.IgnoreNextInstance removed (wrappers now re-apply internal power in BeforeApplied/AfterPowerAmountChanged).
Fix applied (builds clean, 0 errors, warnings only pre-existing): cardPlay plumbed through Hook_ModifyDamageInternal_IgnoreCaps_Patch + preview Hook.ModifyDamage calls + all CreatureCmd.Damage/FromCard/FromOsty/CreateContextAsync call sites; 6 hook-patch attributes renamed to *SideTurnEnd; CreatureCmd.Damage patch type-arrays extended with CardPlay; transform-interop patch retargeted to NCardTransformShineVfx.PlayAnimation (Prepare-guarded, reads _cardNode/_endCard); CardEditorMarkedPower override updated; RewardPools filteredOptions now uses pools+Id-set predicate; GetPotionOptions call fixed; NEW CardEditorTemporaryPowerCompat.cs recreates IgnoreNextInstance as a ConditionalWeakTable flag consumed by Prepare-guarded prefixes on {TemporaryStrength,TemporaryDexterity,TemporaryFocus}Power.{BeforeApplied,AfterPowerAmountChanged}; all 7 IgnoreNextInstance call sites swapped to it. Duration patches were already dual-target (AfterSideTurnEnd primary + guarded legacy) — no change needed.
Reason (MP): v0.108.0 JoinFlow (decompiled) hard-refuses on (1) version mismatch, (2) gameplay-relevant mod list mismatch (card_editor.json declares affects_gameplay:true), (3) ModelIdSerializationCache hash mismatch (modded 0.108.0 = 2175 entries/hash 693432997 vs vanilla 1648/1978543599). So both players must run the same game version AND the identical mod build (or both vanilla) — a friend on stable v0.107.1 or without the updated mod is rejected before the lobby, which players see as "can't ready up". The mod's own ready-gate (holds SetReady until the host snapshot syncs, 8s timeout, StartRunLobby/LoadRunLobby prefixes) compiled clean vs 0.108.0 and none of its patches were skipped; the v0.107.1 log shows it fired correctly all session.
Next Step: deploy the rebuilt card_editor.dll (+pdb) into Steam mods\Card_editor (after backing up the old one), relaunch, confirm 0 incompatible-patch warnings and no MissingMethodException in combat; then retest co-op with BOTH players on beta v0.108.0 + this exact DLL, and if ready-up still fails grab %APPDATA%\SlayTheSpire2\logs\godot.log right after (look for [JoinFlow] VersionMismatch/ModMismatch/hash lines or "[CardEditor][MultiplayerSync] Holding lobby ready").

## 2026-06-16 - Relic effects feasibility (deep dive) + plan

Hypothesis: Porting all card effects onto relics is feasible by reusing the existing effect/trigger infra.
Finding: True, high reuse (~80-90%). The effect engine needs ZERO changes; relics become a new host.
Evidence (6-agent deep dive): StS2 relics are AbstractModel subclasses whose behavior is overridden virtual Hook methods (Core/Models/RelicModel.cs:22/:388 ShouldReceiveCombatHooks; ~60 hooks in AbstractModel.cs:126-621) — the SAME hook bus cards/powers use. The card editor already runs effects from a non-card host: power triggers build a synthetic CardPlay + push 3 AsyncLocal contexts (CardEditorExtraEffectPower.cs:1001-1004) and call ExecuteEffect (CardEditorExtraEffects.cs:26514), which reads owner/source from those contexts. So a relic = same pattern; the one new primitive is a relic "proxy card" for the effect's required CardModel SourceCard. Current relic editor only overrides numbers/desc/pool (CardEditorRelicOverrides.cs:26-42). Two trigger kinds: reactive After*/Before* (run effects, main feature) vs Modify* (return a value -> separate passive-modifier layer). Effects bucket into host-agnostic (work as-is) / adaptable / card-only (gate with a new SupportsOnRelic()).
Decision (user, 2026-06-16): support BOTH overriding existing relics AND brand-new custom relics; expose ALL ~60 hooks.
Full plan: Notes/RELIC_EFFECTS_PLAN.md (architecture, trigger map, effect buckets, 6 components, 6 phases, design decisions, risks, evidence index).
Next Step: await go-ahead to build Phase 1 spine (proxy card + relic effect host + one reactive trigger end-to-end).

## 2026-06-14 - Completed Simplified-Chinese (zhs) localization (native-speaker bug report)

Hypothesis: The zhs loc gap is just my recent patches' labels.
Finding: Partially true — bigger. The new features' DROPDOWN labels were translated, but their CARD-TEXT strings (11 keys: cardText.currentStars/Energy/OrbSlot ScalingSuffix, condition.haveStars/starsCount/haveEnergy/energyCount/haveOrbSlots/orbSlotsCount, value.hasStarCost/hasEnergyCost) were never in ANY loc file — only code fallbacks — so a Chinese user saw English rules text. Plus a pre-existing 16-key zhs backlog (generatedPool.Status/Curse/Quest/Token/Event/AllPools, valueSource.eventActor, poolSuffix/poolDescriptor.allCardPools, powerTrigger.actor + powerTriggerFrom.markedTarget, 5 tooltip.powerTriggerFrom.*).
Evidence: python eng-vs-zhs key diff (was 16 missing + the 11 not-in-any-file). Translated all 27 via a 3-agent workflow (2 independent translators + adjudicator with back-translation + placeholder verification + glossary enforcement: 星星/能量/充能球槽位/攻击-技能-能力, 生物 for creature, 厄运 for Doom, 友方 for ally). Applied via format-preserving insertion script: zhs +27 (->2520), eng +11 (->2503, English source for the new keys so files stay consistent). Re-diff: 0 keys missing from zhs; all {Effect}/{Comparison}/{Value} placeholders verified present, no {StarDescriptor}/{Plural} leak. LOC-ONLY (no code change/rebuild).
Note: kor has the same 16+11 gap (NOT done — user asked Chinese only; offer separately). Scaling-suffix zhs bakes the noun (drops the English-carrying {Descriptor} token) so step>1 multiplier isn't shown — minor, common case perfect.
Next Step: commit+push+Steam-deploy the loc; optionally do kor next.

## 2026-06-14 - Implemented Current-resource count events + Non-Attack/Skill/Power type filters

Hypothesis: The requested scaling params + inverse type filters can be added with contained, low-risk changes.
Finding: True (builds clean, 0 errors). Count events fully done; inverse types functional + labeled + primary text done, with secondary auto-text noted as follow-up.
Evidence (count events, CardEditorExtraEffects.cs): appended CardExtraEffectCountEvent CurrentStars=49/CurrentEnergy=50/CurrentOrbSlots=51 (serialize by NAME via PresetStore string DTO, so append is safe + UI index==value preserved). Resolver GetHistoryCountMultiplier reads owner.PlayerCombatState.Stars / .Energy / .OrbQueue.Capacity (all verified real in decompiled Source/.../PlayerCombatState.cs:71,90 + OrbQueue.Capacity). Mirrored EmptyOrbSlots at: CountEventLabel (3216), CountEventUsesWindow exclusion (live reads, no turn/combat window), scaling-suffix text (17053-area), condition text (17707-area). Loc countEvent.* added to all 6 files (eng/kor/zhs × card_editor_pack + built cfiles).
Evidence (inverse types): appended CardGeneratedCardType NonAttack=8/NonSkill=9/NonPower=10. Handled in ALL 5 type-matchers (else default mis-matches — 31479's default returns card==card=always true): CardEditorExtraEffects MatchesGeneratedCardType (31479) + candidate-filter else-if (37184-area), CardEditorCardTypeCostAuras.MatchesType (418), CardEditorDrawnGeneratedCostController.MatchesType (500), CardEditorRewardPools.MatchesType (808). GeneratedCardTypeLabel + count-filter typeAdj (BuildCountCardFilter ~18686) + loc generatedType.* (6 files). Chose explicit enum members over an invert-toggle: auto-handles UI (Enum.GetValues) + serialization (by-name), works universally incl. generation, ~1/2 the sites of the toggle (no 3-bool UI/serialization/equality plumbing).
Open follow-up: secondary auto-text typeAdj sites (trigger-condition + generation descriptors, ~5 sites) still render the type qualifier as "card" not "non-Attack card" (filter WORKS, only the rules-text adjective is missing). Not yet committed/deployed.
Next Step: optionally finish secondary typeAdj text; commit+push+Steam-deploy when user asks.

## 2026-06-13 (CORRECTED) - Power-specific (non-global) created-card discount: use "Created Cards Cost Less" (created-by-this), On Play

Hypothesis (mine, first pass): "Created Cards Cost Less" (CreatedCardsCostLess) can't reach cards made by a power on the same card; must use the Global variant.
Finding: FALSE — I was wrong (Bartek correct). CreatedCardsCostLess IS source-card-scoped and DOES cover a power's generations, and it is the correct tool when you want ONLY that card/power's generated cards discounted (NOT global).
Evidence: CardEditorCreatedCardsCostPatches.cs:855 CardPileCmd_AddGeneratedCardsToCombat_CreatedCardsCostLess_Patch — prefix on the single generation chokepoint CardPileCmd.AddGeneratedCardsToCombat: (1) ResolveSourceCard() (882), (2) reads ONLY that source card's effects for CreatedCardsCostLess (894-911), (3) stamps ONLY this batch (917-950). CardEditorGeneratedCardSourceResolver: when CardEditorHookModelContext.Current is a PowerModel, resolves source via CardEditorPowerSourceMap.TryGetSourceCard(power) → the power's owning card. So a power's generated cards resolve to its card and get that card's CreatedCardsCostLess — scoped, never global. GATE: only honored when effect.Trigger == OnPlay (905); duration via CreatedCardsCostDuration (UntilPlayed → ApplyUntilPlayed). Distinct from GeneratedCardsCostLess (=58, "Generated Cards Cost Less"/"...(Global)") which is the player-wide grant.
UI location: "Created Cards Cost Less" is NOT a top-level effect — it's variant #10 in the "Card Generation" effect's variant dropdown (UnifiedCardGenerationVariant.CreatedCardsCostLess, NCardEditorPopup.cs:17014). Likely failure cause if it looked dead: Trigger not set to On Play.
Next Step: Config = Card Generation → variant "Created Cards Cost Less", Trigger On Play, Reduce 1, Until Played, Energy, on the same card as the generating power.

## 2026-06-13 (SUPERSEDED — see correction above) - Power-generated cards via "Created Cards Cost Less (Global)"

Hypothesis (Bartek's): The user is wrong that a power-generated card can't get the card's "cards created cost less" discount; it's achievable (e.g. by latching the effect onto the power).
Finding: True (achievable; user's claim of impossible is false) — but the real fix is the effect KIND, not where it's attached.
Evidence: CardEditorExtraEffects.cs — TWO kinds: CreatedCardsCostLess=27 "Created Cards Cost Less" (2253) vs GeneratedCardsCostLess=58 "Created Cards Cost Less (Global)" (2677). SupportsAsPower (5274) EXCLUDES CreatedCardsCostLess (on-play, this-card-scoped only; can't be a power) but ALLOWS GeneratedCardsCostLess. Execution: GeneratedCardsCostLess → CardEditorDrawnGeneratedCostController.Apply (28344/27330) registers a PLAYER-WIDE standing grant (no source-card filter). OnCardGenerated (controller 292) stamps the discount on ANY generated card matching pool/type — fired by Hook.AfterCardGeneratedForCombat (controller patch 545). Generation path: ChooseOneOfThreeCardsToHand (36177) → AddGeneratedChoiceToConfiguredDestinations (36052) → TryAddGeneratedCardToCombat (35967) → vanilla CardPileCmd.AddGeneratedCardsToCombat (35980) which fires that hook. Duration: UntilPlayed → grant=ThisCombat (controller 160); ThisCombat grants are NOT decremented at turn end (controller 74), so the grant persists all combat and covers the power's start-of-turn generations every turn.
Reason: The user used "Created Cards Cost Less" (this-card-scoped, immediate, non-power) so the power's separate generation isn't covered — correct symptom, wrong conclusion. The "(Global)" variant is a standing aura that ignores which card/power generated the card.
Next Step: Tell user to swap effect 1 to "Created Cards Cost Less (Global)", On play, Reduce 1, duration Until Played, pool/type matching the power's cards. (Attaching to the power also works but is unnecessary and order-sensitive.)

## 2026-06-13 - Added HasStarCost / HasEnergyCost count filters (the missing feature)

Hypothesis: A new "card has a star/energy cost" count filter can be added with a small, contained change without breaking the build or existing presets.
Finding: True (build clean, 0 errors; all sites verified by 6-agent discovery + completeness audit, no site missed).
Evidence: mods/card_editor/CardEditorExtraEffects.cs — enum CardExtraEffectCountCardFilter +HasStarCost=38/HasEnergyCost=39 (1141); CountCardFilterLabel +2 arms (3914); CountCardFilterPrefixLabel +2 arms "Star-cost"/"Energy-cost" (3949, card-text adjective); MatchesCountCardEffectFilter +2 cases returning GetCardStarCostAmount/GetCardEnergyCostAmount(card, useBaseCost:true) > 0 (35309). Loc keys added to card_editor_pack + built cfiles eng/kor/zhs (all 6 JSON re-validated). `dotnet build -c Release` = Build succeeded, 0 errors.
Reason: MatchesCountCardEffectFilter (35186) is the SINGLE predicate switch — grant (34879), branch, self-scaling, trigger, and InPile/history counters all delegate to it, so one edit covers every consumer. Default arm is `return true`, so the explicit cases were mandatory (else match-all). Used base cost (card identity, matches Crescent Spear "cards that have a star cost") via the existing defensive helpers (handle null + X-cost). Predicate-only/effect-amount switches (DoesEffectContributeToCountCardFilter→false, GetCountCardFilterDynamicAmount→0, CountCardFilterSupportsAmount excluded) all correct by default, matching the CanBePlayed precedent. UI dropdowns (7), localization warmup, and preset Enum.TryParse auto-handle new members; appended at end so index==value keeps old presets valid.
Known edge: X-cost (HasStarCostX / EnergyCost.CostsX) cards report 0 → NOT counted as having a cost (one-line change if desired). Git hunk header mislabels the matcher edit as MatchesGrantCardFilters — git xfuncname artifact from the nested local funcs; edit is verifiably inside MatchesCountCardEffectFilter.
Next Step: User to launch game and confirm in-combat the +2 applies per star-cost card in hand; tune base-vs-current or X-cost handling if desired.

## 2026-06-13 - "Star Cost Effects" count filter does NOT mean "card costs stars" (user bug report)

Hypothesis: A card "Gain 3 Block, +2 per card in hand with a Star Cost" using Count-by-Cards + filter "Star Cost Effects" stays at base 3 because of a misconfiguration the user can fix.
Finding: Mixed / conflicting evidence — the no-bonus behavior is CORRECT (not a code bug), the user's mental model is FALSE, and their goal is currently UNACHIEVABLE (missing feature, not a setting).
Evidence: CardEditorExtraEffects.cs enum CardExtraEffectCountCardFilter (1102-1142, last = CanBePlayed=37; no "HasStarCost"); label map StarCostModifier => "Star Cost Effects" (3911); predicate PassesCardMatchFilter case StarCostModifier => HasAnyExtraEffectKind(CardStarCostsLess, CardTypeStarCostsLess) (35285-35286) and effect-level twin (34524); aggregation GetCountAggregationAmount CardCount => 1 per match (34306-34314); BaseStarCost/CurrentStarCost modes sum star pips not card count (34311-34312); card.CurrentStarCost / card.BaseStarCost exist (31207, 7599).
Reason: "Star Cost Effects" matches cards whose OWN effect modifies other cards' star costs (the "Card Star Cost Changes" effect kind), NOT cards that cost stars to play. A normal star-costing card has no such effect, so the hand count is 0 and the +2 never applies. The user's "Lose 1 Star" test card uses kind LoseStars (108), also not in the filter set, so it correctly fails too. No existing filter tests star cost > 0; the only star-cost-aware path (BaseStarCost aggregation) sums pips (a 3-star card = +6, not +2), so it cannot express "flat +2 per star-cost card."
Next Step: Add filter CardExtraEffectCountCardFilter.HasStarCost (= card.CurrentStarCost > 0; optionally HasEnergyCost) — one enum value + one PassesCardMatchFilter case + label + loc. Trivial. Until then, tell the user it is a missing feature, not their settings.

## 2026-06-11 - Adversarial re-review of shared-attack-context fix wave

Hypothesis: The post-rejection fixes (inline AsyncLocal scope, powered-row count, phase revert, empty-results guards, co-op participants guards) are now fully correct.
Finding: Partially true (2 refutations, rest confirmed).
Evidence: mods/card_editor/CardEditorExtraEffects.cs (12389-12972, 26602-26811, 27087-27094, 37128-37145), CardEditorMod.cs:2768-2786, CardEditorExtraEffectPower.cs:1518-1524/1618-1635, CardEditorExtraEffectTriggerPatches.cs:318-325, CardEditorCountdownEffectPower.cs:124-140, CardEditorTempStatTrackerPowers.cs:27-43; vanilla AttackContext.cs, AttackCommand.cs, VigorPower.cs, GigantificationPower.cs, CreatureCmd.cs:174, CombatManager.cs:945/1033-1038/1065-1084, DoubleDamagePower/BurstPower.
Reason: (R1) Powered-row count uses raw e.Target while execution maps Target->Self via GetEffectiveResolvedTarget on TargetType.Self cards: 2 "Target" damage rows on a Self-target card open a powered bracketing context whose rows all run Unpowered -> Vigor latched+burned for zero benefit (regression vs old per-row Unpowered attacks). (R2) In RunAfterCardPlayed the SharedAttackContextScope spans the power-add block; an AfterAttack-trigger power granted by the same play fires once off the play's own bracketing context at scope dispose (old code fired AfterAttacks before power adds; created-card path closes the scope before powers are added).
Next Step: (1) Count powered rows with GetEffectiveResolvedTarget(cardPlay, e.Target) != Self instead of raw e.Target; (2) dispose the shared scope right after the immediate-row loop in RunAfterCardPlayed, before Fatal/power-add reactions.

## 2026-06-11 - Delta verification of R1/R2/echo/PetOwner fixes (2nd adversarial pass)

Hypothesis: The just-applied fixes (effective-target powered count, two-step inline scope close, echo damageProps gating, PetOwner rng fallback) survive adversarial re-review.
Finding: Partially true (all five items hold as implemented; two residual edge gaps, neither a regression vs the refuted code).
Evidence: mods/card_editor/CardEditorExtraEffects.cs (12389-12612 RunAfterCardPlayed, 12640-12765 RunResolvedOnPlayEffectsDuringCardPlay, 12809-12869 count+scope, 12907-13010 ExecuteDamageRowInSharedContext, 26649-26725 ExecuteDynamicResultRepeatDamage, 26741/26811/27152 gating, 26882 GrantToCard divert, 37050-37090 ResolveTargets, 37183 GetEffectiveResolvedTarget); vanilla AttackContext.cs (_disposed guard, swallowed AfterAttack exceptions), AttackCommand.cs:418, Creature.cs:159, VigorPower.cs; CardEditorCreatedCardEffectSourceSupport.cs:370.
Reason: Count and every execute path now gate powered on identical predicates (GetEffectiveResolvedTarget==Self || UsesDamageResultAmountSource); scope close is structurally leak-free and double-dispose-safe (AttackContext.DisposeAsync idempotent, sets _disposed before awaiting). Residual: (a) count is static while TryResolveExecutableEffectAmount/ResolveRepeatCount are dynamic -> a play counted at 2 powered rows whose amounts resolve to 0 at runtime still opens+disposes a powered bracketing context and burns latched Vigor for zero hits; (b) nested borrowed-source invocation for the SAME CardPlay restores the outer still-open context on inner close, so the inner RunFatalForCreatedCardOnPlayIfNeeded damage rows can join the outer (undisposed) context, contrary to the "Fatal never joins the play's context" intent.
Next Step: Optionally guard RunFatalForCardPlayNow rows from joining any ambient context (ignore ambient when executing Fatal-trigger rows), and accept the static-count Vigor edge or re-check resolved amounts before opening the context.

## 2026-06-11 - Fixability study: override-row timing + 3 accepted edge cases (8-agent design/verify pass)

Hypothesis: The deferred override-row timing item and the three accepted edge cases (zero-hit Vigor burn, Fatal joining the open shared context, discount skipping co-op extra turns) are all genuinely fixable, not permanent limitations.
Finding: True (all four designs verified feasible; 4/4 survived adversarial attack).
Evidence: Vanilla CardModel.cs:1334/1499-1636 (OnPlayWrapper awaits OnPlay at 1563 before Enchantment/Affliction/Hook.AfterCardPlayed/routing), 579 flat CardModel subclasses with ZERO base.OnPlay() call sites; created-card precedent CardEditorMod.cs:2789-2825 already composes rows onto OnPlay's hot task; CardPlayHookPhase enum already plumbed (ImmediateRowsOnly opens/closes the shared context). VigorPower.cs:57-77/GigantificationPower.cs:55-75 apply bonuses even unlatched -> lazy JIT context creation is equal-or-closer to vanilla; only 3 vanilla BeforeAttack listeners exist. Feed/TheHunt/HandOfGreed/KnockoutBlow/Sunder run kill bonuses strictly after Execute() returns -> Fatal damage = separate attack (one ~10-line down-flowing mask in RunForCardPlayTrigger, no restore needed). CombatManager.cs:531/1185/1189: AfterPlayerTurnStart DOES fire on co-op extra turns, RoundNumber does NOT increment but per-player TurnNumber DOES -> CreatedTurnNumber stamp replaces CreatedRoundNumber.
Reason: (1) "Unachievable" applied only to the hook-postfix seam - per-card OnPlay postfixes (lazy-patched per override) are the proven created-card mechanism; 3 traps found+solved (reflective vanilla-OnPlay payload re-entry needs a suppression gate; borrowed-source marker pollution; idempotency check must not be owner-based - Acrobatics/Survivor/DaggerThrow OnPlay already owned by our HarmonyId). (2-4) small/medium concrete edit lists, all verified against source.
Bonus bugs found: (a) DESTRUCTIVE co-op bug - OnAfterPlayerTurnStart owner-mismatch branch DELETES other players' pending discounts every round (CardEditorCreatedCardsCostPatches.cs:650-654/712-717, Remove instead of skip); (b) PendingStarDiscount loop (:719-739) has no same-turn guard at all.
Next Step: Implement in order: discount-extra-turn + co-op deletion fix (small), Fatal mask (small), lazy Vigor context (medium), override OnPlay postfixes (medium, with the 3 verifier adjustments).

## 2026-06-11 - "Fix the unfixable" wave implemented (override OnPlay timing + 3 edges)

Hypothesis: The four verified designs (per-card OnPlay postfixes, lazy JIT shared context, Fatal ambient mask, CreatedTurnNumber discounts) can be implemented without regressions.
Finding: Partially true on first pass (1 must-fix found), True after fix (5-reviewer pass + focused re-verify all PASS).
Evidence: 5-agent adversarial workflow over the working tree + 1 focused re-verify agent; mods/card_editor/{CardEditorOverrideOnPlayPatches.cs (new), CardEditorExtraEffects.cs, CardEditorEffectExecutionAmountContext.cs, CardEditorCreatedCardsCostPatches.cs, CardEditorMod.cs, CardEditorOverrides.cs, CardEditorUiState.cs, CardEditorCreatedCardEffectSourceSupport.cs}; vanilla CardModel.cs:1499-1637, CombatManager.cs, AttackContext/AttackCommand, VigorPower, EffectScope flush semantics.
Reason: Must-fix was cross-phase session loss - the OnPlay/AfterCardPlayed split gave each phase a fresh execution-amount session, so reaction-time amount sources (self-scaling from damage dealt, end-of-play Fatal, triggered cost-less, selected-card chaining) resolved 0. Fixed by stashing the immediate phase's ROOT session per CardPlay (ConditionalWeakTable) and re-adopting it in the reactions phase; verified effective because EffectScope.Dispose flushes frame data into session-LEVEL dictionaries which survive in the stashed Session object. Key insight for the future: RootSessionScope.Dispose only nulls the AsyncLocal - the Session object and its dictionaries survive.
Bonus fixes shipped: destructive co-op discount deletion (owner-mismatch Remove -> skip), star-only schedules never ticking (early-return condition), star same-turn guard, dead-dealer phantom-hit guards in repeat-damage helpers, safe TryGetOwner at stamp sites, Init-time patch sweep documented as no-op (ModelDb not ready) with the real sweep at NMainMenu._Ready.
Next Step: In-game smoke test (multi-row Gigantification card, Fatal-row card, co-op extra turn discounts, override with self-scaling row sourced from damage).

## 2026-06-11 - Rainbow Glitter tiny top-left window root cause

Hypothesis: The glitter pattern renders in a small fixed top-left region because the shader maps coordinates from screen pixels (FRAGCOORD) instead of the card-tracking UV space all other finishes use.
Finding: True.
Evidence: mods/card_editor/CardEditorCardFinishPatches.cs - Rainbow Glitter was the ONLY shader using FRAGCOORD (old art_rect_uv helper dividing FRAGCOORD by card_effect_screen_origin/size); all 10 other finish shaders use effect_uv = card_effect_uv_origin + UV * card_effect_uv_scale. ApplyLocalArtSpace snapshots the global rect ONCE at sync time - pre-layout it degenerates to origin (0,0) with the 300x422 fallback = the exact tiny top-left window; even a correct snapshot would go stale on every card move/hover/scale.
Reason: FRAGCOORD is window-pixel space; a one-shot screen-rect uniform cannot track an animating Control. UV on the portrait TextureRect (with uv_origin=0/uv_scale=1 from ApplyLocalArtSpace) IS the art-rect coordinate, updated for free by the renderer.
Next Step: In-game check: glitter covers the full art, follows the card in hand/hover/reward screens, matches other finishes. Lesson recorded: finish shaders must stay in UV space; never coordinate-map via FRAGCOORD + snapshotted rect uniforms.

## 2026-06-11 - Auto Action x Whenever ungated (universal, loop-safe)

Hypothesis: Auto Action rows can be ungated for Whenever triggers by routing them through the existing power pipeline, with a chain-scoped guard bounding cross-effect recursion.
Finding: True (design verified by 2-agent pass, implementation by 3 adversarial reviewers; one consensus must-fix found and fixed, plus an exponential stack-merge growth path closed).
Evidence: The gate was 3 pieces: SupportsAsPower exclusions (the root - blocked install/routing/UI), the UI snap-back (already self-resolving once SupportsAsPower passes: it silently ticks Power), and a missing ExecuteEffect dispatch (auto rows reaching it were silent no-ops that still CONSUMED Use Limit). Re-entrancy was already proven: PlayCardFromPile was a legal Whenever power calling CardCmd.AutoPlay from hook continuations - and ran UNGUARDED (real runaway shipping before this work, now retroactively capped).
Implementation: storage-time NormalizeSelfPileAutoEffect in AddPowerEffects (unified-kind keys); ExecuteEffectCore dispatch -> TryRunSelfPileAutoEffect (host via UsesSourceCardForImmediatePowerExecution + EffectSourceContext for scheduler clones); IsPowerEffect skips in all card-hosted sweeps (no double-fire); chain guard in CardEditorAutoPlayLoopGuard (AsyncLocal parent-linked path nodes + shared totals: same key max 3/path, depth 12, 64 activations/chain; consumeActivation made real for precounted inner entries); SINGLE Use Limit consumption point inside TryRunSelfPileAutoEffect AFTER all pile/position/condition gates (power rows skip the generic ExecuteEffect consume - was burning uses on non-matching trigger fires; card-hosted rows now consume once AND actually gate, previously display-only/double-fast); auto-action power entries do NOT merge-stack (replay refreshes - stacking doubled per event with Allow Self Trigger on); card text emits action-clause-only under ApplyPowerTriggerPrefix.
Reason: All 3 unified variants + 2 legacy kinds funnel through one runner; all 20 count events + every other power trigger funnel through ExecuteOrSchedulePowerEffect -> universal by construction.
Next Step: In-game: "Whenever a card is drawn, if this is in your discard pile, play it" (fires once per draw, chain-capped if self-amplifying); Use Limit 2/turn gates only on matching activations; legacy OnDraw/turn-boundary auto rows unchanged.

## 2026-06-12 - Adversarial review: "Triggering Card" unlock (uncommitted vs 97a9a13)

Hypothesis: The ThisCard/"Triggering Card" widening lets a power row "Whenever you discard a card, IT gains Sly" work with zero manual picking, without breaking other kinds, save/load, or card text.
Finding: Partially true (headline build works end-to-end; no crashes; but the new power-ThisCard text branch is trigger-blind, Generated rows silently change executionPlay.Card for ALL kinds, FetchSpecificCardToHand gets a meaningless ThisCard option, and several formatters lack ThisCard/Top/Bottom branches).
Evidence: CardEditorExtraEffectPower.cs:1074-1079,1206-1235,980-1006,1021-1042; CardEditorExtraEffects.cs:5399-5424,29043-29073,29151-29156,30302-30308,30012-30115,20029-20046,16340-16371,34840-34843; NCardEditorPopup.cs:22321-22356,25566,25596-25609,25973; CardEditorExtraEffectScheduler.cs:93,313-332.
Reason: Runtime resolution verified by tracing AfterCardDiscarded -> TriggerCountEvent(triggeringCard) -> ExecuteOrSchedulePowerEffect -> executionPlay.Card -> GrantKeywordToCards source-card append (no tickbox needed; pile is NOT a still-there guard). Text branch keys only on IsPowerEffect, so non-card triggers (StartOfTurn etc.) render "it gains" while acting on the HOST card.
Next Step: Make FormatGrantKeywordToPile's power ThisCard branch trigger-aware (reuse PowerTriggerProvidesTriggeringCard + ThisCardSelectionUsesExecutionCard); gate Fetch out of the ThisCard insert; fix Select((int)All) index-vs-id at NCardEditorPopup.cs:25973/25983.

## 2026-06-12 - "Whenever you discard a card, it gains Sly" unlocked (Triggering Card)

Hypothesis: The runtime already delivered the event card to Whenever power rows; only UI selection exposure was missing.
Finding: True (2-agent research + adversarial implementation review).
Evidence: Vanilla discards append to the BOTTOM of the discard pile (CardPileCmd.Add default position) - the community's Top Offer attempt grabbed the oldest card. The Whenever pipeline sets executionPlay.Card = triggeringCard for kinds not forcing the host; the ThisCard selection mode (existing, save-safe, zero selector UI) auto-includes the source card as candidate WITHOUT the "Include this card" tickbox; GrantKeywordToCards receives cardPlay.Card. The ONLY blocker: RefreshMoveSelectionModeOptions never offered ThisCard for GrantKeywordToPile (only DelayedPileAction/RemoveCardsFromDeck).
Shipped: ThisCard offered for all SupportsIncludingSourceCardInSelection kinds (except Fetch - id-based, mode-blind), labeled "Triggering Card" when power + card-carrying trigger + a kind that passes cardPlay.Card (NOT the EffectSourceContext-first kinds DelayedPileAction/TransformCards/PlayCardFromPile/ConsumeCardValue - those resolve to the HOST on power rows); Top/Bottom exposed for the card-action family (honest positional picks - "bottom of discard pile" = newest discard); trigger-aware grant text ("Whenever you discard a card, it gains Sly." / "This card gains..."); Generated count event now passes triggeringCard (fixes Generated card filters that never fired; aligns with Played/Drawn/Discarded/Exhausted - NOTE: old presets with non-host kinds on Generated rows now act on the generated card instead of the host, accepted as the correct universal semantics); adjacent pre-existing Select(id-as-index) bug fixed at the future-matching-cards coercion (would have shifted targets with the new insert).
Documented edges: scheduled (non-Immediate) ThisCard acts on the host snapshot (graceful no-op for granting); the Card Source pile dropdown is NOT a "still there" guard for ThisCard (the event card is granted even if it moved); six formatters still lack ThisCard/Top/Bottom text branches (cosmetic, listed in review).
Next Step: In-game: power "Whenever you discard a card" + Grant Keyword Sly + Mode "Triggering Card" - each discarded card gains Sly with no picker.

## 2026-06-12 - Adversarial review: ThisCard/Top/Bottom text branches (8 formatters)

Hypothesis: The text-only diff covers every selection mode the widened dropdowns can produce, with correct branch order, grammar, power-prefix lowercasing, and the exact ThisCardSelectionUsesExecutionCard kind list.
Finding: Partially true (one coverage gap).
Evidence: git diff vs d030bda; CardEditorExtraEffects.cs FormatMoveCardsBetweenPiles 19671-19689, FormatUpgradeCardsInPile 19995-20007, UsesEventCardThisCardWording 20054-20058, FormatGrantKeywordToPile 20078-20090, FormatDiscardCards 20177-20189, FormatExhaustCards 20232-20244, FormatTransformCards 20300-20312, FormatCopyCardsFromPileToDeck 20622-20634, FormatSelectCardsFromPile 20731-20743, FormatRemoveCardsFromDeck 20783-20788, FormatPlayCardFromPile 21039-21116, BuildDelayedPileSelectionText 19847-19859, FormatConsumeCardValue 21128-21131, ApplyPowerTriggerPrefix 16340-16342, kind lists 3475-3508, 29130-29152; NCardEditorPopup.cs 22272-22335, 25568.
Reason: All 8 edited formatters verified correct (order, grammar, lowercase-after-prefix, wrapping AppendCardSelectionNote->BuildCostFilteredText). But PlayCardFromPile is in SupportsIncludingSourceCardInSelection AND its MoveSelectionModeSelect is visible, so ThisCard IS offered for it - and FormatPlayCardFromPile has no ThisCard branch: it falls to "Play a card from your hand." ConsumeCardValue/DelayedPileAction Top/Bottom claim verified TRUE via BuildDelayedPileSelectionText.
Next Step: Add a ThisCard branch to FormatPlayCardFromPile ("Play this card." host-card wording; kind is EffectSourceContext-first so no "it" variant), or gate PlayCardFromPile out of the ThisCard insert like Fetch.

## 2026-06-16 - Relic Editor: Custom Effects UI (reuse whole card popup)

Hypothesis: the card editor's effect UI can be reused for relics without re-implementing it or doing surgery on NCardEditorPopup.
Finding: True.
Evidence:
- AddExtraEffectRow is a private instance method woven through NCardEditorPopup (_extraEffectRows referenced in 20+ places) - not extractable cheaply.
- NCardEditorPopup.Create(CardModel, Action onApplied, useModalContainer) opens the whole popup on any card with an apply callback.
- NModalContainer is single-slot (Add rejects a 2nd modal). BUT Close clears the modal ONLY if _useModalContainer==true; with useModalContainer:false the popup just QueueFrees itself and is parented via NGame.Instance.AddChildSafely (proven by ShowPopup else-branch, line 35088). So the card popup can OVERLAY the relic editor without evicting it.
- Card effect round-trip goes through CardEditorOverrides: popup reads via TryGetEffectiveOverride(Get), apply writes via Set (both _isCreatedCard branches mirror to it). IsCreatedCardId requires entry prefix "CARD_EDITOR_CREATED_CARD" - the proxy (CardEditorRelicProxyCard) does NOT match, so it is treated as vanilla, writes only to CardEditorOverrides, and never appears in the created-cards list.
Reason: classified True because the architecture compiles (0 errors) and every coupling/edge (single-slot modal, created-card pollution, seed/readback path) was resolved by reading the source, not guessed.
What Changed:
- Phase 2: wired 6 relic triggers to game hooks (OnCombatStart/TurnStart/TurnEnd/CardPlayed/CombatEnd/DamageTaken). OnEnemyKilled/OnPickup deferred.
- Phase 3: NRelicEditorPopup gained a "Custom Effects" section - one row per trigger with an "Edit Effects (N)/Add Effects" button that opens the full card-effect editor on the proxy card, seeded/read-back via CardEditorOverrides; committed to RelicOverride.ExtraEffects in ApplyCurrentRelicEditsToStore.
Next Step: in-game test (user) - verify the overlay displays/blocks input, effects round-trip per trigger, and a relic with e.g. "OnCombatStart: gain Block" actually fires.

## 2026-06-16 - Relic effects: "share one component" via embedded reuse (architecture locked)

Hypothesis: relics can host the FULL card-effect UI with one shared source of truth, without a ~20k-line duplication and without changing the card editor's behavior.
Finding: True (architecture validated end-to-end; foundation built + compiling).
Evidence / why duplication was rejected: AddExtraEffectRow is 7,471 lines (14132-21603); ExtraEffectRow is 511 lines (~200 controls, ~40 KeywordTickbox); dependency closure is 400+ helpers. Full 1:1 copy ~= 20k lines + drift + dead card-only options. User chose "share one component".
Key viability facts:
- Initialize() (1457) is lightweight STATE-SETTING only (no UI build); layout consts (_fieldMinSize/_coreEffectDropdownWidth/_labelWidth) are static/const -> an embedded host needs almost no special init.
- BuildExtraEffectsUi(VBoxContainer) (11868) is the self-contained effects-section builder (_extraEffectsContainer field at 216).
- QueuePreviewUpdate is called 409x but funnels through QueuePreviewRefresh -> gating ONE method no-ops all preview work.
- NModalContainer is single-slot (Add rejects 2nd modal); useModalContainer:false popups free only themselves (Close at 35244) and are parented via NGame.Instance.AddChildSafely (ShowPopup else-branch 35088).
- BuildOverrideFromUi (31294-32908) effects loop (31740-32808) is CLEAN of overrideData; non-effect blocks are 31304-31736 and 32815-32905.
What Changed (DONE, additive, card path provably unchanged - git diff NCardEditorPopup = +19/-1, all behind _isEmbeddedEffectHost flag default false; builds 0 errors):
1. Added field `_isEmbeddedEffectHost`.
2. QueuePreviewRefresh early-returns when embedded.
3. BuildOverrideFromUi: moved Keywords into a gate; gated both non-effect regions with `if (!_isEmbeddedEffectHost)`. Embedded mode -> only ExtraEffects built.
Architecture (group-based, NOT per-row trigger): relic editor hosts one hidden NCardEditorPopup instance PER relic-trigger group; sets _isEmbeddedEffectHost=true, Initialize on the proxy card, calls BuildExtraEffectsUi into the relic menu container; relic trigger chosen at group level (per-row card trigger dropdown hidden in embedded mode); readback = host.BuildOverrideFromUi().ExtraEffects paired with the group trigger -> RelicEffectEntry.
Next Step (NOT yet done): (a) add internal embedded API on NCardEditorPopup: InitializeAsEmbeddedHost(proxyCard)+BuildEmbeddedEffectsUi(container)+ReadEmbeddedEffects(); (b) hide per-row TriggerSelect column when _isEmbeddedEffectHost; (c) optional kind-filter for relic-unsupported kinds; (d) rewrite NRelicEditorPopup Custom Effects section as trigger groups each embedding a host. Then user in-game test.

## 2026-06-16 - Relic full-parity effects: IMPLEMENTATION COMPLETE (builds, awaiting in-game test)

Finding: True - the embedded "share one component" implementation is complete and compiles (0 errors).
NCardEditorPopup (git diff +80/-1, additive, all behind _isEmbeddedEffectHost; card path output identical):
- _isEmbeddedEffectHost field; QueuePreviewRefresh + _Ready early-return when embedded.
- BuildOverrideFromUi non-effect regions gated -> embedded build = ExtraEffects only.
- Embedded API (internal): InitializeAsEmbeddedEffectHost(CardModel), BuildEmbeddedEffectsUi(VBoxContainer), LoadEmbeddedEffect(CardExtraEffect), ReadEmbeddedEffects()->List<CardExtraEffect>.
- Per-row trigger dropdown hidden when embedded.
NRelicEditorPopup (+240): Custom Effects section = trigger groups. Each group = relic-trigger dropdown + hidden NCardEditorPopup host (InitializeAsEmbeddedEffectHost on the proxy card, AddChild, BuildEmbeddedEffectsUi into the group container, LoadEmbeddedEffect per saved effect). "Add Effect Trigger" button; per-group Remove. ApplyCurrentRelicEditsToStore -> CollectEffectGroupEntries() reads each host.ReadEmbeddedEffects() paired with the group trigger -> RelicOverride.ExtraEffects.
UNTESTED RUNTIME RISKS (user tests in-game): (1) embedded host runs AddExtraEffectRow/BuildExtraEffectsUi outside the full popup - may NPE on popup state set only in EnsureUiBuilt; (2) BuildOverrideFromUi top calls CompletePendingExistingExtraEffectRowsNow/CompleteDeferredCreatedEffectValueRowsNow before the gates - may touch popup state; (3) layout/sizing of the embedded editor inside the relic scroll. Fix path: run game, open a relic, Add Effect Trigger, build an effect, Apply, check combat.
Next Step: user in-game test; iterate on any NPE/layout from the 3 risks above.

## 2026-06-16 - Relic effects deep-dive bug hunt: 10 confirmed, 10 FIXED (builds 0 errors)

Adversarial workflow (6 dimensions, per-finding verify): 17 candidates -> 10 confirmed (7 correctly refuted). All fixed + compile-verified. User tests in-game.
HIGH:
1. EnchantCard dropdown empty + dropped on save (embedded skips EnsureUiBuilt -> _enchantmentIds empty). FIX: new EnsureEnchantmentIdsLoaded() (NCardEditorPopup) called from PopulateExtraEffectEnchantmentSelect + GetSelectedExtraEffectEnchantmentId (lazy-load, mirrors powers).
2. Player-choice relic effects abandoned the paused action. FIX: CardEditorRelicEffects DispatchForPlayer + BeforeCombatStart loop now `bool completed = await Assign...; if(!completed && ctx.GameAction!=null) await ctx.GameAction.CompletionTask;` (matches canonical sites).
3. AddCopyOfThisCard/AddExactCopyOfThisCardToDeck on a relic cloned the blank proxy into the run deck (save corruption). FIX: guard `if (cardPlay?.Card is CardEditorRelicProxyCard) return;` at top of AddExactCopiesOfThisCardToDeck + AddCopiesOfThisCard (CardEditorExtraEffects).
4. Card-picker overlay parented to the invisible embedded host -> never rendered + soft-lock. FIX: new AddOverlayChild() routes overlays to the visible host (GetParent) when embedded; all 7 AddChild(overlay) sites now use it (card path identical).
MEDIUM:
5. Saved trigger outside ActiveRelicTriggers mislabeled/never fired. FIX: AddEffectGroup normalizes trigger to ActiveRelicTriggers[0] when selIndex<0.
6. OnCardPlayed fired for all players in MP (and OnTurnStart). FIX: new WrapForOnePlayer; AfterCardPlayed scopes to cardPlay.Card.Owner, AfterPlayerTurnStart scopes to player.
7. Proxy card leaked into CombatState._allCards each trigger. FIX: combatState.RemoveCard(proxy) after dispatch in RunRelicTrigger.
LOW:
9. Trigger parse case-sensitive + accepted out-of-range numerics. FIX: Enum.TryParse(ignoreCase:true) && Enum.IsDefined in ParseEffectEntries.
10. Hidden "As Power" toggle made relic effects one-shot. FIX: ReadEmbeddedEffects forces e.AsPower=false.
Diff (card path provably unchanged, all behind _isEmbeddedEffectHost): NCardEditorPopup +127/-8 (8 = Keywords move + 7 overlay-call reroutes), CardEditorExtraEffects +10, CardEditorRelicOverrides +68/-3, NRelicEditorPopup +247. CardEditorRelicEffects (new file) updated.
Refuted (not bugs, sound reasoning): proxy-override double-load, host name dups (cosmetic), cross-group contamination, GetById-throws, OnCombatEnd timing, OnPickup resave, unfiltered kind dropdown (cosmetic dead options).
Next Step: user in-game test of the full feature + these fixes.

## 2026-06-16 - Relic effects UX/usability pass: 18 confirmed, fixed the clear wins (builds 0 errors)

Adversarial UX workflow (6 dimensions, per-finding verify): 29 candidates -> 18 confirmed (11 refuted; one refutation corrected my own assumption - the row-width overflow is real but the "dead column gap" was correctly refuted).
FIXED (9 fixes covering 12 confirmed; card path provably unchanged - all NCardEditorPopup changes gated on _isEmbeddedEffectHost):
- Layout overflow (#1): widened relic PanelSize 980x660 -> 1180x720 so full effect rows (~670px) fit the settings column without a horizontal scrollbar.
- One-group-per-trigger (#2 silent merge on save/reopen, #3 Add-button duplicate, #4 dropdown duplicate): FirstUnusedTrigger now nullable; added RefreshGroupTriggerOptions() that SetItemDisabled on already-used triggers in every group dropdown and disables "Add Effect Trigger" when all 6 used; called after add/remove and on trigger change.
- Help autowrap (#13): CreateMutedLabel sets AutowrapMode.WordSmart (all muted help wraps).
- Group frame (#7): groupPanel gets CreateInnerStyle() stylebox -> each trigger renders as a distinct inset card.
- Redundant heading (#6): BuildExtraEffectsUi skips the "Extra Effects" section label when embedded.
- Dead control "Add Effect Source" (#9,#10): hidden when embedded (card-only RunEffectSourceCard picker).
- Dead control "As Power" (#11): tickbox hidden when embedded (value was force-discarded -> misleading).
- Card-only kind filter (#12): new RelicUnsupportedEffectKinds denylist (AddCopyOfThisCard, AddExactCopyOfThisCardToDeck, CardCostsLess, CardStarCostsLess, SelfScaling, PersistentSelfScaling, AutoPlaySelfFromPile, RunEffectSourceCard) skipped from the relic kind dropdown; conservative (ambiguous-but-harmless kinds kept).
DEFERRED (documented, lower value / needs in-game verify): empty group vanishes on reopen (#5/#14 - would need a persisted-triggers data-model change; minor); hidden-trigger column micro-alignment (#8 - contested confirmed-vs-refuted, cosmetic, verify in-game); card-play-only target options offered (#16 - low, benign fallback); per-group title/index (#17 - "When:" dropdown serves as title); multiple pickers can stack (#18 - contrived).
Diff: NCardEditorPopup +190/-42 (all embedded-gated), NRelicEditorPopup +292/-1.
Next Step: user in-game test of the relic menu UX.

## 2026-06-16 - Relic effects: fixed the 4 previously-deferred UX items (builds 0 errors)

- Empty group vanishes on reopen (#5/#14): added RelicOverride.EffectTriggers (List<RelicTriggerKind>) + DTO string round-trip (ParseTriggers with ignoreCase+IsDefined), Clone, and IsEmpty now counts it. ApplyCurrentRelicEditsToStore persists every group's trigger (even empty). LoadExistingEffectGroups now builds groups from EffectTriggers (authoritative) unioned with effect-derived triggers, falling back to effect-derived for legacy overrides. Empty trigger groups now survive a reopen.
- Card-play-only target options (#16): ConfigureExtraEffectTargets strips Target + EventTarget when _isEmbeddedEffectHost (they resolve to a fallback with no card-play/hovered/event context); default-target fixup also runs in embedded mode so a damage effect defaults to RandomEnemy/Self instead of an inapplicable Target. Conservative denylist (kept ally/player targets that may be valid in MP).
- Group titles (#17): each group now has a 24px trigger-name heading (CreateHeading) above the "When:" row, updated live on trigger change - distinct scannable anchor per group.
- Picker stacking (#18): AddOverlayChild now guards at the shared parent (relic editor) via an EmbeddedOverlayMetaKey marker - opening a picker frees any already-open one, so embedded group pickers can't stack.
All gated on _isEmbeddedEffectHost (card editor behavior unchanged) except the additive EffectTriggers field. Deferred remaining: only #8 (hidden-trigger column micro-alignment - contested confirmed-vs-refuted, cosmetic, verify in-game).
Next Step: user in-game test.

## 2026-06-16 - Multiplayer sync deep dive: WHY it fails + robust fix design (diagnosis only, not yet implemented)

Hypothesis: the sync feature fails because "players' cards don't match" and the snapshot is off-by-default.
Finding: Partially true / reframed. Default is ON (MultiplayerSyncEnabled=true). The real causes are TIMING + DETERMINISM, not coverage.
Evidence (game source + mod, via 4-probe workflow w3ps0pjo3):
- Desync = host-authoritative XxHash32 over NetFullCombatState (HP/block/powers/energy/stars/gold, 5 card piles BY id+upgrade+SavedProperties+enchantment, relics, orbs, RNG seeds+counters, last action id + hook id). Cards hashed by IDENTITY not stats (SerializableCard.cs). Per-action, FIRST mismatch = immediate hard kick + abandon run, no resync (ChecksumTracker.cs:118-199). Also checked at event-room + rest-site exits.
- MP is deterministic LOCKSTEP: only actions+card-index cross the wire; each peer re-simulates from a shared seed. Starting deck built synchronously at Player.CreateForNewRun BEFORE Launch; LobbyBeginRunMessage carries no deck. RNG = shared seeded RunRngSet/PlayerRngSet; effects must draw in identical order/count. The all-peers-ready barrier is IStartRunLobbyListener.BeginRun (StartRunLobby.cs).
- Mod RNG SOURCE is correct (all combat picks use owner.RunState.Rng.Combat* seeded channels; no System.Random/Guid in combat). BUT effects run as LOCAL per-peer Harmony postfixes (re-simulated, not replicated); risks: rng==null silent fallbacks offset the stream, per-player trigger loops gated on LocalContext.NetId, PlayerChoice ChooseOne mode = unreplicated human choice.
ROOT CAUSES (layered): (1) NO BARRIER - snapshot applied async on client's first _Process after lobby bind, with nothing blocking deck-build/combat-start; first run desyncs. (2) Mid-run ApplyAllToExistingCards mutates live cards on only the client = itself a desync. (3) Custom effects re-simulate locally -> need identical defs + identical RNG/trigger order across peers. (4) Config: client applies snapshot regardless of own setting; host tickbox is the only real switch; persisted so a prior OFF sticks.
ROBUST FIX (design, not yet built): L1 apply snapshot at the BeginRun barrier + gate run-start/ready until client _lastAppliedSequence>0 (handshake + timeout). L2 freeze the definition set for the whole MP run; never apply mid-combat (defer to safe boundary). L3 determinism hardening: kill rng==null fallbacks, pin ?? fallback-owner RNG, ensure triggers fire identical count/order per peer, force ChooseOne Random (not PlayerChoice) in MP. L4 host-authoritative sync UX + push-on-ready/retry + detect peers missing the mod. L5 (last-resort opt-in) disable ChecksumTracker.IsEnabled consistently on host to suppress the kick - sacrifices correctness, players silently diverge; prevention >> suppression.
Next Step: confirm with user which layers to implement; L1+L2 directly solve "cards dont match"; L3 needed for custom effects to be MP-safe.

## 2026-06-16 - MP sync robust fix: implemented L1/L2/L4/L5 (+L3 verified), bug-hunted, 15 bugs found, fixed the real ones

IMPLEMENTED (all build 0 errors; changes confined to CardEditorMultiplayerSync.cs + CardEditorMultiplayerSettings.cs - card editor/effects untouched):
- L1 ready handshake: Harmony prefix on StartRunLobby.SetReady (+LoadRunLobby.SetReady) -> AllowClientReady(fireReadyTrue, ready). A client holds "ready" until its host snapshot is applied (or 8s timeout), so the run never starts on mismatched definitions. Re-fires via Update->FirePendingReadyIfNeeded.
- L2 freeze-for-run: IsRunActive()=RunManager.Instance.IsInProgress. CanEditSharedState() returns false during a run; OnSnapshotReceived + host Update broadcast skip during a run. Definitions frozen at run start. (Verified no deadlock: RunManager.State is null during the lobby so the snapshot applies pre-run; freeze self-releases at CleanUp.)
- L4: client re-requests snapshot every 2s until applied; timeout warning.
- L5 escape hatch: DisableDesyncProtection setting + Harmony prefix on ChecksumTracker.CompareChecksums (host-only) suppressing the kick; new "Disable Desync Protection" settings line. (Verified CompareChecksums is the single complete chokepoint for the kick chain.)
- L3 (effect determinism): NO code change needed - VERIFIED the mod's RNG already uses the game's seeded channels AND player-choices go through CardSelectCmd.FromChooseACardScreen which is fully MP-synced via RunManager.PlayerChoiceSynchronizer (same path vanilla Discover uses). So given matched definitions (L1/L2), effects are deterministic. Forcing ChooseOne->Random would have been a regression; correctly avoided. rng==null fallback confirmed unreachable+deterministic.
BUG HUNT (workflow wmyyfbbmc): 20 candidates -> 15 confirmed. FIXED: (1/5) stale ready-gate state leaking across sessions -> ClearPendingReady() on bind+detach + session check in FirePendingReadyIfNeeded; (2/3/9) client un-ready silently re-readied -> AllowClientReady clears pending on !ready; (4/11/15) LoadRunLobby ungated -> added LoadRunLobby.SetReady patch + delegate generalization; (6/10) client DisableDesyncProtection no-op -> greyed the tickbox on clients (host-only); (8) 8s stall vs sync-off -> short-circuit AllowClientReady when !MultiplayerSyncEnabled; (12) redundant snapshot broadcast on desync toggle -> dropped Revision++; (14) hover fallback text -> aligned with loc.
DEFERRED (low/cosmetic): (7) editor Apply/Reset silently no-op during a run with a misleading "host-controlled" log (popup stays open, no toast) - needs button-greying across ~10 sites; (13) desync settings line double-fires its handler (benign, idempotent, pre-existing pattern shared by all 5 lines).
REFUTED (not bugs): freeze-deadlock, checksum-patch-incomplete, L2-blocks-saved-run-apply, FirePendingReady-throw, rng-null determinism.
Next Step: user 2-peer in-game test (checksums are masked in singleplayer/testmode, so only a real Host+Client session validates this).

## 2026-06-16 - MP sync second bug hunt + cosmetic fix (editor-lock feedback)

COSMETIC FIX (the deferred bug 7): GetSharedStateLockReason() helper; card editor + relic editor now grey Apply/Reset + set a why-tooltip ("Card Editor is locked during a multiplayer run...") when editing is locked, instead of a silent no-op; all 5 editor block-site logs (card/relic/base-deck/preset) now report the accurate reason instead of the misleading "host-controlled".
SECOND HUNT (workflow w5egeyu83): 18 candidates -> 9 confirmed (3 refuted: controller-select unreachable, PersistenceSuspended-symmetry hypothetical, transient-disconnect safe). FIXED:
- MEDIUM (regression in the cosmetic fix): card-editor Apply/Reset greying was computed once in BuildUi, but the popup is a persistent/cached instance so reopen showed a stale state. FIX: store _applyButton/_resetButton fields + RefreshSharedStateLockUi() called from PreparePersistentPopupForOpen (every open). Relic editor unaffected (rebuilds per open).
- MEDIUM (pre-existing coverage gap): client SetSlotCountForNextRun only sets ConfiguredSlotCount (next launch), not active SlotCount; the slot card types are registered at launch clamped to the local count, so a higher host slot count can't be realized this session -> divergent created-card/reward pools -> desync. FIX: loud Log.Warn on the client when host CreatedCardSlotCount > client active SlotCount, advising raise Max Custom Cards + restart (full runtime re-registration is out of scope; next-launch alignment already queued).
- LOW: held-ready could double-fire SetReady(true) on a manual re-click racing the applied snapshot. FIX: ClearPendingReady() on the IsClientSnapshotApplied() short-circuit in AllowClientReady.
DEFERRED/NOTED (low, safe): (a) greying goes stale if a run STARTS/ENDS while the editor stays open (rare; block-site CanEditSharedState re-check makes it safe; would need a live run-state notification to open popups); (b) dead class CardEditorMultiplayerCreatedCardDto (~183 lines, unreferenced, superseded by CardEditorCreatedCardsStore.CreatedCardDto) - safe to delete, flagged; (c) settings tickbox double-applies via both the Toggled lambda and the OnTick/OnUntick patch - benign (idempotent setters), pre-existing pattern on all 5 lines.
All builds 0 errors. Verified relic ExtraEffects+EffectTriggers DO round-trip through the synced RelicOverrideDto (no coverage gap).
Next Step: user 2-peer in-game test.

## 2026-06-19 - Relic effects "missing" = stale deploy; deferred fixes (dead DTO, double-apply)

Hypothesis: The relic-effects UI not showing in the relic editor is a code bug (AddEffectsSection not running / early-return / not wired).
Finding: FALSE - stale-deployment problem, not a code bug.
Evidence:
- NRelicEditorPopup.Build():330 calls AddEffectsSection(settings) after number/text/pool; AddEffectsSection():702 unconditionally adds the "Custom Effects" heading + "Add Effect Trigger" button (no early-return / no throw for a relic with no effects).
- csproj only outputs to build\ (no post-build deploy). Live game loads ...\Steam\steamapps\common\Slay the Spire 2\mods\Card_editor\card_editor.dll dated 2026-06-14 (3,500,032 B). Repo staging older still: card_editor_pack 2026-06-06, built cfiles 2026-06-12. Fresh build = 2026-06-19 (3,841,536 B). The relic-effects feature was built AFTER 06-14, so the running DLL predates it.
Reason: Code-only feature (pure C#, hardcoded UI strings -> no .pck change needed) was never copied to the game's mod folder; the game kept loading a pre-feature DLL.
What Changed: Deployed fresh DLL to live game (kept backup card_editor.dll.bak-2026-06-14), card_editor_pack, and built cfiles. Relic Custom Effects section appears on next launch (bottom of relic editor).

Deferred fixes done this session (user: "Fix these btw"):
- Dead DTO removed: CardEditorMultiplayerCreatedCardDto (184 lines, unreferenced) deleted from CardEditorMultiplayerSync.cs via verified-boundary splice. Build 0 errors.
- Settings double-apply fixed: OnTick/OnUntick NBackgroundModeTickbox patches now suppress-only (return !TryGetMultiplayerSettingsLineKind); the Toggled lambda in EnsureSettingsTickboxLine is the single apply path. Also removed the now-dead AND divergent ApplyMultiplayerSettingsTick (it called MarkSettingsDirtyForBroadcast() whereas the lambda sets _forceImmediateBroadcast directly).
- Deferred item (a) run-start/end stale greying: judged effectively handled - both editors recompute the lock on open (card editor RefreshSharedStateLockUi in PreparePersistentPopupForOpen; relic editor rebuilds each open), and the editor is never open during the lobby->run / run->menu transition, so the while-open case is unreachable. No live-notification plumbing added.

Still open:
- Rarity dropdown greyed on first open of card editor until a "class" (card-type) swap. No literal .Disabled on _createdRaritySelect/_vanillaRaritySelect; retarget path DOES re-select rarity (BindCreatedBaseControlsForCurrentCard:1671). Suspected first-open refresh gap via the same-card fast path RetargetLocalizedSharedPopup:1522-1533 (returns without RefreshLocalizedSharedPopupControls). Needs in-game confirmation (created vs vanilla; screenshot) before touching the 38k-line editor.

Next Step: user restarts game, confirms relic Custom Effects section shows + clarifies rarity bug (created/vanilla + meaning of "swapping class").

## 2026-06-19 - Verified ChatGPT review (Notes/POTENTIAL_BUGS_2026-06-19.md, 5 items)

Hypothesis: ChatGPT's 5 flagged issues are all real bugs needing fixes.
Finding: Mixed - 1 real+fixed, 1 real-but-out-of-mod-scope, 1 unverifiable-statically (testable in-game), 2 not-bugs.
Evidence + verdicts:
- P1 client edit mid-run (CardEditorMultiplayerSync.OnEditRequestReceived): TRUE -> FIXED. Handler checked Host+SyncEnabled+AnyPlayer authority but NOT IsRunActive(), unlike OnSnapshotReceived / host-broadcast skip / CanEditSharedState. A mid-run client edit (AuthorityMode=AnyPlayer) would ApplyState on host + BroadcastSnapshotToReadyPeers -> desync. Added the same IsRunActive() freeze guard (verbose-logged). Verified compiled into DLL via UTF-16 (strings -e l) search; rebuilt + redeployed to live game + staging.
- P1 root solution build broken by scratch files: TRUE but OUT OF MOD SCOPE. .tmp_cardcmd.cs (48KB, gitignored), _tmp_NCardHolderHitbox.cs, _tmp_NGridCardHolder.cs at root; root csproj uses SDK default compile (no <Compile Remove>) so solution build fails. Mod builds fine via card_editor.csproj (the deploy path). NOT fixed - offered cleanup (move scratch out of compile glob, or <Compile Remove>).
- P2 relic proxy card registration: UNCONFIRMED statically. CardEditorRelicProxyCard : CardEditorCreatedCardBase (same base as working CardEditorCreatedCard01-30). RegisterCreatedCardsInPools is POOL registration (filters CardEditorCreatedCardNN by NAME) and correctly excludes the proxy (must never be pooled) - NOT the ModelDb type-registration path. The mod-loader/ModHelper assigning ModelDb ids is in a compiled assembly (not in decompiled source), so proxy auto-registration cannot be proven by reading. Reuses the working base by deliberate design (comment CardEditorRelicEffects.cs:26-28). DIRECTLY TESTABLE: open relic -> click Add Effect Trigger; works => proxy resolves; if it logs "Relic proxy card is not registered" (NRelicEditorPopup:833) => needs explicit registration. = #1 in-game check after deploy.
- Risk end-of-turn relic semantics: NOT a bug. Hook.AfterTurnEnd is SIDE-level (side param, no player) so Wrap (all players on CombatSide.Player) is the only option + correct for STS2 co-op shared-side turns; consistent with existing scheduled-effect iteration. OnTurnStart uses WrapForOnePlayer only because Hook.AfterTurnStart carries a player. Playtest-confirm, no change.
- Risk desync escape hatch broad: BY DESIGN (L5 Disable Desync Protection, user-enabled, gated). No change.
Next Step: user restarts, tests Add Effect Trigger (P2 gating check), reports; decide P1b scratch-file cleanup.

## 2026-06-19 - Fixed 4 reported bugs (Neow pool, grant-to-ally, self-damage display, osty hover)

Hypothesis: 4 user/forum-reported bugs are real and fixable.
Finding: True for 3 fully + B partially (B's AnyPlayer CARD-target crash is a game-level gap, deferred).
- A (relic removed from all pools still spawns via Neow): TRUE -> FIXED. Neow.GenerateInitialOptions uses a HARDCODED RelicOption<T> list, not the pools; all Neow offerings + NeowsBones gate on RelicModel.IsAllowedAtNeow. Added CardEditorRelicOverrides.IsRemovedFromAllPools (override exists + PoolKeys empty; editor only writes PoolKeys when != vanilla, so empty == deliberate) + a TargetMethods Postfix on every IsAllowedAtNeow impl forcing false for removed relics. Normal rewards/library/ancient already respected the override via GetUnlockedRelics.
- B (grant keyword to ally hand): grant always hit self, AnyPlayer crashed, text said "your hand". FIXED (functional + text): GrantKeywordToPile case now loops ResolveTargetPlayers (like GainGold) honoring Self/AnyAlly/AnyPlayer/AllAllies; description uses new target-aware GetCardPileLocationForTarget. MP-safe (shared RunState RNG / synced choice; enemy/non-player targets resolve to no grant). PARTIAL: the "any player" CARD target-type crash is a GAME targeting gap (CardModel.cs:1404 marks AnyAlly unplayable when <=1 player but has no AnyPlayer equivalent), exposed by the editor offering AnyPlayer. Needs the crash log to patch safely. Workaround: use AnyAlly card target (works) + AnyAlly/AnyPlayer effect target.
- C (self-damage "Take 5" shows attack-scaled number): TRUE -> FIXED. TryGetHookedMoveAmountPreview now returns false for non-osty DealDamage with Self/AnyPlayer/AnyAlly/AllAllies target -> preview shows enchanted base (no Strength/Vulnerable/Weak), matching flat runtime damage.
- D (osty attack damage not updating on enemy hover): TRUE -> FIXED. FormatLineForAmount condition (~15943) now includes OstyAction -> routes through TryGetScaledAmountText -> TryGetHookedMoveAmountPreview (already handles Osty: effectiveDealer = owner.Osty), recomputing Vulnerable/Lethality on hover.
Files: CardEditorExtraEffects.cs (B,C,D), CardEditorRelicOverrides.cs (A). Build 0 errors. Deployed live + staging; committed to main.
Next: user retest; for B's AnyPlayer crash, provide the exception/log so the game-level targeting gap can be patched.

## 2026-06-19 - Fixed Any-Player card-target crash (B#2)

Hypothesis: the 'any player' card-target crash is fixable safely.
Finding: True -> FIXED (via mapping, not a risky core patch).
Root cause: TargetType.AnyPlayer is only half-wired for COMBAT card targeting in the base game - NTargetManager.AllowedToTargetNode handles it (case at ~282) but CardModel.IsValidTarget falls through to 'return false' for any non-null AnyPlayer target (no AnyPlayer branch), and NCardPlay/NControllerCardPlay/NMouseCardPlay + the mod's CardEditorExtraEffectTargetingPatches only special-case AnyEnemy/AnyAlly. So a PLAYED AnyPlayer card has zero valid targets -> crash. AnyPlayer is only used by rest-site/special contexts via direct StartTargeting, never as a played combat card.
Fix: map AnyPlayer -> AnyAlly at the single source of a created card's target (CardEditorCreatedCards.TargetType getter), covering both the dynamic-identity and store paths, new and existing cards. AnyAlly is fully supported end to end; the effect-level target (CardExtraEffectTarget, fixed earlier via ResolveTargetPlayers) still restricts grants to player allies, so grant-to-ally keeps working. Chose this over patching ~5 core game UI methods (high risk to all cards).
Tradeoff: an AnyPlayer card target now behaves like AnyAlly (Osty hoverable in the targeting cursor), but clicking Osty grants nothing (effect filters to players). Build 0 errors; deployed live + staging; committed to main.
Next: user retest the grant-to-ally card with the AnyAlly (or formerly-AnyPlayer) target.

## 2026-06-21 - "Stars Spent" Whenever-trigger (quick win #2)

Hypothesis:
"Stars Spent" can be added as a clean one-line mirror of "Energy Spent" (EnergyUsed), since EnergyUsed already works as a Whenever-trigger.

Finding: Partially true.

Evidence:
- EnergyUsed reads a dedicated game history type EnergySpentEntry (CardEditorExtraEffects.cs TryGetResourceHistoryCountMultiplier case ~34049; CombatHistory.cs:84 adds EnergySpentEntry).
- The game has NO StarsSpentEntry - stars only have StarsModifiedEntry (gained/lost), which is exactly why "Stars Lost" never fires on spending.
- BUT the game DOES expose Hook.AfterStarsSpent(ICombatState, int amount, Player spender) (Hook.cs:910), used by GalacticDust / ChildOfTheStarsPower.
- The mod already self-tracks events the game does not (TimesGainedHp etc.) via _resourceCountHistory + RecordResourceCount, populated from hook Postfix patches in CardEditorResourceCountPatches.cs.

Reason:
No StarsSpentEntry means no one-line history mirror. But AfterStarsSpent + the existing _resourceCountHistory pattern make it a clean hook-tracked feature instead.

What Changed:
Implemented StarsSpent (enum=52) end-to-end: new Hook.AfterStarsSpent Postfix patch -> RecordResourceCount + TriggerPowerCountEvent + RecordRunProgress; added to PowerTriggerCountEvents (the Whenever dropdown) + _cardSmithCountEvents; label "Stars Spent"; verb spend/spent with star icon; scaling-count read case via _resourceCountHistory; quest gate. Deliberately NOT added to PowerCountEventUsesCardFilters (the stars hook supplies no triggering card, unlike EnergySpent). Build: 0 errors; markers present in DLL.

Next Step:
Deploy to the 4 locations + in-game test ("Whenever you spend stars, gain Block"); then proceed to quick win #8 (Osty's Cards filter) / #7 (Copy Buffs/Stats/Power).
## 2026-06-21 - Scaling sources: Current Turn Number + Number of Enemies (quick win #3 subset)

Hypothesis:
"Current value" count events (like CurrentStars) can be mirrored to add CurrentTurnNumber + NumberOfEnemies as scaling/count sources.

Finding: True.

Evidence:
- The count-event dropdown iterates Enum.GetValues<CardExtraEffectCountEvent>() (NCardEditorPopup.cs ~18993), so appending enum values + a label makes them selectable - no explicit array needed (unlike the Whenever-trigger dropdown PowerTriggerCountEvents).
- CombatState.RoundNumber is 1-indexed (CombatState.cs:79 inits to 1, CombatManager.cs:1185 increments) => it IS the turn number, no +1.
- Living enemies enumerated via combatState.Enemies filtered by IsEnemy && IsAlive (mirrors GetRelevantEnemyConditionTargets at 18482).
- A "current value" event touches exactly 6 sites: enum, label switch, CountEventUsesWindow exclusion (instantaneous, no time window), scaling-suffix text, condition text, and the value resolver (~33728, CurrentStars => PlayerCombatState.Stars).

What Changed:
Added CurrentTurnNumber=53 (=> combatState.RoundNumber) and NumberOfEnemies=54 (=> count of living enemies) across all 6 sites. Inserted the 3 text/resolver blocks via a brace-tracked PowerShell script (Edit tool kept failing on deep-nested tab matching); fixed a uniform +1 tab over-indent afterward. Build 0 errors; deployed + hash-verified to all 4 locations.

Next Step:
In-game test ("deal damage equal to the turn number" / "...equal to the number of enemies"); continue backlog (Osty HP scaling, Osty's Cards filter #8, Copy Buffs #7).
## 2026-06-21 - Relic editor: Add-Effect box height + description-from-effects

Hypothesis:
(a) The Add-Effect box is too tall because of a fixed-height inner scroll; (b) relic descriptions ignore custom effects.

Finding: Both True.

Evidence:
- NRelicEditorPopup AddEffectGroup wrapped the effect editor in a ScrollContainer with CustomMinimumSize=(0,560), forcing 560px even when empty, nested INSIDE the editor's own outer scroll (scroll->settings->_effectGroupsContainer) -> double scrollbar + clip.
- TryBuildCustomDynamicDescription (patched into RelicModel.get_DynamicDescription) only used overrideData.CustomDescription; ExtraEffects (List<RelicEffectEntry>) never fed into any description.

What Changed:
- Height: dropped the inner ScrollContainer; effectsContainer added straight to groupRoot, so the box sizes to content (compact empty) and the outer scroll handles overflow.
- Description: new CardEditorRelicOverrides.BuildEffectsDescriptionText (groups ExtraEffects by trigger, formats each via CardEditorExtraEffects.FormatSingleEffectLine on the canonical proxy card, prefixes a trigger phrase). Wired in-game via a Postfix on get_DynamicDescription (appends to the original unless custom text is set) and in the editor preview via RefreshPreviewFromUi (reads BASE description under a [ThreadStatic] SuppressEffectDescriptionAppend flag, then appends the live UI effects from CollectEffectGroupEntries).
- Live preview: the embedded host's QueuePreviewRefresh early-returns for embedded hosts; added EmbeddedEffectsChanged?.Invoke() there + an onEffectsChanged param to InitializeAsEmbeddedEffectHost, so every effect edit calls the relic editor's RefreshPreviewFromUi. Plus explicit refresh on trigger-change and group-remove, and a re-entrancy guard.

Reason:
FormatSingleEffectLine needs only a proxy CardModel (no combat), making relic effect text a clean reuse of the card text builder.

Next Step:
In-game test (add a relic effect, confirm the description updates live in the editor and on the in-game relic); continue backlog (Osty HP scaling, #8, #7).
## 2026-06-21 - Osty's Cards filter (#8) + Copy Buffs (#7)

Hypothesis:
Both can reuse existing patterns rather than new subsystems.

Finding: True (both).

Evidence / What Changed:
- #8 Osty's Cards: card filters match via MatchesCountCardEffectFilter, each case checking HasDynamicVar(card,"X") || HasExtraEffectKind(Kind). Added CardExtraEffectCountCardFilter.ActsThroughOsty=40 -> HasDynamicVar(card,"OstyDamage") || HasExtraEffectKind(CardExtraEffectKind.OstyAction), so it catches BOTH vanilla Osty cards (OstyDamage var) and edited cards with an Osty-action effect. Dropdown auto-populates from Enum.GetValues; added label "Osty's Cards" + prefix "Osty" + the contribute/amount case. 5 sites.
- #7 Copy Buffs: discovered an existing CopyDebuffsFromTarget effect (CardExtraEffectKind.CopyDebuffs=130) that clones a source creature's PowerType.Debuff powers onto destination creatures. Mirrored it as CopyBuffs=143 filtering PowerType.Buff (which includes Strength/Dexterity/Focus stat-powers, so "Buffs/Stats" is covered). 7 sites: enum, effect template ("Copy Buffs / Stats", AllowedTargets AllAllies/Self/OtherEnemies/AllEnemies, default AllAllies), 2 text-dispatch + FormatCopyBuffs, execution case + CopyBuffsFromTarget method, NCardEditorPopup no-amount grouping.

Reason:
Filter matching and the copy-powers execution already existed; both features were extensions, not new machinery.

Next Step:
In-game test: (a) a count/trigger using the "Osty's Cards" filter; (b) a card with "Copy Buffs / Stats" targeting an ally with Strength/etc. Remaining backlog: orb-value inheritance #1, per-enemy "Target Itself" #4, draw-conditionals #5, Hang-style debuff #6, relic QoL.
## 2026-06-21 - Hang debuff (#2 done) + Target-Itself/#3 + Draw-conditionals/#4 scope

#2 Mark/Hang debuff: DONE + deployed.
- New CardEditorMarkedDamage.cs: per-combat registry (ConditionalWeakTable<object combatState, Dictionary<Creature,int>>) + a Postfix on Hook.ModifyDamage gated on modifyDamageHookType==All && props.IsPoweredAttack(), multiplying __result by (1 + 0.5*stacks). Pattern modeled on VulnerablePower.ModifyDamageMultiplicative; chosen the hook-Postfix route to avoid a new PowerModel + its icon/registration machinery.
- New effect kind CardExtraEffectKind.ApplyMarked=144 ("Mark (attack vulnerability)"): enum + template + execution case (AddMark per resolved target) + FormatMarked text in both dispatches.
- KNOWN LIMITATION: the mark has NO visible status icon (it is registry-only, not a real PowerModel). To add a visible debuff later, make a dedicated CardEditorCustomStatusPower-style power that overrides ModifyDamageMultiplicative, or extend CardEditorCustomStatusPower with a damage-amp field + the status-editor UI.

#3 "The Target Itself" (per-target value source) - SCOPE (NOT built):
- The effect amount is resolved ONCE in ResolveConfiguredEffectAmount -> ResolveValueSourceAmount (CardEditorExtraEffects.cs ~26969/26990): ResolveValueSourceCreatures -> GetValueSourceAmount per creature -> AggregateValueSourceAmounts. Then each effect kind applies that single amount to ALL its targets.
- To add a CardExtraEffectValueSourceActor.EachTarget that uses each affected target's OWN stat, the amount must be re-resolved per-target INSIDE each kind's apply loop (DealDamage, ApplyStatus, etc.) - there is no central per-target application hook. This is per-effect-kind execution rework on combat-critical paths.

#4 Draw-based conditionals (#5) - SCOPE (NOT built):
- Pillage/Escape-Plan/Expertise/Scrawl-style: a new control-flow effect that draws until a condition then branches on the drawn card. New effect kind + a draw loop + a condition check + a branch. Medium-large.

Next Step: #3 and #4 are best implemented in a fresh session with full context (combat-critical, multi-kind). #1 (relic every-N) and #2 (Mark) are shipped + deployed this session.
## 2026-06-21 - #3 (Target Itself) + #4 (Draw-until) BUILT cautiously (supersedes the "NOT built" note above)

Both shipped + deployed. Built defensively (opt-in / additive) so existing effects are byte-for-byte unaffected.

#3 "The Target Itself" = CardExtraEffectValueSourceActor.EachTarget (=5):
- KEY SAFETY: fully opt-in. Gated entirely on ValueSourceActor==EachTarget, which no existing effect uses (default Self). Zero change to existing behavior.
- Mechanism (no per-kind rework): a [ThreadStatic] Creature? _eachTargetCurrent. ExecuteEffect, when ValueSourceActor==EachTarget && _eachTargetCurrent==null, fans out: for each ResolveTargets(effect.Target), sets _eachTargetCurrent and RE-RUNS ExecuteEffect on the SAME effect, then returns. During the sub-run, ResolveTargets returns _eachTargetCurrent (top-of-method short-circuit) and the EachTarget value source returns _eachTargetCurrent -> the existing single-target path applies the effect to that creature using that creature's own stat. branchDepth+1 caps runaway; sub-run can't recurse (gate requires _eachTargetCurrent==null). Dropdown auto-includes it (Enum.GetValues); aggregation correctly stays disabled (not a group actor).
- So "deal damage to ALL enemies = each enemy's own Doom" now works. (Single-target already worked via ValueSourceActor.Target.)

#4 Draw-conditionals (subset) = CardExtraEffectKind.DrawUntilHandSize (=145):
- Additive new effect kind ("Draw Until Hand Has N"): draws needed = amount - hand.Count via the existing DrawMatchingCards primitive (which the game caps at hand-full/deck-empty). Covers Expertise/Scrawl.
- NOT YET: Escape-Plan-style "draw 1, branch on the drawn card's type" (the conditional-on-drawn half). Cleanest future approach: a draw + a branch gated by a "drawn-this-effect matches filter" check. Deferred to keep #4 bounded/safe.

Net session shipped (all deployed, 4 locations, hash-verified): relic every-N (#1), Mark/Hang debuff (#2/#6), EachTarget (#3/#4), DrawUntilHandSize (#4/#5) - plus earlier: Stars Spent, Turn#/Enemies/Osty-HP scaling, Osty's Cards filter, Copy Buffs, relic editor height + relic description-from-effects.
## 2026-06-21 - The two #2/#4 limitations are now FIXED (supersedes the "known limitation" notes above)

(1) Mark is now a VISIBLE power. Replaced the registry+Hook.ModifyDamage approach with CardEditorMarkedPower : PowerModel (CardEditorMarkedDamage.cs). It overrides ModifyDamageMultiplicative exactly like VulnerablePower (gate target==Owner && props.IsPoweredAttack(); return 1 + 0.5*Amount). Visibility via additive Harmony Prefixes on PowerModel.get_Icon/get_BigIcon/get_HoverTips that fire ONLY for CardEditorMarkedPower (borrows VulnerablePower's icon; shows "Marked" + a tooltip). PowerCmd.Apply<T> needs no ModelDb registration (custom-status precedent). ApplyMarked now does PowerCmd.Apply<CardEditorMarkedPower>(...). Persists for combat (no tick-down), shows a status pip + amount + tooltip + predicted-damage bump.

(2) Escape-Plan draw-and-check = CardExtraEffectKind.DrawAndCheck (=146): "Draw, Then Branch If Drawn Type". Draws `amount` cards via DrawMatchingCards, checks the ACTUAL drawn cards against effect.BranchCountCardType (MatchesGeneratedCardType), and if any match, runs the effect's branch (GetUsableBranchEffect) AFTER the draw - so it sees the just-drawn card (the existing branch evaluates the condition BEFORE the main effect, which is why DrawCards+branch couldn't do this). Excluded from the normal pre-effect branch (shouldRunBranch += effect.Kind != DrawAndCheck). Branch UI is per-effect (BranchTickbox), so no kind-gating needed. Recursion bounded by a [ThreadStatic] _drawAndCheckBranchDepth (< 5) that also feeds ExecuteEffect's MaxBranchDepth.
  - Build it: DrawAndCheck (amount=1) + tick Branch, set the branch card type = Skill, branch effect = Gain Block => Escape Plan.

All deployed (4 locations, hash-verified). Session total: 14 features/fixes.
## 2026-06-21 - Triaged ChatGPT's "Potential bugs.md" (23 items)

Verified against code (not taken at face value). Outcome:

FIXED this pass (real, in my recent work):
- #17 relic TriggerEveryN dropped by Clone() - TRUE/P1. Clone() copied EffectTriggers but not TriggerEveryN; Set() clones before saving => every-N silently reverted to "every time". Fixed: Clone() now copies TriggerEveryN.
- #21 EachTarget double-consumes use limits - TRUE/P2. Fan-out dispatch sits after TryConsumeEffectUseLimit, so outer + each sub-call consumed. Fixed: sub-calls (_eachTargetCurrent != null) skip consumption (outer pays once). (Sub-point: ResolveValueSourceReferenceText still lacks an EachTarget case -> minor card-text mismatch, not fixed.)
- #20 Copy Buffs clones hidden infra powers - TRUE/P2. CopyBuffsFromTarget cloned ALL PowerType.Buff incl. the mod's invisible behavior/tracker powers (Copy Debuffs was safe since infra powers are Buffs). Fixed: added the CleanseRemainingPowersByType exclusion (CardEditorCustomStatusPower || not-mod-assembly).

STALE: #23 DrawAndCheck build break - was a transient compile error in my mid-work tree; already fixed (thread-static depth guard); build is green.

CONFIRMED REAL, pre-existing, NOT yet fixed:
- #15 OnDamageDealt fires on fully-blocked hits - VERIFIED: Hook_AfterDamageGiven_CardEditorRelicEffects_Patch ignores DamageResult. Same class: #10 OnDamageTaken, #11 OnBlockGained/OnHeal (zero gain), #12 OnEnemyKilled (any death, not "you kill"), #16 OnTurnEnd (ignores participant list). The relic reactive-trigger patches don't filter by effective amount/killer/participants.
- #7 Copy Debuffs no-ops in common cases (user-REPORTED) + #19 Copy Buffs same source-target design flaw: source = ResolveSingleTarget (random fallback on no-target cards), destinations exclude the source => no-op in 1-enemy combat / unclear source UX.
- #6 Hits-All-Enemies grant soft-lock (reported + detailed static path), #13 OnPickup never dispatches, #18 every-N not described in generated text, #22 MarkedTarget watcher ignores stored manual targets.

LIKELY FALSE: #1 proxy card "never registered" - relics demonstrably work; the lookup falls back to safe defaults (Attack/AnyEnemy) per the code comment.
RISK (not broken today): #5 StarsSpent hooks private CardModel.SpendStars - works, brittle across updates.
PLAUSIBLE, need in-game repro: #2, #3, #8, #9, #14.
REPO HYGIENE (not a mod bug): #4 root .sln pulls in .tmp_*.cs scratch files; the card_editor project itself builds clean.
## 2026-06-21 - Fixed the relic reactive-trigger over-firing class (#10/#11/#15/#16)

Verified hook signatures in game Hook.cs first, then guarded each relic patch in CardEditorRelicEffects.cs:
- #15 OnDamageDealt (AfterDamageGiven): added DamageResult results param + require results.UnblockedDamage > 0 (no longer fires on fully-blocked attacks). FIXED.
- #10 OnDamageTaken (AfterDamageReceived): added DamageResult result param + require result.UnblockedDamage > 0 (no longer fires on fully-blocked hits). FIXED. (Caveat: vanilla skips this hook on lethal, so a killing blow still can't fire it - a vanilla limitation.)
- #11 OnBlockGained (AfterBlockGained): added decimal amount param + require amount > 0 (no longer fires on 0-block events). FIXED.
- #16 OnTurnEnd (AfterTurnEnd): added IEnumerable<Creature> participants param; now WrapForTarget per actual participant instead of Wrap-to-all-players (correct in extra-turn / MP flows). FIXED.

DEFERRED (hook limitations, flagged to user):
- #12 OnEnemyKilled: Hook.AfterDeath(IRunState, ICombatState?, Creature, bool, float) has NO killer/dealer param, so "you killed" cannot be derived here. Proper fix needs a per-target last-damage-dealer registry fed from AfterDamageGiven, checked in AfterDeath. Doable but adds machinery; left for a follow-up.
- #11 OnHeal: AfterCurrentHpChanged delta semantics (requested vs effective) are unverified; left the existing delta > 0 check rather than change working behavior on an unverified claim.

All built clean + deployed (4 locations). Session bug-fix tally: 3 (my regressions: #17/#20/#21) + 4 (relic over-fire: #10/#11/#15/#16) = 7 real bugs fixed.
## 2026-06-21 - #12 OnEnemyKilled now attributes the kill
Verified game CreatureCmd.Damage: Hook.AfterDamageGiven fires for the lethal hit (line 279) BEFORE Kill()/AfterDeath (298); DamageResult.WasTargetKilled flags the killing hit. Added a _lastLethalDealer ConditionalWeakTable<Creature,Creature>; AfterDamageGiven records (target->dealer) when WasTargetKilled; AfterDeath now ConsumeLethalDealer(creature) and fires OnEnemyKilled via WrapForTarget(killer) only when killer.Player != null. Result: fires only for the player who dealt the killing blow (not all players, not on poison/scripted/enemy-vs-enemy deaths). Caveat: kills with no captured direct-damage dealer (e.g. pure poison) don't fire; Osty kills depend on Osty.Player resolving. Built clean + deployed.
## 2026-06-22 - Fixed remaining real bugs in order (#7/#19, #6, #13, #18, #22)

Hypothesis: the five remaining list items are genuine and individually fixable without touching the working paths.
Finding: True for all five.
Evidence + fixes (CardEditorExtraEffects.cs / CardEditorRelicOverrides.cs / NRelicEditorPopup.cs / CardEditorExtraEffectPower.cs):
- #7 Copy Debuffs no-op: RequiresManualEnemyTarget now also forces a manual enemy pick for Kind==CopyDebuffs (both override + granted predicate occurrences), so the player designates the source enemy instead of a random fallback. FIXED (2+ enemy combat; 1-enemy is inherently a no-op since destinations exclude the source).
- #19 Copy Buffs source flaw: CopyBuffsFromTarget source changed from ResolveSingleTarget (random enemy on no-target cards) to `cardPlay.Target ?? ownerCreature` - explicit card target if any, else self. Deterministic, no random. FIXED.
- #6 Hits-All-Enemies grant soft-lock: added HitsAllEnemies to the SupportsGrantToCard exclusion list, so the retargeting modifier can no longer be granted onto a vanilla single-target card (which would run OnPlay with Target==null and stick centered). Custom AllEnemies cards use native targeting. FIXED.
- #13 OnPickup never dispatches: OnPickup (RelicTriggerKind=4, "When obtained") has zero runtime dispatch (relic-obtained fires out of combat; the effect engine needs a CombatState). Normalized it on load - ParseEffectEntries maps OnPickup->OnCombatStart, ParseTriggers drops it - so legacy/imported data attaches to a trigger that actually runs instead of being silently dead. FIXED (defensive; OnPickup isn't editor-selectable so no live data expected).
- #18 every-N not in generated text: GetTriggerDescriptionPhrase gained an everyN param + Ordinal() helper ("(every 3rd time)"); BuildEffectsDescriptionText gained an everyN map and applies it per trigger; the saved-override caller passes overrideData.TriggerEveryN and the live preview passes a new shared CollectTriggerEveryN() (same EveryNSelect.Selected+1 the save path persists). FIXED.
- #22 MarkedTarget watcher ignores manual targets: refactored the manual-target predicate into EffectNeedsManualEnemyTarget, which now ALSO returns true for power effects whose PowerTriggerFrom==MarkedTarget (so a no-target card prompts for the marked enemy). CaptureWatchedTarget now falls back from cardPlay.Target to TryGetManualTarget when the former is null. FIXED.
Reason: each was a confirmed code-path gap; fixes are opt-in/narrow (no change to existing non-MarkedTarget targeting, non-OnPickup triggers, or targeted Copy cards).
Next Step: in-game verification of the five behaviors; build clean (0 errors), deployed + hash-verified to all 3 DLL targets. Session real-bug tally now 12.
## 2026-07-02 - Offline investigation: "multiplayer broke after the newest update" (9.0)

Hypothesis: the 9.0 release (tag release-20260620) introduced an MP regression; rival hypothesis = the Jun-19 game update (v0.107.1, buildid 23811903) broke the mod.
Finding: Partially true (mod 9.0 is the dominant cause via several distinct mechanisms; "game update broke the mod's bindings" REFUTED; the game update contributes noise).
Evidence: 17-agent offline audit (read-only, all shipped code read at release-20260620, NOT the working tree). Verified chain:
- REFUTED: game-update broke Harmony bindings. f648a94 rebuilt the mod against the post-update sts2.dll 40 min after the update (bundled build/net9.0/sts2.dll blob == live game dll byte-identical); all patched MP symbols exist unchanged; SP works. No game update after Jun 20.
- JOIN-BLOCK (probable bulk of reports): the game's JoinFlow has 3 gates - version, gameplay-mod list (id+"-"+version), ModelDb hash (JoinFlow.cs:82-100). Mod manifest version is FROZEN across releases ("2.0"/card_editor.json "7.7"), so the mod-list gate can't catch mixed mod versions - but the ModelDb hash gate DOES: 9.0 added CardEditorRelicProxyCard (the ONLY concrete AbstractModel subtype change e4a655e->9.0), so an 8.x + 9.0 pair is REJECTED AT JOIN with a cryptic "ModelDb hash mismatch"/VersionMismatch error. Pre-9.0 out-of-sync updating did not block joins; with 9.0 it does = "we can't play together since the update". Corollary: the LOCAL Jun-22 deployed dll adds CardEditorMarkedPower (PowerModel:AbstractModel) => the dev build cannot join shipped-9.0 peers either.
- SAME-VERSION mechanisms shipped in 9.0 (f648a94's own message admits "MP features still need a real 2-peer test"): (a) L1 ready-gate defers the client's real SetReady; the NEW game build drops late lobby messages (_isBeginningRun guards) => deferred SetReady can be silently ignored = stuck lobby (only occurs 9.0-on-new-build - true regression pair); (b) 8s snapshot timeout lets a client enter a run UNSYNCED, then L2 run-freeze DISCARDS all mid-run snapshots => stale definitions locked in => host-side per-action XxHash32 checksum kick (StateDivergence, hard kick, no resync) the first time edited content acts; (c) new relic reactive-trigger system (16 hooks added same-day as release) with the known over-fire bugs + choice-opening effects awaiting GameAction.CompletionTask inside wrapped hooks (queue stress/hang); (d) new host toggle "Disable Desync Protection" prefixes out ChecksumTracker.CompareChecksums => users who enable it convert kicks into silent state corruption.
- NOISE: vanilla v0.107.1 throws MP exceptions unmodded (relic-pick, MapDrawing InvalidOperationException); a live StateDivergence kick was captured locally Jul 1 with the mod DISABLED (CARD.GOLD_AXE play, checksum 444709782 vs 1058123555) - some reports may be the game itself.
- EVIDENCE GAPS: godot log rotation kept only Jun-22+ sessions, ALL with the mod disabled - no direct log of the breakage; shipped 9.0.rar (Jun 20) != local deployed dll (Jun 22).
Reason: multiple independently verified mechanisms all point at the 9.0 window; the join-block is deterministic and matches the "after the update" wording; the same-version paths are consistent with mid-run kick reports.
Next Step (pending user go-ahead): 9.1 hotfix = bump manifest version EVERY release (turns the cryptic hash error into a clear "Mod mismatch card_editor-9.0 vs 9.1" message), include the 12 working-tree fixes, harden L1 timeout (never enter a run unsynced), don't discard mid-run snapshots silently, bump StateDto.Version with graceful handling; Nexus post: both players must run the exact same mod version; ask one reporter for %APPDATA%\SlayTheSpire2\logs\godot.log to discriminate join-block vs mid-run kick vs stuck lobby.