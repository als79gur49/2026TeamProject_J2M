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

## Locomotion vs Grid Transaction Boundary v1

- Unit locomotion is time-based Unit movement state: player Free2D ordinary movement, player kinematic fallback, enemy ordinary kinematic movement, Charge active kinematic steps, and future special locomotion such as jump, phase, forced motion, or knockback.
- Grid transaction is immediate canonical grid or anchor materialization: spawn, respawn, cleanup removal, box push/flip/action materialization, topology materialization, scripted relocation, and continuous/kinematic anchor normalization.
- `MoveEntity` is an anchor/grid transaction primitive. It is not the ordinary Unit movement abstraction.
- Legacy ordinary Unit movement is the path where an ordinary Unit `MovementCommandKind.Move` reaches `MovementExpander`, commits through `MoveEntity`, and presents as legacy `TickEntityMotionKind.Move` or Charge `TickEntityMotionKind.ChargeMove`.
- Legacy ordinary Unit movement is the deprecation target. Legacy grid transactions are retained in Boundary v1.
- Flag-on player Free2D, player kinematic fallback, enemy ordinary kinematic, and Charge kinematic paths must not emit legacy ordinary Unit movement presentation. Their presentation sources are `TickContinuousLocomotionTrack` or `TickKinematicMotionTrack`.
- Box/action/topology/spawn/respawn/cleanup/scripted relocation may continue to use `MoveEntity` and legacy grid transaction presentation.
- Boundary metadata is diagnostic contract data, not canonical gameplay state. `MovementExecutionBoundaryKind` and boundary reason text may appear in traces, but they must not affect replay canonical hashes.
- Presentation suppression is limited to `LocomotionAnchorCommit` and `UnitOrdinaryLocomotion` `MoveEntity` operations. `BoxActionMovement`, `TopologyMaterialization`, `SpawnRespawnPlacement`, `ScriptedRelocation`, and retained legacy grid transactions must keep their required presentation motions.
- `Unknown` is acceptable only for genuinely unclassified diagnostics during migration. Presentation-relevant movement, placement, and grid transaction paths should emit an explicit boundary kind.

## Consequences
Replay hashes include non-default kinematic runtime state in a deterministic `UnitKinematics` section and non-default player free-local state in a deterministic `UnitContinuousLocomotion` section. Zero/absent kinematic and continuous states are intentionally omitted from those sections to preserve compact canonical dumps and avoid changing settled entity semantics.
