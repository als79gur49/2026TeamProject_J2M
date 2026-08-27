#!/usr/bin/env python3
"""Structurally admit gameplay-performance evidence before p95 aggregation.

The lightweight ``metrics`` command is used by ``run_tests.sh`` at current HEAD.
The ``formal`` command additionally validates historical-revision manifests and an
immutable campaign plan, then writes one machine-readable admission record.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import tempfile
from pathlib import Path
from typing import Any, Iterable


ADMITTED = "ADMITTED"
REJECTED_IDENTITY = "REJECTED_IDENTITY"
REJECTED_RESOLUTION = "REJECTED_RESOLUTION"
REJECTED_SAMPLE_COUNT = "REJECTED_SAMPLE_COUNT"
REJECTED_REVISION = "REJECTED_REVISION"
REJECTED_RUNTIME = "REJECTED_RUNTIME"

EXPECTED_PHASES = ("render-idle", "gameplay-neutral-tick")


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _read_json_object(path: Path, label: str) -> dict[str, Any]:
    try:
        with path.open(encoding="utf-8-sig") as stream:
            value = json.load(stream)
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise ValueError(f"{label} is unreadable or invalid JSON: {error}") from error
    if not isinstance(value, dict):
        raise ValueError(f"{label} root must be a JSON object")
    return value


def _parse_manifest(path: Path) -> dict[str, str]:
    try:
        lines = path.read_text(encoding="utf-8-sig").splitlines()
    except (OSError, UnicodeError) as error:
        raise ValueError(f"manifest is unreadable: {error}") from error

    fields: dict[str, str] = {}
    git_status_seen = False
    git_status_lines: list[str] = []
    for line_number, raw_line in enumerate(lines, 1):
        if git_status_seen:
            if raw_line:
                git_status_lines.append(raw_line)
            continue
        if raw_line == "GitStatusShort:":
            git_status_seen = True
            continue
        if "=" not in raw_line:
            raise ValueError(f"manifest line {line_number} is malformed: {raw_line!r}")
        key, value = raw_line.split("=", 1)
        if not key or key in fields:
            raise ValueError(f"manifest line {line_number} has empty or duplicate key {key!r}")
        fields[key] = value

    if not git_status_seen:
        raise ValueError("manifest is missing GitStatusShort section")
    fields["GitStatusShort"] = "\n".join(git_status_lines)
    return fields


def _mismatch(actual: Any, expected: Any) -> bool:
    return type(actual) is not type(expected) or actual != expected


def _format_issues(issues: Iterable[str]) -> str:
    return "; ".join(issues)


def validate_metrics(
    metrics: dict[str, Any],
    *,
    planned_revision: str,
    expected_width: int,
    expected_height: int,
    expected_warmup_frames: int,
    expected_sample_frames: int,
    expected_tick_interval: int,
) -> tuple[str, list[str]]:
    """Validate the phase-local metrics contract in deterministic verdict order."""

    runtime_issues: list[str] = []
    revision_issues: list[str] = []
    resolution_issues: list[str] = []
    sample_issues: list[str] = []
    identity_issues: list[str] = []

    expected_identity = {
        "schemaVersion": 1,
        "measurementKind": "release-like-player-headroom",
        "developmentBuild": False,
        "warmupFrames": expected_warmup_frames,
        "sampleFramesPerPhase": expected_sample_frames,
        "gameplayTickIntervalFrames": expected_tick_interval,
        "vSyncCount": 0,
        "targetFrameRate": -1,
    }
    for key, expected in expected_identity.items():
        actual = metrics.get(key)
        if _mismatch(actual, expected):
            identity_issues.append(f"{key} expected {expected!r}, observed {actual!r}")

    actual_revision = metrics.get("revision")
    if actual_revision != planned_revision:
        revision_issues.append(
            f"metrics revision expected {planned_revision!r}, observed {actual_revision!r}"
        )

    requested = metrics.get("requestedResolution")
    actual = metrics.get("actualResolution")
    expected_resolution = [expected_width, expected_height]
    if requested != expected_resolution:
        resolution_issues.append(
            f"requestedResolution expected {expected_resolution!r}, observed {requested!r}"
        )
    if actual != expected_resolution:
        resolution_issues.append(
            f"actualResolution expected {expected_resolution!r}, observed {actual!r}"
        )

    phases_value = metrics.get("phases")
    phase_by_name: dict[str, dict[str, Any]] = {}
    if not isinstance(phases_value, list):
        sample_issues.append("exact phases require a two-element array")
    else:
        for index, phase_value in enumerate(phases_value):
            if not isinstance(phase_value, dict):
                sample_issues.append(f"phases[{index}] must be an object")
                continue
            name = phase_value.get("phase")
            if not isinstance(name, str):
                sample_issues.append(f"phases[{index}].phase must be a string")
                continue
            if name in phase_by_name:
                sample_issues.append(f"exact phases contain duplicate {name!r}")
                continue
            phase_by_name[name] = phase_value
        if len(phases_value) != 2 or set(phase_by_name) != set(EXPECTED_PHASES):
            sample_issues.append(
                f"exact phases expected {list(EXPECTED_PHASES)!r}, "
                f"observed {list(phase_by_name)!r}"
            )

    if isinstance(phases_value, list) and set(phase_by_name) == set(EXPECTED_PHASES) and len(phases_value) == 2:
        for phase_name in EXPECTED_PHASES:
            phase_value = phase_by_name[phase_name]
            for key in ("sampleCount", "validCpuMainSamples", "validGpuSamples"):
                actual_count = phase_value.get(key)
                if actual_count != expected_sample_frames:
                    sample_issues.append(
                        f"{phase_name}.{key} expected {expected_sample_frames}, "
                        f"observed {actual_count!r}"
                    )

        idle = phase_by_name["render-idle"]
        for key in ("attemptedTicks", "executedTicks"):
            if idle.get(key) != 0:
                sample_issues.append(f"render-idle.{key} expected 0, observed {idle.get(key)!r}")
        idle_tick = idle.get("tickWallMilliseconds")
        if not isinstance(idle_tick, dict) or idle_tick.get("count") != 0:
            observed = idle_tick.get("count") if isinstance(idle_tick, dict) else idle_tick
            sample_issues.append(
                f"render-idle.tickWallMilliseconds.count expected 0, observed {observed!r}"
            )

        gameplay = phase_by_name["gameplay-neutral-tick"]
        expected_tick_count = (
            expected_sample_frames + expected_tick_interval - 1
        ) // expected_tick_interval
        for key in ("attemptedTicks", "executedTicks"):
            actual_count = gameplay.get(key)
            if actual_count != expected_tick_count:
                sample_issues.append(
                    f"gameplay-neutral-tick.{key} expected {expected_tick_count}, "
                    f"observed {actual_count!r}"
                )
        gameplay_tick = gameplay.get("tickWallMilliseconds")
        if not isinstance(gameplay_tick, dict):
            sample_issues.append("gameplay-neutral-tick.tickWallMilliseconds must be an object")
        else:
            count = gameplay_tick.get("count")
            if count != expected_tick_count:
                sample_issues.append(
                    "gameplay-neutral-tick.tickWallMilliseconds.count "
                    f"expected {expected_tick_count}, observed {count!r}"
                )
            p95 = gameplay_tick.get("p95")
            if (
                isinstance(p95, bool)
                or not isinstance(p95, (int, float))
                or not math.isfinite(p95)
                or p95 <= 0
            ):
                runtime_issues.append(
                    "gameplay-neutral-tick.tickWallMilliseconds.p95 must be finite and positive, "
                    f"observed {p95!r}"
                )

    for verdict, issues in (
        (REJECTED_RUNTIME, runtime_issues),
        (REJECTED_REVISION, revision_issues),
        (REJECTED_RESOLUTION, resolution_issues),
        (REJECTED_SAMPLE_COUNT, sample_issues),
        (REJECTED_IDENTITY, identity_issues),
    ):
        if issues:
            return verdict, issues
    return ADMITTED, []


def _atomic_write_json(path: Path, document: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    file_descriptor, temporary_name = tempfile.mkstemp(
        prefix=path.name + ".",
        suffix=".tmp",
        dir=path.parent,
    )
    try:
        with os.fdopen(file_descriptor, "w", encoding="utf-8") as stream:
            json.dump(document, stream, indent=2, sort_keys=True, allow_nan=False)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary_name, path)
    except BaseException:
        try:
            os.unlink(temporary_name)
        except FileNotFoundError:
            pass
        raise


def admit_run(
    *,
    metrics_path: Path,
    manifest_path: Path,
    campaign_plan_path: Path,
    planned_revision: str,
    expected_clean_diff_hash: str,
    campaign_id: str,
    block: str,
    slot: str,
    state: str,
    kind: str,
    attempt: int,
    record_path: Path,
    expected_validator_sha256: str | None = None,
    expected_campaign_plan_sha256: str | None = None,
) -> dict[str, Any]:
    """Validate one capture and always write its machine-readable admission record."""

    metrics_path = metrics_path.resolve()
    manifest_path = manifest_path.resolve()
    campaign_plan_path = campaign_plan_path.resolve()
    record_path = record_path.resolve()
    validator_path = Path(__file__).resolve()
    validator_hash = _sha256(validator_path)
    plan_hash = _sha256(campaign_plan_path) if campaign_plan_path.is_file() else ""

    record: dict[str, Any] = {
        "schemaVersion": 1,
        "campaignId": campaign_id,
        "block": block,
        "slot": slot,
        "state": state,
        "plannedRuntimeSha": planned_revision,
        "kind": kind,
        "attempt": attempt,
        "verdict": REJECTED_RUNTIME,
        "reason": "admission did not complete",
        "metricsPath": str(metrics_path),
        "manifestPath": str(manifest_path),
        "metricsSha256": _sha256(metrics_path) if metrics_path.is_file() else "",
        "manifestSha256": _sha256(manifest_path) if manifest_path.is_file() else "",
        "validatorSha256": validator_hash,
        "campaignPlanSha256": plan_hash,
    }

    try:
        metrics = _read_json_object(metrics_path, "metrics")
        plan = _read_json_object(campaign_plan_path, "campaign plan")
        manifest = _parse_manifest(manifest_path)

        hash_issues: list[str] = []
        if expected_validator_sha256 and validator_hash != expected_validator_sha256:
            hash_issues.append(
                f"validator SHA-256 expected {expected_validator_sha256}, observed {validator_hash}"
            )
        if expected_campaign_plan_sha256 and plan_hash != expected_campaign_plan_sha256:
            hash_issues.append(
                f"campaign plan SHA-256 expected {expected_campaign_plan_sha256}, observed {plan_hash}"
            )

        revision_issues: list[str] = []
        if manifest.get("HEAD") != planned_revision:
            revision_issues.append(
                f"manifest HEAD expected {planned_revision!r}, observed {manifest.get('HEAD')!r}"
            )
        if manifest.get("WorktreeDiffSHA256") != expected_clean_diff_hash:
            revision_issues.append(
                "manifest WorktreeDiffSHA256 expected "
                f"{expected_clean_diff_hash!r}, observed {manifest.get('WorktreeDiffSHA256')!r}"
            )
        if manifest.get("GitStatusShort"):
            revision_issues.append(
                f"manifest GitStatusShort must be empty, observed {manifest.get('GitStatusShort')!r}"
            )

        settings = plan.get("settings")
        campaign_identity = plan.get("campaignIdentity")
        identity_issues: list[str] = []
        if plan.get("schemaVersion") != 1:
            identity_issues.append(
                f"campaign plan schemaVersion expected 1, observed {plan.get('schemaVersion')!r}"
            )
        if plan.get("campaignId") != campaign_id:
            identity_issues.append(
                f"campaignId expected {plan.get('campaignId')!r}, observed {campaign_id!r}"
            )
        states = plan.get("states")
        slots = plan.get("slots")
        if not isinstance(states, dict):
            identity_issues.append("campaign plan states must be an object")
            states = {}
        if not isinstance(slots, list):
            identity_issues.append("campaign plan slots must be an array")
            slots = []
        matching_slots = [
            candidate
            for candidate in slots
            if isinstance(candidate, dict)
            and candidate.get("kind") == kind
            and candidate.get("block") == block
            and candidate.get("slot") == slot
        ]
        if len(matching_slots) != 1:
            identity_issues.append(
                "campaign plan must contain exactly one matching kind/block/slot, "
                f"observed {len(matching_slots)}"
            )
        elif matching_slots[0].get("state") != state:
            identity_issues.append(
                f"campaign slot state expected {matching_slots[0].get('state')!r}, observed {state!r}"
            )
        state_revision = states.get(state)
        if state_revision != planned_revision:
            identity_issues.append(
                f"campaign state revision expected {state_revision!r}, observed {planned_revision!r}"
            )
        if isinstance(attempt, bool) or not isinstance(attempt, int) or attempt < 1:
            identity_issues.append(f"attempt must be a positive integer, observed {attempt!r}")
        if not isinstance(settings, dict):
            identity_issues.append("campaign plan settings must be an object")
            settings = {}
        if not isinstance(campaign_identity, dict):
            identity_issues.append("campaign plan campaignIdentity must be an object")
            campaign_identity = {}

        requested_resolution = settings.get("requestedResolution")
        expected_width = requested_resolution[0] if requested_resolution == [1920, 1080] else 1920
        expected_height = requested_resolution[1] if requested_resolution == [1920, 1080] else 1080
        if requested_resolution != [1920, 1080]:
            identity_issues.append(
                "campaign plan settings.requestedResolution must equal [1920, 1080], "
                f"observed {requested_resolution!r}"
            )

        metrics_verdict, metrics_issues = validate_metrics(
            metrics,
            planned_revision=planned_revision,
            expected_width=expected_width,
            expected_height=expected_height,
            expected_warmup_frames=settings.get("warmupFrames", 120),
            expected_sample_frames=settings.get("sampleFramesPerPhase", 1200),
            expected_tick_interval=settings.get("gameplayTickIntervalFrames", 1),
        )

        for key, expected in (
            ("metricsSchemaVersion", 1),
            ("measurementKind", "release-like-player-headroom"),
            ("developmentBuild", False),
            ("warmupFrames", 120),
            ("sampleFramesPerPhase", 1200),
            ("gameplayTickIntervalFrames", 1),
            ("vSyncCount", 0),
            ("targetFrameRate", -1),
        ):
            actual = settings.get(key)
            if _mismatch(actual, expected):
                identity_issues.append(
                    f"campaign plan {key} expected {expected!r}, observed {actual!r}"
                )

        for key in (
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
        ):
            expected = campaign_identity.get(key)
            actual = metrics.get(key)
            if expected is None or _mismatch(actual, expected):
                identity_issues.append(
                    f"campaign identity {key} expected {expected!r}, observed {actual!r}"
                )

        manifest_identity_map = {
            "GraphicsApi": "graphicsApi",
            "Backend": "backend",
            "Configuration": "configuration",
            "BuildOptions": "buildOptions",
            "Stage": "stage",
            "SceneRoute": "sceneRoute",
        }
        for manifest_key, plan_key in manifest_identity_map.items():
            expected = campaign_identity.get(plan_key)
            actual = manifest.get(manifest_key)
            if expected is None or actual != str(expected):
                identity_issues.append(
                    f"campaign identity {plan_key} expected {expected!r}, observed {actual!r}"
                )

        manifest_setting_map = {
            "RequestedResolution": "1920x1080",
            "WarmupFrames": "120",
            "SampleFramesPerPhase": "1200",
            "GameplayTickIntervalFrames": "1",
        }
        manifest_resolution_issues: list[str] = []
        for key, expected in manifest_setting_map.items():
            actual = manifest.get(key)
            if actual != expected:
                issue = f"manifest {key} expected {expected!r}, observed {actual!r}"
                if key == "RequestedResolution":
                    manifest_resolution_issues.append(issue)
                else:
                    identity_issues.append(issue)

        if hash_issues:
            verdict, issues = REJECTED_IDENTITY, hash_issues
        elif metrics_verdict == REJECTED_RUNTIME:
            verdict, issues = metrics_verdict, metrics_issues
        elif revision_issues or metrics_verdict == REJECTED_REVISION:
            verdict = REJECTED_REVISION
            issues = revision_issues + (metrics_issues if metrics_verdict == REJECTED_REVISION else [])
        elif manifest_resolution_issues or metrics_verdict == REJECTED_RESOLUTION:
            verdict = REJECTED_RESOLUTION
            issues = manifest_resolution_issues + (
                metrics_issues if metrics_verdict == REJECTED_RESOLUTION else []
            )
        elif metrics_verdict == REJECTED_SAMPLE_COUNT:
            verdict, issues = metrics_verdict, metrics_issues
        elif identity_issues or metrics_verdict == REJECTED_IDENTITY:
            verdict = REJECTED_IDENTITY
            issues = identity_issues + (metrics_issues if metrics_verdict == REJECTED_IDENTITY else [])
        else:
            verdict, issues = ADMITTED, []

        record["verdict"] = verdict
        record["reason"] = "all admission contracts satisfied" if not issues else _format_issues(issues)
    except (OSError, ValueError, TypeError, KeyError, IndexError) as error:
        record["verdict"] = REJECTED_RUNTIME
        record["reason"] = str(error)

    _atomic_write_json(record_path, record)
    return record


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    metrics_parser = subparsers.add_parser("metrics", help="validate one metrics JSON")
    metrics_parser.add_argument("--metrics", required=True, type=Path)
    metrics_parser.add_argument("--planned-revision", required=True)
    metrics_parser.add_argument("--expected-width", required=True, type=int)
    metrics_parser.add_argument("--expected-height", required=True, type=int)
    metrics_parser.add_argument("--expected-warmup-frames", required=True, type=int)
    metrics_parser.add_argument("--expected-sample-frames", required=True, type=int)
    metrics_parser.add_argument("--expected-tick-interval", required=True, type=int)

    formal = subparsers.add_parser("formal", help="write a formal campaign admission record")
    formal.add_argument("--metrics", required=True, type=Path)
    formal.add_argument("--manifest", required=True, type=Path)
    formal.add_argument("--campaign-plan", required=True, type=Path)
    formal.add_argument("--planned-revision", required=True)
    formal.add_argument("--expected-clean-diff-hash", required=True)
    formal.add_argument("--campaign-id", required=True)
    formal.add_argument("--block", required=True)
    formal.add_argument("--slot", required=True)
    formal.add_argument("--state", required=True)
    formal.add_argument("--kind", required=True, choices=("warm-up", "official"))
    formal.add_argument("--attempt", required=True, type=int)
    formal.add_argument("--record", required=True, type=Path)
    formal.add_argument("--expected-validator-sha256")
    formal.add_argument("--expected-campaign-plan-sha256")
    return parser


def main(argv: list[str] | None = None) -> int:
    arguments = _build_parser().parse_args(argv)
    if arguments.command == "metrics":
        try:
            metrics = _read_json_object(arguments.metrics.resolve(), "metrics")
            verdict, issues = validate_metrics(
                metrics,
                planned_revision=arguments.planned_revision,
                expected_width=arguments.expected_width,
                expected_height=arguments.expected_height,
                expected_warmup_frames=arguments.expected_warmup_frames,
                expected_sample_frames=arguments.expected_sample_frames,
                expected_tick_interval=arguments.expected_tick_interval,
            )
        except (OSError, ValueError, TypeError, KeyError, IndexError) as error:
            verdict, issues = REJECTED_RUNTIME, [str(error)]
        print(json.dumps({"verdict": verdict, "reason": _format_issues(issues)}, sort_keys=True))
        return 0 if verdict == ADMITTED else 1

    record = admit_run(
        metrics_path=arguments.metrics,
        manifest_path=arguments.manifest,
        campaign_plan_path=arguments.campaign_plan,
        planned_revision=arguments.planned_revision,
        expected_clean_diff_hash=arguments.expected_clean_diff_hash,
        campaign_id=arguments.campaign_id,
        block=arguments.block,
        slot=arguments.slot,
        state=arguments.state,
        kind=arguments.kind,
        attempt=arguments.attempt,
        record_path=arguments.record,
        expected_validator_sha256=arguments.expected_validator_sha256,
        expected_campaign_plan_sha256=arguments.expected_campaign_plan_sha256,
    )
    print(json.dumps(record, sort_keys=True, allow_nan=False))
    return 0 if record["verdict"] == ADMITTED else 1


if __name__ == "__main__":
    raise SystemExit(main())
