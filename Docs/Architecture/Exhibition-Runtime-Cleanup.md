# 전시 초기화 유지와 Steam smoke·계측 정리

상태: 구현 및 아래 자동 검증 완료. 현재 작업은 `/mnt/d/J2M/worktrees/ex-clean`, branch `fix/exhibition-runtime-cleanup`이다. 전시 구현 `30d575140`을 기준으로 main의 smoke 제거 `80a203573`을 통합했다. 이 문서는 새 빌드 업로드나 실제 Steam 초기화 성공의 증거가 아니다.

## 구현 범위

- Spacewar smoke 상태 머신·정책·DTO·플래그·정상 Publisher 차단을 제거했다. Steam diagnostics는 상태·초기화 결과·AppID·identity/login·실패 정보로 축소한다. 정상 native 초기화·단일 pump·멱등 종료·실패 격리는 유지한다.
- Overlay/DLL 진단 호출과 runtime 횟수·JSON 출력, Publisher StatsStored 기록을 제거한다. Packsize/native 오류 처리와 identity/login 조회는 유지한다. DLL 관측에서만 발생하던 초기화 중단은 해당 호출과 함께 사라진다.
- 일반 Publisher는 정확한 이름의 full-unlock 콜백, FIFO, pre-read, Set/Store, monotonic timeout, quarantine을 유지한다. StatsStored 수신은 원자적 callback pair 안에 남지만 일반 업적 결과를 바꾸지 않는다.
- 전시 초기화는 별도 protocol에서 StatsStored OK와 대상별 post-read false를 요구한다. maintenance lease 해제 후 최초 Publisher 연결, 실패 session 격리, Pending/Ready 원자 저장과 캠페인·장부·백업 초기화를 유지한다.
- ObservationV3, OverlayHandoffObservation, ResetOverlayTrial, ParticipantResetDiagnostics, experiment hook 및 관측 전용 플랫폼 startup permit을 제거한다. 일반 제품·캠페인·Steam publication 시작 보류와 subsystem reset은 유지한다.
- 운영 Steam 재시작에 필요한 helper·native readiness probe·기존 deadline·프로세스 소유권·request hash 검증·한 번 실행 제출을 유지한다. JSONL 기록, 관측 child 실행, 진단 전용 builder/검사 도구는 제거한다. 실제 release/SteamPipe/의존성 도구와 vendored Steamworks는 보존한다.
- native 단계 기록 실패, helper 단계/terminal 기록 실패로 운영을 중단하던 진단 전용 경로가 사라진다. native Init/query/shutdown/cleanup, IPC 형식·크기·stderr, process ownership 실패는 계속 실패다.

## 검증

현재 worktree의 `./run_tests.sh`만 Unity lane 진입점으로 사용한다. 새 증거는 `/mnt/d/J2M/evidence/exhibition-runtime-cleanup-20260911`에 둔다.

```bash
CODEX_VALIDATION_ROOT=/mnt/d/J2M/evidence/exhibition-runtime-cleanup-20260911/core-final ./run_tests.sh core
CODEX_VALIDATION_ROOT=/mnt/d/J2M/evidence/exhibition-runtime-cleanup-20260911/ui ./run_tests.sh ui
CODEX_VALIDATION_ROOT=/mnt/d/J2M/evidence/exhibition-runtime-cleanup-20260911/steam-product ./run_tests.sh full --filter 'Game.Platform;Game.Product.Achievements;CampaignStageAchievement;CampaignStageFlow'
CODEX_VALIDATION_ROOT=/mnt/d/J2M/evidence/exhibition-runtime-cleanup-20260911/exhibition-release ./run_tests.sh full --filter 'Game.Exhibition;WindowsReleaseBuildPipelineTests;WindowsDistributionStagerTests'
UNITY_EDITMODE_ASYNC=1 UNITY_GRAPHICS=1 CODEX_VALIDATION_ROOT=/mnt/d/J2M/evidence/exhibition-runtime-cleanup-20260911/editor-session ./run_tests.sh full --filter 'ParticipantEditorSessionTests;ParticipantMenuPreviewTests'
UNITY_EDITMODE_ASYNC=1 UNITY_GRAPHICS=1 CODEX_VALIDATION_ROOT=/mnt/d/J2M/evidence/exhibition-runtime-cleanup-20260911/menu-playmode ./run_tests.sh full --filter ParticipantResetMenuPlayModeTests
powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:\J2M\worktrees\ex-clean\Tools\Exhibition\Tests\Restart-Experiment.Tests.ps1'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:\J2M\worktrees\ex-clean\Tools\Build\Tests\Build-WindowsRelease.Tests.ps1'
```

