using System;
using Game.Feature.Stages;
using UnityEditor;

namespace Game.Feature.UI.Tests
{
    internal static class CampaignStageSequenceTestAsset
    {
        public static CampaignStageSequenceDefinition LoadProductionDefinition()
        {
            var definition = AssetDatabase.LoadAssetAtPath<CampaignStageSequenceDefinition>(
                StageContentPaths.CampaignStageSequenceAssetPath);
            return definition != null
                ? definition
                : throw new InvalidOperationException("The production campaign sequence asset is required by this integration test.");
        }

        public static CampaignStageSequenceResolver LoadProductionResolver()
        {
            return new CampaignStageSequenceResolver(LoadProductionDefinition());
        }
    }
}
