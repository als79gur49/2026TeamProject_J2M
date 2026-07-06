using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class SettingsScreenView : MonoBehaviour, IScreenView, IUiNavigationTarget
    {
        private const string MissingAudioSectionMessage =
            "Settings screen is missing or miswired required authored audio section. Repair: assign SettingsScreenView._audioView to the SettingsAudioSection child view.";

        private const string MissingDisplaySectionMessage =
            "Settings screen is missing or miswired required authored display section. Repair: assign SettingsScreenView._displayView to the SettingsDisplaySection child view.";

        private const string MissingInputSectionMessage =
            "Settings screen is missing or miswired required authored input section. Repair: assign SettingsScreenView._inputView to the SettingsInputSection child view.";

        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private Button _audioTabButton;
        [SerializeField] private TMP_Text _audioTabButtonLabel;
        [SerializeField] private Button _displayTabButton;
        [SerializeField] private TMP_Text _displayTabButtonLabel;
        [SerializeField] private Button _inputTabButton;
        [SerializeField] private TMP_Text _inputTabButtonLabel;
        [SerializeField] private Button _backButton;
        [SerializeField] private TMP_Text _backButtonLabel;
        [SerializeField] private SettingsAudioView _audioView;
        [SerializeField] private SettingsDisplayView _displayView;
        [SerializeField] private SettingsInputView _inputView;
        [SerializeField] private UiFocusNodeSlot[] _focusNodeSlots = Array.Empty<UiFocusNodeSlot>();

        private bool _isVisible;
        private readonly UiFocusGraphNavigator _focusGraph = new();
        private List<LocalizedTmpTextBinding> _localizedStaticBindings;
        private SettingsScreenViewModel _viewModel;
        private Tween _enterTween;
        private CanvasGroup _rootCanvasGroup;
        private bool _lastVisibleState;
        private bool _hasRootRestAlpha;
        private bool _isFocusGraphBuilt;
        private bool _hasNavigationFocus;
        private float _rootRestAlpha = 1f;

        public event Action<SettingsSectionId> SectionSelected;

        public event Action BackRequested;

        public bool CanHandleUiNavigation =>
            IsVisible &&
            isActiveAndEnabled;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                RefreshView();
            }
        }

        public SettingsAudioView AudioView => _audioView;

        public SettingsDisplayView DisplayView => _displayView;

        public SettingsInputView InputView => _inputView;

        internal SettingsSectionId? CurrentSectionIdForNavigation =>
            _viewModel != null ? _viewModel.SelectedSection : (SettingsSectionId?)null;

        public void Bind(SettingsScreenViewModel viewModel)
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

        public void BindStaticLocalization(
            SettingsScreenPayload payload,
            ILocalizedTextResolver textResolver,
            ILocalizedTypographyResolver typographyResolver,
            ILocalizedTmpFontResolver fontResolver = null)
        {
            UnbindStaticLocalization();
            if (payload == null)
            {
                return;
            }

            _localizedStaticBindings = new List<LocalizedTmpTextBinding>
            {
                new(
                    _titleLabel,
                    payload.TitleTextDescriptor,
                    textResolver,
                    typographyResolver,
                    fontResolver),
                new(
                    _backButtonLabel,
                    payload.BackLabelDescriptor,
                    textResolver,
                    typographyResolver,
                    fontResolver),
                new(
                    _audioTabButtonLabel,
                    payload.AudioTabLabelDescriptor,
                    textResolver,
                    typographyResolver,
                    fontResolver),
                new(
                    _displayTabButtonLabel,
                    payload.DisplayTabLabelDescriptor,
                    textResolver,
                    typographyResolver,
                    fontResolver),
                new(
                    _inputTabButtonLabel,
                    payload.InputTabLabelDescriptor,
                    textResolver,
                    typographyResolver,
                    fontResolver),
            };

            if (_inputView != null)
            {
                _inputView.BindStaticLocalization(payload, textResolver, typographyResolver, fontResolver);
            }
        }

        public void UnbindStaticLocalization()
        {
            DisposeLocalizedStaticBindings();
            if (_inputView != null)
            {
                _inputView.UnbindStaticLocalization();
            }
        }

        public void ValidateAuthoredStructureOrThrow()
        {
            ValidateSection(_audioView, nameof(_audioView), MissingAudioSectionMessage);
            ValidateSection(_displayView, nameof(_displayView), MissingDisplaySectionMessage);
            ValidateSection(_inputView, nameof(_inputView), MissingInputSectionMessage);
            ValidateDistinctSectionReferences();
        }

        public void SetIsCurrent(bool isCurrent)
        {
            IsVisible = isCurrent;
        }

        public void ClickBack()
        {
            if (!IsVisible)
            {
                return;
            }

            BackRequested?.Invoke();
        }

        public void ClickAudioTab()
        {
            if (!IsVisible)
            {
                return;
            }

            SectionSelected?.Invoke(SettingsSectionId.Audio);
        }

        public void ClickDisplayTab()
        {
            if (!IsVisible)
            {
                return;
            }

            SectionSelected?.Invoke(SettingsSectionId.Display);
        }

        public void ClickInputTab()
        {
            if (!IsVisible)
            {
                return;
            }

            SectionSelected?.Invoke(SettingsSectionId.Input);
        }

        public bool HandleNavigate(UiNavigationCommand command)
        {
            if (!CanHandleUiNavigation)
            {
                return false;
            }

            BuildFocusGraphIfNeeded();
            if (InputView != null && InputView.IsRebindingActive)
            {
                return true;
            }

            return _focusGraph.Navigate(command) != UiFocusMoveResult.NotHandled;
        }

        public bool HandleSubmit()
        {
            if (!CanHandleUiNavigation)
            {
                return false;
            }

            BuildFocusGraphIfNeeded();
            if (InputView != null && InputView.IsRebindingActive)
            {
                return true;
            }

            var currentNode = _focusGraph.CurrentNodeId.Value;
            var result = _focusGraph.Submit();
            if (result == UiFocusMoveResult.EnteredEditMode)
            {
                BeginEditedNode(currentNode);
            }

            if (result == UiFocusMoveResult.ExitedEditMode)
            {
                CommitEditedNode(currentNode);
            }

            return result != UiFocusMoveResult.NotHandled;
        }

        public bool HandleCancel()
        {
            BuildFocusGraphIfNeeded();
            if (InputView != null && InputView.IsRebindingActive)
            {
                return true;
            }

            var currentNode = _focusGraph.CurrentNodeId.Value;
            var result = _focusGraph.Cancel();
            if (result == UiFocusMoveResult.ExitedEditMode)
            {
                CommitEditedNode(currentNode);
                return true;
            }

            if (result == UiFocusMoveResult.ExitedListMode)
            {
                return true;
            }

            if (!IsVisible)
            {
                return false;
            }

            ClickBack();
            return true;
        }

        public void OnNavigationFocusGained()
        {
            BuildFocusGraphIfNeeded();
            _hasNavigationFocus = true;
            _focusGraph.FocusFirst();
        }

        public void OnNavigationFocusLost()
        {
            _hasNavigationFocus = false;
            _focusGraph.HideAllFrames();
        }

        private void OnEnable()
        {
            RebindButton(_audioTabButton, ClickAudioTab);
            RebindButton(_displayTabButton, ClickDisplayTab);
            RebindButton(_inputTabButton, ClickInputTab);
            RebindButton(_backButton, ClickBack);
            _hasNavigationFocus = false;
            BuildFocusGraphIfNeeded();
            RefreshView();
        }

        private void OnDisable()
        {
            StopRootEnterMotion();
            UnbindButton(_audioTabButton, ClickAudioTab);
            UnbindButton(_displayTabButton, ClickDisplayTab);
            UnbindButton(_inputTabButton, ClickInputTab);
            UnbindButton(_backButton, ClickBack);
            _hasNavigationFocus = false;
            _focusGraph.HideAllFrames();
            _focusGraph.Clear();
            _isFocusGraphBuilt = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_root, nameof(_root));
            ValidateSerializedReference(_titleLabel, nameof(_titleLabel));
            ValidateSerializedReference(_audioTabButton, nameof(_audioTabButton));
            ValidateSerializedReference(_audioTabButtonLabel, nameof(_audioTabButtonLabel));
            ValidateSerializedReference(_displayTabButton, nameof(_displayTabButton));
            ValidateSerializedReference(_displayTabButtonLabel, nameof(_displayTabButtonLabel));
            ValidateSerializedReference(_inputTabButton, nameof(_inputTabButton));
            ValidateSerializedReference(_inputTabButtonLabel, nameof(_inputTabButtonLabel));
            ValidateSerializedReference(_backButton, nameof(_backButton));
            ValidateSerializedReference(_backButtonLabel, nameof(_backButtonLabel));
            ValidateSerializedReference(_audioView, nameof(_audioView));
            ValidateSerializedReference(_displayView, nameof(_displayView));
            ValidateSerializedReference(_inputView, nameof(_inputView));
        }
