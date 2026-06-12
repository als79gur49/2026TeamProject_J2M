using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TileFeatureVisualNoProfilePolicyTests
    {
        [Test]
        [Category("Full")]
        public void ButtonAndEntranceProductionPrefabs_NoAnimatorNoProfile_AreExplicitVfxOnlyNoOp()
        {
            var prefabPaths = new[]
            {
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Button_Default.prefab",
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Button_MoonOnly.prefab",
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Entrance_Default.prefab",
            };

            for (var i = 0; i < prefabPaths.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[i]);
                Assert.That(prefab, Is.Not.Null, prefabPaths[i]);
                Assert.That(prefab.GetComponent<TileFeatureVisualTargetView>(), Is.Not.Null, prefabPaths[i]);
                Assert.That(prefab.GetComponentInChildren<Animator>(includeInactive: true), Is.Null, prefabPaths[i]);
                Assert.That(prefab.GetComponent<LegacyTileFeatureVisualCueAdapter>(), Is.Null, prefabPaths[i]);
            }
        }
    }
}
