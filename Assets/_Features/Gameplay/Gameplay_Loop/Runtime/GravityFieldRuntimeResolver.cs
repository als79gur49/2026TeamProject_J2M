using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Model.Phases;

namespace Game.Feature.Gameplay.Loop
{
    public static class GravityFieldRuntimePolicy
    {
        public const float ChargeDurationSeconds = 8f;
        public const float ActiveDurationSeconds = 3f;
    }

    internal static class GravityFieldRuntimeResolver
    {
        public static void ResolvePreMovement(
            WorldSnapshot snapshot,
            int tickIndex,
            int chargeTicks,
            int activeTicks,
            FinalizationBatch batch,
            List<string> eventLogEntries)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (batch == null)
            {
                throw new ArgumentNullException(nameof(batch));
            }

            if (eventLogEntries == null)
            {
                throw new ArgumentNullException(nameof(eventLogEntries));
            }

            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);
            var plannedLocksByBoxEntityId = new Dictionary<int, BoxInteractionLockState>();
            for (var i = 0; i < entities.Count; i++)
            {
                var emitter = entities[i];
                if (emitter.type != EntityType.Box ||
                    emitter.boxArchetype != BoxArchetype.GravityField)
                {
                    continue;
                }

                ResolveEmitter(
                    snapshot,
                    emitter,
                    tickIndex,
                    chargeTicks,
                    activeTicks,
                    batch,
                    eventLogEntries,
                    plannedLocksByBoxEntityId);
            }

            if (plannedLocksByBoxEntityId.Count == 0)
            {
                return;
            }

