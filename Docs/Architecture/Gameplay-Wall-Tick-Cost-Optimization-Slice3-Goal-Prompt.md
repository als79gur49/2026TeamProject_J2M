# Gameplay Wall Tick Cost Optimization — Slice 3 Goal Prompt

- 문서 역할: Slice 3 실행 순서·hard pause·evidence·terminal-state 계약
- 현재 실행 상태: `Hold — valid evidence incomplete`
- S3-B·S3-C: P0/P1 evidence remediation, valid S3-A `PASS`, stage별 pre-entry 조건과 사용자 continuation 승인 전까지 금지
- 최초 승인일: 2026-08-27
- authoritative Goal: [Master Plan Slice 3 Goal](./Gameplay-Wall-Tick-Cost-Optimization-Slice3-Goal-Plan.md)
- historical 구현 감사 register: [Slice 3 Evidence Post-Amendment Audit](./Gameplay-Wall-Tick-Cost-Optimization-Slice3-Evidence-Post-Amendment-Audit-2026-08-28.md)
- completed historical remediation 실행 문서: [Slice 3 Evidence Remediation Goal Prompt](./Gameplay-Wall-Tick-Cost-Optimization-Slice3-Evidence-Remediation-Goal-Prompt.md)
- current proposal source: [Slice 3 S3-A Reaudit Remediation Plan](./Gameplay-Wall-Tick-Cost-Optimization-Slice3-S3A-Reaudit-Remediation-Plan.md)
- current docs-only workflow: [Slice 3 S3-A F1/B0 Goal Prompt](./Gameplay-Wall-Tick-Cost-Optimization-Slice3-S3A-F1-B0-Goal-Prompt.md)
- 상위 계획: [Gameplay Wall Tick Cost Optimization Plan](./Gameplay-Wall-Tick-Cost-Optimization-Plan.md)

## 실행 지시

Gameplay Wall Tick Cost Optimization Slice 3를 tests-first로 수행한다. 이 Prompt는 하나의 무중단 구현 지시가 아니다. S3-A, S3-B pre-C, S3-C/final campaign 사이의 hard gate를 지키고, 각 gate가 닫히기 전에는 다음 package로 진행하지 않는다.

사용자가 Goal 생성을 명시한 경우에만 Goal을 생성한다. 명시적 token budget이 없으면 budget을 설정하지 않는다. 실행 Goal은 outcome-neutral한 세 개로 나누는 것을 기본으로 한다: Goal A는 S3-A 귀속·calibration 판정, Goal B는 S3-B pre-C 판정, Goal C는 S3-C·final campaign·retain/reject closeout이다. 한 Goal로 실행하더라도 각 hard gate에서 결과를 보고하고 사용자 continuation 승인을 받기 전에는 다음 package를 시작하지 않는다.

Current precursor state: 최초 post-amendment evidence P0/P1 remediation은 전용 Evidence Remediation Goal R과 승인된 Evidence Contract v4에 따라 historical closeout으로 보존되지만, repository Slice 3는 S3-EV-016 때문에 `Hold — valid evidence incomplete`다. 별도의 명시적 contract/workload amendment가 independent exact full-scan visit oracle의 owner·formula·frozen bytes·approved digest·parser/comparison·identity binding·negative tests를 승인하고 same-revision 구현·검증이 완료되어야 한다. 그 뒤 allocation-capable 또는 승인된 equivalent signal, 새 campaign identity, 별도 S3-A Measurement Goal 승인이 모두 준비된 경우에만 official Measurement Goal A를 시작한다.

Repository의 `Goal complete/closed/rejected/deferred/Hold/blocked` 상태와 실행 도구의 Goal 상태를 구분한다. Outcome-neutral 실행 objective가 reject/defer 판정까지 완료되면 실행 Goal은 complete로 닫을 수 있지만 repository Slice 3를 `Goal complete — indexed package retained`로 부르지 않는다. 사용자 continuation 대기는 blocked가 아니며, 실행 Goal의 blocked는 같은 infrastructure/authority blocker가 최소 세 번 연속 반복되어 더 진행할 수 없을 때만 사용한다.

