using System;

namespace Game.Feature.Stages
{
    public enum GameMode
    {
        Unknown = 0,
        Casual = 1,
        Hardcore = 2,
    }

    public static class CampaignSaveSlotPolicy
    {
        public const int CasualMaxHp = 3;
        public const float CasualDamageCooldownSeconds = 2f;

        public static bool IsValidSurvival(GameMode mode, int resumeHp, int remainingChances)
        {
            return mode == GameMode.Casual
                ? resumeHp >= 1 && resumeHp <= CasualMaxHp && remainingChances == 0
                : mode == GameMode.Hardcore && resumeHp == 0 && IsValidRemainingChances(remainingChances);
        }

        public static void RequireValidSurvival(GameMode mode, int resumeHp, int remainingChances)
        {
            if (mode != GameMode.Casual && mode != GameMode.Hardcore)
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (mode == GameMode.Hardcore)
            {
                RequireValidRemainingChances(remainingChances);
                if (resumeHp != 0) throw new ArgumentOutOfRangeException(nameof(resumeHp));
            }
            else
            {
                if (resumeHp < 1 || resumeHp > CasualMaxHp) throw new ArgumentOutOfRangeException(nameof(resumeHp));
                if (remainingChances != 0) throw new ArgumentOutOfRangeException(nameof(remainingChances));
            }
        }

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
