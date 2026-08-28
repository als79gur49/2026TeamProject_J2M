# Gameplay Wall Tick Cost Optimization — Slice 3 Evidence Post-Amendment Audit

- 상태: v4 follow-up hardening in progress / repository `Hold — valid evidence incomplete`
- 작성일: 2026-08-28 KST
- 최종 보완일: 2026-08-28 KST, 3-round independent/cross-challenge audit 반영
- 감사 기준 revision: `25e623a94890f803142990fbeb7c2e11615b2ed0`의 dirty worktree
- 적용 가드레일: `gameplay-contract-hardening`
- 문서 역할: implementation-status evidence register; normative hard-pause와 entry 변경은 Goal Plan의 승인된 implementation-status erratum이 소유
- 기준 계약: initial findings are against historical [v3](../../Tools/contracts/gameplay_cleanup_slice3_evidence_contract_v3.md); closure is governed by approved [v4](../../Tools/contracts/gameplay_cleanup_slice3_evidence_contract_v4.md)
- 실행 문서: [Slice 3 Goal Plan](./Gameplay-Wall-Tick-Cost-Optimization-Slice3-Goal-Plan.md), [Slice 3 Goal Prompt](./Gameplay-Wall-Tick-Cost-Optimization-Slice3-Goal-Prompt.md), [Evidence Remediation Goal Prompt](./Gameplay-Wall-Tick-Cost-Optimization-Slice3-Evidence-Remediation-Goal-Prompt.md)

## 1. 감사 결론

2026-08-28 KST follow-up erratum: 최초 remediation closeout은 historical implementation record로 보존하지만 terminal-success eligibility는 다시 열렸다. frozen workload가 full-scan visit/copy/removal algebra는 제공해도 workload·repetition·tick별 exact visit expectation을 독립적으로 소유하지 않아, self-consistency가 oracle처럼 사용될 수 있기 때문이다. 이를 S3-EV-016으로 등록하며 승인된 oracle amendment 전에는 otherwise-ready 결과도 `FULL_SCAN_EXPECTATION_UNAPPROVED`로 Hold다. S3-EV-008은 복구 불가능한 historical evidence gap으로 남고, S3-EV-007/010은 이후 S3-B pre-entry 또는 별도 Measurement Goal의 경계다.

| Finding | v4 closeout |
|---|---|
| S3-EV-001, 002, 013 | live pre/post identity와 runtime/build/harness cohort, persisted canonical report recomputation, semantic/exit coherence로 false PASS 차단 |
| S3-EV-003, 005, 009, 014 | bool-safe exact types/domains/cardinality, strict recursive JSON/KV, malformed contract fail-closed, captures-only strategy matrix로 차단 |
| S3-EV-004, 006, 015 | provisional/final lifecycle, downstream `NOT_RUN`, safe alias/atomic-write, exact PASS/DEFERRED/HOLD transport로 차단 |
| S3-EV-016 | **open blocker** — independent exact full-scan visit oracle가 승인될 때까지 `PASS/DEFERRED` hard-block |
| S3-EV-008 | permanent historical deviation; 새 red evidence가 이를 대체하지 않음 |
| S3-EV-007, 010, 011, 012 | documented boundary/residue; repository Hold와 S3-B/S3-C prohibition 유지 |

최초 독립 재감사는 당시 known false `PASS / VERIFIED`를 `0`건으로 확인했지만, S3-EV-016 발견으로 그 closeout은 terminal-success 충분조건이 아니다. 새 official capture는 실행하지 않았으며 repository Slice 3는 `Hold — valid evidence incomplete`다.

세 서브 에이전트의 독립 재검토와 별도 adversarial fixture 재현을 교차검증했다. Goal 자체를 폐기할 정도의 논리 모순이나 production gameplay StrongContract 회귀는 발견되지 않았다. 현재 `Hold`와 S3-B/S3-C 미진입은 올바르다.

