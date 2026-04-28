using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Host;

namespace Game.Feature.Stages.Editor
{
    public sealed class StageAuthoringPresentationOptionModel
    {
        public const string NoneLabel = "(None)";

        private StageAuthoringPresentationOptionModel(
            string[] rawIds,
            string[] popupLabels,
            int selectedPopupIndex,
            bool catalogAssigned,
            bool catalogHasEntries,
            bool selectedIdMissing,
            string[] warningMessages)
        {
            RawIds = rawIds ?? Array.Empty<string>();
            PopupLabels = popupLabels ?? Array.Empty<string>();
            SelectedPopupIndex = selectedPopupIndex;
            CatalogAssigned = catalogAssigned;
            CatalogHasEntries = catalogHasEntries;
            SelectedIdMissing = selectedIdMissing;
            WarningMessages = warningMessages ?? Array.Empty<string>();
        }

        public string[] RawIds { get; }

        public string[] PopupLabels { get; }

        public int SelectedPopupIndex { get; }

        public bool CatalogAssigned { get; }

        public bool CatalogHasEntries { get; }

        public bool SelectedIdMissing { get; }

        public string[] WarningMessages { get; }

        public string WarningMessage => string.Join("\n", WarningMessages);

        public static StageAuthoringPresentationOptionModel Build(
            StageAuthoringEntityKind kind,
            string currentPresentationId,
            StagePresentationDefinition presentationDefinition)
        {
            if (kind == StageAuthoringEntityKind.Player)
            {
                return new StageAuthoringPresentationOptionModel(
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    selectedPopupIndex: 0,
                    catalogAssigned: false,
                    catalogHasEntries: false,
                    selectedIdMissing: false,
                    Array.Empty<string>());
            }

            var warningMessages = new List<string>();
            var rawIds = Array.Empty<string>();
            var catalogAssigned = false;
            var catalogHasEntries = false;

            if (presentationDefinition == null)
            {
                warningMessages.Add("Generated presentation definition is not assigned.");
            }
            else if (kind == StageAuthoringEntityKind.Enemy)
            {
                var catalog = presentationDefinition.EnemyPresentationCatalog;
                catalogAssigned = catalog != null;
                if (catalog == null)
                {
                    warningMessages.Add("Enemy presentation catalog is not assigned.");
                }
                else
                {
                    rawIds = GetEnemyPresentationIdsRaw(catalog);
                    catalogHasEntries = rawIds.Length > 0;
                    if (!catalogHasEntries)
                    {
                        warningMessages.Add("Enemy presentation catalog has no entries.");
                    }
                }
            }
            else
            {
                var catalog = presentationDefinition.StaticEntityPresentationCatalog;
                catalogAssigned = catalog != null;
                if (catalog == null)
                {
                    warningMessages.Add("Static presentation catalog is not assigned.");
                }
                else
                {
                    rawIds = GetStaticPresentationIdsRaw(catalog);
                    catalogHasEntries = rawIds.Length > 0;
                    if (!catalogHasEntries)
                    {
                        warningMessages.Add("Static presentation catalog has no entries.");
                    }
                }
            }

            var normalizedCurrent = NormalizePresentationId(kind, currentPresentationId);
            var selectedRawIndex = IndexOf(rawIds, normalizedCurrent);
            var selectedIdMissing = !string.IsNullOrEmpty(normalizedCurrent) && selectedRawIndex < 0;
            if (selectedIdMissing)
            {
                var catalogDescription = kind == StageAuthoringEntityKind.Enemy
                    ? "enemy presentation catalog"
                    : "static presentation catalog";
                warningMessages.Add(
                    $"Selected PresentationId '{normalizedCurrent}' was not found in the {catalogDescription}.");
            }

            var popupLabels = BuildPopupLabels(rawIds);
            var selectedPopupIndex = selectedRawIndex >= 0 ? selectedRawIndex + 1 : 0;
            return new StageAuthoringPresentationOptionModel(
                rawIds,
                popupLabels,
                selectedPopupIndex,
                catalogAssigned,
                catalogHasEntries,
                selectedIdMissing,
                warningMessages.ToArray());
        }

        public string ResolvePresentationId(int popupIndex)
        {
            var rawIndex = popupIndex - 1;
            return rawIndex >= 0 && rawIndex < RawIds.Length
                ? RawIds[rawIndex]
                : string.Empty;
        }

        private static string[] BuildPopupLabels(IReadOnlyList<string> rawIds)
        {
            var labels = new string[(rawIds?.Count ?? 0) + 1];
            labels[0] = NoneLabel;
            if (rawIds == null)
            {
                return labels;
            }

            for (var i = 0; i < rawIds.Count; i++)
            {
                labels[i + 1] = rawIds[i];
            }

            return labels;
        }

        private static string[] GetEnemyPresentationIdsRaw(EnemyPresentationCatalog catalog)
        {
            var entries = catalog.Entries;
            var ids = new List<string>(entries.Length);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Length; i++)
            {
                var id = EnemyPresentationCatalogResolver.NormalizePresentationId(entries[i].PresentationId);
                if (!string.IsNullOrEmpty(id) && seen.Add(id))
                {
                    ids.Add(id);
                }
            }

            return ids.ToArray();
        }

        private static string[] GetStaticPresentationIdsRaw(StaticEntityPresentationCatalog catalog)
        {
            var entries = catalog.Entries;
            var ids = new List<string>(entries.Length);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Length; i++)
            {
                var id = StaticEntityPresentationCatalogResolver.NormalizePresentationId(entries[i].PresentationId);
                if (!string.IsNullOrEmpty(id) && seen.Add(id))
                {
                    ids.Add(id);
                }
            }

            return ids.ToArray();
        }

        private static string NormalizePresentationId(StageAuthoringEntityKind kind, string presentationId)
        {
            return kind == StageAuthoringEntityKind.Enemy
                ? EnemyPresentationCatalogResolver.NormalizePresentationId(presentationId)
                : StaticEntityPresentationCatalogResolver.NormalizePresentationId(presentationId);
        }

        private static int IndexOf(IReadOnlyList<string> values, string value)
        {
            if (string.IsNullOrEmpty(value) || values == null)
            {
                return -1;
            }

            for (var i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], value, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
