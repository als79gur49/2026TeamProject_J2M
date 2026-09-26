# Campaign Casual / Hardcore Mode Design

## 1. 문서 상태와 구현 범위

- 작성일: 2026-09-24
- 상태: **초기 구현 진행 중, 검증 미완료**. 사용자 요청에 따라 저장·복구 강화안을 초기 구현에서 제외했다.
- 2026-09-25 확정: 저장 오류는 메뉴/종료로 안내하고, 진행 중 슬롯은 기존 Continue, 완료 슬롯은 완료 카드로 처리한다. [구체 수정안](./Campaign-Casual-Hardcore-Save-Error-Menu-Plan.md)에 따라 전용 Reload를 제거하고 runtime 연결을 교체했다. 최종 검증과 번역/Player 확인은 실행 보고서를 따른다.
- 2026-09-25 추가 적용: 방안 A에 따라 전환 동기 알림 중 복구 시 즉시 플레이를 차단하고 호출 종료 후 token 정리·오류 안내를 마무리한다. clear 저장 전 중지 검사와 Iris 소유권 재검사를 추가했다. 진단 조회 정책 및 영속 저장 형식은 바꾸지 않았다.
- 조사 기준 revision: `1719c6a4970313dd54506321ebe9b09513fc924d`.
- 현재 worktree에서 mode/schema 3, 전이·survival 저장, launch HP·무적, HUD·모드 선택·저장 오류 경로를 구현하고 있다. 2026-09-25 사용자 승인으로 11개 문구의 4언어 production table/atlas 적용을 완료했다. Player 확인은 남아 있다. 기존 세이브 변환은 하지 않는다.
- 현재 저장 계약은 [Pre-Release-Save-Baseline-Policy.md](./Pre-Release-Save-Baseline-Policy.md)의 profile schema 3 / local-state schema 1이다. 조사 시점 schema 2는 미지원으로 차단한다.
- 이전 안의 영속 명령 영수증·해시·복구 세대·같은 씬 저장 재시도는 초기 구현 요구에서 제외한다. 후속 보장 강화는 11장에서 별도로 다룬다.

초기 목표는 기존 구조에 두 모드의 동작을 연결하여 플레이하고 검증할 수 있게 만드는 것이다. 기존 파일 검증·atomic writer·백업·launch 소유권은 계속 사용한다.

관련 기준:

- [초기 구현 실행 프롬프트](./Campaign-Casual-Hardcore-Implementation-Prompt.md)
- [Tick-Simulation-Canonical-Spec.md](./Tick-Simulation-Canonical-Spec.md)
- [UI-Architecture-Guidelines.md](./UI-Architecture-Guidelines.md)
- [Gameplay-Death-Recovery-Lifecycle.md](./Gameplay-Death-Recovery-Lifecycle.md)
- [Campaign-LocalState-Launch-State.md](./Campaign-LocalState-Launch-State.md)
- [Campaign-Stage-Sequence-Authority.md](./Campaign-Stage-Sequence-Authority.md)
- [Gameplay-Test-Automation-Guide.md](../Testing/Gameplay-Test-Automation-Guide.md)

## 2. 사용자 확정 규칙

### 2.1 사용자 확정 규칙

| 항목 | 캐주얼 | 하드코어 |
| --- | --- | --- |
| HP | 최대 3 | 현재 authored 값 유지 |
| 일반 피해 | 기존 공격 피해량만큼 감소 | 현재 방식 유지 |
| 피격 후 보호 | 생존하면 일정 시간 무적·깜빡임 | 현재 방식 유지 |
| 낙사·압사 | 즉사 | 현재 방식 유지 |
| HP 회복 | 스테이지 클리어 시 회복 | 현재 방식 유지 |
| 사망 목적지 | 현재 레벨 묶음의 첫 스테이지 | Chance가 남으면 현재 스테이지 |
| 기회 | 무제한, Chance 사용 안 함 | 기존 Chance 3회 |
| 마지막 Chance | 해당 없음 | 캠페인 전체 첫 스테이지, Chance 3 |
| 다음 레벨 진입 | clear에 따른 HP 회복 | 현행처럼 Chance 3 회복 |
| 사망 복귀의 기록 | 과거 기록과 진행 위치를 구분 | 과거 기록·누적 사망·코믹 감상 이력 보존 |
| 슬롯 | 기존 3개 공유, 생성 시 모드 고정 | 동일 |

하드코어는 같은 레벨 내 clear에서 남은 Chance를 유지하고, 최종 clear에는 다음 레벨 진입 회복을 적용하지 않는다. 전체 첫 스테이지는 sequence에서 구한다. 현재 실제 첫 항목은 `level-0 / stage-0-1`이며 `stage-1-1`로 하드코딩하지 않는다.

### 2.2 추가 확정 사항 — P1~P4

