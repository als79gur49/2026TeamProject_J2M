#!/usr/bin/env python3
from __future__ import annotations

import unittest
from unittest import mock

import produce_bundle


class ProduceBundleUnitTests(unittest.TestCase):
    def test_bundle_parent_defaults_to_repository_test_logs(self) -> None:
        with mock.patch.dict(
            "os.environ",
            {produce_bundle.EVIDENCE_BUNDLE_ROOT_ENV: ""},
        ):
            self.assertEqual(
                produce_bundle.resolve_bundle_parent(),
                produce_bundle.DEFAULT_BUNDLE_PARENT,
            )

    def test_bundle_parent_accepts_external_root_override(self) -> None:
        external_root = "/mnt/d/J2M/evidence/terminal-transition/bundles"
        with mock.patch.dict(
            "os.environ",
            {produce_bundle.EVIDENCE_BUNDLE_ROOT_ENV: external_root},
        ):
            self.assertEqual(
                produce_bundle.resolve_bundle_parent(),
                produce_bundle.Path(external_root).resolve(),
            )

    def test_player_visual_runner_supports_external_build_root(self) -> None:
        runner = (produce_bundle.PROJECT_ROOT / "run_tests.sh").read_text(
            encoding="utf-8"
        )
        self.assertEqual(
            runner.count("TERMINAL_IRIS_QUALITY_PLAYER_BUILD_ROOT"),
            4,
        )

    def test_result_origin_root_matches_runner_override_precedence(self) -> None:
        with mock.patch.dict(
            "os.environ",
            {
                "CODEX_VALIDATION_ROOT": "/mnt/d/J2M/evidence/validation",
                "TEST_RESULTS_ROOT": "",
            },
        ):
            self.assertEqual(
                produce_bundle.resolve_test_results_root(),
                produce_bundle.Path(
                    "/mnt/d/J2M/evidence/validation/test-results"
                ).resolve(),
            )
        with mock.patch.dict(
            "os.environ",
            {
                "CODEX_VALIDATION_ROOT": "/ignored",
                "TEST_RESULTS_ROOT": "/mnt/d/J2M/evidence/explicit-results",
            },
        ):
            self.assertEqual(
                produce_bundle.resolve_test_results_root(),
                produce_bundle.Path(
                    "/mnt/d/J2M/evidence/explicit-results"
                ).resolve(),
            )

    def test_player_visual_lane_has_no_unrelated_test_result_origins(self) -> None:
        lane = produce_bundle.Lane(
            "player-visual-quality",
            "CMD-10",
            ("./run_tests.sh", "terminal-iris-player-visual-quality"),
            "player-visual",
        )
        self.assertEqual(produce_bundle.result_origins(lane), [])

    def test_lane_closure_requires_allowed_complete_exact_set(self) -> None:
        contracts = {
            "architecture": {
                "kind": "unity-test-framework",
                "allowedExitCodes": [0],
            },
            "player": {
                "kind": "player-visual",
                "allowedExitCodes": [0],
            },
        }
        records = [
            {
                "laneId": "architecture",
                "commandExitCode": 0,
                "missingOrStaleOrigins": [],
                "resultStatus": "COMPLETED",
            },
            {
                "laneId": "player",
                "commandExitCode": 0,
                "missingOrStaleOrigins": [],
                "resultStatus": "PASS",
            },
        ]
        self.assertEqual(
            produce_bundle.lane_closure_failures(records, contracts), []
        )
        records[1]["commandExitCode"] = 1
        records[1]["resultStatus"] = "INCOMPLETE"
        failures = produce_bundle.lane_closure_failures(records, contracts)
        self.assertTrue(any("exit 1" in failure for failure in failures))
        self.assertTrue(any("INCOMPLETE" in failure for failure in failures))

    def test_only_documented_import_drift_is_restorable(self) -> None:
        self.assertTrue(
            produce_bundle.can_restore_known_unity_import_drift(
                set(),
                {produce_bundle.KNOWN_UNITY_IMPORT_DRIFT_PATH},
            )
        )

    def test_other_or_preexisting_changes_are_not_restorable(self) -> None:
        path = produce_bundle.KNOWN_UNITY_IMPORT_DRIFT_PATH
        self.assertFalse(
            produce_bundle.can_restore_known_unity_import_drift(
                set(), {path, "Assets/Unexpected.asset"}
            )
        )
        self.assertFalse(
            produce_bundle.can_restore_known_unity_import_drift(
                {path}, {path}
            )
        )


if __name__ == "__main__":
    unittest.main()
