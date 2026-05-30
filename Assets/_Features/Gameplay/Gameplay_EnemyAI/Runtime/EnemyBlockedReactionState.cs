using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Entities
{
    public enum EnemyBlockedReactionKind
    {
        None = 0,
        KinematicContinuationTargetBlocked = 1,
    }

    public readonly struct PendingEnemyBlockedReaction
    {
        public PendingEnemyBlockedReaction(
            int enemyEntityId,
            EnemyBlockedReactionKind kind,
            EnemyAiMode modeAtBlock,
            SurfaceCell sourceCell,
            SurfaceCell blockedTargetCell,
            Direction blockedDirection,
            LegalityBlockerKind blockerKind,
            SolidKind? blockerSolidKind,
            EntityType? blockerEntityType,
            int? blockerEntityId,
            int createdTick,
            int expireTick)
        {
            EnemyEntityId = enemyEntityId;
            Kind = kind;
            ModeAtBlock = modeAtBlock;
            SourceCell = sourceCell;
            BlockedTargetCell = blockedTargetCell;
            BlockedDirection = blockedDirection;
            BlockerKind = blockerKind;
            BlockerSolidKind = blockerSolidKind;
            BlockerEntityType = blockerEntityType;
            BlockerEntityId = blockerEntityId;
            CreatedTick = createdTick;
            ExpireTick = expireTick;
        }

        public int EnemyEntityId { get; }

        public EnemyBlockedReactionKind Kind { get; }

        public EnemyAiMode ModeAtBlock { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell BlockedTargetCell { get; }

        public Direction BlockedDirection { get; }

        public LegalityBlockerKind BlockerKind { get; }

        public SolidKind? BlockerSolidKind { get; }

        public EntityType? BlockerEntityType { get; }

        public int? BlockerEntityId { get; }

        public int CreatedTick { get; }

        public int ExpireTick { get; }

        public bool IsExpiredBeforeDecision(int tickIndex)
        {
            return tickIndex > ExpireTick;
        }

        public bool ShouldCleanupAtEndOfTick(int tickIndex)
        {
            return tickIndex >= ExpireTick;
        }
    }

    internal readonly struct PendingEnemyBlockedReactionSnapshotEntry
    {
        public PendingEnemyBlockedReactionSnapshotEntry(int entityId, PendingEnemyBlockedReaction reaction)
        {
            EntityId = entityId;
            Reaction = reaction;
        }

        public int EntityId { get; }

        public PendingEnemyBlockedReaction Reaction { get; }
    }
}
