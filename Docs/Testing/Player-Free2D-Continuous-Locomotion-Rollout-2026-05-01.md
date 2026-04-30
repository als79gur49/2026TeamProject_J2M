# Player Free2D Continuous Locomotion Rollout

Date: 2026-05-01

This rollout is guarded by `GameplayRuntimeFeatureFlags.EnablePlayerFree2DLocalLocomotion`.
When enabled, player ordinary movement uses `UnitContinuousLocomotionState` before the stoppable kinematic, same-face kinematic, and legacy discrete movement paths. Enemy ordinary movement, charge, jump, phase, forced motion, and existing fallback kinematic behavior remain on `UnitKinematicRuntimeState`.

## Validation Contract

- Applies only to player ordinary movement from `PlayerTickCommand.HeldMoveDirection`.
- Movement is 4-direction same-face axis motion only; diagonal input and topology seam free crossing are rejected or clamped.
- `EntityState.position` remains the semantic anchor cell for occupancy, contact, push, flip, action preview, spawn, respawn, and topology decisions.
- `UnitContinuousLocomotionState` stores deterministic fixed-point local offset, velocity, facing, last move direction, speed, mode, sequence, and residual remainders.
- Absent continuous state means local-zero idle at the anchor. Local-nonzero idle must remain present.
- A unit cannot have active `UnitKinematicRuntimeState` and active `UnitContinuousLocomotionState` at the same time.
- Local offset normalizes the anchor at the half-cell boundary. `+2048` is never stored; positive blocked clamp is `+2047`.
- Collision is grid-authoritative: wall, terrain, box, solid, board edge, and topology edge block; unit overlap remains allowed.
- Passive contact remains anchor-cell based. Visual overlap before anchor normalization does not trigger neighbor contact.
- Push, flip, and action preview require local-zero settled pose; local-nonzero idle and moving continuous pose reject settled probes.
- Presentation consumes authoritative continuous local pose through `TickContinuousLocomotionTrack`. Transform, Animator, PhysX, and root motion are not simulation authority.
- Nonlethal hit, lethal hit, removal, death hold, cleanup, and respawn preserve or purge continuous pose through the same authoritative write path as other state.

## Flag Hierarchy

Player ordinary movement dispatch order:

1. `EnablePlayerFree2DLocalLocomotion`
2. `EnablePlayerStoppableKinematicLocomotion`
3. `EnablePlayerSameFaceContinuousLocomotion`
4. Legacy discrete movement

The free2D flag is independent of enemy and charge kinematic flags. Turning it off must restore the existing player stoppable/same-face/legacy behavior without changing enemy or charge movement.

## Known Limitations

- No enemy, charge, jump, phase, glide, forced motion, or knockback migration.
- No diagonal movement.
- No topology seam free crossing; same-face edge movement clamps or rejects.
- No continuous box collider, footprint contact, swept combat, or projectile collision redesign.
- No mid-pose push/flip/action execution. These remain settled-only.
- Contact timing is anchor-based, not visual-footprint based.

## Golden Policy

- Do not regenerate flag-off goldens for this slice.
- Free2D flag-on hashes may add a `UnitContinuousLocomotion` section while continuous state is present.
- Explicit idle-zero state and absent state should be canonical-equivalent after storage normalization.
- Replay validation should cover stop, turn, clamp, hit/death, cleanup, and respawn sequences.

## Rollback

Set `EnablePlayerFree2DLocalLocomotion` to false.
The player ordinary movement path then falls back to `EnablePlayerStoppableKinematicLocomotion`, then `EnablePlayerSameFaceContinuousLocomotion`, then legacy discrete movement. No data migration is required because continuous local-zero idle is represented by absent state.

