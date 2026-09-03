using System;
using UnityEngine;

namespace Game.Feature.UI.HUD
{
    public sealed class SurfaceBeltButtonBadgeGroupView : MonoBehaviour
    {
        [SerializeField] private SurfaceBeltButtonBadgeView _normalBadge;

        public SurfaceBeltButtonBadgeView NormalBadge => _normalBadge;

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
                remainder.HasAnyRemaining,
                styleProfile.NormalButton,
                styleProfile.Transition);
        }

        public void ValidateAuthoredStructureOrThrow()
        {
            RequireReference(_normalBadge, nameof(_normalBadge));
            _normalBadge.ValidateAuthoredStructureOrThrow();
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
