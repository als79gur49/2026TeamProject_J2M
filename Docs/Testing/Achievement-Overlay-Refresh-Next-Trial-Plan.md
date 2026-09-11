# 업적 표시 갱신 후속 시험 구체안

> 이 문서는 당시 revision의 설계·관찰·검증 이력이다. 전시 초기화·재시작을 보존한 smoke·관측 구조 정리 후 현재 코드와 검증 상태는 [Exhibition Runtime Cleanup](../Architecture/Exhibition-Runtime-Cleanup.md)을 따른다. 아래 과거 명령과 삭제된 관측 도구는 현재 실행 절차가 아니다.


> **진행 방향 정정 — 2026-09-09:** 아래는 이미 수행한 GameOnly 비교 시험의 역사적 계획이다. 다음 시험의 우선순위로 재사용하지 않는다. [진행 방향 정정](Achievement-Overlay-Refresh-Direction-Correction.md)을 먼저 읽는다. 게임 단독 재시작 무효·수동 Steam 완전 재시작 성공은 과거에 보고됐고 이번 시험은 표시 불일치의 재확인이다. 다음 검토는 수동 성공 절차와 자동 helper의 차이에 집중하며, 재획득·재초기화를 기본 단계로 삼지 않는다.

2026-09-09. 대상 `/mnt/d/J2M/worktrees/exhibition-reset`. **계획 문서이며 실행 안내가 아니다.** 이번 작업은 소스·기존 증거 검토와 문서 작성뿐이다. 코드 수정, 테스트, 빌드, 업로드, branch 변경, 설치, 게임·Steam·native 실행, 업적 조회·변경을 수행하지 않았다.

## 1. 결정과 다음 질문

최소 Overlay 관찰은 1회 정상 사례를 확보했다. 다음 업적 시험은 별도 시험으로 취급하며, 우선 질문을 **“이미 획득한 매핑 업적을 기존 참가자 초기화로 지운 뒤, 동일 Steam client의 GameOnly worker에서 그 업적이 Overlay에도 미획득으로 표시되는가?”**로 한정한다.

이번 구체안의 다음 실제 시험에는 FullCycle을 포함하지 않는다. 기존 reset trial에는 FullCycle이 연결되어 있으므로, 현재 버튼 흐름을 그대로 실행할 수 있다는 뜻도 아니다. 초기화 직후 비교에서 끝나는 후보의 적합성 검토·필요 수정이 선행되어야 한다. Steam 재시작 전후 비교가 여전히 필요하다고 판정될 때만 별도 범위로 설계한다. 자동 A/B, 재획득/재삭제, 환경·DX·Overlay 설정 변경, Computer Use, 원인 수집기는 추가하지 않는다.

현재 Build25206354의 최소 관찰 시작 옵션은 유지한다. 기존 reset config로 교체하거나 인자를 제거해 일반 실행하지 않는다. 후속 시험안 구체화는 계정 쓰기 실행 지시가 아니다.

## 2. 현재 근거와 부족한 조건

| 근거 | 확인된 사실 | 재사용 한계 |
|---|---|---|
| Build25206354 설치 검증 | 257개 파일 hash 일치, 추가 파일 없음 | 최소 관찰용 검증이며 reset 전용 경로 수용 완료가 아님 |
| `e301cb80c3994177ad5e1cba3e0f12e3` | 원본·자식 사용자 보고 opened, 단회 GameOnly, 동일 client, v2 인계, 5시점 참가자 파일 일치 | 초기화·업적값·업적 화면 갱신은 확인하지 않음 |
| 과거 reset run `428682c9ff2449d3a3c2e83ed3b9f0db` | Baseline `[true,false,false,false,false]` 뒤 ResetCommitted 5개 false, LocalResetVerified, AwaitingOverlay | 검토한 run에서 FullCycle 요청·최종 비교 기록 없음. 현재 계정 상태의 증거로 쓰지 않음 |
| Build25187326 Gate B/C | 별도 Probe·FullCycle의 당시 성공 기록 존재 | 현재 후보·초기화 후 흐름 성공으로 승격하지 않음 |
| 기존 `reset-overlay-config.json` | 과거 trial-correction payload manifest 참조 | 현재 설치와 다름. 경로만 재사용하거나 일부 파일을 덮어써 맞추지 않음 |

현재 SDK 업적 상태는 **미조회**다. 과거에 이미 초기화한 기록이 있으므로 획득 비교 대상이 남아 있다고 가정하지 않는다. 로컬 ledger 또는 이번 opened 보고로 SDK 값을 대신 추정하지 않는다.

근거:

- [최소 관찰 외부 검토](/mnt/d/J2M/evidence/overlay-handoff-observation/20260909T103944445Z-e301cb80c3994177ad5e1cba3e0f12e3/external-review.json)
- [설치 확인](/mnt/d/J2M/evidence/overlay-handoff-observation/20260909T095216Z-observation-minimal/installed-verification-minimal.json)
- [과거 초기화 worker 기록](/mnt/d/J2M/evidence/reset-overlay-trial/20260908T161149470Z-428682c9ff2449d3a3c2e83ed3b9f0db/observations-7848.jsonl)
- [과거 FullCycle 검토](/mnt/d/J2M/evidence/participant-restart-preflight/20260908T131514469Z-31dba880c8834ec9821bf886058e991c/gate-c-review.md)
- [과거 config 재검토](/mnt/d/J2M/evidence/reset-overlay-trial/config-reaudit-20260909/result.json)

## 3. 최소 관찰 수용의 남은 항목

원본 보고→교체→자식 보고→종료 요청은 실제 정상 사례로 기록한다. 작은 화면의 버튼 접근, 키보드·마우스 각각의 입력, 준비 중 종료는 미확인으로 남긴다. 이번 정상 실행이 세 항목을 모두 통과했다는 뜻은 아니다.

향후 입력 검증은 계정 변경 없는 기존 fake 패널 환경에서 우선 확인한다. batch skip은 시각 통과가 아니다. 실제 작은 화면 확인 때문에 게임 graphics/window 설정을 이번 비교에 섞지 않는다. 준비 중 종료는 종료 뒤 늦은 continuation·helper 요청이 없는지를 함께 확인해야 한다. 이 항목들을 업적 초기화와 합쳐 시험하지 않는다.

## 4. 현재 코드와 후속 후보의 차이

| 코드 경계 | 현재 동작 | 다음 준비에서 필요한 결정/검증 |
|---|---|---|
| `ExhibitionApplication` | reset trial과 최소 observation을 서로 다른 composition으로 진입 | 조기 opt-in, 잘못된 인자 fallback 차단, 일반 제품 positive control을 reset 후보에서도 확인 |
| `ResetOverlayTrial.PrepareMenuAsync` | Initiator baseline, worker ResumeAsync 후 로컬/SDK 검사, FinalObserver 서비스 시작 | 다음 시험은 worker 결과 저장·종료까지. 정상 서비스 재개와 final 메뉴는 범위 밖 |
| `RequestReset` | live 검증·baseline 재확인·claim·Pending·GameOnly 요청 | 최초 쓰기 지점은 Pending 저장. 비용 검사/claim 뒤/실제 helper 요청 직전 취소·terminal 확인을 재검토. 최소 observation의 개선을 이 클래스에도 이미 적용했다고 가정하지 않음 |
| `ResetOverlayTrialRuntime.Validate` | GameOnly 전에도 Gate C 자료와 FullCycle preflight를 요구 | FullCycle 없는 시험의 필수 조건과 불필요한 의존을 분리하는 수정 범위를 먼저 정함. Gate C 파일 존재/hash 자체는 검토 결과의 의미를 검증하지 않음 |
| `ReadAchievements`/`Snapshot` | 별도 reset 경로의 5개 getter와 단계별 SDK 값 저장 | 초기화 대상·결과 확인에 필요한 기존 경로만 사용. 최소 observation에 getter·SDK 문서 체인을 복원하지 않음 |
| `ResetOverlayTrialPresentation` | baseline/중간 기록 확인란, 중간 뒤 FullCycle 계속 버튼 | 화면 보고 실제 값의 영구 저장은 현재 확인란만으로 충족하지 않음. 다음 후보는 대상별 사용자 보고 저장과 종료를 제공하고 자동 FullCycle 연결을 제거/차단해야 함 |
| 기존 공통 reset protocol/cycle/helper | 초기화·Store 결과 대기와 공통 단회 인계 수행 | 기존 owner·callback·정상 Shutdown·lock 유지. helper 복제, journal schema 변경, 다른 reset/restart 기능 삭제 금지 |

따라서 **현재 설치에 reset 인자를 넣는 것만으로 후속 시험 준비가 끝나지 않는다.** 필요한 변경은 별도 reset 시험의 보고·종료 경계 및 실제 필요한 preflight에 한정하고, 최소 observation v2와 그 삭제 범위를 유지한다. 이번 문서는 그 변경을 구현하지 않는다.

## 5. 후속 후보의 시작 조건

아래 조건을 갖춘 후보를 먼저 준비한다. 실제 적용 대상은 AppID 5218360, 현재 지정 계정이며, 실행 때 live identity/login을 다시 확인한다.