| ID | 사용자 확정 내용 | 구현 반영 |
| --- | --- | --- |
| P1 | 캐주얼 최초 시작·사망 후 새 도전 HP 3. 수동 재시작·메뉴 복귀·프로세스 재실행에는 남은 HP 유지 | ResumeHp와 생존 피격 저장 구현 |
| P2 | 캐주얼 피격 무적은 우선 2초, 플레이하면서 조정 | 초기 설정 2초를 simulation tick으로 환산 |
| P3 | 기존 공통 캠페인 업적을 두 모드에서 인정 | 정상 clear fact를 기존 업적에 연결 |
| P4 | 기존 세이브 형식 미허용. 기존 구버전 미지원 처리와 동일하게 차단 | 자동 변환·모드 배정·초기화 없이 UnsupportedVersion 처리 |

P1~P4는 사용자 답변으로 모두 확정했다. `ResumeHp`와 피격 저장은 초기 구현 범위다. 수동 재시작·메뉴·재실행마다 HP 3으로 회복시키지 않는다. P2의 수치는 플레이 검증에서 조정할 수 있다.

남은 HP 저장을 구현하되 영속 명령 영수증이나 같은 씬 저장 재시도는 추가하지 않는다.

## 3. 초기 구조

```text
기존 immutable slot / parser / mapper에 모드 추가
→ 기존 planner / transition engine에 모드별 규칙 추가
→ 기존 launch transaction에서 초기 HP 준비
→ 기존 preset 적용 후 캐주얼 무적 설정
→ 기존 Flow에서 결과 선택 → 동기 저장
   성공: 기존 표현·진행
   실패: 실행 중지 → 메뉴/종료 안내 → 메뉴에서 파일 검증 → Continue 또는 완료 카드
```

- `WorldState`가 플레이 중 HP·무적 상태를 소유하고 `WorldSnapshot`으로 조회한다. 피해 쓰기는 기존 Finalize/batch 경계를 유지한다.
- 저장된 `ResumeHp`는 다음 진입용 값이다. 별도 simulation 상태 소유자를 만들지 않는다.
- Production과 Transient는 기존 공통 transition engine을 사용한다.
- 새 모드 framework, 범용 transaction framework, 별도 terminal arbiter를 만들지 않는다. 기존 클래스에 책임을 추가하고 실제로 커진 부분만 추출한다.
- 테스트는 모드 규칙과 기존 동작 보존을 먼저 확인한다. 강화 프로토콜의 테스트를 초기 기능 완료 조건으로 넣지 않는다.

## 4. 저장 위치와 슬롯 모델

### 4.1 저장 위치

```text
Application.persistentDataPath/Saves/
  profile.json               공용 슬롯 3개와 모드별 진행
  profile.json.bak           기존 복구용 백업
  local-launch-state.json    기존 장치 활성 슬롯 번호
  achievements.json          기존 제품 공통 업적
```

모드별 폴더·별도 프로필은 만들지 않는다. 기존 temp·rollback·quarantine 처리와 DirectPlay/capture의 격리 root를 유지한다.

### 4.2 필드와 검증

초기 추가 필드는 다음으로 제한한다.

| 필드 | 용도 |
| --- | --- |
| `GameMode` | Unknown=0 / Casual=1 / Hardcore=2, occupied slot에는 유효한 모드 필수 |
| `ResumeHp` | 캐주얼 다음 진입 HP, 1..3 |

`RemainingChances`, stage/group, 완료 여부, 누적 사망, 기록·코믹 이력은 기존 필드를 확장하여 사용한다. 모드별 검증과 accessor로 HP/Chance 의미를 구분하며 별도 클래스 계층은 필수가 아니다.

새 JSON의 모드별 물리 표현:

| 모드 | ResumeHp | RemainingChances |
| --- | --- | --- |
| Casual | 1..3 | 정확히 0 |
| Hardcore | 정확히 0 | 1..3 |

inactive 필드의 0은 새 형식의 예약 기본값이다. 캐주얼 런타임에 가짜 Chance나 무한대 숫자로 제공하지 않는다. 누락된 active 값의 0, 미지의 모드, inactive nonzero는 거절한다. 현재 schema 2의 Chance 0 거절 계약은 그대로 유지한다.

- 새 profile schema는 3으로 구현한다. 기존 schema 2 및 그 외 미지원 버전은 기존 UnsupportedVersion 경로로 차단한다. 실제 코드의 현재 schema 상수는 구현 시 변경한다.
- local-state는 초기 구현에서 schema 1을 유지한다. 활성 슬롯 번호는 선택 정보이며 실행 권한은 검증한 profile과 launch context에서 얻는다.
- 영속 `Revision`, `OperationId`, `LastMutationReceipt`, fingerprint는 추가하지 않는다. 기존 HUD cache Revision도 영속 버전으로 사용하지 않는다.
- 기존 완료 증빙·processed-ID·performance 필드와 그 검증은 보존한다. 이번에 제외하는 것은 새 명령 영수증이며 기존 완료 증빙이 아니다.
- physical absence만 Empty다. invalid 데이터를 clamp하거나 빈 슬롯으로 바꾸지 않는다. exact clone과 strict mapper의 기존 책임을 유지한다.

### 4.3 기존 슬롯 식별과 기록 보존

