using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.ViewShared;

namespace Game.Feature.UI.Application
{
    public static class ObjectiveHudLocalization
    {
        public const string Table = "UI";

        public static class Keys
        {
            public const string Header = "ui.hud.objectives.title";
            public const string ReachExit = "ui.hud.objective.reach_exit";
            public const string ReachZone = "ui.hud.objective.reach_zone";
            public const string ActivateButton = "ui.hud.objective.activate_button";
            public const string ActivateMoonButton = "ui.hud.objective.activate_moon_button";
        }

        public static LocalizedTextDescriptor HeaderDescriptor =>
            new LocalizedTextDescriptor(
                Table,
                Keys.Header,
                LocalizedTextRole.Title,
                LocalizedTextWeight.Bold);

        public static bool TryCreateConditionDescriptor(
            GameplayObjectivePresentationKind presentationKind,
            int completedCount,
            int requiredCount,
            out LocalizedTextDescriptor descriptor)
        {
            var key = ResolveKey(presentationKind);
            if (string.IsNullOrEmpty(key))
            {
                descriptor = default;
                return false;
            }

            descriptor = new LocalizedTextDescriptor(
                Table,
                key,
                LocalizedTextRole.Body,
                LocalizedTextWeight.Regular,
                new object[]
                {
                    Math.Max(0, completedCount),
                    Math.Max(0, requiredCount),
                });
            return true;
        }

        private static string ResolveKey(GameplayObjectivePresentationKind presentationKind)
        {
            switch (presentationKind)
            {
                case GameplayObjectivePresentationKind.ReachExit:
                    return Keys.ReachExit;

                case GameplayObjectivePresentationKind.ReachZone:
                    return Keys.ReachZone;

                case GameplayObjectivePresentationKind.ActivateButton:
                    return Keys.ActivateButton;

                case GameplayObjectivePresentationKind.ActivateMoonButton:
                    return Keys.ActivateMoonButton;

                case GameplayObjectivePresentationKind.None:
                default:
                    return string.Empty;
            }
        }
    }

    public enum HudWorldGuideLocalizationEntryId
    {
        Movement = 0,
        Push = 1,
        Flip = 2,
    }

    public enum WorldGuideActionLocalizationKind
    {
        None = 0,
        Movement = 1,
        Push = 2,
        Flip = 3,
    }

    public readonly struct HudWorldGuideLocalizationContractEntry
    {
        public HudWorldGuideLocalizationContractEntry(
            HudWorldGuideLocalizationEntryId id,
            string key,
            string english,
            string korean,
            LocalizedTextRole role,
            LocalizedTextWeight weight)
        {
            Id = id;
            Key = key ?? string.Empty;
            English = english ?? string.Empty;
            Korean = korean ?? string.Empty;
            Role = role;
            Weight = weight;
        }

        public HudWorldGuideLocalizationEntryId Id { get; }
        public string Key { get; }
        public string English { get; }
        public string Korean { get; }
        public LocalizedTextRole Role { get; }
        public LocalizedTextWeight Weight { get; }
        public LocalizedTextDescriptor Descriptor =>
            new LocalizedTextDescriptor(HudWorldGuideLocalization.Table, Key, Role, Weight);
    }

    public static class HudWorldGuideLocalization
    {
        public const string Table = "UI";

        public static class Keys
        {
            public const string Movement = HudWorldGuideLocalizationKeys.Movement;
            public const string Push = HudWorldGuideLocalizationKeys.Push;
            public const string Flip = HudWorldGuideLocalizationKeys.Flip;
        }

        private static readonly IReadOnlyList<HudWorldGuideLocalizationContractEntry> ContractEntries =
            Array.AsReadOnly(new[]
            {
                Entry(HudWorldGuideLocalizationEntryId.Movement, Keys.Movement, "Move", "이동",
                    LocalizedTextRole.Body, LocalizedTextWeight.Regular),
                Entry(HudWorldGuideLocalizationEntryId.Push, Keys.Push, "Push", "밀기",
                    LocalizedTextRole.Body, LocalizedTextWeight.Regular),
                Entry(HudWorldGuideLocalizationEntryId.Flip, Keys.Flip, "Flip", "뒤집기",
                    LocalizedTextRole.Body, LocalizedTextWeight.Regular),
            });

        public static IReadOnlyList<HudWorldGuideLocalizationContractEntry> Entries => ContractEntries;

        public static bool TryCreateWorldGuideDescriptor(
            WorldGuideActionLocalizationKind kind,
            out LocalizedTextDescriptor descriptor)
        {
            switch (kind)
            {
                case WorldGuideActionLocalizationKind.Movement:
                    descriptor = ContractEntries[(int)HudWorldGuideLocalizationEntryId.Movement].Descriptor;
                    return true;
                case WorldGuideActionLocalizationKind.Push:
                    descriptor = ContractEntries[(int)HudWorldGuideLocalizationEntryId.Push].Descriptor;
                    return true;
                case WorldGuideActionLocalizationKind.Flip:
                    descriptor = ContractEntries[(int)HudWorldGuideLocalizationEntryId.Flip].Descriptor;
                    return true;
                case WorldGuideActionLocalizationKind.None:
                default:
                    descriptor = default;
                    return false;
            }
        }

        private static HudWorldGuideLocalizationContractEntry Entry(
            HudWorldGuideLocalizationEntryId id,
            string key,
            string english,
            string korean,
            LocalizedTextRole role,
            LocalizedTextWeight weight)
        {
            return new HudWorldGuideLocalizationContractEntry(
                id, key, english, korean, role, weight);
        }
    }
}
