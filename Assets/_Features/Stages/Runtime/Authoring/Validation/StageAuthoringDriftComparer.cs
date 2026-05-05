using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Stages
{
    public static class StageAuthoringDriftComparer
    {
        public static StageValidationIssue[] CompareGameplay(
            StageAuthoringNormalizedGameplaySnapshot expected,
            StageAuthoringNormalizedGameplaySnapshot actual,
            StageAuthoringDriftContext context)
        {
            var issues = new List<StageValidationIssue>();
            if (expected == null || actual == null)
            {
                return issues.ToArray();
            }

            CompareBoard(expected.Board, actual.Board, context, issues);
            CompareSpawns(expected.Spawns, actual.Spawns, context, issues);
            CompareTileFeatures(expected.TileFeatures, actual.TileFeatures, context, issues);
            CompareZones(expected.Zones, actual.Zones, context, issues);
            CompareObjective(expected.Objective, actual.Objective, context, issues);
            return issues.ToArray();
        }

        public static StageValidationIssue[] ComparePresentation(
            StageAuthoringNormalizedPresentationSnapshot expected,
            StageAuthoringNormalizedPresentationSnapshot actual,
            StageAuthoringDriftContext context)
        {
            var issues = new List<StageValidationIssue>();
            if (expected == null || actual == null)
            {
                return issues.ToArray();
            }

            CompareBindings("EnemyPresentationBindings", expected.EnemyBindings, actual.EnemyBindings, context, issues);
            CompareBindings("StaticEntityPresentationBindings", expected.StaticBindings, actual.StaticBindings, context, issues);
            return issues.ToArray();
        }

        private static void CompareBoard(
            StageBoardDefinition expected,
            StageBoardDefinition actual,
            StageAuthoringDriftContext context,
            ICollection<StageValidationIssue> issues)
        {
            AddIfDifferent(issues, context, "GameplayDrift.BoardMismatch", "Board.MinInclusive", expected.MinInclusive, actual.MinInclusive);
            AddIfDifferent(issues, context, "GameplayDrift.BoardMismatch", "Board.MaxInclusive", expected.MaxInclusive, actual.MaxInclusive);
            AddIfDifferent(issues, context, "GameplayDrift.BoardMismatch", "Board.InitialBottomFace", expected.InitialBottomFace, actual.InitialBottomFace);
        }

        private static void CompareSpawns(
            IReadOnlyList<StageAuthoringNormalizedSpawn> expected,
            IReadOnlyList<StageAuthoringNormalizedSpawn> actual,
            StageAuthoringDriftContext context,
            ICollection<StageValidationIssue> issues)
        {
            var actualByKey = new Dictionary<string, StageAuthoringNormalizedSpawn>(StringComparer.Ordinal);
            for (var i = 0; i < actual.Count; i++)
            {
                actualByKey[SpawnKey(actual[i])] = actual[i];
            }

            var expectedKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < expected.Count; i++)
            {
                var expectedSpawn = expected[i];
                var key = SpawnKey(expectedSpawn);
                expectedKeys.Add(key);
                if (!actualByKey.TryGetValue(key, out var actualSpawn))
                {
                    issues.Add(CreateIssue(
                        context,
                        "GameplayDrift.SpawnMissing",
                        $"Expected {expectedSpawn.Kind} spawn EntityId={expectedSpawn.EntityId} is missing.",
                        "Spawn",
                        FormatSpawn(expectedSpawn),
                        string.Empty,
                        expectedSpawn.EntityId,
                        expectedSpawn.StableGuid));
                    continue;
                }

                CompareSpawnFields(expectedSpawn, actualSpawn, context, issues);
            }

            for (var i = 0; i < actual.Count; i++)
            {
                var actualSpawn = actual[i];
                if (expectedKeys.Contains(SpawnKey(actualSpawn)))
                {
                    continue;
                }

                issues.Add(CreateIssue(
                    context,
                    "GameplayDrift.SpawnUnexpected",
                    $"Unexpected {actualSpawn.Kind} spawn EntityId={actualSpawn.EntityId} exists.",
                    "Spawn",
                    string.Empty,
                    FormatSpawn(actualSpawn),
                    actualSpawn.EntityId,
                    string.Empty));
            }
        }

        private static void CompareSpawnFields(
            StageAuthoringNormalizedSpawn expected,
            StageAuthoringNormalizedSpawn actual,
            StageAuthoringDriftContext context,
            ICollection<StageValidationIssue> issues)
        {
            AddIfDifferent(issues, context, "GameplayDrift.SpawnFieldMismatch", "Cell", expected.Cell, actual.Cell, expected.EntityId, expected.StableGuid);
            AddIfDifferent(issues, context, "GameplayDrift.SpawnFieldMismatch", "Hp", expected.Hp, actual.Hp, expected.EntityId, expected.StableGuid);

            if (expected.Kind == StageSpawnKind.Player ||
                expected.Kind == StageSpawnKind.Enemy ||
                expected.Kind == StageSpawnKind.Box)
            {
                AddIfDifferent(issues, context, "GameplayDrift.SpawnFieldMismatch", "Facing", expected.Facing, actual.Facing, expected.EntityId, expected.StableGuid);
            }

            if (expected.Kind == StageSpawnKind.Player ||
                expected.Kind == StageSpawnKind.Enemy)
            {
                AddIfDifferent(issues, context, "GameplayDrift.SpawnFieldMismatch", "UnitStackGroup", expected.UnitStackGroup, actual.UnitStackGroup, expected.EntityId, expected.StableGuid);
            }

            if (expected.Kind == StageSpawnKind.Enemy)
            {
                AddIfDifferent(issues, context, "GameplayDrift.SpawnFieldMismatch", "EnemyAiMode", expected.EnemyAiMode, actual.EnemyAiMode, expected.EntityId, expected.StableGuid);
                AddIfDifferent(issues, context, "GameplayDrift.SpawnFieldMismatch", "EnemyAiStateTimer", expected.EnemyAiStateTimer, actual.EnemyAiStateTimer, expected.EntityId, expected.StableGuid);
                if (expected.EnemyAiProfile != actual.EnemyAiProfile)
                {
                    AddMismatch(issues, context, "GameplayDrift.SpawnFieldMismatch", "EnemyAiProfile", FormatObject(expected.EnemyAiProfile), FormatObject(actual.EnemyAiProfile), expected.EntityId, expected.StableGuid);
                }
            }

            if (expected.Kind == StageSpawnKind.Box)
            {
                AddIfDifferent(issues, context, "GameplayDrift.SpawnFieldMismatch", "BoxCapabilities", expected.BoxCapabilities, actual.BoxCapabilities, expected.EntityId, expected.StableGuid);
            }
        }

        private static void CompareZones(
            IReadOnlyList<StageAuthoringNormalizedZone> expected,
            IReadOnlyList<StageAuthoringNormalizedZone> actual,
            StageAuthoringDriftContext context,
            ICollection<StageValidationIssue> issues)
        {
            if (expected.Count != actual.Count)
            {
                AddMismatch(issues, context, "GameplayDrift.ZoneMismatch", "Zones.Count", expected.Count.ToString(), actual.Count.ToString());
                return;
            }

            for (var i = 0; i < expected.Count; i++)
            {
                var expectedZone = expected[i];
                var actualZone = actual[i];
                AddIfDifferent(issues, context, "GameplayDrift.ZoneMismatch", $"Zones[{i}].ZoneId", expectedZone.ZoneId, actualZone.ZoneId);
                AddIfDifferent(issues, context, "GameplayDrift.ZoneMismatch", $"Zones[{expectedZone.ZoneId}].FaceId", expectedZone.FaceId, actualZone.FaceId);
                if (expectedZone.Regions.Length != actualZone.Regions.Length)
                {
                    AddMismatch(issues, context, "GameplayDrift.ZoneMismatch", $"Zones[{expectedZone.ZoneId}].Regions.Count", expectedZone.Regions.Length.ToString(), actualZone.Regions.Length.ToString());
                    continue;
                }

                for (var regionIndex = 0; regionIndex < expectedZone.Regions.Length; regionIndex++)
                {
                    AddIfDifferent(
                        issues,
                        context,
                        "GameplayDrift.ZoneMismatch",
                        $"Zones[{expectedZone.ZoneId}].Regions[{regionIndex}].MinInclusive",
                        expectedZone.Regions[regionIndex].MinInclusive,
                        actualZone.Regions[regionIndex].MinInclusive);
                    AddIfDifferent(
                        issues,
                        context,
                        "GameplayDrift.ZoneMismatch",
                        $"Zones[{expectedZone.ZoneId}].Regions[{regionIndex}].MaxInclusive",
                        expectedZone.Regions[regionIndex].MaxInclusive,
                        actualZone.Regions[regionIndex].MaxInclusive);
                }
            }
        }

        private static void CompareTileFeatures(
            IReadOnlyList<StageAuthoringNormalizedTileFeature> expected,
            IReadOnlyList<StageAuthoringNormalizedTileFeature> actual,
            StageAuthoringDriftContext context,
            ICollection<StageValidationIssue> issues)
        {
            var actualByTileId = new Dictionary<int, StageAuthoringNormalizedTileFeature>();
            for (var i = 0; i < actual.Count; i++)
            {
                actualByTileId[actual[i].TileId] = actual[i];
            }

            var expectedTileIds = new HashSet<int>();
            for (var i = 0; i < expected.Count; i++)
            {
                var expectedTileFeature = expected[i];
                expectedTileIds.Add(expectedTileFeature.TileId);
                if (!actualByTileId.TryGetValue(expectedTileFeature.TileId, out var actualTileFeature))
                {
                    issues.Add(CreateIssue(
                        context,
                        "GameplayDrift.TileFeatureMissing",
                        $"Expected TileFeature TileId={expectedTileFeature.TileId} is missing.",
                        "TileFeatures",
                        FormatTileFeature(expectedTileFeature),
                        string.Empty,
                        expectedTileFeature.TileId,
                        string.Empty));
                    continue;
                }

                CompareTileFeatureFields(expectedTileFeature, actualTileFeature, context, issues);
            }

            for (var i = 0; i < actual.Count; i++)
            {
                var actualTileFeature = actual[i];
                if (expectedTileIds.Contains(actualTileFeature.TileId))
                {
                    continue;
                }

                issues.Add(CreateIssue(
                    context,
                    "GameplayDrift.TileFeatureUnexpected",
                    $"Unexpected TileFeature TileId={actualTileFeature.TileId} exists.",
                    "TileFeatures",
                    string.Empty,
                    FormatTileFeature(actualTileFeature),
                    actualTileFeature.TileId,
                    string.Empty));
            }
        }

        private static void CompareTileFeatureFields(
            StageAuthoringNormalizedTileFeature expected,
            StageAuthoringNormalizedTileFeature actual,
            StageAuthoringDriftContext context,
            ICollection<StageValidationIssue> issues)
        {
            AddIfDifferent(issues, context, "GameplayDrift.TileFeatureFieldMismatch", $"TileFeatures[{expected.TileId}].Cell", expected.Cell, actual.Cell, expected.TileId);
            AddIfDifferent(issues, context, "GameplayDrift.TileFeatureFieldMismatch", $"TileFeatures[{expected.TileId}].Kind", expected.Kind, actual.Kind, expected.TileId);
            AddIfDifferent(issues, context, "GameplayDrift.TileFeatureFieldMismatch", $"TileFeatures[{expected.TileId}].ActivationRule", expected.ActivationRule, actual.ActivationRule, expected.TileId);
            AddIfDifferent(issues, context, "GameplayDrift.TileFeatureFieldMismatch", $"TileFeatures[{expected.TileId}].Direction", expected.Direction, actual.Direction, expected.TileId);
            AddIfDifferent(issues, context, "GameplayDrift.TileFeatureFieldMismatch", $"TileFeatures[{expected.TileId}].BoxSelector", expected.BoxSelector, actual.BoxSelector, expected.TileId);
            AddIfDifferent(issues, context, "GameplayDrift.TileFeatureFieldMismatch", $"TileFeatures[{expected.TileId}].BoundEntityId", expected.BoundEntityId, actual.BoundEntityId, expected.TileId);
            AddIfDifferent(issues, context, "GameplayDrift.TileFeatureFieldMismatch", $"TileFeatures[{expected.TileId}].PresentationKey", expected.PresentationKey, actual.PresentationKey, expected.TileId);
        }

        private static void CompareObjective(
            StageAuthoringNormalizedObjective expected,
            StageAuthoringNormalizedObjective actual,
            StageAuthoringDriftContext context,
            ICollection<StageValidationIssue> issues)
        {
            AddIfDifferent(issues, context, "GameplayDrift.ObjectiveMismatch", "Objective.CompletionPolicy", expected.CompletionPolicy, actual.CompletionPolicy);
            if (expected.Conditions.Length != actual.Conditions.Length)
            {
                AddMismatch(issues, context, "GameplayDrift.ObjectiveMismatch", "Objective.ConditionEntries.Count", expected.Conditions.Length.ToString(), actual.Conditions.Length.ToString());
                return;
            }

            for (var i = 0; i < expected.Conditions.Length; i++)
            {
                var expectedCondition = expected.Conditions[i];
                var actualCondition = actual.Conditions[i];
                if (expectedCondition.Condition != actualCondition.Condition)
                {
                    AddMismatch(issues, context, "GameplayDrift.ObjectiveMismatch", $"Objective.ConditionEntries[{i}].Condition", FormatObject(expectedCondition.Condition), FormatObject(actualCondition.Condition));
                }

                AddIfDifferent(issues, context, "GameplayDrift.ObjectiveMismatch", $"Objective.ConditionEntries[{i}].Required", expectedCondition.Required, actualCondition.Required);
                AddIfDifferent(issues, context, "GameplayDrift.ObjectiveMismatch", $"Objective.ConditionEntries[{i}].Role", expectedCondition.Role, actualCondition.Role);
                AddIfDifferent(issues, context, "GameplayDrift.ObjectiveMismatch", $"Objective.ConditionEntries[{i}].StableConditionId", expectedCondition.StableConditionId, actualCondition.StableConditionId);
            }
        }

        private static void CompareBindings(
            string fieldPrefix,
            IReadOnlyList<StageAuthoringNormalizedPresentationBinding> expected,
            IReadOnlyList<StageAuthoringNormalizedPresentationBinding> actual,
            StageAuthoringDriftContext context,
            ICollection<StageValidationIssue> issues)
        {
            var actualByEntityId = new Dictionary<int, StageAuthoringNormalizedPresentationBinding>();
            for (var i = 0; i < actual.Count; i++)
            {
                actualByEntityId[actual[i].EntityId] = actual[i];
            }

            var expectedEntityIds = new HashSet<int>();
            for (var i = 0; i < expected.Count; i++)
            {
                var expectedBinding = expected[i];
                expectedEntityIds.Add(expectedBinding.EntityId);
                if (!actualByEntityId.TryGetValue(expectedBinding.EntityId, out var actualBinding))
                {
                    issues.Add(CreateIssue(
                        context,
                        "PresentationDrift.BindingMissing",
                        $"Expected presentation binding EntityId={expectedBinding.EntityId} is missing.",
                        fieldPrefix,
                        expectedBinding.PresentationId,
                        string.Empty,
                        expectedBinding.EntityId,
                        string.Empty));
                    continue;
                }

                AddIfDifferent(
                    issues,
                    context,
                    "PresentationDrift.BindingFieldMismatch",
                    $"{fieldPrefix}[{expectedBinding.EntityId}].PresentationId",
                    expectedBinding.PresentationId,
                    actualBinding.PresentationId,
                    expectedBinding.EntityId,
                    string.Empty);
            }

            for (var i = 0; i < actual.Count; i++)
            {
                var actualBinding = actual[i];
                if (expectedEntityIds.Contains(actualBinding.EntityId))
                {
                    continue;
                }

                issues.Add(CreateIssue(
                    context,
                    "PresentationDrift.BindingUnexpected",
                    $"Unexpected presentation binding EntityId={actualBinding.EntityId} exists.",
                    fieldPrefix,
                    string.Empty,
                    actualBinding.PresentationId,
                    actualBinding.EntityId,
                    string.Empty));
            }
        }

        private static void AddIfDifferent<T>(
            ICollection<StageValidationIssue> issues,
            StageAuthoringDriftContext context,
            string code,
            string fieldName,
            T expected,
            T actual,
            int entityId = 0,
            string stableGuid = "")
        {
            if (EqualityComparer<T>.Default.Equals(expected, actual))
            {
                return;
            }

            AddMismatch(issues, context, code, fieldName, FormatValue(expected), FormatValue(actual), entityId, stableGuid);
        }

        private static void AddMismatch(
            ICollection<StageValidationIssue> issues,
            StageAuthoringDriftContext context,
            string code,
            string fieldName,
            string expected,
            string actual,
            int entityId = 0,
            string stableGuid = "")
        {
            issues.Add(CreateIssue(
                context,
                code,
                $"{fieldName} drift: expected '{expected}', actual '{actual}'.",
                fieldName,
                expected,
                actual,
                entityId,
                stableGuid));
        }

        private static StageValidationIssue CreateIssue(
            StageAuthoringDriftContext context,
            string code,
            string message,
            string fieldName,
            string expected,
            string actual,
            int entityId,
            string stableGuid)
        {
            return new StageValidationIssue(
                context.Severity,
                code,
                message,
                context.Context,
                context.AssetPath,
                context.Timing,
                context.StageId,
                context.AuthoringAssetName,
                context.OutputAssetName,
                entityId,
                stableGuid,
                fieldName,
                expected,
                actual);
        }

        private static string SpawnKey(StageAuthoringNormalizedSpawn spawn)
        {
            return $"{spawn.Kind}:{spawn.EntityId}";
        }

        private static string FormatSpawn(StageAuthoringNormalizedSpawn spawn)
        {
            return $"{spawn.Kind} EntityId={spawn.EntityId} Cell={spawn.Cell} Hp={spawn.Hp}";
        }

        private static string FormatTileFeature(StageAuthoringNormalizedTileFeature tileFeature)
        {
            return $"TileId={tileFeature.TileId} Cell={tileFeature.Cell} Kind={tileFeature.Kind} ActivationRule={tileFeature.ActivationRule}";
        }

        private static string FormatValue<T>(T value)
        {
            return value == null ? string.Empty : value.ToString();
        }

        private static string FormatObject(UnityEngine.Object value)
        {
            return value == null ? string.Empty : value.name;
        }
    }

    public readonly struct StageAuthoringDriftContext
    {
        public StageAuthoringDriftContext(
            StageValidationSeverity severity,
            StageValidationTiming timing,
            UnityEngine.Object context,
            string assetPath,
            string stageId,
            string authoringAssetName,
            string outputAssetName)
        {
            Severity = severity;
            Timing = timing;
            Context = context;
            AssetPath = assetPath ?? string.Empty;
            StageId = stageId ?? string.Empty;
            AuthoringAssetName = authoringAssetName ?? string.Empty;
            OutputAssetName = outputAssetName ?? string.Empty;
        }

        public StageValidationSeverity Severity { get; }

        public StageValidationTiming Timing { get; }

        public UnityEngine.Object Context { get; }

        public string AssetPath { get; }

        public string StageId { get; }

        public string AuthoringAssetName { get; }

        public string OutputAssetName { get; }
    }
}
