# Cleanup Slice 3 Evidence Contract v5 — D1 Design Draft

- Status: `DRAFT / PROPOSED — NOT APPROVED, NOT ACTIVE`
- Drafted: 2026-08-29 KST
- Base contract: `gameplay_cleanup_slice3_evidence_contract_v4.md`
- Base contract SHA-256: `6576219f28fa46197227aa4efa86fe809dfda3d1e31f6beeefe16b3aa2b2d8a7`
- Workload semantic artifact: `gameplay_cleanup_slice3_workloads_v3.json`
- Full-scan oracle artifact: `gameplay_cleanup_slice3_full_scan_oracle_v1.json`
- Authorization trust model: detached Ed25519 approval receipt; no key is enrolled by this draft
- Design authority: D1 approval required
- Implementation authority: none until a separate I2 exact-scope approval
- Activation authority: none until D2 exact integrated-diff approval
- Official capture authority: none until a separate M1 MeasurementAuthorization/Measurement Goal approval

This draft is an exact delta contract over the exact v4 bytes named above. Every v4 requirement remains normative unless this document explicitly replaces it. If the base digest differs, inheritance fails with `BASE_CONTRACT_HASH_MISMATCH`; an implementation MUST NOT guess or inherit from a different v4 file.

`MUST`, `MUST NOT`, `REQUIRED`, and `EXACT` are normative after D1 approval. Before D1 this file is design material only and cannot produce a v5 terminal result.

## 1. Contract classification

| Area | Classification | v5 rule |
|---|---|---|
| Cleanup order, authoritative state, result, lifetime, occupancy, trace, hash and replay parity | Gameplay StrongContract | unchanged from v4; outside v5 evidence implementation |
| Ordinary production Cleanup executor | Runtime CurrentPolicy | full scan remains selected through S3-A and S3-B |
| Evidence schema, oracle, authorization, lifecycle and terminal transport | Harness contract | this v5 delta after D1/I2/D2 only |
| Allocation/timing signal adoption | Separate E1/I3/D3 contract | no signal becomes approved merely because v5 exists |
| Official measurement | M1 authorization | forbidden without exact MeasurementAuthorization bytes and digest |
| S3-B/S3-C production | Separate continuation goals | not authorized by v5 |

## 2. Artifact and schema version matrix

Every JSON parser rejects duplicate keys at every depth, unknown fields, missing fields, non-finite numbers and bool-as-int. Every KV parser retains v4 strictness. The exact target versions are:

| Artifact | Format | `schemaVersion` | `evidenceContractVersion` |
|---|---|---:|---:|
| preflight manifest | strict KV | `3` | `5` |
| captured artifact manifest | strict KV | `3` | `5` |
| performance metrics | strict JSON | `3` | `5` |
| performance admission report | strict JSON | `3` | `5` |
| Cleanup admission report | strict JSON | `3` | `5` |
| Cleanup calibration report | strict JSON | `3` | `5` |
| attempt evidence manifest | strict JSON | `5` | `5` |
| workload semantic contract | strict JSON | `3` | `5` |
| full-scan oracle artifact | strict JSON | `1` | `5` |
| measurement backend contract | strict JSON | `1` | `5` |
| allocation signal contract | strict JSON | `1` | `5` |
| measurement procedure | strict JSON | `1` | `5` |
| canonical threshold artifact | strict JSON | `1` | `5` |
| non-official build-validation manifest | strict JSON | `1` | `5` |
| Measurement Goal | strict JSON | `1` | `5` |
| MeasurementAuthorization/campaign plan | strict JSON | `1` | `5` |
| MeasurementAuthorization approval receipt | strict JSON | `1` | `5` |
| K1 authorization trust root | strict JSON | `1` | `5` |
| pre-authorization terminal manifest | strict JSON | `1` | `5` |
| campaign evidence ledger/final manifest | strict JSON | `1` | `5` |
| campaign aggregation report | strict JSON | `1` | `5` |
| post-final readback receipt | strict JSON | `1` | `5` |

v1-v4 artifacts are historical or negative-test inputs only. They cannot be wrapped, filled, renamed or combined into v5 `PASS`/`DEFERRED`. Mixed versions are `UNSUPPORTED_ARTIFACT_VERSION` plus `MIXED_COHORT` when cohort mixing is also observed.

## 3. Approved design artifacts and external digests

D1 approval identifies the exact external SHA-256 of:

1. this contract;
2. `gameplay_cleanup_slice3_workloads_v3.json`;
3. `gameplay_cleanup_slice3_full_scan_oracle_v1.json`;
4. the D1 review-vector artifact, including this contract's exact approval-message grammar and authorization/campaign schemas.

E0-D separately identifies the exact external SHA-256 of the E0 protocol artifact as design-only, not as official evidence. D1 approval does not imply E0-D approval, and neither approval authorizes execution.