XML에서 fixture별 leaf test-case 실행 수를 확인한다. yielding Editor fixture는 별도 async 실행에서 0이 아닌 실제 실행을 확인한다. filtered full은 unfiltered full 통과를 뜻하지 않는다. UI 관측 presentation을 제거하고 정상 메뉴 초기화 경로를 유지하므로 ui 검증을 포함한다.

수동 범위: 실제 Steam 계정에서 참가자 A 획득, 메뉴 초기화, 자동 게임·Steam 재시작, 같은 AppID/계정 복귀, Overlay 표시 및 미획득 확인, 참가자 B 같은 업적 재획득, 다음 실행의 pending 해소. 실제 계정·사용자 저장소는 자동 테스트에서 변경하지 않는다. 빌드/업로드 및 이 수동 검증은 자동 fake 결과와 분리한다.

## 구현 중 보완한 운영 경계

- `ExhibitionApplication`의 composition 실패가 native 등록보다 먼저 일어나도 자동 publication을 보류한다. 실패 후 callback pump는 유지하면서 Get/Set/Store가 발생하지 않는 회귀 테스트를 추가했다.
- `WindowsDistributionStager`가 운영 재시작 helper 네 파일을 배포에 포함하도록 수정했다. `WindowsDistributionTargetPolicy`와 `Build-WindowsRelease.ps1`은 helper의 정확한 위치와 Steam 대상의 Exhibition Application/Integration DLL을 필수로 검사한다. 누락·잘못된 위치·복사 내용 보존을 테스트한다.
- 전시 reset protocol, 정상 publisher, maintenance lease 및 운영 helper의 분리를 두 서브 에이전트가 다시 검토했다. 지적된 시작 보류, 진단 기록 잔재, 테스트 fake 및 배포 누락을 수정한 뒤 추가 차단 사항이 없음을 확인했다.

## 자동 검증 결과 (2026-09-11)

| 실행 | 실제 결과 |
| --- | --- |
| core-final | EditMode 254 통과, PlayMode 107 통과·4 건너뜀 |
| ui | EditMode 1,359 통과 |
| steam-product: 요청된 정확한 filter | EditMode 450 통과, PlayMode 29 통과 |
| exhibition-release | EditMode 386 통과·1 건너뜀, PlayMode 0 통과·1 건너뜀 |
| editor-session: graphics/async | EditMode 2 통과, PlayMode 선택 0 |
| menu-playmode: graphics/async | EditMode 선택 0, PlayMode 1 통과 |
| Windows 운영 helper PowerShell | 77 통과; 이 harness의 실제 Steam/game/native 실행 0 |
| Windows release wrapper PowerShell | 145 통과 |

core의 건너뛴 네 테스트는 그래픽 장치가 필요한 렌더링 연속성·aperture·campaign terminal 캡처 테스트다. exhibition-release에서 건너뛴 메뉴 prefab preview는 별도 editor-session에서 실제 통과했고 한·영 캡처를 확인했다. 포인터·오류 재시도 PlayMode 테스트도 별도 menu-playmode에서 실제 1개 통과했다.

초기 실행의 컴파일 및 배포 fixture 실패는 수정 후 위 실행으로 재검증했다. 모든 대상을 합친 `targeted-final`은 EditMode XML에 836 통과·1 건너뜀이 남았지만 300초 제한으로 명령이 실패했으므로 통과 증거로 사용하지 않는다. 위 두 filtered full 명령으로 나누어 각각 성공을 확인했다. **unfiltered full은 실행하지 않았으며 전체 full 통과를 주장하지 않는다.**

fixture별 실제 leaf test-case 수는 증거 디렉터리의 `fixture-results.tsv`, 변경 파일 해시는 `source-manifest.json`에 기록한다. Editor session 재진입/초기화 및 메뉴 preview는 별도 그래픽 실행의 실제 실행 수로 확인한다. UI lane과 한·영 메뉴 캡처는 실제 Steam Overlay 또는 초기화 성공 증거가 아니다.

새 worktree 저장 정책 감사는 통과했다. 기존 C worktree의 사용자 변경과 미추적 문서는 보존했다. 이 작업은 아직 미커밋이며 merge를 commit 전 상태로 유지한다. 실제 Windows 빌드 생성·Steam 업로드·전시 계정 초기화·자동 복귀·재획득 수동 검증은 이번 실행에 포함하지 않았다.
