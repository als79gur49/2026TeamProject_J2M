#!/usr/bin/env python3
from __future__ import annotations

import argparse
import csv
from dataclasses import dataclass
from datetime import datetime, timezone
import fnmatch
import hashlib
import json
import math
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
    "SOURCE_TRACKED_MODIFICATION",
    "SOURCE_STAGED_MODIFICATION",
    "SOURCE_UNTRACKED_ENTRY",
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

EXPECTED_PLAYER_CAPTURE_LABELS = frozenset(
    {
        "victory-close-large",
        "victory-close-mid",
        "victory-close-small",
        "victory-close-last-visible",
        "victory-close-fully-closed",
        "defeat-close-mid",
        "defeat-close-small",
        "defeat-close-last-visible",
        "defeat-close-fully-closed",
        "defeat-reveal-first-visible",
        "defeat-reveal-mid",
        "defeat-reveal-fully-open",
        "stage-entry-fully-closed",
        "stage-entry-first-visible",
        "stage-entry-mid",
        "stage-entry-fully-open",
    }
)


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
    summary_failures: list[dict[str, object]] = []

    for node in root.iter():
        local_name = node.tag.rsplit("}", 1)[-1]
        if local_name not in {
            "test-run",
            "test-results",
            "test-suite",
            "result-summary",
            "counters",
        }:
            continue
        for attribute in ("failed", "failures"):
            if attribute not in node.attrib:
                continue
            raw_value = node.attrib[attribute]
            if not raw_value.isdigit():
                raise ValueError(
                    f"invalid non-negative XML failure summary "
                    f"{local_name}@{attribute}={raw_value!r}"
                )
            value = int(raw_value)
            if value > 0:
                summary_failures.append(
                    {
                        "node": local_name,
                        "attribute": attribute,
                        "value": value,
                    }
                )
        if local_name in {"test-run", "test-suite"} and node.attrib.get(
            "result"
        ) in {"Failed", "Failure", "Error"}:
            summary_failures.append(
                {
                    "node": local_name,
                    "attribute": "result",
                    "value": node.attrib["result"],
                }
            )

    return {
        "total": int(root.attrib.get("total", "0")),
        "passed": int(root.attrib.get("passed", "0")),
        "failed": int(
            root.attrib.get("failed", root.attrib.get("failures", "0"))
        ),
        "skipped": int(
            root.attrib.get("skipped", root.attrib.get("inconclusive", "0"))
        ),
        "failedSummaryNodes": summary_failures,
        "failedTestIds": sorted(
            node.attrib.get("fullname", "")
            for node in root.iter("test-case")
            if node.attrib.get("result") == "Failed"
        ),
    }


def xml_report_has_failure(report: dict[str, object]) -> bool:
    return (
        int(report["failed"]) > 0
        or bool(report["failedSummaryNodes"])
        or bool(report["failedTestIds"])
    )


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


def count_components(
    mask: list[bool], width: int, height: int
) -> int:
    if len(mask) != width * height:
        raise ValueError("component mask dimensions do not match")
    visited = bytearray(len(mask))
    components = 0
    for start, enabled in enumerate(mask):
        if not enabled or visited[start]:
            continue
        components += 1
        visited[start] = 1
        pending = [start]
        while pending:
            current = pending.pop()
            x = current % width
            y = current // width
            for neighbor_x, neighbor_y in (
                (x - 1, y),
                (x + 1, y),
                (x, y - 1),
                (x, y + 1),
            ):
                if (
                    neighbor_x < 0
                    or neighbor_x >= width
                    or neighbor_y < 0
                    or neighbor_y >= height
                ):
                    continue
                neighbor = neighbor_y * width + neighbor_x
                if mask[neighbor] and not visited[neighbor]:
                    visited[neighbor] = 1
                    pending.append(neighbor)
    return components


def dominant_rgb(pixels: bytes) -> tuple[int, int, int]:
    if len(pixels) % 4:
        raise ValueError("RGBA buffer length is invalid")
    counts: dict[tuple[int, int, int], int] = {}
    for index in range(0, len(pixels), 4):
        value = (pixels[index], pixels[index + 1], pixels[index + 2])
        counts[value] = counts.get(value, 0) + 1
    if not counts:
        raise ValueError("RGBA buffer is empty")
    return max(counts, key=lambda value: counts[value])


