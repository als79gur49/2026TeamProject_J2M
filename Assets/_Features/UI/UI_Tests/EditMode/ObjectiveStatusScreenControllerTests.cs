using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class ObjectiveStatusScreenControllerTests
    {
        [Test]
        public void ObjectiveStatusScreenController_DerivesOverviewAndSessionContent_FromExistingQueries()
        {
            var queryFacade = new FakeGameplayQueryFacade(
                new GameplaySessionReadModel(nextTickIndex: 7, isPaused: false, canAcceptGameplayCommands: false, isStageCleared: false),
                FakeGameplayQueryFacade.CreateDefaultPlayerHud(),
                new GameplayObjectiveReadModel(hasObjective: true, goalReached: true, allConditionsSatisfied: false, isCleared: false));
            using var presenter = new ObjectiveStatusPresenter(
                queryFacade,
                new FakeGameplayPresentationFeed(),
                new FakeGameplayPauseService());
            using var controller = new ObjectiveStatusScreenController(presenter);

            Assert.That(controller.ViewModel.BadgeText, Is.EqualTo("Goal Reached"));
            Assert.That(controller.ViewModel.SummaryText, Does.Contain("Primary goal reached"));

            var viewObject = new UnityEngine.GameObject("ObjectiveStatusScreenView");
            try
            {
                var view = viewObject.AddComponent<Game.Feature.UI.Screens.ObjectiveStatusScreenView>();
                controller.AttachView(view);
                view.IsVisible = true;
                view.ClickSession();

                Assert.That(controller.ViewModel.BadgeText, Is.EqualTo("Session"));
                Assert.That(controller.ViewModel.SummaryText, Does.Contain("7"));
                Assert.That(controller.BuildInfoContent().Title, Is.EqualTo("Session Info"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(viewObject);
            }
        }
    }
}
