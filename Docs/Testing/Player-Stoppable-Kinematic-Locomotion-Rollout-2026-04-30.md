# Player Stoppable Kinematic Locomotion Rollout

Date: 2026-04-30

This rollout is guarded by `GameplayRuntimeFeatureFlags.EnablePlayerStoppableKinematicLocomotion`.
It is effective only when `EnablePlayerSameFaceContinuousLocomotion` is also enabled.
If `EnablePlayerFree2DLocalLocomotion` is enabled, player ordinary movement bypasses the Held/reverse/queue branch and uses `UnitContinuousLocomotionState`; this rollout remains the fallback when the free2D flag is off.

## Validation Contract

- Flag off: player same-face kinematic locomotion keeps the existing automatic continuation behavior.
- Flag on: releasing actual held movement input during player voluntary same-face kinematic movement stores `MotionMode.Held`.
- `MotionMode.Held` preserves anchor, local offset, elapsed ticks, total ticks, commit tick, started tick, and step direction.
- Held progress does not advance until the same held direction is pressed again.
- Same-direction resume switches back to `MotionMode.Voluntary` on the resume tick; progress advances on the following tick.
- Opposite input while held is accepted as same-edge reverse. The reverse tick reinterprets progress without advancing it, so the world pose does not snap.
- Perpendicular input while held is queued in `PlayerControlState.queuedKinematicTurnDirection`, resumes the current segment forward, and tries the queued move on the next Plan tick after settled-zero.
- Queued perpendicular movement is revalidated at consume time; success and blocked/rejected attempts both clear the queue.
- Held remains non-settled, so push/flip/action preview paths continue to require settled-zero pose.
- Held/reverse state is included in replay hashes through the existing `UnitKinematics` determinism section, and queued turns are included through `PlayerControl`.

## Rollback

Set `EnablePlayerStoppableKinematicLocomotion` to false to restore automatic player kinematic continuation while keeping player kinematic locomotion enabled.
Set `EnablePlayerSameFaceContinuousLocomotion` to false to return to the legacy discrete player movement baseline.
