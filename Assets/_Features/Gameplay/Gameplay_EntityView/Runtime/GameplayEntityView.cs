using System;
using System.Collections.Generic;
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
        private Dictionary<string, Transform> vfxAttachPointById;
        private bool vfxAttachPointCacheBuilt;

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
                InvalidateVfxAttachPointCache();
            }

            modelRoot.name = ModelRootObjectName;
            return modelRoot;
        }

        public bool TryGetVfxAttachPoint(string id, out Transform point)
        {
            point = null;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            EnsureVfxAttachPointCache();
            return vfxAttachPointById != null &&
                   vfxAttachPointById.TryGetValue(id.Trim(), out point) &&
                   point != null;
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

        private void EnsureVfxAttachPointCache()
        {
            if (vfxAttachPointCacheBuilt)
            {
                return;
            }

            vfxAttachPointCacheBuilt = true;
            vfxAttachPointById = new Dictionary<string, Transform>(StringComparer.Ordinal);
            var root = modelRoot != null ? modelRoot : transform;
            if (root == null)
            {
                return;
            }

            var points = root.GetComponentsInChildren<GameplayVfxAttachPoint>(true);
            for (var i = 0; i < points.Length; i++)
            {
                var attachPoint = points[i];
                if (attachPoint == null ||
                    string.IsNullOrWhiteSpace(attachPoint.Id))
                {
                    continue;
                }

                var key = attachPoint.Id.Trim();
                if (vfxAttachPointById.ContainsKey(key))
                {
                    UnityEngine.Debug.LogWarning(
                        $"Duplicate VFX attach point id '{key}' on entity view '{name}'. The first attach point will be used.",
                        this);
                    continue;
                }

                vfxAttachPointById.Add(key, attachPoint.transform);
            }
        }

        private void InvalidateVfxAttachPointCache()
        {
            vfxAttachPointCacheBuilt = false;
            vfxAttachPointById?.Clear();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            InvalidateVfxAttachPointCache();
            ValidateVfxAttachPointDuplicates();
        }

        private void ValidateVfxAttachPointDuplicates()
        {
            var root = modelRoot != null ? modelRoot : transform;
            if (root == null)
            {
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var points = root.GetComponentsInChildren<GameplayVfxAttachPoint>(true);
            for (var i = 0; i < points.Length; i++)
            {
                var attachPoint = points[i];
                if (attachPoint == null ||
                    string.IsNullOrWhiteSpace(attachPoint.Id))
                {
                    continue;
                }

                var key = attachPoint.Id.Trim();
                if (!seen.Add(key))
                {
                    UnityEngine.Debug.LogWarning(
                        $"Duplicate VFX attach point id '{key}' on entity view '{name}'.",
                        this);
                }
            }
        }
#endif

        private static Vector3 SanitizeScaleMultiplier(Vector3 scaleMultiplier)
        {
            return new Vector3(
                Mathf.Max(0.0001f, scaleMultiplier.x),
                Mathf.Max(0.0001f, scaleMultiplier.y),
                Mathf.Max(0.0001f, scaleMultiplier.z));
        }
    }
}
