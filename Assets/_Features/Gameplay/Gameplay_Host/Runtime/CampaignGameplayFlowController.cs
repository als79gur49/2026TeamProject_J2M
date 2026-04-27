using System;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Host.UIAccess;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Stages;
using UnityEngine.SceneManagement;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class CampaignGameplayFlowController : IDisposable
    {
        private readonly ActiveSlotProvider _activeSlotProvider;
        private readonly GameplaySceneHost _host;
        private readonly string _sceneName;
        private readonly SaveSlotStore _saveSlotStore;
        private readonly CampaignStageSequenceResolver _sequenceResolver;
        private readonly StageRetryChanceTracker _retryChanceTracker;
        private GameplayHostPresentationFeed _presentationFeed;
        private bool _handledClear;
        private bool _handledDeath;

        public CampaignGameplayFlowController(
            GameplaySceneHost host,
            SaveSlotStore saveSlotStore,
            ActiveSlotProvider activeSlotProvider,
            CampaignStageSequenceResolver sequenceResolver,
            string sceneName)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _saveSlotStore = saveSlotStore ?? throw new ArgumentNullException(nameof(saveSlotStore));
            _activeSlotProvider = activeSlotProvider ?? throw new ArgumentNullException(nameof(activeSlotProvider));
            _sequenceResolver = sequenceResolver ?? throw new ArgumentNullException(nameof(sequenceResolver));
            _sceneName = sceneName ?? string.Empty;
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
                HandlePlayerDeath();
                return;
            }

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

        private void HandlePlayerDeath()
        {
            _handledDeath = true;
            _host.InputHost.EnterTerminalHold();

            var activeSlotNumber = _activeSlotProvider.ActiveSlotNumber;
            var slot = _saveSlotStore.LoadSlot(activeSlotNumber);
            var route = _retryChanceTracker.ResolveDeathRoute(slot);
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
                PublishLevelFailed(route);
                return;
            }

            StageLaunchContextStore.SetCurrent(route.NextStageId);
            SceneManager.LoadScene(_sceneName);
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
                    "level-failed-restart-level")));
        }

        private void HandleStageClearCommitted(TickResult result, StageCompletionReadModel readModel)
        {
            if (_handledClear)
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

            var completedLevelGroupId = _sequenceResolver.GetLevelGroupId(completedStageId);
            var nextLevelGroupId = _sequenceResolver.GetLevelGroupId(nextStageId);
            _saveSlotStore.UpdateSlot(
                activeSlotNumber,
                mutableSlot =>
                {
                    mutableSlot.CurrentStageId = nextStageId;
                    mutableSlot.CurrentLevelGroupId = nextLevelGroupId;
                    if (!string.Equals(completedLevelGroupId, nextLevelGroupId, StringComparison.Ordinal))
                    {
                        mutableSlot.RemainingChances = SaveSlotStore.DefaultRemainingChances;
                    }

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
    }
}
