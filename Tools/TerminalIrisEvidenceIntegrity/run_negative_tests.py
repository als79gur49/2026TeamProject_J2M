#!/usr/bin/env python3
from __future__ import annotations

import argparse
from concurrent.futures import ThreadPoolExecutor
import csv
import hashlib
import json
import os
from pathlib import Path
import shutil
import tempfile
from typing import Callable

import verify_bundle


EXCLUSIONS = {
    "artifact-hashes.sha256",
    "bundle-produced.json",
    "BUNDLE_CLOSED",
}


def cow_bytes(path: Path, value: bytes) -> None:
    if path.exists():
        path.unlink()
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(value)


def cow_text(path: Path, value: str) -> None:
    cow_bytes(path, value.encode("utf-8"))


def cow_json(path: Path, payload: object) -> None:
    cow_text(path, json.dumps(payload, indent=2, ensure_ascii=False) + "\n")


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def one(root: Path, pattern: str) -> Path:
    matches = sorted(root.glob(pattern))
    if len(matches) != 1:
        raise RuntimeError(f"fixture expected one {pattern}, found {len(matches)}")
    return matches[0]


def mutate_json(path: Path, mutation: Callable[[dict], None]) -> None:
    payload = load_json(path)
    mutation(payload)
    cow_json(path, payload)


def mutate_csv(path: Path, mutation: Callable[[list[dict[str, str]]], None]) -> None:
    with path.open(newline="", encoding="utf-8-sig") as stream:
        reader = csv.DictReader(stream)
        fields = list(reader.fieldnames or [])
        rows = list(reader)
    mutation(rows)
    if path.exists():
        path.unlink()
    with path.open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader()
        writer.writerows(rows)


def refresh_freeze_hash(bundle: Path) -> None:
    freeze = bundle / "00-source/source-freeze.json"
    cow_text(
        bundle / "00-source/source-freeze.sha256",
        f"{verify_bundle.sha256_file(freeze)}  source-freeze.json\n",
    )


def refresh_manifest(bundle: Path) -> None:
    manifest = bundle / "artifact-hashes.sha256"
    previous = {}
    for line in manifest.read_text(encoding="utf-8").splitlines():
        digest, separator, relative = line.partition("  ")
        if separator and len(digest) == 64:
            previous[relative] = digest
    entries = []
    for path in sorted(bundle.rglob("*")):
        if not path.is_file():
            continue
        relative = path.relative_to(bundle).as_posix()
        if relative in EXCLUSIONS:
            continue
        digest = previous.get(relative)
        if digest is None or path.stat().st_nlink <= 1:
            digest = verify_bundle.sha256_file(path)
        entries.append((digest, relative))
    cow_text(
        manifest,
        "".join(f"{digest}  {relative}\n" for digest, relative in entries),
    )
    produced_path = bundle / "bundle-produced.json"
    produced = load_json(produced_path)
    produced["artifactCount"] = len(entries)
    produced["artifactManifestSha256"] = verify_bundle.sha256_file(manifest)
    produced["filesystemPathSetSha256"] = hashlib.sha256(
        "\n".join(relative for _, relative in entries).encode("utf-8")
    ).hexdigest()
    cow_json(produced_path, produced)
    closed_path = bundle / "BUNDLE_CLOSED"
    closed = load_json(closed_path)
    closed["bundleProducedSha256"] = verify_bundle.sha256_file(produced_path)
    cow_json(closed_path, closed)


def command_files(bundle: Path) -> list[Path]:
    return sorted((bundle / "01-commands").glob("*.json"))


def lane_root(bundle: Path, lane_id: str) -> Path:
    return bundle / "02-lanes" / lane_id


