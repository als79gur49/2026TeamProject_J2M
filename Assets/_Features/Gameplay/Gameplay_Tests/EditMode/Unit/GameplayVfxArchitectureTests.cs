using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Authoring;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxArchitectureTests
    {
        private const string GovernancePath = "Docs/Architecture/Gameplay-VFX-Governance.md";
        private const string VfxRuntimePath = "Assets/_Features/Gameplay/Gameplay_Vfx/Runtime";
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
            Assert.That(source, Does.Not.Contain("MonoBehaviour"));
            Assert.That(source, Does.Not.Contain("ParticleSystem"));
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
        public void PlanningContext_DoesNotExposeTickPresentationData()
        {
            var properties = typeof(GameplayVfxPlanningContext)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.PropertyType.Name)
                .ToArray();

            Assert.That(properties, Is.EqualTo(new[] { "Int32" }));
            Assert.That(properties, Does.Not.Contain("TickPresentationData"));
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
