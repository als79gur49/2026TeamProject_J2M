#!/usr/bin/env python3
"""Admission contract for Gameplay Cleanup Slice 3 calibration/campaign evidence."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
from typing import Any, Iterable

try:
    from Tools.gameplay_evidence_v4 import APPROVED_CLEANUP_S3_WORKLOAD_SHA256, EvidenceError, atomic_json, capture_input_snapshots, evidence_identity, load_json_object, reason, validate_v4_context_pair
except ModuleNotFoundError:
    from gameplay_evidence_v4 import APPROVED_CLEANUP_S3_WORKLOAD_SHA256, EvidenceError, atomic_json, capture_input_snapshots, evidence_identity, load_json_object, reason, validate_v4_context_pair


ADMITTED = "ADMITTED"
REJECTED = "REJECTED"
EXPECTED_WORKLOAD_IDS = (
    "cleanup-s3-target-wall-empty-v2",
    "cleanup-s3-stress-dense-v2",
)
DEFAULT_WORKLOAD_CONTRACT = (
    Path(__file__).resolve().parent
    / "contracts"
    / "gameplay_cleanup_slice3_workloads_v2.json"
)
EXPECTED_ALLOCATION_SIGNAL = "per-tick current-thread allocated-byte delta appended by Player probe"


def validate_cleanup_slice3(
    document: Any,
    active_strategies: Iterable[str],
    workload_contract: dict[str, Any] | None = None,
    *,
    require_v4: bool = False,
) -> tuple[str, list[str]]:
    if not isinstance(document, dict):
        return REJECTED, ["SCHEMA_MISMATCH: root must be an object"]
    expected_strategies = tuple(active_strategies)
    contract = workload_contract or _load_workload_contract(DEFAULT_WORKLOAD_CONTRACT)
    frozen_workloads = contract.get("workloads", {})
    reasons: list[str] = []
    if require_v4:
        _check_exact_fields(
            document,
            {
                "schemaVersion", "evidenceContractVersion", "stage", "activeStrategies",
                "repetitions", "warmupTicksPerRepetition", "sampleTicksPerRepetition",
                "allocationSignal", "diagnosticsOffNoOpAllocatedBytes", "captures",
                "frameAllocationCalibration",
            },
            "cleanupSlice3Calibration",
            reasons,
        )
    expected_schema = 2 if require_v4 else 1
    if not _is_json_int(document.get("schemaVersion")) or document.get("schemaVersion") != expected_schema:
        reasons.append(f"SCHEMA_MISMATCH: schemaVersion={document.get('schemaVersion')!r}")
    if require_v4:
        if document.get("evidenceContractVersion") != 4:
            reasons.append(
                "CONTRACT_VERSION_INVALID: evidenceContractVersion must equal 4"
            )
        if document.get("activeStrategies") != list(expected_strategies):
            reasons.append(
                "ACTIVE_STRATEGY_MISMATCH: activeStrategies must exactly match the requested stage"
            )
        if "strategy" in document or "workloads" in document:
            reasons.append(
                "SCHEMA_MISMATCH: legacy top-level strategy/workloads are forbidden in v4"
            )
        for tick_field in ("warmupTicksPerRepetition", "sampleTicksPerRepetition"):
            if not _is_json_int(document.get(tick_field)) or document.get(tick_field) <= 0:
                reasons.append(
                    f"NUMERIC_DOMAIN_INVALID: {tick_field} must be a positive integer"
                )
        if document.get("allocationSignal") != EXPECTED_ALLOCATION_SIGNAL:
            reasons.append(
                "SEMANTIC_INVARIANT_INVALID: allocationSignal must equal the approved producer signal"
            )
    diagnostics_off_bytes = document.get("diagnosticsOffNoOpAllocatedBytes")
    if not _is_json_int(diagnostics_off_bytes) or diagnostics_off_bytes != 0:
        reasons.append(
            "DIAGNOSTICS_OFF_ALLOCATION_NONZERO: "
            f"observed={diagnostics_off_bytes!r}"
        )
    declared_repetitions = document.get("repetitions")
    if declared_repetitions is None:
        reasons.append("CARDINALITY_MISMATCH: repetitions is required")
    elif (
        not _is_json_int(declared_repetitions) or declared_repetitions <= 0
    ):
        reasons.append(
            f"SCHEMA_MISMATCH: repetitions must be a positive integer, observed={declared_repetitions!r}"
        )
        declared_repetitions = None
    expected_stage = {
        ("A",): "S3-A",
        ("A", "B"): "S3-B",
        ("A", "B", "C"): "S3-C",
    }.get(expected_strategies)
    if expected_stage is None or document.get("stage") != expected_stage:
        reasons.append(
            "ACTIVE_STAGE_MISMATCH: "
            f"strategies={expected_strategies!r} expectedStage={expected_stage!r} "
            f"observedStage={document.get('stage')!r}"
        )

    captures_value = document.get("captures")
    if captures_value is None and isinstance(document.get("strategy"), str):
        captures_value = [
            {"strategy": document.get("strategy"), "workloads": document.get("workloads")}
        ]
    captures = captures_value if isinstance(captures_value, list) else []
    observed_strategies = tuple(
        capture.get("strategy")
        for capture in captures
        if isinstance(capture, dict) and isinstance(capture.get("strategy"), str)
    )
    if observed_strategies != expected_strategies:
        reasons.append(
            "ACTIVE_STRATEGY_MISMATCH: "
            f"expected={expected_strategies!r} observed={observed_strategies!r}"
        )

    identity_by_workload: dict[str, tuple[Any, ...]] = {}
    run_identity_by_workload: dict[tuple[str, int], tuple[Any, ...]] = {}
    bc_structure: dict[tuple[str, int], dict[str, tuple[Any, ...]]] = {}
    run_keys_by_strategy: dict[str, list[tuple[str, int]]] = {}
    for capture_index, capture in enumerate(captures):
        if not isinstance(capture, dict):
            reasons.append(f"SCHEMA_MISMATCH: captures[{capture_index}] must be an object")
            continue
        if require_v4:
            _check_exact_fields(capture, {"strategy", "workloads"}, f"captures[{capture_index}]", reasons)
        strategy = capture.get("strategy")
        workloads = capture.get("workloads")
        if not isinstance(strategy, str) or not isinstance(workloads, list):
            reasons.append(
                f"SCHEMA_MISMATCH: captures[{capture_index}] requires strategy/workloads"
            )
            continue
        strategy_run_keys = run_keys_by_strategy.setdefault(strategy, [])
        observed_workload_ids: list[str] = []
        for workload_index, workload in enumerate(workloads):
            location = f"{strategy}.workloads[{workload_index}]"
            if not isinstance(workload, dict):
                reasons.append(f"SCHEMA_MISMATCH: {location} must be an object")
                continue
            if require_v4:
                _check_exact_fields(
                    workload,
                    {
                        "workloadId", "seed", "scheduleHash", "initialWorldFingerprint",
                        "entityCount", "wallCount", "expectedPerTick", "oracleParityVerified", "runs",
                    },
                    location,
                    reasons,
                )
            workload_id = workload.get("workloadId")
            if not isinstance(workload_id, str):
                reasons.append(f"SCHEMA_MISMATCH: {location}.workloadId must be a string")
                continue
            observed_workload_ids.append(workload_id)
            if observed_workload_ids.count(workload_id) > 1:
                reasons.append(
                    f"WORKLOAD_CARDINALITY_MISMATCH: strategy={strategy} duplicate={workload_id!r}"
                )
            identity = (
                workload.get("seed"),
                workload.get("scheduleHash"),
                workload.get("initialWorldFingerprint"),
                workload.get("entityCount"),
                workload.get("wallCount"),
            )
            baseline_identity = identity_by_workload.setdefault(workload_id, identity)
            if identity != baseline_identity:
                reasons.append(
                    f"WORKLOAD_IDENTITY_MISMATCH: {location} expected={baseline_identity!r} "
                    f"observed={identity!r}"
                )
            frozen_identity = frozen_workloads.get(workload_id)
            if not isinstance(frozen_identity, dict) or any(
                workload.get(key) != frozen_identity.get(key)
                for key in (
                    "seed",
                    "scheduleHash",
                    "initialWorldFingerprint",
                    "entityCount",
                    "wallCount",
                )
            ):
                reasons.append(
                    f"FROZEN_WORKLOAD_IDENTITY_MISMATCH: {location} "
                    f"expected={frozen_identity!r} observed={identity!r}"
                )
            if not _is_sha256(workload.get("scheduleHash")) or not _is_sha256(
                workload.get("initialWorldFingerprint")
            ):
                reasons.append(f"WORKLOAD_IDENTITY_MISMATCH: {location} hashes must be SHA-256")
            for integer_key in ("seed", "entityCount", "wallCount"):
                if not _is_json_int(workload.get(integer_key)):
                    reasons.append(
                        f"SCHEMA_MISMATCH: {location}.{integer_key} must be an integer"
                    )
                elif (
                    integer_key in ("seed", "entityCount") and workload.get(integer_key) <= 0
                ) or (integer_key == "wallCount" and workload.get(integer_key) < 0):
                    reasons.append(
                        f"NUMERIC_DOMAIN_INVALID: {location}.{integer_key}={workload.get(integer_key)!r}"
                    )
            if workload.get("entityCount") != 256 or workload.get("wallCount") != 256:
                reasons.append(
                    f"WORKLOAD_IDENTITY_MISMATCH: {location} entity/Wall counts must equal 256"
                )
            if workload.get("oracleParityVerified") is not True:
                reasons.append(f"ORACLE_PARITY_MISSING: {location}")

            expected_per_tick = workload.get("expectedPerTick")
            if not isinstance(expected_per_tick, dict):
                reasons.append(f"SCHEMA_MISMATCH: {location}.expectedPerTick must be an object")
                expected_per_tick = {}
            elif require_v4:
                _check_exact_fields(
                    expected_per_tick,
                    {"mutations", "removalCandidates", "timerCandidates", "immediateTransitionCandidates"},
                    f"{location}.expectedPerTick",
                    reasons,
                )
            is_target = workload_id == "cleanup-s3-target-wall-empty-v2"
            is_stress = workload_id == "cleanup-s3-stress-dense-v2"
            frozen_expected = (
                {"mutations": 0, "removalCandidates": 0, "timerCandidates": 0,
                 "immediateTransitionCandidates": 0}
                if is_target
                else {"mutations": 96, "removalCandidates": 16, "timerCandidates": 32,
                      "immediateTransitionCandidates": 32}
                if is_stress
                else None
            )
            if frozen_expected is None or any(
                expected_per_tick.get(key) != value for key, value in frozen_expected.items()
            ):
                reasons.append(
                    f"SCHEDULE_COUNT_MISMATCH: {location}.expectedPerTick={expected_per_tick!r}"
                )
            for key in (
                "mutations",
                "removalCandidates",
                "timerCandidates",
                "immediateTransitionCandidates",
            ):
                if not _is_json_int(expected_per_tick.get(key)):
                    reasons.append(
                        f"SCHEMA_MISMATCH: {location}.expectedPerTick.{key} must be an integer"
                    )
                elif expected_per_tick.get(key) < 0:
                    reasons.append(
                        f"NUMERIC_DOMAIN_INVALID: {location}.expectedPerTick.{key}"
                    )

            runs = workload.get("runs")
            if not isinstance(runs, list) or not runs:
                reasons.append(f"SCHEMA_MISMATCH: {location}.runs must be non-empty")
                continue
            for run_index, run in enumerate(runs):
                run_location = f"{location}.runs[{run_index}]"
                if not isinstance(run, dict):
                    reasons.append(f"SCHEMA_MISMATCH: {run_location} must be an object")
                    continue
                if require_v4:
                    _check_exact_fields(
                        run,
                        {"runKey", "repetition", "executedTicks"}
                        | set(_RUN_INTEGER_FIELDS)
                        | {
                            "wholeTickMilliseconds", "cleanupProcessorMilliseconds",
                            "runCleanupPhaseMilliseconds", "captureOffWholeTickMilliseconds",
                        },
                        run_location,
                        reasons,
                    )
                ticks = run.get("executedTicks")
                repetition = run.get("repetition")
                run_key: tuple[str, int] | None = None
                if not _is_json_int(repetition) or repetition <= 0:
                    reasons.append(
                        f"RUN_CARDINALITY_MISMATCH: {run_location}.repetition={repetition!r}"
                    )
                else:
                    run_key = (workload_id, repetition)
                    if require_v4 and run.get("runKey") != f"{strategy}/{workload_id}/{repetition}":
                        reasons.append(
                            f"RUN_KEY_MISMATCH: {run_location}.runKey expected="
                            f"{strategy}/{workload_id}/{repetition} observed={run.get('runKey')!r}"
                        )
                    if run_key in strategy_run_keys:
                        reasons.append(
                            f"RUN_CARDINALITY_MISMATCH: strategy={strategy} duplicate={run_key!r}"
                        )
                    strategy_run_keys.append(run_key)
                if not _is_json_int(ticks) or ticks <= 0:
                    reasons.append(f"SAMPLE_COUNT_MISMATCH: {run_location}.executedTicks={ticks!r}")
                    continue
                for integer_key in _RUN_INTEGER_FIELDS:
                    if not _is_json_int(run.get(integer_key)):
                        reasons.append(
                            f"SCHEMA_MISMATCH: {run_location}.{integer_key} must be an integer"
                        )
                    elif run.get(integer_key) < 0:
                        reasons.append(
                            f"NUMERIC_DOMAIN_INVALID: {run_location}.{integer_key} "
                            f"must be nonnegative, observed={run.get(integer_key)!r}"
                        )
                run_identity = (
                    ticks,
                    run.get("expectedMutationCount"),
                    run.get("expectedRemovalCandidateCount"),
                    run.get("expectedTimerCandidateCount"),
                    run.get("expectedImmediateTransitionCandidateCount"),
                )
                if run_key is not None:
                    baseline_run_identity = run_identity_by_workload.setdefault(run_key, run_identity)
                    if run_identity != baseline_run_identity:
                        reasons.append(
                            f"WORKLOAD_IDENTITY_MISMATCH: {run_location} expected={baseline_run_identity!r} "
                            f"observed={run_identity!r}"
                        )

                expected_counts = {
                    "mutationCount": expected_per_tick.get("mutations", -1) * ticks,
                    "removalCandidateCount": expected_per_tick.get("removalCandidates", -1) * ticks,
                    "timerCandidateCount": expected_per_tick.get("timerCandidates", -1) * ticks,
                    "immediateTransitionCandidateCount": expected_per_tick.get(
                        "immediateTransitionCandidates", -1
                    )
                    * ticks,
                }
                for key, expected in expected_counts.items():
                    if run.get(key) != expected:
                        code = "TARGET_CANDIDATE_NONZERO" if is_target and key != "mutationCount" else "SCHEDULE_COUNT_MISMATCH"
                        reasons.append(
                            f"{code}: {run_location}.{key} expected={expected} observed={run.get(key)!r}"
                        )
                if any(
                    run.get(actual_key) != run.get(expected_key)
                    for actual_key, expected_key in (
                        ("mutationCount", "expectedMutationCount"),
                        ("removalCandidateCount", "expectedRemovalCandidateCount"),
                        ("timerCandidateCount", "expectedTimerCandidateCount"),
                        (
                            "immediateTransitionCandidateCount",
                            "expectedImmediateTransitionCandidateCount",
                        ),
                    )
                ):
                    reasons.append(f"SCHEDULE_COUNT_MISMATCH: {run_location} actual/expected totals differ")
                if require_v4:
                    if run.get("referenceOracleInvocationCount") != ticks:
                        reasons.append(
                            f"SEMANTIC_INVARIANT_INVALID: {run_location}.referenceOracleInvocationCount "
                            f"expected={ticks} observed={run.get('referenceOracleInvocationCount')!r}"
                        )
                    expected_zero_opportunities = ticks if is_target else 0
                    if run.get("zeroCandidateOpportunityCount") != expected_zero_opportunities:
                        reasons.append(
                            f"SEMANTIC_INVARIANT_INVALID: {run_location}.zeroCandidateOpportunityCount "
                            f"expected={expected_zero_opportunities} observed={run.get('zeroCandidateOpportunityCount')!r}"
                        )
                    visits = run.get("fullScanEntityVisitCount")
                    copies = run.get("survivorCopyCount")
                    removals = run.get("removalProcessedCount")
                    if strategy in ("A", "B") and (
                        not _is_json_int(visits) or not _is_json_int(copies) or
                        visits - copies != removals
                    ):
                        reasons.append(
                            f"SEMANTIC_INVARIANT_INVALID: {run_location} full-scan visit/copy/removal relation"
                        )

                for key in (
                    "validCleanupProcessorSamples",
                    "validRunCleanupPhaseSamples",
                ):
                    if run.get(key) != ticks:
                        reasons.append(
                            f"SAMPLE_COUNT_MISMATCH: {run_location}.{key} "
                            f"expected={ticks} observed={run.get(key)!r}"
                        )
                for key in (
                    "wholeTickMilliseconds",
                    "cleanupProcessorMilliseconds",
                    "runCleanupPhaseMilliseconds",
                    "captureOffWholeTickMilliseconds",
                ):
                    metric = run.get(key)
                    metric_error = _metric_error(metric, ticks)
                    if metric_error is not None:
                        reasons.append(
                            f"METRIC_SUMMARY_INVALID: {run_location}.{key}: {metric_error}"
                        )
                if run.get("hiddenFallbackCount") != 0:
                    reasons.append(f"HIDDEN_FALLBACK: {run_location}")
                if run.get("invariantMismatchCount") != 0:
                    reasons.append(f"INVARIANT_MISMATCH: {run_location}")
                expected_full_scan = ticks if strategy in ("A", "B") else 0
                expected_indexed = ticks if strategy == "C" else 0
                if (
                    run.get("fullScanInvocationCount") != expected_full_scan
                    or run.get("indexedInvocationCount") != expected_indexed
                ):
                    reasons.append(
                        f"STRATEGY_INVOCATION_MISMATCH: {run_location} "
                        f"full={run.get('fullScanInvocationCount')!r} indexed={run.get('indexedInvocationCount')!r}"
                    )

                structure = tuple(run.get(key) for key in _STRUCTURE_FIELDS)
                if strategy == "A" and any(value != 0 for value in structure):
                    reasons.append(f"A_MAINTENANCE_NONZERO: {run_location} values={structure!r}")
                if strategy in ("B", "C") and run_key is not None:
                    bc_structure.setdefault(run_key, {})[strategy] = structure
        if tuple(observed_workload_ids) != EXPECTED_WORKLOAD_IDS:
            reasons.append(
                f"WORKLOAD_CARDINALITY_MISMATCH: strategy={strategy} "
                f"expected={EXPECTED_WORKLOAD_IDS!r} observed={observed_workload_ids!r}"
            )

    baseline_run_keys: list[tuple[str, int]] | None = None
    for strategy in expected_strategies:
        observed_run_keys = run_keys_by_strategy.get(strategy, [])
        if declared_repetitions is not None:
            expected_run_keys = [
                (workload_id, repetition)
                for workload_id in EXPECTED_WORKLOAD_IDS
                for repetition in range(1, declared_repetitions + 1)
            ]
            if observed_run_keys != expected_run_keys:
                reasons.append(
                    f"RUN_CARDINALITY_MISMATCH: strategy={strategy} "
                    f"expected={expected_run_keys!r} observed={observed_run_keys!r}"
                )
        if baseline_run_keys is None:
            baseline_run_keys = observed_run_keys
        elif observed_run_keys != baseline_run_keys:
            reasons.append(
                f"RUN_CARDINALITY_MISMATCH: strategy={strategy} "
                f"expected={baseline_run_keys!r} observed={observed_run_keys!r}"
            )

    if "B" in expected_strategies and "C" in expected_strategies:
        for run_key, by_strategy in bc_structure.items():
            if by_strategy.get("B") != by_strategy.get("C"):
                reasons.append(
                    f"BC_MAINTENANCE_CARRIAGE_MISMATCH: run={run_key!r} values={by_strategy!r}"
                )
    reasons.extend(
        _validate_frame_allocation(
            document,
            identity_by_workload,
            expected_strategies,
        )
    )
    return (ADMITTED, []) if not reasons else (REJECTED, reasons)


_STRUCTURE_FIELDS = (
    "candidateMembershipCheckCount",
    "candidateMembershipAddCount",
    "candidateMembershipRemoveCount",
    "snapshotCandidateArrayCount",
    "snapshotCandidateCarriedItemCount",
    "fastImportCandidateItemCount",
    "fastImportSeparatePredicateRebuildEntityVisitCount",
)

_RUN_INTEGER_FIELDS = (
    "mutationCount",
    "expectedMutationCount",
    "removalCandidateCount",
    "expectedRemovalCandidateCount",
    "timerCandidateCount",
    "expectedTimerCandidateCount",
    "immediateTransitionCandidateCount",
    "expectedImmediateTransitionCandidateCount",
    "fullScanInvocationCount",
    "fullScanEntityVisitCount",
    "survivorCopyCount",
    "removalProcessedCount",
    "timerProcessedCount",
    "transitionProcessedCount",
    "zeroCandidateOpportunityCount",
    "referenceOracleInvocationCount",
    "indexedInvocationCount",
    "hiddenFallbackCount",
    "invariantMismatchCount",
    "validCleanupProcessorSamples",
    "validRunCleanupPhaseSamples",
) + _STRUCTURE_FIELDS


def _check_exact_fields(
    value: dict[str, Any], expected: set[str], location: str, reasons: list[str]
) -> None:
    for field in sorted(expected - set(value)):
        reasons.append(f"FIELD_MISSING: {location}.{field}")
    for field in sorted(set(value) - expected):
        reasons.append(f"FIELD_UNEXPECTED: {location}.{field}")


def _is_json_int(value: Any) -> bool:
    return type(value) is int


def _is_sha256(value: Any) -> bool:
    return isinstance(value, str) and len(value) == 64 and all(
        character in "0123456789abcdef" for character in value
    )


_METRIC_FIELDS = ("median", "p95", "p99", "maximum")


def _metric_error(value: Any, expected_count: int) -> str | None:
    if not isinstance(value, dict):
        return "must be an object"
    if set(value) != {"count", "median", "p95", "p99", "maximum"}:
        return f"exact fields mismatch observed={sorted(value)}"
    if not _is_json_int(value.get("count")) or value.get("count") != expected_count:
        return f"count expected={expected_count} observed={value.get('count')!r}"

    samples: list[float] = []
    for key in _METRIC_FIELDS:
        sample = value.get(key)
        if (
            isinstance(sample, bool)
            or not isinstance(sample, (int, float))
            or not math.isfinite(sample)
            or sample < 0
        ):
            return f"{key} must be finite and nonnegative"
        samples.append(float(sample))

    if samples != sorted(samples):
        return "expected median <= p95 <= p99 <= maximum"
    return None


def _validate_frame_allocation(
    document: dict[str, Any],
    identity_by_workload: dict[str, tuple[Any, ...]],
    expected_strategies: tuple[str, ...],
) -> list[str]:
    reasons: list[str] = []
    value = document.get("frameAllocationCalibration")
    if not isinstance(value, dict):
        return ["ALLOCATION_SIGNAL_INVALID: frameAllocationCalibration must be an object"]
    is_v4 = document.get("schemaVersion") == 2 and document.get("evidenceContractVersion") == 4
    if is_v4:
        _check_exact_fields(
            value,
            {"signal", "allocationCounterProbeBytes", "warmupFramesPerPhase", "sampleFramesPerPhase", "phases"},
            "frameAllocationCalibration",
            reasons,
        )
    if value.get("signal") != "GC.GetAllocatedBytesForCurrentThread delta around exactly one synthetic tick":
        reasons.append(f"ALLOCATION_SIGNAL_INVALID: signal={value.get('signal')!r}")
    counter_probe_bytes = value.get("allocationCounterProbeBytes")
    if (
        isinstance(counter_probe_bytes, bool)
        or not isinstance(counter_probe_bytes, int)
        or counter_probe_bytes < 4096
    ):
        reasons.append(
            "ALLOCATION_COUNTER_PROBE_INVALID: "
            f"expectedAtLeast=4096 observed={counter_probe_bytes!r}"
        )
    warmup_frames = value.get("warmupFramesPerPhase")
    sample_frames = value.get("sampleFramesPerPhase")
    if (
        not _is_json_int(warmup_frames)
        or not _is_json_int(sample_frames)
        or warmup_frames != 30
        or sample_frames != 100
    ):
        reasons.append(
            "ALLOCATION_SIGNAL_INVALID: expected warmup/sample 30/100, "
            f"observed={value.get('warmupFramesPerPhase')!r}/{value.get('sampleFramesPerPhase')!r}"
        )
    phases = value.get("phases")
    if not isinstance(phases, list):
        return reasons + ["ALLOCATION_SIGNAL_INVALID: phases must be an array"]
    observed_keys: list[tuple[str, str, bool]] = []
    for index, phase in enumerate(phases):
        location = f"frameAllocationCalibration.phases[{index}]"
        if not isinstance(phase, dict):
            reasons.append(f"ALLOCATION_SIGNAL_INVALID: {location} must be an object")
            continue
        if is_v4:
            _check_exact_fields(
                phase,
                {
                    "strategy", "workloadId", "scheduleHash", "initialWorldFingerprint",
                    "captureDiagnostics", "validSamples", "gcAllocatedBytesPerTick",
                },
                location,
                reasons,
            )
        workload_id = phase.get("workloadId")
        strategy = phase.get("strategy")
        capture_diagnostics = phase.get("captureDiagnostics")
        if (
            not isinstance(strategy, str)
            or not isinstance(workload_id, str)
            or not isinstance(capture_diagnostics, bool)
        ):
            reasons.append(f"ALLOCATION_SIGNAL_INVALID: {location} identity is invalid")
            continue
        observed_keys.append((strategy, workload_id, capture_diagnostics))
        baseline = identity_by_workload.get(workload_id)
        if baseline is None or (
            phase.get("scheduleHash") != baseline[1]
            or phase.get("initialWorldFingerprint") != baseline[2]
        ):
            reasons.append(f"WORKLOAD_IDENTITY_MISMATCH: {location}")
        if not _is_json_int(phase.get("validSamples")) or phase.get("validSamples") != 100:
            reasons.append(
                f"SAMPLE_COUNT_MISMATCH: {location}.validSamples expected=100 "
                f"observed={phase.get('validSamples')!r}"
            )
        metric_error = _metric_error(phase.get("gcAllocatedBytesPerTick"), 100)
        if metric_error is not None:
            reasons.append(
                "METRIC_SUMMARY_INVALID: "
                f"{location}.gcAllocatedBytesPerTick: {metric_error}"
            )
    expected_keys = [
        (strategy, workload_id, capture_diagnostics)
        for strategy in expected_strategies
        for workload_id in (
            "cleanup-s3-target-wall-empty-v2",
            "cleanup-s3-stress-dense-v2",
        )
        for capture_diagnostics in (True, False)
    ]
    if observed_keys != expected_keys:
        reasons.append(
            "ALLOCATION_STRATEGY_MATRIX_MISMATCH: "
            f"expected={expected_keys!r} observed={observed_keys!r}"
        )
    return reasons


def _load_workload_contract(path: Path) -> dict[str, Any]:
    observed_hash = _sha256(path)
    if observed_hash != APPROVED_CLEANUP_S3_WORKLOAD_SHA256:
        raise ValueError(
            "TOOL_HASH_MISMATCH: workload contract expected="
            f"{APPROVED_CLEANUP_S3_WORKLOAD_SHA256} observed={observed_hash}"
        )
    contract = load_json_object(path, "workloadContract")
    if contract.get("schemaVersion") != 2 or not isinstance(contract.get("workloads"), dict):
        raise ValueError(f"invalid Cleanup Slice 3 workload contract: {path}")
    if set(contract) != {"schemaVersion", "workloads"}:
        raise ValueError(f"unexpected Cleanup Slice 3 workload contract fields: {sorted(contract)}")
    return contract


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _try_sha256(path: Path) -> str | None:
    try:
        return _sha256(path)
    except OSError:
        return None


def _provenance(metrics_path: Path, active_strategies: tuple[str, ...]) -> dict[str, Any]:
    return {
        "metricsSha256": _sha256(metrics_path),
        "cleanupValidatorSha256": _sha256(Path(__file__).resolve()),
        "workloadContractSha256": _sha256(DEFAULT_WORKLOAD_CONTRACT),
        "activeStrategies": list(active_strategies),
    }


def _write_summary(
    summary: dict[str, Any],
    output: Path | None,
    *,
    inputs: tuple[Path, ...] = (),
    expected_input_snapshots: tuple[tuple[Path, Any], ...] = (),
) -> bool:
    if output is not None:
        try:
            atomic_json(
                output,
                summary,
                inputs=(*inputs, Path(__file__).resolve(), DEFAULT_WORKLOAD_CONTRACT),
                expected_input_snapshots=expected_input_snapshots,
            )
        except (EvidenceError, OSError) as error:
            print(json.dumps({"error": str(error)}, sort_keys=True))
            return False
    print(json.dumps(summary, sort_keys=True, allow_nan=False))
    return True


def build_admission_report(
    metrics: dict[str, Any],
    *,
    metrics_sha256: str,
    active_strategies: tuple[str, ...],
    validator_path: Path,
    workload_contract_path: Path,
) -> dict[str, Any]:
    if type(metrics.get("schemaVersion")) is not int or metrics.get("schemaVersion") != 2:
        verdict, values = REJECTED, [f"SCHEMA_VERSION_INVALID: metrics.schemaVersion={metrics.get('schemaVersion')!r}"]
    elif type(metrics.get("evidenceContractVersion")) is not int or metrics.get("evidenceContractVersion") != 4:
        verdict, values = REJECTED, [f"CONTRACT_VERSION_INVALID: metrics.evidenceContractVersion={metrics.get('evidenceContractVersion')!r}"]
    else:
        try:
            verdict, values = validate_cleanup_slice3(
                metrics.get("cleanupSlice3Calibration", {}),
                active_strategies,
                _load_workload_contract(workload_contract_path),
                require_v4=True,
            )
        except (KeyError, TypeError, ValueError, OSError) as error:
            verdict, values = REJECTED, [f"EVIDENCE_VALIDATION_FAILED: {error}"]
    recognized_codes = {
        "SCHEMA_VERSION_INVALID", "CONTRACT_VERSION_INVALID", "FIELD_MISSING",
        "FIELD_UNEXPECTED", "FIELD_TYPE_INVALID", "NUMERIC_DOMAIN_INVALID",
        "CARDINALITY_MISMATCH", "ORDER_MISMATCH", "STAGE_MISMATCH",
        "STRATEGY_MISMATCH", "WORKLOAD_ID_MISMATCH", "RUN_KEY_MISMATCH",
        "SEMANTIC_INVARIANT_INVALID",
    }
    report_reasons = []
    for value in values:
        code = value.split(":", 1)[0]
        report_reasons.append(
            reason(code if code in recognized_codes else "SEMANTIC_INVARIANT_INVALID", "cleanupSlice3Calibration", None, value)
        )
    return {
        "schemaVersion": 2,
        "evidenceContractVersion": 4,
        "verdict": verdict,
        "reasons": report_reasons,
        "identity": evidence_identity(metrics, metrics_sha256),
        "provenance": {
            "metricsSha256": metrics_sha256,
            "cleanupValidatorSha256": _sha256(validator_path),
            "workloadContractSha256": _sha256(workload_contract_path),
            "activeStrategies": list(active_strategies),
        },
        "inputHashes": {"metricsSha256": metrics_sha256},
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("metrics", type=Path)
    parser.add_argument("--active-strategies", default="A")
    parser.add_argument("--preflight-manifest", type=Path)
    parser.add_argument("--artifact-manifest", type=Path)
    parser.add_argument("--output", type=Path)
    arguments = parser.parse_args()
    active_strategies = tuple(
        value for value in arguments.active_strategies.split(",") if value
    )
    context_inputs = tuple(
        path
        for path in (arguments.preflight_manifest, arguments.artifact_manifest)
        if path is not None
    )
    input_paths = (
        arguments.metrics,
        *context_inputs,
        Path(__file__).resolve(),
        DEFAULT_WORKLOAD_CONTRACT.resolve(),
    )
    input_snapshots = capture_input_snapshots(input_paths)
    try:
        provenance = _provenance(arguments.metrics, active_strategies)
        metrics = load_json_object(arguments.metrics, "metrics")
    except EvidenceError as error:
        summary = {
            "schemaVersion": 2,
            "evidenceContractVersion": 4,
            "verdict": REJECTED,
            "reasons": [error.reason],
            "identity": {},
            "provenance": {
                "metricsSha256": _try_sha256(arguments.metrics),
                "cleanupValidatorSha256": _try_sha256(Path(__file__).resolve()),
                "workloadContractSha256": _try_sha256(DEFAULT_WORKLOAD_CONTRACT),
                "activeStrategies": list(active_strategies),
            },
            "inputHashes": {"metricsSha256": _try_sha256(arguments.metrics)},
        }
        return 1 if _write_summary(
            summary,
            arguments.output,
            inputs=input_paths,
            expected_input_snapshots=input_snapshots,
        ) else 2
    except (OSError, UnicodeError, ValueError) as error:
        summary = {
            "schemaVersion": 2,
            "evidenceContractVersion": 4,
            "verdict": REJECTED,
            "reasons": [reason("JSON_PARSE_FAILED", "metrics", "readable strict JSON", str(error))],
            "identity": {},
            "provenance": {
                "metricsSha256": _try_sha256(arguments.metrics),
                "cleanupValidatorSha256": _try_sha256(Path(__file__).resolve()),
                "workloadContractSha256": _try_sha256(DEFAULT_WORKLOAD_CONTRACT),
                "activeStrategies": list(active_strategies),
            },
            "inputHashes": {"metricsSha256": _try_sha256(arguments.metrics)},
        }
        return 1 if _write_summary(
            summary,
            arguments.output,
            inputs=input_paths,
            expected_input_snapshots=input_snapshots,
        ) else 2

    summary = build_admission_report(
        metrics,
        metrics_sha256=provenance["metricsSha256"],
        active_strategies=active_strategies,
        validator_path=Path(__file__).resolve(),
        workload_contract_path=DEFAULT_WORKLOAD_CONTRACT,
    )
    context_reasons = validate_v4_context_pair(
        arguments.preflight_manifest,
        arguments.artifact_manifest,
        metrics.get("captureIdentity"),
        metrics_sha256=provenance["metricsSha256"],
    )
    if context_reasons:
        summary["verdict"] = REJECTED
        summary["reasons"] = list(summary.get("reasons", [])) + context_reasons
    if not _write_summary(
        summary,
        arguments.output,
        inputs=input_paths,
        expected_input_snapshots=input_snapshots,
    ):
        return 2
    return 0 if summary["verdict"] == ADMITTED else 1


if __name__ == "__main__":
    raise SystemExit(main())
