# Slice 3 S3-A F1/B0 Amendment

- 상태: `I1 approved and implemented — validation complete; repository Hold retained`
- 작성일: 2026-08-29 KST
- source revision: `d0310f8b99589157e82f2fe42cb5bd5aab2b6c28`
- branch: `codex/third-party-license-inventory`
- 승인된 amendment SHA-256: `3f5db2a84580cadb8289ed1f2a0a8c013964b9313e3fb5eba652d1ac8fae8914`
- repository Slice 3: `Hold — valid evidence incomplete`
- B0 proposal: `B-Raw`
- I1 구현·테스트·non-official smoke: 승인 범위에서 완료
- v5·공식 capture·S3-B/S3-C·commit/push 권한: 없음

## 1. 목적과 현재 상태

이 amendment의 exact content SHA-256, source revision/branch와 §9 네 항목 전체를 사용자가 2026-08-29 KST I1로 승인했다. 승인은 비소급적으로 적용됐으며 승인 전에 이미 존재하던 source/test/tool 구현이나 누락된 formal red evidence를 정당화하거나 재구성하지 않는다. 승인 뒤 bounded current-contract 보완, 재검토 결함의 tests-first red/green, validation과 non-official capture smoke를 수행했다.

이 문서의 초기 proposal milestone은 `F1/B0 proposal ready`만 뜻했다. 현재는 승인된 A/B/C bounded closure까지 완료됐지만, 여전히 measurement-ready, official S3-A 결과, S3-B/S3-C 진입 또는 repository Slice 3 완료를 뜻하지 않는다.

작성 시작 시 보존한 dirty inventory는 다음과 같다. 모두 사용자 소유이며 삭제, 이동, stage, revert 또는 broad overwrite하지 않는다.

| 상태 | exact path | 이 Goal의 처리 |
|---|---|---|
| modified | `Docs/Architecture/README.md` | docs-only allowlist 안에서 amendment link만 추가하고 기존 변경 보존 |
| untracked | `Assets/AddressableAssetsData/Windows.meta` | unrelated; no-touch |
| untracked | `Docs/Architecture/Gameplay-Wall-Tick-Cost-Optimization-Slice3-S3A-F1-B0-Goal-Prompt.md` | status/closeout만 동기화하고 기존 Prompt 보존 |
| untracked | `Docs/Architecture/Gameplay-Wall-Tick-Cost-Optimization-Slice3-S3A-Reaudit-Remediation-Plan.md` | dated next-step link만 append하고 기존 proposal 보존 |

## 2. Domain-specific authority와 v4/v5 경계

| 영역 | authority | 이 amendment의 결정 |
|---|---|---|
| Gameplay StrongContract | repository `AGENTS.md`, canonical architecture, Slice 3 Goal semantic contract | Removal → Timer → Transition, removed-ID 후속 제외, timer=1 post-timer transition, same-tick spawn, occupancy/lifetime/result/hash/replay parity 변경 금지 |
| current implementation scope와 stage pause | 사용자가 I1로 승인한 exact revision/content | A/B/C current-contract remediation의 bounded write scope 승인·실행 완료; 후속 권한 확장 없음 |
| artifact schema, reason registry, verdict, transport | approved Evidence Contract v4 | v4 field/reason/schema를 silent edit하지 않으며 I1은 v4-compatible correction만 허용 |
| future v5 design/activation | 별도 D1/I2/D2 approval | v5 contract, oracle bytes/parser, generic-inert v3, 새 reason, `FINALIZING`, activation은 I1 밖 |
| official measurement | 별도 exact MeasurementAuthorization와 Measurement Goal | build validation과 official capture를 구분하며 official capture는 금지 |

충돌 시 더 엄격한 Hold를 적용한다. A/B/C 중 어떤 변경이 v4 artifact schema, reason registry 또는 version을 바꿔야만 구현 가능하다고 판명되면 해당 변경은 I1에서 중단하고 D1/I2 proposal로 돌린다.

