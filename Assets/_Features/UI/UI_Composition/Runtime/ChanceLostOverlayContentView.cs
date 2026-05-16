using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Composition
{
    internal sealed class ChanceLostOverlayContentView : SceneTransitionOverlayContentView
    {
        private const string ChanceSlotNamePrefix = "ChanceSlotView";
        private const string TweenRootName = "LostChanceTweenRoot";
        private const string FilledIconName = "FilledIcon";
        private const string EffectImageName = "Effect";
        private const string CrackLineNamePrefix = "CrackLine";
        private const string AllIn1UiMaskShaderName = "AllIn1SpriteShader/AllIn1SpriteShaderUiMask";
        private const string AllIn1HitEffectKeyword = "HITEFFECT_ON";
        private const string AllIn1DistortKeyword = "DISTORT_ON";
        private const string AllIn1FadeKeyword = "FADE_ON";
        private const string AllIn1GreyscaleKeyword = "GREYSCALE_ON";
        private static readonly int HitEffectBlendId = Shader.PropertyToID("_HitEffectBlend");
        private static readonly int HitEffectColorId = Shader.PropertyToID("_HitEffectColor");
        private static readonly int HitEffectGlowId = Shader.PropertyToID("_HitEffectGlow");
        private static readonly int DistortAmountId = Shader.PropertyToID("_DistortAmount");
        private static readonly int DistortTexXSpeedId = Shader.PropertyToID("_DistortTexXSpeed");
        private static readonly int DistortTexYSpeedId = Shader.PropertyToID("_DistortTexYSpeed");
        private static readonly int FadeAmountId = Shader.PropertyToID("_FadeAmount");
        private static readonly int FadeBurnWidthId = Shader.PropertyToID("_FadeBurnWidth");
        private static readonly int FadeBurnTransitionId = Shader.PropertyToID("_FadeBurnTransition");
        private static readonly int FadeBurnColorId = Shader.PropertyToID("_FadeBurnColor");
        private static readonly int FadeBurnGlowId = Shader.PropertyToID("_FadeBurnGlow");
        private static readonly int GreyscaleBlendId = Shader.PropertyToID("_GreyscaleBlend");
        private static readonly int GreyscaleTintColorId = Shader.PropertyToID("_GreyscaleTintColor");
        private static readonly int GreyscaleLuminosityId = Shader.PropertyToID("_GreyscaleLuminosity");

        [SerializeField] private TMP_Text _previousChanceText;
        [SerializeField] private TMP_Text _currentChanceText;
        [SerializeField] private TMP_Text _totalChanceText;
        [SerializeField] private TMP_Text _deathCountText;
        [SerializeField] private RectTransform[] _chanceSlotRoots;
        [SerializeField] private float _lostShakeDurationSeconds = 0.44f;
        [SerializeField] private float _lostShakeStrength = 20f;
        [SerializeField] private int _lostShakeVibrato = 18;
        [SerializeField] private float _lostFallDistance = 190f;
        [SerializeField] private float _lostFallDurationSeconds = 0.66f;
        [SerializeField] private float _lostFadeDurationSeconds = 0.5f;
        [SerializeField] private float _lostRotationDegrees = -22f;
        [SerializeField] private float _lostSlotStaggerSeconds = 0.05f;
        [SerializeField] private float _emptySlotAlpha = 0.34f;
        [SerializeField] private float _lostImpactScalePunch = 0.14f;
        [SerializeField] private float _lostImpactDurationSeconds = 0.24f;
        [SerializeField] private float _lostImpactFlashAlpha = 0.9f;
        [SerializeField] private float _survivorPulseDelaySeconds = 0.08f;
        [SerializeField] private float _survivorPulseScalePunch = 0.07f;
        [SerializeField] private float _survivorPulseDurationSeconds = 0.22f;
        [SerializeField] private float _currentTextPulseScalePunch = 0.12f;
        [SerializeField] private float _currentTextPulseDurationSeconds = 0.24f;
        [SerializeField] private float _previousTextDimAlpha = 0.45f;
        [SerializeField] private float _previousTextDimDurationSeconds = 0.18f;
        [SerializeField] private bool _useAllIn1LostImpactEffect = true;
        [SerializeField] private Material _allIn1EffectMaterialTemplate;
        [SerializeField] private Color _allIn1HitEffectColor = new(1f, 0.18f, 0.24f, 1f);
        [SerializeField] private float _allIn1HitEffectGlow = 3f;
        [SerializeField] private float _allIn1DistortAmount = 0.16f;
        [SerializeField] private float _allIn1DistortDurationSeconds = 0.34f;
        [SerializeField] private float _allIn1DistortTexSpeed = 4f;
        [SerializeField] private Color _lostFilledIconDecayColor = new(0.34f, 0.21f, 0.18f, 1f);
        [SerializeField] private Color _lostFilledIconBurnColor = new(0.9f, 0.17f, 0.1f, 1f);
        [SerializeField] private float _lostFilledIconFadeAmount = 0.64f;
        [SerializeField] private float _lostFilledIconGreyscaleBlend = 0.85f;
        [SerializeField] private float _lostFilledIconDistortAmount = 0.08f;
        [SerializeField] private Color _crackLineColor = new(0.08f, 0.02f, 0.015f, 0.78f);
        [SerializeField] private float _crackLineRevealDelaySeconds = 0.04f;
        [SerializeField] private float _crackLineRevealDurationSeconds = 0.27f;
        [SerializeField] private bool _useUnscaledTime = true;

        private readonly List<SlotState> _slotStates = new();
        private readonly List<RectTransform> _resolvedSlots = new();
        private Sequence _lostChanceSequence;
        private SceneTransitionOverlayViewModel _boundModel;
        private TextVisualState _previousChanceTextState;
        private TextVisualState _currentChanceTextState;
        private Shader _allIn1UiMaskShader;
        private bool _hasBoundModel;
        private bool _hasResolvedAllIn1UiMaskShader;

        public override void Bind(SceneTransitionOverlayViewModel model)
        {
            KillLostChanceAnimation();
            RestoreChanceSlots();
            RestoreChanceTextVisuals();
            base.Bind(model);
            _boundModel = model;
            _hasBoundModel = true;

            if (!model.HasChanceLost)
            {
                ClearChanceTexts();
                ApplyChanceSlotState(model);
                return;
            }

            SetText(_previousChanceText, model.PreviousRemainingChances.ToString());
            SetText(_currentChanceText, model.CurrentRemainingChances.ToString());
            SetText(_totalChanceText, $"/ {model.TotalChances}");
            SetText(_deathCountText, $"Deaths {model.DeathCount}");
            ApplyChanceSlotState(model);
        }

        public override void Show()
        {
            base.Show();
            PlayLostChanceAnimation();
        }

        public override void Hide()
        {
            KillLostChanceAnimation();
            RestoreChanceSlots();
            RestoreChanceTextVisuals();
            base.Hide();
        }

        public override void ResetView()
        {
            KillLostChanceAnimation();
            RestoreChanceSlots();
            RestoreChanceTextVisuals();
            _hasBoundModel = false;
            base.ResetView();
            ClearChanceTexts();
        }

        internal override IReadOnlyList<string> CollectValidationIssues()
        {
            var issues = new List<string>(base.CollectValidationIssues());
            AddMissing(issues, _previousChanceText, nameof(_previousChanceText));
            AddMissing(issues, _currentChanceText, nameof(_currentChanceText));
            AddMissing(issues, _totalChanceText, nameof(_totalChanceText));
            AddMissing(issues, _deathCountText, nameof(_deathCountText));
            if (ResolveChanceSlots().Count == 0)
            {
                issues.Add("ChanceLostOverlayContentView has no chance slot roots for lost chance animation.");
            }

            return issues;
        }

        private void OnDisable()
        {
            KillLostChanceAnimation();
            RestoreChanceSlots();
            RestoreChanceTextVisuals();
        }

        private void OnDestroy()
        {
            KillLostChanceAnimation();
            RestoreChanceSlots();
        }

        internal int ActiveLostChanceAnimationCountForTests => _lostChanceSequence != null && _lostChanceSequence.IsActive() ? 1 : 0;

        internal int ResolvedChanceSlotCountForTests => ResolveChanceSlots().Count;

        private void ClearChanceTexts()
        {
            SetText(_previousChanceText, string.Empty);
            SetText(_currentChanceText, string.Empty);
            SetText(_totalChanceText, string.Empty);
            SetText(_deathCountText, string.Empty);
        }

        private void ApplyChanceSlotState(SceneTransitionOverlayViewModel model)
        {
            var slots = ResolveChanceSlots();
            if (slots.Count == 0)
            {
                return;
            }

            EnsureSlotStates(slots);
            var totalChances = Mathf.Max(model.TotalChances, model.PreviousRemainingChances, model.CurrentRemainingChances);
            var currentRemaining = Mathf.Clamp(model.CurrentRemainingChances, 0, slots.Count);
            var previousRemaining = Mathf.Clamp(model.PreviousRemainingChances, currentRemaining, slots.Count);
            for (var i = 0; i < slots.Count; i++)
            {
                var state = _slotStates[i];
                RestoreSlot(state);
                var isInAuthoredChanceRange = i < totalChances;
                var isCurrentlyFilled = i < currentRemaining;
                var isLostThisTransition = i >= currentRemaining && i < previousRemaining;
                state.Rect.gameObject.SetActive(isInAuthoredChanceRange);
                state.CanvasGroup.alpha = isCurrentlyFilled || isLostThisTransition ? state.Alpha : Mathf.Clamp01(_emptySlotAlpha);
            }
        }

        private void PlayLostChanceAnimation()
        {
            KillLostChanceAnimation();
            if (!_hasBoundModel || !_boundModel.HasChanceLost)
            {
                return;
            }

            var slots = ResolveChanceSlots();
            if (slots.Count == 0)
            {
                return;
            }

            EnsureSlotStates(slots);
            var lostStart = Mathf.Clamp(_boundModel.CurrentRemainingChances, 0, slots.Count);
            var lostEndExclusive = Mathf.Clamp(_boundModel.PreviousRemainingChances, lostStart, slots.Count);
            if (lostEndExclusive <= lostStart)
            {
                return;
            }

            CaptureChanceTextVisuals();
            _lostChanceSequence = DOTween.Sequence()
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

            InsertSurvivorPulseTweens(lostStart);
            InsertChanceTextTweens();

            for (var i = lostStart; i < lostEndExclusive; i++)
            {
                var state = _slotStates[i];
                RestoreSlot(state);
                state.Rect.gameObject.SetActive(true);
                state.CanvasGroup.alpha = state.Alpha;

                var slotSequence = DOTween.Sequence()
                    .SetUpdate(_useUnscaledTime)
                    .AppendInterval((i - lostStart) * Mathf.Max(0f, _lostSlotStaggerSeconds))
                    .Append(CreateLostImpactTween(state))
                    .Append(CreateHorizontalShakeTween(state))
                    .Append(CreateAnchorMoveTween(
                            state.TweenRect,
                            new Vector2(
                                state.TweenAnchoredPosition.x,
                                state.TweenAnchoredPosition.y - Mathf.Max(0f, _lostFallDistance)),
                            Mathf.Max(0.01f, _lostFallDurationSeconds))
                        .SetEase(Ease.InCubic))
                    .Join(DOTween.To(
                            () => state.CanvasGroup.alpha,
                            value => state.CanvasGroup.alpha = value,
                            0f,
                            Mathf.Max(0.01f, _lostFadeDurationSeconds))
                        .SetEase(Ease.InQuad))
                    .Join(DOTween.To(
                            () => state.TweenRect.localEulerAngles,
                            value => state.TweenRect.localEulerAngles = value,
                            new Vector3(0f, 0f, _lostRotationDegrees),
                            Mathf.Max(0.01f, _lostFallDurationSeconds))
                        .SetEase(Ease.InQuad))
                    .Join(CreateFilledIconBreakTween(state, Mathf.Max(0.01f, _lostFallDurationSeconds)));

                _lostChanceSequence.Join(slotSequence);
            }
        }

        private void InsertSurvivorPulseTweens(int survivorEndExclusive)
        {
            var delay = Mathf.Max(0f, _survivorPulseDelaySeconds);
            var duration = Mathf.Max(0.01f, _survivorPulseDurationSeconds);
            var scalePunch = Mathf.Max(0f, _survivorPulseScalePunch);
            if (scalePunch <= 0f)
            {
                return;
            }

            for (var i = 0; i < survivorEndExclusive && i < _slotStates.Count; i++)
            {
                var state = _slotStates[i];
                if (state.TweenRect == null || !state.Rect.gameObject.activeInHierarchy)
                {
                    continue;
                }

                _lostChanceSequence.Insert(
                    delay,
                    state.TweenRect
                        .DOPunchScale(Vector3.one * scalePunch, duration, 8, 0.7f)
                        .SetEase(Ease.OutQuad));
            }
        }

        private void InsertChanceTextTweens()
        {
            if (_currentChanceText != null && _currentChanceText.rectTransform != null)
            {
                _lostChanceSequence.Insert(
                    Mathf.Max(0f, _survivorPulseDelaySeconds),
                    _currentChanceText.rectTransform
                        .DOPunchScale(
                            Vector3.one * Mathf.Max(0f, _currentTextPulseScalePunch),
                            Mathf.Max(0.01f, _currentTextPulseDurationSeconds),
                            8,
                            0.7f)
                        .SetEase(Ease.OutQuad));
            }

            if (_previousChanceText == null)
            {
                return;
            }

            var target = _previousChanceText.color;
            target.a = Mathf.Clamp01(_previousTextDimAlpha);
            _lostChanceSequence.Insert(
                0f,
                DOTween.To(
                        () => _previousChanceText.color,
                        value => _previousChanceText.color = value,
                        target,
                        Mathf.Max(0.01f, _previousTextDimDurationSeconds))
                    .SetEase(Ease.OutQuad));
        }

        private Tween CreateLostImpactTween(SlotState state)
        {
            var impact = DOTween.Sequence().SetUpdate(_useUnscaledTime);
            var duration = Mathf.Max(0.01f, _lostImpactDurationSeconds);
            var scalePunch = Mathf.Max(0f, _lostImpactScalePunch);
            if (state.TweenRect != null && scalePunch > 0f)
            {
                impact.Join(
                    state.TweenRect
                        .DOPunchScale(Vector3.one * scalePunch, duration, 8, 0.7f)
                        .SetEase(Ease.OutQuad));
            }

            for (var i = 0; i < state.FlashImages.Count; i++)
            {
                var image = state.FlashImages[i];
                if (image == null)
                {
                    continue;
                }

                var authoredColor = state.FlashImageColors[i];
                var flashColor = authoredColor;
                flashColor.a = Mathf.Max(authoredColor.a, Mathf.Clamp01(_lostImpactFlashAlpha));
                var flash = DOTween.Sequence().SetUpdate(_useUnscaledTime)
                    .Append(DOTween.To(
                            () => image.color,
                            value => image.color = value,
                            flashColor,
                            duration * 0.4f)
                        .SetEase(Ease.OutQuad))
                    .Append(DOTween.To(
                            () => image.color,
                            value => image.color = value,
                            authoredColor,
                            duration * 0.6f)
                        .SetEase(Ease.InQuad));
                impact.Join(flash);

                var allIn1Material = PrepareAllIn1Material(state, i);
                if (allIn1Material != null)
                {
                    impact.Join(CreateAllIn1LostImpactTween(allIn1Material, duration));
                }
            }

            return impact;
        }

        private Material PrepareAllIn1Material(SlotState state, int flashImageIndex)
        {
            var shader = ResolveAllIn1UiMaskShader();
            if (shader == null)
            {
                return null;
            }

            var material = state.GetOrCreateAllIn1Material(flashImageIndex, _allIn1EffectMaterialTemplate, shader);
            if (material == null)
            {
                return null;
            }

            material.EnableKeyword(AllIn1HitEffectKeyword);
            material.EnableKeyword(AllIn1DistortKeyword);
            SetColorIfPresent(material, HitEffectColorId, _allIn1HitEffectColor);
            SetFloatIfPresent(material, HitEffectGlowId, Mathf.Max(1f, _allIn1HitEffectGlow));
            SetFloatIfPresent(material, HitEffectBlendId, 0f);
            SetFloatIfPresent(material, DistortAmountId, 0f);
            SetFloatIfPresent(material, DistortTexXSpeedId, _allIn1DistortTexSpeed);
            SetFloatIfPresent(material, DistortTexYSpeedId, -_allIn1DistortTexSpeed);
            return material;
        }

        private Tween CreateFilledIconBreakTween(SlotState state, float duration)
        {
            var sequence = DOTween.Sequence().SetUpdate(_useUnscaledTime);
            var decayColor = _lostFilledIconDecayColor;
            var shader = ResolveAllIn1UiMaskShader();
            for (var i = 0; i < state.FilledImages.Count; i++)
            {
                var image = state.FilledImages[i];
                if (image == null)
                {
                    continue;
                }

                sequence.Join(
                    DOTween.To(
                            () => image.color,
                            value => image.color = value,
                            decayColor,
                            duration * 0.72f)
                        .SetEase(Ease.InQuad));

                var material = shader != null
                    ? state.GetOrCreateFilledIconMaterial(i, _allIn1EffectMaterialTemplate, shader)
                    : null;
                if (material != null)
                {
                    PrepareAllIn1FilledIconMaterial(material);
                    sequence.Join(CreateFilledIconMaterialBreakTween(material, duration));
                }
            }

            var crackDelay = Mathf.Max(0f, _crackLineRevealDelaySeconds);
            var crackDuration = Mathf.Max(0.01f, _crackLineRevealDurationSeconds);
            for (var i = 0; i < state.CrackLineImages.Count; i++)
            {
                var line = state.CrackLineImages[i];
                if (line == null)
                {
                    continue;
                }

                var targetColor = _crackLineColor;
                var transparentColor = targetColor;
                transparentColor.a = 0f;
                line.color = transparentColor;
                sequence.Insert(
                    crackDelay + i * 0.018f,
                    DOTween.To(
                            () => line.color,
                            value => line.color = value,
                            targetColor,
                            crackDuration)
                        .SetEase(Ease.OutQuad));

                if (line.rectTransform != null)
                {
                    line.rectTransform.localScale = new Vector3(0.32f, 1f, 1f);
                    sequence.Insert(
                        crackDelay + i * 0.018f,
                        line.rectTransform
                            .DOScaleX(1f, crackDuration)
                            .SetEase(Ease.OutBack));
                }
            }

            return sequence;
        }

        private void PrepareAllIn1FilledIconMaterial(Material material)
        {
            material.EnableKeyword(AllIn1FadeKeyword);
            material.EnableKeyword(AllIn1GreyscaleKeyword);
            material.EnableKeyword(AllIn1DistortKeyword);
            SetFloatIfPresent(material, FadeAmountId, -0.08f);
            SetFloatIfPresent(material, FadeBurnWidthId, 0.035f);
            SetFloatIfPresent(material, FadeBurnTransitionId, 0.08f);
            SetColorIfPresent(material, FadeBurnColorId, _lostFilledIconBurnColor);
            SetFloatIfPresent(material, FadeBurnGlowId, 2.4f);
            SetFloatIfPresent(material, GreyscaleBlendId, 0f);
            SetColorIfPresent(material, GreyscaleTintColorId, _lostFilledIconDecayColor);
            SetFloatIfPresent(material, GreyscaleLuminosityId, -0.18f);
            SetFloatIfPresent(material, DistortAmountId, 0f);
            SetFloatIfPresent(material, DistortTexXSpeedId, _allIn1DistortTexSpeed * 0.45f);
            SetFloatIfPresent(material, DistortTexYSpeedId, _allIn1DistortTexSpeed * 0.35f);
        }

        private Tween CreateFilledIconMaterialBreakTween(Material material, float duration)
        {
            var sequence = DOTween.Sequence().SetUpdate(_useUnscaledTime);
            if (material.HasProperty(FadeAmountId))
            {
                sequence.Join(
                    DOTween.To(
                            () => material != null ? material.GetFloat(FadeAmountId) : -0.08f,
                            value => SetFloatIfPresent(material, FadeAmountId, value),
                            Mathf.Clamp(_lostFilledIconFadeAmount, -0.08f, 1f),
                            duration)
                        .SetEase(Ease.InQuad));
            }

            if (material.HasProperty(GreyscaleBlendId))
            {
                sequence.Join(
                    DOTween.To(
                            () => material != null ? material.GetFloat(GreyscaleBlendId) : 0f,
                            value => SetFloatIfPresent(material, GreyscaleBlendId, value),
                            Mathf.Clamp01(_lostFilledIconGreyscaleBlend),
                            duration * 0.8f)
                        .SetEase(Ease.OutQuad));
            }

            if (material.HasProperty(DistortAmountId))
            {
                sequence.Join(
                    DOTween.To(
                            () => material != null ? material.GetFloat(DistortAmountId) : 0f,
                            value => SetFloatIfPresent(material, DistortAmountId, value),
                            Mathf.Max(0f, _lostFilledIconDistortAmount),
                            duration * 0.42f)
                        .SetEase(Ease.OutQuad)
                        .SetLoops(2, LoopType.Yoyo));
            }

            return sequence;
        }

        private Tween CreateAllIn1LostImpactTween(Material material, float baseDuration)
        {
            var sequence = DOTween.Sequence().SetUpdate(_useUnscaledTime);
            if (material.HasProperty(HitEffectBlendId))
            {
                var hitDuration = Mathf.Max(0.01f, baseDuration * 0.5f);
                sequence.Join(
                    DOTween.To(
                            () => material != null ? material.GetFloat(HitEffectBlendId) : 0f,
                            value => SetFloatIfPresent(material, HitEffectBlendId, value),
                            1f,
                            hitDuration)
                        .SetEase(Ease.OutQuad)
                        .SetLoops(2, LoopType.Yoyo));
            }

            if (material.HasProperty(DistortAmountId))
            {
                var distortDuration = Mathf.Max(0.01f, _allIn1DistortDurationSeconds * 0.5f);
                sequence.Join(
                    DOTween.To(
                            () => material != null ? material.GetFloat(DistortAmountId) : 0f,
                            value => SetFloatIfPresent(material, DistortAmountId, value),
                            Mathf.Max(0f, _allIn1DistortAmount),
                            distortDuration)
                        .SetEase(Ease.OutQuad)
                        .SetLoops(2, LoopType.Yoyo));
            }

            return sequence;
        }

        private Shader ResolveAllIn1UiMaskShader()
        {
            if (!_useAllIn1LostImpactEffect)
            {
                return null;
            }

            if (_allIn1EffectMaterialTemplate != null &&
                _allIn1EffectMaterialTemplate.shader != null &&
                _allIn1EffectMaterialTemplate.shader.name == AllIn1UiMaskShaderName)
            {
                return _allIn1EffectMaterialTemplate.shader;
            }

            if (!_hasResolvedAllIn1UiMaskShader)
            {
                _allIn1UiMaskShader = Shader.Find(AllIn1UiMaskShaderName);
                _hasResolvedAllIn1UiMaskShader = true;
            }

            return _allIn1UiMaskShader;
        }

        private static void SetFloatIfPresent(Material material, int propertyId, float value)
        {
            if (material != null && material.HasProperty(propertyId))
            {
                material.SetFloat(propertyId, value);
            }
        }

        private static void SetColorIfPresent(Material material, int propertyId, Color value)
        {
            if (material != null && material.HasProperty(propertyId))
            {
                material.SetColor(propertyId, value);
            }
        }

        private IReadOnlyList<RectTransform> ResolveChanceSlots()
        {
            _resolvedSlots.Clear();
            if (_chanceSlotRoots != null)
            {
                for (var i = 0; i < _chanceSlotRoots.Length; i++)
                {
                    if (_chanceSlotRoots[i] != null)
                    {
                        _resolvedSlots.Add(_chanceSlotRoots[i]);
                    }
                }
            }

            if (_resolvedSlots.Count > 0)
            {
                return _resolvedSlots;
            }

            var children = GetComponentsInChildren<RectTransform>(true);
            for (var i = 0; i < children.Length; i++)
            {
                var child = children[i];
                if (child == null || !child.name.StartsWith(ChanceSlotNamePrefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _resolvedSlots.Add(child);
            }

            _resolvedSlots.Sort(CompareChanceSlotNames);
            return _resolvedSlots;
        }

        private Tween CreateHorizontalShakeTween(SlotState state)
        {
            var shake = DOTween.Sequence();
            var vibrato = Mathf.Max(1, _lostShakeVibrato);
            var stepDuration = Mathf.Max(0.01f, _lostShakeDurationSeconds) / vibrato;
            var strength = Mathf.Max(0f, _lostShakeStrength);
            for (var i = 0; i < vibrato; i++)
            {
                var direction = i % 2 == 0 ? 1f : -1f;
                var falloff = 1f - (i / (float)vibrato);
                var target = state.TweenAnchoredPosition + new Vector2(direction * strength * falloff, 0f);
                shake.Append(CreateAnchorMoveTween(state.TweenRect, target, stepDuration).SetEase(Ease.OutQuad));
            }

            shake.Append(CreateAnchorMoveTween(state.TweenRect, state.TweenAnchoredPosition, stepDuration).SetEase(Ease.OutQuad));
            return shake;
        }

        private static Tween CreateAnchorMoveTween(RectTransform rect, Vector2 target, float duration)
        {
            return DOTween.To(
                () => rect.anchoredPosition,
                value => rect.anchoredPosition = value,
                target,
                duration);
        }

        private void EnsureSlotStates(IReadOnlyList<RectTransform> slots)
        {
            if (_slotStates.Count == slots.Count)
            {
                var sameSlots = true;
                for (var i = 0; i < slots.Count; i++)
                {
                    sameSlots &= _slotStates[i].Rect == slots[i];
                }

                if (sameSlots)
                {
                    return;
                }
            }

            _slotStates.Clear();
            for (var i = 0; i < slots.Count; i++)
            {
                var rect = slots[i];
                var canvasGroup = rect.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = rect.gameObject.AddComponent<CanvasGroup>();
                }

                _slotStates.Add(new SlotState(rect, ResolveTweenRoot(rect), canvasGroup));
            }
        }

        private void RestoreChanceSlots()
        {
            for (var i = 0; i < _slotStates.Count; i++)
            {
                RestoreSlot(_slotStates[i]);
            }
        }

        private void CaptureChanceTextVisuals()
        {
            _previousChanceTextState = TextVisualState.Capture(_previousChanceText);
            _currentChanceTextState = TextVisualState.Capture(_currentChanceText);
        }

        private void RestoreChanceTextVisuals()
        {
            RestoreTextVisualState(_previousChanceTextState);
            RestoreTextVisualState(_currentChanceTextState);
        }

        private static void RestoreTextVisualState(TextVisualState state)
        {
            if (state?.Text == null)
            {
                return;
            }

            state.Text.color = state.Color;
            if (state.Text.rectTransform != null)
            {
                state.Text.rectTransform.localScale = state.LocalScale;
            }
        }

        private static void RestoreSlot(SlotState state)
        {
            if (state?.Rect == null || state.CanvasGroup == null)
            {
                return;
            }

            if (state.TweenRect != null)
            {
                state.TweenRect.anchoredPosition = state.TweenAnchoredPosition;
                state.TweenRect.localRotation = state.TweenLocalRotation;
                state.TweenRect.localScale = state.TweenLocalScale;
            }

            state.RestoreFlashImages();

            state.CanvasGroup.alpha = state.Alpha;
            state.Rect.gameObject.SetActive(state.ActiveSelf);
        }

        private void KillLostChanceAnimation()
        {
            if (_lostChanceSequence == null)
            {
                return;
            }

            _lostChanceSequence.Kill(false);
            _lostChanceSequence = null;
        }

        private static int CompareChanceSlotNames(RectTransform left, RectTransform right)
        {
            return ParseTrailingIndex(left.name).CompareTo(ParseTrailingIndex(right.name));
        }

        private static int ParseTrailingIndex(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return int.MaxValue;
            }

            var end = value.Length - 1;
            while (end >= 0 && char.IsWhiteSpace(value[end]))
            {
                end--;
            }

            var start = end;
            while (start >= 0 && char.IsDigit(value[start]))
            {
                start--;
            }

            return start == end || !int.TryParse(value.Substring(start + 1, end - start), out var index)
                ? int.MaxValue
                : index;
        }

        private static RectTransform ResolveTweenRoot(RectTransform slotRoot)
        {
            var existing = slotRoot.Find(TweenRootName);
            if (existing is RectTransform existingRect)
            {
                return existingRect;
            }

            var childCount = slotRoot.childCount;
            var children = new Transform[childCount];
            for (var i = 0; i < childCount; i++)
            {
                children[i] = slotRoot.GetChild(i);
            }

            var tweenRootObject = new GameObject(TweenRootName, typeof(RectTransform));
            var tweenRoot = tweenRootObject.GetComponent<RectTransform>();
            tweenRoot.SetParent(slotRoot, false);
            tweenRoot.SetAsFirstSibling();
            Stretch(tweenRoot);

            for (var i = 0; i < children.Length; i++)
            {
                if (children[i] != null && children[i] != tweenRoot)
                {
                    children[i].SetParent(tweenRoot, false);
                }
            }

            return tweenRoot;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        private sealed class SlotState
        {
            public SlotState(RectTransform rect, RectTransform tweenRect, CanvasGroup canvasGroup)
            {
                Rect = rect;
                TweenRect = tweenRect;
                CanvasGroup = canvasGroup;
                TweenAnchoredPosition = tweenRect != null ? tweenRect.anchoredPosition : Vector2.zero;
                TweenLocalRotation = tweenRect != null ? tweenRect.localRotation : Quaternion.identity;
                TweenLocalScale = tweenRect != null ? tweenRect.localScale : Vector3.one;
                Alpha = canvasGroup.alpha;
                ActiveSelf = rect.gameObject.activeSelf;
                FlashImages = ResolveFlashImages(rect);
                FlashImageColors = CaptureImageColors(FlashImages);
                FlashImageMaterials = CaptureImageMaterials(FlashImages);
                FilledImages = ResolveFilledImages(rect);
                FilledImageColors = CaptureImageColors(FilledImages);
                FilledImageMaterials = CaptureImageMaterials(FilledImages);
                CrackLineImages = ResolveCrackLineImages(tweenRect);
                CrackLineColors = CaptureImageColors(CrackLineImages);
                CrackLineLocalScales = CaptureImageLocalScales(CrackLineImages);
                _allIn1RuntimeMaterials = new Material[FlashImages.Count];
                _filledIconRuntimeMaterials = new Material[FilledImages.Count];
            }

            public RectTransform Rect { get; }

            public RectTransform TweenRect { get; }

            public CanvasGroup CanvasGroup { get; }

            public Vector2 TweenAnchoredPosition { get; }

            public Quaternion TweenLocalRotation { get; }

            public Vector3 TweenLocalScale { get; }

            public float Alpha { get; }

            public bool ActiveSelf { get; }

            public IReadOnlyList<Image> FlashImages { get; }

            public IReadOnlyList<Color> FlashImageColors { get; }

            public IReadOnlyList<Material> FlashImageMaterials { get; }

            public IReadOnlyList<Image> FilledImages { get; }

            public IReadOnlyList<Color> FilledImageColors { get; }

            public IReadOnlyList<Material> FilledImageMaterials { get; }

            public IReadOnlyList<Image> CrackLineImages { get; }

            public IReadOnlyList<Color> CrackLineColors { get; }

            public IReadOnlyList<Vector3> CrackLineLocalScales { get; }

            private Material[] _allIn1RuntimeMaterials { get; }

            private Material[] _filledIconRuntimeMaterials { get; }

            public Material GetOrCreateAllIn1Material(int imageIndex, Material template, Shader fallbackShader)
            {
                if (imageIndex < 0 || imageIndex >= FlashImages.Count || FlashImages[imageIndex] == null || fallbackShader == null)
                {
                    return null;
                }

                if (_allIn1RuntimeMaterials[imageIndex] != null)
                {
                    return _allIn1RuntimeMaterials[imageIndex];
                }

                var sourceMaterial = ResolveAllIn1SourceMaterial(template, FlashImageMaterials[imageIndex], fallbackShader);
                var material = sourceMaterial != null ? new Material(sourceMaterial) : new Material(fallbackShader);
                material.name = $"{FlashImages[imageIndex].name} Runtime AllIn1";
                material.hideFlags = HideFlags.DontSave;
                _allIn1RuntimeMaterials[imageIndex] = material;
                FlashImages[imageIndex].material = material;
                return material;
            }

            public Material GetOrCreateFilledIconMaterial(int imageIndex, Material template, Shader fallbackShader)
            {
                if (imageIndex < 0 || imageIndex >= FilledImages.Count || FilledImages[imageIndex] == null || fallbackShader == null)
                {
                    return null;
                }

                if (_filledIconRuntimeMaterials[imageIndex] != null)
                {
                    return _filledIconRuntimeMaterials[imageIndex];
                }

                var sourceMaterial = ResolveAllIn1SourceMaterial(template, FilledImageMaterials[imageIndex], fallbackShader);
                var material = sourceMaterial != null ? new Material(sourceMaterial) : new Material(fallbackShader);
                material.name = $"{FilledImages[imageIndex].name} Runtime Break AllIn1";
                material.hideFlags = HideFlags.DontSave;
                _filledIconRuntimeMaterials[imageIndex] = material;
                FilledImages[imageIndex].material = material;
                return material;
            }

            public void RestoreFlashImages()
            {
                for (var i = 0; i < FlashImages.Count; i++)
                {
                    if (FlashImages[i] != null)
                    {
                        FlashImages[i].color = FlashImageColors[i];
                        FlashImages[i].material = FlashImageMaterials[i];
                    }

                    DestroyRuntimeMaterial(i);
                }

                for (var i = 0; i < FilledImages.Count; i++)
                {
                    if (FilledImages[i] != null)
                    {
                        FilledImages[i].color = FilledImageColors[i];
                        FilledImages[i].material = FilledImageMaterials[i];
                    }

                    DestroyFilledIconRuntimeMaterial(i);
                }

                for (var i = 0; i < CrackLineImages.Count; i++)
                {
                    if (CrackLineImages[i] == null)
                    {
                        continue;
                    }

                    CrackLineImages[i].color = CrackLineColors[i];
                    if (CrackLineImages[i].rectTransform != null)
                    {
                        CrackLineImages[i].rectTransform.localScale = CrackLineLocalScales[i];
                    }
                }
            }

            private static IReadOnlyList<Image> ResolveFlashImages(RectTransform rect)
            {
                var resolved = new List<Image>();
                var images = rect.GetComponentsInChildren<Image>(true);
                for (var i = 0; i < images.Length; i++)
                {
                    if (images[i] != null && images[i].name == EffectImageName)
                    {
                        resolved.Add(images[i]);
                    }
                }

                return resolved;
            }

            private static IReadOnlyList<Image> ResolveFilledImages(RectTransform rect)
            {
                var resolved = new List<Image>();
                var images = rect.GetComponentsInChildren<Image>(true);
                for (var i = 0; i < images.Length; i++)
                {
                    if (images[i] != null && images[i].name == FilledIconName)
                    {
                        resolved.Add(images[i]);
                    }
                }

                if (resolved.Count == 0 && rect.TryGetComponent<Image>(out var rootImage))
                {
                    resolved.Add(rootImage);
                }

                return resolved;
            }

            private static IReadOnlyList<Image> ResolveCrackLineImages(RectTransform tweenRect)
            {
                if (tweenRect == null)
                {
                    return System.Array.Empty<Image>();
                }

                var resolved = new List<Image>();
                var images = tweenRect.GetComponentsInChildren<Image>(true);
                for (var i = 0; i < images.Length; i++)
                {
                    if (images[i] != null && images[i].name.StartsWith(CrackLineNamePrefix, System.StringComparison.OrdinalIgnoreCase))
                    {
                        resolved.Add(images[i]);
                    }
                }

                return resolved.Count > 0 ? resolved : CreateCrackLines(tweenRect);
            }

            private static IReadOnlyList<Image> CreateCrackLines(RectTransform parent)
            {
                var lineSpecs = new[]
                {
                    new CrackLineSpec(new Vector2(-1.5f, 2.5f), new Vector2(2f, 20f), -24f),
                    new CrackLineSpec(new Vector2(2.5f, 1.5f), new Vector2(2f, 15f), 28f),
                    new CrackLineSpec(new Vector2(-4.5f, -3.5f), new Vector2(2f, 13f), 42f),
                    new CrackLineSpec(new Vector2(5.5f, -4.5f), new Vector2(2f, 12f), -38f),
                    new CrackLineSpec(new Vector2(0f, -1f), new Vector2(2f, 18f), 4f),
                };
                var lines = new List<Image>(lineSpecs.Length);
                for (var i = 0; i < lineSpecs.Length; i++)
                {
                    var lineObject = new GameObject($"{CrackLineNamePrefix} {i}", typeof(RectTransform), typeof(Image));
                    lineObject.transform.SetParent(parent, false);
                    var rect = lineObject.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = lineSpecs[i].AnchoredPosition;
                    rect.sizeDelta = lineSpecs[i].SizeDelta;
                    rect.localRotation = Quaternion.Euler(0f, 0f, lineSpecs[i].RotationDegrees);
                    rect.localScale = new Vector3(0.32f, 1f, 1f);

                    var image = lineObject.GetComponent<Image>();
                    image.color = Color.clear;
                    image.raycastTarget = false;
                    lines.Add(image);
                }

                return lines;
            }

            private static IReadOnlyList<Color> CaptureImageColors(IReadOnlyList<Image> images)
            {
                var colors = new List<Color>(images.Count);
                for (var i = 0; i < images.Count; i++)
                {
                    colors.Add(images[i] != null ? images[i].color : Color.white);
                }

                return colors;
            }

            private static IReadOnlyList<Material> CaptureImageMaterials(IReadOnlyList<Image> images)
            {
                var materials = new List<Material>(images.Count);
                for (var i = 0; i < images.Count; i++)
                {
                    materials.Add(images[i] != null ? images[i].material : null);
                }

                return materials;
            }

            private static IReadOnlyList<Vector3> CaptureImageLocalScales(IReadOnlyList<Image> images)
            {
                var scales = new List<Vector3>(images.Count);
                for (var i = 0; i < images.Count; i++)
                {
                    scales.Add(images[i] != null && images[i].rectTransform != null
                        ? images[i].rectTransform.localScale
                        : Vector3.one);
                }

                return scales;
            }

            private static Material ResolveAllIn1SourceMaterial(Material template, Material authoredMaterial, Shader fallbackShader)
            {
                if (template != null && template.shader != null && template.shader.name == AllIn1UiMaskShaderName)
                {
                    return template;
                }

                if (authoredMaterial != null && authoredMaterial.shader == fallbackShader)
                {
                    return authoredMaterial;
                }

                return null;
            }

            private void DestroyRuntimeMaterial(int imageIndex)
            {
                var material = _allIn1RuntimeMaterials[imageIndex];
                if (material == null)
                {
                    return;
                }

                _allIn1RuntimeMaterials[imageIndex] = null;
                if (UnityEngine.Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(material);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }

            private void DestroyFilledIconRuntimeMaterial(int imageIndex)
            {
                var material = _filledIconRuntimeMaterials[imageIndex];
                if (material == null)
                {
                    return;
                }

                _filledIconRuntimeMaterials[imageIndex] = null;
                if (UnityEngine.Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(material);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }

            private readonly struct CrackLineSpec
            {
                public CrackLineSpec(Vector2 anchoredPosition, Vector2 sizeDelta, float rotationDegrees)
                {
                    AnchoredPosition = anchoredPosition;
                    SizeDelta = sizeDelta;
                    RotationDegrees = rotationDegrees;
                }

                public Vector2 AnchoredPosition { get; }

                public Vector2 SizeDelta { get; }

                public float RotationDegrees { get; }
            }
        }

        private sealed class TextVisualState
        {
            private TextVisualState(TMP_Text text)
            {
                Text = text;
                Color = text.color;
                LocalScale = text.rectTransform != null ? text.rectTransform.localScale : Vector3.one;
            }

            public TMP_Text Text { get; }

            public Color Color { get; }

            public Vector3 LocalScale { get; }

            public static TextVisualState Capture(TMP_Text text)
            {
                return text != null ? new TextVisualState(text) : null;
            }
        }
    }
}