안전한 다음 작업이 같은 package 안에 남아 있는 동안 계속 진행한다. 필요한 권한 부족, 반복 재현되는 infrastructure blocker, 해결할 수 없는 StrongContract 충돌, 또는 이 Prompt의 hard gate에서만 중단한다.

## Contract authority and precedence

1. `Tick-Simulation-Canonical-Spec.md`, repository `AGENTS.md`, architecture guardrail은 gameplay StrongContract의 최상위 근거다.
2. Slice 3 Goal Plan §1–§14는 semantic contract, package scope, entry/close acceptance를 정의한다.
3. 이 Goal Prompt는 실행 순서, hard pause, evidence validity, repository terminal state를 정의한다.
4. Goal Plan의 명시적으로 승인된 `Normative amendment`와 `Normative implementation-status erratum`은 적힌 변경 대상과 hard-pause 범위에서 권위를 가진다. 연결된 audit 문서는 evidence register이며 독립적으로 gameplay semantic contract를 바꾸지 않는다.
5. 그 밖의 dated progress block은 실행 기록이며 기존 normative contract를 자동으로 변경하지 않는다.
6. 기존 계약을 변경하는 amendment/erratum은 유형, 변경 대상, 대체 문구, 승인일을 명시해야 한다.
7. entry permission과 hard pause가 충돌하면 더 엄격한 hard pause를 적용하고 `Hold`에서 중단한다.
8. 해소되지 않은 문서 충돌은 구현 권한으로 해석하지 않는다.

## 시작 전 필수 읽기

1. `Docs/Architecture/Gameplay-Wall-Tick-Cost-Optimization-Slice3-Evidence-Remediation-Goal-Prompt.md`
2. `Docs/Architecture/Gameplay-Wall-Tick-Cost-Optimization-Slice3-Evidence-Post-Amendment-Audit-2026-08-28.md`
3. `Docs/Architecture/Gameplay-Wall-Tick-Cost-Optimization-Slice3-Goal-Plan.md`
4. `Docs/Architecture/Gameplay-Wall-Tick-Cost-Optimization-Plan.md`
5. `Docs/Architecture/Tick-Simulation-Canonical-Spec.md`
6. `Docs/Architecture/README.md`
7. `Docs/Testing/Gameplay-Test-Automation-Guide.md`
8. `AI_GIT_COMMIT_RULES.md`
9. `AGENTS.md`

`gameplay-contract-hardening`을 적용한다. 시작 시 다음을 실행한다.

```bash
git status --short --branch
git diff --stat
./run_tests.sh --print-config
```

현재 dirty/untracked 파일을 inventory하고 사용자 소유 변경을 stage, revert, overwrite하지 않는다. 이 Prompt와 Goal Plan의 기존 변경도 실행 범위로 명시되지 않았다면 사용자 소유로 취급한다.

새 J2M worktree가 필요하면 `j2m-worktree-add`만 사용한다. 생성 전 D free space 30 GiB 이상을 확인하고 `j2m-worktree-audit`을 실행한다. Unity worktree별 `Library`는 공유하지 않는다. 새 evidence와 build는 각각 `/mnt/d/J2M/evidence`, `/mnt/d/J2M/builds` 아래에 둔다.

Commit/push는 사용자가 별도로 승인한 범위에서만 수행한다. Rollback에 `git reset --hard`, broad checkout, 사용자 변경 삭제를 사용하지 않는다.

## Execution-start baseline — historical

이 절은 2026-08-27 실행 시작 전 상태를 보존한 historical baseline이다. 현재 상태나 현재 구현 여부의 판단에는 사용하지 않는다. 현재 상태는 이 Prompt 상단과 Goal Plan의 가장 최근 normative status block을 따른다.

