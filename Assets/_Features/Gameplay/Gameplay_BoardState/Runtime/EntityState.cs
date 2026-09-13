using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Entities;

namespace Game.Feature.Gameplay.BoardState
{
    [Flags]
    public enum BoxCapabilities
    {
        None = 0,
        Push = 1 << 0,
        Flip = 1 << 1,
        Item = 1 << 2,
        Destroy = 1 << 3,
        JumpCrushable = 1 << 4,
    }

    public enum BoxArchetype
    {
        Normal = 0,
        Moon = 1,
        GravityField = 2,
    }

    public enum GravityFieldPhase
    {
        None = 0,
        Charging = 1,
        Active = 2,
    }

    public struct EntityState
    {
        public int entityId;
        public SurfaceCell position;
        public int hp;
        public int maxHp;
        public int teamId;
        public EntityType type;
        public UnitRole unitRole;
        public UnitMobilityKind unitMobilityKind;
        public EntityPhaseState state;
        public int stateTimer;
        public Direction facing;
        public EntityBoardPresence boardPresence;
        public bool markedForDeath;
        public int spawnTick;
        public BoxCapabilities boxCapabilities;
        public BoxArchetype boxArchetype;
        public GravityFieldPhase gravityFieldPhase;
        public int gravityFieldTimerTicks;
        public int kineticInstigatorEntityId;
        public int kineticInstigatorTeamId;
        public EnemyAiMode aiMode;
        // Temporary generic recover countdown only. Charge progress is authoritative in
        // EnemyChargeRuntimeState, and future richer recover semantics should move to
        // a dedicated EnemyRecoverRuntimeState instead of expanding this field again.
        public int aiStateTimer;
        public int enemyLocomotionCooldownTicks;
        public int enemyAttackCooldownTicks;
        public int enemyAttackCooldownTotalTicks;
    }

    internal static class CleanupCandidateQueries
    {
        internal static bool ShouldRemove(in EntityState entity)
        {
            return entity.hp <= 0 || entity.markedForDeath;
        }

        internal static bool HasStateTimer(in EntityState entity)
        {
            return entity.stateTimer > 0;
        }

        internal static bool CanTransitionImmediately(in EntityState entity)
        {
            if (entity.stateTimer > 0)
            {
                return false;
            }

            return entity.state == EntityPhaseState.Acting ||
                   entity.state == EntityPhaseState.Cooldown;
        }

        internal static bool CanTransitionWhenTimerExpires(in EntityState entity)
        {
            return entity.state == EntityPhaseState.Acting ||
                   entity.state == EntityPhaseState.Cooldown;
        }
    }

    internal sealed class CleanupCandidateSnapshot
    {
        internal static readonly CleanupCandidateSnapshot Empty = new(
            Array.Empty<int>(),
            Array.Empty<int>(),
            Array.Empty<int>());

        private readonly int[] _immediateTransitionCandidateIds;
        private readonly int[] _removalCandidateIds;
        private readonly int[] _timerCandidateIds;

        private CleanupCandidateSnapshot(
            int[] removalCandidateIds,
            int[] timerCandidateIds,
            int[] immediateTransitionCandidateIds)
        {
            _removalCandidateIds = removalCandidateIds ?? throw new ArgumentNullException(nameof(removalCandidateIds));
            _timerCandidateIds = timerCandidateIds ?? throw new ArgumentNullException(nameof(timerCandidateIds));
            _immediateTransitionCandidateIds = immediateTransitionCandidateIds ??
                                               throw new ArgumentNullException(nameof(immediateTransitionCandidateIds));
        }

        internal ReadOnlyMemory<int> RemovalCandidateIds => _removalCandidateIds;

        internal ReadOnlyMemory<int> TimerCandidateIds => _timerCandidateIds;

        internal ReadOnlyMemory<int> ImmediateTransitionCandidateIds => _immediateTransitionCandidateIds;

