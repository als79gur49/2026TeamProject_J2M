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

        public bool UsesTemporaryCampaignState { get; set; }

        public bool EnableCampaignFlow { get; set; }

        public bool CampaignRuntimeActive { get; set; }

        public bool HasActiveSlot { get; set; }

        public int ActiveSlotNumber { get; set; }

        public bool HasLaunchHandoff { get; set; }

        public int HandoffSlotNumber { get; set; }

        public string HandoffToken { get; set; } = string.Empty;

        public string SaveStoreDiagnosticsKey { get; set; } = string.Empty;

        public string ActiveSlotDiagnosticsKey { get; set; } = string.Empty;

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
                $"[CampaignChanceHUD] kind={Kind} scene={SceneName} source={Source} requested={RequestedStageId} launch={LaunchStageId} resolved={ResolvedStageId} directPlay={EditorDirectPlayMode} suppress={SuppressCampaignFlow} customNamespace={UsesTemporaryCampaignState} activeSlot={ActiveSlotNumber} hasActiveSlot={HasActiveSlot} handoffSlot={HandoffSlotNumber} hasHandoff={HasLaunchHandoff} handoffToken={HandoffToken} runtimeActive={CampaignRuntimeActive} sourceType={SourceType} sourceNull={SourceIsNull} read={TryReadResult} reason={FailureReason} sourceStage={SourceStageId} remaining={RemainingChances} max={MaxChances} playerFound={PlayerFound} finalHas={FinalHasChances}";
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
        public GameMode GameMode { get; set; } = GameMode.Hardcore;

        public int ResumeHp { get; set; }

        public int SlotNumber { get; set; }

        public StageId CurrentStageId { get; set; }

        public string CurrentLevelGroupId { get; set; } = string.Empty;

        public int RemainingChances { get; set; } = CampaignSaveSlotPolicy.DefaultRemainingChances;

        public bool CampaignCompleted { get; set; }

        public bool HasNormalCampaignCompletionReceipt { get; set; }

        public NormalCampaignCompletionReceipt NormalCampaignCompletionReceipt { get; set; }

        public NormalStagePerformanceRecord[] NormalStagePerformanceRecords { get; set; } =
            Array.Empty<NormalStagePerformanceRecord>();

        public bool IntroComicCompleted { get; set; }

        public bool OutroComicCompleted { get; set; }

        public int TotalDeaths { get; set; }

        public string LastPlayedAt { get; set; } = string.Empty;

        public StageClearProfileSnapshot StageClearProfileSnapshot { get; set; } = new();

        public bool IsEmpty => CampaignSlotRawDataMapper.IsEmpty(this);

        public SaveSlotData Clone()
        {
            return new SaveSlotData
            {
                SlotNumber = SlotNumber,
                CurrentStageId = CurrentStageId,
                CurrentLevelGroupId = CurrentLevelGroupId,
                GameMode = GameMode,
                ResumeHp = ResumeHp,
                RemainingChances = RemainingChances,
                CampaignCompleted = CampaignCompleted,
                HasNormalCampaignCompletionReceipt =
                    HasNormalCampaignCompletionReceipt,
                NormalCampaignCompletionReceipt = NormalCampaignCompletionReceipt?.Clone(),
                IntroComicCompleted = IntroComicCompleted,
                OutroComicCompleted = OutroComicCompleted,
                NormalStagePerformanceRecords = ClonePerformanceRecords(
                    NormalStagePerformanceRecords),
                TotalDeaths = TotalDeaths,
                LastPlayedAt = LastPlayedAt,
                StageClearProfileSnapshot = StageClearProfileSnapshot?.Clone(),
            };
        }

        public static SaveSlotData CreateEmpty(int slotNumber)
        {
            return new SaveSlotData
            {
                SlotNumber = slotNumber,
                CurrentStageId = StageId.None,
                CurrentLevelGroupId = string.Empty,
                RemainingChances = CampaignSaveSlotPolicy.DefaultRemainingChances,
                CampaignCompleted = false,
                HasNormalCampaignCompletionReceipt = false,
                NormalCampaignCompletionReceipt = null,
                IntroComicCompleted = false,
                OutroComicCompleted = false,
                NormalStagePerformanceRecords = Array.Empty<NormalStagePerformanceRecord>(),
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
                RemainingChances = CampaignSaveSlotPolicy.DefaultRemainingChances,
                CampaignCompleted = false,
                HasNormalCampaignCompletionReceipt = false,
                NormalCampaignCompletionReceipt = null,
                IntroComicCompleted = false,
                OutroComicCompleted = false,
                NormalStagePerformanceRecords = Array.Empty<NormalStagePerformanceRecord>(),
                TotalDeaths = 0,
                LastPlayedAt = lastPlayedAt ?? string.Empty,
                StageClearProfileSnapshot = new StageClearProfileSnapshot(),
            };
        }

        private static NormalStagePerformanceRecord[] ClonePerformanceRecords(
            NormalStagePerformanceRecord[] records)
        {
            if (records == null)
            {
                return null;
            }

            var clone = new NormalStagePerformanceRecord[records.Length];
            for (var index = 0; index < records.Length; index++)
            {
                clone[index] = records[index]?.Clone();
            }

            return clone;
        }
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

        internal static string NormalizeRoot(string root)
        {
            if (string.IsNullOrWhiteSpace(root)) return string.Empty;
            var path = Path.GetFullPath(root.Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar));
            path = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (path.Length == 0) path = Path.GetPathRoot(Path.GetFullPath(root));
            return Path.DirectorySeparatorChar == '\\' ? path.ToUpperInvariant() : path;
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

    public sealed class ActiveSlotProvider
    {
        private readonly IActiveSlotStorage _storage;
        private int _activeSlotNumber;

        public ActiveSlotProvider(IActiveSlotStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _activeSlotNumber = _storage.TryGetActiveSlot(out var activeSlotNumber)
                ? activeSlotNumber
                : 0;
        }

        public bool HasActiveSlot
        {
            get
            {
                RefreshActiveSlot();
                return CampaignSaveSlotPolicy.IsValidSlotNumber(_activeSlotNumber);
            }
        }

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
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            _storage.SetActiveSlot(slotNumber);
            _activeSlotNumber = slotNumber;
        }

        public void ClearActiveSlot()
        {
            _storage.ClearActiveSlot();
            _activeSlotNumber = 0;
        }

        private void RefreshActiveSlot()
        {
            _activeSlotNumber = _storage.TryGetActiveSlot(out var activeSlotNumber)
                ? activeSlotNumber
                : 0;
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
        InvalidRemainingChances = 13,
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
        public const int SeedVersion = 2;
        public const string SeedFileName = "campaign-save-seed.json";

        public static string BuildSeedJson(
            StageId stageId,
            int slotNumber,
            int remainingChances,
            GameMode gameMode = GameMode.Hardcore,
            int resumeHp = 0)
        {
            if (!stageId.IsValid)
            {
                throw new ArgumentException("Standalone campaign seed requires a valid StageId.", nameof(stageId));
            }

            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            if (!CampaignSaveSlotPolicy.IsValidSurvival(gameMode, resumeHp, remainingChances))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(remainingChances),
                    $"Standalone campaign seed mode and survival values are invalid.");
            }

            return JsonUtility.ToJson(
                new StandaloneCampaignSaveSeedDto
                {
                    Version = SeedVersion,
                    SlotNumber = slotNumber,
                    StageId = stageId.Value,
                    RemainingChances = remainingChances,
                    GameMode = gameMode,
                    ResumeHp = resumeHp,
                },
                prettyPrint: true);
        }

        public static bool TryImportDefaultSeed(
            ICampaignSlotSeedImportPort saveSlotStore,
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
            ICampaignSlotSeedImportPort saveSlotStore,
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

                if (!CampaignSaveSlotPolicy.IsValidSlotNumber(seed.SlotNumber))
                {
                    result = Failure(
                        StandaloneCampaignSaveSeedImportStatus.InvalidSlotNumber,
                        seedPath,
                        $"Seed slot number '{seed.SlotNumber}' is not valid.");
                    return false;
                }

                if (!CampaignSaveSlotPolicy.IsValidSurvival(seed.GameMode, seed.ResumeHp, seed.RemainingChances))
                {
                    result = Failure(
                        StandaloneCampaignSaveSeedImportStatus.InvalidRemainingChances,
                        seedPath,
                        "Seed mode and survival values are invalid.");
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

                saveSlotStore.ImportSlotSeed(new CampaignSlotSeedImportRequest(
                    seed.SlotNumber,
                    stageId,
                    sequenceResolver.GetLevelGroupId(stageId),
                    seed.RemainingChances,
                    DateTimeOffset.UtcNow.ToString("O"), seed.GameMode, seed.ResumeHp));
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
            public GameMode GameMode;
            public int ResumeHp;
        }
    }
}
