using System;
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
        private const string UtilityWindupAuthoringPath =
            "Assets/_Features/Gameplay/Gameplay_EntityView/Runtime/EnemyUtilityWindupPresentationAuthoring.cs";
        private const string FrontFaceShieldAuthoringPath =
            "Assets/_Features/Gameplay/Gameplay_EntityView/Runtime/EnemyFrontFaceShieldPresentationAuthoring.cs";
        private const string FrontFaceShieldPresenterPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayFrontFaceShieldVfxPresenter.cs";
        private const string FrontFaceShieldPrefabPath =
            "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Enemy/Prefabs/EnemyView_FrontFaceShield.prefab";
        private const string CurrentFrontFaceShieldActivePrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/FrontFaceShieldActiveVfx.prefab";
        private const string CurrentFrontFaceShieldBlockPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/FrontFaceShieldBlockVfx.prefab";
        private const string CurrentFrontFaceShieldActiveMaterialPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Materials/M_FrontFaceShieldActive_Blue.mat";
        private const string CurrentFrontFaceShieldBlockMaterialPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Materials/M_FrontFaceShieldBlock_Cyan.mat";
        private const string CurrentFrontFaceShieldActiveBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/FrontFaceShieldActive_Binding.asset";
        private const string CurrentFrontFaceShieldBlockBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/FrontFaceShieldBlock_Binding.asset";
        private const string HostDefaultCueMapPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset";
        private const string LegacyFrontFaceShieldActivePrefabPath =
            "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Enemy/VFX/FrontFaceShield/VFX_FrontFaceShield_ActiveLoop.prefab";
        private const string LegacyFrontFaceShieldActiveMaterialPath =
            "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Enemy/VFX/FrontFaceShield/M_FrontFaceShield_ActiveLoop.mat";
        private const string LegacyFrontFaceShieldBlockPrefabPath =
            "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Enemy/VFX/FrontFaceShield/VFX_FrontFaceShield_BlockBurst.prefab";
        private const string LegacyFrontFaceShieldBlockMaterialPath =
            "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Enemy/VFX/FrontFaceShield/M_FrontFaceShield_BlockBurst.mat";
        private const string LegacyFrontFaceShieldTelegraphPrefabPath =
            "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Enemy/VFX/FrontFaceShield/VFX_FrontFaceShield_Telegraph.prefab";
        private const string LegacyFrontFaceShieldTelegraphMaterialPath =
            "Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Enemy/VFX/FrontFaceShield/M_FrontFaceShield_Telegraph.mat";

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
            Assert.That(document, Does.Contain("`GameplayFrontFaceShieldVfxPresenter.RefreshWindupWarnings`"));
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
            Assert.That(document, Does.Contain("`GameplayExitPresentationController.PlayImpactBreakEffect`"));
            Assert.That(document, Does.Contain("`BoxFlipInteractionDriver` and `FlipImpactTrack` Stay branch"));
            Assert.That(document, Does.Not.Contain("windup warning path remains old canonical presentation"));
        }

        [Test]
        [Category("Extended")]
        public void OutOfBounds_DormantReservedPolicyIsDocumented()
        {
            var document = ReadRepoFile(GovernancePath);

            Assert.That(document, Does.Contain("## OutOfBounds Exit Policy Gate"));
            Assert.That(document, Does.Contain("dormant/reserved entity exit cause"));
            Assert.That(document, Does.Contain("no Gameplay VFX cue is added for OutOfBounds"));
            Assert.That(document, Does.Contain("no OutOfBounds gameplay producer is added"));
            Assert.That(document, Does.Contain("not stale fallback for any migrated Gameplay VFX fact"));
            Assert.That(document, Does.Contain("reserved old canonical exit presentation"));
        }

        [Test]
        [Category("Extended")]
        public void OutOfBounds_NotConsumedByGameplayVfxPlanner()
        {
            var enums = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Vfx/Runtime/GameplayVfxEnums.cs");
            var planning = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Vfx/Runtime/GameplayVfxPlanning.cs");
            var boxShrinkBuilder = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/BoxDestroyShrinkVfxCommandBuilder.cs");
            var enemyDeathBuilder = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/EnemyDeathMotionVfxCommandBuilder.cs");

            Assert.That(enums, Does.Not.Contain("OutOfBoundsExit"));
            Assert.That(enums, Does.Not.Contain("OutOfBounds"));
            Assert.That(planning, Does.Not.Contain("TickEntityExitCause.OutOfBounds"));
            Assert.That(boxShrinkBuilder, Does.Not.Contain("TickEntityExitCause.OutOfBounds"));
            Assert.That(enemyDeathBuilder, Does.Not.Contain("TickEntityExitCause.OutOfBounds"));
        }

        [Test]
        [Category("Extended")]
        public void TickPipeline_DoesNotProduceOutOfBoundsExitCauseHint()
        {
            var loopFiles = Directory.GetFiles(
                "Assets/_Features/Gameplay/Gameplay_Loop/Runtime",
                "*.cs",
                SearchOption.TopDirectoryOnly);
            var combined = string.Join("\n", Array.ConvertAll(loopFiles, ReadRepoFile));

            Assert.That(combined, Does.Not.Contain("exitCauseHint: TickEntityExitCause.OutOfBounds"));
            Assert.That(combined, Does.Not.Contain("ExitCauseHint = TickEntityExitCause.OutOfBounds"));
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

        [Test]
        [Category("Extended")]
        public void DeferredSerializedReferenceFields_AreRemovedFromSourceAndYaml()
        {
            var utilityAuthoring = ReadRepoFile(UtilityWindupAuthoringPath);
            var shieldAuthoring = ReadRepoFile(FrontFaceShieldAuthoringPath);
            var shieldPresenter = ReadRepoFile(FrontFaceShieldPresenterPath);
            var shieldPrefab = ReadRepoFile(FrontFaceShieldPrefabPath);

            Assert.That(utilityAuthoring, Does.Not.Contain(UtilityRemovedField()));
            Assert.That(utilityAuthoring, Does.Not.Contain("Summon" + "WindupWarningPrefab"));
            Assert.That(shieldAuthoring, Does.Not.Contain(ShieldActiveRemovedField()));
            Assert.That(shieldAuthoring, Does.Not.Contain(ShieldBlockRemovedField()));
            Assert.That(shieldAuthoring, Does.Not.Contain("Active" + "LoopPrefab"));
            Assert.That(shieldAuthoring, Does.Not.Contain("Block" + "BurstPrefab"));
            Assert.That(shieldPresenter, Does.Not.Contain("Active" + "LoopPrefab"));
            Assert.That(shieldPresenter, Does.Not.Contain("Block" + "BurstPrefab"));

            Assert.That(shieldPrefab, Does.Not.Contain(ShieldActiveRemovedField()));
            Assert.That(shieldPrefab, Does.Not.Contain(ShieldBlockRemovedField()));
            Assert.That(shieldPrefab, Does.Contain("telegraphPrefab"));
            Assert.That(shieldPrefab, Does.Contain("6cd12717bd9bde896dd0a4a174eb8512"));
        }

        [Test]
        [Category("Extended")]
        public void DeferredSerializedReferenceCleanup_IsDocumentedWithLegacyAssetRemoval()
        {
            var document = ReadRepoFile(GovernancePath);

            Assert.That(document, Does.Contain("EnemyUtilityWindupPresentationAuthoring." + UtilityRemovedField()));
            Assert.That(document, Does.Contain("EnemyFrontFaceShieldPresentationAuthoring." + ShieldActiveRemovedField()));
            Assert.That(document, Does.Contain("EnemyFrontFaceShieldPresentationAuthoring." + ShieldBlockRemovedField()));
            Assert.That(document, Does.Contain("old FrontFaceShield active/block prefab and material assets were removed after GUID reference scans confirmed zero external references"));
            Assert.That(document, Does.Contain("FrontFaceShieldActiveVfx"));
            Assert.That(document, Does.Contain("FrontFaceShieldBlockVfx"));
            Assert.That(document, Does.Contain("VFX_FrontFaceShield_Telegraph"));
            Assert.That(document, Does.Contain("M_FrontFaceShield_Telegraph.mat"));
        }

        [Test]
        [Category("Extended")]
        public void ProtectedVfxAssets_AreStillPresent()
        {
            AssertFileExists(CurrentFrontFaceShieldActivePrefabPath);
            AssertFileExists(CurrentFrontFaceShieldBlockPrefabPath);
            AssertFileExists(CurrentFrontFaceShieldActiveMaterialPath);
            AssertFileExists(CurrentFrontFaceShieldBlockMaterialPath);
            AssertFileExists(CurrentFrontFaceShieldActiveBindingPath);
            AssertFileExists(CurrentFrontFaceShieldBlockBindingPath);
            AssertFileExists(HostDefaultCueMapPath);
            Assert.That(File.Exists("Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/EnemyUtilityWindupTelegraphVfx.prefab"), Is.True);
            AssertFileExists(LegacyFrontFaceShieldTelegraphPrefabPath);
            AssertFileExists(LegacyFrontFaceShieldTelegraphMaterialPath);
        }

        [Test]
        [Category("Extended")]
        public void LegacyFrontFaceShieldActiveBlockAssets_AreRemoved()
        {
            AssertFileDoesNotExist(LegacyFrontFaceShieldActivePrefabPath);
            AssertFileDoesNotExist(LegacyFrontFaceShieldActivePrefabPath + ".meta");
            AssertFileDoesNotExist(LegacyFrontFaceShieldActiveMaterialPath);
            AssertFileDoesNotExist(LegacyFrontFaceShieldActiveMaterialPath + ".meta");
            AssertFileDoesNotExist(LegacyFrontFaceShieldBlockPrefabPath);
            AssertFileDoesNotExist(LegacyFrontFaceShieldBlockPrefabPath + ".meta");
            AssertFileDoesNotExist(LegacyFrontFaceShieldBlockMaterialPath);
            AssertFileDoesNotExist(LegacyFrontFaceShieldBlockMaterialPath + ".meta");
        }

        private static string ReadRepoFile(string path)
        {
            Assert.That(File.Exists(path), Is.True, $"Missing repo file: {path}");
            return File.ReadAllText(path);
        }

        private static void AssertFileExists(string path)
        {
            Assert.That(File.Exists(path), Is.True, $"Missing repo file: {path}");
        }

        private static void AssertFileDoesNotExist(string path)
        {
            Assert.That(File.Exists(path), Is.False, $"Unexpected legacy asset file: {path}");
        }

        private static string UtilityRemovedField()
        {
            return "summon" + "WindupWarningPrefab";
        }

        private static string ShieldActiveRemovedField()
        {
            return "active" + "LoopPrefab";
        }

        private static string ShieldBlockRemovedField()
        {
            return "block" + "BurstPrefab";
        }
    }
}