            var orderedTargetIds = new List<int>(plannedLocksByBoxEntityId.Keys);
            orderedTargetIds.Sort();
            for (var i = 0; i < orderedTargetIds.Count; i++)
            {
                var targetId = orderedTargetIds[i];
                var state = plannedLocksByBoxEntityId[targetId];
                if (snapshot.TryGetActiveBoxInteractionLockState(targetId, tickIndex, out var existing) &&
                    BoxInteractionLockMerge.AreEqual(existing, state))
                {
                    continue;
                }

                batch.SetBoxInteractionLockState(
                    targetId,
                    state,
                    new FinalizationOperationMetadata(
                        TickPhase.Plan,
                        ResolvedActionSemanticKind.None,
                        state.SourceEntityId,
                        actionPlanId: 0));
                eventLogEntries.Add(
                    $"GravityFieldLockApplied|Source={state.SourceEntityId}|Box={targetId}|Expires={state.ExpiresTickExclusive}|BlocksPush={(state.BlocksPush ? 1 : 0)}|BlocksFlip={(state.BlocksFlip ? 1 : 0)}|BlocksDestroy={(state.BlocksDestroy ? 1 : 0)}");
            }
        }

        private static void ResolveEmitter(
            WorldSnapshot snapshot,
            EntityState emitter,
            int tickIndex,
            int chargeTicks,
            int activeTicks,
            FinalizationBatch batch,
            List<string> eventLogEntries,
            IDictionary<int, BoxInteractionLockState> plannedLocksByBoxEntityId)
        {
            if (!IsEligibleEmitter(snapshot, emitter))
            {
                SetEmitterStateIfChanged(batch, eventLogEntries, emitter, GravityFieldPhase.Charging, chargeTicks);
                return;
            }

            var nextPhase = emitter.gravityFieldPhase;
            var nextTimer = Math.Max(0, emitter.gravityFieldTimerTicks);
            var applyActiveField = false;
            switch (emitter.gravityFieldPhase)
            {
                case GravityFieldPhase.Charging:
                    if (nextTimer <= 1)
                    {
                        nextPhase = GravityFieldPhase.Active;
                        nextTimer = activeTicks;
                        applyActiveField = true;
                    }
                    else
                    {
                        nextTimer--;
                    }

                    break;

                case GravityFieldPhase.Active:
                    if (nextTimer <= 1)
                    {
                        nextPhase = GravityFieldPhase.Charging;
                        nextTimer = chargeTicks;
                    }
                    else
                    {
                        nextTimer--;
                        applyActiveField = true;
                    }

                    break;

                default:
                    nextPhase = GravityFieldPhase.Charging;
                    nextTimer = chargeTicks;
                    break;
            }

            SetEmitterStateIfChanged(batch, eventLogEntries, emitter, nextPhase, nextTimer);
            if (applyActiveField)
            {
                ApplyActiveField(snapshot, emitter, tickIndex, plannedLocksByBoxEntityId);
            }
        }

        private static bool IsEligibleEmitter(WorldSnapshot snapshot, in EntityState entity)
        {
            return entity.type == EntityType.Box &&
                   entity.boxArchetype == BoxArchetype.GravityField &&
                   entity.boardPresence == EntityBoardPresence.Occupying &&
                   entity.hp > 0 &&
                   !entity.markedForDeath &&
                   entity.state != EntityPhaseState.Sliding &&
                   !HasActivePhasedState(snapshot, entity.entityId) &&
                   snapshot.Topology.IsFaceActive(entity.position.face);
        }

        private static void ApplyActiveField(
            WorldSnapshot snapshot,
            in EntityState emitter,
            int tickIndex,
            IDictionary<int, BoxInteractionLockState> plannedLocksByBoxEntityId)
        {
            var targetEntityIds = new HashSet<int>();
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    var cell = new SurfaceCell(emitter.position.face, emitter.position.x + dx, emitter.position.y + dy);
                    if (!snapshot.IsInsideBoard(cell) ||
                        !snapshot.TryGetSolidSemanticAt(cell, out var semantic) ||
                        semantic.Kind != SolidKind.Box ||
                        !IsEligibleTarget(snapshot, semantic.Entity))
                    {
                        continue;
                    }

                    targetEntityIds.Add(semantic.Entity.entityId);
                }
            }

            var newState = new BoxInteractionLockState(
                emitter.entityId,
                sourceEffectIndex: 0,
                expiresTickExclusive: tickIndex + 1,
                blocksPush: true,
                blocksFlip: true,
                blocksDestroy: true,
                BoxInteractionLockSourceReason.GravityField);

            var orderedTargetIds = new List<int>(targetEntityIds);
            orderedTargetIds.Sort();
            for (var i = 0; i < orderedTargetIds.Count; i++)
            {
                var targetId = orderedTargetIds[i];
                var hasPlanned = plannedLocksByBoxEntityId.TryGetValue(targetId, out var planned);
                var hasExisting = snapshot.TryGetActiveBoxInteractionLockState(targetId, tickIndex, out var existing);
                var merged = !hasPlanned && !hasExisting
                    ? newState
                    : BoxInteractionLockMerge.Merge(hasPlanned ? planned : existing, newState);
                plannedLocksByBoxEntityId[targetId] = merged;
            }
        }

        private static bool IsEligibleTarget(WorldSnapshot snapshot, in EntityState entity)
        {
            if (entity.type != EntityType.Box ||
                entity.boardPresence != EntityBoardPresence.Occupying ||
                entity.hp <= 0 ||
                entity.markedForDeath ||
                entity.state == EntityPhaseState.Sliding ||
                HasActivePhasedState(snapshot, entity.entityId))
            {
                return false;
            }

            if (!snapshot.TryGetResolvedSpatialState(entity.entityId, out var spatialState))
            {
                return false;
            }

            return spatialState.Kind == SpatialState.Anchored &&
                   spatialState.ClaimsAuthoritativeOccupancy &&
                   spatialState.IsGameplayVisible;
        }

        private static bool HasActivePhasedState(WorldSnapshot snapshot, int entityId)
        {
            return snapshot.TryGetPhasedState(entityId, out var phasedState) &&
                   phasedState.IsActive;
        }

        private static void SetEmitterStateIfChanged(
            FinalizationBatch batch,
            List<string> eventLogEntries,
            in EntityState emitter,
            GravityFieldPhase phase,
            int timerTicks)
        {
            if (emitter.gravityFieldPhase == phase &&
                emitter.gravityFieldTimerTicks == timerTicks)
            {
                return;
            }

            batch.SetGravityFieldState(
                emitter.entityId,
                phase,
                timerTicks,
                new FinalizationOperationMetadata(
                    TickPhase.Plan,
                    ResolvedActionSemanticKind.None,
                    emitter.entityId,
                    actionPlanId: 0));
            eventLogEntries.Add(
                $"GravityFieldStateUpdated|Source={emitter.entityId}|Phase={phase}|Timer={timerTicks}");
        }
    }

    internal static class BoxInteractionLockMerge
    {
        public static BoxInteractionLockState Merge(
            in BoxInteractionLockState existingState,
            in BoxInteractionLockState newState)
        {
            var mergedExpires = Math.Max(existingState.ExpiresTickExclusive, newState.ExpiresTickExclusive);
            var useNewSource =
                newState.ExpiresTickExclusive > existingState.ExpiresTickExclusive ||
                (newState.ExpiresTickExclusive == existingState.ExpiresTickExclusive &&
                 (newState.SourceEntityId < existingState.SourceEntityId ||
                  (newState.SourceEntityId == existingState.SourceEntityId &&
                   (newState.SourceEffectIndex < existingState.SourceEffectIndex ||
                    (newState.SourceEffectIndex == existingState.SourceEffectIndex &&
                     (int)newState.SourceReason < (int)existingState.SourceReason)))));

            return new BoxInteractionLockState(
                useNewSource ? newState.SourceEntityId : existingState.SourceEntityId,
                useNewSource ? newState.SourceEffectIndex : existingState.SourceEffectIndex,
                mergedExpires,
                existingState.BlocksPush || newState.BlocksPush,
                existingState.BlocksFlip || newState.BlocksFlip,
                existingState.BlocksDestroy || newState.BlocksDestroy,
                useNewSource ? newState.SourceReason : existingState.SourceReason);
        }

        public static bool AreEqual(
            in BoxInteractionLockState left,
            in BoxInteractionLockState right)
        {
            return left.SourceEntityId == right.SourceEntityId &&
                   left.SourceEffectIndex == right.SourceEffectIndex &&
                   left.ExpiresTickExclusive == right.ExpiresTickExclusive &&
                   left.BlocksPush == right.BlocksPush &&
                   left.BlocksFlip == right.BlocksFlip &&
                   left.BlocksDestroy == right.BlocksDestroy &&
                   left.SourceReason == right.SourceReason;
        }
    }
}
