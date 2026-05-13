using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Stages
{
    internal static class AuthoringStageValidityPolicy
    {
        public static int ValidateEntities(
            string stageName,
            IReadOnlyList<StageDefinitionValidator.ExplicitSpawnEntry> spawnEntries,
            BoardBounds boardBounds)
        {
            var spawnsByCell = new Dictionary<SurfaceCell, List<StageDefinitionValidator.ExplicitSpawnEntry>>();
            var explicitEntityIds = new HashSet<int>();
            var playerCount = 0;
            var playerEntityId = 0;
            var moonBlockCount = 0;

            for (var i = 0; i < spawnEntries.Count; i++)
            {
                var spawnEntry = spawnEntries[i];
                var spawn = spawnEntry.Spawn;
                var spawnLabel = StageDefinitionValidator.FormatSpawnLabel(spawnEntry);

                if (spawn.Kind != spawnEntry.ExpectedKind)
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' {spawnEntry.GroupName}[{spawnEntry.GroupIndex}] must use Kind {spawnEntry.ExpectedKind}, but found {spawn.Kind}.");
                }

                if (spawn.EntityId <= 0)
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' {spawnLabel} must use a positive entity id.");
                }

                if (!explicitEntityIds.Add(spawn.EntityId))
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' contains duplicate entity id {spawn.EntityId}.");
                }

                if (!boardBounds.Contains(spawn.Cell.PlanarPosition))
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' {spawnLabel} is outside the configured board bounds at {spawn.Cell}.");
                }

                if (spawn.Hp <= 0)
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' {spawnLabel} must use a positive Hp value.");
                }

                ValidateBoxArchetype(stageName, spawnLabel, spawn, ref moonBlockCount);
                ValidateUnitMobilityKind(stageName, spawnLabel, spawn);

                if (!spawnsByCell.TryGetValue(spawn.Cell, out var cellEntries))
                {
                    cellEntries = new List<StageDefinitionValidator.ExplicitSpawnEntry>();
                    spawnsByCell.Add(spawn.Cell, cellEntries);
                }

                cellEntries.Add(spawnEntry);

                if (spawn.Kind != StageSpawnKind.Player)
                {
                    continue;
                }

                playerCount++;
                playerEntityId = spawn.EntityId;
            }

            ValidateCellOccupancy(stageName, spawnsByCell);

            if (playerCount == 0)
            {
                throw new InvalidOperationException($"Stage '{stageName}' must contain exactly one player spawn, but found none.");
            }

            if (playerCount > 1)
            {
                throw new InvalidOperationException(
                    $"Stage '{stageName}' must contain exactly one player spawn, but found {playerCount}.");
            }

            return playerEntityId;
        }

        private static void ValidateUnitMobilityKind(
            string stageName,
            string spawnLabel,
            StageSpawnDefinition spawn)
        {
            if (!IsUnitSpawn(spawn.Kind))
            {
                return;
            }

            if (!Enum.IsDefined(typeof(UnitMobilityKind), spawn.UnitMobilityKind))
            {
                throw new InvalidOperationException(
                    $"Stage '{stageName}' {spawnLabel} has invalid UnitMobilityKind value {(int)spawn.UnitMobilityKind}.");
            }
        }

        private static void ValidateBoxArchetype(
            string stageName,
            string spawnLabel,
            StageSpawnDefinition spawn,
            ref int moonBlockCount)
        {
            if (!Enum.IsDefined(typeof(BoxArchetype), spawn.BoxArchetype))
            {
                throw new InvalidOperationException(
                    $"Stage '{stageName}' {spawnLabel} has invalid BoxArchetype value {(int)spawn.BoxArchetype}.");
            }

            if (spawn.BoxArchetype == BoxArchetype.Normal)
            {
                return;
            }

            if (spawn.Kind != StageSpawnKind.Box)
            {
                throw new InvalidOperationException(
                    $"Stage '{stageName}' {spawnLabel} uses BoxArchetype {spawn.BoxArchetype}, but box archetypes are only valid on Box spawns.");
            }

            if (spawn.BoxArchetype != BoxArchetype.Moon)
            {
                return;
            }

            moonBlockCount++;
            if (moonBlockCount > 1)
            {
                throw new InvalidOperationException(
                    $"Stage '{stageName}' contains more than one Moon box spawn.");
            }

            const BoxCapabilities requiredMoonCapabilities =
                BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy;
            if ((spawn.BoxCapabilities & requiredMoonCapabilities) != requiredMoonCapabilities)
            {
                throw new InvalidOperationException(
                    $"Stage '{stageName}' {spawnLabel} uses BoxArchetype {spawn.BoxArchetype}, but Moon boxes require BoxCapabilities {requiredMoonCapabilities}.");
            }
        }

        private static void ValidateCellOccupancy(
            string stageName,
            IReadOnlyDictionary<SurfaceCell, List<StageDefinitionValidator.ExplicitSpawnEntry>> spawnsByCell)
        {
            foreach (var pair in spawnsByCell)
            {
                var cellEntries = pair.Value;
                if (cellEntries.Count <= 1)
                {
                    continue;
                }

                if (!CanAuthorUnitStack(cellEntries))
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' contains duplicate occupied cell {pair.Key}.");
                }
            }
        }

        private static bool CanAuthorUnitStack(
            IReadOnlyList<StageDefinitionValidator.ExplicitSpawnEntry> cellEntries)
        {
            string requiredStackGroup = null;

            for (var i = 0; i < cellEntries.Count; i++)
            {
                var spawn = cellEntries[i].Spawn;
                if (!IsUnitSpawn(spawn.Kind))
                {
                    return false;
                }

                var stackGroup = NormalizeUnitStackGroup(spawn.UnitStackGroup);
                if (string.IsNullOrEmpty(stackGroup))
                {
                    return false;
                }

                if (requiredStackGroup == null)
                {
                    requiredStackGroup = stackGroup;
                    continue;
                }

                if (!string.Equals(requiredStackGroup, stackGroup, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return !string.IsNullOrEmpty(requiredStackGroup);
        }

        private static bool IsUnitSpawn(StageSpawnKind kind)
        {
            return kind == StageSpawnKind.Player || kind == StageSpawnKind.Enemy;
        }

        private static string NormalizeUnitStackGroup(string unitStackGroup)
        {
            return string.IsNullOrWhiteSpace(unitStackGroup)
                ? string.Empty
                : unitStackGroup.Trim();
        }
    }
}
