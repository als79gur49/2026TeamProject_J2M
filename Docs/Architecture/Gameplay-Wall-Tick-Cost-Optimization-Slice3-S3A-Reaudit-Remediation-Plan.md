# Slice 3 S3-A 재감사 수정안

- 작성일: 2026-08-29 KST
- 문서 상태: `Proposal-level GO — implementation not authorized`
- 저장소 상태: `Hold — valid evidence incomplete`
- 대상: S3-A 진단·evidence 계약 복구안
- 비대상: 공식 S3-A capture, S3-B/S3-C 구현, production Cleanup executor 교체, commit/push

## 1. 목적과 권한 경계

이 문서는 2026-08-29 S3-A 재감사에서 확인된 모순과 잠재 false-success 경로를 실제 변경 단위, tests-first 순서, 승인 지점, 완료 조건으로 바꾼 **수정 제안서**다. 이 문서의 병합 또는 리뷰 완료만으로 다음 작업을 승인하지 않는다.

- Evidence Contract v5 또는 frozen full-scan oracle의 normative 승인
- production 코드·evidence 도구 구현
- allocation 동등 signal 채택
- 공식 S3-A Measurement Goal 또는 capture 실행
- S3-B/S3-C 진입

권한은 영역별로 나뉜다. Gameplay StrongContract는 `AGENTS.md`, canonical architecture와 Goal semantic contract가 소유한다. implementation scope와 stage pause는 approved Goal amendment/Prompt가 소유한다. artifact schema, reason registry, verdict와 transport는 v5 activation 전까지 Evidence Contract v4가 소유한다. official measurement는 별도 Measurement Goal과 exact authorization artifact가 소유한다. 이 proposal은 어느 영역에도 normative authority를 갖지 않으며, 승인 전까지 v4 §7.5의 `FULL_SCAN_EXPECTATION_UNAPPROVED`를 해제하지 않는다.

## 2. 계약 분류

### 2.1 StrongContract — 변경 금지

- Cleanup semantic order는 `Removal -> Timer -> Transition`이다.
- removal 대상은 같은 tick의 timer/즉시 transition을 실제 처리하지 않는다. raw predicate membership이 removal과 겹치는지 여부는 현재 문서가 충돌하므로 Package B의 normative clarification 전까지 StrongContract로 단정하지 않는다.
- `CleanupPhaseResult` 6개 필드, ordered commit operations, EventLog, FinalEntities, phase trace, determinism hash, FullCanonical trace, occupancy 및 auxiliary lifetime 결과는 capture on/off에서 동일해야 한다.
- presentation, diagnostics, admission 도구는 authoritative simulation을 변경하지 않는다.
- 공식 evidence는 clean/same-revision, exact cohort, exact schema, exact artifact hash를 만족해야 한다.
- evidence가 불완전하거나 모순이면 성공으로 승격하지 않고 `HOLD / HOLD_INVALID_EVIDENCE`로 닫는다.
- contract 승인, 구현 승인, 공식 측정 승인, S3-B/S3-C 진입 승인은 각각 별도다.

### 2.2 CurrentPolicy — 검증 후 교체 가능

- 현재 production Cleanup executor는 full scan이다.
- S3-A 진단 데이터 구조와 report 구현 세부는 StrongContract를 보존하는 범위에서 변경할 수 있다.
- 공식 S3-A 전략은 `A`만 허용된다.
- Evidence Contract v4와 S3-EV-016 hard block은 v5가 명시적으로 승인·구현·검증되기 전까지 유효하다.

## 3. 재감사 finding register

`S3A-RR-*`는 이 제안서에서만 쓰는 추적 ID이며 기존 normative audit ID를 대체하지 않는다.

| ID | 우선순위 | 관찰 | 현재 영향 | 수정 package |
|---|---|---|---|---|
| `S3A-RR-001` | P0 | producer는 measured run 밖의 별도 workload에서 reference를 1회 실행하지만 validator는 각 run의 `referenceOracleInvocationCount == executedTicks`를 요구한다. 실제 preserved metrics는 200 ticks/0 reference다. | 현재 producer output은 다른 gate가 해결돼도 Cleanup admission을 통과할 수 없다. | A |
| `S3-EV-016` | P0 | frozen workload에는 workload/repetition/tick별 독립 exact full-scan visit expectation이 없다. | v4가 의도대로 PASS/DEFERRED를 hard block한다. | D |
| `S3A-RR-002` | P1 | Goal §4의 raw candidate predicate는 removal overlap을 허용하지만 tests-first 표는 removal-only라고 적어 membership과 actual processing의 계약이 충돌한다. | 현재 counter가 raw membership인지 effective post-removal processing 후보인지 확정할 수 없다. | B |
| `S3A-RR-003` | P1 | runner는 Git status를 기록하지만 current finalizer는 clean worktree 자체를 admission gate로 검증하지 않는다. | S3-EV-016 제거 뒤 dirty capture가 성공 후보가 될 수 있다. | C |
| `S3A-RR-004` | P1 | standalone Cleanup admission/calibration은 일부 malformed/missing capture identity를 성공으로 낼 수 있다. | final manifest가 현재 terminal PASS를 막지만 하위 도구의 성공 의미가 v4와 불일치한다. | C |
| `S3A-RR-005` | P1 | finalizer의 live Git check와 atomic replace 사이 repository mutation을 다시 확인하지 않는다. | 협조하지 않는 외부 수정에 대한 terminal identity 보장이 과장될 수 있다. | C |
| `S3A-RR-006` | P1 | workload-level `oracleParityVerified`는 별도 1-tick 관찰 뒤 상수 `true`이고 measured cohort와 결합되지 않는다. reference wrapper도 base `CleanupProcessor` commit path만 관찰한다. | parity claim의 범위와 invocation 증명이 모호하다. | A |
| `S3A-RR-007` | P2 | 상위 plan/prompt/status 문구 일부가 S3-EV-016, exact parser/binding, 현 Hold 이유를 완전하게 요약하지 않는다. | 다음 실행자가 누적 gate를 대안 조건으로 오독할 수 있다. | F |
| `S3-EV-010` | P2 | synthetic entity는 `EntityType.None`인데 `wallCount`가 entity count를 그대로 노출한다. | generic inert-N attribution은 가능하지만 authored Wall-heavy evidence로 주장할 수 없다. | E |

