# Campaign modes → main 병합 준비 실행 계획

작성: 2026-09-26 KST. 상태: **통합 실행 및 검증 완료, Draft PR 준비**. 실제 명령·결과와 남은 수동 항목은 [통합 검증 보고서](./Campaign-Modes-Main-Integration-Validation-Report.md)에 기록한다.

목표는 Casual/Hardcore 저장·플레이 정책을 유지하면서 최신 main의 사망 수명주기, 입력 차단, UI 소유권 및 ChanceLost 연출을 통합하고, 검증 가능한 `main` 대상 PR을 준비하는 것이다. 아래 revision 표와 명령은 작성 시점의 실행 계획이며, 실제 실행 여부와 결과는 통합 검증 보고서를 기준으로 한다.

## 1. 기준 revision과 함께 올라갈 변경

| 항목 | 검토 시점의 값 |
| --- | --- |
| Head branch | `refactor/save-structure-redesign` |
| Head SHA | `d1daa8bce889cf70b42441cfa791e69b3a56b773` |
| 최신 확인 main SHA | `170e8dbbfb3b7ef38fa32e1ca7774ece0c78400c` |
| Merge base | `1719c6a4970313dd54506321ebe9b09513fc924d` |
| 고유 커밋 수 | main 41개 / feature 6개 |
| `git merge-tree --write-tree --messages origin/main HEAD` | exit 1, 충돌 파일 6개 |
| 충돌을 포함한 가상 tree | `423a5c5e640cafd4aa1f36b60da3d37a96ff2928` |

기존 재검토 기록은 `/mnt/d/J2M/evidence/save-structure-premerge-review-20260925T171509Z/review-r2.md`다. 그 기록의 main은 `c802dbbde`이며, 이번 작성 중 main이 4커밋 전진했다. 추가된 PR #222는 ChanceLost 충격·균열·파편 연출과 검증 조건을 변경했다. 최신 main으로 재실행한 가상 병합에서도 충돌 파일 6개는 동일하다. 가상 tree는 컴파일하거나 테스트한 통합 결과가 아니다.

기존 feature 고유 커밋은 다음 6개다. 원격에 공개된 이력을 유지하는 방향으로 main을 feature에 merge한다.

| 순서 | 커밋 | 목적 |
| --- | --- | --- |
| 1 | `13502cfbe` | 승인된 모드 번역과 Static font glyph 적용 |
| 2 | `be200487d` | 모드별 저장·생존·진행 계약 |
| 3 | `4c0d7b9aa` | 모드 선택 UI와 생존 정보 표시 |
| 4 | `5a3513677` | 구현·검증 기록 |
| 5 | `658c4a638` | schema·상태 전이 문서 |
| 6 | `d1daa8bce` | 수동 Player 검증 범위 기록 |

이번 계획 문서를 별도 커밋하면 준비 docs 1개와 통합 merge 1개가 추가되므로 기본 예상은 **8개**다. 후속 수정·증거 문서 커밋은 별도 계산한다. 최종 PR 커밋 수와 목록은 반드시 최종 `origin/main..HEAD`로 다시 산출한다. 현재 6개 또는 이전 예상 7개를 고정값으로 사용하지 않는다.

## 2. 충돌 해결의 기준 계약

아래는 기존 승인된 모드 정책과 main 구조를 함께 만족시켜야 하는 `StrongContract`다. 구현 편의로 테스트 기대값을 바꾸지 않는다.

| 경계 | 통합 뒤 지켜야 할 계약 |
| --- | --- |
| 사망 수명주기 | in-world player respawn은 제거 상태 유지. 사망한 Host의 입력·tick을 차단하고 retry/Continue는 새 stage Host를 초기화한다. |
| Casual | HP `1..3`, inactive Chance `0`. 사망 1회당 TotalDeaths 1회 증가와 현재 level-group 첫 stage/HP 3을 저장한 뒤 LevelFailed. Clear는 HP 3 복원. 수동 retry·메뉴·재실행은 저장된 HP 유지. |
| Hardcore | 예약 HP `0`, Chance `1..3`. Chance가 남으면 감소 후 같은 stage를 새 Host로 retry. 마지막 Chance 소진은 campaign 첫 stage/Chance 3 저장 후 LevelFailed. authored HP/timing 유지. |
| 저장 실패·backup 복구 | 동기 저장 실패 또는 gameplay 중 관찰된 backup 복구는 기존 run을 abandon하고 실패 알림을 발행한다. 기존 Menu/Quit UI를 통한 복귀와 Continue의 저장소 재판정·새 Host 진입을 검증한다. 이전 명령을 재실행하지 않으며 미저장 결과 보존은 보장하지 않는다. 늦은 callback·coordinator 전환의 복구 제한은 아래 참조. |
| 저장 형식 | profile schema 3, local-state schema 1, standalone seed version 2 유지. 구 profile schema 1/2 자동 migration 없음. Unsupported 파일과 backup bytes 보존, 초기화는 기존 명시적 reset UX만 사용. |
| Stage 진입 | stage gameplay는 유효한 active campaign slot 또는 handoff 필요. camera bootstrap 및 저수준 Host fixture 초기화와 구분한다. |
| UI 소유권 | 삭제된 command gateway를 복원하지 않는다. admission policy lifetime은 `GameplayHostUiAccessContext` 소유. UI/presentation은 authoritative simulation을 변경하지 않는다. |
| 입력·연출 | main의 Push/Flip press-time 방향 캡처, interaction lock, 영구 death block 및 ChanceLost 새 연출 유지. Casual damage blink와 save failure UI도 보존. |

