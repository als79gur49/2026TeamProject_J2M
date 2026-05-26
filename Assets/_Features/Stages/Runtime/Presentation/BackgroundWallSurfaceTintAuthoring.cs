using System;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Stages
{
    [Serializable]
    public sealed class BackgroundWallSurfaceTintTarget
    {
        [SerializeField] private Renderer renderer;
        [SerializeField] private int materialIndex;
        [SerializeField] private bool applyBaseColor = true;
        [SerializeField] private string baseColorPropertyName = "_BaseColor";
        [SerializeField] private bool applyEmission = true;
        [SerializeField] private string emissionColorPropertyName = "_EmissionColor";
        [SerializeField] private bool preserveAlpha = true;

        public Renderer Renderer => renderer;

        public int MaterialIndex => materialIndex;

        public bool ApplyBaseColor => applyBaseColor;

        public string BaseColorPropertyName => string.IsNullOrWhiteSpace(baseColorPropertyName)
            ? "_BaseColor"
            : baseColorPropertyName;

        public bool ApplyEmission => applyEmission;

        public string EmissionColorPropertyName => string.IsNullOrWhiteSpace(emissionColorPropertyName)
            ? "_EmissionColor"
            : emissionColorPropertyName;

        public bool PreserveAlpha => preserveAlpha;
    }

    [DisallowMultipleComponent]
    public sealed class BackgroundWallSurfaceTintAuthoring : MonoBehaviour
    {
        private const string LogPrefix = "[BackgroundWallSurfaceTintAuthoring]";
        private const string EmissionKeyword = "_EMISSION";

        [SerializeField] private BackgroundWallSurfaceTintProfile profile;
        [SerializeField] private BackgroundWallSurfaceTintTarget[] targets =
            Array.Empty<BackgroundWallSurfaceTintTarget>();

        private MaterialPropertyBlock _propertyBlock;
        private FaceId _lastAppliedFace;
        private bool _hasAppliedFace;

        public BackgroundWallSurfaceTintProfile Profile => profile;

        public BackgroundWallSurfaceTintTarget[] Targets => targets ?? Array.Empty<BackgroundWallSurfaceTintTarget>();

        internal FaceId DebugLastAppliedFace => _lastAppliedFace;

        internal bool DebugHasAppliedFace => _hasAppliedFace;

        public void ApplyFaceTint(FaceId face)
        {
            if (profile == null)
            {
                Debug.LogWarning($"{LogPrefix} '{name}' has no tint profile assigned.", this);
                return;
            }

            if (!profile.TryGetBaseColor(face, out var baseColor))
            {
                Debug.LogWarning($"{LogPrefix} '{name}' has no base color for face '{face}'.", this);
                return;
            }

            if (!profile.TryGetEmissionColor(face, out var emissionColor))
            {
                Debug.LogWarning($"{LogPrefix} '{name}' has no emission color for face '{face}'.", this);
                return;
            }

            var targetList = Targets;
            for (var i = 0; i < targetList.Length; i++)
            {
                ApplyTarget(face, baseColor, emissionColor, targetList[i], i);
            }

            _lastAppliedFace = face;
            _hasAppliedFace = true;
        }

        private void ApplyTarget(
            FaceId face,
            Color baseColor,
            Color emissionColor,
            BackgroundWallSurfaceTintTarget target,
            int targetIndex)
        {
            if (target == null)
            {
                Debug.LogWarning($"{LogPrefix} '{name}' target at index {targetIndex} is null and will be skipped.", this);
                return;
            }

            var targetRenderer = target.Renderer;
            if (targetRenderer == null)
            {
                Debug.LogWarning($"{LogPrefix} '{name}' target at index {targetIndex} has no renderer and will be skipped.", this);
                return;
            }

            var materialIndex = target.MaterialIndex;
            var sharedMaterials = targetRenderer.sharedMaterials;
            if (materialIndex < 0 || sharedMaterials == null || materialIndex >= sharedMaterials.Length)
            {
                Debug.LogWarning(
                    $"{LogPrefix} '{name}' target '{targetRenderer.name}' material index {materialIndex} is out of range and will be skipped.",
                    targetRenderer);
                return;
            }

            var material = sharedMaterials[materialIndex];
            if (material == null)
            {
                Debug.LogWarning(
                    $"{LogPrefix} '{name}' target '{targetRenderer.name}' material index {materialIndex} has no material and will be skipped.",
                    targetRenderer);
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(_propertyBlock, materialIndex);

            if (target.ApplyBaseColor)
            {
                ApplyColorProperty(
                    targetRenderer,
                    materialIndex,
                    material,
                    target.BaseColorPropertyName,
                    baseColor,
                    profile.PreserveMaterialAlpha || target.PreserveAlpha,
                    "base",
                    face);
            }

            if (target.ApplyEmission)
            {
                ApplyEmissionProperty(
                    targetRenderer,
                    materialIndex,
                    material,
                    target.EmissionColorPropertyName,
                    emissionColor,
                    face);
            }

            targetRenderer.SetPropertyBlock(_propertyBlock, materialIndex);
            _propertyBlock.Clear();
        }

        private void ApplyColorProperty(
            Renderer targetRenderer,
            int materialIndex,
            Material material,
            string propertyName,
            Color color,
            bool preserveAlpha,
            string channelName,
            FaceId face)
        {
            var propertyId = Shader.PropertyToID(propertyName);
            if (!material.HasProperty(propertyId))
            {
                Debug.LogWarning(
                    $"{LogPrefix} '{name}' target '{targetRenderer.name}' material index {materialIndex} has no {channelName} color property '{propertyName}' for face '{face}'.",
                    targetRenderer);
                return;
            }

            if (preserveAlpha)
            {
                var materialColor = material.GetColor(propertyId);
                color.a = materialColor.a;
            }

            _propertyBlock.SetColor(propertyId, color);
        }

        private void ApplyEmissionProperty(
            Renderer targetRenderer,
            int materialIndex,
            Material material,
            string propertyName,
            Color color,
            FaceId face)
        {
            var propertyId = Shader.PropertyToID(propertyName);
            if (!material.HasProperty(propertyId))
            {
                Debug.LogWarning(
                    $"{LogPrefix} '{name}' target '{targetRenderer.name}' material index {materialIndex} has no emission color property '{propertyName}' for face '{face}'.",
                    targetRenderer);
                return;
            }

            if (!material.IsKeywordEnabled(EmissionKeyword))
            {
                Debug.LogWarning(
                    $"{LogPrefix} '{name}' target '{targetRenderer.name}' material index {materialIndex} applies emission but material '{material.name}' does not enable {EmissionKeyword}.",
                    targetRenderer);
                return;
            }

            _propertyBlock.SetColor(propertyId, color);
        }
    }
}