- Slice 1 `FinalEntities` sharing과 Slice 2 Factory prefilter: recovery closeout 기준 complete;
- Slice 3 production candidate: 미구현;
- current Cleanup executor: ordered full scan + survivor copy;
- current Slice 3 attribution/candidate/strategy diagnostics: 없음;
- current performance workload: `stage-1-1` neutral tick만 지원;
- deterministic candidate-dense/mutation-heavy workload: 없음;
- current admission/campaign tooling: Slice 1 phase/state shape에 고정;
- valid Cleanup component p95/allocation signal: 없음;
- S3-A: Prompt 승인 뒤 진입 가능;
- S3-B/C: S3-A와 각 hard gate 전까지 금지.

실행 시작 revision, branch, runtime-tree hash, harness hash는 실제 시작 시 다시 기록한다. 이 문서의 작성 revision을 실행 revision으로 가정하지 않는다.

## StrongContract와 금지 사항

Goal Plan §3의 StrongContract와 §3.3 lifetime matrix가 authoritative하다. 특히 다음을 바꾸지 않는다.

- `WorldState` authority와 immutable `WorldSnapshot` read seam;
- `CleanupProcessor.Process`와 box-lock → aura-field → pending-reaction direct-write entrypoint 순서 및 단일 Cleanup write context;
- indexed implementation은 `CleanupProcessor.Process` 내부에만 두고 새 production direct-write entrypoint를 추가하지 않음;
- Removal → Timer → Transition과 removed-ID 후속 제외;
- same-tick spawn timer/transition/removal 규칙;
- Timer event family 전체가 Transition보다 먼저이고 family 내부 ID 순서 유지;
- timer=1의 post-timer same-Cleanup transition;
- Wall/`EntityType.None` generic participation;
- `CleanupPhaseResult` 모든 field와 removal pose carrier/presentation track;
- occupancy, auxiliary lifetime, EventLog, FinalEntities, hash, FullCanonical trace, replay parity;
- base candidate empty여도 box-lock, aura-field, pending-reaction expiry 실행.

금지 사항:

- Wall 또는 `EntityType.None`을 authoritative world/occupancy/hash/replay에서 제거;
- candidate density 기반 자동 production fallback 또는 hybrid executor 추가;
- Cleanup write context에 authoritative read API 추가;
- Cleanup direct-write entrypoint나 상대 호출 순서 변경;
- timer 처리 뒤 중간 authoritative snapshot 생성;
- derived index를 canonical hash/trace에 추가;
- admitted run을 결과 확인 후 제외·warm-up 재분류;
- historical Slice 1 evidence를 Slice 3 acceptance로 합산;
- `project-wide green`, `full regression closed`, 근거 없는 whole-tick speedup 주장.

## Tests-first 공통 규칙

각 package는 최소 scaffold/no-op seam으로 새 test를 컴파일 가능하게 만든 뒤 기존 동작 때문에 실패하는 assertion red를 먼저 확인한다.

- missing-symbol compile error는 tests-first red evidence가 아니다.
- aggregate matching test `0`은 실패다.
- strategy counter mismatch, hidden fallback, invalid allocation signal은 acceptance evidence가 아니다.
- red revision을 retain/commit하지 않는다.
- red source revision은 commit하거나 retain하지 않되 test command, test name, 실패 assertion, exit code, timestamp, source/worktree hash와 failure log 경로를 progress block에 기록한다.
- pure predicate/index는 Core, runtime/pipeline parity는 Integration, reflection/wiring은 Infrastructure에 둔다.

Package별 tests-first 최소 범위:

- S3-A: independent oracle, diagnostics capture-off 비간섭, workload/admission schema;
- S3-B: mutation sequence, all-state predicate matrix, snapshot immutability, raw constructor policy, fast import, candidate/reference parity;
- S3-C: removal overlap, post-timer state, event family/ID ordering, auxiliary/removal-pose/presentation parity, zero-candidate path, strategy counter/fallback 0, final ordinary non-capture indexed wiring.

