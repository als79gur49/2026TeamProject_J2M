using System.IO;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Vfx.Host;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxLegacyOldPathCleanupTests
    {
        private const string GovernancePath = "Docs/Architecture/Gameplay-VFX-Governance.md";
        private const string CoordinatorPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs";
        private const string ExitControllerPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayExitPresentationController.cs";
        private const string RuntimePath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs";

        [Test]
        [Category("Extended")]
        public void Governance_DocumentsLegacyCleanupPolicy()
        {
            var document = ReadRepoFile(GovernancePath);

            Assert.That(document, Does.Contain("## Gameplay VFX Legacy Old Path Cleanup"));
            Assert.That(document, Does.Contain("For all current migrated cues, flag off means that VFX is off"));
            Assert.That(document, Does.Contain("it does not mean old presenter fallback"));
            Assert.That(document, Does.Contain("Cleanup, visibility, transform reset, and motion ownership responsibilities remain"));
            Assert.That(document, Does.Contain("suppress compatibility gates were removed"));
            Assert.That(document, Does.Contain("old fallback = none"));
            Assert.That(document, Does.Contain("For all current migrated cues, flag off means that VFX is off"));
        }

        [Test]
        [Category("Extended")]
        public void CleanedOldPaths_AreListed()
        {
            var document = ReadRepoFile(GovernancePath);

            Assert.That(document, Does.Contain("Player damage direct hit prefab fallback"));
            Assert.That(document, Does.Contain("BoxDestroy `GameplayExitPresentationController.PlayExitEffect`"));
            Assert.That(document, Does.Contain("ItemConsume `GameplayExitPresentationController.PlayExitEffect`"));
            Assert.That(document, Does.Contain("`GameplayUtilityWindupVfxPresenter.RefreshSummonWarnings`"));
            Assert.That(document, Does.Contain("`GameplayFrontFaceShieldVfxPresenter.RefreshActiveSources`"));
            Assert.That(document, Does.Contain("`GameplayFrontFaceShieldVfxPresenter.PlayBlockBursts`"));
        }

        [Test]
        [Category("Extended")]
        public void FinalizedHighRiskOldPaths_AreListed()
        {
            var document = ReadRepoFile(GovernancePath);

            Assert.That(document, Does.Contain("old fly-away disabled"));
            Assert.That(document, Does.Contain("old clone/fade disabled"));
            Assert.That(document, Does.Contain("Remaining old canonical presentation responsibilities"));
            Assert.That(document, Does.Contain("OutOfBounds `GameplayExitPresentationController.PlayExitEffect`"));
            Assert.That(document, Does.Contain("`GameplayFrontFaceShieldVfxPresenter.RefreshWindupWarnings`"));
            Assert.That(document, Does.Contain("`GameplayExitPresentationController.PlayImpactBreakEffect`"));
            Assert.That(document, Does.Contain("`BoxFlipInteractionDriver` and `FlipImpactTrack` Stay branch"));
        }

        [Test]
        [Category("Extended")]
        public void CleanedOldPaths_AreNotDocumentedAsFlagOffFallback()
        {
            var document = ReadRepoFile(GovernancePath);

            Assert.That(document, Does.Not.Contain("smoke off / shrink off: old BoxDestroy shrink/fade only"));
            Assert.That(document, Does.Not.Contain("smoke on / shrink off: old BoxDestroy shrink/fade plus"));
            Assert.That(document, Does.Not.Contain("flag off old-only"));
            Assert.That(document, Does.Not.Contain("rollback is setting `EnableGameplayVfxDamageBurstMigration` false"));
            Assert.That(document, Does.Not.Contain("rollback is setting `EnableGameplayVfxUtilityWindupMigration` false"));
            Assert.That(document, Does.Not.Contain("old fly-away only"));
            Assert.That(document, Does.Not.Contain("old clone/fade fallback remains"));
        }

        [Test]
        [Category("Extended")]
        public void FinalizedHighRiskOldPaths_CodeHasNoFlagOffFallbacks()
        {
            var exitController = ReadRepoFile(ExitControllerPath);
            var coordinator = ReadRepoFile(CoordinatorPath);
            var runtime = ReadRepoFile(RuntimePath);

            Assert.That(exitController, Does.Not.Contain("PlayFlipImpactDestroyEffect"));
            Assert.That(exitController, Does.Not.Contain("suppressLegacyFlipDestroySelfEffects"));
            Assert.That(exitController, Does.Not.Contain("suppressLegacyEnemyDeathEffects"));
            Assert.That(coordinator, Does.Not.Contain("PlayPlayerHitEffects(TickResult"));
            Assert.That(runtime, Does.Not.Contain("SuppressLegacy"));
        }

        [Test]
        [Category("Extended")]
        public void SuppressGateInterface_Removed()
        {
            var extensionSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationExtension.cs");
            var runtime = ReadRepoFile(RuntimePath);

            Assert.That(extensionSource, Does.Not.Contain("IGameplayPresentationMigrationGate"));
            Assert.That(runtime, Does.Not.Contain("IGameplayPresentationMigrationGate"));
            Assert.That(runtime, Does.Not.Contain("SuppressLegacy"));
        }

        [Test]
        [Category("Extended")]
        public void ApplyEntityExitOwnership_StillRuns()
        {
            var coordinator = ReadRepoFile(CoordinatorPath);
            var exitController = ReadRepoFile(ExitControllerPath);

            Assert.That(exitController, Does.Contain("public void ApplyEntityExitOwnership()"));
            Assert.That(coordinator, Does.Contain("TraceStep(\"ApplyEntityExitOwnership\")"));
            Assert.That(coordinator, Does.Contain("_exitPresentationController.ApplyEntityExitOwnership();"));
        }

        [Test]
        [Category("Extended")]
        public void NoAuthorityOrPresentationCarrierShapeChanges_Documented()
        {
            var document = ReadRepoFile(GovernancePath);

            Assert.That(document, Does.Contain("no `TickPipeline`, `WorldState`, `WorldSnapshot`, `ProjectedWorld`, `FinalizationBatch`, `DeterminismHashBuilder`, `TickPresentationData`, `TickEntityExitPresentationSignal`, `TickEntityMotion`, or `TickResultBuilder` changes"));
        }

        private static string ReadRepoFile(string path)
        {
            Assert.That(File.Exists(path), Is.True, $"Missing repo file: {path}");
            return File.ReadAllText(path);
        }
    }
}