Historical v3 observation: 당시 evidence contract v3 구현에는 서로 다른 revision/worktree evidence와 실패한 performance admission을 `PASS / VERIFIED`로 만들 수 있는 false-PASS 경로가 있었다. 아래 S3-EV-001~015의 재현 설명은 그 historical baseline을 보존하며 current v4 동작을 설명하지 않는다. Current v4는 §1 closeout 표의 identity, canonical report, lifecycle, strict transport 가드로 이 경로를 닫았지만 S3-EV-016 때문에 terminal success는 계속 hard-block된다. 실제 hard-pause와 entry 조건은 Goal Plan의 승인된 `Normative implementation-status erratum`이 소유한다.

| 영역 | 분류 | 현재 판정 |
|---|---|---|
| Cleanup semantic order/authority/result parity | `StrongContract` | 검사한 경로에서 회귀 없음 |
| production executor selection | runtime `CurrentPolicy` | 현재 full scan 유지; B/C 미진입 |
| evidence schema/admission/manifest/runner | harness `CurrentPolicy` | S3-EV-001~015 closed; S3-EV-016 success transport hard-block |
| 최신 보존 S3-A evidence | evidence verdict | `HOLD_INVALID_EVIDENCE` / `Hold` 유지 |
| S3-B/S3-C | progression gate | 진입 금지 유지 |
| Goal 자체 | governance | 폐기하지 않고 국소 remediation으로 유지 |

## 2. 차단 발견 사항

### S3-EV-001 — revision/worktree identity 불일치가 `PASS / VERIFIED`로 통과

- 심각도: **High**
- 계약: revision, runtime-tree, harness, campaign identity는 존재하는 모든 artifact에서 일치해야 한다.
- 구현 관찰: `run_tests.sh`는 preflight와 captured artifact manifest에 `HEAD`, `WorktreeDiffSHA256`를 기록하지만 `gameplay_cleanup_slice3_evidence_manifest.py`는 preflight stage/strategy와 captured metrics/runtime-log hash만 비교한다. metrics에 기록된 revision도 교차검증하지 않는다. 더구나 worktree hash는 build 전에 한 번만 계산하고, build/guard restore 뒤 captured manifest에는 실제 tree를 재계산하지 않은 채 같은 shell 변수 값을 복사한다.
- 재현: preflight를 `HEAD=revision-one`, captured manifest를 `HEAD=revision-two`로 두고 worktree diff hash도 서로 다르게 설정했다. 결과는 exit `0`, `gateStatus=PASS`, `consistencyStatus=VERIFIED`, 빈 `consistencyChecks`였다.
- 추가 위험: preflight/captured 문자열 equality만 보강해도 둘 다 같은 stale hash이면 build 중 guard 밖 tracked/untracked mutation을 탐지하지 못한다. artifact가 실제로 소비한 runtime tree도 현재 bundle에서 독립 입증되지 않는다.
- 영향: 서로 다른 source/runtime tree 또는 capture cohort가 한 evidence bundle로 잘못 승인될 수 있다.
- 종료 조건:
  1. artifact별 required identity matrix를 먼저 고정하고 preflight, captured manifest, metrics, verdict provenance의 revision/runtime-tree/artifact/harness/campaign identity를 교차검증한다.
  2. build 직전과 build/guard restore 직후 실제 `HEAD`와 worktree hash를 각각 재계산하고 불일치를 `HOLD_INVALID_EVIDENCE`로 거부한다.
  3. artifact가 소비한 runtime-tree identity를 build output 또는 동등한 독립 carrier에 기록해 metrics와 교차검증한다.
  4. 단일-field mismatch, mixed cohort, guard 밖 mutation, stale copied hash fixture를 tests-first로 고정한다.

### S3-EV-002 — performance admission을 caller status만으로 우회 가능

