using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TileFeatureVisualProfileValidationTests
    {
        [Test]
        [Category("Extended")]
        public void ProfileValidation_ReportsDuplicateCueMissingSlotInvalidAnimatorAndMaterialBindings()
        {
            var root = new GameObject(nameof(ProfileValidation_ReportsDuplicateCueMissingSlotInvalidAnimatorAndMaterialBindings));

            try
            {
                var target = root.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, new SurfaceCell(FaceId.Front, 1, 2));
                var profile = ScriptableObject.CreateInstance<TileFeatureVisualProfile>();
                SetPrivateField(profile, "featureKind", TileFeatureKind.Button);
                SetPrivateField(
                    profile,
                    "cueBindings",
                    new[]
                    {
                        new TileFeatureVisualCueBinding
                        {
                            CueId = TileFeatureVisualCueId.ButtonActivated,
                            TargetSlot = TileFeatureVisualSlotId.FeatureAnchor0,
                            AnimatorBinding = new TileFeatureAnimatorBinding
                            {
                                CueId = TileFeatureVisualCueId.ButtonActivated,
                                Kind = TileFeatureAnimatorBindingKind.Trigger,
                                ParameterOrStateName = string.Empty,
                            },
                        },
                        new TileFeatureVisualCueBinding
                        {
                            CueId = TileFeatureVisualCueId.ButtonActivated,
                            TargetSlot = TileFeatureVisualSlotId.Root,
                        },
                    });
                SetPrivateField(
                    profile,
                    "inactiveMaterialTargets",
                    new[]
                    {
                        new TileFeatureInactiveMaterialTarget
                        {
                            Renderer = null,
                            MaterialIndex = 0,
                            InactiveColor = Color.gray,
                            InactiveMetallic = 0.5f,
                        },
                    });

                var diagnostics = TileFeatureVisualBindingDiagnostics.ForProfile(profile, target);

                Assert.That(diagnostics.IsValid, Is.False);
                Assert.That(diagnostics.Messages, Has.Some.Contains("Duplicate cue binding"));
                Assert.That(diagnostics.Messages, Has.Some.Contains("Missing target slot"));
                Assert.That(diagnostics.Messages, Has.Some.Contains("Invalid animator binding"));
                Assert.That(diagnostics.Messages, Has.Some.Contains("Invalid material target"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void SetPrivateField<T>(TileFeatureVisualProfile profile, string fieldName, T value)
        {
            typeof(TileFeatureVisualProfile)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(profile, value);
        }
    }
}
