using System;
using UnityEngine;

namespace Game.Feature.UI.HUD
{
    public sealed class SurfaceBeltButtonBadgeGroupView : MonoBehaviour
    {
        [SerializeField] private SurfaceBeltButtonBadgeView _normalBadge;
        [SerializeField] private SurfaceBeltButtonBadgeView _moonBlockOnlyBadge;

        public SurfaceBeltButtonBadgeView NormalBadge => _normalBadge;

        public SurfaceBeltButtonBadgeView MoonBlockOnlyBadge => _moonBlockOnlyBadge;

        public void Bind(
            SurfaceBeltButtonRemainderViewModel remainder,
            SurfaceBeltButtonBadgeStyleProfile styleProfile,
            bool showBadges)
        {
            ValidateAuthoredStructureOrThrow();
            if (styleProfile == null)
            {
                throw new InvalidOperationException($"{nameof(SurfaceBeltButtonBadgeGroupView)} is missing a button badge style profile.");
            }

            if (!styleProfile.TryValidate(out var validationMessage))
            {
                throw new InvalidOperationException(validationMessage);
            }

            if (!showBadges)
            {
                _normalBadge.Hide();
                _moonBlockOnlyBadge.Hide();
                return;
            }

            _normalBadge.Bind(remainder.NormalRemaining, styleProfile.NormalButton);
            _moonBlockOnlyBadge.Bind(remainder.MoonBlockOnlyRemaining, styleProfile.MoonBlockOnlyButton);
        }

        public void ValidateAuthoredStructureOrThrow()
        {
            RequireReference(_normalBadge, nameof(_normalBadge));
            RequireReference(_moonBlockOnlyBadge, nameof(_moonBlockOnlyBadge));
            _normalBadge.ValidateAuthoredStructureOrThrow();
            _moonBlockOnlyBadge.ValidateAuthoredStructureOrThrow();
        }

        private static void RequireReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                throw new InvalidOperationException($"{nameof(SurfaceBeltButtonBadgeGroupView)} is missing authored reference '{fieldName}'.");
            }
        }
    }
}
