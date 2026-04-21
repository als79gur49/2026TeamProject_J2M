using System;
using Game.Feature.Gameplay.Host;
using UnityEngine;

namespace Game.Feature.Stages
{
    public interface IStageLoadStrategy
    {
        ResolvedStageContent Resolve(StageLoadRequest request);
    }

    public sealed class CatalogResolvedStageIdStrategy : IStageLoadStrategy
    {
        public ResolvedStageContent Resolve(StageLoadRequest request)
        {
            if (request.StageCatalogProvider == null)
            {
                throw new InvalidOperationException(
                    $"{request.SceneName} requires a {nameof(ScriptableObjectStageCatalogProvider)} reference when using {nameof(StageLoadSourceMode.CatalogResolvedStageId)}.");
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
                    StageLoadSourceMode.CatalogResolvedStageId,
                    launchStageId,
                    launchEntry,
                    usedLaunchContext: true,
                    usedDefaultStageIdFallback: false);
            }

            if (!request.AllowDefaultStageIdFallback)
            {
                throw new InvalidOperationException(
                    $"{request.SceneName} requires {nameof(StageLaunchContextStore)} to provide a StageId. defaultStageId fallback is not permitted for this runtime flow.");
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
                $"{request.SceneName} bootstrapped without launch context. Falling back to defaultStageId '{request.DefaultStageId.Value}'.");

            return new ResolvedStageContent(
                StageLoadSourceMode.CatalogResolvedStageId,
                request.DefaultStageId,
                defaultEntry,
                usedLaunchContext: false,
                usedDefaultStageIdFallback: true);
        }
    }

    public sealed class SerializedStageContentEntryStrategy : IStageLoadStrategy
    {
        public ResolvedStageContent Resolve(StageLoadRequest request)
        {
            ThrowIfCompatModeUsedInPlayer(request);

            if (request.SerializedStageContentEntry == null)
            {
                throw new InvalidOperationException(
                    $"{request.SceneName} requires a serialized {nameof(StageContentEntry)} when using {nameof(StageLoadSourceMode.SerializedStageContentEntry)}.");
            }

            Debug.LogWarning(
                $"{request.SceneName} is using compat load mode {nameof(StageLoadSourceMode.SerializedStageContentEntry)}. Production scenes must use {nameof(StageLoadSourceMode.CatalogResolvedStageId)}.");

            return new ResolvedStageContent(
                StageLoadSourceMode.SerializedStageContentEntry,
                request.SerializedStageContentEntry.StageId,
                request.SerializedStageContentEntry,
                usedLaunchContext: false,
                usedDefaultStageIdFallback: false);
        }

        private static void ThrowIfCompatModeUsedInPlayer(StageLoadRequest request)
        {
            if (!request.IsPlayerRuntime)
            {
                return;
            }

            throw new InvalidOperationException(
                $"{request.SceneName} cannot use {nameof(StageLoadSourceMode.SerializedStageContentEntry)} in player/runtime. Production scenes must use {nameof(StageLoadSourceMode.CatalogResolvedStageId)}.");
        }
    }

    public sealed class LegacyStageDefinitionStrategy : IStageLoadStrategy
    {
        public ResolvedStageContent Resolve(StageLoadRequest request)
        {
            ThrowIfCompatModeUsedInPlayer(request);

            if (request.LegacyStageDefinition == null)
            {
                throw new InvalidOperationException(
                    $"{request.SceneName} requires a serialized {nameof(StageDefinition)} when using {nameof(StageLoadSourceMode.LegacyStageDefinition)}.");
            }

            var stageId = ResolveCompatStageId(request);
            var entry = ScriptableObject.CreateInstance<StageContentEntry>();
            entry.hideFlags = HideFlags.HideAndDontSave;
            entry.name = $"{stageId.Value}-legacy-entry";
            entry.AssignStageId(stageId);
            entry.AssignGameplayDefinition(request.LegacyStageDefinition);
            entry.AssignPresentationDefinition(BuildTransientPresentationDefinition(request, entry, stageId));

            Debug.LogWarning(
                $"{request.SceneName} is using compat load mode {nameof(StageLoadSourceMode.LegacyStageDefinition)}. Production scenes must use {nameof(StageLoadSourceMode.CatalogResolvedStageId)}.");

            return new ResolvedStageContent(
                StageLoadSourceMode.LegacyStageDefinition,
                stageId,
                entry,
                usedLaunchContext: false,
                usedDefaultStageIdFallback: false);
        }

        private static StageId ResolveCompatStageId(StageLoadRequest request)
        {
            if (request.DefaultStageId.IsValid)
            {
                return request.DefaultStageId;
            }

            if (StageId.TryCreate(request.LegacyStageDefinition.name, out var stageId))
            {
                return stageId;
            }

            throw new InvalidOperationException(
                $"{request.SceneName} could not derive a canonical StageId from legacy StageDefinition '{request.LegacyStageDefinition.name}'.");
        }

        private static StagePresentationDefinition BuildTransientPresentationDefinition(
            StageLoadRequest request,
            StageContentEntry ownerEntry,
            StageId stageId)
        {
            var resolvedPresentation = StagePresentationAssembler.ResolveLegacy(
                request.LegacyStageDefinition,
                request.LegacyEnemyPresentationCatalog,
                request.LegacyStaticEntityPresentationCatalog);

            var presentationDefinition = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            presentationDefinition.hideFlags = HideFlags.HideAndDontSave;
            presentationDefinition.name = $"{stageId.Value}-legacy-presentation";
            presentationDefinition.SetOwnerMetadata(ownerEntry, string.Empty);
            presentationDefinition.ApplyResolvedData(resolvedPresentation);
            return presentationDefinition;
        }

        private static void ThrowIfCompatModeUsedInPlayer(StageLoadRequest request)
        {
            if (!request.IsPlayerRuntime)
            {
                return;
            }

            throw new InvalidOperationException(
                $"{request.SceneName} cannot use {nameof(StageLoadSourceMode.LegacyStageDefinition)} in player/runtime. Production scenes must use {nameof(StageLoadSourceMode.CatalogResolvedStageId)}.");
        }
    }
}