추가 관찰: release-like Mono Player에서 `GC.GetAllocatedBytesForCurrentThread()` liveness control이 유효한 allocation signal을 만들지 못했다. 이는 이미 알려진 S3-A Hold 이유이며 package E에서 별도 characterization과 승인 절차로 다룬다.

## 4. 구체 수정 package

### Package A — measured cohort reference 계약 정합

#### 변경안

1. timing/capture-off 측정 phase를 먼저 완결하고, 별도 deterministic companion phase에서 structural+reference world를 매 sample tick `CleanupCaptureMode.Structural | CleanupCaptureMode.Reference`로 실행한다. reference pass를 stopwatch 밖에서 interleave하는 것만으로는 GC/cache 오염이 배제되지 않으므로 같은 timing loop 앞뒤에 섞지 않는다. 필요하면 별도 Player process로 격리한다.
2. timed world는 `Timing`만, capture-off world는 diagnostics off를 유지한다. 두 phase는 동일 workload/seed/schedule/tick/repetition identity로 결합하되 서로 다른 world instance임을 carrier에 기록한다.
3. warmup은 reference invocation cardinality의 일부가 아니다. exact 요구는 measured companion tick에만 적용한다.
4. `CaptureRunsRoundRobin`은 JSON 문자열만 반환하지 않고 run 목록과 아래 workload parity 판정을 함께 보유하는 구조화 결과를 반환한다.
5. generic `oracleParityVerified`는 v5에서 `cleanupProcessorCommitOracleParityVerified`로 rename한다. 이 값은 상수로 직렬화하지 않고 모든 repetition에 대해 다음을 만족할 때만 `true`다.
   - `referenceOracleInvocationCount == executedTicks`
   - `invariantMismatchCount == 0`
   - exact run cardinality와 tick cardinality가 맞음
6. 기존 별도 `oracleWorkload` 1-tick precheck는 삭제한다. 별도 smoke check로 유지할 필요가 있다면 measured parity의 근거 또는 invocation count로 사용하지 않고 명시적으로 다른 필드에 둔다.
7. reference claim 명칭과 문서 범위를 `CleanupProcessor commit-operation parity`로 한정한다. 전체 `TickPipeline.RunCleanupPhase` parity는 기존 StrongContract scenario/replay fixtures로 증명하며, base processor oracle이 auxiliary expiry를 관찰한다고 주장하지 않는다.
8. `CleanupSlice3PlayerCalibration`은 `VECTORQUAKE_CAPTURE_BUILD` 조건부 facade이므로 normal EditMode test에서 직접 호출하지 않는다. 항상 컴파일되는 internal producer core를 추출하고 facade가 그대로 delegate하게 한다. 이 core test와 동일 scripting define을 사용하는 dedicated capture-build facade/Probe compile-smoke는 대안이 아니라 둘 다 필요하다.
9. producer가 직렬화한 parity bool은 canonical admission이 per-run count/mismatch에서 다시 계산하고 exact equality를 요구한다. producer bool 자체는 신뢰 입력이 아니다.

#### tests first

- 항상 컴파일되는 producer core의 실제 output을 parse하는 Scenario/Integration test와 dedicated capture-build facade/Probe compile-smoke를 모두 먼저 red로 추가한다. smoke가 만든 최종 JSON을 Python admission chain이 실제 소비해야 한다.
- 각 run의 executed/reference count exact equality, mismatch zero, workload-level parity derivation을 검사한다.
- produced JSON shape를 Python Cleanup admission builder에 전달하는 cross-boundary fixture를 추가한다. hand-authored ideal count만 검증하는 fixture로 대체하지 않는다.
- reference mismatch, missing invocation, one-short invocation, duplicated repetition을 각각 fail-closed로 검증한다.
- timed/capture-off world에서 reference invocation이 발생하지 않고 companion phase가 timing phase 뒤에 실행된다는 분리 test를 둔다.
- auxiliary expiry를 포함한 whole `RunCleanupPhase` 결과를 capture on/off로 비교하는 결합 fixture를 추가한다. base CleanupProcessor commit oracle로 이 증명을 대신하지 않는다.

#### 완료 조건

- 실제 producer JSON의 reference sub-contract가 green이고, full Cleanup report에는 아직 해결하지 않은 gate의 정확한 rejection reason만 남는다. Package C/D/E 전에는 full `ADMITTED`를 Package A 단독 완료 조건으로 요구하지 않는다.
- 모든 measured run은 `referenceOracleInvocationCount == sampleTicks`다.
- timing/capture-off와 reference가 별도 phase/process라서 direct 및 interleaved reference 비용을 timing sample에 포함하지 않는다.
- gameplay 결과 및 StrongContract parity는 변경되지 않는다.

### Package B — removal membership/processing 계약 명확화

#### Hard Pause B0 — normative clarification

현재 문서는 두 해석을 동시에 갖는다.

- Goal §4 candidate view: removal, timer, immediate를 독립 raw predicate로 정의하므로 overlap 가능
- tests-first 표와 runtime semantics: removal 대상은 timer/immediate를 실제 처리하지 않음

따라서 `RemovalProcessor` counter를 바로 survivor-only로 바꾸지 않는다. 먼저 membership과 processing vocabulary를 분리하는 normative clarification을 사용자에게 승인받는다.

#### 권고안 B-Raw

미래 S3-B candidate carrier와 현재 predicate 정의를 보존하기 위해 다음을 권고한다.

1. `removalCandidateCount`, `timerCandidateCount`, `immediateTransitionCandidateCount`를 v5에서 각각 raw predicate match라는 exact 의미로 rename한다. raw overlap은 허용한다.
2. `removalProcessedCount`, `timerProcessedCount`, `transitionProcessedCount`는 actual execution 의미를 유지한다. removal 대상의 timer/transition processed count는 0이어야 한다.
3. validator는 raw membership count와 processed count의 equality를 요구하지 않는다. 대신 removal precedence와 workload별 독립 expected tuple을 검증한다.
4. alternative survivor-only/effective view를 채택하려면 `RemovalProcessor` counter뿐 아니라 S3-B membership writer, snapshot/fast-import, validator와 Goal §4 predicate 계약을 함께 변경·승인한다.
5. 어느 안에서도 authoritative write, entity ordering, removal 결과는 바꾸지 않는다.