슬롯 식별은 기존 슬롯 번호 1~3을 사용한다. 새 게임 생성 때 모드를 저장하고, 같은 슬롯의 사망 복귀에는 모드를 유지한다.

삭제·Overwrite·Restart는 기존 확인창·요청 취소·confirmation generation·matching-token 처리를 유지한다. gameplay 요청은 기존 expected stage·생존 값 등 실제 전환 조건을 검사한다.

같은 슬롯 번호에 새 게임이 만들어진 사실을 별도 영속 ID로 추적하지 않는다. 기존 취소·token 검증 범위를 넘어 모든 지연 요청에서 이전 게임과 새 게임을 구별하는 보장은 후속 범위다.

사망 복귀는 cursor와 생존 값, 누적 사망을 변경하고 과거 기록·완료 증빙·코믹 이력·다른 슬롯을 보존한다. 과거 clear 기록으로 현재 cursor 이후에 바로 진입하지 않는다. 이미 완료된 슬롯의 뒤늦은 death는 거절하고 완료 후 다시 시작은 기존 lifecycle 흐름을 사용한다.

## 5. 게임플레이와 초기화

### 5.1 모드별 전환

아래 HP 초기화·재진입 값은 확정된 P1 기준이다.

| 사건 | 캐주얼 | 하드코어 |
| --- | --- | --- |
| 최초 시작 | 캠페인 처음, HP 3 | 기존 초기 상태 |
| 일반 피해 후 생존 | 최종 HP 저장, 무적·깜빡임 | 기존 피해 처리 |
| 현재 레벨 중 사망 | 레벨 처음, HP 3, TotalDeaths+1 | Chance>1이면 같은 stage, Chance-1, TotalDeaths+1 |
| 마지막 Chance 소진 | 해당 없음 | 캠페인 전체 처음, Chance 3, TotalDeaths+1 |
| 같은 레벨 clear | 다음 stage, HP 3 | 다음 stage, Chance 유지 |
| 다음 레벨 clear | 다음 stage/group, HP 3 | 다음 stage/group, Chance 3 |
| 최종 clear | Completed, HP 3 | Completed, 기존 Chance 유지 |
| 수동 재시작·메뉴·재실행 | 저장된 HP로 현재 stage의 보드를 다시 구성 | 기존 방식 |

최고 기록·누적 사망·코믹 이력을 지우는 슬롯 새 생성과 사망에 따른 진행 복귀를 같은 API로 처리하지 않는다. demo 이동·과거 stage route는 현재 cursor 검증을 유지하고 새로운 replay 기능을 추가하지 않는다.

### 5.2 초기 HP와 timing 적용

현재 [GameplayShowcaseSceneInstallerBase.cs](../../Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayShowcaseSceneInstallerBase.cs)는 `ConfigureRuntimeConfiguration` 다음에 timing preset을 적용한다. preset은 timing clone을 교체하지만 초기 entity 배열은 바꾸지 않는다.

초기 구현 순서:

1. 기존 `StageRuntimeBuilder`로 authored 상태를 만든다.
2. [StageBackedGameplaySceneInstallerBase.cs](../../Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs)의 기존 launch transaction에서 검증한 immutable slot을 초기 HP 준비에도 사용한다.
3. Casual이면 초기 entity 배열을 복사하여 Player만 `maxHp=3`, `hp=ResumeHp`로 설정한다. 이 준비·검증은 active write 전 또는 기존 transaction 보상 범위 안에서 처리한다.
4. 기존 active/running commit과 preset 순서를 유지한다.
5. preset 이후 작은 default no-op 훅에서 검증한 캐주얼 cooldown만 적용한다. 초→tick은 기존 timing resolve 경로를 쓴다.
6. 완성된 configuration으로 host를 초기화한다.

HP를 위해 슬롯을 별도 재조회하지 않는다. 원본 entity 배열·공유 asset·적·상자 상태는 유지한다. Hardcore는 authored HP와 기존 timing을 사용한다. Stage gameplay 진입에는 유효한 campaign slot 또는 handoff가 필요하다. camera-only 구성에 save·handoff 소비를 추가하지 않는다.

기존 previous-active 보상과 matching cleanup은 유지한다. launch 전체를 마지막 훅으로 이동하거나 preset/objective/camera/host 전체를 새 transaction으로 감싸는 작업은 초기 범위에서 제외한다. 기존에 없는 bootstrap 전체 rollback 보장을 약속하지 않는다.

### 5.3 피해와 표현

기존 [PlayerDamageState.cs](../../Assets/_Features/Gameplay/Gameplay_PlayerControl/Runtime/PlayerDamageState.cs)의 tick 기반 피해 제한을 재사용한다.

