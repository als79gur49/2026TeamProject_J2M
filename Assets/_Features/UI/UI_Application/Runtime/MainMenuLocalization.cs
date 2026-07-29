using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Feature.UI.Popups;
using Game.Feature.UI.ViewShared;

namespace Game.Feature.UI.Application
{
    public enum MainMenuConfirmationKind
    {
        DeleteSlot,
        RestartSlot,
        OverwriteSlot,
        QuitGame,
    }

    public static class MainMenuLocalization
    {
        private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");
        private static readonly CultureInfo KoreanCulture = CultureInfo.GetCultureInfo("ko-KR");

        public static LocalizedTextDescriptor Descriptor(
            MainMenuLocalizationEntryId id,
            params object[] arguments)
        {
            var entry = MainMenuLocalizationContract.Get(id);
            return new LocalizedTextDescriptor(
                entry.Table,
                entry.Key,
                entry.Role,
                entry.Weight,
                arguments);
        }

        public static string Resolve(
            ILocalizedTextResolver resolver,
            MainMenuLocalizationEntryId id,
            params object[] arguments)
        {
            if (resolver == null)
            {
                throw new ArgumentNullException(nameof(resolver));
            }

            return resolver.Resolve(Descriptor(id, arguments));
        }

        public static string FormatPlayedDate(string lastPlayedAt, string localeCode)
        {
            if (string.IsNullOrWhiteSpace(lastPlayedAt))
            {
                return string.Empty;
            }

            if (!DateTimeOffset.TryParse(
                    lastPlayedAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var playedAt))
            {
                return lastPlayedAt;
            }

            var localDate = playedAt.LocalDateTime;
            return string.Equals(localeCode, "ko-KR", StringComparison.OrdinalIgnoreCase)
                ? localDate.ToString("yyyy. M. d.", KoreanCulture)
                : localDate.ToString("MMM d, yyyy", EnglishCulture);
        }

        public static ConfirmPopupPayload CreateConfirmationPayload(
            MainMenuConfirmationKind kind,
            int? slotNumber = null)
        {
            var cancel = new LocalizedTextDescriptor(
                MainMenuLocalizationContract.Table,
                MainMenuLocalizationContract.Keys.Cancel,
                LocalizedTextRole.Button,
                LocalizedTextWeight.Regular);

            switch (kind)
            {
                case MainMenuConfirmationKind.DeleteSlot:
                    return SlotConfirmation(
                        slotNumber,
                        MainMenuLocalizationEntryId.DeleteTitle,
                        MainMenuLocalizationEntryId.DeleteBody,
                        MainMenuLocalizationEntryId.DeleteWarning,
                        MainMenuLocalizationEntryId.DeleteConfirm,
                        cancel,
                        isDestructive: true);

                case MainMenuConfirmationKind.RestartSlot:
                    return SlotConfirmation(
                        slotNumber,
                        MainMenuLocalizationEntryId.RestartTitle,
                        MainMenuLocalizationEntryId.RestartBody,
                        MainMenuLocalizationEntryId.RestartWarning,
                        MainMenuLocalizationEntryId.RestartConfirm,
                        cancel,
                        isDestructive: true);

                case MainMenuConfirmationKind.OverwriteSlot:
                    return SlotConfirmation(
                        slotNumber,
                        MainMenuLocalizationEntryId.OverwriteTitle,
                        MainMenuLocalizationEntryId.OverwriteBody,
                        MainMenuLocalizationEntryId.OverwriteWarning,
                        MainMenuLocalizationEntryId.OverwriteConfirm,
                        cancel,
                        isDestructive: true);

                case MainMenuConfirmationKind.QuitGame:
                    return new ConfirmPopupPayload(
                        Descriptor(MainMenuLocalizationEntryId.QuitTitle),
                        Descriptor(MainMenuLocalizationEntryId.QuitBody),
                        Descriptor(MainMenuLocalizationEntryId.QuitWarning),
                        Descriptor(MainMenuLocalizationEntryId.QuitConfirm),
                        cancel,
                        isConfirmDestructive: true);

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private static ConfirmPopupPayload SlotConfirmation(
            int? slotNumber,
            MainMenuLocalizationEntryId title,
            MainMenuLocalizationEntryId body,
            MainMenuLocalizationEntryId warning,
            MainMenuLocalizationEntryId confirm,
            LocalizedTextDescriptor cancel,
            bool isDestructive)
        {
            if (!slotNumber.HasValue)
            {
                throw new ArgumentException("Slot confirmation requires a slot number.", nameof(slotNumber));
            }

            return new ConfirmPopupPayload(
                Descriptor(title),
                Descriptor(body, slotNumber.Value),
                Descriptor(warning),
                Descriptor(confirm),
                cancel,
                isDestructive);
        }
    }
}
