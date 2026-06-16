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

- No `SummonBehaviorModule`.
- No `EnemyBehaviorModuleKey.Summon`.
- No `EnemySummonBehaviorModuleAsset`.
- No Utility asset migration.
- No Utility whole-lane migration.
- No GravityFieldAura migration.
- No RetiredLockNearbyBoxes migration/delete.
- No generic behavior registry.
- No `logicModuleAssets`.
- No direct `WorldState` spawn writes.
- No replay/export rename.

## 6. Validation

| Lane | Command | Result |
| --- | --- | --- |
| Summon scenario | `./run_tests.sh --integration-simulation --filter EnemyUtilitySummon_MultipleSpawns_ReserveCellsAndAllocateIdsInMaterializationOrder` | passed |
| Summon scenario | `./run_tests.sh --integration-simulation --filter EnemyUtilitySummon_` | passed, 10 tests |
| Replay | `./run_tests.sh --integration-replay --filter UtilitySummon` | passed, 2 tests |
| Replay | `./run_tests.sh --integration-replay --filter UtilityArchetypeSummon` | passed, 1 test |
| Runtime contract | `./run_tests.sh --integration-simulation --filter JPeterUtilitySummonRuntimeContractTests` | passed, 24 tests |
| Runtime contract | `./run_tests.sh --integration-simulation --filter KaliSummonedUnitRuntimeContractTests` | passed, 5 tests |
| Core | `./run_tests.sh core` | passed, EditMode 183 + PlayMode 33 |
| Static | `git diff --check` | reported passed for the working-tree diff |

Full lane was not run. Therefore do not claim project-wide green, full regression closure, or full lane green.

Review note: the clean working tree has no active `git diff --check` output. A review of the committed seam diff with `git diff --check HEAD^ HEAD` reports Unity `.meta` trailing whitespace in the two new `.meta` files. Treat that as a review-gate caveat unless it is corrected or intentionally accepted.

## 7. Merge Gate Checklist

- [ ] `EntitySpawnRequest` has no entity id.
- [ ] Entity ids are allocated in materializer after placement selection.
- [ ] Failed spawn attempts do not consume ids.
- [ ] `FinalizationBatch.SpawnEntity` remains the write path.
- [ ] `SurfaceCell(face, x, y)` is preserved.
- [ ] `SummonedEntityState` is preserved.
- [ ] `EnemyDefinitionBindingState` is preserved.
- [ ] Replay/event/export names are unchanged.
- [ ] `SummonBehaviorModule` is not introduced.
- [ ] `EnemyBehaviorModuleKey.Summon` is not introduced.
- [ ] Utility assets are not migrated.
- [ ] GravityFieldAura is untouched.
- [ ] RetiredLockNearbyBoxes is untouched.
- [ ] Targeted tests passed.
- [ ] Full lane status is explicitly recorded.

## 8. Follow-up Before Option B

Required before Option B:

- Duplicate Utility Summon vs Behavior Summon compiler guard design.
- BehaviorModule summon runtime state shape.
- Request ordering contract for multi-source behavior emitters.
- Source metadata vocabulary that is not Utility-only.
- Asset migration plan for SummonMinion authoring.
- Replay/export compatibility decision.
- Presentation/audio/VFX parity tests.
- Full lane / CI release gate.

Recommended additional tests:

- `EntitySpawnRequest` does not hold mutable runtime state that can drift before materialization.
- Placement parity for hazard risk fallback.
- Max-alive parity after detached/dead/non-occupying child states.
- Duplicate Utility Summon + future Behavior Summon fail-fast.
- GravityFieldAura unaffected regression.
- RetiredLockNearbyBoxes guard regression.

## 9. PR Summary

This extracts spawn/entity creation materialization from Utility Summon. It does not migrate Summon to BehaviorModule. Utility remains the trigger/timer owner. Entity ids are allocated only during materialization, after placement candidate selection succeeds, and `FinalizationBatch.SpawnEntity` remains the authoritative write path. Replay/export names are preserved. Targeted Summon/Utility/replay/core tests passed. Full lane was not run.
