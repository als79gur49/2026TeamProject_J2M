using System;
using UnityEngine;

namespace Game.Feature.Stages
{
    public interface IActiveSlotStorage
    {
        string DiagnosticsKey { get; }

        bool TryGetActiveSlot(out int slotNumber);

        void SetActiveSlot(int slotNumber);

        void ClearActiveSlot();
    }

    public sealed class PlayerPrefsActiveSlotStorage : IActiveSlotStorage
    {
        private readonly string _playerPrefsKey;

        public PlayerPrefsActiveSlotStorage(string playerPrefsKey)
        {
            _playerPrefsKey = string.IsNullOrWhiteSpace(playerPrefsKey)
                ? SaveSlotPrefsKeys.ActiveSaveSlotKey
                : playerPrefsKey;
        }

        public string DiagnosticsKey => _playerPrefsKey;

        public bool TryGetActiveSlot(out int slotNumber)
        {
            slotNumber = PlayerPrefs.GetInt(_playerPrefsKey, 0);
            if (SaveSlotStore.IsValidSlotNumber(slotNumber))
            {
                return true;
            }

            slotNumber = 0;
            return false;
        }

        public void SetActiveSlot(int slotNumber)
        {
            SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
            PlayerPrefs.SetInt(_playerPrefsKey, slotNumber);
            PlayerPrefs.Save();
        }

        public void ClearActiveSlot()
        {
            if (string.Equals(_playerPrefsKey, SaveSlotPrefsKeys.ActiveSaveSlotKey, StringComparison.Ordinal))
            {
                PlayerPrefs.SetInt(_playerPrefsKey, 0);
                PlayerPrefs.Save();
                return;
            }

            PlayerPrefs.DeleteKey(_playerPrefsKey);
            PlayerPrefs.Save();
        }
    }

    public sealed class LocalStateActiveSlotStorage : IActiveSlotStorage
    {
        private readonly ICampaignLocalLaunchStateRepository _repository;
        private readonly IActiveSlotStorage _legacyImportSource;
        private readonly ICampaignSaveSlotStore _profileSlots;

        public LocalStateActiveSlotStorage(
            ICampaignLocalLaunchStateRepository repository,
            IActiveSlotStorage legacyImportSource,
            ICampaignSaveSlotStore profileSlots)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _legacyImportSource = legacyImportSource ?? throw new ArgumentNullException(nameof(legacyImportSource));
            _profileSlots = profileSlots ?? throw new ArgumentNullException(nameof(profileSlots));
        }

        public string DiagnosticsKey => CampaignLocalLaunchStateRepository.FileName;

        public bool TryGetActiveSlot(out int slotNumber)
        {
            var loadResult = _repository.Load();
            if (loadResult.Status == CampaignLocalLaunchStateLoadStatus.Loaded)
            {
                return TryUseLoadedDocument(loadResult.Document, out slotNumber);
            }

            if (loadResult.AllowsPlayerPrefsImport)
            {
                return TryImportLegacyActiveSlot(out slotNumber);
            }

            slotNumber = 0;
            return false;
        }

        public void SetActiveSlot(int slotNumber)
        {
            SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
            if (!IsProfileSlotAvailable(slotNumber))
            {
                throw new InvalidOperationException(
                    $"Cannot set active campaign slot '{slotNumber}' because the profile slot is empty or missing.");
            }

            _repository.SaveActiveSlot(slotNumber);
        }

        public void ClearActiveSlot()
        {
            _repository.ClearActiveSlot();
        }

        private bool TryUseLoadedDocument(
            CampaignLocalLaunchStateDocument document,
            out int slotNumber)
        {
            slotNumber = document?.campaign?.activeSlotNumber ?? 0;
            if (!SaveSlotStore.IsValidSlotNumber(slotNumber))
            {
                slotNumber = 0;
                return false;
            }

            if (IsProfileSlotAvailable(slotNumber))
            {
                return true;
            }

            _repository.ClearActiveSlot();
            slotNumber = 0;
            return false;
        }

        private bool TryImportLegacyActiveSlot(out int slotNumber)
        {
            if (!_legacyImportSource.TryGetActiveSlot(out var legacySlotNumber) ||
                !IsProfileSlotAvailable(legacySlotNumber))
            {
                slotNumber = 0;
                return false;
            }

            _repository.SaveActiveSlot(legacySlotNumber);
            slotNumber = legacySlotNumber;
            return true;
        }

        private bool IsProfileSlotAvailable(int slotNumber)
        {
            if (!SaveSlotStore.IsValidSlotNumber(slotNumber))
            {
                return false;
            }

            try
            {
                return !_profileSlots.LoadSlot(slotNumber).IsEmpty;
            }
            catch
            {
                return false;
            }
        }
    }

    public sealed class ActiveSlotRepairingCampaignSaveSlotStore : ICampaignSaveSlotStore
    {
        private readonly ICampaignSaveSlotStore _inner;
        private readonly IActiveSlotStorage _activeSlotStorage;

        public ActiveSlotRepairingCampaignSaveSlotStore(
            ICampaignSaveSlotStore inner,
            IActiveSlotStorage activeSlotStorage)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _activeSlotStorage = activeSlotStorage ?? throw new ArgumentNullException(nameof(activeSlotStorage));
        }

        public string DiagnosticsKey => _inner.DiagnosticsKey;

        public CampaignSaveLoadReport LastCampaignLoadReport => _inner.LastCampaignLoadReport;

        public SaveSlotData[] LoadAll()
        {
            return _inner.LoadAll();
        }

        public CampaignSaveLoadResult LoadAllWithReport()
        {
            return _inner.LoadAllWithReport();
        }

        public SaveSlotData LoadSlot(int slotNumber)
        {
            return _inner.LoadSlot(slotNumber);
        }

        public void SaveSlot(SaveSlotData slot)
        {
            _inner.SaveSlot(slot);
        }

        public SaveSlotData InitializeNewGame(
            int slotNumber,
            CampaignStageSequenceResolver sequenceResolver,
            string lastPlayedAt)
        {
            return _inner.InitializeNewGame(slotNumber, sequenceResolver, lastPlayedAt);
        }

        public void UpdateSlot(int slotNumber, Action<SaveSlotData> mutation)
        {
            _inner.UpdateSlot(slotNumber, mutation);
        }

        public void DeleteSlot(int slotNumber)
        {
            var deletedSlotWasActive =
                _activeSlotStorage.TryGetActiveSlot(out var activeSlotNumber) &&
                activeSlotNumber == slotNumber;
            _inner.DeleteSlot(slotNumber);
            if (deletedSlotWasActive)
            {
                _activeSlotStorage.ClearActiveSlot();
            }
        }

        public void ClearAll()
        {
            _inner.ClearAll();
            _activeSlotStorage.ClearActiveSlot();
        }
    }
}
