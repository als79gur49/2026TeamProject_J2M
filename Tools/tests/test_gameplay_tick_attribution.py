import copy
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
MODULE_PATH = REPO_ROOT / "Tools" / "gameplay_tick_attribution.py"
SPEC = importlib.util.spec_from_file_location("gameplay_tick_attribution", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)

REVISION = "1" * 40


def valid_document(sample_count=3):
    samples = []
    for offset in range(sample_count):
        samples.append(
            {
                "tickIndex": 10 + offset,
                "threadId": 7,
                "outerTicks": 100,
                "inputPreparationTicks": 5,
                "simulationTicks": 40,
                "hostPostProcessTicks": 5,
                "presentationTicks": 30,
                "callbackTicks": 20,
                "simulationBootstrapTicks": 5,
                "simulationPlanTicks": 15,
                "simulationResolveTicks": 8,
                "simulationFinalizeAndSnapshotTicks": 3,
                "simulationCleanupAndSnapshotTicks": 2,
                "simulationRespawnAndFinalSnapshotTicks": 2,
                "simulationResultMaterializationTicks": 4,
                "simulationResidualTicks": 1,
                "simulationPlanEnemyAiAndProjectionTicks": 2,
                "simulationPlanKinematicAndGravityProjectionTicks": 2,
                "simulationPlanPreMovementStateAndUtilityProjectionTicks": 2,
                "simulationPlanPreMovementSetupTicks": 0,
                "simulationPlanPreMovementLogicTicks": 1,
                "simulationPlanPreMovementBookkeepingTicks": 0,
                "simulationPlanPreMovementProjectionApplyTicks": 0,
                "simulationPlanPreMovementUtilityInputSnapshotTicks": 1,
                "simulationPlanPreMovementUtilityResolveTicks": 0,
                "simulationPlanPreMovementUtilityProjectionApplyTicks": 0,
                "simulationPlanPreMovementResidualTicks": 0,
                "simulationPlanJumpLandingAndPlayerActionAttemptsTicks": 2,
                "simulationPlanMovementIntentCollectionAndPartitionTicks": 2,
                "simulationPlanLocomotionProjectionTicks": 2,
                "simulationPlanMovementExpansionTicks": 1,
                "simulationPlanPayloadOrderingAndResultTicks": 1,
                "simulationPlanResidualTicks": 1,
                "simulationResolveMovementPlanningAndMaterializationTicks": 1,
                "simulationResolveInitialProjectionAndBeforeAttackStateTicks": 1,
                "simulationResolvePreliminaryAttackAndImpactDispositionTicks": 1,
                "simulationResolveMovementRematerializationAndJumpLandingTicks": 1,
                "simulationResolveTileEffectsAndProjectionTicks": 1,
                "simulationResolveFinalAttackAndMaterializationTicks": 1,
                "simulationResolvePostAttackStateAndUtilityTicks": 1,
                "simulationResolveResultMaterializationTicks": 1,
                "simulationResolveResidualTicks": 0,
                "simulationResolveInitialProjectionSetupAndBatchApplyTicks": 0,
                "simulationResolveInitialPostMovementSnapshotTicks": 1,
                "simulationResolveBeforeAttackAiTransitionTicks": 0,
                "simulationResolveBeforeAttackAiSetupTicks": 0,
                "simulationResolveBeforeAttackAiLogicTicks": 0,
                "simulationResolveBeforeAttackAiProjectionApplyTicks": 0,
                "simulationResolveBeforeAttackAiResidualTicks": 0,
                "simulationResolveBeforeAttackEnemyActionTicks": 0,
                "simulationResolveInitialProjectionResidualTicks": 0,
                "simulationResolveInitialPostMovementSnapshotBaseImportTicks": 1,
                "simulationResolveInitialPostMovementSnapshotOverlayApplyTicks": 0,
                "simulationResolveInitialPostMovementSnapshotMaterializationTicks": 0,
                "simulationResolveInitialPostMovementSnapshotResidualTicks": 0,
                "presentationCoordinatorTicks": 25,
                "presentationCameraAndStateNotificationTicks": 4,
                "presentationResidualTicks": 1,
                "presentationPreCommitPlanningTicks": 5,
                "presentationCommittedFrameAndStateTicks": 5,
                "presentationMotionAnimationVfxTicks": 5,
                "presentationAudioTicks": 4,
                "presentationApplyCleanupUpdateTicks": 5,
                "presentationCoordinatorResidualTicks": 1,
            }
        )
    return {
        "schemaVersion": 7,
        "measurementKind": "gameplay-tick-level7-pre-movement-before-attack-attribution",
        "captureIdentity": {"preBuildHeadSha": REVISION},
        "revision": REVISION,
        "gameplayStage": "stage-4-3",
        "stopwatchFrequency": 1000,
        "sampleCount": sample_count,
        "samples": samples,
    }


