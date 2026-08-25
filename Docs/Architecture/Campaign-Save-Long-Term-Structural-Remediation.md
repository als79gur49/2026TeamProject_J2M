# Campaign Save 장기 구조 개선 계획

- Status: completed execution record
- Reviewed: 2026-08-25 KST
- Scope: campaign slot runtime state, transition ownership, validation, mapping, Production/Transient parity
- Implementation status: Phases 0-5 and the final same-revision Goal audit are complete

## 1. 문서의 위치와 목적

이 문서는 현재 동작을 새로 정의하는 runtime truth-source가 아니다. 현재 저장 계약은
[Architecture README](./README.md)의 `Stage clear save/profile boundary`와
[Pre-Release Save Baseline Policy](./Pre-Release-Save-Baseline-Policy.md)를 따른다.

이 문서의 목적은 최근 수정에서 동일 계열 문제가 반복된 원인을 구조 수준에서 고정하고,
후속 작업자가 국소 패치를 반복하지 않도록 단계별 장기 개선 경로와 완료 조건을 제공하는 것이다.

> Closeout note: 이 문서의 초기 진단, 당시 `current` inventory, 단계별 future-tense 지시는
> 2026-08-24~25 실행 순서를 보존하는 historical provenance다. 완료 후 current runtime truth는
> [Architecture README](./README.md)의 `Stage clear save/profile boundary`와
> [Pre-Release Save Baseline Policy](./Pre-Release-Save-Baseline-Policy.md)가 우선하며, 아래 각
> implementation-result/ledger row가 이전 단계의 transitional wording을 supersede한다.

Goal 시작 당시의 historical 진단은 다음과 같다.

> 현재 구현은 저장 경계의 fail-closed 검증과 exact clone을 통해 안전성을 회복했지만,
> 하나의 가변 `SaveSlotData`가 여러 상태를 동시에 표현하고 Production과 Transient가
> 전이 적용 규칙을 각각 구현한다. 따라서 수정할 때마다 누락·중복·정규화 시점 불일치가
> 다시 생기기 쉬운 구조다.

DirectPlay 자체나 `SaveSlotData.Clone()` 하나가 근본 원인은 아니다. DirectPlay는 추가 진입
경로를 통해 기존 불일치를 드러냈고, exact clone은 현재 구조에서 원본 손상을 막는 안전장치다.

### Historical Goal 실행 계약

실제 Goal 시작에 사용한 입력은
[Campaign-Save-Long-Term-Structural-Remediation-Goal-Prompt.md](./Campaign-Save-Long-Term-Structural-Remediation-Goal-Prompt.md)를
사용한다.

이 문서로 진행할 장기 Goal의 objective는 다음과 같이 고정한다.

> schema 2의 현재 외부 동작을 유지하면서 campaign slot의 authoritative transition owner를
> 하나로 만들고, raw/empty/corrupt/canonical state를 타입과 경계로 분리하여 국소 수정이
> 다른 composition의 계약을 다시 깨뜨리지 않게 한다.

Phase는 반드시 순서대로 진행한다. 뒤 Phase가 쉬워 보여도 앞 Phase의 same-revision exit
evidence 없이 시작하지 않는다.

| Phase | 현재 readiness | 시작 조건 |
| --- | --- | --- |
| 0. 계약 고정 | Complete | 0A/0B/0C same-revision exit evidence green |
| 1. transition engine | Complete — 1A/1B/1C same-revision exit green | characterization matrix green |
| 2. mapper/normalization 경계 | Complete — 2A/2B/2C same-revision exit green | engine single-owner gate green |
| 3. immutable state | Complete — 3A/3B/3C same-revision exit green | normalization owner와 public mapper surface 고정 |
| 4. validation/eligibility/maintenance | Complete — retired-save cleanup, 4A, 4B, 4C same-revision exit green | immutable state consumer adapter 사용 가능 |
| 5. shim 제거 | Complete — 5A/5B/5C same-revision exit green | Phase 4 runtime/UI consumer migration 및 core/UI lane green |

각 Goal turn은 아래 규칙을 따른다.

1. 한 turn/change set에는 한 Phase의 한 work package만 넣는다.
2. 시작 전에 working tree와 해당 파일의 기존 diff를 확인하고 사용자 변경을 보존한다.
3. 기존 evidence는 entry 기준을 설명할 뿐 새 revision의 pass를 대신하지 않는다.
4. exit criteria 중 하나라도 충족하지 못하면 다음 Phase로 넘어가지 않는다.
5. schema, public save compatibility, PlayerPrefs 범위를 넓혀야 하면 Goal을 중단하고 별도 결정을 받는다.
6. Phase 종료 시 이 문서의 실행 기록에 revision, tests run/not run, 잔여 위험을 남긴다.

문서 작성 자체는 Phase 완료가 아니다. 아래 readiness 표의 상태는 구현이 진행될 때만 바꾼다.

## 2. 이번 재검토에서 확인한 현재 안전 기준

장기 개선은 아래 계약을 회귀시키지 않아야 한다.

- Campaign progression의 Production 저장 진실은 `Saves/profile.json` 하나다.
- Campaign progression과 active slot에 PlayerPrefs fallback, import, delete 경로를 다시 만들지 않는다.
- Production과 Campaign DirectPlay는 JSON-backed composition을 사용한다.
- `RemainingChances`의 저장·런타임 범위는 항상 `1..3`이다. `0` sentinel은 없다.
- 마지막 목숨 소진은 `0`을 중간 저장하지 않고 level-group 첫 stage와 기본 목숨 `3`을
  한 transaction으로 commit한다.
- `SaveSlotData.Clone()`과 nested `Clone()`은 null, invalid value, null element, duplicate,
  collection order를 바꾸지 않는 exact deep copy다.
- raw slot은 normalization 전에 `Invalid`, `Empty`, `ValidNonEmpty`로 분류한다.
- malformed nested state는 조용히 제거하거나 clamp하지 않고 fail-closed한다.
- persistence canonicalization은 검증을 통과한 slot에만 적용한다.
- blocked profile의 일반 query와 모든 mutation은 fail-closed하며 repository write를 하지 않는다.
- processed-ID field는 현재 active idempotency 의미가 없어도 schema 2 round-trip에서 보존한다.
- `CampaignProgressionTransitionPlanner`는 route와 전이 의도를 계획하며 저장을 직접 변경하지 않는다.

### 목숨 상한에 대한 명시적 판단

초기 목숨이 `3`이므로 상한도 `3`인 것은 현재 계약에서 안전하다. 현재 death/clear 흐름에는
목숨을 누적 증가시키는 연산이 없으며, level group 전환이나 마지막 목숨 소진 때 `3`으로
복원할 뿐이다. 따라서 현 시점에는 `3`을 넘을 정상 경로가 없다.

향후 보상 등으로 목숨 증가 기능을 추가한다면 범용 `+1`이나 clamp를 사용하지 말고,
별도 typed command와 정책 결정을 추가해야 한다. 정책이 최대 `3`이면 이미 `3`인 상태의
증가 요청은 명시적 no-op/result가 되어야 하고, 최대치 확장을 결정한다면 schema/runtime/UI
계약을 같은 변경에서 수정해야 한다. invalid 값을 저장 직전에 clamp하는 방식은 데이터 오류를
숨기므로 허용하지 않는다.

## 3. 코드 근거와 구조적 결함

### 3.1 하나의 가변 타입이 서로 다른 상태를 모두 표현한다

[`SaveSlotData`](../../Assets/_Features/Stages/Runtime/Campaign/SaveSlotModels.cs)는 public setter를
가진 하나의 carrier로 다음 상태를 모두 표현한다.

- 저장 문서에서 읽은 값
- 아직 검증하지 않은 raw 값
- 빈 슬롯
- 유효하지만 아직 canonical하지 않은 값
- canonical한 runtime 값
- UI query 결과
- corruption 진단용 보존 값
- mutation 도중의 중간 값
- test fixture

이 타입만 보고는 값이 어느 단계에 있는지 알 수 없다. 예를 들어
`ReplaceValidatedSlot(SaveSlotData)`는 이름으로만 validated 상태를 주장하며 컴파일러는 raw
또는 invalid `SaveSlotData` 전달을 막지 못한다.

2026-08-24 기준 단순 source scan에서 `SaveSlotData`는 `Assets` 아래 C# 46개 파일, 326개
텍스트 참조에 걸쳐 있다. 이 수치는 품질 지표나 고정 gate가 아니라, 한 번에 타입을 교체하면
위험한 넓은 변경이 된다는 참고치다.

이 구조 때문에 호출자마다 다음 방어가 반복된다.

- clone해야 하는가
- validation을 먼저 해야 하는가
- null collection을 허용하는가
- empty와 corrupted를 어떻게 구분하는가
- canonicalization을 어디서 수행하는가

### 3.2 전이 계획은 공유하지만 전이 적용은 중복되어 있다

현재 [`CampaignProgressionTransitionPlanner`](../../Assets/_Features/Stages/Runtime/Campaign/StageRetryChanceTracker.cs)는
death와 stage clear의 route를 순수하게 계획한다. 그러나 계획을 실제 slot에 적용하는 코드는
다음 두 구현에 별도로 존재한다.

- [`CampaignSaveService.CommitDeath/CommitStageClear`](../../Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSaveService.cs)
- [`TransientCampaignSaveSlotStore.CommitDeath/CommitStageClear`](../../Assets/_Features/Stages/Runtime/Campaign/TransientCampaignState.cs)

두 구현은 각각 아래 규칙을 재구현한다.

- request/plan 유효성 확인
- current stage/chance precondition 재확인
- stage, level group, chances 변경
- death count 변경
- completion receipt 최초 기록
- performance best-value upsert
- timestamp 갱신

현재 parity test가 대표 결과를 비교하지만, 새 필드나 새 분기를 한쪽에만 추가하는 것을 구조적으로
금지하지는 않는다. 테스트 case가 빠지면 두 구현은 다시 달라질 수 있다.

### 3.3 mapper가 strict persistence와 tolerant projection 역할을 함께 가진다

[`CampaignProfileDocumentMapper`](../../Assets/_Features/Stages/Runtime/Campaign/Save/CampaignProfileDocumentMapper.cs)의
profile/slot 경로는 저장 전에 canonicalizer와 document validator를 거치므로 현재 active 저장
경로는 fail-closed한다. 그러나 공개된 fragment mapper에는 다음과 같은 tolerant 동작이 남아 있다.

- invalid performance document 건너뛰기
- null clear record 건너뛰기
- null collection을 empty collection으로 materialize
- 일부 counter를 non-negative 값으로 보정

현재 repository load는 top-level document validator를 먼저 통과하므로 이 tolerant 구현이 active
profile 손상을 조용히 고치는 경로는 아니다. 문제는 향후 다른 호출자가 public fragment mapper를
직접 사용하면 strict persistence contract를 우회할 수 있다는 점이다.

또한 Production write는 validated canonicalizer에서 performance를 canonicalize한 뒤 mapper에서
다시 `Normalize`한다. 현재 결과는 동일하지만 normalization owner가 둘이라 정책 drift 위험이 있다.

### 3.4 최초 진단 당시 검증, 실행 가능성, 호환 보정, 저장 동기화가 한 service에 섞여 있었다

당시 `SaveSlotValidationService`는 한 흐름에서 다음 책임을 처리했다. 이 source와 combined result/status는
Phase 4C에서 제거되었으며 아래 목록은 문제 진단을 보존하는 historical inventory다.

- runtime slot shape 분류
- empty/corrupted 판정
- stage sequence 존재 여부
- stage catalog 존재 여부
- retired completed stage 보정 (Phase 4 entry cleanup 전 상태; 현재 제거됨)
- level group 재계산
- save sync 필요 여부 결정 및 replacement 실행

이 결과도 `Invalid`, `Empty`, `Valid`, `Completed` 모두 같은 `SaveSlotData`를 운반한다.
구조 유효성과 “현재 catalog로 실행 가능한가”는 서로 다른 질문인데 한 결과에 결합되어 있어,
후속 변경이 validation인지 migration인지 launch policy인지 모호해진다.

### 3.5 DirectPlay는 근본 원인이 아니다

Production과 Campaign DirectPlay는 현재 동일한 JSON-backed save composition을 사용하며 chance
범위도 `1..3`으로 검증한다. `TransientCampaignSaveSlotStore`는 Production DirectPlay 저장소가
아니라 test와 short-lived diagnostic composition이다.

따라서 과거 sentinel 제거 후 DirectPlay에서 문제가 보였던 것은 DirectPlay에 별도 sentinel
정책이 남아서라기보다, 여러 composition과 bootstrap에서 동일 계약을 각자 검증·적용하던 기존
구조가 노출된 것이다. DirectPlay만 수정하면 Production/Transient 전이 중복과 type-state 문제는
남는다.

### 3.6 심화 재검토로 보강된 판단

Goal 실행 전 코드 소비자와 test를 다시 대조한 결과, 초기 계획에 다음 보강이 필요하다.

- 기존 parity fixture에는 death의 `3 -> 2`, last-chance `1 -> 3`은 있지만 `2 -> 1`과
  complete stage-clear parity matrix가 없다. Phase 0에서 먼저 채워야 한다.
- `CampaignSaveService`와 Transient는 failure surface가 다르다. Production service는 status를
  반환하고 adapter가 예외로 바꾸지만 Transient는 직접 예외를 던진다. 공통 engine이 저장소별
  예외 정책까지 소유하면 다시 결합되므로 typed failure를 반환하고 adapter가 현재 surface로
  변환해야 한다.
- public fragment mapper의 runtime production caller는 현재 complete slot/profile 경로 외에는
  없고 나머지는 주로 test다. 따라서 public surface 축소는 비교적 낮은 위험으로 먼저 할 수 있다.
- `NormalStagePerformanceRecordPolicy.Normalize`는 achievement projection도 사용한다. persistence
  normalization을 제거할 때 achievement의 tolerant read를 같은 함수에 남기면 owner가 다시
  섞이므로 목적이 드러나는 별도 projection으로 분리해야 한다.
- `SaveSlotValidationStatus.UnsupportedVersion`은 per-slot `Validate`에서 생성되지 않고 profile
  repository load report가 소유하는 상태다. slot 결과에 남아 있는 것은 responsibility leakage다.
- `ValidateAndSync` 직접 호출은 현재 test뿐이지만 MainMenu Continue는 `RequiresSaveSync`를 보고
  full `ReplaceValidatedSlot`을 호출한다. 이 full replacement는 validation 이후 slot 전체를
  다시 써서 stale data를 덮을 수 있으므로 immutable parameter로만 바꾸는 것으로 충분하지 않다.
- `ICampaignSlotMaintenancePort`의 실제 목적은 MainMenu level-group sync, editor/seed bootstrap,
  test setup으로 서로 다르다. 장기적으로 일반 replacement를 없애고 목적별 command/factory로
  나누는 편이 안전하다.
- comic completion의 `stageId`는 현재 Production에서 persisted stage가 비어 있을 때만 사용되고
  Transient에서는 유효성만 검사한 뒤 실제 current stage와 비교하지 않는다. valid occupied document는
  stage가 비어 있을 수 없으므로 Production의 보정 branch는 사실상 도달 불가능하다. Phase 0에서
  현재 호출 계약을 고정한 뒤, Phase 3 typed command에서 expected-stage precondition으로 사용할지
  불필요한 parameter로 제거할지 결정해야 한다.
- Phase 4 entry cleanup 전 `RetiredCampaignSaveCompatibilityPolicy`는 `stage-5-1`을 current final
  stage로 보정했지만 공개 배포 save가 없었다. 2026-08-25 정책 재확인 뒤 이 호환 경로를 제거했고
  새 maintenance abstraction으로 승격하지 않았다.
- `CurrentLevelGroupId`는 persisted field지만 current sequence에서 파생 가능한 값이다. 따라서
  structural validity와 sequence-resolved launchability를 같은 canonical 의미로 취급하면 parser가
  catalog/sequence에 결합된다. 두 검증 단계를 분리해야 한다.
- Gameplay presentation은 마지막 목숨 연출 중 일시적으로 표시값 `0`을 사용할 수 있다. 금지할
  것은 save document/domain state의 `0`이지 모든 presentation 숫자 `0`이 아니다. source guard도
  save boundary로 제한해야 한다.

## 4. `Clone()`과 canonicalization의 장기 판단

현재 구조에서는 아래 의미 분리가 맞다.

| 연산 | 의미 | 허용되는 변화 |
| --- | --- | --- |
| `Clone()` | 입력을 진단·격리하기 위한 exact deep copy | 없음 |
| validation | raw 값이 현재 계약을 만족하는지 판정 | 없음 |
| canonicalization | valid input을 저장 표현 하나로 정리 | 허용 목록에 명시된 변화만 |

따라서 `Clone()`을 normalization까지 수행하도록 되돌리거나, 일반적인
`CreateCanonicalizedCopy()`로 대체하는 것은 장기 해법이 아니다. 그렇게 하면 corruption을
검증 전에 지우고 호출 위치에 따라 copy 결과가 달라질 수 있다.

장기적으로 더 좋은 해법은 canonical domain state를 immutable하게 만드는 것이다.

- raw document/diagnostic carrier만 exact copy가 필요하다.
- `CampaignSlotState`는 생성 시 이미 valid + canonical이므로 별도 defensive `Clone()`이 필요 없다.
- collection은 외부에서 변경할 수 없는 read-only/immutable 표현을 사용한다.
- 상태 변경은 transition engine이 새 `CampaignSlotState`를 반환하는 방식으로만 수행한다.

즉, 장기 목표는 `Clone()`과 `CreateCanonicalizedCopy()` 중 하나를 고르는 것이 아니라,
두 연산이 필요했던 가변 상태 공유 자체를 canonical immutable state로 줄이는 것이다.

## 5. 목표 구조

### 현재 구조

```text
CampaignSlotDocument (persisted DTO)
        |
        v
validator + tolerant fragment mapper
        |
        v
SaveSlotData (raw/empty/valid/corrupt/mutable를 모두 표현)
        |                              |
        v                              v
CampaignSaveService mutation    Transient store mutation
        |                              |
        +-------- parity tests --------+
```

### 목표 구조

```text
CampaignProfileDocument (untrusted persisted DTO)
        |
        v
profile validator + slot presence resolution
        |
        +---- absent slot ----> CampaignSlotEntry.Empty
        |
        +---- present CampaignSlotDocument ----> CampaignSlotParser / strict validator
                                                |
                                                +---- failure ----> CampaignSlotDiagnostic
                                                |
                                                v
                                     CampaignSlotState (immutable, valid, canonical)
        |
        +---- planner ----> typed transition plan
        |
        v
CampaignSlotTransitionEngine (single mutation-rule owner, pure)
        |
        v
new CampaignSlotState
        |
        v
strict document mapper -> Production repository
        |
        +---------------> Transient state container
```

`CampaignProgressionTransitionPlanner`와 transition engine은 합치지 않는다. planner는 stage
sequence를 바탕으로 “어디로 갈지”를 결정하고, engine은 current state와 plan의 precondition을
검사한 뒤 “slot 전체가 어떻게 바뀌는지”를 한 곳에서 적용한다.

