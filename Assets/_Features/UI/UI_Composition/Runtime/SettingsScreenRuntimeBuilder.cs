using System;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    internal sealed class SettingsScreenRuntimeBuildContext
    {
        public SettingsScreenRuntimeBuildContext(
            Transform parent,
            SettingsScreenView prefab,
            IAudioSettingsPort audioSettingsPort,
            IDisplaySettingsPort displaySettingsPort,
            IKeyboardBindingSettingsPort keyboardBindingSettingsPort,
            IUiAudioPort uiAudioPort,
            DisplayPreviewSessionHost displayPreviewSessionHost,
            DisplaySettingsLifecycleRelay displaySettingsLifecycleRelay,
            DisplayStatusTransientRelay displayStatusTransientRelay = null,
            ILocalizedTextResolver localizedTextResolver = null,
            ILocalizedTypographyResolver localizedTypographyResolver = null,
            ILocalizedTmpFontResolver localizedTmpFontResolver = null,
            GameplayUiTypographyTheme typographyTheme = null,
            IUiLocaleSelectionPort localeSelectionPort = null)
        {
            Parent = parent ?? throw new ArgumentNullException(nameof(parent));
            Prefab = prefab ?? throw new ArgumentNullException(nameof(prefab));
            AudioSettingsPort = audioSettingsPort ?? throw new ArgumentNullException(nameof(audioSettingsPort));
            DisplaySettingsPort = displaySettingsPort ?? throw new ArgumentNullException(nameof(displaySettingsPort));
            KeyboardBindingSettingsPort = keyboardBindingSettingsPort ?? NoOpKeyboardBindingSettingsPort.Instance;
            UiAudioPort = uiAudioPort ?? throw new ArgumentNullException(nameof(uiAudioPort));
            DisplayPreviewSessionHost = displayPreviewSessionHost ?? throw new ArgumentNullException(nameof(displayPreviewSessionHost));
            DisplaySettingsLifecycleRelay = displaySettingsLifecycleRelay ?? throw new ArgumentNullException(nameof(displaySettingsLifecycleRelay));
            DisplayStatusTransientRelay = displayStatusTransientRelay;
            LocalizedTextResolver = localizedTextResolver
                ?? throw new InvalidOperationException(
                    "SettingsScreenRuntimeBuildContext requires an explicit production localized text resolver.");
            LocalizedTypographyResolver = localizedTypographyResolver ?? DefaultLocalizedTypographyResolver.Instance;
            LocalizedTmpFontResolver = localizedTmpFontResolver;
            TypographyTheme = typographyTheme;
            LocaleSelectionPort = localeSelectionPort ?? LocalizedTextResolver as IUiLocaleSelectionPort;
        }

        public Transform Parent { get; }

        public SettingsScreenView Prefab { get; }

        public IAudioSettingsPort AudioSettingsPort { get; }

        public IDisplaySettingsPort DisplaySettingsPort { get; }

        public IKeyboardBindingSettingsPort KeyboardBindingSettingsPort { get; }

        public IUiAudioPort UiAudioPort { get; }

        public DisplayPreviewSessionHost DisplayPreviewSessionHost { get; }

        public DisplaySettingsLifecycleRelay DisplaySettingsLifecycleRelay { get; }

        public DisplayStatusTransientRelay DisplayStatusTransientRelay { get; }

        public ILocalizedTextResolver LocalizedTextResolver { get; }

        public ILocalizedTypographyResolver LocalizedTypographyResolver { get; }

        public ILocalizedTmpFontResolver LocalizedTmpFontResolver { get; }

        public GameplayUiTypographyTheme TypographyTheme { get; }

        public IUiLocaleSelectionPort LocaleSelectionPort { get; }
    }

    internal sealed class SettingsScreenRuntimeBuilder
    {
        public IScreenRuntime Build(SettingsScreenRuntimeBuildContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var view = InstantiateSettingsPrefab(context.Prefab, context.Parent);
            view.ValidateAuthoredStructureOrThrow();
            view.AudioView.ValidateAuthoredControlsOrThrow();
            view.DisplayView.ValidateAuthoredControlsOrThrow();
            view.InputView.ValidateAuthoredControlsOrThrow();

            var presenter = new SettingsScreenPresenter(
                context.AudioSettingsPort,
                context.DisplaySettingsPort,
                context.KeyboardBindingSettingsPort,
                context.LocalizedTextResolver,
                context.LocaleSelectionPort);
            view.Bind(presenter.ViewModel);
            view.AudioView.Bind(presenter.AudioPresenter.ViewModel);
            view.DisplayView.Bind(presenter.DisplayPresenter.ViewModel);
            view.InputView.Bind(presenter.InputPresenter.ViewModel);
            view.SetIsCurrent(false);

            return new SettingsRuntime(
                view,
                presenter,
                context.UiAudioPort,
                context.DisplayPreviewSessionHost,
                context.DisplaySettingsLifecycleRelay,
                context.DisplayStatusTransientRelay ?? view.gameObject.AddComponent<DisplayStatusTransientRelay>(),
                context.LocalizedTextResolver,
                context.LocalizedTypographyResolver,
                context.LocalizedTmpFontResolver,
                context.TypographyTheme,
                context.LocaleSelectionPort,
                () => DestroyObject(view.gameObject));
        }

        private static SettingsScreenView InstantiateSettingsPrefab(SettingsScreenView prefab, Transform parent)
        {
            var instantiatedRoot = UnityEngine.Object.Instantiate(prefab.gameObject, parent, false);
            if (instantiatedRoot == null)
            {
                throw new InvalidOperationException(
                    $"Screen prefab instantiation returned null for screen '{ScreenId.Settings}'.");
            }

            var view = instantiatedRoot.GetComponent<SettingsScreenView>();
            if (view == null)
            {
                throw new InvalidOperationException(
                    $"Screen prefab for '{ScreenId.Settings}' is missing the expected root view component '{nameof(SettingsScreenView)}'.");
            }

            if (view.transform.parent != parent)
            {
                throw new InvalidOperationException(
                    $"Screen '{ScreenId.Settings}' must mount directly beneath ScreenLayer content root.");
            }

            return view;
        }

        private static void DestroyObject(UnityEngine.Object unityObject)
        {
            if (unityObject == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                UnityEngine.Object.Destroy(unityObject);
                return;
            }

            UnityEngine.Object.DestroyImmediate(unityObject);
        }

        private sealed class SettingsRuntime : IScreenRuntime, IUiNavigationTargetProvider
        {
            private const double DisplayStatusTransientSeconds = 2d;
            private readonly Action _dispose;
            private readonly SettingsAudioView _audioView;
            private readonly DisplayPreviewSessionHost _displayPreviewSessionHost;
            private readonly SettingsDisplayView _displayView;
            private readonly SettingsInputView _inputView;
            private readonly DisplaySettingsLifecycleRelay _displaySettingsLifecycleRelay;
            private readonly DisplayStatusTransientRelay _displayStatusTransientRelay;
            private readonly ILocalizedTextResolver _localizedTextResolver;
            private readonly ILocalizedTypographyResolver _localizedTypographyResolver;
            private readonly ILocalizedTmpFontResolver _localizedTmpFontResolver;
            private readonly GameplayUiTypographyTheme _typographyTheme;
            private readonly IUiLocaleSelectionPort _localeSelectionPort;
            private readonly SettingsScreenPresenter _presenter;
            private readonly IUiAudioPort _uiAudioPort;
            private readonly SettingsScreenView _view;
            private bool _isCurrent;

            public SettingsRuntime(
                SettingsScreenView view,
                SettingsScreenPresenter presenter,
                IUiAudioPort uiAudioPort,
                DisplayPreviewSessionHost displayPreviewSessionHost,
                DisplaySettingsLifecycleRelay displaySettingsLifecycleRelay,
                DisplayStatusTransientRelay displayStatusTransientRelay,
                ILocalizedTextResolver localizedTextResolver,
                ILocalizedTypographyResolver localizedTypographyResolver,
                ILocalizedTmpFontResolver localizedTmpFontResolver,
                GameplayUiTypographyTheme typographyTheme,
                IUiLocaleSelectionPort localeSelectionPort,
                Action dispose)
            {
                _view = view ?? throw new ArgumentNullException(nameof(view));
                _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
                _uiAudioPort = uiAudioPort ?? throw new ArgumentNullException(nameof(uiAudioPort));
                _audioView = view.AudioView ?? throw new ArgumentNullException(nameof(view.AudioView));
                _displayView = view.DisplayView ?? throw new ArgumentNullException(nameof(view.DisplayView));
                _inputView = view.InputView ?? throw new ArgumentNullException(nameof(view.InputView));
                _displayPreviewSessionHost = displayPreviewSessionHost ?? throw new ArgumentNullException(nameof(displayPreviewSessionHost));
                _displaySettingsLifecycleRelay = displaySettingsLifecycleRelay ?? throw new ArgumentNullException(nameof(displaySettingsLifecycleRelay));
                _displayStatusTransientRelay = displayStatusTransientRelay ?? throw new ArgumentNullException(nameof(displayStatusTransientRelay));
                _localizedTextResolver = localizedTextResolver ?? throw new ArgumentNullException(nameof(localizedTextResolver));
                _localizedTypographyResolver = localizedTypographyResolver ?? DefaultLocalizedTypographyResolver.Instance;
                _localizedTmpFontResolver = localizedTmpFontResolver;
                _typographyTheme = typographyTheme;
                _localeSelectionPort = localeSelectionPort;
                _dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));

                _audioView.VolumeChanged += HandleAudioVolumeChanged;
                _audioView.MuteChanged += HandleAudioMuteChanged;
                _audioView.InteractionCompleted += HandleAudioInteractionCompleted;
                _displayView.ResolutionChanged += HandleDisplayResolutionChanged;
                _displayView.FullscreenToggled += HandleDisplayFullscreenToggled;
                _displayView.ApplyRequested += HandleDisplayApplyRequested;
                _displayView.RevertRequested += HandleDisplayRevertRequested;
                _displayView.LanguageCycleRequested += HandleLanguageCycleRequested;
                _inputView.MovementSchemeToggleRequested += HandleInputMovementSchemeToggleRequested;
                _inputView.PushRebindRequested += HandleInputPushRebindRequested;
                _inputView.FlipRebindRequested += HandleInputFlipRebindRequested;
                _inputView.ResetRequested += HandleInputResetRequested;
                _presenter.InputPresenter.RebindCompleted += HandleInputRebindCompleted;
                view.SectionSelected += HandleSectionSelected;
                view.BackRequested += HandleBackRequested;
                _displaySettingsLifecycleRelay.ResyncRequested += HandleDisplayResyncRequested;
                _displayPreviewSessionHost.CountdownChanged += HandleDisplayPreviewCountdownChanged;
            }

            public event Action<ScreenAction> ActionRequested;

            public void ApplyPayload(IScreenPayload payload)
            {
                ExpectPayload<SettingsScreenPayload>(payload);
                var settingsPayload = (SettingsScreenPayload)payload;
                _presenter.Apply(settingsPayload, _displayPreviewSessionHost.PreviewTimeoutSeconds);
                _view.BindStaticLocalization(
                    settingsPayload,
                    _localizedTextResolver,
                    _localizedTypographyResolver,
                    _localizedTmpFontResolver,
                    _typographyTheme);
            }

            public void Dispose()
            {
                _displayPreviewSessionHost.CancelActivePreview();
                CancelDisplayStatusAutoHide();
                _presenter.DisplayPresenter.ClearPreviewCountdown();
                _presenter.AudioPresenter.Flush();
                _presenter.InputPresenter.CancelRebind();
                _audioView.VolumeChanged -= HandleAudioVolumeChanged;
                _audioView.MuteChanged -= HandleAudioMuteChanged;
                _audioView.InteractionCompleted -= HandleAudioInteractionCompleted;
                _displayView.ResolutionChanged -= HandleDisplayResolutionChanged;
                _displayView.FullscreenToggled -= HandleDisplayFullscreenToggled;
                _displayView.ApplyRequested -= HandleDisplayApplyRequested;
                _displayView.RevertRequested -= HandleDisplayRevertRequested;
                _displayView.LanguageCycleRequested -= HandleLanguageCycleRequested;
                _inputView.MovementSchemeToggleRequested -= HandleInputMovementSchemeToggleRequested;
                _inputView.PushRebindRequested -= HandleInputPushRebindRequested;
                _inputView.FlipRebindRequested -= HandleInputFlipRebindRequested;
                _inputView.ResetRequested -= HandleInputResetRequested;
                _presenter.InputPresenter.RebindCompleted -= HandleInputRebindCompleted;
                _view.SectionSelected -= HandleSectionSelected;
                _view.BackRequested -= HandleBackRequested;
                _displaySettingsLifecycleRelay.ResyncRequested -= HandleDisplayResyncRequested;
                _displayPreviewSessionHost.CountdownChanged -= HandleDisplayPreviewCountdownChanged;
                _view.UnbindStaticLocalization();
                _displayView.Bind(null);
                _inputView.Bind(null);
                _audioView.Bind(null);
                _view.Bind(null);
                _view.SetIsCurrent(false);
                _dispose();
            }

            public void SetIsCurrent(bool isCurrent)
            {
                if (_isCurrent && !isCurrent)
                {
                    _displayPreviewSessionHost.CancelActivePreview();
                    CancelDisplayStatusAutoHide();
                    _presenter.DisplayPresenter.ClearPreviewCountdown();
                    _presenter.AudioPresenter.Flush();
                    _presenter.InputPresenter.CancelRebind();
                }

                _isCurrent = isCurrent;
                _view.SetIsCurrent(isCurrent);

                if (isCurrent)
                {
                    CancelDisplayStatusAutoHide();
                    _presenter.DisplayPresenter.ResyncState(_displayPreviewSessionHost.PreviewTimeoutSeconds);
                }
            }

            public bool TryGetNavigationTarget(out IUiNavigationTarget target)
            {
                target = _view;
                return target != null;
            }

            private void HandleSectionSelected(SettingsSectionId sectionId)
            {
                if (_presenter.SelectSection(sectionId))
                {
                    PlayLocalCue(UiAudioCueId.Select);
                }
            }

            private void HandleInputMovementSchemeToggleRequested(bool useArrowKeys)
            {
                _presenter.InputPresenter.SetMovementScheme(useArrowKeys
                    ? KeyboardMovementScheme.ArrowKeys
                    : KeyboardMovementScheme.Wasd);
                PlayLocalCue(UiAudioCueId.Toggle);
            }

            private void HandleInputPushRebindRequested()
            {
                _presenter.InputPresenter.StartRebind(KeyboardBindableAction.Push);
                PlayLocalCue(UiAudioCueId.Select);
            }

            private void HandleInputFlipRebindRequested()
            {
                _presenter.InputPresenter.StartRebind(KeyboardBindableAction.Flip);
                PlayLocalCue(UiAudioCueId.Select);
            }

            private void HandleInputRebindCompleted(KeyboardRebindResult result)
            {
                if (result.ValidationResult == KeyboardBindingValidationResult.Success)
                {
                    PlayLocalCue(UiAudioCueId.Confirm);
                }
            }

            private void HandleInputResetRequested()
            {
                RaiseAction(ScreenAction.Popup(new PopupRequest(
                    PopupId.Confirm,
                    new ConfirmPopupPayload(
                        "Reset Input Settings",
                        "Reset input settings to defaults?",
                        "Reset",
                        "Cancel",
                        false),
                    completion =>
                    {
                        if (completion.CompletionKind == PopupCompletionKind.Confirmed)
                        {
                            _presenter.InputPresenter.ResetToDefaults();
                        }
                    })));
            }

            private void HandleAudioVolumeChanged(AudioSettingsChannel channel, float value)
            {
                _presenter.AudioPresenter.SetVolume(channel, value);
            }

            private void HandleAudioMuteChanged(AudioSettingsChannel channel, bool isMuted)
            {
                _presenter.AudioPresenter.SetMuted(channel, isMuted);
                PlayLocalCue(UiAudioCueId.Toggle);
            }

            private void HandleAudioInteractionCompleted()
            {
                _presenter.AudioPresenter.Flush();
                PlayLocalCue(UiAudioCueId.AdjustValueCommit);
            }

            private void HandleDisplayResolutionChanged(int modeIndex)
            {
                CancelDisplayStatusAutoHide();
                _presenter.DisplayPresenter.StageResolution(modeIndex);
                PlayLocalCue(UiAudioCueId.Select);
            }

            private void HandleDisplayFullscreenToggled(bool isFullscreen)
            {
                CancelDisplayStatusAutoHide();
                _presenter.DisplayPresenter.StageWindowMode(isFullscreen
                    ? DisplayWindowMode.FullScreenWindow
                    : DisplayWindowMode.Windowed);
                PlayLocalCue(UiAudioCueId.Toggle);
            }

            private void HandleDisplayApplyRequested()
            {
                CancelDisplayStatusAutoHide();
                if (!_presenter.DisplayPresenter.ApplyStagedSettings(_displayPreviewSessionHost.PreviewTimeoutSeconds))
                {
                    return;
                }

                if (!_displayPreviewSessionHost.TryOpen(
                        BuildDisplayPreviewConfirmPayload(),
                        HandleDisplayPreviewConfirmed,
                        HandleDisplayPreviewCancelled))
                {
                    _presenter.DisplayPresenter.CancelPreview();
                    _presenter.DisplayPresenter.ClearPreviewCountdown();
                    ScheduleDisplayStatusAutoHideIfNeeded();
                }
            }

            private void HandleDisplayRevertRequested()
            {
                CancelDisplayStatusAutoHide();
                _presenter.DisplayPresenter.ResetStagedToCurrent();
                PlayLocalCue(UiAudioCueId.Cancel);
            }

            private void HandleLanguageCycleRequested()
            {
                if (_localeSelectionPort == null || !_presenter.SelectNextLocale())
                {
                    return;
                }

                PlayLocalCue(UiAudioCueId.Toggle);
            }

            private void HandleDisplayPreviewConfirmed()
            {
                _presenter.DisplayPresenter.ConfirmPreview();
                ScheduleDisplayStatusAutoHideIfNeeded();
            }

            private void HandleDisplayPreviewCancelled()
            {
                _presenter.DisplayPresenter.CancelPreview();
                ScheduleDisplayStatusAutoHideIfNeeded();
            }

            private void HandleDisplayResyncRequested()
            {
                if (_isCurrent)
                {
                    CancelDisplayStatusAutoHide();
                    _presenter.DisplayPresenter.ResyncState(_displayPreviewSessionHost.PreviewTimeoutSeconds);
                }
            }

            private void HandleDisplayPreviewCountdownChanged(DisplayPreviewCountdownSnapshot snapshot)
            {
                if (_isCurrent)
                {
                    _presenter.DisplayPresenter.SetPreviewCountdown(snapshot);
                    return;
                }

                _presenter.DisplayPresenter.ClearPreviewCountdown();
            }

            private void ScheduleDisplayStatusAutoHideIfNeeded()
            {
                CancelDisplayStatusAutoHide();
                if (!_isCurrent || !_presenter.DisplayPresenter.ViewModel.IsDisplayStatusTransient)
                {
                    return;
                }

                _displayStatusTransientRelay.Arm(
                    DisplayStatusTransientSeconds,
                    () =>
                    {
                        if (_isCurrent)
                        {
                            _presenter.DisplayPresenter.ClearTransientDisplayStatus(
                                _displayPreviewSessionHost.PreviewTimeoutSeconds);
                        }
                    });
            }

            private void CancelDisplayStatusAutoHide()
            {
                _displayStatusTransientRelay.Cancel();
            }

            private void HandleBackRequested()
            {
                if (_presenter.InputPresenter.IsRebinding)
                {
                    _presenter.InputPresenter.CancelRebind();
                    return;
                }

                RaiseAction(ScreenAction.Back());
            }

            private ConfirmPopupPayload BuildDisplayPreviewConfirmPayload()
            {
                var displayViewModel = _presenter.DisplayPresenter.ViewModel;
                var selectedIndex = Mathf.Clamp(
                    displayViewModel.SelectedResolutionIndex,
                    0,
                    Mathf.Max(0, displayViewModel.ResolutionOptionTexts.Count - 1));
                var resolutionLabel = displayViewModel.ResolutionOptionTexts.Count == 0
                    ? displayViewModel.CurrentDisplayValueText
                    : displayViewModel.ResolutionOptionTexts[selectedIndex];
                var windowModeText = displayViewModel.IsFullscreenEnabled
                    ? "Fullscreen Window"
                    : "Windowed";
                var visibleTimeoutSeconds = DisplayPreviewCountdownSnapshot.ComputeVisibleSeconds(
                    _displayPreviewSessionHost.PreviewTimeoutSeconds,
                    _displayPreviewSessionHost.PreviewTimeoutSeconds);

                return new ConfirmPopupPayload(
                    "Confirm Display Preview",
                    $"Preview {resolutionLabel} in {windowModeText}. These changes are temporary and will revert in {visibleTimeoutSeconds} seconds unless you confirm.",
                    "Keep",
                    "Revert",
                    false);
            }

            private void RaiseAction(ScreenAction action)
            {
                ActionRequested?.Invoke(action);
            }

            private void PlayLocalCue(UiAudioCueId cueId)
            {
                _uiAudioPort.Play(cueId);
            }

            private static void ExpectPayload<TPayload>(IScreenPayload payload) where TPayload : class, IScreenPayload
            {
                if (payload is not TPayload)
                {
                    throw new InvalidOperationException($"Screen payload type mismatch. Expected {typeof(TPayload).Name}.");
                }
            }
        }
    }
}
