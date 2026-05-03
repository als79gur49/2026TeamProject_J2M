using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class MainMenuScreenView : MonoBehaviour
    {
        private static readonly Vector2 SaveSlotPanelPreferredSize = new Vector2(640f, 640f);
        private static readonly Vector2 SaveSlotPanelMinimumSize = new Vector2(420f, 480f);
        private static readonly Vector2 SaveSlotPanelMaximumSize = new Vector2(760f, 640f);

        private const float TopBarPreferredHeight = 84f;
        private const float TopBarMinimumHeight = 64f;
        private const float BottomBarPreferredHeight = 72f;
        private const float BottomBarMinimumHeight = 56f;
        private const float ContentHorizontalPadding = 64f;
        private const float ContentVerticalPadding = 24f;
        private const float MainCommandPanelLeftMargin = 64f;
        private const float MainCommandPanelBottomMargin = 64f;
        private const float MainCommandButtonWidth = 160f;
        private const float MainCommandButtonHeight = 48f;
        private const float MainCommandButtonSpacing = 12f;
        private const float SaveSlotCardSpacing = 16f;

        private const string MissingAuthoredStructureMessage =
            "MainMenu screen is missing required authored shell references. Repair MainMenuScreen.prefab so it contains TopBar, ContentHost, BottomBar, MainCommandPanel, StartButton, SettingsButton, QuitButton, and SaveSlotPanelView.";

        [SerializeField] private GameObject _root;
        [SerializeField] private RectTransform _topBar;
        [SerializeField] private RectTransform _contentHost;
        [SerializeField] private RectTransform _bottomBar;
        [SerializeField] private RectTransform _mainCommandPanel;
        [SerializeField] private SaveSlotPanelView _saveSlotPanel;
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _quitButton;

        public event Action<MainMenuCommandIntent> CommandRequested;

        public event Action<MainMenuNavigationIntent> NavigationRequested;

        public SaveSlotPanelView SaveSlotPanel => _saveSlotPanel;

        public MainMenuSectionId ActiveSection { get; private set; } = MainMenuSectionId.SaveSlots;

        public void SetVisible(bool visible)
        {
            EnsureResponsiveLayout();
            if (_root != null)
            {
                _root.SetActive(visible);
            }
        }

        public void ValidateAuthoredStructureOrThrow()
        {
            if (_root == null ||
                _topBar == null ||
                _contentHost == null ||
                _bottomBar == null ||
                _mainCommandPanel == null ||
                _saveSlotPanel == null ||
                _startButton == null ||
                _settingsButton == null ||
                _quitButton == null)
            {
                throw new InvalidOperationException(MissingAuthoredStructureMessage);
            }

            if (_topBar.parent != transform ||
                _contentHost.parent != transform ||
                _bottomBar.parent != transform ||
                _mainCommandPanel.parent != transform ||
                _saveSlotPanel.transform.parent != _contentHost)
            {
                throw new InvalidOperationException(MissingAuthoredStructureMessage);
            }

            _saveSlotPanel.ValidateAuthoredStructureOrThrow();
        }

        public void ShowSection(MainMenuSectionId sectionId)
        {
            ActiveSection = sectionId;
            if (_saveSlotPanel != null)
            {
                _saveSlotPanel.gameObject.SetActive(sectionId == MainMenuSectionId.SaveSlots);
            }
        }

        public void SetActiveSection(MainMenuSectionId sectionId)
        {
            ShowSection(sectionId);
        }

        public void RequestSection(MainMenuSectionId sectionId)
        {
            NavigationRequested?.Invoke(new MainMenuNavigationIntent(sectionId));
        }

        public void ClickStart()
        {
            RequestSection(MainMenuSectionId.SaveSlots);
        }

        public void ClickSettings()
        {
            CommandRequested?.Invoke(new MainMenuCommandIntent(MainMenuCommandKind.OpenSettings));
        }

        public void ClickQuit()
        {
            CommandRequested?.Invoke(new MainMenuCommandIntent(MainMenuCommandKind.Quit));
        }

        private void OnEnable()
        {
            EnsureResponsiveLayout();
            WireButtons();
        }

        private void OnDisable()
        {
            UnwireButtons();
        }

        private void OnRectTransformDimensionsChange()
        {
            EnsureResponsiveLayout();
        }

        private void EnsureResponsiveLayout()
        {
            var rootRect = ResolveRootRect();
            if (rootRect == null || _topBar == null || _contentHost == null || _bottomBar == null)
            {
                return;
            }

            SettingsLayoutUtility.Stretch(rootRect);
            SettingsLayoutUtility.EnsureVerticalLayout(
                rootRect.gameObject,
                new RectOffset(0, 0, 0, 0),
                0f,
                TextAnchor.UpperCenter);

            ConfigureShellChild(_topBar, 0, TopBarMinimumHeight, TopBarPreferredHeight, flexibleHeight: 0f);
            ConfigureShellChild(_contentHost, 1, minHeight: 1f, preferredHeight: -1f, flexibleHeight: 1f);
            ConfigureShellChild(_bottomBar, 2, BottomBarMinimumHeight, BottomBarPreferredHeight, flexibleHeight: 0f);
            ConfigureMainCommandPanel(rootRect);

            SettingsLayoutUtility.EnsureVerticalLayout(
                _contentHost.gameObject,
                new RectOffset(
                    Mathf.RoundToInt(ContentHorizontalPadding),
                    Mathf.RoundToInt(ContentHorizontalPadding),
                    Mathf.RoundToInt(ContentVerticalPadding),
                    Mathf.RoundToInt(ContentVerticalPadding)),
                0f,
                TextAnchor.MiddleCenter,
                childControlWidth: true,
                childControlHeight: true,
                childForceExpandWidth: false,
                childForceExpandHeight: false);

            ConfigureSaveSlotPanel(rootRect);
        }

        private RectTransform ResolveRootRect()
        {
            if (_root == null)
            {
                _root = gameObject;
            }

            var rootRect = _root.GetComponent<RectTransform>();
            if (rootRect == null)
            {
                rootRect = _root.AddComponent<RectTransform>();
            }

            return rootRect;
        }

        private void ConfigureShellChild(
            RectTransform child,
            int siblingIndex,
            float minHeight,
            float preferredHeight,
            float flexibleHeight)
        {
            SettingsLayoutUtility.FillLayoutChild(child);
            SettingsLayoutUtility.EnsureLayoutElement(
                child,
                minHeight: minHeight,
                preferredHeight: preferredHeight,
                flexibleWidth: 1f,
                flexibleHeight: flexibleHeight);
            child.SetSiblingIndex(siblingIndex);
        }

        private void ConfigureMainCommandPanel(RectTransform rootRect)
        {
            if (_mainCommandPanel == null)
            {
                return;
            }

            SettingsLayoutUtility.MoveToParent(_mainCommandPanel, rootRect);
            SettingsLayoutUtility.ConfigureOverlay(
                _mainCommandPanel,
                Vector2.zero,
                Vector2.zero,
                new Vector2(MainCommandPanelLeftMargin, MainCommandPanelBottomMargin),
                new Vector2(MainCommandButtonWidth, MainCommandButtonHeight * 3f + MainCommandButtonSpacing * 2f));
            SettingsLayoutUtility.EnsureLayoutElement(_mainCommandPanel, ignoreLayout: true);
            SettingsLayoutUtility.EnsureVerticalLayout(
                _mainCommandPanel.gameObject,
                new RectOffset(0, 0, 0, 0),
                MainCommandButtonSpacing,
                TextAnchor.UpperLeft,
                childControlWidth: true,
                childControlHeight: true,
                childForceExpandWidth: false,
                childForceExpandHeight: false);

            ConfigureCommandButton(_startButton, 0, "Start");
            ConfigureCommandButton(_settingsButton, 1, "Setting");
            ConfigureCommandButton(_quitButton, 2, "Quit");
        }

        private void ConfigureCommandButton(Button button, int siblingIndex, string label)
        {
            if (button == null || _mainCommandPanel == null)
            {
                return;
            }

            SettingsLayoutUtility.MoveToParent(button.transform as RectTransform, _mainCommandPanel);
            SettingsLayoutUtility.FillLayoutChild(button.transform as RectTransform);
            SettingsLayoutUtility.EnsureLayoutElement(
                button,
                minWidth: MainCommandButtonWidth,
                minHeight: MainCommandButtonHeight,
                preferredWidth: MainCommandButtonWidth,
                preferredHeight: MainCommandButtonHeight,
                flexibleWidth: 0f,
                flexibleHeight: 0f);
            EnsureButtonLabel(button, label);
            button.transform.SetSiblingIndex(siblingIndex);
        }

        private static void EnsureButtonLabel(Button button, string label)
        {
            if (button == null)
            {
                return;
            }

            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text == null)
            {
                var labelObject = new GameObject("Label", typeof(RectTransform));
                labelObject.transform.SetParent(button.transform, false);
                var labelRect = (RectTransform)labelObject.transform;
                SettingsLayoutUtility.Stretch(labelRect);
                text = labelObject.AddComponent<TextMeshProUGUI>();
            }

            text.text = label ?? string.Empty;
            text.fontSize = 24f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        private void ConfigureSaveSlotPanel(RectTransform rootRect)
        {
            if (_saveSlotPanel == null)
            {
                return;
            }

            var panelRect = _saveSlotPanel.transform as RectTransform;
            if (panelRect == null)
            {
                return;
            }

            SettingsLayoutUtility.MoveToParent(panelRect, _contentHost);
            SettingsLayoutUtility.FillLayoutChild(panelRect);

            var panelSize = CalculateSaveSlotPanelSize(rootRect);
            SettingsLayoutUtility.EnsureLayoutElement(
                panelRect,
                minWidth: Mathf.Min(SaveSlotPanelMinimumSize.x, panelSize.x),
                minHeight: Mathf.Min(SaveSlotPanelMinimumSize.y, panelSize.y),
                preferredWidth: panelSize.x,
                preferredHeight: panelSize.y,
                flexibleWidth: 0f,
                flexibleHeight: 0f);

            SettingsLayoutUtility.EnsureVerticalLayout(
                panelRect.gameObject,
                new RectOffset(0, 0, 0, 0),
                SaveSlotCardSpacing,
                TextAnchor.UpperCenter,
                childControlWidth: true,
                childControlHeight: true,
                childForceExpandWidth: true,
                childForceExpandHeight: false);

            ConfigureSaveSlotCards(panelSize);
        }

        private Vector2 CalculateSaveSlotPanelSize(RectTransform rootRect)
        {
            var rootSize = ResolveRootSize(rootRect);
            var available = new Vector2(
                rootSize.x - ContentHorizontalPadding * 2f,
                rootSize.y - TopBarPreferredHeight - BottomBarPreferredHeight - ContentVerticalPadding * 2f);

            return new Vector2(
                ClampPanelDimension(
                    SaveSlotPanelPreferredSize.x,
                    SaveSlotPanelMinimumSize.x,
                    SaveSlotPanelMaximumSize.x,
                    available.x),
                ClampPanelDimension(
                    SaveSlotPanelPreferredSize.y,
                    SaveSlotPanelMinimumSize.y,
                    SaveSlotPanelMaximumSize.y,
                    available.y));
        }

        private static Vector2 ResolveRootSize(RectTransform rootRect)
        {
            if (rootRect != null && rootRect.rect.size.sqrMagnitude > 0f)
            {
                return rootRect.rect.size;
            }

            if (rootRect != null && rootRect.parent is RectTransform parentRect && parentRect.rect.size.sqrMagnitude > 0f)
            {
                return parentRect.rect.size;
            }

            return new Vector2(1920f, 1080f);
        }

        private static float ClampPanelDimension(float preferred, float minimum, float maximum, float available)
        {
            if (available <= 0f)
            {
                return 1f;
            }

            if (available < minimum)
            {
                return Mathf.Max(1f, available);
            }

            return Mathf.Min(Mathf.Clamp(preferred, minimum, maximum), available);
        }

        private void ConfigureSaveSlotCards(Vector2 panelSize)
        {
            var cards = _saveSlotPanel.SlotCards;
            if (cards == null || cards.Length == 0)
            {
                return;
            }

            var cardCount = 0;
            for (var i = 0; i < cards.Length; i++)
            {
                if (cards[i] != null)
                {
                    cardCount++;
                }
            }

            if (cardCount == 0)
            {
                return;
            }

            var cardHeight = Mathf.Max(1f, (panelSize.y - SaveSlotCardSpacing * (cardCount - 1)) / cardCount);
            for (var i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                if (card == null)
                {
                    continue;
                }

                SettingsLayoutUtility.MoveToParent(card.transform as RectTransform, _saveSlotPanel.transform as RectTransform);
                SettingsLayoutUtility.FillLayoutChild(card.transform as RectTransform);
                SettingsLayoutUtility.EnsureLayoutElement(
                    card,
                    preferredWidth: panelSize.x,
                    preferredHeight: cardHeight,
                    flexibleWidth: 1f,
                    flexibleHeight: 0f);
                card.transform.SetSiblingIndex(i);
            }
        }

        private void WireButtons()
        {
            Rebind(_startButton, ClickStart);
            Rebind(_settingsButton, ClickSettings);
            Rebind(_quitButton, ClickQuit);
        }

        private void UnwireButtons()
        {
            Unbind(_startButton, ClickStart);
            Unbind(_settingsButton, ClickSettings);
            Unbind(_quitButton, ClickQuit);
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
