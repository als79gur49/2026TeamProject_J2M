# Player Same-Face Kinematic Locomotion Rollout

Date: 2026-04-29

This rollout is guarded by `GameplayRuntimeFeatureFlags.EnablePlayerSameFaceContinuousLocomotion`.
The default remains off for scene hosts, composition-root helpers, replay harnesses, and tests.

## Validation Contract

- Flag off: existing discrete player movement remains the baseline and existing goldens should not be regenerated.
- Flag on: player same-face voluntary moves advance using `PlayerKinematicLocomotionTimingSettings`.
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
The legacy `MovementExpander` path remains present and is not migrated in-place.
To reproduce the old 4tick flag-on cadence for migration comparison, set
`PlayerKinematicLocomotionTiming.KinematicMoveDurationSeconds` to `4f / SimulationTicksPerSecond`.
