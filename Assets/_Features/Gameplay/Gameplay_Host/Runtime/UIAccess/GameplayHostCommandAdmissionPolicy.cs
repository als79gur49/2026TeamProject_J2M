using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.UIAccess.Models;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    internal sealed class GameplayHostCommandAdmissionPolicy : IDisposable
    {
        private CachedPresentationWindow _cachedPresentationWindow;
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

            if (!TryGetCommittedControllableActor(out _))
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
            if (_cachedPresentationWindow == null ||
                _cachedPresentationWindow.Snapshot == null)
            {
                snapshot = null;
                return false;
            }

            snapshot = _cachedPresentationWindow.Snapshot;
            return true;
        }

        internal bool TryGetCommittedControllableActor(out EntityState playerEntity)
        {
            ValidateCacheTickIndexInvariant();
            if (_cachedPresentationWindow == null)
            {
                playerEntity = default;
                return false;
            }

            return _cachedPresentationWindow.TryGetCommittedControllableActor(out playerEntity);
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
                _cachedPresentationWindow = null;
                return;
            }

            if (_cachedPresentationWindow != null &&
                completedTickIndex < _cachedPresentationWindow.CompletedTickIndex)
            {
                UnityEngine.Debug.LogError(
                    $"GameplayHostCommandAdmissionPolicy received a stale completed tick index. " +
                    $"Cached={_cachedPresentationWindow.CompletedTickIndex}, Incoming={completedTickIndex}.");
                return;
            }

            var freshSnapshot = GameplayCompositionRoot.CreateSnapshot(_worldState);
            _cachedPresentationWindow = new CachedPresentationWindow(
                freshSnapshot,
                completedTickIndex,
                _inputHost?.PlayerEntityId ?? 0);
            ValidateCacheTickIndexInvariant();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ValidateCacheTickIndexInvariant()
        {
            if (_cachedPresentationWindow == null ||
                _cachedPresentationWindow.Snapshot == null ||
                _tickRunner == null)
            {
                return;
            }

            UnityEngine.Debug.Assert(
                _tickRunner.NextTickIndex == _cachedPresentationWindow.CompletedTickIndex + 1,
                $"Cached presentation snapshot tick invariant violated. " +
                $"NextTickIndex={_tickRunner.NextTickIndex}, CachedCompletedTickIndex={_cachedPresentationWindow.CompletedTickIndex}.");
        }

        private sealed class CachedPresentationWindow
        {
            private readonly int _playerEntityId;
            private bool _hasResolvedControllableActor;
            private bool _hasControllableActor;
            private EntityState _controllableActor;

            public CachedPresentationWindow(
                WorldSnapshot snapshot,
                int completedTickIndex,
                int playerEntityId)
            {
                Snapshot = snapshot;
                CompletedTickIndex = completedTickIndex;
                _playerEntityId = playerEntityId;
            }

            public WorldSnapshot Snapshot { get; }

            public int CompletedTickIndex { get; }

            public bool TryGetCommittedControllableActor(out EntityState playerEntity)
            {
                if (!_hasResolvedControllableActor)
                {
                    _hasControllableActor = Snapshot != null &&
                                            _playerEntityId > 0 &&
                                            Snapshot.TryGetEntity(_playerEntityId, out _controllableActor);
                    _hasResolvedControllableActor = true;
                }

                playerEntity = _controllableActor;
                return _hasControllableActor;
            }
        }
    }
}
