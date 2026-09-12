from __future__ import annotations

import copy
import hashlib
import json
import math
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from Tools import gameplay_cleanup_slice3_admission as admission_tool
from Tools.gameplay_cleanup_slice3_admission import ADMITTED, REJECTED, build_admission_report, validate_cleanup_slice3


CONTRACT = json.loads(
    (
        Path(__file__).resolve().parents[1]
        / "contracts"
        / "gameplay_cleanup_slice3_workloads_v2.json"
    ).read_text(encoding="utf-8")
)["workloads"]


def metric(count: int, p95: float = 1.0) -> dict[str, float | int]:
    return {"count": count, "median": p95, "p95": p95, "p99": p95, "maximum": p95}


def run(strategy: str, stress: bool, repetition: int = 1) -> dict[str, object]:
    ticks = 100
    removal = 16 * ticks if stress else 0
    timer = 32 * ticks if stress else 0
    immediate = 32 * ticks if stress else 0
    maintenance = 2400 if strategy in ("B", "C") else 0
    carrier = 800 if strategy in ("B", "C") else 0
    return {
        "repetition": repetition,
        "executedTicks": ticks,
        "mutationCount": 96 * ticks if stress else 0,
        "expectedMutationCount": 96 * ticks if stress else 0,
        "removalCandidateCount": removal,
        "expectedRemovalCandidateCount": removal,
        "timerCandidateCount": timer,
        "expectedTimerCandidateCount": timer,
        "immediateTransitionCandidateCount": immediate,
        "expectedImmediateTransitionCandidateCount": immediate,
        "fullScanInvocationCount": ticks if strategy in ("A", "B") else 0,
        "fullScanEntityVisitCount": 256 * ticks if strategy in ("A", "B") else 0,
        "survivorCopyCount": (256 * ticks - removal) if strategy in ("A", "B") else 0,
        "removalProcessedCount": removal,
        "timerProcessedCount": timer,
        "transitionProcessedCount": immediate,
        "zeroCandidateOpportunityCount": ticks if not stress else 0,
        "referenceOracleInvocationCount": ticks,
        "indexedInvocationCount": ticks if strategy == "C" else 0,
        "hiddenFallbackCount": 0,
        "invariantMismatchCount": 0,
        "candidateMembershipCheckCount": maintenance,
        "candidateMembershipAddCount": 0,
        "candidateMembershipRemoveCount": 0,
        "snapshotCandidateArrayCount": carrier,
        "snapshotCandidateCarriedItemCount": carrier,
        "fastImportCandidateItemCount": 0,
        "fastImportSeparatePredicateRebuildEntityVisitCount": 0,
        "validCleanupProcessorSamples": ticks,
        "validRunCleanupPhaseSamples": ticks,
        "wholeTickMilliseconds": metric(ticks, 2.0),
        "cleanupProcessorMilliseconds": metric(ticks, 0.8),
        "runCleanupPhaseMilliseconds": metric(ticks, 1.0),
        "captureOffWholeTickMilliseconds": metric(ticks, 1.5),
    }


def workload(strategy: str, stress: bool) -> dict[str, object]:
    workload_id = "cleanup-s3-stress-dense-v2" if stress else "cleanup-s3-target-wall-empty-v2"
    identity = CONTRACT[workload_id]
    return {
        "workloadId": workload_id,
        "seed": identity["seed"],
        "scheduleHash": identity["scheduleHash"],
        "initialWorldFingerprint": identity["initialWorldFingerprint"],
        "entityCount": 256,
        "wallCount": 256,
        "expectedPerTick": {
            "mutations": 96 if stress else 0,
            "removalCandidates": 16 if stress else 0,
            "timerCandidates": 32 if stress else 0,
            "immediateTransitionCandidates": 32 if stress else 0,
        },
        "oracleParityVerified": True,
        "runs": [run(strategy, stress)],
    }


def capture(strategy: str) -> dict[str, object]:
    return {"strategy": strategy, "workloads": [workload(strategy, False), workload(strategy, True)]}


