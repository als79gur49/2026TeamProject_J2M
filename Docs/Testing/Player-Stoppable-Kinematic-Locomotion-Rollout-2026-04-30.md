# Player Stoppable Kinematic Locomotion Rollout

Date: 2026-04-30

This rollout is guarded by `GameplayRuntimeFeatureFlags.EnablePlayerStoppableKinematicLocomotion`.
It is effective only when `EnablePlayerSameFaceContinuousLocomotion` is also enabled.

## Validation Contract

- Flag off: player same-face kinematic locomotion keeps the existing automatic continuation behavior.
- Flag on: releasing actual held movement input during player voluntary same-face kinematic movement stores `MotionMode.Held`.
- `MotionMode.Held` preserves anchor, local offset, elapsed ticks, total ticks, commit tick, started tick, and step direction.
- Held progress does not advance until the same held direction is pressed again.
- Same-direction resume switches back to `MotionMode.Voluntary` on the resume tick; progress advances on the following tick.
- Opposite or perpendicular input while held is rejected for v1 and does not turn or reverse.
- Held remains non-settled, so push/flip/action preview paths continue to require settled-zero pose.
- Held is included in replay hashes through the existing `UnitKinematics` determinism section.

## Rollback

Set `EnablePlayerStoppableKinematicLocomotion` to false to restore automatic player kinematic continuation while keeping player kinematic locomotion enabled.
Set `EnablePlayerSameFaceContinuousLocomotion` to false to return to the legacy discrete player movement baseline.
