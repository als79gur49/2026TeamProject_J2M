# 참가자 초기화의 게임 내부 Steam 진단

> 이 문서는 당시 revision의 설계·관찰·검증 이력이다. 전시 초기화·재시작을 보존한 smoke·관측 구조 정리 후 현재 코드와 검증 상태는 [Exhibition Runtime Cleanup](../Architecture/Exhibition-Runtime-Cleanup.md)을 따른다. 아래 과거 명령과 삭제된 관측 도구는 현재 실행 절차가 아니다.


대상: `/mnt/d/J2M/worktrees/exhibition-reset`. 목적은 게임 내부 업적 값과 Steam Overlay 표시의 불일치를 조사하는 것이다. Overlay 갱신 해결책이 아니며 초기화 순서, 저널 형식, 재시작 횟수는 변경하지 않는다.

## 활성화와 출력

- 전용 Windows 진단 빌드에만 `J2M_PARTICIPANT_RESET_DIAGNOSTICS` scripting define을 추가한다. 일반 빌드에서는 factory가 null을 반환하며 진단 조회/파일 기록을 하지 않는다.
- define은 같은 exe로 재실행해도 유지된다. helper/API/Steam 클라이언트 수명은 변경하지 않는다.
- batch 실행에서는 define 유무와 관계없이 실제 Steam 진단 객체를 만들지 않는다. 자동 테스트는 fake 조회와 sink를 주입한다.
- 출력: `D:\J2M\evidence\participant-reset-diagnostics\<UTC시각-PID>\observations.jsonl`. Unity 프로젝트나 저장 폴더에는 진단 로그를 쓰지 않는다.
- 파일에는 SteamID가 포함되므로 외부 보고 시 계정 식별자를 마스킹한다.

## 관측 단계

| stage | 시점 | 업적 값의 출처 |
|---|---|---|
| SteamResetVerified | 기존 Store 성공 callback 및 모든 대상 readback 통과 직후 | 기존 성공 readback 재사용; 추가 업적 조회 없음 |
| BeforeServices | Pending 복구 Resume 정상 반환 후, startServices 직전 | 게임 내부 GetAchievement |
| MenuReady | 메뉴 준비 및 reconciliation 성공 후 | 게임 내부 GetAchievement |

각 단계는 프로세스/관측 객체당 최대 한 번 기록한다. 과거 Ready 또는 기록 없는 정상 시작은 MenuReady만 관측한다. initialJournalState와 현재 journalState, operationId를 구분하며 과거 Ready의 계정 불일치는 진단 정보일 뿐 실행 제한이 아니다. Ready 확정 실패 시 BeforeServices를 기록하지 않는다.

utc, sessionId, processId, executable, build(Unity buildGUID/version), initialJournalState, journalState, operationId, 현재/기록된 identity, identityMatches, 조회 오류, 업적별 name/querySucceeded/achieved/error를 기록한다. SteamResetVerified에는 기존 storeResult도 포함한다. querySucceeded=false의 achieved 값은 미획득 증거로 해석하지 않는다. identity 조회 실패는 업적 조회를 건너뛰고 unknown 사유를 기록한다.

진단 sink/조회/파일 실패는 reset 또는 메뉴 성공 판정으로 전파하지 않는다. 기록이 없다는 사실은 초기화 실패/성공 어느 쪽도 증명하지 않으므로 출력 파일의 존재와 stage를 먼저 확인한다. 별도 native init/shutdown, callback 등록/해제, Clear/Set/Store/Reset 호출은 없다.

## 실제 검증

1. 진단 빌드의 동일 exe 경로와 AppID를 확인한다. 설치본 교체/배포는 별도 작업이며 이 진단이 자동으로 수행하지 않는다.
2. 사용자가 초기화할 때 세션별 파일을 확보한다. 자동으로 업적 재획득/재삭제를 준비하지 않는다.
3. 초기화 후 메인 메뉴에서 Overlay 화면과 촬영 시각을 기록한다. 게임/Steam을 유지하고 업적 재획득 없이 비교한다.
4. 세 stage가 모두 미획득인데 Overlay만 획득이면 표시 갱신 문제의 근거다. BeforeServices는 미획득이고 MenuReady가 획득이면 서비스 시작/reconciliation/전송 경로를 추가 조사한다.
5. 조회 오류나 identity 불일치가 있으면 판단을 보류한다. 이 로그는 게임 내부 조회이지 Steam 서버 직접 조회 또는 Overlay 내부 캐시 관측이 아니다.

## 자동 검증

`./run_tests.sh core` 및 `./run_tests.sh full --filter 'ParticipantResetDiagnosticsTests;ParticipantResetServiceTests;SteamExhibitionResetProtocolTests;ExhibitionResetCoordinatorTests;FileExhibitionResetJournalTests'`를 대상 worktree에서 실행한다. 증거는 `CODEX_VALIDATION_ROOT`로 D 드라이브에 지정한다.

