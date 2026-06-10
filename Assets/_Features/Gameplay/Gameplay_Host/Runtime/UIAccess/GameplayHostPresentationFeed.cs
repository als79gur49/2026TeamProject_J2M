using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Gameplay.UIAccess.Presentation;
using Game.Feature.Stages;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    internal sealed class GameplayHostPresentationFeed : IGameplayPresentationFeed, IDisposable
    {
        private readonly GameplayInputHost _inputHost;
        private readonly GameplayHostStageCompletionRuntime _stageCompletionRuntime;
        private readonly GameplayTickViewPresenter _presenter;
        private readonly GameplayTimingProfile _timingProfile;
        private readonly GameplayPresentationBarrierTracker _barrierTracker;
        private PendingStageClearPresentation _pendingStageClearPresentation;
        private TickResult _lastTickResult;

        public GameplayHostPresentationFeed(
            GameplayInputHost inputHost,
            GameplayTickViewPresenter presenter,
            StageContentEntry stageContentEntry = null,
            IStageCompletionProfileStore stageCompletionProfileStore = null,
            GameplayTimingProfile timingProfile = null,
            GameplayPresentationBarrierTracker barrierTracker = null)
        {
            _inputHost = inputHost ?? throw new ArgumentNullException(nameof(inputHost));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _stageCompletionRuntime = new GameplayHostStageCompletionRuntime(stageContentEntry, stageCompletionProfileStore);
            _timingProfile = timingProfile ?? GameplayTimingProfile.CreateDefault();
            _barrierTracker = barrierTracker ?? new GameplayPresentationBarrierTracker();
            CurrentState = CreateCurrentState();

            _inputHost.TickCompleted += HandleTickCompleted;
            _presenter.PresentationStateChanged += HandlePresentationStateChanged;
            _presenter.PresentationAdvanced += HandlePresentationAdvanced;
        }

        public event Action<GameplayPresentationFrame> FramePublished;

        public event Action<GameplayPresentationState> StateChanged;

        public event Action<GameplayLevelFailedReadModel> LevelFailedCommitted;

        internal event Action<TickResult, MinimalStageCompletionReadModel> StageClearCommitted;

        public GameplayPresentationState CurrentState { get; private set; }

        public MinimalStageCompletionReadModel CurrentMinimalStageCompletion => _stageCompletionRuntime.CurrentMinimalStageCompletion;

        public StageCompletionReadModel CurrentStageCompletion => _stageCompletionRuntime.CurrentStageCompletion;

        public bool IsStageCompletionInProgress => _stageCompletionRuntime.IsCompletionInProgress;

        public GameplayLevelFailedReadModel CurrentLevelFailed { get; private set; }

        public bool HasPendingStageClearPresentation => _pendingStageClearPresentation.HasValue;

        internal MinimalStageCompletionReadModel ForceClearCurrentStage()
        {
            var readModel = _stageCompletionRuntime.ForceClearCurrentStage();
            StageClearCommitted?.Invoke(null, readModel);
            FramePublished?.Invoke(new GameplayPresentationFrame(
                Math.Max(1, readModel.Result.FinalTickIndex),
                CurrentState.CurrentTopology,
                stageEvent: new GameplayStageEventPresentationSlice(GameplayStageEventKind.Cleared)));
            return readModel;
        }

        internal void PublishLevelFailed(GameplayLevelFailedReadModel readModel)
        {
            CurrentLevelFailed = readModel ?? throw new ArgumentNullException(nameof(readModel));
            LevelFailedCommitted?.Invoke(CurrentLevelFailed);
        }

        public void Dispose()
        {
            _inputHost.TickCompleted -= HandleTickCompleted;
            _presenter.PresentationStateChanged -= HandlePresentationStateChanged;
            _presenter.PresentationAdvanced -= HandlePresentationAdvanced;
        }

        private void HandleTickCompleted(TickResult result)
        {
            if (result == null)
            {
                return;
            }

            _lastTickResult = result;
            var stageCompletion = _stageCompletionRuntime.ProcessTick(result);
            var maxBarrierDelaySeconds = _barrierTracker.RegisterFromTick(result, _timingProfile);
            var stageClearVictoryDelaySeconds = Math.Max(
                0f,
                _presenter.LastStageClearPlayerPresentationDelaySeconds);
            if (result.ObjectiveResult != null && result.ObjectiveResult.ClearedThisTick)
            {
                StageClearCommitted?.Invoke(result, stageCompletion);
                var clearDelaySeconds = Math.Max(maxBarrierDelaySeconds, stageClearVictoryDelaySeconds);
                if (clearDelaySeconds > 0f)
                {
                    _pendingStageClearPresentation = new PendingStageClearPresentation(
                        result,
                        clearDelaySeconds);
                    FramePublished?.Invoke(CreateFrame(result, includeStageEvent: false));
                    return;
                }
            }

            FramePublished?.Invoke(CreateFrame(result, includeStageEvent: true));
        }

        private void HandlePresentationAdvanced(float deltaTime)
        {
            var barriersChanged = _barrierTracker.Advance(deltaTime);
            if (!_pendingStageClearPresentation.HasValue)
            {
                if (barriersChanged && _lastTickResult != null)
                {
                    FramePublished?.Invoke(CreateFrame(_lastTickResult, includeStageEvent: false));
                }

                return;
            }

            var pending = _pendingStageClearPresentation.Advance(deltaTime);
            if (pending.RemainingSeconds > 0f)
            {
                _pendingStageClearPresentation = pending;
                return;
            }

            _pendingStageClearPresentation = default;
            FramePublished?.Invoke(CreateFrame(pending.Result, includeStageEvent: true));
        }

        private void HandlePresentationStateChanged()
        {
            var nextState = CreateCurrentState();
            if (PresentationStatesEqual(CurrentState, nextState))
            {
                return;
            }

            CurrentState = nextState;
            StateChanged?.Invoke(CurrentState);
        }

        private GameplayPresentationState CreateCurrentState()
        {
            return new GameplayPresentationState(
                GameplayUiAccessMapper.ToUiTopology(_presenter.CurrentTopology),
                _presenter.IsPresentationActive,
                _presenter.HasBlockingPresentation,
                _presenter.IsTopologyTransitionActive);
        }

        private GameplayPresentationFrame CreateFrame(TickResult result, bool includeStageEvent)
        {
            GameplayTopologyPresentationSlice? topology = null;
            if (result.PresentationData.TopologyMotion.HasValue)
            {
                var topologyMotion = result.PresentationData.TopologyMotion.Value;
                topology = new GameplayTopologyPresentationSlice(
                    GameplayUiAccessMapper.ToUiTopology(topologyMotion.SourceTopology),
                    GameplayUiAccessMapper.ToUiTopology(topologyMotion.DestinationTopology),
                    GameplayUiAccessMapper.ToUiRotationKind(topologyMotion.RotationKind));
            }

            var player = BuildPlayerSlice(result, _inputHost.PlayerEntityId);
            GameplayStageEventPresentationSlice? stageEvent = null;
            if (includeStageEvent &&
                result.ObjectiveResult != null &&
                result.ObjectiveResult.ClearedThisTick)
            {
                stageEvent = new GameplayStageEventPresentationSlice(GameplayStageEventKind.Cleared);
            }

            return new GameplayPresentationFrame(
                result.TickIndex,
                GameplayUiAccessMapper.ToUiTopology(result.FinalTopology),
                topology,
                player,
                stageEvent);
        }

        private static GameplayPlayerPresentationSlice? BuildPlayerSlice(TickResult result, int playerEntityId)
        {
            if (playerEntityId <= 0)
            {
                return null;
            }

            var playerActionSignal = default(TickPlayerActionPresentationSignal);
            var hasPlayerActionSignal = TryFindPlayerActionSignal(result, playerEntityId, out playerActionSignal);
            var locomotionSignal = default(TickPlayerLocomotionPresentationSignal);
            var hasLocomotionSignal = TryFindPlayerLocomotionSignal(result, playerEntityId, out locomotionSignal);
            var damageSignal = default(TickPlayerDamagePresentationSignal);
            var hasDamageSignal = TryFindPlayerDamageSignal(result, playerEntityId, out damageSignal);

            if (!hasPlayerActionSignal &&
                !hasLocomotionSignal &&
                !hasDamageSignal)
            {
                return null;
            }

            return new GameplayPlayerPresentationSlice(
                playerEntityId,
                hasPlayerActionSignal
                    ? GameplayUiAccessMapper.ToUiActionKind(playerActionSignal.ActiveActionKind)
                    : GameplayUiActionKind.None,
                hasPlayerActionSignal ? playerActionSignal.ActiveActionSequence : 0,
                hasPlayerActionSignal
                    ? GameplayUiAccessMapper.ToUiDirection(playerActionSignal.Direction)
                    : GameplayUiDirection.None,
                hasPlayerActionSignal ? playerActionSignal.TargetEntityId : 0,
                hasPlayerActionSignal && playerActionSignal.StartedThisTick,
                hasPlayerActionSignal && playerActionSignal.ExecutedThisTick,
                hasPlayerActionSignal && playerActionSignal.CompletedThisTick,
                hasPlayerActionSignal && playerActionSignal.CanceledThisTick,
                hasPlayerActionSignal && playerActionSignal.IsRecoveryPhase,
                hasPlayerActionSignal
                    ? GameplayUiAccessMapper.ToUiActionResolutionKind(playerActionSignal.ResolutionKind)
                    : GameplayUiActionResolutionKind.None,
                hasLocomotionSignal && locomotionSignal.ShouldPlayWalkLoop,
                hasLocomotionSignal && locomotionSignal.MoveMotionGeneratedThisTick,
                hasLocomotionSignal && locomotionSignal.WaitingForNextMoveCadence,
                hasDamageSignal && damageSignal.TookDamageThisTick,
                hasDamageSignal ? damageSignal.DamageAmount : 0);
        }

        private static bool TryFindPlayerActionSignal(
            TickResult result,
            int playerEntityId,
            out TickPlayerActionPresentationSignal signal)
        {
            var signals = result.PresentationData.PlayerActionSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                if (signals[i].EntityId == playerEntityId)
                {
                    signal = signals[i];
                    return true;
                }
            }

            signal = default;
            return false;
        }

        private static bool TryFindPlayerLocomotionSignal(
            TickResult result,
            int playerEntityId,
            out TickPlayerLocomotionPresentationSignal signal)
        {
            var signals = result.PresentationData.PlayerLocomotionSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                if (signals[i].EntityId == playerEntityId)
                {
                    signal = signals[i];
                    return true;
                }
            }

            signal = default;
            return false;
        }

        private static bool TryFindPlayerDamageSignal(
            TickResult result,
            int playerEntityId,
            out TickPlayerDamagePresentationSignal signal)
        {
            var signals = result.PresentationData.PlayerDamageSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                if (signals[i].EntityId == playerEntityId)
                {
                    signal = signals[i];
                    return true;
                }
            }

            signal = default;
            return false;
        }

        private static bool PresentationStatesEqual(
            GameplayPresentationState left,
            GameplayPresentationState right)
        {
            return left.CurrentTopology.Equals(right.CurrentTopology) &&
                   left.IsPresentationActive == right.IsPresentationActive &&
                   left.HasBlockingPresentation == right.HasBlockingPresentation &&
                   left.IsTopologyTransitionActive == right.IsTopologyTransitionActive;
        }

        private readonly struct PendingStageClearPresentation
        {
            public PendingStageClearPresentation(TickResult result, float remainingSeconds)
            {
                Result = result;
                RemainingSeconds = Math.Max(0f, remainingSeconds);
            }

            public TickResult Result { get; }

            public float RemainingSeconds { get; }

            public bool HasValue => Result != null;

            public PendingStageClearPresentation Advance(float deltaTime)
            {
                return new PendingStageClearPresentation(Result, RemainingSeconds - Math.Max(0f, deltaTime));
            }
        }
    }

    internal sealed class GameplayPresentationBarrierTracker
    {
        private readonly List<PendingBarrier> _pendingBarriers = new();

        public int Version { get; private set; }

        public float RegisterFromTick(TickResult result, GameplayTimingProfile timingProfile)
        {
            if (result == null)
            {
                return 0f;
            }

            var maxDelaySeconds = 0f;
            var tileEvents = result.PresentationData.TileEvents;
            for (var i = 0; i < tileEvents.Count; i++)
            {
                var tileEvent = tileEvents[i];
                if (tileEvent.BarrierKey.Kind != PresentationBarrierKind.ButtonActivated)
                {
                    continue;
                }

                var delaySeconds = PresentationTimingResolver.ResolveDelaySeconds(
                    tileEvent.TimingAnchor,
                    timingProfile);
                if (delaySeconds <= 0f)
                {
                    continue;
                }

                _pendingBarriers.Add(new PendingBarrier(
                    tileEvent.BarrierKey,
                    result.TickIndex,
                    tileEvent.TimingAnchor.ActionPlanId,
                    tileEvent.TimingAnchor.LocalActionIndex,
                    tileEvent.Cell,
                    tileEvent.EventKind,
                    tileEvent.SourceEntityId,
                    delaySeconds));
                maxDelaySeconds = Math.Max(maxDelaySeconds, delaySeconds);
            }

            if (maxDelaySeconds <= 0f)
            {
                return 0f;
            }

            _pendingBarriers.Sort(ComparePendingBarriers);
            Version++;
            return maxDelaySeconds;
        }

        public bool Advance(float deltaTime)
        {
            if (_pendingBarriers.Count == 0)
            {
                return false;
            }

            var changed = false;
            var boundedDeltaTime = Math.Max(0f, deltaTime);
            for (var i = _pendingBarriers.Count - 1; i >= 0; i--)
            {
                var advanced = _pendingBarriers[i].Advance(boundedDeltaTime);
                if (advanced.RemainingSeconds > 0f)
                {
                    _pendingBarriers[i] = advanced;
                    continue;
                }

                _pendingBarriers.RemoveAt(i);
                changed = true;
            }

            if (changed)
            {
                Version++;
            }

            return changed;
        }

        public bool IsButtonActivationPending(int tileId)
        {
            if (tileId <= 0)
            {
                return false;
            }

            for (var i = 0; i < _pendingBarriers.Count; i++)
            {
                var barrier = _pendingBarriers[i];
                if (barrier.Key.Kind == PresentationBarrierKind.ButtonActivated &&
                    barrier.Key.PrimaryId == tileId)
                {
                    return true;
                }
            }

            return false;
        }

        private static int ComparePendingBarriers(PendingBarrier left, PendingBarrier right)
        {
            var result = left.TickIndex.CompareTo(right.TickIndex);
            if (result != 0)
            {
                return result;
            }

            result = left.ActionPlanId.CompareTo(right.ActionPlanId);
            if (result != 0)
            {
                return result;
            }

            result = left.LocalActionIndex.CompareTo(right.LocalActionIndex);
            if (result != 0)
            {
                return result;
            }

            result = left.Cell.face.CompareTo(right.Cell.face);
            if (result != 0)
            {
                return result;
            }

            result = left.Cell.x.CompareTo(right.Cell.x);
            if (result != 0)
            {
                return result;
            }

            result = left.Cell.y.CompareTo(right.Cell.y);
            if (result != 0)
            {
                return result;
            }

            result = left.EventKind.CompareTo(right.EventKind);
            if (result != 0)
            {
                return result;
            }

            return left.EntityId.CompareTo(right.EntityId);
        }

        private readonly struct PendingBarrier
        {
            public PendingBarrier(
                PresentationBarrierKey key,
                int tickIndex,
                int actionPlanId,
                int localActionIndex,
                SurfaceCell cell,
                TilePresentationEventKind eventKind,
                int entityId,
                float remainingSeconds)
            {
                Key = key;
                TickIndex = tickIndex;
                ActionPlanId = actionPlanId;
                LocalActionIndex = localActionIndex;
                Cell = cell;
                EventKind = eventKind;
                EntityId = entityId;
                RemainingSeconds = Math.Max(0f, remainingSeconds);
            }

            public PresentationBarrierKey Key { get; }

            public int TickIndex { get; }

            public int ActionPlanId { get; }

            public int LocalActionIndex { get; }

            public SurfaceCell Cell { get; }

            public TilePresentationEventKind EventKind { get; }

            public int EntityId { get; }

            public float RemainingSeconds { get; }

            public PendingBarrier Advance(float deltaTime)
            {
                return new PendingBarrier(
                    Key,
                    TickIndex,
                    ActionPlanId,
                    LocalActionIndex,
                    Cell,
                    EventKind,
                    EntityId,
                    RemainingSeconds - Math.Max(0f, deltaTime));
            }
        }
    }
}
