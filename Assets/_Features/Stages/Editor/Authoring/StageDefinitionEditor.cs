using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    internal enum StageDefinitionGeneratedSyncStatus
    {
        InSync,
        GenerateRequired,
        GeneratedOutputMissing,
        InvalidAuthoring,
    }

    internal static class StageDefinitionInspectorActions
    {
        public static bool OpenAuthoring(StageGeneratedDefinitionOwnership ownership)
        {
            if (ownership == null ||
                ownership.Kind != StageDefinitionOwnershipKind.GeneratedOwned ||
                ownership.Owner == null)
            {
                return false;
            }

            Selection.activeObject = ownership.Owner;
            EditorGUIUtility.PingObject(ownership.Owner);
            return true;
        }

        public static StageAuthoringGenerationReport ValidateAuthoring(
            StageGeneratedDefinitionOwnership ownership)
        {
            return ownership != null &&
                   ownership.Kind == StageDefinitionOwnershipKind.GeneratedOwned &&
                   ownership.Owner != null
                ? StageAuthoringGenerator.Generate(
                    ownership.Owner,
                    StageAuthoringGenerateOptions.DryRunValidation)
                : null;
        }

        public static StageAuthoringGenerationReport GenerateFromAuthoring(
            StageGeneratedDefinitionOwnership ownership)
        {
            return ownership != null &&
                   ownership.Kind == StageDefinitionOwnershipKind.GeneratedOwned &&
                   ownership.Owner != null
                ? StageAuthoringGenerator.Generate(
                    ownership.Owner,
                    StageAuthoringGenerateOptions.WriteAll)
                : null;
        }

        public static StageDefinitionGeneratedSyncStatus ResolveSyncStatus(
            StageGeneratedDefinitionOwnership ownership,
            StageAuthoringGenerationReport latestReport = null)
        {
            if (ownership?.Owner == null || ownership.Target == null)
            {
                return StageDefinitionGeneratedSyncStatus.GeneratedOutputMissing;
            }

            if (latestReport != null && latestReport.HasErrors)
            {
                return StageDefinitionGeneratedSyncStatus.InvalidAuthoring;
            }

            var owner = ownership.Owner;
            var allocation = StageAuthoringProjection.BuildAllocationPlan(owner);
            var expected = StageAuthoringProjection.ProjectExpectedGameplay(owner, allocation);
            var actual = StageAuthoringProjection.ProjectActualGameplay(ownership.Target);
            var issues = StageAuthoringDriftComparer.CompareGameplay(
                expected,
                actual,
                new StageAuthoringDriftContext(
                    StageValidationSeverity.Warning,
                    StageValidationTiming.EditorAuthoring,
                    owner,
                    ownership.TargetPath,
                    string.Empty,
                    owner.name,
                    ownership.Target.name));
            return issues.Length == 0
                ? StageDefinitionGeneratedSyncStatus.InSync
                : StageDefinitionGeneratedSyncStatus.GenerateRequired;
        }
    }

    [CustomEditor(typeof(StageDefinition))]
    [CanEditMultipleObjects]
    internal sealed class StageDefinitionEditor : UnityEditor.Editor
    {
        private StageAuthoringGenerationReport lastReport;
        private string lastAction = string.Empty;

        public override void OnInspectorGUI()
        {
            var ownerships = targets
                .OfType<StageDefinition>()
                .Select(StageGeneratedDefinitionOwnershipResolver.Resolve)
                .ToArray();

            if (StageDefinitionInspectorPolicy.IsSelectionEditable(ownerships))
            {
                DrawStandaloneBanner(ownerships);
                DrawDefaultInspector();
                return;
            }

            if (ownerships.Length > 1)
            {
                DrawMultiSelectionBanner(ownerships);
                DrawReadOnlyDefaultInspector();
                return;
            }

            var ownership = ownerships.Length == 1 ? ownerships[0] : null;
            if (ownership == null)
            {
                EditorGUILayout.HelpBox(
                    "StageDefinition ownership could not be resolved. Direct editing is disabled.",
                    MessageType.Error);
                DrawReadOnlyDefaultInspector();
                return;
            }

            switch (ownership.Kind)
            {
                case StageDefinitionOwnershipKind.GeneratedOwned:
                    DrawGeneratedOwnedInspector(ownership);
                    break;
                case StageDefinitionOwnershipKind.Ambiguous:
                    DrawAmbiguousInspector(ownership);
                    break;
                default:
                    EditorGUILayout.HelpBox(
                        "Unexpected StageDefinition ownership state. Direct editing is disabled.",
                        MessageType.Error);
                    DrawReadOnlyDefaultInspector();
                    break;
            }
        }

        private void DrawGeneratedOwnedInspector(StageGeneratedDefinitionOwnership ownership)
        {
            var ownerPath = ownership.Owners[0].AssetPath;
            EditorGUILayout.HelpBox(
                "Generated Stage Definition\n\n" +
                $"This asset is generated from:\n{ownerPath}\n\n" +
                "Edit the Stage authoring definition and run Generate. " +
                "Direct edits to this generated output are disabled.",
                MessageType.Info);

            var syncStatus = StageDefinitionInspectorActions.ResolveSyncStatus(ownership, lastReport);
            EditorGUILayout.LabelField("Generated Output Status", FormatSyncStatus(syncStatus));
            DrawReadOnlyDefaultInspector();

            EditorGUILayout.Space();
            if (GUILayout.Button("Open Authoring Definition"))
            {
                StageDefinitionInspectorActions.OpenAuthoring(ownership);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate Authoring"))
                {
                    lastAction = "Validate Authoring";
                    lastReport = StageDefinitionInspectorActions.ValidateAuthoring(ownership);
                }

                if (GUILayout.Button("Generate from Authoring"))
                {
                    lastAction = "Generate from Authoring";
                    lastReport = StageDefinitionInspectorActions.GenerateFromAuthoring(ownership);
                }
            }

            EditorGUILayout.HelpBox(
                "Generate replaces the generated gameplay and presentation outputs with the canonical authoring projection.",
                MessageType.Warning);
            DrawReport(lastAction, lastReport);
        }

        private void DrawAmbiguousInspector(StageGeneratedDefinitionOwnership ownership)
        {
            EditorGUILayout.HelpBox(
                "Ambiguous Generated Ownership\n\n" +
                "Multiple StageAuthoringDefinition assets reference this StageDefinition as generated output. " +
                "Direct editing, Validate, and Generate actions are disabled.",
                MessageType.Error);
            EditorGUILayout.LabelField("Owners", ownership.Owners.Count.ToString());
            for (var i = 0; i < ownership.Owners.Count; i++)
            {
                EditorGUILayout.SelectableLabel(
                    ownership.Owners[i].AssetPath,
                    EditorStyles.textField,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }

            DrawReadOnlyDefaultInspector();
        }

        private static void DrawMultiSelectionBanner(
            IReadOnlyList<StageGeneratedDefinitionOwnership> ownerships)
        {
            var generatedCount = ownerships.Count(
                ownership => ownership.Kind == StageDefinitionOwnershipKind.GeneratedOwned);
            var standaloneCount = ownerships.Count(
                ownership => ownership.Kind == StageDefinitionOwnershipKind.Standalone);
            var ambiguousCount = ownerships.Count(
                ownership => ownership.Kind == StageDefinitionOwnershipKind.Ambiguous);
            EditorGUILayout.HelpBox(
                $"{ownerships.Count} Stage Definitions selected:\n" +
                $"{generatedCount} generated-owned\n" +
                $"{standaloneCount} standalone\n" +
                $"{ambiguousCount} ambiguous\n\n" +
                "Direct multi-object editing is disabled because generated output or ambiguous ownership is included.",
                ambiguousCount > 0 ? MessageType.Error : MessageType.Warning);
        }

        private static void DrawStandaloneBanner(
            IReadOnlyList<StageGeneratedDefinitionOwnership> ownerships)
        {
            if (ownerships.Count != 1)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "Standalone Stage Definition\n\n" +
                "No StageAuthoringDefinition currently owns this asset as a generated output. " +
                "Inspector editing remains enabled.",
                MessageType.Info);
        }

        private void DrawReadOnlyDefaultInspector()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                DrawDefaultInspector();
            }
        }

        private static string FormatSyncStatus(StageDefinitionGeneratedSyncStatus status)
        {
            return status switch
            {
                StageDefinitionGeneratedSyncStatus.InSync => "In Sync",
                StageDefinitionGeneratedSyncStatus.GenerateRequired => "Generate Required",
                StageDefinitionGeneratedSyncStatus.GeneratedOutputMissing => "Generated Output Missing",
                StageDefinitionGeneratedSyncStatus.InvalidAuthoring => "Invalid Authoring",
                _ => status.ToString(),
            };
        }

        private static void DrawReport(string action, StageAuthoringGenerationReport report)
        {
            if (report == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                action,
                report.HasErrors ? $"FAILED — {report.Issues.Count} issue(s)" : $"PASS — {report.Issues.Count} issue(s)");
            for (var i = 0; i < report.Issues.Count; i++)
            {
                var issue = report.Issues[i];
                var messageType = issue.Severity switch
                {
                    StageValidationSeverity.Error => MessageType.Error,
                    StageValidationSeverity.Warning => MessageType.Warning,
                    _ => MessageType.Info,
                };
                EditorGUILayout.HelpBox($"[{issue.Code}] {issue.Message}", messageType);
            }
        }
    }
}
