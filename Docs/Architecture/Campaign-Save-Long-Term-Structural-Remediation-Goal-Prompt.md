# Campaign Save 장기 구조 개선 Goal Prompt

> Historical completed-Goal prompt. Phase 0-5 실행 provenance를 보존하기 위한 문서이며,
> current runtime truth나 새 작업의 active entrypoint가 아니다. Current contract는
> [Architecture README](./README.md)와
> [Pre-Release Save Baseline Policy](./Pre-Release-Save-Baseline-Policy.md)를 따른다.

이 문서는 [Campaign Save 장기 구조 개선 계획](./Campaign-Save-Long-Term-Structural-Remediation.md)을
실제 장기 Goal로 시작할 때 사용한 실행 프롬프트를 그대로 보존한다. 아래 본문은 historical
execution input이며 새 Goal을 자동으로 시작하지 않는다.

## Goal 시작 프롬프트

Campaign Save 장기 구조 개선 Goal을 시작하고, 아래 objective가 실제로 충족될 때까지 Phase를
순서대로 수행하라.

### Objective

`profile.json` schema 2의 현재 외부 동작을 유지하면서 campaign slot의 authoritative transition
owner를 하나로 만들고, raw/empty/corrupt/canonical state를 타입과 경계로 분리한다. Production,
Campaign DirectPlay, Transient test/diagnostic composition이 같은 domain transition 규칙을 사용하게
하고, validation·launch eligibility·maintenance·persistence 책임을 분리하여 이후 국소 수정이 다른
경로의 저장 계약을 다시 깨뜨리지 않게 한다.

단순히 일부 테스트를 통과하거나 계획 문서의 한 Phase를 끝내는 것이 Goal 완료가 아니다. 아래
전체 완료 조건이 충족되어야 Goal을 완료할 수 있다.

### 먼저 읽을 문서

작업 시작 전 다음 문서를 완전히 읽고 current truth와 long-term plan을 구분하라.

1. `Docs/Architecture/README.md`
2. `Docs/Architecture/Pre-Release-Save-Baseline-Policy.md`
3. `Docs/Architecture/Campaign-Save-Long-Term-Structural-Remediation.md`
4. `Docs/Testing/Gameplay-Test-Automation-Guide.md`
5. `AI_GIT_COMMIT_RULES.md`

`Campaign-Save-Long-Term-Structural-Remediation.md`는 실행 계획이며 아직 구현된 runtime truth가 아니다.
현재 동작 판단은 Architecture README와 Pre-Release policy를 우선한다.

### 작업 시작 규칙

매 Goal turn 또는 자동 continuation 시작 시 다음을 수행하라.

1. `git status --short --branch`와 `git diff --stat`으로 현재 working tree를 확인한다.
2. 현재 Phase/work package와 겹치는 기존 diff를 읽고 사용자 변경을 보존한다.
3. 장기 계획 문서의 실행 기록에서 마지막 `Decision`과 남은 위험을 확인한다.
4. 이전 package의 same-revision exit evidence가 없으면 다음 package를 시작하지 않는다.
5. 한 change set에는 한 Phase의 한 work package만 포함한다.
6. 구현 중 발견한 계약 차이는 추측으로 통일하지 말고 characterization 또는 정책 근거로 판단한다.
7. commit/push는 별도 요청이 있을 때만 수행한다.

### 절대 보존할 현재 계약

