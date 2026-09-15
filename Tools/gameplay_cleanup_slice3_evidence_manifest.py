#!/usr/bin/env python3
"""Create the Evidence Contract v4 provisional/final Cleanup Slice 3 manifest."""

from __future__ import annotations

import argparse
import json
import subprocess
from pathlib import Path
from typing import Any

try:
    from Tools.gameplay_evidence_v4 import APPROVED_CLEANUP_S3_WORKLOAD_SHA256, CAPTURE_IDENTITY_FIELDS, SHA256_PATTERN, EvidenceError, REASON_CODES, atomic_json, build_payload_sha256, canonical_repository_root, capture_input_snapshots, evidence_identity, harness_sha256, live_source_identity, load_json_object, load_strict_kv, reason, runtime_tree_sha256, try_sha256, validate_attempt_identity, validate_reason, validate_runtime_marker
except ModuleNotFoundError:
    from gameplay_evidence_v4 import APPROVED_CLEANUP_S3_WORKLOAD_SHA256, CAPTURE_IDENTITY_FIELDS, SHA256_PATTERN, EvidenceError, REASON_CODES, atomic_json, build_payload_sha256, canonical_repository_root, capture_input_snapshots, evidence_identity, harness_sha256, live_source_identity, load_json_object, load_strict_kv, reason, runtime_tree_sha256, try_sha256, validate_attempt_identity, validate_reason, validate_runtime_marker

try:
    from Tools.gameplay_performance_admission import build_metrics_report
    from Tools.gameplay_cleanup_slice3_admission import build_admission_report
    from Tools.gameplay_cleanup_slice3_calibration import build_calibration_report
except ModuleNotFoundError:
    from gameplay_performance_admission import build_metrics_report
    from gameplay_cleanup_slice3_admission import build_admission_report
    from gameplay_cleanup_slice3_calibration import build_calibration_report


STAGE_NAMES = (
    "preflight", "build", "guardRestore", "player", "markerValidation",
    "tickAttributionAdmission", "performanceAdmission", "cleanupAdmission", "calibration",
    "consistencyFinalization",
)
TERMINAL_STATUSES = {"PASS", "DEFERRED", "HOLD"}
EARLY_HOLD_VERDICTS = {
    "HOLD_BUILD_FAILURE", "HOLD_PLAYER_FAILURE",
    "HOLD_MARKER_FAILURE", "HOLD_TICK_ATTRIBUTION_ADMISSION",
    "HOLD_PERFORMANCE_ADMISSION", "HOLD_CLEANUP_ADMISSION",
    "HOLD_INVALID_SIGNAL", "HOLD_INVALID_EVIDENCE",
}
HOLD_VERDICT_REASON_CODES = {
    "HOLD_BUILD_FAILURE": "BUILD_FAILED",
    "HOLD_PLAYER_FAILURE": "PLAYER_FAILED",
    "HOLD_MARKER_FAILURE": "MARKER_VALIDATION_FAILED",
    "HOLD_TICK_ATTRIBUTION_ADMISSION": "TICK_ATTRIBUTION_REJECTED",
    "HOLD_PERFORMANCE_ADMISSION": "PERFORMANCE_REJECTED",
    "HOLD_CLEANUP_ADMISSION": "CLEANUP_REJECTED",
    "HOLD_INVALID_SIGNAL": "SIGNAL_INVALID",
}
HOLD_VERDICT_STAGES = {
    "HOLD_BUILD_FAILURE": "build",
    "HOLD_PLAYER_FAILURE": "player",
    "HOLD_MARKER_FAILURE": "markerValidation",
    "HOLD_TICK_ATTRIBUTION_ADMISSION": "tickAttributionAdmission",
    "HOLD_PERFORMANCE_ADMISSION": "performanceAdmission",
    "HOLD_CLEANUP_ADMISSION": "cleanupAdmission",
    "HOLD_INVALID_SIGNAL": "calibration",
}
CANONICAL_TOOL_PATHS = {
    "performanceValidator": Path(__file__).resolve().with_name("gameplay_performance_admission.py"),
    "tickAttributionValidator": Path(__file__).resolve().with_name("gameplay_tick_attribution.py"),
    "cleanupValidator": Path(__file__).resolve().with_name("gameplay_cleanup_slice3_admission.py"),
    "aggregator": Path(__file__).resolve().with_name("gameplay_cleanup_slice3_calibration.py"),
    "manifestTool": Path(__file__).resolve(),
    "workloadContract": Path(__file__).resolve().parent / "contracts" / "gameplay_cleanup_slice3_workloads_v2.json",
}
FINAL_MANIFEST_FIELDS = {
    "schemaVersion", "evidenceContractVersion", "manifestState",
    "terminalStatus", "authoritativeVerdict", "identity", "reasons",
    "stages", "artifacts", "exitStatus",
}
DERIVED_IDENTITY_FIELDS = {
    "metricsRevision", "metricsSha256", "workloadIds", "orderedRunKeys",
}
REQUIRED_SUCCESS_ARTIFACTS = {
    "metrics", "runtimeLog", "preflightManifest", "artifactManifest",
    "tickAttribution", "tickAttributionReport", "tickAttributionValidator",
    "performanceAdmission", "cleanupAdmission", "cleanupCalibration",
    "performanceValidator", "cleanupValidator", "aggregator",
    "workloadContract", "runner", "manifestTool", "playerArtifact", "buildLog",
}
STAGE_ARTIFACTS = {
    "preflight": ("preflightManifest",),
    "build": ("buildLog", "playerArtifact"),
    "guardRestore": ("artifactManifest",),
    "player": ("runtimeLog", "metrics"),
    "markerValidation": ("runtimeLog",),
    "tickAttributionAdmission": (
        "tickAttribution", "tickAttributionReport", "tickAttributionValidator",
    ),
    "performanceAdmission": ("performanceAdmission",),
    "cleanupAdmission": ("cleanupAdmission",),
    "calibration": ("cleanupCalibration",),
    # The manifest tool is stable input; the final manifest cannot hash itself.
    "consistencyFinalization": ("manifestTool",),
}
STAGE_STRATEGIES = {"S3-A": ["A"], "S3-B": ["A", "B"], "S3-C": ["A", "B", "C"]}
TICK_ATTRIBUTION_HASH_KEYS = {
    "TickAttributionSHA256",
    "TickAttributionReportSHA256",
    "TickAttributionValidatorSHA256",
}
EXIT_STAGE_FIELDS = {
    "tickAttributionAdmission": "tickAttributionAdmission",
    "performanceAdmission": "performanceAdmission",
    "cleanupAdmission": "cleanupAdmission",
    "calibration": "cleanupCalibration",
}


def artifact(path: Path | None, missing_code: str = "ARTIFACT_MISSING") -> dict[str, Any]:
    digest = try_sha256(path) if path is not None else None
    return {
        "path": str(path.resolve(strict=False)) if path is not None else None,
        "state": "PRESENT" if digest is not None else "MISSING",
        "sha256": digest,
        "missingReasonCode": None if digest is not None else missing_code,
    }


def not_applicable_artifact(path: Path) -> dict[str, Any]:
    return {
        "path": str(path.resolve(strict=False)),
        "state": "NOT_APPLICABLE",
        "sha256": None,
        "missingReasonCode": None,
    }


def path_lexically_exists(path: Path) -> bool:
    return path.exists() or path.is_symlink()


def validate_tick_attribution_bundle(artifact_manifest: Path) -> None:
    captured = load_strict_kv(artifact_manifest, "artifactManifest")
    present_keys = set(captured) & TICK_ATTRIBUTION_HASH_KEYS
    if not present_keys:
        return
    if present_keys != TICK_ATTRIBUTION_HASH_KEYS:
        raise EvidenceError(
            "FIELD_MISSING",
            "artifactManifest.tickAttributionHashes",
            sorted(TICK_ATTRIBUTION_HASH_KEYS),
            sorted(present_keys),
        )

    raw_path = artifact_manifest.parent / "tick-attribution.json"
    report_path = artifact_manifest.parent / "tick-attribution-report.json"
    validator_path = Path(__file__).resolve().with_name("gameplay_tick_attribution.py")
    bindings = (
        ("TickAttributionSHA256", raw_path, "METRICS_HASH_MISMATCH"),
        ("TickAttributionReportSHA256", report_path, "METRICS_HASH_MISMATCH"),
        ("TickAttributionValidatorSHA256", validator_path, "TOOL_HASH_MISMATCH"),
    )
    for key, path, code in bindings:
        observed = try_sha256(path)
        if captured.get(key) != observed:
            raise EvidenceError(code, f"artifactManifest.{key}", observed, captured.get(key))

    report = load_json_object(report_path, "tickAttributionReport")
    report_bindings = (
        ("verdict", "ADMITTED", "PERSISTED_REPORT_MISMATCH"),
        ("sourceSha256", captured["TickAttributionSHA256"], "METRICS_HASH_MISMATCH"),
        ("validatorSha256", captured["TickAttributionValidatorSHA256"], "TOOL_HASH_MISMATCH"),
        ("identitySourceSha256", captured.get("MetricsSHA256"), "METRICS_HASH_MISMATCH"),
    )
    for field, expected, code in report_bindings:
        if report.get(field) != expected:
            raise EvidenceError(code, f"tickAttributionReport.{field}", expected, report.get(field))


_ALLOCATION_ONLY_CLEANUP_OBSERVED = (
    "ALLOCATION_COUNTER_PROBE_INVALID: expectedAtLeast=4096 observed=0"
)
_ALLOCATION_ONLY_DIAGNOSTIC_OBSERVED = (
    "S3-A calibration was not admitted: " + _ALLOCATION_ONLY_CLEANUP_OBSERVED
)


def is_allocation_only_cleanup_report(document: Any) -> bool:
    reasons = document.get("reasons") if isinstance(document, dict) else None
    return (
        document.get("verdict") == "REJECTED"
        and isinstance(reasons, list)
        and len(reasons) == 1
        and reasons[0] == {
            "code": "SEMANTIC_INVARIANT_INVALID",
            "path": "cleanupSlice3Calibration",
            "expected": None,
            "observed": _ALLOCATION_ONLY_CLEANUP_OBSERVED,
        }
    )


def is_allocation_only_diagnostic_report(document: Any) -> bool:
    reasons = document.get("reasons") if isinstance(document, dict) else None
    return (
        document.get("status") == "HOLD_INVALID_EVIDENCE"
        and document.get("admitted") is False
        and isinstance(reasons, list)
        and len(reasons) == 1
        and reasons[0] == {
            "code": "SEMANTIC_INVARIANT_INVALID",
            "path": "cleanupSlice3Calibration",
            "expected": None,
            "observed": _ALLOCATION_ONLY_DIAGNOSTIC_OBSERVED,
        }
    )