- 기존 피해량과 deterministic 공격 순서를 유지한다.
- 생존 피격에 무적을 적용하고 기존 `nextDamageAllowedTick`의 경계를 검증한다.
- 낙사·압사 제거 경로는 일반 공격 무적으로 막지 않는다.
- 일시정지 중 simulation tick이 멈추면 보호 시간도 멈춘다.
- 깜빡임은 read-only 무적 상태를 읽는 View 표현이다. 사망·씬 교체 시 정리하며 renderer 상태로 피격 판정을 바꾸지 않는다.
- HP HUD와 Chance HUD는 다른 의미를 유지한다. 캐주얼에 ChanceLost 연출을 재사용하지 않는다.
- 첫 snapshot/HUD부터 올바른 HP를 표시한다. host 시작 후 UI가 HP를 보정하지 않는다.

## 6. 저장 흐름과 오류 처리

### 6.1 정상 저장

기존 `CampaignGameplayFlowController`, planner, typed committer, save service를 확장한다. 저장할 결과는 최종 tick에서 하나만 선택한다.

1. 사망이면 death 전환.
2. 생존한 clear이면 clear 전환.
3. 그 외 캐주얼 HP 변화는 survival 저장.
4. 변화가 없으면 저장하지 않는다.

death-over-clear와 terminal 중복은 기존 arbiter/token을 사용한다. 생존 HP에는 scene 수명 동안 마지막 처리 tick만 추가한다. 같은 결과를 다시 받아도 사망·Chance를 반복 적용하지 않는다.

기존 `ForceClearCurrentStage`는 TickResult 없이 terminal token으로 진행을 저장한다. 이 경로를 유지하고 가짜 tick이나 새 source-fact protocol을 만들지 않는다. 강제 clear·DirectPlay의 정상 완료/성능/업적 fact 제외 조건도 유지한다.

첫 구현은 동기 저장이다. P1의 생존 피격 저장은 매 tick 저장이 아니며, HP 3에서 clear/death 전 별도 회복이 없는 정상 흐름에서는 최대 두 번이다. 실제 IO 지연을 측정한 뒤 추가 최적화를 판단한다.

같은 profile root를 공유하는 동기 load→validate→mutate→persist는 작은 공유 operation gate로 직렬화하고 중첩 mutation을 거절한다. 기존 composition에서 연결하며 범용 queue·장기 pending owner·비동기 작업 스케줄러는 만들지 않는다. HUD 등 observer는 operation 밖에서 알리고, 테스트의 독립 backing store/root는 서로 차단하지 않는다.

`CommitDeath`·`CommitStageClear`는 유지·확장하고 좁은 `CommitSurvival`을 추가한다. 외부에서 조립한 전체 슬롯 replacement API는 만들지 않는다. 결과는 HP/Chance/변경 없음으로 구분하며 캐주얼에 가짜 PreviousRemainingChances를 채우지 않는다.

### 6.2 실패 시 현재 실행 종료

Flow가 필요한 상태는 저장 중, 저장 성공, 실행 종료 정도로 제한한다. 저장 실패를 같은 scene에서 재시도하는 상태 머신은 만들지 않는다.

| 상황 | 초기 구현 처리 |
| --- | --- |
| 저장 성공 | 성공을 먼저 기록한 뒤 HUD·업적·표현·route 처리 |
| 저장 실패 또는 결과 불명 | 추가 tick·입력·강제 clear 차단, 현재 실행 폐기, 오류 안내 |
| 메뉴에서 파일 다시 읽기 가능 | 기존 repository 복구·검증 후 진행 중 슬롯은 사용자의 기존 Continue, 완료 슬롯은 완료 카드 표시 |
| 파일 읽기도 실패 | 메뉴의 기존 저장 오류 UI로 접근 차단. 자동 초기화 없음 |
| 저장 뒤 scene 이동 실패 | 저장 명령 반복 금지. coordinator 인계 전이면 현재 실행 종료 후 메뉴 안내, 이미 인계됐으면 기존 transition 실패 처리 책임 유지 |
| 저장 뒤 observer 실패 | 저장 성공 유지. 저장 재실행이나 월드 rollback 금지 |

오류 팝업에는 **메인 메뉴/종료**만 제공한다. 전용 Reload/ManualRetry는 추가하지 않는다. 메뉴의 기존 Continue가 실제 파일을 읽어 새 실행을 시작하며 이전 death/clear/피격 명령을 다시 계산하거나 재적용하지 않는다. 저장 전 실패면 이전 파일에서 시작한다. 최종 완료가 저장됐지만 응답만 실패했다면 완료 카드로 충분하며, 오류 복구용 GameClear/outro 진입은 하지 않는다. 정상 성공 시 완료 흐름은 유지한다.

실행 중 backup 복구가 관측되면 이전 월드 결과를 복구 파일에 적용하지 않고 실행을 중지한다. service의 자체 load뿐 아니라 Flow/HUD의 선행 읽기도 포함한다. 구체적인 token 정리·파일별 수정·실패 사례는 [메뉴 복귀 수정안](./Campaign-Casual-Hardcore-Save-Error-Menu-Plan.md)을 따른다.

현재 실행의 callback/launch 소유권은 기존 token 정리 경로로 끝낸다. 복구를 진행하면서 이전 월드를 계속 사용하지 않는다. 별도 recovery generation은 추가하지 않는다. 앱 종료 후 미완료 명령을 추정하여 재생하지 않는다.

