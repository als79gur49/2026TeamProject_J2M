# ChargeMove Presentation Cleanup Readiness

Date: 2026-05-02

## Executive Decision

`TickEntityMotionKind.ChargeMove` is a cleanup candidate, not an immediate deletion target.
The inventory result is: producer current runtime unreachable, synthetic presentation compatibility retained, enum deletion deferred.
Covered Charge active fallback authorization was removed in Phase 6 and diagnostic compatibility does not re-authorize fallback output.
`DefaultGameplayLocomotion`, `GameplayRuntimeFeatureFlags.None`, `RemovedLegacyFallbackDiagnosticBaseline`, and `EnableEnemyChargeKinematicLocomotion` must not produce `ChargeMove`.
Kinematic Charge presentation remains `TickKinematicMotionTrack` plus `TickEnemyChargePresentationSignal`.
Do not delete `TickEntityMotionKind.ChargeMove`, `TickEntityMotionKind.Move`, `MoveEntity`, `MovementExpander`, retained grid transactions, glide retained fallback, or replay/golden vocabulary in this package.

## Producer Inventory

| producer location | condition | reachable after Phase 6? | runtime or synthetic? | expected boundary | expected presentation | current tests | cleanup action | blocker |
|---|---|---|---|---|---|---|---|---|
| `TickResultBuilder.TryResolveMotionKind` / `ShouldUseChargeMovePresentation` | committed `MoveEntity`, `MovementSemanticKind.Move`, enemy unit, `EnemyChargePhase.Active`, no authoritative `MotionMode.Charge` pose | no for covered runtime lanes | retained branch plus synthetic tests | not `LocomotionAnchorCommit`; legacy unsuppressed op only | `TickEntityMotionKind.ChargeMove` if reached | `WorldSnapshotAndPresentationTests` | keep branch, document synthetic reachability | presentation/golden owner |
| legacy active Charge movement path | active Charge emits ordinary `Move` through `EnemyLogic.ResolveBaselineGroundLocomotion` | no | runtime attempt, blocked | rejected before legacy expansion | no `ChargeMove` | boundary and movement phase canaries | add no-producer canaries and rewrite stale expectations | stale tests |
| `RemovedLegacyFallbackDiagnosticBaseline` path | covered Charge fallback attempt with diagnostics enabled | no | runtime diagnostic | no `Boundary=LegacyFallback`; reject `ChargeLegacyFallbackRemovedFromRuntime` | no `ChargeMove` | Phase 6 and cleanup tests | keep diagnostic-only | none |
| `DefaultGameplayLocomotion` | Charge active step | no | runtime kinematic | `UnitSpecialLocomotion` / kinematic payload | `TickKinematicMotionTrack` plus `TickEnemyChargePresentationSignal` | default no-charge canaries | keep absence canary | signal confusion |
| `EnableEnemyChargeKinematicLocomotion` | Charge active step | no | runtime kinematic | `EnemyChargeKinematicActiveStep` | kinematic track, no entity motion | movement phase and replay tests | keep absence canary | none |
| `GameplayRuntimeFeatureFlags.None` | Charge active ordinary `Move` | no | runtime rejected | explicit-baseline-required diagnostic | no `ChargeMove` | Phase 6 and cleanup tests | keep absence canary | none |
| synthetic presentation tests | hand-built movement result with active Charge post-state | yes | synthetic only | bypasses `TickPipeline` | `ChargeMove` | builder/coordinator tests | keep and label compatibility | enum/consumer deletion blocked |

## Consumer Inventory