## Phase 0 — Execution preflight and harness plan

1. candidate predicate writer inventory를 source scan으로 다시 고정한다.
2. `WorldSnapshot` production/test creation seam inventory를 고정한다.
3. current Cleanup, `CleanupPhaseResult`, auxiliary lifetime, presentation carrier 소비 경로를 기록한다.
4. current performance runner/probe/admission/campaign limitations을 focused test로 고정한다.
5. Slice 3 workload/strategy/metrics schema를 tests-first로 설계한다.

새 S3-A remediation evidence schema와 trust boundary는 승인된 [`Tools/contracts/gameplay_cleanup_slice3_evidence_contract_v4.md`](../../Tools/contracts/gameplay_cleanup_slice3_evidence_contract_v4.md)를 따른다. v3는 historical predecessor다. Persisted JSON verdict와 canonical recomputation이 권위이며 CLI status는 coherence/transport다. Evidence manifest는 caller status만으로 `PASS`를 만들 수 없고 verdict, provenance, actual artifact hash, live pre/post tree와 attempt identity의 cross-file consistency를 검증해야 한다.

S3-A 범위에 다음 harness capability를 포함한다.

- target candidate-empty/Wall-heavy workload;
- deterministic stress candidate-dense/mutation-heavy workload;
- workload ID, seed, schedule hash, entity/Wall/candidate/mutation counts;
- capture-start one-time strategy vocabulary/schema와 selector framework; S3-A runtime에서는 A만 executable이고 B/C 요청은 unavailable로 fail closed;
- strategy invocation과 maintenance counter;
- CleanupProcessor와 complete RunCleanupPhase per-tick timing;
- valid main-thread allocation delta/tick 또는 사전 승인된 동등 signal;
- workload/strategy-aware admission validator와 aggregator;
- 기존 Slice 1 neutral admission regression coverage.

S3-A는 아래 전체 A/B/C 규칙을 synthetic admitted/rejected artifact fixture로 검증한다. 실제 runtime evidence는 구현된 strategy만 단계별로 활성화하며, 아직 구현되지 않은 strategy를 A로 fallback해 통과시키지 않는다.

공통 admission validator는 strategy가 실제 활성화된 단계에서 다음 불일치를 machine-readable reject로 처리한다.

- target의 removal/timer/immediate candidate count가 모든 measured tick에서 0이 아님;
- stress candidate/mutation schedule fingerprint가 frozen plan과 다름;
- 같은 workload에서 해당 stage에 실제 활성화된 strategy들의 entity, Wall, mutation, executed tick count가 다름;
- A가 활성화됐을 때 maintenance/carriage count가 0이 아니거나, B/C가 모두 활성화됐을 때 maintenance/carrier count가 다름;
- 해당 stage에서 활성화된 A/B full-scan invocation 또는 C indexed invocation이 executed tick count와 다름;
- initial-world fingerprint 불일치, invariant mismatch, hidden fallback이 하나라도 존재.

Exact schema rule:

- 모든 JSON root는 object여야 한다.
- count, tick, repetition, seed, entity/Wall/mutation count는 bool이 아닌 JSON integer여야 한다.
- workload ID와 `(strategy, workloadId, repetition)` key는 중복될 수 없다.
- 활성 strategy들은 exact workload/repetition key 집합과 순서가 같아야 하며 누락·추가·중복은 reject한다.
- exit status, JSON verdict/status, metrics/tool/contract provenance가 불일치하면 `HOLD_INVALID_EVIDENCE`다.

실제 evidence 활성화 순서는 다음과 같다.

- S3-A: target/stress A-only capture, A maintenance/carriage 0, full-scan invocation == executed tick count, indexed invocation/fallback 0;
- S3-B: 실제 A/B capture, workload fingerprint 동일, A maintenance 0, A/B full-scan invocation == executed tick count, indexed invocation 0;
- S3-C/final: 실제 A/B/C capture, workload fingerprint 동일, B/C maintenance/carrier count 동일, A/B full-scan 및 C indexed invocation == executed tick count.