- 심각도: **High**
- 계약: CLI exit status는 JSON verdict의 운반 수단일 뿐 독립 truth가 아니며, manifest는 caller-provided status만으로 `PASS`를 만들면 안 된다.
- 구현 관찰: evidence manifest 입력에는 performance admission JSON artifact가 없고 `performance_admission_status` 숫자만 있다. runner도 performance admission 결과를 보존 artifact로 넘기지 않는다.
- 재현: 실제 `gameplay_performance_admission.py`가 exit `1`을 반환하는 metrics와 Cleanup `ADMITTED`, calibration `READY` artifact를 준비한 뒤 manifest에 performance status `0`을 전달했다. 결과는 exit `0`, `PASS / VERIFIED`였다.
- 추가 공백: 현행 manifest PASS 테스트는 performance-valid schema 대신 `{"metrics": true}` 수준의 fixture를 사용하므로 이 우회를 막지 못한다.
- 영향: 기존 performance validator가 거부한 capture도 최종 evidence manifest에서 승인될 수 있다.
- 종료 조건:
  1. performance admission을 machine-readable artifact로 항상 보존한다.
  2. manifest가 artifact schema, verdict, provenance, metrics hash와 CLI status coherence를 직접 검증한다.
  3. status `0` 위조, artifact tamper, verdict/status 불일치가 모두 `HOLD_INVALID_EVIDENCE`가 되는 테스트를 추가한다.

### S3-EV-013 — verdict artifact 내부 의미 모순이 `PASS / VERIFIED`로 통과

- 심각도: **High**
- 계약: `READY`는 admitted하고 internally consistent한 evidence에서만 `PASS`가 될 수 있다.
- 재현: admission을 `verdict=ADMITTED`이면서 fatal reason이 존재하도록 만들고, calibration을 `status=READY`, `admitted=true`, `thresholds=null`, `signalValid=false`, `attributionMaterial=false`로 만들었다. caller status와 provenance/hash를 맞추면 manifest는 exit `0`, `PASS / VERIFIED`, 빈 checks를 반환했다.
- 원인: manifest는 admission enum/exit/provenance와 calibration enum/`admitted`/exit/provenance만 검사하고, producer가 정의하는 status별 semantic invariant를 재검증하지 않는다.
- 영향: artifact가 존재하고 hash가 맞더라도 변조되거나 drift한 payload가 authoritative `READY`로 승인될 수 있다. 이는 performance artifact 자체가 없는 S3-EV-002와 다른 trust boundary다.
- 종료 조건:
  1. `ADMITTED`는 빈 reasons와 admission schema invariant를 요구한다.
  2. `READY`는 `signalValid=true`, `attributionMaterial=true`, 유효한 thresholds/campaign rules를 요구한다.
  3. `DEFERRED_NOT_MATERIAL`과 각 Hold status도 signal/material/admitted/threshold 조합을 exact matrix로 검증한다.
  4. artifact semantic tamper fixture가 모두 `HOLD_INVALID_EVIDENCE`가 되는 tests-first evidence를 보존한다.

## 3. 중요 발견 사항

### S3-EV-003 — cardinality/exact-type 구현 위반과 numeric-domain 명세 공백

- 심각도: **Medium**
- 재현된 허용 사례:
  - 최상위 `repetitions` 누락과 workload당 한 run: `ADMITTED`;
  - B/C의 `candidateMembershipAddCount=-1` 및 대응 구조 카운터 음수: `ADMITTED`;
  - `warmupTicksPerRepetition=30.0`, `sampleTicksPerRepetition=100.0`: `ADMITTED`.
- direct v3 구현 위반:
  - missing repetition은 reject해야 하지만 `repetitions`가 존재할 때만 expected run-key cardinality를 구성한다.
  - tick field는 JSON integer여야 하지만 top-level warm-up/sample tick type을 완전히 검사하지 않는다.
- 명세와 구현의 동시 공백:
  - v3는 count의 JSON integer type은 고정하지만 각 count의 positive/nonnegative domain을 완전히 고정하지 않는다.
  - workload contract `schemaVersion=2.0`도 Python numeric equality로 허용되며, v3 exact-type 목록에 모든 schema version이 명시돼 있지 않다.
