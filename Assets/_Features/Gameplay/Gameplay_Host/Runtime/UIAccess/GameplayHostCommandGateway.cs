using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Gameplay.UIAccess.Models;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    internal sealed class GameplayHostCommandGateway : IGameplayCommandGateway
    {
        private readonly GameplayHostCommandAdmissionPolicy _admissionPolicy;
        private readonly GameplayInputHost _inputHost;

        public GameplayHostCommandGateway(
            GameplayInputHost inputHost,
            GameplayHostCommandAdmissionPolicy admissionPolicy)
        {
            _inputHost = inputHost;
            _admissionPolicy = admissionPolicy;
        }

        public GameplayCommandAcceptance SetHeldMoveDirection(Direction direction)
        {
            if (!IsOrthogonalDirection(direction))
            {
                return GameplayCommandAcceptance.Reject(GameplayCommandRejectionReason.InvalidRequest);
            }

            if (_inputHost == null)
            {
                return GameplayCommandAcceptance.Reject(GameplayCommandRejectionReason.GameplayInputUnavailable);
            }

            var acceptance = _admissionPolicy.EvaluateActionableRequest();
            if (!acceptance.Accepted)
            {
                return acceptance;
            }

            _inputHost.SetUiHeldMoveDirection(direction);
            return GameplayCommandAcceptance.Accept();
        }

        public GameplayCommandAcceptance ClearHeldMoveDirection()
        {
            if (_inputHost == null)
            {
                return GameplayCommandAcceptance.Reject(GameplayCommandRejectionReason.GameplayInputUnavailable);
            }

            _inputHost.ClearUiHeldMoveDirection();
            return GameplayCommandAcceptance.Accept();
        }

        public GameplayCommandAcceptance RequestFlip(Direction direction)
        {
            if (!IsOrthogonalDirection(direction))
            {
                return GameplayCommandAcceptance.Reject(GameplayCommandRejectionReason.InvalidRequest);
            }

            if (_inputHost == null)
            {
                return GameplayCommandAcceptance.Reject(GameplayCommandRejectionReason.GameplayInputUnavailable);
            }

            var acceptance = _admissionPolicy.EvaluateActionableRequest();
            if (!acceptance.Accepted)
            {
                return acceptance;
            }

            _inputHost.BufferUiFlip(direction);
            return GameplayCommandAcceptance.Accept();
        }

        private static bool IsOrthogonalDirection(Direction direction)
        {
            return direction == Direction.Up ||
                   direction == Direction.Right ||
                   direction == Direction.Down ||
                   direction == Direction.Left;
        }
    }
}
