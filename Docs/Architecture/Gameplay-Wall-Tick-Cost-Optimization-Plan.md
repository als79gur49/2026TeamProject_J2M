# Gameplay Wall Tick Cost Optimization Plan

- Status: Slice 1/2 complete; Slice 3 Goal/Prompt hardened, S3-A first and B/C hard-gated; later slices unchanged
- Audit date: 2026-08-27
- Audited revision: `25e623a94890f803142990fbeb7c2e11615b2ed0`
- Scope: item 2 only — reduce per-tick work caused by Wall and other non-participating entities
- Slice 1 execution document: [Gameplay Wall Tick Cost Optimization — Slice 1 Goal Plan](./Gameplay-Wall-Tick-Cost-Optimization-Slice1-Goal-Plan.md)
- Slice 3 execution document: [Gameplay Wall Tick Cost Optimization — Slice 3 Goal Plan](./Gameplay-Wall-Tick-Cost-Optimization-Slice3-Goal-Plan.md)
- Slice 3 execution prompt: [Gameplay Wall Tick Cost Optimization — Slice 3 Goal Prompt](./Gameplay-Wall-Tick-Cost-Optimization-Slice3-Goal-Prompt.md)

## 1. Decision

The optimization is feasible, but it must not be implemented as a global `EntityType.None` or Wall skip.

Wall remains an authoritative entity and Solid-lane blocker. The safe optimization target is redundant work around that entity: default Factory probing, repeated final-entity copies, full Cleanup scans, and live diagnostic serialization. Authoritative entity storage, Solid occupancy, ordered results, canonical hash, and canonical replay trace remain unchanged.

The audited slice verdict is:

| Slice | Verdict | Decision |
|---|---|---|
| Factory type prefilter cache | Approve | Implement as an internal conservative opt-in prefilter with per-`EntityType` ordered Factory lists |
| `FinalEntities` trusted owned read-only sharing | Approve | Implement first; retain defensive copying on general/internal enumerable constructors |
| Cleanup candidate indexes | Modify / conditional entry | Measure Cleanup attribution first, then isolate maintenance/carriage cost before enabling indexed execution |
| Live compact diagnostics | Modify | Add a separate mode; never replace canonical `TickTrace` or hash |
| Static presentation cache | Conditional | Require explicit static provenance, entity generation, and revisions first |
| Immutable/static snapshot partition | Defer | Reconsider only if profiling still identifies snapshot copying as a dominant cost |

No slice may treat `EntityType.None` as synonymous with immutable Wall.

## 2. Current Evidence

Current generated runtime stage content contains 2,073 initial entity placements across 13 stages:

- Wall: 954, or 46.0%
- Box: 982
- Enemy: 124
- Player: 13
- `stage-1-1`: 122 total, including 67 Wall entities
- maximum Wall count: `stage-0-1`, 179 Wall entities

`SnapshotEntityLogicProvider.Build` currently enumerates all entities and probes all four default dynamic Factories. Wall creates no default logic object, but the maximum Wall stage still causes 716 default `CanCreate` probes per tick, or 42,960 probes per second at 60 Hz.

That probe count is not expected to be the dominant cost. Larger Wall-related costs remain in:

- repeated `WorldState` snapshot entity and Solid occupancy copies;
- Cleanup ordered enumeration, survivor copying, timer scan, and transition scan;
- repeated `FinalEntities` materialization and copying;
- committed-frame and presentation reconstruction;
- full determinism hash serialization;
- full trace serialization of S0, S1, Final, and `TickResult.FinalEntities`.

In Editor and Development builds, full trace and determinism hash are currently enabled by default. Full trace formats every entity at least four times per tick and active occupancy in multiple sections. Therefore Factory filtering alone must not be reported as solving trace-on hitching.

## 3. Contract Classification

### 3.1 StrongContract

The following behavior is not changed by this plan:

