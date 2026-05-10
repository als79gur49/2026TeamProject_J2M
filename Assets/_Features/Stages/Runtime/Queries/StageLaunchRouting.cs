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

    public interface IStageLaunchRouter
    {
        void Launch(StageNavigationRequest request);
    }
}
