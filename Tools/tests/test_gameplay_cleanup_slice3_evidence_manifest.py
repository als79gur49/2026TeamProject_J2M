from __future__ import annotations

import hashlib
import copy
import contextlib
import io
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from Tools import gameplay_cleanup_slice3_evidence_manifest as manifest_tool
from Tools.gameplay_performance_admission import build_metrics_report
from Tools.gameplay_cleanup_slice3_admission import build_admission_report
from Tools.gameplay_cleanup_slice3_calibration import build_calibration_report
from Tools.gameplay_cleanup_slice3_evidence_manifest import validate_final_manifest_transport
from Tools.gameplay_evidence_v4 import EvidenceError
from Tools.gameplay_evidence_v4 import build_payload_sha256, harness_sha256, live_source_identity, runtime_tree_sha256
from Tools.tests.test_gameplay_cleanup_slice3_admission import document as cleanup_document
from Tools.tests.test_gameplay_performance_admission import admitted_metrics


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
LIVE_SOURCE_IDENTITY = live_source_identity(REPOSITORY_ROOT)


class CleanupSlice3EvidenceManifestTests(unittest.TestCase):
    def test_tick_attribution_bundle_is_hash_bound_and_fail_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            raw = root / "tick-attribution.json"
            report = root / "tick-attribution-report.json"
            artifact_manifest = root / "artifact-manifest.txt"
            validator = Path(manifest_tool.__file__).resolve().with_name(
                "gameplay_tick_attribution.py"
            )
            raw.write_text('{"schemaVersion":6}\n', encoding="utf-8")
            raw_hash = hashlib.sha256(raw.read_bytes()).hexdigest()
            validator_hash = hashlib.sha256(validator.read_bytes()).hexdigest()
            metrics_hash = "a" * 64
            report.write_text(
                json.dumps(
                    {
                        "verdict": "ADMITTED",
                        "sourceSha256": raw_hash,
                        "identitySourceSha256": metrics_hash,
                        "validatorSha256": validator_hash,
                    }
                )
                + "\n",
                encoding="utf-8",
            )
            report_hash = hashlib.sha256(report.read_bytes()).hexdigest()
            artifact_manifest.write_text(
                "\n".join(
                    (
                        f"MetricsSHA256={metrics_hash}",
                        f"TickAttributionSHA256={raw_hash}",
                        f"TickAttributionReportSHA256={report_hash}",
                        f"TickAttributionValidatorSHA256={validator_hash}",
                        "GitStatusShort:",
                    )
                )
                + "\n",
                encoding="utf-8",
            )

            manifest_tool.validate_tick_attribution_bundle(artifact_manifest)
            raw.write_text('{"schemaVersion":5}\n', encoding="utf-8")
            with self.assertRaises(EvidenceError) as context:
                manifest_tool.validate_tick_attribution_bundle(artifact_manifest)
            self.assertEqual("METRICS_HASH_MISMATCH", context.exception.reason["code"])

    def test_live_identity_failure_before_replace_writes_valid_final_hold(self) -> None:
        repository_root, _ = self._repository_paths()
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            paths = self._create_paths(root, "ADMITTED", "READY")
            manifest = paths["lifecycle_manifest"]
            lifecycle = json.loads(manifest.read_text(encoding="utf-8"))
            planned_artifacts = {
                "metrics": paths["metrics"],
                "runtimeLog": paths["runtime_log"],
                "preflightManifest": paths["preflight_manifest"],
                "artifactManifest": paths["artifact_manifest"],
                "performanceAdmission": paths["performance_admission"],
                "cleanupAdmission": paths["cleanup_admission"],
                "cleanupCalibration": paths["cleanup_calibration"],
                "performanceValidator": paths["performance_validator"],
                "tickAttribution": paths["tick_attribution"],
                "tickAttributionReport": paths["tick_attribution_report"],
                "tickAttributionValidator": paths["tick_attribution_validator"],
                "cleanupValidator": paths["validator"],
                "aggregator": paths["aggregator"],
                "manifestTool": Path(manifest_tool.__file__).resolve(),
                "workloadContract": paths["workload_contract"],
                "runner": paths["runner"],
                "playerArtifact": paths["player_artifact"],
                "buildLog": paths["build_log"],
            }
            provisional = manifest_tool.provisional_manifest(
                lifecycle["identity"], planned_artifacts
            )
            provisional["stages"] = lifecycle["stages"]
            provisional["exitStatus"] = {
                "tickAttributionAdmission": 0,
                "performanceAdmission": 0,
                "cleanupAdmission": 0,
                "cleanupCalibration": 0,
            }
            for stage, artifact_names in manifest_tool.STAGE_ARTIFACTS.items():
                provisional["stages"][stage]["artifacts"] = list(artifact_names)
            manifest.write_text(json.dumps(provisional) + "\n", encoding="utf-8")
            def reject_live_identity() -> None:
                raise EvidenceError(
                    "PRE_POST_WORKTREE_MISMATCH",
                    "live.worktreeSha256",
                    "expected",
                    "observed",
                )

            arguments = self._manifest_arguments(paths, manifest, 0, 0, 0)
            with contextlib.redirect_stdout(io.StringIO()):
                with mock.patch.object(
                    manifest_tool,
                    "_live_identity_pre_replace_check",
                    return_value=reject_live_identity,
                ):
                    status = manifest_tool.main(arguments)

            self.assertEqual(0, status)
            final = json.loads(manifest.read_text(encoding="utf-8"))
            self.assertEqual("HOLD", validate_final_manifest_transport(final))
            self.assertEqual("HOLD_INVALID_EVIDENCE", final["authoritativeVerdict"])
            self.assertEqual(
                [
                    {
                        "code": "PRE_POST_WORKTREE_MISMATCH",
                        "path": "live.worktreeSha256",
                        "expected": "expected",
                        "observed": "observed",
                    }
                ],
                final["reasons"],
            )

    def test_verified_hold_manifest_binds_rejected_evidence_and_tool_inputs(self) -> None:
        repository_root, script = self._repository_paths()
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            paths = self._create_paths(root, "REJECTED", "HOLD_INVALID_EVIDENCE")
            allocation_diagnostic = root / "cleanup-s3a-allocation-diagnostic.json"
            paths["cleanup_calibration"].replace(allocation_diagnostic)
            paths["cleanup_calibration"] = root / "cleanup-s3a-calibration-report.json"
            lifecycle = json.loads(paths["lifecycle_manifest"].read_text(encoding="utf-8"))
            for name in (
                "preflight", "build", "guardRestore", "player", "markerValidation",
                "tickAttributionAdmission", "performanceAdmission",
            ):
                lifecycle["stages"][name]["status"] = "PASS"
            lifecycle["stages"]["cleanupAdmission"]["status"] = "HOLD"
            lifecycle["stages"]["cleanupAdmission"]["reasons"] = [
                {
                    "code": "CLEANUP_REJECTED",
                    "path": "stages.cleanupAdmission",
                    "expected": None,
                    "observed": None,
                }
            ]
            lifecycle["stages"]["calibration"]["status"] = "NOT_RUN"
            lifecycle["stages"]["consistencyFinalization"]["status"] = "NOT_RUN"
            paths["lifecycle_manifest"].write_text(
                json.dumps(lifecycle) + "\n",
                encoding="utf-8",
            )
            output_path = root / "evidence-verdict-manifest.json"
            arguments = self._manifest_arguments(paths, output_path, 0, 1, 1)
            arguments.extend(("--allocation-diagnostic", str(allocation_diagnostic)))
            completed = subprocess.run(
                [sys.executable, str(script), *arguments],
                cwd=repository_root,
                check=False,
                capture_output=True,
                text=True,
            )

            self.assertEqual(0, completed.returncode, completed.stderr)
            result = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertEqual(4, result["schemaVersion"])
            self.assertEqual("HOLD", result["terminalStatus"])
            self.assertEqual("HOLD_CLEANUP_ADMISSION", result["authoritativeVerdict"])
            self.assertEqual("HOLD", result["stages"]["cleanupAdmission"]["status"])
            self.assertEqual("NOT_RUN", result["stages"]["calibration"]["status"])
            self.assertEqual("NOT_RUN", result["stages"]["consistencyFinalization"]["status"])
            self.assertEqual([], result["stages"]["calibration"]["artifacts"])
            self.assertEqual([], result["stages"]["consistencyFinalization"]["artifacts"])
            self.assertEqual(
                {"CLEANUP_REJECTED"},
                {reason["code"] for reason in result["reasons"]},
            )
            for name in (
                "metrics",
                "runtimeLog",
                "preflightManifest",
                "artifactManifest",
                "cleanupAdmission",
            ):
                source_key = self._source_key(name)
                self.assertEqual("PRESENT", result["artifacts"][name]["state"])
                self.assertEqual(hashlib.sha256(paths[source_key].read_bytes()).hexdigest(), result["artifacts"][name]["sha256"])
            self.assertEqual(
                "NOT_APPLICABLE",
                result["artifacts"]["cleanupCalibration"]["state"],
            )
            self.assertEqual(result["identity"]["metricsRevision"], result["identity"]["preBuildHeadSha"])
            self.assertTrue(result["identity"]["workloadIds"])
            self.assertTrue(result["identity"]["orderedRunKeys"])
            self.assertEqual("HOLD", validate_final_manifest_transport(result))

            terminal_script = r'''
export RUN_TESTS_LIBRARY_ONLY=1
source "$1/run_tests.sh"
validate_cleanup_s3_capture_smoke_terminal "$2" "$3" "$4" "$5" "$6" "$7"
'''
            terminal_arguments = [
                "bash", "-c", terminal_script, "allocation-terminal",
                str(repository_root), str(output_path), str(paths["metrics"]),
                str(paths["performance_admission"]), str(paths["cleanup_admission"]),
                str(paths["cleanup_calibration"]), str(allocation_diagnostic),
            ]
            accepted_terminal = subprocess.run(
                terminal_arguments,
                cwd=repository_root,
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertEqual(0, accepted_terminal.returncode, accepted_terminal.stderr)

            canonical_diagnostic = allocation_diagnostic.read_bytes()
            allocation_diagnostic.write_text(
                json.dumps({
                    "status": "HOLD_INVALID_EVIDENCE",
                    "reasons": [{
                        "code": "SEMANTIC_INVARIANT_INVALID",
                        "path": "cleanupSlice3Calibration",
                        "expected": None,
                        "observed": (
                            "S3-A calibration was not admitted: "
                            "ALLOCATION_COUNTER_PROBE_INVALID: expectedAtLeast=4096 observed=0"
                        ),
                    }],
                }) + "\n",
                encoding="utf-8",
            )
            tampered_terminal = subprocess.run(
                terminal_arguments,
                cwd=repository_root,
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertNotEqual(0, tampered_terminal.returncode)

            allocation_diagnostic.write_bytes(canonical_diagnostic)
            paths["cleanup_calibration"].write_bytes(canonical_diagnostic)
            collision_output = root / "official-calibration-collision.json"
            collision_arguments = self._manifest_arguments(
                paths, collision_output, 0, 1, 1
            )
            collision_arguments.extend(
                ("--allocation-diagnostic", str(allocation_diagnostic))
            )
            collision = subprocess.run(
                [sys.executable, str(script), *collision_arguments],
                cwd=repository_root,
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertEqual(0, collision.returncode, collision.stderr)
            collision_manifest = json.loads(
                collision_output.read_text(encoding="utf-8")
            )
            self.assertEqual(
                "HOLD_INVALID_EVIDENCE",
                collision_manifest["authoritativeVerdict"],
            )
            self.assertEqual(
                "PRESENT",
                collision_manifest["artifacts"]["cleanupCalibration"]["state"],
            )

    def test_cpu_policy_cli_matches_context_and_preserves_metrics(self):
        repository_root, script = self._repository_paths()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            paths = self._create_paths(root, "ADMITTED", "READY", policy="cpu-tick-v1")
            original = paths["metrics"].read_bytes()
            metrics = json.loads(original)
            for policy, expected_exit in (("cpu-tick-v1", 0), ("strict-v1", 1)):
                output = root / (policy + ".json")
                completed = subprocess.run([
                    sys.executable, str(paths["performance_validator"]), "metrics",
                    "--admission-policy", policy, "--metrics", str(paths["metrics"]),
                    "--planned-revision", metrics["revision"], "--expected-width", "1920",
                    "--expected-height", "1080", "--expected-warmup-frames", "120",
                    "--expected-sample-frames", "1200", "--expected-tick-interval", "1",
                    "--preflight-manifest", str(paths["preflight_manifest"]),
                    "--artifact-manifest", str(paths["artifact_manifest"]), "--output", str(output),
                ], cwd=repository_root, capture_output=True, text=True)
                self.assertEqual(expected_exit, completed.returncode, completed.stderr)
                report = json.loads(output.read_text())
                if policy == "cpu-tick-v1":
                    self.assertEqual("ADMITTED", report["primaryVerdict"])
                    self.assertEqual("partial", report["gpuCoverage"]["gameplay-neutral-tick"]["status"])
                else:
                    self.assertEqual("REJECTED_IDENTITY", report["verdict"])
                self.assertEqual(original, paths["metrics"].read_bytes())

    def test_cpu_policy_is_bound_and_canonically_revalidated(self):
        repository_root, script = self._repository_paths()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            paths = self._create_paths(root, "ADMITTED", "READY", policy="cpu-tick-v1")
            output = root / "cpu-policy.json"
            completed = self._run_manifest(repository_root, script, paths, output, 0, 0, 0)
            self.assertEqual(0, completed.returncode, completed.stderr)
            result = json.loads(output.read_text())
            self.assertEqual({"FULL_SCAN_EXPECTATION_UNAPPROVED"}, {r["code"] for r in result["reasons"]})
            context = paths["artifact_manifest"]
            context.write_text(context.read_text().replace("PerformanceAdmissionPolicy=cpu-tick-v1", "PerformanceAdmissionPolicy=strict-v1"))
            output = root / "mixed-policy.json"
            self._run_manifest(repository_root, script, paths, output, 0, 0, 0)
            result = json.loads(output.read_text())
            self.assertIn("IDENTITY_MISMATCH", {r["code"] for r in result["reasons"]})

    def test_ready_evidence_is_held_until_full_scan_oracle_is_approved(self) -> None:
        repository_root, script = self._repository_paths()
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            paths = self._create_paths(root, "ADMITTED", "READY")
            output_path = root / "ready.json"

            completed = self._run_manifest(
                repository_root, script, paths, output_path, 0, 0, 0
            )

            self.assertEqual(0, completed.returncode, completed.stderr)
            result = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertEqual("HOLD", result["terminalStatus"])
            self.assertEqual("HOLD_INVALID_EVIDENCE", result["authoritativeVerdict"])
            self.assertEqual(
                {"FULL_SCAN_EXPECTATION_UNAPPROVED"},
                {reason["code"] for reason in result["reasons"]},
            )
            self.assertEqual("HOLD", validate_final_manifest_transport(result))
            self.assertEqual(
                result["identity"]["manifestToolSha256"],
                result["artifacts"]["manifestTool"]["sha256"],
            )
            forged_identity = copy.deepcopy(result)
            forged_identity["identity"]["runnerSha256"] = "f" * 64
            with self.assertRaises(EvidenceError):
                validate_final_manifest_transport(forged_identity)
            forged = copy.deepcopy(result)
            forged["terminalStatus"] = "PASS"
            forged["authoritativeVerdict"] = "READY"
            forged["reasons"] = []
            forged["stages"]["consistencyFinalization"]["status"] = "PASS"
            forged["stages"]["consistencyFinalization"]["reasons"] = []
            with self.assertRaises(EvidenceError) as context:
                validate_final_manifest_transport(forged)
            self.assertIn("FULL_SCAN_EXPECTATION_UNAPPROVED", str(context.exception))

    def test_smoke_terminal_rejects_duplicate_member_in_bound_admission_report(self) -> None:
        repository_root, script = self._repository_paths()
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            paths = self._create_paths(root, "ADMITTED", "READY")
            output_path = root / "ready.json"
            completed = self._run_manifest(
                repository_root, script, paths, output_path, 0, 0, 0
            )
            self.assertEqual(0, completed.returncode, completed.stderr)

            performance_path = paths["performance_admission"]
            original = performance_path.read_text(encoding="utf-8")
            tampered = original.replace(
                '"verdict": "ADMITTED"',
                '"verdict": "REJECTED_IDENTITY", "verdict": "ADMITTED"',
                1,
            )
            self.assertNotEqual(original, tampered)
            performance_path.write_text(tampered, encoding="utf-8")
            manifest = json.loads(output_path.read_text(encoding="utf-8"))
            manifest["artifacts"]["performanceAdmission"]["sha256"] = hashlib.sha256(
                performance_path.read_bytes()
            ).hexdigest()
            output_path.write_text(json.dumps(manifest) + "\n", encoding="utf-8")

            terminal_script = r'''
export RUN_TESTS_LIBRARY_ONLY=1
source "$1/run_tests.sh"
validate_cleanup_s3_capture_smoke_terminal "$2" "$3" "$4" "$5" "$6" "$7"
'''
            terminal = subprocess.run(
                [
                    "bash", "-c", terminal_script, "duplicate-admission",
                    str(repository_root), str(output_path), str(paths["metrics"]),
                    str(performance_path), str(paths["cleanup_admission"]),
                    str(paths["cleanup_calibration"]), str(root / "allocation.json"),
                ],
                cwd=repository_root,
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertNotEqual(0, terminal.returncode)

    def test_pre_post_source_identity_mismatch_cannot_pass_v4(self) -> None:
        repository_root, script = self._repository_paths()
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            paths = self._create_paths(root, "ADMITTED", "READY")
            self._replace_kv(paths["artifact_manifest"], "PostRestoreHeadSha", "b" * 40)
            self._replace_kv(
                paths["artifact_manifest"], "PostRestoreWorktreeSha256", "2" * 64
            )
            output_path = root / "mixed-cohort.json"

            completed = self._run_manifest(
                repository_root, script, paths, output_path, 0, 0, 0
            )

            self.assertEqual(0, completed.returncode, completed.stderr)
            result = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertEqual(4, result["schemaVersion"])
            self.assertEqual("HOLD", result["terminalStatus"])
            self.assertEqual("HOLD_INVALID_EVIDENCE", result["authoritativeVerdict"])
            codes = {reason["code"] for reason in result["reasons"]}
            self.assertIn("PRE_POST_HEAD_MISMATCH", codes)
            self.assertIn("PRE_POST_WORKTREE_MISMATCH", codes)

    def test_persisted_performance_verdict_is_recomputed_not_trusted(self) -> None:
        repository_root, script = self._repository_paths()
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            paths = self._create_paths(root, "ADMITTED", "READY")
            forged = json.loads(paths["performance_admission"].read_text(encoding="utf-8"))
            forged["verdict"] = "REJECTED_RUNTIME"
            forged["reasons"] = [{"code": "SEMANTIC_INVARIANT_INVALID", "path": "forged", "expected": None, "observed": None}]
            paths["performance_admission"].write_text(json.dumps(forged), encoding="utf-8")

            completed = self._run_manifest(repository_root, script, paths, root / "forged.json", 1, 0, 0)

            self.assertEqual(0, completed.returncode, completed.stderr)
            result = json.loads((root / "forged.json").read_text(encoding="utf-8"))
            self.assertEqual("HOLD_INVALID_EVIDENCE", result["authoritativeVerdict"])
            self.assertIn("PERSISTED_REPORT_MISMATCH", {value["code"] for value in result["reasons"]})

    def test_duplicate_kv_key_is_fail_closed(self) -> None:
        repository_root, script = self._repository_paths()
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            paths = self._create_paths(root, "ADMITTED", "READY")
            text = paths["preflight_manifest"].read_text(encoding="utf-8")
            paths["preflight_manifest"].write_text(
                text.replace("GitStatusShort:\n", "CampaignId=duplicate\nGitStatusShort:\n"),
                encoding="utf-8",
            )

            completed = self._run_manifest(repository_root, script, paths, root / "duplicate-kv.json", 0, 0, 0)

            self.assertEqual(0, completed.returncode, completed.stderr)
            result = json.loads((root / "duplicate-kv.json").read_text(encoding="utf-8"))
            self.assertEqual("HOLD", result["terminalStatus"])
            self.assertIn("KV_DUPLICATE_KEY", {value["code"] for value in result["reasons"]})

    def test_output_alias_is_rejected_without_touching_input(self) -> None:
        repository_root, script = self._repository_paths()
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            paths = self._create_paths(root, "ADMITTED", "READY")
            before = paths["metrics"].read_bytes()

            completed = self._run_manifest(repository_root, script, paths, paths["metrics"], 0, 0, 0)

            self.assertEqual(1, completed.returncode)
            self.assertEqual(before, paths["metrics"].read_bytes())

    def test_deferred_materiality_is_not_transported_as_pass(self) -> None:
        repository_root, script = self._repository_paths()
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            paths = self._create_paths(root, "ADMITTED", "DEFERRED_NOT_MATERIAL")
            output_path = root / "deferred.json"

            completed = self._run_manifest(repository_root, script, paths, output_path, 0, 0, 0)

            self.assertEqual(0, completed.returncode, completed.stderr)
            result = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertEqual("HOLD", result["terminalStatus"])
            self.assertEqual("HOLD_INVALID_EVIDENCE", result["authoritativeVerdict"])
            self.assertIn(
                "FULL_SCAN_EXPECTATION_UNAPPROVED",
                {reason["code"] for reason in result["reasons"]},
            )

    def test_mixed_campaign_harness_tool_and_build_cohorts_are_fail_closed(self) -> None:
        repository_root, script = self._repository_paths()
        mutations = {
            "campaign": lambda paths: self._replace_json_identity(paths["metrics"], "campaignId", "other-campaign"),
            "harness": lambda paths: self._replace_kv(paths["artifact_manifest"], "HarnessSha256", "d" * 64),
            "tool": lambda paths: self._replace_report_provenance(
                paths["cleanup_admission"], "cleanupValidatorSha256", "e" * 64
            ),
            "player": lambda paths: paths["player_artifact"].write_bytes(b"mutated-player"),
        }
        for label, mutate in mutations.items():
            with self.subTest(label=label), tempfile.TemporaryDirectory() as temporary_directory:
                root = Path(temporary_directory)
                paths = self._create_paths(root, "ADMITTED", "READY")
                mutate(paths)
                output = root / f"{label}.json"

                completed = self._run_manifest(repository_root, script, paths, output, 0, 0, 0)

                self.assertEqual(0, completed.returncode, completed.stderr)
                result = json.loads(output.read_text(encoding="utf-8"))
                self.assertEqual("HOLD_INVALID_EVIDENCE", result["authoritativeVerdict"])
                self.assertNotEqual([], result["reasons"])

    def test_success_verdicts_with_fatal_reasons_or_invalid_ready_fields_are_hold(self) -> None:
        repository_root, script = self._repository_paths()
        mutations = {
            "admitted-with-reason": lambda paths: self._mutate_report(
                paths["performance_admission"],
                reasons=[{"code": "SEMANTIC_INVARIANT_INVALID", "path": "fixture", "expected": None, "observed": None}],
            ),
            "ready-without-thresholds": lambda paths: self._mutate_report(paths["cleanup_calibration"], thresholds=None),
            "ready-invalid-signal": lambda paths: self._mutate_report(paths["cleanup_calibration"], signalValid=False),
            "ready-not-material": lambda paths: self._mutate_report(paths["cleanup_calibration"], attributionMaterial=False),
        }
        for label, mutate in mutations.items():
            with self.subTest(label=label), tempfile.TemporaryDirectory() as temporary_directory:
                root = Path(temporary_directory)
                paths = self._create_paths(root, "ADMITTED", "READY")
                mutate(paths)
                output = root / f"{label}.json"

                completed = self._run_manifest(repository_root, script, paths, output, 0, 0, 0)

                self.assertEqual(0, completed.returncode, completed.stderr)
                result = json.loads(output.read_text(encoding="utf-8"))
                self.assertEqual("HOLD_INVALID_EVIDENCE", result["authoritativeVerdict"])
                codes = {value["code"] for value in result["reasons"]}
                self.assertTrue(
                    {"PERSISTED_REPORT_MISMATCH", "REASONS_COHERENCE_INVALID", "SEMANTIC_INVARIANT_INVALID", "THRESHOLDS_REQUIRED"} & codes,
                    codes,
                )

    def test_symlink_and_hardlink_output_aliases_preserve_input_bytes(self) -> None:
        repository_root, script = self._repository_paths()
        for alias_kind in ("symlink", "hardlink"):
            with self.subTest(alias_kind=alias_kind), tempfile.TemporaryDirectory() as temporary_directory:
                root = Path(temporary_directory)
                paths = self._create_paths(root, "ADMITTED", "READY")
                output = root / f"{alias_kind}.json"
                if alias_kind == "symlink":
                    output.symlink_to(paths["metrics"])
                else:
                    output.hardlink_to(paths["metrics"])
                before = paths["metrics"].read_bytes()

                completed = self._run_manifest(repository_root, script, paths, output, 0, 0, 0)

                self.assertEqual(1, completed.returncode)
                self.assertEqual(before, paths["metrics"].read_bytes())

    def test_historical_schema_one_artifacts_cannot_yield_v4_pass(self) -> None:
        repository_root, script = self._repository_paths()
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            paths = self._create_paths(root, "ADMITTED", "READY")
            metrics = json.loads(paths["metrics"].read_text(encoding="utf-8"))
            metrics["schemaVersion"] = 1
            metrics.pop("evidenceContractVersion")
            metrics.pop("captureIdentity")
            paths["metrics"].write_text(json.dumps(metrics), encoding="utf-8")
            output = root / "historical.json"

            completed = self._run_manifest(repository_root, script, paths, output, 0, 0, 0)

            self.assertEqual(0, completed.returncode, completed.stderr)
            result = json.loads(output.read_text(encoding="utf-8"))
            self.assertEqual("HOLD", result["terminalStatus"])
            self.assertNotEqual([], result["reasons"])

    def test_rejected_artifact_with_zero_caller_status_is_invalid_hold(self) -> None:
        repository_root, script = self._repository_paths()
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            paths = self._create_paths(root, "REJECTED", "HOLD_INVALID_EVIDENCE")
            output_path = root / "invalid.json"

            completed = self._run_manifest(
                repository_root, script, paths, output_path, 0, 0, 0
            )

            self.assertEqual(0, completed.returncode)
            result = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertEqual("HOLD", result["terminalStatus"])
            self.assertIn("EXIT_STATUS_MISMATCH", {reason["code"] for reason in result["reasons"]})

    def test_performance_admission_failure_is_verified_hold(self) -> None:
        repository_root, script = self._repository_paths()
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            paths = self._create_paths(root, "ADMITTED", "READY")
            output_path = root / "performance-hold.json"

            completed = self._run_manifest(
                repository_root, script, paths, output_path, 1, 0, 0
            )

            self.assertEqual(0, completed.returncode, completed.stderr)
            result = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertEqual("HOLD", result["terminalStatus"])
            self.assertEqual("HOLD_INVALID_EVIDENCE", result["authoritativeVerdict"])
            self.assertIn("EXIT_STATUS_MISMATCH", {reason["code"] for reason in result["reasons"]})

    def test_metrics_tamper_after_verdict_creation_is_invalid_hold(self) -> None:
        repository_root, script = self._repository_paths()
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            paths = self._create_paths(root, "ADMITTED", "READY")
            first_output = root / "first.json"
            second_output = root / "second.json"
            self.assertEqual(
                0,
                self._run_manifest(
                    repository_root, script, paths, first_output, 0, 0, 0
                ).returncode,
            )
            paths["metrics"].write_text('{"tampered":true}\n', encoding="utf-8")

            completed = self._run_manifest(
                repository_root, script, paths, second_output, 0, 0, 0
            )

            self.assertEqual(0, completed.returncode)
            result = json.loads(second_output.read_text(encoding="utf-8"))
            self.assertEqual("HOLD", result["terminalStatus"])
            self.assertIn("METRICS_HASH_MISMATCH", {reason["code"] for reason in result["reasons"]})

    def test_missing_artifact_still_writes_invalid_hold_manifest(self) -> None:
        repository_root, script = self._repository_paths()
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            paths = self._create_paths(root, "REJECTED", "HOLD_INVALID_EVIDENCE")
            paths["cleanup_calibration"].unlink()
            output_path = root / "missing.json"

            completed = self._run_manifest(
                repository_root, script, paths, output_path, 0, 1, 1
            )

            self.assertEqual(0, completed.returncode)
            self.assertTrue(output_path.is_file())
            result = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertEqual("MISSING", result["artifacts"]["cleanupCalibration"]["state"])
            self.assertEqual("HOLD_INVALID_EVIDENCE", result["authoritativeVerdict"])

    @staticmethod
    def _repository_paths() -> tuple[Path, Path]:
        repository_root = Path(__file__).resolve().parents[2]
        return (
            repository_root,
            repository_root / "Tools" / "gameplay_cleanup_slice3_evidence_manifest.py",
        )

    @staticmethod
    def _create_paths(
        root: Path, admission_verdict: str, calibration_status: str, policy: str = "strict-v1"
    ) -> dict[str, Path]:
        paths = {
            "metrics": root / "performance-metrics.json",
            "runtime_log": root / "player-runtime.log",
            "preflight_manifest": root / "preflight-manifest.txt",
            "artifact_manifest": root / "artifact-manifest.txt",
            "performance_admission": root / "performance-admission-report.json",
            "tick_attribution": root / "tick-attribution.json",
            "tick_attribution_report": root / "tick-attribution-report.json",
            "tick_attribution_validator": Path(__file__).resolve().parents[1] / "gameplay_tick_attribution.py",
            "cleanup_admission": root / "cleanup-s3a-admission-summary.json",
            "cleanup_calibration": root / "cleanup-s3a-calibration-report.json",
            "validator": Path(__file__).resolve().parents[1] / "gameplay_cleanup_slice3_admission.py",
            "performance_validator": Path(__file__).resolve().parents[1] / "gameplay_performance_admission.py",
            "aggregator": Path(__file__).resolve().parents[1] / "gameplay_cleanup_slice3_calibration.py",
            "workload_contract": Path(__file__).resolve().parents[1] / "contracts" / "gameplay_cleanup_slice3_workloads_v2.json",
            "runner": Path(__file__).resolve().parents[2] / "run_tests.sh",
            "player_artifact": root / "build" / "VectorQuake.exe",
            "build_log": root / "build.log",
            "build_root": root / "build",
            "lifecycle_manifest": root / "attempt-manifest.json",
        }
        paths["build_root"].mkdir(parents=True)
        paths["player_artifact"].write_bytes(b"fixture-player-v4")
        paths["build_log"].write_text("fixture build log\n", encoding="utf-8")
        paths["tick_attribution"].write_text("{}\n", encoding="utf-8")
        paths["tick_attribution_report"].write_text("{}\n", encoding="utf-8")
        runner_sha256 = hashlib.sha256(paths["runner"].read_bytes()).hexdigest()
        performance_validator_sha256 = hashlib.sha256(paths["performance_validator"].read_bytes()).hexdigest()
        cleanup_validator_sha256 = hashlib.sha256(paths["validator"].read_bytes()).hexdigest()
        aggregator_sha256 = hashlib.sha256(paths["aggregator"].read_bytes()).hexdigest()
        manifest_tool_sha256 = hashlib.sha256(
            (Path(__file__).resolve().parents[1] / "gameplay_cleanup_slice3_evidence_manifest.py").read_bytes()
        ).hexdigest()
        contract_sha256 = hashlib.sha256(paths["workload_contract"].read_bytes()).hexdigest()
        source_identity = LIVE_SOURCE_IDENTITY
        worktree_sha256 = source_identity["worktreeSha256"]
        identity = {
            "campaignId": "fixture-campaign",
            "attemptId": "fixture-attempt-1",
            "attemptOrdinal": 1,
            "attemptKind": "calibration",
            "captureNonce": "fixture-nonce",
            "stage": "S3-A",
            "activeStrategies": ["A"],
            "preBuildHeadSha": source_identity["headSha"],
            "preBuildWorktreeSha256": worktree_sha256,
            "postRestoreHeadSha": source_identity["headSha"],
            "postRestoreWorktreeSha256": worktree_sha256,
            "runtimeTreeSha256": source_identity["runtimeTreeSha256"],
            "playerArtifactSha256": hashlib.sha256(paths["player_artifact"].read_bytes()).hexdigest(),
            "buildPayloadSha256": build_payload_sha256(paths["build_root"]),
            "runnerSha256": runner_sha256,
            "performanceValidatorSha256": performance_validator_sha256,
            "cleanupValidatorSha256": cleanup_validator_sha256,
            "aggregatorSha256": aggregator_sha256,
            "manifestToolSha256": manifest_tool_sha256,
            "workloadContractSha256": contract_sha256,
            "harnessSha256": harness_sha256(
                runner_sha256=runner_sha256,
                performance_validator_sha256=performance_validator_sha256,
                cleanup_validator_sha256=cleanup_validator_sha256,
                aggregator_sha256=aggregator_sha256,
                manifest_tool_sha256=manifest_tool_sha256,
                workload_contract_sha256=contract_sha256,
            ),
        }
        metrics = admitted_metrics()
        metrics["schemaVersion"] = 2
        metrics["evidenceContractVersion"] = 4
        metrics["captureIdentity"] = identity
        metrics["revision"] = identity["preBuildHeadSha"]
        cleanup = cleanup_document("A")
        cleanup["schemaVersion"] = 2
        cleanup["evidenceContractVersion"] = 4
        cleanup["activeStrategies"] = ["A"]
        cleanup["repetitions"] = 3
        cleanup["warmupTicksPerRepetition"] = 30
        cleanup["sampleTicksPerRepetition"] = 100
        cleanup["allocationSignal"] = "per-tick current-thread allocated-byte delta appended by Player probe"
        for workload in cleanup["captures"][0]["workloads"]:
            base = workload["runs"][0]
            workload["runs"] = []
            for repetition in range(1, 4):
                run = copy.deepcopy(base)
                run["repetition"] = repetition
                run["runKey"] = f"A/{workload['workloadId']}/{repetition}"
                workload["runs"].append(run)
        if admission_verdict != "ADMITTED":
            cleanup["frameAllocationCalibration"]["allocationCounterProbeBytes"] = 0
        if calibration_status == "DEFERRED_NOT_MATERIAL":
            for run in cleanup["captures"][0]["workloads"][0]["runs"]:
                for key in ("median", "p95", "p99", "maximum"):
                    run["cleanupProcessorMilliseconds"][key] = 0.001
        if policy == "cpu-tick-v1":
            metrics["phases"][1]["validGpuSamples"] -= 1
            metrics["phases"][1]["gpuMilliseconds"]["count"] -= 1
        metrics["cleanupSlice3Calibration"] = cleanup
        paths["metrics"].write_text(json.dumps(metrics) + "\n", encoding="utf-8")
        paths["runtime_log"].write_text("GAMEPLAY_PERFORMANCE:PASS\n", encoding="utf-8")
        metrics_sha256 = hashlib.sha256(paths["metrics"].read_bytes()).hexdigest()
        runtime_sha256 = hashlib.sha256(paths["runtime_log"].read_bytes()).hexdigest()
        validator_sha256 = hashlib.sha256(paths["validator"].read_bytes()).hexdigest()
        aggregator_sha256 = hashlib.sha256(paths["aggregator"].read_bytes()).hexdigest()
        contract_sha256 = hashlib.sha256(paths["workload_contract"].read_bytes()).hexdigest()
        kv_identity = {
            "CampaignId": identity["campaignId"], "AttemptId": identity["attemptId"],
            "AttemptOrdinal": "1", "AttemptKind": identity["attemptKind"],
            "CaptureNonce": identity["captureNonce"], "Stage": "S3-A", "ActiveStrategies": "A",
            "PreBuildHeadSha": identity["preBuildHeadSha"],
            "PreBuildWorktreeSha256": identity["preBuildWorktreeSha256"],
            "RuntimeTreeSha256": identity["runtimeTreeSha256"],
            "RunnerSha256": identity["runnerSha256"],
            "PerformanceValidatorSha256": identity["performanceValidatorSha256"],
            "CleanupValidatorSha256": identity["cleanupValidatorSha256"],
            "AggregatorSha256": identity["aggregatorSha256"],
            "ManifestToolSha256": identity["manifestToolSha256"],
            "WorkloadContractSha256": identity["workloadContractSha256"],
            "HarnessSha256": identity["harnessSha256"],
            "ExpectedWidth": "1920", "ExpectedHeight": "1080",
            "ExpectedWarmupFrames": "120", "ExpectedSampleFrames": "1200",
            "ExpectedTickInterval": "1",
        }
        if policy == "cpu-tick-v1":
            kv_identity["PerformanceAdmissionPolicy"] = policy
        paths["preflight_manifest"].write_text(
            "SchemaVersion=2\nEvidenceContractVersion=4\nEvidencePhase=preflight\n" +
            "\n".join(f"{key}={value}" for key, value in kv_identity.items()) +
            "\nGitStatusShort:\n",
            encoding="utf-8",
        )
        captured_identity = dict(kv_identity)
        captured_identity["PostRestoreHeadSha"] = identity["postRestoreHeadSha"]
        captured_identity["PostRestoreWorktreeSha256"] = identity["postRestoreWorktreeSha256"]
        captured_identity["PlayerArtifactSha256"] = identity["playerArtifactSha256"]
        if policy == "cpu-tick-v1":
            kv_identity["PerformanceAdmissionPolicy"] = policy
            captured_identity["PerformanceAdmissionPolicy"] = policy
        captured_identity["BuildPayloadSHA256"] = identity["buildPayloadSha256"]
        paths["artifact_manifest"].write_text(
            "SchemaVersion=2\nEvidenceContractVersion=4\nEvidencePhase=artifact-captured\n" +
            "\n".join(f"{key}={value}" for key, value in captured_identity.items()) +
            f"\nMetricsSHA256={metrics_sha256}\nRuntimeLogSHA256={runtime_sha256}\nGitStatusShort:\n",
            encoding="utf-8",
        )
        common_provenance = {
            "metricsSha256": metrics_sha256,
            "cleanupValidatorSha256": validator_sha256,
            "workloadContractSha256": contract_sha256,
            "activeStrategies": ["A"],
        }
        performance_report = build_metrics_report(
            metrics,
            metrics_sha256=metrics_sha256,
            validator_path=paths["performance_validator"],
            planned_revision=identity["preBuildHeadSha"],
            expected_width=1920,
            expected_height=1080,
            expected_warmup_frames=120,
            expected_sample_frames=1200,
            expected_tick_interval=1,
            admission_policy=policy,
        )
        paths["performance_admission"].write_text(json.dumps(performance_report) + "\n", encoding="utf-8")
        cleanup_report = build_admission_report(
            metrics,
            metrics_sha256=metrics_sha256,
            active_strategies=("A",),
            validator_path=paths["validator"],
            workload_contract_path=paths["workload_contract"],
        )
        paths["cleanup_admission"].write_text(json.dumps(cleanup_report) + "\n", encoding="utf-8")
        calibration_report = build_calibration_report(
            metrics,
            metrics_sha256=metrics_sha256,
            validator_path=paths["validator"],
            aggregator_path=paths["aggregator"],
            workload_contract_path=paths["workload_contract"],
        )
        self_status = calibration_report["status"]
        if self_status != calibration_status:
            raise AssertionError(f"fixture requested {calibration_status}, canonical builder produced {self_status}")
        paths["cleanup_calibration"].write_text(json.dumps(calibration_report) + "\n", encoding="utf-8")
        stages = {
            name: {"status": "NOT_RUN", "reasons": [], "artifacts": []}
            for name in (
                "preflight", "build", "guardRestore", "player", "markerValidation",
                "tickAttributionAdmission", "performanceAdmission", "cleanupAdmission", "calibration",
                "consistencyFinalization",
            )
        }
        for name in tuple(stages)[:-1]:
            stages[name]["status"] = (
                "DEFERRED"
                if name == "calibration" and calibration_status == "DEFERRED_NOT_MATERIAL"
                else "PASS"
            )
        lifecycle = {
            "schemaVersion": 4,
            "evidenceContractVersion": 4,
            "manifestState": "PROVISIONAL",
            "terminalStatus": "NOT_RUN",
            "authoritativeVerdict": "NOT_RUN",
            "identity": identity,
            "reasons": [],
            "stages": stages,
            "artifacts": {},
            "exitStatus": {},
        }
        paths["lifecycle_manifest"].write_text(json.dumps(lifecycle) + "\n", encoding="utf-8")
        return paths

    @staticmethod
    def _source_key(manifest_name: str) -> str:
        return {
            "runtimeLog": "runtime_log",
            "preflightManifest": "preflight_manifest",
            "artifactManifest": "artifact_manifest",
            "cleanupAdmission": "cleanup_admission",
            "cleanupCalibration": "cleanup_calibration",
        }.get(manifest_name, manifest_name)

    @staticmethod
    def _replace_json_identity(path: Path, field: str, value: object) -> None:
        document = json.loads(path.read_text(encoding="utf-8"))
        document["captureIdentity"][field] = value
        path.write_text(json.dumps(document), encoding="utf-8")

    @staticmethod
    def _replace_kv(path: Path, key: str, value: str) -> None:
        lines = path.read_text(encoding="utf-8").splitlines()
        path.write_text(
            "\n".join(f"{key}={value}" if line.startswith(key + "=") else line for line in lines) + "\n",
            encoding="utf-8",
        )

    @staticmethod
    def _mutate_report(path: Path, **changes: object) -> None:
        document = json.loads(path.read_text(encoding="utf-8"))
        document.update(changes)
        path.write_text(json.dumps(document), encoding="utf-8")

    @staticmethod
    def _replace_report_provenance(path: Path, field: str, value: object) -> None:
        document = json.loads(path.read_text(encoding="utf-8"))
        document["provenance"][field] = value
        path.write_text(json.dumps(document), encoding="utf-8")

    @staticmethod
    def _run_manifest(
        repository_root: Path,
        script: Path,
        paths: dict[str, Path],
        output_path: Path,
        performance_status: int,
        cleanup_admission_status: int,
        cleanup_calibration_status: int,
    ) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [sys.executable, str(script), *CleanupSlice3EvidenceManifestTests._manifest_arguments(
                paths,
                output_path,
                performance_status,
                cleanup_admission_status,
                cleanup_calibration_status,
            )],
            cwd=repository_root,
            check=False,
            capture_output=True,
            text=True,
        )

    @staticmethod
    def _manifest_arguments(
        paths: dict[str, Path],
        output_path: Path,
        performance_status: int,
        cleanup_admission_status: int,
        cleanup_calibration_status: int,
    ) -> list[str]:
        return [
                "--manifest",
                str(paths["lifecycle_manifest"]),
                "--metrics",
                str(paths["metrics"]),
                "--runtime-log",
                str(paths["runtime_log"]),
                "--preflight-manifest",
                str(paths["preflight_manifest"]),
                "--artifact-manifest",
                str(paths["artifact_manifest"]),
                "--performance-admission",
                str(paths["performance_admission"]),
                "--tick-attribution",
                str(paths["tick_attribution"]),
                "--tick-attribution-report",
                str(paths["tick_attribution_report"]),
                "--tick-attribution-validator",
                str(paths["tick_attribution_validator"]),
                "--cleanup-admission",
                str(paths["cleanup_admission"]),
                "--cleanup-calibration",
                str(paths["cleanup_calibration"]),
                "--validator",
                str(paths["validator"]),
                "--performance-validator",
                str(paths["performance_validator"]),
                "--aggregator",
                str(paths["aggregator"]),
                "--workload-contract",
                str(paths["workload_contract"]),
                "--runner",
                str(paths["runner"]),
                "--player-artifact",
                str(paths["player_artifact"]),
                "--build-log",
                str(paths["build_log"]),
                "--build-root",
                str(paths["build_root"]),
                "--performance-admission-status",
                str(performance_status),
                "--cleanup-admission-status",
                str(cleanup_admission_status),
                "--cleanup-calibration-status",
                str(cleanup_calibration_status),
                "--output",
                str(output_path),
            ]


if __name__ == "__main__":
    unittest.main()
