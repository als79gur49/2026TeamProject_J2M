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

            var hasStage = SetOptionalLabel(_stageLabel, _viewModel?.StageText);
            var hasChances = SetOptionalLabel(_chancesLabel, _viewModel?.ChancesText);
            var hasDeaths = SetOptionalLabel(_deathsLabel, _viewModel?.DeathsText);
            var hasLastPlayed = SetOptionalLabel(_lastPlayedLabel, _viewModel?.LastPlayedText);
            SetRowActive(_stageLabel ?? _chancesLabel, hasStage || hasChances);
            SetRowActive(_deathsLabel ?? _lastPlayedLabel, hasDeaths || hasLastPlayed);

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
                var showRestart = _viewModel != null &&
                                  _viewModel.ShowRestart &&
                                  _viewModel.PrimaryIntentKind != SaveSlotIntentKind.Restart;
                _restartButton.gameObject.SetActive(showRestart);
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

            var headerRow = CreateHorizontalRow("HeaderRow", transform, 32f, 12f);
            _titleLabel ??= CreateLabel("Title", 22, FontStyles.Bold, headerRow, TextAlignmentOptions.Left);
            AddFlexibleWidth(_titleLabel.gameObject, 1f);
            _statusLabel ??= CreateLabel("Status", 16, FontStyles.Bold, headerRow, TextAlignmentOptions.Right);
            AddMinWidth(_statusLabel.gameObject, 120f);

            var detailRow = CreateHorizontalRow("DetailRow", transform, 24f, 12f);
            _stageLabel ??= CreateLabel("Stage", 16, FontStyles.Normal, detailRow, TextAlignmentOptions.Left);
            AddFlexibleWidth(_stageLabel.gameObject, 1f);
            _chancesLabel ??= CreateLabel("Chances", 15, FontStyles.Normal, detailRow, TextAlignmentOptions.Right);
            AddMinWidth(_chancesLabel.gameObject, 120f);

            var metaRow = CreateHorizontalRow("MetaRow", transform, 22f, 12f);
            _deathsLabel ??= CreateLabel("Deaths", 14, FontStyles.Normal, metaRow, TextAlignmentOptions.Left);
            AddMinWidth(_deathsLabel.gameObject, 96f);
            _lastPlayedLabel ??= CreateLabel("LastPlayed", 14, FontStyles.Normal, metaRow, TextAlignmentOptions.Right);
            AddFlexibleWidth(_lastPlayedLabel.gameObject, 1f);

            var actionRow = CreateHorizontalRow("ActionRow", transform, 38f, 8f);
            if (_primaryButton == null)
            {
                _primaryButton = CreateButton("PrimaryButton", actionRow, out _primaryButtonLabel);
            }

            if (_restartButton == null)
            {
                _restartButton = CreateButton("RestartButton", actionRow, out var restartLabel);
                restartLabel.text = "Restart";
            }

            if (_deleteButton == null)
            {
                _deleteButton = CreateButton("DeleteButton", actionRow, out var deleteLabel);
                deleteLabel.text = "Delete";
            }
        }

        private static RectTransform CreateHorizontalRow(string objectName, Transform parent, float minHeight, float spacing)
        {
            var rowObject = new GameObject(objectName, typeof(RectTransform));
            rowObject.transform.SetParent(parent, false);
            var layout = rowObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var layoutElement = rowObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = minHeight;
            layoutElement.preferredHeight = minHeight;
            return (RectTransform)rowObject.transform;
        }

        private TMP_Text CreateLabel(
            string objectName,
            int fontSize,
            FontStyles fontStyle,
            Transform parent,
            TextAlignmentOptions alignment)
        {
            var labelObject = new GameObject(objectName, typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.alignment = alignment;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.color = Color.white;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        private Button CreateButton(string objectName, Transform parent, out TMP_Text label)
        {
            var buttonObject = new GameObject(objectName, typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.20f, 0.25f, 0.34f, 1f);
            var button = buttonObject.AddComponent<Button>();
            var layoutElement = buttonObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = 36f;
            layoutElement.preferredHeight = 36f;
            layoutElement.minWidth = 112f;
            layoutElement.flexibleWidth = 1f;

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

        private static bool SetOptionalLabel(TMP_Text label, string text)
        {
            if (label == null)
            {
                return false;
            }

            text ??= string.Empty;
            var hasText = !string.IsNullOrWhiteSpace(text);
            label.text = text;
            label.gameObject.SetActive(hasText);
            return hasText;
        }

        private static void SetRowActive(TMP_Text rowChild, bool active)
        {
            if (rowChild == null || rowChild.transform.parent == null)
            {
                return;
            }

            var row = rowChild.transform.parent;
            if (row.GetComponent<SaveSlotCardView>() == null)
            {
                row.gameObject.SetActive(active);
            }
        }

        private static void AddMinWidth(GameObject target, float minWidth)
        {
            var layoutElement = target.AddComponent<LayoutElement>();
            layoutElement.minWidth = minWidth;
        }

        private static void AddFlexibleWidth(GameObject target, float flexibleWidth)
        {
            var layoutElement = target.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = flexibleWidth;
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
