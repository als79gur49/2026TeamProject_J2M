# Steam 재시작 선행 실험 수정안

2026-09-08. 대상: `/mnt/d/J2M/worktrees/exhibition-reset`.
상태: 독립 재검토 및 문서 검토의 네 가지 보강 사항을 반영한 제안. 이 문서 작성으로 구현·검증·배포가 완료된 것은 아니다.

## 근거와 목표

BuildID `25184112`의 시험 `20260908T100335465Z-e1af2658b5ea41669b165c0ce6c46cb7`에서는 기존 Steam 종료와 새 Steam 생성이 확인됐다. 이후 probe는 `ProbeInitStarted`만 기록하고 `CCrossProcessPipe::BWrite GetLastError=6` native fatal로 끝났다. Init 반환, 준비 확인, Shutdown 성공은 확인되지 않았다.

설치된 helper 소스·스크립트 네 파일은 검토한 소스와 해시가 일치했다. 이전 Steam 환경 상속은 코드로 확인되지만 실제 잘못된 값의 존재·SDK 사용·이번 fatal과의 인과관계는 미확정이다. 환경 정리가 이 오류를 해결한다고 미리 결론 내리지 않는다.

목표는 알려진 제어 결함과 진단 누락을 수정해 다음 **한 번의 조율된 Probe 시험**에서 성공 조건 또는 실패 지점을 판정할 수 있게 하는 것이다. 기존 진단 변경을 보존한다. 제품 CompletedReset 연결, 초기화·업적 쓰기, Editor 교체 구현은 이 수정의 범위가 아니다.

근거 기록:

- `/mnt/d/J2M/evidence/participant-restart-preflight/20260908-multi-review/review.md`
- `/mnt/d/J2M/evidence/participant-restart-preflight/20260908-multi-review/reaudit.md` — 심각도와 표현은 이 재검토를 우선한다.
- `/mnt/d/J2M/evidence/participant-restart-preflight/20260908-multi-review/plan-review.md` — 실제 생성 경계, 기록/lock 소유권, 오류 수용 조건, 정리 예산의 문서 검토 이력.
- [현재 실험 구현 및 검증 이력](Participant-Restart-Preflight.md)

## 우선순위와 변경 파일

| 우선순위 | 내용 | 주요 파일 |
|---|---|---|
| P1 | 음수 대기와 제한 이후 성공 차단 | `RestartExperiment.cs`, `RestartExperimentWindows.cs` |
| P2 | 실행 역할별 Steam 환경 구성 | `RestartExperimentWindows.cs` |
| P2 | SDK·프로세스·cleanup 진단 보존 및 출력 수집 상한 | `RestartExperimentNativeProbe.cs`, `RestartExperimentWindows.cs`, `Restart-Experiment.ps1` |
| P2 조건부 | 종료 명령과 실제 client의 수명 분리 | `RestartExperiment.cs`, `RestartExperimentWindows.cs` |
| 검증 | 위 경계와 배포 파일 일치 확인 | `RestartExperimentTests.cs`, `Tools/Exhibition/Tests/Restart-Experiment.Tests.ps1`, 기존 builder/postprocessor |

첫 네 항목을 함께 검증한 뒤 시험 빌드를 만든다. stdout과 결과 JSON의 충돌은 미관측 위험으로 유지하며, 이번 수정에서 대규모 IPC를 필수로 추가하지 않는다.

## 1. 시간 제한 계약

기존 부모 종료 30초, Steam 종료 60초, 준비 관찰 120초를 유지한다. 단조 시계를 사용하고 한 단계에서 deadline을 다시 시작하지 않는다.

- 비용이 있는 상태 검사 **반환 후** 시간을 검사한다. `alive()==false`와 `ready==true`도 제한을 우회하지 못한다.
- 성공 관측 시각이 deadline 이상이면 timeout으로 처리한다. 정확히 deadline인 경계도 테스트한다.
- 남은 시간을 한 번 계산하고 그 값으로 `<=0` 거부와 다음 대기·probe budget을 결정한다. 검사와 사용 사이에 시간을 다시 읽어 음수를 만들지 않는다.
- `Delay`는 양수만 받도록 방어한다. `Math.Max(0, ...)`로 잘못된 제어 흐름을 숨기지 않는다.
- 준비 관찰의 후속 identity/중복 검사와 최종 실행 전 검사가 끝난 뒤에도 deadline을 확인한다. 제한 이후 새 probe나 게임 생성 요청을 하지 않는다.
- 같은 절대 deadline과 단조 시계를 backend까지 전달한다. `ProbeReady` 내부의 client 검사·환경 구성, `StartGame` 내부의 중복 검사·파일 해시 검증 등 마지막 비용 작업이 끝난 뒤 실제 `Process.Start` 요청 직전에 다시 검사한다. 바깥 `Cycle` 검사만 추가하거나 backend 진입 시 상대 예산을 새로 시작하는 구현은 허용하지 않는다. 프로세스 생성 자체와 OS 스케줄링의 반환 상한을 보장한다는 뜻은 아니다.
- 성공 또는 timeout 뒤 lock 해제는 기존 `using/finally` 계약을 유지한다. 진단 실패가 해제를 건너뛰게 하지 않는다.