## 3. B0 B-Raw exact contract

### 3.1 Raw membership

- raw removal match: `hp <= 0 || markedForDeath`
- raw timer match: `stateTimer > 0`
- raw immediate-transition match: `stateTimer <= 0 && (state == Acting || state == Cooldown)`
- 세 predicate는 독립이다. 같은 entity의 removal+timer 또는 removal+immediate overlap을 허용한다.
- raw membership count는 actual processed operation count가 아니다.

### 3.2 Actual processing

- `shouldRemove` entity는 removal만 실제 처리한다.
- removed ID는 timer/immediate actual processing에서 제외한다.
- survivor만 timer와 immediate processing으로 진행한다.
- timer=1 Acting/Cooldown survivor는 raw timer match 1, raw immediate match 0이지만 timer 처리 뒤 transition processed 1이 될 수 있다.
- same-tick spawned survivor의 `stateTimer > 0`은 raw timer match 1이지만 해당 entity의 timer processed는 0이다.
- 따라서 `transitionProcessedCount == rawImmediateTransitionMatchCount` 또는 모든 raw/processed field의 일반 equality를 invariant로 두지 않는다.

### 3.3 Runtime 대조 결과

2026-08-29 read-only source inventory에서 `RemovalProcessor.Process`는 `shouldRemove`를 계산한 뒤에도 structural capture 시 timer와 immediate predicate를 독립 집계하고, 그 다음 removal entity를 제외한 `survivingEntities`를 만든다. `CleanupProcessor.Process`는 이 survivor 목록만 `StateTimerProcessor`와 `StateTransitionProcessor`에 전달한다. `StateTimerProcessor`는 `spawnTick == tickIndex`를 decrement에서 제외하고, `StateTransitionProcessor`는 timer 처리 뒤 갱신된 survivor state를 사용한다.

따라서 current runtime execution과 B-Raw는 일치한다. B0 채택만으로 `RemovalProcessor.cs`, `CleanupProcessor.cs`, `StateTimerProcessor.cs`, `StateTransitionProcessor.cs`를 수정하지 않는다.

### 3.4 Exact matrix

| 입력 | raw removal | raw timer | raw immediate | actual processing |
|---|---:|---:|---:|---|
| dead + timer positive | 1 | 1 | 0 | removal only |
| marked + Acting + timer zero | 1 | 0 | 1 | removal only |
| marked + Cooldown + timer negative | 1 | 0 | 1 | removal only |
| survivor + timer positive | 0 | 1 | 0 | timer path |
| survivor + Acting/Cooldown + timer zero | 0 | 0 | 1 | transition path |
| inert survivor | 0 | 0 | 0 | none |
| same-tick dead/marked + timer positive | 1 | 1 | 0 | removal only |
| same-tick survivor + timer positive | 0 | 1 | 0 | none; decrement skipped |
| same-tick survivor + Acting/Cooldown + timer nonpositive | 0 | 0 | 1 | transition path |

추가 boundary는 `hp=-1/0/1 × marked=false/true`, 모든 current state와 unknown cast, negative/zero timer, timer=1 post-timer transition, same-tick spawned timer survivor, removal+timer, removal+immediate다.

### 3.5 Schema boundary

I1에서는 v4의 `removalCandidateCount`, `timerCandidateCount`, `immediateTransitionCandidateCount` wire field 이름을 유지한다. 이 amendment의 문서 vocabulary에서 이 세 값은 structural-captured full-scan invocation에서 누적한 raw predicate match를 뜻한다. Timing-only 또는 diagnostics-off invocation의 zero field는 entity가 predicate에 불일치한다는 뜻이 아니라 structural count를 수집하지 않았다는 뜻이다. `rawRemovalPredicateMatchCount` 등으로의 rename과 effective survivor-only 대안은 v5 D1/I2 또는 새 amendment 없이는 시작하지 않는다.

