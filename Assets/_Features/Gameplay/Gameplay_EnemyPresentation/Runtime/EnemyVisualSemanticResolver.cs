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
        public EnemyVisualSemanticState(EnemyVisualActivityState activityState)
        {
            ActivityState = activityState;
        }

        public EnemyVisualActivityState ActivityState { get; }
    }

    public interface IEnemyVisualSemanticResolver
    {
        EnemyVisualSemanticState Resolve(in EnemyVisualPresentationFacts facts);
    }

    public sealed class DefaultEnemyVisualSemanticResolver : IEnemyVisualSemanticResolver
    {
        public EnemyVisualSemanticState Resolve(in EnemyVisualPresentationFacts facts)
        {
            if (!facts.IsEnemy || !facts.IsVisible)
            {
                return new EnemyVisualSemanticState(EnemyVisualActivityState.Normal);
            }

            if (facts.ProjectedSlot == GameplayProjectedFaceSlot.Front)
            {
                return new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive);
            }

            return new EnemyVisualSemanticState(EnemyVisualActivityState.Normal);
        }
    }
}
