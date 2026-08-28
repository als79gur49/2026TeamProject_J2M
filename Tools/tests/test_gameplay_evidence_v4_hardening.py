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
    sha256,
    validate_attempt_identity,
    validate_runtime_marker,
)


class GameplayEvidenceV4HardeningTests(unittest.TestCase):
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
                "GAMEPLAY_PERFORMANCE:PASS\nGAMEPLAY_PERFORMANCE:PASS\n",
                "GAMEPLAY_PERFORMANCE:PASS\nGAMEPLAY_PERFORMANCE:FAIL\n",
            ):
                with self.subTest(text=text):
                    log.write_text(text, encoding="utf-8")
                    self.assertTrue(validate_runtime_marker(log))

            log.write_text("GAMEPLAY_PERFORMANCE:PASS\n", encoding="utf-8")
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