1. 단일 Steam provider, 조기 trial 진입, 제품 publication/campaign/seed의 최초 자동 쓰기 보류가 확인된다. 이미 시작된 세션이나 혼합 인자는 중단한다.
2. Ready journal의 계정·operation·mapping·state와 config/전체 payload/동일 client/참가자 파일을 검증한다. Pending은 자동 복구하지 않는다.
3. 시험 전용 config는 최종 후보 manifest를 참조한다. 이전 config·실패 run·context는 덮어쓰거나 변환하지 않는다.
4. native 준비 후 기존 reset 경로가 5개 SDK 조회 결과를 저장한다. 적어도 한 개가 획득이어야 한다. 전부 미획득, 조회 실패, 기록 실패면 초기화·Pending·helper 모두 없이 종료한다.
5. 사용자가 그 획득 대상의 Overlay 업적 화면도 확인한다. 단순히 Overlay가 열렸다는 보고로 대체하지 않는다. 기준 SDK와 화면이 이미 다르거나 대상 식별이 불충분하면 이번 전후 비교를 시작하지 않는다.
6. 초기화 범위 **VQ_LEVEL_0_CLEAR~VQ_LEVEL_4_CLEAR 5개와 기존 참가자 campaign/로컬 업적 데이터**를 화면에 명시한다. 전체 Steam 통계 초기화가 아니다. 사용자 확인 이후에만 쓰기를 허용한다.

모두 미획득이면 이번 질문에 필요한 비교 대상이 없는 것이다. 재획득, SetAchievement, 계정 교체, save 복원으로 대상을 자동 생성하지 않는다. 이 경우 결과는 “실패”가 아니라 “시작 조건 미충족”이며, 다른 질문이 필요한지 별도로 결정한다.

## 6. 준비된 후보에서 수행할 단회 순서

아래는 향후 구현·검증·설치 확인 후 사용할 절차 사양이다. 현재 실행용 명령은 제공하지 않는다.

| 단계 | 사용자 동작 | 앱이 완료해야 할 일 | 다음 단계 조건 |
|---|---|---|---|
| 준비 | Steam에서 지정 trial 실행 후 기다림 | 읽기 전용 신원/Ready/파일 검증, baseline 저장 | 획득 비교 대상 존재, 필수 기록 성공 |
| 기준 화면 | Shift+Tab으로 업적 목록의 대상 확인 후 결과 보고 | 대상별 화면 보고를 Source=User로 저장 | SDK 획득 대상과 사용자 기준 화면 일치 |
| 초기화 요청 | 명시된 초기화 범위 확인 후 버튼 1회 | live pins/파일/SDK baseline 재검증, 취소 검사, claim, Pending, 단회 GameOnly | 각 검증 통과. 원본 종료는 수락한 인계 진행 |
| worker | 새 게임이 준비될 때까지 기다림 | 부모 종료·receipt 검증, 기존 ResumeAsync 1회, Ready/로컬/SDK 결과 확인 | ResetCommitted 및 로컬 검증 성공, SDK 5개 미획득 |
| 결과 화면 | 같은 대상의 Overlay 업적 표시를 보고 | 대상별 획득/미획득/판정 불가와 열기 시도 진술 저장 | 저장 성공. opened만으로 업적 갱신 성공 판정 금지 |
| 종료 | 결과 저장 확인 후 종료 | 최초 오류 및 종료 요청 보존 | 새 게임·Steam 재시작 요청 없이 끝냄 |

사용자는 캡처·파일 찾기·복사·경로 입력을 하지 않는다. 사람이 읽는 제목만으로 API 이름 대응이 모호하면 해당 대상은 판정 불가다. 사람이 기다리는 5분 제한이나 반복 sampling은 추가하지 않는다. 기계적 준비/receipt/기존 reset 대기는 해당 기능의 검증된 정책을 유지하며 동기 I/O 전체 시간 상한이라고 설명하지 않는다.

화면 보고는 최소한 비교 대상 API 이름, 사용자 표시 판단, run/role/process 및 기록 시각을 연결하면 된다. 기존 단계 기록을 활용하고 별도 제출/최종승인/SDK 문서 체인이나 보조 collector를 만들지 않는다. SDK 조회는 해당 프로세스의 SDK 관측이며 원격 서버 상태를 독립 증명하는 것으로 부르지 않는다.

## 7. 판정표

| 결과 | 판정/후속 |
|---|---|
| SDK baseline 전부 미획득 | 비교 대상 없음. 초기화 없이 종료. 재획득 자동 진행 금지 |
| baseline SDK 획득/화면 미획득 또는 판정 불가 | 시작 전 불일치 또는 증거 부족. 초기화하지 않음 |
| 초기화/Ready/로컬/SDK 검증 실패 | 해당 단계 실패. 표시 갱신 성공으로 분류하지 않음 |
| worker SDK 미획득 + 대상 화면 미획득 | 동일 client의 초기화 후 표시 갱신을 1회 관측 |
| worker SDK 미획득 + 대상 화면 획득 | SDK 관측과 화면 표시의 불일치가 해당 시점에 지속됨 |
| worker Overlay 안 열림/대상 확인 불가/보고 누락 | 표시 비교 판정 불가. 이전 무초기화 GameOnly 성공으로 대체하지 않음 |
| SDK에 획득값이 남음 | 초기화 결과 확인 실패. 캐시 원인으로 단정하지 않음 |
| 교체 수락·생성·receipt·terminal 일부만 존재 | 확인 가능한 단계만 기록. 저장된 화면 보고만으로 전체 성공 선언 금지 |

