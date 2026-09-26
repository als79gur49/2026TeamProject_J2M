using System;
using UnityEngine;

namespace Game.Feature.Stages
{
    [CreateAssetMenu(menuName = "Game/Stages/Player Capture Campaign Content")]
    public sealed class PlayerCaptureCampaignContent : ScriptableObject
    {
        [SerializeField] private ScriptableObjectStageCatalogProvider stageCatalogProvider;
        [SerializeField] private CampaignStageSequenceDefinition stageSequence;

        public bool TryGetLevelGroupId(StageId stageId, out string levelGroupId, out string error)
        {
            levelGroupId = string.Empty;
            if (stageCatalogProvider == null || stageSequence == null)
            {
                error = "Player capture Campaign catalog or sequence is unavailable.";
                return false;
            }

            CampaignStageSequenceResolver sequence;
            try
            {
                sequence = new CampaignStageSequenceResolver(stageSequence);
            }
            catch (ArgumentException exception)
            {
                error = $"Player capture Campaign sequence is invalid: {exception.Message}";
                return false;
            }

            if (!sequence.Contains(stageId) ||
                !new StageCatalogResolver(stageCatalogProvider).TryResolve(stageId, out _))
            {
                error = $"Player capture stage '{stageId.Value}' is not in the Campaign catalog and sequence.";
                return false;
            }

            levelGroupId = sequence.GetLevelGroupId(stageId);
            if (string.IsNullOrWhiteSpace(levelGroupId))
            {
                error = $"Player capture stage '{stageId.Value}' has no Campaign level group.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
