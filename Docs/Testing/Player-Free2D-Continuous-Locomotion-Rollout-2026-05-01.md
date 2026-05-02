# Player Free2D Continuous Locomotion Rollout

Date: 2026-05-01

This rollout is guarded by `GameplayRuntimeFeatureFlags.EnablePlayerFree2DLocalLocomotion`.
Player Free2D Action Assist v1.1 is separately guarded by `GameplayRuntimeFeatureFlags.EnablePlayerFree2DActionAssist`, and that flag is effective only when `EnablePlayerFree2DLocalLocomotion` is also enabled.
When enabled, player ordinary movement uses `UnitContinuousLocomotionState` before the stoppable kinematic, same-face kinematic, and legacy discrete movement paths. Enemy ordinary movement, charge, jump, phase, forced motion, and existing fallback kinematic behavior remain on `UnitKinematicRuntimeState`.

## Validation Contract

- Applies only to player ordinary movement from `PlayerTickCommand.HeldMoveDirection`.
- Movement is 4-direction axis motion. Free2D-native nonzero local topology seam crossing is not implemented; local-zero settled topology moves may hand off to the retained topology grid transaction path, and topology-edge approach may first zero-settle to local-zero before that handoff.
- `EntityState.position` remains the semantic anchor cell for occupancy, contact, push, flip, action preview, spawn, respawn, and topology decisions.
- `UnitContinuousLocomotionState` stores deterministic fixed-point local offset, velocity, facing, last move direction, speed, mode, sequence, and residual remainders.
- Absent continuous state means local-zero idle at the anchor. Local-nonzero idle must remain present.
- Canonical idle-zero omission is `localOffset == 0`, `velocity == 0`, and `mode == Idle`. Residual remainders, sequence, speed, facing, and last move direction are discarded as progression metadata when this condition is true.
- A unit cannot have active `UnitKinematicRuntimeState` and active `UnitContinuousLocomotionState` at the same time.
- Local offset normalizes the anchor at the half-cell boundary when the neighbor cell is traversable. `+2048` is never stored.
- `PlayerContinuousLocomotionSettings.CollisionRadiusCells` is player Free2D-only blocker approach margin. The project default is `0`, which keeps the point/pivot clamp (`+2047` / `-2048`); configured nonzero radius clamps grid-solid blocker approach at `halfCell - radius` while unit overlap and free-neighbor normalization remain unchanged.
- `PlayerContinuousLocomotionSettings.ActionAssistSettleWindowCells` is the near-settled input leniency window for Action Assist. The project default is `0.125f`, converted to `512` fixed units.
- Anchor normalization writes must apply `MoveEntity` first and `SetUnitContinuousLocomotionState(normalized state)` second in the same `FinalizationBatch`; this preserves the normalized pose after `MoveEntity` purges transient unit locomotion state.
- `MoveEntity` anchor normalization is classified as a grid transaction primitive, not legacy ordinary Unit movement. It must not emit legacy `TickEntityMotionKind.Move`; presentation remains `TickContinuousLocomotionTrack`.
- Collision is grid-authoritative: wall, terrain, box, solid, and board edge approaches block; topology-edge approaches may zero-settle only when the retained topology transition is the blocker, and unit overlap remains allowed.
- Passive contact remains anchor-cell based. Visual overlap before anchor normalization does not trigger neighbor contact.
- Contact can begin only after anchor normalization commits the new `EntityState.position`.
- Push, flip, and action preview require local-zero settled pose; local-nonzero idle and moving continuous pose reject settled probes.
- When Action Assist is enabled, only near-settled local-nonzero push/flip input with an actionable candidate in the requested direction queues canonical intent in `PlayerControlState.queuedFree2DAction`, aligns the player back to the current anchor center through `ContinuousLocomotionMode.AlignToAnchor`, and re-enters the existing settled-only push/flip path on the next tick after local-zero is reached.
- Near-settled means `abs(localX) <= ActionAssistSettleWindowUnits && abs(localY) <= ActionAssistSettleWindowUnits`. The gate is inclusive, so `512` queues with the default window and `513` rejects.
- The settle-window gate applies only when creating a new queue. The actionable-candidate gate is also rechecked before queued align; if the candidate is gone, the queue is cleared and continuous pose is frozen in place instead of aligning to anchor.
- The actionable-candidate gate checks only whether current-anchor push/flip resolution has a candidate. It does not store a target entity at queue time and does not widen the default `512` settle window, so showcase box-front radius clamp at `1280` remains outside Action Assist for this slice.
- A rejected push/flip assist does not consume held ordinary free2D movement. If no queue/action starts and a held move direction exists, the tick continues through normal continuous locomotion instead of freezing the local pose.
- Action Assist v1 supports only push and flip. It does not add generic interact/action preview assist.
- Queued Action Assist stores only kind, direction, and requested tick. It does not store a target entity; legality is revalidated against the current snapshot when executed.
- Action Assist align target is always the current anchor center. `EntityState.position` is not changed by align, and align never snaps.
- Queued Action Assist has priority over held ordinary movement until it executes, fails after revalidation, or is cleared by interruption/death/respawn.
- Presentation consumes authoritative continuous local pose through `TickContinuousLocomotionTrack`. Transform, Animator, PhysX, and root motion are not simulation authority.
- Flag-on player ordinary movement must not reach the legacy ordinary `MoveIntent` -> `MovementExpander` -> `TickEntityMotionKind.Move` path. Push, flip, item/action materialization, topology, spawn, respawn, and cleanup remain allowed legacy grid transactions.
- `AlignToAnchor` presentation is emitted through `TickContinuousLocomotionTrack` and is treated as active locomotion. There is no pending-action UI in v1.
- Boundary v1 trace metadata may classify anchor commits as `LocomotionAnchorCommit` and push/flip/item materialization as `BoxActionMovement`; this metadata is diagnostic and must not change canonical replay hashes.
- `SpawnRespawnPlacement` is the explicit placement boundary for respawn. Respawn remains a direct placement/grid transaction path, not ordinary Unit locomotion.
- Continuous nonzero idle pose must emit or retain presentation override so the view does not snap to anchor center.
- Nonlethal hit, lethal hit, removal, death hold, cleanup, and respawn preserve or purge continuous pose through the same authoritative write path as other state.
- Nonlethal hit, lethal hit, cleanup, and respawn must clear queued Action Assist intent.
- Replay/hash includes ordered `UnitContinuousLocomotion` canonical state. Explicit idle-zero and absent state are hash-equivalent.
- Replay/hash includes queued Action Assist kind, direction, and requested tick through ordered `PlayerControl` state.

