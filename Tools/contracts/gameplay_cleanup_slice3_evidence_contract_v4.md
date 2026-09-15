# Cleanup Slice 3 Evidence Contract v4

- Status: `APPROVED — NORMATIVE`
- Drafted: 2026-08-28 KST
- Explicit approval: 2026-08-28 KST, user-approved together with the §15 metrics-producer allowlist amendment
- Historical predecessor: [Cleanup Slice 3 Evidence Contract v3](./gameplay_cleanup_slice3_evidence_contract_v3.md)
- Execution authority: [Slice 3 Evidence Remediation Goal Prompt](../../Docs/Architecture/Gameplay-Wall-Tick-Cost-Optimization-Slice3-Evidence-Remediation-Goal-Prompt.md)
- Current remediation state: `Follow-up hardening in progress — repository Slice 3 Hold retained`
- Official capture authority: none; this contract does not authorize a new capture
- Gameplay candidate authority: none; this contract does not authorize S3-B/S3-C production work

This file is the normative evidence-harness contract for new Cleanup Slice 3 remediation artifacts. v3 remains preserved historical provenance and cannot authorize a new v4 terminal result.

## 1. Contract language and classification

`MUST`, `MUST NOT`, `REQUIRED`, and `EXACT` are normative. A requirement is fail-closed: missing, ambiguous, duplicated, unsupported, or mismatched evidence cannot produce `PASS` or `DEFERRED`.

| Area | Classification | v4 rule |
|---|---|---|
| Cleanup order, authority, result, lifetime, trace, and replay parity | gameplay `StrongContract` | unchanged; outside v4 implementation scope |
| Ordinary production Cleanup executor | runtime `CurrentPolicy` | full scan remains selected |
| Evidence schema, admission, manifest, lifecycle, and terminal transport | harness contract | v4 is the approved normative contract |
| Workload/Wall authored provenance and candidate maintenance | later S3-B entry contract | not authorized by v4 |

## 2. Artifact families and target versions

In JSON artifacts, every `schemaVersion` and `evidenceContractVersion` is a positive JSON integer; JSON booleans and numerically equal floats are invalid. KV manifests use the exact decimal strings `SchemaVersion=2` and `EvidenceContractVersion=4` and reject alternative numeric spellings.

| Artifact | Format | Target `schemaVersion` | Role |
|---|---|---:|---|
| preflight manifest | strict KV | `2` | live pre-build source and attempt identity |
| captured artifact manifest | strict KV | `2` | live post-restore source, build, metrics, and runtime-log identity |
| performance metrics | strict JSON object | `2` | Player-produced measurements and capture identity |
| performance admission report | strict JSON object | `2` | persisted canonical performance verdict |
| Cleanup admission report | strict JSON object | `2` | persisted canonical Cleanup schema/workload verdict |
| Cleanup calibration report | strict JSON object | `2` | persisted canonical signal/materiality result |
| attempt evidence manifest | strict JSON object | `4` | provisional lifecycle record and final terminal truth |
| workload contract | strict JSON object | `2` | frozen workload input; retained unchanged by v4 |

All target artifacts except the workload contract contain `evidenceContractVersion=4`. An artifact with another or missing contract version is unsupported for v4 terminal evidence.

The only approved workload-contract bytes are SHA-256 `e48fa8fe4b91f1c165bee9985b55a1ce2d376e17214baeaf9e1a1635e50d41d4`. Schema compatibility without this exact digest is `TOOL_HASH_MISMATCH` and cannot produce `PASS` or `DEFERRED`.

## 3. Canonical identity vocabulary

### 3.1 Attempt identity

The exact attempt identity is:

- `campaignId`: non-empty string fixed before preflight;
- `attemptId`: non-empty, campaign-unique string fixed before preflight;
- `attemptOrdinal`: positive JSON integer;
- `attemptKind`: one of `calibration`, `warm-up`, or `official`;
- `stage`: one of `S3-A`, `S3-B`, or `S3-C`;
- `activeStrategies`: exact ordered JSON array: `["A"]`, `["A","B"]`, or `["A","B","C"]` for the corresponding stage;
- `captureNonce`: non-empty runner-generated value passed into the Player and echoed by the Player-produced metrics;
- `workloadContractSha256`: lowercase SHA-256;
- per measured run, `runKey = <strategy>/<workloadId>/<repetition>` with no alternative spelling.

Every occurrence of these fields MUST be exactly equal. A missing occurrence where the identity matrix marks it required is not compatibility; it is `IDENTITY_FIELD_MISSING`.

### 3.2 Source and runtime identity

The runner computes live values twice. It MUST NOT copy the pre-build shell variables into the post-restore fields.

