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
        Phase6_SunsetFinalization = 6,
    }

    public readonly struct StageValidationIssue
    {
        public StageValidationIssue(
            StageValidationSeverity severity,
            string code,
            string message,
            UnityEngine.Object context = null,
            string assetPath = "",
            StageValidationTiming timing = StageValidationTiming.EditorAuthoring,
            string stageId = "",
            string authoringAssetName = "",
            string outputAssetName = "",
            int entityId = 0,
            string stableGuid = "",
            string fieldName = "",
            string expectedValue = "",
            string actualValue = "",
            string presentationId = "")
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Context = context;
            AssetPath = assetPath ?? string.Empty;
            Timing = timing;
            StageId = stageId ?? string.Empty;
            AuthoringAssetName = authoringAssetName ?? string.Empty;
            OutputAssetName = outputAssetName ?? string.Empty;
            EntityId = entityId;
            StableGuid = stableGuid ?? string.Empty;
            FieldName = fieldName ?? string.Empty;
            ExpectedValue = expectedValue ?? string.Empty;
            ActualValue = actualValue ?? string.Empty;
            PresentationId = presentationId ?? string.Empty;
        }

        public StageValidationSeverity Severity { get; }

        public string Code { get; }

        public string Message { get; }

        public UnityEngine.Object Context { get; }

        public string AssetPath { get; }

        public StageValidationTiming Timing { get; }

        public string StageId { get; }

        public string AuthoringAssetName { get; }

        public string OutputAssetName { get; }

        public int EntityId { get; }

        public string StableGuid { get; }

        public string FieldName { get; }

        public string ExpectedValue { get; }

        public string ActualValue { get; }

        public string PresentationId { get; }
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

        public bool RequireAudioDefinition { get; set; }

        public StageValidationTiming Timing { get; set; } = StageValidationTiming.EditorAuthoring;

        public StageValidationPhase Phase { get; set; } = StageValidationPhase.Phase1_LoadModeFence;

        public StageValidationWaiverList WaiverList { get; set; }

        public IStageValidationAssetMetadataProvider AssetMetadataProvider { get; set; }

        internal IStageValidationAssetMetadataProvider ResolvedAssetMetadataProvider { get; private set; }

        internal StageCatalogValidationOptions CloneWithResolvedAssetMetadataProvider(
            IStageValidationAssetMetadataProvider provider)
        {
            return new StageCatalogValidationOptions
            {
                RequirePresentationDefinition = RequirePresentationDefinition,
                RequireAudioDefinition = RequireAudioDefinition,
                Timing = Timing,
                Phase = Phase,
                WaiverList = WaiverList,
                AssetMetadataProvider = AssetMetadataProvider,
                ResolvedAssetMetadataProvider = provider,
            };
        }
    }

    public interface IStageValidationAssetMetadataProvider
    {
        string GetAssetPath(UnityEngine.Object asset);

        string GetAssetGuid(UnityEngine.Object asset);
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
