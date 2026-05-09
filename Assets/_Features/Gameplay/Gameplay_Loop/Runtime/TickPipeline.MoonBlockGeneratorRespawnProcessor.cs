using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Loop
{
    internal enum MoonBlockGeneratorBlockedReason
    {
        None = 0,
        UnitOccupant = 1,
        WallLikeSolid = 2,
        PlacementBlocked = 3,
    }

    internal readonly struct MoonBlockGeneratorBlockedKey : IEquatable<MoonBlockGeneratorBlockedKey>
    {
        public MoonBlockGeneratorBlockedKey(
            int generatorTileId,
            MoonBlockGeneratorBlockedReason reason,
            int blockingEntityId)
        {
            GeneratorTileId = generatorTileId;
            Reason = reason;
            BlockingEntityId = blockingEntityId;
        }

        public int GeneratorTileId { get; }

        public MoonBlockGeneratorBlockedReason Reason { get; }

        public int BlockingEntityId { get; }

        public bool Equals(MoonBlockGeneratorBlockedKey other)
        {
            return GeneratorTileId == other.GeneratorTileId &&
                   Reason == other.Reason &&
                   BlockingEntityId == other.BlockingEntityId;
        }

        public override bool Equals(object obj)
        {
            return obj is MoonBlockGeneratorBlockedKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = GeneratorTileId;
                hash = (hash * 397) ^ (int)Reason;
                hash = (hash * 397) ^ BlockingEntityId;
                return hash;
            }
        }
    }

    internal sealed class MoonBlockGeneratorRespawnProcessor
    {
        private readonly Dictionary<int, MoonBlockGeneratorBlockedKey> _lastBlockedKeyByGeneratorTileId = new();
        private readonly List<EntityState> _unitOccupantBuffer = new();

        public MoonBlockGeneratorRespawnProcessorResult Process(
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
                _lastBlockedKeyByGeneratorTileId.Clear();
                return MoonBlockGeneratorRespawnProcessorResult.Empty;
            }

            if (writeContext == null)
            {
                throw new ArgumentNullException(nameof(writeContext));
            }

            var snapshot = postCleanupSnapshot;
            var didRefresh = false;
            var eventLogEntries = new List<string>();
            var respawnFacts = new List<MoonBlockGeneratorRespawnFact>();
            var blockedFacts = new List<MoonBlockGeneratorBlockedFact>();

            for (var i = 0; i < respawnDefinitions.Count; i++)
            {
                var definition = respawnDefinitions[i];
                if (IsBoundMoonBlockAlive(snapshot, definition.MoonBlockEntityId))
                {
                    ClearBlockedMemory(definition.GeneratorTileId);
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
                        ClearBlockedMemory(definition.GeneratorTileId);
                        continue;
                    }
                }

                if (!snapshot.TryGetTileFeature(definition.GeneratorTileId, out var generator) ||
                    generator.Kind != TileFeatureKind.MoonBlockGenerator ||
                    !TryFindTileFeatureDefinition(tileFeatureDefinitions, definition.GeneratorTileId, out var tileDefinition) ||
                    !TileFeatureActivationQueries.IsActive(generator, tileDefinition, snapshot.Topology))
                {
                    ClearBlockedMemory(definition.GeneratorTileId);
                    continue;
                }

                var spawnCell = definition.SpawnCell;
                if (TryGetFirstUnitAt(snapshot, spawnCell, out var unitBlocker))
                {
                    eventLogEntries.Add(
                        $"MoonBlockGeneratorRespawnDeferred|TileId={definition.GeneratorTileId}|E={definition.MoonBlockEntityId}|Reason=UnitBlocked|Tick={tickIndex}");
                    AddDebouncedBlockedFact(
                        blockedFacts,
                        definition,
                        generator,
                        MoonBlockGeneratorBlockedReason.UnitOccupant,
                        unitBlocker.entityId,
                        spawnCell);
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
                        AddDebouncedBlockedFact(
                            blockedFacts,
                            definition,
                            generator,
                            MoonBlockGeneratorBlockedReason.WallLikeSolid,
                            solidOccupant.entityId,
                            spawnCell);
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
                    AddDebouncedBlockedFact(
                        blockedFacts,
                        definition,
                        generator,
                        MoonBlockGeneratorBlockedReason.PlacementBlocked,
                        blockingEntityId: 0,
                        spawnCell);
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
                respawnFacts.Add(
                    new MoonBlockGeneratorRespawnFact(
                        definition.GeneratorTileId,
                        spawnCell,
                        respawnEntity.entityId,
                        generator.SourceEntityId,
                        generator.OwnerEntityId,
                        generator.TeamId));
                ClearBlockedMemory(definition.GeneratorTileId);
                eventLogEntries.Add(
                    $"MoonBlockGeneratorRespawnCommitted|TileId={definition.GeneratorTileId}|E={respawnEntity.entityId}|Pos=({respawnEntity.position.x},{respawnEntity.position.y})|Face={respawnEntity.position.face}|Tick={tickIndex}");
            }

            return new MoonBlockGeneratorRespawnProcessorResult(eventLogEntries, respawnFacts, blockedFacts);
        }

        private void AddDebouncedBlockedFact(
            List<MoonBlockGeneratorBlockedFact> blockedFacts,
            in MoonBlockRespawnDefinition definition,
            in TileFeatureState generator,
            MoonBlockGeneratorBlockedReason reason,
            int blockingEntityId,
            SurfaceCell spawnCell)
        {
            if (reason == MoonBlockGeneratorBlockedReason.None)
            {
                return;
            }

            var key = new MoonBlockGeneratorBlockedKey(definition.GeneratorTileId, reason, blockingEntityId);
            if (_lastBlockedKeyByGeneratorTileId.TryGetValue(definition.GeneratorTileId, out var lastKey) &&
                lastKey.Equals(key))
            {
                return;
            }

            _lastBlockedKeyByGeneratorTileId[definition.GeneratorTileId] = key;
            blockedFacts.Add(
                new MoonBlockGeneratorBlockedFact(
                    definition.GeneratorTileId,
                    spawnCell,
                    blockingEntityId,
                    generator.SourceEntityId,
                    generator.OwnerEntityId,
                    generator.TeamId));
        }

        private void ClearBlockedMemory(int generatorTileId)
        {
            _lastBlockedKeyByGeneratorTileId.Remove(generatorTileId);
        }

        private bool TryGetFirstUnitAt(WorldSnapshot snapshot, SurfaceCell cell, out EntityState unit)
        {
            _unitOccupantBuffer.Clear();
            snapshot.EnumerateUnitsAt(cell, _unitOccupantBuffer);
            if (_unitOccupantBuffer.Count > 0)
            {
                unit = _unitOccupantBuffer[0];
                _unitOccupantBuffer.Clear();
                return true;
            }

            unit = default;
            return false;
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

    internal sealed class MoonBlockGeneratorRespawnProcessorResult
    {
        public static readonly MoonBlockGeneratorRespawnProcessorResult Empty = new(
            Array.Empty<string>(),
            Array.Empty<MoonBlockGeneratorRespawnFact>(),
            Array.Empty<MoonBlockGeneratorBlockedFact>());

        private readonly IReadOnlyList<string> _eventLogEntries;
        private readonly IReadOnlyList<MoonBlockGeneratorBlockedFact> _blockedFacts;
        private readonly IReadOnlyList<MoonBlockGeneratorRespawnFact> _respawnFacts;

        public MoonBlockGeneratorRespawnProcessorResult(
            IReadOnlyList<string> eventLogEntries,
            IReadOnlyList<MoonBlockGeneratorRespawnFact> respawnFacts,
            IReadOnlyList<MoonBlockGeneratorBlockedFact> blockedFacts)
        {
            _eventLogEntries = eventLogEntries ?? throw new ArgumentNullException(nameof(eventLogEntries));
            _respawnFacts = respawnFacts ?? throw new ArgumentNullException(nameof(respawnFacts));
            _blockedFacts = blockedFacts ?? throw new ArgumentNullException(nameof(blockedFacts));
        }

        public IReadOnlyList<string> EventLogEntries => _eventLogEntries;

        public IReadOnlyList<MoonBlockGeneratorRespawnFact> RespawnFacts => _respawnFacts;

        public IReadOnlyList<MoonBlockGeneratorBlockedFact> BlockedFacts => _blockedFacts;
    }
}