- `WorldState` is the sole authoritative mutable gameplay owner.
- `WorldSnapshot` remains the read-only simulation/query seam.
- Wall remains an entity in the Solid occupancy lane.
- `SurfaceCell(face, x, y)` identity is preserved.
- entity ID ordering and Factory registration ordering are preserved.
- static logic keeps ownership precedence over dynamic logic.
- the first logic owner for the same entity and phase keeps precedence.
- an unscoped Custom `IEntityLogicFactory` continues to observe every entity type.
- Wall and `EntityType.None` may still be spawned, damaged, state-changed, marked, and removed through generic runtime seams.
- Cleanup order remains removal, then timer changes, then state transitions.
- every `CleanupPhaseResult` field, removal pose EventLog, and the resulting kinematic/continuous presentation tracks remain ordered and observationally identical.
- `FinalEntities`, EventLog ordering, occupancy, determinism hash, and FullCanonical trace contents remain observationally identical.
- partition/index/cache metadata is derived optimization state and is not added to the canonical hash.

### 3.2 CurrentPolicy

The following implementation choices may be changed while preserving the contracts above:

- probing every default Factory for every entity;
- rebuilding the same type-to-Factory candidate relationship every tick;
- defensively copying the same internally-owned final entity collection multiple times;
- scanning every surviving entity independently for removal, timer, and transition work;
- generating the full canonical trace for ordinary live logging.

Campaign Wall content being practically static is CurrentPolicy, not a runtime immutability contract.

## 4. Non-goals

- Removing Wall from `WorldState`, `WorldSnapshot`, occupancy, `FinalEntities`, hash, or replay.
- Changing traversal, placement, or settlement legality.
- Treating every `EntityType.None` as a stage-static Wall.
- Enemy tick throttling or inactive-face Enemy/Box optimization.
- Entity logic instance caching.
- Reordering entity-major or Factory-major execution.
- Changing the canonical Full trace schema.
- Introducing a delta-reconstructable replay format in the first implementation.

## 5. Final Implementation Plan

### Slice 0 — Measurement and contract baselines

Add development/test diagnostics for:

- entity count by `EntityType`;
- candidate Factory count by audited `EntityType` bucket;
- Factory opportunity, prefilter skip, actual `CanCreate`, created, accepted, and conflict-rejected totals by audited type bucket;
- created logic count;
- Cleanup full-scan and candidate counts;
- final-entity materialization/copy count;
- trace entity/occupancy row count and resulting text size;
- component time and GC allocation where the runtime profiler seam supports it.

The counters must be diagnostics-only and must not affect authoritative state, ordering, hash, or trace contents.

The Slice 1 Goal intentionally avoids per-Factory string/Dictionary aggregation in the hot path. Per-Factory attribution is deferred to a bounded explicit capture or profiler seam if aggregate/type-bucket evidence cannot explain a later regression; it is not required for Slice 1 completion.

Capture a current-HEAD baseline on a Wall-heavy stage with:

- diagnostics Off;
- FullCanonical diagnostics On;
- later, LiveCompact diagnostics On;
- three paired runs for each comparison.

The existing `gameplay-performance` evidence is historical, fixed to `stage-1-1`, trace-off, and has no configured budget or useful allocation sample. It may be cited only as historical context, not as this work's acceptance baseline.

### Slice 1 — Trusted owned read-only `FinalEntities` sharing

Current copies occur at:

1. final snapshot to ordered `List<EntityState>`;
2. `TickResultData` defensive copy;
3. `TickResult` defensive copy.

Change the internal Builder path to create one trusted read-only entity collection. `TickResultData` and the internal `TickResult` constructor may share that exact wrapper.

Requirements:

- keep general/internal `IEnumerable<EntityState>` constructors defensively copying input;
- never expose the mutable backing list or array;
- never return the backing storage to a pool while a `TickResult` can retain it;
- keep a previous tick's result immutable after subsequent ticks;
- keep the exact ordered contents seen by presentation, audio, VFX, replay, and hash consumers.