구현 순서·테스트 실행 순서·증거 디렉터리 이름은 이 계획의 작업 선택이다. gameplay 정책 변경이나 더 강한 저장 보장을 추가하는 근거로 삼지 않는다.

저장 장애와 이후 전환 장애는 구분한다. 기존 `TryAbortSetup`/`TryAbortClaimBeforeTransition` 범위 밖의 late `BlackReached`·UI observer 실패나 coordinator가 이미 소유한 전환 실패에는 자동 Menu 이동·cover 해제를 보장하지 않는다. 이번 통합으로 그 보장을 새로 추가하지 않는다. 저장 실패 알림이 발생한다는 코드 검증을 popup→Menu→Continue 전체 성공의 증거로 확대하지 않는다.

## 3. 단계 A — 실행 기준 고정 및 준비 커밋

1. `git status --short --branch`, `git diff --stat`, 전체 diff로 작업 소유권을 확인한다. 이번 계획서와 README 변경은 준비 docs 커밋 대상으로 구분한다. 다른 사용자 변경이 있으면 소유권을 확인하고 보존하며 자동 stash/reset하지 않는다.
2. `git fetch origin` 후 head/main SHA, merge base, `git log --reverse --oneline origin/main..HEAD`, 좌우 고유 커밋 수를 증거에 남긴다. main이 위 SHA에서 움직였으면 추가 diff와 merge-tree를 먼저 재검토한다.
3. 사용할 main SHA를 고정한다. PR 번호·base/head·Draft 여부는 기존 PR을 다시 조회해 확인한다. 과거의 PR 미존재/CI 미등록 조회를 최신 상태로 취급하지 않는다.
4. 현재 worktree에서 `./run_tests.sh --print-config`, `./run_tests.sh --dry-run core`로 `PROJECT_PATH_WSL`/`PROJECT_PATH_WIN`이 같은 프로젝트인지 확인한다. 경로 검사는 테스트 통과가 아니다.
5. `j2m-worktree-audit`로 저장소 위치 정책을 확인한다. 현재 등록 경로가 legacy allowlist에 속하는지도 검사하며, 임의 이동하지 않는다. 추가 worktree가 필요할 때만 D 여유 30 GiB 이상 확인 후 `j2m-worktree-add`로 `/mnt/d/J2M/worktrees` 아래에 만들고 실제 경로와 독립 Library를 검증한다. C 여유 10 GiB 미만은 경고한다.
6. 새 검증 증거는 `/mnt/d/J2M/evidence`, Player 출력은 `/mnt/d/J2M/builds` 아래에 둔다. 같은 save-root lock을 사용하는 Unity 검증은 순차 실행한다. 다른 Unity 프로세스를 임의 종료하지 않는다.
7. 위 경로·소유권 확인 뒤 계획서와 README만 준비 docs 커밋으로 정리한다. hook이 실행하는 검증에도 D 아래의 준비 전용 `CODEX_VALIDATION_ROOT`와 `PLAYER_BUILD_ROOT`를 환경변수로 전달한다. 이 hook 결과는 통합 전 revision의 증거로 구분한다.

**완료 기준:** 기준 SHA·커밋 목록·현재 변경 소유권·runner 경로가 기록되고 병합을 시작할 worktree가 정리되어 있다.

## 4. 단계 B — 명시적 충돌 6개 해결

후속 통합 실행 시 feature branch에서 `git merge --no-commit --no-ff <단계 A에서 고정한 main SHA>`를 사용한다. 파일 전체에 일괄 ours/theirs를 적용하지 않는다. merge 진행 중에는 통합 목적의 변경만 포함한다.

아래 경로의 `Gameplay/`, `UI/`는 각각 `Assets/_Features/Gameplay/`, `Assets/_Features/UI/` 기준이다.