tick 차단은 기존 InputHost admission을 확장하며 한 frame의 catch-up loop 안에서도 다음 tick 전에 확인한다. 일반 pause 해제로 저장 오류의 차단이 풀리지 않도록 이유를 구분한다. 오류 안내·메뉴 복귀·종료는 가능해야 한다.

### 6.3 초기 보장 범위

- 일반 HP·death·clear는 기존 일반 `Save`와 파일별 atomic replacement를 사용한다.
- 새 게임·삭제·전체 초기화의 기존 `SaveDestructive` 사용은 유지한다.
- 사망 복귀마다 본 파일과 백업을 동시에 최신 상태로 만들지 않는다. 백업 복구로 일부 진행이 되돌아가는 기존 한계를 허용한다.
- 실패한 마지막 피격·사망까지 반드시 기록되거나 현재 보드가 그대로 재개된다는 보장은 하지 않는다.
- 기존 strict validation, unsupported-schema 거절, temp/rollback 복구, quarantine을 유지한다.
- 저수준 복원 발생을 모든 경로에서 추적하는 새 protocol은 추가하지 않는다. 기존에 보고되는 복구·오류에는 실행 종료로 대응하고, 숨겨진 복구까지 포함한 실시간 무효화 보장은 후속 범위다.
- 실제 파일 검사 없이 저장 성공으로 간주하거나 실패 시 자동 세이브 초기화를 하지 않는다.

## 7. UI·launch·주변 기능

- NewGame, 빈 슬롯 Continue, Overwrite, 완료 슬롯 Restart 등 **모든 신규 생성 경로**에 모드 선택을 연결한다.
- occupied slot의 Continue는 저장된 모드를 사용한다. 진행 중 모드 변경은 제공하지 않는다.
- pending/running context는 기존 슬롯 번호와 요청 token을 사용하고 검증한 mode를 연결한다. 기존 first-owner-wins, matching-token cleanup, active commit 책임을 유지한다.
- local-state의 활성 번호만으로 모드를 정하지 않는다. 새 launch마다 현재 profile을 검증한다.
- 캐주얼은 HP, 하드코어는 Chance를 표시한다. 인게임 HUD는 같은 3칸 Prefab 외형을 쓰되 캐주얼 HP를 Chance 연출·오디오로 전달하지 않는다. 메인 메뉴 슬롯 카드의 HP 문구는 유지한다. 두 모드의 실패 화면 제목은 기존의 짧은 `LevelFailedTitle`을 사용하고, 하드코어의 캠페인 재시작 버튼 문구와 실제 복귀 목적지는 유지한다.
- 두 모드의 정상 clear fact를 기존 공통 업적에 연결한다. 업적 저장 실패가 캠페인 저장을 되돌리지 않는다.
- 레벨 업적은 과거 기록으로 복구할 수 있으나 efficient clear의 제품 저장 실패는 새 qualifying clear가 필요하다는 기존 한계를 유지한다.
- 코믹 감상 이력은 사망 복귀 때 보존한다. 처음 stage로 돌아갔다고 intro를 강제로 다시 재생하지 않는다.
- DirectPlay/capture seed는 mode·유효 생존 값을 명시하고 기존 격리 root를 유지한다. 전시 참가자 reset은 기존 삭제 범위와 다른 파일 소유권을 유지한다.
- 신규 UI 문자열 구현 때 기존 4개 locale 및 font atlas 절차를 따른다.

## 8. 수정 후보와 구현 순서

### 8.1 주요 파일

아래는 기존 확장 지점이며 파일 전체 재작성을 뜻하지 않는다. Stages는 `Assets/_Features/Stages`, Gameplay는 `Assets/_Features/Gameplay`, UI는 `Assets/_Features/UI` 기준이다.

| 묶음 | 주요 수정 후보 | 범위 |
| --- | --- | --- |
| 슬롯/schema | Stages `CampaignSlotState.cs`, `CampaignSlotDocument.cs`, `CampaignProfileDocument.cs`, mapper/parser, `SaveSlotModels.cs` | mode·ResumeHp 및 clone/검증 |
| 정책/저장 | `StageRetryChanceTracker.cs`, `CampaignSlotTransitionEngine.cs`, `CampaignSaveSlotPolicy.cs`, `CampaignSavePorts.cs`, `CampaignSaveService.cs`, `CampaignSaveSlotStoreAdapter.cs`, `TransientCampaignState.cs` | 두 모드 전환·typed 결과·survival 저장 |
| composition/launch | `CampaignSaveServiceFactory.cs`, `CampaignSaveCompositionProvider.cs`, `PendingLaunchSlotProvider.cs`, `StageLaunchContextStore.cs`, `ActiveSlotStorage.cs` | 작은 동기 gate·저장된 mode 연결·기존 소유권 유지 |
| 초기화 | Gameplay `StageBackedGameplaySceneInstallerBase.cs`, `GameplayShowcaseSceneInstallerBase.cs`, `GameplaySceneHostConfiguration.cs` | HP 사본 준비와 작은 timing 후처리 |
| 실행/표현 | `CampaignGameplayFlowController.cs`, `GameplayHostPresentationFeed.cs`, `GameplayInputHost.cs`, 기존 Player View/HUD | 결과 선택·실패 종료·catch-up 차단·깜빡임 |
| UI | UI `MainMenuController.cs`, `MainMenuSlotViewModelMapper.cs`; Gameplay `GameplayPlayerHudReadModel.cs`, 관련 View | 생성 모드·HP/Chance·목적지 안내 |
| 주변 소비자 | seed·save readiness·업적·전시 reset | 새 필드 연결과 기존 범위 보존 |