## 4. Proposed I1 scope — A/B/C current-contract remediation only

### Package A — measured companion reference 정합

- 모든 timing/capture-off measured phase를 먼저 완결한 뒤 reference companion phase를 실행하며 두 phase를 interleave하지 않는다.
- companion은 timing/capture-off와 다른 world instance를 사용하되 exact same workload ID, seed, schedule hash, initial fingerprint, repetition, measured tick ordinal을 결합한다.
- companion의 각 measured sample tick은 `CleanupCaptureMode.Structural | CleanupCaptureMode.Reference`로 실행하고 warmup tick은 reference invocation cardinality에서 제외한다.
- timing/capture-off에서는 reference invocation이 0이어야 하고, measured companion의 각 run은 `referenceOracleInvocationCount == executedTicks`, `invariantMismatchCount == 0`, exact repetition/tick cardinality를 만족해야 한다.
- v4 wire field `oracleParityVerified`는 constant가 아니라 per-run canonical 조건에서 derive하되 이름과 schema는 유지한다.
- base `CleanupProcessor` reference claim은 commit-operation parity로 제한한다. auxiliary expiry를 포함한 whole `RunCleanupPhase` capture on/off parity는 별도 scenario fixture로 증명한다.
- 항상 컴파일되는 internal producer core를 조건부 facade에서 분리하고, `VECTORQUAKE_CAPTURE_BUILD` Player build가 facade/Probe를 컴파일하며 실제 JSON을 생성하는 smoke를 별도로 둔다.

### Package B — B-Raw direct contract proof

- raw membership과 actual processing matrix를 actual `CleanupProcessor`/Tick execution 아래의 direct diagnostics counter, EventLog, final entity state assertion으로 고정한다.
- 컴파일 가능한 최소 no-op producer projection seam을 먼저 둔 뒤 actual diagnostics observation을 v4 producer carrier로 옮기는 assertion red를 보존한다. 별도 predicate 구현을 복제한 classifier test만으로 B-Raw를 증명하지 않는다.
- authoritative execution은 바꾸지 않으며 `RemovalProcessor.cs`는 read-only다.
- v4 wire rename, S3-B membership writer/index/carrier는 구현하지 않는다.

### Package C — v4-compatible current identity hardening

- standalone performance/Cleanup/calibration은 v4 preflight/captured context artifact를 함께 받아 identity/schema/hash/cardinality를 공통 validator로 검사한다. standalone success는 supplied context 내부 정합만 뜻한다.
- current v4 finalizer는 authoritative atomic emit 직전 live HEAD/worktree/runtime identity를 다시 확인하고 mismatch를 existing v4 reason으로 Hold한다.
- v4에는 authorized cohort membership을 신뢰 가능하게 결합하는 MeasurementAuthorization carrier가 없다. 따라서 authorized-cohort clean enforcement는 I1에서 runner에 구현하지 않고 D1/I2의 exact MeasurementAuthorization-bound v5 scope로 defer한다. 단순 caller flag, attempt kind 또는 모든 `gameplay-performance` run을 clean-gate하는 확대 해석은 금지한다.
- 새 v5 reason, authorized-cohort clean gate, machine-wide lock protocol, `FINALIZING` manifest state, last-emit 이후 attestation은 구현하지 않는다.

## 5. Exact I1 file authority

I1 승인 뒤에도 아래 `write` 행만 수정할 수 있다. “관련 파일”, directory glob 또는 자동 scope 확장은 허용하지 않는다.

### 5.1 Write allowlist

