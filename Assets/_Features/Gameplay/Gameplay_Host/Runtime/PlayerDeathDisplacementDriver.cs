using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class PlayerDeathDisplacementDriver : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;

        private bool _cachedBasePose;
        private Vector3 _baseLocalPosition;

        public void Initialize(Transform targetVisualRoot)
        {
            if (targetVisualRoot == null)
            {
                return;
            }

            if (visualRoot == targetVisualRoot)
            {
                return;
            }

            visualRoot = targetVisualRoot;
            _cachedBasePose = false;
        }

        public void ApplyDisplacement(Vector3 localPositionOffset)
        {
            if (visualRoot == null)
            {
                return;
            }

            CacheBasePose();
            visualRoot.localPosition = _baseLocalPosition + localPositionOffset;
        }

        public void ResetDisplacement()
        {
            if (visualRoot == null)
            {
                return;
            }

            CacheBasePose();
            visualRoot.localPosition = _baseLocalPosition;
        }

        private void OnDisable()
        {
            ResetDisplacement();
        }

        private void Reset()
        {
            if (visualRoot == null &&
                TryGetComponent<GameplayEntityView>(out var view) &&
                view != null)
            {
                visualRoot = view.ModelRoot;
            }
        }

        private void CacheBasePose()
        {
            if (_cachedBasePose || visualRoot == null)
            {
                return;
            }

            _baseLocalPosition = visualRoot.localPosition;
            _cachedBasePose = true;
        }
    }
}
