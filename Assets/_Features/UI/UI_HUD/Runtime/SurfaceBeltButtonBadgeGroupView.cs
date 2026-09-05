using System;
using UnityEngine;

namespace Game.Feature.UI.HUD
{
    public sealed class SurfaceBeltButtonBadgeGroupView : MonoBehaviour
    {
        [SerializeField] private SurfaceBeltButtonBadgeView _normalBadge;
        [SerializeField] private SurfaceBeltButtonBadgeView _moonBadge;

        public SurfaceBeltButtonBadgeView NormalBadge => _normalBadge;

        public SurfaceBeltButtonBadgeView MoonBadge => _moonBadge;

        public void Bind(
            SurfaceBeltButtonRemainderViewModel remainder,
            SurfaceBeltButtonBadgeStyleProfile styleProfile)
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

            _normalBadge.Bind(
                remainder.SlotIndex,
                remainder.NormalRemaining > 0,
                styleProfile.NormalButton,
                styleProfile.Transition);
            _moonBadge.Bind(
                remainder.SlotIndex,
                remainder.MoonBlockOnlyRemaining > 0,
                styleProfile.MoonButton,
                styleProfile.Transition);
        }

        public void ValidateAuthoredStructureOrThrow()
        {
            RequireReference(_normalBadge, nameof(_normalBadge));
            _normalBadge.ValidateAuthoredStructureOrThrow();
            RequireReference(_moonBadge, nameof(_moonBadge));
            _moonBadge.ValidateAuthoredStructureOrThrow();
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