## 6. 목표 타입과 책임

| 구성 요소 | 입력 | 출력 | 금지 사항 |
| --- | --- | --- | --- |
| `CampaignSlotDocument` | JSON serializer | raw persisted fields | domain 유효 상태라고 가정 금지 |
| `CampaignSlotParser` | untrusted document | immutable state 또는 diagnostic | invalid record skip/clamp 금지 |
| `CampaignSlotEntry` | slot number + empty/occupied | 명시적 empty 또는 state | invalid/corrupt 포함 금지 |
| `CampaignSlotState` | validated factory/parser | canonical read-only state | public setter와 외부 collection mutation 금지 |
| `CampaignSlotTransitionEngine` | current state + typed command/plan + timestamp | new state 또는 typed failure | repository, clock, Unity API 접근 금지 |
| strict document mapper | canonical state | schema 2 document | validation, filtering, business normalization 금지 |
| `CampaignSlotDiagnostic` | raw evidence + failure reason | UI/log용 read model | 저장 가능한 domain state처럼 사용 금지 |
| launch eligibility evaluator | state + sequence/catalog | continue/restart/delete eligibility | 저장 mutation 금지 |
| maintenance/launch preparation | state identity + current sequence policy | preconditioned narrow command | full-slot replacement와 자동 write 금지 |
| Production adapter | repository transaction + engine result | service result | gameplay field 직접 변경 금지 |
| Transient adapter | locked state container + engine result | 동일 domain result | 전이 규칙 재구현 금지 |

`CampaignSlotEntry.Empty`는 physical schema에서 slot document가 없는 상태와 대응한다.
corrupted document는 Empty로 투영하지 않고 load report/diagnostic 경로에 남긴다. UI가 placeholder를
필요로 하면 별도 presentation projection에서 만들며, persistence/domain state로 역유입시키지 않는다.

## 7. 단계별 구현 계획

각 단계는 독립적으로 review·검증·rollback 가능해야 한다. Phase 하나가 여러 work package를
포함하면 Goal turn도 work package 단위로 나눈다. 특히 Phase 1과 Phase 3을 한 change set에
합치지 않는다. 그렇지 않으면 전이 결과가 달라졌는지 타입 migration 때문에 test가 깨졌는지
구분하기 어렵다.

### Phase 0 — 현재 계약과 mutation inventory 고정

#### 목적

구조 변경 전에 현재 동작을 추측이 아닌 executable characterization으로 고정한다. 이 Phase는
production code를 고치지 않는다.

#### 진입 조건

- README와 pre-release save policy의 chance/schema/PlayerPrefs 계약이 현재 코드와 일치한다.
- 기존 dirty working tree에서 save 관련 변경과 무관한 사용자 변경을 구분해 두었다.
- 기존 filtered fixture와 core/UI evidence의 revision을 확인했다.

#### 현재 확인된 test gap

- Production/Transient death parity는 `3 -> 2`와 last chance `1 -> 3`만 직접 비교한다.
- `2 -> 1` death parity가 없다.
- stage-clear Production/Transient complete parity fixture가 없다.
- stale stage-clear/death는 Production service 중심이며 공통 failure mapping이 고정되어 있지 않다.
- Transient가 자체 clock을 읽으므로 timestamp parity를 deterministic하게 비교하기 어렵다.
- comic completion과 diagnostic stage selection은 두 adapter에 구현되어 있지만 complete parity가 없다.

#### Work package 0A — transition characterization

1. 별도 `CampaignSlotTransitionCharacterizationTests` 또는 기존 adapter fixture에 death truth table을
   추가한다.
2. stage-clear는 same-group, group-boundary, final-stage를 모두 비교한다.
3. receipt 상태는 다음 세 상태를 별도로 고정한다.
   - `false + null`: valid receipt가 오면 최초 기록
   - `true + null`: payload를 임의 복원하거나 덮어쓰지 않고 presence 보존
   - `true + valid payload`: 기존 provenance 보존
4. performance는 absent, first insert, better value, worse value, 같은 값, 이미 canonical duplicate
   input을 구분한다. persisted duplicate는 repository validation에서 계속 실패해야 한다.
5. stage-clear profile snapshot과 세 processed-ID collection이 death/clear 후 byte-semantic하게
   보존되는지 확인한다.
6. stale precondition과 invalid plan/request에서 state와 repository save count가 변하지 않는지
   확인한다.

#### Work package 0B — failure와 time 계약 고정

현재 외부 surface를 변경하지 않고 다음 mapping을 test로 기록한다.

| 원인 | Production service | Production adapter | Transient |
| --- | --- | --- | --- |
| invalid plan/request | `InvalidRequest` | 현재 adapter exception | `ArgumentException` |
| stale precondition | `InvalidRequest` | 현재 adapter exception | `InvalidOperationException` |
| invalid slot number | `InvalidSlotNumber` 또는 사전 throw | `ArgumentOutOfRangeException` | `ArgumentOutOfRangeException` |
| blocked/recovery pending | load/write failure status | `InvalidOperationException` | 해당 없음 |

정확한 adapter exception type/message가 public contract인지 먼저 test를 보고 결정한다. public contract가
아니면 message 전체를 고정하지 말고 failure kind와 no-write만 고정한다.

timestamp 비교를 위해 Production service의 기존 clock seam과 동등한 test clock을 Transient에
주입할 수 있는 internal constructor seam을 추가하는 것은 허용한다. 다만 이 seam은 testability
변경이며 gameplay 시간 정책을 새로 정의하지 않는다.

#### Work package 0C — mutation inventory와 구조 기준선

다음 위치를 authoritative field assignment inventory에 기록한다.

- `CampaignSaveService`: death, clear, comic document mutation
- `TransientCampaignSaveSlotStore`: death, clear, comic, diagnostic mutation
- `CampaignSaveSlotStoreAdapter`: diagnostic full-slot mutation
- `SaveSlotValidationService`: 당시 level-group 보정 owner; retired-stage 보정은 Phase 4 entry에서,
  service/result/status 자체는 Phase 4C에서 제거
- `SaveSlotData` factory: empty/new-game 생성
- mapper/canonicalizer: representation materialization

단순 assignment 수를 품질 지표로 사용하지 않는다. 각 assignment가 gameplay transition, factory,
maintenance, representation mapping 중 어느 책임인지 분류한다.

##### 2026-08-24 current authoritative assignment inventory

이 inventory의 단위는 assignment 개수가 아니라 mutation을 소유하거나 완성하는 method/boundary다.
object initializer와 clone assignment는 write authority가 아니라 factory 또는 representation
materialization으로 분류한다. forwarding-only wrapper인 `ActiveSlotCampaignSaveStore`와 gameplay/UI
caller는 별도 authority로 세지 않는다.

| 책임 | 현재 위치 | authoritative field assignment / write boundary | Phase 1 이후 목표 owner |
| --- | --- | --- | --- |
| gameplay transition — death | `CampaignSaveService.CommitDeath -> CampaignSlotTransitionEngine.ApplyDeath` | engine이 expected stage/chance를 검증하고 stage, level group, chance, total deaths, slot timestamp를 계산하며 service가 같은 repository mutation에서 profile timestamp와 함께 저장 | Phase 1 완료: common transition engine + Production transaction |
| gameplay transition — clear | `CampaignSaveService.CommitStageClear -> CampaignSlotTransitionEngine.ApplyStageClear` | engine이 completed stage precondition 뒤 persisted stage/group/completed, 조건부 chance reset, first-write receipt, best performance, slot timestamp를 계산하며 service가 같은 transaction에서 저장 | Phase 1 완료: common transition engine + Production transaction |
| gameplay transition — comic | `CampaignSaveService.SetComicCompletion` | slot lookup 뒤 intro/outro flag와 timestamp를 document에 직접 갱신하며, stage가 비어 있으면 update stage를 materialize | typed common transition command + Production transaction |
| gameplay transition — death | `TransientCampaignSaveSlotStore.CommitDeath -> CampaignSlotTransitionEngine.ApplyDeath` | current slot load, 단일 clock, engine 적용, validated replacement와 exception mapping을 같은 lock 안에서 수행 | Phase 1 완료: common transition engine + Transient lock |
| gameplay transition — clear | `TransientCampaignSaveSlotStore.CommitStageClear -> CampaignSlotTransitionEngine.ApplyStageClear` | current slot load, 단일 clock, engine 적용, validated replacement와 exception mapping을 같은 lock 안에서 수행 | Phase 1 완료: common transition engine + Transient lock |
| gameplay transition — comic | `TransientCampaignSaveSlotStore.MarkIntroComicCompleted`, `MarkOutroComicCompleted` | flag와 timestamp를 clone에 갱신한 뒤 maintenance replacement로 재진입 | typed common transition command + Transient lock |
| factory | `CampaignSaveService.InitializeNewGame` | schema 2 `CampaignSlotDocument`의 initial stage/group, chance `3`, false/null/empty collections, deaths `0`, timestamp를 생성 | new-game factory result를 persistence boundary가 저장 |
| factory | `SaveSlotData.CreateEmpty`, `CreateNewGame` | missing-slot placeholder와 playable new-game carrier의 기본값을 생성한다. `Clone`은 nested value까지 raw-exact copy하며 migration/canonicalization을 수행하지 않는다 | Empty/ValidNonEmpty factory를 분리하되 clone의 exact-copy 계약 유지 |
| maintenance | `CampaignSaveSlotStoreAdapter.SetActiveStageForDiagnostics` | full `SaveSlotData`를 load해 diagnostic stage/group/completed/timestamp/snapshot record를 바꾸고 `ReplaceValidatedSlot`로 저장 | preconditioned narrow diagnostic command; full-slot replacement 제거 |
| maintenance | `TransientCampaignSaveSlotStore.SetActiveStageForDiagnostics` | Production diagnostic path와 같은 full-slot mutation을 수행 | 같은 narrow diagnostic command + Transient lock |
| maintenance | historical `SaveSlotValidationService.Validate`, `ValidateAndSync` | 당시 current level-group drift를 clone에서 보정하고 `RequiresSaveSync`이면 full-slot replacement. retired path는 Phase 4 entry, sync method는 4B, service/result/status는 4C에서 제거 | 완료: read-only evaluator/action policy + explicit Continue preparation command |
| maintenance | `CampaignSaveService.ReplaceValidatedSlot`, `TransientCampaignSaveSlotStore.ReplaceValidatedSlot` | externally assembled full slot을 canonicalize/clone하고 timestamp를 materialize해 authoritative store에 교체 | migration/repair 전용 seam으로 축소; ordinary gameplay/launch path 사용 금지 |
| maintenance | `CampaignSaveService.DeleteSlot`, `ClearAll`; `TransientCampaignSaveSlotStore.DeleteSlot`, `ClearAll` | slot/profile collection lifecycle을 변경한다 | typed delete/clear command; gameplay transition과 분리 유지 |
| maintenance | `StandaloneCampaignSaveSeedImporter.TryImport`, `PlayerCaptureLaunchBootstrap.SeedFixture` | diagnostic/test seed에서 full slot을 조립해 replacement seam으로 투입 | diagnostic-only typed seed command; Production/Campaign DirectPlay progression path와 분리 |
| representation mapping | `CampaignSlotMapper.ToDocument`, `ToDomain` 및 nested receipt/performance/profile mappers | schema 2 document와 runtime carrier를 새 객체로 materialize하며 processed-ID collection을 clone/sort한다 | raw DTO ↔ canonical domain mapper; gameplay 결정을 하지 않음 |
| representation mapping | `CampaignSlotCanonicalizer.CreateValidatedCopy` | level group/timestamp null, performance order·duplicate, snapshot null, receipt presence를 canonical representation으로 materialize | raw validation 성공 뒤 한 번만 호출되는 explicit canonical boundary |
| representation mapping | `CampaignSaveService.Normalize`, `CloneSlot`, `TouchSlot`, `TouchProfile` | save 직전 document null/collection shape와 persistence timestamp를 materialize하고 deep copy한다 | persistence serializer/transaction metadata boundary |
| representation mapping | `FileCampaignProfileRepository.Validate`/`Normalize` | validation을 통과한 loaded document의 null collections/snapshot을 materialize하고 absent receipt payload를 제거한다 | raw parse/validate 뒤 canonical mapping boundary; malformed input에는 실행 금지 |
| representation mapping | `CampaignProfileDocumentMapper.ToDomainSlots` | physical absence를 `CreateEmpty` placeholder로 채워 fixed-size runtime array를 만든다 | physical Empty와 presentation placeholder를 구분하는 entry projection |

Source scan에서 별도 write owner가 아닌 것으로 분류한 경계도 명시한다.
`CampaignGameplayFlowController`, `SlotComicProgressStore`, demo control bridge는 typed port를 호출할 뿐 slot
field를 직접 변경하지 않는다. `ActiveSlotCampaignSaveStore`는 argument/empty guard 뒤 위임만 한다.
`MainMenuController`의 new-game candidate는 factory output이며, validation repair 때만 maintenance port에
전달된다. UI view model과 smoke probe의 동일 이름 assignment는 presentation/diagnostic state이므로 이
authoritative inventory에서 제외한다.

Phase 0 기준선에서 실제 drift hotspot은 Production/Transient에 중복된 death·clear·comic 적용과
diagnostic full-slot replacement다. mapper/canonicalizer/repository normalization의 assignment는
representation 책임이지만 raw evidence를 잃을 수 있으므로 Phase 2의 boundary 분리 대상이다. 이
분류는 assignment를 줄이는 목표가 아니며 schema 2 physical shape나 현재 runtime 결과를 변경하지
않는다.

##### Phase 0 exit audit — 2026-08-24

| exit condition | same-revision evidence | 판정 |
| --- | --- | --- |
| deterministic death/clear truth table | characterization 25 cases와 service/adapter fixtures를 합친 EditMode `88/0` | 충족 |
| failure mapping + no-write | invalid/stale/null/slot/load/recovery cases에서 service status, adapter/Transient exception kind, repository save count를 고정 | 충족 |
| responsibility-classified mutation inventory | 위 current authoritative assignment inventory와 runtime source scan | 충족 |
| production runtime behavior unchanged | 0C는 documentation-only이고, Phase 0 runtime 변경은 Transient의 internal test clock seam뿐이며 public default clock/format은 유지 | 충족 |

Production/Transient의 characterized slot 및 timestamp 결과 차이는 없었고 receipt presence나
processed-ID 의미 변경도 필요하지 않았다. 따라서 중단 조건은 발생하지 않았으며 Phase 1 진입 조건을
충족한다. 이 판정은 broad/full regression green 주장이 아니라 아래 filtered fixture와 core lane에
한정된다.

#### 검증

- `./run_tests.sh full --filter CampaignSlotTransitionCharacterizationTests`
- `./run_tests.sh full --filter CampaignSaveSlotStoreAdapterTests`
- `./run_tests.sh full --filter CampaignSaveServiceTests`
- `./run_tests.sh core`
- UI code를 건드리지 않았다면 UI lane은 not run 사유를 기록한다.

#### Exit / 중단 기준

Exit:

- death/clear truth table의 모든 행이 deterministic test로 고정된다.
- failure mapping과 no-write 보장이 기록된다.
- mutation inventory가 responsibility별로 분류된다.
- production runtime 동작은 바뀌지 않는다.

중단:

- 기존 Production/Transient 결과가 서로 달라 어느 쪽이 canonical인지 제품 결정이 필요한 경우
- timestamp, receipt presence, processed-ID 의미를 바꿔야만 parity가 가능한 경우

Rollback: characterization test와 internal clock seam만 되돌리면 된다. schema/data 변화는 없다.

### Phase 1 — 공통 pure transition engine 추출

#### 목적

가장 큰 drift 원인인 Production/Transient death·stage-clear 전이 적용 중복을 제거한다. 이 Phase는
아직 public `SaveSlotData` 전체 migration을 하지 않는다.

#### 진입 조건

- Phase 0 truth table이 same revision에서 green이다.
- current error mapping과 timestamp owner가 문서화되어 있다.
- Production repository transaction과 Transient lock 안에서 전이를 적용해야 한다는 점이 test로
  확인되어 있다.

#### engine 계약

초기 engine은 blast radius를 줄이기 위해 `SaveSlotData`를 받되 외부 객체를 직접 변경하지 않는다.
Phase 1 동안에는 `ValidNonEmpty` 검증과 공통 canonicalizer를 entry adapter로 한 번 사용하고,
그 임시 의존은 Phase 3에서 제거한다.

```csharp
CampaignSlotTransitionResult ApplyDeath(
    SaveSlotData current,
    CampaignDeathTransitionPlan plan,
    string committedAtUtc);

CampaignSlotTransitionResult ApplyStageClear(
    SaveSlotData current,
    CampaignStageClearCommitRequest request,
    string committedAtUtc);
```

권장 result는 exception 대신 다음 정보를 가진다.

- success 여부
- `CampaignSlotTransitionFailureKind`: `InvalidCurrentState`, `InvalidPlan`, `StalePrecondition`
- 성공 시 새 slot
- stage-clear 성공 시 `PreviousRemainingChances`
- 진단 가능한 짧은 reason code; raw path나 개인정보를 포함한 message는 domain에 넣지 않는다.

engine은 repository status나 UI exception type을 알지 못한다. Production service와 Transient adapter가
Phase 0 mapping에 맞게 typed failure를 각각 변환한다.

#### Work package 1A — pure engine 추가

1. `CampaignSlotTransitionEngine.cs`를 Stages runtime assembly에 추가한다.
2. plan/request shape validation을 service/Transient에서 engine으로 이동한다.
3. current stage/chance stale precondition을 engine 한 곳에서 검사한다.
4. death는 stage, level group, chance, total deaths, timestamp만 변경한다.
5. clear는 stage, level group, completion, conditional chance restore, receipt, performance, timestamp를
   변경한다.
6. 변경 대상이 아닌 snapshot, processed IDs, comic flags, deaths, existing receipt provenance는 exact하게
   보존한다.
7. engine은 `DateTimeOffset.UtcNow`, repository, Unity API, static Transient state를 참조하지 않는다.

#### Work package 1B — Production wiring

1. `CampaignSaveService.Mutate`가 document를 load/validate한 transaction 내부에서 domain slot을 만든다.
2. 같은 transaction 안에서 engine을 호출한다. load 후 transaction 밖에서 계산한 state를 다시 쓰지 않는다.
3. success state만 strict/current mapper를 거쳐 replacement document로 만든다.
4. engine failure를 기존 `CampaignSaveCommandStatus`로 변환하고 `Save`를 호출하지 않는다.
5. slot과 profile timestamp는 같은 `Now()` 값을 사용한다.
6. blocked profile/recovery behavior는 adapter/repository 책임으로 그대로 둔다.

#### Work package 1C — Transient wiring

1. lock을 획득한 상태에서 current slot을 읽고 engine을 호출하고 새 slot을 교체한다.
2. engine 호출 전후에 lock을 풀지 않는다.
3. internal clock seam에서 한 번 얻은 timestamp를 engine에 전달한다.
4. engine failure를 Phase 0에서 고정한 exception surface로 변환한다.
5. death/clear field assignment와 request branch를 Transient에서 제거한다.

