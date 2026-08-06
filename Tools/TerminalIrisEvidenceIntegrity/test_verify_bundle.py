#!/usr/bin/env python3
from __future__ import annotations

import binascii
import csv
import hashlib
import json
import math
from pathlib import Path
import struct
import subprocess
import tempfile
import unittest
import zlib

import verify_bundle


def png_rgba(width: int, height: int, pixels: bytes) -> bytes:
    def chunk(kind: bytes, value: bytes) -> bytes:
        body = kind + value
        return (
            struct.pack(">I", len(value))
            + body
            + struct.pack(">I", binascii.crc32(body) & 0xFFFFFFFF)
        )

    stride = width * 4
    raw = b"".join(
        b"\x00" + pixels[row * stride : (row + 1) * stride]
        for row in range(height)
    )
    return (
        b"\x89PNG\r\n\x1a\n"
        + chunk(
            b"IHDR",
            struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0),
        )
        + chunk(b"IDAT", zlib.compress(raw))
        + chunk(b"IEND", b"")
    )


class VerifyBundleUnitTests(unittest.TestCase):
    PLAYER_CAPTURE_LABELS = (
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
    )

    def test_path_normalization_rejects_parent_and_backslash(self) -> None:
        self.assertEqual(
            verify_bundle.normalize_relative("a/b.txt"), "a/b.txt"
        )
        for value in ("../a", "/a", "a\\b", "./a"):
            with self.assertRaises(ValueError):
                verify_bundle.normalize_relative(value)

    def test_verification_output_rejects_repository_internal_path(self) -> None:
        contract = (
            Path(__file__).resolve().parent / "evidence-contract-v1.json"
        )
        repository = verify_bundle.repository_for_contract(contract)
        internal = repository / "TestLogs/verifier-internal-output-probe"
        with tempfile.TemporaryDirectory() as temporary:
            with self.assertRaisesRegex(ValueError, "outside the repository"):
                verify_bundle.run_verification(
                    Path(temporary), contract, internal
                )
        self.assertFalse(internal.exists())

    def test_case_collision_and_duplicate_are_rejected(self) -> None:
        failures = verify_bundle.collision_failures(
            ["A/file.txt", "a/file.txt", "A/file.txt"]
        )
        self.assertGreaterEqual(len(failures), 2)
        self.assertTrue(
            all(
                failure.code == "ARTIFACT_MANIFEST_MISMATCH"
                for failure in failures
            )
        )

    def test_duplicate_candidate_rejection(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "a").mkdir()
            (root / "b").mkdir()
            (root / "a/result.xml").write_text("<test-run total='1'/>")
            (root / "b/result.xml").write_text("<test-run total='1'/>")
            failures: list[verify_bundle.Failure] = []
            result = verify_bundle.one_match(
                root,
                "**/result.xml",
                failures,
                "MISSING_RESULT_ARTIFACT",
            )
            self.assertIsNone(result)
            self.assertEqual(
                failures[0].code, "DUPLICATE_RESULT_ARTIFACT"
            )

    def test_exact_lane_equality(self) -> None:
        required = {"a", "b"}
        self.assertEqual(
            verify_bundle.exact_lane_failure_codes(["a", "b"], required),
            set(),
        )
        self.assertEqual(
            verify_bundle.exact_lane_failure_codes(["a", "a", "x"], required),
            {
                "MISSING_REQUIRED_LANE",
                "UNKNOWN_LANE",
                "DUPLICATE_LANE",
            },
        )

    def test_player_result_requires_exact_matrix_and_final_zero_exit(self) -> None:
        matrix = [
            {
                "width": width,
                "height": height,
                "frameRate": frame_rate,
                "focus": focus,
                "attemptCount": 1,
                "finalExitCode": 0,
            }
            for width, height in ((1920, 1080), (3440, 1440))
            for frame_rate in (30, 60, 120)
            for focus in ("center", "offcenter")
        ]
        attempts = [
            {
                **{
                    key: row[key]
                    for key in ("width", "height", "frameRate", "focus")
                },
                "attempt": 1,
                "exitCode": 0,
                "passMarker": True,
            }
            for row in matrix
        ]
        payload = {
            "bundleId": "B",
            "sourceFreezeId": "S",
            "laneId": "player-visual-quality",
            "buildExitCode": 0,
            "playerExitCode": 0,
            "captureCount": 192,
            "expectedCaptureCount": 192,
            "matrix": matrix,
            "exitAttempts": attempts,
            "GPU": "GPU",
            "graphicsAPI": "D3D12",
            "driver": "driver",
            "resolutions": ["1920x1080", "3440x1440"],
            "frameRates": [30, 60, 120],
            "captureManifest": [],
            "playerLog": [],
            "buildLog": "player-build.log",
            "result": "PASS",
        }
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            result_root = root / "result"
            result_root.mkdir()
            captures = []
            for row in matrix:
                directory = (
                    f"Player-{row['width']}x{row['height']}-"
                    f"{row['frameRate']}fps-{row['focus']}"
                )
                for label in self.PLAYER_CAPTURE_LABELS:
                    relative = f"{directory}/{label}.png"
                    capture = result_root / relative
                    capture.parent.mkdir(parents=True, exist_ok=True)
                    capture.write_bytes(relative.encode("utf-8"))
                    captures.append(
                        {
                            "path": relative,
                            "sha256": verify_bundle.sha256_file(capture),
                            "width": row["width"],
                            "height": row["height"],
                            "frameRate": row["frameRate"],
                            "focus": row["focus"],
                            "label": label,
                        }
                    )
            payload["captureManifest"] = captures
            result = result_root / "player-visual-result.json"
            result.write_text(json.dumps(payload), encoding="utf-8")
            failures: list[verify_bundle.Failure] = []
            verify_bundle.verify_player_result(
                root,
                {"exitCode": 0},
                {"bundleId": "B", "sourceFreezeId": "S"},
                {
                    "requiredArtifactThresholds": {
                        "playerExpectedCaptureCount": 192
                    }
                },
                failures,
            )
            self.assertEqual(failures, [])
            payload["matrix"][0]["attemptCount"] = 2
            payload["exitAttempts"][0]["attempt"] = 2
            payload["exitAttempts"].insert(
                0,
                {
                    "width": payload["matrix"][0]["width"],
                    "height": payload["matrix"][0]["height"],
                    "frameRate": payload["matrix"][0]["frameRate"],
                    "focus": payload["matrix"][0]["focus"],
                    "attempt": 1,
                    "exitCode": 124,
                    "passMarker": False,
                },
            )
            result.write_text(json.dumps(payload), encoding="utf-8")
            failures = []
            verify_bundle.verify_player_result(
                root,
                {"exitCode": 0},
                {"bundleId": "B", "sourceFreezeId": "S"},
                {
                    "requiredArtifactThresholds": {
                        "playerExpectedCaptureCount": 192
                    }
                },
                failures,
            )
            self.assertEqual(failures, [])
            payload["matrix"][0]["finalExitCode"] = 5
            result.write_text(json.dumps(payload), encoding="utf-8")
            failures = []
            verify_bundle.verify_player_result(
                root,
                {"exitCode": 0},
                {"bundleId": "B", "sourceFreezeId": "S"},
                {
                    "requiredArtifactThresholds": {
                        "playerExpectedCaptureCount": 192
                    }
                },
                failures,
            )
            self.assertIn(
                "PLAYER_RESULT_INCOMPLETE",
                {failure.code for failure in failures},
            )

    def test_xml_parsing(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "result.xml"
            path.write_text(
                "<test-run total='2' passed='1' failed='1' skipped='0'>"
                "<test-suite><test-case fullname='A' result='Passed'/>"
                "<test-case fullname='B' result='Failed'/></test-suite>"
                "</test-run>",
                encoding="utf-8",
            )
            report = verify_bundle.parse_xml(path)
            self.assertEqual(report["total"], 2)
            self.assertEqual(report["failedTestIds"], ["B"])
            self.assertTrue(verify_bundle.xml_report_has_failure(report))

    def test_xml_failure_matrix(self) -> None:
        cases = (
            (
                "root-summary-only",
                "<test-run total='1' passed='0' failed='1'>"
                "<test-suite><test-case fullname='A' result='Passed'/>"
                "</test-suite></test-run>",
                True,
            ),
            (
                "descendant-only",
                "<test-run total='1' passed='1' failed='0'>"
                "<test-suite><test-case fullname='A' result='Failed'/>"
                "</test-suite></test-run>",
                True,
            ),
            (
                "root-and-descendant",
                "<test-run total='1' passed='0' failed='1'>"
                "<test-suite><test-case fullname='A' result='Failed'/>"
                "</test-suite></test-run>",
                True,
            ),
            (
                "passing",
                "<test-run total='1' passed='1' failed='0'>"
                "<test-suite><test-case fullname='A' result='Passed'/>"
                "</test-suite></test-run>",
                False,
            ),
            (
                "suite-setup-failure",
                "<test-run total='1' passed='1' failed='0'>"
                "<test-suite result='Failed' failed='1'>"
                "<test-case fullname='A' result='Passed'/>"
                "</test-suite></test-run>",
                True,
            ),
        )
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "result.xml"
            for name, xml, expected in cases:
                with self.subTest(name=name):
                    path.write_text(xml, encoding="utf-8")
                    report = verify_bundle.parse_xml(path)
                    self.assertEqual(
                        verify_bundle.xml_report_has_failure(report), expected
                    )

    def test_invalid_xml_failure_summary_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "result.xml"
            path.write_text(
                "<test-run total='1' failed='not-a-number'/>",
                encoding="utf-8",
            )
            with self.assertRaisesRegex(ValueError, "failure summary"):
                verify_bundle.parse_xml(path)

    def test_source_identity_requires_exact_repository_head_and_tree(self) -> None:
        repo = Path("/repo").resolve()
        freeze = {
            "repository": str(repo),
            "head": "a" * 40,
            "tree": "b" * 40,
        }
        self.assertEqual(
            verify_bundle.source_identity_failures(
                freeze, repo, "a" * 40, "b" * 40
            ),
            [],
        )
        head_codes = {
            failure.code
            for failure in verify_bundle.source_identity_failures(
                freeze, repo, "c" * 40, "b" * 40
            )
        }
        self.assertEqual(head_codes, {"SOURCE_HEAD_MISMATCH"})
        tree_codes = {
            failure.code
            for failure in verify_bundle.source_identity_failures(
                freeze, repo, "a" * 40, "d" * 40
            )
        }
        self.assertEqual(tree_codes, {"SOURCE_TREE_MISMATCH"})
        unrelated_commit_codes = {
            failure.code
            for failure in verify_bundle.source_identity_failures(
                freeze, repo, "c" * 40, "d" * 40
            )
        }
        self.assertEqual(
            unrelated_commit_codes,
            {"SOURCE_HEAD_MISMATCH", "SOURCE_TREE_MISMATCH"},
        )
        repository_codes = {
            failure.code
            for failure in verify_bundle.source_identity_failures(
                freeze, Path("/different-repo"), "a" * 40, "b" * 40
            )
        }
        self.assertEqual(repository_codes, {"SOURCE_REPOSITORY_MISMATCH"})

    def test_source_worktree_failures_report_status_and_path(self) -> None:
        failures = verify_bundle.source_worktree_failures(
            (
                " M Assets/TrackedProbe.cs",
                "M  Assets/StagedProbe.cs",
                "?? Packages/UntrackedProbe.txt",
            )
        )
        self.assertEqual(
            [(failure.code, failure.path) for failure in failures],
            [
                ("SOURCE_TRACKED_MODIFICATION", "Assets/TrackedProbe.cs"),
                ("SOURCE_STAGED_MODIFICATION", "Assets/StagedProbe.cs"),
                ("SOURCE_UNTRACKED_ENTRY", "Packages/UntrackedProbe.txt"),
            ],
        )
        self.assertEqual(
            [failure.message for failure in failures],
            [
                "repository status ' M' is not clean",
                "repository status 'M ' is not clean",
                "repository status '??' is not clean",
            ],
        )
        self.assertEqual(verify_bundle.source_worktree_failures(()), [])

    def test_source_freeze_rejects_every_untracked_repository_entry(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            repo = root / "repo"
            bundle = root / "bundle"
            source_root = bundle / "00-source"
            repo.mkdir()
            source_root.mkdir(parents=True)
            subprocess.run(["git", "init", "-q"], cwd=repo, check=True)
            subprocess.run(
                ["git", "config", "user.email", "verifier@example.invalid"],
                cwd=repo,
                check=True,
            )
            subprocess.run(
                ["git", "config", "user.name", "Verifier Fixture"],
                cwd=repo,
                check=True,
            )
            (repo / "Assets").mkdir()
            seed = repo / "Assets/Seed.cs"
            seed.write_text("sealed class Seed {}\n", encoding="utf-8")
            contract_path = repo / "contract.json"
            contract = {
                "contractVersion": "source-cleanliness-test-v1",
                "requiredSources": ["Assets/Seed.cs"],
            }
            contract_path.write_text(
                json.dumps(contract, sort_keys=True) + "\n", encoding="utf-8"
            )
            subprocess.run(["git", "add", "."], cwd=repo, check=True)
            subprocess.run(
                ["git", "commit", "-qm", "fixture"], cwd=repo, check=True
            )
            head = subprocess.check_output(
                ["git", "rev-parse", "HEAD"], cwd=repo, text=True
            ).strip()
            tree = subprocess.check_output(
                ["git", "rev-parse", "HEAD^{tree}"], cwd=repo, text=True
            ).strip()
            sources = [
                {
                    "path": "Assets/Seed.cs",
                    "size": seed.stat().st_size,
                    "sha256": verify_bundle.sha256_file(seed),
                    "state": "tracked",
                }
            ]
            source_freeze_id = "TISF-" + hashlib.sha256(
                json.dumps(
                    sources, sort_keys=True, separators=(",", ":")
                ).encode("utf-8")
            ).hexdigest()[:24]
            empty_diff_sha = hashlib.sha256(b"").hexdigest()
            freeze = {
                "sourceFreezeId": source_freeze_id,
                "contractVersion": contract["contractVersion"],
                "contractSha256": verify_bundle.sha256_file(contract_path),
                "repository": str(repo.resolve()),
                "head": head,
                "tree": tree,
                "trackedDiffSha256": empty_diff_sha,
                "cachedDiffSha256": empty_diff_sha,
                "sources": sources,
            }
            freeze_path = source_root / "source-freeze.json"
            freeze_path.write_text(json.dumps(freeze), encoding="utf-8")
            (source_root / "source-freeze.sha256").write_text(
                verify_bundle.sha256_file(freeze_path) + "\n", encoding="utf-8"
            )

            def source_failures() -> list[verify_bundle.Failure]:
                failures: list[verify_bundle.Failure] = []
                verify_bundle.verify_source_freeze(
                    bundle, contract_path, contract, failures, []
                )
                return failures

            self.assertEqual(source_failures(), [])
            for relative in (
                "Assets/VerifierUntrackedProbe.cs",
                "Assets/VerifierUntrackedProbe.asset",
                "Packages/VerifierUntrackedProbe.txt",
            ):
                with self.subTest(relative=relative):
                    probe = repo / relative
                    probe.parent.mkdir(parents=True, exist_ok=True)
                    probe.write_text("untracked\n", encoding="utf-8")
                    failures = source_failures()
                    matching = [
                        failure
                        for failure in failures
                        if failure.code == "SOURCE_UNTRACKED_ENTRY"
                        and failure.path == relative
                    ]
                    self.assertEqual(len(matching), 1)
                    self.assertIn("??", matching[0].message)
                    probe.unlink()
            self.assertEqual(source_failures(), [])

    def test_ui_failure_policy_requires_exact_equality(self) -> None:
        approved = {"A", "B"}
        self.assertIsNone(
            verify_bundle.ui_failure_policy_code({"A", "B"}, approved)
        )
        self.assertEqual(
            verify_bundle.ui_failure_policy_code({"A", "B", "C"}, approved),
            "UNEXPECTED_UI_FAILURE",
        )
        self.assertEqual(
            verify_bundle.ui_failure_policy_code({"A"}, approved),
            "UI_BASELINE_DRIFT",
        )

    def test_offcenter_matrix_is_exact(self) -> None:
        matrix = verify_bundle.offcenter_matrix(
            ("Left", "Right", "Top", "Bottom"), (1, 2, 3)
        )
        self.assertEqual(len(matrix), 12)
        self.assertIn(("Bottom", "3"), matrix)

    def test_png_decode_and_row_hash_binding(self) -> None:
        pixels = bytes((1, 2, 3, 255, 4, 5, 6, 255))
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "capture.png"
            path.write_bytes(png_rgba(2, 1, pixels))
            width, height, decoded = verify_bundle.decode_png_rgba(path)
            self.assertEqual((width, height), (2, 1))
            self.assertEqual(decoded, pixels)
            self.assertEqual(
                hashlib.sha256(decoded).hexdigest(),
                hashlib.sha256(pixels).hexdigest(),
            )

    def test_known_center_rejects_coherent_raw_metric_tamper(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            bundle = Path(temporary)
            fixture_root = (
                bundle
                / "02-lanes/known-center-analyzer/result/cpu-fixtures"
            )
            fixture_root.mkdir(parents=True)
            capture = fixture_root / "wrong-aperture.coverage.bin"
            width = height = 16
            capture.write_bytes(bytes([255]) * width * height)
            digest = verify_bundle.sha256_file(capture)
            csv_path = fixture_root.parent / "known-center-fixtures.csv"
            fieldnames = (
                "fixture",
                "fixture_id",
                "pixel_buffer_sha256",
                "capture_path",
                "capture_sha256",
                "width",
                "height",
                "center_x",
                "center_y",
                "radius",
                "edge_pixels",
                "shader_sha256",
                "material_sha256",
                "measured_x",
                "measured_y",
                "center_error_pixels",
                "rms_radial_error_pixels",
                "max_radial_error_pixels",
                "p99_radial_error_pixels",
                "unexpected_components",
                "opaque_pinholes",
                "transparent_artifacts",
                "verdict",
            )
            with csv_path.open("w", newline="", encoding="utf-8") as stream:
                writer = csv.DictWriter(stream, fieldnames=fieldnames)
                writer.writeheader()
                writer.writerow(
                    {
                        "fixture": "synthetic",
                        "fixture_id": "wrong-aperture",
                        "pixel_buffer_sha256": digest,
                        "capture_path": "cpu-fixtures/wrong-aperture.coverage.bin",
                        "capture_sha256": digest,
                        "width": width,
                        "height": height,
                        "center_x": 0.5,
                        "center_y": 0.5,
                        "radius": 0.25,
                        "edge_pixels": 0,
                        "shader_sha256": "",
                        "material_sha256": "",
                        "measured_x": 0.5,
                        "measured_y": 0.5,
                        "center_error_pixels": 0,
                        "rms_radial_error_pixels": 0,
                        "max_radial_error_pixels": 0,
                        "p99_radial_error_pixels": 0,
                        "unexpected_components": 0,
                        "opaque_pinholes": 0,
                        "transparent_artifacts": 0,
                        "verdict": "PASS",
                    }
                )
            failures: list[verify_bundle.Failure] = []
            verify_bundle.verify_known_center(
                bundle,
                {
                    "requiredArtifactThresholds": {
                        "knownCenterRowCount": 1,
                        "knownCenterCpuFixtureCount": 1,
                        "knownCenterShaderFixtureCount": 0,
                        "centerErrorPixelsMaximum": 1.0,
                        "knownCenterRmsMaximum": 0.35,
                        "knownCenterMaximumRadialErrorMaximum": 1.0,
                    }
                },
                {},
                failures,
                [],
            )
            self.assertIn(
                "PIXEL_ARTIFACT_HASH_MISMATCH",
                {failure.code for failure in failures},
            )

    def test_known_center_valid_raw_aperture_is_independently_accepted(self) -> None:
        width = height = 32
        center_x = center_y = 0.5
        radius = 8.0
        coverage = [
            0.0
            if math.hypot(
                x - center_x * (width - 1),
                y - center_y * (height - 1),
            )
            <= radius
            else 1.0
            for y in range(height)
            for x in range(width)
        ]
        derived = verify_bundle.analyze_coverage(
            coverage, width, height, (center_x, center_y)
        )
        self.assertLessEqual(derived["center_error_pixels"], 1.0)
        self.assertLessEqual(derived["rms_radial_error_pixels"], 0.35)
        self.assertLessEqual(derived["max_radial_error_pixels"], 1.0)
        self.assertEqual(derived["unexpected_components"], 0)
        self.assertEqual(derived["opaque_pinholes"], 0)
        self.assertEqual(derived["transparent_artifacts"], 0)

    def test_final_close_rejects_coherent_open_frame_tamper(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            bundle = Path(temporary)
            open_pixels = bytes((255, 255, 255, 255)) * 4
            cover_pixels = bytes((0, 28, 112, 255)) * 4
            difference = verify_bundle.canonical_difference(
                open_pixels, open_pixels
            )
            for intent in ("victory", "defeat"):
                root = bundle / "02-lanes/final-close-frames" / intent
                frames = root / "frames"
                frames.mkdir(parents=True)
                frame_paths = []
                frame_hashes = []
                for index in range(3):
                    path = frames / f"frame-{index:04d}.png"
                    path.write_bytes(png_rgba(2, 2, open_pixels))
                    frame_paths.append(f"frames/{path.name}")
                    frame_hashes.append(verify_bundle.sha256_file(path))
                (root / "persistent-cover-first-rendered.png").write_bytes(
                    png_rgba(2, 2, cover_pixels)
                )
                selected = {
                    "last-animated-parameter.png": 0,
                    "last-pixel-changing.png": 0,
                    "first-exact-closed.png": 1,
                    "next-stable-closed.png": 2,
                }
                for name, index in selected.items():
                    (root / name).write_bytes(
                        png_rgba(2, 2, open_pixels)
                    )
                with (root / "frame-metrics.csv").open(
                    "w", newline="", encoding="utf-8"
                ) as stream:
                    fieldnames = (
                        "render_sequence_index",
                        "input_radius",
                        "effective_radius",
                        "closed_overshoot",
                        "render_width",
                        "render_height",
                        "capture_path",
                        "capture_sha256",
                        "transparent_pixel_count",
                        "contour_radius",
                        "authoring_exact_closed",
                        "next_changed_pixels",
                        "next_max_channel_delta",
                        "next_unexpected_chroma_pixels",
                        "next_changed_components",
                    )
                    writer = csv.DictWriter(stream, fieldnames=fieldnames)
                    writer.writeheader()
                    for index in range(3):
                        writer.writerow(
                            {
                                "render_sequence_index": index,
                                "input_radius": 1 if index == 0 else 0,
                                "effective_radius": 1 if index == 0 else -4,
                                "closed_overshoot": 0 if index == 0 else 4,
                                "render_width": 2,
                                "render_height": 2,
                                "capture_path": frame_paths[index],
                                "capture_sha256": frame_hashes[index],
                                "transparent_pixel_count": 0,
                                "contour_radius": 0,
                                "authoring_exact_closed": (
                                    "false" if index == 0 else "true"
                                ),
                                "next_changed_pixels": 1 if index == 0 else 0,
                                "next_max_channel_delta": 1 if index == 0 else 0,
                                "next_unexpected_chroma_pixels": 0,
                                "next_changed_components": 1 if index == 0 else 0,
                            }
                        )
                (root / "frame-selection.json").write_text(
                    json.dumps(
                        {
                            "intent": intent,
                            "lastAnimatedParameterFrame": 0,
                            "lastPixelChangingFrame": 0,
                            "firstExactClosedFrame": 1,
                            "nextStableClosedFrame": 2,
                            "missingFrameIndex": False,
                            "duplicateFrameIndex": False,
                            "changedAfterStableClosed": False,
                            "selectedFrames": [
                                {
                                    "selector": selector,
                                    "rowId": row_id,
                                    "path": path,
                                    "sha256": frame_hashes[row_id],
                                }
                                for selector, row_id, path in (
                                    (
                                        "lastAnimatedParameter",
                                        0,
                                        "last-animated-parameter.png",
                                    ),
                                    (
                                        "lastPixelChanging",
                                        0,
                                        "last-pixel-changing.png",
                                    ),
                                    (
                                        "firstExactClosed",
                                        1,
                                        "first-exact-closed.png",
                                    ),
                                    (
                                        "nextStableClosed",
                                        2,
                                        "next-stable-closed.png",
                                    ),
                                )
                            ],
                        }
                    ),
                    encoding="utf-8",
                )
                heatmap_rows = []
                for identity, first_index, second_index, name in (
                    ("last-pixel-changing", 0, 1, "last-change.png"),
                    ("exact-closed-stability", 1, 2, "stable.png"),
                ):
                    stored = root / name
                    stored.write_bytes(png_rgba(2, 2, difference))
                    heatmap_rows.append(
                        {
                            "identity": identity,
                            "sourceFrameAPath": frame_paths[first_index],
                            "sourceFrameASha256": frame_hashes[first_index],
                            "sourceFrameBPath": frame_paths[second_index],
                            "sourceFrameBSha256": frame_hashes[second_index],
                            "heatmapPath": name,
                            "heatmapSha256": verify_bundle.sha256_file(stored),
                            "canonicalDifferenceBufferSha256": hashlib.sha256(
                                verify_bundle.png_to_unity_pixel_order(
                                    2, 2, difference
                                )
                            ).hexdigest(),
                        }
                    )
                (root / "heatmap-manifest.json").write_text(
                    json.dumps(
                        {
                            "differenceAlgorithmVersion": (
                                "max-rgba-delta-red-v1"
                            ),
                            "heatmaps": heatmap_rows,
                        }
                    ),
                    encoding="utf-8",
                )
            failures: list[verify_bundle.Failure] = []
            verify_bundle.verify_final_close(
                bundle,
                {
                    "finalCloseIntentSet": ["victory", "defeat"],
                    "heatmapPolicy": {
                        "differenceAlgorithmVersion": (
                            "max-rgba-delta-red-v1"
                        )
                    },
                },
                failures,
                [],
            )
            self.assertIn(
                "FINAL_SELECTOR_MISMATCH",
                {failure.code for failure in failures},
            )

    def test_final_close_selector_is_derived_from_normal_pixel_sequence(self) -> None:
        width = height = 4
        cover_pixel = bytes((0, 28, 112, 255))
        open_pixel = bytes((255, 255, 255, 255))
        open_frame = open_pixel * (width * height)
        changing_frame = open_pixel * 4 + cover_pixel * 12
        closed_frame = cover_pixel * (width * height)
        frames = [
            (width, height, open_frame),
            (width, height, changing_frame),
            (width, height, closed_frame),
            (width, height, closed_frame),
        ]
        rows = [
            {
                "render_sequence_index": str(index),
                "input_radius": str(max(0, 2 - index)),
                "effective_radius": str(max(0, 2 - index)),
                "closed_overshoot": "4" if index >= 2 else "0",
                "material_center_x": "0.5",
                "material_center_y": "0.5",
                "capture_sha256": hashlib.sha256(pixels).hexdigest(),
            }
            for index, (_, _, pixels) in enumerate(frames)
        ]
        derived_rows = verify_bundle.derive_final_close_rows(
            rows, frames, closed_frame
        )
        selector = verify_bundle.recompute_selector(derived_rows)
        self.assertEqual(selector["lastAnimatedParameterFrame"], 1)
        self.assertEqual(selector["lastPixelChangingFrame"], 1)
        self.assertEqual(selector["firstExactClosedFrame"], 2)
        self.assertEqual(selector["nextStableClosedFrame"], 3)
        self.assertFalse(selector["changedAfterStableClosed"])

    def test_player_capture_redistribution_is_rejected_per_cell(self) -> None:
        matrix = [
            {
                "width": width,
                "height": height,
                "frameRate": frame_rate,
                "focus": focus,
                "attemptCount": 1,
                "finalExitCode": 0,
            }
            for width, height in ((1920, 1080), (3440, 1440))
            for frame_rate in (30, 60, 120)
            for focus in ("center", "offcenter")
        ]
        attempts = [
            {
                **{
                    key: row[key]
                    for key in ("width", "height", "frameRate", "focus")
                },
                "attempt": 1,
                "exitCode": 0,
                "passMarker": True,
            }
            for row in matrix
        ]
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            result_root = root / "result"
            result_root.mkdir()
            captures = []
            for row in matrix:
                directory = (
                    f"Player-{row['width']}x{row['height']}-"
                    f"{row['frameRate']}fps-{row['focus']}"
                )
                labels = list(self.PLAYER_CAPTURE_LABELS)
                for label in labels:
                    relative = f"{directory}/{label}.png"
                    capture = result_root / relative
                    capture.parent.mkdir(parents=True, exist_ok=True)
                    capture.write_bytes(relative.encode("utf-8"))
                    captures.append(
                        {
                            "path": relative,
                            "sha256": verify_bundle.sha256_file(capture),
                            "width": row["width"],
                            "height": row["height"],
                            "frameRate": row["frameRate"],
                            "focus": row["focus"],
                            "label": label,
                        }
                    )
            payload = {
                "bundleId": "B",
                "sourceFreezeId": "S",
                "laneId": "player-visual-quality",
                "buildExitCode": 0,
                "playerExitCode": 0,
                "captureCount": 192,
                "expectedCaptureCount": 192,
                "matrix": matrix,
                "exitAttempts": attempts,
                "GPU": "GPU",
                "graphicsAPI": "D3D12",
                "driver": "driver",
                "resolutions": ["1920x1080", "3440x1440"],
                "frameRates": [30, 60, 120],
                "captureManifest": captures,
                "playerLog": [],
                "buildLog": "player-build.log",
                "result": "PASS",
            }
            (result_root / "player-visual-result.json").write_text(
                json.dumps(payload), encoding="utf-8"
            )
            failures: list[verify_bundle.Failure] = []
            verify_bundle.verify_player_result(
                root,
                {"exitCode": 0},
                {"bundleId": "B", "sourceFreezeId": "S"},
                {
                    "requiredArtifactThresholds": {
                        "playerExpectedCaptureCount": 192
                    }
                },
                failures,
            )
            self.assertEqual(failures, [])

            first_cell = matrix[0]
            removed_label = self.PLAYER_CAPTURE_LABELS[0]
            captures[:] = [
                row
                for row in captures
                if not (
                    row["width"] == first_cell["width"]
                    and row["height"] == first_cell["height"]
                    and row["frameRate"] == first_cell["frameRate"]
                    and row["focus"] == first_cell["focus"]
                    and row["label"] == removed_label
                )
            ]
            second_cell = matrix[1]
            second_directory = (
                f"Player-{second_cell['width']}x{second_cell['height']}-"
                f"{second_cell['frameRate']}fps-{second_cell['focus']}"
            )
            extra_relative = f"{second_directory}/unexpected-extra.png"
            extra_capture = result_root / extra_relative
            extra_capture.write_bytes(extra_relative.encode("utf-8"))
            captures.append(
                {
                    "path": extra_relative,
                    "sha256": verify_bundle.sha256_file(extra_capture),
                    "width": second_cell["width"],
                    "height": second_cell["height"],
                    "frameRate": second_cell["frameRate"],
                    "focus": second_cell["focus"],
                    "label": "unexpected-extra",
                }
            )
            (result_root / "player-visual-result.json").write_text(
                json.dumps(payload), encoding="utf-8"
            )
            failures = []
            verify_bundle.verify_player_result(
                root,
                {"exitCode": 0},
                {"bundleId": "B", "sourceFreezeId": "S"},
                {
                    "requiredArtifactThresholds": {
                        "playerExpectedCaptureCount": 192
                    }
                },
                failures,
            )
            self.assertIn(
                "PLAYER_RESULT_INCOMPLETE",
                {failure.code for failure in failures},
            )

    def test_selector_is_recomputed_from_raw_csv_semantics(self) -> None:
        rows = [
            {
                "render_sequence_index": "0",
                "input_radius": "1",
                "effective_radius": "10",
                "closed_overshoot": "0",
                "next_changed_pixels": "2",
                "next_max_channel_delta": "4",
                "next_unexpected_chroma_pixels": "0",
                "transparent_pixel_count": "2",
                "contour_radius": "3",
                "authoring_exact_closed": "false",
                "capture_sha256": "a",
            },
            {
                "render_sequence_index": "1",
                "input_radius": "0",
                "effective_radius": "-4",
                "closed_overshoot": "4",
                "next_changed_pixels": "0",
                "next_max_channel_delta": "0",
                "next_unexpected_chroma_pixels": "0",
                "transparent_pixel_count": "0",
                "contour_radius": "0",
                "authoring_exact_closed": "true",
                "capture_sha256": "b",
            },
            {
                "render_sequence_index": "2",
                "input_radius": "0",
                "effective_radius": "-4",
                "closed_overshoot": "4",
                "next_changed_pixels": "0",
                "next_max_channel_delta": "0",
                "next_unexpected_chroma_pixels": "0",
                "transparent_pixel_count": "0",
                "contour_radius": "0",
                "authoring_exact_closed": "true",
                "capture_sha256": "b",
            },
        ]
        selector = verify_bundle.recompute_selector(rows)
        self.assertEqual(selector["lastAnimatedParameterFrame"], 0)
        self.assertEqual(selector["lastPixelChangingFrame"], 0)
        self.assertEqual(selector["firstExactClosedFrame"], 1)
        self.assertEqual(selector["nextStableClosedFrame"], 2)
        self.assertFalse(selector["changedAfterStableClosed"])

    def test_heatmap_difference_recalculation(self) -> None:
        first = bytes((0, 0, 0, 255, 10, 20, 30, 40))
        second = bytes((5, 2, 1, 255, 9, 40, 31, 42))
        self.assertEqual(
            verify_bundle.canonical_difference(first, second),
            bytes((5, 0, 0, 255, 20, 0, 0, 255)),
        )

    def test_snapshot_detects_input_mutation(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            path = root / "artifact.txt"
            path.write_text("before", encoding="utf-8")
            before = verify_bundle.snapshot_bundle(root)
            path.write_text("after", encoding="utf-8")
            after = verify_bundle.snapshot_bundle(root)
            difference = verify_bundle.snapshot_difference(before, after)
            self.assertEqual(difference["changed"], ["artifact.txt"])

    def test_artifact_manifest_path_set_equality(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "required.txt").write_text("required", encoding="utf-8")
            digest = verify_bundle.sha256_file(root / "required.txt")
            (root / "artifact-hashes.sha256").write_text(
                f"{digest}  required.txt\n", encoding="utf-8"
            )
            produced = {
                "bundleId": "B",
                "sourceFreezeId": "S",
                "artifactCount": 1,
                "artifactManifestSha256": verify_bundle.sha256_file(
                    root / "artifact-hashes.sha256"
                ),
                "filesystemPathSetSha256": hashlib.sha256(
                    b"required.txt"
                ).hexdigest(),
                "explicitExclusions": [
                    "artifact-hashes.sha256",
                    "bundle-produced.json",
                    "BUNDLE_CLOSED",
                ],
            }
            (root / "bundle-produced.json").write_text(
                json.dumps(produced), encoding="utf-8"
            )
            closed = {
                "bundleId": "B",
                "sourceFreezeId": "S",
                "bundleProducedSha256": verify_bundle.sha256_file(
                    root / "bundle-produced.json"
                ),
            }
            (root / "BUNDLE_CLOSED").write_text(
                json.dumps(closed), encoding="utf-8"
            )
            failures: list[verify_bundle.Failure] = []
            checks: list[str] = []
            verify_bundle.verify_artifact_manifest(
                root,
                {
                    "allowedExplicitExclusions": [
                        "artifact-hashes.sha256",
                        "bundle-produced.json",
                        "BUNDLE_CLOSED",
                    ],
                    "requiredArtifactRules": [
                        {
                            "pattern": "required.txt",
                            "count": 1,
                            "nonZero": True,
                        }
                    ],
                },
                verify_bundle.snapshot_bundle(root),
                failures,
                checks,
            )
            self.assertEqual(failures, [])


if __name__ == "__main__":
    unittest.main()
