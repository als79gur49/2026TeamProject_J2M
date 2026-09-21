using System;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Flow
{
    internal readonly struct ResultContentEntranceMilestone
    {
        internal ResultContentEntranceMilestone(
            TerminalSessionToken terminalToken,
            TerminalDestinationKind destinationKind)
        {
            TerminalToken = terminalToken;
            DestinationKind = destinationKind;
        }

        internal TerminalSessionToken TerminalToken { get; }

        internal TerminalDestinationKind DestinationKind { get; }
    }

    public sealed class UIFlowCoordinator : IDisposable, IUiFlowAudioIntentBoundary, IUIFlowPresentationSource
    {
        private enum PauseReturnMode
        {
            None = 0,
            RestorePausePopupAfterBack = 1,
        }

        private readonly IGameplayUiPresentationSource _presentationSource;
        private readonly CampaignStageSequenceResolver _campaignStageSequenceResolver;
        private readonly IMainMenuReturnRouter _mainMenuReturnRouter;
        private readonly IPauseProgressionReadSource _pauseProgressionReadSource;
        private readonly IUiFlowPauseService _pauseService;
        private readonly PopupController _popupController;
        private readonly ScreenController _screenController;
        private readonly IStageLaunchRouter _stageLaunchRouter;
        private readonly IUiAudioPort _uiAudioPort;
        private readonly UIBlockPolicy _uiBlockPolicy;
        private Action<UIFlowPresentationSnapshot> _flowPresentationChanged;
        private UIFlowPresentationSnapshot _currentFlowPresentation =
            UIFlowPresentationSnapshot.GameplayDefault;
        private UiFlowAudioTransaction _activeAudioTransaction;
        private PopupController.PopupCompletionDispatchEvent? _activePopupCompletionDispatch;
        private UITickEventKey? _lastStageClearedEventKey;
        private TerminalSessionToken _lastResultContentEntranceAudioToken;
        private TerminalDestinationKind _lastResultContentEntranceAudioDestination;
        private PauseReturnMode _pauseReturnMode;

        public UIFlowCoordinator(
            ScreenController screenController,
            PopupController popupController,
            UIBlockPolicy uiBlockPolicy,
            IUiFlowPauseService pauseService,
            IGameplayUiPresentationSource presentationSource,
            IUiAudioPort uiAudioPort,
            IStageLaunchRouter stageLaunchRouter,
            IMainMenuReturnRouter mainMenuReturnRouter = null,
            IPauseProgressionReadSource pauseProgressionReadSource = null,
            CampaignStageSequenceResolver campaignStageSequenceResolver = null)
        {
            _screenController = screenController ?? throw new ArgumentNullException(nameof(screenController));
            _popupController = popupController ?? throw new ArgumentNullException(nameof(popupController));
            _uiBlockPolicy = uiBlockPolicy ?? throw new ArgumentNullException(nameof(uiBlockPolicy));
            _pauseService = pauseService ?? throw new ArgumentNullException(nameof(pauseService));
            _presentationSource = presentationSource ?? throw new ArgumentNullException(nameof(presentationSource));
            _uiAudioPort = uiAudioPort ?? throw new ArgumentNullException(nameof(uiAudioPort));
            _stageLaunchRouter = stageLaunchRouter ?? throw new ArgumentNullException(nameof(stageLaunchRouter));
            _mainMenuReturnRouter = mainMenuReturnRouter ?? NoOpMainMenuReturnRouter.Instance;
            _pauseProgressionReadSource = pauseProgressionReadSource ?? EmptyPauseProgressionReadSource.Instance;
            _campaignStageSequenceResolver = campaignStageSequenceResolver;

            _screenController.StateChanged += HandleFlowStateChanged;
            _screenController.ActionRequested += HandleScreenActionRequested;
            _screenController.ScreenTransitioned += HandleScreenTransitioned;
            _popupController.StateChanged += HandleFlowStateChanged;
            _popupController.PopupOpened += HandlePopupOpened;
            _popupController.PopupCompleted += HandlePopupCompleted;
            _popupController.PopupCompletionDispatching += HandlePopupCompletionDispatching;
            _popupController.PopupCompletionDispatched += HandlePopupCompletionDispatched;
            _presentationSource.TickEventsApplied += HandleTickEventsApplied;
            _presentationSource.LevelFailedCommitted += HandleLevelFailedCommitted;
            TerminalSessionRegistry.Changed += HandleTerminalSessionChanged;
        }

        public UIBlockSnapshot CurrentBlockSnapshot { get; private set; }

        internal UiFlowAudioTrace LastFlowAudioTrace { get; private set; }

        UIFlowPresentationSnapshot IUIFlowPresentationSource.Current =>
            _currentFlowPresentation;

        event Action<UIFlowPresentationSnapshot> IUIFlowPresentationSource.Changed
        {
            add => _flowPresentationChanged += value;
            remove => _flowPresentationChanged -= value;
        }

        public void Initialize()
        {
            ClearPauseReturnMode();
            _screenController.SetRoot(BuildGameplayRequest());
            RefreshBlockSnapshot();
        }

        public bool OpenSettingsScreen()
        {
            return ExecuteIntent(UiFlowAudioIntentKind.OpenForward, () => PushScreenCore(BuildSettingsRequest()));
        }

        public bool RequestPausePopup()
        {
            return ExecuteIntent(UiFlowAudioIntentKind.OpenForward, () => RequestPausePopupCore());
        }

        public bool RequestConfirmPopup(
            ConfirmPopupPayload payload,
            Action<PopupCompletion> completionCallback = null)
        {
            return ExecuteIntent(
                UiFlowAudioIntentKind.OpenForward,
                () => RequestConfirmPopupCore(payload, completionCallback));
        }

        internal bool TryToggleDemoStageControlPopup(
            bool allowNewOpen,
            Func<IPopupPayload> payloadFactory)
        {
            if (SceneEntryPresentationRegistry.IsActive)
            {
                return false;
            }

            var closingDemoPopup = _popupController.TopPopup.HasValue &&
                                   _popupController.TopPopup.Value.PopupId == PopupId.DemoStageControl;
            return ExecuteIntent(
                closingDemoPopup ? ResolveBackIntent() : UiFlowAudioIntentKind.OpenForward,
                () => TryToggleDemoStageControlPopupCore(allowNewOpen, payloadFactory));
        }

        public bool HandleBackRequested()
        {
            return ExecuteIntent(ResolveBackIntent(), HandleBackRequestedCore);
        }

        public bool HandlePopupBackdropClicked()
        {
            return ExecuteIntent(UiFlowAudioIntentKind.Back, HandlePopupBackdropClickedCore);
        }

        internal bool NotifyResultContentEntranceStarted(
            ResultContentEntranceMilestone milestone)
        {
            var session = TerminalSessionRegistry.Current;
            if (!milestone.TerminalToken.IsValid ||
                !session.IsActive ||
                session.Token != milestone.TerminalToken ||
                session.TerminalKind != TerminalTransitionKind.Victory ||
                session.DestinationKind != milestone.DestinationKind ||
                session.Phase != TerminalSessionPhase.WaitingResultInteraction)
            {
                return false;
            }

            if (_lastResultContentEntranceAudioToken == milestone.TerminalToken &&
                _lastResultContentEntranceAudioDestination == milestone.DestinationKind)
            {
                return false;
            }

            var cue = milestone.DestinationKind switch
            {
                TerminalDestinationKind.SameSceneStageResult => UiAudioCueId.StageClear,
                TerminalDestinationKind.SameSceneGameClear => UiAudioCueId.GameClear,
                _ => throw new InvalidOperationException(
                    $"Unsupported Result content entrance destination {milestone.DestinationKind}."),
            };
            _lastResultContentEntranceAudioToken = milestone.TerminalToken;
            _lastResultContentEntranceAudioDestination = milestone.DestinationKind;
            _uiAudioPort.Play(cue);
            return true;
        }

        public void Dispose()
        {
            AbortActiveTransaction(UiFlowAudioSilenceReason.Cleanup);
            ClearPauseReturnMode();
            _screenController.StateChanged -= HandleFlowStateChanged;
            _screenController.ActionRequested -= HandleScreenActionRequested;
            _screenController.ScreenTransitioned -= HandleScreenTransitioned;
            _popupController.StateChanged -= HandleFlowStateChanged;
            _popupController.PopupOpened -= HandlePopupOpened;
            _popupController.PopupCompleted -= HandlePopupCompleted;
            _popupController.PopupCompletionDispatching -= HandlePopupCompletionDispatching;
            _popupController.PopupCompletionDispatched -= HandlePopupCompletionDispatched;
            _presentationSource.TickEventsApplied -= HandleTickEventsApplied;
            _presentationSource.LevelFailedCommitted -= HandleLevelFailedCommitted;
            TerminalSessionRegistry.Changed -= HandleTerminalSessionChanged;
        }

        bool IUiFlowAudioIntentBoundary.ExecuteOpenForwardBoundary(Func<bool> action)
        {
            return ExecuteIntent(UiFlowAudioIntentKind.OpenForward, action);
        }

        private void RefreshBlockSnapshot()
        {
            var blockSnapshot = _uiBlockPolicy.Evaluate(
                new UIFlowStateSnapshot(
                    _screenController.CurrentEntry,
                    _popupController.TopPopup,
                    _popupController.PopupCount,
                    TerminalSessionRegistry.IsActive));
            var presentationSnapshot = new UIFlowPresentationSnapshot(
                isHudVisible: !_screenController.CurrentEntry.HasValue ||
                              _screenController.CurrentEntry.Value.Policy.HudShellMode != HudShellMode.Hidden,
                isUiGameplayInputBlocked: blockSnapshot.BlocksUiGameplayInput,
                isPopupLayerVisible: _popupController.PopupCount > 0,
                showsPopupDim: blockSnapshot.ShowsPopupDim,
                blocksLowerLayerPointer: blockSnapshot.BlocksLowerLayerPointer,
                popupBackdropMode: blockSnapshot.PopupBackdropMode);

            CurrentBlockSnapshot = blockSnapshot;
            if (_currentFlowPresentation.Equals(presentationSnapshot))
            {
                return;
            }

            _currentFlowPresentation = presentationSnapshot;
            _flowPresentationChanged?.Invoke(presentationSnapshot);
        }

        private void HandleTerminalSessionChanged(TerminalSessionSnapshot snapshot)
        {
            if (snapshot.IsActive)
            {
                ClearPauseReturnMode();
                ClosePopupsForScreenTransition();
            }

            RefreshBlockSnapshot();
        }

        private void ClosePopupsForScreenTransition()
        {
            if (_popupController.PopupCount == 0)
            {
                return;
            }

            _popupController.CloseAll(PopupCloseReason.ScreenTransition);
        }

        private void HandlePausePopupCompletion(PopupCompletion completion)
        {
            if (completion.PopupId != PopupId.Pause)
            {
                return;
            }

            switch (completion.CompletionKind)
            {
                case PopupCompletionKind.SettingsRequested:
                    SetPauseReturnMode(PauseReturnMode.RestorePausePopupAfterBack);
                    if (!PushScreenCore(BuildSettingsRequest(), preservePauseReturnMode: true))
                    {
                        ClearPauseReturnMode();
                        RequestPausePopupCore(acquirePauseOwnership: false);
                    }

                    break;

                case PopupCompletionKind.Resumed:
                case PopupCompletionKind.Closed:
                    ClearPauseReturnMode();
                    _pauseService.Resume();
                    break;

                case PopupCompletionKind.RetryRequested:
                    ClearPauseReturnMode();
                    try
                    {
                        if (!TryLaunchStage(BuildPauseRetryRequest()))
                        {
                            throw new InvalidOperationException(
                                "Pause Retry route was rejected before transition ownership.");
                        }
                    }
                    catch
                    {
                        _activeAudioTransaction?.Abort(UiFlowAudioSilenceReason.Aborted);
                        throw;
                    }

                    try
                    {
                        _pauseService.Resume();
                    }
                    catch (Exception exception)
                    {
                        // The route owns the transition once TryLaunchStage returns true.
                        // Keep that owner and prevent retained-popup rollback on Resume failure.
                        UnityEngine.Debug.LogException(exception);
                    }

                    break;

                case PopupCompletionKind.MainMenuRequested:
                    ClearPauseReturnMode();
                    ReturnToMainMenu();
                    break;
            }
        }

        private void HandleFlowStateChanged()
        {
            RefreshBlockSnapshot();
        }

        private void HandleScreenTransitioned(ScreenTransitionedEvent transitionEvent)
        {
            RecordDelta(UiFlowAudioDelta.FromScreenTransition(transitionEvent));
        }

        private void HandlePopupOpened(PopupOpenedEvent openedEvent)
        {
            RecordDelta(UiFlowAudioDelta.FromPopupOpened(openedEvent));
        }

        private void HandlePopupCompleted(PopupCompletedEvent completedEvent)
        {
            RecordDelta(UiFlowAudioDelta.FromPopupCompleted(completedEvent));
        }

        private void HandlePopupCompletionDispatching(PopupController.PopupCompletionDispatchEvent dispatchEvent)
        {
            if (_activeAudioTransaction != null)
            {
                return;
            }

            var intent = ResolvePopupCompletionIntent(dispatchEvent.Completion);
            if (intent == UiFlowAudioIntentKind.None)
            {
                return;
            }

            _activeAudioTransaction = new UiFlowAudioTransaction(intent);
            _activePopupCompletionDispatch = dispatchEvent;
        }

        private void HandlePopupCompletionDispatched(PopupController.PopupCompletionDispatchEvent dispatchEvent)
        {
            if (!_activePopupCompletionDispatch.HasValue)
            {
                return;
            }

            var expected = _activePopupCompletionDispatch.Value.Completion;
            if (!expected.InstanceId.Equals(dispatchEvent.Completion.InstanceId))
            {
                return;
            }

            _activeAudioTransaction?.Leave();
            _activePopupCompletionDispatch = null;
            TryFinalizeActiveTransaction();
        }

        public void HandleScreenActionRequested(ScreenAction action)
        {
            switch (action.ActionKind)
            {
                case ScreenActionKind.BackRequested:
                    HandleBackRequested();
                    break;

                case ScreenActionKind.ShowScreen:
                    ExecuteIntent(UiFlowAudioIntentKind.OpenForward, () => ShowScreenCore(action.ScreenRequest));
                    break;

                case ScreenActionKind.PushScreen:
                    ExecuteIntent(UiFlowAudioIntentKind.OpenForward, () => PushScreenCore(action.ScreenRequest));
                    break;

                case ScreenActionKind.ReplaceScreen:
                    ExecuteIntent(UiFlowAudioIntentKind.OpenForward, () => ReplaceScreenCore(action.ScreenRequest));
                    break;

                case ScreenActionKind.RequestPopup:
                    ExecuteIntent(UiFlowAudioIntentKind.OpenForward, () => TryPushPopupRequestCore(action.PopupRequest));
                    break;

                case ScreenActionKind.LaunchStage:
                    LaunchStage(action.StageNavigationRequest);
                    break;

                case ScreenActionKind.ReturnToMainMenu:
                    ReturnToMainMenu();
                    break;
            }
        }

        private bool ExecuteIntent(UiFlowAudioIntentKind intent, Func<bool> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if ((TerminalSessionRegistry.IsActive ||
                 MainMenuEntryPresentationRegistry.IsActive) &&
                intent != UiFlowAudioIntentKind.SystemPresentation)
            {
                return false;
            }

            var createdRoot = BeginTransaction(intent);

            try
            {
                var result = action();
                if (createdRoot && !result)
                {
                    _activeAudioTransaction?.Abort(UiFlowAudioSilenceReason.FailedOperation);
                }

                return result;
            }
            catch
            {
                if (createdRoot)
                {
                    _activeAudioTransaction?.Abort(UiFlowAudioSilenceReason.Aborted);
                }

                throw;
            }
            finally
            {
                EndTransaction(createdRoot);
            }
        }

        private bool TryToggleDemoStageControlPopupCore(
            bool allowNewOpen,
            Func<IPopupPayload> payloadFactory)
        {
            var topPopup = _popupController.TopPopup;
            if (topPopup.HasValue)
            {
                if (topPopup.Value.PopupId == PopupId.DemoStageControl)
                {
                    return HandleBackRequestedCore();
                }

                return true;
            }

            if (!allowNewOpen)
            {
                return false;
            }

            if (payloadFactory == null)
            {
                throw new ArgumentNullException(nameof(payloadFactory));
            }

            var payload = payloadFactory()
                ?? throw new InvalidOperationException(
                    "Demo Stage Control payload factory returned null.");
            return TryPushPopupRequestCore(
                new PopupRequest(PopupId.DemoStageControl, payload));
        }

        private bool BeginTransaction(UiFlowAudioIntentKind intent)
        {
            if (_activeAudioTransaction != null)
            {
                _activeAudioTransaction.Join();
                return false;
            }

            _activeAudioTransaction = new UiFlowAudioTransaction(intent);
            return true;
        }

        private void EndTransaction(bool createdRoot)
        {
            if (_activeAudioTransaction == null)
            {
                return;
            }

            _activeAudioTransaction.Leave();
            if (createdRoot)
            {
                TryFinalizeActiveTransaction();
            }
        }

        private void TryFinalizeActiveTransaction()
        {
            if (_activeAudioTransaction == null || _activeAudioTransaction.Depth > 0)
            {
                return;
            }

            var completedTransaction = _activeAudioTransaction;
            _activeAudioTransaction = null;
            var trace = completedTransaction.FinalizeTrace();
            LastFlowAudioTrace = trace;

            if (trace.EmittedCueId.HasValue)
            {
                _uiAudioPort.Play(trace.EmittedCueId.Value);
            }
        }

        private void AbortActiveTransaction(UiFlowAudioSilenceReason reason)
        {
            _activeAudioTransaction?.Abort(reason);
            _activePopupCompletionDispatch = null;
        }

        private void RecordDelta(UiFlowAudioDelta delta)
        {
            _activeAudioTransaction?.Record(delta);
        }

        private bool HandleBackRequestedCore()
        {
            if (_popupController.PopupCount > 0)
            {
                return _popupController.HandleBackRequested();
            }

            if (_screenController.HandleBackRequested())
            {
                var pauseReturnDecision = PauseReturnPolicy.Decide(new PauseReturnContext(
                    _pauseReturnMode == PauseReturnMode.RestorePausePopupAfterBack,
                    screenHandledBack: true,
                    _screenController.CurrentScreenId));

                if (pauseReturnDecision.ShouldReopenPausePopup &&
                    RequestPausePopupCore(acquirePauseOwnership: false))
                {
                    ClearPauseReturnMode();
                }

                return true;
            }

            if (_screenController.CurrentScreenId == ScreenId.Gameplay)
            {
                return RequestPausePopupCore();
            }

            return false;
        }

        private bool HandlePopupBackdropClickedCore()
        {
            return _popupController.HandleBackdropClicked();
        }

        private bool RequestPausePopupCore(bool acquirePauseOwnership = true)
        {
            if (_popupController.Contains(PopupId.Pause))
            {
                return false;
            }

            var progression = _pauseProgressionReadSource.TryRead(out var snapshot)
                ? snapshot
                : PauseProgressionSnapshot.Unavailable;
            if (!_popupController.Push(
                    new PopupRequest(
                        PopupId.Pause,
                        new PausePopupPayload(progression),
                        HandlePausePopupCompletion),
                    out _))
            {
                return false;
            }

            if (acquirePauseOwnership)
            {
                _pauseService.Pause();
            }

            return true;
        }

        private bool RequestConfirmPopupCore(
            ConfirmPopupPayload payload,
            Action<PopupCompletion> completionCallback)
        {
            if (payload == null)
            {
                return false;
            }

            return _popupController.Push(
                new PopupRequest(PopupId.Confirm, payload, completionCallback),
                out _);
        }

        private bool TryPushPopupRequestCore(PopupRequest request)
        {
            return _popupController.Push(request, out _);
        }

        private bool ShowScreenCore(ScreenRequest request)
        {
            return ShowScreenCore(request, preservePauseReturnMode: false);
        }

        private bool ShowScreenCore(ScreenRequest request, bool preservePauseReturnMode)
        {
            if (!preservePauseReturnMode)
            {
                ClearPauseReturnMode();
            }

            ClosePopupsForScreenTransition();
            return _screenController.Show(request);
        }

        private bool PushScreenCore(ScreenRequest request)
        {
            return PushScreenCore(request, preservePauseReturnMode: false);
        }

        private bool PushScreenCore(ScreenRequest request, bool preservePauseReturnMode)
        {
            if (!preservePauseReturnMode)
            {
                ClearPauseReturnMode();
            }

            ClosePopupsForScreenTransition();
            return _screenController.Push(request);
        }

        private bool ReplaceScreenCore(ScreenRequest request)
        {
            return ReplaceScreenCore(request, preservePauseReturnMode: false);
        }

        private bool ReplaceScreenCore(ScreenRequest request, bool preservePauseReturnMode)
        {
            if (!preservePauseReturnMode)
            {
                ClearPauseReturnMode();
            }

            ClosePopupsForScreenTransition();
            return _screenController.Replace(request);
        }

        public bool TryLaunchStage(StageNavigationRequest request)
        {
            if (!request.IsValid)
            {
                throw new InvalidOperationException("Stage launch actions require a valid StageNavigationRequest.");
            }

            var routePolicy = SceneTransitionRoutePolicyCatalog.RequireDestination(
                SceneTransitionRoutePolicyCatalog.ResolveProduction(request.TransitionIntent),
                SceneTransitionDestinationKind.Gameplay);
            if (TerminalSessionRegistry.ReadModel.IsActive)
            {
                return false;
            }

            if (SceneEntryPresentationRegistry.IsActive)
            {
                return false;
            }

            if (MainMenuEntryPresentationRegistry.IsActive)
            {
                return false;
            }

            var entryClaimed = false;
            var entryToken = default(SceneEntrySessionToken);
            if (IsCanonicalGameplayEntrySessionRoute(routePolicy) &&
                !SceneEntryPresentationRegistry.TryClaim(
                    routePolicy.Intent,
                    request.StageId,
                    TerminalSessionRegistry.Authority.CurrentSceneGeneration,
                    out entryToken))
            {
                return false;
            }

            entryClaimed = entryToken.IsValid;

            if (routePolicy.Intent != SceneTransitionIntent.ManualRetry)
            {
                ClosePopupsForScreenTransition();
            }

            try
            {
                _stageLaunchRouter.Launch(request);
            }
            catch
            {
                if (entryClaimed)
                {
                    TryCancelCurrentClaimIfStillClaimed(
                        entryToken,
                        routePolicy.Intent,
                        request.StageId);
                }

                throw;
            }

            return true;
        }

        private static void TryCancelCurrentClaimIfStillClaimed(
            SceneEntrySessionToken token,
            SceneTransitionIntent transitionIntent,
            StageId destinationStageId)
        {
            var current = SceneEntryPresentationRegistry.Current;
            if (!current.IsActive ||
                current.Token != token ||
                current.TransitionIntent != transitionIntent ||
                !current.DestinationStageId.Equals(destinationStageId) ||
                current.Phase != SceneEntryPresentationPhase.Claimed)
            {
                return;
            }

            SceneEntryPresentationRegistry.TryCancelClaim(token);
        }

        public bool TryReturnToMainMenu()
        {
            if (TerminalSessionRegistry.ReadModel.IsActive ||
                SceneEntryPresentationRegistry.IsActive ||
                MainMenuEntryPresentationRegistry.IsActive)
            {
                return false;
            }

            _mainMenuReturnRouter.ReturnToMainMenu(SceneTransitionIntent.ReturnToMainMenu);
            return true;
        }

        private void LaunchStage(StageNavigationRequest request)
        {
            TryLaunchStage(request);
        }

        private void ReturnToMainMenu()
        {
            TryReturnToMainMenu();
        }

        private void HandleTickEventsApplied(UITickEventBatch batch)
        {
            if (!batch.HasAnyEvents)
            {
                return;
            }

            for (var i = 0; i < batch.Events.Count; i++)
            {
                var tickEvent = batch.Events[i];
                if (tickEvent.EventKind != UITickEventKind.StageCleared)
                {
                    continue;
                }

                if (!TryCreateSameSceneDestinationSignal(
                        tickEvent.TerminalToken,
                        out var readinessSignal))
                {
                    return;
                }

                if (_lastStageClearedEventKey.HasValue && _lastStageClearedEventKey.Value.Equals(tickEvent.Key))
                {
                    return;
                }

                ExecuteIntent(UiFlowAudioIntentKind.SystemPresentation, () =>
                {
                    TerminalRuntimeTrace.Record(
                        TerminalSessionRegistry.Current,
                        TerminalTraceEvent.DestinationMutationAdmitted);
                    _lastStageClearedEventKey = tickEvent.Key;
                    ClearPauseReturnMode();
                    ClosePopupsForScreenTransition();
                    _screenController.Clear();
                    OpenStageCompletionFlow();
                    TerminalRuntimeTrace.Record(
                        TerminalSessionRegistry.Current,
                        TerminalTraceEvent.StageResultCreated);
                    TerminalRuntimeTrace.Record(
                        TerminalSessionRegistry.Current,
                        TerminalTraceEvent.PayloadBound);
                    if (!TerminalDestinationReadiness.Signal(readinessSignal))
                    {
                        throw new InvalidOperationException(
                            $"Stage completion destination readiness rejected terminal token {tickEvent.TerminalToken}.");
                    }

                    return true;
                });
                return;
            }
        }

        private void HandleLevelFailedCommitted(LevelFailedScreenPayload payload)
        {
            if (payload == null)
            {
                return;
            }

            if (!TryCreateSameSceneDestinationSignal(
                    payload.TerminalToken,
                    out var readinessSignal) ||
                readinessSignal.DestinationKind != TerminalDestinationKind.SameSceneLevelFailed)
            {
                return;
            }

            if (_screenController.CurrentScreenId == ScreenId.LevelFailed)
            {
                return;
            }

            ExecuteIntent(UiFlowAudioIntentKind.SystemPresentation, () =>
            {
                ClearPauseReturnMode();
                ClosePopupsForScreenTransition();
                _screenController.Clear();
                _screenController.SetRoot(new ScreenRequest(
                    ScreenId.LevelFailed,
                    payload,
                    ScreenId.LevelFailed.ToString()));
                RecordDelta(UiFlowAudioDelta.FromRootScreenSet(ScreenId.LevelFailed));
                if (!TerminalDestinationReadiness.Signal(readinessSignal))
                {
                    throw new InvalidOperationException(
                        $"LevelFailed destination readiness rejected terminal token {payload.TerminalToken}.");
                }

                return true;
            });
        }

        private static bool TryCreateSameSceneDestinationSignal(
            TerminalSessionToken token,
            out DestinationReadinessSignal signal)
        {
            var readModel = TerminalSessionRegistry.ReadModel;
            var session = readModel.Current;
            if (!token.IsValid ||
                !session.IsActive ||
                session.Token != token ||
                session.Phase != TerminalSessionPhase.WaitingSameSceneDestination)
            {
                signal = default;
                return false;
            }

            var provenance = session.DestinationKind switch
            {
                TerminalDestinationKind.SameSceneStageResult =>
                    TerminalDestinationProvenance.SameSceneStageResult,
                TerminalDestinationKind.SameSceneGameClear =>
                    TerminalDestinationProvenance.SameSceneGameClear,
                TerminalDestinationKind.SameSceneLevelFailed =>
                    TerminalDestinationProvenance.SameSceneLevelFailed,
                _ => TerminalDestinationProvenance.None,
            };
            signal = new DestinationReadinessSignal(
                token,
                transitionId: 0,
                session.SourceSceneGeneration,
                session.SourceSceneGeneration,
                session.DestinationKind,
                TerminalSessionPhase.WaitingSameSceneDestination,
                provenance,
                DestinationReadinessOutcome.Ready);
            return readModel.CanAcceptDestinationEvent(signal);
        }

        private void OpenStageCompletionFlow()
        {
            var readModel = _presentationSource.CurrentMinimalStageCompletion;
            if (readModel == null)
            {
                throw new InvalidOperationException(
                    "Stage clear tick events require a minimal completion read model before UI flow transition.");
            }

            if (IsCampaignFinalStage(readModel.StageId))
            {
                _screenController.SetRoot(new ScreenRequest(
                    ScreenId.GameClear,
                    GameClearScreenPayload.Default,
                    ScreenId.GameClear.ToString()));
                return;
            }

            _screenController.SetRoot(new ScreenRequest(
                ScreenId.StageResult,
                StageCompletionStageResultPayloadMapper.Map(readModel),
                ScreenId.StageResult.ToString()));
        }

        private bool IsCampaignFinalStage(StageId stageId)
        {
            if (_campaignStageSequenceResolver == null)
            {
                throw new InvalidOperationException(
                    "UIFlowCoordinator requires the gameplay composition campaign sequence resolver before handling StageCleared.");
            }

            return stageId.IsValid && _campaignStageSequenceResolver.IsFinal(stageId);
        }

        private static bool IsCanonicalGameplayEntrySessionRoute(
            SceneTransitionRoutePolicy routePolicy)
        {
            return routePolicy.ImplementsSceneTransitionSession &&
                   (routePolicy.Intent == SceneTransitionIntent.StageAdvance ||
                    routePolicy.Intent == SceneTransitionIntent.DeathRetry ||
                    routePolicy.Intent == SceneTransitionIntent.ManualRetry ||
                    routePolicy.Intent == SceneTransitionIntent.GameplayEntry ||
                    routePolicy.Intent == SceneTransitionIntent.DemoStageRelaunch);
        }

        private UiFlowAudioIntentKind ResolveBackIntent()
        {
            if (_popupController.TopPopup.HasValue &&
                _popupController.TopPopup.Value.Policy.BackAction == PopupBackAction.Cancel)
            {
                return UiFlowAudioIntentKind.Cancel;
            }

            return UiFlowAudioIntentKind.Back;
        }

        private static UiFlowAudioIntentKind ResolvePopupCompletionIntent(PopupCompletion completion)
        {
            if (!IsUserVisibleCompletion(completion.CloseReason))
            {
                return UiFlowAudioIntentKind.None;
            }

            switch (completion.PopupId)
            {
                case PopupId.Pause:
                    return completion.CompletionKind switch
                    {
                        PopupCompletionKind.SettingsRequested => UiFlowAudioIntentKind.OpenForward,
                        PopupCompletionKind.RetryRequested => UiFlowAudioIntentKind.Confirm,
                        PopupCompletionKind.MainMenuRequested => UiFlowAudioIntentKind.Back,
                        PopupCompletionKind.Resumed => UiFlowAudioIntentKind.Confirm,
                        PopupCompletionKind.Closed => UiFlowAudioIntentKind.Back,
                        _ => UiFlowAudioIntentKind.None,
                    };

                case PopupId.Confirm:
                    return completion.CompletionKind switch
                    {
                        PopupCompletionKind.Confirmed => UiFlowAudioIntentKind.Confirm,
                        PopupCompletionKind.Cancelled => UiFlowAudioIntentKind.Cancel,
                        _ => UiFlowAudioIntentKind.None,
                    };

                case PopupId.DemoStageControl:
                    return completion.CompletionKind == PopupCompletionKind.Closed
                        ? UiFlowAudioIntentKind.Back
                        : UiFlowAudioIntentKind.None;

                default:
                    return UiFlowAudioIntentKind.None;
            }
        }

        private static bool IsUserVisibleCompletion(PopupCloseReason closeReason)
        {
            switch (closeReason)
            {
                case PopupCloseReason.UserAction:
                case PopupCloseReason.Back:
                case PopupCloseReason.BackdropClick:
                    return true;

                default:
                    return false;
            }
        }

        private void SetPauseReturnMode(PauseReturnMode mode)
        {
            _pauseReturnMode = mode;
        }

        private void ClearPauseReturnMode()
        {
            _pauseReturnMode = PauseReturnMode.None;
        }

        private static ScreenRequest BuildGameplayRequest()
        {
            return new ScreenRequest(ScreenId.Gameplay, GameplayRootPayload.Default, ScreenId.Gameplay.ToString());
        }

        private static ScreenRequest BuildSettingsRequest()
        {
            return new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, ScreenId.Settings.ToString());
        }

        private StageNavigationRequest BuildPauseRetryRequest()
        {
            var stageId = _presentationSource.CurrentSnapshot.Stage.StageId;
            if (!stageId.IsValid)
            {
                throw new InvalidOperationException(
                    "Pause retry requires the current gameplay presentation snapshot to contain a valid stage id.");
            }

            return new StageNavigationRequest(
                stageId,
                StageNavigationKind.Retry,
                "pause-retry",
                StageTransitionHint.ForKind(StageTransitionKind.StageRetryManual),
                SceneTransitionIntent.ManualRetry,
                EditorDirectPlayContextStore.GetCurrentOrNone().ForStage(stageId));
        }
    }
}
