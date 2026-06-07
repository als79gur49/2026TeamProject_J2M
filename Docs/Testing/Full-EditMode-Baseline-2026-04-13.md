# Full EditMode Baseline 2026-04-13

이 문서는 tick boundary / vocabulary / IR visibility 정리 후속 작업의 검증 baseline을 고정한다.

## Confirmed commands

```bash
./run_tests.sh core
./run_tests.sh full
./run_tests.sh --integration-simulation
./run_tests.sh --integration-replay
./run_tests.sh --integration-fuzz
```

## Current known status

- `./run_tests.sh core`
  - green
  - Unity Core EditMode `13/13`
  - Unity Core PlayMode `2/2`
- `./run_tests.sh full`
  - red
  - Unity Full EditMode `703 total / 101 failed`
  - Unity Full PlayMode는 EditMode failure 때문에 아직 미실행

근거 파일:

- `TestResults/wsl-unity-core-editmode.xml`
- `TestResults/wsl-unity-core-playmode.xml`
- `TestResults/wsl-unity-full-editmode.xml`
- `TestResults/wsl-unity-full-editmode.log`

## Direct touched cluster

다음 클래스는 tick boundary / IR visibility / presentation boundary 작업과 직접 맞닿아 있으므로 후속 PR에서 fail 감소 또는 유지로 관리한다.

- `Game.Feature.Gameplay.Tests.Core.TickPipelineStructureCoreTests` `4`
- `Game.Feature.Gameplay.Tests.Replay.TickReplayDeterminismTests` `7`
- `Game.Feature.Gameplay.Tests.Scenario.AttackPhaseScenarioTests` `9`
- `Game.Feature.Gameplay.Tests.Scenario.EnemyAiScenarioTests` `21`
- `Game.Feature.Gameplay.Tests.Scenario.EnemyViewIsolationTests` `2`
- `Game.Feature.Gameplay.Tests.Scenario.MovementPhaseScenarioTests` `17`
- `Game.Feature.Gameplay.Tests.Scenario.PlayerControlScenarioTests` `4`
- `Game.Feature.Gameplay.Tests.Scenario.TickPipelineExecutionScenarioTests` `0`
- `Game.Feature.Gameplay.Tests.Unit.GameplayTickPresentationCoordinatorTests` `3`
- `Game.Feature.Gameplay.Tests.Unit.RuntimeBoardBoundsGuardTests` `0`
- `Game.Feature.Gameplay.Tests.Unit.GameplayTimingOwnershipTests` `1`
- `Game.Feature.Gameplay.Tests.PlayMode.PlayerMovementPlayModeTests` `0`
- `Game.Feature.Gameplay.Tests.Unit.StageRuntimeBuilderTests` `1`
- `Game.Feature.Gameplay.Tests.Unit.WorldSnapshotAndPresentationTests` `4`

## Producer-side IR cleanup snapshot

- declaration changes
  - `ImpactReservation`의 public metadata fields `SourceActionPlanId`, `ReservationSequence` 제거
  - `ActionGroup`, `ActionGroupKind`, `ActionGroupComparer`를 `internal`로 축소
- usage changes
  - `TickTraceFormatter`에서 `Attack.NormalizedInputs` section 제거
  - reservation trace formatter에서 `Group=` / `Sequence=` 제거
  - runtime ordering/provenance는 internal metadata path로 유지

## Structural replacement execution snapshot

- dead helper removal
  - `CanonicalPathResultExtensions.cs`는 제거했다.
  - live support는 `CanonicalPhaseResultFactory.cs`로 분리했다.
  - live `ResolveAcceptedActions()` call site는 test tree와 tooling registry 모두 `0`이다.
- `SortedInputs` removal
  - `TickPipelineStructureSimulationTests`의 remaining `SortedInputs` oracle `2`건을 semantic assertions로 교체했다.
  - `AttackPhaseResult.SortedInputs` declaration / ctor arg / backing field를 제거했다.
  - `TickPipeline.AttackPlanBuildResult.SortedInputs` internal pass-through carrier도 함께 제거했다.
