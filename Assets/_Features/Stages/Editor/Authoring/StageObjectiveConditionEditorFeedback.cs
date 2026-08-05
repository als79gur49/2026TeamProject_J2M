using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;

namespace Game.Feature.Stages.Editor
{
    internal enum StageObjectiveConditionEditorStatus
    {
        InSync,
        GenerateRequired,
        InvalidAuthoring,
        GeneratedOutputMissing,
        GeneratedOutputError,
        CatalogError,
        Unresolved,
    }

    internal enum StageObjectiveConditionIssueSourceKind
    {
        None,
        InvalidAuthoring,
        GeneratedOutput,
        Catalog,
    }

    internal sealed class StageObjectiveConditionEditorFeedback
    {
        public static readonly StageObjectiveConditionEditorFeedback Unresolved = new(
            StageObjectiveConditionEditorStatus.Unresolved,
            Array.Empty<StageValidationIssue>(),
            Array.Empty<StageValidationIssue>(),
            hasAdditionalValidationIssues: false,
            "Objective status could not be resolved.");

        public StageObjectiveConditionEditorFeedback(
            StageObjectiveConditionEditorStatus status,
            IReadOnlyList<StageValidationIssue> validationIssues,
            IReadOnlyList<StageValidationIssue> driftIssues,
            bool hasAdditionalValidationIssues,
            string message,
            IReadOnlyList<StageValidationIssue> generatedOutputIssues = null,
            IReadOnlyList<StageValidationIssue> catalogIssues = null,
            StageObjectiveConditionIssueSourceKind issueSourceKind =
                StageObjectiveConditionIssueSourceKind.None,
            UnityEngine.Object issueOwner = null,
            bool canGenerateOrRepair = false)
        {
            Status = status;
            ValidationIssues = validationIssues ?? Array.Empty<StageValidationIssue>();
            DriftIssues = driftIssues ?? Array.Empty<StageValidationIssue>();
            GeneratedOutputIssues = generatedOutputIssues ?? Array.Empty<StageValidationIssue>();
            CatalogIssues = catalogIssues ?? Array.Empty<StageValidationIssue>();
            HasAdditionalValidationIssues = hasAdditionalValidationIssues;
            Message = message ?? string.Empty;
            IssueSourceKind = issueSourceKind;
            IssueOwner = issueOwner;
            CanGenerateOrRepair = canGenerateOrRepair;
        }

        public StageObjectiveConditionEditorStatus Status { get; }

        public IReadOnlyList<StageValidationIssue> ValidationIssues { get; }

        public IReadOnlyList<StageValidationIssue> DriftIssues { get; }

        public IReadOnlyList<StageValidationIssue> GeneratedOutputIssues { get; }

        public IReadOnlyList<StageValidationIssue> CatalogIssues { get; }

        public bool HasAdditionalValidationIssues { get; }

        public string Message { get; }

        public StageObjectiveConditionIssueSourceKind IssueSourceKind { get; }

        public UnityEngine.Object IssueOwner { get; }

        public bool CanGenerateOrRepair { get; }

        public string StatusLabel => Status switch
        {
            StageObjectiveConditionEditorStatus.InSync => "In Sync",
            StageObjectiveConditionEditorStatus.GenerateRequired => "Generate Required",
            StageObjectiveConditionEditorStatus.InvalidAuthoring => "Invalid",
            StageObjectiveConditionEditorStatus.GeneratedOutputMissing => "Generated Output Missing",
            StageObjectiveConditionEditorStatus.GeneratedOutputError => "Generated Output Error",
            StageObjectiveConditionEditorStatus.CatalogError => "Catalog Error",
            _ => "Unresolved",
        };

        public string GetRowMessage(StageObjectiveConditionEditorRow row)
        {
            if (Status == StageObjectiveConditionEditorStatus.InvalidAuthoring)
            {
                var issue = FindIssueForRow(ValidationIssues, row);
                return issue.HasValue ? $"{issue.Value.Code}: {issue.Value.Message}" : Message;
            }

            if (Status == StageObjectiveConditionEditorStatus.GenerateRequired)
            {
                var issue = FindIssueForRow(DriftIssues, row);
                if (issue.HasValue &&
                    issue.Value.FieldName.EndsWith(".AuthoringLabel", StringComparison.Ordinal))
                {
                    return "Objective condition AuthoringLabel differs from generated output.";
                }

                if (issue.HasValue &&
                    issue.Value.FieldName.EndsWith(".SortOrder", StringComparison.Ordinal))
                {
                    return "Objective condition SortOrder differs from generated output.";
                }

                return issue.HasValue ? issue.Value.Message : Message;
            }

            return Message;
        }

        private static StageValidationIssue? FindIssueForRow(
            IReadOnlyList<StageValidationIssue> issues,
            StageObjectiveConditionEditorRow row)
        {
            if (row == null)
            {
                return null;
            }

            var indexToken = $"[{row.EntryIndex}]";
            for (var i = 0; i < issues.Count; i++)
            {
                if (issues[i].FieldName.Contains(indexToken, StringComparison.Ordinal) ||
                    issues[i].Message.Contains($"entry[{row.EntryIndex}]", StringComparison.OrdinalIgnoreCase) ||
                    HasExactStableConditionIdToken(issues[i].Message, row.StableConditionId))
                {
                    return issues[i];
                }
            }

            return null;
        }

