using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Host;

namespace Game.Feature.Stages
{
    public sealed class StageAuthoringPresentationCatalogEntrySnapshot
    {
        public StageAuthoringPresentationCatalogEntrySnapshot(
            int entryIndex,
            string rawPresentationId,
            string presentationId,
            bool duplicatePresentationId,
            bool viewPrefabMissing,
            bool vfxProfileAssigned = false,
            string vfxProfileName = "",
            string vfxProfileFamily = "",
            EnemyPresentationVfxProfileStatusKind vfxProfileStatus =
                EnemyPresentationVfxProfileStatusKind.HostDefaultFallback,
            EnemyPresentationVfxProfileDiagnostic[] vfxProfileDiagnostics = null)
        {
            EntryIndex = entryIndex;
            RawPresentationId = rawPresentationId ?? string.Empty;
            PresentationId = presentationId ?? string.Empty;
            DuplicatePresentationId = duplicatePresentationId;
            ViewPrefabMissing = viewPrefabMissing;
            VfxProfileAssigned = vfxProfileAssigned;
            VfxProfileName = vfxProfileName ?? string.Empty;
            VfxProfileFamily = vfxProfileFamily ?? string.Empty;
            VfxProfileStatus = vfxProfileStatus;
            VfxProfileDiagnostics = vfxProfileDiagnostics ?? Array.Empty<EnemyPresentationVfxProfileDiagnostic>();
        }

        public int EntryIndex { get; }

        public string RawPresentationId { get; }

        public string PresentationId { get; }

        public bool EmptyPresentationId => string.IsNullOrEmpty(PresentationId);

        public bool DuplicatePresentationId { get; }

        public bool ViewPrefabMissing { get; }

        public bool VfxProfileAssigned { get; }

        public string VfxProfileName { get; }

        public string VfxProfileFamily { get; }

        public EnemyPresentationVfxProfileStatusKind VfxProfileStatus { get; }

        public EnemyPresentationVfxProfileDiagnostic[] VfxProfileDiagnostics { get; }
    }

    public sealed class StageAuthoringPresentationCatalogSnapshot
    {
        public StageAuthoringPresentationCatalogSnapshot(
            bool catalogAssigned,
            string catalogType,
            string catalogName,
            StageAuthoringPresentationCatalogEntrySnapshot[] entries,
            string[] presentationIds)
        {
            CatalogAssigned = catalogAssigned;
            CatalogType = catalogType ?? string.Empty;
            CatalogName = catalogName ?? string.Empty;
            Entries = entries ?? Array.Empty<StageAuthoringPresentationCatalogEntrySnapshot>();
            PresentationIds = presentationIds ?? Array.Empty<string>();
        }

        public bool CatalogAssigned { get; }

        public string CatalogType { get; }

        public string CatalogName { get; }

        public StageAuthoringPresentationCatalogEntrySnapshot[] Entries { get; }

        public string[] PresentationIds { get; }

        public int EntryCount => Entries.Length;

        public bool ContainsPresentationId(string presentationId)
        {
            if (string.IsNullOrEmpty(presentationId))
            {
                return false;
            }

            for (var i = 0; i < PresentationIds.Length; i++)
            {
                if (string.Equals(PresentationIds[i], presentationId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public readonly struct StageAuthoringPresentationBindingSnapshot
    {
        public StageAuthoringPresentationBindingSnapshot(
            int entityId,
            string presentationId,
            int bindingIndex,
            bool enemyBinding)
        {
            EntityId = entityId;
            PresentationId = presentationId ?? string.Empty;
            BindingIndex = bindingIndex;
            EnemyBinding = enemyBinding;
        }

        public int EntityId { get; }

        public string PresentationId { get; }

        public int BindingIndex { get; }

        public bool EnemyBinding { get; }
    }

    public static class StageAuthoringPresentationCatalogValidator
    {
        public static StageAuthoringPresentationCatalogSnapshot BuildEnemyCatalogSnapshot(
            EnemyPresentationCatalog catalog)
        {
            return StagePresentationCatalogSnapshotBuilder.BuildEnemyCatalogSnapshot(catalog);
        }

        public static StageAuthoringPresentationCatalogSnapshot BuildStaticCatalogSnapshot(
            StaticEntityPresentationCatalog catalog)
        {
            return StagePresentationCatalogSnapshotBuilder.BuildStaticCatalogSnapshot(catalog);
        }

        public static StageValidationIssue[] ValidateEntry(
            StageContentEntry entry,
            StageCatalogValidationOptions options)
        {
            var issues = new List<StageValidationIssue>();
            if (entry == null)
            {
                return issues.ToArray();
            }

            options ??= StageCatalogValidationOptions.Default;
            var authoring = entry.AuthoringDefinition;
            var presentation = entry.PresentationDefinition;
            var gameplay = entry.GameplayDefinition;
            var stageId = entry.StageId.IsValid ? entry.StageId.Value : string.Empty;
            var strict = authoring != null && authoring.EnforceGeneratedSync;

            if (presentation == null)
            {
                if (authoring != null && StagePresentationPlacementReferenceValidator.HasPresentationPlacements(authoring))
                {
                    issues.Add(StagePresentationCatalogIssueFactory.Create(
                        StagePresentationValidationSeverityPolicy.ResolveSoftSeverity(strict),
                        "PresentationCatalog.GeneratedPresentationDefinitionMissing",
                        $"StageAuthoringDefinition '{authoring.name}' has presentation-authored placements but no generated presentation definition is assigned.",
                        authoring,
                        StagePresentationCatalogIssueFactory.GetAssetPath(authoring, options),
                        options.Timing,
                        stageId,
                        authoring.name,
                        string.Empty));
                }

                return issues.ToArray();
            }

            var enemySnapshot = BuildEnemyCatalogSnapshot(presentation.EnemyPresentationCatalog);
            var staticSnapshot = BuildStaticCatalogSnapshot(presentation.StaticEntityPresentationCatalog);

            StagePresentationCatalogEntryValidator.ValidateCatalogEntries(
                presentation.EnemyPresentationCatalog,
                enemySnapshot,
                options,
                stageId,
                presentation.name,
                issues);
            StagePresentationCatalogEntryValidator.ValidateCatalogEntries(
                presentation.StaticEntityPresentationCatalog,
                staticSnapshot,
                options,
                stageId,
                presentation.name,
                issues);

            var requiredCatalogs = StagePresentationPlacementReferenceValidator.ResolveRequiredCatalogs(
                authoring,
                presentation);
            StagePresentationPlacementReferenceValidator.ValidateCatalogAvailability(
                presentation,
                enemySnapshot,
                staticSnapshot,
                requiredCatalogs,
                strict,
                options,
                stageId,
                authoring != null ? authoring.name : string.Empty,
                presentation.name,
                issues);

            if (authoring != null)
            {
                StagePresentationPlacementReferenceValidator.ValidateAuthoringPlacements(
                    authoring,
                    enemySnapshot,
                    staticSnapshot,
                    strict,
                    options,
                    stageId,
                    presentation.name,
                    issues);
            }

            if (gameplay != null)
            {
                StagePresentationBindingIntegrityValidator.ValidateGeneratedBindings(
                    gameplay,
                    presentation,
                    enemySnapshot,
                    staticSnapshot,
                    strict,
                    options,
                    stageId,
                    authoring != null ? authoring.name : string.Empty,
                    issues);
            }

            return issues.ToArray();
        }
    }
}
