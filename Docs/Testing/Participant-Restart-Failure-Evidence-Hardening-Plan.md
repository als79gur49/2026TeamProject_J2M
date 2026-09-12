# 참가자 재시작 실패 증거 보존 수정안

작성일: 2026-09-12
대상 장애: handoff `8abab0d2a0e2488cb63fc8e1b63c999f`, probe attempt 1 종료 코드 1

구현 상태: 1단계 완료. 비정상 종료 시 결과 파싱을 생략하고, 기존 증거를 덮어쓰지 않는 시도별 JSON·stdout·stderr 파일과 byte 수·SHA-256을 저장한다. 자동 재시도와 Steamworks 동작은 변경하지 않았다.

## 결론과 범위

이번 장애에서 확정된 최초 실패는 소유 probe 프로세스의 비정상 종료 코드 1이다. 그 뒤 stdout을 `ProbeObservation`으로 역직렬화하면서 발생한 `SerializationException`은 2차 오류다. 현재 `restart-failure.txt`는 예외 문자열을 제한된 길이로 저장하므로, 최초 실패의 stdout·stderr와 내부 예외가 잘려 정확한 원인을 복원할 수 없다.

첫 수정은 재시작·초기화 정책을 바꾸지 않고 **한 번의 다음 실패만으로 최초 원인을 판정할 수 있게 증거 저장 계약을 고치는 것**이다. 이 변경으로 확보한 실제 증거를 검토한 뒤에만 exit code 1 재시도나 native probe 수정 같은 동작 변경을 한다.

다음 사항은 이 단계의 범위에서 제외한다.

- 알 수 없는 exit code 1의 자동 재시도
- Steam 종료·재시작 횟수와 제한 시간 확대
- Ready journal을 다시 초기화하거나 reset request를 재소비하는 동작
- 추측에 근거한 Steamworks SDK 또는 환경 변수 수정

## 1. 실패 우선순위 고정

한 시도에서 여러 오류가 발생해도 최초 실행 실패를 대표 원인으로 유지한다.

| 우선순위 | 분류 | 예시 |
|---:|---|---|
| 1 | 프로세스 실행 | 생성 실패, timeout, 강제 종료, exit code 1 |
| 2 | 소유 프로세스 정리 | JobObject 종료·종료 확인 실패 |
| 3 | 출력 회수 | stdout/stderr reader 실패 또는 미완료 |
| 4 | 결과 프로토콜 | exit code 0이지만 빈 출력, 잘못된 JSON, 필수 필드 불일치 |
| 5 | 결과 정책 | 정상 wire 결과지만 AppID·SteamID·준비 조건 불일치 |
| 6 | 진단 저장 | attempt 파일 또는 최종 요약 저장 실패 |

- exit code가 0이 아니면 stdout JSON 역직렬화를 성공 판정에 사용하지 않는다. 파싱은 `SkippedBecauseProcessFailed`로 기록한다.
- exit code 0에서 출력 계약이 깨졌을 때만 결과 프로토콜 오류를 대표 원인으로 삼는다.
- cleanup, 출력 회수, 파싱, 기록 오류는 별도 필드에 누적하고 앞선 원인의 예외·HResult·stack을 덮어쓰지 않는다.
- 최종 예외 메시지는 전체 결과 JSON을 문자열로 중첩하지 않고 operation ID, attempt 번호, 대표 원인, 증거 경로만 포함한다.

## 2. 시도별 원본 증거 저장

handoff 폴더에 각 probe 시도마다 다음 파일을 만든다.

```text
probe-attempt-001.json
probe-attempt-001.stdout.txt
probe-attempt-001.stderr.txt
restart-failure.txt
```

`probe-attempt-NNN.json`에는 아래 정보를 기록한다.

