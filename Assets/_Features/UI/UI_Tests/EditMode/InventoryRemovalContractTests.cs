using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Stages;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class InventoryRemovalContractTests
    {
        private static readonly string[] UiInventoryTypeTokens =
        {
            "InventoryScreenView",
            "InventoryCatalogView",
            "InventoryDetailView",
            "InventoryActionView",
            "InventoryScreenPresenter",
            "InventorySampleItem",
            "ScreenId.Inventory",
        };

        [Test]
        public void ScreenIdAndCatalog_DoNotContainInventory()
        {
            Assert.That(Enum.GetNames(typeof(ScreenId)), Does.Not.Contain("Inventory"));

            var catalogMemberNames = typeof(ScreenPrefabCatalog)
                .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(member => member.Name)
                .ToArray();
            Assert.That(catalogMemberNames, Has.No.Member("_inventoryPrefab"));
            Assert.That(catalogMemberNames, Has.No.Member("InventoryPrefab"));

            var catalogAsset = ReadRepoFile(UiTestPrefabAssetUtility.ScreenCatalogPath);
            Assert.That(catalogAsset, Does.Not.Contain("Inventory"));
        }

        [Test]
        public void UiRuntime_DoesNotContainInventoryScreenViews()
        {
            foreach (var token in UiInventoryTypeTokens)
            {
                Assert.That(FindLoadedType(token), Is.Null, token);
            }

            AssertProductionUiSourcesDoNotContain(UiInventoryTypeTokens);
        }

        [Test]
        public void GameplayRoot_DoesNotExposeInventoryButton()
        {
            var uiAssets = new[]
            {
                "Assets/_Features/UI/UI_HUD/Prefabs/GameplayHudRoot.prefab",
                "Assets/_Features/UI/UI_Composition/Resources/UI/GameplayUiCanvasRootShell.prefab",
                UiTestPrefabAssetUtility.ScreenCatalogPath,
            };

            foreach (var assetPath in uiAssets)
            {
                var assetText = ReadRepoFile(assetPath);
                Assert.That(assetText, Does.Not.Contain("InventoryButton"), assetPath);
                Assert.That(assetText, Does.Not.Contain("InventoryMenu"), assetPath);
                Assert.That(assetText, Does.Not.Contain("InventoryScreen"), assetPath);
            }
        }

        [Test]
        public void StageInventoryPatch_IsNotUiInventoryScreen()
        {
            Assert.That(typeof(InventoryPatch), Is.Not.Null);

            var stageSource = ReadRepoFile("Assets/_Features/Stages/Runtime/ClearFlow/StageProgressAndCompletion.cs");
            Assert.That(stageSource, Does.Contain("public sealed class InventoryPatch"));

            foreach (var sourcePath in EnumerateProductionUiSources())
            {
                var source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain("InventoryPatch"), sourcePath);
            }
        }

        private static void AssertProductionUiSourcesDoNotContain(string[] forbiddenTokens)
        {
            foreach (var sourcePath in EnumerateProductionUiSources())
            {
                var source = File.ReadAllText(sourcePath);
                foreach (var token in forbiddenTokens)
                {
                    Assert.That(source, Does.Not.Contain(token), sourcePath);
                }
            }
        }

        private static IEnumerable<string> EnumerateProductionUiSources()
        {
            var root = GetRepoPath("Assets/_Features/UI");
            return Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}UI_Tests{Path.DirectorySeparatorChar}"))
                .OrderBy(path => path);
        }

        private static Type FindLoadedType(string typeName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(GetLoadableTypes)
                .FirstOrDefault(type => type.Name == typeName);
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
        }

        private static string GetRepoPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", relativePath));
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(GetRepoPath(relativePath));
        }
    }
}
