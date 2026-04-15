using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public sealed class NotificationView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _firstLabel;
        [SerializeField] private Text _secondLabel;
        [SerializeField] private Text _thirdLabel;

        private NotificationViewModel _viewModel;

        public NotificationViewModel ViewModel => _viewModel;

        public void Configure(
            GameObject root,
            Text titleLabel,
            Text firstLabel,
            Text secondLabel,
            Text thirdLabel)
        {
            _root = root;
            _titleLabel = titleLabel;
            _firstLabel = firstLabel;
            _secondLabel = secondLabel;
            _thirdLabel = thirdLabel;

            RefreshView();
        }

        public void Bind(NotificationViewModel viewModel)
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
                _titleLabel.text = "Notifications";
            }

            ApplyLabel(_firstLabel, 0);
            ApplyLabel(_secondLabel, 1);
            ApplyLabel(_thirdLabel, 2);
        }

        private void ApplyLabel(Text label, int index)
        {
            if (label == null)
            {
                return;
            }

            var items = _viewModel?.Items;
            label.text = items != null && index < items.Count
                ? items[index].MessageText
                : string.Empty;
        }
    }
}
