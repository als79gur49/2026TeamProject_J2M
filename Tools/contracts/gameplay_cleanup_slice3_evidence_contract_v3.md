# Cleanup Slice 3 Evidence Contract v3

## Authority

- Admission JSON `verdict` is authoritative for workload, strategy, semantic, and provenance admission.
- Calibration JSON `status` is interpreted only after Cleanup admission is `ADMITTED`.
- CLI exit status transports the JSON result; it is not an independent source of truth.
- The evidence manifest records artifacts and verifies cross-file consistency. It must not report `PASS` from caller-provided exit statuses alone.
- Official campaign revision, manifest, metrics, strategy, workload, and tool consistency remains an admission responsibility even when an S3-A manifest is internally consistent.

## Exact schema

- Every JSON document root must be an object.
- Count, tick, repetition, seed, entity count, Wall count, and mutation count fields must be JSON integers; booleans and numerically equal floats are invalid.
- Strategy arrays must match the exact set and order allowed by the stage.
- Each expected workload ID must occur exactly once per strategy and no additional workload is allowed.
- Every `(strategy, workloadId, repetition)` key must be unique.
- Active strategies must contain exactly the same ordered `(workloadId, repetition)` keys.
- Missing, duplicate, or additional workloads and repetitions are rejected.
- Schema-version changes require an explicit contract amendment and matching validator tests.

## Status coherence

- Cleanup admission exit `0` requires `verdict == ADMITTED`; nonzero requires `verdict == REJECTED`.
- Calibration exit `0` requires `status == READY` or `DEFERRED_NOT_MATERIAL`.
- Calibration `READY` and `DEFERRED_NOT_MATERIAL` require admitted Cleanup evidence.
- `HOLD_INVALID_SIGNAL` and `HOLD_INVALID_EVIDENCE` are nonzero results and never authorize S3-B/S3-C.
- A missing or unexecuted stage is `NOT_RUN`; it is never treated as `PASS`.

## Provenance coherence

- The actual metrics SHA-256 must equal every admission and calibration `metricsSha256`.
- Actual validator, aggregator, and workload-contract SHA-256 values must equal the corresponding provenance values.
- Active strategy, stage, revision, runtime-tree, harness, and campaign identity must agree wherever those fields are present.
- The captured artifact manifest metrics/runtime-log hashes must equal the actual files.
- Changing an input artifact after a verdict was created invalidates that verdict and its evidence manifest.

## S3-A terminal mapping

- `READY` with admitted, internally consistent evidence: `PASS`, allowing only a request to proceed to S3-B.
- `DEFERRED_NOT_MATERIAL` with admitted, internally consistent evidence: `DEFERRED`; S3-B/S3-C remain forbidden.
- Performance admission failure: `HOLD_PERFORMANCE_ADMISSION`; S3-B/S3-C remain forbidden.
- Invalid signal, rejected Cleanup admission, missing evidence, or consistency mismatch: `HOLD`; S3-B/S3-C remain forbidden.
