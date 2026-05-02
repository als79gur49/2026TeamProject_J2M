# Legacy Ordinary Unit Movement Deprecation Phase 2: Player Fallback Pilot

Date: 2026-05-02

## Summary

Phase 2 is a scoped pilot for player legacy ordinary fallback only. It does not delete fallback code. It pins where the branch exists, which flags can reach it, and which paths must remain retained grid transactions.

The pilot target is:

```text
Player ordinary MoveIntent
-> MovementExpander legacy ordinary Move branch
-> MoveEntity Boundary=LegacyFallback
-> TickEntityMotionKind.Move
```

`MoveEntity` remains an anchor/grid transaction primitive. `MovementExpander` remains retained for grid transactions and flag-off fallback.

## Branch Inventory

The player fallback branch is currently reachable through this chain:

| stage | runtime location | Phase 2 decision |
|---|---|---|
| player source | `PlayerLogic.CollectMovementIntents` emits ordinary `RawMovementIntent(MovementCommandKind.Move)` | documented source only |
| flag gate | `TickPipeline.RunPlanPhase` consumes player ordinary movement through Free2D when `EnablePlayerFree2DLocalLocomotion` is on, otherwise through player kinematic when `EnablePlayerSameFaceContinuousLocomotion` is on | default/flag-on must not fall through |
| leak guard | `TickPipeline.ValidateLegacyExpansionIntents` and `TryResolveForbiddenLegacyUnitOrdinaryMovement` reject covered player ordinary `Move` with `PlayerCoveredLocomotionReachedLegacyExpansion` | player flag-on leak is a regression |
| legacy candidate | `MovementExpander.ExpandMoveLike` and `ExpandMove` create ordinary `ActionGroupKind.Move` | retained for flag-off and grid transaction support |
| boundary | `TickPipeline` movement boundary resolution classifies ordinary Unit `Move` groups as `LegacyFallback` | deletion candidate only for player ordinary fallback |
| presentation | `TickResultBuilder` maps unsuppressed `MovementSemanticKind.Move` to `TickEntityMotionKind.Move` | Phase 2 allowed this for documented player `None` baseline; Phase 3 moved it to `LegacyOrdinaryFallbackBaseline` |

## Flag Reachability

`DefaultGameplayLocomotion` enables player Free2D, player same-face kinematic, and player stoppable kinematic. Under that bundle, player ordinary movement must be represented by `TickContinuousLocomotionTrack`, player kinematic fallback, or retained topology handoff. It must not emit `LegacyFallback`, legacy `TickEntityMotionKind.Move`, or `LegacyUnitOrdinaryMovementDetected`.

`PlayerFree2DLocalLocomotionEnabled` and `PlayerSameFaceContinuousLocomotionEnabled` both make synthetic player ordinary expansion a forbidden leak. The expected rejection reason is `PlayerCoveredLocomotionReachedLegacyExpansion`.

Historical Phase 2 note: `GameplayRuntimeFeatureFlags.None` was the player fallback baseline in this phase. Phase 3 superseded that policy: `None` blocks covered player fallback with `LegacyOrdinaryFallbackRequiresExplicitBaseline`. Phase 4 supersedes the explicit player baseline too: `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline` now rejects player fallback with `PlayerLegacyFallbackRemovedFromRuntime`.

Custom flags with both player Free2D and player same-face kinematic disabled may reach player fallback only when `EnableLegacyOrdinaryUnitFallback` is explicitly enabled.

## Retained Paths

Phase 2 must not classify these paths as player legacy ordinary fallback:

- topology materialization, including player Free2D local-zero and approach-settle handoff
- box push, flip, item, impact, and action movement
- spawn and respawn placement
- cleanup removal
- scripted relocation and phase relocation
- anchor normalization through `LocomotionAnchorCommit`
- enemy ordinary fallback, charge fallback, and glide fallback

Tests should assert retained boundary kinds such as `TopologyMaterialization`, `BoxActionMovement`, `SpawnRespawnPlacement`, `CleanupRemoval`, `ScriptedRelocation`, or `LocomotionAnchorCommit` rather than banning `MoveEntity`.

## Phase 2 Canaries

The player pilot is pinned by these test additions:

- `BoundaryInventoryScenarioTests.Phase2_PlayerLegacyFallback_DefaultGameplayLocomotion_NoLegacyFallback`
- `BoundaryInventoryScenarioTests.Phase2_PlayerLegacyFallback_Free2DFlagOn_BlockedBeforeMovementExpander`
- `BoundaryInventoryScenarioTests.Phase2_PlayerLegacyFallback_KinematicFlagOn_BlockedBeforeMovementExpander`
- `BoundaryInventoryScenarioTests.Phase2_PlayerLegacyFallback_FlagOffBaseline_RemovedByPhase4`
- `BoundaryInventoryScenarioTests.Phase2_PlayerLegacyFallback_TopologyHandoff_IsRetainedGridTransaction`
- `MovementPhaseScenarioTests.Phase2_PlayerLegacyFallback_ValidateLegacyExpansionIntents_PlayerFlagReachability`
- `PlayerContinuousLocomotionReplayTests.Replay_Phase2_PlayerDefaultGameplayLocomotion_NoLegacyFallback`
- `PlayerContinuousLocomotionReplayTests.Replay_Phase2_PlayerFlagOffLegacyFallback_BaselineDocumented`

The helper vocabulary remains scoped to `LegacyFallback`, legacy `Move` presentation, and `LegacyUnitOrdinaryMovementDetected`. It must not become a broad `MoveEntity` ban.

## Deletion Preconditions

Actual player fallback deletion is not approved by Phase 2. Before deleting or test-only-scoping the player fallback branch, the next phase needs:

- explicit approval to remove or narrow the `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline` player fallback baseline
- replay/golden migration or exemption policy
- green player default, Free2D-on, and kinematic-on no-fallback canaries
- green retained topology, box/action, spawn/respawn, cleanup, and scripted relocation canaries
- no enemy ordinary fallback, charge fallback, or glide fallback changes in the same deletion patch