        internal static bool HasExactStableConditionIdToken(
            string message,
            string stableConditionId)
        {
            if (string.IsNullOrEmpty(message) || string.IsNullOrWhiteSpace(stableConditionId))
            {
                return false;
            }

            var token = new StringBuilder();
            for (var i = 0; i <= message.Length; i++)
            {
                if (i < message.Length && IsStableConditionIdTokenCharacter(message[i]))
                {
                    token.Append(message[i]);
                    continue;
                }

                if (token.Length > 0 &&
                    string.Equals(token.ToString(), stableConditionId, StringComparison.Ordinal))
                {
                    return true;
                }

                token.Clear();
            }

            return false;
        }

        private static bool IsStableConditionIdTokenCharacter(char value)
        {
            return char.IsLetterOrDigit(value) || value == '-' || value == '_';
        }
    }

    internal static class StageObjectiveConditionEditorFeedbackBuilder
    {
        public static StageObjectiveConditionEditorFeedback Build(
            StageAuthoringDefinition authoring,
            StageContentEntry catalogEntry = null,
            StageAuthoringGenerationReport existingValidationReport = null)
        {
            if (authoring == null)
            {
                return StageObjectiveConditionEditorFeedback.Unresolved;
            }

            try
            {
                var validationIssues = new List<StageValidationIssue>();
                var allValidationIssues = new List<StageValidationIssue>();
                var generatedOutputIssues = new List<StageValidationIssue>();
                var catalogIssues = new List<StageValidationIssue>();
                var generationReport = existingValidationReport ?? StageAuthoringGenerator.Generate(
                    authoring,
                    StageAuthoringGenerateOptions.DryRunValidation);
                AddUnique(allValidationIssues, generationReport.Issues);

                if (catalogEntry != null)
                {
                    var catalogReport = new StageCatalogValidator().ValidateEntries(
                        new[] { catalogEntry },
                        aliasTable: null,
                        new StageCatalogValidationOptions
                        {
                            Timing = StageValidationTiming.EditorAuthoring,
                        });
                    AddUnique(
                        generatedOutputIssues,
                        catalogReport.Issues.Where(issue =>
                            IsObjectiveValidationIssue(issue) ||
                            IsObjectiveDriftIssue(issue)));
                    AddUnique(
                        catalogIssues,
                        catalogReport.Issues.Where(issue =>
                            !IsObjectiveValidationIssue(issue) &&
                            !IsObjectiveDriftIssue(issue)));
                }

                for (var i = 0; i < allValidationIssues.Count; i++)
                {
                    if (IsObjectiveValidationIssue(allValidationIssues[i]))
                    {
                        validationIssues.Add(allValidationIssues[i]);
                    }
                }

                var hasAdditionalValidationIssues = allValidationIssues.Any(issue =>
                    !IsObjectiveValidationIssue(issue) &&
                    !IsObjectiveDriftIssue(issue)) ||
                    generatedOutputIssues.Count > 0 ||
                    catalogIssues.Count > 0;
                if (validationIssues.Any(issue => issue.Severity == StageValidationSeverity.Error))
                {
                    var firstError = validationIssues.First(issue =>
                        issue.Severity == StageValidationSeverity.Error);
                    return new StageObjectiveConditionEditorFeedback(
                        StageObjectiveConditionEditorStatus.InvalidAuthoring,
                        validationIssues,
                        Array.Empty<StageValidationIssue>(),
                        hasAdditionalValidationIssues,
                        firstError.Message,
                        generatedOutputIssues,
                        catalogIssues,
                        StageObjectiveConditionIssueSourceKind.InvalidAuthoring,
                        authoring,
                        canGenerateOrRepair: false);
                }

                if (authoring.GeneratedGameplayDefinition == null)
                {
                    return new StageObjectiveConditionEditorFeedback(
                        StageObjectiveConditionEditorStatus.GeneratedOutputMissing,
                        validationIssues,
                        Array.Empty<StageValidationIssue>(),
                        hasAdditionalValidationIssues,
                        "The generated StageDefinition is missing.",
                        generatedOutputIssues,
                        catalogIssues,
                        StageObjectiveConditionIssueSourceKind.GeneratedOutput,
                        authoring,
                        canGenerateOrRepair: true);
                }

                var allocationPlan = StageAuthoringProjection.BuildAllocationPlan(authoring);
                var driftIssues = StageAuthoringDriftComparer.CompareGameplay(
                        StageAuthoringProjection.ProjectExpectedGameplay(authoring, allocationPlan),
                        StageAuthoringProjection.ProjectActualGameplay(authoring.GeneratedGameplayDefinition),
                        new StageAuthoringDriftContext(
                            StageValidationSeverity.Warning,
                            StageValidationTiming.EditorAuthoring,
                            authoring,
                            AssetDatabase.GetAssetPath(authoring),
                            authoring.name,
                            authoring.name,
                            authoring.GeneratedGameplayDefinition.name))
                    .Where(IsObjectiveDriftIssue)
                    .ToArray();
                if (driftIssues.Length > 0)
                {
                    return new StageObjectiveConditionEditorFeedback(
                        StageObjectiveConditionEditorStatus.GenerateRequired,
                        validationIssues,
                        driftIssues,
                        hasAdditionalValidationIssues,
                        "Objective condition authoring differs from the generated StageDefinition.",
                        generatedOutputIssues,
                        catalogIssues,
                        StageObjectiveConditionIssueSourceKind.GeneratedOutput,
                        authoring.GeneratedGameplayDefinition,
                        canGenerateOrRepair: true);
                }

                if (generatedOutputIssues.Any(issue =>
                        issue.Severity == StageValidationSeverity.Error))
                {
                    var firstGeneratedError = generatedOutputIssues.First(issue =>
                        issue.Severity == StageValidationSeverity.Error);
                    return new StageObjectiveConditionEditorFeedback(
                        StageObjectiveConditionEditorStatus.GeneratedOutputError,
                        validationIssues,
                        driftIssues,
                        hasAdditionalValidationIssues,
                        firstGeneratedError.Message,
                        generatedOutputIssues,
                        catalogIssues,
                        StageObjectiveConditionIssueSourceKind.GeneratedOutput,
                        authoring.GeneratedGameplayDefinition,
                        canGenerateOrRepair: true);
                }

                if (catalogIssues.Any(issue => issue.Severity == StageValidationSeverity.Error))
                {
                    var firstCatalogError = catalogIssues.First(issue =>
                        issue.Severity == StageValidationSeverity.Error);
                    return new StageObjectiveConditionEditorFeedback(
                        StageObjectiveConditionEditorStatus.CatalogError,
                        validationIssues,
                        driftIssues,
                        hasAdditionalValidationIssues,
                        firstCatalogError.Message,
                        generatedOutputIssues,
                        catalogIssues,
                        StageObjectiveConditionIssueSourceKind.Catalog,
                        catalogEntry,
                        canGenerateOrRepair: false);
                }

                return new StageObjectiveConditionEditorFeedback(
                    StageObjectiveConditionEditorStatus.InSync,
                    validationIssues,
                    driftIssues,
                    hasAdditionalValidationIssues,
                    "Objective authoring and generated output are in sync.",
                    generatedOutputIssues,
                    catalogIssues,
                    StageObjectiveConditionIssueSourceKind.None,
                    issueOwner: null,
                    canGenerateOrRepair: false);
            }
            catch (Exception exception)
            {
                return new StageObjectiveConditionEditorFeedback(
                    StageObjectiveConditionEditorStatus.Unresolved,
                    Array.Empty<StageValidationIssue>(),
                    Array.Empty<StageValidationIssue>(),
                    hasAdditionalValidationIssues: false,
                    exception.Message);
            }
        }

