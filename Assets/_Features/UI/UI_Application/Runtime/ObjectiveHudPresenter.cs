using Game.Feature.UI.HUD;

namespace Game.Feature.UI.Application
{
    public sealed class ObjectiveHudPresenter
    {
        private const string CompleteText = "Objective Complete";

        public ObjectiveHudViewModel ViewModel { get; } = new();

        public void Apply(UIObjectiveSlice objective)
        {
            if (!objective.HasObjective)
            {
                ViewModel.SetState(false, string.Empty, false);
                return;
            }

            ViewModel.SetState(
                true,
                objective.IsCleared ? CompleteText : ResolveObjectiveText(objective),
                objective.IsCleared);
        }

        private static string ResolveObjectiveText(UIObjectiveSlice objective)
        {
            if (!string.IsNullOrWhiteSpace(objective.Summary))
            {
                return objective.Summary;
            }

            if (!string.IsNullOrWhiteSpace(objective.Title))
            {
                return objective.Title;
            }

            var conditions = objective.Conditions;
            for (var i = 0; i < conditions.Count; i++)
            {
                if (conditions[i].Required &&
                    !string.IsNullOrWhiteSpace(conditions[i].TitleText))
                {
                    return conditions[i].TitleText;
                }
            }

            return string.Empty;
        }
    }
}
