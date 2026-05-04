using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Host;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxHostAnchorArchitectureTests
    {
        private const string VfxRuntimePath = "Assets/_Features/Gameplay/Gameplay_Vfx/Runtime";
        private const string VfxHostRuntimePath = "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime";
        private const string VfxHostAsmdefPath = "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Gameplay.Vfx.Host.asmdef";
        private const string HostAsmdefPath = "Assets/_Features/Gameplay/Gameplay_Host/Gameplay.Host.asmdef";

        private static readonly string[] ProductionBoundaryPaths =
        {
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHost.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHostConfiguration.cs",
        };

        [Test]
        [Category("Extended")]
        public void ProductionHosts_DoNotReferenceHostAnchorResolver()
        {
            foreach (var path in ProductionBoundaryPaths)
            {
                var source = ReadRepoFile(path);

                Assert.That(source, Does.Not.Contain(nameof(GameplayVfxHostAnchorResolver)), path);
                Assert.That(source, Does.Not.Contain("Game.Feature.Gameplay.Vfx.Host"), path);
            }
        }

        [Test]
        [Category("Extended")]
        public void RuntimeCore_DoesNotReferenceHostAssembly()
        {
            var references = typeof(GameplayVfxCueId).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            var source = ReadCombinedSource(VfxRuntimePath);

            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay.Vfx.Host"));
            Assert.That(source, Does.Not.Contain("Game.Feature.Gameplay.Vfx.Host"));
            Assert.That(source, Does.Not.Contain(nameof(GameplayVfxHostAnchorResolver)));
        }

        [Test]
        [Category("Extended")]
        public void HostAnchorAssembly_DoesNotReferenceAuthoringOrAuthorityRuntime()
        {
            var references = typeof(GameplayVfxHostAnchorResolver).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            var source = ReadCombinedSource(VfxHostRuntimePath);
            var asmdef = ReadRepoFile(VfxHostAsmdefPath);

            Assert.That(references, Does.Contain("Game.Feature.Gameplay.Vfx"));
            Assert.That(references, Does.Contain("Game.Feature.Gameplay.Host"));
            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay.Vfx.Authoring"));
            Assert.That(references, Does.Not.Contain("Game.Feature.Stages"));
            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay.Loop"));
            Assert.That(asmdef, Does.Not.Contain("Game.Feature.Gameplay.Vfx.Authoring"));
            Assert.That(asmdef, Does.Not.Contain("Game.Feature.Stages"));
            Assert.That(asmdef, Does.Not.Contain("Game.Feature.Gameplay.Loop"));
            AssertForbiddenSourceTokensAbsent(source);
        }

        [Test]
        [Category("Extended")]
        public void GameplayHostAssembly_DoesNotReferenceVfxOrVfxHost()
        {
            var asmdef = ReadRepoFile(HostAsmdefPath);

            Assert.That(asmdef, Does.Not.Contain("Game.Feature.Gameplay.Vfx"));
            Assert.That(asmdef, Does.Not.Contain("Game.Feature.Gameplay.Vfx.Host"));
        }

        [Test]
        [Category("Extended")]
        public void HostAnchorPublicSurface_DoesNotExposePrefabOrAuthorityTypes()
        {
            var forbiddenTypeNames = new[]
            {
                "Game.Feature.Gameplay.BoardState.WorldState",
                "Game.Feature.Gameplay.BoardState.WorldSnapshot",
                "Game.Feature.Gameplay.Loop.TickPipeline",
                "UnityEngine.GameObject",
                "UnityEngine.MonoBehaviour",
                "UnityEngine.ParticleSystem",
            };
            var publicTypes = typeof(GameplayVfxHostAnchorResolver).Assembly
                .GetTypes()
                .Where(type => type.IsPublic || type.IsNestedPublic)
                .ToArray();

            foreach (var type in publicTypes)
            {
                foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    Assert.That(
                        ContainsForbiddenType(property.PropertyType, forbiddenTypeNames),
                        Is.False,
                        $"{type.FullName}.{property.Name}");
                }

                foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                             .Where(method => !method.IsSpecialName))
                {
                    Assert.That(
                        ContainsForbiddenType(method.ReturnType, forbiddenTypeNames),
                        Is.False,
                        $"{type.FullName}.{method.Name} return");

                    foreach (var parameter in method.GetParameters())
                    {
                        Assert.That(
                            ContainsForbiddenType(parameter.ParameterType, forbiddenTypeNames),
                            Is.False,
                            $"{type.FullName}.{method.Name} {parameter.Name}");
                    }
                }
            }
        }

        private static void AssertForbiddenSourceTokensAbsent(string source)
        {
            var forbiddenTokens = new[]
            {
                "Game.Feature.Gameplay.Vfx.Authoring",
                "VfxBindingDefinitionAsset",
                "VfxCueMapAsset",
                "VfxProfileAsset",
                "ScriptableObject",
                "WorldState",
                "WorldSnapshot",
                "TickPipeline",
                "CreateSnapshot",
                "Instantiate",
                "Destroy(",
                "ParticleSystem",
                "prefab",
            };

            foreach (var token in forbiddenTokens)
            {
                Assert.That(source, Does.Not.Contain(token), token);
            }
        }

        private static bool ContainsForbiddenType(Type type, string[] forbiddenTypeNames)
        {
            if (type.IsByRef)
            {
                type = type.GetElementType();
            }

            if (type == null)
            {
                return false;
            }

            if (forbiddenTypeNames.Contains(type.FullName))
            {
                return true;
            }

            return type.IsGenericType &&
                   type.GetGenericArguments().Any(argument => ContainsForbiddenType(argument, forbiddenTypeNames));
        }

        private static string ReadCombinedSource(string relativeDirectory)
        {
            return string.Join(
                "\n",
                Directory.GetFiles(Path.GetFullPath(relativeDirectory), "*.cs", SearchOption.AllDirectories)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(path => File.ReadAllText(path).Replace("\r\n", "\n")));
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(Path.GetFullPath(relativePath)).Replace("\r\n", "\n");
        }
    }
}
