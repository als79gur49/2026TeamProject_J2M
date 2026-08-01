using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Objectives;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    internal enum ButtonObjectiveLinkState
    {
        NoSelection,
        NotButton,
        ObjectiveDisabled,
        ExitPrimaryGoalMissing,
        NotLinked,
        Linked,
        ConditionAssetMissing,
        ConditionAssetInvalid,
        ConditionReferencesDifferentTile,
        ConditionReferencesNonButtonTile,
        DuplicateCondition,
        StableConditionIdConflict,
    }

    internal readonly struct ButtonObjectiveLinkStatus
    {
        public ButtonObjectiveLinkStatus(
            ButtonObjectiveLinkState state,
            string message,
            int tileId = 0,
            string stableConditionId = "",
            string expectedConditionPath = "",
            StageConditionAsset conditionAsset = null,
            int matchingEntryCount = 0)
        {
            State = state;
            Message = message ?? string.Empty;
            TileId = tileId;
            StableConditionId = stableConditionId ?? string.Empty;
            ExpectedConditionPath = expectedConditionPath ?? string.Empty;
            ConditionAsset = conditionAsset;
            MatchingEntryCount = matchingEntryCount;
        }

        public ButtonObjectiveLinkState State { get; }

        public string Message { get; }

        public int TileId { get; }

        public string StableConditionId { get; }

        public string ExpectedConditionPath { get; }

        public StageConditionAsset ConditionAsset { get; }

        public int MatchingEntryCount { get; }
    }

    internal readonly struct ButtonObjectiveCommandResult
    {
        private ButtonObjectiveCommandResult(
            bool succeeded,
            string message,
            MessageType messageType,
            UnityEngine.Object pingTarget)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
            MessageType = messageType;
            PingTarget = pingTarget;
        }

        public bool Succeeded { get; }

        public string Message { get; }

        public MessageType MessageType { get; }

        public UnityEngine.Object PingTarget { get; }

        public static ButtonObjectiveCommandResult Success(
            string message,
            UnityEngine.Object pingTarget = null)
        {
            return new ButtonObjectiveCommandResult(true, message, MessageType.Info, pingTarget);
        }

        public static ButtonObjectiveCommandResult Warning(
            string message,
            UnityEngine.Object pingTarget = null)
        {
            return new ButtonObjectiveCommandResult(false, message, MessageType.Warning, pingTarget);
        }

        public static ButtonObjectiveCommandResult SuccessWarning(
            string message,
            UnityEngine.Object pingTarget = null)
        {
            return new ButtonObjectiveCommandResult(true, message, MessageType.Warning, pingTarget);
        }

        public static ButtonObjectiveCommandResult Failure(string message)
        {
            return new ButtonObjectiveCommandResult(false, message, MessageType.Error, null);
        }
    }

    internal static class StageAuthoringButtonObjectiveHelperCommands
    {
        private const string StableConditionIdPrefix = "button-";
        private const string ButtonConditionNameSuffix = "ButtonActivated";

        public static ButtonObjectiveLinkStatus GetLinkStatus(
            StageAuthoringDefinition definition,
            StageTileFeatureDefinition selectedFeature)
        {
            if (!TryValidateSelectedButton(
                    definition,
                    selectedFeature,
                    requireButton: false,
                    out var tileId,
                    out var stableConditionId,
                    out var validationStatus))
            {
                return validationStatus;
            }

            if (selectedFeature.Kind != TileFeatureKind.Button)
            {
                return CreateStatus(
                    ButtonObjectiveLinkState.NotButton,
                    $"TileFeature TileId {tileId} is {selectedFeature.Kind}, not Button.",
                    tileId,
                    stableConditionId);
            }

            TryGetExpectedButtonConditionPath(definition, tileId, out var expectedConditionPath, out _);
            var expectedAsset = !string.IsNullOrEmpty(expectedConditionPath)
                ? AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(expectedConditionPath)
                : null;
            if (expectedAsset != null && expectedAsset is not ButtonActivatedConditionAsset)
            {
                return CreateStatus(
                    ButtonObjectiveLinkState.ConditionAssetInvalid,
                    $"Expected condition path contains {expectedAsset.GetType().Name}, not ButtonActivatedConditionAsset.",
                    tileId,
                    stableConditionId,
                    expectedConditionPath);
            }

            var expectedButton = expectedAsset as ButtonActivatedConditionAsset;
            if (expectedButton != null && expectedButton.TileId != tileId)
            {
                return CreateStatus(
                    ButtonObjectiveLinkState.ConditionReferencesDifferentTile,
                    $"Expected condition asset references TileId {expectedButton.TileId}, not selected Button TileId {tileId}.",
                    tileId,
                    stableConditionId,
                    expectedConditionPath,
                    expectedButton);
            }

            var entries = definition.Objective.GetConditionEntriesOrEmpty();
            var matchingIndices = FindButtonObjectiveEntryIndices(entries, tileId, stableConditionId);
            if (matchingIndices.Count > 1)
            {
                return CreateStatus(
                    ButtonObjectiveLinkState.DuplicateCondition,
                    $"Duplicate condition entry for {stableConditionId}.",
                    tileId,
                    stableConditionId,
                    expectedConditionPath,
                    ResolveFirstCondition(entries, matchingIndices),
                    matchingIndices.Count);
            }

            if (matchingIndices.Count == 1)
            {
                var entry = entries[matchingIndices[0]];
                if (entry.Condition == null)
                {
                    return CreateStatus(
                        ButtonObjectiveLinkState.ConditionAssetMissing,
                        "Condition asset missing/invalid.",
                        tileId,
                        stableConditionId,
                        expectedConditionPath,
                        matchingEntryCount: 1);
                }

                if (entry.Condition is not ButtonActivatedConditionAsset buttonCondition)
                {
                    return CreateStatus(
                        ButtonObjectiveLinkState.ConditionAssetInvalid,
                        $"Condition asset is {entry.Condition.GetType().Name}, not ButtonActivatedConditionAsset.",
                        tileId,
                        stableConditionId,
                        expectedConditionPath,
                        entry.Condition,
                        1);
                }

                if (buttonCondition.TileId != tileId)
                {
                    var state = string.Equals(
                            NormalizeStableConditionId(entry.StableConditionId),
                            stableConditionId,
                            StringComparison.Ordinal)
                        ? ButtonObjectiveLinkState.StableConditionIdConflict
                        : ButtonObjectiveLinkState.ConditionReferencesDifferentTile;
                    return CreateStatus(
                        state,
                        $"Condition references TileId {buttonCondition.TileId}, not selected Button TileId {tileId}.",
                        tileId,
                        stableConditionId,
                        expectedConditionPath,
                        buttonCondition,
                        1);
                }

                if (!TryFindTileFeature(definition, buttonCondition.TileId, out var referencedFeature))
                {
                    return CreateStatus(
                        ButtonObjectiveLinkState.ConditionReferencesDifferentTile,
                        $"Condition references unknown TileId {buttonCondition.TileId}.",
                        tileId,
                        stableConditionId,
                        expectedConditionPath,
                        buttonCondition,
                        1);
                }

                if (referencedFeature.Kind != TileFeatureKind.Button)
                {
                    return CreateStatus(
                        ButtonObjectiveLinkState.ConditionReferencesNonButtonTile,
                        $"Condition references non-Button TileId {buttonCondition.TileId}.",
                        tileId,
                        stableConditionId,
                        expectedConditionPath,
                        buttonCondition,
                        1);
                }

                if (!entry.Required || entry.Role != StageObjectiveConditionRole.SecondaryGoal)
                {
                    return CreateStatus(
                        ButtonObjectiveLinkState.ConditionAssetInvalid,
                        $"Button condition exists, but it is not a required {StageObjectiveConditionRole.SecondaryGoal}.",
                        tileId,
                        stableConditionId,
                        expectedConditionPath,
                        buttonCondition,
                        1);
                }

                if (HasExitTileFeature(definition) && !HasValidRequiredExitPrimaryGoal(definition.Objective))
                {
                    return CreateStatus(
                        ButtonObjectiveLinkState.ExitPrimaryGoalMissing,
                        "Exit PrimaryGoal condition must be created first.",
                        tileId,
                        stableConditionId,
                        expectedConditionPath,
                        buttonCondition,
                        1);
                }

                return CreateStatus(
                    ButtonObjectiveLinkState.Linked,
                    $"Linked as required SecondaryGoal: {stableConditionId}.",
                    tileId,
                    stableConditionId,
                    expectedConditionPath,
                    buttonCondition,
                    1);
            }

            if (HasExitTileFeature(definition) && !HasValidRequiredExitPrimaryGoal(definition.Objective))
            {
                return CreateStatus(
                    ButtonObjectiveLinkState.ExitPrimaryGoalMissing,
                    "Exit PrimaryGoal condition must be created first.",
                    tileId,
                    stableConditionId,
                    expectedConditionPath,
                    expectedButton);
            }

            if (definition.Objective.CompletionPolicy == StageCompletionPolicy.Disabled)
            {
                return CreateStatus(
                    ButtonObjectiveLinkState.ObjectiveDisabled,
                    "Not linked to clear condition. Objective is disabled and will be switched to RequireAllConditions.",
                    tileId,
                    stableConditionId,
                    expectedConditionPath,
                    expectedButton);
            }

            return CreateStatus(
                ButtonObjectiveLinkState.NotLinked,
                "Not linked to clear condition.",
                tileId,
                stableConditionId,
                expectedConditionPath,
                expectedButton);
        }

        public static ButtonObjectiveCommandResult TryAddRequiredSecondaryGoal(
            StageAuthoringDefinition definition,
            StageTileFeatureDefinition selectedFeature,
            string stageIdOrAssetName,
            string authoringLabel = null)
        {
            _ = stageIdOrAssetName;
            if (!TryValidateSelectedButton(
                    definition,
                    selectedFeature,
                    requireButton: true,
                    out var tileId,
                    out var stableConditionId,
                    out var validationStatus))
            {
                return ButtonObjectiveCommandResult.Failure(validationStatus.Message);
            }

            var status = GetLinkStatus(definition, selectedFeature);
            switch (status.State)
            {
                case ButtonObjectiveLinkState.Linked:
                    return ButtonObjectiveCommandResult.Success("Button Clear Condition Linked.", status.ConditionAsset);

                case ButtonObjectiveLinkState.ObjectiveDisabled:
                case ButtonObjectiveLinkState.NotLinked:
                    break;

                case ButtonObjectiveLinkState.ExitPrimaryGoalMissing:
                    return ButtonObjectiveCommandResult.Failure("Exit PrimaryGoal condition must be created first.");

                case ButtonObjectiveLinkState.DuplicateCondition:
                case ButtonObjectiveLinkState.StableConditionIdConflict:
                case ButtonObjectiveLinkState.ConditionAssetMissing:
                case ButtonObjectiveLinkState.ConditionAssetInvalid:
                case ButtonObjectiveLinkState.ConditionReferencesDifferentTile:
                case ButtonObjectiveLinkState.ConditionReferencesNonButtonTile:
                    return ButtonObjectiveCommandResult.Warning(status.Message, status.ConditionAsset);

                default:
                    return ButtonObjectiveCommandResult.Failure(status.Message);
            }

            var objective = definition.Objective;
            if (objective.CompletionPolicy != StageCompletionPolicy.Disabled &&
                objective.CompletionPolicy != StageCompletionPolicy.RequireAllConditions)
            {
                return ButtonObjectiveCommandResult.Failure(
                    $"Button clear condition requires {StageCompletionPolicy.RequireAllConditions}.");
            }

            if (HasExitTileFeature(definition) && !HasValidRequiredExitPrimaryGoal(objective))
            {
                return ButtonObjectiveCommandResult.Failure("Exit PrimaryGoal condition must be created first.");
            }

            var entries = objective.GetConditionEntriesOrEmpty();
            var matchingIndices = FindButtonObjectiveEntryIndices(entries, tileId, stableConditionId);
            if (matchingIndices.Count > 0)
            {
                return ButtonObjectiveCommandResult.Warning(
                    $"Button clear condition already has {matchingIndices.Count} matching objective entry.",
                    ResolveFirstCondition(entries, matchingIndices));
            }

            if (!TryGetNextSecondaryGoalSortOrder(entries, out var nextSortOrder))
            {
                return ButtonObjectiveCommandResult.Failure(
                    "No additional automatic Sort Order can be allocated.");
            }

            if (!TryResolveOrCreateConditionAsset(
                    definition,
                    tileId,
                    out var condition,
                    out var error))
            {
                return ButtonObjectiveCommandResult.Failure(error);
            }

            var nextEntries = new List<StageObjectiveConditionEntry>(entries)
            {
                new()
                {
                    Condition = condition,
                    Required = true,
                    Role = StageObjectiveConditionRole.SecondaryGoal,
                    StableConditionId = stableConditionId,
                    AuthoringLabel = string.IsNullOrWhiteSpace(authoringLabel)
                        ? GetDefaultAuthoringLabel(selectedFeature)
                        : authoringLabel.Trim(),
                    SortOrder = nextSortOrder,
                }
            };

            objective.CompletionPolicy = StageCompletionPolicy.RequireAllConditions;
            objective.ConditionEntries = nextEntries.ToArray();
            Undo.RecordObject(definition, "Add Button Clear Condition");
            definition.SetObjective(objective);
            EditorUtility.SetDirty(definition);

            return ButtonObjectiveCommandResult.Success("Added required SecondaryGoal Button clear condition.", condition);
        }

        public static string GetDefaultAuthoringLabel(StageTileFeatureDefinition selectedFeature)
        {
            return selectedFeature.BoxSelector switch
            {
                TileFeatureBoxSelector.MoonBlockOnly => "Place the MoonBlock on the button",
                _ => "Place a push box on the button",
            };
        }

        public static ButtonObjectiveCommandResult TryRemoveRequiredSecondaryGoal(
            StageAuthoringDefinition definition,
            StageTileFeatureDefinition selectedFeature)
        {
            if (!TryValidateSelectedButton(
                    definition,
                    selectedFeature,
                    requireButton: true,
                    out var tileId,
                    out var stableConditionId,
                    out var validationStatus))
            {
                return ButtonObjectiveCommandResult.Failure(validationStatus.Message);
            }

            var objective = definition.Objective;
            var entries = objective.GetConditionEntriesOrEmpty();
            var matchingIndices = FindButtonObjectiveEntryIndices(entries, tileId, stableConditionId);
            if (matchingIndices.Count == 0)
            {
                return ButtonObjectiveCommandResult.Warning("Button clear condition is not linked.");
            }

            var matchingSet = new HashSet<int>(matchingIndices);
            var nextEntries = new List<StageObjectiveConditionEntry>(entries.Length);
            for (var i = 0; i < entries.Length; i++)
            {
                if (!matchingSet.Contains(i))
                {
                    nextEntries.Add(entries[i]);
                }
            }

            var pingTarget = ResolveFirstCondition(entries, matchingIndices);
            objective.ConditionEntries = nextEntries.ToArray();
            Undo.RecordObject(definition, "Remove Button Clear Condition");
            definition.SetObjective(objective);
            EditorUtility.SetDirty(definition);

            return matchingIndices.Count == 1
                ? ButtonObjectiveCommandResult.Success("Removed Button clear condition objective entry.", pingTarget)
                : ButtonObjectiveCommandResult.SuccessWarning(
                    $"Removed {matchingIndices.Count} duplicate Button clear condition objective entries.",
                    pingTarget);
        }

        public static ButtonObjectiveCommandResult TryPingConditionAsset(
            StageAuthoringDefinition definition,
            StageTileFeatureDefinition selectedFeature)
        {
            var status = GetLinkStatus(definition, selectedFeature);
            if (status.ConditionAsset != null)
            {
                return ButtonObjectiveCommandResult.Success("Ping Button condition asset.", status.ConditionAsset);
            }

            if (!string.IsNullOrEmpty(status.ExpectedConditionPath))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(status.ExpectedConditionPath);
                if (asset != null)
                {
                    return ButtonObjectiveCommandResult.Success("Ping Button condition asset.", asset);
                }
            }

            return ButtonObjectiveCommandResult.Failure("Button condition asset is missing.");
        }

        private static bool TryResolveOrCreateConditionAsset(
            StageAuthoringDefinition definition,
            int tileId,
            out ButtonActivatedConditionAsset condition,
            out string error)
        {
            condition = null;
            error = string.Empty;
            if (!TryGetExpectedButtonConditionPath(definition, tileId, out var conditionPath, out error))
            {
                return false;
            }

            var existingAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(conditionPath);
            if (existingAsset != null && existingAsset is not ButtonActivatedConditionAsset)
            {
                error = $"Cannot create Button condition because '{conditionPath}' already contains '{existingAsset.GetType().Name}'.";
                return false;
            }

            var existingCondition = existingAsset as ButtonActivatedConditionAsset;
            if (existingCondition != null)
            {
                if (existingCondition.TileId != tileId)
                {
                    error = $"Button condition asset '{conditionPath}' references TileId {existingCondition.TileId}, not selected Button TileId {tileId}.";
                    return false;
                }

                condition = existingCondition;
                return true;
            }

            EnsureFolder(Path.GetDirectoryName(conditionPath)?.Replace('\\', '/'));
            condition = ScriptableObject.CreateInstance<ButtonActivatedConditionAsset>();
            condition.name = Path.GetFileNameWithoutExtension(conditionPath);
            SetButtonConditionTileId(condition, tileId);
            AssetDatabase.CreateAsset(condition, conditionPath);
            Undo.RegisterCreatedObjectUndo(condition, "Create Button Clear Condition Asset");
            EditorUtility.SetDirty(condition);
            return true;
        }

        private static bool TryGetExpectedButtonConditionPath(
            StageAuthoringDefinition definition,
            int tileId,
            out string conditionPath,
            out string error)
        {
            conditionPath = string.Empty;
            error = string.Empty;
            var ownerEntry = definition != null ? definition.OwnerEntry : null;
            if (ownerEntry == null || !ownerEntry.StageId.IsValid)
            {
                error = "Button condition auto-creation requires a StageContentEntry owner with a valid StageId.";
                return false;
            }

            var entryPath = AssetDatabase.GetAssetPath(ownerEntry);
            if (string.IsNullOrEmpty(entryPath))
            {
                error = "Button condition auto-creation requires the owner StageContentEntry to be saved as an asset.";
                return false;
            }

            if (!TryGetCampaignRoot(entryPath, out var campaignRoot, out var campaignFolder))
            {
                error = $"Cannot resolve campaign root from owner StageContentEntry path '{entryPath}'.";
                return false;
            }

            var sharedConditionsRoot = $"{campaignRoot}/_Shared/Gameplay/Conditions";
            var campaignName = SanitizeName(campaignFolder);
            var stageName = SanitizeName(ownerEntry.StageId.Value);
            conditionPath = $"{sharedConditionsRoot}/{campaignName}_{stageName}_{ButtonConditionNameSuffix}_Tile{tileId}.asset";
            return true;
        }

        private static bool TryGetCampaignRoot(
            string assetPath,
            out string campaignRoot,
            out string campaignFolder)
        {
            campaignRoot = string.Empty;
            campaignFolder = string.Empty;
            var marker = "/Campaigns/";
            var markerIndex = assetPath.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return false;
            }

            var campaignStart = markerIndex + marker.Length;
            var campaignEnd = assetPath.IndexOf('/', campaignStart);
            if (campaignEnd <= campaignStart)
            {
                return false;
            }

            campaignFolder = assetPath.Substring(campaignStart, campaignEnd - campaignStart);
            campaignRoot = assetPath.Substring(0, campaignEnd);
            return !string.IsNullOrWhiteSpace(campaignFolder) &&
                   !string.IsNullOrWhiteSpace(campaignRoot);
        }

        private static bool TryValidateSelectedButton(
            StageAuthoringDefinition definition,
            StageTileFeatureDefinition selectedFeature,
            bool requireButton,
            out int tileId,
            out string stableConditionId,
            out ButtonObjectiveLinkStatus status)
        {
            tileId = selectedFeature.TileId;
            stableConditionId = CreateStableConditionId(tileId);
            status = default;
            if (definition == null)
            {
                status = CreateStatus(
                    ButtonObjectiveLinkState.NoSelection,
                    "StageAuthoringDefinition is missing.");
                return false;
            }

            if (tileId <= 0)
            {
                status = CreateStatus(
                    ButtonObjectiveLinkState.NoSelection,
                    "Select a Button TileFeature first.");
                return false;
            }

            if (!TryFindTileFeature(definition, tileId, out var currentFeature))
            {
                status = CreateStatus(
                    ButtonObjectiveLinkState.NoSelection,
                    $"TileFeature TileId {tileId} was not found.",
                    tileId,
                    stableConditionId);
                return false;
            }

            if (currentFeature.Kind != selectedFeature.Kind)
            {
                selectedFeature = currentFeature;
            }

            if (requireButton && selectedFeature.Kind != TileFeatureKind.Button)
            {
                status = CreateStatus(
                    ButtonObjectiveLinkState.NotButton,
                    $"TileFeature TileId {tileId} is {selectedFeature.Kind}, not Button.",
                    tileId,
                    stableConditionId);
                return false;
            }

            return true;
        }

        private static List<int> FindButtonObjectiveEntryIndices(
            IReadOnlyList<StageObjectiveConditionEntry> entries,
            int tileId,
            string stableConditionId)
        {
            var result = new List<int>();
            for (var i = 0; i < entries.Count; i++)
            {
                if (string.Equals(
                        NormalizeStableConditionId(entries[i].StableConditionId),
                        stableConditionId,
                        StringComparison.Ordinal) ||
                    entries[i].Condition is ButtonActivatedConditionAsset buttonCondition &&
                    buttonCondition.TileId == tileId)
                {
                    result.Add(i);
                }
            }

            return result;
        }

        private static bool HasExitTileFeature(StageAuthoringDefinition definition)
        {
            var tileFeatures = definition.TileFeatures;
            for (var i = 0; i < tileFeatures.Count; i++)
            {
                if (tileFeatures[i].Kind == TileFeatureKind.Exit)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasValidRequiredExitPrimaryGoal(StageObjectiveAuthoring objective)
        {
            if (objective.CompletionPolicy != StageCompletionPolicy.RequireAllConditions)
            {
                return false;
            }

            var entries = objective.GetConditionEntriesOrEmpty();
            var requiredPrimaryGoalCount = 0;
            var hasPlayerAtAnyZone = false;
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].Role != StageObjectiveConditionRole.PrimaryGoal ||
                    !entries[i].Required)
                {
                    continue;
                }

                requiredPrimaryGoalCount++;
                hasPlayerAtAnyZone = entries[i].Condition is PlayerAtAnyZoneConditionAsset;
            }

            return requiredPrimaryGoalCount == 1 && hasPlayerAtAnyZone;
        }

        private static bool TryGetNextSecondaryGoalSortOrder(
            IReadOnlyList<StageObjectiveConditionEntry> entries,
            out int sortOrder)
        {
            var maxSecondary = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].Role == StageObjectiveConditionRole.SecondaryGoal &&
                    entries[i].SortOrder > maxSecondary)
                {
                    maxSecondary = entries[i].SortOrder;
                }
            }

            if (maxSecondary > int.MaxValue - 10)
            {
                sortOrder = 0;
                return false;
            }

            sortOrder = Math.Max(10, maxSecondary + 10);
            return true;
        }

        private static void SetButtonConditionTileId(ButtonActivatedConditionAsset condition, int tileId)
        {
            var serializedObject = new SerializedObject(condition);
            serializedObject.FindProperty("tileId").intValue = tileId;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (string.IsNullOrWhiteSpace(assetFolder) || AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder));
        }

        private static bool TryFindTileFeature(
            StageAuthoringDefinition definition,
            int tileId,
            out StageTileFeatureDefinition feature)
        {
            var features = definition.TileFeatures;
            for (var i = 0; i < features.Count; i++)
            {
                if (features[i].TileId == tileId)
                {
                    feature = features[i];
                    return true;
                }
            }

            feature = default;
            return false;
        }

        private static StageConditionAsset ResolveFirstCondition(
            IReadOnlyList<StageObjectiveConditionEntry> entries,
            IReadOnlyList<int> indices)
        {
            for (var i = 0; i < indices.Count; i++)
            {
                var index = indices[i];
                if (index >= 0 &&
                    index < entries.Count &&
                    entries[index].Condition != null)
                {
                    return entries[index].Condition;
                }
            }

            return null;
        }

        private static string CreateStableConditionId(int tileId)
        {
            return tileId > 0 ? $"{StableConditionIdPrefix}{tileId}" : string.Empty;
        }

        private static string NormalizeStableConditionId(string stableConditionId)
        {
            return stableConditionId?.Trim() ?? string.Empty;
        }

        private static string SanitizeName(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "Unnamed"
                : string.Concat(value.Split('-', StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        }

        private static ButtonObjectiveLinkStatus CreateStatus(
            ButtonObjectiveLinkState state,
            string message,
            int tileId = 0,
            string stableConditionId = "",
            string expectedConditionPath = "",
            StageConditionAsset conditionAsset = null,
            int matchingEntryCount = 0)
        {
            return new ButtonObjectiveLinkStatus(
                state,
                message,
                tileId,
                stableConditionId,
                expectedConditionPath,
                conditionAsset,
                matchingEntryCount);
        }
    }
}
