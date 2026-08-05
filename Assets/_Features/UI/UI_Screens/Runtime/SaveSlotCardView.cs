using System;
using System.Collections.Generic;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public enum SaveSlotActionSelection
    {
        Primary = 0,
        Delete = 1,
    }

    public sealed class SaveSlotCardView : MonoBehaviour
    {
        private const string MissingAuthoredStructureMessage =
            "MainMenu save slot card is missing required authored UI references. Repair MainMenuScreen.prefab so each SaveSlotCardView owns its labels and action buttons.";

        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private TMP_Text _stageLabel;
        [SerializeField] private TMP_Text _chancesLabel;
        [SerializeField] private TMP_Text _deathsLabel;
        [SerializeField] private TMP_Text _lastPlayedLabel;
        [SerializeField] private Button _primaryButton;
        [SerializeField] private TMP_Text _primaryButtonLabel;
        [SerializeField] private Button _deleteButton;
        [SerializeField] private TMP_Text _deleteButtonLabel;
        [SerializeField] private Image _primarySelectionFrame;
        [SerializeField] private Image _deleteSelectionFrame;
        [SerializeField] private UiSelectionVisualProfile _selectionVisualProfile;

        private SaveSlotCardViewModel _viewModel;
        private SaveSlotActionSelection _currentSelection;
        private bool _navigationFrameVisible;
        private bool _interactionBlocked;

        public event Action<SaveSlotIntent> IntentRequested;

        public bool CanFocusPrimary => _primaryButton != null &&
                                       _primaryButton.interactable &&
                                       _viewModel != null &&
                                       _viewModel.PrimaryIntentKind != SaveSlotIntentKind.None;

        public bool CanFocusDelete => _deleteButton != null &&
                                      _deleteButton.interactable &&
                                      _viewModel != null &&
                                      _viewModel.ShowDelete;

        public bool HasAnyFocusableAction => CanFocusPrimary || CanFocusDelete;

        public SaveSlotActionSelection CurrentSelection => _currentSelection;

        public void Bind(SaveSlotCardViewModel viewModel)
        {
            _viewModel = viewModel;
            ValidateAuthoredStructureOrThrow();
            Refresh();
            RefreshNavigationVisuals();
        }

        public void SetInteractionBlocked(bool blocked)
        {
            _interactionBlocked = blocked;
            Refresh();
            RefreshNavigationVisuals();
        }

        public void ValidateAuthoredStructureOrThrow()
        {
            if (_titleLabel == null ||
                _statusLabel == null ||
                _stageLabel == null ||
                _chancesLabel == null ||
                _deathsLabel == null ||
                _lastPlayedLabel == null ||
                _primaryButton == null ||
                _primaryButtonLabel == null ||
                _deleteButton == null ||
                _deleteButtonLabel == null ||
                _primarySelectionFrame == null ||
                _deleteSelectionFrame == null ||
                _selectionVisualProfile == null)
            {
                throw new InvalidOperationException(MissingAuthoredStructureMessage);
            }

            if (!IsOwnedByCard(_titleLabel.transform) ||
                !IsOwnedByCard(_statusLabel.transform) ||
                !IsOwnedByCard(_stageLabel.transform) ||
                !IsOwnedByCard(_chancesLabel.transform) ||
                !IsOwnedByCard(_deathsLabel.transform) ||
                !IsOwnedByCard(_lastPlayedLabel.transform) ||
                !IsOwnedByCard(_primaryButton.transform) ||
                !IsOwnedByCard(_primaryButtonLabel.transform) ||
                !IsOwnedByCard(_deleteButton.transform) ||
                !IsOwnedByCard(_deleteButtonLabel.transform) ||
                !_primarySelectionFrame.transform.IsChildOf(_primaryButton.transform) ||
                !_deleteSelectionFrame.transform.IsChildOf(_deleteButton.transform))
            {
                throw new InvalidOperationException(MissingAuthoredStructureMessage);
            }
        }

        public IReadOnlyList<TMP_Text> CreateTypographyTargets()
        {
            return new[]
            {
                _titleLabel,
                _statusLabel,
                _stageLabel,
                _chancesLabel,
                _deathsLabel,
                _lastPlayedLabel,
                _primaryButtonLabel,
                _deleteButtonLabel,
            };
        }

        private void OnEnable()
        {
            ValidateAuthoredStructureOrThrow();
            Rebind(_primaryButton, HandlePrimaryClicked);
            Rebind(_deleteButton, HandleDeleteClicked);
        }

        private void OnDisable()
        {
            Unbind(_primaryButton, HandlePrimaryClicked);
            Unbind(_deleteButton, HandleDeleteClicked);
        }

        private void HandlePrimaryClicked()
        {
            if (_interactionBlocked ||
                _viewModel == null ||
                _viewModel.PrimaryIntentKind == SaveSlotIntentKind.None)
            {
                return;
            }

            IntentRequested?.Invoke(new SaveSlotIntent(_viewModel.SlotNumber, _viewModel.PrimaryIntentKind));
        }

        private void HandleDeleteClicked()
        {
            if (_interactionBlocked || _viewModel == null || !_viewModel.ShowDelete)
            {
                return;
            }

            IntentRequested?.Invoke(new SaveSlotIntent(_viewModel.SlotNumber, SaveSlotIntentKind.Delete));
        }

        public void SetActionSelection(SaveSlotActionSelection selection, bool showFrame)
        {
            _currentSelection = NormalizeSelection(selection);
            _navigationFrameVisible = showFrame;
            RefreshNavigationVisuals();
        }

        public bool MoveActionLeft()
        {
            if (_currentSelection != SaveSlotActionSelection.Delete || !CanFocusPrimary)
            {
                return false;
            }

            SetActionSelection(SaveSlotActionSelection.Primary, _navigationFrameVisible);
            return true;
        }

        public bool MoveActionRight()
        {
            if (_currentSelection != SaveSlotActionSelection.Primary || !CanFocusDelete)
            {
                return false;
            }

            SetActionSelection(SaveSlotActionSelection.Delete, _navigationFrameVisible);
            return true;
        }

        public bool SubmitSelectedAction()
        {
            _currentSelection = NormalizeSelection(_currentSelection);
            switch (_currentSelection)
            {
                case SaveSlotActionSelection.Primary:
                    if (!CanFocusPrimary)
                    {
                        return false;
                    }

                    PlayActionSubmitFeedback(SaveSlotActionSelection.Primary);
                    HandlePrimaryClicked();
                    return true;

                case SaveSlotActionSelection.Delete:
                    if (!CanFocusDelete)
                    {
                        return false;
                    }

                    PlayActionSubmitFeedback(SaveSlotActionSelection.Delete);
                    HandleDeleteClicked();
                    return true;

                default:
                    return false;
            }
        }

        public void HideNavigationFrames()
        {
            _navigationFrameVisible = false;
            ApplyFrame(_primarySelectionFrame, false, CanFocusPrimary);
            ApplyFrame(_deleteSelectionFrame, false, CanFocusDelete);
            ApplyActionFeedback(SaveSlotActionSelection.Primary, focused: false);
            ApplyActionFeedback(SaveSlotActionSelection.Delete, focused: false);
        }

        public void RefreshNavigationVisuals()
        {
            _currentSelection = NormalizeSelection(_currentSelection);
            if (!_navigationFrameVisible)
            {
                HideNavigationFrames();
                return;
            }

            ApplyFrame(
                _primarySelectionFrame,
                _currentSelection == SaveSlotActionSelection.Primary && CanFocusPrimary,
                CanFocusPrimary);
            ApplyFrame(
                _deleteSelectionFrame,
                _currentSelection == SaveSlotActionSelection.Delete && CanFocusDelete,
                CanFocusDelete);
            ApplyActionFeedback(
                SaveSlotActionSelection.Primary,
                _currentSelection == SaveSlotActionSelection.Primary && CanFocusPrimary);
            ApplyActionFeedback(
                SaveSlotActionSelection.Delete,
                _currentSelection == SaveSlotActionSelection.Delete && CanFocusDelete);
        }

        public SaveSlotActionSelection NormalizeSelection(SaveSlotActionSelection desired)
        {
            if (desired == SaveSlotActionSelection.Delete && CanFocusDelete)
            {
                return SaveSlotActionSelection.Delete;
            }

            if (CanFocusPrimary)
            {
                return SaveSlotActionSelection.Primary;
            }

            return CanFocusDelete ? SaveSlotActionSelection.Delete : SaveSlotActionSelection.Primary;
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

            SetOptionalLabel(_stageLabel, _viewModel?.StageText);
            SetOptionalLabel(_chancesLabel, _viewModel?.ChancesText);
            SetOptionalLabel(_deathsLabel, _viewModel?.DeathsText);
            SetOptionalLabel(_lastPlayedLabel, _viewModel?.LastPlayedText);

            if (_primaryButtonLabel != null)
            {
                _primaryButtonLabel.text = _viewModel?.PrimaryActionText ?? string.Empty;
            }

            if (_primaryButton != null)
            {
                var hasPrimaryIntent = _viewModel != null && _viewModel.PrimaryIntentKind != SaveSlotIntentKind.None;
                var hideUnavailableFailureAction =
                    _viewModel != null &&
                    _viewModel.FailureKind != SaveSlotFailurePresentationKind.None &&
                    !hasPrimaryIntent;
                _primaryButton.gameObject.SetActive(!hideUnavailableFailureAction);
                _primaryButton.interactable = !_interactionBlocked && hasPrimaryIntent;
            }

            if (_deleteButton != null)
            {
                var showDelete = _viewModel != null && _viewModel.ShowDelete;
                var hideUnavailableFailureAction =
                    _viewModel != null &&
                    _viewModel.FailureKind != SaveSlotFailurePresentationKind.None &&
                    !showDelete;
                _deleteButton.gameObject.SetActive(!hideUnavailableFailureAction);
                _deleteButton.interactable = !_interactionBlocked && showDelete;
            }

            if (_deleteButtonLabel != null)
            {
                _deleteButtonLabel.text = _viewModel?.DeleteActionText ?? string.Empty;
            }
        }

        private void ApplyFrame(Image frame, bool isSelected, bool isFocusable)
        {
            if (frame == null)
            {
                return;
            }

            if (_selectionVisualProfile != null && _selectionVisualProfile.FrameSprite != null)
            {
                frame.sprite = _selectionVisualProfile.FrameSprite;
            }

            frame.color = _selectionVisualProfile != null
                ? (isSelected ? _selectionVisualProfile.SelectedFrameColor : _selectionVisualProfile.UnselectedFrameColor)
                : (isSelected ? Color.white : new Color(1f, 1f, 1f, 0f));
            frame.gameObject.SetActive(isFocusable && (isSelected ||
                                                       _selectionVisualProfile == null ||
                                                       !_selectionVisualProfile.HideUnselectedFrames));
        }

        private void ApplyActionFeedback(SaveSlotActionSelection selection, bool focused)
        {
            ResolveActionFeedback(selection)?.SetNavigationFocused(focused);
        }

        private void PlayActionSubmitFeedback(SaveSlotActionSelection selection)
        {
            ResolveActionFeedback(selection)?.PlaySubmitFeedback();
        }

        private IUiSelectionFeedback ResolveActionFeedback(SaveSlotActionSelection selection)
        {
            var button = selection == SaveSlotActionSelection.Delete ? _deleteButton : _primaryButton;
            if (button != null && button.TryGetComponent<IUiSelectionFeedback>(out var buttonFeedback))
            {
                return buttonFeedback;
            }

            var frame = selection == SaveSlotActionSelection.Delete ? _deleteSelectionFrame : _primarySelectionFrame;
            return frame != null
                ? frame.GetComponentInParent<IUiSelectionFeedback>(includeInactive: true)
                : null;
        }

        private static void SetOptionalLabel(TMP_Text label, string text)
        {
            if (label == null)
            {
                return;
            }

            label.text = text ?? string.Empty;
            label.gameObject.SetActive(true);
        }

        private bool IsOwnedByCard(Transform child)
        {
            if (child == null)
            {
                return false;
            }

            return child == transform || child.IsChildOf(transform);
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
