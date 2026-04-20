# ADR-001 Tick Boundary And IR Visibility

## Status
- Accepted

## Context
- `WorldState`와 `WorldSnapshot`은 이미 layered occupancy model을 구현하고 있는데, 문서와 테스트는 `TryGetUnitAt`, `IsBlockedForUnit`, `BlocksMovement`를 구조 vocabulary처럼 취급하고 있었다.
- host runtime 일부는 `TickResult.PresentationData` 대신 `CleanupPhaseResult`를 직접 읽고 있었다.
- attack path에는 `AttackIntent`, `AttackInputKind`, synthetic intent normalization, `PhaseTransientBuffer` 같은 implementation IR이 public/canonical language에 노출돼 있었다.

## Decision
- canonical query vocabulary는 layered query 중심으로 정리한다.
  - `EnumerateUnitsAt`
  - `TryGetSolidOccupantAt`
  - `TryPickImpactTargetAt`
  - `TryGetUnitTraversalBlocker`
- `TryGetUnitAt`, `IsBlockedForUnit`, `BlocksMovement`는 legacy compatibility API로 강등한다.
- `ImpactReservation`은 유지한다. 다만 external public contract가 아니라 inter-phase gameplay contract로 본다.
- `AttackIntent`, `AttackInputKind`, synthetic normalized input shape는 phase-private IR로 본다.
- `PhaseTransientBuffer`는 historical shim으로 남기고, narrower role name `ImpactReservationBuffer`를 기준으로 정리한다.
- host/view는 `TickResult.PresentationData`만 소비한다.

## Consequences
- black-box scenario tests는 impact/blocked/recovery, cleanup occupancy, determinism, presentation isolation 중심으로 이동한다.
- trace token schema와 synthetic normalized input shape를 직접 검증하는 테스트는 우선순위가 내려간다.
- old blueprint / implementation / governance docs는 non-canonical historical documents로 강등한다.

## Follow-up Decisions
- `ActionGroup`, `ActionGroupKind`, `ActionGroupComparer`
  - 이번 시리즈에서는 runtime core 내부 compatibility IR로 유지한다.
  - public/test/doc/trace surface에서 먼저 끊고, 이후 runtime core 밖 참조가 정리되면 `internal`로 축소한다.
- `ImpactReservation`
  - semantic contract는 유지한다.
  - `SourceActionGroupId`, `ReservationSequence`는 trace/tests decoupling 이후 제거 대상이다.
- `ImpactReservationBuffer`
  - 최종 inter-phase handoff는 `ImpactReservationBuffer`로 본다.
  - explicit `MovementPhaseResult -> Attack input` handoff는 현재 `MovementPhaseResult`가 `SortedIntents`, `ExpandedCandidates`, `PlanFinalizationBatch`, `PreMovementStatePhaseResult` 등 internal IR aggregate를 포함하므로 도입하지 않는다.
- docs archive
  - canonical truth-source는 `Docs/Architecture/README.md`, `Docs/Architecture/Tick-Simulation-Canonical-Spec.md`, `Docs/Architecture/Gameplay-Rules-Appendix.md`, 본 ADR, `Docs/Testing/Gameplay-Test-Automation-Guide.md`, `Docs/Testing/Full-EditMode-Baseline-2026-04-13.md`로 고정한다.
  - old blueprint / implementation / rulebook / prompt / structure docs는 `Docs/Archive/Architecture/` historical archive로 이동한다.
  - `Docs/Archive/README.md`는 archive index이며 canonical truth-source가 아니다.
