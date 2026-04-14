using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Feature.UI.Tests
{
    public sealed class GameplayUiFlowIntegrationTests
    {
        [Test]
        [Category("Extended")]
        public void GameplayUiFlowInstaller_PreservesStage2RegressionSlice_ThroughDurableViews()
        {
            var hostObject = new GameObject("GameplayUiFlowInstaller_PreservesStage2RegressionSlice_ThroughDurableViews");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 1), Direction.Up),
                }));

                var installer = hostObject.AddComponent<GameplayUiFlowInstaller>();
                installer.Install(host);

                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
                Assert.That(installer.HudView.IsVisible, Is.True);
                Assert.That(installer.HudView.ViewModel.IsInteractive, Is.True);

                installer.GameplayScreenView.ClickHelp();

                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Help));
                Assert.That(installer.HudView.ViewModel.IsInteractive, Is.False);
                Assert.That(installer.HelpScreenView.IsVisible, Is.True);

                installer.HelpScreenView.ClickBack();

                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
                Assert.That(installer.HudView.ViewModel.IsInteractive, Is.True);

                installer.HudView.ClickMoveUp();

                Assert.That(installer.HudController.ViewModel.LastCommandResult.HasValue, Is.True);
                Assert.That(installer.HudController.ViewModel.LastCommandResult.Value.Accepted, Is.True);

                host.InputHost.RunSingleTick();

                Assert.That(host.UiAccess.PresentationFeed.CurrentState.HasBlockingPresentation, Is.True);

                installer.HudView.ClickFlipRight();

                Assert.That(installer.HudController.ViewModel.LastCommandResult.HasValue, Is.True);
                Assert.That(installer.HudController.ViewModel.LastCommandResult.Value.Accepted, Is.False);
                Assert.That(installer.HudController.ViewModel.LastCommandResult.Value.FailureKind, Is.EqualTo(Game.Feature.UI.HUD.GameplayHudCommandFailureKind.Busy));
                Assert.That(installer.HudController.ViewModel.FeedbackText, Is.EqualTo("Busy"));

                installer.HudView.ClickPause();

                Assert.That(installer.PopupController.Contains(PopupId.Pause), Is.True);
                Assert.That(installer.PausePopupView.IsVisible, Is.True);
                Assert.That(installer.Ports.PauseService.IsPaused, Is.True);
                Assert.That(installer.HudView.ViewModel.IsInteractive, Is.False);

                installer.PausePopupView.ClickResume();

                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));
                Assert.That(installer.Ports.PauseService.IsPaused, Is.False);
                Assert.That(installer.PausePopupView.IsVisible, Is.False);
            }
            finally
            {
                DestroySupportObjects(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayUiFlowInstaller_ComposesObjectiveStatusAndNonPausingObjectiveInfoPopup()
        {
            var hostObject = new GameObject("GameplayUiFlowInstaller_ComposesObjectiveStatusAndNonPausingObjectiveInfoPopup");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 1), Direction.Up),
                }));

                var installer = hostObject.AddComponent<GameplayUiFlowInstaller>();
                installer.Install(host);

                installer.GameplayScreenView.ClickObjectives();

                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.ObjectiveStatus));
                Assert.That(installer.ObjectiveStatusScreenView.IsVisible, Is.True);
                Assert.That(installer.HudView.ViewModel.IsInteractive, Is.False);
                Assert.That(installer.ObjectiveStatusScreenController.ViewModel.SummaryText, Is.Not.Empty);

                installer.ObjectiveStatusScreenView.ClickInfo();

                Assert.That(installer.PopupController.Contains(PopupId.ObjectiveInfo), Is.True);
                Assert.That(installer.ObjectiveInfoPopupView.IsVisible, Is.True);
                Assert.That(installer.Ports.PauseService.IsPaused, Is.False);
                Assert.That(installer.ObjectiveInfoPopupView.BodyText, Is.Not.Empty);

                installer.ObjectiveInfoPopupView.ClickClose();

                Assert.That(installer.PopupController.Contains(PopupId.ObjectiveInfo), Is.False);
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.ObjectiveStatus));
                Assert.That(installer.Ports.PauseService.IsPaused, Is.False);

                installer.ObjectiveStatusScreenView.ClickBack();

                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
                Assert.That(installer.HudView.ViewModel.IsInteractive, Is.True);
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

        private static GameplaySceneHostConfiguration CreateConfiguration(EntityState[] initialEntities)
        {
            return new GameplaySceneHostConfiguration
            {
                AutoAdvanceTicks = false,
                AutoCreateViews = false,
                InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                InitialEntities = initialEntities,
                InitialTopology = new CubeTopologyState(FaceId.Floor),
                ObjectiveRuntimeDefinition = StageObjectiveRuntimeDefinition.Disabled,
                PlayerEntityId = 10,
            };
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
