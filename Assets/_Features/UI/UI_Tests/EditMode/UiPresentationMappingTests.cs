using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Gameplay.UIAccess.Presentation;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.HUD;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class UiPresentationMappingTests
    {
        [Test]
        public void UITickEventRouter_RoutesSameTickEventsInCanonicalPriorityOrder()
        {
            var router = new UITickEventRouter();
            var frame = new GameplayPresentationFrame(
                tickIndex: 12,
                finalTopology: new GameplayUiTopology(GameplayUiFace.Front),
                topology: new GameplayTopologyPresentationSlice(
                    new GameplayUiTopology(GameplayUiFace.Floor),
                    new GameplayUiTopology(GameplayUiFace.Front),
                    GameplayUiRotationKind.Forward),
                player: CreatePlayerSlice(
                    activeActionKind: GameplayUiActionKind.Flip,
                    actionSequence: 7,
                    executedThisTick: true,
                    completedThisTick: true,
                    resolutionKind: GameplayUiActionResolutionKind.Impact,
                    tookDamageThisTick: true,
                    damageAmount: 2),
                stageEvent: new GameplayStageEventPresentationSlice(GameplayStageEventKind.Cleared));

            var events = router.Route(frame).ToArray();

            Assert.That(
                events.Select(evt => evt.EventKind).ToArray(),
                Is.EqualTo(new[]
                {
                    UITickEventKind.TopologyTransitionStarted,
                    UITickEventKind.PlayerActionResolved,
                    UITickEventKind.PlayerActionCompleted,
                    UITickEventKind.PlayerDamaged,
                    UITickEventKind.StageCleared,
                }));
        }

        [Test]
        public void UITickEventRouter_CanceledActionSuppressesResolvedAndCompletedEvents()
        {
            var router = new UITickEventRouter();
            var frame = new GameplayPresentationFrame(
                tickIndex: 4,
                finalTopology: new GameplayUiTopology(GameplayUiFace.Floor),
                player: CreatePlayerSlice(
                    activeActionKind: GameplayUiActionKind.Push,
                    actionSequence: 3,
                    executedThisTick: true,
                    completedThisTick: true,
                    canceledThisTick: true,
                    resolutionKind: GameplayUiActionResolutionKind.Blocked));

            var events = router.Route(frame).ToArray();

            Assert.That(events.Select(evt => evt.EventKind).ToArray(), Is.EqualTo(new[] { UITickEventKind.PlayerActionCanceled }));
        }

        [Test]
        public void UITickEventRouter_SameFrameRoutesDeterministically()
        {
            var router = new UITickEventRouter();
            var frame = new GameplayPresentationFrame(
                tickIndex: 8,
                finalTopology: new GameplayUiTopology(GameplayUiFace.Floor),
                player: CreatePlayerSlice(
                    activeActionKind: GameplayUiActionKind.Flip,
                    actionSequence: 5,
                    startedThisTick: true,
                    executedThisTick: true,
                    resolutionKind: GameplayUiActionResolutionKind.Success));

            var first = router.Route(frame).ToArray();
            var second = router.Route(frame).ToArray();

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void UIStateMapper_ReduceTick_DedupesDuplicateEventKeysAndRetainsOutcome()
        {
            var mapper = new UIStateMapper();
            var refreshInput = CreateRefreshInput(
                tickIndex: 5,
                shouldUpdateTickIndex: true,
                activeActionKind: GameplayUiActionKind.Flip,
                isRecoveryPhase: true,
                canMoveThisTick: false,
                canStartActionThisTick: false);
            var resolvedEvent = CreateEvent(
                tickIndex: 5,
                eventKind: UITickEventKind.PlayerActionResolved,
                actorEntityId: 10,
                actionKind: GameplayUiActionKind.Flip,
                actionSequence: 9,
                resolutionKind: GameplayUiActionResolutionKind.Impact);

            var result = mapper.ReduceTick(
                UIPresentationSnapshot.Empty,
                refreshInput,
                new[] { resolvedEvent, resolvedEvent });

            Assert.That(result.AppliedEvents.Count, Is.EqualTo(1));
            Assert.That(result.Snapshot.Player.LastResolvedOutcome, Is.EqualTo(GameplayUiActionResolutionKind.Impact));
            Assert.That(result.Snapshot.Player.LastResolvedTickIndex, Is.EqualTo(5));
            Assert.That(result.Snapshot.Player.IsRecoveryPhase, Is.True);
            Assert.That(result.Snapshot.Notifications.ActiveNotifications.Count, Is.EqualTo(1));
        }

        [Test]
        public void UIStateMapper_ReduceRefresh_UpdatesInteractionWithoutCreatingFakeTickEvents()
        {
            var mapper = new UIStateMapper();
            var damageResult = mapper.ReduceTick(
                UIPresentationSnapshot.Empty,
                CreateRefreshInput(tickIndex: 6, shouldUpdateTickIndex: true),
                new[]
                {
                    CreateEvent(
                        tickIndex: 6,
                        eventKind: UITickEventKind.PlayerDamaged,
                        actorEntityId: 10,
                        damageAmount: 1),
                });

            var refreshResult = mapper.ReduceRefresh(
                damageResult.Snapshot,
                CreateRefreshInput(
                    isPaused: true,
                    canAcceptGameplayCommands: false,
                    hasBlockingGameplayPresentation: true,
                    isUiGameplayInputBlocked: true));

            Assert.That(refreshResult.AppliedEvents, Is.Empty);
            Assert.That(refreshResult.Snapshot.Interaction.IsPaused, Is.True);
            Assert.That(refreshResult.Snapshot.Interaction.CanAcceptGameplayCommands, Is.False);
            Assert.That(refreshResult.Snapshot.Interaction.HasBlockingGameplayPresentation, Is.True);
            Assert.That(refreshResult.Snapshot.Interaction.IsUiGameplayInputBlocked, Is.True);
            Assert.That(refreshResult.Snapshot.Player.TookDamageThisTick, Is.True);
            Assert.That(refreshResult.Snapshot.Notifications.ActiveNotifications.Count, Is.EqualTo(1));
        }

        [Test]
        public void UIStateMapper_ReduceRefresh_PreservesTopologyRotationKindInSurfaceBeltSnapshot()
        {
            var mapper = new UIStateMapper();

            var result = mapper.ReduceRefresh(
                UIPresentationSnapshot.Empty,
                CreateRefreshInput(
                    tickIndex: 12,
                    shouldUpdateTickIndex: true,
                    finalFace: GameplayUiFace.Front,
                    shouldUpdateFinalTopology: true,
                    isTopologyTransitionActive: true,
                    topologyPresentation: new GameplayTopologyPresentationSlice(
                        new GameplayUiTopology(GameplayUiFace.Floor),
                        new GameplayUiTopology(GameplayUiFace.Front),
                        GameplayUiRotationKind.Forward)));

            Assert.That(result.Snapshot.SurfaceBelt.SourceSlotIndex, Is.EqualTo(0));
            Assert.That(result.Snapshot.SurfaceBelt.DestinationSlotIndex, Is.EqualTo(1));
            Assert.That(result.Snapshot.SurfaceBelt.CurrentSlotIndex, Is.EqualTo(1));
            Assert.That(result.Snapshot.SurfaceBelt.Direction, Is.EqualTo(SurfaceBeltDirection.Forward));
            Assert.That(result.Snapshot.SurfaceBelt.IsTransitioning, Is.True);
            Assert.That(result.Snapshot.SurfaceBelt.TransitionSequenceId, Is.GreaterThan(0));
        }

        [Test]
        public void UIStateMapper_ReduceRefresh_MapsMissingRotationKindToNoSurfaceBeltDirection()
        {
            var mapper = new UIStateMapper();

            var result = mapper.ReduceRefresh(
                UIPresentationSnapshot.Empty,
                CreateRefreshInput(
                    tickIndex: 13,
                    shouldUpdateTickIndex: true,
                    finalFace: GameplayUiFace.Front,
                    shouldUpdateFinalTopology: true,
                    isTopologyTransitionActive: true,
                    topologyPresentation: new GameplayTopologyPresentationSlice(
                        new GameplayUiTopology(GameplayUiFace.Floor),
                        new GameplayUiTopology(GameplayUiFace.Front),
                        GameplayUiRotationKind.None)));

            Assert.That(result.Snapshot.SurfaceBelt.Direction, Is.EqualTo(SurfaceBeltDirection.None));
            Assert.That(result.Snapshot.SurfaceBelt.SourceSlotIndex, Is.EqualTo(0));
            Assert.That(result.Snapshot.SurfaceBelt.DestinationSlotIndex, Is.EqualTo(1));
        }

        [Test]
        public void UIStateMapper_ReduceRefresh_MapsSurfaceButtonRemaindersIntoSurfaceBeltSnapshot()
        {
            var mapper = new UIStateMapper();

            var result = mapper.ReduceRefresh(
                UIPresentationSnapshot.Empty,
                CreateRefreshInput(
                    surfaceButtonRemainders: new[]
                    {
                        new UISurfaceButtonRemainderInput(GameplayUiFace.Front, 2, 1),
                        new UISurfaceButtonRemainderInput(GameplayUiFace.Back, 0, 3),
                    }));

            Assert.That(
                result.Snapshot.SurfaceBelt.ButtonRemainders.Count,
                Is.EqualTo(SurfaceBeltSlotMapping.SurfaceCount));
            Assert.That(result.Snapshot.SurfaceBelt.ButtonRemainders[0].TotalRemaining, Is.EqualTo(0));
            Assert.That(result.Snapshot.SurfaceBelt.ButtonRemainders[1].SlotIndex, Is.EqualTo(1));
            Assert.That(result.Snapshot.SurfaceBelt.ButtonRemainders[1].NormalRemaining, Is.EqualTo(2));
            Assert.That(result.Snapshot.SurfaceBelt.ButtonRemainders[1].MoonBlockOnlyRemaining, Is.EqualTo(1));
            Assert.That(result.Snapshot.SurfaceBelt.ButtonRemainders[3].NormalRemaining, Is.EqualTo(0));
            Assert.That(result.Snapshot.SurfaceBelt.ButtonRemainders[3].MoonBlockOnlyRemaining, Is.EqualTo(3));
        }

        [Test]
        public void SurfaceBeltSnapshot_Empty_ProvidesFourZeroButtonRemainders()
        {
            Assert.That(
                SurfaceBeltSnapshot.Empty.ButtonRemainders.Count,
                Is.EqualTo(SurfaceBeltSlotMapping.SurfaceCount));
            Assert.That(SurfaceBeltSnapshot.Empty.ButtonRemainders.All(remainder => remainder.TotalRemaining == 0), Is.True);
        }

        [Test]
        public void UIStateMapper_ReduceRefresh_MapsMinimalRecoveryCooldownSlice()
        {
            var mapper = new UIStateMapper();

            var result = mapper.ReduceRefresh(
                UIPresentationSnapshot.Empty,
                CreateRefreshInput(
                    tickIndex: 6,
                    shouldUpdateTickIndex: true,
                    activeActionKind: GameplayUiActionKind.Push,
                    isRecoveryPhase: true,
                    canMoveThisTick: false,
                    canStartActionThisTick: false,
                    recoveryCooldown: new UIRecoveryCooldownSlice(
                        GameplayUiActionKind.Push,
                        remainingRecoveryTicks: 2,
                        totalRecoveryTicks: 2)));

            Assert.That(result.Snapshot.Player.RecoveryCooldown.HasValue, Is.True);
            Assert.That(result.Snapshot.Player.RecoveryCooldown.Value.ActionKind, Is.EqualTo(GameplayUiActionKind.Push));
            Assert.That(result.Snapshot.Player.RecoveryCooldown.Value.RemainingRecoveryTicks, Is.EqualTo(2));
            Assert.That(result.Snapshot.Player.RecoveryCooldown.Value.TotalRecoveryTicks, Is.EqualTo(2));
        }

        [Test]
        public void UIStateMapper_ReduceRefresh_MapsChancesCapacitySlice()
        {
            var mapper = new UIStateMapper();

            var result = mapper.ReduceRefresh(
                UIPresentationSnapshot.Empty,
                CreateRefreshInput(
                    hasRemainingChances: true,
                    remainingChances: 2,
                    maxChances: 3,
                    chanceAudioPolicy: GameplayChanceAudioPolicy.SuppressChanceChangeCue));

            Assert.That(result.Snapshot.Player.HasRemainingChances, Is.True);
            Assert.That(result.Snapshot.Player.RemainingChances, Is.EqualTo(2));
            Assert.That(result.Snapshot.Player.MaxChances, Is.EqualTo(3));
            Assert.That(result.Snapshot.Chance.AudioPolicy, Is.EqualTo(GameplayChanceAudioPolicy.SuppressChanceChangeCue));
        }

        [Test]
        public void UIStateMapper_ReduceRefresh_MapsStageSlice()
        {
            var mapper = new UIStateMapper();
            var stageId = StageId.CreateOrThrow("stage-1-1");

            var result = mapper.ReduceRefresh(
                UIPresentationSnapshot.Empty,
                CreateRefreshInput(
                    stageId: stageId,
                    stageDisplayNameKey: "stage.stage-1-1.display_name"));

            Assert.That(result.Snapshot.Stage.StageId, Is.EqualTo(stageId));
            Assert.That(result.Snapshot.Stage.DisplayNameKey, Is.EqualTo("stage.stage-1-1.display_name"));
            Assert.That(result.Snapshot.Stage.DisplayNameDescriptor.Table, Is.EqualTo("Stage"));
            Assert.That(result.Snapshot.Stage.DisplayNameDescriptor.Key, Is.EqualTo("stage.stage-1-1.display_name"));
            Assert.That(result.Snapshot.Stage.HasDisplayName, Is.True);
        }

        [Test]
        public void UIStateMapper_MapsObjectiveSlice()
        {
            var mapper = new UIStateMapper();
            var objective = CreateObjectiveReadModel(isSatisfied: false);

            var result = mapper.ReduceRefresh(
                UIPresentationSnapshot.Empty,
                CreateRefreshInput(objective: objective));

            Assert.That(result.Snapshot.Objective.HasObjective, Is.True);
            Assert.That(result.Snapshot.Objective.Title, Is.Empty);
            Assert.That(result.Snapshot.Objective.Summary, Is.Empty);
            Assert.That(result.Snapshot.Objective.Conditions, Has.Count.EqualTo(1));
            Assert.That(
                result.Snapshot.Objective.Conditions[0].PresentationKind,
                Is.EqualTo(GameplayObjectivePresentationKind.ReachExit));
            Assert.That(
                result.Snapshot.Objective.Conditions[0].TextDescriptor.Key,
                Is.EqualTo(ObjectiveHudLocalization.Keys.ReachExit));
            Assert.That(
                result.Snapshot.Objective.Conditions[0].TextDescriptor.Arguments,
                Is.EqualTo(new object[] { 0, 1 }));
            Assert.That(result.Snapshot.Objective.Conditions[0].Role, Is.EqualTo(UIObjectiveConditionRole.PrimaryGoal));
        }

        [Test]
        public void UIStateMapper_MapsGeneralZoneToDedicatedLocalizationKey()
        {
            var objective = new GameplayObjectiveReadModel(
                hasObjective: true,
                goalReached: false,
                allConditionsSatisfied: false,
                isCleared: false,
                conditions: new[]
                {
                    new GameplayObjectiveConditionReadModel(
                        stableId: "general-zone",
                        presentationKind: GameplayObjectivePresentationKind.ReachZone,
                        stableGroupKey: "reach-zone|role-1",
                        role: GameplayObjectiveConditionRole.PrimaryGoal,
                        required: true,
                        isSatisfied: false,
                        completedCount: 0,
                        requiredCount: 1,
                        sortOrder: 0),
                });

            var result = new UIStateMapper().ReduceRefresh(
                UIPresentationSnapshot.Empty,
                CreateRefreshInput(objective: objective));

            Assert.That(result.Snapshot.Objective.Conditions, Has.Count.EqualTo(1));
            Assert.That(
                result.Snapshot.Objective.Conditions[0].PresentationKind,
                Is.EqualTo(GameplayObjectivePresentationKind.ReachZone));
            Assert.That(
                result.Snapshot.Objective.Conditions[0].TextDescriptor.Key,
                Is.EqualTo(ObjectiveHudLocalization.Keys.ReachZone));
        }

        [Test]
        public void UIObjectiveSlice_EmptyWhenNoObjective()
        {
            var mapper = new UIStateMapper();

            var result = mapper.ReduceRefresh(
                UIPresentationSnapshot.Empty,
                CreateRefreshInput(objective: GameplayObjectiveReadModel.NoObjective));

            Assert.That(result.Snapshot.Objective.HasObjective, Is.False);
            Assert.That(result.Snapshot.Objective.Title, Is.Empty);
            Assert.That(result.Snapshot.Objective.Summary, Is.Empty);
            Assert.That(result.Snapshot.Objective.Conditions, Is.Empty);
        }

        [Test]
        public void UIObjectiveSlice_PreservesConditionSatisfiedState()
        {
            var mapper = new UIStateMapper();

            var result = mapper.ReduceRefresh(
                UIPresentationSnapshot.Empty,
                CreateRefreshInput(objective: CreateObjectiveReadModel(isSatisfied: true)));

            Assert.That(result.Snapshot.Objective.Conditions, Has.Count.EqualTo(1));
            Assert.That(result.Snapshot.Objective.Conditions[0].IsSatisfied, Is.True);
        }

        [Test]
        public void UIStateMapper_UnsupportedObjectivePresentationKind_FailsClosed()
        {
            var mapper = new UIStateMapper();
            var objective = new GameplayObjectiveReadModel(
                hasObjective: true,
                goalReached: false,
                allConditionsSatisfied: false,
                isCleared: false,
                conditions: new[]
                {
                    new GameplayObjectiveConditionReadModel(
                        stableId: "unsupported",
                        presentationKind: GameplayObjectivePresentationKind.None,
                        stableGroupKey: "unsupported",
                        role: GameplayObjectiveConditionRole.SecondaryGoal,
                        required: true,
                        isSatisfied: false,
                        completedCount: 0,
                        requiredCount: 1,
                        sortOrder: 10),
                });

            var result = mapper.ReduceRefresh(
                UIPresentationSnapshot.Empty,
                CreateRefreshInput(objective: objective));

            Assert.That(result.Snapshot.Objective.HasObjective, Is.True);
            Assert.That(result.Snapshot.Objective.Conditions, Is.Empty);
            Assert.That(result.Snapshot.Objective.Title, Is.Empty);
            Assert.That(result.Snapshot.Objective.Summary, Is.Empty);
        }

        [Test]
        public void UIObjectiveSlice_PreservesVisibleAndSemanticTopLevelStateSeparately()
        {
            var mapper = new UIStateMapper();
            var objective = new GameplayObjectiveReadModel(
                hasObjective: true,
                goalReached: false,
                allConditionsSatisfied: false,
                isCleared: false,
                conditions: Array.Empty<GameplayObjectiveConditionReadModel>(),
                semanticGoalReached: true,
                semanticAllConditionsSatisfied: true,
                semanticIsCleared: true);

            var result = mapper.ReduceRefresh(
                UIPresentationSnapshot.Empty,
                CreateRefreshInput(objective: objective));

            Assert.That(result.Snapshot.Objective.GoalReached, Is.False);
            Assert.That(result.Snapshot.Objective.AllConditionsSatisfied, Is.False);
            Assert.That(result.Snapshot.Objective.IsCleared, Is.False);
            Assert.That(result.Snapshot.Objective.SemanticGoalReached, Is.True);
            Assert.That(result.Snapshot.Objective.SemanticAllConditionsSatisfied, Is.True);
            Assert.That(result.Snapshot.Objective.SemanticIsCleared, Is.True);
        }

        [Test]
        public void UIStateMapper_IdenticalInputSequences_ProduceIdenticalSnapshotsAndAppliedEvents()
        {
            var firstSequence = RunMapperSequence();
            var secondSequence = RunMapperSequence();

            Assert.That(firstSequence.finalSnapshot, Is.EqualTo(secondSequence.finalSnapshot));
            Assert.That(firstSequence.appliedEvents, Is.EqualTo(secondSequence.appliedEvents));
        }

        [Test]
        public void GameplayUiPresentationSource_DuplicateFramePublication_DoesNotDuplicateSemanticNotifications()
        {
            var queryFacade = new FakeGameplayQueryFacade(
                new GameplaySessionReadModel(1, false, true, false),
                FakeGameplayQueryFacade.CreateDefaultPlayerHud(),
                new GameplayObjectiveReadModel(false, false, false, false));
            var presentationFeed = new FakeGameplayPresentationFeed();
            var pauseService = new FakeGameplayPauseService();
            using var source = new GameplayUiPresentationSource(queryFacade, presentationFeed, pauseService);
            var appliedBatches = new List<UITickEventBatch>();
            var snapshotChangeCount = 0;

            source.TickEventsApplied += batch => appliedBatches.Add(batch);
            source.SnapshotChanged += _ => snapshotChangeCount++;

            var frame = new GameplayPresentationFrame(
                tickIndex: 3,
                finalTopology: new GameplayUiTopology(GameplayUiFace.Floor),
                player: CreatePlayerSlice(
                    activeActionKind: GameplayUiActionKind.Flip,
                    actionSequence: 2,
                    executedThisTick: true,
                    completedThisTick: true,
                    isRecoveryPhase: true,
                    resolutionKind: GameplayUiActionResolutionKind.Impact));

            presentationFeed.PublishFrame(frame);
            presentationFeed.PublishFrame(frame);

            Assert.That(appliedBatches.Count, Is.EqualTo(1));
            Assert.That(appliedBatches[0].Events.Select(evt => evt.EventKind).ToArray(), Is.EqualTo(new[]
            {
                UITickEventKind.PlayerActionResolved,
                UITickEventKind.PlayerActionCompleted,
            }));
            Assert.That(snapshotChangeCount, Is.EqualTo(1));
            Assert.That(source.CurrentSnapshot.Notifications.ActiveNotifications.Count, Is.EqualTo(2));
            Assert.That(source.CurrentSnapshot.Player.LastResolvedOutcome, Is.EqualTo(GameplayUiActionResolutionKind.Impact));
        }

        [Test]
        public void GameplayUiPresentationSource_RefreshOnlyChanges_UpdateSnapshotWithoutTickEvents()
        {
            var queryFacade = new FakeGameplayQueryFacade(
                new GameplaySessionReadModel(1, false, true, false),
                FakeGameplayQueryFacade.CreateDefaultPlayerHud(),
                new GameplayObjectiveReadModel(false, false, false, false));
            var presentationFeed = new FakeGameplayPresentationFeed();
            var pauseService = new FakeGameplayPauseService();
            using var source = new GameplayUiPresentationSource(queryFacade, presentationFeed, pauseService);
            var appliedEventCount = 0;

            source.TickEventsApplied += batch => appliedEventCount += batch.Events.Count;

            queryFacade.SetSession(new GameplaySessionReadModel(2, false, false, false));
            presentationFeed.PublishState(new GameplayPresentationState(
                new GameplayUiTopology(GameplayUiFace.Floor),
                isPresentationActive: false,
                hasBlockingPresentation: true,
                isTopologyTransitionActive: true));
            source.UpdateUiGameplayInputBlocked(true);
            pauseService.Pause();

            Assert.That(appliedEventCount, Is.EqualTo(0));
            Assert.That(source.CurrentSnapshot.Interaction.CanAcceptGameplayCommands, Is.False);
            Assert.That(source.CurrentSnapshot.Interaction.HasBlockingGameplayPresentation, Is.True);
            Assert.That(source.CurrentSnapshot.Interaction.IsUiGameplayInputBlocked, Is.True);
            Assert.That(source.CurrentSnapshot.Interaction.IsPaused, Is.True);
            Assert.That(source.CurrentSnapshot.Tick.IsTopologyTransitionActive, Is.True);
            Assert.That(source.CurrentSnapshot.Notifications.ActiveNotifications, Is.Empty);
        }

        [Test]
        public void GameplayUiPresentationSource_SemanticStageClearWaitsForPresentationFrameWhenBarrierPending()
        {
            var queryFacade = new FakeGameplayQueryFacade(
                new GameplaySessionReadModel(1, false, true, false),
                FakeGameplayQueryFacade.CreateDefaultPlayerHud(),
                new GameplayObjectiveReadModel(false, false, false, false));
            var presentationFeed = new FakeGameplayPresentationFeed();
            var pauseService = new FakeGameplayPauseService();
            using var source = new GameplayUiPresentationSource(queryFacade, presentationFeed, pauseService);
            var appliedBatches = new List<UITickEventBatch>();
            source.TickEventsApplied += batch => appliedBatches.Add(batch);

            queryFacade.SetSession(new GameplaySessionReadModel(2, false, false, true));
            presentationFeed.HasPendingStageClearPresentation = true;
            presentationFeed.PublishState(new GameplayPresentationState(
                new GameplayUiTopology(GameplayUiFace.Floor),
                isPresentationActive: false,
                hasBlockingPresentation: false,
                isTopologyTransitionActive: false));

            Assert.That(source.CurrentSnapshot.Tick.IsStageCleared, Is.False);
            Assert.That(appliedBatches, Is.Empty);

            presentationFeed.HasPendingStageClearPresentation = false;
            presentationFeed.PublishFrame(new GameplayPresentationFrame(
                tickIndex: 2,
                finalTopology: new GameplayUiTopology(GameplayUiFace.Floor),
                stageEvent: new GameplayStageEventPresentationSlice(GameplayStageEventKind.Cleared)));

            Assert.That(source.CurrentSnapshot.Tick.IsStageCleared, Is.True);
            Assert.That(appliedBatches, Has.Count.EqualTo(1));
            Assert.That(appliedBatches[0].Events.Single().EventKind, Is.EqualTo(UITickEventKind.StageCleared));
        }

        [Test]
        public void GameplayUiPresentationSource_RefreshMapsRecoveryCooldownFromPlayerHudQuery()
        {
            var queryFacade = new FakeGameplayQueryFacade(
                new GameplaySessionReadModel(1, false, true, false),
                new GameplayPlayerHudReadModel(
                    isAvailable: true,
                    playerEntityId: 10,
                    currentHp: 3,
                    maxHp: 3,
                    facing: GameplayUiDirection.Right,
                    activeActionKind: GameplayUiActionKind.Flip,
                    activeActionDirection: GameplayUiDirection.Right,
                    activeTargetEntityId: 20,
                    isActionInProgress: true,
                    isActionInRecoveryPhase: true,
                    canMoveThisTick: false,
                    canStartActionThisTick: false,
                    recoveryCooldown: new GameplayUiRecoveryCooldown(
                        GameplayUiActionKind.Flip,
                        remainingRecoveryTicks: 1,
                        totalRecoveryTicks: 2)),
                new GameplayObjectiveReadModel(false, false, false, false));
            var presentationFeed = new FakeGameplayPresentationFeed();
            var pauseService = new FakeGameplayPauseService();
            using var source = new GameplayUiPresentationSource(queryFacade, presentationFeed, pauseService);

            queryFacade.SetSession(new GameplaySessionReadModel(2, false, true, false));
            presentationFeed.PublishState(new GameplayPresentationState(
                new GameplayUiTopology(GameplayUiFace.Front),
                isPresentationActive: false,
                hasBlockingPresentation: false,
                isTopologyTransitionActive: false));

            Assert.That(source.CurrentSnapshot.Player.RecoveryCooldown.HasValue, Is.True);
            Assert.That(source.CurrentSnapshot.Player.RecoveryCooldown.Value.ActionKind, Is.EqualTo(GameplayUiActionKind.Flip));
            Assert.That(source.CurrentSnapshot.Player.RecoveryCooldown.Value.RemainingRecoveryTicks, Is.EqualTo(1));
            Assert.That(source.CurrentSnapshot.Player.RecoveryCooldown.Value.TotalRecoveryTicks, Is.EqualTo(2));
        }

        [Test]
        public void GameplayUiPresentationSource_RefreshMapsPushReadyAndArmedContractFromPlayerHudQuery()
        {
            var queryFacade = new FakeGameplayQueryFacade(
                new GameplaySessionReadModel(1, false, true, false),
                new GameplayPlayerHudReadModel(
                    isAvailable: true,
                    playerEntityId: 10,
                    currentHp: 3,
                    maxHp: 3,
                    facing: GameplayUiDirection.Right,
                    activeActionKind: GameplayUiActionKind.None,
                    activeActionDirection: GameplayUiDirection.None,
                    activeTargetEntityId: 0,
                    isActionInProgress: false,
                    isActionInRecoveryPhase: false,
                    canMoveThisTick: true,
                    canStartActionThisTick: true,
                    recoveryCooldown: null,
                    canStartAnyActionThisTick: true,
                    hasExplicitPushCandidateInCurrentDirection: true),
                new GameplayObjectiveReadModel(false, false, false, false));
            var presentationFeed = new FakeGameplayPresentationFeed();
            var pauseService = new FakeGameplayPauseService();
            using var source = new GameplayUiPresentationSource(queryFacade, presentationFeed, pauseService);

            queryFacade.SetSession(new GameplaySessionReadModel(2, false, true, false));
            presentationFeed.PublishState(new GameplayPresentationState(
                new GameplayUiTopology(GameplayUiFace.Front),
                isPresentationActive: false,
                hasBlockingPresentation: false,
                isTopologyTransitionActive: false));

            Assert.That(source.CurrentSnapshot.Player.CanStartActionThisTick, Is.True);
            Assert.That(source.CurrentSnapshot.Player.CanStartAnyActionThisTick, Is.True);
            Assert.That(source.CurrentSnapshot.Player.HasExplicitPushCandidateInCurrentDirection, Is.True);
        }

        [Test]
        public void GameplayUiPresentationSource_RefreshMapsRemainingAndMaxChancesFromPlayerHudQuery()
        {
            var queryFacade = new FakeGameplayQueryFacade(
                new GameplaySessionReadModel(1, false, true, false),
                new GameplayPlayerHudReadModel(
                    isAvailable: true,
                    playerEntityId: 10,
                    currentHp: 3,
                    maxHp: 3,
                    facing: GameplayUiDirection.Right,
                    activeActionKind: GameplayUiActionKind.None,
                    activeActionDirection: GameplayUiDirection.None,
                    activeTargetEntityId: 0,
                    isActionInProgress: false,
                    isActionInRecoveryPhase: false,
                    canMoveThisTick: true,
                    canStartActionThisTick: true,
                    recoveryCooldown: null,
                    canStartAnyActionThisTick: true,
                    hasExplicitPushCandidateInCurrentDirection: false,
                    hasRemainingChances: true,
                    remainingChances: 2,
                    maxChances: 3,
                    chanceAudioPolicy: GameplayChanceAudioPolicy.SuppressChanceChangeCue),
                new GameplayObjectiveReadModel(false, false, false, false));
            var presentationFeed = new FakeGameplayPresentationFeed();
            var pauseService = new FakeGameplayPauseService();
            using var source = new GameplayUiPresentationSource(queryFacade, presentationFeed, pauseService);

            presentationFeed.PublishState(new GameplayPresentationState(
                new GameplayUiTopology(GameplayUiFace.Front),
                isPresentationActive: false,
                hasBlockingPresentation: false,
                isTopologyTransitionActive: false));

            Assert.That(source.CurrentSnapshot.Player.HasRemainingChances, Is.True);
            Assert.That(source.CurrentSnapshot.Player.RemainingChances, Is.EqualTo(2));
            Assert.That(source.CurrentSnapshot.Player.MaxChances, Is.EqualTo(3));
            Assert.That(source.CurrentSnapshot.Chance.AudioPolicy, Is.EqualTo(GameplayChanceAudioPolicy.SuppressChanceChangeCue));
        }

        [Test]
        public void GameplayUiPresentationSource_RefreshMapsSurfaceButtonRemaindersFromQuery()
        {
            var queryFacade = new FakeGameplayQueryFacade(
                new GameplaySessionReadModel(1, false, true, false),
                FakeGameplayQueryFacade.CreateDefaultPlayerHud(),
                new GameplayObjectiveReadModel(false, false, false, false),
                surfaceButtonRemainders: new[]
                {
                    new GameplaySurfaceButtonRemainderReadModel(GameplayUiFace.Ceiling, 4, 1),
                });
            var presentationFeed = new FakeGameplayPresentationFeed();
            var pauseService = new FakeGameplayPauseService();
            using var source = new GameplayUiPresentationSource(queryFacade, presentationFeed, pauseService);

            presentationFeed.PublishState(new GameplayPresentationState(
                new GameplayUiTopology(GameplayUiFace.Floor),
                isPresentationActive: false,
                hasBlockingPresentation: false,
                isTopologyTransitionActive: false));

            Assert.That(source.CurrentSnapshot.SurfaceBelt.ButtonRemainders[2].NormalRemaining, Is.EqualTo(4));
            Assert.That(source.CurrentSnapshot.SurfaceBelt.ButtonRemainders[2].MoonBlockOnlyRemaining, Is.EqualTo(1));
        }

        [Test]
        public void GameplayUiPresentationSource_RefreshMapsStageFromStageQuery()
        {
            var stageId = StageId.CreateOrThrow("stage-1-1");
            var queryFacade = new FakeGameplayQueryFacade(
                new GameplaySessionReadModel(1, false, true, false),
                FakeGameplayQueryFacade.CreateDefaultPlayerHud(),
                new GameplayObjectiveReadModel(false, false, false, false),
                new GameplayStageReadModel(stageId, "stage.stage-1-1.display_name"));
            var presentationFeed = new FakeGameplayPresentationFeed();
            var pauseService = new FakeGameplayPauseService();
            using var source = new GameplayUiPresentationSource(queryFacade, presentationFeed, pauseService);

            presentationFeed.PublishState(new GameplayPresentationState(
                new GameplayUiTopology(GameplayUiFace.Front),
                isPresentationActive: false,
                hasBlockingPresentation: false,
                isTopologyTransitionActive: false));

            Assert.That(source.CurrentSnapshot.Stage.StageId, Is.EqualTo(stageId));
            Assert.That(source.CurrentSnapshot.Stage.DisplayNameKey, Is.EqualTo("stage.stage-1-1.display_name"));
        }

        [Test]
        public void GameplayUiPresentationSource_ReadsObjectiveQuery()
        {
            var queryFacade = new FakeGameplayQueryFacade(
                new GameplaySessionReadModel(1, false, true, false),
                FakeGameplayQueryFacade.CreateDefaultPlayerHud(),
                CreateObjectiveReadModel(isSatisfied: false));
            var presentationFeed = new FakeGameplayPresentationFeed();
            var pauseService = new FakeGameplayPauseService();
            using var source = new GameplayUiPresentationSource(queryFacade, presentationFeed, pauseService);

            presentationFeed.PublishState(new GameplayPresentationState(
                new GameplayUiTopology(GameplayUiFace.Front),
                isPresentationActive: false,
                hasBlockingPresentation: false,
                isTopologyTransitionActive: false));

            Assert.That(source.CurrentSnapshot.Objective.HasObjective, Is.True);
            Assert.That(source.CurrentSnapshot.Objective.Summary, Is.Empty);
            Assert.That(
                source.CurrentSnapshot.Objective.Conditions[0].TextDescriptor.Key,
                Is.EqualTo(ObjectiveHudLocalization.Keys.ReachExit));
        }

        [Test]
        public void GameplayUiPresentationSource_OlderFramePublication_RefreshesWithoutTickRegressionOrNewEvents()
        {
            var queryFacade = new FakeGameplayQueryFacade(
                new GameplaySessionReadModel(1, false, true, false),
                FakeGameplayQueryFacade.CreateDefaultPlayerHud(),
                new GameplayObjectiveReadModel(false, false, false, false));
            var presentationFeed = new FakeGameplayPresentationFeed();
            var pauseService = new FakeGameplayPauseService();
            using var source = new GameplayUiPresentationSource(queryFacade, presentationFeed, pauseService);
            var appliedBatches = new List<UITickEventBatch>();

            source.TickEventsApplied += batch => appliedBatches.Add(batch);

            presentationFeed.PublishFrame(new GameplayPresentationFrame(
                tickIndex: 6,
                finalTopology: new GameplayUiTopology(GameplayUiFace.Front),
                player: CreatePlayerSlice(
                    activeActionKind: GameplayUiActionKind.Flip,
                    actionSequence: 7,
                    executedThisTick: true,
                    resolutionKind: GameplayUiActionResolutionKind.Success)));

            Assert.That(source.CurrentSnapshot.Tick.LastReducedTickIndex, Is.EqualTo(6));
            Assert.That(appliedBatches, Has.Count.EqualTo(1));

            queryFacade.SetSession(new GameplaySessionReadModel(2, false, false, false));
            presentationFeed.PublishFrame(new GameplayPresentationFrame(
                tickIndex: 5,
                finalTopology: new GameplayUiTopology(GameplayUiFace.Floor)));

            Assert.That(source.CurrentSnapshot.Tick.LastReducedTickIndex, Is.EqualTo(6));
            Assert.That(source.CurrentSnapshot.Interaction.CanAcceptGameplayCommands, Is.False);
            Assert.That(source.CurrentSnapshot.Tick.FinalTopology, Is.EqualTo(new GameplayUiTopology(GameplayUiFace.Front)));
            Assert.That(appliedBatches, Has.Count.EqualTo(1));
        }

        private static GameplayPlayerPresentationSlice CreatePlayerSlice(
            GameplayUiActionKind activeActionKind = GameplayUiActionKind.None,
            int actionSequence = 0,
            bool startedThisTick = false,
            bool executedThisTick = false,
            bool completedThisTick = false,
            bool canceledThisTick = false,
            bool isRecoveryPhase = false,
            GameplayUiActionResolutionKind resolutionKind = GameplayUiActionResolutionKind.None,
            bool tookDamageThisTick = false,
            int damageAmount = 0)
        {
            return new GameplayPlayerPresentationSlice(
                playerEntityId: 10,
                activeActionKind: activeActionKind,
                activeActionSequence: actionSequence,
                actionDirection: GameplayUiDirection.Right,
                targetEntityId: 22,
                startedThisTick: startedThisTick,
                executedThisTick: executedThisTick,
                completedThisTick: completedThisTick,
                canceledThisTick: canceledThisTick,
                isRecoveryPhase: isRecoveryPhase,
                resolutionKind: resolutionKind,
                shouldPlayWalkLoop: false,
                moveMotionGeneratedThisTick: false,
                waitingForNextMoveCadence: false,
                tookDamageThisTick: tookDamageThisTick,
                damageAmount: damageAmount);
        }

        private static UIStateRefreshInput CreateRefreshInput(
            int tickIndex = 0,
            bool shouldUpdateTickIndex = false,
            GameplayUiFace finalFace = GameplayUiFace.Floor,
            bool shouldUpdateFinalTopology = false,
            bool isStageCleared = false,
            bool isTopologyTransitionActive = false,
            bool hasBlockingGameplayPresentation = false,
            bool isPaused = false,
            bool canAcceptGameplayCommands = true,
            bool isUiGameplayInputBlocked = false,
            int playerEntityId = 10,
            int currentHp = 3,
            int maxHp = 3,
            GameplayUiDirection facing = GameplayUiDirection.Up,
            GameplayUiActionKind activeActionKind = GameplayUiActionKind.None,
            bool isRecoveryPhase = false,
            bool canMoveThisTick = true,
            bool canStartActionThisTick = true,
            UIRecoveryCooldownSlice? recoveryCooldown = null,
            bool hasRemainingChances = false,
            int remainingChances = 0,
            int maxChances = 0,
            StageId stageId = default,
            string stageDisplayNameKey = "",
            GameplayObjectiveReadModel objective = default,
            GameplayTopologyPresentationSlice? topologyPresentation = null,
            GameplayChanceAudioPolicy chanceAudioPolicy = GameplayChanceAudioPolicy.Default,
            IReadOnlyList<UISurfaceButtonRemainderInput> surfaceButtonRemainders = null)
        {
            return new UIStateRefreshInput(
                tickIndex,
                shouldUpdateTickIndex,
                new GameplayUiTopology(finalFace),
                shouldUpdateFinalTopology,
                isStageCleared,
                isTopologyTransitionActive,
                hasBlockingGameplayPresentation,
                isPaused,
                canAcceptGameplayCommands,
                isUiGameplayInputBlocked,
                playerEntityId,
                currentHp,
                maxHp,
                facing,
                activeActionKind,
                isRecoveryPhase,
                canMoveThisTick,
                canStartActionThisTick,
                recoveryCooldown,
                hasRemainingChances: hasRemainingChances,
                remainingChances: remainingChances,
                maxChances: maxChances,
                stageId: stageId,
                stageDisplayNameKey: stageDisplayNameKey,
                objective: objective,
                topologyPresentation: topologyPresentation,
                chanceAudioPolicy: chanceAudioPolicy,
                surfaceButtonRemainders: surfaceButtonRemainders);
        }

        private static GameplayObjectiveReadModel CreateObjectiveReadModel(bool isSatisfied)
        {
            return new GameplayObjectiveReadModel(
                hasObjective: true,
                goalReached: isSatisfied,
                allConditionsSatisfied: isSatisfied,
                isCleared: false,
                conditions: new[]
                {
                    new GameplayObjectiveConditionReadModel(
                        stableId: "primary-goal",
                        presentationKind: GameplayObjectivePresentationKind.ReachExit,
                        stableGroupKey: "reach-exit|role-1",
                        role: GameplayObjectiveConditionRole.PrimaryGoal,
                        required: true,
                        isSatisfied: isSatisfied,
                        completedCount: isSatisfied ? 1 : 0,
                        requiredCount: 1,
                        sortOrder: 0),
                });
        }

        private static UITickEvent CreateEvent(
            int tickIndex,
            UITickEventKind eventKind,
            int actorEntityId,
            GameplayUiActionKind actionKind = GameplayUiActionKind.None,
            int actionSequence = 0,
            GameplayUiActionResolutionKind resolutionKind = GameplayUiActionResolutionKind.None,
            int damageAmount = 0)
        {
            return new UITickEvent(
                new UITickEventKey(
                    tickIndex,
                    eventKind,
                    actorEntityId,
                    actionKind,
                    actionSequence,
                    resolutionKind),
                damageAmount);
        }

        private static (UIPresentationSnapshot finalSnapshot, IReadOnlyList<UITickEvent> appliedEvents) RunMapperSequence()
        {
            var mapper = new UIStateMapper();
            var firstTick = mapper.ReduceTick(
                UIPresentationSnapshot.Empty,
                CreateRefreshInput(
                    tickIndex: 2,
                    shouldUpdateTickIndex: true,
                    activeActionKind: GameplayUiActionKind.Push,
                    isRecoveryPhase: true,
                    canMoveThisTick: false,
                    canStartActionThisTick: false),
                new[]
                {
                    CreateEvent(
                        tickIndex: 2,
                        eventKind: UITickEventKind.PlayerActionResolved,
                        actorEntityId: 10,
                        actionKind: GameplayUiActionKind.Push,
                        actionSequence: 4,
                        resolutionKind: GameplayUiActionResolutionKind.Success),
                    CreateEvent(
                        tickIndex: 2,
                        eventKind: UITickEventKind.PlayerActionCompleted,
                        actorEntityId: 10,
                        actionKind: GameplayUiActionKind.Push,
                        actionSequence: 4),
                });
            var refresh = mapper.ReduceRefresh(
                firstTick.Snapshot,
                CreateRefreshInput(
                    isPaused: true,
                    canAcceptGameplayCommands: false,
                    activeActionKind: GameplayUiActionKind.None,
                    isRecoveryPhase: false));
            var finalTick = mapper.ReduceTick(
                refresh.Snapshot,
                CreateRefreshInput(
                    tickIndex: 5,
                    shouldUpdateTickIndex: true,
                    activeActionKind: GameplayUiActionKind.None,
                    isRecoveryPhase: false,
                    canMoveThisTick: true,
                    canStartActionThisTick: true),
                new[]
                {
                    CreateEvent(
                        tickIndex: 5,
                        eventKind: UITickEventKind.PlayerDamaged,
                        actorEntityId: 10,
                        damageAmount: 1),
                });

            return (finalTick.Snapshot, finalTick.AppliedEvents);
        }
    }
}