- 원인:
  - conditional cardinality, partial exact-type validation, 명시되지 않은 numeric domain이 한 경계에 섞여 있다.
- 영향: 누락되거나 의미상 불가능한 schedule/maintenance evidence가 authoritative `ADMITTED` verdict를 받을 수 있다. calibration의 별도 3-run 검사가 일부 runner 경로를 막더라도 admission verdict의 공백은 남는다.
- 종료 조건: 먼저 formal contract amendment/version policy로 required field, schemaVersion exact JSON integer, positive/nonnegative domain을 고정한다. 이후 validator가 exact repetition/cardinality/type/range를 거부하는 negative fixture를 추가한다.

### S3-EV-004 — `DEFERRED`가 runner 최종 출력에서 `PASS`로 축약됨

- 심각도: **Medium**
- 계약: `DEFERRED_NOT_MATERIAL`은 terminal `DEFERRED`이며 `PASS`와 구분해야 하고 S3-B/S3-C를 허용하지 않는다.
- 구현 관찰: calibration과 manifest는 `DEFERRED`에 exit `0`을 반환한다. runner는 세 status가 모두 `0`이면 manifest의 `gateStatus`를 다시 읽지 않고 `Gameplay performance measurement: PASS`를 출력한다.
- schema 관찰: manifest의 `runnerObservedStatus`는 실제 runner 관측값을 입력받지 않고 계산된 `gateStatus`를 그대로 복제한다. `VERIFIED / HOLD`에서 manifest exit `0`은 consistency-tool 성공으로 해석할 수 있지만 현재 contract에 그 transport 의미가 명시돼 있지 않다.
- 영향: 사람 또는 후속 automation이 defer를 S3-A pass로 오독할 수 있다.
- 종료 조건: runner가 final manifest의 enum을 authoritative하게 읽고 `PASS`, `DEFERRED`, `HOLD`를 그대로 출력·전파한다. `runnerObservedStatus`는 제거하거나 `derivedGateStatus`로 고치고, manifest CLI exit가 consistency transport인지 terminal gate인지 contract에 명시한다.

### S3-EV-005 — malformed workload contract가 result artifact 없이 crash

- 심각도: **Medium**
- 재현: repository-owned workload contract의 JSON root를 `[]`로 둔 임시 복사본에서 admission과 calibration 모두 exit `1`, output artifact 없음, `AttributeError: 'list' object has no attribute 'get'` traceback으로 종료했다.
- 원인: `_load_workload_contract()`가 root object 여부를 확인하기 전에 `.get()`을 호출하고, `AttributeError`가 CLI fail-closed 변환 범위에 포함되지 않는다.
- 영향: 잘못된 contract를 거부하기는 하지만 v3가 요구하는 machine-readable failure provenance가 사라진다.
- 종료 조건: root type을 먼저 확인하고 모든 repository-input load/validation failure가 `REJECTED` 또는 `HOLD_INVALID_EVIDENCE` artifact를 생성하게 한다.

### S3-EV-006 — runner 조기 실패에 final manifest와 `NOT_RUN`이 없음

- 심각도: **Medium**
- 구현 관찰: build, Player, marker validation failure는 final evidence manifest 생성 전에 반환한다.
- 계약: missing 또는 unexecuted stage는 `NOT_RUN`이며 `PASS`로 취급하지 않는다.
- 영향: 실패한 campaign attempt에서 어느 단계가 실행됐고 어느 단계가 실행되지 않았는지 하나의 final artifact로 복원할 수 없다.
- 종료 조건: runner attempt 시작 시 provisional manifest를 만들고 각 stage를 `NOT_RUN -> PASS/HOLD/DEFERRED`로 전이한다. 모든 조기 종료 경로에서 final manifest를 원자적으로 기록한다.

### S3-EV-007 — Phase 0 candidate-writer inventory에 fast import가 완전히 포함되지 않음