| package | exact path | 변경 목적 |
|---|---|---|
| A/B | `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/CleanupSlice3Diagnostics.cs` | 조건부 facade를 always-compiled core에 delegate, companion reference phase, derived v4 parity bool, B-Raw diagnostics seam |
| A/B | `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/CleanupSlice3PlayerCalibrationCore.cs` | 신규 always-compiled internal producer core; actual diagnostics observations를 v4 producer carrier와 derived parity로 projection |
| A/B | `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/CleanupSlice3PlayerCalibrationCore.cs.meta` | 신규 Unity C# asset의 paired metadata; core 파일과 같은 change/rollback unit |
| A/B | `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/CleanupSlice3AttributionSimulationTests.cs` | actual producer run cardinality, phase isolation, B-Raw direct matrix tests-first assertions |
| A | `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/CleanupPhaseScenarioTests.cs` | auxiliary expiry를 포함한 whole `RunCleanupPhase` capture on/off parity |
| A/C | `Tools/gameplay_performance_admission.py` | actual capture JSON과 supplied v4 context의 strict standalone validation |
| A/C | `Tools/gameplay_cleanup_slice3_admission.py` | actual producer reference count/parity와 v4 context fail-closed validation |
| A/C | `Tools/gameplay_cleanup_slice3_calibration.py` | supplied context와 admitted actual producer report만 calibration에 운반 |
| A/C | `Tools/gameplay_cleanup_slice3_evidence_manifest.py` | canonical chain 재계산과 atomic emit 직전 live identity recheck |
| C | `Tools/gameplay_evidence_v4.py` | preflight/captured context와 live Git identity를 공유하는 strict helper |
| A/C | `Tools/tests/test_gameplay_performance_admission.py` | missing/mixed context와 actual producer-shape rejection |
| A/C | `Tools/tests/test_gameplay_cleanup_slice3_admission.py` | reference short/missing/duplicate 및 actual JSON chain fixtures |
| A/C | `Tools/tests/test_gameplay_cleanup_slice3_calibration.py` | supplied-context mismatch와 downstream fail-closed fixtures |
| A/C | `Tools/tests/test_gameplay_cleanup_slice3_evidence_manifest.py` | canonical recomputation, v4 block 유지, pre-emit mutation fixtures |
| C | `Tools/tests/test_gameplay_cleanup_slice3_runner_lifecycle.py` | non-official capture-smoke lifecycle, roots, terminal wording |
| C | `Tools/tests/test_gameplay_evidence_v4_hardening.py` | common context/live-identity adversarial coverage |
| A/C | `run_tests.sh` | non-official `cleanup-s3-capture-smoke` lane와 actual JSON cross-boundary orchestration |
| A | `Docs/Testing/Gameplay-Test-Automation-Guide.md` | 새 non-official smoke lane의 목적, output, exit/claim 경계를 문서화할 때만 수정 |

신규 파일은 위 `CleanupSlice3PlayerCalibrationCore.cs`와 paired `.meta` 두 path만 허용한다. 별도 C# smoke fixture, Python helper, v5 contract/oracle/workload/authorization 파일은 I1에서 만들지 않는다. 추가 신규 파일이 필요하면 I1을 중단하고 exact path와 이유를 다시 승인받는다.

### 5.2 Read-only inventory

