from __future__ import annotations

import copy
import hashlib
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from Tools.gameplay_cleanup_slice3_evidence_manifest import validate_final_manifest_transport
from Tools.gameplay_evidence_v4 import EvidenceError


REPO_ROOT = Path(__file__).resolve().parents[2]


class CleanupSlice3RunnerLifecycleTests(unittest.TestCase):
    def test_pre_provisional_failure_returns_transport_error_without_terminal_line(self) -> None:
        script = r'''
set +e
export RUN_TESTS_LIBRARY_ONLY=1
source "$1/run_tests.sh"
set +e
DRY_RUN=0
GAMEPLAY_PERFORMANCE_EVIDENCE_ROOT="$2/evidence"
GAMEPLAY_PERFORMANCE_BUILD_ROOT="$2/build"
ensure_no_current_project_unity_process() { return 7; }
run_gameplay_performance
status=$?
printf 'STATUS=%s\n' "$status"
exit 0
'''
        with tempfile.TemporaryDirectory() as temporary_directory:
            completed = subprocess.run(
                ["bash", "-c", script, "runner-probe", str(REPO_ROOT), temporary_directory],
                cwd=REPO_ROOT,
                check=False,
                capture_output=True,
                text=True,
            )

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn("STATUS=2", completed.stdout)
        self.assertNotIn("Gameplay performance measurement:", completed.stdout)

    def test_pre_provisional_setup_failures_return_transport_error_without_terminal_line(self) -> None:
        script = r'''
export RUN_TESTS_LIBRARY_ONLY=1
source "$1/run_tests.sh"
set +e
DRY_RUN=0
GAMEPLAY_PERFORMANCE_EVIDENCE_ROOT="$2/evidence"
GAMEPLAY_PERFORMANCE_BUILD_ROOT="$2/build"
case "$3" in
    git) git() { return 7; } ;;
    sha256sum) sha256sum() { return 7; } ;;
    wslpath) wslpath() { return 7; } ;;
esac
run_gameplay_performance
status=$?
printf 'STATUS=%s\n' "$status"
trap -p ERR INT TERM
exit 0
'''
        for failing_command in ("git", "sha256sum", "wslpath"):
            with self.subTest(command=failing_command), tempfile.TemporaryDirectory() as temporary_directory:
                completed = subprocess.run(
                    [
                        "bash", "-c", script, "runner-setup-probe",
                        str(REPO_ROOT), temporary_directory, failing_command,
                    ],
                    cwd=REPO_ROOT,
                    check=False,
                    capture_output=True,
                    text=True,
                )

                self.assertEqual(0, completed.returncode, completed.stderr)
                self.assertIn("STATUS=2", completed.stdout)
                self.assertNotIn("Gameplay performance measurement:", completed.stdout)
                self.assertNotIn("trap --", completed.stdout)

    def test_gameplay_performance_dry_run_clears_pre_provisional_traps(self) -> None:
        script = r'''
export RUN_TESTS_LIBRARY_ONLY=1
source "$1/run_tests.sh"
set +e
DRY_RUN=1
run_gameplay_performance >/dev/null
status=$?
printf 'STATUS=%s\n' "$status"
trap -p ERR INT TERM
exit 0
'''
        completed = subprocess.run(
            ["bash", "-c", script, "runner-dry-run-probe", str(REPO_ROOT)],
            cwd=REPO_ROOT,
            check=False,
            capture_output=True,
            text=True,
        )

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn("STATUS=0", completed.stdout)
        self.assertNotIn("trap --", completed.stdout)

    def test_pre_provisional_signals_return_transport_error_without_terminal_line(self) -> None:
        script = r'''
export RUN_TESTS_LIBRARY_ONLY=1
source "$1/run_tests.sh"
set +e
DRY_RUN=0
GAMEPLAY_PERFORMANCE_EVIDENCE_ROOT="$2/evidence"
GAMEPLAY_PERFORMANCE_BUILD_ROOT="$2/build"
RUNNER_PROBE_SIGNAL="$3"
ensure_no_current_project_unity_process() { kill -"$RUNNER_PROBE_SIGNAL" "$BASHPID"; }
run_gameplay_performance
status=$?
printf 'STATUS=%s\n' "$status"
exit 0
'''
        for signal_name in ("INT", "TERM"):
            with self.subTest(signal=signal_name), tempfile.TemporaryDirectory() as temporary_directory:
                completed = subprocess.run(
                    [
                        "bash", "-c", script, "runner-signal-probe",
                        str(REPO_ROOT), temporary_directory, signal_name,
                    ],
                    cwd=REPO_ROOT,
                    check=False,
                    capture_output=True,
                    text=True,
                )

                self.assertEqual(0, completed.returncode, completed.stderr)
                self.assertIn("STATUS=2", completed.stdout)
                self.assertNotIn("Gameplay performance measurement:", completed.stdout)

    def test_runner_initializes_provisional_manifest_before_build(self) -> None:
        source = (REPO_ROOT / "run_tests.sh").read_text(encoding="utf-8")
        function = source.split("run_gameplay_performance() {", 1)[1].split(
            "\n}\n\nrun_typography_visual()", 1
        )[0]
        provisional = function.index("--initialize-provisional")
        build = function.index("build_status=0")
        self.assertLess(provisional, build)

    def test_runner_reads_terminal_status_instead_of_child_exit_inference(self) -> None:
        source = (REPO_ROOT / "run_tests.sh").read_text(encoding="utf-8")
        function = source.split("run_gameplay_performance() {", 1)[1].split(
            "\n}\n\nrun_typography_visual()", 1
        )[0]
        self.assertIn('terminalStatus', function)
        self.assertIn('--print-terminal-status --manifest "$cleanup_evidence_manifest_path"', function)
        self.assertNotIn(
            'terminal_status="$(python3 -c \'import json,sys; print(json.load',
            function,
        )
        self.assertIn('Gameplay performance measurement: DEFERRED', function)
        self.assertIn('Gameplay performance measurement: HOLD', function)
        self.assertNotIn('echo "Gameplay performance measurement: PASS"\n    return 0', function)

    def test_runner_recomputes_live_post_restore_identity(self) -> None:
        source = (REPO_ROOT / "run_tests.sh").read_text(encoding="utf-8")
        function = source.split("run_gameplay_performance() {", 1)[1].split(
            "\n}\n\nrun_typography_visual()", 1
        )[0]
        build = function.index('"${build_command[@]}"')
        post_head = function.index('post_restore_head_sha="$(git rev-parse HEAD)"', build)
        post_worktree = function.index('post_restore_worktree_hash="$(' , post_head)
        self.assertLess(build, post_head)
        self.assertLess(post_head, post_worktree)
        self.assertNotIn('post_restore_worktree_hash="$worktree_diff_hash"', function)

    def test_runner_uses_uuid_scoped_exclusive_attempt_directories(self) -> None:
        source = (REPO_ROOT / "run_tests.sh").read_text(encoding="utf-8")
        function = source.split("run_gameplay_performance() {", 1)[1].split(
            "\n}\n\nrun_typography_visual()", 1
        )[0]
        self.assertIn('attempt_uuid="$(tr -d', function)
        self.assertIn('campaign_id="cleanup-s3a-$timestamp-$attempt_uuid"', function)
        self.assertIn('evidence_dir="$GAMEPLAY_PERFORMANCE_EVIDENCE_ROOT/$campaign_id"', function)
        self.assertIn('if ! mkdir -- "$evidence_dir"; then', function)
        self.assertIn('if ! mkdir -- "$build_dir"; then', function)
        self.assertNotIn('mkdir -p "$evidence_dir" "$build_dir"', function)

    def test_runner_installs_attempt_wide_finalizer_and_reads_every_early_hold(self) -> None:
        source = (REPO_ROOT / "run_tests.sh").read_text(encoding="utf-8")
        function = source.split("run_gameplay_performance() {", 1)[1].split(
            "\n}\n\nrun_typography_visual()", 1
        )[0]
        self.assertIn("gameplay_performance_attempt_return_guard", function)
        self.assertIn(" RETURN", function)
        self.assertIn(" ERR", function)
        self.assertIn(" INT", function)
        self.assertIn(" TERM", function)
        self.assertNotIn("return 130", function)
        self.assertNotIn("return 143", function)
        self.assertGreaterEqual(function.count("trap - RETURN ERR INT TERM;"), 4)
        self.assertGreaterEqual(function.count("gameplay_performance_attempt_return_guard"), 4)
        self.assertIn('if [ "$attempt_manifest_ready" -ne 1 ]; then return 2; fi', function)
        self.assertGreaterEqual(function.count("gameplay_performance_terminal_exit_from_manifest"), 2)
        self.assertGreaterEqual(
            function.count("require_gameplay_performance_hold_manifest"),
            8,
        )

    def test_runner_preserves_admission_infrastructure_exit_class(self) -> None:
        source = (REPO_ROOT / "run_tests.sh").read_text(encoding="utf-8")
        function = source.split("run_gameplay_performance() {", 1)[1].split(
            "\n}\n\nrun_typography_visual()", 1
        )[0]
        for status in (
            "performance_admission_status",
            "cleanup_admission_status",
            "cleanup_calibration_status",
        ):
            self.assertIn(f"{status}=$?", function)
        self.assertEqual(
            3,
            function.count("finalize_gameplay_performance_infrastructure_stage"),
        )
        self.assertGreaterEqual(
            function.count('echo "Gameplay performance measurement: HOLD"'),
            3,
        )

    def test_attempt_fallback_strictly_reads_the_finalized_hold(self) -> None:
        source = (REPO_ROOT / "run_tests.sh").read_text(encoding="utf-8")
        helper = source.split("gameplay_performance_attempt_return_guard() {", 1)[1].split(
            "\n}\n\nrequire_gameplay_performance_hold_manifest()", 1
        )[0]
        self.assertIn("--finalize-infrastructure-failure", helper)
        self.assertIn('require_gameplay_performance_hold_manifest "$manifest_path"', helper)
        self.assertNotIn("|| true", helper)

    def test_runner_restores_link_file_and_meta_independently(self) -> None:
        source = (REPO_ROOT / "run_tests.sh").read_text(encoding="utf-8")
        function = source.split("run_gameplay_performance() {", 1)[1].split(
            "\n}\n\nrun_typography_visual()", 1
        )[0]
        self.assertIn("generated_link_file_existed=1", function)
        self.assertIn("generated_link_meta_existed=1", function)
        self.assertNotIn("generated_link_existed", function)

    def test_build_player_and_marker_failures_finalize_with_downstream_not_run(self) -> None:
        script = REPO_ROOT / "Tools" / "gameplay_cleanup_slice3_evidence_manifest.py"
        cases = (
            ("build", ("preflight",), "HOLD_BUILD_FAILURE", "BUILD_FAILED"),
            ("player", ("preflight", "build", "guardRestore"), "HOLD_PLAYER_FAILURE", "PLAYER_FAILED"),
            ("markerValidation", ("preflight", "build", "guardRestore", "player"), "HOLD_MARKER_FAILURE", "MARKER_VALIDATION_FAILED"),
        )
        for failing_stage, passed_stages, verdict, code in cases:
            with self.subTest(stage=failing_stage), tempfile.TemporaryDirectory() as temporary_directory:
                root = Path(temporary_directory)
                preflight = root / "preflight.txt"
                preflight.write_text("fixture\n", encoding="utf-8")
                output = root / "attempt.json"
                self._initialize_attempt(script, root, output, preflight)
                for stage in passed_stages:
                    self._manifest_call(
                        script, "--transition-manifest", "--manifest", str(output),
                        "--stage", stage, "--stage-status", "PASS", "--output", str(output),
                    )
                self._manifest_call(
                    script, "--transition-manifest", "--manifest", str(output),
                    "--stage", failing_stage, "--stage-status", "HOLD", "--reason-code", code,
                    "--output", str(output),
                )
                self._manifest_call(
                    script, "--finalize-lifecycle", "--manifest", str(output),
                    "--terminal-status", "HOLD", "--authoritative-verdict", verdict,
                    "--output", str(output),
                )

                document = json.loads(output.read_text(encoding="utf-8"))
                self.assertEqual("FINAL", document["manifestState"])
                self.assertEqual("HOLD", document["terminalStatus"])
                self.assertEqual(verdict, document["authoritativeVerdict"])
                ordered_stages = (
                    "preflight", "build", "guardRestore", "player", "markerValidation",
                    "performanceAdmission", "cleanupAdmission", "calibration", "consistencyFinalization",
                )
                failure_index = ordered_stages.index(failing_stage)
                for downstream in ordered_stages[failure_index + 1:]:
                    self.assertEqual("NOT_RUN", document["stages"][downstream]["status"])
                self.assertEqual("PRESENT", document["artifacts"]["preflightManifest"]["state"])
                self.assertEqual("MISSING", document["artifacts"]["metrics"]["state"])

                readback = subprocess.run(
                    [
                        sys.executable,
                        str(script),
                        "--print-terminal-status",
                        "--manifest",
                        str(output),
                    ],
                    cwd=REPO_ROOT,
                    check=False,
                    capture_output=True,
                    text=True,
                )
                self.assertEqual(0, readback.returncode, readback.stderr)
                self.assertEqual("HOLD", readback.stdout.strip())

    def test_terminal_readback_rejects_duplicate_json_members(self) -> None:
        script = REPO_ROOT / "Tools" / "gameplay_cleanup_slice3_evidence_manifest.py"
        with tempfile.TemporaryDirectory() as temporary_directory:
            manifest = Path(temporary_directory) / "duplicate.json"
            manifest.write_text(
                '{"terminalStatus":"HOLD","terminalStatus":"PASS"}\n',
                encoding="utf-8",
            )
            completed = subprocess.run(
                [sys.executable, str(script), "--print-terminal-status", "--manifest", str(manifest)],
                cwd=REPO_ROOT,
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertEqual(1, completed.returncode)
            self.assertIn("JSON_DUPLICATE_MEMBER", completed.stdout)

    def test_terminal_readback_rejects_forged_empty_pass_lifecycle(self) -> None:
        stages = {
            name: {"status": "NOT_RUN", "reasons": [], "artifacts": []}
            for name in (
                "preflight", "build", "guardRestore", "player", "markerValidation",
                "performanceAdmission", "cleanupAdmission", "calibration",
                "consistencyFinalization",
            )
        }
        forged = {
            "schemaVersion": 4,
            "evidenceContractVersion": 4,
            "manifestState": "FINAL",
            "terminalStatus": "PASS",
            "authoritativeVerdict": "READY",
            "identity": {},
            "reasons": [],
            "stages": stages,
            "artifacts": {},
            "exitStatus": {},
        }

        with self.assertRaises(EvidenceError):
            validate_final_manifest_transport(forged)

    def test_terminal_readback_rejects_incoherent_hold_truth(self) -> None:
        script = REPO_ROOT / "Tools" / "gameplay_cleanup_slice3_evidence_manifest.py"
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            preflight = root / "preflight.txt"
            preflight.write_text("fixture\n", encoding="utf-8")
            manifest = root / "attempt.json"
            self._initialize_attempt(script, root, manifest, preflight)
            self._manifest_call(
                script, "--transition-manifest", "--manifest", str(manifest),
                "--stage", "preflight", "--stage-status", "PASS", "--output", str(manifest),
            )
            self._manifest_call(
                script, "--transition-manifest", "--manifest", str(manifest),
                "--stage", "build", "--stage-status", "HOLD", "--reason-code", "BUILD_FAILED",
                "--output", str(manifest),
            )
            self._manifest_call(
                script, "--finalize-lifecycle", "--manifest", str(manifest),
                "--terminal-status", "HOLD", "--authoritative-verdict", "HOLD_BUILD_FAILURE",
                "--output", str(manifest),
            )
            valid = json.loads(manifest.read_text(encoding="utf-8"))
            self.assertEqual("HOLD", validate_final_manifest_transport(valid))

            mutations = []
            wrong_reason = copy.deepcopy(valid)
            wrong_reason["reasons"][0]["code"] = "PLAYER_FAILED"
            wrong_reason["stages"]["build"]["reasons"] = copy.deepcopy(wrong_reason["reasons"])
            mutations.append(wrong_reason)
            extra_identity = copy.deepcopy(valid)
            extra_identity["identity"]["unexpected"] = "value"
            mutations.append(extra_identity)
            extra_artifact = copy.deepcopy(valid)
            extra_artifact["artifacts"]["bogus"] = copy.deepcopy(valid["artifacts"]["preflightManifest"])
            mutations.append(extra_artifact)
            bad_exit = copy.deepcopy(valid)
            bad_exit["exitStatus"] = {"unexpected": 1}
            mutations.append(bad_exit)
            unbound_reason = copy.deepcopy(valid)
            unbound_reason["reasons"].append(
                {"code": "CLEANUP_REJECTED", "path": "extra", "expected": None, "observed": None}
            )
            mutations.append(unbound_reason)
            for forged in mutations:
                with self.assertRaises(EvidenceError):
                    validate_final_manifest_transport(forged)

    def test_infrastructure_finalizer_closes_first_pending_stage(self) -> None:
        script = REPO_ROOT / "Tools" / "gameplay_cleanup_slice3_evidence_manifest.py"
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            preflight = root / "preflight.txt"
            preflight.write_text("fixture\n", encoding="utf-8")
            manifest = root / "attempt.json"
            self._initialize_attempt(script, root, manifest, preflight)
            self._manifest_call(
                script,
                "--transition-manifest",
                "--manifest",
                str(manifest),
                "--stage",
                "preflight",
                "--stage-status",
                "PASS",
                "--output",
                str(manifest),
            )
            self._manifest_call(
                script,
                "--finalize-infrastructure-failure",
                "--manifest",
                str(manifest),
                "--output",
                str(manifest),
            )

            document = json.loads(manifest.read_text(encoding="utf-8"))
            self.assertEqual("FINAL", document["manifestState"])
            self.assertEqual("HOLD", document["terminalStatus"])
            self.assertEqual("PASS", document["stages"]["preflight"]["status"])
            self.assertEqual("HOLD", document["stages"]["build"]["status"])
            self.assertEqual("FINAL_MANIFEST_UNAVAILABLE", document["reasons"][0]["code"])

    @staticmethod
    def _initialize_attempt(
        script: Path, root: Path, manifest: Path, preflight: Path
    ) -> None:
        tool_root = REPO_ROOT / "Tools"
        zero_hash = "0" * 64
        identity = {
            "campaignId": "fixture",
            "attemptId": "attempt-1",
            "attemptOrdinal": 1,
            "attemptKind": "calibration",
            "captureNonce": "fixture-nonce",
            "stage": "S3-A",
            "activeStrategies": ["A"],
            "preBuildHeadSha": "0" * 40,
            "preBuildWorktreeSha256": zero_hash,
            "postRestoreHeadSha": None,
            "postRestoreWorktreeSha256": None,
            "runtimeTreeSha256": zero_hash,
            "playerArtifactSha256": None,
            "buildPayloadSha256": None,
            "runnerSha256": zero_hash,
            "performanceValidatorSha256": zero_hash,
            "cleanupValidatorSha256": zero_hash,
            "aggregatorSha256": zero_hash,
            "manifestToolSha256": zero_hash,
            "workloadContractSha256": zero_hash,
            "harnessSha256": zero_hash,
            "metricsRevision": None,
            "metricsSha256": None,
            "workloadIds": [],
            "orderedRunKeys": [],
        }
        artifacts = {
            "preflightManifest": preflight,
            "buildLog": root / "build.log",
            "playerArtifact": root / "VectorQuake.exe",
            "artifactManifest": root / "artifact-manifest.txt",
            "runtimeLog": root / "runtime.log",
            "metrics": root / "metrics.json",
            "performanceAdmission": root / "performance.json",
            "cleanupAdmission": root / "cleanup.json",
            "cleanupCalibration": root / "calibration.json",
            "performanceValidator": tool_root / "gameplay_performance_admission.py",
            "cleanupValidator": tool_root / "gameplay_cleanup_slice3_admission.py",
            "aggregator": tool_root / "gameplay_cleanup_slice3_calibration.py",
            "manifestTool": tool_root / "gameplay_cleanup_slice3_evidence_manifest.py",
            "workloadContract": tool_root / "contracts" / "gameplay_cleanup_slice3_workloads_v2.json",
            "runner": REPO_ROOT / "run_tests.sh",
        }
        for field, artifact_name in {
            "runnerSha256": "runner",
            "performanceValidatorSha256": "performanceValidator",
            "cleanupValidatorSha256": "cleanupValidator",
            "aggregatorSha256": "aggregator",
            "manifestToolSha256": "manifestTool",
            "workloadContractSha256": "workloadContract",
        }.items():
            identity[field] = hashlib.sha256(artifacts[artifact_name].read_bytes()).hexdigest()
        arguments = [
            str(script), "--initialize-provisional", "--identity-json", json.dumps(identity),
        ]
        for name, path in artifacts.items():
            arguments.extend(("--artifact-path", f"{name}={path}"))
        arguments.extend(("--output", str(manifest)))
        completed = subprocess.run(
            [sys.executable, *arguments], cwd=REPO_ROOT, check=False,
            capture_output=True, text=True,
        )
        if completed.returncode != 0:
            raise AssertionError(completed.stdout + completed.stderr)

    @staticmethod
    def _manifest_call(script: Path, *arguments: str) -> None:
        completed = subprocess.run(
            [sys.executable, str(script), *arguments],
            cwd=REPO_ROOT,
            check=False,
            capture_output=True,
            text=True,
        )
        if completed.returncode != 0:
            raise AssertionError(completed.stdout + completed.stderr)


if __name__ == "__main__":
    unittest.main()
