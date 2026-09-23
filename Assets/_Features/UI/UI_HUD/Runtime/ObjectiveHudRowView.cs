using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public enum ObjectiveRowVisualState
    {
        Hidden,
        Entering,
        Idle,
        Completing,
        WaitingForOut,
        Collapsing,
    }

    public enum ObjectiveRowTransitionKind
    {
        Enter,
        Dismiss,
    }

    public sealed class ObjectiveHudRowView : MonoBehaviour
    {
        private const int BaseLayerIndex = 0;
        private const float DefaultFullHeight = 60.0f;

        [SerializeField] private TMP_Text _label;
        [SerializeField] private Animator _animator;
        [SerializeField] private LayoutElement _layoutElement;
        [SerializeField] private Graphic _progressHighlightGraphic;
        [SerializeField] private CanvasGroup _progressHighlightGroup;
        [SerializeField] private Graphic[] _progressHighlightGraphics = Array.Empty<Graphic>();
        [SerializeField] private Transform _progressPulseScaleTarget;
        [SerializeField] private float maxTransitionDeltaSeconds = 1.0f / 30.0f;
        [SerializeField] private bool _enableProgressPulse = true;
        [SerializeField] private bool _enableProgressScalePulse = true;
        [SerializeField] private float progressPulseDuration = 0.22f;
        [SerializeField] private float progressPulseScale = 1.03f;
        [SerializeField] private float _progressHighlightMaxAlpha = 0.35f;
        [SerializeField] private Color _progressHighlightColor = new Color(1.0f, 0.92f, 0.55f, 1.0f);
        [SerializeField] private ObjectiveHudRowTransitionSettings _transitionSettings =
            new ObjectiveHudRowTransitionSettings();

        private float _fullHeight = DefaultFullHeight;
        private float _heightFrom;
        private float _heightTo;
        private float _heightDuration;
        private float _heightElapsed;
        private bool _hasBoundModel;
        private bool _wasSatisfied;
        private bool _enterFinishedRaised;
        private bool _dismissFinishedRaised;
        private int _outStateHash;
        private bool _isProgressPulsePlaying;
        private float _progressPulseElapsed;
        private Vector3 _progressPulseBaseScale = Vector3.one;
        private bool _hasProgressPulseBaseScale;
        private Graphic[] _resolvedProgressHighlightGraphics = Array.Empty<Graphic>();
        private Color[] _progressHighlightBaseColors = Array.Empty<Color>();
        private bool _hasProgressHighlightBaseColors;
        private float _progressHighlightAlpha;
        private bool _missingProgressHighlightWarningRaised;
        private IObjectiveHudTypographyApplicator _typographyApplicator;

        public event Action<ObjectiveHudRowView> DismissFinished;
        public event Action<ObjectiveHudRowView, ObjectiveRowTransitionKind> TransitionFinished;

        public string StableId { get; private set; } = string.Empty;

        public ObjectiveRowVisualState VisualState { get; private set; } =
            ObjectiveRowVisualState.Hidden;

        internal int ProgressPulsePlayCount { get; private set; }

        internal float ProgressHighlightAlphaForTests => GetProgressHighlightAlpha();

        internal bool IsProgressPulsePlayingForTests => _isProgressPulsePlaying;

        internal Transform ProgressPulseScaleTargetForTests => _progressPulseScaleTarget;

        internal TMP_Text LabelForTests => _label;

        public void ConfigureTypography(IObjectiveHudTypographyApplicator typographyApplicator)
        {
            _typographyApplicator = typographyApplicator;
            RefreshTypography();
        }

        public void RefreshTypography()
        {
            ResolveReferences();
            _typographyApplicator?.ApplyRow(_label);
        }

        public void Initialize()
        {
            ResolveReferences();
            ValidateAuthoredStructureOrThrow();
            _fullHeight = ResolveFullHeight();
            _outStateHash = Animator.StringToHash(Settings.OutStateName);
            CaptureProgressPulseBaseScaleIfNeeded();
            CaptureProgressHighlightBaseColorIfNeeded();
        }

        public void Bind(ObjectiveConditionHudViewModel model)
        {
            Refresh(model);
        }

        public void RefreshContent(ObjectiveConditionHudViewModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (_hasBoundModel && !string.Equals(StableId, model.StableId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{nameof(ObjectiveHudRowView)} cannot refresh content for a different stable id.");
            }

            ResolveReferences();
            _label.text = model.Text;
            RefreshTypography();
        }

        public void PlayEnter(ObjectiveConditionHudViewModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            Initialize();
            gameObject.SetActive(true);
            StableId = model.StableId ?? string.Empty;
            _hasBoundModel = true;
            _wasSatisfied = model.IsSatisfied;
            _enterFinishedRaised = false;
            _dismissFinishedRaised = false;
            RefreshContent(model);
            _layoutElement.ignoreLayout = false;
            ResetProgressPulseState();
            SetAnimatorBool(false);
            PlayAnimatorState(Settings.InStateName);
            BeginHeightTween(0.0f, _fullHeight, Settings.EnterDuration, ObjectiveRowVisualState.Entering);
            if (VisualState == ObjectiveRowVisualState.Idle)
            {
                RaiseEnterFinished();
            }
        }

        public void Refresh(ObjectiveConditionHudViewModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            Initialize();
            if (_hasBoundModel &&
                !string.Equals(StableId, model.StableId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{nameof(ObjectiveHudRowView)} cannot be rebound from '{StableId}' to '{model.StableId}' while active.");
            }

            if (!_hasBoundModel)
            {
                StableId = model.StableId ?? string.Empty;
                _hasBoundModel = true;
            }

            RefreshContent(model);

            if (IsDismissing)
            {
                return;
            }

            if (!model.IsSatisfied)
            {
                SetAnimatorBool(false);
                if (_wasSatisfied)
                {
                    PlayAnimatorState(Settings.InactiveStateName);
                }

                _wasSatisfied = false;
                VisualState = ObjectiveRowVisualState.Idle;
                SetHeight(_fullHeight);
                return;
            }

            _wasSatisfied = true;
            VisualState = ObjectiveRowVisualState.Idle;
            SetHeight(_fullHeight);
        }

        public void CompleteAndDismiss()
        {
            if (IsDismissing || VisualState == ObjectiveRowVisualState.Hidden)
            {
                return;
            }

            Initialize();
            _wasSatisfied = true;
            ResetProgressPulseState();
            SetAnimatorBool(true);
            PlayAnimatorState(Settings.ActiveStateName);
            VisualState = ObjectiveRowVisualState.Completing;
        }

        public void ExitAndDismiss()
        {
            if (IsDismissing || VisualState == ObjectiveRowVisualState.Hidden)
            {
                return;
            }

            Initialize();
            ResetProgressPulseState();
            SetAnimatorBool(true);
            PlayAnimatorState(Settings.ActiveStateName);
            VisualState = ObjectiveRowVisualState.Completing;
        }

        public void PlayProgressPulse()
        {
            if (!_enableProgressPulse ||
                VisualState != ObjectiveRowVisualState.Idle ||
                IsDismissing ||
                !gameObject.activeInHierarchy)
            {
                return;
            }

            if (!HasProgressHighlightTarget() &&
                (!_enableProgressScalePulse || _progressPulseScaleTarget == null))
            {
                RaiseMissingProgressHighlightWarningOnce();
                return;
            }

            CaptureProgressPulseBaseScaleIfNeeded();
            CaptureProgressHighlightBaseColorIfNeeded();
            _isProgressPulsePlaying = true;
            _progressPulseElapsed = 0.0f;
            ProgressPulsePlayCount++;
            SetProgressHighlightAlpha(_progressHighlightMaxAlpha);

            if (_enableProgressScalePulse && _progressPulseScaleTarget != null)
            {
                SetProgressPulseScale(progressPulseScale);
            }
        }

        public void ForceResetForPool()
        {
            ResolveReferences();
            StableId = string.Empty;
            _hasBoundModel = false;
            _wasSatisfied = false;
            _heightElapsed = 0.0f;
            _heightDuration = 0.0f;
            _enterFinishedRaised = false;
            _dismissFinishedRaised = false;
            ResetProgressPulseState();
            ProgressPulsePlayCount = 0;

            if (_layoutElement != null)
            {
                SetHeight(_fullHeight > 0.0f ? _fullHeight : ResolveFullHeight());
                _layoutElement.ignoreLayout = true;
            }

            VisualState = ObjectiveRowVisualState.Hidden;
        }

        public void Tick(float deltaTime)
        {
            AdvanceProgressPulse(deltaTime);

            switch (VisualState)
            {
                case ObjectiveRowVisualState.Entering:
                    AdvanceHeightTween(ClampTransitionDelta(deltaTime), ObjectiveRowVisualState.Idle);
                    if (VisualState == ObjectiveRowVisualState.Idle)
                    {
                        RaiseEnterFinished();
                    }

                    break;

                case ObjectiveRowVisualState.Completing:
                    VisualState = ObjectiveRowVisualState.WaitingForOut;
                    break;

                case ObjectiveRowVisualState.WaitingForOut:
                    if (IsOutFinished())
                    {
                        BeginHeightTween(
                            CurrentHeightOrFullHeight(),
                            0.0f,
                            Settings.CollapseDuration,
                            ObjectiveRowVisualState.Collapsing);
                        if (VisualState == ObjectiveRowVisualState.Hidden)
                        {
                            RaiseDismissFinished();
                        }
                    }

                    break;

                case ObjectiveRowVisualState.Collapsing:
                    AdvanceHeightTween(ClampTransitionDelta(deltaTime), ObjectiveRowVisualState.Hidden);
                    if (VisualState == ObjectiveRowVisualState.Hidden)
                    {
                        RaiseDismissFinished();
                    }

                    break;
            }
        }

        public void ValidateAuthoredStructureOrThrow()
        {
            RequireReference(_label, nameof(_label));
            RequireReference(_animator, nameof(_animator));
            RequireReference(_layoutElement, nameof(_layoutElement));
        }

        private bool IsDismissing =>
            VisualState == ObjectiveRowVisualState.Completing ||
            VisualState == ObjectiveRowVisualState.WaitingForOut ||
            VisualState == ObjectiveRowVisualState.Collapsing;

        private ObjectiveHudRowTransitionSettings Settings =>
            _transitionSettings ?? (_transitionSettings = new ObjectiveHudRowTransitionSettings());

        private void RaiseEnterFinished()
        {
            if (_enterFinishedRaised)
            {
                return;
            }

            _enterFinishedRaised = true;
            TransitionFinished?.Invoke(this, ObjectiveRowTransitionKind.Enter);
        }

        private void RaiseDismissFinished()
        {
            if (_dismissFinishedRaised)
            {
                return;
            }

            _dismissFinishedRaised = true;
            TransitionFinished?.Invoke(this, ObjectiveRowTransitionKind.Dismiss);
            DismissFinished?.Invoke(this);
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            ResetProgressPulseState();
        }

        private float ClampTransitionDelta(float deltaTime)
        {
            var safeDelta = Mathf.Max(0.0f, deltaTime);
            return maxTransitionDeltaSeconds > 0.0f
                ? Mathf.Min(safeDelta, maxTransitionDeltaSeconds)
                : safeDelta;
        }

        private void ResolveReferences()
        {
            if (_label == null || _label.name != "Label_Objective")
            {
                var label = FindLabel(gameObject);
                if (label != null)
                {
                    _label = label;
                }
            }

            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }

            if (_layoutElement == null)
            {
                _layoutElement = GetComponent<LayoutElement>();
            }
        }

        private void CaptureProgressPulseBaseScaleIfNeeded()
        {
            if (_hasProgressPulseBaseScale)
            {
                return;
            }

            _progressPulseBaseScale = _progressPulseScaleTarget != null
                ? _progressPulseScaleTarget.localScale
                : Vector3.one;
            _hasProgressPulseBaseScale = true;
        }

        private void CaptureProgressHighlightBaseColorIfNeeded()
        {
            if (_hasProgressHighlightBaseColors)
            {
                return;
            }

            _resolvedProgressHighlightGraphics = ResolveProgressHighlightGraphics();
            _progressHighlightBaseColors = new Color[_resolvedProgressHighlightGraphics.Length];
            for (var i = 0; i < _resolvedProgressHighlightGraphics.Length; i++)
            {
                _progressHighlightBaseColors[i] = _resolvedProgressHighlightGraphics[i].color;
            }

            _hasProgressHighlightBaseColors = true;
        }

        private void ResetProgressPulseState()
        {
            _isProgressPulsePlaying = false;
            _progressPulseElapsed = 0.0f;
            SetProgressHighlightAlpha(0.0f);

            if (_hasProgressPulseBaseScale && _progressPulseScaleTarget != null)
            {
                _progressPulseScaleTarget.localScale = _progressPulseBaseScale;
            }
        }

        private void AdvanceProgressPulse(float deltaTime)
        {
            if (!_isProgressPulsePlaying)
            {
                return;
            }

            var duration = Mathf.Max(0.0f, progressPulseDuration);
            if (duration <= 0.0f)
            {
                ResetProgressPulseState();
                return;
            }

            _progressPulseElapsed += Mathf.Max(0.0f, deltaTime);
            var progress = Mathf.Clamp01(_progressPulseElapsed / duration);
            var remaining = 1.0f - EaseOutQuad(progress);
            SetProgressHighlightAlpha(_progressHighlightMaxAlpha * remaining);

            if (_enableProgressScalePulse && _progressPulseScaleTarget != null)
            {
                var scale = Mathf.Lerp(Mathf.Max(1.0f, progressPulseScale), 1.0f, progress);
                SetProgressPulseScale(scale);
            }

            if (progress >= 1.0f)
            {
                ResetProgressPulseState();
            }
        }

        private void SetProgressPulseScale(float scale)
        {
            if (_progressPulseScaleTarget == null)
            {
                return;
            }

            CaptureProgressPulseBaseScaleIfNeeded();
            _progressPulseScaleTarget.localScale = _progressPulseBaseScale * Mathf.Max(0.0f, scale);
        }

        private bool HasProgressHighlightTarget()
        {
            return _progressHighlightGroup != null ||
                   _progressHighlightGraphic != null ||
                   HasProgressHighlightGraphics();
        }

        private float GetProgressHighlightAlpha()
        {
            return _progressHighlightAlpha;
        }

        private void SetProgressHighlightAlpha(float alpha)
        {
            var clampedAlpha = Mathf.Clamp01(alpha);
            _progressHighlightAlpha = clampedAlpha;
            if (_progressHighlightGroup != null)
            {
                _progressHighlightGroup.alpha = clampedAlpha;
            }

            CaptureProgressHighlightBaseColorIfNeeded();
            for (var i = 0; i < _resolvedProgressHighlightGraphics.Length; i++)
            {
                var graphic = _resolvedProgressHighlightGraphics[i];
                if (graphic == null)
                {
                    continue;
                }

                var baseColor = i < _progressHighlightBaseColors.Length
                    ? _progressHighlightBaseColors[i]
                    : graphic.color;
                graphic.color = ResolveProgressHighlightColor(graphic, baseColor, clampedAlpha);
            }
        }

        private Color ResolveProgressHighlightColor(Graphic graphic, Color baseColor, float alpha)
        {
            if (graphic == _progressHighlightGraphic && _progressHighlightGroup != null)
            {
                var color = _progressHighlightColor;
                color.a = Mathf.Clamp01(color.a);
                return color;
            }

            if (graphic == _progressHighlightGraphic && !HasAdditionalProgressHighlightGraphics())
            {
                var color = _progressHighlightColor;
                color.a = alpha;
                return color;
            }

            var tintAmount = _progressHighlightMaxAlpha > 0.0f
                ? Mathf.Clamp01(alpha / _progressHighlightMaxAlpha)
                : alpha;
            var tinted = Color.Lerp(baseColor, _progressHighlightColor, tintAmount);
            tinted.a = Mathf.Lerp(
                baseColor.a,
                Mathf.Max(baseColor.a, Mathf.Clamp01(_progressHighlightColor.a)),
                tintAmount);
            return tinted;
        }

        private bool HasProgressHighlightGraphics()
        {
            if (_progressHighlightGraphics == null)
            {
                return false;
            }

            for (var i = 0; i < _progressHighlightGraphics.Length; i++)
            {
                if (_progressHighlightGraphics[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasAdditionalProgressHighlightGraphics()
        {
            return HasProgressHighlightGraphics();
        }

        private Graphic[] ResolveProgressHighlightGraphics()
        {
            var graphics = new List<Graphic>();
            AddProgressHighlightGraphic(graphics, _progressHighlightGraphic);

            if (_progressHighlightGraphics != null)
            {
                for (var i = 0; i < _progressHighlightGraphics.Length; i++)
                {
                    AddProgressHighlightGraphic(graphics, _progressHighlightGraphics[i]);
                }
            }

            return graphics.ToArray();
        }

        private static void AddProgressHighlightGraphic(List<Graphic> graphics, Graphic graphic)
        {
            if (graphic == null || graphics.Contains(graphic))
            {
                return;
            }

            graphics.Add(graphic);
        }

        private static float EaseOutQuad(float value)
        {
            var clamped = Mathf.Clamp01(value);
            return 1.0f - (1.0f - clamped) * (1.0f - clamped);
        }

        private void RaiseMissingProgressHighlightWarningOnce()
        {
            if (_missingProgressHighlightWarningRaised)
            {
                return;
            }

            _missingProgressHighlightWarningRaised = true;
            Debug.LogWarning(
                $"{nameof(ObjectiveHudRowView)} on '{name}' is missing a prefab-authored progress highlight reference.",
                this);
        }

        private void BeginHeightTween(
            float from,
            float to,
            float duration,
            ObjectiveRowVisualState state)
        {
            _heightFrom = from;
            _heightTo = to;
            _heightDuration = Mathf.Max(0.0f, duration);
            _heightElapsed = 0.0f;
            VisualState = state;
            SetHeight(from);

            if (_heightDuration <= 0.0f)
            {
                SetHeight(to);
                VisualState = state == ObjectiveRowVisualState.Entering
                    ? ObjectiveRowVisualState.Idle
                    : ObjectiveRowVisualState.Hidden;
            }
        }

        private void AdvanceHeightTween(float deltaTime, ObjectiveRowVisualState completedState)
        {
            if (_heightDuration <= 0.0f)
            {
                SetHeight(_heightTo);
                VisualState = completedState;
                return;
            }

            _heightElapsed += Mathf.Max(0.0f, deltaTime);
            var progress = Mathf.Clamp01(_heightElapsed / _heightDuration);
            SetHeight(Mathf.Lerp(_heightFrom, _heightTo, progress));
            if (progress >= 1.0f)
            {
                VisualState = completedState;
            }
        }

        private void SetHeight(float height)
        {
            if (_layoutElement == null)
            {
                return;
            }

            var clamped = Mathf.Max(0.0f, height);
            _layoutElement.ignoreLayout = false;
            _layoutElement.minHeight = clamped;
            _layoutElement.preferredHeight = clamped;
        }

        private float CurrentHeightOrFullHeight()
        {
            if (_layoutElement == null || _layoutElement.preferredHeight <= 0.0f)
            {
                return _fullHeight;
            }

            return _layoutElement.preferredHeight;
        }

        private float ResolveFullHeight()
        {
            if (_layoutElement != null && _layoutElement.preferredHeight > 0.0f)
            {
                return _layoutElement.preferredHeight;
            }

            if (Settings.FullHeight > 0.0f)
            {
                return Settings.FullHeight;
            }

            if (transform is RectTransform rectTransform && rectTransform.sizeDelta.y > 0.0f)
            {
                return rectTransform.sizeDelta.y;
            }

            return DefaultFullHeight;
        }

        private bool IsOutFinished()
        {
            if (_animator == null)
            {
                return true;
            }

            var state = _animator.GetCurrentAnimatorStateInfo(BaseLayerIndex);
            return !_animator.IsInTransition(BaseLayerIndex) &&
                   state.shortNameHash == _outStateHash &&
                   state.normalizedTime >= 1.0f;
        }

        private void SetAnimatorBool(bool value)
        {
            if (_animator == null ||
                !gameObject.activeInHierarchy ||
                string.IsNullOrWhiteSpace(Settings.ActiveBoolParameter) ||
                !HasAnimatorBoolParameter(Settings.ActiveBoolParameter))
            {
                return;
            }

            _animator.SetBool(Settings.ActiveBoolParameter, value);
        }

        private bool HasAnimatorBoolParameter(string parameterName)
        {
            var parameters = _animator.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == AnimatorControllerParameterType.Bool &&
                    string.Equals(parameters[i].name, parameterName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void PlayAnimatorState(string stateName)
        {
            if (_animator == null ||
                !gameObject.activeInHierarchy ||
                string.IsNullOrWhiteSpace(stateName))
            {
                return;
            }

            _animator.Play(stateName, BaseLayerIndex, 0.0f);
            _animator.Update(0.0f);
        }

        private static TMP_Text FindLabel(GameObject root)
        {
            if (root == null)
            {
                return null;
            }

            var labels = root.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                if (labels[i].name == "Label_Objective")
                {
                    return labels[i];
                }
            }

            return labels.Length > 0 ? labels[0] : null;
        }

        private static void RequireReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(ObjectiveHudRowView)} is missing authored reference '{fieldName}'.");
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_label == null)
            {
                Debug.LogWarning($"{nameof(ObjectiveHudRowView)} on '{name}' is missing serialized reference '{nameof(_label)}'.", this);
            }

            if (_animator == null)
            {
                Debug.LogWarning($"{nameof(ObjectiveHudRowView)} on '{name}' is missing serialized reference '{nameof(_animator)}'.", this);
            }

            if (_layoutElement == null)
            {
                Debug.LogWarning($"{nameof(ObjectiveHudRowView)} on '{name}' is missing serialized reference '{nameof(_layoutElement)}'.", this);
            }

            if (_progressHighlightGraphic == null &&
                _progressHighlightGroup == null &&
                !HasProgressHighlightGraphics())
            {
                Debug.LogWarning(
                    $"{nameof(ObjectiveHudRowView)} on '{name}' is missing a serialized progress highlight reference.",
                    this);
            }

            if (_progressHighlightGraphic != null && _progressHighlightGraphic.raycastTarget)
            {
                Debug.LogWarning(
                    $"{nameof(ObjectiveHudRowView)} on '{name}' progress highlight must have Raycast Target disabled.",
                    _progressHighlightGraphic);
            }

            if (_progressHighlightGraphics != null)
            {
                for (var i = 0; i < _progressHighlightGraphics.Length; i++)
                {
                    var graphic = _progressHighlightGraphics[i];
                    if (graphic != null && graphic.raycastTarget)
                    {
                        Debug.LogWarning(
                            $"{nameof(ObjectiveHudRowView)} on '{name}' progress highlight must have Raycast Target disabled.",
                            graphic);
                    }
                }
            }
        }
#endif
    }
}
