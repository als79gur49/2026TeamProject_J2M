using System;

namespace Game.Feature.Stages
{
    public enum StageNavigationKind
    {
        None = 0,
        Continue = 1,
        Retry = 2,
        NextStage = 3,
    }

    public readonly struct StageNavigationRequest
    {
        public static readonly StageNavigationRequest None = new(StageId.None, StageNavigationKind.None, string.Empty);

        public StageNavigationRequest(
            StageId stageId,
            StageNavigationKind navigationKind,
            string source,
            StageTransitionHint transitionHint = default)
        {
            if (navigationKind != StageNavigationKind.None && !stageId.IsValid)
            {
                throw new ArgumentException("Stage navigation requests require a canonical StageId.", nameof(stageId));
            }

            StageId = stageId;
            NavigationKind = navigationKind;
            Source = source ?? string.Empty;
            TransitionHint = transitionHint;
        }

        public StageId StageId { get; }

        public StageNavigationKind NavigationKind { get; }

        public string Source { get; }

        public StageTransitionHint TransitionHint { get; }

        public bool IsValid => StageId.IsValid && NavigationKind != StageNavigationKind.None;

        public StageNavigationRequest WithTransitionHint(StageTransitionHint transitionHint)
        {
            return new StageNavigationRequest(StageId, NavigationKind, Source, transitionHint);
        }
    }

    public static class CampaignPendinglessLaunchPolicy
    {
        public static bool IsAllowed(StageNavigationRequest request)
        {
            if (!request.IsValid)
            {
                return false;
            }

            switch (request.NavigationKind)
            {
                case StageNavigationKind.Retry:
                    return string.Equals(request.Source, "stage-result-retry", StringComparison.Ordinal) ||
                           string.Equals(request.Source, "pause-retry", StringComparison.Ordinal) ||
                           string.Equals(request.Source, "campaign-death-retry", StringComparison.Ordinal) ||
                           string.Equals(request.Source, "level-failed-restart-level", StringComparison.Ordinal);
                case StageNavigationKind.NextStage:
                    return string.Equals(request.Source, "campaign-auto-next", StringComparison.Ordinal);
                default:
                    return false;
            }
        }
    }

    public interface IStageLaunchRouter
    {
        void Launch(StageNavigationRequest request);
    }

    public interface IStageLaunchRouterProvider
    {
        bool TryCreateStageLaunchRouter(string currentSceneName, out IStageLaunchRouter router);
    }
}