제안 흐름은 `관측 → 제한 검사 → 결과 판정 → 같은 remaining 값으로 양수 대기`이다. 종료 대기의 `while (alive())`처럼 false 반환이 시간 검사를 건너뛰는 구조를 없앤다.

120초는 새 Steam 생성 및 해당 단계 기록 뒤 시작하는 준비 관찰 예산이다. 소유 probe 출력 회수 최대 1초와 종료 확인 최대 2초는 별도 정리 예산이며, 한 시도에서 각 예산을 한 번만 사용한다. 동기 프로세스 생성·파일 해시·로그 I/O까지 포함한 helper 전체의 절대 종료 상한은 보장하지 않는다. 이 한계를 해결한다는 이유로 외부 감독 프로세스를 조용히 추가하지 않는다.

## 2. 실행 역할별 환경 정책

환경 구성 함수를 분리하고 실제 `Process.Start`에 전달되는 `ProcessStartInfo`를 무해한 자식 프로세스 테스트로 검증한다.

| 실행 역할 | 환경 정책 |
|---|---|
| 최초 GameOnly/실험 helper | 현재 인계 환경을 유지한다. 환경 정리를 helper 추적 분리 수단으로 사용하지 않는다. |
| Steam 정상 종료 명령·새 Steam | 현재처럼 대소문자 무시 `Steam*` 항목을 제거한다. AppID 자동 실행 인자를 넣지 않는다. |
| 새 Steam 준비 probe | 상속된 `Steam*`를 제거한 뒤 현재 request의 `SteamAppId`, `SteamGameId`만 설정한다. |
| FullCycle 최종 게임 | probe와 같은 새 client 환경 정책을 사용한다. 같은 exe와 기존 provider/observation 인자를 유지한다. |
| GameOnly 최종 게임 | 기존 client를 사용하는 현재 정책을 유지하고 별도 회귀 검증한다. |

OS의 PATH·TEMP·사용자 환경 등은 보존한다. 작업 디렉터리, DLL 경로, nonce 부여와 소유 JobObject 계약은 별개로 유지한다. 새 환경 정책은 연결·추적 해제·Overlay 갱신의 보장이 아니다.

실제 시험 로그에는 환경 정책 버전, 정리 대상 변수의 이름/존재 여부, 현재 AppID 설정 일치 여부만 기록한다. 전체 환경이나 계정·연결 값은 덤프하지 않는다. 환경 구성·검증 실패 시 probe/게임 생성 전에 중단한다.

## 3. SDK 진단과 프로세스 결과

### SDK 결과

`InitFlat`에 전달하는 1024바이트 계약의 버퍼를 유지하고 반환한 설명을 읽는다. 로컬 SDK165 헤더의 반환형·호출 규약·export 계약을 유지한다.

- `InitResult`와 별도로 길이가 제한된 `InitDiagnostic` 필드를 둔다.
- 정상 `NoSteamClient(2)` 설명은 fatal용 `Error`에 넣지 않는다. 기존 제한 내 새 프로세스 관찰을 유지한다.
- Init 성공 이후 query 실패, Shutdown 실패, 기록 실패를 구분해 보존한다. 뒤의 cleanup 오류가 최초 오류를 덮어쓰지 않게 한다.
- 성공 Init 후 Shutdown 시도, 실패 Init 후 추가 SDK 호출·Shutdown·DLL unload 없이 소유 프로세스 종료라는 기존 계약을 유지한다.
- native fatal처럼 Init 자체가 반환하지 않은 경우에는 SDK 설명을 만들어내지 않는다.

이는 진단용 wire 결과 변경이다. 필수 필드/진단 형식을 명시하고 PS host, C# 소스, 테스트를 한 묶음으로 배포한다. 이전·부분 결과가 조용히 성공하지 않도록 거부 테스트를 둔다. 제품 reset journal schema는 바꾸지 않는다.