#### tests first

승인된 vocabulary에 따라 아래 membership/processing matrix를 direct diagnostics assertion으로 먼저 red 고정한다. 표의 membership 수치는 권고안 B-Raw 기준이다.

| 입력 | raw removal | raw timer | raw immediate | processed 결과 |
|---|---:|---:|---:|---|
| dead + `stateTimer > 0` | 1 | 1 | 0 | removal only |
| marked + Acting + `stateTimer <= 0` | 1 | 0 | 1 | removal only |
| marked + Cooldown + `stateTimer <= 0` | 1 | 0 | 1 | removal only |
| survivor + `stateTimer > 0` | 0 | 1 | 0 | timer path |
| survivor + Acting/Cooldown + `stateTimer <= 0` | 0 | 0 | 1 | transition path |
| inert survivor | 0 | 0 | 0 | none |

추가 boundary는 `hp=-1/0/1 + marked`, unknown state/negative timer, same-tick spawned survivor `timer>0`의 candidate 1/processed 0, Acting/Cooldown `timer=1`의 timer candidate 1 + post-timer transition processed 1/immediate candidate 0이다. 동시에 removal/timer/transition ordered commit fixture와 final result parity를 유지한다.

#### 완료 조건

- B0에서 raw/effective vocabulary가 승인되고 matrix가 모두 green이다.
- 기존 frozen target/stress counts가 의도한 수치와 계속 일치한다.
- raw membership과 actual processing이 서로 다른 필드로 명확하며 production semantic diff가 없음을 focused scenario와 core/replay 범위 판단으로 증명한다.

### Package C — clean identity, standalone verdict, bounded finalization 강화

#### C1. clean worktree gate

MeasurementAuthorization이 승인되면 그 exact campaign cohort에 속한 build, warm-up, calibration, official, retry attempt 전체는 최종 admission 결과와 caller의 `attemptKind`에 관계없이 실행 전에 exact clean worktree를 요구한다. terminal PASS/DEFERRED는 authorized cohort membership이 exact한 attempt에만 허용한다. audit/remediation과 authorization 밖 characterization까지 확대하는 것은 v5/E0에서 별도 승인할 사항이며 이 제안이 current v4 범위를 조용히 넓히지 않는다.

- `git status --porcelain=v1 -z --untracked-files=all --ignore-submodules=none`이 exit 0이고 raw result가 empty여야 한다.
- staged, unstaged, untracked, submodule dirtiness를 포함한다. ignored file은 evidence identity 밖임을 명시한다.
- dirty면 provisional attempt manifest를 남기고 `HOLD / HOLD_INVALID_EVIDENCE`로 종료하며 build/player는 실행하지 않는다. v4에서는 `SEMANTIC_INVARIANT_INVALID` + exact path `preflight.GitStatusShort`를 사용한다. `WORKTREE_DIRTY` 같은 신규 code/field는 v5 exact reason registry의 D1 승인 뒤에만 도입한다.
- 현재 사용자 소유 untracked `Assets/AddressableAssetsData/Windows.meta`를 자동 삭제·이동·stage하지 않는다. 공식 attempt 전에 사용자가 별도로 처리해야 한다.
- preflight와 terminal checkpoint의 exact empty status, HEAD, runtime-tree identity를 attempt/final manifest에 결합한다.

#### C2. standalone report identity

performance admission, Cleanup admission, calibration이 같은 strict context/envelope validator를 호출하게 한다. 각 CLI는 metrics만 받지 않고 frozen preflight/captured context artifact를 필수 입력으로 받아 실제 input bytes와 expected revision/runtime tree/tool/contract/oracle hash를 비교한다. 성공 verdict 전 다음 exact 필드를 공통 검증한다.

- evidence contract/schema version
- repository revision/runtime-tree identity
- full capture identity와 attempt ID
- stage/strategy
- workload/cohort/repetition/tick cardinality
- required input/tool/contract hashes

missing/malformed/mixed identity에서 standalone CLI는 machine-readable reject/Hold report와 semantic non-zero exit를 반환한다. standalone `ADMITTED/READY`는 supplied frozen context와의 내부 정합만 의미하며 live repository/cohort attestation이나 terminal authority가 아니다. final manifest의 canonical recomputation은 계속 최종 권한이다.

#### C3. bounded finalization과 TOCTOU

보장 범위를 “임의 외부 편집이 불가능함”이 아니라 “각 named checkpoint에서 관찰한 identity가 exact equality가 아니면 성공 불가”로 정의한다. checkpoint 사이 변경 후 원복되는 ABA mutation과 마지막 checkpoint 뒤 외부 수정은 탐지 보장 밖이다.

1. official performance와 E0 characterization은 `/mnt/d/J2M/evidence/.locks/cleanup-s3-performance.lock`의 machine-wide nonblocking cooperative `flock`을 사용한다. authorized build/warm-up/calibration/official/retry 전체에 적용한다. per-worktree lock은 보조 수단일 뿐 이 요구를 대체하지 않는다. owner metadata, signal/exit release, stale file semantics, provisional manifest 생성 순서와 contention stage/reason을 v5 matrix에 고정한다.
2. v5 lifecycle에 non-authoritative `FINALIZING` candidate를 추가한다. 모든 check 전에는 `FINAL/PASS`를 쓰지 않는다.
3. 순서는 `FINALIZING candidate -> runner pre-final authorization check -> finalizer의 마지막 live HEAD/worktree/runtime identity check -> authoritative FINAL 단일 atomic emit -> post-final bytes/schema/hash readback -> terminal line`으로 고정한다.
4. 마지막 live check까지 green일 때 canonical finalizer가 authoritative `FINAL/PASS|DEFERRED|HOLD`를 단 한 번 발행한다. 그 전 failure는 `FINAL/HOLD`와 non-zero exit만 발행한다. post-final readback은 live repository를 다시 attest하거나 FINAL을 downgrade하지 않고 artifact bytes/schema/hash 손상만 infrastructure failure로 보고한다.
5. 마지막 atomic emit 이후 repository mutation은 보장 범위 밖이다. 이를 포함하려면 manifest와 별도의 authoritative terminal-attestation carrier를 새로 설계·승인해야 한다.
6. `REPOSITORY_IDENTITY_CHANGED` 같은 신규 reason은 v5 registry 승인 뒤에만 사용한다.
7. executable/build payload hash와 player metrics hash는 실제 실행 bytes에 계속 결합한다.
8. cooperative lock은 임의 editor/shell을 막지 못하며 전역 mutation 방지를 주장하지 않는다.