def validate_allocation_diagnostic_contract(
    *,
    metrics: Path,
    allocation_diagnostic: Path,
    cleanup_calibration: Path,
    validator: Path,
    aggregator: Path,
    workload_contract: Path,
) -> dict[str, Any]:
    if path_lexically_exists(cleanup_calibration):
        raise EvidenceError(
            "SEMANTIC_INVARIANT_INVALID",
            "cleanupCalibrationArtifactPath",
            "absent for allocation-only diagnostic",
            str(cleanup_calibration.resolve(strict=False)),
        )
    metrics_hash = try_sha256(metrics)
    if metrics_hash is None:
        raise EvidenceError(
            "ARTIFACT_MISSING",
            "metrics",
            "readable regular file",
            str(metrics.resolve(strict=False)),
        )
    metrics_document = load_json_object(metrics, "metrics")
    diagnostic = load_json_object(allocation_diagnostic, "allocationDiagnostic")
    canonical = build_calibration_report(
        metrics_document,
        metrics_sha256=metrics_hash,
        validator_path=validator,
        aggregator_path=aggregator,
        workload_contract_path=workload_contract,
    )
    if diagnostic != canonical:
        raise EvidenceError(
            "PERSISTED_REPORT_MISMATCH",
            "allocationDiagnostic",
            canonical,
            diagnostic,
        )
    if not is_allocation_only_diagnostic_report(diagnostic):
        raise EvidenceError(
            "SEMANTIC_INVARIANT_INVALID",
            "allocationDiagnostic",
            "exact allocation-only Cleanup rejection diagnostic",
            diagnostic.get("status"),
        )
    return diagnostic


def empty_stages() -> dict[str, Any]:
    return {name: {"status": "NOT_RUN", "reasons": [], "artifacts": []} for name in STAGE_NAMES}


def provisional_manifest(
    identity: dict[str, Any], planned_artifacts: dict[str, Path] | None = None
) -> dict[str, Any]:
    normalized_identity = {
        field: identity.get(field)
        for field in CAPTURE_IDENTITY_FIELDS | DERIVED_IDENTITY_FIELDS
    }
    normalized_identity["workloadIds"] = identity.get("workloadIds", [])
    normalized_identity["orderedRunKeys"] = identity.get("orderedRunKeys", [])
    normalized_artifacts = dict(planned_artifacts or {})
    normalized_artifacts.setdefault("manifestTool", Path(__file__).resolve())
    artifacts = {
        name: artifact(path)
        for name, path in normalized_artifacts.items()
    }
    stages = empty_stages()
    for stage, names in STAGE_ARTIFACTS.items():
        stages[stage]["artifacts"] = [name for name in names if name in artifacts]
    return {
        "schemaVersion": 4, "evidenceContractVersion": 4, "manifestState": "PROVISIONAL",
        "terminalStatus": "NOT_RUN", "authoritativeVerdict": "NOT_RUN", "identity": normalized_identity,
        "reasons": [], "stages": stages, "artifacts": artifacts, "exitStatus": {},
    }


def transition_manifest(document: dict[str, Any], stage: str, status: str, stage_reason: dict[str, Any] | None) -> dict[str, Any]:
    if document.get("manifestState") != "PROVISIONAL" or document.get("terminalStatus") != "NOT_RUN":
        raise EvidenceError("SEMANTIC_INVARIANT_INVALID", "manifestState", "PROVISIONAL/NOT_RUN", [document.get("manifestState"), document.get("terminalStatus")])
    if stage not in STAGE_NAMES or status not in {"PASS", "DEFERRED", "HOLD"}:
        raise EvidenceError("FIELD_TYPE_INVALID", "transition", "known stage/status", [stage, status])
    stage_index = STAGE_NAMES.index(stage)
    for prior in STAGE_NAMES[:stage_index]:
        prior_status = document.get("stages", {}).get(prior, {}).get("status")
        if prior_status != "PASS":
            raise EvidenceError("ORDER_MISMATCH", f"stages.{stage}", f"{prior}=PASS", prior_status)
    for later in STAGE_NAMES[stage_index + 1:]:
        later_status = document.get("stages", {}).get(later, {}).get("status")
        if later_status != "NOT_RUN":
            raise EvidenceError("ORDER_MISMATCH", f"stages.{later}", "NOT_RUN", later_status)
    stage_value = document.get("stages", {}).get(stage)
    if not isinstance(stage_value, dict) or stage_value.get("status") != "NOT_RUN":
        raise EvidenceError("SEMANTIC_INVARIANT_INVALID", f"stages.{stage}", "NOT_RUN", stage_value)
    if status == "PASS" and stage_reason is not None:
        raise EvidenceError("REASONS_COHERENCE_INVALID", f"stages.{stage}.reasons", [], stage_reason)
    if status == "HOLD" and stage_reason is None:
        raise EvidenceError("REASONS_COHERENCE_INVALID", f"stages.{stage}.reasons", "one registered reason", None)
    if status == "DEFERRED" and stage != "calibration":
        raise EvidenceError("SEMANTIC_INVARIANT_INVALID", f"stages.{stage}.status", "DEFERRED only for calibration", status)
    if stage_reason is not None:
        reason_issues = validate_reason(stage_reason, f"stages.{stage}.reasons[0]")
        if reason_issues:
            first = reason_issues[0]
            raise EvidenceError(first["code"], first["path"], first["expected"], first["observed"])
    stage_value["status"] = status
    stage_value["reasons"] = [] if stage_reason is None else [stage_reason]
    if stage_reason is not None:
        document["reasons"].append(stage_reason)
    return document


def finalize_lifecycle(document: dict[str, Any], terminal_status: str, authoritative_verdict: str) -> dict[str, Any]:
    if document.get("manifestState") != "PROVISIONAL" or document.get("terminalStatus") != "NOT_RUN":
        raise EvidenceError("SEMANTIC_INVARIANT_INVALID", "manifestState", "PROVISIONAL/NOT_RUN", [document.get("manifestState"), document.get("terminalStatus")])
    if terminal_status != "HOLD" or authoritative_verdict not in EARLY_HOLD_VERDICTS:
        raise EvidenceError("FIELD_TYPE_INVALID", "terminalStatus", "early lifecycle may finalize registered HOLD only", [terminal_status, authoritative_verdict])
    stages = document.get("stages")
    if not isinstance(stages, dict):
        raise EvidenceError("FIELD_TYPE_INVALID", "stages", "object", stages)
    hold_indexes = [index for index, name in enumerate(STAGE_NAMES) if stages.get(name, {}).get("status") == "HOLD"]
    if len(hold_indexes) != 1:
        raise EvidenceError("CARDINALITY_MISMATCH", "stages", "exactly one HOLD stage", hold_indexes)
    hold_index = hold_indexes[0]
    for name in STAGE_NAMES[hold_index + 1:]:
        if stages.get(name, {}).get("status") != "NOT_RUN":
            raise EvidenceError("ORDER_MISMATCH", f"stages.{name}", "NOT_RUN after HOLD", stages.get(name))
        stages[name]["artifacts"] = []
    if not document.get("reasons"):
        raise EvidenceError("REASONS_COHERENCE_INVALID", "reasons", "nonempty for HOLD", document.get("reasons"))
    document["manifestState"] = "FINAL"
    document["terminalStatus"] = terminal_status
    document["authoritativeVerdict"] = authoritative_verdict
    for name, record in document.get("artifacts", {}).items():
        if not isinstance(record, dict) or not isinstance(record.get("path"), str):
            continue
        document["artifacts"][name] = artifact(Path(record["path"]))
    identity = document.get("identity")
    if isinstance(identity, dict):
        for field, artifact_name in {
            "metricsSha256": "metrics",
            "playerArtifactSha256": "playerArtifact",
            "runnerSha256": "runner",
            "performanceValidatorSha256": "performanceValidator",
            "cleanupValidatorSha256": "cleanupValidator",
            "aggregatorSha256": "aggregator",
            "manifestToolSha256": "manifestTool",
            "workloadContractSha256": "workloadContract",
        }.items():
            record = document.get("artifacts", {}).get(artifact_name)
            if isinstance(record, dict) and record.get("state") == "PRESENT":
                identity[field] = record.get("sha256")
        harness_fields = (
            "runnerSha256",
            "performanceValidatorSha256",
            "cleanupValidatorSha256",
            "aggregatorSha256",
            "manifestToolSha256",
            "workloadContractSha256",
        )
        if all(isinstance(identity.get(field), str) for field in harness_fields):
            identity["harnessSha256"] = harness_sha256(
                runner_sha256=identity["runnerSha256"],
                performance_validator_sha256=identity["performanceValidatorSha256"],
                cleanup_validator_sha256=identity["cleanupValidatorSha256"],
                aggregator_sha256=identity["aggregatorSha256"],
                manifest_tool_sha256=identity["manifestToolSha256"],
                workload_contract_sha256=identity["workloadContractSha256"],
            )
        metrics_record = document.get("artifacts", {}).get("metrics")
        if (
            isinstance(metrics_record, dict)
            and metrics_record.get("state") == "PRESENT"
            and isinstance(metrics_record.get("path"), str)
        ):
            try:
                metrics_document = load_json_object(
                    Path(metrics_record["path"]),
                    "manifest.artifacts.metrics",
                )
            except EvidenceError:
                identity["metricsSha256"] = metrics_record.get("sha256")
            else:
                derived = evidence_identity(metrics_document, metrics_record.get("sha256"))
                if not validate_attempt_identity(metrics_document.get("captureIdentity")):
                    document["identity"] = derived
                else:
                    for field, value in derived.items():
                        if value is not None and (not isinstance(value, list) or value):
                            identity[field] = value
    return document


def finalize_infrastructure_failure(document: dict[str, Any]) -> dict[str, Any]:
    if document.get("manifestState") != "PROVISIONAL" or document.get("terminalStatus") != "NOT_RUN":
        return document
    stages = document.get("stages")
    if not isinstance(stages, dict):
        raise EvidenceError("FIELD_TYPE_INVALID", "stages", "object", stages)
    hold_names = [name for name in STAGE_NAMES if stages.get(name, {}).get("status") == "HOLD"]
    if not hold_names:
        pending_names = [name for name in STAGE_NAMES if stages.get(name, {}).get("status") == "NOT_RUN"]
        if not pending_names:
            raise EvidenceError("STAGE_NOT_RUN", "stages", "at least one NOT_RUN stage", stages)
        name = pending_names[0]
        failure_reason = reason("FINAL_MANIFEST_UNAVAILABLE", f"stages.{name}")
        stage = stages.get(name)
        if not isinstance(stage, dict):
            raise EvidenceError("FIELD_TYPE_INVALID", f"stages.{name}", "stage object", stage)
        stage["status"] = "HOLD"
        stage["reasons"] = [failure_reason]
        document.setdefault("reasons", []).append(failure_reason)
    elif len(hold_names) != 1:
        raise EvidenceError("CARDINALITY_MISMATCH", "stages", "at most one HOLD", hold_names)
    hold_name = next(name for name in STAGE_NAMES if stages.get(name, {}).get("status") == "HOLD")
    exit_field = EXIT_STAGE_FIELDS.get(hold_name)
    if exit_field is not None:
        document.setdefault("exitStatus", {}).setdefault(exit_field, 2)
    return finalize_lifecycle(document, "HOLD", "HOLD_INVALID_EVIDENCE")