권장 campaign artifact policy는 build-once/run-many다. 같은 Player artifact를 6 warm-up과 18 official attempt에 재사용하고, 각 attempt는 task-owned 독립 persistent/temp/output 영역과 동일한 initial-world fingerprint를 가져야 한다. Harness가 이를 지원하지 못하면 첫 official run 전에 multi-build amendment를 승인받고 runtime-tree/harness hash 동일성, attempt별 artifact hash, persistent-state 격리를 admission에 포함한다.

## Phase S3-A — Attribution, oracle, calibration

Production Cleanup executor를 바꾸지 않고 Goal Plan §8 S3-A counter, timing, allocation, independent oracle을 구현한다.

필수 결과:

- oracle과 current full-scan `CleanupPhaseResult` 모든 field exact parity;
- removal pose EventLog와 final kinematic/continuous presentation track parity;
- target/stress workload deterministic reproduction;
- valid component sample count == executed tick count;
- allocation signal valid;
- diagnostics/reference capture-off allocation 0과 whole-tick 비악화;
- strategy/maintenance/fallback counters가 실제 A-only 상태를 정확히 증명;
- B/C selection request가 unavailable로 실패하고 A로 fallback하지 않음;
- synthetic artifact fixture가 future A/B/C admission schema의 pass/reject matrix를 검증.

Official이 아닌 calibration만 수행한다. Calibration으로 다음 exact 값을 Goal Plan의 dated amendment/progress block에 기록한다.

- target `CleanupProcessor` component material threshold;
- complete `RunCleanupPhase` containment threshold와 합격 방식;
- target whole-tick A→C end-to-end benefit threshold;
- noise 판정식;
- B maintenance/allocation tax ceiling;
- target allocation safety ceiling과 stress whole-tick/allocation safety ceiling;
- warm-up/sample/retry/aggregation/admission 규칙;
- immutable campaign plan/validator/aggregator hash 생성 절차.

### S3-A hard gate

다음 중 하나로 판정하고 중단한다.

- `S3-A ready / proceed requested`: attribution이 material하고 모든 measurement/oracle gate가 유효;
- `Goal deferred — Cleanup attribution not material`: B/C 미진입;
- `Hold`: approved independent exact full-scan oracle/frozen amendment 또는 allocation/component/workload/admission signal 미완성;
- `Goal blocked`: 반복 infrastructure/authority blocker.

S3-A preliminary evidence는 production retain evidence가 아니다. Goal amendment와 사용자 continuation 승인 전에는 S3-B를 시작하지 않는다.

Current post-audit hard pause: 최초 audit P0/P1 remediation closeout은 historical record로 보존하지만, approved independent exact full-scan oracle/frozen contract-workload amendment와 그 same-revision 구현·negative tests, 새 admitted·noise-valid S3-A `PASS`, fast-import writer inventory와 tests-first fast/slow import candidate-parity precondition, truthful workload/Wall identity, 사용자 continuation이 모두 완료되기 전에는 S3-B candidate maintenance production 구현을 시작하지 않는다. Writer inventory/source audit과 failing parity test 작성은 pre-entry remediation으로 허용하지만, oracle amendment나 official capture 권한으로 해석하지 않는다.

## Phase S3-B — Maintenance/carriage pre-C gate

Goal Plan §4–§5에 따라 candidate maintenance와 immutable snapshot carrier를 구현하되 production Cleanup executor는 full scan을 유지한다.

필수 결과:

- all-state/unknown-cast predicate matrix parity;
- 모든 authoritative mutation seam parity;
- entity-owned lifetime matrix와 same-ID respawn parity;
- production/test snapshot creation parity와 silent-empty carrier 0;
- fast/slow import parity와 별도 candidate-predicate O(N) scan 0;
- old snapshot carrier immutability;
- irrelevant update membership mutation 0;
- preliminary A/B whole-tick과 allocation tax가 사전 고정 ceiling 이내;
- 실제 A/B workload fingerprint가 동일하고 A maintenance 0, A/B full-scan invocation == executed tick count, indexed invocation 0;
- mismatch/fallback 0.

