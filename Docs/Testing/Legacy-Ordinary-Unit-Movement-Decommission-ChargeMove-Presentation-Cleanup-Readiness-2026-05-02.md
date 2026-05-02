# ChargeMove Presentation Cleanup Readiness

Date: 2026-05-02

## Executive Decision

`TickEntityMotionKind.ChargeMove` is a cleanup candidate, not an immediate deletion target.
The inventory result is: producer current runtime unreachable, synthetic presentation compatibility retained, enum deletion deferred.
The producer isolation decision is Option A: remove `ShouldUseChargeMovePresentation` inference from `TickResultBuilder.TryResolveMotionKind` and keep `ChargeMove` only through explicit synthetic presentation data.
Covered Charge active fallback authorization was removed in Phase 6 and diagnostic compatibility does not re-authorize fallback output.
`DefaultGameplayLocomotion`, `GameplayRuntimeFeatureFlags.None`, `RemovedLegacyFallbackDiagnosticBaseline`, `EnableEnemyChargeKinematicLocomotion`, and `AllKinematicLocomotionEnabled` must not produce `ChargeMove`.
Kinematic Charge presentation remains `TickKinematicMotionTrack` plus `TickEnemyChargePresentationSignal`.
Do not delete `TickEntityMotionKind.ChargeMove`, `TickEntityMotionKind.Move`, `MoveEntity`, `MovementExpander`, retained grid transactions, glide retained fallback, or replay/golden vocabulary in this package.

## Producer Inventory

| producer location | condition | reachable after Phase 6? | runtime or synthetic? | expected boundary | expected presentation | current tests | cleanup action | blocker |
|---|---|---|---|---|---|---|---|---|
| `TickResultBuilder.TryResolveMotionKind` | committed `MoveEntity`, `MovementSemanticKind.Move` | no `ChargeMove` inference | runtime builder | not `LocomotionAnchorCommit`; legacy unsuppressed op only | `TickEntityMotionKind.Move` for `MovementSemanticKind.Move` | `ChargeMoveIsolation_RuntimeBuilder_DoesNotInferChargeMove` | keep `Move`; no `ChargeMove` override | none |
| legacy active Charge movement path | active Charge emits ordinary `Move` through `EnemyLogic.ResolveBaselineGroundLocomotion` | no | runtime attempt, blocked | rejected before legacy expansion | no `ChargeMove` | boundary and movement phase canaries | add no-producer canaries and rewrite stale expectations | stale tests |
| `RemovedLegacyFallbackDiagnosticBaseline` path | covered Charge fallback attempt with diagnostics enabled | no | runtime diagnostic | no `Boundary=LegacyFallback`; reject `ChargeLegacyFallbackRemovedFromRuntime` | no `ChargeMove` | Phase 6 and cleanup tests | keep diagnostic-only | none |
| `DefaultGameplayLocomotion` | Charge active step | no | runtime kinematic | `UnitSpecialLocomotion` / kinematic payload | `TickKinematicMotionTrack` plus `TickEnemyChargePresentationSignal` | default no-charge canaries | keep absence canary | signal confusion |
| `EnableEnemyChargeKinematicLocomotion` | Charge active step | no | runtime kinematic | `EnemyChargeKinematicActiveStep` | kinematic track, no entity motion | movement phase and replay tests | keep absence canary | none |
| `AllKinematicLocomotionEnabled` | Charge active step inside full kinematic bundle | no | runtime kinematic | charge kinematic payload | kinematic track, no entity motion | runtime reachability matrix | keep absence canary | bundle drift |
| `GameplayRuntimeFeatureFlags.None` | Charge active ordinary `Move` | no | runtime rejected | explicit-baseline-required diagnostic | no `ChargeMove` | Phase 6 and cleanup tests | keep absence canary | none |
| synthetic presentation tests | explicit `TickPresentationData` / host fixture with `TickEntityMotionKind.ChargeMove` | yes | synthetic only | bypasses `TickPipeline` and runtime builder inference | `ChargeMove` | synthetic data/coordinator tests | keep and label compatibility | enum/consumer deletion blocked |

## Consumer Inventory