기본 `StageRuntimeBuilder`, `TickPipeline`, 저수준 `AtomicTextFileStore`의 재설계를 선행 조건으로 삼지 않는다. 새로운 복잡한 클래스 이름을 먼저 정해 모두 구현하는 방식도 피한다.

### 8.2 단계

| 단계 | 작업 | 완료 기준 |
| --- | --- | --- |
| S0 | 관련 기존 테스트·현재 동작 확인 | baseline 실패와 변경 범위 분리 |
| S1 | mode·schema/seed·typed 결과 연결 | parser/round-trip, 기존 기록·다른 슬롯 보존; P4 반영 |
| S2 | 모드별 전환·초기 HP·무적·표현 | Casual/Hardcore 전환표와 camera-only 초기화 보존 검증 |
| S3 | ResumeHp·피격 저장, 공통 실패 종료 | HP 재진입, 중복 차단, 오류 뒤 추가 tick 없음 |
| S4 | 3슬롯 UI·terminal·주변 소비자 연결 | P3 반영, UI 및 Player 확인 |

확정된 P1/P4에 맞춰 저장 모델·loader·writer·seed를 함께 연결한다. 단계별로 컴파일 가능한 내부 변경을 만들고, loader/flow가 준비되지 않은 캐주얼 슬롯을 사용자 경로에 노출하지 않는다. 같은 최종 revision에서 변경 범위 검증을 마친 후 초기 기능 완료를 판단한다.

## 9. 기존 동작 보존과 테스트

### 9.1 보존 기준

| 대상 | 확인할 내용 |
| --- | --- |
| 하드코어 | HP·피해·timing, Chance 3→2→1, 같은/다음 레벨 및 최종 clear 유지. 마지막 소진 목적지만 전체 처음으로 변경 |
| 데이터 | 사망 복귀 때 기록·코믹·다른 슬롯 불변, 성공한 death 한 건의 TotalDeaths+1 |
| launch | 기존 active/pending/running 소유자, 실패 보상, 격리 root 유지 |
| Stage 진입 | 유효한 campaign slot 또는 handoff를 요구하며, 사망 뒤 같은 Host에서 player를 부활시키지 않음 |
| 표현 | 하드코어 Chance 연출 유지, 캐주얼 HP와 깜빡임 연결 |
| 파일 | 기존 검증·atomic writer·backup 정책 유지 |

기존 테스트 기대값은 의도한 변경만 이유를 기록하여 바꾼다. 무관한 assertion을 삭제하거나 전체 fixture를 약화하지 않는다.

### 9.2 초기 필수 검증

| 영역 | 기존 fixture 활용 | 핵심 사례 |
| --- | --- | --- |
| 모델/파일 | `CampaignSlotStateTests`, `CampaignSlotRawDataMapperTests`, `CampaignSaveArchitectureV2Tests` | mode/survival 오류 거절, 실제 JsonUtility round-trip, exact 기록 보존, 구버전 UnsupportedVersion 및 파일 불변 |
| 전환 | `CampaignSlotTransitionEngineTests`, `CampaignSlotTransitionCharacterizationTests` | 레벨 경계·최종 stage·Chance 3/2/1·Casual death/clear, Production/Transient parity |
| 초기화/launch | `StageBackedGameplaySceneInstallerTests`, `CampaignRunningSlotContextTests`, `CampaignLaunchHandoffPlayModeTests` | authored HP1→Casual3/저장HP, 첫 snapshot/HUD, preset 뒤 timing, 원본 불변, 기존 보상·요청 token 검증 |
| 피해 | `AttackPhaseScenarioTests`, `CleanupPhaseScenarioTests`, `WorldSnapshotAndPresentationTests` | 피해량 1/2/치명타, 무적 경계·다중 공격, 낙사/압사, death 우선 |
| flow/오류 | `CampaignStageFlowTests`, `CampaignChanceDisplayScopeTests`, 기존 service/factory 테스트 | 중복 결과·강제 clear, 실패 뒤 catch-up tick 차단, 성공 후 route/observer 실패 시 재저장 없음, 작은 gate의 중첩/공유 root 보호 |
| UI/주변 | `CampaignProductionEntryTests`, `CampaignMainMenuAndAutoNextTests`, `HudChanceInvalidationTests`, `CampaignSaveRepairStateTests`, 기존 업적/seed 테스트 | 혼합 3슬롯, 생성 경로 전체·취소·기존 확인창 callback 검증, HP/Chance, 업적 제외·복구 한계, root 격리 |