The oracle and workload artifacts do not contain their own digest. Exact approved digests are external approval facts compiled into the I2 implementation and repeated in the future MeasurementAuthorization. The future parser implementation digest is not a D1 design fact; it is bound at D2 after same-revision implementation review.

D1 does not invent, generate, store or enroll an approval private key. Before M1, a separate `K1` approval MUST enroll one exact Ed25519 public key and its exact trust-root artifact digest. With no K1-enrolled key, every purported authorization is untrusted and the only allowed authorization verdict is `HOLD_MEASUREMENT_AUTHORIZATION`. A caller-supplied JSON digest, approval boolean, environment variable, CLI flag, branch name or commit message is not a trust root.

## 4. v5 identity vocabulary

The v4 identity is inherited and extended with these exact fields:

- `baseEvidenceContractSha256`: exact v4 digest stated in this document;
- `evidenceContractSha256`: exact D1-approved v5 bytes;
- `captureBackend`: exact enum `Mono` or `IL2CPP`;
- `allocationSignalId`: non-empty identifier approved by E1; before E1 no official authorization may be issued;
- `allocationSignalContractSha256`: SHA-256 of the exact E1/D3-approved allocation-signal contract bytes;
- `measurementBackendId`: non-empty identifier approved by E1;
- `measurementBackendContractSha256`: SHA-256 of the exact E1/D3-approved backend contract bytes;
- `measurementProcedureSha256`: SHA-256 of the exact adopted timing/allocation procedure bytes;
- `workloadContractSha256`: exact approved v3 workload bytes;
- `fullScanOracleSha256`: exact approved oracle bytes;
- `fullScanOracleParserSha256`: exact D2-approved parser implementation bytes;
- `measurementAuthorizationSha256`: SHA-256 of the exact authorization/campaign-plan artifact;
- `measurementAuthorizationApprovalReceiptSha256`: SHA-256 of the exact detached approval receipt;
- `measurementGoalSha256`: SHA-256 of the exact Measurement Goal bytes approved by M1;
- `authorizationId`: exact non-empty identifier from that artifact;
- `slotId`: exact finite slot identifier from that artifact.

The following v4 fields retain their exact spelling and semantics: campaign/attempt/ordinal/kind/nonce, stage/active strategies, HEAD/worktree/runtime tree, Player/build payload, runner/tool/harness hashes, resolution and capture schedule.

Every required v5 carrier MUST contain every applicable extension field. There is no fallback to command-line-only values. Metrics cannot contain their own hash; downstream carriers bind the metrics hash as in v4.

### 4.1 Exact hash record order

`harnessSha256` uses one LF-terminated lowercase `label=sha256` record in this order:

1. `runnerSha256`;
2. `performanceValidatorSha256`;
3. `cleanupValidatorSha256`;
4. `aggregatorSha256`;
5. `manifestToolSha256`;
6. `workloadContractSha256`;
7. `fullScanOracleSha256`;
8. `fullScanOracleParserSha256`;
9. `measurementProcedureSha256`;
10. `allocationSignalContractSha256`;
11. `measurementBackendContractSha256`.

The external `measurementAuthorizationSha256` is not included in the authorization artifact itself. It is appended to capture identity and downstream provenance after the artifact bytes are finalized.

### 4.2 Acyclic hash dependency graph

The only permitted dependency direction is:

```text
v4 -> v5 contract
workload -> full-scan oracle
contract/workload/oracle/parser/tools/backend/signal/procedure -> harness
Measurement Goal -> MeasurementAuthorization
clean revision/runtime/harness/Goal/backend/signal/procedure/finite slots -> MeasurementAuthorization
authorization digest + Goal digest -> exact approval message
K1 trust root + M1 signature over that message -> approval receipt
authorization + approval receipt -> attempts -> campaign final manifest
```

The reverse edges are forbidden. In particular, an authorization does not contain its own digest or receipt digest; a Goal does not contain the authorization digest; a harness binary does not contain a future authorization digest; and workload/oracle references are one-way only.

## 5. Exact v5 workload vocabulary

The only D1 workload bytes are the externally approved bytes of `gameplay_cleanup_slice3_workloads_v3.json`.

- Workload order is target then stress.
- IDs are exactly `cleanup-s3-target-generic-inert-v3` and `cleanup-s3-stress-generic-inert-v3`.
- `wallCount` and `inertEntityCount` are forbidden. `syntheticEntityTypeNoneCount` describes authored `EntityType.None` provenance only and does not claim schedule inactivity.
- Legacy `*-wall-*-v2` IDs are forbidden in v5 metrics and reports.
- `scheduleCanonicalVersion=2` truthfully states that the unchanged producer schedule hash algorithm still uses its v2 canonical byte format. The workload schema version does not relabel those source bytes.
- Exactly 3 repetitions, 200 warm-up ticks and 200 measured ticks per repetition are required. Warm-up engine ticks are exactly `2000..2199`; measured engine ticks are exactly `3000..3199`, while serialized measured ordinals remain `1..200`. Each timing, capture-off and reference phase uses its contract-declared fresh instance and completes its own warm-up before measurement. The stress cold first tick has 80 mutations; its measured steady-state expectation is 96 only after that warm-up. A later approved measurement-procedure amendment must replace the whole coordinate/instance contract and start a new campaign identity.
- Warm-up observations never satisfy measured cardinality.