- `preBuildHeadSha`: `git rev-parse HEAD` immediately before build;
- `preBuildWorktreeSha256`: live worktree hash immediately before build;
- `postRestoreHeadSha`: a new `git rev-parse HEAD` after the build guard has restored all owned paths;
- `postRestoreWorktreeSha256`: a newly computed live worktree hash after guard restore;
- `runtimeTreeSha256`: `SHA-256('HEAD\0' + headSha + '\0WORKTREE\0' + worktreeSha256)` using ASCII labels and lowercase hexadecimal values.

The worktree hash algorithm is the SHA-256 of the exact byte stream formed by:

1. ASCII `TRACKED`, NUL, then the unmodified bytes from `git diff --binary HEAD -- .`, then NUL;
2. ASCII `UNTRACKED`, NUL;
3. for each path from `git ls-files --others --exclude-standard -z | sort -z`, the raw repository-relative path bytes, NUL, lowercase file SHA-256, NUL.

Pre-build and post-restore HEAD, worktree, and derived runtime-tree values MUST match. During canonical finalization the manifest tool performs a third live Git identity computation and compares it to the frozen pre-build identity before atomic replacement. Any guard-outside tracked or untracked mutation therefore closes the gate.

### 3.3 Build and harness identity

- `playerArtifactSha256`: SHA-256 of the Player executable bytes;
- `buildPayloadSha256`: SHA-256 of sorted records for every regular build-output file. Each record is repository-independent build-relative UTF-8 path, NUL, decimal byte size, NUL, lowercase file SHA-256, LF;
- `runnerSha256`: SHA-256 of `run_tests.sh`;
- `performanceValidatorSha256`, `cleanupValidatorSha256`, `aggregatorSha256`, and `manifestToolSha256`: SHA-256 of the exact tool bytes;
- `workloadContractSha256`: SHA-256 of the exact workload-contract bytes;
- `harnessSha256`: SHA-256 of the ASCII label/value records above in the exact listed order, one `label=sha256` record per line.

The Player receives `runtimeTreeSha256`, `playerArtifactSha256`, `buildPayloadSha256`, `harnessSha256`, attempt identity, stage, and strategies as capture arguments and writes them unchanged into `performance-metrics.json`. This is the independent carrier that prevents a copied historical metrics file from being rewrapped as v4 evidence.

### 3.4 Exact KV carriers and canonical context

The preflight KV carrier has exactly these keys: `SchemaVersion`, `EvidenceContractVersion`, `EvidencePhase`, `CampaignId`, `AttemptId`, `AttemptOrdinal`, `AttemptKind`, `CaptureNonce`, `Stage`, `ActiveStrategies`, `PreBuildHeadSha`, `PreBuildWorktreeSha256`, `RuntimeTreeSha256`, `RunnerSha256`, `PerformanceValidatorSha256`, `CleanupValidatorSha256`, `AggregatorSha256`, `ManifestToolSha256`, `WorkloadContractSha256`, `HarnessSha256`, `ExpectedWidth`, `ExpectedHeight`, `ExpectedWarmupFrames`, `ExpectedSampleFrames`, `ExpectedTickInterval`, and the multiline `GitStatusShort` marker.

The captured KV carrier has that same exact set plus `PostRestoreHeadSha`, `PostRestoreWorktreeSha256`, `PlayerArtifactSha256`, `BuildPayloadSHA256`, `MetricsSHA256`, and `RuntimeLogSHA256`. `EvidencePhase` is exactly `preflight` or `artifact-captured` respectively. The five `Expected*` values are frozen preflight context used by both the performance CLI and final canonical recomputation; they are not mutable verdict inputs supplied after capture.

No second `GitStatusShort` marker, duplicate key, malformed nonempty line, or unregistered extra key is valid.

## 4. Required identity matrix

`R` means required and exact. `—` means the field is not carried by that artifact. An extra identity field in a `—` position is an unexpected field, not a second truth source.

| Identity | Preflight KV | Captured KV | Metrics | Performance report | Cleanup report | Calibration report | Attempt manifest |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| contract/schema version | R | R | R | R | R | R | R |
| campaign/attempt/ordinal/kind/nonce | R | R | R | R | R | R | R |
| stage/active strategies | R | R | R | R | R | R | R |
| pre-build HEAD/worktree/runtime tree | R | R | R | R | R | R | R |
| post-restore HEAD/worktree/runtime tree | — | R | R | R | R | R | R |
| Player artifact/build payload hash | — | R | R | R | R | R | R |
| metrics revision and metrics hash | — | R | R / — | R | R | R | R |
| runner/harness hash | R | R | R | R | R | R | R |
| performance validator hash | R | R | R | R | R | R | R |
| Cleanup validator hash | R | R | R | R | R | R | R |
| aggregator hash | R | R | R | R | R | R | R |
| manifest-tool hash | R | R | R | R | R | R | R |
| workload-contract hash | R | R | R | R | R | R | R |
| workload IDs and ordered run keys | — | — | R | R | R | R | R |