| 파일 | 구체 수정 | 완료 확인 |
| --- | --- | --- |
| `Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs` | main의 try/catch 자원 정리 및 admission policy를 받는 Context 생성자 유지. feature의 mode 전달을 Initialize 전에 유지하고, `CasualPlayerDamageBlink` 연결을 try 내부 UIAccess 생성 뒤/Context 반환 전에 배치. | 초기화 실패 cleanup과 정상 Dispose 소유권 일치. 삭제된 respawn timing/delay 인자를 되살리지 않음. |
| `Gameplay/Gameplay_Host/Runtime/GameplayInputHost.cs` | pause/terminal/abandoned/death 차단을 통합. presentation lock 시간 누적 분기와 사망 tick의 후반 전달을 유지. 세부 순서는 아래 참조. | 다음 catch-up tick 중단, 기존 Host 재개 불가, 현재 사망 tick의 저장·terminal 전달 완료. |
| `Gameplay/Gameplay_Host/Runtime/UIAccess/GameplayHostCommandGateway.cs` | main의 파일·`.meta` 삭제 유지. feature 차이는 빈 줄이므로 이식할 동작 없음. | gateway/interface/acceptance의 삭제된 API에 runtime·test 참조가 없음. |
| `Gameplay/Gameplay_Tests/EditMode/Scenario/MovementPhaseScenarioTests.cs` | 삭제된 `allowPlayerRespawn` helper 인자·전달부와 pipeline의 respawn delay 인자를 제거. feature DestroyTile/cooldown 테스트와 검증은 유지. | HP와 cooldown에 무관한 사망·제거, signal 1회, 다음 tick 재사망 없음 검증 유지. |
| `Gameplay/Gameplay_Tests/EditMode/Unit/TileFeatureEffectResolverTests.cs` | 폐기된 respawn 인자만 제거하고 feature의 `damageCooldownSeconds` 기반 `PlayerControlTimingSettings` snapshot을 유지. | 기본 timing으로 덮어써 cooldown 시나리오를 무력화하지 않음. |
| `UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs` | OnDestroy에서 main action-count typography controller Dispose/null과 feature failure presenter Dispose를 모두 유지. failure presenter는 popup/presentation source 해제 전에 정리. | localized action count 및 오류 UI 구독·소유권 모두 보존. |

### GameplayInputHost에서 놓치기 쉬운 순서

1. `IsTerminalAdmissionBlocked()`가 `IsCampaignRunAbandoned`를 포함하는 기존 feature 의미를 유지한다. 동일 abandoned 조건을 모든 곳에 중복 작성할 필요는 없다.
2. `AdvanceTime` 진입은 pause/terminal/death를 차단한다. 별도의 presentation-lock 분기에서는 기존 `AccumulateLockedTime(deltaTime)`를 계속 수행한다.
3. catch-up loop의 tick 전·후와 `RunSingleTick`은 pause/terminal/presentation/death 조건을 검사한다. 첫 tick의 저장 실패 callback으로 abandon되면 둘째 tick을 실행하지 않는다.
4. `RunSingleTickUnlocked`의 `RunNextTick → buffered input/action lock 반영 → BlockInputOnPlayerDeath → Presenter.Present → TickCompleted → ObjectiveResultUpdated` 전달을 유지한다. death flag 설정 직후 return하면 현재 tick의 저장·terminal 처리가 유실된다.
5. abandon 시 pending input/action binding/누적 시간을 정리한다. pause나 terminal gate가 해제되어도 `BindActions`가 기존 abandoned Host를 다시 활성화하지 않아야 한다.
6. Presenter의 `LateUpdate/UpdatePresentation` 진행은 simulation tick 차단과 구분한다. death block 때문에 terminal 연출 자체를 중단하는 변경을 추가하지 않는다.

해결한 파일은 내용을 검토한 뒤 명시적 path로 stage해 unresolved index를 해소한다. 삭제 파일과 `.meta` 삭제도 함께 확인한다. 자동 병합으로 이미 stage된 파일을 포함해 `git diff --cached HEAD`를 검토한다.

**완료 기준:** `git diff --name-only --diff-filter=U`가 비어 있고, 의미 충돌까지 검토한 6개 해법이 반영되어 있다. 충돌 마커 제거만으로 단계 C를 생략하지 않는다.

## 5. 단계 C — 자동 병합 오류와 문서 모순 수정

### C1. 컴파일·진단 오류

