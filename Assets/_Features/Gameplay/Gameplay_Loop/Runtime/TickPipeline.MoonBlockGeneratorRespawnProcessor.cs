using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Loop
{
    internal sealed class MoonBlockGeneratorRespawnProcessor
    {
        public IReadOnlyList<string> Process(
            WorldSnapshot postCleanupSnapshot,
            Func<WorldSnapshot> refreshedSnapshotFactory,
            bool refreshAfterPriorRespawnMutation,
            IReadOnlyList<MoonBlockRespawnDefinition> respawnDefinitions,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            int tickIndex,
            IWorldWriteContext writeContext)
        {
            if (postCleanupSnapshot == null)
            {
                throw new ArgumentNullException(nameof(postCleanupSnapshot));
            }

            if (respawnDefinitions == null || respawnDefinitions.Count == 0)
            {
                return Array.Empty<string>();
            }

            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            var snapshot = postCleanupSnapshot;
            var didRefresh = false;
            var eventLogEntries = new List<string>();

            for (var i = 0; i < respawnDefinitions.Count; i++)
            {
                var definition = respawnDefinitions[i];
                if (IsBoundMoonBlockAlive(snapshot, definition.MoonBlockEntityId))
                {
                    continue;
                }

                if (refreshAfterPriorRespawnMutation && !didRefresh)
                {
                    if (refreshedSnapshotFactory == null)
                    {
                        throw new ArgumentNullException(nameof(refreshedSnapshotFactory));
                    }

                    snapshot = refreshedSnapshotFactory();
                    didRefresh = true;
                    if (IsBoundMoonBlockAlive(snapshot, definition.MoonBlockEntityId))
                    {
                        continue;
                    }
                }

                if (!snapshot.TryGetTileFeature(definition.GeneratorTileId, out var generator) ||
                    generator.Kind != TileFeatureKind.MoonBlockGenerator ||
                    !TryFindTileFeatureDefinition(tileFeatureDefinitions, definition.GeneratorTileId, out var tileDefinition) ||
                    !TileFeatureActivationQueries.IsActive(generator, tileDefinition, snapshot.Topology))
                {
                    continue;
                }

                var spawnCell = definition.SpawnCell;
                if (snapshot.HasAnyUnitAt(spawnCell))
                {
                    eventLogEntries.Add(
                        $"MoonBlockGeneratorRespawnDeferred|TileId={definition.GeneratorTileId}|E={definition.MoonBlockEntityId}|Reason=UnitBlocked|Tick={tickIndex}");
                    continue;
                }

                var boundEntityExists = snapshot.TryGetEntity(definition.MoonBlockEntityId, out var existingBoundEntity);
                var blockingBoxEntityId = 0;
                if (snapshot.TryGetSolidSemanticAt(spawnCell, out var solidSemantic))
                {
                    var solidOccupant = solidSemantic.Entity;
                    if (solidOccupant.entityId == definition.MoonBlockEntityId)
                    {
                        if (IsBoundMoonBlockAlive(snapshot, definition.MoonBlockEntityId))
                        {
                            continue;
                        }
                    }
                    else if (solidSemantic.Kind == SolidKind.Box)
                    {
                        if (solidOccupant.boxArchetype == BoxArchetype.Moon)
                        {
                            continue;
                        }

                        blockingBoxEntityId = solidOccupant.entityId;
                    }
                    else
                    {
                        eventLogEntries.Add(
                            $"MoonBlockGeneratorRespawnDeferred|TileId={definition.GeneratorTileId}|E={definition.MoonBlockEntityId}|Reason=SolidBlocked|Blocker={solidOccupant.entityId}|Tick={tickIndex}");
                        continue;
                    }
                }

                var placementLegality = RuntimePlacementValidityPolicy.EvaluateAuthoritativePlacement(
                    snapshot,
                    EntityType.Box,
                    spawnCell,
                    ignoredEntityId: definition.MoonBlockEntityId);
                if (placementLegality.Verdict == LegalityVerdict.Blocked && blockingBoxEntityId == 0)
                {
                    eventLogEntries.Add(
                        $"MoonBlockGeneratorRespawnDeferred|TileId={definition.GeneratorTileId}|E={definition.MoonBlockEntityId}|Reason=PlacementBlocked|Tick={tickIndex}");
                    continue;
                }

                if (boundEntityExists)
                {
                    writeContext.RemoveBoxInteractionLockState(definition.MoonBlockEntityId);
                    writeContext.RemoveEntity(definition.MoonBlockEntityId);
                    eventLogEntries.Add(
                        $"MoonBlockGeneratorStaleMoonBlockRemoved|TileId={definition.GeneratorTileId}|E={definition.MoonBlockEntityId}|Tick={tickIndex}");
                }

                if (blockingBoxEntityId > 0)
                {
                    writeContext.SetBoardPresence(blockingBoxEntityId, EntityBoardPresence.Detached);
                    ((IAttackCommitContext)writeContext).MarkDestroy(blockingBoxEntityId);
                    writeContext.RemoveBoxInteractionLockState(blockingBoxEntityId);
                    eventLogEntries.Add(
                        $"MoonBlockGeneratorBlockingBoxDestroyed|TileId={definition.GeneratorTileId}|Blocker={blockingBoxEntityId}|E={definition.MoonBlockEntityId}|Tick={tickIndex}");
                }

                var respawnEntity = BuildRespawnEntity(definition, tickIndex);
                writeContext.SpawnEntity(respawnEntity);
                writeContext.RemoveBoxInteractionLockState(respawnEntity.entityId);
                eventLogEntries.Add(
                    $"MoonBlockGeneratorRespawnCommitted|TileId={definition.GeneratorTileId}|E={respawnEntity.entityId}|Pos=({respawnEntity.position.x},{respawnEntity.position.y})|Face={respawnEntity.position.face}|Tick={tickIndex}");
            }

            return eventLogEntries;
        }

        private static EntityState BuildRespawnEntity(
            MoonBlockRespawnDefinition definition,
            int tickIndex)
        {
            var template = definition.Template;
            var maxHp = template.maxHp > 0 ? template.maxHp : template.hp;
            template.position = definition.SpawnCell;
            template.hp = maxHp;
            template.maxHp = maxHp;
            template.state = EntityPhaseState.Idle;
            template.stateTimer = 0;
            template.boardPresence = EntityBoardPresence.Occupying;
            template.markedForDeath = false;
            template.spawnTick = tickIndex;
            template.kineticInstigatorEntityId = 0;
            template.kineticInstigatorTeamId = 0;
            template.aiStateTimer = 0;
            template.enemyLocomotionCooldownTicks = 0;
            return template;
        }

        private static bool IsBoundMoonBlockAlive(WorldSnapshot snapshot, int entityId)
        {
            return snapshot.TryGetEntity(entityId, out var entity) &&
                   entity.entityId == entityId &&
                   entity.type == EntityType.Box &&
                   entity.boxArchetype == BoxArchetype.Moon &&
                   entity.boardPresence == EntityBoardPresence.Occupying &&
                   entity.hp > 0 &&
                   !entity.markedForDeath;
        }

        private static bool TryFindTileFeatureDefinition(
            IReadOnlyList<TileFeatureRuntimeDefinition> definitions,
            int tileId,
            out TileFeatureRuntimeDefinition definition)
        {
            if (definitions != null)
            {
                for (var i = 0; i < definitions.Count; i++)
                {
                    if (definitions[i].TileId == tileId)
                    {
                        definition = definitions[i];
                        return true;
                    }
                }
            }

            definition = default;
            return false;
        }
    }
}
