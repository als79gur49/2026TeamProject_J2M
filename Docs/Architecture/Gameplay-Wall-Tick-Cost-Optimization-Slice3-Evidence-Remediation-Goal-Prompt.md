# Gameplay Wall Tick Cost Optimization — Slice 3 Evidence Remediation Goal Prompt

- 문서 역할: S3-A evidence harness P0/P1 복구를 위한 bounded execution Goal
- 작성일: 2026-08-28 KST
- 실행 상태: `Complete — Evidence Contract v4 P0/P1 remediation independently re-audited`
- repository 상태: `Hold — valid evidence incomplete`
- production Cleanup executor: full-scan `CurrentPolicy` 유지
- 후속 S3-A official capture: 이 Goal 범위 밖, 자동 시작 금지
- S3-B/S3-C production 구현: 이 Goal 범위 밖, 진입 금지

## 1. 실행 Goal

사용자가 이 remediation의 실행을 명시적으로 요청한 경우에만 다음 outcome-neutral Goal을 생성한다. 이 문서 작성만으로 실행 Goal을 생성하거나 시작하지 않는다. 명시적 token budget이 없으면 budget을 설정하지 않는다.

> Evidence Contract v4를 명시적으로 승인된 normative contract로 고정하고, S3-A evidence harness의 알려진 false-PASS, schema, artifact-lifecycle, terminal-transport 결함을 tests-first로 fail-closed하며, 보존된 red/green evidence와 독립 재감사로 P0/P1 closure를 입증한다. 새 official capture나 gameplay candidate implementation은 수행하지 않는다.

이 실행 Goal이 complete가 되더라도 repository Slice 3는 자동으로 complete가 되지 않는다. 완료 기록은 §14의 exact 문구를 사용하며 repository Hold를 유지한다.

## 2. 이 Goal을 분리하는 이유

Evidence 도구 복구, allocation-capable signal 확보, official performance capture, S3-B candidate maintenance를 한 Goal에 넣으면 다음 문제가 반복된다.

- 외부 signal 부재가 tool remediation 완료 여부를 가린다.
- `Hold`, `DEFERRED`, 실행 Goal `complete`, repository Goal `complete`가 혼합된다.
- P0/P1 tool 수정이 끝나기 전에 performance 수치나 B/C 진입으로 범위가 확장된다.
- fast-import tests-first pre-entry와 production candidate 구현 사이에 순환 gate가 생긴다.

따라서 현재 Goal은 evidence harness 신뢰성 복구에서 종료한다. 새 S3-A 측정은 별도 Measurement Goal, fast-import/workload identity와 S3-B 구현은 별도 Pre-entry/Implementation Goal로만 시작한다.

## 3. 권한과 우선순위

1. repository `AGENTS.md`, canonical architecture guardrail, `Tick-Simulation-Canonical-Spec.md`가 gameplay StrongContract 최상위 근거다.
2. [Slice 3 Goal Plan](./Gameplay-Wall-Tick-Cost-Optimization-Slice3-Goal-Plan.md)과 승인된 normative amendment/erratum이 repository 상태와 hard-pause를 소유한다.
3. [Slice 3 Goal Prompt](./Gameplay-Wall-Tick-Cost-Optimization-Slice3-Goal-Prompt.md)가 전체 Slice 실행 순서와 terminal-state 계약을 소유한다.
4. [Post-Amendment Audit](./Gameplay-Wall-Tick-Cost-Optimization-Slice3-Evidence-Post-Amendment-Audit-2026-08-28.md)은 finding/evidence register다.
5. 이 문서는 P0/P1 remediation의 작업 순서, touch scope, validation, 종료 조건만 구체화한다.
6. [Evidence Contract v4](../../Tools/contracts/gameplay_cleanup_slice3_evidence_contract_v4.md)는 2026-08-28 KST 사용자 승인으로 새 remediation artifact의 normative harness contract가 되었다. 기존 [Evidence Contract v3](../../Tools/contracts/gameplay_cleanup_slice3_evidence_contract_v3.md)는 historical predecessor로 보존하며 v4 terminal result를 만들 수 없다.
7. 충돌 시 더 엄격한 Hold를 적용하고 unresolved ambiguity를 구현 권한으로 해석하지 않는다.

