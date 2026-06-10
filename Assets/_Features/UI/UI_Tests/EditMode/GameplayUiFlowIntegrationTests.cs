using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Stages;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
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
                Assert.That(installer.HudController.IsGameplayReadOnly, Is.False);

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
        public void GameplayUiFlowInstaller_HudPause_SettingsBack_ReturnsThroughFreshPausePopup_AndResumesOnlyOnResume()
        {
            var hostObject = new GameObject("GameplayUiFlowInstaller_HudPause_SettingsBack_ReturnsThroughFreshPausePopup_AndResumesOnlyOnResume");

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

                installer.HudView.ClickPause();
                var originalPausePopup = installer.PausePopupView;

                Assert.That(installer.Ports.PauseService.IsPaused, Is.True);
                Assert.That(installer.HudController.IsGameplayReadOnly, Is.True);

                originalPausePopup.ClickSettings();
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Settings));
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));
                Assert.That(installer.Ports.PauseService.IsPaused, Is.True);
                Assert.That(installer.HudController.IsGameplayReadOnly, Is.True);

                installer.SettingsScreenView.ClickBack();
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
                Assert.That(installer.PausePopupView, Is.Not.Null);
                Assert.That(installer.PausePopupView, Is.Not.SameAs(originalPausePopup));
                Assert.That(installer.Ports.PauseService.IsPaused, Is.True);
                Assert.That(installer.HudController.IsGameplayReadOnly, Is.True);

                installer.PausePopupView.ClickResume();
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));
                Assert.That(installer.Ports.PauseService.IsPaused, Is.False);
                Assert.That(installer.HudController.IsGameplayReadOnly, Is.False);
            }
            finally
            {
                DestroySupportObjects(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiFlowInstaller_ComposesTooltipAndRewardPolicies()
        {
            var hostObject = new GameObject("GameplayUiFlowInstaller_ComposesTooltipAndRewardPolicies");

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

                var tooltipCompletions = new List<PopupCompletion>();
                Assert.That(installer.Coordinator.RequestTooltipPopup(
                    new TooltipPopupPayload("Tip", "Tooltip body"),
                    tooltipCompletions.Add), Is.True);
                Assert.That(installer.PopupController.TopPopup.Value.PopupId, Is.EqualTo(PopupId.Tooltip));
                Assert.That(installer.PopupLayerView.IsDimVisible, Is.False);
                Assert.That(installer.Coordinator.CurrentBlockSnapshot.BlocksScreenInteraction, Is.False);

                Assert.That(installer.Coordinator.HandleBackRequested(), Is.True);
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));
                Assert.That(tooltipCompletions, Has.Count.EqualTo(1));
                Assert.That(tooltipCompletions[0].CloseReason, Is.EqualTo(PopupCloseReason.Back));

                var gameplayTooltipCompletions = new List<PopupCompletion>();
                Assert.That(installer.Coordinator.RequestTooltipPopup(
                    new TooltipPopupPayload("Gameplay Tip", "Tooltip body"),
                    gameplayTooltipCompletions.Add), Is.True);

                Assert.That(installer.Coordinator.OpenSettingsScreen(), Is.True);
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Settings));
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
            StageContentEntry contentEntry = null;
            StagePresentationDefinition presentationDefinition = null;
            StageClearEvaluationDefinition clearEvaluationDefinition = null;
            StageRewardDefinition rewardDefinition = null;
            StageProgressionDefinition progressionDefinition = null;

            try
            {
                StageLaunchContextStore.Clear();
                contentEntry = CreateStageContentEntry(
                    out presentationDefinition,
                    out clearEvaluationDefinition,
                    out rewardDefinition,
                    out progressionDefinition);
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(
                    new[]
                    {
                        CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), Direction.Up),
                    },
                    CreateSingleCellObjective(new SurfaceCell(FaceId.Floor, 0, 0)),
                    contentEntry));

                var installer = hostObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(host);

                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));

                var result = host.InputHost.RunSingleTick();

                Assert.That(result, Is.Not.Null);
                Assert.That(result.ObjectiveResult.ClearedThisTick, Is.True);
                Assert.That(host.CurrentObjectiveResult.IsCleared, Is.True);
                Assert.That(host.UiAccess.PresentationFeed.CurrentStageCompletion, Is.Not.Null);
                Assert.That(host.UiAccess.PresentationFeed.CurrentStageCompletion.ClearResult.WasCleared, Is.True);
                Assert.That(host.UiAccess.PresentationFeed.CurrentStageCompletion.RewardGrantResult.AnyGranted, Is.True);
                Assert.That(host.UiAccess.PresentationFeed.CurrentStageCompletion.UpdatedProgress.HasCleared, Is.True);
                Assert.That(host.UiAccess.PresentationFeed.CurrentStageCompletion.UpdatedProgress.ClearCount, Is.EqualTo(1));
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.StageResult));
                Assert.That(installer.StageResultScreenView, Is.Not.Null);
                Assert.That(installer.StageResultScreenView.transform.parent, Is.EqualTo(installer.ScreenLayerView.ContentRoot));
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(1));
                Assert.That(installer.PopupController.TopPopup.HasValue, Is.True);
                Assert.That(installer.PopupController.TopPopup.Value.PopupId, Is.EqualTo(PopupId.Reward));

                installer.RewardPopupView.ClickAcknowledge();
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));

                var stageResultPayload = installer.ScreenController.CurrentEntry.Value.Payload as StageResultScreenPayload;
                Assert.That(stageResultPayload, Is.Not.Null);
                Assert.That(stageResultPayload.ContinueStageRequest.IsValid, Is.True);
                Assert.That(stageResultPayload.ContinueStageRequest.StageId, Is.EqualTo(contentEntry.StageId));
                Assert.That(stageResultPayload.ContinueStageRequest.NavigationKind, Is.EqualTo(StageNavigationKind.Continue));

                installer.StageResultScreenView.ClickContinue();

                Assert.That(StageLaunchContextStore.TryGetCurrent(out _), Is.False);
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.StageResult));
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));
            }
            finally
            {
                StageLaunchContextStore.Clear();
                DestroySupportObjects(hostObject);
                DestroyImmediateIfExists(contentEntry);
                DestroyImmediateIfExists(presentationDefinition);
                DestroyImmediateIfExists(clearEvaluationDefinition);
                DestroyImmediateIfExists(rewardDefinition);
                DestroyImmediateIfExists(progressionDefinition);
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
            StageObjectiveRuntimeDefinition objectiveRuntimeDefinition = null,
            StageContentEntry stageContentEntry = null)
        {
            return new GameplaySceneHostConfiguration
            {
                AutoAdvanceTicks = false,
                AutoCreateViews = false,
                InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                InitialEntities = initialEntities,
                InitialTopology = new CubeTopologyState(FaceId.Floor),
                StageContentEntry = stageContentEntry,
                ObjectiveRuntimeDefinition = objectiveRuntimeDefinition ?? StageObjectiveRuntimeDefinition.Disabled,
                PlayerEntityId = 10,
            };
        }

        private static StageContentEntry CreateStageContentEntry(
            out StagePresentationDefinition presentationDefinition,
            out StageClearEvaluationDefinition clearEvaluationDefinition,
            out StageRewardDefinition rewardDefinition,
            out StageProgressionDefinition progressionDefinition)
        {
            var stageId = StageId.CreateOrThrow("ui-flow-clear");
            var entry = ScriptableObject.CreateInstance<StageContentEntry>();
            entry.AssignStageId(stageId);

            presentationDefinition = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            SetPrivateField(presentationDefinition, "displayName", "UI Flow Clear");
            SetPrivateField(presentationDefinition, "resultTitle", "Clear Confirmed");
            SetPrivateField(presentationDefinition, "resultSummaryText", "Mapped from StageCompletionReadModel");
            SetPrivateField(presentationDefinition, "resultDetailText", "Reward popup and stage result share the same completion pipeline.");
            SetPrivateField(presentationDefinition, "resultContinueLabel", "Continue");

            clearEvaluationDefinition = ScriptableObject.CreateInstance<StageClearEvaluationDefinition>();
            SetPrivateField(clearEvaluationDefinition, "baseScore", 500);
            SetPrivateField(clearEvaluationDefinition, "starThresholds", new[]
            {
                new StageStarThresholdDefinition
                {
                    StarCount = 3,
                    MinimumScore = 500,
                },
            });
            SetPrivateField(clearEvaluationDefinition, "rankThresholds", new[]
            {
                new StageRankThresholdDefinition
                {
                    RankId = "S",
                    MinimumScore = 500,
                },
            });

            rewardDefinition = ScriptableObject.CreateInstance<StageRewardDefinition>();
            var clearRewardRule = new StageRewardRuleDefinition
            {
                TriggerKind = StageRewardTriggerKind.Clear,
                GrantOnce = true,
                Rewards = new[]
                {
                    new RewardEntry
                    {
                        RewardId = "Crystal",
                        Amount = 2,
                    },
                },
            };
            clearRewardRule.SetRuleId("first-clear");
            clearRewardRule.SetDeprecatedRuleIds(System.Array.Empty<string>());
            SetPrivateField(rewardDefinition, "rules", new[] { clearRewardRule });

            progressionDefinition = ScriptableObject.CreateInstance<StageProgressionDefinition>();

            entry.AssignPresentationDefinition(presentationDefinition);
            entry.AssignClearEvaluationDefinition(clearEvaluationDefinition);
            entry.AssignRewardDefinition(rewardDefinition);
            entry.AssignProgressionDefinition(progressionDefinition);
            return entry;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void DestroyImmediateIfExists(Object value)
        {
            if (value != null)
            {
                Object.DestroyImmediate(value);
            }
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
                StageCompletionPolicy.RequireAllConditions,
                10,
                new[] { zone },
                new[]
                {
                    new StageObjectiveConditionRuntimeDefinitionEntry(
                        new PlayerAtAnyZoneConditionRuntimeDefinition(
                            "primary-goal",
                            "Primary Goal",
                            10,
                            new[] { zone },
                            requireAlive: true),
                        required: true,
                        StageObjectiveConditionRole.PrimaryGoal,
                        "primary-goal"),
                });
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