For metrics, `metrics revision` is required and `metrics hash` is impossible because a document cannot contain its own cryptographic hash; the captured manifest and every downstream report carry the actual metrics hash.

Required artifacts missing because an earlier stage failed MUST be represented by the attempt manifest stage state and `artifactState=MISSING`. They are not required to exist as empty placeholder files. Once their producer stage reports `PASS`, they become required and their absence is `ARTIFACT_MISSING`.

## 5. Strict parsing and exact object policy

### 5.1 JSON

- Every JSON root MUST be an object.
- Parsing MUST reject duplicate object members at every nesting depth.
- UTF-8 with an optional leading BOM is accepted; non-UTF-8 input is rejected.
- `NaN`, positive/negative infinity, and non-standard JSON constants are rejected.
- v4-owned reports and the attempt manifest use an exact field allowlist. Missing and unexpected fields are rejected.
- Metrics use the exact producer field allowlist in §6. No legacy or unknown top-level, phase, workload, run, allocation, or capture-identity field is ignored.
- Strict parsing happens before `.get()` or semantic validation. All parse/load/type failures become machine-readable reasons when a safe output path exists.

### 5.2 KV

- Every nonempty line before the `GitStatusShort:` section is exactly `key=value` with a nonempty key.
- Duplicate keys, empty keys, malformed nonempty lines, a second section marker, and content after an unsupported section are rejected.
- `GitStatusShort:` occurs exactly once and terminates key parsing. Its following raw lines are preserved as the one section value.
- Preflight and captured manifests have exact required-key allowlists. Unknown keys are rejected.

### 5.3 Numeric domains

- Positive JSON integers: all schema/contract versions, repetitions, repetition numbers, attempt ordinals, warm-up tick/frame counts, sample tick/frame counts, executed ticks, valid sample counts, seeds, and positive entity counts.
- Nonnegative JSON integers: Wall count; mutation, candidate, processed, visit, copy, invocation, maintenance, carriage, allocation, fallback, and mismatch counts.
- `bool` is never an integer.
- Metric summary `count` is a JSON integer. It is positive when the corresponding sample set is required. The render-idle tick summary and an explicitly unavailable optional profiler counter are the only allowed exact-zero count shapes.
- Timing/allocation summary values are finite JSON numbers in nondecreasing order `median <= p95 <= p99 <= maximum`. Timing and available allocation values are nonnegative. The existing unavailable optional profiler-counter sentinel is accepted only in its exact documented phase shape and never substitutes for the required Cleanup allocation signal.

### 5.4 Cardinality and order

- `repetitions` is required; omission is not equivalent to one repetition.
- Workload order is exactly target then stress.
- Repetitions are exactly `1..repetitions` with no gaps, duplicates, or extras.
- Strategies appear once and in the exact stage order.
- Every active strategy has the exact same ordered `(workloadId,repetition)` sequence.
- Every `runKey` is unique and equals the values in its containing strategy/workload/run objects.
- Allocation phases are exactly strategy-major, workload-major, diagnostics `true` then `false`.
- Dual strategy representations are forbidden. Metrics schema 2 uses `captures` only; top-level legacy `strategy` and `workloads` are unexpected fields.

## 6. Performance metrics schema 2

Metrics schema 2 preserves the measured v1 performance fields and adds binding identity. Its exact top-level fields are:

- `schemaVersion`, `evidenceContractVersion`, `captureIdentity`;
- `measurementKind`, `budgetVerdict`, `revision`, `unityVersion`, `developmentBuild`, `productName`;
- `operatingSystem`, `processorType`, `processorCount`, `systemMemorySizeMB`;
- `graphicsDeviceType`, `graphicsDeviceName`, `graphicsDeviceVersion`, `graphicsMemorySizeMB`;
- `qualityLevel`, `qualityName`, `requestedResolution`, `actualResolution`;
- `vSyncCount`, `targetFrameRate`, `warmupFrames`, `sampleFramesPerPhase`, `gameplayTickIntervalFrames`;
- `drawCallsCounterAvailable`, `gcAllocatedCounterAvailable`, `phases`, `cleanupSlice3Calibration`.

`captureIdentity` has exactly the attempt, source/runtime, build, and harness fields required by §4. `revision` MUST equal both HEAD values. `cleanupSlice3Calibration` has exactly:

- `schemaVersion=2`, `evidenceContractVersion=4`, `stage`, `activeStrategies`, `repetitions`;
- `warmupTicksPerRepetition`, `sampleTicksPerRepetition`, `allocationSignal`, `diagnosticsOffNoOpAllocatedBytes`;
- `captures`, `frameAllocationCalibration`.

`allocationSignal` is exactly `per-tick current-thread allocated-byte delta appended by Player probe`. Descriptive alternatives and legacy signal labels are invalid. This is the frozen serialized wire value emitted by Cleanup diagnostics. The Player probe separately appends the measured current-thread allocation payload as `frameAllocationCalibration`.

Each capture has exactly `strategy` and `workloads`. Each workload has exactly `workloadId`, `seed`, `scheduleHash`, `initialWorldFingerprint`, `entityCount`, `wallCount`, `expectedPerTick`, `oracleParityVerified`, and `runs`.

Each run has exactly:

- `runKey`, `repetition`, `executedTicks`;
- actual and expected mutation/removal/timer/immediate-transition counts;
- `fullScanInvocationCount`, `fullScanEntityVisitCount`, `survivorCopyCount`;
- `removalProcessedCount`, `timerProcessedCount`, `transitionProcessedCount`, `zeroCandidateOpportunityCount`;
- `referenceOracleInvocationCount`, `indexedInvocationCount`, `hiddenFallbackCount`, `invariantMismatchCount`;
- the seven membership/carriage/fast-import counts defined by v3;
- `validCleanupProcessorSamples`, `validRunCleanupPhaseSamples`;
- `wholeTickMilliseconds`, `cleanupProcessorMilliseconds`, `runCleanupPhaseMilliseconds`, `captureOffWholeTickMilliseconds`.

No producer field may be silently dropped from validation. Changes require a new explicit contract version or an approved v4 amendment.

## 7. Canonical reports and semantic invariants

All report `reasons` fields are arrays of exact reason objects:

```json
{"code":"IDENTITY_MISMATCH","path":"captureIdentity.attemptId","expected":"a","observed":"b"}
```

Each reason object has exactly `code`, `path`, `expected`, and `observed`; unavailable values are JSON `null`. `reasons=[]` is required for a successful semantic verdict and a nonempty array is required for rejection/Hold.

Every report includes exact `identity`, `provenance`, and `inputHashes` objects. There are no generated timestamps or ignored presentation fields in canonical reports, so strict object equality is deterministic.

The performance and Cleanup admission reports have exactly `schemaVersion`, `evidenceContractVersion`, `verdict`, `reasons`, `identity`, `provenance`, and `inputHashes`. Performance provenance has exactly `metricsSha256` and `performanceValidatorSha256`. Cleanup provenance has exactly `metricsSha256`, `cleanupValidatorSha256`, `workloadContractSha256`, and `activeStrategies`.

The calibration report preserves its exact semantic fields defined below and additionally has exact `evidenceContractVersion`, `reasons`, `identity`, `provenance`, and `inputHashes`. Calibration provenance has exactly `metricsSha256`, `activeStrategies`, `cleanupValidatorSha256`, `aggregatorSha256`, and `workloadContractSha256`. Its `toolHashes` uses `cleanupValidatorSha256` and `aggregatorSha256`; the ambiguous generic name `validatorSha256` is not a v4 Cleanup report field.

### 7.1 Performance admission report

`verdict` is one of:

- `ADMITTED`;
- `REJECTED_IDENTITY`;
- `REJECTED_RESOLUTION`;
- `REJECTED_SAMPLE_COUNT`;
- `REJECTED_REVISION`;
- `REJECTED_RUNTIME`.

`ADMITTED` requires `reasons=[]` and every performance invariant true. Every rejected verdict requires at least one reason and cannot coexist with a success claim. The producer CLI MUST accept `--output`, always preserve the report when the output is safe, and use exit `0` only for `ADMITTED`, exit `1` for every rejected verdict, and exit `2` only when no safe report can be written.

### 7.2 Cleanup admission report

- `ADMITTED`: `reasons=[]` and the exact schema, identity, workload, strategy, sample, allocation, invocation, maintenance, fallback, and oracle rules all pass.
- `REJECTED`: nonempty reasons.

CLI exit `0` is coherent only with `ADMITTED`; exit `1` is coherent only with `REJECTED`; exit `2` means no safe report could be written.

### 7.3 Calibration report

| `status` | `admitted` | `signalValid` | `attributionMaterial` | `thresholds` | `reasons` |
|---|---:|---:|---:|---|---|
| `READY` | `true` | `true` | `true` | non-null exact object | empty |
| `DEFERRED_NOT_MATERIAL` | `true` | `true` | `false` | non-null exact object | empty |
| `HOLD_INVALID_SIGNAL` | `true` | `false` | boolean observation | `null` | nonempty |
| `HOLD_INVALID_EVIDENCE` | `false` | `null` | `null` | `null` | nonempty |

