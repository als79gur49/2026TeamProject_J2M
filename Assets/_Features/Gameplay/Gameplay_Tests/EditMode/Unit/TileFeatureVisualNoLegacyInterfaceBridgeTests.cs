using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TileFeatureVisualNoLegacyInterfaceBridgeTests
    {
        private const string RuntimeRoot = "Assets/_Features/Gameplay/Gameplay_Host/Runtime";
        private const string ControllerPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/TileFeatureVisualPresentationController.cs";
        private const string FactoryPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs";
        private const string RegistryPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/ITileFeatureVisualRegistry.cs";

        [Test]
        [Category("Extended")]
        public void RuntimeSource_DoesNotContainRetiredBridgeSink()
        {
            Assert.That(ReadRepoFile(ControllerPath), Does.Not.Contain(RetiredBridgeSinkName()));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeRegistry_DoesNotDeclareObsoleteFeatureSpecificVisualInterfaces()
        {
            var source = ReadRepoFile(RegistryPath);
            var forbiddenTokens = new[]
            {
                "IDestroyTileVisualTarget",
                "IDestroyTileActivatedVisualTarget",
                "IDestroyTileDeactivatedVisualTarget",
                "IDestroyTileActiveStateVisualTarget",
                "ITileFeatureActiveStateVisualTarget",
                "ISlideTileVisualTarget",
                "IBarricadeBlockedVisualTarget",
                "IBarricadeCrushedVisualTarget",
                "IBarricadeActivatedVisualTarget",
                "IBarricadeDeactivatedVisualTarget",
                "IBarricadeActiveStateVisualTarget",
                "IExitOpenedVisualTarget",
                "IExitEnteredVisualTarget",
                "IExitOpenStateVisualTarget",
                "IMoonBlockGeneratedVisualTarget",
                "IMoonBlockGeneratorBlockedVisualTarget",
                "System.Obsolete",
            };

            foreach (var token in forbiddenTokens)
            {
                Assert.That(source, Does.Not.Contain(token), token);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayHostRuntimeFactory_DoesNotFallbackToObsoletePlaySetInterfaces()
        {
            var source = ReadRepoFile(FactoryPath);
            var forbiddenTokens = new[]
            {
                "SetBarricadeActiveImmediate",
                "SetTileFeatureActiveImmediate",
                "SetDestroyTileActiveImmediate",
                "SetExitOpenImmediate",
                "IBarricadeActiveStateVisualTarget",
                "ITileFeatureActiveStateVisualTarget",
                "IDestroyTileActiveStateVisualTarget",
                "IExitOpenStateVisualTarget",
            };

            foreach (var token in forbiddenTokens)
            {
                Assert.That(source, Does.Not.Contain(token), token);
            }
        }

        [Test]
        [Category("Extended")]
        public void TileFeatureVisualPresentationController_DoesNotUseLegacyVisualReflection()
        {
            var source = ReadRepoFile(ControllerPath);

            Assert.That(source, Does.Not.Contain("GetMethod"));
            Assert.That(source, Does.Not.Contain("BindingFlags"));
            Assert.That(source, Does.Not.Contain("method.Invoke"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayHostRuntime_DoesNotReferenceRetiredAdapter()
        {
            var source = ReadCombinedRuntimeSource();

            Assert.That(source, Does.Not.Contain(RetiredAdapterName()));
            Assert.That(source, Does.Not.Contain("AddComponent<" + "Legacy"));
        }

        [Test]
        [Category("Extended")]
        public void FeatureAssets_DoNotContainDeletedAdapterGuid()
        {
            var source = ReadCombinedFeatureAssetText();

            Assert.That(source, Does.Not.Contain(DeletedAdapterGuid()));
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(relativePath).Replace("\r\n", "\n");
        }

        private static string ReadCombinedRuntimeSource()
        {
            return string.Join(
                "\n",
                Directory.GetFiles(RuntimeRoot, "*.cs", SearchOption.AllDirectories)
                    .Select(ReadRepoFile));
        }

        private static string ReadCombinedFeatureAssetText()
        {
            var textExtensions = new[]
            {
                ".asmdef",
                ".asset",
                ".cs",
                ".meta",
                ".prefab",
                ".unity",
            };

            return string.Join(
                "\n",
                Directory.GetFiles("Assets/_Features", "*", SearchOption.AllDirectories)
                    .Where(path => textExtensions.Contains(Path.GetExtension(path)))
                    .Select(ReadRepoFile));
        }

        private static string RetiredAdapterName()
        {
            return "Legacy" + "TileFeatureVisualCue" + "Adapter";
        }

        private static string RetiredBridgeSinkName()
        {
            return "Legacy" + "InterfaceCueSink";
        }

        private static string DeletedAdapterGuid()
        {
            return "698f950f2ec6479" + "ca0c7f14bdf115c0d";
        }
    }
}
