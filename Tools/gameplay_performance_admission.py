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
from pathlib import Path
from typing import Any, Iterable

try:
    from Tools.gameplay_evidence_v4 import EvidenceError, atomic_json, capture_input_snapshots, evidence_identity, load_json_object, reason, validate_attempt_identity
except ModuleNotFoundError:
    from gameplay_evidence_v4 import EvidenceError, atomic_json, capture_input_snapshots, evidence_identity, load_json_object, reason, validate_attempt_identity


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
    return load_json_object(path, label)


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


def _summary_issue(
    value: Any,
    *,
    expected_count: int,
    allow_zero_metric: bool = False,
    unavailable_sentinel: bool = False,
    integers: bool = False,
) -> str | None:
    fields = {"count", "median", "p95", "p99", "maximum"}
    if not isinstance(value, dict) or set(value) != fields:
        return f"exact summary fields expected {sorted(fields)!r}, observed {value!r}"
    if type(value.get("count")) is not int or value["count"] != expected_count:
        return f"count expected {expected_count}, observed {value.get('count')!r}"
    samples = [value[name] for name in ("median", "p95", "p99", "maximum")]
    if unavailable_sentinel:
        return None if samples == [-1, -1, -1, -1] else f"unavailable sentinel expected, observed {samples!r}"
    for sample in samples:
        if isinstance(sample, bool) or not isinstance(sample, (int, float)):
            return f"summary values must be JSON numbers, observed {samples!r}"
        if integers and type(sample) is not int:
            return f"summary values must be JSON integers, observed {samples!r}"
        if not math.isfinite(sample) or sample < 0:
            return f"summary values must be finite and nonnegative, observed {samples!r}"
    if samples != sorted(samples):
        return f"summary values must be nondecreasing, observed {samples!r}"
    if expected_count == 0 and not allow_zero_metric:
        return "zero-count summary is not allowed for this field"
    return None


def _format_issues(issues: Iterable[str]) -> str:
    return "; ".join(issues)


def build_metrics_report(
    metrics: dict[str, Any],
    *,
    metrics_sha256: str,
    validator_path: Path,
    planned_revision: str,
    expected_width: int,
    expected_height: int,
    expected_warmup_frames: int,
    expected_sample_frames: int,
    expected_tick_interval: int,
) -> dict[str, Any]:
    verdict, issues = validate_metrics(
        metrics,
        planned_revision=planned_revision,
        expected_width=expected_width,
        expected_height=expected_height,
        expected_warmup_frames=expected_warmup_frames,
        expected_sample_frames=expected_sample_frames,
        expected_tick_interval=expected_tick_interval,
    )
    if metrics.get("schemaVersion") != 2 or metrics.get("evidenceContractVersion") != 4:
        verdict = REJECTED_IDENTITY
        issues = [
            "UNSUPPORTED_ARTIFACT_VERSION: v4 requires metrics schemaVersion=2 and evidenceContractVersion=4"
        ]
    report_reasons = [
        issue
        if isinstance(issue, dict)
        else reason(
            "UNSUPPORTED_ARTIFACT_VERSION"
            if str(issue).startswith("UNSUPPORTED_ARTIFACT_VERSION:")
            else "SEMANTIC_INVARIANT_INVALID",
            "metrics",
            None,
            str(issue),
        )
        for issue in issues
    ]
    return {
        "schemaVersion": 2,
        "evidenceContractVersion": 4,
        "verdict": verdict,
        "reasons": report_reasons,
        "identity": evidence_identity(metrics, metrics_sha256),
        "provenance": {
            "metricsSha256": metrics_sha256,
            "performanceValidatorSha256": _sha256(validator_path),
        },
        "inputHashes": {"metricsSha256": metrics_sha256},
    }


