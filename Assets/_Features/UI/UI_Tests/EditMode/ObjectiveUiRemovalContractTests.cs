using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class ObjectiveUiRemovalContractTests
    {
        private static readonly string ObjectiveStatusPrefabGuid = string.Concat("ba1034f4e0daecc4", "28ca3e9371be547f");
        private static readonly string ObjectiveInfoPrefabGuid = string.Concat("fc48a7db19d86374", "ba6a432dfd699dad");
        private static readonly string RewardPopupPrefabGuid = string.Concat("5f4bf5ee49ba6b14", "795abf377afba43a");

        private static readonly string[] RemovedRuntimeTokens =
        {
            "ScreenId.ObjectiveStatus",
            "PopupId.ObjectiveInfo",
            "OpenObjectiveStatusScreen",
            "RequestObjectiveInfoPopup",
            "ObjectiveStatusScreenView",
            "ObjectiveStatusPresenter",
            "ObjectiveStatusScreenPresenter",
            "ObjectiveStatusScreenPayload",
            "ObjectiveStatusScreenViewModel",
            "ObjectiveInfoPopupView",
            "ObjectiveInfoPopupPayload",
            "ObjectiveInfoPopupViewModel",
            "ObjectiveInfoPopupPresenter",
            "PausePopup.ObjectiveRequested",
            "ObjectiveRequested",
            "PopupId.Reward",
            "RequestRewardPopup",
            "OpenRewardPopup",
            "RewardPopupView",
            "RewardPopupPayload",
            "RewardPopupViewModel",
            "RewardPopupPresenter",
            "RewardPopupPayloadMapper",
        };

        [Test]
        public void ScreenAndPopupIds_DoNotContainRemovedObjectiveUi()
        {
            Assert.That(Enum.GetNames(typeof(ScreenId)), Does.Not.Contain("ObjectiveStatus"));
            Assert.That(Enum.GetNames(typeof(PopupId)), Does.Not.Contain("ObjectiveInfo"));
            Assert.That(Enum.GetNames(typeof(PopupId)), Does.Not.Contain("Reward"));
            Assert.That(Enum.GetNames(typeof(PopupCompletionKind)), Does.Not.Contain("ObjectiveRequested"));
            Assert.That(GetMemberNames(typeof(PausePopupPayload)), Has.No.Member("ObjectiveLabel"));
            Assert.That(GetMemberNames(typeof(PausePopupViewModel)), Has.No.Member("ObjectiveLabel"));
        }

        [Test]
        public void Catalogs_DoNotExposeRemovedObjectivePrefabs()
        {
            var screenCatalogMembers = GetMemberNames(typeof(ScreenPrefabCatalog));
            Assert.That(screenCatalogMembers, Has.No.Member("_objectiveStatusPrefab"));
            Assert.That(screenCatalogMembers, Has.No.Member("ObjectiveStatusPrefab"));

            var popupCatalogMembers = GetMemberNames(typeof(PopupPrefabCatalog));
            Assert.That(popupCatalogMembers, Has.No.Member("_objectiveInfoPrefab"));
            Assert.That(popupCatalogMembers, Has.No.Member("ObjectiveInfoPrefab"));
            Assert.That(popupCatalogMembers, Has.No.Member("_rewardPrefab"));
            Assert.That(popupCatalogMembers, Has.No.Member("RewardPrefab"));

            Assert.That(ReadRepoFile(UiTestPrefabAssetUtility.ScreenCatalogPath), Does.Not.Contain(ObjectiveStatusPrefabGuid));
            Assert.That(ReadRepoFile(UiTestPrefabAssetUtility.PopupCatalogPath), Does.Not.Contain(ObjectiveInfoPrefabGuid));
            Assert.That(ReadRepoFile(UiTestPrefabAssetUtility.PopupCatalogPath), Does.Not.Contain(RewardPopupPrefabGuid));
            Assert.That(ReadRepoFile(UiTestPrefabAssetUtility.PopupCatalogPath), Does.Not.Contain("RewardPopup"));
        }

        [Test]
        public void DeletedObjectivePrefabs_AreAbsentAndNotReferencedByUiAssets()
        {
            Assert.That(File.Exists(GetRepoPath("Assets/_Features/UI/UI_Screens/Prefabs/ObjectiveStatusScreen.prefab")), Is.False);
            Assert.That(File.Exists(GetRepoPath("Assets/_Features/UI/UI_Popups/Prefabs/ObjectiveInfoPopup.prefab")), Is.False);
            Assert.That(File.Exists(GetRepoPath("Assets/_Features/UI/UI_Popups/Prefabs/RewardPopup.prefab")), Is.False);
            Assert.That(File.Exists(GetRepoPath("Assets/_Features/UI/UI_Popups/Prefabs/RewardPopup.prefab.meta")), Is.False);

            foreach (var assetPath in EnumerateUiAssetFiles())
            {
                var text = File.ReadAllText(assetPath);
                Assert.That(text, Does.Not.Contain(ObjectiveStatusPrefabGuid), assetPath);
                Assert.That(text, Does.Not.Contain(ObjectiveInfoPrefabGuid), assetPath);
                Assert.That(text, Does.Not.Contain(RewardPopupPrefabGuid), assetPath);
                Assert.That(text, Does.Not.Contain("RewardPopup"), assetPath);
            }

            foreach (var docPath in EnumerateCurrentUiStructureDocs())
            {
                var text = File.ReadAllText(docPath);
                Assert.That(text, Does.Not.Contain(RewardPopupPrefabGuid), docPath);
            }
        }

        [Test]
        public void ProductionUiSources_DoNotContainRemovedObjectiveUiRuntimeTokens()
        {
            foreach (var sourcePath in EnumerateProductionUiSources())
            {
                var source = File.ReadAllText(sourcePath);
                foreach (var token in RemovedRuntimeTokens)
                {
                    Assert.That(source, Does.Not.Contain(token), sourcePath);
                }
            }
        }

        private static string[] GetMemberNames(Type type)
        {
            return type
                .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(member => member.Name)
                .ToArray();
        }

        private static IEnumerable<string> EnumerateProductionUiSources()
        {
            var root = GetRepoPath("Assets/_Features/UI");
            return Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}UI_Tests{Path.DirectorySeparatorChar}"))
                .OrderBy(path => path);
        }

        private static IEnumerable<string> EnumerateUiAssetFiles()
        {
            var root = GetRepoPath("Assets/_Features/UI");
            return Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(path =>
                    path.EndsWith(".asset", StringComparison.Ordinal) ||
                    path.EndsWith(".prefab", StringComparison.Ordinal) ||
                    path.EndsWith(".meta", StringComparison.Ordinal) ||
                    path.EndsWith(".unity", StringComparison.Ordinal))
                .OrderBy(path => path);
        }

        private static IEnumerable<string> EnumerateCurrentUiStructureDocs()
        {
            return new[]
            {
                "UI-Current-Structure-Source.md",
                "Docs/Architecture/UI-Architecture-Guidelines.md",
                "Docs/Architecture/Audio-Architecture-Guidelines.md",
                "Docs/Testing/UI-EditMode-Baseline-2026-04-15.md",
                "Docs/Testing/Gameplay-Test-Automation-Guide.md",
            }
            .Select(GetRepoPath)
            .OrderBy(path => path);
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(GetRepoPath(relativePath));
        }

        private static string GetRepoPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", relativePath));
        }
    }
}
