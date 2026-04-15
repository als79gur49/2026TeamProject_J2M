using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.HUD;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Feature.UI.Tests
{
    public sealed class HUDControllerTests
    {
        [Test]
        public void HUDController_AttachView_BindsChildViewModels_AndRelaysActionBarInput()
        {
            var rootObject = new GameObject("HUDController_AttachView_BindsChildViewModels_AndRelaysActionBarInput");

            try
            {
                var rootView = rootObject.AddComponent<GameplayUiCanvasRootView>();
                rootView.EnsureHierarchy();

                var commandGateway = new FakeGameplayCommandGateway();
                var source = new ManualGameplayUiPresentationSource();
                var playerStatusPresenter = new PlayerStatusPresenter();
                var actionBarPresenter = new ActionBarPresenter(commandGateway);
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

                controller.AttachView(rootView.HudView);
                source.PublishSnapshot(CreateSnapshot());

                Assert.That(rootView.HudView.ViewModel, Is.SameAs(controller.RootViewModel));
                Assert.That(rootView.HudView.PlayerStatusView.ViewModel, Is.SameAs(controller.PlayerStatusViewModel));
                Assert.That(rootView.HudView.ActionBarView.ViewModel, Is.SameAs(controller.ActionBarViewModel));
                Assert.That(rootView.HudView.NotificationView.ViewModel, Is.SameAs(controller.NotificationViewModel));

                rootView.HudView.ActionBarView.ClickSlot(HudActionSlotId.Primary);

                Assert.That(commandGateway.SetHeldMoveDirectionCallCount, Is.EqualTo(1));
                Assert.That(controller.ActionBarViewModel.LastCommandResult.HasValue, Is.True);
                Assert.That(controller.ActionBarViewModel.LastCommandResult.Value.Accepted, Is.True);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        [Test]
        public void HUDController_Dispose_DetachesAllHudBindings()
        {
            var rootObject = new GameObject("HUDController_Dispose_DetachesAllHudBindings");

            try
            {
                var rootView = rootObject.AddComponent<GameplayUiCanvasRootView>();
                rootView.EnsureHierarchy();

                var commandGateway = new FakeGameplayCommandGateway();
                var source = new ManualGameplayUiPresentationSource();
                var playerStatusPresenter = new PlayerStatusPresenter();
                var actionBarPresenter = new ActionBarPresenter(commandGateway);
                var notificationPresenter = new NotificationPresenter();
                using var rootPresenter = new HUDRootPresenter(
                    source,
                    playerStatusPresenter,
                    actionBarPresenter,
                    notificationPresenter);
                var controller = new HUDController(
                    rootPresenter.ViewModel,
                    playerStatusPresenter.ViewModel,
                    actionBarPresenter.ViewModel,
                    notificationPresenter.ViewModel,
                    actionBarPresenter);

                controller.AttachView(rootView.HudView);
                controller.Dispose();

                Assert.That(rootView.HudView.ViewModel, Is.Null);
                Assert.That(rootView.HudView.PlayerStatusView.ViewModel, Is.Null);
                Assert.That(rootView.HudView.ActionBarView.ViewModel, Is.Null);
                Assert.That(rootView.HudView.NotificationView.ViewModel, Is.Null);
            }
            finally
            {
                DestroySupportObjects(rootObject);
            }
        }

        private static UIPresentationSnapshot CreateSnapshot(
            bool isPaused = false,
            bool isUiBlocked = false,
            bool hasBlockingPresentation = false,
            GameplayUiActionKind activeActionKind = GameplayUiActionKind.None,
            bool isRecoveryPhase = false,
            bool canMoveThisTick = true,
            bool canStartActionThisTick = true,
            GameplayUiActionResolutionKind lastOutcome = GameplayUiActionResolutionKind.None)
        {
            return new UIPresentationSnapshot(
                new UITickSlice(
                    lastReducedTickIndex: 4,
                    finalTopology: new GameplayUiTopology(GameplayUiFace.Front),
                    isStageCleared: false,
                    isTopologyTransitionActive: false),
                new UIInteractionSlice(
                    isPaused,
                    canAcceptGameplayCommands: !isPaused && !hasBlockingPresentation,
                    hasBlockingGameplayPresentation: hasBlockingPresentation,
                    isUiGameplayInputBlocked: isUiBlocked),
                new UIPlayerActionSlice(
                    playerEntityId: 10,
                    currentHp: 3,
                    facing: GameplayUiDirection.Up,
                    activeActionKind: activeActionKind,
                    isRecoveryPhase: isRecoveryPhase,
                    canMoveThisTick: canMoveThisTick,
                    canStartActionThisTick: canStartActionThisTick,
                    lastResolvedOutcome: lastOutcome,
                    lastResolvedTickIndex: lastOutcome == GameplayUiActionResolutionKind.None ? 0 : 4,
                    tookDamageThisTick: false,
                    lastDamageAmount: 0,
                    lastDamageTickIndex: 0),
                new UINotificationLedgerSlice(new[]
                {
                    new UINotificationRecord(
                        new UITickEventKey(
                            tickIndex: 4,
                            eventKind: UITickEventKind.PlayerActionResolved,
                            actorEntityId: 10,
                            actionKind: GameplayUiActionKind.Flip,
                            actionSequence: 2,
                            resolutionKind: GameplayUiActionResolutionKind.Success),
                        UITickEventKind.PlayerActionResolved,
                        damageAmount: 0,
                        expireAfterTickIndex: 8),
                }));
        }

        private static void DestroySupportObjects(GameObject rootObject)
        {
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem != null)
            {
                Object.DestroyImmediate(eventSystem.gameObject);
            }

            if (rootObject != null)
            {
                Object.DestroyImmediate(rootObject);
            }
        }
    }
}
