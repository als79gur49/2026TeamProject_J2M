# Deferred Stale Ledger

This ledger tracks failures that are intentionally not being fixed in runtime code during the current `P0-B / P1` pass.

Rules
- Do not update tests in this ledger until the listed blocking semantic dependency is stable.
- Do not restore legacy runtime vocabulary to satisfy trace-only assertions.
- If the failure message or adjacent semantic assertions change shape, reclassify the item instead of carrying it forward unchanged.
- `Confirmed stale-only` means the authoritative commit/query/final-snapshot semantics are already green.
- `Semantic-trace hybrid` means the remaining trace token still exposes branch or ordering semantics and cannot be treated as pure wording drift yet.
- `Deferred classification` means the current failure still needs an upstream semantic checkpoint before it can move to either bucket.
- Rows promoted out of stale lanes should be removed from this ledger and tracked in the dated lane snapshot document instead.

Current operating plan
- Use [Remaining-55-Non-Runtime-Lane-Lock-Refinement-2026-04-18.md](./Testing/Remaining-55-Non-Runtime-Lane-Lock-Refinement-2026-04-18.md) as the current lane-lock execution plan for the open `55` rows.
- `stale-contract` execution is split into `stale-contract/public-contract` and `stale-contract/fixture-cleanup`.
- `stale-literal` execution is split into `stale-literal/trace-dump-serialized-literal` and `stale-literal/comparer-order-wording`.
- This ledger continues to track stale rows only. `consumer/view` and `topology/view/post-fx` rows stay in the dated lane snapshot and follow-up host/view backlog, not here.

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
  - `StageBackedGameplaySceneInstallerTests.GameplayBoxCapabilityLabelViewFactory_StaticBoxPrefab_KeepsCapabilityLabelDecorator` -> `topology/view/post-fx`
- Closed after query-contract recheck:
  - `MovementPhaseScenarioTests.Movement_ItemBox_LosesBoardPresenceBeforeCleanupRemoval` -> test expectation updated to current canonical `TryPickImpactTargetAt(...)` friendly-fallback contract; no runtime code change required

