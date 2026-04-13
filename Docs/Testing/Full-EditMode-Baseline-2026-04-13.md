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
  - Unity Core EditMode `18/18`
  - Unity Core PlayMode `2/2`
- `./run_tests.sh full`
  - red
  - Unity Full EditMode `706 total / 112 failed`
  - Unity Full PlayMode는 EditMode failure 때문에 아직 미실행

근거 파일:

- `TestResults/wsl-unity-core-editmode.xml`
- `TestResults/wsl-unity-core-playmode.xml`
- `TestResults/wsl-unity-full-editmode.xml`
- `TestResults/wsl-unity-full-editmode.log`

## Direct touched cluster

다음 클래스는 tick boundary / IR visibility / presentation boundary 작업과 직접 맞닿아 있으므로 후속 PR에서 fail 감소 또는 유지로 관리한다.

- `Game.Feature.Gameplay.Tests.Core.TickPipelineStructureCoreTests` `4`
- `Game.Feature.Gameplay.Tests.Replay.TickReplayDeterminismTests` `10`
- `Game.Feature.Gameplay.Tests.Scenario.AttackPhaseScenarioTests` `11`
- `Game.Feature.Gameplay.Tests.Scenario.EnemyAiScenarioTests` `21`
- `Game.Feature.Gameplay.Tests.Scenario.EnemyViewIsolationTests` `2`
- `Game.Feature.Gameplay.Tests.Scenario.MovementPhaseScenarioTests` `21`
- `Game.Feature.Gameplay.Tests.Scenario.PlayerControlScenarioTests` `4`
- `Game.Feature.Gameplay.Tests.Scenario.TickPipelineExecutionScenarioTests` `1`
- `Game.Feature.Gameplay.Tests.Unit.GameplayTickPresentationCoordinatorTests` `3`
- `Game.Feature.Gameplay.Tests.Unit.GameplayTimingOwnershipTests` `1`
- `Game.Feature.Gameplay.Tests.Unit.StageRuntimeBuilderTests` `1`
- `Game.Feature.Gameplay.Tests.Unit.TickPipelineStructureTests` `1`
- `Game.Feature.Gameplay.Tests.Unit.WorldSnapshotAndPresentationTests` `4`

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
