using System.Collections.Generic;
using Game.Feature.Gameplay.Host;

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

                if (snapshot.CatalogType == "Enemy")
                {
                    ValidateEnemyVfxProfile(
                        catalog,
                        catalogPath,
                        snapshot,
                        entry,
                        entryField,
                        options,
                        stageId,
                        outputAssetName,
                        issues);
                }
            }
        }

        private static void ValidateEnemyVfxProfile(
            UnityEngine.Object catalog,
            string catalogPath,
            StageAuthoringPresentationCatalogSnapshot snapshot,
            StageAuthoringPresentationCatalogEntrySnapshot entry,
            string entryField,
            StageCatalogValidationOptions options,
            string stageId,
            string outputAssetName,
            ICollection<StageValidationIssue> issues)
        {
            if (!entry.VfxProfileAssigned)
            {
                return;
            }

            if (entry.VfxProfileStatus == EnemyPresentationVfxProfileStatusKind.WrongFamily)
            {
                issues.Add(StagePresentationCatalogIssueFactory.Create(
                    StagePresentationValidationSeverityPolicy.AlwaysError,
                    "PresentationCatalog.EnemyVfxProfileFamilyMismatch",
                    $"{snapshot.CatalogType} presentation catalog '{snapshot.CatalogName}' entry[{entry.EntryIndex}] PresentationId '{entry.PresentationId}' must reference an Enemy VFX profile. Actual family: '{entry.VfxProfileFamily}'.",
                    catalog,
                    catalogPath,
                    options.Timing,
                    stageId,
                    string.Empty,
                    outputAssetName,
                    fieldName: $"{entryField}.VfxProfileAsset",
                    expectedValue: "Enemy",
                    actualValue: entry.VfxProfileFamily,
                    presentationId: entry.PresentationId));
                return;
            }

            var emittedDiagnostic = false;
            for (var i = 0; i < entry.VfxProfileDiagnostics.Length; i++)
            {
                var diagnostic = entry.VfxProfileDiagnostics[i];
                if (diagnostic.Severity == EnemyPresentationVfxProfileDiagnosticSeverity.Info)
                {
                    continue;
                }

                emittedDiagnostic = true;
                var isError = diagnostic.Severity == EnemyPresentationVfxProfileDiagnosticSeverity.Error;
                issues.Add(StagePresentationCatalogIssueFactory.Create(
                    isError ? StageValidationSeverity.Error : StageValidationSeverity.Warning,
                    isError
                        ? "PresentationCatalog.EnemyVfxProfileInvalid"
                        : "PresentationCatalog.EnemyVfxProfileWarning",
                    $"{snapshot.CatalogType} presentation catalog '{snapshot.CatalogName}' entry[{entry.EntryIndex}] PresentationId '{entry.PresentationId}' VFX profile '{entry.VfxProfileName}' reported {diagnostic.Code}: {diagnostic.Message}",
                    catalog,
                    catalogPath,
                    options.Timing,
                    stageId,
                    string.Empty,
                    outputAssetName,
                    fieldName: $"{entryField}.VfxProfileAsset",
                    expectedValue: "valid Enemy VFX profile",
                    actualValue: entry.VfxProfileName,
                    presentationId: entry.PresentationId));
            }

            if (!emittedDiagnostic &&
                entry.VfxProfileStatus == EnemyPresentationVfxProfileStatusKind.Invalid)
            {
                issues.Add(StagePresentationCatalogIssueFactory.Create(
                    StagePresentationValidationSeverityPolicy.AlwaysError,
                    "PresentationCatalog.EnemyVfxProfileInvalid",
                    $"{snapshot.CatalogType} presentation catalog '{snapshot.CatalogName}' entry[{entry.EntryIndex}] PresentationId '{entry.PresentationId}' has an invalid VFX profile.",
                    catalog,
                    catalogPath,
                    options.Timing,
                    stageId,
                    string.Empty,
                    outputAssetName,
                    fieldName: $"{entryField}.VfxProfileAsset",
                    expectedValue: "valid Enemy VFX profile",
                    actualValue: entry.VfxProfileName,
                    presentationId: entry.PresentationId));
            }
        }
    }
}
