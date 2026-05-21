using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class GameplayBoardRoot : MonoBehaviour
    {
        private const string BoardSkinRootObjectName = "BoardSkinRoot";
        private const string BoardSurfaceRootObjectName = "BoardSurfaceRoot";
        private const string EntityRootObjectName = "EntityRoot";
        private const string CameraTargetRootObjectName = "CameraTargetRoot";
        private const string CameraOrbitPivotObjectName = "CameraOrbitPivot";
        private const string CameraPoseRootObjectName = "CameraPoseRoot";
        private const string CameraEffectsRootObjectName = "CameraEffectsRoot";

        [SerializeField] private Transform boardSkinRoot;
        [SerializeField] private Transform boardSurfaceRoot;
        [SerializeField] private Transform entityRoot;
        [SerializeField] private Transform cameraTargetRoot;
        [SerializeField] private Transform cameraOrbitPivot;
        [SerializeField] private Transform cameraPoseRoot;
        [SerializeField] private Transform cameraEffectsRoot;
        [SerializeField] private Vector3 presentationPivotLocalPoint;

        private GameObject _boardSkinInstance;

        public Transform BoardSkinRoot => boardSkinRoot;

        public Transform BoardSurfaceRoot => boardSurfaceRoot;

        public Transform EntityRoot => entityRoot;

        public Transform CameraTargetRoot => cameraTargetRoot;

        public Transform CameraOrbitPivot => cameraOrbitPivot;

        public Transform CameraPoseRoot => cameraPoseRoot;

        public Transform CameraEffectsRoot => cameraEffectsRoot;

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
            cameraEffectsRoot = EnsureChild(cameraPoseRoot, cameraEffectsRoot, CameraEffectsRootObjectName);
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

            if (cameraEffectsRoot != null)
            {
                cameraEffectsRoot.localScale = Vector3.one;
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

        public void AttachBoardPresentationProfile(Game.Feature.Stages.BoardPresentationProfile profile)
        {
            if (profile == null || profile.BoardRootPrefab == null)
            {
                return;
            }

            boardSkinRoot = EnsureChild(boardSkinRoot, BoardSkinRootObjectName);
            DestroyBoardSkinInstance();

            _boardSkinInstance = Instantiate(profile.BoardRootPrefab, boardSkinRoot, worldPositionStays: false);
            _boardSkinInstance.name = profile.BoardRootPrefab.name;
            _boardSkinInstance.transform.localPosition = Vector3.zero;
            _boardSkinInstance.transform.localRotation = Quaternion.identity;
            _boardSkinInstance.transform.localScale = Vector3.one;
        }

        private Transform EnsureChild(Transform existingChild, string childName)
        {
            return EnsureChild(transform, existingChild, childName);
        }

        private void DestroyBoardSkinInstance()
        {
            if (_boardSkinInstance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_boardSkinInstance);
            }
            else
            {
                DestroyImmediate(_boardSkinInstance);
            }

            _boardSkinInstance = null;
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
