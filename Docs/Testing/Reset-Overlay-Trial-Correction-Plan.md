# Reset / Overlay 시험 P1·P2 수정 계획

2026-09-08. 대상: `/mnt/d/J2M/worktrees/exhibition-reset`.
상태: 수정 계획 작성. 이 문서로 코드 수정·검증·빌드·실제 시험이 완료된 것은 아니다.

2026-09-09 구현 후속: 서브 에이전트 수정과 독립 소스 검토를 수행했다. core, ui, focused 및 Windows helper fake 검증은 통과했다. 진단 패널의 batch 화면 캡처는 실패했으며 실제 렌더링·입력 검증은 미완료다. 이후 사용자의 업로드 지시로 새 후보 **BuildID 25190245**를 업로드했다. branch 활성화·설치·실제 시험은 수행하지 않았다. 계획 작성 당시 상태와 구분한 상세 결과는 [수정 검증 기록](/mnt/d/J2M/evidence/participant-restart-preflight/20260908T144501Z-trial-correction/implementation-status.md)과 [업로드 기록](/mnt/d/J2M/evidence/participant-restart-preflight/20260908T144501Z-trial-correction/upload-summary.md)을 참조한다.

BuildID **25189120**의 실제 초기화 시험은 보류한다. 업로드 완료와 시험 적합 판정은 별개다. 근거는 [업로드 후 독립 재검토](/mnt/d/J2M/evidence/participant-restart-preflight/20260908T134757Z-reset-overlay-integration/post-upload-review.md), 같은 폴더의 `subagent-flow-review.md`, `subagent-handoff-review.md`다. 현재 동작 설명은 [Reset-Overlay-Trial.md](Reset-Overlay-Trial.md)를 참조하며 구현 후 이 계획과 함께 갱신한다.

## 1. 목표와 유지 범위

최초 trial 실행이 기준 업적 상태를 바꾸지 않고, 실패 후 사용자가 시험 검증을 거치지 않는 복구로 유도되지 않으며, 화면의 조작이 실제 허용 동작과 일치하게 한다.

초기화 대상은 기존 5개 `VQ_LEVEL_0_CLEAR`…`VQ_LEVEL_4_CLEAR`와 로컬 참가자 데이터다. 게임 3개 흐름(최초 → GameOnly worker → FullCycle final observer), 중간 Overlay 수동 관찰, 동일 op/account 및 payload 검증, 단회 claim, 기존 probe/deadline/cleanup/lock 계약을 보존한다. 제품 journal schema와 일반 실행의 Pending 복구 정책을 바꾸지 않는다. 제품 CompletedReset, Editor lifecycle, Overlay 갱신 보장, 최초 native fatal 원인 규명은 범위 밖이다.

## 2. P1: 최초 startup에서 기준 상태 변경 차단

문제: Ready Initiator는 현재 startup 보류에서 제외돼 로컬 earned → Steam SetAchievement가 설정·계정 검증과 baseline 기록보다 먼저 실행될 수 있다. 버튼에서 StopPublication을 호출하는 것만으로 이미 발생한 쓰기를 취소할 수 없다.

### 시작 순서

1. opt-in 인자를 초기 composition에서 판별한다. 정상 Initiator뿐 아니라 worker/final, 잘못된·중복된 trial 인자도 trial로 식별되면 일반 startup으로 돌아가지 않는다.
2. trial로 식별한 시점에 **제품 업적 서비스 자동 시작, Steam 자동 게시, campaign production 접근을 모두 보류**한다. config/context 생성, journal 검사 등의 후속 실패에서도 보류를 유지한다. native Steam 초기화·callback pump와 읽기 전용 identity/업적 조회는 허용한다.
3. 실제 `AfterAssembliesLoaded` → `BeforeSceneLoad` 순서에서 보류가 publisher 등록보다 앞섰음을 검증한다. 같은 초기화 단계의 우연한 호출 순서에 의존하지 않는다. 이미 runtime이 시작돼 보류가 불가능하면 startup 계약 위반으로 중단하며 성공적으로 차단했다고 기록하지 않는다.
4. 새 프로세스의 trial에서 baseline 확인 전 `SetAchievement`, `ClearAchievement`, `StoreStats`, 참가자 데이터 reset, Pending 쓰기, helper 생성은 모두 0이어야 한다. 단순 getter가 아니라 실제 publisher/저장 호출 경계로 측정한다.

### 최초 화면 변경 — 명시적 설계 결정

