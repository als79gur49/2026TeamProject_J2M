using System;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Host.UIAccess;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Stages;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class CampaignGameplayFlowController : IDisposable
    {
        private readonly ActiveSlotProvider _activeSlotProvider;
        private readonly CampaignChanceDisplayOverride _chanceDisplayOverride;
        private readonly GameplaySceneHost _host;
        private readonly IStageLaunchRouter _stageLaunchRouter;
        private readonly SaveSlotStore _saveSlotStore;
        private readonly CampaignStageSequenceResolver _sequenceResolver;
        private readonly StageRetryChanceTracker _retryChanceTracker;
        private GameplayHostPresentationFeed _presentationFeed;
        private bool _handledClear;
        private bool _handledDeath;
        private PendingDeathRecoveryState _pendingDeathRecovery;

        public CampaignGameplayFlowController(
            GameplaySceneHost host,
            SaveSlotStore saveSlotStore,
            ActiveSlotProvider activeSlotProvider,
            CampaignStageSequenceResolver sequenceResolver,
            IStageLaunchRouter stageLaunchRouter,
            CampaignChanceDisplayOverride chanceDisplayOverride = null)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _saveSlotStore = saveSlotStore ?? throw new ArgumentNullException(nameof(saveSlotStore));
            _activeSlotProvider = activeSlotProvider ?? throw new ArgumentNullException(nameof(activeSlotProvider));
            _sequenceResolver = sequenceResolver ?? throw new ArgumentNullException(nameof(sequenceResolver));
            _stageLaunchRouter = stageLaunchRouter ?? throw new ArgumentNullException(nameof(stageLaunchRouter));
            _chanceDisplayOverride = chanceDisplayOverride;
            _retryChanceTracker = new StageRetryChanceTracker(_sequenceResolver);
        }

        public void Bind()
        {
            if (_host.InputHost == null)
            {
                throw new InvalidOperationException("Campaign gameplay flow requires an initialized GameplayInputHost.");
            }

            _host.InputHost.TickCompleted += HandleTickCompleted;
            _presentationFeed = _host.UiAccess?.PresentationFeed as GameplayHostPresentationFeed;
            if (_presentationFeed != null)
            {
                _presentationFeed.StageClearCommitted += HandleStageClearCommitted;
            }
        }

        public void Dispose()
        {
            if (_host != null && _host.InputHost != null)
            {
                _host.InputHost.TickCompleted -= HandleTickCompleted;
            }

            if (_presentationFeed != null)
            {
                _presentationFeed.StageClearCommitted -= HandleStageClearCommitted;
            }
        }

        private void HandleTickCompleted(TickResult result)
        {
            if (result == null)
            {
                return;
            }

            if (!_handledDeath && ContainsPlayerDeathSignal(result))
            {
                HandlePlayerDeath(result);
                return;
            }

            TryFlushPendingDeathRecovery(result);
        }

        private bool ContainsPlayerDeathSignal(TickResult result)
        {
            var signals = result.PresentationData.PlayerDeathSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.EntityId == _host.InputHost.PlayerEntityId && signal.DidDieThisTick)
                {
                    return true;
                }
            }

            return false;
        }

        private void HandlePlayerDeath(TickResult result)
        {
            _handledDeath = true;

            var activeSlotNumber = _activeSlotProvider.ActiveSlotNumber;
            var slot = _saveSlotStore.LoadSlot(activeSlotNumber);
            var route = _retryChanceTracker.ResolveDeathRoute(slot);
            var previousRemainingChances = slot.RemainingChances <= 0
                ? SaveSlotStore.DefaultRemainingChances
                : slot.RemainingChances;
            var deathCount = slot.TotalDeaths + 1;
            var routeLevelGroupId = _sequenceResolver.GetLevelGroupId(route.NextStageId);
            _saveSlotStore.UpdateSlot(
                activeSlotNumber,
                mutableSlot =>
                {
                    mutableSlot.CurrentStageId = route.NextStageId;
                    mutableSlot.CurrentLevelGroupId = routeLevelGroupId;
                    mutableSlot.RemainingChances = route.RemainingChances;
                    mutableSlot.TotalDeaths += 1;
                    mutableSlot.LastPlayedAt = DateTimeOffset.UtcNow.ToString("O");
                });

            if (route.RouteKind == StageRetryRouteKind.ReturnToLevelGroupFirstStage)
            {
                _chanceDisplayOverride?.Set(
                    0,
                    SaveSlotStore.DefaultRemainingChances,
                    GameplayChanceAudioPolicy.SuppressChanceChangeCue);
                _pendingDeathRecovery = PendingDeathRecoveryState.CreateLevelFailed(
                    route,
                    result.TickIndex,
                    ResolveDeathRecoveryEligibleTick(result));
                return;
            }

            _host.InputHost.EnterTerminalHold();
            _host.Presenter?.ApplyStageTerminalPresentation(
                GameplayStageTerminalPresentationReason.PlayerDeathRetry,
                result);
            _stageLaunchRouter.Launch(new StageNavigationRequest(
                route.NextStageId,
                StageNavigationKind.Retry,
                "campaign-death-retry",
                StageTransitionHint.ForChanceLost(new StageTransitionChanceLostPayload(
                    previousRemainingChances,
                    route.RemainingChances,
                    SaveSlotStore.DefaultRemainingChances,
                    slot.CurrentStageId,
                    route.NextStageId,
                    deathCount,
                    "campaign-death-retry",
                    "Chance Lost",
                    "Retrying with one fewer chance."))));
        }

        private int ResolveDeathRecoveryEligibleTick(TickResult result)
        {
            var playerEntityId = _host.InputHost.PlayerEntityId;
            var holdSignals = result.PresentationData.PlayerDeathHoldSignals;
            for (var i = 0; i < holdSignals.Count; i++)
            {
                var signal = holdSignals[i];
                if (signal.EntityId == playerEntityId)
                {
                    return signal.EligibleTick;
                }
            }

            return result.TickIndex + Math.Max(1, _host.PlayerRespawnDelayTicks);
        }

        private void TryFlushPendingDeathRecovery(TickResult result)
        {
            if (!_pendingDeathRecovery.HasValue ||
                result.TickIndex < _pendingDeathRecovery.EligibleTick)
            {
                return;
            }

            _host.InputHost.EnterTerminalHold();
            _host.Presenter?.ApplyStageTerminalPresentation(
                GameplayStageTerminalPresentationReason.LevelFailed,
                result);
            PublishLevelFailed(_pendingDeathRecovery.LevelFailedRoute);
            _pendingDeathRecovery = default;
        }

        private void PublishLevelFailed(StageRetryRouteResult route)
        {
            if (_presentationFeed == null)
            {
                throw new InvalidOperationException("Campaign level failed flow requires a gameplay presentation feed.");
            }

            _presentationFeed.PublishLevelFailed(new GameplayLevelFailedReadModel(
                "Level Failed",
                "All chances were used. Restart the level or return to main.",
                "Restart Level",
                "Main",
                new StageNavigationRequest(
                    route.NextStageId,
                    StageNavigationKind.Retry,
                    "level-failed-restart-level",
                    StageTransitionHint.ForKind(StageTransitionKind.LevelFailedRestart))));
        }

        private void HandleStageClearCommitted(TickResult result, StageCompletionReadModel readModel)
        {
            if (_handledClear ||
                _handledDeath ||
                _pendingDeathRecovery.HasValue ||
                (result != null && ContainsPlayerDeathSignal(result)))
            {
                return;
            }

            HandleStageClear(readModel);
        }

        private void HandleStageClear(StageCompletionReadModel readModel)
        {
            _handledClear = true;
            _host.InputHost.EnterTerminalHold();

            var completedStageId = readModel != null && readModel.StageId.IsValid
                ? readModel.StageId
                : ResolveCurrentSlotStageId();
            if (!completedStageId.IsValid)
            {
                throw new InvalidOperationException("Campaign clear flow could not resolve the completed stage id.");
            }

            var activeSlotNumber = _activeSlotProvider.ActiveSlotNumber;
            if (_sequenceResolver.IsFinal(completedStageId))
            {
                _saveSlotStore.UpdateSlot(
                    activeSlotNumber,
                    mutableSlot =>
                    {
                        mutableSlot.CurrentStageId = completedStageId;
                        mutableSlot.CurrentLevelGroupId = _sequenceResolver.GetLevelGroupId(completedStageId);
                        mutableSlot.CampaignCompleted = true;
                        mutableSlot.LastPlayedAt = DateTimeOffset.UtcNow.ToString("O");
                    });
                return;
            }

            if (!_sequenceResolver.TryGetNext(completedStageId, out var nextStageId))
            {
                throw new InvalidOperationException(
                    $"Campaign sequence could not resolve a next stage for '{completedStageId.Value}'.");
            }

            var nextLevelGroupId = _sequenceResolver.GetLevelGroupId(nextStageId);
            _saveSlotStore.UpdateSlot(
                activeSlotNumber,
                mutableSlot =>
                {
                    mutableSlot.CurrentStageId = nextStageId;
                    mutableSlot.CurrentLevelGroupId = nextLevelGroupId;
                    mutableSlot.LastPlayedAt = DateTimeOffset.UtcNow.ToString("O");
                });

        }

        private StageId ResolveCurrentSlotStageId()
        {
            if (!_activeSlotProvider.TryGetActiveSlotNumber(out var activeSlotNumber))
            {
                return StageId.None;
            }

            return _saveSlotStore.LoadSlot(activeSlotNumber).CurrentStageId;
        }

        private enum PendingDeathRecoveryKind
        {
            LevelFailed,
        }

        private readonly struct PendingDeathRecoveryState
        {
            private PendingDeathRecoveryState(
                PendingDeathRecoveryKind kind,
                StageRetryRouteResult levelFailedRoute,
                int deathTick,
                int eligibleTick)
            {
                Kind = kind;
                LevelFailedRoute = levelFailedRoute;
                DeathTick = deathTick;
                EligibleTick = eligibleTick;
            }

            public PendingDeathRecoveryKind Kind { get; }

            public StageRetryRouteResult LevelFailedRoute { get; }

            public int DeathTick { get; }

            public int EligibleTick { get; }

            public bool HasValue => EligibleTick > 0;

            public static PendingDeathRecoveryState CreateLevelFailed(
                StageRetryRouteResult route,
                int deathTick,
                int eligibleTick)
            {
                return new PendingDeathRecoveryState(
                    PendingDeathRecoveryKind.LevelFailed,
                    route,
                    deathTick,
                    eligibleTick);
            }
        }
    }
}
