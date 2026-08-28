#!/usr/bin/env python3
"""Shared fail-closed parsing and persistence primitives for Evidence Contract v4."""

from __future__ import annotations

import hashlib
import json
import os
import re
import stat
import subprocess
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable


EVIDENCE_CONTRACT_VERSION = 4
APPROVED_CLEANUP_S3_WORKLOAD_SHA256 = (
    "e48fa8fe4b91f1c165bee9985b55a1ce2d376e17214baeaf9e1a1635e50d41d4"
)
ATTEMPT_KINDS = frozenset(("calibration", "warm-up", "official"))
STAGE_STRATEGIES = {
    "S3-A": ["A"],
    "S3-B": ["A", "B"],
    "S3-C": ["A", "B", "C"],
}
HEAD_PATTERN = re.compile(r"^[0-9a-f]{40}$")
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
CAPTURE_IDENTITY_FIELDS = frozenset(
    (
        "campaignId", "attemptId", "attemptOrdinal", "attemptKind", "captureNonce",
        "stage", "activeStrategies", "preBuildHeadSha", "preBuildWorktreeSha256",
        "postRestoreHeadSha", "postRestoreWorktreeSha256", "runtimeTreeSha256",
        "playerArtifactSha256", "buildPayloadSha256", "runnerSha256",
        "performanceValidatorSha256", "cleanupValidatorSha256", "aggregatorSha256",
        "manifestToolSha256", "workloadContractSha256", "harnessSha256",
    )
)
REASON_CODES = frozenset(
    (
        "JSON_PARSE_FAILED", "JSON_DUPLICATE_MEMBER", "JSON_ROOT_INVALID",
        "KV_MALFORMED_LINE", "KV_DUPLICATE_KEY", "SCHEMA_VERSION_INVALID",
        "CONTRACT_VERSION_INVALID", "FIELD_MISSING", "FIELD_UNEXPECTED",
        "FIELD_TYPE_INVALID", "NUMERIC_DOMAIN_INVALID", "CARDINALITY_MISMATCH",
        "ORDER_MISMATCH", "IDENTITY_FIELD_MISSING", "IDENTITY_FIELD_INVALID",
        "IDENTITY_MISMATCH", "PRE_POST_HEAD_MISMATCH", "PRE_POST_WORKTREE_MISMATCH",
        "RUNTIME_TREE_MISMATCH", "PLAYER_ARTIFACT_HASH_MISMATCH",
        "BUILD_PAYLOAD_HASH_MISMATCH", "METRICS_REVISION_MISMATCH",
        "METRICS_HASH_MISMATCH", "STAGE_MISMATCH", "STRATEGY_MISMATCH",
        "WORKLOAD_ID_MISMATCH", "RUN_KEY_MISMATCH", "TOOL_HASH_MISMATCH",
        "CAMPAIGN_ID_MISMATCH", "ATTEMPT_ID_MISMATCH", "MIXED_COHORT",
        "SHA_FORMAT_INVALID", "PERFORMANCE_REJECTED", "CLEANUP_REJECTED",
        "SIGNAL_INVALID", "SEMANTIC_INVARIANT_INVALID", "THRESHOLDS_REQUIRED",
        "THRESHOLDS_FORBIDDEN", "REASONS_COHERENCE_INVALID", "EXIT_STATUS_MISMATCH",
        "PERSISTED_REPORT_MISMATCH", "BUILD_FAILED", "GUARD_RESTORE_FAILED",
        "PLAYER_FAILED", "MARKER_VALIDATION_FAILED", "ARTIFACT_MISSING",
        "STAGE_NOT_RUN", "OUTPUT_INPUT_PATH_ALIAS", "OUTPUT_INPUT_INODE_ALIAS",
        "OUTPUT_ROOT_CONTAINMENT", "INPUT_ARTIFACT_ALIAS",
        "INPUT_MUTATED_DURING_VALIDATION", "ATOMIC_WRITE_FAILED",
        "FINAL_MANIFEST_UNAVAILABLE", "BUILD_ROOT_INVALID",
        "FULL_SCAN_EXPECTATION_UNAPPROVED", "UNSUPPORTED_ARTIFACT_VERSION",
        "HISTORICAL_ARTIFACT_NOT_UPGRADABLE",
    )
)


