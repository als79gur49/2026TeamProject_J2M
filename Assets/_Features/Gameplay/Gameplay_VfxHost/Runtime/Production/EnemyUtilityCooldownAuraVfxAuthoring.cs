using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Vfx;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    [DisallowMultipleComponent]
    public sealed class EnemyUtilityCooldownAuraVfxAuthoring : MonoBehaviour
    {
        [SerializeField] private bool enableCooldownAura = true;
        [SerializeField] private EnemyUtilityPresentationKind utilityKind = EnemyUtilityPresentationKind.GravityFieldAura;
        [SerializeField] private EnemyVfxCue cooldownCue = EnemyVfxCue.UtilityCooldownAura;
        [SerializeField] private string attachPointId = "WeaponAura";

        public bool EnableCooldownAura => enableCooldownAura;

        public EnemyUtilityPresentationKind UtilityKind => utilityKind;

        public GameplayVfxCueId CooldownCueId => GameplayVfxCueId.From(cooldownCue);

        public string AttachPointId => string.IsNullOrWhiteSpace(attachPointId)
            ? null
            : attachPointId.Trim();

        public bool TryGetUtilityCooldownAura(out GameplayVfxCueId cueId, out string normalizedAttachPointId)
        {
            cueId = default;
            normalizedAttachPointId = AttachPointId;
            if (!enableCooldownAura ||
                utilityKind == EnemyUtilityPresentationKind.None ||
                cooldownCue == 0 ||
                string.IsNullOrWhiteSpace(normalizedAttachPointId))
            {
                return false;
            }

            cueId = CooldownCueId;
            return true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (attachPointId != null)
            {
                attachPointId = attachPointId.Trim();
            }
        }
#endif
    }
}
