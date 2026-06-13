using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Feature.Gameplay.Vfx.Host
{
    internal sealed class VfxRendererMaterialInstanceSet
    {
        private const string RenderTypeTag = "RenderType";
        private const string TransparentRenderType = "Transparent";
        private const string SurfaceTypeTransparentKeyword = "_SURFACE_TYPE_TRANSPARENT";
        private const string AlphaTestKeyword = "_ALPHATEST_ON";
        private const string AlphaPremultiplyKeyword = "_ALPHAPREMULTIPLY_ON";
        private const string AlphaModulateKeyword = "_ALPHAMODULATE_ON";

        private static readonly int InactiveBlendId = Shader.PropertyToID("_InactiveBlend");
        private static readonly int InactiveNoiseRevealId = Shader.PropertyToID("_InactiveNoiseReveal");
        private static readonly int DesaturateStrengthId = Shader.PropertyToID("_DesaturateStrength");
        private static readonly int EmissionSuppressionId = Shader.PropertyToID("_EmissionSuppression");
        private static readonly int InactiveTintId = Shader.PropertyToID("_InactiveTint");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int SrcBlendAlphaId = Shader.PropertyToID("_SrcBlendAlpha");
        private static readonly int DstBlendAlphaId = Shader.PropertyToID("_DstBlendAlpha");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

        private readonly Material[][] originalSharedMaterials;
        private readonly bool restoreSharedMaterials;
        private readonly bool disableRendererShadows;
        private readonly ShadowCastingMode[] originalShadowCastingModes;
        private readonly bool[] originalReceiveShadows;
        private readonly Material[][] runtimeMaterials;
        private readonly Renderer[] renderers;
        private bool hasRuntimeMaterials;

        private VfxRendererMaterialInstanceSet(
            Renderer[] renderers,
            bool restoreSharedMaterials,
            bool disableRendererShadows)
        {
            this.renderers = renderers ?? Array.Empty<Renderer>();
            this.restoreSharedMaterials = restoreSharedMaterials;
            this.disableRendererShadows = disableRendererShadows;
            originalSharedMaterials = new Material[this.renderers.Length][];
            originalShadowCastingModes = new ShadowCastingMode[this.renderers.Length];
            originalReceiveShadows = new bool[this.renderers.Length];
            runtimeMaterials = new Material[this.renderers.Length][];
        }

        public Renderer[] Renderers => renderers;

        public static VfxRendererMaterialInstanceSet Create(
            Renderer[] renderers,
            bool restoreSharedMaterials = true,
            bool disableRendererShadows = false)
        {
            var set = new VfxRendererMaterialInstanceSet(
                renderers,
                restoreSharedMaterials,
                disableRendererShadows);
            set.EnsureRuntimeMaterials();
            return set;
        }

        public void ApplyAlpha(float alpha)
        {
            if (!hasRuntimeMaterials)
            {
                return;
            }

            for (var rendererIndex = 0; rendererIndex < runtimeMaterials.Length; rendererIndex++)
            {
                var materials = runtimeMaterials[rendererIndex];
                if (materials == null)
                {
                    continue;
                }

                for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    SetMaterialAlpha(materials[materialIndex], alpha);
                }
            }
        }

        public void ApplyInactiveVisualSnapshot(in VfxRendererInactiveVisualSnapshotSet snapshotSet)
        {
            if (!hasRuntimeMaterials || !snapshotSet.HasEntries)
            {
                return;
            }

            var rendererCount = Mathf.Min(runtimeMaterials.Length, snapshotSet.Count);
            for (var rendererIndex = 0; rendererIndex < rendererCount; rendererIndex++)
            {
                if (!snapshotSet.TryGetEntry(rendererIndex, out var snapshot))
                {
                    continue;
                }

                var materials = runtimeMaterials[rendererIndex];
                if (materials == null)
                {
                    continue;
                }

                for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    ApplyInactiveVisualSnapshot(materials[materialIndex], snapshot);
                }
            }
        }

        public void Clear()
        {
            if (!hasRuntimeMaterials)
            {
                return;
            }

            for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                var renderer = renderers[rendererIndex];
                if (restoreSharedMaterials && renderer != null)
                {
                    renderer.sharedMaterials = originalSharedMaterials[rendererIndex] ?? Array.Empty<Material>();
                }

                if (disableRendererShadows && renderer != null)
                {
                    renderer.shadowCastingMode = originalShadowCastingModes[rendererIndex];
                    renderer.receiveShadows = originalReceiveShadows[rendererIndex];
                }

                var materials = runtimeMaterials[rendererIndex];
                if (materials == null)
                {
                    continue;
                }

                for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    SafeDestroy(materials[materialIndex]);
                }

                runtimeMaterials[rendererIndex] = Array.Empty<Material>();
            }

            hasRuntimeMaterials = false;
        }

        private void EnsureRuntimeMaterials()
        {
            Clear();

            for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                var renderer = renderers[rendererIndex];
                if (renderer == null)
                {
                    originalSharedMaterials[rendererIndex] = Array.Empty<Material>();
                    runtimeMaterials[rendererIndex] = Array.Empty<Material>();
                    continue;
                }

                var sharedMaterials = renderer.sharedMaterials;
                originalSharedMaterials[rendererIndex] = sharedMaterials;
                originalShadowCastingModes[rendererIndex] = renderer.shadowCastingMode;
                originalReceiveShadows[rendererIndex] = renderer.receiveShadows;
                var clonedMaterials = new Material[sharedMaterials.Length];
                for (var materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
                {
                    var sharedMaterial = sharedMaterials[materialIndex];
                    if (sharedMaterial != null)
                    {
                        clonedMaterials[materialIndex] = new Material(sharedMaterial);
                    }
                }

                renderer.sharedMaterials = clonedMaterials;
                if (disableRendererShadows)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                runtimeMaterials[rendererIndex] = clonedMaterials;
            }

            hasRuntimeMaterials = true;
        }

        private static void ApplyInactiveVisualSnapshot(
            Material material,
            in VfxRendererInactiveVisualSnapshot snapshot)
        {
            if (!VfxRendererInactiveVisualSnapshotSet.MaterialSupportsInactiveContract(material))
            {
                return;
            }

            material.SetFloat(InactiveBlendId, snapshot.InactiveBlend);
            material.SetFloat(InactiveNoiseRevealId, snapshot.InactiveNoiseReveal);
            material.SetFloat(DesaturateStrengthId, snapshot.DesaturateStrength);
            material.SetFloat(EmissionSuppressionId, snapshot.EmissionSuppression);
            material.SetColor(InactiveTintId, snapshot.InactiveTint);
        }

        private static void SetMaterialAlpha(Material material, float alpha)
        {
            if (material == null)
            {
                return;
            }

            if (!material.HasProperty("_BaseColor") &&
                !material.HasProperty("_Color"))
            {
                return;
            }

            ConfigureTransparentMaterial(material);
            if (material.HasProperty("_BaseColor"))
            {
                var color = material.GetColor("_BaseColor");
                color.a = alpha;
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                var color = material.color;
                color.a = alpha;
                material.color = color;
            }
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            SetFloatIfHasProperty(material, SurfaceId, 1f);
            SetFloatIfHasProperty(material, BlendId, 0f);
            SetFloatIfHasProperty(material, SrcBlendId, (float)BlendMode.SrcAlpha);
            SetFloatIfHasProperty(material, DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
            SetFloatIfHasProperty(material, SrcBlendAlphaId, (float)BlendMode.One);
            SetFloatIfHasProperty(material, DstBlendAlphaId, (float)BlendMode.OneMinusSrcAlpha);
            SetFloatIfHasProperty(material, ZWriteId, 0f);

            if (HasTransparentSurfaceContract(material))
            {
                material.SetOverrideTag(RenderTypeTag, TransparentRenderType);
                material.EnableKeyword(SurfaceTypeTransparentKeyword);
                material.DisableKeyword(AlphaTestKeyword);
                material.DisableKeyword(AlphaPremultiplyKeyword);
                material.DisableKeyword(AlphaModulateKeyword);
            }

            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void SetFloatIfHasProperty(Material material, int propertyId, float value)
        {
            if (material.HasProperty(propertyId))
            {
                material.SetFloat(propertyId, value);
            }
        }

        private static bool HasTransparentSurfaceContract(Material material)
        {
            return material.HasProperty(SurfaceId) ||
                   material.HasProperty(BlendId) ||
                   material.HasProperty(SrcBlendId) ||
                   material.HasProperty(DstBlendId) ||
                   material.HasProperty(SrcBlendAlphaId) ||
                   material.HasProperty(DstBlendAlphaId) ||
                   material.HasProperty(ZWriteId);
        }

        private static void SafeDestroy(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
