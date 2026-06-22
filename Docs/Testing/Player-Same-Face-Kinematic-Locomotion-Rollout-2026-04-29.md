# Player Same-Face Kinematic Locomotion Rollout

Date: 2026-04-29

Status: historical/superseded. This document records the earlier player same-face kinematic rollout. Current player ordinary movement is Free2D-owned and must not use this player kinematic fallback path.

## Validation Contract

- Historical flag-off/flag-on behavior in this section is preserved only as migration context.
- Current player ordinary movement must not reach the generic `MoveIntent` -> `MovementExpander` -> `TickEntityMotionKind.Move` path.
- `MoveEntity` midpoint anchor commit is classified as a grid transaction primitive, not legacy ordinary Unit movement.
- Boundary v1 suppresses legacy `TickEntityMotionKind.Move` only for locomotion anchor commits and ordinary Unit locomotion leaks. Box/action/topology/spawn/respawn grid transactions retain their existing presentation paths.
- Boundary metadata is trace-only diagnostic data and must not enter canonical replay hashes.
- Boundary v1 / deprecation Phase 1 stabilization adds direct canaries that block flag-on covered ordinary Unit `Move` leaks, allow retained grid transactions, and assert normal movement/finalization traces do not contain unexpected `Boundary=Unknown`. The Phase 1 targeted Unity XML canaries are runtime green, but this is fallback isolation, not legacy branch deletion.
- Same-destination ordinary Unit stacking remains the current gameplay contract. Priority-winner regression coverage is asserted through blocking grid transaction candidates in `MovementPhase_SameDestination_OnlyHigherPriorityWins_BoundaryInvariant`.
- Default `KinematicMoveDurationSeconds` is `1f / 3f`, quantized with even ceil. At 60 TPS this is 20 ticks with midpoint anchor commit at tick 10.
- Mid-motion hashes are expected to include the `UnitKinematics` determinism section.
- Mid-motion accepted damage interrupts voluntary locomotion with a `MotionMode.Interrupted` kinematic state; the next flag-on plan tick clears surviving interrupted state to settled-zero.
- Lethal mid-motion damage preserves the interrupted pose until cleanup and then removes the entity plus its kinematic state through `WorldState.RemoveEntity`.
- Settled final hashes are expected to omit settled-zero kinematics.
- Historical Unity scenario coverage lived in the player kinematic locomotion scenario suite.
- Current coverage lives in player Free2D continuous locomotion scenario and replay suites plus movement intent partition tests.

## Golden Policy

- Do not rewrite existing flag-off replay or scenario goldens for this slice.
- Add only explicit flag-on goldens if a downstream lane needs committed artifacts.
- Any flag-on golden diff should be limited to expected kinematic pose state during non-settled ticks and the resulting duration-derived anchor commit tick.
- Mid-motion hit/removal goldens, when needed, may additionally include the interrupted kinematic dump, `KinematicMotionInterrupted`, `KinematicInterruptClosed`, and cleanup removal events.

## Rollback

This rollback section is historical. The generic `MovementExpander` path remains present for retained non-player movement and explicit action/grid transaction owners, not for player ordinary fallback.
Phase 4 removes player legacy ordinary fallback from the runtime path. Phase 5/6 remove enemy and Charge covered fallback authorization too. After Phase 8B/8C, current removed diagnostics use the canonical `the removed diagnostic baseline preset (historical, deleted)`; old diagnostic baseline alias vocabulary is historical-only and is not accepted by runtime code. `MoveEntity`, `MovementExpander`, retained grid transactions, and glide retained fallback stay retained.
Scoped deletion preparation for the player branch is now covered by `Phase4_RemovedDiagnosticBaseline_PlayerFallbackRemoved`; player legacy discrete fallback is no longer a supported runtime fallback after Phase 4.
To reproduce the old 4tick flag-on cadence for migration comparison, set
`PlayerKinematicLocomotionTiming.KinematicMoveDurationSeconds` to `4f / SimulationTicksPerSecond`.