- `StageBackedGameplaySceneInstallerTests.cs`: 신규 startup 테스트의 `Unknown`을 지원하는 noncampaign stage 사례와 삭제된 `DisableCampaignFlow` 호출을 제거한다. Casual/Hardcore의 첫 snapshot·HUD·HP·다른 entity 보존·preset 검증은 유지한다. main의 `StageBackedGameplaySceneInstaller_NoCampaignSlot_RejectsBeforeGameplay`가 음성 사례를 담당한다. camera-only 초기화 검증은 별도로 보존한다.
- `CampaignStageFlowTests.cs`: 신규 fault/recovery 테스트의 `new NoOpGameplayCommandGateway()` 3개를 main에 남아 있는 `NoOpLifetime`으로 교체한다. save failure/backup recovery assertion을 줄이지 않는다.
- `ActualSceneBootstrapSmokePlayModeTests.cs`: CasualInputProbe reflection 필드 `_isPlayerRespawnDelayInputBlocked`를 `_isPlayerDeathInputBlocked`로 바꾸고 출력 label도 `deathBlocked`로 맞춘다. null-conditional reflection은 누락을 조용히 숨기므로 실제 값이 기록되는지 확인한다.
- 변경한 파일 밖도 `rg`로 `DisableCampaignFlow`, `NoOpGameplayCommandGateway`, 폐기된 gateway 타입, `allowPlayerRespawn`, respawn delay API와 reflection 필드를 검색한다. 역사 문서 언급과 활성 C# 참조를 구분하고 잔여 호출은 선언 존재 및 의도로 판정한다.

### C2. 현재 정책 문서 정합성

- [모드 설계](./Campaign-Casual-Hardcore-Mode-Design.md), [구현 프롬프트](./Campaign-Casual-Hardcore-Implementation-Prompt.md), [구현 보고서](./Campaign-Casual-Hardcore-Implementation-Report.md)의 현재 지침에서 noncampaign stage 지원/in-world respawn 보존 문구를 제거하거나 새 계약으로 대체한다. 과거에 수행한 테스트 기록은 지우지 않고 당시 revision의 기록임을 표시한다.
- main에서 들어올 `Docs/Architecture/Gameplay-Death-Recovery-Lifecycle.md`의 death commit/Save Durability 설명은 모든 사망을 Chance 저장으로 설명하지 않도록 Casual/Hardcore 분기를 명시한다. 문서의 no-respawn·새 Host·저장 후 terminal 구조는 유지한다.
- README와 [저장 기준 정책](./Pre-Release-Save-Baseline-Policy.md), [LocalState 정책](./Campaign-LocalState-Launch-State.md)의 현재 설명을 교차 확인한다. schema 2를 다루는 역사 자료는 역사 상태로 보존한다.
- 새 schema, 신규 번역, atlas 재생성을 통합 해결책에 끼워 넣지 않는다. 이미 승인된 모드 번역·폰트와 main의 localized action count를 함께 보존한다.

### C3. 최신 main의 ChanceLost 보존

- PR #222의 production prefab, fracture/fragment material·shader 및 `.meta`, `ChanceLostOverlayContentView`, 관련 UI/PlayMode 테스트를 유지한다.
- `LostChanceImpactRoot/LostChanceTweenRoot` 계층과 rebind 시 연출 초기화를 보존한다. Hardcore 사망 후 Chance가 남는 자동 재시도에서 연출 완료 뒤 새 Host가 입력을 받는지 확인한다. 마지막 Chance 소진은 ChanceLost 자동 retry가 아닌 LevelFailed 경로로 별도 확인한다.
- 기존 번역·폰트/모드 선택 prefab은 모드 UX 표시를 위한 변경이며, ChanceLost 자산은 사망 후 전환 연출을 위한 변경이다. 최종 PR에 각 목적을 구분해 적는다. 새 Scene 변경이 생기면 원인을 검토하고 가능한 별도 intent로 분리한다.

**완료 기준:** 삭제 API 참조와 현재 지침의 상충이 해소되고, 기존 mode 동작과 최신 ChanceLost 계약을 검증할 코드가 함께 컴파일된다.

## 6. 단계 D — 순차 검증과 실패 분류

검증 직전에 C의 수정까지 명시적 path로 stage한다. unresolved index가 없고, tracked 파일의 unstaged 변경이 없으며, 필요한 신규 파일이 untracked로 빠지지 않았는지 확인한다. `git diff --cached HEAD`로 자동 병합분까지 포함한 전체 통합 diff를 검토한다. head/main SHA, staged diff, `git write-tree`의 tree ID 및 입력 source/asset hash manifest를 기록한다. merge 미커밋 결과를 부모 HEAD만의 결과라고 적지 않는다. 아래 명령은 현재 worktree에서 실행하며 shell block 사이의 변수를 같은 세션에서 유지한다.

