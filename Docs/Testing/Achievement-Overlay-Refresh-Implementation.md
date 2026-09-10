# GameOnly 업적 표시 비교 구현

> **진행 방향 및 실제 결과 갱신 — 2026-09-09:** [진행 방향 정정](Achievement-Overlay-Refresh-Direction-Correction.md)이 후속 검토의 기준이다. Build25209388의 실제 run `4956de62dc69455fbf670e627feb9c7c`은 SDK 5개 미획득·로컬 초기화·Ready 이후에도 사용자 Overlay 보고에 대상 획득 표시가 남았다. 이는 과거 현상의 재현이며 새 해결책의 검증이 아니다. 아래 미실행 표기는 각 구현/후보 준비 시점의 이력으로 읽고, GameOnly reset 시험을 다시 시작하는 지시로 사용하지 않는다.

2026-09-09. `Achievement-Overlay-Refresh-Next-Trial-Plan.md`의 제한된 후속 시험을 기존 reset trial에 구현한다. 이 문서는 구현·검증 상태이며 업로드, branch 변경, 설치, 실제 초기화 승인이 아니다.

## 실행 계약

`-j2mResetOverlayTrial <config-v2>`와 정확히 하나의 명시적 Steam provider로 조기 진입한다. 잘못된/중복/혼합/비지원 인자는 일반 startup으로 돌아가지 않는다. 이미 시작된 세션은 이전 이력을 보존하며 서비스와 추가 쓰기를 막고 종료만 제공한다. 정상 제품 startup과 독립 restart/FullCycle 기능은 유지한다.

새 trial의 시작 journal은 schema 1의 Ready여야 한다. Pending은 새 시험으로 재사용하거나 자동 복구하지 않는다. SDK의 현재 획득 상태는 실행 전까지 미확인이다. 획득 대상을 만들기 위한 SetAchievement는 없다.

| 역할·상태 | 사용자 동작 | 허용 효과 |
|---|---|---|
| Initiator 준비 | 종료 예약 | 신원·config·전체 payload·파일 집합 검증, reset 전용 5 getter와 evidence 저장 |
| Initiator 기준 보고 | 열기 시도 진술 및 SDK 획득 대상 각각의 획득/미획득/판정 불가 저장, 종료 | 역할당 단 한 번의 필수 보고 저장 |
| Initiator 범위 확인 | 별도 범위 확인 후 단회 요청 | 재검증 → durable claim → Pending → context/request → GameOnly helper |
| Worker 준비 | 종료 예약 | 소유 receipt/helper terminal·Pending·동일 client·payload·참가자 파일 검증 |
| Worker 초기화 | 시작 이후 종료는 예약 | 기존 coordinator 1회: Steam 5개 reset → 로컬 초기화 → Ready → 결과 검증/저장 |
| Worker 결과 보고 | 열기 시도 및 대상별 획득/미획득/판정 불가 저장, 종료 | 필수 보고 1회, 최신 파일 집합 확인, 종료 요청 |
| 실패/생성 불확실 | 종료 | 첫 오류와 가능한 종료 기록 보존; 재시도·서비스 재개 없음 |

baseline의 SDK 획득 대상 **모두**를 비교 집합으로 사용한다. 5 getter 중 실패가 있거나 전부 미획득이면 Pending 전에 종료한다. 비교 대상에 대한 사용자의 기준 화면 보고가 모두 획득이고 Overlay opened인 경우만 범위 확인과 초기화가 가능하다. 보고 저장 중 중복 입력과 초기화는 차단한다. 저장 도중 종료를 요청해도 완료된 보고는 보존한다.

초기화 범위는 `VQ_LEVEL_0_CLEAR`부터 `VQ_LEVEL_4_CLEAR`까지 5개와 기존 참가자 campaign/로컬 업적 데이터다. 제품 publication, campaign reconciliation, seed import와 일반 서비스는 양 역할에서 종료까지 보류한다. observation의 별도 무쓰기 latch와 v2 wire는 유지하며 getter/초기화 capability를 넣지 않는다. native Init 소유자는 하나이고 callback pump와 정상 Shutdown은 유지한다.

