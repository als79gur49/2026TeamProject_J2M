using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class GameplayBoardRoot : MonoBehaviour
    {
        private const string BoardSurfaceRootObjectName = "BoardSurfaceRoot";
        private const string EntityRootObjectName = "EntityRoot";
        private const string CameraTargetRootObjectName = "CameraTargetRoot";
        private const string CameraOrbitPivotObjectName = "CameraOrbitPivot";
        private const string CameraPoseRootObjectName = "CameraPoseRoot";

        [SerializeField] private Transform boardSurfaceRoot;
        [SerializeField] private Transform entityRoot;
        [SerializeField] private Transform cameraTargetRoot;
        [SerializeField] private Transform cameraOrbitPivot;
        [SerializeField] private Transform cameraPoseRoot;
        [SerializeField] private Vector3 presentationPivotLocalPoint;

        public Transform BoardSurfaceRoot => boardSurfaceRoot;

        public Transform EntityRoot => entityRoot;

        public Transform CameraTargetRoot => cameraTargetRoot;

        public Transform CameraOrbitPivot => cameraOrbitPivot;

        public Transform CameraPoseRoot => cameraPoseRoot;

        public Vector3 PresentationPivotLocalPoint => presentationPivotLocalPoint;

        public GameplayBoardSurfaceRenderer BoardSurfaceRenderer =>
            boardSurfaceRoot != null ? boardSurfaceRoot.GetComponent<GameplayBoardSurfaceRenderer>() : null;

        private void Awake()
        {
            EnsureHierarchy();
        }

        public void EnsureHierarchy()
        {
            boardSurfaceRoot = EnsureChild(boardSurfaceRoot, BoardSurfaceRootObjectName);
            entityRoot = EnsureChild(entityRoot, EntityRootObjectName);
            cameraTargetRoot = EnsureChild(cameraTargetRoot, CameraTargetRootObjectName);
            cameraOrbitPivot = EnsureChild(cameraTargetRoot, cameraOrbitPivot, CameraOrbitPivotObjectName);
            cameraPoseRoot = EnsureChild(cameraOrbitPivot, cameraPoseRoot, CameraPoseRootObjectName);
            ApplyPresentationRotation(Quaternion.identity, presentationPivotLocalPoint);
        }

        public void ApplyPresentationRotation(Quaternion presentedTopologyReferenceRotation, Vector3 pivotLocalPoint)
        {
            _ = presentedTopologyReferenceRotation;
            presentationPivotLocalPoint = pivotLocalPoint;
            transform.localPosition = Vector3.zero;

            // Camera-only topology presentation keeps physical board geometry fixed.
            // This method only refreshes the camera presentation anchor hierarchy.
            // The topology reference rotation is consumed by the camera rig, not this transform.
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            if (cameraTargetRoot != null)
            {
                cameraTargetRoot.localPosition = pivotLocalPoint;
                cameraTargetRoot.localRotation = Quaternion.identity;
                cameraTargetRoot.localScale = Vector3.one;
            }

            if (cameraOrbitPivot != null)
            {
                cameraOrbitPivot.localPosition = Vector3.zero;
                cameraOrbitPivot.localRotation = Quaternion.identity;
                cameraOrbitPivot.localScale = Vector3.one;
            }

            if (cameraPoseRoot != null)
            {
                cameraPoseRoot.localScale = Vector3.one;
            }
        }

        public GameplayBoardSurfaceRenderer EnsureBoardSurfaceRenderer()
        {
            EnsureHierarchy();

            var surfaceRenderer = boardSurfaceRoot.GetComponent<GameplayBoardSurfaceRenderer>();
            if (surfaceRenderer == null)
            {
                surfaceRenderer = boardSurfaceRoot.gameObject.AddComponent<GameplayBoardSurfaceRenderer>();
            }

            return surfaceRenderer;
        }

        private Transform EnsureChild(Transform existingChild, string childName)
        {
            return EnsureChild(transform, existingChild, childName);
        }

        private static Transform EnsureChild(Transform parent, Transform existingChild, string childName)
        {
            var child = existingChild != null ? existingChild : parent.Find(childName);
            if (child == null)
            {
                var childObject = new GameObject(childName);
                child = childObject.transform;
                child.SetParent(parent, worldPositionStays: false);
            }
            else if (child.parent != parent)
            {
                child.SetParent(parent, worldPositionStays: false);
            }

            child.name = childName;
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            return child;
        }
    }
}