#### 이번 Phase의 비범위

- comic completion과 diagnostic stage selection 이동
- immutable state 도입
- mapper public API 정리
- MainMenu maintenance replacement 변경
- schema/document field 변경

예상 touch set:

- `CampaignSlotTransitionEngine.cs`와 `.meta`
- `CampaignSaveService.cs`
- `TransientCampaignState.cs`
- `CampaignSaveServiceTests.cs`
- `CampaignSaveSlotStoreAdapterTests.cs`
- 신규 engine test와 `.meta`

`CampaignSlotDocument`, repository recovery, MainMenu/UI 파일이 이 Phase에서 변경되기 시작하면 scope가
넓어진 이유를 먼저 설명하고 중단 여부를 판단한다.

#### 검증

- 새 `CampaignSlotTransitionEngineTests` 전체 truth table
- Phase 0 characterization fixture
- `CampaignSaveServiceTests`
- `CampaignSaveSlotStoreAdapterTests`
- stale/invalid/blocked no-write tests
- `./run_tests.sh core`
- 필요 시 `./run_tests.sh full --filter CampaignSlotTransitionEngineTests`

순수 engine fixture는 현재 Stages Editor test assembly에 놓일 가능성이 높으므로 `[Category("Core")]`
표시만으로 core lane에 포함된다고 가정하지 않는다. runner selection을 확인하고 filtered `full`
fixture evidence를 별도로 남긴다.

#### Exit / 중단 기준

Exit:

- death/stage-clear gameplay field assignment가 engine 한 곳에만 있다.
- Production/Transient에는 transaction, lock, error mapping만 남는다.
- 두 composition이 같은 engine result를 소비한다.
- Phase 0 결과, schema 2 JSON, no-write 동작이 유지된다.

중단:

- engine이 repository status, Unity clock, composition type을 알아야만 동작하는 경우
- Production과 Transient failure 차이를 engine exception 분기로 넣어야 하는 경우
- 기존 receipt/processed-ID 의미 변경이 필요한 경우

Rollback: service와 Transient wiring만 이전 inline implementation으로 되돌릴 수 있다. persisted
schema가 같으므로 데이터 migration은 필요 없다.

### Phase 2 — mapper surface와 normalization owner 분리

#### 목적

strict persistence, serializer materialization, runtime canonicalization, tolerant achievement projection을
서로 다른 이름과 owner로 분리한다. Phase 3의 immutable state가 들어오기 전까지 mapper input은
transitional `SaveSlotData`일 수 있지만 public 우회 surface와 중복 normalization은 먼저 줄인다.

#### 진입 조건

- Phase 1 engine과 양 adapter가 green이다.
- mapper fragment 호출자를 source scan으로 다시 확인했다.
- document validator가 어떤 null을 허용하고 repository가 어떤 null을 materialize하는지 표로 고정했다.

#### 2026-08-24 Phase 2 entry audit

Phase 1의 same-revision exit가 green인 working tree에서 repository 전체 C# caller와 Stages assembly
visibility를 다시 확인했다. product/runtime assembly가 public fragment mapper를 직접 사용하는 경우는
없다. 직접 fragment 호출은 Stages Editor test에만 있으며, `CampaignSlotMapper`는 internal type이고
Stages test assembly는 `InternalsVisibleTo`로 complete slot path를 직접 검증한다.

##### mapper caller inventory

| surface | 현재 visibility | product/runtime caller | test-only caller / 의미 | 다음 package 처리 |
| --- | --- | --- | --- | --- |
| `CampaignProfileDocumentMapper.ToDocument(IReadOnlyList<SaveSlotData>, ...)` | public | 없음 | mapper/receipt/architecture/PlayerPrefs-removal fixture가 complete profile serialization을 검증 | complete profile path로 유지 |
| `CampaignProfileDocumentMapper.ToDomainSlots(CampaignProfileDocument)` | internal | `CampaignSaveSlotStoreAdapter.LoadAllWithReport` | `CampaignSlotMapperTests`가 top-level validator 선행과 physical absence projection을 검증 | internal complete profile parse path로 유지 |
| `CampaignProfileDocumentMapper.ToSlotDocument(SaveSlotData)` | public | `CampaignSaveSlotStoreAdapter.ReplaceValidatedSlot` | transition/mapper/receipt/architecture fixture가 complete slot serialization을 검증 | complete slot path로 유지하되 Phase 3 state mapper 전환 전까지만 transitional carrier를 허용 |
| public receipt fragment `ToReceiptDocument` / `ToReceipt` | public | 없음 | `NormalCampaignCompletionReceiptTests`만 직접 호출 | 2A에서 public facade 제거 또는 internal strict helper로 축소하고 complete slot round-trip으로 test 이동 |
| public performance fragment `ToPerformanceRecordDocuments` / `ToPerformanceRecords` | public | 없음 | architecture/transition fingerprint fixture만 직접 호출 | 2A에서 public facade 제거; 2C strict serialization과 tolerant projection을 별도 API로 분리 |
| public stage-clear fragment `ToStageClearProfileDocument` / `ToPlayerStageClearRecordDocument` | public | 없음 | architecture/transition fingerprint fixture만 직접 호출; player-record public wrapper caller는 없음 | 2A에서 public facade 제거 또는 private strict helper로 축소 |
| internal `CampaignSlotMapper.ToDocument` / `ToDomain` | internal type | `CampaignSaveService`, `CampaignSaveSlotStoreAdapter`, complete profile mapper | mapper/service fixture가 complete slot 양방향 경계를 검증 | strict complete slot path로 유지 |
| internal receipt/performance/stage-clear helpers | internal type의 public method | 같은 mapper 파일의 complete slot path만 사용 | direct external caller 없음 | 2A/2C에서 private strict helper로 축소 |

따라서 “실제 외부 assembly consumer 발견” 중단 조건은 발생하지 않았다. 2A는 schema나 runtime caller
migration 없이 test-only fragment facade부터 제거할 수 있다.

##### validator 허용 null과 post-validation materialization

| raw/document 위치 | validator 판정 | validation 뒤 current materialization | 보존해야 할 의미 |
| --- | --- | --- | --- |
| root `Slots == null` | empty slot document collection으로 허용 | repository가 `Array.Empty<CampaignSlotDocument>()`로 변경 | physical slot absence; `LastPlayedSlotNumber`는 여전히 persisted slot을 가리켜야 함 |
| root `ProductVersion` / `SavedAtUtc == null` | 허용 | repository는 변경하지 않고 service `Normalize`가 clone에서 empty string으로 변경 | serializer incidental null이며 schema field 삭제나 default version repair가 아님 |
| slot `LevelGroupId` / `LastPlayedAtUtc == null` | 허용 | repository는 변경하지 않고 service/mapper가 domain 진입 시 empty string으로 변경 | current canonical string materialization |
| `NormalStagePerformanceRecords == null` | 허용 | repository가 empty array로 변경 | missing collection만 materialize; null/invalid/duplicate element는 허용하지 않음 |
| performance element null, invalid version/stage/count, duplicate stage | invalid | materialization 없음 | normalization 전에 `InvalidDocument` fail-closed |
| `StageClearProfileSnapshot == null` | 허용 | repository가 empty profile document로 변경 | missing nested container materialization |
| snapshot `Records` / profile processed-ID arrays `== null` | 허용 | repository가 각각 empty array로 변경 | collection absence만 materialize; invalid/duplicate ID는 fail-closed |
| record `ProcessedStageRunIds == null` | 허용 | repository가 empty array로 변경 | collection absence만 materialize; null record와 invalid/duplicate ID는 fail-closed |
| receipt presence false + incidental payload object | 허용 | repository가 payload를 null로 제거 | presence flag가 authoritative; JsonUtility incidental object를 receipt로 추론하지 않음 |
| receipt presence true + null payload | 허용 | 변경하지 않음 | `PresentWithoutPayload` 상태를 그대로 보존 |
| receipt presence true + invalid payload | invalid | materialization 없음 | invalid receipt를 repair/skip하지 않음 |
| null slot element, blank `ProfileId`, non-canonical `StageId`, invalid chance/counter | invalid | materialization 없음 | top-level/profile/slot structural failure |

current load order는 syntax validation -> `JsonUtility.FromJson` -> mutation 없는
`CampaignProfileDocumentValidator.Validate` -> repository `Normalize` 순이다. 다만 owner 이름과 위치에는
다음 중복/혼합이 남아 있다.

- `FileCampaignProfileRepository.Validate`는 실제로 validation 성공 뒤 document를 materialize하므로
  이름만으로 side effect를 알기 어렵다.
- `CampaignSaveService.Normalize`는 repository 반환값을 clone한 뒤 root/slot string과 snapshot을 다시
  materialize하며, `Loaded`를 잘못 보고하는 임의 repository에 대해서는 schema/profile ID repair처럼
  동작할 수 있다.
- `CampaignSlotMapper.ToPerformanceRecords`와 `ToStageClearProfileSnapshot` fragment는 invalid element
  skip, negative counter clamp, tolerant `Normalize`를 포함한다. complete slot path에서는 선행 validator
  때문에 결과를 바꾸지 않지만 public fragment로는 validation을 우회할 수 있다.
- achievement integration은 committed slot을 읽을 때 `NormalStagePerformanceRecordPolicy.Normalize`를
  방어적으로 사용한다. 이 tolerant projection은 persistence mapper와 다른 목적이지만 현재 이름과
  policy type을 공유한다.

이 audit 결과로 2A는 public fragment surface 축소, 2B는 post-validation document materializer 단일화,
2C는 strict performance serialization과 achievement tolerant read projection 분리 순서를 유지한다.

#### normalization 분류표

| 종류 | 예 | owner | 정책 |
| --- | --- | --- | --- |
| raw structural validation | invalid stage, negative count, duplicate persisted ID | document validator/parser | mutation 없이 fail-closed |
| serializer materialization | valid document의 null array를 empty array로 | repository document materializer | validation 통과 후 한 번만 허용 |
| runtime canonicalization | valid performance duplicate의 best-value + deterministic order | state factory/canonicalizer | valid runtime input에만 허용 |
| tolerant read projection | achievement가 방어적으로 malformed record를 무시 | 목적별 read-model builder | persistence에 재사용 금지 |
| 금지된 repair | negative count clamp, invalid element skip | 없음 | 오류를 숨기므로 금지 |

현재 `ToStageClearProfileSnapshot`의 `Math.Max`와 invalid fragment `continue`는 top-level validator 뒤에서는
결과를 바꾸지 않지만, public fragment 호출 시 repair처럼 동작한다. strict path에서는 제거한다.

#### Work package 2A — public mapper surface 축소

1. public API는 complete profile/slot mapping만 남긴다.
2. receipt/performance/stage-clear fragment mapper는 private/internal strict helper로 바꾼다.
3. fragment API를 직접 호출하던 tests는 complete slot round-trip 또는 해당 policy test로 변경한다.
4. runtime consumer scan에서 complete path 외 fragment caller가 없음을 architecture test로 고정한다.
5. test assembly 접근이 꼭 필요하면 `InternalsVisibleTo`보다 public behavior test를 우선한다.

2026-08-24 구현 결과: `CampaignProfileDocumentMapper`의 public surface는 complete profile
`ToDocument`와 complete slot `ToSlotDocument`만 남겼다. receipt/performance/stage-clear 변환은
`CampaignSlotMapper`의 private helper로 축소했고, 직접 fragment를 호출하던 fixture는 complete
slot/profile round-trip을 사용한다. reflection architecture guard가 두 mapper의 complete-path surface를
고정하며 physical DTO, schema version, 변환 정책은 이 package에서 변경하지 않았다.

#### Work package 2B — document materializer 단일화

1. repository load에서 validation을 먼저 수행한다.
2. validation을 통과한 뒤 JsonUtility incidental null만 `CampaignProfileDocumentMaterializer` 같은 한
   owner가 materialize한다.
3. service와 repository에 중복된 `Normalize(CampaignSlotDocument)`를 비교해 한 owner로 합친다.
4. `HasNormalCampaignCompletionReceipt == false`일 때 JsonUtility가 만든 incidental nested object를
   null로 되돌리는 current presence-flag 계약은 유지한다.
5. duplicate persisted performance/processed ID는 materialization 전에 계속 실패한다.

2026-08-24 구현 결과: mutation-free validator 통과 뒤 repository가
`CampaignProfileDocumentMaterializer.MaterializeValidated`를 한 번 호출하도록 load/save 경계를
명시했다. 이 owner는 허용된 null string/collection/snapshot과 receipt absence payload만
materialize한다. `CampaignSaveService.Normalize`를 제거하고 document deep clone을 raw exact copy로
바꾸었으며, repository가 `Loaded`/`BackupRecovered`로 잘못 반환한 invalid document는 service에서도
validation result에 대응하는 `UnsupportedVersion`/`InvalidDocument` load failure로 차단한다.
physical DTO와 schema version은 변경하지 않았다.

#### Work package 2C — performance policy 분리

1. engine/state 경계는 `CanonicalizeValidated`/`UpsertBest`의 strict path를 사용한다.
2. mapper는 이미 canonical인 performance collection을 순서대로 직렬화하며 다시 `Normalize`하지 않는다.
3. achievement의 방어적 `Normalize`는 `CampaignStageAchievementReadModelBuilder`처럼 목적이 드러나는
   projection으로 이동한다.
4. Phase 3에서 achievement가 immutable committed state를 받게 되면 tolerant projection 제거 가능성을
   다시 검토한다.

2026-08-24 구현 결과: `NormalStagePerformanceRecordPolicy`의 tolerant public `Normalize`를 제거하고
`CanonicalizeValidated`/`UpsertBest`가 invalid runtime record를 거부한 뒤에만 best-value와 deterministic
order를 만든다. Persistence mapper는 validator를 통과한 performance/stage-clear document를 순서와
counter 그대로 일대일 변환하며 invalid skip, counter clamp, business normalization을 하지 않는다.
Achievement recovery의 방어적 projection은 별도 `CampaignStageAchievementReadModelBuilder`와
`CampaignStageAchievementReadModel`이 소유하며 malformed/null record를 무시하고 valid duplicate의
best 값을 deterministic order로 만든다. physical DTO field와 schema version은 변경하지 않았다.

예상 touch set:

- `CampaignProfileDocumentMapper.cs`
- `CampaignProfileDocument.cs`
- `FileCampaignProfileRepository.cs`
- `CampaignSaveService.cs`의 document materialization 중복부
- `CampaignSlotDocument.cs`의 performance policy
- `CampaignStageAchievementIntegration.cs`
- mapper/receipt/achievement/repository 관련 Editor tests

physical DTO field와 schema version은 변경 대상이 아니다.

#### 검증

- `CampaignSlotMapperTests`
- `CampaignSaveArchitectureV2Tests`의 document/repository subset
- `NormalCampaignCompletionReceiptTests`
- campaign achievement integration tests
- invalid persisted nested data no-write/recovery tests
- canonical round-trip property matrix
- `./run_tests.sh core`
- achievement/UI assembly가 영향을 받으면 `./run_tests.sh ui`

#### Exit / 중단 기준

Exit:

- public fragment mapper로 top-level validation을 우회할 수 없다.
- serializer materialization owner가 하나다.
- persistence mapper에 invalid element skip, counter clamp, business normalize가 없다.
- achievement tolerant projection은 persistence 코드와 이름/타입이 분리된다.
- canonical state/document round-trip에서 값 손실이 없다.

중단:

- public fragment API를 사용하는 실제 외부 assembly가 발견된 경우
- JsonUtility null materialization을 제거하면 current schema 2 파일을 읽을 수 없는 경우
- strict mapping을 위해 physical schema 변경이 필요한 경우

Rollback: public facade를 한 release/change set 동안 forwarding shim으로 되살릴 수 있다. strict와
tolerant 구현을 다시 합치지는 않는다.

### Phase 3 — immutable canonical `CampaignSlotState` 도입

#### 목적

raw/empty/corrupt/canonical 상태를 타입으로 분리하고, canonical state가 생성된 뒤 외부에서
변경되지 않도록 한다. 이 Phase가 완료되면 `Clone()`은 raw/compatibility 경계에만 남는다.

#### 진입 조건

- Phase 2 normalization 분류와 mapper surface가 고정되어 있다.
- `SaveSlotData` runtime consumer inventory를 query/UI/achievement/gameplay/bootstrap/test로 분류했다.
- Unity/C# runtime에서 사용할 read-only collection 구현을 작은 compile test로 확인했다.

#### 2026-08-24 Phase 3 entry audit

Phase 2 same-revision exit 뒤 `SaveSlotData` 참조를 다시 분류했다. Stages save store/engine/planner/service,
Gameplay query/flow/installer, achievement integration, UI application/composition, DirectPlay/bootstrap,
Editor fixture가 현재 consumer다. 3A는 이 consumer를 동시에 옮기지 않고 새 state/parser/factory와 strict
mapper만 추가한다. engine과 Production/Transient 내부는 3B, query/UI/achievement/gameplay/bootstrap은
3C의 순서를 유지한다.

Runtime assembly에서 이미 `WorldSnapshot`이 `ReadOnlyCollection<T>`와 read-only collection surface를
사용하고 있으며, 3A focused compile/behavior fixture가 private copied backing collection을
`IReadOnlyList<T>`로 노출해도 cast mutation이 거절됨을 확인했다. receipt의 absent / present-null /
present-payload는 기존 schema 2의 bool + nullable payload로 모두 표현 가능하다. 따라서 신규 package나
physical schema 변경 없이 3A를 진행할 수 있고 중단 조건은 발생하지 않았다.

#### 타입 불변식

`CampaignSlotState`는 occupied slot의 structural canonical state만 표현한다.

- slot number는 `1..3`이다.
- stage ID는 canonical하고 valid하다.
- remaining chances는 `1..3`이다.
- deaths와 nested counters는 non-negative다.
- performance는 structurally valid, stage별 한 개, deterministic order다.
- processed-ID는 blank/duplicate 없이 보존된다.
- null collection과 null canonical string은 없다.
- nested object도 외부에서 변경할 수 없다.

단, “현재 sequence/catalog에서 launch 가능한가”는 이 타입의 불변식이 아니다. 오래된 stage나
level-group mismatch를 검사하려고 parser를 catalog에 결합하지 않는다. 이는 Phase 4 evaluator가
소유한다.

#### 권장 타입 형태

- `CampaignSlotEntry`: `Empty(slotNumber)` 또는 `Occupied(CampaignSlotState)`만 허용한다.
- `CampaignSlotState`: internal/private constructor와 get-only property를 사용한다.
- `CampaignSlotStateFactory`: new game와 validated document parse 결과만 생성한다.
- `CampaignReceiptState`: `Absent`, `PresentWithoutPayload`, `PresentWithPayload`를 명시적으로 표현한다.
  현재의 `bool + nullable receipt` 조합을 잃지 않으면서 invalid 조합을 막는다.
