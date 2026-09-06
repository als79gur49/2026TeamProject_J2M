from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

import gameplay_test_stratification_lib as stratification


class GameplayTestStratificationBoundaryTests(unittest.TestCase):
    def test_core_host_import_is_allowed_only_for_two_exact_paths(self) -> None:
        allowed_paths = tuple(stratification.CORE_IMPORT_EXCEPTIONS_BY_PATH)
        self.assertEqual(2, len(allowed_paths))
        for path in allowed_paths:
            self.assertTrue(stratification.is_core_import_allowed(path, "Game.Feature.Gameplay.Host"))
            self.assertTrue(
                stratification.is_core_import_allowed(path.replace("/", "\\"), "Game.Feature.Gameplay.Host")
            )
        self.assertFalse(
            stratification.is_core_import_allowed(
                allowed_paths[0].replace("SnapshotTests", "SnapshotNearMissTests"),
                "Game.Feature.Gameplay.Host",
            )
        )
        self.assertFalse(
            stratification.is_core_import_allowed(
                "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Core/ThirdCoreFixtureTests.cs",
                "Game.Feature.Gameplay.Host",
            )
        )

    def test_core_candidate_and_assembly_guard_share_exact_path_exception(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_root:
            root = Path(temporary_root)
            relative = next(iter(stratification.CORE_IMPORT_EXCEPTIONS_BY_PATH))
            source = root / relative
            source.parent.mkdir(parents=True)
            source.write_text("using Game.Feature.Gameplay.Host;\n", encoding="utf-8")
            test = self._test_method(source, relative)
            self.assertTrue(stratification.is_core_candidate(test, stratification.FEATURE_ASSEMBLY_NAME))
            self.assertEqual([], stratification.check_core_assembly_rules(root, [], {}))

            near_miss = source.with_name("EnemyAnimationBindingSnapshotNearMissTests.cs")
            near_miss.write_text("using Game.Feature.Gameplay.Host;\n", encoding="utf-8")
            near_miss_relative = near_miss.relative_to(root).as_posix()
            near_miss_test = self._test_method(near_miss, near_miss_relative)
            self.assertFalse(
                stratification.is_core_candidate(near_miss_test, stratification.FEATURE_ASSEMBLY_NAME)
            )
            issues = stratification.check_core_assembly_rules(root, [], {})
            self.assertEqual(1, len(issues))
            self.assertIn(near_miss_relative, issues[0])

    def test_infrastructure_reference_and_editor_tools_imports_are_exact(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_root:
            root = Path(temporary_root)
            infra_root = root / "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/TestSupport/Infrastructure"
            infra_root.mkdir(parents=True)
            asmdef = infra_root / "Game.TestInfrastructure.asmdef"
            asmdef.write_text(
                json.dumps({"references": sorted(stratification.APPROVED_INFRASTRUCTURE_REFERENCES)}),
                encoding="utf-8",
            )
            for relative in stratification.INFRASTRUCTURE_EDITOR_TOOLS_IMPORT_PATHS:
                (root / relative).write_text(
                    "using Game.Feature.Gameplay.Host.EditorTools;\n", encoding="utf-8"
                )
            self.assertEqual([], stratification.check_infrastructure_rules(root, [], {}))

            near_miss = infra_root / "EnemyAnimationEditorToolsNearMissTests.cs"
            near_miss.write_text("using Game.Feature.Gameplay.Host.EditorTools;\n", encoding="utf-8")
            issues = stratification.check_infrastructure_rules(root, [], {})
            self.assertEqual(1, len(issues))
            self.assertIn("restricted infrastructure dependency", issues[0])

            near_miss.unlink()
            asmdef.write_text(
                json.dumps({"references": sorted(stratification.APPROVED_INFRASTRUCTURE_REFERENCES | {
                    "Game.Feature.Unrelated.Editor", "Game.Feature.Gameplay.Host"
                })}),
                encoding="utf-8",
            )
            issues = stratification.check_infrastructure_rules(root, [], {})
            self.assertEqual(1, len(issues))
            self.assertIn("Game.Feature.Gameplay.Host", issues[0])
            self.assertIn("Game.Feature.Unrelated.Editor", issues[0])

    def test_all_seven_canonical_full_files_resolve_to_full(self) -> None:
        expected = {
            "EnemyViewAnimatorControllerContractTests.cs",
            "EnemyAnimationBindingDispatchTests.cs",
            "EnemyAnimationSparseBindingRuntimeScenarioTests.cs",
            "EnemyAnimationBindingAuthoringTests.cs",
            "EnemyAnimationBindingEditorValidationTests.cs",
            "EnemyAnimationBindingMigrationTests.cs",
            "EnemyAnimationSparseBindingAssetCharacterizationTests.cs",
        }
        self.assertTrue(expected.issubset(stratification.FULL_FILE_NAMES))
        for file_name in expected:
            test = self._test_method(Path(file_name), file_name)
            self.assertEqual(
                "Full", stratification.auto_category(test, [], stratification.FEATURE_ASSEMBLY_NAME)
            )

    def test_repo_relative_normalization_matches_windows_and_wsl(self) -> None:
        path = next(iter(stratification.CORE_IMPORT_EXCEPTIONS_BY_PATH))
        self.assertEqual(
            stratification.normalize_repo_relative_path(path),
            stratification.normalize_repo_relative_path(path.replace("/", "\\")),
        )

    @staticmethod
    def _test_method(path: Path, relative_path: str) -> stratification.TestMethod:
        return stratification.TestMethod(
            absolute_path=path,
            relative_path=relative_path,
            mode="EditMode",
            namespace="Game.Feature.Gameplay.Tests.Core",
            class_name="BoundaryTests",
            method_name="Boundary",
            fully_qualified_name="Game.Feature.Gameplay.Tests.Core.BoundaryTests.Boundary",
            line_number=1,
            attribute_block_start=0,
            attribute_block_end=0,
            attribute_lines=['[Test]', '[Category("Core")]'],
            body="",
            newline="\n",
        )


if __name__ == "__main__":
    unittest.main()
