using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayUiAccessRuntimeTests
    {
        [Test]
        [Category("Extended")]
        public void GameplaySceneHost_Initialize_ExposesUiAccessQueriesWithCommittedHudState()
        {
            var hostObject = new GameObject("GameplaySceneHost_Initialize_ExposesUiAccessQueriesWithCommittedHudState");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                }));

                var session = host.UiAccess.QueryFacade.Session.Read();
                var playerHud = host.UiAccess.QueryFacade.PlayerHud.Read();
                var objectives = host.UiAccess.QueryFacade.Objectives.Read();

                Assert.That(session.NextTickIndex, Is.EqualTo(1));
                Assert.That(session.IsPaused, Is.False);
                Assert.That(session.CanAcceptGameplayCommands, Is.True);
                Assert.That(session.IsStageCleared, Is.False);

                Assert.That(playerHud.IsAvailable, Is.True);
                Assert.That(playerHud.PlayerEntityId, Is.EqualTo(10));
                Assert.That(playerHud.CurrentHp, Is.EqualTo(3));
                Assert.That(playerHud.Facing, Is.EqualTo(GameplayUiDirection.Right));
                Assert.That(playerHud.ActiveActionKind, Is.EqualTo(GameplayUiActionKind.None));
                Assert.That(playerHud.IsActionInRecoveryPhase, Is.False);
                Assert.That(playerHud.CanMoveThisTick, Is.True);
                Assert.That(playerHud.CanStartActionThisTick, Is.True);
                Assert.That(playerHud.RecoveryCooldown.HasValue, Is.False);

                Assert.That(objectives.HasObjective, Is.False);
                Assert.That(objectives.IsCleared, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayPlayerHudQuery_PlayerMissing_PreservesCampaignChances()
        {
            var hostObject = new GameObject("GameplayPlayerHudQuery_PlayerMissing_PreservesCampaignChances");
            var saveKey = CreatePrefsKey(nameof(GameplayPlayerHudQuery_PlayerMissing_PreservesCampaignChances));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);

            try
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                    CurrentLevelGroupId = "level-1",
                    RemainingChances = 2,
                });
                activeSlotProvider.SetActiveSlot(1);

                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(
                    Array.Empty<EntityState>(),
                    campaignChancesReadSource: new SaveSlotCampaignChancesReadSource(
                        saveStore,
                        new CampaignRunningSlotContext(1))));

                var playerHud = host.UiAccess.QueryFacade.PlayerHud.Read();

                Assert.That(playerHud.IsAvailable, Is.False);
                Assert.That(playerHud.HasRemainingChances, Is.True);
                Assert.That(playerHud.RemainingChances, Is.EqualTo(2));
                Assert.That(playerHud.MaxChances, Is.EqualTo(SaveSlotStore.DefaultRemainingChances));
            }
            finally
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayPlayerHudQuery_PlayerAlive_PreservesCampaignChances()
        {
            var hostObject = new GameObject("GameplayPlayerHudQuery_PlayerAlive_PreservesCampaignChances");
            var saveKey = CreatePrefsKey(nameof(GameplayPlayerHudQuery_PlayerAlive_PreservesCampaignChances));
            var activeKey = saveKey + ".active";
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);

            try
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                saveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                    CurrentLevelGroupId = "level-1",
                    RemainingChances = 2,
                });
                activeSlotProvider.SetActiveSlot(1);

                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(
                    new[] { CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right) },
                    campaignChancesReadSource: new SaveSlotCampaignChancesReadSource(
                        saveStore,
                        new CampaignRunningSlotContext(1))));

                var playerHud = host.UiAccess.QueryFacade.PlayerHud.Read();

                Assert.That(playerHud.IsAvailable, Is.True);
                Assert.That(playerHud.HasRemainingChances, Is.True);
                Assert.That(playerHud.RemainingChances, Is.EqualTo(2));
                Assert.That(playerHud.MaxChances, Is.EqualTo(SaveSlotStore.DefaultRemainingChances));
            }
            finally
            {
                saveStore.ClearAll();
                activeSlotProvider.ClearActiveSlot();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayPlayerHudQuery_PlayerMissing_WithoutCampaignChances_RemainsUnavailable()
        {
            var hostObject = new GameObject("GameplayPlayerHudQuery_PlayerMissing_WithoutCampaignChances_RemainsUnavailable");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(Array.Empty<EntityState>()));

                var playerHud = host.UiAccess.QueryFacade.PlayerHud.Read();

                Assert.That(playerHud.IsAvailable, Is.False);
                Assert.That(playerHud.HasRemainingChances, Is.False);
                Assert.That(playerHud.RemainingChances, Is.EqualTo(0));
                Assert.That(playerHud.MaxChances, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayHostObjectiveQuery_DoesNotExposeAuthoringLabel()
        {
            var hostObject = new GameObject("GameplayHostObjectiveQuery_DoesNotExposeAuthoringLabel");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(
                    new[] { CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right) },
                    CreateUiObjectiveDefinition()));

                var objective = host.UiAccess.QueryFacade.Objectives.Read();

                Assert.That(objective.HasObjective, Is.True);
                Assert.That(
                    typeof(GameplayObjectiveConditionReadModel).GetProperty("TitleText"),
                    Is.Null);
                Assert.That(
                    typeof(GameplayObjectiveConditionReadModel).GetProperty("AuthoringLabel"),
                    Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayHostObjectiveQuery_MapsConditionRows()
        {
            var hostObject = new GameObject("GameplayHostObjectiveQuery_MapsConditionRows");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(
                    new[] { CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right) },
                    CreateUiObjectiveDefinition()));

                var objective = host.UiAccess.QueryFacade.Objectives.Read();

                Assert.That(objective.Conditions.Count, Is.EqualTo(1));
                Assert.That(objective.Conditions[0].StableId, Is.EqualTo("primary-goal"));
                Assert.That(objective.Conditions[0].Role, Is.EqualTo(GameplayObjectiveConditionRole.PrimaryGoal));
                Assert.That(objective.Conditions[0].Required, Is.True);
                Assert.That(
                    objective.Conditions[0].PresentationKind,
                    Is.EqualTo(GameplayObjectivePresentationKind.ReachExit));
                Assert.That(
                    objective.Conditions[0].StableGroupKey,
                    Is.EqualTo("reach-exit|role-1"));
                Assert.That(objective.Conditions[0].CompletedCount, Is.EqualTo(0));
                Assert.That(objective.Conditions[0].RequiredCount, Is.EqualTo(1));
                Assert.That(objective.Conditions[0].SortOrder, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void ObjectiveConditionSortOrder_GameplayHostObjectiveQuery_SortsByValueThenAuthoringOrder()
        {
            var hostObject = new GameObject("ObjectiveConditionSortOrder_GameplayHostObjectiveQuery");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(
                    new[]
                    {
                        CreatePlayerEntity(
                            new SurfaceCell(FaceId.Floor, 0, 0),
                            facing: Direction.Right),
                    },
                    CreateSortOrderObjectiveDefinition()));

                var objective = host.UiAccess.QueryFacade.Objectives.Read();

                Assert.That(objective.Conditions.Count, Is.EqualTo(3));
                Assert.That(
                    objective.Conditions.Select(condition => condition.StableId),
                    Is.EqualTo(new[] { "tie-earlier", "tie-later", "late" }));
                Assert.That(
                    objective.Conditions.Select(condition => condition.SortOrder),
                    Is.EqualTo(new[] { 10, 10, 20 }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayHostObjectiveQuery_DoesNotExposeRawDetailsOrAuthoringLabel()
        {
            var hostObject = new GameObject("GameplayHostObjectiveQuery_DoesNotExposeRawDetailsAsAuthoringLabel");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(
                    new[] { CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right) },
                    CreateUiObjectiveDefinition()));

                var objective = host.UiAccess.QueryFacade.Objectives.Read();

                Assert.That(objective.Conditions.Count, Is.EqualTo(1));
                var stringProperties = typeof(GameplayObjectiveConditionReadModel)
                    .GetProperties()
                    .Where(property => property.PropertyType == typeof(string))
                    .Select(property => property.Name)
                    .ToArray();
                Assert.That(
                    stringProperties,
                    Is.EquivalentTo(new[] { "StableId", "StableGroupKey" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayHostObjectiveQuery_DisabledObjective_ReturnsNoObjective()
        {
            var hostObject = new GameObject("GameplayHostObjectiveQuery_DisabledObjective_ReturnsNoObjective");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(
                    new[] { CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right) },
                    StageObjectiveRuntimeDefinition.Disabled));

                var objective = host.UiAccess.QueryFacade.Objectives.Read();

                Assert.That(objective.HasObjective, Is.False);
                Assert.That(objective.Conditions, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiAccess_SameWindowQueriesReuseCommittedPlayerFact_WhilePauseRemainsLiveGate()
        {
            var hostObject = new GameObject("GameplayUiAccess_SameWindowQueriesReuseCommittedPlayerFact_WhilePauseRemainsLiveGate");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                }));

                var freshSnapshot = GameplayCompositionRoot.CreateSnapshot(host.WorldState);
                Assert.That(freshSnapshot.TryGetEntity(10, out var freshPlayer), Is.True);

                var session = host.UiAccess.QueryFacade.Session.Read();
                var playerHud = host.UiAccess.QueryFacade.PlayerHud.Read();
                var moveAcceptance = host.UiAccess.CommandGateway.SetHeldMoveDirection(GameplayUiDirection.Right);

                host.UiAccess.PauseService.Pause();

                var pausedSession = host.UiAccess.QueryFacade.Session.Read();
                var pausedHud = host.UiAccess.QueryFacade.PlayerHud.Read();
                var pausedMove = host.UiAccess.CommandGateway.SetHeldMoveDirection(GameplayUiDirection.Up);

                Assert.That(session.CanAcceptGameplayCommands, Is.True);
                Assert.That(playerHud.IsAvailable, Is.True);
                Assert.That(playerHud.PlayerEntityId, Is.EqualTo(freshPlayer.entityId));
                Assert.That(playerHud.CurrentHp, Is.EqualTo(freshPlayer.hp));
                Assert.That(playerHud.Facing, Is.EqualTo(GameplayUiDirection.Right));
                Assert.That(moveAcceptance.Accepted, Is.True);

                Assert.That(pausedSession.IsPaused, Is.True);
                Assert.That(pausedSession.CanAcceptGameplayCommands, Is.False);
                Assert.That(pausedHud.IsAvailable, Is.True);
                Assert.That(pausedHud.PlayerEntityId, Is.EqualTo(freshPlayer.entityId));
                Assert.That(pausedHud.CurrentHp, Is.EqualTo(freshPlayer.hp));
                Assert.That(pausedHud.Facing, Is.EqualTo(GameplayUiDirection.Right));
                Assert.That(pausedHud.CanMoveThisTick, Is.False);
                Assert.That(pausedHud.CanStartActionThisTick, Is.False);
                Assert.That(pausedHud.CanStartAnyActionThisTick, Is.False);
                Assert.That(pausedMove.Accepted, Is.False);
                Assert.That(pausedMove.RejectionReason, Is.EqualTo(GameplayCommandRejectionReason.Paused));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiAccess_PreRefreshTransientQueries_ReadPreviousCommittedHudState_BeforeTickCompletedRefresh()
        {
            var hostObject = new GameObject("GameplayUiAccess_PreRefreshTransientQueries_ReadPreviousCommittedHudState_BeforeTickCompletedRefresh");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(
                    new[]
                    {
                        CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 1), facing: Direction.Right),
                    }));
                SetPlayerContinuousLocalOffset(
                    host.WorldState,
                    localX: 0,
                    localY: SimulationFixed.MaxPositiveLocalOffset,
                    speedUnitsPerTick: DefaultFree2DSpeedUnitsPerTick());

                var beforeTickHud = host.UiAccess.QueryFacade.PlayerHud.Read();
                var observedPresentWindow = false;
                var transientSession = default(GameplaySessionReadModel);
                var transientHud = default(GameplayPlayerHudReadModel);

                host.UiAccess.PresentationFeed.StateChanged += _ =>
                {
                    if (observedPresentWindow)
                    {
                        return;
                    }

                    observedPresentWindow = true;
                    transientSession = host.UiAccess.QueryFacade.Session.Read();
                    transientHud = host.UiAccess.QueryFacade.PlayerHud.Read();
                };

                Assert.That(host.UiAccess.CommandGateway.SetHeldMoveDirection(GameplayUiDirection.Up).Accepted, Is.True);
                var tickResult = host.InputHost.RunSingleTick();

                var refreshedHud = host.UiAccess.QueryFacade.PlayerHud.Read();

                Assert.That(tickResult, Is.Not.Null);
                Assert.That(tickResult.PresentationData.TopologyMotion.HasValue, Is.True);
                Assert.That(observedPresentWindow, Is.True);
                Assert.That(transientSession.NextTickIndex, Is.EqualTo(2));
                Assert.That(transientHud.IsAvailable, Is.True);
                Assert.That(transientHud.PlayerEntityId, Is.EqualTo(beforeTickHud.PlayerEntityId));
                Assert.That(transientHud.CurrentHp, Is.EqualTo(beforeTickHud.CurrentHp));
                Assert.That(transientHud.Facing, Is.EqualTo(beforeTickHud.Facing));
                Assert.That(refreshedHud.Facing, Is.EqualTo(GameplayUiDirection.Up));
                Assert.That(refreshedHud.Facing, Is.Not.EqualTo(transientHud.Facing));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiAccess_PauseRejectsActionableCommands_AllowsClear_AndClearsPendingUiInput()
        {
            var hostObject = new GameObject("GameplayUiAccess_PauseRejectsActionableCommands_AllowsClear_AndClearsPendingUiInput");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                }));

                var commandGateway = host.UiAccess.CommandGateway;
                var pauseService = host.UiAccess.PauseService;

                Assert.That(commandGateway.SetHeldMoveDirection(GameplayUiDirection.Right).Accepted, Is.True);

                pauseService.Pause();

                var rejectedMove = commandGateway.SetHeldMoveDirection(GameplayUiDirection.Up);
                var clearAcceptance = commandGateway.ClearHeldMoveDirection();
                var pausedSession = host.UiAccess.QueryFacade.Session.Read();

                Assert.That(rejectedMove.Accepted, Is.False);
                Assert.That(rejectedMove.RejectionReason, Is.EqualTo(GameplayCommandRejectionReason.Paused));
                Assert.That(clearAcceptance.Accepted, Is.True);
                Assert.That(pausedSession.IsPaused, Is.True);
                Assert.That(pausedSession.CanAcceptGameplayCommands, Is.False);
                Assert.That(host.InputHost.RunSingleTick(), Is.Null);
                Assert.That(host.Presenter.IsPresentationPaused, Is.True);

                pauseService.Resume();

                var resumedTick = host.InputHost.RunSingleTick();
                var snapshotAfter = GameplayCompositionRoot.CreateSnapshot(host.WorldState);

                Assert.That(resumedTick, Is.Not.Null);
                Assert.That(host.Presenter.IsPresentationPaused, Is.False);
                Assert.That(snapshotAfter.TryGetEntity(10, out var player), Is.True);
                Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Core")]
        public void PauseService_PauseTwiceResumeOnce_RemainsSimulationAndPresentationPaused()
        {
            var hostObject = new GameObject(nameof(PauseService_PauseTwiceResumeOnce_RemainsSimulationAndPresentationPaused));

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                }));

                host.UiAccess.PauseService.Pause();
                host.UiAccess.PauseService.Pause();
                host.UiAccess.PauseService.Resume();

                Assert.That(host.UiAccess.PauseService.IsPaused, Is.True);
                Assert.That(host.InputHost.IsSimulationPaused, Is.True);
                Assert.That(host.Presenter.IsPresentationPaused, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Core")]
        public void PauseService_PauseOnceResumeTwice_DoesNotErroneouslyResumeOrUnderflow()
        {
            var hostObject = new GameObject(nameof(PauseService_PauseOnceResumeTwice_DoesNotErroneouslyResumeOrUnderflow));

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                }));
                var pauseService = (Game.Feature.Gameplay.Host.UIAccess.GameplayHostPauseService)host.UiAccess.PauseService;

                pauseService.Pause();
                pauseService.Resume();
                pauseService.Resume();

                Assert.That(pauseService.IsPaused, Is.False);
                Assert.That(pauseService.PauseDepth, Is.Zero);
                Assert.That(host.InputHost.IsSimulationPaused, Is.False);
                Assert.That(host.Presenter.IsPresentationPaused, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Core")]
        public void PauseService_OnlyZeroDepthResumesPresenterAndVfx()
        {
            var hostObject = new GameObject(nameof(PauseService_OnlyZeroDepthResumesPresenterAndVfx));

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                }));

                host.UiAccess.PauseService.Pause();
                host.UiAccess.PauseService.Pause();
                host.UiAccess.PauseService.Resume();
                Assert.That(host.Presenter.IsPresentationPaused, Is.True);

                host.UiAccess.PauseService.Resume();
                Assert.That(host.Presenter.IsPresentationPaused, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Core")]
        public void PauseService_RepeatedPauseResume_BalancedDepthTransitionsOnlyApplyOnEdges()
        {
            var hostObject = new GameObject(nameof(PauseService_RepeatedPauseResume_BalancedDepthTransitionsOnlyApplyOnEdges));

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                }));
                var changedStates = new List<bool>();
                host.UiAccess.PauseService.PauseChanged += changedStates.Add;

                host.UiAccess.PauseService.Pause();
                host.UiAccess.PauseService.Pause();
                host.UiAccess.PauseService.Resume();
                host.UiAccess.PauseService.Resume();

                Assert.That(changedStates, Is.EqualTo(new[] { true, false }));
                Assert.That(host.UiAccess.PauseService.IsPaused, Is.False);
                Assert.That(host.InputHost.IsSimulationPaused, Is.False);
                Assert.That(host.Presenter.IsPresentationPaused, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiAccess_PresentationFeed_RotationTickPublishesTopologySliceAndStateChanges()
        {
            var hostObject = new GameObject("GameplayUiAccess_PresentationFeed_RotationTickPublishesTopologySliceAndStateChanges");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 1), facing: Direction.Up),
                }));
                SetPlayerContinuousLocalOffset(
                    host.WorldState,
                    localX: 0,
                    localY: SimulationFixed.MaxPositiveLocalOffset,
                    speedUnitsPerTick: DefaultFree2DSpeedUnitsPerTick());

                var frames = new List<GameplayPresentationFrame>();
                var states = new List<GameplayPresentationState>();
                host.UiAccess.PresentationFeed.FramePublished += frames.Add;
                host.UiAccess.PresentationFeed.StateChanged += states.Add;

                var acceptance = host.UiAccess.CommandGateway.SetHeldMoveDirection(GameplayUiDirection.Up);
                var tickResult = host.InputHost.RunSingleTick();

                Assert.That(acceptance.Accepted, Is.True);
                Assert.That(tickResult, Is.Not.Null);
                Assert.That(tickResult.PresentationData.TopologyMotion.HasValue, Is.True);
                Assert.That(frames.Count, Is.EqualTo(1));
                Assert.That(frames[0].Topology.HasValue, Is.True);
                Assert.That(frames[0].Topology.Value.RotationKind, Is.EqualTo(GameplayUiRotationKind.Forward));
                Assert.That(frames[0].Topology.Value.DestinationTopology.BottomFace, Is.EqualTo(GameplayUiFace.Front));
                Assert.That(frames[0].FinalTopology.BottomFace, Is.EqualTo(GameplayUiFace.Front));
                Assert.That(host.UiAccess.PresentationFeed.CurrentState.IsTopologyTransitionActive, Is.True);
                Assert.That(states.Count, Is.GreaterThanOrEqualTo(1));

                host.Presenter.UpdatePresentation(host.TimingProfile.TopologyMotionDurationSeconds);

                Assert.That(host.UiAccess.PresentationFeed.CurrentState.IsTopologyTransitionActive, Is.False);
                Assert.That(states.Count, Is.GreaterThanOrEqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiAccess_PresentationFeed_MapsPlayerSlice_ForHeldMove()
        {
            var hostObject = new GameObject("GameplayUiAccess_PresentationFeed_MapsPlayerSlice_ForHeldMove");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Up),
                },
                boardBounds: new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0))));

                GameplayPresentationFrame capturedFrame = default;
                var frameCount = 0;
                host.UiAccess.PresentationFeed.FramePublished += frame =>
                {
                    capturedFrame = frame;
                    frameCount++;
                };

                var acceptance = host.UiAccess.CommandGateway.SetHeldMoveDirection(GameplayUiDirection.Right);
                var tickResult = host.InputHost.RunSingleTick();

                Assert.That(acceptance.Accepted, Is.True);
                Assert.That(tickResult, Is.Not.Null);
                Assert.That(tickResult.PresentationData.PlayerLocomotionSignals.Count, Is.EqualTo(1));
                Assert.That(tickResult.PresentationData.PlayerLocomotionSignals[0].ShouldPlayWalkLoop, Is.True);
                Assert.That(tickResult.PresentationData.PlayerLocomotionSignals[0].MoveMotionGeneratedThisTick, Is.False);
                Assert.That(frameCount, Is.EqualTo(1));
                Assert.That(capturedFrame.Player.HasValue, Is.True);
                Assert.That(capturedFrame.Player.Value.PlayerEntityId, Is.EqualTo(10));
                Assert.That(capturedFrame.Player.Value.ActiveActionKind, Is.EqualTo(GameplayUiActionKind.None));
                Assert.That(capturedFrame.Player.Value.ActiveActionSequence, Is.EqualTo(0));
                Assert.That(capturedFrame.Player.Value.ShouldPlayWalkLoop, Is.True);
                Assert.That(capturedFrame.Player.Value.MoveMotionGeneratedThisTick, Is.False);
                Assert.That(capturedFrame.Player.Value.ActionDirection, Is.EqualTo(GameplayUiDirection.None));
                Assert.That(capturedFrame.Player.Value.TargetEntityId, Is.EqualTo(0));
                Assert.That(capturedFrame.Player.Value.StartedThisTick, Is.False);
                Assert.That(capturedFrame.Player.Value.ExecutedThisTick, Is.False);
                Assert.That(capturedFrame.Player.Value.IsRecoveryPhase, Is.False);
                Assert.That(capturedFrame.Player.Value.ResolutionKind, Is.EqualTo(GameplayUiActionResolutionKind.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiAccess_PlayerHud_ExposesRecoveryCooldown_AsRecoveryOnlySemantic()
        {
            var hostObject = new GameObject("GameplayUiAccess_PlayerHud_ExposesRecoveryCooldown_AsRecoveryOnlySemantic");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(
                    new[]
                    {
                        CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                        CreateBoxEntity(new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push),
                    },
                    boardBounds: new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0)),
                    playerControlTiming: CreateRecoveryTimingSettings(
                        pushExecuteDelayTicks: 1,
                        pushInputLockDurationTicks: 3)));

                host.InputHost.SetRawMoveInput(Vector2.right);
                host.InputHost.BufferPush();

                var startTick = host.InputHost.RunSingleTick();
                var startHud = host.UiAccess.QueryFacade.PlayerHud.Read();
                var executeTick = host.InputHost.RunSingleTick();
                var fullRecoveryHud = host.UiAccess.QueryFacade.PlayerHud.Read();
                var lastRecoveryTick = host.InputHost.RunSingleTick();
                var lastRecoveryHud = host.UiAccess.QueryFacade.PlayerHud.Read();
                var postFinalRecoveryTick = host.InputHost.RunSingleTick();
                var postFinalRecoveryHud = host.UiAccess.QueryFacade.PlayerHud.Read();
                var clearTick = host.InputHost.RunSingleTick();
                var clearedHud = host.UiAccess.QueryFacade.PlayerHud.Read();

                Assert.That(startTick, Is.Not.Null);
                Assert.That(startHud.ActiveActionKind, Is.EqualTo(GameplayUiActionKind.Push));
                Assert.That(startHud.IsActionInRecoveryPhase, Is.False);
                Assert.That(startHud.RecoveryCooldown.HasValue, Is.False);

                Assert.That(executeTick, Is.Not.Null);
                Assert.That(fullRecoveryHud.IsActionInRecoveryPhase, Is.True);
                Assert.That(fullRecoveryHud.RecoveryCooldown.HasValue, Is.True);
                Assert.That(fullRecoveryHud.RecoveryCooldown.Value.ActionKind, Is.EqualTo(GameplayUiActionKind.Push));
                Assert.That(fullRecoveryHud.RecoveryCooldown.Value.TotalRecoveryTicks, Is.EqualTo(2));
                Assert.That(fullRecoveryHud.RecoveryCooldown.Value.RemainingRecoveryTicks, Is.EqualTo(2));

                Assert.That(lastRecoveryTick, Is.Not.Null);
                Assert.That(lastRecoveryHud.IsActionInRecoveryPhase, Is.True);
                Assert.That(lastRecoveryHud.RecoveryCooldown.HasValue, Is.True);
                Assert.That(lastRecoveryHud.RecoveryCooldown.Value.RemainingRecoveryTicks, Is.EqualTo(1));

                Assert.That(postFinalRecoveryTick, Is.Not.Null);
                Assert.That(postFinalRecoveryHud.IsActionInRecoveryPhase, Is.True);
                Assert.That(postFinalRecoveryHud.RecoveryCooldown.HasValue, Is.False);

                Assert.That(clearTick, Is.Not.Null);
                Assert.That(clearedHud.ActiveActionKind, Is.EqualTo(GameplayUiActionKind.None));
                Assert.That(clearedHud.IsActionInRecoveryPhase, Is.False);
                Assert.That(clearedHud.RecoveryCooldown.HasValue, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiAccess_PlayerHud_PushReadiness_NoCandidate_IsReadyButNotArmed()
        {
            var hostObject = new GameObject("GameplayUiAccess_PlayerHud_PushReadiness_NoCandidate_IsReadyButNotArmed");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(
                    new[]
                    {
                        CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                    },
                    boardBounds: new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0))));

                Assert.That(host.UiAccess.CommandGateway.SetHeldMoveDirection(GameplayUiDirection.Right).Accepted, Is.True);

                var playerHud = host.UiAccess.QueryFacade.PlayerHud.Read();
                var snapshot = GameplayCompositionRoot.CreateSnapshot(host.WorldState);
                snapshot.TryGetPlayerControlState(10, out var playerControlState);

                Assert.That(playerHud.IsAvailable, Is.True);
                Assert.That(
                    PlayerControlQueries.CanStartExplicitAction(
                        playerControlState,
                        host.TickRunner.NextTickIndex),
                    Is.True);
                Assert.That(playerHud.CanStartAnyActionThisTick, Is.True);
                Assert.That(playerHud.CanStartActionThisTick, Is.True);
                Assert.That(playerHud.HasExplicitPushCandidateInCurrentDirection, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiAccess_PlayerHud_PushReadiness_WithCandidate_IsArmed()
        {
            var hostObject = new GameObject("GameplayUiAccess_PlayerHud_PushReadiness_WithCandidate_IsArmed");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(
                    new[]
                    {
                        CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                        CreateBoxEntity(new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push),
                    },
                    boardBounds: new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0))));

                Assert.That(host.UiAccess.CommandGateway.SetHeldMoveDirection(GameplayUiDirection.Right).Accepted, Is.True);

                var playerHud = host.UiAccess.QueryFacade.PlayerHud.Read();

                Assert.That(playerHud.IsAvailable, Is.True);
                Assert.That(playerHud.CanStartAnyActionThisTick, Is.True);
                Assert.That(playerHud.HasExplicitPushCandidateInCurrentDirection, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayUiAccess_PlayerHud_PushReadiness_CrossFaceCandidate_IsNotArmed()
        {
            var hostObject = new GameObject("GameplayUiAccess_PlayerHud_PushReadiness_CrossFaceCandidate_IsNotArmed");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(
                    new[]
                    {
                        CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 1), facing: Direction.Up),
                        CreateBoxEntity(new SurfaceCell(FaceId.Front, 0, 0), BoxCapabilities.Push),
                    },
                    boardBounds: new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 1))));

                Assert.That(host.UiAccess.CommandGateway.SetHeldMoveDirection(GameplayUiDirection.Up).Accepted, Is.True);

                var playerHud = host.UiAccess.QueryFacade.PlayerHud.Read();

                Assert.That(playerHud.IsAvailable, Is.True);
                Assert.That(playerHud.CanStartAnyActionThisTick, Is.True);
                Assert.That(playerHud.HasExplicitPushCandidateInCurrentDirection, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiAccess_PlayerHud_PushReadiness_ActionLock_DisablesPush()
        {
            var hostObject = new GameObject("GameplayUiAccess_PlayerHud_PushReadiness_ActionLock_DisablesPush");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(
                    new[]
                    {
                        CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                        CreateBoxEntity(new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push),
                    },
                    boardBounds: new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0)),
                    playerControlTiming: CreateRecoveryTimingSettings(
                        pushExecuteDelayTicks: 1,
                        pushInputLockDurationTicks: 3)));

                host.InputHost.SetRawMoveInput(Vector2.right);
                host.InputHost.BufferPush();

                var startTick = host.InputHost.RunSingleTick();
                var executeTick = host.InputHost.RunSingleTick();
                var playerHud = host.UiAccess.QueryFacade.PlayerHud.Read();
                var snapshot = GameplayCompositionRoot.CreateSnapshot(host.WorldState);

                Assert.That(startTick, Is.Not.Null);
                Assert.That(executeTick, Is.Not.Null);
                Assert.That(snapshot.TryGetPlayerControlState(10, out var playerControlState), Is.True);
                Assert.That(
                    PlayerControlQueries.CanStartExplicitAction(
                        playerControlState,
                        host.TickRunner.NextTickIndex),
                    Is.False);
                Assert.That(playerHud.ActiveActionKind, Is.EqualTo(GameplayUiActionKind.Push));
                Assert.That(playerHud.IsActionInRecoveryPhase, Is.True);
                Assert.That(playerHud.CanStartAnyActionThisTick, Is.False);
                Assert.That(playerHud.HasExplicitPushCandidateInCurrentDirection, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplaySurfaceButtonRemainderQuery_CountsUnactivatedButtonsByFaceAndSelector()
        {
            var hostObject = new GameObject("GameplaySurfaceButtonRemainderQuery_CountsUnactivatedButtonsByFaceAndSelector");
            var normalFloor = CreateTileFeature(101, new SurfaceCell(FaceId.Floor, 0, 0), TileFeatureKind.Button, TileFeatureFlags.None);
            var moonFloor = CreateTileFeature(102, new SurfaceCell(FaceId.Floor, 1, 0), TileFeatureKind.Button, TileFeatureFlags.None);
            var activatedFront = CreateTileFeature(103, new SurfaceCell(FaceId.Front, 0, 0), TileFeatureKind.Button, TileFeatureFlags.Activated);
            var normalBack = CreateTileFeature(104, new SurfaceCell(FaceId.Back, 0, 0), TileFeatureKind.Button, TileFeatureFlags.None);
            var nonButton = CreateTileFeature(105, new SurfaceCell(FaceId.Ceiling, 0, 0), TileFeatureKind.Slide, TileFeatureFlags.None);

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(
                    new[] { CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0)) },
                    initialTileFeatures: new[]
                    {
                        normalFloor,
                        moonFloor,
                        activatedFront,
                        normalBack,
                        nonButton,
                    },
                    tileFeatureDefinitions: new[]
                    {
                        CreateTileFeatureDefinition(normalFloor.TileId, TileFeatureBoxSelector.AnyPushableBox),
                        CreateTileFeatureDefinition(moonFloor.TileId, TileFeatureBoxSelector.MoonBlockOnly),
                        CreateTileFeatureDefinition(activatedFront.TileId, TileFeatureBoxSelector.MoonBlockOnly),
                        CreateTileFeatureDefinition(normalBack.TileId, TileFeatureBoxSelector.FeatureCell),
                        CreateTileFeatureDefinition(nonButton.TileId, TileFeatureBoxSelector.None),
                    }));

                var remainders = host.UiAccess.QueryFacade.SurfaceButtonRemainders.Read();

                Assert.That(remainders.Count, Is.EqualTo(Enum.GetValues(typeof(GameplayUiFace)).Length));
                Assert.That(remainders[(int)GameplayUiFace.Floor].NormalRemaining, Is.EqualTo(1));
                Assert.That(remainders[(int)GameplayUiFace.Floor].MoonBlockOnlyRemaining, Is.EqualTo(1));
                Assert.That(remainders[(int)GameplayUiFace.Front].TotalRemaining, Is.EqualTo(0));
                Assert.That(remainders[(int)GameplayUiFace.Ceiling].TotalRemaining, Is.EqualTo(0));
                Assert.That(remainders[(int)GameplayUiFace.Back].NormalRemaining, Is.EqualTo(1));
                Assert.That(remainders[(int)GameplayUiFace.Back].MoonBlockOnlyRemaining, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplaySurfaceButtonRemainderQuery_MissingButtonDefinitionThrows()
        {
            var hostObject = new GameObject("GameplaySurfaceButtonRemainderQuery_MissingButtonDefinitionThrows");
            var button = CreateTileFeature(201, new SurfaceCell(FaceId.Floor, 0, 0), TileFeatureKind.Button, TileFeatureFlags.None);

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(
                    new[] { CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0)) },
                    initialTileFeatures: new[] { button },
                    tileFeatureDefinitions: Array.Empty<TileFeatureRuntimeDefinition>()));

                Assert.Throws<InvalidOperationException>(() => host.UiAccess.QueryFacade.SurfaceButtonRemainders.Read());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        private static GameplaySceneHostConfiguration CreateConfiguration(
            EntityState[] initialEntities,
            StageObjectiveRuntimeDefinition objectiveDefinition = null,
            BoardBounds? boardBounds = null,
            PlayerControlTimingSettings playerControlTiming = null,
            ICampaignChancesReadSource campaignChancesReadSource = null,
            TileFeatureState[] initialTileFeatures = null,
            TileFeatureRuntimeDefinition[] tileFeatureDefinitions = null)
        {
            var configuration = new GameplaySceneHostConfiguration
            {
                AutoAdvanceTicks = false,
                AutoCreateViews = false,
                InitialBoardBounds = boardBounds ?? new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                InitialEntities = initialEntities,
                InitialTileFeatures = initialTileFeatures ?? Array.Empty<TileFeatureState>(),
                TileFeatureDefinitions = tileFeatureDefinitions ?? Array.Empty<TileFeatureRuntimeDefinition>(),
                InitialTopology = new CubeTopologyState(FaceId.Floor),
                ObjectiveRuntimeDefinition = objectiveDefinition ?? StageObjectiveRuntimeDefinition.Disabled,
                PlayerEntityId = 10,
                PlayerControlTiming = playerControlTiming ?? PlayerControlTimingSettings.CreateDefault(),
                CampaignChancesReadSource = campaignChancesReadSource,
            };
            configuration.ApplyRuntimeFeatureFlags(GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);
            return configuration;
        }

        private static TileFeatureState CreateTileFeature(
            int tileId,
            SurfaceCell cell,
            TileFeatureKind kind,
            TileFeatureFlags flags)
        {
            return new TileFeatureState(
                tileId,
                cell,
                kind,
                flags,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0,
                lifetimeTicks: 0,
                charges: 0);
        }

        private static TileFeatureRuntimeDefinition CreateTileFeatureDefinition(
            int tileId,
            TileFeatureBoxSelector boxSelector)
        {
            return new TileFeatureRuntimeDefinition(
                tileId,
                TileFeatureActivationRule.Always,
                Direction2D.None,
                boxSelector,
                boundEntityId: 0);
        }

        private static StageObjectiveRuntimeDefinition CreateUiObjectiveDefinition()
        {
            var goalZone = new StageZoneRuntimeDefinition(
                "goal",
                FaceId.Floor,
                new[]
                {
                    new StageZoneRuntimeRegion(
                        new Vector2Int(0, 0),
                        new Vector2Int(0, 0)),
                });
            var condition = new PlayerAtAnyZoneConditionRuntimeDefinition(
                "player-at-goal",
                "Debug Player At Goal",
                10,
                new[] { goalZone },
                requireAlive: true);
            var runtimeEntry = new StageObjectiveConditionRuntimeDefinitionEntry(
                condition: condition,
                required: true,
                role: StageObjectiveConditionRole.PrimaryGoal,
                stableConditionId: "primary-goal",
                presentationId: StageObjectiveConditionPresentationIds.ReachExit,
                stableGroupKey: "reach-exit|role-1",
                sortOrder: 0,
                authoringOrder: 0);

            return new StageObjectiveRuntimeDefinition(
                StageCompletionPolicy.RequireAllConditions,
                playerEntityId: 10,
                new[] { goalZone },
                new[] { runtimeEntry });
        }

        private static StageObjectiveRuntimeDefinition CreateSortOrderObjectiveDefinition()
        {
            var goalZone = new StageZoneRuntimeDefinition(
                "goal",
                FaceId.Floor,
                new[]
                {
                    new StageZoneRuntimeRegion(
                        new Vector2Int(0, 0),
                        new Vector2Int(0, 0)),
                });
            var entries = new[]
            {
                CreateSortOrderRuntimeEntry("late", goalZone, sortOrder: 20, authoringOrder: 0),
                CreateSortOrderRuntimeEntry("tie-later", goalZone, sortOrder: 10, authoringOrder: 2),
                CreateSortOrderRuntimeEntry("tie-earlier", goalZone, sortOrder: 10, authoringOrder: 1),
            };

            return new StageObjectiveRuntimeDefinition(
                StageCompletionPolicy.RequireAllConditions,
                playerEntityId: 10,
                new[] { goalZone },
                entries);
        }

        private static StageObjectiveConditionRuntimeDefinitionEntry CreateSortOrderRuntimeEntry(
            string stableConditionId,
            StageZoneRuntimeDefinition goalZone,
            int sortOrder,
            int authoringOrder)
        {
            return new StageObjectiveConditionRuntimeDefinitionEntry(
                new PlayerAtAnyZoneConditionRuntimeDefinition(
                    stableConditionId,
                    stableConditionId,
                    playerEntityId: 10,
                    new[] { goalZone },
                    requireAlive: true),
                required: true,
                role: StageObjectiveConditionRole.SecondaryGoal,
                stableConditionId,
                StageObjectiveConditionPresentationIds.ReachZone,
                "reach-zone|role-2",
                sortOrder,
                authoringOrder);
        }

        private static PlayerControlTimingSettings CreateRecoveryTimingSettings(
            int pushExecuteDelayTicks,
            int pushInputLockDurationTicks)
        {
            var ticksPerSecond = GameplayTimingProfile.DefaultSimulationTicksPerSecond;
            return new PlayerControlTimingSettings
            {
                PushExecuteDelaySeconds = pushExecuteDelayTicks / (float)ticksPerSecond,
                PushInputLockDurationSeconds = pushInputLockDurationTicks / (float)ticksPerSecond,
            };
        }

        private static EntityState CreatePlayerEntity(
            SurfaceCell position,
            Direction facing = Direction.Up)
        {
            return new EntityState
            {
                entityId = 10,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                state = EntityPhaseState.Idle,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.None,
            };
        }

        private static EntityState CreateBoxEntity(
            SurfaceCell position,
            BoxCapabilities capabilities)
        {
            return new EntityState
            {
                entityId = 20,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                unitRole = UnitRole.None,
                state = EntityPhaseState.Idle,
                facing = Direction.Up,
                boardPresence = EntityBoardPresence.Occupying,
                boxCapabilities = capabilities,
                aiMode = EnemyAiMode.None,
            };
        }

        private static void SetPlayerContinuousLocalOffset(
            WorldState worldState,
            int localX,
            int localY,
            int speedUnitsPerTick)
        {
            worldState.CreateWriteContext().SetUnitContinuousLocomotionState(
                10,
                new UnitContinuousLocomotionState
                {
                    localOffset = new SimulationOffset2(
                        SimulationFixed.FromRaw(localX),
                        SimulationFixed.FromRaw(localY)),
                    velocity = SimulationVelocity2.Zero,
                    facing = Direction.Up,
                    lastMoveDirection = Direction.Up,
                    speedUnitsPerTick = speedUnitsPerTick,
                    mode = ContinuousLocomotionMode.Idle,
                    sequenceId = 1,
                }.NormalizedForStorage());
        }

        private static int DefaultFree2DSpeedUnitsPerTick()
        {
            return PlayerContinuousLocomotionSettings.CreateDefault()
                .CreateAuthoritativeSnapshot(GameplayTimingProfile.DefaultSimulationTicksPerSecond)
                .SpeedUnitsPerTick;
        }

        private static string CreatePrefsKey(string suffix)
        {
            return "Game.Feature.Tests." + suffix + "." + Guid.NewGuid().ToString("N");
        }
    }
}