- friendship
  - friendship reduction PR의 P0-A 제거 범위는 다음으로 제한한다:
    - gameplay main `InternalsVisibleTo("Game.Integration.Fuzz.Tests")`
    - host assembly `InternalsVisibleTo("Game.Integration.Simulation.Tests")`
    - host assembly `InternalsVisibleTo("Game.Integration.Replay.Tests")`
    - host assembly `InternalsVisibleTo("Game.Integration.Fuzz.Tests")`
    - `Gameplay_Tests/EditMode/InternalsVisibleTo.cs`의 `InternalsVisibleTo("Game.TestInfrastructure")`
  - host/playmode seam cleanup PR에서는 friendship declaration을 지우지 않고 blocker member만 먼저 제거한다.
    - `GameplayHostRuntimeFactory`의 `SessionStartEntityNormalizer` 의존은 host-local semantic helper로 치환한다.
    - `GameplayHostRuntimeFactory`의 `SnapshotBuilder` 의존은 `GameplayCompositionRoot.CreateSnapshot(WorldState)` public static seam으로 치환한다.
    - `GameplayTickPresentationCoordinator`와 `GameplayTrackPlanner`는 `TickResult.FinalTopology` public semantic seam만 읽는다.
    - 이 seam의 scope는 presentation-commit final state only로 제한한다.
    - `DefaultGameplayEntityViewFactory`는 `EnemyInactiveVisualController.ConfigureLegacyColorFallback` small public config seam만 사용한다.
    - `PlayerMovementPlayModeTests`는 `WorldState.CreateSnapshot` direct call을 버리고 `GameplayCompositionRoot.CreateSnapshot(host.WorldState)`로 이동한다.
    - `PlayerMovementPlayModeTests`의 `GameplayCameraRig` internal method/property direct call은 PlayMode-local reflection adapter로 치환한다.
  - Host final friendship reduction PR에서는 Plan A를 택했다.
    - host runtime / enemy-presentation runtime의 gameplay-main internal consumer 재감사 결과, `GameplayHostRuntimeFactory`, `GameplayTickPresentationCoordinator`, `GameplayTrackPlanner`, `DefaultGameplayEntityViewFactory`를 포함해 hidden gameplay-main internal consumer는 더 이상 발견되지 않았다.
    - gameplay main `InternalsVisibleTo("Game.Feature.Gameplay.Host")`는 제거했다.
    - host runtime은 `GameplayCompositionRoot.CreateSnapshot(WorldState)`, public `TickResult.FinalTopology`, `TickResult.PresentationData`, public `EnemyInactiveVisualController.ConfigureLegacyColorFallback`만으로 동작한다.
  - PlayMode final friendship reduction PR에서도 Plan A를 택했다.
    - gameplay main `InternalsVisibleTo("Game.Feature.Gameplay.PlayModeTests")`와 host assembly `InternalsVisibleTo("Game.Feature.Gameplay.PlayModeTests")`를 함께 제거했다.
    - `PlayerMovementPlayModeTests`의 gameplay-main / host compile-time blocker는 모두 제거됐고, actual remaining access는 `GameplayCompositionRoot.CreateSnapshot(host.WorldState)` public seam과 file-local reflection adapter뿐이다.
    - file-local `GameplayCameraRig` reflection adapter는 temporary pragmatic seam이며, compile-time IVT dependency가 아니므로 PlayMode friendship 제거 gate를 막지 않는다.
    - `TopologyTransitionCameraShake_PlayMode_DirectAndCinemachinePaths_SharePulseTimingAndReset`의 parity failure는 1) compile-time IVT dependency와 직접 관련이 없고 2) reflection path setup 자체는 통과하며 3) baseline command set 밖이므로 nongating PlayMode-later diagnostic issue로 유지한다.
  - `Gameplay_Tests/EditMode/InternalsVisibleTo.cs`에서는 `Game.TestInfrastructure` friend를 제거한다.
  - 남는 friend는 phase-private result / replay harness / structure guard처럼 intentional internal test coverage가 있는 경우만 유지한다.
  - host-side unit tests 중 `FlipInteractionPlannerInternalTests`, `EntityEffectPresentationRuntimePolicyInternalTests`는 gameplay-main friendship 문제와 분리된 intentional host-IVT coverage로 남긴다.
  - sequencing은 Host-first / PlayMode-later 원칙을 유지한 채 닫았다.
    - `Game.Feature.Gameplay.Host` removal은 이전 PR에서 landed했다.
    - 이번 PR에서 `Game.Feature.Gameplay.PlayModeTests` removal도 landed 상태로 만들었다.
    - 다음 later work는 host-internal test seam refactor와 PlayMode camera parity follow-up으로만 남긴다.