This is the first production slice because it is independent of Wall semantics and has the smallest contract surface.

### Slice 2 — Default Factory type prefilter cache

Do not add a member to the existing `IEntityLogicFactory`. Add a separate internal conservative prefilter contract:

```csharp
internal interface IEntityLogicFactoryEntityTypePrefilter
{
    bool MayCreateForEntityType(EntityType entityType);
}
```

The four default Factories declare:

- Enemy core: `EntityType.Unit`
- Enemy action: `EntityType.Unit`
- Enemy combat: `EntityType.Unit`
- Sliding box: `EntityType.Box`

At `SnapshotEntityLogicProvider` construction, build one candidate Factory list per known `EntityType` by filtering the original Factory list in its existing registration order.

Rules:

- a Factory without the prefilter is included for every entity type;
- an unknown future enum value falls back to the complete original Factory list;
- prefilter checks happen while building the per-type list, not once per entity per tick;
- do not use raw `EntityType` numeric values as flags (`None=0`, `Unit=1`, `Box=3`);
- do not cache entity ID eligibility, `CanCreate` results, profile bindings, AI mode, or logic instances;
- do not change `TryResolveEnemyGlidePresentationSettings` Factory ordering;
- do not apply the dynamic Factory prefilter to static logic.

This removes default Wall probes while preserving Custom Factory compatibility and first-owner ordering. It is a bounded micro-optimization, not the primary trace-on fix.

### Slice 3 — Ordered Cleanup candidate views

Slice 3 is not unconditionally implementation-ready. Its entry and close gates are owned by the separate [Slice 3 Goal Plan](./Gameplay-Wall-Tick-Cost-Optimization-Slice3-Goal-Plan.md), and its hard pauses, evidence procedure, and terminal states are fixed by the [Slice 3 Goal Prompt](./Gameplay-Wall-Tick-Cost-Optimization-Slice3-Goal-Prompt.md).

The 2026-08-28 Evidence Contract v4 remediation closed the known S3-A evidence false-PASS, strict-schema, lifecycle, and terminal-transport defects without running a new official capture or changing the production full-scan Cleanup executor. This is harness closure only: repository Slice 3 remains Hold pending an allocation-capable or approved-equivalent signal and a separate Measurement Goal.

If entry permission and an execution hard pause conflict, the stricter hard pause wins. A dated progress block does not override a normative gate unless it is explicitly marked as an approved normative amendment.

The semantic target is three derived, entity-ID-ordered candidate views:

- removal: `hp <= 0 || markedForDeath`;
- timer: `stateTimer > 0`;
- immediate transition: `stateTimer <= 0 && state is Acting or Cooldown`.

`SortedSet<int>` is a prototype option, not a contract. The retained representation must be selected from same-revision maintenance, snapshot-carriage, fast-import, timing, and allocation evidence.

Candidate membership changes only through the audited authoritative mutation seams:

- spawn;
- remove;
- central stored-entity update, with old/new predicate comparison before membership writes;
- fast snapshot restore/import.

Cleanup execution preserves these StrongContracts:

1. keep `CleanupProcessor.Process` as the base Cleanup direct-write entrypoint and introduce indexed execution only inside that boundary;
2. preserve the direct-write order `CleanupProcessor.Process` → box lock → aura field → pending reaction on one Cleanup write context;
3. process ordered removal candidates;
4. exclude removed IDs from every later Cleanup processor;
5. process ordered timer candidates, skipping `spawnTick == tickIndex` only for timer decrement;
6. retain the updated local entity state for IDs whose timer became zero;
7. ordered merge/dedupe those IDs with pre-existing immediate-transition candidates;
8. exclude removed IDs from the merged transition work;
9. emit every timer event before every transition event;
10. process transitions from post-timer local state, not the stale pre-Cleanup snapshot.

