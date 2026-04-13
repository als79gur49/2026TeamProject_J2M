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
  - Host final friendship reduction PR에서는 Plan A를 택한다.
    - host runtime / enemy-presentation runtime의 gameplay-main internal consumer 재감사 결과, `GameplayHostRuntimeFactory`, `GameplayTickPresentationCoordinator`, `GameplayTrackPlanner`, `DefaultGameplayEntityViewFactory`를 포함해 hidden gameplay-main internal consumer는 더 이상 발견되지 않았다.
    - gameplay main `InternalsVisibleTo("Game.Feature.Gameplay.Host")`는 제거한다.
    - host runtime은 `GameplayCompositionRoot.CreateSnapshot(WorldState)`, public `TickResult.FinalTopology`, `TickResult.PresentationData`, public `EnemyInactiveVisualController.ConfigureLegacyColorFallback`만으로 동작한다.
  - `Game.Feature.Gameplay.PlayModeTests` friendship도 gameplay main / host 둘 다 이번 PR에서 유지한다.
    - `PlayerMovementPlayModeTests`의 gameplay-main compile-time blocker는 제거됐고, camera rig access는 file-local reflection adapter만 남아 있다.
    - Host-first / PlayMode-later sequencing과 failure attribution 분리를 위해 gameplay main / host 양쪽 friendship 제거는 다음 PR로 미룬다.
  - `Gameplay_Tests/EditMode/InternalsVisibleTo.cs`에서는 `Game.TestInfrastructure` friend를 제거한다.
  - 남는 friend는 phase-private result / replay harness / structure guard처럼 intentional internal test coverage가 있는 경우만 유지한다.
  - host-side unit tests 중 `FlipInteractionPresentationTests`, `EntityEffectPresentationAuthoringTests`는 gameplay-main friendship 문제와 분리된 host-IVT blocker로 남긴다.
  - 이 PR 이후 sequencing은 Host-first / PlayMode-later로 고정한다.
    - 이번 PR에서 `Game.Feature.Gameplay.Host` removal을 landed한다.
    - `Game.Feature.Gameplay.PlayModeTests` removal은 그 다음 PR에서 follow-up으로 처리한다.
- internal naming
  - `PreMovementState` family rename은 boundary closeout 이후 별도 internal naming cleanup으로 미룬다.

## Producer-side IR Cleanup Follow-up
- `ImpactReservation`
  - public semantic surface는 `SourceId`, `TargetId`, `Position`, `Damage`, `TickGenerated`만 남긴다.
  - `SourceActionGroupId`, `ReservationSequence`는 public contract에서 제거하고 internal provenance/order metadata로만 유지한다.
  - same-tick reservation drain ordering과 replay determinism은 유지한다.
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
  - `run_tests.sh`는 checker, python stratification library, manifest path injection에 직접 묶여 있다.
  - execution PR에서는 old structural vocabulary registry만 제거하고 manifest / report를 regenerated 상태로 맞춘다.
  - manifest / overrides / tooling physical removal은 runner decoupling sequencing 없이 섞지 않는다.
- scaffolding/tooling completion
  - completion PR에서는 Plan A를 택한다.
  - `TickPipelineStratificationInfrastructureTests`는 제거하고, `GameplayWorldStateTestFactory`에서는 `LoadOverrides`, override DTO, source discovery, `ReportRelativePath`를 함께 제거한다.
  - persisted manifest는 runner/bootstrap selection에 필요한 `fullyQualifiedName`, `category`, `mode`만 남기는 slim schema로 축소한다.
  - `Tools/check_gameplay_test_stratification.py`, `Tools/generate_gameplay_test_stratification.py`, `Tools/gameplay_test_stratification_lib.py`는 report/assertion-summary generation을 active governance chain에서 제거한다.
  - `Docs/Architecture/Gameplay-Test-Stratification.md`는 generated truth-source가 아니라 historical/non-canonical note로 강등한다.
  - 이 PR 이후 남는 runner decoupling 범위는 manifest-free selection path 도입과 checker/generator/lib/manifest/overrides physical removal로만 요약된다.
- keep targets
  - `TickPipelineStructureCoreTests`와 `TickPipelineStructureCoreArchitectureTests`는 old IR scaffolding 제거 대상이 아니라 boundary / architecture keep tests로 유지한다.
