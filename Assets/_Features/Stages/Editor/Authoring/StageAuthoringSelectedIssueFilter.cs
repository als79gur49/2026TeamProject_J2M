using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Host;

namespace Game.Feature.Stages.Editor
{
    internal static class StageAuthoringSelectedIssueFilter
    {
        public static StageValidationIssue[] Filter(
            IReadOnlyList<StageValidationIssue> issues,
            StagePlacedEntityAuthoring placement,
            int entityId)
        {
            if (issues == null || placement == null)
            {
                return Array.Empty<StageValidationIssue>();
            }

            var stableGuid = StageAuthoringProjection.Normalize(placement.StableGuid);
            var presentationId = NormalizePresentationId(placement.Kind, placement.PresentationId);
            var result = new List<StageValidationIssue>();
            for (var i = 0; i < issues.Count; i++)
            {
                var issue = issues[i];
                if (Matches(issue, placement.Kind, stableGuid, entityId, presentationId))
                {
                    result.Add(issue);
                }
            }

            return result.ToArray();
        }

        private static bool Matches(
            StageValidationIssue issue,
            StageAuthoringEntityKind kind,
            string stableGuid,
            int entityId,
            string presentationId)
        {
            if (!string.IsNullOrEmpty(stableGuid) &&
                string.Equals(StageAuthoringProjection.Normalize(issue.StableGuid), stableGuid, StringComparison.Ordinal))
            {
                return true;
            }

            if (entityId > 0 && issue.EntityId == entityId)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(presentationId) &&
                string.Equals(NormalizeIssuePresentationId(kind, issue.PresentationId), presentationId, StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(issue.FieldName) && IsKindBindingField(kind, issue.FieldName))
            {
                if (entityId > 0 && issue.FieldName.Contains($"[{entityId}]", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(stableGuid) &&
                issue.Message.Contains(stableGuid, StringComparison.Ordinal))
            {
                return true;
            }

            return !string.IsNullOrEmpty(presentationId) &&
                   issue.Message.Contains(presentationId, StringComparison.Ordinal);
        }

        private static bool IsKindBindingField(StageAuthoringEntityKind kind, string fieldName)
        {
            var expectsEnemy = kind == StageAuthoringEntityKind.Enemy;
            return expectsEnemy
                ? fieldName.Contains("EnemyPresentationBindings", StringComparison.Ordinal)
                : fieldName.Contains("StaticEntityPresentationBindings", StringComparison.Ordinal);
        }

        private static string NormalizeIssuePresentationId(StageAuthoringEntityKind kind, string presentationId)
        {
            return NormalizePresentationId(kind, presentationId);
        }

        private static string NormalizePresentationId(StageAuthoringEntityKind kind, string presentationId)
        {
            return kind == StageAuthoringEntityKind.Enemy
                ? EnemyPresentationCatalogResolver.NormalizePresentationId(presentationId)
                : StaticEntityPresentationCatalogResolver.NormalizePresentationId(presentationId);
        }
    }
}
