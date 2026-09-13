using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.ViewShared;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.SmartFormat;
using UnityEngine.Localization.SmartFormat.Core.Parsing;
using UnityEngine.Localization.Tables;

namespace Game.Feature.UI.Composition.Editor
{
    public static class LocalizationStringGovernanceUnityAdapter
    {
        public const string CompatibilityRetainedStageDisplayNameKey =
            "stage.legacy-stage-5-1.display_name";

        public static LocalizationStringGovernanceSnapshot Capture(UiLocaleCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var requirements = UiLocalizationRequirementProjection.Create().ToList();
            requirements.AddRange(CollectStageRequirements());
            var registeredCodes = LocalizationEditorSettings.GetLocales()
                .Where(locale => locale != null)
                .Select(locale => locale.Identifier.Code)
                .ToArray();
            var tables = requirements
                .Select(requirement => requirement.Table)
                .Distinct(StringComparer.Ordinal)
                .Select(table => CaptureTable(table, catalog))
                .Where(table => table != null)
                .ToArray();
            return new LocalizationStringGovernanceSnapshot(
                catalog, registeredCodes, requirements, tables);
        }

        public static LocalizationStringEntrySnapshot AnalyzeEntry(
            string key,
            string value,
            bool isSmart,
            SmartFormatter smartFormatter)
        {
            if (!isSmart)
            {
                return new LocalizationStringEntrySnapshot(
                    key, value, false, true, PlaceholderSignature.Empty, "");
            }

            if (smartFormatter == null)
            {
                return new LocalizationStringEntrySnapshot(
                    key, value, isSmart, false, PlaceholderSignature.Empty,
                    "Localization SmartFormatter is unavailable.");
            }

            if (value == null)
            {
                return new LocalizationStringEntrySnapshot(
                    key, null, isSmart, false, PlaceholderSignature.Empty,
                    "A null value cannot be parsed.");
            }

            try
            {
                var parsed = smartFormatter.Parser.ParseFormat(
                    value,
                    smartFormatter.GetNotEmptyFormatterExtensionNames());
                var selectors = new List<string>();
                CollectSelectors(parsed, selectors);
                return new LocalizationStringEntrySnapshot(
                    key, value, isSmart, true,
                    PlaceholderSignature.FromSelectors(selectors), "");
            }
            catch (Exception exception)
            {
                return new LocalizationStringEntrySnapshot(
                    key, value, isSmart, false, PlaceholderSignature.Empty,
                    exception.Message);
            }
        }

        private static IReadOnlyList<LocalizationStringRequirement> CollectStageRequirements()
        {
            var sequence = AssetDatabase.LoadAssetAtPath<CampaignStageSequenceDefinition>(
                StageContentPaths.CampaignStageSequenceAssetPath);
            if (sequence == null)
            {
                throw new InvalidOperationException(
                    $"Missing active campaign sequence at '{StageContentPaths.CampaignStageSequenceAssetPath}'.");
            }

            var entries = AssetDatabase
                .FindAssets($"t:{nameof(StageContentEntry)}", new[] { StageContentPaths.CampaignLevel01StagesRoot })
                .Select(guid => AssetDatabase.LoadAssetAtPath<StageContentEntry>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(entry => entry != null && entry.StageId.IsValid)
                .ToDictionary(entry => entry.StageId.Value, StringComparer.Ordinal);
            var requirements = new List<LocalizationStringRequirement>();
            foreach (var sequenceEntry in sequence.Entries)
            {
                if (!entries.TryGetValue(sequenceEntry.StageId.Value, out var contentEntry) ||
                    contentEntry.PresentationDefinition == null)
                {
                    throw new InvalidOperationException(
                        $"Active campaign stage '{sequenceEntry.StageId.Value}' has no presentation definition.");
                }

                requirements.Add(new LocalizationStringRequirement(
                    StageDisplayNameKeys.Table,
                    contentEntry.PresentationDefinition.DisplayNameKey,
                    false,
                    PlaceholderSignature.Empty,
                    $"StageContentEntry:{sequenceEntry.StageId.Value}->PresentationDefinition.DisplayNameKey"));
            }

            requirements.Add(new LocalizationStringRequirement(
                StageDisplayNameKeys.Table,
                CompatibilityRetainedStageDisplayNameKey,
                false,
                PlaceholderSignature.Empty,
                "Compatibility-retained legacy stage display name"));
            return requirements;
        }

        private static LocalizationStringTableSnapshot CaptureTable(string tableName, UiLocaleCatalog catalog)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(tableName);
            if (collection == null)
            {
                return null;
            }

            var smartFormatter = LocalizationSettings.StringDatabase?.SmartFormatter;
            var localeTables = new List<LocalizationLocaleTableSnapshot>();
            foreach (var locale in catalog.AuthoringKnownLocales)
            {
                var unityLocale = LocalizationEditorSettings.GetLocale(locale.CanonicalCode);
                var table = unityLocale == null
                    ? null
                    : collection.GetTable(unityLocale.Identifier) as StringTable;
                if (table == null)
                {
                    continue;
                }

                var entries = new List<LocalizationStringEntrySnapshot>();
                foreach (var sharedEntry in collection.SharedData.Entries)
                {
                    var entry = table.GetEntry(sharedEntry.Key);
                    if (entry != null)
                    {
                        entries.Add(AnalyzeEntry(
                            sharedEntry.Key,
                            entry.LocalizedValue,
                            entry.IsSmart,
                            smartFormatter));
                    }
                }

                localeTables.Add(new LocalizationLocaleTableSnapshot(locale.CanonicalCode, entries));
            }

            return new LocalizationStringTableSnapshot(
                tableName,
                collection.SharedData.Entries.Select(entry => entry.Key),
                localeTables);
        }

        private static void CollectSelectors(Format format, ICollection<string> selectors)
        {
            foreach (var item in format.Items)
            {
                if (!(item is Placeholder placeholder))
                {
                    continue;
                }

                selectors.Add(string.Concat(placeholder.Selectors.Select(selector =>
                    selector.Operator + selector.RawText)));
                if (placeholder.Format != null)
                {
                    CollectSelectors(placeholder.Format, selectors);
                }
            }
        }
    }
}
