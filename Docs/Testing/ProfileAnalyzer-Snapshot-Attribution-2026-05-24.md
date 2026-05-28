# Profile Analyzer Snapshot Attribution - 2026-05-24

## Summary

This is an investigation note only. No gameplay behavior change is proposed here.

Baseline source:

- Capture: `ProfilerCaptures/2026TeamProject_J2M_2026-05-24_01-39-04.data` (local-only raw capture; not committed because the committed evidence is the extracted CSV summary)
- Extracted CSV evidence:
  - `ProfilerCaptures/profile-marker-summary-013904.csv`
  - `ProfilerCaptures/profile-parent-summary-013904.csv`
- Selected frames exported: 16
- Frames containing gameplay tick markers: 15
- `GameplayInputHost.RunSingleTickUnlocked()` count: 113
- `RunSingleTickUnlocked()` count/frame over selected range: 7.0625
- Deep Profile: ON, inferred from method-level managed markers and `Profiler.Callstack`
- Call Stacks: ON, inferred from `Profiler.Callstack` count 681501
- Warmup / scene load exclusion: not proven from the `.data` artifact; the selected range is short and includes one non-tick selected frame.
- Stage name / real stage identity: not recoverable from the capture artifact used here.

Important baseline caveat: the current worktree already contains uncommitted `PlayerControlStateLogic` same-state guard changes. The numbers below are therefore a current-dirty-worktree baseline, not a pre-guard baseline.

Single Profiler frame vs Profile Analyzer interpretation:

- A single Deep Profile frame is too sensitive to spike frame, GC, selected frame position, and artificial catch-up.
- This capture shows `RunSingleTickUnlocked()` running 113 times across 16 selected frames, so Deep Profile is an amplified structural workload, not an FPS claim.
- Profile Analyzer-style median / mean / count / count-frame is still useful for repeated bottleneck ordering.
- Real performance still needs a separate Deep Profile OFF capture.

## Key Conclusions

- Current average structural bottleneck is not one isolated `ProjectedWorld.CreateSnapshot` call-site. It is repeated snapshot creation plus snapshot-owned index first-copy.
- `ProjectedWorld.CreateSnapshot()` is called 1469 times in the selected range, but only 341 calls materialize. 1128 calls are cache hits.
- `SnapshotBuilder.Create()` is called 793 times: 341 projected materializations plus 452 authoritative tick snapshots.
- `WorldState.CreateSnapshot()` is called 906 times: the 793 above plus 113 direct `GameplayCompositionRoot.CreateSnapshot()` calls from host command admission snapshot refresh.
- Every `WorldState.CreateSnapshot()` builds both snapshot-owned cell indexes. The capture has 906 tile-feature index builds and 906 stacked-unit index builds.
- Ordered snapshot-local caches are working: entity ordered enumeration has 789 hits and 567 misses. The remaining sort cost is mostly first enumeration on newly created snapshots, not repeated sorting of the same snapshot.
- The next implementation priority should be snapshot/index reuse, not another narrow source-local guard, unless the immediate goal is a very small low-risk patch.

Recommended P1: snapshot-owned `tileFeatureIdsByCell` unchanged-index reuse, with dirty tracking and immutable/COW ownership rules.

Why:

- `WorldState.CreateSnapshotOwnedTileFeatureIdsByCell()` alone is 47.9828 ms median in Deep Profile and runs once per snapshot.
- It is rebuilt for authoritative snapshots, projected materializations, and host read snapshots even when tile features did not change.
- The tile-feature index is the larger part of `CreateSnapshotOwnedCellIndex()` cost.

Expected effect:

- Reduce the first-copy cost for snapshots with no tile-feature operation.
- Expected call reduction target: up to 906 tile-feature index builds in this selected range down to only tile-feature-dirty snapshots.
- Projected materialization count may remain 341, but each unchanged-tile materialization becomes cheaper.

Risk:

- TileFeature add/update/remove and activation transitions must correctly mark dirty.
- Snapshot immutability must not be weakened by sharing mutable `SortedSet<int>` or mutable dictionaries.
- Fast import and projected overlay paths need explicit ownership rules.

Required tests:

- `./run_tests.sh core`
- Targeted snapshot immutability tests for shared index reuse.
- TileFeature operation tests: add, update same cell, update moved cell, remove, activation/update with no cell move.
- ProjectedWorld fast import tests with tile-feature overlay and no-overlay paths.
- Determinism/hash comparison on a representative tick scenario.

Rollback condition:

- Any snapshot immutability failure, tile feature lookup drift, replay hash drift, or full-lane touched-cluster regression returns this change to current first-copy behavior.

Deferred:

- `PlayerControlStateLogic` same-state guard: still a good narrow quick win, but the current dirty worktree already contains it and the capture still shows snapshot-owned index cost as a larger remaining average cost.
- Ordered ids carry-through: promising, but order ownership and snapshot immutability risk are higher than tile-feature index reuse.
- TickResultBuilder empty/unchanged collection reuse: relevant for presentation allocation, but it does not address the dominant snapshot first-copy marker.

## Marker Summary

Values are milliseconds from the extracted selected range. GC alloc per marker was not available from the raw export; the capture does contain the aggregate `GC.Alloc` marker.

| Marker | Median | Mean | Count | Count/Frame | GC | Interpretation |
|---|---:|---:|---:|---:|---:|---|
| GameplayInputHost.RunSingleTickUnlocked | 465.0415 | 481.5407 | 113 | 7.0625 | n/a | Deep Profile catch-up amplified tick workload. |
| TickPipeline.RunTick | 291.5277 | 296.6884 | 113 | 7.0625 | n/a | Main gameplay simulation work. |
| GameplayTickViewPresenter.Present | 132.7534 | 143.6946 | 113 | 7.0625 | n/a | Presentation work is still high. |
| GameplayTickPresentationCoordinator.Present | 132.5688 | 143.4843 | 113 | 7.0625 | n/a | Mirrors presenter cost. |
| TickPipeline.RunPlanPhase | 101.8244 | 106.0686 | 113 | 7.0625 | n/a | Highest gameplay pipeline phase. |
| WorldState.CreateSnapshot | 90.1427 | 88.5352 | 906 | 56.6250 | n/a | All authoritative, projected-materialized, and host read snapshots. |
| SnapshotBuilder.Create | 78.8795 | 77.4144 | 793 | 49.5625 | n/a | 452 authoritative + 341 projected materializations. |
| ProjectedWorld.CreateSnapshot | 77.9841 | 79.0092 | 1469 | 91.8125 | n/a | 341 materialized, 1128 cache hit. |
| WorldState.CreateSnapshotOwnedCellIndex | 61.3388 | 59.9276 | 1812 | 113.2500 | n/a | Combined tile + stacked index first-copy path. |
| TickPipeline.RunResolvePhase | 56.3378 | 57.7646 | 113 | 7.0625 | n/a | Lower than snapshot/index cluster in this capture. |
| GameplayTickPresentationCoordinator.UpdatePresentation | 56.3294 | 55.1801 | 128 | 8.0000 | n/a | Presentation update cost. |
| ArraySortHelper`1.IntroSort | 54.5841 | 52.8464 | 11132 | 695.7500 | n/a | Includes snapshot ordered caches plus other sorts. |
| WorldState.CreateSnapshotOwnedTileFeatureIdsByCell | 47.9828 | 46.6943 | 906 | 56.6250 | n/a | Dominant cell-index first-copy component. |
| WorldSnapshot.EnumerateEntitiesOrdered | 40.1569 | 39.8196 | 1356 | 84.7500 | n/a | 567 misses, 789 hits. |
| SnapshotEntityLogicProvider.Build | 36.5839 | 36.2052 | 113 | 7.0625 | n/a | Initial snapshot consumer and ordered entity enumeration trigger. |
| TickResultBuilder.Build | 35.8171 | 36.2498 | 113 | 7.0625 | n/a | Presentation/result payload construction. |
| WorldSnapshot.BuildOrderedEntitiesCache | 28.6200 | 27.5630 | 567 | 35.4375 | n/a | First ordered entity enumeration per new snapshot. |
| TickPresentationDataBuilder.Build | 27.2580 | 28.1313 | 113 | 7.0625 | n/a | Payload sub-builder. |
| WorldState.CreateSnapshotOwnedStackedUnitsByCell | 13.4418 | 13.3180 | 906 | 56.6250 | n/a | Smaller cell-index component. |
| WorldSnapshot.EnumerateTileFeaturesOrdered | 8.6806 | 8.4596 | 678 | 42.3750 | n/a | 226 misses, 452 hits. |
| WorldSnapshot.BuildOrderedTileFeaturesCache | 6.3515 | 6.1158 | 226 | 14.1250 | n/a | First ordered tile-feature enumeration per new snapshot. |

## Snapshot Call-Site Attribution

| Call-site | Phase | Reason | Count/Frame | Materialized | CacheHit | CellIndexBuild | GC | Notes |
|---|---|---|---:|---:|---:|---:|---:|---|
| TickPipeline initial snapshot | Authoritative | Tick start | 7.0625 | n/a | n/a | 14.1250 | n/a | `SnapshotBuilder.Create(_worldState)` before entity logic build. |
| TickPipeline post-finalize snapshot | Authoritative | Finalize output | 7.0625 | n/a | n/a | 14.1250 | n/a | Cleanup input. |
| TickPipeline post-cleanup snapshot | Authoritative | Cleanup output | 7.0625 | n/a | n/a | 14.1250 | n/a | Respawn input. |
| TickPipeline final authoritative snapshot | Authoritative | Result/objective/presentation | 7.0625 | n/a | n/a | 14.1250 | n/a | Final result, objective, presentation, trace/hash input. |
| GameplayHostCommandAdmissionPolicy.RefreshCachedPresentationSnapshot | Host/UIAccess | committed read window | 7.0625 | n/a | n/a | 14.1250 | n/a | Direct `GameplayCompositionRoot.CreateSnapshot(_worldState)`, not via `SnapshotBuilder`. |
| ProjectedWorld.CreateSnapshot | Plan/Resolve/Damage | all projected reasons | 91.8125 | 21.3125 | 70.5000 | 42.6250 | n/a | 1469 calls: 341 materialized, 1128 cache hit. |
| ProjectedWorld materialization internals | Plan/Resolve/Damage | materialized only | 21.3125 | 21.3125 | 0 | 42.6250 | n/a | `WorldState.CreateFromSnapshotFast` + overlay apply + `SnapshotBuilder.Create`. |
| Respawn moon-block callback | Respawn | conditional | 0 observed | 0 | n/a | 0 | n/a | Non-projected `SnapshotBuilder.Create` total exactly matches 4 authoritative snapshots/tick. |

Projected reason call-site inventory from code:

| Call-site | Reason | Count pattern | Notes |
|---|---|---:|---|
| RunPlanPhase line 571 | PlanAfterEnemyAi | 1/tick | After before-movement AI batch. |
| RunPlanPhase line 591 | PlanAfterKinematicClosure | conditional | Only when same-face/enemy glide closure feature path runs. |
| RunPlanPhase line 611 | PlanAfterGravityField | conditional | Only if gravity field batch has operations. |
| RunPlanPhase line 644 | PlanPreMovementUtilityInput | 1/tick | Utility resolver input; current same-state guard can turn this into cache hit. |
| RunPlanPhase line 662 | PlanPostPreMovement | 1/tick | Plan snapshot after utility effects. |
| RunPlanPhase line 702 | PlanAfterPlayerActionAttempt | conditional | Only if player action attempt batch nonempty. |
| RunPlanPhase line 736 | PlanAfterPlayerFree2DLocomotion | feature-path 1/tick | Free2D path calls even when batch is empty; dirty only if writes exist. |
| CaptureTopologyActivationPreviousSnapshot lines 565, 585, 605, 637, 648, 696, 730 | same as source reason | conditional | Adds an extra projected snapshot before the first topology-source operation. |
| RunResolvePhase line 959 | ResolvePostMovement | 1/tick, plus remat branches | Movement-visible resolve surface. |
| RunResolvePhase lines 977, 1089, 1135, 1250 | ResolveEnemyActionBeforeAttackInput | 1/tick, plus remat branches | Enemy action before attack collection input. |
| RunResolvePhase line 985 | ResolveAttackSnapshot | 1/tick | Initial attack plan input. |
| RunResolvePhase lines 1168, 1257, 1273 | ResolveAttackRead | 1/tick, plus tile-effect branches | Final attack read surface before pending impacts. |
| RunResolvePhase lines 1378, 1390, 1399, 1406 | ResolvePostAttack | 4/tick | After attack, after enemy action, after AI, after utility. |
| CreateCompositeDamageProjectionSnapshot line 8951 | DamageProjection | only if damage projection has operations | Current capture records `RecordCompositeDamageProjection` once/tick, but projected materialization only when operations exist. |

Exact per-enum materialized/cache-hit counts are not emitted as profiler marker names in the current capture. The existing diagnostics records them in dictionaries during `BeginCapture()`, but Profile Analyzer only sees `RecordProjectedWorldMaterializedSnapshot()` / `RecordProjectedWorldCacheHit()` as aggregate methods.

## ProjectedWorld CreateSnapshot Breakdown

| Metric | Count | Count/Frame | Count/Tick |
|---|---:|---:|---:|
| ProjectedWorld.CreateSnapshot calls | 1469 | 91.8125 | 13.0000 |
| Materialized snapshots | 341 | 21.3125 | 3.0177 |
| Cache hits | 1128 | 70.5000 | 9.9823 |
| ApplyBatch calls | 2373 | 148.3125 | 21.0000 |
| Fast base snapshot imports | 341 | 21.3125 | 3.0177 |
| Fast overlay applies | 341 | 21.3125 | 3.0177 |
| SnapshotBuilder.Create inside projected materialization | 341 | 21.3125 | 3.0177 |
| Cell-index builds caused by projected materialization | 682 | 42.6250 | 6.0354 |

Interpretation:

- Call count is high, but most projected calls are cache hits.
- The expensive path is materialization: fast import, overlay apply, then `SnapshotBuilder.Create`.
- `ProjectedWorld.CreateSnapshot` remains important because each materialization triggers full `WorldState.CreateSnapshot` first-copy behavior.

## Cell Index First-Copy Attribution

| Reason / source | Tile cells | Tile ids | Stacked cells | Stacked ids | Build count | Reuse candidate | Risk |
|---|---:|---:|---:|---:|---:|---|---|
| All WorldState.CreateSnapshot | not recorded | not recorded | not recorded | not recorded | 906 tile + 906 stacked | Yes | Must preserve snapshot immutability. |
| Authoritative TickPipeline snapshots | not recorded | not recorded | not recorded | not recorded | 452 tile + 452 stacked | Partial | Authoritative state may mutate during finalize/cleanup/respawn. |
| Projected materialized snapshots | not recorded | not recorded | not recorded | not recorded | 341 tile + 341 stacked | Strong | Reuse possible when overlay has no relevant tile/stack operations. |
| Host command admission snapshots | not recorded | not recorded | not recorded | not recorded | 113 tile + 113 stacked | Strong | Read-window snapshot should share immutable indexes only. |

Current implementation fact:

- `WorldState.CreateSnapshot()` always calls `CreateSnapshotOwnedStackedUnitsByCell()` and `CreateSnapshotOwnedTileFeatureIdsByCell()`.
- `CreateSnapshotOwnedCellIndex()` copies every cell entry into a new `List<int>` and `ReadOnlyCollection`.
- There is no dirty check in this path, so tile-feature operation absence does not prevent tile-feature index rebuild.
- There is no dirty check in this path, so stacked-unit absence or unchanged stacked-unit state does not prevent stacked index rebuild.

Answer to the core question:

- The current cost is not only Plan materialization.
- Plan materialization contributes, but every `WorldState.CreateSnapshot()` pays the first-copy cost.
- Therefore snapshot call-site reduction and unchanged-index reuse are both relevant; the larger remaining target is first-copy reuse.

## Ordered Enumeration / Sort Attribution

| Consumer | Snapshot reason | Cache hit | Cache miss | Sort | Entity count | Candidate |
|---|---|---:|---:|---:|---:|---|
| All `WorldSnapshot.EnumerateEntitiesOrdered()` | mixed | 789 | 567 | 567 | not exported | Seed/carry ordered ids for new snapshots. |
| SnapshotEntityLogicProvider.Build | initial authoritative snapshot | mixed into totals | mixed into totals | first use likely miss | not exported | Could consume seeded ordered ids. |
| BuildPlayerFree2DLocalLocomotionPlans | plan snapshot | mixed into totals | mixed into totals | likely first use on plan snapshot if new | not exported | Lower priority than index reuse. |
| BuildEnemySameFaceKinematicLocomotionPlans | plan snapshot | mixed into totals | mixed into totals | likely cache hit if same plan snapshot already enumerated | not exported | Existing snapshot cache helps. |
| CleanupProcessor.Process | post-finalize snapshot | mixed into totals | mixed into totals | likely first use on post-finalize snapshot | not exported | Ordered seed could help. |
| TileFeatureEffectResolver.Resolve | postMovement / plan snapshots | mixed into totals | mixed into totals | depends on prior consumers | not exported | Needs parent/reason-visible diagnostics for exact split. |
| TickResultBuilder.Build | final authoritative snapshot | mixed into totals | mixed into totals | first final snapshot enumeration likely miss, later final uses hit | not exported | Existing local cache already benefits repeated final snapshot reads. |

Supporting numbers:

- Entity ordered enumeration: 1356 calls, 789 hits, 567 misses/sorts.
- Tile-feature ordered enumeration: 678 calls, 452 hits, 226 misses/sorts.
- `ArraySortHelper` total includes snapshot ordered cache sorts and other sort consumers. The parent summary attributes 793 `Array.Sort` calls and 569 `List.Sort` calls to the main observed sort families; the rest are nested/internal sort recursion markers.

Interpretation:

- Snapshot-local ordered caches are effective for repeated enumeration of the same snapshot.
- Remaining sort cost comes from first enumeration of newly created snapshots.
- Ordered ids carry-through would reduce this, but it has broader order ownership risk than cell-index reuse.

## Candidate Comparison

| Candidate | Expected impact | Size | Correctness risk | Required tests | Priority |
|---|---|---|---|---|---|
| A. PlayerControlStateLogic same-state guard | Reduces PlanPreMovementState dirty source and PlanPreMovementUtilityInput materialization in idle/same-state cases | Small | Low-medium; push/flip action transition timeline must remain intact | Core, PlayerMovementInput, push/flip transition tests | Deferred: already present in dirty worktree; still useful if not landed. |
| B. tileFeatureIdsByCell unchanged-index reuse | Reduces dominant tile index first-copy across unchanged snapshots | Medium | Medium; tile feature add/update/remove/cell move/activation dirty tracking | Core, tile feature operation tests, ProjectedWorld fast import, snapshot immutability | Recommended P1 |
| C. stackedUnitsByCell unchanged-index reuse | Reduces smaller stacked index first-copy | Medium | Medium; movement/spawn/despawn/stacking dirty tracking | Core, movement/spawn/despawn/stacking tests | P2 after tile index pattern is proven |
| D. ordered ids carry-through | Reduces first ordered cache sort on new snapshots | Medium-large | Medium-high; deterministic order ownership and immutability | Core, determinism/replay/hash, ordered enumeration immutability | P3 |
| E. TickResultBuilder empty/unchanged collection reuse | Reduces result/presentation allocation and build cost | Medium | Medium; replay/presentation contract drift | Core plus presentation-focused tests | P3/P4 |

## Next Implementation Recommendation

Recommended P1:

`tileFeatureIdsByCell` unchanged-index reuse for snapshot-owned cell indexes.

Why:

- It attacks the largest remaining repeated first-copy marker.
- It applies to authoritative, projected, and host read snapshots.
- It does not require reducing valid snapshot call-sites first.

Expected effect:

- Reduce `WorldState.CreateSnapshotOwnedTileFeatureIdsByCell` calls that do full copy work when tile features are unchanged.
- Reduce the `CreateSnapshotOwnedCellIndex` median share in the current capture.
- Leave behavior and call-site count intact initially, making rollback straightforward.

Risk:

- Incorrect dirty tracking can produce stale tile-feature lookup by cell.
- Mutable index sharing would break snapshot immutability.
- Projected overlay tile-feature operations must force a fresh index.

Required tests:

- `./run_tests.sh core`
- Snapshot immutability: old snapshot remains unchanged after world mutation.
- TileFeature add/update/remove/cell move lookup tests.
- ProjectedWorld no-overlay reuses unchanged index but overlay operation rebuilds.
- Replay/hash equality on representative scenarios.

Rejected / deferred:

- Same-state guard: quick win, but already in the current dirty baseline and does not remove global first-copy cost.
- Ordered ids carry-through: valuable but higher contract risk.
- TickResultBuilder collection reuse: useful after snapshot/index first-copy is reduced.
