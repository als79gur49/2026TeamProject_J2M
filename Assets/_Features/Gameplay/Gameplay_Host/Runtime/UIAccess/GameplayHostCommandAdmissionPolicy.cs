using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.UIAccess.Models;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    internal sealed class GameplayHostCommandAdmissionPolicy : IDisposable
    {
        // Current host order is RunNextTick -> Present -> TickCompleted.
        // Read queries may observe one extra pre-refresh completed window during Present.
        private const int MaxAllowedReadTimeCompletedTickLagForCurrentHostOrder = 2;

        private CachedPresentationWindow _cachedPresentationWindow;
        private readonly GameplayInputHost _inputHost;
        private readonly GameplayHostPauseService _pauseService;
        private readonly GameplayTickViewPresenter _presenter;
        private readonly TickRunner _tickRunner;
        private readonly WorldState _worldState;
        private bool _isDisposed;

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
            _isDisposed = true;
            if (_inputHost != null)
            {
                _inputHost.TickCompleted -= HandleTickCompleted;
            }
        }

        public bool TryCreateSnapshot(out WorldSnapshot snapshot)
        {
            if (_cachedPresentationWindow == null ||
                _cachedPresentationWindow.Snapshot == null)
            {
                snapshot = null;
                return false;
            }

            ValidateReadTimeSnapshotWindowInvariant();
            snapshot = _cachedPresentationWindow.Snapshot;
            return true;
        }

        internal bool TryGetCommittedControllableActor(out EntityState playerEntity)
        {
            if (_cachedPresentationWindow == null)
            {
                playerEntity = default;
                return false;
            }

            ValidateReadTimeSnapshotWindowInvariant();
            return _cachedPresentationWindow.TryGetCommittedControllableActor(out playerEntity);
        }

        private void HandleTickCompleted(TickResult result)
        {
            if (result == null)
            {
                return;
            }

            ValidateTickCompletedRefreshInput(result);
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
            var refreshedWindow = new CachedPresentationWindow(
                freshSnapshot,
                completedTickIndex,
                _inputHost?.PlayerEntityId ?? 0);
            _cachedPresentationWindow = refreshedWindow;
            ValidateRefreshTimeSnapshotWindowInvariant(refreshedWindow, completedTickIndex);
        }

        // Read-time validation is intentionally weaker than refresh-time validation.
        // It must tolerate the current host pre-refresh transient during Present.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ValidateReadTimeSnapshotWindowInvariant()
        {
            if (_cachedPresentationWindow == null ||
                _cachedPresentationWindow.Snapshot == null ||
                _tickRunner == null ||
                _isDisposed)
            {
                return;
            }

            var nextTickIndex = _tickRunner.NextTickIndex;
            var completedTickLag = nextTickIndex - _cachedPresentationWindow.CompletedTickIndex;

            UnityEngine.Debug.Assert(
                completedTickLag >= 1,
                $"Cached presentation snapshot read-time invariant violated: cached completed tick must not point to the future. " +
                $"NextTickIndex={nextTickIndex}, CachedCompletedTickIndex={_cachedPresentationWindow.CompletedTickIndex}.");

            UnityEngine.Debug.Assert(
                completedTickLag <= MaxAllowedReadTimeCompletedTickLagForCurrentHostOrder,
                $"Cached presentation snapshot read-time invariant violated: lag exceeded the current host pre-refresh allowance. " +
                $"NextTickIndex={nextTickIndex}, CachedCompletedTickIndex={_cachedPresentationWindow.CompletedTickIndex}, Lag={completedTickLag}, " +
                $"MaxAllowedLag={MaxAllowedReadTimeCompletedTickLagForCurrentHostOrder}.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ValidateTickCompletedRefreshInput(TickResult result)
        {
            if (result == null ||
                _tickRunner == null ||
                _isDisposed)
            {
                return;
            }

            if (_cachedPresentationWindow != null &&
                _cachedPresentationWindow.Snapshot != null)
            {
                UnityEngine.Debug.Assert(
                    result.TickIndex > _cachedPresentationWindow.CompletedTickIndex,
                    $"Cached presentation snapshot refresh invariant violated: TickCompleted must advance the completed tick window. " +
                    $"Incoming={result.TickIndex}, CachedCompletedTickIndex={_cachedPresentationWindow.CompletedTickIndex}.");
            }

            UnityEngine.Debug.Assert(
                result.TickIndex == _tickRunner.NextTickIndex - 1,
                $"Cached presentation snapshot refresh invariant violated: TickCompleted result must align with the live next tick index. " +
                $"Incoming={result.TickIndex}, NextTickIndex={_tickRunner.NextTickIndex}.");
        }

        // Refresh-time validation is the authoritative freshness check for the cached completed snapshot.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ValidateRefreshTimeSnapshotWindowInvariant(
            CachedPresentationWindow refreshedWindow,
            int completedTickIndex)
        {
            if (refreshedWindow == null ||
                refreshedWindow.Snapshot == null ||
                _tickRunner == null ||
                _isDisposed)
            {
                return;
            }

            UnityEngine.Debug.Assert(
                ReferenceEquals(_cachedPresentationWindow, refreshedWindow),
                "Cached presentation snapshot refresh invariant violated: refreshed holder must be published atomically.");

            UnityEngine.Debug.Assert(
                refreshedWindow.CompletedTickIndex == completedTickIndex,
                $"Cached presentation snapshot refresh invariant violated: refreshed holder tick index must match the completed tick. " +
                $"Expected={completedTickIndex}, Actual={refreshedWindow.CompletedTickIndex}.");

            UnityEngine.Debug.Assert(
                _tickRunner.NextTickIndex == completedTickIndex + 1,
                $"Cached presentation snapshot refresh invariant violated: refreshed completed window must align with the live next tick index. " +
                $"NextTickIndex={_tickRunner.NextTickIndex}, CachedCompletedTickIndex={refreshedWindow.CompletedTickIndex}.");
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
