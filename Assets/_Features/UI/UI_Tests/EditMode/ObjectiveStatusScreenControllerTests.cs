using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.Application;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class ObjectiveStatusScreenControllerTests
    {
        [Test]
        public void ObjectiveStatusScreenPresenter_DerivesOverviewAndSessionContent_FromExistingQueries()
        {
            var queryFacade = new FakeGameplayQueryFacade(
                new GameplaySessionReadModel(nextTickIndex: 7, isPaused: false, canAcceptGameplayCommands: false, isStageCleared: false),
                FakeGameplayQueryFacade.CreateDefaultPlayerHud(),
                new GameplayObjectiveReadModel(hasObjective: true, goalReached: true, allConditionsSatisfied: false, isCleared: false));
            using var presentationSource = UiTestPortFactory.CreatePresentationSource(queryFacade: queryFacade);
            using var statePresenter = new ObjectiveStatusPresenter(queryFacade, presentationSource);
            using var presenter = new ObjectiveStatusScreenPresenter(statePresenter);

            presenter.ApplyPayload(Game.Feature.UI.Screens.ObjectiveStatusScreenPayload.Default);

            Assert.That(presenter.ViewModel.BadgeText, Is.EqualTo("Goal Reached"));
            Assert.That(presenter.ViewModel.SummaryText, Does.Contain("Primary goal reached"));

            presenter.ShowSession();

            Assert.That(presenter.ViewModel.BadgeText, Is.EqualTo("Session"));
            Assert.That(presenter.ViewModel.SummaryText, Does.Contain("7"));
            Assert.That(presenter.BuildInfoPopupPayload().TitleText, Is.EqualTo("Session Info"));
        }
    }
}