현재 일반 초기화 버튼을 사용하려면 일반 메뉴의 save/hub 모듈 구성이 필요하다. 위 서비스들을 보류한 상태에서 기존 메뉴를 억지로 구성하거나 서비스를 다시 시작하지 않는다. **opt-in Initiator는 진단 전용 화면의 초기화 버튼을 사용한다.** 일반 실행의 메뉴·초기화 버튼은 유지한다. 이는 이번 수정의 사용자 조작 변경이며 문서/실행 안내에 반영한다. Ctrl+Shift+F10을 초기화에 연결하지 않는다.

- 최초 trial도 `BlocksMenu=true`로 메뉴 shell까지만 준비한다. save/hub/gameplay 진입은 차단한다.
- 일반 `CompleteMenuInitialization`에 의존하지 않는 trial 준비 상태를 둔다. 기존 `CanRequest`의 일반 menu-ready 의존성을 명시적인 trial 준비 완료로 바꾼다.
- 기존 단조 30초 startup 대기 seam을 이용해 Steam 가용성을 기다리고 Ready journal, config의 AppID/계정, prerequisites/Gate C, payload를 읽기 전용으로 검증한다. Initiator에는 child receipt를 요구하지 않는다.
- 유효 identity 아래 5개 업적을 조회·기록하고 화면에 기준 상태를 제공한다. 최소 1개가 이미 획득 상태여야 한다. 모두 미획득/조회 실패/기록 실패이면 중단하며 업적을 만들어 조건을 충족시키지 않는다.
- 사용자는 Overlay를 확인·기록하고 확인란을 선택한다. 화면은 5개 업적과 로컬 참가자 데이터의 초기화 범위를 명시한다. 확인 전 버튼은 사용할 수 없다.
- 버튼 실행 직전에 같은 config/context/계정/Ready/파일을 재검증하고 SDK 값을 다시 조회한다. 처음 기록한 baseline과 달라지면 추가 쓰기 없이 중단한다. 확인한 기준 상태와 실제 초기화 대상이 달라진 상태로 진행하지 않는다.
- 검증 통과 후에만 기존 단회 claim → Pending 저장 → context 고정 → GameOnly worker 한 번 요청을 수행한다. baseline 준비와 버튼 연타/중복 callback은 각각 한 번만 수용한다.
- 종료/취소를 선택하면 진행 중인 준비 continuation이 뒤늦게 버튼을 활성화하거나 다음 단계로 넘어가지 않도록 종료 상태를 고정한다.
- 시작 취소·실패 시 서비스를 다시 켜서 일반 메뉴로 보내지 않는다. 증거 안내와 게임 종료만 제공한다. 계정 상태를 원복하거나 Pending을 삭제하지 않는다.

30초는 단조 시계로 측정하는 startup 가용성 대기 예산이다. 동기 파일 해시·로그 I/O를 포함한 프로세스 전체의 종료 상한으로 주장하지 않는다. 기존 helper의 절대 deadline 전달 및 최종 Process.Start 직전 검사 계약과 구분한다.

worker는 초기화 후에도 모든 보류를 유지한다. final observer만 기존 Ready/identity/local-empty/SDK-cleared 검증 후 정상 서비스를 한 번 시작한다. 최종 메뉴 후 SDK 확인을 유지한다.

주요 파일: `ExhibitionApplication.cs`, `ResetOverlayTrial.cs`, `ResetOverlayTrialRuntime.cs`, `ResetOverlayTrialPresentation.cs`. startup 정책은 실제 composition이 호출하는 seam으로 만들고, 테스트에 정책의 복사본을 만들지 않는다.

## 3. P1: helper 실패 안내에서 일반 재실행 권고 제거

문제: Pending 저장 뒤 worker helper가 실패하면, 현재 “start the game manually” 안내를 따른 일반 실행은 제품 Pending 복구를 수행한다. 초기화 의도는 앞서 요청했지만 trial 검증/claim/관측 절차를 거치지 않는다.

