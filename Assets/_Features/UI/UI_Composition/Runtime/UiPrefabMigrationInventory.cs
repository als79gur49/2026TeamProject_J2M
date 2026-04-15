using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.UI.Flow;

namespace Game.Feature.UI.Composition
{
    internal enum UiPrefabMigrationEntryKind
    {
        RootShell = 0,
        Hud = 1,
        Popup = 2,
        Screen = 3,
        ScreenInternal = 4,
    }

    internal readonly struct UiPrefabMigrationEntry
    {
        internal UiPrefabMigrationEntry(
            UiPrefabMigrationEntryKind kind,
            string entryId,
            string legacyBuilderPath)
        {
            Kind = kind;
            EntryId = entryId ?? string.Empty;
            LegacyBuilderPath = legacyBuilderPath ?? string.Empty;
        }

        internal UiPrefabMigrationEntryKind Kind { get; }

        internal string EntryId { get; }

        internal string LegacyBuilderPath { get; }

        internal string DocumentationToken => $"{Kind}:{EntryId} -> {LegacyBuilderPath}";
    }

    internal static class UiPrefabMigrationInventory
    {
        private static readonly UiPrefabMigrationEntry[] AllowedMixedModeEntries =
        {
            new(UiPrefabMigrationEntryKind.Screen, ScreenId.Gameplay.ToString(), "GameplayScreenRuntimeFactory.CreateGameplayScreen"),
            new(UiPrefabMigrationEntryKind.Screen, ScreenId.Help.ToString(), "GameplayScreenRuntimeFactory.CreateHelpScreen"),
            new(UiPrefabMigrationEntryKind.Screen, ScreenId.ObjectiveStatus.ToString(), "GameplayScreenRuntimeFactory.CreateObjectiveStatusScreen"),
            new(UiPrefabMigrationEntryKind.Screen, ScreenId.Inventory.ToString(), "GameplayScreenRuntimeFactory.CreateInventoryScreen"),
            new(UiPrefabMigrationEntryKind.Screen, ScreenId.Settings.ToString(), "GameplayScreenRuntimeFactory.CreateSettingsScreen"),
            new(UiPrefabMigrationEntryKind.Screen, ScreenId.StageResult.ToString(), "GameplayScreenRuntimeFactory.CreateStageResultScreen"),
            new(UiPrefabMigrationEntryKind.ScreenInternal, "InventoryScreen.Sections", "InventoryScreenView authored child sections remain runtime-built"),
        };

        internal static IReadOnlyList<UiPrefabMigrationEntry> AllowedMixedModeEntriesView => AllowedMixedModeEntries;

        internal static IReadOnlyList<string> DocumentationTokens => AllowedMixedModeEntries
            .Select(entry => entry.DocumentationToken)
            .ToArray();

        internal static bool IsRootShellMigrated => AllowedMixedModeEntries.All(entry => entry.Kind != UiPrefabMigrationEntryKind.RootShell);

        internal static bool AllowsLegacyPopupBuilder(PopupId popupId)
        {
            return popupId != PopupId.None && HasEntry(UiPrefabMigrationEntryKind.Popup, popupId.ToString());
        }

        internal static bool AllowsLegacyScreenBuilder(ScreenId screenId)
        {
            return screenId != ScreenId.None && HasEntry(UiPrefabMigrationEntryKind.Screen, screenId.ToString());
        }

        internal static bool AllowsLegacyInventoryScreenSections =>
            HasEntry(UiPrefabMigrationEntryKind.ScreenInternal, "InventoryScreen.Sections");

        internal static bool LayerHasNoMixedModeEntries(UiPrefabMigrationEntryKind kind)
        {
            return AllowedMixedModeEntries.All(entry => entry.Kind != kind);
        }

        private static bool HasEntry(UiPrefabMigrationEntryKind kind, string entryId)
        {
            return AllowedMixedModeEntries.Any(entry =>
                entry.Kind == kind &&
                string.Equals(entry.EntryId, entryId, StringComparison.Ordinal));
        }
    }
}