## 쓰기·취소 경계

공통 session lock은 trial에서 기존 파일을 열어 소유권만 얻는다. lock 파일이나 저장 루트가 없으면 생성하지 않고 중단한다. Evidence 디렉터리/기준 기록은 준비 중 최초로 생성된다. **참가자 저장소의 첫 쓰기는 claim 후 coordinator의 Pending commit**이다. 비동기 복귀와 비용 큰 검사 뒤, claim 뒤, Pending 직전, helper Process.Start 직전마다 취소/terminal/core 상태를 검사한다.

참가자 snapshot은 소유권으로 확인하는 root session lock을 제외한 모든 파일을 포함한다. Pending 후 파일 검증은 journal 변경만 허용한다. worker 전 파일 집합은 그 snapshot과 같아야 한다. 초기화 후에는 기존 초기화 대상 파일만 변경을 허용하며 로컬 결과는 복구나 정리를 하지 않는 `LoadReadOnly()`로 확인한다. 종료 직전은 역할의 가장 최근 snapshot과 비교한다.

claim은 CreateNew이며 부분 생성도 소비다. context를 만든 뒤 고정 hash를 request에 넣는다. helper도 실제 자식 생성 직전에 durable claim을 소비한다. 불확실한 생성, 실패한 생성, 소비한 claim은 다시 요청하지 않는다. helper 수락 이후 증거 저장 실패는 수락 사실을 취소 성공으로 바꾸지 않고 원본 종료를 진행한다. worker reset이 시작된 뒤 정상 닫기는 진행 중인 초기화와 필수 결과 보존을 마칠 때까지 기다린다. 강제 종료/크래시의 완결은 보장하지 않는다.

## 최소 wire 및 근거

Reset 전용 config/context/baseline/report/receipt는 version 2다. config는 Version/AppId/SteamId/PayloadManifestPath만 허용한다. context는 trial ID, 고정 handoff nonce, Ready 및 Pending operation ID, mapping, origin/client 전체 신원, config·payload·baseline·기준 보고·초기 및 Pending 파일 manifest의 경로/hash, 명시적 범위 확인 UTC를 연결한다.

baseline은 SDK source, 정확한 5개 이름, getter 성공/획득 배열, 계정·mapping·Ready operation·origin·UTC/monotonic을 저장한다. 화면 보고는 User source, 역할/process, 대상과 판정, 열기 시도/visibility, baseline hash, worker에서 reset result hash를 저장한다. SDK 관측은 원격 서버 상태의 독립 증명이 아니다.

request는 GameOnly/ResetWorker만 허용하고 context hash·nonce·계정·operation·origin/client·고정 도구/인계 경로를 검증한다. receipt는 실제 child PID/start ticks/scope/path/hash 및 request bytes hash와 context/operation을 연결한다. helper-created와 단 하나의 HelperCompleted terminal을 worker가 확인한다. old/mixed/partial reset 요청 및 FinalObserver/FullCycle은 거부한다. FinalObserver enum 값은 예전 serialized 값의 오해석을 막는 거부용 값으로만 남긴다. 제품 journal schema는 변경하지 않는다.

종료 기록의 `ActualExitConfirmed=false;SteamTrackingReleased=false`는 종료 요청의 한계를 명시한다. 보고 저장과 종료 요청만으로 자식 실제 종료나 Steam tracking 해제를 주장하지 않는다.

## 코드 연결