        public static StageContentEntry ResolveCatalogEntry(StageAuthoringDefinition authoring)
        {
            if (authoring == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(authoring)))
            {
                return null;
            }

            var guids = AssetDatabase.FindAssets("t:StageContentEntry");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var entry = AssetDatabase.LoadAssetAtPath<StageContentEntry>(path);
                if (entry != null && ReferenceEquals(entry.AuthoringDefinition, authoring))
                {
                    return entry;
                }
            }

            return null;
        }

        private static bool IsObjectiveValidationIssue(StageValidationIssue issue)
        {
            if (IsObjectiveDriftIssue(issue))
            {
                return false;
            }

            if (issue.Code.StartsWith("objective.", StringComparison.OrdinalIgnoreCase) ||
                issue.FieldName.StartsWith("Objective.", StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.Equals(
                    issue.Code,
                    "authoring.generated-gameplay.invalid",
                    StringComparison.Ordinal))
            {
                return false;
            }

            return issue.Message.Contains("objective", StringComparison.OrdinalIgnoreCase) ||
                   issue.Message.Contains("PrimaryGoal", StringComparison.Ordinal) ||
                   issue.Message.Contains("ButtonActivatedCondition", StringComparison.Ordinal);
        }

        private static bool IsObjectiveDriftIssue(StageValidationIssue issue)
        {
            return string.Equals(
                       issue.Code,
                       "GameplayDrift.ObjectiveMismatch",
                       StringComparison.Ordinal) ||
                   issue.FieldName.StartsWith("Objective.", StringComparison.Ordinal) &&
                   issue.Code.StartsWith("GameplayDrift.", StringComparison.Ordinal);
        }

        private static void AddUnique(
            ICollection<StageValidationIssue> target,
            IEnumerable<StageValidationIssue> source)
        {
            foreach (var issue in source)
            {
                if (target.Any(existing =>
                        existing.Severity == issue.Severity &&
                        string.Equals(existing.Code, issue.Code, StringComparison.Ordinal) &&
                        string.Equals(existing.Message, issue.Message, StringComparison.Ordinal) &&
                        string.Equals(existing.FieldName, issue.FieldName, StringComparison.Ordinal)))
                {
                    continue;
                }

                target.Add(issue);
            }
        }
    }
}