- behavioral replacement anchors
  - `TickReplayDeterminismTests`
    - `Replay_AttackLogicRegistrationPermutation_ProducesSameHashTraceAndEventLog`
    - `Replay_OffBottomEnemySuppression_ProducesStableEmptyAttackSurface`
    - `Replay_TopologyRotation_CancelsEnemyActionBeforeAttackCollection_WithoutAttackArtifacts`
  - `TickPipelineStructureSimulationTests`
    - movement sorting은 유지하되 attack는 `RawIntents` / no-side-effect oracle로 이동
- core structural thinning
  - `TickPipelineStageOneCoreTests`에서 order-focused structural tests `3`건 제거
  - `AttackInputNormalizationCoreTests`에서 normalized-order / synthetic-intent structural tests `2`건 제거
- stratification/tooling shrink
  - `Tools/gameplay_test_stratification_lib.py`에서 `ResolveAcceptedActions(` / `SortedInputs` old pattern registry를 제거했다.
  - manifest / overrides / report는 regenerated 상태다.
  - `run_tests.sh`, checker, generator, manifest physical removal은 아직 하지 않았다.
- scaffolding/tooling completion snapshot
  - `TickPipelineStratificationInfrastructureTests.cs`와 its Unity-side duplicate support를 active chain에서 제거했다.
  - persisted manifest는 `fullyQualifiedName`, `category`, `mode`만 남기는 runner-selection cache로 축소했다.
  - checker/generator/lib는 report/assertion-summary generation을 중단하고 manifest/source consistency + governance summary만 유지한다.
  - [Docs/Archive/Architecture/Gameplay-Test-Stratification.md](../Archive/Architecture/Gameplay-Test-Stratification.md)는 generated report가 아니라 archived historical/non-canonical note로 유지한다.
  - `Docs/Testing/Gameplay-Test-Automation-Guide.md`는 stale baseline 대신 본 baseline 문서와 ADR를 truth-source로 가리킨다.
- validation readout
  - `python3 Tools/check_gameplay_test_stratification.py --root /mnt/c/Users/user/2026TeamProject_J2M --mode strict` pass
  - `./run_tests.sh core` green 유지
  - `./run_tests.sh full` red 유지, full EditMode `703 total / 101 failed`
  - direct touched cluster는 증가하지 않았고 `TickPipelineStructureTests`는 `1 -> 0`, `TickPipelineExecutionScenarioTests`는 `1 -> 0`, `TickReplayDeterminismTests`는 `7 유지`
  - unrelated 신규 fail class `0`

## Friendship reduction snapshot

- removed friends
  - gameplay main `InternalsVisibleTo`
    - `Game.Integration.Fuzz.Tests`
    - `Game.Feature.Gameplay.PlayModeTests`
  - host assembly `InternalsVisibleTo`
    - `Game.Integration.Simulation.Tests`
    - `Game.Integration.Replay.Tests`
    - `Game.Integration.Fuzz.Tests`
    - `Game.Feature.Gameplay.PlayModeTests`
  - gameplay test-support `InternalsVisibleTo`
    - `Game.TestInfrastructure`
- keep rationale
  - gameplay main의 `Game.Feature.Gameplay.Tests`, `Game.Core.Tests`, `Game.Integration.Simulation.Tests`, `Game.Integration.Replay.Tests`, `Game.TestInfrastructure`는 direct internal construction, replay support, structure guard 때문에 유지한다.
  - replay support assembly의 `Game.Integration.Fuzz.Tests` friendship은 fuzz replay harness가 `TickReplayHarness`, `IReplayTickAwareEntityLogic`, `TickReplayFrame`를 계속 사용하므로 유지한다.
  - host assembly의 `Game.Feature.Gameplay.Tests` friendship은 `FlipInteractionPlannerInternalTests`, `EntityEffectPresentationRuntimePolicyInternalTests` 같은 intentional host-internal tests 때문에 유지한다.
- validation readout
  - friendship reduction은 semantic/runtime behavior를 바꾸지 않는 declaration-only cleanup으로 집행한다.
  - baseline expectation은 계속 `./run_tests.sh core` green, `./run_tests.sh full` red, full EditMode `703 total / 101 failed`다.
  - gameplay main `-> Host`, gameplay main `-> PlayModeTests`, host `-> PlayModeTests` 제거는 모두 landed 상태다.

