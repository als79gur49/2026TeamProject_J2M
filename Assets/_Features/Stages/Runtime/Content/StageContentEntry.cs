using UnityEngine;

namespace Game.Feature.Stages
{
    public enum CampaignParticipation
    {
        Unspecified = 0,
        Campaign = 1,
        CatalogOnly = 2,
    }

    public enum CatalogOnlyReason
    {
        None = 0,
        LegacyArchived = 1,
    }

    [CreateAssetMenu(menuName = "Gameplay/Stages/Stage Content Entry", fileName = "stage-content-entry")]
    public sealed class StageContentEntry : ScriptableObject
    {
        [SerializeField, HideInInspector] private StageId stageId = StageId.None;
        [SerializeField] private StageAuthoringDefinition authoringDefinition;
        [SerializeField] private StageDefinition gameplayDefinition;
        [SerializeField] private StagePresentationDefinition presentationDefinition;
        [SerializeField] private StageAudioDefinition audioDefinition;
        [SerializeField] private bool isInitiallyAvailable = true;
        [SerializeField] private CampaignParticipation campaignParticipation;
        [SerializeField] private CatalogOnlyReason catalogOnlyReason;

        public StageId StageId => stageId;

        public StageAuthoringDefinition AuthoringDefinition => authoringDefinition;

        public StageDefinition GameplayDefinition => gameplayDefinition;

        public StagePresentationDefinition PresentationDefinition => presentationDefinition;

        public StageAudioDefinition AudioDefinition => audioDefinition;

        public bool IsInitiallyAvailable => isInitiallyAvailable;

        public CampaignParticipation CampaignParticipation => campaignParticipation;

        public CatalogOnlyReason CatalogOnlyReason => catalogOnlyReason;

        public void AssignStageId(StageId value)
        {
            stageId = value;
        }

        public void AssignAuthoringDefinition(StageAuthoringDefinition definition)
        {
            authoringDefinition = definition;
        }

        public void AssignGameplayDefinition(StageDefinition definition)
        {
            gameplayDefinition = definition;
        }

        public void AssignPresentationDefinition(StagePresentationDefinition definition)
        {
            presentationDefinition = definition;
        }

        public void AssignAudioDefinition(StageAudioDefinition definition)
        {
            audioDefinition = definition;
        }

        public void AssignInitialAvailability(bool value)
        {
            isInitiallyAvailable = value;
        }

        public void AssignCampaignParticipation(
            CampaignParticipation participation,
            CatalogOnlyReason exclusionReason = CatalogOnlyReason.None)
        {
            campaignParticipation = participation;
            catalogOnlyReason = exclusionReason;
        }
    }
}