def mutation_cases() -> list[tuple[str, str, str, Callable[[Path], None], bool]]:
    def required_source_deleted(bundle: Path) -> None:
        path = bundle / "00-source/source-freeze.json"
        mutate_json(path, lambda data: data["sources"].pop(0))
        refresh_freeze_hash(bundle)

    def source_hash_changed(bundle: Path) -> None:
        path = bundle / "00-source/source-freeze.json"
        mutate_json(
            path,
            lambda data: data["sources"][0].__setitem__("sha256", "0" * 64),
        )
        refresh_freeze_hash(bundle)

    def command_deleted(bundle: Path) -> None:
        command_files(bundle)[0].unlink()

    def duplicate_lane(bundle: Path) -> None:
        first, second = command_files(bundle)[:2]
        first_lane = load_json(first)["laneId"]
        mutate_json(second, lambda data: data.__setitem__("laneId", first_lane))

    def unknown_lane(bundle: Path) -> None:
        mutate_json(
            command_files(bundle)[0],
            lambda data: data.__setitem__("laneId", "unknown-lane"),
        )

    def xml_deleted(bundle: Path) -> None:
        (lane_root(bundle, "known-center-analyzer") / "unity-playmode.xml").unlink()

    def zero_xml(bundle: Path) -> None:
        cow_bytes(
            lane_root(bundle, "known-center-analyzer") / "unity-playmode.xml",
            b"",
        )

    def duplicate_xml(bundle: Path) -> None:
        source = lane_root(bundle, "known-center-analyzer") / "unity-playmode.xml"
        cow_bytes(
            lane_root(bundle, "known-center-analyzer")
            / "stale/unity-playmode.xml",
            source.read_bytes(),
        )

    def command_xml_mismatch(bundle: Path) -> None:
        path = next(
            path
            for path in command_files(bundle)
            if load_json(path)["laneId"] == "known-center-analyzer"
        )
        mutate_json(path, lambda data: data.__setitem__("exitCode", 1))

    def environment_deleted(bundle: Path) -> None:
        (
            lane_root(bundle, "known-center-analyzer") / "lane-environment.json"
        ).unlink()

    def player_result_deleted(bundle: Path) -> None:
        one(
            lane_root(bundle, "player-visual-quality"),
            "**/player-visual-result.json",
        ).unlink()

    def ui_failure_changed(bundle: Path) -> None:
        path = lane_root(bundle, "ui") / "unity-editmode.xml"
        original_stat = path.stat()
        text = path.read_text(encoding="utf-8")
        approved_fullname = (
            'fullname="Game.Feature.UI.Tests.'
            "ClimateCrisisKrTypographyContractTests."
            'ClimateAssets_KeepCommittedIdentityAndCanonicalMaterial"'
        )
        unexpected_fullname = (
            'fullname="Game.Feature.UI.Tests.'
            "ClimateCrisisKrTypographyContractTests."
            'TerminalIrisInjectedUnexpectedFailure"'
        )
        if approved_fullname not in text:
            raise RuntimeError("approved UI failure fullname was not found")
        cow_text(
            path,
            text.replace(approved_fullname, unexpected_fullname, 1),
        )
        os.utime(
            path,
            ns=(original_stat.st_atime_ns, original_stat.st_mtime_ns),
        )

    def known_png_replaced(bundle: Path) -> None:
        path = sorted(
            lane_root(bundle, "known-center-analyzer").glob(
                "**/shader-captures/*.png"
            )
        )[0]
        cow_bytes(path, b"not-a-png")

    def offcenter_csv_sha_changed(bundle: Path) -> None:
        path = one(
            lane_root(bundle, "production-offcenter-focus"),
            "**/off-center-focus-trace.csv",
        )
        mutate_csv(
            path, lambda rows: rows[0].__setitem__("capture_sha256", "0" * 64)
        )

    def offcenter_png_replaced(bundle: Path) -> None:
        path = sorted(
            lane_root(bundle, "production-offcenter-focus").glob(
                "**/production-offcenter/*.png"
            )
        )[0]
        cow_bytes(path, b"not-a-png")

    def duplicate_sample(bundle: Path) -> None:
        path = one(
            lane_root(bundle, "production-offcenter-focus"),
            "**/off-center-focus-trace.csv",
        )
        mutate_csv(
            path,
            lambda rows: rows[1].__setitem__("sample_id", rows[0]["sample_id"]),
        )

    def matrix_missing(bundle: Path) -> None:
        path = one(
            lane_root(bundle, "production-offcenter-focus"),
            "**/off-center-focus-trace.csv",
        )
        mutate_csv(
            path,
            lambda rows: (
                rows[0].__setitem__("direction", rows[1]["direction"]),
                rows[0].__setitem__("run", rows[1]["run"]),
            ),
        )

    def selector_changed(bundle: Path) -> None:
        path = sorted(
            lane_root(bundle, "final-close-frames").glob(
                "**/frame-selection.json"
            )
        )[0]
        mutate_json(
            path,
            lambda data: data.__setitem__(
                "firstExactClosedFrame",
                int(data["firstExactClosedFrame"]) + 1,
            ),
        )

    def frame_csv_delta_changed(bundle: Path) -> None:
        path = sorted(
            lane_root(bundle, "final-close-frames").glob("**/frame-metrics.csv")
        )[0]
        mutate_csv(
            path,
            lambda rows: rows[-2].__setitem__("next_changed_pixels", "1"),
        )

    def selected_png_replaced(bundle: Path) -> None:
        path = sorted(
            lane_root(bundle, "final-close-frames").glob(
                "**/first-exact-closed.png"
            )
        )[0]
        cow_bytes(path, b"not-a-png")

    def heatmap_hash_changed(bundle: Path) -> None:
        path = sorted(
            lane_root(bundle, "final-close-frames").glob(
                "**/heatmap-manifest.json"
            )
        )[0]
        mutate_json(
            path,
            lambda data: data["heatmaps"][0].__setitem__(
                "sourceFrameASha256", "0" * 64
            ),
        )

    def manifest_entry_deleted(bundle: Path) -> None:
        path = bundle / "artifact-hashes.sha256"
        lines = path.read_text(encoding="utf-8").splitlines()
        cow_text(path, "\n".join(lines[1:]) + "\n")

    def unmanifested_file(bundle: Path) -> None:
        cow_text(bundle / "unmanifested-injection.txt", "mutation\n")

    def manifest_changed(bundle: Path) -> None:
        path = bundle / "artifact-hashes.sha256"
        cow_text(path, path.read_text(encoding="utf-8") + "corrupt\n")

    def verifier_mutates(bundle: Path) -> None:
        cow_text(bundle / "verifier-mutation-attempt.txt", "mutation\n")

    return [
        ("NEG-01", "required source inventory entry deleted", "MISSING_REQUIRED_SOURCE", required_source_deleted, True),
        ("NEG-02", "source hash changed", "SOURCE_HASH_MISMATCH", source_hash_changed, True),
        ("NEG-03", "required lane command deleted", "MISSING_REQUIRED_LANE", command_deleted, True),
        ("NEG-04", "lane ID duplicated", "DUPLICATE_LANE", duplicate_lane, True),
        ("NEG-05", "unknown lane keeps command count at 12", "UNKNOWN_LANE", unknown_lane, True),
        ("NEG-06", "Unity XML deleted", "MISSING_RESULT_ARTIFACT", xml_deleted, True),
        ("NEG-07", "Unity XML made zero-byte", "INVALID_XML", zero_xml, True),
        ("NEG-08", "stale duplicate XML added", "DUPLICATE_RESULT_ARTIFACT", duplicate_xml, True),
        ("NEG-09", "command exit and XML result disagree", "COMMAND_RESULT_MISMATCH", command_xml_mismatch, True),
        ("NEG-10", "graphics environment deleted", "MISSING_GRAPHICS_ENVIRONMENT", environment_deleted, True),
        ("NEG-11", "player visual result deleted", "PLAYER_RESULT_INCOMPLETE", player_result_deleted, True),
        ("NEG-12", "UI failure exact set changed", "UNEXPECTED_UI_FAILURE", ui_failure_changed, True),
        ("NEG-13", "known-center PNG replaced", "PIXEL_ARTIFACT_HASH_MISMATCH", known_png_replaced, True),
        ("NEG-14", "off-center CSV capture SHA changed", "PIXEL_ARTIFACT_HASH_MISMATCH", offcenter_csv_sha_changed, True),
        ("NEG-15", "off-center PNG replaced", "PIXEL_ARTIFACT_HASH_MISMATCH", offcenter_png_replaced, True),
        ("NEG-16", "off-center SampleId duplicated", "DUPLICATE_SAMPLE_ID", duplicate_sample, True),
        ("NEG-17", "off-center direction/run matrix entry removed", "OFFCENTER_MATRIX_INCOMPLETE", matrix_missing, True),
        ("NEG-18", "final selector JSON index changed", "FINAL_SELECTOR_MISMATCH", selector_changed, True),
        ("NEG-19", "frame CSV pixel delta changed", "FINAL_SELECTOR_MISMATCH", frame_csv_delta_changed, True),
        ("NEG-20", "selected PNG replaced", "FINAL_SELECTOR_MISMATCH", selected_png_replaced, True),
        ("NEG-21", "heatmap source hash changed", "HEATMAP_CORRELATION_MISMATCH", heatmap_hash_changed, True),
        ("NEG-22", "artifact manifest entry deleted", "UNMANIFESTED_FILE", manifest_entry_deleted, False),
        ("NEG-23", "unmanifested filesystem file added", "UNMANIFESTED_FILE", unmanifested_file, False),
        ("NEG-24", "artifact manifest itself changed", "ARTIFACT_MANIFEST_MISMATCH", manifest_changed, False),
        ("NEG-25", "verifier attempts to mutate input bundle", "VERIFIER_MUTATED_INPUT", verifier_mutates, False),
    ]


