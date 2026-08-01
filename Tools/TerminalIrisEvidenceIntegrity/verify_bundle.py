#!/usr/bin/env python3
from __future__ import annotations

import argparse
import csv
from dataclasses import dataclass
from datetime import datetime, timezone
import fnmatch
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import platform
import re
import struct
import subprocess
import sys
from typing import Callable, Iterable
import xml.etree.ElementTree as ET
import zlib


FAILURE_CODES = {
    "MISSING_REQUIRED_SOURCE",
    "SOURCE_HASH_MISMATCH",
    "SOURCE_FREEZE_DRIFT",
    "MISSING_REQUIRED_LANE",
    "DUPLICATE_LANE",
    "UNKNOWN_LANE",
    "MISSING_RESULT_ARTIFACT",
    "DUPLICATE_RESULT_ARTIFACT",
    "INVALID_XML",
    "ZERO_TEST_RESULT",
    "COMMAND_RESULT_MISMATCH",
    "MISSING_GRAPHICS_ENVIRONMENT",
    "GRAPHICS_ENVIRONMENT_MISMATCH",
    "PLAYER_RESULT_INCOMPLETE",
    "UNEXPECTED_UI_FAILURE",
    "UI_BASELINE_DRIFT",
    "MISSING_PIXEL_ARTIFACT",
    "PIXEL_ARTIFACT_HASH_MISMATCH",
    "DUPLICATE_SAMPLE_ID",
    "OFFCENTER_MATRIX_INCOMPLETE",
    "TRACE_CORRELATION_MISMATCH",
    "FINAL_SELECTOR_MISMATCH",
    "HEATMAP_CORRELATION_MISMATCH",
    "ARTIFACT_MANIFEST_MISMATCH",
    "UNMANIFESTED_FILE",
    "MISSING_MANIFESTED_FILE",
    "VERIFIER_MUTATED_INPUT",
}


@dataclass(frozen=True)
class Failure:
    code: str
    message: str
    path: str = ""

    def as_dict(self) -> dict[str, str]:
        return {"code": self.code, "message": self.message, "path": self.path}


@dataclass(frozen=True)
class BundleSnapshot:
    path_set: tuple[str, ...]
    file_count: int
    total_size: int
    artifact_manifest_sha256: str
    aggregate_digest: str
    files: dict[str, tuple[int, int, str]]

    def summary(self) -> dict[str, object]:
        return {
            "fileCount": self.file_count,
            "totalSize": self.total_size,
            "artifactManifestSha256": self.artifact_manifest_sha256,
            "rootAggregateDigest": self.aggregate_digest,
            "filesystemPathSetSha256": hashlib.sha256(
                "\n".join(self.path_set).encode("utf-8")
            ).hexdigest(),
        }


def utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%fZ")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def normalize_relative(value: str) -> str:
    if not value or "\\" in value or "\x00" in value:
        raise ValueError(f"invalid relative path: {value!r}")
    raw_parts = value.split("/")
    if any(part in ("", ".", "..") for part in raw_parts):
        raise ValueError(f"unsafe relative path: {value!r}")
    pure = PurePosixPath(value)
    if pure.is_absolute():
        raise ValueError(f"unsafe relative path: {value!r}")
    return pure.as_posix()


def collision_failures(paths: Iterable[str]) -> list[Failure]:
    failures: list[Failure] = []
    exact: set[str] = set()
    folded: dict[str, str] = {}
    for raw in paths:
        try:
            path = normalize_relative(raw)
        except ValueError as exc:
            failures.append(Failure("ARTIFACT_MANIFEST_MISMATCH", str(exc), raw))
            continue
        if path in exact:
            failures.append(
                Failure(
                    "ARTIFACT_MANIFEST_MISMATCH",
                    "duplicate normalized path",
                    path,
                )
            )
        exact.add(path)
        key = path.casefold()
        if key in folded and folded[key] != path:
            failures.append(
                Failure(
                    "ARTIFACT_MANIFEST_MISMATCH",
                    f"case collision with {folded[key]}",
                    path,
                )
            )
        folded[key] = path
    return failures


def exact_lane_failure_codes(
    actual_lane_ids: Iterable[str], required_lane_ids: Iterable[str]
) -> set[str]:
    actual = list(actual_lane_ids)
    required = set(required_lane_ids)
    codes: set[str] = set()
    if required - set(actual):
        codes.add("MISSING_REQUIRED_LANE")
    if set(actual) - required:
        codes.add("UNKNOWN_LANE")
    if len(actual) != len(set(actual)):
        codes.add("DUPLICATE_LANE")
    return codes


def ui_failure_policy_code(
    actual_failed_ids: set[str], approved_failed_ids: set[str]
) -> str | None:
    if actual_failed_ids - approved_failed_ids:
        return "UNEXPECTED_UI_FAILURE"
    if approved_failed_ids - actual_failed_ids:
        return "UI_BASELINE_DRIFT"
    return None


def offcenter_matrix(
    directions: Iterable[str], repetitions: Iterable[int]
) -> set[tuple[str, str]]:
    return {
        (direction, str(repetition))
        for direction in directions
        for repetition in repetitions
    }


def snapshot_bundle(bundle: Path) -> BundleSnapshot:
    files: dict[str, tuple[int, int, str]] = {}
    aggregate = hashlib.sha256()
    for path in sorted(bundle.rglob("*")):
        relative = path.relative_to(bundle).as_posix()
        if path.is_symlink():
            target = os.readlink(path)
            digest = hashlib.sha256(target.encode("utf-8")).hexdigest()
            stat = path.lstat()
        elif path.is_file():
            digest = sha256_file(path)
            stat = path.stat()
        else:
            continue
        files[relative] = (stat.st_size, stat.st_mtime_ns, digest)
        aggregate.update(relative.encode("utf-8"))
        aggregate.update(b"\0")
        aggregate.update(str(stat.st_size).encode("ascii"))
        aggregate.update(b"\0")
        aggregate.update(str(stat.st_mtime_ns).encode("ascii"))
        aggregate.update(b"\0")
        aggregate.update(digest.encode("ascii"))
        aggregate.update(b"\n")
    manifest = bundle / "artifact-hashes.sha256"
    return BundleSnapshot(
        tuple(files),
        len(files),
        sum(value[0] for value in files.values()),
        sha256_file(manifest) if manifest.is_file() else "",
        aggregate.hexdigest(),
        files,
    )


def snapshot_difference(
    before: BundleSnapshot, after: BundleSnapshot
) -> dict[str, list[str]]:
    before_paths = set(before.files)
    after_paths = set(after.files)
    return {
        "added": sorted(after_paths - before_paths),
        "removed": sorted(before_paths - after_paths),
        "changed": sorted(
            path
            for path in before_paths & after_paths
            if before.files[path] != after.files[path]
        ),
    }


def parse_xml(path: Path) -> dict[str, object]:
    root = ET.parse(path).getroot()
    return {
        "total": int(root.attrib.get("total", "0")),
        "passed": int(root.attrib.get("passed", "0")),
        "failed": int(root.attrib.get("failed", "0")),
        "skipped": int(
            root.attrib.get("skipped", root.attrib.get("inconclusive", "0"))
        ),
        "failedTestIds": sorted(
            node.attrib.get("fullname", "")
            for node in root.iter("test-case")
            if node.attrib.get("result") == "Failed"
        ),
    }