### 감독 프로세스의 결과 기록

유효 JSON을 얻은 경우에만 쓰는 `probes.jsonl`과 별도로, 시작 시도마다 `probe-attempts.jsonl`에 종료 결과를 기록한다. 생성 여부가 불확실하면 그대로 기록하며 성공으로 추정하지 않는다.

필드는 시도 번호, nonce, UTC, 단조 경과 시간, PID+StartTicks(확보한 경우), 실패 단계, exit 확인/exit code(관측한 경우), stdout/stderr, 잘림 여부, 결과 파싱/identity 검증 결과, 소유 종료 확인, 최초 오류와 cleanup/기록 오류로 구성한다. 확보하지 못한 값은 0 성공값으로 대체하지 않는다. 이 파일은 재개 저널이나 자동 재시도 입력이 아니다.

실패 경로를 `최초 실패 원인 보존 → 필요한 소유 cleanup → 제한 내 프로세스/출력 관측 확정 → 최종 결과 기록`으로 정리한다. cleanup 전 값은 최종 성공/실패 판정이 아니다. 최초 오류와 cleanup·수집·기록 오류를 함께 보존한다. 기록 예외는 별도 경로로 남기되 cleanup을 막거나 원래 오류를 덮지 않는다. 모든 기록 경로가 실패하면 증거 부족이며 성공으로 표기하지 않는다.

### 출력 수집과 판정

이번 수정은 stdout의 단일 완전 JSON과 stderr 진단 구조를 유지한다. stderr가 있거나 출력이 불완전하면 중단하는 현재 계약도 유지한다. fatal stderr를 잡음으로 무시하거나 stdout의 마지막 JSON만 골라 채택하지 않는다.

- `ReadToEndAsync` 완료 후 길이 검사 대신, stdout/stderr 각각 독립적으로 최대 16,384문자를 보관하는 수집기를 둔다. 고정 크기 청크로 읽어 보관 메모리 상한을 유지한다.
- 어느 채널이든 상한 초과는 fatal이다. 초과 뒤에도 제한된 버퍼로 배출하며 감독자가 소유 probe를 중단·정리한다. 읽기를 멈춰 파이프를 막아 두지 않는다.
- 수집기는 Unity API를 호출하지 않는다. 정리 뒤에도 읽기 종료를 확인할 수 없으면 성공을 거부하고 관측 부족을 기록한다.
- 정상 종료 코드 0, 출력 완료, 필수 필드, nonce/PID/StartTicks, SDK 결과/identity, 필요한 Shutdown 반환을 모두 만족해야 준비 결과를 수용한다.

수용 조건을 두 경로로 명시한다. 아래 공통 거부 조건은 진단 필드가 어디에 저장되는지와 무관하게 적용한다.

| 판정 | 필수 조건 |
|---|---|
| 공통 거부 | `Error` 또는 최초 실패·query·Shutdown·cleanup·수집·기록 오류, 출력 overflow, 불완전한 출력/종료 관측, wire/nonce/PID/StartTicks 불일치 중 하나라도 있으면 준비 성공과 재관찰 모두 거부 |
| 준비 성공 | 공통 거부 사유 없음, 정상 프로세스 종료, Init 성공, 실제 AppID/로그인/이번 SteamID 일치, Shutdown 반환, 소유 프로세스·출력 회수 확인, deadline 이내 |
| 제한 내 재관찰 | 공통 거부 사유 없음, 정상 프로세스 종료, 깨끗한 `NoSteamClient(2)` 또는 Init·Shutdown을 정상 완료했지만 로그인/identity가 아직 준비되지 않은 결과, 기존 deadline 잔여 있음 |

깨끗한 `NoSteamClient(2)`는 query 데이터 없음, 로그인 false, Shutdown 호출/반환 없음이어야 한다. `InitDiagnostic`은 설명 필드이므로 그 존재 자체는 실패가 아니지만, 설명이 다른 실패를 면제하지 않는다. 그 밖의 실패 Init 코드와 native fatal은 계속 중단한다. 새 진단 필드에 오류를 옮기고 기존 `Error`만 비워 성공시키는 구현을 금지한다.

