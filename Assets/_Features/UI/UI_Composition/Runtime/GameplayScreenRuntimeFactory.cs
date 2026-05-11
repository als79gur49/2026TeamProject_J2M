using System;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    public sealed class GameplayScreenRuntimeFactory : IScreenRuntimeFactory
    {
        private readonly IGameplayQueryFacade _queryFacade;
        private readonly IGameplayUiPresentationSource _presentationSource;
        private readonly IAudioSettingsPort _audioSettingsPort;
        private readonly IDisplaySettingsPort _displaySettingsPort;
        private readonly IKeyboardBindingSettingsPort _keyboardBindingSettingsPort;
        private readonly IUiAudioPort _uiAudioPort;
        private readonly DisplayPreviewSessionHost _displayPreviewSessionHost;
        private readonly DisplaySettingsLifecycleRelay _displaySettingsLifecycleRelay;
        private readonly DisplayStatusTransientRelay _displayStatusTransientRelay;
        private readonly ScreenPrefabCatalog _screenPrefabCatalog;
        private readonly ScreenLayerView _screenLayerView;
        private readonly AccessibilitySettingsStore _accessibilitySettingsStore;

        internal GameplayScreenRuntimeFactory(
            ScreenLayerView screenLayerView,
            IGameplayQueryFacade queryFacade,
            IGameplayUiPresentationSource presentationSource,
            AccessibilitySettingsStore accessibilitySettingsStore,
            IAudioSettingsPort audioSettingsPort,
            IDisplaySettingsPort displaySettingsPort,
            IUiAudioPort uiAudioPort,
            DisplayPreviewSessionHost displayPreviewSessionHost,
            DisplaySettingsLifecycleRelay displaySettingsLifecycleRelay,
            ScreenPrefabCatalog screenPrefabCatalog,
            DisplayStatusTransientRelay displayStatusTransientRelay = null)
            : this(
                screenLayerView,
                queryFacade,
                presentationSource,
                accessibilitySettingsStore,
                audioSettingsPort,
                displaySettingsPort,
                NoOpKeyboardBindingSettingsPort.Instance,
                uiAudioPort,
                displayPreviewSessionHost,
                displaySettingsLifecycleRelay,
                screenPrefabCatalog,
                displayStatusTransientRelay)
        {
        }

        internal GameplayScreenRuntimeFactory(
            ScreenLayerView screenLayerView,
            IGameplayQueryFacade queryFacade,
            IGameplayUiPresentationSource presentationSource,
            AccessibilitySettingsStore accessibilitySettingsStore,
            IAudioSettingsPort audioSettingsPort,
            IDisplaySettingsPort displaySettingsPort,
            IKeyboardBindingSettingsPort keyboardBindingSettingsPort,
            IUiAudioPort uiAudioPort,
            DisplayPreviewSessionHost displayPreviewSessionHost,
            DisplaySettingsLifecycleRelay displaySettingsLifecycleRelay,
            ScreenPrefabCatalog screenPrefabCatalog,
            DisplayStatusTransientRelay displayStatusTransientRelay = null)
        {
            _screenLayerView = screenLayerView ?? throw new ArgumentNullException(nameof(screenLayerView));
            _queryFacade = queryFacade ?? throw new ArgumentNullException(nameof(queryFacade));
            _presentationSource = presentationSource ?? throw new ArgumentNullException(nameof(presentationSource));
            _accessibilitySettingsStore = accessibilitySettingsStore ?? throw new ArgumentNullException(nameof(accessibilitySettingsStore));
            _audioSettingsPort = audioSettingsPort ?? throw new ArgumentNullException(nameof(audioSettingsPort));
            _displaySettingsPort = displaySettingsPort ?? throw new ArgumentNullException(nameof(displaySettingsPort));
            _keyboardBindingSettingsPort = keyboardBindingSettingsPort ?? NoOpKeyboardBindingSettingsPort.Instance;
            _uiAudioPort = uiAudioPort ?? throw new ArgumentNullException(nameof(uiAudioPort));
            _displayPreviewSessionHost = displayPreviewSessionHost ?? throw new ArgumentNullException(nameof(displayPreviewSessionHost));
            _displaySettingsLifecycleRelay = displaySettingsLifecycleRelay ?? throw new ArgumentNullException(nameof(displaySettingsLifecycleRelay));
            _displayStatusTransientRelay = displayStatusTransientRelay;
            _screenPrefabCatalog = screenPrefabCatalog ?? throw new ArgumentNullException(nameof(screenPrefabCatalog));
        }

        public ScreenRuntimeFactoryResult Create(ScreenRequest request)
        {
            switch (request.ScreenId)
            {
                case ScreenId.Gameplay:
                    return CreateGameplayRuntime();

                case ScreenId.ObjectiveStatus:
                    return CreateObjectiveStatusRuntime();

                case ScreenId.Settings:
                    return CreateSettingsRuntime();

                case ScreenId.StageResult:
                    return CreateStageResultRuntime();

                case ScreenId.LevelFailed:
                    return CreateLevelFailedRuntime();

                default:
                    throw new InvalidOperationException($"Unsupported screen id: {request.ScreenId}");
            }
        }

        private ScreenRuntimeFactoryResult CreateGameplayRuntime()
        {
            return new ScreenRuntimeFactoryResult(
                new ScreenPolicy(
                    ScreenPolicyClass.GameplayRoot,
                    ScreenRetentionMode.RetainMountedHistory,
                    ScreenBackAction.None,
                    HudShellMode.Visible,
                    blocksUiGameplayInput: false),
                new GameplayRootRuntime());
        }

        private ScreenRuntimeFactoryResult CreateObjectiveStatusRuntime()
        {
            var objectiveStatusPresenter = new ObjectiveStatusPresenter(_presentationSource);
            var presenter = new ObjectiveStatusScreenPresenter(objectiveStatusPresenter);
            var view = InstantiateScreenPrefab(_screenPrefabCatalog.ObjectiveStatusPrefab, ScreenId.ObjectiveStatus);
            view.Bind(presenter.ViewModel);
            view.SetIsCurrent(false);

            return new ScreenRuntimeFactoryResult(
                new ScreenPolicy(
                    ScreenPolicyClass.GameplayAdjacentOverlay,
                    ScreenRetentionMode.RetainMountedHistory,
                    ScreenBackAction.Pop,
                    HudShellMode.Visible,
                    blocksUiGameplayInput: true),
                new ObjectiveStatusRuntime(view, presenter, _uiAudioPort, () => DestroyObject(view.gameObject)));
        }

        private ScreenRuntimeFactoryResult CreateSettingsRuntime()
        {
            var presenter = new SettingsScreenPresenter(
                _accessibilitySettingsStore,
                _audioSettingsPort,
                _displaySettingsPort,
                _keyboardBindingSettingsPort);
            var view = InstantiateScreenPrefab(_screenPrefabCatalog.SettingsPrefab, ScreenId.Settings);
            view.ValidateAuthoredStructureOrThrow();
            view.AudioView.ValidateAuthoredControlsOrThrow();
            view.DisplayView.ValidateAuthoredControlsOrThrow();
            view.InputView.ValidateAuthoredControlsOrThrow();

            view.Bind(presenter.ViewModel);
            view.AudioView.Bind(presenter.AudioPresenter.ViewModel);
            view.DisplayView.Bind(presenter.DisplayPresenter.ViewModel);
            view.InputView.Bind(presenter.InputPresenter.ViewModel);
            view.SetIsCurrent(false);

            return new ScreenRuntimeFactoryResult(
                new ScreenPolicy(
                    ScreenPolicyClass.Configuration,
                    ScreenRetentionMode.RetainMountedHistory,
                    ScreenBackAction.Pop,
                    HudShellMode.Hidden,
                    blocksUiGameplayInput: true),
                new SettingsRuntime(
                    view,
                    presenter,
                    _uiAudioPort,
                    _displayPreviewSessionHost,
                    _displaySettingsLifecycleRelay,
                    _displayStatusTransientRelay ?? view.gameObject.AddComponent<DisplayStatusTransientRelay>(),
                    () => DestroyObject(view.gameObject)));
        }

        private ScreenRuntimeFactoryResult CreateStageResultRuntime()
        {
            var presenter = new StageResultScreenPresenter();
            var view = InstantiateScreenPrefab(_screenPrefabCatalog.StageResultPrefab, ScreenId.StageResult);
            view.Bind(presenter.ViewModel);
            view.SetIsCurrent(false);

            return new ScreenRuntimeFactoryResult(
                new ScreenPolicy(
                    ScreenPolicyClass.TerminalResult,
                    ScreenRetentionMode.DisposeOnHide,
                    ScreenBackAction.Consume,
                    HudShellMode.Hidden,
                    blocksUiGameplayInput: true),
                new StageResultRuntime(view, presenter, _uiAudioPort, () => DestroyObject(view.gameObject)));
        }

        private ScreenRuntimeFactoryResult CreateLevelFailedRuntime()
        {
            var presenter = new LevelFailedScreenPresenter();
            var view = InstantiateScreenPrefab(_screenPrefabCatalog.LevelFailedPrefab, ScreenId.LevelFailed);
            view.Bind(presenter.ViewModel);
            view.SetIsCurrent(false);

            return new ScreenRuntimeFactoryResult(
                new ScreenPolicy(
                    ScreenPolicyClass.TerminalResult,
                    ScreenRetentionMode.DisposeOnHide,
                    ScreenBackAction.Consume,
                    HudShellMode.Hidden,
                    blocksUiGameplayInput: true),
                new LevelFailedRuntime(view, presenter, _uiAudioPort, () => DestroyObject(view.gameObject)));
        }

        private TView InstantiateScreenPrefab<TView>(TView prefab, ScreenId screenId)
            where TView : Component, IScreenView
        {
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"Screen prefab catalog is missing a canonical prefab for screen '{screenId}'.");
            }

            var instantiatedRoot = UnityEngine.Object.Instantiate(prefab.gameObject, _screenLayerView.ContentRoot, false);
            if (instantiatedRoot == null)
            {
                throw new InvalidOperationException(
                    $"Screen prefab instantiation returned null for screen '{screenId}'.");
            }

            var view = instantiatedRoot.GetComponent<TView>();
            if (view == null)
            {
                throw new InvalidOperationException(
                    $"Screen prefab for '{screenId}' is missing the expected root view component '{typeof(TView).Name}'.");
            }

            if (view.transform.parent != _screenLayerView.ContentRoot)
            {
                throw new InvalidOperationException(
                    $"Screen '{screenId}' must mount directly beneath ScreenLayer content root.");
            }

            return view;
        }

        private static TPayload ExpectPayload<TPayload>(IScreenPayload payload) where TPayload : class, IScreenPayload
        {
            if (payload is not TPayload typedPayload)
            {
                throw new InvalidOperationException($"Screen payload type mismatch. Expected {typeof(TPayload).Name}.");
            }

            return typedPayload;
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

        private abstract class ScreenRuntimeBase<TView> : IScreenRuntime where TView : Component, IScreenView
        {
            private readonly Action _dispose;
            private readonly IUiAudioPort _uiAudioPort;

            protected ScreenRuntimeBase(TView view, IUiAudioPort uiAudioPort, Action dispose)
            {
                View = view ?? throw new ArgumentNullException(nameof(view));
                _uiAudioPort = uiAudioPort ?? throw new ArgumentNullException(nameof(uiAudioPort));
                _dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));
            }

            public event Action<ScreenAction> ActionRequested;

            protected TView View { get; }

            public abstract void ApplyPayload(IScreenPayload payload);

            public virtual void Dispose()
            {
                View.SetIsCurrent(false);
                _dispose();
            }

            public virtual void SetIsCurrent(bool isCurrent)
            {
                View.SetIsCurrent(isCurrent);
            }

            protected void RaiseAction(ScreenAction action)
            {
                ActionRequested?.Invoke(action);
            }

            protected void PlayLocalCue(UiAudioCueId cueId)
            {
                _uiAudioPort.Play(cueId);
            }
        }

        private sealed class GameplayRootRuntime : IScreenRuntime
        {
            public event Action<ScreenAction> ActionRequested
            {
                add { }
                remove { }
            }

            public void ApplyPayload(IScreenPayload payload)
            {
                ExpectPayload<GameplayRootPayload>(payload);
            }

            public void SetIsCurrent(bool isCurrent)
            {
            }

            public void Dispose()
            {
            }
        }

        private sealed class ObjectiveStatusRuntime : ScreenRuntimeBase<ObjectiveStatusScreenView>
        {
            private readonly ObjectiveStatusScreenPresenter _presenter;

            public ObjectiveStatusRuntime(
                ObjectiveStatusScreenView view,
                ObjectiveStatusScreenPresenter presenter,
                IUiAudioPort uiAudioPort,
                Action dispose)
                : base(view, uiAudioPort, dispose)
            {
                _presenter = presenter;
                view.InfoRequested += HandleInfoRequested;
                view.BackRequested += HandleBackRequested;
            }

            public override void ApplyPayload(IScreenPayload payload)
            {
                _presenter.ApplyPayload(ExpectPayload<ObjectiveStatusScreenPayload>(payload));
            }

            public override void Dispose()
            {
                View.InfoRequested -= HandleInfoRequested;
                View.BackRequested -= HandleBackRequested;
                View.Bind(null);
                _presenter.Dispose();
                base.Dispose();
            }

            private void HandleInfoRequested()
            {
                RaiseAction(ScreenAction.Popup(new PopupRequest(PopupId.ObjectiveInfo, _presenter.BuildInfoPopupPayload())));
            }

            private void HandleBackRequested()
            {
                RaiseAction(ScreenAction.Back());
            }
        }

        private sealed class SettingsRuntime : ScreenRuntimeBase<SettingsScreenView>
        {
            private readonly SettingsAudioView _audioView;
            private readonly DisplayPreviewSessionHost _displayPreviewSessionHost;
            private readonly SettingsDisplayView _displayView;
            private readonly SettingsInputView _inputView;
            private readonly DisplaySettingsLifecycleRelay _displaySettingsLifecycleRelay;
            private readonly DisplayStatusTransientRelay _displayStatusTransientRelay;
            private readonly SettingsScreenPresenter _presenter;
            private const double DisplayStatusTransientSeconds = 2d;
            private bool _isCurrent;

            public SettingsRuntime(
                SettingsScreenView view,
                SettingsScreenPresenter presenter,
                IUiAudioPort uiAudioPort,
                DisplayPreviewSessionHost displayPreviewSessionHost,
                DisplaySettingsLifecycleRelay displaySettingsLifecycleRelay,
                DisplayStatusTransientRelay displayStatusTransientRelay,
                Action dispose)
                : base(view, uiAudioPort, dispose)
            {
                _presenter = presenter;
                _audioView = view.AudioView ?? throw new ArgumentNullException(nameof(view.AudioView));
                _displayView = view.DisplayView ?? throw new ArgumentNullException(nameof(view.DisplayView));
                _inputView = view.InputView ?? throw new ArgumentNullException(nameof(view.InputView));
                _displayPreviewSessionHost = displayPreviewSessionHost ?? throw new ArgumentNullException(nameof(displayPreviewSessionHost));
                _displaySettingsLifecycleRelay = displaySettingsLifecycleRelay ?? throw new ArgumentNullException(nameof(displaySettingsLifecycleRelay));
                _displayStatusTransientRelay = displayStatusTransientRelay ?? throw new ArgumentNullException(nameof(displayStatusTransientRelay));
                _audioView.VolumeChanged += HandleAudioVolumeChanged;
                _audioView.MuteChanged += HandleAudioMuteChanged;
                _audioView.InteractionCompleted += HandleAudioInteractionCompleted;
                _displayView.ResolutionChanged += HandleDisplayResolutionChanged;
                _displayView.FullscreenToggled += HandleDisplayFullscreenToggled;
                _displayView.ApplyRequested += HandleDisplayApplyRequested;
                _displayView.RevertRequested += HandleDisplayRevertRequested;
                _inputView.MovementSchemeToggleRequested += HandleInputMovementSchemeToggleRequested;
                _inputView.PushRebindRequested += HandleInputPushRebindRequested;
                _inputView.FlipRebindRequested += HandleInputFlipRebindRequested;
                _inputView.ResetRequested += HandleInputResetRequested;
                view.SectionSelected += HandleSectionSelected;
                view.BackRequested += HandleBackRequested;
                _displaySettingsLifecycleRelay.ResyncRequested += HandleDisplayResyncRequested;
                _displayPreviewSessionHost.CountdownChanged += HandleDisplayPreviewCountdownChanged;
            }

            public override void ApplyPayload(IScreenPayload payload)
            {
                _presenter.Apply(ExpectPayload<SettingsScreenPayload>(payload), _displayPreviewSessionHost.PreviewTimeoutSeconds);
            }

            public override void Dispose()
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
                _inputView.MovementSchemeToggleRequested -= HandleInputMovementSchemeToggleRequested;
                _inputView.PushRebindRequested -= HandleInputPushRebindRequested;
                _inputView.FlipRebindRequested -= HandleInputFlipRebindRequested;
                _inputView.ResetRequested -= HandleInputResetRequested;
                View.SectionSelected -= HandleSectionSelected;
                View.BackRequested -= HandleBackRequested;
                _displaySettingsLifecycleRelay.ResyncRequested -= HandleDisplayResyncRequested;
                _displayPreviewSessionHost.CountdownChanged -= HandleDisplayPreviewCountdownChanged;
                _displayView.Bind(null);
                _inputView.Bind(null);
                _audioView.Bind(null);
                View.Bind(null);
                base.Dispose();
            }

            public override void SetIsCurrent(bool isCurrent)
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
                base.SetIsCurrent(isCurrent);

                if (isCurrent)
                {
                    CancelDisplayStatusAutoHide();
                    _presenter.DisplayPresenter.ResyncState(_displayPreviewSessionHost.PreviewTimeoutSeconds);
                }
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
        }

        private sealed class StageResultRuntime : ScreenRuntimeBase<StageResultScreenView>
        {
            private readonly StageResultScreenPresenter _presenter;
            private StageResultScreenPayload _payload;

            public StageResultRuntime(
                StageResultScreenView view,
                StageResultScreenPresenter presenter,
                IUiAudioPort uiAudioPort,
                Action dispose)
                : base(view, uiAudioPort, dispose)
            {
                _presenter = presenter;
                view.ContinueRequested += HandleContinueRequested;
            }

            public override void ApplyPayload(IScreenPayload payload)
            {
                _payload = ExpectPayload<StageResultScreenPayload>(payload);
                _presenter.Apply(_payload);
            }

            public override void Dispose()
            {
                View.ContinueRequested -= HandleContinueRequested;
                View.Bind(null);
                base.Dispose();
            }

            private void HandleContinueRequested()
            {
                if (_payload == null || !_payload.ContinueStageRequest.IsValid)
                {
                    throw new InvalidOperationException(
                        "Stage result continue requires a valid StageNavigationRequest payload.");
                }

                RaiseAction(ScreenAction.LaunchStage(_payload.ContinueStageRequest));
            }
        }

        private sealed class LevelFailedRuntime : ScreenRuntimeBase<LevelFailedScreenView>
        {
            private readonly LevelFailedScreenPresenter _presenter;
            private LevelFailedScreenPayload _payload;

            public LevelFailedRuntime(
                LevelFailedScreenView view,
                LevelFailedScreenPresenter presenter,
                IUiAudioPort uiAudioPort,
                Action dispose)
                : base(view, uiAudioPort, dispose)
            {
                _presenter = presenter;
                view.RestartLevelRequested += HandleRestartLevelRequested;
                view.MainRequested += HandleMainRequested;
            }

            public override void ApplyPayload(IScreenPayload payload)
            {
                _payload = ExpectPayload<LevelFailedScreenPayload>(payload);
                _presenter.Apply(_payload);
            }

            public override void Dispose()
            {
                View.RestartLevelRequested -= HandleRestartLevelRequested;
                View.MainRequested -= HandleMainRequested;
                View.Bind(null);
                base.Dispose();
            }

            private void HandleRestartLevelRequested()
            {
                if (_payload == null || !_payload.RestartLevelRequest.IsValid)
                {
                    throw new InvalidOperationException(
                        "Level failed restart requires a valid StageNavigationRequest payload.");
                }

                RaiseAction(ScreenAction.LaunchStage(_payload.RestartLevelRequest));
            }

            private void HandleMainRequested()
            {
                RaiseAction(ScreenAction.ReturnToMainMenu());
            }
        }
    }
}
