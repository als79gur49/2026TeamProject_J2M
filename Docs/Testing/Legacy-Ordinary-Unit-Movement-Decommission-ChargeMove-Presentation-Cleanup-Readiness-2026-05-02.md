# ChargeMove Presentation Consumer Deletion

Date: 2026-05-02

## Executive Decision

`TickEntityMotionKind.ChargeMove` enum/data support has been removed.
The retained synthetic presentation compatibility path is closed.
Current Charge presentation is represented by `TickKinematicMotionTrack(MotionMode.Charge)` for movement and `TickEnemyChargePresentationSignal` for the Charge semantic/effect.
`TickEntityMotionKind.Move`, `MoveEntity`, `MovementExpander`, retained grid transactions, and glide retained fallback remain out of scope for this deletion.
Replay/golden files were not automatically rewritten.

## Current State

`TickResultBuilder.TryResolveMotionKind` maps `MovementSemanticKind.Move` to `TickEntityMotionKind.Move` regardless of active Charge state.
`DefaultGameplayLocomotion`, `GameplayRuntimeFeatureFlags.None`, `RemovedLegacyFallbackDiagnosticBaseline`, `EnableEnemyChargeKinematicLocomotion`, and `AllKinematicLocomotionEnabled` must not produce a legacy Charge entity motion.
Covered Charge fallback attempts continue to reject with `ChargeLegacyFallbackRemovedFromRuntime` under removed-diagnostic lanes.

## Deleted Consumers

| area | removed surface | replacement |
|---|---|---|
| enum/data contract | `TickEntityMotionKind.ChargeMove` | no replacement enum; use kinematic Charge presentation |
| timing | dedicated Charge entity-motion duration config | Charge active timing stays in Charge kinematic/AI timing |
| authoring | per-entity Charge entity-motion override | generic entity `Move`/`Push`/`Flip` authoring only |
| host presenter | Charge entity-motion branch | `TickKinematicMotionTrack(MotionMode.Charge)` and signal handling |
| synthetic tests | explicit synthetic Charge entity-motion construction | canonical Charge track/signal canaries |

## Validation Policy

Current-policy tests assert:
- default gameplay Charge emits a Charge kinematic track and Charge signal;
- Charge kinematic flag-on emits a Charge kinematic track and Charge signal;
- generic `TickEntityMotionKind.Move` presentation still works;
- retained grid transactions remain allowed;
- replay output contains no legacy Charge entity-motion text.

Historical docs may mention the old `ChargeMove` name only as removed legacy vocabulary.
Docs must not describe synthetic Charge entity-motion compatibility as retained current behavior.

## Asset And Golden Policy

Unity serialized residue may remain in existing YAML assets until an owner-approved asset migration.
Runtime code no longer reads Charge entity-motion authoring or timing fields.
Replay/golden files are not rewritten by this package; any historical golden drift requires owner approval before migration.
