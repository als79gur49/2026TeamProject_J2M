using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Gameplay.UIAccess.Queries;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    internal sealed class GameplayHostPlayerHudQuery : IGameplayPlayerHudQuery
    {
        private readonly GameplayHostCommandAdmissionPolicy _admissionPolicy;
        private readonly ICampaignChancesReadSource _campaignChancesReadSource;
        private readonly GameplayInputHost _inputHost;
        private readonly TickRunner _tickRunner;

        public GameplayHostPlayerHudQuery(
            TickRunner tickRunner,
            GameplayInputHost inputHost,
            GameplayHostCommandAdmissionPolicy admissionPolicy,
            ICampaignChancesReadSource campaignChancesReadSource = null)
        {
            _tickRunner = tickRunner;
            _inputHost = inputHost;
            _admissionPolicy = admissionPolicy;
            _campaignChancesReadSource = campaignChancesReadSource;
        }

        public GameplayPlayerHudReadModel Read()
        {
            if (_inputHost == null ||
                _admissionPolicy == null ||
                !_admissionPolicy.TryCreateSnapshot(out var snapshot))
            {
                return default;
            }

            var remainingChances = 0;
            var maxChances = 0;
            var hasRemainingChances = _campaignChancesReadSource != null &&
                                      _campaignChancesReadSource.TryReadChances(out remainingChances, out maxChances);

            if (!_admissionPolicy.TryGetCommittedControllableActor(out var playerEntity))
            {
                return new GameplayPlayerHudReadModel(
                    isAvailable: false,
                    playerEntityId: 0,
                    currentHp: 0,
                    maxHp: 0,
                    facing: GameplayUiDirection.None,
                    activeActionKind: GameplayUiActionKind.None,
                    activeActionDirection: GameplayUiDirection.None,
                    activeTargetEntityId: 0,
                    isActionInProgress: false,
                    isActionInRecoveryPhase: false,
                    canMoveThisTick: false,
                    canStartActionThisTick: false,
                    recoveryCooldown: null,
                    canStartAnyActionThisTick: false,
                    hasExplicitPushCandidateInCurrentDirection: false,
                    hasRemainingChances: hasRemainingChances,
                    remainingChances: hasRemainingChances ? remainingChances : 0,
                    maxChances: hasRemainingChances ? maxChances : 0);
            }

            var playerEntityId = playerEntity.entityId;
            var playerControlState = default(PlayerControlState);
            snapshot.TryGetPlayerControlState(playerEntityId, out playerControlState);

            var nextTickIndex = _tickRunner?.NextTickIndex ?? 0;
            var canAcceptActionableCommands = _admissionPolicy.CanAcceptActionableCommands();
            var isSettledAtAnchor = UnitSpatialQuery.IsSettledAtAnchor(snapshot, playerEntityId);
            var canStartAnyActionThisTick = nextTickIndex > 0 &&
                                            canAcceptActionableCommands &&
                                            isSettledAtAnchor &&
                                            snapshot.CanStartAction(playerEntityId, nextTickIndex);
            var hasExplicitPushCandidateInCurrentDirection = canStartAnyActionThisTick &&
                                                             PlayerActionPreviewQueries.HasExplicitPushCandidate(
                                                                 snapshot,
                                                                 playerEntity,
                                                                 _inputHost?.PreviewPushDirection() ?? Direction.None);
            var recoveryCooldown = TryCreateRecoveryCooldown(playerControlState, nextTickIndex);

            return new GameplayPlayerHudReadModel(
                isAvailable: true,
                playerEntityId: playerEntityId,
                currentHp: playerEntity.hp,
                maxHp: playerEntity.maxHp,
                facing: GameplayUiAccessMapper.ToUiDirection(playerEntity.facing),
                activeActionKind: GameplayUiAccessMapper.ToUiActionKind(playerControlState.activeAction.kind),
                activeActionDirection: GameplayUiAccessMapper.ToUiDirection(playerControlState.activeAction.direction),
                activeTargetEntityId: playerControlState.activeAction.targetEntityId,
                isActionInProgress: playerControlState.activeAction.IsActive,
                isActionInRecoveryPhase: playerControlState.activeAction.IsActive &&
                                         nextTickIndex > 0 &&
                                         playerControlState.activeAction.executeTick < nextTickIndex,
                canMoveThisTick: nextTickIndex > 0 &&
                                canAcceptActionableCommands &&
                                isSettledAtAnchor &&
                                snapshot.CanExecuteMovementIntent(playerEntityId, nextTickIndex),
                canStartActionThisTick: canStartAnyActionThisTick,
                recoveryCooldown: recoveryCooldown,
                canStartAnyActionThisTick: canStartAnyActionThisTick,
                hasExplicitPushCandidateInCurrentDirection: hasExplicitPushCandidateInCurrentDirection,
                hasRemainingChances: hasRemainingChances,
                remainingChances: hasRemainingChances ? remainingChances : 0,
                maxChances: hasRemainingChances ? maxChances : 0);
        }

        private static GameplayUiRecoveryCooldown? TryCreateRecoveryCooldown(
            in PlayerControlState playerControlState,
            int nextTickIndex)
        {
            var activeAction = playerControlState.activeAction;
            if (!activeAction.IsActive ||
                nextTickIndex <= 0 ||
                nextTickIndex <= activeAction.executeTick)
            {
                return null;
            }

            var totalRecoveryTicks = System.Math.Max(0, activeAction.recoveryEndTick - activeAction.executeTick);
            if (totalRecoveryTicks <= 0)
            {
                return null;
            }

            var remainingRecoveryTicks = System.Math.Max(0, activeAction.recoveryEndTick - nextTickIndex + 1);
            if (remainingRecoveryTicks <= 0)
            {
                return null;
            }

            return new GameplayUiRecoveryCooldown(
                GameplayUiAccessMapper.ToUiActionKind(activeAction.kind),
                remainingRecoveryTicks,
                totalRecoveryTicks);
        }
    }
}
