using System;

namespace Game.Feature.Stages
{
    public static class CampaignSaveSlotPolicy
    {
        public const int SlotCount = 3;
        public const int DefaultRemainingChances = 3;

        public static bool IsValidSlotNumber(int slotNumber)
        {
            return slotNumber >= 1 && slotNumber <= SlotCount;
        }

        public static int ToRuntimeRemainingChances(int persistedRemainingChances)
        {
            return persistedRemainingChances <= 0
                ? DefaultRemainingChances
                : persistedRemainingChances;
        }

        public static void ThrowIfInvalidSlotNumber(int slotNumber)
        {
            if (!IsValidSlotNumber(slotNumber))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(slotNumber),
                    "Save slot number must be 1, 2, or 3.");
            }
        }
    }
}
