using System;
using System.Collections.Generic;

namespace Game.Feature.Stages
{
    /// <summary>
    /// Non-persistent campaign slot storage for tests and short-lived diagnostic compositions.
    /// Production and DirectPlay campaign flows use the JSON-backed composition provider.
    /// </summary>
    public sealed class TransientCampaignSaveSlotStore : ICampaignSaveSlotStore
    {
        public const string DefaultDiagnosticsKey = "transient-campaign-state";

        private static readonly object Gate = new();
        private static readonly Dictionary<string, SaveSlotData[]> SlotsByNamespace = new();

        private readonly string _diagnosticsKey;

        public TransientCampaignSaveSlotStore(string diagnosticsKey = DefaultDiagnosticsKey)
        {
            _diagnosticsKey = string.IsNullOrWhiteSpace(diagnosticsKey)
                ? throw new ArgumentException("A transient campaign namespace is required.", nameof(diagnosticsKey))
                : diagnosticsKey;
            LastCampaignLoadReport = CampaignSaveLoadReport.Missing("Transient campaign state has not been read.");
        }

        public string DiagnosticsKey => _diagnosticsKey;

        public CampaignSaveLoadReport LastCampaignLoadReport { get; private set; }

        public SaveSlotData[] LoadAll()
        {
            return LoadAllWithReport().Slots;
        }

        public CampaignSaveLoadResult LoadAllWithReport()
        {
            lock (Gate)
            {
                if (!SlotsByNamespace.TryGetValue(_diagnosticsKey, out var slots))
                {
                    LastCampaignLoadReport = CampaignSaveLoadReport.Missing("Transient campaign state is empty.");
                    return new CampaignSaveLoadResult(CreateEmptySlots(), LastCampaignLoadReport);
                }

                LastCampaignLoadReport = CampaignSaveLoadReport.Loaded(
                    "Transient campaign state loaded.",
                    _diagnosticsKey);
                return new CampaignSaveLoadResult(CloneSlots(slots), LastCampaignLoadReport);
            }
        }

        public SaveSlotData LoadSlot(int slotNumber)
        {
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            return LoadAll()[slotNumber - 1];
        }

        public void SaveSlot(SaveSlotData slot)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slot.SlotNumber);
            lock (Gate)
            {
                var slots = GetOrCreateSlots();
                var storedSlot = slot.Clone();
                storedSlot.HasNormalCampaignCompletionReceipt =
                    storedSlot.HasNormalCampaignCompletionReceipt ||
                    storedSlot.NormalCampaignCompletionReceipt != null;
                slots[slot.SlotNumber - 1] = storedSlot;
            }
        }

        public SaveSlotData InitializeNewGame(
            int slotNumber,
            CampaignStageSequenceResolver sequenceResolver,
            string lastPlayedAt)
        {
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            var slot = SaveSlotData.CreateNewGame(slotNumber, sequenceResolver, lastPlayedAt);
            SaveSlot(slot);
            return slot.Clone();
        }

        public void UpdateSlot(int slotNumber, Action<SaveSlotData> mutation)
        {
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
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
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            lock (Gate)
            {
                var slots = GetOrCreateSlots();
                slots[slotNumber - 1] = SaveSlotData.CreateEmpty(slotNumber);
            }
        }

        public void ClearAll()
        {
            lock (Gate)
            {
                SlotsByNamespace.Remove(_diagnosticsKey);
            }

            LastCampaignLoadReport = CampaignSaveLoadReport.Missing("Transient campaign state cleared.");
        }

        private SaveSlotData[] GetOrCreateSlots()
        {
            if (!SlotsByNamespace.TryGetValue(_diagnosticsKey, out var slots))
            {
                slots = CreateEmptySlots();
                SlotsByNamespace.Add(_diagnosticsKey, slots);
            }

            return slots;
        }

        private static SaveSlotData[] CreateEmptySlots()
        {
            var slots = new SaveSlotData[CampaignSaveSlotPolicy.SlotCount];
            for (var index = 0; index < slots.Length; index++)
            {
                slots[index] = SaveSlotData.CreateEmpty(index + 1);
            }

            return slots;
        }

        private static SaveSlotData[] CloneSlots(SaveSlotData[] slots)
        {
            var clone = new SaveSlotData[slots.Length];
            for (var index = 0; index < slots.Length; index++)
            {
                clone[index] = slots[index].Clone();
            }

            return clone;
        }
    }

    /// <summary>
    /// Non-persistent active-slot storage for tests and short-lived diagnostic compositions.
    /// </summary>
    public sealed class TransientActiveSlotStorage : IActiveSlotStorage
    {
        public const string DefaultDiagnosticsKey = "transient-active-slot";

        private static readonly object Gate = new();
        private static readonly Dictionary<string, int> SlotsByNamespace = new();

        public TransientActiveSlotStorage(string diagnosticsKey = DefaultDiagnosticsKey)
        {
            DiagnosticsKey = string.IsNullOrWhiteSpace(diagnosticsKey)
                ? throw new ArgumentException("A transient active-slot namespace is required.", nameof(diagnosticsKey))
                : diagnosticsKey;
        }

        public string DiagnosticsKey { get; }

        public bool TryGetActiveSlot(out int slotNumber)
        {
            lock (Gate)
            {
                return SlotsByNamespace.TryGetValue(DiagnosticsKey, out slotNumber) &&
                       CampaignSaveSlotPolicy.IsValidSlotNumber(slotNumber);
            }
        }

        public void SetActiveSlot(int slotNumber)
        {
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            lock (Gate)
            {
                SlotsByNamespace[DiagnosticsKey] = slotNumber;
            }
        }

        public void ClearActiveSlot()
        {
            lock (Gate)
            {
                SlotsByNamespace.Remove(DiagnosticsKey);
            }
        }
    }
}
