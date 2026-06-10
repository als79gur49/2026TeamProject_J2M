using UnityEngine;

namespace Game.Feature.Stages
{
    [CreateAssetMenu(menuName = "Gameplay/Stages/Stage Content Entry", fileName = "stage-content-entry")]
    public sealed class StageContentEntry : ScriptableObject
    {
        [SerializeField, HideInInspector] private StageId stageId = StageId.None;
        [SerializeField] private StageAuthoringDefinition authoringDefinition;
        [SerializeField] private StageDefinition gameplayDefinition;
        [SerializeField] private StagePresentationDefinition presentationDefinition;
        [SerializeField] private StageAudioDefinition audioDefinition;
        [SerializeField] private string catalogWorldId = string.Empty;
        [SerializeField] private string catalogChapterId = string.Empty;
        [SerializeField] private int catalogSortOrder;
        [SerializeField] private bool isInitiallyAvailable = true;

        public StageId StageId => stageId;

        public StageAuthoringDefinition AuthoringDefinition => authoringDefinition;

        public StageDefinition GameplayDefinition => gameplayDefinition;

        public StagePresentationDefinition PresentationDefinition => presentationDefinition;

        public StageAudioDefinition AudioDefinition => audioDefinition;

        public string CatalogWorldId => catalogWorldId ?? string.Empty;

        public string CatalogChapterId => catalogChapterId ?? string.Empty;

        public int CatalogSortOrder => catalogSortOrder;

        public bool IsInitiallyAvailable => isInitiallyAvailable;

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

        public void AssignCatalogMetadata(
            string worldId,
            string chapterId,
            int sortOrder,
            bool initiallyAvailable)
        {
            catalogWorldId = worldId ?? string.Empty;
            catalogChapterId = chapterId ?? string.Empty;
            catalogSortOrder = sortOrder;
            isInitiallyAvailable = initiallyAvailable;
        }
    }
}
