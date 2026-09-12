#!/usr/bin/env python3

from __future__ import annotations

import hashlib
import importlib.util
import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


REPO_ROOT = Path(__file__).resolve().parents[2]
TOOLS_ROOT = REPO_ROOT / "Tools"
sys.path.insert(0, str(TOOLS_ROOT))
CAMPAIGN_PATH = TOOLS_ROOT / "gameplay_performance_campaign.py"
VALIDATOR_PATH = TOOLS_ROOT / "gameplay_performance_admission.py"
SPEC = importlib.util.spec_from_file_location("gameplay_performance_campaign", CAMPAIGN_PATH)
assert SPEC is not None and SPEC.loader is not None
CAMPAIGN = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CAMPAIGN)


def identity_metrics() -> dict[str, object]:
    return {
        "unityVersion": "6000.3.11f1",
        "operatingSystem": "Windows 11",
        "processorType": "CPU",
        "processorCount": 20,
        "systemMemorySizeMB": 32000,
        "graphicsDeviceType": "Direct3D11",
        "graphicsDeviceName": "GPU",
        "graphicsDeviceVersion": "D3D11",
        "graphicsMemorySizeMB": 8000,
        "qualityLevel": 0,
        "qualityName": "PC",
    }


def identity_manifest() -> dict[str, str]:
    return {
        "GraphicsApi": "Direct3D11",
        "Backend": "Mono",
        "Configuration": "ReleaseLikeCapture",
        "BuildOptions": "None",
        "Stage": "stage-1-1",
        "SceneRoute": "canonical gameplay shell",
    }


def metrics_with_p95(value: float) -> dict[str, object]:
    return {
        "phases": [
            {"phase": "render-idle", "tickWallMilliseconds": {"p95": 0}},
            {"phase": "gameplay-neutral-tick", "tickWallMilliseconds": {"p95": value}},
        ]
    }


class GameplayPerformanceCampaignTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.plan_path = self.root / "campaign-plan.json"
        self.admissions = self.root / "admissions"
        self.plan = CAMPAIGN.build_plan("slice1-test", identity_metrics(), identity_manifest())
        CAMPAIGN.atomic_json(self.plan_path, self.plan)

    def tearDown(self) -> None:
        self.temp.cleanup()

    def materialize_complete_campaign(self, *, c2_median: float = 10.4) -> None:
        values = {"baseline": 10.0, "A": 10.1, "B1": 10.2, "B2": 10.3, "C2": c2_median}
        official_seen = {state: 0 for state in values}
        for slot in self.plan["slots"]:
            state = slot["state"]
            if slot["kind"] == "official":
                offset = (-0.01, 0.0, 0.01)[official_seen[state]]
                official_seen[state] += 1
                p95 = values[state] + offset
            else:
                p95 = values[state]
            artifact_root = self.root / "captures" / slot["block"] / slot["slot"]
            artifact_root.mkdir(parents=True, exist_ok=True)
            metrics_path = artifact_root / "performance-metrics.json"
            manifest_path = artifact_root / "manifest.txt"
            metrics_path.write_text(json.dumps(metrics_with_p95(p95)), encoding="utf-8")
            manifest_path.write_text("manifest\n", encoding="utf-8")
            record = {
                "schemaVersion": 1,
                "campaignId": self.plan["campaignId"],
                "kind": slot["kind"],
                "block": slot["block"],
                "slot": slot["slot"],
                "state": state,
                "plannedRuntimeSha": self.plan["states"][state],
                "attempt": 1,
                "verdict": "ADMITTED",
                "reason": "fixture",
                "metricsPath": str(metrics_path.resolve()),
                "manifestPath": str(manifest_path.resolve()),
                "metricsSha256": hashlib.sha256(metrics_path.read_bytes()).hexdigest(),
                "manifestSha256": hashlib.sha256(manifest_path.read_bytes()).hexdigest(),
                "validatorSha256": hashlib.sha256(VALIDATOR_PATH.read_bytes()).hexdigest(),
                "campaignPlanSha256": hashlib.sha256(self.plan_path.read_bytes()).hexdigest(),
            }
            record_path = self.admissions / slot["block"] / slot["slot"] / "attempt-01.json"
            CAMPAIGN.atomic_json(record_path, record)

    def test_plan_has_exact_warmup_and_official_order(self) -> None:
        slots = self.plan["slots"]
        self.assertEqual(list(CAMPAIGN.WARMUP_ORDER), [slot["state"] for slot in slots[:5]])
        self.assertEqual(5, sum(slot["kind"] == "warm-up" for slot in slots))
        self.assertEqual(15, sum(slot["kind"] == "official" for slot in slots))
        self.assertEqual(
            [state for _, states in CAMPAIGN.OFFICIAL_BLOCKS for state in states],
            [slot["state"] for slot in slots[5:]],
        )

    def test_complete_index_requires_exactly_five_warmups_and_fifteen_official_slots(self) -> None:
        self.materialize_complete_campaign()
        report = CAMPAIGN.verify_index(
            self.plan_path,
            VALIDATOR_PATH,
            self.admissions,
            require_complete=True,
        )
        self.assertTrue(report["complete"])
        self.assertEqual(5, report["warmupAdmittedSlotCount"])
        self.assertEqual(15, report["officialAdmittedSlotCount"])

    def test_index_rejects_post_admission_artifact_mutation(self) -> None:
        self.materialize_complete_campaign()
        record_path = next(self.admissions.rglob("attempt-01.json"))
        record = CAMPAIGN.read_json(record_path)
        Path(record["metricsPath"]).write_text("{}", encoding="utf-8")
        report = CAMPAIGN.verify_index(
            self.plan_path,
            VALIDATOR_PATH,
            self.admissions,
            require_complete=True,
        )
        self.assertFalse(report["complete"])
        self.assertTrue(any("changed after admission" in issue for issue in report["issues"]))

    def test_index_rejects_deleted_admitted_artifacts(self) -> None:
        for field in ("metricsPath", "manifestPath"):
            with self.subTest(field=field):
                self.admissions.mkdir(parents=True, exist_ok=True)
                self.materialize_complete_campaign()
                record = CAMPAIGN.read_json(next(self.admissions.rglob("attempt-01.json")))
                Path(record[field]).unlink()

                report = CAMPAIGN.verify_index(
                    self.plan_path,
                    VALIDATOR_PATH,
                    self.admissions,
                    require_complete=True,
                )

                self.assertFalse(report["complete"])
                self.assertTrue(
                    any(f"{field} missing" in issue for issue in report["issues"]),
                    report["issues"],
                )
                for child in tuple(self.admissions.iterdir()):
                    if child.is_dir():
                        shutil.rmtree(child)

    def test_index_rejects_empty_admitted_artifact_hash(self) -> None:
        self.materialize_complete_campaign()
        record_path = next(self.admissions.rglob("attempt-01.json"))
        record = CAMPAIGN.read_json(record_path)
        record["metricsSha256"] = ""
        CAMPAIGN.atomic_json(record_path, record)

        report = CAMPAIGN.verify_index(
            self.plan_path,
            VALIDATOR_PATH,
            self.admissions,
            require_complete=True,
        )

        self.assertFalse(report["complete"])
        self.assertTrue(
            any("metricsSha256" in issue and "lowercase SHA-256" in issue for issue in report["issues"]),
            report["issues"],
        )

    def test_create_plan_output_cannot_alias_identity_input(self) -> None:
        metrics_path = self.root / "identity.json"
        manifest_path = self.root / "identity.txt"
        metrics_path.write_text(json.dumps(identity_metrics()), encoding="utf-8")
        manifest_path.write_text(
            "\n".join(f"{key}={value}" for key, value in identity_manifest().items())
            + "\nGitStatusShort:\n",
            encoding="utf-8",
        )
        original = metrics_path.read_bytes()

        with self.assertRaises(ValueError):
            CAMPAIGN.main(
                [
                    "create-plan",
                    "--campaign-id", "alias-test",
                    "--identity-metrics", str(metrics_path),
                    "--identity-manifest", str(manifest_path),
                    "--output", str(metrics_path),
                ]
            )

        self.assertEqual(original, metrics_path.read_bytes())

    def test_verify_index_rejects_record_mutated_after_validation_snapshot(self) -> None:
        self.materialize_complete_campaign()
        output = self.root / "index.json"
        record_path = next(self.admissions.rglob("attempt-01.json"))
        original_writer = CAMPAIGN.evidence_atomic_json

        def mutate_then_write(*args: object, **kwargs: object) -> None:
            record = CAMPAIGN.read_json(record_path)
            record["reason"] = "mutated"
            record_path.write_text(json.dumps(record), encoding="utf-8")
            original_writer(*args, **kwargs)

        with mock.patch.object(CAMPAIGN, "evidence_atomic_json", side_effect=mutate_then_write):
            with self.assertRaises(ValueError):
                CAMPAIGN.main(
                    [
                        "verify-index",
                        "--campaign-plan", str(self.plan_path),
                        "--validator", str(VALIDATOR_PATH),
                        "--admissions-root", str(self.admissions),
                        "--output", str(output),
                    ]
                )
        self.assertFalse(output.exists())

    def test_verify_index_rejects_new_attempt_added_before_replace(self) -> None:
        self.materialize_complete_campaign()
        output = self.root / "index.json"
        injected = self.admissions / "block-1" / "slot-01" / "attempt-02.json"
        original_writer = CAMPAIGN.evidence_atomic_json

        def add_record_then_write(*args: object, **kwargs: object) -> None:
            injected.write_text('{"injected":true}\n', encoding="utf-8")
            original_writer(*args, **kwargs)

        with mock.patch.object(CAMPAIGN, "evidence_atomic_json", side_effect=add_record_then_write):
            with self.assertRaises(ValueError):
                CAMPAIGN.main(
                    [
                        "verify-index",
                        "--campaign-plan", str(self.plan_path),
                        "--validator", str(VALIDATOR_PATH),
                        "--admissions-root", str(self.admissions),
                        "--output", str(output),
                    ]
                )
        self.assertFalse(output.exists())

    def test_aggregate_uses_raw_median_of_three_and_all_five_gates(self) -> None:
        self.materialize_complete_campaign()
        result = CAMPAIGN.aggregate(self.plan_path, VALIDATOR_PATH, self.admissions)
        self.assertEqual(10.0, result["stateResults"]["baseline"]["medianP95"])
        self.assertEqual(5, len(result["gates"]))
        self.assertEqual(15, len(result["supplementalPairedDeltas"]))
        self.assertTrue(result["allGatesPassed"])
        self.assertEqual("UNVERIFIED", result["gcAllocationVerdict"])

    def test_aggregate_fails_baseline_to_c2_above_five_percent(self) -> None:
        self.materialize_complete_campaign(c2_median=10.6)
        result = CAMPAIGN.aggregate(self.plan_path, VALIDATOR_PATH, self.admissions)
        gate = next(item for item in result["gates"] if item["gate"] == "baseline->C2")
        self.assertFalse(gate["passed"])
        self.assertFalse(result["allGatesPassed"])

    def test_v4_plan_rejects_historical_schema_one_admission_records(self) -> None:
        self.materialize_complete_campaign()
        plan = CAMPAIGN.read_json(self.plan_path)
        plan["evidenceContractVersion"] = 4
        CAMPAIGN.atomic_json(self.plan_path, plan)
        for record_path in self.admissions.rglob("attempt-01.json"):
            record = CAMPAIGN.read_json(record_path)
            record["campaignPlanSha256"] = hashlib.sha256(self.plan_path.read_bytes()).hexdigest()
            CAMPAIGN.atomic_json(record_path, record)

        report = CAMPAIGN.verify_index(
            self.plan_path,
            VALIDATOR_PATH,
            self.admissions,
            require_complete=True,
        )

        self.assertFalse(report["complete"])
        self.assertTrue(
            any("historical schemaVersion 1" in issue for issue in report["issues"]),
            report["issues"],
        )

    def test_duplicate_member_admission_record_makes_index_incomplete(self) -> None:
        self.materialize_complete_campaign()
        record_path = next(self.admissions.rglob("attempt-01.json"))
        payload = record_path.read_text(encoding="utf-8").replace(
            '"schemaVersion": 1',
            '"schemaVersion": 999, "schemaVersion": 1',
            1,
        )
        record_path.write_text(payload, encoding="utf-8")

        report = CAMPAIGN.verify_index(
            self.plan_path,
            VALIDATOR_PATH,
            self.admissions,
            require_complete=True,
        )

        self.assertFalse(report["complete"])
        self.assertTrue(any("strict load failed" in issue for issue in report["issues"]), report["issues"])


if __name__ == "__main__":
    unittest.main()