The candidate carrier must be snapshot-owned and immutable. It must not alias mutable `WorldState` storage, empty carriers must use shared storage, and projected fast import must not add a separate O(N) candidate-predicate scan beyond its existing entity copy. Candidate metadata remains derived internal state and is excluded from canonical hash and trace.

The Development/test reference oracle must be independent of the candidate carrier and indexed predicate implementation. It uses a pure operation plan by default. Any isolated-world supplemental check must reuse only the existing approved direct-write entrypoints; it must not add a production mutation entrypoint or execute both mutating processors against the same live write context. Reference comparison is explicit test/capture instrumentation, is off during official performance candidates, and fails fast on mismatch rather than silently falling back.

An empty-candidate fast path may skip only the base `CleanupProcessor` work. `RunCleanupPhase` must still run box-lock, aura-field, and pending-reaction expiry routines.

Slice 3 rolls out as three measured packages:

- S3-A: Cleanup/reference diagnostics, synthetic A/B/C schema validation, and actual A-only capture; B/C selection is unavailable and fail-closed;
- S3-B: candidate maintenance and immutable snapshot carriage plus actual A/B capture while production Cleanup still uses the full-scan executor;
- S3-C: indexed Cleanup execution, the base-processor empty fast path, and actual A/B/C capture.

S3-A-only measurement is preliminary attribution/calibration. S3-B has a pre-C correctness/maintenance-tax gate. S3-C first passes a provisional semantic/structural gate while ordinary production remains full scan; after explicit user approval, ordinary production selects indexed Cleanup directly and that final candidate revision must rerun focused/core/replay before the same-revision official A/B/C campaign decides B/C retention.

Defer the slice only when admitted, noise-valid timing/allocation evidence proves that S3-A does not attribute material cost to Cleanup. Incomplete admission, allocation liveness, noise, or workload signal is `Hold`, not defer. If S3-B maintenance/carriage cost or S3-C net result erases the measured benefit, reject the complete production index package and retain the full-scan implementation.

### Slice 4 — Diagnostics policy split

Introduce an explicit diagnostics policy:

```text
Off
LiveCompact
FullCanonical
```

- `Off`: no full trace and no full per-tick determinism hash.
- `LiveCompact`: does not invoke the full formatter or full hash builder. It may report tick/topology revision, counts, timing, allocation, event counts, and bounded sampled data through a separate sink/carrier.
- `FullCanonical`: preserves the existing `TickResult.Trace.Text`, full determinism hash, parser surface, and ordering exactly.

Requirements:

- test/replay composition explicitly selects `FullCanonical`;
- compact text is never placed into the existing canonical `TickTrace.Text` surface;
- dropped, sampled, or ring-buffered compact logs cannot affect simulation;
- delta replay reconstruction, static-table schemas, and periodic checkpoints are deferred.

This slice directly targets the user's trace-on hitch report while keeping replay evidence intact.

### Slice 5 — Static presentation provenance and cache, conditional

Do not cache presentation based on entity ID or `EntityType.None` alone. First introduce explicit stage-static presentation provenance and revisions:

- session identity;
- entity generation;
- static presentation revision;
- topology revision.

Only after those carriers exist may static face descriptors and poses be cached. The current frame must merge cached active-face static entries with dynamic entries after the presentation state store clears its per-frame committed maps.

Invalidate on:

- stage/session reset;
- topology rotation;
- static entity spawn/remove/move/facing/presence/type change;
- entity ID reuse;
- projector/profile reset.

This slice is conditional on profiling because it adds lifecycle state and invalidation responsibility.

### Slice 6 — Immutable base plus dynamic overlay snapshots, deferred

Do not implement this in the initial optimization series.

Re-open only when same-revision profiling after Slices 1–4 still shows authoritative snapshot copying as a dominant cost. A future design must provide:

- explicit stage-seeded static provenance;
- immutable base entities and Solid occupancy;
- mutable overlay, base tombstones, and per-ID generation;
- copy-on-write promotion when a base entity is damaged, moved, marked, or state-changed;
- dynamic treatment for runtime-spawned `EntityType.None` without static provenance;
- composite `TryGetEntity`, ordered enumeration, count, max-ID, and Solid semantic queries;
- fast/slow projected-world materialization parity;
- byte-identical FinalEntities, hash, occupancy, and FullCanonical trace.

Topology rotation changes visibility, not stored `SurfaceCell.face`; it must not rewrite the storage partition.

## 6. Tests-first Additions

### Factory scope tests

- unscoped Custom Factory still observes Wall/`EntityType.None`;
- scoped Factory does not receive unsupported types in `CanCreate`;
- mixed scoped/unscoped Factories preserve entity-major and Factory registration order;
- scoped filtering preserves first phase owner;
- default Factory scopes are supersets of all cases where `CanCreate` may return true;
- dynamic Wall spawn/remove is reflected on the next Build without stale entity eligibility;
- unknown `EntityType` values fall back to all Factories.

### `FinalEntities` tests

- general/internal enumerable constructor input mutation cannot mutate the result;
- a previous `TickResult` remains unchanged after later ticks;
- the trusted internal path uses one owned wrapper;
- presentation/audio/replay observe identical ordered values.

### Cleanup tests

- `timer=1` Acting/Cooldown reaches Idle in the same Cleanup;
- pre-existing zero- and negative-timer Acting/Cooldown transition;
- all current `EntityPhaseState` values and unknown casts preserve immediate-transition eligibility;
- same-tick spawned entity timer is not decremented;
- same-tick spawned zero-timer Acting/Cooldown entity still transitions;
- same-tick spawned dead or marked entity is still removed;
- entity that is both removal and timer candidate is removed only;
- entity that is both removal and immediate-transition candidate is removed only and emits no transition event;
- Wall/`EntityType.None` damage, mark, state, timer, and removal behavior;
- exact entity-owned auxiliary-state and occupancy cleanup matrix;
- independently-lived pending impact and emitted aura/derived-lock lifetime after source removal;
- removal pose carriers, pose-removal EventLog, and final kinematic/continuous presentation tracks;
- timer event ordering before transition event ordering;
- ID ordering within each event family;
- mixed Cleanup event log preserves removal-family, timer-family, transition-family, and later expiry ordering;
- candidate views equal an independent full-scan reference over deterministic fixtures and generated state matrices;
- mutation sequences cover damage, mark, state change, irrelevant entity updates, remove plus same-ID respawn, and multi-operation finalization;
- an older snapshot's candidate carrier remains immutable after later world mutations;
- fast import preserves all non-empty candidate groups without a separate O(N) predicate rebuild;
- strategy counters prove that the intended reference or indexed executor ran and that hidden fallback count is zero.
- final-candidate wiring guards prove that ordinary non-capture composition selects indexed Cleanup directly without a capture selector or hidden fallback.

### Diagnostics tests

- FullCanonical produces the existing canonical output byte-for-byte;
- replay parsers continue to consume FullCanonical output;
- LiveCompact does not call full formatter or full hash builder;
- Off/LiveCompact/FullCanonical produce identical authoritative final state and EventLog;
- logging sampling or dropped records never change determinism.

### Conditional presentation/snapshot tests

- inactive-to-active-to-inactive topology rotation for cached Wall presentation;
- removed cached Wall does not reappear for one frame;
- same-ID Wall-to-Box/Unit reuse has no stale presentation owner;
- fast/slow projected-world import parity;
- static Wall damage/move/remove and occupancy parity on every face;
- merged entity ordering and canonical hash/trace parity.

## 7. Validation Matrix

### Baseline executed during this audit

All commands ran through `./run_tests.sh` from this worktree. Unity was not invoked directly.

