# Campaign modes → main 통합 검증 보고서

기록: 2026-09-26 KST. 기준 branch `refactor/save-structure-redesign`, 병합 대상 `170e8dbbfb3b7ef38fa32e1ca7774ece0c78400c` (`origin/main`). 통합 merge commit은 `99c7a1845e62f614862ecb1d54a96516e0abcd7e`이며 부모는 계획서 선행 문서 commit `c588cf8f29e3d9f8efc643e85e1feea9ffbbb8a8`과 위 main이다. 검증한 staged tree와 merge commit tree는 모두 `8d6ce4aa5f049908f9dfec22c355e9e2af48a141`이다. 후속 문서 commit은 tree가 다르므로, 아래 Unity 결과를 그 문서 commit에서 새로 실행한 것으로 해석하지 않는다. 검증 worktree는 `/mnt/d/j2m/worktrees/save-structure-redesign`이고 runner의 Windows project path는 `D:\j2m\worktrees\save-structure-redesign`이다.

증거 root: `/mnt/d/J2M/evidence/campaign-modes-main-integration-20260925T185553Z`. 빌드 root: `/mnt/d/J2M/builds/campaign-modes-main-integration-20260925T185553Z`. `validated-tree.txt`, `source-asset-sha256.txt`, `integration-staged.patch`, `validation-lanes.json`에 검증 입력과 XML 집계를 남겼다. source/asset manifest의 169개 파일 해시와 삭제 대상 5개 경로는 commit 전 재확인해 모두 일치했다. Unity 실행 중 SDF asset 변이와 unstaged diff는 runner가 없다고 확인했다.

## 통합 결과

- 여섯 충돌에서 main의 death input gate, fatal DestroyTile, UI 정리 및 제거된 respawn/command gateway 계약을 유지하면서 feature의 Casual HP·Hardcore Chance 저장과 damage blink를 결합했다. 사망 tick에서 `RunNextTick → input/action lock → death block → Present → TickCompleted → ObjectiveResultUpdated` 순서를 유지해 저장과 terminal 전달 전에 반환하지 않는다.
- 자동 병합으로 어긋난 stage startup의 `GameMode.Unknown` 허용, 삭제된 gateway 대역, PlayMode death-block reflection을 정리했다. 현재 정책 문서의 비캠페인 stage 진입과 in-world respawn 주장을 제거하고 역사 기록과 현행 계약을 구분했다.
- production save port에 실패를 주입한 `CasualSurvival_ProductionSaveFailureFromHostTick_AbandonsBeforeCatchUp` 회귀에서 첫 tick만 실행, Host abandon, 후속 tick·입력 차단, 기존 저장 HP 보존을 확인했다. UI의 Menu 복귀는 별도 UI 계약과 수동 항목으로 구분한다.
- 무적 passive contact의 첫 `EnemyActionExecuted` 신호 기대값은 정확한 main SHA에서도 동일하게 실패했다. main의 거절된 contact는 실행 신호를 만들지 않는 계약과 맞춰, 세 tick의 피해 시도·거절·쿨다운 cadence를 모두 확인하고 실행 신호 0을 요구하도록 고쳤다. 비교 증거는 `main-comparison`, 최종 통합 재검증은 `targeted-final`에 있다.

## 실행한 검증

| 실행 | XML 결과 | 증거 |
| --- | --- | --- |
| targeted `full --filter` (계획서 D1) | EditMode 718 pass, 14 skip, 0 fail / PlayMode 1 pass | `targeted-final/test-results`; 11개 지정 fixture 모두 실제 선택 |
| 입력 PlayMode 11개 메서드 `full --filter` | EditMode 대상 0 / PlayMode 34 pass, 0 fail; 매개변수 사례 포함 11개 메서드 모두 선택 | `input-playmode/test-results` |
| `./run_tests.sh core` | EditMode 293 pass / PlayMode 109 pass, 4 skip, 0 fail | `core/test-results` |
| `./run_tests.sh ui` | EditMode 1,648 pass, 0 fail | `ui/test-results`; ChanceLost 연속 프레임, Casual/Hardcore HUD, 모드 선택, 저장 오류 popup 사례 선택 |
| `UNITY_GRAPHICS=1 ./run_tests.sh full --filter 'CasualInputProbe_,CampaignLaunchHandoffPlayModeTests'` | EditMode 대상 0 / PlayMode 10 pass, 0 fail | `graphics/test-results`; `frames/20260926-044608/manifest.txt` 및 4해상도×19장 PNG |
| C1 commit hook `./run_tests.sh core` | EditMode 293 pass / PlayMode 109 pass, 4 skip, 0 fail | `commit-hook-C1-20260926T045503Z/test-results` |
| `./run_tests.sh player-capture-save-safety` | PASS: 허용되지 않은 normal-slot capture 거부, profile/backup/active-slot sentinel SHA-256 불변 | `player-save-safety/20260926T044725Z/manifest.txt`, Player 로그와 빌드 |

graphics의 shader compile 오류 없음·지원 여부와 pixel assertion은 `TerminalRenderEvidence_FourAspectRatios_CapturesNoGapLifecycleFrames`의 통과 조건이다. frame manifest는 색상 채널 허용 오차 0/255, 투명 pixel 0, 예상 밖 chroma shift 0을 기록한다. Casual input probe의 실제 출력은 별도 경로 `/mnt/d/J2M/evidence/campaign-modes-implementation/casual-input-probe-20260925/20260926-044539-continue`와 `.../20260926-044551-demo`의 `probe.log`, `terminal-trace.txt`, 입력/클릭 기록이다. 두 probe는 popup→Menu 저장 실패 복구 증거가 아니다.

