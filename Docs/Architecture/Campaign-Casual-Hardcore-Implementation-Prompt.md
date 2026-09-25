# Campaign Casual / Hardcore — 초기 구현 실행 프롬프트

- 작성일: 2026-09-24
- 상태: 실행용 지시문. 이 문서 작성으로 기능 구현이나 테스트가 실행된 것은 아니다.
- 설계 기준: [Campaign-Casual-Hardcore-Mode-Design.md](./Campaign-Casual-Hardcore-Mode-Design.md)

아래 지시를 따라 초기 기능을 구현하라. 목표는 두 모드가 기존 구조 안에서 동작하도록 만드는 것이다. 복잡한 보장 강화는 초기 기능이 완성된 뒤 별도 요구에 따라 진행한다.

## 1. 작업 시작

1. 현재 worktree의 `git status --short --branch`, `git diff --stat`를 확인하고 기존 변경을 읽어 보존하라.
2. `AGENTS.md`, `Docs/Architecture/README.md`, `Docs/Testing/Gameplay-Test-Automation-Guide.md`, `AI_GIT_COMMIT_RULES.md`와 위 설계 문서를 읽어라.
3. 설계에 적힌 조사 revision과 현재 HEAD가 다르면 관련 소스를 대조하라. 오래된 API 설명을 그대로 구현하지 마라.
4. 초기 작업은 현재 worktree에서 수행하라. 새 worktree가 실제 필요하면 저장 정책과 `j2m-worktree-add` 규칙을 따르라.
5. 아래 확정된 P1~P4를 적용하고 소스 조사와 기존 테스트 기준 확보를 진행하라. 같은 결정을 다시 질문하지 마라.

이미 정해진 규칙을 다시 질문하거나 제외된 보장을 구현의 선행 조건으로 만들지 마라. 실제 코드·테스트·필요한 UI 연결까지 진행하고, 계획 작성만으로 작업을 완료했다고 보고하지 마라. 별도 요청 없는 commit·push·PR 생성은 범위에 포함하지 않는다.

## 2. 확정 요구

### 캐주얼

- 최대 HP 3, 기존 공격 피해량만큼 감소.
- 생존 피격 후 일정 시간 무적과 깜빡임.
- 낙사·압사는 무적 여부와 관계없이 즉사.
- 스테이지 클리어 시 HP 회복.
- 사망하면 현재 레벨 묶음의 첫 스테이지로 복귀.
- 재도전 무제한이며 Chance를 사용하지 않음.

### 하드코어

- 기존 HP·피해·timing·Chance 연출 유지.
- Chance가 남으면 기존처럼 감소 후 같은 스테이지 재도전.
- 마지막 Chance 소진 시 캠페인 전체 첫 스테이지로 복귀하고 Chance 3.
- 같은 레벨 clear의 Chance 유지, 다음 레벨 진입 시 3 회복, 최종 clear의 기존 처리 유지.

### 공통

- 기존 슬롯 번호 1~3을 두 모드가 공유하며, 생성 시 모드를 고정한다.
- occupied slot의 Continue는 저장된 모드를 사용한다.
- 사망 복귀 때 과거 기록·누적 사망·코믹 감상 이력·다른 슬롯을 보존한다. 정상 저장된 사망 한 건마다 TotalDeaths를 1 증가시킨다.
- 사망 복귀와 새 게임 생성·삭제·덮어쓰기를 구분한다.
- 복귀 목적지는 sequence에서 구한다. 현재 전체 첫 항목은 `level-0 / stage-0-1`이며 이름을 하드코딩하지 않는다.

## 3. 확정된 추가 규칙 — P1~P4

아래 항목은 사용자가 모두 확정했다. 이를 미확정 선택이나 실행 전 승인 단계로 되돌리지 마라. 이후 사용자가 변경을 명시하면 최신 지시를 반영하라.

| 항목 | 확정 내용 | 영향 |
| --- | --- | --- |
| P1 | 최초 시작·사망 후 HP 3, 수동 재시작·메뉴·재실행에는 남은 HP 유지 | ResumeHp와 피격 저장, 초기화·재진입 테스트 |
| P2 | 피격 무적은 우선 2초, 플레이하면서 조정 | 초기 설정 2초와 실제 플레이 확인 |
| P3 | 두 모드 모두 기존 공통 업적 인정 | 정상 clear의 업적 fact 연결 |
| P4 | 기존 세이브 형식 미허용, 기존 구버전 미지원 처리와 동일하게 차단 | UnsupportedVersion, 자동 변환·초기화 없음 |

기존 schema 2를 포함한 미지원 profile은 파일을 보존한 채 기존 오류 UI로 안내하라. 빈 슬롯으로 간주하거나 backup으로 덮거나 자동 변환·모드 배정·삭제하지 마라. 기존 명시적 초기화 흐름은 사용자의 해당 선택과 확인을 거쳐 동작하도록 유지하라.