- Production campaign progression 저장 진실은 `Saves/profile.json` 하나다.
- root document는 `CampaignProfileDocument.SchemaVersion = 2`를 유지한다.
- active local pointer는 `Saves/local-launch-state.json` 계약을 유지한다.
- campaign progression과 active slot에 PlayerPrefs fallback/import/delete를 다시 만들지 않는다.
- audio/display/input/locale PlayerPrefs는 campaign 작업 범위 밖이며 삭제하지 않는다.
- Production과 Campaign DirectPlay는 JSON-backed composition을 사용한다.
- Transient store는 Production DirectPlay 저장소가 아니라 test/short-lived diagnostic composition이다.
- persisted/runtime `RemainingChances`는 항상 `1..3`이다. save state에서 `0` sentinel을 만들지 않는다.
- 마지막 목숨 소진은 중간 `0` 저장 없이 group 첫 stage와 chance `3`을 한 transaction으로 commit한다.
- terminal presentation이 일시적으로 chance display `0`을 사용하는 것은 save sentinel과 구분한다.
- malformed persisted slot은 normalization 전에 fail-closed한다. invalid 값을 clamp/drop하여 저장하지 않는다.
- blocked profile의 일반 query/mutation은 repository write 없이 실패한다.
- processed-ID field는 active idempotency 의미가 없어도 schema 2 round-trip에서 보존한다.
- current `SaveSlotData.Clone()`과 nested clone은 migration 완료 전까지 raw shape를 보존하는 exact copy다.
- public 배포된 이전 save schema는 없다. public legacy migration이나 sentinel compatibility를 새로 만들지 않는다.
- physical schema 변경이 필요해지면 현재 Goal 범위를 멈추고 별도 schema/release 결정을 요청한다.

### 목표 구조

최종 구조는 다음 책임 분리를 만족해야 한다.

```text
CampaignProfileDocument / CampaignSlotDocument (untrusted DTO)
    -> profile validator + slot presence resolution
       -> absent: CampaignSlotEntry.Empty
       -> invalid: CampaignSlotDiagnostic / profile load failure
       -> valid: immutable CampaignSlotState
          -> CampaignProgressionTransitionPlanner
          -> CampaignSlotTransitionEngine
          -> new immutable CampaignSlotState
             -> strict document mapper -> Production JSON repository
             -> same state/engine -> Transient container

CampaignSlotState + sequence/catalog
    -> read-only launch eligibility evaluator
    -> action policy
    -> 필요한 경우 expected identity를 가진 narrow maintenance/launch-preparation command
```

Planner는 route를 계획하고, transition engine은 slot 전체 전이를 적용하며, repository는 결과를
저장한다. 세 책임을 합치지 않는다.

### Phase 진행 순서

Phase는 `0 -> 1 -> 2 -> 3 -> 4 -> 5` 순서로만 진행한다. 각 Phase의 상세 entry/exit/rollback 기준은
장기 개선 계획 문서를 따른다.

#### Phase 0 — Characterization과 mutation inventory

먼저 실행할 Phase다. production behavior를 바꾸지 않는다.

수행할 일:

- death `3 -> 2`, `2 -> 1`, last chance `1 -> group first stage + 3`을 고정한다.
- clear same-group, group-boundary, final-stage Production/Transient parity를 추가한다.
- receipt의 absent, present-null, present-with-payload 세 상태를 고정한다.
- performance first/better/worse/equal과 processed-ID 보존을 고정한다.
- invalid/stale request의 failure kind와 no-write를 고정한다.
- deterministic timestamp 비교를 위해 필요하면 Transient internal clock seam만 추가한다.
- current authoritative assignments를 transition/factory/maintenance/mapping으로 분류한다.

Phase 0에서 engine, immutable state, mapper visibility, schema, retired compatibility를 변경하지 않는다.

Phase 0 exit evidence가 green일 때만 Phase 1로 간다.

#### Phase 1 — 단일 pure transition engine

`CampaignSlotTransitionEngine`을 추가하고 death/stage-clear의 plan validation, stale precondition,
authoritative field update를 한 곳으로 이동한다.

- engine은 repository, Unity API, system clock, composition type을 참조하지 않는다.
- engine은 external state를 직접 변경하지 않고 새 state/result를 반환한다.
- engine failure는 typed failure이며 Production/Transient가 기존 surface로 변환한다.
- Production은 repository transaction 내부에서 engine을 호출한다.
- Transient는 같은 lock 안에서 load/engine/replace를 수행한다.
- receipt, performance, processed IDs, snapshot 등 변경 대상이 아닌 필드는 보존한다.
- comic, diagnostic, immutable state, MainMenu maintenance는 이 Phase에 섞지 않는다.

