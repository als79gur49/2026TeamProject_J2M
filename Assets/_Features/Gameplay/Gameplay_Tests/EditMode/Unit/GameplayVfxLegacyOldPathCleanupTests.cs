using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Host;
using Game.Feature.Stages;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxLegacyOldPathCleanupTests
    {
        private const string CoordinatorPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs";
        private const string ExitControllerPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayExitPresentationController.cs";
        private const string ActivityInspectorPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPresentationActivityInspector.cs";
        private const string OldTransientPresenterPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTransientEffectPresenter.cs";
        private const string EnemyDeathExitEffectPlanBuilderPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyDeathExitEffectPlanBuilder.cs";
        private const string TransientEffectTrackUtilityPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTransientEffectTrackUtility.cs";
        private const string PresentationMotionTrackPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/PresentationMotionTrack.cs";
        private const string BoxFlipInteractionDriverPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxFlipInteractionDriver.cs";
        private const string RuntimePath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs";
        private const string UtilityWindupAuthoringPath =
            "Assets/_Features/Gameplay/Gameplay_EntityView/Runtime/EnemyUtilityWindupPresentationAuthoring.cs";
        private const string FrontFaceShieldAuthoringPath =
            "Assets/_Features/Gameplay/Gameplay_EntityView/Runtime/EnemyFrontFaceShieldPresentationAuthoring.cs";
        private const string FrontFaceShieldPresenterPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayFrontFaceShieldVfxPresenter.cs";
        private const string FrontFaceShieldPrefabPath =
            StageContentPaths.SharedEnemyPresentationRoot + "/Prefabs/EnemyView_FrontFaceShield.prefab";
        private const string CurrentFrontFaceShieldActivePrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/FrontFaceShieldActiveVfx.prefab";
        private const string CurrentFrontFaceShieldBlockPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/FrontFaceShieldBlockVfx.prefab";
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
            StageContentPaths.SharedVfxPresentationRoot + "/FrontFaceShield/VFX_FrontFaceShield_Telegraph.prefab";
        private const string LegacyFrontFaceShieldTelegraphMaterialPath =
            StageContentPaths.SharedVfxPresentationRoot + "/FrontFaceShield/M_FrontFaceShield_Telegraph.mat";

        [Test]
        [Category("Extended")]
        public void OutOfBounds_IsConsumedByReservedGameplayVfxPlannerOnly()
        {
            var enums = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Vfx/Runtime/GameplayVfxEnums.cs");
            var planning = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Vfx/Runtime/GameplayVfxPlanning.cs");
            var boxShrinkBuilder = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/BoxDestroyShrinkVfxCommandBuilder.cs");
            var enemyDeathBuilder = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/EnemyDeathMotionVfxCommandBuilder.cs");

            Assert.That(enums, Does.Contain("OutOfBoundsExit"));
            Assert.That(planning, Does.Contain("TickEntityExitCause.OutOfBounds"));
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
        public void FinalizedHighRiskOldPaths_CodeHasNoFlagOffFallbacks()
        {
            var exitController = ReadRepoFile(ExitControllerPath);
            var coordinator = ReadRepoFile(CoordinatorPath);
            var runtime = ReadRepoFile(RuntimePath);

            Assert.That(exitController, Does.Not.Contain("PlayFlipImpactDestroyEffect"));
            Assert.That(exitController, Does.Not.Contain("suppressLegacyFlipDestroySelfEffects"));
            Assert.That(exitController, Does.Not.Contain("suppress" + "Legacy" + "EnemyDeathEffects"));
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
        public void ActiveTransientEffectCount_PublicSurfaceRemoved()
        {
            Assert.That(
                typeof(GameplayTickPresentationCoordinator).GetProperty(
                    "ActiveTransientEffectCount",
                    BindingFlags.Instance | BindingFlags.Public),
                Is.Null);
            Assert.That(
                typeof(GameplayTickViewPresenter).GetProperty(
                    "ActiveTransientEffectCount",
                    BindingFlags.Instance | BindingFlags.Public),
                Is.Null);

            var hostRuntimeSource = ReadCombinedSource("Assets/_Features/Gameplay/Gameplay_Host/Runtime");
            var vfxHostRuntimeSource = ReadCombinedSource("Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime");

            Assert.That(hostRuntimeSource, Does.Not.Contain("ActiveTransientEffectCount"));
            Assert.That(vfxHostRuntimeSource, Does.Not.Contain("ActiveTransientEffectCount"));
        }

        [Test]
        [Category("Extended")]
        public void RemovedOldTransientPlaybackApis_StayOutOfProductionSource()
        {
            var hostRuntimeSource = ReadCombinedSource("Assets/_Features/Gameplay/Gameplay_Host/Runtime");
            var vfxHostRuntimeSource = ReadCombinedSource("Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime");
            var productionSource = hostRuntimeSource + "\n" + vfxHostRuntimeSource;

            Assert.That(productionSource, Does.Not.Contain("GameplayTransientEffectPresenter"));
            Assert.That(productionSource, Does.Not.Contain("PlayEntityExitEffects"));
            Assert.That(productionSource, Does.Not.Contain("PlayImpactBreakEffect"));
            Assert.That(productionSource, Does.Not.Contain("EntityExitEffectTrack"));
            Assert.That(productionSource, Does.Not.Contain("ImpactBreakEffectTrack"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayVfxDiagnostics_DoesNotReuseOldTransientCount()
        {
            var runtime = ReadRepoFile(RuntimePath);

            Assert.That(runtime, Does.Contain("ActiveVfxInstanceCount"));
            Assert.That(runtime, Does.Not.Contain("ActiveTransientEffectCount"));
        }

        [Test]
        [Category("Extended")]
        public void OldTransientActivity_IsNotPresentationActivityInput()
        {
            var inspector = ReadRepoFile(ActivityInspectorPath);

            Assert.That(inspector, Does.Not.Contain("ActiveTransientEffectCount"));
            Assert.That(inspector, Does.Not.Contain("TransientEffectCount"));
            Assert.That(inspector, Does.Not.Contain("ActiveTransient"));
            Assert.That(inspector, Does.Contain("HasActiveEntityPresentationClips"));
        }

        [Test]
        [Category("Extended")]
        public void RetainedCleanupHelpers_AreStillPresent()
        {
            var exitController = ReadRepoFile(ExitControllerPath);
            var deathPlanBuilder = ReadRepoFile(EnemyDeathExitEffectPlanBuilderPath);
            var trackUtility = ReadRepoFile(TransientEffectTrackUtilityPath);
            var presentationMotionTrack = ReadRepoFile(PresentationMotionTrackPath);
            var boxFlipInteractionDriver = ReadRepoFile(BoxFlipInteractionDriverPath);

            Assert.That(exitController, Does.Contain("public void ApplyEntityExitOwnership()"));
            Assert.That(deathPlanBuilder, Does.Contain("EnemyDeathExitEffectPlanBuilder"));
            Assert.That(trackUtility, Does.Contain("SafeDestroy"));
            Assert.That(presentationMotionTrack, Does.Contain("PresentationMotionTrack"));
            Assert.That(presentationMotionTrack, Does.Contain("CreateFlipImpactStay"));
            Assert.That(boxFlipInteractionDriver, Does.Contain("BoxFlipInteractionDriver"));
        }

        [Test]
        [Category("Extended")]
        public void OldTransientTracks_AreRemoved()
        {
            AssertFileDoesNotExist(OldTransientPresenterPath);
            AssertFileDoesNotExist(OldTransientPresenterPath + ".meta");

            var exitController = ReadRepoFile(ExitControllerPath);
            Assert.That(exitController, Does.Not.Contain("EntityExitEffectTrack"));
            Assert.That(exitController, Does.Not.Contain("ImpactBreakEffectTrack"));
            Assert.That(exitController, Does.Not.Contain("GameplayPresentationEffectFactory"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayTransientEffectPresenter_DoesNotExposeOldPlaybackMethods()
        {
            AssertFileDoesNotExist(OldTransientPresenterPath);

            var hostRuntimeSource = ReadCombinedSource("Assets/_Features/Gameplay/Gameplay_Host/Runtime");
            Assert.That(hostRuntimeSource, Does.Not.Contain("PlayExitEffect"));
            Assert.That(hostRuntimeSource, Does.Not.Contain("PlayImpactBreakEffect"));
            Assert.That(hostRuntimeSource, Does.Not.Contain("CreateExitEffect"));
            Assert.That(hostRuntimeSource, Does.Not.Contain("CreateImpactBreakEffect"));
            Assert.That(hostRuntimeSource, Does.Not.Contain("EntityExitEffectTrack"));
            Assert.That(hostRuntimeSource, Does.Not.Contain("ImpactBreakEffectTrack"));
            Assert.That(hostRuntimeSource, Does.Not.Contain("GameplayPresentationEffectFactory"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyDeathExitEffectPlanBuilder_RetainedForDeathMotion()
        {
            var builder = ReadRepoFile(EnemyDeathExitEffectPlanBuilderPath);
            var deathMotionBuilder = ReadRepoFile(
                "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/EnemyDeathMotionVfxCommandBuilder.cs");

            Assert.That(builder, Does.Contain("EnemyDeathExitEffectPlanBuilder"));
            Assert.That(deathMotionBuilder, Does.Contain("EnemyDeathExitEffectPlanBuilder.Build"));
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
        public void ProtectedVfxAssets_KeepSharedAssetsAndRemoveNonParticleAuthoring()
        {
            AssertFileDoesNotExist(CurrentFrontFaceShieldActivePrefabPath);
            AssertFileDoesNotExist(CurrentFrontFaceShieldBlockPrefabPath);
            AssertFileDoesNotExist(CurrentFrontFaceShieldActiveBindingPath);
            AssertFileDoesNotExist(CurrentFrontFaceShieldBlockBindingPath);
            AssertFileExists(HostDefaultCueMapPath);
            Assert.That(File.Exists("Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/EnemyUtilityWindupTelegraphVfx.prefab"), Is.False);
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

        private static string ReadCombinedSource(string relativeDirectory)
        {
            return string.Join(
                "\n",
                Directory.GetFiles(relativeDirectory, "*.cs", SearchOption.AllDirectories)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(File.ReadAllText));
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