- 심각도: **Medium**, S3-B 전 차단
- 문서 관찰: Goal Plan 본문은 `WorldState.CreateFromSnapshotFast(WorldSnapshot)`를 candidate predicate를 바꿀 수 있는 seam으로 열거하지만 candidate predicate writer 표에는 포함하지 않고 snapshot creation 표에만 기록한다.
- runtime 관찰: 이 경로는 snapshot으로부터 mutable `WorldState`와 derived index를 재구성하므로 future candidate carrier parity의 입력 seam이다.
- 영향: 현재 S3-A full scan과 `Hold`에는 영향이 없지만, 불완전한 writer inventory로 S3-B index maintenance를 구현하면 import 후 candidate membership drift를 놓칠 수 있다.
- 문서 보완 상태: Goal Plan writer inventory에는 fast import reconstruction seam을 추가했다.
- 남은 종료 조건: authored snapshot import 후 removal/timer/immediate-transition candidate parity fixture를 S3-B precondition으로 추가한다. 해당 tests-first fixture 전에는 production candidate maintenance 구현을 시작하지 않는다.

### S3-EV-009 — KV/JSON duplicate serialized key가 last-write-wins

- 심각도: **Medium**
- 재현: preflight/captured KV에서 앞에 충돌하는 schema/hash를 두고 뒤에 정상 값을 반복하면 마지막 값으로 `PASS / VERIFIED`가 된다. JSON admission에서도 `"verdict":"REJECTED","verdict":"ADMITTED"` 순서의 duplicate member가 뒤 값으로 해석돼 `PASS / VERIFIED`가 됐다.
- 영향: 동일 artifact bytes를 parser별로 다르게 해석할 수 있고, 앞쪽의 충돌 evidence가 조용히 사라진다. 단순 SHA-256 binding으로 semantic ambiguity가 해소되지 않는다.
- 종료 조건: KV는 duplicate key와 malformed nonempty line을 거부하고, JSON은 duplicate-member-detecting loader를 공통 사용한다. verdict/status/schema/hash/identity duplicate fixture를 추가한다.

### S3-EV-014 — active strategy identity 충돌이 `ADMITTED`

- 심각도: **Medium**, P0 identity fixture 대상
- 재현: 유효한 A captures와 allocation/invocation 증거에 top-level `strategy="C"`를 동시에 넣어도 validator는 captures form만 사용하고 top-level 값을 무시해 `ADMITTED`를 반환했다.
- 영향: 실제 A 실행 자체가 C로 바뀌지는 않지만 dual representation과 future producer drift가 상충하는 active-strategy identity를 남긴다.
- 종료 조건: legacy top-level form과 captures form의 동시 존재를 금지하거나 exact equality를 요구한다. stage, requested/top-level/captures/allocation/provenance strategy의 identity matrix와 pairwise mismatch fixture를 고정한다.

## 4. 낮은 위험 및 명확화 항목

### S3-EV-008 — formal tests-first red provenance의 영구 historical gap

- 심각도: **Low**, historical/process deviation
- 요구사항: red command, test name, assertion, exit code, timestamp, source/worktree hash, failure-log path를 보존해야 한다.
- 관찰: v3 amendment 이전 재현은 filesystem failure log가 없었고 formal retained red라고 주장하지 않는다. 이는 허위 claim이 아니지만 소급 복원할 수도 없다.
- disposition: 기존 공백은 `permanently unavailable historical provenance`와 승인된 limitation/waiver로 기록한다. 다음 remediation부터 각 새 결함의 red를 요구 metadata와 함께 `/mnt/d/J2M/evidence` 아래에 보존하되, 이를 과거 log의 대체물이라고 주장하지 않는다.

### S3-EV-010 — `WallCount`는 authored Wall provenance가 아님

- 심각도: S3-A **Low** vocabulary/claim 제한, S3-B/official authored-Wall evidence **Medium** pre-entry gap

