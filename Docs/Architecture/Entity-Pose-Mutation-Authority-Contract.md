# Entity Pose Mutation Authority Contract

## 1. Purpose

`EntityPoseMutationAuthority` exists to keep authoritative `Position`, `Facing`, and `Kinematic` writes behind one gameplay policy. AI, movement, action, kinematic, and presentation lanes must not directly decide gameplay pose mutation through ad-hoc writes.

Plan and Resolve stages create semantic requests or presentation carriers. Authoritative pose writes close through `FinalizationBatch.ApplyTo` and the `WorldState` write path only after `EntityPoseMutationAuthority` allows the request. Presentation lanes consume carriers; they do not create authoritative pose.

## 2. Historical Failure Modes

- Movement-derived facing pre-write:
  - movement could be suppressed or rejected while facing had already changed.
- FacingWriteGate-only solution:
  - a facing-only gate could not validate the combined Position/Facing/Kinematic lifecycle.
- MovementAccepted != PositionChanged:
  - accepted no-op or facing resolution paths could open facing-only rotation.
- Discrete carrier as fallback:
  - converting kinematic movement to `EntityMotions` loses teleport, skill, settle, and release semantics.
- Kinematic movement as KinematicOnly:
  - the visual track moved while authoritative facing stayed at the old value.
- View-side fake motion:
  - synthesizing tracks from previous/final position diffs can make respawn, cleanup, topology relocation, or other non-locomotion changes look like movement.

## 3. Authoritative Write Boundary

- `WorldState` is the authoritative mutable gameplay owner.
- Plan/Resolve stages produce staged operations, semantic requests, and presentation carriers.
- Authoritative pose writes close only through `FinalizationBatch.ApplyTo` and the underlying `WorldState` mutation path.
- Feature logic does not directly mutate `EntityState.position`, `EntityState.facing`, or `UnitKinematicState` for gameplay pose purposes.
- Feature logic creates semantic request/carrier data. Only mutations allowed by `EntityPoseMutationAuthority` are applied.

## 4. EntityPoseMutationAuthority Responsibility

EntityPoseMutationAuthority is the sole gameplay policy for authoritative Position/Facing/Kinematic mutation.

EntityPoseMutationAuthority는 authoritative Position/Facing/Kinematic mutation의 단일 gameplay policy owner다.

## 5. PoseMutationSource / PoseMutationKind Matrix

| Source | Allowed mutation | Conditions |
|---|---|---|
| MovementProbe | none | 후보 계산만 가능 |
| MovementIntent | none | accepted 전 상태, authoritative write 금지 |
| MovementCommit | PositionAndFacing | accepted, not suppressed, position changed, facing == movement direction |
| CombatActionStart | FacingOnly | explicit action facing metadata 필요 |
| CombatActionHold | FacingOnly | active action sequence lock |
| CombatActionRelease | FacingOnly 또는 none | release semantic에 따라 명시 |
| MovementSkillStart | FacingOnly 또는 skill-defined | explicit skill metadata 필요 |
| MovementSkillActive | KinematicAndFacing 또는 skill-defined | skill direction metadata 필요 |
| ExplicitRotateAction | FacingOnly | explicit rotate metadata 필요 |
| KinematicLocomotion | KinematicAndFacing | kinematic direction 필요, facing == direction |
| KinematicMovementSkill | KinematicAndFacing | skill/kinematic direction 필요 |
| KinematicHold | KinematicOnly | facing 변경 금지 |
| KinematicRelease | KinematicOnly | facing 변경 금지 |
| KinematicSettle | KinematicOnly | preserve facing |
| PresentationOnly | none | authoritative mutation 금지 |

## 6. Discrete Movement Policy

Discrete EntityMotions are not the canonical enemy locomotion path. They are valid only for accepted discrete MovementCommit presentation. Kinematic enemy locomotion must use KinematicPresentationRecord / TickKinematicMotionTrack, never EntityMotions fallback.

Discrete EntityMotions는 일반 enemy locomotion의 canonical path가 아니다. Discrete EntityMotions는 accepted discrete MovementCommit presentation에만 유효하다. Kinematic enemy locomotion은 반드시 KinematicPresentationRecord / TickKinematicMotionTrack 경로를 사용해야 하며, EntityMotions fallback으로 변환하면 안 된다.

`MovementPresentationRecord` is created only when all of these are true:

- `Source=MovementCommit`
- `Kind=PositionAndFacing`
- `PositionChanged=true`
- `MovementAccepted=true`
- `MovementSuppressed=false`
- authority decision `Allowed=true`

`MovementPresentationRecord` / discrete `EntityMotions` must not be used as fallback for `KinematicOnly`, `KinematicSettle`, `KinematicRelease`, action facing, explicit rotate, spawn, respawn, or cleanup relocation.

## 7. Kinematic Locomotion Policy

General enemy locomotion is canonical through the kinematic path.

