# Slice 3 S3-A D1/E0/I3 권한 및 측정 복구 Amendment

- 상태: `Proposed — awaiting exact design/implementation approvals`
- 작성일: 2026-08-29 KST
- source revision: `d0310f8b99589157e82f2fe42cb5bd5aab2b6c28`
- branch: `codex/third-party-license-inventory`
- repository Slice 3: `Hold — valid evidence incomplete`
- 선행 상태: I1 bounded v4 A/B/C remediation 완료
- 이 문서가 승인하지 않는 것: runtime/tool 구현, Player 실행, evidence 생성, official capture, commit, push, S3-B/S3-C

## 1. 목적

이 amendment는 I1 종료 뒤 재검토에서 확인된 다음 세 가지를 현재 실행 계획에 반영한다.

1. allocation liveness `expectedAtLeast=4096 observed=0`의 안전한 characterization 절차;
2. 최신 non-official smoke raw timing에서 계산된 `335.781533%` calibration noise의 falsification 및 안정화 절차;
3. E0 실행 승인과 E1 normative 채택만으로는 실제 Probe/runner/tool 변경 권한이 생기지 않는 implementation-authority gap.

이 문서는 Evidence Contract v5의 exact normative bytes, oracle bytes/digest 또는 MeasurementAuthorization을 승인하지 않는다. D1/I2/D2, E0/E1, I3/D3, M1은 각각 독립 hard pause이며 한 단계의 승인을 다음 단계 권한으로 확대 해석하지 않는다.

## 2. Contract 분류

### StrongContract — 변경 금지

- Cleanup 순서는 `Removal -> Timer -> Transition`이다.
- authoritative state, occupancy, lifetime, `CleanupPhaseResult`, EventLog, FinalEntities, phase trace, determinism hash와 replay 결과는 diagnostics/capture on/off에서 동일해야 한다.
- diagnostics, reference oracle, allocation/timing probe와 evidence tooling은 authoritative simulation을 변경하지 않는다.
- exact identity, artifact hash, cohort와 canonical recomputation이 불완전하면 성공으로 승격하지 않는다.
- allocation/timing evidence를 구조 counter 또는 self-authored bookkeeping으로 대체하지 않는다.

### CurrentPolicy — 승인된 계약 안에서 변경 가능

- production Cleanup executor는 full scan이다.
- ReleaseLike capture backend는 현재 Windows Mono다.
- timing repetition은 현재 한 coroutine step 안에서 fixed slot order로 동기 실행된다.
- v4 allocation wire는 `GC.GetAllocatedBytesForCurrentThread` 기반 current-thread delta다.
- v1-v4 success transport는 `FULL_SCAN_EXPECTATION_UNAPPROVED`로 계속 잠긴다.

## 3. 재검토 관찰

### 3.1 Allocation

- latest non-official smoke의 `allocationCounterProbeBytes`는 `0`이다.
- 4개 allocation phase의 100개 sample은 모두 `0`이지만 positive-control probe가 실패했으므로 zero-allocation 증거로 사용할 수 없다.
- current Probe는 `new byte[4096]`, write, `GC.KeepAlive`를 수행한다. 단순 상수 대입이나 dead-code 제거만으로 결과를 설명할 수 없다.
- current `Math.Max(0, after - before)`는 reset 또는 음수 delta를 `0`으로 접을 수 있으므로 characterization artifact는 clamp 전 signed delta를 보존해야 한다.

### 3.2 Timing

최신 target workload의 3개 repetition은 다음과 같다.

| metric | repetition 1 | repetition 2 | repetition 3 |
|---|---:|---:|---:|
| Cleanup median ms | 0.080500 | 0.081200 | 0.080700 |
| Cleanup p95 ms | 0.109995 | 0.666385 | 0.165700 |
| Cleanup p99 ms | 0.695303 | 0.731274 | 0.741161 |
| whole tick p95 ms | 2.630935 | 2.596915 | 2.589470 |

Median과 p99 slow class는 유사하지만 p95만 크게 갈린다. 200개 표본의 p95 경계에서 약 `0.7ms` slow sample 수가 repetition별로 달라지는 tail-frequency 문제다. canonical calibration 식의 `335.781533%`는 계산 오류가 아니며 `10%` ceiling을 초과한다.

현재 warm-up과 measured timing은 fixed repetition slot을 한 프레임에서 동기 실행한다. GC, OS preemption 또는 render/job pressure가 특정 slot에 편향될 수 있지만 raw per-tick timestamp/GC 관찰이 없으므로 원인은 아직 확정하지 않는다.

