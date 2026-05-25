using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.EditorTools
{
    public static class EnemyInactiveMaterialAuthoringGenerator
    {
        public const string MaterialRoot =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Materials/InactiveCompatible";

        private const string PrefabRoot =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs";

        private const string CustomEnemyLitShaderName = "Game/Enemy/CustomEnemyLit";
        private const string BlackEyeBridgeShaderName = "Game/Enemy/BlackEyeInactiveBridge";
        private const string AdditiveBridgeShaderName = "Game/Enemy/AdditiveInactiveBridge";

        private const string BlackEyeSourceMaterialPath = "Assets/3DM/2BlackEye/BE_LS_M1.mat";
        private const string PackageLitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Lit.mat";
        private const string JumpingInferredSourceMaterialPath = "Assets/_Shared/Art/Game_Enemy_CustomEnemyLit_NonAttacking.mat";
        private const string InactiveNoiseTexturePath = "Assets/_Shared/Art/Textures/GravityFieldLockRevealNoise.png";

        private static readonly Color InactiveTint = new(0.62f, 0.64f, 0.68f, 1f);

        private static readonly (string PrefabName, string FbxPath)[] FbxTargets =
        {
            ("Startis", "Assets/3DM/3Startis/Startis.fbx"),
            ("BlackEye", "Assets/3DM/2BlackEye/BlackEye.fbx"),
            ("Astreton", "Assets/3DM/5Astra/Astra.fbx"),
            ("Sunwheel", "Assets/3DM/4Sunwheel/Sun.fbx"),
            ("JPeter", "Assets/3DM/6J/J.fbx"),
            ("RocketFace", "Assets/3DM/7RF/RF15(Edit).fbx"),
            ("DrSaturn", "Assets/3DM/8DrS/DrS155.fbx"),
            ("Kali", "Assets/3DM/9Kali/Kali01.fbx"),
            ("SecBot", "Assets/3DM/10SecBot/SecBot04.fbx"),
        };

        [MenuItem("Tools/Enemy/Generate Inactive-Compatible Materials")]
        public static void RunFromMenu()
        {
            Run();
        }

        public static void RunFromCommandLine()
        {
            Run();
        }

        public static void Run()
        {
            EnsureFolder(MaterialRoot);
            OrganizeExistingDuplicateFolders();

            var customEnemyLit = RequireShader(CustomEnemyLitShaderName);
            var blackEyeBridge = RequireShader(BlackEyeBridgeShaderName);
            var additiveBridge = RequireShader(AdditiveBridgeShaderName);
            var duplicateCache = new Dictionary<string, Material>(StringComparer.Ordinal);

            UpdateModelImporterRemaps(customEnemyLit, blackEyeBridge, duplicateCache);
            UpdateDirectPrefabRendererSlots(customEnemyLit, blackEyeBridge, additiveBridge, duplicateCache);
            UpdateExistingDuplicateDefaults();
            UpdateExistingCompatibleMaterialDefaults();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void UpdateModelImporterRemaps(
            Shader customEnemyLit,
            Shader blackEyeBridge,
            Dictionary<string, Material> duplicateCache)
        {
            foreach (var target in FbxTargets)
            {
                var importer = AssetImporter.GetAtPath(target.FbxPath) as ModelImporter;
                if (importer == null)
                {
                    throw new InvalidOperationException($"Missing ModelImporter: {target.FbxPath}");
                }

                var changed = false;
                var externalMap = importer.GetExternalObjectMap();
                foreach (var pair in externalMap)
                {
                    if (pair.Value is not Material sourceMaterial)
                    {
                        var existingDuplicate = FindExistingDuplicateForSourceName(target.PrefabName, pair.Key.name);
                        if (existingDuplicate != null)
                        {
                            importer.AddRemap(pair.Key, existingDuplicate);
                            changed = true;
                        }

                        continue;
                    }

                    var sourcePath = AssetDatabase.GetAssetPath(sourceMaterial);
                    if (sourcePath.StartsWith(MaterialRoot + "/", StringComparison.Ordinal))
                    {
                        var sourceOfDuplicate = ReadInactiveCompatibleSourcePath(sourcePath);
                        if (sourceOfDuplicate.StartsWith(MaterialRoot + "/", StringComparison.Ordinal))
                        {
                            var intendedDuplicate = AssetDatabase.LoadAssetAtPath<Material>(sourceOfDuplicate);
                            if (intendedDuplicate == null)
                            {
                                throw new InvalidOperationException(
                                    $"Inactive duplicate source remap points to a missing material: {sourcePath} -> {sourceOfDuplicate}");
                            }

                            importer.AddRemap(pair.Key, intendedDuplicate);
                            changed = true;
                        }

                        continue;
                    }

                    var targetShader = string.Equals(sourcePath, BlackEyeSourceMaterialPath, StringComparison.Ordinal)
                        ? blackEyeBridge
                        : customEnemyLit;
                    var duplicate = GetOrCreateDuplicate(target.PrefabName, sourceMaterial, targetShader, duplicateCache);
                    if (duplicate != null && pair.Value != duplicate)
                    {
                        importer.AddRemap(pair.Key, duplicate);
                        changed = true;
                    }
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                }
            }
        }

        private static void UpdateDirectPrefabRendererSlots(
            Shader customEnemyLit,
            Shader blackEyeBridge,
            Shader additiveBridge,
            Dictionary<string, Material> duplicateCache)
        {
            var prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetFileNameWithoutExtension(path).StartsWith("EnemyView_", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            foreach (var prefabPath in prefabPaths)
            {
                var prefabName = Path.GetFileNameWithoutExtension(prefabPath).Replace("EnemyView_", string.Empty);
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                var changed = false;
                try
                {
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    {
                        var materials = renderer.sharedMaterials;
                        for (var index = 0; index < materials.Length; index++)
                        {
                            var replacement = ResolveReplacementForRendererSlot(
                                prefabName,
                                renderer.name,
                                materials[index],
                                customEnemyLit,
                                blackEyeBridge,
                                additiveBridge,
                                duplicateCache);
                            if (replacement == null || replacement == materials[index])
                            {
                                continue;
                            }

                            materials[index] = replacement;
                            changed = true;
                        }

                        if (changed)
                        {
                            renderer.sharedMaterials = materials;
                        }
                    }

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static Material ResolveReplacementForRendererSlot(
            string prefabName,
            string rendererName,
            Material sourceMaterial,
            Shader customEnemyLit,
            Shader blackEyeBridge,
            Shader additiveBridge,
            Dictionary<string, Material> duplicateCache)
        {
            if (string.Equals(prefabName, "BlackEye", StringComparison.Ordinal) &&
                (string.Equals(rendererName, "Cylinder002", StringComparison.Ordinal) ||
                 string.Equals(rendererName, "Cylinder003", StringComparison.Ordinal)))
            {
                return GetOrCreateDuplicate(
                    prefabName,
                    RequireMaterial(PackageLitMaterialPath),
                    customEnemyLit,
                    duplicateCache);
            }

            if (string.Equals(prefabName, "Jumping", StringComparison.Ordinal) &&
                string.Equals(rendererName, "HumanM_BodyMesh", StringComparison.Ordinal))
            {
                return GetOrCreateDuplicate(
                    prefabName,
                    RequireMaterial(JumpingInferredSourceMaterialPath),
                    customEnemyLit,
                    duplicateCache);
            }

            if (sourceMaterial == null)
            {
                throw new InvalidOperationException(
                    $"Unresolved material slot without policy: EnemyView_{prefabName}/{rendererName}");
            }

            var sourcePath = AssetDatabase.GetAssetPath(sourceMaterial);
            if (string.IsNullOrEmpty(sourcePath) ||
                sourcePath.StartsWith(MaterialRoot + "/", StringComparison.Ordinal))
            {
                return null;
            }

            if (sourceMaterial.shader != null &&
                string.Equals(sourceMaterial.shader.name, CustomEnemyLitShaderName, StringComparison.Ordinal) &&
                SupportsInactiveContract(sourceMaterial))
            {
                return null;
            }

            if (string.Equals(sourcePath, BlackEyeSourceMaterialPath, StringComparison.Ordinal))
            {
                return GetOrCreateDuplicate(prefabName, sourceMaterial, blackEyeBridge, duplicateCache);
            }

            if (string.Equals(sourcePath, "Assets/Polygon Arsenal/Materials/Gradients/PolySpriteGlow_ADD.mat", StringComparison.Ordinal))
            {
                return GetOrCreateDuplicate(prefabName, sourceMaterial, additiveBridge, duplicateCache);
            }

            return GetOrCreateDuplicate(prefabName, sourceMaterial, customEnemyLit, duplicateCache);
        }

        private static Material GetOrCreateDuplicate(
            string prefabName,
            Material sourceMaterial,
            Shader targetShader,
            Dictionary<string, Material> duplicateCache)
        {
            if (sourceMaterial == null)
            {
                return null;
            }

            var targetPath = BuildDuplicatePath(prefabName, sourceMaterial);
            if (duplicateCache.TryGetValue(targetPath, out var cached))
            {
                return cached;
            }

            var duplicate = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
            if (duplicate == null)
            {
                duplicate = new Material(targetShader);
                AssetDatabase.CreateAsset(duplicate, targetPath);
            }

            duplicate.shader = targetShader;
            CopyMaterialValues(sourceMaterial, duplicate);
            ApplyInactiveDefaults(duplicate);
            duplicate.enableInstancing = sourceMaterial.enableInstancing;
            duplicate.doubleSidedGI = sourceMaterial.doubleSidedGI;
            duplicate.globalIlluminationFlags = sourceMaterial.globalIlluminationFlags;
            duplicate.renderQueue = sourceMaterial.renderQueue;
            duplicate.shaderKeywords = sourceMaterial.shaderKeywords;
            EditorUtility.SetDirty(duplicate);

            var importer = AssetImporter.GetAtPath(targetPath);
            if (importer != null)
            {
                importer.userData = $"InactiveCompatibleSource={AssetDatabase.GetAssetPath(sourceMaterial)}";
                importer.SaveAndReimport();
            }

            duplicateCache[targetPath] = duplicate;
            return duplicate;
        }

        private static void CopyMaterialValues(Material source, Material target)
        {
            CopyTexture(source, target, "_BaseMap", "_BaseMap");
            CopyTexture(source, target, "_MainTex", "_MainTex");
            CopyTexture(source, target, "_MainTex", "_BaseMap");
            CopyTexture(source, target, "_BaseMap", "_MainTex");
            CopyTexture(source, target, "PA_SSS_Texture2D_", "PA_SSS_Texture2D_");
            CopyTexture(source, target, "PA_SSS_Texture2D_", "_MainTex");
            CopyTexture(source, target, "PA_SSS_Texture2D_", "_BaseMap");
            CopyTexture(source, target, "_BumpMap", "_BumpMap");
            CopyTexture(source, target, "_NormalMap", "_BumpMap");
            CopyTexture(source, target, "_EmissionMap", "_EmissionMap");
            CopyTexture(source, target, "_MetallicGlossMap", "_MetallicGlossMap");
            CopyTexture(source, target, "_SpecGlossMap", "_SpecGlossMap");
            CopyTexture(source, target, "_OcclusionMap", "_OcclusionMap");

            foreach (var property in new[]
                     {
                         "_BaseColor", "_Color", "_B", "_W", "_SpecColor", "_EmissionColor",
                         "_TintColor", "_BaseColorAddSubDiff", "_ColorAddSubDiff"
                     })
            {
                CopyColor(source, target, property);
            }

            foreach (var property in new[]
                     {
                         "_Border", "_Cutoff", "_Smoothness", "_Glossiness", "_Metallic", "_BumpScale", "_OcclusionStrength",
                         "_Surface", "_Blend", "_SrcBlend", "_DstBlend", "_SrcBlendAlpha", "_DstBlendAlpha",
                         "_ZWrite", "_Cull", "_AlphaClip", "_AlphaToMask", "_BlendOp", "_QueueOffset",
                         "_ReceiveShadows"
                     })
            {
                CopyFloat(source, target, property);
            }
        }

        private static void CopyTexture(Material source, Material target, string sourceProperty, string targetProperty)
        {
            if (!source.HasProperty(sourceProperty) || !target.HasProperty(targetProperty))
            {
                return;
            }

            target.SetTexture(targetProperty, source.GetTexture(sourceProperty));
            target.SetTextureScale(targetProperty, source.GetTextureScale(sourceProperty));
            target.SetTextureOffset(targetProperty, source.GetTextureOffset(sourceProperty));
        }

        private static void CopyColor(Material source, Material target, string property)
        {
            if (!source.HasProperty(property) || !target.HasProperty(property))
            {
                return;
            }

            target.SetColor(property, source.GetColor(property));
        }

        private static void CopyFloat(Material source, Material target, string property)
        {
            if (!source.HasProperty(property) || !target.HasProperty(property))
            {
                return;
            }

            target.SetFloat(property, source.GetFloat(property));
        }

        private static void ApplyInactiveDefaults(Material material)
        {
            if (material.HasProperty("_InactiveBlend"))
            {
                material.SetFloat("_InactiveBlend", 0f);
            }

            if (material.HasProperty("_InactiveNoiseReveal"))
            {
                material.SetFloat("_InactiveNoiseReveal", 0f);
            }

            if (material.HasProperty("_DesaturateStrength"))
            {
                material.SetFloat("_DesaturateStrength", 0.85f);
            }

            if (material.HasProperty("_EmissionSuppression"))
            {
                material.SetFloat("_EmissionSuppression", 0.85f);
            }

            if (material.HasProperty("_InactiveTint"))
            {
                material.SetColor("_InactiveTint", InactiveTint);
            }

            ApplyInactiveNoiseDefaults(material);
        }

        private static void ApplyInactiveNoiseDefaults(Material material)
        {
            if (material.HasProperty("_InactiveNoiseMap") &&
                material.GetTexture("_InactiveNoiseMap") == null)
            {
                material.SetTexture("_InactiveNoiseMap", RequireTexture(InactiveNoiseTexturePath));
            }

            if (material.HasProperty("_InactiveNoiseStrength") &&
                !HasSerializedFloat(material, "_InactiveNoiseStrength"))
            {
                material.SetFloat("_InactiveNoiseStrength", ResolveInactiveNoiseStrength(material));
            }

            if (material.HasProperty("_InactiveNoiseScale") &&
                !HasSerializedFloat(material, "_InactiveNoiseScale"))
            {
                material.SetFloat("_InactiveNoiseScale", 1f);
            }

            if (material.HasProperty("_InactiveNoiseEdgeWidth") &&
                !HasSerializedFloat(material, "_InactiveNoiseEdgeWidth"))
            {
                material.SetFloat("_InactiveNoiseEdgeWidth", 0.08f);
            }

            if (material.HasProperty("_InactiveNoiseThreshold") &&
                !HasSerializedFloat(material, "_InactiveNoiseThreshold"))
            {
                material.SetFloat("_InactiveNoiseThreshold", 0.5f);
            }
        }

        private static bool HasSerializedFloat(Material material, string propertyName)
        {
            if (material == null)
            {
                return false;
            }

            var serializedObject = new SerializedObject(material);
            var floats = serializedObject.FindProperty("m_SavedProperties.m_Floats");
            if (floats == null)
            {
                return false;
            }

            for (var i = 0; i < floats.arraySize; i++)
            {
                var element = floats.GetArrayElementAtIndex(i);
                var first = element.FindPropertyRelative("first");
                if (first != null &&
                    string.Equals(first.stringValue, propertyName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static float ResolveInactiveNoiseStrength(Material material)
        {
            var shaderName = material.shader != null ? material.shader.name : string.Empty;
            if (string.Equals(shaderName, AdditiveBridgeShaderName, StringComparison.Ordinal))
            {
                return 0f;
            }

            if (string.Equals(shaderName, BlackEyeBridgeShaderName, StringComparison.Ordinal))
            {
                return 0.25f;
            }

            return 0.3f;
        }

        private static void UpdateExistingCompatibleMaterialDefaults()
        {
            var prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetFileNameWithoutExtension(path).StartsWith("EnemyView_", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal);

            foreach (var prefabPath in prefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    continue;
                }

                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null)
                        {
                            continue;
                        }

                        var materialPath = AssetDatabase.GetAssetPath(material);
                        if (materialPath.StartsWith(MaterialRoot + "/", StringComparison.Ordinal) ||
                            material.shader == null ||
                            !string.Equals(material.shader.name, CustomEnemyLitShaderName, StringComparison.Ordinal) ||
                            !SupportsInactiveContract(material))
                        {
                            continue;
                        }

                        ApplyInactiveDefaults(material);
                        EditorUtility.SetDirty(material);
                    }
                }
            }
        }

        private static void UpdateExistingDuplicateDefaults()
        {
            var materialPaths = AssetDatabase.FindAssets("t:Material", new[] { MaterialRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".mat", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal);

            foreach (var materialPath in materialPaths)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null || !SupportsInactiveContract(material))
                {
                    continue;
                }

                ApplyInactiveDefaults(material);
                EditorUtility.SetDirty(material);
            }
        }

        private static string BuildDuplicatePath(string prefabName, Material sourceMaterial)
        {
            var sourceName = Sanitize(sourceMaterial.name);
            var folderPath = $"{MaterialRoot}/EnemyView_{Sanitize(prefabName)}";
            EnsureFolder(folderPath);
            return $"{folderPath}/EnemyView_{Sanitize(prefabName)}_{sourceName}_Inactive.mat";
        }

        private static Material FindExistingDuplicateForSourceName(string prefabName, string sourceName)
        {
            var directPath = $"{MaterialRoot}/EnemyView_{Sanitize(prefabName)}/EnemyView_{Sanitize(prefabName)}_{Sanitize(sourceName)}_Inactive.mat";
            var direct = AssetDatabase.LoadAssetAtPath<Material>(directPath);
            if (direct != null)
            {
                return direct;
            }

            if (string.Equals(prefabName, "BlackEye", StringComparison.Ordinal) &&
                string.Equals(sourceName, "Material #4", StringComparison.Ordinal))
            {
                return AssetDatabase.LoadAssetAtPath<Material>(
                    $"{MaterialRoot}/EnemyView_BlackEye/EnemyView_BlackEye_BELSM1_Inactive.mat");
            }

            return null;
        }

        private static void OrganizeExistingDuplicateFolders()
        {
            var materialGuids = AssetDatabase.FindAssets("t:Material", new[] { MaterialRoot });
            foreach (var guid in materialGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith(MaterialRoot + "/", StringComparison.Ordinal) ||
                    !string.Equals(Path.GetDirectoryName(path)?.Replace('\\', '/'), MaterialRoot, StringComparison.Ordinal))
                {
                    continue;
                }

                var fileName = Path.GetFileNameWithoutExtension(path);
                if (!TryGetEnemyViewFolderName(fileName, out var folderName))
                {
                    continue;
                }

                var folderPath = $"{MaterialRoot}/{folderName}";
                EnsureFolder(folderPath);
                var targetPath = $"{folderPath}/{Path.GetFileName(path)}";
                if (string.Equals(path, targetPath, StringComparison.Ordinal))
                {
                    continue;
                }

                var error = AssetDatabase.MoveAsset(path, targetPath);
                if (!string.IsNullOrEmpty(error))
                {
                    throw new InvalidOperationException($"Failed to move inactive material {path} -> {targetPath}: {error}");
                }
            }
        }

        private static bool TryGetEnemyViewFolderName(string materialName, out string folderName)
        {
            folderName = string.Empty;
            const string prefix = "EnemyView_";
            if (!materialName.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            var remainder = materialName.Substring(prefix.Length);
            var separatorIndex = remainder.IndexOf('_');
            if (separatorIndex <= 0)
            {
                return false;
            }

            folderName = $"{prefix}{remainder.Substring(0, separatorIndex)}";
            return true;
        }

        private static string Sanitize(string value)
        {
            var chars = value.Where(char.IsLetterOrDigit).ToArray();
            return chars.Length == 0 ? "Material" : new string(chars);
        }

        private static bool SupportsInactiveContract(Material material)
        {
            return material.HasProperty("_InactiveBlend") &&
                   material.HasProperty("_InactiveNoiseReveal") &&
                   material.HasProperty("_DesaturateStrength") &&
                   material.HasProperty("_EmissionSuppression") &&
                   material.HasProperty("_InactiveTint") &&
                   material.HasProperty("_InactiveNoiseMap") &&
                   material.HasProperty("_InactiveNoiseStrength") &&
                   material.HasProperty("_InactiveNoiseScale") &&
                   material.HasProperty("_InactiveNoiseEdgeWidth") &&
                   material.HasProperty("_InactiveNoiseThreshold");
        }

        private static string ReadInactiveCompatibleSourcePath(string materialPath)
        {
            var importer = AssetImporter.GetAtPath(materialPath);
            const string prefix = "InactiveCompatibleSource=";
            var userData = importer?.userData ?? string.Empty;
            return userData.StartsWith(prefix, StringComparison.Ordinal)
                ? userData.Substring(prefix.Length)
                : string.Empty;
        }

        private static Material RequireMaterial(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                throw new InvalidOperationException($"Missing material: {path}");
            }

            return material;
        }

        private static Texture2D RequireTexture(string path)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                throw new InvalidOperationException($"Missing texture: {path}");
            }

            return texture;
        }

        private static Shader RequireShader(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                throw new InvalidOperationException($"Missing shader: {shaderName}");
            }

            return shader;
        }

        private static void EnsureFolder(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }
    }
}
