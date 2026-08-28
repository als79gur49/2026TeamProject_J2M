# Gameplay Wall Tick Cost Optimization — Master Plan Slice 3 Goal

- 현재 상태: `Hold — valid evidence incomplete`
- 현재 package: S3-A measurement-contract remediation
- S3-B·S3-C: valid S3-A signal과 사용자 continuation 승인 전까지 금지
- 최초 작성일: 2026-08-27
- 최신 상태 갱신: 2026-08-28 KST
- 실행 시작 기준 revision: `25e623a94890f803142990fbeb7c2e11615b2ed0`
- 상위 계획: [Gameplay Wall Tick Cost Optimization Plan](./Gameplay-Wall-Tick-Cost-Optimization-Plan.md)
- 실행 Prompt: [Gameplay Wall Tick Cost Optimization — Slice 3 Goal Prompt](./Gameplay-Wall-Tick-Cost-Optimization-Slice3-Goal-Prompt.md)
- 선행 상태: 상위 Slice 1의 `FinalEntities` 공유와 Slice 2의 Factory prefilter는 복구 closeout 기준 완료

## 1. 판정과 목표

Slice 3의 대상은 Wall 자체가 아니라 후보 밀도가 낮은 Cleanup에서 발생하는 불필요한 전체 엔티티 정렬·복사·순회다.

최종 목표는 다음과 같다.

> Cleanup 후보 밀도가 낮은 tick에서 불필요한 전체 엔티티 정렬·복사·순회를 제거한다. 후보 view의 유지, snapshot 운반, projected fast import 비용을 포함한 end-to-end 순이익을 같은 revision에서 증명하고, Removal → Timer → Transition 의미와 EventLog, FinalEntities, occupancy, auxiliary lifetime, determinism hash, FullCanonical trace, replay 결과를 정확히 보존한다. 순이익이 증명되지 않으면 production candidate package를 유지하지 않는다.

이 Goal은 곧바로 persistent index를 구현하라는 승인이 아니다.

- S3-A는 계측과 독립 reference oracle을 추가하는 진입 단계다.
- S3-B는 유지·운반 비용만 격리 측정한다.
- S3-C는 indexed executor의 실행 이득을 격리 측정한다.
- S3-B와 S3-C는 함께 retain 또는 rollback하는 하나의 production candidate package다.

같은 revision A/B/C 비교를 위해 명시적인 test/performance capture strategy seam을 둘 수 있다. 이는 자동 fallback이 아니다. official run manifest는 선택된 strategy를 기록하고, 최종 retain revision의 ordinary production composition은 승인된 executor 하나만 선택한다.

S3-A-only 측정은 B 진입 여부를 정하는 preliminary attribution/calibration이다. Production retain은 B/C 코드와 세 capture strategy가 모두 존재하는 하나의 final candidate revision에서 다시 수행한 official A/B/C campaign으로만 판정한다.

## 2. 실행 시작 비용 baseline과 현재 선행 조건

아래 비용·instrumentation inventory는 2026-08-27 실행 시작 baseline이다. 그 아래의 S3-A signal, Hold/defer, S3-B/S3-C 진입 규칙은 현재 normative gate다.

현재 base `CleanupProcessor`는 snapshot의 모든 엔티티를 ID 순서로 materialize하고 survivor 목록을 만든 뒤 Removal, Timer, Transition을 실행한다. 후보가 없어도 ordered/survivor/result collection과 전체 방문이 발생한다.

그러나 현재 workload diagnostics에는 다음 Slice 3 귀속 정보가 없다.

- base Cleanup 전체 방문 수와 survivor 복사 수;
- removal/timer/immediate 후보 수와 실제 처리 수;
- candidate membership 유지비;
- snapshot-carried candidate item 및 allocation;
- projected fast-import candidate item 및 별도 predicate rebuild 방문 수;
- `CleanupProcessor` 시간과 complete `RunCleanupPhase` 시간;
- 유효한 main-thread allocated bytes/tick 또는 동등한 allocation sample.

따라서 persistent candidate structure를 추가하기 전에 S3-A가 이 정보를 제공해야 한다.

- 유효한 timing/allocation signal로 Cleanup attribution이 material하지 않음이 증명되면 `Goal deferred — Cleanup attribution not material`로 종결한다.
- timing noise, allocation liveness 실패, workload/admission 불완전 등으로 판정할 수 없으면 `Hold — valid evidence incomplete`로 유지한다.
- `Hold` 상태에서는 S3-B/S3-C에 진입하지 않는다.

현재 `gameplay-performance`의 `stage-1-1` neutral-tick 결과와 Slice 1 historical 수치는 Slice 3 acceptance baseline이 아니다.

## 3. Contract 분류

### 3.1 StrongContract

다음은 구현 방식과 무관하게 고정한다.

- `WorldState`가 authoritative mutable gameplay state를 소유한다.
- `WorldSnapshot`은 snapshot 생성 시점의 immutable read/query seam이다.
- 허용된 Cleanup direct-write entrypoint는 `CleanupProcessor.Process`, `ExpireBoxInteractionLocks`, `ExpireEnemyGravityFieldAuraFields`, `ExpirePendingEnemyBlockedReactions`다.
- Cleanup direct-write 호출 순서는 `CleanupProcessor.Process` → box lock → aura field → pending reaction이며, 해당 tick의 단일 Cleanup write context를 사용한다.
- indexed executor와 base empty fast path는 `CleanupProcessor.Process` 경계 내부 구현으로만 도입한다. 새 production direct-write entrypoint를 추가하지 않는다.
- Wall과 `EntityType.None`도 generic Cleanup predicate에 참여한다.
- base Cleanup 의미 순서는 Removal → Timer → Transition이다.
- Removal된 ID는 같은 Cleanup의 모든 후속 Timer·Transition 처리에서 제외한다.
- `spawnTick == tickIndex`는 timer decrement만 건너뛴다.
- same-tick spawned entity도 dead/marked이면 제거된다.
- same-tick spawned Acting/Cooldown entity가 이미 `stateTimer <= 0`이면 transition 대상이다.
- timer event family 전체가 transition event family 전체보다 먼저 기록된다.
- 각 event family 내부 entity ID 순서는 오름차순이다.
- removal/timer/immediate-transition predicate와 각 candidate view의 entity ID ordering은 이번 최적화가 보존할 의미 계약이다.
- `stateTimer == 1`인 Acting/Cooldown entity는 같은 Cleanup에서 0으로 감소한 뒤 post-timer state를 사용해 transition한다.
- EventLog, FinalEntities, Solid/Unit/Projectile occupancy, authoritative auxiliary state, determinism hash, FullCanonical trace, replay 결과는 reference와 동일하다.
- `CleanupPhaseResult`의 RemovedEntityIds, TimerChanges, StateTransitions, EventLogEntries, RemovedUnitKinematicPoses, RemovedUnitContinuousLocomotionPoses 전체와 각 항목 순서는 reference와 동일하다.
- `KinematicPoseRemoved`와 `ContinuousLocomotionPoseRemoved` EventLog 및 이 carrier가 만드는 최종 kinematic/continuous presentation track은 reference와 동일하다.
- entity 제거는 정확한 entity-owned auxiliary-state cleanup matrix를 보존한다.
- 이미 생성되어 독립 수명을 가진 PendingCellImpact, GravityFieldAura 및 그 파생 lock은 source 제거만으로 조기 소멸하지 않는다.
- base candidate가 비어 있어도 box-lock, aura-field, pending-reaction expiry routine은 실행된다.
- derived candidate metadata는 canonical hash나 trace payload에 추가하지 않는다.

### 3.2 CurrentPolicy

다음은 증거에 따라 바꿀 수 있다.

- full entity scan;
- ordered entity list와 survivor list의 물리적 materialization;
- 세 개의 물리적 `SortedSet<int>` 사용 여부;
- 단일 sparse ID→flags index 또는 복수 ordered container 사용 여부;
- snapshot carrier의 내부 wrapper와 merge 알고리즘;
- `CleanupProcessor.Process` 경계 내부 private helper/class 분할.

공개/내부 계약은 `SortedSet`이 아니라 entity-ID-ordered candidate view다.

### 3.3 Entity removal lifetime matrix

현재 entity 제거 계약은 다음과 같다. 새 entity-owned store가 추가되면 S3-B 전에 이 표와 reference fixture를 함께 갱신한다.