- nested performance/clear record/profile도 immutable state type으로 바꾸고 persisted DTO와 분리한다.

새 package 의존성을 바로 추가하지 않는다. 우선 private copied collection을
`ReadOnlyCollection<T>`/`ReadOnlyDictionary<TKey,TValue>`로 감싸 실제 backing collection을 노출하지
않는 방식을 사용한다. `IReadOnlyList<T>`의 실제 객체로 raw array를 그대로 반환하면 cast를 통해
변경될 수 있으므로 피한다.

#### Work package 3A — state/factory/parser 추가

1. schema DTO인 `CampaignSlotDocument`는 JsonUtility용 mutable field를 유지한다.
2. parser는 raw document validation 실패 시 state를 만들지 않고 typed diagnostic을 반환한다.
3. empty는 document 부재에서 `CampaignSlotEntry.Empty`로 만든다.
4. valid document는 materialization 후 immutable nested state로 deep copy한다.
5. state-to-document mapper는 strict serialization만 수행한다.
6. state round-trip과 external mutation 불가를 reflection/behavior test로 고정한다.

2026-08-24 구현 결과: `CampaignSlotEntry`, immutable `CampaignSlotState`와 receipt/performance/stage-clear
nested state, validated-document parser, new-game factory, strict state-to-document mapper를 추가했다.
Document 부재는 explicit Empty로, slot mismatch/invalid raw document는 exact deep-copied evidence를 가진
typed diagnostic으로 분리하며 invalid input에서는 state를 만들지 않는다. Valid serializer null은 raw
document를 변경하지 않고 canonical empty collection/string/nested profile로 materialize된다. receipt 세
표현과 processed ID를 보존하고 deterministic collection order를 사용하며 모든 public state property는
get-only이고 backing collection은 외부 mutation을 거절한다. JsonUtility DTO와 schema version은 변경하지
않았고 engine/store/runtime consumer는 의도적으로 아직 이 타입에 연결하지 않았다.

#### Work package 3B — engine과 store 내부 migration

1. transition engine 입출력을 `CampaignSlotState`로 바꾼다.
2. Phase 1의 temporary canonicalizer entry adapter를 제거한다.
3. Production service는 document를 state로 parse하고 engine result state를 document로 직렬화한다.
4. Transient container는 `SaveSlotData[]` 대신 `CampaignSlotEntry[]` 또는 equivalent immutable state를
   보관한다.
5. comic completion을 typed engine command로 이동한다. `stageId`는 Phase 0에서 고정한 계약에 따라
   expected current stage precondition으로 승격하거나 제거하며, 기존처럼 무시되는 인자로 두지 않는다.
6. new game는 state factory가 생성한다.
7. diagnostic stage selection은 gameplay engine command가 아니라 별도 diagnostic/maintenance factory
   command로 남겨 Phase 4에서 좁힌다.

2026-08-24 구현 결과: death/stage-clear transition engine의 입출력과 result를 immutable
`CampaignSlotState`로 전환하고 intro/outro comic completion을 stage identity가 없는 typed engine command로
통합했다. 기존 port의 comic `StageId`는 3C 전까지 compatibility facade에서 valid 여부만 검사하며 service와
engine에는 전달하지 않으므로 expected-stage precondition처럼 오해되는 ignored service argument가 없다.
Production service는 transaction 안에서 persisted document를 fail-closed parse하고 engine result를 strict
state mapper로 직렬화한다. Transient는 `CampaignSlotEntry[]`를 보관하고 같은 engine을 lock 안에서 호출하며,
new game와 diagnostic selection은 각각 state factory와 별도 diagnostic factory path가 소유한다.
`SaveSlotData` 변환은 `CampaignSaveSlotStoreAdapter.cs`의 명시적 compatibility adapter와 Transient public
facade에만 남았고 source allowlist test가 신규 runtime 참조를 차단한다. Physical DTO/schema와 일반-purpose
public state replacement API는 추가하지 않았다.

#### Work package 3C — consumer migration

consumer는 다음 순서로 이동한다.

1. progression committer result와 gameplay chance query
2. achievement committed-state consumer
3. comic progress query/command
4. MainMenu slot query와 view-model mapper
5. editor DirectPlay와 standalone seed bootstrap
6. tests/fixtures

compatibility 기간에는 `SaveSlotData` adapter를 한 파일/namespace에만 두고 allowlist한다. 새 runtime
consumer가 adapter를 직접 요청하는 것은 금지한다.

2026-08-25 구현 결과: `ICampaignSaveQuery`를 Empty/Occupied `CampaignSlotEntry` query로, lifecycle,
diagnostic, progression commit result를 immutable `CampaignSlotState`로 전환했다. Gameplay chance/flow와
installer, achievement immediate/startup reconciliation, comic progress, MainMenu query/view-model,
DirectPlay, standalone/player-capture seed consumer는 mutable carrier를 더 이상 사용하지 않는다.
Achievement는 canonical performance state를 직접 소비하므로 Phase 2의 tolerant recovery projection을
제거했다. Comic command의 ignored `StageId`를 port에서 제거했고 DirectPlay/seed는 narrow
`CampaignSlotSeedImportRequest`/`ICampaignSlotSeedImportPort`를 사용한다. 기존 concrete adapter와
Transient의 source-compatible mutable facade, test fixture projection, 당시 `SaveSlotValidationService`의 Phase 4
maintenance bridge만 explicit compatibility allowlist로 남겼으며 새 general-purpose immutable state
replacement API나 physical schema 변경은 추가하지 않았다.

#### 일반 replacement에 대한 수정된 결론

`ReplaceValidatedSlot(SaveSlotData)`를 단순히 `ReplaceSlot(CampaignSlotState)`로 공개 교체하지 않는다.
immutable parameter는 invalid 전달은 막지만 stale full replacement가 다른 필드 변경을 덮는 문제를
막지 못한다.

- complete state replacement는 repository/service 내부 implementation detail로 제한한다.
- test setup은 fixture/factory를 사용한다.
- MainMenu sync는 Phase 4의 preconditioned narrow command를 사용한다.
- DirectPlay/seed는 목적이 명시된 initialize/import command를 사용한다.

Phase 4B에서 generic maintenance port를 제거했고 Phase 4C에서 combined validation facade도 제거했다.
남은 concrete/internal complete-replacement와 fixture projection은 Phase 5의 shim inventory다.

예상 touch set은 넓으므로 work package별로 제한한다.

- 3A: 신규 state/entry/parser/factory 파일, mapper, state/parser tests
- 3B: transition engine, save service, adapter, Transient store, engine/store tests
- 3C: `CampaignSavePorts.cs`, gameplay host query/flow, achievement integration, UI application/composition,
  DirectPlay/seed bootstrap과 각 consumer test

3A에서 UI/Gameplay consumer를 동시에 바꾸거나 3C에서 persisted DTO를 다시 설계하지 않는다.

#### 검증

- constructor/public setter reflection guard
- nested collection mutation attempt가 state를 바꾸지 않는 test
- raw invalid document가 state를 생성하지 않는 parser matrix
- empty/occupied round-trip
- receipt 세 상태 round-trip
- engine truth table 전체
- Production/Transient parity
- achievement/UI query compatibility
- `./run_tests.sh core`
- `./run_tests.sh ui`
- touched PlayMode fixture가 있다면 filtered `full`

#### Exit / 중단 기준

Exit:

- canonical state와 모든 nested state가 immutable하다.
- validated/canonical 의미를 parameter type과 factory가 보장한다.
- engine과 store 내부가 `SaveSlotData`를 사용하지 않는다.
- compatibility adapter 참조가 명시된 allowlist로만 제한된다.
- public general-purpose state replacement API가 새로 생기지 않는다.

중단:

- immutable collection을 위해 Unity 지원 범위를 벗어난 package/runtime이 필요한 경우
- content eligibility를 state constructor에 넣지 않으면 기존 동작을 보존할 수 없는 경우
- receipt presence 세 상태 중 하나를 삭제해야 하는 경우

Rollback: work package별 compatibility adapter를 유지해 consumer 단위로 되돌린다. 모든 consumer가
전환되기 전에 `SaveSlotData`와 clone 구현을 삭제하지 않는다.

### Phase 4 — empty, corruption, launch eligibility, maintenance 분리

#### 목적

`SaveSlotValidationService`에 섞인 structural state, profile failure, content eligibility, action policy,
maintenance write를 분리하고 MainMenu의 stale full replacement 가능성을 제거한다.

#### 진입 조건

- Phase 3 immutable state와 compatibility facade가 same revision에서 green이다.
- MainMenu Continue/Restart/Delete/NewGame 및 handoff ownership baseline이 고정되어 있다.
- 공개 배포 save가 없다는 release fact와 내부 QA reset 정책을 다시 확인했다.
- level-group sync를 계속 저장할지 launch 시 파생값만 사용할지 current schema 2 계약과 대조했다.

#### 진입 전 정책 결정 — retired save compatibility

공개 배포된 이전 save schema가 없으므로 기본 권고는 다음과 같다.

1. `RetiredCampaignSaveCompatibilityPolicy`의 public compatibility 필요가 없음을 제품/릴리스 정책과
   다시 확인한다.
2. 확인되면 `stage-5-1 -> current final stage` 자동 보정, 관련 runtime branch, test, current-policy
   표현을 별도 scoped cleanup으로 제거한다.
3. archived `legacy-stage-5-1` content/catalog governance는 save migration과 별개이므로 그대로 둔다.
4. 내부 QA 파일이 필요하면 정확한 pre-release reset으로 처리하고 public migration abstraction을
   만들지 않는다.

제품이 내부 save retention을 명시적으로 선택한 경우에만 retired-stage 변환을 typed maintenance
plan으로 유지한다. 그 결정 없이 generic migration framework를 만들지 않는다.

2026-08-25 entry 결정: canonical pre-release policy에서 공개 이전 save가 없고 내부 QA save가
compatibility target이 아님을 재확인했다. 별도 retention 요청도 없으므로
`RetiredCampaignSaveCompatibilityPolicy`, `stage-5-1 -> current final stage` runtime branch, migration
test/current-policy 표현을 scoped cleanup으로 제거했다. `stage-5-1` cursor는 이제
`StageMissingFromSequence`로 fail-closed하고 sync/write를 요청하지 않는다. Catalog-only
`legacy-stage-5-1` content와 alias/catalog governance는 변경하지 않았다. 후속 4A에서 read-only
evaluator/action-policy 분리, 4B mutation port 축소, 4C UI result migration과 facade 제거까지 완료했다.

#### 결과 타입 분리

- profile read failure: 기존 `CampaignSaveLoadReport`
- slot structural parse: Phase 3 parser result
- empty/occupied: `CampaignSlotEntry`
- launch evaluation: `CampaignSlotLaunchEvaluation`
- UI action availability: 별도 `CampaignSlotActionPolicy`
- persisted correction: typed maintenance/launch-preparation command

`UnsupportedVersion`은 profile report에만 남기고 per-slot validation status에서 제거한다.
corrupt raw data는 diagnostic에 남기며 `CreateEmpty(1)` 같은 fallback state로 바꾸지 않는다.

#### launch evaluation 계약

권장 status:

- `Empty`
- `Ready`
- `Completed`
- `StageMissingFromSequence`
- `StageMissingFromCatalog`
- `LevelGroupSynchronizationRequired`

evaluator는 state와 sequence/catalog를 받아 resolved stage/group과 action 가능 여부를 반환하지만
repository를 쓰지 않는다. `CurrentLevelGroupId`의 structural normalization과 current sequence 일치
여부를 구분한다.

#### narrow maintenance command

level-group sync가 필요하면 full state를 넘기지 않고 최소 command를 만든다.

```csharp
CampaignLevelGroupSyncCommand(
    int slotNumber,
    StageId expectedStageId,
    string expectedPersistedLevelGroupId,
    string targetLevelGroupId);
```

service는 transaction 안에서 slot을 다시 읽고 expected stage/group을 확인한 뒤 group 하나만
변경한다. 다른 필드가 바뀌었거나 precondition이 stale이면 no-write failure를 반환한다. 가능하면
MainMenu 전용 `PrepareContinue` command가 이 sync와 committed-state 재확인을 한 transaction으로
수행하고 결과 state를 반환하게 한다.

#### MainMenu Continue 목표 흐름

```text
load profile/entry
  -> structural parse result 확인
  -> sequence/catalog launch evaluation
  -> handoff reservation
  -> 필요한 경우 preconditioned narrow sync/prepare
  -> returned committed identity 재확인
  -> route accept
  -> stale/failure면 exact handoff clear + refresh
```

UI는 raw diagnostic이나 mutable slot을 고치지 않는다. presentation mapper는 entry, evaluation,
localized descriptor를 받아 card read model만 만든다.

#### Work package 4A — read-only service 분리

1. `SaveSlotValidationService.Validate`를 parser/entry/evaluator/action policy 조합으로 대체한다.
2. `SaveSlotValidationResult` compatibility facade는 기존 UI migration 동안만 새 result들을 조합한다.
3. `CanContinue`, `CanRestart`, `CanDelete`를 status 편의 property가 아니라 action policy로 이동한다.
4. `UnsupportedVersion`과 profile corruption은 slot enum에서 제거한다.

2026-08-25 구현 결과: repository-free `CampaignSlotLaunchEvaluator`가 immutable entry/state와
sequence/catalog만 읽고 `Empty`, `Ready`, `Completed`, sequence/catalog missing,
`LevelGroupSynchronizationRequired`를 반환한다. 입력 state는 그대로 보존하고 resolved stage/group만
evaluation에 담는다. Continue/Restart/Delete 가능 여부는 `CampaignSlotActionPolicy`가 소유하며 기존
`SaveSlotValidationResult`의 convenience property는 이 policy에 delegate한다. Service는 structural raw
classification 뒤 valid entry를 evaluator/policy에 전달하고, 기존 UI를 위해서만 corrected legacy clone과
`RequiresSaveSync`를 조합한다. `UnsupportedVersion` slot enum/UI mapping은 제거하고 profile load report의
unsupported-version presentation은 유지했다. Repository write와 MainMenu full replacement는 4A에
포함하지 않았으며 4B/4C residual이다.

#### Work package 4B — mutation port 축소

1. MainMenu의 `ICampaignSlotMaintenancePort` 의존을 narrow preparation/sync port로 교체한다.
2. editor DirectPlay는 diagnostic initialize command를 사용한다.
3. standalone seed는 import command와 state factory를 사용한다.
4. test는 public maintenance port 대신 fixture/factory 또는 internal test seam을 사용한다.
5. 마지막 consumer가 이동하면 `ReplaceValidatedSlot` port를 제거한다.

2026-08-25 구현 결과: `ICampaignSlotMaintenancePort`와 validation service의 `ValidateAndSync`를
제거하고 `CampaignContinuePreparationCommand`/`ICampaignContinuePreparationPort`로 교체했다.
command는 slot, expected stage, expected persisted group, target group만 전달한다. Production service는
profile mutation 안에서 slot을 다시 parse하고 completed/stage/group precondition을 확인하며, 필요한
경우 `CurrentLevelGroupId`만 바꾼 immutable state를 저장한다. 이미 current인 group은 write 없이 committed
state를 반환하고 missing/stale/completed는 no-write failure다. Transient는 같은 pure preparation policy를
기존 `Gate` 안에서 사용한다. MainMenu는 handoff 예약 뒤 command를 실행하고 반환된 slot/stage/group과
completed 상태를 재확인한 뒤에만 route한다. failure는 original token만 clear하고 view model을 refresh하며,
그 사이 생긴 newer handoff는 보존한다. DirectPlay/standalone seed는 3C에서 이미 diagnostic/import command로
이동했으므로 재작업하지 않았다. complete replacement는 public runtime port가 아니라 concrete
compatibility와 internal `CampaignSaveTestFixture` setup seam으로만 남겨 Phase 5 제거 대상으로 한정했다.

#### Work package 4C — UI migration

1. `MainMenuController`는 raw slot mutation이나 full replacement를 하지 않는다.
2. `MainMenuSlotViewModelMapper`는 `CampaignSlotEntry`와 launch/action result를 받는다.
3. blocked profile screen과 per-slot launch failure를 다른 presentation path로 유지한다.
4. restart/delete/new-game confirmation과 handoff ownership 계약을 회귀시키지 않는다.

2026-08-25 구현 결과: `MainMenuController`가 immutable `CampaignSlotEntry`를
`CampaignSlotLaunchEvaluator`로 평가하고 `CampaignSlotActionPolicy`를 별도로 파생한다.
`MainMenuSlotViewModelMapper`는 이 세 결과를 묶은 `MainMenuSlotPresentationInput`만 받아 card read model을
만들며 sequence resolver나 combined validation facade를 내부에서 호출하지 않는다. input은 entry/evaluation
state identity와 supplied action-policy 일치를 검증한다. Profile corruption/schema/IO failure는 기존
`CampaignSaveLoadReport` global blocked path를 유지하고 sequence/catalog failure는 occupied per-slot card의
restart/delete path로 유지했다. completed state는 group synchronization이 필요해도 Completed card로 표시된다.
`SaveSlotValidationService`, `SaveSlotValidationResult`, `SaveSlotValidationStatus` source/meta와 corrected mutable
clone 경로는 제거했다. Continue의 narrow preparation, restart/delete/new-game confirmation, exact handoff token
ownership은 변경하지 않았다. Concrete/internal complete-replacement와 fixture projection은 Phase 5에 남는다.

예상 touch set:

- `SaveSlotValidationService.cs` 또는 이를 대체하는 evaluator/action-policy 파일
- `CampaignSavePorts.cs`의 maintenance/preparation port
- `CampaignSaveService.cs`와 adapter의 narrow command
- `MainMenuController.cs`
- `MainMenuSlotViewModelMapper.cs`
- `MainMenuUiFlowInstaller.cs`
- `StageEditorDirectPlayLauncher.cs`, `PlayerCaptureLaunchBootstrap.cs`의 목적별 bootstrap path
- Stages/UI DirectPlay, validation, MainMenu, handoff tests
- retired compatibility를 제거할 경우 해당 policy/test/current docs의 별도 scoped change set

#### 검증

- launch evaluation pure matrix
- level-group sync success/stale/no-write transaction tests
- retired policy 제거 또는 유지 결정에 맞춘 targeted tests/docs
- MainMenu Continue/Restart/Delete/NewGame/blocked recovery UI tests
- pending handoff stale callback tests
- DirectPlay/seed initialization tests
- `SaveSlotValidationAndDirectPlayTests`
- `CampaignProductionEntryTests`
- `./run_tests.sh core`
- `./run_tests.sh ui`

#### Exit / 중단 기준

Exit:

- structural validity, launch eligibility, UI action policy가 별도 result/type이다.
- read-only evaluator가 repository write를 직접 호출하지 않는다.
- MainMenu가 full slot replacement를 요청하지 않는다.
- maintenance command가 expected identity를 검증하고 한 필드/의도만 변경한다.
- profile-level failure가 per-slot status에 섞이지 않는다.
- retired compatibility의 유지/제거 결정과 코드/문서가 일치한다.

중단:

