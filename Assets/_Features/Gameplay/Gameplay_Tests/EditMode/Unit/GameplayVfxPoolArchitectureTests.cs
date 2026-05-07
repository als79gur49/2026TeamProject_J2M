using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Host;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxPoolArchitectureTests
    {
        private const string VfxHostRuntimePath = "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime";
        private const string VfxHostPoolPath = "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Pool";
        private const string VfxHostRuntimeAsmdefPath = "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Gameplay.Vfx.Host.asmdef";

        private static readonly string[] ProductionBoundaryPaths =
        {
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHost.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHostConfiguration.cs",
            "Assets/_Features/Stages/Runtime/Content/StagePresentationDefinition.cs",
        };

        private static readonly string[] ExistingPresenterPaths =
        {
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyDeathExitEffectPlanBuilder.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayExitPresentationController.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayFrontFaceShieldVfxPresenter.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayUtilityWindupVfxPresenter.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxFlipInteractionDriver.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/FlipImpactTrack.cs",
        };

        [Test]
        [Category("Extended")]
        public void HostPoolSource_DoesNotReferenceGameplayAuthority()
        {
            var source = ReadCombinedSource(VfxHostPoolPath);

            Assert.That(source, Does.Not.Contain("WorldState"));
            Assert.That(source, Does.Not.Contain("WorldSnapshot"));
            Assert.That(source, Does.Not.Contain("TickPipeline"));
            Assert.That(source, Does.Not.Contain("CreateSnapshot"));
        }

        [Test]
        [Category("Extended")]
        public void HostRuntimeAssembly_DoesNotReferenceAuthoringStageOrLoop()
        {
            var references = typeof(GameplayVfxGameObjectPool).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            var asmdef = ReadRepoFile(VfxHostRuntimeAsmdefPath);
            var source = ReadCombinedSource(VfxHostRuntimePath);

            Assert.That(references, Does.Contain("Game.Feature.Gameplay.Vfx"));
            Assert.That(references, Does.Contain("Game.Feature.Gameplay.Host"));
            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay.Vfx.Authoring"));
            Assert.That(references, Does.Not.Contain("Game.Feature.Stages"));
            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay.Loop"));
            Assert.That(asmdef, Does.Not.Contain("Game.Feature.Gameplay.Vfx.Authoring"));
            Assert.That(asmdef, Does.Not.Contain("Game.Feature.Stages"));
            Assert.That(asmdef, Does.Not.Contain("Game.Feature.Gameplay.Loop"));
            Assert.That(source, Does.Not.Contain("VfxBindingDefinitionAsset"));
            Assert.That(source, Does.Not.Contain("VfxCueMapAsset"));
            Assert.That(source, Does.Not.Contain("VfxProfileAsset"));
        }

        [Test]
        [Category("Extended")]
        public void ProductionBindingOwners_DoNotReferencePooledRuntime()
        {
            foreach (var path in ProductionBoundaryPaths)
            {
                var source = ReadRepoFile(path);

                AssertNoPoolRuntimeReferences(source, path);
            }
        }

        [Test]
        [Category("Extended")]
        public void ExistingPresenters_DoNotReferencePooledRuntime()
        {
            foreach (var path in ExistingPresenterPaths)
            {
                var source = ReadRepoFile(path);

                AssertNoPoolRuntimeReferences(source, path);
            }
        }

        [Test]
        [Category("Extended")]
        public void CoreAssembly_DoesNotReferenceHostPooledRuntime()
        {
            var references = typeof(GameplayVfxCueId).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay.Vfx.Host"));
        }

        private static void AssertNoPoolRuntimeReferences(string source, string path)
        {
            Assert.That(source, Does.Not.Contain(nameof(GameplayVfxGameObjectPool)), path);
            Assert.That(source, Does.Not.Contain(nameof(GameplayVfxRuntimeRoot)), path);
            Assert.That(source, Does.Not.Contain(nameof(IVfxPrefabProvider)), path);
            Assert.That(source, Does.Not.Contain("Game.Feature.Gameplay.Vfx.Host"), path);
        }

        private static string ReadCombinedSource(string relativeDirectory)
        {
            return string.Join(
                "\n",
                Directory.GetFiles(Path.GetFullPath(relativeDirectory), "*.cs", SearchOption.AllDirectories)
                    .Where(path => !IsNestedProductionRuntimeSource(path))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(path => File.ReadAllText(path).Replace("\r\n", "\n")));
        }

        private static bool IsNestedProductionRuntimeSource(string path)
        {
            return path.Replace('\\', '/').Contains("/Runtime/Production/");
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(Path.GetFullPath(relativePath)).Replace("\r\n", "\n");
        }
    }
}