#endif

        private void OnDestroy()
        {
            StopRootEnterMotion();
            UnbindStaticLocalization();
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
            ApplySectionVisibility();

            ApplyRootVisibility();

            if (_viewModel == null)
            {
                return;
            }

            if (!HasLocalizedStaticBindings && _titleLabel != null)
            {
                _titleLabel.text = _viewModel.TitleText;
            }

            if (!HasLocalizedStaticBindings && _backButtonLabel != null)
            {
                _backButtonLabel.text = _viewModel.BackLabel;
            }

            if (!HasLocalizedStaticBindings && _audioTabButtonLabel != null)
            {
                _audioTabButtonLabel.text = _viewModel.AudioTabLabel;
            }

            if (!HasLocalizedStaticBindings && _displayTabButtonLabel != null)
            {
                _displayTabButtonLabel.text = _viewModel.DisplayTabLabel;
            }

            if (!HasLocalizedStaticBindings && _inputTabButtonLabel != null)
            {
                _inputTabButtonLabel.text = _viewModel.InputTabLabel;
            }

            if (_audioTabButton != null)
            {
                _audioTabButton.interactable = _viewModel.SelectedSection != SettingsSectionId.Audio;
            }

            if (_displayTabButton != null)
            {
                _displayTabButton.interactable = _viewModel.SelectedSection != SettingsSectionId.Display;
            }

            if (_inputTabButton != null)
            {
                _inputTabButton.interactable = _viewModel.SelectedSection != SettingsSectionId.Input;
            }

            BuildFocusGraphIfNeeded();
            _focusGraph.SetVisibleContentRegion(ToFocusRegion(_viewModel.SelectedSection));
            if (!_hasNavigationFocus)
            {
                _focusGraph.HideAllFrames();
            }
        }

        private void BuildFocusGraphIfNeeded()
        {
            if (_isFocusGraphBuilt)
            {
                return;
            }

            SettingsFocusGraphBinding.Build(this, _focusGraph);

            _focusGraph.SetVisibleContentRegion(ToFocusRegion(_viewModel != null ? _viewModel.SelectedSection : SettingsSectionId.Audio));
            _isFocusGraphBuilt = true;
            if (!_hasNavigationFocus)
            {
                _focusGraph.HideAllFrames();
            }
        }

        internal void RegisterFocusNode(
            string id,
            UiFocusRegion region,
            UiFocusNodeKind kind,
            int row,
            int column,
            Func<bool> submit,
            Func<int, bool> adjust = null,
            Func<bool> isInteractable = null)
        {
            var slot = FindFocusSlot(id) ?? new UiFocusNodeSlot
            {
                Id = id,
                Region = region,
                Kind = kind,
                Row = row,
                Column = column,
            };
            slot.Id = id;
            slot.Region = region;
            slot.Kind = kind;
            slot.Row = row;
            slot.Column = column;
            _focusGraph.AddNode(slot, new UiDelegateFocusAdapter(submit, adjust, isInteractable));
        }

        internal void RegisterFocusNode(
            string id,
            UiFocusRegion region,
            UiFocusNodeKind kind,
            int row,
            int column,
            IUiFocusableControlAdapter adapter)
        {
            var slot = FindFocusSlot(id) ?? new UiFocusNodeSlot
            {
                Id = id,
                Region = region,
                Kind = kind,
                Row = row,
                Column = column,
            };
            slot.Id = id;
            slot.Region = region;
            slot.Kind = kind;
            slot.Row = row;
            slot.Column = column;
            _focusGraph.AddNode(slot, adapter);
        }

        internal bool SelectSectionFromKeyboard(SettingsSectionId sectionId)
        {
            switch (sectionId)
            {
                case SettingsSectionId.Display:
                    ClickDisplayTab();
                    break;

                case SettingsSectionId.Input:
                    ClickInputTab();
                    break;

                default:
                    ClickAudioTab();
                    break;
            }

            _focusGraph.SetVisibleContentRegion(ToFocusRegion(_viewModel != null ? _viewModel.SelectedSection : sectionId));
            if (!_hasNavigationFocus)
            {
                _focusGraph.HideAllFrames();
            }

            return true;
        }

        private UiFocusNodeSlot FindFocusSlot(string id)
        {
            if (_focusNodeSlots == null)
            {
                return null;
            }

            for (var i = 0; i < _focusNodeSlots.Length; i++)
            {
                if (_focusNodeSlots[i] != null && string.Equals(_focusNodeSlots[i].Id, id, StringComparison.Ordinal))
                {
                    return _focusNodeSlots[i];
                }
            }

            return null;
        }

        internal bool AdjustAudioVolume(AudioSettingsChannel channel, int delta)
        {
            if (!IsVisible || AudioView == null)
            {
                return false;
            }

            var current = AudioView.GetVolume(channel);
            var next = Mathf.Clamp01(current + delta * 0.05f);
            if (Mathf.Approximately(current, next))
            {
                return false;
            }

            AudioView.SetVolume(channel, next);
            return true;
        }

        internal bool AdjustResolution(int delta)
        {
            if (!IsVisible || DisplayView == null || DisplayView.ResolutionOptionCount <= 0)
            {
                return false;
            }

            var next = Mathf.Clamp(
                DisplayView.SelectedResolutionIndex + delta,
                0,
                DisplayView.ResolutionOptionCount - 1);
            DisplayView.SelectResolution(next);
            return true;
        }

        private void CommitEditedNode(string nodeId)
        {
            SettingsFocusGraphBinding.CommitEditedNode(this, nodeId);
        }

        private void BeginEditedNode(string nodeId)
        {
            SettingsFocusGraphBinding.BeginEditedNode(this, nodeId);
        }

        private static UiFocusRegion ToFocusRegion(SettingsSectionId sectionId)
        {
            switch (sectionId)
            {
                case SettingsSectionId.Display:
                    return UiFocusRegion.Display;

                case SettingsSectionId.Input:
                    return UiFocusRegion.Input;

                default:
                    return UiFocusRegion.Audio;
            }
        }

        internal static bool InvokeAndReturnTrue(Action action)
        {
            action?.Invoke();
            return true;
        }

        private void ApplySectionVisibility()
        {
            var selectedSection = _viewModel != null ? _viewModel.SelectedSection : SettingsSectionId.Audio;
            ApplySectionVisibility(_audioView, IsVisible && selectedSection == SettingsSectionId.Audio);
            ApplySectionVisibility(_displayView, IsVisible && selectedSection == SettingsSectionId.Display);
            ApplySectionVisibility(_inputView, IsVisible && selectedSection == SettingsSectionId.Input);
        }

        private static void ApplySectionVisibility(Component sectionView, bool isVisible)
        {
            if (sectionView == null)
            {
                return;
            }

            if (isVisible && !sectionView.gameObject.activeSelf)
            {
                sectionView.gameObject.SetActive(true);
            }

            if (sectionView is SettingsAudioView audioView)
            {
                audioView.SetIsVisible(isVisible);
            }
            else if (sectionView is SettingsDisplayView displayView)
            {
                displayView.SetIsVisible(isVisible);
            }
            else if (sectionView is SettingsInputView inputView)
            {
                inputView.SetIsVisible(isVisible);
            }

            if (!isVisible && sectionView.gameObject.activeSelf)
            {
                sectionView.gameObject.SetActive(false);
            }
        }

        private void ApplyRootVisibility()
        {
            var becameVisible = !_lastVisibleState && IsVisible;
            var becameHidden = _lastVisibleState && !IsVisible;

            if (becameVisible)
            {
                _lastVisibleState = true;
                if (_root != null)
                {
                    _root.SetActive(true);
                }

                PlayRootEnterMotion();
                return;
            }

            if (becameHidden || !IsVisible)
            {
                StopRootEnterMotion();
            }

            if (_root != null)
            {
                _root.SetActive(IsVisible);
            }

            _lastVisibleState = IsVisible;
        }

        private void PlayRootEnterMotion()
        {
            _rootCanvasGroup = ScreenEnterTweenUtility.EnsureCanvasGroup(_root, _rootCanvasGroup);
            ScreenEnterTweenUtility.Kill(ref _enterTween);
            _enterTween = ScreenEnterTweenUtility.PlayEnterFade(_rootCanvasGroup, out _rootRestAlpha);
            _hasRootRestAlpha = _rootCanvasGroup != null;
        }

        private void StopRootEnterMotion()
        {
            ScreenEnterTweenUtility.Kill(ref _enterTween);
            if (_hasRootRestAlpha)
            {
                ScreenEnterTweenUtility.RestoreAlpha(_rootCanvasGroup, _rootRestAlpha);
            }
        }

        private bool HasLocalizedStaticBindings =>
            _localizedStaticBindings != null && _localizedStaticBindings.Count > 0;

        private void DisposeLocalizedStaticBindings()
        {
            if (_localizedStaticBindings == null)
            {
                return;
            }

            for (var i = 0; i < _localizedStaticBindings.Count; i++)
            {
                _localizedStaticBindings[i]?.Dispose();
            }

            _localizedStaticBindings = null;
        }

        private void ValidateSection(Component sectionView, string fieldName, string baseMessage)
        {
            var issues = new List<string>();
            var expectedRoot = _root != null ? _root.transform : transform;

            if (sectionView == null)
            {
                issues.Add($"serialized reference '{fieldName}' is not assigned");
                throw new InvalidOperationException(BuildValidationMessage(baseMessage, issues));
            }

            if (!sectionView.transform.IsChildOf(expectedRoot))
            {
                issues.Add($"serialized reference '{fieldName}' must remain under '{expectedRoot.name}' settings shell");
            }

            if (issues.Count > 0)
            {
                throw new InvalidOperationException(BuildValidationMessage(baseMessage, issues));
            }
        }

        private void ValidateDistinctSectionReferences()
        {
            if (_audioView == null || _displayView == null || _inputView == null)
            {
                return;
            }

            var audioSection = (Component)_audioView;
            var displaySection = (Component)_displayView;
            var inputSection = (Component)_inputView;
            if (audioSection == displaySection || audioSection == inputSection || displaySection == inputSection)
            {
                throw new InvalidOperationException(
                    BuildValidationMessage(
                        "Settings screen has duplicate section references.",
                        new[]
                        {
                            "audio, display, and input section serialized references must point to distinct section components",
                        }));
            }
        }

        private static string BuildValidationMessage(string baseMessage, IReadOnlyList<string> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                return baseMessage;
            }

            return $"{baseMessage} Details: {string.Join("; ", issues)}.";
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
                Debug.LogWarning($"{nameof(SettingsScreenView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif
    }
}
