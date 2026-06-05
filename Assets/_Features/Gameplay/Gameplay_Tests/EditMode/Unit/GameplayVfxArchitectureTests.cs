using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Authoring;
using Game.Feature.Gameplay.Vfx.Host;
using Game.Feature.Stages;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxArchitectureTests
    {
        private const string GovernancePath = "Docs/Architecture/Gameplay-VFX-Governance.md";
        private const string VfxRuntimePath = "Assets/_Features/Gameplay/Gameplay_Vfx/Runtime";
        private const string VfxPlanningPath = "Assets/_Features/Gameplay/Gameplay_Vfx/Runtime/GameplayVfxPlanning.cs";
        private const string VfxEnumsPath = "Assets/_Features/Gameplay/Gameplay_Vfx/Runtime/GameplayVfxEnums.cs";
        private const string VfxHostDiagnosticsPath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Diagnostics";
        private const string PresentationMotionTrackPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/PresentationMotionTrack.cs";
        private const string FlipImpactStayMotionCommandPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/FlipImpactStayMotionCommand.cs";
        private const string FlipImpactStayMotionCommandBuilderPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/FlipImpactStayMotionCommandBuilder.cs";
        private const string GameplayPresentationTrackStatePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPresentationTrackState.cs";
        private const string GameplayEntityPresentationApplierPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayEntityPresentationApplier.cs";
        private const string CoordinatorPath = "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs";
        private const string ExitControllerPath = "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayExitPresentationController.cs";
        private const string VfxProductionRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs";
        private const string PresentationMotionFollowingVfxControllerPath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/PresentationMotionFollowingVfxController.cs";
        private const string GameplayVfxGameObjectPoolPath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Pool/GameplayVfxGameObjectPool.cs";
        private const string GameplayVfxPlaybackHandlePath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Pool/GameplayVfxPlaybackHandle.cs";
        private const string ParameterizedMotionVfxCommandPath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/ParameterizedMotion/ParameterizedMotionVfxCommand.cs";
        private const string FlipImpactBurstVfxRequestPlannerPath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/FlipImpactBurstVfxRequestPlanner.cs";
        private const string FlipDestroySelfMotionVfxCommandBuilderPath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/FlipDestroySelfMotionVfxCommandBuilder.cs";
        private const string EnemyDeathExitEffectPlanBuilderPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyDeathExitEffectPlanBuilder.cs";
        private const string FrontFaceShieldPresenterPath = "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayFrontFaceShieldVfxPresenter.cs";
        private const string UtilityWindupAuthoringPath =
            "Assets/_Features/Gameplay/Gameplay_EntityView/Runtime/EnemyUtilityWindupPresentationAuthoring.cs";
        private const string FrontFaceShieldAuthoringPath =
            "Assets/_Features/Gameplay/Gameplay_EntityView/Runtime/EnemyFrontFaceShieldPresentationAuthoring.cs";
        private const string FlipImpactStayTrailBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/FlipImpactStayTrail_Binding.asset";
        private const string GlideWindTrailBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/GlideWindTrail_Binding.asset";
        private const string ChargeBoosterTrailBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/ChargeBoosterTrail_Binding.asset";

        [Test]
        [Category("Core")]
        public void GovernanceDocument_ExistsAndDeclaresBoundary()
        {
            var document = ReadRepoFile(GovernancePath);
            var readme = ReadRepoFile("Docs/Architecture/README.md");
            var requiredSections = new[]
            {
                "## Authoring Binding Gate",
                "## Composition Ownership Gate",
                "## Host Anchor Resolver Gate",
                "## VFX Planner Dependency Rule",
                "## Production Runtime Dependency Rule",
                "## Gameplay VFX Legacy Old Path Cleanup",
                "## Gameplay VFX Flag Rollout Policy",
                "## Visual Source Modes",
                "## Placeholder Prefab Policy",
                "## ADR: SourceCloneMotion Host Strategy",
                "## Test Naming Policy",
                "## Legacy Name",
            };

            Assert.That(readme, Does.Contain("Gameplay-VFX-Governance.md"));
            foreach (var section in requiredSections)
            {
                Assert.That(document, Does.Contain(section));
            }

            Assert.That(document, Does.Contain("presentation-only lane"));
            Assert.That(document, Does.Contain("family-specific planners"));
            Assert.That(document, Does.Contain("SurfaceCell"));
            Assert.That(document, Does.Contain("must not read WorldState"));
            Assert.That(document, Does.Contain("must not call WorldState.CreateSnapshot"));
            Assert.That(document, Does.Contain("Binding missing, anchor missing, and invalid policy are distinct failure modes"));
            Assert.That(document, Does.Contain("canonical Gameplay VFX runtime path"));
            Assert.That(document, Does.Contain("do not select an old path"));
            Assert.That(document, Does.Contain("### PrefabOnly"));
            Assert.That(document, Does.Contain("### SourceCloneMotion"));
            Assert.That(document, Does.Contain("### PrefabWithSourceClone"));
            Assert.That(document, Does.Contain("SourceViewCloneWithPrefabFallback"));
        }

        [Test]
        [Category("Core")]
        public void GameplayVfxGovernance_DocumentsVisualSourceModes()
        {
            var document = ReadRepoFile(GovernancePath);

            Assert.That(document, Does.Contain("## Visual Source Modes"));
            Assert.That(document, Does.Contain("### PrefabOnly"));
            Assert.That(document, Does.Contain("### SourceCloneMotion"));
            Assert.That(document, Does.Contain("### PrefabWithSourceClone"));
            Assert.That(document, Does.Contain("## Legacy Name"));
            Assert.That(document, Does.Contain("do not use `SourceViewCloneWithPrefabFallback` for `SourceCloneMotion` cues"));
            Assert.That(document, Does.Contain("do not mix `EnemyDeathMotion_Binding.asset` with `EnemyOutOfBoundsExit_Binding.asset`"));
        }

        [Test]
        [Category("Core")]
        public void GameplayVfxGovernance_DocumentsPlaceholderPrefabPolicy()
        {
            var document = ReadRepoFile(GovernancePath);

            Assert.That(document, Does.Contain("## Placeholder Prefab Policy"));
            Assert.That(document, Does.Contain("cue-specific placeholder prefabs must not be created for `SourceCloneMotion`"));
            Assert.That(document, Does.Contain("the prefab field may be null; null prefab is normal and must not warn or fail"));
            Assert.That(document, Does.Contain("`GameplayVfxCommonEmptyHost.prefab` is deletion-protected"));
            Assert.That(document, Does.Contain("`EnemyDeathMotionVfx.prefab` is a fallback visual and must not be deleted"));
            Assert.That(document, Does.Contain("particle/contact visual prefabs are actual visual prefabs, not placeholders"));
            Assert.That(document, Does.Contain("sample-only particle prefabs must not be kept in the production VFX tree"));
        }

        [Test]
        [Category("Core")]
        public void GameplayVfxGovernance_DocumentsSourceCloneMotionHostStrategy()
        {
            var document = ReadRepoFile(GovernancePath);

            Assert.That(document, Does.Contain("## ADR: SourceCloneMotion Host Strategy"));
            Assert.That(document, Does.Contain("Keep `GameplayVfxCommonEmptyHost.prefab` as the default and required common host for `SourceCloneMotion`"));
            Assert.That(document, Does.Contain("Do not implement a runtime-created host path in P2-2"));
            Assert.That(document, Does.Contain("Runtime-created hosts remain a documented future extension only"));
            Assert.That(document, Does.Contain("prefab instance id keys pooled host storage"));
            Assert.That(document, Does.Contain("max concurrency is enforced by cue id"));
            Assert.That(document, Does.Contain("Sharing one common host prefab therefore has no current pool-key issue"));
            Assert.That(document, Does.Contain("Missing common host setup reports `CommonHostUnavailable`"));
            Assert.That(document, Does.Contain("no `RuntimeHostAllowed` policy is introduced in P2-2"));
            Assert.That(document, Does.Contain("Future Entry Criteria"));
        }

        [Test]
        [Category("Core")]
        public void GameplayVfxTestNaming_DocumentsRuntimeBehaviorNamingPolicy()
        {
            var document = ReadRepoFile(GovernancePath);

            Assert.That(document, Does.Contain("## Test Naming Policy"));
            Assert.That(document, Does.Contain("VFX tests should describe the runtime contract they guard"));
            Assert.That(document, Does.Contain("`SourceCloneMotion`"));
            Assert.That(document, Does.Contain("`PrefabWithSourceClone`"));
            Assert.That(document, Does.Contain("`PrefabOnly`"));
            Assert.That(document, Does.Contain("`CommonHost`"));
            Assert.That(document, Does.Contain("`MissingSourceView`"));
            Assert.That(document, Does.Contain("`MissingPrefab`"));
            Assert.That(document, Does.Contain("`FallbackPrefab`"));
            Assert.That(document, Does.Contain("`OriginalViewImmutability`"));
            Assert.That(document, Does.Contain("`HostRelease`"));
            Assert.That(document, Does.Contain("Avoid for new tests"));
            Assert.That(document, Does.Contain("tests that explicitly verify obsolete compatibility aliases"));
            Assert.That(document, Does.Contain("tests that verify historical cleanup rules"));
            Assert.That(document, Does.Contain("tests that document intentional non-regression against a past bug"));
        }

        [Test]
        [Category("Extended")]
        public void PresentationMotionTrack_IsHostOriginalViewMotion_NotGameplayVfx()
        {
            var source = ReadRepoFile(PresentationMotionTrackPath);
            var trackState = ReadRepoFile(GameplayPresentationTrackStatePath);
            var applier = ReadRepoFile(GameplayEntityPresentationApplierPath);

            Assert.That(source, Does.Contain("internal sealed class PresentationMotionTrack"));
            Assert.That(source, Does.Contain("internal readonly struct PresentationMotionCommand"));
            Assert.That(source, Does.Contain("internal readonly struct PresentationMotionSample"));
            Assert.That(trackState, Does.Contain("Dictionary<int, PresentationMotionTrack> _originalViewMotionTracks"));
            Assert.That(applier, Does.Contain("originalViewMotionTrack.Sample()"));
            Assert.That(applier, Does.Contain("motionVisualScaleMultiplier = sample.VisualScaleMultiplier"));
            Assert.That(applier, Does.Contain("localPose = sample.LocalPose"));
            Assert.That(source, Does.Not.Contain("Game.Feature.Gameplay.Vfx"));
            Assert.That(source, Does.Not.Contain("GameplayVfx"));
            Assert.That(source, Does.Not.Contain("BoxVfxCue"));
            Assert.That(source, Does.Not.Contain("EnemyVfxCue"));
            Assert.That(source, Does.Not.Contain("ParameterizedMotionVfxCommand"));
            Assert.That(source, Does.Not.Contain("prefab"));
            Assert.That(source, Does.Not.Contain("material"));
            Assert.That(source, Does.Not.Contain("binding"));
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactStayTrail_IsAttachedFollowerNotMotionCommand()
        {
            var vfxEnums = ReadRepoFile(VfxEnumsPath);
            var vfxPlanning = ReadRepoFile(VfxPlanningPath);
            var productionRuntime = ReadRepoFile(VfxProductionRuntimePath);

            Assert.That(vfxEnums, Does.Contain("FlipImpactStayTrail"));
            Assert.That(vfxEnums, Does.Not.Contain("StayMotion"));
            Assert.That(vfxPlanning, Does.Not.Contain("FlipImpactStayMotion"));
            Assert.That(productionRuntime, Does.Not.Contain("FlipImpactStayMotion"));
            Assert.That(productionRuntime, Does.Contain("PresentationMotionFollowingVfxController"));
        }

        [Test]
        [Category("Extended")]
        public void AttachedFollowers_UseControllerManagedPoolLifetime()
        {
            var controller = ReadRepoFile(PresentationMotionFollowingVfxControllerPath);
            var pool = ReadRepoFile(GameplayVfxGameObjectPoolPath);
            var handle = ReadRepoFile(GameplayVfxPlaybackHandlePath);

            Assert.That(controller, Does.Contain("controllerManagedLifetime: true"));
            Assert.That(pool, Does.Contain("bool controllerManagedLifetime = false"));
            Assert.That(pool, Does.Contain("handle.IsLifetimeControllerManaged"));
            Assert.That(handle, Does.Contain("IsLifetimeControllerManaged"));
        }

        [Test]
        [Category("Extended")]
        public void AttachedFollowerBindings_DoNotUseHugeLifetimeWorkaround()
        {
            var bindingPaths = new[]
            {
                GlideWindTrailBindingPath,
                ChargeBoosterTrailBindingPath,
            };

            Assert.That(File.Exists(FlipImpactStayTrailBindingPath), Is.False);

            foreach (var bindingPath in bindingPaths)
            {
                var binding = ReadRepoFile(bindingPath);

                Assert.That(binding, Does.Contain("defaultLifetimeSeconds: 0"), bindingPath);
                Assert.That(binding, Does.Not.Contain("defaultLifetimeSeconds: 999"), bindingPath);
                Assert.That(binding, Does.Not.Contain("defaultLifetimeSeconds: 9999"), bindingPath);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayVfxRuntime_DoesNotOwnOriginalViewMotion()
        {
            var productionRuntime = ReadRepoFile(VfxProductionRuntimePath);

            Assert.That(productionRuntime, Does.Not.Contain("PresentationMotionTrack"));
            Assert.That(productionRuntime, Does.Not.Contain("PresentationMotionSample"));
            Assert.That(productionRuntime, Does.Not.Contain("OriginalViewMotionTracks"));
            Assert.That(productionRuntime, Does.Not.Contain("CompletedPresentationMotionKeys"));
            Assert.That(productionRuntime, Does.Not.Contain("HasSuppressingOriginalViewMotion"));
            Assert.That(productionRuntime, Does.Not.Contain("BoxFlipInteractionDriver"));
        }

        [Test]
        [Category("Extended")]
        public void ParameterizedMotionVfxCommand_NotUsedForStayOriginalViewMotion()
        {
            var parameterizedMotion = ReadRepoFile(ParameterizedMotionVfxCommandPath);
            var stayMotionSource = ReadRepoFile(FlipImpactStayMotionCommandPath) + "\n" +
                                   ReadRepoFile(FlipImpactStayMotionCommandBuilderPath) + "\n" +
                                   ReadRepoFile(PresentationMotionTrackPath);

            Assert.That(parameterizedMotion, Does.Not.Contain("FlipImpactStay"));
            Assert.That(parameterizedMotion, Does.Not.Contain("PresentationMotionTrack"));
            Assert.That(stayMotionSource, Does.Not.Contain("ParameterizedMotionVfxCommand"));
            Assert.That(stayMotionSource, Does.Not.Contain("GameplayVfxProductionRuntime"));
        }

        [Test]
        [Category("Extended")]
        public void FlipImpactBurst_RemainsContactFeedbackOnly()
        {
            var planner = ReadRepoFile(FlipImpactBurstVfxRequestPlannerPath);

            Assert.That(planner, Does.Contain("FlipFloorImpactSignals"));
            Assert.That(planner, Does.Contain("BoxVfxCue.FlipImpactBurst"));
            Assert.That(planner, Does.Contain("VfxAnchor.ForCell"));
            Assert.That(planner, Does.Contain("signal.ContactCell"));
            Assert.That(planner, Does.Not.Contain("PresentationMotionTrack"));
            Assert.That(planner, Does.Not.Contain("OriginalViewMotionTracks"));
            Assert.That(planner, Does.Not.Contain("Suppress"));
        }

        [Test]
        [Category("Extended")]
        public void FlipDestroySelfMotion_RejectsStayDisposition()
        {
            var builder = ReadRepoFile(FlipDestroySelfMotionVfxCommandBuilderPath);

            Assert.That(builder, Does.Contain("signal.Disposition != FlipImpactPresentationDisposition.DestroySelf"));
            Assert.That(builder, Does.Not.Contain("FlipImpactPresentationDisposition.Stay"));
        }

        [Test]
        [Category("Extended")]
        public void LegacyFinalization_RemovesOldPlaybackButKeepsCleanupOwners()
        {
            var coordinator = ReadRepoFile(CoordinatorPath);
            var exitController = ReadRepoFile(ExitControllerPath);
            var deathPlanBuilder = ReadRepoFile(EnemyDeathExitEffectPlanBuilderPath);
            var frontFaceShieldPresenter = ReadRepoFile(FrontFaceShieldPresenterPath);
            var utilityWindupAuthoring = ReadRepoFile(UtilityWindupAuthoringPath);
            var frontFaceShieldAuthoring = ReadRepoFile(FrontFaceShieldAuthoringPath);

            Assert.That(coordinator, Does.Not.Contain("TraceStep(\"PlayPlayerHitEffects\")"));
            Assert.That(coordinator, Does.Not.Contain("TraceStep(\"PlayFrontFaceShieldBlockBursts\")"));
            Assert.That(coordinator, Does.Contain("_exitPresentationController.ApplyEntityExitOwnership();"));
            Assert.That(coordinator, Does.Not.Contain("PlayPlayerHitEffects(TickResult"));

            Assert.That(exitController, Does.Contain("public void ApplyEntityExitOwnership()"));
            Assert.That(exitController, Does.Contain("_entitiesWithDestroySelfFlipImpact"));
            Assert.That(exitController, Does.Not.Contain("FlipImpactInstanceKey"));
            Assert.That(exitController, Does.Not.Contain("PresentationMotionInstanceKey"));
            Assert.That(exitController, Does.Not.Contain("PlayExitEffect"));
            Assert.That(exitController, Does.Not.Contain("PlayFlipImpactDestroyEffect"));
            Assert.That(exitController, Does.Not.Contain("suppressLegacyEnemyDeathEffects"));

            Assert.That(File.Exists(GetAbsolutePath("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTransientEffectPresenter.cs")), Is.False);
            Assert.That(deathPlanBuilder, Does.Contain("EnemyDeathExitEffectPlanBuilder"));
            Assert.That(deathPlanBuilder, Does.Not.Contain("ImpactBreakEffectTrack"));
            Assert.That(deathPlanBuilder, Does.Not.Contain("EntityExitEffectTrack"));

            Assert.That(frontFaceShieldPresenter, Does.Contain("RefreshWindupWarnings"));
            Assert.That(frontFaceShieldPresenter, Does.Contain("RefreshActiveSources"));
            Assert.That(frontFaceShieldPresenter, Does.Not.Contain("PlayBlockBursts("));
            Assert.That(frontFaceShieldPresenter, Does.Not.Contain("Active" + "LoopPrefab"));
            Assert.That(frontFaceShieldPresenter, Does.Not.Contain("Block" + "BurstPrefab"));
            Assert.That(utilityWindupAuthoring, Does.Not.Contain("summon" + "WindupWarningPrefab"));
            Assert.That(frontFaceShieldAuthoring, Does.Not.Contain("active" + "LoopPrefab"));
            Assert.That(frontFaceShieldAuthoring, Does.Not.Contain("block" + "BurstPrefab"));
            Assert.That(frontFaceShieldAuthoring, Does.Contain("telegraphPrefab"));
        }

        [Test]
        [Category("Extended")]
        public void VfxAssembly_ReferencesGameplayOnly()
        {
            var references = typeof(GameplayVfxCueId).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Contain("Game.Feature.Gameplay"));
            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay.Host"));
            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay.Audio"));
            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay.ActionAudio"));
            Assert.That(references, Does.Not.Contain("Game.Feature.Stages"));
        }

        [Test]
        [Category("Extended")]
        public void VfxAuthoringAssembly_ReferencesVfxButNotProductionAssemblies()
        {
            var references = typeof(VfxBindingDefinitionAsset).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Contain("Game.Feature.Gameplay.Vfx"));
            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay.Host"));
            Assert.That(references, Does.Not.Contain("Game.Feature.Stages"));
            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay.Loop"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayAssembly_DoesNotReferenceVfxAuthoring()
        {
            var references = typeof(WorldState).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay.Vfx.Authoring"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPresentationAssembly_ReferencesGameplayAndVfxAuthoringButNotStagesOrHost()
        {
            var references = typeof(EnemyPresentationCatalog).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Contain("Game.Feature.Gameplay"));
            Assert.That(references, Does.Contain("Game.Feature.Gameplay.Vfx"));
            Assert.That(references, Does.Contain("Game.Feature.Gameplay.Vfx.Authoring"));
            Assert.That(references, Does.Not.Contain("Game.Feature.Stages"));
            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay.Host"));
        }

        [Test]
        [Category("Extended")]
        public void StagesAndHostReferenceEnemyPresentationAssembly()
        {
            Assert.That(
                typeof(StagePresentationDefinition).Assembly.GetReferencedAssemblies().Select(reference => reference.Name),
                Does.Contain("Game.Feature.Gameplay.EnemyPresentation"));
            Assert.That(
                typeof(GameplaySceneHostConfiguration).Assembly.GetReferencedAssemblies().Select(reference => reference.Name),
                Does.Contain("Game.Feature.Gameplay.EnemyPresentation"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPresentationCatalogSchema_IncludesOptionalVfxProfileAsset()
        {
            Assert.That(
                GetDeclaredFieldNames(typeof(EnemyPresentationCatalogEntry)),
                Is.EqualTo(new[] { "PresentationId", "ViewPrefab", "VfxProfileAsset" }));
            Assert.That(
                GetDeclaredFieldNames(typeof(EnemyPresentationBinding)),
                Is.EqualTo(new[] { "EntityId", "PresentationId" }));

            AssertDoesNotExposeVfxProfileOverride(typeof(EnemyPresentationBinding));
        }

        [Test]
        [Category("Full")]
        public void CorePublicSurface_DoesNotExposeHostPrefabOrAuthorityTypes()
        {
            var forbiddenTypes = new HashSet<string>
            {
                "UnityEngine.GameObject",
                "UnityEngine.MonoBehaviour",
                "UnityEngine.ParticleSystem",
                typeof(WorldState).FullName,
                typeof(WorldSnapshot).FullName,
                typeof(TickPipeline).FullName,
            };

            var publicTypes = typeof(GameplayVfxCueId).Assembly
                .GetTypes()
                .Where(type => type.IsPublic || type.IsNestedPublic)
                .ToArray();

            foreach (var type in publicTypes)
            {
                var members = type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                    .Select(field => (MemberName: field.Name, MemberType: field.FieldType))
                    .Concat(type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .Select(property => (MemberName: property.Name, MemberType: property.PropertyType)));

                foreach (var member in members)
                {
                    Assert.That(
                        ContainsForbiddenType(member.MemberType, forbiddenTypes),
                        Is.False,
                        $"{type.FullName}.{member.MemberName} exposes forbidden type {member.MemberType.FullName}.");
                }
            }
        }

        [Test]
        [Category("Extended")]
        public void CoreSource_DoesNotReferenceSnapshotOrProductionRuntimeTypes()
        {
            var source = ReadRuntimeSources();

            Assert.That(source, Does.Not.Contain("CreateSnapshot"));
            Assert.That(source, Does.Not.Contain("WorldState"));
            Assert.That(source, Does.Not.Contain("WorldSnapshot"));
            Assert.That(source, Does.Not.Contain("TickPipeline"));
            Assert.That(source, Does.Not.Contain("GameObject"));
            Assert.That(source, Does.Not.Contain("Renderer"));
            Assert.That(source, Does.Not.Contain("MonoBehaviour"));
            Assert.That(source, Does.Not.Contain("ParticleSystem"));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeDiagnostics_DoNotUseDedicatedLifetimeTraceAdapter()
        {
            var document = ReadRepoFile(GovernancePath);

            Assert.That(Directory.Exists(GetAbsolutePath(VfxHostDiagnosticsPath)), Is.False);
            Assert.That(document, Does.Contain("Dedicated Gameplay VFX lifetime trace adapters are removed."));
            Assert.That(document, Does.Not.Contain("`Gameplay_VfxHost/Runtime/Diagnostics`"));
            Assert.That(document, Does.Not.Contain("GameplayVfxLifetimeUnityTrace"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayVfxLifetimeTrace_IsRemovedFromCoreAndHostRuntime()
        {
            var vfxSource = ReadRuntimeSources();
            var hostSource = ReadSourceDirectory("Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime");

            Assert.That(File.Exists(GetAbsolutePath("Assets/_Features/Gameplay/Gameplay_Vfx/Runtime/GameplayVfxLifetimeTrace.cs")), Is.False);
            Assert.That(
                File.Exists(GetAbsolutePath("Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Diagnostics/GameplayVfxLifetimeUnityTrace.cs")),
                Is.False);
            Assert.That(vfxSource, Does.Not.Contain("GameplayVfxLifetimeTrace"));
            Assert.That(hostSource, Does.Not.Contain("GameplayVfxLifetimeUnityTrace"));
            Assert.That(hostSource, Does.Not.Contain("TraceEntrance"));
        }

        [Test]
        [Category("Extended")]
        public void CloneProvider_LivesOnlyOutsideVfxCore()
        {
            var coreSource = ReadRuntimeSources();

            Assert.That(coreSource, Does.Not.Contain("IGameplayVfxCloneSourceProvider"));
            Assert.That(coreSource, Does.Not.Contain("GameplayVfxCloneSource"));
            Assert.That(typeof(IGameplayVfxCloneSourceProvider).Assembly.GetName().Name, Is.EqualTo("Game.Feature.Gameplay.Vfx.Host"));
        }

        [Test]
        [Category("Extended")]
        public void ParameterizedMotion_HasNoBoxSlideUnitOrProjectileAdaptersYet()
        {
            var productionSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs");

            Assert.That(productionSource, Does.Contain("FlipDestroySelfMotion"));
            Assert.That(productionSource, Does.Not.Contain("SlideStartDust"));
            Assert.That(productionSource, Does.Not.Contain("ProjectileVfxCue.Trail"));
            Assert.That(productionSource, Does.Not.Contain("Unit movement"));
        }

        [Test]
        [Category("Extended")]
        public void Architecture_RequestSourceIdDoesNotOpenAuthorityReference()
        {
            Assert.That(typeof(GameplayVfxRequest).GetProperty(nameof(GameplayVfxRequest.SourceEntityId)), Is.Not.Null);

            var source = ReadRuntimeSources();

            AssertForbiddenAuthorityTokensAbsent(source);
        }

        [Test]
        [Category("Extended")]
        public void Governance_NoStageBindingOverrideYet()
        {
            AssertDoesNotExposeVfxProfileOverride(typeof(StagePresentationDefinition));
            AssertDoesNotExposeVfxProfileOverride(typeof(EnemyPresentationBinding));

            var document = ReadRepoFile(GovernancePath);
            Assert.That(document, Does.Contain("EnemyPresentationCatalogEntry.VfxProfileAsset"));
            Assert.That(document, Does.Contain("source entity presentation profile"));
            Assert.That(document, Does.Contain("StagePresentationDefinition binding override"));
            Assert.That(document, Does.Contain("future stage-specific override"));
        }

        [Test]
        [Category("Extended")]
        public void PlannerSource_DoesNotReferenceAuthorityRuntimeTypes()
        {
            var source = ReadRepoFile(VfxPlanningPath);

            Assert.That(source, Does.Contain(nameof(TickPresentationData)));
            AssertForbiddenAuthorityTokensAbsent(source);
        }

        [Test]
        [Category("Extended")]
        public void Coordinator_DoesNotReferenceGameplayVfxFoundation()
        {
            var source = ReadRepoFile(CoordinatorPath);

            Assert.That(source, Does.Not.Contain("GameplayVfx"));
            Assert.That(source, Does.Not.Contain("Game.Feature.Gameplay.Vfx"));
        }

        [Test]
        [Category("Extended")]
        public void PlanningContext_ExposesOnlyPresentationSeamInputs()
        {
            var properties = typeof(GameplayVfxPlanningContext)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.PropertyType.Name)
                .ToArray();

            Assert.That(properties, Does.Contain("Int32"));
            Assert.That(properties, Does.Contain(nameof(TickPresentationData)));
            Assert.That(properties, Does.Contain(nameof(CubeTopologyState)));
            Assert.That(properties, Does.Contain(nameof(GameplayTimingProfile)));
            Assert.That(properties, Does.Contain("IReadOnlyList`1"));
            Assert.That(properties, Does.Not.Contain("WorldState"));
            Assert.That(properties, Does.Not.Contain("WorldSnapshot"));
            Assert.That(properties, Does.Not.Contain("TickPipeline"));
        }

        private static void AssertForbiddenAuthorityTokensAbsent(string source)
        {
            var forbiddenTokens = new[]
            {
                "WorldState",
                "WorldSnapshot",
                "TickPipeline",
                "ProjectedWorld",
                "FinalizationBatch",
                "DeterminismHashBuilder",
                "CreateSnapshot",
            };

            foreach (var token in forbiddenTokens)
            {
                Assert.That(source, Does.Not.Contain(token), token);
            }
        }

        private static void AssertDoesNotExposeVfxProfileOverride(Type type)
        {
            var memberNames = type.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(member => member.DeclaringType == type)
                .Select(member => member.Name)
                .ToArray();

            foreach (var memberName in memberNames)
            {
                Assert.That(memberName, Does.Not.Contain("VfxProfile"), $"{type.FullName}.{memberName}");
                Assert.That(memberName, Does.Not.Contain("VFXProfile"), $"{type.FullName}.{memberName}");
            }

            var exposedTypes = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(field => field.FieldType)
                .Concat(type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Select(property => property.PropertyType))
                .Select(memberType => memberType.Name)
                .ToArray();

            foreach (var exposedType in exposedTypes)
            {
                Assert.That(exposedType, Does.Not.Contain("VfxProfile"), $"{type.FullName} exposes {exposedType}");
                Assert.That(exposedType, Does.Not.Contain("VFXProfile"), $"{type.FullName} exposes {exposedType}");
            }
        }

        private static string[] GetDeclaredFieldNames(Type type)
        {
            return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Select(field => field.Name)
                .ToArray();
        }

        private static bool ContainsForbiddenType(Type type, HashSet<string> forbiddenTypes)
        {
            if (forbiddenTypes.Contains(type.FullName))
            {
                return true;
            }

            if (!type.IsGenericType)
            {
                return false;
            }

            return type.GetGenericArguments().Any(argument => ContainsForbiddenType(argument, forbiddenTypes));
        }

        private static string ReadRuntimeSources()
        {
            return ReadSourceDirectory(VfxRuntimePath);
        }

        private static string ReadSourceDirectory(string relativePath)
        {
            var absoluteDirectory = GetAbsolutePath(relativePath);
            return string.Join(
                "\n",
                Directory.GetFiles(absoluteDirectory, "*.cs", SearchOption.AllDirectories)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(File.ReadAllText));
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(GetAbsolutePath(relativePath)).Replace("\r\n", "\n");
        }

        private static string GetAbsolutePath(string relativePath)
        {
            return Path.GetFullPath(relativePath);
        }
    }
}
