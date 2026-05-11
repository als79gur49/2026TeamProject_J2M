using System;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    internal sealed class MainMenuSettingsRuntime : IDisposable
    {
        private readonly AccessibilitySettingsStore accessibilitySettingsStore;
        private readonly IAudioSettingsPort audioSettingsPort;
        private readonly IDisplaySettingsPort displaySettingsPort;
        private readonly IKeyboardBindingSettingsPort keyboardBindingSettingsPort;
        private readonly DisplayPreviewSessionHost displayPreviewSessionHost;
        private readonly DisplaySettingsLifecycleRelay displaySettingsLifecycleRelay;
        private DisplayStatusTransientRelay displayStatusTransientRelay;
        private readonly SettingsScreenPayload payload;
        private readonly PopupController popupController;
        private readonly double previewTimeoutSeconds;
        private readonly SettingsScreenView settingsScreenPrefab;
        private readonly RectTransform settingsContentRoot;
        private const double DisplayStatusTransientSeconds = 2d;
        private bool isDisposed;
        private SettingsAudioView audioView;
        private SettingsDisplayView displayView;
        private SettingsInputView inputView;
        private SettingsScreenPresenter presenter;
        private SettingsScreenView view;

        public MainMenuSettingsRuntime(
            SettingsScreenView settingsScreenPrefab,
            RectTransform settingsContentRoot,
            AccessibilitySettingsStore accessibilitySettingsStore,
            IAudioSettingsPort audioSettingsPort,
            IDisplaySettingsPort displaySettingsPort,
            IKeyboardBindingSettingsPort keyboardBindingSettingsPort,
            PopupController popupController,
            DisplayPreviewSessionHost displayPreviewSessionHost,
            DisplaySettingsLifecycleRelay displaySettingsLifecycleRelay,
            SettingsScreenPayload payload,
            double previewTimeoutSeconds,
            DisplayStatusTransientRelay displayStatusTransientRelay = null)
        {
            this.settingsScreenPrefab = settingsScreenPrefab != null
                ? settingsScreenPrefab
                : throw new ArgumentNullException(nameof(settingsScreenPrefab));
            this.settingsContentRoot = settingsContentRoot != null
                ? settingsContentRoot
                : throw new ArgumentNullException(nameof(settingsContentRoot));
            this.accessibilitySettingsStore = accessibilitySettingsStore ?? throw new ArgumentNullException(nameof(accessibilitySettingsStore));
            this.audioSettingsPort = audioSettingsPort ?? throw new ArgumentNullException(nameof(audioSettingsPort));
            this.displaySettingsPort = displaySettingsPort ?? throw new ArgumentNullException(nameof(displaySettingsPort));
            this.keyboardBindingSettingsPort = keyboardBindingSettingsPort ?? throw new ArgumentNullException(nameof(keyboardBindingSettingsPort));
            this.popupController = popupController ?? throw new ArgumentNullException(nameof(popupController));
            this.displayPreviewSessionHost = displayPreviewSessionHost ?? throw new ArgumentNullException(nameof(displayPreviewSessionHost));
            this.displaySettingsLifecycleRelay = displaySettingsLifecycleRelay ?? throw new ArgumentNullException(nameof(displaySettingsLifecycleRelay));
            this.displayStatusTransientRelay = displayStatusTransientRelay;
            this.payload = payload ?? throw new ArgumentNullException(nameof(payload));
            this.previewTimeoutSeconds = previewTimeoutSeconds;
        }

        public event Action CloseRequested;

        public event Action<SettingsSectionId> SectionChanged;

        public bool IsOpen => view != null && !isDisposed;

        public SettingsScreenView View => view;

        public SettingsScreenPresenter Presenter => presenter;

        public MainMenuSettingsRuntime(
            SettingsScreenView settingsScreenPrefab,
            RectTransform settingsContentRoot,
            AccessibilitySettingsStore accessibilitySettingsStore,
            IAudioSettingsPort audioSettingsPort,
            IDisplaySettingsPort displaySettingsPort,
            PopupController popupController,
            DisplayPreviewSessionHost displayPreviewSessionHost,
            DisplaySettingsLifecycleRelay displaySettingsLifecycleRelay,
            SettingsScreenPayload payload,
            double previewTimeoutSeconds,
            DisplayStatusTransientRelay displayStatusTransientRelay = null)
            : this(
                settingsScreenPrefab,
                settingsContentRoot,
                accessibilitySettingsStore,
                audioSettingsPort,
                displaySettingsPort,
                NoOpKeyboardBindingSettingsPort.Instance,
                popupController,
                displayPreviewSessionHost,
                displaySettingsLifecycleRelay,
                payload,
                previewTimeoutSeconds,
                displayStatusTransientRelay)
        {
        }

        public void Open()
        {
            ThrowIfDisposed();
            if (view != null)
            {
                view.SetIsCurrent(true);
                return;
            }

            var instantiatedRoot = UnityEngine.Object.Instantiate(settingsScreenPrefab.gameObject, settingsContentRoot, false);
            if (instantiatedRoot == null)
            {
                throw new InvalidOperationException("Settings screen prefab instantiation returned null.");
            }

            view = instantiatedRoot.GetComponent<SettingsScreenView>();
            if (view == null)
                {
                    DestroyObject(instantiatedRoot);
                    throw new InvalidOperationException(
                        $"Settings screen prefab is missing the expected root component '{nameof(SettingsScreenView)}'.");
                }

            try
            {
                view.name = settingsScreenPrefab.name;
                displayStatusTransientRelay ??= view.gameObject.AddComponent<DisplayStatusTransientRelay>();
                audioView = view.AudioView ?? throw new InvalidOperationException("Settings screen view is missing an audio section.");
                displayView = view.DisplayView ?? throw new InvalidOperationException("Settings screen view is missing a display section.");
                inputView = view.InputView ?? throw new InvalidOperationException("Settings screen view is missing an input section.");
                presenter = new SettingsScreenPresenter(accessibilitySettingsStore, audioSettingsPort, displaySettingsPort, keyboardBindingSettingsPort);
                view.ValidateAuthoredStructureOrThrow();
                audioView.ValidateAuthoredControlsOrThrow();
                displayView.ValidateAuthoredControlsOrThrow();
                inputView.ValidateAuthoredControlsOrThrow();
                presenter.Apply(payload, previewTimeoutSeconds);
                view.Bind(presenter.ViewModel);
                audioView.Bind(presenter.AudioPresenter.ViewModel);
                displayView.Bind(presenter.DisplayPresenter.ViewModel);
                inputView.Bind(presenter.InputPresenter.ViewModel);
                SubscribeEvents();
                view.SetIsCurrent(true);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            CancelDisplayPreviewOnClose();
            CancelDisplayStatusAutoHide();
            presenter?.InputPresenter.CancelRebind();

            if (presenter != null)
            {
                presenter.DisplayPresenter.ClearPreviewCountdown();
                presenter.AudioPresenter.Flush();
            }

            UnsubscribeEvents();

            if (displayView != null)
            {
                displayView.Bind(null);
            }

            if (inputView != null)
            {
                inputView.Bind(null);
            }

            if (audioView != null)
            {
                audioView.Bind(null);
            }

            if (view != null)
            {
                view.Bind(null);
                view.SetIsCurrent(false);
                DestroyObject(view.gameObject);
            }

            displayView = null;
            inputView = null;
            audioView = null;
            presenter = null;
            view = null;
        }

        public bool TryHandleBackRequested()
        {
            if (!IsOpen)
            {
                return false;
            }

            if (presenter != null && presenter.InputPresenter.IsRebinding)
            {
                presenter.InputPresenter.CancelRebind();
                return true;
            }

            CloseRequested?.Invoke();
            return true;
        }

        private void SubscribeEvents()
        {
            audioView.VolumeChanged += HandleAudioVolumeChanged;
            audioView.MuteChanged += HandleAudioMuteChanged;
            audioView.InteractionCompleted += HandleAudioInteractionCompleted;
            displayView.ResolutionChanged += HandleDisplayResolutionChanged;
            displayView.FullscreenToggled += HandleDisplayFullscreenToggled;
            displayView.ApplyRequested += HandleDisplayApplyRequested;
            displayView.RevertRequested += HandleDisplayRevertRequested;
            inputView.MovementSchemeToggleRequested += HandleInputMovementSchemeToggleRequested;
            inputView.PushRebindRequested += HandleInputPushRebindRequested;
            inputView.FlipRebindRequested += HandleInputFlipRebindRequested;
            inputView.ResetRequested += HandleInputResetRequested;
            view.SectionSelected += HandleSectionSelected;
            view.BackRequested += HandleBackRequested;
            displaySettingsLifecycleRelay.ResyncRequested += HandleDisplayResyncRequested;
            displayPreviewSessionHost.CountdownChanged += HandleDisplayPreviewCountdownChanged;
        }

        private void UnsubscribeEvents()
        {
            if (audioView != null)
            {
                audioView.VolumeChanged -= HandleAudioVolumeChanged;
                audioView.MuteChanged -= HandleAudioMuteChanged;
                audioView.InteractionCompleted -= HandleAudioInteractionCompleted;
            }

            if (displayView != null)
            {
                displayView.ResolutionChanged -= HandleDisplayResolutionChanged;
                displayView.FullscreenToggled -= HandleDisplayFullscreenToggled;
                displayView.ApplyRequested -= HandleDisplayApplyRequested;
                displayView.RevertRequested -= HandleDisplayRevertRequested;
            }

            if (inputView != null)
            {
                inputView.MovementSchemeToggleRequested -= HandleInputMovementSchemeToggleRequested;
                inputView.PushRebindRequested -= HandleInputPushRebindRequested;
                inputView.FlipRebindRequested -= HandleInputFlipRebindRequested;
                inputView.ResetRequested -= HandleInputResetRequested;
            }

            if (view != null)
            {
                view.SectionSelected -= HandleSectionSelected;
                view.BackRequested -= HandleBackRequested;
            }

            displaySettingsLifecycleRelay.ResyncRequested -= HandleDisplayResyncRequested;
            displayPreviewSessionHost.CountdownChanged -= HandleDisplayPreviewCountdownChanged;
        }

        private void CancelDisplayPreviewOnClose()
        {
            if (presenter == null)
            {
                return;
            }

            if (displayPreviewSessionHost.CancelActivePreview())
            {
                return;
            }

            if (presenter.DisplayPresenter.ViewModel.IsDisplayPreviewActive)
            {
                presenter.DisplayPresenter.CancelPreview();
            }
        }

        private void HandleSectionSelected(SettingsSectionId sectionId)
        {
            if (presenter.SelectSection(sectionId))
            {
                SectionChanged?.Invoke(sectionId);
            }
        }

        private void HandleInputMovementSchemeToggleRequested(bool useArrowKeys)
        {
            presenter.InputPresenter.SetMovementScheme(useArrowKeys
                ? KeyboardMovementScheme.ArrowKeys
                : KeyboardMovementScheme.Wasd);
        }

        private void HandleInputPushRebindRequested()
        {
            presenter.InputPresenter.StartRebind(KeyboardBindableAction.Push);
        }

        private void HandleInputFlipRebindRequested()
        {
            presenter.InputPresenter.StartRebind(KeyboardBindableAction.Flip);
        }

        private void HandleInputResetRequested()
        {
            popupController.Push(
                new PopupRequest(
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
                            presenter?.InputPresenter.ResetToDefaults();
                        }
                    }),
                out _);
        }

        private void HandleAudioVolumeChanged(AudioSettingsChannel channel, float value)
        {
            presenter.AudioPresenter.SetVolume(channel, value);
        }

        private void HandleAudioMuteChanged(AudioSettingsChannel channel, bool isMuted)
        {
            presenter.AudioPresenter.SetMuted(channel, isMuted);
        }

        private void HandleAudioInteractionCompleted()
        {
            presenter.AudioPresenter.Flush();
        }

        private void HandleDisplayResolutionChanged(int modeIndex)
        {
            CancelDisplayStatusAutoHide();
            presenter.DisplayPresenter.StageResolution(modeIndex);
        }

        private void HandleDisplayFullscreenToggled(bool isFullscreen)
        {
            CancelDisplayStatusAutoHide();
            presenter.DisplayPresenter.StageWindowMode(isFullscreen
                ? DisplayWindowMode.FullScreenWindow
                : DisplayWindowMode.Windowed);
        }

        private void HandleDisplayApplyRequested()
        {
            CancelDisplayStatusAutoHide();
            if (!presenter.DisplayPresenter.ApplyStagedSettings(displayPreviewSessionHost.PreviewTimeoutSeconds))
            {
                return;
            }

            if (!displayPreviewSessionHost.TryOpen(
                    BuildDisplayPreviewConfirmPayload(),
                    HandleDisplayPreviewConfirmed,
                    HandleDisplayPreviewCancelled))
            {
                presenter.DisplayPresenter.CancelPreview();
                presenter.DisplayPresenter.ClearPreviewCountdown();
                ScheduleDisplayStatusAutoHideIfNeeded();
            }
        }

        private void HandleDisplayRevertRequested()
        {
            CancelDisplayStatusAutoHide();
            presenter.DisplayPresenter.ResetStagedToCurrent();
        }

        private void HandleDisplayPreviewConfirmed()
        {
            presenter?.DisplayPresenter.ConfirmPreview();
            ScheduleDisplayStatusAutoHideIfNeeded();
        }

        private void HandleDisplayPreviewCancelled()
        {
            presenter?.DisplayPresenter.CancelPreview();
            ScheduleDisplayStatusAutoHideIfNeeded();
        }

        private void HandleDisplayResyncRequested()
        {
            CancelDisplayStatusAutoHide();
            presenter?.DisplayPresenter.ResyncState(displayPreviewSessionHost.PreviewTimeoutSeconds);
        }

        private void HandleDisplayPreviewCountdownChanged(DisplayPreviewCountdownSnapshot snapshot)
        {
            presenter?.DisplayPresenter.SetPreviewCountdown(snapshot);
        }

        private void ScheduleDisplayStatusAutoHideIfNeeded()
        {
            CancelDisplayStatusAutoHide();
            if (presenter == null || !presenter.DisplayPresenter.ViewModel.IsDisplayStatusTransient)
            {
                return;
            }

            displayStatusTransientRelay.Arm(
                DisplayStatusTransientSeconds,
                () => presenter?.DisplayPresenter.ClearTransientDisplayStatus(displayPreviewSessionHost.PreviewTimeoutSeconds));
        }

        private void CancelDisplayStatusAutoHide()
        {
            displayStatusTransientRelay?.Cancel();
        }

        private void HandleBackRequested()
        {
            TryHandleBackRequested();
        }

        private ConfirmPopupPayload BuildDisplayPreviewConfirmPayload()
        {
            var displayViewModel = presenter.DisplayPresenter.ViewModel;
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
                displayPreviewSessionHost.PreviewTimeoutSeconds,
                displayPreviewSessionHost.PreviewTimeoutSeconds);

            return new ConfirmPopupPayload(
                "Confirm Display Preview",
                $"Preview {resolutionLabel} in {windowModeText}. These changes are temporary and will revert in {visibleTimeoutSeconds} seconds unless you confirm.",
                "Keep",
                "Revert",
                false);
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(MainMenuSettingsRuntime));
            }
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
    }
}
