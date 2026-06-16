using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Model.Phases;
using UnityEngine;

namespace Game.Feature.Gameplay.Loop
{
    internal static class EntitySpawnMaterializer
    {
        public static EnemyUnitSpawnDefaultsRuntime ResolveSummonSpawnDefaults(
            EnemyUnitArchetypeId archetypeId,
            IReadOnlyDictionary<EnemyUnitArchetypeId, EnemyUnitSpawnDefaultsRuntime> spawnDefaultsByArchetypeId)
        {
            archetypeId.Validate(nameof(archetypeId));
            if (spawnDefaultsByArchetypeId != null &&
                spawnDefaultsByArchetypeId.TryGetValue(archetypeId, out var spawnDefaults))
            {
                return spawnDefaults;
            }

            throw new InvalidOperationException(
                $"Missing enemy unit spawn defaults for archetype '{archetypeId}'.");
        }

        public static EntitySpawnMaterializationResult Materialize(
            WorldSnapshot snapshot,
            in EntitySpawnRequest request,
            EntityIdAllocator entityIdAllocator,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            ISet<SurfaceCell> reservedSpawnCells,
            FinalizationBatch batch,
            List<string> eventLogEntries)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (entityIdAllocator == null)
            {
                throw new ArgumentNullException(nameof(entityIdAllocator));
            }

            if (reservedSpawnCells == null)
            {
                throw new ArgumentNullException(nameof(reservedSpawnCells));
            }

            if (batch == null)
            {
                throw new ArgumentNullException(nameof(batch));
            }

            if (eventLogEntries == null)
            {
                throw new ArgumentNullException(nameof(eventLogEntries));
            }

            if (request.Kind != EntitySpawnRequestKind.Summon)
            {
                throw new ArgumentOutOfRangeException(nameof(request), request.Kind, "Unsupported entity spawn request kind.");
            }

            if (!EntitySpawnPlacementResolver.TrySelectSummonCandidateCell(
                    snapshot,
                    request.Source.OriginCell,
                    request.Source.SourceFacing,
                    request.SpawnDefaults.UnitMobilityKind,
                    request.Summon,
                    reservedSpawnCells,
                    tileFeatureDefinitions,
                    out var spawnCell))
            {
                AppendSummonSkipEvent(eventLogEntries, request, "NoCandidateCell");
                return EntitySpawnMaterializationResult.Skipped();
            }

            var minionHp = request.Summon.OverrideHp
                ? request.Summon.HpOverride
                : request.SpawnDefaults.Hp;
            var summonedEntityState = new SummonedEntityState(
                request.Source.SourceEntityId,
                request.Source.SourceEffectIndex);
            var enemyDefinitionBindingState = new EnemyDefinitionBindingState(request.Summon.SummonedArchetypeId);
            var spawnedEntity = CreateSummonedMinionEntity(
                entityIdAllocator.AllocateEntityId(),
                request.Source.SourceTeamId,
                request.Source.SourceFacing,
                spawnCell,
                minionHp,
                request.SpawnDefaults.InitialAiMode,
                request.SpawnDefaults.UnitMobilityKind,
                request.TickIndex);

            batch.SpawnEntity(
                spawnedEntity,
                new FinalizationOperationMetadata(
                    TickPhase.Resolve,
                    ResolvedActionSemanticKind.None,
                    request.Source.SourceEntityId,
                    actionPlanId: 0,
                    movementExecutionBoundaryKind: MovementExecutionBoundaryKind.SpawnRespawnPlacement,
                    boundaryReason: "EnemyUtilitySummonPlacement"),
                hasSummonedEntityState: true,
                summonedEntityState: summonedEntityState,
                hasEnemyDefinitionBindingState: true,
                enemyDefinitionBindingState: enemyDefinitionBindingState);
            reservedSpawnCells.Add(spawnCell);
            AppendSummonCommittedEvent(eventLogEntries, request, spawnedEntity);
            return EntitySpawnMaterializationResult.Success(spawnedEntity, spawnCell);
        }

        private static EntityState CreateSummonedMinionEntity(
            int entityId,
            int sourceTeamId,
            Direction sourceFacing,
            SurfaceCell spawnCell,
            int minionHp,
            EnemyAiMode initialAiMode,
            UnitMobilityKind unitMobilityKind,
            int tickIndex)
        {
            return new EntityState
            {
                entityId = entityId,
                position = spawnCell,
                hp = minionHp,
                maxHp = minionHp,
                teamId = sourceTeamId,
                type = EntityType.Unit,
                unitRole = UnitRole.Enemy,
                unitMobilityKind = unitMobilityKind,
                state = EntityPhaseState.Idle,
                stateTimer = 0,
                facing = sourceFacing,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = false,
                spawnTick = tickIndex,
                aiMode = initialAiMode,
                aiStateTimer = 0,
                enemyLocomotionCooldownTicks = 0,
                enemyAttackCooldownTicks = 0,
                enemyAttackCooldownTotalTicks = 0,
            };
        }