| Command | Result |
|---|---|
| `./run_tests.sh core --filter SnapshotEntityLogicProviderCoreTests` | EditMode 7 passed, 0 failed; no matching PlayMode tests |
| `./run_tests.sh full --filter 'CleanupPhaseScenarioTests;SnapshotBudgetGuardTests;WorldSnapshotAndPresentationTests'` | EditMode 130 passed, 0 failed; no matching PlayMode tests |
| `./run_tests.sh --integration-replay --filter TickReplayDeterminismTests` | EditMode 59 passed, 0 failed |

The wrapper detected and deleted generated temporary `InitTestScene` artifacts. Source mutation guard passed and the final Git diff was empty before this document was added.

These tests establish a usable current baseline. They do not prove the future optimization correct until the tests-first additions above are implemented.

### Required after each production slice

Run the relevant focused tests, then:

```bash
./run_tests.sh core
```

Additionally:

- Factory slice: filtered `SnapshotEntityLogicProviderCoreTests`.
- Cleanup slice: filtered `CleanupPhaseScenarioTests`, snapshot budget, and projected-world fast import tests.
- FinalEntities/presentation slice: filtered `WorldSnapshotAndPresentationTests` and relevant presentation coordinator tests.
- replay/hash/full trace changes: `./run_tests.sh --integration-replay --filter TickReplayDeterminismTests`.
- diagnostics policy changes: canonical trace golden/parity tests and trace-mode isolation tests.

The broad full baseline is documented as red and is not a substitute for touched-cluster evidence. Report focused results separately and do not make project-wide claims without the matching broad evidence.

## 8. Performance Acceptance Gates

Each child slice must pre-register its campaign order and material threshold before official runs. Slice 3 uses alternating A/B/C runs on the same revision window and machine configuration: three runs per state for a candidate-empty/Wall-heavy workload and three runs per state for a deterministic candidate-dense/mutation-heavy workload. Report A→B maintenance tax, B→C execution benefit, and A→C net benefit separately. Historical Slice 1 evidence cannot be substituted.

Structural gates:

- default Factory Wall probes: exactly zero after Slice 2;
- unscoped Custom Factory Wall probes: unchanged;
- created logic set and phase owner ordering: unchanged;
- Cleanup index/reference output: identical;
- Cleanup full-scan entity visits and survivor-copy counts are explicit;
- candidate visits, membership checks/adds/removes, snapshot-carried items, and fast-import items are explicit;
- projected fast import adds zero separate entity visits for candidate predicate rebuilding;
- invariant mismatch and production fallback counts are exactly zero;
- EventLog, FinalEntities, occupancy, determinism hash, and FullCanonical trace: identical;
- all `CleanupPhaseResult` fields, removal pose EventLog, and final kinematic/continuous presentation tracks: identical;
- trusted final-entity wrapper count: one on the internal Builder path.

Performance gates:

- total tick p95 regression no greater than 5%; this is a safety ceiling, not proof of benefit;
- no new sustained GC allocation regression in trace-off mode;
- when a targeted component marker or valid allocation sample exists, it must improve or at minimum remain non-regressed as specified by the child slice gate;
- when neither is available for a bounded micro-optimization, deterministic structural counters must prove the intended work removal, total tick p95 must remain within the non-regression gate, and the unverified allocation scope must be reported explicitly;
- LiveCompact must avoid full formatter/hash work and show a material trace-on cost reduction;
- deterministic removal of entity copies or default Factory probes is a valid measured benefit for the bounded Slice 1 micro-optimizations; when those structural gates pass, semantic parity holds, and p95 remains within the non-regression gate, wall-clock movement inside run-to-run noise does not by itself require rollback;
- if neither measured timing/allocation nor deterministic structural counters prove the targeted work removal, defer or revert the slice instead of retaining unproven complexity.

Slice 3 additionally requires:

- S3-A must measure both `CleanupProcessor` and the complete `RunCleanupPhase`, plus valid main-thread allocated bytes per tick or another approved allocation sample;
- S3-A preliminary calibration must freeze exact material/noise/B-tax/allocation thresholds before the first official run;
- S3-B pre-C must prove exact candidate parity, zero separate O(N) candidate rebuild, and preliminary whole-tick/allocation maintenance tax within its pre-registered ceiling; B does not require allocation 0 by itself;
- S3-C must reduce base Cleanup full-scan visits from N to zero on the target workload and preserve exact semantic/replay/presentation-carrier parity before the final campaign;
- final target workload A→C must meet the pre-registered `CleanupProcessor` component material-improvement threshold, complete `RunCleanupPhase` containment threshold, whole-tick end-to-end benefit threshold, and allocation safety gate;
- final dense/mutation stress workload does not require speedup, but must meet whole-tick `<= +5%` and its pre-registered allocation safety ceiling;
- the two workload results must not be averaged into one acceptance value;
- a missing valid allocation signal holds the Goal at S3-A and blocks S3-B/S3-C entry; an equivalent signal requires an explicit Goal amendment and user approval before a new S3-A recapture;
- target improvement only inside run-to-run noise, or a slower target A→C result masked by the `+5%` safety ceiling, is a reject/defer outcome rather than a speedup claim.

Do not combine historical performance artifacts from other revisions into a same-revision acceptance claim.

## 9. Rollback Boundaries

Each slice must remain independently revertible:

1. diagnostics/counters;
2. `FinalEntities` trusted sharing;
3. Factory type prefilter cache;
4. Cleanup S3-A diagnostics/reference seam;
5. Cleanup S3-B/S3-C production candidate package as one rollback unit;
6. diagnostics policy and LiveCompact sink;
7. static presentation provenance/cache;
8. snapshot partition, if ever approved.

Keep the Cleanup full-scan reference executor as the production path through S3-B. The independent comparison oracle remains test/capture-only after parity is established; do not retain a hidden production fallback that can mask stale-index defects. FullCanonical trace remains available regardless of LiveCompact rollout.

## 10. Commit Intent Split

Recommended commit intents:

1. `test: Gameplay - Wall tick optimization contracts add`
2. `refactor: Gameplay - Final entity result ownership share`
3. `refactor: Gameplay - Entity logic Factory type prefilter add`
4. `test: Gameplay - Cleanup reference oracle and contracts add`
5. `chore: Gameplay - Cleanup attribution diagnostics add`
6. `refactor: Gameplay - Cleanup candidate maintenance and carriage add`
7. `refactor: Gameplay - Cleanup indexed executor add`
8. `docs: Gameplay - Cleanup indexed execution evidence close out`
9. `feat: Gameplay - Live compact tick diagnostics mode add`
10. conditional later commits for presentation cache or snapshot partition

Do not combine production refactors, diagnostics behavior, and performance evidence into one commit.

## 11. Final Readiness Verdict

Slices 1 and 2 are complete under the Slice 1 Goal and its recovery closeout. Slice 3 has a valid target and may enter only S3-A under its dedicated Goal Prompt; S3-B/S3-C production work is not approved until the preceding hard gates pass. Slice 4 remains implementable after its tests-first guards are added. These slices preserve the authoritative world and optimize redundant participation, copying, and diagnostics work rather than redefining Wall semantics.

Static presentation caching requires provenance and revision carriers and is therefore conditional. Immutable/static snapshot partitioning is not approved for the first implementation series because its authority, restore, occupancy, ID reuse, and replay surface is substantially wider.

The first implementation milestone is complete only when:

- focused and core validation pass on the changed revision;
- FullCanonical output remains identical;
- current-HEAD paired measurements demonstrate component-level improvement where observable, or the approved bounded structural gates plus total p95 non-regression where component/allocation instrumentation is unavailable;
- no retained slice lacks either observable timing/allocation evidence or an approved deterministic structural reduction with p95 non-regression.

The structural-only exception above applies only to the already approved bounded micro-optimization gates. Slice 3 follows its dedicated Goal and cannot enter S3-B/S3-C without the required valid allocation signal or an equivalent signal approved by Goal amendment before a new S3-A recapture.
