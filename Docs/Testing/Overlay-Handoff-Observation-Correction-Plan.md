# Overlay 관찰 진단 수정안 — 필수 기능만 유지

2026-09-09. 대상 `/mnt/d/J2M/worktrees/exhibition-reset`. **설계 수정안이며 새 코드·테스트 실행·빌드·업로드·설치·실제 시험은 없다.** 현재 설치 Build25203840은 기존 캡처 필수 흐름이다. 아래 간소화된 UI를 현재 설치본의 실행 안내로 사용하지 않는다.

이 문서는 앞선 “보조 자료를 선택 기능으로 유지” 수정안을 대체한다. 보조 자료 생산자뿐 아니라 그 때문에 추가한 상태·비동기 수명·오류 채널·SDK 문서 체인을 폐기한다. 이전 문서는 [보존 사본](/mnt/d/J2M/evidence/overlay-minimal-plan-review/20260909/superseded-optional-plan.md)에만 남기며 구현 요구로 합치지 않는다.

[최초 설계](./Overlay-Handoff-Observation-Plan.md)와 [현재 구현 기록](./Overlay-Handoff-Observation.md)은 기존 동작의 기록이다. 그 문서의 보조 수집 요구를 이 최소 수정안에 다시 적용하지 않는다.

## 1. 하나의 질문과 최소 사용자 흐름

질문: **초기화 없이 기존 GameOnly로 교체했을 때, 원본에서 보였던 Overlay가 자식에서도 보이는가?**

사용자는 Steam에서 원본 실행 → Shift+Tab으로 열기 시도 → 보임/안 보임/판정 불가 보고 → 원본이 보였으면 게임만 교체 한 번 → 자식에서 열기 시도·결과 보고 → 종료만 수행한다. 별도 시각 기록 버튼, 캡처, 파일 찾기·이동·경로 입력은 없다. 앱이 필수 신원·격리·인계 검증과 최소 결과 기록을 맡는다.

표시 결과는 사용자 보고다. 보고 버튼 문구에 “열어 봤고 보임”, “열어 봤지만 안 보임”, “확인하지 못함/판정 불가”를 사용한다. 별도 필수 체크박스를 추가하지 않는다. API값이나 파일 존재로 보고를 대신 생성하지 않는다.

## 2. 남길 필수 계약

|대상|남길 계약|
|---|---|
|진입|조기 opt-in, 혼합/중복/잘못된 인자의 일반 startup fallback 금지. provider 하나와 기본 native 준비|
|쓰기 차단|제품 서비스/publication/campaign/reset/seed/Resume 보류. native Init 소유자 하나, 정상 callback pump·Shutdown 유지|
|이미 시작된 세션|StartupAlreadyStarted 부적격, 진입 전 이력 보존. 이후 추가 쓰기/인계 차단|
|대상·조건|AppID/live SteamId/login validity, Ready op/account/mapping/state, config·전체 payload, 동일 Steam client. Pending·필수 읽기 실패는 복구하지 않고 중단|
|참가자 파일|준비 전/교체 직전/자식 준비 후/종료 전 JSON·backup 파일 집합/해시 확인. 변경·검증 불능이면 중단. session lock/log/cache는 별개|
|교체|기존 GameOnly 환경·cycle·launch·공통 lock, 부모 종료·중복 게임 확인, claim/helper/자식 단회, nonce/run/role/PID/StartTicks/exe/account 연결|
|결과|원본 opened 사용자 보고의 필수 저장 완료가 교체 전제. 자식 결과는 사용자 보고 그대로 보존. 핵심 기록 불능이면 신규 교체 중단|
|종료·취소|모든 비동기 반환·비용 검사 뒤·claim 뒤·helper 요청 직전 취소/terminal 검사. 수락 뒤 원본 종료는 위임된 교체 진행. 생성 불확실 뒤 재시도 금지|

Ready와 참가자 파일 검증은 현재 제품의 안전한 격리 조건이다. 이를 Overlay 일반 법칙이라고 설명하지 않는다. 금지 호출 차단과 파일 해시를 함께 검증하되, 해시 일치만으로 중간 모든 쓰기가 0이었다고 주장하지 않는다.

