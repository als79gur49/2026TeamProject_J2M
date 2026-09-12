# Overlay v3 단회 Steam 재실행 후보

> 이 문서는 당시 revision의 설계·관찰·검증 이력이다. 전시 초기화·재시작을 보존한 smoke·관측 구조 정리 후 현재 코드와 검증 상태는 [Exhibition Runtime Cleanup](../Architecture/Exhibition-Runtime-Cleanup.md)을 따른다. 아래 과거 명령과 삭제된 관측 도구는 현재 실행 절차가 아니다.


이 문서는 runtime-3 소스 계약이다. 업로드·설치·실제 Steam 재실행 성공을 뜻하지 않는다. runtime-2 실행 기록은 변환하지 않는다.

## 변경한 실행 관계

Origin은 기존 준비·보고·최종 grant 승인을 수행하고 정상 종료한다. helper는 origin 종료 확인, Steam 정상 종료·재시작, readiness probe 및 probe 종료 확인을 마친다. context와 submission-intent를 저장한 뒤 `steam.exe -applaunch`를 한 번 요청하고 종료한다. 요청용 process 종료·replacement 연결·보고·종료를 기다리지 않는다. 제출 후 process identity 조회나 결과 파일 저장도 하지 않는다. observation PS 오류는 비대화형 종료이며 일반 reset 오류 안내는 그대로다.

정상 종료를 기다리는 원본 게임·원본 Steam과 종료 명령은 보관된 OS 핸들로 확인한다. 종료 중인 프로세스의 live identity를 PID로 재조회하지 않는다. 종료 명령은 생성 시 반환된 Process를 보유하고 실제 종료 코드 0을 확인한다. `new-client`의 `ShutdownCommand` identity는 실행 직전 검증한 파일 path/hash, 자격 증명을 변경하지 않는 직접 생성의 상속 scope, 반환 핸들의 PID/시작 시각으로 구성한 생성 기록이다. 종료 후 image/token을 다시 관측했다는 뜻이 아니다. 원본 Steam 종료와 별개로 다른 same-session Steam 출현은 거부한다.

replacement는 살아 있는 helper를 요구하지 않는다. request/context, 실제 argv/cwd, 실행 파일과 journal, 현재 Steam identity를 검증하고 create-only claim을 소비한다. canonical native 초기화와 account/App 확인, 독립 시작 기록을 마친 뒤 관찰 UI를 연다. 시작 window는 context 준비부터 30초이며 같은 Windows uptime API (`GetTickCount64`)를 사용한다. 이후 관찰 입력 idle에는 제한을 두지 않는다.

보고는 두 역할 모두 로컬 create-only 저장이다. 파일명은 역할과 sequence로 구분한다. 수락부터 출력 대기·저장·hash 확인·main-thread 승인까지 30초다. native fault, account 변경, pinned Steam 교체, 저장 실패나 deadline 초과는 최초 실패를 보존한다. Close/Alt-F4는 단일 task에서 pending 보고를 처리하고 canonical Shutdown 1회 반환과 lifecycle 저장을 확인한 뒤 Quit한다. 이 경로에는 report ACK나 exit frame이 없다.

공유 source는 현재 13개 목록을 유지한다. 신규 문서와 PS/compiled inspector의 revision은 `Version=3`, `ProtocolRevision=observation-v3-runtime-3`이다. 이전 endpoint 실행 인자는 거부한다. 일반 v2/reset 및 legacy journal schema 1은 바꾸지 않는다. 기존 protocol fixture는 공용 admission/pipe 회귀용이며 runtime-3 인계 성공 증거로 계산하지 않는다.

## 사용자 실행과 T4 절차

새 후보가 설치됐음을 확인한 뒤 진행한다. Steam 사용자 시작 옵션은 비운다. Origin 인자를 시작 옵션에 저장하면 replacement에도 붙는다.

Windows `Win+R`에 다음을 넣는다.

```text
"C:\Program Files (x86)\Steam\steam.exe" -applaunch 5218360 -j2mOverlayV3Role OriginObserver -j2mOverlayV3Owner SteamDelegated
```

Steam의 고정 실행 항목이 provider를 전달한다. revision 문자열·config·endpoint를 직접 추가하지 않는다. 별도 게임 런처는 없다.

1. 관찰 입력이 열리면 Shift+Tab으로 Overlay를 확인하고 실제 열림을 저장한다.
2. 대상별 실제 획득/미획득 표시를 저장한다. SDK baseline이 모두 false이고 표시가 판정 가능하면 0/5도 재시작을 허용한다.
3. 단회 Steam 재시작 버튼을 한 번 누른다. 관찰 구간 동안 직접 Steam/게임을 재실행하지 않는다.
4. replacement가 열리면 Overlay 열림과 대상별 표시를 저장하고 종료 버튼 또는 Alt-F4로 정상 종료한다.
5. 자동 시작을 확인하지 못했으면 시험을 종료한다. 사용자의 수동 복구는 시험의 단발 요청과 별도 사건으로 남긴다.

reset·재획득·참가자 변경을 하지 않는다. Build25212700의 이전 수동 입력 차단 결과를 반복 요구하거나 재분류하지 않는다.

