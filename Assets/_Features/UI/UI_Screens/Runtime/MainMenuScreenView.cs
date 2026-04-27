using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class MainMenuScreenView : MonoBehaviour
    {
        private const string MissingAuthoredStructureMessage =
            "MainMenu screen is missing required authored shell references. Repair MainMenuScreen.prefab so it contains TopBar, ContentHost, BottomBar, SaveSlotPanelView, SettingsButton, and QuitButton.";

        [SerializeField] private GameObject _root;
        [SerializeField] private RectTransform _topBar;
        [SerializeField] private RectTransform _contentHost;
        [SerializeField] private RectTransform _bottomBar;
        [SerializeField] private SaveSlotPanelView _saveSlotPanel;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _quitButton;

        public event Action<MainMenuCommandIntent> CommandRequested;

        public event Action<MainMenuNavigationIntent> NavigationRequested;

        public SaveSlotPanelView SaveSlotPanel => _saveSlotPanel;

        public MainMenuSectionId ActiveSection { get; private set; } = MainMenuSectionId.SaveSlots;

        public void SetVisible(bool visible)
        {
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
                _saveSlotPanel == null ||
                _settingsButton == null ||
                _quitButton == null)
            {
                throw new InvalidOperationException(MissingAuthoredStructureMessage);
            }

            if (_topBar.parent != transform ||
                _contentHost.parent != transform ||
                _bottomBar.parent != transform ||
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
            WireButtons();
        }

        private void OnDisable()
        {
            UnwireButtons();
        }

        private void WireButtons()
        {
            Rebind(_settingsButton, ClickSettings);
            Rebind(_quitButton, ClickQuit);
        }

        private void UnwireButtons()
        {
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
