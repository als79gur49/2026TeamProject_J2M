using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TileFeatureVisualProfileProviderPreferredOverLegacyAdapterTests
    {
        [Test]
        [Category("Extended")]
        public void ProviderProfile_IsPreferredOverLegacyAdapterProfiles()
        {
            var root = new GameObject(nameof(ProviderProfile_IsPreferredOverLegacyAdapterProfiles));
            var providerProfile = CreateExitOpenBoolProfile("ProviderOpen");
            var legacyProfile = CreateExitOpenBoolProfile("LegacyOpen");
            var controller = CreateAnimatorController("ProviderPreferred_Controller");

            try
            {
                var target = root.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, new SurfaceCell(FaceId.Floor, 1, 1));
                var animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                var provider = root.AddComponent<TileFeatureVisualProfileProvider>();
                SetProfiles(provider, providerProfile);
#pragma warning disable CS0618
                var adapter = root.AddComponent<LegacyTileFeatureVisualCueAdapter>();
#pragma warning restore CS0618
                adapter.ConfigureTarget(target);
                SetAdapterProfiles(adapter, legacyProfile);

                adapter.SetExitOpenImmediate(true);

                Assert.That(animator.GetBool("ProviderOpen"), Is.True);
                Assert.That(animator.GetBool("LegacyOpen"), Is.False);
                Assert.That(adapter.DebugExitLegacyAnimatorFallbackCount, Is.Zero);
                Assert.That(adapter.DebugLegacyAnimatorFallbackCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(providerProfile);
                Object.DestroyImmediate(legacyProfile);
                Object.DestroyImmediate(controller);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProviderProfile_HandlesCueWhenLegacyAdapterProfilesAreEmpty()
        {
            var root = new GameObject(nameof(ProviderProfile_HandlesCueWhenLegacyAdapterProfilesAreEmpty));
            var providerProfile = CreateExitOpenBoolProfile("ProviderOpen");
            var controller = CreateAnimatorController("ProviderOnly_Controller");

            try
            {
                var target = root.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, new SurfaceCell(FaceId.Floor, 1, 1));
                var animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                var provider = root.AddComponent<TileFeatureVisualProfileProvider>();
                SetProfiles(provider, providerProfile);
#pragma warning disable CS0618
                var adapter = root.AddComponent<LegacyTileFeatureVisualCueAdapter>();
#pragma warning restore CS0618
                adapter.ConfigureTarget(target);
                SetAdapterProfiles(adapter);

                adapter.SetExitOpenImmediate(true);

                Assert.That(animator.GetBool("ProviderOpen"), Is.True);
                Assert.That(adapter.DebugExitLegacyAnimatorFallbackCount, Is.Zero);
                Assert.That(adapter.DebugLegacyAnimatorFallbackCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(providerProfile);
                Object.DestroyImmediate(controller);
            }
        }

        private static TileFeatureVisualProfile CreateExitOpenBoolProfile(string boolParameterName)
        {
            var profile = ScriptableObject.CreateInstance<TileFeatureVisualProfile>();
            typeof(TileFeatureVisualProfile)
                .GetField("featureKind", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(profile, TileFeatureKind.Exit);
            typeof(TileFeatureVisualProfile)
                .GetField("cueBindings", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(
                    profile,
                    new[]
                    {
                        new TileFeatureVisualCueBinding
                        {
                            CueId = TileFeatureVisualCueId.ExitOpenState,
                            TargetSlot = TileFeatureVisualSlotId.Root,
                            AnimatorBinding = new TileFeatureAnimatorBinding
                            {
                                CueId = TileFeatureVisualCueId.ExitOpenState,
                                Kind = TileFeatureAnimatorBindingKind.Bool,
                                ParameterOrStateName = boolParameterName,
                            },
                        },
                    });
            return profile;
        }

        private static AnimatorController CreateAnimatorController(string name)
        {
            var stateMachine = new AnimatorStateMachine
            {
                name = $"{name}_StateMachine",
            };
            var controller = new AnimatorController
            {
                name = name,
                layers = new[]
                {
                    new AnimatorControllerLayer
                    {
                        name = "Base Layer",
                        defaultWeight = 1f,
                        stateMachine = stateMachine,
                    },
                },
            };
            controller.AddParameter("ProviderOpen", AnimatorControllerParameterType.Bool);
            controller.AddParameter("LegacyOpen", AnimatorControllerParameterType.Bool);
            return controller;
        }

        private static void SetProfiles(
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

#pragma warning disable CS0618
        private static void SetAdapterProfiles(
            LegacyTileFeatureVisualCueAdapter adapter,
            params TileFeatureVisualProfile[] profiles)
        {
            var serializedAdapter = new SerializedObject(adapter);
            var profilesProperty = serializedAdapter.FindProperty("profiles");
            Assert.That(profilesProperty, Is.Not.Null);
            profilesProperty.arraySize = profiles.Length;
            for (var i = 0; i < profiles.Length; i++)
            {
                profilesProperty.GetArrayElementAtIndex(i).objectReferenceValue = profiles[i];
            }

            serializedAdapter.ApplyModifiedPropertiesWithoutUndo();
        }
#pragma warning restore CS0618
    }
}