- `Restart-Experiment.ps1`의 **모든 비-probe helper 실패**는 공통 중단 안내를 사용한다. 역할별 안내가 필요하다는 이유로 request 읽기/JSON 파싱/Add-Type 이전 실패를 빠뜨리지 않는다.
- 안내 의미: “시험이 중단됐습니다. 초기화가 일부 적용됐거나 Pending이 남아 있을 수 있습니다. 게임을 다시 실행하거나 cycle을 반복하지 말고 증거를 검토하세요.” 오류 요약과 확보한 evidence 경로를 함께 표시한다.
- request/역할/증거 경로를 확보하지 못하면 미확보로 표시한다. 초기화가 실행되지 않았다고 추정하지 않는다. 안내 생성은 helper C# 컴파일 성공에 의존하지 않는다.
- 기존 재시작 전용 시험도 실패 직후 수동 게임 시작을 일률적으로 권하지 않는다. 검토 후 수동 복구는 별도의 조율된 절차로 문서화한다. 정상 성공 경로와 프로세스 제어는 바꾸지 않는다.
- 공통 cycle의 cleanup → lock 안의 최종 기록 → lock 해제 → 외부 팝업 순서를 유지한다. 팝업 표시/기록 실패가 최초 오류를 덮거나 lock 해제를 막지 않는다.
- 메시지 수정이 일반 실행의 Pending 복구 자체를 금지하는 것은 아니다. **사용자가 trial 밖에서 수동 실행하면 제품 복구가 진행될 수 있다는 한계**를 명시한다. 이를 완전히 봉쇄하는 제품 journal 변경/새 전역 잠금은 이번 범위에 추가하지 않는다.

주요 파일: `Assets/_Features/Exhibition/Tools/Restart-Experiment.ps1`, `Tools/Exhibition/Tests/Restart-Experiment.Tests.ps1`, 운영 문서. 네 helper 소스/스크립트와 player는 같은 후보로 배포한다.

## 4. P2: trial 상태 화면의 소유권 명확화

- UI application에 선택적 presentation capability를 두어 trial이 자체 상태 화면을 소유함을 알린다. UI가 Exhibition 구체 타입에 의존하거나 오류 문자열로 trial을 판별하지 않게 한다.
- trial에서는 최초 준비/확인, worker 대기, final 준비, 실패에 대해 공통 participant 상태 팝업을 띄우지 않는다. 이미 열린 공통 상태 팝업은 닫되 programmatic close가 restart/quit callback을 실행하지 않아야 한다.
- trial presentation이 상태, 증거, 확인/계속/종료 조작을 제공한다. 실패에는 재시작·재시도 조작이 없다. 기존 `Restart()` no-op은 방어로 남길 수 있지만 UI 계약의 해결책으로 간주하지 않는다.
- 모든 trial 역할에서 기존 일반 초기화 버튼은 숨긴다. final 메뉴가 열린 뒤에도 초기화/재시도를 제공하지 않는다.
- ordinary participant flow에는 capability 기본 동작을 유지해 정상 recovery/restart 버튼의 표시·callback이 달라지지 않게 한다.
- 진단 화면과 uGUI 팝업의 중복, 마우스/키보드 입력, 작은 화면에서 버튼·증거 경로가 잘리는지 확인한다. no-op 호출 테스트만으로 UI 검증을 대체하지 않는다.

주요 파일: `MainMenuPorts.cs`, `MainMenuUiFlowInstaller.cs`, `ResetOverlayTrial.cs`, `ResetOverlayTrialPresentation.cs`, 해당 UI 테스트.

## 5. 회귀 테스트 수용 기준

| 영역 | 재현과 필수 결과 |
|---|---|
| 실제 startup 조합 | Ready + 로컬 earned/pending + Steam 미획득 fake로 production startup/publication 등록 경계를 연결. 버튼 전 Set/Clear/Store/reset/Pending/helper 모두 0, baseline 미획득이면 거부 |
| 실패 startup | 잘못된/중복 trial 인자, config 경로/계정/manifest 오류, journal/constructor/진단 실패, runtime 등록 전 실패에서도 후속 자동 게시 0 |
| baseline | 한 개/복수 획득, 전부 미획득, query 실패, 기록 실패, 정확한 startup deadline, 대기 중 종료, 확인 전 클릭, 중복 입력, 확인 뒤 identity/파일/업적값 변경을 검증. 사전 실패에서 Pending/helper 0 |
| 서비스 경계 | Initiator/worker에서 자동 서비스·게시·campaign reconciliation/seed import 0. final은 기존 cleared 검증 뒤 시작 1회. 일반 실행의 기존 동작 유지 |
| helper 안내 | request 없음/부분 JSON/잘못된 역할/Add-Type 실패/환경 검증 실패/worker 생성 불확실/FullCycle 실패에 재실행 권고 없음. 실제 host가 사용하는 formatter/표시 seam을 검증하고 C# 미컴파일 경로 포함 |
| failure UI | 실제 installer + trial port에서 준비/대기/오류 시 공통 retry 팝업 없음. 전용 패널에서 오류·증거·종료 표시, 실패 시 초기화/계속/재시작 callback 0. 공통 팝업 programmatic close 부작용 0 |
| 일반 UI 회귀 | 일반 Pending/error flow의 restart/quit 동작 유지, 기존 재시작 전용 hotkey 동작 유지 |
| 기존 trial | Steam reset 1회, GameOnly helper 1회, FullCycle 1회, final reset 0, 불확실 생성 및 claim 소비 후 재시도 0, nonce/PID/StartTicks/hash/lock 계약 유지 |

