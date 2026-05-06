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
        private const string CoordinatorPath = "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs";

        [Test]
        [Category("Extended")]
        public void GovernanceDocument_ExistsAndDeclaresBoundary()
        {
            var document = ReadRepoFile(GovernancePath);
            var readme = ReadRepoFile("Docs/Architecture/README.md");

            Assert.That(readme, Does.Contain("Gameplay-VFX-Governance.md"));
            Assert.That(document, Does.Contain("presentation-only lane"));
            Assert.That(document, Does.Contain("not tile-only"));
            Assert.That(document, Does.Contain("must not read WorldState"));
            Assert.That(document, Does.Contain("must not call WorldState.CreateSnapshot"));
            Assert.That(document, Does.Contain("family-specific planners"));
            Assert.That(document, Does.Contain("existing presenter migration is a future slice"));
            Assert.That(document, Does.Contain("StopEmitting"));
            Assert.That(document, Does.Contain("TailPlaying"));
            Assert.That(document, Does.Contain("ReleasedToPool"));
            Assert.That(document, Does.Contain("persistent desired state"));
            Assert.That(document, Does.Contain("SurfaceCell"));
            Assert.That(document, Does.Contain("GameplayTransientEffectPresenter"));
            Assert.That(document, Does.Contain("GameplayExitPresentationController"));
            Assert.That(document, Does.Contain("GameplayFrontFaceShieldVfxPresenter"));
            Assert.That(document, Does.Contain("GameplayUtilityWindupVfxPresenter"));
            Assert.That(document, Does.Contain("BoxFlipInteractionDriver"));
            Assert.That(document, Does.Contain("FlipImpactTrack"));
            Assert.That(document, Does.Contain("FlipImpact MotionTrack Anchor Gate"));
            Assert.That(document, Does.Contain("FlipImpactContactVfxAnchor"));
            Assert.That(document, Does.Contain("VfxAnchorKind.MotionTrack` remains unsupported"));
            Assert.That(document, Does.Contain("must not consume the same fact concurrently"));
            Assert.That(document, Does.Contain("GameplayVfxRequest` is a semantic request"));
            Assert.That(document, Does.Contain("does not own missing-anchor policy"));
            Assert.That(document, Does.Contain("VfxBindingRuntimePolicy` owns"));
            Assert.That(document, Does.Contain("Binding missing, anchor missing, and invalid policy are distinct failure modes"));
            Assert.That(document, Does.Contain("Authoring Binding Gate"));
            Assert.That(document, Does.Contain("VfxBindingDefinitionAsset"));
            Assert.That(document, Does.Contain("VfxCueMapAsset"));
            Assert.That(document, Does.Contain("VfxProfileAsset"));
            Assert.That(document, Does.Contain("prefab validation"));
            Assert.That(document, Does.Contain("Composition Ownership Gate"));
            Assert.That(document, Does.Contain("GameplayVfxBindingComposition"));
            Assert.That(document, Does.Contain("Host default map is optional"));
            Assert.That(document, Does.Contain("Family profiles override the host default map"));
            Assert.That(document, Does.Contain("Duplicate family profiles are invalid"));
            Assert.That(document, Does.Contain("Null profile entries are invalid"));
            Assert.That(document, Does.Contain("Stage map composition is a future slice"));
            Assert.That(document, Does.Contain("Future Owner Binding"));
            Assert.That(document, Does.Contain("This stage does not add fields to `StagePresentationDefinition`"));
            Assert.That(document, Does.Contain("does not add runtime prefab references or production playback connection"));
            Assert.That(document, Does.Contain("Host Anchor Resolver Gate"));
            Assert.That(document, Does.Contain("Anchor Resolver Ownership"));
            Assert.That(document, Does.Contain("resolver true/false is independent from missing-anchor policy"));
            Assert.That(document, Does.Contain("transition-aware VFX anchors are future"));
            Assert.That(document, Does.Contain("First Production Cue Gate"));
            Assert.That(document, Does.Contain("EnemyVfxCue.JumperLandingTarget"));
            Assert.That(document, Does.Contain("TickPresentationData.EnemyJumpSignals"));
            Assert.That(document, Does.Contain("StartedWindupThisTick"));
            Assert.That(document, Does.Contain("TickEnemyJumpPresentationOutcome.WindupStarted"));
            Assert.That(document, Does.Contain("PresentationTargetCell"));
            Assert.That(document, Does.Contain("VfxAnchorSlot.CellFloor"));
            Assert.That(document, Does.Contain("EnableEnemyJumpTargetVfx"));
            Assert.That(document, Does.Contain("JumperLandingTargetVfx.prefab"));
            Assert.That(document, Does.Contain("JumperLandingTarget_Binding.asset"));
            Assert.That(document, Does.Contain("GameplayVfxHostDefaultCueMap.asset"));
            Assert.That(document, Does.Contain("Enemy Death Burst Migration"));
            Assert.That(document, Does.Contain("EnemyVfxCue.Death"));
            Assert.That(document, Does.Contain("EnableGameplayVfxEnemyDeathBurstMigration"));
            Assert.That(document, Does.Contain("SuppressLegacyEnemyDeathEffects"));
            Assert.That(document, Does.Contain("EnemyDeathBurstVfx.prefab"));
            Assert.That(document, Does.Contain("EnemyDeathBurst_Binding.asset"));
            Assert.That(document, Does.Contain("old clone/arc/fade"));
            Assert.That(document, Does.Contain("FlipImpact DestroySelf Motion VFX Migration"));
            Assert.That(document, Does.Contain("BoxVfxCue.FlipDestroySelfMotion"));
            Assert.That(document, Does.Contain("EnableGameplayVfxFlipDestroySelfMotionMigration"));
            Assert.That(document, Does.Contain("SuppressLegacyFlipDestroySelfEffects"));
            Assert.That(document, Does.Contain("red/orange danger palette"));
            Assert.That(document, Does.Contain("AuthoredDuration"));
            Assert.That(document, Does.Contain("Parameterized Motion VFX Generalization"));
            Assert.That(document, Does.Contain("Parameterized Motion Sampler Modes"));
            Assert.That(document, Does.Contain("`FlipArc`"));
            Assert.That(document, Does.Contain("`Linear`"));
            Assert.That(document, Does.Contain("Position uses direct linear interpolation"));
            Assert.That(document, Does.Contain("Sampler modes are presentation-only"));
            Assert.That(document, Does.Contain("FlipDestroySelf Source-View Clone Parity"));
            Assert.That(document, Does.Contain("ParameterizedMotionVfxCommand"));
            Assert.That(document, Does.Contain("SourceViewCloneWithPrefabFallback"));
            Assert.That(document, Does.Contain("Box Slide trail"));
            Assert.That(document, Does.Contain("Unit movement trail"));
            Assert.That(document, Does.Contain("Projectile trail"));
            Assert.That(document, Does.Contain("Utility Windup VFX Migration"));
            Assert.That(document, Does.Contain("TickPresentationData.SummonWindupWarnings"));
            Assert.That(document, Does.Contain("EnemyVfxCue.UtilityWindup"));
            Assert.That(document, Does.Contain("EnableGameplayVfxUtilityWindupMigration"));
            Assert.That(document, Does.Contain("SuppressLegacyUtilityWindupVfx"));
            Assert.That(document, Does.Contain("VfxPersistentKey"));
            Assert.That(document, Does.Contain("EnemyUtilityWindupTelegraphVfx.prefab"));
            Assert.That(document, Does.Contain("EnemyUtilityWindupTelegraph_Binding.asset"));
            Assert.That(document, Does.Contain("no one-shot fallback"));
            Assert.That(document, Does.Contain("VFX Planner Dependency Rule"));
            Assert.That(document, Does.Contain("Gameplay VFX planners may read presentation carriers"));
            Assert.That(document, Does.Contain("Production Runtime Dependency Rule"));
            Assert.That(document, Does.Contain("Gameplay_Host` uses the `IGameplayTickPresentationExtension` seam"));
            Assert.That(document, Does.Contain("Prefab-local Profile Owner Gate"));
            Assert.That(document, Does.Contain("GameplayVfxRequest.SourceEntityId"));
            Assert.That(document, Does.Contain("PresentationSeed` must not be used as source identity"));
            Assert.That(document, Does.Contain("EnemyPresentationCatalogEntry.VfxProfileAsset"));
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

            Assert.That(properties, Is.EqualTo(new[]
            {
                "Int32",
                nameof(TickPresentationData),
                nameof(CubeTopologyState),
                nameof(GameplayTimingProfile),
            }));
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
            var absoluteDirectory = GetAbsolutePath(VfxRuntimePath);
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
