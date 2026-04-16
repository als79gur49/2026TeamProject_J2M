using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.UIAccess.Models;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    internal sealed class GameplayHostCommandAdmissionPolicy : IDisposable
    {
        private WorldSnapshot _cachedPresentationSnapshot;
        private int _cachedCompletedTickIndex;
        private readonly GameplayInputHost _inputHost;
        private readonly GameplayHostPauseService _pauseService;
        private readonly GameplayTickViewPresenter _presenter;
        private readonly TickRunner _tickRunner;
        private readonly WorldState _worldState;

        public GameplayHostCommandAdmissionPolicy(
            WorldState worldState,
            TickRunner tickRunner,
            GameplayInputHost inputHost,
            GameplayTickViewPresenter presenter,
            GameplayHostPauseService pauseService)
        {
            _worldState = worldState;
            _tickRunner = tickRunner;
            _inputHost = inputHost;
            _presenter = presenter;
            _pauseService = pauseService;
            RefreshCachedPresentationSnapshot(completedTickIndex: 0);
            if (_inputHost != null)
            {
                _inputHost.TickCompleted += HandleTickCompleted;
            }
        }

        public bool CanAcceptActionableCommands()
        {
            return CanAcceptActionableCommands(out _);
        }

        public bool CanAcceptActionableCommands(out GameplayCommandRejectionReason rejectionReason)
        {
            if (_inputHost == null || _worldState == null)
            {
                rejectionReason = GameplayCommandRejectionReason.GameplayInputUnavailable;
                return false;
            }

            if (_pauseService != null && _pauseService.IsPaused)
            {
                rejectionReason = GameplayCommandRejectionReason.Paused;
                return false;
            }

            if (_presenter != null && _presenter.HasBlockingPresentation)
            {
                rejectionReason = GameplayCommandRejectionReason.BlockingPresentation;
                return false;
            }

            if (!TryCreateSnapshot(out var snapshot) ||
                !snapshot.TryGetEntity(_inputHost.PlayerEntityId, out _))
            {
                rejectionReason = GameplayCommandRejectionReason.NoControllableActor;
                return false;
            }

            rejectionReason = GameplayCommandRejectionReason.None;
            return true;
        }

        public GameplayCommandAcceptance EvaluateActionableRequest()
        {
            return CanAcceptActionableCommands(out var rejectionReason)
                ? GameplayCommandAcceptance.Accept()
                : GameplayCommandAcceptance.Reject(rejectionReason);
        }

        public void Dispose()
        {
            if (_inputHost != null)
            {
                _inputHost.TickCompleted -= HandleTickCompleted;
            }
        }

        public bool TryCreateSnapshot(out WorldSnapshot snapshot)
        {
            ValidateCacheTickIndexInvariant();
            if (_cachedPresentationSnapshot == null)
            {
                snapshot = null;
                return false;
            }

            snapshot = _cachedPresentationSnapshot;
            return true;
        }

        private void HandleTickCompleted(TickResult result)
        {
            if (result == null)
            {
                return;
            }

            RefreshCachedPresentationSnapshot(result.TickIndex);
        }

        private void RefreshCachedPresentationSnapshot(int completedTickIndex)
        {
            if (_worldState == null)
            {
                _cachedPresentationSnapshot = null;
                _cachedCompletedTickIndex = 0;
                return;
            }

            if (_cachedPresentationSnapshot != null &&
                completedTickIndex < _cachedCompletedTickIndex)
            {
                UnityEngine.Debug.LogError(
                    $"GameplayHostCommandAdmissionPolicy received a stale completed tick index. " +
                    $"Cached={_cachedCompletedTickIndex}, Incoming={completedTickIndex}.");
                return;
            }

            var freshSnapshot = GameplayCompositionRoot.CreateSnapshot(_worldState);
            _cachedPresentationSnapshot = freshSnapshot;
            _cachedCompletedTickIndex = completedTickIndex;
            ValidateCacheTickIndexInvariant();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ValidateCacheTickIndexInvariant()
        {
            if (_cachedPresentationSnapshot == null ||
                _tickRunner == null)
            {
                return;
            }

            UnityEngine.Debug.Assert(
                _tickRunner.NextTickIndex == _cachedCompletedTickIndex + 1,
                $"Cached presentation snapshot tick invariant violated. " +
                $"NextTickIndex={_tickRunner.NextTickIndex}, CachedCompletedTickIndex={_cachedCompletedTickIndex}.");
        }
    }
}
