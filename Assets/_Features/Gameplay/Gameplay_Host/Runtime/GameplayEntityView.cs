using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplayEntityView : MonoBehaviour
    {
        private const string ModelRootObjectName = "ModelRoot";

        [SerializeField] private int entityId;
        [SerializeField] private Transform modelRoot;

        public int EntityId => entityId;

        public Transform ModelRoot => modelRoot != null ? modelRoot : EnsureModelRoot();

        private void Awake()
        {
            EnsureModelRoot();
        }

        public void Initialize(int newEntityId)
        {
            entityId = newEntityId;
        }

        public Transform EnsureModelRoot()
        {
            if (modelRoot == null)
            {
                var existingChild = transform.Find(ModelRootObjectName);
                if (existingChild == null)
                {
                    var modelRootObject = new GameObject(ModelRootObjectName);
                    existingChild = modelRootObject.transform;
                    existingChild.SetParent(transform, worldPositionStays: false);
                }
                else if (existingChild.parent != transform)
                {
                    existingChild.SetParent(transform, worldPositionStays: false);
                }

                modelRoot = existingChild;
            }

            modelRoot.name = ModelRootObjectName;
            return modelRoot;
        }

        public void ConfigureModelRoot(Vector3 localPosition, Quaternion localRotation)
        {
            var targetModelRoot = EnsureModelRoot();
            targetModelRoot.localPosition = localPosition;
            targetModelRoot.localRotation = localRotation;
            targetModelRoot.localScale = Vector3.one;
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
