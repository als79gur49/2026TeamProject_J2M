using System.Collections.Generic;

namespace Game.Feature.Stages
{
    internal static class StagePresentationCatalogEntryValidator
    {
        public static void ValidateCatalogEntries(
            UnityEngine.Object catalog,
            StageAuthoringPresentationCatalogSnapshot snapshot,
            StageCatalogValidationOptions options,
            string stageId,
            string outputAssetName,
            ICollection<StageValidationIssue> issues)
        {
            if (!snapshot.CatalogAssigned)
            {
                return;
            }

            var catalogPath = StagePresentationCatalogIssueFactory.GetAssetPath(catalog, options);
            for (var i = 0; i < snapshot.Entries.Length; i++)
            {
                var entry = snapshot.Entries[i];
                var entryField = $"{snapshot.CatalogType}Catalog.Entries[{entry.EntryIndex}]";
                if (entry.EmptyPresentationId)
                {
                    issues.Add(StagePresentationCatalogIssueFactory.Create(
                        StagePresentationValidationSeverityPolicy.AlwaysError,
                        "PresentationCatalog.EmptyPresentationId",
                        $"{snapshot.CatalogType} presentation catalog '{snapshot.CatalogName}' entry[{entry.EntryIndex}] must declare a non-empty PresentationId.",
                        catalog,
                        catalogPath,
                        options.Timing,
                        stageId,
                        string.Empty,
                        outputAssetName,
                        fieldName: $"{entryField}.PresentationId",
                        expectedValue: "non-empty",
                        actualValue: entry.RawPresentationId,
                        presentationId: entry.PresentationId));
                }

                if (entry.DuplicatePresentationId)
                {
                    issues.Add(StagePresentationCatalogIssueFactory.Create(
                        StagePresentationValidationSeverityPolicy.AlwaysError,
                        "PresentationCatalog.DuplicatePresentationId",
                        $"{snapshot.CatalogType} presentation catalog '{snapshot.CatalogName}' contains duplicate PresentationId '{entry.PresentationId}'.",
                        catalog,
                        catalogPath,
                        options.Timing,
                        stageId,
                        string.Empty,
                        outputAssetName,
                        fieldName: $"{entryField}.PresentationId",
                        expectedValue: "unique normalized id",
                        actualValue: entry.RawPresentationId,
                        presentationId: entry.PresentationId));
                }

                if (entry.ViewPrefabMissing)
                {
                    issues.Add(StagePresentationCatalogIssueFactory.Create(
                        StagePresentationValidationSeverityPolicy.AlwaysError,
                        "PresentationCatalog.ViewPrefabMissing",
                        $"{snapshot.CatalogType} presentation catalog '{snapshot.CatalogName}' entry[{entry.EntryIndex}] PresentationId '{entry.PresentationId}' requires a non-null ViewPrefab.",
                        catalog,
                        catalogPath,
                        options.Timing,
                        stageId,
                        string.Empty,
                        outputAssetName,
                        fieldName: $"{entryField}.ViewPrefab",
                        expectedValue: "non-null",
                        actualValue: "null",
                        presentationId: entry.PresentationId));
                }
            }
        }
    }
}