## Flag Hierarchy

Player ordinary movement dispatch order:

1. `EnablePlayerFree2DLocalLocomotion`
2. `EnablePlayerStoppableKinematicLocomotion`
3. `EnablePlayerSameFaceContinuousLocomotion`
4. Legacy discrete movement

Action Assist flag hierarchy:

1. `EnablePlayerFree2DLocalLocomotion`
2. `EnablePlayerFree2DActionAssist`

The free2D flag is independent of enemy and charge kinematic flags. Turning it off must restore the existing player stoppable/same-face/legacy behavior without changing enemy or charge movement.
Turning Action Assist off while keeping free2D on restores local-nonzero push/flip rejection without disabling Free2D movement.

`GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion` is the readiness default-on bundle for gameplay hosts and boundary inventory tests. As of legacy ordinary movement deprecation Phase 3, `GameplayRuntimeFeatureFlags.None` no longer authorizes covered player/enemy/Charge fallback. As of Phase 4/5/6, `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline` also no longer authorizes player, enemy, or Charge covered fallback; after Phase 7 it is diagnostic compatibility only.
Default bundle adoption is explicit: showcase/dev gameplay hosts may call `GameplaySceneHostConfiguration.ApplyRuntimeFeatureFlags(GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion)`, but replay harnesses, composition-root helpers, historical tests, migration comparison tests, and flag-off goldens must keep `None` unless they intentionally opt into the bundle.
Default bundle adoption is not full legacy deletion. Phase 6 removes player, enemy, and Charge covered fallback authorization, while Phase 7 keeps `LegacyOrdinaryFallbackBaseline` as a diagnostic compatibility preset. The v3 deletion-readiness gate lives in `Docs/Testing/Legacy-Ordinary-Unit-Movement-Deprecation-Readiness-2026-05-01.md` and keeps `MoveEntity`, `MovementExpander`, retained grid transactions, and glide retained fallback out of the deletion target.
Scoped deletion preparation is superseded for the player branch by `Phase4_LegacyOrdinaryFallbackBaseline_PlayerFallbackRemoved` and `Replay_Phase4_LegacyBaseline_PlayerFallbackRemoved`; player legacy discrete fallback is no longer a supported runtime fallback after Phase 4.