Phase 1 exit 시 Production/Transient death-clear business branch와 field assignment가 engine 밖에 남아
있지 않아야 한다.

#### Phase 2 — Mapper와 normalization owner 분리

strict persistence, JsonUtility materialization, runtime canonicalization, tolerant projection을 분리한다.

- public persistence mapper는 complete profile/slot path만 남긴다.
- fragment mapper는 internal/private strict helper로 축소한다.
- document validation은 mutation 없이 fail-closed한다.
- JsonUtility incidental null materialization은 validation 후 한 owner에서 한 번만 수행한다.
- strict mapper에서 invalid skip, counter clamp, performance `Normalize`를 제거한다.
- achievement 방어적 읽기는 목적이 명시된 별도 read-model builder로 이동한다.
- schema 2 physical field와 receipt presence semantics를 유지한다.

Phase 2 exit 시 public fragment API로 top-level validation을 우회할 수 없어야 한다.

#### Phase 3 — Immutable canonical state

mutable persisted DTO와 canonical runtime state를 분리한다.

- `CampaignSlotEntry.Empty`와 occupied immutable `CampaignSlotState`를 도입한다.
- state와 nested receipt/performance/clear-profile state에 public setter를 두지 않는다.
- private copy와 read-only wrapper를 사용해 collection mutation을 막는다.
- raw array를 `IReadOnlyList` 실제 객체로 그대로 노출하지 않는다.
- parser/factory/engine만 canonical state를 생성한다.
- receipt는 `Absent`, `PresentWithoutPayload`, `PresentWithPayload`를 명시적으로 보존한다.
- engine과 Production/Transient 내부를 immutable state로 전환한다.
- comic completion은 typed command로 이동하고 stageId를 expected-stage로 쓸지 제거할지 Phase 0 계약에
  따라 결정한다.
- new game는 state factory, diagnostic selection은 목적별 diagnostic command가 소유한다.

`ReplaceValidatedSlot(SaveSlotData)`를 public `ReplaceSlot(CampaignSlotState)`로 이름만 바꾸지 않는다.
immutable state도 stale full replacement 문제를 막지 못한다. 기존 replacement port는 Phase 4 consumer
migration을 위한 compatibility facade로만 유지한다.

#### Phase 4 — Validation, launch eligibility, maintenance 분리

- profile load failure는 `CampaignSaveLoadReport`가 소유한다.
- structural parse, empty/occupied, launch eligibility, UI action policy를 별도 result/type으로 만든다.
- `UnsupportedVersion`을 per-slot validation status에서 제거하고 profile report에만 둔다.
- MainMenu는 full slot replacement를 하지 않는다.
- level-group sync는 slot/stage/old-group precondition을 가진 narrow command 또는 `PrepareContinue`로
  transaction 안에서 수행한다.
- stale이면 write하지 않고 exact pending handoff만 clear한 뒤 UI를 refresh한다.
- DirectPlay와 seed import는 목적별 initialize/import command를 사용한다.
- 마지막 runtime consumer가 이동하면 general maintenance replacement port를 제거한다.

공개 배포 save가 없음을 다시 확인한 뒤 `RetiredCampaignSaveCompatibilityPolicy`의 `stage-5-1` 자동
보정은 제거를 기본으로 한다. archived `legacy-stage-5-1` content/catalog governance는 별개로 유지한다.
제품이 내부 save retention을 명시적으로 요구할 때만 typed maintenance plan으로 유지한다.

#### Phase 5 — Shim 제거와 architecture guard

- runtime mutable `SaveSlotData` business 사용을 제거한다.
- old mapper facade/public fragment와 `ReplaceValidatedSlot`을 제거한다.
- compatibility allowlist가 비면 adapter/test shim을 제거한다.
- immutable constructor/setter, engine composition, direct field assignment, mapper strictness,
  PlayerPrefs absence, save chance `1..3`을 architecture guard로 고정한다.