- retired save retention 여부가 결정되지 않은 경우
- sync와 handoff의 원자성 정책이 기존 UI contract와 충돌하는 경우
- per-slot corruption을 profile-level blocked 상태와 분리할 제품 UX 결정이 필요한 경우

Rollback: 4C 시작 전 기록된 4B working-tree 상태로 package 전체를 되돌린다. 제거된 combined facade를
부분적으로 복원해 새 consumer와 병행하지 않는다. Concrete full replacement 제거는 Phase 5 inventory와
runtime consumer gate 뒤에만 한다.

### Phase 5 — compatibility shim 제거와 구조 gate 고정

#### 목적

새 구조가 자리 잡은 뒤 old mutable path와 naming-only contract를 제거하고 재유입을 자동으로 막는다.

#### 진입 조건

- Phase 4 runtime/UI consumer migration이 same revision에서 green이다.
- `SaveSlotData`, old mapper fragments, `ReplaceValidatedSlot`, old validation status의 남은 참조가
  allowlist와 함께 조사되어 있다.
- 이전 Phase의 green revision이 rollback point로 기록되어 있다.

#### Work package 5A — shim 제거

1. mutable `SaveSlotData` runtime business 사용을 제거한다.
2. exact clone은 raw diagnostic/compatibility DTO에 정말 필요한 경우만 남긴다.
3. old mapper facade와 public fragment API를 제거한다.
4. `ICampaignSlotMaintenancePort.ReplaceValidatedSlot`과 forwarding wrapper를 제거한다.
5. compatibility allowlist가 비면 adapter와 obsolete test fixture를 삭제한다.

#### Work package 5B — architecture guard

reflection/behavior guard를 우선하고 source string guard는 좁은 금지 패턴에만 사용한다.

- `CampaignSlotState`와 nested state에 public setter가 없다.
- state constructor는 factory/parser/engine 외부에서 접근할 수 없다.
- Production/Transient가 동일 transition engine을 조합한다.
- service/Transient/UI/DirectPlay에 gameplay slot field 직접 assignment가 없다.
- persistence mapper에 invalid-element `continue`, negative clamp, `Normalize` 호출이 없다.
- runtime consumer가 public fragment mapper나 full replacement port를 요청하지 않는다.
- campaign persistence composition에 PlayerPrefs backend/import/delete가 없다.
- saved `RemainingChances`에 `0` sentinel이 없다.

마지막 guard는 save document/state/command 경계만 검사한다. terminal presentation의 일시적 chance display
`0`까지 금지하면 정상 연출을 오탐하므로 broad text scan을 사용하지 않는다.

#### Work package 5C — 문서와 evidence closeout

1. Architecture README current flow를 실제 engine/state/evaluator 경로로 갱신한다.
2. pre-release policy의 clone/canonicalization 설명을 immutable state 계약으로 갱신한다.
3. testing guide에 same-revision touched evidence를 추가한다.
4. historical 문서가 current truth처럼 보이면 archive/superseded 표기를 보강한다.
5. broad `full`을 실행하지 않았다면 touched-cluster/core/UI 결과만 보고한다.

예상 touch set:

- compatibility `SaveSlotData`/adapter/shim 파일
- `CampaignSaveArchitectureV2Tests.cs` 또는 전용 architecture guard
- 필요 시 `Tools/check_campaign_save_architecture.*`와 runner 연결
- `Docs/Architecture/README.md`
- `Pre-Release-Save-Baseline-Policy.md`
- `Gameplay-Test-Automation-Guide.md`

source guard를 위해 새 도구를 추가할지는 기존 reflection/source test로 충분하지 않을 때만 결정한다.

#### 검증

- architecture/reflection/source guards
- transition/parser/mapper/evaluator 전체 targeted fixture
- save repository/recovery/DirectPlay targeted fixture
- achievement integration fixture
- `./run_tests.sh core`
- `./run_tests.sh ui`
- 위험도에 따라 filtered 또는 broad `full`; 실행하지 않은 lane은 이유 명시
- `git diff --check`

#### Exit / 중단 기준

Exit:

- authoritative gameplay slot transition owner가 하나다.
- canonical state 생성 owner가 parser/factory/engine으로 제한된다.
- general-purpose full replacement와 mutable business carrier가 runtime surface에서 사라진다.
- strict persistence와 tolerant projection이 다시 연결될 public seam이 없다.
- 모든 guard와 필요한 validation evidence가 같은 revision에 있다.
- current 문서와 실제 composition이 일치한다.

중단:

- allowlist에 실제 runtime consumer가 남은 경우
- shim 삭제가 schema 2 round-trip이나 recovery를 깨뜨리는 경우
- broad baseline failure를 touched regression과 분리할 evidence가 없는 경우

Rollback: Phase 4 green revision으로 되돌릴 수 있게 closeout 전에 기록한다. schema를 유지하므로 save
migration rollback은 필요 없지만, 실패한 revision에서 생성된 파일이 current validator를 통과하는지
확인한 뒤 rollback한다.

### Phase별 최대 위험 요약

| Phase | 최대 위험 | 조기 탐지 신호 | 대응 |
| --- | --- | --- | --- |
| 0 | 기존 버그를 canonical behavior로 고정 | Production/Transient expected가 다름 | 제품 계약을 먼저 결정하고 test 작성 중단 |
| 1 | engine 호출이 transaction/lock 밖으로 이동 | stale/no-write test에서 save 발생 | engine 계산과 replace를 동일 critical section으로 복귀 |
| 2 | JsonUtility null semantics 손실 | absent receipt/null array round-trip 변화 | validator 후 materializer 순서와 presence flag 유지 |
| 3 | nested collection을 통해 immutable state가 변경됨 | 외부 mutation test에서 state 변화 | private copy + read-only wrapper, raw array 미노출 |
| 4 | MainMenu sync가 handoff보다 오래된 state를 덮음 | stale sync 뒤 route 진행 | expected identity command + failure 시 exact handoff clear |
| 5 | broad source guard가 presentation/test를 오탐 | save와 무관한 `0`/setter failure | guard scope를 type/file/semantic boundary로 축소 |

## 8. 전이 테스트 매트릭스

최소한 다음 행을 pure engine test로 유지한다.

| 전이 | 입력 핵심 | 기대 결과 |
| --- | --- | --- |
| Death, chance 3 | current stage, 3 | same retry route, chance 2, deaths +1 |
| Death, chance 2 | current stage, 2 | same retry route, chance 1, deaths +1 |
| Death, last chance | current stage, 1 | group first stage, chance 3, deaths +1, 중간 0 없음 |
| Death, stale plan | current state와 expected 불일치 | typed failure, state unchanged |
| Clear, same group | non-final clear | next stage, chance 유지 |
| Clear, next group | group boundary clear | next stage, chance 3 복원 |
| Clear, final | final stage | completed true, stage/receipt 계약 유지 |
| Clear, first receipt | receipt 없음 + valid receipt | receipt 한 번 기록 |
| Clear, present-null receipt | presence true + payload null | payload를 합성/덮어쓰기하지 않고 상태 보존 |
| Clear, repeated receipt | 기존 receipt 있음 | 기존 provenance 보존 |
| Clear, performance better | 기존보다 작은 usage | best value 갱신 |
| Clear, performance worse | 기존보다 큰 usage | 기존 best value 보존 |
| Clear, processed IDs | dormant ID collections 존재 | 모든 ID와 record가 그대로 보존 |
| Invalid current state | chance/counter/nested shape invalid | typed failure, state 생성/write 없음 |
| Invalid request | stage/receipt/performance 불일치 | typed failure, state unchanged, write 없음 |

추가 경계 테스트:

- document parser가 null/invalid/duplicate nested element를 fail-closed한다.
- exact raw diagnostic copy가 invalid shape와 order를 보존한다.
- state-to-document-to-state round-trip이 canonical state와 동일하다.
- blocked profile mutation은 writer를 호출하지 않는다.
- Production JSON root와 temporary DirectPlay JSON root가 같은 domain/mapper/engine을 조합한다.
- Transient는 같은 engine을 사용하지만 Production DirectPlay persistence로 오인하지 않는다.
- comic completion은 duplicate call, expected-stage match/mismatch, intro/outro 독립성을 검증한다.
- diagnostic stage selection은 gameplay transition test와 분리하고 snapshot record 생성 및 다른 필드
  보존을 검증한다.
- level-group sync는 expected identity가 stale일 때 full replacement나 부분 write를 하지 않는다.

## 9. 검증 lane과 증거 기준

각 구현 phase는 최소 다음을 실행한다.

1. 변경된 pure transition/parser test의 filtered fixture
2. save mapper/service/adapter/validation/DirectPlay touched-cluster fixture
3. `./run_tests.sh core`
4. UI-facing result/port가 바뀌면 `./run_tests.sh ui`
5. broad integration 위험이 있으면 `./run_tests.sh full --filter <fixture>` 또는 별도 full lane

문서 작성 시점의 참고 baseline은 다음과 같다.

- core: EditMode `217/0`, PlayMode `109/0`
- UI: EditMode `1353/0`
- `CampaignSlotMapperTests`: `25/0`
- `CampaignSaveSlotStoreAdapterTests`: `39/0`
- `SaveSlotValidationAndDirectPlayTests`: `33/0`

이 수치는 2026-08-24 working-tree evidence이며 미래 phase의 pass를 대신하지 않는다. 각 phase는
동일 revision에서 새로 실행한 결과를 기록해야 한다. broad `full`을 실행하지 않았으면
project-wide/full green을 주장하지 않는다.

## 10. 공개 배포와 schema 처리

현재 공개 배포된 이전 save schema는 없다. 따라서 이 구조 개선을 위해 public legacy migration,
PlayerPrefs importer, sentinel compatibility를 만들 이유가 없다.

다만 schema 2는 첫 공개 목표 계약으로 이미 코드·문서·테스트에 고정되어 있다. 구조 개선의 목적은
runtime semantic ownership을 정리하는 것이므로 기본 방침은 physical `profile.json` schema 2를
유지하는 것이다. physical field를 바꾸어야 한다면 이 계획에 묻어서 변경하지 말고 별도
schema/release review로 결정한다.

내부 QA save는 public compatibility 대상이 아니다. 구조 전환 중 내부 QA reset이 필요하면
정확한 `Saves` 파일 범위와 별도 승인을 사용하며, audio/display/input/locale PlayerPrefs를
삭제하지 않는다.

## 11. 금지되는 단기 우회책

- `Clone()`에서 validation이나 normalization을 다시 수행하지 않는다.
- invalid chance를 `1..3`으로 clamp하여 저장하지 않는다.
- `0`을 death exhausted, DirectPlay, empty slot의 sentinel로 다시 도입하지 않는다.
- Production과 Transient에 동일 mutation patch를 복사해 parity test만 추가하지 않는다.
- public fragment mapper에서 invalid persisted element를 skip하여 “복구”하지 않는다.
- corrupted slot을 empty slot으로 바꾼 뒤 정상 domain path로 전달하지 않는다.
- `ReplaceValidatedSlot(SaveSlotData)` 같은 naming-only contract를 새 API에 반복하지 않는다.
- 구조 개선을 이유로 processed-ID physical field를 ordinary cleanup으로 제거하지 않는다.
- campaign PlayerPrefs compatibility/import/delete를 되살리지 않는다.

## 12. 전체 완료 정의

장기 개선은 다음 조건이 모두 충족될 때 완료로 본다.

- death, clear, comic progress 등 authoritative slot transition의 규칙 owner가 명시적으로 하나다.
- Production과 Transient는 동일 engine을 조합하며 business mutation을 각각 구현하지 않는다.
- canonical occupied state는 immutable하고 항상 valid + canonical이다.
- empty/occupied는 slot entry로, corrupt raw data는 diagnostic으로, unsupported schema는 profile load
  report로 구분된다.
- persistence mapper는 strict serialization만 수행하고 validation/normalization/filtering을 하지 않는다.
- validation, launch eligibility, maintenance planning, persistence write가 별도 책임이다.
- DirectPlay는 별도 규칙 복제 없이 동일 domain engine과 strict chance contract를 사용한다.
- `SaveSlotData`와 compatibility shim의 runtime business 참조가 제거되거나 명시된 raw boundary로 제한된다.
- 같은 revision의 touched-cluster/core/UI 필요 lane 증거와 source guard가 있다.
- current architecture 문서가 실제 구현과 일치한다.

### 12.1 Final whole-Goal audit — 2026-08-25 KST

| Original requirement | Same-working-tree evidence | Result |
| --- | --- | --- |
| `profile.json` schema 2와 `local-launch-state.json` schema 1 유지 | `CampaignProfileDocument.CurrentSchemaVersion = 2`, `CampaignLocalLaunchStateRepository.SchemaVersion = 1`, repository/architecture round-trip guards | Satisfied; physical contract retained |
| Production/DirectPlay JSON-backed, Transient는 test/diagnostic composition | Production/temporary composition 모두 `CampaignSaveFacadeFactory -> FileCampaignProfileRepository`; DirectPlay launcher는 `CreateProductionProfileBacked`/`CreateTemporaryProfileBacked`; architecture composition guards | Satisfied |
| death/clear/comic authoritative transition owner 하나와 Production/Transient parity | 두 store가 transaction/lock 안에서 동일 `CampaignSlotTransitionEngine.Apply*`를 호출; transition/parity fixtures가 15-fixture `472/0`에 포함 | Satisfied |
| canonical state/nested state immutable, 생성 owner 제한 | public constructor/setter reflection guard와 construction source guard; construction은 `CampaignSlotState.cs` factory/parser와 engine으로 제한 | Satisfied |
| Empty/Occupied, corrupt diagnostic, unsupported profile schema 분리 | `CampaignSlotParser`, `CampaignSlotEntry`, `CampaignSlotDiagnostic`, `CampaignSaveLoadReport`와 parser/repository recovery fixtures | Satisfied |
| malformed persisted input은 normalization 전 fail-closed, blocked query/mutation은 no-write | repository malformed matrix, service invalid-loaded-status, adapter recovery-pending and blocked-seed no-write tests | Satisfied |
| strict mapper와 separated validation/evaluator/action/maintenance/persistence | `CampaignSlotStateDocumentMapper` one-surface/no-repair guard; repository-free `CampaignSlotLaunchEvaluator`, `CampaignSlotActionPolicy`, expected-identity Continue preparation command | Satisfied |
| general full replacement/mutable compatibility shim 제거, raw DTO 격리 | runtime old-token/raw-consumer source guards; `SaveSlotData` runtime references는 `SaveSlotModels.cs`와 explicit `CampaignSlotRawDataMapper` boundary로 제한 | Satisfied |
| chance `1..3`, last chance atomic reset, saved zero/clamp 금지 | transition truth table, seed/document/parser boundary guards, saved-zero source scan | Satisfied |
| processed IDs와 receipt/raw clone 계약 보존 | service processed-ID preservation, receipt 3-state transition/parser tests, raw clone malformed/null/duplicate/order deep-copy tests | Satisfied |
| campaign PlayerPrefs compatibility/import/delete와 public legacy migration 부재 | production composition/token/type guards와 pre-release no-repair tests; settings PlayerPrefs는 범위 밖으로 유지 | Satisfied |
| current docs와 same-working-tree validation evidence | README/pre-release/sequence/testing reconciliation; focused architecture `114/0`, 15-fixture `472/0`, core `217/0 + 109/0`, UI `1352/0`, source audits, generated-scene audit, `git diff --check` | Satisfied; broad unfiltered `full` 및 manual Player/build smoke는 실행하지 않았으므로 그 범위는 claim하지 않음 |

Final verdict: 위 original objective와 전체 완료 정의를 모두 충족했다. 남은 항목은 이 Goal의
구조 계약이 아니라 broad baseline recovery, cross-process writer coordination, 새 schema/release
결정, manual Player/build 검증처럼 명시적으로 실행하지 않았거나 범위 밖인 후속 작업이다.

후속 독립 감사에서 확인한 test-truth 교정과 current-state 재검증은 13.5를 따른다.

## 13. 권장 Goal 시작 단위와 실행 기록

첫 Goal turn은 Phase 0의 transition characterization만 수행한다. engine을 바로 추가하면 기존
Production/Transient 차이를 새 engine의 정답으로 잘못 고정할 수 있다.

첫 change set의 권장 touch set:

- `Assets/_Features/Stages/Editor/Tests/CampaignSaveSlotStoreAdapterTests.cs`
- 필요하면 신규 `CampaignSlotTransitionCharacterizationTests.cs`와 `.meta`
- Transient deterministic clock을 위한 최소 internal seam
- 이 문서의 Phase 0 실행 기록

첫 change set에서 금지할 것:

- `CampaignSaveService`/Transient의 field mutation 이동
- mapper visibility 변경
- immutable state 추가
- schema/document 수정
- retired-stage compatibility 제거

Phase 0이 green인 다음 Goal turn에서 Phase 1의 `CampaignSlotTransitionEngine` 추출을 시작한다. 이
단계는 persisted schema와 public UI contract를 바꾸지 않으면서 가장 큰 중복 원인을 제거한다.

Phase 1 첫 work package의 권장 touch set:

- `Assets/_Features/Stages/Runtime/Campaign/CampaignSlotTransitionEngine.cs` 신규 추가
- Unity가 생성하는 matching `.meta`
- `CampaignSaveService`의 death/stage-clear 적용부를 engine 호출로 교체
- `TransientCampaignSaveSlotStore`의 동일 적용부를 engine 호출로 교체
- engine truth-table test 추가
- 기존 service/adapter/parity test 유지
- Architecture README에는 실제 구현 완료 후 current flow만 갱신

immutable state와 mapper visibility 변경은 같은 PR에 넣지 않는다. 먼저 single transition owner를
확립하고, 그 위에서 Phase 2와 Phase 3을 각각 진행해야 실패 원인과 rollback 지점을 명확히 유지할
수 있다.

### 실행 기록 템플릿

각 Phase/work package 종료 시 아래 표에 한 행을 추가한다. `Pass`만 쓰지 말고 실행 수와 실패 수,
matching test가 0개였는지, 실행하지 않은 lane과 이유를 기록한다.