### S3-B hard gate

- 실패: B package를 사용자 변경을 보존해 제거/재설계하고 C에 진입하지 않는다.
- 통과: B는 provisional이며 final retained가 아니다. 결과와 revision을 기록하고 사용자 continuation 승인 전 C를 시작하지 않는다.

## Phase S3-C — Indexed executor

S3-B와 같은 representation/maintenance를 사용하고 C strategy의 indexed Cleanup executor와 base empty fast path를 구현한다.

- removed ID를 모든 후속 timer/transition에서 제외한다.
- timer=1의 post-timer local state를 transition carrier로 전달한다.
- 모든 Timer operation을 모든 Transition operation보다 먼저 commit한다.
- base candidate empty path는 auxiliary expiry routine을 건너뛰지 않는다.
- S3-C pre-campaign gate와 사용자 continuation 승인 전까지 ordinary production composition은 full-scan executor를 유지한다.

Goal Plan §9 전체와 independent oracle parity를 통과한다. 이 단계의 focused/structural/semantic failure는 B/C production package rollback 또는 재설계 대상이다.

실제 A/B/C workload fingerprint가 동일하고, B/C maintenance/carrier count가 일치하며, A/B full-scan 및 C indexed invocation이 executed tick count와 일치해야 한다.

## S3-C pre-campaign validation

Ordinary production path가 아직 full scan인 S3-C provisional revision에서 focused fixture 목록을 고정하고 C strategy의 semantic/structural/replay parity를 검증한다.

```bash
./run_tests.sh full --filter 'CleanupPhaseScenarioTests;SnapshotBudgetGuardTests;ProjectedWorldFastImportCoreTests;WorldSnapshotAndPresentationTests'
./run_tests.sh --integration-replay --filter TickReplayDeterminismTests
./run_tests.sh core
```

각 lane의 exact total/pass/fail/skip과 matching PlayMode 수를 기록한다. Broad unfiltered `full`을 실행하지 않으면 이유를 기록하고 touched-cluster evidence와 분리한다.

### S3-C pre-campaign hard gate

- 실패: campaign에 진입하지 않는다. 사용자 변경을 보존해 B/C production package를 rollback하거나 재설계하고 결과를 보고한다.
- 통과: exact validation counts, strategy/fallback counters, revision을 보고하고 final campaign continuation과 clean final candidate revision을 만들기 위한 별도 commit 승인을 함께 요청한다. Push 승인은 별도다.

사용자 continuation 및 commit 승인 뒤 ordinary non-capture production composition이 selector 없이 indexed executor를 직접 선택하게 한다. Infrastructure wiring guard와 Integration runtime-composition test가 이 non-capture path를 직접 증명해야 한다. 승인된 intent commit으로 clean final candidate revision을 고정하고 위 focused/core/replay 명령을 같은 revision에서 다시 실행한다. 이 재검증이 통과하기 전에는 official campaign을 시작하지 않는다.

## Final campaign freeze

B/C 코드와 A/B/C capture strategy가 모두 존재하고 ordinary production composition이 indexed executor를 직접 선택하는 하나의 final candidate revision을 고정한다. A/B/C selector는 capture에서만 override한다. 같은 revision의 재검증이 통과한 뒤 S3-A에서 승인된 artifact policy를 적용하며, 기본값은 동일 Player artifact의 build-once/run-many다.

첫 official run 전에 다음을 immutable campaign plan으로 저장하고 SHA-256을 기록한다.

- revision/runtime-tree/artifact/harness hashes;
- workload IDs, seeds, schedule hashes;
- strategy, warm-up, official slot 순서;
- resolution, quality, backend, trace mode, tick/sample counts;
- admission, retry, aggregation, noise, timing/allocation threshold;
- validator와 aggregator hashes.

