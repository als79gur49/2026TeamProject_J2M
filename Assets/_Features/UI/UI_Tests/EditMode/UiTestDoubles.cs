using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Gameplay.UIAccess.Queries;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Tests
{
    internal sealed class FakeGameplayCommandGateway : IGameplayCommandGateway
    {
        public int ClearHeldMoveDirectionCallCount { get; private set; }

        public int SetHeldMoveDirectionCallCount { get; private set; }

        public Func<GameplayUiDirection, GameplayCommandAcceptance> OnSetHeldMoveDirection { get; set; } =
            _ => GameplayCommandAcceptance.Accept();

        public Func<GameplayCommandAcceptance> OnClearHeldMoveDirection { get; set; } =
            () => GameplayCommandAcceptance.Accept();

        public GameplayCommandAcceptance SetHeldMoveDirection(GameplayUiDirection direction)
        {
            SetHeldMoveDirectionCallCount++;
            return OnSetHeldMoveDirection(direction);
        }

        public GameplayCommandAcceptance ClearHeldMoveDirection()
        {
            ClearHeldMoveDirectionCallCount++;
            return OnClearHeldMoveDirection();
        }

    }

    internal sealed class FakeGameplayPauseService : IGameplayPauseService, IUiFlowPauseService
    {
        public event Action<bool> PauseChanged;

        public bool IsPaused { get; private set; }

        public int PauseCallCount { get; private set; }

        public int ResumeCallCount { get; private set; }

        public void Pause()
        {
            PauseCallCount++;
            if (IsPaused)
            {
                return;
            }

            IsPaused = true;
            PauseChanged?.Invoke(true);
        }

        public void Resume()
        {
            ResumeCallCount++;
            if (!IsPaused)
            {
                return;
            }

            IsPaused = false;
            PauseChanged?.Invoke(false);
        }

        public void Toggle()
        {
            if (IsPaused)
            {
                Resume();
                return;
            }

            Pause();
        }
    }

    internal sealed class FakeGameplayPresentationFeed : IGameplayPresentationFeed
    {
        public event Action<GameplayPresentationFrame> FramePublished;

        public event Action<GameplayPresentationState> StateChanged;

        public event Action<GameplayLevelFailedReadModel> LevelFailedCommitted;

        public GameplayPresentationState CurrentState { get; private set; } =
            new GameplayPresentationState(new GameplayUiTopology(GameplayUiFace.Floor), false, false, false);

        public MinimalStageCompletionReadModel CurrentMinimalStageCompletion { get; private set; }

        public GameplayLevelFailedReadModel CurrentLevelFailed { get; private set; }

        public bool HasPendingStageClearPresentation { get; set; }

        public void PublishFrame(GameplayPresentationFrame frame)
        {
            FramePublished?.Invoke(frame);
        }

        public void PublishState(GameplayPresentationState state)
        {
            CurrentState = state;
            StateChanged?.Invoke(state);
        }

        public void PublishMinimalStageCompletion(MinimalStageCompletionReadModel readModel)
        {
            CurrentMinimalStageCompletion = readModel;
        }

        public void PublishLevelFailed(GameplayLevelFailedReadModel readModel)
        {
            CurrentLevelFailed = readModel;
            LevelFailedCommitted?.Invoke(readModel);
        }
    }

    internal sealed class FakeAudioSettingsPort : IAudioSettingsPort
    {
        private AudioSettingsPortSnapshot _snapshot = new(
            new AudioSettingsPortChannelState(1f, false),
            new AudioSettingsPortChannelState(1f, false),
            new AudioSettingsPortChannelState(1f, false));

        public int FlushCallCount { get; private set; }

        public AudioSettingsPortSnapshot Read()
        {
            return _snapshot;
        }

        public void SetVolume(AudioSettingsChannel channel, float volume)
        {
            var clamped = Clamp01(volume);
            var state = _snapshot.GetChannelState(channel);
            SetChannelState(channel, new AudioSettingsPortChannelState(clamped, state.IsMuted));
        }

        public void SetMuted(AudioSettingsChannel channel, bool isMuted)
        {
            var state = _snapshot.GetChannelState(channel);
            SetChannelState(channel, new AudioSettingsPortChannelState(state.Volume, isMuted));
        }

        public void Flush()
        {
            FlushCallCount++;
        }

        private void SetChannelState(AudioSettingsChannel channel, AudioSettingsPortChannelState state)
        {
            switch (channel)
            {
                case AudioSettingsChannel.Main:
                    _snapshot = new AudioSettingsPortSnapshot(state, _snapshot.Bgm, _snapshot.Sfx);
                    break;
                case AudioSettingsChannel.Bgm:
                    _snapshot = new AudioSettingsPortSnapshot(_snapshot.Main, state, _snapshot.Sfx);
                    break;
                case AudioSettingsChannel.Sfx:
                    _snapshot = new AudioSettingsPortSnapshot(_snapshot.Main, _snapshot.Bgm, state);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
            }
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            if (value > 1f)
            {
                return 1f;
            }

            return value;
        }
    }

    internal sealed class RecordingUiAudioPort : IUiAudioPort
    {
        private readonly List<UiAudioCueId> _playedCueIds = new();

        public IReadOnlyList<UiAudioCueId> PlayedCueIds => _playedCueIds;

        public void Play(UiAudioCueId cueId)
        {
            _playedCueIds.Add(cueId);
        }

        public void Clear()
        {
            _playedCueIds.Clear();
        }
    }

    internal sealed class FakeDisplaySettingsPort : IDisplaySettingsPort
    {
        private readonly List<DisplaySettingsPortModeOption> _availableModes = new()
        {
            new DisplaySettingsPortModeOption(1920, 1080, "1920 x 1080"),
            new DisplaySettingsPortModeOption(1600, 900, "1600 x 900"),
            new DisplaySettingsPortModeOption(1280, 720, "1280 x 720"),
        };

        private DisplaySettingsPortPreviewRequest _lastPreviewRequest;
        private bool _hasPreviewRequest;

        public int BeginPreviewCallCount { get; private set; }

        public int CommitPreviewCallCount { get; private set; }

        public int RevertPreviewCallCount { get; private set; }

        public bool BeginPreviewResult { get; set; } = true;

        public bool CommitPreviewResult { get; set; } = true;

        public bool RevertPreviewResult { get; set; } = true;

        public int CommittedModeIndex { get; private set; } = 0;

        public DisplayWindowMode CommittedWindowMode { get; private set; } = DisplayWindowMode.Windowed;

        public string CurrentRuntimeResolutionLabel { get; private set; } = "1920 x 1080";

        public DisplayWindowMode CurrentRuntimeWindowMode { get; private set; } = DisplayWindowMode.Windowed;

        public bool IsPreviewActive { get; private set; }

        public DisplaySettingsPortPreviewRequest LastPreviewRequest => _lastPreviewRequest;

        public DisplaySettingsPortSnapshot Read()
        {
            return new DisplaySettingsPortSnapshot(
                _availableModes,
                CommittedModeIndex,
                CommittedWindowMode,
                CurrentRuntimeResolutionLabel,
                CurrentRuntimeWindowMode,
                IsPreviewActive);
        }

        public bool BeginPreview(DisplaySettingsPortPreviewRequest request)
        {
            BeginPreviewCallCount++;
            _lastPreviewRequest = request;
            _hasPreviewRequest = true;
            if (!BeginPreviewResult)
            {
                return false;
            }

            var clampedIndex = ClampIndex(request.ModeIndex);
            IsPreviewActive = true;
            CurrentRuntimeResolutionLabel = _availableModes[clampedIndex].LabelText;
            CurrentRuntimeWindowMode = request.WindowMode;
            return true;
        }

        public bool CommitPreview()
        {
            CommitPreviewCallCount++;
            if (!CommitPreviewResult)
            {
                return false;
            }

            if (_hasPreviewRequest)
            {
                CommittedModeIndex = ClampIndex(_lastPreviewRequest.ModeIndex);
                CommittedWindowMode = _lastPreviewRequest.WindowMode;
                CurrentRuntimeResolutionLabel = _availableModes[CommittedModeIndex].LabelText;
                CurrentRuntimeWindowMode = CommittedWindowMode;
            }

            IsPreviewActive = false;
            return true;
        }

        public bool RevertPreview()
        {
            RevertPreviewCallCount++;
            if (!RevertPreviewResult)
            {
                return false;
            }

            IsPreviewActive = false;
            CurrentRuntimeResolutionLabel = _availableModes[CommittedModeIndex].LabelText;
            CurrentRuntimeWindowMode = CommittedWindowMode;
            return true;
        }

        public void SetCommittedState(int modeIndex, DisplayWindowMode windowMode)
        {
            CommittedModeIndex = ClampIndex(modeIndex);
            CommittedWindowMode = windowMode;
            CurrentRuntimeResolutionLabel = _availableModes[CommittedModeIndex].LabelText;
            CurrentRuntimeWindowMode = CommittedWindowMode;
            IsPreviewActive = false;
        }

        public void SetRuntimeDrift(int modeIndex, DisplayWindowMode windowMode)
        {
            CurrentRuntimeResolutionLabel = _availableModes[ClampIndex(modeIndex)].LabelText;
            CurrentRuntimeWindowMode = windowMode;
        }

        public void SetPreviewState(
            int modeIndex,
            DisplayWindowMode windowMode)
        {
            IsPreviewActive = true;
            CurrentRuntimeResolutionLabel = _availableModes[ClampIndex(modeIndex)].LabelText;
            CurrentRuntimeWindowMode = windowMode;
        }

        private int ClampIndex(int index)
        {
            if (_availableModes.Count == 0)
            {
                return 0;
            }

            if (index < 0)
            {
                return 0;
            }

            if (index >= _availableModes.Count)
            {
                return _availableModes.Count - 1;
            }

            return index;
        }
    }

    internal sealed class FakeGameplayQueryFacade : IGameplayQueryFacade
    {
        private readonly MutableObjectiveQuery _objectiveQuery;
        private readonly MutablePlayerHudQuery _playerHudQuery;
        private readonly MutableSurfaceButtonRemainderQuery _surfaceButtonRemainderQuery;
        private readonly MutableStageQuery _stageQuery;
        private readonly MutableSessionQuery _sessionQuery;

        public FakeGameplayQueryFacade(
            GameplaySessionReadModel session,
            GameplayPlayerHudReadModel playerHud,
            GameplayObjectiveReadModel objective,
            GameplayStageReadModel stage = default,
            IReadOnlyList<GameplaySurfaceButtonRemainderReadModel> surfaceButtonRemainders = null)
        {
            _sessionQuery = new MutableSessionQuery(session);
            _stageQuery = new MutableStageQuery(stage);
            _playerHudQuery = new MutablePlayerHudQuery(playerHud);
            _objectiveQuery = new MutableObjectiveQuery(objective);
            _surfaceButtonRemainderQuery = new MutableSurfaceButtonRemainderQuery(surfaceButtonRemainders);
        }

        public IGameplaySessionQuery Session => _sessionQuery;

        public IGameplayStageQuery Stage => _stageQuery;

        public IGameplayPlayerHudQuery PlayerHud => _playerHudQuery;

        public IGameplayObjectiveQuery Objectives => _objectiveQuery;

        public IGameplaySurfaceButtonRemainderQuery SurfaceButtonRemainders => _surfaceButtonRemainderQuery;

        public void SetSession(GameplaySessionReadModel session)
        {
            _sessionQuery.Value = session;
        }

        public void SetPlayerHud(GameplayPlayerHudReadModel playerHud)
        {
            _playerHudQuery.Value = playerHud;
        }

        public void SetStage(GameplayStageReadModel stage)
        {
            _stageQuery.Value = stage;
        }

        public void SetObjective(GameplayObjectiveReadModel objective)
        {
            _objectiveQuery.Value = objective;
        }

        public void SetSurfaceButtonRemainders(IReadOnlyList<GameplaySurfaceButtonRemainderReadModel> surfaceButtonRemainders)
        {
            _surfaceButtonRemainderQuery.Value = surfaceButtonRemainders;
        }

        public static GameplayPlayerHudReadModel CreateDefaultPlayerHud()
        {
            return new GameplayPlayerHudReadModel(
                isAvailable: true,
                playerEntityId: 10,
                currentHp: 3,
                maxHp: 3,
                facing: GameplayUiDirection.Up,
                activeActionKind: GameplayUiActionKind.None,
                activeActionDirection: GameplayUiDirection.None,
                activeTargetEntityId: 0,
                isActionInProgress: false,
                isActionInRecoveryPhase: false,
                canMoveThisTick: true,
                canStartActionThisTick: true);
        }

        private sealed class MutableSessionQuery : IGameplaySessionQuery
        {
            public MutableSessionQuery(GameplaySessionReadModel value)
            {
                Value = value;
            }

            public GameplaySessionReadModel Value { get; set; }

            public GameplaySessionReadModel Read()
            {
                return Value;
            }
        }

        private sealed class MutableStageQuery : IGameplayStageQuery
        {
            public MutableStageQuery(GameplayStageReadModel value)
            {
                Value = value;
            }

            public GameplayStageReadModel Value { get; set; }

            public GameplayStageReadModel Read()
            {
                return Value;
            }
        }

        private sealed class MutablePlayerHudQuery : IGameplayPlayerHudQuery
        {
            public MutablePlayerHudQuery(GameplayPlayerHudReadModel value)
            {
                Value = value;
            }

            public GameplayPlayerHudReadModel Value { get; set; }

            public GameplayPlayerHudReadModel Read()
            {
                return Value;
            }
        }

        private sealed class MutableObjectiveQuery : IGameplayObjectiveQuery
        {
            public MutableObjectiveQuery(GameplayObjectiveReadModel value)
            {
                Value = value;
            }

            public GameplayObjectiveReadModel Value { get; set; }

            public GameplayObjectiveReadModel Read()
            {
                return Value;
            }
        }

        private sealed class MutableSurfaceButtonRemainderQuery : IGameplaySurfaceButtonRemainderQuery
        {
            public MutableSurfaceButtonRemainderQuery(IReadOnlyList<GameplaySurfaceButtonRemainderReadModel> value)
            {
                Value = value;
            }

            public IReadOnlyList<GameplaySurfaceButtonRemainderReadModel> Value { get; set; }

            public IReadOnlyList<GameplaySurfaceButtonRemainderReadModel> Read()
            {
                return Value ?? Array.Empty<GameplaySurfaceButtonRemainderReadModel>();
            }
        }
    }

    internal sealed class ManualGameplayUiPresentationSource : IGameplayUiPresentationSource
    {
        private event Action<UIPresentationSnapshot> _snapshotChanged;
        private event Action<UITickEventBatch> _tickEventsApplied;
        private event Action<LevelFailedScreenPayload> _levelFailedCommitted;

        public int SnapshotSubscriberCount { get; private set; }

        public int TickEventSubscriberCount { get; private set; }

        public int LevelFailedSubscriberCount { get; private set; }

        public event Action<UIPresentationSnapshot> SnapshotChanged
        {
            add
            {
                SnapshotSubscriberCount++;
                _snapshotChanged += value;
            }
            remove
            {
                SnapshotSubscriberCount--;
                _snapshotChanged -= value;
            }
        }

        public event Action<UITickEventBatch> TickEventsApplied
        {
            add
            {
                TickEventSubscriberCount++;
                _tickEventsApplied += value;
            }
            remove
            {
                TickEventSubscriberCount--;
                _tickEventsApplied -= value;
            }
        }

        public event Action<LevelFailedScreenPayload> LevelFailedCommitted
        {
            add
            {
                LevelFailedSubscriberCount++;
                _levelFailedCommitted += value;
            }
            remove
            {
                LevelFailedSubscriberCount--;
                _levelFailedCommitted -= value;
            }
        }

        public UIPresentationSnapshot CurrentSnapshot { get; private set; } = UIPresentationSnapshot.Empty;

        public UITickEventBatch CurrentTickEvents { get; private set; } = UITickEventBatch.Empty;

        public MinimalStageCompletionReadModel CurrentMinimalStageCompletion { get; private set; }

        public LevelFailedScreenPayload CurrentLevelFailed { get; private set; }

        public void PublishSnapshot(UIPresentationSnapshot snapshot)
        {
            CurrentSnapshot = snapshot;
            _snapshotChanged?.Invoke(snapshot);
        }

        public void PublishTickEvents(UITickEventBatch tickEvents)
        {
            CurrentTickEvents = tickEvents;
            _tickEventsApplied?.Invoke(tickEvents);
        }

        public void PublishMinimalStageCompletion(MinimalStageCompletionReadModel readModel)
        {
            CurrentMinimalStageCompletion = readModel;
        }

        public void PublishLevelFailed(LevelFailedScreenPayload payload)
        {
            CurrentLevelFailed = payload;
            _levelFailedCommitted?.Invoke(payload);
        }

        public void UpdateUiGameplayInputBlocked(bool isUiGameplayInputBlocked)
        {
        }
    }

    internal sealed class FakeStageLaunchRouter : IStageLaunchRouter
    {
        private readonly List<StageNavigationRequest> requests = new();

        public IReadOnlyList<StageNavigationRequest> Requests => requests;

        public void Launch(StageNavigationRequest request)
        {
            requests.Add(request);
        }
    }

    internal sealed class FakeMainMenuReturnRouter : IMainMenuReturnRouter
    {
        public int ReturnCallCount { get; private set; }

        public void ReturnToMainMenu()
        {
            ReturnCallCount++;
        }
    }

    internal sealed class FakePopupRuntimeFactory : IPopupRuntimeFactory
    {
        private readonly Dictionary<PopupId, PopupPolicy> _policies = new()
        {
            {
                PopupId.Pause,
                new PopupPolicy(
                    PopupPolicyClass.ModalBlocking,
                    PopupLifetimeScope.CurrentScreen,
                    PopupBackAction.Close,
                    PopupBackdropMode.Consume,
                    showsDim: true,
                    blocksLowerLayers: true)
            },
            {
                PopupId.ObjectiveInfo,
                new PopupPolicy(
                    PopupPolicyClass.NonModalInformational,
                    PopupLifetimeScope.CurrentScreen,
                    PopupBackAction.Close,
                    PopupBackdropMode.None,
                    showsDim: false,
                    blocksLowerLayers: false)
            },
            {
                PopupId.Confirm,
                new PopupPolicy(
                    PopupPolicyClass.ModalBlocking,
                    PopupLifetimeScope.CurrentScreen,
                    PopupBackAction.Cancel,
                    PopupBackdropMode.Consume,
                    showsDim: true,
                    blocksLowerLayers: true)
            },
            {
                PopupId.Tooltip,
                new PopupPolicy(
                    PopupPolicyClass.AnchoredEphemeral,
                    PopupLifetimeScope.CurrentScreen,
                    PopupBackAction.Close,
                    PopupBackdropMode.None,
                    showsDim: false,
                    blocksLowerLayers: false)
            },
        };

        public List<FakePopupRuntimeRecord> CreatedRuntimes { get; } = new();

        public PopupRuntimeFactoryResult Create(PopupRequest request)
        {
            var runtime = new FakePopupRuntime();
            CreatedRuntimes.Add(new FakePopupRuntimeRecord(request, runtime));
            return new PopupRuntimeFactoryResult(_policies[request.PopupId], runtime);
        }

        public void SetPolicy(PopupId popupId, PopupPolicy policy)
        {
            _policies[popupId] = policy;
        }
    }

    internal readonly struct FakePopupRuntimeRecord
    {
        public FakePopupRuntimeRecord(PopupRequest request, FakePopupRuntime runtime)
        {
            Request = request;
            Runtime = runtime;
        }

        public PopupRequest Request { get; }

        public FakePopupRuntime Runtime { get; }
    }

    internal sealed class FakePopupRuntime : IPopupRuntime
    {
        public event Action<PopupCompletionKind> CompletionRequested;

        public bool IsDisposed { get; private set; }

        public bool IsTopmost { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }

        public void Emit(PopupCompletionKind completionKind)
        {
            CompletionRequested?.Invoke(completionKind);
        }

        public void SetIsTopmost(bool isTopmost)
        {
            IsTopmost = isTopmost;
        }
    }

    internal sealed class FakeScreenRuntimeFactory : IScreenRuntimeFactory
    {
        private readonly Dictionary<ScreenId, ScreenPolicy> _policies = new()
        {
            {
                ScreenId.Gameplay,
                new ScreenPolicy(
                    ScreenPolicyClass.GameplayRoot,
                    ScreenRetentionMode.RetainMountedHistory,
                    ScreenBackAction.None,
                    HudShellMode.Visible,
                    blocksUiGameplayInput: false)
            },
            {
                ScreenId.ObjectiveStatus,
                new ScreenPolicy(
                    ScreenPolicyClass.GameplayAdjacentOverlay,
                    ScreenRetentionMode.RetainMountedHistory,
                    ScreenBackAction.Pop,
                    HudShellMode.Visible,
                    blocksUiGameplayInput: true)
            },
            {
                ScreenId.Settings,
                new ScreenPolicy(
                    ScreenPolicyClass.Configuration,
                    ScreenRetentionMode.RetainMountedHistory,
                    ScreenBackAction.Pop,
                    HudShellMode.Hidden,
                    blocksUiGameplayInput: true)
            },
            {
                ScreenId.StageResult,
                new ScreenPolicy(
                    ScreenPolicyClass.TerminalResult,
                    ScreenRetentionMode.DisposeOnHide,
                    ScreenBackAction.Consume,
                    HudShellMode.Hidden,
                    blocksUiGameplayInput: true)
            },
            {
                ScreenId.LevelFailed,
                new ScreenPolicy(
                    ScreenPolicyClass.TerminalResult,
                    ScreenRetentionMode.DisposeOnHide,
                    ScreenBackAction.Consume,
                    HudShellMode.Hidden,
                    blocksUiGameplayInput: true)
            },
            {
                ScreenId.GameClear,
                new ScreenPolicy(
                    ScreenPolicyClass.TerminalResult,
                    ScreenRetentionMode.DisposeOnHide,
                    ScreenBackAction.Consume,
                    HudShellMode.Hidden,
                    blocksUiGameplayInput: true)
            },
        };

        public List<FakeScreenRuntimeRecord> CreatedRuntimes { get; } = new();

        public ScreenRuntimeFactoryResult Create(ScreenRequest request)
        {
            var runtime = new FakeScreenRuntime();
            CreatedRuntimes.Add(new FakeScreenRuntimeRecord(request, runtime));
            return new ScreenRuntimeFactoryResult(_policies[request.ScreenId], runtime);
        }

        public void SetPolicy(ScreenId screenId, ScreenPolicy policy)
        {
            _policies[screenId] = policy;
        }
    }

    internal readonly struct FakeScreenRuntimeRecord
    {
        public FakeScreenRuntimeRecord(ScreenRequest request, FakeScreenRuntime runtime)
        {
            Request = request;
            Runtime = runtime;
        }

        public ScreenRequest Request { get; }

        public FakeScreenRuntime Runtime { get; }
    }

    internal sealed class FakeScreenRuntime : IScreenRuntime
    {
        public event Action<ScreenAction> ActionRequested;

        public bool IsCurrent { get; private set; }

        public bool IsDisposed { get; private set; }

        public int ApplyPayloadCallCount { get; private set; }

        public IScreenPayload LastPayload { get; private set; }

        public void ApplyPayload(IScreenPayload payload)
        {
            ApplyPayloadCallCount++;
            LastPayload = payload;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }

        public void Emit(ScreenAction action)
        {
            ActionRequested?.Invoke(action);
        }

        public void SetIsCurrent(bool isCurrent)
        {
            IsCurrent = isCurrent;
        }
    }

    internal static class UiTestPortFactory
    {
        public static GameplayUiFlowPorts CreatePorts(
            FakeGameplayCommandGateway commandGateway = null,
            FakeGameplayQueryFacade queryFacade = null,
            FakeGameplayPresentationFeed presentationFeed = null,
            FakeGameplayPauseService pauseService = null)
        {
            commandGateway ??= new FakeGameplayCommandGateway();
            queryFacade ??= new FakeGameplayQueryFacade(
                new GameplaySessionReadModel(1, false, true, false),
                FakeGameplayQueryFacade.CreateDefaultPlayerHud(),
                new GameplayObjectiveReadModel(false, false, false, false));
            pauseService ??= new FakeGameplayPauseService();
            return new GameplayUiFlowPorts(
                commandGateway,
                queryFacade,
                CreatePresentationSource(queryFacade, presentationFeed, pauseService),
                pauseService);
        }

        public static GameplayUiPresentationSource CreatePresentationSource(
            FakeGameplayQueryFacade queryFacade = null,
            FakeGameplayPresentationFeed presentationFeed = null,
            FakeGameplayPauseService pauseService = null)
        {
            queryFacade ??= new FakeGameplayQueryFacade(
                new GameplaySessionReadModel(1, false, true, false),
                FakeGameplayQueryFacade.CreateDefaultPlayerHud(),
                new GameplayObjectiveReadModel(false, false, false, false));
            presentationFeed ??= new FakeGameplayPresentationFeed();
            pauseService ??= new FakeGameplayPauseService();
            return new GameplayUiPresentationSource(queryFacade, presentationFeed, pauseService);
        }
    }
}
