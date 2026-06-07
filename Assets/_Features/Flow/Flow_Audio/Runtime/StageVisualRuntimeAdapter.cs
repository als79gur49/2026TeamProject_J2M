using Game.Feature.Stages;
using UnityEngine;

namespace Game.Feature.Flow.Audio
{
    public sealed class StageVisualRuntimeAdapter
    {
        private GameObject _backgroundInstance;

        public GameObject CurrentBackgroundInstance => _backgroundInstance;

        public void Apply(
            StagePresentationDefinition presentationDefinition,
            Transform backgroundRoot)
        {
            if (presentationDefinition == null)
            {
                return;
            }

            ApplyBackground(presentationDefinition.BackgroundPrefab, backgroundRoot);
        }

        private void ApplyBackground(GameObject backgroundPrefab, Transform backgroundRoot)
        {
            ReplaceBackgroundInstance(backgroundPrefab, backgroundRoot);
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
