using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Stages;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.UI.Application;
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
                Assert.That(installer.HudView.IsVisible, Is.False);
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));
                Assert.That(installer.Ports.PauseService.IsPaused, Is.True);
                Assert.That(installer.HudController.IsGameplayReadOnly, Is.True);

                installer.SettingsScreenView.ClickBack();
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
                Assert.That(installer.HudView.IsVisible, Is.True);
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
        public void GameplayUiFlowInstaller_ComposesConfirmPolicyWithScreenTransitions()
        {
            var hostObject = new GameObject("GameplayUiFlowInstaller_ComposesConfirmPolicyWithScreenTransitions");

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

                var confirmCompletions = new List<PopupCompletion>();
                Assert.That(installer.Coordinator.RequestConfirmPopup(
                    new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false),
                    confirmCompletions.Add), Is.True);
                Assert.That(installer.PopupController.TopPopup.Value.PopupId, Is.EqualTo(PopupId.Confirm));
                Assert.That(installer.PopupLayerView.IsDimVisible, Is.True);
                Assert.That(installer.Coordinator.CurrentBlockSnapshot.BlocksScreenInteraction, Is.True);

                Assert.That(installer.Coordinator.HandleBackRequested(), Is.True);
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));
                Assert.That(confirmCompletions, Has.Count.EqualTo(1));
                Assert.That(confirmCompletions[0].CloseReason, Is.EqualTo(PopupCloseReason.Back));
                Assert.That(confirmCompletions[0].CompletionKind, Is.EqualTo(PopupCompletionKind.Cancelled));

                var transitionConfirmCompletions = new List<PopupCompletion>();
                Assert.That(installer.Coordinator.RequestConfirmPopup(
                    new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false),
                    transitionConfirmCompletions.Add), Is.True);

                Assert.That(installer.Coordinator.OpenSettingsScreen(), Is.True);
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Settings));
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));
                Assert.That(transitionConfirmCompletions, Has.Count.EqualTo(1));
                Assert.That(transitionConfirmCompletions[0].CloseReason, Is.EqualTo(PopupCloseReason.ScreenTransition));

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

            try
            {
                StageLaunchContextStore.Clear();
                contentEntry = CreateStageContentEntry(out presentationDefinition);
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
                Assert.That(installer.HudView.IsVisible, Is.True);

                var result = host.InputHost.RunSingleTick();

                Assert.That(result, Is.Not.Null);
                Assert.That(result.ObjectiveResult.ClearedThisTick, Is.True);
                Assert.That(host.CurrentObjectiveResult.IsCleared, Is.True);
                Assert.That(host.UiAccess.PresentationFeed.CurrentMinimalStageCompletion, Is.Not.Null);
                Assert.That(host.UiAccess.PresentationFeed.CurrentMinimalStageCompletion.Result.WasCleared, Is.True);
                Assert.That(host.UiAccess.PresentationFeed.CurrentMinimalStageCompletion.Result.StageRunId.IsValid, Is.True);
                Assert.That(host.UiAccess.PresentationFeed.CurrentMinimalStageCompletion.Result.ObjectiveSnapshot.IsCleared, Is.True);
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.StageResult));
                Assert.That(installer.HudView.IsVisible, Is.False);
                Assert.That(installer.StageResultScreenView, Is.Not.Null);
                Assert.That(installer.StageResultScreenView.transform.parent, Is.EqualTo(installer.ScreenLayerView.ContentRoot));
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));

                var stageResultPayload = installer.ScreenController.CurrentEntry.Value.Payload as StageResultScreenPayload;
                Assert.That(stageResultPayload, Is.Not.Null);
                Assert.That(stageResultPayload.ContinueStageRequest.IsValid, Is.True);
                Assert.That(stageResultPayload.ContinueStageRequest.StageId, Is.EqualTo(contentEntry.StageId));
                Assert.That(stageResultPayload.ContinueStageRequest.NavigationKind, Is.EqualTo(StageNavigationKind.Continue));
                Assert.That(stageResultPayload.RetryStageRequest.IsValid, Is.True);
                Assert.That(stageResultPayload.RetryStageRequest.StageId, Is.EqualTo(contentEntry.StageId));
                Assert.That(stageResultPayload.RetryStageRequest.NavigationKind, Is.EqualTo(StageNavigationKind.Retry));

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
            }
        }

        private static void DestroySupportObjects(GameObject hostObject)
        {
            if (hostObject != null)
            {
                var installer = hostObject.GetComponent<GameplayUiFlowInstaller>();
                if (installer != null)
                {
                    var onDestroy = typeof(GameplayUiFlowInstaller).GetMethod(
                        "OnDestroy",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.That(onDestroy, Is.Not.Null);
                    onDestroy.Invoke(installer, null);
                }
            }

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
            out StagePresentationDefinition presentationDefinition)
        {
            var stageId = StageId.CreateOrThrow("ui-flow-clear");
            var entry = ScriptableObject.CreateInstance<StageContentEntry>();
            entry.AssignStageId(stageId);

            presentationDefinition = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            SetPrivateField(presentationDefinition, "displayNameKey", StageDisplayNameKeys.ForStage(stageId));

            entry.AssignPresentationDefinition(presentationDefinition);
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
