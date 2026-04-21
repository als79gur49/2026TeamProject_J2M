using UnityEngine;

namespace Game.Feature.Stages
{
    [CreateAssetMenu(menuName = "Gameplay/Stages/Stage Content Entry", fileName = "stage-content-entry")]
    public sealed class StageContentEntry : ScriptableObject
    {
        [SerializeField, HideInInspector] private StageId stageId = StageId.None;
        [SerializeField] private StageDefinition gameplayDefinition;
        [SerializeField] private StagePresentationDefinition presentationDefinition;
        [SerializeField] private StageClearEvaluationDefinition clearEvaluationDefinition;
        [SerializeField] private StageRewardDefinition rewardDefinition;
        [SerializeField] private StageProgressionDefinition progressionDefinition;

        public StageId StageId => stageId;

        public StageDefinition GameplayDefinition => gameplayDefinition;

        public StagePresentationDefinition PresentationDefinition => presentationDefinition;

        public StageClearEvaluationDefinition ClearEvaluationDefinition => clearEvaluationDefinition;

        public StageRewardDefinition RewardDefinition => rewardDefinition;

        public StageProgressionDefinition ProgressionDefinition => progressionDefinition;

        public void AssignStageId(StageId value)
        {
            stageId = value;
        }

        public void AssignGameplayDefinition(StageDefinition definition)
        {
            gameplayDefinition = definition;
        }

        public void AssignPresentationDefinition(StagePresentationDefinition definition)
        {
            presentationDefinition = definition;
        }

        public void AssignClearEvaluationDefinition(StageClearEvaluationDefinition definition)
        {
            clearEvaluationDefinition = definition;
        }

        public void AssignRewardDefinition(StageRewardDefinition definition)
        {
            rewardDefinition = definition;
        }

        public void AssignProgressionDefinition(StageProgressionDefinition definition)
        {
            progressionDefinition = definition;
        }
    }
}