#### tests first

- temp repository에서 tracked dirty, untracked dirty, HEAD change를 각각 preflight rejection으로 고정한다.
- missing capture identity, invalid revision, mixed attempt/cohort가 Cleanup `ADMITTED` 또는 calibration `READY`가 되지 않음을 검증한다.
- authoritative emit 전 각 named checkpoint에 persistent tracked/untracked mutation을 주입하고 terminal Hold, non-zero exit, preserved final artifact를 검사한다. post-final에는 artifact bytes/schema/hash tamper만 readback failure로 검사하고, repository ABA 및 last-emit 이후 mutation은 한계 test/문서로 고정한다.
- concurrent official runner 두 개 중 하나만 lock을 소유하고 다른 하나는 명시적 Hold/abort artifact를 남기는 fixture, lock path가 clean status에 나타나지 않는 fixture, crash/signal lifecycle fixture를 추가한다.
- unsafe output alias/symlink와 atomic-write 기존 adversarial coverage를 유지한다.

#### 완료 조건

- dirty authorized-cohort worktree는 build 전에 닫히며 재현 가능한 terminal artifact가 있다.
- standalone success는 supplied context 내부 정합으로 한정되고 terminal success와 혼동되지 않는다.
- 각 named checkpoint에서 관찰된 mismatch가 PASS/DEFERRED로 운반되지 않는다.
- 외부 mutation 완전 방지라는 과도한 claim이 없다.

### Package D — Evidence Contract v5와 독립 exact full-scan oracle

#### 승인 방식

v4를 silent edit하지 않는다. artifact/schema/harness identity가 바뀌므로 별도 `gameplay_cleanup_slice3_evidence_contract_v5.md`와 새 contract version을 제안한다. v1-v4 artifact는 historical이고 자동 승격하지 않는다.

먼저 v5 exact text, artifact-version matrix, workload semantic source, oracle exact bytes/digest, parser API와 review vectors를 초안으로 만든다. **Hard Pause D1 — 사용자 contract-design 승인**은 vocabulary/bytes 설계만 승인한다. 그 뒤 exact v5 implementation allowlist와 validation lane을 **Hard Pause I2 — v5 tests/implementation-scope 승인**으로 별도 승인받아야 tests-first red와 구현을 시작할 수 있다. exact integrated diff를 다시 검토한 **Hard Pause D2 — activation 승인** 전에는 terminal PASS/DEFERRED를 열지 않는다.

#### artifact-version matrix

v5 초안은 최소한 다음 carrier별 exact schema/version, required/forbidden fields, strict compatibility와 hash record order를 먼저 고정한다.

- preflight/captured KV와 frozen context
- Player metrics
- performance/Cleanup/calibration reports
- provisional/candidate/final attempt manifest
- workload semantic contract
- full-scan oracle artifact
- measurement authorization/campaign plan

oracle artifact/parser hash의 exact field name을 Probe command line부터 KV, metrics, report provenance, capture identity, manifest까지 모두 정의한다. contract version 숫자 하나만 올리는 방식은 허용하지 않는다.

#### frozen artifact 제안

- 경로: `Tools/contracts/gameplay_cleanup_slice3_full_scan_oracle_v1.json`
- parser: `Tools/gameplay_cleanup_slice3_full_scan_oracle.py`
- workload semantic source 제안: silent edit하지 않은 `Tools/contracts/gameplay_cleanup_slice3_workloads_v3.json`
- normative owner: Evidence Contract v5의 full-scan oracle 절. v3 workload semantic artifact가 base case, pre-tick restoration/schedule, cleanup recurrence와 measured ordinal 범위를 소유하고 oracle parser는 production counter/source를 import하지 않는 pure derivation/comparison만 수행한다.
- binding:
  - oracle schema/version
  - workload contract exact SHA-256
  - workload ID/seed/schedule hash/initial fingerprint
  - warmup/sample ticks와 repetition set
  - ordered per-tick actual/expected ordinal과 exact-expandable range
  - per-tick `fullScanInvocationCount`, `fullScanEntityVisitCount`, `survivorCopyCount`, `removalProcessedCount`
- independent derivation review 전 illustrative hypothesis이며 아직 승인된 expectation이 아닌 값:
  - target: measured tick마다 invocation 1, visits 256, survivors 256, removals 0
  - stress: schedule가 매 measured tick missing entities를 복원한 뒤 cleanup하는 현재 계약을 독립 계산해 invocation 1, visits 256, survivors 240, removals 16

v3 semantic source는 target base case와 stress recurrence(`previous live 240 -> missing 16 restore -> pre-cleanup 256 -> remove 16 -> survivor 240`), warmup 전제, repetition, measured ordinal 범위를 exact bytes로 직접 소유한다. 위 숫자는 그 artifact와 독립 derivation review가 승인된 뒤에만 normative expectation이 된다. tick별 값이 일정하다는 전제가 review에서 깨지면 range formula 대신 모든 tick의 exact expectation을 frozen artifact에 열거한다.

#### parser/comparison 규칙

- exact object members, types, domains, order/cardinality를 검증한다.
- duplicate keys, unknown fields, missing workload/repetition/tick, non-integral number를 거부한다.
- v5 metrics run은 ordered `perTickFullScanObservations`를 운반한다. 각 tuple은 `runKey`, `tickOrdinal`, invocation/visit/survivor-copy/removal-processed count를 가지며 tuple cardinality가 `executedTicks`와 exact equality여야 한다.
- actual tuple을 workload/repetition/tick identity에 exact join한 뒤 expected series와 비교한다. aggregate totals도 series에서 재계산해 serialized total과 exact equality를 요구한다. ordered-sequence hash만 운반하는 대안은 별도 contract 승인이 필요하다.
- `visits - survivors == removals` 같은 same-producer algebra는 보조 invariant일 뿐 oracle 대체물이 아니다.
- oracle artifact SHA와 parser SHA를 campaign plan, capture identity, attempt manifest, final manifest에 모두 결합한다.
- v5 finalizer만 v5 artifact를 인정하며 v4 input을 compatibility rejection한다.
- generic parity bool을 재사용하지 않는다. canonical validator가 `cleanupProcessorCommitOracleParityVerified`와 `fullScanExpectationVerified`를 각각 재계산하고 producer 값과 exact equality를 요구한다.

