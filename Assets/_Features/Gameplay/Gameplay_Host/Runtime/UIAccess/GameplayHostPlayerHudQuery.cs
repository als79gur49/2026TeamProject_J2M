using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Gameplay.UIAccess.Queries;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    internal sealed class GameplayHostPlayerHudQuery : IGameplayPlayerHudQuery
    {
        private readonly GameplayHostCommandAdmissionPolicy _admissionPolicy;
        private readonly GameplayInputHost _inputHost;
        private readonly TickRunner _tickRunner;

        public GameplayHostPlayerHudQuery(
            TickRunner tickRunner,
            GameplayInputHost inputHost,
            GameplayHostCommandAdmissionPolicy admissionPolicy)
        {
            _tickRunner = tickRunner;
            _inputHost = inputHost;
            _admissionPolicy = admissionPolicy;
        }

        public GameplayPlayerHudReadModel Read()
        {
            if (_inputHost == null ||
                _admissionPolicy == null ||
                !_admissionPolicy.TryCreateSnapshot(out var snapshot))
            {
                return default;
            }

            var playerEntityId = _inputHost.PlayerEntityId;
            if (playerEntityId <= 0 ||
                !snapshot.TryGetEntity(playerEntityId, out var playerEntity))
            {
                return default;
            }

            var playerControlState = default(PlayerControlState);
            snapshot.TryGetPlayerControlState(playerEntityId, out playerControlState);

            var nextTickIndex = _tickRunner?.NextTickIndex ?? 0;
            var canAcceptActionableCommands = _admissionPolicy.CanAcceptActionableCommands();

            return new GameplayPlayerHudReadModel(
                isAvailable: true,
                playerEntityId: playerEntityId,
                currentHp: playerEntity.hp,
                facing: playerEntity.facing,
                activeActionKind: playerControlState.activeAction.kind,
                activeActionDirection: playerControlState.activeAction.direction,
                activeTargetEntityId: playerControlState.activeAction.targetEntityId,
                isActionInProgress: playerControlState.activeAction.IsActive,
                canMoveThisTick: nextTickIndex > 0 &&
                                canAcceptActionableCommands &&
                                snapshot.CanExecuteMovementIntent(playerEntityId, nextTickIndex),
                canStartActionThisTick: nextTickIndex > 0 &&
                                        canAcceptActionableCommands &&
                                        snapshot.CanStartAction(playerEntityId, nextTickIndex));
        }
    }
}
