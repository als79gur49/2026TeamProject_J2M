using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class InventoryDetailView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _badgeLabel;
        [SerializeField] private TMP_Text _descriptionLabel;
        [SerializeField] private TMP_Text _detailLabel;

        private InventoryDetailViewModel _viewModel;

        public string TitleText => _titleLabel != null ? _titleLabel.text : string.Empty;

        public void Bind(InventoryDetailViewModel viewModel)
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
            ValidateSerializedReference(_titleLabel, nameof(_titleLabel));
            ValidateSerializedReference(_badgeLabel, nameof(_badgeLabel));
            ValidateSerializedReference(_descriptionLabel, nameof(_descriptionLabel));
            ValidateSerializedReference(_detailLabel, nameof(_detailLabel));
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
            if (_viewModel == null)
            {
                return;
            }

            if (_titleLabel != null)
            {
                _titleLabel.text = _viewModel.TitleText;
            }

            if (_badgeLabel != null)
            {
                _badgeLabel.text = _viewModel.BadgeText;
            }

            if (_descriptionLabel != null)
            {
                _descriptionLabel.text = _viewModel.DescriptionText;
            }

            if (_detailLabel != null)
            {
                _detailLabel.text = _viewModel.DetailText;
            }
        }

#if UNITY_EDITOR
        private void ValidateSerializedReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                Debug.LogWarning($"{nameof(InventoryDetailView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif
    }
}