## 6. Metrics delta from v4

All v4 performance fields remain. `cleanupSlice3Calibration` changes to schema 3/contract 5 and replaces the following fields.

### 6.1 Workload fields

Each workload has exactly:

- `workloadId`, `seed`, `scheduleHash`, `initialWorldFingerprint`, `entityCount`, `syntheticEntityTypeNoneCount`;
- `expectedPerTick`;
- `cleanupProcessorCommitOracleParityVerified`;
- `fullScanExpectationVerified`;
- `runs`.

`wallCount` and generic `oracleParityVerified` are forbidden.

### 6.2 Candidate vocabulary

The v4 run fields `removalCandidateCount`, `timerCandidateCount`, and `immediateTransitionCandidateCount` are replaced by:

- `rawRemovalPredicateMatchCount`;
- `rawTimerPredicateMatchCount`;
- `rawImmediateTransitionPredicateMatchCount`.

Their expected fields use the same `raw*` prefix. They describe structural full-scan predicate membership, not survivor-only processing. `removalProcessedCount`, `timerProcessedCount`, and `transitionProcessedCount` retain actual processing semantics and removal precedence.

The seven maintenance/carriage fields retain their v4 spelling. No B/C producer is authorized by this rename.

### 6.3 Independent per-tick full-scan observations

Each run adds `perTickFullScanObservations`. It is an ordered array of exactly `executedTicks` objects. Each object has exactly:

- `runKey`;
- `tickOrdinal`;
- `engineTickIndex`;
- `fullScanInvocationCount`;
- `fullScanEntityVisitCount`;
- `survivorCopyCount`;
- `removalProcessedCount`.

`tickOrdinal` is exactly `1..executedTicks`; no gaps, duplicates, sorting repair or alternate zero-based spelling are allowed. `engineTickIndex` is exactly `2999 + tickOrdinal`, hence `3000..3199`. Every `runKey` equals its containing run key. Serialized aggregate totals are recomputed from this array and MUST exactly equal the run totals. A compensating `+1/-1` mutation therefore fails even if the aggregate remains unchanged.

### 6.4 Oracle parity fields

`cleanupProcessorCommitOracleParityVerified` is recomputed only from the independent CleanupProcessor commit-operation reference contract and exact invocation/mismatch cardinality. It does not claim whole `RunCleanupPhase` parity.

`fullScanExpectationVerified` is recomputed only by the v5 oracle parser/comparator after exact identity join. Producer booleans are observations and MUST equal canonical recomputation; they are never verdict inputs by themselves.

## 7. Full-scan oracle parser/comparison API

The I2 parser implementation MUST be a pure module that imports neither production Cleanup code, producer diagnostics nor producer helper/formula code.

Conceptual API:

```text
load_exact_workload(path, expectedWorkloadSha256)
  -> ExactWorkload

load_exact_oracle(path, expectedOracleSha256, ExactWorkload)
  -> ExactOracle

expand_exact_oracle(ExactOracle, ExactWorkload)
  -> ordered map[(workloadId, repetition, tickOrdinal, engineTickIndex)] = ExpectedFullScanTuple

compare_full_scan(metricsRuns, ExactOracle)
  -> ordered reasons + recomputed per-run totals + verified boolean
```

The loader MUST:

- hash exact bytes before strict parsing and again before final verdict emission;
- require the exact schema/contract/oracle ID and workload digest;
- enforce object field allowlists, array order, numeric domains and inclusive range cardinality;
- reject overlapping or missing repetition/tick ranges;
- expand exactly 2 workloads × 3 repetitions × 200 measured ticks;
- reject actual missing/extra/reordered tuples before value comparison;
- join by exact workload ID, repetition, tick ordinal and engine tick index;
- independently expand the structured initial entity generator and ordered schedule blocks from the workload artifact, recreate their canonical UTF-8 bytes, and verify the frozen initial-world and schedule hashes without importing production `BuildSchedule`, `ScheduledEntityId`, fingerprint helpers or reference-oracle code;
- distinguish measured ordinals from engine tick indices and reject cold-first-tick 80 as the stress measured recurrence of 96;
- compare all four fields independently;
- recompute aggregate totals from actual tuples and compare them with serialized totals.

Same-producer algebra such as `visits - survivors = removals` remains a secondary invariant and never substitutes for the oracle.

### 7.1 Supporting strict artifact roots