| Date / KST | Phase / package | Revision 또는 working-tree identity | 변경 의도 | Tests run | Tests not run / reason | 잔여 위험 | Decision |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 미실행 | Phase 0 / 0A | N/A | transition characterization 준비 | 없음 | 구현 시작 전 | 기존 working tree가 dirty하므로 관련 diff 보존 필요 | Ready |
| 2026-08-24 20:23 KST | Phase 0 / 0A | `3323ac0f5` + working tree; characterization SHA-256 `9a1fad8f99c1bcfa478a6557a50885fc498c0a8f44a31f8489d9fbbe998b6915` | Production/Transient death·clear truth table, receipt 3-state, performance first/better/worse/equal/duplicate, processed-ID 보존, stale/invalid no-write를 production code 변경 없이 고정 | `./run_tests.sh full --filter CampaignSlotTransitionCharacterizationTests,CampaignSaveSlotStoreAdapterTests,CampaignSaveServiceTests`: EditMode `81/0` (`18+39+24`), PlayMode matching `0`; `./run_tests.sh core`: EditMode `217/0`, PlayMode `109/0`; `git diff --check`: pass | UI lane: UI-facing code/port/result를 변경하지 않은 test-only package라 미실행; broad unfiltered `full`: touched fixture와 core gate가 범위를 충족하여 미실행 | Transient exact clock/failure-surface 세부는 0B, authoritative assignment responsibility inventory는 0C에 남음; Production/Transient inline assignment 중복은 Phase 1 전까지 의도적으로 유지 | Ready for next package |
| 2026-08-24 20:33 KST | Phase 0 / 0B | `3323ac0f5` + working tree; Transient SHA-256 `1aad2d8964769ebf1508b9ca148777872600cb9a393adf76a8c8000298f04e45`, characterization SHA-256 `6f9a3871f246d306ad949ea1c798a498d3da7cc7d1168393e5aba54c612b1fd6` | Transient internal clock seam으로 death/clear timestamp를 Production과 exact 비교하고 invalid/stale/null/slot/load/recovery failure kind와 no-write를 service·adapter·Transient surface별로 고정 | `./run_tests.sh full --filter CampaignSlotTransitionCharacterizationTests,CampaignSaveSlotStoreAdapterTests,CampaignSaveServiceTests`: EditMode `88/0` (`25+39+24`), PlayMode matching `0`; `./run_tests.sh core`: EditMode `217/0`, PlayMode `109/0`; `git diff --check`: pass | UI lane: UI-facing code/port/result를 변경하지 않아 미실행; broad unfiltered `full`: failure/time characterization과 core gate가 package 범위를 충족하여 미실행 | adapter message 전체는 public contract 근거가 없어 고정하지 않고 exception type/status만 고정함; authoritative assignment responsibility inventory와 Phase 0 exit audit는 0C에 남음 | Ready for next package |
| 2026-08-24 20:42 KST | Phase 0 / 0C | `3323ac0f5` + working tree; inventory pre-log SHA-256 `9b308fc243c309acf84197d452fc970cdea69dc670226a7812f6cb6954ef5088`, Transient `1aad2d8964769ebf1508b9ca148777872600cb9a393adf76a8c8000298f04e45`, characterization `6f9a3871f246d306ad949ea1c798a498d3da7cc7d1168393e5aba54c612b1fd6` | runtime slot assignment를 gameplay transition/factory/maintenance/representation mapping으로 분류하고 bypass·0-sentinel·campaign PlayerPrefs source scan과 Phase 0 exit audit를 완료; 0C production code 변경 없음 | `./run_tests.sh full --filter CampaignSlotTransitionCharacterizationTests,CampaignSaveSlotStoreAdapterTests,CampaignSaveServiceTests`: EditMode `88/0` (`25+39+24`), PlayMode matching `0`; `./run_tests.sh core`: EditMode `217/0`, PlayMode `109/0`; `git diff --check`: pass | UI lane: documentation-only 0C이고 UI-facing code를 변경하지 않아 미실행; broad unfiltered `full`: Phase 0 filtered parity/failure fixture와 core gate가 package 범위를 충족하여 미실행 | inline Production/Transient transition 중복과 maintenance full-slot seam은 Phase 1 이후 remediation 대상으로 의도적으로 남음; full/broad regression은 증명하지 않음 | Ready for next package |
| 2026-08-24 20:57 KST | Phase 1 / 1A | `3323ac0f5` + working tree; engine SHA-256 `83f5a5fa53ca38e8369ad2f2ba457d01d3d20d7952263a0db224f527c14d365d`, fixture SHA-256 `b11851c88bde83f2c518c3ba80e61659e2b45228847a2466e9a4c4b49f0e0bd5` | repository/clock/Unity/composition을 참조하지 않는 pure engine과 typed failure/result를 추가하고 death·clear truth table, invalid/stale, input 불변성, receipt/performance/nested preservation을 30-case fixture로 고정; Production/Transient wiring은 변경하지 않음 | `./run_tests.sh full --filter CampaignSlotTransitionEngineTests,CampaignSlotTransitionCharacterizationTests,CampaignSaveSlotStoreAdapterTests,CampaignSaveServiceTests`: EditMode `118/0` (`30+25+39+24`), PlayMode matching `0`; `./run_tests.sh core`: EditMode `217/0`, PlayMode `109/0`; purity/assignment/0-sentinel/schema source scan과 `git diff --check`: pass. 최초 engine-only 시도는 private nested test enum 접근성 compile error로 XML 없이 실패했고 접근성을 수정한 뒤 최종 결합 run이 통과 | UI lane: UI-facing port/result/controller를 변경하지 않아 미실행; broad unfiltered `full`: engine/characterization/service/adapter filtered gate와 core가 1A 범위를 충족하여 미실행 | service와 Transient의 inline death/clear branch는 1B/1C까지 의도적으로 남음. `int.MaxValue` death counter는 engine에서 typed fail-closed지만 아직 runtime 미연결이며, 1B/1C mapping에서 기존 no-write/exception surface를 보존해야 함 | Ready for next package |
| 2026-08-24 21:09 KST | Phase 1 / 1B | `3323ac0f5` + working tree; service SHA-256 `7d280104d29a44219eb1987588d059554be70f820f953164993a4057be8bbac0`, engine `83f5a5fa53ca38e8369ad2f2ba457d01d3d20d7952263a0db224f527c14d365d`, service fixture `9505e5e21a9423c250ca582d926a636f3085da08cba2732199e1a50041523a0c` | Production death·clear를 repository mutation 내부의 common engine 호출로 교체하고 success만 document로 매핑; typed invalid/stale는 기존 `InvalidRequest`/no-write로, death counter overflow는 기존 `ArgumentException`/no-write로 변환하며 slot/profile에 단일 clock 값을 사용 | tests-first 구조 guard는 변경 전 service fixture EditMode `28/1`로 의도대로 실패; wiring 후 `./run_tests.sh full --filter CampaignSaveServiceTests`: EditMode `28/0`, PlayMode matching `0`; `./run_tests.sh full --filter CampaignSlotTransitionEngineTests,CampaignSlotTransitionCharacterizationTests,CampaignSaveSlotStoreAdapterTests,CampaignSaveServiceTests`: EditMode `122/0` (`30+25+39+28`), PlayMode matching `0`; `./run_tests.sh core`: EditMode `217/0`, PlayMode `109/0`; engine purity, Production/Transient assignment, campaign PlayerPrefs/0-sentinel, schema-scope source scan과 `git diff --check`: pass | UI lane: UI-facing port/result/controller를 변경하지 않아 미실행; broad unfiltered `full`: 1B touched fixtures와 core gate가 범위를 충족하여 미실행 | Transient death/clear의 inline branch는 1C까지 의도적으로 남아 Phase 1 single-owner exit는 아직 미충족; physical schema/document 변경 없음. Unity runner가 각 PlayMode run 뒤 생성된 `InitTestScene` 2개를 탐지·제거했고 최종 잔존 없음 | Ready for next package |
| 2026-08-24 21:22 KST | Phase 1 / 1C | `3323ac0f5` + working tree; Transient SHA-256 `19a3dafbfc6daffbb1b127645f00ee7cc400edd2dd414f104b4da94e3234b95a`, engine `86aad7912a3298e31c4c482bb3089ef35acb2ad1284a619d222e4af55c38bc86`, characterization `c01d8f4a9ef5ad736e8dd286df6fcfb7a281c1e0684ee6e658923aaed32966eb`, engine fixture `ca044b5dcdd927dcc255dcb58ea4ed376c34da3f1fcd3544ca7f7c0dfc2b41c2` | Transient death·clear의 inline validation/mutation Core를 제거하고 같은 `Gate` 안에서 load, 단일 clock, common engine, validated replacement를 수행; typed failure를 기존 null/invalid/stale/overflow exception surface로 변환하고 null request reason을 domain에서 구분 | tests-first engine+characterization gate는 wiring 전 EditMode `59/1`로 구조 guard만 의도대로 실패; wiring 후 같은 gate EditMode `59/0` (`30+29`), PlayMode matching `0`; `./run_tests.sh full --filter CampaignSlotTransitionEngineTests,CampaignSlotTransitionCharacterizationTests,CampaignSaveSlotStoreAdapterTests,CampaignSaveServiceTests`: EditMode `126/0` (`30+29+39+28`), PlayMode matching `0`; `./run_tests.sh core`: EditMode `217/0`, PlayMode `109/0`; engine-consumer/assignment/purity, campaign PlayerPrefs/0-sentinel/schema-scope scan과 `git diff --check`: pass | UI lane: UI-facing port/result/controller를 변경하지 않아 미실행; broad unfiltered `full`: Phase 1 touched fixtures와 core exit gate가 범위를 충족하여 미실행 | Phase 1 death/clear single-owner exit는 충족. comic/diagnostic transition, mutable `SaveSlotData`, mapper/canonicalizer 임시 결합과 full-slot maintenance seam은 계획대로 후속 Phase에 남음; physical schema/document 변경 없음. Unity runner가 PlayMode run 뒤 생성한 `InitTestScene`을 탐지·제거했고 최종 잔존 없음 | Ready for next package |
| 2026-08-24 21:35 KST | Phase 2 / entry audit | `3323ac0f5` + working tree; audit-section pre-log SHA-256 `1f96d755efaec9932814ae005d690c985400bb96d576795db0e01d17481ceb49`, architecture fixture `952cc85f7e9440bab624c2ad9d89377ae713e50b64f2c83f71b9837be3361ad0`, receipt fixture `ed667fc6341521bc889c109262d527945aeb75de7971de0c7b4f30737e6ffbe4` | repository 전체 mapper caller를 complete/fragment path로 분류하고 validator 허용 null과 post-validation materialization owner를 표로 고정; production/runtime fragment caller가 없음을 확인하고 stale architecture guard와 schema 2 raw JSON fixture의 필수 chance를 현 계약에 맞춤. production runtime 및 physical schema 변경 없음 | 최초 `./run_tests.sh full --filter CampaignSlotMapperTests,CampaignSaveArchitectureV2Tests,NormalCampaignCompletionReceiptTests,CampaignStageAchievementIntegrationTests`: EditMode `140/3` (common engine 호출을 old service API로 오인한 guard 1건, `RemainingChances`가 빠진 raw fixture 2건); fixture 교정 후 동일 filter: EditMode `140/0`, PlayMode matching `0`; `./run_tests.sh core`: EditMode `217/0`, PlayMode `109/0`; fragment caller/load-order/schema-scope scan과 `git diff --check`: pass | UI lane: documentation/test-only audit이며 UI-facing runtime/port/result를 변경하지 않아 미실행; broad unfiltered `full`: mapper/receipt/achievement filtered fixture와 core gate가 audit 범위를 충족하여 미실행 | public fragment facade, repository/service 중복 materialization, tolerant performance/profile projection은 각각 2A/2B/2C에 남음; 임의 repository가 잘못된 `Loaded`를 반환할 때 service `Normalize`가 repair처럼 동작할 위험도 2B까지 남음. full/broad regression은 증명하지 않음 | Ready for next package |
| 2026-08-24 21:46 KST | Phase 2 / 2A | `3323ac0f5` + working tree; mapper SHA-256 `94664275fbdafd6082f87c6e7285eccf6939eaaec3e6490bbac0a375b41ed0ae`, architecture fixture `b25a9109c9c44d8457bd29f2ad6f92e15c35fa5a85024edf47092bf444fce984`, receipt fixture `6400de300473023a8a46654194381714be3265f6b58e69d5943d359bfab48b04`, characterization `69a4d8c9990d6c05f7603179174f7aee0f93935eeb94b87a47f4115dbaab9911`, README `e3e4038323254af7103c87539c2571287c8cafd50187680991299afa440ae4ff`, plan pre-log `0544e1c1942ebd71e721dd0911580acfa9d722c59b0b711d0a424bfbc86d5260` | public persistence mapper를 complete profile/slot 두 경로로 축소하고 receipt/performance/stage-clear facade를 제거; 내부 fragment 변환은 private helper로 제한하고 직접 fragment fixture를 complete slot/profile round-trip으로 이동. reflection guard와 current README에 surface를 고정했으며 변환 정책과 physical schema는 변경하지 않음 | tests-first `./run_tests.sh full --filter CampaignSaveArchitectureV2Tests`: EditMode `98/1`로 새 surface guard만 의도대로 실패; 구현 후 `./run_tests.sh full --filter CampaignSlotMapperTests,CampaignSaveArchitectureV2Tests,NormalCampaignCompletionReceiptTests,CampaignSlotTransitionCharacterizationTests,CampaignSaveSlotStoreAdapterTests,CampaignStageAchievementIntegrationTests`: EditMode `209/0`, PlayMode matching `0`; `./run_tests.sh core`: EditMode `217/0`, PlayMode `109/0`; Assets 전체 removed-fragment caller/public helper source scan, generated `InitTestScene` 잔존 확인, `git diff --check`: pass | UI lane: UI-facing port/result/controller/composition을 변경하지 않아 미실행; broad unfiltered `full`: six-fixture mapper/receipt/adapter/achievement/characterization gate와 core가 2A 범위를 충족하여 미실행 | repository/service의 중복 materialization과 `Loaded` 신뢰 repair 위험은 2B에 남음; private mapper helper의 tolerant skip/clamp/`Normalize`와 achievement projection 혼합은 2C까지 남음. public validation 우회 surface는 제거되었으나 Phase 2 전체 exit는 아직 미충족 | Ready for next package |
| 2026-08-24 22:03 KST | Phase 2 / 2B | `3323ac0f5` + working tree; document/materializer SHA-256 `fa4c3caa27b3de6b39747552fe439dc6fbad9131e7cc45eb3d4cb0bc29770ecd`, repository `12905f6709c7cb960e4702630b2db40ce12894e2f1a36430ce45592dcc5d735d`, service `de1bfa7a7730f14c4cd9bbfac1ecca4e1bc481f87061233115cf427b70722f47`, architecture fixture `70e8bf44f12dcb2402147a52c7f161acc227532ad936731b42b35de94879eba0`, service fixture `a8bea59dbc666351758febd80ed131a6ac69b893281b7851892b03c0da31dccc`, README `4e74dc63225c2e0f65c6373ac79f97081aaf1534391a77279be81af6d43c7e66`, pre-release policy `4392fe4021c60cd2f3cde3678515376db6d855fe0db8c51c3beaea96d9204b5c`, plan pre-log `d741dba676a9da3d687e5f63bebf3389c8d6879d2c850703f38e413c33976c1e` | repository validation 뒤 허용된 JsonUtility null/presence만 materialize하는 단일 explicit owner를 도입하고 load/save 양 경계에서 사용; service의 중복 `Normalize`와 clamp/materializing clone을 제거해 deep clone을 exact copy로 전환. 잘못된 `Loaded`/`BackupRecovered` invalid document는 typed profile load failure로 차단하며 schema/DTO/mapper policy는 유지 | tests-first `./run_tests.sh full --filter CampaignSaveArchitectureV2Tests,CampaignSaveServiceTests`: EditMode `130/4` (materializer owner 부재 1, schema/profile repair 2, invalid chance exception 누출 1)로 의도대로 실패; 첫 구현은 신규 source가 generated csproj에 아직 없어 Windows full build `CS0103` 1건으로 test XML 전 중단했고 owner를 기존 document source에 배치한 뒤 `./run_tests.sh full --filter CampaignSaveArchitectureV2Tests,CampaignSaveServiceTests,CampaignSaveSlotStoreAdapterTests,CampaignSlotMapperTests,NormalCampaignCompletionReceiptTests,SaveSlotValidationAndDirectPlayTests`: EditMode `240/0`, PlayMode matching `0`; `./run_tests.sh core`: EditMode `217/0`, PlayMode `109/0`; materializer caller/validation-order/service Normalize·clamp/schema-scope scan, generated `InitTestScene` 잔존 확인, `git diff --check`: pass | UI lane: UI-facing port/result/controller/composition을 변경하지 않아 미실행; broad unfiltered `full`: six-fixture repository/service/adapter/mapper/receipt/DirectPlay gate와 core가 2B 범위를 충족하여 미실행 | private persistence mapper에 남은 invalid skip/counter clamp/business `Normalize`와 achievement tolerant projection의 owner/type 혼합은 2C에 남음. materializer는 repository만 호출하며 Phase 2 전체 exit는 2C 전까지 미충족; full/broad regression은 증명하지 않음 | Ready for next package |
| 2026-08-24 22:22 KST | Phase 2 / 2C | `3323ac0f5` + working tree; mapper SHA-256 `63f0a5e07a61671d55cd9fe025f94ac59463e3ce88e5beda28c25b2f614ad276`, performance policy `7bd5777f745145439aac1770e748322194a0ca5b9d85198703546cb76aad3a28`, achievement projection `a2c4e2c657a899011c5dd96d4be3b2b46e3700661a7a71fe4b3dfd9a8d31179d`, mapper fixture `ca7b005c1b4fb7057ec6d3f1ace1304591114ec89aa16730f468c38097411fc9`, architecture fixture `e5081975a7483c34e2dd369bcace29610b92528ce27b8aa4a161402f09d4d5d4`, achievement fixture `a6a233082e08d70d68f8990abe4ed468b5e11b7780a4d6ffec2c347eece090e4` | mapper의 invalid skip/counter clamp/business normalization을 제거하고 validated document를 순서대로 일대일 mapping; performance best/order canonicalization을 strict `CanonicalizeValidated`/`UpsertBest`에 한정하고 public tolerant `Normalize` 제거; achievement recovery는 purpose-named typed read-model builder로 분리. physical DTO/schema 변경 없음 | tests-first `./run_tests.sh full --filter CampaignSlotMapperTests,CampaignSaveArchitectureV2Tests`: EditMode `127/2`로 새 source-owner guard와 order-preservation behavior만 의도대로 실패; 구현 후 `./run_tests.sh full --filter CampaignSlotMapperTests,CampaignSaveArchitectureV2Tests,CampaignStageAchievementIntegrationTests`: EditMode `134/0`, PlayMode matching `0`; widened `./run_tests.sh full --filter CampaignSlotMapperTests,CampaignSaveArchitectureV2Tests,CampaignSaveServiceTests,CampaignSaveSlotStoreAdapterTests,NormalCampaignCompletionReceiptTests,CampaignSlotTransitionCharacterizationTests,CampaignStageAchievementIntegrationTests`: EditMode `245/0`, PlayMode matching `0`; `./run_tests.sh core`: EditMode `217/0`, PlayMode `109/0`; `./run_tests.sh ui`: EditMode `1353/0`; production Normalize/clamp/caller/schema scan and `git diff --check`: pass | broad unfiltered `full`: seven-fixture mapper/repository/service/adapter/receipt/transition/achievement gate, core, UI가 2C 범위를 충족하여 미실행 | Phase 2 exit는 충족. mutable `SaveSlotData`, immutable canonical state factory/consumer adapter, 그리고 achievement tolerant projection 제거 가능성 재검토는 Phase 3에 남음. validator가 허용하는 valid unique performance document의 입력 순서는 mapper가 그대로 보존하며 canonical persisted output은 validated slot canonicalizer가 계속 정렬한다. full/broad regression은 증명하지 않음 | Ready for next package |
| 2026-08-24 22:50 KST | Phase 3 / 3A | `3323ac0f5` + working tree; state/parser/factory SHA-256 `66e259d13a03e035ec72071e7f880261b34a6bc283e7d74e0653baa70d99b9ef`, fixture `f83057ab5aec7cfc7c2b7002a31e1277a3b46a5f46bf69abeb883c88ac3bd5b7`, README pre-update `f32e8a424517b76293b0ca7aca85e62b9aba7fc12230f4701f18759041ec465f`, plan pre-log `603ab1466611bb86371be5df5e88c5d71135ce2d7ee7c25840ef57c5f2972aea` | schema DTO와 runtime consumer를 유지한 채 explicit Empty/Occupied entry, immutable canonical slot/nested state, fail-closed parser와 typed raw diagnostic, new-game factory, strict state-to-document mapper를 추가; receipt 3-state와 exact raw evidence를 보존하고 external mutation을 차단 | tests-first `./run_tests.sh full --filter CampaignSlotStateTests`: 신규 타입 부재 `CS0246`로 XML 전 의도대로 실패; 구현 후 동일 filter EditMode `9/0`, PlayMode matching `0`; `./run_tests.sh full --filter CampaignSlotStateTests,CampaignSlotMapperTests,CampaignSaveArchitectureV2Tests,NormalCampaignCompletionReceiptTests`: EditMode `148/0`, PlayMode matching `0`; `./run_tests.sh core`: EditMode `217/0`, PlayMode `109/0`; receipt/nested round-trip assertion 보강 후 focused EditMode `9/0`, PlayMode matching `0`; constructor/setter/mutation behavior와 source/schema scope scan, `git diff --check`: pass | UI lane: UI-facing port/result/controller/composition과 runtime consumer를 변경하지 않아 미실행; broad unfiltered `full`: focused state/parser + mapper/receipt/architecture compatibility와 core gate가 3A 범위를 충족하여 미실행 | engine/store는 3B까지, query/UI/achievement/gameplay/bootstrap consumer와 compatibility adapter는 3C까지 `SaveSlotData`를 사용한다. achievement tolerant projection 재검토도 3C에 남으며 physical schema/document 변경 없음. full/broad regression은 증명하지 않음 | Ready for next package |
| 2026-08-24 23:29 KST | Phase 3 / 3B | `3323ac0f5` + working tree; engine SHA-256 `4cb4949407bea75a556570a47af4ab2aff4cb453a44bf0103aa9affc36ebd076`, service `3bbfa63d355768bcedd1931e67248a39a2e3f33d40bf5508bab54b983e19b72f`, adapter `1b45996185742e8ae4eb958810f8326ccf38ecf6cbb8eaa8ad8d72328ed379d8`, Transient `071d54a2f4ae8c5ff761e39fc399abcb166bda384c5975cd3e87a371abf64ce6`, engine fixture `5898a1fdedbdb65cd7f63ccf0661fe12a9ab2da362e2928dfd0e12413d031ef2`, README `0afd98fe89e4ab2542fab13e61a2bc49afa429fb4b9f86ecb32c96593cd8de5d`, plan pre-log `b848a2ae6a16e99e4249ca6e6d0af012177819d8e816cae1bb8ccf299e4012e9` | engine result/input, Production transaction, Transient storage를 immutable state로 전환; comic completion을 typed engine command로 통합하고 service의 ignored stage argument를 제거하되 legacy port는 stage validity를 검사; new game/diagnostic selection은 목적별 state factory가 소유. compatibility 변환은 한 adapter source와 Transient facade로 제한하고 physical schema/API general replacement는 추가하지 않음 | tests-first engine+architecture gate는 `CampaignComicCompletionKind` 부재 `CS0246`/`CS0103` 3건으로 XML 전 의도대로 실패; 구현 후 동일 gate EditMode `132/0`, PlayMode matching `0`; widened save cluster의 첫 run은 EditMode `278/2`로 old mapper delegation/source-line assertion만 실패했고 guard 교정 후 해당 fixtures `55/0`; final eight-fixture save cluster EditMode `280/0`, PlayMode matching `0`; `./run_tests.sh core`: EditMode `217/0`, PlayMode `109/0`; compatibility allowlist, engine/service mutable-carrier, schema/PlayerPrefs/0-sentinel source scan과 `git diff --check`: pass | UI lane: UI consumer/port/result/composition을 변경하지 않아 미실행; broad unfiltered `full`: eight-fixture immutable/engine/characterization/service/adapter/mapper/receipt gate와 core가 3B 범위를 충족하여 미실행 | query/UI/achievement/gameplay/bootstrap와 compatibility port surface는 3C까지 `SaveSlotData`를 유지한다. tolerant achievement projection 재검토와 legacy mapper/canonicalizer 제거도 3C residual이며 Phase 3 전체 exit는 아직 미충족; full/broad regression은 증명하지 않음 | Ready for next package |
| 2026-08-25 00:22 KST | Phase 3 / 3C | `3323ac0f5` + working tree; ports SHA-256 `aa213ce43278496bb0d2896193808e0828a61ad4e693dac7f8c83244fb2267e0`, adapter `f8dd794c13c2242e2c835969aa47af84b996f39acf5256e7f5e7f210a49f6817`, Transient `3d0016dbc62631dc8d2e8dccae40d949792d3ae5751cc0626e127805314c826d`, gameplay flow `ba94079ef2ad99e205aada2077e1966f9c5f168222a529460fad9a897a434fb7`, achievement `bb1730b8c3182bafe5e772b1c51ab5553998313537c74734470ee84de1d18ed1`, MainMenu `842f058244fdca4c4e2f030126db61e04f7ee54e8f0d9aa30cecef6cc7a48576`, DirectPlay `12fc323ddb06ab86934f024beb2f03b45fa12f4e6781e6435069fe5422500a54`, player capture `43bf270a7efd061507910e9bb7312eab23bacb7e679647eb946cdea4c520b246`, architecture fixture `1af845f210bfbba8f4722624ad6508bb625baf7f9513dbc62f68af42c535710d` | query/result와 gameplay/achievement/comic/MainMenu/DirectPlay/bootstrap consumer를 immutable entry/state로 전환; achievement tolerant projection 제거; comic ignored stage argument 제거; seed import를 narrow purpose port로 분리. concrete store/test/Phase 4 validation maintenance compatibility만 explicit facade로 유지하고 schema 2 physical surface는 보존 | tests-first consumer architecture gate는 EditMode `106/2`로 immutable port와 runtime mutable-carrier guard만 의도대로 실패; compile migration 중 Windows full build `77` errors까지 fixture/facade boundary를 수렴; focused correction gate EditMode `223/0`, PlayMode matching `0`; final 18-fixture touched cluster EditMode `575/0`, PlayMode matching `0`; `./run_tests.sh core`: EditMode `217/0`, PlayMode `109/0`; `./run_tests.sh ui`: EditMode `1353/0`; runtime mutable-carrier/compatibility/achievement projection/seed/comic source scan과 `git diff --check`: pass | broad unfiltered `full`: 18-fixture save/gameplay/achievement/UI/DirectPlay gate와 core/UI가 3C 범위를 충족하여 미실행 | Phase 3 exit 충족. mutable concrete store facade, test fixture projection, `SaveSlotValidationService`와 MainMenu full maintenance sync는 Phase 4/5 explicit residual이다. Retired stage save retention decision은 Phase 4 entry에서 policy 재확인 필요; full/broad regression은 증명하지 않음 | Ready for next package |
| 2026-08-25 00:41 KST | Phase 4 / retired-save entry cleanup | `3323ac0f5` + working tree; validation service pre-log SHA-256 `81430fa3dffb132aa4b66c5a70b72540eff6c488046f53e8ede7ba1127c09ffc`, validation fixture `be25610847bca12c3cc091bd8e4f9745006f12fb696e841248406fc5a9595955`, pre-release policy pre-log `29f61f6eb6a48b27e1b6ebf67bf6d5482e80e18f864f5f0f4706543b73fa21c0` | no-public-save policy를 재확인하고 `stage-5-1 -> final stage` auto-repair branch와 runtime policy source/meta를 제거; retired cursor를 ordinary sequence-missing/no-sync/no-write로 고정하고 catalog-only legacy content/governance는 보존 | tests-first `SaveSlotValidationAndDirectPlayTests`: EditMode `34/2` intentional red; 구현 후 `34/0`, PlayMode matching `0`; filtered `SaveSlotValidationAndDirectPlayTests,CampaignStageFlowTests`: EditMode `118/0`, PlayMode matching `0`; `./run_tests.sh core`: EditMode `217/0`, PlayMode `109/0`; `./run_tests.sh ui`: EditMode `1353/0`; final runtime-policy/source and diff audit: pass. 첫 구현 후 ignored generated `.csproj`에 삭제 source entry가 남아 Windows build `CS2001`로 중단되었고 tracked content 변경 없이 generated entry를 refresh한 뒤 통과 | broad unfiltered `full`: scoped retired-save fixture + gameplay sequence fixture, core, UI가 package 범위를 충족하여 미실행; Player/build manual smoke: physical schema/content/scene/prefab을 변경하지 않아 미실행 | 4A read-only evaluator/action policy, `UnsupportedVersion` slot leakage, level-group narrow sync, MainMenu full replacement와 compatibility facade는 후속 package에 남음. 내부 QA save retention/migration은 만들지 않았고 full/broad regression은 증명하지 않음 | Ready for next package |
| 2026-08-25 01:05 KST | Phase 4 / 4A | `3323ac0f5` + working tree; launch evaluator SHA-256 `00697e538e16d606e0bf3a56808e13f202fcc76f7e2f9cd0fb6eb080962a914b`, validation facade `9490b0641213ca35461537346f56ed6aa602e7e360a00b255474b219ccfa2a22`, validation fixture `a6d05f83192c8159682fbf43f3b2dc9fef7bb7f49026819d09200ebf8ed717f2`, UI wiring fixture `95920936f7d7fddf279f6b670a5ae456a43b35200af47fb1909197a83372de37` | immutable entry/state와 sequence/catalog만 읽는 launch evaluator와 별도 action policy를 추가하고 legacy validation result를 조합 facade로 전환; mismatched group은 evaluation에서 mutation 없이 sync-required로 분류하고 facade clone에서만 기존 UI semantics를 유지; null/malformed raw evidence를 fallback slot으로 바꾸지 않으며 per-slot `UnsupportedVersion`과 UI mapping 제거 | tests-first focused run은 신규 action-policy type 부재 `CS0246`로 XML 전 의도대로 실패; 구현 후 `SaveSlotValidationAndDirectPlayTests` EditMode `39/0`, PlayMode matching `0`; final null-evidence review도 새 assertion이 EditMode `40/1`로 의도대로 실패한 뒤 fallback 제거 후 `40/0`, PlayMode matching `0`; seven-fixture save/stage/UI cluster 첫 run EditMode `417/1`은 validator의 제거된 resolver field를 보던 reflection assertion만 실패했고 새 evaluator owner로 교정 후 `417/0`, PlayMode matching `0`; final `./run_tests.sh core`: EditMode `217/0`, PlayMode `109/0`; final `./run_tests.sh ui`: EditMode `1351/0`; current docs 갱신 후 architecture+validation fixture EditMode `146/0`, PlayMode matching `0`; evaluator write-dependency, slot unsupported-version, action-policy delegation, generated scene, diff source audit: pass | broad unfiltered `full`: seven-fixture touched cluster와 core/UI가 4A 범위를 충족하여 미실행; Player/build/manual smoke: physical schema/content/scene/prefab을 변경하지 않아 미실행 | 4B narrow level-group preparation/sync port, 4C MainMenu result migration/full replacement 제거, Phase 5 compatibility facade/test shim 제거가 남음. Profile-level blocked/unsupported presentation은 유지했고 full/broad regression은 증명하지 않음 | Ready for next package |
| 2026-08-25 01:45 KST | Phase 4 / 4B | `3323ac0f5` + working tree; ports SHA-256 `3b8adea13de1f51f497c52eb12e2ba1b36ecbc4bca3d060763767e4cd95f5802`, service `deb063f81535e9996e829a58443396e3f22506e3638279835783c4bd0e2bf7ca`, adapter `ecbec29e851e2e4efb4c1796d199e81c467f0dd9cefdea9ef5ac6787f8cd2d86`, Transient `b3dc7b929888ba10405126b7231e71f6e5d617ad6c55be5f6229dff66c876fab`, MainMenu `e78416d24c8da18ee75c3b1d255b8ba0afe6c74f2416bd77d58c85dfb0d5dd1a`, service fixture `360390bfc65b499cfda7999a1a37241531500deef2ff674b3e6fac5a6a84835f`, handoff fixture `861ea9ab56a1baea8790862a79f184835f80d32731e19ec75147e4ce4891b6d1`, plan pre-log `ffc2da485978daea2e1684356e8fb69a738913bfb3ec11519aaa1339eb48f3d6` | MainMenu의 generic maintenance port를 expected slot/stage/persisted-group identity를 가진 Continue preparation port로 교체; Production/Transient가 공통 pure policy로 mutation 경계에서 precondition을 다시 확인하고 필요한 경우 group 한 필드만 commit. already-current는 no-write success, missing/stale/completed는 no-write failure이며 returned committed identity 확인과 exact handoff-token cleanup으로 stale route/newer reservation 손상을 차단. DirectPlay/seed narrow port는 3C 상태를 유지하고 complete replacement는 concrete/internal fixture residual로만 제한 | tests-first `CampaignSaveServiceTests`는 신규 command/port/result 부재로 Windows build compile error `7`건에서 의도대로 중단; 구현 후 EditMode `35/0`, PlayMode matching `0`; `PendingLaunchSlotProviderTests` EditMode `29/0`, PlayMode matching `0`; final seven-fixture filtered `full` EditMode `286/0`, PlayMode matching `0`; `./run_tests.sh core`: EditMode `217/0`, PlayMode `109/0`; `./run_tests.sh ui`: EditMode `1352/0`; old maintenance/validation-sync, runtime full-replacement consumer, schema/PlayerPrefs/chance, generated-scene와 diff source audit: pass | broad unfiltered `full`: seven-fixture service/adapter/handoff/architecture/validation/local-state/PlayerPrefs gate와 core/UI가 package 범위를 충족하여 미실행; manual Player/build smoke: physical schema/content/scene/prefab을 변경하지 않아 미실행 | 4C legacy `SaveSlotValidationResult`/view-model migration과 Phase 5 concrete complete-replacement/test-fixture compatibility 제거가 남음. cross-process writer coordination은 baseline policy 범위 밖이며 full/broad regression은 증명하지 않음 | Ready for next package |
| 2026-08-25 02:16 KST | Phase 4 / 4C | `3323ac0f5` + working tree; MainMenu controller SHA-256 `3c0bfd0c28594e8bf37e185d2382a7551124e675af9a3b5693c0677a8aaad93f`, mapper `87c707cae1821913232ba6bf9434f6f7ff78eef16cb176799752c3bf44aa90d0`, installer `8d97c43d41eb6a280f51a24f10a747633d45969a495ce0018b0cf9014be7ef07`, evaluator `00697e538e16d606e0bf3a56808e13f202fcc76f7e2f9cd0fb6eb080962a914b`, architecture fixture `01adbd1f2db0a75ef1d40f890a67ff905d1679a8269cb7f55791e8c3a0127cae`, validation fixture `abcc5e4448a223eae11f0f31e4d72ed8c200ae430e75603da98b2973f04f646b`, MainMenu fixture `2d39420bb615fb75644aa9bba2a3d313851c0b3778fa259c8a98950cd3fd5073` | MainMenu controller/mapper/composition을 immutable entry + launch evaluation + action policy input으로 전환하고 entry/evaluation identity와 policy 일치를 검증; profile blocked report와 per-slot launch-failure presentation을 분리한 채 completed stale-group, confirmation, exact handoff ownership을 보존. combined validation service/result/status와 corrected mutable clone source/meta 제거 | tests-first architecture+MainMenu fixture는 mapper legacy signature 때문에 Windows build `CS1503` 2건으로 의도대로 red; 구현 중 삭제 source의 ignored generated `.csproj` entry가 `CS2001` 1건을 냈고 generated include만 refresh; 첫 focused gate EditMode `113/0`, PlayMode matching `0`; final nine-fixture touched cluster EditMode `403/0`, PlayMode matching `0`; `./run_tests.sh core`: EditMode `217/0`, PlayMode `109/0`; `./run_tests.sh ui`: Windows build + EditMode `1352/0`; generated scene cleanup과 `git diff --check`: pass | broad unfiltered `full`: nine-fixture launch/UI/composition/architecture cluster와 core/UI가 4C 및 Phase 4 exit 범위를 충족하여 미실행; manual Player/build smoke: schema/content/scene/prefab 변경이 없어 미실행 | Phase 4 exit 충족. Phase 5에는 concrete/internal complete-replacement, mutable compatibility adapter/facade, test fixture projection inventory와 제거가 남음. profile blocked/per-slot failure UX는 current contract로 확정했고 full/broad regression은 증명하지 않음 | Ready for next package |
| 2026-08-25 03:18 KST | Phase 5 / 5A | `3323ac0f5` + working tree; ports SHA-256 `3b8adea13de1f51f497c52eb12e2ba1b36ecbc4bca3d060763767e4cd95f5802`, adapter `f167426232bf33a7e875225042614d9f71abaa256ced77ab0e3ee362d7c9659a`, Transient `35c2080ce6b643278b6c369e450e924a7e1530e120bca31c9316bc04c1301d2e`, raw mapper `af351aa633a37e427b80601062b5d17ba47fc01209cfa6ebcb55f4edf29ecea7`, architecture fixture `1cced766ac311cbf4811929229ff1512864e1b02b8d8666546832aec630acdaf` | concrete Production/Transient query·mutation surface를 immutable entry/state와 purpose-specific command로 제한하고 general `ReplaceValidatedSlot`, mutable compatibility adapter/projection/test fixture, old mapper facade, canonicalizer/domain validator를 제거. `SaveSlotData`와 exact clone은 raw DTO/diagnostic boundary에만 남기고 fixture/editor preview를 typed command 또는 repository-boundary setup으로 이동 | tests-first `./run_tests.sh full --filter CampaignSaveArchitectureV2Tests`: EditMode `107/1`에서 mutable adapter-result guard만 의도대로 red; 구현 후 동일 fixture EditMode `107/0`, PlayMode matching `0`; final 15-fixture filtered `full` EditMode `465/0`, PlayMode matching `0`; `./run_tests.sh core`: EditMode `217/0`, PlayMode `109/0`; `./run_tests.sh ui`: EditMode `1352/0`; old-symbol/runtime-carrier/generated-scene source scan과 `git diff --check`: pass | broad unfiltered `full`: 15-fixture transition/parser/mapper/service/adapter/architecture/DirectPlay/achievement/production-entry cluster와 core/UI가 5A 범위를 충족하여 미실행; manual Player/build smoke: physical schema/content/scene/prefab/ScriptableObject를 변경하지 않아 미실행 | 5B reflection/behavior/source architecture guard와 5C current documentation/evidence closeout이 남음. mutable raw DTO는 `SaveSlotModels.cs`와 explicit raw mapper boundary에만 유지하며 full/broad regression은 증명하지 않음 | Ready for next package |
| 2026-08-25 03:34 KST | Phase 5 / 5B | `3323ac0f5` + working tree; architecture fixture SHA-256 `a00b7ddbbccfc3280b8feb0022ae9f138b5b9cc3e7898131ec16824006516bc4`, plan pre-log `58acc5e99a9db824215acf77cb291e7f91019927d53f3d07feeadaa1ab3a5dc8` | Phase 5 checklist를 일대일 executable guard로 고정: canonical state/nested state의 public constructor·setter 부재와 parser/factory/engine construction owner, Production/Transient common engine wiring, service/Transient/campaign UI/DirectPlay direct gameplay-field assignment 부재, strict mapper의 complete write surface와 no skip/clamp/Normalize, raw fragment/full replacement runtime 소비자 부재, campaign PlayerPrefs composition 부재, save policy/command/document/parser의 chance `1..3` 계약. source guard는 campaign/save 경계 파일로 제한해 presentation chance `0`을 제외 | 신규 guard를 먼저 추가한 `./run_tests.sh full --filter CampaignSaveArchitectureV2Tests`: 기존 runtime이 이미 계약을 충족해 첫 실행부터 EditMode `114/0`, PlayMode matching `0`; final 14-fixture filtered `full` EditMode `443/0`, PlayMode matching `0`; `./run_tests.sh core`: EditMode `217/0`, PlayMode `109/0`; `./run_tests.sh ui`: EditMode `1352/0`; constructor-owner/engine-wiring/raw-boundary/saved-zero/generated-scene source scan과 `git diff --check`: pass | broad unfiltered `full`: 14-fixture transition/parser/mapper/service/adapter/architecture/DirectPlay/PlayerPrefs/achievement/production-entry cluster와 core/UI가 5B 범위를 충족하여 미실행; manual Player/build smoke: test-only guard와 plan ledger 변경이며 runtime/schema/content/scene/prefab/ScriptableObject를 변경하지 않아 미실행 | 5C current README/pre-release/testing documentation 및 same-revision evidence closeout과 Goal 전체 requirement-by-requirement final audit가 남음. source guard는 의도적으로 presentation/general UI numeric state를 검사하지 않으며 full/broad regression은 증명하지 않음 | Ready for next package |
| 2026-08-25 03:52 KST | Phase 5 / 5C | `3323ac0f5` + working tree; README SHA-256 `cf5fd407cd233b1ad79a2b535df391eb8beda569a9db0b6b11057f39f4cdbbc6`, pre-release policy `386bb21d732fd720b401e7166736e7dc2bb563231cdd27a2c701742c7522119e`, sequence authority `73dce6a6134ea93727a159062d2268629a2746b3dfa4af5b3f670a917eec8f09`, testing guide `120468e3f39cb708d855f95c4948fd1f17569b62b3e139620cbf67d772bb326f`, architecture fixture `0ad5be96d894e984d5ab5a82dbba0808c623a2ad2e92775b554b04384e2d8247` | current architecture/policy/testing truth를 immutable `CampaignSlotState`, common `CampaignSlotTransitionEngine`, strict `CampaignSlotStateDocumentMapper`, separated launch evaluator/action policy, explicit raw DTO boundary와 일치시키고 removed mapper/canonicalizer/achievement projection/full replacement wording을 제거. sequence authority flow를 current MainMenu evaluation/preparation path로 갱신하고 remediation/Goal prompt를 historical execution provenance로 표기; README/pre-release doc guard를 강화 | `./run_tests.sh full --filter CampaignSaveArchitectureV2Tests`: EditMode `114/0`, PlayMode matching `0`; 15-fixture transition/parser/mapper/service/adapter/recovery/DirectPlay/achievement/production-entry filtered `full`: EditMode `472/0`, PlayMode matching `0`; `./run_tests.sh core`: EditMode `217/0`, PlayMode `109/0`; `./run_tests.sh ui`: Windows UI build + EditMode `1352/0`; old runtime shim/raw-carrier consumer/saved-zero/generated-scene source audit와 `git diff --check`: pass | broad unfiltered `full`: targeted 15-fixture cluster와 core/UI가 Phase 5C risk를 충족해 미실행; manual Player/build smoke: runtime/schema/content/scene/prefab/ScriptableObject를 변경하지 않은 docs/guard closeout이라 미실행 | Phase 5 exit 충족. Whole-Goal requirement-by-requirement audit만 다음 closeout action으로 남으며, broad/full regression recovery와 manual Player behavior는 증명하지 않음 | Ready for next package |

