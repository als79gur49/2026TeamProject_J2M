#!/usr/bin/env python3
"""Freeze S3-A attribution and future campaign thresholds from admitted A calibration."""

from __future__ import annotations

import argparse
import hashlib
import json
import statistics
from pathlib import Path
from typing import Any

try:
    from Tools.gameplay_cleanup_slice3_admission import ADMITTED, _load_workload_contract, validate_cleanup_slice3
except ModuleNotFoundError:
    from gameplay_cleanup_slice3_admission import ADMITTED, _load_workload_contract, validate_cleanup_slice3

try:
    from Tools.gameplay_evidence_v4 import EvidenceError, atomic_json, capture_input_snapshots, evidence_identity, load_json_object, validate_v4_context_pair
except ModuleNotFoundError:
    from gameplay_evidence_v4 import EvidenceError, atomic_json, capture_input_snapshots, evidence_identity, load_json_object, validate_v4_context_pair


def derive_calibration(
    metrics: dict[str, Any], workload_contract: dict[str, Any] | None = None
) -> dict[str, Any]:
    is_v4 = (
        type(metrics.get("schemaVersion")) is int
        and metrics.get("schemaVersion") == 2
        and type(metrics.get("evidenceContractVersion")) is int
        and metrics.get("evidenceContractVersion") == 4
    )
    calibration = metrics.get("cleanupSlice3Calibration")
    if not isinstance(calibration, dict):
        raise ValueError("metrics cleanupSlice3Calibration must be an object")
    verdict, reasons = validate_cleanup_slice3(
        calibration,
        ("A",),
        workload_contract=workload_contract,
        require_v4=is_v4,
    )
    if verdict != ADMITTED:
        raise ValueError("S3-A calibration was not admitted: " + "; ".join(reasons))
    if calibration.get("repetitions") != 3:
        raise ValueError("S3-A calibration requires exactly three repetitions")

    workloads = (
        calibration["captures"][0]["workloads"]
        if is_v4
        else calibration["workloads"]
    )
    target = _workload(workloads, "cleanup-s3-target-wall-empty-v2")
    stress = _workload(workloads, "cleanup-s3-stress-dense-v2")
    target_runs = target["runs"]
    stress_runs = stress["runs"]
    if len(target_runs) != 3 or len(stress_runs) != 3:
        raise ValueError("each workload requires exactly three admitted repetitions")

    noise_series = []
    for metric_name in (
        "wholeTickMilliseconds",
        "cleanupProcessorMilliseconds",
        "runCleanupPhaseMilliseconds",
    ):
        p95_values = [float(run[metric_name]["p95"]) for run in target_runs]
        median_p95 = statistics.median(p95_values)
        if median_p95 <= 0:
            raise ValueError(f"target {metric_name} median p95 must be positive")
        noise_series.append((max(p95_values) - min(p95_values)) / median_p95 * 100.0)
    observed_noise_percent = round(max(noise_series), 6)
    noise_floor_percent = max(5.0, observed_noise_percent)

    target_whole_p95 = _median_p95(target_runs, "wholeTickMilliseconds")
    target_cleanup_p95 = _median_p95(target_runs, "cleanupProcessorMilliseconds")
    target_phase_p95 = _median_p95(target_runs, "runCleanupPhaseMilliseconds")
    component_share_percent = target_cleanup_p95 / target_whole_p95 * 100.0
    complete_phase_share_percent = target_phase_p95 / target_whole_p95 * 100.0
    allocation = calibration["frameAllocationCalibration"]
    allocation_phases = allocation["phases"]
    target_allocation_p95 = _allocation_p95(
        allocation_phases, "cleanup-s3-target-wall-empty-v2", True
    )
    stress_allocation_p95 = _allocation_p95(
        allocation_phases, "cleanup-s3-stress-dense-v2", True
    )
    target_capture_off_p95 = _median_p95(target_runs, "captureOffWholeTickMilliseconds")
    stress_capture_off_p95 = _median_p95(stress_runs, "captureOffWholeTickMilliseconds")
    target_capture_off_allocation_p95 = _allocation_p95(
        allocation_phases, "cleanup-s3-target-wall-empty-v2", False
    )
    stress_capture_off_allocation_p95 = _allocation_p95(
        allocation_phases, "cleanup-s3-stress-dense-v2", False
    )
    capture_off_ceiling_percent = max(5.0, noise_floor_percent)
    target_capture_off_regression_percent = (
        (target_capture_off_p95 - target_whole_p95) / target_whole_p95 * 100.0
    )
    stress_whole_p95 = _median_p95(stress_runs, "wholeTickMilliseconds")
    stress_capture_off_regression_percent = (
        (stress_capture_off_p95 - stress_whole_p95) / stress_whole_p95 * 100.0
    )
    capture_off_non_interfering = (
        target_capture_off_regression_percent <= capture_off_ceiling_percent
        and stress_capture_off_regression_percent <= capture_off_ceiling_percent
        and target_capture_off_allocation_p95 <= target_allocation_p95
        and stress_capture_off_allocation_p95 <= stress_allocation_p95
    )
    attribution_material = target_cleanup_p95 >= 0.01 and component_share_percent >= 10.0
    signal_valid = observed_noise_percent <= 10.0 and capture_off_non_interfering
    status = (
        "HOLD_INVALID_SIGNAL"
        if not signal_valid
        else "READY"
        if attribution_material
        else "DEFERRED_NOT_MATERIAL"
    )
    thresholds = (
        {
            "cleanupProcessorMaterialImprovementPercent": max(10.0, noise_floor_percent * 2.0),
            "runCleanupPhaseContainmentRequiredImprovementPercent": noise_floor_percent,
            "runCleanupPhaseContainmentMethod": (
                "median-of-three raw p95 A-to-C improvement must be at least the frozen noise floor"
            ),
            "targetWholeTickBenefitPercent": max(3.0, noise_floor_percent),
            "bWholeTickMaintenanceTaxCeilingPercent": max(5.0, noise_floor_percent),
            "bAllocationTaxCeilingBytesPerTick": max(256, round(target_allocation_p95 * 0.05)),
            "targetAllocationRegressionCeilingBytesPerTick": 0,
            "targetAllocationRegressionCeilingPercent": 0.0,
            "stressWholeTickRegressionCeilingPercent": 5.0,
            "stressAllocationRegressionCeilingBytesPerTick": max(
                256, round(stress_allocation_p95 * 0.05)
            ),
            "captureOffWholeTickRegressionCeilingPercent": capture_off_ceiling_percent,
            "attributionMinimumCleanupProcessorMilliseconds": 0.01,
            "attributionMinimumWholeTickSharePercent": 10.0,
            "maximumCalibrationNoisePercent": 10.0,
        }
        if signal_valid
        else None
    )
    validator_path = Path(__file__).with_name("gameplay_cleanup_slice3_admission.py")
    aggregator_path = Path(__file__).resolve()

    campaign_rules = {
        "artifactPolicy": "build-once/run-many",
        "internalWarmupTicks": calibration["warmupTicksPerRepetition"],
        "sampleTicks": calibration["sampleTicksPerRepetition"],
        "rawP95RepetitionsPerWorkloadStrategy": 3,
        "discardedAdmittedFullRunWarmups": 6,
        "admittedOfficialRuns": 18,
        "maximumAdmissionRetriesPerSlot": 2,
        "retryPolicy": "retry only rejected admission in the same slot; preserve every attempt",
        "aggregation": "median of three raw JSON p95 values; round only after verdict",
        "admissionBeforePerformanceVerdict": True,
        "cohortRestartRule": (
            "any runtime/workload/validator/aggregator/plan hash change or uncertain valid cohort "
            "requires a new campaign ID and complete restart"
        ),
    }
    report = {
        "schemaVersion": 2 if is_v4 else 1,
        "stage": "S3-A",
        "status": status,
        "admitted": True,
        "officialEvidence": False,
        "attributionMaterial": attribution_material,
        "captureOffNonInterfering": capture_off_non_interfering,
        "signalValid": signal_valid,
        "observedNoisePercent": observed_noise_percent,
        "noiseFormula": (
            "max over target A timing metrics of "
            "((maximum repetition raw p95 - minimum repetition raw p95) / median repetition raw p95) * 100; "
            "minimum operational noise floor 5%"
        ),
        "observations": {
            "targetCleanupProcessorMedianP95Milliseconds": target_cleanup_p95,
            "targetRunCleanupPhaseMedianP95Milliseconds": target_phase_p95,
            "targetWholeTickMedianP95Milliseconds": target_whole_p95,
            "targetCleanupProcessorWholeTickSharePercent": component_share_percent,
            "targetRunCleanupPhaseWholeTickSharePercent": complete_phase_share_percent,
            "targetGcAllocatedBytesPerTickP95": target_allocation_p95,
            "stressGcAllocatedBytesPerTickP95": stress_allocation_p95,
            "targetCaptureOffWholeTickMedianP95Milliseconds": target_capture_off_p95,
            "stressCaptureOffWholeTickMedianP95Milliseconds": stress_capture_off_p95,
            "targetCaptureOffWholeTickRegressionPercent": target_capture_off_regression_percent,
            "stressCaptureOffWholeTickRegressionPercent": stress_capture_off_regression_percent,
            "targetCaptureOffGcAllocatedBytesPerTickP95": target_capture_off_allocation_p95,
            "stressCaptureOffGcAllocatedBytesPerTickP95": stress_capture_off_allocation_p95,
        },
        "thresholds": thresholds,
        "campaignRules": campaign_rules if signal_valid else None,
        "immutableHashProcedure": (
            "SHA-256 the exact UTF-8 bytes of the canonical campaign-plan JSON, admission validator, "
            "and aggregator; record all three hashes in the plan before the first official run and "
            "require exact equality for every attempt"
        ),
        "toolHashes": {
            "cleanupValidatorSha256": _sha256(validator_path),
            "aggregatorSha256": _sha256(aggregator_path),
        },
    }
    if is_v4:
        report.update(
            {
                "evidenceContractVersion": 4,
                "reasons": []
                if signal_valid
                else [
                    {
                        "code": "SIGNAL_INVALID",
                        "path": "cleanupCalibration.signalValid",
                        "expected": True,
                        "observed": False,
                    }
                ],
                "identity": evidence_identity(metrics, None),
                "inputHashes": {},
            }
        )
    return report


