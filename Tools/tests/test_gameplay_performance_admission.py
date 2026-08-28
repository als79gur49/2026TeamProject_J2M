#!/usr/bin/env python3

from __future__ import annotations

import copy
import hashlib
import importlib.util
import json
import math
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
VALIDATOR_PATH = REPO_ROOT / "Tools" / "gameplay_performance_admission.py"
SPEC = importlib.util.spec_from_file_location("gameplay_performance_admission", VALIDATOR_PATH)
assert SPEC is not None and SPEC.loader is not None
ADMISSION = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(ADMISSION)

REVISION = "29d26ab18b0023a2a7815786f71dfbd2efd5c083"
CLEAN_DIFF_HASH = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"


def distribution(count: int, p95: float) -> dict[str, float | int]:
    return {
        "count": count,
        "median": p95 * 0.8 if count else 0,
        "p95": p95,
        "p99": p95 * 1.1 if count else 0,
        "maximum": p95 * 1.2 if count else 0,
    }


def long_distribution(count: int, p95: int) -> dict[str, int]:
    if count == 0:
        return {"count": 0, "median": -1, "p95": -1, "p99": -1, "maximum": -1}
    return {"count": count, "median": p95, "p95": p95, "p99": p95, "maximum": p95}


def phase(name: str, *, gameplay: bool) -> dict[str, object]:
    tick_count = 1200 if gameplay else 0
    return {
        "phase": name,
        "sampleCount": 1200,
        "attemptedTicks": tick_count,
        "executedTicks": tick_count,
        "validCpuMainSamples": 1200,
        "validCpuRenderSamples": 1200,
        "validGpuSamples": 1200,
        "validDrawCallSamples": 1200,
        "validGcAllocatedSamples": 0,
        "frameIntervalMilliseconds": distribution(1200, 8.0),
        "cpuMainMilliseconds": distribution(1200, 7.5),
        "cpuRenderMilliseconds": distribution(1200, 2.5),
        "gpuMilliseconds": distribution(1200, 3.5),
        "tickWallMilliseconds": distribution(tick_count, 7.12319 if gameplay else 0),
        "drawCalls": long_distribution(1200, 2042),
        "gcAllocatedBytes": long_distribution(0, -1),
    }


def admitted_metrics() -> dict[str, object]:
    return {
        "schemaVersion": 1,
        "measurementKind": "release-like-player-headroom",
        "budgetVerdict": "NOT_CONFIGURED",
        "revision": REVISION,
        "unityVersion": "6000.3.11f1",
        "developmentBuild": False,
        "productName": "VectorQuake-GameplayPerformance-fixture",
        "operatingSystem": "Windows 11  (10.0.26200)",
        "processorType": "13th Gen Intel(R) Core(TM) i5-13500",
        "processorCount": 20,
        "systemMemorySizeMB": 32539,
        "graphicsDeviceType": "Direct3D11",
        "graphicsDeviceName": "NVIDIA GeForce RTX 4060 Ti",
        "graphicsDeviceVersion": "Direct3D 11.0 [level 11.1]",
        "graphicsMemorySizeMB": 7949,
        "qualityLevel": 0,
        "qualityName": "PC",
        "requestedResolution": [1920, 1080],
        "actualResolution": [1920, 1080],
        "vSyncCount": 0,
        "targetFrameRate": -1,
        "warmupFrames": 120,
        "sampleFramesPerPhase": 1200,
        "gameplayTickIntervalFrames": 1,
        "drawCallsCounterAvailable": True,
        "gcAllocatedCounterAvailable": False,
        "phases": [phase("render-idle", gameplay=False), phase("gameplay-neutral-tick", gameplay=True)],
    }


