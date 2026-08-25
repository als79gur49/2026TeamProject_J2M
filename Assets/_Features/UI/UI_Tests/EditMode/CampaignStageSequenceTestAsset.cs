using System;
using System.Collections.Generic;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
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

        public static CampaignSlotLaunchEvaluator LoadProductionLaunchEvaluator()
        {
            return LoadProductionLaunchEvaluator(LoadProductionResolver());
        }

        public static CampaignSlotLaunchEvaluator LoadProductionLaunchEvaluator(
            CampaignStageSequenceResolver resolver)
        {
            var provider = AssetDatabase.LoadAssetAtPath<ScriptableObjectStageCatalogProvider>(
                StageContentPaths.StageCatalogProviderAssetPath);
            if (provider == null)
            {
                throw new InvalidOperationException(
                    $"The production stage catalog provider is required at '{StageContentPaths.StageCatalogProviderAssetPath}'.");
            }

            return new CampaignSlotLaunchEvaluator(
                resolver,
                provider);
        }

        public static MainMenuSlotPresentationInput[] BuildPresentationInputs(
            IReadOnlyList<CampaignSlotEntry> entries)
        {
            var evaluator = LoadProductionLaunchEvaluator();
            var inputs = new MainMenuSlotPresentationInput[entries.Count];
            for (var index = 0; index < entries.Count; index++)
            {
                var evaluation = evaluator.Evaluate(entries[index]);
                inputs[index] = new MainMenuSlotPresentationInput(
                    entries[index],
                    evaluation,
                    CampaignSlotActionPolicy.Evaluate(evaluation));
            }

            return inputs;
        }
    }
}
