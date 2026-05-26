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
            "Assets/_Features/Stages/Runtime/Presentation/BackgroundWallSurfaceTintProfile.cs",
            "Assets/_Features/Stages/Runtime/Presentation/BackgroundWallSurfaceTintAuthoring.cs",
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
                AssertColorApproximately(propertyBlock.GetColor(baseColorPropertyName), profile.FloorBaseColor);
                AssertColorApproximately(propertyBlock.GetColor(EmissionColorPropertyName), profile.FloorEmissionColor);
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
            }

            foreach (var path in RuntimeAssemblyDefinitionPaths)
            {
                var source = File.ReadAllText(path);
                Assert.That(source, Does.Not.Contain("Game.Feature.UI.HUD"), path);
                Assert.That(source, Does.Not.Contain("UI_HUD"), path);
            }
        }

        [Test]
        public void BoundWallPrefabs_UseExplicitTintTargetsWithEmissionKeywordEnabledMaterials()
        {
            var expectedProfile = AssetDatabase.LoadAssetAtPath<BackgroundWallSurfaceTintProfile>(ProfilePath);
            Assert.That(expectedProfile, Is.Not.Null, ProfilePath);

            foreach (var path in BoundWallPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);

                var authoring = prefab.GetComponent<BackgroundWallSurfaceTintAuthoring>();
                Assert.That(authoring, Is.Not.Null, path);
                Assert.That(authoring.Profile, Is.EqualTo(expectedProfile), path);
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
            bool preserveAlpha = true)
        {
            var target = new BackgroundWallSurfaceTintTarget();
            SetPrivateField(target, "renderer", renderer);
            SetPrivateField(target, "materialIndex", materialIndex);
            SetPrivateField(target, "applyBaseColor", applyBaseColor);
            SetPrivateField(target, "baseColorPropertyName", baseColorPropertyName);
            SetPrivateField(target, "applyEmission", applyEmission);
            SetPrivateField(target, "emissionColorPropertyName", emissionColorPropertyName);
            SetPrivateField(target, "preserveAlpha", preserveAlpha);
            return target;
        }

        private static void ConfigureAuthoring(
            BackgroundWallSurfaceTintAuthoring authoring,
            BackgroundWallSurfaceTintProfile profile,
            params BackgroundWallSurfaceTintTarget[] targets)
        {
            SetPrivateField(authoring, "profile", profile);
            SetPrivateField(authoring, "targets", targets);
        }

        private static void AssertColorApproximately(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.0001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.0001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.0001f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.0001f));
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }
    }
}