`READY` and `DEFERRED_NOT_MATERIAL` also require `captureOffNonInterfering=true`, non-null `observations`, and the exact non-null `campaignRules`. `HOLD_INVALID_SIGNAL` requires non-null observations and `campaignRules=null`. `HOLD_INVALID_EVIDENCE` requires `captureOffNonInterfering=null`, `observations=null`, and `campaignRules=null`.

CLI exit `0` is coherent only with `READY` or `DEFERRED_NOT_MATERIAL`; exit `1` is coherent only with either Hold status; exit `2` means no safe report could be written.

### 7.4 One implementation of each verdict

- Performance CLI and manifest both call the same pure performance report builder.
- Cleanup CLI and calibration call the same pure Cleanup admission builder.
- Calibration CLI and manifest call the same pure calibration report builder.
- The manifest never contains a copied or independently reimplemented verdict formula.
- The manifest strictly loads each persisted report, recomputes the canonical report from metrics and frozen context, and requires exact object equality. Any field, reason, verdict, provenance, hash, or semantic difference is `PERSISTED_REPORT_MISMATCH` and terminal `HOLD`.
- Caller-supplied exit statuses are checked only for coherence after canonical recomputation. They are never verdict inputs.

### 7.5 Follow-up full-scan oracle gate

The current frozen workload schema proves full-scan algebra (`fullScanEntityVisitCount - survivorCopyCount = removalProcessedCount`) but does not provide an independently authored exact visit-count expectation for every workload, repetition, and tick. Algebraic self-consistency is not an independent oracle.

Until an explicit contract amendment approves that exact oracle and its frozen bytes, an otherwise `READY` or `DEFERRED_NOT_MATERIAL` S3-A result is finalized as `HOLD / HOLD_INVALID_EVIDENCE` with `FULL_SCAN_EXPECTATION_UNAPPROVED`. This follow-up gate takes precedence over the success rows in §9 and does not authorize a new capture or a workload-schema change.

The implementation has no boolean, environment variable, or command-line switch that can enable success transport. The future amendment must add the frozen oracle artifact, its approved digest, exact parser/comparison, identity binding, and negative tests in the same reviewed change; changing a single approval flag is not a valid activation path.

## 8. Attempt lifecycle and artifact states

The runner creates the attempt directory and atomically writes the provisional attempt manifest before build. The exact ordered stages are:

1. `preflight`;
2. `build`;
3. `guardRestore`;
4. `player`;
5. `markerValidation`;
6. `tickAttributionAdmission`;
7. `performanceAdmission`;
8. `cleanupAdmission`;
9. `calibration`;
10. `consistencyFinalization`.

Each stage has exactly `status`, `reasons`, and `artifacts`. Status is one of `NOT_RUN`, `PASS`, `DEFERRED`, or `HOLD`. At attempt start every stage is `NOT_RUN`. A stage transitions at most once to a terminal stage status; downstream stages remain `NOT_RUN` after early failure.

The attempt manifest is rewritten atomically after every transition and once more on finalization. Build, guard restore, Player, marker, admission, calibration, malformed-input, and consistency failures MUST all leave a final machine-readable attempt manifest. A final attempt cannot have `terminalStatus=NOT_RUN`; that value exists only in a provisional manifest.

Artifact records have exactly `path`, `state`, `sha256`, and `missingReasonCode`. `state` is `PRESENT`, `MISSING`, or `NOT_APPLICABLE`. `sha256` is non-null only for `PRESENT`; `missingReasonCode` is non-null only for `MISSING`.

Any future `PASS` or `DEFERRED` transport requires the exact required artifact set and every record `PRESENT`: `metrics`, `runtimeLog`, `preflightManifest`, `artifactManifest`, `tickAttribution`, `tickAttributionReport`, `tickAttributionValidator`, `performanceAdmission`, `cleanupAdmission`, `cleanupCalibration`, `performanceValidator`, `cleanupValidator`, `aggregator`, `workloadContract`, `runner`, `manifestTool`, `playerArtifact`, and `buildLog`. `tickAttributionAdmission` owns the raw capture, admitted report, and validator artifacts. `manifestTool` is the stable finalization-tool input; its stage reference and capture-identity hash, like every other required input, must bind to the same artifact bytes. The final attempt manifest does not hash or list itself as an artifact because a stable self-hash is impossible.

