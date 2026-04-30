# ADR-005: Grid-Authoritative Unit Kinematics

## Status
Accepted for v1.

## Decision
Gameplay spatial authority remains `EntityState.position: SurfaceCell` plus the `WorldState` occupancy layers. Segment-based unit locomotion is represented as additive deterministic fixed-point state in `UnitKinematicRuntimeState`; absent state means settled zero pose at the anchor cell. Player ordinary free-local movement is represented separately by `UnitContinuousLocomotionState`, also with absent state meaning local-zero idle at the anchor.

`WorldState` remains the only authoritative mutable owner. Kinematic state must be read through `WorldSnapshot` and written through `FinalizationBatch`/`IWorldWriteContext`; Transform, Animator, PhysX callbacks, presenter code, and root motion are presentation-only.

## Rules
- `EntityState.position` is the semantic anchor for occupancy, spawn, respawn, topology, push, flip, and enemy brain decisions.
- `UnitKinematicPose` is the read model combining anchor cell and local fixed-point offset.
- `UnitContinuousLocomotionState` is player ordinary movement only. It must not be active on the same entity as `UnitKinematicRuntimeState`.
- `1 cell = 4096` fixed units, with representable local offset range `[-2048, 2047]`.
- Box, wall, terrain, projectile, reservation, topology, and impact disposition remain grid-authoritative in v1.
- v1 player free-local locomotion is 4-direction same-face only; topology seam crossing and continuous box colliders are non-goals.
- Presentation may consume `TickKinematicMotionTrack` or `TickContinuousLocomotionTrack`, but presentation data is never canonical simulation state.

## Consequences
Replay hashes include non-default kinematic runtime state in a deterministic `UnitKinematics` section and non-default player free-local state in a deterministic `UnitContinuousLocomotion` section. Zero/absent kinematic and continuous states are intentionally omitted from those sections to preserve compact canonical dumps and avoid changing settled entity semantics.
