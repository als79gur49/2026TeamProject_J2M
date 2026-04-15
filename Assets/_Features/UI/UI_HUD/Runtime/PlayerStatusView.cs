using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public sealed class PlayerStatusView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _hpLabel;
        [SerializeField] private Text _facingLabel;
        [SerializeField] private Text _actionLabel;
        [SerializeField] private Text _topologyLabel;
        [SerializeField] private Text _statusLabel;
        [SerializeField] private Text _damageLabel;

        private PlayerStatusViewModel _viewModel;

        public PlayerStatusViewModel ViewModel => _viewModel;

        public void Configure(
            GameObject root,
            Text titleLabel,
            Text hpLabel,
            Text facingLabel,
            Text actionLabel,
            Text topologyLabel,
            Text statusLabel,
            Text damageLabel)
        {
            _root = root;
            _titleLabel = titleLabel;
            _hpLabel = hpLabel;
            _facingLabel = facingLabel;
            _actionLabel = actionLabel;
            _topologyLabel = topologyLabel;
            _statusLabel = statusLabel;
            _damageLabel = damageLabel;

            RefreshView();
        }

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
                _hpLabel.text = $"HP: {_viewModel.CurrentHp}";
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
    }
}
