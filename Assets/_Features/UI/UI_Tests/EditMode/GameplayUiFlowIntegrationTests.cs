using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.HUD;
using Game.Feature.UI.Popups;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Feature.UI.Tests
{
    public sealed class GameplayUiFlowIntegrationTests
    {
        [Test]
        [Category("Extended")]
        public void GameplayUiFlowInstaller_PreservesHudReadOnlySeam_ThroughScreenAndPausePopup()
        {
            var hostObject = new GameObject("GameplayUiFlowInstaller_PreservesHudReadOnlySeam_ThroughScreenAndPausePopup");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 1), Direction.Up),
                }));

                var installer = hostObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(host);

                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
                Assert.That(installer.HudView.IsVisible, Is.True);
                Assert.That(installer.HudController.ActionBarViewModel.IsInteractive, Is.True);

                installer.GameplayScreenView.ClickHelp();
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Help));
                Assert.That(installer.HudController.ActionBarViewModel.IsInteractive, Is.False);
                Assert.That(installer.HelpScreenView.IsVisible, Is.True);

                installer.HelpScreenView.ClickBack();
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
                Assert.That(installer.HudController.ActionBarViewModel.IsInteractive, Is.True);

                installer.HudView.ActionBarView.ClickSlot(HudActionSlotId.Primary);
                Assert.That(installer.HudController.ActionBarViewModel.LastCommandResult.HasValue, Is.True);
                Assert.That(installer.HudController.ActionBarViewModel.LastCommandResult.Value.Accepted, Is.True);

                host.InputHost.RunSingleTick();
                Assert.That(host.UiAccess.PresentationFeed.CurrentState.HasBlockingPresentation, Is.True);
                Assert.That(installer.HudController.ActionBarViewModel.IsInteractive, Is.False);

                installer.HudView.ClickPause();
                Assert.That(installer.PopupController.Contains(PopupId.Pause), Is.True);
                Assert.That(installer.Ports.PauseService.IsPaused, Is.True);
                Assert.That(installer.PopupLayerView.IsDimVisible, Is.True);

                installer.PausePopupView.ClickResume();
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));
                Assert.That(installer.Ports.PauseService.IsPaused, Is.False);
            }
            finally
            {
                DestroySupportObjects(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiFlowInstaller_ComposesObjectiveInfoTooltipAndRewardPolicies()
        {
            var hostObject = new GameObject("GameplayUiFlowInstaller_ComposesObjectiveInfoTooltipAndRewardPolicies");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 1), Direction.Up),
                }));

                var installer = hostObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(host);

                installer.GameplayScreenView.ClickObjectives();
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.ObjectiveStatus));

                installer.ObjectiveStatusScreenView.ClickInfo();
                Assert.That(installer.PopupController.Contains(PopupId.ObjectiveInfo), Is.True);
                Assert.That(installer.ObjectiveInfoPopupView, Is.Not.Null);
                Assert.That(installer.PopupLayerView.IsDimVisible, Is.False);
                Assert.That(installer.Coordinator.CurrentBlockSnapshot.BlocksScreenInteraction, Is.False);

                var tooltipCompletions = new List<PopupCompletion>();
                Assert.That(installer.Coordinator.RequestTooltipPopup(
                    new TooltipPopupPayload("Tip", "Tooltip body"),
                    tooltipCompletions.Add), Is.True);
                Assert.That(installer.PopupController.TopPopup.Value.PopupId, Is.EqualTo(PopupId.Tooltip));

                installer.ObjectiveStatusScreenView.ClickBack();
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.ObjectiveStatus));
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(1));
                Assert.That(tooltipCompletions, Has.Count.EqualTo(1));
                Assert.That(tooltipCompletions[0].CloseReason, Is.EqualTo(PopupCloseReason.Back));
                Assert.That(installer.PopupController.TopPopup.Value.PopupId, Is.EqualTo(PopupId.ObjectiveInfo));

                Assert.That(installer.Coordinator.HandleBackRequested(), Is.True);
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.ObjectiveStatus));
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));

                installer.ObjectiveStatusScreenView.ClickBack();
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));

                var gameplayTooltipCompletions = new List<PopupCompletion>();
                Assert.That(installer.Coordinator.RequestTooltipPopup(
                    new TooltipPopupPayload("Gameplay Tip", "Tooltip body"),
                    gameplayTooltipCompletions.Add), Is.True);

                installer.GameplayScreenView.ClickHelp();
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Help));
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));
                Assert.That(gameplayTooltipCompletions, Has.Count.EqualTo(1));
                Assert.That(gameplayTooltipCompletions[0].CloseReason, Is.EqualTo(PopupCloseReason.ScreenTransition));

                var rewardCompletions = new List<PopupCompletion>();
                Assert.That(installer.Coordinator.RequestRewardPopup(
                    new RewardPopupPayload(
                        "Reward",
                        new[] { new RewardPopupItemPayload("Crystal", 2) },
                        "Summary",
                        "Claim"),
                    rewardCompletions.Add), Is.True);
                Assert.That(installer.PopupLayerView.IsDimVisible, Is.True);
                Assert.That(installer.Coordinator.HandleBackRequested(), Is.True);
                Assert.That(rewardCompletions, Is.Empty);
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(1));

                installer.RewardPopupView.ClickAcknowledge();
                Assert.That(rewardCompletions, Has.Count.EqualTo(1));
                Assert.That(rewardCompletions[0].CompletionKind, Is.EqualTo(PopupCompletionKind.Acknowledged));
            }
            finally
            {
                DestroySupportObjects(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiFlowInstaller_RunSingleTick_TransitionsStageClearIntoCanonicalStageResultScreen()
        {
            var hostObject = new GameObject("GameplayUiFlowInstaller_RunSingleTick_TransitionsStageClearIntoCanonicalStageResultScreen");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(
                    new[]
                    {
                        CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), Direction.Up),
                    },
                    CreateSingleCellObjective(new SurfaceCell(FaceId.Floor, 0, 0))));

                var installer = hostObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(host);

                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));

                var result = host.InputHost.RunSingleTick();

                Assert.That(result, Is.Not.Null);
                Assert.That(result.ObjectiveResult.ClearedThisTick, Is.True);
                Assert.That(host.CurrentObjectiveResult.IsCleared, Is.True);
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.StageResult));
                Assert.That(installer.StageResultScreenView, Is.Not.Null);
                Assert.That(installer.StageResultScreenView.transform.parent, Is.EqualTo(installer.ScreenLayerView.ContentRoot));
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));

                installer.StageResultScreenView.ClickContinue();

                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));
            }
            finally
            {
                DestroySupportObjects(hostObject);
            }
        }

        private static void DestroySupportObjects(GameObject hostObject)
        {
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem != null)
            {
                Object.DestroyImmediate(eventSystem.gameObject);
            }

            if (hostObject != null)
            {
                Object.DestroyImmediate(hostObject);
            }
        }

        private static GameplaySceneHostConfiguration CreateConfiguration(
            EntityState[] initialEntities,
            StageObjectiveRuntimeDefinition objectiveRuntimeDefinition = null)
        {
            return new GameplaySceneHostConfiguration
            {
                AutoAdvanceTicks = false,
                AutoCreateViews = false,
                InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                InitialEntities = initialEntities,
                InitialTopology = new CubeTopologyState(FaceId.Floor),
                ObjectiveRuntimeDefinition = objectiveRuntimeDefinition ?? StageObjectiveRuntimeDefinition.Disabled,
                PlayerEntityId = 10,
            };
        }

        private static StageObjectiveRuntimeDefinition CreateSingleCellObjective(SurfaceCell goalCell)
        {
            var zone = new StageZoneRuntimeDefinition(
                "goal",
                goalCell.face,
                new[]
                {
                    new StageZoneRuntimeRegion(goalCell.PlanarPosition, goalCell.PlanarPosition),
                });

            return new StageObjectiveRuntimeDefinition(
                StageCompletionPolicy.RequirePlayerOnGoalWithAllConditions,
                10,
                new[] { zone },
                new[] { zone },
                System.Array.Empty<StageConditionRuntimeDefinition>());
        }

        private static EntityState CreatePlayerEntity(SurfaceCell position, Direction facing)
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
    }
}