class EvidenceError(ValueError):
    def __init__(self, code: str, path: str, expected: Any, observed: Any) -> None:
        super().__init__(f"{code}: {path}: expected={expected!r} observed={observed!r}")
        self.reason = reason(code, path, expected, observed)


@dataclass(frozen=True)
class InputSnapshot:
    lexical_path: str
    resolved_path: str
    lexical_exists: bool
    lexical_device: int | None
    lexical_inode: int | None
    lexical_mode: int | None
    symlink_target: str | None
    target_exists: bool
    target_device: int | None
    target_inode: int | None
    target_mode: int | None
    sha256: str | None


def reason(code: str, path: str, expected: Any = None, observed: Any = None) -> dict[str, Any]:
    return {"code": code, "path": path, "expected": expected, "observed": observed}


def validate_reason(value: Any, path: str = "reason") -> list[dict[str, Any]]:
    issues: list[dict[str, Any]] = []
    if not isinstance(value, dict):
        return [reason("FIELD_TYPE_INVALID", path, "object", type(value).__name__)]
    expected_fields = {"code", "path", "expected", "observed"}
    if set(value) != expected_fields:
        issues.append(reason("FIELD_UNEXPECTED", path, sorted(expected_fields), sorted(value)))
    if value.get("code") not in REASON_CODES:
        issues.append(reason("FIELD_TYPE_INVALID", f"{path}.code", sorted(REASON_CODES), value.get("code")))
    if not isinstance(value.get("path"), str) or not value.get("path"):
        issues.append(reason("FIELD_TYPE_INVALID", f"{path}.path", "non-empty string", value.get("path")))
    return issues


def validate_attempt_identity(identity: Any, *, exact: bool = True) -> list[dict[str, Any]]:
    issues: list[dict[str, Any]] = []
    if not isinstance(identity, dict):
        return [reason("FIELD_TYPE_INVALID", "captureIdentity", "object", type(identity).__name__)]
    if exact:
        for field in sorted(CAPTURE_IDENTITY_FIELDS - set(identity)):
            issues.append(reason("IDENTITY_FIELD_MISSING", f"captureIdentity.{field}", "present", None))
        for field in sorted(set(identity) - CAPTURE_IDENTITY_FIELDS):
            issues.append(reason("FIELD_UNEXPECTED", f"captureIdentity.{field}", None, identity[field]))
    for field in ("campaignId", "attemptId", "captureNonce"):
        value = identity.get(field)
        if not isinstance(value, str) or not value:
            issues.append(reason("IDENTITY_FIELD_INVALID", f"captureIdentity.{field}", "non-empty string", value))
    ordinal = identity.get("attemptOrdinal")
    if not is_json_int(ordinal, positive=True):
        issues.append(reason("NUMERIC_DOMAIN_INVALID", "captureIdentity.attemptOrdinal", "positive JSON integer", ordinal))
    attempt_kind = identity.get("attemptKind")
    if attempt_kind not in ATTEMPT_KINDS:
        issues.append(reason("IDENTITY_FIELD_INVALID", "captureIdentity.attemptKind", sorted(ATTEMPT_KINDS), attempt_kind))
    stage = identity.get("stage")
    strategies = identity.get("activeStrategies")
    if stage not in STAGE_STRATEGIES:
        issues.append(reason("IDENTITY_FIELD_INVALID", "captureIdentity.stage", sorted(STAGE_STRATEGIES), stage))
    elif strategies != STAGE_STRATEGIES[stage]:
        issues.append(reason("STRATEGY_MISMATCH", "captureIdentity.activeStrategies", STAGE_STRATEGIES[stage], strategies))
    for field in ("preBuildHeadSha", "postRestoreHeadSha"):
        value = identity.get(field)
        if not isinstance(value, str) or HEAD_PATTERN.fullmatch(value) is None:
            issues.append(reason("SHA_FORMAT_INVALID", f"captureIdentity.{field}", "lowercase 40-hex", value))
    for field in CAPTURE_IDENTITY_FIELDS - {
        "campaignId", "attemptId", "attemptOrdinal", "attemptKind", "captureNonce",
        "stage", "activeStrategies", "preBuildHeadSha", "postRestoreHeadSha",
    }:
        value = identity.get(field)
        if not isinstance(value, str) or SHA256_PATTERN.fullmatch(value) is None:
            issues.append(reason("SHA_FORMAT_INVALID", f"captureIdentity.{field}", "lowercase SHA-256", value))
    return issues


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def try_sha256(path: Path) -> str | None:
    try:
        return sha256(path)
    except OSError:
        return None


