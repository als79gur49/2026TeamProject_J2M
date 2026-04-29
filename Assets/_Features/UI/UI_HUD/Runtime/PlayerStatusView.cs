using TMPro;
using UnityEngine;

namespace Game.Feature.UI.HUD
{
    public sealed class PlayerStatusView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _facingLabel;
        [SerializeField] private TMP_Text _topologyLabel;
        [SerializeField] private TMP_Text _chancesLabel;

        private PlayerStatusViewModel _viewModel;

        public PlayerStatusViewModel ViewModel => _viewModel;

        public void Bind(PlayerStatusViewModel viewModel)
        {
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }

            _viewModel = viewModel;
            if (_viewModel != null)
            {
                _viewModel.Changed += HandleViewModelChanged;
            }

            RefreshView();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_root, nameof(_root));
            ValidateSerializedReference(_titleLabel, nameof(_titleLabel));
            ValidateSerializedReference(_facingLabel, nameof(_facingLabel));
            ValidateSerializedReference(_topologyLabel, nameof(_topologyLabel));
            ValidateSerializedReference(_chancesLabel, nameof(_chancesLabel));
        }
#endif

        private void OnDestroy()
        {
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }
        }

        private void HandleViewModelChanged()
        {
            RefreshView();
        }

        private void RefreshView()
        {
            if (_root != null)
            {
                _root.SetActive(true);
            }

            if (_titleLabel != null)
            {
                _titleLabel.gameObject.SetActive(false);
                _titleLabel.text = string.Empty;
            }

            if (_viewModel == null)
            {
                if (_facingLabel != null)
                {
                    _facingLabel.gameObject.SetActive(false);
                    _facingLabel.text = string.Empty;
                }

                if (_topologyLabel != null)
                {
                    _topologyLabel.gameObject.SetActive(false);
                    _topologyLabel.text = string.Empty;
                }

                if (_chancesLabel != null)
                {
                    _chancesLabel.gameObject.SetActive(false);
                    _chancesLabel.text = string.Empty;
                }

                return;
            }

            if (_facingLabel != null)
            {
                _facingLabel.gameObject.SetActive(false);
                _facingLabel.text = string.Empty;
            }

            if (_topologyLabel != null)
            {
                _topologyLabel.gameObject.SetActive(false);
                _topologyLabel.text = string.Empty;
            }

            if (_chancesLabel != null)
            {
                _chancesLabel.gameObject.SetActive(false);
                _chancesLabel.text = string.Empty;
            }
        }

#if UNITY_EDITOR
        private void ValidateSerializedReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                Debug.LogWarning($"{nameof(PlayerStatusView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif
    }
}