## Known Limitations

- No enemy, charge, jump, phase, glide, forced motion, or knockback migration.
- No diagonal movement.
- No Free2D-native nonzero local topology seam crossing or local offset face-basis remap. v1.1 only zero-settles eligible topology-edge approach poses to local-zero before using retained topology grid materialization.
- Local-zero settled topology moves may hand off to the retained `TopologyMaterialization` grid transaction path.
- No continuous box collider, footprint contact, swept combat, or projectile collision redesign.
- No mid-pose push/flip/action execution. These remain settled-only.
- No pose-based action probe and no local-nonzero tolerance-as-settled behavior.
- No Action Assist cancel by held movement input in v1.
- Queued push/flip execution is intentionally delayed until the tick after local-zero align completes.
- A box-front radius clamp at `CollisionRadiusCells = 0.1875f` leaves the player at `1280` fixed units from anchor center, which is intentionally outside the default Action Assist window. That scenario remains a settled-only reject unless future stance solving or explicit wider tuning is introduced.
- Contact timing is anchor-based, not visual-footprint based.

## Stabilization Coverage

The stabilization suite locks the following acceptance tests:

- Unit/state: `UnitContinuousLocomotionState_IdleZero_OmissionPolicy`, `WorldState_RemoveEntity_PurgesContinuousLocomotionState`, `WorldState_MutualExclusion_KinematicAndContinuous`, `FinalizationBatch_MoveEntityThenSetContinuousState_PreservesNormalizedPose`.
- Movement/scenario: `Player_Free2D_WallClamp`, `Player_Free2D_TerrainClamp`, `Player_Free2D_TopologyEdge_LocalZero_HandsOffToTopologyGridTransaction`, `Player_Free2D_TopologyEdge_LocalNonZero_ClampsOrRejects`, radius blocker approach coverage, `Player_Free2D_BeforeAnchorBoundary_NoEnemyContact`, `Player_Free2D_AfterAnchorBoundary_EnemyContactPossible`, `Player_Free2D_LocalZero_PushFlipAllowed`, `Player_Free2D_LocalNonZero_ActionPreviewRejected`, `Player_Free2D_HitNonlethal_PreservesPose`, `Player_Free2D_HitLethal_RemovedTerminalPreservesPose`.
- Action Assist: `Free2DActionAssist_PushQueuedAtLocalNonZero`, `Free2DActionAssist_EmptyFloorWithinSettleWindow_PushDoesNotQueueOrAlign`, `Free2DActionAssist_EmptyFloorWithinSettleWindow_FlipDoesNotQueueOrAlign`, `Free2DActionAssist_NoCandidatePushWithHeldMove_ContinuesFree2DMovement`, `Free2DActionAssist_NoCandidateFlipWithHeldMove_ContinuesFree2DMovement`, `Free2DActionAssist_NoActionCandidate_EmitsDeterministicRejectTrace`, `Free2DActionAssist_BoxWithoutPushCapability_DoesNotQueueOrAlign`, `Free2DActionAssist_AlignsToAnchorWithoutSnap`, `Free2DActionAssist_PushExecutesAfterAlign`, `Free2DActionAssist_FlipExecutesAfterAlign`, `Free2DActionAssist_BoxRadiusClampThenPush`, `Free2DActionAssist_WithinSettleWindow_QueuesAndAligns`, `Free2DActionAssist_OutsideSettleWindow_DoesNotQueueOrAlign`, `Free2DActionAssist_WindowBoundaryInclusive`, `Free2DActionAssist_WindowBoundaryExclusiveAbove`, `Free2DActionAssist_ExistingQueue_IgnoresWindowAndContinuesAlign`, `Free2DActionAssist_ExistingQueue_NoCandidateClearsWithoutAlign`, `Free2DActionAssist_ActionTargetRevalidatedAtExecute`, `Free2DActionAssist_InvalidAfterAlign_ClearsQueue`, `Free2DActionAssist_MovementInputDoesNotCancelQueue`, `Free2DActionAssist_HitClearsQueue`, `Free2DActionAssist_DeathClearsQueue`, `Free2DActionAssist_LocalZero_PushStillImmediate`, `Free2DActionAssist_LocalNonZero_ActionNotExecutedBeforeSettled`, `Free2DActionAssist_FlagOff_Baseline`, and `Free2DActionAssist_DoesNotAffectKinematicFallback`.
- Presentation: `GameplayTickViewPresenter_ContinuousPose_AppliesAnchorPlusLocalOffset`, `GameplayTickViewPresenter_ContinuousIdleNonZero_DoesNotSnapToAnchor`, `GameplayTickViewPresenter_ContinuousRemovedTerminal_RetainsPose`.
- Replay: `Replay_PlayerFree2D_StopTurnClamp_IsDeterministic`, `Replay_PlayerFree2D_RadiusApproachBlocker_IsDeterministic`, `Replay_PlayerFree2D_AnchorNormalizeContact_IsDeterministic`, `Replay_PlayerFree2D_TopologyApproachHandoff_IsDeterministic`, `Replay_PlayerFree2D_HitDeath_IsDeterministic`, `Replay_Free2DActionAssist_QueueAlignExecute_IsDeterministic`, `Replay_Free2DActionAssist_OutsideWindowReject_IsDeterministic`, `Replay_Free2DActionAssist_NoCandidateReject_IsDeterministic`.
- Boundary v1 / deprecation Phase 1: `TickPipeline_ValidateLegacyExpansionIntents_BlocksFlagOnUnitOrdinaryMove`, `DeprecationPhase1_DefaultGameplayLocomotion_PlayerEnemyCharge_NoLegacyFallback`, `Replay_DeprecationPhase1_DefaultGameplayLocomotion_NoCoveredLegacyFallback`, and `Boundary_UnknownInventory_NormalGameplayHasNoUnexpectedUnknownMovement` verify that flag-on Free2D ordinary movement does not leak into legacy ordinary Unit movement while push/flip/item/topology grid transactions remain retained.
- Baseline: `Player_Free2D_FlagOff_ExistingKinematicBaseline`, `Player_Free2D_DoesNotAffectEnemyOrCharge`.

## Golden Policy

- Do not regenerate flag-off goldens for this slice.
- Free2D flag-on hashes may add a `UnitContinuousLocomotion` section while continuous state is present.
- Action Assist flag-on hashes add queued action fields to `PlayerControl` while an action is pending and may add `AlignToAnchor` continuous mode while align is active.
- Explicit idle-zero state and absent state should be canonical-equivalent after storage normalization.
- Replay validation should cover stop, turn, clamp, hit/death, cleanup, and respawn sequences.

## Rollback

First set `EnablePlayerFree2DActionAssist` to false to restore settled-only local-nonzero push/flip rejection while keeping Free2D movement enabled.
If the full Free2D movement rollout must be disabled, set `EnablePlayerFree2DLocalLocomotion` to false.
The player ordinary movement path then falls back to `EnablePlayerStoppableKinematicLocomotion`, then `EnablePlayerSameFaceContinuousLocomotion`, then legacy discrete movement. No data migration is required because continuous local-zero idle is represented by absent state.
No data migration is required for Action Assist because the default queued action is `None`.