def _workload(workloads: list[dict[str, Any]], workload_id: str) -> dict[str, Any]:
    matching = [value for value in workloads if value.get("workloadId") == workload_id]
    if len(matching) != 1:
        raise ValueError(f"expected exactly one {workload_id} workload")
    return matching[0]


def _median_p95(runs: list[dict[str, Any]], metric_name: str) -> float:
    return float(statistics.median(float(run[metric_name]["p95"]) for run in runs))


def _allocation_p95(
    phases: list[dict[str, Any]], workload_id: str, capture_diagnostics: bool
) -> float:
    matching = [
        phase
        for phase in phases
        if phase.get("workloadId") == workload_id
        and phase.get("captureDiagnostics") is capture_diagnostics
    ]
    if len(matching) != 1:
        raise ValueError(
            f"expected one allocation phase workload={workload_id} capture={capture_diagnostics}"
        )
    return float(matching[0]["gcAllocatedBytesPerTick"]["p95"])


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


def _provenance(metrics_path: Path) -> dict[str, Any]:
    return {
        "metricsSha256": _sha256(metrics_path),
        "activeStrategies": ["A"],
        "cleanupValidatorSha256": _sha256(
            Path(__file__).resolve().with_name("gameplay_cleanup_slice3_admission.py")
        ),
        "aggregatorSha256": _sha256(Path(__file__).resolve()),
        "workloadContractSha256": _sha256(
            Path(__file__).resolve().parent
            / "contracts"
            / "gameplay_cleanup_slice3_workloads_v2.json"
        ),
    }