| exact path | read-only 목적 |
|---|---|
| `Assets/_Features/Gameplay/Gameplay_Cleanup/Runtime/RemovalProcessor.cs` | current independent raw count와 survivor split 확인 |
| `Assets/_Features/Gameplay/Gameplay_Cleanup/Runtime/CleanupProcessor.cs` | Removal → Timer → Transition 및 diagnostics recording 확인 |
| `Assets/_Features/Gameplay/Gameplay_Cleanup/Runtime/StateTimerProcessor.cs` | same-tick spawn와 timer=1 처리 확인 |
| `Assets/_Features/Gameplay/Gameplay_Cleanup/Runtime/StateTransitionProcessor.cs` | post-timer survivor transition 확인 |
| `Assets/_Features/UI/UI_Composition/Runtime/GameplayPerformancePlayerProbe.cs` | 기존 facade JSON과 v4 capture identity bridge를 smoke에서 소비; I1 수정 필요성 없음 |
| `Assets/_Features/Stages/Editor/Capture/PlayerProfilerCaptureCli.cs` | 기존 `VECTORQUAKE_CAPTURE_BUILD` performance Player build entrypoint를 smoke에서 사용 |
| `Assets/_Features/Stages/Editor/Tests/PlayerCaptureLaunchBootstrapSafetyTests.cs` | existing define/build option guard 확인 |
| `Assets/_Features/Stages/Editor/Tests/StageDefaultStageIdPolicyTests.cs` | capture scene routing regression 확인 |
| `Tools/contracts/gameplay_cleanup_slice3_evidence_contract_v4.md` | normative schema/reason/transport owner; silent edit 금지 |
| `Tools/contracts/gameplay_cleanup_slice3_workloads_v2.json` | exact v4 workload bytes/digest owner; edit 금지 |
| `Tools/tests/test_gameplay_performance_campaign.py` | existing performance campaign regression; test source 변경 없이 실행 |

### 5.3 Forbidden implementation scope

- `Assets/_Features/Gameplay/Gameplay_Cleanup/Runtime/CleanupProcessor.cs`, `StateTimerProcessor.cs`, `StateTransitionProcessor.cs`, `RemovalProcessor.cs`의 production semantic 변경
- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs`와 partial TickPipeline files의 semantic 변경
- `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldState.cs`, `WorldSnapshot.cs` 변경
- authoritative write/order/lifetime/occupancy, `CleanupPhaseResult`, EventLog, FinalEntities, presentation carrier, hash/trace/replay 변경
- S3-B candidate writer/index/snapshot carrier/fast import와 S3-C indexed executor/empty fast path
- Scene, Prefab, ScriptableObject, addressable asset 변경
- `Tools/contracts/gameplay_cleanup_slice3_evidence_contract_v4.md` 및 `Tools/contracts/gameplay_cleanup_slice3_workloads_v2.json` 변경
- `Tools/contracts/gameplay_cleanup_slice3_evidence_contract_v5.md`, `Tools/contracts/gameplay_cleanup_slice3_full_scan_oracle_v1.json`, `Tools/gameplay_cleanup_slice3_full_scan_oracle.py`, `Tools/contracts/gameplay_cleanup_slice3_workloads_v3.json` 생성 또는 구현
- v5 reason registry, `FINALIZING` lifecycle, machine-wide performance lock, MeasurementAuthorization/campaign-plan artifact
- caller flag/attempt kind 기반 clean-cohort selector 또는 MeasurementAuthorization 없는 authorized-cohort clean enforcement
- `CleanupReferenceOracle`에서 신규 producer core/projection helper를 재사용하는 상관 oracle 구현
- allocation characterization, official gameplay-performance capture, S3-B/S3-C, commit, push

## 6. Tests-first red plan

I1 승인 뒤 먼저 compile 가능한 no-op/scaffold만 추가하고 아래 red를 확인한다. missing-symbol compile error는 red evidence가 아니다. Red source는 commit하지 않으며 각 command, test, expected/actual assertion, exit, UTC/KST timestamp, HEAD, worktree hash를 보존한다.

Exact root template: `/mnt/d/J2M/evidence/20260829-s3a-f1-b0-i1-red/<attempt-uuid>/`. Runner는 UUID leaf를 exclusive create하고 기존 directory를 재사용하지 않는다.

| ID | command / test | 처음 기대하는 red | exact log |
|---|---|---|---|
| `I1-A-RED-001` | `./run_tests.sh full --filter Game.Feature.Gameplay.Tests.Scenario.CleanupSlice3AttributionSimulationTests.S3A_PlayerCalibrationCore_DerivesMeasuredReferenceParity` | current measured runs의 reference count `0` 대 expected `executedTicks` | `unity-producer-reference-red.log` |
| `I1-A-RED-002` | `./run_tests.sh full --filter Game.Feature.Gameplay.Tests.Scenario.CleanupSlice3AttributionSimulationTests.S3A_PlayerCalibrationCore_SeparatesTimingAndReferenceCompanionPhases` | timing/off reference 0, companion distinct world, same repetition/tick binding, measured `Structural|Reference`, warmup exclusion 중 scaffold가 불일치 | `unity-reference-phase-red.log` |
| `I1-A-RED-003` | `./run_tests.sh full --filter Game.Feature.Gameplay.Tests.Scenario.CleanupPhaseScenarioTests.S3A_RunCleanupPhaseCaptureParity_IncludesAuxiliaryExpiry` | 새 combined fixture의 scaffold parity carrier가 incomplete | `unity-auxiliary-parity-red.log` |
| `I1-B-RED-001` | `./run_tests.sh full --filter Game.Feature.Gameplay.Tests.Scenario.CleanupSlice3AttributionSimulationTests.S3A_BRawMembershipAndActualProcessing_MatchExactMatrix` | actual Cleanup diagnostics raw/processed/EventLog/final-state matrix는 관찰하되 no-op producer projection이 expected tuple과 불일치 | `unity-braw-matrix-red.log` |
| `I1-C-RED-001` | Python suite / standalone context negative cases | missing/mixed preflight/captured v4 context가 standalone success 가능 | `python-standalone-context-red.log` |
| `I1-C-RED-002` | Python suite / pre-atomic persistent mutation hook | 마지막 live check 뒤 atomic emit 전 mutation이 current success 후보를 닫지 못함 | `python-pre-emit-identity-red.log` |
| `I1-A-C-RED-003` | non-official `./run_tests.sh cleanup-s3-capture-smoke` | actual capture-build JSON의 measured reference sub-contract가 거부되고 downstream Hold envelope가 exact하지 않음 | `capture-build-cross-boundary-red.log` |

Python suite command는 다음 exact module set을 사용한다.

```bash
python3 -m unittest \
  Tools.tests.test_gameplay_performance_admission \
  Tools.tests.test_gameplay_performance_campaign \
  Tools.tests.test_gameplay_cleanup_slice3_admission \
  Tools.tests.test_gameplay_cleanup_slice3_calibration \
  Tools.tests.test_gameplay_cleanup_slice3_evidence_manifest \
  Tools.tests.test_gameplay_cleanup_slice3_runner_lifecycle \
  Tools.tests.test_gameplay_evidence_v4_hardening
