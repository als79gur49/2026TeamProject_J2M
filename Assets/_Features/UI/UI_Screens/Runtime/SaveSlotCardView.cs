using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class SaveSlotCardView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private TMP_Text _stageLabel;
        [SerializeField] private TMP_Text _chancesLabel;
        [SerializeField] private TMP_Text _deathsLabel;
        [SerializeField] private TMP_Text _lastPlayedLabel;
        [SerializeField] private Button _primaryButton;
        [SerializeField] private TMP_Text _primaryButtonLabel;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _deleteButton;

        private SaveSlotCardViewModel _viewModel;

        public event Action<SaveSlotIntent> IntentRequested;

        public void Bind(SaveSlotCardViewModel viewModel)
        {
            _viewModel = viewModel;
            EnsureDefaultHierarchy();
            Refresh();
        }

        private void OnEnable()
        {
            EnsureDefaultHierarchy();
            Rebind(_primaryButton, HandlePrimaryClicked);
            Rebind(_restartButton, HandleRestartClicked);
            Rebind(_deleteButton, HandleDeleteClicked);
        }

        private void OnDisable()
        {
            Unbind(_primaryButton, HandlePrimaryClicked);
            Unbind(_restartButton, HandleRestartClicked);
            Unbind(_deleteButton, HandleDeleteClicked);
        }

        private void HandlePrimaryClicked()
        {
            if (_viewModel == null || _viewModel.PrimaryIntentKind == SaveSlotIntentKind.None)
            {
                return;
            }

            IntentRequested?.Invoke(new SaveSlotIntent(_viewModel.SlotNumber, _viewModel.PrimaryIntentKind));
        }

        private void HandleRestartClicked()
        {
            if (_viewModel == null)
            {
                return;
            }

            IntentRequested?.Invoke(new SaveSlotIntent(_viewModel.SlotNumber, SaveSlotIntentKind.Restart));
        }

        private void HandleDeleteClicked()
        {
            if (_viewModel == null)
            {
                return;
            }

            IntentRequested?.Invoke(new SaveSlotIntent(_viewModel.SlotNumber, SaveSlotIntentKind.Delete));
        }

        private void Refresh()
        {
            if (_titleLabel != null)
            {
                _titleLabel.text = _viewModel?.TitleText ?? string.Empty;
            }

            if (_statusLabel != null)
            {
                _statusLabel.text = _viewModel?.StatusText ?? string.Empty;
            }

            if (_stageLabel != null)
            {
                _stageLabel.text = _viewModel?.StageText ?? string.Empty;
            }

            if (_chancesLabel != null)
            {
                _chancesLabel.text = _viewModel?.ChancesText ?? string.Empty;
            }

            if (_deathsLabel != null)
            {
                _deathsLabel.text = _viewModel?.DeathsText ?? string.Empty;
            }

            if (_lastPlayedLabel != null)
            {
                _lastPlayedLabel.text = _viewModel?.LastPlayedText ?? string.Empty;
            }

            if (_primaryButtonLabel != null)
            {
                _primaryButtonLabel.text = _viewModel?.PrimaryActionText ?? string.Empty;
            }

            if (_primaryButton != null)
            {
                var hasPrimaryIntent = _viewModel != null && _viewModel.PrimaryIntentKind != SaveSlotIntentKind.None;
                _primaryButton.gameObject.SetActive(hasPrimaryIntent);
                _primaryButton.interactable = hasPrimaryIntent;
            }

            if (_restartButton != null)
            {
                _restartButton.gameObject.SetActive(_viewModel != null && _viewModel.ShowRestart);
            }

            if (_deleteButton != null)
            {
                _deleteButton.gameObject.SetActive(_viewModel != null && _viewModel.ShowDelete);
            }
        }

        private void EnsureDefaultHierarchy()
        {
            if (_titleLabel != null &&
                _statusLabel != null &&
                _primaryButton != null &&
                _restartButton != null &&
                _deleteButton != null)
            {
                return;
            }

            var rectTransform = transform as RectTransform;
            if (rectTransform != null)
            {
                rectTransform.sizeDelta = rectTransform.sizeDelta == Vector2.zero
                    ? new Vector2(320f, 220f)
                    : rectTransform.sizeDelta;
            }

            var background = GetComponent<Image>();
            if (background == null)
            {
                background = gameObject.AddComponent<Image>();
                background.color = new Color(0.12f, 0.14f, 0.18f, 0.92f);
            }

            var layout = GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = gameObject.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(16, 16, 14, 14);
                layout.spacing = 8f;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
            }

            _titleLabel ??= CreateLabel("Title", 22, FontStyles.Bold);
            _statusLabel ??= CreateLabel("Status", 16, FontStyles.Bold);
            _stageLabel ??= CreateLabel("Stage", 15, FontStyles.Normal);
            _chancesLabel ??= CreateLabel("Chances", 14, FontStyles.Normal);
            _deathsLabel ??= CreateLabel("Deaths", 14, FontStyles.Normal);
            _lastPlayedLabel ??= CreateLabel("LastPlayed", 12, FontStyles.Normal);
            if (_primaryButton == null)
            {
                _primaryButton = CreateButton("PrimaryButton", out _primaryButtonLabel);
            }

            if (_restartButton == null)
            {
                _restartButton = CreateButton("RestartButton", out var restartLabel);
                restartLabel.text = "Restart";
            }

            if (_deleteButton == null)
            {
                _deleteButton = CreateButton("DeleteButton", out var deleteLabel);
                deleteLabel.text = "Delete";
            }
        }

        private TMP_Text CreateLabel(string objectName, int fontSize, FontStyles fontStyle)
        {
            var labelObject = new GameObject(objectName, typeof(RectTransform));
            labelObject.transform.SetParent(transform, false);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.color = Color.white;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        private Button CreateButton(string objectName, out TMP_Text label)
        {
            var buttonObject = new GameObject(objectName, typeof(RectTransform));
            buttonObject.transform.SetParent(transform, false);
            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.20f, 0.25f, 0.34f, 1f);
            var button = buttonObject.AddComponent<Button>();
            var layoutElement = buttonObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = 36f;

            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label = labelObject.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 15;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.raycastTarget = false;
            return button;
        }

        private static void Rebind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static void Unbind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
        }
    }
}
