using System;
using DG.Tweening;
using UnityEngine;

namespace Game.Feature.UI.HUD
{
    [CreateAssetMenu(
        fileName = "SurfaceBeltButtonBadgeStyleProfile",
        menuName = "Game/UI/HUD/Surface Belt Button Badge Style Profile")]
    public sealed class SurfaceBeltButtonBadgeStyleProfile : ScriptableObject
    {
        [SerializeField] private ButtonBadgeVisualStyle _normalButton =
            new ButtonBadgeVisualStyle(
                new ButtonBadgeVisualStateStyle(
                    new Color(0.8980392f, 0.8392157f, 0.2941177f, 1.0f)),
                new ButtonBadgeVisualStateStyle(
                    new Color(0.8980392f, 0.8392157f, 0.2941177f, 0.2f)));

        [SerializeField] private ButtonBadgeTransitionStyle _transition =
            new ButtonBadgeTransitionStyle(
                activationDurationSeconds: 0.24f,
                deactivationDurationSeconds: 0.16f,
                slotConfirmationDurationSeconds: 0.18f,
                shineDurationSeconds: 0.24f,
                inactiveScale: 0.92f,
                activationPeakScale: 1.06f,
                slotConfirmationPeakScale: 1.04f,
                activationEase: Ease.OutBack,
                settleEase: Ease.OutQuad,
                shineWidth: 0.14f,
                shineGlow: 2.0f,
                shineRotateRadians: 0.0f,
                shineColor: Color.white,
                useUnscaledTime: true);

        public ButtonBadgeVisualStyle NormalButton => _normalButton;

        public ButtonBadgeTransitionStyle Transition => _transition;

        public bool TryValidate(out string message)
        {
            if (!_normalButton.TryValidate("Button remainder", out message))
            {
                return false;
            }

            if (!_transition.TryValidate(out message))
            {
                return false;
            }

            message = string.Empty;
            return true;
        }

        private void OnValidate()
        {
            if (!TryValidate(out var message))
            {
                Debug.LogWarning(message, this);
            }
        }
    }

    [Serializable]
    public struct ButtonBadgeTransitionStyle
    {
        [SerializeField] private float _activationDurationSeconds;
        [SerializeField] private float _deactivationDurationSeconds;
        [SerializeField] private float _slotConfirmationDurationSeconds;
        [SerializeField] private float _shineDurationSeconds;
        [SerializeField] private float _inactiveScale;
        [SerializeField] private float _activationPeakScale;
        [SerializeField] private float _slotConfirmationPeakScale;
        [SerializeField] private Ease _activationEase;
        [SerializeField] private Ease _settleEase;
        [SerializeField] private float _shineWidth;
        [SerializeField] private float _shineGlow;
        [SerializeField] private float _shineRotateRadians;
        [SerializeField] private Color _shineColor;
        [SerializeField] private bool _useUnscaledTime;

        public ButtonBadgeTransitionStyle(
            float activationDurationSeconds,
            float deactivationDurationSeconds,
            float slotConfirmationDurationSeconds,
            float shineDurationSeconds,
            float inactiveScale,
            float activationPeakScale,
            float slotConfirmationPeakScale,
            Ease activationEase,
            Ease settleEase,
            float shineWidth,
            float shineGlow,
            float shineRotateRadians,
            Color shineColor,
            bool useUnscaledTime)
        {
            _activationDurationSeconds = activationDurationSeconds;
            _deactivationDurationSeconds = deactivationDurationSeconds;
            _slotConfirmationDurationSeconds = slotConfirmationDurationSeconds;
            _shineDurationSeconds = shineDurationSeconds;
            _inactiveScale = inactiveScale;
            _activationPeakScale = activationPeakScale;
            _slotConfirmationPeakScale = slotConfirmationPeakScale;
            _activationEase = activationEase;
            _settleEase = settleEase;
            _shineWidth = shineWidth;
            _shineGlow = shineGlow;
            _shineRotateRadians = shineRotateRadians;
            _shineColor = shineColor;
            _useUnscaledTime = useUnscaledTime;
        }

        public float ActivationDurationSeconds => _activationDurationSeconds;
        public float DeactivationDurationSeconds => _deactivationDurationSeconds;
        public float SlotConfirmationDurationSeconds => _slotConfirmationDurationSeconds;
        public float ShineDurationSeconds => _shineDurationSeconds;
        public float InactiveScale => _inactiveScale;
        public float ActivationPeakScale => _activationPeakScale;
        public float SlotConfirmationPeakScale => _slotConfirmationPeakScale;
        public Ease ActivationEase => _activationEase;
        public Ease SettleEase => _settleEase;
        public float ShineWidth => _shineWidth;
        public float ShineGlow => _shineGlow;
        public float ShineRotateRadians => _shineRotateRadians;
        public Color ShineColor => _shineColor;
        public bool UseUnscaledTime => _useUnscaledTime;

        public bool TryValidate(out string message)
        {
            if (_activationDurationSeconds <= 0.0f ||
                _deactivationDurationSeconds <= 0.0f ||
                _slotConfirmationDurationSeconds <= 0.0f ||
                _shineDurationSeconds <= 0.0f)
            {
                message = "Button badge transition durations must be greater than zero.";
                return false;
            }

            if (_inactiveScale <= 0.0f || _inactiveScale > 1.0f)
            {
                message = "Button badge inactive scale must be greater than zero and no greater than one.";
                return false;
            }

            if (_activationPeakScale < 1.0f || _slotConfirmationPeakScale < 1.0f)
            {
                message = "Button badge peak scales must be at least one.";
                return false;
            }

            if (_shineWidth < 0.05f || _shineWidth > 1.0f)
            {
                message = "Button badge shine width must be between 0.05 and 1.";
                return false;
            }

            if (_shineGlow <= 0.0f)
            {
                message = "Button badge shine glow must be greater than zero.";
                return false;
            }

            if (_shineColor.a <= 0.0f)
            {
                message = "Button badge shine color alpha must be greater than zero.";
                return false;
            }

            message = string.Empty;
            return true;
        }
    }

    [Serializable]
    public struct ButtonBadgeVisualStyle
    {
        [SerializeField] private ButtonBadgeVisualStateStyle _active;
        [SerializeField] private ButtonBadgeVisualStateStyle _inactive;

        public ButtonBadgeVisualStyle(ButtonBadgeVisualStateStyle active, ButtonBadgeVisualStateStyle inactive)
        {
            _active = active;
            _inactive = inactive;
        }

        public ButtonBadgeVisualStateStyle Active => _active;

        public ButtonBadgeVisualStateStyle Inactive => _inactive;

        public ButtonBadgeVisualStateStyle GetStateStyle(bool isActive)
        {
            return isActive ? _active : _inactive;
        }

        public bool TryValidate(string label, out string message)
        {
            if (!_active.TryValidate($"{label} active", out message))
            {
                return false;
            }

            if (!_inactive.TryValidate($"{label} inactive", out message))
            {
                return false;
            }

            message = string.Empty;
            return true;
        }
    }

    [Serializable]
    public struct ButtonBadgeVisualStateStyle
    {
        [SerializeField] private Color _backgroundColor;

        public ButtonBadgeVisualStateStyle(Color backgroundColor)
        {
            _backgroundColor = backgroundColor;
        }

        public Color BackgroundColor => _backgroundColor;

        public bool TryValidate(string label, out string message)
        {
            if (_backgroundColor.a <= 0.0f)
            {
                message = $"{label} badge background alpha must be greater than zero.";
                return false;
            }

            message = string.Empty;
            return true;
        }
    }
}