종료 뒤 ComputeBuffer/GraphicsBuffer 및 JobTempAlloc 경고는 이미 기록된 timing의 직접 원인으로 간주하지 않는다. 별도 resource-hygiene 조사에서 실행 중 pressure와의 상관을 입증해야 한다.

### 3.3 Authority gap

기존 E0는 external Player 실행과 evidence 생성을, E1은 동등 signal의 normative 채택을 승인한다. 다음 변경은 어느 승인에도 자동 포함되지 않는다.

- forced-allocation probe 또는 raw signed carrier 변경;
- timing region, clock, thread, phase ordering 또는 repetition isolation 변경;
- warm-up, sample count, retry/exclusion 또는 percentile transport 변경;
- PlayerProbe, build CLI, runner, validator, calibration, manifest 변경;
- signal/backend identity와 contamination ceiling 추가.

따라서 characterization-only 구현을 위한 `I3-pre`, 채택된 measurement chain 구현을 위한 `I3`, exact integrated diff activation을 위한 `D3/E2`가 필요하다.

## 4. Corrected hard-pause sequence

| Phase | 작업 | 산출물 | Hard pause |
|---|---|---|---|
| P2 | 이 amendment와 current-truth link 작성 | exact 문서 bytes/SHA, no runtime mutation | amendment identity review |
| D1-D | v5 schema/version matrix, generic-inert workload v3, independent full-scan oracle bytes/digest, parser API, reason registry, signed MeasurementAuthorization/campaign-ledger schema와 review vectors 작성 | exact design package | **D1 contract-design 승인** |
| I2-D | D1 구현 exact path/new-file allowlist, tests-first red matrix와 validation lane 작성 | implementation amendment | **I2 구현범위 승인** |
| I2 | tests-first red 뒤 v5 fail-closed 구현·same-revision 검증 | red/green evidence와 integrated diff | **D2 activation 승인** |
| E0-D | allocation/timing characterization protocol, sample/order/threshold/hash exact bytes 작성 | canonical E0 artifact/digest | **I3-pre 필요성 판정** |
| I3-pre | E0 실행에 새 diagnostic harness가 필요할 때 exact allowlist로 구현하고 success transport와 분리 | diagnostic-only diff/tests | **diagnostic activation 승인** |
| E0 | 승인된 E0 artifact로 external characterization 실행 | valid candidate 또는 explicit Hold | **E1 normative adoption 승인** |
| K1 | M1 detached signature 검증용 Ed25519 public-key trust-root exact bytes/digest 등록 | one exact active public key; private key는 repo에 저장하지 않음 | **K1 trust-root 승인** |
| I3-D | 채택된 signal/timing procedure를 production measurement chain에 반영할 exact scope 작성 | adoption implementation amendment | **I3 구현범위 승인** |
| I3 | tests-first 구현, K1 trust-root digest를 포함한 full canonical chain과 adversarial 검증 | exact integrated diff/validation | **D3/E2 activation 승인** |
| F2 | current truth, independent audit, approved commit/clean revision | fixed HEAD/runtime/tool/oracle hashes | commit은 별도 승인 |
| M1 | exact MeasurementAuthorization/campaign plan 및 S3-A Measurement Goal | exact bytes/digest/campaign ID | **M1 official capture 승인** |

기존 계획의 `D1 -> I2 -> D2 -> E0 -> E1` 직렬 순서는 유지한다. E0 설계 초안은 D1과 병렬 검토할 수 있지만 Player 실행이나 implementation은 앞 단계 hard pause를 건너뛸 수 없다. Allocation risk를 먼저 실행해 확인하려면 이 순서를 바꾸는 별도 exact amendment가 필요하다.

## 5. E0 allocation characterization contract

### 5.1 Primary adoption candidate

ReleaseLike Windows Mono와 현재 API를 유지한 control을 먼저 사용한다.

- exact ordered `0 / 4096 / 8192` retained allocation controls;
- before/after raw counter, signed delta, managed thread ID;
- allocation object를 after-read 이후까지 opaque sink에 retain;
- 사전 고정한 warm-up, repetition, sample cardinality;
- monotonic separation, repeatability와 no-op contamination ceiling;
- tool/source/build/backend hashes와 unique attempt identity;
- 즉흥 size 증가, retry 또는 sample 제외 금지.

Allocator context보다 큰 retained batch가 필요하면 E0 exact artifact에 크기와 순서를 먼저 고정한다. 실행 결과를 본 뒤 크기를 바꾸면 새 artifact digest와 새 attempt가 필요하다.

### 5.2 Diagnostic controls — silent adoption 금지

