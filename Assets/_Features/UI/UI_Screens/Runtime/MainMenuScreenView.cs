using System;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class MainMenuScreenView : MonoBehaviour, IUiNavigationTarget
    {
        private enum MainMenuFocusDomain
        {
            Commands,
            SaveSlots,
        }

        private const int StartCommandIndex = 0;
        private const int SettingsCommandIndex = 1;
        private const int QuitCommandIndex = 2;

        private const string MissingAuthoredStructureMessage =
            "MainMenu screen is missing required authored shell references. Repair MainMenuScreen.prefab so it contains TopBar, ContentHost, BottomBar, MainCommandPanel, StartButton, SettingsButton, QuitButton, SaveSlotPanelView, and command SelectionFrame slots.";

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
        [SerializeField] private UiSelectableButtonGroup _commandNavigationGroup = new UiSelectableButtonGroup();

        private MainMenuFocusDomain _activeFocusDomain = MainMenuFocusDomain.Commands;
        private bool _navigationFocusVisible;
        private bool _pendingEnterSaveSlotNavigation;

        public event Action<MainMenuCommandIntent> CommandRequested;

        public event Action<MainMenuNavigationIntent> NavigationRequested;

        public SaveSlotPanelView SaveSlotPanel => _saveSlotPanel;

        public MainMenuSectionId ActiveSection { get; private set; } = MainMenuSectionId.SaveSlots;

        public bool CanHandleUiNavigation => isActiveAndEnabled && (_root == null || _root.activeInHierarchy);

        public int SelectedCommandIndex => _commandNavigationGroup != null ? _commandNavigationGroup.SelectedIndex : 0;

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
                _quitButtonLabel == null ||
                _commandNavigationGroup == null)
            {
                throw new InvalidOperationException(MissingAuthoredStructureMessage);
            }

            _commandNavigationGroup.ValidateOrThrow(MissingAuthoredStructureMessage);

            RequireOwnedBy(_topBar, transform);
            RequireOwnedBy(_contentHost, transform);
            RequireOwnedBy(_bottomBar, transform);
            RequireOwnedBy(_mainCommandPanel, transform);
            RequireOwnedBy(_saveSlotPanel.transform, _contentHost);
            RequireOwnedBy(_startButton.transform, _mainCommandPanel);
            RequireOwnedBy(_settingsButton.transform, _mainCommandPanel);
            RequireOwnedBy(_quitButton.transform, _mainCommandPanel);
            RequireOwnedBy(_startButtonLabel.transform, _startButton.transform);
            RequireOwnedBy(_settingsButtonLabel.transform, _settingsButton.transform);
            RequireOwnedBy(_quitButtonLabel.transform, _quitButton.transform);

            _saveSlotPanel.ValidateAuthoredStructureOrThrow();
        }

        public bool HandleNavigate(UiNavigationCommand command)
        {
            if (!CanHandleUiNavigation || _commandNavigationGroup == null)
            {
                return false;
            }

            switch (command)
            {
                case UiNavigationCommand.Up:
                case UiNavigationCommand.Down:
                case UiNavigationCommand.Left:
                case UiNavigationCommand.Right:
                    if (IsSaveSlotPanelOpen())
                    {
                        return TryEnterOrRestoreSaveSlotDomain(_navigationFocusVisible)
                            ? _saveSlotPanel.HandleNavigate(command)
                            : true;
                    }

                    if (_activeFocusDomain == MainMenuFocusDomain.SaveSlots && !TryKeepSaveSlotDomainValid())
                    {
                        return true;
                    }

                    if (_activeFocusDomain == MainMenuFocusDomain.SaveSlots)
                    {
                        return _saveSlotPanel.HandleNavigate(command);
                    }

                    switch (command)
                    {
                        case UiNavigationCommand.Up:
                            return _commandNavigationGroup.TryMove(-1);

                        case UiNavigationCommand.Down:
                            return _commandNavigationGroup.TryMove(1);

                        default:
                            return false;
                    }

                default:
                    return false;
            }
        }

        public bool HandleSubmit()
        {
            if (!CanHandleUiNavigation || _commandNavigationGroup == null)
            {
                return false;
            }

            if (_activeFocusDomain == MainMenuFocusDomain.SaveSlots)
            {
                if (!TryKeepSaveSlotDomainValid())
                {
                    return true;
                }

                return _saveSlotPanel.HandleSubmit();
            }

            switch (_commandNavigationGroup.SelectedIndex)
            {
                case StartCommandIndex:
                    _pendingEnterSaveSlotNavigation = _navigationFocusVisible;
                    ClickStart();
                    TryEnterPendingSaveSlotNavigation();
                    return true;

                case SettingsCommandIndex:
                    ClickSettings();
                    return true;

                case QuitCommandIndex:
                    ClickQuit();
                    return true;

                default:
                    return false;
            }
        }

        public bool HandleCancel()
        {
            if (_activeFocusDomain == MainMenuFocusDomain.SaveSlots)
            {
                CloseSaveSlotPanelAndReturnToStart();
                return true;
            }

            return false;
        }

        public void OnNavigationFocusGained()
        {
            _navigationFocusVisible = true;
            if (IsSaveSlotPanelOpen() && TryEnterOrRestoreSaveSlotDomain(showFrame: true))
            {
                return;
            }

            _activeFocusDomain = MainMenuFocusDomain.Commands;
            _saveSlotPanel?.HideNavigationFrames();
            _commandNavigationGroup?.RefreshVisuals();
        }

        public void OnNavigationFocusLost()
        {
            _navigationFocusVisible = false;
            _pendingEnterSaveSlotNavigation = false;
            _commandNavigationGroup?.HideAllFrames();
            _saveSlotPanel?.HideNavigationFrames();
        }

        public void ShowSection(MainMenuSectionId sectionId)
        {
            ActiveSection = sectionId;
            if (_saveSlotPanel != null)
            {
                _saveSlotPanel.gameObject.SetActive(sectionId == MainMenuSectionId.SaveSlots);
                if (sectionId == MainMenuSectionId.SaveSlots)
                {
                    TryEnterPendingSaveSlotNavigation();
                }
                else
                {
                    _pendingEnterSaveSlotNavigation = false;
                    _saveSlotPanel.HideNavigationFrames();
                    if (_activeFocusDomain == MainMenuFocusDomain.SaveSlots)
                    {
                        FocusCommandStart(showFrame: _navigationFocusVisible);
                    }
                }
            }
        }

        public bool TryCloseActiveSection()
        {
            if (ActiveSection == MainMenuSectionId.None)
            {
                return false;
            }

            ShowSection(MainMenuSectionId.None);
            FocusCommandStart(showFrame: _navigationFocusVisible);
            return true;
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
            _commandNavigationGroup?.SetSelectedIndexSilently(_commandNavigationGroup.SelectedIndex);
            _commandNavigationGroup?.HideAllFrames();
            _saveSlotPanel?.HideNavigationFrames();
            _activeFocusDomain = MainMenuFocusDomain.Commands;
            _navigationFocusVisible = false;
            _pendingEnterSaveSlotNavigation = false;
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

        private bool IsSaveSlotPanelOpen()
        {
            return ActiveSection == MainMenuSectionId.SaveSlots &&
                   _saveSlotPanel != null &&
                   _saveSlotPanel.gameObject.activeSelf;
        }

        private void TryEnterPendingSaveSlotNavigation()
        {
            if (!_pendingEnterSaveSlotNavigation)
            {
                if (!_navigationFocusVisible || !IsSaveSlotPanelOpen())
                {
                    _saveSlotPanel?.HideNavigationFrames();
                    return;
                }
            }

            if (IsSaveSlotPanelOpen() && TryEnterOrRestoreSaveSlotDomain(showFrame: _navigationFocusVisible || _pendingEnterSaveSlotNavigation))
            {
                _pendingEnterSaveSlotNavigation = false;
            }
        }

        private bool TryEnterOrRestoreSaveSlotDomain(bool showFrame)
        {
            if (_saveSlotPanel == null || !IsSaveSlotPanelOpen() || !_saveSlotPanel.HasFocusableCards)
            {
                return false;
            }

            _activeFocusDomain = MainMenuFocusDomain.SaveSlots;
            _commandNavigationGroup?.HideAllFrames();
            if (!_saveSlotPanel.RestorePreviousFocusOrFallback(showFrame))
            {
                return _saveSlotPanel.FocusFirstAvailableCardPrimary(showFrame);
            }

            return true;
        }

        private bool TryKeepSaveSlotDomainValid()
        {
            if (IsSaveSlotPanelOpen() && _saveSlotPanel != null && _saveSlotPanel.HasFocusableCards)
            {
                return _saveSlotPanel.RestorePreviousFocusOrFallback(_navigationFocusVisible);
            }

            FocusCommandStart(showFrame: _navigationFocusVisible);
            return false;
        }

        private void CloseSaveSlotPanelAndReturnToStart()
        {
            _saveSlotPanel?.HandleCancel();
            ShowSection(MainMenuSectionId.None);
            FocusCommandStart(showFrame: true);
        }

        private void FocusCommandStart(bool showFrame)
        {
            _activeFocusDomain = MainMenuFocusDomain.Commands;
            _pendingEnterSaveSlotNavigation = false;
            _saveSlotPanel?.HideNavigationFrames();
            _commandNavigationGroup?.SetSelectedIndex(StartCommandIndex);
            if (showFrame)
            {
                _commandNavigationGroup?.RefreshVisuals();
            }
            else
            {
                _commandNavigationGroup?.HideAllFrames();
            }
        }

        private static void RequireOwnedBy(Transform child, Transform owner)
        {
            if (child == null || owner == null || !child.IsChildOf(owner))
            {
                throw new InvalidOperationException(MissingAuthoredStructureMessage);
            }
        }
    }
}
