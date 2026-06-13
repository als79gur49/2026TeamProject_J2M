using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TileFeatureVisualNoProfilePolicyTests
    {
        [Test]
        [Category("Full")]
        public void ButtonEntranceAndDefaultExitProductionPrefabs_NoAnimatorNoProviderNoProfile_AreExplicitVfxOnlyNoOp()
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
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[i]);
                Assert.That(prefab, Is.Not.Null, prefabPaths[i]);
                Assert.That(prefab.GetComponent<TileFeatureVisualTargetView>(), Is.Not.Null, prefabPaths[i]);
                Assert.That(prefab.GetComponent<TileFeatureVisualProfileProvider>(), Is.Null, prefabPaths[i]);
                Assert.That(prefab.GetComponentInChildren<Animator>(includeInactive: true), Is.Null, prefabPaths[i]);
                AssertNoMissingScriptResidue(prefab, prefabPaths[i]);
            }
        }

        [Test]
        [Category("Full")]
        public void ButtonEntranceAndDefaultExitProductionPrefabs_NoProviderNoProfile_RuntimePolicyDoesNotWarn()
        {
            AssertNoProfileRuntimeNoOp(
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Button_Default.prefab",
                TileFeatureKind.Button);
            AssertNoProfileRuntimeNoOp(
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Button_MoonOnly.prefab",
                TileFeatureKind.Button);
            AssertNoProfileRuntimeNoOp(
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Entrance_Default.prefab",
                TileFeatureKind.Entrance);
            AssertNoProfileRuntimeNoOp(
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Exit_Default.prefab",
                TileFeatureKind.Exit);

            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertNoProfileRuntimeNoOp(string prefabPath, TileFeatureKind featureKind)
        {
            var root = new GameObject($"{nameof(AssertNoProfileRuntimeNoOp)}_{featureKind}");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);

            try
            {
                var cell = new SurfaceCell(FaceId.Floor, 1, 1);
                var instance = Object.Instantiate(prefab, root.transform, worldPositionStays: false);
                var target = instance.GetComponent<TileFeatureVisualTargetView>();
                Assert.That(target, Is.Not.Null, prefabPath);
                target.Configure(100, cell);

                var registry = root.AddComponent<TileFeatureVisualRegistry>();
                registry.ConfigureSearchRoot(root.transform);
                var controller = new TileFeatureVisualPresentationController();
                controller.AttachRegistry(registry);

                if (featureKind == TileFeatureKind.Button)
                {
                    controller.PlayButtonActivatedRequests(
                        new[]
                        {
                            new TilePresentationRequest(
                                TilePresentationRequestKind.ButtonActivated,
                                100,
                                cell,
                                TileFeatureKind.Button,
                                sourceEntityId: 0,
                                ownerEntityId: 0,
                                teamId: 0),
                        });
                }
                else
                {
                    controller.RefreshContinuousStates(
                        new[]
                        {
                            new TileFeatureVisualState(
                                100,
                                cell,
                            featureKind,
                                isActive: true,
                                sourceEntityId: 0,
                                ownerEntityId: 0,
                                teamId: 0),
                        });
                }

                Assert.That(instance.GetComponent<TileFeatureVisualProfileProvider>(), Is.Null, prefabPath);
                AssertNoMissingScriptResidue(instance, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AssertNoMissingScriptResidue(GameObject root, string context)
        {
            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                Assert.That(behaviours[i], Is.Not.Null, context);
            }
        }
    }
}