```bash
CAMPAIGN_INTEGRATION_RUN_ID="$(date -u +%Y%m%dT%H%M%SZ)"
CAMPAIGN_INTEGRATION_EVIDENCE_ROOT="/mnt/d/J2M/evidence/campaign-modes-main-integration-$CAMPAIGN_INTEGRATION_RUN_ID"
export PLAYER_BUILD_ROOT="/mnt/d/J2M/builds/campaign-modes-main-integration-$CAMPAIGN_INTEGRATION_RUN_ID"
mkdir -p "$CAMPAIGN_INTEGRATION_EVIDENCE_ROOT/frames" "$PLAYER_BUILD_ROOT"
git status --short
git diff --name-only --diff-filter=U
git diff --quiet
git diff --cached --check
git diff --check
git diff --cached HEAD > "$CAMPAIGN_INTEGRATION_EVIDENCE_ROOT/integration-staged.patch"
git write-tree > "$CAMPAIGN_INTEGRATION_EVIDENCE_ROOT/validated-tree.txt"
```

위 명령은 단계별 확인용이다. unresolved 출력, unstaged diff 또는 오류가 있으면 검증을 시작하지 않는다. 검증 도중 source/assets가 바뀌면 다시 stage하고 tree/manifest를 갱신한 뒤 영향 lane을 재실행한다. C1 commit 뒤 `git rev-parse 'HEAD^{tree}'`를 검증 tree와 비교한다. 문서만 후속 변경된 경우에도 전체 tree 차이와 검증 입력 동일성을 구분해 기록한다.

### D1. 변경 계약의 targeted lane

```bash
CODEX_VALIDATION_ROOT="$CAMPAIGN_INTEGRATION_EVIDENCE_ROOT/targeted" \
./run_tests.sh full --filter 'CampaignSaveServiceTests,CampaignSlotTransitionEngineTests,CampaignSlotStateTests,CampaignStageFlowTests,StageBackedGameplaySceneInstallerTests,MovementPhaseScenarioTests,TileFeatureEffectResolverTests,AttackPhaseScenarioTests,PlayerMovementInputTests,GameplayHostCommandAdmissionPolicyTests,GameplayUiAccessRuntimeTests,CampaignSaveFailure_StopsCatchUpBeforeSecondTickAndSurvivesPauseRelease'
```

직접 충돌한 `GameplayInputHost`의 실제 키 입력·방향 캡처·playback 검증은 다음 별도 실행으로 포함한다. `PlayerMovementInputTests`는 EditMode이며 아래 사례는 `Category("Full")`이므로 core만으로 대체할 수 없다. fixture 전체는 별도 camera 증거 조건을 요구하는 사례까지 선택하므로 필요한 메서드를 명시한다.

```bash
CODEX_VALIDATION_ROOT="$CAMPAIGN_INTEGRATION_EVIDENCE_ROOT/input-playmode" \
./run_tests.sh full --filter 'GameplayInputHost_PushKeyAlone_StartsAgainstFacingBox,GameplayInputHost_FlipKeyAlone_StartsAgainstFacingBox,GameplayInputHost_DirectionlessActionAtProductionBoxClamp_QueuesAssist,GameplayInputHost_FlipKeyThenDown_PreservesFacingTargetAcrossInputUpdates,GameplayInputHost_PlayerS1FlipStartedThenDown_KeepsRealTargetAndPlayback,GameplayInputHost_PlayerS1PushStartedThenDown_KeepsModelFacingAndMovesBox,GameplayInputHost_PlayerS1FakeFlipStartedThenDown_KeepsFacingAndBlocksMove,GameplayInputHost_PushKeyThenDownInSameInputUpdate_PreservesFacingTarget,GameplayInputHost_FailedFlipThenDownInSameInputUpdate_KeepsFakeFacing,GameplayInputHost_FlipVisualHold_BlocksMoveCommandUntilPlaybackCompletes,GameplayInputHost_FlipAfterDirectionChange_PreservesProjectedViewState'
```

- XML에서 지정 fixture/대상 사례가 실제로 선택됐는지 확인한다. filter 전체가 0건이 아니어도 의도한 fixture가 빠졌으면 미검증으로 취급한다. 이름이 바뀌었으면 현행 fixture 이름으로 갱신한다.
- 저장·backup 장애 테스트와 Host catch-up 테스트의 증거를 구분한다. 기존 `CampaignSaveFailure_StopsCatchUp...`는 reflection을 통한 abandon 검증이며, production save failure 전체 경로의 E2E 증거가 아니다.
- 실제 저장 실패부터 Host abandon까지 연결에 검증 공백이 남으면, 기존 save port에 장애를 주입해 `Host → tick/feed → controller → save failure → abandon`을 통과하는 최소 회귀 테스트를 추가한다. UI 복구는 D4에서도 확인한다.
- `PlayerInvincible_PassiveContactOverlap_DoesNotRetryInvincibleEveryTick`이 실패하면 과거에도 실패했다는 이유만으로 baseline으로 분류하지 않는다. 최소 재현과 비교 revision의 동일 조건 결과로 feature 회귀·main 기존 실패·환경 문제를 분류한다.