        internal static CleanupCandidateSnapshot Create(
            SortedSet<int> removalCandidateIds,
            SortedSet<int> timerCandidateIds,
            SortedSet<int> immediateTransitionCandidateIds)
        {
            if (removalCandidateIds == null)
            {
                throw new ArgumentNullException(nameof(removalCandidateIds));
            }

            if (timerCandidateIds == null)
            {
                throw new ArgumentNullException(nameof(timerCandidateIds));
            }

            if (immediateTransitionCandidateIds == null)
            {
                throw new ArgumentNullException(nameof(immediateTransitionCandidateIds));
            }

            if (removalCandidateIds.Count == 0 &&
                timerCandidateIds.Count == 0 &&
                immediateTransitionCandidateIds.Count == 0)
            {
                return Empty;
            }

            return new CleanupCandidateSnapshot(
                CopyToArray(removalCandidateIds),
                CopyToArray(timerCandidateIds),
                CopyToArray(immediateTransitionCandidateIds));
        }

        internal static CleanupCandidateSnapshot CreateFromEntities(
            IReadOnlyDictionary<int, EntityState> entitiesById)
        {
            if (entitiesById == null)
            {
                throw new ArgumentNullException(nameof(entitiesById));
            }

            var removalCandidateIds = new SortedSet<int>();
            var timerCandidateIds = new SortedSet<int>();
            var immediateTransitionCandidateIds = new SortedSet<int>();
            foreach (var pair in entitiesById)
            {
                UpdateMembership(
                    pair.Value,
                    removalCandidateIds,
                    timerCandidateIds,
                    immediateTransitionCandidateIds);
            }

            return Create(removalCandidateIds, timerCandidateIds, immediateTransitionCandidateIds);
        }

        internal void CopyTo(
            SortedSet<int> removalCandidateIds,
            SortedSet<int> timerCandidateIds,
            SortedSet<int> immediateTransitionCandidateIds)
        {
            CopyToSet(_removalCandidateIds, removalCandidateIds);
            CopyToSet(_timerCandidateIds, timerCandidateIds);
            CopyToSet(_immediateTransitionCandidateIds, immediateTransitionCandidateIds);
        }

        internal static void UpdateMembership(
            in EntityState entity,
            SortedSet<int> removalCandidateIds,
            SortedSet<int> timerCandidateIds,
            SortedSet<int> immediateTransitionCandidateIds)
        {
            SetMembership(removalCandidateIds, entity.entityId, CleanupCandidateQueries.ShouldRemove(entity));
            SetMembership(timerCandidateIds, entity.entityId, CleanupCandidateQueries.HasStateTimer(entity));
            SetMembership(
                immediateTransitionCandidateIds,
                entity.entityId,
                CleanupCandidateQueries.CanTransitionImmediately(entity));
        }

        internal static void UpdateMembership(
            in EntityState previousEntity,
            in EntityState updatedEntity,
            SortedSet<int> removalCandidateIds,
            SortedSet<int> timerCandidateIds,
            SortedSet<int> immediateTransitionCandidateIds)
        {
            UpdateMembershipWhenChanged(
                removalCandidateIds,
                updatedEntity.entityId,
                CleanupCandidateQueries.ShouldRemove(previousEntity),
                CleanupCandidateQueries.ShouldRemove(updatedEntity));
            UpdateMembershipWhenChanged(
                timerCandidateIds,
                updatedEntity.entityId,
                CleanupCandidateQueries.HasStateTimer(previousEntity),
                CleanupCandidateQueries.HasStateTimer(updatedEntity));
            UpdateMembershipWhenChanged(
                immediateTransitionCandidateIds,
                updatedEntity.entityId,
                CleanupCandidateQueries.CanTransitionImmediately(previousEntity),
                CleanupCandidateQueries.CanTransitionImmediately(updatedEntity));
        }

        private static int[] CopyToArray(SortedSet<int> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (source.Count == 0)
            {
                return Array.Empty<int>();
            }

            var result = new int[source.Count];
            source.CopyTo(result);
            return result;
        }

        private static void CopyToSet(int[] source, SortedSet<int> target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            target.Clear();
            for (var i = 0; i < source.Length; i++)
            {
                target.Add(source[i]);
            }
        }

        private static void SetMembership(SortedSet<int> target, int entityId, bool shouldContain)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (shouldContain)
            {
                target.Add(entityId);
            }
            else
            {
                target.Remove(entityId);
            }
        }

        private static void UpdateMembershipWhenChanged(
            SortedSet<int> target,
            int entityId,
            bool wasMember,
            bool isMember)
        {
            if (wasMember == isMember)
            {
                return;
            }

            SetMembership(target, entityId, isMember);
        }
    }
}