def runtime_tree_sha256(head_sha: str, worktree_sha256: str) -> str:
    return hashlib.sha256(
        b"HEAD\0" + head_sha.encode("ascii") + b"\0WORKTREE\0" + worktree_sha256.encode("ascii")
    ).hexdigest()


def build_payload_sha256(root: Path) -> str:
    resolved_root = root.resolve(strict=False)
    if not resolved_root.exists() or not resolved_root.is_dir():
        raise EvidenceError("BUILD_ROOT_INVALID", "buildRoot", "existing directory", str(resolved_root))
    digest = hashlib.sha256()
    files = sorted(
        (
            path
            for path in resolved_root.rglob("*")
            if not path.is_symlink() and path.exists() and stat.S_ISREG(path.stat().st_mode)
        ),
        key=lambda path: path.relative_to(resolved_root).as_posix().encode("utf-8"),
    )
    if not files:
        raise EvidenceError("BUILD_ROOT_INVALID", "buildRoot", "at least one regular payload file", str(resolved_root))
    for path in files:
        relative = path.relative_to(resolved_root).as_posix().encode("utf-8")
        size = str(path.stat().st_size).encode("ascii")
        digest.update(relative + b"\0" + size + b"\0" + sha256(path).encode("ascii") + b"\n")
    return digest.hexdigest()


def directory_snapshot_sha256(root: Path) -> str:
    """Hash exact directory membership, entry type, symlink target, and file bytes."""

    lexical_root = Path(_lexical_path(root))
    if not lexical_root.exists():
        return hashlib.sha256(b"MISSING\n").hexdigest()
    if not lexical_root.is_dir() or lexical_root.is_symlink():
        raise EvidenceError(
            "FIELD_TYPE_INVALID", "directoryInput", "non-symlink directory", str(lexical_root)
        )
    digest = hashlib.sha256(b"DIRECTORY-SNAPSHOT-V1\n")
    entries = sorted(
        lexical_root.rglob("*"),
        key=lambda path: path.relative_to(lexical_root).as_posix().encode("utf-8"),
    )
    for path in entries:
        relative = path.relative_to(lexical_root).as_posix().encode("utf-8")
        value = path.lstat()
        if stat.S_ISLNK(value.st_mode):
            payload = b"L\0" + os.fsencode(os.readlink(path))
        elif stat.S_ISDIR(value.st_mode):
            payload = b"D"
        elif stat.S_ISREG(value.st_mode):
            payload = b"F\0" + str(value.st_size).encode("ascii") + b"\0" + sha256(path).encode("ascii")
        else:
            payload = b"O\0" + str(stat.S_IFMT(value.st_mode)).encode("ascii")
        digest.update(relative + b"\0" + payload + b"\n")
    return digest.hexdigest()


def validate_runtime_marker(path: Path) -> list[dict[str, Any]]:
    try:
        lines = path.read_text(encoding="utf-8-sig").splitlines()
    except (OSError, UnicodeError) as error:
        return [reason("MARKER_VALIDATION_FAILED", "runtimeLog", "readable UTF-8 log", str(error))]
    pass_count = sum(line.strip() == "GAMEPLAY_PERFORMANCE:PASS" for line in lines)
    fail_count = sum(line.strip() == "GAMEPLAY_PERFORMANCE:FAIL" for line in lines)
    if pass_count != 1 or fail_count != 0:
        return [
            reason(
                "MARKER_VALIDATION_FAILED",
                "runtimeLog.terminalMarkers",
                {"pass": 1, "fail": 0},
                {"pass": pass_count, "fail": fail_count},
            )
        ]
    return []


def canonical_repository_root(tool_path: Path) -> Path:
    expected = tool_path.resolve().parents[1]
    completed = subprocess.run(
        ["git", "-C", str(expected), "rev-parse", "--show-toplevel"],
        check=False,
        capture_output=True,
        text=True,
    )
    if completed.returncode != 0:
        raise EvidenceError("SEMANTIC_INVARIANT_INVALID", "repositoryRoot", "Git worktree", completed.stderr.strip())
    observed = Path(completed.stdout.strip()).resolve()
    if observed != expected:
        raise EvidenceError("SEMANTIC_INVARIANT_INVALID", "repositoryRoot", str(expected), str(observed))
    return expected


