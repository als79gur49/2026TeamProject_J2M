using TMPro;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public sealed class NotificationView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _firstLabel;
        [SerializeField] private TMP_Text _secondLabel;
        [SerializeField] private TMP_Text _thirdLabel;

        private NotificationViewModel _viewModel;

        public NotificationViewModel ViewModel => _viewModel;

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

        public void ValidateAuthoredStructureOrThrow()
        {
            RequireReference(_root, nameof(_root));
            RequireReference(_titleLabel, nameof(_titleLabel));
            RequireReference(_firstLabel, nameof(_firstLabel));
            RequireReference(_secondLabel, nameof(_secondLabel));
            RequireReference(_thirdLabel, nameof(_thirdLabel));
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_root, nameof(_root));
            ValidateSerializedReference(_titleLabel, nameof(_titleLabel));
            ValidateSerializedReference(_firstLabel, nameof(_firstLabel));
            ValidateSerializedReference(_secondLabel, nameof(_secondLabel));
            ValidateSerializedReference(_thirdLabel, nameof(_thirdLabel));
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
            ValidateAuthoredStructureOrThrow();
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

        private void ApplyLabel(TMP_Text label, int index)
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

#if UNITY_EDITOR
        private void ValidateSerializedReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                Debug.LogWarning($"{nameof(NotificationView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif

        private static void RequireReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                throw new InvalidOperationException($"{nameof(NotificationView)} is missing authored reference '{fieldName}'.");
            }
        }
    }
}