def clone_hardlinked(source: Path, destination: Path) -> None:
    shutil.copytree(source, destination, copy_function=os.link)


def manifest_backed_snapshot(bundle: Path) -> verify_bundle.BundleSnapshot:
    manifest = bundle / "artifact-hashes.sha256"
    entries = {}
    if manifest.is_file():
        for line in manifest.read_text(encoding="utf-8").splitlines():
            digest, separator, relative = line.partition("  ")
            if separator and len(digest) == 64:
                entries[relative] = digest
    files: dict[str, tuple[int, int, str]] = {}
    aggregate = hashlib.sha256()
    for path in sorted(bundle.rglob("*")):
        if not path.is_file() and not path.is_symlink():
            continue
        relative = path.relative_to(bundle).as_posix()
        stat = path.lstat() if path.is_symlink() else path.stat()
        digest = entries.get(relative, "")
        files[relative] = (stat.st_size, stat.st_mtime_ns, digest)
        aggregate.update(
            (
                f"{relative}\0{stat.st_size}\0{stat.st_mtime_ns}\0"
                f"{digest}\n"
            ).encode("utf-8")
        )
    return verify_bundle.BundleSnapshot(
        tuple(files),
        len(files),
        sum(value[0] for value in files.values()),
        verify_bundle.sha256_file(manifest) if manifest.is_file() else "",
        aggregate.hexdigest(),
        files,
    )


