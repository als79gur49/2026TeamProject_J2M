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

        public GameplayCommandAcceptance SetHeldMoveDirection(GameplayUiDirection direction)
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

            _inputHost.SetUiHeldMoveDirection(GameplayUiAccessMapper.ToGameplayDirection(direction));
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

        public GameplayCommandAcceptance RequestFlip(GameplayUiDirection direction)
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

            _inputHost.BufferUiFlip(GameplayUiAccessMapper.ToGameplayDirection(direction));
            return GameplayCommandAcceptance.Accept();
        }

        private static bool IsOrthogonalDirection(GameplayUiDirection direction)
        {
            return direction == GameplayUiDirection.Up ||
                   direction == GameplayUiDirection.Right ||
                   direction == GameplayUiDirection.Down ||
                   direction == GameplayUiDirection.Left;
        }
    }
}