targeted의 14 skip은 삭제된 MechanicsShowcase stage 전용 fixture이고, 현행 campaign stage content 사례는 통과했다. core PlayMode의 4 skip은 그래픽 장치 또는 전용 graphics 실행을 요구한다. 그중 terminal render 4해상도 사례는 위 graphics 실행에서 선택되어 통과했으며, 나머지 세 graphics 전용 사례의 core skip을 통과로 바꾸어 세지 않는다.

Player save-safety는 고유 product name `VectorQuake-P0Phase4Smoke-SaveSafety-20260926T044725Z`로 만든 Windows64 Mono Development Player이다. 빌드는 `/mnt/d/J2M/builds/campaign-modes-main-integration-20260925T185553Z/player-save-safety/20260926T044725Z`에 있다. 격리 save root는 `/mnt/c/Users/user/AppData/LocalLow/J2M/VectorQuake-P0Phase4Smoke-SaveSafety-20260926T044725Z/Saves`이며 사용자 일반 save를 건드리지 않았다. runner manifest의 `HEAD=c588cf8f...`는 merge commit 전 `git HEAD`를 읽은 값이다. 실제 빌드 입력은 위에 적은 staged tree였고, source/asset 해시가 commit까지 동일했다. 이 probe는 앱 재시작 HP 보존이나 저장 실패 popup을 검사하지 않는다.

`player-runtime.log`는 의도한 normal-slot 요청 거부 뒤, launch context 없이 `UIAudioScene` 부팅을 계속 시도하면서 `StageRuntimeContentResolver`와 `GameplayUiFlowInstaller` 초기화 예외를 기록한다. runner의 PASS 조건은 거부 메시지와 sentinel 해시 불변에 한정된다. 이 실행을 정상 gameplay 부팅 또는 수동 Player 조작의 증거로 취급하지 않는다.

첫 targeted 실행은 병합 후 갱신되지 않은 ignored Unity `.csproj`가 삭제된 `TickPipeline.RespawnProcessor.cs`를 참조해 `CS2001`로 중단됐고, 두 번째는 새 UI test assembly의 `Unity.InputSystem.TestFramework` 참조 누락으로 `CS0246`에서 중단됐다. generated project 입력만 현행 asmdef/source에 맞춰 로컬 재생성했으며 해시 전후는 `generated-project-reconciliation.txt`에 있다. 세 번째 실행의 무적 접촉 단언 실패는 위 main 비교를 거쳐 수정했다. 이 실패와 중단 기록은 `targeted`, `targeted-retry1`, `targeted-retry2`, `invincible-contract-retest`에 보존했다. 최종 성공은 `targeted-final`을 기준으로 한다.

## 미실시 및 Draft 유지 조건

- 무필터 `full`은 문서화된 red baseline이 있어 실행하지 않았다. 이 보고서의 `full --filter` 결과를 전체 회귀 통과로 확장하지 않는다.
- Player 수동 Casual HP2 피격·cooldown·blink, DestroyTile death, retry/Menu/Continue, Hardcore Chance2/1과 LevelFailed, 번역·화면 배치는 같은 통합 Player에서 직접 조작하지 않았다. Scene `UIAudioScene`, `PlayerActionCountView` Prefab, timing preset 및 capture content asset의 실제 Editor/Player 육안 확인도 실시하지 않았다. 관련 자동 테스트와 graphics capture만 위 표에 기록했다.
- 현재 `CampaignTempSlot`은 프로세스 GUID별 임시 저장소를 사용하고 정상 종료 시 삭제한다. 이를 사용한 두 프로세스 실행으로 앱 재시작 HP 보존을 주장할 수 없다. 후속 담당자는 일반 캠페인 UI가 가능한 격리 Windows 사용자 프로필 또는 동등한 고정 save root를 마련하고, 두 실행의 실제 `persistentDataPath/Saves`가 같은지 기록한 뒤 HP2 피격 → 종료 → 재실행 → Continue의 JSON/HUD/로그를 대조해야 한다. clear 시 HP3 복원도 같은 환경에서 확인한다.
- 제품 Player에서 동기 저장 실패를 주입하는 지원 옵션은 확인되지 않았다. 별도 QA 장애 주입 빌드 또는 지원되는 repository seam을 마련해 격리 save root에서 write 실패와 관찰 가능한 backup 복구를 각각 만들고, popup → Menu → Continue, 구 Host tick/input 차단, 새 Host의 합법적 저장 위치를 로그와 영상으로 확인해야 한다. `player-capture-save-safety`의 무단 요청 거부를 이 E2E 결과로 대신하지 않는다.
- stage asset과 runtime 경로 검색에서 authored player 낙하·압사 재현 경로를 식별하지 못했다. 현재 확인한 Barricade/Jump crush는 Box 대상이다. 해당 동작이 요구사항이면 stage authoring과 생산 제거 경로를 먼저 특정한 뒤 동일 death 저장·terminal·input 차단을 확인하고, 경로가 없다면 제품 요구사항 결정을 별도로 받아야 한다. 검증을 채우기 위해 새 gameplay 기능을 추가하지 않았다.

따라서 자동화된 변경 계약 검증은 완료됐지만 필수 수동 Player/Editor 항목이 남아 PR은 Draft로 유지한다. 독립 읽기 검토에서는 확정적인 runtime 병합 결함을 발견하지 못했으며, 이 검토를 Unity 실행 증거로 세지 않았다.