### 13.1 Post-closeout corrective audit — 2026-08-25 KST

독립 재감사에서 receipt absence payload의 허용 범위, performance policy owner 문서, 과도기 테스트
가이드 wording, 상세 filtered artifact 보존을 다시 열었다. correction은 schema 2나 receipt presence
3-state를 바꾸지 않고 다음 계약을 고정했다.

- `HasNormalCampaignCompletionReceipt == false`에는 null 또는 모든 receipt field가 기본값인
  JsonUtility residue만 허용한다. 값이 채워진 payload는 `InvalidDocument`로 fail-closed하며
  normalization이나 unrelated write로 삭제하지 않는다.
- performance uniqueness/deterministic order는 immutable `CampaignSlotState` construction이,
  best-value update는 `CampaignSlotTransitionEngine.UpsertPerformance`가 소유한다. Production에서
  사용되지 않던 mutable `NormalStagePerformanceRecordPolicy`는 제거했다.
- Gameplay recording test store도 death/clear 결과 계산에 common transition engine을 사용한다.
- 테스트 가이드의 2026-08-24 complete-replacement/canonicalization 설명은 당시 과도기 역사 기록으로
  표시하며 current Phase 5 composition과 구분한다.

tests-first focused gate는 `CampaignSaveArchitectureV2Tests,CampaignSlotStateTests` EditMode `126/4`로
예상한 policy/receipt 계약 4건만 red였다. 구현 후 architecture EditMode `117/0`, 명시적 15-fixture
touched cluster EditMode `564/0`과 matching PlayMode `0`, core EditMode `217/0`, core PlayMode
`109 total / 105 passed / 4 skipped / 0 failed`, UI Windows build + EditMode `1352/0`이 같은 working
tree에서 통과했다. 상세 command/fixture/XML/log evidence는
`/mnt/d/J2M/evidence/20260825-055226-campaign-save-corrective-closeout/`에 보존한다. broad unfiltered
`full`과 manual Player/build smoke는 실행하지 않았으므로 project-wide 또는 broad recovery를
claim하지 않는다.