runtime에는 별도 `EntityType.Wall`이 없고 authored Wall이 `EntityType.None`으로 materialize될 수 있다. 현행 S3-A `WallCount`는 `WorkloadEntityCount`를 복제하는 합성 진단값이다. S3-A generic Cleanup participation evidence로는 사용할 수 있어 Low vocabulary 제한이지만, S3-B predicate/content parity와 official authored-Wall claim에는 Medium pre-entry gap이다. S3-B 전 field/workload를 truthful generic-None/participant vocabulary로 바꾸거나 authored origin identity를 별도 보존한다. runtime authoritative schema 확대는 요구하지 않는다.

### S3-EV-011 — defer 설명 한 문장의 단독 오독 가능성

Goal Plan과 상위 Plan의 “S3-A에서 Cleanup attribution 실패 -> defer” 문장은 admitted·noise-valid timing/allocation signal에서 non-material임이 증명된 경우만 의미한다. noise, allocation liveness, admission 불완전은 `Hold`다. 다른 normative section과 직접 모순되지는 않지만 문장 자체에 이 전제를 넣는다.

- 문서 보완 상태: Goal Plan과 상위 Plan의 defer 문장을 admitted·noise-valid 전제와 invalid-signal `Hold` 분기로 교정했다.

### S3-EV-015 — output/input path alias가 self-invalid manifest를 생성

- 심각도: **Low hardening**, 독립 CLI trust boundary로 운영하면 Medium
- 재현: manifest `--output`을 metrics input과 같은 경로로 지정하면 overwrite 전 hash로 `PASS / VERIFIED`를 계산한 뒤 metrics를 manifest로 덮어쓴다. 종료 직후부터 기록 hash와 실제 input이 다르다.
- 범위: 공식 runner의 기본 경로는 서로 다르므로 자연 발생하는 현재 campaign blocker는 아니다. symlink/hardlink 또는 독립 CLI 오용에는 취약하다.
- 종료 조건: output과 모든 input/tool/contract의 resolved path/inode alias를 거부하고 temporary write + atomic replace 및 write 전후 input hash 확인을 적용한다.

### S3-EV-016 — full-scan expected visit count가 independent oracle이 아님

- 심각도: **High terminal-evidence blocker**
- 재현: 현행 validator는 `fullScanEntityVisitCount - survivorCopyCount = removalProcessedCount` 관계를 검사하지만, 세 값이 동일 producer/capture에서 생성된다. frozen workload contract에는 workload·repetition·tick별 exact visit expectation이 없다.
- 영향: producer가 세 값을 함께 잘못 생성해도 algebra가 맞으면 Cleanup admission이 통과할 수 있어, `READY` 또는 `DEFERRED_NOT_MATERIAL`이 독립 기대값 없이 terminal success로 운반될 수 있다.
- 현재 차단: finalizer가 otherwise-successful 결과에 `FULL_SCAN_EXPECTATION_UNAPPROVED`를 추가해 `HOLD_INVALID_EVIDENCE`로 고정한다.
- 종료 조건: 별도 계약 승인으로 exact oracle의 소유자·계산식·frozen bytes를 고정하고, tests-first negative fixture와 same-revision validation을 완료한다. 이 문서나 현 수정은 workload amendment 또는 새 official capture를 승인하지 않는다.

### S3-EV-012 — unrelated untracked residue

`Assets/AddressableAssetsData/Windows.meta`가 untracked 상태다. Slice 3 계약 결함은 아니며 이 감사/후속 remediation에 섞어 삭제하거나 stage하지 않는다.

## 5. 모순이 아닌 것으로 확인한 사항

