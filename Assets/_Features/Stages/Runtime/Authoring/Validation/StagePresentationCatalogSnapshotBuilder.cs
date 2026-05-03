using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Host;

namespace Game.Feature.Stages
{
    internal static class StagePresentationCatalogSnapshotBuilder
    {
        private const string EnemyCatalogType = "Enemy";
        private const string StaticCatalogType = "Static";

        public static StageAuthoringPresentationCatalogSnapshot BuildEnemyCatalogSnapshot(
            EnemyPresentationCatalog catalog)
        {
            if (catalog == null)
            {
                return CreateMissingSnapshot(EnemyCatalogType);
            }

            var entries = catalog.Entries;
            var snapshots = new List<StageAuthoringPresentationCatalogEntrySnapshot>(entries.Length);
            var ids = new List<string>(entries.Length);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var presentationId = EnemyPresentationCatalogResolver.NormalizePresentationId(entry.PresentationId);
                var duplicate = !string.IsNullOrEmpty(presentationId) && !seen.Add(presentationId);
                if (!string.IsNullOrEmpty(presentationId) && !duplicate)
                {
                    ids.Add(presentationId);
                }

                snapshots.Add(new StageAuthoringPresentationCatalogEntrySnapshot(
                    i,
                    entry.PresentationId,
                    presentationId,
                    duplicate,
                    entry.ViewPrefab == null));
            }

            return new StageAuthoringPresentationCatalogSnapshot(
                catalogAssigned: true,
                EnemyCatalogType,
                catalog.name,
                snapshots.ToArray(),
                ids.ToArray());
        }

        public static StageAuthoringPresentationCatalogSnapshot BuildStaticCatalogSnapshot(
            StaticEntityPresentationCatalog catalog)
        {
            if (catalog == null)
            {
                return CreateMissingSnapshot(StaticCatalogType);
            }

            var entries = catalog.Entries;
            var snapshots = new List<StageAuthoringPresentationCatalogEntrySnapshot>(entries.Length);
            var ids = new List<string>(entries.Length);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var presentationId = StaticEntityPresentationCatalogResolver.NormalizePresentationId(entry.PresentationId);
                var duplicate = !string.IsNullOrEmpty(presentationId) && !seen.Add(presentationId);
                if (!string.IsNullOrEmpty(presentationId) && !duplicate)
                {
                    ids.Add(presentationId);
                }

                snapshots.Add(new StageAuthoringPresentationCatalogEntrySnapshot(
                    i,
                    entry.PresentationId,
                    presentationId,
                    duplicate,
                    entry.ViewPrefab == null));
            }

            return new StageAuthoringPresentationCatalogSnapshot(
                catalogAssigned: true,
                StaticCatalogType,
                catalog.name,
                snapshots.ToArray(),
                ids.ToArray());
        }

        private static StageAuthoringPresentationCatalogSnapshot CreateMissingSnapshot(string catalogType)
        {
            return new StageAuthoringPresentationCatalogSnapshot(
                catalogAssigned: false,
                catalogType,
                string.Empty,
                Array.Empty<StageAuthoringPresentationCatalogEntrySnapshot>(),
                Array.Empty<string>());
        }
    }
}