## 4. 시작 전 필수 읽기와 상태 확인

1. `Docs/Architecture/Gameplay-Wall-Tick-Cost-Optimization-Slice3-Evidence-Post-Amendment-Audit-2026-08-28.md`
2. `Docs/Architecture/Gameplay-Wall-Tick-Cost-Optimization-Slice3-Goal-Plan.md`
3. `Docs/Architecture/Gameplay-Wall-Tick-Cost-Optimization-Slice3-Goal-Prompt.md`
4. `Docs/Architecture/Gameplay-Wall-Tick-Cost-Optimization-Plan.md`
5. `Docs/Architecture/README.md`
6. `Docs/Testing/Gameplay-Test-Automation-Guide.md`
7. `AI_GIT_COMMIT_RULES.md`
8. `AGENTS.md`

`gameplay-contract-hardening`을 적용하고 시작 시 다음을 실행한다.

```bash
git status --short --branch
git diff --stat
./run_tests.sh --print-config
```

현재 dirty/untracked 파일은 사용자 소유로 취급한다. 이 Goal의 allowlist 밖 파일을 stage, revert, overwrite, delete하지 않는다. `Assets/AddressableAssetsData/Windows.meta`는 unrelated residue로 유지한다.

## 5. Contract 분류와 불변 경계

| 영역 | 분류 | 이 Goal의 정책 |
|---|---|---|
| Cleanup order/authority/result parity | `StrongContract` | 변경 금지 |
| production executor selection | runtime `CurrentPolicy` | full scan 유지 |
| evidence schema/admission/manifest/runner | harness `CurrentPolicy` → v4 contract 대상 | 이 Goal의 수정 범위 |
| workload/Wall authored provenance | S3-B pre-entry contract | characterization/결정만 기록하고 runtime 확대 금지 |

다음을 변경하지 않는다.

- `WorldState` authoritative write ownership;
- `WorldSnapshot` immutable read seam;
- Removal → Timer → Transition과 timer/transition event ordering;
- same-tick spawn, removed-ID exclusion, lifetime/occupancy cleanup;
- `CleanupPhaseResult`, presentation carrier, hash/trace/replay semantics;
- production Cleanup full-scan executor와 A-only runtime selector;
- S3-B candidate carrier/index, S3-C indexed executor/empty fast path.

## 6. 수정 allowlist

기본 allowlist:

- `Tools/contracts/gameplay_cleanup_slice3_evidence_contract_v4.md` 신규;
- `Tools/gameplay_performance_admission.py`;
- `Tools/gameplay_cleanup_slice3_admission.py`;
- `Tools/gameplay_cleanup_slice3_calibration.py`;
- `Tools/gameplay_cleanup_slice3_evidence_manifest.py`;
- 필요 시 `Tools/` 아래 strict evidence parsing/report helper 신규 파일;
- 위 도구의 `Tools/tests/test_gameplay_*.py` 및 신규 runner-lifecycle test;
- `run_tests.sh`의 `gameplay-performance` evidence orchestration 범위;
- 이 Goal과 직접 연결된 Slice 3 Goal/Prompt/audit/README 문서.

기본 금지:

- `Assets/_Features/Gameplay/Gameplay_Cleanup/**`;
- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline*.cs`;
- `WorldState`, `WorldSnapshot`, entity logic와 gameplay production runtime;
- Scene, Prefab, ScriptableObject, addressable asset;
- S3-B/S3-C production code.

Metrics producer 또는 C# probe 변경이 불가피하다고 판단되면 즉시 scope를 넓히지 않는다. 필요한 필드, 외부 manifest로 대체할 수 없는 이유, 추가 Unity/UI validation을 먼저 보고하고 사용자 승인을 받은 뒤 allowlist amendment를 작성한다.

2026-08-28 KST 승인 기록: 사용자는 Evidence Contract v4와 v4 §15의 최소 metrics-producer allowlist amendment를 함께 명시 승인했다. `GameplayPerformancePlayerProbe.cs` 변경은 attempt/runtime/build/harness identity echo와 schema-2 serialization으로만 제한하며 focused/Python/core/ui validation을 요구한다. 새 official capture는 승인되지 않았다.

## 7. Phase R0 — Evidence Contract v4 draft

도구를 수정하기 전에 v4 draft에 다음을 exact contract로 고정한다.

### Required identity matrix

- pre-build HEAD/worktree hash;
- post-build/guard-restore HEAD/worktree hash;
- runtime-tree/build artifact/build-payload hash;
- metrics revision;
- stage, active strategy, workload ID/run key;
- validator, aggregator, manifest, workload-contract hash;
- campaign/attempt identity;
- artifact별 required/optional field와 missing/mismatch reason code.

### Exact schema/domain

- 모든 schemaVersion은 bool/float가 아닌 JSON integer;
- repetition/tick/sample count는 positive JSON integer;
- mutation/candidate/invocation/maintenance/fallback/mismatch count는 nonnegative JSON integer;
- workload/repetition/strategy cardinality와 order exactness;
- KV duplicate/malformed line과 JSON duplicate member 거부;
- old artifact schema의 historical/unsupported/upgrade 금지 정책.

### Verdict/status invariant

- performance admission JSON artifact schema와 exit coherence;
- `ADMITTED`, `REJECTED`, `READY`, `DEFERRED_NOT_MATERIAL`, 각 Hold status의 required fields;
- reasons, admitted, signalValid, attributionMaterial, thresholds, campaignRules 조합;
- manifest가 persisted verdict를 canonical recomputation과 비교하는 방식;
- `PASS`, `DEFERRED`, `HOLD`, `NOT_RUN` terminal mapping.

### Artifact lifecycle/transport

- attempt 시작 시 stage는 `NOT_RUN`;
- build/Player/marker/admission/calibration/manifest 조기 실패도 final machine-readable artifact 보존;
- output/input resolved-path 및 inode alias 거부;
- temporary write + atomic replace;
- manifest CLI exit와 JSON terminal verdict의 서로 다른 의미;
- `runnerObservedStatus` 제거/개명 정책.

### Hard pause R0

v4 draft, v3 대비 변경표, artifact version/compatibility matrix를 사용자에게 보고한다. 사용자가 v4를 명시 승인하기 전에는 production tool implementation과 기존 테스트 기대값 변경을 시작하지 않는다.

## 8. Phase R1 — Formal tests-first red

승인된 v4 vocabulary로 기존 5개 Python module에 negative fixture를 먼저 추가한다.

### Identity/cohort red

- preflight/captured revision 및 worktree mismatch;
- build 후 guard 밖 tracked/untracked mutation;
- stale copied hash;
- metrics/runtime/artifact/harness/campaign mixed cohort;
- top-level/captures/allocation/provenance strategy conflict.

### Verdict red

- 실제 performance validator failure + forged status `0`;
- `ADMITTED` + fatal reason;
- `READY` + `thresholds=null`;
- `READY` + `signalValid=false`;
- `READY` + `attributionMaterial=false`;
- verdict/status/provenance/hash tamper.

### Schema/lifecycle/transport red

- missing repetitions, float ticks, negative counts, float schemaVersion;
- KV/JSON duplicate key;
- malformed workload-contract root;
- build/Player/marker early failure with downstream `NOT_RUN`;
- output/input/symlink/hardlink alias;
- `DEFERRED`가 runner에서 `PASS`로 축약되는 경로.

각 red는 수정 전에 실제 실패해야 하며 다음을 `/mnt/d/J2M/evidence/<new-remediation-id>/red/`에 기록한다.

- command와 test name;
- expected/actual assertion;
- exit code;
- UTC/KST timestamp;
- HEAD와 worktree diff hash;
- failure log와 fixture mutation 설명.

과거 v3 amendment의 보존되지 않은 red log를 새 red로 복원했다고 주장하지 않는다. 과거 공백은 permanent historical deviation으로 유지한다.

## 9. Phase R2 — P0 identity/cohort binding

1. build 직전 HEAD와 worktree hash를 계산한다.
2. build 및 guarded restore 완료 직후 실제 HEAD/worktree hash를 다시 계산한다.
3. copied shell variable이 아니라 두 시점의 실제 값을 artifact에 기록한다.
4. required identity matrix를 admission/manifest에서 교차검증한다.
5. dual strategy representation을 금지하거나 exact equality를 요구한다.
6. 누락과 mismatch를 별도 machine-readable reason으로 `HOLD_INVALID_EVIDENCE` 처리한다.

단순 preflight/captured 문자열 equality만 추가하고 stale copied hash를 남기는 수정은 불완전한 것으로 거부한다.

## 10. Phase R3 — P0 authoritative verdict semantics

1. `gameplay_performance_admission.py`에 machine-readable `--output`을 추가해 verdict/reasons/provenance를 항상 보존한다.
2. admission/calibration CLI가 사용하는 pure report builder를 manifest에서도 재사용한다.
3. manifest는 raw caller status나 artifact enum을 독립 truth로 신뢰하지 않는다.
4. metrics로 canonical report를 재생성하고 persisted artifact와 exact 비교한다.
5. status별 semantic invariant와 CLI exit coherence를 함께 검증한다.

Manifest에 별도의 복제 판정식을 작성해 producer와 drift하는 구조는 허용하지 않는다.

## 11. Phase R4 — P1 exact schema와 strict parsing

1. 공통 strict JSON loader로 duplicate member를 거부한다.
2. KV parser는 duplicate key와 malformed nonempty line을 거부한다.
3. contract root type을 `.get()` 전에 확인한다.
4. required cardinality, exact JSON type, positive/nonnegative domain을 검증한다.
5. 모든 repository-input load/validation exception을 machine-readable Reject/Hold artifact로 변환한다.

정상 producer가 생성하지 않는 extra/legacy field를 조용히 무시하지 않는다. 허용할 compatibility shape는 v4 allowlist에 명시된 것만 사용한다.

## 12. Phase R5 — P1 artifact lifecycle와 terminal transport

Runner의 여러 early return을 하나의 finalization path로 수렴시킨다.

1. attempt 시작 시 provisional manifest에 모든 stage를 `NOT_RUN`으로 기록한다.
2. build, Player, marker, performance admission, Cleanup admission, calibration, final consistency를 순서대로 전이한다.
3. 어떤 단계에서 실패해도 final manifest를 atomic write한다.
4. output과 모든 input/tool/contract path/inode alias를 거부한다.
5. runner는 final manifest의 authoritative enum을 읽어 `PASS`, `DEFERRED`, `HOLD`를 그대로 출력한다.
6. `runnerObservedStatus`는 제거하거나 `derivedGateStatus`로 바꾼다. 실제 runner observation이 필요하면 runner-owned final record에만 둔다.

Manifest CLI exit `0`을 consistency artifact 생성 성공으로 유지한다면 Hold/Deferred와의 관계를 v4에 명시하고 runner는 exit code만으로 terminal PASS를 만들지 않는다.

## 13. Phase R6 — Green validation과 독립 재감사

최소 Python gate:

```bash
python3 -m unittest \
  Tools.tests.test_gameplay_performance_admission \
  Tools.tests.test_gameplay_performance_campaign \
  Tools.tests.test_gameplay_cleanup_slice3_admission \
  Tools.tests.test_gameplay_cleanup_slice3_calibration \
  Tools.tests.test_gameplay_cleanup_slice3_evidence_manifest

