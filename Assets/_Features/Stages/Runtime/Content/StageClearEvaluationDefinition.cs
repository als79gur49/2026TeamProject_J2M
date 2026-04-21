using System;
using UnityEngine;

namespace Game.Feature.Stages
{
    [Serializable]
    public struct StageScoreRuleDefinition
    {
        public string MetricId;
        public int Multiplier;
        public int ConstantBonus;
        public bool SubtractMetricValue;
    }

    [Serializable]
    public struct StageStarThresholdDefinition
    {
        public int StarCount;
        public int MinimumScore;
    }

    [Serializable]
    public struct StageRankThresholdDefinition
    {
        public string RankId;
        public int MinimumScore;
    }

    [Serializable]
    public struct StageChallengeDefinition
    {
        public string ChallengeId;
        public string DisplayName;
        public bool AwardOnlyWhenCleared;
        public int ScoreBonus;
    }

    [CreateAssetMenu(menuName = "Gameplay/Stages/Stage Clear Evaluation Definition", fileName = "stage-clear-evaluation")]
    public sealed class StageClearEvaluationDefinition : StageCompanionDefinitionBase
    {
        [SerializeField] private int baseScore;
        [SerializeField] private bool requireClearForAwards = true;
        [SerializeField] private StageScoreRuleDefinition[] scoreRules = Array.Empty<StageScoreRuleDefinition>();
        [SerializeField] private StageStarThresholdDefinition[] starThresholds = Array.Empty<StageStarThresholdDefinition>();
        [SerializeField] private StageRankThresholdDefinition[] rankThresholds = Array.Empty<StageRankThresholdDefinition>();
        [SerializeField] private StageChallengeDefinition[] challenges = Array.Empty<StageChallengeDefinition>();

        public int BaseScore => baseScore;

        public bool RequireClearForAwards => requireClearForAwards;

        public StageScoreRuleDefinition[] ScoreRules => scoreRules ?? Array.Empty<StageScoreRuleDefinition>();

        public StageStarThresholdDefinition[] StarThresholds => starThresholds ?? Array.Empty<StageStarThresholdDefinition>();

        public StageRankThresholdDefinition[] RankThresholds => rankThresholds ?? Array.Empty<StageRankThresholdDefinition>();

        public StageChallengeDefinition[] Challenges => challenges ?? Array.Empty<StageChallengeDefinition>();
    }
}