def v4_metrics() -> dict[str, object]:
    value = admitted_metrics()
    value["schemaVersion"] = 2
    value["evidenceContractVersion"] = 4
    value["captureIdentity"] = {
        "campaignId": "fixture-campaign",
        "attemptId": "fixture-attempt",
        "attemptOrdinal": 1,
        "attemptKind": "calibration",
        "captureNonce": "fixture-nonce",
        "stage": "S3-A",
        "activeStrategies": ["A"],
        "preBuildHeadSha": REVISION,
        "preBuildWorktreeSha256": "1" * 64,
        "postRestoreHeadSha": REVISION,
        "postRestoreWorktreeSha256": "1" * 64,
        "runtimeTreeSha256": "2" * 64,
        "playerArtifactSha256": "3" * 64,
        "buildPayloadSha256": "4" * 64,
        "runnerSha256": "5" * 64,
        "performanceValidatorSha256": "6" * 64,
        "cleanupValidatorSha256": "7" * 64,
        "aggregatorSha256": "8" * 64,
        "manifestToolSha256": "9" * 64,
        "workloadContractSha256": "a" * 64,
        "harnessSha256": "b" * 64,
    }
    value["cleanupSlice3Calibration"] = {}
    return value


def campaign_plan() -> dict[str, object]:
    metrics = admitted_metrics()
    return {
        "schemaVersion": 1,
        "campaignId": "slice1-recovery-fixture",
        "states": {"C2": REVISION},
        "slots": [
            {
                "kind": "official",
                "block": "block-1",
                "slot": "slot-04",
                "state": "C2",
            },
            {
                "kind": "warm-up",
                "block": "warm-up",
                "slot": "warmup-C2",
                "state": "C2",
            },
        ],
        "settings": {
            "metricsSchemaVersion": 1,
            "measurementKind": "release-like-player-headroom",
            "developmentBuild": False,
            "requestedResolution": [1920, 1080],
            "warmupFrames": 120,
            "sampleFramesPerPhase": 1200,
            "gameplayTickIntervalFrames": 1,
            "vSyncCount": 0,
            "targetFrameRate": -1,
        },
        "campaignIdentity": {
            "unityVersion": metrics["unityVersion"],
            "operatingSystem": metrics["operatingSystem"],
            "processorType": metrics["processorType"],
            "processorCount": metrics["processorCount"],
            "systemMemorySizeMB": metrics["systemMemorySizeMB"],
            "graphicsDeviceType": metrics["graphicsDeviceType"],
            "graphicsDeviceName": metrics["graphicsDeviceName"],
            "graphicsDeviceVersion": metrics["graphicsDeviceVersion"],
            "graphicsMemorySizeMB": metrics["graphicsMemorySizeMB"],
            "qualityLevel": metrics["qualityLevel"],
            "qualityName": metrics["qualityName"],
            "graphicsApi": "Direct3D11",
            "backend": "Mono",
            "configuration": "ReleaseLikeCapture",
            "buildOptions": "None",
            "stage": "stage-1-1",
            "sceneRoute": "canonical gameplay shell",
        },
    }


def manifest_text(*, revision: str = REVISION, diff_hash: str = CLEAN_DIFF_HASH) -> str:
    return "\n".join(
        [
            "UTC=20260826T200124Z",
            f"HEAD={revision}",
            "Branch=",
            f"WorktreeDiffSHA256={diff_hash}",
            "WorktreeHashIncludes=tracked-binary-diff+untracked-path-content-sha256",
            "ArtifactSHA256=" + "a" * 64,
            "BuildPayloadSHA256=" + "b" * 64,
            "Player=/mnt/d/J2M/builds/gameplay-performance/fixture/VectorQuake-GameplayPerformance.exe",
            "Stage=stage-1-1",
            "SceneRoute=canonical gameplay shell",
            "Backend=Mono",
            "Configuration=ReleaseLikeCapture",
            "BuildOptions=None",
            "CaptureBuildCapability=present",
            "FrameTimingStats=enabled-for-capture",
            "BuildMutationRestore=PASS",
            "PersistentDataIsolation=unique-product-name",
            "GraphicsApi=Direct3D11",
            "RequestedResolution=1920x1080",
            "WarmupFrames=120",
            "SampleFramesPerPhase=1200",
            "GameplayTickIntervalFrames=1",
            "Metrics=/evidence/performance-metrics.json",
            "GitStatusShort:",
            "",
        ]
    )


class GameplayPerformanceAdmissionTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.metrics_path = self.root / "performance-metrics.json"
        self.manifest_path = self.root / "manifest.txt"
        self.plan_path = self.root / "campaign-plan.json"
        self.record_path = self.root / "admission-record.json"
        self.metrics = admitted_metrics()
        self.plan = campaign_plan()

    def tearDown(self) -> None:
        self.temp.cleanup()

    def write_inputs(self, *, manifest: str | None = None) -> None:
        self.metrics_path.write_text(json.dumps(self.metrics), encoding="utf-8")
        self.manifest_path.write_text(manifest or manifest_text(), encoding="utf-8")
        self.plan_path.write_text(json.dumps(self.plan, sort_keys=True), encoding="utf-8")

    def admit(self, *, manifest: str | None = None) -> dict[str, object]:
        self.write_inputs(manifest=manifest)
        return ADMISSION.admit_run(
            metrics_path=self.metrics_path,
            manifest_path=self.manifest_path,
            campaign_plan_path=self.plan_path,
            planned_revision=REVISION,
            expected_clean_diff_hash=CLEAN_DIFF_HASH,
            campaign_id="slice1-recovery-fixture",
            block="block-1",
            slot="slot-04",
            state="C2",
            kind="official",
            attempt=2,
            record_path=self.record_path,
        )

    def test_admitted_record_contains_run_identity_and_immutable_hashes(self) -> None:
        record = self.admit()

        self.assertEqual("ADMITTED", record["verdict"])
        self.assertEqual("slice1-recovery-fixture", record["campaignId"])
        self.assertEqual("block-1", record["block"])
        self.assertEqual("slot-04", record["slot"])
        self.assertEqual("C2", record["state"])
        self.assertEqual(REVISION, record["plannedRuntimeSha"])
        self.assertEqual("official", record["kind"])
        self.assertEqual(2, record["attempt"])
        self.assertEqual(str(self.metrics_path.resolve()), record["metricsPath"])
        self.assertEqual(str(self.manifest_path.resolve()), record["manifestPath"])
        self.assertEqual(
            hashlib.sha256(VALIDATOR_PATH.read_bytes()).hexdigest(),
            record["validatorSha256"],
        )
        self.assertEqual(
            hashlib.sha256(self.plan_path.read_bytes()).hexdigest(),
            record["campaignPlanSha256"],
        )
        self.assertEqual(record, json.loads(self.record_path.read_text(encoding="utf-8")))

    def test_formal_record_cannot_alias_metrics_input(self) -> None:
        self.write_inputs()
        original = self.metrics_path.read_bytes()

        with self.assertRaises(ADMISSION.EvidenceError):
            ADMISSION.admit_run(
                metrics_path=self.metrics_path,
                manifest_path=self.manifest_path,
                campaign_plan_path=self.plan_path,
                planned_revision=REVISION,
                expected_clean_diff_hash=CLEAN_DIFF_HASH,
                campaign_id="slice1-recovery-fixture",
                block="block-1",
                slot="slot-04",
                state="C2",
                kind="official",
                attempt=2,
                record_path=self.metrics_path,
            )

        self.assertEqual(original, self.metrics_path.read_bytes())

    def test_valid_v4_metrics_are_admitted(self) -> None:
        verdict, reasons = ADMISSION.validate_metrics(
            v4_metrics(),
            planned_revision=REVISION,
            expected_width=1920,
            expected_height=1080,
            expected_warmup_frames=120,
            expected_sample_frames=1200,
            expected_tick_interval=1,
        )
        self.assertEqual("ADMITTED", verdict, reasons)

    def test_v4_identity_domains_are_fail_closed(self) -> None:
        value = v4_metrics()
        value["captureIdentity"]["campaignId"] = ""
        value["captureIdentity"]["attemptKind"] = "unknown"
        value["captureIdentity"]["captureNonce"] = ""

        verdict, reasons = ADMISSION.validate_metrics(
            value,
            planned_revision=REVISION,
            expected_width=1920,
            expected_height=1080,
            expected_warmup_frames=120,
            expected_sample_frames=1200,
            expected_tick_interval=1,
        )

        self.assertEqual("REJECTED_IDENTITY", verdict)
        self.assertIn("IDENTITY_FIELD_INVALID", {item["code"] for item in reasons if isinstance(item, dict)})

    def test_v4_ignored_producer_fields_are_fail_closed(self) -> None:
        value = v4_metrics()
        value["budgetVerdict"] = {"wrong": "type"}
        value["unityVersion"] = None
        value["productName"] = []

        verdict, reasons = ADMISSION.validate_metrics(
            value,
            planned_revision=REVISION,
            expected_width=1920,
            expected_height=1080,
            expected_warmup_frames=120,
            expected_sample_frames=1200,
            expected_tick_interval=1,
        )

        self.assertEqual("REJECTED_IDENTITY", verdict)
        self.assertTrue(any(isinstance(item, dict) and item["path"] == "metrics.budgetVerdict" for item in reasons))

    def test_v4_zero_expected_tick_interval_is_rejected_without_exception(self) -> None:
        verdict, reasons = ADMISSION.validate_metrics(
            v4_metrics(),
            planned_revision=REVISION,
            expected_width=1920,
            expected_height=1080,
            expected_warmup_frames=120,
            expected_sample_frames=1200,
            expected_tick_interval=0,
        )

        self.assertEqual("REJECTED_SAMPLE_COUNT", verdict)
        self.assertIn("NUMERIC_DOMAIN_INVALID", {item["code"] for item in reasons if isinstance(item, dict)})

    def test_v4_float_counts_and_nested_extra_fields_are_rejected(self) -> None:
        value = v4_metrics()
        value["phases"][1]["executedTicks"] = 1200.0
        value["phases"][0]["unexpected"] = True

        verdict, reasons = ADMISSION.validate_metrics(
            value,
            planned_revision=REVISION,
            expected_width=1920,
            expected_height=1080,
            expected_warmup_frames=120,
            expected_sample_frames=1200,
            expected_tick_interval=1,
        )

        self.assertEqual("REJECTED_SAMPLE_COUNT", verdict)
        self.assertTrue(any("executedTicks" in reason for reason in reasons), reasons)
        self.assertTrue(any("FIELD_UNEXPECTED" in reason for reason in reasons), reasons)

    def test_metrics_cli_rejects_duplicate_json_member_with_reason_artifact(self) -> None:
        self.metrics = v4_metrics()
        payload = json.dumps(self.metrics).replace(
            '"schemaVersion": 2',
            '"schemaVersion": 999, "schemaVersion": 2',
            1,
        )
        self.metrics_path.write_text(payload, encoding="utf-8")
        output = self.root / "duplicate-report.json"

        completed = subprocess.run(
            [
                sys.executable, str(VALIDATOR_PATH), "metrics", "--metrics", str(self.metrics_path),
                "--planned-revision", REVISION, "--expected-width", "1920", "--expected-height", "1080",
                "--expected-warmup-frames", "120", "--expected-sample-frames", "1200",
                "--expected-tick-interval", "1", "--output", str(output),
            ],
            cwd=REPO_ROOT,
            check=False,
            capture_output=True,
            text=True,
        )

        self.assertEqual(1, completed.returncode)
        report = json.loads(output.read_text(encoding="utf-8"))
        self.assertEqual("JSON_DUPLICATE_MEMBER", report["reasons"][0]["code"])

    def test_metrics_cli_output_alias_returns_exit_two_without_truncation(self) -> None:
        self.metrics_path.write_text(json.dumps(v4_metrics()), encoding="utf-8")
        before = self.metrics_path.read_bytes()

        completed = subprocess.run(
            [
                sys.executable, str(VALIDATOR_PATH), "metrics", "--metrics", str(self.metrics_path),
                "--planned-revision", REVISION, "--expected-width", "1920", "--expected-height", "1080",
                "--expected-warmup-frames", "120", "--expected-sample-frames", "1200",
                "--expected-tick-interval", "1", "--output", str(self.metrics_path),
            ],
            cwd=REPO_ROOT,
            check=False,
            capture_output=True,
            text=True,
        )

        self.assertEqual(2, completed.returncode)
        self.assertEqual(before, self.metrics_path.read_bytes())

    def test_resolution_mismatch_is_rejected_before_p95_admission(self) -> None:
        self.metrics["actualResolution"] = [1080, 1080]
        record = self.admit()
        self.assertEqual("REJECTED_RESOLUTION", record["verdict"])
        self.assertIn("actualResolution", record["reason"])

    def test_each_phase_requires_exact_cpu_gpu_and_sample_counts(self) -> None:
        gameplay = self.metrics["phases"][1]
        gameplay["validGpuSamples"] = 1199
        record = self.admit()
        self.assertEqual("REJECTED_SAMPLE_COUNT", record["verdict"])
        self.assertIn("gameplay-neutral-tick.validGpuSamples", record["reason"])

    def test_exact_two_named_phases_are_required(self) -> None:
        self.metrics["phases"].append(copy.deepcopy(self.metrics["phases"][1]))
        record = self.admit()
        self.assertEqual("REJECTED_SAMPLE_COUNT", record["verdict"])
        self.assertIn("exact phases", record["reason"])

    def test_gameplay_tick_attempt_execution_and_distribution_count_are_exact(self) -> None:
        gameplay = self.metrics["phases"][1]
        gameplay["executedTicks"] = 1199
        gameplay["tickWallMilliseconds"]["count"] = 1199
        record = self.admit()
        self.assertEqual("REJECTED_SAMPLE_COUNT", record["verdict"])
        self.assertIn("executedTicks", record["reason"])

    def test_tick_p95_must_be_finite_positive_number(self) -> None:
        self.metrics["phases"][1]["tickWallMilliseconds"]["p95"] = math.nan
        record = self.admit()
        self.assertEqual("REJECTED_RUNTIME", record["verdict"])
        self.assertIn("JSON_PARSE_FAILED", record["reason"])

    def test_schema_and_capture_settings_are_identity_contracts(self) -> None:
        self.metrics["warmupFrames"] = 121
        record = self.admit()
        self.assertEqual("REJECTED_IDENTITY", record["verdict"])
        self.assertIn("warmupFrames", record["reason"])

    def test_manifest_metrics_plan_revision_and_clean_diff_must_match(self) -> None:
        record = self.admit(manifest=manifest_text(revision="0" * 40, diff_hash="f" * 64))
        self.assertEqual("REJECTED_REVISION", record["verdict"])
        self.assertIn("HEAD", record["reason"])

    def test_hardware_quality_graphics_and_backend_match_campaign_identity(self) -> None:
        self.metrics["graphicsDeviceName"] = "Different GPU"
        record = self.admit()
        self.assertEqual("REJECTED_IDENTITY", record["verdict"])
        self.assertIn("graphicsDeviceName", record["reason"])

    def test_campaign_id_must_match_immutable_plan(self) -> None:
        self.write_inputs()
        record = ADMISSION.admit_run(
            metrics_path=self.metrics_path,
            manifest_path=self.manifest_path,
            campaign_plan_path=self.plan_path,
            planned_revision=REVISION,
            expected_clean_diff_hash=CLEAN_DIFF_HASH,
            campaign_id="different-campaign",
            block="warmup",
            slot="C2",
            state="C2",
            kind="warm-up",
            attempt=1,
            record_path=self.record_path,
        )
        self.assertEqual("REJECTED_IDENTITY", record["verdict"])
        self.assertIn("campaignId", record["reason"])

    def test_kind_block_slot_state_and_revision_must_match_immutable_plan(self) -> None:
        self.write_inputs()
        record = ADMISSION.admit_run(
            metrics_path=self.metrics_path,
            manifest_path=self.manifest_path,
            campaign_plan_path=self.plan_path,
            planned_revision=REVISION,
            expected_clean_diff_hash=CLEAN_DIFF_HASH,
            campaign_id="slice1-recovery-fixture",
            block="block-1",
            slot="slot-04",
            state="unexpected-state",
            kind="official",
            attempt=1,
            record_path=self.record_path,
        )
        self.assertEqual("REJECTED_IDENTITY", record["verdict"])
        self.assertIn("campaign slot state", record["reason"])

    def test_record_hashes_metrics_and_manifest_for_post_admission_immutability(self) -> None:
        record = self.admit()
        self.assertEqual(hashlib.sha256(self.metrics_path.read_bytes()).hexdigest(), record["metricsSha256"])
        self.assertEqual(hashlib.sha256(self.manifest_path.read_bytes()).hexdigest(), record["manifestSha256"])

    def test_runner_uses_structural_metrics_validator_not_global_string_search(self) -> None:
        runner = (REPO_ROOT / "run_tests.sh").read_text(encoding="utf-8")
        function = runner.split("run_gameplay_performance() {", 1)[1].split("\n}\n\nrun_typography_visual()", 1)[0]
        self.assertIn("Tools/gameplay_performance_admission.py", function)
        self.assertIn("metrics", function)
        self.assertNotIn('rg -qF "\\\"validGpuSamples\\\"', function)

    def test_player_waits_for_actual_resolution_before_any_warmup(self) -> None:
        source = (
            REPO_ROOT
            / "Assets/_Features/UI/UI_Composition/Runtime/GameplayPerformancePlayerProbe.cs"
        ).read_text(encoding="utf-8")
        set_resolution = source.index("Screen.SetResolution(width, height, FullScreenMode.Windowed);")
        resolution_check = source.index("Screen.width == width && Screen.height == height", set_resolution)
        timeout_failure = source.index("requested resolution did not become active", resolution_check)
        warmup = source.index("for (var frame = 0; frame < warmupFrames; frame++)")
        self.assertLess(set_resolution, resolution_check)
        self.assertLess(resolution_check, timeout_failure)
        self.assertLess(timeout_failure, warmup)

    def test_player_emits_v4_capture_identity_and_captures_only_cleanup_schema(self) -> None:
        source = (
            REPO_ROOT
            / "Assets/_Features/UI/UI_Composition/Runtime/GameplayPerformancePlayerProbe.cs"
        ).read_text(encoding="utf-8")

        self.assertIn('builder.AppendLine("  \\"schemaVersion\\": 2,")', source)
        self.assertIn('builder.AppendLine("  \\"evidenceContractVersion\\": 4,")', source)
        self.assertIn('"  \\"captureIdentity\\": "', source)
        self.assertIn("BuildCaptureIdentityJson", source)
        self.assertIn("NormalizeCleanupCalibrationSchema2", source)
        self.assertIn('",\\\"captures\\\":[{\\\"strategy\\\":\\\"A\\\",\\\"workloads\\\":["', source)
        self.assertIn('"\\\"runKey\\\":\\\"A/"', source)
        self.assertIn("PostRestoreWorktreeArgument", source)

    def test_runner_passes_every_v4_identity_carrier_to_player(self) -> None:
        runner = (REPO_ROOT / "run_tests.sh").read_text(encoding="utf-8")
        for argument in (
            "--gameplay-evidence-campaign-id",
            "--gameplay-evidence-attempt-id",
            "--gameplay-evidence-capture-nonce",
            "--gameplay-evidence-pre-build-head",
            "--gameplay-evidence-post-restore-head",
            "--gameplay-evidence-runtime-tree-sha256",
            "--gameplay-evidence-player-artifact-sha256",
            "--gameplay-evidence-build-payload-sha256",
            "--gameplay-evidence-harness-sha256",
        ):
            self.assertIn(argument, runner)

    def test_metrics_cli_persists_authoritative_schema_two_report(self) -> None:
        self.write_inputs()
        output_path = self.root / "performance-admission.json"

        completed = subprocess.run(
            [
                sys.executable,
                str(VALIDATOR_PATH),
                "metrics",
                "--metrics",
                str(self.metrics_path),
                "--planned-revision",
                REVISION,
                "--expected-width",
                "1920",
                "--expected-height",
                "1080",
                "--expected-warmup-frames",
                "120",
                "--expected-sample-frames",
                "1200",
                "--expected-tick-interval",
                "1",
                "--output",
                str(output_path),
            ],
            cwd=REPO_ROOT,
            check=False,
            capture_output=True,
            text=True,
        )

        self.assertTrue(output_path.is_file(), completed.stderr)
        report = json.loads(output_path.read_text(encoding="utf-8"))
        self.assertEqual(2, report["schemaVersion"])
        self.assertEqual(4, report["evidenceContractVersion"])
        self.assertIn("reasons", report)


if __name__ == "__main__":
    unittest.main()