        private static void AppendSummonCommittedEvent(
            List<string> eventLogEntries,
            in EntitySpawnRequest request,
            in EntityState spawnedEntity)
        {
            eventLogEntries.Add(
                $"SummonCommitted|Source={request.Source.SourceEntityId}|Effect={request.Source.SourceEffectIndex}|SpawnIndex={request.SpawnIndex}|Spawned={spawnedEntity.entityId}|Pos=({spawnedEntity.position.x},{spawnedEntity.position.y})|Archetype={request.Summon.SummonedArchetypeId}|Tick={request.TickIndex}");
        }

        private static void AppendSummonSkipEvent(
            List<string> eventLogEntries,
            in EntitySpawnRequest request,
            string reason)
        {
            eventLogEntries.Add(
                $"SummonSkipped|Source={request.Source.SourceEntityId}|Effect={request.Source.SourceEffectIndex}|SpawnIndex={request.SpawnIndex}|Reason={reason}|Tick={request.TickIndex}");
        }
    }

    internal static class EntitySpawnPlacementResolver
    {
        public static bool TrySelectSummonCandidateCell(
            WorldSnapshot snapshot,
            SurfaceCell originCell,
            Direction sourceFacing,
            UnitMobilityKind summonedUnitMobilityKind,
            in SummonMinionRuntime summonRuntime,
            ISet<SurfaceCell> reservedSpawnCells,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            out SurfaceCell spawnCell)
        {
            var candidateOffsets = BuildCandidateOffsets(sourceFacing);
            var riskActor = CreateSummonedPlacementRiskActor(summonedUnitMobilityKind);
            var hasRiskCandidate = false;
            var riskCandidate = default(SurfaceCell);
            for (var i = 0; i < candidateOffsets.Count; i++)
            {
                var candidateCell = originCell + candidateOffsets[i];
                if (!snapshot.IsInsideBoard(candidateCell))
                {
                    continue;
                }

                if (summonRuntime.RequireNoSolidAtSpawnCell &&
                    snapshot.TryGetSolidSemanticAt(candidateCell, out _))
                {
                    continue;
                }

                if (TileFeatureMovementBlockerQuery.TryGetActiveBarricadeBlocker(
                        snapshot,
                        tileFeatureDefinitions,
                        candidateCell,
                        TileFeatureBlockerSubject.Unit,
                        TileFeatureMovementKind.UnitPlacement,
                        out _))
                {
                    continue;
                }

                if (snapshot.TryGetPlacementBlocker(EntityType.Unit, candidateCell, ignoredEntityId: 0, out _))
                {
                    continue;
                }

                if (summonRuntime.RequireNoUnitAtSpawnCell &&
                    snapshot.HasAnyUnitAt(candidateCell))
                {
                    continue;
                }

                if (reservedSpawnCells.Contains(candidateCell))
                {
                    continue;
                }

                if (TileFeatureHazardQueries.EvaluateTileApproachRisk(
                        snapshot,
                        tileFeatureDefinitions,
                        riskActor,
                        candidateCell) != TileApproachRisk.Neutral)
                {
                    if (!hasRiskCandidate)
                    {
                        riskCandidate = candidateCell;
                        hasRiskCandidate = true;
                    }

                    continue;
                }

                spawnCell = candidateCell;
                return true;
            }

            if (hasRiskCandidate)
            {
                spawnCell = riskCandidate;
                return true;
            }

            spawnCell = default;
            return false;
        }

        private static EntityState CreateSummonedPlacementRiskActor(UnitMobilityKind summonedUnitMobilityKind)
        {
            return new EntityState
            {
                entityId = 0,
                type = EntityType.Unit,
                unitMobilityKind = summonedUnitMobilityKind,
                boardPresence = EntityBoardPresence.Occupying,
                hp = 1,
                markedForDeath = false,
            };
        }

        private static List<Vector2Int> BuildCandidateOffsets(Direction facing)
        {
            if (!EnemyMovementStrategyShared.TryResolveDelta(facing, out var forward) ||
                !EnemyMovementStrategyShared.TryResolveDelta(TurnRight(facing), out var right) ||
                !EnemyMovementStrategyShared.TryResolveDelta(TurnLeft(facing), out var left) ||
                !EnemyMovementStrategyShared.TryResolveDelta(TurnBack(facing), out var back))
            {
                return new List<Vector2Int>
                {
                    new Vector2Int(0, 1),
                    new Vector2Int(1, 0),
                    new Vector2Int(-1, 0),
                    new Vector2Int(0, -1),
                };
            }

            return new List<Vector2Int>
            {
                forward,
                right,
                left,
                back,
            };
        }

        private static Direction TurnRight(Direction direction)
        {
            return direction switch
            {
                Direction.Up => Direction.Right,
                Direction.Right => Direction.Down,
                Direction.Down => Direction.Left,
                Direction.Left => Direction.Up,
                _ => Direction.None,
            };
        }

        private static Direction TurnLeft(Direction direction)
        {
            return direction switch
            {
                Direction.Up => Direction.Left,
                Direction.Left => Direction.Down,
                Direction.Down => Direction.Right,
                Direction.Right => Direction.Up,
                _ => Direction.None,
            };
        }

        private static Direction TurnBack(Direction direction)
        {
            return direction switch
            {
                Direction.Up => Direction.Down,
                Direction.Right => Direction.Left,
                Direction.Down => Direction.Up,
                Direction.Left => Direction.Right,
                _ => Direction.None,
            };
        }
    }
}
