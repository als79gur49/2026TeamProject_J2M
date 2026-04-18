# Remaining 56 Lane Reclassification 2026-04-18

Historical note:
- This snapshot predates the query-contract recheck for `Movement_ItemBox_LosesBoardPresenceBeforeCleanupRemoval`.
- That row has since been closed by updating the test to the current canonical `TryPickImpactTargetAt(...)` friendly-fallback contract.
- After the confirming rerun on `2026-04-18`, `./run_tests.sh full` moved from `944 total / 56 failed` to `944 total / 55 failed`.
- Treat this document as a pre-close snapshot rather than the latest open-failure ledger.
- The current operating plan for the still-open `55` non-runtime rows lives in [Remaining-55-Non-Runtime-Lane-Lock-Refinement-2026-04-18.md](./Remaining-55-Non-Runtime-Lane-Lock-Refinement-2026-04-18.md).
- Do not use this file as the current execution checklist for open backlog handling because it still preserves the historical `runtime authoritative bug = 1` snapshot and the pre-refinement step structure.

This document executes the approved reclassification plan against the current `./run_tests.sh full` checkpoint and locks each of the remaining `56` failed rows into one primary lane.

Current evidence:
- `TestResults/wsl-unity-full-editmode.xml`
- `Docs/DeferredStaleLedger.md`
- `Docs/Architecture/Tick-Simulation-Canonical-Spec.md`
- `Docs/Architecture/Gameplay-Rules-Appendix.md`
- `Docs/Architecture/ADR/ADR-001-Tick-Boundary-and-IR-Visibility.md`

Ground rules:
- Do not widen runtime/core changes during this phase.
- Do not restore old trace, dump, API, constructor, or component contracts.
- Do not mix `PresentationData` semantic mismatches with geometry/camera/projector/post-fx mismatches.
- Use one primary lane per failing row.

## Lane Counts

| Lane | Count | Notes |
| --- | ---: | --- |
| `stale-contract` | `14` | Old API shape, old validation source, old prefab/component contract, or obsolete seeded internal state assumptions. |
| `stale-literal / trace / comparer drift` | `19` | Authoritative state is already acceptable; failing surface is legacy token, dump, asset literal, or comparer/order wording. |
| `consumer/view` | `10` | `TickResult.PresentationData` or presenter-driver semantic output mismatch. |
| `topology/view/post-fx` | `12` | Geometry, camera, projector, scene graph, view-factory wiring, or post-fx mismatch. |
| `runtime authoritative bug` | `1` | Post-movement layered query semantics still disagree with current canonical runtime contract. |

## Early Adjudications

These rows were explicitly pulled forward instead of leaving them to a late generic review step.

- `PlayerMovementInputTests.PlayerLogic_ConfiguredTimingSnapshot_OnlyExecutesOnSnapshotExecuteTick`
  - Locked to `stale-contract`.
  - The test seeds an active push with `targetEntityId=30` but no matching box exists in the snapshot.
  - Current runtime invalidates pending push/flip actions when the target can no longer be resolved before execute.
- `PlayerMovementInputTests.PlayerControlStateLogic_ActiveAction_AdvancesExecutionAndCompletion`
  - Locked to `stale-contract`.
  - Same obsolete target-less pending push assumption as above; current runtime clears the action instead of advancing it.
- `MovementPhaseScenarioTests.Movement_SameDestination_OnlyHigherPriorityWins`
  - Locked to `stale-literal / trace / comparer drift`.
  - The failing assertion is the loser reject-reason surface, while the winner commit and final occupancy assertions already pass.
- `MovementPhaseScenarioTests.Movement_ItemBox_LosesBoardPresenceBeforeCleanupRemoval`
  - Locked to `runtime authoritative bug`.
  - The failure is not a trace token. It is a post-movement layered query mismatch on `TryPickImpactTargetAt(...)` after item detachment.

## Ledger Sync

