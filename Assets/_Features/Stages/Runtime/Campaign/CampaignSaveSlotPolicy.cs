using System;

namespace Game.Feature.Stages
{
    public static class CampaignSaveSlotPolicy
    {
        public const int SlotCount = 3;
        public const int MaxRemainingChances = 3;
        public const int DefaultRemainingChances = MaxRemainingChances;

        public static bool IsValidSlotNumber(int slotNumber)
        {
            return slotNumber >= 1 && slotNumber <= SlotCount;
        }

        public static int RequireValidRemainingChances(int remainingChances)
        {
            if (!IsValidRemainingChances(remainingChances))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(remainingChances),
                    $"Campaign chances must be between 1 and {MaxRemainingChances}.");
            }

            return remainingChances;
        }

        public static bool IsValidRemainingChances(int remainingChances)
        {
            return remainingChances >= 1 && remainingChances <= MaxRemainingChances;
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
