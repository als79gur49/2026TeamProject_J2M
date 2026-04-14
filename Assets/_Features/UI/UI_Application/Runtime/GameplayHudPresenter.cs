using System;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.HUD;

namespace Game.Feature.UI.Application
{
    public sealed class GameplayHudPresenter : IDisposable
    {
        private readonly IGameplayCommandGateway _commandGateway;
        private readonly IGameplayUiPresentationSource _presentationSource;

        public GameplayHudPresenter(
            IGameplayCommandGateway commandGateway,
            IGameplayUiPresentationSource presentationSource)
        {
            _commandGateway = commandGateway ?? throw new ArgumentNullException(nameof(commandGateway));
            _presentationSource = presentationSource ?? throw new ArgumentNullException(nameof(presentationSource));

            _presentationSource.SnapshotChanged += HandleSnapshotChanged;

            Refresh();
        }

        public event Action<GameplayHudState> StateChanged;

        public GameplayHudState CurrentState { get; private set; }

        public void Dispose()
        {
            _presentationSource.SnapshotChanged -= HandleSnapshotChanged;
        }

        public void Refresh()
        {
            var snapshot = _presentationSource.CurrentSnapshot;

            CurrentState = new GameplayHudState(
                snapshot.Player.PlayerEntityId,
                snapshot.Player.CurrentHp,
                snapshot.Player.Facing.ToString(),
                snapshot.Player.ActiveActionKind.ToString(),
                snapshot.Tick.FinalTopology.BottomFace.ToString(),
                snapshot.Player.CanMoveThisTick,
                snapshot.Player.CanStartActionThisTick,
                snapshot.Interaction.IsPaused,
                snapshot.Tick.IsStageCleared,
                snapshot.Interaction.CanAcceptGameplayCommands);
            StateChanged?.Invoke(CurrentState);
        }

        public GameplayHudCommandResult RequestMoveUp()
        {
            var acceptance = _commandGateway.SetHeldMoveDirection(GameplayUiDirection.Up);
            Refresh();
            return MapAcceptance(acceptance);
        }

        public GameplayHudCommandResult RequestFlipRight()
        {
            var acceptance = _commandGateway.RequestFlip(GameplayUiDirection.Right);
            Refresh();
            return MapAcceptance(acceptance);
        }

        private void HandleSnapshotChanged(UIPresentationSnapshot _)
        {
            Refresh();
        }

        private static GameplayHudCommandResult MapAcceptance(GameplayCommandAcceptance acceptance)
        {
            if (acceptance.Accepted)
            {
                return GameplayHudCommandResult.Accept();
            }

            switch (acceptance.RejectionReason)
            {
                case GameplayCommandRejectionReason.Paused:
                    return GameplayHudCommandResult.Reject(GameplayHudCommandFailureKind.Paused);
                case GameplayCommandRejectionReason.BlockingPresentation:
                    return GameplayHudCommandResult.Reject(GameplayHudCommandFailureKind.Busy);
                default:
                    return GameplayHudCommandResult.Reject(GameplayHudCommandFailureKind.Unavailable);
            }
        }
    }
}
