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
            Assert.That(document, Does.Contain("For cleaned cues, flag off means that VFX is off"));
            Assert.That(document, Does.Contain("it does not mean old presenter fallback"));
            Assert.That(document, Does.Contain("Cleanup, visibility, transform reset, and motion ownership responsibilities remain"));
            Assert.That(document, Does.Contain("Compatibility migration gate properties remain"));
            Assert.That(document, Does.Contain("High-risk suppress gates for enemy death motion and FlipDestroySelf motion remain meaningful rollback gates"));
        }

        [Test]
        [Category("Extended")]
        public void CleanedOldPaths_AreListed()
        {
            var document = ReadRepoFile(GovernancePath);

            Assert.That(document, Does.Contain("`GameplayTickPresentationCoordinator.PlayPlayerHitEffects`"));
            Assert.That(document, Does.Contain("BoxDestroy `GameplayExitPresentationController.PlayExitEffect`"));
            Assert.That(document, Does.Contain("ItemConsume `GameplayExitPresentationController.PlayExitEffect`"));
            Assert.That(document, Does.Contain("`GameplayUtilityWindupVfxPresenter.RefreshSummonWarnings`"));
            Assert.That(document, Does.Contain("`GameplayFrontFaceShieldVfxPresenter.RefreshActiveSources`"));
            Assert.That(document, Does.Contain("`GameplayFrontFaceShieldVfxPresenter.PlayBlockBursts`"));
        }

        [Test]
        [Category("Extended")]
        public void RetainedHighRiskOldPaths_AreListed()
        {
            var document = ReadRepoFile(GovernancePath);

            Assert.That(document, Does.Contain("Retained high-risk legacy paths"));
            Assert.That(document, Does.Contain("old fly-away fallback remains when `EnableGameplayVfxEnemyDeathMotionMigration` is false"));
            Assert.That(document, Does.Contain("old clone/fade fallback remains when `EnableGameplayVfxFlipDestroySelfMotionMigration` is false"));
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
        }

        [Test]
        [Category("Extended")]
        public void RetainedHighRiskOldPaths_CodeKeepsFlagOffFallbacks()
        {
            var exitController = ReadRepoFile(ExitControllerPath);
            var runtime = ReadRepoFile(RuntimePath);

            Assert.That(exitController, Does.Contain("PlayFlipImpactDestroyEffect"));
            Assert.That(exitController, Does.Contain("suppressLegacyFlipDestroySelfEffects"));
            Assert.That(exitController, Does.Contain("suppressLegacyEnemyDeathEffects"));
            Assert.That(runtime, Does.Contain("SuppressLegacyEnemyDeathEffects => enableGameplayVfxEnemyDeathMotionMigration"));
            Assert.That(runtime, Does.Contain("SuppressLegacyFlipDestroySelfEffects => enableGameplayVfxFlipDestroySelfMotionMigration"));
        }

        [Test]
        [Category("Extended")]
        public void CleanedSuppressGates_AreCompatibilityAliases()
        {
            var runtime = ReadRepoFile(RuntimePath);

            Assert.That(typeof(IGameplayPresentationMigrationGate).GetProperty("SuppressLegacyPlayerDamageHitEffects"), Is.Not.Null);
            Assert.That(typeof(IGameplayPresentationMigrationGate).GetProperty("SuppressLegacyBoxDestroyShrinkEffects"), Is.Not.Null);
            Assert.That(typeof(IGameplayPresentationMigrationGate).GetProperty("SuppressLegacyItemConsumeEffects"), Is.Not.Null);
            Assert.That(typeof(IGameplayPresentationMigrationGate).GetProperty("SuppressLegacyUtilityWindupVfx"), Is.Not.Null);
            Assert.That(typeof(IGameplayPresentationMigrationGate).GetProperty("SuppressLegacyFrontFaceShieldActiveVfx"), Is.Not.Null);
            Assert.That(typeof(IGameplayPresentationMigrationGate).GetProperty("SuppressLegacyFrontFaceShieldBlockVfx"), Is.Not.Null);
            Assert.That(runtime, Does.Contain("SuppressLegacyPlayerDamageHitEffects => true"));
            Assert.That(runtime, Does.Contain("SuppressLegacyBoxDestroyShrinkEffects => true"));
            Assert.That(runtime, Does.Contain("SuppressLegacyItemConsumeEffects => true"));
            Assert.That(runtime, Does.Contain("SuppressLegacyUtilityWindupVfx => true"));
            Assert.That(runtime, Does.Contain("SuppressLegacyFrontFaceShieldActiveVfx => true"));
            Assert.That(runtime, Does.Contain("SuppressLegacyFrontFaceShieldBlockVfx => true"));
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