The measurement backend contract root has exactly `schemaVersion=1`, `evidenceContractVersion=5`, `artifactKind="CLEANUP_S3_MEASUREMENT_BACKEND"`, `measurementBackendId`, `captureBackend`, `unityScriptingBackend`, `buildMode`, `developmentBuild`. `captureBackend` is therefore owned directly by both this contract and MeasurementAuthorization, and MUST equal the backend contract value.

The allocation signal contract root has exactly `schemaVersion=1`, `evidenceContractVersion=5`, `artifactKind="CLEANUP_S3_ALLOCATION_SIGNAL"`, `allocationSignalId`, `measurementBackendId`, `api`, `threadScope`, `measurementInterval`, `unit`, `livenessControl`, `contaminationCeilingBytes`, `negativeDeltaPolicy`.

The measurement procedure root has exactly `schemaVersion=1`, `evidenceContractVersion=5`, `artifactKind="CLEANUP_S3_MEASUREMENT_PROCEDURE"`, `procedureId`, `measurementBackendContractSha256`, `allocationSignalContractSha256`, `workloadContractSha256`, `warmupTicks`, `sampleTicks`, `repetitions`, `phaseOrder`, `repetitionIsolation`, `retryPolicy`.

The canonical threshold artifact root has exactly `schemaVersion=1`, `evidenceContractVersion=5`, `artifactKind="CLEANUP_S3_THRESHOLDS"`, `calibrationReportSha256`, `measurementProcedureSha256`, `thresholds`. `thresholds` has exactly `cleanupProcessorMaterialImprovementPercent`, `runCleanupPhaseContainmentRequiredImprovementPercent`, `runCleanupPhaseContainmentMethod`, `targetWholeTickBenefitPercent`, `bWholeTickMaintenanceTaxCeilingPercent`, `bAllocationTaxCeilingBytesPerTick`, `targetAllocationRegressionCeilingBytesPerTick`, `targetAllocationRegressionCeilingPercent`, `stressWholeTickRegressionCeilingPercent`, `stressAllocationRegressionCeilingBytesPerTick`, `captureOffWholeTickRegressionCeilingPercent`, `attributionMinimumCleanupProcessorMilliseconds`, `attributionMinimumWholeTickSharePercent`, `maximumCalibrationNoisePercent`.

The non-official build-validation manifest root has exactly `schemaVersion=1`, `evidenceContractVersion=5`, `artifactKind="CLEANUP_S3_BUILD_VALIDATION"`, `authorizedHeadSha`, `authorizedWorktreeSha256`, `authorizedRuntimeTreeSha256`, `measurementBackendContractSha256`, `harnessSha256`, `playerArtifactSha256`, `buildPayloadSha256`, `validationStatus`, `validationReasons`. It is produced before M1 and `validationStatus` MUST be `VALIDATED`; it is not measurement evidence and cannot carry `PASS`/`DEFERRED`.

The Measurement Goal root has exactly `schemaVersion=1`, `evidenceContractVersion=5`, `artifactKind="CLEANUP_S3_MEASUREMENT_GOAL"`, `goalId`, `stage`, `activeStrategies`, `workloadContractSha256`, `measurementProcedureSha256`, `thresholdsSha256`, `requiredOfficialSlotCount`, `terminalDecisionRule`.

The pre-authorization terminal manifest root has exactly `schemaVersion=1`, `evidenceContractVersion=5`, `artifactKind="CLEANUP_S3_PREAUTH_TERMINAL"`, `manifestState="FINAL"`, `measurementGoalSha256`, `authorizationState`, `observedAuthorizationPath`, `observedAuthorizationSha256`, `reasons`, `terminalStatus="HOLD"`, `authoritativeVerdict`, `runnerExitCode=1`. `authorizationState` is `MISSING`, `MALFORMED`, `UNTRUSTED` or `OUT_OF_SCOPE`; `observedAuthorizationSha256` is null only for `MISSING` or unreadable malformed bytes. `authoritativeVerdict` is `HOLD_MEASUREMENT_AUTHORIZATION` for missing/unapproved/out-of-scope authority and `HOLD_INVALID_EVIDENCE` for malformed/hash/signature forgery. This is the sole terminal carrier before a valid authorization supplies campaign/slot identity; it contains no caller-invented campaign, authorization or slot fields and can never emit success.

The campaign aggregation report root has exactly `schemaVersion=1`, `evidenceContractVersion=5`, `artifactKind="CLEANUP_S3_CAMPAIGN_AGGREGATION"`, `campaignId`, `measurementAuthorizationSha256`, `orderedOfficialAttemptManifestSha256`, `performanceAdmissionReportSha256`, `cleanupAdmissionReportSha256`, `calibrationReportSha256`, `aggregationStatus`, `aggregationVerdict`, `reasons`. It is canonical recomputation over every `admitted-official` slot after exact slot completion. `aggregationStatus` is `ADMITTED` or `HOLD`; if admitted, `aggregationVerdict` is `READY` or `DEFERRED_NOT_MATERIAL`; if Hold, it is a coherent `HOLD_*` verdict. Warm-up and calibration-input slot-local outcomes cannot set this verdict.