2026-09-25의 feature 단독 테스트 보강에서는 실제 installer→host 초기화→첫 WorldSnapshot/HUD query와 별도의 read model→UI source/mapper/presenter→authored HP 라벨 경계를 검사했다. 당시 Casual/Hardcore/비캠페인 초기화 사례가 있었으나 main 통합 뒤 stage gameplay는 slot 없는 비캠페인 진입을 거절한다. 현행 fixture는 Casual/Hardcore의 초기 HP·timing·원본 배열·공유 preset 보존을 검사하고, slot 없는 stage 진입의 거절 사례와 camera-only 초기화 사례는 별도로 유지한다. DestroyTile은 실제 이동 접촉과 topology 변경으로 점유 중 활성화되는 두 경로에 receiver cooldown을 결합하며, 실제 생존 공격으로 HP2와 무적이 생성된 다음 위험 타일에서 제거되는 사례를 포함한다. 이 검증을 실제 Player 화면이나 낙사·압사 전체의 증거로 확대하지 않는다. DestroyTile과 낙사의 authored 경로 대응, 플레이어 압사의 실제 생산 경로는 별도 확인이 남는다. 현재 확인된 Barricade/Jump crush는 Box 대상이다. 구체 실행 결과는 구현 보고서의 최신 테스트 보강 절을 따른다.

HP2에서 수동 재시작·메뉴·정상 재실행 유지, clear/death 후 HP3을 확인한다. 구버전 canonical이 있으면 유효한 새 버전 backup이 있어도 이를 덮어써 진행하지 않고, 빈 슬롯으로 표시하거나 자동 초기화하지 않는지 확인한다. 파일 실패는 기존 대역으로 다음 세 경우를 우선 검증한다.

1. 쓰기 전 실패: 오류 안내에서 메뉴로 이동, 기존 Continue로 이전 파일에서 새 실행. 이전 명령 재적용 없음.
2. 저장 후 응답 실패: 메뉴에서 실제 저장된 파일을 확인. 진행 중은 기존 Continue, 완료는 완료 카드. 중복 death/Chance 차감·자동 Restart 없음.
3. 지속 IO 실패: gameplay 정지, 메뉴의 기존 오류 UI로 접근 차단, 종료 가능. 실제 popup 닫힘과 종료 버튼 선택을 구분.

실제 round-trip은 격리된 test root에서 수행한다. Player에서는 캐주얼 피격·깜빡임·즉사·복귀, 하드코어 Chance/전체 복귀, 혼합 슬롯 Continue를 확인한다. 동기 IO 지연은 실제 환경에서 관측하고 fake 테스트만으로 성능을 판정하지 않는다.

### 9.3 실행과 결과 해석

구현 시 해당 worktree에서 실행할 예시:

```bash
./run_tests.sh core
./run_tests.sh ui
./run_tests.sh full --filter CampaignSlotTransitionCharacterizationTests
./run_tests.sh full --filter CampaignSaveArchitectureV2Tests
./run_tests.sh full --filter CampaignLaunchHandoffPlayModeTests
```

변경한 fixture를 추가로 선택하며 위 예시만으로 전체 coverage를 주장하지 않는다. 0 selected tests는 검증으로 인정하지 않는다. 순수 정책·runtime integration·wiring 검사를 구분하고, full baseline의 무관한 실패와 새 회귀를 별도로 보고한다.

Scene/Prefab/asset 변경은 목적과 필요한 Editor/Player 증거를 남긴다. evidence는 `/mnt/d/J2M/evidence`, build는 `/mnt/d/J2M/builds`에 둔다. 기존 mandatory lane 기준은 유지한다.

## 10. Schema 적용과 구버전 미지원

- 새 profile schema 3에 맞춰 parser·mapper·service·Transient·seed·진단·reader를 함께 연결한다.
- 기존 schema 2를 포함한 미지원 profile은 기존 UnsupportedVersion 경로로 차단한다. 모드 배정이나 migration을 추가하지 않는다.
- 미지원 파일은 그대로 두고 기존 저장 오류 UI로 안내한다. 빈 슬롯으로 표시하거나 backup으로 덮거나 자동 삭제·reset하지 않는다. 사용자가 직접 선택하는 기존 명시적 초기화 흐름은 그 확인·삭제 범위를 유지한다.
- local-state schema 1과 기존 활성 슬롯 선택을 유지한다. profile/local 두 파일의 원자적 변경은 추가하지 않는다.
- 실제 구현 시 baseline policy·death lifecycle·launch/readiness 문서를 갱신한다. 지금 canonical 현재 계약을 미래 상태로 덮어쓰지 않는다.
- P1~P4의 추가 확인은 필요 없다. 초기 무적 2초는 플레이하면서 조정하고, 두 모드의 기존 공통 업적 허용을 구현한다.

## 11. 초기 기능 이후의 보장 강화