def decode_png_rgba(path: Path) -> tuple[int, int, bytes]:
    data = path.read_bytes()
    if not data.startswith(b"\x89PNG\r\n\x1a\n"):
        raise ValueError("invalid PNG signature")
    offset = 8
    width = height = bit_depth = color_type = interlace = None
    compressed = bytearray()
    while offset + 12 <= len(data):
        length = struct.unpack(">I", data[offset : offset + 4])[0]
        chunk_type = data[offset + 4 : offset + 8]
        chunk = data[offset + 8 : offset + 8 + length]
        offset += 12 + length
        if chunk_type == b"IHDR":
            width, height, bit_depth, color_type, _, _, interlace = struct.unpack(
                ">IIBBBBB", chunk
            )
        elif chunk_type == b"IDAT":
            compressed.extend(chunk)
        elif chunk_type == b"IEND":
            break
    if (
        width is None
        or height is None
        or bit_depth != 8
        or color_type not in (2, 6)
        or interlace != 0
    ):
        raise ValueError(
            "only non-interlaced 8-bit RGB/RGBA PNG evidence is supported"
        )
    channels = 4 if color_type == 6 else 3
    stride = width * channels
    raw = zlib.decompress(bytes(compressed))
    expected = height * (stride + 1)
    if len(raw) != expected:
        raise ValueError(f"PNG scanline length mismatch: {len(raw)} != {expected}")
    rows: list[bytearray] = []
    position = 0
    for _ in range(height):
        filter_type = raw[position]
        position += 1
        encoded = raw[position : position + stride]
        position += stride
        row = bytearray(stride)
        previous = rows[-1] if rows else bytearray(stride)
        for index, value in enumerate(encoded):
            left = row[index - channels] if index >= channels else 0
            up = previous[index]
            up_left = previous[index - channels] if index >= channels else 0
            if filter_type == 0:
                decoded = value
            elif filter_type == 1:
                decoded = (value + left) & 0xFF
            elif filter_type == 2:
                decoded = (value + up) & 0xFF
            elif filter_type == 3:
                decoded = (value + ((left + up) // 2)) & 0xFF
            elif filter_type == 4:
                estimate = left + up - up_left
                distances = (
                    abs(estimate - left),
                    abs(estimate - up),
                    abs(estimate - up_left),
                )
                predictor = (left, up, up_left)[distances.index(min(distances))]
                decoded = (value + predictor) & 0xFF
            else:
                raise ValueError(f"unsupported PNG filter {filter_type}")
            row[index] = decoded
        rows.append(row)
    if channels == 4:
        pixels = b"".join(rows)
    else:
        expanded = bytearray(width * height * 4)
        output = 0
        for row in rows:
            for index in range(0, len(row), 3):
                expanded[output : output + 4] = row[index : index + 3] + b"\xff"
                output += 4
        pixels = bytes(expanded)
    return width, height, pixels


def png_to_unity_pixel_order(width: int, height: int, pixels: bytes) -> bytes:
    stride = width * 4
    return b"".join(
        pixels[row * stride : (row + 1) * stride]
        for row in range(height - 1, -1, -1)
    )


def one_match(
    root: Path,
    pattern: str,
    failures: list[Failure],
    missing_code: str,
    duplicate_code: str = "DUPLICATE_RESULT_ARTIFACT",
) -> Path | None:
    matches = sorted(root.glob(pattern))
    if not matches:
        failures.append(Failure(missing_code, f"no match for {pattern}", str(root)))
        return None
    if len(matches) != 1:
        failures.append(
            Failure(
                duplicate_code,
                f"{len(matches)} matches for exact-one pattern {pattern}",
                str(root),
            )
        )
        return None
    return matches[0]


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open(newline="", encoding="utf-8-sig") as stream:
        return list(csv.DictReader(stream))


def repository_for_contract(contract_path: Path) -> Path:
    result = subprocess.check_output(
        ["git", "rev-parse", "--show-toplevel"], cwd=contract_path.parent
    )
    return Path(result.decode().strip()).resolve()


def verify_source_freeze(
    bundle: Path,
    contract_path: Path,
    contract: dict[str, object],
    failures: list[Failure],
    checks: list[str],
) -> tuple[dict[str, object], dict[str, dict[str, object]], Path]:
    freeze_path = bundle / "00-source/source-freeze.json"
    hash_path = bundle / "00-source/source-freeze.sha256"
    repo = repository_for_contract(contract_path)
    if not freeze_path.is_file():
        failures.append(
            Failure(
                "MISSING_REQUIRED_SOURCE",
                "source-freeze.json is missing",
                "00-source/source-freeze.json",
            )
        )
        return {}, {}, repo
    freeze = json.loads(freeze_path.read_text(encoding="utf-8"))
    if not hash_path.is_file():
        failures.append(
            Failure(
                "SOURCE_HASH_MISMATCH",
                "source-freeze SHA record is missing",
                "00-source/source-freeze.sha256",
            )
        )
    else:
        expected = hash_path.read_text(encoding="utf-8").split()[0]
        if sha256_file(freeze_path) != expected:
            failures.append(
                Failure(
                    "SOURCE_HASH_MISMATCH",
                    "source-freeze manifest SHA does not match its record",
                    "00-source/source-freeze.json",
                )
            )
    if freeze.get("contractVersion") != contract.get("contractVersion"):
        failures.append(
            Failure(
                "SOURCE_FREEZE_DRIFT",
                "contract version differs from source freeze",
                "00-source/source-freeze.json",
            )
        )
    if freeze.get("contractSha256") != sha256_file(contract_path):
        failures.append(
            Failure(
                "SOURCE_HASH_MISMATCH",
                "contract SHA differs from source freeze",
                str(contract_path),
            )
        )
    sources = freeze.get("sources", [])
    recomputed_freeze_id = "TISF-" + hashlib.sha256(
        json.dumps(
            sources, sort_keys=True, separators=(",", ":")
        ).encode("utf-8")
    ).hexdigest()[:24]
    if freeze.get("sourceFreezeId") != recomputed_freeze_id:
        failures.append(
            Failure(
                "SOURCE_FREEZE_DRIFT",
                "sourceFreezeId does not match the canonical source inventory",
                "00-source/source-freeze.json",
            )
        )
    paths = [str(source.get("path", "")) for source in sources]
    failures.extend(collision_failures(paths))
    required = set(contract["requiredSources"])
    actual = set(paths)
    for missing in sorted(required - actual):
        failures.append(
            Failure(
                "MISSING_REQUIRED_SOURCE",
                "contract-required source absent from freeze inventory",
                missing,
            )
        )
    for unknown in sorted(actual - required):
        failures.append(
            Failure(
                "SOURCE_FREEZE_DRIFT",
                "source freeze contains an uncontracted source",
                unknown,
            )
        )
    source_map = {
        str(source.get("path", "")): source for source in sources if source.get("path")
    }
    for relative in sorted(required & actual):
        path = repo / relative
        source = source_map[relative]
        if not path.is_file():
            failures.append(
                Failure(
                    "MISSING_REQUIRED_SOURCE",
                    "required source does not exist in repository",
                    relative,
                )
            )
            continue
        if (
            path.stat().st_size != int(source.get("size", -1))
            or sha256_file(path) != source.get("sha256")
        ):
            failures.append(
                Failure(
                    "SOURCE_HASH_MISMATCH",
                    "repository source differs from frozen size/SHA",
                    relative,
                )
            )
        tracked = (
            subprocess.run(
                ["git", "ls-files", "--error-unmatch", "--", relative],
                cwd=repo,
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
            ).returncode
            == 0
        )
        expected_state = "tracked" if tracked else "untracked"
        if source.get("state") != expected_state:
            failures.append(
                Failure(
                    "SOURCE_FREEZE_DRIFT",
                    f"source state {source.get('state')} != {expected_state}",
                    relative,
                )
            )
    tracked_hash = hashlib.sha256(
        subprocess.check_output(["git", "diff", "--binary"], cwd=repo)
    ).hexdigest()
    cached_hash = hashlib.sha256(
        subprocess.check_output(["git", "diff", "--cached", "--binary"], cwd=repo)
    ).hexdigest()
    if (
        tracked_hash != freeze.get("trackedDiffSha256")
        or cached_hash != freeze.get("cachedDiffSha256")
    ):
        failures.append(
            Failure(
                "SOURCE_FREEZE_DRIFT",
                "current tracked/cached diff digest differs from source freeze",
                str(repo),
            )
        )
    checks.append(f"required source exact set checked: {len(required)}")
    return freeze, source_map, repo


def verify_artifact_manifest(
    bundle: Path,
    contract: dict[str, object],
    pre_snapshot: BundleSnapshot,
    failures: list[Failure],
    checks: list[str],
) -> dict[str, str]:
    manifest = bundle / "artifact-hashes.sha256"
    produced_path = bundle / "bundle-produced.json"
    closed_path = bundle / "BUNDLE_CLOSED"
    if not manifest.is_file() or not produced_path.is_file() or not closed_path.is_file():
        failures.append(
            Failure(
                "ARTIFACT_MANIFEST_MISMATCH",
                "closed bundle markers or pre-generated manifest are missing",
                str(bundle),
            )
        )
        return {}
    produced = json.loads(produced_path.read_text(encoding="utf-8"))
    closed = json.loads(closed_path.read_text(encoding="utf-8"))
    if (
        closed.get("bundleId") != produced.get("bundleId")
        or closed.get("sourceFreezeId") != produced.get("sourceFreezeId")
        or closed.get("bundleProducedSha256") != sha256_file(produced_path)
    ):
        failures.append(
            Failure(
                "ARTIFACT_MANIFEST_MISMATCH",
                "BUNDLE_CLOSED identity or bundle-produced SHA mismatch",
                "BUNDLE_CLOSED",
            )
        )
    exclusions = list(contract["allowedExplicitExclusions"])
    if produced.get("explicitExclusions") != exclusions:
        failures.append(
            Failure(
                "ARTIFACT_MANIFEST_MISMATCH",
                "producer exclusions differ from contract exact set",
                "bundle-produced.json",
            )
        )
    if produced.get("artifactManifestSha256") != sha256_file(manifest):
        failures.append(
            Failure(
                "ARTIFACT_MANIFEST_MISMATCH",
                "artifact manifest SHA differs from bundle-produced.json",
                "artifact-hashes.sha256",
            )
        )
    entries: dict[str, str] = {}
    raw_paths: list[str] = []
    for line_number, line in enumerate(
        manifest.read_text(encoding="utf-8").splitlines(), 1
    ):
        match = re.fullmatch(r"([0-9a-f]{64})  (.+)", line)
        if not match:
            failures.append(
                Failure(
                    "ARTIFACT_MANIFEST_MISMATCH",
                    f"invalid manifest line {line_number}",
                    "artifact-hashes.sha256",
                )
            )
            continue
        digest, raw_path = match.groups()
        raw_paths.append(raw_path)
        try:
            relative = normalize_relative(raw_path)
        except ValueError as exc:
            failures.append(
                Failure(
                    "ARTIFACT_MANIFEST_MISMATCH",
                    str(exc),
                    "artifact-hashes.sha256",
                )
            )
            continue
        if relative in entries:
            failures.append(
                Failure(
                    "ARTIFACT_MANIFEST_MISMATCH",
                    "duplicate manifest entry",
                    relative,
                )
            )
        entries[relative] = digest
    failures.extend(collision_failures(raw_paths))
    actual_paths = set(pre_snapshot.path_set)
    expected_filesystem = actual_paths - set(exclusions)
    manifest_paths = set(entries)
    for path in sorted(expected_filesystem - manifest_paths):
        failures.append(
            Failure("UNMANIFESTED_FILE", "filesystem file is not manifested", path)
        )
    for path in sorted(manifest_paths - expected_filesystem):
        failures.append(
            Failure(
                "MISSING_MANIFESTED_FILE",
                "manifest entry has no filesystem file",
                path,
            )
        )
    for relative in sorted(manifest_paths & actual_paths):
        path = bundle / relative
        if path.is_symlink():
            failures.append(
                Failure(
                    "ARTIFACT_MANIFEST_MISMATCH",
                    "symlinks are forbidden in evidence bundles",
                    relative,
                )
            )
            continue
        actual_hash = pre_snapshot.files[relative][2]
        if actual_hash != entries[relative]:
            failures.append(
                Failure(
                    "ARTIFACT_MANIFEST_MISMATCH",
                    f"{actual_hash} != {entries[relative]}",
                    relative,
                )
            )
    for rule in contract["requiredArtifactRules"]:
        pattern = str(rule["pattern"])
        matches = sorted(
            path
            for path in manifest_paths
            if fnmatch.fnmatchcase(path, pattern)
        )
        if len(matches) != int(rule["count"]):
            failures.append(
                Failure(
                    "ARTIFACT_MANIFEST_MISMATCH",
                    f"required artifact rule {pattern} matched {len(matches)}; "
                    f"expected {rule['count']}",
                    pattern,
                )
            )
        if rule.get("nonZero"):
            for relative in matches:
                if pre_snapshot.files.get(relative, (0, 0, ""))[0] <= 0:
                    failures.append(
                        Failure(
                            "ARTIFACT_MANIFEST_MISMATCH",
                            "required artifact is zero-byte",
                            relative,
                        )
                    )
    path_set_sha = hashlib.sha256(
        "\n".join(sorted(manifest_paths)).encode("utf-8")
    ).hexdigest()
    if produced.get("filesystemPathSetSha256") != path_set_sha:
        failures.append(
            Failure(
                "ARTIFACT_MANIFEST_MISMATCH",
                "producer filesystem path-set SHA differs from manifest set",
                "bundle-produced.json",
            )
        )
    if int(produced.get("artifactCount", -1)) != len(entries):
        failures.append(
            Failure(
                "ARTIFACT_MANIFEST_MISMATCH",
                "producer artifact count differs from manifest",
                "bundle-produced.json",
            )
        )
    checks.append(f"immutable artifact manifest checked: {len(entries)} files")
    return entries


def api_token(value: str) -> str:
    compact = re.sub(r"[^a-z0-9]", "", value.lower())
    match = re.search(r"(?:direct3d|d3d)(\d+)", compact)
    return f"d3d{match.group(1)}" if match else compact


def verify_environment(
    lane_root: Path,
    lane_id: str,
    failures: list[Failure],
) -> None:
    path = lane_root / "lane-environment.json"
    if not path.is_file():
        failures.append(
            Failure(
                "MISSING_GRAPHICS_ENVIRONMENT",
                "dedicated lane environment record is missing",
                str(path),
            )
        )
        return
    payload = json.loads(path.read_text(encoding="utf-8"))
    required = (
        "unityVersion",
        "operatingSystem",
        "gpu",
        "graphicsApi",
        "featureLevel",
        "driver",
        "colorSpace",
        "captureResolution",
        "graphicsEnabled",
    )
    missing = [
        field
        for field in required
        if payload.get(field) in ("", None)
        or (field == "graphicsEnabled" and payload.get(field) is not True)
    ]
    if missing:
        failures.append(
            Failure(
                "MISSING_GRAPHICS_ENVIRONMENT",
                f"missing/disabled environment fields: {missing}",
                str(path),
            )
        )
        return
    log_paths = payload.get("unityLogPaths", [])
    if not log_paths:
        failures.append(
            Failure(
                "GRAPHICS_ENVIRONMENT_MISMATCH",
                "environment record has no Unity/player log correlation",
                str(path),
            )
        )
        return
    logs = []
    for relative in log_paths:
        try:
            log_path = lane_root / normalize_relative(str(relative))
        except ValueError:
            log_path = Path()
        if not log_path.is_file():
            failures.append(
                Failure(
                    "GRAPHICS_ENVIRONMENT_MISMATCH",
                    "referenced environment log is missing",
                    str(relative),
                )
            )
        else:
            logs.append(log_path.read_text(encoding="utf-8", errors="replace"))
    combined = "\n".join(logs)
    normalized_log = re.sub(r"[^a-z0-9]", "", combined.lower())
    if re.sub(r"[^a-z0-9]", "", str(payload["gpu"]).lower()) not in normalized_log:
        failures.append(
            Failure(
                "GRAPHICS_ENVIRONMENT_MISMATCH",
                "GPU record does not match Unity/player log",
                str(path),
            )
        )
    if api_token(str(payload["graphicsApi"])) not in api_token(combined):
        failures.append(
            Failure(
                "GRAPHICS_ENVIRONMENT_MISMATCH",
                "graphics API record does not match Unity/player log",
                str(path),
            )
        )
    if (
        re.sub(r"[^a-z0-9]", "", str(payload["driver"]).lower())
        not in normalized_log
    ):
        failures.append(
            Failure(
                "GRAPHICS_ENVIRONMENT_MISMATCH",
                "driver record does not match Unity/player log",
                str(path),
            )
        )


def verify_player_result(
    lane_root: Path,
    command: dict[str, object],
    freeze: dict[str, object],
    contract: dict[str, object],
    failures: list[Failure],
) -> None:
    result = one_match(
        lane_root,
        "**/player-visual-result.json",
        failures,
        "PLAYER_RESULT_INCOMPLETE",
    )
    if result is None:
        return
    payload = json.loads(result.read_text(encoding="utf-8"))
    expected_count = int(
        contract["requiredArtifactThresholds"]["playerExpectedCaptureCount"]
    )
    required = (
        "bundleId",
        "sourceFreezeId",
        "laneId",
        "buildExitCode",
        "playerExitCode",
        "captureCount",
        "expectedCaptureCount",
        "matrix",
        "exitAttempts",
        "GPU",
        "graphicsAPI",
        "driver",
        "resolutions",
        "frameRates",
        "captureManifest",
        "playerLog",
        "buildLog",
        "result",
    )
    if any(field not in payload for field in required):
        failures.append(
            Failure(
                "PLAYER_RESULT_INCOMPLETE",
                "player result is missing required fields",
                str(result),
            )
        )
        return
    if (
        payload["bundleId"] != freeze.get("bundleId")
        or payload["sourceFreezeId"] != freeze.get("sourceFreezeId")
        or payload["laneId"] != "player-visual-quality"
        or int(payload["buildExitCode"]) != 0
        or int(payload["playerExitCode"]) != 0
        or int(payload["captureCount"]) != expected_count
        or int(payload["expectedCaptureCount"]) != expected_count
        or payload["result"] != "PASS"
    ):
        failures.append(
            Failure(
                "PLAYER_RESULT_INCOMPLETE",
                "player result identity, exits, count, or verdict is invalid",
                str(result),
            )
        )
    expected_matrix = {
        (width, height, fps, focus)
        for width, height in ((1920, 1080), (3440, 1440))
        for fps in (30, 60, 120)
        for focus in ("center", "offcenter")
    }
    actual_matrix = {
        (
            int(row.get("width", 0)),
            int(row.get("height", 0)),
            int(row.get("frameRate", 0)),
            str(row.get("focus", "")),
        )
        for row in payload.get("matrix", [])
    }
    if actual_matrix != expected_matrix or len(payload.get("matrix", [])) != 12:
        failures.append(
            Failure(
                "PLAYER_RESULT_INCOMPLETE",
                "player visual matrix is not the exact 2x3x2 set",
                str(result),
            )
        )
    matrix_rows = payload.get("matrix", [])
    if any(
        int(row.get("attemptCount", 0)) < 1
        or int(row.get("attemptCount", 0)) > 3
        or int(row.get("finalExitCode", -1)) != 0
        for row in matrix_rows
    ):
        failures.append(
            Failure(
                "PLAYER_RESULT_INCOMPLETE",
                "player matrix final attempt exits are not canonical zero exits",
                str(result),
            )
        )
    attempts = payload.get("exitAttempts", [])
    attempts_by_cell: dict[tuple[int, int, int, str], list[dict[str, object]]] = {}
    for attempt in attempts:
        key = (
            int(attempt.get("width", 0)),
            int(attempt.get("height", 0)),
            int(attempt.get("frameRate", 0)),
            str(attempt.get("focus", "")),
        )
        attempts_by_cell.setdefault(key, []).append(attempt)
    if set(attempts_by_cell) != expected_matrix:
        failures.append(
            Failure(
                "PLAYER_RESULT_INCOMPLETE",
                "player exit-attempt cells are not the exact matrix",
                str(result),
            )
        )
    for key, rows in attempts_by_cell.items():
        indices = [int(row.get("attempt", 0)) for row in rows]
        if (
            indices != list(range(1, len(rows) + 1))
            or len(rows) > 3
            or int(rows[-1].get("exitCode", -1)) != 0
            or not bool(rows[-1].get("passMarker"))
            or any(
                int(row.get("exitCode", -1)) == 0
                and bool(row.get("passMarker"))
                for row in rows[:-1]
            )
        ):
            failures.append(
                Failure(
                    "PLAYER_RESULT_INCOMPLETE",
                    "player exit attempts are incomplete or do not end at exit zero",
                    f"{result}:{key}",
                )
            )
    captures = payload.get("captureManifest", [])
    capture_paths = [str(row.get("path", "")) for row in captures]
    if len(captures) != expected_count or len(set(capture_paths)) != expected_count:
        failures.append(
            Failure(
                "PLAYER_RESULT_INCOMPLETE",
                "player capture manifest count/path uniqueness failed",
                str(result),
            )
        )
    for row in captures:
        try:
            path = result.parent / normalize_relative(str(row.get("path", "")))
        except ValueError:
            path = Path()
        if not path.is_file():
            failures.append(
                Failure(
                    "MISSING_PIXEL_ARTIFACT",
                    "player capture is missing",
                    str(row.get("path", "")),
                )
            )
        elif sha256_file(path) != row.get("sha256"):
            failures.append(
                Failure(
                    "PIXEL_ARTIFACT_HASH_MISMATCH",
                    "player capture SHA mismatch",
                    str(path),
                )
            )
    if int(command.get("exitCode", -1)) != 0:
        failures.append(
            Failure(
                "COMMAND_RESULT_MISMATCH",
                "player command exit is not zero",
                str(result),
            )
        )


def verify_lanes(
    bundle: Path,
    contract: dict[str, object],
    freeze: dict[str, object],
    failures: list[Failure],
    checks: list[str],
) -> list[dict[str, object]]:
    records: list[dict[str, object]] = []
    command_files = sorted((bundle / "01-commands").glob("*.json"))
    for path in command_files:
        try:
            payload = json.loads(path.read_text(encoding="utf-8"))
            payload["_path"] = path
            records.append(payload)
        except Exception as exc:
            failures.append(
                Failure(
                    "COMMAND_RESULT_MISMATCH",
                    f"invalid command record JSON: {exc}",
                    str(path),
                )
            )
    required = set(contract["requiredLaneIds"])
    lane_ids = [str(record.get("laneId", "")) for record in records]
    for lane_id in sorted(required - set(lane_ids)):
        failures.append(
            Failure("MISSING_REQUIRED_LANE", "required lane is absent", lane_id)
        )
    for lane_id in sorted(set(lane_ids) - required):
        failures.append(
            Failure("UNKNOWN_LANE", "uncontracted lane is present", lane_id)
        )
    for lane_id in sorted(set(lane_ids)):
        if lane_ids.count(lane_id) > 1:
            failures.append(
                Failure("DUPLICATE_LANE", "lane ID is duplicated", lane_id)
            )
    command_ids = [str(record.get("commandId", "")) for record in records]
    if len(command_ids) != len(set(command_ids)) or any(not value for value in command_ids):
        failures.append(
            Failure(
                "DUPLICATE_LANE",
                "command IDs are duplicated or empty",
                "01-commands",
            )
        )
    all_result_paths: list[str] = []
    xml_reports: list[dict[str, object]] = []
    graphics_lanes = set(contract["requiredGraphicsLanes"])
    expected_repo = str(repository_for_contract(Path(__file__).resolve().parent / "evidence-contract-v1.json"))
    for record in records:
        lane_id = str(record.get("laneId", ""))
        if lane_id not in required:
            continue
        lane_contract = contract["laneResultContracts"][lane_id]
        lane_root = bundle / "02-lanes" / lane_id
        if record.get("commandArgv") != lane_contract.get("commandArgv"):
            failures.append(
                Failure(
                    "COMMAND_RESULT_MISMATCH",
                    "canonical command argv differs from independent contract",
                    lane_id,
                )
            )
        if (
            record.get("bundleId") != freeze.get("bundleId")
            or record.get("sourceFreezeId") != freeze.get("sourceFreezeId")
            or record.get("workingDirectory") != expected_repo
            or record.get("resultContractKind") != lane_contract.get("kind")
        ):
            failures.append(
                Failure(
                    "COMMAND_RESULT_MISMATCH",
                    "command identity, working directory, freeze, or result kind mismatch",
                    lane_id,
                )
            )
        if int(record.get("exitCode", -999)) not in lane_contract["allowedExitCodes"]:
            failures.append(
                Failure(
                    "COMMAND_RESULT_MISMATCH",
                    f"exit {record.get('exitCode')} is not approved by lane contract",
                    lane_id,
                )
            )
        result_paths = [str(value) for value in record.get("resultPaths", [])]
        all_result_paths.extend(result_paths)
        for relative in result_paths:
            try:
                normalized = normalize_relative(relative)
            except ValueError as exc:
                failures.append(
                    Failure("COMMAND_RESULT_MISMATCH", str(exc), lane_id)
                )
                continue
            if not normalized.startswith(f"02-lanes/{lane_id}/"):
                failures.append(
                    Failure(
                        "COMMAND_RESULT_MISMATCH",
                        "result does not reside in its exact lane root",
                        normalized,
                    )
                )
            if not (bundle / normalized).is_file():
                failures.append(
                    Failure(
                        "MISSING_RESULT_ARTIFACT",
                        "command-referenced result is missing",
                        normalized,
                    )
                )
        lane_result = lane_root / "lane-result.json"
        if result_paths.count(
            lane_result.relative_to(bundle).as_posix()
        ) != 1:
            failures.append(
                Failure(
                    "MISSING_RESULT_ARTIFACT",
                    "lane must reference exactly one canonical lane-result.json",
                    lane_id,
                )
            )
        elif lane_result.is_file():
            payload = json.loads(lane_result.read_text(encoding="utf-8"))
            if (
                payload.get("laneId") != lane_id
                or payload.get("commandId") != record.get("commandId")
                or payload.get("bundleId") != freeze.get("bundleId")
                or payload.get("sourceFreezeId") != freeze.get("sourceFreezeId")
            ):
                failures.append(
                    Failure(
                        "COMMAND_RESULT_MISMATCH",
                        "lane result identity differs from command/freeze",
                        str(lane_result),
                    )
                )
        try:
            start_ns = int(record["startUnixNanoseconds"])
            end_ns = int(record["endUnixNanoseconds"])
        except Exception:
            start_ns = end_ns = 0
            failures.append(
                Failure(
                    "COMMAND_RESULT_MISMATCH",
                    "command interval is missing or invalid",
                    lane_id,
                )
            )
        lane_xml_reports: list[dict[str, object]] = []
        if lane_contract["kind"] == "unity-test-framework":
            for xml_contract in lane_contract["xml"]:
                xml_candidates = sorted(
                    lane_root.rglob(str(xml_contract["path"]))
                )
                if not xml_candidates:
                    failures.append(
                        Failure(
                            "MISSING_RESULT_ARTIFACT",
                            "required Unity XML is missing",
                            str(lane_root / xml_contract["path"]),
                        )
                    )
                    continue
                if len(xml_candidates) != 1:
                    failures.append(
                        Failure(
                            "DUPLICATE_RESULT_ARTIFACT",
                            f"{len(xml_candidates)} candidates for canonical XML",
                            str(xml_contract["path"]),
                        )
                    )
                    continue
                xml_path = xml_candidates[0]
                try:
                    report = parse_xml(xml_path)
                except Exception as exc:
                    failures.append(
                        Failure(
                            "INVALID_XML",
                            f"XML parse failed: {exc}",
                            str(xml_path),
                        )
                    )
                    continue
                report.update({"laneId": lane_id, "path": str(xml_path)})
                lane_xml_reports.append(report)
                xml_reports.append(report)
                if int(report["total"]) <= 0:
                    failures.append(
                        Failure(
                            "ZERO_TEST_RESULT",
                            "Unity XML has zero tests",
                            str(xml_path),
                        )
                    )
                if int(report["total"]) != int(xml_contract["total"]):
                    failures.append(
                        Failure(
                            "COMMAND_RESULT_MISMATCH",
                            f"XML total {report['total']} != exact contract "
                            f"{xml_contract['total']}",
                            str(xml_path),
                        )
                    )
                mtime = xml_path.stat().st_mtime_ns
                if start_ns and not (
                    start_ns - 5_000_000_000
                    <= mtime
                    <= end_ns + 60_000_000_000
                ):
                    failures.append(
                        Failure(
                            "COMMAND_RESULT_MISMATCH",
                            "result timestamp is outside command interval tolerance",
                            str(xml_path),
                        )
                    )
            if lane_id == "ui" and lane_xml_reports:
                actual_failures = set(lane_xml_reports[0]["failedTestIds"])
                approved = set(
                    contract["approvedUiFailurePolicy"][
                        "approvedExpectedFailureIds"
                    ]
                )
                policy_code = ui_failure_policy_code(actual_failures, approved)
                if policy_code == "UNEXPECTED_UI_FAILURE":
                    failures.append(
                        Failure(
                            "UNEXPECTED_UI_FAILURE",
                            f"unexpected failures: {sorted(actual_failures - approved)}",
                            lane_id,
                        )
                    )
                if policy_code == "UI_BASELINE_DRIFT":
                    failures.append(
                        Failure(
                            "UI_BASELINE_DRIFT",
                            f"approved debt disappeared: {sorted(approved - actual_failures)}",
                            lane_id,
                        )
                    )
                expected_exit_code = 1 if approved else 0
                if int(record.get("exitCode", -1)) != expected_exit_code:
                    failures.append(
                        Failure(
                            "COMMAND_RESULT_MISMATCH",
                            "UI exact-set result requires command exit "
                            f"{expected_exit_code}",
                            lane_id,
                        )
                    )
            elif any(report["failedTestIds"] for report in lane_xml_reports):
                failures.append(
                    Failure(
                        "COMMAND_RESULT_MISMATCH",
                        "non-UI lane XML contains failed tests",
                        lane_id,
                    )
                )
            elif int(record.get("exitCode", -1)) != 0:
                failures.append(
                    Failure(
                        "COMMAND_RESULT_MISMATCH",
                        "passing XML requires command exit 0",
                        lane_id,
                    )
                )
        else:
            verify_player_result(
                lane_root, record, freeze, contract, failures
            )
        for log_name in lane_contract["logs"]:
            if log_name == "player-build.log":
                log_matches = list(lane_root.glob("**/player-build.log"))
                if len(log_matches) != 1:
                    failures.append(
                        Failure(
                            "MISSING_RESULT_ARTIFACT",
                            "player build log exact-one check failed",
                            lane_id,
                        )
                    )
            elif not (lane_root / log_name).is_file():
                failures.append(
                    Failure(
                        "MISSING_RESULT_ARTIFACT",
                        "required lane log is missing",
                        str(lane_root / log_name),
                    )
                )
        if lane_id in graphics_lanes:
            verify_environment(lane_root, lane_id, failures)
    for relative in sorted(set(all_result_paths)):
        if all_result_paths.count(relative) > 1:
            failures.append(
                Failure(
                    "DUPLICATE_RESULT_ARTIFACT",
                    "the same canonical result is shared by multiple lanes",
                    relative,
                )
            )
    checks.append(
        f"required lane exact set and one-to-one results checked: {len(records)}"
    )
    return xml_reports


def verify_known_center(
    bundle: Path,
    contract: dict[str, object],
    source_map: dict[str, dict[str, object]],
    failures: list[Failure],
    checks: list[str],
) -> None:
    lane_root = bundle / "02-lanes/known-center-analyzer"
    csv_path = one_match(
        lane_root,
        "**/known-center-fixtures.csv",
        failures,
        "MISSING_PIXEL_ARTIFACT",
    )
    if csv_path is None:
        return
    rows = read_csv(csv_path)
    thresholds = contract["requiredArtifactThresholds"]
    cpu = [row for row in rows if row.get("fixture") == "synthetic"]
    shader = [row for row in rows if row.get("fixture") == "shader"]
    if (
        len(rows) != int(thresholds["knownCenterRowCount"])
        or len(cpu) != int(thresholds["knownCenterCpuFixtureCount"])
        or len(shader) != int(thresholds["knownCenterShaderFixtureCount"])
    ):
        failures.append(
            Failure(
                "MISSING_PIXEL_ARTIFACT",
                f"known-center counts rows/cpu/shader={len(rows)}/{len(cpu)}/{len(shader)}",
                str(csv_path),
            )
        )
    fixture_ids = [row.get("fixture_id", "") for row in rows]
    capture_paths = [row.get("capture_path", "") for row in rows]
    if (
        len(set(fixture_ids)) != len(rows)
        or any(not value for value in fixture_ids)
        or len(set(capture_paths)) != len(rows)
    ):
        failures.append(
            Failure(
                "DUPLICATE_SAMPLE_ID",
                "known-center fixture IDs or capture paths are not unique",
                str(csv_path),
            )
        )
    shader_source = source_map.get(
        "Assets/_Features/UI/UI_Composition/Shaders/TerminalIris.shader", {}
    ).get("sha256")
    material_source = source_map.get(
        "Assets/_Features/UI/UI_Composition/Shaders/TerminalIrisOverlay.mat", {}
    ).get("sha256")
    for row in rows:
        try:
            capture = csv_path.parent / normalize_relative(row["capture_path"])
        except (KeyError, ValueError):
            capture = Path()
        if not capture.is_file():
            failures.append(
                Failure(
                    "MISSING_PIXEL_ARTIFACT",
                    "known-center fixture artifact is missing",
                    str(row.get("capture_path", "")),
                )
            )
            continue
        if sha256_file(capture) != row.get("capture_sha256"):
            failures.append(
                Failure(
                    "PIXEL_ARTIFACT_HASH_MISMATCH",
                    "known-center capture SHA mismatch",
                    str(capture),
                )
            )
        width = int(row["width"])
        height = int(row["height"])
        if row["fixture"] == "synthetic":
            data = capture.read_bytes()
            if (
                len(data) != width * height
                or hashlib.sha256(data).hexdigest()
                != row.get("pixel_buffer_sha256")
            ):
                failures.append(
                    Failure(
                        "PIXEL_ARTIFACT_HASH_MISMATCH",
                        "CPU fixture buffer size/SHA mismatch",
                        str(capture),
                    )
                )
        else:
            try:
                png_width, png_height, pixels = decode_png_rgba(capture)
            except Exception as exc:
                failures.append(
                    Failure(
                        "MISSING_PIXEL_ARTIFACT",
                        f"shader PNG decode failed: {exc}",
                        str(capture),
                    )
                )
                continue
            if (
                png_width != width
                or png_height != height
                or hashlib.sha256(
                    png_to_unity_pixel_order(
                        png_width, png_height, pixels
                    )
                ).hexdigest()
                != row.get("pixel_buffer_sha256")
            ):
                failures.append(
                    Failure(
                        "PIXEL_ARTIFACT_HASH_MISMATCH",
                        "shader PNG dimensions/raw buffer SHA mismatch",
                        str(capture),
                    )
                )
            if (
                row.get("shader_sha256") != shader_source
                or row.get("material_sha256") != material_source
            ):
                failures.append(
                    Failure(
                        "SOURCE_HASH_MISMATCH",
                        "shader row source SHA differs from source freeze",
                        str(capture),
                    )
                )
        accepted = (
            float(row["center_error_pixels"])
            <= float(thresholds["centerErrorPixelsMaximum"])
            and float(row["rms_radial_error_pixels"])
            <= float(thresholds["knownCenterRmsMaximum"])
            and float(row["max_radial_error_pixels"])
            <= float(thresholds["knownCenterMaximumRadialErrorMaximum"])
            and int(row["unexpected_components"]) == 0
            and int(row["opaque_pinholes"]) == 0
            and int(row["transparent_artifacts"]) == 0
        )
        if (row.get("verdict") == "PASS") != accepted:
            failures.append(
                Failure(
                    "PIXEL_ARTIFACT_HASH_MISMATCH",
                    "known-center verdict differs from recalculated thresholds",
                    row.get("fixture_id", ""),
                )
            )
    checks.append(
        f"known-center row/artifact correlation checked: {len(rows)} "
        f"(cpu={len(cpu)}, shader={len(shader)})"
    )


def float_pair_distance(
    first: dict[str, str],
    second: dict[str, str],
    first_prefix: str,
    second_prefix: str,
) -> float:
    dx = float(first[f"{first_prefix}_x"]) - float(second[f"{second_prefix}_x"])
    dy = float(first[f"{first_prefix}_y"]) - float(second[f"{second_prefix}_y"])
    return (dx * dx + dy * dy) ** 0.5


def verify_offcenter(
    bundle: Path,
    contract: dict[str, object],
    failures: list[Failure],
    checks: list[str],
) -> None:
    lane_root = bundle / "02-lanes/production-offcenter-focus"
    metrics_path = one_match(
        lane_root,
        "**/off-center-focus-trace.csv",
        failures,
        "MISSING_PIXEL_ARTIFACT",
    )
    trace_path = one_match(
        lane_root,
        "**/focus-transport-trace.csv",
        failures,
        "TRACE_CORRELATION_MISMATCH",
    )
    if metrics_path is None or trace_path is None:
        return
    rows = read_csv(metrics_path)
    traces = read_csv(trace_path)
    sample_ids = [row.get("sample_id", "") for row in rows]
    trace_ids = [row.get("trace_row_id", "") for row in traces]
    if len(rows) != 12 or len(set(sample_ids)) != 12 or any(not x for x in sample_ids):
        failures.append(
            Failure(
                "DUPLICATE_SAMPLE_ID",
                "off-center SampleId set is not exactly 12 unique values",
                str(metrics_path),
            )
        )
    if len(traces) != 12 or len(set(trace_ids)) != 12:
        failures.append(
            Failure(
                "TRACE_CORRELATION_MISMATCH",
                "focus trace row IDs are not exactly 12 unique values",
                str(trace_path),
            )
        )
    matrix_contract = contract["offCenterMatrix"]
    expected_matrix = offcenter_matrix(
        matrix_contract["directions"], matrix_contract["repetitions"]
    )
    actual_matrix = {(row.get("direction"), row.get("run")) for row in rows}
    if actual_matrix != expected_matrix or len(rows) != len(expected_matrix):
        failures.append(
            Failure(
                "OFFCENTER_MATRIX_INCOMPLETE",
                f"{sorted(actual_matrix)} != {sorted(expected_matrix)}",
                str(metrics_path),
            )
        )
    trace_by_id = {row.get("trace_row_id", ""): row for row in traces}
    thresholds = contract["requiredArtifactThresholds"]
    quality_root = metrics_path.parent.parent
    capture_paths: list[str] = []
    correlation_fields = (
        "evidence_bundle_id",
        "evidence_run_id",
        "sample_id",
        "projected_x",
        "projected_y",
        "request_x",
        "request_y",
        "session_x",
        "session_y",
        "overlay_x",
        "overlay_y",
        "material_x",
        "material_y",
        "overlay_instance_id",
        "camera_instance_id",
        "projection_success",
        "fallback_reason",
    )
    for row in rows:
        trace_id = row.get("trace_row_id", "")
        trace = trace_by_id.get(trace_id)
        if trace is None or any(row.get(field) != trace.get(field) for field in correlation_fields):
            failures.append(
                Failure(
                    "TRACE_CORRELATION_MISMATCH",
                    "metric row and focus trace row differ",
                    row.get("sample_id", ""),
                )
            )
        relative = row.get("artifact_path", "")
        capture_paths.append(relative)
        try:
            capture = quality_root / normalize_relative(relative)
        except ValueError:
            capture = Path()
        if not capture.is_file():
            failures.append(
                Failure(
                    "MISSING_PIXEL_ARTIFACT",
                    "off-center capture is missing",
                    relative,
                )
            )
        elif sha256_file(capture) != row.get("capture_sha256"):
            failures.append(
                Failure(
                    "PIXEL_ARTIFACT_HASH_MISMATCH",
                    "off-center row/PNG SHA mismatch",
                    relative,
                )
            )
        tolerance = float(thresholds["focusTransportTolerance"])
        transport_ok = (
            float_pair_distance(row, row, "request", "session") <= tolerance
            and float_pair_distance(row, row, "session", "overlay") <= tolerance
            and float_pair_distance(row, row, "overlay", "material") <= tolerance
        )
        projected_error = float_pair_distance(row, row, "expected", "projected")
        accepted = (
            row.get("projection_success") == "true"
            and row.get("fallback_reason") == "None"
            and transport_ok
            and projected_error <= 0.025
            and float(row["center_error_pixels"])
            <= float(thresholds["centerErrorPixelsMaximum"])
            and float(row["rms_radial_error_pixels"])
            <= float(thresholds["offCenterRmsMaximum"])
            and float(row["max_radial_error_pixels"])
            <= float(thresholds["offCenterMaximumRadialErrorMaximum"])
            and float(row["p99_radial_error_pixels"])
            <= float(thresholds["offCenterP99Maximum"])
            and int(row["unexpected_components"]) == 0
            and int(row["opaque_pinholes"]) == 0
            and int(row["transparent_artifacts"]) == 0
        )
        if (row.get("verdict") == "PASS") != accepted:
            failures.append(
                Failure(
                    "TRACE_CORRELATION_MISMATCH",
                    "off-center verdict differs from independent recalculation",
                    row.get("sample_id", ""),
                )
            )
    if len(set(capture_paths)) != 12:
        failures.append(
            Failure(
                "DUPLICATE_SAMPLE_ID",
                "off-center capture paths are not unique",
                str(metrics_path),
            )
        )
    checks.append("off-center 12-row PNG/focus-trace exact correlation checked")


def unity_approximately(first: float, second: float) -> bool:
    return abs(second - first) < max(
        1e-6 * max(abs(first), abs(second)), 1.121039e-44
    )


def recompute_selector(rows: list[dict[str, str]]) -> dict[str, object]:
    sequences = [int(row["render_sequence_index"]) for row in rows]
    missing = any(
        sequences[index] - sequences[index - 1] > 1
        for index in range(1, len(sequences))
    )
    duplicate = len(sequences) != len(set(sequences)) or any(
        sequences[index] <= sequences[index - 1]
        for index in range(1, len(sequences))
    )
    last_animated = -1
    for index in range(len(rows) - 1):
        changed = any(
            not unity_approximately(float(rows[index][field]), float(rows[index + 1][field]))
            for field in ("input_radius", "effective_radius", "closed_overshoot")
        )
        if changed:
            last_animated = sequences[index]
    last_changing = -1
    for index, row in enumerate(rows[:-1]):
        if int(row["next_changed_pixels"]) > 0:
            last_changing = sequences[index]
    first_closed = -1
    for index, row in enumerate(rows[:-1]):
        if (
            row["authoring_exact_closed"].lower() == "true"
            and int(row["transparent_pixel_count"]) == 0
            and float(row["contour_radius"]) <= 0
            and int(row["next_changed_pixels"]) == 0
            and row["capture_sha256"] == rows[index + 1]["capture_sha256"]
        ):
            first_closed = sequences[index]
            break
    next_stable = first_closed + 1 if first_closed >= 0 else -1
    changed_after = False
    if first_closed >= 0:
        first_index = sequences.index(first_closed)
        for row in rows[first_index:]:
            changed_after |= (
                int(row["next_changed_pixels"]) != 0
                or int(row["next_max_channel_delta"]) != 0
                or int(row["next_unexpected_chroma_pixels"]) != 0
                or int(row["transparent_pixel_count"]) != 0
            )
    return {
        "lastAnimatedParameterFrame": last_animated,
        "lastPixelChangingFrame": last_changing,
        "firstExactClosedFrame": first_closed,
        "nextStableClosedFrame": next_stable,
        "missingFrameIndex": missing,
        "duplicateFrameIndex": duplicate,
        "changedAfterStableClosed": changed_after,
    }


def canonical_difference(first: bytes, second: bytes) -> bytes:
    if len(first) != len(second) or len(first) % 4:
        raise ValueError("RGBA buffers are not comparable")
    output = bytearray(len(first))
    for index in range(0, len(first), 4):
        delta = max(
            abs(second[index + channel] - first[index + channel])
            for channel in range(4)
        )
        output[index : index + 4] = bytes((delta, 0, 0, 255))
    return bytes(output)


def verify_final_close(
    bundle: Path,
    contract: dict[str, object],
    failures: list[Failure],
    checks: list[str],
) -> None:
    lane_root = bundle / "02-lanes/final-close-frames"
    selections = sorted(lane_root.glob("**/frame-selection.json"))
    intents: set[str] = set()
    for selection_path in selections:
        selection = json.loads(selection_path.read_text(encoding="utf-8"))
        intent = str(selection.get("intent", ""))
        intents.add(intent)
        metrics_path = selection_path.parent / "frame-metrics.csv"
        if not metrics_path.is_file():
            failures.append(
                Failure(
                    "MISSING_RESULT_ARTIFACT",
                    "final-close frame metrics are missing",
                    str(metrics_path),
                )
            )
            continue
        rows = read_csv(metrics_path)
        by_index = {int(row["render_sequence_index"]): row for row in rows}
        capture_paths: list[str] = []
        for row in rows:
            relative = row.get("capture_path", "")
            capture_paths.append(relative)
            try:
                capture = selection_path.parent / normalize_relative(relative)
            except ValueError:
                capture = Path()
            if not capture.is_file():
                failures.append(
                    Failure(
                        "MISSING_PIXEL_ARTIFACT",
                        "raw final-close frame is missing",
                        relative,
                    )
                )
                continue
            if sha256_file(capture) != row.get("capture_sha256"):
                failures.append(
                    Failure(
                        "PIXEL_ARTIFACT_HASH_MISMATCH",
                        "raw final-close frame SHA mismatch",
                        str(capture),
                    )
                )
            try:
                width, height, _ = decode_png_rgba(capture)
            except Exception as exc:
                failures.append(
                    Failure(
                        "MISSING_PIXEL_ARTIFACT",
                        f"raw frame PNG decode failed: {exc}",
                        str(capture),
                    )
                )
                continue
            if width != int(row["render_width"]) or height != int(row["render_height"]):
                failures.append(
                    Failure(
                        "PIXEL_ARTIFACT_HASH_MISMATCH",
                        "raw frame dimensions differ from CSV",
                        str(capture),
                    )
                )
        if len(capture_paths) != len(set(capture_paths)):
            failures.append(
                Failure(
                    "FINAL_SELECTOR_MISMATCH",
                    "raw frame capture paths are duplicated",
                    str(metrics_path),
                )
            )
        recomputed = recompute_selector(rows)
        reported = {
            key: selection.get(key)
            for key in recomputed
        }
        if reported != recomputed:
            failures.append(
                Failure(
                    "FINAL_SELECTOR_MISMATCH",
                    f"reported={reported} recomputed={recomputed}",
                    str(selection_path),
                )
            )
        selected_frames = selection.get("selectedFrames", [])
        expected_selectors = {
            "lastAnimatedParameter": recomputed["lastAnimatedParameterFrame"],
            "lastPixelChanging": recomputed["lastPixelChangingFrame"],
            "firstExactClosed": recomputed["firstExactClosedFrame"],
            "nextStableClosed": recomputed["nextStableClosedFrame"],
        }
        if {
            row.get("selector") for row in selected_frames
        } != set(expected_selectors):
            failures.append(
                Failure(
                    "FINAL_SELECTOR_MISMATCH",
                    "selected-frame selector set mismatch",
                    str(selection_path),
                )
            )
        for selected in selected_frames:
            selector = str(selected.get("selector", ""))
            row_id = int(selected.get("rowId", -1))
            row = by_index.get(row_id)
            try:
                selected_png = selection_path.parent / normalize_relative(
                    str(selected.get("path", ""))
                )
            except ValueError:
                selected_png = Path()
            if (
                row is None
                or expected_selectors.get(selector) != row_id
                or not selected_png.is_file()
                or sha256_file(selected_png) != selected.get("sha256")
                or selected.get("sha256") != row.get("capture_sha256")
            ):
                failures.append(
                    Failure(
                        "FINAL_SELECTOR_MISMATCH",
                        "selected PNG row/index/SHA correlation failed",
                        selector,
                    )
                )
        heatmap_manifest = selection_path.parent / "heatmap-manifest.json"
        if not heatmap_manifest.is_file():
            failures.append(
                Failure(
                    "HEATMAP_CORRELATION_MISMATCH",
                    "heatmap manifest is missing",
                    str(heatmap_manifest),
                )
            )
            continue
        heatmaps = json.loads(heatmap_manifest.read_text(encoding="utf-8"))
        if (
            heatmaps.get("differenceAlgorithmVersion")
            != contract["heatmapPolicy"]["differenceAlgorithmVersion"]
            or len(heatmaps.get("heatmaps", [])) != 2
        ):
            failures.append(
                Failure(
                    "HEATMAP_CORRELATION_MISMATCH",
                    "heatmap policy/version/count mismatch",
                    str(heatmap_manifest),
                )
            )
        for heatmap in heatmaps.get("heatmaps", []):
            try:
                first_path = selection_path.parent / normalize_relative(
                    heatmap["sourceFrameAPath"]
                )
                second_path = selection_path.parent / normalize_relative(
                    heatmap["sourceFrameBPath"]
                )
                stored_path = selection_path.parent / normalize_relative(
                    heatmap["heatmapPath"]
                )
                width_a, height_a, first = decode_png_rgba(first_path)
                width_b, height_b, second = decode_png_rgba(second_path)
                width_h, height_h, stored = decode_png_rgba(stored_path)
                difference = canonical_difference(first, second)
            except Exception as exc:
                failures.append(
                    Failure(
                        "HEATMAP_CORRELATION_MISMATCH",
                        f"heatmap decode/recompute failed: {exc}",
                        str(heatmap_manifest),
                    )
                )
                continue
            correlated = (
                sha256_file(first_path) == heatmap.get("sourceFrameASha256")
                and sha256_file(second_path) == heatmap.get("sourceFrameBSha256")
                and sha256_file(stored_path) == heatmap.get("heatmapSha256")
                and hashlib.sha256(
                    png_to_unity_pixel_order(
                        width_a, height_a, difference
                    )
                ).hexdigest()
                == heatmap.get("canonicalDifferenceBufferSha256")
                and stored == difference
                and (width_a, height_a) == (width_b, height_b)
                == (width_h, height_h)
            )
            if not correlated:
                failures.append(
                    Failure(
                        "HEATMAP_CORRELATION_MISMATCH",
                        "heatmap source/hash/recomputed buffer correlation failed",
                        str(stored_path),
                    )
                )
    if intents != set(contract["finalCloseIntentSet"]) or len(selections) != 2:
        failures.append(
            Failure(
                "FINAL_SELECTOR_MISMATCH",
                f"final intent set {sorted(intents)} is not exact",
                str(lane_root),
            )
        )
    checks.append(
        f"final-close selectors independently recalculated: {len(selections)}"
    )


def verify_core(
    bundle: Path,
    contract_path: Path,
    contract: dict[str, object],
    pre_snapshot: BundleSnapshot,
) -> tuple[list[Failure], list[str], list[dict[str, object]], dict[str, object]]:
    failures: list[Failure] = []
    checks: list[str] = []
    if not bundle.is_dir():
        return (
            [
                Failure(
                    "ARTIFACT_MANIFEST_MISMATCH",
                    "bundle root does not exist",
                    str(bundle),
                )
            ],
            checks,
            [],
            {},
        )
    if any(path.is_symlink() for path in bundle.rglob("*")):
        failures.append(
            Failure(
                "ARTIFACT_MANIFEST_MISMATCH",
                "bundle contains a symlink",
                str(bundle),
            )
        )
    verify_artifact_manifest(bundle, contract, pre_snapshot, failures, checks)
    freeze, source_map, _ = verify_source_freeze(
        bundle, contract_path, contract, failures, checks
    )
    xml_reports = verify_lanes(
        bundle, contract, freeze, failures, checks
    )
    verify_known_center(bundle, contract, source_map, failures, checks)
    verify_offcenter(bundle, contract, failures, checks)
    verify_final_close(bundle, contract, failures, checks)
    return failures, checks, xml_reports, freeze


def run_verification(
    bundle: Path,
    contract_path: Path,
    output: Path,
    mutation_hook: Callable[[Path], None] | None = None,
) -> tuple[int, dict[str, object]]:
    bundle = bundle.resolve()
    contract_path = contract_path.resolve()
    output = output.resolve()
    if output == bundle or output.is_relative_to(bundle):
        raise ValueError("verification output must be outside the input bundle")
    contract = json.loads(contract_path.read_text(encoding="utf-8"))
    output.mkdir(parents=True, exist_ok=False)
    verification_id = f"TICEIV-{datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')}"
    command_text = shlex_join(
        [
            "python3",
            str(Path(__file__).resolve()),
            "--bundle",
            str(bundle),
            "--contract",
            str(contract_path),
            "--output",
            str(output),
        ]
    )
    (output / "verification-command.txt").write_text(
        command_text + "\n", encoding="utf-8"
    )
    (output / "verification-environment.txt").write_text(
        "\n".join(
            [
                f"VerificationId={verification_id}",
                f"UTC={utc_now()}",
                f"Python={sys.version}",
                f"Platform={platform.platform()}",
                f"ContractSHA256={sha256_file(contract_path)}",
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    before = snapshot_bundle(bundle)
    if mutation_hook is not None:
        mutation_hook(bundle)
    failures, checks, xml_reports, freeze = verify_core(
        bundle, contract_path, contract, before
    )
    after = snapshot_bundle(bundle)
    difference = snapshot_difference(before, after)
    unchanged = not any(difference.values())
    if not unchanged:
        failures.append(
            Failure(
                "VERIFIER_MUTATED_INPUT",
                f"input changed during verification: {difference}",
                str(bundle),
            )
        )
    failure_codes = sorted({failure.code for failure in failures})
    status = "PASS" if not failures else "BLOCKED"
    report = {
        "schemaVersion": 2,
        "verificationId": verification_id,
        "bundleId": freeze.get("bundleId", ""),
        "sourceFreezeId": freeze.get("sourceFreezeId", ""),
        "contractVersion": contract.get("contractVersion", ""),
        "contractSha256": sha256_file(contract_path),
        "bundleRoot": str(bundle),
        "verificationRoot": str(output),
        "status": status,
        "failureCodes": failure_codes,
        "failures": [failure.as_dict() for failure in failures],
        "checks": checks,
        "xmlSummaries": xml_reports,
        "inputBundleUnchanged": unchanged,
        "inputSnapshotBefore": before.summary(),
        "inputSnapshotAfter": after.summary(),
        "inputSnapshotDifference": difference,
        "completedUtc": utc_now(),
    }
    (output / "bundle-verification.json").write_text(
        json.dumps(report, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    markdown = [
        "# Terminal Iris Evidence Bundle Verification",
        "",
        f"- Verification: `{verification_id}`",
        f"- Bundle: `{report['bundleId']}`",
        f"- Result: **{status}**",
        f"- Input bundle unchanged: **{unchanged}**",
        f"- Contract: `{report['contractVersion']}`",
        "",
        "## Checks",
        "",
        *[f"- {check}" for check in checks],
        "",
        "## Failures",
        "",
        *(
            [
                f"- `{failure.code}` — {failure.message} "
                f"(`{failure.path}`)"
                for failure in failures
            ]
            if failures
            else ["- None"]
        ),
        "",
        "## Input snapshot",
        "",
        f"- Before aggregate: `{before.aggregate_digest}`",
        f"- After aggregate: `{after.aggregate_digest}`",
        f"- File count: `{before.file_count}` → `{after.file_count}`",
        f"- Total size: `{before.total_size}` → `{after.total_size}`",
        "",
    ]
    (output / "bundle-verification-report.md").write_text(
        "\n".join(markdown), encoding="utf-8"
    )
    (output / "hash-check.log").write_text(
        (
            f"{status}\n"
            f"ManifestSHA256={before.artifact_manifest_sha256}\n"
            f"BeforeAggregate={before.aggregate_digest}\n"
            f"AfterAggregate={after.aggregate_digest}\n"
            f"InputBundleUnchanged={str(unchanged).lower()}\n"
            f"FailureCodes={','.join(failure_codes)}\n"
        ),
        encoding="utf-8",
    )
    print(f"bundle verification: {status}")
    print(f"verification output: {output}")
    print(f"input bundle unchanged: {unchanged}")
    return (0 if not failures else 1), report


def shlex_join(values: list[str]) -> str:
    import shlex

    return shlex.join(values)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--bundle", required=True, type=Path)
    parser.add_argument("--contract", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        code, _ = run_verification(args.bundle, args.contract, args.output)
        return code
    except Exception as exc:
        print(f"verifier invocation error: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
