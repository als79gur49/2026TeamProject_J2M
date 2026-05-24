using Game.Feature.Gameplay;
using Game.Feature.Gameplay.Entities;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct EnemyVisualPresentationFacts
    {
        public EnemyVisualPresentationFacts(
            int entityId,
            bool isEnemy,
            bool isVisible,
            bool isCommittedVisible,
            bool isTransitionVisible,
            bool isTransitionOnlyVisible,
            bool isJumpDetachedVisible,
            GameplayProjectedFaceSlot? projectedSlot,
            bool isGameplayAutonomySuppressed,
            EnemyAiMode aiMode,
            bool hasActiveMotion)
            : this(
                entityId,
                isEnemy,
                isVisible,
                isCommittedVisible,
                isTransitionVisible,
                isTransitionOnlyVisible,
                isJumpDetachedVisible,
                isJumpLandingCompletionHeld: false,
                projectedSlot,
                isGameplayAutonomySuppressed,
                aiMode,
                hasActiveMotion)
        {
        }

        public EnemyVisualPresentationFacts(
            int entityId,
            bool isEnemy,
            bool isVisible,
            bool isCommittedVisible,
            bool isTransitionVisible,
            bool isTransitionOnlyVisible,
            bool isJumpDetachedVisible,
            bool isJumpLandingCompletionHeld,
            GameplayProjectedFaceSlot? projectedSlot,
            bool isGameplayAutonomySuppressed,
            EnemyAiMode aiMode,
            bool hasActiveMotion,
            bool hasJumpAirborneVisualState = false,
            bool isTopologyTransitionActive = false,
            bool isJumpTopologySuspended = false)
        {
            EntityId = entityId;
            IsEnemy = isEnemy;
            IsVisible = isVisible;
            IsCommittedVisible = isCommittedVisible;
            IsTransitionVisible = isTransitionVisible;
            IsTransitionOnlyVisible = isTransitionOnlyVisible;
            IsJumpDetachedVisible = isJumpDetachedVisible;
            IsJumpLandingCompletionHeld = isJumpLandingCompletionHeld;
            ProjectedSlot = projectedSlot;
            IsGameplayAutonomySuppressed = isGameplayAutonomySuppressed;
            AiMode = aiMode;
            HasActiveMotion = hasActiveMotion;
            HasJumpAirborneVisualState = hasJumpAirborneVisualState;
            IsTopologyTransitionActive = isTopologyTransitionActive;
            IsJumpTopologySuspended = isJumpTopologySuspended;
        }

        public int EntityId { get; }

        public bool IsEnemy { get; }

        public bool IsVisible { get; }

        public bool IsCommittedVisible { get; }

        public bool IsTransitionVisible { get; }

        public bool IsTransitionOnlyVisible { get; }

        public bool IsJumpDetachedVisible { get; }

        public bool IsJumpLandingCompletionHeld { get; }

        public GameplayProjectedFaceSlot? ProjectedSlot { get; }

        public bool IsGameplayAutonomySuppressed { get; }

        public EnemyAiMode AiMode { get; }

        public bool HasActiveMotion { get; }

        public bool HasJumpAirborneVisualState { get; }

        public bool IsTopologyTransitionActive { get; }

        public bool IsJumpTopologySuspended { get; }
    }
}