fake tests는 실제 startup/installer/helper가 사용하는 구현을 연결한다. `IResetOverlayTrialRuntime` 전체를 fake로 바꾼 상태기계 테스트만으로 첫 P1의 해소를 주장하지 않는다. 실제 Steam/native reset/업적 변경은 자동 테스트에서 실행하지 않는다. 정책 seam 추출이 필요하면 원래 production 경계를 통해 테스트하고 병렬 구현을 만들지 않는다.

대상 worktree에서 다음을 실행하고 새 증거를 D 아래 보존한다.

```bash
./run_tests.sh core
./run_tests.sh ui
./run_tests.sh full --filter 'ResetOverlayTrialTests;RestartExperimentTests;ParticipantResetDiagnosticsTests;ParticipantResetServiceTests;SteamExhibitionResetProtocolTests;ExhibitionResetCoordinatorTests;FileExhibitionResetJournalTests'
```

신규 startup/composition fixture가 위 필터 밖이면 해당 fixture의 focused lane을 추가한다. 기존 Windows helper fake tests를 실행한다. 실제 publisher/achievement composition 파일을 수정하면 해당 touched-cluster fixture도 추가한다. 필터 없는 full과 실제 Editor lifecycle을 실행하지 않았다면 별도로 명시한다. UI 안내/입력의 수동 검증은 계정 쓰기가 없는 fake 환경에서 우선 확인하고, 최종 player 화면은 조율된 시험에서 별도 기록한다.

## 6. 구현·검토·배포 순서

1. 현재 소스/사용자 변경과 증거를 보존하고 세 결함의 재현 테스트를 작성한다. 기존 구현에서 실패하는 근거를 확보한다.
2. startup 보류와 최초 진단 화면, 공통 helper 중단 안내, trial 상태 UI 소유권을 구현한다. 문서에서 “기존 버튼으로 시작”을 새 진단 패널 절차로 갱신한다.
3. 필요한 lane과 Windows fake를 통과시킨다. 서브 에이전트가 실제 startup 조합/최초 쓰기 시점, bootstrap 실패 안내, 실제 installer의 버튼 노출을 독립 재검토한다. 미해결 P1 및 시험 실패 판정을 방해하는 문제가 있으면 후보를 만들지 않는다.
4. 검증 소스를 고정하고 기존 builder로 새 후보를 만든다. 두 임시 define 복원, 실제 player의 opt-in 연결, 테스트/빌드 소스 일치, helper 네 파일, 비활성 예제, steam_appid.txt 부재, 전체 payload manifest/해시를 확인한다.
5. 새 BuildID로 업로드한다. Build25189120의 일부 파일 교체를 검증된 설치로 취급하지 않는다. 업로드, 시험 branch 활성화, 설치 및 전체 파일 일치를 각각 기록한다.
6. 설치 확인과 계정/초기화 범위 확인 뒤 한 번의 실제 시험을 조율한다. 이미 획득한 비교 대상이 없으면 중단한다. 최초 baseline → 초기화 → 중간 Overlay → FullCycle → final SDK/메뉴/Overlay → 정상 종료/Steam 추적 해제를 각각 기록한다.
7. 실패하면 해당 자료를 검토하고 중단한다. 동일 미수정 빌드 반복 실행, trial 밖 일반 실행을 통한 자동 복구 유도, 비교 데이터 생성을 위한 재획득을 요청하지 않는다.

## 이번 문서 작업의 검증 상태

소스와 재검토 근거를 대조해 계획을 작성했다. 코드 수정, 새 테스트 lane, 빌드·업로드·branch 변경, 실제 Steam/game/probe 실행, 업적/저장 초기화는 수행하지 않았다. 과거 core/UI/focused/Windows 통과는 이 계획의 구현 완료 증거가 아니다.
