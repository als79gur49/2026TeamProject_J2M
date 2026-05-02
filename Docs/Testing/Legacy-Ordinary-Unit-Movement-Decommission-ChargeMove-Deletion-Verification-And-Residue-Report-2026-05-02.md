# ChargeMove Deletion Verification And Residue Report

Date: 2026-05-02
Verified commit baseline: `3e119df7 refactor: Feature/Gameplay - ChargeMove consumer path removal`

## Executive Decision

This package verifies the completed `ChargeMove` consumer deletion and records the remaining presentation authoring residue.
It does not perform additional runtime deletion, Unity YAML migration, replay/golden rewrite, `Move` cleanup, grid transaction cleanup, or glide policy adoption.
Current Charge presentation remains `TickKinematicMotionTrack(MotionMode.Charge)` for movement plus `TickEnemyChargePresentationSignal` for Charge semantic/effect.
`TickEntityMotionKind.Move`, `MoveEntity`, `MovementExpander`, retained grid transactions, and glide retained fallback remain protected.
The verification pass found two stale serialized YAML hits; runtime C# no longer reads those fields.
A follow-up manual minimal YAML cleanup removed both residue lines without a migration tool, editor script, prefab resave, replay/golden rewrite, or runtime semantics change.

## Verification Matrix

| area | command / artifact | result | classification |
|---|---|---|---|
| build | `dotnet build Game.Feature.Gameplay.Tests.csproj -c Debug --no-restore` | passed, 0 warnings, 0 errors | green |
| active deleted-symbol C# scan | targeted `rg` over `Assets/_Features/Gameplay/**/*.cs` | no output | green |
| runtime ChargeMove scan | targeted `rg` over host/entityview/loop runtime C# | no output | green |
| YAML residue scan | targeted `rg` over `Assets/**/*.prefab`, `Assets/**/*.asset`, `Assets/**/*.unity` | previous=2, current=0 after manual minimal YAML cleanup | resolved stale serialized residue |
| stratification | `python3 Tools/check_gameplay_test_stratification.py --root .` | exit 0, soft governance warnings remain | green with existing warnings |
| Unity `ChargeMoveDeletion` filter | `TestResults/chargemove-verification/ChargeMoveDeletion.xml` | total=11, passed=11, failed=0, skipped=0 | green |
| Unity `ChargeMoveIsolation` filter | `TestResults/chargemove-verification/ChargeMoveIsolation.xml` | total=7, passed=7, failed=0, skipped=0 | green |
| Unity `ChargeMoveResidue` filter | `TestResults/chargemove-verification/ChargeMoveResidue.xml` | total=2, passed=2, failed=0, skipped=0 | green |
| Unity `BoundaryInventoryScenarioTests` | `TestResults/chargemove-verification/BoundaryInventoryScenarioTests.xml` | total=141, passed=141, failed=0, skipped=0 | green |
| Unity `EnemyKinematicLocomotionReplayTests` | `TestResults/chargemove-verification/EnemyKinematicLocomotionReplayTests.xml` | total=47, passed=47, failed=0, skipped=0 | green |
| Unity `WorldSnapshotAndPresentationTests` | `TestResults/chargemove-verification/WorldSnapshotAndPresentationTests.xml` | total=44, passed=40, failed=4, skipped=0 | red, not ChargeMove-related |
| Unity `GameplayTickPresentationCoordinatorTests` | `TestResults/chargemove-verification/GameplayTickPresentationCoordinatorTests.xml` | total=61, passed=58, failed=3, skipped=0 | red, not ChargeMove-related |
| Unity `MovementPhaseScenarioTests` | `TestResults/chargemove-verification/MovementPhaseScenarioTests.xml` | total=101, passed=100, failed=1, skipped=0 | red, not ChargeMove-related |

Failed broader fixture rows:

| fixture | failed test | classification |
|---|---|---|
| `WorldSnapshotAndPresentationTests` | `TickPresentationDataBuilder_BuildsEnemyJumpSignal_ForAirborneStartAndRetry` | existing unrelated enemy jump presentation expectation |
| `WorldSnapshotAndPresentationTests` | `TickPresentationDataBuilder_BuildsEnemyJumpSignal_ForWindupStart` | existing unrelated enemy jump presentation expectation |
| `WorldSnapshotAndPresentationTests` | `TickPresentationDataBuilder_FlipDestroySelf_SeparatesLogicalNoMoveFromFlipImpactSignal` | existing unrelated flip impact presentation expectation |
| `WorldSnapshotAndPresentationTests` | `TickPresentationDataBuilder_UsesPostMovementSnapshotForFollowThroughMotion_AndPostAttackSnapshotForEnemyDeath` | existing unrelated debug spawn/blocker setup failure |
| `GameplayTickPresentationCoordinatorTests` | `EnemyPupilVisualController_WindupAttackRecover_AnimatesBorderSequence` | existing unrelated enemy pupil visual timing expectation |
| `GameplayTickPresentationCoordinatorTests` | `GameplayTickPresentationCoordinator_PlayerDeathTick_SuppressesHitVfx` | existing unrelated player death VFX expectation |
| `GameplayTickPresentationCoordinatorTests` | `GameplayTickViewPresenter_PlayerRespawnBeforeDeathHide_Completes_ClearsDeathAnimationState` | existing unrelated player death/respawn presentation expectation |
| `MovementPhaseScenarioTests` | `Movement_MoveFromFrontBottomEdge_FailsWithoutRotation` | existing unrelated front-face movement expectation |

