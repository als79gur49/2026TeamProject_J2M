# Enemy AI Summon Spawn/EntityCreation Seam Implementation Note

## 1. Decision Summary

- Option C is implemented.
- Utility Summon remains the trigger, timer, cooldown, windup, recovery, max-alive, and source-validation owner through `EnemyUtilityResolver` / `EnemyUtilityEffectState`.
- Spawn/entity creation materialization is extracted behind `EntitySpawnRequest` and `EntitySpawnMaterializer`.
- This is not a SummonBehaviorModule migration.
- Full lane was not run.

## 2. What Changed

| Area | Before | After |
| --- | --- | --- |
| Summon trigger owner | `EnemyUtilityResolver` / `EnemyUtilityEffectState` | unchanged |
| Spawn request | resolver-local materialization | `EntitySpawnRequest` |
| Entity id allocation | resolver path | materializer after placement candidate confirmed |
| Placement | resolver-local code | `EntitySpawnPlacementResolver` / materializer seam |
| Authoritative write | `FinalizationBatch.SpawnEntity` | unchanged |
| Metadata | `SummonedEntityState` / `EnemyDefinitionBindingState` | unchanged |
| Replay names | `SummonCommitted` / `SummonSkipped` | unchanged |
| BehaviorModule | none | still none |

## 3. New Seam Contract

- `EntitySpawnRequest` does not carry an entity id.
- Entity id allocation happens only during materialization.
- Materialization happens after deterministic request order is established.
- Utility trigger request order is established before materialization by
  `SourceEntityId`, `EffectIndex`, then `TriggerTick`.
- `EntitySpawnMaterializer` preserves received request order; it does not
  establish a separate sort policy.
- `EntitySpawnRequest` is a materialization request, not a live view of the
  summoner. Placement-affecting source data, source metadata, archetype binding,
  and deterministic event output are captured when the request is emitted.
- Failed spawn attempts do not allocate ids.
- Successful spawn attempts allocate ids in materialization order.
- `SurfaceCell(face, x, y)` identity must be preserved.
- `FinalizationBatch.SpawnEntity` remains the only authoritative write path for spawned entities.
- Replay/export-visible Summon names are intentionally preserved.

## 4. Preserved Behavior

- Source metadata is preserved through `SourceEntityId`, `SourceEffectIndex`, and `TriggerTick`.
- Candidate placement order is preserved.
- Reserved spawn cell behavior is preserved.
- Hazard placement keeps neutral-first selection and risk fallback behavior.
- `SummonedEntityState` is preserved.
- `EnemyDefinitionBindingState` is preserved.
- Determinism hash paths for `SummonedEntities` and `EnemyDefinitionBindings` are preserved.
- Presentation binding behavior is preserved.
- `SummonCommitted` and `SummonSkipped` event naming is preserved.

## 5. Explicit Non-Goals

- At the original Option C seam extraction point there was no `SummonBehaviorModule`, `EnemyBehaviorModuleKey.Summon`, or `EnemySummonBehaviorModuleAsset`; the later Option B compile-skeleton slice now adds them without changing this spawn seam.
- No Utility asset migration.
- No Utility whole-lane migration.
- No GravityFieldAura migration.
- No RetiredLockNearbyBoxes migration/delete.
- No generic behavior registry.
- No `logicModuleAssets`.
- No direct `WorldState` spawn writes.
- No replay/export rename.

## 6. Validation

### Initial Option C seam validation

| Lane | Command | Result |
| --- | --- | --- |
| Summon scenario | `./run_tests.sh --integration-simulation --filter EnemyUtilitySummon_MultipleSpawns_ReserveCellsAndAllocateIdsInMaterializationOrder` | previously recorded passed |
| Summon scenario | `./run_tests.sh --integration-simulation --filter EnemyUtilitySummon_` | previously recorded passed, 10 tests |
| Replay | `./run_tests.sh --integration-replay --filter UtilitySummon` | previously recorded passed, 2 tests |
| Replay | `./run_tests.sh --integration-replay --filter UtilityArchetypeSummon` | previously recorded passed, 1 test |
| Runtime contract | `./run_tests.sh --integration-simulation --filter JPeterUtilitySummonRuntimeContractTests` | previously recorded passed, 24 tests |
| Runtime contract | `./run_tests.sh --integration-simulation --filter KaliSummonedUnitRuntimeContractTests` | previously recorded passed, 5 tests |
| Core | `./run_tests.sh core` | previously recorded passed, EditMode 183 + PlayMode 33 |
| Static | `git diff --check` | passed for the relevant working-tree / PR hygiene checks |

Review note: the previous Unity `.meta` trailing whitespace caveat was resolved by the pre-merge hygiene cleanup. Current hygiene checks for the PR/worktree passed `git diff --check`, PR-diff whitespace checks, and touched-file trailing whitespace scans. Full lane was not run.

Full lane was not run. Therefore this note does not report broad project validation, full regression closure, or broad lane success.

### Follow-up characterization / guard validation

