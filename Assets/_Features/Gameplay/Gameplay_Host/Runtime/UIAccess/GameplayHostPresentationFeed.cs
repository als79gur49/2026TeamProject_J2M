using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Gameplay.UIAccess.Presentation;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    internal sealed class GameplayHostPresentationFeed : IGameplayPresentationFeed, IDisposable
    {
        private readonly GameplayInputHost _inputHost;
        private readonly GameplayTickViewPresenter _presenter;

        public GameplayHostPresentationFeed(
            GameplayInputHost inputHost,
            GameplayTickViewPresenter presenter)
        {
            _inputHost = inputHost ?? throw new ArgumentNullException(nameof(inputHost));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            CurrentState = CreateCurrentState();

            _inputHost.TickCompleted += HandleTickCompleted;
            _presenter.PresentationStateChanged += HandlePresentationStateChanged;
        }

        public event Action<GameplayPresentationFrame> FramePublished;

        public event Action<GameplayPresentationState> StateChanged;

        public GameplayPresentationState CurrentState { get; private set; }

        public void Dispose()
        {
            _inputHost.TickCompleted -= HandleTickCompleted;
            _presenter.PresentationStateChanged -= HandlePresentationStateChanged;
        }

        private void HandleTickCompleted(TickResult result)
        {
            if (result == null)
            {
                return;
            }

            FramePublished?.Invoke(CreateFrame(result));
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
                _presenter.CurrentTopology,
                _presenter.IsPresentationActive,
                _presenter.HasBlockingPresentation,
                _presenter.IsTopologyTransitionActive);
        }

        private GameplayPresentationFrame CreateFrame(TickResult result)
        {
            GameplayTopologyPresentationSlice? topology = null;
            if (result.PresentationData.TopologyMotion.HasValue)
            {
                var topologyMotion = result.PresentationData.TopologyMotion.Value;
                topology = new GameplayTopologyPresentationSlice(
                    topologyMotion.SourceTopology,
                    topologyMotion.DestinationTopology,
                    topologyMotion.RotationKind);
            }

            var player = BuildPlayerSlice(result, _inputHost.PlayerEntityId);
            GameplayStageEventPresentationSlice? stageEvent = null;
            if (result.ObjectiveResult != null && result.ObjectiveResult.ClearedThisTick)
            {
                stageEvent = new GameplayStageEventPresentationSlice(GameplayStageEventKind.Cleared);
            }

            return new GameplayPresentationFrame(
                result.TickIndex,
                result.FinalTopology,
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
                hasPlayerActionSignal ? playerActionSignal.ActiveActionKind : PlayerActionKind.None,
                hasPlayerActionSignal ? playerActionSignal.Direction : Direction.None,
                hasPlayerActionSignal ? playerActionSignal.TargetEntityId : 0,
                hasPlayerActionSignal && playerActionSignal.StartedThisTick,
                hasPlayerActionSignal && playerActionSignal.ExecutedThisTick,
                hasPlayerActionSignal && playerActionSignal.CompletedThisTick,
                hasPlayerActionSignal && playerActionSignal.CanceledThisTick,
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
    }
}
