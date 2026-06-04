using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public sealed class SurfaceBeltButtonBadgeView : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private TMP_Text _countText;

        public void Bind(int count, ButtonBadgeVisualStyle style)
        {
            ValidateAuthoredStructureOrThrow();
            var clampedCount = Mathf.Max(0, count);
            var stateStyle = style.GetStateStyle(clampedCount > 0);
            gameObject.SetActive(true);

            _background.color = stateStyle.BackgroundColor;
            _countText.color = stateStyle.TextColor;
            _countText.text = clampedCount.ToString(CultureInfo.InvariantCulture);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void ValidateAuthoredStructureOrThrow()
        {
            RequireReference(_background, nameof(_background));
            RequireReference(_countText, nameof(_countText));
        }

        private static void RequireReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                throw new InvalidOperationException($"{nameof(SurfaceBeltButtonBadgeView)} is missing authored reference '{fieldName}'.");
            }
        }
    }
}
