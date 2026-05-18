using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Commit;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Expansion;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.Attack.Sorting;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Cleanup;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Model.Sorting;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Commit;
using Game.Feature.Gameplay.Movement.Expansion;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.Movement.Sorting;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.Loop
{
    internal sealed class RespawnProcessor
    {
        private readonly Dictionary<int, PlayerRespawnDelayState> _respawnDelayStatesByEntityId = new();

        public RespawnPhaseResult Process(
            WorldSnapshot tickStartSnapshot,
            WorldSnapshot postCleanupSnapshot,
            CleanupPhaseResult cleanupPhaseResult,
            IReadOnlyList<EntityState> respawnTemplates,
            int tickIndex,
            int respawnDelayTicks,
            bool allowRespawn,
            IWorldWriteContext writeContext)
        {
            if (tickStartSnapshot == null)
            {
                throw new ArgumentNullException(nameof(tickStartSnapshot));
            }

            if (postCleanupSnapshot == null)
            {
                throw new ArgumentNullException(nameof(postCleanupSnapshot));
            }

            if (cleanupPhaseResult == null)
            {
                throw new ArgumentNullException(nameof(cleanupPhaseResult));
            }

            if (respawnTemplates == null)
            {
                throw new ArgumentNullException(nameof(respawnTemplates));
            }

            if (respawnDelayTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(respawnDelayTicks),
                    "Respawn delay ticks must be greater than zero.");
            }

            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            var respawnedEntities = new List<EntityState>();
            var eventLogEntries = new List<string>();
            var delayRecords = new List<PlayerRespawnDelayRecord>();
            var placementRecords = new List<RespawnPlacementRecord>();
            RespawnTopologyResetRequest? topologyResetRequest = null;
            var removedEntityIdsThisTick = cleanupPhaseResult.RemovedEntityIds.Count > 0
                ? new HashSet<int>(cleanupPhaseResult.RemovedEntityIds)
                : null;

            for (var i = 0; i < respawnTemplates.Count; i++)
            {
                var template = respawnTemplates[i];
                var entityId = template.entityId;
                var existedAtTickStart = tickStartSnapshot.TryGetEntity(entityId, out _);
                var existsAfterCleanup = postCleanupSnapshot.TryGetEntity(entityId, out _);
                var removedThisTick = removedEntityIdsThisTick != null &&
                    removedEntityIdsThisTick.Contains(entityId);

                if (existsAfterCleanup)
                {
                    _respawnDelayStatesByEntityId.Remove(entityId);
                    continue;
                }

                if (removedThisTick)
                {
                    var startedState = new PlayerRespawnDelayState(
                        tickIndex,
                        tickIndex + respawnDelayTicks,
                        respawnDelayTicks,
                        elapsedEventEmitted: false);
                    _respawnDelayStatesByEntityId[entityId] = startedState;
                    delayRecords.Add(CreateDelayRecord(entityId, startedState, tickIndex, startedThisTick: true, elapsedThisTick: false));
                    eventLogEntries.Add(
                        $"PlayerRespawnDelayStarted|E={entityId}|StartTick={startedState.StartTick}|EligibleTick={startedState.EligibleTick}|DelayTicks={startedState.DelayTicks}");
                }
                else if (existedAtTickStart)
                {
                    _respawnDelayStatesByEntityId.Remove(entityId);
                    continue;
                }
                else if (!_respawnDelayStatesByEntityId.ContainsKey(entityId))
                {
                    _respawnDelayStatesByEntityId[entityId] = new PlayerRespawnDelayState(
                        tickIndex,
                        tickIndex,
                        delayTicks: 0,
                        elapsedEventEmitted: false);
                }

                var delayState = _respawnDelayStatesByEntityId[entityId];
                if (tickIndex < delayState.EligibleTick)
                {
                    if (!removedThisTick)
                    {
                        delayRecords.Add(CreateDelayRecord(entityId, delayState, tickIndex, startedThisTick: false, elapsedThisTick: false));
                        eventLogEntries.Add(
                            $"PlayerRespawnDelayTicking|E={entityId}|StartTick={delayState.StartTick}|EligibleTick={delayState.EligibleTick}|RemainingTicks={Math.Max(0, delayState.EligibleTick - tickIndex)}|Tick={tickIndex}");
                    }

                    continue;
                }

                if (!delayState.ElapsedEventEmitted)
                {
                    delayState = delayState.WithElapsedEventEmitted();
                    _respawnDelayStatesByEntityId[entityId] = delayState;
                    delayRecords.Add(CreateDelayRecord(entityId, delayState, tickIndex, startedThisTick: false, elapsedThisTick: true));
                    eventLogEntries.Add(
                        $"PlayerRespawnDelayElapsed|E={entityId}|StartTick={delayState.StartTick}|EligibleTick={delayState.EligibleTick}|Tick={tickIndex}");
                }

                if (!allowRespawn)
                {
                    _respawnDelayStatesByEntityId.Remove(entityId);
                    eventLogEntries.Add($"RespawnSuppressed|E={entityId}|Reason=PolicyDisabled|Tick={tickIndex}");
                    continue;
                }

                var respawnEntity = BuildRespawnEntity(template, tickIndex);
                if (postCleanupSnapshot.Topology.BottomFace != respawnEntity.position.face)
                {
                    if (!TryResolveRespawnTopologyReset(
                            postCleanupSnapshot.Topology,
                            respawnEntity.position.face,
                            out var resetTopology,
                            out var rotationKind))
                    {
                        throw new InvalidOperationException(
                            $"Respawn topology reset could not resolve a bottom-face topology step for entity {respawnEntity.entityId} on face {respawnEntity.position.face} from {postCleanupSnapshot.Topology}.");
                    }

                    writeContext.SetTopology(resetTopology);
                    topologyResetRequest = new RespawnTopologyResetRequest(
                        respawnEntity.entityId,
                        respawnEntity.position.face,
                        postCleanupSnapshot.Topology,
                        resetTopology,
                        rotationKind);
                    eventLogEntries.Add(FormatRespawnDeferredEvent(respawnEntity, tickIndex));
                    eventLogEntries.Add(
                        FormatRespawnTopologyResetRequestedEvent(
                            respawnEntity,
                            postCleanupSnapshot.Topology,
                            resetTopology,
                            rotationKind,
                            tickIndex));
                    continue;
                }

                var respawnLegality = RuntimePlacementValidityPolicy.EvaluateAuthoritativePlacement(
                    postCleanupSnapshot,
                    respawnEntity.type,
                    respawnEntity.position,
                    ignoredEntityId: 0);
                if (respawnLegality.Verdict == LegalityVerdict.Blocked)
                {
                    eventLogEntries.Add(FormatRespawnSkippedEvent(respawnEntity, respawnLegality, tickIndex));
                    continue;
                }

                writeContext.SpawnEntity(respawnEntity);
                writeContext.SetPlayerControlState(respawnEntity.entityId, default);
                writeContext.SetPlayerDamageState(respawnEntity.entityId, default);
                respawnedEntities.Add(respawnEntity);
                placementRecords.Add(
                    new RespawnPlacementRecord(
                        respawnEntity.entityId,
                        respawnEntity.position,
                        MovementExecutionBoundaryKind.SpawnRespawnPlacement,
                        "PlayerRespawnPlacement"));
                _respawnDelayStatesByEntityId.Remove(entityId);
                eventLogEntries.Add(
                    $"RespawnCommitted|E={respawnEntity.entityId}|Pos=({respawnEntity.position.x},{respawnEntity.position.y})|Face={respawnEntity.position.face}|Facing={respawnEntity.facing}|Tick={tickIndex}");
            }

            return new RespawnPhaseResult(
                respawnedEntities,
                eventLogEntries,
                delayRecords,
                placementRecords,
                topologyResetRequest);
        }

        private static PlayerRespawnDelayRecord CreateDelayRecord(
            int entityId,
            PlayerRespawnDelayState state,
            int currentTick,
            bool startedThisTick,
            bool elapsedThisTick)
        {
            return new PlayerRespawnDelayRecord(
                entityId,
                state.StartTick,
                state.EligibleTick,
                state.DelayTicks,
                currentTick,
                startedThisTick,
                elapsedThisTick);
        }

        private static EntityState BuildRespawnEntity(EntityState template, int tickIndex)
        {
            var maxHp = template.maxHp > 0 ? template.maxHp : template.hp;
            var respawnEntity = template;
            respawnEntity.hp = maxHp;
            respawnEntity.maxHp = maxHp;
            respawnEntity.state = EntityPhaseState.Idle;
            respawnEntity.stateTimer = 0;
            respawnEntity.boardPresence = EntityBoardPresence.Occupying;
            respawnEntity.markedForDeath = false;
            respawnEntity.spawnTick = tickIndex;
            respawnEntity.kineticInstigatorEntityId = 0;
            respawnEntity.kineticInstigatorTeamId = 0;
            respawnEntity.aiStateTimer = 0;
            respawnEntity.enemyLocomotionCooldownTicks = 0;
            respawnEntity.enemyAttackCooldownTicks = 0;
            respawnEntity.enemyAttackCooldownTotalTicks = 0;
            return respawnEntity;
        }

        private readonly struct PlayerRespawnDelayState
        {
            public PlayerRespawnDelayState(
                int startTick,
                int eligibleTick,
                int delayTicks,
                bool elapsedEventEmitted)
            {
                StartTick = startTick;
                EligibleTick = eligibleTick;
                DelayTicks = delayTicks;
                ElapsedEventEmitted = elapsedEventEmitted;
            }

            public int StartTick { get; }

            public int EligibleTick { get; }

            public int DelayTicks { get; }

            public bool ElapsedEventEmitted { get; }

            public PlayerRespawnDelayState WithElapsedEventEmitted()
            {
                return new PlayerRespawnDelayState(
                    StartTick,
                    EligibleTick,
                    DelayTicks,
                    elapsedEventEmitted: true);
            }
        }

        private static string FormatRespawnSkippedEvent(
            EntityState entity,
            LegalityResult legality,
            int tickIndex)
        {
            return LegalityDiagnosticsFormatter.FormatRespawnSkippedEvent(entity, legality, tickIndex);
        }

        private static string FormatRespawnDeferredEvent(EntityState entity, int tickIndex)
        {
            return
                $"RespawnDeferred|E={entity.entityId}|Reason=TopologyResetRequired|TargetFace={entity.position.face}|Tick={tickIndex}";
        }

        private static string FormatRespawnTopologyResetRequestedEvent(
            EntityState entity,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            CubeRotationKind rotationKind,
            int tickIndex)
        {
            return
                $"RespawnTopologyResetRequested|E={entity.entityId}|From={sourceTopology.BottomFace}|To={destinationTopology.BottomFace}|Rotation={rotationKind}|TargetFace={entity.position.face}|Tick={tickIndex}";
        }

        private static bool TryResolveRespawnTopologyReset(
            CubeTopologyState currentTopology,
            FaceId targetFace,
            out CubeTopologyState resetTopology,
            out CubeRotationKind rotationKind)
        {
            var forwardTopology = currentTopology.Rotate(CubeRotationKind.Forward);
            if (forwardTopology.BottomFace == targetFace)
            {
                resetTopology = forwardTopology;
                rotationKind = CubeRotationKind.Forward;
                return true;
            }

            var backwardTopology = currentTopology.Rotate(CubeRotationKind.Backward);
            if (backwardTopology.BottomFace == targetFace)
            {
                resetTopology = backwardTopology;
                rotationKind = CubeRotationKind.Backward;
                return true;
            }

            resetTopology = forwardTopology;
            rotationKind = CubeRotationKind.Forward;
            return true;
        }
    }

}
