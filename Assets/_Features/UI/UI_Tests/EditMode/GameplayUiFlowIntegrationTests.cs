using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.UI.Tests
{
    public sealed class GameplayUiFlowIntegrationTests
    {
        [Test]
        [Category("Extended")]
        public void GameplayUiFlowInstaller_ComposesVerticalSlice_ThroughUiAccessOnly()
        {
            var hostObject = new GameObject("GameplayUiFlowInstaller_ComposesVerticalSlice_ThroughUiAccessOnly");

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
                Assert.That(installer.HudView.IsVisible, Is.True);
                Assert.That(installer.HudView.ViewModel.IsInteractive, Is.False);
                Assert.That(installer.HelpScreenView.IsVisible, Is.True);

                installer.HelpScreenView.ClickBack();

                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
                Assert.That(installer.HudView.ViewModel.IsInteractive, Is.True);

                installer.HudView.ClickMoveUp();

                Assert.That(installer.HudController.ViewModel.LastCommandAcceptance.HasValue, Is.True);
                Assert.That(installer.HudController.ViewModel.LastCommandAcceptance.Value.Accepted, Is.True);

                var tickResult = host.InputHost.RunSingleTick();

                Assert.That(tickResult, Is.Not.Null);
                Assert.That(host.UiAccess.PresentationFeed.CurrentState.HasBlockingPresentation, Is.True);

                installer.HudView.ClickFlipRight();

                Assert.That(installer.HudController.ViewModel.LastCommandAcceptance.HasValue, Is.True);
                Assert.That(installer.HudController.ViewModel.LastCommandAcceptance.Value.Accepted, Is.False);
                Assert.That(installer.HudController.ViewModel.LastCommandAcceptance.Value.RejectionReason, Is.EqualTo(GameplayCommandRejectionReason.BlockingPresentation));
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