- host-internal test seam refactor
  - 이 범위는 gameplay-main friendship 문제가 아니라 `Gameplay_Host` assembly 내부 type/member를 직접 쓰는 test seam 문제로 분리한다.
  - `FlipInteractionPresentationTests` split execution landed
    - old mixed file/class `FlipInteractionPresentationTests`는 제거했다.
    - public observable row는 새 `GameplayFlipInteractionObservableTests`로 옮겼다.
      - committed root transform invariance를 `GameplayEntityView.transform.localPosition` baseline으로 읽는다.
      - `PlayerFlipInteractionDriver` hand IK target이 execute / active 동안 hand rest pose에서 이탈하고 completion 후 rest pose로 복귀하는지 본다.
      - `BoxFlipInteractionDriver` visual root local offset / rotation이 execute / active 동안 base pose에서 이탈하고 completion 후 base pose로 복귀하는지 본다.
      - public coordinator는 flip-specific signal을 노출하지 않으므로 broad oracle은 `GameplayTickViewPresenter.CurrentPresentationPhase`의 `EntityMotion -> Idle`, `IsPresentationActive true -> false`, root/child transform 차이만 사용한다.
    - planner/phase machine rows는 새 `FlipInteractionPlannerInternalTests`로 옮겼다.
      - `FlipInteractionTrack_PhasesTransitionFromWindupToRecoveryToComplete`는 `FlipInteractionTrack` / `FlipInteractionPhase` phase machine 자체를 검증하므로 internal 유지한다.
      - `GameplayTrackPlanner_FlipInteractionTrack_CancelAndTargetLossRemoveTrackSafely`는 `GameplayTrackPlanner`, `GameplayPresentationTrackState.FlipInteractionTracks`, `FlipInteractionResetRequests`를 직접 읽는 planner bookkeeping 검증이므로 internal 유지한다.
    - planner bookkeeping과 phase machine을 public seam으로 승격하지 않는 원칙은 그대로 유지한다.
  - `EntityEffectPresentationAuthoringTests` split execution landed
    - 기존 `EntityEffectPresentationAuthoringTests` file/class는 retained public-facing file로 유지했다.
    - `EntityEffectPresentationAuthoring.CreateSnapshot()/Validate()`, prefab surface checks, public `DefaultGameplayEntityViewFactory.CreateView(...)`, public `EnemyViewPrefabRequirements.ValidateEnemyViewPrefab(...)`만 남겨 public bucket으로 정규화했다.
    - 기존 file/class를 유지한 이유는 baseline continuity, git diff 최소화, 그리고 기존 `Player_S1.prefab` failing rows 비교 용이성 때문이다.
    - `GameplayMotionTimingResolver.ResolveVisibilityDurationSeconds(...)`를 직접 읽는 death-tail max-duration / animator-speed invariance 검증 두 건은 새 `EntityEffectPresentationRuntimePolicyInternalTests`로 이동했다.
    - `PlayerViewPrefabRequirements.ValidatePlayerViewPrefab(...)` reflection helper 경로는 public `DefaultGameplayEntityViewFactory.CreateView(...)` 기반으로 치환했고, tiny helper fallback은 이번 PR에서 필요하지 않았다.
  - execution 결과, host-internal seam 시리즈의 mixed-file split backlog는 닫혔다.
    - entity-effect split은 landed했다.
    - flip split도 landed했다.
    - 이후 남는 범위는 intentional internal coverage hygiene, docs archive, runner residual cleanup뿐이다.
- internal naming
  - `PreMovementState` family rename은 boundary closeout 이후 별도 internal naming cleanup으로 미룬다.

## Producer-side IR Cleanup Follow-up
- `ImpactReservation`
  - public semantic surface는 `SourceId`, `TargetId`, `Position`, `Damage`, `TickGenerated`만 남긴다.
  - `SourceActionGroupId`, `ReservationSequence`는 public contract에서 제거하고 internal provenance/order metadata로만 유지한다.
  - same-tick reservation drain ordering과 replay determinism은 유지한다.