Official admission은 manifest를 단순 hash inventory로 신뢰하지 않는다. Actual artifact hash와 각 verdict provenance, revision, strategy, workload, validator, aggregator, contract hash를 교차 검증하고 consistency mismatch를 admission reject로 처리한다.

Official measurement worktree는 `git status --porcelain`이 빈 clean 상태여야 한다. `HEAD == manifest revision == metrics revision`과 frozen worktree-diff hash를 admission에서 검증하고, cohort 도중 source/diff 변경을 금지한다.

Goal Plan §11의 고정 순서를 사용한다.

```text
6 discarded admitted full-run warm-ups
18 admitted official runs
rejected admission retries
```

Admission verdict를 p95/alloc 결과 검사 전에 기록한다. Admission 실패만 동일 slot에서 attempt number를 증가시켜 retry하고 모든 artifact를 보존한다. Runtime/workload/validator/aggregator/plan 변경 또는 valid cohort 불확정은 새 campaign ID로 전체 재시작한다.

## Aggregation and final decision

각 workload/strategy의 세 raw JSON p95 median을 계산하고 판정 후에만 표시값을 반올림한다. 다음을 별도로 보고한다.

- A→B maintenance/carriage tax;
- B→C executor benefit;
- A→C production package net result;
- target `CleanupProcessor` component material threshold;
- complete `RunCleanupPhase` containment threshold;
- target whole-tick end-to-end benefit threshold와 allocation safety;
- stress whole-tick/allocation safety.

판정:

- 모든 gate 통과: `Goal complete — indexed package retained`;
- B/C timing/allocation/semantic gate 실패: `Goal closed/rejected — full scan retained`;
- S3-A attribution 비material: `Goal deferred — Cleanup attribution not material`;
- valid signal 부족: `Hold`, retain/reject 단정 금지;
- 반복 infrastructure/authority blocker: `Goal blocked`.

`<= +5%`는 safety ceiling일 뿐 speedup 증거가 아니다. Stress workload에는 speedup을 요구하지 않으며 target/stress 결과를 평균내지 않는다.

Target retain은 `CleanupProcessor` component material improvement, complete `RunCleanupPhase` containment, whole-tick A→C end-to-end benefit을 모두 요구한다. Target whole-tick이 0보다 느리거나 개선이 frozen noise 범위 안이면 component 개선과 관계없이 reject/defer한다.

## Rollback

- A attribution 실패: B/C 미진입, production rollback 없음;
- B pre-C 실패: B candidate maintenance/carrier 제거, C 미진입;
- C semantic/structural 또는 final campaign 실패: B/C production package 전체 rollback;
- A diagnostics/oracle: capture-off allocation 0과 비악화 gate 통과 시에만 독립 retain 가능.

Rollback은 candidate maintenance, snapshot carrier, fast import, indexed executor의 정확한 intent/file inventory를 먼저 만들고 사용자 변경과 겹치지 않는 범위에서 수행한다. 파괴적 Git 명령을 사용하지 않고 실패/retry evidence를 삭제하지 않는다.

## 진행 기록과 closeout

각 phase 종료 시 Goal Plan에 dated progress block을 추가한다.

```text
Goal objective:
Current package: preflight | S3-A | S3-B | S3-C | campaign | closeout
Status: in_progress | provisional | retained | rejected | deferred | Hold | blocked
Revision / branch / runtime-tree hash:
Pre-existing user changes preserved:
Tests-first red evidence:
Focused/core/replay exact counts:
Strategy and structural counters:
Performance evidence paths:
Admission/retry status:
A→B / B→C / A→C observations:
Allocation signal:
Open risks:
Tests not run / reason:
Next safe action:
```

Closeout은 actual ordinary production path, final status, rollback revision, executed/not-run lanes, allowed claims와 explicit non-claims를 기록한다. `Hold — valid evidence incomplete`는 비종결 상태다. Reject/defer closeout은 유효한 조사 종결이지만 `Goal complete`나 speedup으로 부르지 않는다.

