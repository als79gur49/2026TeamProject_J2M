# Legacy Ordinary Unit Movement Deprecation Phase 2B: Enemy Fallback Pilot

Date: 2026-05-02

## Summary

Phase 2B is a scoped pilot for enemy ordinary legacy fallback only. It does not delete fallback code. It pins where the branch exists, which flags can reach it, and which paths remain retained or out of scope.

The pilot target is:

```text
Enemy ordinary MoveIntent
-> MovementExpander legacy ordinary Move branch
-> MoveEntity Boundary=LegacyFallback
-> TickEntityMotionKind.Move
```

`MoveEntity` remains an anchor/grid transaction primitive. `MovementExpander` remains retained for grid transactions and flag-off fallback.

## Branch Inventory

The enemy ordinary fallback branch is currently reachable through this chain:

| stage | runtime location | Phase 2B decision |
|---|---|---|
| enemy source | `EnemyLogic.CollectMovementIntents` calls `ResolveBaselineGroundLocomotion`, which can emit ordinary `RawMovementIntent(MovementCommandKind.Move)` | documented source only |
| flag gate | `TickPipeline.RunPlanPhase` consumes enemy ordinary movement through `BuildEnemySameFaceKinematicLocomotionPlans` when `EnableEnemySameFaceContinuousLocomotion` is on | default/flag-on must not fall through |
| leak guard | `TickPipeline.ValidateLegacyExpansionIntents` and `TryResolveForbiddenLegacyUnitOrdinaryMovement` reject covered enemy ordinary `Move` with `EnemyCoveredOrdinaryKinematicReachedLegacyExpansion` | enemy flag-on leak is a regression |
| legacy candidate | `MovementExpander.ExpandMoveLike` and `ExpandMove` create ordinary `ActionGroupKind.Move` | retained for flag-off and grid transaction support |
| boundary | `TickPipeline` movement boundary resolution classifies ordinary Unit `Move` groups as `LegacyFallback` | deletion candidate only for enemy ordinary fallback |
| presentation | `TickResultBuilder` maps unsuppressed `MovementSemanticKind.Move` to `TickEntityMotionKind.Move` | Phase 2B allowed this for documented enemy `None` baseline; Phase 3 moved it to `LegacyOrdinaryFallbackBaseline` |

## Flag Reachability

`DefaultGameplayLocomotion` enables enemy same-face continuous locomotion and Charge kinematic locomotion, but still excludes enemy glide kinematic locomotion. Under that bundle, enemy ordinary movement must be represented by `TickKinematicMotionTrack`. It must not emit enemy ordinary `LegacyFallback`, legacy `TickEntityMotionKind.Move`, or `LegacyUnitOrdinaryMovementDetected`.

`EnemySameFaceContinuousLocomotionEnabled` makes synthetic enemy ordinary expansion a forbidden leak. The expected rejection reason is `EnemyCoveredOrdinaryKinematicReachedLegacyExpansion`.

Historical Phase 2B note: `GameplayRuntimeFeatureFlags.None` was the enemy ordinary fallback baseline in this phase. Phase 3 supersedes that policy: `None` now blocks covered enemy ordinary fallback with `LegacyOrdinaryFallbackRequiresExplicitBaseline`. Phase 5 supersedes the explicit baseline policy: `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline` now blocks enemy ordinary fallback with `EnemyLegacyFallbackRemovedFromRuntime`.

Custom flags with `EnableEnemySameFaceContinuousLocomotion` disabled no longer authorize enemy ordinary fallback at runtime. Phase 6/7 also supersede the Charge explicit baseline path; `LegacyOrdinaryFallbackBaseline` is diagnostic compatibility only.

## Retained And Out-Of-Scope Paths

Phase 2B must not classify these paths as enemy ordinary fallback:

- player ordinary fallback
- Charge active fallback and `TickEntityMotionKind.ChargeMove`
- glide default and flag-off fallback retained exception
- jump start, airborne, and landing special movement
- scripted relocation and phase relocation
- topology materialization
- box push, flip, item, impact, and action movement
- spawn and respawn placement
- cleanup removal
- anchor normalization through `LocomotionAnchorCommit`

Tests should assert retained boundary kinds such as `TopologyMaterialization`, `BoxActionMovement`, `SpawnRespawnPlacement`, `CleanupRemoval`, `ScriptedRelocation`, or `LocomotionAnchorCommit` rather than banning `MoveEntity`.

## Phase 2B Canaries

The enemy pilot is pinned by these test additions:

- `BoundaryInventoryScenarioTests.Phase2B_EnemyLegacyFallback_DefaultGameplayLocomotion_NoLegacyFallback`
- `BoundaryInventoryScenarioTests.Phase2B_EnemyLegacyFallback_KinematicFlagOn_BlockedBeforeMovementExpander`
- `BoundaryInventoryScenarioTests.Phase2B_EnemyLegacyFallback_FlagOffBaseline_RemovedByPhase5`
- `BoundaryInventoryScenarioTests.Phase2B_EnemyLegacyFallback_GlideDefault_IsRetainedException_NotEnemyOrdinaryPilot`
- `BoundaryInventoryScenarioTests.Phase2B_EnemyLegacyFallback_ChargeActive_IsOutOfScope`
- `MovementPhaseScenarioTests.Phase2B_EnemyLegacyFallback_ValidateLegacyExpansionIntents_EnemyFlagReachability`
- `EnemyKinematicLocomotionReplayTests.Replay_Phase2B_EnemyDefaultGameplayLocomotion_NoLegacyFallback`
- `EnemyKinematicLocomotionReplayTests.Replay_Phase2B_EnemyKinematicFlagOn_NoLegacyFallback`
- `EnemyKinematicLocomotionReplayTests.Replay_Phase5_EnemyLegacyBaseline_FallbackRemoved`

The helper vocabulary remains scoped to `LegacyFallback`, legacy `Move` presentation, and `LegacyUnitOrdinaryMovementDetected`. It must not become a broad `MoveEntity` ban.

## Deletion Preconditions

Actual enemy fallback deletion is not approved by Phase 2B. Before deleting or test-only-scoping the enemy fallback branch, the next phase needs:

- explicit approval to remove or narrow the `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline` enemy fallback baseline
- historical EnemyAi baseline owner approval
- replay/golden migration or exemption policy
- green enemy default and kinematic-on no-fallback canaries
- green retained glide, Charge, jump, phase/scripted relocation, topology, box/action, spawn/respawn, and cleanup canaries
- no player fallback, Charge fallback, or glide fallback changes in the same deletion patch
