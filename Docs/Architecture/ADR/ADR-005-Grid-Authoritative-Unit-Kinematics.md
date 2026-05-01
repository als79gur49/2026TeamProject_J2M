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
- Enemy glide visual height is carried by `TickEnemyGlidePresentationSignal` as presentation-only fixed units. The host converts that height to an additive offset along the current `SurfaceCell` face normal after resolving the base pose. This signal does not change `EntityState.position`, `UnitKinematicRuntimeState`, `UnitContinuousLocomotionState`, collision/contact behavior, or canonical replay hashes. With `EnableEnemyGlideKinematicLocomotion`, active glide horizontal chase movement uses the enemy kinematic lane with `MotionMode.Voluntary`; continuation is allowed only for voluntary segments that started inside the authoritative glide active window. Flag-off and default-bundle paths retain the documented legacy fallback baseline until readiness adopts the flag.

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
- Boundary v1 stabilization closes the remaining `MovementPhaseScenarioTests` failures as follows: blocked flip landing remains blocked without `MoveCommitted`, impact reservation, damage, or destroy mark; ordinary Unit same-destination movement remains stack-capable, and priority-winner coverage is now asserted through blocking grid transactions instead of stale ordinary-Unit rejection wording.
- `BoxImpact` and box slide `Stop` action groups are `BoxActionMovement` boundaries. Player topology transactions are allowed through the legacy/grid transaction guard even when player ordinary locomotion flags are enabled.
- Normal gameplay movement/finalization traces must not leave meaningful operations as `Boundary=Unknown`; only explicitly synthetic test-only operations may remain unknown.
- Legacy ordinary Unit movement deletion requires a separate readiness gate. `GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion` is the explicit default-on bundle for player Free2D, player Action Assist, player kinematic fallback, enemy ordinary kinematic, and Charge kinematic canaries, while `GameplayRuntimeFeatureFlags.None` remains the flag-off rollback/golden baseline. Enemy glide active kinematic locomotion is guarded by `EnableEnemyGlideKinematicLocomotion` and is intentionally not included in `DefaultGameplayLocomotion` through Glide Default Adoption Readiness v1.
- Default bundle adoption is not deletion. Scene hosts may opt in explicitly through host configuration, but composition roots, bootstrapper defaults, replay harness defaults, historical tests, fallback tests, and golden baselines must continue to preserve `None` unless a test or host opts in directly.
- Jump start, airborne, and landing are classified as `UnitSpecialLocomotion`; phase relocation remains `ScriptedRelocation`; glide start/end remain state-only special movement candidates. Active glide chase movement is a flag-gated kinematic candidate: flag-on starts and continues a voluntary kinematic segment, uses `LocomotionAnchorCommit` with reason `GlideActiveKinematicAnchorCommit`, preserves solid blocker bypass, keeps anchor-based contact timing, enters LandingPending when active ends on a solid overlap, and composes horizontal `TickKinematicMotionTrack` with `TickEnemyGlidePresentationSignal`. Forced motion and knockback have vocabulary in `UnitKinematicRuntimeState` through `MotionMode.Forced` and `ForcedMotionOp.Knockback`, but there is no gameplay producer in readiness v3. Any future forced/knockback producer must define an explicit special or kinematic boundary before shipping.
- `Boundary=Unknown` is unacceptable for anchor-changing `MoveEntity`, movement presentation, grid transaction, unit special movement, spawn/respawn/topology/scripted relocation, or ordinary Unit locomotion finalization. It is acceptable only for documented state-only, diagnostic, debug-only, or synthetic test-only operations.
- The readiness inventory and deletion checklist live in `Docs/Testing/Legacy-Ordinary-Unit-Movement-Deprecation-Readiness-2026-05-01.md`.
- Legacy ordinary Unit movement deletion remains blocked after readiness v3 until replay/golden migration policy is decided. The deletion draft phases in the readiness document are planning gates only and do not remove `MoveEntity`, `MovementExpander`, retained grid transactions, or flag-off fallback baselines.

## Consequences
Replay hashes include non-default kinematic runtime state in a deterministic `UnitKinematics` section and non-default player free-local state in a deterministic `UnitContinuousLocomotion` section. Zero/absent kinematic and continuous states are intentionally omitted from those sections to preserve compact canonical dumps and avoid changing settled entity semantics.
