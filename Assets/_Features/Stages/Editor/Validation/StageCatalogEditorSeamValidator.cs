using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Host;

namespace Game.Feature.Stages.Editor
{
    public sealed class StageCatalogEditorSeamValidator
    {
        public StageValidationReport Validate(
            StageCatalog catalog,
            StageCatalogValidationOptions options = null)
        {
            options ??= StageCatalogValidationOptions.Default;
            var report = new StageValidationReport();
            if (catalog == null)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "catalog.null",
                    "StageCatalog reference cannot be null.",
                    timing: options.Timing);
                return report;
            }

            ValidateEntries(catalog.Entries, options, report);
            return report;
        }

        public StageValidationReport ValidateEntries(
            IReadOnlyList<StageContentEntry> entries,
            StageCatalogValidationOptions options = null)
        {
            var report = new StageValidationReport();
            ValidateEntries(entries, options ?? StageCatalogValidationOptions.Default, report);
            return report;
        }

        private static void ValidateEntries(
            IReadOnlyList<StageContentEntry> entries,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            if (entries == null)
            {
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                ValidateEntry(entries[i], options, report);
            }
        }

        private static void ValidateEntry(
            StageContentEntry entry,
            StageCatalogValidationOptions options,
            StageValidationReport report)
        {
            if (!options.EnforceCanonicalLegacyPresentationBridgeWarnings ||
                entry == null ||
                entry.GameplayDefinition == null ||
                entry.PresentationDefinition == null ||
                !HasLegacyPresentationIds(entry.GameplayDefinition))
            {
                return;
            }

            var legacyResolved = LegacyStagePresentationEditorBridge.Resolve(
                entry.GameplayDefinition,
                entry.PresentationDefinition.EnemyPresentationCatalog,
                entry.PresentationDefinition.StaticEntityPresentationCatalog);
            var enemyBindingsMatch = BindingsEqual(
                legacyResolved.EnemyPresentationBindings,
                entry.PresentationDefinition.EnemyPresentationBindings);
            var staticBindingsMatch = BindingsEqual(
                legacyResolved.StaticEntityPresentationBindings,
                entry.PresentationDefinition.StaticEntityPresentationBindings);
            if (enemyBindingsMatch && staticBindingsMatch)
            {
                return;
            }

            report.Add(
                ResolveLegacyBindingMismatchSeverity(options),
                "presentation.legacy-fallback.mismatch",
                $"StagePresentationDefinition '{entry.PresentationDefinition.name}' disagrees with the editor-only legacy PresentationId bridge for stage '{entry.StageId.Value}'.",
                entry.PresentationDefinition,
                GetAssetPath(entry.PresentationDefinition),
                options.Timing);
        }

        private static bool HasLegacyPresentationIds(StageDefinition gameplayDefinition)
        {
            if (gameplayDefinition == null)
            {
                return false;
            }

            var spawns = gameplayDefinition.Spawns;
            for (var i = 0; i < spawns.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(spawns[i].PresentationId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool BindingsEqual(
            IReadOnlyList<EnemyPresentationBinding> left,
            IReadOnlyList<EnemyPresentationBinding> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (var i = 0; i < left.Count; i++)
            {
                if (left[i].EntityId != right[i].EntityId ||
                    !string.Equals(left[i].PresentationId, right[i].PresentationId, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool BindingsEqual(
            IReadOnlyList<StaticEntityPresentationBinding> left,
            IReadOnlyList<StaticEntityPresentationBinding> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (var i = 0; i < left.Count; i++)
            {
                if (left[i].EntityId != right[i].EntityId ||
                    !string.Equals(left[i].PresentationId, right[i].PresentationId, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static StageValidationSeverity ResolveLegacyBindingMismatchSeverity(StageCatalogValidationOptions options)
        {
            return options.Phase >= StageValidationPhase.Phase2_MigrationAnalysis
                ? StageValidationSeverity.Error
                : StageValidationSeverity.Warning;
        }

        private static string GetAssetPath(UnityEngine.Object asset)
        {
            return asset == null ? string.Empty : UnityEditor.AssetDatabase.GetAssetPath(asset);
        }
    }
}
