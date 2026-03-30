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
        [SerializeField] private Vector3 presentationPivotLocalPoint;

        public Transform BoardSurfaceRoot => boardSurfaceRoot;

        public Transform EntityRoot => entityRoot;

        public Transform CameraTargetRoot => cameraTargetRoot;

        public Vector3 PresentationPivotLocalPoint => presentationPivotLocalPoint;

        private void Awake()
        {
            EnsureHierarchy();
        }

        public void EnsureHierarchy()
        {
            boardSurfaceRoot = EnsureChild(boardSurfaceRoot, BoardSurfaceRootObjectName);
            entityRoot = EnsureChild(entityRoot, EntityRootObjectName);
            cameraTargetRoot = EnsureChild(cameraTargetRoot, CameraTargetRootObjectName);
            ApplyPresentationRotation(Quaternion.identity, presentationPivotLocalPoint);
        }

        public void ApplyPresentationRotation(Quaternion localRotation, Vector3 pivotLocalPoint)
        {
            presentationPivotLocalPoint = pivotLocalPoint;
            transform.localRotation = localRotation;
            transform.localPosition = pivotLocalPoint - (localRotation * pivotLocalPoint);
            transform.localScale = Vector3.one;

            if (cameraTargetRoot != null)
            {
                cameraTargetRoot.localPosition = pivotLocalPoint;
                cameraTargetRoot.localRotation = Quaternion.identity;
                cameraTargetRoot.localScale = Vector3.one;
            }
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