## Host/playmode seam cleanup snapshot

- gameplay-main internal seam replacement
  - `GameplayHostRuntimeFactory`는 더 이상 `SessionStartEntityNormalizer`나 `SnapshotBuilder`를 직접 사용하지 않는다.
  - session-start projectile cadence 보정은 host-local helper로 이동했다.
  - authoritative snapshot capture는 `GameplayCompositionRoot.CreateSnapshot(WorldState)` public static seam으로 이동했다.
- presentation final-state seam
  - `TickResult.FinalTopology`는 host/view가 commit-complete topology만 읽는 public semantic seam으로 승격했다.
  - `GameplayTickPresentationCoordinator`와 `GameplayTrackPlanner`는 더 이상 gameplay-main internal topology member에 묶이지 않는다.
- small config seam
  - `EnemyInactiveVisualController.ConfigureLegacyColorFallback`와 `AllowLegacyColorFallback`는 primitive enemy fallback policy용 public config seam으로 승격했다.
  - `DefaultGameplayEntityViewFactory`는 더 이상 gameplay-main internal config member에 묶이지 않는다.
- PlayMode seam cleanup
  - `PlayerMovementPlayModeTests`의 topology-only assertion은 `Presenter.CurrentTopology`로 이동했다.
  - authoritative entity/state oracle은 `GameplayCompositionRoot.CreateSnapshot(host.WorldState)` public static seam으로 이동했다.
  - `GameplayCameraRig` internal method/property direct call은 file-local reflection adapter로 치환했다.
  - serialized private-field reflection helper는 계속 유지한다.
- validation readout
  - `python3 Tools/check_gameplay_test_stratification.py --root /mnt/c/Users/user/2026TeamProject_J2M --mode strict` pass
  - `python3 Tools/generate_gameplay_test_stratification.py --root /mnt/c/Users/user/2026TeamProject_J2M --check` pass
  - `./run_tests.sh core` green 유지
  - `./run_tests.sh full` red 유지, full EditMode `703 total / 101 failed`
  - direct touched cluster는 `RuntimeBoardBoundsGuardTests 0`, `PlayerMovementPlayModeTests 0` 포함해 증가하지 않았다.
  - targeted PlayMode readout 기준 `GameplayInputHost_MovePresentation_DoesNotBlockSubsequentTicks`는 direct Unity `-runTests` path에서도 pass했다.
- next-step gate
  - friendship cleanup sequencing은 Host landed 뒤 PlayMode landed까지 닫혔다.

## Host friendship reduction snapshot

- Plan A decision
  - gameplay main `InternalsVisibleTo("Game.Feature.Gameplay.Host")`를 제거한다.
  - host runtime / enemy-presentation runtime 재감사에서 hidden gameplay-main internal consumer는 발견되지 않았다.
- host public seam readout
  - `GameplayHostRuntimeFactory`는 `GameplayCompositionRoot.CreateSnapshot(WorldState)` public static seam만 사용한다.
  - `GameplayTickPresentationCoordinator`, `GameplayTrackPlanner`는 presentation-commit final state only scope의 public `TickResult.FinalTopology`를 사용한다.
  - `DefaultGameplayEntityViewFactory`는 public `EnemyInactiveVisualController.ConfigureLegacyColorFallback`만 사용한다.
- PlayMode reduction note
  - `PlayerMovementPlayModeTests`의 `WorldState.CreateSnapshot(` direct call은 `0`이다.
  - `PlayerMovementPlayModeTests`의 gameplay-main / host compile-time internal dependency는 `0`이고, `GameplayCameraRig` access는 file-local reflection adapter만 남아 있다.
  - file-local reflection adapter는 temporary pragmatic seam이며 IVT가 필요한 compile-time access가 아니므로 gameplay main / host assembly의 `Game.Feature.Gameplay.PlayModeTests` friendship은 이번 PR에서 제거했다.
  - `TopologyTransitionCameraShake_PlayMode_DirectAndCinemachinePaths_SharePulseTimingAndReset`의 parity failure는 1) compile-time IVT dependency와 직접 관련이 없고 2) reflection path setup 자체는 통과하며 3) baseline command set 밖이므로 nongating later issue로 유지한다.