- ReleaseLike IL2CPP + 동일 API: Mono runtime 원인 분리 전용이다. Backend identity가 없는 v4 official cohort에 섞지 않는다.
- ReleaseLike profiler counter: marker availability와 thread/frame contamination characterization 전용이다.
- Development Mono profiler counter: instrumentation availability control일 뿐 release-like official signal 후보가 아니다.
- custom counter: sequencing 확인에는 사용할 수 있으나 독립 allocation signal로 채택하지 않는다.
- `GC.GetTotalMemory` 또는 live/used heap counter: per-tick cumulative allocation과 동등하지 않으므로 채택하지 않는다.

후보 신호가 current-thread가 아니거나 frame-wide이면 포함 thread/interval, background contamination, subtraction 금지와 ceiling을 E1 normative bytes에 명시한다. 어떤 후보도 monotonic/repeatable하지 않으면 Hold를 유지한다.

## 6. E0 timing falsification contract

Characterization artifact는 official metrics schema와 분리된 diagnostic-only artifact로 다음 raw tuple을 보존한다.

```text
workloadId, repetition, tickOrdinal, executionSlot, phase,
monotonicTimestamp, managedThreadId,
cleanupProcessorTicks, runCleanupPhaseTicks, wholeTickTicks,
gcCollectionCount0Before/After, gcCollectionCount1Before/After,
gcCollectionCount2Before/After
```

실험 순서는 고정한다.

1. current fixed order를 fresh Player UUID로 반복해 slow tail이 instance 또는 execution slot을 따르는지 확인한다.
2. diagnostic-only rotating order `123 / 231 / 312`로 slot bias와 workload-instance bias를 분리한다.
3. timing warm-up→timing sample, capture-off warm-up→capture-off sample의 phase-local warm-up을 current mixed warm-up과 비교한다.
4. repetition별 fresh process 또는 one-sample-per-frame isolation과 current one-frame tight loop를 비교한다.
5. explicit GC-before-phase/NoGC는 원인 판별 control로만 사용하고 official stabilization으로 채택하지 않는다.
6. shutdown leak validation과 graphics/job-light control은 별도 artifact로 실행하고 in-window raw timestamp 상관 없이 timing 원인으로 판정하지 않는다.

### 6.1 Adoption rules

- `10%` maximum noise ceiling을 완화하지 않는다.
- 결과 확인 뒤 valid repetition을 제외하거나 warm-up으로 재분류하지 않는다.
- p95를 median/p99 또는 유리한 percentile로 교체하지 않는다.
- noise에 따라 retry하지 않는다.
- sample count/order/warm-up/repetition isolation을 바꾸면 E1 계약과 새 campaign identity에 사전 고정한다.
- 우선 채택 후보는 phase-local warm-up과 independent/fresh-process repetition이다. 실제 선택은 E0 raw evidence가 결정한다.

## 7. Future exact implementation candidates

이 목록은 review inventory이며 현재 write authority가 아니다. I2/I3 amendment는 실제 변경 대상만 exact path로 좁혀야 한다.

- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/CleanupSlice3PlayerCalibrationCore.cs`
- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/CleanupSlice3Diagnostics.cs`
- `Assets/_Features/UI/UI_Composition/Runtime/GameplayPerformancePlayerProbe.cs`
- `Assets/_Features/Stages/Editor/Capture/PlayerProfilerCaptureCli.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/CleanupSlice3AttributionSimulationTests.cs`
- 관련 capture CLI/UI fixture의 exact existing path와 승인된 신규 test/helper path
- `Tools/gameplay_cleanup_slice3_admission.py`
- `Tools/gameplay_cleanup_slice3_calibration.py`
- `Tools/gameplay_cleanup_slice3_evidence_manifest.py`
- v5 strict context/oracle helper와 exact paired tests
- `run_tests.sh`
- 새 contract/workload/oracle/authorization artifacts와 paired docs

Production `CleanupProcessor`, `TickPipeline`, `WorldState`, `WorldSnapshot`, Scene, Prefab, ScriptableObject, S3-B writer/index와 S3-C executor는 이 amendment에서 forbidden이다.

## 8. Tests-first 및 validation requirements

I2/I3 구현은 다음 red를 먼저 보존한다.

- current tail triplet이 exact `335.781533`, `HOLD_INVALID_SIGNAL`, `thresholds=null`을 만드는 fixture;
- 어떤 repetition도 결과 기반으로 제외되지 않는 fixture;
- allocation signed negative/reset, zero probe, monotonic 0/4096/8192와 tamper fixture;
- backend/signal/thread/interval identity missing 또는 mixed cohort rejection;
- oracle missing/wrong digest, per-tick mutation, `+1/-1` 상쇄 mutation;
- missing/forged MeasurementAuthorization과 stale campaign plan;
- E0 diagnostic artifact가 official evidence로 승격되지 않는 transport fixture.

