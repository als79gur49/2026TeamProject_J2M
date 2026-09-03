using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public sealed class SurfaceBeltCellView : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private Image _slotTintEffectImage;
        [SerializeField] private SurfaceBeltButtonBadgeGroupView _buttonBadgeGroup;

        public void Bind(
            SurfaceBeltCellViewModel viewModel,
            SurfaceBeltStyleProfile styleProfile,
            SurfaceBeltButtonRemainderViewModel buttonRemainder,
            SurfaceBeltButtonBadgeStyleProfile buttonBadgeStyleProfile)
        {
            ValidateAuthoredStructureOrThrow();
            if (styleProfile == null)
            {
                throw new InvalidOperationException($"{nameof(SurfaceBeltCellView)} is missing a style profile.");
            }

            if (!styleProfile.TryGetStyle(viewModel.SlotIndex, out var style))
            {
                throw new InvalidOperationException($"Surface belt style for slot {viewModel.SlotIndex} was not found.");
            }

            _background.color = style.Color;
            if (_slotTintEffectImage != null)
            {
                var effectColor = style.Color;
                effectColor.a = _slotTintEffectImage.color.a;
                _slotTintEffectImage.color = effectColor;
            }

            if (_buttonBadgeGroup != null)
            {
                _buttonBadgeGroup.Bind(buttonRemainder, buttonBadgeStyleProfile);
            }
        }

        public void ValidateAuthoredStructureOrThrow()
        {
            RequireReference(_background, nameof(_background));
            if (_buttonBadgeGroup != null)
            {
                _buttonBadgeGroup.ValidateAuthoredStructureOrThrow();
            }
        }

        private static void RequireReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                throw new InvalidOperationException($"{nameof(SurfaceBeltCellView)} is missing authored reference '{fieldName}'.");
            }
        }
    }
}
