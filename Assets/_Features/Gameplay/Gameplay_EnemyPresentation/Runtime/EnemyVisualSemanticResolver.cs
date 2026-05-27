using Game.Feature.Gameplay;

namespace Game.Feature.Gameplay.Host
{
    public enum EnemyVisualActivityState
    {
        Normal = 0,
        FrontFaceInactive = 1,
    }

    public readonly struct EnemyVisualSemanticState
    {
        public EnemyVisualSemanticState(
            EnemyVisualActivityState activityState,
            bool shouldPauseAnimatorPlayback = false,
            bool shouldPauseAutonomousPresentation = false)
        {
            ActivityState = activityState;
            ShouldPauseAnimatorPlayback = shouldPauseAnimatorPlayback;
            ShouldPauseAutonomousPresentation = shouldPauseAutonomousPresentation;
        }

        public EnemyVisualActivityState ActivityState { get; }

        public bool ShouldPauseAnimatorPlayback { get; }

        public bool ShouldPauseAutonomousPresentation { get; }
    }

    public interface IEnemyVisualSemanticResolver
    {
        EnemyVisualSemanticState Resolve(in EnemyVisualPresentationFacts facts);
    }

    public sealed class DefaultEnemyVisualSemanticResolver : IEnemyVisualSemanticResolver
    {
        public EnemyVisualSemanticState Resolve(in EnemyVisualPresentationFacts facts)
        {
            var activityState = facts.IsEnemy &&
                                facts.IsVisible &&
                                facts.IsOnVisualFrontFace &&
                                facts.IsGameplayAutonomySuppressed
                ? EnemyVisualActivityState.FrontFaceInactive
                : EnemyVisualActivityState.Normal;
            var shouldPauseAutonomousPresentation = facts.IsEnemy &&
                                                   facts.IsVisible &&
                                                   (facts.IsGameplayAutonomySuppressed ||
                                                    facts.IsJumpLandingCompletionHeld);
            var shouldPauseAirborneAnimator = facts.IsEnemy &&
                                              facts.HasJumpAirborneVisualState &&
                                              (!facts.IsVisible ||
                                               facts.IsGameplayAutonomySuppressed ||
                                               facts.IsTopologyTransitionActive ||
                                               facts.IsJumpTopologySuspended);
            return new EnemyVisualSemanticState(
                activityState,
                shouldPauseAnimatorPlayback: shouldPauseAutonomousPresentation || shouldPauseAirborneAnimator,
                shouldPauseAutonomousPresentation: shouldPauseAutonomousPresentation);
        }
    }
}