## 3. 완전히 제거할 기능과 연쇄 의존성

|삭제 기능|함께 삭제할 것|
|---|---|
|PNG/JPEG/영상 첨부|importer·DTO·입력 예외·복사·해시·형식/크기/횟수 제한·클립보드·경로 입력·선택 펼치기 UI·수용 상태·generation/late completion·테스트|
|별도 열기 요청 시각 버튼|openRequestReported gate, 물리 키와 의도 시각을 정렬하는 절차|
|관찰 전용 IsOverlayEnabled sampling|SubscribeOverlay·Sample DTO·snapshot·sequence/freshness/stale·조회 오류/복구·구독 해제 수명·패널 API 표시|
|관찰 전용 activation 기록|추가 callback 구독/forwarder·512건 buffer·overflow/dropped count·완전성 오류|
|frame/graphics 조사|Update/frame 통계·카메라/SRP 구독·EndOfFrame coroutine·focus/pause·graphics API/해상도/window mode 수집|
|DLL 조사|module 열람·존재/경로/unknown·관련 사건|
|Steam 로그 사본|collector·allowlist/tail/PID 검증·checkpoint·별도 task/timeout/skip/late 처리·관련 사건과 테스트|
|관찰용 업적 getter 5개|ReadSdk/ReadObservationAchievement·bool 또는 nullable baseline·업적 이름/값 비교·unknown 처리·FirstKnown/EverIncomplete·RemoteAchievementCheck|
|앞선 SDK v2 설계|SDK prepared/handoff/final 문서·known source reference·문서 체인·Incomplete 누적·교차 역할 값 변화 테스트|
|선택 자료 오류 모델|AuxiliaryEvidence/채널 상태·경고 합성·선택 자료 실패 분류·선택 작업 상태|
|자료 수집 예산|readiness30초·1Hz/300회/512건·tail2MiB/5checkpoint/추가1초·이미지32MiB/3회 등|

위 항목은 UI에서 숨기거나 빈 구현으로 남기는 것이 아니라 관찰 composition과 producer/consumer에서 제거한다. `CheckpointAsync`, `Subscribe`, `StopSampling`, `ImportScreenEvidenceAsync`, `AcceptScreenEvidence` 등 불필요한 인터페이스도 제거한다. 삭제된 기능을 위한 성공/오류 테스트와 compiled 검사 요구도 삭제한다.

기존 제품 및 별도 smoke/reset/restart 기능은 이번 관찰 모드가 추가한 생산자와 구분한다. 공통 native의 정상 callback 처리나 다른 기능까지 이름이 비슷하다는 이유로 지우지 않는다.

## 4. 시간 제한도 최소화

**인간 관찰 5분 제한을 제거한다.** 보조 수집기가 없어 그 수명을 제한하기 위한 사람의 입력 deadline도 없앤다. Expires/ObservationExpired/LateObservation/남은시간 UI/정확한5분 경계 테스트/관찰용 native-ready 시각 저장을 제거한다. 새 인간 관찰 제한이나 수집 예산을 대신 추가하지 않는다.

SDK 기본 준비 대기30초, child receipt 대기30초와 기존 GameOnly 부모 종료 대기30초는 유지한다. 준비 중 사용자 종료도 작동해야 하며 늦은 continuation으로 준비/교체에 들어가지 않는다. Overlay readiness30초 대기나 API true gate는 없다.

사람이 오래 기다렸더라도 교체 직전에 계정·Ready·참가자 파일·config/payload·동일 Steam client를 새로 검증한다. 핵심 runtime 확정 실패와 취소를 재검사한다. 단회 claim 뒤와 실제 helper Process.Start 요청 직전에도 종료 상태를 검사한다. 취소 전에 소비한 claim은 되살리지 않는다.

기존 GameOnly의 부모 대기 이후 StartGame(null)과 이후 별도 생성 deadline 부재는 변경하지 않는다. OS 동기 I/O·Process.Start 전체에 시간 상한이 있다고 주장하지 않는다. 사용자 관찰 시간창을 없애는 것과 GameOnly helper의 실행 방식을 바꾸는 것은 별개다.

## 5. 최소 상태와 결과 저장