def document(*strategies: str) -> dict[str, object]:
    captures = [capture(strategy) for strategy in strategies]
    value = {
        "schemaVersion": 1,
        "repetitions": 1,
        "stage": "S3-A" if strategies == ("A",) else "S3-C",
        "diagnosticsOffNoOpAllocatedBytes": 0,
        "captures": captures,
    }
    phases = []
    for strategy in strategies:
        for stress in (False, True):
            identity = workload(strategy, stress)
            for capture_diagnostics, p95 in ((True, 100.0), (False, 80.0)):
                phases.append(
                    {
                        "strategy": strategy,
                        "workloadId": identity["workloadId"],
                        "scheduleHash": identity["scheduleHash"],
                        "initialWorldFingerprint": identity["initialWorldFingerprint"],
                        "captureDiagnostics": capture_diagnostics,
                        "validSamples": 100,
                        "gcAllocatedBytesPerTick": metric(100, p95),
                    }
                )
    value["frameAllocationCalibration"] = {
        "signal": "GC.GetAllocatedBytesForCurrentThread delta around exactly one synthetic tick",
        "allocationCounterProbeBytes": 4120,
        "warmupFramesPerPhase": 30,
        "sampleFramesPerPhase": 100,
        "phases": phases,
    }
    return value


def v4_document() -> dict[str, object]:
    value = document("A")
    value["schemaVersion"] = 2
    value["evidenceContractVersion"] = 4
    value["activeStrategies"] = ["A"]
    value["warmupTicksPerRepetition"] = 30
    value["sampleTicksPerRepetition"] = 100
    value["allocationSignal"] = "per-tick current-thread allocated-byte delta appended by Player probe"
    for workload_value in value["captures"][0]["workloads"]:
        run_value = workload_value["runs"][0]
        run_value["runKey"] = f"A/{workload_value['workloadId']}/1"
    return value


