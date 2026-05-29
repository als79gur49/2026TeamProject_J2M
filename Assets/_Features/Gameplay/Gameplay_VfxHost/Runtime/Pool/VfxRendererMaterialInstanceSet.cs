using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    internal sealed class VfxRendererMaterialInstanceSet
    {
        private static readonly int InactiveBlendId = Shader.PropertyToID("_InactiveBlend");
        private static readonly int InactiveNoiseRevealId = Shader.PropertyToID("_InactiveNoiseReveal");
        private static readonly int DesaturateStrengthId = Shader.PropertyToID("_DesaturateStrength");
        private static readonly int EmissionSuppressionId = Shader.PropertyToID("_EmissionSuppression");
        private static readonly int InactiveTintId = Shader.PropertyToID("_InactiveTint");

        private readonly Material[][] originalSharedMaterials;
        private readonly bool restoreSharedMaterials;
        private readonly Material[][] runtimeMaterials;
        private readonly Renderer[] renderers;
        private bool hasRuntimeMaterials;

        private VfxRendererMaterialInstanceSet(Renderer[] renderers, bool restoreSharedMaterials)
        {
            this.renderers = renderers ?? Array.Empty<Renderer>();
            this.restoreSharedMaterials = restoreSharedMaterials;
            originalSharedMaterials = new Material[this.renderers.Length][];
            runtimeMaterials = new Material[this.renderers.Length][];
        }

        public Renderer[] Renderers => renderers;

        public static VfxRendererMaterialInstanceSet Create(Renderer[] renderers, bool restoreSharedMaterials = true)
        {
            var set = new VfxRendererMaterialInstanceSet(renderers, restoreSharedMaterials);
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

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
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