- validation readout
  - `python3 Tools/check_gameplay_test_stratification.py --root /mnt/c/Users/user/2026TeamProject_J2M --mode strict` pass
  - `python3 Tools/generate_gameplay_test_stratification.py --root /mnt/c/Users/user/2026TeamProject_J2M --check` pass
  - `./run_tests.sh core` green 유지
  - `./run_tests.sh full` red 유지, full EditMode `703 total / 101 failed`
  - touched cluster 증가 없음

## Runner/tooling physical removal snapshot

- Plan A decision
  - manifest-free runner path가 landed 상태다.
  - `run_tests.sh`는 더 이상 `GameplayTestStratificationManifest.json`을 요구하거나 `gameplay_test_stratification_lib.py`를 직접 import하지 않는다.
  - `TestRunnerCliBootstrap`의 Core PlayMode selection은 `Game.Feature.Gameplay.PlayModeTests` assembly + `[Category("Core")]` filter로 고정됐다.
- physical removal
  - `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/TestSupport/GameplayTestStratificationManifest.json`와 its `.meta`를 제거했다.
  - `GameplayWorldStateTestFactory.cs`에서는 live `CreateBounded(...)` block을 유지하고 dead `GameplayCliTestRunner` / manifest DTO / loader block만 제거했다.
- tooling shrink
  - `Tools/check_gameplay_test_stratification.py`는 persisted manifest consistency 대신 source category / override inventory / governance summary를 검증한다.
  - `Tools/generate_gameplay_test_stratification.py`는 manifest writer가 아니라 `--check` compatibility validation wrapper로 축소한다.
  - `Tools/gameplay_test_stratification_lib.py`는 runner helper를 잃고 checker/generator backend + metrics helper만 유지한다.
  - `GameplayTestStratificationOverrides.json`는 101 locked category/contract/reason metadata source이므로 no-touch로 유지한다.
- validation readout
  - `python3 Tools/check_gameplay_test_stratification.py --root /mnt/c/Users/user/2026TeamProject_J2M --mode strict` pass
  - `python3 Tools/generate_gameplay_test_stratification.py --root /mnt/c/Users/user/2026TeamProject_J2M --check` pass
  - `./run_tests.sh core` green 유지
  - `./run_tests.sh full` red 유지, full EditMode `703 total / 101 failed`
  - touched cluster 증가 없음
  - unrelated 신규 fail class `0`

## Host-internal seam execution snapshot

- scope split
  - 이 범위는 gameplay-main friendship이 아니라 `Gameplay_Host` internal-heavy test seam을 실제로 분리하는 execution PR로 고정한다.
  - entity-effect split landed 뒤, 이번 execution으로 `FlipInteractionPresentationTests.cs` mixed file도 분리했다.
- current readout
  - `Game.Feature.Gameplay.Tests.Unit.GameplayFlipInteractionObservableTests`
    - new public observable file 기준 full EditMode XML `1 total / 0 failed`
  - `Game.Feature.Gameplay.Tests.Unit.FlipInteractionPlannerInternalTests`
    - new internal planner file 기준 full EditMode XML `2 total / 0 failed`
  - flip subtotal
    - split 후 full EditMode XML 합계는 `3 total / 0 failed`
  - `Game.Feature.Gameplay.Tests.Unit.EntityEffectPresentationAuthoringTests`
    - retained public file 기준 full EditMode XML `18 total / 3 failed`
    - current unrelated baseline cluster에 그대로 남아 있다.
  - `Game.Feature.Gameplay.Tests.Unit.EntityEffectPresentationRuntimePolicyInternalTests`
    - internal policy file 기준 full EditMode XML `2 total / 0 failed`
  - entity-effect subtotal
    - `20 total / 3 failed`
