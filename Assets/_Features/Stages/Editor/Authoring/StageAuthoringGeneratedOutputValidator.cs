using System;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    internal static class StageAuthoringGeneratedOutputValidator
    {
        public static void ValidateGeneratedGameplay(
            StageAuthoringBuildData buildData,
            StageAuthoringGenerationReport report)
        {
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            try
            {
                stage.name = "StageAuthoring_GeneratedPreview";
                StageAuthoringGeneratedAssetWriter.ApplyGameplayOutput(stage, buildData, recordUndo: false, markDirty: false);
                StageDefinitionValidator.Validate(stage);
                StageRuntimeBuilder.Build(stage);
            }
            catch (Exception exception)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "authoring.generated-gameplay.invalid",
                    $"Generated gameplay StageDefinition is invalid: {exception.Message}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        public static void ValidateGeneratedPresentation(
            StagePresentationDefinition presentationOutput,
            StageAuthoringBuildData buildData,
            StageAuthoringGenerationReport report)
        {
            if (presentationOutput == null)
            {
                return;
            }

            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            try
            {
                presentation.name = "StageAuthoring_PresentationPreview";
                presentation.ApplyResolvedData(StagePresentationAssembler.Resolve(presentationOutput));
                StageAuthoringGeneratedAssetWriter.ApplyPresentationOutput(presentation, buildData, recordUndo: false, markDirty: false);
                StagePresentationAssembler.Resolve(presentation);
            }
            catch (Exception exception)
            {
                report.Add(
                    StageValidationSeverity.Error,
                    "authoring.generated-presentation.invalid",
                    $"Generated presentation data is invalid: {exception.Message}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(presentation);
            }
        }
    }
}