| 분류 | 제거 시 처리 |
|---|---|
| occupancy와 `_entitiesById` | 제거 |
| enemy action, blocked reaction, patrol, charge, execution lock, jump, glide, utility, summon behavior | 제거 |
| box interaction lock, phased state | 제거 |
| player damage/control state | 제거 |
| summoned-entity metadata, enemy-definition binding | 제거 |
| unit kinematic/continuous locomotion authoritative state | 제거하되 removal pose carrier와 EventLog는 제거 전 snapshot 값으로 보존 |
| 이미 생성된 PendingCellImpact | source 제거만으로 조기 제거하지 않음 |
| 이미 방출된 GravityFieldAura와 다른 entity가 소유하는 source-derived target lock | 원래 lifetime까지 유지 |

remove 뒤 같은 ID를 respawn해도 이전 entity-owned state가 새 entity에 재부착되지 않아야 한다.

## 4. Candidate 의미와 authoritative mutation seam

세 candidate view의 의미는 다음과 같다.

| View | Predicate |
|---|---|
| Removal | `hp <= 0 || markedForDeath` |
| Timer | `stateTimer > 0` |
| Immediate transition | `stateTimer <= 0 && state is Acting or Cooldown` |

현재 production에서 이 predicate를 바꿀 수 있는 쓰기는 아래 seam으로 수렴한다.

- spawn;
- damage와 mark를 포함한 central stored-entity update;
- state/timer change;
- remove;
- fast snapshot restore/import;
- respawn의 fresh entity spawn.

S3-B 구현 전에 source scan과 focused test로 이 inventory를 다시 고정한다. 새 writer가 발견되면 index를 구현하기 전에 해당 writer를 authoritative seam으로 합류시키거나 Goal의 유지 규칙을 갱신한다.

Stored-entity update는 모든 호출에서 세 container를 무조건 remove/add하지 않는다. old/new entity의 세 predicate 결과를 비교하고 membership이 실제로 달라진 view만 갱신한다.

## 5. Snapshot과 fast import 계약

- snapshot candidate carrier는 snapshot-owned immutable ordered storage다.
- mutable `WorldState` collection을 직접 alias하지 않는다.
- 빈 view는 `Array.Empty<int>()` 또는 동등한 공유 empty storage를 사용한다.
- 이전 snapshot의 carrier는 이후 world mutation과 same-ID reuse 뒤에도 변하지 않는다.
- fast import는 candidate predicate를 계산하기 위한 별도 O(N) entity scan을 추가하지 않는다.
- 기존 필수 entity-copy loop에 분류를 융합하거나 ordered carrier를 O(K)로 import할 수 있다.
- fast import가 mutable tree node를 매 materialization마다 재할당하는 구조라면 그 비용을 S3-B allocation에 포함한다.
- fast/slow projected-world 경로는 동일한 candidate contents와 최종 authoritative 결과를 만든다.
- production snapshot factory는 정확한 candidate carrier를 필수로 제공한다.
- test-only raw `WorldSnapshot` constructor는 entity에서 candidate를 독립적으로 한 번 derive하거나 production factory로 수렴한다.
- candidate가 존재하는 snapshot에 silent empty carrier를 허용하지 않는다.

“fast import O(N) 제거”라고 보고하지 않는다. 정확한 structural claim은 “기존 entity copy 외 별도 candidate-predicate O(N) scan 0”이다.

## 6. Indexed Cleanup execution 계약

S3-C executor의 순서는 다음으로 고정한다.

1. ordered Removal 후보를 처리하고 removed ID set을 만든다.
2. ordered Timer 후보에서 removed ID와 `spawnTick == tickIndex`를 제외한다.
3. timer를 감소한 local entity state와 TimerTicked operation을 보존한다.
4. timer가 0이 된 `entityId + postTimerState`를 local transition carrier에 추가한다.
5. 이를 pre-existing Immediate transition 후보와 ID 순서로 merge/dedupe한다.
6. merged 후보에서 removed ID를 제외한다.
7. 모든 TimerTicked operation을 ID 순서로 먼저 commit한다.
8. post-timer local state를 사용해 모든 StateTransitioned operation을 ID 순서로 commit한다.

원본 pre-Cleanup snapshot을 다시 읽어 timer=1 entity의 transition을 판정해서는 안 된다. 이를 위해 Cleanup write context에 authoritative read API를 추가하거나 중간 snapshot을 새로 만드는 것은 이 Goal의 승인된 해법이 아니다.

base candidate 세 view가 모두 비었으면 `CleanupPhaseResult.Empty`를 공유할 수 있다. 이 fast path는 base `CleanupProcessor`만 건너뛰며 `RunCleanupPhase`의 후속 expiry routine을 건너뛰지 않는다.

## 7. Independent reference oracle

Reference는 indexed implementation과 상관된 오류를 공유하지 않아야 한다. Pure ordered operation plan을 기본 구현으로 사용한다.

- snapshot entity를 직접 full scan한다.
- candidate carrier를 읽지 않는다.
- indexed candidate predicate helper를 그대로 재사용하지 않는다.
- pure ordered operation plan을 만든다. Isolated identical world/recording context가 보조 검증에 필요하더라도 기존 허용 direct-write entrypoint만 사용하며 새 production mutation entrypoint를 만들지 않는다.
- reference와 indexed mutating processor를 같은 live write context에 연속 적용하지 않는다.
- mismatch는 즉시 실패시키며 production full-scan fallback으로 숨기지 않는다.
- reference comparison은 명시적 test/Development capture에서만 켠다.
- official performance candidate에서는 reference comparison을 끄고 strategy counter로 indexed path와 fallback 0을 확인한다.

현재 구현을 두 번 실행해 결과가 같은지만 보는 determinism test는 독립 reference parity를 대체하지 않는다.

## 8. 실행 패키지

### S3-A — Attribution, reference, strategy seam

Production executor와 authoritative result를 바꾸지 않고 다음을 추가한다.

- full-scan entity visits;
- survivor copied entities;
- removal/timer/immediate candidate counts;
- removal/timer/transition processed counts;
- zero-candidate base fast-path opportunity count;
- reference/indexed executor invocation count;
- hidden fallback count;
- candidate membership checks/adds/removes;
- snapshot candidate array count와 carried item count;
- fast-import candidate item과 separate predicate-rebuild entity visit count;
- `CleanupProcessor` marker;
- complete `RunCleanupPhase` marker;
- main-thread allocated bytes/tick 또는 승인된 동등 allocation sample;
- independent reference operation oracle.

S3-A exit gate:

- counter가 authoritative state, ordering, hash, FullCanonical trace에 영향을 주지 않는다.
- reference oracle이 현재 full-scan 결과와 exact parity를 보인다.
- candidate-empty/Wall-heavy와 candidate-dense/mutation-heavy workload가 재현 가능하다.
- runner, probe, admission validator, aggregator가 두 workload와 A/B/C strategy schema를 합성 artifact fixture로 검증한다.
- S3-A runtime에서는 A만 executable이다. B/C 선택 요청은 unavailable로 fail closed하며 A로 fallback하지 않는다.
- 실제 A calibration은 target의 세 candidate count가 모든 measured tick에서 0인지, stress schedule fingerprint가 frozen plan과 일치하는지, A maintenance/carriage count 0, full-scan invocation이 executed tick count와 일치, indexed invocation/fallback/invariant mismatch 0을 검증한다.
- valid `CleanupProcessor`/`RunCleanupPhase` per-tick sample count가 executed tick count와 일치한다.
- 유효한 main-thread allocation delta/tick 또는 승인된 동등 신호가 존재한다.
- official이 아닌 A calibration 뒤 `CleanupProcessor` material threshold, complete `RunCleanupPhase` containment threshold, target whole-tick end-to-end benefit threshold, noise 판정식, B tax ceiling, allocation ceiling을 문서 amendment로 고정한다.
- diagnostics/reference capture-off 상태에서 authoritative parity, hot-path allocation 0, 사전 고정 whole-tick 비악화 gate를 통과해야 S3-A를 독립 retain할 수 있다.
- Cleanup 귀속이 material하지 않으면 Slice 3를 defer한다.

### S3-B — Maintenance and carriage isolation / pre-C gate

Candidate maintenance와 immutable snapshot carrier를 추가하지만 production Cleanup executor는 full scan을 유지한다.

S3-B pre-C entry gate:

- 세 candidate view가 independent reference와 exact parity다.
- snapshot과 fast/slow import parity가 유지된다.
- candidate predicate rebuild를 위한 별도 entity visit은 0이다.
- irrelevant entity update는 membership을 불필요하게 변경하지 않는다.
- preliminary A/B에서 candidate-empty와 candidate-dense workload 모두 사전 고정한 whole-tick maintenance tax ceiling 이내다.
- preliminary A/B allocation 증가는 사전 고정한 B allocation tax ceiling 이내다. B 자체에 A 대비 allocation 0을 요구하지 않는다.
- 실제 A/B에서 같은 workload의 entity/Wall/mutation/executed tick count와 schedule fingerprint가 동일하다.
- A maintenance/carriage count는 0이고, A/B full-scan invocation은 executed tick count와 일치하며 indexed invocation은 0이다.
- invariant mismatch와 hidden fallback은 0이다.

유효한 allocation signal 또는 사전 승인된 동등 signal을 얻지 못하면 S3-A exit gate는 닫힌다. 이 경우 상태는 `Hold — valid evidence incomplete`이며 S3-B/S3-C에 진입하지 않는다.

측정 seam 변경이나 동등 signal 승인은 별도 normative Goal amendment와 사용자 승인을 필요로 한다. 승인 후 새 calibration identity로 S3-A를 다시 실행하고, timing·allocation·oracle·admission gate가 모두 통과한 경우에만 S3-B 진입을 요청할 수 있다. S3-B pre-C gate는 C 구현 허가일 뿐 B의 final production retention 판정이 아니다.

### S3-C — Indexed executor and base empty fast path

S3-B와 같은 candidate representation을 유지하고 Cleanup executor만 indexed 경로로 바꾼다.

S3-C pre-campaign 검증 동안 ordinary non-capture production composition은 full-scan executor를 유지하고 C는 명시적 test/capture strategy로만 실행한다.

S3-C pre-campaign gate:

- candidate-empty workload의 base Cleanup full-scan visit이 N에서 0으로 감소한다.
- ordered entity/survivor/result collection work가 S3-A에서 고정한 structural target까지 감소한다.
- EventLog, FinalEntities, occupancy, auxiliary lifetime, hash, FullCanonical trace, replay가 exact parity다.
- `CleanupPhaseResult`의 모든 field, removal pose EventLog, 최종 kinematic/continuous presentation track이 exact parity다.
- 실제 A/B/C에서 같은 workload의 entity/Wall/mutation/executed tick count와 schedule fingerprint가 동일하다.
- B/C maintenance와 snapshot carrier count가 동일하고, A maintenance/carriage count는 0이다.
- A/B full-scan invocation과 C indexed invocation이 executed tick count와 일치한다.
- invariant mismatch와 hidden fallback은 0이다.

이 gate가 통과해도 바로 campaign에 진입하지 않는다. Exact validation/counter/revision을 보고하고 사용자 continuation 및 clean revision을 만들 별도 commit 승인을 받는다. 승인 뒤 ordinary non-capture production composition이 selector 없이 indexed executor를 직접 선택하도록 만든 revision을 final candidate revision으로 고정한다. Infrastructure wiring guard와 Integration runtime-composition test로 이 경로를 직접 증명하고, 그 동일 revision에서 focused/core/replay를 다시 실행한 뒤 official A/B/C campaign으로 B/C final retention을 판정한다.

## 9. Tests-first matrix

### Predicate와 overlap

- hp `-1/0/1`, mark false/true;
- state None/Idle/Acting/Cooldown/Sliding/Dead와 unknown cast;
- timer `-1/0/1/2`;
- type Wall/`EntityType.None`/Unit/Box;
- removal + timer overlap은 removal only;
- removal + immediate-transition overlap은 removal only;
- timer=1 Acting/Cooldown은 TimerTicked 뒤 같은 Cleanup에서 transition;
- pre-existing zero/negative Acting/Cooldown transition;
- same-tick spawn timer>0은 decrement하지 않음;
- same-tick spawn zero-timer Acting/Cooldown은 transition;
- same-tick spawn dead/marked는 removal.

### Mutation sequence

- ApplyDamage;
- MarkDestroy;
- ApplyStateChange와 timer change;
- predicate와 무관한 update;
- remove 뒤 same-ID respawn;
- multi-operation FinalizationBatch와 projected overlay;
- old snapshot 생성 뒤 world mutation;
- fast import에서 세 candidate group 모두 non-empty;
- fast import 직후 추가 mutation.
- production snapshot factory와 test-only raw constructor가 동일 candidate contents를 만든다.
- candidate가 있는 raw snapshot이 silent empty carrier를 만들지 않는다.

### Exact result와 lifetime

- mixed fixture에서 모든 CleanupRemoved, 모든 TimerTicked, 모든 StateTransitioned의 family/ID 순서;
- base Cleanup event 뒤 box-lock/aura/pending-reaction expiry 상대 순서;
- FinalEntities와 occupancy exact parity;
- entity-owned auxiliary cleanup matrix;
- remove 뒤 same-ID respawn에 이전 entity-owned auxiliary state가 재부착되지 않음;
- source removal 뒤 independently-lived PendingCellImpact 유지;
- source removal/death 뒤 emitted GravityFieldAura와 derived lock의 원래 lifetime 유지;
- `CleanupPhaseResult` 여섯 field와 removal pose carrier ordering exact parity;
- pose-removal EventLog와 최종 kinematic/continuous presentation track exact parity;
- determinism hash, FullCanonical trace, replay exact parity.

### Strategy와 structure

- reference/indexed strategy invocation counter;
- fallback 0;
- invariant mismatch 0;
- S3-A synthetic A/B/C schema fixture와 실제 A-only unavailable/fail-closed selection;
- S3-B 실제 A/B invocation 및 workload parity;
- S3-C/final 실제 A/B/C invocation 및 workload/carrier parity;
- final ordinary non-capture composition이 selector 없이 indexed executor를 직접 선택하는 wiring/runtime parity;
- empty carrier shared;
- old snapshot carrier immutable;
- separate fast-import candidate predicate scan 0;
- zero-candidate base Cleanup full-scan visit 0;
- candidate processed count와 실제 operation count exact match.

## 10. Validation

각 production package에서 관련 focused test를 먼저 실행하고 같은 revision에서 다음을 실행한다.

```bash
./run_tests.sh full --filter 'CleanupPhaseScenarioTests;SnapshotBudgetGuardTests;ProjectedWorldFastImportCoreTests;WorldSnapshotAndPresentationTests'
./run_tests.sh --integration-replay --filter TickReplayDeterminismTests
./run_tests.sh core
```

추가 원칙:

- pure predicate/index tests는 Core에 둘 수 있다.
- `TickPipeline` 또는 runtime composition을 실행하는 parity test는 Integration에 둔다.
- reflection/constructor/wiring guard는 Infrastructure에 둔다.
- focused, core, replay 결과를 별도 claim으로 보고한다.
- broad `full`은 현재 documented red baseline이며 실행하지 않았으면 그 이유를 기록한다.
- full/broad lane을 실제로 실행하고 통과하지 않은 상태에서 project-wide green이나 full regression closure를 주장하지 않는다.

## 11. Performance campaign

### Workloads

1. target: candidate-empty/Wall-heavy neutral workload;
2. stress: deterministic candidate-dense/mutation-heavy workload.

두 workload 모두 candidate 분포, mutation count, entity count, Wall count, workload ID, seed, schedule hash를 manifest에 기록한다.

- target workload는 candidate가 희소한 실제 최적화 대상이며 사전 고정 component material improvement와 whole-tick end-to-end benefit을 모두 요구한다.
- stress workload는 maintenance/merge 안전성 대상이며 speedup을 요구하지 않는다. Whole-tick과 allocation safety ceiling을 요구한다.
- 두 workload 결과를 하나의 평균으로 합쳐 통과시키지 않는다.

### Calibration and campaign freeze

S3-A preliminary calibration은 official evidence가 아니다. Calibration 결과로 다음 exact 값을 Goal amendment에 기록한 뒤 첫 official run 전에 동결한다.

- target `CleanupProcessor` material improvement threshold;
- complete `RunCleanupPhase` containment threshold와 합격 방식;
- target whole-tick A→C end-to-end benefit threshold;
- noise 판정식;
- B whole-tick/allocation maintenance tax ceiling;
- target/stress whole-tick과 allocation safety ceiling;
- sample count, warm-up, retry, aggregation 규칙.

