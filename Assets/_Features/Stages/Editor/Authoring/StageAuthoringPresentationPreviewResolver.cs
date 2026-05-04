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
            if (placement == null || !StageAuthoringKindRegistry.RequiresPresentation(placement.Kind))
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

            var lane = StageAuthoringKindRegistry.GetPresentationLane(placement.Kind);
            return lane == StageAuthoringPresentationLane.Enemy
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
            var vfxProfileAsset = ResolveEnemyVfxProfileAsset(
                catalog,
                presentationId,
                out var vfxProfileStatusLabel,
                out var vfxProfileStatusType);
            return CreateResolved(
                placement,
                catalog,
                snapshot.EntryCount > 0,
                presentationId,
                viewPrefab,
                "enemy presentation catalog",
                vfxProfileAsset,
                vfxProfileStatusLabel,
                vfxProfileStatusType);
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
            string catalogDescription,
            UnityEngine.Object vfxProfileAsset = null,
            string vfxProfileStatusLabel = "",
            MessageType vfxProfileStatusType = MessageType.Info)
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
                presentationId,
                vfxProfileAsset,
                vfxProfileStatusLabel,
                vfxProfileStatusType);
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
            string presentationId,
            UnityEngine.Object vfxProfileAsset = null,
            string vfxProfileStatusLabel = "",
            MessageType vfxProfileStatusType = MessageType.Info)
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
                warnings,
                vfxProfileAsset,
                vfxProfileStatusLabel,
                vfxProfileStatusType);
        }

        private static UnityEngine.Object ResolveEnemyVfxProfileAsset(
            EnemyPresentationCatalog catalog,
            string presentationId,
            out string statusLabel,
            out MessageType statusType)
        {
            statusLabel = string.Empty;
            statusType = MessageType.Info;
            if (catalog == null || string.IsNullOrEmpty(presentationId))
            {
                return null;
            }

            var entries = catalog.Entries;
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (EnemyPresentationCatalogResolver.NormalizePresentationId(entry.PresentationId) != presentationId)
                {
                    continue;
                }

                var status = EnemyPresentationVfxProfileStatusResolver.Resolve(entry);
                statusLabel = EnemyPresentationVfxProfileStatusResolver.ToPreviewLabel(status);
                statusType =
                    status.Kind == EnemyPresentationVfxProfileStatusKind.Invalid ||
                    status.Kind == EnemyPresentationVfxProfileStatusKind.WrongFamily
                        ? MessageType.Error
                        : MessageType.Info;
                return status.ProfileAsset;
            }

            return null;
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

            var lane = StageAuthoringKindRegistry.GetPresentationLane(kind);
            if (lane == StageAuthoringPresentationLane.Enemy && catalog is EnemyPresentationCatalog enemyCatalog)
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
            else if (lane == StageAuthoringPresentationLane.Static && catalog is StaticEntityPresentationCatalog staticCatalog)
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
            return StageAuthoringKindRegistry.GetPresentationLane(kind) == StageAuthoringPresentationLane.Enemy
                ? EnemyPresentationCatalogResolver.NormalizePresentationId(presentationId)
                : StaticEntityPresentationCatalogResolver.NormalizePresentationId(presentationId);
        }

        private static string ToPresentationKindLabel(StageAuthoringEntityKind kind)
        {
            return StageAuthoringKindRegistry.GetPresentationLane(kind) == StageAuthoringPresentationLane.Enemy
                ? "Enemy"
                : "Static";
        }
    }
}