class GameplayTickAttributionTests(unittest.TestCase):
    def summarize(self, value):
        return MODULE.validate_and_summarize(
            value,
            expected_revision=REVISION,
            expected_stage="stage-4-3",
            expected_samples=3,
        )

    def test_valid_samples_produce_additive_shares(self):
        report = self.summarize(valid_document())
        self.assertEqual("ADMITTED", report["verdict"])
        self.assertEqual(40.0, report["outerSharesPercent"]["simulationTicks"])
        self.assertEqual(30.0, report["outerSharesPercent"]["presentationTicks"])
        self.assertEqual(20.0, report["outerSharesPercent"]["callbackTicks"])
        self.assertEqual(50.0, report["outerSharesPercent"]["presentationEcosystemTicks"])
        self.assertAlmostEqual(
            100.0,
            sum(report["outerSharesPercent"][field] for field in MODULE.SECTIONS),
        )

    def test_parent_child_mismatch_is_rejected(self):
        value = valid_document()
        value["samples"][1]["simulationTicks"] += 1
        with self.assertRaisesRegex(MODULE.AttributionError, "arithmetic mismatch"):
            self.summarize(value)

    def test_nested_parent_child_mismatch_is_rejected(self):
        for field in (
            "simulationBootstrapTicks",
            "simulationPlanEnemyAiAndProjectionTicks",
            "simulationPlanPreMovementLogicTicks",
            "simulationResolveFinalAttackAndMaterializationTicks",
            "simulationResolveBeforeAttackAiTransitionTicks",
            "simulationResolveBeforeAttackAiLogicTicks",
            "simulationResolveInitialPostMovementSnapshotBaseImportTicks",
            "presentationCameraAndStateNotificationTicks",
            "presentationAudioTicks",
        ):
            value = valid_document()
            value["samples"][1][field] += 1
            with self.subTest(field=field), self.assertRaisesRegex(
                MODULE.AttributionError, "child arithmetic mismatch"
            ):
                self.summarize(value)

    def test_level6_document_is_rejected(self):
        value = valid_document()
        value["schemaVersion"] = 6
        value["measurementKind"] = (
            "gameplay-tick-level6-resolve-initial-post-movement-snapshot-attribution"
        )
        with self.assertRaisesRegex(MODULE.AttributionError, "schemaVersion must be 7"):
            self.summarize(value)

    def test_thread_and_tick_sequence_are_fail_closed(self):
        for mutation in ("thread", "duplicate", "gap"):
            value = valid_document()
            if mutation == "thread":
                value["samples"][1]["threadId"] = 8
            elif mutation == "duplicate":
                value["samples"][1]["tickIndex"] = 10
            else:
                value["samples"][1]["tickIndex"] = 15
            with self.subTest(mutation=mutation), self.assertRaises(MODULE.AttributionError):
                self.summarize(value)

    def test_identity_count_and_shape_are_fail_closed(self):
        mutations = []
        wrong_revision = valid_document()
        wrong_revision["revision"] = "2" * 40
        mutations.append(wrong_revision)
        wrong_stage = valid_document()
        wrong_stage["gameplayStage"] = "stage-4-2"
        mutations.append(wrong_stage)
        wrong_count = valid_document()
        wrong_count["sampleCount"] = 2
        mutations.append(wrong_count)
        extra_field = valid_document()
        extra_field["samples"][0]["unexpected"] = 1
        mutations.append(extra_field)
        for value in mutations:
            with self.subTest(value=value), self.assertRaises(MODULE.AttributionError):
                self.summarize(value)

    def test_full_capture_identity_mismatch_is_rejected(self):
        value = valid_document()
        expected_identity = copy.deepcopy(value["captureIdentity"])
        expected_identity["captureNonce"] = "expected"
        with self.assertRaisesRegex(MODULE.AttributionError, "admitted performance metrics"):
            MODULE.validate_and_summarize(
                value,
                expected_revision=REVISION,
                expected_stage="stage-4-3",
                expected_samples=3,
                expected_capture_identity=expected_identity,
            )

    def test_duplicate_json_members_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "duplicate.json"
            path.write_text('{"schemaVersion":1,"schemaVersion":1}', encoding="utf-8")
            with self.assertRaisesRegex(MODULE.AttributionError, "duplicate JSON member"):
                MODULE.load_json(path)


if __name__ == "__main__":
    unittest.main()
