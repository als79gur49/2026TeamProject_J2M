using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    public static class StageAuthoringGenerator
    {
        public static StageAuthoringGenerationReport Generate(
            StageAuthoringDefinition source,
            StageAuthoringGenerateOptions options)
        {
            return Generate(
                source,
                source != null ? source.GeneratedGameplayDefinition : null,
                source != null ? source.GeneratedPresentationDefinition : null,
                options);
        }

        public static StageAuthoringGenerationReport Generate(
            StageAuthoringDefinition source,
            StageDefinition gameplayOutput,
            StagePresentationDefinition presentationOutput,
            StageAuthoringGenerateOptions options)
        {
            options ??= StageAuthoringGenerateOptions.WriteAll;
            var report = new StageAuthoringGenerationReport();
            if (source == null)
            {
                report.Add(StageValidationSeverity.Error, "authoring.source.null", "StageAuthoringDefinition cannot be null.");
                return report;
            }

            gameplayOutput ??= source.GeneratedGameplayDefinition;
            presentationOutput ??= source.GeneratedPresentationDefinition;
            if ((options.WriteGameplay || options.ValidateAfterGenerate) && gameplayOutput == null)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "authoring.output.gameplay-null",
                    "Stage authoring generation requires a generated gameplay StageDefinition.",
                    source,
                    AssetDatabase.GetAssetPath(source));
            }

            if ((options.WritePresentationBindings || options.ValidateAfterGenerate) && presentationOutput == null)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "authoring.output.presentation-null",
                    "Stage authoring generation requires a generated StagePresentationDefinition.",
                    source,
                    AssetDatabase.GetAssetPath(source));
            }

            var allocation = StageAuthoringEntityIdAllocator.Allocate(source, report);
            if (report.HasErrors)
            {
                return report;
            }

            var buildData = BuildGeneratedData(source, presentationOutput, allocation.EntityIdsByStableGuid, report);
            if (report.HasErrors)
            {
                return report;
            }

            ValidateGeneratedGameplay(buildData, report);
            ValidateGeneratedPresentation(presentationOutput, buildData, report);
            if (report.HasErrors)
            {
                return report;
            }

            if (!options.DryRun)
            {
                ApplyMappings(source, allocation.Mappings);
                if (options.WriteGameplay)
                {
                    ApplyGameplayOutput(gameplayOutput, buildData);
                }

                if (options.WritePresentationBindings)
                {
                    ApplyPresentationOutput(presentationOutput, buildData);
                }

                AssetDatabase.SaveAssets();
            }

            for (var i = 0; i < buildData.Placements.Count; i++)
            {
                var placement = buildData.Placements[i];
                report.RecordEntityId(placement.StableGuid, buildData.EntityIdsByStableGuid[placement.StableGuid]);
            }

            return report;
        }

        internal static StageAuthoringBuildData BuildExpectedDataForComparison(
            StageAuthoringDefinition source,
            StagePresentationDefinition presentationOutput,
            StageAuthoringGenerationReport report)
        {
            var allocation = StageAuthoringEntityIdAllocator.Allocate(source, report);
            if (report.HasErrors)
            {
                return null;
            }

            return BuildGeneratedData(source, presentationOutput, allocation.EntityIdsByStableGuid, report);
        }

        private static StageAuthoringBuildData BuildGeneratedData(
            StageAuthoringDefinition source,
            StagePresentationDefinition presentationOutput,
            IReadOnlyDictionary<string, int> entityIdsByStableGuid,
            StageAuthoringGenerationReport report)
        {
            var buildData = new StageAuthoringBuildData(source.Board, source.Objective, source.Zones, entityIdsByStableGuid);
            var placements = source.Placements;
            var seenStableGuids = new HashSet<string>(StringComparer.Ordinal);
            var playerCount = 0;

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
                        AssetDatabase.GetAssetPath(source));
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
                        AssetDatabase.GetAssetPath(source));
                    continue;
                }

                if (!seenStableGuids.Add(normalizedGuid))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "authoring.stable-guid.duplicate",
                        $"Duplicate placement stable authoring guid '{normalizedGuid}'.",
                        source,
                        AssetDatabase.GetAssetPath(source));
                    continue;
                }

                if (!Enum.IsDefined(typeof(StageAuthoringEntityKind), placement.Kind))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "authoring.kind.invalid",
                        $"Placement '{normalizedGuid}' has invalid kind value {(int)placement.Kind}.",
                        source,
                        AssetDatabase.GetAssetPath(source));
                    continue;
                }

                if (!Enum.IsDefined(typeof(FaceId), placement.Cell.face))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "authoring.surface-cell.face-invalid",
                        $"Placement '{normalizedGuid}' has invalid face value {(int)placement.Cell.face}.",
                        source,
                        AssetDatabase.GetAssetPath(source));
                    continue;
                }

                if (!Enum.IsDefined(typeof(Direction), placement.Facing))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "authoring.facing.invalid",
                        $"Placement '{normalizedGuid}' has invalid facing value {(int)placement.Facing}.",
                        source,
                        AssetDatabase.GetAssetPath(source));
                    continue;
                }

                if (placement.Hp <= 0)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "authoring.hp.non-positive",
                        $"Placement '{normalizedGuid}' must use positive Hp.",
                        source,
                        AssetDatabase.GetAssetPath(source));
                    continue;
                }

                if (!entityIdsByStableGuid.TryGetValue(normalizedGuid, out var entityId) || entityId <= 0)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "authoring.entity-id.missing",
                        $"Placement '{normalizedGuid}' does not have a positive generated EntityId mapping.",
                        source,
                        AssetDatabase.GetAssetPath(source));
                    continue;
                }

                if (placement.Kind == StageAuthoringEntityKind.Player)
                {
                    playerCount++;
                }

                var normalizedPlacement = placement.Clone();
                normalizedPlacement.StableGuid = normalizedGuid;
                normalizedPlacement.DisplayName = Normalize(placement.DisplayName);
                normalizedPlacement.UnitStackGroup = Normalize(placement.UnitStackGroup);
                normalizedPlacement.PresentationId = Normalize(placement.PresentationId);
                buildData.Placements.Add(normalizedPlacement);

                var spawn = new StageSpawnDefinition
                {
                    EntityId = entityId,
                    Kind = ToSpawnKind(placement.Kind),
                    Cell = placement.Cell,
                    Facing = placement.Facing,
                    Hp = placement.Hp,
                    BoxCapabilities = placement.BoxCapabilities,
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
                        TryAddEnemyPresentationBinding(source, presentationOutput, placement, entityId, buildData, report);
                        break;
                    case StageAuthoringEntityKind.Box:
                    case StageAuthoringEntityKind.Wall:
                        if (placement.Kind == StageAuthoringEntityKind.Box)
                        {
                            buildData.BoxSpawns.Add(spawn);
                        }
                        else
                        {
                            buildData.WallSpawns.Add(spawn);
                        }

                        TryAddStaticPresentationBinding(source, presentationOutput, placement, entityId, buildData, report);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(placement.Kind), placement.Kind, null);
                }
            }

            if (playerCount != 1)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "authoring.player-count.invalid",
                    $"Stage authoring must contain exactly one player placement, but found {playerCount}.",
                    source,
                    AssetDatabase.GetAssetPath(source));
            }

            SortByEntityId(buildData.PlayerSpawns);
            SortByEntityId(buildData.EnemySpawns);
            SortByEntityId(buildData.BoxSpawns);
            SortByEntityId(buildData.WallSpawns);
            buildData.EnemyPresentationBindings.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
            buildData.StaticEntityPresentationBindings.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
            return buildData;
        }

        private static void TryAddEnemyPresentationBinding(
            StageAuthoringDefinition source,
            StagePresentationDefinition presentationOutput,
            StagePlacedEntityAuthoring placement,
            int entityId,
            StageAuthoringBuildData buildData,
            StageAuthoringGenerationReport report)
        {
            var presentationId = Normalize(placement.PresentationId);
            if (string.IsNullOrEmpty(presentationId))
            {
                report.Add(
                    StageValidationSeverity.Warning,
                    "authoring.presentation.enemy-missing",
                    $"Enemy placement '{placement.StableGuid}' has no presentation id; runtime fallback will be used.",
                    source,
                    AssetDatabase.GetAssetPath(source));
                return;
            }

            if (presentationOutput == null || !ContainsEnemyPresentationId(presentationOutput.EnemyPresentationCatalog, presentationId))
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "authoring.presentation.enemy-invalid",
                    $"Enemy placement '{placement.StableGuid}' references unresolved presentation id '{presentationId}'.",
                    source,
                    AssetDatabase.GetAssetPath(source));
                return;
            }

            buildData.EnemyPresentationBindings.Add(new EnemyPresentationBinding
            {
                EntityId = entityId,
                PresentationId = presentationId,
            });
        }

        private static void TryAddStaticPresentationBinding(
            StageAuthoringDefinition source,
            StagePresentationDefinition presentationOutput,
            StagePlacedEntityAuthoring placement,
            int entityId,
            StageAuthoringBuildData buildData,
            StageAuthoringGenerationReport report)
        {
            var presentationId = Normalize(placement.PresentationId);
            if (string.IsNullOrEmpty(presentationId))
            {
                report.Add(
                    StageValidationSeverity.Warning,
                    "authoring.presentation.static-missing",
                    $"{placement.Kind} placement '{placement.StableGuid}' has no presentation id; runtime fallback will be used.",
                    source,
                    AssetDatabase.GetAssetPath(source));
                return;
            }

            if (presentationOutput == null || !ContainsStaticPresentationId(presentationOutput.StaticEntityPresentationCatalog, presentationId))
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "authoring.presentation.static-invalid",
                    $"{placement.Kind} placement '{placement.StableGuid}' references unresolved presentation id '{presentationId}'.",
                    source,
                    AssetDatabase.GetAssetPath(source));
                return;
            }

            buildData.StaticEntityPresentationBindings.Add(new StaticEntityPresentationBinding
            {
                EntityId = entityId,
                PresentationId = presentationId,
            });
        }

        private static void ValidateGeneratedGameplay(
            StageAuthoringBuildData buildData,
            StageAuthoringGenerationReport report)
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            try
            {
                stage.name = "StageAuthoring_GeneratedPreview";
                ApplyGameplayOutput(stage, buildData, recordUndo: false, markDirty: false);
                StageDefinitionValidator.Validate(stage);
                StageRuntimeBuilder.Build(stage);
            }
            catch (Exception exception)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "authoring.generated-gameplay.invalid",
                    $"Generated gameplay StageDefinition is invalid: {exception.Message}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        private static void ValidateGeneratedPresentation(
            StagePresentationDefinition presentationOutput,
            StageAuthoringBuildData buildData,
            StageAuthoringGenerationReport report)
        {
            if (presentationOutput == null)
            {
                return;
            }

            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            try
            {
                presentation.name = "StageAuthoring_PresentationPreview";
                presentation.ApplyResolvedData(StagePresentationAssembler.Resolve(presentationOutput));
                ApplyPresentationOutput(presentation, buildData, recordUndo: false, markDirty: false);
                StagePresentationAssembler.Resolve(presentation);
            }
            catch (Exception exception)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "authoring.generated-presentation.invalid",
                    $"Generated presentation data is invalid: {exception.Message}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(presentation);
            }
        }

        private static void ApplyMappings(
            StageAuthoringDefinition source,
            IReadOnlyList<StageAuthoringIdMapping> mappings)
        {
            Undo.RecordObject(source, "Generate Stage Authoring Entity IDs");
            source.SetEntityIdMappings(mappings);
            EditorUtility.SetDirty(source);
        }

        internal static void ApplyGameplayOutput(
            StageDefinition stage,
            StageAuthoringBuildData buildData,
            bool recordUndo = true,
            bool markDirty = true)
        {
            if (recordUndo)
            {
                Undo.RecordObject(stage, "Generate Stage Gameplay Definition");
            }

            var serializedObject = new SerializedObject(stage);
            SetBoard(serializedObject.FindProperty("board"), buildData.Board);
            SetSpawnArray(serializedObject.FindProperty("playerSpawns"), buildData.PlayerSpawns);
            SetSpawnArray(serializedObject.FindProperty("enemySpawns"), buildData.EnemySpawns);
            SetSpawnArray(serializedObject.FindProperty("boxSpawns"), buildData.BoxSpawns);
            SetSpawnArray(serializedObject.FindProperty("wallSpawns"), buildData.WallSpawns);
            SetZoneArray(serializedObject.FindProperty("zones"), buildData.Zones);
            SetObjective(serializedObject.FindProperty("objective"), buildData.Objective);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            if (markDirty)
            {
                EditorUtility.SetDirty(stage);
            }
        }

        internal static void ApplyPresentationOutput(
            StagePresentationDefinition presentation,
            StageAuthoringBuildData buildData,
            bool recordUndo = true,
            bool markDirty = true)
        {
            if (recordUndo)
            {
                Undo.RecordObject(presentation, "Generate Stage Presentation Bindings");
            }

            var serializedObject = new SerializedObject(presentation);
            SetEnemyBindingArray(
                serializedObject.FindProperty("enemyPresentationBindings"),
                buildData.EnemyPresentationBindings);
            SetStaticBindingArray(
                serializedObject.FindProperty("staticEntityPresentationBindings"),
                buildData.StaticEntityPresentationBindings);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            if (markDirty)
            {
                EditorUtility.SetDirty(presentation);
            }
        }

        private static void SetBoard(SerializedProperty property, StageBoardDefinition board)
        {
            property.FindPropertyRelative("MinInclusive").vector2IntValue = board.MinInclusive;
            property.FindPropertyRelative("MaxInclusive").vector2IntValue = board.MaxInclusive;
            property.FindPropertyRelative("InitialBottomFace").intValue = (int)board.InitialBottomFace;
        }

        private static void SetSpawnArray(
            SerializedProperty property,
            IReadOnlyList<StageSpawnDefinition> spawns)
        {
            property.arraySize = spawns.Count;
            for (var i = 0; i < spawns.Count; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                var spawn = spawns[i];
                element.FindPropertyRelative("EntityId").intValue = spawn.EntityId;
                element.FindPropertyRelative("Kind").intValue = (int)spawn.Kind;
                SetSurfaceCell(element.FindPropertyRelative("Cell"), spawn.Cell);
                element.FindPropertyRelative("Facing").intValue = (int)spawn.Facing;
                element.FindPropertyRelative("Hp").intValue = spawn.Hp;
                element.FindPropertyRelative("BoxCapabilities").intValue = (int)spawn.BoxCapabilities;
                element.FindPropertyRelative("EnemyAiMode").intValue = (int)spawn.EnemyAiMode;
                element.FindPropertyRelative("EnemyAiStateTimer").intValue = spawn.EnemyAiStateTimer;
                element.FindPropertyRelative("EnemyAiProfile").objectReferenceValue = spawn.EnemyAiProfile;
                element.FindPropertyRelative("PresentationId").stringValue = string.Empty;
                element.FindPropertyRelative("UnitStackGroup").stringValue = Normalize(spawn.UnitStackGroup);
            }
        }

        private static void SetSurfaceCell(SerializedProperty property, SurfaceCell cell)
        {
            property.FindPropertyRelative("face").intValue = (int)cell.face;
            property.FindPropertyRelative("x").intValue = cell.x;
            property.FindPropertyRelative("y").intValue = cell.y;
        }

        private static void SetZoneArray(
            SerializedProperty property,
            IReadOnlyList<StageZoneDefinition> zones)
        {
            property.arraySize = zones.Count;
            for (var i = 0; i < zones.Count; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                var zone = zones[i];
                element.FindPropertyRelative("ZoneId").stringValue = Normalize(zone.ZoneId);
                element.FindPropertyRelative("FaceId").intValue = (int)zone.FaceId;
                var regions = zone.GetRegionsOrEmpty();
                var regionsProperty = element.FindPropertyRelative("Regions");
                regionsProperty.arraySize = regions.Length;
                for (var regionIndex = 0; regionIndex < regions.Length; regionIndex++)
                {
                    var regionProperty = regionsProperty.GetArrayElementAtIndex(regionIndex);
                    regionProperty.FindPropertyRelative("MinInclusive").vector2IntValue = regions[regionIndex].MinInclusive;
                    regionProperty.FindPropertyRelative("MaxInclusive").vector2IntValue = regions[regionIndex].MaxInclusive;
                }
            }
        }

        private static void SetObjective(SerializedProperty property, StageObjectiveAuthoring objective)
        {
            property.FindPropertyRelative("CompletionPolicy").intValue = (int)objective.CompletionPolicy;
            var entries = objective.GetConditionEntriesOrEmpty();
            var entriesProperty = property.FindPropertyRelative("ConditionEntries");
            entriesProperty.arraySize = entries.Length;
            for (var i = 0; i < entries.Length; i++)
            {
                var element = entriesProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Condition").objectReferenceValue = entries[i].Condition;
                element.FindPropertyRelative("Required").boolValue = entries[i].Required;
                element.FindPropertyRelative("Role").intValue = (int)entries[i].Role;
                element.FindPropertyRelative("StableConditionId").stringValue = Normalize(entries[i].StableConditionId);
            }
        }

        private static void SetEnemyBindingArray(
            SerializedProperty property,
            IReadOnlyList<EnemyPresentationBinding> bindings)
        {
            property.arraySize = bindings.Count;
            for (var i = 0; i < bindings.Count; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("EntityId").intValue = bindings[i].EntityId;
                element.FindPropertyRelative("PresentationId").stringValue = Normalize(bindings[i].PresentationId);
            }
        }

        private static void SetStaticBindingArray(
            SerializedProperty property,
            IReadOnlyList<StaticEntityPresentationBinding> bindings)
        {
            property.arraySize = bindings.Count;
            for (var i = 0; i < bindings.Count; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("EntityId").intValue = bindings[i].EntityId;
                element.FindPropertyRelative("PresentationId").stringValue = Normalize(bindings[i].PresentationId);
            }
        }

        private static bool ContainsEnemyPresentationId(EnemyPresentationCatalog catalog, string presentationId)
        {
            if (catalog == null)
            {
                return false;
            }

            var entries = catalog.Entries;
            for (var i = 0; i < entries.Length; i++)
            {
                if (string.Equals(
                        EnemyPresentationCatalogResolver.NormalizePresentationId(entries[i].PresentationId),
                        presentationId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsStaticPresentationId(StaticEntityPresentationCatalog catalog, string presentationId)
        {
            if (catalog == null)
            {
                return false;
            }

            var entries = catalog.Entries;
            for (var i = 0; i < entries.Length; i++)
            {
                if (string.Equals(
                        StaticEntityPresentationCatalogResolver.NormalizePresentationId(entries[i].PresentationId),
                        presentationId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
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

        private static void SortByEntityId(List<StageSpawnDefinition> spawns)
        {
            spawns.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        internal static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    internal sealed class StageAuthoringBuildData
    {
        public StageAuthoringBuildData(
            StageBoardDefinition board,
            StageObjectiveAuthoring objective,
            IReadOnlyList<StageZoneDefinition> zones,
            IReadOnlyDictionary<string, int> entityIdsByStableGuid)
        {
            Board = board;
            Objective = objective;
            Zones.AddRange(zones ?? Array.Empty<StageZoneDefinition>());
            EntityIdsByStableGuid = entityIdsByStableGuid;
        }

        public StageBoardDefinition Board { get; }

        public StageObjectiveAuthoring Objective { get; }

        public List<StageZoneDefinition> Zones { get; } = new();

        public IReadOnlyDictionary<string, int> EntityIdsByStableGuid { get; }

        public List<StagePlacedEntityAuthoring> Placements { get; } = new();

        public List<StageSpawnDefinition> PlayerSpawns { get; } = new();

        public List<StageSpawnDefinition> EnemySpawns { get; } = new();

        public List<StageSpawnDefinition> BoxSpawns { get; } = new();

        public List<StageSpawnDefinition> WallSpawns { get; } = new();

        public List<EnemyPresentationBinding> EnemyPresentationBindings { get; } = new();

        public List<StaticEntityPresentationBinding> StaticEntityPresentationBindings { get; } = new();
    }
}
