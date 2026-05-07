using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private const string ActivityInspectorPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPresentationActivityInspector.cs";
        private const string OldTransientPresenterPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTransientEffectPresenter.cs";
        private const string EnemyDeathExitEffectPlanBuilderPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyDeathExitEffectPlanBuilder.cs";
        private const string TransientEffectTrackUtilityPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTransientEffectTrackUtility.cs";
        private const string FlipImpactTrackPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/FlipImpactTrack.cs";
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
            Assert.That(document, Does.Contain("## Active Transient Effect Count Cleanup"));
            Assert.That(document, Does.Contain("`ActiveTransientEffectCount` compatibility surface was removed"));
            Assert.That(document, Does.Contain("`GameplayVfxRuntimeDiagnostics`"));
        }

        [Test]
        [Category("Extended")]
        public void CleanedOldPaths_AreListed()
        {
            var document = ReadRepoFile(GovernancePath);

            Assert.That(document, Does.Contain("Player damage direct hit prefab fallback"));
            Assert.That(document, Does.Contain("BoxDestroy old entity exit transient track"));
            Assert.That(document, Does.Contain("ItemConsume old entity exit transient track"));
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

            Assert.That(document, Does.Contain("old fly-away track removed"));
            Assert.That(document, Does.Contain("old clone/fade track removed"));
            Assert.That(document, Does.Contain("Remaining old canonical presentation responsibilities"));
            Assert.That(document, Does.Contain("old impact break playback removed"));
            Assert.That(document, Does.Contain("old OutOfBounds fade track removed"));
            Assert.That(document, Does.Contain("`BoxFlipInteractionDriver` and `FlipImpactTrack` Stay branch"));
            Assert.That(document, Does.Not.Contain("windup warning path remains old canonical presentation"));
        }

        [Test]
        [Category("Extended")]
        public void OutOfBounds_ReservedMigrationPolicyIsDocumented()
        {
            var document = ReadRepoFile(GovernancePath);

            Assert.That(document, Does.Contain("## OutOfBounds Exit VFX Migration"));
            Assert.That(document, Does.Contain("dormant/reserved entity exit cause"));
            Assert.That(document, Does.Contain("no OutOfBounds gameplay producer is added"));
            Assert.That(document, Does.Contain("BoxVfxCue.OutOfBoundsExit"));
            Assert.That(document, Does.Contain("EnemyVfxCue.OutOfBoundsExit"));
            Assert.That(document, Does.Contain("old `GameplayExitPresentationController.PlayExitEffect` playback surface is removed"));
        }

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
            var flipImpactTrack = ReadRepoFile(FlipImpactTrackPath);
            var boxFlipInteractionDriver = ReadRepoFile(BoxFlipInteractionDriverPath);

            Assert.That(exitController, Does.Contain("public void ApplyEntityExitOwnership()"));
            Assert.That(deathPlanBuilder, Does.Contain("EnemyDeathExitEffectPlanBuilder"));
            Assert.That(trackUtility, Does.Contain("SafeDestroy"));
            Assert.That(flipImpactTrack, Does.Contain("FlipImpactPresentationDisposition.Stay"));
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
