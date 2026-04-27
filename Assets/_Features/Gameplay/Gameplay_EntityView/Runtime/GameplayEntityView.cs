using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplayEntityView : MonoBehaviour
    {
        private const string ModelRootObjectName = "ModelRoot";

        [SerializeField] private int entityId;
        [SerializeField] private Transform modelRoot;
        private Vector3 baseModelRootLocalScale = Vector3.one;
        private bool hasBaseModelRootLocalScale;

        public int EntityId => entityId;

        public Transform ModelRoot => modelRoot != null ? modelRoot : EnsureModelRoot();

        private void Awake()
        {
            EnsureModelRoot();
            CaptureModelRootBaseScale();
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
            CaptureModelRootBaseScale();
        }

        public void ApplyLocalPose(Vector3 localPosition, Quaternion localRotation)
        {
            transform.localPosition = localPosition;
            transform.localRotation = localRotation;
        }

        public void ApplyModelRootVisualScale(Vector3 scaleMultiplier)
        {
            var targetModelRoot = EnsureModelRoot();
            if (!hasBaseModelRootLocalScale)
            {
                CaptureModelRootBaseScale();
            }

            targetModelRoot.localScale = Vector3.Scale(
                baseModelRootLocalScale,
                SanitizeScaleMultiplier(scaleMultiplier));
        }

        public void ResetModelRootVisualScale()
        {
            var targetModelRoot = EnsureModelRoot();
            if (!hasBaseModelRootLocalScale)
            {
                CaptureModelRootBaseScale();
            }

            targetModelRoot.localScale = baseModelRootLocalScale;
        }

        public void SetVisible(bool isVisible)
        {
            if (gameObject.activeSelf == isVisible)
            {
                return;
            }

            gameObject.SetActive(isVisible);
        }

        private void CaptureModelRootBaseScale()
        {
            var targetModelRoot = EnsureModelRoot();
            baseModelRootLocalScale = targetModelRoot.localScale;
            hasBaseModelRootLocalScale = true;
        }

        private static Vector3 SanitizeScaleMultiplier(Vector3 scaleMultiplier)
        {
            return new Vector3(
                Mathf.Max(0.0001f, scaleMultiplier.x),
                Mathf.Max(0.0001f, scaleMultiplier.y),
                Mathf.Max(0.0001f, scaleMultiplier.z));
        }
    }
}