def live_source_identity(repository_root: Path) -> dict[str, str]:
    head = subprocess.run(
        ["git", "-C", str(repository_root), "rev-parse", "HEAD"],
        check=True,
        capture_output=True,
        text=True,
    ).stdout.strip()
    tracked = subprocess.run(
        ["git", "-C", str(repository_root), "diff", "--binary", "HEAD", "--", "."],
        check=True,
        capture_output=True,
    ).stdout
    untracked_raw = subprocess.run(
        ["git", "-C", str(repository_root), "ls-files", "--others", "--exclude-standard", "-z"],
        check=True,
        capture_output=True,
    ).stdout
    digest = hashlib.sha256()
    digest.update(b"TRACKED\0")
    digest.update(tracked)
    digest.update(b"\0UNTRACKED\0")
    for raw_path in sorted(value for value in untracked_raw.split(b"\0") if value):
        path = repository_root / os.fsdecode(raw_path)
        digest.update(raw_path)
        digest.update(b"\0")
        digest.update(sha256(path).encode("ascii"))
        digest.update(b"\0")
    worktree = digest.hexdigest()
    return {
        "headSha": head,
        "worktreeSha256": worktree,
        "runtimeTreeSha256": runtime_tree_sha256(head, worktree),
    }


def harness_sha256(
    *,
    runner_sha256: str,
    performance_validator_sha256: str,
    cleanup_validator_sha256: str,
    aggregator_sha256: str,
    manifest_tool_sha256: str,
    workload_contract_sha256: str,
) -> str:
    records = (
        ("runnerSha256", runner_sha256),
        ("performanceValidatorSha256", performance_validator_sha256),
        ("cleanupValidatorSha256", cleanup_validator_sha256),
        ("aggregatorSha256", aggregator_sha256),
        ("manifestToolSha256", manifest_tool_sha256),
        ("workloadContractSha256", workload_contract_sha256),
    )
    payload = "".join(f"{label}={value}\n" for label, value in records).encode("ascii")
    return hashlib.sha256(payload).hexdigest()


def _reject_constant(value: str) -> None:
    raise EvidenceError("JSON_PARSE_FAILED", "$", "standard finite JSON number", value)


def _unique_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    value: dict[str, Any] = {}
    for key, member in pairs:
        if key in value:
            raise EvidenceError("JSON_DUPLICATE_MEMBER", f"$.{key}", "unique member", key)
        value[key] = member
    return value


def load_json_object(path: Path, label: str = "input") -> dict[str, Any]:
    try:
        text = path.read_text(encoding="utf-8-sig")
        value = json.loads(
            text,
            object_pairs_hook=_unique_object,
            parse_constant=_reject_constant,
        )
    except EvidenceError:
        raise
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise EvidenceError("JSON_PARSE_FAILED", label, "strict UTF-8 JSON", str(error)) from error
    if not isinstance(value, dict):
        raise EvidenceError("JSON_ROOT_INVALID", label, "object", type(value).__name__)
    return value


def load_strict_kv(path: Path, label: str = "manifest") -> dict[str, str]:
    try:
        lines = path.read_text(encoding="utf-8-sig").splitlines()
    except (OSError, UnicodeError) as error:
        raise EvidenceError("JSON_PARSE_FAILED", label, "readable UTF-8 KV", str(error)) from error
    values: dict[str, str] = {}
    status_seen = False
    status_lines: list[str] = []
    for number, line in enumerate(lines, 1):
        if status_seen:
            if line == "GitStatusShort:":
                raise EvidenceError(
                    "KV_MALFORMED_LINE",
                    f"{label}:{number}",
                    "single GitStatusShort section",
                    line,
                )
            status_lines.append(line)
            continue
        if line == "GitStatusShort:":
            status_seen = True
            continue
        if not line:
            continue
        if "=" not in line:
            raise EvidenceError("KV_MALFORMED_LINE", f"{label}:{number}", "nonempty key=value", line)
        key, value = line.split("=", 1)
        if not key:
            raise EvidenceError("KV_MALFORMED_LINE", f"{label}:{number}", "nonempty key", key)
        if key in values:
            raise EvidenceError("KV_DUPLICATE_KEY", f"{label}.{key}", "unique key", key)
        values[key] = value
    if not status_seen:
        raise EvidenceError("FIELD_MISSING", f"{label}.GitStatusShort", "exactly one section", None)
    values["GitStatusShort"] = "\n".join(status_lines)
    return values


