using System;
using UnityEngine;

namespace Game.Feature.Stages
{
    [Serializable]
    public struct StageUnlockRuleDefinition
    {
        public StageId RequiredStageId;
        public int MinimumStars;
        public string RequiredRankId;
        public string RequiredChallengeId;
    }

    [CreateAssetMenu(menuName = "Gameplay/Stages/Stage Progression Definition", fileName = "stage-progression")]
    public sealed class StageProgressionDefinition : StageCompanionDefinitionBase
    {
        [SerializeField] private string worldId = string.Empty;
        [SerializeField] private string chapterId = string.Empty;
        [SerializeField] private int sortOrder;
        [SerializeField] private bool unlockedByDefault;
        [SerializeField] private StageUnlockRuleDefinition[] unlockRules = Array.Empty<StageUnlockRuleDefinition>();

        public string WorldId => worldId ?? string.Empty;

        public string ChapterId => chapterId ?? string.Empty;

        public int SortOrder => sortOrder;

        public bool UnlockedByDefault => unlockedByDefault;

        public StageUnlockRuleDefinition[] UnlockRules => unlockRules ?? Array.Empty<StageUnlockRuleDefinition>();
    }
}