| Test Id | Current Classification | Evidence | Blocking Semantic Dependency | Recheck Step | Promote to Confirmed stale-only when | Promote to Active Regression when |
| --- | --- | --- | --- | --- | --- | --- |
| `MovementPhaseScenarioTests.Movement_Flip_SucceedsWhenOppositeCellIsFree` | `Confirmed stale-only` | Flip resolution and landing semantics are already locked; the failure now sits on the legacy `Kind=Flip` trace token only. | None during this phase. | `P3` | Flip resolution, landing cell, and cleanup semantics remain green while only the token name differs. | Flip branch selection, landing cell, or action timing semantics change. |
| `MovementPhaseScenarioTests.Movement_FlipInputOnItemFlipDestroyBox_UsesFlipBranch_WithoutConsumeOrDestroy` | `Confirmed stale-only` | Flip-over-item precedence is preserved in the current canonical runtime; the remaining failure is the old `Kind=Flip` token. | None during this phase. | `P3` | Final snapshot and action precedence stay green and only the trace token differs. | The scenario starts consuming or destroying instead of flipping, or final snapshot semantics drift. |
| `EnemyLogicTests.EnemyAiRuntimeDefinition_NegativeWindupTicks_ThrowsArgumentException` | `Confirmed stale-only` | `ParamName` surface now comes from composed runtime validation instead of the old aggregate constructor surface. | None in this phase; not part of `P0-B / P1`. | `P3` | Validation source remains the composed runtime parts. | Validation path or thrown parameter source changes again. |
| `EnemyLogicTests.EnemyAiRuntimeDefinition_NegativeMoveCooldownTicks_ThrowsArgumentException` | `Confirmed stale-only` | Same `ParamName` drift as above. | None in this phase. | `P3` | Validation source remains stable. | Validation surface changes again. |
| `EnemyLogicTests.EnemyAiRuntimeDefinition_NegativeDesiredChaseDistance_ThrowsArgumentException` | `Confirmed stale-only` | Same `ParamName` drift as above. | None in this phase. | `P3` | Validation source remains stable. | Validation surface changes again. |
| `EntityEffectPresentationAuthoringTests.*Player_S1.prefab*` | `Confirmed stale-only` | Player prefab path now resolves optional presentation authoring instead of requiring the previous root-component contract. | None in this phase; contract drift only. | `P3` | Optional-authoring contract remains stable. | Runtime starts requiring the old component again or prefab binding semantics drift. |
| `TickPipelineStructureCoreTests.*` | `Confirmed stale-only` | Composition/bootstrap signatures changed and tests still construct the old API shape. | None in this phase. | `P3` | Public composition API remains at the new signature. | Runtime API surface changes again. |
| `GameplayTimingOwnershipTests.PlayerAnimatorDriver_WithAnimatorOverride_PrefersAnimatorDurationOverResolvedMotionDuration` | `Confirmed stale-only` | Timing contract is phase-based now, so the old single-duration expectation is stale. | None in this phase. | `P3` | Phase-based timing contract remains stable. | Player animation timing semantics change again. |
| `StageBackedGameplaySceneInstallerTests.CombinedGameplayStage_BuildsJumpShowcaseProfileOverride` | `Confirmed stale-only` | Current serialized showcase asset values differ from the old literal expectation. | None in this phase. | `P3` | Asset values remain unchanged after upstream runtime fixes. | Showcase runtime starts depending on a different literal again. |
| `StageBackedGameplaySceneInstallerTests.CombinedGameplayStage_PlacesWallFollowerShowcaseEnemyAtConfiguredPatrolLane` | `Confirmed stale-only` | Same showcase asset literal drift. | None in this phase. | `P3` | Asset placement remains stable. | Stage placement semantics change again. |
| `StageRuntimeBuilderTests.StageRuntimeBuilder_CombinedShowcaseStageBuild_ReflectsCurrentConfiguredContract` | `Confirmed stale-only` | Same showcase asset literal drift. | None in this phase. | `P3` | Stage asset contract remains stable. | Stage asset contract changes again. |
| `StageRuntimeBuilderTests.StageRuntimeBuilder_Stage31Build_ReflectsVfxSfxStageContract` | `Deferred classification` | Current `stage-3-1.asset` has `BoxSpawns.Length = 4` while the test expects `12`; latest stage asset history includes level-01 stage composition updates. This is a stage asset contract issue, not Finding 1 presentation leakage. | Confirm whether the current Stage 3-1 asset design intentionally reduced BoxSpawns before changing tests or assets. | `P3` | Stage 3-1 design owner confirms the current asset is canonical and only the literal test expectation is stale. | BoxSpawns count impacts objective, VFX/SFX, pacing, or authored content intent and the asset must be restored. |
| `EnemyPrefabScaffoldTests.EnemyViewNonAttackingPrefab_UsesMoveOnlyLocomotionAuthoringAlongsideEnemyAnimationTiming` | `Confirmed stale-only` | Serialized prefab literal no longer matches the current asset value. | None in this phase. | `P3` | Prefab serialized values remain stable. | Prefab runtime binding semantics change. |
| `PlayerMovementInputTests.PlayerControlStateLogic_ActiveAction_AdvancesExecutionAndCompletion` | `Confirmed stale-only` | The test advances a target-less pending push that current runtime now invalidates before execute via `CanPendingActionStillExecute(...)`; the stale surface is the seeded internal state, not authoritative movement semantics. | None during this phase. | `P3` | Buffered-contact and timing-guard fixes remain green, and the row continues to differ only on the obsolete target-less action path. | The test starts failing together with valid-target `executionAttempted`, `nextMoveAllowedTick`, or pending-action clear regressions. |
| `PlayerMovementInputTests.PlayerLogic_ConfiguredTimingSnapshot_OnlyExecutesOnSnapshotExecuteTick` | `Confirmed stale-only` | The test seeds `targetEntityId=30` without a matching box in the snapshot; current runtime clears the action instead of executing against a missing target. | None during this phase. | `P3` | The row continues to fail only on the obsolete missing-target action assumption. | A valid-target execute-tick scenario starts failing on authoritative action timing semantics. |
| `MovementPhaseScenarioTests.Movement_SameDestination_OnlyHigherPriorityWins` | `Confirmed stale-only` | Higher-priority winner commit and final occupancy are already correct; the remaining failure is loser reject-reason/order wording. | None during this phase. | `P3` | The row continues to fail only on reject wording or local comparer surface. | Winner selection, committed destination, or final occupancy regresses. |
| `AttackInputNormalizationTests.ImpactReservationComparer_PreservesFaceBeforePlanarOrder` | `Confirmed stale-only` | No current failing downstream row shows accepted-winner or occupancy drift from this comparer surface; the remaining mismatch is isolated to ordering wording. | None during this phase. | `P3` | Downstream runtime rows stay green and the comparer test remains wording-only. | A downstream runtime row starts failing on winner selection, occupancy, or impact target from the same ordering surface. |