| Area | Evidence |
| --- | --- |
| Same-tick multi-summoner ordering | Characterized: Utility trigger intents sort by `SourceEntityId`, `EffectIndex`, `TriggerTick`; materializer preserves received order. |
| Mutable request payload drift guard | Guarded: `EntitySpawnRequest` uses captured `OriginCell`, `SourceFacing`, and `SourceTeamId`, not a broad live source entity payload. |
| Duplicate Utility/Behavior Summon guard design | Documented as Option B prerequisite. |
| BehaviorModule Summon runtime state shape design | Documented as Option B prerequisite. |
| Summon asset migration plan | Documented as Option B prerequisite. |
| Replay/export compatibility plan | Documented as Option B prerequisite; initial Option B preserves external replay/export names. |
| Presentation/audio/VFX parity plan | Documented as Option B prerequisite; initial Option B preserves current names/cue semantics. |

## 7. Implemented Contract Checklist

- [x] `EntitySpawnRequest` has no entity id.
- [x] Entity ids are allocated in materializer after placement selection.
- [x] Failed spawn attempts do not consume ids.
- [x] `FinalizationBatch.SpawnEntity` remains the write path.
- [x] `SurfaceCell(face, x, y)` is preserved.
- [x] `SummonedEntityState` is preserved.
- [x] `EnemyDefinitionBindingState` is preserved.
- [x] Replay/event/export names are unchanged.
- [x] Utility trigger ordering is established before materialization by `SourceEntityId`, `EffectIndex`, then `TriggerTick`.
- [x] `EntitySpawnMaterializer` preserves received request order and does not define a separate sort policy.
- [x] `EntitySpawnRequest` uses captured request metadata and is not a live summoner view.
- [x] Option C did not introduce `SummonBehaviorModule` or `EnemyBehaviorModuleKey.Summon`; the later Option B compile-skeleton slice introduces them without changing this seam.
- [x] Utility assets are not migrated.
- [x] GravityFieldAura is untouched.
- [x] RetiredLockNearbyBoxes is untouched.
- [x] Targeted tests and follow-up characterization evidence are recorded.
- [x] Full lane status is explicitly recorded as not run.

## 8. Option B Readiness Status

The following Option B prerequisite gates are now documented or characterized:

- Same-tick multi-summoner ordering characterization.
- Mutable `EntitySpawnRequest` payload drift guard.
- Duplicate Utility/Behavior Summon guard design; see [Enemy-AI-Summon-Duplicate-Guard-Design.md](./Enemy-AI-Summon-Duplicate-Guard-Design.md).
- BehaviorModule Summon runtime state shape design; see [Enemy-AI-Summon-Behavior-Runtime-State-Design.md](./Enemy-AI-Summon-Behavior-Runtime-State-Design.md).
- Summon asset migration plan; see [Enemy-AI-Summon-Asset-Migration-Plan.md](./Enemy-AI-Summon-Asset-Migration-Plan.md).
- Replay/export compatibility plan; see [Enemy-AI-Summon-Replay-Export-Compatibility-Plan.md](./Enemy-AI-Summon-Replay-Export-Compatibility-Plan.md).
- Presentation/audio/VFX parity plan; see [Enemy-AI-Summon-Presentation-Audio-VFX-Parity-Plan.md](./Enemy-AI-Summon-Presentation-Audio-VFX-Parity-Plan.md).
- Option B implementation slicing and validation gate plan; see [Enemy-AI-Summon-Option-B-Implementation-Plan.md](./Enemy-AI-Summon-Option-B-Implementation-Plan.md).

Remaining before Option B implementation:

- Runtime emission for Behavior Summon remains future work.
- Add Behavior Summon request ordering, request snapshot, max-alive, source invalidation, topology participation, replay/export, and presentation/audio/VFX parity tests.
- Add placement parity coverage for hazard risk fallback.
- Add max-alive parity coverage after detached, dead, and non-occupying child states.
- Add GravityFieldAura unaffected regression coverage.
- Add RetiredLockNearbyBoxes guard regression coverage.
- Run asset-scoped production migration only after Behavior implementation and parity gates pass.
- Decide and/or run full lane / CI release gate.

Non-goals still in force:

- No Utility whole-lane migration.
- No GravityFieldAura migration.
- No RetiredLockNearbyBoxes migration/delete.
- No generic registry or `logicModuleAssets`.
- No direct `WorldState` spawn writes.
- No replay/export rename without an explicit compatibility migration.
- No presentation/audio/VFX rename without an explicit asset/schema migration.

## 9. PR Summary

This extracts spawn/entity creation materialization from Utility Summon. It does not migrate Summon to BehaviorModule. Utility remains the trigger/timer owner. Entity ids are allocated only during materialization, after placement candidate selection succeeds, and `FinalizationBatch.SpawnEntity` remains the authoritative write path. Replay/export names are preserved. Targeted Summon/Utility/replay/core tests were previously recorded as passed. Full lane was not run.

This document now also acts as the Option B readiness index for Summon migration planning. The duplicate guard and compile skeleton have started; runtime emission, production asset migration, replay/export migration, and presentation/audio/VFX parity migration remain future gated work.