집중 테스트는 disabled/batch factory의 무조회, 기존 readback 재사용, stage 순서, 초기화 실패 시 성공 관측 부재, historical Ready 통과, 조회 오류와 false 구분, 중복 관측 방지, sink 예외 격리를 확인한다. UI/Prefab은 변경하지 않아 ui lane은 이번 범위에서 미실행한다. 전체 full lane과 실제 Steam/Overlay 검증 결과는 집중 테스트와 구분한다.

## 2026-09-07 구현 검증

| 검사 | 결과 | D evidence 하위 경로 |
|---|---|---|
| 위 필터의 집중 EditMode | 60 통과, 실패 0 | participant-reset-diagnostics-focused-final/test-results |
| 같은 필터 PlayMode | 선택 0건, 수명 검증 증거 아님 | participant-reset-diagnostics-focused-final/test-results |
| core EditMode | 254 통과, 실패 0 | participant-reset-diagnostics-core/test-results |
| core PlayMode | 107 통과, 실패 0, 그래픽 전용 4 skip | participant-reset-diagnostics-core/test-results |

첫 집중 시도는 새 파일이 아직 Unity 생성 csproj에 등록되지 않아 Windows dotnet 단계에서 실패했다. Unity import/SyncSolution으로 프로젝트와 meta를 생성한 뒤 v2에서 통과했다. 이후 진단 빌드의 심볼 설정을 복원하고 factory 무조회 테스트의 카운터 검증을 보강한 최종 집중 검사에서도 60개가 통과했다. 일반 심볼의 컴파일된 Integration DLL에는 진단 출력 경로 문자열이 없음을 추가 확인했다.

전체 full, ui, 진단 빌드의 실제 계정 초기화/Overlay 비교 및 Editor 수동 Play 검증은 미실행이다. 자동 테스트는 fake Steam과 임시/메모리 저장소를 사용했다. 게임 실행이나 실제 업적 변경을 자동으로 수행하지 않았다.

## Windows 진단 빌드

`D:\J2M\builds\participant-reset-diagnostics\.staging-diagnostics\VectorQuake.exe`에 별도 빌드를 생성했다. 기존 `WindowsReleaseBuildCli.BuildWindowsX64NonDevelopment` 경로를 사용했으며 결과는 Succeeded, 오류 0, 경고 1(Unity Cloud native symbol 업로드 토큰 없음)이다. 실제 게임은 실행하지 않았다.

빌드 시에만 Standalone define을 추가했고 `ProjectSettings.asset`의 전후 SHA-256 일치로 복원을 확인했다. Player의 Integration DLL에 진단 출력 경로가 포함되며 기본 심볼 DLL에는 없었다. 동봉 helper는 저장소 원본과 일치하고 steam_api64.dll은 포함되며 steam_appid.txt는 없다. 증거와 실행 스크립트는 `/mnt/d/J2M/evidence/participant-reset-diagnostics-build`에 있다. 첫 빌드 시도는 `.staging-` 출력 경로 계약을 만족하지 않아 exit 10으로 종료됐고, 경로를 조정한 재시도는 exit 0이다.

Steam 클라이언트를 유지하고 기존 게임만 종료한 뒤 Windows PowerShell에서 다음 명령으로 **진단 빌드**를 수동 실행한다. 계정/업적은 명령으로 변경하지 않는다. 게임 자체는 기존 저장 경로를 사용한다.

```powershell
$env:SteamAppId = '5218360'
$env:SteamGameId = '5218360'
& 'D:\J2M\builds\participant-reset-diagnostics\.staging-diagnostics\VectorQuake.exe' -j2mPlatformProvider steam
```

기존 Ready 상태에서 실행하면 MenuReady만 기록되는 것이 정상이다. 세 단계 비교는 사용자가 이 진단 빌드에서 새 초기화를 수행한 뒤 가능하다. 초기화 후 게임과 Steam을 유지한 채 Overlay 화면과 촬영 시각을 함께 확인한다. 이 빌드는 조사용이며 Steam 설치본 교체나 배포를 수행하지 않았다.

## SteamPipe 업로드 (2026-09-07 22:21 KST)

사용자 요청으로 위 진단 빌드를 SteamPipe에 업로드했다. AppID `5218360`, DepotID `5218361`, **BuildID `25167899`**, depot manifest `5945815295630630760`. SteamCMD exit 0과 app/depot 성공 로그를 확인했다. `SetLive`는 생략했으며 브랜치 활성화/설치본 업데이트는 수행하지 않았다.

Stager로 252개 파일(381,502,874 bytes)을 검증했다. manifest SHA-256은 `f68648e450e3680f805373bb266f047a1635a3038da41330ba5ef34ce88ac342`다. 업로드 전 전체 파일 해시와 진단 코드 포함 여부를 다시 확인했다. raw 빌드에 필수 배포 메타데이터와 저장소 라이선스 고지 파일을 보완한 뒤 staging이 통과했다. 첫 staging은 metadata 부재로 실패했고, 빌드를 다시 만들거나 검증 정책을 완화하지 않았다.

결과 증거: `/mnt/d/J2M/evidence/participant-reset-diagnostics-build/steam-upload-result.json`. 실제 적용은 Steamworks Builds에서 위 BuildID를 원하는 브랜치에 설정한 뒤 확인해야 한다.