## 기록의 의미

| 기록 | 의미 | 뜻하지 않는 것 |
| --- | --- | --- |
| submission-intent | 실행 준비 및 제출 예정 인자 | 실제 호출, Steam 접수, 재실행 성공 |
| helper OS exit code 0 | 요청 호출이 process 객체를 반환하고 helper가 종료 | Steam의 실행 수락 ACK |
| replacement-claim | 이 context를 해당 child가 단회 소비 | native 초기화 성공 |
| replacement-started | 검증·native 초기화·identity 확인을 마친 독립 시작 | Overlay 열림, 전체 완료 |
| 역할별 report | 해당 사용자의 표시 관찰을 로컬 저장 | helper ACK, 원격 업적 상태 증명 |
| 역할별 native-shutdown | canonical native 정리 반환 | 실제 game process 종료, Steam 추적 해제 |

report의 BaselineRef는 origin에서 한 번 읽은 SDK baseline이다. replacement SDK 업적 재측정으로 설명하지 않는다. 시작 전부터 Overlay 0/5였다면 자동 경로·Overlay 사용 가능·미획득 표시 유지를 확인하며 잔존 획득 표시 제거 성공으로 판정하지 않는다.

불명확한 호출은 재시도하거나 직접 exe 실행으로 우회하지 않는다. 실패/timeout 뒤 늦게 저장된 문서가 있더라도 그 사실로 checkpoint 또는 정상 완료를 복구하지 않는다. run의 terminal-failure와 모순되는 성공 기록은 정상 완료로 집계하지 않는다.

## 외부 관찰과 판정

`Tools/Exhibition/Observe-OverlayRestart.ps1`은 개발 시험용 읽기 전용 관찰자다. 설치 payload에 포함하지 않는다. Steam이나 게임을 시작하지 않으며 WMI·재요청·helper handshake를 사용하지 않는다. Origin 수동 실행 전에 별도 PowerShell에서 실행한다. `EvidenceRoot`는 새 D evidence 폴더여야 한다.

```powershell
& 'D:\J2M\worktrees\exhibition-reset\Tools\Exhibition\Observe-OverlayRestart.ps1' -EvidenceRoot ('D:\J2M\evidence\overlay-v3-observer\' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'))
```

관찰 시작 이후 생성된 run 중 `helper-attempt.json`이 있는 run에 연결하고 helper/child handle을 확보하여 실제 exit 시각을 기록한다. 인계 전 중단된 run에는 연결하지 않는다. 연결한 run의 `terminal-failure.json`을 읽으면 submission-intent 이전이라도 실패 원문을 관찰 기록에 남기고 종료한다. submission-intent를 관측한 뒤 60초 동안 독립 시작이 없거나 child handle을 확보하지 못하면 관측 공백을 남기고 끝낸다. child handle을 확보하면 사용자 관찰을 재촉하지 않고 정상 종료를 기다린다. Ctrl+C 중단도 불완전 기록이다. 관찰자의 재읽기는 실행 문서를 복구하거나 런타임 권한을 연장하지 않는다.

Steam 원본 `gameprocess_log.txt`와 `console_log.txt`의 전후 복사본을 보존한다. 기록은 Steam session + AppID + ActionID로 연결하며 재시작 전후 ActionID 재사용, 로그 정밀도, helper의 OS 종료와 Steam 추적 제거 차이를 구분한다. 자동 분류 기본값은 U다. 원문 대조 전 W/R/F로 승격하지 않는다.

- W: helper가 추적 중인 상태에서 같은 Action이 기다리고, 추적 제거 후 추가 요청 없이 게임을 생성했다.
- R: helper 추적 제거 후 요청이 접수되어 게임을 생성했다. 실행 성공이지만 대기 요청 보존 증거는 아니다.
- F: Action이 실패했고 관찰 구간 내 새 게임이 시작하지 않았다.
- U: 접수나 사건 순서를 확인하지 못했다. 실행 성공 여부와 별도로 남긴다.

T4에서 새 Steam의 helper PID–AppID 재추적이 확인되지 않으면, 실행이 성공해도 핵심 조건 검증으로 계산하지 않는다. 기존 T3 실패를 재사용하며 T1–T3 반복은 구체적인 해석 공백이 있을 때만 추가한다. 한 번의 성공은 해당 환경의 성공 관찰이며 Valve의 요청 보존 보장으로 표현하지 않는다.

## 검증

현재 worktree에서 core, focused startup/wire/helper/platform/save/runtime fixtures, ui를 실행한다. focused filter 구분자는 `;`이며 실제 fixture/건수는 XML로 확인한다. Windows `ObservationV3.Runtime2.Tests.ps1`은 기존 공용 pipe 회귀와 runtime-3 실제 PS revision/컴파일 검사, 신규 harmless final executor/process fixture를 함께 실행한다. fake Steam/native는 실제 Steam 접수나 Overlay 증거가 아니다.

소스 hash와 검사 결과, 초반 실패/재시도 로그를 같은 evidence 묶음에 보존한다. full unfiltered baseline과 touched 결과를 혼합하지 않는다. 실제 설치본 T4와 Overlay 수동 결과는 자동 검사 이후 별도로 기록한다.
