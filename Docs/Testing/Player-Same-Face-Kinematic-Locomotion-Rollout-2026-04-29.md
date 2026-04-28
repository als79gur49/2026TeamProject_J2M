# Player Same-Face Kinematic Locomotion Rollout

Date: 2026-04-29

This rollout is guarded by `GameplayRuntimeFeatureFlags.EnablePlayerSameFaceContinuousLocomotion`.
The default remains off for scene hosts, composition-root helpers, replay harnesses, and tests.

## Validation Contract

- Flag off: existing discrete player movement remains the baseline and existing goldens should not be regenerated.
- Flag on: player same-face voluntary moves advance one cell over four ticks using `UnitKinematicRuntimeState`.
- Mid-motion hashes are expected to include the `UnitKinematics` determinism section.
- Settled final hashes are expected to omit settled-zero kinematics.
- Unity scenario coverage lives in `PlayerKinematicLocomotionScenarioTests`.
- Replay determinism coverage lives in `PlayerKinematicLocomotionReplayTests`.

## Golden Policy

- Do not rewrite existing flag-off replay or scenario goldens for this slice.
- Add only explicit flag-on goldens if a downstream lane needs committed artifacts.
- Any flag-on golden diff should be limited to expected kinematic pose state during non-settled ticks and the resulting anchor commit tick.

## Rollback

Set `EnablePlayerSameFaceContinuousLocomotion` to false or pass `GameplayRuntimeFeatureFlags.None`.
The legacy `MovementExpander` path remains present and is not migrated in-place.