구현 뒤 최소 validation은 다음과 같다.

- changed Python compile/unit suites;
- focused `CleanupSlice3AttributionSimulationTests`와 exact nonzero count;
- `./run_tests.sh core`;
- `GameplayPerformancePlayerProbe.cs` 또는 capture bridge 변경 시 `./run_tests.sh ui`;
- same define capture-build compile + non-official full canonical chain;
- semantics/replay carrier가 바뀐 경우에만 targeted replay;
- `bash -n run_tests.sh`, `git diff --check`, exact allowlist scan;
- external E0/official capture는 각각 E0/M1 승인 전 실행 금지.

Broad unfiltered full을 실행하지 않으면 baseline red와 함께 not-run 이유를 기록한다. Focused/core/smoke 결과를 project-wide green으로 표현하지 않는다.

## 9. Approval semantics

이 문서의 작성·검증은 다음을 의미하지 않는다.

- D1 exact v5 design 승인;
- I2/I3 implementation 승인;
- D2/D3 success activation 승인;
- E0 external execution 또는 E1 signal 채택 승인;
- commit/clean cohort 생성 승인;
- M1 official capture, S3-B 또는 S3-C 승인.

각 hard pause는 대상 문서/artifact의 exact SHA-256, source HEAD/branch, scope 항목을 명시한 사용자 승인만 인정한다. 모호한 “진행”을 다음 단계 권한으로 재사용하지 않는다.

## 10. 현재 판정

- Gameplay StrongContract: unchanged
- production Cleanup CurrentPolicy: full scan retained
- allocation: unresolved, current Mono probe liveness invalid
- timing: unresolved, latest raw metrics imply `HOLD_INVALID_SIGNAL`
- exact full-scan oracle: unapproved
- official measurement: unauthorized
- S3-B/S3-C: forbidden
- repository Slice 3: `Hold — valid evidence incomplete`

다음 안전 작업은 D1 exact design package와 E0-D exact protocol을 작성해 독립 검토한 뒤 각 digest를 승인 요청으로 제시하는 것이다. Runtime/tool 구현, Player execution과 evidence 생성은 그 뒤의 hard pause까지 시작하지 않는다.

## 11. P2 승인 대상과 다음 정지점

이 amendment exact SHA-256과 source revision/branch를 식별한 P2 승인은 다음 네 항목만 확정한다.

1. E0/E1 뒤에 `I3-pre`, `I3`, `D3/E2`를 두는 corrected authority sequence;
2. §5의 primary allocation candidate와 diagnostic-only control 분류;
3. §6의 timing falsification order와 threshold/outlier/retry 금지 규칙;
4. §7~§9의 future scope boundary, tests-first 조건과 독립 hard pause 해석.

P2 승인은 D1/E0-D 설계 문서 작성을 다음 허용 작업으로 만들지만, contract/artifact normative 승인, source/test/tool 구현, Player 실행 또는 evidence 생성을 허용하지 않는다. D1 exact design package와 E0-D protocol이 작성·독립 검토되면 각 exact bytes/digest를 보고하고 다음 hard pause에서 중단한다.

## 12. P2 승인 실행 결과와 D1/E0-D hard pause

사용자는 이 amendment SHA-256 `fbdd1c3d7e74acb92e4924b1d522a9dc881e9bf64c3fc8822a4437ef60f54a70`를 source `d0310f8b99589157e82f2fe42cb5bd5aab2b6c28 / codex/third-party-license-inventory`에서 P2로 승인했다. 그 승인에 따라 runtime/test/tool을 변경하지 않고 다음 design-only package를 작성했다.

### D1-D exact candidates — 아직 normative 승인 아님

- `Tools/contracts/gameplay_cleanup_slice3_evidence_contract_v5.md`: SHA-256 `32ea0e8af9c437f2ddcca4349e153af509724bf1c8d3376dc1cba02f49de986d`
- `Tools/contracts/gameplay_cleanup_slice3_workloads_v3.json`: SHA-256 `b6c143188566c5a7a68a4775ecc4c753dabd0f54792a9bb11e82a1b21dadce2e`
- `Tools/contracts/gameplay_cleanup_slice3_full_scan_oracle_v1.json`: SHA-256 `fdc8e157fe43cbb8731bc384a17d5d04ee99921c71a19b4e428d58e035279aac`
- `Tools/contracts/gameplay_cleanup_slice3_evidence_review_vectors_v1.json`: SHA-256 `775aaa17b470e52744dc414fae424fa454545e059e497004c618d83417a6b8a8`

