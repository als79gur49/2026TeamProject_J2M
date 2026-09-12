using System;
using System.Collections.Generic;
using Game.Feature.UI.Composition;
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
            SaveSlotClose,
        }

        private const int StartCommandIndex = 0;
        private const int SettingsCommandIndex = 1;
        private const int QuitCommandIndex = 2;

        private const string MissingAuthoredStructureMessage =
            "MainMenu screen is missing required authored shell references. Repair MainMenuScreen.prefab so it contains TopBar, ContentHost, BottomBar, MainCommandPanel, SaveSlotOverlayLayer, StartButton, SettingsButton, QuitButton, SaveSlotPanelView, SaveSlotBlocker, SaveSlotCloseButton, and command SelectionFrame slots.";

        [SerializeField] private GameObject _root;
        [SerializeField] private RectTransform _topBar;
        [SerializeField] private RectTransform _contentHost;
        [SerializeField] private RectTransform _bottomBar;
        [SerializeField] private RectTransform _mainCommandPanel;
        [SerializeField] private RectTransform _saveSlotOverlayLayer;
        [SerializeField] private SaveSlotPanelView _saveSlotPanel;
        [SerializeField] private GameObject _saveSlotBlockerRoot;
        [SerializeField] private CanvasGroup _saveSlotBlockerCanvasGroup;
        [SerializeField] private Image _saveSlotBlockerImage;
        [SerializeField] private Button _saveSlotCloseButton;
        [SerializeField] private Image _saveSlotCloseSelectionFrame;
        [SerializeField] private UiSelectionVisualProfile _saveSlotCloseSelectionVisualProfile;
        [SerializeField] private Button _startButton;
        [SerializeField] private TMP_Text _startButtonLabel;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private TMP_Text _settingsButtonLabel;
        [SerializeField] private Button _quitButton;
        [SerializeField] private TMP_Text _quitButtonLabel;
        [SerializeField] private UiSelectableButtonGroup _commandNavigationGroup = new UiSelectableButtonGroup();

        private MainMenuFocusDomain _activeFocusDomain = MainMenuFocusDomain.Commands;
        private List<LocalizedTmpTextBinding> _localizedStaticBindings;
        private List<LocalizedTmpTypographyBinding> _localizedSlotTypographyBindings;
        private bool _navigationFocusVisible;
        private bool _pendingEnterSaveSlotNavigation;
        private bool _launchInteractionBlocked;

        public event Action<MainMenuCommandIntent> CommandRequested;

        public event Action<MainMenuNavigationIntent> NavigationRequested;

        public event Action<MainMenuSectionId> SectionChanged;

        public SaveSlotPanelView SaveSlotPanel => _saveSlotPanel;

        public MainMenuSectionId ActiveSection { get; private set; } = MainMenuSectionId.SaveSlots;

        public bool CanHandleUiNavigation =>
            !_launchInteractionBlocked &&
            isActiveAndEnabled &&
            (_root == null || _root.activeInHierarchy);

        public int SelectedCommandIndex => _commandNavigationGroup != null ? _commandNavigationGroup.SelectedIndex : 0;

        public void SetVisible(bool visible)
        {
            EnsureSaveSlotCardOrder();
            if (_root != null)
            {
                _root.SetActive(visible);
            }
        }

        public void SetLaunchInteractionBlocked(bool blocked)
        {
            _launchInteractionBlocked = blocked;
            _saveSlotPanel?.SetInteractionBlocked(blocked);
            if (_saveSlotCloseButton != null)
            {
                _saveSlotCloseButton.interactable = !blocked && ActiveSection == MainMenuSectionId.SaveSlots;
            }
            ApplyCommandButtonsInteractable(!blocked && ActiveSection != MainMenuSectionId.SaveSlots);
            if (blocked)
            {
                OnNavigationFocusLost();
            }
        }

        public void BindStaticLocalization(
            MainMenuStaticTextPayload payload,
            ILocalizedTextResolver textResolver,
            ILocalizedTypographyResolver typographyResolver,
            ILocalizedTmpFontResolver fontResolver = null,
            GameplayUiTypographyTheme typographyTheme = null)
        {
            UnbindStaticLocalization();
            if (payload == null)
            {
                return;
            }

            _localizedStaticBindings = new List<LocalizedTmpTextBinding>
            {
                new(
                    _startButtonLabel,
                    payload.StartLabelDescriptor,
                    textResolver,
                    typographyResolver,
                    fontResolver,
                    typographyTheme),
                new(
                    _settingsButtonLabel,
                    payload.SettingsLabelDescriptor,
                    textResolver,
                    typographyResolver,
                    fontResolver,
                    typographyTheme),
                new(
                    _quitButtonLabel,
                    payload.QuitLabelDescriptor,
                    textResolver,
                    typographyResolver,
                    fontResolver,
                    typographyTheme),
            };

            var slotTargets = _saveSlotPanel != null
                ? _saveSlotPanel.CreateTypographyTargets()
                : Array.Empty<TMP_Text>();
            var slotCardTypographyTargetCount = _saveSlotPanel != null
                ? _saveSlotPanel.SlotCards.Length * SaveSlotCardView.TypographyTargetCount
                : 0;
            _localizedSlotTypographyBindings = new List<LocalizedTmpTypographyBinding>(slotTargets.Count);
            for (var i = 0; i < slotTargets.Count; i++)
            {
                var target = slotTargets[i];
                var authoredBinding = TypographyBinding.FindFor(target);
                if (authoredBinding != null)
                {
                    _localizedSlotTypographyBindings.Add(new LocalizedTmpTypographyBinding(
                        target,
                        textResolver,
                        typographyTheme,
                        authoredBinding));
                    continue;
                }

                if (i >= slotCardTypographyTargetCount)
                {
                    throw new InvalidOperationException(
                        $"MainMenu non-card typography target '{target?.name}' requires an authored TypographyBinding.");
                }

                _localizedSlotTypographyBindings.Add(new LocalizedTmpTypographyBinding(
                    target,
                    textResolver,
                    typographyTheme,
                    ResolveSlotCardTypographyStyle(i)));
            }
        }

        private static TypographyStyleTag ResolveSlotCardTypographyStyle(int targetIndex)
        {
            switch (targetIndex % SaveSlotCardView.TypographyTargetCount)
            {
                case 0:
                    return TypographyStyleTag.HeaderSmall;
                case 1:
                    return TypographyStyleTag.Status;
                case 2:
                    return TypographyStyleTag.Label;
                case 3:
                case 4:
                case 5:
                    return TypographyStyleTag.BodySmall;
                case 6:
                case 7:
                    return TypographyStyleTag.Button;
                default:
                    throw new ArgumentOutOfRangeException(nameof(targetIndex), targetIndex, null);
            }
        }

        public void UnbindStaticLocalization()
        {
            if (_localizedStaticBindings == null)
            {
                return;
            }

            for (var i = 0; i < _localizedStaticBindings.Count; i++)
            {
                _localizedStaticBindings[i]?.Dispose();
            }

            _localizedStaticBindings.Clear();
            _localizedStaticBindings = null;

            if (_localizedSlotTypographyBindings == null)
            {
                return;
            }

            for (var i = 0; i < _localizedSlotTypographyBindings.Count; i++)
            {
                _localizedSlotTypographyBindings[i]?.Dispose();
            }

            _localizedSlotTypographyBindings.Clear();
            _localizedSlotTypographyBindings = null;
        }

        public void ValidateAuthoredStructureOrThrow()
        {
            if (_root == null ||
                _topBar == null ||
                _contentHost == null ||
                _bottomBar == null ||
                _mainCommandPanel == null ||
                _saveSlotOverlayLayer == null ||
                _saveSlotPanel == null ||
                _saveSlotBlockerRoot == null ||
                _saveSlotBlockerCanvasGroup == null ||
                _saveSlotBlockerImage == null ||
                _saveSlotCloseButton == null ||
                _saveSlotCloseSelectionFrame == null ||
                _saveSlotCloseSelectionVisualProfile == null ||
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
            RequireOwnedBy(_saveSlotOverlayLayer, transform);
            RequireOwnedBy(_saveSlotPanel.transform, _saveSlotOverlayLayer);
            RequireOwnedBy(_saveSlotBlockerRoot.transform, _saveSlotOverlayLayer);
            RequireOwnedBy(_saveSlotCloseButton.transform, _saveSlotOverlayLayer);
            RequireOwnedBy(_saveSlotCloseSelectionFrame.transform, _saveSlotCloseButton.transform);
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
                        if (_activeFocusDomain == MainMenuFocusDomain.SaveSlotClose)
                        {
                            return HandleSaveSlotCloseNavigate(command);
                        }

                        if (command == UiNavigationCommand.Up &&
                            TryEnterOrRestoreSaveSlotDomain(_navigationFocusVisible) &&
                            _saveSlotPanel.IsAtTopNavigationBoundary)
                        {
                            FocusSaveSlotClose();
                            return true;
                        }

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

            if (_activeFocusDomain == MainMenuFocusDomain.SaveSlotClose)
            {
                ResolveSaveSlotCloseFeedback()?.PlaySubmitFeedback();
                ClickSaveSlotClose();
                return true;
            }

            switch (_commandNavigationGroup.SelectedIndex)
            {
                case StartCommandIndex:
                    _pendingEnterSaveSlotNavigation = _navigationFocusVisible;
                    _commandNavigationGroup.PlaySelectedSubmitFeedback();
                    ClickStart();
                    TryEnterPendingSaveSlotNavigation();
                    return true;

                case SettingsCommandIndex:
                    _commandNavigationGroup.PlaySelectedSubmitFeedback();
                    ClickSettings();
                    return true;

                case QuitCommandIndex:
                    _commandNavigationGroup.PlaySelectedSubmitFeedback();
                    ClickQuit();
                    return true;

                default:
                    return false;
            }
        }

        public bool HandleCancel()
        {
            if (_launchInteractionBlocked)
            {
                return true;
            }

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
            HideSaveSlotCloseFocus();
        }

        public void ShowSection(MainMenuSectionId sectionId)
        {
            if (_launchInteractionBlocked)
            {
                return;
            }

            var previousSection = ActiveSection;
            ActiveSection = sectionId;
            var showSaveSlots = sectionId == MainMenuSectionId.SaveSlots;

            ApplySaveSlotOverlayState(showSaveSlots);

            if (_saveSlotPanel != null)
            {
                _saveSlotPanel.gameObject.SetActive(showSaveSlots);
                if (showSaveSlots)
                {
                    TryEnterPendingSaveSlotNavigation();
                }
                else
                {
                    _pendingEnterSaveSlotNavigation = false;
                    _saveSlotPanel.HideNavigationFrames();
                    HideSaveSlotCloseFocus();
                    if (_activeFocusDomain == MainMenuFocusDomain.SaveSlots)
                    {
                        FocusCommandStart(showFrame: _navigationFocusVisible);
                    }
                }
            }

            ApplySaveSlotModalState(showSaveSlots);

            if (previousSection != ActiveSection)
            {
                SectionChanged?.Invoke(ActiveSection);
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
            if (_launchInteractionBlocked)
            {
                return;
            }

            NavigationRequested?.Invoke(new MainMenuNavigationIntent(sectionId));
        }

        public void ClickStart()
        {
            RequestSection(MainMenuSectionId.SaveSlots);
        }

        public void ClickSettings()
        {
            if (_launchInteractionBlocked)
            {
                return;
            }

            CommandRequested?.Invoke(new MainMenuCommandIntent(MainMenuCommandKind.OpenSettings));
        }

        public void ClickQuit()
        {
            if (_launchInteractionBlocked)
            {
                return;
            }

            CommandRequested?.Invoke(new MainMenuCommandIntent(MainMenuCommandKind.Quit));
        }

        private void OnEnable()
        {
            EnsureSaveSlotCardOrder();
            WireButtons();
            _commandNavigationGroup?.SetSelectedIndexSilently(_commandNavigationGroup.SelectedIndex);
            _commandNavigationGroup?.HideAllFrames();
            _saveSlotPanel?.HideNavigationFrames();
            HideSaveSlotCloseFocus();
            _activeFocusDomain = MainMenuFocusDomain.Commands;
            _navigationFocusVisible = false;
            _pendingEnterSaveSlotNavigation = false;
        }

        private void OnDisable()
        {
            UnwireButtons();
            ApplyCommandButtonsInteractable(true);
        }

        private void OnDestroy()
        {
            UnbindStaticLocalization();
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
            Rebind(_saveSlotCloseButton, ClickSaveSlotClose);
        }

        private void UnwireButtons()
        {
            Unbind(_startButton, ClickStart);
            Unbind(_settingsButton, ClickSettings);
            Unbind(_quitButton, ClickQuit);
            Unbind(_saveSlotCloseButton, ClickSaveSlotClose);
        }

        private void ApplySaveSlotModalState(bool showSaveSlots)
        {
            ApplySaveSlotBlockerState(showSaveSlots);
            ApplyCommandButtonsInteractable(!_launchInteractionBlocked && !showSaveSlots);
            if (_saveSlotCloseButton != null)
            {
                _saveSlotCloseButton.interactable = !_launchInteractionBlocked && showSaveSlots;
            }
        }

        private void ClickSaveSlotClose()
        {
            if (_launchInteractionBlocked || !IsSaveSlotPanelOpen())
            {
                return;
            }

            CloseSaveSlotPanelAndReturnToStart();
        }

        private void ApplySaveSlotOverlayState(bool showSaveSlots)
        {
            if (_saveSlotOverlayLayer == null)
            {
                return;
            }

            _saveSlotOverlayLayer.gameObject.SetActive(showSaveSlots);
            if (showSaveSlots)
            {
                _saveSlotOverlayLayer.SetAsLastSibling();
            }
        }

        private void ApplySaveSlotBlockerState(bool showSaveSlots)
        {
            if (_saveSlotBlockerRoot != null)
            {
                _saveSlotBlockerRoot.SetActive(showSaveSlots);
            }

            if (_saveSlotBlockerCanvasGroup != null)
            {
                _saveSlotBlockerCanvasGroup.alpha = showSaveSlots ? 1f : 0f;
                _saveSlotBlockerCanvasGroup.interactable = showSaveSlots;
                _saveSlotBlockerCanvasGroup.blocksRaycasts = showSaveSlots;
            }

            if (_saveSlotBlockerImage != null)
            {
                _saveSlotBlockerImage.raycastTarget = showSaveSlots;
            }
        }

        private void ApplyCommandButtonsInteractable(bool interactable)
        {
            if (_startButton != null)
            {
                _startButton.interactable = interactable;
            }

            if (_settingsButton != null)
            {
                _settingsButton.interactable = interactable;
            }

            if (_quitButton != null)
            {
                _quitButton.interactable = interactable;
            }
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
            HideSaveSlotCloseFocus();
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
            HideSaveSlotCloseFocus();
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

        private bool HandleSaveSlotCloseNavigate(UiNavigationCommand command)
        {
            if (command != UiNavigationCommand.Down)
            {
                return true;
            }

            _activeFocusDomain = MainMenuFocusDomain.SaveSlots;
            HideSaveSlotCloseFocus();
            return _saveSlotPanel != null &&
                   _saveSlotPanel.RestorePreviousFocusOrFallback(showFrame: _navigationFocusVisible);
        }

        private void FocusSaveSlotClose()
        {
            _activeFocusDomain = MainMenuFocusDomain.SaveSlotClose;
            _commandNavigationGroup?.HideAllFrames();
            _saveSlotPanel?.HideNavigationFrames();
            ApplySaveSlotCloseFrame(focused: true);
            ResolveSaveSlotCloseFeedback()?.SetNavigationFocused(true);
            _saveSlotCloseButton?.Select();
        }

        private void HideSaveSlotCloseFocus()
        {
            ApplySaveSlotCloseFrame(focused: false);
            ResolveSaveSlotCloseFeedback()?.SetNavigationFocused(false);
        }

        private void ApplySaveSlotCloseFrame(bool focused)
        {
            if (_saveSlotCloseSelectionFrame == null)
            {
                return;
            }

            if (_saveSlotCloseSelectionVisualProfile != null &&
                _saveSlotCloseSelectionVisualProfile.FrameSprite != null)
            {
                _saveSlotCloseSelectionFrame.sprite = _saveSlotCloseSelectionVisualProfile.FrameSprite;
            }

            _saveSlotCloseSelectionFrame.color = _saveSlotCloseSelectionVisualProfile != null
                ? (focused
                    ? _saveSlotCloseSelectionVisualProfile.SelectedFrameColor
                    : _saveSlotCloseSelectionVisualProfile.UnselectedFrameColor)
                : (focused ? Color.white : new Color(1f, 1f, 1f, 0f));
            _saveSlotCloseSelectionFrame.gameObject.SetActive(focused);
        }

        private IUiSelectionFeedback ResolveSaveSlotCloseFeedback()
        {
            return _saveSlotCloseButton != null &&
                   _saveSlotCloseButton.TryGetComponent<IUiSelectionFeedback>(out var feedback)
                ? feedback
                : null;
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
