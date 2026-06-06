using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
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
            var objectiveHudPresenter = new ObjectiveHudPresenter();

            using var rootPresenter = new HUDRootPresenter(
                source,
                stageInfoPresenter,
                objectiveHudPresenter,
                playerStatusPresenter);
            using var controller = new HUDController(
                rootPresenter.ViewModel,
                stageInfoPresenter.ViewModel,
                objectiveHudPresenter.ViewModel,
                playerStatusPresenter.ViewModel);

            Assert.That(source.SnapshotSubscriberCount, Is.EqualTo(1));
            Assert.That(source.TickEventSubscriberCount, Is.EqualTo(0));
        }

        [Test]
        public void HUDRootPresenter_Dispose_RemovesItsMappedSnapshotSubscription()
        {
            var source = new ManualGameplayUiPresentationSource();
            var playerStatusPresenter = new PlayerStatusPresenter();
            var stageInfoPresenter = new StageInfoPresenter();
            var objectiveHudPresenter = new ObjectiveHudPresenter();
            var rootPresenter = new HUDRootPresenter(
                source,
                stageInfoPresenter,
                objectiveHudPresenter,
                playerStatusPresenter);

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
            var objectiveHudPresenter = new ObjectiveHudPresenter();
            var chancePanelPresenter = new ChancePanelPresenter();
            var surfaceBeltIndicatorPresenter = new SurfaceBeltIndicatorPresenter();
            using var rootPresenter = new HUDRootPresenter(
                source,
                stageInfoPresenter,
                objectiveHudPresenter,
                chancePanelPresenter,
                surfaceBeltIndicatorPresenter,
                playerStatusPresenter);

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
            Assert.That(playerStatusPresenter.ViewModel.TopologyText, Is.Empty);
            Assert.That(playerStatusPresenter.ViewModel.HasRemainingChances, Is.False);
            Assert.That(playerStatusPresenter.ViewModel.MaxChances, Is.EqualTo(0));
            Assert.That(chancePanelPresenter.ViewModel.HasChances, Is.False);
            Assert.That(surfaceBeltIndicatorPresenter.ViewModel.CenterSlotIndex, Is.EqualTo(1));
            Assert.That(objectiveHudPresenter.ViewModel.IsVisible, Is.False);
        }

        [Test]
        public void PlayerStatusPresenter_DoesNotOwnChanceOrTopologyHudState()
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

            Assert.That(presenter.ViewModel.HasRemainingChances, Is.False);
            Assert.That(presenter.ViewModel.RemainingChances, Is.EqualTo(0));
            Assert.That(presenter.ViewModel.MaxChances, Is.EqualTo(0));
            Assert.That(presenter.ViewModel.FacingText, Is.EqualTo("Right"));
            Assert.That(presenter.ViewModel.TopologyText, Is.Empty);
        }

        [Test]
        public void ChancePanelPresenter_DetectsChanceLossIndex()
        {
            var presenter = new ChancePanelPresenter();

            presenter.Apply(new UIChanceSlice(true, 3, 3));
            presenter.Apply(new UIChanceSlice(true, 2, 3));

            Assert.That(presenter.ViewModel.AnimationHint.Kind, Is.EqualTo(ChanceChangeKind.Lost));
            Assert.That(presenter.ViewModel.AnimationHint.PrimarySlotIndex, Is.EqualTo(2));
        }

        [Test]
        public void ChancePanelPresenter_DetectsChanceGainIndex()
        {
            var presenter = new ChancePanelPresenter();

            presenter.Apply(new UIChanceSlice(true, 1, 3));
            presenter.Apply(new UIChanceSlice(true, 2, 3));

            Assert.That(presenter.ViewModel.AnimationHint.Kind, Is.EqualTo(ChanceChangeKind.Gained));
            Assert.That(presenter.ViewModel.AnimationHint.PrimarySlotIndex, Is.EqualTo(1));
        }

        [Test]
        public void ChancePanelPresenter_LastChanceState()
        {
            var presenter = new ChancePanelPresenter();

            presenter.Apply(new UIChanceSlice(true, 2, 3));
            presenter.Apply(new UIChanceSlice(true, 1, 3));

            Assert.That(presenter.ViewModel.IsLastChance, Is.True);
            Assert.That(presenter.ViewModel.AnimationHint.Kind, Is.EqualTo(ChanceChangeKind.LastChanceEntered));
        }

        [Test]
        public void ChancePanelPresenter_FinalChanceLostShowsAllSlotsEmpty()
        {
            var presenter = new ChancePanelPresenter();

            presenter.Apply(new UIChanceSlice(true, 1, 3));
            presenter.Apply(new UIChanceSlice(true, 0, 3));

            Assert.That(presenter.ViewModel.RemainingChances, Is.EqualTo(0));
            Assert.That(presenter.ViewModel.Slots.Count, Is.EqualTo(3));
            Assert.That(presenter.ViewModel.Slots.All(slot => !slot.IsFilled), Is.True);
            Assert.That(presenter.ViewModel.AnimationHint.Kind, Is.EqualTo(ChanceChangeKind.Lost));
            Assert.That(presenter.ViewModel.AnimationHint.PrimarySlotIndex, Is.EqualTo(0));
            Assert.That(presenter.ViewModel.AnimationHint.AudioCuePolicy, Is.EqualTo(ChanceChangeAudioCuePolicy.Default));
        }

        [Test]
        public void ChancePanelPresenter_FinalChanceLostSuppressPolicyKeepsVisualHint()
        {
            var presenter = new ChancePanelPresenter();

            presenter.Apply(new UIChanceSlice(true, 1, 3));
            presenter.Apply(new UIChanceSlice(
                true,
                0,
                3,
                GameplayChanceAudioPolicy.SuppressChanceChangeCue));

            Assert.That(presenter.ViewModel.RemainingChances, Is.EqualTo(0));
            Assert.That(presenter.ViewModel.Slots.All(slot => !slot.IsFilled), Is.True);
            Assert.That(presenter.ViewModel.AnimationHint.Kind, Is.EqualTo(ChanceChangeKind.Lost));
            Assert.That(presenter.ViewModel.AnimationHint.PrimarySlotIndex, Is.EqualTo(0));
            Assert.That(presenter.ViewModel.AnimationHint.AudioCuePolicy, Is.EqualTo(ChanceChangeAudioCuePolicy.Suppress));
        }

        [Test]
        public void HudUiAudioFeedbackController_SuppressesTerminalChanceLossCue()
        {
            var presenter = new ChancePanelPresenter();
            var uiAudioPort = new RecordingUiAudioPort();
            using var controller = new HudUiAudioFeedbackController(
                uiAudioPort,
                presenter.ViewModel,
                new ObjectiveHudViewModel());

            presenter.Apply(new UIChanceSlice(true, 1, 3));
            presenter.Apply(new UIChanceSlice(
                true,
                0,
                3,
                GameplayChanceAudioPolicy.SuppressChanceChangeCue));

            Assert.That(uiAudioPort.PlayedCueIds, Is.Empty);
        }

        [Test]
        public void HudUiAudioFeedbackController_DefaultFinalChanceLossPlaysChanceLossCue()
        {
            var presenter = new ChancePanelPresenter();
            var uiAudioPort = new RecordingUiAudioPort();
            using var controller = new HudUiAudioFeedbackController(
                uiAudioPort,
                presenter.ViewModel,
                new ObjectiveHudViewModel());

            presenter.Apply(new UIChanceSlice(true, 1, 3));
            presenter.Apply(new UIChanceSlice(true, 0, 3));

            Assert.That(uiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.ChanceLoss }));
        }

        [Test]
        public void SurfaceBeltIndicatorPresenter_MapsCurrentFaceToSevenAuthoredCells()
        {
            var presenter = new SurfaceBeltIndicatorPresenter();

            presenter.Apply(new SurfaceBeltSnapshot(
                currentSlotIndex: 2,
                sourceSlotIndex: 2,
                destinationSlotIndex: 2,
                SurfaceBeltDirection.None,
                isTransitioning: false,
                transitionSequenceId: 0));

            Assert.That(presenter.ViewModel.CenterSlotIndex, Is.EqualTo(2));
            Assert.That(presenter.ViewModel.Cells, Has.Length.EqualTo(7));
            Assert.That(presenter.ViewModel.Cells.Single(cell => cell.IsCurrent).SlotIndex, Is.EqualTo(2));
        }

        [Test]
        public void SurfaceBeltIndicatorPresenter_PreservesTransitionState()
        {
            var presenter = new SurfaceBeltIndicatorPresenter();

            presenter.Apply(new SurfaceBeltSnapshot(
                currentSlotIndex: 1,
                sourceSlotIndex: 0,
                destinationSlotIndex: 1,
                SurfaceBeltDirection.Forward,
                isTransitioning: true,
                transitionSequenceId: 42));

            Assert.That(presenter.ViewModel.IsTransitioning, Is.True);
            Assert.That(presenter.ViewModel.Direction, Is.EqualTo(SurfaceBeltDirection.Forward));
            Assert.That(presenter.ViewModel.TransitionSequenceId, Is.EqualTo(42));
            Assert.That(presenter.ViewModel.CenterSlotIndex, Is.EqualTo(0));
        }

        [Test]
        public void ObjectiveHudPresenter_HidesWhenNoObjective()
        {
            var presenter = new ObjectiveHudPresenter();

            presenter.Apply(UIObjectiveSlice.Empty);

            Assert.That(presenter.ViewModel.IsVisible, Is.False);
            Assert.That(presenter.ViewModel.Rows, Is.Empty);
        }

        [Test]
        public void ObjectiveHudPresenter_ShowsObjectiveConditionRows()
        {
            var presenter = new ObjectiveHudPresenter();

            presenter.Apply(CreateObjectiveSlice(summary: "Move to the exit zone."));

            Assert.That(presenter.ViewModel.IsVisible, Is.True);
            Assert.That(presenter.ViewModel.ObjectiveStableId, Is.EqualTo("test-objective|Reach the Exit|Move to the exit zone."));
            Assert.That(presenter.ViewModel.Rows.Count, Is.EqualTo(1));
            Assert.That(presenter.ViewModel.Rows[0].Text, Is.EqualTo("Reach the exit zone"));
            Assert.That(presenter.ViewModel.Rows[0].IsSatisfied, Is.False);
            Assert.That(presenter.ViewModel.Rows[0].JustSatisfied, Is.False);
        }

        [Test]
        public void ObjectiveHudPresenter_SortsRowsByConditionSortOrder()
        {
            var presenter = new ObjectiveHudPresenter();

            presenter.Apply(CreateObjectiveSlice(
                summary: "Complete the required objectives.",
                title: "Reach the Exit",
                conditions: new[]
                {
                    CreateCondition("primary-goal", isSatisfied: true, role: UIObjectiveConditionRole.PrimaryGoal, sortOrder: 0),
                    CreateCondition("Open the gate", isSatisfied: false, role: UIObjectiveConditionRole.SecondaryGoal, sortOrder: 10),
                    CreateCondition("Enter the exit room", isSatisfied: false, role: UIObjectiveConditionRole.SecondaryGoal, sortOrder: 20),
                    CreateCondition("Leave no enemies behind", isSatisfied: true, role: UIObjectiveConditionRole.SecondaryGoal, sortOrder: 30),
                }));

            Assert.That(presenter.ViewModel.Rows.Count, Is.EqualTo(4));
            Assert.That(presenter.ViewModel.Rows[0].Text, Is.EqualTo("primary-goal"));
            Assert.That(presenter.ViewModel.Rows[1].Text, Is.EqualTo("Open the gate"));
            Assert.That(presenter.ViewModel.Rows[2].Text, Is.EqualTo("Enter the exit room"));
            Assert.That(presenter.ViewModel.Rows[3].Text, Is.EqualTo("Leave no enemies behind"));
        }

        [Test]
        public void ObjectiveHudPresenter_MarksConditionJustSatisfiedOnFalseToTrueTransition()
        {
            var presenter = new ObjectiveHudPresenter();

            presenter.Apply(CreateObjectiveSlice(summary: "Move to the exit zone."));
            presenter.Apply(CreateObjectiveSlice(
                summary: "Move to the exit zone.",
                conditions: new[]
                {
                    CreateCondition(
                        "Reach the exit zone",
                        isSatisfied: true,
                        role: UIObjectiveConditionRole.PrimaryGoal,
                        sortOrder: 0),
                }));

            Assert.That(presenter.ViewModel.Rows.Count, Is.EqualTo(1));
            Assert.That(presenter.ViewModel.Rows[0].IsSatisfied, Is.True);
            Assert.That(presenter.ViewModel.Rows[0].JustSatisfied, Is.True);
        }

        [Test]
        public void ObjectiveHudPresenter_DoesNotMarkNewSatisfiedConditionAsJustSatisfied()
        {
            var presenter = new ObjectiveHudPresenter();

            presenter.Apply(CreateObjectiveSlice(summary: "Move to the exit zone."));
            presenter.Apply(CreateObjectiveSlice(
                summary: "Move to the exit zone.",
                conditions: new[]
                {
                    CreateCondition("Reach the exit zone", isSatisfied: false, role: UIObjectiveConditionRole.PrimaryGoal, sortOrder: 0),
                    CreateCondition("Open the gate", isSatisfied: true, role: UIObjectiveConditionRole.SecondaryGoal, sortOrder: 10),
                }));

            Assert.That(presenter.ViewModel.Rows.Count, Is.EqualTo(2));
            Assert.That(presenter.ViewModel.Rows[1].Text, Is.EqualTo("Open the gate"));
            Assert.That(presenter.ViewModel.Rows[1].JustSatisfied, Is.False);
        }

        [Test]
        public void ObjectiveHudPresenter_ButtonRows_AreGroupedByDisplayGoal()
        {
            var presenter = new ObjectiveHudPresenter();

            presenter.Apply(CreateObjectiveSlice(
                summary: "Activate all buttons.",
                conditions: new[]
                {
                    CreateCondition("Place a push box on the button", true, UIObjectiveConditionRole.SecondaryGoal, 10, "button-1"),
                    CreateCondition("Place a push box on the button", false, UIObjectiveConditionRole.SecondaryGoal, 20, "button-2"),
                    CreateCondition("Place a push box on the button", false, UIObjectiveConditionRole.SecondaryGoal, 30, "button-3"),
                    CreateCondition("Place a push box on the button", false, UIObjectiveConditionRole.SecondaryGoal, 40, "button-4"),
                }));

            Assert.That(presenter.ViewModel.Rows.Count, Is.EqualTo(1));
            Assert.That(presenter.ViewModel.Rows[0].Text, Is.EqualTo("Place a push box on the button (1/4)"));
            Assert.That(presenter.ViewModel.Rows[0].IsGrouped, Is.True);
            Assert.That(presenter.ViewModel.Rows[0].CompletedCount, Is.EqualTo(1));
            Assert.That(presenter.ViewModel.Rows[0].RequiredCount, Is.EqualTo(4));
            Assert.That(presenter.ViewModel.Rows[0].StableId, Does.Not.Contain("1/4"));
        }

        [Test]
        public void ObjectiveHudPresenter_MoonButtonRows_AreGroupedSeparatelyFromGenericButtons()
        {
            var presenter = new ObjectiveHudPresenter();

            presenter.Apply(CreateObjectiveSlice(
                summary: "Activate all buttons.",
                conditions: new[]
                {
                    CreateCondition("Place a push box on the button", true, UIObjectiveConditionRole.SecondaryGoal, 10, "button-1"),
                    CreateCondition("Place a push box on the button", false, UIObjectiveConditionRole.SecondaryGoal, 20, "button-2"),
                    CreateCondition("Place the MoonBlock on the button", false, UIObjectiveConditionRole.SecondaryGoal, 30, "button-3"),
                    CreateCondition("Place the MoonBlock on the button", false, UIObjectiveConditionRole.SecondaryGoal, 40, "button-4"),
                }));

            Assert.That(presenter.ViewModel.Rows.Count, Is.EqualTo(2));
            Assert.That(presenter.ViewModel.Rows[0].Text, Is.EqualTo("Place a push box on the button (1/2)"));
            Assert.That(presenter.ViewModel.Rows[0].RowKind, Is.EqualTo(ObjectiveHudRowKind.ButtonGroupGeneric));
            Assert.That(presenter.ViewModel.Rows[1].Text, Is.EqualTo("Place the MoonBlock on the button (0/2)"));
            Assert.That(presenter.ViewModel.Rows[1].RowKind, Is.EqualTo(ObjectiveHudRowKind.ButtonGroupMoon));
            Assert.That(presenter.ViewModel.Rows[0].StableId, Is.Not.EqualTo(presenter.ViewModel.Rows[1].StableId));
        }

        [Test]
        public void ObjectiveHudPresenter_GroupedRow_StableId_DoesNotChangeWhenCompletedCountChanges()
        {
            var presenter = new ObjectiveHudPresenter();

            presenter.Apply(CreateObjectiveSlice(
                summary: "Activate all buttons.",
                conditions: new[]
                {
                    CreateCondition("Place a push box on the button", true, UIObjectiveConditionRole.SecondaryGoal, 10, "button-1"),
                    CreateCondition("Place a push box on the button", false, UIObjectiveConditionRole.SecondaryGoal, 20, "button-2"),
                    CreateCondition("Place a push box on the button", false, UIObjectiveConditionRole.SecondaryGoal, 30, "button-3"),
                    CreateCondition("Place a push box on the button", false, UIObjectiveConditionRole.SecondaryGoal, 40, "button-4"),
                }));
            var stableId = presenter.ViewModel.Rows[0].StableId;

            presenter.Apply(CreateObjectiveSlice(
                summary: "Activate all buttons.",
                conditions: new[]
                {
                    CreateCondition("Place a push box on the button", true, UIObjectiveConditionRole.SecondaryGoal, 10, "button-1"),
                    CreateCondition("Place a push box on the button", true, UIObjectiveConditionRole.SecondaryGoal, 20, "button-2"),
                    CreateCondition("Place a push box on the button", false, UIObjectiveConditionRole.SecondaryGoal, 30, "button-3"),
                    CreateCondition("Place a push box on the button", false, UIObjectiveConditionRole.SecondaryGoal, 40, "button-4"),
                }));

            Assert.That(presenter.ViewModel.Rows[0].StableId, Is.EqualTo(stableId));
            Assert.That(presenter.ViewModel.Rows[0].Text, Is.EqualTo("Place a push box on the button (2/4)"));
            Assert.That(presenter.ViewModel.Rows[0].JustSatisfied, Is.False);
        }

        [Test]
        public void HUDRootPresenter_FansOutObjectiveSliceToObjectivePresenter()
        {
            var source = new ManualGameplayUiPresentationSource();
            var playerStatusPresenter = new PlayerStatusPresenter();
            var stageInfoPresenter = new StageInfoPresenter();
            var objectiveHudPresenter = new ObjectiveHudPresenter();
            using var rootPresenter = new HUDRootPresenter(
                source,
                stageInfoPresenter,
                objectiveHudPresenter,
                playerStatusPresenter);

            source.PublishSnapshot(CreateSnapshot(objective: CreateObjectiveSlice(summary: "Move to the exit zone.")));

            Assert.That(objectiveHudPresenter.ViewModel.IsVisible, Is.True);
            Assert.That(objectiveHudPresenter.ViewModel.Rows.Count, Is.EqualTo(1));
            Assert.That(objectiveHudPresenter.ViewModel.Rows[0].Text, Is.EqualTo("Reach the exit zone"));
        }

        [Test]
        public void HUDRootPresenter_RefreshOnlyInteractionChanges_UpdateShellReadOnlyState_ThroughMappedSourceOnly()
        {
            var source = new ManualGameplayUiPresentationSource();
            var playerStatusPresenter = new PlayerStatusPresenter();
            var stageInfoPresenter = new StageInfoPresenter();
            var objectiveHudPresenter = new ObjectiveHudPresenter();
            using var rootPresenter = new HUDRootPresenter(
                source,
                stageInfoPresenter,
                objectiveHudPresenter,
                playerStatusPresenter);

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
        public void HUDRootPresenter_NonBlockingMoonBlockLocalPresentation_DoesNotDimShell()
        {
            var source = new ManualGameplayUiPresentationSource();
            var playerStatusPresenter = new PlayerStatusPresenter();
            var stageInfoPresenter = new StageInfoPresenter();
            var objectiveHudPresenter = new ObjectiveHudPresenter();
            using var rootPresenter = new HUDRootPresenter(
                source,
                stageInfoPresenter,
                objectiveHudPresenter,
                playerStatusPresenter);

            source.PublishSnapshot(CreateSnapshot(
                hasBlockingPresentation: false,
                canAcceptGameplayCommands: true));

            Assert.That(rootPresenter.ViewModel.IsDimmed, Is.False);
            Assert.That(rootPresenter.ViewModel.IsGameplayReadOnly, Is.False);
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
            string stageDisplayName = "",
            UIObjectiveSlice? objective = null)
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
                objective ?? UIObjectiveSlice.Empty,
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

        private static UIObjectiveSlice CreateObjectiveSlice(
            string summary,
            string title = "Reach the Exit",
            bool isCleared = false,
            IReadOnlyList<UIObjectiveConditionSlice> conditions = null)
        {
            return new UIObjectiveSlice(
                hasObjective: true,
                objectiveStableId: $"test-objective|{title}|{summary}",
                title,
                summary,
                goalReached: false,
                allConditionsSatisfied: false,
                isCleared,
                conditions ?? new[]
                {
                    CreateCondition(
                        "Reach the exit zone",
                        isSatisfied: false,
                        role: UIObjectiveConditionRole.PrimaryGoal,
                        sortOrder: 0),
                });
        }

        private static UIObjectiveConditionSlice CreateCondition(
            string titleText,
            bool isSatisfied,
            UIObjectiveConditionRole role,
            int sortOrder,
            string stableId = "")
        {
            return new UIObjectiveConditionSlice(
                stableId,
                titleText,
                progressText: string.Empty,
                isSatisfied,
                required: true,
                role,
                sortOrder);
        }
    }
}