독립 workload/oracle 검토는 초기 256 entity의 전체 fingerprint field order/default, seed 기반 position permutation, exact ordered stress operation blocks, warm-up engine tick `2000..2199`, measured engine tick `3000..3199`, cold-first `80` 대 measured steady-state `96` 구분을 요구했다. 보완 뒤 production helper를 import하지 않은 독립 확장에서 target/stress initial fingerprint와 schedule hash 및 stress 96-operation cardinality가 일치했다.

독립 authorization/runner 검토는 단순 caller-supplied digest가 위조 가능하므로 detached Ed25519 approval receipt, K1 public-key trust root, finite pre-generated slot/nonces, campaign ledger/final manifest, build-once Player payload binding과 non-authoritative `FINALIZING`을 요구했다. v5 candidate에 반영했지만 이 P2 작업은 실제 key를 만들거나 등록하지 않았다. 따라서 K1 전에는 official authorization이 언제나 `HOLD_MEASUREMENT_AUTHORIZATION`이다.

### E0-D exact candidate — 아직 실행/채택 승인 아님

- `Tools/contracts/gameplay_cleanup_slice3_e0_characterization_protocol_v1.json`: SHA-256 `8d9a85cf0d0fd9fc05b153f6e82f4dd602605804162d494928980bb7729a065b`

이 protocol은 Mono ReleaseLike current-thread `0/4096/8192` allocation primary, diagnostic-only IL2CPP/profiler controls, fixed/rotating/phase-local/fresh-process timing matrix, signed raw counters, exact cardinality, no result-based retry/exclusion, nonblocking global lock과 hash-bound result bundle을 고정한다. 결과 enum은 candidate 또는 Hold만 허용하고 `PASS`, `DEFERRED`, `READY` transport를 금지한다. 현재 code에는 이 raw carrier와 orchestration이 없으므로 protocol이 승인되더라도 실행 전 `I3-pre` exact implementation allowlist와 별도 diagnostic activation 승인이 필요하다.

### 현재 정지점

- D1-D와 E0-D는 독립 서브 에이전트 설계 검토를 반영한 proposed bytes다.
- JSON syntax, independent workload hash expansion과 docs-only whitespace validation만 허용되며 Unity/Player/evidence execution은 수행하지 않는다.
- 다음 권한은 D1 exact four-artifact approval과 E0-D exact protocol approval을 각각 별도로 받는 것이다.
- I2/I3-pre 구현, D2 activation, E0 실행, E1 adoption, K1/M1과 official capture는 계속 금지된다.
- Repository Slice 3는 `Hold — valid evidence incomplete`; S3-B/S3-C는 금지 상태다.


## 13. 2026-09-09 — Calibration 승인 순환 의존성 수정

커밋 검토 후 사용자 수정 요청에 따라 proposed v5의 `승인 -> calibration -> threshold -> 승인` 순환을 제거했다. §12의 v5/review-vector digest는 이번 수정본의 exact candidate로 갱신했으며, 기존 검토 당시 bytes와 구분한다. 이전 독립 검토 기록은 이 수정본의 독립 검토 증거가 아니다.

- D1/I2/D2, E1, K1, I3/D3 및 clean build validation 뒤 별도 C1 서명 승인으로 threshold 없는 calibration Goal/authorization을 만든다.
- C1 finite slots의 noise-valid calibration 완료 후 report/completion을 묶은 canonical threshold를 산출하고, 새 Goal/campaign/authorization의 M1이 이를 고정한다.
- C1/M1 signature domain과 slot 권한을 분리하고, 두 단계의 source/build/tool/procedure identity는 일치해야 한다. C1 완료는 official PASS/DEFERRED가 아니다.
- 이 순서는 기존 §4의 F2와 M1 사이에 C1 승인·calibration 완료·threshold 산출을 추가하는 proposed 설계 수정이다. C1 승인, 구현 또는 측정 실행을 수행하거나 승인한 것은 아니다.
- Review vectors에 순환 필드, signature-domain 혼용, official slot 오용, 불완전·stale handoff, official 결과 기반 threshold 교체 거절 및 최초 acyclic 생성 시나리오를 추가했다.

D1/E0-D와 이후 구현·실행 gate는 계속 미승인이다. E0 protocol, workload/oracle bytes와 production runtime은 이 수정 범위에서 바뀌지 않는다.