#### activation rule

`FULL_SCAN_EXPECTATION_UNAPPROVED` 제거는 아래 항목이 **같은 reviewed change**에 모두 있을 때만 가능하다.

1. 승인된 v5 normative text
2. frozen oracle exact bytes와 approved digest
3. strict parser/comparison
4. capture identity와 manifest binding
5. tests-first positive/negative fixtures
6. same-revision implementation validation
7. D2 exact integrated-diff activation approval

boolean, environment variable, CLI flag 하나로 success transport를 여는 경로는 금지한다. v5 구현과 D2 뒤에도 별도 approved `MeasurementAuthorization`/campaign-plan artifact가 없으면 terminal `HOLD_MEASUREMENT_AUTHORIZATION`만 허용한다. authorization artifact의 exact digest, campaign ID, attempt kind/ordinal/slot을 capture identity, KV, metrics, reports, manifest에 결합한다. allocation signal, clean identity, campaign identity, Measurement Goal은 누적 조건이며 Phase M1 승인 뒤에만 official PASS/DEFERRED가 가능하다.

#### negative matrix

- missing oracle/artifact hash
- wrong approved digest 또는 parser/tool hash
- duplicate/unknown/missing member
- workload ID/seed/schedule/fingerprint mismatch
- tick/repetition/cardinality mismatch
- expected value 1개 변조
- observed per-tick counter 1개 변조와 서로 다른 tick의 `+1/-1` 상쇄 변조
- mixed v4/v5 cohort 또는 stale campaign plan
- missing/forged MeasurementAuthorization
- persisted report와 canonical recomputation mismatch

#### 완료 조건

- actual producer run 전체가 independent exact expectation과 일치한다.
- 변조 matrix의 모든 경우가 machine-readable rejection/Hold와 non-zero semantic exit다.
- S3-EV-016 hard block은 D2까지 승인된 v5 경로에서만 대체되고 v1-v4에는 계속 적용된다.
- v5 구현 완료는 공식 capture 승인으로 해석되지 않는다.

### Package E — allocation signal과 workload provenance

#### E1. allocation characterization

새 공식 campaign 전에 release-like Mono Player에서 candidate signal을 별도 characterization한다. Player 실행과 새 evidence 생성을 허가하는 **Hard Pause E0 — characterization 실행 승인**을 먼저 받는다.

- forced allocation 0/4096/8192 bytes를 같은 thread/scope에서 실행한다. exact sample count/order, warmup, repetition, monotonic tolerance, noise/contamination ceiling, retry/exclusion rule, artifact/tool/build hash는 E0 승인 artifact에 사전 고정한다.
- candidate는 monotonically distinguishable, repeatable, exact sample cardinality, diagnostics on/off attribution을 보여야 한다.
- current-thread가 아니거나 frame-wide이면 포함 thread/interval과 contamination ceiling을 normative amendment에 명시한다.
- signal을 찾지 못하면 `Hold`를 유지한다. allocation gate를 structural-only signal로 조용히 낮추지 않는다.

동등 signal 채택은 **Hard Pause E1 — 사용자 normative amendment 승인** 뒤에만 가능하다.

#### E2. Wall provenance

현재 synthetic entities가 `EntityType.None`이면 문서와 report에서 `generic inert entity` workload로 표현한다. `wallCount`는 truthful provenance를 확보하기 전 `entityCount`의 별칭으로 사용하지 않으며, field 제거/rename은 새 schema에서만 수행한다.

v5 workload identity도 machine-readable하게 바꾼다. 제안 ID는 `cleanup-s3-target-generic-inert-v3`와 `cleanup-s3-stress-generic-inert-v3`이며 `wallCount`는 제거하거나 `inertEntityCount`로 exact rename한다. legacy `*-wall-*-v2` ID/workload contract는 v5 compatibility rejection하고 oracle, campaign, cohort identity를 새 ID/digest에 다시 결합한다. legacy ID를 섞거나 이름만 v3로 바꾼 fixture는 negative test로 거부한다.

authored Wall-heavy claim 또는 S3-B 진입 전에는 `authored stage-Wall source identity -> 기존 EntityType.None materialization -> Solid occupancy claim` provenance, factory/import path, candidate writer 및 snapshot/fast-import parity를 별도 pre-entry evidence로 증명한다. 새 `EntityType.Wall` 도입이나 authoritative schema 확대는 이 remediation의 non-goal이다.

### Package F — 사전 authority와 사후 상태 동기화

F1은 구현 전에 Goal Plan/Prompt에 bounded execution authority, runtime/evidence allowlist, forbidden scope와 validation lane을 dated amendment로 연결한다. F2는 각 approved package 구현 뒤 실제 결과와 remaining risk를 closeout synchronization한다. 과거 기록을 다시 쓰지 않고 최신 dated erratum을 추가한다.

다음 current-truth 문서는 상단 상태와 진입 조건을 같은 문구로 맞춘다.

- `Gameplay-Wall-Tick-Cost-Optimization-Plan.md`
- `Gameplay-Wall-Tick-Cost-Optimization-Slice3-Goal-Plan.md`
- `Gameplay-Wall-Tick-Cost-Optimization-Slice3-Goal-Prompt.md`
- Architecture README

historical post-amendment audit와 completed Evidence Remediation Goal Prompt의 상단 상태/당시 결론은 보존하고 최신 dated cross-reference/erratum만 추가한다.

필수 문구는 다음과 같다.