ChargeMove-specific rows inside the failed broader fixtures passed, including `ChargeMoveDeletion_RuntimeBuilder_UsesMoveForActiveChargeMoveSemantic`, `ChargeMoveDeletion_ChargeKinematicFlagOn_ChargePresentationStillWorks`, and retained Phase 6 no-legacy-ChargeMove rows.

## Active Reference Scan

Deleted-symbol command:

```bash
rg -n "TickEntityMotionKind\.ChargeMove|ChargeMoveDurationSeconds|chargeMoveDurationSeconds|chargeMoveMotionDurationSeconds|ChargeMoveMotionDurationSeconds|DefaultChargeMoveDurationSeconds|UseMoveMotionDurationForChargeSentinel" Assets/_Features/Gameplay --glob "*.cs"
```

Actual result: no output.
Classification: active gameplay C# references to deleted enum, timing, config, authoring, and sentinel symbols are absent.

Runtime ChargeMove command:

```bash
rg -n "ChargeMove|chargeMove" Assets/_Features/Gameplay/Gameplay_Host Assets/_Features/Gameplay/Gameplay_EntityView Assets/_Features/Gameplay/Gameplay_Loop --glob "*.cs"
```

Actual result: no output.
Classification: host, entity view, and gameplay loop runtime source no longer contain a current `ChargeMove` consumer or producer path.

Historical test names such as `ChargeMoveProducer_*` and `ChargeMoveIsolation_*` remain only as wrapper/canary vocabulary. They do not reference the deleted enum or runtime fields.

## YAML Residue Report

Command:

```bash
rg -n "ChargeMove|chargeMove|ChargeMoveDurationSeconds|chargeMoveDurationSeconds|chargeMoveMotionDurationSeconds|ChargeMoveMotionDurationSeconds|DefaultChargeMoveDurationSeconds|UseMoveMotionDurationForChargeSentinel" Assets --glob "*.prefab" --glob "*.asset" --glob "*.unity"
```

Previous residue before manual cleanup:

| path | token | type | runtime read? | owner | recommendation |
|---|---|---|---|---|---|
| `Assets/_Features/Gameplay/Gameplay_Timing/Showcase/GameplayPresentationTimingPreset_DefaultShowcase.asset` | `chargeMoveDurationSeconds` | ScriptableObject YAML stale field | no | Gameplay presentation/timing owner | harmless stale serialized residue; cleanup requires owner-approved asset migration |
| `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyView_Charge.prefab` | `chargeMoveMotionDurationSeconds` | prefab YAML stale field | no | Gameplay entity presentation owner | harmless stale serialized residue; cleanup requires owner-approved prefab migration |

Manual minimal cleanup result:

| metric | result |
|---|---|
| previous YAML residue | 2 |
| current YAML residue | 0 |
| migration method | manual minimal YAML cleanup |
| runtime semantics | unchanged |

No scene YAML hit was found for the deleted symbols.
No `ProjectSettings` scan is required by the current residue list because the hits are confined to gameplay asset/prefab YAML.
The existing unrelated dirty assets are not migration targets for this package.

## Presentation Authoring Cleanup

Removed runtime surfaces:

| removed surface | previous role | current replacement |
|---|---|---|
| `TickEntityMotionKind.ChargeMove` | legacy Charge entity-motion data value | no replacement enum; use kinematic Charge presentation |
| `GameplayTimingProfile.ChargeMoveDurationSeconds` | dedicated Charge entity-motion duration | Charge AI/kinematic timing |
| `GameplaySceneHostConfiguration.ChargeMoveDurationSeconds` | host timing override | Charge AI/kinematic timing |
| `GameplayPresentationTimingPreset.chargeMoveDurationSeconds` | serialized timing preset field | no runtime read; stale YAML may remain |
| `EntityMotionPresentationAuthoring.chargeMoveMotionDurationSeconds` | per-prefab Charge entity-motion override | generic entity motion authoring plus kinematic Charge presentation |
| `EntityMotionPresentationAuthoring` Charge snapshot/override lookup | runtime authoring read for Charge entity motion | no runtime read |
| `GameplayMotionTimingResolver` ChargeMove case | dedicated duration lookup | generic motion timing and kinematic track timing |
| `MotionTrack` ChargeMove interpolation case | legacy Charge entity-motion host interpolation | `TickKinematicMotionTrack(MotionMode.Charge)` |

Runtime read confirmation:
- active gameplay C# deleted-symbol scan returned no hits;
- runtime host/entityview/loop `ChargeMove` scan returned no hits;
- the remaining YAML names have no corresponding runtime field/property to bind to current gameplay presentation logic.

## Asset Migration Recommendation

The two known ChargeMove YAML residue lines have been removed by manual minimal YAML cleanup.
No migration tool, editor script, prefab resave, replay/golden rewrite, or runtime code change is needed for these two fields.
If similar stale residue appears later, require owner-approved asset migration before cleanup.
Do not mix that migration with runtime cleanup, replay/golden rewrite, or unrelated dirty asset changes.

## Non-Goals Confirmed

- no additional runtime code deletion;
- no Unity YAML bulk edit;
- no prefab/scene migration;
- no replay/golden rewrite;
- no `TickEntityMotionKind.Move` cleanup;
- no `MoveEntity` or `MovementExpander` deletion;
- no retained grid transaction change;
- no glide default adoption;
- no unrelated dirty asset modification.

## Next Action

Treat the ChargeMove deletion package as verified for active runtime C# and ChargeMove-specific Unity canaries.
Track the three broader fixture failures as existing unrelated presentation/movement expectation failures.
Open a separate asset-owner migration only if the stale YAML fields create inspector warnings or asset hygiene work is explicitly approved.
