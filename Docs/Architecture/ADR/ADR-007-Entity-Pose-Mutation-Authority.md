# ADR-007: Adopt EntityPoseMutationAuthority for Position/Facing/Kinematic authoritative mutation

## Status

Accepted

## Context

Position, facing, and kinematic state previously drifted across multiple runtime surfaces. The observed failures were:

- BlackEye rapid rotation during action/windup loops.
- DrSaturn `MoveSuppress` facing drift.
- left-facing/right-moving mismatch after movement commit.
- Recover generic rotate risk through broad movement-facing classification.
- Kinematic movement teleport when ordinary entity motion was used as fallback.
- Kinematic locomotion direction/facing mismatch when movement was treated as `KinematicOnly`.

These failures had the same root shape: a feature lane could produce authoritative pose effects without one shared policy validating source, kind, and metadata.

## Decision

- Position/Facing/Kinematic mutation must pass through `EntityPoseMutationAuthority`.
- Discrete movement carrier and kinematic carrier are separate.
- Discrete `EntityMotions` are not kinematic fallback.
- Kinematic locomotion is `KinematicAndFacing`.
- Kinematic settle/release is `KinematicOnly`.
- Presentation consumes `MovementPresentationRecord`, `KinematicPresentationRecord`, and `TickKinematicMotionTrack`; it does not infer gameplay direction or synthesize authoritative movement.

## Consequences

- Movement probes, movement intents, and presentation-only paths cannot mutate authoritative pose.
- Accepted discrete `MovementCommit` can create `MovementPresentationRecord` only when it changes position and final facing matches movement direction.
- Kinematic enemy locomotion must carry direction and pose facing through the kinematic carrier path.
- Kinematic cleanup/settle/release preserves facing even when local offset changes.
- New action, skill, or rotate-facing producers must use structured metadata/source rather than reason-string inference.

## Rejected alternatives

- Keep only `FacingWriteGate`.
- Clamp or override yaw in the view.
- Synthesize motion from previous/final position diffs.
- Reallow `MovementDerivedProbe`.
- Convert `KinematicOnly` to `EntityMotions` fallback.
- Hide BlackEye behavior with special prefab/view overrides.

## Test enforcement

- Authority tests lock source/kind decisions and reject reasons.
- Carrier tests lock `MovementPresentationRecord` creation to accepted discrete `MovementCommit`.
- Kinematic contract tests lock locomotion direction/facing and settle/release facing preservation.
- Governance tests block direct movement-derived facing writes, `KinematicOnly` to `EntityMotions` fallback, presentation diff motion synthesis, and generic `MovementFacingResolution` to `ExplicitRotate` fallback.
