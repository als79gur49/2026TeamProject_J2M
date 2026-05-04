using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    internal sealed class GameplayVfxPooledInstance
    {
        private readonly ParticleSystem[] particleSystems;
        private readonly TrailRenderer[] trailRenderers;
        private readonly Transform tailRoot;
        private GameplayVfxPlaybackHandle handle;

        public GameplayVfxPooledInstance(GameObject gameObject, Transform tailRoot)
        {
            GameObject = gameObject;
            Transform = gameObject.transform;
            this.tailRoot = tailRoot;
            particleSystems = gameObject.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            trailRenderers = gameObject.GetComponentsInChildren<TrailRenderer>(includeInactive: true);
        }

        public GameObject GameObject { get; }

        public Transform Transform { get; }

        public GameplayVfxPlaybackHandle Handle => handle;

        public int PrefabInstanceId { get; private set; }

        public void Activate(
            int prefabInstanceId,
            GameplayVfxPlaybackHandle playbackHandle,
            Transform parent)
        {
            PrefabInstanceId = prefabInstanceId;
            handle = playbackHandle;
            Transform.SetParent(parent, worldPositionStays: false);
            Transform.localPosition = Vector3.zero;
            Transform.localRotation = Quaternion.identity;
            Transform.localScale = Vector3.one;
            GameObject.SetActive(true);
            RestartParticles();
        }

        public void StopEmitting()
        {
            for (var i = 0; i < particleSystems.Length; i++)
            {
                var particleSystem = particleSystems[i];
                if (particleSystem != null)
                {
                    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        public void DetachToTailRoot()
        {
            Transform.SetParent(tailRoot, worldPositionStays: true);
        }

        public bool IsTailComplete(float nowSeconds)
        {
            if (handle == null || !handle.HasTailStarted)
            {
                return false;
            }

            var tailSeconds = handle.Policy.TailSeconds;
            return tailSeconds <= 0f || nowSeconds - handle.TailStartedAtSeconds >= tailSeconds;
        }

        public void DeactivateForPool(Transform poolRoot)
        {
            StopEmitting();
            ClearTrails();
            GameObject.SetActive(false);
            Transform.SetParent(poolRoot, worldPositionStays: false);
            Transform.localPosition = Vector3.zero;
            Transform.localRotation = Quaternion.identity;
            Transform.localScale = Vector3.one;
            handle = null;
        }

        public void HardCleanup()
        {
            if (GameObject != null)
            {
                Object.DestroyImmediate(GameObject);
            }

            handle = null;
        }

        private void RestartParticles()
        {
            for (var i = 0; i < particleSystems.Length; i++)
            {
                var particleSystem = particleSystems[i];
                if (particleSystem != null)
                {
                    particleSystem.Clear(true);
                    particleSystem.Play(true);
                }
            }
        }

        private void ClearTrails()
        {
            for (var i = 0; i < trailRenderers.Length; i++)
            {
                var trailRenderer = trailRenderers[i];
                if (trailRenderer != null)
                {
                    trailRenderer.Clear();
                }
            }
        }
    }
}