The attempt manifest has exactly `schemaVersion`, `evidenceContractVersion`, `manifestState`, `terminalStatus`, `authoritativeVerdict`, `identity`, `reasons`, `stages`, `artifacts`, and `exitStatus`. A provisional manifest uses `manifestState=PROVISIONAL`, `terminalStatus=NOT_RUN`, and `authoritativeVerdict=NOT_RUN`; a final manifest uses `manifestState=FINAL` and the §9 terminal mapping. Every terminal verdict is carried by this one manifest shape.

## 9. Final manifest and terminal mapping

The final manifest has no `runnerObservedStatus`. The authoritative field is `terminalStatus`; `authoritativeVerdict` explains it.

| Condition after canonical recomputation | `terminalStatus` | `authoritativeVerdict` |
|---|---|---|
| all artifacts/cohort valid, performance and Cleanup admitted, calibration `READY`, and the §7.5 exact oracle is approved and satisfied | `PASS` | `READY` |
| all artifacts/cohort valid, performance and Cleanup admitted, calibration `DEFERRED_NOT_MATERIAL`, and the §7.5 exact oracle is approved and satisfied | `DEFERRED` | `DEFERRED_NOT_MATERIAL` |
| otherwise-successful result while the §7.5 oracle remains unapproved | `HOLD` | `HOLD_INVALID_EVIDENCE` |
| build failed | `HOLD` | `HOLD_BUILD_FAILURE` |
| guard restore or live pre/post identity failed | `HOLD` | `HOLD_INVALID_EVIDENCE` |
| Player failed | `HOLD` | `HOLD_PLAYER_FAILURE` |
| marker validation failed | `HOLD` | `HOLD_MARKER_FAILURE` |
| Tick attribution validation rejected | `HOLD` | `HOLD_TICK_ATTRIBUTION_ADMISSION` |
| performance rejected | `HOLD` | `HOLD_PERFORMANCE_ADMISSION` |
| Cleanup rejected | `HOLD` | `HOLD_CLEANUP_ADMISSION` |
| calibration `HOLD_INVALID_SIGNAL` | `HOLD` | `HOLD_INVALID_SIGNAL` |
| any schema, duplicate, alias, hash, identity, cohort, persisted-report, or exit mismatch | `HOLD` | `HOLD_INVALID_EVIDENCE` |

Precedence is fail-closed in table order only after checking invalid evidence first: any consistency/identity/schema defect overrides a seemingly valid semantic report and yields `HOLD_INVALID_EVIDENCE`.

## 10. CLI and terminal transport

Manifest-tool exit and runner terminal state have intentionally different meanings.

- Manifest CLI exit `0`: canonical recomputation completed and a schema-valid provisional/final manifest was atomically written. Its terminal status may be `PASS`, `DEFERRED`, or `HOLD`.
- Manifest CLI exit `1`: input/output alias, unsafe output path, or atomic-write failure prevented canonical finalization. Malformed or missing input artifacts are recoverable evidence defects and MUST instead produce an atomic final `HOLD` manifest with exit `0` when the output path is safe.
- Runner exit `0`: final manifest says `PASS`.
- Runner exit `1`: final manifest says `DEFERRED` or `HOLD`. The JSON enum distinguishes the valid non-material terminal from Hold.
- Runner exit `2`: runner infrastructure could not preserve a valid final manifest.

After finalization the runner invokes the manifest tool's strict terminal readback. The readback rejects duplicate JSON members, non-final state, unexpected top-level/stage/artifact fields, unregistered reasons, and incoherent terminal/verdict pairs before emitting exactly one of:

- `Gameplay performance measurement: PASS`;
- `Gameplay performance measurement: DEFERRED`;
- `Gameplay performance measurement: HOLD`.

It MUST NOT infer terminal status from child exit codes and MUST NOT print `PASS` for `DEFERRED`.

## 11. Safe paths, alias rejection, and atomic writes

- Every input, tool, contract, and output path is resolved to an absolute normalized path before reading.
- Output MUST differ from every input/tool/contract by resolved path.
- If paths exist, output MUST also differ by `(st_dev, st_ino)` to reject symlink and hardlink aliases.
- Required semantic input artifacts MUST be pairwise distinct by resolved path and inode unless the identity matrix explicitly names the same artifact.
- Alias detection occurs before opening output for write. Rejection never truncates or replaces an input.
- All JSON/KV writes use a temporary file in the destination directory, flush, file `fsync`, atomic replace, and destination-directory `fsync` where supported.
- Inputs are hashed before strict parse and again immediately before final atomic replace. A change is `INPUT_MUTATED_DURING_VALIDATION` and terminal `HOLD_INVALID_EVIDENCE`.
- Standalone manifest CLI alias misuse returns nonzero without touching the aliased file. Inside the runner, the already-created independent attempt manifest records the alias failure and final `HOLD`.
- Final evidence output MUST be outside the repository and build payload root. Attempt directories include a runner-generated UUID and are created exclusively; an existing path is infrastructure failure, never a reusable attempt directory.

