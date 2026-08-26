# Gameplay Wall Tick Cost Optimization Plan

- Status: Audited / implementation-ready by bounded slice
- Audit date: 2026-08-26
- Audited revision: `5d338c54a890bb5225ddda9d769f880846b8f1ca`
- Scope: item 2 only — reduce per-tick work caused by Wall and other non-participating entities
- Slice 1 execution document: [Gameplay Wall Tick Cost Optimization — Slice 1 Goal Plan](./Gameplay-Wall-Tick-Cost-Optimization-Slice1-Goal-Plan.md)

## 1. Decision

The optimization is feasible, but it must not be implemented as a global `EntityType.None` or Wall skip.

Wall remains an authoritative entity and Solid-lane blocker. The safe optimization target is redundant work around that entity: default Factory probing, repeated final-entity copies, full Cleanup scans, and live diagnostic serialization. Authoritative entity storage, Solid occupancy, ordered results, canonical hash, and canonical replay trace remain unchanged.

The audited slice verdict is:

| Slice | Verdict | Decision |
|---|---|---|
| Factory type prefilter cache | Approve | Implement as an internal conservative opt-in prefilter with per-`EntityType` ordered Factory lists |
| `FinalEntities` trusted owned read-only sharing | Approve | Implement first; retain defensive copying on general/internal enumerable constructors |
| Cleanup candidate indexes | Modify | Use three ordered derived indexes and preserve same-Cleanup timer-to-transition behavior |
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

### Slice 3 — Ordered Cleanup candidate indexes

Maintain three derived, entity-ID-ordered candidate sets:

- removal: `hp <= 0 || markedForDeath`;
- timer: `stateTimer > 0`;
- immediate transition: `stateTimer <= 0 && state is Acting or Cooldown`.

Update membership only at the authoritative entity mutation seams:

- spawn;
- remove;
- central stored-entity update;
- fast snapshot restore/import.

Cleanup execution remains:

1. process ordered removal candidates;
2. process ordered timer candidates, skipping removed entities and `spawnTick == tickIndex`;
3. collect IDs whose timer became zero;
4. ordered merge/dedupe those IDs with the pre-existing immediate-transition candidates;
5. emit every timer event before every transition event;
6. process the merged transition candidates using post-timer entity state.

The candidate sets must be carried through snapshot/fast restore without rebuilding them by an O(N) full scan during projected materialization. They remain derived internal indexes and are excluded from canonical hash and trace.

Add a Development/test invariant checker that compares index results with the current full-scan reference implementation. Keep the reference path until parity and performance evidence are established.

If index maintenance or snapshot carriage costs erase the measured benefit, retain the reference/full-scan implementation and do not merge the index structure merely for architectural appearance.

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
- pre-existing zero-timer transition;
- same-tick spawned entity timer is not decremented;
- entity that is both removal and timer candidate is removed only;
- Wall/`EntityType.None` damage, mark, state, timer, and removal behavior;
- removed entity auxiliary state and occupancy cleanup;
- timer event ordering before transition event ordering;
- ID ordering within each event family;
- candidate indexes equal the full-scan reference over deterministic fixtures and generated state matrices.

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

Use three paired baseline/candidate runs on the same revision window and machine configuration.

Structural gates:

- default Factory Wall probes: exactly zero after Slice 2;
- unscoped Custom Factory Wall probes: unchanged;
- created logic set and phase owner ordering: unchanged;
- Cleanup index/reference output: identical;
- EventLog, FinalEntities, occupancy, determinism hash, and FullCanonical trace: identical;
- trusted final-entity wrapper count: one on the internal Builder path.

Performance gates:

- total tick p95 regression no greater than 5%;
- no new sustained GC allocation regression in trace-off mode;
- when a targeted component marker or valid allocation sample exists, it must improve or at minimum remain non-regressed as specified by the child slice gate;
- when neither is available for a bounded micro-optimization, deterministic structural counters must prove the intended work removal, total tick p95 must remain within the non-regression gate, and the unverified allocation scope must be reported explicitly;
- LiveCompact must avoid full formatter/hash work and show a material trace-on cost reduction;
- deterministic removal of entity copies or default Factory probes is a valid measured benefit for the bounded Slice 1 micro-optimizations; when those structural gates pass, semantic parity holds, and p95 remains within the non-regression gate, wall-clock movement inside run-to-run noise does not by itself require rollback;
- if neither measured timing/allocation nor deterministic structural counters prove the targeted work removal, defer or revert the slice instead of retaining unproven complexity.

Do not combine historical performance artifacts from other revisions into a same-revision acceptance claim.

## 9. Rollback Boundaries

Each slice must remain independently revertible:

1. diagnostics/counters;
2. `FinalEntities` trusted sharing;
3. Factory type prefilter cache;
4. Cleanup candidate indexes;
5. diagnostics policy and LiveCompact sink;
6. static presentation provenance/cache;
7. snapshot partition, if ever approved.

Keep the Cleanup full-scan reference path behind a test/development comparison seam until the candidate index has parity and performance evidence. FullCanonical trace remains available regardless of LiveCompact rollout.

## 10. Commit Intent Split

Recommended commit intents:

1. `test: Gameplay - Wall tick optimization contracts add`
2. `refactor: Gameplay - Final entity result ownership share`
3. `refactor: Gameplay - Entity logic Factory type prefilter add`
4. `refactor: Gameplay - Cleanup ordered candidate indexes add`
5. `feat: Gameplay - Live compact tick diagnostics mode add`
6. conditional later commits for presentation cache or snapshot partition

Do not combine production refactors, diagnostics behavior, and performance evidence into one commit.

## 11. Final Readiness Verdict

Slices 1–4 are implementable after their tests-first guards are added. They preserve the authoritative world and optimize redundant participation, copying, and diagnostics work rather than redefining Wall semantics.

Static presentation caching requires provenance and revision carriers and is therefore conditional. Immutable/static snapshot partitioning is not approved for the first implementation series because its authority, restore, occupancy, ID reuse, and replay surface is substantially wider.

The first implementation milestone is complete only when:

- focused and core validation pass on the changed revision;
- FullCanonical output remains identical;
- current-HEAD paired measurements demonstrate component-level improvement where observable, or the approved bounded structural gates plus total p95 non-regression where component/allocation instrumentation is unavailable;
- no retained slice lacks either observable timing/allocation evidence or an approved deterministic structural reduction with p95 non-regression.
