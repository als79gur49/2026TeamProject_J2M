from __future__ import annotations

import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from Tools.gameplay_evidence_v4 import (
    APPROVED_CLEANUP_S3_WORKLOAD_SHA256,
    EvidenceError,
    atomic_json,
    build_payload_sha256,
    capture_input_snapshots,
    runtime_tree_sha256,
    sha256,
    validate_attempt_identity,
    validate_runtime_marker,
    validate_v4_context_pair,
)


class GameplayEvidenceV4HardeningTests(unittest.TestCase):
    def test_v4_context_pair_accepts_exact_binding_and_rejects_mixed_attempt(self) -> None:
        metrics_sha256 = "d" * 64
        runtime_log_sha256 = "e" * 64
        identity = {
            "campaignId": "fixture-campaign",
            "attemptId": "fixture-attempt",
            "attemptOrdinal": 1,
            "attemptKind": "calibration",
            "captureNonce": "fixture-nonce",
            "stage": "S3-A",
            "activeStrategies": ["A"],
            "preBuildHeadSha": "1" * 40,
            "preBuildWorktreeSha256": "2" * 64,
            "postRestoreHeadSha": "1" * 40,
            "postRestoreWorktreeSha256": "2" * 64,
            "runtimeTreeSha256": runtime_tree_sha256("1" * 40, "2" * 64),
            "playerArtifactSha256": "4" * 64,
            "buildPayloadSha256": "5" * 64,
            "runnerSha256": "6" * 64,
            "performanceValidatorSha256": "7" * 64,
            "cleanupValidatorSha256": "8" * 64,
            "aggregatorSha256": "9" * 64,
            "manifestToolSha256": "a" * 64,
            "workloadContractSha256": "b" * 64,
            "harnessSha256": "c" * 64,
        }
        shared = {
            "CampaignId": identity["campaignId"],
            "AttemptId": identity["attemptId"],
            "AttemptOrdinal": str(identity["attemptOrdinal"]),
            "AttemptKind": identity["attemptKind"],
            "CaptureNonce": identity["captureNonce"],
            "Stage": identity["stage"],
            "ActiveStrategies": "A",
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
            "ExpectedWidth": "1280",
            "ExpectedHeight": "720",
            "ExpectedWarmupFrames": "12",
            "ExpectedSampleFrames": "24",
            "ExpectedTickInterval": "2",
        }

        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            preflight = root / "preflight.txt"
            captured = root / "captured.txt"

            def write_context(
                path: Path,
                phase: str,
                values: dict[str, str],
                *,
                git_status: str = "",
                extra: dict[str, str] | None = None,
            ) -> None:
                document = {
                    "SchemaVersion": "2",
                    "EvidenceContractVersion": "4",
                    "EvidencePhase": phase,
                    **values,
                }
                if phase == "artifact-captured":
                    document["PostRestoreHeadSha"] = identity["postRestoreHeadSha"]
                    document["PostRestoreWorktreeSha256"] = identity["postRestoreWorktreeSha256"]
                    document["PlayerArtifactSha256"] = identity["playerArtifactSha256"]
                    document["BuildPayloadSHA256"] = identity["buildPayloadSha256"]
                    document["MetricsSHA256"] = metrics_sha256
                    document["RuntimeLogSHA256"] = runtime_log_sha256
                document.update(extra or {})
                path.write_text(
                    "".join(f"{key}={value}\n" for key, value in document.items())
                    + "GitStatusShort:\n"
                    + git_status,
                    encoding="utf-8",
                )

            write_context(preflight, "preflight", shared)
            write_context(captured, "artifact-captured", shared)
            self.assertEqual(
                [],
                validate_v4_context_pair(
                    preflight,
                    captured,
                    identity,
                    metrics_sha256=metrics_sha256,
                ),
            )

            mixed = dict(shared)
            mixed["AttemptId"] = "different-attempt"
            write_context(captured, "artifact-captured", mixed)
            issues = validate_v4_context_pair(
                preflight,
                captured,
                identity,
                metrics_sha256=metrics_sha256,
            )

        self.assertIn("IDENTITY_MISMATCH", {value["code"] for value in issues})

    def test_v4_context_pair_rejects_extra_key_git_status_drift_and_forged_hashes(self) -> None:
        metrics_sha256 = "d" * 64
        identity = {
            "campaignId": "fixture-campaign",
            "attemptId": "fixture-attempt",
            "attemptOrdinal": 1,
            "attemptKind": "calibration",
            "captureNonce": "fixture-nonce",
            "stage": "S3-A",
            "activeStrategies": ["A"],
            "preBuildHeadSha": "1" * 40,
            "preBuildWorktreeSha256": "2" * 64,
            "postRestoreHeadSha": "1" * 40,
            "postRestoreWorktreeSha256": "2" * 64,
            "runtimeTreeSha256": runtime_tree_sha256("1" * 40, "2" * 64),
            "playerArtifactSha256": "4" * 64,
            "buildPayloadSha256": "5" * 64,
            "runnerSha256": "6" * 64,
            "performanceValidatorSha256": "7" * 64,
            "cleanupValidatorSha256": "8" * 64,
            "aggregatorSha256": "9" * 64,
            "manifestToolSha256": "a" * 64,
            "workloadContractSha256": "b" * 64,
            "harnessSha256": "c" * 64,
        }
        shared = {
            "CampaignId": identity["campaignId"],
            "AttemptId": identity["attemptId"],
            "AttemptOrdinal": "1",
            "AttemptKind": identity["attemptKind"],
            "CaptureNonce": identity["captureNonce"],
            "Stage": identity["stage"],
            "ActiveStrategies": "A",
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
            "ExpectedWidth": "1280",
            "ExpectedHeight": "720",
            "ExpectedWarmupFrames": "12",
            "ExpectedSampleFrames": "24",
            "ExpectedTickInterval": "2",
        }

        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            preflight = root / "preflight.txt"
            captured = root / "captured.txt"

            def write(path: Path, phase: str, values: dict[str, str], status: str) -> None:
                path.write_text(
                    "".join(
                        f"{key}={value}\n"
                        for key, value in {
                            "SchemaVersion": "2",
                            "EvidenceContractVersion": "4",
                            "EvidencePhase": phase,
                            **values,
                        }.items()
                    )
                    + "GitStatusShort:\n"
                    + status,
                    encoding="utf-8",
                )

            write(preflight, "preflight", shared, " M expected.txt\n")
            write(
                captured,
                "artifact-captured",
                {
                    **shared,
                    "PostRestoreHeadSha": identity["postRestoreHeadSha"],
                    "PostRestoreWorktreeSha256": identity["postRestoreWorktreeSha256"],
                    "PlayerArtifactSha256": "f" * 64,
                    "BuildPayloadSHA256": "f" * 64,
                    "MetricsSHA256": "f" * 64,
                    "RuntimeLogSHA256": "f" * 64,
                    "UnexpectedKey": "forged",
                },
                " M forged.txt\n",
            )
            issues = validate_v4_context_pair(
                preflight,
                captured,
                identity,
                metrics_sha256=metrics_sha256,
            )

        codes = {value["code"] for value in issues}
        self.assertIn("FIELD_UNEXPECTED", codes)
        self.assertIn("IDENTITY_MISMATCH", codes)
        self.assertIn("PLAYER_ARTIFACT_HASH_MISMATCH", codes)
        self.assertIn("BUILD_PAYLOAD_HASH_MISMATCH", codes)
        self.assertIn("METRICS_HASH_MISMATCH", codes)

    def test_atomic_json_runs_pre_replace_check_before_persistent_mutation(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            output = Path(temporary_directory) / "output.json"
            calls = []

            def reject_changed_live_identity() -> None:
                calls.append("checked")
                raise EvidenceError(
                    "PRE_POST_WORKTREE_MISMATCH",
                    "live.worktreeSha256",
                    "validated",
                    "mutated",
                )

            with self.assertRaises(EvidenceError) as context:
                atomic_json(
                    output,
                    {"derived": 1},
                    pre_replace_check=reject_changed_live_identity,
                )

            self.assertEqual(["checked"], calls)
            self.assertIn("PRE_POST_WORKTREE_MISMATCH", str(context.exception))
            self.assertFalse(output.exists())

    def test_atomic_json_rejects_input_changed_after_validation_snapshot(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            source = root / "source.json"
            output = root / "output.json"
            source.write_text('{"value":1}\n', encoding="utf-8")
            snapshots = capture_input_snapshots((source,))

            source.write_text('{"value":2}\n', encoding="utf-8")

            with self.assertRaises(EvidenceError) as context:
                atomic_json(
                    output,
                    {"derived": 1},
                    inputs=(source,),
                    expected_input_snapshots=snapshots,
                )
            self.assertIn("INPUT_MUTATED_DURING_VALIDATION", str(context.exception))
            serialized_reason = json.loads(json.dumps(context.exception.reason))
            self.assertEqual(str(source), serialized_reason["path"])
            self.assertEqual(64, len(serialized_reason["expected"]["sha256"]))
            self.assertNotEqual(
                serialized_reason["expected"]["sha256"],
                serialized_reason["observed"]["sha256"],
            )
            self.assertIsInstance(serialized_reason["observed"], dict)
            self.assertFalse(output.exists())

    def test_atomic_json_rejects_symlink_retarget_after_validation_snapshot(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            first = root / "first.json"
            second = root / "second.json"
            source = root / "source.json"
            output = root / "output.json"
            first.write_text('{"value":1}\n', encoding="utf-8")
            second.write_text('{"value":1}\n', encoding="utf-8")
            source.symlink_to(first)
            snapshots = capture_input_snapshots((source,))

            source.unlink()
            source.symlink_to(second)

            with self.assertRaises(EvidenceError) as context:
                atomic_json(
                    output,
                    {"derived": 1},
                    inputs=(source,),
                    expected_input_snapshots=snapshots,
                )
            self.assertIn("INPUT_MUTATED_DURING_VALIDATION", str(context.exception))
            self.assertFalse(output.exists())

    def test_approved_workload_contract_digest_is_pinned(self) -> None:
        repository_root = Path(__file__).resolve().parents[2]
        workload_contract = (
            repository_root
            / "Tools"
            / "contracts"
            / "gameplay_cleanup_slice3_workloads_v2.json"
        )

        self.assertEqual(
            "e48fa8fe4b91f1c165bee9985b55a1ce2d376e17214baeaf9e1a1635e50d41d4",
            APPROVED_CLEANUP_S3_WORKLOAD_SHA256,
        )
        self.assertEqual(APPROVED_CLEANUP_S3_WORKLOAD_SHA256, sha256(workload_contract))

    def test_attempt_identity_rejects_empty_and_unknown_domains(self) -> None:
        identity = {
            "campaignId": "",
            "attemptId": "",
            "attemptOrdinal": 0,
            "attemptKind": "not-a-kind",
            "captureNonce": "",
            "stage": "S3-A",
            "activeStrategies": ["A"],
            "preBuildHeadSha": "not-a-head",
            "preBuildWorktreeSha256": "not-a-sha",
            "postRestoreHeadSha": "not-a-head",
            "postRestoreWorktreeSha256": "not-a-sha",
            "runtimeTreeSha256": "not-a-sha",
            "playerArtifactSha256": "not-a-sha",
            "buildPayloadSha256": "not-a-sha",
            "runnerSha256": "not-a-sha",
            "performanceValidatorSha256": "not-a-sha",
            "cleanupValidatorSha256": "not-a-sha",
            "aggregatorSha256": "not-a-sha",
            "manifestToolSha256": "not-a-sha",
            "workloadContractSha256": "not-a-sha",
            "harnessSha256": "not-a-sha",
        }

        codes = {value["code"] for value in validate_attempt_identity(identity)}

        self.assertIn("IDENTITY_FIELD_INVALID", codes)
        self.assertIn("NUMERIC_DOMAIN_INVALID", codes)
        self.assertIn("SHA_FORMAT_INVALID", codes)

    def test_runtime_marker_requires_exactly_one_pass_and_no_fail(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            log = Path(temporary_directory) / "runtime.log"
            for text in (
                "",
                "GAMEPLAY_PERFORMANCE:FAIL\n",
                "GAMEPLAY_PERFORMANCE:FAIL diagnostic\n",
                "prefix GAMEPLAY_PERFORMANCE:PASS resolution=1280x720\n",
                "GAMEPLAY_PERFORMANCE:PASSING\n",
                "GAMEPLAY_PERFORMANCE:PASS\nGAMEPLAY_PERFORMANCE:PASS\n",
                "GAMEPLAY_PERFORMANCE:PASS resolution=1280x720\nGAMEPLAY_PERFORMANCE:PASS\n",
                "GAMEPLAY_PERFORMANCE:PASS\nGAMEPLAY_PERFORMANCE:FAIL\n",
            ):
                with self.subTest(text=text):
                    log.write_text(text, encoding="utf-8")
                    self.assertTrue(validate_runtime_marker(log))

            log.write_text("GAMEPLAY_PERFORMANCE:PASS\n", encoding="utf-8")
            self.assertEqual([], validate_runtime_marker(log))
            log.write_text(
                "GAMEPLAY_PERFORMANCE:PASS resolution=1280x720 idleFrames=24 "
                "gameplayFrames=24 executedTicks=12\n",
                encoding="utf-8",
            )
            self.assertEqual([], validate_runtime_marker(log))

    def test_build_payload_requires_existing_nonempty_directory(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            missing = root / "missing"
            empty = root / "empty"
            empty.mkdir()

            for path in (missing, empty):
                with self.subTest(path=path), self.assertRaises(EvidenceError):
                    build_payload_sha256(path)

    def test_lifecycle_cli_cannot_finalize_pass_from_empty_provisional(self) -> None:
        repository_root = Path(__file__).resolve().parents[2]
        script = repository_root / "Tools" / "gameplay_cleanup_slice3_evidence_manifest.py"
        with tempfile.TemporaryDirectory() as temporary_directory:
            manifest = Path(temporary_directory) / "attempt.json"
            initialized = subprocess.run(
                [
                    sys.executable,
                    str(script),
                    "--initialize-provisional",
                    "--identity-json",
                    "{}",
                    "--output",
                    str(manifest),
                ],
                cwd=repository_root,
                check=False,
                capture_output=True,
                text=True,
            )
            finalized = subprocess.run(
                [
                    sys.executable,
                    str(script),
                    "--finalize-lifecycle",
                    "--manifest",
                    str(manifest),
                    "--terminal-status",
                    "PASS",
                    "--authoritative-verdict",
                    "READY",
                    "--output",
                    str(manifest),
                ],
                cwd=repository_root,
                check=False,
                capture_output=True,
                text=True,
            )

            self.assertEqual(0, initialized.returncode, initialized.stderr)
            self.assertNotEqual(0, finalized.returncode, finalized.stdout)
            document = json.loads(manifest.read_text(encoding="utf-8"))
            self.assertEqual("PROVISIONAL", document["manifestState"])
            self.assertEqual("NOT_RUN", document["terminalStatus"])

    def test_provisional_output_cannot_alias_planned_artifact(self) -> None:
        repository_root = Path(__file__).resolve().parents[2]
        script = repository_root / "Tools" / "gameplay_cleanup_slice3_evidence_manifest.py"
        with tempfile.TemporaryDirectory() as temporary_directory:
            artifact = Path(temporary_directory) / "preflight.txt"
            original = b"preserve-me\n"
            artifact.write_bytes(original)

            completed = subprocess.run(
                [
                    sys.executable,
                    str(script),
                    "--initialize-provisional",
                    "--identity-json",
                    "{}",
                    "--artifact-path",
                    f"preflightManifest={artifact}",
                    "--output",
                    str(artifact),
                ],
                cwd=repository_root,
                check=False,
                capture_output=True,
                text=True,
            )

            self.assertNotEqual(0, completed.returncode, completed.stdout)
            self.assertEqual(original, artifact.read_bytes())


if __name__ == "__main__":
    unittest.main()
