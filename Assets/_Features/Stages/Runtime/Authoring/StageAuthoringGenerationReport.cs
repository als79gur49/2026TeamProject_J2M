using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Stages
{
    public sealed class StageAuthoringGenerationReport
    {
        private readonly List<StageValidationIssue> issues = new();
        private readonly Dictionary<string, int> entityIdsByStableGuid = new(System.StringComparer.Ordinal);

        public IReadOnlyList<StageValidationIssue> Issues => issues;

        public IReadOnlyDictionary<string, int> EntityIdsByStableGuid => entityIdsByStableGuid;

        public bool HasErrors
        {
            get
            {
                for (var i = 0; i < issues.Count; i++)
                {
                    if (issues[i].Severity == StageValidationSeverity.Error)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void Add(
            StageValidationSeverity severity,
            string code,
            string message,
            Object context = null,
            string assetPath = "",
            StageValidationTiming timing = StageValidationTiming.EditorAuthoring)
        {
            issues.Add(new StageValidationIssue(severity, code, message, context, assetPath, timing));
        }

        public void AddRange(IEnumerable<StageValidationIssue> additionalIssues)
        {
            if (additionalIssues == null)
            {
                return;
            }

            issues.AddRange(additionalIssues);
        }

        public void RecordEntityId(string stableGuid, int entityId)
        {
            if (string.IsNullOrWhiteSpace(stableGuid) || entityId <= 0)
            {
                return;
            }

            entityIdsByStableGuid[stableGuid.Trim()] = entityId;
        }
    }
}