## 12. Required reason-code registry

v4 reason `code` is one of the following. More detail belongs in `path`, `expected`, and `observed`, not in an unregistered code.

- parsing/schema: `JSON_PARSE_FAILED`, `JSON_DUPLICATE_MEMBER`, `JSON_ROOT_INVALID`, `KV_MALFORMED_LINE`, `KV_DUPLICATE_KEY`, `SCHEMA_VERSION_INVALID`, `CONTRACT_VERSION_INVALID`, `FIELD_MISSING`, `FIELD_UNEXPECTED`, `FIELD_TYPE_INVALID`, `NUMERIC_DOMAIN_INVALID`, `CARDINALITY_MISMATCH`, `ORDER_MISMATCH`;
- identity/cohort: `IDENTITY_FIELD_MISSING`, `IDENTITY_FIELD_INVALID`, `IDENTITY_MISMATCH`, `SHA_FORMAT_INVALID`, `PRE_POST_HEAD_MISMATCH`, `PRE_POST_WORKTREE_MISMATCH`, `RUNTIME_TREE_MISMATCH`, `PLAYER_ARTIFACT_HASH_MISMATCH`, `BUILD_PAYLOAD_HASH_MISMATCH`, `METRICS_REVISION_MISMATCH`, `METRICS_HASH_MISMATCH`, `STAGE_MISMATCH`, `STRATEGY_MISMATCH`, `WORKLOAD_ID_MISMATCH`, `RUN_KEY_MISMATCH`, `TOOL_HASH_MISMATCH`, `CAMPAIGN_ID_MISMATCH`, `ATTEMPT_ID_MISMATCH`, `MIXED_COHORT`;
- verdict: `PERFORMANCE_REJECTED`, `CLEANUP_REJECTED`, `SIGNAL_INVALID`, `SEMANTIC_INVARIANT_INVALID`, `THRESHOLDS_REQUIRED`, `THRESHOLDS_FORBIDDEN`, `REASONS_COHERENCE_INVALID`, `EXIT_STATUS_MISMATCH`, `PERSISTED_REPORT_MISMATCH`, `FULL_SCAN_EXPECTATION_UNAPPROVED`;
- lifecycle/transport: `BUILD_FAILED`, `GUARD_RESTORE_FAILED`, `PLAYER_FAILED`, `MARKER_VALIDATION_FAILED`, `ARTIFACT_MISSING`, `STAGE_NOT_RUN`, `OUTPUT_INPUT_PATH_ALIAS`, `OUTPUT_INPUT_INODE_ALIAS`, `OUTPUT_ROOT_CONTAINMENT`, `INPUT_ARTIFACT_ALIAS`, `INPUT_MUTATED_DURING_VALIDATION`, `ATOMIC_WRITE_FAILED`, `FINAL_MANIFEST_UNAVAILABLE`, `BUILD_ROOT_INVALID`;
- compatibility: `UNSUPPORTED_ARTIFACT_VERSION`, `HISTORICAL_ARTIFACT_NOT_UPGRADABLE`.

## 13. v3-to-v4 change table

| v3 area | v3 state | v4 change | Closed finding |
|---|---|---|---|
| source identity | equality where fields exist | exact artifact matrix plus live pre/post recomputation and runtime-tree carrier | S3-EV-001 |
| performance admission | caller status could be the only manifest input | persisted schema-2 report, shared pure builder, canonical recomputation | S3-EV-002 |
| verdict semantics | enums checked but internal combinations incomplete | exact status tables, reasons/thresholds/signal/material invariants | S3-EV-013 |
| cardinality/types | partial integer/cardinality rules | required repetitions, exact bool-safe integer domains and exact order | S3-EV-003 |
| strategy identity | dual legacy/captures representation tolerated | captures-only schema and exact stage/strategy/run-key matrix | S3-EV-014 |
| parsing | ordinary JSON and permissive KV | recursive JSON duplicate rejection; duplicate/malformed/extra KV rejection | S3-EV-005, S3-EV-009 |
| early failure | downstream artifacts absent without one final truth | provisional manifest with fixed `NOT_RUN` lifecycle and finalization path | S3-EV-006 |
| terminal transport | `DEFERRED` could print `PASS` | final enum readback and exact PASS/DEFERRED/HOLD output | S3-EV-004 |
| output safety | input could be overwritten by output | path/inode alias rejection, double input hash, atomic replace | S3-EV-015 |
| report drift | manifest duplicated partial checks | one pure builder per verdict and exact persisted-vs-recomputed comparison | S3-EV-013 |
| historical evidence | schema-version policy incomplete | no automatic upgrade; unsupported artifacts stay historical | completion re-audit requirement |
| runner-observation field | computed value mislabeled as observation | `runnerObservedStatus` removed; `terminalStatus` is canonical | S3-EV-004 |