def _is_within(path: Path, root: Path) -> bool:
    try:
        path.relative_to(root)
    except ValueError:
        return False
    return True


def _lexical_path(path: Path) -> str:
    return os.path.abspath(os.fspath(path))


def _path_snapshot(path: Path) -> InputSnapshot:
    lexical = _lexical_path(path)
    lexical_path = Path(lexical)
    resolved = lexical_path.resolve(strict=False)
    try:
        lexical_stat = lexical_path.lstat()
    except OSError:
        return InputSnapshot(
            lexical, str(resolved), False, None, None, None, None,
            False, None, None, None, None,
        )
    symlink_target: str | None = None
    if stat.S_ISLNK(lexical_stat.st_mode):
        try:
            symlink_target = os.readlink(lexical_path)
        except OSError:
            symlink_target = None
    try:
        target_stat = lexical_path.stat()
    except OSError:
        return InputSnapshot(
            lexical, str(resolved), True,
            lexical_stat.st_dev, lexical_stat.st_ino, lexical_stat.st_mode,
            symlink_target, False, None, None, None, None,
        )
    digest = try_sha256(lexical_path) if stat.S_ISREG(target_stat.st_mode) else None
    return InputSnapshot(
        lexical, str(resolved), True,
        lexical_stat.st_dev, lexical_stat.st_ino, lexical_stat.st_mode,
        symlink_target, True,
        target_stat.st_dev, target_stat.st_ino, target_stat.st_mode, digest,
    )


def capture_input_snapshots(paths: Iterable[Path]) -> tuple[tuple[Path, InputSnapshot], ...]:
    """Capture caller-owned input identity before parsing or validation begins."""

    return tuple((Path(path), _path_snapshot(Path(path))) for path in paths)


def ensure_safe_output(
    output: Path,
    inputs: Iterable[Path],
    *,
    mutable_input: Path | None = None,
    forbidden_roots: Iterable[Path] = (),
) -> Path:
    resolved_output = output.resolve(strict=False)
    resolved_mutable = mutable_input.resolve(strict=False) if mutable_input is not None else None
    for root in forbidden_roots:
        resolved_root = root.resolve(strict=False)
        if _is_within(resolved_output, resolved_root):
            raise EvidenceError(
                "OUTPUT_ROOT_CONTAINMENT", "output", "outside forbidden root", str(resolved_root)
            )
    output_stat = None
    try:
        output_stat = resolved_output.stat()
    except OSError:
        pass
    for source in inputs:
        resolved_source = source.resolve(strict=False)
        if resolved_mutable is not None and resolved_source == resolved_mutable == resolved_output:
            continue
        if resolved_source == resolved_output:
            raise EvidenceError(
                "OUTPUT_INPUT_PATH_ALIAS", "output", "path distinct from every input", str(resolved_source)
            )
        if output_stat is not None:
            try:
                source_stat = resolved_source.stat()
            except OSError:
                continue
            if (source_stat.st_dev, source_stat.st_ino) == (output_stat.st_dev, output_stat.st_ino):
                raise EvidenceError(
                    "OUTPUT_INPUT_INODE_ALIAS", "output", "inode distinct from every input", str(resolved_source)
                )
    return resolved_output


