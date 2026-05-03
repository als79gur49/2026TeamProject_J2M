using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class LevelFailedScreenView : MonoBehaviour, IScreenView
    {
        private static readonly Vector2 PreferredPanelSize = new Vector2(460f, 230f);
        private static readonly Vector2 MinimumPanelSize = new Vector2(360f, 200f);
        private static readonly Vector2 MaximumPanelSize = new Vector2(560f, 300f);

        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _detailLabel;
        [SerializeField] private Button _restartLevelButton;
        [SerializeField] private TMP_Text _restartLevelButtonLabel;
        [SerializeField] private Button _mainButton;
        [SerializeField] private TMP_Text _mainButtonLabel;

        private LevelFailedScreenViewModel _viewModel;
        private bool _isVisible;

        public event Action RestartLevelRequested;

        public event Action MainRequested;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                RefreshView();
            }
        }

        public void Bind(LevelFailedScreenViewModel viewModel)
        {
            EnsureRuntimeHierarchy();
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

        public void SetIsCurrent(bool isCurrent)
        {
            IsVisible = isCurrent;
        }

        public void ClickRestartLevel()
        {
            if (!IsVisible)
            {
                return;
            }

            RestartLevelRequested?.Invoke();
        }

        public void ClickMain()
        {
            if (!IsVisible)
            {
                return;
            }

            MainRequested?.Invoke();
        }

        private void OnEnable()
        {
            EnsureRuntimeHierarchy();
            RebindButton(_restartLevelButton, ClickRestartLevel);
            RebindButton(_mainButton, ClickMain);
            RefreshView();
        }

        private void OnDisable()
        {
            UnbindButton(_restartLevelButton, ClickRestartLevel);
            UnbindButton(_mainButton, ClickMain);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_root, nameof(_root));
            ValidateSerializedReference(_titleLabel, nameof(_titleLabel));
            ValidateSerializedReference(_detailLabel, nameof(_detailLabel));
            ValidateSerializedReference(_restartLevelButton, nameof(_restartLevelButton));
            ValidateSerializedReference(_restartLevelButtonLabel, nameof(_restartLevelButtonLabel));
            ValidateSerializedReference(_mainButton, nameof(_mainButton));
            ValidateSerializedReference(_mainButtonLabel, nameof(_mainButtonLabel));
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
            EnsureRuntimeHierarchy();
            if (_root != null)
            {
                _root.SetActive(IsVisible);
            }

            if (_viewModel == null)
            {
                return;
            }

            if (_titleLabel != null)
            {
                _titleLabel.text = _viewModel.TitleText;
            }

            if (_detailLabel != null)
            {
                _detailLabel.text = _viewModel.DetailText;
            }

            if (_restartLevelButtonLabel != null)
            {
                _restartLevelButtonLabel.text = _viewModel.RestartLevelLabel;
            }

            if (_mainButtonLabel != null)
            {
                _mainButtonLabel.text = _viewModel.MainLabel;
            }
        }

        private void EnsureRuntimeHierarchy()
        {
            if (_root == null)
            {
                _root = gameObject;
            }

            var rootRect = TerminalResultScreenLayoutUtility.ConfigureRoot(
                _root,
                PreferredPanelSize,
                MinimumPanelSize,
                MaximumPanelSize);
            if (rootRect == null)
            {
                return;
            }

            _titleLabel ??= CreateLabel("Title", rootRect, TextAnchor.MiddleCenter, 22);
            _detailLabel ??= CreateLabel("Detail", rootRect, TextAnchor.UpperCenter, 15);
            var restart = _restartLevelButton != null
                ? new ButtonParts(_restartLevelButton, _restartLevelButtonLabel)
                : CreateButton("RestartLevelButton", rootRect, "Restart Level");
            var main = _mainButton != null
                ? new ButtonParts(_mainButton, _mainButtonLabel)
                : CreateButton("MainButton", rootRect, "Main");
            _restartLevelButton = restart.Button;
            _restartLevelButtonLabel = restart.Label;
            _mainButton = main.Button;
            _mainButtonLabel = main.Label;

            var header = TerminalResultScreenLayoutUtility.EnsureContainer(rootRect, "ResultHeader", 0, preferredHeight: 32f);
            var detail = TerminalResultScreenLayoutUtility.EnsureContainer(rootRect, "ResultDetail", 1, minHeight: 70f, preferredHeight: 82f, flexibleHeight: 1f);
            var footer = TerminalResultScreenLayoutUtility.EnsureContainer(rootRect, "ResultFooter", 2, preferredHeight: 34f);

            TerminalResultScreenLayoutUtility.LayoutText(_titleLabel, header, preferredHeight: 32f);
            TerminalResultScreenLayoutUtility.LayoutText(_detailLabel, detail, flexibleHeight: 1f);
            TerminalResultScreenLayoutUtility.LayoutFooter(footer);
            TerminalResultScreenLayoutUtility.LayoutButton(_restartLevelButton, footer, preferredWidth: 136f, preferredHeight: 32f);
            TerminalResultScreenLayoutUtility.LayoutButton(_mainButton, footer, preferredWidth: 116f, preferredHeight: 32f);
        }

        private static TMP_Text CreateLabel(
            string objectName,
            RectTransform parent,
            TextAnchor alignment,
            int fontSize)
        {
            var labelObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.alignment = ToTextAlignment(alignment);
            label.fontSize = fontSize;
            label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        private static ButtonParts CreateButton(
            string objectName,
            RectTransform parent,
            string labelText)
        {
            var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.24f, 0.68f, 0.87f, 1f);
            var button = buttonObject.GetComponent<Button>();
            var label = CreateLabel("Label", (RectTransform)buttonObject.transform, TextAnchor.MiddleCenter, 14);
            SettingsLayoutUtility.Stretch(label.rectTransform);
            label.text = labelText;
            return new ButtonParts(button, label);
        }

        private static TextAlignmentOptions ToTextAlignment(TextAnchor alignment)
        {
            return alignment switch
            {
                TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
                TextAnchor.UpperCenter => TextAlignmentOptions.Top,
                _ => TextAlignmentOptions.Left,
            };
        }

        private static void RebindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static void UnbindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
        }

#if UNITY_EDITOR
        private void ValidateSerializedReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                Debug.LogWarning($"{nameof(LevelFailedScreenView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif

        private readonly struct ButtonParts
        {
            public ButtonParts(Button button, TMP_Text label)
            {
                Button = button;
                Label = label;
            }

            public Button Button { get; }

            public TMP_Text Label { get; }
        }
    }
}