The post-final readback receipt root has exactly `schemaVersion=1`, `evidenceContractVersion=5`, `artifactKind="CLEANUP_S3_POST_FINAL_READBACK"`, `campaignId`, `finalCampaignManifestSha256`, `readbackStatus`, `diagnosticReasons`, `runnerExitCode`. It is non-authoritative; `readbackStatus` is `VALID` or `FAILED`. For `VALID`, `runnerExitCode` is canonically derived from the immutable campaign FINAL (`PASS=0`, `DEFERRED=1`, `HOLD=1`); for `FAILED` it is 2. Caller-supplied exit status is never a verdict input, and the receipt never changes the already emitted campaign FINAL.

| Carrier | Required authority binding | Forbidden authority |
|---|---|---|
| MeasurementAuthorization | Goal, clean source/runtime, v5/workload/oracle/parser/harness/backend/signal/procedure/threshold/build-validation and finite slots | own digest, approval receipt digest, wildcard/retry slot |
| Player metrics and every report | authorization/receipt/Goal/campaign/slot plus all measurement identity digests | caller-only identity or self hash |
| attempt manifest | all above plus persisted report/artifact hashes and local attempt outcome | official campaign `PASS`/`DEFERRED` |
| campaign ledger | authorization/receipt, exact ordered slot consumption and attempt-manifest hashes | raw producer booleans or unlisted attempts |
| post-final receipt | immutable campaign FINAL digest and readback diagnostics | terminal rewrite or verdict downgrade |

## 8. MeasurementAuthorization schema 1

The authorization is a strict, finite, non-self-referential campaign plan. Its root has exactly:

- `schemaVersion=1`, `evidenceContractVersion=5`;
- `authorizationId`, `authorizationKind`, `campaignId`, `measurementGoalSha256`;
- `stage`, `activeStrategies`;
- `authorizedHeadSha`, `authorizedWorktreeSha256`, `authorizedRuntimeTreeSha256`;
- `baseEvidenceContractSha256`, `evidenceContractSha256`;
- `workloadContractSha256`, `fullScanOracleSha256`, `fullScanOracleParserSha256`;
- `measurementBackendId`, `measurementBackendContractSha256`;
- `captureBackend`;
- `allocationSignalId`, `allocationSignalContractSha256`, `measurementProcedureSha256`;
- `playerArtifactSha256`, `buildPayloadSha256`, `buildValidationManifestSha256`;
- `toolHashes`, `captureSettings`, `thresholdsSha256`, `attemptSlots`, `mandatorySlotIds`, `retryPolicy`, `cleanPolicy`, `lockPolicy`.

`authorizationKind` is exactly `S3_A_OFFICIAL`. The Player is built and validation-recorded once from the clean D3-activated revision before M1, without official measurement. M1 binds those exact three Player/build digests; official slots reuse that immutable build. Rebuilding after M1 invalidates the authorization instead of silently changing the measured executable.

### 8.1 Exact nested shapes

`toolHashes` has exactly `runnerSha256`, `performanceValidatorSha256`, `cleanupValidatorSha256`, `aggregatorSha256`, `manifestToolSha256`, `workloadContractSha256`, `fullScanOracleSha256`, `fullScanOracleParserSha256`, `measurementProcedureSha256`, `allocationSignalContractSha256`, `measurementBackendContractSha256`, `harnessSha256`, in that order.

`captureSettings` has exactly `width`, `height`, `warmupFrames`, `sampleFramesPerPhase`, `gameplayTickIntervalFrames`, `cleanupWarmupTicks`, `cleanupSampleTicks`, `repetitions`.

`thresholdsSha256` binds the exact canonical threshold artifact. That artifact can be produced only from an admitted noise-valid calibration bound to the same protocol/tool/workload/oracle identity. Null, caller-selected or hand-edited thresholds cannot authorize an official slot.

`attemptSlots` is a nonempty ordered array. Each object has exactly:

- `slotId`, `attemptId`, `attemptOrdinal`, `attemptKind`, `slotKind`;
- `strategyId`, `workloadId`, `repetition`, `executionOrder`;
- `captureNonce`.

`attemptKind` is `warm-up`, `calibration`, or `official`; `slotKind` is `discarded-warm-up`, `calibration`, or `admitted-official`. `captureNonce` is a unique lowercase 32-byte hexadecimal value generated before authorization. Every slot ID, attempt ID, ordinal and nonce is unique. Wildcards, caller-created slots and retries are forbidden.