- current state는 `Hold — valid evidence incomplete`
- S3-EV-016은 allocation/Measurement Goal과 대안 관계가 아니라 누적 선행 gate다.
- exact parser/comparison, artifact+parser digest, capture identity, same-revision implementation이 oracle 승인 범위에 포함된다.
- historical v4 remediation closure는 당시 P0/P1 scope에 한정되며 이번 재감사 finding을 자동 폐쇄하지 않는다.
- 2026-08-28 “known P0/P1 closed”는 당시 finding set의 historical closure이고, 2026-08-29 `S3A-RR-*`는 구현·검증 전인 신규 gap이다.
- 공식 capture와 S3-B/S3-C는 별도 승인 전 forbidden이다.

F1은 implementation authority link와 proposed status를 검증하는 doc assertion/수동 review를, F2는 finding/status/validation result의 exact 동기화를 완료 조건으로 둔다. D2 전에는 v5를 항상 `Draft/Proposed`로 표시한다.

## 5. 실행 순서와 hard pauses

| Phase | 작업 | 종료 조건 | 다음 단계 권한 |
|---|---|---|---|
| 0 | 이 수정안 작성과 세 관점의 논리 재검토 | High 반론을 본 문서에 반영 | 완료; 구현 권한 없음 |
| 1 | F1 bounded authority amendment와 B0 membership vocabulary 제안 | exact allowlist/forbidden/lanes 및 B-Raw 또는 alternative 결정 | **Hard Pause I1 — implementation-scope 승인** |
| 2 | 승인된 A/B/C current-contract red와 “v4 S3-EV-016 block 유지” regression을 `/mnt/d/J2M/evidence/<attempt>/red/`에 보존 | 각 current finding이 예상 이유로 실패 | 승인 범위 구현 가능 |
| 3 | Package A, 승인된 B, v4-compatible C 구현 | reference sub-contract, membership/processing, current identity adversarial green | v5 초안 가능 |
| 4 | v5 artifact-version matrix, semantic workload source, oracle bytes/digest, parser API, reason registry, review vectors 초안 | independent review 완료 | **Hard Pause D1 — contract-design 승인** |
| 5 | approved v5 design에 맞춘 exact implementation allowlist/lanes amendment 작성 | D/C-v5 변경 경계 합의 | **Hard Pause I2 — v5 tests/implementation-scope 승인** |
| 6 | 승인된 v5 vocabulary로 D 및 v5-only C lifecycle positive/negative tests를 red 작성 | authorization 없음/상쇄 변조/identity mutation이 정확히 실패 | v5 구현 가능 |
| 7 | v5 fail-closed implementation; terminal success는 계속 잠금 | same-revision matrix green, authorization 없이는 Hold | **Hard Pause D2 — exact diff activation 승인** |
| 8 | E0 characterization plan/artifact exact bytes와 digest 작성 | sample/order/threshold/hash 고정 | **Hard Pause E0 — 외부 실행 승인** |
| 9 | 승인된 E0 artifact로 allocation characterization 실행 | valid candidate 또는 explicit Hold 결론 | **Hard Pause E1 — signal adoption 승인** |
| 10 | cumulative focused/core/replay/UI/capture-build 판단, 독립 재감사, F2 문서 동기화 | touched-cluster 회귀 없음, remaining risks 명시 | repository는 여전히 Hold 가능 |
| 11 | proposed MeasurementAuthorization/campaign-plan exact bytes와 digest를 포함한 별도 S3-A Measurement Goal 제안 | exact proposal review 완료 | **Hard Pause M1 — exact digest 승인 후에만 official capture** |

공식 capture가 PASS하더라도 S3-B를 자동 시작하지 않는다. 결과 리뷰와 별도 continuation 승인이 필요하다.

## 6. 변경 allowlist와 금지 범위

### 예상 allowlist

- Runtime diagnostics:
  - `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/CleanupSlice3Diagnostics.cs`
  - B0에서 effective view를 승인하거나 counter rename이 해당 파일에 필요할 때만 `Assets/_Features/Gameplay/Gameplay_Cleanup/Runtime/RemovalProcessor.cs`
  - `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/CleanupSlice3AttributionSimulationTests.cs`
  - 새 C# test/helper와 Unity가 생성하는 `.meta`
- Capture/UI bridge:
  - v5 identity/schema 전달에 필수인 `Assets/_Features/UI/UI_Composition/Runtime/GameplayPerformancePlayerProbe.cs`
  - `Assets/_Features/Stages/Editor/Capture/PlayerProfilerCaptureCli.cs`
  - 현재 관련 fixture `Assets/_Features/Stages/Editor/Tests/PlayerCaptureLaunchBootstrapSafetyTests.cs`, `Assets/_Features/Stages/Editor/Tests/StageDefaultStageIdPolicyTests.cs` 및 I1/I2에서 exact path로 승인할 신규 capture-build fixture
- Evidence:
  - `Tools/gameplay_cleanup_slice3_admission.py`
  - `Tools/gameplay_cleanup_slice3_calibration.py`
  - `Tools/gameplay_cleanup_slice3_evidence_manifest.py`
  - shared strict envelope/oracle helper와 해당 `Tools/tests/`
  - `Tools/gameplay_evidence_v4.py`의 v5/shared 후계
  - `Tools/gameplay_performance_admission.py`
  - `Tools/gameplay_performance_campaign.py`
  - `run_tests.sh`
- Contract/docs:
  - proposed v5 contract, silent edit하지 않은 workload semantic contract, full-scan oracle artifact/parser
  - 이 문서가 지정한 Slice 3 문서, Architecture README, 새 lane이 생길 경우 Testing Guide

### 금지 범위

- `CleanupProcessor`, `WorldState`, Tick semantic order의 production 변경
- S3-B candidate index/writer 또는 S3-C indexed executor 구현
- Scene/Prefab/ScriptableObject/asset 변경
- 기존 preserved evidence 재작성
- 사용자 소유 untracked file의 삭제·이동·stage
- 공식 capture, commit, push

allowlist 밖 변경이 필요하면 작업을 중단하고 범위와 이유를 다시 승인받는다.
이 절은 proposal 예상 범위다. 실행 권한으로 쓰는 F1/I1 및 D1/I2 amendment는 모든 기존 파일 exact path와 신규 파일 directory/name pattern을 닫아야 한다.

## 7. 검증 matrix

### 정적·Python

- `python3 -m py_compile` on changed Python tools/tests
- 기존 S3-A evidence 7개 module과 신규 oracle/envelope module
- `bash -n run_tests.sh`
- `git diff --check`
- relative Markdown link와 frozen digest exact check