- execution result
  - old mixed file/class `FlipInteractionPresentationTests.cs`는 제거했다.
  - public observable row는 새 `GameplayFlipInteractionObservableTests.cs`로 옮겼다.
    - execute / active 동안 committed root local pose는 불변이어야 한다.
    - Historical baseline: execute / active 동안 player hand IK target과 `BoxFlipInteractionDriver` visual root는 base/rest pose에서 이탈해야 했다. Current product removes the player hand IK path and keeps the box-side visual root expectation.
    - completion 후 broad phase는 `EntityMotion -> Idle`로 돌아오고, child visuals는 base/rest pose로 복귀해야 한다.
  - planner/phase machine rows는 새 `FlipInteractionPlannerInternalTests.cs`로 옮겼다.
    - `FlipInteractionTrack` / `FlipInteractionPhase` phase machine 검증 1건
    - `GameplayTrackPlanner` lifecycle/reset bookkeeping 검증 1건
  - `GameplayTrackPlanner` bookkeeping과 `FlipInteractionTrack` phase machine은 public seam으로 승격하지 않고 intentional internal coverage로 유지한다.
  - entity-effect split landed 상태는 그대로 유지한다.
- next-step gate
  - mixed host-internal test file backlog는 닫혔다.
  - 이후 남는 범위는 intentional internal coverage hygiene, docs archive, runner residual cleanup뿐이다.
- validation readout
  - `python3 Tools/check_gameplay_test_stratification.py --root /mnt/c/Users/user/2026TeamProject_J2M --mode strict` pass
  - `python3 Tools/generate_gameplay_test_stratification.py --root /mnt/c/Users/user/2026TeamProject_J2M --check` pass
  - `./run_tests.sh core` green 유지
  - `./run_tests.sh full` red 유지, full EditMode `703 total / 101 failed`
  - touched cluster 증가 없음
  - unrelated 신규 fail class `0`

## Docs archive cleanup snapshot

- canonical/active docs keep set
  - repo-level entrypoint는 `README.md`다.
  - gameplay docs canonical entrypoint는 `Docs/Architecture/README.md`다.
  - active truth-source chain은 canonical spec, rules appendix, ADR, testing guide, pinned baseline으로 고정한다.
- archive move
  - historical/non-canonical architecture docs는 `Docs/Archive/Architecture/`로 이동했다.
  - `Docs/Archive/README.md`는 archive index와 canonical re-entry link를 제공한다.
- link cleanup
  - active docs는 old top-level historical architecture paths 대신 archive path 또는 current truth-source만 가리킨다.
  - [Docs/Archive/Architecture/Gameplay-Test-Stratification.md](../Archive/Architecture/Gameplay-Test-Stratification.md)는 archived historical note로 유지한다.

## Current unrelated baseline cluster

다음 클래스는 현재 full baseline에는 포함되어 있지만, boundary cleanup PR의 직접 수정 대상은 아니다. 후속 PR에서는 신규 unrelated failure `0`을 유지해야 한다.

- `Game.Feature.Gameplay.Tests.Unit.CombinedGameplayShowcaseInstallerTests` `4`
- `Game.Feature.Gameplay.Tests.Unit.EnemyLogicTests` `5`
- `Game.Feature.Gameplay.Tests.Unit.EnemyPrefabScaffoldTests` `1`
- `Game.Feature.Gameplay.Tests.Unit.EntityEffectPresentationAuthoringTests` `3`
- `Game.Feature.Gameplay.Tests.Unit.GameplayShowcaseScaffoldTests` `1`
- `Game.Feature.Gameplay.Tests.Unit.GameplayViewProjectionTests` `11`
- `Game.Feature.Gameplay.Tests.Unit.PlayerMovementInputTests` `2`
- `Game.Feature.Gameplay.Tests.Unit.TopologyTransitionPostFxTests` `1`

## Gating rule

- 모든 후속 PR은 `./run_tests.sh core` green을 유지해야 한다.
- `./run_tests.sh full`은 per-class fail histogram으로 비교한다.
- direct touched cluster는 fail 감소 또는 유지가 허용된다.
- unrelated baseline cluster는 신규 fail `0`이 조건이다.
- full PlayMode 및 integration suites는 full EditMode의 direct touched cluster가 안정화된 뒤 실행한다.