- `Docs/DeferredStaleLedger.md` is now aligned with this snapshot for rows that were previously left as `Deferred classification` or `Semantic-trace hybrid`.
- `PlayerMovementInputTests.PlayerControlStateLogic_ActiveAction_AdvancesExecutionAndCompletion` and `PlayerMovementInputTests.PlayerLogic_ConfiguredTimingSnapshot_OnlyExecutesOnSnapshotExecuteTick` move from deferred review to `Confirmed stale-only`.
- `MovementPhaseScenarioTests.Movement_SameDestination_OnlyHigherPriorityWins` and `AttackInputNormalizationTests.ImpactReservationComparer_PreservesFaceBeforePlanarOrder` are now tracked as `Confirmed stale-only` comparer/order wording drift.
- `MovementPhaseScenarioTests.Movement_ItemBox_LosesBoardPresenceBeforeCleanupRemoval` and `CombinedGameplayShowcaseInstallerTests.GameplayBoxCapabilityLabelViewFactory_StaticBoxPrefab_KeepsCapabilityLabelDecorator` are intentionally removed from the stale ledger because their primary lanes are now `runtime authoritative bug` and `topology/view/post-fx`.

## Lane Lock

### `stale-contract`

- `Game.Feature.Gameplay.Tests.Core.TickPipelineStructureCoreTests.GameplayCompositionRoot_AndBootstrapper_ExposeExplicitGeneralAndPlayerTimingOverloads`
- `Game.Feature.Gameplay.Tests.Core.TickPipelineStructureCoreTests.MovementCommitter_ConsumesCanonicalPlayerControlTimingSnapshot`
- `Game.Feature.Gameplay.Tests.Core.TickPipelineStructureCoreTests.TickPipeline_CanBeExtendedWithInjectedEntityLogicProvider`
- `Game.Feature.Gameplay.Tests.Core.TickPipelineStructureCoreTests.TickPipeline_DoesNotExposeDefaultCompositionConstructors`
- `Game.Feature.Gameplay.Tests.Unit.EnemyLogicTests.EnemyAiRuntimeDefinition_NegativeDesiredChaseDistance_ThrowsArgumentException`
- `Game.Feature.Gameplay.Tests.Unit.EnemyLogicTests.EnemyAiRuntimeDefinition_NegativeMoveCooldownTicks_ThrowsArgumentException`
- `Game.Feature.Gameplay.Tests.Unit.EnemyLogicTests.EnemyAiRuntimeDefinition_NegativeWindupTicks_ThrowsArgumentException`
- `Game.Feature.Gameplay.Tests.Unit.EntityEffectPresentationAuthoringTests.GameplayPrefabs_EntityEffectPresentationAuthoring_CreateSnapshot_UsesRolloutDefaults("Assets/_Features/Gameplay/Gameplay_Entities/Runtime/Player_S1.prefab")`
- `Game.Feature.Gameplay.Tests.Unit.EntityEffectPresentationAuthoringTests.GameplayPrefabs_EntityEffectPresentationAuthoring_PassesPrefabValidationWithCloneSourceViewDefaults("Assets/_Features/Gameplay/Gameplay_Entities/Runtime/Player_S1.prefab")`
- `Game.Feature.Gameplay.Tests.Unit.EntityEffectPresentationAuthoringTests.GameplayPrefabs_HaveEntityEffectPresentationAuthoringOnRoot("Assets/_Features/Gameplay/Gameplay_Entities/Runtime/Player_S1.prefab")`
- `Game.Feature.Gameplay.Tests.Unit.GameplayTimingOwnershipTests.PlayerAnimatorDriver_WithAnimatorOverride_PrefersAnimatorDurationOverResolvedMotionDuration`
- `Game.Feature.Gameplay.Tests.Unit.PlayerMovementInputTests.PlayerControlStateLogic_ActiveAction_AdvancesExecutionAndCompletion`
- `Game.Feature.Gameplay.Tests.Unit.PlayerMovementInputTests.PlayerLogic_ConfiguredTimingSnapshot_OnlyExecutesOnSnapshotExecuteTick`
- `Game.Feature.Gameplay.Tests.Replay.TickReplayDeterminismTests.DeterminismHash_EnemyActionState_IsIncludedInCanonicalState`

Evidence summary:
- The `TickPipelineStructureCoreTests` failures are reflection and constructor-shape expectations against current public composition APIs, not live runtime regressions.
- The `EnemyLogicTests` failures are `ParamName` source drift from composed validation, not behavior regressions.
- The `Player_S1.prefab` failures are old required-root-authoring expectations; current runtime treats that authoring as optional on this path.
- The two `PlayerMovementInputTests` seed obsolete target-less pending push state that current runtime invalidates before execute.
- `DeterminismHash_EnemyActionState_IsIncludedInCanonicalState` seeds an enemy action state that current runtime does not preserve through the current AI/action state pipeline at tick `1`; this is a stale fixture/state-construction assumption, not a new authoritative regression.

