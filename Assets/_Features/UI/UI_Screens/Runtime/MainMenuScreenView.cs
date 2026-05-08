using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class MainMenuScreenView : MonoBehaviour
    {
        private const string MissingAuthoredStructureMessage =
            "MainMenu screen is missing required authored shell references. Repair MainMenuScreen.prefab so it contains TopBar, ContentHost, BottomBar, MainCommandPanel, StartButton, SettingsButton, QuitButton, and SaveSlotPanelView.";

        [SerializeField] private GameObject _root;
        [SerializeField] private RectTransform _topBar;
        [SerializeField] private RectTransform _contentHost;
        [SerializeField] private RectTransform _bottomBar;
        [SerializeField] private RectTransform _mainCommandPanel;
        [SerializeField] private SaveSlotPanelView _saveSlotPanel;
        [SerializeField] private Button _startButton;
        [SerializeField] private TMP_Text _startButtonLabel;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private TMP_Text _settingsButtonLabel;
        [SerializeField] private Button _quitButton;
        [SerializeField] private TMP_Text _quitButtonLabel;

        public event Action<MainMenuCommandIntent> CommandRequested;

        public event Action<MainMenuNavigationIntent> NavigationRequested;

        public SaveSlotPanelView SaveSlotPanel => _saveSlotPanel;

        public MainMenuSectionId ActiveSection { get; private set; } = MainMenuSectionId.SaveSlots;

        public void SetVisible(bool visible)
        {
            EnsureSaveSlotCardOrder();
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
                _startButtonLabel == null ||
                _settingsButton == null ||
                _settingsButtonLabel == null ||
                _quitButton == null ||
                _quitButtonLabel == null)
            {
                throw new InvalidOperationException(MissingAuthoredStructureMessage);
            }

            if (_topBar.parent != transform ||
                _contentHost.parent != transform ||
                _bottomBar.parent != transform ||
                _mainCommandPanel.parent != transform ||
                _saveSlotPanel.transform.parent != _contentHost ||
                _startButton.transform.parent != _mainCommandPanel ||
                _settingsButton.transform.parent != _mainCommandPanel ||
                _quitButton.transform.parent != _mainCommandPanel ||
                _startButtonLabel.transform.parent != _startButton.transform ||
                _settingsButtonLabel.transform.parent != _settingsButton.transform ||
                _quitButtonLabel.transform.parent != _quitButton.transform)
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
            EnsureSaveSlotCardOrder();
            WireButtons();
        }

        private void OnDisable()
        {
            UnwireButtons();
        }

        private void EnsureSaveSlotCardOrder()
        {
            var cards = _saveSlotPanel != null ? _saveSlotPanel.SlotCards : null;
            if (cards == null || cards.Length == 0)
            {
                return;
            }

            for (var i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                if (card == null)
                {
                    continue;
                }

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
