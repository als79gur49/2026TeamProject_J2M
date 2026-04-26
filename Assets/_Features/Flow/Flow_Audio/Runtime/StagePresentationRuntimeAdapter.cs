using System;
using Game.Feature.Stages;
using UnityEngine;

namespace Game.Feature.Flow.Audio
{
    public sealed class StagePresentationRuntimeAdapter
    {
        private readonly IBgmFlowCoordinator _bgmFlowCoordinator;
        private GameObject _backgroundInstance;

        public StagePresentationRuntimeAdapter(IBgmFlowCoordinator bgmFlowCoordinator = null)
        {
            _bgmFlowCoordinator = bgmFlowCoordinator;
        }

        public GameObject CurrentBackgroundInstance => _backgroundInstance;

        public void Apply(
            StagePresentationDefinition presentationDefinition,
            Transform backgroundRoot,
            StageBgmProfileCatalog bgmProfileCatalog)
        {
            if (presentationDefinition == null)
            {
                return;
            }

            ApplyBackground(presentationDefinition.BackgroundPrefab, backgroundRoot);
            ApplyBgm(presentationDefinition, bgmProfileCatalog, _bgmFlowCoordinator);
        }

        public void Apply(
            StagePresentationDefinition presentationDefinition,
            Transform backgroundRoot,
            StageBgmProfileCatalog bgmProfileCatalog,
            GlobalAudioFlowBootstrap audioFlowBootstrap)
        {
            if (presentationDefinition == null)
            {
                return;
            }

            ApplyBackground(presentationDefinition.BackgroundPrefab, backgroundRoot);
            ApplyBgm(presentationDefinition, bgmProfileCatalog, audioFlowBootstrap?.Coordinator);
        }

        private void ApplyBackground(GameObject backgroundPrefab, Transform backgroundRoot)
        {
            ReplaceBackgroundInstance(backgroundPrefab, backgroundRoot);
        }

        private static void ApplyBgm(
            StagePresentationDefinition presentationDefinition,
            StageBgmProfileCatalog bgmProfileCatalog,
            IBgmFlowCoordinator coordinator)
        {
            var bgmReference = presentationDefinition.BgmReference;
            if (!bgmReference.HasValue)
            {
                return;
            }

            if (bgmProfileCatalog == null || !bgmProfileCatalog.TryResolve(bgmReference.BgmKey, out var profile))
            {
                Debug.LogWarning(
                    $"Stage BGM key '{bgmReference.BgmKey}' was not found in the stage BGM profile catalog.",
                    presentationDefinition);
                return;
            }

            coordinator?.RequestSceneDefault(profile);
        }

        private void ReplaceBackgroundInstance(GameObject backgroundPrefab, Transform backgroundRoot)
        {
            if (_backgroundInstance != null)
            {
                DestroyObject(_backgroundInstance);
                _backgroundInstance = null;
            }

            if (backgroundPrefab == null || backgroundRoot == null)
            {
                return;
            }

            _backgroundInstance = UnityEngine.Object.Instantiate(backgroundPrefab, backgroundRoot, false);
            _backgroundInstance.name = backgroundPrefab.name;
        }

        private static void DestroyObject(UnityEngine.Object value)
        {
            if (value == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(value);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(value);
            }
        }
    }
}
