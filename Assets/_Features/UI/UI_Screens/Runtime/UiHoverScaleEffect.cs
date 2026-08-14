using DG.Tweening;
using Game.Feature.UI.ViewShared;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class UiHoverScaleEffect : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerClickHandler,
        ISelectHandler,
        IDeselectHandler,
        IUiSelectionFeedback
    {
        [SerializeField] private RectTransform _target;
        [SerializeField] private float _hoverScale = 1.1f;
        [SerializeField] private float _pressedScale = 1.04f;
        [SerializeField] private float _clickPunchStrength = 0.08f;
        [SerializeField] private float _clickPunchDurationSeconds = 0.18f;
        [SerializeField] private int _clickPunchVibrato = 6;
        [SerializeField] private float _clickPunchElasticity = 0.65f;
        [SerializeField] private float _durationSeconds = 0.12f;
        [SerializeField] private Ease _ease = Ease.OutQuad;
        [SerializeField] private bool _useUnscaledTime = true;
        [SerializeField] private bool _restoreOnDisable = true;

        private Tween _scaleTween;
        private Tween _clickPunchTween;
        private Selectable _selectable;
        private Vector3 _baseScale = Vector3.one;
        private bool _hasBaseScale;
        private bool _hasInteractionAllowedState;
        private bool _wasInteractionAllowed;
        private bool _isHovered;
        private bool _isPressed;
        private bool _isSelected;

        public RectTransform Target => ResolveTarget();

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            RefreshScale(animate: true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            _isPressed = false;
            RefreshScale(animate: true);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsPrimaryPointer(eventData) || !IsInteractionAllowed())
            {
                return;
            }

            _isPressed = true;
            RefreshScale(animate: true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!IsPrimaryPointer(eventData))
            {
                return;
            }

            _isPressed = false;
            RefreshScale(animate: true);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsPrimaryPointer(eventData) || !IsInteractionAllowed())
            {
                return;
            }

            PlayClickPunch();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _isSelected = true;
            RefreshScale(animate: true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _isSelected = false;
            _isPressed = false;
            RefreshScale(animate: true);
        }

        public void SetNavigationFocused(bool focused)
        {
            _isSelected = focused;
            if (!focused)
            {
                _isPressed = false;
            }

            RefreshScale(animate: CanAnimateScale());
        }

        public void PlaySubmitFeedback()
        {
            PlayClickPunch();
        }

        private void Reset()
        {
            _target = transform as RectTransform;
        }

        private void Awake()
        {
            ResolveSelectable();
            CaptureBaseScale();
        }

        private void OnEnable()
        {
            CaptureBaseScale();
            _wasInteractionAllowed = IsInteractionAllowed();
            _hasInteractionAllowedState = true;
            RefreshScale(animate: false);
        }

        private void Update()
        {
            var interactionAllowed = IsInteractionAllowed();
            if (_hasInteractionAllowedState && interactionAllowed == _wasInteractionAllowed)
            {
                return;
            }

            _wasInteractionAllowed = interactionAllowed;
            _hasInteractionAllowedState = true;
            if (!interactionAllowed)
            {
                _isPressed = false;
            }

            RefreshScale(animate: CanAnimateScale());
        }

        private void OnDisable()
        {
            KillTween();
            KillClickPunchTween();
            _isHovered = false;
            _isPressed = false;
            _isSelected = false;
            _hasInteractionAllowedState = false;

            if (_restoreOnDisable && _hasBaseScale)
            {
                var target = ResolveTarget();
                if (target != null)
                {
                    target.localScale = _baseScale;
                }
            }
        }

        private void OnDestroy()
        {
            KillTween();
            KillClickPunchTween();
        }

        private void CaptureBaseScale()
        {
            if (_hasBaseScale)
            {
                return;
            }

            var target = ResolveTarget();
            if (target == null)
            {
                return;
            }

            _baseScale = target.localScale;
            _hasBaseScale = true;
        }

        private void RefreshScale(bool animate)
        {
            CaptureBaseScale();

            var target = ResolveTarget();
            if (target == null || !_hasBaseScale)
            {
                return;
            }

            var scaleMultiplier = ResolveScaleMultiplier();
            var targetScale = _baseScale * scaleMultiplier;

            KillClickPunchTween();
            KillTween();
            if (!animate || _durationSeconds <= 0f)
            {
                target.localScale = targetScale;
                return;
            }

            _scaleTween = target
                .DOScale(targetScale, _durationSeconds)
                .SetEase(_ease)
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() => _scaleTween = null)
                .OnKill(() => _scaleTween = null);
        }

        private void PlayClickPunch()
        {
            CaptureBaseScale();

            var target = ResolveTarget();
            if (target == null ||
                !_hasBaseScale ||
                !IsInteractionAllowed() ||
                _clickPunchStrength <= 0f ||
                _clickPunchDurationSeconds <= 0f)
            {
                return;
            }

            KillTween();
            KillClickPunchTween();

            var restScale = _baseScale * ResolveScaleMultiplier();
            target.localScale = restScale;

            _clickPunchTween = target
                .DOPunchScale(
                    restScale * Mathf.Max(0f, _clickPunchStrength),
                    Mathf.Max(0.01f, _clickPunchDurationSeconds),
                    Mathf.Max(0, _clickPunchVibrato),
                    Mathf.Max(0f, _clickPunchElasticity))
                .SetEase(_ease)
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() =>
                {
                    _clickPunchTween = null;
                    RefreshScale(animate: true);
                })
                .OnKill(() => _clickPunchTween = null);
        }

        private float ResolveScaleMultiplier()
        {
            if (!IsInteractionAllowed())
            {
                return 1f;
            }

            if (_isPressed)
            {
                return Mathf.Max(0f, _pressedScale);
            }

            return _isHovered || _isSelected ? Mathf.Max(0f, _hoverScale) : 1f;
        }

        private RectTransform ResolveTarget()
        {
            return _target != null ? _target : transform as RectTransform;
        }

        private Selectable ResolveSelectable()
        {
            if (_selectable == null)
            {
                _selectable = GetComponent<Selectable>();
            }

            return _selectable;
        }

        private bool IsInteractionAllowed()
        {
            var selectable = ResolveSelectable();
            return selectable != null && selectable.IsActive() && selectable.IsInteractable();
        }

        private static bool IsPrimaryPointer(PointerEventData eventData)
        {
            return eventData != null && eventData.button == PointerEventData.InputButton.Left;
        }

        private bool CanAnimateScale()
        {
            return isActiveAndEnabled && gameObject.activeInHierarchy;
        }

        private void KillTween()
        {
            if (_scaleTween == null)
            {
                return;
            }

            _scaleTween.Kill();
            _scaleTween = null;
        }

        private void KillClickPunchTween()
        {
            if (_clickPunchTween == null)
            {
                return;
            }

            _clickPunchTween.Kill();
            _clickPunchTween = null;
        }
    }
}
