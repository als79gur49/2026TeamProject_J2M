using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EnemyInactiveMaterialAuthoringTests
    {
        private const string PrefabRoot =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs";

        private const string MaterialRoot =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Materials/InactiveCompatible";

        private const string SourceManifestPrefix = "InactiveCompatibleSource=";
        private const string PackageLitMaterialGuid = "31321ba15b8f8eb4c954353edc038b1d";
        private const string CustomEnemyLitShaderName = "Game/Enemy/CustomEnemyLit";
        private const string BlackEyeBridgeShaderName = "Game/Enemy/BlackEyeInactiveBridge";
        private const string AdditiveBridgeShaderName = "Game/Enemy/AdditiveInactiveBridge";
        private const string ExistingCompatibleMaterialPath = "Assets/_Shared/Art/Game_Enemy_CustomEnemyLit_NonAttacking.mat";
        private const string MaterialAuthoringGeneratorPath =
            "Assets/_Features/Stages/Editor/EnemyInactiveMaterialAuthoringGenerator.cs";
        private const string BlackEyeSourceMaterialPath = "Assets/3DM/2BlackEye/BE_LS_M1.mat";
        private const string PackageLitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Lit.mat";
        private const string SecBotGlowSourceMaterialPath = "Assets/Polygon Arsenal/Materials/Gradients/PolySpriteGlow_ADD.mat";
        private const string ExpectedInactiveNoiseTexturePath = "Assets/_Shared/Art/Textures/GravityFieldLockRevealNoise.png";
        private const string EnemyInactiveVisualSettingsPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/EnemyInactiveVisualSettings.asset";

        [Test]
        [Category("Extended")]
        public void AllowedInactiveShaders_ExposeNoiseContract()
        {
            foreach (var shaderName in new[] { CustomEnemyLitShaderName, BlackEyeBridgeShaderName, AdditiveBridgeShaderName })
            {
                var shader = Shader.Find(shaderName);
                Assert.That(shader, Is.Not.Null, shaderName);
                var material = new Material(shader);
                try
                {
                    AssertInactiveContract(material, shaderName);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyInactiveVisualSettings_DefaultAsset_UsesCurrentRuntimeDefaults()
        {
            var settings = AssetDatabase.LoadAssetAtPath<EnemyInactiveVisualSettings>(EnemyInactiveVisualSettingsPath);
            Assert.That(settings, Is.Not.Null, EnemyInactiveVisualSettingsPath);

            AssertColorEqual(new Color(0.62f, 0.64f, 0.68f, 1f), settings.InactiveTint, nameof(settings.InactiveTint));
            AssertFloatEqual(0.85f, settings.DesaturateStrength, nameof(settings.DesaturateStrength));
            AssertFloatEqual(0.85f, settings.EmissionSuppression, nameof(settings.EmissionSuppression));
            AssertFloatEqual(0.25f, settings.InactiveRevealInSeconds, nameof(settings.InactiveRevealInSeconds));
            AssertFloatEqual(0.18f, settings.InactiveRevealOutSeconds, nameof(settings.InactiveRevealOutSeconds));
            Assert.That(settings.InactiveRevealCurve, Is.Not.Null);
            Assert.That(settings.InactiveRevealCurve.Evaluate(0f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(settings.InactiveRevealCurve.Evaluate(1f), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        [Category("Extended")]
        public void EnemyInactiveMaterialAuthoringGenerator_WritesNoiseRevealDefault()
        {
            var source = File.ReadAllText(MaterialAuthoringGeneratorPath);

            Assert.That(source, Does.Contain("material.SetFloat(\"_InactiveNoiseReveal\", 0f);"));
        }

        [Test]
        [Category("Core")]
        public void CampaignEnemyPrefabs_AllRendererMaterialsSatisfyInactiveContract()
        {
            foreach (var prefabPath in CampaignEnemyPrefabPaths())
            {
                var prefabText = File.ReadAllText(prefabPath);
                Assert.That(prefabText, Does.Not.Contain(PackageLitMaterialGuid), prefabPath);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.That(prefab, Is.Not.Null, prefabPath);

                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for (var index = 0; index < materials.Length; index++)
                    {
                        var material = materials[index];
                        var context = $"{prefabPath}/{renderer.name}[{index}]";
                        Assert.That(material, Is.Not.Null, context);
                        AssertInactiveContract(material, context);
                        AssertInactiveNoiseAuthoring(material, context);
                        AssertAllowedShader(material, context);
                        AssertDuplicatePathPolicy(material, context);
                    }
                }
            }
        }

        [Test]
        [Category("Extended")]
        public void InactiveCompatibleDuplicates_RecordSourceAndPreserveMaterialValues()
        {
            foreach (var materialPath in InactiveDuplicateMaterialPaths())
            {
                var duplicate = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                Assert.That(duplicate, Is.Not.Null, materialPath);
                AssertInactiveContract(duplicate, materialPath);
                AssertInactiveNoiseAuthoring(duplicate, materialPath);

                var sourcePath = ReadSourcePath(materialPath);
                Assert.That(sourcePath, Is.Not.Empty, materialPath);
                Assert.That(sourcePath, Does.Not.StartWith(MaterialRoot), materialPath);

                var sources = LoadSourceMaterials(duplicate, sourcePath).ToArray();
                Assert.That(sources, Is.Not.Empty, $"Missing source for {materialPath}: {sourcePath}");
                AssertCommonMaterialFidelityMatchesAnySource(sources, duplicate, materialPath);
            }
        }

        [Test]
        [Category("Extended")]
        public void BlackEyeBridge_PreservesShaderGraphColorProperties()
        {
            var duplicate = LoadRequiredMaterial($"{MaterialRoot}/EnemyView_BlackEye/EnemyView_BlackEye_BELSM1_Inactive.mat");
            var source = LoadRequiredMaterial(BlackEyeSourceMaterialPath);

            Assert.That(source.shader.name, Is.EqualTo("Shader Graphs/BE_LS1"));
            Assert.That(duplicate.shader.name, Is.EqualTo(BlackEyeBridgeShaderName));
            Assert.That(ReadSourcePath(AssetDatabase.GetAssetPath(duplicate)), Is.EqualTo(BlackEyeSourceMaterialPath));

            AssertColorEqual(source.GetColor("_B"), duplicate.GetColor("_B"), "_B");
            AssertColorEqual(source.GetColor("_W"), duplicate.GetColor("_W"), "_W");
            AssertFloatEqual(source.GetFloat("_Border"), duplicate.GetFloat("_Border"), "_Border");
            AssertInactiveNoiseAuthoring(duplicate, AssetDatabase.GetAssetPath(duplicate));
        }

        [Test]
        [Category("Extended")]
        public void BlackEyeLegLitDuplicate_PreservesPackageLitSource()
        {
            var duplicatePath = $"{MaterialRoot}/EnemyView_BlackEye/EnemyView_BlackEye_Lit_Inactive.mat";
            var duplicate = LoadRequiredMaterial(duplicatePath);
            var source = LoadRequiredMaterial(PackageLitMaterialPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/EnemyView_BlackEye.prefab");
            Assert.That(prefab, Is.Not.Null);

            Assert.That(duplicate.shader.name, Is.EqualTo(CustomEnemyLitShaderName));
            Assert.That(ReadSourcePath(duplicatePath), Is.EqualTo(PackageLitMaterialPath));
            AssertCommonMaterialFidelity(source, duplicate, duplicatePath);

            AssertRendererUsesOnlyMaterial(prefab, "Cylinder002", duplicate);
            AssertRendererUsesOnlyMaterial(prefab, "Cylinder003", duplicate);
        }

        [Test]
        [Category("Extended")]
        public void SecBotAdditiveBridge_PreservesGlowRenderStateAndEmissionSource()
        {
            var duplicate = LoadRequiredMaterial($"{MaterialRoot}/EnemyView_SecBot/EnemyView_SecBot_PolySpriteGlowADD_Inactive.mat");
            var source = LoadRequiredMaterial(SecBotGlowSourceMaterialPath);

            Assert.That(duplicate.shader.name, Is.EqualTo(AdditiveBridgeShaderName));
            Assert.That(ReadSourcePath(AssetDatabase.GetAssetPath(duplicate)), Is.EqualTo(SecBotGlowSourceMaterialPath));
            AssertInactiveNoiseAuthoring(duplicate, AssetDatabase.GetAssetPath(duplicate));
            Assert.That(duplicate.GetTexture("_MainTex"), Is.EqualTo(source.GetTexture("_MainTex")));
            AssertFloatEqual(source.GetFloat("_SrcBlend"), duplicate.GetFloat("_SrcBlend"), "_SrcBlend");
            AssertFloatEqual(source.GetFloat("_DstBlend"), duplicate.GetFloat("_DstBlend"), "_DstBlend");
            AssertFloatEqual(source.GetFloat("_ZWrite"), duplicate.GetFloat("_ZWrite"), "_ZWrite");
            AssertFloatEqual(source.GetFloat("_Cull"), duplicate.GetFloat("_Cull"), "_Cull");
            Assert.That(duplicate.renderQueue, Is.EqualTo(source.renderQueue));
        }

        private static string[] CampaignEnemyPrefabPaths()
        {
            return AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetFileNameWithoutExtension(path).StartsWith("EnemyView_", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] InactiveDuplicateMaterialPaths()
        {
            return AssetDatabase.FindAssets("t:Material", new[] { MaterialRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".mat", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static void AssertInactiveContract(Material material, string context)
        {
            Assert.That(material.HasProperty("_InactiveBlend"), Is.True, context);
            Assert.That(material.HasProperty("_DesaturateStrength"), Is.True, context);
            Assert.That(material.HasProperty("_EmissionSuppression"), Is.True, context);
            Assert.That(material.HasProperty("_InactiveTint"), Is.True, context);
            Assert.That(material.HasProperty("_InactiveNoiseMap"), Is.True, context);
            Assert.That(material.HasProperty("_InactiveNoiseReveal"), Is.True, context);
            Assert.That(material.HasProperty("_InactiveNoiseStrength"), Is.True, context);
            Assert.That(material.HasProperty("_InactiveNoiseScale"), Is.True, context);
            Assert.That(material.HasProperty("_InactiveNoiseEdgeWidth"), Is.True, context);
            Assert.That(material.HasProperty("_InactiveNoiseThreshold"), Is.True, context);
        }

        private static void AssertInactiveNoiseAuthoring(Material material, string context)
        {
            var expectedNoise = LoadRequiredTexture(ExpectedInactiveNoiseTexturePath);
            Assert.That(material.GetTexture("_InactiveNoiseMap"), Is.EqualTo(expectedNoise), $"{context}:_InactiveNoiseMap");
            Assert.That(material.GetFloat("_InactiveNoiseReveal"), Is.EqualTo(0f).Within(0.0001f), $"{context}:_InactiveNoiseReveal");

            var strength = material.GetFloat("_InactiveNoiseStrength");
            var shaderName = material.shader != null ? material.shader.name : string.Empty;
            if (string.Equals(shaderName, AdditiveBridgeShaderName, StringComparison.Ordinal))
            {
                Assert.That(strength, Is.InRange(0f, 0.15f), $"{context}:_InactiveNoiseStrength");
            }
            else
            {
                Assert.That(strength, Is.InRange(0f, 0.5f), $"{context}:_InactiveNoiseStrength");
            }

            Assert.That(material.GetFloat("_InactiveNoiseScale"), Is.GreaterThan(0f), $"{context}:_InactiveNoiseScale");
            Assert.That(material.GetFloat("_InactiveNoiseEdgeWidth"), Is.GreaterThan(0f), $"{context}:_InactiveNoiseEdgeWidth");
            Assert.That(material.GetFloat("_InactiveNoiseThreshold"), Is.InRange(0f, 1f), $"{context}:_InactiveNoiseThreshold");
        }

        private static void AssertAllowedShader(Material material, string context)
        {
            var shaderName = material.shader != null ? material.shader.name : string.Empty;
            Assert.That(
                shaderName,
                Is.EqualTo(CustomEnemyLitShaderName)
                    .Or.EqualTo(BlackEyeBridgeShaderName)
                    .Or.EqualTo(AdditiveBridgeShaderName),
                context);
        }

        private static void AssertDuplicatePathPolicy(Material material, string context)
        {
            var path = AssetDatabase.GetAssetPath(material);
            if (string.Equals(path, ExistingCompatibleMaterialPath, StringComparison.Ordinal))
            {
                return;
            }

            Assert.That(path, Does.StartWith(MaterialRoot + "/"), context);
            Assert.That(Path.GetFileNameWithoutExtension(path), Does.StartWith("EnemyView_"), context);
            Assert.That(Path.GetFileNameWithoutExtension(path), Does.EndWith("_Inactive"), context);
            Assert.That(ReadSourcePath(path), Is.Not.Empty, context);
        }

        private static void AssertRendererUsesOnlyMaterial(GameObject prefab, string rendererName, Material expected)
        {
            var renderer = prefab.GetComponentsInChildren<Renderer>(true)
                .FirstOrDefault(candidate => string.Equals(candidate.name, rendererName, StringComparison.Ordinal));
            Assert.That(renderer, Is.Not.Null, rendererName);
            Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(1), rendererName);
            Assert.That(renderer.sharedMaterials[0], Is.EqualTo(expected), rendererName);
        }

        private static void AssertCommonMaterialFidelity(Material source, Material duplicate, string context)
        {
            AssertTextureMapped(source, duplicate, "_BaseMap", "_BaseMap", context);
            AssertTextureMapped(source, duplicate, "_MainTex", "_MainTex", context);
            AssertTextureMapped(source, duplicate, "_MainTex", "_BaseMap", context);
            AssertTextureMapped(source, duplicate, "_BaseMap", "_MainTex", context);
            AssertTextureMapped(source, duplicate, "_BumpMap", "_BumpMap", context);
            AssertTextureMapped(source, duplicate, "_EmissionMap", "_EmissionMap", context);
            AssertTextureMapped(source, duplicate, "_MetallicGlossMap", "_MetallicGlossMap", context);
            AssertTextureMapped(source, duplicate, "_OcclusionMap", "_OcclusionMap", context);

            foreach (var property in new[] { "_BaseColor", "_Color", "_B", "_W", "_EmissionColor", "_SpecColor" })
            {
                if (source.HasProperty(property) && duplicate.HasProperty(property))
                {
                    AssertColorEqual(source.GetColor(property), duplicate.GetColor(property), $"{context}:{property}");
                }
            }

            foreach (var property in new[]
                     {
                         "_Border", "_Smoothness", "_Glossiness", "_Metallic", "_BumpScale", "_OcclusionStrength",
                         "_SrcBlend", "_DstBlend", "_SrcBlendAlpha", "_DstBlendAlpha", "_ZWrite", "_Cull",
                         "_Surface", "_Blend", "_AlphaClip", "_Cutoff"
                     })
            {
                if (source.HasProperty(property) && duplicate.HasProperty(property))
                {
                    AssertFloatEqual(source.GetFloat(property), duplicate.GetFloat(property), $"{context}:{property}");
                }
            }

            Assert.That(duplicate.renderQueue, Is.EqualTo(source.renderQueue), $"{context}:renderQueue");
            Assert.That(duplicate.enableInstancing, Is.EqualTo(source.enableInstancing), $"{context}:enableInstancing");
            Assert.That(duplicate.doubleSidedGI, Is.EqualTo(source.doubleSidedGI), $"{context}:doubleSidedGI");
        }

        private static void AssertTextureMapped(
            Material source,
            Material duplicate,
            string sourceProperty,
            string targetProperty,
            string context)
        {
            if (!source.HasProperty(sourceProperty) || !duplicate.HasProperty(targetProperty))
            {
                return;
            }

            Assert.That(duplicate.GetTexture(targetProperty), Is.EqualTo(source.GetTexture(sourceProperty)), $"{context}:{targetProperty}");
            Assert.That(duplicate.GetTextureScale(targetProperty), Is.EqualTo(source.GetTextureScale(sourceProperty)), $"{context}:{targetProperty}_ST scale");
            Assert.That(duplicate.GetTextureOffset(targetProperty), Is.EqualTo(source.GetTextureOffset(sourceProperty)), $"{context}:{targetProperty}_ST offset");
        }

        private static Material LoadSourceMaterial(Material duplicate, string sourcePath)
        {
            return LoadSourceMaterials(duplicate, sourcePath).FirstOrDefault();
        }

        private static IEnumerable<Material> LoadSourceMaterials(Material duplicate, string sourcePath)
        {
            var direct = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            if (direct != null)
            {
                yield return direct;
                yield break;
            }

            var sourceName = ExtractSourceName(duplicate.name);
            foreach (var material in AssetDatabase.LoadAllAssetsAtPath(sourcePath)
                         .OfType<Material>()
                         .Where(material => string.Equals(Sanitize(material.name), sourceName, StringComparison.Ordinal)))
            {
                yield return material;
            }
        }

        private static void AssertCommonMaterialFidelityMatchesAnySource(
            Material[] sources,
            Material duplicate,
            string context)
        {
            var failures = new List<string>();
            foreach (var source in sources)
            {
                try
                {
                    AssertCommonMaterialFidelity(source, duplicate, context);
                    return;
                }
                catch (AssertionException exception)
                {
                    failures.Add(exception.Message);
                }
            }

            Assert.Fail($"{context}: duplicate did not match any source candidate.\n{string.Join("\n", failures)}");
        }

        private static Material LoadRequiredMaterial(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.That(material, Is.Not.Null, path);
            return material;
        }

        private static Texture2D LoadRequiredTexture(string path)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(texture, Is.Not.Null, path);
            return texture;
        }

        private static string ReadSourcePath(string materialPath)
        {
            var importer = AssetImporter.GetAtPath(materialPath);
            var userData = importer != null ? importer.userData : string.Empty;
            return userData.StartsWith(SourceManifestPrefix, StringComparison.Ordinal)
                ? userData.Substring(SourceManifestPrefix.Length)
                : string.Empty;
        }

        private static string ExtractSourceName(string duplicateName)
        {
            var withoutSuffix = duplicateName.EndsWith("_Inactive", StringComparison.Ordinal)
                ? duplicateName.Substring(0, duplicateName.Length - "_Inactive".Length)
                : duplicateName;
            var parts = withoutSuffix.Split(new[] { '_' }, 3);
            return parts.Length == 3 ? parts[2] : withoutSuffix;
        }

        private static string Sanitize(string value)
        {
            return new string(value.Where(char.IsLetterOrDigit).ToArray());
        }

        private static void AssertColorEqual(Color expected, Color actual, string context)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.0001f), context);
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.0001f), context);
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.0001f), context);
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.0001f), context);
        }

        private static void AssertFloatEqual(float expected, float actual, string context)
        {
            Assert.That(actual, Is.EqualTo(expected).Within(0.0001f), context);
        }
    }
}
