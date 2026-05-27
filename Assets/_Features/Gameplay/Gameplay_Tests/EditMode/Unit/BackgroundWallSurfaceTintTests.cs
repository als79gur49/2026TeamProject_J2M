using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class BackgroundWallSurfaceTintTests
    {
        private const string EmissionKeyword = "_EMISSION";
        private const string EmissionColorPropertyName = "_EmissionColor";
        private const string ProfilePath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Background/Profiles/BackgroundWallSurfaceTintProfile.asset";

        private static readonly string[] RuntimeSourcePaths =
        {
            "Assets/_Features/Stages/Runtime/Presentation/BackgroundWallSurfaceNoiseProfile.cs",
            "Assets/_Features/Stages/Runtime/Presentation/BackgroundWallSurfaceTintProfile.cs",
            "Assets/_Features/Stages/Runtime/Presentation/BackgroundWallSurfaceTintAuthoring.cs",
            "Assets/_Features/Stages/Runtime/Presentation/BackgroundWallSurfaceTintNoiseController.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/BackgroundWallSurfaceTintPresenterAdapter.cs",
        };

        private static readonly string[] RuntimeAssemblyDefinitionPaths =
        {
            "Assets/_Features/Stages/Stages.asmdef",
            "Assets/_Features/Gameplay/Gameplay_Host/Gameplay.Host.asmdef",
        };

        private static readonly string[] BoundWallPrefabPaths =
        {
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Background/Prefabs/WallRoot_Default.prefab",
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Background/Prefabs/WallRoot_AGate.prefab",
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Background/Prefabs/WallRoot_BGate.prefab",
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Background/Prefabs/WallRoot_Lv4Gate.prefab",
        };

        private const string NoiseProfilePath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Background/Profiles/BackgroundWallSurfaceNoiseProfile.asset";

        [Test]
        public void Profile_ReturnsSemanticBaseColors()
        {
            var profile = CreateProfile();
            try
            {
                Assert.That(profile.TryGetBaseColor(FaceId.Floor, out var floor), Is.True);
                Assert.That(profile.TryGetBaseColor(FaceId.Front, out var front), Is.True);
                Assert.That(profile.TryGetBaseColor(FaceId.Ceiling, out var ceiling), Is.True);
                Assert.That(profile.TryGetBaseColor(FaceId.Back, out var back), Is.True);

                Assert.That(floor, Is.EqualTo(new Color(0.1f, 0.2f, 0.3f, 0.4f)));
                Assert.That(front, Is.EqualTo(new Color(0.2f, 0.3f, 0.4f, 0.5f)));
                Assert.That(ceiling, Is.EqualTo(new Color(0.3f, 0.4f, 0.5f, 0.6f)));
                Assert.That(back, Is.EqualTo(new Color(0.4f, 0.5f, 0.6f, 0.7f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Profile_ReturnsSemanticEmissionColors()
        {
            var profile = CreateProfile();
            try
            {
                Assert.That(profile.TryGetEmissionColor(FaceId.Floor, out var floor), Is.True);
                Assert.That(profile.TryGetEmissionColor(FaceId.Front, out var front), Is.True);
                Assert.That(profile.TryGetEmissionColor(FaceId.Ceiling, out var ceiling), Is.True);
                Assert.That(profile.TryGetEmissionColor(FaceId.Back, out var back), Is.True);

                Assert.That(floor, Is.EqualTo(new Color(0.5f, 0.6f, 0.7f, 0.8f)));
                Assert.That(front, Is.EqualTo(new Color(0.6f, 0.7f, 0.8f, 0.9f)));
                Assert.That(ceiling, Is.EqualTo(new Color(0.7f, 0.8f, 0.9f, 1f)));
                Assert.That(back, Is.EqualTo(new Color(0.8f, 0.9f, 1f, 0.9f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ApplyFaceTint_UsesMaterialPropertyBlockForBaseAndEmissionWithoutMutatingMaterial()
        {
            var root = new GameObject(nameof(ApplyFaceTint_UsesMaterialPropertyBlockForBaseAndEmissionWithoutMutatingMaterial));
            var renderer = root.AddComponent<MeshRenderer>();
            var material = CreateTintMaterial("Tint Material", out var baseColorPropertyName);
            var profile = CreateProfile(preserveMaterialAlpha: false);
            var authoring = root.AddComponent<BackgroundWallSurfaceTintAuthoring>();

            try
            {
                material.EnableKeyword(EmissionKeyword);
                material.SetColor(baseColorPropertyName, new Color(0.9f, 0.8f, 0.7f, 0.6f));
                material.SetColor(EmissionColorPropertyName, Color.black);
                renderer.sharedMaterials = new[] { material };
                ConfigureAuthoring(
                    authoring,
                    profile,
                    CreateTarget(renderer, baseColorPropertyName: baseColorPropertyName, preserveAlpha: false));

                var originalBaseColor = material.GetColor(baseColorPropertyName);
                var originalEmissionColor = material.GetColor(EmissionColorPropertyName);

                authoring.ApplyFaceTint(FaceId.Floor);

                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock, 0);
                AssertRgbApproximately(propertyBlock.GetColor(baseColorPropertyName), profile.FloorBaseColor);
                AssertRgbApproximately(propertyBlock.GetColor(EmissionColorPropertyName), profile.FloorEmissionColor);
                Assert.That(material.GetColor(baseColorPropertyName), Is.EqualTo(originalBaseColor));
                Assert.That(material.GetColor(EmissionColorPropertyName), Is.EqualTo(originalEmissionColor));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyFaceTint_WhenPreserveMaterialAlphaIsTrue_KeepsMaterialAlphaInPropertyBlock()
        {
            var root = new GameObject(nameof(ApplyFaceTint_WhenPreserveMaterialAlphaIsTrue_KeepsMaterialAlphaInPropertyBlock));
            var renderer = root.AddComponent<MeshRenderer>();
            var material = CreateTintMaterial("Transparent Tint Material", out var baseColorPropertyName);
            var profile = CreateProfile(preserveMaterialAlpha: true);
            var authoring = root.AddComponent<BackgroundWallSurfaceTintAuthoring>();

            try
            {
                const float materialAlpha = 0.25f;
                material.EnableKeyword(EmissionKeyword);
                material.SetColor(baseColorPropertyName, new Color(1f, 1f, 1f, materialAlpha));
                renderer.sharedMaterials = new[] { material };
                ConfigureAuthoring(authoring, profile, CreateTarget(renderer, baseColorPropertyName: baseColorPropertyName));

                authoring.ApplyFaceTint(FaceId.Floor);

                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock, 0);
                var applied = propertyBlock.GetColor(baseColorPropertyName);
                Assert.That(applied.r, Is.EqualTo(profile.FloorBaseColor.r).Within(0.0001f));
                Assert.That(applied.g, Is.EqualTo(profile.FloorBaseColor.g).Within(0.0001f));
                Assert.That(applied.b, Is.EqualTo(profile.FloorBaseColor.b).Within(0.0001f));
                Assert.That(applied.a, Is.EqualTo(materialAlpha).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ResolveTintFace_UsesCurrentBottomWhenStableAndDestinationBottomDuringTransition()
        {
            var current = new CubeTopologyState(FaceId.Back);
            var inactive = TopologyTransitionVisualState.Inactive(current, Quaternion.identity);
            var transition = new TopologyTransitionVisualState(
                isActive: true,
                progress01: 0.5f,
                sourceTopology: current,
                destinationTopology: new CubeTopologyState(FaceId.Front),
                rotationKind: CubeRotationKind.Forward,
                durationSeconds: 1f,
                presentedVisualRotation: Quaternion.identity,
                angularVelocityNormalized: 1f);

            Assert.That(BackgroundWallSurfaceTintPresenterAdapter.ResolveTintFace(current, inactive), Is.EqualTo(FaceId.Back));
            Assert.That(BackgroundWallSurfaceTintPresenterAdapter.ResolveTintFace(current, transition), Is.EqualTo(FaceId.Front));
        }

        [Test]
        public void ApplyFaceTint_WhenEmissionKeywordIsDisabled_WarnsAndSkipsEmissionProperty()
        {
            var root = new GameObject(nameof(ApplyFaceTint_WhenEmissionKeywordIsDisabled_WarnsAndSkipsEmissionProperty));
            var renderer = root.AddComponent<MeshRenderer>();
            var material = CreateTintMaterial("Emission Disabled Material", out var baseColorPropertyName);
            var profile = CreateProfile();
            var authoring = root.AddComponent<BackgroundWallSurfaceTintAuthoring>();

            try
            {
                material.DisableKeyword(EmissionKeyword);
                renderer.sharedMaterials = new[] { material };
                ConfigureAuthoring(authoring, profile, CreateTarget(renderer, baseColorPropertyName: baseColorPropertyName));

                LogAssert.Expect(LogType.Warning, new Regex("does not enable _EMISSION"));
                authoring.ApplyFaceTint(FaceId.Floor);

                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock, 0);
                Assert.That(propertyBlock.GetColor(EmissionColorPropertyName), Is.Not.EqualTo(profile.FloorEmissionColor));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NoiseController_InitialStableApply_DoesNotStartOverlay()
        {
            var root = new GameObject(nameof(NoiseController_InitialStableApply_DoesNotStartOverlay));
            var renderer = root.AddComponent<MeshRenderer>();
            var material = CreateTintMaterial("Initial Stable Noise Material", out var baseColorPropertyName);
            var profile = CreateProfile(preserveMaterialAlpha: false);
            var noiseProfile = CreateNoiseProfile();
            var authoring = root.AddComponent<BackgroundWallSurfaceTintAuthoring>();

            try
            {
                material.EnableKeyword(EmissionKeyword);
                renderer.sharedMaterials = new[] { material };
                ConfigureAuthoring(
                    authoring,
                    profile,
                    noiseProfile,
                    CreateTarget(renderer, baseColorPropertyName: baseColorPropertyName, preserveAlpha: false));

                var controller = new BackgroundWallSurfaceTintNoiseController(authoring);
                controller.ApplyInitialStableFace(FaceId.Floor);

                Assert.That(controller.IsOverlayActive, Is.False);
                Assert.That(controller.DebugRestartSequence, Is.EqualTo(0));
                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock, 0);
                AssertColorApproximately(propertyBlock.GetColor(baseColorPropertyName), profile.FloorBaseColor);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(noiseProfile);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NoiseController_TransitionDestinationFaceChange_StartsAndDoesNotRestartForRepeatedFace()
        {
            var root = new GameObject(nameof(NoiseController_TransitionDestinationFaceChange_StartsAndDoesNotRestartForRepeatedFace));
            var renderer = root.AddComponent<MeshRenderer>();
            var material = CreateTintMaterial("Repeated Face Noise Material", out var baseColorPropertyName);
            var profile = CreateProfile();
            var noiseProfile = CreateNoiseProfile(durationSeconds: 1f);
            var authoring = root.AddComponent<BackgroundWallSurfaceTintAuthoring>();

            try
            {
                material.EnableKeyword(EmissionKeyword);
                renderer.sharedMaterials = new[] { material };
                ConfigureAuthoring(
                    authoring,
                    profile,
                    noiseProfile,
                    CreateTarget(renderer, baseColorPropertyName: baseColorPropertyName));

                var controller = new BackgroundWallSurfaceTintNoiseController(authoring);
                controller.ApplyInitialStableFace(FaceId.Floor);
                controller.ObservePresentationFace(FaceId.Front, isTopologyTransitionActive: true, topologyTransitionDurationSeconds: 1f);
                Assert.That(controller.DebugActiveSourceFace, Is.EqualTo(FaceId.Floor));
                controller.Advance(0.25f);
                var elapsedAfterAdvance = controller.DebugElapsedSeconds;
                var restartSequence = controller.DebugRestartSequence;

                controller.ObservePresentationFace(FaceId.Front, isTopologyTransitionActive: true, topologyTransitionDurationSeconds: 1f);

                Assert.That(controller.IsOverlayActive, Is.True);
                Assert.That(controller.DebugActiveFace, Is.EqualTo(FaceId.Front));
                Assert.That(controller.DebugRestartSequence, Is.EqualTo(restartSequence));
                Assert.That(controller.DebugElapsedSeconds, Is.EqualTo(elapsedAfterAdvance).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(noiseProfile);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NoiseController_DifferentDestinationFace_RestartsOverlay()
        {
            var root = new GameObject(nameof(NoiseController_DifferentDestinationFace_RestartsOverlay));
            var renderer = root.AddComponent<MeshRenderer>();
            var material = CreateTintMaterial("Restart Noise Material", out var baseColorPropertyName);
            var profile = CreateProfile();
            var noiseProfile = CreateNoiseProfile(durationSeconds: 1f);
            var authoring = root.AddComponent<BackgroundWallSurfaceTintAuthoring>();

            try
            {
                material.EnableKeyword(EmissionKeyword);
                renderer.sharedMaterials = new[] { material };
                ConfigureAuthoring(
                    authoring,
                    profile,
                    noiseProfile,
                    CreateTarget(renderer, baseColorPropertyName: baseColorPropertyName));

                var controller = new BackgroundWallSurfaceTintNoiseController(authoring);
                controller.ApplyInitialStableFace(FaceId.Floor);
                controller.ObservePresentationFace(FaceId.Front, isTopologyTransitionActive: true, topologyTransitionDurationSeconds: 1f);
                controller.Advance(0.25f);
                var restartSequence = controller.DebugRestartSequence;

                controller.ObservePresentationFace(FaceId.Back, isTopologyTransitionActive: true, topologyTransitionDurationSeconds: 1f);

                Assert.That(controller.IsOverlayActive, Is.True);
                Assert.That(controller.DebugActiveSourceFace, Is.EqualTo(FaceId.Front));
                Assert.That(controller.DebugActiveFace, Is.EqualTo(FaceId.Back));
                Assert.That(controller.DebugRestartSequence, Is.GreaterThan(restartSequence));
                Assert.That(controller.DebugElapsedSeconds, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(noiseProfile);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NoiseController_DuringDuration_ChangesBaseAndEmissionThenReturnsToProfileColors()
        {
            var root = new GameObject(nameof(NoiseController_DuringDuration_ChangesBaseAndEmissionThenReturnsToProfileColors));
            var renderer = root.AddComponent<MeshRenderer>();
            var material = CreateTintMaterial("Duration Noise Material", out var baseColorPropertyName);
            var profile = CreateProfile(preserveMaterialAlpha: false);
            var noiseProfile = CreateNoiseProfile(durationSeconds: 1f);
            var authoring = root.AddComponent<BackgroundWallSurfaceTintAuthoring>();

            try
            {
                material.EnableKeyword(EmissionKeyword);
                renderer.sharedMaterials = new[] { material };
                ConfigureAuthoring(
                    authoring,
                    profile,
                    noiseProfile,
                    CreateTarget(renderer, baseColorPropertyName: baseColorPropertyName, preserveAlpha: false));

                var controller = new BackgroundWallSurfaceTintNoiseController(authoring);
                controller.ApplyInitialStableFace(FaceId.Floor);
                controller.ObservePresentationFace(FaceId.Front, isTopologyTransitionActive: true, topologyTransitionDurationSeconds: 1f);

                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock, 0);
                AssertRgbApproximately(propertyBlock.GetColor(baseColorPropertyName), profile.FloorBaseColor);
                AssertRgbApproximately(propertyBlock.GetColor(EmissionColorPropertyName), profile.FloorEmissionColor);

                controller.Advance(0.5f);

                renderer.GetPropertyBlock(propertyBlock, 0);
                AssertRgbDifferent(propertyBlock.GetColor(baseColorPropertyName), profile.FloorBaseColor);
                AssertRgbDifferent(propertyBlock.GetColor(baseColorPropertyName), profile.FrontBaseColor);
                AssertRgbDifferent(propertyBlock.GetColor(EmissionColorPropertyName), profile.FloorEmissionColor);
                AssertRgbDifferent(propertyBlock.GetColor(EmissionColorPropertyName), profile.FrontEmissionColor);

                controller.Advance(0.5f);

                renderer.GetPropertyBlock(propertyBlock, 0);
                AssertColorApproximately(propertyBlock.GetColor(baseColorPropertyName), profile.FrontBaseColor);
                AssertColorApproximately(propertyBlock.GetColor(EmissionColorPropertyName), profile.FrontEmissionColor);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(noiseProfile);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NoiseController_PreserveMaterialAlpha_KeepsBaseAlphaDuringNoise()
        {
            var root = new GameObject(nameof(NoiseController_PreserveMaterialAlpha_KeepsBaseAlphaDuringNoise));
            var renderer = root.AddComponent<MeshRenderer>();
            var material = CreateTintMaterial("Alpha Noise Material", out var baseColorPropertyName);
            var profile = CreateProfile(preserveMaterialAlpha: true);
            var noiseProfile = CreateNoiseProfile(durationSeconds: 1f);
            var authoring = root.AddComponent<BackgroundWallSurfaceTintAuthoring>();

            try
            {
                const float materialAlpha = 0.23f;
                material.EnableKeyword(EmissionKeyword);
                material.SetColor(baseColorPropertyName, new Color(1f, 1f, 1f, materialAlpha));
                renderer.sharedMaterials = new[] { material };
                ConfigureAuthoring(
                    authoring,
                    profile,
                    noiseProfile,
                    CreateTarget(renderer, baseColorPropertyName: baseColorPropertyName));

                var controller = new BackgroundWallSurfaceTintNoiseController(authoring);
                controller.ApplyInitialStableFace(FaceId.Floor);
                controller.ObservePresentationFace(FaceId.Front, isTopologyTransitionActive: true, topologyTransitionDurationSeconds: 1f);
                controller.Advance(0.5f);

                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock, 0);
                Assert.That(propertyBlock.GetColor(baseColorPropertyName).a, Is.EqualTo(materialAlpha).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(noiseProfile);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyFaceTint_WithNoisePolicyDisabled_PerTargetChannelsRemainProfileColors()
        {
            var root = new GameObject(nameof(ApplyFaceTint_WithNoisePolicyDisabled_PerTargetChannelsRemainProfileColors));
            var renderer = root.AddComponent<MeshRenderer>();
            var material = CreateTintMaterial("Policy Noise Material", out var baseColorPropertyName);
            var profile = CreateProfile(preserveMaterialAlpha: false);
            var noiseProfile = CreateNoiseProfile(durationSeconds: 1f);
            var authoring = root.AddComponent<BackgroundWallSurfaceTintAuthoring>();

            try
            {
                material.EnableKeyword(EmissionKeyword);
                renderer.sharedMaterials = new[] { material };
                ConfigureAuthoring(
                    authoring,
                    profile,
                    noiseProfile,
                    CreateTarget(
                        renderer,
                        baseColorPropertyName: baseColorPropertyName,
                        preserveAlpha: false,
                        noiseAffectsBaseColor: false,
                        noiseAffectsEmission: false));

                authoring.ApplyFaceTint(
                    FaceId.Front,
                    BackgroundWallSurfaceNoiseOverlay.Active(noiseProfile, FaceId.Floor, 0.5f, 1f, sequence: 1));

                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock, 0);
                AssertColorApproximately(propertyBlock.GetColor(baseColorPropertyName), profile.FrontBaseColor);
                AssertColorApproximately(propertyBlock.GetColor(EmissionColorPropertyName), profile.FrontEmissionColor);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(noiseProfile);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyFaceTint_WhenApplyEmissionIsFalse_DoesNotApplyEmissionNoise()
        {
            var root = new GameObject(nameof(ApplyFaceTint_WhenApplyEmissionIsFalse_DoesNotApplyEmissionNoise));
            var renderer = root.AddComponent<MeshRenderer>();
            var material = CreateTintMaterial("No Emission Noise Material", out var baseColorPropertyName);
            var profile = CreateProfile(preserveMaterialAlpha: false);
            var noiseProfile = CreateNoiseProfile(durationSeconds: 1f);
            var authoring = root.AddComponent<BackgroundWallSurfaceTintAuthoring>();

            try
            {
                material.EnableKeyword(EmissionKeyword);
                renderer.sharedMaterials = new[] { material };
                ConfigureAuthoring(
                    authoring,
                    profile,
                    noiseProfile,
                    CreateTarget(
                        renderer,
                        baseColorPropertyName: baseColorPropertyName,
                        applyEmission: false,
                        preserveAlpha: false));

                authoring.ApplyFaceTint(
                    FaceId.Front,
                    BackgroundWallSurfaceNoiseOverlay.Active(noiseProfile, FaceId.Floor, 0.5f, 1f, sequence: 1));

                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock, 0);
                AssertRgbDifferent(propertyBlock.GetColor(baseColorPropertyName), profile.FrontBaseColor);
                Assert.That(propertyBlock.GetColor(EmissionColorPropertyName), Is.Not.EqualTo(profile.FrontEmissionColor));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(noiseProfile);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyFaceTint_WhenEmissionKeywordDisabledDuringNoise_WarnsAndSkipsEmission()
        {
            var root = new GameObject(nameof(ApplyFaceTint_WhenEmissionKeywordDisabledDuringNoise_WarnsAndSkipsEmission));
            var renderer = root.AddComponent<MeshRenderer>();
            var material = CreateTintMaterial("Emission Disabled Noise Material", out var baseColorPropertyName);
            var profile = CreateProfile();
            var noiseProfile = CreateNoiseProfile(durationSeconds: 1f);
            var authoring = root.AddComponent<BackgroundWallSurfaceTintAuthoring>();

            try
            {
                material.DisableKeyword(EmissionKeyword);
                renderer.sharedMaterials = new[] { material };
                ConfigureAuthoring(
                    authoring,
                    profile,
                    noiseProfile,
                    CreateTarget(renderer, baseColorPropertyName: baseColorPropertyName));

                LogAssert.Expect(LogType.Warning, new Regex("does not enable _EMISSION"));
                authoring.ApplyFaceTint(
                    FaceId.Front,
                    BackgroundWallSurfaceNoiseOverlay.Active(noiseProfile, FaceId.Floor, 0.5f, 1f, sequence: 1));

                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock, 0);
                Assert.That(propertyBlock.GetColor(EmissionColorPropertyName), Is.Not.EqualTo(profile.FrontEmissionColor));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(noiseProfile);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyFaceTint_WithNoise_MaterialSlotsUseIndependentPropertyBlocks()
        {
            var root = new GameObject(nameof(ApplyFaceTint_WithNoise_MaterialSlotsUseIndependentPropertyBlocks));
            var renderer = root.AddComponent<MeshRenderer>();
            var firstMaterial = CreateTintMaterial("Slot 0 Material", out var baseColorPropertyName);
            var secondMaterial = CreateTintMaterial("Slot 1 Material", out _);
            var profile = CreateProfile(preserveMaterialAlpha: false);
            var noiseProfile = CreateNoiseProfile(durationSeconds: 1f);
            var authoring = root.AddComponent<BackgroundWallSurfaceTintAuthoring>();

            try
            {
                firstMaterial.EnableKeyword(EmissionKeyword);
                secondMaterial.EnableKeyword(EmissionKeyword);
                renderer.sharedMaterials = new[] { firstMaterial, secondMaterial };
                ConfigureAuthoring(
                    authoring,
                    profile,
                    noiseProfile,
                    CreateTarget(renderer, materialIndex: 0, baseColorPropertyName: baseColorPropertyName, preserveAlpha: false),
                    CreateTarget(
                        renderer,
                        materialIndex: 1,
                        baseColorPropertyName: baseColorPropertyName,
                        preserveAlpha: false,
                        hasBaseNoiseStrengthOverride: true,
                        baseNoiseStrengthOverride: 0f,
                        hasEmissionNoiseStrengthOverride: true,
                        emissionNoiseStrengthOverride: 0f));

                authoring.ApplyFaceTint(
                    FaceId.Front,
                    BackgroundWallSurfaceNoiseOverlay.Active(noiseProfile, FaceId.Floor, 0.5f, 1f, sequence: 1));

                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock, 0);
                AssertRgbDifferent(propertyBlock.GetColor(baseColorPropertyName), profile.FrontBaseColor);
                renderer.GetPropertyBlock(propertyBlock, 1);
                AssertColorApproximately(propertyBlock.GetColor(baseColorPropertyName), profile.FrontBaseColor);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(noiseProfile);
                UnityEngine.Object.DestroyImmediate(firstMaterial);
                UnityEngine.Object.DestroyImmediate(secondMaterial);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NoiseController_Reset_ClearsOverlayAndReappliesBaseTint()
        {
            var root = new GameObject(nameof(NoiseController_Reset_ClearsOverlayAndReappliesBaseTint));
            var renderer = root.AddComponent<MeshRenderer>();
            var material = CreateTintMaterial("Reset Noise Material", out var baseColorPropertyName);
            var profile = CreateProfile(preserveMaterialAlpha: false);
            var noiseProfile = CreateNoiseProfile(durationSeconds: 1f);
            var authoring = root.AddComponent<BackgroundWallSurfaceTintAuthoring>();

            try
            {
                material.EnableKeyword(EmissionKeyword);
                renderer.sharedMaterials = new[] { material };
                ConfigureAuthoring(
                    authoring,
                    profile,
                    noiseProfile,
                    CreateTarget(renderer, baseColorPropertyName: baseColorPropertyName, preserveAlpha: false));

                var controller = new BackgroundWallSurfaceTintNoiseController(authoring);
                controller.ApplyInitialStableFace(FaceId.Floor);
                controller.ObservePresentationFace(FaceId.Front, isTopologyTransitionActive: true, topologyTransitionDurationSeconds: 1f);
                controller.Advance(0.5f);
                controller.Reset();

                Assert.That(controller.IsOverlayActive, Is.False);
                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock, 0);
                AssertColorApproximately(propertyBlock.GetColor(baseColorPropertyName), profile.FrontBaseColor);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(noiseProfile);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyFaceTint_WithMissingRendererOrMaterialIndex_WarnsAndNoOps()
        {
            var root = new GameObject(nameof(ApplyFaceTint_WithMissingRendererOrMaterialIndex_WarnsAndNoOps));
            var renderer = root.AddComponent<MeshRenderer>();
            var material = CreateTintMaterial("Missing Target Material", out var baseColorPropertyName);
            var profile = CreateProfile();
            var authoring = root.AddComponent<BackgroundWallSurfaceTintAuthoring>();

            try
            {
                renderer.sharedMaterials = new[] { material };
                ConfigureAuthoring(
                    authoring,
                    profile,
                    CreateTarget(null, baseColorPropertyName: baseColorPropertyName),
                    CreateTarget(renderer, materialIndex: 3, baseColorPropertyName: baseColorPropertyName));

                LogAssert.Expect(LogType.Warning, new Regex("has no renderer"));
                LogAssert.Expect(LogType.Warning, new Regex("material index 3 is out of range"));

                Assert.DoesNotThrow(() => authoring.ApplyFaceTint(FaceId.Floor));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RuntimeTintCode_DoesNotReferenceHudStyleProfileOrHudAssembly()
        {
            foreach (var path in RuntimeSourcePaths)
            {
                var source = File.ReadAllText(path);
                Assert.That(source, Does.Not.Contain("SurfaceBeltStyleProfile"), path);
                Assert.That(source, Does.Not.Contain("Game.Feature.UI.HUD"), path);
                Assert.That(source, Does.Not.Contain("UI_HUD"), path);
                Assert.That(source, Does.Not.Contain("_GravityField"), path);
                Assert.That(source, Does.Not.Contain("_Inactive"), path);
                Assert.That(source, Does.Not.Contain("EnemyInactiveVisual"), path);
                Assert.That(source, Does.Not.Contain("Renderer.material"), path);
                Assert.That(source, Does.Not.Contain("NoiseColor"), path);
                Assert.That(source, Does.Not.Contain("noiseColor"), path);
            }

            foreach (var path in RuntimeAssemblyDefinitionPaths)
            {
                var source = File.ReadAllText(path);
                Assert.That(source, Does.Not.Contain("Game.Feature.UI.HUD"), path);
                Assert.That(source, Does.Not.Contain("UI_HUD"), path);
            }
        }

        [Test]
        public void RuntimeNoiseCode_DoesNotWriteAuthoritativeGameplayOrDeterminismState()
        {
            foreach (var path in RuntimeSourcePaths)
            {
                var source = File.ReadAllText(path);
                Assert.That(source, Does.Not.Contain("WorldState"), path);
                Assert.That(source, Does.Not.Contain("TickResult"), path);
                Assert.That(source, Does.Not.Contain("Determinism"), path);
                Assert.That(source, Does.Not.Contain("Hash"), path);
            }
        }

        [Test]
        public void BoundWallPrefabs_UseExplicitTintTargetsWithEmissionKeywordEnabledMaterials()
        {
            var expectedProfile = AssetDatabase.LoadAssetAtPath<BackgroundWallSurfaceTintProfile>(ProfilePath);
            Assert.That(expectedProfile, Is.Not.Null, ProfilePath);
            var expectedNoiseProfile = AssetDatabase.LoadAssetAtPath<BackgroundWallSurfaceNoiseProfile>(NoiseProfilePath);
            Assert.That(expectedNoiseProfile, Is.Not.Null, NoiseProfilePath);
            Assert.That(expectedNoiseProfile.Enabled, Is.True, NoiseProfilePath);
            Assert.That(expectedNoiseProfile.SeedMode, Is.EqualTo(BackgroundWallSurfaceNoiseSeedMode.FaceAndTarget), NoiseProfilePath);
            var noiseProfileAssetText = File.ReadAllText(NoiseProfilePath);
            Assert.That(noiseProfileAssetText, Does.Not.Contain("noiseColor"), NoiseProfilePath);

            foreach (var path in BoundWallPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);

                var authoring = prefab.GetComponent<BackgroundWallSurfaceTintAuthoring>();
                Assert.That(authoring, Is.Not.Null, path);
                Assert.That(authoring.Profile, Is.EqualTo(expectedProfile), path);
                Assert.That(authoring.NoiseProfile, Is.EqualTo(expectedNoiseProfile), path);
                Assert.That(authoring.Targets, Is.Not.Empty, path);

                foreach (var target in authoring.Targets)
                {
                    Assert.That(target.Renderer, Is.Not.Null, path);
                    var materials = target.Renderer.sharedMaterials;
                    Assert.That(target.MaterialIndex, Is.GreaterThanOrEqualTo(0), path);
                    Assert.That(target.MaterialIndex, Is.LessThan(materials.Length), path);
                    var material = materials[target.MaterialIndex];
                    Assert.That(material, Is.Not.Null, path);
                    Assert.That(material.HasProperty(target.BaseColorPropertyName), Is.True, $"{path}:{material.name}");
                    Assert.That(material.HasProperty(target.EmissionColorPropertyName), Is.True, $"{path}:{material.name}");
                    Assert.That(target.ApplyNoise, Is.True, path);

                    if (target.ApplyEmission)
                    {
                        Assert.That(
                            material.IsKeywordEnabled(EmissionKeyword),
                            Is.True,
                            $"{path}:{target.Renderer.name}[{target.MaterialIndex}] '{material.name}' must enable {EmissionKeyword}.");
                    }
                }
            }
        }

        private static BackgroundWallSurfaceTintProfile CreateProfile(bool preserveMaterialAlpha = true)
        {
            var profile = ScriptableObject.CreateInstance<BackgroundWallSurfaceTintProfile>();
            SetPrivateField(profile, "floorBaseColor", new Color(0.1f, 0.2f, 0.3f, 0.4f));
            SetPrivateField(profile, "frontBaseColor", new Color(0.2f, 0.3f, 0.4f, 0.5f));
            SetPrivateField(profile, "ceilingBaseColor", new Color(0.3f, 0.4f, 0.5f, 0.6f));
            SetPrivateField(profile, "backBaseColor", new Color(0.4f, 0.5f, 0.6f, 0.7f));
            SetPrivateField(profile, "floorEmissionColor", new Color(0.5f, 0.6f, 0.7f, 0.8f));
            SetPrivateField(profile, "frontEmissionColor", new Color(0.6f, 0.7f, 0.8f, 0.9f));
            SetPrivateField(profile, "ceilingEmissionColor", new Color(0.7f, 0.8f, 0.9f, 1f));
            SetPrivateField(profile, "backEmissionColor", new Color(0.8f, 0.9f, 1f, 0.9f));
            SetPrivateField(profile, "preserveMaterialAlpha", preserveMaterialAlpha);
            return profile;
        }

        private static BackgroundWallSurfaceNoiseProfile CreateNoiseProfile(
            bool enabled = true,
            float durationSeconds = 0.5f,
            bool useTopologyTransitionDuration = true)
        {
            var profile = ScriptableObject.CreateInstance<BackgroundWallSurfaceNoiseProfile>();
            SetPrivateField(profile, "enabled", enabled);
            SetPrivateField(profile, "durationSeconds", durationSeconds);
            SetPrivateField(profile, "useTopologyTransitionDuration", useTopologyTransitionDuration);
            SetPrivateField(profile, "noiseFrequency", 8f);
            SetPrivateField(profile, "noiseStrength", 1f);
            SetPrivateField(profile, "baseColorNoiseAmount", 0.2f);
            SetPrivateField(profile, "emissionNoiseAmount", 0.2f);
            SetPrivateField(
                profile,
                "progressCurve",
                new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(1f, 1f)));
            SetPrivateField(profile, "seedMode", BackgroundWallSurfaceNoiseSeedMode.FaceAndTarget);
            SetPrivateField(profile, "seed", 137);
            return profile;
        }

        private static Material CreateTintMaterial(string materialName, out string baseColorPropertyName)
        {
            foreach (var candidate in new[]
                     {
                         ("Universal Render Pipeline/Lit", "_BaseColor"),
                         ("Standard", "_Color"),
                     })
            {
                var shader = Shader.Find(candidate.Item1);
                if (shader == null)
                {
                    continue;
                }

                var material = new Material(shader)
                {
                    name = materialName,
                };

                if (material.HasProperty(candidate.Item2) && material.HasProperty(EmissionColorPropertyName))
                {
                    baseColorPropertyName = candidate.Item2;
                    return material;
                }

                UnityEngine.Object.DestroyImmediate(material);
            }

            Assert.Fail("Expected a test shader with base color and emission color properties.");
            baseColorPropertyName = "_Color";
            return null;
        }

        private static BackgroundWallSurfaceTintTarget CreateTarget(
            Renderer renderer,
            int materialIndex = 0,
            bool applyBaseColor = true,
            string baseColorPropertyName = "_Color",
            bool applyEmission = true,
            string emissionColorPropertyName = EmissionColorPropertyName,
            bool preserveAlpha = true,
            bool applyNoise = true,
            bool noiseAffectsBaseColor = true,
            bool noiseAffectsEmission = true,
            bool hasBaseNoiseStrengthOverride = false,
            float baseNoiseStrengthOverride = 1f,
            bool hasEmissionNoiseStrengthOverride = false,
            float emissionNoiseStrengthOverride = 1f)
        {
            var target = new BackgroundWallSurfaceTintTarget();
            SetPrivateField(target, "renderer", renderer);
            SetPrivateField(target, "materialIndex", materialIndex);
            SetPrivateField(target, "applyBaseColor", applyBaseColor);
            SetPrivateField(target, "baseColorPropertyName", baseColorPropertyName);
            SetPrivateField(target, "applyEmission", applyEmission);
            SetPrivateField(target, "emissionColorPropertyName", emissionColorPropertyName);
            SetPrivateField(target, "preserveAlpha", preserveAlpha);
            SetPrivateField(target, "applyNoise", applyNoise);
            SetPrivateField(target, "noiseAffectsBaseColor", noiseAffectsBaseColor);
            SetPrivateField(target, "noiseAffectsEmission", noiseAffectsEmission);
            SetPrivateField(target, "hasBaseNoiseStrengthOverride", hasBaseNoiseStrengthOverride);
            SetPrivateField(target, "baseNoiseStrengthOverride", baseNoiseStrengthOverride);
            SetPrivateField(target, "hasEmissionNoiseStrengthOverride", hasEmissionNoiseStrengthOverride);
            SetPrivateField(target, "emissionNoiseStrengthOverride", emissionNoiseStrengthOverride);
            return target;
        }

        private static void ConfigureAuthoring(
            BackgroundWallSurfaceTintAuthoring authoring,
            BackgroundWallSurfaceTintProfile profile,
            params BackgroundWallSurfaceTintTarget[] targets)
        {
            ConfigureAuthoring(authoring, profile, null, targets);
        }

        private static void ConfigureAuthoring(
            BackgroundWallSurfaceTintAuthoring authoring,
            BackgroundWallSurfaceTintProfile profile,
            BackgroundWallSurfaceNoiseProfile noiseProfile,
            params BackgroundWallSurfaceTintTarget[] targets)
        {
            SetPrivateField(authoring, "profile", profile);
            SetPrivateField(authoring, "noiseProfile", noiseProfile);
            SetPrivateField(authoring, "targets", targets);
        }

        private static void AssertColorApproximately(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.0001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.0001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.0001f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.0001f));
        }

        private static void AssertRgbApproximately(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.0001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.0001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.0001f));
        }

        private static void AssertRgbDifferent(Color actual, Color expected)
        {
            var difference =
                Mathf.Abs(actual.r - expected.r) +
                Mathf.Abs(actual.g - expected.g) +
                Mathf.Abs(actual.b - expected.b);
            Assert.That(difference, Is.GreaterThan(0.0001f), $"Expected RGB to differ. Actual={actual}, Expected={expected}");
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }
    }
}