## 4. 초기 구현에서 제외할 것

- `SlotInstanceId` 및 같은 목적의 별도 슬롯 재생성 ID.
- 새 영속 Revision·OperationId·LastMutationReceipt·payload/result fingerprint.
- 결과 불명 후 같은 scene에서 동일 저장 명령을 재시도하는 protocol.
- 별도 recovery generation, 장기 pending owner, 범용 명령 queue, 비동기 저장 framework.
- 사망 복귀마다 canonical과 backup을 동시에 최신화하는 강화.
- launch 전체 이동, bootstrap 전체 rollback, 저수준 파일 writer 재설계.
- cross-process writer, Cloud 충돌 처리, 중간 보드 snapshot, 강제 종료 시 무손실 보장.

기존 request/terminal token, confirmation generation, 완료 증빙, processed-ID 필드는 보존하라. 위 제외 사항을 다른 이름으로 다시 도입하지 마라. 필요한 작은 함수나 데이터 모델의 추가는 가능하지만 새 framework를 먼저 만들지 마라.

## 5. 구현 순서

### S0. 현재 기준 확보

- 관련 코드와 기존 테스트를 읽고 실제 확장 지점을 확인하라.
- 변경 범위에 해당하는 기존 테스트 결과를 확보하고 알려진 baseline 실패와 구분하라.
- `WorldState` 소유권, Finalize/batch 쓰기, Production/Transient 공통 transition engine, typed save port를 유지하라.

### S1. 슬롯·저장 모델

- 기존 state/parser/mapper/document/clone에 `GameMode`를 연결하라.
- `ResumeHp`를 추가하고, mode별 생존 값 검증을 적용하라.
- 새 profile schema는 3으로 구현하고 구버전은 UnsupportedVersion으로 처리하라. local-state는 schema 1을 유지하라.
- 캐주얼의 inactive Chance 0은 새 파일 형식의 예약값으로만 취급하라. 현재 schema 2의 검증을 완화하거나 UI에 무한 Chance로 노출하지 마라.
- strict validation·exact clone·기존 완료 증빙과 기록을 보존하라.
- service·Transient·seed·reader·진단 소비자를 함께 연결하고, 준비되지 않은 캐주얼 슬롯이 실제 사용자 경로로 노출되지 않게 하라.

### S2. 진행 규칙·초기 HP·무적

- `StageRetryChanceTracker`와 `CampaignSlotTransitionEngine`에 모드별 사망·클리어 규칙을 추가하라.
- `StageBackedGameplaySceneInstallerBase`의 기존 launch transaction이 검증한 slot으로 초기 HP를 준비하라. HP용 별도 재조회는 피하라.
- 초기 entity 배열의 사본에서 캐주얼 Player만 변경하라. 새 준비 검증은 active write 이전 또는 기존 보상 범위 안에 두어라.
- 기존 preset 순서를 유지하고, preset 이후 작은 훅에서 캐주얼 cooldown만 적용하라. 기존 timing resolve를 재사용하라.
- 기존 `PlayerDamageState`와 authoritative 피해 경계를 재사용하고, 깜빡임은 View가 read-only 상태를 읽어 표현하게 하라.
- Hardcore·비캠페인·camera-only의 기존 동작과 shared asset을 보존하라.

### S3. 동기 저장·실패 종료

- 기존 Flow/committer/service를 확장하라. 한 tick에서는 death → 생존 clear → HP 변화 순서로 결과 하나만 저장하라.
- 좁은 `CommitSurvival`을 추가하라. 전체 슬롯 replacement API는 추가하지 마라.
- 기존 terminal arbiter/token을 사용하고, 생존 피격에 필요한 마지막 처리 tick만 관리하라. TickResult 없는 강제 clear도 기존 방식으로 처리하라.
- 같은 root의 동기 load→validate→mutate→persist는 작은 공유 operation gate로 직렬화하고 중첩 mutation을 거절하라. observer는 operation 밖에서 알리도록 하라.
- 일반 HP·death·clear는 기존 일반 Save를 사용하라. 기존 생성·삭제·전체 초기화의 SaveDestructive 사용을 유지하라.
- 저장 성공을 먼저 기록한 뒤 표현·업적·route를 처리하라. observer/scene 이동 실패를 저장 재실행으로 처리하지 마라.
- 저장 실패 또는 결과 불명이면 추가 tick·입력·강제 clear를 막고 현재 실행을 폐기하라. 파일 재로드·기존 복구 후 진행 중 슬롯은 새 launch, 완료 슬롯은 기존 완료 흐름으로 연결하라.
- 재시도는 파일 재로드·재진입을 뜻한다. 이전 death/clear/피격 명령을 다시 적용하지 마라.
- 기존 InputHost의 catch-up loop에서도 매 tick 진입 전에 gate를 확인하라. 일반 pause 해제로 저장 오류 차단이 풀리지 않게 하고 오류 안내·종료는 가능하게 하라.

