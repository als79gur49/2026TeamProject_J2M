# Overlay 게임 교체 관찰 전용 진단 계획

> 아래는 구현 전 설계 기록이다. 구현 승인 이후 현재 연결·검증·운영 범위는 [구현 기록](./Overlay-Handoff-Observation.md)을 따른다.

2026-09-09. 대상 `/mnt/d/J2M/worktrees/exhibition-reset`. **설계 문서이며 신규 진단 코드·빌드·실제 시험은 아직 없다.** 기존 Build25190245의 reset trial이나 Ctrl+Shift+F10에 아래 계약이 구현돼 있다고 해석하지 않는다.

같은 날 독립 검토의 P2 네 건을 반영했다: [startup·인계 검토](/mnt/d/J2M/evidence/overlay-handoff-plan-review-20260909/safety.md), [관측·판정 검토](/mnt/d/J2M/evidence/overlay-handoff-plan-review-20260909/evidence.md). 실시간 관측 생산, 만료 뒤 인계 금지, 기존 GameOnly 시간 제한의 한계, 이미 시작된 runtime의 이력 구분을 아래 계약에 포함한다.

## 1. 질문과 근거

첫 질문은 “초기화 없이 GameOnly로 게임을 교체해도 Overlay 표시가 실패하는가?”다. 업적 표시 갱신의 성공 여부와 분리한다.

현재 시험 `20260908T161149470Z-428682c9ff2449d3a3c2e83ed3b9f0db`에서 최초 SDK `[true,false,false,false,false]`와 Overlay 획득 표시, worker SDK 5개 false·로컬 초기화·AwaitingOverlay를 확인했다. worker PID7848은 Steam에 추적됐고 Overlay 프로세스 PID8368도 연결됐지만, 열기 요청 뒤 Overlay 프레임 경고가 반복됐다. 후속 FullCycle은 실행하지 않았다. [조사 기록](/mnt/d/J2M/evidence/reset-overlay-trial/20260908T161149470Z-428682c9ff2449d3a3c2e83ed3b9f0db/overlay-unavailable-review/investigation.md).

GameOnly와 FullCycle은 모두 기존 `LaunchEnvironment.PrepareGame` → `Process.Start`로 동일 exe를 직접 생성한다. FullCycle은 먼저 Steam을 재시작하고 probe를 확인하지만, Steam에 AppID 실행을 맡기는 구조가 아니다. 이전 Gate C 성공은 Overlay 연결·표시·업적 갱신을 증명하지 않는다.