def resolve_coverage_from_rgba(
    pixels: bytes,
    opaque_cover: tuple[int, int, int] | None = None,
) -> list[float]:
    cover = opaque_cover or dominant_rgb(pixels)
    background = (255, 255, 255)
    delta = tuple((cover[channel] - background[channel]) / 255.0 for channel in range(3))
    denominator = sum(value * value for value in delta)
    if denominator <= 0.000001:
        raise ValueError("opaque cover cannot equal the white capture background")
    coverage: list[float] = []
    for index in range(0, len(pixels), 4):
        sample = tuple(pixels[index + channel] / 255.0 for channel in range(3))
        projected = sum(
            (sample[channel] - background[channel] / 255.0) * delta[channel]
            for channel in range(3)
        ) / denominator
        coverage.append(min(1.0, max(0.0, projected)))
    return coverage


def dominant_non_background_rgb(
    decoded_frames: list[tuple[int, int, bytes]],
) -> tuple[int, int, int]:
    counts: dict[tuple[int, int, int], int] = {}
    for _, _, pixels in decoded_frames:
        for index in range(0, len(pixels), 4):
            value = (pixels[index], pixels[index + 1], pixels[index + 2])
            if value == (255, 255, 255):
                continue
            counts[value] = counts.get(value, 0) + 1
    if not counts:
        raise ValueError(
            "raw frame sequence contains no non-background opaque-cover color"
        )
    return max(counts, key=lambda value: counts[value])


def expected_synthetic_coverage_bytes(
    width: int,
    height: int,
    center: tuple[float, float],
    radius_viewport_height: float,
    edge_width_pixels: float,
) -> bytes:
    center_x = center[0] * (width - 1)
    center_y = center[1] * (height - 1)
    radius = radius_viewport_height * height
    output = bytearray(width * height)
    for y in range(height):
        for x in range(width):
            signed_distance = math.hypot(x - center_x, y - center_y) - radius
            if edge_width_pixels <= 0.0:
                value = 1.0 if signed_distance >= 0.0 else 0.0
            else:
                t = min(
                    1.0,
                    max(
                        0.0,
                        (signed_distance + edge_width_pixels * 0.5)
                        / edge_width_pixels,
                    ),
                )
                value = t * t * (3.0 - 2.0 * t)
            output[y * width + x] = round(
                min(1.0, max(0.0, value)) * 255.0
            )
    return bytes(output)


def solve_linear3(matrix: list[list[float]], vector: list[float]) -> list[float]:
    augmented = [matrix[row][:] + [vector[row]] for row in range(3)]
    for pivot in range(3):
        largest = max(range(pivot, 3), key=lambda row: abs(augmented[row][pivot]))
        if abs(augmented[largest][pivot]) <= 1e-9:
            raise ValueError("coverage contour circle-fit matrix is singular")
        if largest != pivot:
            augmented[pivot], augmented[largest] = (
                augmented[largest],
                augmented[pivot],
            )
        divisor = augmented[pivot][pivot]
        for column in range(pivot, 4):
            augmented[pivot][column] /= divisor
        for row in range(3):
            if row == pivot:
                continue
            factor = augmented[row][pivot]
            for column in range(pivot, 4):
                augmented[row][column] -= factor * augmented[pivot][column]
    return [augmented[row][3] for row in range(3)]


def percentile(sorted_values: list[float], fraction: float) -> float:
    position = min(1.0, max(0.0, fraction)) * (len(sorted_values) - 1)
    lower = math.floor(position)
    upper = math.ceil(position)
    return sorted_values[lower] + (
        sorted_values[upper] - sorted_values[lower]
    ) * (position - lower)


