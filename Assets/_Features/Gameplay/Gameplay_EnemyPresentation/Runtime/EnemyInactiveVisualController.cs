using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class EnemyInactiveVisualController :
        MonoBehaviour,
        IEnemyVisualSemanticPresentationDriver
    {
        private static readonly int BaseColorHash = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorHash = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorHash = Shader.PropertyToID("_EmissionColor");
        private static readonly int InactiveBlendHash = Shader.PropertyToID("_InactiveBlend");
        private static readonly int InactiveNoiseRevealHash = Shader.PropertyToID("_InactiveNoiseReveal");
        private static readonly int DesaturateStrengthHash = Shader.PropertyToID("_DesaturateStrength");
        private static readonly int EmissionSuppressionHash = Shader.PropertyToID("_EmissionSuppression");
        private static readonly int InactiveTintHash = Shader.PropertyToID("_InactiveTint");
        private const float RevealEpsilon = 0.0001f;

        [SerializeField] private Color inactiveTint = new(0.62f, 0.64f, 0.68f, 1f);
        [SerializeField] [Range(0f, 1f)] private float desaturateStrength = 0.85f;
        [SerializeField] [Range(0f, 1f)] private float emissionSuppression = 0.85f;
        [SerializeField] private bool allowLegacyColorFallback;
        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField] [Min(0.0001f)] private float inactiveRevealInSeconds = 0.25f;
        [SerializeField] [Min(0.0001f)] private float inactiveRevealOutSeconds = 0.18f;
        [SerializeField] private AnimationCurve inactiveRevealCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        private MaterialPropertyBlock _propertyBlock;
        private RendererCacheEntry[] _rendererEntries = Array.Empty<RendererCacheEntry>();
        private ParticleSystem[] _childParticleSystems = Array.Empty<ParticleSystem>();
        private TrailRenderer[] _childTrailRenderers = Array.Empty<TrailRenderer>();
        private bool _isInactiveTarget;
        private bool _isGameplayPresentationPaused;
        private bool _inactiveGateEnabled;
        private float _currentInactiveNoiseReveal;
        private float _targetInactiveNoiseReveal;

        public EnemyVisualActivityState CurrentActivityState { get; private set; }

        public float CurrentInactiveBlend { get; private set; }

        public float CurrentInactiveNoiseReveal => _currentInactiveNoiseReveal;

        public float TargetInactiveNoiseReveal => _targetInactiveNoiseReveal;

        public bool IsInactiveGateEnabled => _inactiveGateEnabled;

        public bool AllowLegacyColorFallback => allowLegacyColorFallback;

        private void Awake()
        {
            CacheRenderers();
            CacheChildEffects();
        }

        private void OnEnable()
        {
            CacheRenderers();
            CacheChildEffects();
        }

        private void Update()
        {
            if (_isGameplayPresentationPaused)
            {
                return;
            }

            AdvanceInactiveNoiseReveal(Time.deltaTime);
        }

        private void OnDisable()
        {
            ResetInactiveRevealImmediate();
        }

        public void Apply(in EnemyVisualSemanticState state)
        {
            Apply(state.ActivityState);
        }

        public void ApplyEnemyVisualSemanticState(in EnemyVisualSemanticState state)
        {
            Apply(state);
        }

        public void ResetVisual()
        {
            CurrentActivityState = EnemyVisualActivityState.Normal;
            ResetInactiveRevealImmediate();
        }

        public void ConfigureLegacyColorFallback(bool allow)
        {
            allowLegacyColorFallback = allow;
        }

        public void Configure(EnemyInactiveVisualSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            inactiveTint = settings.InactiveTint;
            desaturateStrength = settings.DesaturateStrength;
            emissionSuppression = settings.EmissionSuppression;
            inactiveRevealInSeconds = settings.InactiveRevealInSeconds;
            inactiveRevealOutSeconds = settings.InactiveRevealOutSeconds;
            inactiveRevealCurve = CloneCurve(settings.InactiveRevealCurve);
            WriteInactiveProperties();
        }

        public void SetPresentationPaused(bool paused)
        {
            _isGameplayPresentationPaused = paused;
        }

        public void AdvanceInactiveNoiseReveal(float deltaTime)
        {
            if (_isGameplayPresentationPaused)
            {
                return;
            }

            if (deltaTime < 0f)
            {
                deltaTime = 0f;
            }

            AdvanceCurrentReveal(deltaTime);

            if (!_isInactiveTarget &&
                _targetInactiveNoiseReveal <= RevealEpsilon &&
                _currentInactiveNoiseReveal <= RevealEpsilon)
            {
                _inactiveGateEnabled = false;
                _currentInactiveNoiseReveal = 0f;
            }

            WriteInactiveProperties();
        }

        private void Apply(EnemyVisualActivityState activityState)
        {
            CacheRenderers();

            CurrentActivityState = activityState;

            if (activityState == EnemyVisualActivityState.FrontFaceInactive)
            {
                _isInactiveTarget = true;
                _inactiveGateEnabled = true;
                _targetInactiveNoiseReveal = 1f;
                StopAndClearChildEffects();
                WriteInactiveProperties();
                return;
            }

            _isInactiveTarget = false;
            _targetInactiveNoiseReveal = 0f;
            if (_currentInactiveNoiseReveal > RevealEpsilon ||
                _inactiveGateEnabled)
            {
                _inactiveGateEnabled = true;
            }

            WriteInactiveProperties();
        }

        private void ResetInactiveRevealImmediate()
        {
            _isInactiveTarget = false;
            _inactiveGateEnabled = false;
            _currentInactiveNoiseReveal = 0f;
            _targetInactiveNoiseReveal = 0f;
            WriteInactiveProperties();
        }

        private void AdvanceCurrentReveal(float deltaTime)
        {
            var targetReveal = Mathf.Clamp01(_targetInactiveNoiseReveal);
            var duration = targetReveal > _currentInactiveNoiseReveal
                ? inactiveRevealInSeconds
                : inactiveRevealOutSeconds;

            if (duration <= RevealEpsilon)
            {
                _currentInactiveNoiseReveal = targetReveal;
                return;
            }

            _currentInactiveNoiseReveal = Mathf.MoveTowards(
                _currentInactiveNoiseReveal,
                targetReveal,
                deltaTime / duration);
        }

        private float EvaluateInactiveReveal(float reveal)
        {
            var clampedReveal = Mathf.Clamp01(reveal);
            if (inactiveRevealCurve == null ||
                inactiveRevealCurve.length == 0)
            {
                return clampedReveal;
            }

            return Mathf.Clamp01(inactiveRevealCurve.Evaluate(clampedReveal));
        }

        private void WriteInactiveProperties()
        {
            CacheRenderers();

            var inactiveBlend = _inactiveGateEnabled ? 1f : 0f;
            var inactiveNoiseReveal = _inactiveGateEnabled
                ? EvaluateInactiveReveal(_currentInactiveNoiseReveal)
                : 0f;
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
                _propertyBlock.SetFloat(InactiveNoiseRevealHash, inactiveNoiseReveal);
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

        private void CacheChildEffects()
        {
            _childParticleSystems = GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            _childTrailRenderers = GetComponentsInChildren<TrailRenderer>(includeInactive: true);
        }

        private void StopAndClearChildEffects()
        {
            CacheChildEffects();
            for (var i = 0; i < _childParticleSystems.Length; i++)
            {
                var particleSystem = _childParticleSystems[i];
                if (particleSystem != null)
                {
                    particleSystem.Stop(
                        withChildren: true,
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            for (var i = 0; i < _childTrailRenderers.Length; i++)
            {
                var trailRenderer = _childTrailRenderers[i];
                if (trailRenderer != null)
                {
                    trailRenderer.Clear();
                }
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
                    sharedMaterial.HasProperty(InactiveNoiseRevealHash) &&
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

        private static AnimationCurve CloneCurve(AnimationCurve source)
        {
            if (source == null ||
                source.length == 0)
            {
                return AnimationCurve.Linear(0f, 0f, 1f, 1f);
            }

            return new AnimationCurve(source.keys)
            {
                preWrapMode = source.preWrapMode,
                postWrapMode = source.postWrapMode
            };
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