- schema version, operation ID, attempt 번호, 단계와 UTC/단조 시각
- child PID와 StartTicks, 생성 성공 여부, exit 관측 여부와 exit code
- 대표 오류의 분류, 예외 타입, HResult, message, stack trace
- cleanup·출력 회수·파싱·정책·저장 오류의 독립 목록
- stdout/stderr의 원래 byte 수, 저장 byte 수, SHA-256, 잘림 여부와 파일명
- 파싱 수행 여부, wire schema, nonce/PID/StartTicks 검증 결과
- 시도 결과가 재관찰 가능, 준비 완료, 중단 중 어디에 해당하는지

stdout과 stderr는 현재 IPC 계약대로 UTF-8 텍스트 파일로 보존한다. 각 채널은 기존 16,384문자 메모리 상한을 유지하고 저장 byte 수와 SHA-256을 JSON에 기록한다. 상한을 넘어도 파이프는 끝까지 배출해 자식 프로세스가 block되지 않게 하며, 기존 overflow 실패 판정을 유지한다.

모든 canonical 파일은 같은 폴더의 임시 파일에 쓴 뒤 flush하고 원자적으로 교체한다. attempt 번호는 lock 안에서 할당하고 기존 파일을 덮어쓰지 않는다. 저장 자체가 실패하면 가능한 별도 fallback 파일과 Windows Event/host 출력에 짧은 오류를 남기되 대표 실패는 유지한다.

시도별 JSON이 구조화된 원본 진단 자료가 된다. `restart-failure.txt`는 팝업용 요약으로 유지한다. 고정 길이 텍스트가 유일한 진단 자료가 되지 않도록 `ProbeAttemptException`은 시도별 JSON 경로를 직접 표시한다.

## 3. 실행 및 저장 순서

`RunOwnedProbeCore`의 실패 경로를 다음 순서로 고정한다.

1. child 생성과 소유권 등록 결과를 기록한다.
2. exit 또는 deadline을 관찰하고 필요하면 소유 child를 정리한다.
3. 제한된 시간 안에 stdout/stderr를 함께 끝까지 회수한다.
4. 원본 stream 파일과 process/cleanup/output 상태를 attempt 파일에 저장한다.
5. exit code 0일 때만 stdout 결과를 파싱하고 identity와 준비 조건을 검증한다.
6. 파싱·정책 결과를 같은 attempt 파일에 원자적으로 갱신한다.
7. 실패 시 대표 원인을 보존한 `ProbeAttemptException`을 발생시킨다.
8. 최상위 host가 `restart-failure.json`과 짧은 사용자 메시지를 저장한다.

attempt 저장이 완료되기 전에 예외 문자열을 조립하거나 JSON 파싱을 수행하지 않는다. PowerShell host의 문자열 자르기는 팝업 본문에만 적용하고 구조화 파일에는 적용하지 않는다.

## 4. 변경 대상

| 파일 | 변경 목적 |
|---|---|
| `RestartExperimentWindows.cs` | raw stream 회수, attempt artifact 원자 저장, 실패 우선순위와 파싱 조건 적용 |
| `RestartExperiment.cs` | 대표 원인과 부가 오류를 분리한 결과/예외 계약 정의 |
| `Restart-Experiment.ps1` | 구조화 최종 요약 저장, 팝업은 증거 경로 중심으로 축약 |
| `RestartExperimentTests.cs` | process-backed 장애와 단위 경계 검증 |
| `Tools/Exhibition/Tests/Restart-Experiment.Tests.ps1` | host 저장·팝업·fallback 검증 |

실제 경로와 타입명은 구현 직전 현재 소스에서 다시 확인한다. 기존 Ready journal schema와 participant reset 완료 상태는 변경하지 않는다.

## 5. TDD 장애 사례

구현은 아래 실패 테스트를 먼저 추가하고, 각 사례가 현재 구현에서 왜 정보를 잃는지 확인한 뒤 통과시킨다.

