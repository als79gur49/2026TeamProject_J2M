using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Objectives;

namespace Game.Feature.Stages
{
    public static class StageDefinitionValidator
    {
        public static void Validate(StageDefinition stage)
        {
            ValidateAndNormalize(stage);
        }

        internal static ValidatedStageData ValidateAndNormalize(StageDefinition stage)
        {
            if (stage == null)
            {
                throw new ArgumentNullException(nameof(stage), "StageDefinition cannot be null.");
            }

            var stageName = GetStageName(stage);
            var board = stage.Board;
            var boardBounds = CreateBoardBounds(stageName, board);
            var spawnEntries = NormalizeExplicitSpawns(stage.GetSpawnGroups());
            var playerEntityId = ValidateEntities(stageName, spawnEntries, boardBounds);
            var zones = ValidateZones(stageName, stage.Zones, boardBounds);
            var objective = ValidateObjective(stageName, stage.Objective, zones);

            return new ValidatedStageData(
                stageName,
                boardBounds,
                new CubeTopologyState(board.InitialBottomFace),
                ExtractSpawns(spawnEntries),
                playerEntityId,
                zones,
                objective);
        }

        private static BoardBounds CreateBoardBounds(string stageName, StageBoardDefinition board)
        {
            if (board.MaxInclusive.x < board.MinInclusive.x ||
                board.MaxInclusive.y < board.MinInclusive.y)
            {
                throw new InvalidOperationException(
                    $"Stage '{stageName}' has invalid board bounds. MinInclusive=({board.MinInclusive.x},{board.MinInclusive.y}) must be less than or equal to MaxInclusive=({board.MaxInclusive.x},{board.MaxInclusive.y}).");
            }

            return new BoardBounds(board.MinInclusive, board.MaxInclusive);
        }

        private static int ValidateEntities(
            string stageName,
            IReadOnlyList<ExplicitSpawnEntry> spawnEntries,
            BoardBounds boardBounds)
        {
            var occupiedCells = new HashSet<SurfaceCell>();
            var explicitEntityIds = new HashSet<int>();
            var playerCount = 0;
            var playerEntityId = 0;

            for (var i = 0; i < spawnEntries.Count; i++)
            {
                var spawnEntry = spawnEntries[i];
                var spawn = spawnEntry.Spawn;
                var spawnLabel = FormatSpawnLabel(spawnEntry);

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

                if (!occupiedCells.Add(spawn.Cell))
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' contains duplicate occupied cell {spawn.Cell}.");
                }

                if (spawn.Hp <= 0)
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' {spawnLabel} must use a positive Hp value.");
                }

                if (spawn.Kind != StageSpawnKind.Player)
                {
                    continue;
                }

                playerCount++;
                playerEntityId = spawn.EntityId;
            }

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

        private static StageZoneDefinition[] ValidateZones(
            string stageName,
            IReadOnlyList<StageZoneDefinition> zones,
            BoardBounds boardBounds)
        {
            if (zones == null || zones.Count == 0)
            {
                return Array.Empty<StageZoneDefinition>();
            }

            var normalizedZones = new StageZoneDefinition[zones.Count];
            var zoneIds = new HashSet<string>(StringComparer.Ordinal);

            for (var zoneIndex = 0; zoneIndex < zones.Count; zoneIndex++)
            {
                var zone = zones[zoneIndex];
                var normalizedZoneId = NormalizeZoneId(stageName, zoneIndex, zone.ZoneId);
                if (!zoneIds.Add(normalizedZoneId))
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' contains duplicate zone id '{normalizedZoneId}'.");
                }

                var normalizedRegions = ValidateRegions(stageName, zoneIndex, normalizedZoneId, zone.GetRegionsOrEmpty(), boardBounds);
                normalizedZones[zoneIndex] = new StageZoneDefinition
                {
                    ZoneId = normalizedZoneId,
                    FaceId = zone.FaceId,
                    Regions = normalizedRegions,
                };
            }

