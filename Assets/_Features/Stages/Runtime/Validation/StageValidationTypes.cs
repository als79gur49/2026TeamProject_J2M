using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Stages
{
    public enum StageValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
    }

    public enum StageValidationTiming
    {
        EditorAuthoring = 0,
        PreBuild = 1,
        TestOrCi = 2,
        RuntimeDefensive = 3,
    }

    public enum StageValidationPhase
    {
        Phase1_LoadModeFence = 1,
        Phase2_MigrationAnalysis = 2,
        Phase3_CanonicalContentApply = 3,
        Phase4_ProductionBootstrapConversion = 4,
        Phase5_Hardening = 5,
    }

    public readonly struct StageValidationIssue
    {
        public StageValidationIssue(
            StageValidationSeverity severity,
            string code,
            string message,
            UnityEngine.Object context = null,
            string assetPath = "",
            StageValidationTiming timing = StageValidationTiming.EditorAuthoring)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Context = context;
            AssetPath = assetPath ?? string.Empty;
            Timing = timing;
        }

        public StageValidationSeverity Severity { get; }

        public string Code { get; }

        public string Message { get; }

        public UnityEngine.Object Context { get; }

        public string AssetPath { get; }

        public StageValidationTiming Timing { get; }
    }

    public sealed class StageValidationReport
    {
        private readonly List<StageValidationIssue> issues = new();

        public IReadOnlyList<StageValidationIssue> Issues => issues;

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

        public void Add(StageValidationIssue issue)
        {
            issues.Add(issue);
        }

        public void Add(
            StageValidationSeverity severity,
            string code,
            string message,
            UnityEngine.Object context = null,
            string assetPath = "",
            StageValidationTiming timing = StageValidationTiming.EditorAuthoring)
        {
            Add(new StageValidationIssue(severity, code, message, context, assetPath, timing));
        }

        public void AddRange(IEnumerable<StageValidationIssue> additionalIssues)
        {
            if (additionalIssues == null)
            {
                return;
            }

            issues.AddRange(additionalIssues);
        }
    }

    public sealed class StageCatalogValidationOptions
    {
        public static readonly StageCatalogValidationOptions Default = new();

        public bool RequirePresentationDefinition { get; set; }

        public bool RequireClearEvaluationDefinition { get; set; }

        public bool RequireRewardDefinition { get; set; }

        public bool RequireProgressionDefinition { get; set; }

        public StageValidationTiming Timing { get; set; } = StageValidationTiming.EditorAuthoring;

        public StageValidationPhase Phase { get; set; } = StageValidationPhase.Phase1_LoadModeFence;

        public ISet<string> KnownBgmKeys { get; set; }

        public StageValidationWaiverList WaiverList { get; set; }

        public bool EnforceCanonicalLegacyPresentationBridgeWarnings { get; set; } = true;

        public ISet<string> GrandfatherGameplayAssetGuids { get; set; }
    }

    [Serializable]
    public struct StageValidationWaiverEntry
    {
        public string IssueCode;
        public string AssetGuidOrScenePath;
        public string Owner;
        public string Reason;
        public StageValidationPhase AllowedUntilPhase;
    }

    [CreateAssetMenu(menuName = "Gameplay/Stages/Stage Validation Waiver List", fileName = "StageValidationWaiverList")]
    public sealed class StageValidationWaiverList : ScriptableObject
    {
        [SerializeField] private StageValidationWaiverEntry[] entries = Array.Empty<StageValidationWaiverEntry>();

        public IReadOnlyList<StageValidationWaiverEntry> Entries => entries ?? Array.Empty<StageValidationWaiverEntry>();

        public bool IsWaived(string issueCode, string assetGuidOrScenePath, StageValidationPhase phase)
        {
            if (string.IsNullOrWhiteSpace(issueCode))
            {
                return false;
            }

            for (var i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];
                if (!string.Equals(entry.IssueCode, issueCode, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(entry.AssetGuidOrScenePath) &&
                    !string.Equals(entry.AssetGuidOrScenePath, assetGuidOrScenePath, StringComparison.Ordinal))
                {
                    continue;
                }

                if (phase > entry.AllowedUntilPhase)
                {
                    continue;
                }

                return true;
            }

            return false;
        }
    }
}
