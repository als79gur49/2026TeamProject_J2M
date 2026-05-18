using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Vfx;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    [DisallowMultipleComponent]
    public sealed class EnemyWeaponAuraVfxAuthoring : MonoBehaviour
    {
        [SerializeField] private bool enableWeaponWindupAura = true;
        [SerializeField] private EnemyActionKind actionKind = EnemyActionKind.Melee;
        [SerializeField] private EnemyVfxCue windupCue = EnemyVfxCue.WeaponWindupAura;
        [SerializeField] private string attachPointId = "WeaponAura";

        public bool EnableWeaponWindupAura => enableWeaponWindupAura;

        public EnemyActionKind ActionKind => actionKind;

        public GameplayVfxCueId WindupCueId => GameplayVfxCueId.From(windupCue);

        public string AttachPointId => string.IsNullOrWhiteSpace(attachPointId)
            ? null
            : attachPointId.Trim();

        public bool TryGetWeaponWindupAura(out GameplayVfxCueId cueId, out string normalizedAttachPointId)
        {
            cueId = default;
            normalizedAttachPointId = AttachPointId;
            if (!enableWeaponWindupAura ||
                windupCue == 0 ||
                string.IsNullOrWhiteSpace(normalizedAttachPointId))
            {
                return false;
            }

            cueId = WindupCueId;
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
