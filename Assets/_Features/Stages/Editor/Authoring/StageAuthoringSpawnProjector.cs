using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;

namespace Game.Feature.Stages.Editor
{
    internal static class StageAuthoringSpawnProjector
    {
        public static StageAuthoringBuildData Project(
            StageAuthoringDefinition source,
            IReadOnlyDictionary<string, int> entityIdsByStableGuid,
            StageAuthoringGenerationReport report)
        {
            var buildData = new StageAuthoringBuildData(source.Board, source.Objective, source.Zones, entityIdsByStableGuid);
            var placements = source.Placements;
            var seenStableGuids = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                if (placement == null)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "authoring.placement.null",
                        $"Placement[{i}] is null.",
                        source,
                        string.Empty);
                    continue;
                }

                var normalizedGuid = Normalize(placement.StableGuid);
                if (string.IsNullOrEmpty(normalizedGuid))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "authoring.stable-guid.empty",
                        $"Placement[{i}] must declare a non-empty stable authoring guid.",
                        source,
                        string.Empty);
                    continue;
                }

                if (!seenStableGuids.Add(normalizedGuid))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "authoring.stable-guid.duplicate",
                        $"Duplicate placement stable authoring guid '{normalizedGuid}'.",
                        source,
                        string.Empty);
                    continue;
                }

                if (!Enum.IsDefined(typeof(StageAuthoringEntityKind), placement.Kind))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "authoring.kind.invalid",
                        $"Placement '{normalizedGuid}' has invalid kind value {(int)placement.Kind}.",
                        source,
                        string.Empty);
                    continue;
                }

                if (!Enum.IsDefined(typeof(FaceId), placement.Cell.face))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "authoring.surface-cell.face-invalid",
                        $"Placement '{normalizedGuid}' has invalid face value {(int)placement.Cell.face}.",
                        source,
                        string.Empty);
                    continue;
                }

                if (!Enum.IsDefined(typeof(Direction), placement.Facing))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "authoring.facing.invalid",
                        $"Placement '{normalizedGuid}' has invalid facing value {(int)placement.Facing}.",
                        source,
                        string.Empty);
                    continue;
                }

                if (!Enum.IsDefined(typeof(BoxArchetype), placement.BoxArchetype))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "authoring.box-archetype.invalid",
                        $"Placement '{normalizedGuid}' has invalid BoxArchetype value {(int)placement.BoxArchetype}.",
                        source,
                        string.Empty);
                    continue;
                }

                if (IsUnitPlacement(placement.Kind) &&
                    !Enum.IsDefined(typeof(UnitMobilityKind), placement.UnitMobilityKind))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "authoring.unit-mobility-kind.invalid",
                        $"Placement '{normalizedGuid}' has invalid UnitMobilityKind value {(int)placement.UnitMobilityKind}.",
                        source,
                        string.Empty);
                    continue;
                }

                if (placement.Hp <= 0)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "authoring.hp.non-positive",
                        $"Placement '{normalizedGuid}' must use positive Hp.",
                        source,
                        string.Empty);
                    continue;
                }

                if (!entityIdsByStableGuid.TryGetValue(normalizedGuid, out var entityId) || entityId <= 0)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "authoring.entity-id.missing",
                        $"Placement '{normalizedGuid}' does not have a positive generated EntityId mapping.",
                        source,
                        string.Empty);
                    continue;
                }

                if (placement.Kind == StageAuthoringEntityKind.Player)
                {
                    buildData.PlayerPlacementCount++;
                }

                var normalizedPlacement = placement.Clone();
                normalizedPlacement.StableGuid = normalizedGuid;
                normalizedPlacement.DisplayName = Normalize(placement.DisplayName);
                normalizedPlacement.UnitStackGroup = Normalize(placement.UnitStackGroup);
                normalizedPlacement.PresentationId = Normalize(placement.PresentationId);
                normalizedPlacement.UnitMobilityKind = NormalizeUnitMobility(placement.Kind, placement.UnitMobilityKind);
                buildData.Placements.Add(normalizedPlacement);

                var spawn = new StageSpawnDefinition
                {
                    EntityId = entityId,
                    Kind = ToSpawnKind(placement.Kind),
                    Cell = placement.Cell,
                    Facing = placement.Facing,
                    Hp = placement.Hp,
                    UnitMobilityKind = NormalizeUnitMobility(placement.Kind, placement.UnitMobilityKind),
                    BoxCapabilities = placement.BoxCapabilities,
                    BoxArchetype = placement.BoxArchetype,
                    EnemyAiMode = placement.EnemyAiMode,
                    EnemyAiStateTimer = placement.EnemyAiStateTimer,
                    EnemyAiProfile = placement.EnemyAiProfileOverride,
                    PresentationId = string.Empty,
                    UnitStackGroup = Normalize(placement.UnitStackGroup),
                };

                switch (placement.Kind)
                {
                    case StageAuthoringEntityKind.Player:
                        buildData.PlayerSpawns.Add(spawn);
                        break;
                    case StageAuthoringEntityKind.Enemy:
                        buildData.EnemySpawns.Add(spawn);
                        break;
                    case StageAuthoringEntityKind.Box:
                        buildData.BoxSpawns.Add(spawn);
                        break;
                    case StageAuthoringEntityKind.Wall:
                        buildData.WallSpawns.Add(spawn);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(placement.Kind), placement.Kind, null);
                }
            }

            SortByEntityId(buildData.PlayerSpawns);
            SortByEntityId(buildData.EnemySpawns);
            SortByEntityId(buildData.BoxSpawns);
            SortByEntityId(buildData.WallSpawns);
            AddTileFeatures(source.TileFeatures, buildData.TileFeatures);
            return buildData;
        }

        public static void ValidatePlayerCount(
            StageAuthoringDefinition source,
            StageAuthoringBuildData buildData,
            StageAuthoringGenerationReport report)
        {
            if (buildData.PlayerPlacementCount == 1)
            {
                return;
            }

            report.Add(
                StageValidationSeverity.Error,
                "authoring.player-count.invalid",
                $"Stage authoring must contain exactly one player placement, but found {buildData.PlayerPlacementCount}.",
                source,
                string.Empty);
        }

        private static StageSpawnKind ToSpawnKind(StageAuthoringEntityKind kind)
        {
            return kind switch
            {
                StageAuthoringEntityKind.Player => StageSpawnKind.Player,
                StageAuthoringEntityKind.Enemy => StageSpawnKind.Enemy,
                StageAuthoringEntityKind.Box => StageSpawnKind.Box,
                StageAuthoringEntityKind.Wall => StageSpawnKind.Wall,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
            };
        }

        private static bool IsUnitPlacement(StageAuthoringEntityKind kind)
        {
            return kind == StageAuthoringEntityKind.Player ||
                   kind == StageAuthoringEntityKind.Enemy;
        }

        private static UnitMobilityKind NormalizeUnitMobility(
            StageAuthoringEntityKind kind,
            UnitMobilityKind unitMobilityKind)
        {
            if (!IsUnitPlacement(kind))
            {
                return UnitMobilityKind.Ground;
            }

            return Enum.IsDefined(typeof(UnitMobilityKind), unitMobilityKind)
                ? unitMobilityKind
                : UnitMobilityKind.Ground;
        }

        private static void SortByEntityId(List<StageSpawnDefinition> spawns)
        {
            spawns.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        private static void AddTileFeatures(
            IReadOnlyList<StageTileFeatureDefinition> source,
            List<StageTileFeatureDefinition> target)
        {
            target.Clear();
            if (source == null)
            {
                return;
            }

            for (var i = 0; i < source.Count; i++)
            {
                var tileFeature = source[i];
                tileFeature.PresentationKey = Normalize(tileFeature.PresentationKey);
                target.Add(tileFeature);
            }

            target.Sort((left, right) => left.TileId.CompareTo(right.TileId));
        }

        private static string Normalize(string value)
        {
            return StageAuthoringGenerator.Normalize(value);
        }
    }
}
