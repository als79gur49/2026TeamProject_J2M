using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.UIAccess.Models;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    internal sealed class GameplayHostCommandAdmissionPolicy
    {
        private readonly GameplayInputHost _inputHost;
        private readonly GameplayHostPauseService _pauseService;
        private readonly GameplayTickViewPresenter _presenter;
        private readonly WorldState _worldState;

        public GameplayHostCommandAdmissionPolicy(
            WorldState worldState,
            GameplayInputHost inputHost,
            GameplayTickViewPresenter presenter,
            GameplayHostPauseService pauseService)
        {
            _worldState = worldState;
            _inputHost = inputHost;
            _presenter = presenter;
            _pauseService = pauseService;
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

        public bool TryCreateSnapshot(out WorldSnapshot snapshot)
        {
            if (_worldState == null)
            {
                snapshot = null;
                return false;
            }

            snapshot = GameplayCompositionRoot.CreateSnapshot(_worldState);
            return true;
        }
    }
}