### `stale-literal / trace / comparer drift`

- `Game.Feature.Gameplay.Tests.Unit.AttackInputNormalizationTests.ImpactReservationComparer_PreservesFaceBeforePlanarOrder`
- `Game.Feature.Gameplay.Tests.Unit.CombinedGameplayShowcaseInstallerTests.CombinedGameplayStage_BuildsJumpShowcaseProfileOverride`
- `Game.Feature.Gameplay.Tests.Unit.CombinedGameplayShowcaseInstallerTests.CombinedGameplayStage_PlacesWallFollowerShowcaseEnemyAtConfiguredPatrolLane`
- `Game.Feature.Gameplay.Tests.Unit.EnemyPrefabScaffoldTests.EnemyViewNonAttackingPrefab_UsesMoveOnlyLocomotionAuthoringAlongsideEnemyAnimationTiming`
- `Game.Feature.Gameplay.Tests.Unit.StageRuntimeBuilderTests.StageRuntimeBuilder_CombinedShowcaseStageBuild_ReflectsCurrentConfiguredContract`
- `Game.Feature.Gameplay.Tests.Replay.TickReplayDeterminismTests.Replay_CompositeItemAttackScenario_ProducesSameHashTraceAndEventLog`
- `Game.Feature.Gameplay.Tests.Replay.TickReplayDeterminismTests.Replay_ItemScenario_ProducesSameHashTraceAndEventLog`
- `Game.Feature.Gameplay.Tests.Replay.TickReplayDeterminismTests.Replay_PassiveContactScenario_ProducesStableHashTraceAndPlayerDamage`
- `Game.Feature.Gameplay.Tests.Replay.TickReplayDeterminismTests.Replay_PlayerControlState_DeterministicallyReflectsCustomAuthoritativeActionTiming`
- `Game.Feature.Gameplay.Tests.Replay.TickReplayDeterminismTests.Replay_PlayerControlState_IsIncludedInHashTraceAndReplayDump`
- `Game.Feature.Gameplay.Tests.Scenario.AttackPhaseScenarioTests.Attack_FireProjectileIntent_SpawnsProjectileDuringCommit`
- `Game.Feature.Gameplay.Tests.Scenario.MovementPhaseScenarioTests.Movement_Flip_SucceedsWhenOppositeCellIsFree`
- `Game.Feature.Gameplay.Tests.Scenario.MovementPhaseScenarioTests.Movement_FlipInputOnItemFlipDestroyBox_UsesFlipBranch_WithoutConsumeOrDestroy`
- `Game.Feature.Gameplay.Tests.Scenario.MovementPhaseScenarioTests.Movement_PushInputOnItemPushFlipBox_ResolvesAsItemBeforePushOrFlip`
- `Game.Feature.Gameplay.Tests.Scenario.MovementPhaseScenarioTests.Movement_PushInputOnItemPushFlipDestroyBox_ResolvesAsItemBeforePushFlipOrDestroy_AndPresentationUsesEntityExitOwnership`
- `Game.Feature.Gameplay.Tests.Scenario.MovementPhaseScenarioTests.Movement_PushInputPushBox_ContinuesAcrossBottomFrontSharedEdge`
- `Game.Feature.Gameplay.Tests.Scenario.MovementPhaseScenarioTests.Movement_PushInputPushBox_ContinuesAcrossFrontBottomSharedEdgeBackToBottom`
- `Game.Feature.Gameplay.Tests.Scenario.MovementPhaseScenarioTests.Movement_PushInputPushBox_StopsBeforeEntityBlocker_AndEntityTypeNoneWallRemainsValid`
- `Game.Feature.Gameplay.Tests.Scenario.MovementPhaseScenarioTests.Movement_SameDestination_OnlyHigherPriorityWins`