def validate_metrics(
    metrics: dict[str, Any],
    *,
    planned_revision: str,
    expected_width: int,
    expected_height: int,
    expected_warmup_frames: int,
    expected_sample_frames: int,
    expected_tick_interval: int,
) -> tuple[str, list[Any]]:
    """Validate the phase-local metrics contract in deterministic verdict order."""

    runtime_issues: list[str] = []
    revision_issues: list[str] = []
    resolution_issues: list[str] = []
    sample_issues: list[str] = []
    identity_issues: list[str] = []

    is_v4 = metrics.get("schemaVersion") == 2 and metrics.get("evidenceContractVersion") == 4
    expected_identity = {
        "schemaVersion": 2 if is_v4 else 1,
        "measurementKind": "release-like-player-headroom",
        "developmentBuild": False,
        "warmupFrames": expected_warmup_frames,
        "sampleFramesPerPhase": expected_sample_frames,
        "gameplayTickIntervalFrames": expected_tick_interval,
        "vSyncCount": 0,
        "targetFrameRate": -1,
    }
    if is_v4:
        expected_identity["evidenceContractVersion"] = 4
        exact_top_level = {
            "schemaVersion", "evidenceContractVersion", "captureIdentity", "measurementKind",
            "budgetVerdict", "revision", "unityVersion", "developmentBuild", "productName",
            "operatingSystem", "processorType", "processorCount", "systemMemorySizeMB",
            "graphicsDeviceType", "graphicsDeviceName", "graphicsDeviceVersion",
            "graphicsMemorySizeMB", "qualityLevel", "qualityName", "requestedResolution",
            "actualResolution", "vSyncCount", "targetFrameRate", "warmupFrames",
            "sampleFramesPerPhase", "gameplayTickIntervalFrames", "drawCallsCounterAvailable",
            "gcAllocatedCounterAvailable", "phases", "cleanupSlice3Calibration",
        }
        for field in sorted(exact_top_level - set(metrics)):
            identity_issues.append(f"FIELD_MISSING: metrics.{field}")
        for field in sorted(set(metrics) - exact_top_level):
            identity_issues.append(f"FIELD_UNEXPECTED: metrics.{field}")
        capture_identity = metrics.get("captureIdentity")
        capture_fields = {
            "campaignId", "attemptId", "attemptOrdinal", "attemptKind", "captureNonce", "stage",
            "activeStrategies", "preBuildHeadSha", "preBuildWorktreeSha256", "postRestoreHeadSha",
            "postRestoreWorktreeSha256", "runtimeTreeSha256", "playerArtifactSha256",
            "buildPayloadSha256", "runnerSha256", "performanceValidatorSha256",
            "cleanupValidatorSha256", "aggregatorSha256", "manifestToolSha256",
            "workloadContractSha256", "harnessSha256",
        }
        if not isinstance(capture_identity, dict):
            identity_issues.append("IDENTITY_FIELD_MISSING: captureIdentity must be an object")
        else:
            for field in sorted(capture_fields - set(capture_identity)):
                identity_issues.append(f"IDENTITY_FIELD_MISSING: captureIdentity.{field}")
            for field in sorted(set(capture_identity) - capture_fields):
                identity_issues.append(f"FIELD_UNEXPECTED: captureIdentity.{field}")
            if type(capture_identity.get("attemptOrdinal")) is not int or capture_identity.get("attemptOrdinal") <= 0:
                identity_issues.append("NUMERIC_DOMAIN_INVALID: captureIdentity.attemptOrdinal")
            if capture_identity.get("activeStrategies") != ["A"] or capture_identity.get("stage") != "S3-A":
                identity_issues.append("STRATEGY_MISMATCH: captureIdentity stage/strategies")
            if capture_identity.get("preBuildHeadSha") != capture_identity.get("postRestoreHeadSha"):
                revision_issues.append("PRE_POST_HEAD_MISMATCH: captureIdentity")
            if capture_identity.get("preBuildWorktreeSha256") != capture_identity.get("postRestoreWorktreeSha256"):
                revision_issues.append("PRE_POST_WORKTREE_MISMATCH: captureIdentity")
            if capture_identity.get("postRestoreHeadSha") != planned_revision:
                revision_issues.append("METRICS_REVISION_MISMATCH: captureIdentity.postRestoreHeadSha")
            identity_issues.extend(validate_attempt_identity(capture_identity))
        expected_strings = {
            "budgetVerdict": "NOT_CONFIGURED",
        }
        for field, expected in expected_strings.items():
            if metrics.get(field) != expected:
                identity_issues.append(
                    reason("SEMANTIC_INVARIANT_INVALID", f"metrics.{field}", expected, metrics.get(field))
                )
        for field in (
            "revision", "unityVersion", "productName", "operatingSystem", "processorType",
            "graphicsDeviceType", "graphicsDeviceName", "graphicsDeviceVersion", "qualityName",
        ):
            value = metrics.get(field)
            if not isinstance(value, str) or not value:
                identity_issues.append(
                    reason("FIELD_TYPE_INVALID", f"metrics.{field}", "non-empty string", value)
                )
        for field in ("processorCount", "systemMemorySizeMB"):
            value = metrics.get(field)
            if type(value) is not int or value <= 0:
                identity_issues.append(f"NUMERIC_DOMAIN_INVALID: {field} must be a positive integer")
        for field in ("graphicsMemorySizeMB", "qualityLevel"):
            value = metrics.get(field)
            if type(value) is not int or value < 0:
                identity_issues.append(f"NUMERIC_DOMAIN_INVALID: {field} must be a nonnegative integer")
        for field in ("developmentBuild", "drawCallsCounterAvailable", "gcAllocatedCounterAvailable"):
            if type(metrics.get(field)) is not bool:
                identity_issues.append(f"FIELD_TYPE_INVALID: {field} must be a boolean")
        for field in ("requestedResolution", "actualResolution"):
            value = metrics.get(field)
            if (
                not isinstance(value, list) or len(value) != 2 or
                any(type(dimension) is not int or dimension <= 0 for dimension in value)
            ):
                identity_issues.append(f"NUMERIC_DOMAIN_INVALID: {field} must contain two positive integers")
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
            if is_v4:
                exact_phase_fields = {
                    "phase", "sampleCount", "attemptedTicks", "executedTicks",
                    "validCpuMainSamples", "validCpuRenderSamples", "validGpuSamples",
                    "validDrawCallSamples", "validGcAllocatedSamples",
                    "frameIntervalMilliseconds", "cpuMainMilliseconds", "cpuRenderMilliseconds",
                    "gpuMilliseconds", "tickWallMilliseconds", "drawCalls", "gcAllocatedBytes",
                }
                if set(phase_value) != exact_phase_fields:
                    sample_issues.append(
                        f"FIELD_UNEXPECTED: phases[{index}] exact fields mismatch "
                        f"missing={sorted(exact_phase_fields - set(phase_value))!r} "
                        f"extra={sorted(set(phase_value) - exact_phase_fields)!r}"
                    )
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
                if (type(actual_count) is not int if is_v4 else False) or actual_count != expected_sample_frames:
                    sample_issues.append(
                        f"{phase_name}.{key} expected {expected_sample_frames}, "
                        f"observed {actual_count!r}"
                    )
            if is_v4:
                for key in ("validCpuRenderSamples",):
                    count = phase_value.get(key)
                    if type(count) is not int or count < 0 or count > expected_sample_frames:
                        sample_issues.append(f"NUMERIC_DOMAIN_INVALID: {phase_name}.{key}={count!r}")
                for counter_name, available_name, valid_name in (
                    ("drawCalls", "drawCallsCounterAvailable", "validDrawCallSamples"),
                    ("gcAllocatedBytes", "gcAllocatedCounterAvailable", "validGcAllocatedSamples"),
                ):
                    available = metrics.get(available_name)
                    expected_counter_count = expected_sample_frames if available else 0
                    valid_count = phase_value.get(valid_name)
                    if type(valid_count) is not int or valid_count != expected_counter_count:
                        sample_issues.append(
                            f"{phase_name}.{valid_name} expected {expected_counter_count}, observed {valid_count!r}"
                        )
                    issue = _summary_issue(
                        phase_value.get(counter_name),
                        expected_count=expected_counter_count,
                        unavailable_sentinel=not available,
                        integers=True,
                    )
                    if issue:
                        sample_issues.append(f"{phase_name}.{counter_name}: {issue}")
                for summary_name in (
                    "frameIntervalMilliseconds", "cpuMainMilliseconds", "cpuRenderMilliseconds", "gpuMilliseconds"
                ):
                    summary_count = phase_value.get(f"valid{summary_name[0].upper()}{summary_name[1:].replace('Milliseconds', '')}Samples")
                    if summary_name == "frameIntervalMilliseconds":
                        summary_count = expected_sample_frames
                    elif summary_name == "cpuMainMilliseconds":
                        summary_count = phase_value.get("validCpuMainSamples")
                    elif summary_name == "cpuRenderMilliseconds":
                        summary_count = phase_value.get("validCpuRenderSamples")
                    elif summary_name == "gpuMilliseconds":
                        summary_count = phase_value.get("validGpuSamples")
                    issue = _summary_issue(
                        phase_value.get(summary_name),
                        expected_count=summary_count if type(summary_count) is int else -1,
                    )
                    if issue:
                        sample_issues.append(f"{phase_name}.{summary_name}: {issue}")

        idle = phase_by_name["render-idle"]
        for key in ("attemptedTicks", "executedTicks"):
            if (is_v4 and type(idle.get(key)) is not int) or idle.get(key) != 0:
                sample_issues.append(f"render-idle.{key} expected 0, observed {idle.get(key)!r}")
        idle_tick = idle.get("tickWallMilliseconds")
        if not isinstance(idle_tick, dict) or idle_tick.get("count") != 0:
            observed = idle_tick.get("count") if isinstance(idle_tick, dict) else idle_tick
            sample_issues.append(
                f"render-idle.tickWallMilliseconds.count expected 0, observed {observed!r}"
            )
        elif is_v4:
            issue = _summary_issue(idle_tick, expected_count=0, allow_zero_metric=True)
            if issue:
                sample_issues.append(f"render-idle.tickWallMilliseconds: {issue}")

        gameplay = phase_by_name["gameplay-neutral-tick"]
        if type(expected_tick_interval) is not int or expected_tick_interval <= 0:
            sample_issues.append(
                reason(
                    "NUMERIC_DOMAIN_INVALID",
                    "expectedTickInterval",
                    "positive integer",
                    expected_tick_interval,
                )
            )
            expected_tick_count = -1
        else:
            expected_tick_count = (
                expected_sample_frames + expected_tick_interval - 1
            ) // expected_tick_interval
        for key in ("attemptedTicks", "executedTicks"):
            actual_count = gameplay.get(key)
            if (is_v4 and type(actual_count) is not int) or actual_count != expected_tick_count:
                sample_issues.append(
                    f"gameplay-neutral-tick.{key} expected {expected_tick_count}, "
                    f"observed {actual_count!r}"
                )
        gameplay_tick = gameplay.get("tickWallMilliseconds")
        if not isinstance(gameplay_tick, dict):
            sample_issues.append("gameplay-neutral-tick.tickWallMilliseconds must be an object")
        else:
            count = gameplay_tick.get("count")
            if (is_v4 and type(count) is not int) or count != expected_tick_count:
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
            if is_v4:
                issue = _summary_issue(gameplay_tick, expected_count=expected_tick_count)
                if issue:
                    sample_issues.append(f"gameplay-neutral-tick.tickWallMilliseconds: {issue}")

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
    input_paths = (metrics_path, manifest_path, campaign_plan_path, validator_path)
    input_snapshots = capture_input_snapshots(input_paths)
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

    atomic_json(
        record_path,
        record,
        inputs=input_paths,
        expected_input_snapshots=input_snapshots,
    )
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
    metrics_parser.add_argument("--output", type=Path)

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
        metrics_hash = None
        try:
            metrics_path = arguments.metrics.resolve()
            input_paths = (metrics_path, Path(__file__).resolve())
            input_snapshots = capture_input_snapshots(input_paths)
            metrics_hash = _sha256(metrics_path)
            metrics = load_json_object(metrics_path, "metrics")
            report = build_metrics_report(
                metrics,
                metrics_sha256=metrics_hash,
                validator_path=Path(__file__).resolve(),
                planned_revision=arguments.planned_revision,
                expected_width=arguments.expected_width,
                expected_height=arguments.expected_height,
                expected_warmup_frames=arguments.expected_warmup_frames,
                expected_sample_frames=arguments.expected_sample_frames,
                expected_tick_interval=arguments.expected_tick_interval,
            )
            verdict = report["verdict"]
        except EvidenceError as error:
            verdict, issues = REJECTED_RUNTIME, [error.reason]
        except (OSError, ValueError, TypeError, KeyError, IndexError) as error:
            verdict, issues = REJECTED_RUNTIME, [str(error)]
        if "report" not in locals():
            report = {
                "schemaVersion": 2,
                "evidenceContractVersion": 4,
                "verdict": verdict,
                "reasons": [
                    issue if isinstance(issue, dict) else reason("SEMANTIC_INVARIANT_INVALID", "metrics", None, str(issue))
                    for issue in issues
                ],
                "identity": {},
                "provenance": {"metricsSha256": metrics_hash, "performanceValidatorSha256": _sha256(Path(__file__).resolve())},
                "inputHashes": {"metricsSha256": metrics_hash},
            }
        if arguments.output is not None:
            try:
                atomic_json(
                    arguments.output,
                    report,
                    inputs=input_paths,
                    expected_input_snapshots=input_snapshots,
                )
            except (EvidenceError, OSError):
                return 2
        print(json.dumps(report, sort_keys=True, allow_nan=False))
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
