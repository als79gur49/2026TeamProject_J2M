using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Stages
{
    public sealed class SaveSlotData
    {
        public int SlotNumber { get; set; }

        public StageId CurrentStageId { get; set; }

        public string CurrentLevelGroupId { get; set; } = string.Empty;

        public int RemainingChances { get; set; } = SaveSlotStore.DefaultRemainingChances;

        public bool CampaignCompleted { get; set; }

        public int TotalDeaths { get; set; }

        public string LastPlayedAt { get; set; } = string.Empty;

        public StageCompletionProfileSnapshot StageCompletionProfileSnapshot { get; set; } = new();

        public bool IsEmpty => !CurrentStageId.IsValid &&
                               !CampaignCompleted &&
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
}
