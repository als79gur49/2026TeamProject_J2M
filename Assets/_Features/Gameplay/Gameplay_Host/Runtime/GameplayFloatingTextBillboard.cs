using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class GameplayFloatingTextBillboard : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float verticalOffset = 1f;

        public void Initialize(Transform targetTransform, float worldVerticalOffset, Camera camera = null)
        {
            target = targetTransform;
            targetCamera = camera;
            verticalOffset = Mathf.Max(0f, worldVerticalOffset);
            RefreshPose();
        }

        private void LateUpdate()
        {
            RefreshPose();
        }

        private void RefreshPose()
        {
            if (target == null)
            {
                return;
            }

            transform.position = target.position + (Vector3.up * verticalOffset);

            var viewCamera = ResolveCamera();
            if (viewCamera == null)
            {
                return;
            }

            var toCamera = transform.position - viewCamera.transform.position;
            if (toCamera.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(toCamera.normalized, viewCamera.transform.up);
        }

        private Camera ResolveCamera()
        {
            if (targetCamera != null)
            {
                return targetCamera;
            }

            targetCamera = Camera.main;
            return targetCamera;
        }
    }
}
