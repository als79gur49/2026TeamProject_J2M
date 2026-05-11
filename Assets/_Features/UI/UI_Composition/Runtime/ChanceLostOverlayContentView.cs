using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    internal sealed class ChanceLostOverlayContentView : SceneTransitionOverlayContentView
    {
        private const string ChanceSlotNamePrefix = "ChanceSlotView";
        private const string TweenRootName = "LostChanceTweenRoot";

        [SerializeField] private TMP_Text _previousChanceText;
        [SerializeField] private TMP_Text _currentChanceText;
        [SerializeField] private TMP_Text _totalChanceText;
        [SerializeField] private TMP_Text _deathCountText;
        [SerializeField] private RectTransform[] _chanceSlotRoots;
        [SerializeField] private float _lostShakeDurationSeconds = 0.34f;
        [SerializeField] private float _lostShakeStrength = 20f;
        [SerializeField] private int _lostShakeVibrato = 18;
        [SerializeField] private float _lostFallDistance = 190f;
        [SerializeField] private float _lostFallDurationSeconds = 0.44f;
        [SerializeField] private float _lostFadeDurationSeconds = 0.34f;
        [SerializeField] private float _lostRotationDegrees = -22f;
        [SerializeField] private float _lostSlotStaggerSeconds = 0.05f;
        [SerializeField] private float _emptySlotAlpha = 0.34f;
        [SerializeField] private bool _useUnscaledTime = true;

        private readonly List<SlotState> _slotStates = new();
        private readonly List<RectTransform> _resolvedSlots = new();
        private Sequence _lostChanceSequence;
        private SceneTransitionOverlayViewModel _boundModel;
        private bool _hasBoundModel;

        public override void Bind(SceneTransitionOverlayViewModel model)
        {
            KillLostChanceAnimation();
            RestoreChanceSlots();
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
            base.Hide();
        }

        public override void ResetView()
        {
            KillLostChanceAnimation();
            RestoreChanceSlots();
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
        }

        private void OnDestroy()
        {
            KillLostChanceAnimation();
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

            _lostChanceSequence = DOTween.Sequence()
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

            for (var i = lostStart; i < lostEndExclusive; i++)
            {
                var state = _slotStates[i];
                RestoreSlot(state);
                state.Rect.gameObject.SetActive(true);
                state.CanvasGroup.alpha = state.Alpha;

                var slotSequence = DOTween.Sequence()
                    .SetUpdate(_useUnscaledTime)
                    .AppendInterval((i - lostStart) * Mathf.Max(0f, _lostSlotStaggerSeconds))
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
                        .SetEase(Ease.InQuad));

                _lostChanceSequence.Join(slotSequence);
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
            }

            public RectTransform Rect { get; }

            public RectTransform TweenRect { get; }

            public CanvasGroup CanvasGroup { get; }

            public Vector2 TweenAnchoredPosition { get; }

            public Quaternion TweenLocalRotation { get; }

            public Vector3 TweenLocalScale { get; }

            public float Alpha { get; }

            public bool ActiveSelf { get; }
        }
    }
}
