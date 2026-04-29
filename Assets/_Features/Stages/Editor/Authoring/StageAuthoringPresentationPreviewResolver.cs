using System.Collections.Generic;
using Game.Feature.Gameplay.Host;
using UnityEditor;

namespace Game.Feature.Stages.Editor
{
    internal static class StageAuthoringPresentationPreviewResolver
    {
        public static StageAuthoringPresentationPreviewModel Resolve(
            StageAuthoringDefinition source,
            StagePlacedEntityAuthoring placement,
            StagePresentationDefinition generatedPresentationDefinition)
        {
            if (placement == null || placement.Kind == StageAuthoringEntityKind.Player)
            {
                return StageAuthoringPresentationPreviewModel.Empty;
            }

            if (generatedPresentationDefinition == null)
            {
                return Create(
                    placement,
                    catalogAsset: null,
                    catalogHasEntries: false,
                    entryFound: false,
                    viewPrefab: null,
                    "Generated presentation definition is not assigned.",
                    MessageType.Warning,
                    "Generated presentation definition is not assigned.");
            }

            return placement.Kind == StageAuthoringEntityKind.Enemy
                ? ResolveEnemy(placement, generatedPresentationDefinition.EnemyPresentationCatalog)
                : ResolveStatic(placement, generatedPresentationDefinition.StaticEntityPresentationCatalog);
        }

        private static StageAuthoringPresentationPreviewModel ResolveEnemy(
            StagePlacedEntityAuthoring placement,
            EnemyPresentationCatalog catalog)
        {
            var presentationId = EnemyPresentationCatalogResolver.NormalizePresentationId(placement.PresentationId);
            if (catalog == null)
            {
                return Create(
                    placement,
                    null,
                    catalogHasEntries: false,
                    entryFound: false,
                    viewPrefab: null,
                    "Enemy presentation catalog is not assigned.",
                    MessageType.Warning,
                    "Enemy presentation catalog is not assigned.");
            }

            var snapshot = StageAuthoringPresentationCatalogValidator.BuildEnemyCatalogSnapshot(catalog);
            var viewPrefab = FindEnemyViewPrefab(catalog, presentationId);
            return CreateResolved(placement, catalog, snapshot.EntryCount > 0, presentationId, viewPrefab, "enemy presentation catalog");
        }

        private static StageAuthoringPresentationPreviewModel ResolveStatic(
            StagePlacedEntityAuthoring placement,
            StaticEntityPresentationCatalog catalog)
        {
            var presentationId = StaticEntityPresentationCatalogResolver.NormalizePresentationId(placement.PresentationId);
            if (catalog == null)
            {
                return Create(
                    placement,
                    null,
                    catalogHasEntries: false,
                    entryFound: false,
                    viewPrefab: null,
                    "Static presentation catalog is not assigned.",
                    MessageType.Warning,
                    "Static presentation catalog is not assigned.");
            }

            var snapshot = StageAuthoringPresentationCatalogValidator.BuildStaticCatalogSnapshot(catalog);
            var viewPrefab = FindStaticViewPrefab(catalog, presentationId);
            return CreateResolved(placement, catalog, snapshot.EntryCount > 0, presentationId, viewPrefab, "static presentation catalog");
        }

        private static StageAuthoringPresentationPreviewModel CreateResolved(
            StagePlacedEntityAuthoring placement,
            UnityEngine.Object catalog,
            bool catalogHasEntries,
            string presentationId,
            GameplayEntityView viewPrefab,
            string catalogDescription)
        {
            var warnings = new List<string>();
            var entryFound = ContainsEntry(placement.Kind, catalog, presentationId, out var entryViewMissing);
            if (viewPrefab != null)
            {
                entryFound = true;
            }
            var statusType = MessageType.Info;
            var status = "Resolved.";

            if (!catalogHasEntries)
            {
                status = $"{ToPresentationKindLabel(placement.Kind)} presentation catalog has no entries.";
                statusType = MessageType.Warning;
                warnings.Add(status);
            }
            else if (string.IsNullOrEmpty(presentationId))
            {
                status = $"{placement.Kind} placement has no PresentationId.";
                statusType = MessageType.Warning;
                warnings.Add(status);
            }
            else if (!entryFound)
            {
                status = $"Selected PresentationId '{presentationId}' was not found in the {catalogDescription}.";
                statusType = MessageType.Warning;
                warnings.Add(status);
            }
            else if (entryViewMissing)
            {
                status = $"PresentationId '{presentationId}' requires a non-null ViewPrefab.";
                statusType = MessageType.Error;
                warnings.Add(status);
            }

            return Create(
                placement,
                catalog,
                catalogHasEntries,
                entryFound,
                viewPrefab,
                status,
                statusType,
                warnings.ToArray(),
                presentationId);
        }

