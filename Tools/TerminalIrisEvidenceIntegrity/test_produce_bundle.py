#!/usr/bin/env python3
from __future__ import annotations

import unittest

import produce_bundle


class ProduceBundleUnitTests(unittest.TestCase):
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
