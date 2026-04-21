using Game.Feature.Stages.Editor.Validation;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageContentRefactorTriageTests
    {
        [Test]
        public void Classify_CompatResidue_IsSunsetCore()
        {
            var lane = StageContentRefactorTriage.Classify(
                "Production scene compat residue reached validator blocker.",
                source: "Assets/_Features/Stages/Runtime/Load/StageRuntimeContentResolver.cs");

            Assert.That(lane, Is.EqualTo(StageContentRefactorLane.SunsetCore));
        }

        [Test]
        public void Classify_ResolveLegacyRemoval_IsSunsetCore()
        {
            var lane = StageContentRefactorTriage.Classify(
                "runtime ResolveLegacy residue",
                source: "Assets/_Features/Stages/Runtime/Presentation/StagePresentationAssemblers.cs");

            Assert.That(lane, Is.EqualTo(StageContentRefactorLane.SunsetCore));
        }

        [Test]
        public void Classify_PresentationIdZeroization_IsSunsetCore()
        {
            var lane = StageContentRefactorTriage.Classify(
                "canonical set PresentationId zeroization regression",
                source: "Assets/_Features/Stages/Runtime/Validation/StageCatalogValidator.cs");

            Assert.That(lane, Is.EqualTo(StageContentRefactorLane.SunsetCore));
        }

        [Test]
        public void Classify_StageResultPayloadMissingStageId_IsStageIdUxContract()
        {
            var lane = StageContentRefactorTriage.Classify(
                "StageResultScreenPayload lost StageId payload contract",
                source: "Assets/_Features/UI/UI_Screens/Runtime/ScreenModels.cs");

            Assert.That(lane, Is.EqualTo(StageContentRefactorLane.StageIdUxContract));
        }

        [Test]
        public void Classify_DirectGameplayReturnInUiFlow_IsStageIdUxContract()
        {
            var lane = StageContentRefactorTriage.Classify(
                "UIFlowCoordinator still routes to ScreenId.Gameplay on continue",
                source: "Assets/_Features/UI/UI_Flow/Runtime/UIFlowCoordinator.cs");

            Assert.That(lane, Is.EqualTo(StageContentRefactorLane.StageIdUxContract));
        }

        [Test]
        public void Classify_PrefabLayoutDrift_IsAdjacentBroadBacklog()
        {
            var lane = StageContentRefactorTriage.Classify(
                "Settings prefab layout drift",
                source: "Assets/_Features/UI/UI_Composition/Runtime/GameplayScreenRuntimeFactory.cs");

            Assert.That(lane, Is.EqualTo(StageContentRefactorLane.AdjacentBroadBacklog));
        }

        [Test]
        public void Classify_AudioViewRegression_IsAdjacentBroadBacklog()
        {
            var lane = StageContentRefactorTriage.Classify(
                "audio presenter visual mismatch",
                source: "Assets/_Features/UI/UI_Tests/EditMode/UiPresentationMappingTests.cs");

            Assert.That(lane, Is.EqualTo(StageContentRefactorLane.AdjacentBroadBacklog));
        }
    }
}
