using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TileFeatureVisualNoAutoAddForbiddenRuntimePathTests
    {
        [Test]
        [Category("Extended")]
        public void Controller_ProviderBackedTarget_DoesNotAutoAddForbiddenRuntimePath()
        {
            var root = new GameObject(nameof(Controller_ProviderBackedTarget_DoesNotAutoAddForbiddenRuntimePath));
            var targetObject = new GameObject("TileFeatureTarget");
            targetObject.transform.SetParent(root.transform, worldPositionStays: false);
            var profile = CreateProfile(TileFeatureKind.Button, TileFeatureVisualCueId.ButtonActivated);

            try
            {
                var cell = new SurfaceCell(FaceId.Floor, 1, 1);
                var registry = root.AddComponent<TileFeatureVisualRegistry>();
                var target = targetObject.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, cell);
                var provider = targetObject.AddComponent<TileFeatureVisualProfileProvider>();
                SetProviderProfiles(provider, profile);
                registry.ConfigureSearchRoot(root.transform);
                var controller = new TileFeatureVisualPresentationController();
                controller.AttachRegistry(registry);

                controller.PlayButtonActivatedRequests(new[] { CreateButtonRequest(100, cell) });

                AssertNoMissingMonoBehaviours(targetObject);
                Assert.That(targetObject.GetComponent<TileFeatureVisualProfileCueSink>(), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void Factory_ProviderBackedTarget_DoesNotAutoAddForbiddenRuntimePath()
        {
            var targetObject = new GameObject(nameof(Factory_ProviderBackedTarget_DoesNotAutoAddForbiddenRuntimePath));
            var profile = CreateProfile(TileFeatureKind.Exit, TileFeatureVisualCueId.ExitOpenState);

            try
            {
                var target = targetObject.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, new SurfaceCell(FaceId.Floor, 1, 1));
                var provider = targetObject.AddComponent<TileFeatureVisualProfileProvider>();
                SetProviderProfiles(provider, profile);

                var sink = InvokeFactoryResolveTileFeatureCueSink(target, TileFeatureKind.Exit);

                Assert.That(sink, Is.TypeOf<TileFeatureVisualProfileCueSink>());
                AssertNoMissingMonoBehaviours(targetObject);
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(profile);
            }
        }

        private static ITileFeatureVisualCueSink InvokeFactoryResolveTileFeatureCueSink(
            ITileFeatureVisualTarget target,
            TileFeatureKind featureKind)
        {
            var method = typeof(GameplayHostRuntimeFactory).GetMethod(
                "ResolveTileFeatureCueSink",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return (ITileFeatureVisualCueSink)method.Invoke(null, new object[] { target, featureKind });
        }

        private static TilePresentationRequest CreateButtonRequest(int tileId, SurfaceCell cell)
        {
            return new TilePresentationRequest(
                TilePresentationRequestKind.ButtonActivated,
                tileId,
                cell,
                TileFeatureKind.Button,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0);
        }

        private static TileFeatureVisualProfile CreateProfile(
            TileFeatureKind featureKind,
            TileFeatureVisualCueId cueId)
        {
            var profile = ScriptableObject.CreateInstance<TileFeatureVisualProfile>();
            typeof(TileFeatureVisualProfile)
                .GetField("featureKind", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(profile, featureKind);
            typeof(TileFeatureVisualProfile)
                .GetField("cueBindings", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(
                    profile,
                    new[]
                    {
                        new TileFeatureVisualCueBinding
                        {
                            CueId = cueId,
                        },
                    });
            return profile;
        }

        private static void SetProviderProfiles(
            TileFeatureVisualProfileProvider provider,
            params TileFeatureVisualProfile[] profiles)
        {
            var serializedProvider = new SerializedObject(provider);
            var profilesProperty = serializedProvider.FindProperty("profiles");
            Assert.That(profilesProperty, Is.Not.Null);
            profilesProperty.arraySize = profiles.Length;
            for (var i = 0; i < profiles.Length; i++)
            {
                profilesProperty.GetArrayElementAtIndex(i).objectReferenceValue = profiles[i];
            }

            serializedProvider.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssertNoMissingMonoBehaviours(GameObject root)
        {
            var behaviours = root.GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                Assert.That(behaviours[i], Is.Not.Null, root.name);
            }
        }
    }
}
