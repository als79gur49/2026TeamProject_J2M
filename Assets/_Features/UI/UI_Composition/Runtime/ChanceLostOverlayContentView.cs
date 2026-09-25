using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Composition
{
    internal sealed class ChanceLostOverlayContentView :
        SceneTransitionOverlayContentView,
        ITransitionContentPlaybackProvider
    {
        private const string ChanceSlotNamePrefix = "ChanceSlotView";
        private const string ImpactRootName = "LostChanceImpactRoot";
        private const string TweenRootName = "LostChanceTweenRoot";
        private const string FilledIconName = "FilledIcon";
        private const string EffectImageName = "Effect";
        private const string CrackLineNamePrefix = "CrackLine";
        private const string CrackShardNamePrefix = "CrackShard";
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
        private static readonly int CrackRevealId = Shader.PropertyToID("_CrackReveal");
        private static readonly int DecayId = Shader.PropertyToID("_Decay");
        private static readonly int DetachId = Shader.PropertyToID("_Detach");
        private static readonly int FragmentId = Shader.PropertyToID("_Fragment");
        private static readonly int IconUvRectId = Shader.PropertyToID("_IconUvRect");
        private static readonly CrackShardSpec[] CrackShardSpecs =
        {
            new(new Vector2(-55f, 24f), new Vector2(28f, 32f), -18f, -18f, 1f, 0.72f, 1f, 0.02f),
            new(new Vector2(51f, -9f), new Vector2(23f, 27f), 22f, 18f, -1f, 0.7f, 0.75f, 0.13f),
            new(new Vector2(-26f, -55f), new Vector2(14f, 12f), 8f, -7f, -1f, 0.6f, 0f, 0.18f),
            new(new Vector2(48f, 21f), new Vector2(13f, 17f), -34f, 13f, 1f, 0.72f, 0f, 0.23f),
            new(new Vector2(-46f, -28f), new Vector2(14f, 14f), 46f, -12f, 1f, 0.62f, 0f, 0.29f),
            new(new Vector2(23f, -50f), new Vector2(12f, 15f), 4f, 7f, -1f, 0.54f, 0f, 0.35f),
            new(new Vector2(36f, 7f), new Vector2(10f, 9f), -12f, 11f, 1f, 0.76f, 0f, 0.41f),
            new(new Vector2(-34f, -13f), new Vector2(9f, 11f), 28f, -10f, -1f, 0.82f, 0f, 0.47f),
            new(new Vector2(15f, -54f), new Vector2(9f, 10f), -6f, 5f, 1f, 0.62f, 0f, 0.52f),
        };
        private static readonly Vector2[] ImpactShakeDirections =
        {
            new(0.8f, 0.55f), new(-1f, -0.35f), new(0.65f, -0.8f),
            new(-0.5f, 0.75f), new(0.7f, 0.25f), new(-0.35f, -0.55f),
            new(0.25f, 0.4f),
        };

        [Header("Localized Text")]
        [SerializeField] private TMP_Text _remainingChancesLabel;
        [SerializeField] private TMP_Text _loadingLabel;
        [SerializeField] private GameplayUiTypographyTheme _typographyTheme;

        [Header("Slot Roots")]
        [Tooltip("Explicitly bound ChanceLost slot roots. Order matters; ChanceSlotView name fallback is only a safety net.")]
        [SerializeField] private RectTransform[] _chanceSlotRoots;

        [Header("Lost Slot Motion")]
        [Tooltip("Seconds spent in the damped two-axis impact shake before the icon falls.")]
        [Min(0f)]
        [SerializeField] private float _lostShakeDurationSeconds = 0.36f;

        [Tooltip("Maximum horizontal UI distance of the impact shake.")]
        [Min(0f)]
        [SerializeField] private float _lostShakeStrength = 13f;

        [Tooltip("Maximum vertical UI distance of the impact shake.")]
        [Min(0f)]
        [SerializeField] private float _lostShakeVerticalStrength = 5f;

        [Tooltip("Number of irregular shake steps before returning to the authored position.")]
        [Min(1)]
        [SerializeField] private int _lostShakeVibrato = 7;

        [Tooltip("UI distance the lost slot falls after impact. Larger values make the slot drop farther.")]
        [Min(0f)]
        [SerializeField] private float _lostFallDistance = 78f;

        [Tooltip("Seconds for the lost slot fall motion. Runtime keeps a small minimum duration.")]
        [Min(0f)]
        [SerializeField] private float _lostFallDurationSeconds = 0.92f;

        [Tooltip("Final seconds of the fall used to fade the icon. The frame remains visible.")]
        [Min(0f)]
        [SerializeField] private float _lostFadeDurationSeconds = 0.22f;

        [Tooltip("Degrees of z rotation applied while the lost slot falls. Negative values rotate the opposite direction.")]
        [SerializeField] private float _lostRotationDegrees = -10f;

        [Tooltip("Seconds between each lost slot animation when multiple slots are lost. 0 starts them together.")]
        [Min(0f)]
        [SerializeField] private float _lostSlotStaggerSeconds = 0.05f;

        [Tooltip("Alpha used for authored chance slots outside the active chance range. 0 hides empty slots; 1 leaves them opaque.")]
        [Range(0f, 1f)]
        [SerializeField] private float _emptySlotAlpha = 0.34f;

        [Header("Impact & Survivor Pulse")]
        [Tooltip("Scale punch added to the lost slot impact. 0 disables the scale punch.")]
        [Min(0f)]
        [SerializeField] private float _lostImpactScalePunch = 0.14f;

        [Tooltip("Seconds for the lost slot impact punch and flash. Runtime keeps a small minimum duration.")]
        [Min(0f)]
        [SerializeField] private float _lostImpactDurationSeconds = 0.24f;

        [Tooltip("Visual-only pause between the impact and recoil. Does not change gameplay time scale.")]
        [Min(0f)]
        [SerializeField] private float _lostImpactHoldSeconds = 0.05f;

        [Tooltip("Target alpha for the lost slot impact flash. 0 uses only the authored alpha; 1 allows a full flash.")]
        [Range(0f, 1f)]
        [SerializeField] private float _lostImpactFlashAlpha = 0.9f;

        [Tooltip("Seconds after the last shard before remaining survivor slots pulse.")]
        [Min(0f)]
        [SerializeField] private float _survivorPulseDelaySeconds = 0.02f;

        [Tooltip("Scale punch applied to remaining survivor slots. 0 disables survivor pulse.")]
        [Min(0f)]
        [SerializeField] private float _survivorPulseScalePunch = 0.07f;

        [Tooltip("Seconds for the remaining survivor slot pulse. Runtime keeps a small minimum duration.")]
        [Min(0f)]
        [SerializeField] private float _survivorPulseDurationSeconds = 0.22f;

        [Header("AllIn1 Material Hit Effect")]
        [Tooltip("Enables the optional AllIn1 material hit effect. If disabled, AllIn1 effects are skipped; the authored fracture material is independent.")]
        [SerializeField] private bool _useAllIn1LostImpactEffect = true;

        [Tooltip("Optional AllIn1 UI mask material template. If null, the view tries the authored image material or shader fallback.")]
        [SerializeField] private Material _allIn1EffectMaterialTemplate;

        [Tooltip("Color multiplied by the AllIn1 hit-effect glow for the lost impact material flash.")]
        [SerializeField] private Color _allIn1HitEffectColor = new(1f, 0.18f, 0.24f, 1f);

        [Tooltip("Glow multiplier for the AllIn1 hit-effect color. Runtime treats values below 1 as 1.")]
        [Min(1f)]
        [SerializeField] private float _allIn1HitEffectGlow = 3f;

        [Tooltip("AllIn1 distortion amount for the lost impact hit effect. 0 disables impact distortion.")]
        [Min(0f)]
        [SerializeField] private float _allIn1DistortAmount = 0.16f;

        [Tooltip("Seconds for the AllIn1 impact distortion pulse. Runtime uses half this duration for each yoyo leg.")]
        [Min(0f)]
        [SerializeField] private float _allIn1DistortDurationSeconds = 0.24f;

        [Tooltip("Texture scroll speed used by AllIn1 distortion. 0 keeps the distortion texture still.")]
        [Min(0f)]
        [SerializeField] private float _allIn1DistortTexSpeed = 4f;

        [Header("Lost Filled Icon Material")]
        [Tooltip("Authored crack, corrosion and fragment material. AllIn1 remains the fallback for unconfigured views.")]
        [SerializeField] private Material _filledIconFractureMaterial;

        [Tooltip("Seconds after the icon starts falling before the corrosion spreads.")]
        [Min(0f)]
        [SerializeField] private float _lostCorrosionDelaySeconds = 0.1f;

        [Tooltip("Visible decay tint applied to the filled icon as a chance is lost. Alpha controls the target icon opacity.")]
        [ColorUsage(true, false)]
        [SerializeField] private Color _lostFilledIconDecayColor = new(0.5f, 0.42f, 0.35f, 1f);

        [Tooltip("Burn edge color used by the filled icon material fade. Alpha controls the burn color opacity.")]
        [ColorUsage(true, false)]
        [SerializeField] private Color _lostFilledIconBurnColor = new(0.74f, 0.62f, 0.51f, 1f);

        [Tooltip("AllIn1 fade amount for the filled icon lost-state material. -0.08 starts at the authored burn edge; 1 fully advances the fade.")]
        [Range(-0.08f, 1f)]
        [SerializeField] private float _lostFilledIconFadeAmount = 0.64f;

        [Tooltip("Greyscale blend for the filled icon lost-state material. 0 keeps source color; 1 reaches full greyscale tint.")]
        [Range(0f, 1f)]
        [SerializeField] private float _lostFilledIconGreyscaleBlend = 0.85f;

        [Tooltip("AllIn1 distortion amount for the filled icon lost-state material. 0 disables filled icon distortion.")]
        [Min(0f)]
        [SerializeField] private float _lostFilledIconDistortAmount = 0.08f;

        [Header("Crack Line")]
        [Tooltip("Reveal color for crack lines. Alpha controls final crack line opacity.")]
        [ColorUsage(true, false)]
        [SerializeField] private Color _crackLineColor = new(0.08f, 0.02f, 0.015f, 0.78f);

        [Tooltip("Seconds before crack lines begin revealing after the impact ends. 0 reveals immediately.")]
        [Min(0f)]
        [SerializeField] private float _crackLineRevealDelaySeconds = 0.04f;

        [Tooltip("Seconds for each crack line reveal. Runtime keeps a small minimum duration.")]
        [Min(0f)]
        [SerializeField] private float _crackLineRevealDurationSeconds = 0.27f;

        [Header("Crack Shards")]
        [Tooltip("Template material that selects matching pieces of the authored helmet sprite.")]
        [SerializeField] private Material _crackShardMaterial;

        [Tooltip("Starting color for early crack shards. Alpha controls shard visibility before fade.")]
        [ColorUsage(true, false)]
        [SerializeField] private Color _crackShardFreshColor = Color.white;

        [Tooltip("Decay color blended into later crack shards. Alpha controls shard visibility before fade.")]
        [ColorUsage(true, false)]
        [SerializeField] private Color _crackShardDecayColor = new(0.78f, 0.77f, 0.74f, 0.96f);

        [Tooltip("Base seconds before crack shards spawn. Individual shards add their own offsets.")]
        [Min(0f)]
        [SerializeField] private float _crackShardDelaySeconds = 0.02f;

        [Tooltip("Seconds for crack shard rise, fall, scale, and fade motion. Runtime keeps a small minimum duration.")]
        [Min(0f)]
        [SerializeField] private float _crackShardDurationSeconds = 0.54f;

        [Tooltip("UI distance crack shards fall after the initial rise. Larger values make shards drop farther.")]
        [Min(0f)]
        [SerializeField] private float _crackShardFallDistance = 52f;

        [Tooltip("Initial UI rise distance before crack shards fall. 0 skips the upward lift.")]
        [Min(0f)]
        [SerializeField] private float _crackShardInitialRise = 0f;

        [Tooltip("Seconds before crack shards begin fading during their motion. Runtime clamps this within shard duration.")]
        [Min(0f)]
        [SerializeField] private float _crackShardFadeDelaySeconds = 0.33f;

        [Tooltip("Degrees of shard rotation during the crack motion. 0 keeps authored shard rotation.")]
        [Range(0f, 360f)]
        [SerializeField] private float _crackShardRotationDegrees = 75f;

        [Header("Timing")]
        [Tooltip("Uses unscaled DOTween update for ChanceLost animation so overlay timing can ignore gameplay time scale.")]
        [SerializeField] private bool _useUnscaledTime = true;

        [Tooltip("Authored quiet interval after every lost slot and its final shard have completed.")]
        [Min(0f)]
        [SerializeField] private float _postShatterSettleDurationSeconds = 0.15f;

        private readonly List<SlotState> _slotStates = new();
        private readonly List<RectTransform> _resolvedSlots = new();
        private Sequence _lostChanceSequence;
        private SceneTransitionOverlayModel _boundModel;
        private Shader _allIn1UiMaskShader;
        private bool _hasBoundModel;
        private bool _hasResolvedAllIn1UiMaskShader;
        private bool _playbackStarted;
        private TransitionContentPlaybackHandle _playback = new();
        private int _playbackGeneration;
        private bool _hasCapturedLocalizedTextTypography;
        private TmpTypographyAuthoredState _remainingChancesAuthoredState;
        private TmpTypographyAuthoredState _loadingAuthoredState;

        public event Action Completed;

        public event Action Cancelled;

        public event Action Failed;

        public bool IsCompleted => _playback.IsCompleted;

        public bool IsCancelled => _playback.IsCancelled;

        public bool IsFailed => _playback.IsFailed;

        public ITransitionContentPlayback Playback => _playback;

        public override void Bind(SceneTransitionOverlayModel model)
        {
            ResetPlayback(cancelActive: true);
            BeginNewPlayback();
            RestoreChanceSlots();
            base.Bind(model);
            ApplyLocalizedText(model.Text);
            _boundModel = model;
            _hasBoundModel = true;

            if (!model.HasChanceLost)
            {
                ApplyChanceSlotState(model);
                return;
            }

            ApplyChanceSlotState(model);
        }

        public override void Show()
        {
            base.Show();
            if (_playbackStarted)
            {
                return;
            }

            PlayLostChanceAnimation();
        }

        public override void Hide()
        {
            KillLostChanceAnimation(signalCancellation: true);
            RestoreChanceSlots();
            base.Hide();
        }

        public override void ResetView()
        {
            ResetPlayback(cancelActive: true);
            BeginNewPlayback();
            RestoreChanceSlots();
            _hasBoundModel = false;
            base.ResetView();
        }

        internal override IReadOnlyList<string> CollectValidationIssues()
        {
            var issues = new List<string>(base.CollectValidationIssues());
            AddMissing(issues, _remainingChancesLabel, nameof(_remainingChancesLabel));
            AddMissing(issues, _loadingLabel, nameof(_loadingLabel));
            AddMissing(issues, _typographyTheme, nameof(_typographyTheme));
            var hasExplicitSlotRoots = _chanceSlotRoots != null && _chanceSlotRoots.Length > 0;
            var fallbackSlots = new List<RectTransform>();
            CollectFallbackChanceSlots(fallbackSlots);

            if (!hasExplicitSlotRoots)
            {
                issues.Add("ChanceLostOverlayContentView requires explicit _chanceSlotRoots inspector bindings; ChanceSlotView name fallback is safety-only.");
            }
            else
            {
                for (var i = 0; i < _chanceSlotRoots.Length; i++)
                {
                    if (_chanceSlotRoots[i] == null)
                    {
                        issues.Add($"ChanceLostOverlayContentView _chanceSlotRoots has null entry at index {i}.");
                    }
                }

                if (fallbackSlots.Count > 0 && _chanceSlotRoots.Length != fallbackSlots.Count)
                {
                    issues.Add($"ChanceLostOverlayContentView _chanceSlotRoots count {_chanceSlotRoots.Length} does not match authored ChanceSlotView count {fallbackSlots.Count}.");
                }
            }

            if (ResolveChanceSlots().Count == 0)
            {
                issues.Add("ChanceLostOverlayContentView has no chance slot roots for lost chance animation.");
            }

            return issues;
        }

        private void OnDisable()
        {
            KillLostChanceAnimation(signalCancellation: true);
            RestoreChanceSlots();
        }

        private void OnDestroy()
        {
            KillLostChanceAnimation(signalCancellation: true);
            RestoreChanceSlots();
        }

        private void ApplyLocalizedText(SceneTransitionOverlayTextSnapshot text)
        {
            CaptureLocalizedTextTypography();
            SetText(_remainingChancesLabel, text.RemainingChancesLabel);
            SetText(_loadingLabel, text.LoadingLabel);

            var localeCode = string.IsNullOrWhiteSpace(text.LocaleCode)
                ? UnityStringTableTextResolver.DefaultLocaleCode
                : text.LocaleCode;
            GameplayHudLocalizationBinding.ApplyTypography(
                _remainingChancesLabel,
                _typographyTheme,
                localeCode,
                TypographyStyleTag.HeaderLarge,
                _remainingChancesAuthoredState);
            GameplayHudLocalizationBinding.ApplyTypography(
                _loadingLabel,
                _typographyTheme,
                localeCode,
                TypographyStyleTag.Label,
                _loadingAuthoredState);
        }

        private void CaptureLocalizedTextTypography()
        {
            if (_hasCapturedLocalizedTextTypography)
            {
                return;
            }

            _remainingChancesAuthoredState = TmpTypographyAuthoredState.Capture(_remainingChancesLabel);
            _loadingAuthoredState = TmpTypographyAuthoredState.Capture(_loadingLabel);
            _hasCapturedLocalizedTextTypography = true;
        }

        internal int ActiveLostChanceAnimationCountForTests => _lostChanceSequence != null && _lostChanceSequence.IsActive() ? 1 : 0;

        internal int ResolvedChanceSlotCountForTests => ResolveChanceSlots().Count;

        internal IReadOnlyList<RectTransform> ResolvedChanceSlotsForTests => ResolveChanceSlots();

        internal Color CrackShardVisibleColorForTests(float shardDelaySeconds) => EvaluateCrackShardVisibleColor(shardDelaySeconds);

        internal int CrackShardCountPerLostSlotForTests => CrackShardSpecs.Length;

        internal float PostShatterSettleDurationSecondsForTests =>
            Mathf.Max(0f, _postShatterSettleDurationSeconds);

        internal float RootSequenceDurationSecondsForTests =>
            _lostChanceSequence != null ? _lostChanceSequence.Duration(false) : 0f;

        internal float RootSequencePositionSecondsForTests =>
            _lostChanceSequence != null ? _lostChanceSequence.Elapsed(false) : 0f;

        internal void GotoRootSequenceForTests(float positionSeconds)
        {
            _lostChanceSequence?.Goto(Mathf.Max(0f, positionSeconds), andPlay: false);
        }

        private void ApplyChanceSlotState(SceneTransitionOverlayModel model)
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
                state.IconCanvasGroup.alpha = isCurrentlyFilled || isLostThisTransition ? 1f : 0f;
            }
        }

        private void PlayLostChanceAnimation()
        {
            if (_playbackStarted)
            {
                return;
            }

            _playbackStarted = true;
            var generation = ++_playbackGeneration;
            if (!_hasBoundModel || !_boundModel.HasChanceLost)
            {
                CompletePlayback(generation);
                return;
            }

            var slots = ResolveChanceSlots();
            if (slots.Count == 0)
            {
                Debug.LogError(
                    "Chance Lost payload expected exactly one lost slot, but no authored chance slots were resolved. " +
                    "Completing content to avoid a transition deadlock.",
                    this);
                CompletePlayback(generation);
                return;
            }

            EnsureSlotStates(slots);
            var lostStart = Mathf.Clamp(_boundModel.CurrentRemainingChances, 0, slots.Count);
            var lostEndExclusive = Mathf.Clamp(_boundModel.PreviousRemainingChances, lostStart, slots.Count);
            if (lostEndExclusive <= lostStart)
            {
                Debug.LogError(
                    $"Chance Lost payload expected exactly one lost slot, but resolved 0 " +
                    $"(previous={_boundModel.PreviousRemainingChances}, current={_boundModel.CurrentRemainingChances}). " +
                    "Completing content to avoid a transition deadlock.",
                    this);
                CompletePlayback(generation);
                return;
            }

            var lostSlotCount = lostEndExclusive - lostStart;
            if (lostSlotCount > 1)
            {
                Debug.LogWarning(
                    $"Chance Lost campaign payload expected exactly one lost slot, but resolved {lostSlotCount}. " +
                    "All lost slots will complete as one aggregate playback.",
                    this);
            }

            _lostChanceSequence = DOTween.Sequence()
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

            for (var i = lostStart; i < lostEndExclusive; i++)
            {
                var state = _slotStates[i];
                RestoreSlot(state);
                state.Rect.gameObject.SetActive(true);
                state.CanvasGroup.alpha = state.Alpha;

                var slotDelay = (i - lostStart) * Mathf.Max(0f, _lostSlotStaggerSeconds);
                var impact = CreateLostImpactTween(state);
                var impactEnd = slotDelay + impact.Duration(false);
                var recoilStart = impactEnd + Mathf.Max(0f, _lostImpactHoldSeconds);
                var shake = CreateImpactShakeTween(state);
                var fallStart = recoilStart + shake.Duration(false);
                var fallDuration = Mathf.Max(0.01f, _lostFallDurationSeconds);
                var fadeDuration = Mathf.Clamp(_lostFadeDurationSeconds, 0.01f, fallDuration);
                var slotSequence = DOTween.Sequence()
                    .SetUpdate(_useUnscaledTime)
                    .Insert(slotDelay, impact)
                    .Insert(impactEnd, CreateCrackRevealTween(state))
                    .Insert(recoilStart, shake)
                    .Insert(fallStart, CreateAnchorMoveTween(
                            state.TweenRect,
                            state.TweenAnchoredPosition + Vector2.down * Mathf.Max(0f, _lostFallDistance),
                            fallDuration)
                        .SetEase(Ease.InCubic))
                    // The icon stays opaque while corrosion spreads; only the late fade removes it.
                    .Insert(fallStart + fallDuration - fadeDuration, DOTween.To(
                            () => state.IconCanvasGroup.alpha,
                            value => state.IconCanvasGroup.alpha = value,
                            0f, fadeDuration).SetEase(Ease.InQuad))
                    .Insert(fallStart, DOTween.To(
                            () => state.CanvasGroup.alpha,
                            value => state.CanvasGroup.alpha = value,
                            Mathf.Clamp01(_emptySlotAlpha), fallDuration).SetEase(Ease.InOutQuad))
                    .Insert(fallStart, DOTween.To(
                            () => state.TweenRect.localEulerAngles,
                            value => state.TweenRect.localEulerAngles = value,
                            state.TweenLocalRotation.eulerAngles + new Vector3(0f, 0f, _lostRotationDegrees),
                            fallDuration).SetEase(Ease.InQuad))
                    .Insert(fallStart, CreateFilledIconBreakTween(state, fallDuration))
                    .Insert(fallStart, CreateCrackShardTween(state));

                _lostChanceSequence.Join(slotSequence);
            }

            // Capture the complete debris duration before adding the survivor acknowledgement.
            InsertSurvivorPulseTweens(lostStart, _lostChanceSequence.Duration(false));
            _lostChanceSequence
                .AppendInterval(Mathf.Max(0f, _postShatterSettleDurationSeconds))
                .OnComplete(() => CompletePlayback(generation));
        }

        private void InsertSurvivorPulseTweens(int survivorEndExclusive, float shatterEnd)
        {
            var delay = shatterEnd + Mathf.Max(0f, _survivorPulseDelaySeconds);
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

        private Tween CreateLostImpactTween(SlotState state)
        {
            var impact = DOTween.Sequence().SetUpdate(_useUnscaledTime);
            var duration = Mathf.Max(0.01f, _lostImpactDurationSeconds);
            var scalePunch = Mathf.Max(0f, _lostImpactScalePunch);
            if (state.ImpactRect != null && scalePunch > 0f)
            {
                impact.Join(
                    state.ImpactRect
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

                if (_filledIconFractureMaterial != null)
                {
                    var fractureMaterial = PrepareFractureMaterial(state, i);
                    var delay = Mathf.Clamp(_lostCorrosionDelaySeconds, 0f, duration - 0.01f);
                    sequence.Insert(delay, DOTween.To(
                            () => fractureMaterial != null ? fractureMaterial.GetFloat(DecayId) : 0f,
                            value => SetFloatIfPresent(fractureMaterial, DecayId, value),
                            1f, duration - delay).SetEase(Ease.InQuad));
                    sequence.Insert(0f, DOTween.To(
                            () => fractureMaterial != null ? fractureMaterial.GetFloat(DetachId) : 0f,
                            value => SetFloatIfPresent(fractureMaterial, DetachId, value),
                            1f, duration).SetEase(Ease.Linear));
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

            return sequence;
        }

        private Tween CreateCrackRevealTween(SlotState state)
        {
            var sequence = DOTween.Sequence().SetUpdate(_useUnscaledTime);
            if (_filledIconFractureMaterial != null)
            {
                for (var i = 0; i < state.FilledImages.Count; i++)
                {
                    var material = PrepareFractureMaterial(state, i);
                    if (material == null)
                    {
                        continue;
                    }
                    sequence.Insert(Mathf.Max(0f, _crackLineRevealDelaySeconds), DOTween.To(
                        () => material != null ? material.GetFloat(CrackRevealId) : 0f,
                        value => SetFloatIfPresent(material, CrackRevealId, value),
                        1f, Mathf.Max(0.01f, _crackLineRevealDurationSeconds)).SetEase(Ease.OutQuad));
                }
                return sequence;
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

        private Material PrepareFractureMaterial(SlotState state, int imageIndex)
        {
            var image = state.FilledImages[imageIndex];
            var material = state.GetOrCreateFilledIconMaterial(
                imageIndex, _filledIconFractureMaterial, _filledIconFractureMaterial.shader);
            if (material != null && image != null)
            {
                // Sprite atlas UVs are normalized back into icon space by the shader.
                var uv = image.sprite != null
                    ? UnityEngine.Sprites.DataUtility.GetOuterUV(image.sprite)
                    : new Vector4(0f, 0f, 1f, 1f);
                material.SetVector(IconUvRectId, uv);
            }
            return material;
        }

        private Tween CreateCrackShardTween(SlotState state)
        {
            var sequence = DOTween.Sequence().SetUpdate(_useUnscaledTime);
            if (state.CrackShardImages.Count == 0)
            {
                return sequence;
            }

            var baseDelay = Mathf.Max(0f, _crackShardDelaySeconds);
            var duration = Mathf.Max(0.01f, _crackShardDurationSeconds);
            var riseDuration = Mathf.Clamp(duration * 0.08f, 0.02f, duration * 0.18f);
            var fallDuration = Mathf.Max(0.01f, duration - riseDuration);
            var fadeDelay = Mathf.Clamp(_crackShardFadeDelaySeconds, 0f, duration - 0.01f);
            for (var i = 0; i < state.CrackShardImages.Count; i++)
            {
                var image = state.CrackShardImages[i];
                if (image == null || image.rectTransform == null)
                {
                    continue;
                }

                var rect = image.rectTransform;
                var spec = CrackShardSpecs[i % CrackShardSpecs.Length];
                var material = state.GetOrCreateCrackShardMaterial(i, _crackShardMaterial);
                if (material != null)
                {
                    material.SetFloat(FragmentId, i % CrackShardSpecs.Length + 1);
                    var uv = image.sprite != null
                        ? UnityEngine.Sprites.DataUtility.GetOuterUV(image.sprite)
                        : new Vector4(0f, 0f, 1f, 1f);
                    material.SetVector(IconUvRectId, uv);
                }
                var origin = state.CrackShardAnchoredPositions[i];
                var delay = baseDelay + spec.DelaySeconds;
                var visibleColor = EvaluateCrackShardVisibleColor(delay);
                var clearColor = visibleColor;
                clearColor.a = 0f;

                image.color = clearColor;
                rect.anchoredPosition = origin;
                rect.localRotation = state.CrackShardLocalRotations[i];
                rect.localScale = state.CrackShardLocalScales[i];

                var riseTarget = origin + new Vector2(spec.SpreadX * 0.08f, Mathf.Max(0f, _crackShardInitialRise) * spec.RiseMultiplier);
                var fallTarget = origin + new Vector2(
                    spec.SpreadX,
                    -Mathf.Max(0f, _crackShardFallDistance) * spec.FallMultiplier);
                var rotationTarget = rect.localEulerAngles + new Vector3(0f, 0f, spec.RotationSign * Mathf.Max(0f, _crackShardRotationDegrees));
                var targetScale = state.CrackShardLocalScales[i] * 0.64f;

                var shardSequence = DOTween.Sequence().SetUpdate(_useUnscaledTime)
                    .Insert(0f, CreateAnchorMoveTween(rect, riseTarget, riseDuration).SetEase(Ease.OutQuad))
                    .Insert(riseDuration, CreateAnchorMoveTween(rect, fallTarget, fallDuration).SetEase(Ease.InCubic))
                    .Insert(0f, DOTween.To(
                            () => rect.localEulerAngles,
                            value => rect.localEulerAngles = value,
                            rotationTarget,
                            duration)
                        .SetEase(Ease.OutQuad))
                    .Insert(duration * 0.42f, rect.DOScale(targetScale, duration * 0.5f).SetEase(Ease.InQuad))
                    .Insert(0f, CreateShardColorTween(image, visibleColor, clearColor, duration, fadeDelay));

                sequence.Insert(delay, shardSequence);
            }

            return sequence;
        }

        private Color EvaluateCrackShardVisibleColor(float shardDelaySeconds)
        {
            var breakDuration = Mathf.Max(0.01f, _lostShakeDurationSeconds + _lostFallDurationSeconds);
            var breakProgress = Mathf.Clamp01(Mathf.Max(0f, shardDelaySeconds) / breakDuration);
            return Color.Lerp(_crackShardFreshColor, _crackShardDecayColor, breakProgress);
        }

        private static Tween CreateShardColorTween(
            Image image,
            Color visibleColor,
            Color clearColor,
            float duration,
            float fadeDelay)
        {
            var revealDuration = Mathf.Max(0.01f, duration * 0.12f);
            var colorSequence = DOTween.Sequence()
                .Append(DOTween.To(
                        () => image != null ? image.color : clearColor,
                        value =>
                        {
                            if (image != null)
                            {
                                image.color = value;
                            }
                        },
                        visibleColor,
                        revealDuration)
                    .SetEase(Ease.OutQuad));

            if (fadeDelay > revealDuration)
            {
                colorSequence.AppendInterval(fadeDelay - revealDuration);
            }

            colorSequence.Append(DOTween.To(
                    () => image != null ? image.color : visibleColor,
                    value =>
                    {
                        if (image != null)
                        {
                            image.color = value;
                        }
                    },
                    clearColor,
                    Mathf.Max(0.01f, duration - fadeDelay))
                .SetEase(Ease.InQuad));
            return colorSequence;
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
            if (_chanceSlotRoots != null && _chanceSlotRoots.Length > 0)
            {
                for (var i = 0; i < _chanceSlotRoots.Length; i++)
                {
                    if (_chanceSlotRoots[i] != null)
                    {
                        _resolvedSlots.Add(_chanceSlotRoots[i]);
                    }
                }

                return _resolvedSlots;
            }

            CollectFallbackChanceSlots(_resolvedSlots);
            return _resolvedSlots;
        }

        private void CollectFallbackChanceSlots(List<RectTransform> slots)
        {
            var children = GetComponentsInChildren<RectTransform>(true);
            for (var i = 0; i < children.Length; i++)
            {
                var child = children[i];
                if (child == null || !child.name.StartsWith(ChanceSlotNamePrefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                slots.Add(child);
            }

            slots.Sort(CompareChanceSlotNames);
        }

        private Tween CreateImpactShakeTween(SlotState state)
        {
            var shake = DOTween.Sequence();
            var vibrato = Mathf.Max(1, _lostShakeVibrato);
            var stepDuration = Mathf.Max(0.01f, _lostShakeDurationSeconds) / (vibrato + 1);
            var horizontalStrength = Mathf.Max(0f, _lostShakeStrength);
            var verticalStrength = Mathf.Max(0f, _lostShakeVerticalStrength);
            for (var i = 0; i < vibrato; i++)
            {
                var direction = ImpactShakeDirections[i % ImpactShakeDirections.Length];
                var falloff = 1f - 0.7f * (i / (float)vibrato);
                var target = state.ImpactAnchoredPosition + new Vector2(
                    direction.x * horizontalStrength * falloff,
                    direction.y * verticalStrength * falloff);
                shake.Append(CreateAnchorMoveTween(state.ImpactRect, target, stepDuration).SetEase(Ease.OutQuad));
            }

            shake.Append(CreateAnchorMoveTween(state.ImpactRect, state.ImpactAnchoredPosition, stepDuration).SetEase(Ease.OutQuad));
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

                var impactRoot = ResolveImpactRoot(rect);
                var state = new SlotState(rect, impactRoot, ResolveTweenRoot(impactRoot), canvasGroup);
                _slotStates.Add(state);
            }
        }

        private void RestoreChanceSlots()
        {
            for (var i = 0; i < _slotStates.Count; i++)
            {
                RestoreSlot(_slotStates[i]);
            }
        }

        private static void RestoreSlot(SlotState state)
        {
            if (state?.Rect == null || state.CanvasGroup == null)
            {
                return;
            }

            if (state.ImpactRect != null)
            {
                state.ImpactRect.anchoredPosition = state.ImpactAnchoredPosition;
                state.ImpactRect.localRotation = state.ImpactLocalRotation;
                state.ImpactRect.localScale = state.ImpactLocalScale;
            }

            if (state.TweenRect != null)
            {
                state.TweenRect.anchoredPosition = state.TweenAnchoredPosition;
                state.TweenRect.localRotation = state.TweenLocalRotation;
                state.TweenRect.localScale = state.TweenLocalScale;
            }

            state.RestoreFlashImages();

            state.CanvasGroup.alpha = state.Alpha;
            state.IconCanvasGroup.alpha = 1f;
            state.Rect.gameObject.SetActive(state.ActiveSelf);
        }

        private void ResetPlayback(bool cancelActive)
        {
            KillLostChanceAnimation(cancelActive);
            _playbackStarted = false;
            _playbackGeneration++;
        }

        private void BeginNewPlayback()
        {
            _playback = new TransitionContentPlaybackHandle();
            _playback.Completed += () => Completed?.Invoke();
            _playback.Cancelled += () => Cancelled?.Invoke();
            _playback.Failed += () => Failed?.Invoke();
        }

        private void KillLostChanceAnimation(bool signalCancellation)
        {
            if (_lostChanceSequence != null)
            {
                _lostChanceSequence.Kill(false);
                _lostChanceSequence = null;
            }

            if (signalCancellation)
            {
                CancelPlayback(_playbackGeneration);
            }
        }

        private void CompletePlayback(int generation)
        {
            if (generation != _playbackGeneration ||
                !_playbackStarted)
            {
                return;
            }

            _lostChanceSequence = null;
            _playback.TryComplete();
        }

        private void CancelPlayback(int generation)
        {
            if (generation != _playbackGeneration ||
                !_playbackStarted)
            {
                return;
            }

            _playback.TryCancel();
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

        private static RectTransform ResolveImpactRoot(RectTransform slotRoot)
        {
            var existing = slotRoot.Find(ImpactRootName);
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

            var impactRootObject = new GameObject(ImpactRootName, typeof(RectTransform));
            var impactRoot = impactRootObject.GetComponent<RectTransform>();
            impactRoot.SetParent(slotRoot, false);
            Stretch(impactRoot);

            for (var i = 0; i < children.Length; i++)
            {
                if (children[i] != null)
                {
                    children[i].SetParent(impactRoot, false);
                }
            }

            return impactRoot;
        }

        private static RectTransform ResolveTweenRoot(RectTransform impactRoot)
        {
            var existing = impactRoot.Find(TweenRootName);
            if (existing is RectTransform existingRect)
            {
                return existingRect;
            }

            var childCount = impactRoot.childCount;
            var children = new Transform[childCount];
            for (var i = 0; i < childCount; i++)
            {
                children[i] = impactRoot.GetChild(i);
            }

            var tweenRootObject = new GameObject(TweenRootName, typeof(RectTransform));
            var tweenRoot = tweenRootObject.GetComponent<RectTransform>();
            tweenRoot.SetParent(impactRoot, false);
            Stretch(tweenRoot);

            for (var i = 0; i < children.Length; i++)
            {
                if (children[i] != null && children[i].name == FilledIconName)
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
            public SlotState(RectTransform rect, RectTransform impactRect, RectTransform tweenRect, CanvasGroup canvasGroup)
            {
                Rect = rect;
                ImpactRect = impactRect;
                TweenRect = tweenRect;
                CanvasGroup = canvasGroup;
                IconCanvasGroup = tweenRect.GetComponent<CanvasGroup>();
                if (IconCanvasGroup == null)
                {
                    IconCanvasGroup = tweenRect.gameObject.AddComponent<CanvasGroup>();
                }
                IconCanvasGroup.interactable = false;
                IconCanvasGroup.blocksRaycasts = false;
                IconCanvasGroup.ignoreParentGroups = true;
                ImpactAnchoredPosition = impactRect != null ? impactRect.anchoredPosition : Vector2.zero;
                ImpactLocalRotation = impactRect != null ? impactRect.localRotation : Quaternion.identity;
                ImpactLocalScale = impactRect != null ? impactRect.localScale : Vector3.one;
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
                CrackShardImages = ResolveCrackShardImages(tweenRect);
                CrackShardColors = CaptureImageColors(CrackShardImages);
                CrackShardMaterials = CaptureImageMaterials(CrackShardImages);
                CrackShardAnchoredPositions = CaptureImageAnchoredPositions(CrackShardImages);
                CrackShardLocalRotations = CaptureImageLocalRotations(CrackShardImages);
                CrackShardLocalScales = CaptureImageLocalScales(CrackShardImages);
                _allIn1RuntimeMaterials = new Material[FlashImages.Count];
                _filledIconRuntimeMaterials = new Material[FilledImages.Count];
                _crackShardRuntimeMaterials = new Material[CrackShardImages.Count];
            }

            public RectTransform Rect { get; }

            public RectTransform ImpactRect { get; }

            public RectTransform TweenRect { get; }

            public CanvasGroup CanvasGroup { get; }

            public CanvasGroup IconCanvasGroup { get; }

            public Vector2 ImpactAnchoredPosition { get; }

            public Quaternion ImpactLocalRotation { get; }

            public Vector3 ImpactLocalScale { get; }

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

            public IReadOnlyList<Image> CrackShardImages { get; }

            public IReadOnlyList<Color> CrackShardColors { get; }

            public IReadOnlyList<Material> CrackShardMaterials { get; }

            public IReadOnlyList<Vector2> CrackShardAnchoredPositions { get; }

            public IReadOnlyList<Quaternion> CrackShardLocalRotations { get; }

            public IReadOnlyList<Vector3> CrackShardLocalScales { get; }

            private Material[] _allIn1RuntimeMaterials { get; }

            private Material[] _filledIconRuntimeMaterials { get; }

            private Material[] _crackShardRuntimeMaterials { get; }

            public Material GetOrCreateCrackShardMaterial(int imageIndex, Material template)
            {
                if (template == null || imageIndex < 0 || imageIndex >= CrackShardImages.Count ||
                    CrackShardImages[imageIndex] == null)
                {
                    return null;
                }

                if (_crackShardRuntimeMaterials[imageIndex] != null)
                {
                    return _crackShardRuntimeMaterials[imageIndex];
                }

                var material = new Material(template)
                {
                    name = $"{CrackShardImages[imageIndex].name} Runtime Fragment",
                    hideFlags = HideFlags.DontSave,
                };
                _crackShardRuntimeMaterials[imageIndex] = material;
                CrackShardImages[imageIndex].material = material;
                return material;
            }

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
                material.name = $"{FilledImages[imageIndex].name} Runtime Fracture";
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

                for (var i = 0; i < CrackShardImages.Count; i++)
                {
                    if (CrackShardImages[i] == null)
                    {
                        continue;
                    }

                    CrackShardImages[i].color = CrackShardColors[i];
                    CrackShardImages[i].material = CrackShardMaterials[i];
                    DestroyCrackShardRuntimeMaterial(i);
                    if (CrackShardImages[i].rectTransform != null)
                    {
                        CrackShardImages[i].rectTransform.anchoredPosition = CrackShardAnchoredPositions[i];
                        CrackShardImages[i].rectTransform.localRotation = CrackShardLocalRotations[i];
                        CrackShardImages[i].rectTransform.localScale = CrackShardLocalScales[i];
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

            private static IReadOnlyList<Image> ResolveCrackShardImages(RectTransform tweenRect)
            {
                if (tweenRect == null)
                {
                    return System.Array.Empty<Image>();
                }

                var resolved = new List<Image>();
                var images = tweenRect.GetComponentsInChildren<Image>(true);
                for (var i = 0; i < images.Length; i++)
                {
                    if (images[i] != null && images[i].name.StartsWith(CrackShardNamePrefix, System.StringComparison.OrdinalIgnoreCase))
                    {
                        resolved.Add(images[i]);
                    }
                }

                return resolved.Count > 0 ? resolved : CreateCrackShards(tweenRect);
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

            private static IReadOnlyList<Image> CreateCrackShards(RectTransform parent)
            {
                var shards = new List<Image>(CrackShardSpecs.Length);
                for (var i = 0; i < CrackShardSpecs.Length; i++)
                {
                    var spec = CrackShardSpecs[i];
                    var shardObject = new GameObject($"{CrackShardNamePrefix} {i}", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                    shardObject.transform.SetParent(parent, false);
                    var rect = shardObject.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = spec.AnchoredPosition;
                    rect.sizeDelta = spec.SizeDelta;
                    rect.localRotation = Quaternion.Euler(0f, 0f, spec.RotationDegrees);
                    rect.localScale = Vector3.one;

                    var canvasGroup = shardObject.GetComponent<CanvasGroup>();
                    canvasGroup.alpha = 1f;
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                    canvasGroup.ignoreParentGroups = true;

                    var image = shardObject.GetComponent<Image>();
                    image.color = Color.clear;
                    image.raycastTarget = false;
                    shards.Add(image);
                }

                return shards;
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

            private static IReadOnlyList<Vector2> CaptureImageAnchoredPositions(IReadOnlyList<Image> images)
            {
                var positions = new List<Vector2>(images.Count);
                for (var i = 0; i < images.Count; i++)
                {
                    positions.Add(images[i] != null && images[i].rectTransform != null
                        ? images[i].rectTransform.anchoredPosition
                        : Vector2.zero);
                }

                return positions;
            }

            private static IReadOnlyList<Quaternion> CaptureImageLocalRotations(IReadOnlyList<Image> images)
            {
                var rotations = new List<Quaternion>(images.Count);
                for (var i = 0; i < images.Count; i++)
                {
                    rotations.Add(images[i] != null && images[i].rectTransform != null
                        ? images[i].rectTransform.localRotation
                        : Quaternion.identity);
                }

                return rotations;
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
                if (template != null && template.shader == fallbackShader)
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

            private void DestroyCrackShardRuntimeMaterial(int imageIndex)
            {
                var material = _crackShardRuntimeMaterials[imageIndex];
                if (material == null)
                {
                    return;
                }

                _crackShardRuntimeMaterials[imageIndex] = null;
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

        private readonly struct CrackShardSpec
        {
            public CrackShardSpec(
                Vector2 anchoredPosition,
                Vector2 sizeDelta,
                float rotationDegrees,
                float spreadX,
                float rotationSign,
                float fallMultiplier,
                float riseMultiplier,
                float delaySeconds)
            {
                AnchoredPosition = anchoredPosition;
                SizeDelta = sizeDelta;
                RotationDegrees = rotationDegrees;
                SpreadX = spreadX;
                RotationSign = rotationSign;
                FallMultiplier = fallMultiplier;
                RiseMultiplier = riseMultiplier;
                DelaySeconds = delaySeconds;
            }

            public Vector2 AnchoredPosition { get; }

            public Vector2 SizeDelta { get; }

            public float RotationDegrees { get; }

            public float SpreadX { get; }

            public float RotationSign { get; }

            public float FallMultiplier { get; }

            public float RiseMultiplier { get; }

            public float DelaySeconds { get; }
        }

    }
}
