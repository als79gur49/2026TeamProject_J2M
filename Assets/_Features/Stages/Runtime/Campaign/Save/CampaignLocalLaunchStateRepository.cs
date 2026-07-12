using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Game.Feature.Stages
{
    public static class CampaignLocalLaunchStateRepository
    {
        public const int SchemaVersion = 1;
        public const string FileName = "local-launch-state.json";
    }

    public sealed class FileCampaignLocalLaunchStateRepository : ICampaignLocalLaunchStateRepository
    {
        private readonly IAtomicTextFileStore _textFileStore;
        private readonly Func<DateTime> _utcNow;

        public FileCampaignLocalLaunchStateRepository(
            IAtomicTextFileStore textFileStore,
            Func<DateTime> utcNow = null)
        {
            _textFileStore = textFileStore ?? throw new ArgumentNullException(nameof(textFileStore));
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public CampaignLocalLaunchStateLoadResult Load()
        {
            try
            {
                _textFileStore.CleanupTempFiles(CampaignLocalLaunchStateRepository.FileName);
                if (!_textFileStore.Exists(CampaignLocalLaunchStateRepository.FileName))
                {
                    return new CampaignLocalLaunchStateLoadResult(
                        CampaignLocalLaunchStateLoadStatus.Missing,
                        null,
                        "local-launch-state.json is missing.");
                }

                var rawState = _textFileStore.ReadAllText(CampaignLocalLaunchStateRepository.FileName);
                if (!CampaignJsonSyntaxValidator.IsValid(rawState))
                {
                    return new CampaignLocalLaunchStateLoadResult(
                        CampaignLocalLaunchStateLoadStatus.CorruptNoFallback,
                        null,
                        "local-launch-state.json is corrupt.");
                }

                var document = JsonUtility.FromJson<CampaignLocalLaunchStateDocument>(rawState);
                return Validate(document) == CampaignLocalLaunchStateLoadStatus.Loaded
                    ? new CampaignLocalLaunchStateLoadResult(
                        CampaignLocalLaunchStateLoadStatus.Loaded,
                        document,
                        "local-launch-state.json loaded.")
                    : new CampaignLocalLaunchStateLoadResult(
                        CampaignLocalLaunchStateLoadStatus.SchemaInvalid,
                        null,
                        "local-launch-state.json schema is invalid.");
            }
            catch (UnauthorizedAccessException exception)
            {
                return new CampaignLocalLaunchStateLoadResult(
                    CampaignLocalLaunchStateLoadStatus.Unauthorized,
                    null,
                    exception.Message);
            }
            catch (IOException exception)
            {
                return new CampaignLocalLaunchStateLoadResult(
                    CampaignLocalLaunchStateLoadStatus.IoFailed,
                    null,
                    exception.Message);
            }
            catch (ArgumentException exception)
            {
                return new CampaignLocalLaunchStateLoadResult(
                    CampaignLocalLaunchStateLoadStatus.CorruptNoFallback,
                    null,
                    exception.Message);
            }
        }

        public void SaveActiveSlot(int slotNumber)
        {
            Save(slotNumber);
        }

        public void ClearActiveSlot()
        {
            Save(0);
        }

        private void Save(int activeSlotNumber)
        {
            if (activeSlotNumber != 0)
            {
                SaveSlotStore.ThrowIfInvalidSlotNumber(activeSlotNumber);
            }

            var document = new CampaignLocalLaunchStateDocument
            {
                schemaVersion = CampaignLocalLaunchStateRepository.SchemaVersion,
                campaign = new CampaignLocalLaunchStateCampaignDocument
                {
                    activeSlotNumber = activeSlotNumber,
                    lastUpdatedUtc = _utcNow().ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
                },
            };
            _textFileStore.WriteAllTextAtomic(
                CampaignLocalLaunchStateRepository.FileName,
                JsonUtility.ToJson(document, prettyPrint: true));
        }

        private static CampaignLocalLaunchStateLoadStatus Validate(
            CampaignLocalLaunchStateDocument document)
        {
            if (document == null ||
                document.schemaVersion != CampaignLocalLaunchStateRepository.SchemaVersion ||
                document.campaign == null)
            {
                return CampaignLocalLaunchStateLoadStatus.SchemaInvalid;
            }

            if (document.campaign.activeSlotNumber != 0 &&
                !SaveSlotStore.IsValidSlotNumber(document.campaign.activeSlotNumber))
            {
                return CampaignLocalLaunchStateLoadStatus.SchemaInvalid;
            }

            return CampaignLocalLaunchStateLoadStatus.Loaded;
        }
    }
}
