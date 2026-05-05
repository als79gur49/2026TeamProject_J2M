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
            var tileFeatures = ValidateTileFeatures(stageName, stage.TileFeatures, spawnEntries, boardBounds);
            var objective = ValidateObjective(stageName, stage.Objective, zones);

            return new ValidatedStageData(
                stageName,
                boardBounds,
                new CubeTopologyState(board.InitialBottomFace),
                ExtractSpawns(spawnEntries),
                tileFeatures,
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
            return AuthoringStageValidityPolicy.ValidateEntities(stageName, spawnEntries, boardBounds);
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

        private static StageTileFeatureDefinition[] ValidateTileFeatures(
            string stageName,
            IReadOnlyList<StageTileFeatureDefinition> tileFeatures,
            IReadOnlyList<ExplicitSpawnEntry> spawnEntries,
            BoardBounds boardBounds)
        {
            if (tileFeatures == null || tileFeatures.Count == 0)
            {
                return Array.Empty<StageTileFeatureDefinition>();
            }

            var normalized = new StageTileFeatureDefinition[tileFeatures.Count];
            var tileIds = new HashSet<int>();
            var wallCells = BuildWallCells(spawnEntries);

            for (var i = 0; i < tileFeatures.Count; i++)
            {
                var tileFeature = tileFeatures[i];
                var label = $"tileFeatures[{i}] (TileId={tileFeature.TileId}, Cell={tileFeature.Cell})";

                if (tileFeature.TileId <= 0)
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' {label} must use a positive tile id.");
                }

                if (!tileIds.Add(tileFeature.TileId))
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' contains duplicate tile feature id {tileFeature.TileId}.");
                }

                if (!Enum.IsDefined(typeof(FaceId), tileFeature.Cell.face))
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' {label} has invalid face value {(int)tileFeature.Cell.face}.");
                }

                if (!boardBounds.Contains(tileFeature.Cell.PlanarPosition))
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' {label} is outside the configured board bounds.");
                }

                if (!Enum.IsDefined(typeof(TileFeatureKind), tileFeature.Kind) ||
                    tileFeature.Kind == TileFeatureKind.Unknown)
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' {label} must use a known TileFeatureKind.");
                }

                if (!Enum.IsDefined(typeof(TileFeatureActivationRule), tileFeature.ActivationRule))
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' {label} has invalid activation rule value {(int)tileFeature.ActivationRule}.");
                }

                if (!Enum.IsDefined(typeof(Direction2D), tileFeature.Direction))
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' {label} has invalid Direction2D value {(int)tileFeature.Direction}.");
                }

                if (!Enum.IsDefined(typeof(TileFeatureBoxSelector), tileFeature.BoxSelector))
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' {label} has invalid TileFeatureBoxSelector value {(int)tileFeature.BoxSelector}.");
                }

                if (wallCells.Contains(tileFeature.Cell))
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' {label} overlaps a wall-like solid occupant. TileFeature solid overlap policy is not enabled in this phase.");
                }

                normalized[i] = new StageTileFeatureDefinition
                {
                    TileId = tileFeature.TileId,
                    Cell = tileFeature.Cell,
                    Kind = tileFeature.Kind,
                    ActivationRule = tileFeature.ActivationRule,
                    Direction = tileFeature.Direction,
                    BoxSelector = tileFeature.BoxSelector,
                    BoundEntityId = tileFeature.BoundEntityId,
                    PresentationKey = tileFeature.PresentationKey?.Trim() ?? string.Empty,
                };
            }

            Array.Sort(normalized, (left, right) => left.TileId.CompareTo(right.TileId));
            return normalized;
        }

        private static HashSet<SurfaceCell> BuildWallCells(IReadOnlyList<ExplicitSpawnEntry> spawnEntries)
        {
            var wallCells = new HashSet<SurfaceCell>();
            for (var i = 0; i < spawnEntries.Count; i++)
            {
                var spawn = spawnEntries[i].Spawn;
                if (spawn.Kind == StageSpawnKind.Wall)
                {
                    wallCells.Add(spawn.Cell);
                }
            }

            return wallCells;
        }

        private static StageObjectiveAuthoring ValidateObjective(
            string stageName,
            StageObjectiveAuthoring objective,
            IReadOnlyList<StageZoneDefinition> zones)
        {
            var conditionEntries = objective.GetConditionEntriesOrEmpty();
            var zonesById = BuildZonesById(zones);
            var validationContext = new StageConditionValidationContext(stageName, zonesById);

            var normalizedConditionEntries = ValidateConditionEntries(
                stageName,
                conditionEntries,
                in validationContext);

            ValidateCompletionPolicy(
                stageName,
                objective.CompletionPolicy,
                normalizedConditionEntries);

            return new StageObjectiveAuthoring
            {
                CompletionPolicy = objective.CompletionPolicy,
                ObjectiveTitle = objective.ObjectiveTitle?.Trim() ?? string.Empty,
                ObjectiveSummary = objective.ObjectiveSummary?.Trim() ?? string.Empty,
                ConditionEntries = normalizedConditionEntries,
            };
        }

        private static StageObjectiveConditionEntry[] ValidateConditionEntries(
            string stageName,
            IReadOnlyList<StageObjectiveConditionEntry> conditionEntries,
            in StageConditionValidationContext validationContext)
        {
            if (conditionEntries == null || conditionEntries.Count == 0)
            {
                return Array.Empty<StageObjectiveConditionEntry>();
            }

            var normalizedEntries = new StageObjectiveConditionEntry[conditionEntries.Count];
            var stableConditionIds = new HashSet<string>(StringComparer.Ordinal);
            var primaryGoalCount = 0;

            for (var i = 0; i < conditionEntries.Count; i++)
            {
                var entry = conditionEntries[i];
                if (entry.Condition == null)
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' objective condition entry[{i}] contains a null condition asset.");
                }

                if (entry.Role == StageObjectiveConditionRole.PrimaryGoal)
                {
                    primaryGoalCount++;
                }

                var stableConditionId = entry.StableConditionId?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(stableConditionId) &&
                    !stableConditionIds.Add(stableConditionId))
                {
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' objective contains duplicate condition stable id '{stableConditionId}'.");
                }

                entry.Condition.Validate(in validationContext);
                normalizedEntries[i] = new StageObjectiveConditionEntry
                {
                    Condition = entry.Condition,
                    Required = entry.Required,
                    Role = entry.Role,
                    StableConditionId = stableConditionId,
                    DisplayText = entry.DisplayText?.Trim() ?? string.Empty,
                    SortOrder = entry.SortOrder,
                };
            }

            if (primaryGoalCount > 1)
            {
                throw new InvalidOperationException(
                    $"Stage '{stageName}' objective cannot contain more than one PrimaryGoal condition entry.");
            }

            return normalizedEntries;
        }

        private static void ValidateCompletionPolicy(
            string stageName,
            StageCompletionPolicy completionPolicy,
            IReadOnlyList<StageObjectiveConditionEntry> conditionEntries)
        {
            switch (completionPolicy)
            {
                case StageCompletionPolicy.Disabled:
                    return;

                case StageCompletionPolicy.RequireAllConditions:
                    var requiredConditionCount = CountRequiredEntries(conditionEntries);
                    if (requiredConditionCount == 0)
                    {
                        throw new InvalidOperationException(
                            $"Stage '{stageName}' objective policy {completionPolicy} requires at least one required condition.");
                    }

                    if (ContainsOnlyTimeLimitEntries(conditionEntries))
                    {
                        throw new InvalidOperationException(
                            $"Stage '{stageName}' objective policy {completionPolicy} cannot use only time limit conditions because it would clear immediately.");
                    }

                    return;

                default:
                    throw new InvalidOperationException(
                        $"Stage '{stageName}' objective uses unknown completion policy value {(int)completionPolicy}.");
            }
        }

        private static int CountRequiredEntries(IReadOnlyList<StageObjectiveConditionEntry> conditionEntries)
        {
            var count = 0;
            if (conditionEntries != null)
            {
                for (var i = 0; i < conditionEntries.Count; i++)
                {
                    if (conditionEntries[i].Required)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static bool ContainsOnlyTimeLimitEntries(
            IReadOnlyList<StageObjectiveConditionEntry> conditionEntries)
        {
            var requiredConditionCount = CountRequiredEntries(conditionEntries);
            if (requiredConditionCount == 0)
            {
                return false;
            }

            if (conditionEntries != null)
            {
                for (var i = 0; i < conditionEntries.Count; i++)
                {
                    if (!conditionEntries[i].Required)
                    {
                        continue;
                    }

                    if (conditionEntries[i].Condition is not ClearWithinTimeLimitConditionAsset)
                    {
                        return false;
                    }
                }
            }

            return true;
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

        internal static string FormatSpawnLabel(ExplicitSpawnEntry spawnEntry)
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
                StageTileFeatureDefinition[] tileFeatures,
                int playerEntityId,
                StageZoneDefinition[] zones,
                StageObjectiveAuthoring objective)
            {
                StageName = string.IsNullOrWhiteSpace(stageName) ? "<unnamed stage>" : stageName;
                BoardBounds = boardBounds;
                InitialTopology = initialTopology;
                Spawns = spawns ?? Array.Empty<StageSpawnDefinition>();
                TileFeatures = tileFeatures ?? Array.Empty<StageTileFeatureDefinition>();
                PlayerEntityId = playerEntityId;
                Zones = zones ?? Array.Empty<StageZoneDefinition>();
                Objective = objective;
            }

            public string StageName { get; }

            public BoardBounds BoardBounds { get; }

            public CubeTopologyState InitialTopology { get; }

            public StageSpawnDefinition[] Spawns { get; }

            public StageTileFeatureDefinition[] TileFeatures { get; }

            public int PlayerEntityId { get; }

            public StageZoneDefinition[] Zones { get; }

            public StageObjectiveAuthoring Objective { get; }
        }
    }
}
