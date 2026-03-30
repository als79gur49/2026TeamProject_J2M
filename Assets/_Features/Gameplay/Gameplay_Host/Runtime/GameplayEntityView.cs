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

        public void ApplyLocalPose(Vector3 localPosition, Quaternion localRotation)
        {
            transform.localPosition = localPosition;
            transform.localRotation = localRotation;
        }

        public void ApplyPose(Vector3 worldPosition, Quaternion worldRotation)
        {
            var parent = transform.parent;
            if (parent == null)
            {
                transform.SetPositionAndRotation(worldPosition, worldRotation);
                return;
            }

            ApplyLocalPose(
                parent.InverseTransformPoint(worldPosition),
                Quaternion.Inverse(parent.rotation) * worldRotation);
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
