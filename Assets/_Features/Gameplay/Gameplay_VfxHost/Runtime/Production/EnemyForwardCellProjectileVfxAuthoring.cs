using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    [DisallowMultipleComponent]
    public sealed class EnemyForwardCellProjectileVfxAuthoring : MonoBehaviour
    {
        [SerializeField] private string forwardCellMuzzleAttachPointId = "ForwardCellMuzzle";
        [SerializeField] private string attackCooldownAttachPointId = "StatusAura";

        public string ForwardCellMuzzleAttachPointId => Normalize(forwardCellMuzzleAttachPointId);

        public string AttackCooldownAttachPointId => Normalize(attackCooldownAttachPointId);

#if UNITY_EDITOR
        private void OnValidate()
        {
            forwardCellMuzzleAttachPointId = Normalize(forwardCellMuzzleAttachPointId) ?? string.Empty;
            attackCooldownAttachPointId = Normalize(attackCooldownAttachPointId) ?? string.Empty;
        }
#endif

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
