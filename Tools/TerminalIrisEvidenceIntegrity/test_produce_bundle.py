#!/usr/bin/env python3
from __future__ import annotations

import unittest

import produce_bundle


class ProduceBundleUnitTests(unittest.TestCase):
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