### Unity

- focused scenario fixture 전체를 실제 실행하는 `./run_tests.sh full --filter Game.Feature.Gameplay.Tests.Scenario.CleanupSlice3AttributionSimulationTests`
- `./run_tests.sh core`
- authoritative semantics 또는 replay carrier가 바뀌면 `./run_tests.sh --integration-replay --filter TickReplayDeterminismTests`
- v5는 `GameplayPerformancePlayerProbe.cs` identity/schema 전달을 바꾸므로 `./run_tests.sh ui`
- always-compiled producer test와 동일 define의 dedicated capture-build facade/Probe/CLI compile-smoke를 모두 실행한다. UI lane만으로 conditional capture code 실행을 증명하지 않는다.

테스트 이름 filter가 0개를 실행하면 성공으로 인정하지 않는다. `core --filter`로 Scenario fixture 실행을 대체하지 않는다.

### adversarial independent re-audit

- capture-build facade/Probe/CLI가 실제 생성한 C# producer JSON -> Python admission -> calibration -> manifest terminal chain
- dirty tracked/untracked, HEAD change, tool/artifact hash mutation
- missing/forged/mixed identity와 cohort
- oracle expectation/counter/parser mutation
- per-tick `+1/-1` 상쇄 변조와 missing/forged MeasurementAuthorization
- persisted report mutation, success exit/status forgery
- finalization hook별 repository mutation, lock path/contention/crash/signal lifecycle와 ABA 보장 한계

### 명시적 not-run

수정 package 구현 전인 이 제안서 단계에서는 Unity lane, official gameplay-performance capture, full broad lane을 실행하지 않는다. 문서-only 검증 결과를 runtime green으로 표현하지 않는다.

## 8. 전체 acceptance criteria

다음 항목이 모두 충족돼야 이 remediation 구현을 완료로 판정할 수 있다.

1. actual C# producer의 각 measured run이 exact reference invocation cardinality와 zero mismatch를 가진다.
2. B0에서 raw membership과 actual processing vocabulary가 승인되고, overlap matrix가 그 계약과 removal precedence를 정확히 증명한다.
3. dirty authorized-cohort worktree와 malformed standalone identity가 build/success 전에 fail-closed된다.
4. 각 named finalization checkpoint에서 관찰된 repository identity mismatch가 PASS/DEFERRED로 운반되지 않으며 ABA/last-check 이후 한계를 명시한다.
5. 승인된 frozen oracle이 workload/repetition/tick별 full-scan exact expectation을 독립 제공하고 모든 identity/hash에 결합된다.
6. same-producer algebra만으로 oracle parity를 주장하지 않는다.
7. base CleanupProcessor commit oracle을 whole `RunCleanupPhase` parity라고 부르지 않고 두 validator-derived parity field를 분리하며, auxiliary expiry를 포함한 capture on/off 결합 fixture가 별도로 green이다.
8. valid allocation signal이 없으면 Hold가 유지된다.
9. v5 generic-inert workload ID/field가 machine-readable하고 legacy wall-named v2 identity는 compatibility rejection되며, generic inert workload를 authored Wall-heavy workload로 표현하지 않는다.
10. focused/core 및 risk-triggered replay/UI/capture-build 결과와 not-run 항목을 분리 보고한다.
11. v1-v4 historical artifact를 v5 success evidence로 자동 승격하지 않는다.
12. F1/F2 문서가 신규 finding, authority, proposed/normative 상태와 validation 결과를 exact 동기화한다.
13. remediation 완료와 Measurement-ready, official S3-A terminal result, S3-B 진입을 분리한다.

어느 항목이라도 불완전하면 repository state는 `Hold — valid evidence incomplete`다.

## 9. 논리 재검토 반영과 추적성

초안 작성 뒤 docs/authority, runtime/StrongContract, evidence/fail-closed 관점의 독립 서브 에이전트 재검토를 수행했다. 세 검토 모두 파일은 수정하지 않았고, 다음 High 반론을 이 문서에 반영했다.

- implementation authority를 tests보다 먼저 두는 I1
- B raw membership과 actual processing 충돌을 닫는 B0
- timing과 structural+reference companion phase 분리
- conditional capture facade를 대신할 testable producer seam
- per-tick actual carrier와 independent workload semantic owner/formula
- v5 artifact-version matrix, D1 design 승인과 D2 activation 승인
- MeasurementAuthorization 없이는 성공 불가
- clean official scope, v4/v5 reason registry 경계, strict standalone context
- non-authoritative FINALIZING lifecycle, lock 위치와 finite-checkpoint 한계
- existing `EntityType.None` materialization을 보존하는 Wall provenance

반영 후 같은 세 관점으로 2차 closure audit을 수행했고, 여기서 발견된 official-clean attempt-kind 우회, FINAL 단일 emit 순서, conditional capture path 선택 검증, D implementation authority, E0/M1 승인 순환, machine-wide lock scope와 generic-inert identity 문제도 재보정했다. 보정 뒤 targeted closure check에서 docs/authority, runtime/StrongContract, evidence/fail-closed 세 관점 모두 새 High/Medium 없이 `proposal-level GO`를 판정했다. 모든 에이전트 검토는 파일 수정 없이 수행됐다.

| Finding | Contract owner | Red fixture | Green proof | Global AC |
|---|---|---|---|---|
| `S3A-RR-001` | A | actual producer reference count 0/short | per-run measured companion count exact | 1, 7 |
| `S3-EV-016` | D | v4 block, per-tick/mixed/hash mutation | approved series exact join + canonical recompute | 5, 6, 11 |
| `S3A-RR-002` | B0/B | overlap boundary matrix | approved membership/processing matrix | 2 |
| `S3A-RR-003` | C1/D | dirty authorized-cohort temp repo | pre-build terminal Hold | 3 |
| `S3A-RR-004` | C2 | missing/malformed frozen context | standalone reject + final recompute | 3 |
| `S3A-RR-005` | C3/D | hook mutation/lock lifecycle | named-checkpoint Hold + canonical final | 4 |
| `S3A-RR-006` | A | generic parity/auxiliary overclaim | scoped commit parity + scenario/replay parity | 1, 7 |
| `S3A-RR-007` | F1/F2 | authority/status doc assertion | dated erratum과 index 동기화 | 12 |
| allocation Hold | E1 | forced-allocation liveness | approved signal 또는 explicit Hold | 8 |
| `S3-EV-010` | E2 | false Wall provenance fixture | authored source→None→Solid carrier | 9 |