Campaign plan, admission validator, aggregator content의 SHA-256을 기록한다. Official measurement worktree는 clean이어야 하고 `HEAD == manifest revision == metrics revision` 및 frozen worktree-diff hash가 일치해야 한다. 첫 official run 뒤 source/diff, runtime code, workload, validator, aggregator, plan 중 하나라도 바뀌면 기존 cohort를 이어가지 않고 새 campaign ID로 처음부터 실행한다.

새 remediation evidence schema와 trust boundary는 승인된 [`Tools/contracts/gameplay_cleanup_slice3_evidence_contract_v4.md`](../../Tools/contracts/gameplay_cleanup_slice3_evidence_contract_v4.md)를 따른다. v3는 historical predecessor다. Manifest `PASS`는 persisted report의 canonical recomputation, CLI coherence, live pre/post tree, attempt/cohort identity, actual artifact hash, validator/aggregator/manifest/workload-contract provenance가 모두 일치할 때만 허용한다. Manifest 자체의 hash inventory는 official admission을 대체하지 않는다.

### Official run order

- A: final candidate revision에서 candidate maintenance/carriage off + full-scan Cleanup + S3-A diagnostics;
- B: candidate maintenance/carriage + full-scan Cleanup;
- C: 같은 candidate representation + indexed Cleanup.

각 workload/strategy 조합마다 admitted full-run warm-up 하나를 먼저 확보하고 항상 폐기한다. 그 뒤 각 workload에서 A/B/C를 세 번씩 실행한다. 권장 artifact policy는 build-once/run-many이며, 모든 attempt는 task-owned 독립 persistent/temp/output 영역과 동일한 initial-world fingerprint를 가져야 한다.

```text
Discarded warm-ups: target C -> B -> A, stress A -> B -> C
Official block 1: target A -> B -> C, stress C -> B -> A
Official block 2: target C -> A -> B, stress B -> A -> C
Official block 3: target B -> C -> A, stress A -> C -> B
```

고정 campaign은 `6 discarded admitted full-run warm-ups + 18 admitted official runs + rejected admission retries`다. Player 내부 frame warm-up은 full-run warm-up을 대체하지 않는다. Tick count, 해상도, quality, trace mode, machine 상태, admission rule을 첫 official run 전에 고정한다.

세 상태는 같은 source revision의 명시적 capture strategy로 선택한다.

- A는 candidate maintenance/carriage를 끄고 reference full-scan executor를 사용한다.
- B는 candidate maintenance/carriage를 켜고 reference full-scan executor를 사용한다.
- C는 같은 candidate maintenance/carriage를 켜고 indexed executor를 사용한다.
- strategy selection은 manifest와 strategy counter에 일치해야 하며 runtime 상태에 따라 자동 전환하지 않는다.
- strategy는 capture 시작 시 한 번 resolve하며 mutation/Cleanup hot path에서 command-line을 재조회하지 않는다.
- ordinary non-capture production composition은 selector 없이 승인된 executor를 직접 구성한다.
- 동일 Player artifact를 모든 strategy와 warm-up/official attempt에 재사용한다. Harness가 이를 지원하지 못하면 첫 official run 전에 Goal amendment로 multi-build 정책을 승인하고, runtime-tree/harness hash 동일성, attempt별 artifact hash, persistent-state 격리를 admission에서 검증한다.

다음을 각각 보고한다.

- A→B: maintenance/carriage tax;
- B→C: indexed execution benefit;
- A→C: production package net benefit.

각 workload/strategy의 raw JSON p95 세 개에서 median을 계산하고 pass/fail 판정 뒤에만 표시값을 반올림한다. Admission 실패만 동일 slot에서 retry하며 모든 attempt를 보존한다. 유효한 run을 결과 확인 뒤 제외·warm-up 재분류·추가 실행하지 않는다. 유효 cohort가 불확정이면 amendment와 새 campaign ID로 전체 campaign을 다시 시작한다.

서로 다른 revision이나 Slice 1 historical artifact를 같은 acceptance claim으로 합치지 않는다.

## 12. Retain, reject, rollback

Retain에는 다음이 모두 필요하다.

- S3-A attribution/measurement gate 통과;
- S3-B pre-C correctness/structural/preliminary tax gate 통과;
- S3-C pre-campaign structural/semantic/replay gate 통과;
- final candidate revision의 admitted campaign에서 target workload `CleanupProcessor` A→C component material improvement 충족;
- complete `RunCleanupPhase` A→C가 사전 고정 containment threshold와 합격 방식을 충족;
- target workload whole-tick A→C가 사전 고정 end-to-end benefit threshold를 충족하며, 0보다 느리거나 noise 안의 개선이면 실패;
- target workload end-to-end allocation 비악화 또는 material improvement;
- stress workload A→C whole-tick `<= +5%` 및 사전 고정 allocation safety ceiling 충족;
- B maintenance/allocation tax가 사전 고정 ceiling 이내이고 B→C executor benefit을 별도 보고;
- production fallback 0.

분기 기준:

- admitted·noise-valid timing/allocation evidence에서 Cleanup attribution이 non-material임이 입증됨: S3-B/S3-C defer. Admission, allocation liveness, noise 또는 workload signal이 불완전하면 Hold.
- S3-B pre-C 실패: B candidate representation과 carrier reject; C 미진입; full scan 유지.
- S3-C semantic/structural 실패: S3-B/S3-C production package 전체 제거; full scan 유지.
- target component/complete-phase/whole-tick improvement가 noise 안이거나 각 material/containment/end-to-end threshold 미달: B/C reject 또는 새 campaign으로 defer.
- target만 개선되고 stress safety ceiling이 실패: B/C rollback 또는 representation 재설계 후 새 campaign.
- allocation signal을 검증하지 못함: 실패로 단정하지 않고 Hold; measurement seam 복구 또는 동등 signal을 승인하는 normative Goal amendment 뒤 새 S3-A recapture 필요.
- final A→C timing/allocation 실패: B/C rollback; full scan 유지.

S3-A diagnostics/reference seam은 capture-off allocation 0과 non-regression gate를 통과한 경우에만 production package와 독립적으로 유지할 수 있다. S3-B/S3-C의 candidate maintenance, snapshot carrier, fast import, indexed executor는 하나의 rollback unit이다.

## 13. Scope와 non-goal

이번 Goal에 포함하지 않는다.

- Wall을 authoritative entity, Solid occupancy, FinalEntities, hash, replay에서 제거;
- `EntityType.None`을 immutable Wall로 간주;
- traversal, placement, settlement legality 변경;
- Cleanup 뒤 auxiliary expiry routine의 별도 최적화;
- Cleanup write context에 authoritative read seam 추가;
- 중간 authoritative snapshot 추가;
- candidate density 기반 hybrid executor 자동 전환;
- canonical trace schema 변경;
- static presentation cache 또는 immutable snapshot partition.

## 14. 실행 상태와 완료 선언

실행 및 종결 상태를 다음으로 구분한다.

- `in progress/provisional`: 아직 다음 gate 또는 유효 evidence가 남아 있음;
- `Hold — valid evidence incomplete`: 유효한 measurement/admission evidence가 부족한 비종결 상태이며 retain/reject를 단정하지 않음;
- `Goal complete — indexed package retained`: 모든 semantic/validation/final campaign gate를 통과하고 B/C를 production에 유지;
- `Goal closed/rejected — full scan retained`: 유효한 조사와 campaign이 B/C reject를 결정하고 full scan을 유지;
- `Goal deferred — Cleanup attribution not material`: S3-A가 현재 비용 우선순위가 아님을 증명;
- `Goal blocked — infrastructure/authority`: 반복 재현되는 infrastructure blocker 또는 필요한 권한 부족으로 판정 불가.

Retain 완료에는 다음이 모두 필요하다.

- StrongContract matrix와 independent reference parity 통과;
- focused/core/replay evidence가 같은 retained revision에 존재;
- 6 warm-up과 A/B/C 18-run campaign admission/artifact가 유효;
- S3-B 비용과 S3-C 이득을 분리해 보고;
- target component/complete-phase/whole-tick end-to-end benefit, stress safety, final allocation gate 통과;
- open risk, 미실행 lane, non-claim, rollback revision을 closeout에 기록;
- retain 결정과 실제 ordinary production path가 일치.