def atomic_json(
    path: Path,
    document: dict[str, Any],
    *,
    inputs: Iterable[Path] = (),
    mutable_input: Path | None = None,
    forbidden_roots: Iterable[Path] = (),
    tree_inputs: Iterable[Path] = (),
    tree_input_hashes: Iterable[tuple[Path, str]] = (),
    directory_input_hashes: Iterable[tuple[Path, str]] = (),
    expected_input_snapshots: Iterable[tuple[Path, InputSnapshot]] = (),
) -> None:
    input_paths = tuple(inputs)
    tree_paths = tuple(tree_inputs)
    destination = ensure_safe_output(
        path,
        input_paths,
        mutable_input=mutable_input,
        forbidden_roots=forbidden_roots,
    )
    input_by_lexical = {_lexical_path(source): source for source in input_paths}
    supplied_snapshots: dict[str, tuple[Path, InputSnapshot]] = {}
    for source, expected in expected_input_snapshots:
        key = _lexical_path(source)
        if key not in input_by_lexical:
            raise EvidenceError(
                "SEMANTIC_INVARIANT_INVALID",
                "expectedInputSnapshots",
                "snapshot path included in inputs",
                key,
            )
        if key in supplied_snapshots and supplied_snapshots[key][1] != expected:
            raise EvidenceError(
                "SEMANTIC_INVARIANT_INVALID",
                "expectedInputSnapshots",
                "one snapshot per lexical path",
                key,
            )
        supplied_snapshots[key] = (source, expected)
    snapshots = tuple(
        supplied_snapshots.get(
            key,
            (source, _path_snapshot(source)),
        )
        for key, source in input_by_lexical.items()
    )
    tree_snapshots = {
        source.resolve(strict=False): build_payload_sha256(source) for source in tree_paths
    }
    tree_snapshots.update(
        (source.resolve(strict=False), expected)
        for source, expected in tree_input_hashes
    )
    directory_snapshots = tuple(directory_input_hashes)
    for source, expected in snapshots:
        observed = _path_snapshot(source)
        if observed != expected:
            raise EvidenceError(
                "INPUT_MUTATED_DURING_VALIDATION", str(source), expected, observed
            )
    for source, expected in directory_snapshots:
        observed = directory_snapshot_sha256(source)
        if observed != expected:
            raise EvidenceError(
                "INPUT_MUTATED_DURING_VALIDATION", str(source), expected, observed
            )
    destination.parent.mkdir(parents=True, exist_ok=True)
    payload = json.dumps(document, indent=2, sort_keys=True, allow_nan=False) + "\n"
    descriptor, temporary_name = tempfile.mkstemp(prefix=f".{destination.name}.", dir=destination.parent)
    temporary = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as stream:
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
        for source, expected in snapshots:
            observed = _path_snapshot(source)
            if observed != expected:
                raise EvidenceError(
                    "INPUT_MUTATED_DURING_VALIDATION", str(source), expected, observed
                )
        for source, expected in tree_snapshots.items():
            observed = build_payload_sha256(source)
            if observed != expected:
                raise EvidenceError(
                    "INPUT_MUTATED_DURING_VALIDATION", str(source), expected, observed
                )
        for source, expected in directory_snapshots:
            observed = directory_snapshot_sha256(source)
            if observed != expected:
                raise EvidenceError(
                    "INPUT_MUTATED_DURING_VALIDATION", str(source), expected, observed
                )
        os.replace(temporary, destination)
        try:
            directory_fd = os.open(destination.parent, os.O_RDONLY)
            try:
                os.fsync(directory_fd)
            finally:
                os.close(directory_fd)
        except OSError:
            pass
    finally:
        try:
            temporary.unlink()
        except FileNotFoundError:
            pass


def is_json_int(value: Any, *, positive: bool = False, nonnegative: bool = False) -> bool:
    if type(value) is not int:
        return False
    if positive:
        return value > 0
    if nonnegative:
        return value >= 0
    return True


def evidence_identity(metrics: dict[str, Any], metrics_sha256: str | None) -> dict[str, Any]:
    capture = metrics.get("captureIdentity")
    identity = dict(capture) if isinstance(capture, dict) else {}
    workload_ids: list[str] = []
    run_keys: list[str] = []
    calibration = metrics.get("cleanupSlice3Calibration")
    captures = calibration.get("captures") if isinstance(calibration, dict) else None
    if isinstance(captures, list):
        for capture_value in captures:
            workloads = capture_value.get("workloads") if isinstance(capture_value, dict) else None
            if not isinstance(workloads, list):
                continue
            for workload in workloads:
                if not isinstance(workload, dict):
                    continue
                workload_id = workload.get("workloadId")
                if isinstance(workload_id, str) and workload_id not in workload_ids:
                    workload_ids.append(workload_id)
                runs = workload.get("runs")
                if isinstance(runs, list):
                    for run in runs:
                        run_key = run.get("runKey") if isinstance(run, dict) else None
                        if isinstance(run_key, str):
                            run_keys.append(run_key)
    identity.update(
        {
            "metricsRevision": metrics.get("revision"),
            "metricsSha256": metrics_sha256,
            "workloadIds": workload_ids,
            "orderedRunKeys": run_keys,
        }
    )
    return identity
