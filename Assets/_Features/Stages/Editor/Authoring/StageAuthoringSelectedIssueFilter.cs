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
            var lane = StageAuthoringKindRegistry.GetPresentationLane(placement.Kind);
            var presentationId = NormalizePresentationId(lane, placement.PresentationId);
            var result = new List<StageValidationIssue>();
            for (var i = 0; i < issues.Count; i++)
            {
                var issue = issues[i];
                if (Matches(issue, lane, stableGuid, entityId, presentationId))
                {
                    result.Add(issue);
                }
            }

            return result.ToArray();
        }

        private static bool Matches(
            StageValidationIssue issue,
            StageAuthoringPresentationLane lane,
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
                string.Equals(NormalizeIssuePresentationId(lane, issue.PresentationId), presentationId, StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(issue.FieldName) && IsKindBindingField(lane, issue.FieldName))
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

        private static bool IsKindBindingField(StageAuthoringPresentationLane lane, string fieldName)
        {
            return lane switch
            {
                StageAuthoringPresentationLane.Enemy => fieldName.Contains("EnemyPresentationBindings", StringComparison.Ordinal),
                StageAuthoringPresentationLane.Static => fieldName.Contains("StaticEntityPresentationBindings", StringComparison.Ordinal),
                _ => false,
            };
        }

        private static string NormalizeIssuePresentationId(StageAuthoringPresentationLane lane, string presentationId)
        {
            return NormalizePresentationId(lane, presentationId);
        }

        private static string NormalizePresentationId(StageAuthoringPresentationLane lane, string presentationId)
        {
            return lane switch
            {
                StageAuthoringPresentationLane.Enemy => EnemyPresentationCatalogResolver.NormalizePresentationId(presentationId),
                StageAuthoringPresentationLane.Static => StaticEntityPresentationCatalogResolver.NormalizePresentationId(presentationId),
                _ => StageAuthoringProjection.Normalize(presentationId),
            };
        }
    }
}
