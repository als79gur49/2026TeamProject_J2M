#!/usr/bin/env python3
"""Validate capture-only gameplay Tick Level-7 detailed attribution."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import tempfile
from pathlib import Path
from typing import Any


SECTIONS = (
    "inputPreparationTicks",
    "simulationTicks",
    "hostPostProcessTicks",
    "presentationTicks",
    "callbackTicks",
)
SIMULATION_SECTIONS = (
    "simulationBootstrapTicks",
    "simulationPlanTicks",
    "simulationResolveTicks",
    "simulationFinalizeAndSnapshotTicks",
    "simulationCleanupAndSnapshotTicks",
    "simulationRespawnAndFinalSnapshotTicks",
    "simulationResultMaterializationTicks",
    "simulationResidualTicks",
)
PLAN_SECTIONS = (
    "simulationPlanEnemyAiAndProjectionTicks",
    "simulationPlanKinematicAndGravityProjectionTicks",
    "simulationPlanPreMovementStateAndUtilityProjectionTicks",
    "simulationPlanJumpLandingAndPlayerActionAttemptsTicks",
    "simulationPlanMovementIntentCollectionAndPartitionTicks",
    "simulationPlanLocomotionProjectionTicks",
    "simulationPlanMovementExpansionTicks",
    "simulationPlanPayloadOrderingAndResultTicks",
    "simulationPlanResidualTicks",
)
RESOLVE_SECTIONS = (
    "simulationResolveMovementPlanningAndMaterializationTicks",
    "simulationResolveInitialProjectionAndBeforeAttackStateTicks",
    "simulationResolvePreliminaryAttackAndImpactDispositionTicks",
    "simulationResolveMovementRematerializationAndJumpLandingTicks",
    "simulationResolveTileEffectsAndProjectionTicks",
    "simulationResolveFinalAttackAndMaterializationTicks",
    "simulationResolvePostAttackStateAndUtilityTicks",
    "simulationResolveResultMaterializationTicks",
    "simulationResolveResidualTicks",
)
RESOLVE_INITIAL_PROJECTION_SECTIONS = (
    "simulationResolveInitialProjectionSetupAndBatchApplyTicks",
    "simulationResolveInitialPostMovementSnapshotTicks",
    "simulationResolveBeforeAttackAiTransitionTicks",
    "simulationResolveBeforeAttackEnemyActionTicks",
    "simulationResolveInitialProjectionResidualTicks",
)
RESOLVE_INITIAL_POST_MOVEMENT_SNAPSHOT_SECTIONS = (
    "simulationResolveInitialPostMovementSnapshotBaseImportTicks",
    "simulationResolveInitialPostMovementSnapshotOverlayApplyTicks",
    "simulationResolveInitialPostMovementSnapshotMaterializationTicks",
    "simulationResolveInitialPostMovementSnapshotResidualTicks",
)
PLAN_PRE_MOVEMENT_SECTIONS = (
    "simulationPlanPreMovementSetupTicks",
    "simulationPlanPreMovementLogicTicks",
    "simulationPlanPreMovementBookkeepingTicks",
    "simulationPlanPreMovementProjectionApplyTicks",
    "simulationPlanPreMovementUtilityInputSnapshotTicks",
    "simulationPlanPreMovementUtilityResolveTicks",
    "simulationPlanPreMovementUtilityProjectionApplyTicks",
    "simulationPlanPreMovementResidualTicks",
)
RESOLVE_BEFORE_ATTACK_AI_SECTIONS = (
    "simulationResolveBeforeAttackAiSetupTicks",
    "simulationResolveBeforeAttackAiLogicTicks",
    "simulationResolveBeforeAttackAiProjectionApplyTicks",
    "simulationResolveBeforeAttackAiResidualTicks",
)
PRESENTATION_SECTIONS = (
    "presentationCoordinatorTicks",
    "presentationCameraAndStateNotificationTicks",
    "presentationResidualTicks",
)
PRESENTATION_COORDINATOR_SECTIONS = (
    "presentationPreCommitPlanningTicks",
    "presentationCommittedFrameAndStateTicks",
    "presentationMotionAnimationVfxTicks",
    "presentationAudioTicks",
    "presentationApplyCleanupUpdateTicks",
    "presentationCoordinatorResidualTicks",
)
DETAIL_SECTIONS = (
    *SIMULATION_SECTIONS,
    *PLAN_SECTIONS,
    *RESOLVE_SECTIONS,
    *RESOLVE_INITIAL_PROJECTION_SECTIONS,
    *RESOLVE_INITIAL_POST_MOVEMENT_SNAPSHOT_SECTIONS,
    *PLAN_PRE_MOVEMENT_SECTIONS,
    *RESOLVE_BEFORE_ATTACK_AI_SECTIONS,
    *PRESENTATION_SECTIONS,
    *PRESENTATION_COORDINATOR_SECTIONS,
)
ROOT_KEYS = {
    "schemaVersion",
    "measurementKind",
    "captureIdentity",
    "revision",
    "gameplayStage",
    "stopwatchFrequency",
    "sampleCount",
    "samples",
}
SAMPLE_KEYS = {"tickIndex", "threadId", "outerTicks", *SECTIONS, *DETAIL_SECTIONS}


class AttributionError(ValueError):
    pass


def _reject_duplicates(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise AttributionError(f"duplicate JSON member: {key}")
        result[key] = value
    return result


def load_json(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=_reject_duplicates)
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise AttributionError(f"could not read attribution JSON: {exc}") from exc
    if not isinstance(value, dict):
        raise AttributionError("attribution root must be an object")
    return value


def _exact_keys(value: dict[str, Any], expected: set[str], path: str) -> None:
    missing = sorted(expected - set(value))
    unexpected = sorted(set(value) - expected)
    if missing or unexpected:
        raise AttributionError(f"{path} keys mismatch: missing={missing} unexpected={unexpected}")


def _integer(value: Any, path: str, minimum: int = 0) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < minimum:
        raise AttributionError(f"{path} must be an integer >= {minimum}")
    return value


def _percentile(values: list[int], percentile: float) -> float:
    ordered = sorted(values)
    position = max(0.0, min(1.0, percentile)) * (len(ordered) - 1)
    lower = math.floor(position)
    upper = math.ceil(position)
    return ordered[lower] + (ordered[upper] - ordered[lower]) * (position - lower)


def validate_and_summarize(
    value: dict[str, Any],
    *,
    expected_revision: str,
    expected_stage: str,
    expected_samples: int,
    expected_capture_identity: dict[str, Any] | None = None,
) -> dict[str, Any]:
    _exact_keys(value, ROOT_KEYS, "root")
    if value["schemaVersion"] != 7:
        raise AttributionError("schemaVersion must be 7")
    if value["measurementKind"] != "gameplay-tick-level7-pre-movement-before-attack-attribution":
        raise AttributionError("measurementKind is invalid")
    if value["revision"] != expected_revision:
        raise AttributionError("revision does not match the expected revision")
    if value["gameplayStage"] != expected_stage:
        raise AttributionError("gameplayStage does not match the expected stage")
    frequency = _integer(value["stopwatchFrequency"], "stopwatchFrequency", 1)
    sample_count = _integer(value["sampleCount"], "sampleCount", 1)
    if sample_count != expected_samples:
        raise AttributionError(
            f"sampleCount mismatch: actual={sample_count} expected={expected_samples}"
        )
    samples = value["samples"]
    if not isinstance(samples, list) or len(samples) != sample_count:
        raise AttributionError("samples length must equal sampleCount")

    capture_identity = value["captureIdentity"]
    if not isinstance(capture_identity, dict):
        raise AttributionError("captureIdentity must be an object")
    if expected_capture_identity is not None and capture_identity != expected_capture_identity:
        raise AttributionError("captureIdentity does not match the admitted performance metrics")
    if capture_identity.get("preBuildHeadSha") != expected_revision:
        raise AttributionError("captureIdentity.preBuildHeadSha does not match revision")

    values_by_field: dict[str, list[int]] = {"outerTicks": []}
    values_by_field.update({section: [] for section in (*SECTIONS, *DETAIL_SECTIONS)})
    thread_ids: set[int] = set()
    tick_indices: list[int] = []
    for index, sample in enumerate(samples):
        if not isinstance(sample, dict):
            raise AttributionError(f"samples[{index}] must be an object")
        _exact_keys(sample, SAMPLE_KEYS, f"samples[{index}]")
        tick_index = _integer(sample["tickIndex"], f"samples[{index}].tickIndex")
        thread_id = _integer(sample["threadId"], f"samples[{index}].threadId", 1)
        outer = _integer(sample["outerTicks"], f"samples[{index}].outerTicks", 1)
        sections = [
            _integer(sample[field], f"samples[{index}].{field}") for field in SECTIONS
        ]
        detail_sections = {
            field: _integer(sample[field], f"samples[{index}].{field}")
            for field in DETAIL_SECTIONS
        }
        if sum(sections) != outer:
            raise AttributionError(f"samples[{index}] parent/child arithmetic mismatch")
        nested_contracts = (
            ("simulationTicks", SIMULATION_SECTIONS),
            ("simulationPlanTicks", PLAN_SECTIONS),
            ("simulationResolveTicks", RESOLVE_SECTIONS),
            ("simulationResolveInitialProjectionAndBeforeAttackStateTicks", RESOLVE_INITIAL_PROJECTION_SECTIONS),
            ("simulationResolveInitialPostMovementSnapshotTicks", RESOLVE_INITIAL_POST_MOVEMENT_SNAPSHOT_SECTIONS),
            ("simulationPlanPreMovementStateAndUtilityProjectionTicks", PLAN_PRE_MOVEMENT_SECTIONS),
            ("simulationResolveBeforeAttackAiTransitionTicks", RESOLVE_BEFORE_ATTACK_AI_SECTIONS),
            ("presentationTicks", PRESENTATION_SECTIONS),
            ("presentationCoordinatorTicks", PRESENTATION_COORDINATOR_SECTIONS),
        )
        for parent, children in nested_contracts:
            parent_value = sample[parent] if parent in SECTIONS else detail_sections[parent]
            child_total = sum(detail_sections[field] for field in children)
            if child_total != parent_value:
                raise AttributionError(
                    f"samples[{index}] {parent} child arithmetic mismatch"
                )
        tick_indices.append(tick_index)
        thread_ids.add(thread_id)
        values_by_field["outerTicks"].append(outer)
        for field, section_ticks in zip(SECTIONS, sections, strict=True):
            values_by_field[field].append(section_ticks)
        for field, section_ticks in detail_sections.items():
            values_by_field[field].append(section_ticks)

    if len(thread_ids) != 1:
        raise AttributionError("all samples must execute on one thread")
    if len(set(tick_indices)) != sample_count:
        raise AttributionError("tickIndex values must be unique")
    if tick_indices != list(range(tick_indices[0], tick_indices[0] + sample_count)):
        raise AttributionError("tickIndex values must be contiguous and ordered")

    total_outer = sum(values_by_field["outerTicks"])
    milliseconds_per_tick: dict[str, float] = {}
    shares: dict[str, float] = {}
    distributions: dict[str, dict[str, float | int]] = {}
    for field, raw_values in values_by_field.items():
        milliseconds_per_tick[field] = sum(raw_values) * 1000.0 / frequency / sample_count
        distributions[field] = {
            "count": len(raw_values),
            "medianMilliseconds": _percentile(raw_values, 0.5) * 1000.0 / frequency,
            "p95Milliseconds": _percentile(raw_values, 0.95) * 1000.0 / frequency,
            "maximumMilliseconds": max(raw_values) * 1000.0 / frequency,
        }
        if field != "outerTicks":
            shares[field] = sum(raw_values) * 100.0 / total_outer

    shares["presentationEcosystemTicks"] = (
        sum(values_by_field["presentationTicks"]) + sum(values_by_field["callbackTicks"])
    ) * 100.0 / total_outer
    parent_shares: dict[str, dict[str, float]] = {}
    for parent, children in (
        ("simulationTicks", SIMULATION_SECTIONS),
        ("simulationPlanTicks", PLAN_SECTIONS),
        ("simulationResolveTicks", RESOLVE_SECTIONS),
        ("simulationResolveInitialProjectionAndBeforeAttackStateTicks", RESOLVE_INITIAL_PROJECTION_SECTIONS),
        ("simulationResolveInitialPostMovementSnapshotTicks", RESOLVE_INITIAL_POST_MOVEMENT_SNAPSHOT_SECTIONS),
        ("simulationPlanPreMovementStateAndUtilityProjectionTicks", PLAN_PRE_MOVEMENT_SECTIONS),
        ("simulationResolveBeforeAttackAiTransitionTicks", RESOLVE_BEFORE_ATTACK_AI_SECTIONS),
        ("presentationTicks", PRESENTATION_SECTIONS),
        ("presentationCoordinatorTicks", PRESENTATION_COORDINATOR_SECTIONS),
    ):
        parent_total = sum(values_by_field[parent])
        parent_shares[parent] = {
            child: (sum(values_by_field[child]) * 100.0 / parent_total if parent_total else 0.0)
            for child in children
        }
    return {
        "schemaVersion": 7,
        "verdict": "ADMITTED",
        "revision": expected_revision,
        "gameplayStage": expected_stage,
        "sampleCount": sample_count,
        "threadId": next(iter(thread_ids)),
        "firstTickIndex": tick_indices[0],
        "lastTickIndex": tick_indices[-1],
        "millisecondsPerTick": milliseconds_per_tick,
        "outerSharesPercent": shares,
        "parentSharesPercent": parent_shares,
        "distributions": distributions,
    }


def write_json_atomic(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(prefix=path.name + ".", suffix=".tmp", dir=path.parent)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as handle:
            json.dump(value, handle, ensure_ascii=False, indent=2, sort_keys=True)
            handle.write("\n")
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary_name, path)
    finally:
        try:
            os.unlink(temporary_name)
        except FileNotFoundError:
            pass


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--expected-revision", required=True)
    parser.add_argument("--expected-stage", required=True)
    parser.add_argument("--expected-samples", required=True, type=int)
    parser.add_argument("--expected-identity-from", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()

    validator_sha256 = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
    try:
        source = load_json(args.input)
        identity_source = load_json(args.expected_identity_from)
        expected_capture_identity = identity_source.get("captureIdentity")
        if not isinstance(expected_capture_identity, dict):
            raise AttributionError(
                "expected identity source must contain a captureIdentity object"
            )
        report = validate_and_summarize(
            source,
            expected_revision=args.expected_revision,
            expected_stage=args.expected_stage,
            expected_samples=args.expected_samples,
            expected_capture_identity=expected_capture_identity,
        )
        report["sourceSha256"] = hashlib.sha256(args.input.read_bytes()).hexdigest()
        report["identitySourceSha256"] = hashlib.sha256(
            args.expected_identity_from.read_bytes()
        ).hexdigest()
        report["validatorSha256"] = validator_sha256
        status = 0
    except AttributionError as exc:
        report = {
            "schemaVersion": 1,
            "verdict": "REJECTED",
            "reason": str(exc),
            "validatorSha256": validator_sha256,
        }
        status = 1
    write_json_atomic(args.output, report)
    return status


if __name__ == "__main__":
    raise SystemExit(main())