Reject/defer는 최적화 성공이나 `Goal complete`가 아니지만 유효하게 닫힌 조사 결과다. 구조적 전체 순회 제거만으로 retain 완료를 선언하지 않으며, `+5%` safety ceiling 안의 회귀를 성능 개선이라고 부르지 않는다.

## 2026-08-28 KST — Goal A / S3-A hard-gate progress

Goal objective: production Cleanup executor를 바꾸지 않고 Slice 3 attribution, independent oracle, deterministic workload, strategy seam, admission/aggregation schema를 확립하고 S3-A hard gate를 판정한다.

Current package: S3-A

Status: `Hold — valid evidence incomplete`

Revision / branch / runtime-tree hash: `25e623a94890f803142990fbeb7c2e11615b2ed0` / `codex/third-party-license-inventory` / runtime-tree SHA-256 `d06a7ad31c212d7eea0904a3678a430a31ab9324b3c02733b1c5b3dcbf3fcb41`; harness SHA-256 `0270542f788a26e1d9feb07e1ed0e32aec2cde63642a9e8ba901d48c2cd46df8`.

Pre-existing user changes preserved: 실행 전부터 수정/추가되어 있던 `Gameplay-Wall-Tick-Cost-Optimization-Plan.md`, `README.md`, 이 Goal Plan과 authoritative Goal Prompt를 되돌리거나 덮어쓰지 않았다. 이 block만 Prompt가 요구한 진행 기록으로 Goal Plan 끝에 추가했다. Commit/push는 수행하지 않았다.

Tests-first red evidence:

- initial diagnostics/oracle fixture: EditMode 4 total / 2 failed; full-scan counter가 0이었고 independent oracle scaffold가 removal result를 만들지 못해 assertion red를 확인한 뒤 구현했다.
- diagnostics capture-off fixture: EditMode 6 total / 1 failed; disabled no-op allocation scaffold `-1` 대 expected `0` assertion red를 확인한 뒤 구현했다.
- admission fixture: Python 6 tests / 10 failing subcases; `NOT_IMPLEMENTED` verdict red 뒤 A-only 및 future A/B/C pass/reject matrix를 구현했다.
- calibration aggregator fixture: Python 1 test / 1 error; tests-first `NotImplementedError` 뒤 exact formula/report schema를 구현했다.

Focused/core/replay exact counts:

- `./run_tests.sh full --filter 'CleanupSlice3AttributionSimulationTests;CleanupPhaseScenarioTests;SnapshotBudgetGuardTests;ProjectedWorldFastImportCoreTests;WorldSnapshotAndPresentationTests'`: EditMode 142/142 passed, PlayMode matching 0.
- `./run_tests.sh --integration-replay --filter TickReplayDeterminismTests`: EditMode 59/59 passed.
- `./run_tests.sh core`: EditMode 228/228 passed; PlayMode 111/111 passed.
- `./run_tests.sh ui`: EditMode 1340/1340 passed.
- Python final regression set (`cleanup Slice 3 admission/calibration` + existing performance admission/campaign): 26/26 passed.

Strategy and structural counters:

- actual runtime strategy: A only; B/C selection requests fail closed and do not fall back to A.
- target, each of three repetitions: 200 executed ticks, 200 full-scan invocations, removal/timer/immediate candidates `0/0/0`, indexed invocation 0, hidden fallback 0, invariant mismatch 0, all maintenance/carriage counters 0.
- stress, each repetition: 200 executed ticks, 19,200 scheduled mutations, removal/timer/immediate candidates `3,200/6,400/6,400`, 200 full-scan invocations, indexed invocation 0, hidden fallback 0, invariant mismatch 0, all A maintenance/carriage counters 0.
- target/stress entity and Wall count `256/256`; both use initial-world fingerprint `2f5272157600d5f151b8a234701247b9557d31c3711396b1a80a0c7fb3dc9f38`.
- target schedule hash `fc2e746fb21f5ac62a759fced0a534e189f05a3e7f297c0fb8118564877bd9f2`; stress schedule hash `40c32c71e896544d7cfffabbf89f8c273279557c56faeb56af73853b3690e52a`.
- independent oracle exact parity covers all six `CleanupPhaseResult` fields. Capture on/off parity covers EventLog, FinalEntities, phase trace, determinism hash, FullCanonical trace, and final kinematic/continuous presentation tracks. Existing Cleanup order/lifetime fixtures remain green.

Performance evidence paths:

- final raw calibration: `/mnt/d/J2M/evidence/gameplay-performance/20260827T155701Z/performance-metrics.json`.
- hard-gate machine-readable report: `/mnt/d/J2M/evidence/gameplay-performance/20260827T155701Z/cleanup-s3a-hard-gate-hold.json` (SHA-256 `e6a37137da571e51378d1c52ef674eb93e76ce51141330568eb42ac75ba8aeb9`).
- final Player build: `/mnt/d/J2M/builds/gameplay-performance/20260827T155701Z`.
- an intermediate A-only run at `/mnt/d/J2M/evidence/gameplay-performance/20260827T154540Z` passed the pre-existing gameplay-performance lane, but its superseded allocation-block signal and 34.284965% noise are not accepted as this gate's final evidence.

Admission/retry status:

- workload identity, schedule, counter, oracle, A maintenance, invocation, fallback/invariant portions are structurally consistent.
- final Cleanup admission is `REJECTED`: all four one-tick-per-frame allocation phases produced 0/100 valid samples because `GC Allocated In Frame` is unavailable in this non-development Player.
- the final pre-existing gameplay-performance admission also stopped at `REJECTED_SAMPLE_COUNT` because gameplay-neutral GPU samples were 599/600. This is separate from, and not used to excuse, the Cleanup allocation rejection.
- validator SHA-256: `2f134204ab78e8e24369cb3055c2b4ff42b0648a938f2e4b82556d5507075399`; aggregator SHA-256: `b1880208f4e70b7c30a9c9c401bcf89e8ddbec209a3646ffd8576c4469288888`.

A→B / B→C / A→C observations: not run; S3-B/S3-C were not entered. Final target A raw p95 milliseconds were CleanupProcessor `[0.21117, 0.145685, 0.16838]`, complete RunCleanupPhase `[0.69161, 0.17505, 0.17068]`, and whole tick `[2.494635, 2.641935, 2.64751]`. Median component/whole share was 6.373359%, but the frozen candidate noise formula observed 297.589260% due complete-phase variance, so neither a material/non-material conclusion nor B/C threshold use is valid.

Allocation signal: EditMode proves disabled diagnostics no-op allocation delta 0 and authoritative capture-off parity. Release-like Player allocation is invalid: target/stress capture on/off each had 0/100 valid frame samples. Therefore capture-off end-to-end allocation/non-regression and future B tax/target/stress allocation ceilings cannot be frozen.

Calibration/freeze status:

- measured calibration identity is exact: 3 round-robin repetitions, 200 internal warm-up ticks and 200 measured ticks per workload/repetition; allocation probe attempted 30 warm-up + 100 measured frames per target/stress capture on/off phase.
- intended noise formula is `max((max raw p95 - min raw p95) / median raw p95 × 100)` over target A CleanupProcessor, complete RunCleanupPhase, and whole-tick metrics, with minimum 5% floor and maximum valid calibration noise 10%.
- intended threshold formulas remain provisional and are **not campaign-frozen**: component material improvement `max(10%, 2×noise floor)`; complete RunCleanupPhase improvement `>= noise floor`; target whole-tick benefit `max(3%, noise floor)`; B whole-tick tax `max(5%, noise floor)`; B allocation tax `max(256 bytes/frame, 5% of A)`; target allocation regression `0`; stress whole-tick regression `<= +5%`; stress allocation regression `max(256 bytes/frame, 5% of A)`.
- retry/aggregation rules are frozen as procedure only: admission before performance verdict; median of three raw JSON p95 values; round only after verdict; admission-only retry, maximum two retries per slot, preserve all attempts; build-once/run-many; 6 discarded admitted full-run warm-ups and 18 admitted official runs.
- immutable campaign procedure: SHA-256 the exact UTF-8 bytes of canonical campaign-plan JSON, validator, and aggregator; write all hashes into the plan before the first official run and require exact equality for every attempt. Any runtime/workload/tool/plan hash change or uncertain cohort requires a new campaign ID and full restart. No official campaign plan hash was created because S3-A is Hold.

Open risks: allocation-capable release-like measurement seam is unresolved; component/complete-phase p95 variance exceeds the validity ceiling; diagnostics/oracle are provisional and do not authorize B/C; ordinary production composition remains the current full-scan executor.