- Kinematic locomotion uses `KinematicPresentationRecord` and `TickKinematicMotionTrack`.
- Real locomotion is `KinematicAndFacing`.
- `KinematicDirection == FacingAfter == PoseFacing`.
- The first same-anchor locomotion tick may have no anchor delta, so `Velocity`, cardinal direction, or explicit skill direction can classify `KinematicLocomotion`.
- Gameplay direction is not inferred from world transforms or visual diffs.

## 8. Kinematic Settle / Release / Cleanup Policy

`KinematicHold`, `KinematicRelease`, and `KinematicSettle` are cleanup/settle-style kinematic lifecycle paths.

- These paths are `KinematicOnly`.
- Facing is preserved.
- Local offset settle direction and `PoseFacing` may differ.
- `FacingPolicy=PreserveFacing` is required.
- Settle/release must not be promoted to locomotion.

## 9. Explicit Action / Skill / Rotate Facing Policy

- `CombatActionStart`, `CombatActionHold`, and `CombatActionRelease` allow facing-only mutation only with explicit action metadata.
- Jump, Charge, and utility movement skills allow facing or kinematic-facing mutation only with explicit skill metadata.
- `ExplicitRotateAction` allows facing-only mutation only with explicit rotate metadata.
- Generic `MovementFacingResolution` is not `ExplicitRotate`.
- Reason strings are not sufficient proof of explicit rotate intent. Structured metadata/source must carry that decision.

## 10. Presentation Carrier Rules

- `MovementPresentationRecord` is the discrete `MovementCommit` presentation carrier.
- `KinematicPresentationRecord` is the kinematic locomotion/settle carrier.
- `TickEntityMotion` / `EntityMotions` are not kinematic fallback.
- `TickKinematicMotionTrack` is the kinematic interpolation carrier.
- `PresentationOnly` cannot create authoritative mutation.
- Views do not infer direction. They consume carrier `PoseFacing` / `Direction`.

## 11. Forbidden Patterns

- Direct `EntityState.WithFacing` / `writeContext.SetFacing` from AI probe or movement intent code.
- `MovementDerivedProbe` / `MovementDerivedIntent` changing authoritative facing.
- Treating `MovementAccepted=true` as `PositionChanged=true`.
- Classifying generic `MovementFacingResolution` as `ExplicitRotate`.
- Creating `EntityMotions` from `KinematicOnly`, `KinematicSettle`, or `KinematicRelease`.
- Synthesizing movement tracks in presentation from previous/final position diffs.
- Inferring gameplay direction from world-space visual transforms.
- Changing facing during `KinematicSettle` / `KinematicRelease` cleanup.
- Hiding movement/facing problems with BlackEye prefab or view overrides.

## 12. Diagnostics

| Diagnostic | Contract checked |
|---|---|
| `[EntityPoseMutation]` | authority source/kind decision, explicit metadata, accepted/suppressed/position-changed state, facing-direction match |
| `[MovementPresentationRecord]` | discrete carrier creation only for accepted `MovementCommit` presentation |
| `[MotionTrackBuild]` | host track build consumes discrete carriers and reports non-kinematic motion build decisions |
| `[KinematicPresentationRecord]` | kinematic carrier creation only for kinematic mutation requests and valid kinematic state deltas |
| `[KinematicTrackBuild]` | host kinematic track build consumes `TickKinematicMotionTrack` carrier fields |
| `[KinematicViewApply]` | view application reports pose/direction mismatch for kinematic locomotion presentation |
| `[EntityFacingWrite]` | direct facing-write warning surface for legacy/current writer audit |
| `[BlackEyeWindup]` | BlackEye windup/action timing surface used to diagnose facing/movement drift in action phases |

## 13. Test Coverage Map

| Contract | Coverage |
|---|---|
| Movement probes/intents/presentation cannot mutate pose | `EntityPoseMutationAuthorityTests`, `PoseMutationArchitectureGovernanceTests` |
| MovementCommit requires accepted, unsuppressed, position-changed, direction-matched commit | `EntityPoseMutationAuthorityTests`, `PoseMutationPresentationCarrierTests` |
| Generic movement facing is not explicit rotate | `EntityPoseMutationAuthorityTests`, `PoseMutationArchitectureGovernanceTests` |
| Discrete carrier is MovementCommit-only | `PoseMutationPresentationCarrierTests`, `TickResultBuilder` presentation tests |
| Kinematic locomotion is KinematicAndFacing | `KinematicLocomotionFacingContractCoreTests` |
| Kinematic settle/release preserve facing | `KinematicLocomotionFacingContractCoreTests`, `EntityPoseMutationAuthorityTests` |
| Kinematic carriers do not create EntityMotions | `KinematicLocomotionFacingContractCoreTests`, `PoseMutationArchitectureGovernanceTests` |
| Presentation does not synthesize fake motion from snapshot diffs | `PoseMutationArchitectureGovernanceTests` |
| Diagnostics include clear reject/create reasons | `EntityPoseMutationAuthorityTests`, `PoseMutationPresentationCarrierTests`, `KinematicLocomotionFacingContractCoreTests` |