- 당시 v3-era Python regression fixture의 `48/48 green`과 현재 evidence `Hold`는 모순이 아니다. 전자는 해당 historical fixture의 결과이고, 후자는 current v4 evidence eligibility와 실제 release-like allocation signal의 liveness/admission 판정이다. 이 감사의 false-PASS 사례와 S3-EV-016은 historical test coverage가 current contract 전체를 대변하지 못함을 보여준다.
- allocation/noise가 불완전하면 `Hold`이고, admitted·noise-valid signal에서 non-material임이 증명된 경우만 `DEFERRED`다.
- 권한 우선순위는 순환하지 않는다. 외부 사용자 승인과 최신 stricter hard-pause가 historical progress block보다 우선한다.
- A/B/C enum은 schema vocabulary일 뿐이다. runtime selector는 B/C를 거부하고 production executor는 full scan을 유지한다.
- current dirty worktree는 S3-A audit/remediation에는 허용된다. clean worktree는 official campaign admission 조건이다.
- broad unfiltered `full`은 실행되지 않았고 금지된 project-wide/full-green claim도 없다.

## 6. 현재 evidence 판정

Historical v3 diagnostic only: 보존 capture `20260827T171209Z`에 당시 diagnostic 도구를 적용한 기록은 다음과 같다. Compatibility policy상 이 schema-1/v3 artifact는 v4 terminal manifest로 승격되거나 현재 v4 verdict로 재발급될 수 없다.

| Gate | Result |
|---|---|
| performance admission | `ADMITTED`, exit `0` |
| Cleanup admission | `REJECTED`, exit `1` |
| rejection reason | `allocationCounterProbeBytes=0`, minimum `4096` 미충족 |
| calibration | `HOLD_INVALID_EVIDENCE`, `thresholds=null`, exit `1` |
| historical evidence manifest | `VERIFIED / HOLD` (v3 diagnostic label; not a v4 terminal verdict) |

따라서 이 historical evidence로 `PASS`, S3-B 또는 S3-C 진행을 정당화할 수 없다. 위 false-PASS 결함은 당시 미래 evidence 신뢰성을 차단했던 문제였고 current v4에서는 닫혔다. 현재 blocker는 S3-EV-016이며, historical capture의 liveness 실패를 pass로 뒤집는 근거도 없다.

## 7. 수정 순서

Evidence artifact/schema/version을 바꾸는 항목은 단순 validator patch로 처리하지 않는다. required identity matrix, performance verdict artifact, status semantic matrix, `NOT_RUN` lifecycle, numeric domain, duplicate rejection을 먼저 Evidence Contract v4 또는 명시적인 v3 normative amendment로 고정하고 matching compatibility/rejection policy를 함께 기록한다.

### P0-1 — identity/cohort binding

1. S3-EV-001의 cross-file identity와 build 전/후 live tree 재계산을 구현한다.
2. S3-EV-014의 stage/strategy identity matrix를 구현한다.
3. revision/runtime-tree/artifact/stage/strategy/workload/harness/campaign mismatch와 build-time mutation을 `HOLD_INVALID_EVIDENCE`로 고정한다.

### P0-2 — authoritative verdict semantics

1. S3-EV-002의 performance admission JSON truth artifact를 보존하고 status/provenance/hash coherence를 검증한다.
2. S3-EV-013의 admission/calibration status별 payload invariant를 검증한다.
3. caller-status 위조와 semantic tamper가 모두 false `PASS`를 만들지 못하게 한다.

### P1-1 — exact schema

1. S3-EV-003의 required cardinality, exact JSON type, positive/nonnegative domain, schemaVersion policy를 계약과 validator에 함께 고정한다.
2. S3-EV-009의 KV/JSON duplicate ambiguity를 fail-closed한다.

### P1-2 — fail-closed artifact lifecycle

1. S3-EV-005의 malformed repository input도 machine-readable result를 보존한다.
2. S3-EV-006의 runner 조기 실패에 provisional/final manifest와 `NOT_RUN` stage를 보존한다.
3. S3-EV-015의 input/output alias를 거부하고 atomic output을 사용한다.

### P1-3 — terminal-state transport