def validate_final_manifest_transport(document: Any) -> str:
    if not isinstance(document, dict):
        raise EvidenceError("JSON_ROOT_INVALID", "finalManifest", "object", type(document).__name__)
    if set(document) != FINAL_MANIFEST_FIELDS:
        raise EvidenceError(
            "FIELD_UNEXPECTED",
            "finalManifest",
            sorted(FINAL_MANIFEST_FIELDS),
            sorted(document),
        )
    if document.get("schemaVersion") != 4 or type(document.get("schemaVersion")) is not int:
        raise EvidenceError("SCHEMA_VERSION_INVALID", "finalManifest.schemaVersion", 4, document.get("schemaVersion"))
    if document.get("evidenceContractVersion") != 4 or type(document.get("evidenceContractVersion")) is not int:
        raise EvidenceError("CONTRACT_VERSION_INVALID", "finalManifest.evidenceContractVersion", 4, document.get("evidenceContractVersion"))
    if document.get("manifestState") != "FINAL":
        raise EvidenceError("SEMANTIC_INVARIANT_INVALID", "finalManifest.manifestState", "FINAL", document.get("manifestState"))
    terminal_status = document.get("terminalStatus")
    authoritative = document.get("authoritativeVerdict")
    allowed_verdicts = {
        "PASS": {"READY"},
        "DEFERRED": {"DEFERRED_NOT_MATERIAL"},
        "HOLD": EARLY_HOLD_VERDICTS,
    }
    if terminal_status not in allowed_verdicts or authoritative not in allowed_verdicts[terminal_status]:
        raise EvidenceError(
            "SEMANTIC_INVARIANT_INVALID",
            "finalManifest.terminalStatus",
            {key: sorted(value) for key, value in allowed_verdicts.items()},
            [terminal_status, authoritative],
        )
    reasons = document.get("reasons")
    if not isinstance(reasons, list):
        raise EvidenceError("FIELD_TYPE_INVALID", "finalManifest.reasons", "array", reasons)
    for index, value in enumerate(reasons):
        issues = validate_reason(value, f"finalManifest.reasons[{index}]")
        if issues:
            issue = issues[0]
            raise EvidenceError(issue["code"], issue["path"], issue["expected"], issue["observed"])
    if terminal_status in {"PASS", "DEFERRED"} and reasons:
        raise EvidenceError(
            "REASONS_COHERENCE_INVALID",
            "finalManifest.reasons",
            "empty for PASS/DEFERRED",
            reasons,
        )
    strict_identity_binding = (
        authoritative != "HOLD_INVALID_EVIDENCE"
        or (
            len(reasons) == 1
            and reasons[0].get("code") == "FULL_SCAN_EXPECTATION_UNAPPROVED"
        )
    )
    stages = document.get("stages")
    if not isinstance(stages, dict) or set(stages) != set(STAGE_NAMES):
        raise EvidenceError("FIELD_TYPE_INVALID", "finalManifest.stages", list(STAGE_NAMES), stages)
    for name in STAGE_NAMES:
        stage = stages[name]
        if not isinstance(stage, dict) or set(stage) != {"status", "reasons", "artifacts"}:
            raise EvidenceError("FIELD_TYPE_INVALID", f"finalManifest.stages.{name}", "exact stage object", stage)
        if stage.get("status") not in {"NOT_RUN", "PASS", "DEFERRED", "HOLD"}:
            raise EvidenceError("FIELD_TYPE_INVALID", f"finalManifest.stages.{name}.status", "known status", stage.get("status"))
        stage_reasons = stage.get("reasons")
        stage_artifacts = stage.get("artifacts")
        if not isinstance(stage_reasons, list) or not isinstance(stage_artifacts, list):
            raise EvidenceError("FIELD_TYPE_INVALID", f"finalManifest.stages.{name}", "array reasons/artifacts", stage)
        if (stage["status"] == "HOLD") != bool(stage_reasons):
            raise EvidenceError(
                "REASONS_COHERENCE_INVALID",
                f"finalManifest.stages.{name}.reasons",
                "nonempty iff status=HOLD",
                stage_reasons,
            )
        for index, value in enumerate(stage_reasons):
            issues = validate_reason(value, f"finalManifest.stages.{name}.reasons[{index}]")
            if issues:
                issue = issues[0]
                raise EvidenceError(issue["code"], issue["path"], issue["expected"], issue["observed"])
            if value not in reasons:
                raise EvidenceError(
                    "REASONS_COHERENCE_INVALID",
                    f"finalManifest.stages.{name}.reasons[{index}]",
                    "reason also present in finalManifest.reasons",
                    value,
                )
        if len(stage_artifacts) != len(set(stage_artifacts)) or not all(
            isinstance(value, str) and value for value in stage_artifacts
        ):
            raise EvidenceError("FIELD_TYPE_INVALID", f"finalManifest.stages.{name}.artifacts", "unique names", stage_artifacts)
    artifacts = document.get("artifacts")
    if not isinstance(artifacts, dict) or not artifacts:
        raise EvidenceError("FIELD_TYPE_INVALID", "finalManifest.artifacts", "nonempty object", artifacts)
    for name, record in artifacts.items():
        if not isinstance(record, dict) or set(record) != {"path", "state", "sha256", "missingReasonCode"}:
            raise EvidenceError("FIELD_TYPE_INVALID", f"finalManifest.artifacts.{name}", "exact artifact object", record)
        if record.get("state") not in {"PRESENT", "MISSING", "NOT_APPLICABLE"}:
            raise EvidenceError("FIELD_TYPE_INVALID", f"finalManifest.artifacts.{name}.state", "known state", record.get("state"))
        path_value = record.get("path")
        if not isinstance(path_value, str) or not path_value or not Path(path_value).is_absolute():
            raise EvidenceError("FIELD_TYPE_INVALID", f"finalManifest.artifacts.{name}.path", "absolute path", path_value)
        observed_hash = try_sha256(Path(path_value))
        if record["state"] == "PRESENT":
            if (
                not isinstance(record.get("sha256"), str)
                or SHA256_PATTERN.fullmatch(record["sha256"]) is None
                or record.get("missingReasonCode") is not None
                or observed_hash != record["sha256"]
            ):
                raise EvidenceError("METRICS_HASH_MISMATCH", f"finalManifest.artifacts.{name}", "present coherent hash", record)
        elif record["state"] == "MISSING":
            if (
                record.get("sha256") is not None
                or record.get("missingReasonCode") not in REASON_CODES
                or observed_hash is not None
            ):
                raise EvidenceError("ARTIFACT_MISSING", f"finalManifest.artifacts.{name}", "missing coherent record", record)
        elif (
            record.get("sha256") is not None
            or record.get("missingReasonCode") is not None
            or path_lexically_exists(Path(path_value))
        ):
            raise EvidenceError(
                "SEMANTIC_INVARIANT_INVALID",
                f"finalManifest.artifacts.{name}",
                "not-applicable absent path with null hashes",
                record,
            )
    unknown_artifacts = set(artifacts) - REQUIRED_SUCCESS_ARTIFACTS
    if unknown_artifacts:
        raise EvidenceError(
            "FIELD_UNEXPECTED",
            "finalManifest.artifacts",
            sorted(REQUIRED_SUCCESS_ARTIFACTS),
            sorted(unknown_artifacts),
        )
    if set(artifacts) != REQUIRED_SUCCESS_ARTIFACTS:
        raise EvidenceError(
            "ARTIFACT_MISSING",
            "finalManifest.artifacts",
            sorted(REQUIRED_SUCCESS_ARTIFACTS),
            sorted(artifacts),
        )
    if artifacts["artifactManifest"]["state"] == "PRESENT":
        validate_tick_attribution_bundle(Path(artifacts["artifactManifest"]["path"]))
    for name in STAGE_NAMES:
        unknown_artifacts = set(stages[name]["artifacts"]) - set(artifacts)
        if unknown_artifacts:
            raise EvidenceError("FIELD_UNEXPECTED", f"finalManifest.stages.{name}.artifacts", sorted(artifacts), sorted(unknown_artifacts))

    identity = document.get("identity")
    if not isinstance(identity, dict) or not identity.get("campaignId") or not identity.get("attemptId"):
        raise EvidenceError("IDENTITY_FIELD_MISSING", "finalManifest.identity", "attempt identity", identity)
    expected_identity_fields = set(CAPTURE_IDENTITY_FIELDS) | DERIVED_IDENTITY_FIELDS
    unexpected_identity_fields = set(identity) - expected_identity_fields
    if unexpected_identity_fields:
        raise EvidenceError(
            "FIELD_UNEXPECTED",
            "finalManifest.identity",
            sorted(expected_identity_fields),
            sorted(unexpected_identity_fields),
        )
    if set(identity) != expected_identity_fields:
        raise EvidenceError(
            "IDENTITY_FIELD_MISSING",
            "finalManifest.identity",
            sorted(expected_identity_fields),
            sorted(identity),
        )
    for field in ("campaignId", "attemptId"):
        if not isinstance(identity[field], str) or not identity[field]:
            raise EvidenceError("IDENTITY_FIELD_INVALID", f"finalManifest.identity.{field}", "nonempty string", identity[field])
    for field in ("attemptOrdinal",):
        if identity[field] is not None and (type(identity[field]) is not int or identity[field] <= 0):
            raise EvidenceError("NUMERIC_DOMAIN_INVALID", f"finalManifest.identity.{field}", "null or positive JSON integer", identity[field])
    for field in ("attemptKind", "captureNonce", "stage"):
        if identity[field] is not None and (not isinstance(identity[field], str) or not identity[field]):
            raise EvidenceError("IDENTITY_FIELD_INVALID", f"finalManifest.identity.{field}", "null or nonempty string", identity[field])
    if identity["activeStrategies"] is not None and not isinstance(identity["activeStrategies"], list):
        raise EvidenceError("FIELD_TYPE_INVALID", "finalManifest.identity.activeStrategies", "null or array", identity["activeStrategies"])
    if identity["attemptKind"] not in {"calibration", "warm-up", "official"}:
        raise EvidenceError("IDENTITY_FIELD_INVALID", "finalManifest.identity.attemptKind", "known attempt kind", identity["attemptKind"])
    if identity["stage"] not in STAGE_STRATEGIES or identity["activeStrategies"] != STAGE_STRATEGIES[identity["stage"]]:
        raise EvidenceError("STRATEGY_MISMATCH", "finalManifest.identity.activeStrategies", STAGE_STRATEGIES, identity["activeStrategies"])
    if not isinstance(identity["captureNonce"], str) or not identity["captureNonce"]:
        raise EvidenceError("IDENTITY_FIELD_INVALID", "finalManifest.identity.captureNonce", "nonempty string", identity["captureNonce"])
    for field in CAPTURE_IDENTITY_FIELDS - {
        "campaignId", "attemptId", "attemptOrdinal", "attemptKind", "captureNonce",
        "stage", "activeStrategies", "preBuildHeadSha", "postRestoreHeadSha",
    }:
        value = identity[field]
        if value is not None and (not isinstance(value, str) or SHA256_PATTERN.fullmatch(value) is None):
            raise EvidenceError("SHA_FORMAT_INVALID", f"finalManifest.identity.{field}", "null or lowercase SHA-256", value)
    for field in ("preBuildHeadSha", "postRestoreHeadSha", "metricsRevision"):
        value = identity[field]
        if value is not None and (not isinstance(value, str) or len(value) != 40 or any(character not in "0123456789abcdef" for character in value)):
            raise EvidenceError("SHA_FORMAT_INVALID", f"finalManifest.identity.{field}", "null or lowercase 40-hex", value)
    for field in ("workloadIds", "orderedRunKeys"):
        if not isinstance(identity[field], list):
            raise EvidenceError("FIELD_TYPE_INVALID", f"finalManifest.identity.{field}", "array", identity[field])
    for field in (
        "preBuildHeadSha", "preBuildWorktreeSha256", "runtimeTreeSha256",
        "runnerSha256", "performanceValidatorSha256", "cleanupValidatorSha256",
        "aggregatorSha256", "manifestToolSha256", "workloadContractSha256",
        "harnessSha256",
    ):
        if identity[field] is None:
            raise EvidenceError("IDENTITY_FIELD_MISSING", f"finalManifest.identity.{field}", "known before attempt start", None)
    artifact_identity_hashes = {
        "metricsSha256": "metrics",
        "playerArtifactSha256": "playerArtifact",
        "runnerSha256": "runner",
        "performanceValidatorSha256": "performanceValidator",
        "cleanupValidatorSha256": "cleanupValidator",
        "aggregatorSha256": "aggregator",
        "manifestToolSha256": "manifestTool",
        "workloadContractSha256": "workloadContract",
    }
    for field, artifact_name in artifact_identity_hashes.items():
        record = artifacts[artifact_name]
        if (
            record["state"] == "PRESENT"
            and identity[field] != record["sha256"]
            and strict_identity_binding
        ):
            raise EvidenceError(
                "IDENTITY_MISMATCH",
                f"finalManifest.identity.{field}",
                record["sha256"],
                identity[field],
            )
    if terminal_status in {"PASS", "DEFERRED"}:
        if set(artifacts) != REQUIRED_SUCCESS_ARTIFACTS or any(
            artifacts[name]["state"] != "PRESENT"
            for name in REQUIRED_SUCCESS_ARTIFACTS & set(artifacts)
        ):
            raise EvidenceError(
                "ARTIFACT_MISSING",
                "finalManifest.artifacts",
                sorted(REQUIRED_SUCCESS_ARTIFACTS),
                sorted(name for name, value in artifacts.items() if value["state"] == "PRESENT"),
            )
        for name in STAGE_NAMES:
            if stages[name]["artifacts"] != list(STAGE_ARTIFACTS[name]):
                raise EvidenceError(
                    "SEMANTIC_INVARIANT_INVALID",
                    f"finalManifest.stages.{name}.artifacts",
                    list(STAGE_ARTIFACTS[name]),
                    stages[name]["artifacts"],
                )
    if artifacts["metrics"]["state"] == "PRESENT":
        if set(identity) != expected_identity_fields:
            raise EvidenceError(
                "FIELD_UNEXPECTED",
                "finalManifest.identity",
                sorted(expected_identity_fields),
                sorted(identity),
            )
        metrics_document = None
        try:
            metrics_document = load_json_object(
                Path(artifacts["metrics"]["path"]),
                "finalManifest.artifacts.metrics",
            )
        except EvidenceError:
            if strict_identity_binding:
                raise
        complete_metrics_identity = (
            metrics_document is not None
            and metrics_document.get("schemaVersion") == 2
            and metrics_document.get("evidenceContractVersion") == 4
            and not validate_attempt_identity(metrics_document.get("captureIdentity"))
        )
        if complete_metrics_identity and strict_identity_binding:
            expected_identity = evidence_identity(
                metrics_document,
                artifacts["metrics"]["sha256"],
            )
            if identity != expected_identity:
                raise EvidenceError(
                    "IDENTITY_MISMATCH",
                    "finalManifest.identity",
                    expected_identity,
                    identity,
                )
        elif strict_identity_binding:
            raise EvidenceError(
                "IDENTITY_MISMATCH",
                "finalManifest.identity",
                "complete v4 metrics identity",
                identity,
            )
        if strict_identity_binding and (any(
            artifacts[name]["state"] == "PRESENT"
            and identity[field] != artifacts[name]["sha256"]
            for field, name in artifact_identity_hashes.items()
        ) or identity["workloadContractSha256"] != APPROVED_CLEANUP_S3_WORKLOAD_SHA256):
            raise EvidenceError(
                "IDENTITY_MISMATCH",
                "finalManifest.identity.artifactHashes",
                artifact_identity_hashes,
                identity,
            )
        if complete_metrics_identity and (
            identity.get("metricsRevision") != identity.get("preBuildHeadSha")
            or not isinstance(identity.get("metricsSha256"), str)
            or SHA256_PATTERN.fullmatch(identity["metricsSha256"]) is None
            or not isinstance(identity.get("workloadIds"), list)
            or not identity["workloadIds"]
            or not isinstance(identity.get("orderedRunKeys"), list)
            or not identity["orderedRunKeys"]
        ):
            raise EvidenceError("IDENTITY_MISMATCH", "finalManifest.identity", "complete terminal identity", identity)

    exit_status = document.get("exitStatus")
    if not isinstance(exit_status, dict):
        raise EvidenceError("FIELD_TYPE_INVALID", "finalManifest.exitStatus", "object", exit_status)
    expected_exit_fields = {
        "tickAttributionAdmission", "performanceAdmission", "cleanupAdmission",
        "cleanupCalibration",
    }
    if set(exit_status) - expected_exit_fields or any(
        type(value) is not int or value not in {0, 1, 2} for value in exit_status.values()
    ):
        raise EvidenceError(
            "EXIT_STATUS_MISMATCH",
            "finalManifest.exitStatus",
            sorted(expected_exit_fields),
            exit_status,
        )
    if terminal_status in {"PASS", "DEFERRED"}:
        if set(exit_status) != expected_exit_fields or any(type(value) is not int or value != 0 for value in exit_status.values()):
            raise EvidenceError("EXIT_STATUS_MISMATCH", "finalManifest.exitStatus", {key: 0 for key in expected_exit_fields}, exit_status)

    statuses = [stages[name]["status"] for name in STAGE_NAMES]
    if terminal_status == "PASS":
        expected_statuses = ["PASS"] * len(STAGE_NAMES)
    elif terminal_status == "DEFERRED":
        expected_statuses = ["PASS"] * len(STAGE_NAMES)
        expected_statuses[STAGE_NAMES.index("calibration")] = "DEFERRED"
    else:
        hold_indexes = [index for index, value in enumerate(statuses) if value == "HOLD"]
        if len(hold_indexes) != 1:
            raise EvidenceError("CARDINALITY_MISMATCH", "finalManifest.stages", "exactly one HOLD", statuses)
        hold_index = hold_indexes[0]
        expected_hold_stage = HOLD_VERDICT_STAGES.get(authoritative)
        if (
            expected_hold_stage is not None
            and STAGE_NAMES[hold_index] != expected_hold_stage
        ):
            raise EvidenceError(
                "SEMANTIC_INVARIANT_INVALID",
                "finalManifest.authoritativeVerdict",
                f"{authoritative} at {expected_hold_stage}",
                STAGE_NAMES[hold_index],
            )
        hold_reasons = stages[STAGE_NAMES[hold_index]]["reasons"]
        if reasons != hold_reasons:
            raise EvidenceError(
                "REASONS_COHERENCE_INVALID",
                "finalManifest.reasons",
                "exactly the HOLD stage reasons",
                reasons,
            )
        expected_reason_code = HOLD_VERDICT_REASON_CODES.get(authoritative)
        if expected_reason_code is not None and (
            len(hold_reasons) != 1 or hold_reasons[0]["code"] != expected_reason_code
        ):
            raise EvidenceError(
                "REASONS_COHERENCE_INVALID",
                "finalManifest.authoritativeVerdict",
                expected_reason_code,
                hold_reasons,
            )
        reached_exit_fields = {
            name
            for name in expected_exit_fields
            if STAGE_NAMES.index(
                "calibration" if name == "cleanupCalibration" else name
            ) <= hold_index
        }
        if set(exit_status) != reached_exit_fields:
            raise EvidenceError(
                "EXIT_STATUS_MISMATCH",
                "finalManifest.exitStatus",
                sorted(reached_exit_fields),
                exit_status,
            )
        expected_hold_exits = {
            "HOLD_TICK_ATTRIBUTION_ADMISSION": {"tickAttributionAdmission": 1},
            "HOLD_PERFORMANCE_ADMISSION": {
                "tickAttributionAdmission": 0, "performanceAdmission": 1,
            },
            "HOLD_CLEANUP_ADMISSION": {
                "tickAttributionAdmission": 0,
                "performanceAdmission": 0,
                "cleanupAdmission": 1,
            },
            "HOLD_INVALID_SIGNAL": {
                "tickAttributionAdmission": 0,
                "performanceAdmission": 0,
                "cleanupAdmission": 0,
                "cleanupCalibration": 1,
            },
        }.get(authoritative)
        if expected_hold_exits is not None and exit_status != expected_hold_exits:
            raise EvidenceError(
                "EXIT_STATUS_MISMATCH",
                "finalManifest.exitStatus",
                expected_hold_exits,
                exit_status,
            )
        expected_statuses = ["PASS"] * hold_index + ["HOLD"] + ["NOT_RUN"] * (len(STAGE_NAMES) - hold_index - 1)
        if hold_index == STAGE_NAMES.index("consistencyFinalization") and statuses[STAGE_NAMES.index("calibration")] == "DEFERRED":
            expected_statuses[STAGE_NAMES.index("calibration")] = "DEFERRED"
    if statuses != expected_statuses:
        raise EvidenceError("STAGE_NOT_RUN", "finalManifest.stages", expected_statuses, statuses)
    for index, name in enumerate(STAGE_NAMES):
        expected_artifacts = [] if statuses[index] == "NOT_RUN" else list(STAGE_ARTIFACTS[name])
        if stages[name]["artifacts"] != expected_artifacts:
            raise EvidenceError(
                "SEMANTIC_INVARIANT_INVALID",
                f"finalManifest.stages.{name}.artifacts",
                expected_artifacts,
                stages[name]["artifacts"],
            )
    # There is intentionally no runtime flag that can enable success transport.
    # A future oracle amendment must replace this gate together with frozen-oracle
    # parsing, hashing, and exact comparison in one reviewed code change.
    if terminal_status in {"PASS", "DEFERRED"}:
        raise EvidenceError(
            "FULL_SCAN_EXPECTATION_UNAPPROVED",
            "finalManifest.terminalStatus",
            "HOLD until approved independent exact visit oracle",
            terminal_status,
        )
    return terminal_status