            return normalizedZones;
        }

        private static StageObjectiveAuthoring ValidateObjective(
            string stageName,
            StageObjectiveAuthoring objective,
            IReadOnlyList<StageZoneDefinition> zones)
        {
            var normalizedGoalZoneIds = objective.GetGoalZoneIdsOrEmpty();
            var requiredConditions = objective.GetRequiredConditionsOrEmpty();
            var zonesById = BuildZonesById(zones);
            var validationContext = new StageConditionValidationContext(stageName, zonesById);
            var normalizedGoalIds = new string[normalizedGoalZoneIds.Length];

            for (var i = 0; i < normalizedGoalZoneIds.Length; i++)
            {
                var goalZoneId = normalizedGoalZoneIds[i];
                if (string.IsNullOrWhiteSpace(goalZoneId))
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' objective contains an empty goal zone id at index {i}.");
                }

                var normalizedGoalZoneId = goalZoneId.Trim();
                if (!zonesById.ContainsKey(normalizedGoalZoneId))
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' objective references unknown goal zone id '{normalizedGoalZoneId}'.");
                }

                normalizedGoalIds[i] = normalizedGoalZoneId;
            }

            var normalizedConditions = new StageConditionAsset[requiredConditions.Length];
            for (var i = 0; i < requiredConditions.Length; i++)
            {
                var condition = requiredConditions[i];
                if (condition == null)
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' objective contains a null condition asset at index {i}.");
                }

                condition.Validate(in validationContext);
                normalizedConditions[i] = condition;
            }

            return new StageObjectiveAuthoring
            {
                CompletionPolicy = objective.CompletionPolicy,
                GoalZoneIds = normalizedGoalIds,
                RequiredConditions = normalizedConditions,
            };
        }

        private static string NormalizeZoneId(string stageName, int zoneIndex, string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                throw new InvalidOperationException(
                    $"Stage '{stageName}' zone[{zoneIndex}] must declare a non-empty zone id.");
            }

            return zoneId.Trim();
        }

        private static StageZoneRegionDefinition[] ValidateRegions(
            string stageName,
            int zoneIndex,
            string zoneId,
            IReadOnlyList<StageZoneRegionDefinition> regions,
            BoardBounds boardBounds)
        {
            if (regions == null || regions.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Stage '{stageName}' zone '{zoneId}' must contain at least one region.");
            }

            var normalizedRegions = new StageZoneRegionDefinition[regions.Count];
            for (var regionIndex = 0; regionIndex < regions.Count; regionIndex++)
            {
                var region = regions[regionIndex];
                if (region.MaxInclusive.x < region.MinInclusive.x ||
                    region.MaxInclusive.y < region.MinInclusive.y)
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' zone '{zoneId}' region[{regionIndex}] has inverted min/max bounds.");
                }

                for (var x = region.MinInclusive.x; x <= region.MaxInclusive.x; x++)
                {
                    for (var y = region.MinInclusive.y; y <= region.MaxInclusive.y; y++)
                    {
                        var cell = new UnityEngine.Vector2Int(x, y);
                        if (!boardBounds.Contains(cell))
                        {
                            throw new InvalidOperationException(
                                $"Stage '{stageName}' zone '{zoneId}' region[{regionIndex}] contains out-of-bounds cell ({x},{y}).");
                        }
                    }
                }

                normalizedRegions[regionIndex] = region;
            }

            return normalizedRegions;
        }

        private static Dictionary<string, StageZoneDefinition> BuildZonesById(IReadOnlyList<StageZoneDefinition> zones)
        {
            var zonesById = new Dictionary<string, StageZoneDefinition>(StringComparer.Ordinal);
            for (var i = 0; i < zones.Count; i++)
            {
                zonesById[zones[i].ZoneId] = zones[i];
            }

            return zonesById;
        }

        private static ExplicitSpawnEntry[] NormalizeExplicitSpawns(
            IReadOnlyList<StageDefinition.StageSpawnGroup> spawnGroups)
        {
            var entries = new List<ExplicitSpawnEntry>();

            for (var groupIndex = 0; groupIndex < spawnGroups.Count; groupIndex++)
            {
                var spawnGroup = spawnGroups[groupIndex];
                var groupSpawns = spawnGroup.Spawns ?? Array.Empty<StageSpawnDefinition>();

                for (var spawnIndex = 0; spawnIndex < groupSpawns.Length; spawnIndex++)
                {
                    entries.Add(new ExplicitSpawnEntry(
                        spawnGroup.GroupName,
                        spawnGroup.ExpectedKind,
                        spawnIndex,
                        groupSpawns[spawnIndex]));
                }
            }

            if (entries.Count == 0)
            {
                return Array.Empty<ExplicitSpawnEntry>();
            }

            return entries.ToArray();
        }

        private static StageSpawnDefinition[] ExtractSpawns(IReadOnlyList<ExplicitSpawnEntry> spawnEntries)
        {
            if (spawnEntries.Count == 0)
            {
                return Array.Empty<StageSpawnDefinition>();
            }

            var spawns = new StageSpawnDefinition[spawnEntries.Count];
            for (var i = 0; i < spawnEntries.Count; i++)
            {
                spawns[i] = spawnEntries[i].Spawn;
            }

            return spawns;
        }

        private static string FormatSpawnLabel(ExplicitSpawnEntry spawnEntry)
        {
            var spawn = spawnEntry.Spawn;
            return $"{spawnEntry.GroupName}[{spawnEntry.GroupIndex}] ({spawn.Kind}, EntityId={spawn.EntityId}, Cell={spawn.Cell})";
        }

        private static string GetStageName(StageDefinition stage)
        {
            return string.IsNullOrWhiteSpace(stage.name) ? "<unnamed stage>" : stage.name;
        }

        internal readonly struct ExplicitSpawnEntry
        {
            public ExplicitSpawnEntry(
                string groupName,
                StageSpawnKind expectedKind,
                int groupIndex,
                StageSpawnDefinition spawn)
            {
                GroupName = groupName ?? string.Empty;
                ExpectedKind = expectedKind;
                GroupIndex = groupIndex;
                Spawn = spawn;
            }

            public string GroupName { get; }

            public StageSpawnKind ExpectedKind { get; }

            public int GroupIndex { get; }

            public StageSpawnDefinition Spawn { get; }
        }

        internal sealed class ValidatedStageData
        {
            public ValidatedStageData(
                string stageName,
                BoardBounds boardBounds,
                CubeTopologyState initialTopology,
                StageSpawnDefinition[] spawns,
                int playerEntityId,
                StageZoneDefinition[] zones,
                StageObjectiveAuthoring objective)
            {
                StageName = string.IsNullOrWhiteSpace(stageName) ? "<unnamed stage>" : stageName;
                BoardBounds = boardBounds;
                InitialTopology = initialTopology;
                Spawns = spawns ?? Array.Empty<StageSpawnDefinition>();
                PlayerEntityId = playerEntityId;
                Zones = zones ?? Array.Empty<StageZoneDefinition>();
                Objective = objective;
            }

            public string StageName { get; }

            public BoardBounds BoardBounds { get; }

            public CubeTopologyState InitialTopology { get; }

            public StageSpawnDefinition[] Spawns { get; }

            public int PlayerEntityId { get; }

            public StageZoneDefinition[] Zones { get; }

            public StageObjectiveAuthoring Objective { get; }
        }
    }
}
