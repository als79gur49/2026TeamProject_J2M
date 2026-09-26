using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Vfx;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    internal sealed class EnemyMotionAttachedVfxFollowerPlanner
    {
        private readonly List<AttachedVfxFollowerDesiredState> desiredFollowers = new();
        private readonly List<AttachedVfxFollowerKey> explicitStopKeys = new();
        private readonly HashSet<AttachedVfxFollowerKey> desiredFollowerKeys = new();
        private readonly HashSet<AttachedVfxFollowerKey> explicitStopKeySet = new();
        private readonly HashSet<int> removedEntityIds = new();

        public IReadOnlyList<AttachedVfxFollowerDesiredState> DesiredFollowers => desiredFollowers;

        public IReadOnlyList<AttachedVfxFollowerKey> ExplicitStopKeys => explicitStopKeys;

        public void Build(
            int tickIndex,
            TickPresentationData presentationData,
            bool enableGlideWindTrail,
            bool enableChargeBoosterTrail,
            bool enableBoxSlideFollowLoop,
            bool enableEnemyJumpWindupLoop,
            IReadOnlyList<EntityState> finalEntities = null,
            IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId = null,
            bool enableEnemyUtilityCooldownAura = false,
            bool enableEnemyAttackCooldownFollow = false)
        {
            desiredFollowers.Clear();
            explicitStopKeys.Clear();
            desiredFollowerKeys.Clear();
            explicitStopKeySet.Clear();
            removedEntityIds.Clear();

            if (presentationData == null ||
                (!enableGlideWindTrail &&
                     !enableChargeBoosterTrail &&
                     !enableBoxSlideFollowLoop &&
                     !enableEnemyJumpWindupLoop &&
                     !enableEnemyUtilityCooldownAura &&
                     !enableEnemyAttackCooldownFollow))
            {
                return;
            }

            CollectRemovedEntityIds(presentationData);

            if (enableBoxSlideFollowLoop)
            {
                AddBoxSlideExplicitStopKeys(presentationData, finalEntities);
                AddBoxSlideFollowers(tickIndex, presentationData);
            }

            if (enableEnemyJumpWindupLoop)
            {
                AddJumpWindupFollowers(presentationData);
            }

            if (enableEnemyUtilityCooldownAura)
            {
                AddUtilityCooldownAuraFollowers(presentationData, viewsByEntityId);
            }

            if (enableEnemyAttackCooldownFollow)
            {
                AddEnemyAttackCooldownFollowers(finalEntities, viewsByEntityId);
            }

            if (enableGlideWindTrail)
            {
                AddGlideFollowers(presentationData);
            }

            if (enableChargeBoosterTrail)
            {
                AddChargeFollowers(presentationData);
            }
        }

        public void Clear()
        {
            desiredFollowers.Clear();
            explicitStopKeys.Clear();
            desiredFollowerKeys.Clear();
            explicitStopKeySet.Clear();
            removedEntityIds.Clear();
        }

        public void RemoveCue(GameplayVfxCueId cueId)
        {
            desiredFollowers.RemoveAll(state => state.CueId == cueId);
            desiredFollowerKeys.RemoveWhere(key => key.CueId.Equals(cueId));
            explicitStopKeys.RemoveAll(key => key.CueId.Equals(cueId));
            explicitStopKeySet.RemoveWhere(key => key.CueId.Equals(cueId));
        }

        private void AddGlideFollowers(TickPresentationData presentationData)
        {
            var signals = presentationData.EnemyGlideSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.EntityId <= 0 ||
                    removedEntityIds.Contains(signal.EntityId))
                {
                    continue;
                }

                if (!TryResolveGlideFollowerCue(signal.Phase, out var cueId, out var stateKind))
                {
                    continue;
                }

                AddDesiredFollower(
                    new AttachedVfxFollowerDesiredState(
                        cueId,
                        signal.EntityId,
                        stateKind,
                        signal.Sequence,
                        Vector3.zero,
                        Quaternion.identity));

                if (signal.Phase == EnemyGlidePhase.Active)
                {
                    AddDesiredFollower(
                        new AttachedVfxFollowerDesiredState(
                            GameplayVfxCueId.From(EnemyVfxCue.GlideMagicBlastFollow),
                            signal.EntityId,
                            AttachedVfxFollowerStateKind.EnemyGlideMagicBlastFollow,
                            signal.Sequence,
                            Vector3.zero,
                            Quaternion.identity));
                }
            }
        }

        private void AddBoxSlideFollowers(
            int tickIndex,
            TickPresentationData presentationData)
        {
            var startSignals = presentationData.BoxSlideStartSignals;
            for (var i = 0; i < startSignals.Count; i++)
            {
                var signal = startSignals[i];
                if (signal.BoxEntityId <= 0 ||
                    removedEntityIds.Contains(signal.BoxEntityId))
                {
                    continue;
                }

                AddBoxSlideFollower(signal.BoxEntityId);
            }

            var motions = presentationData.EntityMotions;
            for (var i = 0; i < motions.Count; i++)
            {
                var motion = motions[i];
                if (motion.MotionKind != TickEntityMotionKind.BoxSlide ||
                    motion.EntityId <= 0 ||
                    removedEntityIds.Contains(motion.EntityId))
                {
                    continue;
                }

                AddBoxSlideFollower(motion.EntityId);
            }
        }

        private void AddBoxSlideFollower(int entityId)
        {
            var desiredState = new AttachedVfxFollowerDesiredState(
                GameplayVfxCueId.From(BoxVfxCue.BoxSlideFollowLoop),
                entityId,
                AttachedVfxFollowerStateKind.BoxSlideFollow,
                entityId,
                Vector3.zero,
                Quaternion.identity,
                AttachedVfxFollowerRetentionPolicy.RetainUntilExplicitStop);

            if (explicitStopKeySet.Contains(desiredState.Key))
            {
                return;
            }

            AddDesiredFollower(desiredState);
        }

        private void AddBoxSlideExplicitStopKeys(
            TickPresentationData presentationData,
            IReadOnlyList<EntityState> finalEntities)
        {
            var stopSignals = presentationData.BoxSlideStopSignals;
            for (var i = 0; i < stopSignals.Count; i++)
            {
                AddBoxSlideExplicitStopKey(stopSignals[i].BoxEntityId);
            }

            foreach (var entityId in removedEntityIds)
            {
                AddBoxSlideExplicitStopKey(entityId);
            }

            if (finalEntities == null)
            {
                return;
            }

            for (var i = 0; i < finalEntities.Count; i++)
            {
                var entity = finalEntities[i];
                if (entity.entityId <= 0 ||
                    entity.type != EntityType.Box)
                {
                    continue;
                }

                if (entity.hp <= 0 ||
                    entity.markedForDeath ||
                    entity.boardPresence == EntityBoardPresence.Detached ||
                    entity.state != EntityPhaseState.Sliding)
                {
                    AddBoxSlideExplicitStopKey(entity.entityId);
                }
            }
        }

        private void AddBoxSlideExplicitStopKey(int entityId)
        {
            if (entityId <= 0)
            {
                return;
            }

            AddExplicitStopKey(
                new AttachedVfxFollowerKey(
                    GameplayVfxCueId.From(BoxVfxCue.BoxSlideFollowLoop),
                    entityId,
                    AttachedVfxFollowerStateKind.BoxSlideFollow,
                    entityId));
        }

        private void AddJumpWindupFollowers(TickPresentationData presentationData)
        {
            var signals = presentationData.EnemyJumpSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.EntityId <= 0 ||
                    removedEntityIds.Contains(signal.EntityId) ||
                    signal.Phase != EnemyJumpPhase.Windup)
                {
                    continue;
                }

                AddDesiredFollower(
                    new AttachedVfxFollowerDesiredState(
                        GameplayVfxCueId.From(EnemyVfxCue.JumperWindupLoop),
                        signal.EntityId,
                        AttachedVfxFollowerStateKind.EnemyJumpWindup,
                        signal.Sequence > 0 ? signal.Sequence : signal.EntityId,
                        Vector3.zero,
                        Quaternion.identity));
            }
        }

        private void AddChargeFollowers(TickPresentationData presentationData)
        {
            var signals = presentationData.EnemyChargeSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.EntityId <= 0 ||
                    removedEntityIds.Contains(signal.EntityId) ||
                    signal.Phase != EnemyChargePhase.Active)
                {
                    continue;
                }

                AddDesiredFollower(
                    new AttachedVfxFollowerDesiredState(
                        GameplayVfxCueId.From(EnemyVfxCue.ChargeBoosterTrail),
                        signal.EntityId,
                        AttachedVfxFollowerStateKind.EnemyChargeActive,
                        signal.Sequence,
                        Vector3.zero,
                        Quaternion.identity));
            }
        }

        private void AddUtilityCooldownAuraFollowers(
            TickPresentationData presentationData,
            IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId)
        {
            if (viewsByEntityId == null)
            {
                return;
            }

            var signals = presentationData.EnemyUtilityCooldownSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.EntityId <= 0 ||
                    signal.CooldownTicksRemaining <= 0 ||
                    removedEntityIds.Contains(signal.EntityId) ||
                    !TryGetUtilityCooldownAuraAuthoring(
                        viewsByEntityId,
                        signal.EntityId,
                        out var authoring) ||
                    !authoring.TryGetUtilityCooldownAura(out var cueId, out var attachPointId) ||
                    signal.Kind != authoring.UtilityKind)
                {
                    continue;
                }

                var sequenceId = signal.ActivationSequence > 0
                    ? (signal.ActivationSequence * 1000) + signal.EffectIndex
                    : signal.EntityId;
                AddDesiredFollower(
                    new AttachedVfxFollowerDesiredState(
                        cueId,
                        signal.EntityId,
                        AttachedVfxFollowerStateKind.EnemyUtilityCooldownAura,
                        sequenceId,
                        Vector3.zero,
                        Quaternion.identity,
                        AttachedVfxFollowerRetentionPolicy.RefreshDesiredOnly,
                        attachPointId));
            }
        }

        private static bool TryGetUtilityCooldownAuraAuthoring(
            IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId,
            int entityId,
            out EnemyUtilityCooldownAuraVfxAuthoring authoring)
        {
            authoring = null;
            return viewsByEntityId.TryGetValue(entityId, out var view) &&
                   view != null &&
                   view.TryGetComponent(out authoring) &&
                   authoring != null;
        }

        private void AddEnemyAttackCooldownFollowers(
            IReadOnlyList<EntityState> finalEntities,
            IReadOnlyDictionary<int, GameplayEntityView> viewsByEntityId)
        {
            if (finalEntities == null ||
                viewsByEntityId == null)
            {
                return;
            }

            for (var i = 0; i < finalEntities.Count; i++)
            {
                var entity = finalEntities[i];
                if (entity.entityId <= 0 ||
                    entity.type != EntityType.Unit ||
                    entity.enemyAttackCooldownTicks <= 0 ||
                    removedEntityIds.Contains(entity.entityId) ||
                    !viewsByEntityId.TryGetValue(entity.entityId, out var view) ||
                    view == null)
                {
                    continue;
                }

                var attachPointId = TryGetForwardCellProjectileVfxAuthoring(view, out var authoring)
                    ? authoring.AttackCooldownAttachPointId
                    : null;
                AddDesiredFollower(
                    new AttachedVfxFollowerDesiredState(
                        GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellAttackCooldownFollow),
                        entity.entityId,
                        AttachedVfxFollowerStateKind.EnemyAttackCooldownFollow,
                        entity.entityId,
                        Vector3.zero,
                        Quaternion.identity,
                        AttachedVfxFollowerRetentionPolicy.RefreshDesiredOnly,
                        attachPointId));
            }
        }

        private static bool TryGetForwardCellProjectileVfxAuthoring(
            GameplayEntityView view,
            out EnemyForwardCellProjectileVfxAuthoring authoring)
        {
            authoring = null;
            return view != null &&
                   view.TryGetComponent(out authoring) &&
                   authoring != null;
        }

        private static bool TryResolveGlideFollowerCue(
            EnemyGlidePhase phase,
            out GameplayVfxCueId cueId,
            out AttachedVfxFollowerStateKind stateKind)
        {
            switch (phase)
            {
                case EnemyGlidePhase.Windup:
                    cueId = GameplayVfxCueId.From(EnemyVfxCue.GlideWindupLoop);
                    stateKind = AttachedVfxFollowerStateKind.EnemyGlideWindup;
                    return true;
                case EnemyGlidePhase.Active:
                    cueId = GameplayVfxCueId.From(EnemyVfxCue.GlideWindTrail);
                    stateKind = AttachedVfxFollowerStateKind.EnemyGlideActive;
                    return true;
                case EnemyGlidePhase.Recovery:
                    cueId = GameplayVfxCueId.From(EnemyVfxCue.GlideRecoverLoop);
                    stateKind = AttachedVfxFollowerStateKind.EnemyGlideRecover;
                    return true;
                default:
                    cueId = default;
                    stateKind = AttachedVfxFollowerStateKind.None;
                    return false;
            }
        }

        private void CollectRemovedEntityIds(TickPresentationData presentationData)
        {
            var exitSignals = presentationData.EntityExitSignals;
            for (var i = 0; i < exitSignals.Count; i++)
            {
                removedEntityIds.Add(exitSignals[i].ExitedEntityId);
            }

            var visibilityChanges = presentationData.VisibilityChanges;
            for (var i = 0; i < visibilityChanges.Count; i++)
            {
                var change = visibilityChanges[i];
                if (change.ChangeKind == TickVisibilityChangeKind.Remove)
                {
                    removedEntityIds.Add(change.EntityId);
                }
            }
        }

        private void AddDesiredFollower(AttachedVfxFollowerDesiredState desiredState)
        {
            if (desiredFollowerKeys.Add(desiredState.Key))
            {
                desiredFollowers.Add(desiredState);
            }
        }

        private void AddExplicitStopKey(AttachedVfxFollowerKey key)
        {
            if (explicitStopKeySet.Add(key))
            {
                explicitStopKeys.Add(key);
            }
        }
    }
}
