#!/usr/bin/env python3
from __future__ import annotations

import binascii
import hashlib
import json
from pathlib import Path
import struct
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
    def test_path_normalization_rejects_parent_and_backslash(self) -> None:
        self.assertEqual(
            verify_bundle.normalize_relative("a/b.txt"), "a/b.txt"
        )
        for value in ("../a", "/a", "a\\b", "./a"):
            with self.assertRaises(ValueError):
                verify_bundle.normalize_relative(value)

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
            "captureCount": 0,
            "expectedCaptureCount": 0,
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
            result = result_root / "player-visual-result.json"
            result.write_text(json.dumps(payload), encoding="utf-8")
            failures: list[verify_bundle.Failure] = []
            verify_bundle.verify_player_result(
                root,
                {"exitCode": 0},
                {"bundleId": "B", "sourceFreezeId": "S"},
                {
                    "requiredArtifactThresholds": {
                        "playerExpectedCaptureCount": 0
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
                        "playerExpectedCaptureCount": 0
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
                        "playerExpectedCaptureCount": 0
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
        repository_codes = {
            failure.code
            for failure in verify_bundle.source_identity_failures(
                freeze, Path("/different-repo"), "a" * 40, "b" * 40
            )
        }
        self.assertEqual(repository_codes, {"SOURCE_REPOSITORY_MISMATCH"})

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
