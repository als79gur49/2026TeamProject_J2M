#!/usr/bin/env python3
"""Freeze, execute, verify, and aggregate the Slice 1 performance campaign."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import statistics
import subprocess
import sys
from pathlib import Path
from typing import Any

import gameplay_performance_admission as admission


CLEAN_DIFF_SHA256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
STATE_REVISIONS = {
    "baseline": "5d338c54a890bb5225ddda9d769f880846b8f1ca",
    "A": "2dc8e5c53c743fbc531c6ed7421a91baa3349a12",
    "B1": "bfe16e8ddd60af03ad036e163fc917cba85f698e",
    "B2": "4498148adc19a9f1fea02b83732756e2c9e6e05e",
    "C2": "29d26ab18b0023a2a7815786f71dfbd2efd5c083",
}
WARMUP_ORDER = ("C2", "B2", "B1", "A", "baseline")
OFFICIAL_BLOCKS = (
    ("block-1", ("baseline", "A", "B1", "B2", "C2")),
    ("block-2", ("C2", "B2", "B1", "A", "baseline")),
    ("block-3", ("B1", "C2", "A", "baseline", "B2")),
)
IDENTITY_KEYS = (
    "unityVersion",
    "operatingSystem",
    "processorType",
    "processorCount",
    "systemMemorySizeMB",
    "graphicsDeviceType",
    "graphicsDeviceName",
    "graphicsDeviceVersion",
    "graphicsMemorySizeMB",
    "qualityLevel",
    "qualityName",
)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, dict):
        raise ValueError(f"{path} root must be an object")
    return value


def atomic_json(path: Path, value: dict[str, Any]) -> None:
    admission._atomic_write_json(path, value)


def planned_slots() -> list[dict[str, str]]:
    slots = [
        {"kind": "warm-up", "block": "warm-up", "slot": f"warmup-{state}", "state": state}
        for state in WARMUP_ORDER
    ]
    for block, states in OFFICIAL_BLOCKS:
        slots.extend(
            {
                "kind": "official",
                "block": block,
                "slot": f"slot-{index:02d}",
                "state": state,
            }
            for index, state in enumerate(states, 1)
        )
    return slots


def build_plan(campaign_id: str, identity_metrics: dict[str, Any], identity_manifest: dict[str, str]) -> dict[str, Any]:
    identity = {key: identity_metrics.get(key) for key in IDENTITY_KEYS}
    identity.update(
        {
            "graphicsApi": identity_manifest.get("GraphicsApi"),
            "backend": identity_manifest.get("Backend"),
            "configuration": identity_manifest.get("Configuration"),
            "buildOptions": identity_manifest.get("BuildOptions"),
            "stage": identity_manifest.get("Stage"),
            "sceneRoute": identity_manifest.get("SceneRoute"),
        }
    )
    return {
        "schemaVersion": 1,
        "campaignId": campaign_id,
        "states": STATE_REVISIONS,
        "slots": planned_slots(),
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
        "campaignIdentity": identity,
        "admissionPolicy": {
            "cleanDiffSha256": CLEAN_DIFF_SHA256,
            "retrySameSlot": True,
            "warmupsAlwaysDiscarded": True,
            "admitBeforeP95Inspection": True,
        },
        "aggregationPolicy": {
            "officialRunsPerState": 3,
            "statistic": "median-of-three-raw-tick-p95",
            "gateMaximumDeltaPercent": 5.0,
            "gates": ["baseline->A", "A->B1", "B1->B2", "B2->C2", "baseline->C2"],
        },
    }


def read_manifest(path: Path) -> dict[str, str]:
    return admission._parse_manifest(path)


def record_key(value: dict[str, Any]) -> tuple[str, str, str]:
    return str(value.get("kind")), str(value.get("block")), str(value.get("slot"))


def load_records(root: Path) -> list[dict[str, Any]]:
    if not root.exists():
        return []
    records = []
    for path in sorted(root.rglob("attempt-*.json")):
        record = read_json(path)
        record["_recordPath"] = str(path.resolve())
        records.append(record)
    return records


def verify_index(
    plan_path: Path,
    validator_path: Path,
    admissions_root: Path,
    *,
    require_complete: bool,
) -> dict[str, Any]:
    plan = read_json(plan_path)
    records = load_records(admissions_root)
    plan_hash = sha256(plan_path)
    validator_hash = sha256(validator_path)
    expected = {record_key(slot): slot for slot in plan.get("slots", [])}
    issues: list[str] = []
    grouped: dict[tuple[str, str, str], list[dict[str, Any]]] = {}
    for record in records:
        key = record_key(record)
        grouped.setdefault(key, []).append(record)
        slot = expected.get(key)
        if slot is None:
            issues.append(f"unexpected admission record slot {key}")
            continue
        for field, expected_value in (
            ("campaignId", plan.get("campaignId")),
            ("state", slot.get("state")),
            ("plannedRuntimeSha", plan.get("states", {}).get(slot.get("state"))),
            ("campaignPlanSha256", plan_hash),
            ("validatorSha256", validator_hash),
        ):
            if record.get(field) != expected_value:
                issues.append(f"{key} {field} expected {expected_value!r}, observed {record.get(field)!r}")
        for path_field, hash_field in (("metricsPath", "metricsSha256"), ("manifestPath", "manifestSha256")):
            artifact = Path(str(record.get(path_field, "")))
            recorded_hash = record.get(hash_field)
            if artifact.is_file() and recorded_hash and sha256(artifact) != recorded_hash:
                issues.append(f"{key} {path_field} changed after admission")

    admitted_slots = 0
    official_admitted = 0
    warmup_admitted = 0
    for key, slot in expected.items():
        attempts = sorted(grouped.get(key, []), key=lambda item: item.get("attempt", 0))
        numbers = [item.get("attempt") for item in attempts]
        if numbers and numbers != list(range(1, len(numbers) + 1)):
            issues.append(f"{key} attempt numbers must be contiguous from 1, observed {numbers!r}")
        admitted = [item for item in attempts if item.get("verdict") == admission.ADMITTED]
        if len(admitted) > 1:
            issues.append(f"{key} has {len(admitted)} admitted attempts")
        if admitted:
            admitted_slots += 1
            official_admitted += slot.get("kind") == "official"
            warmup_admitted += slot.get("kind") == "warm-up"
            if admitted[0].get("attempt") != len(attempts):
                issues.append(f"{key} contains attempts after admission")
        elif require_complete:
            issues.append(f"{key} has no admitted attempt")

    if require_complete and official_admitted != 15:
        issues.append(f"official admitted slot count expected 15, observed {official_admitted}")
    if require_complete and warmup_admitted != 5:
        issues.append(f"warm-up admitted slot count expected 5, observed {warmup_admitted}")
    return {
        "schemaVersion": 1,
        "campaignId": plan.get("campaignId"),
        "campaignPlanSha256": plan_hash,
        "validatorSha256": validator_hash,
        "plannedSlotCount": len(expected),
        "admittedSlotCount": admitted_slots,
        "officialAdmittedSlotCount": official_admitted,
        "warmupAdmittedSlotCount": warmup_admitted,
        "recordCount": len(records),
        "complete": not issues and admitted_slots == len(expected),
        "issues": issues,
    }


def tick_p95(record: dict[str, Any]) -> float:
    metrics = read_json(Path(record["metricsPath"]))
    phases = {phase["phase"]: phase for phase in metrics["phases"]}
    value = phases["gameplay-neutral-tick"]["tickWallMilliseconds"]["p95"]
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"invalid p95 in {record['metricsPath']}")
    return float(value)


def aggregate(plan_path: Path, validator_path: Path, admissions_root: Path) -> dict[str, Any]:
    index = verify_index(plan_path, validator_path, admissions_root, require_complete=True)
    if not index["complete"]:
        raise ValueError("campaign index is incomplete or invalid: " + "; ".join(index["issues"]))
    records = [record for record in load_records(admissions_root) if record.get("verdict") == admission.ADMITTED]
    official = [record for record in records if record.get("kind") == "official"]
    raw: dict[str, list[float]] = {state: [] for state in STATE_REVISIONS}
    ordered_runs = []
    for record in official:
        value = tick_p95(record)
        raw[record["state"]].append(value)
        ordered_runs.append(
            {"block": record["block"], "slot": record["slot"], "state": record["state"], "p95": value}
        )
    state_results: dict[str, Any] = {}
    for state, values in raw.items():
        if len(values) != 3:
            raise ValueError(f"{state} expected exactly 3 official p95 values, observed {len(values)}")
        median = statistics.median(values)
        state_results[state] = {
            "rawP95": values,
            "medianP95": median,
            "minimumP95": min(values),
            "maximumP95": max(values),
            "display": {
                "medianP95": f"{median:.6f}",
                "range": f"{min(values):.6f}..{max(values):.6f}",
            },
        }
    gates = []
    gate_pairs = (("baseline", "A"), ("A", "B1"), ("B1", "B2"), ("B2", "C2"), ("baseline", "C2"))
    for source, target in gate_pairs:
        delta = (state_results[target]["medianP95"] / state_results[source]["medianP95"] - 1.0) * 100.0
        gates.append(
            {"gate": f"{source}->{target}", "deltaPercent": delta, "displayDeltaPercent": f"{delta:.6f}", "passed": delta <= 5.0}
        )
    paired_deltas = []
    for block, _ in OFFICIAL_BLOCKS:
        block_values = {run["state"]: run["p95"] for run in ordered_runs if run["block"] == block}
        for source, target in gate_pairs:
            delta = (block_values[target] / block_values[source] - 1.0) * 100.0
            paired_deltas.append(
                {
                    "block": block,
                    "gate": f"{source}->{target}",
                    "deltaPercent": delta,
                    "displayDeltaPercent": f"{delta:.6f}",
                }
            )
    rejected = [record for record in load_records(admissions_root) if record.get("verdict") != admission.ADMITTED]
    return {
        "schemaVersion": 1,
        "campaignId": index["campaignId"],
        "campaignPlanSha256": index["campaignPlanSha256"],
        "validatorSha256": index["validatorSha256"],
        "stateResults": state_results,
        "gates": gates,
        "supplementalPairedDeltas": paired_deltas,
        "allGatesPassed": all(gate["passed"] for gate in gates),
        "officialExecutionOrder": sorted(ordered_runs, key=lambda item: (item["block"], item["slot"])),
        "rejectedAttempts": [
            {key: record.get(key) for key in ("kind", "block", "slot", "state", "attempt", "verdict", "reason")}
            for record in rejected
        ],
        "gcAllocationVerdict": "UNVERIFIED",
    }


def run_campaign(args: argparse.Namespace) -> int:
    plan_path = args.campaign_plan.resolve()
    validator_path = args.validator.resolve()
    worktree = args.worktree.resolve()
    campaign_root = args.campaign_root.resolve()
    admissions_root = campaign_root / "admissions"
    captures_root = campaign_root / "captures"
    builds_root = args.build_root.resolve()
    plan = read_json(plan_path)
    frozen_plan_hash = sha256(plan_path)
    frozen_validator_hash = sha256(validator_path)
    created_attempts = 0
    while created_attempts < args.max_new_attempts:
        records = load_records(admissions_root)
        pending = None
        for slot in plan["slots"]:
            matching = [record for record in records if record_key(record) == record_key(slot)]
            if not any(record.get("verdict") == admission.ADMITTED for record in matching):
                pending = (slot, len(matching) + 1)
                break
        if pending is None:
            report = verify_index(plan_path, validator_path, admissions_root, require_complete=True)
            atomic_json(campaign_root / "campaign-index.json", report)
            return 0 if report["complete"] else 1
        slot, attempt_number = pending
        if sha256(plan_path) != frozen_plan_hash or sha256(validator_path) != frozen_validator_hash:
            raise RuntimeError("campaign plan or validator changed after campaign execution began")
        status = subprocess.run(
            ["git", "status", "--porcelain"], cwd=worktree, check=True, text=True, stdout=subprocess.PIPE
        ).stdout
        if status:
            raise RuntimeError(f"measurement worktree is dirty before slot {record_key(slot)}: {status}")
        revision = plan["states"][slot["state"]]
        subprocess.run(["git", "switch", "--detach", revision], cwd=worktree, check=True)
        before = {path.resolve() for path in captures_root.iterdir()} if captures_root.exists() else set()
        environment = os.environ.copy()
        environment.update(
            {
                "GAMEPLAY_PERFORMANCE_EVIDENCE_ROOT": str(captures_root),
                "GAMEPLAY_PERFORMANCE_BUILD_ROOT": str(builds_root),
                "GAMEPLAY_PERFORMANCE_WIDTH": "1920",
                "GAMEPLAY_PERFORMANCE_HEIGHT": "1080",
                "GAMEPLAY_PERFORMANCE_WARMUP_FRAMES": "120",
                "GAMEPLAY_PERFORMANCE_SAMPLE_FRAMES": "1200",
                "GAMEPLAY_PERFORMANCE_TICK_INTERVAL": "1",
            }
        )
        print(f"CAMPAIGN_SLOT_START {slot} attempt={attempt_number}", flush=True)
        capture_status = subprocess.run(["./run_tests.sh", "gameplay-performance"], cwd=worktree, env=environment).returncode
        after = {path.resolve() for path in captures_root.iterdir()} if captures_root.exists() else set()
        new_directories = sorted(path for path in after - before if path.is_dir())
        capture = new_directories[-1] if new_directories else captures_root / "missing-capture"
        record_path = admissions_root / slot["block"] / slot["slot"] / f"attempt-{attempt_number:02d}.json"
        record = admission.admit_run(
            metrics_path=capture / "performance-metrics.json",
            manifest_path=capture / "manifest.txt",
            campaign_plan_path=plan_path,
            planned_revision=revision,
            expected_clean_diff_hash=CLEAN_DIFF_SHA256,
            campaign_id=plan["campaignId"],
            block=slot["block"],
            slot=slot["slot"],
            state=slot["state"],
            kind=slot["kind"],
            attempt=attempt_number,
            record_path=record_path,
            expected_validator_sha256=frozen_validator_hash,
            expected_campaign_plan_sha256=frozen_plan_hash,
        )
        created_attempts += 1
        print(
            f"CAMPAIGN_SLOT_RESULT {record['verdict']} captureStatus={capture_status} "
            f"record={record_path} reason={record['reason']}",
            flush=True,
        )
        report = verify_index(plan_path, validator_path, admissions_root, require_complete=False)
        atomic_json(campaign_root / "campaign-index.json", report)
    return 2


def parser() -> argparse.ArgumentParser:
    root = argparse.ArgumentParser(description=__doc__)
    commands = root.add_subparsers(dest="command", required=True)
    create = commands.add_parser("create-plan")
    create.add_argument("--campaign-id", required=True)
    create.add_argument("--identity-metrics", type=Path, required=True)
    create.add_argument("--identity-manifest", type=Path, required=True)
    create.add_argument("--output", type=Path, required=True)
    verify = commands.add_parser("verify-index")
    verify.add_argument("--campaign-plan", type=Path, required=True)
    verify.add_argument("--validator", type=Path, required=True)
    verify.add_argument("--admissions-root", type=Path, required=True)
    verify.add_argument("--output", type=Path)
    aggregate_parser = commands.add_parser("aggregate")
    aggregate_parser.add_argument("--campaign-plan", type=Path, required=True)
    aggregate_parser.add_argument("--validator", type=Path, required=True)
    aggregate_parser.add_argument("--admissions-root", type=Path, required=True)
    aggregate_parser.add_argument("--output", type=Path, required=True)
    run = commands.add_parser("run")
    run.add_argument("--campaign-plan", type=Path, required=True)
    run.add_argument("--validator", type=Path, required=True)
    run.add_argument("--worktree", type=Path, required=True)
    run.add_argument("--campaign-root", type=Path, required=True)
    run.add_argument("--build-root", type=Path, required=True)
    run.add_argument("--max-new-attempts", type=int, default=25)
    return root


def main(argv: list[str] | None = None) -> int:
    args = parser().parse_args(argv)
    if args.command == "create-plan":
        plan = build_plan(args.campaign_id, read_json(args.identity_metrics), read_manifest(args.identity_manifest))
        atomic_json(args.output.resolve(), plan)
        print(json.dumps({"campaignPlan": str(args.output.resolve()), "sha256": sha256(args.output.resolve())}, sort_keys=True))
        return 0
    if args.command == "verify-index":
        report = verify_index(args.campaign_plan.resolve(), args.validator.resolve(), args.admissions_root.resolve(), require_complete=True)
        if args.output:
            atomic_json(args.output.resolve(), report)
        print(json.dumps(report, sort_keys=True))
        return 0 if report["complete"] else 1
    if args.command == "aggregate":
        result = aggregate(args.campaign_plan.resolve(), args.validator.resolve(), args.admissions_root.resolve())
        atomic_json(args.output.resolve(), result)
        print(json.dumps(result, sort_keys=True))
        return 0 if result["allGatesPassed"] else 1
    return run_campaign(args)


if __name__ == "__main__":
    raise SystemExit(main())
