using System;
using Game.Feature.Gameplay.Host;

namespace Game.Feature.Stages.Editor
{
    internal static class StageAuthoringPresentationBindingProjector
    {
        public static void Project(
            StageAuthoringDefinition source,
            StagePresentationDefinition presentationOutput,
            StageAuthoringBuildData buildData,
            StageAuthoringGenerationReport report)
        {
            for (var i = 0; i < buildData.Placements.Count; i++)
            {
                var placement = buildData.Placements[i];
                var entityId = buildData.EntityIdsByStableGuid[placement.StableGuid];
                switch (StageAuthoringKindRegistry.GetPresentationLane(placement.Kind))
                {
                    case StageAuthoringPresentationLane.None:
                        break;
                    case StageAuthoringPresentationLane.Enemy:
                        TryAddEnemyPresentationBinding(source, presentationOutput, placement, entityId, buildData, report);
                        break;
                    case StageAuthoringPresentationLane.Static:
                        TryAddStaticPresentationBinding(source, presentationOutput, placement, entityId, buildData, report);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            buildData.EnemyPresentationBindings.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
            buildData.StaticEntityPresentationBindings.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        private static void TryAddEnemyPresentationBinding(
            StageAuthoringDefinition source,
            StagePresentationDefinition presentationOutput,
            StagePlacedEntityAuthoring placement,
            int entityId,
            StageAuthoringBuildData buildData,
            StageAuthoringGenerationReport report)
        {
            var presentationId = Normalize(placement.PresentationId);
            if (string.IsNullOrEmpty(presentationId))
            {
                report.Add(
                    StageValidationSeverity.Warning,
                    "authoring.presentation.enemy-missing",
                    $"Enemy placement '{placement.StableGuid}' has no presentation id; runtime fallback will be used.",
                    source,
                    string.Empty);
                return;
            }

            if (presentationOutput == null || !ContainsEnemyPresentationId(presentationOutput.EnemyPresentationCatalog, presentationId))
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "authoring.presentation.enemy-invalid",
                    $"Enemy placement '{placement.StableGuid}' references unresolved presentation id '{presentationId}'.",
                    source,
                    string.Empty);
                return;
            }

            buildData.EnemyPresentationBindings.Add(new EnemyPresentationBinding
            {
                EntityId = entityId,
                PresentationId = presentationId,
            });
        }

        private static void TryAddStaticPresentationBinding(
            StageAuthoringDefinition source,
            StagePresentationDefinition presentationOutput,
            StagePlacedEntityAuthoring placement,
            int entityId,
            StageAuthoringBuildData buildData,
            StageAuthoringGenerationReport report)
        {
            var presentationId = Normalize(placement.PresentationId);
            if (string.IsNullOrEmpty(presentationId))
            {
                report.Add(
                    StageValidationSeverity.Warning,
                    "authoring.presentation.static-missing",
                    $"{placement.Kind} placement '{placement.StableGuid}' has no presentation id; runtime fallback will be used.",
                    source,
                    string.Empty);
                return;
            }

            if (presentationOutput == null || !ContainsStaticPresentationId(presentationOutput.StaticEntityPresentationCatalog, presentationId))
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "authoring.presentation.static-invalid",
                    $"{placement.Kind} placement '{placement.StableGuid}' references unresolved presentation id '{presentationId}'.",
                    source,
                    string.Empty);
                return;
            }

            buildData.StaticEntityPresentationBindings.Add(new StaticEntityPresentationBinding
            {
                EntityId = entityId,
                PresentationId = presentationId,
            });
        }

        private static bool ContainsEnemyPresentationId(EnemyPresentationCatalog catalog, string presentationId)
        {
            if (catalog == null)
            {
                return false;
            }

            var entries = catalog.Entries;
            for (var i = 0; i < entries.Length; i++)
            {
                if (string.Equals(
                        EnemyPresentationCatalogResolver.NormalizePresentationId(entries[i].PresentationId),
                        presentationId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsStaticPresentationId(StaticEntityPresentationCatalog catalog, string presentationId)
        {
            if (catalog == null)
            {
                return false;
            }

            var entries = catalog.Entries;
            for (var i = 0; i < entries.Length; i++)
            {
                if (string.Equals(
                        StaticEntityPresentationCatalogResolver.NormalizePresentationId(entries[i].PresentationId),
                        presentationId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Normalize(string value)
        {
            return StageAuthoringGenerator.Normalize(value);
        }
    }
}