`mandatorySlotIds` exactly equals all `attemptSlots[].slotId` in authorization order. `retryPolicy` is exactly:

```json
{"automaticRetryAllowed":false,"resultBasedRetryAllowed":false,"maximumAuthorizedRetrySlots":0,"authorizedRetrySlotIds":[]}
```

Every official campaign slot is single-use and mandatory. A campaign may stop after a Hold and preserve later slots as unconsumed, but it can emit `PASS` or `DEFERRED` only when every mandatory slot was consumed exactly once in authorization order. A new attempt after any consumed measurement requires a new authorization and campaign identity; there is no same-campaign replacement or aggregation choice.

`cleanPolicy` is exactly:

```json
{"requireEmptyPorcelainV1Z":true,"includeUntracked":true,"ignoreSubmodules":false,"ignoredFilesOutsideIdentity":true}
```

`lockPolicy` has exactly `path`, `nonBlocking`, `scope`, `ownerMetadataRequired`. The path is `/mnt/d/J2M/evidence/.locks/cleanup-s3-performance.lock`, `nonBlocking=true`, `scope=authorized-campaign`, and `ownerMetadataRequired=true`.

The authorization SHA-256, approval-receipt SHA-256, authorization ID, campaign ID and exact slot identity are carried by preflight, captured context, Player metrics, every report, every attempt manifest and the campaign ledger. A caller flag, attempt kind, environment variable or filename is not authorization.

### 8.2 Detached M1 approval and K1 trust root

The approval algorithm is exactly Ed25519 over the UTF-8 bytes of this LF-terminated message, with lowercase hexadecimal digests and no extra whitespace:

```text
cleanup-s3-m1-v1
measurementAuthorizationSha256=<64 lowercase hex>
measurementGoalSha256=<64 lowercase hex>
```

The strict approval receipt root has exactly `schemaVersion=1`, `evidenceContractVersion=5`, `approvalKind="S3_A_M1"`, `trustRootId`, `measurementAuthorizationSha256`, `measurementGoalSha256`, `signatureAlgorithm="Ed25519"`, `signatureBase64`.

The separately K1-approved strict trust-root artifact has exactly `schemaVersion=1`, `evidenceContractVersion=5`, `artifactKind="CLEANUP_S3_AUTHORIZATION_TRUST_ROOT"`, `trustRootId`, `signatureAlgorithm="Ed25519"`, `publicKeyBase64`, `publicKeyFingerprintSha256`. The fingerprint is SHA-256 of the exact decoded 32 public-key bytes. Unknown fields, multiple active keys, key lookup from the network, and repository-local key substitution are forbidden.

This D1 draft intentionally enrolls no key. Therefore M1 cannot be transported until K1 approves exact trust-root bytes and D2/D3-integrated code recognizes only that digest. This is a secure Hold, not an implementation default that may be bypassed.

### 8.3 Campaign ledger and terminal owner

The campaign ledger/final-manifest root has exactly:

- `schemaVersion=1`, `evidenceContractVersion=5`, `manifestState`;
- `campaignId`, `authorizationId`, `measurementAuthorizationSha256`, `measurementAuthorizationApprovalReceiptSha256`;
- `authorizedSlotIds`, `mandatorySlotIds`, `slotConsumptions`, `orderedAttemptManifestSha256`, `unconsumedSlotIds`;
- `campaignAggregationReportSha256`, `campaignReasons`, `terminalStatus`, `authoritativeVerdict`.

`authorizedSlotIds` and `mandatorySlotIds` both exactly preserve all authorization slot IDs in order. `slotConsumptions` has one object per authorized slot, in the same order, with exactly `slotId`, `state`, `attemptManifestSha256`; `state` is `UNCONSUMED` or `CONSUMED`, and the digest is respectively null or lowercase 64-hex. No digest may occur twice. `orderedAttemptManifestSha256` is exactly the non-null digests in slot order. `unconsumedSlotIds` is exactly the IDs whose state is `UNCONSUMED`.

A campaign FINAL may retain unconsumed tail slots only when its terminal is `HOLD`; the first Hold/failed-infrastructure slot and every earlier slot must be consumed, and every later slot must remain unconsumed. In that case `campaignAggregationReportSha256` is null. `PASS` and `DEFERRED` require an empty `unconsumedSlotIds` array and a non-null, hash-valid campaign aggregation report.

The campaign ledger, not an individual attempt manifest, owns the official S3-A terminal truth. It rejects duplicate consumption, reordered attempts, missing mandatory slots for a success terminal, unlisted slots and result-selected replacement. `manifestState` uses the same lifecycle as §9. An attempt may describe its local admission outcome, but cannot independently emit official campaign `PASS` or `DEFERRED`.

## 9. Clean cohort and lifecycle delta

Every official attempt slot in an authorization requires an empty result from:

```text
git status --porcelain=v1 -z --untracked-files=all --ignore-submodules=none
```

