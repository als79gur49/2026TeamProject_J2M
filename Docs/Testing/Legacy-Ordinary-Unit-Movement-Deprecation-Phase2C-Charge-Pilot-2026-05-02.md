# Legacy Ordinary Unit Movement Deprecation Phase 2C: Charge Fallback Pilot

Date: 2026-05-02

## Summary

Phase 2C is a scoped pilot for Charge active legacy fallback only. It does not delete fallback code. It pins where the branch exists, which flags can reach it, and which paths remain retained or out of scope.

The pilot target is:

```text
Charge active movement
-> legacy charge active movement/finalization branch
-> MoveEntity Boundary=LegacyFallback
-> TickEntityMotionKind.ChargeMove
```

`MoveEntity` remains an anchor/grid transaction primitive. `MovementExpander` remains retained for grid transactions and flag-off fallback.

## Branch Inventory

The Charge active fallback branch is currently reachable through this chain:

| stage | runtime location | Phase 2C decision |
|---|---|---|
| charge source | active `EnemyChargeRuntimeState(Active)` plus a charge enemy ordinary `MoveIntent` | documented source only |
| flag gate | `TickPipeline.RunPlanPhase` consumes active charge movement through `BuildEnemyChargeKinematicLocomotionPlans` when `EnableEnemyChargeKinematicLocomotion` is on | default/flag-on must not fall through |
| leak guard | `TickPipeline.ValidateLegacyExpansionIntents` and `TryResolveForbiddenLegacyUnitOrdinaryMovement` reject covered charge active `Move` with `ChargeCoveredKinematicReachedLegacyExpansion` | charge flag-on leak is a regression |
| legacy candidate | `MovementExpander.ExpandMoveLike` and `ExpandMove` create ordinary `ActionGroupKind.Move` for flag-off active charge | retained for flag-off support |
| legacy charge write | legacy action finalization consumes the active step with `ConsumeLegacyActiveStep` | deletion candidate only for charge active fallback |
| presentation | `TickResultBuilder.ShouldUseChargeMovePresentation` maps active non-kinematic charge movement to `TickEntityMotionKind.ChargeMove` | Phase 2C allowed this for documented charge `None` baseline; Phase 3 moved it to `LegacyOrdinaryFallbackBaseline` |

## Flag Reachability

`DefaultGameplayLocomotion` enables Charge kinematic locomotion. Under that bundle, active charge movement must be represented by `TickKinematicMotionTrack` with `MotionMode.Charge`. It must not emit `LegacyFallback`, legacy `TickEntityMotionKind.ChargeMove`, or `LegacyUnitOrdinaryMovementDetected`.

`EnemyChargeKinematicLocomotionEnabled` makes synthetic active charge expansion a forbidden leak. The expected rejection reason is `ChargeCoveredKinematicReachedLegacyExpansion`.

Historical Phase 2C note: `GameplayRuntimeFeatureFlags.None` was the Charge fallback baseline in this phase. Phase 3 supersedes that policy: `None` now blocks covered Charge active fallback with `LegacyOrdinaryFallbackRequiresExplicitBaseline`. Phase 6 supersedes the explicit baseline policy: `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline` now blocks Charge active fallback with `ChargeLegacyFallbackRemovedFromRuntime`.

Custom flags with `EnableEnemyChargeKinematicLocomotion` disabled may reach Charge active fallback only when `EnableLegacyOrdinaryUnitFallback` is explicitly enabled.

## Retained And Out-Of-Scope Paths

Phase 2C must not classify these paths as Charge active fallback:

- player ordinary fallback
- enemy ordinary fallback
- glide default and flag-off fallback retained exception
- jump start, airborne, and landing special movement
- scripted relocation and phase relocation
- topology materialization
- box push, flip, item, impact, and action movement
- spawn and respawn placement
- cleanup removal
- anchor normalization through `LocomotionAnchorCommit`

Tests should assert retained boundary kinds such as `TopologyMaterialization`, `BoxActionMovement`, `SpawnRespawnPlacement`, `CleanupRemoval`, `ScriptedRelocation`, or `LocomotionAnchorCommit` rather than banning `MoveEntity`.

## Phase 2C Canaries

The Charge pilot is pinned by these test additions:

- `BoundaryInventoryScenarioTests.Phase2C_ChargeLegacyFallback_DefaultGameplayLocomotion_NoChargeMoveFallback`
- `BoundaryInventoryScenarioTests.Phase2C_ChargeLegacyFallback_ChargeKinematicFlagOn_BlockedBeforeMovementExpander`
- `BoundaryInventoryScenarioTests.Phase2C_ChargeLegacyFallback_FlagOffBaseline_RemovedByPhase6`
- `BoundaryInventoryScenarioTests.Phase2C_ChargeLegacyFallback_PlayerEnemyOrdinary_AreOutOfScope`
- `BoundaryInventoryScenarioTests.Phase2C_ChargeLegacyFallback_GlideDefault_IsRetainedException_NotChargePilot`
- `MovementPhaseScenarioTests.Phase2C_ChargeLegacyFallback_ValidateLegacyExpansionIntents_ChargeFlagReachability`
- `EnemyKinematicLocomotionReplayTests.Replay_Phase2C_ChargeDefaultGameplayLocomotion_NoChargeMoveFallback`
- `EnemyKinematicLocomotionReplayTests.Replay_Phase2C_ChargeKinematicFlagOn_NoChargeMoveFallback`
- `EnemyKinematicLocomotionReplayTests.Replay_Phase2C_ChargeFlagOffLegacyFallback_BaselineDocumented`

The helper vocabulary remains scoped to `LegacyFallback`, legacy `ChargeMove` presentation, and `LegacyUnitOrdinaryMovementDetected`. It must not become a broad `MoveEntity` ban.

## Deletion Preconditions

Actual Charge fallback deletion is not approved by Phase 2C. Before deleting or test-only-scoping the Charge fallback branch, the next phase needs:

- explicit approval to remove or narrow the `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline` Charge fallback baseline
- `ChargeMove` presentation owner approval
- replay/golden migration or exemption policy
- green Charge default and kinematic-on no-fallback canaries
- green retained player, enemy ordinary, glide, jump, phase/scripted relocation, topology, box/action, spawn/respawn, and cleanup canaries
- no player fallback, enemy ordinary fallback, or glide fallback changes in the same deletion patch
