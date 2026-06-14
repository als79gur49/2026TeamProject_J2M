using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TileFeatureVisualProfileProviderTests
    {
        [Test]
        [Category("Extended")]
        public void Provider_StoresAndResolvesProfilesByFeatureKind()
        {
            var root = new GameObject(nameof(Provider_StoresAndResolvesProfilesByFeatureKind));
            var barricadeProfile = CreateProfile(TileFeatureKind.Barricade);
            var exitProfile = CreateProfile(TileFeatureKind.Exit);

            try
            {
                var provider = root.AddComponent<TileFeatureVisualProfileProvider>();
                SetProviderProfiles(provider, barricadeProfile, exitProfile);

                Assert.That(provider.Profiles.Count, Is.EqualTo(2));
                Assert.That(provider.TryGetProfile(TileFeatureKind.Barricade, out var resolvedBarricade), Is.True);
                Assert.That(resolvedBarricade, Is.SameAs(barricadeProfile));
                Assert.That(provider.TryGetProfile(TileFeatureKind.Exit, out var resolvedExit), Is.True);
                Assert.That(resolvedExit, Is.SameAs(exitProfile));
                Assert.That(provider.TryGetProfile(TileFeatureKind.MoonBlockGenerator, out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(barricadeProfile);
                Object.DestroyImmediate(exitProfile);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProviderValidation_ReportsDuplicateFeatureKindAndNullProfile()
        {
            var root = new GameObject(nameof(ProviderValidation_ReportsDuplicateFeatureKindAndNullProfile));
            var firstProfile = CreateProfile(TileFeatureKind.Exit);
            var duplicateProfile = CreateProfile(TileFeatureKind.Exit);

            try
            {
                var target = root.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, new SurfaceCell(FaceId.Front, 1, 2));
                var provider = root.AddComponent<TileFeatureVisualProfileProvider>();
                SetProviderProfiles(provider, firstProfile, null, duplicateProfile);

                var diagnostics = provider.ValidateProfiles(target);

                Assert.That(diagnostics.IsValid, Is.False);
                Assert.That(diagnostics.Messages, Has.Some.Contains("Profile at index 1 is missing."));
                Assert.That(diagnostics.Messages, Has.Some.Contains("Duplicate profile feature kind: Exit."));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(firstProfile);
                Object.DestroyImmediate(duplicateProfile);
            }
        }

        private static TileFeatureVisualProfile CreateProfile(TileFeatureKind featureKind)
        {
            var profile = ScriptableObject.CreateInstance<TileFeatureVisualProfile>();
            typeof(TileFeatureVisualProfile)
                .GetField("featureKind", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(profile, featureKind);
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
    }
}