def run_manifest_backed_fixture_verification(
    bundle: Path,
    contract_path: Path,
    output: Path,
) -> tuple[int, dict[str, object]]:
    contract = json.loads(contract_path.read_text(encoding="utf-8"))
    output.mkdir(parents=True, exist_ok=False)
    (output / "verification-command.txt").write_text(
        "negative-fixture manifest-backed verifier core\n", encoding="utf-8"
    )
    (output / "verification-environment.txt").write_text(
        f"UTC={verify_bundle.utc_now()}\n"
        "Mode=manifest-backed-mutation-fixture\n",
        encoding="utf-8",
    )
    before = manifest_backed_snapshot(bundle)
    failures, checks, xml_reports, freeze = verify_bundle.verify_core(
        bundle, contract_path, contract, before
    )
    after = manifest_backed_snapshot(bundle)
    difference = verify_bundle.snapshot_difference(before, after)
    unchanged = not any(difference.values())
    if not unchanged:
        failures.append(
            verify_bundle.Failure(
                "VERIFIER_MUTATED_INPUT",
                f"input changed during verification: {difference}",
                str(bundle),
            )
        )
    report = {
        "schemaVersion": 2,
        "verificationMode": "manifest-backed-mutation-fixture",
        "bundleId": freeze.get("bundleId", ""),
        "sourceFreezeId": freeze.get("sourceFreezeId", ""),
        "status": "PASS" if not failures else "BLOCKED",
        "failureCodes": sorted({failure.code for failure in failures}),
        "failures": [failure.as_dict() for failure in failures],
        "checks": checks,
        "xmlSummaries": xml_reports,
        "inputBundleUnchanged": unchanged,
        "inputSnapshotBefore": before.summary(),
        "inputSnapshotAfter": after.summary(),
        "inputSnapshotDifference": difference,
        "completedUtc": verify_bundle.utc_now(),
    }
    (output / "bundle-verification.json").write_text(
        json.dumps(report, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    print(
        f"fixture verification: {report['status']} "
        f"output={output} unchanged={unchanged}"
    )
    return (0 if not failures else 1), report


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--bundle", required=True, type=Path)
    parser.add_argument("--contract", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    bundle = args.bundle.resolve()
    contract = args.contract.resolve()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    cases_output = output / "negative-cases"
    cases_output.mkdir(exist_ok=False)
    original_before = verify_bundle.snapshot_bundle(bundle)
    results = []
    temporary_parent = Path(
        tempfile.mkdtemp(prefix=".ticei-negative-", dir=str(bundle.parent))
    )
    try:
        def run_case(
            case: tuple[str, str, str, Callable[[Path], None], bool]
        ) -> dict[str, object]:
            case_id, description, expected, mutation, refresh = case
            fixture = temporary_parent / case_id
            clone_hardlinked(bundle, fixture)
            try:
                hook = None
                if case_id == "NEG-25":
                    hook = mutation
                else:
                    mutation(fixture)
                    if refresh:
                        refresh_manifest(fixture)
                case_output = cases_output / case_id
                if case_id == "NEG-25":
                    exit_code, report = verify_bundle.run_verification(
                        fixture, contract, case_output, mutation_hook=hook
                    )
                else:
                    exit_code, report = (
                        run_manifest_backed_fixture_verification(
                            fixture, contract, case_output
                        )
                    )
                codes = list(report.get("failureCodes", []))
                verdict = (
                    "PASS"
                    if exit_code != 0 and expected in codes
                    else "FAIL"
                )
                result = {
                    "caseId": case_id,
                    "mutation": description,
                    "expectedFailureCode": expected,
                    "actualFailureCode": expected
                    if expected in codes
                    else (codes[0] if codes else "UNEXPECTED_PASS"),
                    "allFailureCodes": codes,
                    "verdict": verdict,
                }
                print(
                    f"{case_id}: {verdict} expected={expected} "
                    f"actual={result['actualFailureCode']}"
                )
                return result
            finally:
                shutil.rmtree(fixture, ignore_errors=True)

        cases = mutation_cases()
        with ThreadPoolExecutor(max_workers=3) as executor:
            results = list(executor.map(run_case, cases))
    finally:
        shutil.rmtree(temporary_parent, ignore_errors=True)
    original_after = verify_bundle.snapshot_bundle(bundle)
    original_unchanged = (
        verify_bundle.snapshot_difference(original_before, original_after)
        == {"added": [], "removed": [], "changed": []}
    )
    passed = sum(row["verdict"] == "PASS" for row in results)
    report = {
        "schemaVersion": 1,
        "bundle": str(bundle),
        "caseCount": len(results),
        "passed": passed,
        "falseNegativeCount": sum(
            row["actualFailureCode"] == "UNEXPECTED_PASS" for row in results
        ),
        "unexpectedPassCount": sum(
            row["actualFailureCode"] == "UNEXPECTED_PASS" for row in results
        ),
        "originalBundleUnchanged": original_unchanged,
        "fixtureVerificationMode": (
            "manifest-backed verifier core for NEG-01..24; "
            "full read-only verifier snapshot for NEG-25"
        ),
        "verdict": "PASS"
        if passed == len(results) and original_unchanged
        else "FAIL",
        "cases": results,
    }
    for name in ("verifier-negative-tests.json", "negative-test-report.json"):
        (output / name).write_text(
            json.dumps(report, indent=2, ensure_ascii=False) + "\n",
            encoding="utf-8",
        )
    print(
        f"verifier negative tests: {passed}/{len(results)} "
        f"original unchanged={original_unchanged}"
    )
    return 0 if report["verdict"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
