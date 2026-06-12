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
            var rendererObject = new GameObject("MaterialTarget");
            rendererObject.transform.SetParent(root.transform, worldPositionStays: false);
            var nullMaterialRendererObject = new GameObject("NullMaterialTarget");
            nullMaterialRendererObject.transform.SetParent(root.transform, worldPositionStays: false);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));

            try
            {
                var target = root.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, new SurfaceCell(FaceId.Front, 1, 2));
                var renderer = rendererObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { material };
                var nullMaterialRenderer = nullMaterialRendererObject.AddComponent<MeshRenderer>();
                nullMaterialRenderer.sharedMaterials = new Material[] { null };
                var profile = ScriptableObject.CreateInstance<TileFeatureVisualProfile>();
                SetPrivateField(profile, "featureKind", TileFeatureKind.Slide);
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
                        new TileFeatureInactiveMaterialTarget
                        {
                            Renderer = renderer,
                            MaterialIndex = 4,
                            InactiveColor = Color.gray,
                            InactiveMetallic = 0.5f,
                        },
                        new TileFeatureInactiveMaterialTarget
                        {
                            Renderer = nullMaterialRenderer,
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
                Assert.That(diagnostics.Messages, Has.Some.Contains("renderer is missing"));
                Assert.That(diagnostics.Messages, Has.Some.Contains("material index 4 is out of range"));
                Assert.That(diagnostics.Messages, Has.Some.Contains("material is missing"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProfileValidation_ReportsInvalidActiveStateAnimatorBindings()
        {
            var profile = ScriptableObject.CreateInstance<TileFeatureVisualProfile>();

            try
            {
                SetPrivateField(profile, "featureKind", TileFeatureKind.Barricade);
                SetPrivateField(
                    profile,
                    "cueBindings",
                    new[]
                    {
                        new TileFeatureVisualCueBinding
                        {
                            CueId = TileFeatureVisualCueId.BarricadeActiveState,
                            TargetSlot = TileFeatureVisualSlotId.Root,
                            AnimatorBinding = new TileFeatureAnimatorBinding
                            {
                                CueId = TileFeatureVisualCueId.BarricadeActiveState,
                                Kind = TileFeatureAnimatorBindingKind.Bool,
                                ParameterOrStateName = "BarricadeActive",
                            },
                            ActiveStateAnimatorBinding = new TileFeatureAnimatorBinding
                            {
                                CueId = TileFeatureVisualCueId.BarricadeBlocked,
                                Kind = TileFeatureAnimatorBindingKind.State,
                                ParameterOrStateName = string.Empty,
                            },
                            InactiveStateAnimatorBinding = new TileFeatureAnimatorBinding
                            {
                                CueId = TileFeatureVisualCueId.BarricadeActiveState,
                                Kind = TileFeatureAnimatorBindingKind.State,
                                ParameterOrStateName = string.Empty,
                            },
                        },
                    });

                var diagnostics = TileFeatureVisualBindingDiagnostics.ForProfile(profile);

                Assert.That(diagnostics.IsValid, Is.False);
                Assert.That(diagnostics.Messages, Has.Some.Contains("Invalid active state animator binding"));
                Assert.That(diagnostics.Messages, Has.Some.Contains("Invalid inactive state animator binding"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
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
