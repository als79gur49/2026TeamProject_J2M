using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TileFeatureVisualProductionPrefabNoRetiredAdapterComponentTests
    {
        [Test]
        [Category("Full")]
        public void ProductionProfilePrefabs_LoadWithoutRetiredAdapterResidueAndKeepProviderProfiles()
        {
            var prefabCases = new (string Path, TileFeatureKind Kind)[]
            {
                ("Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Barricade_Default.prefab", TileFeatureKind.Barricade),
                ("Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Exit_3x3.prefab", TileFeatureKind.Exit),
                ("Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_MoonGenerator_Default.prefab", TileFeatureKind.MoonBlockGenerator),
                ("Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Destroy_Bottom.prefab", TileFeatureKind.Destroy),
                ("Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Destroy_Front.prefab", TileFeatureKind.Destroy),
                ("Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Slide_Down.prefab", TileFeatureKind.Slide),
                ("Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Slide_Left.prefab", TileFeatureKind.Slide),
                ("Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Slide_Right.prefab", TileFeatureKind.Slide),
                ("Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Slide_Right_DirectVariant.prefab", TileFeatureKind.Slide),
                ("Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Slide_Up.prefab", TileFeatureKind.Slide),
            };

            for (var i = 0; i < prefabCases.Length; i++)
            {
                var prefab = LoadPrefab(prefabCases[i].Path);
                AssertNoRetiredAdapterResidue(prefab, prefabCases[i].Path);

                var provider = prefab.GetComponent<TileFeatureVisualProfileProvider>();
                Assert.That(provider, Is.Not.Null, prefabCases[i].Path);
                Assert.That(provider.Profiles.Count, Is.GreaterThan(0), prefabCases[i].Path);
                Assert.That(provider.TryGetProfile(prefabCases[i].Kind, out var profile), Is.True, prefabCases[i].Path);
                Assert.That(profile, Is.Not.Null, prefabCases[i].Path);
            }
        }

        [Test]
        [Category("Full")]
        public void ExplicitNoProfilePolicyPrefabs_LoadWithoutRetiredAdapterResidueOrProvider()
        {
            var prefabPaths = new[]
            {
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Button_Default.prefab",
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Button_MoonOnly.prefab",
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Entrance_Default.prefab",
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Exit_Default.prefab",
            };

            for (var i = 0; i < prefabPaths.Length; i++)
            {
                var prefab = LoadPrefab(prefabPaths[i]);
                AssertNoRetiredAdapterResidue(prefab, prefabPaths[i]);
                Assert.That(prefab.GetComponent<TileFeatureVisualProfileProvider>(), Is.Null, prefabPaths[i]);
            }
        }

        private static GameObject LoadPrefab(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            return prefab;
        }

        private static void AssertNoRetiredAdapterResidue(GameObject prefab, string prefabPath)
        {
            var behaviours = prefab.GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                Assert.That(behaviours[i], Is.Not.Null, prefabPath);
            }
        }
    }
}
