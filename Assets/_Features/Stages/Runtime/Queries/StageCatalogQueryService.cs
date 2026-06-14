using System;
using System.Collections.Generic;

namespace Game.Feature.Stages
{
    public readonly struct StageLaunchCatalogItem
    {
        public StageLaunchCatalogItem(
            StageId stageId,
            string displayName,
            string worldId,
            string chapterId,
            int sortOrder,
            bool isInitiallyAvailable)
        {
            StageId = stageId;
            DisplayName = displayName ?? string.Empty;
            WorldId = worldId ?? string.Empty;
            ChapterId = chapterId ?? string.Empty;
            SortOrder = sortOrder;
            IsInitiallyAvailable = isInitiallyAvailable;
        }

        public StageId StageId { get; }

        public string DisplayName { get; }

        public string WorldId { get; }

        public string ChapterId { get; }

        public int SortOrder { get; }

        public bool IsInitiallyAvailable { get; }
    }

    public sealed class StageCatalogQueryService
    {
        private readonly StageCatalogResolver resolver;
        private readonly IReadOnlyList<StageContentEntry> entries;

        public StageCatalogQueryService(IStageCatalogProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            entries = provider.LoadEntries() ?? Array.Empty<StageContentEntry>();
            resolver = new StageCatalogResolver(provider);
        }

        public bool TryGetLaunchSummary(StageId stageId, out StageLaunchCatalogItem summary)
        {
            if (resolver.TryResolve(stageId, out var entry))
            {
                summary = BuildItem(entry);
                return true;
            }

            summary = default;
            return false;
        }

        public IReadOnlyList<StageLaunchCatalogItem> EnumerateLaunchCatalogItems()
        {
            var results = new List<StageLaunchCatalogItem>(entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i] == null)
                {
                    continue;
                }

                results.Add(BuildItem(entries[i]));
            }

            results.Sort(CompareItems);
            return results;
        }

        private static StageLaunchCatalogItem BuildItem(StageContentEntry entry)
        {
            var presentation = entry != null ? entry.PresentationDefinition : null;

            return new StageLaunchCatalogItem(
                entry != null ? entry.StageId : StageId.None,
                presentation != null && !string.IsNullOrWhiteSpace(presentation.DisplayName)
                    ? presentation.DisplayName
                    : entry != null ? entry.StageId.Value : string.Empty,
                entry != null ? entry.CatalogWorldId : string.Empty,
                entry != null ? entry.CatalogChapterId : string.Empty,
                entry != null ? entry.CatalogSortOrder : 0,
                entry == null || entry.IsInitiallyAvailable);
        }

        private static int CompareItems(StageLaunchCatalogItem left, StageLaunchCatalogItem right)
        {
            var worldComparison = string.Compare(left.WorldId, right.WorldId, StringComparison.Ordinal);
            if (worldComparison != 0)
            {
                return worldComparison;
            }

            var chapterComparison = string.Compare(left.ChapterId, right.ChapterId, StringComparison.Ordinal);
            if (chapterComparison != 0)
            {
                return chapterComparison;
            }

            var sortOrderComparison = left.SortOrder.CompareTo(right.SortOrder);
            if (sortOrderComparison != 0)
            {
                return sortOrderComparison;
            }

            return string.Compare(left.StageId.Value, right.StageId.Value, StringComparison.Ordinal);
        }
    }
}
