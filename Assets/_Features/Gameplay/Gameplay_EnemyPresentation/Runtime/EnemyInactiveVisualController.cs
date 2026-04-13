using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class EnemyInactiveVisualController : MonoBehaviour
    {
        private static readonly int BaseColorHash = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorHash = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorHash = Shader.PropertyToID("_EmissionColor");
        private static readonly int InactiveBlendHash = Shader.PropertyToID("_InactiveBlend");
        private static readonly int DesaturateStrengthHash = Shader.PropertyToID("_DesaturateStrength");
        private static readonly int EmissionSuppressionHash = Shader.PropertyToID("_EmissionSuppression");
        private static readonly int InactiveTintHash = Shader.PropertyToID("_InactiveTint");

        [SerializeField] private Color inactiveTint = new(0.62f, 0.64f, 0.68f, 1f);
        [SerializeField] [Range(0f, 1f)] private float desaturateStrength = 0.85f;
        [SerializeField] [Range(0f, 1f)] private float emissionSuppression = 0.85f;
        [SerializeField] private bool allowLegacyColorFallback;
        [SerializeField] private Renderer[] targetRenderers;

        private MaterialPropertyBlock _propertyBlock;
        private RendererCacheEntry[] _rendererEntries = Array.Empty<RendererCacheEntry>();

        public EnemyVisualActivityState CurrentActivityState { get; private set; }

        public float CurrentInactiveBlend { get; private set; }

        public bool AllowLegacyColorFallback => allowLegacyColorFallback;

        private void Awake()
        {
            CacheRenderers();
        }

        private void OnEnable()
        {
            CacheRenderers();
        }

        public void Apply(in EnemyVisualSemanticState state)
        {
            var inactiveBlend = state.ActivityState == EnemyVisualActivityState.FrontFaceInactive
                ? 1f
                : 0f;
            Apply(state.ActivityState, inactiveBlend);
        }

        public void ResetVisual()
        {
            Apply(EnemyVisualActivityState.Normal, 0f);
        }

        public void ConfigureLegacyColorFallback(bool allow)
        {
            allowLegacyColorFallback = allow;
        }

        private void Apply(EnemyVisualActivityState activityState, float inactiveBlend)
        {
            CacheRenderers();

            CurrentActivityState = activityState;
            CurrentInactiveBlend = inactiveBlend;

            if (_rendererEntries.Length == 0)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();

            for (var i = 0; i < _rendererEntries.Length; i++)
            {
                var entry = _rendererEntries[i];
                var renderer = entry.Renderer;
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(_propertyBlock);

                _propertyBlock.SetFloat(InactiveBlendHash, inactiveBlend);
                _propertyBlock.SetFloat(DesaturateStrengthHash, desaturateStrength);
                _propertyBlock.SetFloat(EmissionSuppressionHash, emissionSuppression);
                _propertyBlock.SetColor(InactiveTintHash, inactiveTint);

                if (allowLegacyColorFallback &&
                    !entry.SupportsInactiveShaderContract &&
                    entry.HasBaseColor)
                {
                    var targetColor = ResolveInactiveColor(entry.BaseColor, inactiveBlend);
                    _propertyBlock.SetColor(entry.BaseColorPropertyId, targetColor);
                }

                if (allowLegacyColorFallback &&
                    !entry.SupportsInactiveShaderContract &&
                    entry.HasEmissionColor)
                {
                    var suppressedEmission = Color.Lerp(
                        entry.EmissionColor,
                        entry.EmissionColor * (1f - emissionSuppression),
                        inactiveBlend);
                    _propertyBlock.SetColor(EmissionColorHash, suppressedEmission);
                }

                renderer.SetPropertyBlock(_propertyBlock);
                _propertyBlock.Clear();
            }
        }

        private void CacheRenderers()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            }

            var validCount = 0;
            for (var i = 0; i < targetRenderers.Length; i++)
            {
                if (targetRenderers[i] != null)
                {
                    validCount++;
                }
            }

            if (_rendererEntries.Length == validCount)
            {
                var unchanged = true;
                var entryIndex = 0;
                for (var i = 0; i < targetRenderers.Length; i++)
                {
                    var renderer = targetRenderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    if (_rendererEntries[entryIndex].Renderer != renderer)
                    {
                        unchanged = false;
                        break;
                    }

                    entryIndex++;
                }

                if (unchanged)
                {
                    return;
                }
            }

            _rendererEntries = new RendererCacheEntry[validCount];
            var targetIndex = 0;
            for (var i = 0; i < targetRenderers.Length; i++)
            {
                var renderer = targetRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                var sharedMaterial = renderer.sharedMaterial;
                var hasBaseColor = TryResolveBaseColorPropertyId(sharedMaterial, out var baseColorPropertyId);
                _rendererEntries[targetIndex++] = new RendererCacheEntry(
                    renderer,
                    hasBaseColor,
                    baseColorPropertyId,
                    hasBaseColor ? sharedMaterial.GetColor(baseColorPropertyId) : Color.white,
                    sharedMaterial != null && sharedMaterial.HasProperty(EmissionColorHash),
                    sharedMaterial != null && sharedMaterial.HasProperty(EmissionColorHash)
                        ? sharedMaterial.GetColor(EmissionColorHash)
                        : Color.black,
                    sharedMaterial != null &&
                    sharedMaterial.HasProperty(InactiveBlendHash) &&
                    sharedMaterial.HasProperty(DesaturateStrengthHash) &&
                    sharedMaterial.HasProperty(EmissionSuppressionHash) &&
                    sharedMaterial.HasProperty(InactiveTintHash));
            }
        }

        private Color ResolveInactiveColor(Color sourceColor, float inactiveBlend)
        {
            var grayscale = ResolveGrayscale(sourceColor);
            var tinted = Color.Lerp(grayscale, inactiveTint, inactiveBlend * 0.35f);
            var desaturated = Color.Lerp(sourceColor, tinted, inactiveBlend * desaturateStrength);
            desaturated.a = sourceColor.a;
            return desaturated;
        }

        private static bool TryResolveBaseColorPropertyId(Material material, out int propertyId)
        {
            if (material != null && material.HasProperty(BaseColorHash))
            {
                propertyId = BaseColorHash;
                return true;
            }

            if (material != null && material.HasProperty(ColorHash))
            {
                propertyId = ColorHash;
                return true;
            }

            propertyId = default;
            return false;
        }

        private static Color ResolveGrayscale(Color color)
        {
            var luminance = (color.r * 0.2126f) + (color.g * 0.7152f) + (color.b * 0.0722f);
            return new Color(luminance, luminance, luminance, color.a);
        }

        private readonly struct RendererCacheEntry
        {
            public RendererCacheEntry(
                Renderer renderer,
                bool hasBaseColor,
                int baseColorPropertyId,
                Color baseColor,
                bool hasEmissionColor,
                Color emissionColor,
                bool supportsInactiveShaderContract)
            {
                Renderer = renderer;
                HasBaseColor = hasBaseColor;
                BaseColorPropertyId = baseColorPropertyId;
                BaseColor = baseColor;
                HasEmissionColor = hasEmissionColor;
                EmissionColor = emissionColor;
                SupportsInactiveShaderContract = supportsInactiveShaderContract;
            }

            public Renderer Renderer { get; }

            public bool HasBaseColor { get; }

            public int BaseColorPropertyId { get; }

            public Color BaseColor { get; }

            public bool HasEmissionColor { get; }

            public Color EmissionColor { get; }

            public bool SupportsInactiveShaderContract { get; }
        }
    }
}
