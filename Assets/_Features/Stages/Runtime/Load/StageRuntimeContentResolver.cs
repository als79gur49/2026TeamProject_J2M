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
                        $"{request.SceneName} failed to resolve launch-context StageId '{launchStageId.Value}'. defaultStageId fallback not permitted when launch context is supplied.");
                    throw new InvalidOperationException(
                        $"{request.SceneName} failed to resolve launch-context StageId '{launchStageId.Value}'. defaultStageId fallback not permitted when launch context is supplied.");
                }

                return new ResolvedStageContent(
                    launchStageId,
                    launchEntry,
                    usedLaunchContext: true,
                    usedDefaultStageIdFallback: false);
            }

            if (request.FallbackPolicy == StageLoadFallbackPolicy.None)
            {
                throw new InvalidOperationException(
                    $"{request.SceneName} requires {nameof(StageLaunchContextStore)} to provide a StageId. defaultStageId fallback is not permitted for this runtime flow.");
            }

            if (request.FallbackPolicy != StageLoadFallbackPolicy.EditorDirectPlayOnly)
            {
                throw new InvalidOperationException(
                    $"{request.SceneName} declared unsupported fallback policy '{request.FallbackPolicy}'.");
            }

            if (!Application.isEditor)
            {
                throw new InvalidOperationException(
                    $"{request.SceneName} cannot use defaultStageId fallback outside the Unity editor direct-play flow.");
            }

            if (!request.DefaultStageId.IsValid)
            {
                throw new InvalidOperationException(
                    $"{request.SceneName} is missing a valid defaultStageId for editor/test fallback.");
            }

            if (!resolver.TryResolve(request.DefaultStageId, out var defaultEntry))
            {
                throw new InvalidOperationException(
                    $"{request.SceneName} failed to resolve fallback defaultStageId '{request.DefaultStageId.Value}'.");
            }

            Debug.LogWarning(
                $"{request.SceneName} bootstrapped without launch context. Falling back to editor direct-play defaultStageId '{request.DefaultStageId.Value}'.");

            return new ResolvedStageContent(
                request.DefaultStageId,
                defaultEntry,
                usedLaunchContext: false,
                usedDefaultStageIdFallback: true);
        }
    }
}
