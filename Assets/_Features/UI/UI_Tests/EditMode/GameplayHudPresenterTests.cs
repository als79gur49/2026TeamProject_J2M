using System.Linq;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.HUD;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class HudPresentationPresenterTests
    {
        [Test]
        public void HUDRootPresenter_IsTheOnlyHudSubscriberToMappedPresentationSource()
        {
            var source = new ManualGameplayUiPresentationSource();
            var playerStatusPresenter = new PlayerStatusPresenter();
            var actionBarPresenter = new ActionBarPresenter(new FakeGameplayCommandGateway());
            var notificationPresenter = new NotificationPresenter();

            using var rootPresenter = new HUDRootPresenter(
                source,
                playerStatusPresenter,
                actionBarPresenter,
                notificationPresenter);
            using var controller = new HUDController(
                rootPresenter.ViewModel,
                playerStatusPresenter.ViewModel,
                actionBarPresenter.ViewModel,
                notificationPresenter.ViewModel,
                actionBarPresenter);

            Assert.That(source.SnapshotSubscriberCount, Is.EqualTo(1));
            Assert.That(source.TickEventSubscriberCount, Is.EqualTo(0));
        }

        [Test]
        public void HUDRootPresenter_Dispose_RemovesItsMappedSnapshotSubscription()
        {
            var source = new ManualGameplayUiPresentationSource();
            var playerStatusPresenter = new PlayerStatusPresenter();
            var actionBarPresenter = new ActionBarPresenter(new FakeGameplayCommandGateway());
            var notificationPresenter = new NotificationPresenter();
            var rootPresenter = new HUDRootPresenter(
                source,
                playerStatusPresenter,
                actionBarPresenter,
                notificationPresenter);

            Assert.That(source.SnapshotSubscriberCount, Is.EqualTo(1));

            rootPresenter.Dispose();

            Assert.That(source.SnapshotSubscriberCount, Is.EqualTo(0));
            Assert.That(source.TickEventSubscriberCount, Is.EqualTo(0));
        }

        [Test]
        public void HUDRootPresenter_FansOutMappedSnapshotToShellAndChildViewModels()
        {
            var source = new ManualGameplayUiPresentationSource();
            var playerStatusPresenter = new PlayerStatusPresenter();
            var actionBarPresenter = new ActionBarPresenter(new FakeGameplayCommandGateway());
            var notificationPresenter = new NotificationPresenter();
            using var rootPresenter = new HUDRootPresenter(
                source,
                playerStatusPresenter,
                actionBarPresenter,
                notificationPresenter);

            source.PublishSnapshot(CreateSnapshot(
                isPaused: false,
                isUiBlocked: true,
                hasBlockingPresentation: false,
                currentHp: 2,
                activeActionKind: GameplayUiActionKind.Flip,
                isRecoveryPhase: true,
                lastOutcome: GameplayUiActionResolutionKind.Blocked));

            Assert.That(rootPresenter.ViewModel.IsDimmed, Is.True);
            Assert.That(rootPresenter.ViewModel.IsPauseButtonEnabled, Is.False);
            Assert.That(playerStatusPresenter.ViewModel.CurrentHp, Is.EqualTo(2));
            Assert.That(playerStatusPresenter.ViewModel.ActionText, Is.EqualTo("Flip (Recovery)"));
            Assert.That(playerStatusPresenter.ViewModel.StatusText, Is.EqualTo("Read Only"));
            Assert.That(actionBarPresenter.ViewModel.OutcomeText, Is.EqualTo("Blocked"));
            Assert.That(actionBarPresenter.ViewModel.Slots.Count, Is.EqualTo(2));
            Assert.That(actionBarPresenter.ViewModel.Slots[1].StateText, Is.EqualTo("Recovering"));
            Assert.That(notificationPresenter.ViewModel.Items.Count, Is.EqualTo(2));
        }

        [Test]
        public void HUDRootPresenter_RefreshOnlyInteractionChanges_UpdateShellAndActionStates_ThroughMappedSourceOnly()
        {
            var source = new ManualGameplayUiPresentationSource();
            var playerStatusPresenter = new PlayerStatusPresenter();
            var actionBarPresenter = new ActionBarPresenter(new FakeGameplayCommandGateway());
            var notificationPresenter = new NotificationPresenter();
            using var rootPresenter = new HUDRootPresenter(
                source,
                playerStatusPresenter,
                actionBarPresenter,
                notificationPresenter);

            source.PublishSnapshot(CreateSnapshot(isPaused: true));
            Assert.That(rootPresenter.ViewModel.IsDimmed, Is.True);
            Assert.That(rootPresenter.ViewModel.IsPauseButtonEnabled, Is.False);
            Assert.That(actionBarPresenter.ViewModel.IsInteractive, Is.False);
            Assert.That(actionBarPresenter.ViewModel.Slots[0].StateText, Is.EqualTo("Paused"));

            source.PublishSnapshot(CreateSnapshot(hasBlockingPresentation: true));
            Assert.That(rootPresenter.ViewModel.IsDimmed, Is.True);
            Assert.That(rootPresenter.ViewModel.IsPauseButtonEnabled, Is.True);
            Assert.That(actionBarPresenter.ViewModel.Slots[0].StateText, Is.EqualTo("Busy"));

            source.PublishSnapshot(CreateSnapshot(isUiBlocked: true));
            Assert.That(rootPresenter.ViewModel.IsDimmed, Is.True);
            Assert.That(actionBarPresenter.ViewModel.Slots[0].StateText, Is.EqualTo("Read Only"));

            source.PublishSnapshot(CreateSnapshot(canAcceptGameplayCommands: false));
            Assert.That(rootPresenter.ViewModel.IsDimmed, Is.False);
            Assert.That(actionBarPresenter.ViewModel.IsInteractive, Is.False);
            Assert.That(actionBarPresenter.ViewModel.Slots[0].StateText, Is.EqualTo("Blocked"));

            source.PublishSnapshot(CreateSnapshot());
            Assert.That(rootPresenter.ViewModel.IsDimmed, Is.False);
            Assert.That(rootPresenter.ViewModel.IsPauseButtonEnabled, Is.True);
            Assert.That(actionBarPresenter.ViewModel.IsInteractive, Is.True);
            Assert.That(actionBarPresenter.ViewModel.Slots[0].StateText, Is.EqualTo("Ready"));
        }

        [Test]
        public void ActionBarPresenter_MapsCommandRejectionToLocalFeedback_AndClearsWhenReadyReturns()
        {
            var commandGateway = new FakeGameplayCommandGateway
            {
                OnRequestFlip = _ => GameplayCommandAcceptance.Reject(GameplayCommandRejectionReason.BlockingPresentation),
            };
            var presenter = new ActionBarPresenter(commandGateway);

            presenter.Apply(
                new UITickSlice(3, new GameplayUiTopology(GameplayUiFace.Floor), false, false),
                new UIInteractionSlice(false, true, false, false),
                new UIPlayerActionSlice(
                    playerEntityId: 10,
                    currentHp: 3,
                    facing: GameplayUiDirection.Up,
                    activeActionKind: GameplayUiActionKind.None,
                    isRecoveryPhase: false,
                    canMoveThisTick: true,
                    canStartActionThisTick: true,
                    lastResolvedOutcome: GameplayUiActionResolutionKind.None,
                    lastResolvedTickIndex: 0,
                    tookDamageThisTick: false,
                    lastDamageAmount: 0,
                    lastDamageTickIndex: 0));

            var result = presenter.RequestSlot(HudActionSlotId.Secondary);

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(ActionBarCommandFailureKind.Busy));
            Assert.That(presenter.ViewModel.FeedbackText, Is.EqualTo("Busy"));
            Assert.That(presenter.ViewModel.LastCommandResult.HasValue, Is.True);

            presenter.Apply(
                new UITickSlice(4, new GameplayUiTopology(GameplayUiFace.Front), false, false),
                new UIInteractionSlice(false, true, false, false),
                new UIPlayerActionSlice(
                    playerEntityId: 10,
                    currentHp: 3,
                    facing: GameplayUiDirection.Right,
                    activeActionKind: GameplayUiActionKind.None,
                    isRecoveryPhase: false,
                    canMoveThisTick: true,
                    canStartActionThisTick: true,
                    lastResolvedOutcome: GameplayUiActionResolutionKind.None,
                    lastResolvedTickIndex: 0,
                    tookDamageThisTick: false,
                    lastDamageAmount: 0,
                    lastDamageTickIndex: 0));

            Assert.That(presenter.ViewModel.FeedbackText, Is.EqualTo(string.Empty));
            Assert.That(presenter.ViewModel.LastCommandResult.HasValue, Is.False);
        }

        [Test]
        public void ActionBarPresenter_DoesNotChangeState_WhenOnlyNotificationRetentionChanges()
        {
            var source = new ManualGameplayUiPresentationSource();
            var playerStatusPresenter = new PlayerStatusPresenter();
            var actionBarPresenter = new ActionBarPresenter(new FakeGameplayCommandGateway());
            var notificationPresenter = new NotificationPresenter();
            using var rootPresenter = new HUDRootPresenter(
                source,
                playerStatusPresenter,
                actionBarPresenter,
                notificationPresenter);

            source.PublishSnapshot(CreateSnapshot(lastOutcome: GameplayUiActionResolutionKind.Success));
            var before = SerializeActionBar(actionBarPresenter.ViewModel);

            source.PublishSnapshot(CreateSnapshot(
                lastOutcome: GameplayUiActionResolutionKind.Success,
                notificationEventKind: UITickEventKind.StageCleared,
                notificationActionKind: GameplayUiActionKind.None,
                notificationResolutionKind: GameplayUiActionResolutionKind.None));
            var after = SerializeActionBar(actionBarPresenter.ViewModel);

            Assert.That(after, Is.EqualTo(before));
        }

        [Test]
        public void ActionBarPresenter_CommandFeedback_RemainsLocal_AndDoesNotMutateNotifications()
        {
            var source = new ManualGameplayUiPresentationSource();
            var playerStatusPresenter = new PlayerStatusPresenter();
            var actionBarPresenter = new ActionBarPresenter(new FakeGameplayCommandGateway
            {
                OnRequestFlip = _ => GameplayCommandAcceptance.Reject(GameplayCommandRejectionReason.BlockingPresentation),
            });
            var notificationPresenter = new NotificationPresenter();
            using var rootPresenter = new HUDRootPresenter(
                source,
                playerStatusPresenter,
                actionBarPresenter,
                notificationPresenter);

            source.PublishSnapshot(CreateSnapshot());
            var before = notificationPresenter.ViewModel.Items.Select(item => item.MessageText).ToArray();

            var result = actionBarPresenter.RequestSlot(HudActionSlotId.Secondary);

            Assert.That(result.Accepted, Is.False);
            Assert.That(actionBarPresenter.ViewModel.FeedbackText, Is.EqualTo("Busy"));
            Assert.That(notificationPresenter.ViewModel.Items.Select(item => item.MessageText).ToArray(), Is.EqualTo(before));
        }

        [Test]
        public void ActionBarPresenter_UsesReplaceableSlotDefinitions_ForStageFiveDefaults()
        {
            var presenter = new ActionBarPresenter(
                new FakeGameplayCommandGateway(),
                new[]
                {
                    new ActionBarSlotDefinition(HudActionSlotId.Primary, "Advance", GameplayUiActionKind.Push, ActionBarSlotCommandKind.HoldMove, GameplayUiDirection.Down),
                    new ActionBarSlotDefinition(HudActionSlotId.Secondary, "Turn Left", GameplayUiActionKind.Flip, ActionBarSlotCommandKind.Flip, GameplayUiDirection.Left),
                });

            presenter.Apply(
                new UITickSlice(2, new GameplayUiTopology(GameplayUiFace.Floor), false, false),
                new UIInteractionSlice(false, true, false, false),
                new UIPlayerActionSlice(
                    playerEntityId: 10,
                    currentHp: 3,
                    facing: GameplayUiDirection.Up,
                    activeActionKind: GameplayUiActionKind.None,
                    isRecoveryPhase: false,
                    canMoveThisTick: true,
                    canStartActionThisTick: true,
                    lastResolvedOutcome: GameplayUiActionResolutionKind.None,
                    lastResolvedTickIndex: 0,
                    tookDamageThisTick: false,
                    lastDamageAmount: 0,
                    lastDamageTickIndex: 0));

            Assert.That(presenter.ViewModel.Slots.Select(slot => slot.LabelText).ToArray(), Is.EqualTo(new[] { "Advance", "Turn Left" }));
        }

        private static string SerializeActionBar(ActionBarViewModel viewModel)
        {
            return string.Join(
                "|",
                viewModel.Slots.Select(slot => $"{slot.SlotId}:{slot.LabelText}:{slot.StateText}:{slot.IsInteractive}:{slot.IsHighlighted}")) +
                   $"|{viewModel.FeedbackText}|{viewModel.OutcomeText}|{viewModel.IsInteractive}";
        }

        private static UIPresentationSnapshot CreateSnapshot(
            bool isPaused = false,
            bool isUiBlocked = false,
            bool hasBlockingPresentation = false,
            bool? canAcceptGameplayCommands = null,
            int currentHp = 3,
            GameplayUiActionKind activeActionKind = GameplayUiActionKind.None,
            bool isRecoveryPhase = false,
            GameplayUiActionResolutionKind lastOutcome = GameplayUiActionResolutionKind.None,
            UITickEventKind notificationEventKind = UITickEventKind.PlayerActionResolved,
            GameplayUiActionKind notificationActionKind = GameplayUiActionKind.Flip,
            GameplayUiActionResolutionKind notificationResolutionKind = GameplayUiActionResolutionKind.Success)
        {
            var acceptsGameplayCommands = canAcceptGameplayCommands ?? (!isPaused && !hasBlockingPresentation);

            return new UIPresentationSnapshot(
                new UITickSlice(
                    lastReducedTickIndex: 4,
                    finalTopology: new GameplayUiTopology(GameplayUiFace.Front),
                    isStageCleared: false,
                    isTopologyTransitionActive: false),
                new UIInteractionSlice(
                    isPaused,
                    canAcceptGameplayCommands: acceptsGameplayCommands,
                    hasBlockingGameplayPresentation: hasBlockingPresentation,
                    isUiGameplayInputBlocked: isUiBlocked),
                new UIPlayerActionSlice(
                    playerEntityId: 10,
                    currentHp: currentHp,
                    facing: GameplayUiDirection.Right,
                    activeActionKind: activeActionKind,
                    isRecoveryPhase: isRecoveryPhase,
                    canMoveThisTick: true,
                    canStartActionThisTick: true,
                    lastResolvedOutcome: lastOutcome,
                    lastResolvedTickIndex: lastOutcome == GameplayUiActionResolutionKind.None ? 0 : 4,
                    tookDamageThisTick: true,
                    lastDamageAmount: 1,
                    lastDamageTickIndex: 4),
                new UINotificationLedgerSlice(new[]
                {
                    new UINotificationRecord(
                        new UITickEventKey(
                            tickIndex: 4,
                            eventKind: notificationEventKind,
                            actorEntityId: 10,
                            actionKind: notificationActionKind,
                            actionSequence: 2,
                            resolutionKind: notificationResolutionKind),
                        notificationEventKind,
                        damageAmount: notificationEventKind == UITickEventKind.PlayerDamaged ? 2 : 0,
                        expireAfterTickIndex: 8),
                    new UINotificationRecord(
                        new UITickEventKey(
                            tickIndex: 3,
                            eventKind: UITickEventKind.PlayerDamaged,
                            actorEntityId: 10,
                            actionKind: GameplayUiActionKind.None,
                            actionSequence: 0,
                            resolutionKind: GameplayUiActionResolutionKind.None),
                        UITickEventKind.PlayerDamaged,
                        damageAmount: 1,
                        expireAfterTickIndex: 7),
                }));
        }
    }
}