Tests not run / reason: broad unfiltered `full` was not run because the documented full baseline is red and touched-cluster/focused/core/replay/UI evidence was run separately. Official A/B/C campaign, S3-B, and S3-C were forbidden by this hard gate and not run.

Next safe action: obtain or explicitly approve an allocation-capable release-like measurement seam and stabilize component timing calibration, then rerun S3-A under a new calibration identity. Do not begin S3-B or S3-C without a valid S3-A result and explicit user continuation approval.

## 2026-08-28 KST — S3-A measurement-contract remediation

Status remains `Hold — valid evidence incomplete`. This amendment records implementation hardening only; it does not replace or reinterpret the preserved `20260827T155701Z` rejected evidence and does not authorize S3-B/S3-C.

Implemented remediation:

- split capture modes into timing, structural counters, and reference comparison so timed samples no longer execute candidate-count bookkeeping or the independent oracle;
- upgraded the independent oracle from result-only comparison to an ordered commit-operation plan and compare actual `ICleanupCommitContext` calls for Removal → Timer → Transition order as well as all six `CleanupPhaseResult` fields;
- replaced frame-wide allocation sampling with `GC.GetAllocatedBytesForCurrentThread()` deltas immediately around exactly one synthetic tick; zero-byte samples are valid only when the forced-allocation liveness control passes, and exact sample cardinality remains mandatory;
- added per-strategy allocation identity, so future A/B/C evidence requires the complete `(strategy, workload, diagnostics on/off)` matrix and cannot reuse A allocation phases for B/C;
- froze workload contract v2 in `Tools/contracts/gameplay_cleanup_slice3_workloads_v2.json`; seed now controls initial entity placement and stress-operation entity IDs, the schedule hash is derived from the canonical scheduled-operation template consumed by the synthetic workload executor, conditional respawn execution is separately reflected by observed mutation counts, and the initial-world fingerprint includes board/topology plus every authoritative `EntityState` field;
- made workload admission compare against the repository-owned frozen contract so synchronized changes to metrics identity fields are rejected;
- made excessive calibration noise produce `HOLD_INVALID_SIGNAL` with no thresholds, and made the aggregator write that report before returning failure;
- added preflight and captured-artifact manifests before admission, preserved a machine-readable Cleanup admission summary, and ran the gameplay and Cleanup admissions without suppressing either result.

Frozen v2 workload identity:

| workload | seed | schedule SHA-256 | initial-world SHA-256 |
|---|---:|---|---|
| `cleanup-s3-target-wall-empty-v2` | 31001 | `672ce82fe95f40bb5077fa7014ab501a849dc35072a997328bd06108fb1e6385` | `6d3a0d63249ca1bf4517ccbc39c808fa4db0efee2f61e80fd14757fa18da39bc` |
| `cleanup-s3-stress-dense-v2` | 31002 | `ac786715ea3dd77f89423fb30db33d209c2529da97e7c53d140760522a517b24` | `2282b3ceaea055d8d382f7c2c20d4d915d11d54ed37c6cc0231784753e2dc924` |

Tests-first evidence added during remediation:

- Python admission red: synchronized frozen-identity drift was admitted, B allocation phases could be absent from an A/B/C campaign, valid zero allocation was rejected, and an unavailable zero-valued counter lacked a liveness rejection; all four cases now pass.
- Python calibration red: noise above the maximum validity ceiling still exposed thresholds; it now returns `HOLD_INVALID_SIGNAL` and `thresholds: null`.
- Unity timing-mode red: timing-only capture collected structural counters; timing and structural runs are now isolated.
- Unity oracle red: the reference operation list was empty; the exact four-operation removal/timer/transition fixture now passes.

The allocation ceiling vocabulary from this point is bytes per tick, not bytes per frame. Exact numerical campaign thresholds remain unfrozen until a new release-like S3-A recapture is admitted and its noise signal is valid. The next safe action is `./run_tests.sh gameplay-performance` on this remediated runtime, followed by review of both admission summaries and the calibration status. A failed or noisy recapture remains Hold and does not permit S3-B/S3-C.

Remediated recapture result:

- first remediation attempt `/mnt/d/J2M/evidence/gameplay-performance/20260827T170451Z` exposed a timing-sample serialization defect after capture-mode separation (`validCleanupProcessorSamples` and `validRunCleanupPhaseSamples` were serialized from structural totals as zero). The evidence is preserved and rejected; the serializer now uses the actual timing sample lists.
- corrected timing attempt `/mnt/d/J2M/evidence/gameplay-performance/20260827T170726Z` initially passed the then-current gameplay-performance and Cleanup admissions. Each of the six timing runs has exactly 200/200 component and complete-phase samples. All four allocation phases had 100/100 samples and p95 `0 bytes/tick`; because the full-scan implementation necessarily allocates its per-tick collections, this synchronized zero result triggered an additional counter-liveness audit rather than an allocation-success claim.
- the corrected timing attempt also produced `HOLD_INVALID_SIGNAL`: observed timing noise is `50.055418%`, above the fixed `10%` validity ceiling. `attributionMaterial` was provisionally true and capture-off was non-interfering, but invalid noise kept `thresholds` null.
- target raw p95 `(whole tick, CleanupProcessor, RunCleanupPhase, capture-off whole tick)` by repetition is `(2.69325, 0.67933, 0.682995, 2.48162)`, `(2.63452, 0.72153, 0.725135, 2.800215)`, `(1.37453, 0.672265, 0.675595, 2.792995)` milliseconds. The whole-tick outlier is retained rather than excluded.
- liveness hardening adds a warmed 4096-byte forced allocation control and requires an observed delta of at least 4096 before zero tick samples may be admitted. Final attempt `/mnt/d/J2M/evidence/gameplay-performance/20260827T171209Z` observed `allocationCounterProbeBytes: 0`, so Cleanup admission correctly rejected it with `ALLOCATION_COUNTER_PROBE_INVALID`; the aggregator preserved `HOLD_INVALID_EVIDENCE` with null thresholds.
- final evidence hashes: metrics `9002ac700ec1abacdcac39618d78522e237275f3c9cdee2297c81345b30c89bd`; Cleanup admission summary `a5ce80c5eb3c89da30a34bb1b41bb0ebba30bbf4bb2ef1953dba760725797c86`; calibration report `5c8e29bd3a197fa8014debe87af9f96d87a15d41975831890ba35e7c51160b4b`; preflight manifest `331aa7107fe8bd5f22b7015e3e8553c7f41a4ca71058094bf857c2530f7a183c`; artifact manifest `219254520552c22278f8c512a93f7479712467c6dd4a0de35b693e1640b9b245`.

Current terminal state for this remediation is still `Hold — valid evidence incomplete`. The release-like allocation API is demonstrably unavailable in this Mono Player, and the last timing-admitted attempt exceeded the noise ceiling. Do not enter S3-B/S3-C without an allocation-capable seam, a new admitted noise-valid S3-A attempt, and explicit continuation approval.

## 2026-08-28 KST — final audit hardening and Phase 0 source inventory

Status remains `Hold — valid evidence incomplete`. This amendment hardens future admission and evidence provenance and records the previously missing Phase 0 source inventory. It does not modify or reinterpret any preserved campaign artifact, enable S3-B/S3-C, or replace the allocation-capable seam requirement.

Phase 0 source scans executed from the current dirty worktree at HEAD `25e623a94890f803142990fbeb7c2e11615b2ed0`:

```bash
rg -n 'markedForDeath|hp\s*[<=>]|stateTimer|EntityPhaseState|RemoveEntity|ApplyDamage|ApplyStateChange|SpawnEntity' Assets/_Features/Gameplay -g '*.cs'
rg -n 'CreateSnapshot\(|new WorldSnapshot\(|WorldSnapshot\(' Assets/_Features/Gameplay -g '*.cs'
rg -n 'CleanupPhaseResult|RemovedEntityIds|TimerChanges|StateTransitions|RemovedUnitKinematicPoses|RemovedUnitContinuousLocomotionPoses|EventLogEntries' Assets/_Features/Gameplay -g '*.cs'
rg -n 'gameplay-performance|CleanupSlice3|allocationCounterProbeBytes|GC.GetAllocatedBytesForCurrentThread|FrameTiming|captureDiagnostics' run_tests.sh Assets/_Features/UI/UI_Composition/Runtime/GameplayPerformancePlayerProbe.cs Tools -g '*.py' -g '*.cs' -g '*.sh'
```

