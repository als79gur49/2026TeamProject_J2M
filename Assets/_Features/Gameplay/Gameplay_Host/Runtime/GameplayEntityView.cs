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

        public void ApplyPose(Vector3 worldPosition, Quaternion worldRotation)
        {
            transform.position = worldPosition;
            transform.rotation = worldRotation;
        }

        public void SetVisible(bool isVisible)
        {
            if (gameObject.activeSelf == isVisible)
            {
                return;
            }

            gameObject.SetActive(isVisible);
        }
    }
}
