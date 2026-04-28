using System.Text;
using TMPro;
using UnityEngine;

namespace Game.Feature.UI.HUD
{
    public sealed class PlayerStatusView : MonoBehaviour
    {
        private const char FilledHeart = '\u2665';
        private const char EmptyHeart = '\u2661';
        private const string FilledHeartColor = "#E94B68";
        private const string EmptyHeartColor = "#536071";

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
                _titleLabel.text = "Player";
            }

            if (_viewModel == null)
            {
                if (_chancesLabel != null)
                {
                    _chancesLabel.gameObject.SetActive(false);
                }

                return;
            }

            if (_facingLabel != null)
            {
                _facingLabel.text = $"Facing: {_viewModel.FacingText}";
            }

            if (_topologyLabel != null)
            {
                _topologyLabel.text = $"Topology: {_viewModel.TopologyText}";
            }

            if (_chancesLabel != null)
            {
                _chancesLabel.gameObject.SetActive(_viewModel.HasRemainingChances);
                _chancesLabel.text = _viewModel.HasRemainingChances
                    ? BuildChancesText(_viewModel.RemainingChances, _viewModel.MaxChances)
                    : string.Empty;
            }
        }

        private static string BuildChancesText(int remainingChances, int maxChances)
        {
            var builder = new StringBuilder("Chances: ");
            for (var i = 0; i < maxChances; i++)
            {
                if (i > 0)
                {
                    builder.Append(' ');
                }

                if (i < remainingChances)
                {
                    builder.Append("<color=")
                        .Append(FilledHeartColor)
                        .Append('>')
                        .Append(FilledHeart)
                        .Append("</color>");
                }
                else
                {
                    builder.Append("<color=")
                        .Append(EmptyHeartColor)
                        .Append('>')
                        .Append(EmptyHeart)
                        .Append("</color>");
                }
            }

            return builder.ToString();
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
