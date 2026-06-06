using System;
using UnityEngine;

namespace Game.Feature.Stages
{
    public sealed class StageRuntimeContentResolver
    {
        public ResolvedStageContent Resolve(StageLoadRequest request)
        {
            if (request.StageCatalogProvider == null)
            {
                throw new InvalidOperationException(
                    $"{request.SceneName} requires a {nameof(ScriptableObjectStageCatalogProvider)} reference.");
            }

            var resolver = new StageCatalogResolver(request.StageCatalogProvider);
            if (StageLaunchContextStore.TryGetCurrent(out var launchStageId))
            {
                if (!resolver.TryResolve(launchStageId, out var launchEntry))
                {
                    Debug.LogError(
                        $"{request.SceneName} failed to resolve launch-context StageId '{launchStageId.Value}'.");
                    throw new InvalidOperationException(
                        $"{request.SceneName} failed to resolve launch-context StageId '{launchStageId.Value}'.");
                }

                CampaignChanceHudDiagnostics.Record(new CampaignChanceHudDiagnosticRecord(CampaignChanceHudDiagnosticKind.StageResolve)
                {
                    SceneName = request.SceneName,
                    LaunchStageId = launchStageId.Value,
                    ResolvedStageId = launchEntry.StageId.IsValid ? launchEntry.StageId.Value : string.Empty,
                    GameplayDefinitionName = launchEntry.GameplayDefinition != null ? launchEntry.GameplayDefinition.name : string.Empty,
                    PresentationDefinitionName = launchEntry.PresentationDefinition != null ? launchEntry.PresentationDefinition.name : string.Empty,
                });
                return new ResolvedStageContent(
                    launchStageId,
                    launchEntry,
                    usedLaunchContext: true);
            }

            throw new InvalidOperationException(BuildMissingLaunchContextMessage(request.SceneName));
        }

        private static string BuildMissingLaunchContextMessage(string sceneName)
        {
            var baseMessage =
                $"{sceneName} requires {nameof(StageLaunchContextStore)} to provide a canonical StageId before runtime bootstrap.";
#if UNITY_EDITOR
            return $"{baseMessage} Use Tools/Stages/Direct Play/Launch Stage... or a supported stage quick-launch before entering Play mode.";
#else
            return baseMessage;
#endif
        }
    }
}
