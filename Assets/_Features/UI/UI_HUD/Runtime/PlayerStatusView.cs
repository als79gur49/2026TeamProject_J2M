using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public sealed class PlayerStatusView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _hpLabel;
        [SerializeField] private Slider _hpSlider;
        [SerializeField] private Text _facingLabel;
        [SerializeField] private Text _actionLabel;
        [SerializeField] private Text _topologyLabel;
        [SerializeField] private Text _statusLabel;
        [SerializeField] private Text _damageLabel;

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
            ValidateSerializedReference(_hpLabel, nameof(_hpLabel));
            ValidateSerializedReference(_hpSlider, nameof(_hpSlider));
            ValidateSerializedReference(_facingLabel, nameof(_facingLabel));
            ValidateSerializedReference(_actionLabel, nameof(_actionLabel));
            ValidateSerializedReference(_topologyLabel, nameof(_topologyLabel));
            ValidateSerializedReference(_statusLabel, nameof(_statusLabel));
            ValidateSerializedReference(_damageLabel, nameof(_damageLabel));
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
                _titleLabel.text = "Player";
            }

            if (_viewModel == null)
            {
                return;
            }

            if (_hpLabel != null)
            {
                _hpLabel.text = _viewModel.MaxHp > 0
                    ? $"HP: {_viewModel.CurrentHp}/{_viewModel.MaxHp}"
                    : $"HP: {_viewModel.CurrentHp}";
            }

            if (_hpSlider != null)
            {
                _hpSlider.value = _viewModel.HpNormalized;
            }

            if (_facingLabel != null)
            {
                _facingLabel.text = $"Facing: {_viewModel.FacingText}";
            }

            if (_actionLabel != null)
            {
                _actionLabel.text = $"Action: {_viewModel.ActionText}";
            }

            if (_topologyLabel != null)
            {
                _topologyLabel.text = $"Topology: {_viewModel.TopologyText}";
            }

            if (_statusLabel != null)
            {
                _statusLabel.text = $"Status: {_viewModel.StatusText}";
            }

            if (_damageLabel != null)
            {
                _damageLabel.text = $"Damage: {_viewModel.DamageText}";
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