Evidence summary:
- The push/flip/item scenario rows still fail on legacy `Kind=...` tokens while current movement, cleanup, and final-snapshot semantics are already anchored elsewhere.
- `Attack_FireProjectileIntent_SpawnsProjectileDuringCommit` already shows the correct spawn event and final projectile entity; the remaining mismatch is the old trace dump shape.
- The replay `PlayerControlState` and `PassiveContact` rows fail on dump surface expectations such as `NextDamageAllowed=0` or old trace tokens, not on per-tick hash equality or final entity/event consistency.
- The showcase and stage rows are current asset literal drift, not runtime authoritative breakage.
- `Movement_SameDestination_OnlyHigherPriorityWins` already preserves the higher-priority winner and final occupancy; the surviving mismatch is the loser reject-reason/order wording surface.
- `ImpactReservationComparer_PreservesFaceBeforePlanarOrder` remains in the comparer/order drift lane because no current failing runtime row demonstrates an accepted-winner or occupancy regression downstream from this ordering surface.

### `consumer/view`

- `Game.Feature.Gameplay.Tests.Scenario.PlayerControlScenarioTests.PlayerControl_LocomotionPresentationSignal_StaysTrueDuringCooldownGapAndDropsWhenBlocked`
- `Game.Feature.Gameplay.Tests.Unit.GameplayTickPresentationCoordinatorTests.EnemyPupilVisualController_WindupAttackRecover_AnimatesBorderSequence`
- `Game.Feature.Gameplay.Tests.Unit.GameplayTickPresentationCoordinatorTests.GameplayTickPresentationCoordinator_PlayerDeathTick_SuppressesHitVfx`
- `Game.Feature.Gameplay.Tests.Unit.GameplayTickPresentationCoordinatorTests.GameplayTickViewPresenter_PlayerRespawnBeforeDeathHide_Completes_ClearsDeathAnimationState`
- `Game.Feature.Gameplay.Tests.Unit.GameplayViewProjectionTests.GameplayTickViewPresenter_Present_MapsEnemyAttackHitAndMoveSignalsToAnimatorDrivers`
- `Game.Feature.Gameplay.Tests.Unit.GameplayViewProjectionTests.GameplayTickViewPresenter_Present_PlayerActionSignals_HoldPushAndFlipUntilPresentationDurationExpires`
- `Game.Feature.Gameplay.Tests.Unit.WorldSnapshotAndPresentationTests.TickPresentationDataBuilder_BuildsEnemyJumpSignal_ForAirborneStartAndRetry`
- `Game.Feature.Gameplay.Tests.Unit.WorldSnapshotAndPresentationTests.TickPresentationDataBuilder_BuildsEnemyJumpSignal_ForWindupStart`
- `Game.Feature.Gameplay.Tests.Unit.WorldSnapshotAndPresentationTests.TickPresentationDataBuilder_BuildsMoveMotionForUnitMove`
- `Game.Feature.Gameplay.Tests.Unit.WorldSnapshotAndPresentationTests.TickPresentationDataBuilder_BuildsProjectileMoveMotionForProjectileMove`

Evidence summary:
- These rows fail on `TickPresentationData` semantics, semantic signal flags, motion-kind selection, or presenter-driver state mapping.
- They do not primarily claim that `WorldState`, cleanup removal, or final authoritative entities are wrong.
- `PlayerControl_LocomotionPresentationSignal_StaysTrueDuringCooldownGapAndDropsWhenBlocked` fails on locomotion signal timing, not on movement commit truth.
- The four `WorldSnapshotAndPresentationTests` failures are all `TickPresentationDataBuilder` semantic output mismatches.

### `topology/view/post-fx`