python3 -m py_compile Tools/gameplay_*.py Tools/tests/test_gameplay_*.py
bash -n run_tests.sh
git diff --check
```

추가 runner-lifecycle test가 별도 module이면 같은 unittest invocation에 포함한다. Test count는 실행 결과로만 기록하며 사전에 고정하거나 기존 `48/48`을 재사용하지 않는다.

독립 재감사에서는 red matrix 전체를 fresh temporary fixture로 다시 실행한다. 서브 에이전트 사용은 사용자가 요청한 경우에만 수행한다. 다음을 모두 만족해야 한다.

- known false `PASS / VERIFIED` 0건;
- malformed/early failure마다 final machine-readable artifact 존재;
- duplicate/alias/mixed-cohort가 fail-closed;
- `DEFERRED`가 terminal `DEFERRED`로 보존;
- old/historical artifact가 v4 result로 자동 승격되지 않음;
- audit, Goal Plan, Goal Prompt, contract, tool enum/field 이름 일치.

Gameplay/C# runtime을 건드리지 않았다면 Unity lane은 실행하지 않고 그 이유를 기록한다. Allowlist amendment로 C# probe를 변경한 경우에는 focused Cleanup Slice 3 test, `./run_tests.sh core`, 파일 소유상 필요한 `./run_tests.sh ui`를 실행한다. Broad unfiltered `full`은 known-red baseline과 분리한다.

## 14. 완료 조건

이 Goal은 다음이 모두 충족될 때만 `complete`로 닫는다.

- Evidence Contract v4가 명시 승인되고 current truth-source에 연결됨;
- 모든 새 negative test의 formal red evidence가 보존됨;
- P0 identity/cohort와 authoritative verdict semantics가 fail-closed;
- P1 schema, parsing, artifact lifecycle, terminal transport가 fail-closed;
- independent exact full-scan visit oracle와 frozen contract/workload amendment가 별도 승인되기 전 `PASS/DEFERRED` transport 및 official capture가 hard-block됨;
- Python/structural validation이 같은 revision에서 통과;
- 독립 재감사에서 known false PASS가 0건;
- 테스트·미실행 lane·한계가 정확히 기록됨;
- production gameplay/runtime semantic 변경과 S3-B/S3-C 구현이 없음.

완료 문구:

> Evidence remediation complete — known S3-A false-PASS paths closed; official capture not run; repository Slice 3 remains Hold pending an approved independent exact visit oracle/frozen contract amendment, an allocation-capable or approved-equivalent signal, and a separate Measurement Goal.

## 15. 중단 및 terminal 상태

- `Hold — contract approval required`: v4 draft가 아직 승인되지 않음.
- `Hold — evidence remediation incomplete`: P0/P1 또는 validation/audit가 닫히지 않음.
- `Evidence remediation complete`: 이 문서의 완료 조건 충족. Repository Slice 3 `Hold`는 유지.
- `Goal blocked`: 같은 infrastructure/authority blocker가 최소 세 번 연속 반복되어 safe alternative를 소진한 경우에만 사용.
- `Goal deferred`: 이 remediation Goal에는 사용하지 않는다. Non-material attribution에 의한 defer는 별도 S3-A Measurement Goal의 terminal result다.

사용자 continuation 대기는 blocked가 아니다. 안전한 같은-package 작업이 남아 있으면 계속 진행하고, hard pause에서는 현재 결과와 필요한 승인만 보고한다.

## 16. 후속 Goal — 자동 생성/시작 금지

### Measurement Goal

Entry:

- 이 remediation Goal complete;
- independent exact full-scan visit oracle의 owner·formula·frozen bytes·approved digest·negative tests를 고정한 contract/workload amendment 승인;
- allocation-capable 또는 명시 승인된 equivalent signal;
- 새 campaign identity;
- official measurement용 clean worktree.

Objective:

> 승인된 v4 evidence harness로 새 S3-A capture를 수행하고 `PASS`, `DEFERRED`, `HOLD` 중 하나의 trustworthy terminal verdict를 산출한다.

`PASS`여도 S3-B를 자동 시작하지 않고 사용자 continuation을 요청한다.

### S3-B Pre-entry/Implementation Goal

Entry:

- 새 S3-A `PASS`;
- 사용자 continuation;
- truthful workload/Wall identity 결정;
- fast-import writer inventory와 independent candidate oracle 준비.

Tests-first writer inventory/source audit과 failing fast/slow import candidate-parity fixture는 pre-entry로 허용한다. Candidate maintenance production code는 해당 Goal의 승인 후에만 구현한다.

## 17. Storage, commit, reporting

- 현재 C-drive worktree는 grandfathered legacy path로 유지하며 이동·삭제하지 않는다.
- 새 worktree가 필요하면 D free space 30 GiB 이상 확인, `j2m-worktree-audit`, `j2m-worktree-add` 순서를 사용한다.
- 새 worktree는 `/mnt/d/J2M/worktrees`, evidence는 `/mnt/d/J2M/evidence`, build는 `/mnt/d/J2M/builds` 아래에 둔다.
- Unity `Library`는 worktree별로 분리한다.
- commit/push는 별도 사용자 승인 없이는 수행하지 않는다.
- 보고는 touched Python/runner results, 실행하지 않은 Unity lane과 이유, remaining risk를 분리한다.
- broad lane을 실행하고 같은 revision에서 통과하지 않은 한 `project-wide green`, `full regression closed`, `all regressions fixed`를 주장하지 않는다.

## 18. 2026-08-28 KST closeout record

Follow-up erratum: 아래 항목은 최초 remediation pass의 historical record다. 이후 independent exact full-scan visit oracle 부재가 발견되어 `FULL_SCAN_EXPECTATION_UNAPPROVED` hard-block을 추가했으며, 승인된 contract/workload amendment 전에는 `READY` 또는 `DEFERRED_NOT_MATERIAL` 입력도 terminal `HOLD_INVALID_EVIDENCE`다. 따라서 아래 validation은 당시 touched scope의 기록이지 현재 terminal-success 또는 새 capture 권한이 아니다.

- Evidence Contract v4와 §15 probe amendment: 승인·구현 완료.
- formal tests-first red: `/mnt/d/J2M/evidence/20260827T204003Z-cleanup-s3-v4-remediation/red/`에 보존; S3-EV-008의 historical gap을 복원한 것으로 취급하지 않음.
- P0: live pre/post source/runtime identity, attempt/cohort/harness binding, persisted performance/Cleanup/calibration report의 canonical recomputation과 exit coherence 구현.
- P1: recursive duplicate JSON·strict KV·exact type/domain/cardinality, provisional/final lifecycle, artifact state, alias/inode/double-hash/atomic-write guard, exact terminal transport 구현.
- validation: historical remediation snapshot 기준 Python v4+runner suite `80/80`; focused Cleanup Slice 3 EditMode `8 passed / 0 failed` (matching PlayMode `0`); core EditMode `228 passed / 0 failed`, PlayMode `111 total / 107 passed / 4 skipped / 0 failed`; UI Windows build와 EditMode `1340 passed / 0 failed`; `py_compile`, `bash -n`, `git diff --check` 통과.
- explicitly not run: 새 `gameplay-performance` official capture, replay, broad unfiltered full. 첫 `core --filter` 시도는 Scenario fixture가 Core lane에 포함되지 않아 `0 tests`로 거절되었고, 실제 focused proof는 `full --filter`로 실행했다.
- scope: gameplay StrongContract, production Cleanup full-scan selector, S3-B/S3-C candidate implementation은 변경하지 않음. 승인된 probe 변경은 v4 identity echo/schema serialization 경계에 한정됨.
- repository terminal: `Hold — valid evidence incomplete`; 승인된 independent exact visit oracle/frozen contract amendment, allocation-capable 또는 승인된 equivalent signal, 별도 Measurement Goal 없이는 새 capture/S3-B/S3-C를 시작하지 않음.
- commit/push: 실행하지 않음.