역할, 기존 진행 단계, 사용자 화면 보고, 첫 핵심 오류만 기본 모델로 둔다. 보조 상태축·업적 검사축·새 Admission 다단계 프로토콜을 도입하지 않는다. helper terminal과 child receipt는 기존의 독립 사건으로 보존하여 도착 순서가 달라도 하나로 덮어쓰지 않는다.

역할당 결과 보고는 한 번이다. `user-observation.json`에 run/role/process identity, Source=User, AttemptReported, Visibility, 기록 시각을 저장한다. 보임/안 보임 버튼은 열기 시도를 했다는 사용자 진술을 포함하고, 판정 불가는 확인 불충분이다. 보고 시각을 물리 키 입력 시각으로 부르지 않는다.

저장 시작 전·반환 후 취소/terminal/core 상태를 검사한다. 저장이 성공하고 종료되지 않은 경우에만 다음 단계를 허용한다. 중복 결과 클릭이나 보고 저장 중 교체는 차단한다. 저장 후 취소됐다면 이미 기록한 사용자 진술은 보존하지만 신규 교체를 허용하지 않는다. raw 화면 보고 하나만으로 전체 절차 완료를 선언하지 않는다.

원본 인계 context는 저장된 원본 opened 보고의 경로/hash를 참조한다. 실제 helper 요청은 그 보고가 원본/run/process와 맞고 core 검증이 유효한 경우에만 허용한다. 별도 제출/수용/최종승인 문서 체인을 만들지 않는다. 보고가 남아도 교체 요청/receipt/terminal이 없으면 해당 단계는 미확인으로 해석한다.

최소 필수 기록은 준비·교체 직전 격리 검증, 원본/자식 보고, claim/request/helper 생성/receipt/기존 helper terminal, 최초 핵심 오류, 최종 참가자 파일 검증 및 종료 요청이다. UTC/프로세스별 단조 시각·run/role/process identity는 이 사건을 연결할 정도로만 남긴다. 반복적인 관측 snapshot은 없다.

패널의 완료 문구는 “관찰 결과 저장 완료”다. 자기 프로세스는 자신의 실제 종료나 Steam tracking 해제를 미리 확인했다고 기록하지 않는다. 외부 검토는 남아 있는 필수 프로세스/인계 사실로 확인 가능한 완료 범위만 보고하며, 종료 확인을 위해 새로운 추적 수집기나 UI 조작을 추가하지 않는다.

## 6. 필요한 최소 관찰 wire 변경

현재 v1 context는 SdkBaselineSha256을 필수로 검증한다. getter만 삭제하거나 더미 SDK 문서를 넣지 않는다. **최소 관찰 wire Version=2**로 Request/Context/Receipt 생산자·소비자를 함께 바꾸고 v1/partial/mixed를 거부한다. 앞선 미구현 SDK v2 설계를 이어받는 것이 아니라 최소 계약 변경을 표시하는 번호다. 호환 변환/migration은 만들지 않는다.

- config는 같은 필드 의미의 Version=1 유지. 새 후보 manifest를 가리키는 새 config를 만든다.
- context의 SDK baseline 경로/hash·SDK 관련 필드는 삭제한다. run/role/origin identity, account/AppID, Ready identity, config/payload·참가자 manifest hash 등 핵심 pin은 유지한다.
- OriginObserver context는 준비 때 생성하며 아직 없는 원본 보고 경로/hash는 요구하지 않는다. ReplacementObserver context는 인계 때 새로 생성하고 원본 opened 보고의 경로/hash를 필수로 요구한다. 역할 자체로 검증하며 별도 ContextStage 축을 추가하지 않는다.
- helper/child는 run root 안의 보고 경로/hash, 보고 role=OriginObserver·원본 process identity·opened·AttemptReported를 검증한다. 원본 context/보고를 덮어쓰지 않는다. request→context→receipt의 동일성 검증은 유지한다.
- 제품 reset journal schema/reset wire와 공통 cycle/launch 구현은 변경하지 않는다. helper 파일을 복제하지 않는다. 기존 실패 run은 새 wire로 변환하거나 소급 성공 처리하지 않는다.