### 13.2 Exact serializer residue correction — 2026-08-25 KST

13.1의 “모든 field가 기본값인 residue” 표현을 Unity의 실제 직렬화 경계에 맞게 더 좁혔다. tests-first
focused gate의 EditMode `140/7` 중 6건은 기존 빈 문자열 허용 경계를, 1건은 repository의 실제
Save→JSON→Load에서 `PresentWithoutPayload`가 `InvalidDocument`로 바뀌는 문제를 드러냈다.

`JsonUtility` characterization으로 확인한 post-deserialization 자동 형태는 정확히 두 가지다.

- 빈 nested receipt object `{}`를 읽으면 두 receipt 문자열이 모두 null이다.
- null nested receipt를 writer로 저장한 뒤 다시 읽으면 두 receipt 문자열이 모두 `string.Empty`다.
- 두 경우 모두 `Version == 0`, `ClearSource == 0`이어야 serializer residue다.
- 직접 구성된 in-memory mixed null/empty, whitespace, 실제 문자열 값, non-zero version/source는
  residue가 아니며 absence flag와 함께 들어오면 normalization 전에 fail-closed한다.

원시 JSON의 개별 문자열 `null`은 `JsonUtility`가 빈 문자열로 정규화하므로 그 출처는 DTO 경계에서
보존되지 않는다. 따라서 이 계약은 raw-token provenance가 아니라 post-`JsonUtility` object shape를
검증한다. mixed null/empty 거부는 직접 in-memory document와 direct `Save` 입력에서 검증한다.

materializer는 absence와 exact residue를 canonical null로 만들며, state parser는 presence true와 exact
residue를 `PresentWithoutPayload`로 복원한다. repository physical round-trip test는 absent,
present-without-payload, present-with-payload 세 상태를 모두 고정한다. 최종 focused EditMode `140/0`,
architecture `123/0`, 15-fixture touched cluster `577/0`, core EditMode `217/0`, core PlayMode
`109 total / 105 passed / 4 skipped / 0 failed`, UI Windows build + EditMode `1352/0`을 같은 working
tree에서 검증했다. 증거는
`/mnt/d/J2M/evidence/20260825-065534-campaign-save-exact-receipt-residue/`에 보존한다. broad unfiltered
`full`과 manual Player/build smoke는 실행하지 않았으므로 project-wide 또는 broad recovery를 claim하지
않는다.

### 13.3 Raw receipt mapper boundary closeout — 2026-08-25 KST

추가 재감사에서 public `CampaignSlotRawDataMapper`의 outbound receipt projection이 두 문자열에
`?? string.Empty`를 적용해 직접 구성한 `(null, string.Empty)`와 `(string.Empty, null)` raw pair를
`(string.Empty, string.Empty)`로 바꾼 뒤 검증을 통과시키는 경계를 확인했다. Production canonical write는
이 public raw mapper를 사용하지 않지만, 문서화된 fail-closed raw conversion 계약과 달랐으므로 boundary
contract를 닫았다.

tests-first focused gate는 EditMode `179/4`로 예상한 네 건만 red였다. 하나는 `(null, null)` exact pair의
outbound 보존, 둘은 양방향 mixed pair 거부, 하나는 outbound coalesce 금지 source guard였다. 수정은
`CampaignProfileDocumentMapper.ToReceiptDocument`의 두 문자열을 원값 그대로 projection하도록 제한했다.
Inbound document-to-runtime 정규화, schema 2, receipt 3-state, Production canonical write path는 변경하지 않았다.

보강된 matrix는 다음을 독립적으로 고정한다.

- raw mapper의 `(null, null)` / `(string.Empty, string.Empty)` exact pair 허용과 양방향 mixed pair 거부
- structurally valid payload를 사용한 false-presence fail-closed
- exact string pair와 non-zero version/source residue의 독립 거부 및 diagnostic raw evidence 보존
- physical present-with-payload의 version/stage/run/source field 보존
- exact clone owner와 non-mutating validating raw conversion owner의 분리
- post-`JsonUtility` object-shape 계약과 raw JSON token provenance 비보존

최종 검증은 focused EditMode `179/0`, architecture EditMode `127/0`, 명시적 15-fixture touched cluster
EditMode `590/0`과 각 matching PlayMode `0`, core EditMode `217/0`, core PlayMode
`109 total / 105 passed / 4 skipped / 0 failed`, UI Windows build + EditMode `1352/0`이다. tests-first
red와 최종 XML/log/metrics/source hash evidence는
`/mnt/d/J2M/evidence/20260825-075401-campaign-save-receipt-raw-boundary-closeout/`에 보존한다. broad
unfiltered `full`과 manual Player/build smoke는 실행하지 않았으므로 project-wide 또는 broad recovery를
claim하지 않는다.

### 13.4 Final structural audit closeout — 2026-08-25 KST

최종 구조 재감사는 receipt field fix 자체가 아니라 public raw collection aggregate와 documentation
parity를 다시 열었다. `FromDocuments`와 그 alias인 `ToRawSlots`는 각 document를 개별 검증했지만 동일한
`SlotNumber`를 조용히 last-write-wins로 덮어써 먼저 들어온 receipt 3-state evidence를 유실할 수 있었다.
Production runtime caller는 없지만 non-mutating validating raw conversion boundary의 fail-closed 계약과
불일치하므로 public signature를 유지한 채 중복 slot number를 `ArgumentException`으로 거부한다.

tests-first raw-mapper+architecture gate는 EditMode `160/3`으로 예상한 세 건만 red였다. 두 건은
`FromDocuments`/`ToRawSlots` duplicate-slot 경로였고, 한 건은 의도적으로 bilingual인 testing guide의
English Original 구역에 13.3 대응 항목이 없다는 documentation guard였다. 기존 receipt 4-pair matrix는
`ToEntry`와 `ToEntries`도 직접 호출해 모든 full-slot outbound wrapper가 같은 validator 경계를 타도록
고정했다. 한국어와 English Original에는 13.3 closeout과 이번 follow-up을 함께 기록했다.

최종 검증은 focused EditMode `181/0`, architecture EditMode `127/0`, 명시적 15-fixture touched cluster
EditMode `592/0`과 각 matching PlayMode `0`, core EditMode `217/0`, core PlayMode
`109 total / 105 passed / 4 skipped / 0 failed`, UI Windows build + EditMode `1352/0`이다. tests-first
red와 최종 XML/log/metrics/source hash evidence는
`/mnt/d/J2M/evidence/20260825-084058-campaign-save-final-structure-closeout/`에 보존한다. broad
unfiltered `full`과 manual Player/build smoke는 실행하지 않았으므로 project-wide 또는 broad recovery를
claim하지 않는다.

### 13.5 Independent audit corrective closeout — 2026-08-25 KST

후속 독립 감사에서 `DemoStageControlTests`의 diagnostic stage-selection assertion 하나가 기존 runtime과
문서 계약에 반대로 이식된 사실을 확인했다. Runtime은 이전부터 선택 stage의 snapshot record가 없으면
`HasAttempted = false`, `HasCleared = false`, `ClearCount = 0`인 record를 만들었지만, 변경된 테스트는 전체
record collection이 비어 있다고 기대해 단독 재실행에서도 `1/1`로 반복 실패했다.

교정은 production runtime을 변경하지 않고 purpose-named diagnostic port로 이전 stage record를 준비한 뒤
선택 stage의 미시도/미완료 record 생성과 이전 record의 field 보존을 함께 검증하도록 제한했다. 같은
fixture의 launch-context 테스트 이름도 실제 ownership 계약에 맞게 Demo bridge가 context를 직접 등록하지
않고 router에 위임한다는 의미로 정정했다. Planner/engine 책임 분리, profile-level fail-closed diagnostic,
raw DTO/test boundary는 재감사 결과 current StrongContract와 일치하므로 변경하지 않았다. Stage-clear save
failure terminal recovery와 profile/LocalState cleanup failure는 각각 별도 CurrentPolicy 후속이며 이 corrective
slice에 섞지 않았다.

최종 current-state 검증은 교정된 단일 test EditMode `1/0`, `DemoStageControlTests` EditMode `18/0`,
`CampaignSaveArchitectureV2Tests` EditMode `127/0`과 각 matching PlayMode `0`, core EditMode `217/0`,
core PlayMode `109 total / 105 passed / 4 skipped / 0 failed`, UI Windows build + EditMode `1352/0`이다.
XML/log/source hash와 working-tree 상태는
`/mnt/d/J2M/evidence/20260825-110252-campaign-save-independent-audit-corrective-closeout/`에 별도 보존한다.
기존 evidence bundle은 수정하거나 재패키징하지 않았다. Broad unfiltered `full`, ActualScene Full-category
PlayMode, manual Player/build smoke는 실행하지 않았으므로 그 범위의 회귀 해소를 claim하지 않는다.

### Historical post-package verification notes

2A current README와 실행 기록 갱신 뒤 같은 working tree에서
`./run_tests.sh full --filter CampaignSaveArchitectureV2Tests`를 다시 실행해 EditMode `98/0`,
PlayMode matching `0`을 확인했다.

2B current README/pre-release policy와 실행 기록 갱신 뒤 같은 working tree에서
`./run_tests.sh full --filter CampaignSaveArchitectureV2Tests,CampaignSaveServiceTests`를 다시
실행해 EditMode `131/0`, PlayMode matching `0`을 확인했다.

2C current README/pre-release policy와 실행 기록 갱신 뒤 같은 working tree에서
`./run_tests.sh full --filter CampaignSaveArchitectureV2Tests,CampaignSlotMapperTests,CampaignStageAchievementIntegrationTests`를
다시 실행해 EditMode `134/0`, PlayMode matching `0`을 확인했다.

3A current README와 실행 기록 갱신 뒤 같은 working tree에서
`./run_tests.sh full --filter CampaignSlotStateTests,CampaignSaveArchitectureV2Tests`를 다시 실행해
EditMode `110/0`, PlayMode matching `0`을 확인했다.

3B current README와 실행 기록 갱신 뒤 같은 working tree에서
`./run_tests.sh full --filter CampaignSlotStateTests,CampaignSaveArchitectureV2Tests`를 다시 실행해
EditMode `113/0`, PlayMode matching `0`을 확인했다.

3C current README/pre-release policy와 실행 기록 갱신 뒤 같은 working tree에서
`./run_tests.sh full --filter CampaignSaveArchitectureV2Tests,CampaignSlotMapperTests`를 다시 실행해
EditMode `132/0`, PlayMode matching `0`을 확인했다.

Phase 4 retired-save current policy/docs와 실행 기록 갱신 뒤 같은 working tree에서
`./run_tests.sh full --filter CampaignSaveArchitectureV2Tests,SaveSlotValidationAndDirectPlayTests`를
실행해 EditMode `140/0`, PlayMode matching `0`을 확인했다. 마지막 historical/current wording
reconciliation 뒤 `CampaignSaveArchitectureV2Tests`만 다시 실행해 EditMode `106/0`, PlayMode
matching `0`을 확인했다.

Phase 4A current README와 실행 기록 갱신 뒤 같은 working tree에서
`./run_tests.sh full --filter CampaignSaveArchitectureV2Tests,SaveSlotValidationAndDirectPlayTests`를
다시 실행해 EditMode `146/0`, PlayMode matching `0`을 확인했다.

Phase 4B current architecture/testing docs와 실행 기록 갱신 뒤 같은 working tree에서
`./run_tests.sh full --filter CampaignSaveArchitectureV2Tests,SaveSlotValidationAndDirectPlayTests`를
다시 실행해 EditMode `146/0`, PlayMode matching `0`을 확인했다.

Phase 4C current architecture/testing/UI docs와 실행 기록 갱신 뒤 같은 working tree에서
`./run_tests.sh full --filter CampaignSaveArchitectureV2Tests,SaveSlotValidationAndDirectPlayTests`를
다시 실행해 EditMode `147/0`, PlayMode matching `0`을 확인했다. Phase 4 exit source audit도 combined
validation facade/retired policy absence, evaluator repository independence, MainMenu full-replacement absence,
separated result presence, generated-scene residue 없음과 `git diff --check`를 통과했다.

Decision은 `Ready for next package`, `Hold`, `Rollback` 중 하나를 사용한다. `Hold`이면 다음 Goal
turn은 새 구현이 아니라 실패 분석과 증거 보강만 수행한다.