```

`cleanup-s3-capture-smoke`는 official capture나 performance verdict가 아니다. 같은 define으로 CLI가 Player를 build하고 facade/Probe를 컴파일한 뒤 actual producer JSON 한 세트를 reference sub-contract check와 Python performance/Cleanup admission → calibration → manifest chain에 전달하는 bounded validation lane이다.

기본 output은 `/mnt/d/J2M/evidence/cleanup-s3-capture-smoke/<attempt-uuid>/`, build는 `/mnt/d/J2M/builds/cleanup-s3-capture-smoke/<attempt-uuid>/`다. 두 UUID leaf는 exclusive create하며 official gameplay-performance campaign/attempt namespace와 환경 변수를 사용하거나 기존 output을 재사용하지 않는다.

Smoke wrapper exit `0`은 다음을 모두 만족할 때만 허용한다.

1. CLI와 `VECTORQUAKE_CAPTURE_BUILD` facade/Probe compile, Player execution, marker validation이 성공한다.
2. actual Player JSON의 every measured run에서 reference count, mismatch, repetition/tick binding과 derived parity가 exact하다.
3. performance/Cleanup/calibration/manifest tool이 모두 실행돼 schema-valid artifact를 남기고 identity/schema/reference 관련 unexpected reason이 없다.
4. signal이 valid하면 performance/Cleanup은 admitted, calibration은 `READY` 또는 `DEFERRED_NOT_MATERIAL`, authoritative final manifest는 오직 v4 §7.5 `FULL_SCAN_EXPECTATION_UNAPPROVED` 때문에 `FINAL/HOLD/HOLD_INVALID_EVIDENCE`다.
5. known allocation liveness가 invalid하면 exact allocation-only rejection/Hold envelope를 허용하되 reference sub-contract는 별도로 green이어야 하며 다른 semantic/identity/schema reason을 허용하지 않는다. 구현 전 fixture에서 exact allowed code/path/status set을 고정하고 scope를 넓히지 않는다.

Runner는 manifest의 authoritative Hold를 `PASS`/`DEFERRED`로 바꾸거나 generic `ALL TESTS PASSED`/official gameplay-performance terminal line을 출력하지 않는다. 유일한 success line은 `Cleanup S3 capture smoke: PASS (non-official; authoritative manifest remains HOLD)`이며, 그 외 envelope는 nonzero다.

## 7. I1 이후 validation matrix

### Required

| 검증 | exact command / proof |
|---|---|
| Python syntax | `python3 -m py_compile Tools/gameplay_performance_admission.py Tools/gameplay_cleanup_slice3_admission.py Tools/gameplay_cleanup_slice3_calibration.py Tools/gameplay_cleanup_slice3_evidence_manifest.py Tools/gameplay_evidence_v4.py Tools/tests/test_gameplay_performance_admission.py Tools/tests/test_gameplay_performance_campaign.py Tools/tests/test_gameplay_cleanup_slice3_admission.py Tools/tests/test_gameplay_cleanup_slice3_calibration.py Tools/tests/test_gameplay_cleanup_slice3_evidence_manifest.py Tools/tests/test_gameplay_cleanup_slice3_runner_lifecycle.py Tools/tests/test_gameplay_evidence_v4_hardening.py` |
| Python regression | §6의 7-module unittest command |
| shell syntax | `bash -n run_tests.sh` |
| focused Scenario | `./run_tests.sh full --filter Game.Feature.Gameplay.Tests.Scenario.CleanupSlice3AttributionSimulationTests`; nonzero test count 필수 |
| auxiliary Cleanup scenario | `./run_tests.sh full --filter Game.Feature.Gameplay.Tests.Scenario.CleanupPhaseScenarioTests.S3A_RunCleanupPhaseCaptureParity_IncludesAuxiliaryExpiry`; nonzero test count 필수 |
| gameplay/core | `./run_tests.sh core` |
| capture-build compile and chain | `./run_tests.sh cleanup-s3-capture-smoke`; CLI + `VECTORQUAKE_CAPTURE_BUILD` facade/Probe compile, actual JSON → Cleanup admission → calibration → manifest |
| static diff | `git diff --check`와 exact allowlist scan |

### Conditional / explicit not-run

- `GameplayPerformancePlayerProbe.cs`는 read-only다. scope exception으로 수정하게 되면 즉시 중단해 I1을 다시 승인받고 `./run_tests.sh ui`를 required로 승격한다.
- authoritative semantics 또는 replay carrier는 forbidden이다. 예상대로 변경이 없으면 `./run_tests.sh --integration-replay --filter TickReplayDeterminismTests`는 not run이며 이유는 “semantic/replay carrier unchanged”다. 변경 필요성이 생기면 I1을 중단한다.
- broad unfiltered `./run_tests.sh full`은 documented red baseline이며 별도 touched risk가 없으면 실행하지 않는다. focused `full --filter`와 broad full을 혼동하지 않는다.
- official `./run_tests.sh gameplay-performance`, allocation characterization, v5 capture는 실행하지 않는다.

### Forbidden claims

`project-wide green`, `full lane green`, `full regression closed`, `all regressions fixed`, `S3-A fixed`, `S3-A complete`, `measurement-ready`, official performance improvement를 주장하지 않는다. Focused/core/Python/smoke 결과는 각각 실제 실행 범위로만 보고한다.

## 8. Rollback과 사용자 변경 보호

- I1 변경은 A/B/C intent별로 분리 가능하게 유지한다. 새 core 파일과 `.meta`는 한 rollback unit이다.
- B-Raw는 documentation/tests/diagnostics vocabulary만 고정하며 production Cleanup processor를 rollback 대상으로 만들지 않는다.
- C changes는 v4 context/identity/runner boundary 안에서 되돌릴 수 있어야 하며 v4 contract bytes를 수정하지 않는다.
- rollback 전에 exact touched-file inventory와 user-owned diff overlap을 다시 확인한다.
- `git reset --hard`, broad checkout, 사용자 변경 삭제, preserved evidence 삭제를 사용하지 않는다.
- `Assets/AddressableAssetsData/Windows.meta`와 이 Goal 시작 전 docs diff는 계속 사용자 소유다.

## 9. I1 승인 대상과 승인 후 첫 순서

I1 승인 대상은 다음 네 항목 전체다.

1. F1 bounded A/B/C implementation scope
2. B0 B-Raw membership/processing contract
3. §5 exact write/read-only/forbidden allowlist
4. §6 tests-first red evidence plan과 §7 validation lanes

승인은 외부에서 계산한 이 문서의 exact file SHA-256과 source HEAD/branch를 함께 식별하고 위 네 항목 전부를 명시해야 한다. 대안으로 사용자가 이 문서의 exact full content와 위 네 항목 전부를 명시 승인할 수 있다. identity 누락, 일부 승인 또는 모호한 “진행”은 source 변경 권한이 아니다.

승인 후 첫 실행 순서는 다음과 같다.

1. `git status --short --branch`, `git diff --stat`, `./run_tests.sh --print-config` 재확인
2. 승인 revision/content와 amendment SHA-256 재확인
3. exact write allowlist 및 user-owned overlap 재확인
4. compile 가능한 no-op/scaffold 작성
5. §6 formal red를 exact evidence root에 보존
6. A → B → v4-compatible C 구현
7. §7 validation과 current-truth F2 결과 동기화
8. D1/v5/measurement로 자동 진행하지 않고 다음 approval에서 중단

## 10. Terminal status와 remaining blockers

```text
Goal outcome: I1 implementation and validation complete — next approval required
Repository Slice 3: Hold — valid evidence incomplete
B0 vocabulary: B-Raw implemented and tested
Implementation/tests: bounded I1 validation complete
Post-review red evidence: /mnt/d/J2M/evidence/20260829-slice3-i1-postreview-red/red/
Evidence Contract v5: not drafted or approved
Official S3-A capture: not authorized
S3-B/S3-C: forbidden
```

원래 §6 formal A/B/C red는 I1 승인 전에 구현이 이미 존재해 보존할 수 없었으며 소급 생성하지 않은 permanent deviation이다. 승인 뒤 새 재검토 finding의 red는 위 경로에 보존했다. Initial closure에서 Python/runner `117 passed`, `./run_tests.sh core` EditMode `228/228` 및 PlayMode `111 total / 0 failed`, focused Cleanup attribution `11/11`, auxiliary expiry `1/1`, non-official capture smoke의 exact authoritative `HOLD_CLEANUP_ADMISSION` envelope를 확인했지만, 이는 subsequent terminal re-audit correction 전 historical validation이다. 최종 current-source 재검증 수치는 terminal report가 소유한다. broad unfiltered full, replay, UI와 official capture는 실행하지 않았다.

Remaining blockers는 D1/I2/D2 v5 exact full-scan oracle design·scope·activation, valid allocation signal, exact MeasurementAuthorization/Measurement Goal이다. Non-official smoke의 allocation counter는 `expectedAtLeast=4096 observed=0`으로 fail-closed했고 official measurement authority가 없으므로 repository Slice 3는 계속 Hold다.