공식 문서는 Overlay의 graphics hook 및 `IsOverlayEnabled`를 설명한다. 이를 가설 근거로만 사용한다: [Overlay](https://partner.steamgames.com/doc/features/overlay), [IsOverlayEnabled](https://partner.steamgames.com/doc/api/ISteamUtils#IsOverlayEnabled). DLL 로드, Steam 추적, API 가용성, 실제 화면 표시는 별도 관측이다.

## 2. 첫 구현의 범위

첫 구현은 **GameOnly 한 번**만 지원한다. FullCycle 비교는 첫 결과 검토 후 별도 조율 시험으로 설계한다. 자동 연속 A/B 실행은 하지 않는다.

- 새 opt-in 이름 제안: `-j2mOverlayHandoffObservation "D:\J2M\evidence\overlay-handoff-observation.json"`. 아직 유효한 실행 안내가 아니다.
- 기존 reset trial/restart experiment 인자와 혼용을 거부한다. 인자 오류를 발견해도 일반 startup으로 돌아가지 않는다.
- Steam 사용자 시작 옵션에는 위 진단 인자만 넣는다. Steam 기본 실행 설정의 provider와 합친 **실효 provider가 정확히 하나**인지 확인한다. 직접 exe 실행은 비교 시험에 섞지 않는다.
- 설정 필드: `Version=1`, `AppId=5218360`, 명시한 `SteamId`, 현재 후보 `PayloadManifestPath`, 새 D evidence root. 첫 GameOnly에 불필요한 Gate B/C를 성공 조건으로 추가하지 않는다. 현재 Steam identity/생존/환경 정책은 기존 GameOnly 검증을 유지한다.
- 기존 Ready journal의 op/account/mapping/state 및 전체 설치 payload를 고정한다. Pending·읽기 실패·다른 계정이면 복구하지 않고 중단한다.
- 이미 초기화된 5개 업적이 모두 미획득이어도 진행할 수 있다. 최소 한 개 획득 조건을 가져오지 않는다. 업적 비교 데이터를 생성하지 않는다.

## 3. 쓰기 차단과 구성 경계

`ExhibitionApplication`의 AfterAssembliesLoaded 단계에서 관찰 인자를 판별하고, 파일/설정 구성보다 먼저 기존 제품 서비스·Steam 게시·campaign 접근 보류를 적용한다. 이후 Steam native Init/callback pump와 계정·업적 getter는 허용한다. provider 선택 실패/native 초기화의 확정 실패는 즉시 원인을 표시한다. 아직 준비 중인 상태만 하나의 단조 30초 예산에서 기다린다. 일반 timeout으로 확정 실패를 숨기지 않는다.

관찰 전용 composition은 `ExhibitionResetCoordinator`, `ParticipantResetService`, `SteamExhibitionResetAdapter.ResetAsync`, 로컬 reset adapter를 생성·호출하는 기존 reset 분기로 들어가지 않는다. 읽기 전용 journal/ledger seam과 관찰 runtime만 갖는다. 부모와 자식 모두 정상 제품 서비스 시작/reconciliation을 허용하지 않고 메뉴 shell과 진단 패널만 유지한다.

진입 시 제품 서비스나 native runtime이 이미 시작돼 조기 보류 계약을 확보할 수 없으면 즉시 `StartupAlreadyStarted` 부적격 상태로 끝낸다. 진입 전 게시·저장 이력은 보존하고, 진입 이후 추가 쓰기와 helper 생성이 없음을 별도로 검증한다. 과거 쓰기를 취소하거나 0회였다고 주장하지 않으며 이 세션은 관찰 비교 표본으로 수용하지 않는다.

금지 호출은 `SetAchievement`, `ClearAchievement`, `StoreStats`, `ResetAllStats`, 참가자 데이터 삭제/seed import, Pending/Ready 저장, reset Resume다. native Init/RunCallbacks/정상 종료 시 Shutdown과 getter는 쓰기 금지의 대상이 아니다. Unity/Steam 자체 로그·shader cache까지 파일 무변경이라고 주장하지 않는다.

관찰 증거는 D에 새로 쓴다. 참가자 save root의 journal·campaign·업적 JSON/backup 파일 집합과 SHA256을 준비 전/교체 전/자식 준비 후/정상 종료 전 비교한다. 해시 불변은 해당 관측 사이 최종 바이트 일치의 증거이며 “중간에 어떤 쓰기도 없었다”의 단독 증거가 아니다. API/저장 호출 차단 테스트와 함께 판단한다. 예상하지 못한 변경이면 중단하며 원복하거나 journal을 삭제하지 않는다.

기존 session lock 파일의 생성·해제는 위 참가자 JSON/backup 불변 계약과 구분해 유지한다. save root의 모든 파일 쓰기가 0이라고 표현하지 않는다.

## 4. 역할·인계·lock

역할은 `OriginObserver`와 `ReplacementObserver`로 분리한다. `ResetWorker`를 재사용하거나 reset context의 값을 조작해 관찰 역할을 흉내 내지 않는다.

새 관찰 context/receipt에는 wire version, runId, 역할, expected account, 최초 Ready identity, config/manifest 해시, 원본 process identity를 둔다. helper request에는 reset와 관찰 용도가 동시에 들어갈 수 없도록 배타 검증한다. 이전/부분/mixed wire는 거부한다. 제품 reset journal schema는 변경하지 않는다.

기존 cycle·launch 구현과 공통 cycle lock을 재사용한다. `GameOnlyGame` 환경 정책, 부모 종료 확인, 중복 게임 검사, 파일·client 검증을 그대로 거친다. **첫 비교에서는 GameOnly의 기존 시간 제한을 변경하지 않는다.** 현재 `Cycle.Execute`는 부모 종료 대기에만 30초를 부여하고, 이후 `StartGame(null)`을 호출한다. 따라서 부모 종료 후 환경·해시·client 검사와 실제 Process.Start 요청에는 별도 deadline이 없다. FullCycle의 120초 준비 deadline이 GameOnly에도 적용된다고 주장하지 않는다. 별도 복제 launch 구현을 만들지 않으며, 필요한 관찰 역할 전달만 명시적으로 확장한다.

원본의 단회 `handoff.claimed.json` → GameOnly helper 한 번 → 원본 정상 종료 → 자식 한 번 순서다. 생성 불확실/claim 소비/인계 오류 뒤 retry하지 않는다. receipt는 nonce/runId/role/PID/StartTicks/exe/account를 검증한다. receipt 존재만으로 준비나 성공을 선언하지 않는다.

helper 생성 요청이 수락된 뒤 원본 종료는 이미 위임한 교체를 진행시키는 동작이다. 이 시점의 종료를 “helper까지 취소”로 표시하지 않는다. 생성 전 취소/만료 차단과 수락 후 단회 진행·오류 수집을 분리한다.

helper는 cleanup → lock 안 terminal 기록 → lock 해제 순서를 유지한다. 사람이 Overlay를 관찰하는 동안 cycle lock을 연장하지 않는다. 관찰 프로세스의 기존 제품 session lock은 각 프로세스의 수명에 따른다. 종료가 확인되지 않은 이전 세션을 새 진단의 자동 종료 대상으로 삼지 않는다.

## 5. 사용자 진행과 시간 예산

| 단계 | 조작 | 필수 기록/중단 조건 |
|---|---|---|
| 준비 | 새 후보의 branch/전체 설치와 config를 확인 후 Steam에서 최초 실행 | provider 1개, SDK identity, Ready, payload, publication 보류. 실패 시 실행/복구 없이 종료 |
| 원본 관찰 | 준비 표시 뒤 Shift+Tab을 한 번 눌러 Overlay를 확인하고 닫음 | 열림/미열림/판정 불가를 명시. 화면과 activation 관측을 별도 저장 |
| 교체 | 원본 Overlay가 실제 열렸고 증거 기록이 완료됐으며 관찰 예산이 남았을 때만 “게임만 교체” 한 번 | 인계 직전 재검증, Steam 종료 요청 0, helper 1, 자식 생성 1 |
| 자식 관찰 | 소유 인계와 읽기 전용 준비 후 Shift+Tab 한 번 | opened / not-visible / inconclusive 중 사용자 관측 저장. 실패 관측을 성공 확인란으로 입력하지 않음 |
| 종료 | 결과를 저장하고 진단 종료 | 저장 해시/최종 SDK 조회, helper terminal, 자식 정상 종료와 Steam tracking 해제를 별도로 검토 |

SDK/receipt 대기는 한 단계당 기존 단조 30초, 비용 검사 후 deadline 확인을 유지한다. Overlay readiness 관찰은 native 준비 후 별도 단조 30초 창이며 `IsOverlayEnabled` 변화와 UI 요청 시각을 남긴다. API false만으로 시각 실패를 단정하지 않는다. 준비 창 종료를 초기화/재실행/FullCycle 트리거로 쓰지 않는다.

인간의 화면 확인은 helper deadline과 별개다. 역할별 native 준비 시점에 절대 관찰 deadline을 단조 시계의 현재값 + 5분으로 한 번 정한다. 위 Overlay 30초 창도 이 5분 안에 포함한다. 관찰 deadline 이상이면 정확한 경계도 `ObservationExpired` 종료 상태로 고정하고 능동 sampling과 신규 인계를 금지한다. 증거 표시·내보내기·게임 종료만 유지한다. 늦은 확인은 `LateObservation`/판정 불가로 따로 보존할 수 있지만 만료를 성공으로 바꾸거나 예산·교체 버튼을 재시작하지 않는다. SDK/graphics를 자동 종료하거나 관측 성공을 추정하지 않는다. 사용자 종료는 준비 중에도 작동하며 늦은 async continuation으로 다음 단계에 진입하지 않는다.

원본 인계 직전에는 live 계정, 최초 Ready op/state/mapping, 참가자 파일 집합/해시, config/payload, 동일 Steam client와 필수 증거를 다시 검증한다. 비용 검사 반환 후, 단회 claim 기록 후, **helper Process.Start 요청 직전**에 같은 5분 절대 deadline과 종료 상태를 확인한다. 검사 중 만료되면 helper 생성 0으로 끝내며 이미 소비한 claim은 유지한다. 이 제한은 원본의 helper 요청 경계까지만 적용된다. 수락 후 helper 내부의 부모 대기 30초 및 그 이후 별도 생성 deadline 부재와 혼동하지 않는다. OS 프로세스 생성 반환이나 동기 I/O 전체의 상한은 보장하지 않는다. 종료를 방해하는 무기한 출력/로그 회수 Wait를 두지 않는다.

## 6. 반드시 수집할 진단

`D:\J2M\evidence\overlay-handoff-observation\<UTC>-<runId>` 아래 저장한다.

- 단계 이벤트: UTC와 프로세스별 단조 elapsed, runId/role/PID/StartTicks, client identity, 실제 실행 인자의 허용 항목·중복 개수. 계정/연결 값과 전체 환경을 덤프하지 않는다.
- `NativeAvailable`, callback pump 수, `IsOverlayEnabled`의 unknown/false/true/조회 오류, 실제 조회 시작/반환 단조 시각·sequence·freshness, `GameOverlayActivated_t` 활성/비활성 횟수 및 **callback 수신 시각**. callback 수신 시각은 사용자의 물리 키 입력 시각과 구분하고, callback만으로 보이는 화면을 증명하지 않는다.
- 최초 Update/첫 렌더 관측, 이후 frame count, focused/pause, 실제 graphics API·해상도·window mode. 첫 managed frame 기록은 OS의 device 생성/Overlay hook 시각을 뜻하지 않는다. Update와 카메라 렌더 완료, 화면 present 확인을 구분한다.
- 5개 업적 getter 결과와 save manifest, 계정 일치. 조회 실패는 false로 대체하지 않는다.
- 자식이 살아 있을 때 로드된 `gameoverlayrenderer64.dll`의 유무·경로·관측 시각. 모듈 열람 실패는 unknown. DLL 존재를 hook 성공으로 판정하지 않는다. 인젝션·디버거 연결은 하지 않는다.
- Steam gameprocess/UI/renderer 로그 사본. 원본 관찰, 교체 직전, 자식 준비, 열기 관측, 종료 직전의 checkpoint에서 확보한다. renderer 로그의 PID가 대상 게임과 다르면 별도 파일로 남기고 유효 worker 증거로 채택하지 않는다.

수집은 유한하게 한다. 정기 API/frame snapshot은 초당 최대 1회, 역할별 총 300회 이내, 문자열 개별 2,048자 이내다. callback 사건은 snapshot과 분리해 수신 시각을 즉시 붙이고 역할별 최대 512건을 보존한다. 초과 시 누락 수와 `ActivationEvidenceIncomplete`를 기록하고 신규 인계를 거부하며, 앞의 사건을 덮거나 오래된 값을 최신 관측으로 표시하지 않는다. Steam 로그는 gameprocess/UI/renderer 세 파일의 고정 allowlist에서 checkpoint당 파일별 tail 최대 2MiB·역할별 최대 5 checkpoint, read 시 공유 허용 및 실제 bytes/mtime/잘림/실패를 기록한다. 로그 회수는 UI thread를 막지 않는 단일 비중첩 작업으로 수행하고 checkpoint당 추가 대기 최대 1초; 취소 불가능한 읽기가 남으면 신규 작업을 만들지 않고 skipped checkpoint와 불완전 관측을 기록한다. 게임 종료를 위해 무기한 기다리지 않는다.

선택적 Steam 로그 사본 실패만으로 SDK 준비 자체를 실패 처리하지 않되 hook 원인 확정은 금지한다. 필수 run/identity/쓰기 차단/인계/사용자 결과 기록 실패는 교체를 허용하지 않는다. 최초 오류와 수집·종료 오류를 따로 보존한다.

기존 platform runtime에 **관찰 전용 유한 구독**을 추가한다. 현재 Diagnostics getter의 캐시만 반복 조회하는 구현은 허용하지 않는다. 구독 중 runtime의 기존 native API getter를 위 주기로 실제 호출하며, `smokeRequested`와 “한 번 true면 관찰 중단” 조건에 의존하지 않는다. false→true→false 및 조회 오류→복구를 모두 기록하고 이전 값은 마지막 성공 시각과 stale 표시로만 제공한다. activation callback은 두 snapshot 사이에 켜짐/꺼짐이 모두 발생해도 두 사건을 보존한다. native 조회는 runtime 소유 thread에서 수행하며 background 로그 수집기가 Unity/Steam API를 호출하지 않는다.

구독은 역할별 하나만 허용하고 만료·취소·오류·handoff 수락·종료 시 해제해 추가 진단 조회가 발생하지 않도록 한다. 이미 진행 중인 동기 호출은 강제 중단했다고 주장하지 않으며 늦은 결과를 새 준비 증거로 수용하지 않는다. 구독 해제가 정상 Steam callback pump를 중단하지 않는다. 새 Steam runtime이나 두 번째 native Init, achievement smoke를 켜지 않는다. screenshot 캡처는 사용자의 실제 표시 증거이며 자동 성공 판정의 대체물이 아니다.

## 7. 판정과 후속 분기

| 관측 | 판정/후속 |
|---|---|
| 원본 Overlay부터 열리지 않음 | 교체 비교 조건 미충족. helper 생성 0, 현재 세션의 표시 문제 조사 |
| 원본 열림, 초기화 없는 자식 미표시 | 게임 교체만으로 재현. 초기화는 이 실패 재현의 필요조건이 아님. 환경·hook 시점 비교 대상으로 좁힘 |
| 원본/자식 모두 열림 | 이 조건에서 재현 안 됨. 이전 실패를 해결했다고 결론 내리지 않음. reset/타이밍/동시 Overlay 차이는 후속 가설 |
| API enabled/activation 있으나 화면 없음 | 활성 관측과 표시 불일치. render 경로 우선 조사, 입력 미수신으로 단정하지 않음 |
| API false 또는 모듈 없음/unknown | 해당 관측 범위에서 준비/주입 증거 부족. 정확한 실패 원인 자동 판정 금지 |
| 업적값/save가 변경되거나 인계 불일치 | 진단 격리 실패. 결과를 비교 근거로 수용하지 않고 중단 |
| 만료/취소/필수 관측 불완전 | 신규 인계 0. 늦은 보고는 별도 판정 불가 기록, 정상 비교 표본으로 수용하지 않음 |
| 진단 진입 전 runtime/제품 서비스 이미 시작 | StartupAlreadyStarted 부적격. 이전 쓰기 이력 보존, 사후 무쓰기 보장으로 재분류하지 않음 |

다음 FullCycle 비교는 동일한 관찰 전용 자식 구성과 동일 계정/graphics 조건으로 별도 요청한다. GameOnly와 환경·Steam 수명이 함께 바뀌므로 차이가 나도 단일 원인으로 단정하지 않는다. 필요할 때 환경 정책, graphics 선택, 다른 Overlay 활성 상태를 **한 번에 하나씩** 바꾸는 후속 시험을 제안한다. 이번 구현에서 기본 환경 정책이나 DX 설정, Discord/NVIDIA 설정을 조용히 바꾸지 않는다.

이 진단은 이미 미획득 상태에서 Overlay가 열리는지를 본다. “초기화 전 획득 표시가 Steam 재시작 후 미획득으로 바뀌는가”라는 원래 갱신 검증은 별도 미완료로 남는다. 그 비교를 위해 자동 재획득/재삭제를 넣지 않는다.

## 8. 구현 파일과 수용 테스트

예상 신규 파일: `OverlayHandoffObservation.cs`, `OverlayHandoffObservationRuntime.cs`, `OverlayHandoffObservationPresentation.cs` 및 해당 tests/meta. 실제 이름은 구현 시 확정한다.

수정 대상: `ExhibitionApplication.cs`의 조기 판별/별도 composition, platform read-only 진단 접근 seam, `RestartExperimentWindows.cs`의 관찰 역할/receipt/argument 검증, 필요 시 PS wire host, 기존 builder의 source manifest. 기존 UI status ownership capability를 사용하고 공통 retry 팝업과 일반 메뉴 진입을 노출하지 않는다. four helper 파일이 늘어나는 방식보다 기존 helper로 관찰 역할을 전달하는 방식을 우선한다.

필수 테스트:

1. 실제 startup/publication 및 저장 호출 경계에 fake를 연결해 정상 조기 opt-in 원본/자식/설정 실패/중복 인자에서 금지 쓰기 0. 이미 시작된 runtime은 진입 전 호출 이력을 가진 control을 두고 부적격 실패·진입 이후 추가 쓰기/교체 0 및 정상 표본 수용 금지를 검증한다. 정상 일반 startup positive control 유지.
2. Pending 거부, Ready 불변, 계정 변경, 5개 모두 미획득 수용, payload 추가/누락/변경 거부. 기존 reset trial의 획득 baseline 조건은 유지.
3. 실제 launch seam에서 GameOnly 환경·인자·생성 1, Steam 종료 0, claim 소비 뒤 retry 0. mixed/old/partial wire, 잘못된 역할/nonce/PID/start/hash 거부.
4. SDK 확정 실패 즉시 표기, SDK/receipt 30초와 관찰 5분의 정확한 deadline, 느린 재검증/claim 기록 뒤 만료 시 helper 0, 늦은 확인/continuation 차단을 검증한다. 기존 GameOnly 부모 종료 30초와 `StartGame(null)` 동작은 별도 characterization으로 보존한다. helper 수락 뒤 종료를 취소 성공으로 표시하지 않는다.
5. 실제 관측 생산 seam에서 smoke 비활성의 false→true→false, 조회 오류→복구, 두 snapshot 사이 반대 callback 두 개, sequence/조회 시각/freshness, 구독 해제 후 추가 조회 0, 진행 중 결과의 늦은 반환을 검증한다. API true지만 사용자 not-visible, activation 없이 opened 보고, 사건 overflow, log PID 불일치/overwrite/truncation/수집 예외·미종료·checkpoint skip을 각각 보존하며 조용히 성공/실패로 단일화하지 않는다.
6. 실제 패널/installer 조합, 버튼 한 번, 작은 화면/스크롤/마우스/키보드/종료, 상태 팝업 중복 없음. graphics 없는 batch skip은 시각 검증 통과가 아님.

검증은 target worktree에서 `./run_tests.sh core`, `./run_tests.sh ui`, 기존 reset/restart/startup 회귀와 신규 관찰 fixture의 focused full, Windows helper fake suite를 수행한다. 자동 테스트는 실제 Steam·native·계정 저장을 호출하지 않는다. 필터 없는 full과 실제 Editor lifecycle 미실행은 별도로 기록한다.

그 뒤 diff 재검토, 동일 소스 builder, 임시 define 복원, 관찰 경로의 compiled 연결과 raw→검사→최종 DLL/EXE·helper·전체 payload 일치를 확인한다. 새 config를 먼저 실제 경로에 생성하고 현재 Ready와 설치 manifest에 대조한 후 정확한 Steam 시작 옵션을 안내한다. 빌드/업로드/branch/설치/실제 실행은 각각 별도 상태로 기록한다.

## 이번 작업 상태

기존 소스와 보존 증거를 대조해 계획만 작성했다. 신규 코드, 테스트 lane, 후보 생성·업로드, Steam/game 실행, 업적·저장 변경은 수행하지 않았다. 원래 reset trial은 중간 Overlay 미표시로 중단 상태를 유지한다.