v4 does not claim to repair the permanently unavailable historical red logs identified by S3-EV-008. New remediation red evidence cannot be represented as a replacement for that gap.

## 14. Artifact version and compatibility matrix

| Artifact/version | Read for historical diagnostics | Input to v4 negative tests | Can yield v4 `PASS`/`DEFERRED` | Policy |
|---|:---:|:---:|:---:|---|
| v3 contract document | yes | yes | no | preserved historical predecessor; superseded for new remediation artifacts |
| workload contract schema 2 | yes | yes | yes, only by exact approved hash | unchanged frozen input; does not attest attempt identity |
| metrics schema 1 | yes | yes | no | missing Player-echoed v4 capture identity; no wrapping or upgrade |
| metrics schema 2 + contract 4 | yes | yes | yes | target producer shape |
| preflight/captured KV schema 1 | yes | yes | no | unsupported historical evidence |
| preflight/captured KV schema 2 + contract 4 | yes | yes | yes | target runner shape |
| performance stdout-only metrics verdict | yes | yes | no | no persisted authoritative artifact |
| performance formal report schema 1 | yes | yes | no | historical campaign shape, not v4 |
| performance report schema 2 + contract 4 | yes | yes | yes | target canonical report |
| Cleanup admission schema 1 | yes | yes | no | historical only |
| Cleanup admission schema 2 + contract 4 | yes | yes | yes | target canonical report |
| calibration schema 1 | yes | yes | no | historical only |
| calibration schema 2 + contract 4 | yes | yes | yes | target canonical report |
| evidence manifest schema 2 | yes | yes | no | known false-PASS surface; never upgrade in place |
| attempt manifest schema 4 + contract 4 | yes | yes | yes | target provisional/final truth |
| any unknown/future schema | no | yes | no | `UNSUPPORTED_ARTIFACT_VERSION` until explicit amendment |

An old artifact remains byte-preserved historical evidence. v4 tooling may report why it is unsupported, but MUST NOT rewrite, relabel, fill missing identity from command-line arguments, or issue a v4 terminal result from it.

## 15. Minimal metrics-producer amendment required by this draft

The current Player metrics schema has revision, stage, strategy, workload, and run data but does not carry campaign/attempt nonce, runtime-tree, Player artifact, build-payload, or harness identity. An external manifest can bind a file hash after the fact, but it cannot prove that the Player produced that file for this attempt or prevent a same-revision historical file from being rewrapped.

Therefore v4 requires a bounded allowlist amendment for:

- `Assets/_Features/UI/UI_Composition/Runtime/GameplayPerformancePlayerProbe.cs`;
- its `.meta` only if Unity changes it;
- focused tests needed to prove exact argument-to-metrics echo and schema-2 output.

The probe change is limited to parsing the runner-owned capture identity and serializing the §6 fields. It MUST NOT change gameplay simulation, Cleanup semantics, timing region boundaries, allocation measurement, workload execution, or production executor selection.

If approved, validation for this scope is:

- focused Cleanup Slice 3 probe/schema tests;
- the Python v4 suites and runner-lifecycle tests;
- `./run_tests.sh core`;
- `./run_tests.sh ui` because the probe is owned by UI Composition;
- no new `gameplay-performance` capture;
- no broad unfiltered `full` unless separately justified.

## 16. Approval and implementation gate

The 2026-08-28 KST user approval explicitly covers both:

1. this Evidence Contract v4 as the normative harness contract; and
2. the minimal metrics-producer allowlist amendment in §15.

The R0 approval gate is satisfied. Production evidence tools, `run_tests.sh` orchestration, metrics-producer identity echo, and existing test expectations may now change only within this contract and the remediation allowlist.

After approval, implementation order is fixed:

1. preserve formal negative-test red evidence under `/mnt/d/J2M/evidence/<new-remediation-id>/red/`;
2. implement identity/cohort and authoritative-verdict P0 closure;
3. implement strict schema/parsing and lifecycle/transport P1 closure;
4. preserve same-revision green evidence;
5. perform a fresh independent red-matrix re-audit without a new official capture.

Historical closeout record: the first v4 remediation pass preserved red evidence under `/mnt/d/J2M/evidence/20260827T204003Z-cleanup-s3-v4-remediation/red/`, same-package green evidence, and an independent negative-matrix re-audit. The §7.5 follow-up finding supersedes that pass's terminal-success eligibility: repository Slice 3 remains Hold, no official gameplay-performance capture was run, and no v3 artifact was upgraded.