### S4. UI·주변 소비자

- NewGame·빈 슬롯 Continue·Overwrite·완료 슬롯 Restart 등 모든 신규 생성 경로에 모드 선택을 연결하라.
- 캐주얼 HP와 하드코어 Chance의 read model·HUD·연출을 구분하라. 복귀 안내와 실제 목적지를 일치시켜라.
- 기존 슬롯 번호·요청 취소·확인창 token 처리를 유지하라.
- 두 모드의 정상 clear를 기존 공통 업적에 연결하고, forced clear/DirectPlay의 정상 fact 제외와 기존 업적 복구 한계를 유지하라.
- 코믹 감상 이력, DirectPlay/capture root 격리, 전시 reset의 기존 삭제 범위를 보존하라.
- 실제 localization 변경에는 `j2m-localization-atlas` skill과 기존 4개 locale/atlas 절차를 적용하라.

## 6. 검증

새 동작과 실패 위험을 확인하는 테스트를 기존 fixture에 추가하라. 단순한 내부 필드 배치를 그대로 검증하는 테스트나 제외된 보장 전용 테스트를 늘리지 마라.

필수 확인:

1. mode/survival 검증, 실제 JsonUtility round-trip, 기록·다른 슬롯 보존. 구버전 canonical은 UnsupportedVersion으로 차단하고 파일·backup 불변, 빈 슬롯 표시·자동 변환·초기화 없음.
2. 두 모드의 사망·clear·레벨 경계·최종 stage와 Chance 3/2/1 전환.
3. 첫 snapshot/HUD의 HP, preset 이후 무적 설정, 원본 배열 및 Hardcore/비캠페인 불변.
4. 피해량 1/2/치명타, 무적 경계·다중 공격, 낙사/압사, death-over-clear.
5. 중복 결과·강제 clear, 저장 실패 후 catch-up 차단, 저장 후 observer/route 실패 시 재저장 없음.
6. 쓰기 전 실패·저장 후 응답 실패·지속 IO 실패에서 이전 명령을 재적용하지 않고 파일 기반으로 재진입하거나 오류 안내.
7. 혼합 모드 3슬롯 생성·취소·Continue와 HP/Chance 표현. HP2에서 수동 재시작·메뉴·정상 재실행 유지 및 clear/death 후 HP3. 두 모드의 기존 공통 업적 인정.

해당 worktree에서 repository runner로 실행하라.

```bash
./run_tests.sh core
./run_tests.sh ui
./run_tests.sh full --filter CampaignSlotTransitionCharacterizationTests
./run_tests.sh full --filter CampaignSaveArchitectureV2Tests
./run_tests.sh full --filter CampaignLaunchHandoffPlayModeTests
```

위 filtered 명령은 예시다. 실제 수정한 fixture와 설계 9장의 필요한 검증을 선택하라. 0 selected tests는 통과 증거가 아니다. 같은 최종 revision의 결과를 보고하고 unrelated full baseline 실패와 변경 범위 회귀를 분리하라.

격리된 test root를 사용하고 실제 사용자 세이브를 실패 실험 대상으로 삼지 마라. evidence는 `/mnt/d/J2M/evidence`, build는 `/mnt/d/J2M/builds`에 둬라.

실제 Player에서 캐주얼 피격·깜빡임·즉사·복귀, 하드코어 Chance·전체 복귀, 혼합 슬롯 Continue를 확인하라. Scene/Prefab/asset 수정 목적과 필요한 Editor/Player 검증을 기록하라. 실행할 수 없는 검증은 이유와 남은 확인 사항을 명시하라.

## 7. 완료 기준과 보고

- 확정된 두 모드 규칙이 실제 생성→플레이→저장→복귀 경로에 연결되어 있다.
- 관련 기존 동작과 데이터 보존을 검증했고, 의도한 정책 변경만 테스트 기대값에 반영했다.
- 제외한 ID·영속 영수증·해시·복잡한 재시도 구조가 추가되지 않았다.
- 필요한 제품 결정과 구현 상태를 설계 문서에 반영하고 실제 schema 적용 시 canonical 저장 문서를 갱신했다.
- 실행한 테스트·미실행 테스트·Player 확인·남은 문제를 구분하여 보고했다.

최종 보고에는 변경 파일과 핵심 동작, 검증 명령/결과, 미검증 항목, 저장 실패 시 최신 미저장 결과가 유실될 수 있는 초기 보장 한계를 간결하게 적어라. 기능이 미완료이거나 필수 검증이 남으면 그 상태를 완료로 포장하지 마라. 실행하지 않은 full/broad lane의 통과를 주장하지 마라.