인계: 저장된 원본 opened 확인 → live pins/save 재검증 → 취소/core 검사 → 단회 claim → 취소/core 검사 → 최소 replacement context/request 생성 → 기존 launch 준비 → 최종 취소/core 검사 → helper 요청1회. helper 수락 이후 오류는 수락 사실과 함께 기록하고 기존 정상 종료를 진행한다.

## 7. 삭제 중 남겨야 할 실제 코드 경계

|영역|작업과 함정|
|---|---|
|OverlayHandoffObservation.cs|캡처/요청시각/sampling/checkpoint/5분 gate와 보조 결과 모델 제거. 기본 준비, 핵심 보고 저장, 취소, 단회 인계 유지|
|OverlayHandoffObservationRuntime.cs|SDK 값·DLL·frame·log·import 전체 제거. live identity/Ready/payload/save, claim/request/receipt/helper terminal, 핵심 기록/종료 유지|
|OverlayHandoffObservationEvidence.cs|이미지·Steam 로그 수집 클래스 삭제. 필요한 참가자 파일 읽기·비교 및 핵심 직렬화만 유지. 파일 전체를 무조건 삭제하지 않음|
|OverlayHandoffObservationPresentation.cs|결과 버튼·교체·오류·종료 중심으로 축소. coroutine 삭제 시 함께 있던 PrepareMenuAsync 시작 fallback을 잃지 않음|
|SteamOverlayObservationAccess.cs|구독/DTO는 삭제하되 Requested/InhibitWrites/Starting/RequireWritesAllowed/native runtime 참조·fault 판별/live identity seam은 유지. 파일 통삭제 금지|
|SteamPlatformRuntime.cs|진단 구독/forwarder/업적 getter seam 제거. Requested 조건이 억제하던 publication/achievement smoke를 다시 켜지 않음. 정상 callback pump 보존|
|공통 유틸 참조|삭제할 subscription 클래스의 MonotonicSeconds/Limit 등을 핵심 flow가 참조하는지 확인. 준비 대기에 필요한 시계/핵심 오류 형식만 적절한 기존/작은 core seam으로 옮기고 빈 구독 클래스를 유지하지 않음|
|RestartExperimentWindows.cs·필요한 PS host|최소 관찰 v2 필드/validator/receipt 적용. 기존 reset/cycle/launch는 보존|
|tests/builder/compiled inspector|삭제된 생산자의 긍정 테스트·필터·구독/getter 연결 요구 제거. 핵심 startup/write guard/보고/인계 연결 검증으로 갱신|

현재 미커밋 변경과 사용자 파일은 사전 보존한다. HEAD commit revert, reset --hard, clean, 넓은 restore를 사용하지 않는다. 불필요한 이름 변경·새 추상화·새 서비스·추가 관측 도구는 만들지 않는다.

## 8. 최소 수용 테스트

1. 요청시각/캡처/보조 API 호출 없이 원본 opened 저장 → GameOnly helper1·Steam 종료0 → 최소 v2 receipt/child 준비 → 자식 opened 또는 not-visible 저장 → 종료. 원본 미보고/not-visible/inconclusive이면 helper0.
2. 조기 opt-in 정상/설정실패/중복/원본·자식에서 금지 쓰기0. StartupAlreadyStarted는 이전 이력 보존·추가 쓰기/교체0. 정상 일반 startup positive control 유지.
3. 계정/Ready/Pending/save/payload/Steam client·핵심 기록 실패 시 차단. 업적 getter 결과를 준비 전제로 요구하지 않음. remote achievement 불변을 검증했다고 주장하지 않음.
4. 느린 핵심 저장/재검증 중 취소·claim 뒤 취소·helper 요청 직전 취소·중복 클릭·생성 불확실에서 늦은 진행/재시도0. 수락 후 종료를 취소 성공으로 바꾸지 않음.
5. 사람의 대기가5분을 넘어도 새 live 검증 성공 시 교체 가능. 그 사이 계정/save/client 변경은 차단. native/receipt 준비30초와 기존 GameOnly 부모 대기30초·StartGame(null)은 유지.
6. 실제 최소 v2 producer→helper→child에서 old/mixed/partial/context/report/nonce/identity/hash 오류 거부. SDK baseline 필드/더미 파일 의존이 없음.
7. 실제 패널의 캡처 없는 보고·교체·준비 중 종료·작은 화면/키보드/마우스 조작. batch skip은 시각 통과 아님. 삭제한 선택 영역/입력 절차가 없음.
8. 관찰 composition에서 삭제된 sampler/collector/getter/module/frame/import 호출0 확인. 정상 제품 callback·쓰기 차단 유지. 삭제 기능의 정상 작동을 검증하는 새 복잡한 테스트는 만들지 않음.

