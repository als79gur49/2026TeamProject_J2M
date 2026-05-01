# Player Same-Face Kinematic Locomotion Rollout

Date: 2026-04-29

This rollout is guarded by `GameplayRuntimeFeatureFlags.EnablePlayerSameFaceContinuousLocomotion`.
The default remains off for scene hosts, composition-root helpers, replay harnesses, and tests.
If `EnablePlayerFree2DLocalLocomotion` is enabled, player ordinary movement is dispatched to `UnitContinuousLocomotionState` first and this same-face kinematic path remains available only as fallback/legacy coverage.
`GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion` includes this fallback flag for explicit default gameplay host adoption, but it does not change replay harness, composition-root, historical, or golden defaults. Those paths continue to use `GameplayRuntimeFeatureFlags.None` unless a test or host opts in directly.

## Validation Contract

- Flag off: existing discrete player movement remains the baseline and existing goldens should not be regenerated.
- Flag on: player same-face voluntary moves advance using `PlayerKinematicLocomotionTimingSettings`.
- Flag on: player ordinary movement must not reach the legacy ordinary `MoveIntent` -> `MovementExpander` -> `TickEntityMotionKind.Move` path.
- `MoveEntity` midpoint anchor commit is classified as a grid transaction primitive, not legacy ordinary Unit movement.
- Boundary v1 suppresses legacy `TickEntityMotionKind.Move` only for locomotion anchor commits and ordinary Unit locomotion leaks. Box/action/topology/spawn/respawn grid transactions retain their existing presentation paths.
- Boundary metadata is trace-only diagnostic data and must not enter canonical replay hashes.
- Boundary v1 / deprecation Phase 1 stabilization adds direct canaries that block flag-on covered ordinary Unit `Move` leaks, allow retained grid transactions, and assert normal movement/finalization traces do not contain unexpected `Boundary=Unknown`. This is fallback isolation, not legacy branch deletion.
- Same-destination ordinary Unit stacking remains the current gameplay contract. Priority-winner regression coverage is asserted through blocking grid transaction candidates in `MovementPhase_SameDestination_OnlyHigherPriorityWins_BoundaryInvariant`.
- Default `KinematicMoveDurationSeconds` is `1f / 3f`, quantized with even ceil. At 60 TPS this is 20 ticks with midpoint anchor commit at tick 10.
- Mid-motion hashes are expected to include the `UnitKinematics` determinism section.
- Mid-motion accepted damage interrupts voluntary locomotion with a `MotionMode.Interrupted` kinematic state; the next flag-on plan tick clears surviving interrupted state to settled-zero.
- Lethal mid-motion damage preserves the interrupted pose until cleanup and then removes the entity plus its kinematic state through `WorldState.RemoveEntity`.
- Settled final hashes are expected to omit settled-zero kinematics.
- Unity scenario coverage lives in `PlayerKinematicLocomotionScenarioTests`.
- Replay determinism coverage lives in `PlayerKinematicLocomotionReplayTests`.

## Golden Policy

- Do not rewrite existing flag-off replay or scenario goldens for this slice.
- Add only explicit flag-on goldens if a downstream lane needs committed artifacts.
- Any flag-on golden diff should be limited to expected kinematic pose state during non-settled ticks and the resulting duration-derived anchor commit tick.
- Mid-motion hit/removal goldens, when needed, may additionally include the interrupted kinematic dump, `KinematicMotionInterrupted`, `KinematicInterruptClosed`, and cleanup removal events.

## Rollback

Set `EnablePlayerSameFaceContinuousLocomotion` to false or pass `GameplayRuntimeFeatureFlags.None`.
The legacy `MovementExpander` path remains present for flag-off fallback and retained grid transactions. It is not migrated in-place.
To reproduce the old 4tick flag-on cadence for migration comparison, set
`PlayerKinematicLocomotionTiming.KinematicMoveDurationSeconds` to `4f / SimulationTicksPerSecond`.