- result carrier provenance naming
  - post-plan runtime/canonical carrier의 plan correlation key는 `ActionPlanId`로 통일한다.
  - `DamageResolutionRecord.GroupId`, `DestroyResolutionRecord.GroupId`, `DelayedAttackEffectRecord.SourceActionGroupId`는 compatibility alias로만 남기고 새 runtime reader는 읽지 않는다.
  - `IntentId`는 canonical internal carry-forward ID로 유지하되, result carrier의 semantic field와 혼동하지 않는다.
- `AttackPhaseResult.SortedInputs`
  - external/debug consumer에서는 제거했다.
  - `TickTraceFormatter`의 `Attack.NormalizedInputs` section은 제거했다.
  - structural/scaffolding replacement execution에서 declaration과 internal pass-through carrier를 함께 제거했다.
- `ActionGroup` family
  - `ActionGroup`, `ActionGroupKind`, `ActionGroupComparer`는 runtime core internal compatibility IR로 고정한다.
  - public visibility는 줄이되, direct test construction 제거와 docs cleanup은 후속 PR에서 별도로 다룬다.

## Scaffolding Replacement Sequencing
- `CanonicalPathResultExtensions`
  - dead helper block는 execution PR에서 제거했다.
  - live factory/support surface만 `CanonicalPhaseResultFactory`로 분리해 유지한다.
- `AttackPhaseResult.SortedInputs`
  - execution PR에서 `TickPipelineStructureSimulationTests`의 ordering / alive-only attack collection assertions을 semantic oracle로 치환했다.
  - 그 다음 late slice에서 `TickPipeline.AttackPlanBuildResult.SortedInputs`와 `AttackPhaseResult.SortedInputs`를 함께 제거했다.
  - 따라서 남은 후속 범위에는 `SortedInputs` deletion 자체가 아니라 tooling / runner decoupling만 남는다.
- structural/core replacement coverage
  - `TickPipelineStageOneCoreTests`와 `AttackInputNormalizationCoreTests`의 order-focused structural cases는 replacement coverage가 landed된 뒤 제거했다.
  - execution PR에서 P0 behavioral anchors는 다음 의미를 black-box semantic surface로 잡는다:
    - attack ordering determinism
    - topology rotation / off-bottom attack suppression
    - same-tick impact reservation consumption
    - cleanup occupancy policy
    - `TickResult -> Presenter` isolation
    - replay hash / trace / dump equality
- stratification governance
  - tooling physical removal PR에서는 Plan A를 택했고 manifest-free runner path를 landed 상태로 전환했다.
  - `run_tests.sh`는 더 이상 manifest path를 주입하지 않고 `gameplay_test_stratification_lib.py`를 직접 import하지 않는다.
  - strict governance는 checker entrypoint를 통해 유지하고, PlayMode Core selection은 bootstrap의 `assemblyNames + categoryNames("Core")` path로 전환한다.
- scaffolding/tooling completion
  - `TickPipelineStratificationInfrastructureTests` 제거와 report/assertion-summary 축소 이후, `GameplayWorldStateTestFactory`의 dead `GameplayCliTestRunner` / manifest loader block도 이번 PR에서 제거한다.
  - persisted manifest `GameplayTestStratificationManifest.json`은 runner-selection cache 역할을 마치고 이번 PR에서 제거한다.
  - `Tools/check_gameplay_test_stratification.py`, `Tools/generate_gameplay_test_stratification.py`, `Tools/gameplay_test_stratification_lib.py`는 source category / override inventory / governance summary만 유지하는 shrink-only 상태로 남긴다.
  - `GameplayTestStratificationOverrides.json`는 101 locked category/contract/reason metadata source이므로 no-touch로 유지한다.
  - [Docs/Archive/Architecture/Gameplay-Test-Stratification.md](../../Archive/Architecture/Gameplay-Test-Stratification.md)는 archived historical/non-canonical note로 유지한다.
  - docs archive cleanup landed 뒤 남은 후속 범위는 checker/generator/lib/overrides physical removal과 runner residual cleanup으로 한정한다.
- keep targets
  - `TickPipelineStructureCoreTests`와 `TickPipelineStructureCoreArchitectureTests`는 old IR scaffolding 제거 대상이 아니라 boundary / architecture keep tests로 유지한다.
