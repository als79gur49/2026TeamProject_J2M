# ChancePanel Production Launch Visibility Fix

Date: 2026-05-12

## Symptom

MainMenu에서 production campaign stage를 실행했는데 HUD의 ChancePanel이 표시되지 않았다.

표면 증상은 UI inactive였지만, 실제 실패 지점은 UI 조건이 아니라 HUD read source 주입 및 chance 값 전달이었다.

## Root Causes

### 1. Stale editor direct-play context

`EditorDirectPlayContextStore`에 이전 editor direct-play context가 남아 있으면 MainMenu production launch도 direct-play launch처럼 해석될 수 있었다.

확인된 실패 경로는 두 가지다.

- `EditorDirectPlayMode.CampaignTempSlot` stale context가 남으면 installer가 production save namespace가 아니라 temp direct-play namespace를 본다.
- `EditorDirectPlayMode.NonCampaign` stale context가 남으면 `SuppressCampaignFlow=true`로 평가되어 campaign runtime activation이 꺼진다.

그 결과 `StageBackedGameplayShowcaseInstallerBase`에서 `CampaignRuntimeActive=false`가 되고 `CampaignChancesReadSource`가 생성되지 않았다.

후속 흐름은 다음과 같았다.

```text
CampaignChancesReadSource == null
-> GameplayHostPlayerHudQuery hasRemainingChances=false
-> UIChanceSlice.HasChances=false
-> ChancePanelViewModel.HasChances=false
-> ChancePanelView root inactive
```

### 2. Inactive display override out-param contamination

stale context를 끊은 뒤에도 ChancePanel이 보이지 않는 경로가 하나 더 있었다.

`SaveSlotCampaignChancesReadSource.TryReadChances()`는 기본 `maxChances`를 `SaveSlotStore.DefaultRemainingChances`로 세팅한다. 하지만 `CampaignChanceDisplayOverride.TryRead(out remaining, out max)`가 비활성 상태에서도 out 파라미터에 내부 기본값 `0`을 써버렸다.

이 때문에 override가 실제로 적용되지 않았는데도 `maxChances`가 `0`으로 오염된 상태로 save slot read 경로가 계속 진행될 수 있었다.

결과적으로 source는 주입되어도 HUD에는 `maxChances=0`이 전달되고, ChancePanel은 여전히 숨겨졌다.

## Fix

### Production launch boundary

`ConfiguredGameplayStageLaunchRouter.Launch(...)`에서 production stage transition 시작 전에 stale direct-play 상태를 명시적으로 정리한다.

순서는 다음과 같다.

```text
EditorDirectPlayContextStore.Clear()
EditorDirectPlayContextStore.ClearTempDirectPlaySave()
StageLaunchContextStore.SetCurrent(request.StageId)
SceneTransitionCoordinator.TryStartStageTransition(...) 또는 scene load
```

이 경계는 MainMenu production launch 전용이다. direct-play launcher나 installer의 direct-play 정책은 우회하지 않는다.

### Chance source read

`SaveSlotCampaignChancesReadSource.TryReadChances()`는 display override가 실제로 활성일 때만 override out 값을 적용하도록 수정했다.

비활성 override 호출이 기본 `maxChances`를 0으로 덮지 못하게 하여, production active slot의 chance 값이 HUD까지 정상 전달된다.

## Regression Tests

추가된 주요 회귀 테스트는 다음을 검증한다.

- `ConfiguredGameplayStageLaunchRouter`가 stale `CampaignTempSlot` context를 scene transition 전에 clear한다.
- `ConfiguredGameplayStageLaunchRouter`가 stale `NonCampaign` context를 scene transition 전에 clear한다.
- stale context가 있어도 production MainMenu launch 후 `CampaignChancesReadSource`가 주입된다.
- HUD read model이 `hasRemainingChances=true`, `maxChances>0`을 반환한다.
- ChancePanel ViewModel과 root active 상태가 visible 경로로 연결된다.
- final chance lost 상황에서는 save slot recovery와 별개로 HUD 표시값이 `0/DefaultRemainingChances`로 유지된다.

기존 installer 진단 테스트는 stale context가 installer까지 도달하면 source injection failure가 발생할 수 있음을 보여주는 용도로 유지한다. production path의 보장은 router 회귀 테스트가 담당한다.

## Verification

확인한 명령:

```bash
dotnet build Game.Feature.Gameplay.Tests.csproj -c Debug
dotnet build Game.Feature.UI.Tests.csproj -c Debug
./run_tests.sh core
PROJECT_PATH_WIN='C:\Users\user\2026teamproject_j2m-ui-audio' ./run_tests.sh ui
```

결과:

- Gameplay test project build: pass
- UI test project build: pass
- `./run_tests.sh core`: pass
- `./run_tests.sh ui`: ChancePanel 관련 신규 회귀 테스트는 pass

`./run_tests.sh ui`에 남은 실패는 StageResult/Settings prefab authoring 및 UI flow 쪽 기존 worktree 변경과 연결된 별도 범위다. ChancePanel source injection fix와 한 커밋에 섞지 않는다.
