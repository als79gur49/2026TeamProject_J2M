using Game.Feature.UI.ViewShared;
using UnityEngine;

namespace Game.Feature.UI.Screens
{
    [DisallowMultipleComponent]
    public sealed class ScreenLayerView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private RectTransform _contentRoot;

        public RectTransform ContentRoot => _contentRoot;

        public void Configure(GameObject root, RectTransform contentRoot)
        {
            _root = root;
            _contentRoot = contentRoot;
        }

        public T FindScreenView<T>() where T : Component
        {
            if (_contentRoot == null)
            {
                return null;
            }

            return _contentRoot.GetComponentInChildren<T>(true);
        }

        public bool TryFindNavigationTarget(out IUiNavigationTarget target)
        {
            target = null;
            if (_contentRoot == null)
            {
                return false;
            }

            var behaviours = _contentRoot.GetComponentsInChildren<MonoBehaviour>(true);
            for (var i = behaviours.Length - 1; i >= 0; i--)
            {
                if (behaviours[i] is IUiNavigationTarget candidate)
                {
                    target = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
