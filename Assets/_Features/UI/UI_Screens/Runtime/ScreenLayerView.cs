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
    }
}