- chance `0` guard는 save document/state/command만 검사하고 presentation을 broad scan하지 않는다.
- README, pre-release policy, testing guide를 실제 최종 구조와 same revision evidence로 갱신한다.

### Validation 규칙

각 work package에서 변경 범위에 맞는 targeted fixture를 먼저 실행하고 다음 lane을 적용한다.

- 기본: `./run_tests.sh core`
- UI-facing port/result/controller 변경: `./run_tests.sh ui`
- Stages Editor fixture 전체: `./run_tests.sh full --filter <fixture>`
- broad integration 위험: 필요에 따라 broad `full`

최소 touched fixture 후보:

- `CampaignSlotTransitionCharacterizationTests`
- `CampaignSlotTransitionEngineTests`
- `CampaignSaveServiceTests`
- `CampaignSaveSlotStoreAdapterTests`
- `CampaignSlotMapperTests`
- `CampaignSaveArchitectureV2Tests`
- `SaveSlotValidationAndDirectPlayTests`
- `NormalCampaignCompletionReceiptTests`
- campaign achievement integration tests
- `CampaignProductionEntryTests`

filtered run에서 matching test가 0개면 pass evidence로 계산하지 않는다. broad `full`을 실행하지 않았으면
project-wide/full green을 주장하지 않는다. 기존 baseline failure와 touched regression을 구분한다.

### 매 work package 종료 시 수행할 일

1. 관련 source scan으로 old/new owner 위치를 확인한다.
2. `git diff --check`를 실행한다.
3. 실행한 테스트, 수치, 실패, 실행하지 않은 lane과 이유를 기록한다.
4. `Campaign-Save-Long-Term-Structural-Remediation.md`의 실행 기록 표에 행을 추가한다.
5. Decision을 `Ready for next package`, `Hold`, `Rollback` 중 하나로 기록한다.
6. `Hold`이면 다음 continuation에서 새 Phase를 시작하지 않고 실패 분석과 보강만 수행한다.

### 즉시 중단하고 사용자 결정을 요청할 조건

- Production과 Transient의 characterization 결과가 달라 canonical 동작 선택이 필요한 경우
- receipt present-null, processed-ID, chance, timestamp 의미 변경이 필요한 경우
- physical schema 2 field/version 변경이 필요한 경우
- public save compatibility나 PlayerPrefs migration을 새로 요구하는 상황
- retired save retention 여부가 확인되지 않은 상태에서 Phase 4를 진행해야 하는 경우
- 기존 사용자 변경과 같은 코드 구간이 충돌하여 보존 가능한 분리가 어려운 경우
- UI handoff/sync 원자성을 바꾸는 제품 동작 결정이 필요한 경우

### Goal 전체 완료 조건

다음 조건이 모두 충족될 때만 Goal을 완료한다.

- death, clear, comic 등 authoritative gameplay slot transition owner가 하나다.
- Production과 Transient가 동일 transition engine을 사용한다.
- canonical occupied state와 모든 nested state가 immutable하다.
- empty/occupied, corrupt diagnostic, unsupported profile schema가 서로 다른 타입/result다.
- strict mapper가 validation, filtering, normalization, repair를 수행하지 않는다.
- validation, launch eligibility, UI action policy, maintenance command, persistence write가 분리된다.
- MainMenu/DirectPlay/seed/test가 general full-slot replacement를 사용하지 않는다.
- runtime business code의 `SaveSlotData`와 compatibility shim이 제거되거나 명시된 raw boundary로 제한된다.
- campaign PlayerPrefs persistence와 saved chance `0` sentinel 재유입 경로가 없다.
- required targeted/core/UI evidence와 architecture guard가 같은 revision에서 통과한다.
- current architecture 문서와 실제 composition이 일치한다.

Goal 완료 보고에는 최종 구조, 제거한 기존 문제, tests run/not run, broad lane 상태, 남은 비범위를
명확히 포함하라. 단순히 예산이 부족하거나 한 Phase가 끝났다는 이유로 Goal을 완료 처리하지 마라.
