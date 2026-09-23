using System.Collections.Generic;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;

namespace Game.Feature.UI.Application
{
    public static class UiLocalizationRequirementProjection
    {
        public static IReadOnlyList<LocalizationStringRequirement> Create()
        {
            var requirements = new List<LocalizationStringRequirement>();
            AddSettings(requirements);
            AddMainMenu(requirements);
            AddTerminalResult(requirements);
            AddSceneTransition(requirements);
            AddWorldGuide(requirements);
            AddObjectiveHud(requirements);
            AddMainMenuStatic(requirements);
            AddPause(requirements);
            return requirements.AsReadOnly();
        }

        private static void AddSettings(ICollection<LocalizationStringRequirement> requirements)
        {
            foreach (var entry in SettingsLocalizationContract.Entries)
            {
                var selectors = entry.IsSmart
                    ? SettingsSelectors(entry.Id)
                    : PlaceholderSignature.Empty;
                Add(requirements, entry.Table, entry.Key, entry.IsSmart, selectors,
                    $"{nameof(SettingsLocalizationContract)}.{entry.Id}");
            }
        }

        private static PlaceholderSignature SettingsSelectors(SettingsLocalizationEntryId id)
        {
            return id == SettingsLocalizationEntryId.DisplayPreviewConfirmFullscreenBody ||
                   id == SettingsLocalizationEntryId.DisplayPreviewConfirmWindowedBody
                ? PlaceholderSignature.FromSelectors(new[] { "0", "1", "2" })
                : PlaceholderSignature.FromSelectors(new[] { "0" });
        }

        private static void AddMainMenu(ICollection<LocalizationStringRequirement> requirements)
        {
            foreach (var entry in MainMenuLocalizationContract.Entries)
            {
                Add(requirements, entry.Table, entry.Key, entry.IsSmart,
                    entry.IsSmart ? PlaceholderSignature.FromSelectors(new[] { "0" }) : PlaceholderSignature.Empty,
                    $"{nameof(MainMenuLocalizationContract)}.{entry.Id}");
            }
        }

        private static void AddTerminalResult(ICollection<LocalizationStringRequirement> requirements)
        {
            foreach (var entry in TerminalResultLocalizationContract.Entries)
            {
                Add(requirements, entry.Table, entry.Key, entry.IsSmart, PlaceholderSignature.Empty,
                    $"{nameof(TerminalResultLocalizationContract)}.{entry.Id}");
            }
        }

        private static void AddSceneTransition(ICollection<LocalizationStringRequirement> requirements)
        {
            foreach (var entry in SceneTransitionLocalizationContract.Entries)
            {
                Add(requirements, entry.Table, entry.Key, entry.IsSmart, PlaceholderSignature.Empty,
                    $"{nameof(SceneTransitionLocalizationContract)}.{entry.Id}");
            }
        }

        private static void AddWorldGuide(ICollection<LocalizationStringRequirement> requirements)
        {
            foreach (var entry in HudWorldGuideLocalization.Entries)
            {
                Add(requirements, HudWorldGuideLocalization.Table, entry.Key, false, PlaceholderSignature.Empty,
                    $"{nameof(HudWorldGuideLocalization)}.{entry.Id}");
            }
        }

        private static void AddObjectiveHud(ICollection<LocalizationStringRequirement> requirements)
        {
            Add(requirements, ObjectiveHudLocalization.Table, ObjectiveHudLocalization.Keys.Header,
                false, PlaceholderSignature.Empty, $"{nameof(ObjectiveHudLocalization)}.HeaderDescriptor");
            var signature = PlaceholderSignature.FromSelectors(new[] { "0", "1" });
            Add(requirements, ObjectiveHudLocalization.Table, ObjectiveHudLocalization.Keys.ReachExit,
                true, signature, $"{nameof(ObjectiveHudLocalization)}.ReachExit");
            Add(requirements, ObjectiveHudLocalization.Table, ObjectiveHudLocalization.Keys.ReachZone,
                true, signature, $"{nameof(ObjectiveHudLocalization)}.ReachZone");
            Add(requirements, ObjectiveHudLocalization.Table, ObjectiveHudLocalization.Keys.ActivateButton,
                true, signature, $"{nameof(ObjectiveHudLocalization)}.ActivateButton");
            Add(requirements, ObjectiveHudLocalization.Table, ObjectiveHudLocalization.Keys.ActivateMoonButton,
                true, signature, $"{nameof(ObjectiveHudLocalization)}.ActivateMoonButton");
        }

        private static void AddMainMenuStatic(ICollection<LocalizationStringRequirement> requirements)
        {
            AddDescriptor(requirements, MainMenuStaticTextDescriptors.Start, "MainMenuStatic.Start");
            AddDescriptor(requirements, MainMenuStaticTextDescriptors.Settings, "MainMenuStatic.Settings");
            AddDescriptor(requirements, MainMenuStaticTextDescriptors.Quit, "MainMenuStatic.Quit");
        }

        private static void AddPause(ICollection<LocalizationStringRequirement> requirements)
        {
            AddDescriptor(requirements, PauseStaticTextDescriptors.Title, "Pause.Title");
            AddDescriptor(requirements, PauseStaticTextDescriptors.Resume, "Pause.Resume");
            AddDescriptor(requirements, PauseStaticTextDescriptors.Settings, "Pause.Settings");
            AddDescriptor(requirements, PauseStaticTextDescriptors.Retry, "Pause.Retry");
            AddDescriptor(requirements, PauseStaticTextDescriptors.MainMenu, "Pause.MainMenu");
        }

        private static void AddDescriptor(
            ICollection<LocalizationStringRequirement> requirements,
            LocalizedTextDescriptor descriptor,
            string owner)
        {
            Add(requirements, descriptor.Table, descriptor.Key, false, PlaceholderSignature.Empty, owner);
        }

        private static void Add(
            ICollection<LocalizationStringRequirement> requirements,
            string table,
            string key,
            bool isSmart,
            PlaceholderSignature signature,
            string owner)
        {
            requirements.Add(new LocalizationStringRequirement(table, key, isSmart, signature, owner));
        }
    }
}
