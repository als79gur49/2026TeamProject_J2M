using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplayEntityView : MonoBehaviour
    {
        [SerializeField] private int entityId;

        public int EntityId => entityId;

        public void Initialize(int newEntityId)
        {
            entityId = newEntityId;
        }
    }
}