## 10. 완료 상태 vocabulary

- `Reaudit remediation complete — approved S3A-RR packages implemented and verified; allocation result=<valid|unavailable>; official capture not run; repository remains Hold unless all Measurement-ready gates pass`
- `Measurement-ready — v5 approved/activated, valid allocation signal, clean worktree, new campaign identity, exact MeasurementAuthorization/campaign plan and Measurement Goal approved`
- `S3-A PASS/DEFERRED/HOLD — official Measurement Goal의 trustworthy terminal result`
- repository Slice 3 완료는 별도 retain/reject closeout 뒤에만 판정

## 11. 제안 commit split

실제 구현 승인을 받은 경우에도 한 commit에 섞지 않는다.

1. `test: Gameplay/Performance - S3-A 증거 계약 실패 경로 고정`
2. `fix: Gameplay/Performance - S3-A reference 계측과 후보 vocabulary 정합`
3. `chore: Testing/Performance - S3-A identity와 clean finalization 강화`
4. `docs: Gameplay/Performance - S3-A v5 오라클 계약과 재개 게이트 고정`
5. 필요 시 `chore: Testing/Performance - S3-A 승인 오라클 검증 구현`

각 commit은 `AI_GIT_COMMIT_RULES.md` 형식과 tests/not-run evidence를 따른다. 이 제안서 작성은 commit 또는 push를 승인하지 않는다.

## 12. 현재 판정

- S3-A: `Hold — valid evidence incomplete`
- S3-EV-016: open, v4 hard block 유지
- allocation signal: unresolved
- S3-B/S3-C: forbidden
- 논리 재검토: 세 관점 최종 closure 완료, 새 High/Medium 없음, `proposal-level GO`
- 다음 허용 작업: 외부 계산 exact amendment SHA-256과 source HEAD/branch를 포함한 I1 implementation-scope 승인 요청

## 13. 2026-08-29 KST — Proposed F1/B0 handoff

Historical pre-I1 record: [S3-A F1/B0 Amendment](./Gameplay-Wall-Tick-Cost-Optimization-Slice3-S3A-F1-B0-Amendment.md)는 세 관점 independent closure와 docs-only static validation을 완료해 `Proposed — awaiting I1 implementation-scope approval`에서 멈췄다. 이후 exact I1 승인에 따라 B-Raw 및 bounded A/B/C 구현·검증 closure가 완료됐다. v5, allocation resolution, official capture, S3-B/S3-C, commit 또는 push는 승인되지 않았고 Repository Slice 3는 계속 `Hold — valid evidence incomplete`다.

## 14. 2026-08-29 KST — D1/E0/I3 authority and measurement erratum

[D1/E0/I3 권한 및 측정 복구 Amendment](./Gameplay-Wall-Tick-Cost-Optimization-Slice3-S3A-D1-E0-I3-Amendment.md)는 I1 이후 최신 evidence 재검토를 반영한다. Latest non-official smoke의 immediate allocation rejection은 하나지만, 같은 raw timing은 canonical formula에서 `335.781533%` noise를 만들며 allocation만 고쳐도 `HOLD_INVALID_SIGNAL`이 된다.

이 erratum은 §5 Phase 8~10 사이의 누락된 implementation authority를 보정한다. E0는 external characterization 실행, E1은 normative signal/procedure 채택만 승인한다. Diagnostic harness 변경은 `I3-pre`, 채택된 PlayerProbe/runner/validator/calibration/manifest 변경은 `I3`, integrated success path는 `D3/E2` exact diff activation 승인이 추가로 필요하다. E0/M1 설정값 선택으로 source 또는 evidence semantics 변경을 숨기지 않는다.

이 amendment는 proposed docs-only correction이며 D1/I2/D2, E0/E1, I3/D3, official capture, commit/push 또는 S3-B/S3-C를 승인하지 않는다. 기존 §5의 직렬 계약은 amendment가 별도로 승인되기 전까지 더 엄격한 해석으로 유지한다.

§12의 “다음 허용 작업: I1 승인 요청”은 historical pre-I1 상태이며 §13의 completed I1 handoff와 이 §14가 supersede한다. Current next step은 새 amendment의 exact P2 identity review이고, 그 승인 전에는 D1/E0-D design drafting을 새 실행 권한으로 간주하지 않는다.

## 15. 2026-08-29 KST — P2 design execution and D1/E0-D hard pause

사용자는 amendment SHA-256 `fbdd1c3d7e74acb92e4924b1d522a9dc881e9bf64c3fc8822a4437ef60f54a70`를 source `d0310f8b99589157e82f2fe42cb5bd5aab2b6c28 / codex/third-party-license-inventory`에서 승인했다. P2 범위로 [Evidence Contract v5 draft](../../Tools/contracts/gameplay_cleanup_slice3_evidence_contract_v5.md), generic-inert workload v3, independent full-scan oracle v1, D1 review vectors와 E0 characterization protocol v1을 작성했다. Source/test/tool 구현이나 Player/evidence 실행은 수행하지 않았다.

세 관점 독립 재검토는 workload의 전체 entity canonical template와 exact schedule expansion, measured engine tick/steady recurrence, signed authorization trust root, finite slot/campaign terminal ledger, build-once Player binding과 E0 diagnostic-only terminal을 추가로 요구했다. Proposed package는 이를 반영했고, 실제 public key는 등록하지 않았다. 따라서 현재 open gate는 D1 exact four-artifact approval과 별도 E0-D protocol approval이며, 이후에도 I2/D2, I3-pre/E0/E1, K1/I3/D3, clean F2와 M1이 누적된다.

이 §15가 §14 마지막 문장의 P2 next-step 표현을 supersede한다. Repository Slice 3는 `Hold — valid evidence incomplete`; S3-EV-016, allocation liveness, timing stability와 MeasurementAuthorization gate는 모두 open이고 S3-B/S3-C는 금지된다.
