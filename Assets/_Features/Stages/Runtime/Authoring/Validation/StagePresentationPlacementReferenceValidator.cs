using System.Collections.Generic;
using Game.Feature.Gameplay.Host;

namespace Game.Feature.Stages
{
    internal static class StagePresentationPlacementReferenceValidator
    {
        public static StagePresentationRequiredCatalogs ResolveRequiredCatalogs(
            StageAuthoringDefinition authoring,
            StagePresentationDefinition presentation)
        {
            var result = new StagePresentationRequiredCatalogs();
            if (authoring != null)
            {
                var placements = authoring.Placements;
                for (var i = 0; i < placements.Count; i++)
                {
                    var placement = placements[i];
                    if (placement == null)
                    {
                        continue;
                    }

                    switch (StageAuthoringKindRegistry.GetPresentationLane(placement.Kind))
                    {
                        case StageAuthoringPresentationLane.Enemy:
                            result.Enemy = true;
                            break;
                        case StageAuthoringPresentationLane.Static:
                            result.Static = true;
                            break;
                    }
                }
            }

            result.Enemy |= presentation.EnemyPresentationBindings.Length > 0;
            result.Static |= presentation.StaticEntityPresentationBindings.Length > 0;
            return result;
        }

        public static void ValidateCatalogAvailability(
            StagePresentationDefinition presentation,
            StageAuthoringPresentationCatalogSnapshot enemySnapshot,
            StageAuthoringPresentationCatalogSnapshot staticSnapshot,
            StagePresentationRequiredCatalogs requiredCatalogs,
            bool strict,
            StageCatalogValidationOptions options,
            string stageId,
            string authoringAssetName,
            string outputAssetName,
            ICollection<StageValidationIssue> issues)
        {
            var presentationPath = StagePresentationCatalogIssueFactory.GetAssetPath(presentation, options);
            if (requiredCatalogs.Enemy)
            {
                if (!enemySnapshot.CatalogAssigned)
                {
                    issues.Add(StagePresentationCatalogIssueFactory.Create(
                        StagePresentationValidationSeverityPolicy.ResolveSoftSeverity(strict),
                        "PresentationCatalog.EnemyCatalogMissing",
                        $"StagePresentationDefinition '{presentation.name}' requires an enemy presentation catalog.",
                        presentation,
                        presentationPath,
                        options.Timing,
                        stageId,
                        authoringAssetName,
                        outputAssetName,
                        fieldName: "EnemyPresentationCatalog",
                        expectedValue: "assigned",
                        actualValue: "null"));
                }
                else if (enemySnapshot.PresentationIds.Length == 0)
                {
                    issues.Add(StagePresentationCatalogIssueFactory.Create(
                        StagePresentationValidationSeverityPolicy.ResolveSoftSeverity(strict),
                        "PresentationCatalog.EnemyCatalogEmpty",
                        $"StagePresentationDefinition '{presentation.name}' enemy presentation catalog has no usable PresentationId entries.",
                        presentation,
                        presentationPath,
                        options.Timing,
                        stageId,
                        authoringAssetName,
                        outputAssetName,
                        fieldName: "EnemyPresentationCatalog",
                        expectedValue: "at least one usable PresentationId",
                        actualValue: "0"));
                }
            }

            if (!requiredCatalogs.Static)
            {
                return;
            }

            if (!staticSnapshot.CatalogAssigned)
            {
                issues.Add(StagePresentationCatalogIssueFactory.Create(
                    StagePresentationValidationSeverityPolicy.ResolveSoftSeverity(strict),
                    "PresentationCatalog.StaticCatalogMissing",
                    $"StagePresentationDefinition '{presentation.name}' requires a static entity presentation catalog.",
                    presentation,
                    presentationPath,
                    options.Timing,
                    stageId,
                    authoringAssetName,
                    outputAssetName,
                    fieldName: "StaticEntityPresentationCatalog",
                    expectedValue: "assigned",
                    actualValue: "null"));
            }
            else if (staticSnapshot.PresentationIds.Length == 0)
            {
                issues.Add(StagePresentationCatalogIssueFactory.Create(
                    StagePresentationValidationSeverityPolicy.ResolveSoftSeverity(strict),
                    "PresentationCatalog.StaticCatalogEmpty",
                    $"StagePresentationDefinition '{presentation.name}' static entity presentation catalog has no usable PresentationId entries.",
                    presentation,
                    presentationPath,
                    options.Timing,
                    stageId,
                    authoringAssetName,
                    outputAssetName,
                    fieldName: "StaticEntityPresentationCatalog",
                    expectedValue: "at least one usable PresentationId",
                    actualValue: "0"));
            }
        }