class CleanupSlice3AdmissionTests(unittest.TestCase):
    def test_standalone_missing_v4_context_fails_closed(self) -> None:
        from Tools.tests.test_gameplay_performance_admission import v4_metrics

        repository_root = Path(__file__).resolve().parents[2]
        script = repository_root / "Tools" / "gameplay_cleanup_slice3_admission.py"
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            metrics_path = root / "metrics.json"
            output_path = root / "cleanup-admission.json"
            metrics = v4_metrics()
            metrics["cleanupSlice3Calibration"] = v4_document()
            metrics_path.write_text(json.dumps(metrics), encoding="utf-8")

            completed = subprocess.run(
                [sys.executable, str(script), str(metrics_path), "--output", str(output_path)],
                cwd=repository_root,
                check=False,
                capture_output=True,
                text=True,
            )

            self.assertNotEqual(0, completed.returncode)
            report = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertTrue(any(
                value.get("code") == "IDENTITY_FIELD_MISSING"
                and value.get("path") == "evidenceContext.preflightManifest"
                for value in report["reasons"]
            ))

    def test_valid_v4_captures_only_fixture_is_admitted(self) -> None:
        verdict, reasons = validate_cleanup_slice3(v4_document(), ("A",), require_v4=True)
        self.assertEqual(ADMITTED, verdict, reasons)

    def test_v4_raw_membership_does_not_require_processed_count_equality(self) -> None:
        value = v4_document()
        stress_run = value["captures"][0]["workloads"][1]["runs"][0]
        stress_run["timerProcessedCount"] -= 1
        stress_run["transitionProcessedCount"] -= 2

        verdict, reasons = validate_cleanup_slice3(value, ("A",), require_v4=True)

        self.assertEqual(ADMITTED, verdict, reasons)

    def test_v4_allocation_signal_is_exact_and_typed(self) -> None:
        for observed in ({"wrong": "type"}, "different producer"):
            with self.subTest(observed=observed):
                value = v4_document()
                value["allocationSignal"] = observed

                verdict, reasons = validate_cleanup_slice3(value, ("A",), require_v4=True)

                self.assertEqual(REJECTED, verdict)
                self.assertTrue(any("allocationSignal" in item for item in reasons), reasons)

    def test_v4_workload_contract_must_match_approved_digest(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            changed_contract = root / "workloads.json"
            changed = copy.deepcopy(CONTRACT)
            changed["cleanup-s3-target-wall-empty-v2"]["seed"] = 731001
            changed_contract.write_text(
                json.dumps({"schemaVersion": 2, "workloads": changed}),
                encoding="utf-8",
            )
            metrics = {"schemaVersion": 2, "evidenceContractVersion": 4, "cleanupSlice3Calibration": v4_document()}

            report = build_admission_report(
                metrics,
                metrics_sha256="1" * 64,
                active_strategies=("A",),
                validator_path=Path(__file__).resolve().parents[1] / "gameplay_cleanup_slice3_admission.py",
                workload_contract_path=changed_contract,
            )

            self.assertEqual(REJECTED, report["verdict"])
            self.assertTrue(report["reasons"])

    def test_v4_dual_strategy_representation_and_unknown_fields_are_rejected(self) -> None:
        value = v4_document()
        value["strategy"] = "A"
        value["workloads"] = copy.deepcopy(value["captures"][0]["workloads"])
        value["captures"][0]["workloads"][0]["unexpected"] = True

        verdict, reasons = validate_cleanup_slice3(value, ("A",), require_v4=True)

        self.assertEqual(REJECTED, verdict)
        self.assertTrue(any(reason.startswith("SCHEMA_MISMATCH:") for reason in reasons), reasons)
        self.assertTrue(any(reason.startswith("FIELD_UNEXPECTED:") for reason in reasons), reasons)

    def test_v4_float_schema_and_tick_counts_are_rejected(self) -> None:
        value = v4_document()
        value["schemaVersion"] = 2.0
        value["sampleTicksPerRepetition"] = 100.0

        verdict, reasons = validate_cleanup_slice3(value, ("A",), require_v4=True)

        self.assertEqual(REJECTED, verdict)
        self.assertTrue(any(reason.startswith("SCHEMA_MISMATCH:") for reason in reasons), reasons)
        self.assertTrue(any(reason.startswith("NUMERIC_DOMAIN_INVALID:") for reason in reasons), reasons)

    def test_malformed_workload_contract_root_becomes_machine_readable_rejection(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            contract = root / "contract.json"
            validator = root / "validator.py"
            contract.write_text("[]\n", encoding="utf-8")
            validator.write_text("fixture\n", encoding="utf-8")
            metrics = {
                "schemaVersion": 2,
                "evidenceContractVersion": 4,
                "cleanupSlice3Calibration": v4_document(),
                "captureIdentity": {},
            }

            report = build_admission_report(
                metrics,
                metrics_sha256="0" * 64,
                active_strategies=("A",),
                validator_path=validator,
                workload_contract_path=contract,
            )

            self.assertEqual(REJECTED, report["verdict"])
            self.assertNotEqual([], report["reasons"])
            self.assertEqual("SEMANTIC_INVARIANT_INVALID", report["reasons"][0]["code"])
    def test_repetitions_is_required_and_cannot_default_to_one(self) -> None:
        value = document("A")
        value.pop("repetitions")
        self.assert_rejected_with(value, ("A",), "CARDINALITY_MISMATCH")

    def test_nonnegative_counter_domains_reject_negative_values(self) -> None:
        value = document("A")
        value["captures"][0]["workloads"][0]["runs"][0]["hiddenFallbackCount"] = -1
        self.assert_rejected_with(value, ("A",), "NUMERIC_DOMAIN_INVALID")

    def test_valid_a_only_and_future_abc_fixtures_are_admitted(self) -> None:
        for strategies in (("A",), ("A", "B", "C")):
            with self.subTest(strategies=strategies):
                verdict, reasons = validate_cleanup_slice3(document(*strategies), strategies)
                self.assertEqual(ADMITTED, verdict, reasons)

    def test_target_nonzero_candidate_is_rejected(self) -> None:
        value = document("A")
        value["captures"][0]["workloads"][0]["runs"][0]["removalCandidateCount"] = 1
        self.assert_rejected_with(value, ("A",), "TARGET_CANDIDATE_NONZERO")

    def test_stress_schedule_or_cross_strategy_identity_mismatch_is_rejected(self) -> None:
        value = document("A", "B", "C")
        value["captures"][1]["workloads"][1]["runs"][0]["mutationCount"] -= 1
        self.assert_rejected_with(value, ("A", "B", "C"), "SCHEDULE_COUNT_MISMATCH")

        value = document("A", "B", "C")
        value["captures"][2]["workloads"][0]["entityCount"] = 255
        self.assert_rejected_with(value, ("A", "B", "C"), "WORKLOAD_IDENTITY_MISMATCH")

    def test_maintenance_carriage_and_invocation_mismatch_are_rejected(self) -> None:
        value = document("A")
        value["captures"][0]["workloads"][0]["runs"][0]["candidateMembershipAddCount"] = 1
        self.assert_rejected_with(value, ("A",), "A_MAINTENANCE_NONZERO")

        value = document("A", "B", "C")
        value["captures"][2]["workloads"][0]["runs"][0]["snapshotCandidateCarriedItemCount"] += 1
        self.assert_rejected_with(value, ("A", "B", "C"), "BC_MAINTENANCE_CARRIAGE_MISMATCH")

        value = document("A")
        value["captures"][0]["workloads"][0]["runs"][0]["fullScanInvocationCount"] = 99
        self.assert_rejected_with(value, ("A",), "STRATEGY_INVOCATION_MISMATCH")

    def test_fingerprint_fallback_invariant_and_sample_mismatch_are_rejected(self) -> None:
        cases = (
            ("initialWorldFingerprint", "x" * 64, "WORKLOAD_IDENTITY_MISMATCH"),
            ("hiddenFallbackCount", 1, "HIDDEN_FALLBACK"),
            ("invariantMismatchCount", 1, "INVARIANT_MISMATCH"),
            ("frameAllocationSamples", 99, "SAMPLE_COUNT_MISMATCH"),
        )
        for key, replacement, code in cases:
            with self.subTest(key=key):
                value = document("A", "B", "C")
                if key == "initialWorldFingerprint":
                    value["captures"][1]["workloads"][0][key] = replacement
                elif key == "frameAllocationSamples":
                    value["frameAllocationCalibration"]["phases"][0]["validSamples"] = replacement
                else:
                    value["captures"][0]["workloads"][0]["runs"][0][key] = replacement
                self.assert_rejected_with(value, ("A", "B", "C"), code)

    def test_missing_requested_strategy_does_not_fallback_to_a(self) -> None:
        self.assert_rejected_with(document("A"), ("A", "B"), "ACTIVE_STRATEGY_MISMATCH")

    def test_synchronized_frozen_identity_drift_is_rejected(self) -> None:
        value = document("A")
        stress = value["captures"][0]["workloads"][1]
        stress["seed"] = 99999
        stress["scheduleHash"] = "0" * 64
        stress["initialWorldFingerprint"] = "1" * 64
        for phase in value["frameAllocationCalibration"]["phases"]:
            if phase["workloadId"] == stress["workloadId"]:
                phase["scheduleHash"] = stress["scheduleHash"]
                phase["initialWorldFingerprint"] = stress["initialWorldFingerprint"]

        self.assert_rejected_with(value, ("A",), "FROZEN_WORKLOAD_IDENTITY_MISMATCH")

    def test_future_abc_requires_strategy_specific_allocation_phases(self) -> None:
        value = document("A", "B", "C")
        value["frameAllocationCalibration"]["phases"] = [
            phase
            for phase in value["frameAllocationCalibration"]["phases"]
            if phase["strategy"] != "B"
        ]

        self.assert_rejected_with(value, ("A", "B", "C"), "ALLOCATION_STRATEGY_MATRIX_MISMATCH")

    def test_valid_zero_allocation_samples_are_admitted(self) -> None:
        value = document("A")
        for phase in value["frameAllocationCalibration"]["phases"]:
            phase["gcAllocatedBytesPerTick"] = metric(100, 0.0)

        verdict, reasons = validate_cleanup_slice3(value, ("A",))

        self.assertEqual(ADMITTED, verdict, reasons)

    def test_zero_allocation_counter_probe_is_rejected(self) -> None:
        value = document("A")
        value["frameAllocationCalibration"]["allocationCounterProbeBytes"] = 0

        self.assert_rejected_with(value, ("A",), "ALLOCATION_COUNTER_PROBE_INVALID")

    def test_non_monotonic_timing_and_allocation_summaries_are_rejected(self) -> None:
        cases = (
            ("timing-median", "wholeTickMilliseconds", {"median": 2.0, "p95": 1.0}),
            ("timing-p95", "cleanupProcessorMilliseconds", {"p95": 2.0, "p99": 1.0}),
            ("timing-p99", "runCleanupPhaseMilliseconds", {"p99": 2.0, "maximum": 1.0}),
            ("allocation", "gcAllocatedBytesPerTick", {"median": 2.0, "p95": 1.0}),
        )
        for name, metric_name, replacement in cases:
            with self.subTest(name=name):
                value = document("A")
                if name == "allocation":
                    summary = value["frameAllocationCalibration"]["phases"][0][metric_name]
                else:
                    summary = value["captures"][0]["workloads"][0]["runs"][0][metric_name]
                summary.update(replacement)
                self.assert_rejected_with(value, ("A",), "METRIC_SUMMARY_INVALID")

    def test_equal_zero_metric_summaries_remain_valid(self) -> None:
        value = document("A")
        for workload_value in value["captures"][0]["workloads"]:
            for run_value in workload_value["runs"]:
                for metric_name in (
                    "wholeTickMilliseconds",
                    "cleanupProcessorMilliseconds",
                    "runCleanupPhaseMilliseconds",
                    "captureOffWholeTickMilliseconds",
                ):
                    run_value[metric_name] = metric(run_value["executedTicks"], 0.0)
        for phase in value["frameAllocationCalibration"]["phases"]:
            phase["gcAllocatedBytesPerTick"] = metric(100, 0.0)

        verdict, reasons = validate_cleanup_slice3(value, ("A",))

        self.assertEqual(ADMITTED, verdict, reasons)

    def test_non_numeric_non_finite_and_negative_metric_values_are_rejected(self) -> None:
        for replacement in (True, math.nan, math.inf, -0.01):
            with self.subTest(replacement=replacement):
                value = document("A")
                value["captures"][0]["workloads"][0]["runs"][0][
                    "wholeTickMilliseconds"
                ]["p95"] = replacement
                self.assert_rejected_with(value, ("A",), "METRIC_SUMMARY_INVALID")

    def test_duplicate_workload_and_run_keys_are_rejected(self) -> None:
        value = document("A")
        value["captures"][0]["workloads"].append(
            copy.deepcopy(value["captures"][0]["workloads"][0])
        )
        self.assert_rejected_with(value, ("A",), "WORKLOAD_CARDINALITY_MISMATCH")

        value = document("A")
        runs = value["captures"][0]["workloads"][0]["runs"]
        runs.append(copy.deepcopy(runs[0]))
        self.assert_rejected_with(value, ("A",), "RUN_CARDINALITY_MISMATCH")

    def test_active_strategies_require_exact_same_run_keys(self) -> None:
        value = document("A", "B", "C")
        for workload_value in value["captures"][0]["workloads"]:
            extra_run = copy.deepcopy(workload_value["runs"][0])
            extra_run["repetition"] = 2
            workload_value["runs"].append(extra_run)

        self.assert_rejected_with(value, ("A", "B", "C"), "RUN_CARDINALITY_MISMATCH")

    def test_integer_cardinality_fields_reject_numerically_equal_floats(self) -> None:
        cases = (
            ("metric-count", lambda value: value["captures"][0]["workloads"][0]["runs"][0]["wholeTickMilliseconds"].__setitem__("count", 100.0), "METRIC_SUMMARY_INVALID"),
            ("valid-samples", lambda value: value["frameAllocationCalibration"]["phases"][0].__setitem__("validSamples", 100.0), "SAMPLE_COUNT_MISMATCH"),
            ("repetitions", lambda value: value.__setitem__("repetitions", 1.0), "SCHEMA_MISMATCH"),
            ("executed-ticks", lambda value: value["captures"][0]["workloads"][0]["runs"][0].__setitem__("executedTicks", 100.0), "SAMPLE_COUNT_MISMATCH"),
        )
        for name, mutate, code in cases:
            with self.subTest(name=name):
                value = document("A")
                mutate(value)
                self.assert_rejected_with(value, ("A",), code)

    def test_non_object_document_root_is_rejected(self) -> None:
        verdict, reasons = validate_cleanup_slice3([], ("A",))

        self.assertEqual(REJECTED, verdict)
        self.assertEqual(["SCHEMA_MISMATCH: root must be an object"], reasons)

    def test_non_object_metrics_cli_preserves_rejected_artifact(self) -> None:
        repository_root = Path(__file__).resolve().parents[2]
        script = repository_root / "Tools" / "gameplay_cleanup_slice3_admission.py"
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            metrics_path = root / "array.json"
            output_path = root / "admission.json"
            metrics_path.write_text("[]\n", encoding="utf-8")

            completed = subprocess.run(
                [sys.executable, str(script), str(metrics_path), "--output", str(output_path)],
                cwd=repository_root,
                check=False,
                capture_output=True,
                text=True,
            )

            self.assertEqual(1, completed.returncode)
            result = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertEqual(REJECTED, result["verdict"])
            self.assertEqual("JSON_ROOT_INVALID", result["reasons"][0]["code"])

    def test_malformed_json_cli_preserves_rejected_artifact_with_provenance(self) -> None:
        repository_root = Path(__file__).resolve().parents[2]
        script = repository_root / "Tools" / "gameplay_cleanup_slice3_admission.py"
        contract = repository_root / "Tools" / "contracts" / "gameplay_cleanup_slice3_workloads_v2.json"
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            metrics_path = root / "malformed.json"
            output_path = root / "admission.json"
            metrics_path.write_text("{ malformed", encoding="utf-8")

            completed = subprocess.run(
                [
                    sys.executable,
                    str(script),
                    str(metrics_path),
                    "--active-strategies",
                    "A",
                    "--output",
                    str(output_path),
                ],
                cwd=repository_root,
                check=False,
                capture_output=True,
                text=True,
            )

            self.assertEqual(1, completed.returncode)
            self.assertTrue(output_path.is_file())
            result = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertEqual(REJECTED, result["verdict"])
            self.assertEqual("JSON_PARSE_FAILED", result["reasons"][0]["code"])
            provenance = result["provenance"]
            self.assertEqual(["A"], provenance["activeStrategies"])
            self.assertEqual(hashlib.sha256(metrics_path.read_bytes()).hexdigest(), provenance["metricsSha256"])
            self.assertEqual(hashlib.sha256(script.read_bytes()).hexdigest(), provenance["cleanupValidatorSha256"])
            self.assertEqual(
                hashlib.sha256(contract.read_bytes()).hexdigest(),
                provenance["workloadContractSha256"],
            )

    def test_duplicate_json_member_is_rejected_before_semantic_validation(self) -> None:
        repository_root = Path(__file__).resolve().parents[2]
        script = repository_root / "Tools" / "gameplay_cleanup_slice3_admission.py"
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            metrics_path = root / "duplicate.json"
            output_path = root / "admission.json"
            payload = json.dumps(document("A")).replace(
                '"schemaVersion": 1',
                '"schemaVersion": 999, "schemaVersion": 1',
                1,
            )
            metrics_path.write_text(payload, encoding="utf-8")

            completed = subprocess.run(
                [sys.executable, str(script), str(metrics_path), "--output", str(output_path)],
                cwd=repository_root,
                check=False,
                capture_output=True,
                text=True,
            )

            self.assertEqual(1, completed.returncode)
            report = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertEqual("REJECTED", report["verdict"])
            self.assertEqual("JSON_DUPLICATE_MEMBER", report["reasons"][0]["code"])

    def test_output_input_alias_returns_infrastructure_exit_without_truncation(self) -> None:
        repository_root = Path(__file__).resolve().parents[2]
        script = repository_root / "Tools" / "gameplay_cleanup_slice3_admission.py"
        with tempfile.TemporaryDirectory() as temporary_directory:
            metrics_path = Path(temporary_directory) / "metrics.json"
            metrics_path.write_text(json.dumps({"schemaVersion": 2}), encoding="utf-8")
            before = metrics_path.read_bytes()

            completed = subprocess.run(
                [sys.executable, str(script), str(metrics_path), "--output", str(metrics_path)],
                cwd=repository_root,
                check=False,
                capture_output=True,
                text=True,
            )

            self.assertEqual(2, completed.returncode)
            self.assertEqual(before, metrics_path.read_bytes())

    def test_metrics_mutation_after_validation_is_rejected_before_output_replace(self) -> None:
        from Tools.tests.test_gameplay_performance_admission import v4_metrics

        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            metrics_path = root / "metrics.json"
            output_path = root / "admission.json"
            metrics = v4_metrics()
            metrics["cleanupSlice3Calibration"] = document("A")
            metrics_path.write_text(json.dumps(metrics), encoding="utf-8")

            def mutate_metrics(*_args: object, **_kwargs: object) -> list[dict[str, object]]:
                metrics_path.write_text(json.dumps({"mutated": True}), encoding="utf-8")
                return []

            argv = [
                str(Path(admission_tool.__file__).resolve()),
                str(metrics_path),
                "--output",
                str(output_path),
            ]
            with mock.patch.object(sys, "argv", argv):
                with mock.patch.object(
                    admission_tool,
                    "validate_v4_context_pair",
                    side_effect=mutate_metrics,
                ):
                    status = admission_tool.main()

            self.assertEqual(2, status)
            self.assertFalse(output_path.exists())

    def test_metrics_symlink_retarget_after_validation_is_rejected(self) -> None:
        from Tools.tests.test_gameplay_performance_admission import v4_metrics

        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            first = root / "first.json"
            second = root / "second.json"
            metrics_path = root / "metrics-link.json"
            output_path = root / "admission.json"
            metrics = v4_metrics()
            metrics["cleanupSlice3Calibration"] = document("A")
            first.write_text(json.dumps(metrics), encoding="utf-8")
            second.write_text(json.dumps({"mutated": True}), encoding="utf-8")
            metrics_path.symlink_to(first)

            def retarget(*_args: object, **_kwargs: object) -> list[dict[str, object]]:
                metrics_path.unlink()
                metrics_path.symlink_to(second)
                return []

            argv = [str(Path(admission_tool.__file__).resolve()), str(metrics_path), "--output", str(output_path)]
            with mock.patch.object(sys, "argv", argv):
                with mock.patch.object(admission_tool, "validate_v4_context_pair", side_effect=retarget):
                    status = admission_tool.main()

            self.assertEqual(2, status)
            self.assertFalse(output_path.exists())

    def assert_rejected_with(
        self, value: dict[str, object], strategies: tuple[str, ...], code: str
    ) -> None:
        verdict, reasons = validate_cleanup_slice3(copy.deepcopy(value), strategies)
        self.assertEqual(REJECTED, verdict)
        self.assertTrue(any(reason.startswith(code + ":") for reason in reasons), reasons)


if __name__ == "__main__":
    unittest.main()