동일 D worktree에서 core/UI 및 남은 관찰/startup/reset/restart/platform/save focused full, 관찰 wire를 바꾸는 Windows helper fake suite를 수행한다. 삭제된 fixture 이름을 필터에 남겨0건 통과로 처리하지 않는다. 각 실행의 실제 선택 건수·exit를 확인한다. 자동 시험은 실제 Steam/game/native/참가자 save를 호출하지 않는다. 무필터 full과 실제 Steam 표시 시험은 별도 상태로 기록한다.

빌드는 같은 소스 manifest, compiled 조기진입/쓰기차단/최소 report·wire 연결, raw→검사→final DLL/EXE/helper/전체 payload, define·사용자 파일 복원을 확인한다. 삭제된 sampling 연결을 검사 도구가 계속 요구하지 않아야 한다. 캡처 없는 실제 패널 검증은 자동 batch 결과와 별도로 남긴다.

## 9. 완료 조건과 결론의 한계

완료 조건은 **보조 기능의 생산·저장·UI·상태·시간창·테스트·검사 의존이 함께 사라지고, 필수 정상 흐름 및 실패 차단이 검증된 후보 하나**다. 삭제 후 빈 collector/더미 SDK 파일/숨은 gate를 남기면 완료가 아니다. 중간 부분수정 후보를 사용자에게 순차 시험시키지 않는다.

이 시험에서 말할 수 있는 것은 사용자 보고 기반 원본/자식 표시 비교, 관찰 코드의 reset·게시·참가자 저장 호출 차단, checkpoint 사이 참가자 파일 바이트 불변, 검증된 단회 GameOnly 인계다.

API enabled·activation/입력 전달·hook/DLL 원인·원격 업적 값 불변·실제 graphics 완전 동일·중간 모든 파일 쓰기0은 조사하지 않으므로 결론에 포함하지 않는다. 자동 helper/결과 기록만으로 프로세스 종료·Steam tracking 해제를 증명하지 않는다. 원인 분석이 나중에 필요하면 그 질문에 필요한 별도 시험을 새로 설계하며 보조 기능을 이번 수정에 미리 넣지 않는다.

FullCycle, 업적 재획득/재삭제, 자동 반복 A/B, DX/환경/다른 Overlay 설정 변경, Computer Use 구축은 범위 밖이다. 원래 업적 표시 갱신 시험도 미완료로 유지한다.

새 config·후보를 실제 준비한 뒤 구현/검증/빌드/업로드/branch/설치/시험 상태를 구분한다. 전체 설치가 새 후보와 일치하는지 확인한 후에만 새 절차를 안내한다. 관찰 인자를 지우고 일반 실행하는 것을 무쓰기 rollback으로 안내하지 않는다.

## 10. 독립 검토

minimal_deletion_audit와 minimal_contract_review가 필수 기능만 유지하는 삭제 범위를 독립 검토했다. 두 검토는 보조 기능·그 의존성 삭제, 핵심 write guard 보존, SDK를 제거한 최소 wire 변경, 사람의5분 제한 제거를 권고했다. 문서 반영 후 삭제 누락·안전 경계 누락을 재확인했고, 두 검토의 범위에서 추가 P1/P2 지적은 없었다.

근거: [삭제 범위 재검토](/mnt/d/J2M/evidence/overlay-minimal-plan-review/20260909/deletion.md), [최소 계약 재검토](/mnt/d/J2M/evidence/overlay-minimal-plan-review/20260909/contract.md). 구현·실행 검증이 완료됐다는 뜻은 아니다.

이전 선택 기능 유지안의 P2 해결 결과를 새 최소안의 검증 결과로 재사용하지 않는다. 이번 작업은 계획 수정·검토이며 실제 구현은 변경하지 않았다.
