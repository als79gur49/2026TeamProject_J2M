# Deferred Stale Ledger

This ledger tracks failures that are intentionally not being fixed in runtime code during the current cleanup pass.

Rules
- Do not update tests in this ledger until the listed blocking semantic dependency is stable.
- Do not restore legacy runtime vocabulary to satisfy trace-only assertions.
- If the failure message or adjacent semantic assertions change shape, reclassify the item instead of carrying it forward unchanged.
- `Confirmed stale-only` means the authoritative commit/query/final-snapshot semantics are already green.
- `Semantic-trace hybrid` means the remaining trace token still exposes branch or ordering semantics and cannot be treated as pure wording drift yet.
- `Deferred classification` means the current failure still needs an upstream semantic checkpoint before it can move to either bucket.
- Rows promoted out of stale lanes should be removed from this ledger and tracked in the dated lane snapshot document instead.

Current operating plan
- Active rows are limited to items that still need a semantic owner decision before they can be closed or promoted.
- Closed stale-only rows from the 2026-04-18 lane-lock pass are listed below for provenance only; they are no longer active ledger work.
- `consumer/view` and `topology/view/post-fx` rows stay in the dated lane snapshot and follow-up host/view backlog, not here.

Limited deferred gate
- Default target remains `Deferred classification = 0`.
- Deferred rows are allowed only for three ambiguity classes:
  - insufficient downstream winner/occupancy guard for comparer/order rows
  - mixed `PresentationData` vs geometry oracle
  - mixed public-contract vs fixture drift
- Maximum deferred carry: `3` rows total, one reason per row.
- Every deferred row must carry:
  - one current-caller control
  - one guard/control test near the suspected oracle
  - one minimum missing-oracle evidence capture
- Deferred rows are not runtime candidates. Runtime promotion still requires a current-caller authoritative snapshot/query/final-state mismatch.
- No deferred row should survive past the adjacent step recheck or the final verification pass.

2026-04-18 lane-lock sync
- Promoted out of stale tracking and into the dated lane snapshot:
  - `CombinedGameplayShowcaseInstallerTests.GameplayBoxCapabilityLabelViewFactory_StaticBoxPrefab_KeepsCapabilityLabelDecorator` -> `topology/view/post-fx`
- Closed after query-contract recheck:
  - `MovementPhaseScenarioTests.Movement_ItemBox_LosesBoardPresenceBeforeCleanupRemoval` -> test expectation updated to current canonical `TryPickImpactTargetAt(...)` friendly-fallback contract; no runtime code change required

2026-05-29 legacy/deprecated cleanup closeout
- Closed the old `Confirmed stale-only` rows out of active tracking. They remain historical evidence from the lane-lock cleanup and should not be used as current failure inventory.
- Closed `StageRuntimeBuilderTests.StageRuntimeBuilder_Stage31Build_ReflectsVfxSfxStageContract` because the current Stage 3-1 asset and test contract are already generalized/current: the test no longer carries the stale literal `BoxSpawns.Length == 12` expectation and derives runtime entity count from authored stage arrays.
- Remaining open row count: `0`.

Closed stale-only rows:
- `MovementPhaseScenarioTests.Movement_PushInputPushBox_ContinuesAcrossBottomFrontSharedEdge`
- `MovementPhaseScenarioTests.Movement_PushInputPushBox_ContinuesAcrossFrontBottomSharedEdgeBackToBottom`
- `MovementPhaseScenarioTests.Movement_PushInputPushBox_StopsBeforeEntityBlocker_AndEntityTypeNoneWallRemainsValid`
- `MovementPhaseScenarioTests.Movement_PushInputOnItemPushFlipBox_ResolvesAsItemBeforePushOrFlip`
- `MovementPhaseScenarioTests.Movement_PushInputOnItemPushFlipDestroyBox_ResolvesAsItemBeforePushFlipOrDestroy_AndPresentationUsesEntityExitOwnership`
- `Replay.TickReplayDeterminismTests.Replay_ItemScenario_ProducesSameHashTraceAndEventLog`
- `Replay.TickReplayDeterminismTests.Replay_CompositeItemAttackScenario_ProducesSameHashTraceAndEventLog`
- `MovementPhaseScenarioTests.Movement_Flip_SucceedsWhenOppositeCellIsFree`
- `MovementPhaseScenarioTests.Movement_FlipInputOnItemFlipDestroyBox_UsesFlipBranch_WithoutConsumeOrDestroy`
- `EnemyLogicTests.EnemyAiRuntimeDefinition_NegativeWindupTicks_ThrowsArgumentException`
- `EnemyLogicTests.EnemyAiRuntimeDefinition_NegativeMoveCooldownTicks_ThrowsArgumentException`
- `EnemyLogicTests.EnemyAiRuntimeDefinition_NegativeDesiredChaseDistance_ThrowsArgumentException`
- `EntityEffectPresentationAuthoringTests.*Player_S1.prefab*`
- `TickPipelineStructureCoreTests.*`
- `GameplayTimingOwnershipTests.PlayerAnimatorDriver_WithAnimatorOverride_PrefersAnimatorDurationOverResolvedMotionDuration`
- `CombinedGameplayShowcaseInstallerTests.CombinedGameplayStage_BuildsJumpShowcaseProfileOverride`
- `CombinedGameplayShowcaseInstallerTests.CombinedGameplayStage_PlacesWallFollowerShowcaseEnemyAtConfiguredPatrolLane`
- `StageRuntimeBuilderTests.StageRuntimeBuilder_CombinedShowcaseStageBuild_ReflectsCurrentConfiguredContract`
- `EnemyPrefabScaffoldTests.EnemyViewNonAttackingPrefab_UsesMoveOnlyLocomotionAuthoringAlongsideEnemyAnimationTiming`
- `PlayerMovementInputTests.PlayerControlStateLogic_ActiveAction_AdvancesExecutionAndCompletion`
- `PlayerMovementInputTests.PlayerLogic_ConfiguredTimingSnapshot_OnlyExecutesOnSnapshotExecuteTick`
- `MovementPhaseScenarioTests.Movement_SameDestination_OnlyHigherPriorityWins`
- `AttackInputNormalizationTests.ImpactReservationComparer_PreservesFaceBeforePlanarOrder`
