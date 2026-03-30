using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class GameplayBoardRoot : MonoBehaviour
    {
        private const string BoardSurfaceRootObjectName = "BoardSurfaceRoot";
        private const string EntityRootObjectName = "EntityRoot";
        private const string CameraTargetRootObjectName = "CameraTargetRoot";

        [SerializeField] private Transform boardSurfaceRoot;
        [SerializeField] private Transform entityRoot;
        [SerializeField] private Transform cameraTargetRoot;

        public Transform BoardSurfaceRoot => boardSurfaceRoot;

        public Transform EntityRoot => entityRoot;

        public Transform CameraTargetRoot => cameraTargetRoot;

        private void Awake()
        {
            EnsureHierarchy();
        }

        public void EnsureHierarchy()
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            boardSurfaceRoot = EnsureChild(boardSurfaceRoot, BoardSurfaceRootObjectName);
            entityRoot = EnsureChild(entityRoot, EntityRootObjectName);
            cameraTargetRoot = EnsureChild(cameraTargetRoot, CameraTargetRootObjectName);
        }

        private Transform EnsureChild(Transform existingChild, string childName)
        {
            var child = existingChild != null ? existingChild : transform.Find(childName);
            if (child == null)
            {
                var childObject = new GameObject(childName);
                child = childObject.transform;
                child.SetParent(transform, worldPositionStays: false);
            }
            else if (child.parent != transform)
            {
                child.SetParent(transform, worldPositionStays: false);
            }

            child.name = childName;
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            return child;
        }
    }
}