Candidate predicate writer inventory:

| candidate fact | authoritative writer / mutation seam | Cleanup reader | authority result |
|---|---|---|---|
| removal: `hp <= 0` or `markedForDeath` | `WorldState.ApplyDamage`, death-mark mutation, and FinalizationBatch commit adapters | `RemovalProcessor.ShouldRemove` | writes remain in `WorldState`/Finalize/committer paths |
| timer candidate: `stateTimer > 0` | `WorldState.ApplyStateChange`; movement/attack FinalizationBatch adapters; Cleanup `StateTimerProcessor` commit | `StateTimerProcessor`, structural count fused into `RemovalProcessor` scan | same-tick spawn은 candidate에서 제외되는 것이 아니라 decrement execution만 건너뛴다; Cleanup mutation still uses `ICleanupCommitContext` |
| immediate transition: timer `<= 0` with `Acting` or `Cooldown` | same state-change paths plus Cleanup timer decrement | `StateTransitionProcessor`, structural count fused into `RemovalProcessor` scan | Removal → Timer → Transition order remains unchanged |
| membership creation/removal/reconstruction inputs | `WorldState.SpawnEntity` / `RemoveEntity`, respawn processors, attack spawn commit, `WorldState.CreateFromSnapshotFast(WorldSnapshot)` | current production Cleanup does not maintain an index | S3-A remains full scan; B/C counters stay zero; S3-B requires fast/slow import candidate-carrier parity before production maintenance implementation |

`WorldSnapshot` creation seam inventory:

| seam | role | policy |
|---|---|---|
| `SnapshotBuilder.Create(WorldState)` → `WorldState.CreateSnapshot` → `WorldSnapshot.CreateWithSnapshotOwnedCellIndexes` | canonical production materialization from authoritative state | immutable read/query seam |
| `GameplayCompositionRoot.CreateSnapshot(WorldState)` | public composition wrapper | delegates to `WorldState.CreateSnapshot` |
| `ProjectedWorld.CreateSnapshot(reason)` | TickPipeline plan/resolve projected read seam | reason-tagged projected snapshots; not an authoritative writer |
| `WorldState.CreateFromSnapshotFast(WorldSnapshot)` | projected fast-import mutable-state reconstruction seam | authoritative entity values와 derived indexes를 snapshot에서 복구하며 S3-B candidate carrier parity 대상 |
| tests using `WorldState.CreateSnapshot` | preferred test seam | exercises production materialization |
| direct `new WorldSnapshot(...)` in focused test helpers | legacy/raw test-only construction | remains test-only and is covered by snapshot/presentation fixtures; no new production constructor path was added |

Cleanup result and auxiliary/presentation consumer inventory:

| carrier | consumer path | preserved contract |
|---|---|---|
| `CleanupPhaseResult.RemovedEntityIds`, timer/state events | `TickPipeline` auxiliary expiry/respawn composition, `StageObjectiveTickFacts`, and `TickResultBuilder` event/presentation aggregation | exact order and IDs |
| `EventLogEntries` | `TickResultBuilder` → `TickResultData.EventLog` | Cleanup event ordering before respawn events |
| final authoritative entities | final `WorldState.CreateSnapshot` → `TickResultBuilder` owned `FinalEntities` | read-only result ownership and determinism input |
| removed kinematic/continuous poses | `TickResultBuilder` presentation-track planners | presentation consumes terminal pose carriers without authoritative writes |
| Cleanup result plus final snapshot | determinism hash, FullCanonical trace, replay fixtures | exact parity remains required |

Wall vocabulary note: runtime `EntityType`에는 별도 `Wall` 값이 없다. Authored Wall은 현재 runtime `EntityType.None` entity로 materialize될 수 있지만 모든 `EntityType.None`을 immutable authored Wall과 동의어로 취급하지 않는다. S3-B predicate fixture는 authored Wall provenance와 generic `EntityType.None` participation을 분리해 검증한다.

Runner/probe/admission limitation inventory and guards:

| limitation | fail-closed guard | focused evidence |
|---|---|---|
| Mono Player may return a synchronized zero thread-allocation delta | warmed forced-allocation probe must observe at least 4096 bytes | zero probe rejects with `ALLOCATION_COUNTER_PROBE_INVALID` |
| FrameTiming/component timing can be noisy or capture-off can interfere | maximum calibration noise 10%, capture-off ceiling, thresholds null on invalid signal | calibration tests cover READY versus `HOLD_INVALID_SIGNAL` |
| malformed or statistically impossible metric JSON | CLI preserves `REJECTED`/`HOLD_INVALID_EVIDENCE`; metric summaries require `median <= p95 <= p99 <= maximum` | admission/calibration Python adversarial fixtures |
| result/tool/contract drift can detach a verdict from its inputs | result provenance records metrics/tool/contract SHA-256; final evidence manifest binds both verdict files and exit statuses before runner failure return | evidence-manifest Python fixtures |
| unavailable strategies could fall back to A | requested strategy matrix and allocation phase identity are exact | A-only and future A/B/C pass/reject fixtures |

Implementation hardening in this amendment:

- synthetic stress operations are constructed once and the same immutable operation array drives execution and canonical schedule serialization; the frozen v2 target/stress hashes remain unchanged;
- admission rejects non-finite, negative, wrong-count, and non-monotonic timing/allocation summaries;
- admission and calibration CLIs preserve fail-closed machine-readable artifacts on malformed JSON and include metrics, validator/aggregator, workload-contract, and active-strategy provenance;
- `run_tests.sh gameplay-performance` creates `cleanup-s3a-evidence-manifest.json` after all three gates and before returning failure, so rejected captures retain hashes for metrics, runtime log, preflight/captured manifests, both Cleanup results, tools, contract, and gate statuses;
- the unused legacy `CaptureRun` path remains a separately tracked low-risk maintenance item and was not mixed into this contract fix.

Current-source validation after this amendment:

- Python Cleanup admission/calibration/evidence-manifest plus existing gameplay-performance admission/campaign regression: 38/38 passed;
- `./run_tests.sh full --filter CleanupSlice3AttributionSimulationTests`: EditMode 8/8 passed; matching PlayMode 0;
- `./run_tests.sh core`: EditMode 228/228 passed; PlayMode 111 total, 107 passed, 4 skipped, 0 failed;
- `./run_tests.sh --integration-replay --filter TickReplayDeterminismTests`: EditMode 59/59 passed;
- `./run_tests.sh ui`: EditMode 1340/1340 passed;
- `python3 -m py_compile` for the three Cleanup tools and their tests, `bash -n run_tests.sh`, and `git diff --check`: passed;
- broad unfiltered `./run_tests.sh full` and a new release-like gameplay-performance capture were not run. A new capture remains deferred until an allocation-capable seam is available and must use a new campaign identity because validator/aggregator/manifest tool hashes changed.

## 2026-08-28 KST — Normative amendment: S3-A evidence contract v3

Approval and overrides: the user authorized this amendment after an independent Goal/evidence audit. It replaces the former §2 noise-to-defer wording and the former §8 allocation-missing provisional S3-B wording. Dated progress blocks remain historical evidence and do not override this amendment.

Current package/status: S3-A measurement-contract remediation / `Hold — valid evidence incomplete`. S3-B/S3-C remain forbidden until an allocation-capable or pre-approved equivalent signal produces a new admitted, noise-valid S3-A result and the user grants continuation.

Contract classification:

- gameplay StrongContract and the production full-scan executor are unchanged;
- evidence schema, admission exactness, manifest consistency, and document precedence are harness CurrentPolicy hardened by this amendment.

Implemented contract changes:

- frozen exact JSON integer, workload cardinality, unique run-key, and cross-strategy run-key equality rules in `Tools/contracts/gameplay_cleanup_slice3_evidence_contract_v3.md`;
- admission now rejects non-object roots, numerically equal floats in integer cardinality fields, duplicate workload/run keys, and strategy-only repetitions;
- evidence manifest schema v2 cross-checks admission/calibration verdicts, CLI statuses, metrics/runtime artifact hashes, active strategy, and validator/aggregator/workload-contract provenance;
- inconsistent or missing evidence writes a machine-readable `HOLD_INVALID_EVIDENCE` manifest and cannot report `PASS`;
- Phase 0 inventory now records `RemovalProcessor.ShouldRemove`, distinguishes timer candidate membership from same-tick decrement execution, includes `WorldState.CreateFromSnapshotFast` and objective/presentation consumers, and clarifies authored Wall versus generic `EntityType.None` vocabulary.