def _append(reasons: list[dict[str, Any]], value: dict[str, Any]) -> None:
    if value not in reasons:
        reasons.append(value)


def _load_json(path: Path | None, label: str, reasons: list[dict[str, Any]]) -> dict[str, Any] | None:
    if path is None:
        _append(reasons, reason("ARTIFACT_MISSING", label, "PRESENT", "MISSING"))
        return None
    try:
        return load_json_object(path, label)
    except EvidenceError as error:
        _append(reasons, error.reason)
        return None


def _best_effort_kv(path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    try:
        for line in path.read_text(encoding="utf-8-sig").splitlines():
            if "=" in line:
                key, value = line.split("=", 1)
                if key and key not in values:
                    values[key] = value
    except (OSError, UnicodeError):
        pass
    return values


def _load_kv(path: Path, label: str, reasons: list[dict[str, Any]]) -> dict[str, str]:
    try:
        return load_strict_kv(path, label)
    except EvidenceError as error:
        _append(reasons, error.reason)
        return _best_effort_kv(path)


def _equal(reasons: list[dict[str, Any]], code: str, path: str, expected: Any, observed: Any) -> None:
    if type(expected) is not type(observed) or expected != observed:
        _append(reasons, reason(code, path, expected, observed))


def _validate_report(report: dict[str, Any] | None, label: str, success_field: str, success_values: set[str], caller_status: int, reasons: list[dict[str, Any]]) -> str | None:
    if report is None:
        return None
    _equal(reasons, "SCHEMA_VERSION_INVALID", f"{label}.schemaVersion", 2, report.get("schemaVersion"))
    _equal(reasons, "CONTRACT_VERSION_INVALID", f"{label}.evidenceContractVersion", 4, report.get("evidenceContractVersion"))
    value = report.get(success_field)
    report_reasons = report.get("reasons")
    success = value in success_values
    if not isinstance(report_reasons, list):
        _append(reasons, reason("FIELD_TYPE_INVALID", f"{label}.reasons", "array", report_reasons))
    elif success != (len(report_reasons) == 0):
        _append(reasons, reason("REASONS_COHERENCE_INVALID", f"{label}.reasons", "empty iff success", report_reasons))
    expected_exit = 0 if success else 1
    if caller_status != expected_exit:
        _append(reasons, reason("EXIT_STATUS_MISMATCH", f"exitStatus.{label}", expected_exit, caller_status))
    return value if isinstance(value, str) else None


def _live_identity_pre_replace_check(lifecycle_identity: dict[str, Any]):
    expected = {
        "headSha": lifecycle_identity.get("preBuildHeadSha"),
        "worktreeSha256": lifecycle_identity.get("preBuildWorktreeSha256"),
        "runtimeTreeSha256": lifecycle_identity.get("runtimeTreeSha256"),
    }
    repository_root = canonical_repository_root(Path(__file__))

    def check() -> None:
        observed = live_source_identity(repository_root)
        for field, code in (
            ("headSha", "PRE_POST_HEAD_MISMATCH"),
            ("worktreeSha256", "PRE_POST_WORKTREE_MISMATCH"),
            ("runtimeTreeSha256", "RUNTIME_TREE_MISMATCH"),
        ):
            if observed[field] != expected[field]:
                raise EvidenceError(code, f"live.{field}", expected[field], observed[field])

    return check


def build_manifest(
    *, metrics: Path, runtime_log: Path, preflight_manifest: Path, artifact_manifest: Path,
    cleanup_admission: Path, cleanup_calibration: Path | None, validator: Path, aggregator: Path,
    workload_contract: Path, performance_admission_status: int, cleanup_admission_status: int,
    cleanup_calibration_status: int, performance_admission: Path | None = None,
    allocation_diagnostic: Path | None = None,
    performance_validator: Path | None = None,
    tick_attribution: Path | None = None,
    tick_attribution_report: Path | None = None,
    tick_attribution_validator: Path | None = None,
    runner: Path | None = None,
    player_artifact: Path | None = None, build_log: Path | None = None,
    build_root: Path | None = None,
    lifecycle_manifest: dict[str, Any] | None = None,
) -> dict[str, Any]:
    reasons: list[dict[str, Any]] = []
    input_paths = {
        "metrics": metrics, "runtimeLog": runtime_log, "preflightManifest": preflight_manifest,
        "artifactManifest": artifact_manifest, "performanceAdmission": performance_admission,
        "tickAttribution": tick_attribution,
        "tickAttributionReport": tick_attribution_report,
        "tickAttributionValidator": tick_attribution_validator,
        "cleanupAdmission": cleanup_admission, "cleanupCalibration": cleanup_calibration,
        "performanceValidator": performance_validator, "cleanupValidator": validator,
        "aggregator": aggregator, "workloadContract": workload_contract,
        "runner": runner, "manifestTool": Path(__file__).resolve(),
        "playerArtifact": player_artifact, "buildLog": build_log,
    }
    validation_paths = dict(input_paths)
    if allocation_diagnostic is not None:
        validation_paths["allocationDiagnostic"] = allocation_diagnostic
    initial_hashes = {
        name: try_sha256(path) if path is not None else None
        for name, path in validation_paths.items()
    }
    seen_paths: dict[Path, str] = {}
    seen_inodes: dict[tuple[int, int], str] = {}
    for name, path in validation_paths.items():
        if path is None:
            continue
        resolved = path.resolve(strict=False)
        if resolved in seen_paths:
            _append(reasons, reason("INPUT_ARTIFACT_ALIAS", name, "distinct path", seen_paths[resolved]))
        else:
            seen_paths[resolved] = name
        try:
            stat = resolved.stat()
        except OSError:
            continue
        inode = (stat.st_dev, stat.st_ino)
        if inode in seen_inodes:
            _append(reasons, reason("INPUT_ARTIFACT_ALIAS", name, "distinct inode", seen_inodes[inode]))
        else:
            seen_inodes[inode] = name
    for name, expected in CANONICAL_TOOL_PATHS.items():
        observed = input_paths.get(name)
        if observed is None or observed.resolve(strict=False) != expected.resolve(strict=False):
            _append(
                reasons,
                reason(
                    "TOOL_HASH_MISMATCH",
                    name,
                    str(expected.resolve(strict=False)),
                    str(observed.resolve(strict=False)) if observed is not None else None,
                ),
            )
    observed_workload_hash = try_sha256(workload_contract)
    if observed_workload_hash != APPROVED_CLEANUP_S3_WORKLOAD_SHA256:
        _append(
            reasons,
            reason(
                "TOOL_HASH_MISMATCH",
                "workloadContract.approvedSha256",
                APPROVED_CLEANUP_S3_WORKLOAD_SHA256,
                observed_workload_hash,
            ),
        )
    try:
        resolved_build_root = build_root.resolve(strict=True) if build_root is not None else None
        resolved_player = player_artifact.resolve(strict=True) if player_artifact is not None else None
        if resolved_build_root is None or not resolved_build_root.is_dir():
            raise EvidenceError("BUILD_ROOT_INVALID", "buildRoot", "existing directory", str(build_root))
        if (
            resolved_player is None
            or not resolved_player.is_file()
            or resolved_build_root not in resolved_player.parents
        ):
            raise EvidenceError(
                "BUILD_ROOT_INVALID",
                "playerArtifact",
                "regular file inside buildRoot",
                str(player_artifact),
            )
    except (OSError, EvidenceError) as error:
        _append(
            reasons,
            error.reason
            if isinstance(error, EvidenceError)
            else reason("BUILD_ROOT_INVALID", "buildRoot", "readable build tree", str(error)),
        )
    artifacts = {name: artifact(path) for name, path in input_paths.items()}
    for name, value in artifacts.items():
        if value["state"] != "PRESENT" and not (
            name == "cleanupCalibration" and allocation_diagnostic is not None
        ):
            _append(reasons, reason("ARTIFACT_MISSING", f"artifacts.{name}", "PRESENT", "MISSING"))

    preflight = _load_kv(preflight_manifest, "preflightManifest", reasons)
    captured = _load_kv(artifact_manifest, "artifactManifest", reasons)
    metrics_document = _load_json(metrics, "metrics", reasons)
    performance = _load_json(performance_admission, "performanceAdmission", reasons)
    cleanup = _load_json(cleanup_admission, "cleanupAdmission", reasons)
    calibration = (
        _load_json(cleanup_calibration, "cleanupCalibration", reasons)
        if cleanup_calibration is not None and allocation_diagnostic is None
        else None
    )
    diagnostic = (
        _load_json(allocation_diagnostic, "allocationDiagnostic", reasons)
        if allocation_diagnostic is not None
        else None
    )
    for marker_reason in validate_runtime_marker(runtime_log):
        _append(reasons, marker_reason)

    for label, values in (("preflight", preflight), ("captured", captured)):
        _equal(reasons, "SCHEMA_VERSION_INVALID", f"{label}.SchemaVersion", "2", values.get("SchemaVersion"))
        _equal(reasons, "CONTRACT_VERSION_INVALID", f"{label}.EvidenceContractVersion", "4", values.get("EvidenceContractVersion"))
    _equal(reasons, "SEMANTIC_INVARIANT_INVALID", "preflight.EvidencePhase", "preflight", preflight.get("EvidencePhase"))
    _equal(reasons, "SEMANTIC_INVARIANT_INVALID", "captured.EvidencePhase", "artifact-captured", captured.get("EvidencePhase"))

    pre_head = preflight.get("PreBuildHeadSha")
    post_head = captured.get("PostRestoreHeadSha")
    pre_worktree = preflight.get("PreBuildWorktreeSha256")
    post_worktree = captured.get("PostRestoreWorktreeSha256")
    _equal(reasons, "PRE_POST_HEAD_MISMATCH", "source.head", pre_head, post_head)
    _equal(reasons, "PRE_POST_WORKTREE_MISMATCH", "source.worktree", pre_worktree, post_worktree)

    identity_keys = (
        "CampaignId", "AttemptId", "AttemptOrdinal", "AttemptKind", "CaptureNonce", "Stage",
        "ActiveStrategies", "GitStatusShort", "PreBuildHeadSha", "PreBuildWorktreeSha256", "RuntimeTreeSha256",
        "RunnerSha256", "PerformanceValidatorSha256",
        "CleanupValidatorSha256", "AggregatorSha256", "ManifestToolSha256",
        "WorkloadContractSha256", "HarnessSha256",
    )
    if any("PerformanceAdmissionPolicy" in values for values in (preflight, captured)):
        identity_keys += ("PerformanceAdmissionPolicy",)
    settings_keys = {
        "ExpectedWidth", "ExpectedHeight", "ExpectedWarmupFrames",
        "ExpectedSampleFrames", "ExpectedTickInterval",
    }
    preflight_keys = {
        "SchemaVersion", "EvidenceContractVersion", "EvidencePhase", "GitStatusShort",
        *identity_keys, *settings_keys,
    }
    captured_keys = preflight_keys | {
        "PostRestoreHeadSha", "PostRestoreWorktreeSha256", "PlayerArtifactSha256",
        "BuildPayloadSHA256", "MetricsSHA256", "RuntimeLogSHA256",
    }
    present_tick_attribution_hash_keys = set(captured) & TICK_ATTRIBUTION_HASH_KEYS
    if present_tick_attribution_hash_keys:
        captured_keys |= TICK_ATTRIBUTION_HASH_KEYS
    for label, values, expected_keys in (
        ("preflight", preflight, preflight_keys),
        ("captured", captured, captured_keys),
    ):
        for key in sorted(expected_keys - set(values)):
            _append(reasons, reason("FIELD_MISSING", f"{label}.{key}", "present", None))
        for key in sorted(set(values) - expected_keys):
            _append(reasons, reason("FIELD_UNEXPECTED", f"{label}.{key}", None, values[key]))
    for key in identity_keys:
        if key not in preflight or key not in captured:
            _append(reasons, reason("IDENTITY_FIELD_MISSING", key, "present in both KV manifests", None))
        else:
            _equal(reasons, "IDENTITY_MISMATCH", key, preflight[key], captured[key])

    actual_tool_hashes = {
        "RunnerSha256": try_sha256(runner) if runner is not None else None,
        "PerformanceValidatorSha256": try_sha256(performance_validator) if performance_validator is not None else None,
        "CleanupValidatorSha256": try_sha256(validator),
        "AggregatorSha256": try_sha256(aggregator),
        "ManifestToolSha256": try_sha256(Path(__file__).resolve()),
        "WorkloadContractSha256": try_sha256(workload_contract),
    }
    for key, actual in actual_tool_hashes.items():
        _equal(reasons, "TOOL_HASH_MISMATCH", key, actual, preflight.get(key))
    if all(actual_tool_hashes.values()):
        actual_harness = harness_sha256(
            runner_sha256=actual_tool_hashes["RunnerSha256"],
            performance_validator_sha256=actual_tool_hashes["PerformanceValidatorSha256"],
            cleanup_validator_sha256=actual_tool_hashes["CleanupValidatorSha256"],
            aggregator_sha256=actual_tool_hashes["AggregatorSha256"],
            manifest_tool_sha256=actual_tool_hashes["ManifestToolSha256"],
            workload_contract_sha256=actual_tool_hashes["WorkloadContractSha256"],
        )
        _equal(reasons, "TOOL_HASH_MISMATCH", "HarnessSha256", actual_harness, preflight.get("HarnessSha256"))
    try:
        expected_runtime_tree = runtime_tree_sha256(pre_head, pre_worktree)
    except (AttributeError, UnicodeError):
        expected_runtime_tree = None
    _equal(reasons, "RUNTIME_TREE_MISMATCH", "RuntimeTreeSha256", expected_runtime_tree, preflight.get("RuntimeTreeSha256"))
    _equal(reasons, "PLAYER_ARTIFACT_HASH_MISMATCH", "PlayerArtifactSha256", try_sha256(player_artifact) if player_artifact is not None else None, captured.get("PlayerArtifactSha256"))
    try:
        actual_build_payload = build_payload_sha256(build_root) if build_root is not None else None
    except (OSError, EvidenceError):
        actual_build_payload = None
    _equal(reasons, "BUILD_PAYLOAD_HASH_MISMATCH", "BuildPayloadSHA256", actual_build_payload, captured.get("BuildPayloadSHA256"))

    metrics_hash = artifacts["metrics"]["sha256"]
    runtime_hash = artifacts["runtimeLog"]["sha256"]
    _equal(reasons, "METRICS_HASH_MISMATCH", "captured.MetricsSHA256", metrics_hash, captured.get("MetricsSHA256"))
    _equal(reasons, "IDENTITY_MISMATCH", "captured.RuntimeLogSHA256", runtime_hash, captured.get("RuntimeLogSHA256"))
    if present_tick_attribution_hash_keys:
        try:
            validate_tick_attribution_bundle(artifact_manifest)
        except EvidenceError as error:
            _append(reasons, error.reason)

    if metrics_document is not None:
        _equal(reasons, "SCHEMA_VERSION_INVALID", "metrics.schemaVersion", 2, metrics_document.get("schemaVersion"))
        _equal(reasons, "CONTRACT_VERSION_INVALID", "metrics.evidenceContractVersion", 4, metrics_document.get("evidenceContractVersion"))
        capture_identity = metrics_document.get("captureIdentity")
        if not isinstance(capture_identity, dict):
            _append(reasons, reason("IDENTITY_FIELD_MISSING", "metrics.captureIdentity", "object", capture_identity))
        else:
            for identity_reason in validate_attempt_identity(capture_identity):
                _append(reasons, identity_reason)
            mappings = {
                "campaignId": "CampaignId", "attemptId": "AttemptId", "attemptKind": "AttemptKind",
                "captureNonce": "CaptureNonce", "stage": "Stage", "preBuildHeadSha": "PreBuildHeadSha",
                "preBuildWorktreeSha256": "PreBuildWorktreeSha256", "runtimeTreeSha256": "RuntimeTreeSha256",
                "runnerSha256": "RunnerSha256", "performanceValidatorSha256": "PerformanceValidatorSha256",
                "cleanupValidatorSha256": "CleanupValidatorSha256", "aggregatorSha256": "AggregatorSha256",
                "manifestToolSha256": "ManifestToolSha256", "workloadContractSha256": "WorkloadContractSha256",
                "harnessSha256": "HarnessSha256",
            }
            for json_key, kv_key in mappings.items():
                _equal(reasons, "IDENTITY_MISMATCH", f"metrics.captureIdentity.{json_key}", preflight.get(kv_key), capture_identity.get(json_key))
            captured_mappings = {
                "postRestoreHeadSha": "PostRestoreHeadSha",
                "postRestoreWorktreeSha256": "PostRestoreWorktreeSha256",
                "playerArtifactSha256": "PlayerArtifactSha256",
                "buildPayloadSha256": "BuildPayloadSHA256",
            }
            for json_key, kv_key in captured_mappings.items():
                _equal(reasons, "IDENTITY_MISMATCH", f"metrics.captureIdentity.{json_key}", captured.get(kv_key), capture_identity.get(json_key))
            _equal(reasons, "STRATEGY_MISMATCH", "metrics.captureIdentity.activeStrategies", ["A"], capture_identity.get("activeStrategies"))
            _equal(reasons, "METRICS_REVISION_MISMATCH", "metrics.revision", pre_head, metrics_document.get("revision"))
            expected_ordinal: Any = preflight.get("AttemptOrdinal")
            try:
                expected_ordinal = int(expected_ordinal)
            except (TypeError, ValueError):
                pass
            _equal(reasons, "IDENTITY_MISMATCH", "metrics.captureIdentity.attemptOrdinal", expected_ordinal, capture_identity.get("attemptOrdinal"))

    performance_verdict = _validate_report(performance, "performanceAdmission", "verdict", {"ADMITTED"}, performance_admission_status, reasons)
    if metrics_document is not None and performance is not None and performance_validator is not None:
        try:
            canonical_performance = build_metrics_report(
                metrics_document,
                metrics_sha256=metrics_hash,
                validator_path=performance_validator,
                planned_revision=pre_head,
                expected_width=int(preflight.get("ExpectedWidth")),
                expected_height=int(preflight.get("ExpectedHeight")),
                expected_warmup_frames=int(preflight.get("ExpectedWarmupFrames")),
                expected_sample_frames=int(preflight.get("ExpectedSampleFrames")),
                expected_tick_interval=int(preflight.get("ExpectedTickInterval")),
                admission_policy=preflight.get("PerformanceAdmissionPolicy", "strict-v1"),
            )
        except (TypeError, ValueError, IndexError, ArithmeticError) as error:
            _append(reasons, reason("PERSISTED_REPORT_MISMATCH", "performanceAdmission", "canonical report", str(error)))
        else:
            if performance != canonical_performance:
                _append(reasons, reason("PERSISTED_REPORT_MISMATCH", "performanceAdmission", canonical_performance, performance))
    cleanup_verdict = _validate_report(cleanup, "cleanupAdmission", "verdict", {"ADMITTED"}, cleanup_admission_status, reasons)
    calibration_status = _validate_report(calibration, "cleanupCalibration", "status", {"READY", "DEFERRED_NOT_MATERIAL"}, cleanup_calibration_status, reasons)
    canonical_cleanup = None
    if metrics_document is not None and cleanup is not None:
        canonical_cleanup = build_admission_report(
            metrics_document,
            metrics_sha256=metrics_hash,
            active_strategies=("A",),
            validator_path=validator,
            workload_contract_path=workload_contract,
        )
        if cleanup != canonical_cleanup:
            _append(reasons, reason("PERSISTED_REPORT_MISMATCH", "cleanupAdmission", canonical_cleanup, cleanup))
    if metrics_document is not None and calibration is not None:
        canonical_calibration = build_calibration_report(
            metrics_document,
            metrics_sha256=metrics_hash,
            validator_path=validator,
            aggregator_path=aggregator,
            workload_contract_path=workload_contract,
        )
        if calibration != canonical_calibration:
            _append(reasons, reason("PERSISTED_REPORT_MISMATCH", "cleanupCalibration", canonical_calibration, calibration))
    allocation_only = False
    if allocation_diagnostic is not None:
        diagnostic_status = _validate_report(
            diagnostic,
            "allocationDiagnostic",
            "status",
            {"READY", "DEFERRED_NOT_MATERIAL"},
            cleanup_calibration_status,
            reasons,
        )
        canonical_diagnostic = None
        if metrics_document is not None and diagnostic is not None:
            canonical_diagnostic = build_calibration_report(
                metrics_document,
                metrics_sha256=metrics_hash,
                validator_path=validator,
                aggregator_path=aggregator,
                workload_contract_path=workload_contract,
            )
            if diagnostic != canonical_diagnostic:
                _append(
                    reasons,
                    reason(
                        "PERSISTED_REPORT_MISMATCH",
                        "allocationDiagnostic",
                        canonical_diagnostic,
                        diagnostic,
                    ),
                )
        allocation_only = (
            cleanup is not None
            and cleanup == canonical_cleanup
            and is_allocation_only_cleanup_report(cleanup)
            and diagnostic is not None
            and diagnostic == canonical_diagnostic
            and diagnostic_status == "HOLD_INVALID_EVIDENCE"
            and is_allocation_only_diagnostic_report(diagnostic)
        )
        if not allocation_only:
            _append(
                reasons,
                reason(
                    "SEMANTIC_INVARIANT_INVALID",
                    "allocationDiagnostic",
                    "exact allocation-only Cleanup rejection diagnostic",
                    diagnostic_status,
                ),
            )

    if allocation_only:
        if cleanup_calibration is None:
            _append(
                reasons,
                reason(
                    "FIELD_MISSING",
                    "cleanupCalibrationArtifactPath",
                    "planned authoritative artifact path",
                    None,
                ),
            )
        elif path_lexically_exists(cleanup_calibration):
            _append(
                reasons,
                reason(
                    "SEMANTIC_INVARIANT_INVALID",
                    "cleanupCalibrationArtifactPath",
                    "absent for allocation-only diagnostic",
                    str(cleanup_calibration.resolve(strict=False)),
                ),
            )
        else:
            artifacts["cleanupCalibration"] = not_applicable_artifact(cleanup_calibration)
    elif artifacts["cleanupCalibration"]["state"] != "PRESENT":
        _append(
            reasons,
            reason(
                "ARTIFACT_MISSING",
                "artifacts.cleanupCalibration",
                "PRESENT",
                artifacts["cleanupCalibration"]["state"],
            ),
        )
    if calibration is not None:
        semantic = {
            "READY": (True, True, True, True, False),
            "DEFERRED_NOT_MATERIAL": (True, True, False, True, False),
            "HOLD_INVALID_SIGNAL": (True, False, None, False, True),
            "HOLD_INVALID_EVIDENCE": (False, None, None, False, True),
        }.get(calibration_status)
        if semantic is None:
            _append(reasons, reason("FIELD_TYPE_INVALID", "cleanupCalibration.status", "known status", calibration_status))
        else:
            admitted, signal, material, thresholds_present, reasons_required = semantic
            _equal(reasons, "SEMANTIC_INVARIANT_INVALID", "cleanupCalibration.admitted", admitted, calibration.get("admitted"))
            _equal(reasons, "SEMANTIC_INVARIANT_INVALID", "cleanupCalibration.signalValid", signal, calibration.get("signalValid"))
            if material is not None:
                _equal(reasons, "SEMANTIC_INVARIANT_INVALID", "cleanupCalibration.attributionMaterial", material, calibration.get("attributionMaterial"))
            elif calibration_status == "HOLD_INVALID_SIGNAL" and type(calibration.get("attributionMaterial")) is not bool:
                _append(reasons, reason("SEMANTIC_INVARIANT_INVALID", "cleanupCalibration.attributionMaterial", "boolean observation", calibration.get("attributionMaterial")))
            if (calibration.get("thresholds") is not None) != thresholds_present:
                _append(reasons, reason("THRESHOLDS_REQUIRED" if thresholds_present else "THRESHOLDS_FORBIDDEN", "cleanupCalibration.thresholds", thresholds_present, calibration.get("thresholds")))
            if reasons_required and not calibration.get("reasons"):
                _append(reasons, reason("REASONS_COHERENCE_INVALID", "cleanupCalibration.reasons", "nonempty", calibration.get("reasons")))
            if calibration_status in {"READY", "DEFERRED_NOT_MATERIAL"}:
                _equal(reasons, "SEMANTIC_INVARIANT_INVALID", "cleanupCalibration.captureOffNonInterfering", True, calibration.get("captureOffNonInterfering"))
                if calibration.get("observations") is None:
                    _append(reasons, reason("FIELD_MISSING", "cleanupCalibration.observations", "non-null", None))
                if calibration.get("campaignRules") is None:
                    _append(reasons, reason("FIELD_MISSING", "cleanupCalibration.campaignRules", "non-null", None))
            elif calibration_status == "HOLD_INVALID_SIGNAL":
                if calibration.get("observations") is None:
                    _append(reasons, reason("FIELD_MISSING", "cleanupCalibration.observations", "non-null", None))
                if calibration.get("campaignRules") is not None:
                    _append(reasons, reason("THRESHOLDS_FORBIDDEN", "cleanupCalibration.campaignRules", None, calibration.get("campaignRules")))
            elif calibration_status == "HOLD_INVALID_EVIDENCE":
                for field in ("captureOffNonInterfering", "observations", "campaignRules"):
                    _equal(reasons, "SEMANTIC_INVARIANT_INVALID", f"cleanupCalibration.{field}", None, calibration.get(field))

    for name, path in validation_paths.items():
        final_hash = try_sha256(path) if path is not None else None
        if final_hash != initial_hashes[name]:
            _append(reasons, reason("INPUT_MUTATED_DURING_VALIDATION", name, initial_hashes[name], final_hash))

    try:
        repository_root = canonical_repository_root(Path(__file__))
        live_identity = live_source_identity(repository_root)
        _equal(reasons, "PRE_POST_HEAD_MISMATCH", "live.headSha", pre_head, live_identity["headSha"])
        _equal(reasons, "PRE_POST_WORKTREE_MISMATCH", "live.worktreeSha256", pre_worktree, live_identity["worktreeSha256"])
        _equal(
            reasons,
            "RUNTIME_TREE_MISMATCH",
            "live.runtimeTreeSha256",
            preflight.get("RuntimeTreeSha256"),
            live_identity["runtimeTreeSha256"],
        )
    except (EvidenceError, OSError, subprocess.SubprocessError) as error:
        _append(
            reasons,
            error.reason
            if isinstance(error, EvidenceError)
            else reason("SEMANTIC_INVARIANT_INVALID", "liveSourceIdentity", "readable live Git identity", str(error)),
        )

    if lifecycle_manifest is None:
        _append(reasons, reason("STAGE_NOT_RUN", "lifecycleManifest", "existing provisional manifest", None))
        stages = empty_stages()
    else:
        stages = lifecycle_manifest.get("stages")
        if (
            lifecycle_manifest.get("manifestState") != "PROVISIONAL"
            or lifecycle_manifest.get("terminalStatus") != "NOT_RUN"
            or not isinstance(stages, dict)
        ):
            _append(
                reasons,
                reason(
                    "SEMANTIC_INVARIANT_INVALID",
                    "lifecycleManifest",
                    "valid PROVISIONAL manifest",
                    lifecycle_manifest.get("manifestState"),
                ),
            )
            stages = empty_stages()
        else:
            semantic_hold_stage = None
            if performance_verdict is not None and performance_verdict != "ADMITTED":
                semantic_hold_stage = "performanceAdmission"
            elif cleanup_verdict is not None and cleanup_verdict != "ADMITTED":
                semantic_hold_stage = "cleanupAdmission"
            elif calibration_status in {"HOLD_INVALID_SIGNAL", "HOLD_INVALID_EVIDENCE"}:
                semantic_hold_stage = "calibration"
            semantic_hold_index = (
                STAGE_NAMES.index(semantic_hold_stage)
                if semantic_hold_stage is not None
                else None
            )
            for index, name in enumerate(STAGE_NAMES[:-1]):
                if semantic_hold_index is not None:
                    expected_status = (
                        "PASS"
                        if index < semantic_hold_index
                        else "HOLD"
                        if index == semantic_hold_index
                        else "NOT_RUN"
                    )
                else:
                    expected_status = (
                        "DEFERRED"
                        if name == "calibration" and calibration_status == "DEFERRED_NOT_MATERIAL"
                        else "PASS"
                    )
                _equal(
                    reasons,
                    "STAGE_NOT_RUN",
                    f"stages.{name}.status",
                    expected_status,
                    stages.get(name, {}).get("status"),
                )
            _equal(
                reasons,
                "STAGE_NOT_RUN",
                "stages.consistencyFinalization.status",
                "NOT_RUN",
                stages.get("consistencyFinalization", {}).get("status"),
            )

    evidence_invalid = bool(reasons)
    if performance_verdict is not None and performance_verdict != "ADMITTED":
        _append(reasons, reason("PERFORMANCE_REJECTED", "performanceAdmission.verdict", "ADMITTED", performance_verdict))
    elif cleanup_verdict is not None and cleanup_verdict != "ADMITTED":
        _append(reasons, reason("CLEANUP_REJECTED", "cleanupAdmission.verdict", "ADMITTED", cleanup_verdict))
    elif calibration_status in {"HOLD_INVALID_SIGNAL", "HOLD_INVALID_EVIDENCE"}:
        _append(reasons, reason("SIGNAL_INVALID", "cleanupCalibration.status", "READY or DEFERRED_NOT_MATERIAL", calibration_status))

    full_scan_blocked = (
        not reasons
        and performance_verdict == "ADMITTED"
        and cleanup_verdict == "ADMITTED"
        and calibration_status in {"READY", "DEFERRED_NOT_MATERIAL"}
    )
    if full_scan_blocked:
        _append(
            reasons,
            reason(
                "FULL_SCAN_EXPECTATION_UNAPPROVED",
                "cleanupSlice3Calibration.fullScanEntityVisitCount",
                "approved independent exact visit oracle",
                None,
            ),
        )

    if evidence_invalid or full_scan_blocked:
        terminal_status, authoritative = "HOLD", "HOLD_INVALID_EVIDENCE"
    elif performance_verdict != "ADMITTED":
        terminal_status, authoritative = "HOLD", "HOLD_PERFORMANCE_ADMISSION"
    elif cleanup_verdict != "ADMITTED":
        terminal_status, authoritative = "HOLD", "HOLD_CLEANUP_ADMISSION"
    elif calibration_status == "READY":
        terminal_status, authoritative = "PASS", "READY"
    elif calibration_status == "DEFERRED_NOT_MATERIAL":
        terminal_status, authoritative = "DEFERRED", "DEFERRED_NOT_MATERIAL"
    elif calibration_status == "HOLD_INVALID_SIGNAL":
        terminal_status, authoritative = "HOLD", "HOLD_INVALID_SIGNAL"
    else:
        terminal_status, authoritative = "HOLD", "HOLD_INVALID_EVIDENCE"

    stages = json.loads(json.dumps(stages))
    for name in STAGE_NAMES:
        stages[name]["artifacts"] = [
            artifact_name
            for artifact_name in STAGE_ARTIFACTS[name]
            if artifact_name in artifacts
        ]
    semantic_hold_stages = {
        "HOLD_PERFORMANCE_ADMISSION": ("performanceAdmission", "PERFORMANCE_REJECTED"),
        "HOLD_CLEANUP_ADMISSION": ("cleanupAdmission", "CLEANUP_REJECTED"),
        "HOLD_INVALID_SIGNAL": ("calibration", "SIGNAL_INVALID"),
    }
    semantic_hold = semantic_hold_stages.get(authoritative)
    existing_hold_stages = [
        name for name in STAGE_NAMES if stages[name].get("status") == "HOLD"
    ]
    effective_hold_stage = semantic_hold[0] if semantic_hold is not None else None
    if (
        terminal_status == "HOLD"
        and effective_hold_stage is None
        and len(existing_hold_stages) == 1
    ):
        effective_hold_stage = existing_hold_stages[0]
    if terminal_status == "HOLD" and effective_hold_stage is not None:
        hold_stage = effective_hold_stage
        hold_index = STAGE_NAMES.index(hold_stage)
        for name in STAGE_NAMES[hold_index + 1:]:
            stages[name]["status"] = "NOT_RUN"
            stages[name]["reasons"] = []
            stages[name]["artifacts"] = []
        stages[hold_stage]["status"] = "HOLD"
        stages[hold_stage]["reasons"] = list(reasons)
    else:
        stages["consistencyFinalization"]["status"] = "HOLD" if terminal_status == "HOLD" else "PASS"
        stages["consistencyFinalization"]["reasons"] = reasons if terminal_status == "HOLD" else []
    final_exit_status = {
        "tickAttributionAdmission": 0,
        "performanceAdmission": performance_admission_status,
        "cleanupAdmission": cleanup_admission_status,
        "cleanupCalibration": cleanup_calibration_status,
    }
    if terminal_status == "HOLD" and effective_hold_stage is not None:
        hold_index = STAGE_NAMES.index(effective_hold_stage)
        final_exit_status = {
            name: value
            for name, value in final_exit_status.items()
            if STAGE_NAMES.index("calibration" if name == "cleanupCalibration" else name) <= hold_index
        }
    final_identity = {
        field: (lifecycle_manifest or {}).get("identity", {}).get(field)
        for field in CAPTURE_IDENTITY_FIELDS | DERIVED_IDENTITY_FIELDS
    }
    final_identity["workloadIds"] = final_identity.get("workloadIds") or []
    final_identity["orderedRunKeys"] = final_identity.get("orderedRunKeys") or []
    derived_identity = evidence_identity(metrics_document or {}, metrics_hash)
    for field, value in derived_identity.items():
        if value is not None and (not isinstance(value, list) or value):
            final_identity[field] = value
    document = {
        "schemaVersion": 4, "evidenceContractVersion": 4, "manifestState": "FINAL",
        "terminalStatus": terminal_status, "authoritativeVerdict": authoritative,
        "identity": final_identity, "reasons": reasons,
        "stages": stages, "artifacts": artifacts,
        "exitStatus": final_exit_status,
    }
    validate_final_manifest_transport(document)
    return document


def parser() -> argparse.ArgumentParser:
    value = argparse.ArgumentParser()
    value.add_argument("--initialize-provisional", action="store_true")
    value.add_argument("--transition-manifest", action="store_true")
    value.add_argument("--finalize-lifecycle", action="store_true")
    value.add_argument("--finalize-infrastructure-failure", action="store_true")
    value.add_argument("--print-terminal-status", action="store_true")
    value.add_argument("--manifest", type=Path)
    value.add_argument("--stage")
    value.add_argument("--stage-status")
    value.add_argument("--reason-code")
    value.add_argument("--terminal-status")
    value.add_argument("--authoritative-verdict")
    value.add_argument("--identity-json", default="{}")
    value.add_argument("--artifact-path", action="append", default=[])
    value.add_argument("--record-exit-status", action="append", default=[])
    for name in ("metrics", "runtime-log", "preflight-manifest", "artifact-manifest", "performance-admission", "cleanup-admission", "cleanup-calibration", "allocation-diagnostic", "performance-validator", "tick-attribution", "tick-attribution-report", "tick-attribution-validator", "validator", "aggregator", "workload-contract", "runner", "player-artifact", "build-log", "build-root"):
        value.add_argument(f"--{name}", type=Path)
    value.add_argument("--performance-admission-status", type=int, default=1)
    value.add_argument("--cleanup-admission-status", type=int, default=1)
    value.add_argument("--cleanup-calibration-status", type=int, default=1)
    value.add_argument("--output", type=Path)
    return value


def main(argv: list[str] | None = None) -> int:
    arguments = parser().parse_args(argv)
    pre_replace_check = None
    final_evidence_attempt = False
    try:
        if arguments.print_terminal_status:
            if arguments.manifest is None:
                raise EvidenceError("FIELD_MISSING", "manifest", "path", None)
            if arguments.output is not None:
                raise EvidenceError("FIELD_UNEXPECTED", "output", None, str(arguments.output))
            document = load_json_object(arguments.manifest, "finalManifest")
            print(validate_final_manifest_transport(document))
            return 0
        if arguments.output is None:
            raise EvidenceError("FIELD_MISSING", "output", "path", None)
        repository_root = canonical_repository_root(Path(__file__))
        mutable_input: Path | None = None
        forbidden_roots: tuple[Path, ...] = (repository_root,)
        tree_inputs: tuple[Path, ...] = ()
        tree_input_hashes: tuple[tuple[Path, str], ...] = ()
        expected_input_snapshots = ()
        if arguments.initialize_provisional:
            identity = json.loads(arguments.identity_json)
            if not isinstance(identity, dict):
                raise EvidenceError("JSON_ROOT_INVALID", "identity", "object", type(identity).__name__)
            planned_artifacts: dict[str, Path] = {}
            for specification in arguments.artifact_path:
                if "=" not in specification:
                    raise EvidenceError("FIELD_TYPE_INVALID", "artifact-path", "name=path", specification)
                name, raw_path = specification.split("=", 1)
                if not name or name in planned_artifacts or not raw_path:
                    raise EvidenceError("FIELD_TYPE_INVALID", "artifact-path", "unique nonempty name=path", specification)
                planned_artifacts[name] = Path(raw_path)
            inputs = tuple(planned_artifacts.values())
            expected_input_snapshots = capture_input_snapshots(inputs)
            document = provisional_manifest(identity, planned_artifacts)
            mutable_input = planned_artifacts.get("attemptManifest")
        elif arguments.transition_manifest or arguments.finalize_lifecycle or arguments.finalize_infrastructure_failure:
            if arguments.manifest is None:
                raise EvidenceError("FIELD_MISSING", "manifest", "path", None)
            manifest_snapshot = capture_input_snapshots((arguments.manifest,))
            document = load_json_object(arguments.manifest, "manifest")
            artifact_inputs = tuple(
                Path(value["path"])
                for value in document.get("artifacts", {}).values()
                if isinstance(value, dict) and isinstance(value.get("path"), str)
            )
            inputs = (arguments.manifest, *artifact_inputs)
            expected_input_snapshots = (
                *manifest_snapshot,
                *capture_input_snapshots(artifact_inputs),
            )
            identity_update = json.loads(arguments.identity_json)
            if not isinstance(identity_update, dict):
                raise EvidenceError("JSON_ROOT_INVALID", "identity", "object", type(identity_update).__name__)
            if identity_update:
                if not isinstance(document.get("identity"), dict):
                    raise EvidenceError("JSON_ROOT_INVALID", "manifest.identity", "object", document.get("identity"))
                document["identity"].update(identity_update)
            for specification in arguments.record_exit_status:
                if "=" not in specification:
                    raise EvidenceError("FIELD_TYPE_INVALID", "record-exit-status", "name=integer", specification)
                name, raw_status = specification.split("=", 1)
                if name not in {
                    "tickAttributionAdmission", "performanceAdmission", "cleanupAdmission",
                    "cleanupCalibration",
                }:
                    raise EvidenceError("FIELD_TYPE_INVALID", "record-exit-status", "known exit field", name)
                try:
                    status = int(raw_status)
                except ValueError as error:
                    raise EvidenceError("FIELD_TYPE_INVALID", "record-exit-status", "integer", raw_status) from error
                if status not in {0, 1, 2}:
                    raise EvidenceError("EXIT_STATUS_MISMATCH", f"exitStatus.{name}", "0, 1, or 2", status)
                document.setdefault("exitStatus", {})[name] = status
            if arguments.transition_manifest:
                stage_reason = reason(arguments.reason_code, f"stages.{arguments.stage}") if arguments.reason_code else None
                document = transition_manifest(document, arguments.stage, arguments.stage_status, stage_reason)
            elif arguments.finalize_lifecycle:
                document = finalize_lifecycle(document, arguments.terminal_status, arguments.authoritative_verdict)
            else:
                document = finalize_infrastructure_failure(document)
            mutable_input = arguments.manifest
        else:
            final_evidence_attempt = True
            required = ("manifest", "metrics", "runtime_log", "preflight_manifest", "artifact_manifest", "cleanup_admission", "cleanup_calibration", "validator", "aggregator", "workload_contract", "performance_validator", "tick_attribution", "tick_attribution_report", "tick_attribution_validator", "runner", "player_artifact", "build_log", "build_root")
            missing = [name for name in required if getattr(arguments, name) is None]
            if missing:
                raise EvidenceError("FIELD_MISSING", "arguments", required, missing)
            inputs = tuple(path for path in (
                arguments.manifest,
                arguments.metrics, arguments.runtime_log, arguments.preflight_manifest, arguments.artifact_manifest,
                arguments.performance_admission, arguments.cleanup_admission, arguments.cleanup_calibration,
                arguments.allocation_diagnostic,
                arguments.performance_validator, arguments.tick_attribution,
                arguments.tick_attribution_report, arguments.tick_attribution_validator,
                arguments.validator, arguments.aggregator, arguments.workload_contract,
                arguments.runner, Path(__file__).resolve(),
                arguments.player_artifact, arguments.build_log,
            ) if path is not None)
            expected_input_snapshots = capture_input_snapshots(inputs)
            lifecycle_document = load_json_object(arguments.manifest, "lifecycleManifest")
            initial_build_root_hash = build_payload_sha256(arguments.build_root)
            document = build_manifest(
                metrics=arguments.metrics, runtime_log=arguments.runtime_log,
                preflight_manifest=arguments.preflight_manifest, artifact_manifest=arguments.artifact_manifest,
                performance_admission=arguments.performance_admission, cleanup_admission=arguments.cleanup_admission,
                cleanup_calibration=arguments.cleanup_calibration, performance_validator=arguments.performance_validator,
                tick_attribution=arguments.tick_attribution,
                tick_attribution_report=arguments.tick_attribution_report,
                tick_attribution_validator=arguments.tick_attribution_validator,
                allocation_diagnostic=arguments.allocation_diagnostic,
                validator=arguments.validator, aggregator=arguments.aggregator,
                workload_contract=arguments.workload_contract,
                performance_admission_status=arguments.performance_admission_status,
                cleanup_admission_status=arguments.cleanup_admission_status,
                cleanup_calibration_status=arguments.cleanup_calibration_status,
                runner=arguments.runner, player_artifact=arguments.player_artifact,
                build_log=arguments.build_log,
                build_root=arguments.build_root,
                lifecycle_manifest=lifecycle_document,
            )
            lifecycle_identity = lifecycle_document.get("identity")
            if not isinstance(lifecycle_identity, dict):
                raise EvidenceError(
                    "JSON_ROOT_INVALID",
                    "lifecycleManifest.identity",
                    "object",
                    lifecycle_identity,
                )
            pre_replace_check = _live_identity_pre_replace_check(lifecycle_identity)
            mutable_input = arguments.manifest
            forbidden_roots = (repository_root, arguments.build_root)
            tree_inputs = (arguments.build_root,)
            tree_input_hashes = ((arguments.build_root, initial_build_root_hash),)
        atomic_json(
            arguments.output,
            document,
            inputs=inputs,
            mutable_input=mutable_input,
            forbidden_roots=forbidden_roots,
            tree_inputs=tree_inputs,
            tree_input_hashes=tree_input_hashes,
            expected_input_snapshots=expected_input_snapshots,
            pre_replace_check=pre_replace_check,
        )
    except (EvidenceError, OSError, ValueError) as error:
        fallback_error_code = (
            error.reason.get("code")
            if isinstance(error, EvidenceError)
            else None
        )
        fallback_safe_error = (
            not isinstance(error, EvidenceError)
            or fallback_error_code in {
                "PRE_POST_HEAD_MISMATCH",
                "PRE_POST_WORKTREE_MISMATCH",
                "RUNTIME_TREE_MISMATCH",
                "INPUT_MUTATED_DURING_VALIDATION",
                "TREE_INPUT_MUTATED_DURING_VALIDATION",
                "DIRECTORY_INPUT_MUTATED_DURING_VALIDATION",
            }
        )
        if (
            final_evidence_attempt
            and fallback_safe_error
            and arguments.manifest is not None
            and arguments.output is not None
        ):
            try:
                fallback_manifest_snapshot = capture_input_snapshots(
                    (arguments.manifest,)
                )
                fallback_document = load_json_object(
                    arguments.manifest, "lifecycleManifest"
                )
                fallback_artifact_inputs = tuple(
                    Path(value["path"])
                    for value in fallback_document.get("artifacts", {}).values()
                    if isinstance(value, dict)
                    and isinstance(value.get("path"), str)
                )
                fallback_inputs = (
                    arguments.manifest,
                    *fallback_artifact_inputs,
                )
                fallback_snapshots = (
                    *fallback_manifest_snapshot,
                    *capture_input_snapshots(fallback_artifact_inputs),
                )
                if isinstance(error, EvidenceError):
                    fallback_stages = fallback_document.get("stages")
                    if not isinstance(fallback_stages, dict):
                        raise EvidenceError(
                            "FIELD_TYPE_INVALID",
                            "stages",
                            "object",
                            fallback_stages,
                        )
                    fallback_pending = [
                        name
                        for name in STAGE_NAMES
                        if fallback_stages.get(name, {}).get("status") == "NOT_RUN"
                    ]
                    if not fallback_pending:
                        raise EvidenceError(
                            "STAGE_NOT_RUN",
                            "stages",
                            "at least one NOT_RUN stage",
                            fallback_stages,
                        )
                    fallback_document = transition_manifest(
                        fallback_document,
                        fallback_pending[0],
                        "HOLD",
                        error.reason,
                    )
                    fallback_document = finalize_lifecycle(
                        fallback_document,
                        "HOLD",
                        "HOLD_INVALID_EVIDENCE",
                    )
                else:
                    fallback_document = finalize_infrastructure_failure(
                        fallback_document
                    )
                fallback_forbidden_roots = (repository_root,)
                if arguments.build_root is not None:
                    fallback_forbidden_roots = (
                        repository_root,
                        arguments.build_root,
                    )
                atomic_json(
                    arguments.output,
                    fallback_document,
                    inputs=fallback_inputs,
                    mutable_input=arguments.manifest,
                    forbidden_roots=fallback_forbidden_roots,
                    expected_input_snapshots=fallback_snapshots,
                )
            except (EvidenceError, OSError, ValueError) as fallback_error:
                print(json.dumps({
                    "error": str(error),
                    "fallbackError": str(fallback_error),
                }, sort_keys=True))
                return 1
            print(json.dumps(fallback_document, sort_keys=True, allow_nan=False))
            return 0
        print(json.dumps({"error": str(error)}, sort_keys=True))
        return 1
    print(json.dumps(document, sort_keys=True, allow_nan=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
