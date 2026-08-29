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
    def test_capture_smoke_dry_run_uses_non_official_roots_and_uuid_leaf(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            evidence_root = root / "smoke-evidence"
            build_root = root / "smoke-build"
            completed = subprocess.run(
                ["bash", "-c", (
                    'CLEANUP_S3_CAPTURE_SMOKE_EVIDENCE_ROOT="$1" '
                    'CLEANUP_S3_CAPTURE_SMOKE_BUILD_ROOT="$2" '
                    './run_tests.sh --dry-run cleanup-s3-capture-smoke'
                ), "smoke-probe", str(evidence_root), str(build_root)],
                cwd=REPO_ROOT,
                check=False,
                capture_output=True,
                text=True,
            )

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn(str(evidence_root), completed.stdout)
        self.assertIn(str(build_root), completed.stdout)
        self.assertNotIn("/gameplay-performance/", completed.stdout)

    def test_capture_smoke_has_exact_non_official_terminal_contract(self) -> None:
        source = (REPO_ROOT / "run_tests.sh").read_text(encoding="utf-8")
        function = source.split("run_gameplay_performance() {", 1)[1].split(
            "\n}\n\nrun_typography_visual()", 1
        )[0]

        self.assertIn("validate_cleanup_s3_capture_smoke_terminal", source)
        self.assertIn(
            "Cleanup S3 capture smoke: PASS (non-official; authoritative manifest remains HOLD)",
            function,
        )
        self.assertIn('campaign_id="cleanup-s3-capture-smoke-$attempt_uuid"', function)
        self.assertIn('evidence_dir="$capture_evidence_root/$attempt_uuid"', function)
        self.assertIn('build_dir="$capture_build_root/$attempt_uuid"', function)

    def test_capture_smoke_rejects_an_incomplete_allocation_hold_manifest(self) -> None:
        manifest = {
            "manifestState": "FINAL",
            "terminalStatus": "HOLD",
            "authoritativeVerdict": "HOLD_CLEANUP_ADMISSION",
            "reasons": [{"code": "CLEANUP_REJECTED"}],
        }
        performance = {"verdict": "ADMITTED", "reasons": []}
        cleanup = {
            "verdict": "REJECTED",
            "reasons": [{
                "code": "SEMANTIC_INVARIANT_INVALID",
                "path": "cleanupSlice3Calibration",
                "observed": "ALLOCATION_COUNTER_PROBE_INVALID: expectedAtLeast=4096 observed=0",
            }],
        }
        calibration = {
            "status": "HOLD_INVALID_EVIDENCE",
            "reasons": [{
                "code": "SEMANTIC_INVARIANT_INVALID",
                "path": "cleanupSlice3Calibration",
                "observed": (
                    "S3-A calibration was not admitted: "
                    "ALLOCATION_COUNTER_PROBE_INVALID: expectedAtLeast=4096 observed=0"
                ),
            }],
        }
        metrics = {
            "cleanupSlice3Calibration": {
                "captures": [{
                    "strategy": "A",
                    "workloads": [{
                        "workloadId": "fixture",
                        "oracleParityVerified": True,
                        "runs": [{
                            "runKey": "A/fixture/1",
                            "repetition": 1,
                            "executedTicks": 2,
                            "referenceOracleInvocationCount": 2,
                            "invariantMismatchCount": 0,
                        }],
                    }],
                }],
            },
        }
        script = r'''
export RUN_TESTS_LIBRARY_ONLY=1
source "$1/run_tests.sh"
validate_cleanup_s3_capture_smoke_terminal "$2" "$3" "$4" "$5" "$6" "$7"
'''

        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            paths = []
            for name, value in (
                ("manifest.json", manifest),
                ("metrics.json", metrics),
                ("performance.json", performance),
                ("cleanup.json", cleanup),
                ("calibration.json", calibration),
            ):
                path = root / name
                path.write_text(json.dumps(value), encoding="utf-8")
                paths.append(path)
            official_calibration = root / "official-calibration.json"
            terminal_paths = [*paths[:4], official_calibration, paths[4]]

            rejected_incomplete_manifest = subprocess.run(
                [
                    "bash", "-c", script, "smoke-envelope", str(REPO_ROOT),
                    *map(str, terminal_paths),
                ],
                cwd=REPO_ROOT,
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertNotEqual(0, rejected_incomplete_manifest.returncode)

            cleanup["reasons"].append({"code": "IDENTITY_MISMATCH"})
            paths[3].write_text(json.dumps(cleanup), encoding="utf-8")
            rejected = subprocess.run(
                [
                    "bash", "-c", script, "smoke-envelope", str(REPO_ROOT),
                    *map(str, terminal_paths),
                ],
                cwd=REPO_ROOT,
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertNotEqual(0, rejected.returncode)

    def test_allocation_smoke_routes_diagnostic_without_advancing_calibration(self) -> None:
        source = (REPO_ROOT / "run_tests.sh").read_text(encoding="utf-8")
        function = source.split("run_gameplay_performance() {", 1)[1].split(
            "\n}\n\nrun_typography_visual()", 1
        )[0]
        allocation_branch = function.split(
            '"$cleanup_allocation_diagnostic_path" calibration; then', 1
        )[1].split('elif [ "$cleanup_allocation_only" -eq 1 ]; then', 1)[0]

        self.assertIn("--allocation-diagnostic", function)
        self.assertIn('calibration_validation_path="$cleanup_allocation_diagnostic_path"', function)
        self.assertIn('"$cleanup_calibration_report_path"', function)
        self.assertIn('"$cleanup_allocation_diagnostic_path"', function)
        self.assertIn("--stage cleanupAdmission --stage-status HOLD", allocation_branch)
        self.assertNotIn("--stage calibration", allocation_branch)
        self.assertNotIn("--finalize-lifecycle", allocation_branch)

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
        self.assertIn('capture_evidence_root="$GAMEPLAY_PERFORMANCE_EVIDENCE_ROOT"', function)
        self.assertIn('evidence_dir="$capture_evidence_root/$campaign_id"', function)
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
        self.assertIn("return 130", function)
        self.assertIn("return 143", function)
        self.assertIn("trap '' INT TERM", source)
        self.assertGreaterEqual(function.count("trap - RETURN ERR INT TERM;"), 4)
        self.assertGreaterEqual(function.count("gameplay_performance_attempt_return_guard"), 2)
        self.assertGreaterEqual(function.count("gameplay_performance_signal_return_guard"), 2)
        self.assertIn('if [ "$attempt_manifest_ready" -ne 1 ]; then', function)
        self.assertGreaterEqual(function.count("gameplay_performance_terminal_exit_from_manifest"), 1)
        self.assertGreaterEqual(
            function.count("require_gameplay_performance_hold_manifest"),
            8,
        )

    def test_attempt_guard_rejects_and_cleans_owned_process_survivors(self) -> None:
        source = (REPO_ROOT / "run_tests.sh").read_text(encoding="utf-8")
        helper = source.split("gameplay_performance_attempt_return_guard() {", 1)[1].split(
            "\n}\n\ngameplay_performance_signal_return_guard()", 1
        )[0]
        self.assertIn('wait_for_terminal_player_exit "$player_path_win"', helper)
        self.assertIn('terminate_terminal_player_processes "$player_path_win"', helper)
        self.assertIn("gameplay_performance_wait_for_no_unity_processes", helper)
        self.assertIn("terminate_current_project_unity_processes", helper)

    def test_unexpected_err_cannot_be_converted_to_smoke_success(self) -> None:
        source = (REPO_ROOT / "run_tests.sh").read_text(encoding="utf-8")
        function = source.split("run_gameplay_performance() {", 1)[1].split(
            "\n}\n\nrun_typography_visual()", 1
        )[0]
        err_traps = [
            line.strip()
            for line in function.splitlines()
            if line.strip().startswith("trap '") and line.rstrip().endswith(" ERR")
        ]
        self.assertGreaterEqual(len(err_traps), 2)
        self.assertTrue(all("return 2" in line for line in err_traps))

    def test_success_line_follows_explicit_final_process_inventory(self) -> None:
        source = (REPO_ROOT / "run_tests.sh").read_text(encoding="utf-8")
        function = source.split("run_gameplay_performance() {", 1)[1].split(
            "\n}\n\nrun_typography_visual()", 1
        )[0]
        manifest_write = function.index('} > "$evidence_dir/manifest.txt"')
        final_inventory = function.index(
            "gameplay_performance_attempt_return_guard", manifest_write
        )
        success_line = function.index('echo "$terminal_message"', final_inventory)
        self.assertLess(manifest_write, final_inventory)
        self.assertLess(final_inventory, success_line)

    def test_all_cleanup_entry_paths_ignore_repeated_signals(self) -> None:
        source = (REPO_ROOT / "run_tests.sh").read_text(encoding="utf-8")
        ignored_signal_traps = source.count("trap '' INT TERM") + source.count(
            'trap "" INT TERM'
        )
        self.assertGreaterEqual(ignored_signal_traps, 3)

    def test_smoke_verifier_is_single_process_atomic_and_transport_strict(self) -> None:
        source = (REPO_ROOT / "run_tests.sh").read_text(encoding="utf-8")
        helper = source.split("validate_cleanup_s3_capture_smoke_terminal() {", 1)[1].split(
            "\n}\n\nis_cleanup_s3_capture_smoke_allocation_only_report()", 1
        )[0]
        self.assertNotIn("--print-terminal-status", helper)
        self.assertIn("validate_final_manifest_transport(manifest)", helper)
        self.assertIn("capture_input_snapshots", helper)
        self.assertIn("validate_input_snapshots", helper)

    def test_process_inventory_queries_fail_closed(self) -> None:
        source = (REPO_ROOT / "run_tests.sh").read_text(encoding="utf-8")
        player_query = source.split("terminal_player_windows_pids() {", 1)[1].split(
            "\n}\n\nwait_for_terminal_player_exit()", 1
        )[0]
        self.assertNotIn("|| true", player_query)
        self.assertIn("command -v powershell.exe", player_query)
        self.assertIn("ExecutablePath", player_query)
        self.assertIn("throw", player_query)
        unity_query = source.split(
            "visual_guard_iter_windows_unity_process_records() {", 1
        )[1].split("\n}\n\nvisual_guard_iter_unity_process_records()", 1)[0]
        self.assertIn("Unity process command line is unavailable", unity_query)
        self.assertIn("Unity process command line parsing failed", unity_query)
        self.assertIn("Unity project path is indeterminate", unity_query)

    def test_final_cleanup_latches_first_signal_and_waits_for_unity_quiet_period(self) -> None:
        source = (REPO_ROOT / "run_tests.sh").read_text(encoding="utf-8")
        function = source.split("run_gameplay_performance() {", 1)[1].split(
            "\n}\n\nrun_typography_visual()", 1
        )[0]
        self.assertIn("final_signal_status=130", function)
        self.assertIn("final_signal_status=143", function)
        self.assertEqual(
            2,
            function.count('if [ "$final_signal_status" -eq 0 ]; then'),
        )
        self.assertIn('return "$final_signal_status"', function)
        guard = source.split("gameplay_performance_attempt_return_guard() {", 1)[1].split(
            "\n}\n\ngameplay_performance_signal_return_guard()", 1
        )[0]
        self.assertIn("gameplay_performance_wait_for_no_unity_processes", guard)
        quiet = source.split("gameplay_performance_wait_for_no_unity_processes() {", 1)[1].split(
            "\n}\n\nfind_current_project_windows_unity_processes()", 1
        )[0]
        self.assertIn("grace_deadline_ms", quiet)
        self.assertIn("quiet_started_at_ms", quiet)

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
            "\n}\n\ngameplay_performance_signal_return_guard()", 1
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