### D2. 필수 core / UI lane

```bash
CODEX_VALIDATION_ROOT="$CAMPAIGN_INTEGRATION_EVIDENCE_ROOT/core" ./run_tests.sh core
CODEX_VALIDATION_ROOT="$CAMPAIGN_INTEGRATION_EVIDENCE_ROOT/ui" ./run_tests.sh ui
```

main에 추가된 `ChanceLostProductionContent_CracksBeforeDebrisPreservesFramesAndRestoresOnRebind`와 feature의 모드·HP/HUD·저장 오류 UI 사례가 XML에 포함됐는지 확인한다. 건수를 과거 feature/main 단독 실행 건수에 고정하지 않는다.

### D3. Graphics PlayMode와 연속 프레임

```bash
CODEX_VALIDATION_ROOT="$CAMPAIGN_INTEGRATION_EVIDENCE_ROOT/graphics" \
WSLENV="${WSLENV:+$WSLENV:}J2M_TERMINAL_RENDER_EVIDENCE_ROOT" \
J2M_TERMINAL_RENDER_EVIDENCE_ROOT="$(wslpath -w "$CAMPAIGN_INTEGRATION_EVIDENCE_ROOT/frames")" \
UNITY_GRAPHICS=1 ./run_tests.sh full --filter 'CasualInputProbe_,CampaignLaunchHandoffPlayModeTests'
```

- Windows Unity에는 Windows 절대 경로로 frame 출력 위치를 전달한다. CasualInputProbe의 일반 접촉·사망·재시작과 최신 ChanceLost handoff를 확인한다.
- PR #222 capture는 4해상도×19장=76 PNG, manifest, shader compile/supported 확인, pixel assertion을 요구한다. 정상 종료만으로 대체하지 않는다.
- 현행 CasualInputProbe는 위 환경변수와 별개로 `D:\J2M\evidence\campaign-modes-implementation\casual-input-probe-20260925\<timestamp>`에 기록한다. 실행 전후 생성된 정확한 디렉터리·로그·revision을 이번 graphics manifest에 연결한다. 모든 probe 출력이 `CODEX_VALIDATION_ROOT` 아래에 생긴다고 가정하지 않는다.
- EditMode 0건은 대상 없음으로 기록할 수 있지만, 대상 PlayMode 미선택·graphics skip을 성공으로 세지 않는다. probe를 저장 실패 popup→menu E2E 증거로 사용하지 않는다.

### D4. Player / 수동 확인

동일한 통합 source/asset으로 만든 Player의 build 식별자·로그·절차·save root를 기록한다. 사용자 일반 save를 장애 실험에 사용하지 않는다. 빌드는 repository의 해당 runner 경로와 옵션을 먼저 확정해 기록하며, Unity 직접 실행을 lane 증거로 사용하지 않는다.

Player의 기존 `CampaignTempSlot`은 process-GUID별 임시 save를 사용하며 정상 종료 시 삭제한다. 같은 process의 retry/Menu 복귀 검증에는 쓸 수 있으나 앱 종료 후 HP 보존 검증에는 쓸 수 없다. 재시작 항목은 프로세스 간 같은 save를 유지하는 격리 환경(예: 전용 테스트 사용자 프로필)을 확보하고 두 실행의 실제 save root가 같은지 확인한 뒤 수행한다. 지속 격리 환경 또는 Player 장애 주입 절차가 확보되지 않았으면 구체적인 준비 미완료 사유를 기록하고 해당 항목은 미실시로 둔다. 지원 여부를 확인하지 않은 save-root 옵션이나 임시 save의 자동 승계를 전제하지 않는다.

