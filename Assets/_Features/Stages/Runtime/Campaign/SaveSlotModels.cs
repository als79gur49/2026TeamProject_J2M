using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Game.Feature.Stages
{
    public enum CampaignChanceHudDiagnosticKind
    {
        StageLaunch = 0,
        StageResolve = 1,
        Installer = 2,
        SourceRead = 3,
        HudQueryConstructed = 4,
        HudQueryRead = 5,
        HudViewModel = 6,
    }

    public enum CampaignChanceReadFailureReason
    {
        None = 0,
        SourceMissing = 1,
        NoActiveSlot = 2,
        SlotNotLoaded = 3,
        StageIdMissing = 4,
        StageIdMismatch = 5,
        CampaignProgressMissing = 6,
        StageNotCampaignTracked = 7,
        MaxChancesZero = 8,
        SaveDataNotInitialized = 9,
        EditorDirectPlaySuppressed = 10,
        Unknown = 11,
    }

    public sealed class CampaignChanceHudDiagnosticRecord
    {
        public CampaignChanceHudDiagnosticRecord(CampaignChanceHudDiagnosticKind kind)
        {
            Kind = kind;
        }

        public CampaignChanceHudDiagnosticKind Kind { get; }

        public string SceneName { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;

        public string RequestedStageId { get; set; } = string.Empty;

        public string LaunchStageId { get; set; } = string.Empty;

        public string ResolvedStageId { get; set; } = string.Empty;

        public string GameplayDefinitionName { get; set; } = string.Empty;

        public string PresentationDefinitionName { get; set; } = string.Empty;

        public EditorDirectPlayMode EditorDirectPlayMode { get; set; }

        public bool SuppressCampaignFlow { get; set; }

        public bool HasCustomSaveNamespace { get; set; }

        public bool EnableCampaignFlow { get; set; }

        public bool CampaignRuntimeActive { get; set; }

        public bool HasActiveSlot { get; set; }

        public int ActiveSlotNumber { get; set; }

        public string SaveSlotStoreKey { get; set; } = string.Empty;

        public string ActiveSlotProviderKey { get; set; } = string.Empty;

        public string SourceType { get; set; } = string.Empty;

        public bool SourceIsNull { get; set; }

        public bool TryReadResult { get; set; }

        public CampaignChanceReadFailureReason FailureReason { get; set; }

        public string SourceStageId { get; set; } = string.Empty;

        public int RemainingChances { get; set; }

        public int MaxChances { get; set; }

        public bool PlayerFound { get; set; }

        public bool FinalHasChances { get; set; }

        public override string ToString()
        {
            return
                $"[CampaignChanceHUD] kind={Kind} scene={SceneName} source={Source} requested={RequestedStageId} launch={LaunchStageId} resolved={ResolvedStageId} directPlay={EditorDirectPlayMode} suppress={SuppressCampaignFlow} customNamespace={HasCustomSaveNamespace} activeSlot={ActiveSlotNumber} hasActiveSlot={HasActiveSlot} runtimeActive={CampaignRuntimeActive} sourceType={SourceType} sourceNull={SourceIsNull} read={TryReadResult} reason={FailureReason} sourceStage={SourceStageId} remaining={RemainingChances} max={MaxChances} playerFound={PlayerFound} finalHas={FinalHasChances}";
        }
    }

    public static class CampaignChanceHudDiagnostics
    {
        private const int Capacity = 128;
        private static readonly List<CampaignChanceHudDiagnosticRecord> Records = new(Capacity);

        public static bool IsEnabled { get; set; }

        public static bool LogToUnityConsole { get; set; }

        public static void Clear()
        {
            Records.Clear();
        }

        public static IReadOnlyList<CampaignChanceHudDiagnosticRecord> Snapshot()
        {
            return Records.ToArray();
        }

        public static void Record(CampaignChanceHudDiagnosticRecord record)
        {
            if (!IsEnabled || record == null)
            {
                return;
            }

            if (Records.Count == Capacity)
            {
                Records.RemoveAt(0);
            }

            Records.Add(record);
            if (LogToUnityConsole)
            {
                Debug.Log(record.ToString());
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload()
        {
            Clear();
            IsEnabled = false;
            LogToUnityConsole = false;
        }
    }

    public sealed class SaveSlotData
    {
        public int SlotNumber { get; set; }

        public StageId CurrentStageId { get; set; }

        public string CurrentLevelGroupId { get; set; } = string.Empty;

        public int RemainingChances { get; set; } = SaveSlotStore.DefaultRemainingChances;

        public bool CampaignCompleted { get; set; }

        public bool IntroPlayed { get; set; }

        public bool OutroPlayed { get; set; }

        public int TotalDeaths { get; set; }

        public string LastPlayedAt { get; set; } = string.Empty;

        public StageClearProfileSnapshot StageClearProfileSnapshot { get; set; } = new();

        public bool IsEmpty => !CurrentStageId.IsValid &&
                               !CampaignCompleted &&
                               !IntroPlayed &&
                               !OutroPlayed &&
                               TotalDeaths == 0 &&
                               string.IsNullOrWhiteSpace(LastPlayedAt) &&
                               IsClearProfileEmpty(StageClearProfileSnapshot);

        public SaveSlotData Clone()
        {
            return new SaveSlotData
            {
                SlotNumber = SlotNumber,
                CurrentStageId = CurrentStageId,
                CurrentLevelGroupId = CurrentLevelGroupId ?? string.Empty,
                RemainingChances = RemainingChances,
                CampaignCompleted = CampaignCompleted,
                IntroPlayed = IntroPlayed,
                OutroPlayed = OutroPlayed,
                TotalDeaths = TotalDeaths,
                LastPlayedAt = LastPlayedAt ?? string.Empty,
                StageClearProfileSnapshot = StageClearProfileSnapshot?.Clone() ?? new StageClearProfileSnapshot(),
            };
        }

        public static SaveSlotData CreateEmpty(int slotNumber)
        {
            return new SaveSlotData
            {
                SlotNumber = slotNumber,
                CurrentStageId = StageId.None,
                CurrentLevelGroupId = string.Empty,
                RemainingChances = SaveSlotStore.DefaultRemainingChances,
                CampaignCompleted = false,
                IntroPlayed = false,
                OutroPlayed = false,
                TotalDeaths = 0,
                LastPlayedAt = string.Empty,
                StageClearProfileSnapshot = new StageClearProfileSnapshot(),
            };
        }

        public static SaveSlotData CreateNewGame(
            int slotNumber,
            CampaignStageSequenceResolver sequenceResolver,
            string lastPlayedAt)
        {
            if (sequenceResolver == null)
            {
                throw new ArgumentNullException(nameof(sequenceResolver));
            }

            var firstStageId = sequenceResolver.FirstStageId;
            return new SaveSlotData
            {
                SlotNumber = slotNumber,
                CurrentStageId = firstStageId,
                CurrentLevelGroupId = sequenceResolver.GetLevelGroupId(firstStageId),
                RemainingChances = SaveSlotStore.DefaultRemainingChances,
                CampaignCompleted = false,
                IntroPlayed = false,
                OutroPlayed = false,
                TotalDeaths = 0,
                LastPlayedAt = lastPlayedAt ?? string.Empty,
                StageClearProfileSnapshot = new StageClearProfileSnapshot(),
            };
        }

        private static bool IsClearProfileEmpty(StageClearProfileSnapshot snapshot)
        {
            return snapshot == null ||
                   (snapshot.Version == 0 &&
                    snapshot.ClearRecordsByStageId.Count == 0 &&
                    snapshot.ProcessedStageRunIds.Count == 0 &&
                    snapshot.ProcessedClearAttemptIds.Count == 0);
        }
    }

    public static class SaveSlotPrefsKeys
    {
        public const string LegacySaveSlotsKey = "Game.Feature.Stages.SaveSlots";
        public const string LegacyActiveSaveSlotKey = "Game.Feature.Stages.ActiveSaveSlot";
        public const string SaveSlotsKey = "Game.Feature.Stages.StageClearSaveSlots";
        public const string ActiveSaveSlotKey = "Game.Feature.Stages.ActiveStageClearSaveSlot";
    }

    public interface ISavePathProvider
    {
        string SaveRootPath { get; }

        string GetSaveFilePath(string fileName);
    }

    public abstract class SavePathProviderBase : ISavePathProvider
    {
        protected SavePathProviderBase(string saveRootPath)
        {
            SaveRootPath = saveRootPath ?? string.Empty;
        }

        public string SaveRootPath { get; }

        public string GetSaveFilePath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("Save file name must not be empty.", nameof(fileName));
            }

            if (Path.IsPathRooted(fileName) ||
                fileName.IndexOf('/') >= 0 ||
                fileName.IndexOf('\\') >= 0)
            {
                throw new ArgumentException("Save file name must be a simple file name.", nameof(fileName));
            }

            return Path.Combine(SaveRootPath, fileName);
        }
    }

    public sealed class ApplicationPersistentDataSavePathProvider : SavePathProviderBase
    {
        public const string SavesDirectoryName = "Saves";

        public ApplicationPersistentDataSavePathProvider()
            : base(Path.Combine(Application.persistentDataPath, SavesDirectoryName))
        {
        }
    }

    public enum StageClearSavePayloadStatus
    {
        Empty = 0,
        Current = 1,
        LegacyRejected = 2,
        InvalidRejected = 3,
    }

    public readonly struct StageClearSaveLoadReport
    {
        public StageClearSaveLoadReport(
            StageClearSavePayloadStatus status,
            string reason,
            string matchedToken)
        {
            Status = status;
            Reason = reason ?? string.Empty;
            MatchedToken = matchedToken ?? string.Empty;
        }

        public StageClearSavePayloadStatus Status { get; }

        public string Reason { get; }

        public string MatchedToken { get; }

        public static StageClearSaveLoadReport Empty(string reason = "No payload.")
        {
            return new StageClearSaveLoadReport(StageClearSavePayloadStatus.Empty, reason, string.Empty);
        }
    }

    internal readonly struct StageClearSavePayloadInspectionResult
    {
        public StageClearSavePayloadInspectionResult(
            StageClearSavePayloadStatus status,
            string reason,
            string matchedToken)
        {
            Status = status;
            Reason = reason ?? string.Empty;
            MatchedToken = matchedToken ?? string.Empty;
        }

        public StageClearSavePayloadStatus Status { get; }

        public string Reason { get; }

        public string MatchedToken { get; }

        public bool ShouldReset =>
            Status == StageClearSavePayloadStatus.LegacyRejected ||
            Status == StageClearSavePayloadStatus.InvalidRejected;

        public StageClearSaveLoadReport ToLoadReport()
        {
            return new StageClearSaveLoadReport(Status, Reason, MatchedToken);
        }
    }

    internal static class StageClearSavePayloadGuard
    {
        internal static readonly string[] LegacyTokens =
        {
            "StageCompletionProfileSnapshot",
            "ProgressByStageId",
            "PlayerStageProgress",
            "HasStarted",
            "ProcessedCompletionAttemptIds",
            "InventoryBalances",
            "AppliedRewardGrantIds",
            "ConsumedRewardRuleIds",
            "BestScore",
            "BestStars",
            "BestRankId",
            "CompletedChallengeIds",
        };

        public static StageClearSavePayloadInspectionResult Inspect(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return new StageClearSavePayloadInspectionResult(
                    StageClearSavePayloadStatus.Empty,
                    "Payload is empty.",
                    string.Empty);
            }

            foreach (var token in LegacyTokens)
            {
                if (rawJson.IndexOf(token, StringComparison.Ordinal) >= 0)
                {
                    return new StageClearSavePayloadInspectionResult(
                        StageClearSavePayloadStatus.LegacyRejected,
                        "Payload contains legacy stage save vocabulary.",
                        token);
                }
            }

            if (!TryReadRootSchema(rawJson, out var schemaId, out var schemaVersion, out var parseReason))
            {
                return new StageClearSavePayloadInspectionResult(
                    StageClearSavePayloadStatus.InvalidRejected,
                    parseReason,
                    string.Empty);
            }

            if (!string.Equals(schemaId, SaveSlotStore.SchemaId, StringComparison.Ordinal))
            {
                return new StageClearSavePayloadInspectionResult(
                    StageClearSavePayloadStatus.InvalidRejected,
                    string.IsNullOrEmpty(schemaId)
                        ? "Payload is missing SchemaId."
                        : "Payload SchemaId does not match the current stage-clear save schema.",
                    "SchemaId");
            }

            if (schemaVersion != SaveSlotStore.SchemaVersion)
            {
                return new StageClearSavePayloadInspectionResult(
                    StageClearSavePayloadStatus.InvalidRejected,
                    schemaVersion.HasValue
                        ? "Payload SchemaVersion does not match the current stage-clear save schema."
                        : "Payload is missing SchemaVersion.",
                    "SchemaVersion");
            }

            return new StageClearSavePayloadInspectionResult(
                StageClearSavePayloadStatus.Current,
                "Payload matches the current stage-clear save schema.",
                SaveSlotStore.SchemaId);
        }

        private static bool TryReadRootSchema(
            string json,
            out string schemaId,
            out int? schemaVersion,
            out string reason)
        {
            schemaId = string.Empty;
            schemaVersion = null;
            reason = string.Empty;

            var index = 0;
            if (!SkipWhitespace(json, ref index) || !TryConsume(json, ref index, '{'))
            {
                reason = "Payload is not a JSON object.";
                return false;
            }

            SkipWhitespace(json, ref index);
            if (TryConsume(json, ref index, '}'))
            {
                SkipWhitespace(json, ref index);
                if (index == json.Length)
                {
                    return true;
                }

                reason = "Payload has trailing content after the root object.";
                return false;
            }

            while (index < json.Length)
            {
                if (!TryReadString(json, ref index, out var propertyName))
                {
                    reason = "Payload has an invalid root property name.";
                    return false;
                }

                SkipWhitespace(json, ref index);
                if (!TryConsume(json, ref index, ':'))
                {
                    reason = "Payload has an invalid root property separator.";
                    return false;
                }

                SkipWhitespace(json, ref index);
                if (string.Equals(propertyName, nameof(SaveSlotStoreDto.SchemaId), StringComparison.Ordinal))
                {
                    if (!TryReadString(json, ref index, out schemaId))
                    {
                        reason = "Payload SchemaId is not a string.";
                        return false;
                    }
                }
                else if (string.Equals(propertyName, nameof(SaveSlotStoreDto.SchemaVersion), StringComparison.Ordinal))
                {
                    if (!TryReadInt(json, ref index, out var parsedVersion))
                    {
                        reason = "Payload SchemaVersion is not an integer.";
                        return false;
                    }

                    schemaVersion = parsedVersion;
                }
                else if (!TrySkipValue(json, ref index))
                {
                    reason = "Payload contains malformed JSON.";
                    return false;
                }

                SkipWhitespace(json, ref index);
                if (TryConsume(json, ref index, '}'))
                {
                    SkipWhitespace(json, ref index);
                    if (index == json.Length)
                    {
                        return true;
                    }

                    reason = "Payload has trailing content after the root object.";
                    return false;
                }

                if (!TryConsume(json, ref index, ','))
                {
                    reason = "Payload has an invalid root property delimiter.";
                    return false;
                }

                SkipWhitespace(json, ref index);
            }

            reason = "Payload root object is not closed.";
            return false;
        }

        private static bool TrySkipValue(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length)
            {
                return false;
            }

            var current = json[index];
            if (current == '"')
            {
                return TryReadString(json, ref index, out _);
            }

            if (current == '{')
            {
                return TrySkipObject(json, ref index);
            }

            if (current == '[')
            {
                return TrySkipArray(json, ref index);
            }

            return TrySkipPrimitive(json, ref index);
        }

        private static bool TrySkipObject(string json, ref int index)
        {
            if (!TryConsume(json, ref index, '{'))
            {
                return false;
            }

            SkipWhitespace(json, ref index);
            if (TryConsume(json, ref index, '}'))
            {
                return true;
            }

            while (index < json.Length)
            {
                if (!TryReadString(json, ref index, out _))
                {
                    return false;
                }

                SkipWhitespace(json, ref index);
                if (!TryConsume(json, ref index, ':') || !TrySkipValue(json, ref index))
                {
                    return false;
                }

                SkipWhitespace(json, ref index);
                if (TryConsume(json, ref index, '}'))
                {
                    return true;
                }

                if (!TryConsume(json, ref index, ','))
                {
                    return false;
                }

                SkipWhitespace(json, ref index);
            }

            return false;
        }

        private static bool TrySkipArray(string json, ref int index)
        {
            if (!TryConsume(json, ref index, '['))
            {
                return false;
            }

            SkipWhitespace(json, ref index);
            if (TryConsume(json, ref index, ']'))
            {
                return true;
            }

            while (index < json.Length)
            {
                if (!TrySkipValue(json, ref index))
                {
                    return false;
                }

                SkipWhitespace(json, ref index);
                if (TryConsume(json, ref index, ']'))
                {
                    return true;
                }

                if (!TryConsume(json, ref index, ','))
                {
                    return false;
                }

                SkipWhitespace(json, ref index);
            }

            return false;
        }

        private static bool TrySkipPrimitive(string json, ref int index)
        {
            var start = index;
            while (index < json.Length)
            {
                var current = json[index];
                if (current == ',' || current == '}' || current == ']' || char.IsWhiteSpace(current))
                {
                    break;
                }

                index++;
            }

            if (index == start)
            {
                return false;
            }

            var value = json.Substring(start, index - start);
            if (string.Equals(value, "true", StringComparison.Ordinal) ||
                string.Equals(value, "false", StringComparison.Ordinal) ||
                string.Equals(value, "null", StringComparison.Ordinal))
            {
                return true;
            }

            return IsJsonNumber(value);
        }

        private static bool IsJsonNumber(string value)
        {
            var index = 0;
            if (index < value.Length && value[index] == '-')
            {
                index++;
            }

            if (index >= value.Length)
            {
                return false;
            }

            if (value[index] == '0')
            {
                index++;
            }
            else if (value[index] >= '1' && value[index] <= '9')
            {
                do
                {
                    index++;
                }
                while (index < value.Length && char.IsDigit(value[index]));
            }
            else
            {
                return false;
            }

            if (index < value.Length && value[index] == '.')
            {
                index++;
                var fractionStart = index;
                while (index < value.Length && char.IsDigit(value[index]))
                {
                    index++;
                }

                if (index == fractionStart)
                {
                    return false;
                }
            }

            if (index < value.Length && (value[index] == 'e' || value[index] == 'E'))
            {
                index++;
                if (index < value.Length && (value[index] == '+' || value[index] == '-'))
                {
                    index++;
                }

                var exponentStart = index;
                while (index < value.Length && char.IsDigit(value[index]))
                {
                    index++;
                }

                if (index == exponentStart)
                {
                    return false;
                }
            }

            return index == value.Length;
        }

        private static bool TryReadString(string json, ref int index, out string value)
        {
            value = string.Empty;
            if (!TryConsume(json, ref index, '"'))
            {
                return false;
            }

            var start = index;
            var builder = default(System.Text.StringBuilder);
            while (index < json.Length)
            {
                var current = json[index];
                if (current == '"')
                {
                    if (builder == null)
                    {
                        value = json.Substring(start, index - start);
                    }
                    else
                    {
                        builder.Append(json, start, index - start);
                        value = builder.ToString();
                    }

                    index++;
                    return true;
                }

                if (current == '\\')
                {
                    builder ??= new System.Text.StringBuilder();
                    builder.Append(json, start, index - start);
                    index++;
                    if (index >= json.Length)
                    {
                        return false;
                    }

                    var escaped = json[index];
                    if (escaped == 'u')
                    {
                        if (index + 4 >= json.Length)
                        {
                            return false;
                        }

                        for (var i = 1; i <= 4; i++)
                        {
                            if (!Uri.IsHexDigit(json[index + i]))
                            {
                                return false;
                            }
                        }

                        builder.Append('\\');
                        builder.Append('u');
                        builder.Append(json, index + 1, 4);
                        index += 5;
                    }
                    else if (escaped == '"' ||
                             escaped == '\\' ||
                             escaped == '/' ||
                             escaped == 'b' ||
                             escaped == 'f' ||
                             escaped == 'n' ||
                             escaped == 'r' ||
                             escaped == 't')
                    {
                        builder.Append('\\');
                        builder.Append(escaped);
                        index++;
                    }
                    else
                    {
                        return false;
                    }

                    start = index;
                    continue;
                }

                if (char.IsControl(current))
                {
                    return false;
                }

                index++;
            }

            return false;
        }

        private static bool TryReadInt(string json, ref int index, out int value)
        {
            value = 0;
            var start = index;
            if (index < json.Length && json[index] == '-')
            {
                index++;
            }

            while (index < json.Length && char.IsDigit(json[index]))
            {
                index++;
            }

            if (index == start || (index == start + 1 && json[start] == '-'))
            {
                return false;
            }

            return int.TryParse(
                json.Substring(start, index - start),
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out value);
        }

        private static bool TryConsume(string value, ref int index, char expected)
        {
            if (index >= value.Length || value[index] != expected)
            {
                return false;
            }

            index++;
            return true;
        }

        private static bool SkipWhitespace(string value, ref int index)
        {
            while (index < value.Length && char.IsWhiteSpace(value[index]))
            {
                index++;
            }

            return index < value.Length;
        }
    }

    internal readonly struct StageClearSavePrefsScope
    {
        public StageClearSavePrefsScope(
            string saveSlotsKey,
            string activeSlotKey,
            bool isProductionDefaultScope)
        {
            SaveSlotsKey = saveSlotsKey ?? string.Empty;
            ActiveSlotKey = activeSlotKey ?? string.Empty;
            IsProductionDefaultScope = isProductionDefaultScope;
        }

        public string SaveSlotsKey { get; }

        public string ActiveSlotKey { get; }

        public bool IsProductionDefaultScope { get; }

        public static StageClearSavePrefsScope Create(string saveSlotsKey, string activeSlotKey)
        {
            if (string.Equals(saveSlotsKey, SaveSlotPrefsKeys.SaveSlotsKey, StringComparison.Ordinal))
            {
                return new StageClearSavePrefsScope(
                    SaveSlotPrefsKeys.SaveSlotsKey,
                    SaveSlotPrefsKeys.ActiveSaveSlotKey,
                    isProductionDefaultScope: true);
            }

            if (string.Equals(saveSlotsKey, EditorDirectPlayContextStore.TempSaveSlotStoreKey, StringComparison.Ordinal))
            {
                return new StageClearSavePrefsScope(
                    EditorDirectPlayContextStore.TempSaveSlotStoreKey,
                    EditorDirectPlayContextStore.TempActiveSlotProviderKey,
                    isProductionDefaultScope: false);
            }

            return new StageClearSavePrefsScope(
                saveSlotsKey,
                activeSlotKey,
                isProductionDefaultScope: false);
        }
    }

    internal static class StageClearSavePrefsResetPolicy
    {
        public static void ResetProductionStageClearPrefs()
        {
            var deleted = DeleteKeyIfPresent(SaveSlotPrefsKeys.SaveSlotsKey);
            deleted |= DeleteKeyIfPresent(SaveSlotPrefsKeys.LegacySaveSlotsKey);
            deleted |= DeleteKeyIfPresent(SaveSlotPrefsKeys.LegacyActiveSaveSlotKey);
            SaveIfDeleted(deleted);
        }

        public static void ResetCurrentPrefs(string saveSlotsKey, string activeSlotKey)
        {
            var deleted = DeleteKeyIfPresent(saveSlotsKey);
            deleted |= DeleteKeyIfPresent(activeSlotKey);
            SaveIfDeleted(deleted);
        }

        public static void DeleteLegacyStageSavePrefs()
        {
            var deleted = DeleteKeyIfPresent(SaveSlotPrefsKeys.LegacySaveSlotsKey);
            deleted |= DeleteKeyIfPresent(SaveSlotPrefsKeys.LegacyActiveSaveSlotKey);
            SaveIfDeleted(deleted);
        }

        public static void Reset(StageClearSavePrefsScope scope)
        {
            if (scope.IsProductionDefaultScope)
            {
                ResetProductionStageClearPrefs();
                return;
            }

            ResetCurrentPrefs(scope.SaveSlotsKey, scope.ActiveSlotKey);
        }

        private static bool DeleteKeyIfPresent(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || !PlayerPrefs.HasKey(key))
            {
                return false;
            }

            PlayerPrefs.DeleteKey(key);
            return true;
        }

        private static void SaveIfDeleted(bool deleted)
        {
            if (deleted)
            {
                PlayerPrefs.Save();
            }
        }
    }

    internal interface ISaveSlotStorageBackend
    {
        string SaveSlotsKey { get; }

        bool HasPayload();

        string LoadPayload(string defaultValue);

        void SavePayload(string payload);

        void ClearPayload();

        void ResetRejectedPayload();
    }

    internal sealed class PlayerPrefsSaveSlotStorageBackend : ISaveSlotStorageBackend
    {
        private readonly StageClearSavePrefsScope _prefsScope;

        public PlayerPrefsSaveSlotStorageBackend(
            string saveSlotsKey,
            StageClearSavePrefsScope prefsScope)
        {
            SaveSlotsKey = string.IsNullOrWhiteSpace(saveSlotsKey)
                ? SaveSlotStore.DefaultPlayerPrefsKey
                : saveSlotsKey;
            _prefsScope = prefsScope;
        }

        public string SaveSlotsKey { get; }

        public bool HasPayload()
        {
            return PlayerPrefs.HasKey(SaveSlotsKey);
        }

        public string LoadPayload(string defaultValue)
        {
            return PlayerPrefs.GetString(SaveSlotsKey, defaultValue);
        }

        public void SavePayload(string payload)
        {
            PlayerPrefs.SetString(SaveSlotsKey, payload ?? string.Empty);
            PlayerPrefs.Save();
        }

        public void ClearPayload()
        {
            PlayerPrefs.DeleteKey(SaveSlotsKey);
            PlayerPrefs.Save();
        }

        public void ResetRejectedPayload()
        {
            StageClearSavePrefsResetPolicy.Reset(_prefsScope);
        }
    }

    internal sealed class FileSaveSlotStorageBackend : ISaveSlotStorageBackend
    {
        public const string ProfileFileName = "profile.json";
        public const string BackupFileName = "profile.json.bak";

        private static readonly UTF8Encoding Utf8NoBom = new(false);
        private readonly ISavePathProvider _pathProvider;
        private readonly string _profilePath;
        private readonly string _backupPath;
        private readonly string _profileDirectory;

        public FileSaveSlotStorageBackend(ISavePathProvider pathProvider)
        {
            _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
            _profilePath = _pathProvider.GetSaveFilePath(ProfileFileName);
            _backupPath = Path.Combine(Path.GetDirectoryName(_profilePath) ?? _pathProvider.SaveRootPath, BackupFileName);
            _profileDirectory = Path.GetDirectoryName(_profilePath) ?? _pathProvider.SaveRootPath;
            if (string.IsNullOrWhiteSpace(_profileDirectory))
            {
                _profileDirectory = ".";
            }
        }

        public string SaveSlotsKey => _profilePath;

        public bool HasPayload()
        {
            return File.Exists(_profilePath);
        }

        public string LoadPayload(string defaultValue)
        {
            CleanupTempFilesBestEffort();
            if (!File.Exists(_profilePath))
            {
                return defaultValue;
            }

            var rawPayload = File.ReadAllText(_profilePath);
            if (string.IsNullOrWhiteSpace(rawPayload) || JsonSyntaxValidator.IsValid(rawPayload))
            {
                return rawPayload;
            }

            if (TryRestoreBackupPayload(out var backupPayload))
            {
                return backupPayload;
            }

            QuarantineProfileBestEffort();
            return defaultValue;
        }

        public void SavePayload(string payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            Directory.CreateDirectory(_profileDirectory);
            var tempPath = CreateTempPath();
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, Utf8NoBom))
                {
                    writer.Write(payload);
                    writer.Flush();
                    stream.Flush(true);
                }

                CommitTempPayload(tempPath);
            }
            finally
            {
                DeleteFileBestEffort(tempPath);
                CleanupTempFilesBestEffort();
            }
        }

        public void ClearPayload()
        {
            DeleteFileBestEffort(_profilePath);
            DeleteFileBestEffort(_backupPath);
            CleanupTempFilesBestEffort();
        }

        public void ResetRejectedPayload()
        {
            CleanupTempFilesBestEffort();
            QuarantineProfileBestEffort();
        }

        private string CreateTempPath()
        {
            return Path.Combine(_profileDirectory, $"profile.{Guid.NewGuid():N}.tmp");
        }

        private void CommitTempPayload(string tempPath)
        {
            if (!File.Exists(_profilePath))
            {
                File.Move(tempPath, _profilePath);
                return;
            }

            try
            {
                File.Replace(tempPath, _profilePath, _backupPath, ignoreMetadataErrors: true);
                return;
            }
            catch (PlatformNotSupportedException)
            {
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            CommitTempPayloadWithFallback(tempPath);
        }

        private void CommitTempPayloadWithFallback(string tempPath)
        {
            var backupUpdated = false;
            try
            {
                File.Copy(_profilePath, _backupPath, overwrite: true);
                backupUpdated = true;
                File.Delete(_profilePath);
                File.Move(tempPath, _profilePath);
            }
            catch
            {
                if (!File.Exists(_profilePath) && backupUpdated && File.Exists(_backupPath))
                {
                    File.Copy(_backupPath, _profilePath, overwrite: true);
                }

                throw;
            }
        }

        private bool TryRestoreBackupPayload(out string backupPayload)
        {
            backupPayload = string.Empty;
            if (!File.Exists(_backupPath))
            {
                return false;
            }

            var candidate = File.ReadAllText(_backupPath);
            if (string.IsNullOrWhiteSpace(candidate) || !JsonSyntaxValidator.IsValid(candidate))
            {
                return false;
            }

            File.Copy(_backupPath, _profilePath, overwrite: true);
            backupPayload = candidate;
            return true;
        }

        private void QuarantineProfileBestEffort()
        {
            if (!File.Exists(_profilePath))
            {
                return;
            }

            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfffffff", CultureInfo.InvariantCulture);
            var quarantinePath = Path.Combine(_profileDirectory, $"profile.json.corrupt.{timestamp}");
            try
            {
                File.Move(_profilePath, quarantinePath);
            }
            catch
            {
            }
        }

        private void CleanupTempFilesBestEffort()
        {
            try
            {
                if (!Directory.Exists(_profileDirectory))
                {
                    return;
                }

                var tempFiles = Directory.GetFiles(_profileDirectory, "profile.*.tmp");
                for (var i = 0; i < tempFiles.Length; i++)
                {
                    DeleteFileBestEffort(tempFiles[i]);
                }
            }
            catch
            {
            }
        }

        private static void DeleteFileBestEffort(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private static class JsonSyntaxValidator
        {
            public static bool IsValid(string json)
            {
                if (string.IsNullOrWhiteSpace(json))
                {
                    return false;
                }

                var index = 0;
                if (!TrySkipValue(json, ref index))
                {
                    return false;
                }

                SkipWhitespace(json, ref index);
                return index == json.Length;
            }

            private static bool TrySkipValue(string json, ref int index)
            {
                SkipWhitespace(json, ref index);
                if (index >= json.Length)
                {
                    return false;
                }

                var current = json[index];
                if (current == '"')
                {
                    return TryReadString(json, ref index);
                }

                if (current == '{')
                {
                    return TrySkipObject(json, ref index);
                }

                if (current == '[')
                {
                    return TrySkipArray(json, ref index);
                }

                return TrySkipPrimitive(json, ref index);
            }

            private static bool TrySkipObject(string json, ref int index)
            {
                if (!TryConsume(json, ref index, '{'))
                {
                    return false;
                }

                SkipWhitespace(json, ref index);
                if (TryConsume(json, ref index, '}'))
                {
                    return true;
                }

                while (index < json.Length)
                {
                    if (!TryReadString(json, ref index))
                    {
                        return false;
                    }

                    SkipWhitespace(json, ref index);
                    if (!TryConsume(json, ref index, ':') || !TrySkipValue(json, ref index))
                    {
                        return false;
                    }

                    SkipWhitespace(json, ref index);
                    if (TryConsume(json, ref index, '}'))
                    {
                        return true;
                    }

                    if (!TryConsume(json, ref index, ','))
                    {
                        return false;
                    }

                    SkipWhitespace(json, ref index);
                }

                return false;
            }

            private static bool TrySkipArray(string json, ref int index)
            {
                if (!TryConsume(json, ref index, '['))
                {
                    return false;
                }

                SkipWhitespace(json, ref index);
                if (TryConsume(json, ref index, ']'))
                {
                    return true;
                }

                while (index < json.Length)
                {
                    if (!TrySkipValue(json, ref index))
                    {
                        return false;
                    }

                    SkipWhitespace(json, ref index);
                    if (TryConsume(json, ref index, ']'))
                    {
                        return true;
                    }

                    if (!TryConsume(json, ref index, ','))
                    {
                        return false;
                    }

                    SkipWhitespace(json, ref index);
                }

                return false;
            }

            private static bool TrySkipPrimitive(string json, ref int index)
            {
                var start = index;
                while (index < json.Length)
                {
                    var current = json[index];
                    if (current == ',' || current == '}' || current == ']' || char.IsWhiteSpace(current))
                    {
                        break;
                    }

                    index++;
                }

                if (index == start)
                {
                    return false;
                }

                var value = json.Substring(start, index - start);
                if (string.Equals(value, "true", StringComparison.Ordinal) ||
                    string.Equals(value, "false", StringComparison.Ordinal) ||
                    string.Equals(value, "null", StringComparison.Ordinal))
                {
                    return true;
                }

                return IsJsonNumber(value);
            }

            private static bool IsJsonNumber(string value)
            {
                var index = 0;
                if (index < value.Length && value[index] == '-')
                {
                    index++;
                }

                if (index >= value.Length)
                {
                    return false;
                }

                if (value[index] == '0')
                {
                    index++;
                }
                else if (value[index] >= '1' && value[index] <= '9')
                {
                    do
                    {
                        index++;
                    }
                    while (index < value.Length && char.IsDigit(value[index]));
                }
                else
                {
                    return false;
                }

                if (index < value.Length && value[index] == '.')
                {
                    index++;
                    var fractionStart = index;
                    while (index < value.Length && char.IsDigit(value[index]))
                    {
                        index++;
                    }

                    if (index == fractionStart)
                    {
                        return false;
                    }
                }

                if (index < value.Length && (value[index] == 'e' || value[index] == 'E'))
                {
                    index++;
                    if (index < value.Length && (value[index] == '+' || value[index] == '-'))
                    {
                        index++;
                    }

                    var exponentStart = index;
                    while (index < value.Length && char.IsDigit(value[index]))
                    {
                        index++;
                    }

                    if (index == exponentStart)
                    {
                        return false;
                    }
                }

                return index == value.Length;
            }

            private static bool TryReadString(string json, ref int index)
            {
                if (!TryConsume(json, ref index, '"'))
                {
                    return false;
                }

                while (index < json.Length)
                {
                    var current = json[index];
                    if (current == '"')
                    {
                        index++;
                        return true;
                    }

                    if (current == '\\')
                    {
                        index++;
                        if (index >= json.Length)
                        {
                            return false;
                        }

                        var escaped = json[index];
                        if (escaped == 'u')
                        {
                            if (index + 4 >= json.Length)
                            {
                                return false;
                            }

                            for (var i = 1; i <= 4; i++)
                            {
                                if (!Uri.IsHexDigit(json[index + i]))
                                {
                                    return false;
                                }
                            }

                            index += 5;
                            continue;
                        }

                        if (escaped == '"' ||
                            escaped == '\\' ||
                            escaped == '/' ||
                            escaped == 'b' ||
                            escaped == 'f' ||
                            escaped == 'n' ||
                            escaped == 'r' ||
                            escaped == 't')
                        {
                            index++;
                            continue;
                        }

                        return false;
                    }

                    if (char.IsControl(current))
                    {
                        return false;
                    }

                    index++;
                }

                return false;
            }

            private static bool TryConsume(string value, ref int index, char expected)
            {
                if (index >= value.Length || value[index] != expected)
                {
                    return false;
                }

                index++;
                return true;
            }

            private static void SkipWhitespace(string value, ref int index)
            {
                while (index < value.Length && char.IsWhiteSpace(value[index]))
                {
                    index++;
                }
            }
        }
    }

    public sealed class ActiveSlotProvider
    {
        private const string DefaultPlayerPrefsKey = SaveSlotPrefsKeys.ActiveSaveSlotKey;
        private readonly IActiveSlotStorage _storage;
        private readonly string _playerPrefsKey;
        private int _activeSlotNumber;

        public ActiveSlotProvider(string playerPrefsKey = DefaultPlayerPrefsKey)
            : this(new PlayerPrefsActiveSlotStorage(
                string.IsNullOrWhiteSpace(playerPrefsKey) ? DefaultPlayerPrefsKey : playerPrefsKey))
        {
        }

        public ActiveSlotProvider(IActiveSlotStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _playerPrefsKey = _storage.DiagnosticsKey;
            DeleteLegacyPrefsIfUsingDefaultKey(_playerPrefsKey);
            _activeSlotNumber = _storage.TryGetActiveSlot(out var activeSlotNumber)
                ? activeSlotNumber
                : 0;
        }

        public bool HasActiveSlot
        {
            get
            {
                RefreshActiveSlot();
                return SaveSlotStore.IsValidSlotNumber(_activeSlotNumber);
            }
        }

        public string PlayerPrefsKey => _playerPrefsKey;

        public string DiagnosticsKey => _storage.DiagnosticsKey;

        public int ActiveSlotNumber
        {
            get
            {
                if (!HasActiveSlot)
                {
                    throw new InvalidOperationException("No active save slot is selected.");
                }

                return _activeSlotNumber;
            }
        }

        public bool TryGetActiveSlotNumber(out int slotNumber)
        {
            RefreshActiveSlot();
            if (HasActiveSlot)
            {
                slotNumber = _activeSlotNumber;
                return true;
            }

            slotNumber = 0;
            return false;
        }

        public void SetActiveSlot(int slotNumber)
        {
            SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
            _storage.SetActiveSlot(slotNumber);
            _activeSlotNumber = slotNumber;
        }

        public void ClearActiveSlot()
        {
            _storage.ClearActiveSlot();
            _activeSlotNumber = 0;
        }

        private static void DeleteLegacyPrefsIfUsingDefaultKey(string playerPrefsKey)
        {
            if (string.Equals(playerPrefsKey, DefaultPlayerPrefsKey, StringComparison.Ordinal))
            {
                StageClearSavePrefsResetPolicy.DeleteLegacyStageSavePrefs();
            }
        }

        private void RefreshActiveSlot()
        {
            _activeSlotNumber = _storage.TryGetActiveSlot(out var activeSlotNumber)
                ? activeSlotNumber
                : 0;
        }
    }

    public sealed class SaveSlotStore : ICampaignSaveSlotStore
    {
        public const string SchemaId = "StageClearSaveSlots";
        public const int SchemaVersion = 2;
        public const int SaveVersion = 1;
        public const int SlotCount = 3;
        public const int DefaultRemainingChances = 3;
        public const string DefaultPlayerPrefsKey = SaveSlotPrefsKeys.SaveSlotsKey;

        private readonly string _playerPrefsKey;
        private readonly ISaveSlotStorageBackend _storageBackend;

        public SaveSlotStore(string playerPrefsKey = DefaultPlayerPrefsKey, string activeSlotPrefsKey = null)
        {
            _playerPrefsKey = string.IsNullOrWhiteSpace(playerPrefsKey)
                ? DefaultPlayerPrefsKey
                : playerPrefsKey;
            var prefsScope = StageClearSavePrefsScope.Create(_playerPrefsKey, activeSlotPrefsKey);
            DeleteLegacyPrefsIfUsingDefaultScope(prefsScope);
            _storageBackend = new PlayerPrefsSaveSlotStorageBackend(_playerPrefsKey, prefsScope);
            LastLoadReport = StageClearSaveLoadReport.Empty("Load has not run.");
            LastCampaignLoadReport = CampaignSaveLoadReport.Missing("Load has not run.");
        }

        internal SaveSlotStore(ISaveSlotStorageBackend storageBackend, string playerPrefsKey = DefaultPlayerPrefsKey)
        {
            _storageBackend = storageBackend ?? throw new ArgumentNullException(nameof(storageBackend));
            _playerPrefsKey = string.IsNullOrWhiteSpace(playerPrefsKey)
                ? DefaultPlayerPrefsKey
                : playerPrefsKey;
            LastLoadReport = StageClearSaveLoadReport.Empty("Load has not run.");
            LastCampaignLoadReport = CampaignSaveLoadReport.Missing("Load has not run.");
        }

        public string PlayerPrefsKey => _playerPrefsKey;

        public string DiagnosticsKey => _playerPrefsKey;

        public StageClearSaveLoadReport LastLoadReport { get; private set; }

        public CampaignSaveLoadReport LastCampaignLoadReport { get; private set; }

        public static bool IsValidSlotNumber(int slotNumber)
        {
            return slotNumber >= 1 && slotNumber <= SlotCount;
        }

        public static void ThrowIfInvalidSlotNumber(int slotNumber)
        {
            if (!IsValidSlotNumber(slotNumber))
            {
                throw new ArgumentOutOfRangeException(nameof(slotNumber), "Save slot number must be 1, 2, or 3.");
            }
        }

        public SaveSlotData[] LoadAll()
        {
            return LoadAllWithReport().Slots;
        }

        public CampaignSaveLoadResult LoadAllWithReport()
        {
            var slots = SaveSlotDtoMapper.FromDto(LoadDto());
            LastCampaignLoadReport = ToCampaignLoadReport(LastLoadReport);
            return new CampaignSaveLoadResult(slots, LastCampaignLoadReport);
        }

        public SaveSlotData LoadSlot(int slotNumber)
        {
            ThrowIfInvalidSlotNumber(slotNumber);
            return LoadAll()[slotNumber - 1].Clone();
        }

        public void SaveSlot(SaveSlotData slot)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            ThrowIfInvalidSlotNumber(slot.SlotNumber);
            var slots = LoadAll();
            slots[slot.SlotNumber - 1] = slot.Clone();
            SaveAll(slots);
        }

        public SaveSlotData InitializeNewGame(
            int slotNumber,
            CampaignStageSequenceResolver sequenceResolver,
            string lastPlayedAt)
        {
            ThrowIfInvalidSlotNumber(slotNumber);
            var slot = SaveSlotData.CreateNewGame(slotNumber, sequenceResolver, lastPlayedAt);
            SaveSlot(slot);
            return slot.Clone();
        }

        public void UpdateSlot(int slotNumber, Action<SaveSlotData> mutation)
        {
            ThrowIfInvalidSlotNumber(slotNumber);
            if (mutation == null)
            {
                throw new ArgumentNullException(nameof(mutation));
            }

            var slot = LoadSlot(slotNumber);
            mutation(slot);
            SaveSlot(slot);
        }

        public void DeleteSlot(int slotNumber)
        {
            ThrowIfInvalidSlotNumber(slotNumber);
            var slots = LoadAll();
            slots[slotNumber - 1] = SaveSlotData.CreateEmpty(slotNumber);
            SaveAll(slots);
        }

        public void ClearAll()
        {
            _storageBackend.ClearPayload();
        }

        private void SaveAll(SaveSlotData[] slots)
        {
            _storageBackend.SavePayload(JsonUtility.ToJson(SaveSlotDtoMapper.ToDto(slots)));
        }

        private SaveSlotStoreDto LoadDto()
        {
            if (!_storageBackend.HasPayload())
            {
                LastLoadReport = StageClearSaveLoadReport.Empty("PlayerPrefs key is missing.");
                return SaveSlotDtoMapper.CreateEmptyDto();
            }

            var rawJson = _storageBackend.LoadPayload(string.Empty);
            var inspection = StageClearSavePayloadGuard.Inspect(rawJson);
            LastLoadReport = inspection.ToLoadReport();

            if (inspection.Status == StageClearSavePayloadStatus.Empty)
            {
                return SaveSlotDtoMapper.CreateEmptyDto();
            }

            if (inspection.ShouldReset)
            {
                _storageBackend.ResetRejectedPayload();
                return SaveSlotDtoMapper.CreateEmptyDto();
            }

            try
            {
                var dto = JsonUtility.FromJson<SaveSlotStoreDto>(rawJson);
                if (!SaveSlotStoreDtoValidator.IsCurrentDtoValid(dto, out var invalidReason))
                {
                    _storageBackend.ResetRejectedPayload();
                    LastLoadReport = new StageClearSaveLoadReport(
                        StageClearSavePayloadStatus.InvalidRejected,
                        invalidReason,
                        string.Empty);
                    return SaveSlotDtoMapper.CreateEmptyDto();
                }

                LastLoadReport = new StageClearSaveLoadReport(
                    StageClearSavePayloadStatus.Current,
                    "Payload loaded successfully.",
                    SchemaId);
                return dto;
            }
            catch (Exception exception)
            {
                _storageBackend.ResetRejectedPayload();
                LastLoadReport = new StageClearSaveLoadReport(
                    StageClearSavePayloadStatus.InvalidRejected,
                    $"Save slot data could not be parsed and will be ignored. {exception.Message}",
                    string.Empty);
                Debug.LogWarning(LastLoadReport.Reason);
                return SaveSlotDtoMapper.CreateEmptyDto();
            }
        }

        private static void DeleteLegacyPrefsIfUsingDefaultScope(StageClearSavePrefsScope scope)
        {
            if (scope.IsProductionDefaultScope)
            {
                StageClearSavePrefsResetPolicy.DeleteLegacyStageSavePrefs();
            }
        }

        private static CampaignSaveLoadReport ToCampaignLoadReport(StageClearSaveLoadReport report)
        {
            switch (report.Status)
            {
                case StageClearSavePayloadStatus.Current:
                    return CampaignSaveLoadReport.Loaded(report.Reason, report.MatchedToken);
                case StageClearSavePayloadStatus.Empty:
                case StageClearSavePayloadStatus.LegacyRejected:
                case StageClearSavePayloadStatus.InvalidRejected:
                default:
                    return CampaignSaveLoadReport.Missing(report.Reason);
            }
        }
    }

    public sealed class SaveSlotStageClearProfileStore : IStageClearProfileStore
    {
        private readonly ICampaignSaveSlotStore _saveSlotStore;
        private readonly CampaignRunningSlotContext _runningSlotContext;

        public SaveSlotStageClearProfileStore(
            ICampaignSaveSlotStore saveSlotStore,
            CampaignRunningSlotContext runningSlotContext)
        {
            _saveSlotStore = saveSlotStore ?? throw new ArgumentNullException(nameof(saveSlotStore));
            _runningSlotContext = runningSlotContext ?? throw new ArgumentNullException(nameof(runningSlotContext));
        }

        public StageClearProfileSnapshot Load()
        {
            var slot = _saveSlotStore.LoadSlot(_runningSlotContext.SlotNumber);
            return slot.StageClearProfileSnapshot?.Clone() ?? new StageClearProfileSnapshot();
        }

        public void Save(StageClearProfileSnapshot snapshot)
        {
            _saveSlotStore.UpdateSlot(
                _runningSlotContext.SlotNumber,
                slot => slot.StageClearProfileSnapshot = snapshot?.Clone() ?? new StageClearProfileSnapshot());
        }
    }

    [Serializable]
    public sealed class SaveSlotStoreDto
    {
        public string SchemaId = SaveSlotStore.SchemaId;
        public int SchemaVersion = SaveSlotStore.SchemaVersion;
        public int SaveVersion = SaveSlotStore.SaveVersion;
        public SaveSlotDto[] Slots = Array.Empty<SaveSlotDto>();
    }

    public static class SaveSlotStoreDtoValidator
    {
        public static bool IsCurrentDtoValid(SaveSlotStoreDto dto, out string reason)
        {
            if (dto == null)
            {
                reason = "Current payload parsed to a null DTO.";
                return false;
            }

            if (!string.Equals(dto.SchemaId, SaveSlotStore.SchemaId, StringComparison.Ordinal))
            {
                reason = "Current payload DTO SchemaId does not match.";
                return false;
            }

            if (dto.SchemaVersion != SaveSlotStore.SchemaVersion)
            {
                reason = "Current payload DTO SchemaVersion does not match.";
                return false;
            }

            if (dto.SaveVersion != SaveSlotStore.SaveVersion)
            {
                reason = "Current payload SaveVersion is unsupported.";
                return false;
            }

            if (dto.Slots == null)
            {
                reason = "Current payload Slots collection is null.";
                return false;
            }

            for (var i = 0; i < dto.Slots.Length; i++)
            {
                var slot = dto.Slots[i];
                if (slot == null)
                {
                    reason = "Current payload contains a null slot.";
                    return false;
                }

                if (slot.StageClearProfileSnapshot == null)
                {
                    reason = "Current payload contains a null StageClearProfileSnapshot.";
                    return false;
                }

                if (slot.StageClearProfileSnapshot.ClearRecordsByStageId == null)
                {
                    reason = "Current payload contains a null ClearRecordsByStageId collection.";
                    return false;
                }

                if (slot.StageClearProfileSnapshot.ProcessedStageRunIds == null)
                {
                    reason = "Current payload contains a null ProcessedStageRunIds collection.";
                    return false;
                }

                if (slot.StageClearProfileSnapshot.ProcessedClearAttemptIds == null)
                {
                    reason = "Current payload contains a null ProcessedClearAttemptIds collection.";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class SaveSlotDto
    {
        public int SlotNumber;
        public string CurrentStageId;
        public string CurrentLevelGroupId;
        public int RemainingChances;
        public bool CampaignCompleted;
        public bool IntroPlayed;
        public bool OutroPlayed;
        public int TotalDeaths;
        public string LastPlayedAt;
        public StageClearProfileSnapshotDto StageClearProfileSnapshot;
    }

    [Serializable]
    public sealed class StageClearProfileSnapshotDto
    {
        public int Version;
        public PlayerStageClearRecordDto[] ClearRecordsByStageId = Array.Empty<PlayerStageClearRecordDto>();
        public string[] ProcessedStageRunIds = Array.Empty<string>();
        public string[] ProcessedClearAttemptIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class PlayerStageClearRecordDto
    {
        public string StageId;
        public bool HasAttempted;
        public bool HasCleared;
        public int ClearCount;
        public string[] ProcessedStageRunIds = Array.Empty<string>();
    }

    public static class SaveSlotDtoMapper
    {
        public static SaveSlotStoreDto CreateEmptyDto()
        {
            return ToDto(CreateEmptySlots());
        }

        public static SaveSlotStoreDto ToDto(IReadOnlyList<SaveSlotData> slots)
        {
            var normalizedSlots = NormalizeSlots(slots);
            var dtoSlots = new SaveSlotDto[SaveSlotStore.SlotCount];
            for (var i = 0; i < dtoSlots.Length; i++)
            {
                dtoSlots[i] = ToDto(normalizedSlots[i]);
            }

            return new SaveSlotStoreDto
            {
                SchemaId = SaveSlotStore.SchemaId,
                SchemaVersion = SaveSlotStore.SchemaVersion,
                SaveVersion = SaveSlotStore.SaveVersion,
                Slots = dtoSlots,
            };
        }

        public static SaveSlotData[] FromDto(SaveSlotStoreDto dto)
        {
            var result = CreateEmptySlots();
            if (dto == null || dto.Slots == null)
            {
                return result;
            }

            for (var i = 0; i < dto.Slots.Length; i++)
            {
                var slotDto = dto.Slots[i];
                if (slotDto == null || !SaveSlotStore.IsValidSlotNumber(slotDto.SlotNumber))
                {
                    continue;
                }

                result[slotDto.SlotNumber - 1] = FromDto(slotDto);
            }

            return result;
        }

        public static StageClearProfileSnapshotDto ToDto(StageClearProfileSnapshot snapshot)
        {
            snapshot ??= new StageClearProfileSnapshot();

            var records = new List<PlayerStageClearRecordDto>();
            foreach (var pair in snapshot.ClearRecordsByStageId)
            {
                if (!pair.Key.IsValid || pair.Value == null)
                {
                    continue;
                }

                records.Add(ToDto(pair.Value));
            }

            return new StageClearProfileSnapshotDto
            {
                Version = snapshot.Version,
                ClearRecordsByStageId = records.ToArray(),
                ProcessedStageRunIds = ToArray(snapshot.ProcessedStageRunIds),
                ProcessedClearAttemptIds = ToArray(snapshot.ProcessedClearAttemptIds),
            };
        }

        public static StageClearProfileSnapshot FromDto(StageClearProfileSnapshotDto dto)
        {
            var snapshot = new StageClearProfileSnapshot();
            if (dto == null)
            {
                return snapshot;
            }

            snapshot.Version = Math.Max(0, dto.Version);

            var records = dto.ClearRecordsByStageId ?? Array.Empty<PlayerStageClearRecordDto>();
            for (var i = 0; i < records.Length; i++)
            {
                var item = FromDto(records[i]);
                if (item.StageId.IsValid)
                {
                    snapshot.ClearRecordsByStageId[item.StageId] = item;
                }
            }

            snapshot.ProcessedStageRunIds = new HashSet<string>(
                dto.ProcessedStageRunIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            snapshot.ProcessedClearAttemptIds = new HashSet<string>(
                dto.ProcessedClearAttemptIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            return snapshot;
        }

        private static SaveSlotDto ToDto(SaveSlotData slot)
        {
            slot ??= SaveSlotData.CreateEmpty(1);
            return new SaveSlotDto
            {
                SlotNumber = slot.SlotNumber,
                CurrentStageId = slot.CurrentStageId.IsValid ? slot.CurrentStageId.Value : string.Empty,
                CurrentLevelGroupId = slot.CurrentLevelGroupId ?? string.Empty,
                RemainingChances = slot.RemainingChances,
                CampaignCompleted = slot.CampaignCompleted,
                IntroPlayed = slot.IntroPlayed,
                OutroPlayed = slot.OutroPlayed,
                TotalDeaths = slot.TotalDeaths,
                LastPlayedAt = slot.LastPlayedAt ?? string.Empty,
                StageClearProfileSnapshot = ToDto(slot.StageClearProfileSnapshot),
            };
        }

        private static SaveSlotData FromDto(SaveSlotDto dto)
        {
            var stageId = StageId.TryCreate(dto.CurrentStageId, out var parsedStageId)
                ? parsedStageId
                : StageId.None;
            return new SaveSlotData
            {
                SlotNumber = dto.SlotNumber,
                CurrentStageId = stageId,
                CurrentLevelGroupId = dto.CurrentLevelGroupId ?? string.Empty,
                RemainingChances = dto.RemainingChances > 0 ? dto.RemainingChances : SaveSlotStore.DefaultRemainingChances,
                CampaignCompleted = dto.CampaignCompleted,
                IntroPlayed = dto.IntroPlayed,
                OutroPlayed = dto.OutroPlayed,
                TotalDeaths = Math.Max(0, dto.TotalDeaths),
                LastPlayedAt = dto.LastPlayedAt ?? string.Empty,
                StageClearProfileSnapshot = FromDto(dto.StageClearProfileSnapshot),
            };
        }

        private static PlayerStageClearRecordDto ToDto(PlayerStageClearRecord record)
        {
            return new PlayerStageClearRecordDto
            {
                StageId = record.StageId.IsValid ? record.StageId.Value : string.Empty,
                HasAttempted = record.HasAttempted,
                HasCleared = record.HasCleared,
                ClearCount = record.ClearCount,
                ProcessedStageRunIds = CloneArray(record.ProcessedStageRunIds),
            };
        }

        private static PlayerStageClearRecord FromDto(PlayerStageClearRecordDto dto)
        {
            if (dto == null || !StageId.TryCreate(dto.StageId, out var stageId))
            {
                return PlayerStageClearRecord.CreateEmpty(StageId.None);
            }

            return new PlayerStageClearRecord
            {
                StageId = stageId,
                HasAttempted = dto.HasAttempted,
                HasCleared = dto.HasCleared,
                ClearCount = Math.Max(0, dto.ClearCount),
                ProcessedStageRunIds = CloneArray(dto.ProcessedStageRunIds),
            };
        }

        private static SaveSlotData[] CreateEmptySlots()
        {
            var slots = new SaveSlotData[SaveSlotStore.SlotCount];
            for (var i = 0; i < slots.Length; i++)
            {
                slots[i] = SaveSlotData.CreateEmpty(i + 1);
            }

            return slots;
        }

        private static SaveSlotData[] NormalizeSlots(IReadOnlyList<SaveSlotData> slots)
        {
            var result = CreateEmptySlots();
            if (slots == null)
            {
                return result;
            }

            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || !SaveSlotStore.IsValidSlotNumber(slot.SlotNumber))
                {
                    continue;
                }

                result[slot.SlotNumber - 1] = slot.Clone();
            }

            return result;
        }

        private static string[] ToArray(HashSet<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<string>();
            }

            var result = new string[values.Count];
            values.CopyTo(result);
            return result;
        }

        private static string[] CloneArray(string[] values)
        {
            return (string[])(values ?? Array.Empty<string>()).Clone();
        }
    }

    public enum StandaloneCampaignSaveSeedImportStatus
    {
        None = 0,
        Imported = 1,
        ImportedButSeedDeleteFailed = 2,
        FileNotFound = 3,
        EmptyPath = 4,
        InvalidJson = 5,
        UnsupportedVersion = 6,
        InvalidSlotNumber = 7,
        InvalidStageId = 8,
        StageMissingFromSequence = 9,
        StageMissingFromCatalog = 10,
        Exception = 11,
        EditorRuntimeSkipped = 12,
    }

    public readonly struct StandaloneCampaignSaveSeedImportResult
    {
        public StandaloneCampaignSaveSeedImportResult(
            StandaloneCampaignSaveSeedImportStatus status,
            string seedPath,
            int slotNumber,
            StageId stageId,
            string message)
        {
            Status = status;
            SeedPath = seedPath ?? string.Empty;
            SlotNumber = slotNumber;
            StageId = stageId;
            Message = message ?? string.Empty;
        }

        public StandaloneCampaignSaveSeedImportStatus Status { get; }

        public string SeedPath { get; }

        public int SlotNumber { get; }

        public StageId StageId { get; }

        public string Message { get; }

        public bool Imported =>
            Status == StandaloneCampaignSaveSeedImportStatus.Imported ||
            Status == StandaloneCampaignSaveSeedImportStatus.ImportedButSeedDeleteFailed;
    }

    public static class StandaloneCampaignSaveSeedImporter
    {
        public const int SeedVersion = 1;
        public const string SeedFileName = "campaign-save-seed.json";

        public static string BuildSeedJson(
            StageId stageId,
            int slotNumber,
            int remainingChances)
        {
            if (!stageId.IsValid)
            {
                throw new ArgumentException("Standalone campaign seed requires a valid StageId.", nameof(stageId));
            }

            SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
            remainingChances = Mathf.Clamp(remainingChances, 1, SaveSlotStore.DefaultRemainingChances);
            return JsonUtility.ToJson(
                new StandaloneCampaignSaveSeedDto
                {
                    Version = SeedVersion,
                    SlotNumber = slotNumber,
                    StageId = stageId.Value,
                    RemainingChances = remainingChances,
                },
                prettyPrint: true);
        }

        public static bool TryImportDefaultSeed(
            ICampaignSaveSlotStore saveSlotStore,
            ActiveSlotProvider activeSlotProvider,
            CampaignStageSequenceResolver sequenceResolver,
            IStageCatalogProvider stageCatalogProvider,
            out StandaloneCampaignSaveSeedImportResult result)
        {
#if UNITY_EDITOR
            result = new StandaloneCampaignSaveSeedImportResult(
                StandaloneCampaignSaveSeedImportStatus.EditorRuntimeSkipped,
                string.Empty,
                0,
                StageId.None,
                "Standalone save seed import is skipped in the Unity editor.");
            return false;
#else
            if (saveSlotStore == null)
            {
                throw new ArgumentNullException(nameof(saveSlotStore));
            }

            if (activeSlotProvider == null)
            {
                throw new ArgumentNullException(nameof(activeSlotProvider));
            }

            foreach (var seedPath in EnumerateDefaultSeedPaths())
            {
                if (!File.Exists(seedPath))
                {
                    continue;
                }

                return TryImportSeedFile(
                    seedPath,
                    saveSlotStore,
                    activeSlotProvider,
                    sequenceResolver,
                    stageCatalogProvider,
                    deleteAfterImport: true,
                    out result);
            }

            result = new StandaloneCampaignSaveSeedImportResult(
                StandaloneCampaignSaveSeedImportStatus.FileNotFound,
                string.Empty,
                0,
                StageId.None,
                "No standalone campaign save seed file was found.");
            return false;
#endif
        }

        public static bool TryImportSeedFile(
            string seedPath,
            ICampaignSaveSlotStore saveSlotStore,
            ActiveSlotProvider activeSlotProvider,
            CampaignStageSequenceResolver sequenceResolver,
            IStageCatalogProvider stageCatalogProvider,
            bool deleteAfterImport,
            out StandaloneCampaignSaveSeedImportResult result)
        {
            if (string.IsNullOrWhiteSpace(seedPath))
            {
                result = Failure(StandaloneCampaignSaveSeedImportStatus.EmptyPath, seedPath, "Seed path is empty.");
                return false;
            }

            if (!File.Exists(seedPath))
            {
                result = Failure(StandaloneCampaignSaveSeedImportStatus.FileNotFound, seedPath, "Seed file was not found.");
                return false;
            }

            if (saveSlotStore == null)
            {
                throw new ArgumentNullException(nameof(saveSlotStore));
            }

            if (activeSlotProvider == null)
            {
                throw new ArgumentNullException(nameof(activeSlotProvider));
            }

            if (sequenceResolver == null)
            {
                throw new ArgumentNullException(nameof(sequenceResolver));
            }

            if (stageCatalogProvider == null)
            {
                throw new ArgumentNullException(nameof(stageCatalogProvider));
            }

            try
            {
                var rawJson = File.ReadAllText(seedPath);
                var seed = JsonUtility.FromJson<StandaloneCampaignSaveSeedDto>(rawJson);
                if (seed == null)
                {
                    result = Failure(StandaloneCampaignSaveSeedImportStatus.InvalidJson, seedPath, "Seed file could not be parsed.");
                    return false;
                }

                if (seed.Version != SeedVersion)
                {
                    result = Failure(
                        StandaloneCampaignSaveSeedImportStatus.UnsupportedVersion,
                        seedPath,
                        $"Seed version '{seed.Version}' is not supported.");
                    return false;
                }

                if (!SaveSlotStore.IsValidSlotNumber(seed.SlotNumber))
                {
                    result = Failure(
                        StandaloneCampaignSaveSeedImportStatus.InvalidSlotNumber,
                        seedPath,
                        $"Seed slot number '{seed.SlotNumber}' is not valid.");
                    return false;
                }

                if (!StageId.TryCreate(seed.StageId, out var stageId))
                {
                    result = Failure(
                        StandaloneCampaignSaveSeedImportStatus.InvalidStageId,
                        seedPath,
                        $"Seed stage id '{seed.StageId}' is not valid.");
                    return false;
                }

                if (!sequenceResolver.Contains(stageId))
                {
                    result = Failure(
                        StandaloneCampaignSaveSeedImportStatus.StageMissingFromSequence,
                        seedPath,
                        $"Seed stage id '{stageId.Value}' is not in the campaign sequence.");
                    return false;
                }

                var catalogResolver = new StageCatalogResolver(stageCatalogProvider);
                if (!catalogResolver.TryResolve(stageId, out _))
                {
                    result = Failure(
                        StandaloneCampaignSaveSeedImportStatus.StageMissingFromCatalog,
                        seedPath,
                        $"Seed stage id '{stageId.Value}' is missing from the stage catalog.");
                    return false;
                }

                var remainingChances = Mathf.Clamp(
                    seed.RemainingChances,
                    1,
                    SaveSlotStore.DefaultRemainingChances);
                saveSlotStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = seed.SlotNumber,
                    CurrentStageId = stageId,
                    CurrentLevelGroupId = sequenceResolver.GetLevelGroupId(stageId),
                    RemainingChances = remainingChances,
                    LastPlayedAt = DateTimeOffset.UtcNow.ToString("O"),
                });
                activeSlotProvider.SetActiveSlot(seed.SlotNumber);

                if (deleteAfterImport)
                {
                    try
                    {
                        File.Delete(seedPath);
                    }
                    catch (Exception exception)
                    {
                        result = new StandaloneCampaignSaveSeedImportResult(
                            StandaloneCampaignSaveSeedImportStatus.ImportedButSeedDeleteFailed,
                            seedPath,
                            seed.SlotNumber,
                            stageId,
                            $"Seed imported, but the seed file could not be deleted. {exception.Message}");
                        return true;
                    }
                }

                result = new StandaloneCampaignSaveSeedImportResult(
                    StandaloneCampaignSaveSeedImportStatus.Imported,
                    seedPath,
                    seed.SlotNumber,
                    stageId,
                    "Seed imported.");
                return true;
            }
            catch (Exception exception)
            {
                result = Failure(
                    StandaloneCampaignSaveSeedImportStatus.Exception,
                    seedPath,
                    exception.Message);
                return false;
            }
        }

        public static IReadOnlyList<string> EnumerateDefaultSeedPaths()
        {
            var paths = new List<string>();
            AddUnique(paths, Path.Combine(Application.persistentDataPath, SeedFileName));

            var dataPath = Application.dataPath;
            if (!string.IsNullOrWhiteSpace(dataPath))
            {
                var dataDirectory = Directory.GetParent(dataPath);
                if (dataDirectory != null)
                {
                    AddUnique(paths, Path.Combine(dataDirectory.FullName, SeedFileName));
                }
            }

            return paths;
        }

        private static StandaloneCampaignSaveSeedImportResult Failure(
            StandaloneCampaignSaveSeedImportStatus status,
            string seedPath,
            string message)
        {
            return new StandaloneCampaignSaveSeedImportResult(
                status,
                seedPath,
                0,
                StageId.None,
                message);
        }

        private static void AddUnique(List<string> paths, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            for (var i = 0; i < paths.Count; i++)
            {
                if (string.Equals(paths[i], path, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            paths.Add(path);
        }

        [Serializable]
        private sealed class StandaloneCampaignSaveSeedDto
        {
            public int Version;
            public int SlotNumber;
            public string StageId;
            public int RemainingChances;
        }
    }
}
