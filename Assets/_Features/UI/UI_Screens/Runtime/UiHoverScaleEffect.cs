using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Feature.UI.Screens
{
    public sealed class UiHoverScaleEffect : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        ISelectHandler,
        IDeselectHandler
    {
        [SerializeField] private RectTransform _target;
        [SerializeField] private float _hoverScale = 1.1f;
        [SerializeField] private float _pressedScale = 1.04f;
        [SerializeField] private float _durationSeconds = 0.12f;
        [SerializeField] private Ease _ease = Ease.OutQuad;
        [SerializeField] private bool _useUnscaledTime = true;
        [SerializeField] private bool _restoreOnDisable = true;

        private Tween _scaleTween;
        private Vector3 _baseScale = Vector3.one;
        private bool _hasBaseScale;
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
            _isPressed = true;
            RefreshScale(animate: true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed = false;
            RefreshScale(animate: true);
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

        private void Reset()
        {
            _target = transform as RectTransform;
        }

        private void Awake()
        {
            CaptureBaseScale();
        }

        private void OnEnable()
        {
            CaptureBaseScale();
            RefreshScale(animate: false);
        }

        private void OnDisable()
        {
            KillTween();
            _isHovered = false;
            _isPressed = false;
            _isSelected = false;

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

            KillTween();
            if (!animate || _durationSeconds <= 0f)
            {
                target.localScale = targetScale;
                return;
            }

            _scaleTween = target
                .DOScale(targetScale, _durationSeconds)
                .SetEase(_ease)
                .SetUpdate(_useUnscaledTime);
        }

        private float ResolveScaleMultiplier()
        {
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

        private void KillTween()
        {
            if (_scaleTween == null)
            {
                return;
            }

            _scaleTween.Kill();
            _scaleTween = null;
        }
    }
}