| 조작 | 확인할 결과 |
| --- | --- |
| Casual HP2로 Continue, 피격, 무적 중 재접촉 | 초기 snapshot/HUD HP2, 피격 1회만 반영, 피격 후 damage cooldown 설정 2초와 blink, 공유 preset 변경 없음. spawn 시점의 무적 2초를 뜻하지 않음 |
| Casual DestroyTile 접촉/점유 중 활성화 | HP나 cooldown이 치명적 제거를 무효화하지 않음, 사망 1회, level-group 선두/HP3 저장, 동일 Host 내 부활 없음 |
| authored 낙하 및 플레이어 압사 경로 조사 | 먼저 stage·입력·production 제거 경로를 식별한 뒤 해당 경로로 동일 사망 계약 확인. 검토된 Barricade/Jump crush는 Box 대상이며 player crush 존재를 증명하지 않음. 재현 경로가 없으면 미확인으로 기록하고 요구사항 처리 결정을 남김. 검증 항목을 채우기 위해 새 gameplay 기능을 만들거나 완료로 처리하지 않음 |
| Casual 수동 retry·menu 경유 Continue·앱 재시작 | 저장 HP 유지. clear 때 규정대로 HP3 회복 |
| Hardcore Chance2에서 사망 | Chance1 저장, 최신 ChanceLost 연출을 거쳐 동일 stage 자동 retry, 새 Host 입력 가능 |
| Hardcore Chance1에서 사망 | Chance3/campaign 선두 저장 후 LevelFailed. 구 Host 차단 유지. 명시적 Restart 또는 Menu→Continue 뒤 새 Host 입력 가능. ChanceLost 자동 retry를 기대하지 않음 |
| 동기 저장 실패 또는 관찰된 backup 복구 → popup → Menu → Continue | 구 Host tick/input 재개 없음. Menu에서 저장 상태 재조회 후 합법적인 저장 위치를 새 Host로 전달. 구 command replay 없음. late callback/coordinator 실패까지 자동 복구된다는 증거로 확대하지 않음 |
| 모드 전환·HP/Chance·오류 문구·action count | 기존 번역/atlas로 표시, 잘림·겹침·missing glyph 없음 |

기존 사용자 확인은 모드 전환과 화면 배치에 한정되며 build 식별자가 없다. 위 통합 revision의 gameplay/저장 오류 검증을 대신하지 않는다.

### 증거 판정

- 실행별 command, revision/tree/hash, runner path, XML/log, passed/failed/skipped/selected 건수를 남긴다. 실패한 첫 시도의 증거도 유지한다.
- 변경 영역 실패는 수정 후 해당 검증을 다시 실행한다. 입력 source/asset이 바뀌면 영향을 받는 lane의 증거를 갱신한다. 근거 없이 무관한 시험을 반복하지 않는다.
- 무필터 `full`은 알려진 red baseline이 있어 이 계획의 필수 gate로 두지 않는다. 미실행이면 이유를 명시하고 touched-cluster 결과와 기존 baseline을 구분한다. 특정 실패가 기존 baseline에 해당하는지는 개별 확인한다.
- 이전 feature 단독 core/UI pass, main PR #222 pass, dry-run을 통합 revision의 pass로 합산하지 않는다.

**완료 기준:** targeted/core/UI/graphics의 대상 계약이 통과하고, D4 증거 또는 구체적인 미실시 항목이 기록되어 있다. 미실시 필수 수동 항목이 있으면 PR은 Draft로 둔다.

## 7. 단계 E — 커밋 구성과 PR 준비

| 단위 | 예정 메시지 | 포함할 의도 |
| --- | --- | --- |
| C0 | `docs: Campaign - main 통합 절차와 검증 조건 정의` | 이 계획서와 README 진입점. 구현 전 작업 tree 정리. |
| C1 | `fix: Campaign - main 사망 계약과 모드 저장 통합` | main을 가져오는 merge commit. 충돌 해소, 관련 자동 merge 오류, 계약 회귀 테스트, 현재 정책 문서의 정합성을 함께 성립시킴. |
| C2 이후 | 필요한 경우에만 intent별로 명명 | C1 이후 발견된 수정, 최종 검증 기록. 불필요한 변경으로 늘리지 않음. |

각 메시지 본문은 이유와 결과를 설명하는 최소 2개 bullet을 포함한다. merge 자동 생성 제목도 commit 규칙에 맞춘다. B/C에서 stage한 내용을 `git diff --staged`와 `git diff --staged --check`로 재확인한다. 기존 hook의 core 실행을 우회하지 않는다. D의 명령 앞 `CODEX_VALIDATION_ROOT=...`는 해당 명령에만 적용되므로, C1과 후속 commit 각각에 고유한 D 아래 hook 증거 경로를 명시적으로 전달한다. A의 준비 hook 경로를 재사용하지 않는다.

예: 메시지 파일을 검토·작성한 뒤 C1은 다음 형태로 실행한다. 재시도도 별도 디렉터리를 사용한다.

```bash
CODEX_VALIDATION_ROOT="$CAMPAIGN_INTEGRATION_EVIDENCE_ROOT/commit-hook-C1-$(date -u +%Y%m%dT%H%M%SZ)" \
PLAYER_BUILD_ROOT="$PLAYER_BUILD_ROOT" \
git commit --file "$CAMPAIGN_INTEGRATION_EVIDENCE_ROOT/merge-message.txt"
```

