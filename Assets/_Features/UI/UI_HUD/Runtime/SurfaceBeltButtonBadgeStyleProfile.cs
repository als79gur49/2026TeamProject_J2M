using System;
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
                    new Color(0.12f, 0.12f, 0.14f, 0.94f),
                    Color.white),
                new ButtonBadgeVisualStateStyle(
                    new Color(0.12f, 0.12f, 0.14f, 0.38f),
                    new Color(1.0f, 1.0f, 1.0f, 0.58f)));

        [SerializeField] private ButtonBadgeVisualStyle _moonBlockOnlyButton =
            new ButtonBadgeVisualStyle(
                new ButtonBadgeVisualStateStyle(
                    new Color(0.16f, 0.28f, 0.72f, 0.94f),
                    Color.white),
                new ButtonBadgeVisualStateStyle(
                    new Color(0.16f, 0.28f, 0.72f, 0.38f),
                    new Color(1.0f, 1.0f, 1.0f, 0.58f)));

        public ButtonBadgeVisualStyle NormalButton => _normalButton;

        public ButtonBadgeVisualStyle MoonBlockOnlyButton => _moonBlockOnlyButton;

        public bool TryValidate(out string message)
        {
            if (!_normalButton.TryValidate("Normal Button", out message))
            {
                return false;
            }

            if (!_moonBlockOnlyButton.TryValidate("MoonBlockOnly Button", out message))
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
        [SerializeField] private Color _textColor;

        public ButtonBadgeVisualStateStyle(Color backgroundColor, Color textColor)
        {
            _backgroundColor = backgroundColor;
            _textColor = textColor;
        }

        public Color BackgroundColor => _backgroundColor;

        public Color TextColor => _textColor;

        public bool TryValidate(string label, out string message)
        {
            if (_backgroundColor.a <= 0.0f)
            {
                message = $"{label} badge background alpha must be greater than zero.";
                return false;
            }

            if (_textColor.a <= 0.0f)
            {
                message = $"{label} badge text alpha must be greater than zero.";
                return false;
            }

            message = string.Empty;
            return true;
        }
    }
}
