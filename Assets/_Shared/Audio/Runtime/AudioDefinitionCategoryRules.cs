using System;

namespace Game.Shared.Audio
{
    public static class AudioDefinitionCategoryRules
    {
        public static bool TryGetReservedCategoryMessage(
            AudioCategory category,
            string ownerDescription,
            out string message)
        {
            if (category == AudioCategory.Master)
            {
                message = $"{ownerDescription} cannot use AudioCategory.Master because Master is reserved for mixer-only control.";
                return true;
            }

            message = string.Empty;
            return false;
        }

        public static void ThrowIfDefinitionCategoryReserved(AudioCategory category, string ownerDescription)
        {
            if (TryGetReservedCategoryMessage(category, ownerDescription, out var message))
            {
                throw new InvalidOperationException(message);
            }
        }

        public static AudioChannel ToLeafChannel(AudioCategory category, string ownerDescription)
        {
            ThrowIfDefinitionCategoryReserved(category, ownerDescription);

            switch (category)
            {
                case AudioCategory.Bgm:
                    return AudioChannel.Bgm;
                case AudioCategory.Sfx:
                    return AudioChannel.Sfx;
                case AudioCategory.Ui:
                    return AudioChannel.Ui;
                case AudioCategory.Voice:
                    return AudioChannel.Voice;
                case AudioCategory.Ambience:
                    return AudioChannel.Ambience;
                default:
                    throw new ArgumentOutOfRangeException(nameof(category), category, null);
            }
        }
    }
}