1. commit 후 status와 실제 commit 내용을 확인하고 최종 SHA와 검증 입력 hash를 연결한다. 증거 문서만 담은 후속 commit은 source/assets 동일성을 제시하며 다른 revision의 실행 결과를 동일 SHA 실행으로 표현하지 않는다.
2. push 전 branch/upstream·diff·로컬 변경을 재확인한다. 일반 push 후 remote head와 local head 일치를 확인한다.
3. `origin/main`을 다시 fetch하고 `origin/main..HEAD` 전체 커밋과 `origin/main...HEAD` 최종 PR diff를 확인한다. main이 진행됐으면 추가 diff·충돌·영향 lane을 다시 판정한다.
4. 기존 PR 유무와 base=`main`/head=`refactor/save-structure-redesign`을 확인해 중복 PR을 피한다. 생성/갱신 시 최종 head의 CI, review 미해결 thread, mergeability를 확인한다. CI 미등록은 pass가 아니다.
5. PR 본문에 변경 목적, 포함 커밋, 6개 충돌과 자동 merge 오류의 해법, asset 목적, schema 호환성, 실행/미실행 테스트와 이유, manual evidence를 기록한다.
6. D의 필수 검증과 필요한 CI/review가 완료되고 최신 base에 충돌이 없으며 남은 제약이 명시된 뒤 Ready로 전환한다. 실제 main으로 merge하는 것은 PR 준비와 별도 작업이다.

## 8. 실행 중 중단·재조사 조건

- runner가 다른 worktree를 가리키거나 storage 감사에서 미해결 위반을 발견하면 실행 위치를 확정할 때까지 Unity 검증을 시작하지 않는다.
- 삭제 API 복원, in-world respawn 복원, slot 없는 stage gameplay 허용, Casual cooldown fixture 약화가 해결안에 포함되면 채택하지 않고 계약에 맞게 다시 구성한다.
- 저장 실패 뒤 tick/input 재개, 사망 commit 중복/누락, terminal 연출 정지가 발생하면 Ready 전환을 보류하고 최소 재현을 만들어 수정한다.
- main 이동, 검증 중 source 변경, 환경 lock 실패는 새로운 사실로 기록한다. 과거 성공 기록으로 대체하지 않는다.

이 계획의 도달점은 포함 커밋과 의미상 해결책을 확인할 수 있고 남은 미검증 사항이 명시된 main 대상 PR이다. 이 계획의 제한된 lane 결과로 무필터 full 성공이나 project-wide green을 주장하지 않는다.

## 9. 서브 에이전트 재검토 기록 — 2026-09-26

Runtime 계약, validation, workflow 담당 3개 서브 에이전트가 HEAD·pinned main·가상 통합 tree와 계획을 읽기 전용으로 대조했다. 재fetch 후 main SHA는 `170e8dbbf`로 동일하며 가상 병합 충돌 6개도 동일하다. 기본 6개 충돌 해결안과 death tick 전달 순서는 유지한다.

| 발견 사항 | 반영 위치 |
| --- | --- |
| main Push/Flip 실제 입력·playback Full 사례 누락 | D1에 11개 메서드의 별도 targeted 명령 추가 |
| 마지막 Chance 소진과 ChanceLost 자동 retry 혼동 | C3/D4에서 LevelFailed와 자동 retry 분리 |
| 저장 실패 이후 전환까지 복구 보장 확대 가능성 | 기준 계약/D4에 동기 실패 알림과 late/coordinator 복구 제한 명시 |
| 검증 working tree와 merge index 동일성 확인 누락 | B/D/E에 stage 시점·staged diff·tree ID·commit 대조 추가 |
| C1 이후 hook의 고유 D 증거 경로 누락 | E에 commit별 환경변수 전달 명령 추가 |
| process-GUID 임시 save와 앱 재시작 보존 검증 충돌 | D4에 지속 격리 환경 준비 조건 추가 |
| 플레이어 crush production 경로 존재 단정 | D4에서 authored 경로 조사와 실제 검증 분리 |
| CasualInputProbe의 별도 출력 경로 미기록 | D3 manifest에 실제 probe 디렉터리 연결 |
| 초기 무적 2초가 spawn grace로 읽히는 표현 | D4를 피격 후 cooldown 설정으로 명확화 |

수정 뒤 같은 3개 서브 에이전트가 담당 항목을 다시 검토해 지적사항 반영을 확인했으며, 수정 범위에서 새 모순을 발견하지 않았다. 문서의 로컬 링크 5개, Bash 명령 블록 6개의 구문, 추가 입력 테스트 메서드 11개의 pinned 가상 tree 내 존재도 확인했다.

이 재검토는 문서·코드 대조와 가상 병합 확인이다. Unity 테스트나 실제 병합을 실행한 증거가 아니다. 지속 격리 Player 환경, 장애 주입 절차 및 미확인 authored 사망 경로는 후속 실행에서 해결할 준비 항목으로 남는다.
