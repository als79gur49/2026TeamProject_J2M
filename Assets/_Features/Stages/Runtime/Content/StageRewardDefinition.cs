using System;
using UnityEngine;

namespace Game.Feature.Stages
{
    public enum StageRewardTriggerKind
    {
        Clear = 0,
        EvaluationThreshold = 1,
        ChallengeCompletion = 2,
    }

    [Serializable]
    public struct RewardEntry
    {
        public string RewardId;
        public int Amount;
    }

    [Serializable]
    public struct StageRewardRuleDefinition
    {
        [SerializeField] private string ruleId;
        [SerializeField] private string[] deprecatedRuleIds;

        public StageRewardTriggerKind TriggerKind;
        public bool GrantOnce;
        public int MinimumStars;
        public string RequiredRankId;
        public string RequiredChallengeId;
        public RewardEntry[] Rewards;

        public string RuleId => ruleId ?? string.Empty;

        public string[] DeprecatedRuleIds => deprecatedRuleIds ?? Array.Empty<string>();

        public void SetRuleId(string value)
        {
            ruleId = value ?? string.Empty;
        }

        public void SetDeprecatedRuleIds(string[] value)
        {
            deprecatedRuleIds = value ?? Array.Empty<string>();
        }
    }

    [CreateAssetMenu(menuName = "Gameplay/Stages/Stage Reward Definition", fileName = "stage-reward")]
    public sealed class StageRewardDefinition : StageCompanionDefinitionBase
    {
        [SerializeField] private StageRewardRuleDefinition[] rules = Array.Empty<StageRewardRuleDefinition>();

        public StageRewardRuleDefinition[] Rules => rules ?? Array.Empty<StageRewardRuleDefinition>();
    }
}
