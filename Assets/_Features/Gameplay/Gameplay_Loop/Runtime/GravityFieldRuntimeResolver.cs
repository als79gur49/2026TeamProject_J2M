using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    internal static class GravityFieldAreaPolicy
    {
        public static GravityFieldAreaFootprint BuildFootprint(WorldSnapshot snapshot, SurfaceCell emitterCell)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var areaCells = new List<SurfaceCell>(GravityFieldAreaFootprint.SlotCount);
            var slotVisibilityMask = 0;
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var slotIndex = ToSlotIndex(dx, dy);
                    var cell = new SurfaceCell(emitterCell.face, emitterCell.x + dx, emitterCell.y + dy);
                    if (!snapshot.IsInsideBoard(cell))
                    {
                        continue;
                    }

                    areaCells.Add(cell);
                    slotVisibilityMask |= 1 << slotIndex;
                }
            }

            return areaCells.Count == 0
                ? GravityFieldAreaFootprint.Empty
                : new GravityFieldAreaFootprint(areaCells, slotVisibilityMask);
        }

        public static int ToSlotIndex(int dx, int dy)
        {
            return (dy + 1) * 3 + (dx + 1);
        }
    }

    internal readonly struct GravityFieldRuntimeResolverResult
    {
        public GravityFieldRuntimeResolverResult(
            FinalizationBatch batch,
            IReadOnlyList<string> eventLogEntries,
            IReadOnlyList<GravityFieldPresentationEvent> presentationEvents,
            IReadOnlyList<GravityFieldLockedTargetFact> lockedTargetFacts = null)
        {
            Batch = batch ?? throw new ArgumentNullException(nameof(batch));
            EventLogEntries = new ReadOnlyCollection<string>(
                new List<string>(eventLogEntries ?? Array.Empty<string>()));
            PresentationEvents = new ReadOnlyCollection<GravityFieldPresentationEvent>(
                new List<GravityFieldPresentationEvent>(
                    presentationEvents ?? Array.Empty<GravityFieldPresentationEvent>()));
            LockedTargetFacts = new ReadOnlyCollection<GravityFieldLockedTargetFact>(
                new List<GravityFieldLockedTargetFact>(
                    lockedTargetFacts ?? Array.Empty<GravityFieldLockedTargetFact>()));
        }

        public FinalizationBatch Batch { get; }

        public IReadOnlyList<string> EventLogEntries { get; }

        public IReadOnlyList<GravityFieldPresentationEvent> PresentationEvents { get; }

        public IReadOnlyList<GravityFieldLockedTargetFact> LockedTargetFacts { get; }
    }

    internal readonly struct GravityFieldLockedTargetFact
    {
        public GravityFieldLockedTargetFact(
            int emitterEntityId,
            int targetEntityId,
            SurfaceCell emitterCell,
            SurfaceCell targetCell = default)
        {
            EmitterEntityId = emitterEntityId;
            TargetEntityId = targetEntityId;
            EmitterCell = emitterCell;
            TargetCell = targetCell;
        }

        public int EmitterEntityId { get; }

        public int TargetEntityId { get; }

        public SurfaceCell EmitterCell { get; }

        public SurfaceCell TargetCell { get; }
    }

    internal sealed class GravityFieldLockedBoxOneShotState
    {
        private readonly Dictionary<int, HashSet<int>> _emittedTargetIdsByEmitterId = new();
        private readonly List<int> _removeBuffer = new();

        public void BeginActiveWindow(int emitterEntityId)
        {
            if (emitterEntityId <= 0)
            {
                return;
            }

            _emittedTargetIdsByEmitterId[emitterEntityId] = new HashSet<int>();
        }

        public void EnsureActiveWindow(int emitterEntityId)
        {
            if (emitterEntityId <= 0 ||
                _emittedTargetIdsByEmitterId.ContainsKey(emitterEntityId))
            {
                return;
            }

            _emittedTargetIdsByEmitterId.Add(emitterEntityId, new HashSet<int>());
        }

        public void EndActiveWindow(int emitterEntityId)
        {
            if (emitterEntityId > 0)
            {
                _emittedTargetIdsByEmitterId.Remove(emitterEntityId);
            }
        }

        public bool TryMarkLockedBoxEmitted(int emitterEntityId, int targetEntityId)
        {
            if (emitterEntityId <= 0 ||
                targetEntityId <= 0)
            {
                return false;
            }

            EnsureActiveWindow(emitterEntityId);
            return _emittedTargetIdsByEmitterId[emitterEntityId].Add(targetEntityId);
        }

        public void PruneMissingEmitters(ISet<int> observedGravityFieldEmitterIds)
        {
            if (observedGravityFieldEmitterIds == null ||
                _emittedTargetIdsByEmitterId.Count == 0)
            {
                return;
            }

            _removeBuffer.Clear();
            foreach (var pair in _emittedTargetIdsByEmitterId)
            {
                if (!observedGravityFieldEmitterIds.Contains(pair.Key))
                {
                    _removeBuffer.Add(pair.Key);
                }
            }

            for (var i = 0; i < _removeBuffer.Count; i++)
            {
                _emittedTargetIdsByEmitterId.Remove(_removeBuffer[i]);
            }

            _removeBuffer.Clear();
        }
    }

    internal static class GravityFieldRuntimeResolver
    {
        public static GravityFieldRuntimeResolverResult ResolvePreMovement(
            WorldSnapshot snapshot,
            int tickIndex,
            int chargeTicks,
            int activeTicks,
            GravityFieldLockedBoxOneShotState lockedBoxOneShotState = null)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var batch = new FinalizationBatch();
            var eventLogEntries = new List<string>();
            var presentationEvents = new List<GravityFieldPresentationEvent>();
            var lockedTargetFacts = new List<GravityFieldLockedTargetFact>();
            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);
            var observedGravityFieldEmitterIds = lockedBoxOneShotState != null
                ? new HashSet<int>()
                : null;
            var plannedLocksByBoxEntityId = new Dictionary<int, BoxInteractionLockState>();
            for (var i = 0; i < entities.Count; i++)
            {
                var emitter = entities[i];
                if (emitter.type != EntityType.Box ||
                    emitter.boxArchetype != BoxArchetype.GravityField)
                {
                    continue;
                }

                observedGravityFieldEmitterIds?.Add(emitter.entityId);
                ResolveEmitter(
                    snapshot,
                    emitter,
                    tickIndex,
                    chargeTicks,
                    activeTicks,
                    batch,
                    eventLogEntries,
                    presentationEvents,
                    lockedTargetFacts,
                    plannedLocksByBoxEntityId,
                    lockedBoxOneShotState);
            }

            lockedBoxOneShotState?.PruneMissingEmitters(observedGravityFieldEmitterIds);
            if (plannedLocksByBoxEntityId.Count == 0)
            {
                return new GravityFieldRuntimeResolverResult(batch, eventLogEntries, presentationEvents, lockedTargetFacts);
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

            return new GravityFieldRuntimeResolverResult(batch, eventLogEntries, presentationEvents, lockedTargetFacts);
        }

        private static void ResolveEmitter(
            WorldSnapshot snapshot,
            EntityState emitter,
            int tickIndex,
            int chargeTicks,
            int activeTicks,
            FinalizationBatch batch,
            List<string> eventLogEntries,
            List<GravityFieldPresentationEvent> presentationEvents,
            List<GravityFieldLockedTargetFact> lockedTargetFacts,
            IDictionary<int, BoxInteractionLockState> plannedLocksByBoxEntityId,
            GravityFieldLockedBoxOneShotState lockedBoxOneShotState)
        {
            if (!IsEligibleEmitter(snapshot, emitter))
            {
                lockedBoxOneShotState?.EndActiveWindow(emitter.entityId);
                SetEmitterStateIfChanged(batch, eventLogEntries, emitter, GravityFieldPhase.Charging, chargeTicks);
                return;
            }

            var nextPhase = emitter.gravityFieldPhase;
            var nextTimer = Math.Max(0, emitter.gravityFieldTimerTicks);
            var applyActiveField = false;
            var presentationEventKind = GravityFieldPresentationEventKind.None;
            switch (emitter.gravityFieldPhase)
            {
                case GravityFieldPhase.Charging:
                    if (nextTimer <= 1)
                    {
                        nextPhase = GravityFieldPhase.Active;
                        nextTimer = activeTicks;
                        applyActiveField = true;
                        presentationEventKind = GravityFieldPresentationEventKind.Activated;
                        lockedBoxOneShotState?.BeginActiveWindow(emitter.entityId);
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
                        presentationEventKind = GravityFieldPresentationEventKind.Expired;
                        lockedBoxOneShotState?.EndActiveWindow(emitter.entityId);
                    }
                    else
                    {
                        nextTimer--;
                        applyActiveField = true;
                        lockedBoxOneShotState?.EnsureActiveWindow(emitter.entityId);
                    }

                    break;

                default:
                    nextPhase = GravityFieldPhase.Charging;
                    nextTimer = chargeTicks;
                    lockedBoxOneShotState?.EndActiveWindow(emitter.entityId);
                    break;
            }

            var stateChanged = SetEmitterStateIfChanged(batch, eventLogEntries, emitter, nextPhase, nextTimer);
            if (stateChanged &&
                presentationEventKind != GravityFieldPresentationEventKind.None)
            {
                presentationEvents.Add(new GravityFieldPresentationEvent(
                    presentationEventKind,
                    emitter.entityId,
                    emitter.position));
            }

            if (applyActiveField)
            {
                ApplyActiveField(
                    snapshot,
                    emitter,
                    tickIndex,
                    lockedTargetFacts,
                    plannedLocksByBoxEntityId,
                    presentationEvents,
                    lockedBoxOneShotState);
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
            List<GravityFieldLockedTargetFact> lockedTargetFacts,
            IDictionary<int, BoxInteractionLockState> plannedLocksByBoxEntityId,
            List<GravityFieldPresentationEvent> presentationEvents,
            GravityFieldLockedBoxOneShotState lockedBoxOneShotState)
        {
            var targetEntityIds = new HashSet<int>();
            var targetCellsByEntityId = new Dictionary<int, SurfaceCell>();
            var footprint = GravityFieldAreaPolicy.BuildFootprint(snapshot, emitter.position);
            for (var i = 0; i < footprint.AreaCells.Count; i++)
            {
                var cell = footprint.AreaCells[i];
                if (!snapshot.TryGetSolidSemanticAt(cell, out var semantic) ||
                    semantic.Kind != SolidKind.Box ||
                    !IsEligibleTarget(snapshot, semantic.Entity))
                {
                    continue;
                }

                if (targetEntityIds.Add(semantic.Entity.entityId))
                {
                    targetCellsByEntityId.Add(semantic.Entity.entityId, semantic.Entity.position);
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
                var targetCell = targetCellsByEntityId.TryGetValue(targetId, out var resolvedTargetCell)
                    ? resolvedTargetCell
                    : default;
                lockedTargetFacts.Add(new GravityFieldLockedTargetFact(
                    emitter.entityId,
                    targetId,
                    emitter.position,
                    targetCell));
                if (lockedBoxOneShotState != null &&
                    lockedBoxOneShotState.TryMarkLockedBoxEmitted(emitter.entityId, targetId))
                {
                    var payload = new GravityFieldLockedBoxPayload(
                        emitter.entityId,
                        targetId,
                        emitter.position,
                        targetCell);
                    presentationEvents.Add(new GravityFieldPresentationEvent(
                        GravityFieldPresentationEventKind.LockedBox,
                        emitter.entityId,
                        emitter.position,
                        targetId,
                        payload));
                }

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

        private static bool SetEmitterStateIfChanged(
            FinalizationBatch batch,
            List<string> eventLogEntries,
            in EntityState emitter,
            GravityFieldPhase phase,
            int timerTicks)
        {
            if (emitter.gravityFieldPhase == phase &&
                emitter.gravityFieldTimerTicks == timerTicks)
            {
                return false;
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
            return true;
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