- `Game.Feature.Gameplay.Tests.Unit.CombinedGameplayShowcaseInstallerTests.CombinedGameplayShowcaseInstaller_ViewFactory_AttachesTimingAuthoringToConfiguredShowcaseEnemy`
- `Game.Feature.Gameplay.Tests.Unit.CombinedGameplayShowcaseInstallerTests.GameplayBoxCapabilityLabelViewFactory_StaticBoxPrefab_KeepsCapabilityLabelDecorator`
- `Game.Feature.Gameplay.Tests.Unit.GameplayShowcaseScaffoldTests.GameplayCameraRig_InitializeWithoutDirectCamera_DrivesOrbitHierarchyPose`
- `Game.Feature.Gameplay.Tests.Unit.GameplayViewProjectionTests.DefaultGameplayEntityViewFactory_StaticEnemyBindingWithNoneAiMode_UsesEnemyPrefab`
- `Game.Feature.Gameplay.Tests.Unit.GameplayViewProjectionTests.GameplayBoardSurfaceRenderer_TopologyTransition_StartWorldPosesMatchDestinationVisibleSurfacePoses`
- `Game.Feature.Gameplay.Tests.Unit.GameplayViewProjectionTests.GameplayBoardSurfaceRenderer_TopologyTransition_UsesDestinationVisibleFacesAtStart`
- `Game.Feature.Gameplay.Tests.Unit.GameplayViewProjectionTests.GameplayCubeProjector_ProjectsBottomFaceToHorizontalPlane`
- `Game.Feature.Gameplay.Tests.Unit.GameplayViewProjectionTests.GameplayCubeProjector_ProjectsFrontFaceToVerticalPlane`
- `Game.Feature.Gameplay.Tests.Unit.GameplayViewProjectionTests.GameplaySceneHost_CameraTarget_StaysOnCubeCenterDuringTopologyTransition`
- `Game.Feature.Gameplay.Tests.Unit.GameplayViewProjectionTests.GameplaySceneHost_TopologyTransition_CameraOrbitPreservesScreenContinuityAtStart`
- `Game.Feature.Gameplay.Tests.Unit.GameplayViewProjectionTests.GameplayTickViewPresenter_TopologyMotion_MaintainsBoardRootIdentity`
- `Game.Feature.Gameplay.Tests.Unit.TopologyTransitionPostFxTests.GameplaySceneHost_Initialize_TopologyTransitionPostFx_CreatesRuntimeVolumeCloneFromAuthoritativeAsset`

Evidence summary:
- These rows fail on projected pose, visible surface set, scene graph shape, prefab/view-factory composition, camera target/orbit continuity, rig pose, or runtime post-fx clone values.
- Their primary oracle is not `TickResult.PresentationData` semantic meaning. It is host/view geometry or scene wiring.
- `CombinedGameplayShowcaseInstaller_ViewFactory_AttachesTimingAuthoringToConfiguredShowcaseEnemy` and `GameplayBoxCapabilityLabelViewFactory_StaticBoxPrefab_KeepsCapabilityLabelDecorator` belong here because they are view-factory composition and prefab-renderer contract rows, not authoritative runtime rows.

### `runtime authoritative bug`

- `Game.Feature.Gameplay.Tests.Scenario.MovementPhaseScenarioTests.Movement_ItemBox_LosesBoardPresenceBeforeCleanupRemoval`

Evidence summary:
- This row fails on a post-movement layered query contract, not on trace wording.
- The failing assertion is `SnapshotAfterMovement.TryPickImpactTargetAt(..., sourceTeamId: 1)` returning `True` when the detached item box should no longer be selectable as the impact target on that cell.
- Keep this row isolated as the current minimum authoritative/runtime candidate until the post-movement query timing contract is rechecked directly.

## Step 2 and Step 3/4 Execution Notes

### Step 2-A locked rows
- All legacy `Kind=Push`, `Kind=Flip`, `Kind=Item`, spawn dump, replay dump, and showcase asset literal rows above are locked to the literal/trace lane.

### Step 2-B locked rows
- `ImpactReservationComparer_PreservesFaceBeforePlanarOrder`
- `Movement_SameDestination_OnlyHigherPriorityWins`

Current rationale:
- No surviving failing row currently demonstrates a downstream authoritative winner regression from these order surfaces.
- If a future rerun moves either row from wording/order surface to accepted-winner or final-occupancy breakage, promote it out of this lane immediately.

### Step 3 primary signal
- `TickResult.PresentationData` or presenter-driver semantic state is wrong first.

### Step 4 primary signal
- Geometry, camera, projector, scene graph, rig, prefab/view-factory, or post-fx state is wrong first.

### Tie-break rule
- If both lanes appear implicated, inspect `PresentationData` first.
- If `PresentationData` is already wrong, classify as `consumer/view`.
- If `PresentationData` is acceptable and the rendered/projected/scene result is wrong, classify as `topology/view/post-fx`.

## Follow-up Order

1. Maintain the current `stale-contract` and `stale-literal` rows as no-runtime-fix items.
2. Keep `consumer/view` and `topology/view/post-fx` as separate host/view backlogs.
3. Treat `Movement_ItemBox_LosesBoardPresenceBeforeCleanupRemoval` as the only currently retained `runtime authoritative bug`.
4. On the next full rerun, promote any row out of a stale lane only if its failure surface moves from wording/contract/view into authoritative snapshot or final-state mismatch.
