using System.Linq;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Stages;
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
            var stageInfoPresenter = new StageInfoPresenter();
            var notificationPresenter = new NotificationPresenter();

            using var rootPresenter = new HUDRootPresenter(
                source,
                stageInfoPresenter,
                playerStatusPresenter,
                notificationPresenter);
            using var controller = new HUDController(
                rootPresenter.ViewModel,
                stageInfoPresenter.ViewModel,
                playerStatusPresenter.ViewModel,
                notificationPresenter.ViewModel);

            Assert.That(source.SnapshotSubscriberCount, Is.EqualTo(1));
            Assert.That(source.TickEventSubscriberCount, Is.EqualTo(0));
        }

        [Test]
        public void HUDRootPresenter_Dispose_RemovesItsMappedSnapshotSubscription()
        {
            var source = new ManualGameplayUiPresentationSource();
            var playerStatusPresenter = new PlayerStatusPresenter();
            var stageInfoPresenter = new StageInfoPresenter();
            var notificationPresenter = new NotificationPresenter();
            var rootPresenter = new HUDRootPresenter(
                source,
                stageInfoPresenter,
                playerStatusPresenter,
                notificationPresenter);

            Assert.That(source.SnapshotSubscriberCount, Is.EqualTo(1));
            Assert.That(source.TickEventSubscriberCount, Is.EqualTo(0));

            rootPresenter.Dispose();

            Assert.That(source.SnapshotSubscriberCount, Is.EqualTo(0));
            Assert.That(source.TickEventSubscriberCount, Is.EqualTo(0));
        }

        [Test]
        public void HUDRootPresenter_FansOutMappedSnapshotToShellAndChildViewModels()
        {
            var source = new ManualGameplayUiPresentationSource();
            var playerStatusPresenter = new PlayerStatusPresenter();
            var stageInfoPresenter = new StageInfoPresenter();
            var notificationPresenter = new NotificationPresenter();
            using var rootPresenter = new HUDRootPresenter(
                source,
                stageInfoPresenter,
                playerStatusPresenter,
                notificationPresenter);

            source.PublishSnapshot(CreateSnapshot(
                isPaused: false,
                isUiBlocked: true,
                hasBlockingPresentation: false,
                currentHp: 2,
                activeActionKind: GameplayUiActionKind.Flip,
                isRecoveryPhase: true,
                lastOutcome: GameplayUiActionResolutionKind.Blocked,
                stageDisplayName: "Stage 1-1"));

            Assert.That(rootPresenter.ViewModel.IsDimmed, Is.True);
            Assert.That(rootPresenter.ViewModel.IsPauseButtonEnabled, Is.False);
            Assert.That(stageInfoPresenter.ViewModel.StageName, Is.EqualTo("Stage 1-1"));
            Assert.That(stageInfoPresenter.ViewModel.HasStageName, Is.True);
            Assert.That(playerStatusPresenter.ViewModel.FacingText, Is.EqualTo("Right"));
            Assert.That(playerStatusPresenter.ViewModel.TopologyText, Is.EqualTo("Front"));
            Assert.That(playerStatusPresenter.ViewModel.HasRemainingChances, Is.False);
            Assert.That(playerStatusPresenter.ViewModel.MaxChances, Is.EqualTo(0));
            Assert.That(notificationPresenter.ViewModel.Items.Count, Is.EqualTo(1));
        }

        [Test]
        public void NotificationPresenter_DoesNotExposePlayerActionOrOutcomeEvents()
        {
            var presenter = new NotificationPresenter();

            presenter.Apply(CreateSnapshot().Notifications);

            Assert.That(presenter.ViewModel.Items.Count, Is.EqualTo(1));
            Assert.That(presenter.ViewModel.Items[0].MessageText, Is.EqualTo("T3: Took 1 damage."));
        }

        [Test]
        public void PlayerStatusPresenter_CarriesRemainingAndMaxChances_ForHeartHud()
        {
            var presenter = new PlayerStatusPresenter();

            presenter.Apply(
                new UITickSlice(4, new GameplayUiTopology(GameplayUiFace.Front), false, false),
                new UIInteractionSlice(false, true, false, false),
                new UIPlayerActionSlice(
                    playerEntityId: 10,
                    currentHp: 2,
                    maxHp: 4,
                    facing: GameplayUiDirection.Right,
                    activeActionKind: GameplayUiActionKind.None,
                    isRecoveryPhase: false,
                    canMoveThisTick: true,
                    canStartActionThisTick: true,
                    lastResolvedOutcome: GameplayUiActionResolutionKind.None,
                    lastResolvedTickIndex: 0,
                    tookDamageThisTick: false,
                    lastDamageAmount: 0,
                    lastDamageTickIndex: 0,
                    hasRemainingChances: true,
                    remainingChances: 2,
                    maxChances: 3));

            Assert.That(presenter.ViewModel.HasRemainingChances, Is.True);
            Assert.That(presenter.ViewModel.RemainingChances, Is.EqualTo(2));
            Assert.That(presenter.ViewModel.MaxChances, Is.EqualTo(3));
            Assert.That(presenter.ViewModel.FacingText, Is.EqualTo("Right"));
            Assert.That(presenter.ViewModel.TopologyText, Is.EqualTo("Front"));
        }

        [Test]
        public void HUDRootPresenter_RefreshOnlyInteractionChanges_UpdateShellReadOnlyState_ThroughMappedSourceOnly()
        {
            var source = new ManualGameplayUiPresentationSource();
            var playerStatusPresenter = new PlayerStatusPresenter();
            var stageInfoPresenter = new StageInfoPresenter();
            var notificationPresenter = new NotificationPresenter();
            using var rootPresenter = new HUDRootPresenter(
                source,
                stageInfoPresenter,
                playerStatusPresenter,
                notificationPresenter);

            source.PublishSnapshot(CreateSnapshot(isPaused: true));
            Assert.That(rootPresenter.ViewModel.IsDimmed, Is.True);
            Assert.That(rootPresenter.ViewModel.IsPauseButtonEnabled, Is.False);
            Assert.That(rootPresenter.ViewModel.IsGameplayReadOnly, Is.True);

            source.PublishSnapshot(CreateSnapshot(hasBlockingPresentation: true));
            Assert.That(rootPresenter.ViewModel.IsDimmed, Is.True);
            Assert.That(rootPresenter.ViewModel.IsPauseButtonEnabled, Is.True);
            Assert.That(rootPresenter.ViewModel.IsGameplayReadOnly, Is.True);

            source.PublishSnapshot(CreateSnapshot(isUiBlocked: true));
            Assert.That(rootPresenter.ViewModel.IsDimmed, Is.True);
            Assert.That(rootPresenter.ViewModel.IsGameplayReadOnly, Is.True);

            source.PublishSnapshot(CreateSnapshot(canAcceptGameplayCommands: false));
            Assert.That(rootPresenter.ViewModel.IsDimmed, Is.False);
            Assert.That(rootPresenter.ViewModel.IsGameplayReadOnly, Is.True);

            source.PublishSnapshot(CreateSnapshot());
            Assert.That(rootPresenter.ViewModel.IsDimmed, Is.False);
            Assert.That(rootPresenter.ViewModel.IsPauseButtonEnabled, Is.True);
            Assert.That(rootPresenter.ViewModel.IsGameplayReadOnly, Is.False);
        }

        [Test]
        public void ActionBarPresenter_UsesAuthoritativeRecoveryCountdown_AndHidesZero()
        {
            var presenter = new ActionBarPresenter();

            presenter.Apply(
                new UITickSlice(6, new GameplayUiTopology(GameplayUiFace.Front), false, false),
                new UIInteractionSlice(false, true, false, false),
                new UIPlayerActionSlice(
                    playerEntityId: 10,
                    currentHp: 3,
                    facing: GameplayUiDirection.Right,
                    activeActionKind: GameplayUiActionKind.Flip,
                    isRecoveryPhase: true,
                    canMoveThisTick: false,
                    canStartActionThisTick: false,
                    lastResolvedOutcome: GameplayUiActionResolutionKind.Success,
                    lastResolvedTickIndex: 5,
                    tookDamageThisTick: false,
                    lastDamageAmount: 0,
                    lastDamageTickIndex: 0,
                    recoveryCooldown: new UIRecoveryCooldownSlice(
                        GameplayUiActionKind.Flip,
                        remainingRecoveryTicks: 2,
                        totalRecoveryTicks: 2)));

            Assert.That(presenter.ViewModel.Slots[1].StateText, Is.EqualTo("Recovering: 2"));
            Assert.That(presenter.ViewModel.Slots[1].CooldownNormalized, Is.EqualTo(0f));

            presenter.Apply(
                new UITickSlice(7, new GameplayUiTopology(GameplayUiFace.Front), false, false),
                new UIInteractionSlice(false, true, false, false),
                new UIPlayerActionSlice(
                    playerEntityId: 10,
                    currentHp: 3,
                    facing: GameplayUiDirection.Right,
                    activeActionKind: GameplayUiActionKind.Flip,
                    isRecoveryPhase: true,
                    canMoveThisTick: false,
                    canStartActionThisTick: false,
                    lastResolvedOutcome: GameplayUiActionResolutionKind.Success,
                    lastResolvedTickIndex: 5,
                    tookDamageThisTick: false,
                    lastDamageAmount: 0,
                    lastDamageTickIndex: 0,
                    recoveryCooldown: new UIRecoveryCooldownSlice(
                        GameplayUiActionKind.Flip,
                        remainingRecoveryTicks: 1,
                        totalRecoveryTicks: 2)));

            Assert.That(presenter.ViewModel.Slots[1].StateText, Is.EqualTo("Recovering: 1"));
            Assert.That(presenter.ViewModel.Slots[1].CooldownNormalized, Is.EqualTo(0.5f));

            presenter.Apply(
                new UITickSlice(8, new GameplayUiTopology(GameplayUiFace.Front), false, false),
                new UIInteractionSlice(false, true, false, false),
                new UIPlayerActionSlice(
                    playerEntityId: 10,
                    currentHp: 3,
                    facing: GameplayUiDirection.Right,
                    activeActionKind: GameplayUiActionKind.Flip,
                    isRecoveryPhase: true,
                    canMoveThisTick: false,
                    canStartActionThisTick: false,
                    lastResolvedOutcome: GameplayUiActionResolutionKind.Success,
                    lastResolvedTickIndex: 5,
                    tookDamageThisTick: false,
                    lastDamageAmount: 0,
                    lastDamageTickIndex: 0));

            Assert.That(presenter.ViewModel.Slots[1].StateText, Is.EqualTo("Recovering"));
            Assert.That(presenter.ViewModel.Slots[1].CooldownNormalized, Is.EqualTo(0f));
        }

        [Test]
        public void ActionBarPresenter_PushSlot_UsesReadyAndArmedStatesFromExplicitPushContract()
        {
            var presenter = new ActionBarPresenter();

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
                    lastDamageTickIndex: 0,
                    canStartAnyActionThisTick: true,
                    hasExplicitPushCandidateInCurrentDirection: false));

            Assert.That(presenter.ViewModel.Slots[0].StateText, Is.EqualTo("Ready"));
            Assert.That(presenter.ViewModel.Slots[0].IsArmed, Is.False);
            Assert.That(presenter.ViewModel.Slots[0].CooldownNormalized, Is.EqualTo(1f));

            presenter.Apply(
                new UITickSlice(5, new GameplayUiTopology(GameplayUiFace.Front), false, false),
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
                    lastDamageTickIndex: 0,
                    canStartAnyActionThisTick: true,
                    hasExplicitPushCandidateInCurrentDirection: true));

            Assert.That(presenter.ViewModel.Slots[0].StateText, Is.EqualTo("Armed"));
            Assert.That(presenter.ViewModel.Slots[0].IsArmed, Is.True);
            Assert.That(presenter.ViewModel.Slots[0].CooldownNormalized, Is.EqualTo(1f));
        }

        [Test]
        public void ActionBarPresenter_PushSlot_IsUnavailableWhenCanStartAnyActionThisTickIsFalse()
        {
            var presenter = new ActionBarPresenter();

            presenter.Apply(
                new UITickSlice(6, new GameplayUiTopology(GameplayUiFace.Front), false, false),
                new UIInteractionSlice(false, true, false, false),
                new UIPlayerActionSlice(
                    playerEntityId: 10,
                    currentHp: 3,
                    facing: GameplayUiDirection.Right,
                    activeActionKind: GameplayUiActionKind.None,
                    isRecoveryPhase: false,
                    canMoveThisTick: true,
                    canStartActionThisTick: false,
                    lastResolvedOutcome: GameplayUiActionResolutionKind.None,
                    lastResolvedTickIndex: 0,
                    tookDamageThisTick: false,
                    lastDamageAmount: 0,
                    lastDamageTickIndex: 0,
                    canStartAnyActionThisTick: false,
                    hasExplicitPushCandidateInCurrentDirection: true));

            Assert.That(presenter.ViewModel.Slots[0].StateText, Is.EqualTo("Unavailable"));
            Assert.That(presenter.ViewModel.Slots[0].IsArmed, Is.False);
            Assert.That(presenter.ViewModel.IsInteractive, Is.False);
            Assert.That(presenter.ViewModel.Slots[0].CooldownNormalized, Is.EqualTo(1f));
        }

        [Test]
        public void ActionBarPresenter_UsesReplaceableSlotDefinitions_ForStageFiveDefaults()
        {
            var presenter = new ActionBarPresenter(new[]
            {
                new ActionBarSlotDefinition(HudActionSlotId.Primary, "Advance", GameplayUiActionKind.Push),
                new ActionBarSlotDefinition(HudActionSlotId.Secondary, "Turn Left", GameplayUiActionKind.Flip),
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

        [Test]
        public void ActionBarPresenter_MapsRecoveryCooldown_ByActionKindThroughReplaceableSlotDefinitions()
        {
            var presenter = new ActionBarPresenter(new[]
            {
                new ActionBarSlotDefinition(HudActionSlotId.Primary, "Flip First", GameplayUiActionKind.Flip),
                new ActionBarSlotDefinition(HudActionSlotId.Secondary, "Push Second", GameplayUiActionKind.Push),
            });

            presenter.Apply(
                new UITickSlice(4, new GameplayUiTopology(GameplayUiFace.Front), false, false),
                new UIInteractionSlice(false, true, false, false),
                new UIPlayerActionSlice(
                    playerEntityId: 10,
                    currentHp: 3,
                    facing: GameplayUiDirection.Right,
                    activeActionKind: GameplayUiActionKind.Push,
                    isRecoveryPhase: true,
                    canMoveThisTick: false,
                    canStartActionThisTick: false,
                    lastResolvedOutcome: GameplayUiActionResolutionKind.Success,
                    lastResolvedTickIndex: 3,
                    tookDamageThisTick: false,
                    lastDamageAmount: 0,
                    lastDamageTickIndex: 0,
                    recoveryCooldown: new UIRecoveryCooldownSlice(
                        GameplayUiActionKind.Push,
                        remainingRecoveryTicks: 2,
                        totalRecoveryTicks: 2)));

            Assert.That(presenter.ViewModel.Slots[0].StateText, Is.EqualTo("Unavailable"));
            Assert.That(presenter.ViewModel.Slots[1].StateText, Is.EqualTo("Recovering: 2"));
            Assert.That(presenter.ViewModel.Slots[0].CooldownNormalized, Is.EqualTo(1f));
            Assert.That(presenter.ViewModel.Slots[1].CooldownNormalized, Is.EqualTo(0f));
        }

        private static UIPresentationSnapshot CreateSnapshot(
            bool isPaused = false,
            bool isUiBlocked = false,
            bool hasBlockingPresentation = false,
            bool? canAcceptGameplayCommands = null,
            int currentHp = 3,
            GameplayUiActionKind activeActionKind = GameplayUiActionKind.None,
            bool isRecoveryPhase = false,
            bool canStartActionThisTick = true,
            bool canStartAnyActionThisTick = true,
            bool hasExplicitPushCandidateInCurrentDirection = false,
            GameplayUiActionResolutionKind lastOutcome = GameplayUiActionResolutionKind.None,
            UITickEventKind notificationEventKind = UITickEventKind.PlayerActionResolved,
            GameplayUiActionKind notificationActionKind = GameplayUiActionKind.Flip,
            GameplayUiActionResolutionKind notificationResolutionKind = GameplayUiActionResolutionKind.Success,
            UIRecoveryCooldownSlice? recoveryCooldown = null,
            string stageDisplayName = "")
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
                new UIStageSlice(
                    string.IsNullOrWhiteSpace(stageDisplayName)
                        ? StageId.None
                        : StageId.CreateOrThrow("stage-1-1"),
                    stageDisplayName),
                new UIPlayerActionSlice(
                    playerEntityId: 10,
                    currentHp: currentHp,
                    facing: GameplayUiDirection.Right,
                    activeActionKind: activeActionKind,
                    isRecoveryPhase: isRecoveryPhase,
                    canMoveThisTick: true,
                    canStartActionThisTick: canStartActionThisTick,
                    lastResolvedOutcome: lastOutcome,
                    lastResolvedTickIndex: lastOutcome == GameplayUiActionResolutionKind.None ? 0 : 4,
                    tookDamageThisTick: true,
                    lastDamageAmount: 1,
                    lastDamageTickIndex: 4,
                    recoveryCooldown: recoveryCooldown,
                    canStartAnyActionThisTick: canStartAnyActionThisTick,
                    hasExplicitPushCandidateInCurrentDirection: hasExplicitPushCandidateInCurrentDirection),
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