파일 검증은 초기화 **전에는 불변**, 초기화 **뒤에는 의도한 참가자 초기화 결과**를 확인한다. 삭제를 수행하는 시험에서 전후 모든 hash 일치를 성공 조건으로 두지 않는다. 자동 코드 검증에서 금지 호출 차단을 확인하되, 실제 hash만으로 중간 모든 쓰기 0을 주장하지 않는다.

## 8. 실패·중단·후속 분기

- Pending 전 오류: 기록 검토 후 종료. 일반 startup으로 fallback하지 않는다.
- Pending 뒤 오류/worker 생성 불확실/초기화 일부 적용: claim을 되살리거나 재시도하지 않는다. journal·인계·오류를 읽어 적용 범위를 먼저 확인한다. 일반 실행은 Pending 복구를 수행할 수 있으므로 무쓰기 복구 방법으로 안내하지 않는다.
- worker 표시 비교 뒤에는 종료한다. 현재 구체안은 FullCycle을 요청하지 않는다. 기존 실패 run의 계속 버튼이나 context를 재개하는 절차도 없다.
- 표시 불일치가 남고 Steam 재시작 비교가 필요하면, 그때 **원래 획득 상태를 다시 만들지 않고 이미 미획득인 상태의 표시가 재시작 전후 달라지는가**라는 별도 비초기화 시험을 설계할 수 있다. 이는 현재 완료/승인된 흐름이 아니며 새 재시작 시험의 시작 조건·서비스 보류·결과 연결을 검토해야 한다.
- 프로세스 실제 종료와 Steam tracking 해제는 종료 요청/HelperCompleted와 구분한다. 확인하지 않은 항목은 미확인으로 남기며, 확인을 위해 새 추적 수집기를 추가하지 않는다.

## 9. 구현·검증·배포 준비의 완료 기준

다음 구현에 들어갈 경우 기존 사용자 변경을 먼저 보존하고, 4절의 제한된 차이를 수정한다. 최소 observation의 삭제 기능을 다시 넣거나 별도 helper를 만들지 않는다.

필수 검증은 실제 startup/write guard, baseline 보고 저장 전 쓰기 차단, 대상 없음/조회·기록 실패, baseline 변경, Pending/claim/취소/생성 불확실, 단회 Resume/인계, worker 결과 보존, 정상 제품 positive control이다. 새 상태기계 fake만으로 production composition 검증을 대신하지 않는다.

동일 D worktree에서 `./run_tests.sh core`, `./run_tests.sh ui`, 실제 존재하는 reset/observation/startup/restart/platform/save fixture를 선택한 focused full, 공통 helper 변경 시 Windows fake suite를 실행한다. 선택 건수·exit·skip을 기록한다. 자동 시험은 실제 Steam/native/game/save를 호출하지 않는다. 무필터 full 및 실제 UI는 별도 상태로 남긴다.

고정한 소스 manifest와 compiled trial 조기 진입/쓰기 경계/보고/인계/FullCycle 차단 연결을 검사하고 raw→검사→final 전체 payload 및 define·사용자 파일 복원을 확인한다. 필요 수정이 있는 이상 현재 빌드에 config만 바꿔 끼우는 것으로 대체하지 않는다.

| 상태 | 현재 |
|---|---|
| 이번 최소 관찰 정상 사례 | 검토 완료 |
| 최소 관찰 전체 수동 UI 수용 | 일부 미확인 |
| 후속 업적 시험 질문·판정·중단 절차 | 이 문서로 구체화 |
| 후속 제한 범위 구현·자동 검증·compiled 검사 | 미실행 |
| 후속 후보/config 준비·업로드·branch·설치 | 미실행 |
| 현재 SDK baseline 조회·초기화·표시 비교 | 미실행 |
| FullCycle 추가 시험·재획득/재삭제 | 이번 범위 밖 |

이번 계획은 [기존 reset trial](./Reset-Overlay-Trial.md)의 사실 기록을 보존하면서 후속 범위를 좁힌다. [최소 observation 수정안](./Overlay-Handoff-Observation-Correction-Plan.md)의 무쓰기 계약에 초기화 쓰기를 섞지 않는다. 검토 입력 hash와 기존 문서 보존 사본은 `/mnt/d/J2M/evidence/achievement-overlay-next-plan/20260909`에 남겼다. 독립 검토나 실행 검증이 완료됐다는 주장은 하지 않는다.
