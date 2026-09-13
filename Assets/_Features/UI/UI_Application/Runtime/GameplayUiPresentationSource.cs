using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Gameplay.UIAccess.Queries;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Gameplay.UIAccess.Presentation;
using Game.Feature.Stages;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Application
{
    public interface IGameplayUiPresentationSource
    {
        event Action<UIPresentationSnapshot> SnapshotChanged;

        event Action<UITickEventBatch> TickEventsApplied;

        event Action<LevelFailedScreenPayload> LevelFailedCommitted;

        UIPresentationSnapshot CurrentSnapshot { get; }

        UITickEventBatch CurrentTickEvents { get; }

        MinimalStageCompletionReadModel CurrentMinimalStageCompletion { get; }

        LevelFailedScreenPayload CurrentLevelFailed { get; }

        void UpdateUiGameplayInputBlocked(bool isUiGameplayInputBlocked);
    }

    public sealed class GameplayUiPresentationSource : IGameplayUiPresentationSource, IGameplayHudPendingRefresh, IDisposable
    {
        private readonly UITickEventRouter _eventRouter;
        private readonly IGameplayPauseService _pauseService;
        private readonly IGameplayPresentationFeed _presentationFeed;
        private readonly IGameplayQueryFacade _queryFacade;
        private readonly UIStateMapper _stateMapper;
        private bool _isUiGameplayInputBlocked;
        private IGameplayHudChanceChanges _chanceChanges;
        private long _inputChanceRevision = -1, _handledChanceRevision = -1, _autoAttemptedChanceRevision = -1;
        private long _preparedChanceRevision = -1;
        private GameplayPlayerHudReadModel _preparedPlayerHud;
        private bool _isFlushingChance, _isDisposed;
        private HudQueryCache<GameplayStageReadModel> _stageReads;
        private HudQueryCache<GameplayObjectiveReadModel> _objectiveReads;
        private HudQueryCache<GameplayPlayerHudReadModel> _playerReads;
        private HudQueryCache<IReadOnlyList<GameplaySurfaceButtonRemainderReadModel>> _surfaceReads;
        private long _uncachedChanceRevision = -1;
        private GameplayHudQueryStamp _preparedHudStamp;
        private bool _hasPreparedHudStamp;

        public GameplayUiPresentationSource(
            IGameplayQueryFacade queryFacade,
            IGameplayPresentationFeed presentationFeed,
            IGameplayPauseService pauseService,
            UITickEventRouter eventRouter = null,
            UIStateMapper stateMapper = null)
        {
            _queryFacade = queryFacade ?? throw new ArgumentNullException(nameof(queryFacade));
            _presentationFeed = presentationFeed ?? throw new ArgumentNullException(nameof(presentationFeed));
            _pauseService = pauseService ?? throw new ArgumentNullException(nameof(pauseService));
            _eventRouter = eventRouter ?? new UITickEventRouter();
            _stateMapper = stateMapper ?? new UIStateMapper();

            CurrentSnapshot = _stateMapper.ReduceRefresh(
                UIPresentationSnapshot.Empty,
                CreateRefreshInput(
                    frame: null,
                    shouldUpdateTickIndex: false,
                    shouldUpdateFinalTopology: true)).Snapshot;
            _handledChanceRevision = _inputChanceRevision;
            CurrentTickEvents = UITickEventBatch.Empty;

            _presentationFeed.FramePublished += HandleFramePublished;
            _presentationFeed.StateChanged += HandlePresentationStateChanged;
            _presentationFeed.LevelFailedCommitted += HandleLevelFailedCommitted;
            _pauseService.PauseChanged += HandlePauseChanged;
        }

        public event Action<UIPresentationSnapshot> SnapshotChanged;

        public event Action<UITickEventBatch> TickEventsApplied;

        public event Action<LevelFailedScreenPayload> LevelFailedCommitted;

        public UIPresentationSnapshot CurrentSnapshot { get; private set; }

        public UITickEventBatch CurrentTickEvents { get; private set; }

        public MinimalStageCompletionReadModel CurrentMinimalStageCompletion => _presentationFeed.CurrentMinimalStageCompletion;

        public LevelFailedScreenPayload CurrentLevelFailed { get; private set; }

        public void Dispose()
        {
            _isDisposed = true;
            InvalidateHudQueries();
            _preparedChanceRevision = -1;
            _presentationFeed.FramePublished -= HandleFramePublished;
            _presentationFeed.StateChanged -= HandlePresentationStateChanged;
            _presentationFeed.LevelFailedCommitted -= HandleLevelFailedCommitted;
            _pauseService.PauseChanged -= HandlePauseChanged;
        }

        public void FlushPendingChanceChanges()
        {
            if (_isDisposed || _isFlushingChance || _chanceChanges == null ||
                _chanceChanges.IsChanceDisplayUpdating ||
                !_chanceChanges.TryGetChanceRevision(out var revision) ||
                revision == _handledChanceRevision || revision == _autoAttemptedChanceRevision) return;

#if VECTORQUAKE_CAPTURE_BUILD
            using var capture = UiCallbackCapture.Measure(UiCallbackSection.UiRefreshHandling, UiCallbackOrigin.PresentStateChanged);
#endif
            _isFlushingChance = true;
            var attempted = revision;
            try
            {
                var player = ReadPlayerHudForRefresh(out attempted, force: true);
                _hasPreparedHudStamp = _playerReads?.Query is IGameplayHudRevisionProbe;
                if (_playerReads?.Query is IGameplayHudRevisionProbe preparedProbe)
                    preparedProbe.TryGetRevision(out _preparedHudStamp);
                if (_chanceChanges.IsChanceDisplayUpdating) return;
                var chance = new UIChanceSlice(player.HasRemainingChances, player.RemainingChances,
                    player.MaxChances, player.ChanceAudioPolicy);
                if (CurrentSnapshot.Chance.Equals(chance))
                {
                    _handledChanceRevision = Math.Max(_handledChanceRevision, attempted);
                    return;
                }
                _preparedPlayerHud = player;
                _preparedChanceRevision = attempted;
                PublishSnapshot(_stateMapper.ReduceRefresh(CurrentSnapshot, CreateRefreshInput(
                    frame: null, shouldUpdateTickIndex: false, shouldUpdateFinalTopology: false)).Snapshot);
            }
            finally
            {
                if (attempted < 0) attempted = revision;
                _autoAttemptedChanceRevision = attempted;
                _preparedChanceRevision = -1;
                _isFlushingChance = false;
            }
        }

        public void InvalidateHudQueries()
        {
            _stageReads?.Invalidate();
            _objectiveReads?.Invalidate();
            _playerReads?.Invalidate();
            _surfaceReads?.Invalidate();
        }

        private GameplayStageReadModel ReadStageForRefresh()
        {
            var query = _queryFacade.Stage;
            if (_stageReads == null || !ReferenceEquals(_stageReads.Query, query))
            {
                _stageReads = new HudQueryCache<GameplayStageReadModel>(query, query.Read);
#if VECTORQUAKE_CAPTURE_BUILD
                _stageReads.CaptureSection = UiCallbackSection.QueryStage;
#endif
            }
            return _stageReads.Read().Value;
        }
        private GameplayObjectiveReadModel ReadObjectiveForRefresh()
        {
            var query = _queryFacade.Objectives;
            if (_objectiveReads == null || !ReferenceEquals(_objectiveReads.Query, query))
            {
                _objectiveReads = new HudQueryCache<GameplayObjectiveReadModel>(query, query.Read);
#if VECTORQUAKE_CAPTURE_BUILD
                _objectiveReads.CaptureSection = UiCallbackSection.QueryObjectives;
#endif
            }
            return _objectiveReads.Read().Value;
        }
        private IReadOnlyList<GameplaySurfaceButtonRemainderReadModel> ReadSurfaceForRefresh()
        {
            var query = _queryFacade.SurfaceButtonRemainders;
            if (_surfaceReads == null || !ReferenceEquals(_surfaceReads.Query, query))
            {
                _surfaceReads = new HudQueryCache<IReadOnlyList<GameplaySurfaceButtonRemainderReadModel>>(query, query.Read);
#if VECTORQUAKE_CAPTURE_BUILD
                _surfaceReads.CaptureSection = UiCallbackSection.QuerySurfaceButtonRemainders;
#endif
            }
            return _surfaceReads.Read().Value;
        }
        private GameplayPlayerHudReadModel ReadUncachedPlayerHud(IGameplayPlayerHudQuery query)
        {
            if (query is IGameplayHudChanceChanges changes && changes.TryGetChanceRevision(out _))
            {
                var attempted = -1L;
                try { return changes.ReadChance(out attempted); }
                finally { _uncachedChanceRevision = attempted; }
            }
            return query.Read();
        }
        private GameplayPlayerHudReadModel ReadPlayerHudForRefresh(out long revision, bool force = false)
        {
            revision = -1;
            var playerQuery = _queryFacade.PlayerHud;
            _chanceChanges = playerQuery as IGameplayHudChanceChanges;
            if (_playerReads == null || !ReferenceEquals(_playerReads.Query, playerQuery))
            {
                _playerReads = new HudQueryCache<GameplayPlayerHudReadModel>(playerQuery, () => ReadUncachedPlayerHud(playerQuery));
#if VECTORQUAKE_CAPTURE_BUILD
                _playerReads.CaptureSection = UiCallbackSection.QueryPlayerHud;
#endif
            }
            if (!force && _preparedChanceRevision >= 0 && _chanceChanges != null &&
                _chanceChanges.TryGetChanceRevision(out var current) && _preparedChanceRevision == current)
            {
                var sameWindow = true;
                if (_hasPreparedHudStamp && playerQuery is IGameplayHudRevisionProbe probe)
                {
                    probe.TryGetRevision(out var now);
                    sameWindow = now.Equals(_preparedHudStamp);
                }
                if (sameWindow) { revision = current; return _preparedPlayerHud; }
            }
            _uncachedChanceRevision = -1;
            try
            {
                var read = _playerReads.Read(force);
                revision = read.CanReuse ? read.Stamp.ChanceRevision : _uncachedChanceRevision;
                return read.Value;
            }
            catch
            {
                if (_uncachedChanceRevision < 0 && playerQuery is IGameplayHudRevisionedQuery<GameplayPlayerHudReadModel>)
                    _chanceChanges?.TryGetChanceRevision(out _uncachedChanceRevision);
                throw;
            }
            finally { if (revision < 0) revision = _uncachedChanceRevision; }
        }

        public void UpdateUiGameplayInputBlocked(bool isUiGameplayInputBlocked)
        {
            if (_isUiGameplayInputBlocked == isUiGameplayInputBlocked)
            {
                return;
            }

            _isUiGameplayInputBlocked = isUiGameplayInputBlocked;
            PublishSnapshot(
                _stateMapper.ReduceRefresh(
                    CurrentSnapshot,
                    CreateRefreshInput(
                        frame: null,
                        shouldUpdateTickIndex: false,
                        shouldUpdateFinalTopology: false)).Snapshot);
        }

        private void HandleFramePublished(GameplayPresentationFrame frame)
        {
            if (frame.TickIndex < CurrentSnapshot.Tick.LastReducedTickIndex)
            {
                PublishSnapshot(
                    _stateMapper.ReduceRefresh(
                        CurrentSnapshot,
                        CreateRefreshInput(
                            frame: null,
                            shouldUpdateTickIndex: false,
                            shouldUpdateFinalTopology: false)).Snapshot);
                return;
            }

            var reduction = _stateMapper.ReduceTick(
                CurrentSnapshot,
                CreateRefreshInput(
                    frame,
                    shouldUpdateTickIndex: true,
                    shouldUpdateFinalTopology: true),
                _eventRouter.Route(frame));

            PublishSnapshot(reduction.Snapshot);
            PublishTickEvents(frame.TickIndex, reduction.AppliedEvents);
        }

        private void HandlePauseChanged(bool _)
        {
            PublishSnapshot(
                _stateMapper.ReduceRefresh(
                    CurrentSnapshot,
                    CreateRefreshInput(
                        frame: null,
                        shouldUpdateTickIndex: false,
                        shouldUpdateFinalTopology: false)).Snapshot);
        }

        private void HandlePresentationStateChanged(GameplayPresentationState _)
        {
            PublishSnapshot(
                _stateMapper.ReduceRefresh(
                    CurrentSnapshot,
                    CreateRefreshInput(
                        frame: null,
                        shouldUpdateTickIndex: false,
                        shouldUpdateFinalTopology: false)).Snapshot);
        }

        private void HandleLevelFailedCommitted(GameplayLevelFailedReadModel readModel)
        {
            CurrentLevelFailed = LevelFailedPayloadMapper.Map(readModel);
            LevelFailedCommitted?.Invoke(CurrentLevelFailed);
        }

        private UIStateRefreshInput CreateRefreshInput(
            GameplayPresentationFrame? frame,
            bool shouldUpdateTickIndex,
            bool shouldUpdateFinalTopology)
        {
            long chanceRevision;
            var session = _queryFacade.Session.Read();
            var stage = ReadStageForRefresh();
            var objective = ReadObjectiveForRefresh();
            var playerHud = ReadPlayerHudForRefresh(out chanceRevision);
            var surfaceButtonRemainders = ReadSurfaceForRefresh();
            _inputChanceRevision = _chanceChanges?.IsChanceDisplayUpdating == true ? -1 : chanceRevision;
            var hasStageClearFrame =
                frame.HasValue &&
                frame.Value.StageEvent.HasValue &&
                frame.Value.StageEvent.Value.EventKind == GameplayStageEventKind.Cleared;
            var isStageCleared =
                hasStageClearFrame ||
                (session.IsStageCleared && !_presentationFeed.HasPendingStageClearPresentation);

            return new UIStateRefreshInput(
                tickIndex: shouldUpdateTickIndex && frame.HasValue ? frame.Value.TickIndex : 0,
                shouldUpdateTickIndex,
                finalTopology: shouldUpdateFinalTopology && frame.HasValue
                    ? frame.Value.FinalTopology
                    : _presentationFeed.CurrentState.CurrentTopology,
                shouldUpdateFinalTopology,
                isStageCleared,
                _presentationFeed.CurrentState.IsTopologyTransitionActive,
                _presentationFeed.CurrentState.HasBlockingPresentation,
                _pauseService.IsPaused,
                session.CanAcceptGameplayCommands,
                _isUiGameplayInputBlocked,
                playerHud.HasRemainingChances,
                playerHud.RemainingChances,
                playerHud.MaxChances,
                stage.StageId,
                stage.DisplayNameKey,
                objective,
                frame.HasValue ? frame.Value.Topology : null,
                playerHud.ChanceAudioPolicy,
                MapSurfaceButtonRemainders(surfaceButtonRemainders));
        }

        private static UISurfaceButtonRemainderInput[] MapSurfaceButtonRemainders(
            IReadOnlyList<GameplaySurfaceButtonRemainderReadModel> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<UISurfaceButtonRemainderInput>();
            }

            var result = new UISurfaceButtonRemainderInput[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                result[i] = new UISurfaceButtonRemainderInput(
                    item.Face,
                    item.NormalRemaining,
                    item.MoonBlockOnlyRemaining);
            }

            return result;
        }

        private void PublishSnapshot(UIPresentationSnapshot nextSnapshot)
        {
            var revision = _inputChanceRevision;
            PublishSnapshotCore(nextSnapshot);
            if (_chanceChanges?.IsChanceDisplayUpdating != true)
                _handledChanceRevision = Math.Max(_handledChanceRevision, revision);
        }

        private void PublishSnapshotCore(UIPresentationSnapshot nextSnapshot)
        {
            if (CurrentSnapshot.Equals(nextSnapshot))
            {
                return;
            }

            CurrentSnapshot = nextSnapshot;
            SnapshotChanged?.Invoke(CurrentSnapshot);
        }

        private void PublishTickEvents(int tickIndex, System.Collections.Generic.IReadOnlyList<UITickEvent> appliedEvents)
        {
            CurrentTickEvents = appliedEvents.Count == 0
                ? UITickEventBatch.Empty
                : new UITickEventBatch(tickIndex, appliedEvents);
            if (!CurrentTickEvents.HasAnyEvents)
            {
                return;
            }

            TickEventsApplied?.Invoke(CurrentTickEvents);
        }
    }
}