- `ExhibitionApplication.InspectProcessStartup/InspectResetStartup/Compose`: 조기 분기, 실패 시 격리, 공통 session lock/coordinator 재사용.
- `ResetOverlayTrial`: 두 역할의 상태·한 번의 보고·claim/취소/종료 예약. FullCycle 또는 서비스 재개 메서드 없음.
- `ResetOverlayTrialRuntime`: SDK baseline, evidence, live pins, 전체 payload/파일 검증, 기존 coordinator 결과 확인, GameOnly request 생성.
- `RestartExperimentWindows`: 기존 helper/lock/GameOnly 검증 재사용; reset v2 producer/helper/child 및 durable child claim.
- `ResetOverlayTrialPresentation`: 스크롤 가능한 대상별 실제 보고, 별도 초기화 범위 확인. Tab/방향키/Enter 및 기존 mouse controls. Shift+Tab은 가로채지 않음.
- Product/Steam/campaign composition latch와 coordinator의 선택적 guard overload: 일반 경로 동작을 유지하며 trial 자동 쓰기 및 위험 경계를 제어.
- `Build-RestartExperiment.py --reset-overlay-trial`, `Prepare-OverlayObservationCandidate.py --reset-overlay-trial`, inspector의 `-RequireResetTrial`: 고정 manifest, 임시 define 복원, compiled 진입/쓰기/보고/인계/FullCycle 차단, raw→검사→final 및 helper 일치 검사.

캡처/import/경로 입력, 별도 열기 시각, Overlay sampling/activation 추가 수집, frame/graphics/DLL 조사 기능, Steam 로그 collector, 보조 오류 채널, SDK 문서 체인, 인간 관찰 5분 제한은 없다. compiled metadata 검사는 후보의 구현 검증이며 게임 안의 DLL 조사 기능이 아니다.

## 검증 상태

실행 근거 및 최종 결과는 아래 완료 기록에 갱신한다. 무필터 full, interactive 작은 화면/mouse 검토, 실제 Steam 시험은 별도 상태다. batch skip은 시각 통과가 아니며 0건 선택도 통과 증거가 아니다.

구현 전 사용자 파일은 `/mnt/d/J2M/evidence/reset-overlay-gameonly-implementation-20260909T114556Z/before.tar.gz` 및 `before-files.json`으로 보존했다. 기존 미커밋 변경 위의 제한된 편집이며 HEAD 복원/reset/clean/branch 변경은 사용하지 않는다. Unity 생성 파일은 해당 백업의 원래 바이트로만 복원한다.

## 최초 구현 자동 검증 완료 기록 (P2 수정 전)

근거 루트: `/mnt/d/J2M/evidence/reset-overlay-gameonly-implementation-20260909T114556Z`.

| lane | 선택/통과/실패/skip | exit |
|---|---|---|
| 동일 D worktree `./run_tests.sh core` EditMode | 254 / 254 / 0 / 0 | 0 |
| core PlayMode | 111 / 107 / 0 / 4 | 0 |
| `./run_tests.sh ui` EditMode | 1359 / 1359 / 0 / 0 | 0 |
| 관련 영역 `full --filter` EditMode | 835 / 835 / 0 / 0 | 0 |
| 관련 영역 PlayMode | 4 / 2 / 0 / 2 | 0 |
| Windows helper fake suite | 89 / 89 / 0 / 0 | 0 |
| 후보 도구의 순수 파일·인자 검사 | 4 / 4 / 0 / 0 | 0 |

명령과 lane exit는 `final-lane-exits-v2.json`, fixture별 선택 건수와 skip 사유는 `validation-summary.json`, 고정 475개 소스는 `validated-sources.json`, helper/tool 결과는 `helper-and-tool-results.json`에 있다. 모든 필수 최종 lane은 0건보다 많은 시험을 선택했다. `CampaignSaveArchitectureV2Tests`와 실제 파일 저장소 fixture가 atomic save 경계를 검증한다. 정상 startup positive control은 `ResetOverlayStartupTests` 및 `ProductAchievementApplicationCompositionTests`에서 포함했다.

PlayMode에서 실제 패널의 키보드 보고→범위 확인과 준비 중 종료를 fake 효과로 확인했다. 그래픽 환경을 요구하는 observation/reset panel fixture 두 건은 batch에서 skip했다. 작은 화면 렌더링과 실제 mouse/keyboard 사용성의 interactive 검토는 **NotRun**이며 시각 통과로 해석하지 않는다. core의 기존 PlayMode skip 네 건도 별도 사유를 XML에 보존했다.

