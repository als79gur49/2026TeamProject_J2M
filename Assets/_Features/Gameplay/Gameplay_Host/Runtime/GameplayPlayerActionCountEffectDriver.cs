using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class GameplayPlayerActionCountEffectDriver : MonoBehaviour
    {
        internal const string AllIn1UiMaskShaderName = "AllIn1SpriteShader/AllIn1SpriteShaderUiMask";

        private const string HitEffectKeyword = "HITEFFECT_ON";
        private const string DistortKeyword = "DISTORT_ON";
        private const float RiseDurationSeconds = 0.12f;
        private const float TotalDurationSeconds = 0.24f;
        private const float ShaderPulseDurationSeconds = 0.20f;

        private static readonly int HitEffectBlendId = Shader.PropertyToID("_HitEffectBlend");
        private static readonly int HitEffectColorId = Shader.PropertyToID("_HitEffectColor");
        private static readonly int HitEffectGlowId = Shader.PropertyToID("_HitEffectGlow");
        private static readonly int DistortAmountId = Shader.PropertyToID("_DistortAmount");

        [SerializeField] private RectTransform motionRoot;
        [SerializeField] private Image glowImage;
        [SerializeField] private Material allIn1MaterialTemplate;
        [SerializeField] private Color hitEffectColor = new(1f, 0.8737255f, 0.58f, 1f);
        [SerializeField, Min(1f)] private float hitEffectGlow = 3f;
        [SerializeField, Min(0f)] private float peakDistortAmount = 0.08f;

        private Sequence _sequence;
        private Material _runtimeMaterial;
        private Material _authoredImageMaterial;
        private Vector2 _authoredAnchoredPosition;
        private Vector3 _authoredMotionScale = Vector3.one;
        private Vector3 _authoredGlowScale = Vector3.one;
        private Color _authoredGlowColor = Color.white;
        private float _elapsedSeconds;
        private bool _hasCapturedAuthoredState;
        private bool _isPlaying;
        private int _debugPlayCount;

        public bool IsReady =>
            motionRoot != null &&
            glowImage != null &&
            allIn1MaterialTemplate != null &&
            allIn1MaterialTemplate.shader != null &&
            string.Equals(
                allIn1MaterialTemplate.shader.name,
                AllIn1UiMaskShaderName,
                StringComparison.Ordinal);

        internal bool IsPlaying => _isPlaying;

        internal int DebugPlayCount => _debugPlayCount;

        internal float DebugElapsedSeconds => _elapsedSeconds;

        internal Vector2 DebugMotionAnchoredPosition =>
            motionRoot != null ? motionRoot.anchoredPosition : Vector2.zero;

        internal Vector3 DebugMotionLocalScale =>
            motionRoot != null ? motionRoot.localScale : Vector3.zero;

        internal Color DebugGlowColor => glowImage != null ? glowImage.color : Color.clear;

        internal Vector3 DebugGlowLocalScale =>
            glowImage != null ? glowImage.rectTransform.localScale : Vector3.zero;

        public string DescribeReadiness()
        {
            if (motionRoot == null || glowImage == null)
            {
                return $"{nameof(GameplayPlayerActionCountEffectDriver)} requires a motion root and glow image.";
            }

            if (allIn1MaterialTemplate == null || allIn1MaterialTemplate.shader == null)
            {
                return $"{nameof(GameplayPlayerActionCountEffectDriver)} requires an AllIn1 material template.";
            }

            if (!string.Equals(
                    allIn1MaterialTemplate.shader.name,
                    AllIn1UiMaskShaderName,
                    StringComparison.Ordinal))
            {
                return $"{nameof(GameplayPlayerActionCountEffectDriver)} requires shader '{AllIn1UiMaskShaderName}'.";
            }

            return $"{nameof(GameplayPlayerActionCountEffectDriver)} is ready.";
        }

        internal void PlayIncrement()
        {
            EnsureReady();
            CaptureAuthoredState();
            EnsureRuntimeMaterial();
            ResetVisuals();

            motionRoot.anchoredPosition = _authoredAnchoredPosition + new Vector2(0f, -6f);
            motionRoot.localScale = _authoredMotionScale * 0.70f;

            var glowRect = glowImage.rectTransform;
            glowRect.localScale = _authoredGlowScale * 0.65f;
            glowImage.color = WithAlpha(_authoredGlowColor, 0.90f);

            SetMaterialFloat(HitEffectBlendId, 0f);
            SetMaterialFloat(DistortAmountId, 0f);
            _runtimeMaterial.SetColor(HitEffectColorId, hitEffectColor);
            _runtimeMaterial.SetFloat(HitEffectGlowId, Mathf.Max(1f, hitEffectGlow));

            _sequence = DOTween.Sequence()
                .SetAutoKill(false)
                .Pause();
            _sequence.Append(
                motionRoot
                    .DOScale(_authoredMotionScale * 1.12f, RiseDurationSeconds)
                    .SetEase(Ease.OutBack));
            _sequence.Join(
                motionRoot
                    .DOAnchorPosY(_authoredAnchoredPosition.y + 6f, RiseDurationSeconds)
                    .SetEase(Ease.OutQuad));
            _sequence.Join(
                glowRect
                    .DOScale(_authoredGlowScale * 1.35f, TotalDurationSeconds)
                    .SetEase(Ease.OutQuad));
            _sequence.Join(glowImage.DOFade(0f, TotalDurationSeconds).SetEase(Ease.OutQuad));
            _sequence.Insert(
                RiseDurationSeconds,
                motionRoot
                    .DOScale(_authoredMotionScale, RiseDurationSeconds)
                    .SetEase(Ease.OutQuad));
            _sequence.Insert(
                RiseDurationSeconds,
                motionRoot
                    .DOAnchorPosY(_authoredAnchoredPosition.y, RiseDurationSeconds)
                    .SetEase(Ease.OutQuad));

            var shaderHalfDuration = ShaderPulseDurationSeconds * 0.5f;
            _sequence.Insert(
                0f,
                DOTween.To(
                        () => GetMaterialFloat(HitEffectBlendId),
                        value => SetMaterialFloat(HitEffectBlendId, value),
                        1f,
                        shaderHalfDuration)
                    .SetEase(Ease.OutQuad));
            _sequence.Insert(
                shaderHalfDuration,
                DOTween.To(
                        () => GetMaterialFloat(HitEffectBlendId),
                        value => SetMaterialFloat(HitEffectBlendId, value),
                        0f,
                        shaderHalfDuration)
                    .SetEase(Ease.InQuad));
            _sequence.Insert(
                0f,
                DOTween.To(
                        () => GetMaterialFloat(DistortAmountId),
                        value => SetMaterialFloat(DistortAmountId, value),
                        Mathf.Max(0f, peakDistortAmount),
                        shaderHalfDuration)
                    .SetEase(Ease.OutQuad));
            _sequence.Insert(
                shaderHalfDuration,
                DOTween.To(
                        () => GetMaterialFloat(DistortAmountId),
                        value => SetMaterialFloat(DistortAmountId, value),
                        0f,
                        shaderHalfDuration)
                    .SetEase(Ease.InQuad));

            _elapsedSeconds = 0f;
            _isPlaying = true;
            _debugPlayCount++;
        }

        internal void Advance(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            if (!_isPlaying)
            {
                return;
            }

            _elapsedSeconds += deltaTime;
            _sequence?.Goto(Mathf.Min(_elapsedSeconds, TotalDurationSeconds), andPlay: false);
            if (_elapsedSeconds >= TotalDurationSeconds)
            {
                ResetVisuals();
            }
        }

        internal void ResetVisuals()
        {
            KillSequence();
            CaptureAuthoredState();

            if (motionRoot != null)
            {
                motionRoot.anchoredPosition = _authoredAnchoredPosition;
                motionRoot.localScale = _authoredMotionScale;
            }

            if (glowImage != null)
            {
                glowImage.rectTransform.localScale = _authoredGlowScale;
                glowImage.color = _authoredGlowColor;
            }

            SetMaterialFloat(HitEffectBlendId, 0f);
            SetMaterialFloat(DistortAmountId, 0f);
            _elapsedSeconds = 0f;
            _isPlaying = false;
        }

        private void Awake()
        {
            CaptureAuthoredState();
            if (IsReady)
            {
                EnsureRuntimeMaterial();
            }

            ResetVisuals();
        }

        private void OnDisable()
        {
            ResetVisuals();
        }

        private void OnDestroy()
        {
            ResetVisuals();
            if (glowImage != null && glowImage.material == _runtimeMaterial)
            {
                glowImage.material = _authoredImageMaterial;
            }

            if (_runtimeMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_runtimeMaterial);
            }
            else
            {
                DestroyImmediate(_runtimeMaterial);
            }

            _runtimeMaterial = null;
        }

        private void EnsureReady()
        {
            if (!IsReady)
            {
                throw new InvalidOperationException(DescribeReadiness());
            }
        }

        private void CaptureAuthoredState()
        {
            if (_hasCapturedAuthoredState || motionRoot == null || glowImage == null)
            {
                return;
            }

            _authoredAnchoredPosition = motionRoot.anchoredPosition;
            _authoredMotionScale = motionRoot.localScale;
            _authoredGlowScale = glowImage.rectTransform.localScale;
            _authoredGlowColor = glowImage.color;
            _authoredImageMaterial = glowImage.material;
            _hasCapturedAuthoredState = true;
        }

        private void EnsureRuntimeMaterial()
        {
            if (_runtimeMaterial != null)
            {
                return;
            }

            _runtimeMaterial = new Material(allIn1MaterialTemplate)
            {
                name = $"{allIn1MaterialTemplate.name} (Runtime)",
                hideFlags = HideFlags.DontSave,
            };
            _runtimeMaterial.EnableKeyword(HitEffectKeyword);
            _runtimeMaterial.EnableKeyword(DistortKeyword);
            _runtimeMaterial.SetColor(HitEffectColorId, hitEffectColor);
            _runtimeMaterial.SetFloat(HitEffectGlowId, Mathf.Max(1f, hitEffectGlow));
            glowImage.material = _runtimeMaterial;
        }

        private float GetMaterialFloat(int propertyId)
        {
            return _runtimeMaterial != null && _runtimeMaterial.HasProperty(propertyId)
                ? _runtimeMaterial.GetFloat(propertyId)
                : 0f;
        }

        private void SetMaterialFloat(int propertyId, float value)
        {
            if (_runtimeMaterial != null && _runtimeMaterial.HasProperty(propertyId))
            {
                _runtimeMaterial.SetFloat(propertyId, value);
            }
        }

        private void KillSequence()
        {
            if (_sequence == null)
            {
                return;
            }

            _sequence.Kill(complete: false);
            _sequence = null;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