출력 수집은 자식 실행 중 계속 수행한다. 정상 종료 또는 소유 종료 시도 뒤 출력 완료를 기다리는 총 추가 예산은 한 시도당 1초이며 stdout/stderr가 공유한다. 소유 프로세스를 중단해야 하면 종료 확인에는 한 시도당 2초를 사용한다. 이미 소비한 출력 회수 예산은 뒤의 cleanup에서 다시 시작하지 않는다. 수집 태스크의 예외·미종료, 파이프 종료 확인 실패는 별도 실패로 회수한다. 남은 출력이나 태스크를 무기한 기다리는 마지막 `Wait`를 추가하지 않는다. 미완료 시 확보한 제한된 출력과 관측 부족을 기록하고 실패로 끝낸다.

실제 성공 Init에서 SDK의 정상 출력과 JSON이 충돌한다는 증거가 생기면 결과 전용 채널을 별도 설계한다. 그때도 완전성·소유 프로세스·정상 종료 검증을 유지한다.

## 4. 정상 종료 명령의 수명

종료 명령 반환과 실제 Steam 종료는 계속 별개의 관측으로 취급한다.

- `-shutdown` 명령을 시작할 때 소유 `Process`와 실제 handle을 유지한다. PID만으로 다시 찾아 소유권을 추정하지 않는다.
- 정상 종료 단계 진입 시 하나의 60초 deadline을 정하고, 명령 생성·원본 client 종료·명령 프로세스 종료 관측에 공유한다. 각각 새 60초를 부여하지 않는다.
- 원본 client 검사에서는 정확한 소유 명령 PID+StartTicks만 제외한다. 원본 client 종료 후에도 소유 명령이 종료됐음을 별도로 확인한다.
- 두 종료가 확인되고 새/대체 client가 없을 때만 Survival 완료 또는 새 Steam 생성 단계로 간다. 명령이 계속 살아 있으면 `ShutdownCommandExitTimeout`으로 중단한다.
- 명령 exit code는 진단으로 남긴다. 성공 exit code를 client 종료 증거로 삼지 않는다. 비정상 코드에서는 보수적으로 중단한다.
- 소유 명령 정보 획득 실패·명령 생성 여부 불확실·사용자 재실행·업데이트 replacement는 추가 종료/실행 요청 없이 중단한다.
- 종료 명령을 강제로 죽여 진행하지 않는다. handle 해제는 프로세스 강제 종료를 뜻하지 않는다. 이 정리와 기존 probe 전용 강제 종료 권한을 혼합하지 않는다.

최종 상태 기록까지 기존 공통 cycle lock을 유지하고 성공/실패에서 해제한다. 명령이 잔존한 실패 뒤 자동 cycle 재시도는 없다.

현재 PS host가 `Cycle.Run` 반환/예외 뒤 쓰는 `HelperCompleted/HelperFailed`와 lock 소유 범위를 함께 조정한다. lock을 획득한 cycle의 순서는 `실행 및 소유 cleanup → lock 안에서 최종 상태 기록 → finally에서 lock 해제 → 외부 오류 팝업`으로 고정한다. 팝업을 닫을 때까지 lock을 유지하지 않는다. 최종 기록의 주체는 cycle 경계 한 곳으로 두고 PS host가 중복되거나 상충하는 cycle 완료 기록을 쓰지 않게 한다. 기록 실패에도 lock을 해제하며, 성공 기록 실패는 성공 증거로 취급하지 않는다. lock 획득 전 실패는 별도 host/bootstrap 실패로 기록하고 cycle 실행 성공을 주장하지 않는다.

## 5. 테스트 수용 기준

| 영역 | 추가로 검증할 경우 |
|---|---|
| 시간 | Ensure/`alive()` 중 시간 경과, 연속 clock 조회 경과, backend의 최종 검사·환경 구성·해시 검증 중 deadline 초과 후 실제 생성 요청 0, 정확한 deadline 및 1/2ms 초과, ready true/false, alive true/false, 양수 Delay 방어, timeout 시 추가 실행 0·lock 해제 |
| 환경 | 합성 Steam 항목 대소문자 변형, 제거/보존 목록, AppID 덮어쓰기, probe/FullCycle/GameOnly 역할 차이, 실제 launch 생성 함수 사용 |
| SDK | 반환 설명 보존, NoSteamClient 설명만 있으면 재관찰 유지, 그 결과에 별도 실패가 있으면 거부, Init 미반환, query+Shutdown+기록 오류 동시 보존, identity/Shutdown 값이 정상이어도 별도 오류가 있으면 성공 거부 |
| 프로세스 | 빈 stderr의 비정상 exit, 부분 출력 timeout, 잘못된/누락된 결과, 유효 JSON 뒤 상한 초과도 거부, stdout/stderr 상한 초과, 늦은 파이프 닫힘, cleanup 실패, 기록 실패, 수집 태스크 예외·미종료, stdout/stderr 공유 1초 및 종료 확인 2초 예산 재시작 없음 |
| 종료 명령 | client 먼저/명령 먼저 종료, 명령 잔존, 동일 60초 예산, 비정상 exit, 생성/handle 획득 실패, PID 재사용, 사용자 replacement, Survival/Probe/FullCycle 각각 |
| 기존 계약 | 최초 GameOnly Steam 종료 0, client cycle 1회, fatal 재시도 0, 명시 미생성 retry 외 추가 helper 0, 공통 lock 경합 시 실행 0 |
| 기록/lock | 성공·실패에서 cleanup→최종 기록→해제 순서, 기록 시 lock 보유, 기록 예외에도 해제, 팝업 시 lock 해제, PS 중복 완료 기록 없음, lock 획득 전 실패 분리 |