초기 반복의 컴파일 오류, NUnit/Unity synchronization 대기, fake counter 초기화 누락, 실제 atomic 읽기의 cleanup 부작용, 빈 backup 결과 및 Windows wildcard 판정을 교정했다. 초기 실패 결과는 최종 통과 근거와 구분해 보존했다. 마지막 공통 lock/전체 참가자 파일 검증 보강 이후 core/ui/focused를 고정 소스로 다시 실행했다.

무필터 full은 실행하지 않았고 project-wide/full-lane green을 주장하지 않는다. 과거 Gate B/C 및 Build25206354 observation run은 새 reset 시험의 검증 완료 근거로 사용하지 않았다. 현재 획득 비교 대상 여부는 SDK 미조회 상태다.

## 최초 로컬 후보와 분리된 완료 기준 (P2 수정 전)

로컬 후보: `/mnt/d/J2M/builds/reset-overlay-trial/20260909T124514Z-reset-gameonly/candidate/payload`.
후보 근거: `/mnt/d/J2M/evidence/reset-overlay-trial/20260909T124514Z-reset-gameonly`.
config: 위 후보 근거 디렉터리의 `config.json` (v2, 기존 config를 덮어쓰지 않음).

| 단계 | 상태와 근거 |
|---|---|
| 구현 | 제한된 두 역할, 단회 reset/GameOnly, 보고와 실패/취소 경계 구현 완료 |
| 자동 검증 | 고정 manifest의 core/ui/focused 및 helper/tool fake 통과. 위 표와 XML 참조 |
| 후보 빌드 | Windows non-development 빌드 성공, exit 0 |
| compiled 검사 | Cecil read-only 검사 `Verified=true`, `ResetTrialChecked=true`, player/native 실행 없음 |
| 후보 준비 | 257개 전체 payload와 raw→검사→final 바이너리 일치, helper 소스 일치, exit 0 |
| 설정/사용자 파일 | 임시 define 전후 SHA256 일치. 시작 전 생성 파일 원본 복원 및 사용자 변경 audit 별도 저장 |
| 업로드 | 미실행 |
| branch 변경 | 미실행. 기존 `feature/exhibition-reset` 및 HEAD 유지 |
| 설치 | 미실행. 현재 설치본과 새 후보는 다름 (`installedMatchesCandidate=false`) |
| interactive fake UI | 작은 화면 렌더링/실제 mouse 사용성 검토 미실행 |
| 실제 Steam 비교 시험 | 미실행. SDK 획득 대상 여부 미조회; 실제 초기화·업적 조회/변경 실행 없음 |

후보의 `candidate-verification.json`, `player-metadata-check.json`, `diagnostic-payload-manifest.json`, `build/define-transaction.json`이 각 주장을 연결한다. 준비 과정의 journal은 파일 읽기만 수행했고 SHA256 `65a499c4f8b1b4401e6520c3f80bf9ff991f8b0af2a6cd06cf241cab50994ce8`이 유지됐다. 이 파일의 Ready/계정 일치는 현재 SDK 획득 상태나 로그인 상태의 검증이 아니다.

실제 실행은 별도 범위다. 승인된 업로드/설치 뒤 동일 후보의 전체 설치 파일 집합을 확인하고, 기존 session lock과 Ready journal, 단일 Steam provider, fresh native/client/account/payload/file pins, 적어도 하나의 SDK 획득 대상 및 일치하는 사용자 기준 보고를 충족해야 한다. 없거나 일치하지 않으면 이번 경로가 Pending 전에 중단한다. 비교 대상 생성, 재획득/재삭제, 계정 교체, FullCycle 및 서비스 재개를 대안으로 자동 실행하지 않는다.

## 서브 에이전트 재검토 P2 수정

준비 중 마지막 파일 검증 이후 또는 worker claim 이후 Steam client가 교체되면, SDK Available/동일 계정만으로 coordinator guard가 통과할 수 있었다. `ResetOverlayTrialRuntime.ValidateRecord`가 고정 client 신원도 검증하도록 수정했다. 기존 `ResetOverlayTrial.PrepareCoreAsync` → `ExhibitionResetCoordinator.ResumeAsync(guard)` 연결을 그대로 사용하므로 reset 진입 전, 비동기 Steam reset 반환 후, 로컬 초기화 후 Ready 전의 guard에 적용된다. 일반 coordinator의 선택적 guard 계약, UI, wire, helper와 journal schema는 변경하지 않았다.