1. child가 stdout 첫 글자 `s`, stderr에 native 예외를 쓰고 exit 1로 종료한다. 대표 원인은 exit 1이며 파싱은 생략되고 두 stream이 그대로 남아야 한다.
2. child가 exit 0과 잘못된 JSON을 반환한다. 이 경우에만 결과 프로토콜 오류가 대표 원인이어야 한다.
3. stdout과 stderr가 각각 상한을 넘는다. deadlock 없이 종료되고 byte 수, hash, 잘림 구간이 맞아야 한다.
4. process 실행 실패와 cleanup 실패가 함께 발생한다. 실행 실패가 대표 원인이고 cleanup 실패도 유실되지 않아야 한다.
5. 출력 reader 한쪽만 실패하거나 제한 안에 닫히지 않는다. 확보된 반대쪽 출력과 관측 부족 상태가 함께 남아야 한다.
6. attempt 저장이 실패한다. 원래 process 실패가 저장 오류로 교체되지 않고 fallback 위치가 보고되어야 한다.
7. 임시 파일 작성 도중 host를 중단한다. 다음 실행이 부분 canonical JSON을 정상 기록으로 읽지 않아야 한다.
8. 두 시도를 연속 또는 경합 실행한다. 서로 다른 번호를 사용하고 기존 증거를 덮어쓰지 않아야 한다.
9. 정상 probe 결과는 기존 준비 판정과 동일해야 하며 관측 코드가 Steam 업적·reset API를 호출하지 않아야 한다.
10. 실제 무해한 Windows child 프로세스로 stdout/stderr/exit 1을 발생시켜 fake 밖에서도 같은 결과가 저장되는지 확인한다.

경로·명령줄·환경값은 필요한 진단 항목만 저장한다. Steam auth ticket, 비밀번호, 전체 환경 block 같은 민감 정보가 artifact에 포함되지 않는지 별도 테스트한다.

## 6. 검증과 배포 기준

구현 revision에서 다음을 수행한다.

- restart focused EditMode 테스트
- Windows helper PowerShell 테스트
- `./run_tests.sh core`
- helper 소스와 Steam payload의 해시 일치 확인
- 구조화 artifact schema와 팝업의 한국어/영어 수동 확인

Steam 시험은 기존 Ready handoff를 사용해 reset을 다시 수행하지 않고 재시작 경로만 한 번 실행한다. 실패하면 같은 빌드를 반복 실행하기 전에 `probe-attempt-NNN.json`, stdout, stderr를 검토한다. 성공하면 Steam client 준비, 게임 재실행, request 소비와 journal 전이를 기존 절차로 확인한다.

다음 조건을 모두 만족해야 1단계를 완료한 것으로 본다.

- exit code 1 장애에서 child stderr와 stdout이 잘리지 않은 독립 자료로 남는다.
- `SerializationException`이 process exit 실패를 대표 원인으로 바꾸지 않는다.
- 팝업만으로도 operation ID와 정확한 증거 파일 위치를 찾을 수 있다.
- 관측 기능 추가가 reset, Steam 업적, 재시도 횟수와 deadline을 바꾸지 않는다.

## 7. 증거 확보 후 2단계 판정

새 자료로 원인이 확인되면 원인별로 별도 변경을 만든다.

- Steamworks native fatal이면 반환 전 호출 지점, SDK 초기화 환경과 ABI를 수정한다.
- helper runtime/assembly load 실패이면 배포 파일·runtime 선택·working directory를 수정한다.
- stdout 오염이고 프로세스 자체는 정상이라면 결과 전용 파일 또는 별도 IPC 계약으로 분리한다.
- 확인된 일시적 연결 상태이면 명시된 결과 코드만 기존 120초 deadline 안에서 재관찰한다.
- 알 수 없는 exit 1, native crash, identity 불일치는 자동 재시도하지 않는다.

2단계 수정은 확보된 attempt artifact를 회귀 fixture로 추가하고 별도 커밋으로 검증한다. 따라서 이번 수정안은 장애 원인을 추측해 숨기는 방안이 아니라, 다음 발생에서 원인을 확정하고 그 원인만 고칠 수 있게 만드는 방안이다.
