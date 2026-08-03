using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Objectives;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    internal enum StageButtonObjectiveRemovalMode
    {
        Unavailable,
        SingleCanonical,
        ConflictRepair,
    }

    internal enum StageButtonObjectiveRemovalMatchReason
    {
        StableIdMatch,
        TileIdMatch,
        StableAndTileMatch,
    }

    internal enum StageButtonObjectiveRemovalResultCode
    {
        Success,
        Unavailable,
        Cancelled,
        ButtonObjectiveRemovalTargetChanged,
        CandidateIdentityResolutionFailed,
    }

    internal sealed class StageButtonObjectiveRemovalCandidate
    {
        public StageButtonObjectiveRemovalCandidate(
            StageObjectiveConditionEntry entry,
            int arrayIndex,
            int conditionTileId,
            string conditionAssetPath,
            string conditionAssetGuid,
            string conditionReferenceIdentity,
            string conditionType,
            StageButtonObjectiveRemovalMatchReason matchReason,
            bool conditionReferenceMatches,
            bool roleMatches,
            bool typeMatches)
        {
            StableConditionId = entry.StableConditionId ?? string.Empty;
            Required = entry.Required;
            Role = entry.Role;
            AuthoringLabel = entry.AuthoringLabel ?? string.Empty;
            SortOrder = entry.SortOrder;
            Condition = entry.Condition;
            ConditionTileId = conditionTileId;
            ConditionAssetPath = conditionAssetPath ?? string.Empty;
            ConditionAssetGuid = conditionAssetGuid ?? string.Empty;
            ConditionReferenceIdentity = conditionReferenceIdentity ?? string.Empty;
            ConditionType = conditionType ?? string.Empty;
            MatchReason = matchReason;
            ConditionReferenceMatches = conditionReferenceMatches;
            RoleMatches = roleMatches;
            TypeMatches = typeMatches;
            ArrayIndex = arrayIndex;
        }

        public string StableConditionId { get; }
        public bool Required { get; }
        public StageObjectiveConditionRole Role { get; }
        public string AuthoringLabel { get; }
        public int SortOrder { get; }
        public StageConditionAsset Condition { get; }
        public int ConditionTileId { get; }
        public string ConditionAssetPath { get; }
        public string ConditionAssetGuid { get; }
        public string ConditionReferenceIdentity { get; }
        public string ConditionType { get; }
        public StageButtonObjectiveRemovalMatchReason MatchReason { get; }
        public bool ConditionReferenceMatches { get; }
        public bool RoleMatches { get; }
        public bool TypeMatches { get; }
        public int ArrayIndex { get; }

        internal string IdentityKey => string.Join("\u001f", new[]
        {
            StableConditionId,
            ConditionReferenceIdentity,
            ConditionTileId.ToString(),
            ((int)Role).ToString(),
            Required ? "1" : "0",
            SortOrder.ToString(),
            AuthoringLabel,
            ConditionType,
            MatchReason.ToString(),
            ConditionReferenceMatches ? "1" : "0",
            RoleMatches ? "1" : "0",
            TypeMatches ? "1" : "0",
        });
    }

    internal sealed class StageButtonObjectiveRemovalPlan
    {
        public StageButtonObjectiveRemovalPlan(
            StageButtonObjectiveRemovalMode mode,
            string stageId,
            string authoringAssetPath,
            string authoringAssetGuid,
            int selectedTileId,
            SurfaceCell selectedButtonCell,
            TileFeatureBoxSelector selectedBoxSelector,
            string expectedStableConditionId,
            StageConditionAsset canonicalCondition,
            IReadOnlyList<StageButtonObjectiveRemovalCandidate> candidates,
            string fingerprint)
        {
            Mode = mode;
            StageId = stageId ?? string.Empty;
            AuthoringAssetPath = authoringAssetPath ?? string.Empty;
            AuthoringAssetGuid = authoringAssetGuid ?? string.Empty;
            SelectedTileId = selectedTileId;
            SelectedButtonCell = selectedButtonCell;
            SelectedBoxSelector = selectedBoxSelector;
            ExpectedStableConditionId = expectedStableConditionId ?? string.Empty;
            CanonicalCondition = canonicalCondition;
            Candidates = Array.AsReadOnly(
                candidates?.ToArray() ?? Array.Empty<StageButtonObjectiveRemovalCandidate>());
            Fingerprint = fingerprint ?? string.Empty;
        }

        public StageButtonObjectiveRemovalMode Mode { get; }
        public string StageId { get; }
        public string AuthoringAssetPath { get; }
        public string AuthoringAssetGuid { get; }
        public int SelectedTileId { get; }
        public SurfaceCell SelectedButtonCell { get; }
        public TileFeatureBoxSelector SelectedBoxSelector { get; }
        public string ExpectedStableConditionId { get; }
        public StageConditionAsset CanonicalCondition { get; }
        public IReadOnlyList<StageButtonObjectiveRemovalCandidate> Candidates { get; }
        public string Fingerprint { get; }
    }

    internal readonly struct StageButtonObjectiveRemovalCommandResult
    {
        public StageButtonObjectiveRemovalCommandResult(
            StageButtonObjectiveRemovalResultCode code,
            string message,
            int removedCount = 0)
        {
            Code = code;
            Message = message ?? string.Empty;
            RemovedCount = removedCount;
        }

        public StageButtonObjectiveRemovalResultCode Code { get; }
        public string Message { get; }
        public int RemovedCount { get; }
        public bool Succeeded => Code == StageButtonObjectiveRemovalResultCode.Success;
    }

    internal interface IStageButtonObjectiveRemovalConfirmation
    {
        bool Confirm(StageButtonObjectiveRemovalPlan plan);
    }

    internal sealed class StageButtonObjectiveRemovalDialogConfirmation : IStageButtonObjectiveRemovalConfirmation
    {
        public bool Confirm(StageButtonObjectiveRemovalPlan plan)
        {
            if (plan == null || plan.Mode == StageButtonObjectiveRemovalMode.Unavailable)
            {
                return false;
            }

            return plan.Mode == StageButtonObjectiveRemovalMode.SingleCanonical
                ? EditorUtility.DisplayDialog(
                    "Remove Button Objective?",
                    StageButtonObjectiveRemovalConfirmationMessage.BuildSingle(plan),
                    "Remove",
                    "Cancel")
                : EditorUtility.DisplayDialog(
                    "Resolve Conflicting Button Objectives?",
                    StageButtonObjectiveRemovalConfirmationMessage.BuildRepair(plan),
                    "Remove Conflicting Entries",
                    "Cancel");
        }
    }

    internal static class StageButtonObjectiveRemovalConfirmationMessage
    {
        public static string BuildSingle(StageButtonObjectiveRemovalPlan plan)
        {
            var candidate = plan.Candidates.Single();
            return
                "Remove this Button Objective from the Stage authoring definition?\n\n" +
                $"Label:\n  {DisplayLabel(candidate.AuthoringLabel)}\n\n" +
                $"Stable ID:\n  {DisplayStableId(candidate.StableConditionId)}\n\n" +
                $"Button:\n  TileId {plan.SelectedTileId} — {plan.SelectedButtonCell}\n\n" +
                $"Type:\n  {DisplayBoxSelector(plan.SelectedBoxSelector)}\n\n" +
                $"Sort Order:\n  {candidate.SortOrder}\n\n" +
                "The referenced condition asset will be retained.\n" +
                "The generated Stage will not change until Generate is run.";
        }

        public static string BuildRepair(StageButtonObjectiveRemovalPlan plan)
        {
            var builder = new StringBuilder();
            builder.Append("Remove ").Append(plan.Candidates.Count)
                .Append(" conflicting Button Objective entries?\n\n")
                .Append("The following entries match Stable ID ")
                .Append(plan.ExpectedStableConditionId)
                .Append(" or Button TileId ")
                .Append(plan.SelectedTileId)
                .Append(":\n\n");

            for (var i = 0; i < plan.Candidates.Count; i++)
            {
                var candidate = plan.Candidates[i];
                builder.Append(i + 1).Append(". ").Append(DisplayStableId(candidate.StableConditionId)).Append('\n')
                    .Append("   TileId ").Append(candidate.ConditionTileId > 0
                        ? candidate.ConditionTileId.ToString()
                        : "<unavailable>").Append('\n')
                    .Append("   Sort Order ").Append(candidate.SortOrder).Append('\n')
                    .Append("   ").Append(DisplayLabel(candidate.AuthoringLabel)).Append('\n')
                    .Append("   Match: ").Append(DisplayMatchReason(candidate.MatchReason)).Append('\n')
                    .Append("   Condition: ").Append(DisplayCondition(candidate)).Append('\n')
                    .Append("   Identity: ").Append(DisplayIdentityFlags(candidate)).Append("\n\n");
            }

            builder.Append("The referenced condition assets will be retained.\n")
                .Append("The generated Stage will not change until Generate is run.");
            return builder.ToString();
        }

        private static string DisplayLabel(string label) =>
            string.IsNullOrEmpty(label) ? "<empty authoring label>" : label;

        private static string DisplayStableId(string stableId) =>
            string.IsNullOrEmpty(stableId) ? "<empty stable ID>" : stableId;

        private static string DisplayCondition(StageButtonObjectiveRemovalCandidate candidate)
        {
            if (candidate.Condition == null)
            {
                return "<missing condition reference>";
            }

            return string.IsNullOrEmpty(candidate.ConditionAssetPath)
                ? candidate.ConditionType
                : candidate.ConditionAssetPath;
        }

        private static string DisplayMatchReason(StageButtonObjectiveRemovalMatchReason reason) => reason switch
        {
            StageButtonObjectiveRemovalMatchReason.StableIdMatch => "Stable ID",
            StageButtonObjectiveRemovalMatchReason.TileIdMatch => "Tile ID",
            _ => "Stable ID + Tile ID",
        };

        private static string DisplayIdentityFlags(StageButtonObjectiveRemovalCandidate candidate)
        {
            var flags = new List<string>
            {
                candidate.ConditionReferenceMatches
                    ? "condition reference match"
                    : "condition reference mismatch",
                candidate.RoleMatches ? "role match" : "role mismatch",
                candidate.TypeMatches ? "type match" : "type mismatch",
            };
            return string.Join(", ", flags);
        }

        private static string DisplayBoxSelector(TileFeatureBoxSelector selector) => selector switch
        {
            TileFeatureBoxSelector.AnyPushableBox => "Any Pushable Box",
            TileFeatureBoxSelector.MoonBlockOnly => "MoonBlock Only",
            _ => selector.ToString(),
        };
    }

    internal static class StageButtonObjectiveRemovalPlanner
    {
        public static StageButtonObjectiveRemovalPlan Build(
            SerializedObject serializedDefinition,
            StageAuthoringDefinition definition,
            StageTileFeatureDefinition selectedFeature)
        {
            serializedDefinition?.Update();
            return Build(definition, selectedFeature);
        }

        public static StageButtonObjectiveRemovalPlan Build(
            StageAuthoringDefinition definition,
            StageTileFeatureDefinition selectedFeature)
        {
            var authoringPath = definition != null ? AssetDatabase.GetAssetPath(definition) : string.Empty;
            var authoringGuid = string.IsNullOrEmpty(authoringPath)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(authoringPath);
            var stageId = definition?.OwnerEntry != null && definition.OwnerEntry.StageId.IsValid
                ? definition.OwnerEntry.StageId.Value
                : string.Empty;
            var tileId = selectedFeature.TileId;
            var expectedStableId = StageAuthoringButtonObjectiveHelperCommands.CreateStableConditionId(tileId);

            if (definition == null || tileId <= 0 || selectedFeature.Kind != TileFeatureKind.Button ||
                !TryResolveCurrentButton(definition, selectedFeature, out selectedFeature))
            {
                return CreatePlan(
                    StageButtonObjectiveRemovalMode.Unavailable,
                    stageId,
                    authoringPath,
                    authoringGuid,
                    selectedFeature,
                    expectedStableId,
                    null,
                    Array.Empty<StageButtonObjectiveRemovalCandidate>());
            }

            StageAuthoringButtonObjectiveHelperCommands.TryGetExpectedButtonConditionPath(
                definition,
                tileId,
                out var expectedConditionPath,
                out _);
            var canonicalCondition = string.IsNullOrEmpty(expectedConditionPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<ButtonActivatedConditionAsset>(expectedConditionPath);
            var entries = definition.Objective.GetConditionEntriesOrEmpty();
            var repairIndices = StageAuthoringButtonObjectiveHelperCommands
                .CollectButtonObjectiveRepairCandidateIndices(entries, tileId, expectedStableId);
            var candidates = repairIndices
                .Select(index => CreateCandidate(
                    entries[index],
                    index,
                    tileId,
                    expectedStableId,
                    canonicalCondition))
                .OrderBy(candidate => candidate.StableConditionId, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.ConditionAssetGuid, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.ConditionReferenceIdentity, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.ConditionTileId)
                .ThenBy(candidate => candidate.SortOrder)
                .ThenBy(candidate => candidate.IdentityKey, StringComparer.Ordinal)
                .ToArray();
            var exactCount = candidates.Count(candidate =>
                string.Equals(
                    StageAuthoringButtonObjectiveHelperCommands.NormalizeStableConditionId(candidate.StableConditionId),
                    expectedStableId,
                    StringComparison.Ordinal) &&
                candidate.ConditionReferenceMatches &&
                candidate.ConditionTileId == tileId &&
                candidate.Required &&
                candidate.RoleMatches &&
                candidate.TypeMatches);
            var mode = candidates.Length == 0
                ? StageButtonObjectiveRemovalMode.Unavailable
                : exactCount == 1 && candidates.Length == 1
                    ? StageButtonObjectiveRemovalMode.SingleCanonical
                    : StageButtonObjectiveRemovalMode.ConflictRepair;
            return CreatePlan(
                mode,
                stageId,
                authoringPath,
                authoringGuid,
                selectedFeature,
                expectedStableId,
                canonicalCondition,
                candidates);
        }

        private static bool TryResolveCurrentButton(
            StageAuthoringDefinition definition,
            StageTileFeatureDefinition selectedFeature,
            out StageTileFeatureDefinition current)
        {
            for (var i = 0; i < definition.TileFeatures.Count; i++)
            {
                if (definition.TileFeatures[i].TileId == selectedFeature.TileId &&
                    definition.TileFeatures[i].Kind == TileFeatureKind.Button)
                {
                    current = definition.TileFeatures[i];
                    return true;
                }
            }

            current = default;
            return false;
        }

        private static StageButtonObjectiveRemovalCandidate CreateCandidate(
            StageObjectiveConditionEntry entry,
            int index,
            int selectedTileId,
            string expectedStableId,
            StageConditionAsset canonicalCondition)
        {
            var stableMatches = string.Equals(
                StageAuthoringButtonObjectiveHelperCommands.NormalizeStableConditionId(entry.StableConditionId),
                expectedStableId,
                StringComparison.Ordinal);
            var buttonCondition = entry.Condition as ButtonActivatedConditionAsset;
            var tileMatches = buttonCondition != null && buttonCondition.TileId == selectedTileId;
            var path = entry.Condition != null ? AssetDatabase.GetAssetPath(entry.Condition) : string.Empty;
            var guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            var referenceIdentity = ResolveReferenceIdentity(entry.Condition, guid, path);
            return new StageButtonObjectiveRemovalCandidate(
                entry,
                index,
                buttonCondition != null ? buttonCondition.TileId : 0,
                path,
                guid,
                referenceIdentity,
                entry.Condition != null ? entry.Condition.GetType().FullName : string.Empty,
                stableMatches && tileMatches
                    ? StageButtonObjectiveRemovalMatchReason.StableAndTileMatch
                    : stableMatches
                        ? StageButtonObjectiveRemovalMatchReason.StableIdMatch
                        : StageButtonObjectiveRemovalMatchReason.TileIdMatch,
                canonicalCondition != null && ReferenceEquals(entry.Condition, canonicalCondition),
                entry.Role == StageObjectiveConditionRole.SecondaryGoal,
                entry.Condition is ButtonActivatedConditionAsset);
        }

        private static string ResolveReferenceIdentity(UnityEngine.Object condition, string guid, string path)
        {
            if (condition == null)
            {
                return "<missing>";
            }

            if (!string.IsNullOrEmpty(guid))
            {
                return $"guid:{guid}|path:{path}";
            }

            return $"global:{GlobalObjectId.GetGlobalObjectIdSlow(condition)}";
        }

        private static StageButtonObjectiveRemovalPlan CreatePlan(
            StageButtonObjectiveRemovalMode mode,
            string stageId,
            string authoringPath,
            string authoringGuid,
            StageTileFeatureDefinition selectedFeature,
            string expectedStableId,
            StageConditionAsset canonicalCondition,
            IReadOnlyList<StageButtonObjectiveRemovalCandidate> candidates)
        {
            var fingerprint = ComputeFingerprint(
                mode,
                stageId,
                authoringPath,
                authoringGuid,
                selectedFeature,
                expectedStableId,
                candidates);
            return new StageButtonObjectiveRemovalPlan(
                mode,
                stageId,
                authoringPath,
                authoringGuid,
                selectedFeature.TileId,
                selectedFeature.Cell,
                selectedFeature.BoxSelector,
                expectedStableId,
                canonicalCondition,
                candidates,
                fingerprint);
        }

        private static string ComputeFingerprint(
            StageButtonObjectiveRemovalMode mode,
            string stageId,
            string authoringPath,
            string authoringGuid,
            StageTileFeatureDefinition selectedFeature,
            string expectedStableId,
            IReadOnlyList<StageButtonObjectiveRemovalCandidate> candidates)
        {
            var payload = new StringBuilder()
                .Append(stageId).Append('\n')
                .Append(authoringGuid).Append('|').Append(authoringPath).Append('\n')
                .Append(selectedFeature.TileId).Append('|').Append(selectedFeature.Kind).Append('|')
                .Append(selectedFeature.Cell).Append('|').Append(selectedFeature.BoxSelector).Append('\n')
                .Append(expectedStableId).Append('|').Append(mode).Append('|').Append(candidates.Count).Append('\n');
            for (var i = 0; i < candidates.Count; i++)
            {
                payload.Append(candidates[i].IdentityKey).Append('\n');
            }

            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payload.ToString()));
            return string.Concat(hash.Select(value => value.ToString("x2")));
        }
    }

    internal static class StageButtonObjectiveRemovalExecutor
    {
        public static StageButtonObjectiveRemovalCommandResult TryExecute(
            SerializedObject serializedDefinition,
            StageAuthoringDefinition definition,
            StageTileFeatureDefinition selectedFeature,
            StageButtonObjectiveRemovalPlan originalPlan)
        {
            if (definition == null || originalPlan == null ||
                originalPlan.Mode == StageButtonObjectiveRemovalMode.Unavailable)
            {
                return new StageButtonObjectiveRemovalCommandResult(
                    StageButtonObjectiveRemovalResultCode.Unavailable,
                    "No safely removable Button Objective target was resolved.");
            }

            serializedDefinition?.Update();
            var currentPlan = StageButtonObjectiveRemovalPlanner.Build(
                serializedDefinition,
                definition,
                selectedFeature);
            if (!PlansMatch(originalPlan, currentPlan))
            {
                return new StageButtonObjectiveRemovalCommandResult(
                    StageButtonObjectiveRemovalResultCode.ButtonObjectiveRemovalTargetChanged,
                    "BUTTON_OBJECTIVE_REMOVAL_TARGET_CHANGED. Reopen the removal dialog and review the current targets.");
            }

            var entries = definition.Objective.GetConditionEntriesOrEmpty();
            var targetIndices = currentPlan.Candidates.Select(candidate => candidate.ArrayIndex).ToArray();
            if (targetIndices.Length != originalPlan.Candidates.Count ||
                targetIndices.Distinct().Count() != targetIndices.Length ||
                targetIndices.Any(index => index < 0 || index >= entries.Length))
            {
                return new StageButtonObjectiveRemovalCommandResult(
                    StageButtonObjectiveRemovalResultCode.CandidateIdentityResolutionFailed,
                    "Confirmed Button Objective candidates could not be resolved exactly. Nothing was removed.");
            }

            var expectedIdentities = originalPlan.Candidates
                .Select(candidate => candidate.IdentityKey)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var currentIdentities = currentPlan.Candidates
                .Select(candidate => candidate.IdentityKey)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (!expectedIdentities.SequenceEqual(currentIdentities, StringComparer.Ordinal))
            {
                return new StageButtonObjectiveRemovalCommandResult(
                    StageButtonObjectiveRemovalResultCode.CandidateIdentityResolutionFailed,
                    "Confirmed Button Objective candidate identities changed. Nothing was removed.");
            }

            var nextEntries = entries.ToList();
            foreach (var targetIndex in targetIndices.OrderByDescending(index => index))
            {
                nextEntries.RemoveAt(targetIndex);
            }

            Undo.RecordObject(definition, originalPlan.Mode == StageButtonObjectiveRemovalMode.SingleCanonical
                ? "Remove Button Objective"
                : "Repair Conflicting Button Objectives");
            var objective = definition.Objective;
            objective.ConditionEntries = nextEntries.ToArray();
            definition.SetObjective(objective);
            EditorUtility.SetDirty(definition);
            return new StageButtonObjectiveRemovalCommandResult(
                StageButtonObjectiveRemovalResultCode.Success,
                originalPlan.Mode == StageButtonObjectiveRemovalMode.SingleCanonical
                    ? "Removed one canonical Button Objective entry."
                    : $"Removed {targetIndices.Length} confirmed conflicting Button Objective entries.",
                targetIndices.Length);
        }

        private static bool PlansMatch(
            StageButtonObjectiveRemovalPlan original,
            StageButtonObjectiveRemovalPlan current)
        {
            return original.Mode == current.Mode &&
                   string.Equals(original.StageId, current.StageId, StringComparison.Ordinal) &&
                   string.Equals(original.AuthoringAssetPath, current.AuthoringAssetPath, StringComparison.Ordinal) &&
                   string.Equals(original.AuthoringAssetGuid, current.AuthoringAssetGuid, StringComparison.Ordinal) &&
                   original.SelectedTileId == current.SelectedTileId &&
                   original.Candidates.Count == current.Candidates.Count &&
                   string.Equals(original.Fingerprint, current.Fingerprint, StringComparison.Ordinal);
        }
    }
}