| consumer location | use kind | runtime required? | test/historical only? | retained until | risk if removed | current tests | action |
|---|---|---|---|---|---|---|---|
| `TickPresentationData.TickEntityMotionKind.ChargeMove` | enum/data contract | no current covered runtime requirement | presentation compatibility | golden/presentation approval | serialization/test break | unit/replay tests | retain |
| `GameplayMotionTimingResolver` | global/entity duration routing | only synthetic/future producer | presentation compatibility | timing owner approval | charge timing override lost | coordinator tests | retain |
| `GameplayTrackPlanner` | `EntityMotions` to local motion clips | generic consumer | not fallback-specific | enum removal approval | host motion planning break | coordinator tests | retain |
| `MotionTrack` / `GameplayEntityPresentationApplier` | linear interpolation and pose application | generic consumer | presentation compatibility | presentation owner approval | visual interpolation drift | coordinator tests | retain |
| `EntityMotionPresentationAuthoring` | per-entity duration override | prefab/authoring compatibility | not runtime fallback | prefab/golden approval | serialized field churn | authoring/prefab tests | retain |
| presentation timing config | `ChargeMoveDurationSeconds` | host config compatibility | not fallback | config migration approval | scene config churn | timing preset tests | retain |
| `WorldSnapshotAndPresentationTests` | synthetic compatibility canary | no runtime requirement | synthetic presentation compatibility | producer isolation decision | loses explicit compatibility coverage | explicit synthetic presentation data test | retain and classify |
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
| `AllKinematicLocomotionEnabled` | no `ChargeMove`; active Charge still handled by kinematic payload/track |
| synthetic presentation path | `ChargeMove` may be built by direct fixtures that bypass `TickPipeline`; this is presentation compatibility only |

## Producer Isolation Decision

| option | decision | reason |
|---|---|---|
| remove runtime builder inference | choose | smallest change that makes `TickResultBuilder` unable to infer `ChargeMove` from active Charge state |
| force helper false | defer | leaves a dead producer-looking branch behind |
| source metadata guard | defer | metadata/API churn is unnecessary for this isolation |

`ChargeMove` compatibility is explicit synthetic presentation data only. A current runtime `ChargeMove` result is a regression.

## Tests And Replay

Absence canaries:
- `ChargeMoveCleanup_DefaultGameplay_NoChargeMoveProducer`
- `ChargeMoveCleanup_None_NoChargeMoveProducer`
- `ChargeMoveCleanup_RemovedDiagnosticBaseline_NoChargeMoveProducer`
- `ChargeMoveCleanup_ChargeKinematicFlagOn_NoChargeMoveProducer`
- `ChargeMoveCleanup_ChargePresentationSignal_StillUsedForKinematicCharge`
- `ChargeMoveProducer_DefaultGameplay_Unreachable`
- `ChargeMoveProducer_None_Unreachable`
- `ChargeMoveProducer_RemovedDiagnosticBaseline_Unreachable`
- `ChargeMoveProducer_ChargeKinematicFlagOn_Unreachable`
- `ChargeMoveProducer_AllKinematic_Unreachable`
- `ChargeMoveProducer_RuntimeReachabilityMatrix_IsCurrent`
- `ChargeMoveProducer_ChargeKinematicSignal_IsNotChargeMove`
- `ChargeMoveIsolation_RuntimeBuilder_DoesNotInferChargeMove`
- `ChargeMoveIsolation_DefaultGameplay_NoChargeMove`
- `ChargeMoveIsolation_None_NoChargeMove`
- `ChargeMoveIsolation_RemovedDiagnosticBaseline_NoChargeMove`
- `ChargeMoveIsolation_ChargeKinematicFlagOn_NoChargeMove`
- `ChargeMoveIsolation_AllKinematic_NoChargeMove`
- `Replay_ChargeMoveCleanup_NoChargeMoveOutput`
- `Replay_ChargeMoveCleanup_DiagnosticBaseline_NoChargeMoveOutput`
- `Replay_ChargeMoveProducer_RuntimeMatrix_NoChargeMove`
- `Replay_ChargeMoveProducer_DiagnosticBaseline_NoChargeMove`
- `Replay_ChargeMoveIsolation_NoRuntimeChargeMove`
- `Replay_ChargeMoveIsolation_DiagnosticBaseline_NoChargeMove`

Synthetic/retained consumers:
- `ChargeMoveProducer_SyntheticCompatibility_StillBuildsChargeMove`
- `ChargeMoveIsolation_SyntheticCompatibility_CanStillBuildChargeMove`
- `ChargeMoveIsolation_ConsumerCompatibility_Retained`
- `GameplayTickViewPresenter_EnemyChargeMoveMotionOverride_UsesEntityChargeMoveAuthoring_AndIgnoresUnitMoveOverride`
- `GameplayTickViewPresenter_EnemyChargeMoveWithoutExplicitOverride_UsesGlobalChargeMoveDurationWithMoveInterpolation`
- `DeterminismHash_ChargeMovePresentationData_DoesNotAffectCanonicalStateOrHash`

No replay/golden files are rewritten.
No trace migration is approved.
`LegacyFallback=` remains stable for golden compatibility.

## Final Recommendation

Keep the enum, timing, authoring, host consumers, and synthetic presentation tests.
Keep synthetic `ChargeMove` compatibility through explicit presentation data and host consumer tests.
Next cleanup candidate is enum/consumer retirement only after presentation and replay/golden owners approve it.
Do not delete `TickEntityMotionKind.ChargeMove` in this readiness package.