## 2026-08-29 KST — Proposed F1/B0 authority link

Historical pre-I1 record: [Slice 3 S3-A F1/B0 Amendment](./Gameplay-Wall-Tick-Cost-Optimization-Slice3-S3A-F1-B0-Amendment.md)은 B-Raw membership/processing clarification과 A/B/C current-contract remediation의 exact future scope를 작성한 non-normative docs-only artifact였고 `Proposed — awaiting I1 implementation-scope approval`에서 멈췄다. 사용자는 이후 exact amendment identity와 네 scope 항목을 I1로 승인했으며 bounded 구현·검증 closure가 완료됐다. 이 승인은 current `Hold — valid evidence incomplete`, Evidence Contract v4 §7.5 hard block, allocation/MeasurementAuthorization 누적 gate를 대체하지 않고 v5 또는 official capture 권한을 부여하지 않는다.

## 2026-08-29 KST — I1 bounded closure

사용자는 amendment SHA-256 `3f5db2a84580cadb8289ed1f2a0a8c013964b9313e3fb5eba652d1ac8fae8914`, source `d0310f8b99589157e82f2fe42cb5bd5aab2b6c28 / codex/third-party-license-inventory`와 네 scope 항목 전체를 승인했다. Bounded implementation/validation은 완료됐고, initial non-official capture smoke의 authoritative 결과는 allocation liveness 부족으로 `HOLD_CLEANUP_ADMISSION`이었다. 이후 terminal re-audit corrections의 current-source 검증 결과는 terminal report가 소유한다. 원래 formal red 누락은 소급 복원하지 않은 deviation이며 새 재검토 red만 `/mnt/d/J2M/evidence/20260829-slice3-i1-postreview-red/red/`에 보존했다. Repository Slice 3는 계속 Hold이며 v5, official measurement, S3-B/S3-C, commit/push는 승인되지 않았다.

## 2026-08-29 KST — Proposed D1/E0/I3 recovery link

[Slice 3 S3-A D1/E0/I3 Amendment](./Gameplay-Wall-Tick-Cost-Optimization-Slice3-S3A-D1-E0-I3-Amendment.md)는 allocation liveness, latest `335.781533%` timing noise와 E0/E1 이후 implementation-authority gap을 하나의 current proposal로 고정한다. E0 external execution과 E1 signal 채택은 runtime/probe/tool 수정 권한이 아니며, 필요한 diagnostic/adoption 변경은 exact `I3-pre`/`I3`와 `D3/E2` activation 승인을 거쳐야 한다.

이 link는 docs-only proposal을 가리킨다. D1/I2/D2, E0/E1, I3/D3, MeasurementAuthorization/M1, official capture, commit/push와 S3-B/S3-C는 승인되지 않았다. 다음 실행자는 threshold 완화, result-based outlier 제외, zero allocation의 성공 해석 또는 diagnostic artifact의 official 승격을 해서는 안 된다.

## 2026-08-29 KST — P2 design handoff

P2 design-only 작성은 완료됐다. 다음 실행자는 [D1/E0/I3 Amendment](./Gameplay-Wall-Tick-Cost-Optimization-Slice3-S3A-D1-E0-I3-Amendment.md)의 §12에 기록된 exact candidates를 승인 없이 구현하거나 실행하지 않는다. D1은 v5/workload/oracle/review-vector 네 artifact, E0-D는 characterization protocol 한 artifact의 별도 hard pause다.

Trustworthy M1 transport에는 caller digest가 아니라 별도 K1 승인된 Ed25519 public key와 detached signature가 필요하고, official terminal truth는 individual attempt가 아니라 finite authorized slots를 집계한 campaign FINAL이 소유한다. K1 key 미등록, allocation primary invalid, timing noise invalid, oracle 미활성 또는 어느 authorization/identity gate 누락도 계속 Hold다.
