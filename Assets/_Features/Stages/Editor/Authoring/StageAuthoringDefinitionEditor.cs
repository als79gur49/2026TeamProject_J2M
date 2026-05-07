using System.Linq;
using Game.Feature.Gameplay.BoardState;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    [CustomEditor(typeof(StageAuthoringDefinition))]
    public sealed class StageAuthoringDefinitionEditor : UnityEditor.Editor
    {
        private StageAuthoringGenerationReport lastReport;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var authoring = (StageAuthoringDefinition)target;

            EditorGUILayout.PropertyField(serializedObject.FindProperty("generatedGameplayDefinition"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("generatedPresentationDefinition"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enforceGeneratedSync"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("board"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("placements"), includeChildren: true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tileFeatures"), includeChildren: true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("zones"), includeChildren: true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("objective"), includeChildren: true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("entityIdMappings"), includeChildren: true);
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            DrawPlacementSummary(authoring);
            DrawExitGoalSummary(authoring);
            DrawTileFeaturePresentationSummary(authoring);
            DrawToolbar(authoring);
            DrawReport(lastReport);
        }

        private void DrawToolbar(StageAuthoringDefinition authoring)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate"))
                {
                    lastReport = StageAuthoringGenerator.Generate(
                        authoring,
                        StageAuthoringGenerateOptions.DryRunValidation);
                }

                if (GUILayout.Button("Dry Run Generate"))
                {
                    lastReport = StageAuthoringGenerator.Generate(
                        authoring,
                        StageAuthoringGenerateOptions.DryRunValidation);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate Gameplay + Presentation"))
                {
                    lastReport = StageAuthoringGenerator.Generate(
                        authoring,
                        StageAuthoringGenerateOptions.WriteAll);
                }

                if (GUILayout.Button("Open Grid / TileFeature Editor"))
                {
                    StageAuthoringGridWindow.Open(authoring);
                }
            }

            using (new EditorGUI.DisabledScope(authoring.GeneratedPresentationDefinition == null))
            {
                if (GUILayout.Button("Open Presentation Definition"))
                {
                    Selection.activeObject = authoring.GeneratedPresentationDefinition;
                    EditorGUIUtility.PingObject(authoring.GeneratedPresentationDefinition);
                }
            }
        }

        private static void DrawPlacementSummary(StageAuthoringDefinition authoring)
        {
            var placements = authoring.Placements;
            var placementSummary = string.Join(
                " / ",
                StageAuthoringKindRegistry.Descriptors.Select(
                    descriptor => $"{descriptor.Marker} {CountPlacements(placements, descriptor.Kind)}"));
            var missingPresentation = placements.Count(placement =>
                placement != null &&
                StageAuthoringKindRegistry.RequiresPresentation(placement.Kind) &&
                string.IsNullOrWhiteSpace(placement.PresentationId));
            var duplicateGuidCount = placements
                .Where(placement => placement != null)
                .GroupBy(placement => StageAuthoringGenerator.Normalize(placement.StableGuid))
                .Count(group => string.IsNullOrEmpty(group.Key) || group.Count() > 1);
            var duplicateIdCount = authoring.EntityIdMappings
                .Where(mapping => mapping.EntityId > 0)
                .GroupBy(mapping => mapping.EntityId)
                .Count(group => group.Count() > 1);

            EditorGUILayout.LabelField(
                "Placements",
                placementSummary);
            if (missingPresentation > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{missingPresentation} non-player placement(s) have no presentation id.",
                    MessageType.Warning);
            }

            if (duplicateGuidCount > 0 || duplicateIdCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Duplicate/stale mapping warning: duplicate guid groups={duplicateGuidCount}, duplicate entity id groups={duplicateIdCount}.",
                    MessageType.Warning);
            }
        }

        private static void DrawTileFeaturePresentationSummary(StageAuthoringDefinition authoring)
        {
            var presentation = authoring.GeneratedPresentationDefinition;
            EditorGUILayout.LabelField(
                "Generated Presentation",
                presentation != null ? presentation.name : "None");
            var catalog = presentation != null ? presentation.TileFeaturePresentationCatalog : null;
            DrawBoardTilePresentationSummary(presentation);
            EditorGUILayout.LabelField(
                "TileFeature Catalog",
                catalog != null ? catalog.name : "Missing");
            EditorGUILayout.LabelField("TileFeature Count", authoring.TileFeatures.Count.ToString());
            EditorGUILayout.LabelField(
                "TileFeature Visual Bindings",
                presentation != null
                    ? presentation.TileFeaturePresentationBindings.Length.ToString()
                    : "0");
            if (presentation == null)
            {
                EditorGUILayout.HelpBox(
                    "No StagePresentationDefinition assigned; visual binding editing disabled.",
                    MessageType.Warning);
            }

            var directOverrideCount = presentation != null
                ? presentation.TileFeaturePresentationBindings.Count(binding => binding != null)
                : 0;
            var missingKeyCount = authoring.TileFeatures.Count(feature =>
                string.IsNullOrWhiteSpace(feature.PresentationKey));
            var unresolvedKeyCount = CountUnresolvedTileFeaturePresentationKeys(authoring, catalog);
            var invalidCatalogEntryCount = CountInvalidTileFeatureCatalogEntries(catalog);
            EditorGUILayout.LabelField(
                "TileFeature Presentation Summary",
                $"missing keys={missingKeyCount}, unresolved keys={unresolvedKeyCount}, direct overrides={directOverrideCount}, invalid catalog entries={invalidCatalogEntryCount}");

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(presentation == null))
                {
                    if (GUILayout.Button("Open Presentation Definition"))
                    {
                        Selection.activeObject = presentation;
                        EditorGUIUtility.PingObject(presentation);
                    }
                }

                using (new EditorGUI.DisabledScope(catalog == null))
                {
                    if (GUILayout.Button("Open TileFeature Catalog"))
                    {
                        Selection.activeObject = catalog;
                        EditorGUIUtility.PingObject(catalog);
                    }
                }
            }
        }

        private static void DrawBoardTilePresentationSummary(StagePresentationDefinition presentation)
        {
            var catalog = presentation != null ? presentation.BoardTilePresentationCatalog : null;
            var entryCount = catalog != null ? catalog.Entries.Count : 0;
            var invalidEntryCount = CountInvalidBoardTileCatalogEntries(catalog);
            var missingRoleDefaultCount = CountMissingBoardTileRoleDefaults(catalog);

            EditorGUILayout.LabelField(
                "BoardTile Catalog",
                catalog != null ? catalog.name : "Missing");
            EditorGUILayout.LabelField(
                "BoardTile Catalog Summary",
                $"entries={entryCount}, invalid entries={invalidEntryCount}, missing role defaults={missingRoleDefaultCount}");
        }

        private static int CountInvalidBoardTileCatalogEntries(BoardTilePresentationCatalog catalog)
        {
            if (catalog == null)
            {
                return 0;
            }

            var invalidCount = 0;
            var seenKeys = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            var defaultRoles = new System.Collections.Generic.HashSet<BoardTileVisualRole>();
            var entries = catalog.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    invalidCount++;
                    continue;
                }

                var presentationKey = entry.PresentationKey;
                var invalid = string.IsNullOrEmpty(presentationKey) ||
                              !seenKeys.Add(presentationKey) ||
                              !System.Enum.IsDefined(typeof(BoardTileVisualRole), entry.Role) ||
                              entry.IsDefaultForRole && !defaultRoles.Add(entry.Role) ||
                              entry.TilePrefab == null && entry.MaterialFallback == null;
                if (invalid)
                {
                    invalidCount++;
                }
            }

            return invalidCount;
        }

        private static int CountMissingBoardTileRoleDefaults(BoardTilePresentationCatalog catalog)
        {
            if (catalog == null)
            {
                return 0;
            }

            var missingCount = 0;
            if (!catalog.TryGetDefaultEntry(BoardTileVisualRole.ActiveBottom, out _))
            {
                missingCount++;
            }

            if (!catalog.TryGetDefaultEntry(BoardTileVisualRole.ActiveFront, out _))
            {
                missingCount++;
            }

            return missingCount;
        }

        private static int CountUnresolvedTileFeaturePresentationKeys(
            StageAuthoringDefinition authoring,
            TileFeaturePresentationCatalog catalog)
        {
            if (catalog == null)
            {
                return authoring.TileFeatures.Count(feature =>
                    !string.IsNullOrWhiteSpace(feature.PresentationKey));
            }

            return authoring.TileFeatures.Count(feature =>
            {
                var presentationKey = TileFeaturePresentationCatalog.NormalizePresentationKey(feature.PresentationKey);
                return !string.IsNullOrEmpty(presentationKey) &&
                       !catalog.TryGetEntry(presentationKey, out _);
            });
        }

        private static int CountInvalidTileFeatureCatalogEntries(TileFeaturePresentationCatalog catalog)
        {
            if (catalog == null)
            {
                return 0;
            }

            var invalidCount = 0;
            var seenKeys = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            var defaultKinds = new System.Collections.Generic.HashSet<TileFeatureKind>();
            var entries = catalog.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    invalidCount++;
                    continue;
                }

                var presentationKey = entry.PresentationKey;
                var invalid = string.IsNullOrEmpty(presentationKey) ||
                              !seenKeys.Add(presentationKey) ||
                              entry.VisualPrefab == null ||
                              !StageAuthoringPresentationBindingCommands
                                  .PrefabHasConfigurableTileFeatureVisualTarget(entry.VisualPrefab) ||
                              entry.Kind == TileFeatureKind.Unknown ||
                              !System.Enum.IsDefined(typeof(TileFeatureKind), entry.Kind) ||
                              entry.IsDefaultForKind && !defaultKinds.Add(entry.Kind);
                if (invalid)
                {
                    invalidCount++;
                }
            }

            return invalidCount;
        }

        private static void DrawExitGoalSummary(StageAuthoringDefinition authoring)
        {
            var exits = authoring.TileFeatures
                .Where(feature => feature.Kind == TileFeatureKind.Exit)
                .ToArray();
            EditorGUILayout.LabelField("Exit TileFeatures", exits.Length.ToString());
            if (exits.Length == 0)
            {
                return;
            }

            if (exits.Length > 1)
            {
                EditorGUILayout.HelpBox(
                    $"Stage has multiple Exit TileFeatures ({exits.Length}). Exit MVP supports one Exit per stage.",
                    MessageType.Error);
                return;
            }

            StageAuthoringExitGoalHelperCommands.TryGetExitGoalZoneStatus(
                authoring,
                exits[0].TileId,
                out var status);
            EditorGUILayout.HelpBox(status.Message, ToMessageType(status.Kind));
            if (!string.IsNullOrWhiteSpace(status.PrimaryGoalZoneId))
            {
                EditorGUILayout.LabelField("Exit PrimaryGoal Zone", status.PrimaryGoalZoneId);
            }

            if (status.Kind == ExitGoalZoneStatusKind.ZoneCellMismatch)
            {
                EditorGUILayout.HelpBox(
                    "Exit center does not match PrimaryGoal zone.",
                    MessageType.Warning);
            }
        }

        private static MessageType ToMessageType(ExitGoalZoneStatusKind kind)
        {
            return kind switch
            {
                ExitGoalZoneStatusKind.Valid => MessageType.Info,
                ExitGoalZoneStatusKind.ReferencedZoneMissing => MessageType.Warning,
                ExitGoalZoneStatusKind.ZoneNotSingleCell => MessageType.Warning,
                ExitGoalZoneStatusKind.ZoneCellMismatch => MessageType.Warning,
                ExitGoalZoneStatusKind.NoExitSelected => MessageType.Info,
                ExitGoalZoneStatusKind.SelectedTileFeatureIsNotExit => MessageType.Info,
                _ => MessageType.Error,
            };
        }

        private static int CountPlacements(
            System.Collections.Generic.IEnumerable<StagePlacedEntityAuthoring> placements,
            StageAuthoringEntityKind kind)
        {
            return placements.Count(placement => placement != null && placement.Kind == kind);
        }

        private static void DrawReport(StageAuthoringGenerationReport report)
        {
            if (report == null)
            {
                return;
            }

            EditorGUILayout.Space();
            var messageType = report.HasErrors ? MessageType.Error : MessageType.Info;
            EditorGUILayout.HelpBox(
                report.HasErrors
                    ? "Stage authoring generation has errors."
                    : $"Stage authoring validation completed with {report.Issues.Count} issue(s).",
                messageType);

            for (var i = 0; i < report.Issues.Count; i++)
            {
                var issue = report.Issues[i];
                var issueType = issue.Severity switch
                {
                    StageValidationSeverity.Error => MessageType.Error,
                    StageValidationSeverity.Warning => MessageType.Warning,
                    _ => MessageType.Info,
                };
                EditorGUILayout.HelpBox($"[{issue.Code}] {issue.Message}", issueType);
            }
        }
    }
}
