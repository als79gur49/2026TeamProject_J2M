using System.IO;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TileFeatureVisualNoHardcodedAnimatorCommandPathTests
    {
        private static readonly string[] SourcePaths =
        {
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/TileFeatureVisualProfileCueSink.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/TileFeatureVisualHandlers.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/TileFeatureVisualPresentationController.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs",
        };

        [Test]
        [Category("Extended")]
        public void RuntimeTileFeatureVisualPath_HasNoHardcodedAnimatorCommandPath()
        {
            var source = string.Join("\n", SourcePaths.Select(File.ReadAllText));

            Assert.That(source, Does.Not.Contain("Apply" + RemovedPathPrefix() + "Animator" + RemovedCommandSuffix()));
            Assert.That(source, Does.Not.Contain("PlayBarricade" + RemovedCommandSuffix()));
            Assert.That(source, Does.Not.Contain("PlayExit" + RemovedCommandSuffix()));
            Assert.That(source, Does.Not.Contain("Debug" + RemovedPathPrefix() + "Animator" + RemovedCommandSuffix()));
            Assert.That(source, Does.Not.Contain("ExitOpenedTrigger"));
        }

        private static string RemovedPathPrefix()
        {
            return "Leg" + "acy";
        }

        private static string RemovedCommandSuffix()
        {
            return "Fall" + "back";
        }
    }

    public sealed class TileFeatureVisualProfilePathOwnsAnimatorBehaviorTests
    {
        [Test]
        [Category("Extended")]
        public void BarricadeExitAndMoonAnimatorCues_AreHandledByProviderProfileSink()
        {
            var root = new GameObject(nameof(BarricadeExitAndMoonAnimatorCues_AreHandledByProviderProfileSink));
            var controller = CreateAnimatorController(nameof(BarricadeExitAndMoonAnimatorCues_AreHandledByProviderProfileSink));
            var barricadeProfile = CreateAnimatorProfile(
                TileFeatureKind.Barricade,
                CreateBinding(TileFeatureVisualCueId.BarricadeActiveState, TileFeatureAnimatorBindingKind.Bool, "BarricadeActive"));
            var exitProfile = CreateAnimatorProfile(
                TileFeatureKind.Exit,
                CreateBinding(TileFeatureVisualCueId.ExitOpenState, TileFeatureAnimatorBindingKind.Bool, "ExitOpen"));
            var moonProfile = CreateAnimatorProfile(
                TileFeatureKind.MoonBlockGenerator,
                CreateBinding(TileFeatureVisualCueId.MoonBlockGenerated, TileFeatureAnimatorBindingKind.Trigger, "MoonBlockGenerated"));

            try
            {
                var target = root.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, new SurfaceCell(FaceId.Floor, 1, 1));
                var animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                var provider = root.AddComponent<TileFeatureVisualProfileProvider>();
                SetProviderProfiles(provider, barricadeProfile, exitProfile, moonProfile);
                var sink = root.AddComponent<TileFeatureVisualProfileCueSink>();
                sink.Configure(target, provider);

                Assert.That(sink.TryHandle(new TileFeatureVisualRequest(
                    TileFeatureVisualCueId.BarricadeActiveState,
                    100,
                    target.Cell,
                    TileFeatureKind.Barricade,
                    active: true)), Is.True);
                Assert.That(animator.GetBool("BarricadeActive"), Is.True);

                Assert.That(sink.TryHandle(new TileFeatureVisualRequest(
                    TileFeatureVisualCueId.ExitOpenState,
                    100,
                    target.Cell,
                    TileFeatureKind.Exit,
                    active: true)), Is.True);
                Assert.That(animator.GetBool("ExitOpen"), Is.True);

                Assert.That(sink.TryHandle(new TileFeatureVisualRequest(
                    TileFeatureVisualCueId.MoonBlockGenerated,
                    100,
                    target.Cell,
                    TileFeatureKind.MoonBlockGenerator,
                    targetEntityId: 20)), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(controller);
                Object.DestroyImmediate(barricadeProfile);
                Object.DestroyImmediate(exitProfile);
                Object.DestroyImmediate(moonProfile);
            }
        }

        private static AnimatorController CreateAnimatorController(string name)
        {
            var stateMachine = new AnimatorStateMachine
            {
                name = $"{name}_StateMachine",
            };
            stateMachine.AddState("Idle");

            var controller = new AnimatorController
            {
                name = $"{name}_Controller",
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
            controller.AddParameter("BarricadeActive", AnimatorControllerParameterType.Bool);
            controller.AddParameter("ExitOpen", AnimatorControllerParameterType.Bool);
            controller.AddParameter("MoonBlockGenerated", AnimatorControllerParameterType.Trigger);
            return controller;
        }

        private static TileFeatureVisualProfile CreateAnimatorProfile(
            TileFeatureKind kind,
            params TileFeatureVisualCueBinding[] bindings)
        {
            var profile = ScriptableObject.CreateInstance<TileFeatureVisualProfile>();
            var serializedProfile = new SerializedObject(profile);
            serializedProfile.FindProperty("featureKind").intValue = (int)kind;
            var bindingsProperty = serializedProfile.FindProperty("cueBindings");
            bindingsProperty.arraySize = bindings.Length;
            for (var i = 0; i < bindings.Length; i++)
            {
                WriteBinding(bindingsProperty.GetArrayElementAtIndex(i), bindings[i]);
            }

            serializedProfile.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }

        private static TileFeatureVisualCueBinding CreateBinding(
            TileFeatureVisualCueId cueId,
            TileFeatureAnimatorBindingKind kind,
            string parameterName)
        {
            return new TileFeatureVisualCueBinding
            {
                CueId = cueId,
                TargetSlot = TileFeatureVisualSlotId.Root,
                AnimatorBinding = new TileFeatureAnimatorBinding
                {
                    CueId = cueId,
                    Kind = kind,
                    ParameterOrStateName = parameterName,
                },
            };
        }

        internal static void SetProviderProfiles(
            TileFeatureVisualProfileProvider provider,
            params TileFeatureVisualProfile[] profiles)
        {
            var serializedProvider = new SerializedObject(provider);
            var profilesProperty = serializedProvider.FindProperty("profiles");
            profilesProperty.arraySize = profiles.Length;
            for (var i = 0; i < profiles.Length; i++)
            {
                profilesProperty.GetArrayElementAtIndex(i).objectReferenceValue = profiles[i];
            }

            serializedProvider.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WriteBinding(SerializedProperty property, TileFeatureVisualCueBinding binding)
        {
            property.FindPropertyRelative("CueId").intValue = (int)binding.CueId;
            property.FindPropertyRelative("TargetSlot").intValue = (int)binding.TargetSlot;
            WriteAnimatorBinding(property.FindPropertyRelative("AnimatorBinding"), binding.AnimatorBinding);
        }

        private static void WriteAnimatorBinding(
            SerializedProperty property,
            TileFeatureAnimatorBinding binding)
        {
            property.FindPropertyRelative("CueId").intValue = (int)binding.CueId;
            property.FindPropertyRelative("Kind").intValue = (int)binding.Kind;
            property.FindPropertyRelative("ParameterOrStateName").stringValue = binding.ParameterOrStateName;
        }
    }

    public sealed class TileFeatureVisualProfilePathOwnsMaterialBehaviorTests
    {
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int MetallicPropertyId = Shader.PropertyToID("_Metallic");

        [Test]
        [Category("Extended")]
        public void DestroyAndSlideMaterialState_IsHandledByProviderProfileSink()
        {
            var root = new GameObject(nameof(DestroyAndSlideMaterialState_IsHandledByProviderProfileSink));
            var destroyProfile = CreateMaterialProfile(TileFeatureKind.Destroy, Color.gray, 0.75f);
            var slideProfile = CreateMaterialProfile(TileFeatureKind.Slide, Color.cyan, 0.25f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));

            try
            {
                var target = root.AddComponent<TileFeatureVisualTargetView>();
                target.Configure(100, new SurfaceCell(FaceId.Floor, 1, 1));
                var renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { material };
                BindMaterialTarget(destroyProfile, renderer);
                BindMaterialTarget(slideProfile, renderer);
                var provider = root.AddComponent<TileFeatureVisualProfileProvider>();
                TileFeatureVisualProfilePathOwnsAnimatorBehaviorTests.SetProviderProfiles(
                    provider,
                    destroyProfile,
                    slideProfile);
                var sink = root.AddComponent<TileFeatureVisualProfileCueSink>();
                sink.Configure(target, provider);

                Assert.That(sink.TryHandle(new TileFeatureVisualRequest(
                    TileFeatureVisualCueId.DestroyTileActiveState,
                    100,
                    target.Cell,
                    TileFeatureKind.Destroy,
                    active: false)), Is.True);
                AssertMaterialState(renderer, Color.gray, 0.75f);

                Assert.That(sink.TryHandle(new TileFeatureVisualRequest(
                    TileFeatureVisualCueId.DestroyTileActiveState,
                    100,
                    target.Cell,
                    TileFeatureKind.Destroy,
                    active: true)), Is.True);
                AssertMaterialCleared(renderer, Color.gray, 0.75f);

                Assert.That(sink.TryHandle(new TileFeatureVisualRequest(
                    TileFeatureVisualCueId.SlideTileActiveState,
                    100,
                    target.Cell,
                    TileFeatureKind.Slide,
                    active: false)), Is.True);
                AssertMaterialState(renderer, Color.cyan, 0.25f);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(destroyProfile);
                Object.DestroyImmediate(slideProfile);
                Object.DestroyImmediate(material);
            }
        }

        private static TileFeatureVisualProfile CreateMaterialProfile(
            TileFeatureKind kind,
            Color inactiveColor,
            float inactiveMetallic)
        {
            var profile = ScriptableObject.CreateInstance<TileFeatureVisualProfile>();
            var serializedProfile = new SerializedObject(profile);
            serializedProfile.FindProperty("featureKind").intValue = (int)kind;
            var bindingsProperty = serializedProfile.FindProperty("cueBindings");
            bindingsProperty.arraySize = 1;
            var binding = bindingsProperty.GetArrayElementAtIndex(0);
            binding.FindPropertyRelative("CueId").intValue = kind == TileFeatureKind.Destroy
                ? (int)TileFeatureVisualCueId.DestroyTileActiveState
                : (int)TileFeatureVisualCueId.SlideTileActiveState;
            binding.FindPropertyRelative("TargetSlot").intValue = (int)TileFeatureVisualSlotId.Root;
            var targetsProperty = serializedProfile.FindProperty("inactiveMaterialTargets");
            targetsProperty.arraySize = 1;
            var targetProperty = targetsProperty.GetArrayElementAtIndex(0);
            targetProperty.FindPropertyRelative("MaterialIndex").intValue = 0;
            targetProperty.FindPropertyRelative("InactiveColor").colorValue = inactiveColor;
            targetProperty.FindPropertyRelative("InactiveMetallic").floatValue = inactiveMetallic;
            serializedProfile.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }

        private static void BindMaterialTarget(TileFeatureVisualProfile profile, Renderer renderer)
        {
            var serializedProfile = new SerializedObject(profile);
            serializedProfile
                .FindProperty("inactiveMaterialTargets")
                .GetArrayElementAtIndex(0)
                .FindPropertyRelative("Renderer")
                .objectReferenceValue = renderer;
            serializedProfile.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssertMaterialState(Renderer renderer, Color color, float metallic)
        {
            var propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock, 0);
            Assert.That(propertyBlock.GetColor(BaseColorPropertyId), Is.EqualTo(color));
            Assert.That(propertyBlock.GetFloat(MetallicPropertyId), Is.EqualTo(metallic));
        }

        private static void AssertMaterialCleared(Renderer renderer, Color color, float metallic)
        {
            var propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock, 0);
            Assert.That(propertyBlock.GetColor(BaseColorPropertyId), Is.Not.EqualTo(color));
            Assert.That(propertyBlock.GetFloat(MetallicPropertyId), Is.Not.EqualTo(metallic));
        }
    }

}