before authorized-artifact validation and at every named v5 live-identity checkpoint. Dirty authorized cohort input is `WORKTREE_DIRTY` and stops before Player.

For schema-5 attempt manifests, this section explicitly replaces inherited v4 attempt terminal/coherence rules. A FINAL attempt has exact fields `attemptOutcome`, `attemptVerdict`, `terminalStatus`, `authoritativeVerdict`. `terminalStatus` and `authoritativeVerdict` are always `NOT_RUN`; official campaign terminal vocabulary is forbidden. `attemptOutcome` is `ADMITTED` or `HOLD`. If admitted, `attemptVerdict` is determined only by slot kind: `discarded-warm-up -> ADMITTED_WARMUP_DISCARDED`, `calibration -> ADMITTED_CALIBRATION_INPUT`, `admitted-official -> ADMITTED_OFFICIAL_SAMPLE`. If Hold, it is one inherited or v5 `HOLD_*` verdict with coherent reasons. No slot-local verdict can assert materiality, `READY`, `DEFERRED` or campaign success. Campaign recomputation consumes these local fields plus every persisted report; it never trusts a caller status.

Attempt and campaign manifest state is one of `PROVISIONAL`, `FINALIZING`, `FINAL`.

1. `PROVISIONAL` is created before authorized-artifact validation.
2. The runner completes named `authorizationValidation`, `lockAcquisition`, `authorizedArtifactValidation`, Player/admission/calibration stages while state remains `PROVISIONAL`. `authorizedArtifactValidation` rehashes the M1-bound Player executable, build payload and non-official build-validation manifest. Official attempts never build, rebuild or restore build output; inherited build/guard-restore stages are `NOT_APPLICABLE` for schema 5 official attempts.
3. `processFinalization` inventories every launched Unity/Player child, terminates only owned children under the existing contract, and proves zero owned survivors before any authoritative final emit.
4. `FINALIZING` is a non-authoritative candidate emitted only after all semantic stages and process finalization complete. It always has `terminalStatus=NOT_RUN` and `authoritativeVerdict=NOT_RUN`; it cannot carry `PASS` or `DEFERRED` authority.
5. The finalizer revalidates the detached authorization signature, exact unconsumed slot, campaign order, lock ownership, live HEAD/worktree/runtime identity and every input hash.
6. The attempt finalizer emits one authoritative local `FINAL` attempt manifest atomically. After an admitted nonterminal slot, the campaign owner atomically updates only the campaign `PROVISIONAL` ledger and proceeds to the next mandatory slot.
7. Only after every mandatory slot is consumed, or immediately after the first local Hold/failed-infrastructure slot, may the campaign enter `FINALIZING`. For an all-consumed cohort, the campaign aggregator emits and re-reads the canonical aggregation report before campaign finalization; for a Hold with an unconsumed tail, aggregation is skipped and the report digest remains null.
8. The campaign finalizer then emits the one authoritative campaign `FINAL` atomically. It never emits campaign FINAL merely because an intermediate attempt reached FINAL.
9. Strict post-final readback validates bytes/schema/hash and prints the terminal line. It does not re-attest live repository state or rewrite either FINAL.

Persistent mismatch at a named checkpoint is `REPOSITORY_IDENTITY_CHANGED`. Mutation-and-restore between checkpoints and mutation after final atomic emit remain explicit limitations. Cooperative locking does not claim to prevent unrelated editor/shell mutation.

Power loss or crash never resumes a `PROVISIONAL` or `FINALIZING` artifact into success. A process survivor blocks `FINAL`. If post-final readback fails after valid FINAL bytes exist, the runner exits 2 and may emit a separate non-authoritative readback receipt/log; it MUST NOT rewrite, downgrade or replace the authoritative FINAL bytes.

## 10. Canonical terminal mapping

Invalid schema, hash, identity, signature, slot, campaign order, lock, oracle or persisted-report consistency takes precedence. The official terminal rows describe the campaign FINAL, not an individual attempt.

| Condition | `terminalStatus` | `authoritativeVerdict` |
|---|---|---|
| all mandatory slots consumed exactly once in order with their slot-kind local admitted verdicts, campaign aggregation report admitted `READY`, all persisted reports admitted and oracle exact | `PASS` | `READY` |
| all mandatory slots consumed exactly once in order with their slot-kind local admitted verdicts, campaign aggregation report admitted `DEFERRED_NOT_MATERIAL`, all persisted reports coherent and oracle exact | `DEFERRED` | `DEFERRED_NOT_MATERIAL` |
| authorization or K1 trust root absent/unapproved/out of scope; pre-authorization terminal carrier only | `HOLD` | `HOLD_MEASUREMENT_AUTHORIZATION` |
| authorization/receipt malformed, signature invalid, or approved artifact hash mismatched/stale; pre-authorization terminal carrier only | `HOLD` | `HOLD_INVALID_EVIDENCE` |
| oracle missing/hash/parser/tuple mismatch | `HOLD` | `HOLD_INVALID_EVIDENCE` |
| calibration invalid signal | `HOLD` | `HOLD_INVALID_SIGNAL` |
| performance or Cleanup rejected | `HOLD` | inherited v4 stage-specific Hold |
| build/player/marker/guard failure | `HOLD` | inherited v4 stage-specific Hold |