1. S3-EV-004의 `PASS / DEFERRED / HOLD` enum을 runner가 직접 읽고 그대로 출력한다.
2. manifest exit semantics를 consistency transport와 terminal verdict 중 하나로 명시한다.
3. `runnerObservedStatus`를 사실에 맞게 제거·개명하거나 runner-owned final artifact로 이동한다.

### P1-4 — S3-B entry completeness

1. S3-EV-007의 fast-import writer inventory/source audit과 failing fast/slow import candidate-parity fixture를 production candidate maintenance 구현 전에 완료한다.
2. S3-EV-010의 workload/Wall identity를 truthful vocabulary 또는 별도 authored provenance로 고정한다.
3. S3-EV-011의 defer 전제를 Goal Plan과 상위 Plan에서 명확히 한다.

### History/governance disposition

- S3-EV-008의 과거 red provenance는 소급 복원하지 않고 permanent historical deviation으로 남긴다. 새 remediation red만 formal evidence로 보존한다.
- 이 감사는 implementation-status register다. 현재 hard-pause와 entry 변경은 Goal Plan의 승인된 normative implementation-status erratum에서 고정한다.
- S3-EV-012는 remediation scope 밖 사용자 소유 residue로 유지한다.

## 8. 단계별 재개 gate

| Stage | 현재 허용 범위 | terminal/entry blocker |
|---|---|---|
| S3-A remediation | P0/P1 contract·tool·negative-test 수정과 비공식 diagnostic | P0/P1 closure, approved independent exact visit oracle와 frozen contract/workload amendment, allocation-capable 또는 사전 승인 equivalent signal, 새 campaign identity, admitted·noise-valid `PASS` capture 필요 |
| S3-B pre-entry | writer inventory/source audit와 failing fast-import parity test 작성까지 | P0/P1, 새 S3-A `PASS`, S3-EV-007 inventory/parity precondition, truthful workload identity, 사용자 continuation 전 production candidate maintenance 구현 금지 |
| S3-C pre-entry | S3-B gate 통과 후 indexed executor 설계/구현 | S3-B all-state/mutation/lifetime/snapshot/fast-import/A-B parity와 maintenance/allocation tax gate 및 사용자 continuation 필요 |
| final official campaign/retain | 위 gate 뒤 승인된 build-once/run-many protocol | approved exact visit oracle amendment, clean worktree, build 전/후 live tree identity, exact A/B/C cohort, focused/core/replay와 official timing/allocation/parity gate 전 retain/Goal complete 금지 |

`DEFERRED` 또는 `HOLD`는 S3-B 진입을 허용하지 않는다. Tests-first writer inventory와 failing parity test까지 “S3-B production 구현”으로 금지하면 순환하므로 pre-entry remediation으로 허용하되, candidate maintenance production code는 위 gate 뒤에만 시작한다.

## 9. 감사 시 실행한 검증과 한계

- 세 서브 에이전트가 Goal/Prompt scope, evidence admission/manifest/runner, gameplay StrongContract를 1차 독립 감사, 2차 상호 반론, 3차 severity/gate 합의로 재검토했다.
- 당시 audit snapshot의 Python regression suite: `48/48 passed`.
- `py_compile`, `bash -n run_tests.sh`, trailing-whitespace scan, `git diff --check`: 통과.
- adversarial temporary fixture로 S3-EV-001~005, S3-EV-009, S3-EV-013~015를 비파괴 재현했고, S3-EV-006은 runner의 조기 return과 final-manifest 생성 순서를 source audit으로 확인했다.
- production gameplay code와 evidence artifact는 이 감사에서 수정하지 않았다.
- replay, UI, broad unfiltered `full`, 새 gameplay-performance capture는 실행하지 않았다.

이 문서는 문제 등록, remediation evidence requirement, stage별 blocker matrix를 기록한다. Normative hard-pause는 Goal Plan의 승인된 erratum이 소유한다. 발견 사항을 해결하기 전에는 Goal Plan의 과거 green count나 “hardened” 문구를 evidence contract 완료 증거로 사용하지 않는다.