def analyze_coverage(
    coverage: list[float],
    width: int,
    height: int,
    expected_center: tuple[float, float],
) -> dict[str, float | int]:
    if width <= 1 or height <= 1 or len(coverage) != width * height:
        raise ValueError("coverage dimensions are invalid")
    contour: list[tuple[float, float]] = []
    for y in range(height):
        for x in range(width - 1):
            first = coverage[y * width + x]
            second = coverage[y * width + x + 1]
            if (first < 0.5 <= second) or (second < 0.5 <= first):
                difference = second - first
                fraction = 0.5 if abs(difference) <= 0.000001 else min(
                    1.0, max(0.0, (0.5 - first) / difference)
                )
                contour.append((x + fraction, float(y)))
    for x in range(width):
        for y in range(height - 1):
            first = coverage[y * width + x]
            second = coverage[(y + 1) * width + x]
            if (first < 0.5 <= second) or (second < 0.5 <= first):
                difference = second - first
                fraction = 0.5 if abs(difference) <= 0.000001 else min(
                    1.0, max(0.0, (0.5 - first) / difference)
                )
                contour.append((float(x), y + fraction))
    if len(contour) < 16:
        raise ValueError(
            f"alpha-0.5 coverage contour has only {len(contour)} points"
        )
    count = float(len(contour))
    sum_x = sum(point[0] for point in contour)
    sum_y = sum(point[1] for point in contour)
    sum_xx = sum(point[0] * point[0] for point in contour)
    sum_xy = sum(point[0] * point[1] for point in contour)
    sum_yy = sum(point[1] * point[1] for point in contour)
    radii_squared = [point[0] * point[0] + point[1] * point[1] for point in contour]
    sum_radius_squared = sum(radii_squared)
    solution = solve_linear3(
        [
            [count, sum_x, sum_y],
            [sum_x, sum_xx, sum_xy],
            [sum_y, sum_xy, sum_yy],
        ],
        [
            -sum_radius_squared,
            -sum(point[0] * radius for point, radius in zip(contour, radii_squared)),
            -sum(point[1] * radius for point, radius in zip(contour, radii_squared)),
        ],
    )
    center_x = -0.5 * solution[1]
    center_y = -0.5 * solution[2]
    radius = math.sqrt(max(0.0, center_x * center_x + center_y * center_y - solution[0]))
    radial_errors = sorted(
        abs(math.hypot(point[0] - center_x, point[1] - center_y) - radius)
        for point in contour
    )
    expected_x = expected_center[0] * max(1, width - 1)
    expected_y = expected_center[1] * max(1, height - 1)
    inner_radius = max(0.0, radius - 2.0)
    outer_radius = radius + 2.0
    opaque_pinholes = 0
    transparent_artifacts = 0
    transparent_mask = [value < 0.1 for value in coverage]
    for index, value in enumerate(coverage):
        distance = math.hypot(index % width - center_x, index // width - center_y)
        if distance < inner_radius and value > 0.9:
            opaque_pinholes += 1
        elif distance > outer_radius and value < 0.1:
            transparent_artifacts += 1
    transparent_components = count_components(
        transparent_mask, width, height
    )
    return {
        "measured_x": center_x / max(1, width - 1),
        "measured_y": center_y / max(1, height - 1),
        "center_error_pixels": math.hypot(center_x - expected_x, center_y - expected_y),
        "rms_radial_error_pixels": math.sqrt(
            sum(error * error for error in radial_errors) / len(radial_errors)
        ),
        "max_radial_error_pixels": radial_errors[-1],
        "p99_radial_error_pixels": percentile(radial_errors, 0.99),
        "transparent_pixel_count": sum(transparent_mask),
        "unexpected_components": max(0, transparent_components - 1),
        "opaque_pinholes": opaque_pinholes,
        "transparent_artifacts": transparent_artifacts,
        "contour_point_count": len(contour),
        "contour_radius": radius,
    }


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


def source_worktree_failures(status_lines: Iterable[str]) -> list[Failure]:
    failures: list[Failure] = []
    for line in status_lines:
        status = line[:2]
        path = line[3:] if len(line) > 3 else ""
        if status == "??":
            code = "SOURCE_UNTRACKED_ENTRY"
        elif status[:1] != " ":
            code = "SOURCE_STAGED_MODIFICATION"
        else:
            code = "SOURCE_TRACKED_MODIFICATION"
        failures.append(
            Failure(
                code,
                f"repository status {status!r} is not clean",
                path,
            )
        )
    return failures


def source_identity_failures(
    freeze: dict[str, object],
    repo: Path,
    actual_head: str,
    actual_tree: str,
) -> list[Failure]:
    failures: list[Failure] = []
    expected_head = str(freeze.get("head", ""))
    expected_tree = str(freeze.get("tree", ""))
    expected_repository = str(freeze.get("repository", ""))
    if not expected_head or not expected_tree or not expected_repository:
        failures.append(
            Failure(
                "SOURCE_FREEZE_DRIFT",
                "source freeze is missing repository, head, or tree identity",
                str(repo),
            )
        )
        return failures
    try:
        frozen_repo = Path(expected_repository).resolve(strict=True)
    except (OSError, RuntimeError):
        frozen_repo = Path(expected_repository).resolve()
    if frozen_repo != repo.resolve():
        failures.append(
            Failure(
                "SOURCE_REPOSITORY_MISMATCH",
                f"expected frozen repository {frozen_repo}; "
                f"actual repository {repo.resolve()}",
                str(repo),
            )
        )
    if actual_head != expected_head:
        failures.append(
            Failure(
                "SOURCE_HEAD_MISMATCH",
                f"expected frozen head {expected_head}; "
                f"actual repository head {actual_head}; "
                f"expected tree {expected_tree}; actual tree {actual_tree}",
                str(repo),
            )
        )
    if actual_tree != expected_tree:
        failures.append(
            Failure(
                "SOURCE_TREE_MISMATCH",
                f"expected frozen tree {expected_tree}; "
                f"actual repository tree {actual_tree}",
                str(repo),
            )
        )
    return failures


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
    actual_head = subprocess.check_output(
        ["git", "rev-parse", "HEAD"], cwd=repo
    ).decode().strip()
    actual_tree = subprocess.check_output(
        ["git", "rev-parse", "HEAD^{tree}"], cwd=repo
    ).decode().strip()
    repository_status = subprocess.check_output(
        ["git", "status", "--porcelain=v1", "-uall"], cwd=repo
    ).decode().splitlines()
    failures.extend(
        source_identity_failures(
            freeze,
            repo,
            actual_head,
            actual_tree,
        )
    )
    failures.extend(source_worktree_failures(repository_status))
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
    checks.append(
        f"source identity checked: head={actual_head} tree={actual_tree} "
        f"repository={repo}"
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
    captures_by_cell: dict[
        tuple[int, int, int, str], list[dict[str, object]]
    ] = {}
    for row in captures:
        key = (
            int(row.get("width", 0)),
            int(row.get("height", 0)),
            int(row.get("frameRate", 0)),
            str(row.get("focus", "")),
        )
        captures_by_cell.setdefault(key, []).append(row)
    if expected_count != len(expected_matrix) * len(
        EXPECTED_PLAYER_CAPTURE_LABELS
    ):
        failures.append(
            Failure(
                "PLAYER_RESULT_INCOMPLETE",
                "player expected capture count differs from the canonical "
                "matrix-by-label product",
                str(result),
            )
        )
    if set(captures_by_cell) != expected_matrix:
        failures.append(
            Failure(
                "PLAYER_RESULT_INCOMPLETE",
                "player capture cells are not the exact matrix",
                str(result),
            )
        )
    for key in sorted(expected_matrix):
        width, height, frame_rate, focus = key
        rows = captures_by_cell.get(key, [])
        labels = [str(row.get("label", "")) for row in rows]
        expected_directory = (
            f"Player-{width}x{height}-{frame_rate}fps-{focus}"
        )
        if (
            len(rows) != len(EXPECTED_PLAYER_CAPTURE_LABELS)
            or len(labels) != len(set(labels))
            or set(labels) != EXPECTED_PLAYER_CAPTURE_LABELS
        ):
            failures.append(
                Failure(
                    "PLAYER_RESULT_INCOMPLETE",
                    "player capture labels are missing, duplicated, or "
                    "unexpected for matrix cell",
                    f"{result}:{key}",
                )
            )
        for row in rows:
            label = str(row.get("label", ""))
            try:
                relative = normalize_relative(str(row.get("path", "")))
            except ValueError:
                relative = ""
            if relative != f"{expected_directory}/{label}.png":
                failures.append(
                    Failure(
                        "PLAYER_RESULT_INCOMPLETE",
                        "player capture path is not bound to its matrix cell "
                        "and label",
                        str(row.get("path", "")),
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
                if xml_report_has_failure(report):
                    failures.append(
                        Failure(
                            "XML_FAILURE_SUMMARY_NONZERO",
                            "Unity XML reports a failed root/suite summary "
                            "or failed descendant test case",
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
        coverage: list[float] | None = None
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
                coverage = [value / 255.0 for value in data]
                expected_buffer = expected_synthetic_coverage_bytes(
                    width,
                    height,
                    (float(row["center_x"]), float(row["center_y"])),
                    float(row["radius"]),
                    float(row["edge_pixels"]),
                )
                mismatch_count = sum(
                    (actual < 128) != (expected < 128)
                    for actual, expected in zip(data, expected_buffer)
                )
                if mismatch_count:
                    failures.append(
                        Failure(
                            "PIXEL_ARTIFACT_HASH_MISMATCH",
                            "synthetic raw aperture mask differs from the "
                            "recorded center/radius/edge aperture at "
                            f"{mismatch_count} pixels",
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
                    png_to_unity_pixel_order(png_width, png_height, pixels)
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
            else:
                unity_pixels = png_to_unity_pixel_order(
                    png_width, png_height, pixels
                )
                try:
                    coverage = resolve_coverage_from_rgba(unity_pixels)
                except ValueError as exc:
                    failures.append(
                        Failure(
                            "PIXEL_ARTIFACT_HASH_MISMATCH",
                            f"shader raw coverage reconstruction failed: {exc}",
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
        declared_accepted = (
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
        if (row.get("verdict") == "PASS") != declared_accepted:
            failures.append(
                Failure(
                    "PIXEL_ARTIFACT_HASH_MISMATCH",
                    "known-center verdict differs from recalculated thresholds",
                    row.get("fixture_id", ""),
                )
            )
        if coverage is None:
            continue
        try:
            derived = analyze_coverage(
                coverage,
                width,
                height,
                (float(row["center_x"]), float(row["center_y"])),
            )
        except (KeyError, ValueError, ZeroDivisionError) as exc:
            failures.append(
                Failure(
                    "PIXEL_ARTIFACT_HASH_MISMATCH",
                    f"independent raw coverage analysis failed: {exc}",
                    str(capture),
                )
            )
            continue
        integer_mismatches = {
            field: (int(row[field]), int(derived[field]))
            for field in (
                "unexpected_components",
                "opaque_pinholes",
                "transparent_artifacts",
            )
            if int(row[field]) != int(derived[field])
        }
        radius_error_pixels = abs(
            float(derived["contour_radius"])
            - float(row["radius"]) * height
        )
        derived_accepted = (
            float(derived["center_error_pixels"])
            <= float(thresholds["centerErrorPixelsMaximum"])
            and float(derived["rms_radial_error_pixels"])
            <= float(thresholds["knownCenterRmsMaximum"])
            and float(derived["max_radial_error_pixels"])
            <= float(thresholds["knownCenterMaximumRadialErrorMaximum"])
            and radius_error_pixels
            <= float(thresholds["knownCenterMaximumRadialErrorMaximum"])
            and int(derived["unexpected_components"]) == 0
            and int(derived["opaque_pinholes"]) == 0
            and int(derived["transparent_artifacts"]) == 0
        )
        if integer_mismatches or (
            row.get("verdict") == "PASS"
        ) != derived_accepted:
            failures.append(
                Failure(
                    "PIXEL_ARTIFACT_HASH_MISMATCH",
                    "producer metrics/verdict differ from independently "
                    f"derived raw coverage metrics: integers={integer_mismatches}; "
                    f"radiusErrorPixels={radius_error_pixels}; "
                    f"derivedAccepted={derived_accepted}",
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


def frame_delta_metrics(
    first: bytes,
    second: bytes,
    width: int,
    height: int,
    opaque_cover: tuple[int, int, int],
) -> dict[str, int]:
    if len(first) != len(second) or len(first) != width * height * 4:
        raise ValueError("frame RGBA buffers are not comparable")
    changed_mask = [False] * (width * height)
    changed_pixels = 0
    max_channel_delta = 0
    unexpected_chroma = 0
    for pixel_index, index in enumerate(range(0, len(first), 4)):
        deltas = [
            abs(second[index + channel] - first[index + channel])
            for channel in range(4)
        ]
        maximum = max(deltas)
        if maximum == 0:
            continue
        changed_mask[pixel_index] = True
        changed_pixels += 1
        max_channel_delta = max(max_channel_delta, maximum)
        before_distance = sum(
            (first[index + channel] - opaque_cover[channel]) ** 2
            for channel in range(3)
        )
        after_distance = sum(
            (second[index + channel] - opaque_cover[channel]) ** 2
            for channel in range(3)
        )
        if after_distance > before_distance + 1:
            unexpected_chroma += 1
    return {
        "next_changed_pixels": changed_pixels,
        "next_max_channel_delta": max_channel_delta,
        "next_unexpected_chroma_pixels": unexpected_chroma,
        "next_changed_components": count_components(
            changed_mask, width, height
        ),
    }


def derive_final_close_rows(
    rows: list[dict[str, str]],
    decoded_frames: list[tuple[int, int, bytes]],
    reference_pixels: bytes,
) -> list[dict[str, str]]:
    if len(rows) != len(decoded_frames):
        raise ValueError("frame rows and decoded frame count differ")
    opaque_cover = dominant_non_background_rgb(decoded_frames)
    if dominant_rgb(reference_pixels) != opaque_cover:
        raise ValueError(
            "persistent-cover color differs from the cover independently "
            "derived from raw frames"
        )
    derived_rows: list[dict[str, str]] = []
    for index, row in enumerate(rows):
        width, height, pixels = decoded_frames[index]
        if len(reference_pixels) != len(pixels):
            raise ValueError("persistent-cover dimensions differ from raw frame")
        coverage = resolve_coverage_from_rgba(pixels, opaque_cover)
        transparent_pixels = sum(value < 0.1 for value in coverage)
        contour_radius = 0.0
        if any(value < 0.5 for value in coverage) and any(
            value >= 0.5 for value in coverage
        ):
            try:
                contour_radius = float(
                    analyze_coverage(
                        coverage,
                        width,
                        height,
                        (
                            float(row.get("material_center_x", "0.5")),
                            float(row.get("material_center_y", "0.5")),
                        ),
                    )["contour_radius"]
                )
            except ValueError:
                # A sub-16-point open region is still independently open even
                # though it is too small for a stable circle fit.
                contour_radius = 1.0
        derived = dict(row)
        derived["transparent_pixel_count"] = str(transparent_pixels)
        derived["contour_radius"] = repr(contour_radius)
        derived["authoring_exact_closed"] = (
            "true"
            if transparent_pixels == 0 and contour_radius <= 0.0
            else "false"
        )
        if index + 1 < len(decoded_frames):
            next_width, next_height, next_pixels = decoded_frames[index + 1]
            if (width, height) != (next_width, next_height):
                raise ValueError("adjacent frame dimensions differ")
            delta = frame_delta_metrics(
                pixels, next_pixels, width, height, opaque_cover
            )
        else:
            delta = {
                "next_changed_pixels": 0,
                "next_max_channel_delta": 0,
                "next_unexpected_chroma_pixels": 0,
                "next_changed_components": 0,
            }
        for field, value in delta.items():
            derived[field] = str(value)
        derived_rows.append(derived)
    return derived_rows


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
        decoded_frames: list[tuple[int, int, bytes]] = []
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
                width, height, pixels = decode_png_rgba(capture)
            except Exception as exc:
                failures.append(
                    Failure(
                        "MISSING_PIXEL_ARTIFACT",
                        f"raw frame PNG decode failed: {exc}",
                        str(capture),
                    )
                )
                continue
            decoded_frames.append((width, height, pixels))
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
        reference_path = selection_path.parent / "persistent-cover-first-rendered.png"
        derived_rows: list[dict[str, str]] | None = None
        if not reference_path.is_file():
            failures.append(
                Failure(
                    "FINAL_SELECTOR_MISMATCH",
                    "opaque persistent-cover reference is missing",
                    str(reference_path),
                )
            )
        elif len(decoded_frames) == len(rows):
            try:
                reference_width, reference_height, reference_pixels = (
                    decode_png_rgba(reference_path)
                )
                if decoded_frames and (
                    reference_width,
                    reference_height,
                ) != decoded_frames[0][:2]:
                    raise ValueError(
                        "opaque persistent-cover dimensions differ from raw frames"
                    )
                derived_rows = derive_final_close_rows(
                    rows, decoded_frames, reference_pixels
                )
            except Exception as exc:
                failures.append(
                    Failure(
                        "FINAL_SELECTOR_MISMATCH",
                        f"pixel-derived frame metric calculation failed: {exc}",
                        str(reference_path),
                    )
                )
        if derived_rows is None:
            derived_rows = rows
        else:
            integer_fields = (
                "transparent_pixel_count",
                "next_changed_pixels",
                "next_max_channel_delta",
                "next_unexpected_chroma_pixels",
                "next_changed_components",
            )
            for declared, derived in zip(rows, derived_rows):
                mismatches = {
                    field: (int(declared[field]), int(derived[field]))
                    for field in integer_fields
                    if int(declared[field]) != int(derived[field])
                }
                contour_closedness_differs = (
                    float(declared["contour_radius"]) <= 0.0
                ) != (float(derived["contour_radius"]) <= 0.0)
                if mismatches or contour_closedness_differs:
                    failures.append(
                        Failure(
                            "FINAL_SELECTOR_MISMATCH",
                            "producer frame metrics differ from decoded pixels: "
                            f"integers={mismatches}; "
                            "contourClosednessDiffers="
                            f"{contour_closedness_differs}",
                            str(declared.get("capture_path", "")),
                        )
                    )
        recomputed = recompute_selector(derived_rows)
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
    repository = repository_for_contract(contract_path)
    if output == repository or output.is_relative_to(repository):
        raise ValueError(
            "verification output must resolve outside the repository: "
            f"{output}"
        )
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
