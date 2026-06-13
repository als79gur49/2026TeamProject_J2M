using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TileFeatureVisualProductionProfileProviderTests
    {
        [Test]
        [Category("Full")]
        public void AnimatorAndMaterialBackedProductionPrefabs_HaveProviderProfileReferences()
        {
            AssertProviderProfile(
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Barricade_Default.prefab",
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Profiles/TileFeatureVisualProfile_Barricade.asset",
                TileFeatureKind.Barricade);
            AssertProviderProfile(
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Exit_3x3.prefab",
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Profiles/TileFeatureVisualProfile_Exit.asset",
                TileFeatureKind.Exit);
            AssertProviderProfile(
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_MoonGenerator_Default.prefab",
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Profiles/TileFeatureVisualProfile_MoonGenerator.asset",
                TileFeatureKind.MoonBlockGenerator);
            AssertProviderProfile(
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Destroy_Bottom.prefab",
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Profiles/TileFeatureVisualProfile_Destroy_Bottom.asset",
                TileFeatureKind.Destroy);
            AssertProviderProfile(
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Destroy_Front.prefab",
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Profiles/TileFeatureVisualProfile_Destroy_Front.asset",
                TileFeatureKind.Destroy);
            AssertProviderProfile(
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Slide_Down.prefab",
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Profiles/TileFeatureVisualProfile_Slide.asset",
                TileFeatureKind.Slide);
            AssertProviderProfile(
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Slide_Left.prefab",
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Profiles/TileFeatureVisualProfile_Slide.asset",
                TileFeatureKind.Slide);
            AssertProviderProfile(
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Slide_Right.prefab",
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Profiles/TileFeatureVisualProfile_Slide.asset",
                TileFeatureKind.Slide);
            AssertProviderProfile(
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Slide_Right_DirectVariant.prefab",
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Profiles/TileFeatureVisualProfile_Slide.asset",
                TileFeatureKind.Slide);
            AssertProviderProfile(
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Slide_Up.prefab",
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Profiles/TileFeatureVisualProfile_Slide.asset",
                TileFeatureKind.Slide);
        }

        [Test]
        [Category("Full")]
        public void VfxOnlyProductionPrefabs_DoNotHaveProviderProfiles()
        {
            AssertNoProviderProfile("Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Button_Default.prefab");
            AssertNoProviderProfile("Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Button_MoonOnly.prefab");
            AssertNoProviderProfile("Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Entrance_Default.prefab");
            AssertNoProviderProfile("Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Exit_Default.prefab");
        }

        private static void AssertProviderProfile(
            string prefabPath,
            string profilePath,
            TileFeatureKind expectedKind)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var expectedProfile = AssetDatabase.LoadAssetAtPath<TileFeatureVisualProfile>(profilePath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            Assert.That(expectedProfile, Is.Not.Null, profilePath);
            Assert.That(expectedProfile.FeatureKind, Is.EqualTo(expectedKind), profilePath);

            var provider = prefab.GetComponent<TileFeatureVisualProfileProvider>();
            Assert.That(provider, Is.Not.Null, prefabPath);
            Assert.That(provider.Profiles.Count, Is.GreaterThan(0), prefabPath);
            Assert.That(provider.TryGetProfile(expectedKind, out var actualProfile), Is.True, prefabPath);
            Assert.That(actualProfile, Is.SameAs(expectedProfile), prefabPath);
            AssertLegacyAdapterProfilesEmpty(prefab, prefabPath);
        }

        private static void AssertNoProviderProfile(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            Assert.That(prefab.GetComponent<TileFeatureVisualProfileProvider>(), Is.Null, prefabPath);
        }

#pragma warning disable CS0618
        private static void AssertLegacyAdapterProfilesEmpty(GameObject prefab, string prefabPath)
        {
            var adapter = prefab.GetComponent<LegacyTileFeatureVisualCueAdapter>();
            Assert.That(adapter, Is.Not.Null, prefabPath);

            var serializedAdapter = new SerializedObject(adapter);
            var profiles = serializedAdapter.FindProperty("profiles");
            Assert.That(profiles, Is.Not.Null, prefabPath);
            Assert.That(profiles.arraySize, Is.Zero, prefabPath);
        }
#pragma warning restore CS0618
    }
}