환경·파이프·종료 명령 테스트는 무해한 Windows 자식 프로세스와 fake로 실행한다. 실제 Steam/native probe를 자동 테스트 준비 과정에 호출하지 않는다. 실제 launch 코드가 사용하는 seam을 검증하고 동일 구현을 복사한 테스트를 만들지 않는다.

대상 worktree에서 `./run_tests.sh core`, `./run_tests.sh full --filter 'RestartExperimentTests;ParticipantResetDiagnosticsTests;ParticipantResetServiceTests;SteamExhibitionResetProtocolTests;ExhibitionResetCoordinatorTests;FileExhibitionResetJournalTests'`, 기존 Windows helper 테스트를 실행한다. 증거 경로는 D 아래에 둔다. UI 파일을 변경하면 ui lane을 추가한다. UI 무변경이면 미실행 사유를 기록한다. 필터 없는 full 및 실제 Editor lifecycle 미실행을 별도로 표기한다.

## 6. 구현·배포·실제 시험 순서

1. 시간/환경/명령 수명/진단을 구현하고 위 focused·Windows fake 검증을 완료한다.
2. 수정 diff를 독립 재검토한다. P1 및 다음 시험 판정을 막는 미해결 문제가 있으면 시험 빌드를 내지 않는다.
3. 필요한 lane을 통과한 동일 소스로 기존 builder를 사용한다. 임시 define 복원, 네 helper 소스·스크립트와 player 소스 일치, 모든 prerequisites 예제 비활성, `steam_appid.txt` 없음, payload manifest·해시를 확인한다.
4. 배포 시 시험 빌드 식별자와 변경 내용을 기록한다. 업로드·branch 활성화·설치 확인을 구분한다. 수정 소스 일부만 바꾼 혼합 설치를 이번 검증 대상으로 삼지 않는다.
5. 실제 Steam 빌드/파일 해시 일치를 확인한 뒤 이미 Ready인 게임에서 명시적 Probe 인자와 Ctrl+Shift+F10 한 번으로 시험한다. 새 환경 정책 시험을 위해 초기화·재획득을 준비하지 않는다.
6. Steam 재실행, Init 반환, AppID/로그인/이번 SteamID 일치, Shutdown 반환, probe 정상 종료, helper 종료, Steam 추적 해제를 각각 기록한다. Probe에서는 게임 자동 실행 0이다.
7. 실패하면 해당 단계 자료를 검토하고 중단한다. 같은 미수정 빌드 반복 입력이나 client cycle 자동 재시도를 요청하지 않는다. 성공한 경우에만 Gate B 근거를 갱신하고 별도 FullCycle 시험으로 넘어간다.

Gate B 성공은 Overlay 목록 갱신이나 제품 참가자 초기화 성공을 뜻하지 않는다. FullCycle에는 같은 exe 생성 1회와 실제 새 메뉴 준비, 게임 종료 후 Steam 실행 중 표시 해제의 별도 관측이 필요하다. 제품의 두 게임 교체·canonical Ready·완료 retry·Editor Domain Reload·KO/EN UI 검증은 원래 제품 구현 단계에 남긴다.

## 이번 문서 작업의 검증 상태

수정안 작성 및 기존 소스·검토 기록 대조만 수행했다. 코드 수정, 테스트 lane, 빌드·업로드, 실제 Steam/game/probe 실행과 업적 변경은 수행하지 않았다. 과거 테스트 통과를 이 수정안의 구현 성공 증거로 사용하지 않는다.
