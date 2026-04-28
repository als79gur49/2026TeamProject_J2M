using System;
using Game.Feature.Gameplay.UIAccess.Contracts;
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

        StageCompletionReadModel CurrentStageCompletion { get; }

        LevelFailedScreenPayload CurrentLevelFailed { get; }

        void UpdateUiGameplayInputBlocked(bool isUiGameplayInputBlocked);
    }

    public sealed class GameplayUiPresentationSource : IGameplayUiPresentationSource, IDisposable
    {
        private readonly UITickEventRouter _eventRouter;
        private readonly IGameplayPauseService _pauseService;
        private readonly IGameplayPresentationFeed _presentationFeed;
        private readonly IGameplayQueryFacade _queryFacade;
        private readonly UIStateMapper _stateMapper;
        private bool _isUiGameplayInputBlocked;

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

        public StageCompletionReadModel CurrentStageCompletion => _presentationFeed.CurrentStageCompletion;

        public LevelFailedScreenPayload CurrentLevelFailed { get; private set; }

        public void Dispose()
        {
            _presentationFeed.FramePublished -= HandleFramePublished;
            _presentationFeed.StateChanged -= HandlePresentationStateChanged;
            _presentationFeed.LevelFailedCommitted -= HandleLevelFailedCommitted;
            _pauseService.PauseChanged -= HandlePauseChanged;
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
            var session = _queryFacade.Session.Read();
            var playerHud = _queryFacade.PlayerHud.Read();
            var player = frame.HasValue && frame.Value.Player.HasValue
                ? frame.Value.Player.Value
                : default;
            var hasFramePlayer = frame.HasValue && frame.Value.Player.HasValue;
            var isStageCleared =
                session.IsStageCleared ||
                (frame.HasValue &&
                 frame.Value.StageEvent.HasValue &&
                 frame.Value.StageEvent.Value.EventKind == GameplayStageEventKind.Cleared);

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
                playerHud.PlayerEntityId,
                playerHud.CurrentHp,
                playerHud.MaxHp,
                playerHud.Facing,
                playerHud.ActiveActionKind,
                hasFramePlayer ? player.IsRecoveryPhase : playerHud.IsActionInRecoveryPhase,
                playerHud.CanMoveThisTick,
                playerHud.CanStartActionThisTick,
                MapRecoveryCooldown(playerHud.RecoveryCooldown),
                playerHud.CanStartAnyActionThisTick,
                playerHud.HasExplicitPushCandidateInCurrentDirection,
                playerHud.HasRemainingChances,
                playerHud.RemainingChances,
                playerHud.MaxChances);
        }

        private static UIRecoveryCooldownSlice? MapRecoveryCooldown(GameplayUiRecoveryCooldown? recoveryCooldown)
        {
            if (!recoveryCooldown.HasValue)
            {
                return null;
            }

            var value = recoveryCooldown.Value;
            return new UIRecoveryCooldownSlice(
                value.ActionKind,
                value.RemainingRecoveryTicks,
                value.TotalRecoveryTicks);
        }

        private void PublishSnapshot(UIPresentationSnapshot nextSnapshot)
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
