using System;
using System.Collections.Generic;
using System.IO;
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

        public StageCompletionProfileSnapshot StageCompletionProfileSnapshot { get; set; } = new();

        public bool IsEmpty => !CurrentStageId.IsValid &&
                               !CampaignCompleted &&
                               !IntroPlayed &&
                               !OutroPlayed &&
                               TotalDeaths == 0 &&
                               string.IsNullOrWhiteSpace(LastPlayedAt) &&
                               IsCompletionProfileEmpty(StageCompletionProfileSnapshot);

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
                StageCompletionProfileSnapshot = StageCompletionProfileSnapshot?.Clone() ?? new StageCompletionProfileSnapshot(),
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
                StageCompletionProfileSnapshot = new StageCompletionProfileSnapshot(),
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
                StageCompletionProfileSnapshot = new StageCompletionProfileSnapshot(),
            };
        }

        private static bool IsCompletionProfileEmpty(StageCompletionProfileSnapshot snapshot)
        {
            return snapshot == null ||
                   (snapshot.Version == 0 &&
                    snapshot.InventoryBalances.Count == 0 &&
                    snapshot.ProgressByStageId.Count == 0 &&
                    snapshot.ProcessedStageRunIds.Count == 0 &&
                    snapshot.ProcessedCompletionAttemptIds.Count == 0 &&
                    snapshot.AppliedRewardGrantIds.Count == 0);
        }
    }

    public sealed class ActiveSlotProvider
    {
        private const string DefaultPlayerPrefsKey = "Game.Feature.Stages.ActiveSaveSlot";
        private readonly string _playerPrefsKey;
        private int _activeSlotNumber;

        public ActiveSlotProvider(string playerPrefsKey = DefaultPlayerPrefsKey)
        {
            _playerPrefsKey = string.IsNullOrWhiteSpace(playerPrefsKey)
                ? DefaultPlayerPrefsKey
                : playerPrefsKey;
            _activeSlotNumber = PlayerPrefs.GetInt(_playerPrefsKey, 0);
        }

        public bool HasActiveSlot => SaveSlotStore.IsValidSlotNumber(_activeSlotNumber);

        public string PlayerPrefsKey => _playerPrefsKey;

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
            _activeSlotNumber = slotNumber;
            PlayerPrefs.SetInt(_playerPrefsKey, slotNumber);
            PlayerPrefs.Save();
        }

        public void ClearActiveSlot()
        {
            _activeSlotNumber = 0;
            PlayerPrefs.DeleteKey(_playerPrefsKey);
            PlayerPrefs.Save();
        }
    }

    public sealed class SaveSlotStore
    {
        public const int SaveVersion = 1;
        public const int SlotCount = 3;
        public const int DefaultRemainingChances = 3;
        public const string DefaultPlayerPrefsKey = "Game.Feature.Stages.SaveSlots";

        private readonly string _playerPrefsKey;

        public SaveSlotStore(string playerPrefsKey = DefaultPlayerPrefsKey)
        {
            _playerPrefsKey = string.IsNullOrWhiteSpace(playerPrefsKey)
                ? DefaultPlayerPrefsKey
                : playerPrefsKey;
        }

        public string PlayerPrefsKey => _playerPrefsKey;

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
            return SaveSlotDtoMapper.FromDto(LoadDto());
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
            PlayerPrefs.DeleteKey(_playerPrefsKey);
            PlayerPrefs.Save();
        }

        private void SaveAll(SaveSlotData[] slots)
        {
            PlayerPrefs.SetString(_playerPrefsKey, JsonUtility.ToJson(SaveSlotDtoMapper.ToDto(slots)));
            PlayerPrefs.Save();
        }

        private SaveSlotStoreDto LoadDto()
        {
            if (!PlayerPrefs.HasKey(_playerPrefsKey))
            {
                return SaveSlotDtoMapper.CreateEmptyDto();
            }

            var rawJson = PlayerPrefs.GetString(_playerPrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return SaveSlotDtoMapper.CreateEmptyDto();
            }

            try
            {
                var dto = JsonUtility.FromJson<SaveSlotStoreDto>(rawJson);
                if (dto == null || dto.SaveVersion != SaveVersion)
                {
                    return SaveSlotDtoMapper.CreateEmptyDto();
                }

                return dto;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Save slot data could not be parsed and will be ignored. {exception.Message}");
                return SaveSlotDtoMapper.CreateEmptyDto();
            }
        }
    }

    public sealed class SaveSlotStageCompletionProfileStore : IStageCompletionProfileStore
    {
        private readonly SaveSlotStore _saveSlotStore;
        private readonly ActiveSlotProvider _activeSlotProvider;

        public SaveSlotStageCompletionProfileStore(
            SaveSlotStore saveSlotStore,
            ActiveSlotProvider activeSlotProvider)
        {
            _saveSlotStore = saveSlotStore ?? throw new ArgumentNullException(nameof(saveSlotStore));
            _activeSlotProvider = activeSlotProvider ?? throw new ArgumentNullException(nameof(activeSlotProvider));
        }

        public StageCompletionProfileSnapshot Load()
        {
            var slot = _saveSlotStore.LoadSlot(_activeSlotProvider.ActiveSlotNumber);
            return slot.StageCompletionProfileSnapshot?.Clone() ?? new StageCompletionProfileSnapshot();
        }

        public void Save(StageCompletionProfileSnapshot snapshot)
        {
            _saveSlotStore.UpdateSlot(
                _activeSlotProvider.ActiveSlotNumber,
                slot => slot.StageCompletionProfileSnapshot = snapshot?.Clone() ?? new StageCompletionProfileSnapshot());
        }
    }

    [Serializable]
    public sealed class SaveSlotStoreDto
    {
        public int SaveVersion = SaveSlotStore.SaveVersion;
        public SaveSlotDto[] Slots = Array.Empty<SaveSlotDto>();
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
        public StageCompletionProfileSnapshotDto StageCompletionProfileSnapshot;
    }

    [Serializable]
    public sealed class StageCompletionProfileSnapshotDto
    {
        public int Version;
        public StringIntPairDto[] InventoryBalances = Array.Empty<StringIntPairDto>();
        public PlayerStageProgressDto[] ProgressByStageId = Array.Empty<PlayerStageProgressDto>();
        public string[] ProcessedStageRunIds = Array.Empty<string>();
        public string[] ProcessedCompletionAttemptIds = Array.Empty<string>();
        public string[] AppliedRewardGrantIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class StringIntPairDto
    {
        public string Key;
        public int Value;
    }

    [Serializable]
    public sealed class PlayerStageProgressDto
    {
        public string StageId;
        public bool HasStarted;
        public bool HasCleared;
        public int ClearCount;
        public int BestScore;
        public int BestStars;
        public string BestRankId;
        public string[] CompletedChallengeIds = Array.Empty<string>();
        public string[] ConsumedRewardRuleIds = Array.Empty<string>();
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

        public static StageCompletionProfileSnapshotDto ToDto(StageCompletionProfileSnapshot snapshot)
        {
            snapshot ??= new StageCompletionProfileSnapshot();

            var inventory = new List<StringIntPairDto>();
            foreach (var pair in snapshot.InventoryBalances)
            {
                inventory.Add(new StringIntPairDto { Key = pair.Key, Value = pair.Value });
            }

            var progress = new List<PlayerStageProgressDto>();
            foreach (var pair in snapshot.ProgressByStageId)
            {
                if (!pair.Key.IsValid || pair.Value == null)
                {
                    continue;
                }

                progress.Add(ToDto(pair.Value));
            }

            return new StageCompletionProfileSnapshotDto
            {
                Version = snapshot.Version,
                InventoryBalances = inventory.ToArray(),
                ProgressByStageId = progress.ToArray(),
                ProcessedStageRunIds = ToArray(snapshot.ProcessedStageRunIds),
                ProcessedCompletionAttemptIds = ToArray(snapshot.ProcessedCompletionAttemptIds),
                AppliedRewardGrantIds = ToArray(snapshot.AppliedRewardGrantIds),
            };
        }

        public static StageCompletionProfileSnapshot FromDto(StageCompletionProfileSnapshotDto dto)
        {
            var snapshot = new StageCompletionProfileSnapshot();
            if (dto == null)
            {
                return snapshot;
            }

            snapshot.Version = Math.Max(0, dto.Version);
            var inventory = dto.InventoryBalances ?? Array.Empty<StringIntPairDto>();
            for (var i = 0; i < inventory.Length; i++)
            {
                var pair = inventory[i];
                if (pair == null || string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                snapshot.InventoryBalances[pair.Key] = pair.Value;
            }

            var progress = dto.ProgressByStageId ?? Array.Empty<PlayerStageProgressDto>();
            for (var i = 0; i < progress.Length; i++)
            {
                var item = FromDto(progress[i]);
                if (item.StageId.IsValid)
                {
                    snapshot.ProgressByStageId[item.StageId] = item;
                }
            }

            snapshot.ProcessedStageRunIds = new HashSet<string>(
                dto.ProcessedStageRunIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            snapshot.ProcessedCompletionAttemptIds = new HashSet<string>(
                dto.ProcessedCompletionAttemptIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            snapshot.AppliedRewardGrantIds = new HashSet<string>(
                dto.AppliedRewardGrantIds ?? Array.Empty<string>(),
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
                StageCompletionProfileSnapshot = ToDto(slot.StageCompletionProfileSnapshot),
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
                StageCompletionProfileSnapshot = FromDto(dto.StageCompletionProfileSnapshot),
            };
        }

        private static PlayerStageProgressDto ToDto(PlayerStageProgress progress)
        {
            return new PlayerStageProgressDto
            {
                StageId = progress.StageId.IsValid ? progress.StageId.Value : string.Empty,
                HasStarted = progress.HasStarted,
                HasCleared = progress.HasCleared,
                ClearCount = progress.ClearCount,
                BestScore = progress.BestScore,
                BestStars = progress.BestStars,
                BestRankId = progress.BestRankId ?? string.Empty,
                CompletedChallengeIds = CloneArray(progress.CompletedChallengeIds),
                ConsumedRewardRuleIds = CloneArray(progress.ConsumedRewardRuleIds),
                ProcessedStageRunIds = CloneArray(progress.ProcessedStageRunIds),
            };
        }

        private static PlayerStageProgress FromDto(PlayerStageProgressDto dto)
        {
            if (dto == null || !StageId.TryCreate(dto.StageId, out var stageId))
            {
                return PlayerStageProgress.CreateEmpty(StageId.None);
            }

            return new PlayerStageProgress
            {
                StageId = stageId,
                HasStarted = dto.HasStarted,
                HasCleared = dto.HasCleared,
                ClearCount = Math.Max(0, dto.ClearCount),
                BestScore = Math.Max(0, dto.BestScore),
                BestStars = Math.Max(0, dto.BestStars),
                BestRankId = dto.BestRankId ?? string.Empty,
                CompletedChallengeIds = CloneArray(dto.CompletedChallengeIds),
                ConsumedRewardRuleIds = CloneArray(dto.ConsumedRewardRuleIds),
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
            var saveSlotStore = new SaveSlotStore();
            var activeSlotProvider = new ActiveSlotProvider();
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
            SaveSlotStore saveSlotStore,
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
