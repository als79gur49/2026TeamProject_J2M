using System;
using Game.Feature.Gameplay.Host;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    internal readonly struct VfxRendererInactiveVisualSnapshot
    {
        public VfxRendererInactiveVisualSnapshot(
            float inactiveBlend,
            float inactiveNoiseReveal,
            float desaturateStrength,
            float emissionSuppression,
            Color inactiveTint)
        {
            HasInactiveContractSnapshot = true;
            InactiveBlend = inactiveBlend;
            InactiveNoiseReveal = inactiveNoiseReveal;
            DesaturateStrength = desaturateStrength;
            EmissionSuppression = emissionSuppression;
            InactiveTint = inactiveTint;
        }

        public bool HasInactiveContractSnapshot { get; }

        public float InactiveBlend { get; }

        public float InactiveNoiseReveal { get; }

        public float DesaturateStrength { get; }

        public float EmissionSuppression { get; }

        public Color InactiveTint { get; }
    }

    internal readonly struct VfxRendererInactiveVisualSnapshotSet
    {
        private static readonly int InactiveBlendId = Shader.PropertyToID("_InactiveBlend");
        private static readonly int InactiveNoiseRevealId = Shader.PropertyToID("_InactiveNoiseReveal");
        private static readonly int DesaturateStrengthId = Shader.PropertyToID("_DesaturateStrength");
        private static readonly int EmissionSuppressionId = Shader.PropertyToID("_EmissionSuppression");
        private static readonly int InactiveTintId = Shader.PropertyToID("_InactiveTint");

        private readonly VfxRendererInactiveVisualSnapshot[] entries;

        private VfxRendererInactiveVisualSnapshotSet(
            VfxRendererInactiveVisualSnapshot[] entries,
            bool hasEntries)
        {
            this.entries = entries ?? Array.Empty<VfxRendererInactiveVisualSnapshot>();
            HasEntries = hasEntries;
        }

        public static VfxRendererInactiveVisualSnapshotSet Empty { get; } =
            new(Array.Empty<VfxRendererInactiveVisualSnapshot>(), hasEntries: false);

        public bool HasEntries { get; }

        public int Count => entries?.Length ?? 0;

        public bool TryGetEntry(int rendererIndex, out VfxRendererInactiveVisualSnapshot snapshot)
        {
            if (entries != null &&
                rendererIndex >= 0 &&
                rendererIndex < entries.Length &&
                entries[rendererIndex].HasInactiveContractSnapshot)
            {
                snapshot = entries[rendererIndex];
                return true;
            }

            snapshot = default;
            return false;
        }

        public static VfxRendererInactiveVisualSnapshotSet CaptureFromModelRoot(Transform modelRoot)
        {
            if (modelRoot == null)
            {
                return Empty;
            }

            var renderers = modelRoot.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers == null || renderers.Length == 0)
            {
                return Empty;
            }

            var block = new MaterialPropertyBlock();
            var snapshots = new VfxRendererInactiveVisualSnapshot[renderers.Length];
            var hasSnapshot = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                if (TryCaptureRenderer(renderers[i], block, out var snapshot))
                {
                    snapshots[i] = snapshot;
                    hasSnapshot = true;
                }
            }

            return hasSnapshot
                ? new VfxRendererInactiveVisualSnapshotSet(snapshots, hasEntries: true)
                : Empty;
        }

        public static bool TryCapture(
            GameplayPresentationStateStore stateStore,
            int sourceEntityId,
            out VfxRendererInactiveVisualSnapshotSet snapshotSet)
        {
            snapshotSet = Empty;
            if (stateStore == null ||
                sourceEntityId <= 0 ||
                !stateStore.ViewsByEntityId.TryGetValue(sourceEntityId, out var view) ||
                view == null ||
                view.ModelRoot == null)
            {
                return false;
            }

            snapshotSet = CaptureFromModelRoot(view.ModelRoot);
            return snapshotSet.HasEntries;
        }

        private static bool TryCaptureRenderer(
            Renderer renderer,
            MaterialPropertyBlock block,
            out VfxRendererInactiveVisualSnapshot snapshot)
        {
            snapshot = default;
            if (renderer == null ||
                block == null ||
                !RendererSupportsInactiveContract(renderer))
            {
                return false;
            }

            renderer.GetPropertyBlock(block);
            if (block.isEmpty ||
                !block.HasFloat(InactiveBlendId) ||
                !block.HasFloat(InactiveNoiseRevealId) ||
                !block.HasFloat(DesaturateStrengthId) ||
                !block.HasFloat(EmissionSuppressionId) ||
                !block.HasColor(InactiveTintId))
            {
                block.Clear();
                return false;
            }

            snapshot = new VfxRendererInactiveVisualSnapshot(
                block.GetFloat(InactiveBlendId),
                block.GetFloat(InactiveNoiseRevealId),
                block.GetFloat(DesaturateStrengthId),
                block.GetFloat(EmissionSuppressionId),
                block.GetColor(InactiveTintId));
            block.Clear();
            return true;
        }

        private static bool RendererSupportsInactiveContract(Renderer renderer)
        {
            var materials = renderer != null ? renderer.sharedMaterials : null;
            if (materials == null)
            {
                return false;
            }

            for (var i = 0; i < materials.Length; i++)
            {
                if (MaterialSupportsInactiveContract(materials[i]))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool MaterialSupportsInactiveContract(Material material)
        {
            return material != null &&
                   material.HasProperty(InactiveBlendId) &&
                   material.HasProperty(InactiveNoiseRevealId) &&
                   material.HasProperty(DesaturateStrengthId) &&
                   material.HasProperty(EmissionSuppressionId) &&
                   material.HasProperty(InactiveTintId);
        }
    }
}