`PASS`/`DEFERRED` remains impossible until D2 activates v5 and M1 approves exact authorization bytes. Implementations before D2 MUST force Hold even when every semantic field is otherwise ready.

## 11. v5 reason registry delta

All v4 reason codes are inherited except `FULL_SCAN_EXPECTATION_UNAPPROVED`, which remains valid only for v1-v4 compatibility diagnostics. v5 adds:

- `BASE_CONTRACT_HASH_MISMATCH`;
- `WORKTREE_DIRTY`;
- `REPOSITORY_IDENTITY_CHANGED`;
- `PERFORMANCE_LOCK_UNAVAILABLE`;
- `MEASUREMENT_AUTHORIZATION_REQUIRED`;
- `MEASUREMENT_AUTHORIZATION_HASH_MISMATCH`;
- `MEASUREMENT_AUTHORIZATION_SIGNATURE_INVALID`;
- `AUTHORIZATION_TRUST_ROOT_UNENROLLED`;
- `AUTHORIZATION_SCOPE_MISMATCH`;
- `ATTEMPT_SLOT_UNAUTHORIZED`;
- `ATTEMPT_SLOT_DUPLICATE`;
- `CAMPAIGN_ORDER_MISMATCH`;
- `CAMPAIGN_LEDGER_MISMATCH`;
- `MEASUREMENT_BACKEND_MISMATCH`;
- `ALLOCATION_SIGNAL_UNAPPROVED`;
- `ALLOCATION_SIGNAL_CONTRACT_MISMATCH`;
- `MEASUREMENT_PROCEDURE_MISMATCH`;
- `WORKLOAD_CONTRACT_HASH_MISMATCH`;
- `ORACLE_ARTIFACT_HASH_MISMATCH`;
- `ORACLE_PARSER_HASH_MISMATCH`;
- `ORACLE_EXPECTATION_MISMATCH`;
- `PER_TICK_OBSERVATION_MISMATCH`;
- `FULL_SCAN_OBSERVATION_ORDER_MISMATCH`;
- `FULL_SCAN_AGGREGATE_MISMATCH`;
- `PROCESS_INVENTORY_FAILED`;
- `PROCESS_SURVIVOR_DETECTED`;
- `POST_FINAL_READBACK_FAILED`.

Every reason remains an exact object with `code`, `path`, `expected`, `observed`. Free-form or dynamically constructed codes are forbidden.

## 12. Negative review matrix

D1 review vectors and later tests MUST cover:

- base contract/workload/oracle/parser/protocol/authorization hash mutation;
- missing/duplicate/unknown fields and bool-as-int;
- legacy wall-named workload or mixed v4/v5 carrier;
- missing/duplicate/reordered run/tick tuple;
- one expected or actual field mutation;
- two different ticks changed by `+1/-1` with unchanged aggregate;
- aggregate changed without tuple change and tuple changed without aggregate change;
- producer parity bool forged true/false;
- missing, forged, stale or wrong-slot authorization;
- missing/changed trust root, wrong key/signature/Goal digest, and authorization/Goal circular-hash attempts;
- duplicate/reordered/cherry-picked slot consumption, unlisted retries and attempt-level forged campaign success;
- dirty tracked, staged, untracked and submodule state;
- backend/signal/protocol mismatch;
- lock contention, signal/crash cleanup and stale lock-file semantics;
- repository mutation at every named pre-final checkpoint;
- persisted report mutation and terminal/exit forgery.

## 13. Activation and compatibility rules

D1 approval approves exact vocabulary and design bytes only. I2 separately approves tests and implementation paths. D2 separately approves the exact integrated diff and parser/tool hashes.

No boolean, environment variable, CLI flag, attempt kind or single approval flag can activate v5. The following are cumulative:

1. D1 exact contract/artifact approval;
2. I2 tests/implementation scope approval;
3. same-revision implementation and negative matrix;
4. D2 integrated-diff activation;
5. E1 approved allocation/timing protocol;
6. K1 exact Ed25519 public-key trust-root enrollment;
7. any required I3 implementation and D3 integration recognizing only the K1-approved trust-root digest;
8. clean committed revision, build-once validated Player payload and new campaign identity;
9. M1 exact MeasurementAuthorization/Measurement Goal signature approval.

Until all cumulative conditions hold, repository Slice 3 remains `Hold — valid evidence incomplete`; official capture and S3-B/S3-C are forbidden.