다음은 **초기 구현과 완료 조건에서 제외**한다. 기능이 동작한 뒤 실제 문제·요구를 확인하여 필요한 항목만 다시 설계한다.

| 이전 설계 항목 | 현재 처리 |
| --- | --- |
| SlotInstanceId 생성·저장·전달 및 슬롯 재생성 검증 | 초기 구현에서 제외. 기존 슬롯 번호·요청 취소·token 처리 유지 |
| 영속 Revision·OperationId·LastMutationReceipt·payload/result fingerprint | schema에서 제외 |
| 결과 불명 후 같은 scene·같은 명령 재시도 | 실행 폐기 후 재진입으로 대체 |
| 장기 pending owner·일반 command queue·비동기 저장 | 작은 동기 operation gate로 시작 |
| 독립 recovery generation·저수준 복원 전 경로 추적 | 기존 token 정리와 보고된 오류 처리로 시작 |
| death 복귀의 canonical/backup 동시 최신화 | 기존 일반 Save 유지 |
| launch 전체 이동·bootstrap 전체 rollback | 기존 transaction에 HP 준비와 timing 훅만 추가 |
| receipt/hash 직렬화·두 파일 중간 강제 종료 등의 신규 강화 실험 | 해당 강화안을 실제 채택할 때 추가 |

앞선 검토의 기본 동작은 유지한다: 기존 요청 취소·token 검증, 최종 tick 결과 하나만 저장, 실패 뒤 진행 중지, 강제 clear 정상 fact 제외, 기존 업적 한계 명시. 슬롯 재생성 ID와 영수증·재시도 protocol을 전제로 했던 추가 요구는 초기 필수 목록에서 제거했다.

중간 보드 snapshot, cross-process writer, Cloud 충돌 처리, 파일 조작 방지, 실패한 사건의 무손실 기록은 초기 보장 범위가 아니다. 기능 구현 후 이런 요구가 생기면 현재의 제한과 필요한 비용을 함께 검토한다.

## 12. 설계 문서 작성 당시 검증

설계 문서 작성 당시 문서 구조·상대 링크·파일/fixture 이름·index 연결과 공백 검사를 확인했다. 당시 코드·에셋·테스트·세이브 파일은 변경하지 않았으며 Unity core/ui/full, Player 실행, fault injection은 실행하지 않았다. 이후 구현 작업과 검증은 아래 작업 기록과 실행 보고서를 따른다.

위 테스트는 구현 단계의 계획이며 통과 결과가 아니다. 현재 문서는 이전의 확대된 저장 프로토콜안을 대체하는 초기 구현 기준이다.

## 13. 초기 구현 작업 기록

- P1~P4는 확정대로 적용한다. 신규 생성 공통 모드 선택창은 기존 Confirm popup의 두 버튼과 Back/바깥 클릭 취소를 사용한다. occupied Continue는 모드 선택을 열지 않는다. New Game 클릭은 팝업 열림 소리만 내고, 두 모드 버튼 선택에서 `StageLaunch`를 한 번 재생한다. 덮어쓰기·완료 슬롯 Restart는 후속 확인을 취소해도 모드 선택 소리가 이미 재생된다는 초기 UI 정책을 허용한다.
- Casual HUD는 기존 Prefab의 3칸 아이콘에 HP를 표시하고 인게임 HP 텍스트를 두지 않는다. Hardcore의 Chance 표시·연출·오디오는 유지한다. 깜빡임은 read-only PlayerDamageState의 무적 구간을 읽는다.
- 2026-09-25 수정으로 전용 Reload를 제거했다. 저장 오류는 메뉴/종료로 안내하고 진행 중 슬롯은 기존 Continue, 완료 슬롯은 완료 카드로 처리한다. Claimed token 정리와 복구 관측 중 gameplay mutation 차단을 추가했다. [구체 수정안](./Campaign-Casual-Hardcore-Save-Error-Menu-Plan.md)의 최종 검증 상태는 실행 보고서를 따른다. 일반 pause 해제는 실행 폐기 latch를 해제하지 않는다.
- Scene/shared timing asset 수정은 없다. `GameplayHudRoot.prefab`의 비활성 HP TMP 라벨을 제거하고 기존 3칸 아이콘을 캐주얼에도 표시한다. 목적은 HP와 Chance의 외형을 공유하면서 의미와 오디오를 분리하는 것이다. 신규 runtime MonoBehaviour 메타가 추가됐다. 승인된 번역은 `../Localization/Campaign-Modes-Translation-Review.md`에 있다. 2026-09-25 네 언어 UI String Table과 한국어·일본어·중국어 폰트 6개의 Static atlas에 적용했다. 영어 atlas는 이미 필요한 글자를 포함하여 변경되지 않았다.
- 실행 evidence: `/mnt/d/J2M/evidence/campaign-modes-implementation`. 최종 검증 결과와 미검증 범위는 [실행 보고서](./Campaign-Casual-Hardcore-Implementation-Report.md)에 기록한다. 실제 Player 검증이 남아 있어 구현 완료 상태가 아니다.