| consumer location | use kind | runtime required? | test/historical only? | retained until | risk if removed | current tests | action |
|---|---|---|---|---|---|---|---|
| `TickPresentationData.TickEntityMotionKind.ChargeMove` | enum/data contract | no current covered runtime requirement | presentation compatibility | golden/presentation approval | serialization/test break | unit/replay tests | retain |
| `GameplayMotionTimingResolver` | global/entity duration routing | only synthetic/future producer | presentation compatibility | timing owner approval | charge timing override lost | coordinator tests | retain |
| `GameplayTrackPlanner` | `EntityMotions` to local motion clips | generic consumer | not fallback-specific | enum removal approval | host motion planning break | coordinator tests | retain |
| `MotionTrack` / `GameplayEntityPresentationApplier` | linear interpolation and pose application | generic consumer | presentation compatibility | presentation owner approval | visual interpolation drift | coordinator tests | retain |
| `EntityMotionPresentationAuthoring` | per-entity duration override | prefab/authoring compatibility | not runtime fallback | prefab/golden approval | serialized field churn | authoring/prefab tests | retain |
| presentation timing config | `ChargeMoveDurationSeconds` | host config compatibility | not fallback | config migration approval | scene config churn | timing preset tests | retain |
| `WorldSnapshotAndPresentationTests` | synthetic producer canary | no runtime requirement | synthetic presentation compatibility | producer deletion decision | loses branch guard | synthetic builder test | retain and classify |
| `GameplayTickPresentationCoordinatorTests` | host consumer canary | no runtime fallback | synthetic consumer | consumer deletion decision | host regression undetected | timing/interpolation tests | retain |
| `EnemyAiScenarioTests` | stale runtime `ChargeMove` expectations | no | current-policy invalid | cleanup classification | false policy signal | charge flag-off/default tests | rewrite to no `ChargeMove` |
| `TickReplayDeterminismTests` | presentation hash-neutral fixture | no | synthetic replay/hash | replay owner approval | hash contract ambiguity | hash test | keep |

## Reachability Result

| lane | expected result |
|---|---|
| `DefaultGameplayLocomotion` | no `ChargeMove`; use `TickKinematicMotionTrack(MotionMode.Charge)` plus `TickEnemyChargePresentationSignal` |
| `GameplayRuntimeFeatureFlags.None` | no `ChargeMove`; covered Charge attempt rejects with `LegacyOrdinaryFallbackRequiresExplicitBaseline` |
| `RemovedLegacyFallbackDiagnosticBaseline` | no `ChargeMove`; covered Charge attempt rejects with `ChargeLegacyFallbackRemovedFromRuntime` |
| `EnableEnemyChargeKinematicLocomotion` | no `ChargeMove`; active Charge handled by kinematic payload/track |
| synthetic presentation path | `ChargeMove` may be built by direct fixtures that bypass `TickPipeline`; this is presentation compatibility only |

## Tests And Replay

Absence canaries:
- `ChargeMoveCleanup_DefaultGameplay_NoChargeMoveProducer`
- `ChargeMoveCleanup_None_NoChargeMoveProducer`
- `ChargeMoveCleanup_RemovedDiagnosticBaseline_NoChargeMoveProducer`
- `ChargeMoveCleanup_ChargeKinematicFlagOn_NoChargeMoveProducer`
- `ChargeMoveCleanup_ChargePresentationSignal_StillUsedForKinematicCharge`
- `Replay_ChargeMoveCleanup_NoChargeMoveOutput`
- `Replay_ChargeMoveCleanup_DiagnosticBaseline_NoChargeMoveOutput`

Synthetic/retained consumers:
- `TickPresentationDataBuilder_SyntheticChargeMovePresentationCompatibility_BuildsChargeMoveMotion`
- `GameplayTickViewPresenter_EnemyChargeMoveMotionOverride_UsesEntityChargeMoveAuthoring_AndIgnoresUnitMoveOverride`
- `GameplayTickViewPresenter_EnemyChargeMoveWithoutExplicitOverride_UsesGlobalChargeMoveDurationWithMoveInterpolation`
- `DeterminismHash_ChargeMovePresentationData_DoesNotAffectCanonicalStateOrHash`

No replay/golden files are rewritten.
No trace migration is approved.
`LegacyFallback=` remains stable for golden compatibility.

## Final Recommendation

Keep the enum, timing, authoring, host consumers, and synthetic presentation tests.
Keep the `TickResultBuilder.ShouldUseChargeMovePresentation` branch as presentation compatibility until producer deletion and golden owners approve a follow-up package.
Next cleanup candidate is producer-branch guarding/removal only after tests, presentation ownership, and replay/golden ownership are resolved.
Do not delete `TickEntityMotionKind.ChargeMove` in this readiness package.