def build_calibration_report(
    metrics: dict[str, Any],
    *,
    metrics_sha256: str,
    validator_path: Path,
    aggregator_path: Path,
    workload_contract_path: Path,
) -> dict[str, Any]:
    provenance = {
        "metricsSha256": metrics_sha256,
        "activeStrategies": ["A"],
        "cleanupValidatorSha256": _sha256(validator_path),
        "aggregatorSha256": _sha256(aggregator_path),
        "workloadContractSha256": _sha256(workload_contract_path),
    }
    try:
        if (
            type(metrics.get("schemaVersion")) is not int
            or metrics.get("schemaVersion") != 2
            or type(metrics.get("evidenceContractVersion")) is not int
            or metrics.get("evidenceContractVersion") != 4
        ):
            raise ValueError("UNSUPPORTED_ARTIFACT_VERSION: calibration requires metrics schema 2 / contract 4")
        report = derive_calibration(metrics, _load_workload_contract(workload_contract_path))
    except (KeyError, TypeError, ValueError) as error:
        report = {
            "schemaVersion": 2, "evidenceContractVersion": 4, "stage": "S3-A",
            "status": "HOLD_INVALID_EVIDENCE", "admitted": False, "officialEvidence": False,
            "attributionMaterial": None, "captureOffNonInterfering": None, "signalValid": None,
            "observedNoisePercent": None, "noiseFormula": None, "observations": None,
            "thresholds": None, "campaignRules": None, "immutableHashProcedure": None,
            "toolHashes": {},
            "reasons": [{
                "code": "UNSUPPORTED_ARTIFACT_VERSION" if str(error).startswith("UNSUPPORTED_ARTIFACT_VERSION:") else "SEMANTIC_INVARIANT_INVALID",
                "path": "cleanupSlice3Calibration", "expected": None, "observed": str(error),
            }],
            "identity": evidence_identity(metrics, metrics_sha256), "inputHashes": {"metricsSha256": metrics_sha256},
        }
    report["provenance"] = provenance
    report["inputHashes"] = {"metricsSha256": metrics_sha256}
    report["identity"] = evidence_identity(metrics, metrics_sha256)
    return report


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("metrics", type=Path)
    parser.add_argument("--preflight-manifest", type=Path)
    parser.add_argument("--artifact-manifest", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    arguments = parser.parse_args()
    context_inputs = tuple(
        path
        for path in (arguments.preflight_manifest, arguments.artifact_manifest)
        if path is not None
    )
    input_paths = (
        arguments.metrics,
        Path(__file__).resolve(),
        Path(__file__).resolve().with_name("gameplay_cleanup_slice3_admission.py"),
        (
            Path(__file__).resolve().parent
            / "contracts"
            / "gameplay_cleanup_slice3_workloads_v2.json"
        ),
        *context_inputs,
    )
    input_snapshots = capture_input_snapshots(input_paths)
    try:
        provenance = _provenance(arguments.metrics)
        metrics = load_json_object(arguments.metrics, "metrics")
    except EvidenceError as error:
        report = {
            "schemaVersion": 2,
            "evidenceContractVersion": 4,
            "stage": "S3-A",
            "status": "HOLD_INVALID_EVIDENCE",
            "admitted": False,
            "officialEvidence": False,
            "signalValid": None,
            "attributionMaterial": None,
            "captureOffNonInterfering": None,
            "observedNoisePercent": None,
            "noiseFormula": None,
            "observations": None,
            "thresholds": None,
            "campaignRules": None,
            "immutableHashProcedure": None,
            "toolHashes": {},
            "reasons": [error.reason],
            "identity": {},
            "provenance": {
                "metricsSha256": _try_sha256(arguments.metrics),
                "activeStrategies": ["A"],
                "cleanupValidatorSha256": _try_sha256(
                    Path(__file__).resolve().with_name("gameplay_cleanup_slice3_admission.py")
                ),
                "aggregatorSha256": _try_sha256(Path(__file__).resolve()),
                "workloadContractSha256": _try_sha256(
                    Path(__file__).resolve().parent
                    / "contracts"
                    / "gameplay_cleanup_slice3_workloads_v2.json"
                ),
            },
            "inputHashes": {"metricsSha256": _try_sha256(arguments.metrics)},
        }
    except (OSError, UnicodeError, ValueError) as error:
        report = {
            "schemaVersion": 2,
            "evidenceContractVersion": 4,
            "stage": "S3-A",
            "status": "HOLD_INVALID_EVIDENCE",
            "admitted": False,
            "officialEvidence": False,
            "attributionMaterial": None,
            "captureOffNonInterfering": None,
            "signalValid": None,
            "observedNoisePercent": None,
            "noiseFormula": None,
            "observations": None,
            "thresholds": None,
            "campaignRules": None,
            "immutableHashProcedure": None,
            "toolHashes": {},
            "reasons": [{"code": "JSON_PARSE_FAILED", "path": "metrics", "expected": "readable strict JSON", "observed": str(error)}],
            "identity": {},
            "provenance": {
                "metricsSha256": _try_sha256(arguments.metrics),
                "activeStrategies": ["A"],
                "cleanupValidatorSha256": _try_sha256(
                    Path(__file__).resolve().with_name("gameplay_cleanup_slice3_admission.py")
                ),
                "aggregatorSha256": _try_sha256(Path(__file__).resolve()),
                "workloadContractSha256": _try_sha256(
                    Path(__file__).resolve().parent
                    / "contracts"
                    / "gameplay_cleanup_slice3_workloads_v2.json"
                ),
            },
            "inputHashes": {"metricsSha256": _try_sha256(arguments.metrics)},
        }
    else:
        report = build_calibration_report(
            metrics,
            metrics_sha256=provenance["metricsSha256"],
            validator_path=Path(__file__).resolve().with_name("gameplay_cleanup_slice3_admission.py"),
            aggregator_path=Path(__file__).resolve(),
            workload_contract_path=Path(__file__).resolve().parent / "contracts" / "gameplay_cleanup_slice3_workloads_v2.json",
        )
    context_reasons = validate_v4_context_pair(
        arguments.preflight_manifest,
        arguments.artifact_manifest,
        metrics.get("captureIdentity") if "metrics" in locals() else None,
        metrics_sha256=provenance.get("metricsSha256") if "provenance" in locals() else None,
    )
    if context_reasons:
        report["status"] = "HOLD_INVALID_EVIDENCE"
        report["admitted"] = False
        report["officialEvidence"] = False
        report["attributionMaterial"] = None
        report["captureOffNonInterfering"] = None
        report["signalValid"] = None
        report["observations"] = None
        report["thresholds"] = None
        report["campaignRules"] = None
        report["reasons"] = list(report.get("reasons", [])) + context_reasons
    try:
        atomic_json(
            arguments.output,
            report,
            inputs=input_paths,
            expected_input_snapshots=input_snapshots,
        )
    except (EvidenceError, OSError) as error:
        print(json.dumps({"error": str(error)}, sort_keys=True))
        return 2
    print(json.dumps(report, sort_keys=True, allow_nan=False))
    return 0 if report["status"] in ("READY", "DEFERRED_NOT_MATERIAL") else 1


if __name__ == "__main__":
    raise SystemExit(main())