        public static void ValidateAuthoringPlacements(
            StageAuthoringDefinition authoring,
            StageAuthoringPresentationCatalogSnapshot enemySnapshot,
            StageAuthoringPresentationCatalogSnapshot staticSnapshot,
            bool strict,
            StageCatalogValidationOptions options,
            string stageId,
            string outputAssetName,
            ICollection<StageValidationIssue> issues)
        {
            var authoringPath = StagePresentationCatalogIssueFactory.GetAssetPath(authoring, options);
            var allocationPlan = StageAuthoringProjection.BuildAllocationPlan(authoring);
            var placements = authoring.Placements;
            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                if (placement == null || !StageAuthoringKindRegistry.RequiresPresentation(placement.Kind))
                {
                    continue;
                }

                var stableGuid = StageAuthoringProjection.Normalize(placement.StableGuid);
                allocationPlan.EntityIdsByStableGuid.TryGetValue(stableGuid, out var entityId);
                var lane = StageAuthoringKindRegistry.GetPresentationLane(placement.Kind);
                var presentationId = NormalizePlacementPresentationId(lane, placement.PresentationId);
                if (string.IsNullOrEmpty(presentationId))
                {
                    var staticPlacement = lane == StageAuthoringPresentationLane.Static;
                    issues.Add(StagePresentationCatalogIssueFactory.Create(
                        StagePresentationValidationSeverityPolicy.ResolveSoftSeverity(strict),
                        staticPlacement
                            ? "PresentationCatalog.StaticPresentationIdEmpty"
                            : "PresentationCatalog.EnemyPresentationIdEmpty",
                        $"{placement.Kind} placement '{stableGuid}' must declare a non-empty PresentationId.",
                        authoring,
                        authoringPath,
                        options.Timing,
                        stageId,
                        authoring.name,
                        outputAssetName,
                        entityId,
                        stableGuid,
                        $"Placements[{i}].PresentationId",
                        "non-empty",
                        placement.PresentationId,
                        presentationId));
                    continue;
                }

                if (lane == StageAuthoringPresentationLane.Enemy)
                {
                    if (enemySnapshot.PresentationIds.Length > 0 &&
                        !enemySnapshot.ContainsPresentationId(presentationId))
                    {
                        issues.Add(StagePresentationCatalogIssueFactory.Create(
                            StagePresentationValidationSeverityPolicy.ResolveSoftSeverity(strict),
                            "PresentationCatalog.EnemyPresentationIdMissing",
                            $"Enemy placement '{stableGuid}' references unresolved PresentationId '{presentationId}'.",
                            authoring,
                            authoringPath,
                            options.Timing,
                            stageId,
                            authoring.name,
                            outputAssetName,
                            entityId,
                            stableGuid,
                            $"Placements[{i}].PresentationId",
                            "id present in enemy presentation catalog",
                            placement.PresentationId,
                            presentationId));
                    }
                }
                else if (lane == StageAuthoringPresentationLane.Static &&
                         staticSnapshot.PresentationIds.Length > 0 &&
                         !staticSnapshot.ContainsPresentationId(presentationId))
                {
                    issues.Add(StagePresentationCatalogIssueFactory.Create(
                        StagePresentationValidationSeverityPolicy.ResolveSoftSeverity(strict),
                        "PresentationCatalog.StaticPresentationIdMissing",
                        $"{placement.Kind} placement '{stableGuid}' references unresolved PresentationId '{presentationId}'.",
                        authoring,
                        authoringPath,
                        options.Timing,
                        stageId,
                        authoring.name,
                        outputAssetName,
                        entityId,
                        stableGuid,
                        $"Placements[{i}].PresentationId",
                        "id present in static entity presentation catalog",
                        placement.PresentationId,
                        presentationId));
                }
            }
        }

        public static bool HasPresentationPlacements(StageAuthoringDefinition authoring)
        {
            var placements = authoring.Placements;
            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                if (placement != null && StageAuthoringKindRegistry.RequiresPresentation(placement.Kind))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizePlacementPresentationId(
            StageAuthoringPresentationLane lane,
            string presentationId)
        {
            return lane switch
            {
                StageAuthoringPresentationLane.Enemy => EnemyPresentationCatalogResolver.NormalizePresentationId(presentationId),
                StageAuthoringPresentationLane.Static => StaticEntityPresentationCatalogResolver.NormalizePresentationId(presentationId),
                _ => StageAuthoringProjection.Normalize(presentationId),
            };
        }
    }

    internal struct StagePresentationRequiredCatalogs
    {
        public bool Enemy;
        public bool Static;
    }
}