Tests-first red evidence: the immediately preceding independent audit reproduced caller-status false `PASS`, post-verdict metrics tamper `PASS`, duplicate workload admission, strategy-only extra repetition admission, float cardinality admission, and non-object-root crashes. No filesystem failure log was created before this amendment, so those reproductions are not claimed as a retained formal red artifact. This amendment makes command/test/assertion/exit/timestamp/source-hash/log-path recording mandatory for subsequent red evidence.

Current-source validation after this amendment:

- gameplay Python admission/calibration/manifest plus existing performance admission/campaign regression: 48/48 passed;
- `./run_tests.sh full --filter CleanupSlice3AttributionSimulationTests`: EditMode 8/8 passed; matching PlayMode 0;
- `./run_tests.sh core`: EditMode 228/228 passed; PlayMode 111 total, 107 passed, 4 skipped, 0 failed;
- `python3 -m py_compile` for the three Cleanup tools and their focused tests, `bash -n run_tests.sh`, trailing-whitespace scan, and `git diff --check`: passed.

Tests not run: replay, UI, broad unfiltered full, and a new gameplay-performance capture. This amendment changes no runtime gameplay/presentation semantics; a new performance capture remains invalid until an allocation-capable or approved equivalent signal exists and must use a new campaign identity because tool hashes changed.

## 2026-08-28 KST — Normative implementation-status erratum: post-amendment evidence audit

Current audit truth: [Gameplay Wall Tick Cost Optimization — Slice 3 Evidence Post-Amendment Audit](./Gameplay-Wall-Tick-Cost-Optimization-Slice3-Evidence-Post-Amendment-Audit-2026-08-28.md).

Bounded remediation execution prompt: [Gameplay Wall Tick Cost Optimization — Slice 3 Evidence Remediation Goal Prompt](./Gameplay-Wall-Tick-Cost-Optimization-Slice3-Evidence-Remediation-Goal-Prompt.md). This remediation Goal stops after v4 contract/tool/test closure and independent re-audit; it does not include a new official S3-A capture or any S3-B/S3-C production implementation.

Approval and changed targets: the user approved supplementing the audit on 2026-08-28 KST. This erratum changes only the current implementation-status claim and hard-pause/entry conditions created by the preceding v3 amendment. It does not change gameplay StrongContract, authorize B/C, or silently amend evidence artifact schemas. Any schema/domain/version change identified by the audit requires a separate explicit evidence-contract amendment and matching tests.

The independent audit preserves the v3 requirement and the current `Hold — valid evidence incomplete`, but found that its implementation is not yet complete. Revision/runtime-tree identity mismatch, stale copied worktree hash, caller-only performance status, and internally contradictory verdict payload can still produce false `PASS / VERIFIED`. Cardinality/type/domain, strategy identity, duplicate serialization key, `DEFERRED` transport, malformed/early-failure artifact lifecycle, and input/output alias guards also remain open. Therefore the preceding amendment's “manifest consistency hardened” and “cannot report PASS” wording records implementation intent, not a completed closure claim, and is superseded by this erratum for current implementation status.

Stage-specific hard pause:

- S3-A remediation may implement the audit's P0/P1 contract/tool/negative-test corrections. A new official capture additionally requires an allocation-capable or separately approved equivalent signal and a new campaign identity.
- S3-B pre-entry source audit and tests-first failing fast/slow import candidate-parity fixture are allowed remediation. Candidate maintenance production implementation remains forbidden until P0/P1 are closed with retained tests-first evidence, a new admitted·noise-valid S3-A `PASS` exists, `WorldState.CreateFromSnapshotFast` is included in the writer inventory/parity precondition, workload/Wall identity is truthful, and the user grants continuation.
- S3-C remains forbidden until the existing S3-B pre-C correctness, structural, snapshot/fast-import, maintenance-tax, and user-continuation gates are closed.

Gameplay StrongContract remains unchanged. The full-scan executor is the current runtime `CurrentPolicy` and remains selected while this Hold is active; it is not reclassified as an immutable StrongContract.

## 2026-08-28 KST — Normative amendment: Evidence Contract v4 approval

Approval and changed targets: the user explicitly approved `Tools/contracts/gameplay_cleanup_slice3_evidence_contract_v4.md` together with its §15 minimal metrics-producer allowlist amendment. v4 supersedes v3 for new remediation artifact schema, identity/cohort binding, canonical verdict recomputation, lifecycle, alias/atomic-write, and terminal-transport requirements. v3 remains historical provenance and no existing artifact is automatically upgraded.

The approved probe scope is limited to attempt/runtime/build/harness identity echo and metrics schema-2 serialization in `GameplayPerformancePlayerProbe.cs`. It does not authorize gameplay semantic changes, Cleanup executor changes, a new official capture, S3-B/S3-C production work, commit, or push. Remediation proceeds tests-first with retained red/green evidence and independent re-audit.

## 2026-08-28 KST — Normative implementation-status closure: Evidence Contract v4 remediation

The approved P0/P1 evidence remediation is complete. Live pre/post source and runtime identity, actual artifact/build/tool/harness hashes, attempt/cohort binding, persisted verdict recomputation, strict schema/parsing/domain/cardinality, provisional/final lifecycle, safe atomic output, and exact terminal transport are implemented under Evidence Contract v4. Formal red, green, and independent re-audit evidence is preserved under `/mnt/d/J2M/evidence/20260827T204003Z-cleanup-s3-v4-remediation/`; the unavailable historical red evidence in S3-EV-008 remains a permanent deviation and was not reconstructed.

Validation on that remediation snapshot passed the Python v4 plus runner suite `80/80`, focused Cleanup Slice 3 EditMode `8 passed / 0 failed` with matching PlayMode `0`, core EditMode `228 passed / 0 failed` and PlayMode `111 total / 107 passed / 4 skipped / 0 failed`, UI Windows build and EditMode `1340 passed / 0 failed`, plus Python compilation, shell syntax, and diff checks. A new gameplay-performance capture, replay, and broad unfiltered full were not run. No commit or push was performed.

This closes only the audit's evidence-harness P0/P1 findings. Repository status remains `Hold — valid evidence incomplete`; the full-scan Cleanup executor remains selected, and a new official S3-A capture requires an allocation-capable or explicitly approved equivalent signal, a new campaign identity, and a separate Measurement Goal. S3-B/S3-C production entry remains forbidden.

## 2026-08-28 KST — Normative current-state erratum: S3-EV-016 exact full-scan oracle gate

Authority and changed targets: this erratum reflects Evidence Contract v4 §7.5 and the post-amendment audit's S3-EV-016 in the current S3-A entry/status wording. It supersedes earlier wording that could be read as allowing official capture from an allocation signal, campaign identity, or separate Measurement Goal alone. It changes neither gameplay StrongContract nor the production full-scan runtime CurrentPolicy or frozen workload/schema bytes, and it does not approve an oracle amendment or a new official capture.

Current entry rule: S3-A official capture and `PASS / DEFERRED` transport remain forbidden until a separate explicit contract/workload amendment approves and verifies the independent exact full-scan visit oracle owner, formula, frozen artifact bytes, approved digest, exact parser/comparison, capture-identity binding, same-revision implementation, and tests-first negative fixtures. An otherwise-ready `READY` or `DEFERRED_NOT_MATERIAL` result is terminal `HOLD / HOLD_INVALID_EVIDENCE` with `FULL_SCAN_EXPECTATION_UNAPPROVED` until that amendment is complete.

After that oracle amendment is separately approved, implemented, and verified, official capture additionally requires an allocation-capable or pre-approved equivalent signal, a new campaign identity, clean/same-revision identity, and separate S3-A Measurement Goal approval. These are cumulative conditions, not alternatives. Until then the repository Slice 3 state is `Hold — valid evidence incomplete`, and S3-B/S3-C production entry remains forbidden.

Historical validation clarification: the preceding closure block's `80/80` and Unity results are historical touched-scope evidence from the initial remediation snapshot preserved under `/mnt/d/J2M/evidence/20260827T204003Z-cleanup-s3-v4-remediation/`. They are not current-source whole-suite validation after the S3-EV-016 hard block. The carrier's actual core PlayMode result is `111 total / 107 passed / 4 skipped / 0 failed`.