새 `ResetOverlayRuntimeAndWireTests.WorkerGuardRejectsClientReplacementWithSameAccountAndAvailableSdk`의 세 case는 준비 후 PID 변경, claim 후 start ticks 변경, 비동기 fake Steam reset 후 start ticks 변경을 재현한다. 앞의 두 경우 SDK reset 호출 0회, 마지막 경우 이미 수행한 5개 reset 이후 로컬 reset 0회·Pending 보존을 확인한다. 소비한 claim은 복원하지 않는다. client가 유지되는 기존 producer/helper/worker 성공 시험에도 실제 record guard를 연결했다. compiled inspector에는 `ValidateRecord → ValidateClient → Same` 호출 확인을 추가했다.

이번 수정 근거: `/mnt/d/J2M/evidence/reset-overlay-client-guard-20260909T130021Z`. 수정 전 새 시험은 3건 선택·3건 실패했으며, 현재 소스의 검증과 후보 결과는 아래에 기록한다. 위 최초 구현의 결과와 후보는 수정 전 이력이며 이번 수정의 검증 결과로 대체 사용하지 않는다.

| P2 수정 후 lane | 선택/통과/실패/skip | exit |
|---|---|---|
| focused full EditMode | 838 / 838 / 0 / 0 | 0 |
| focused full PlayMode | 4 / 2 / 0 / 2 | 0 |
| core EditMode | 254 / 254 / 0 / 0 | 0 |
| core PlayMode | 111 / 107 / 0 / 4 | 0 |

`lane-exits.json`에 정확한 명령/exit, `validation-summary.json`에 실제 fixture 선택 건수와 skip 사유, `validated-sources.json`에 475개 고정 소스를 저장했다. 새 재현 시험은 수정 전 0/3 통과에서 수정 후 3/3 통과로 바뀌었다. 서브 에이전트가 수정된 호출 연결과 시험을 읽기 전용으로 재검토했으며 새 blocker는 없었다 (`subagent-review.json`).

이번 수정은 UI/presentation과 helper/wire를 바꾸지 않아 별도 `ui` lane, Windows helper fake suite 및 순수 후보 도구 unit suite는 재실행하지 않았다. 최초 구현 때의 각 통과 기록은 이전 소스의 이력으로 남긴다. focused에는 기존 fake UI 경계 시험이 포함되지만 그래픽 fixture 두 건은 여전히 skip이며 interactive 작은 화면/mouse 검증은 NotRun이다. 무필터 full과 실제 Steam 시험도 NotRun이다. SDK 및 참가자 실제 초기화 호출은 수행하지 않았다.

### P2 수정 반영 로컬 후보

현재 후보: `/mnt/d/J2M/builds/reset-overlay-trial/20260909T131205Z-reset-client-guard/candidate/payload`.
후보 근거와 v2 config: `/mnt/d/J2M/evidence/reset-overlay-trial/20260909T131205Z-reset-client-guard`.

빌드 및 준비 exit 0, compiled `Verified=true`/`ResetTrialChecked=true`, 전체 payload 257개 일치와 검사한 raw 바이너리→final 일치를 확인했다. helper도 고정 소스와 일치한다. `candidate-exits.json`은 정확한 실행 명령과 exit를 저장한다. 이 후보가 P2 수정 전 후보를 대체하며 이전 산출물은 보존한다.

임시 define 전후 해시가 같고, Unity 생성 파일 3개는 이번 수정 전 백업의 원래 바이트로 복원했다. `user-change-preservation.json`에 수정 대상 4개 외 기존 파일 보존 및 최종 고정 소스 일치를 기록한다. HEAD/branch 변경·commit·업로드·설치·게임/Steam/native 실행은 하지 않았다. 참가자 journal은 파일로 읽기만 했으며 기존 해시가 유지됐다. 현재 설치본은 새 후보와 다르다. 현재 획득 대상 여부와 실제 실행 전 조건은 여전히 미검증이다.
