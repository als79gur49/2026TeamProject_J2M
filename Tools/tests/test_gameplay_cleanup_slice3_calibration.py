from __future__ import annotations

import copy
import hashlib
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from Tools import gameplay_cleanup_slice3_calibration as calibration_tool
from Tools.gameplay_cleanup_slice3_calibration import derive_calibration
from Tools.tests.test_gameplay_cleanup_slice3_admission import document


class CleanupSlice3CalibrationTests(unittest.TestCase):
    def test_standalone_missing_v4_context_fails_closed(self) -> None:
        from Tools.tests.test_gameplay_performance_admission import v4_metrics

        repository_root = Path(__file__).resolve().parents[2]
        script = repository_root / "Tools" / "gameplay_cleanup_slice3_calibration.py"
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            metrics_path = root / "metrics.json"
            output_path = root / "cleanup-calibration.json"
            metrics = v4_metrics()
            metrics["cleanupSlice3Calibration"] = copy.deepcopy(document("A"))
            metrics["cleanupSlice3Calibration"]["schemaVersion"] = 2
            metrics["cleanupSlice3Calibration"]["evidenceContractVersion"] = 4
            metrics["cleanupSlice3Calibration"]["activeStrategies"] = ["A"]
            metrics["cleanupSlice3Calibration"]["warmupTicksPerRepetition"] = 30
            metrics["cleanupSlice3Calibration"]["sampleTicksPerRepetition"] = 100
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

    def test_derives_exact_thresholds_from_three_admitted_a_repetitions(self) -> None:
        source = document("A")
        capture = source["captures"][0]
        calibration = {
            "schemaVersion": 1,
            "stage": "S3-A",
            "strategy": "A",
            "repetitions": 3,
            "warmupTicksPerRepetition": 30,
            "sampleTicksPerRepetition": 100,
            "allocationSignal": "per-tick current-thread allocated-byte delta appended by Player probe",
            "diagnosticsOffNoOpAllocatedBytes": 0,
            "workloads": copy.deepcopy(capture["workloads"]),
            "frameAllocationCalibration": copy.deepcopy(source["frameAllocationCalibration"]),
        }
        for workload in calibration["workloads"]:
            base = workload["runs"][0]
            workload["runs"] = []
            for repetition, scale in enumerate((0.95, 1.0, 1.05), 1):
                run = copy.deepcopy(base)
                run["repetition"] = repetition
                for key in (
                    "wholeTickMilliseconds",
                    "cleanupProcessorMilliseconds",
                    "runCleanupPhaseMilliseconds",
                    "captureOffWholeTickMilliseconds",
                ):
                    for field in ("median", "p95", "p99", "maximum"):
                        run[key][field] *= scale
                workload["runs"].append(run)

        report = derive_calibration({"cleanupSlice3Calibration": calibration})

        self.assertEqual("S3-A", report["stage"])
        self.assertTrue(report["admitted"])
        self.assertTrue(report["attributionMaterial"])
        self.assertAlmostEqual(10.0, report["observedNoisePercent"])
        self.assertAlmostEqual(20.0, report["thresholds"]["cleanupProcessorMaterialImprovementPercent"])
        self.assertAlmostEqual(10.0, report["thresholds"]["targetWholeTickBenefitPercent"])
        self.assertEqual(5.0, report["thresholds"]["stressWholeTickRegressionCeilingPercent"])
        self.assertEqual(30, report["campaignRules"]["internalWarmupTicks"])
        self.assertEqual(100, report["campaignRules"]["sampleTicks"])

    def test_noise_above_ceiling_is_hold_and_does_not_freeze_thresholds(self) -> None:
        source = document("A")
        capture = source["captures"][0]
        calibration = {
            "schemaVersion": 1,
            "stage": "S3-A",
            "strategy": "A",
            "repetitions": 3,
            "warmupTicksPerRepetition": 30,
            "sampleTicksPerRepetition": 100,
            "allocationSignal": "per-tick current-thread allocated-byte delta appended by Player probe",
            "diagnosticsOffNoOpAllocatedBytes": 0,
            "workloads": copy.deepcopy(capture["workloads"]),
            "frameAllocationCalibration": copy.deepcopy(source["frameAllocationCalibration"]),
        }
        for workload in calibration["workloads"]:
            base = workload["runs"][0]
            workload["runs"] = []
            for repetition, scale in enumerate((0.5, 1.0, 1.5), 1):
                run = copy.deepcopy(base)
                run["repetition"] = repetition
                for key in (
                    "wholeTickMilliseconds",
                    "cleanupProcessorMilliseconds",
                    "runCleanupPhaseMilliseconds",
                    "captureOffWholeTickMilliseconds",
                ):
                    for field in ("median", "p95", "p99", "maximum"):
                        run[key][field] *= scale
                workload["runs"].append(run)

        report = derive_calibration({"cleanupSlice3Calibration": calibration})

        self.assertEqual("HOLD_INVALID_SIGNAL", report["status"])
        self.assertFalse(report["signalValid"])
        self.assertIsNone(report["thresholds"])

    def test_malformed_json_cli_preserves_hold_artifact_with_provenance(self) -> None:
        repository_root = Path(__file__).resolve().parents[2]
        script = repository_root / "Tools" / "gameplay_cleanup_slice3_calibration.py"
        validator = repository_root / "Tools" / "gameplay_cleanup_slice3_admission.py"
        contract = repository_root / "Tools" / "contracts" / "gameplay_cleanup_slice3_workloads_v2.json"
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            metrics_path = root / "malformed.json"
            output_path = root / "calibration.json"
            metrics_path.write_text("{ malformed", encoding="utf-8")

            completed = subprocess.run(
                [sys.executable, str(script), str(metrics_path), "--output", str(output_path)],
                cwd=repository_root,
                check=False,
                capture_output=True,
                text=True,
            )

            self.assertEqual(1, completed.returncode)
            self.assertTrue(output_path.is_file())
            result = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertEqual("HOLD_INVALID_EVIDENCE", result["status"])
            self.assertFalse(result["admitted"])
            self.assertIsNone(result["thresholds"])
            self.assertEqual("JSON_PARSE_FAILED", result["reasons"][0]["code"])
            provenance = result["provenance"]
            self.assertEqual(["A"], provenance["activeStrategies"])
            self.assertEqual(hashlib.sha256(metrics_path.read_bytes()).hexdigest(), provenance["metricsSha256"])
            self.assertEqual(hashlib.sha256(script.read_bytes()).hexdigest(), provenance["aggregatorSha256"])
            self.assertEqual(hashlib.sha256(validator.read_bytes()).hexdigest(), provenance["cleanupValidatorSha256"])
            self.assertEqual(
                hashlib.sha256(contract.read_bytes()).hexdigest(),
                provenance["workloadContractSha256"],
            )

    def test_non_object_metrics_cli_preserves_hold_artifact(self) -> None:
        repository_root = Path(__file__).resolve().parents[2]
        script = repository_root / "Tools" / "gameplay_cleanup_slice3_calibration.py"
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            metrics_path = root / "array.json"
            output_path = root / "calibration.json"
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
            self.assertEqual("HOLD_INVALID_EVIDENCE", result["status"])
            self.assertFalse(result["admitted"])
            self.assertEqual("JSON_ROOT_INVALID", result["reasons"][0]["code"])

    def test_duplicate_json_member_has_exact_machine_readable_hold_reason(self) -> None:
        repository_root = Path(__file__).resolve().parents[2]
        script = repository_root / "Tools" / "gameplay_cleanup_slice3_calibration.py"
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            metrics_path = root / "duplicate.json"
            output_path = root / "calibration.json"
            metrics_path.write_text('{"schemaVersion":1,"schemaVersion":1}\n', encoding="utf-8")

            completed = subprocess.run(
                [sys.executable, str(script), str(metrics_path), "--output", str(output_path)],
                cwd=repository_root,
                check=False,
                capture_output=True,
                text=True,
            )

            self.assertEqual(1, completed.returncode)
            report = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertEqual("HOLD_INVALID_EVIDENCE", report["status"])
            self.assertEqual("JSON_DUPLICATE_MEMBER", report["reasons"][0]["code"])

    def test_output_input_alias_returns_infrastructure_exit_without_truncation(self) -> None:
        repository_root = Path(__file__).resolve().parents[2]
        script = repository_root / "Tools" / "gameplay_cleanup_slice3_calibration.py"
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
            output_path = root / "calibration.json"
            metrics = v4_metrics()
            metrics["cleanupSlice3Calibration"] = copy.deepcopy(document("A"))
            metrics_path.write_text(json.dumps(metrics), encoding="utf-8")

            def mutate_metrics(*_args: object, **_kwargs: object) -> list[dict[str, object]]:
                metrics_path.write_text(json.dumps({"mutated": True}), encoding="utf-8")
                return []

            argv = [
                str(Path(calibration_tool.__file__).resolve()),
                str(metrics_path),
                "--output",
                str(output_path),
            ]
            with mock.patch.object(sys, "argv", argv):
                with mock.patch.object(
                    calibration_tool,
                    "validate_v4_context_pair",
                    side_effect=mutate_metrics,
                ):
                    status = calibration_tool.main()

            self.assertEqual(2, status)
            self.assertFalse(output_path.exists())

    def test_metrics_symlink_retarget_after_validation_is_rejected(self) -> None:
        from Tools.tests.test_gameplay_performance_admission import v4_metrics

        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            first = root / "first.json"
            second = root / "second.json"
            metrics_path = root / "metrics-link.json"
            output_path = root / "calibration.json"
            metrics = v4_metrics()
            metrics["cleanupSlice3Calibration"] = copy.deepcopy(document("A"))
            first.write_text(json.dumps(metrics), encoding="utf-8")
            second.write_text(json.dumps({"mutated": True}), encoding="utf-8")
            metrics_path.symlink_to(first)

            def retarget(*_args: object, **_kwargs: object) -> list[dict[str, object]]:
                metrics_path.unlink()
                metrics_path.symlink_to(second)
                return []

            argv = [str(Path(calibration_tool.__file__).resolve()), str(metrics_path), "--output", str(output_path)]
            with mock.patch.object(sys, "argv", argv):
                with mock.patch.object(calibration_tool, "validate_v4_context_pair", side_effect=retarget):
                    status = calibration_tool.main()

            self.assertEqual(2, status)
            self.assertFalse(output_path.exists())


if __name__ == "__main__":
    unittest.main()