        private static StageAuthoringPresentationPreviewModel Create(
            StagePlacedEntityAuthoring placement,
            UnityEngine.Object catalogAsset,
            bool catalogHasEntries,
            bool entryFound,
            GameplayEntityView viewPrefab,
            string statusLabel,
            MessageType statusMessageType,
            params string[] warnings)
        {
            var presentationId = NormalizePresentationId(placement.Kind, placement.PresentationId);
            return Create(
                placement,
                catalogAsset,
                catalogHasEntries,
                entryFound,
                viewPrefab,
                statusLabel,
                statusMessageType,
                warnings,
                presentationId);
        }

        private static StageAuthoringPresentationPreviewModel Create(
            StagePlacedEntityAuthoring placement,
            UnityEngine.Object catalogAsset,
            bool catalogHasEntries,
            bool entryFound,
            GameplayEntityView viewPrefab,
            string statusLabel,
            MessageType statusMessageType,
            string[] warnings,
            string presentationId)
        {
            return new StageAuthoringPresentationPreviewModel(
                requiresPresentation: true,
                placement.Kind,
                ToPresentationKindLabel(placement.Kind),
                presentationId,
                catalogAssigned: catalogAsset != null,
                catalogHasEntries,
                entryFound,
                catalogAsset,
                viewPrefab,
                catalogAsset != null ? catalogAsset.name : string.Empty,
                viewPrefab != null ? viewPrefab.name : string.Empty,
                statusLabel,
                statusMessageType,
                warnings);
        }

        private static bool ContainsEntry(
            StageAuthoringEntityKind kind,
            UnityEngine.Object catalog,
            string presentationId,
            out bool viewMissing)
        {
            viewMissing = false;
            if (string.IsNullOrEmpty(presentationId))
            {
                return false;
            }

            if (kind == StageAuthoringEntityKind.Enemy && catalog is EnemyPresentationCatalog enemyCatalog)
            {
                var entries = enemyCatalog.Entries;
                for (var i = 0; i < entries.Length; i++)
                {
                    if (EnemyPresentationCatalogResolver.NormalizePresentationId(entries[i].PresentationId) != presentationId)
                    {
                        continue;
                    }

                    viewMissing = entries[i].ViewPrefab == null;
                    return true;
                }
            }
            else if (catalog is StaticEntityPresentationCatalog staticCatalog)
            {
                var entries = staticCatalog.Entries;
                for (var i = 0; i < entries.Length; i++)
                {
                    if (StaticEntityPresentationCatalogResolver.NormalizePresentationId(entries[i].PresentationId) != presentationId)
                    {
                        continue;
                    }

                    viewMissing = entries[i].ViewPrefab == null;
                    return true;
                }
            }

            return false;
        }

        private static GameplayEntityView FindEnemyViewPrefab(EnemyPresentationCatalog catalog, string presentationId)
        {
            if (catalog == null || string.IsNullOrEmpty(presentationId))
            {
                return null;
            }

            var entries = catalog.Entries;
            for (var i = 0; i < entries.Length; i++)
            {
                if (EnemyPresentationCatalogResolver.NormalizePresentationId(entries[i].PresentationId) == presentationId)
                {
                    return entries[i].ViewPrefab;
                }
            }

            return null;
        }

        private static GameplayEntityView FindStaticViewPrefab(StaticEntityPresentationCatalog catalog, string presentationId)
        {
            if (catalog == null || string.IsNullOrEmpty(presentationId))
            {
                return null;
            }

            var entries = catalog.Entries;
            for (var i = 0; i < entries.Length; i++)
            {
                if (StaticEntityPresentationCatalogResolver.NormalizePresentationId(entries[i].PresentationId) == presentationId)
                {
                    return entries[i].ViewPrefab;
                }
            }

            return null;
        }

        private static string NormalizePresentationId(StageAuthoringEntityKind kind, string presentationId)
        {
            return kind == StageAuthoringEntityKind.Enemy
                ? EnemyPresentationCatalogResolver.NormalizePresentationId(presentationId)
                : StaticEntityPresentationCatalogResolver.NormalizePresentationId(presentationId);
        }

        private static string ToPresentationKindLabel(StageAuthoringEntityKind kind)
        {
            return kind == StageAuthoringEntityKind.Enemy ? "Enemy" : "Static";
        }
    }
}
