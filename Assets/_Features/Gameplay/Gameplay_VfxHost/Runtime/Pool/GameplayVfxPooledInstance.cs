using Game.Feature.Gameplay.Host;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    internal sealed class GameplayVfxPooledInstance
    {
        private readonly ParticleSystem[] particleSystems;
        private readonly Renderer[] renderers;
        private readonly Material[][] originalSharedMaterials;
        private readonly Material[][] runtimeMaterials;
        private readonly TrailRenderer[] trailRenderers;
        private readonly Transform tailRoot;
        private GameplayVfxPlaybackHandle handle;
        private bool hasRuntimeMaterials;

        public GameplayVfxPooledInstance(GameObject gameObject, Transform tailRoot)
        {
            GameObject = gameObject;
            Transform = gameObject.transform;
            this.tailRoot = tailRoot;
            particleSystems = gameObject.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            trailRenderers = gameObject.GetComponentsInChildren<TrailRenderer>(includeInactive: true);
            renderers = gameObject.GetComponentsInChildren<Renderer>(includeInactive: true);
            originalSharedMaterials = new Material[renderers.Length][];
            runtimeMaterials = new Material[renderers.Length][];
            for (var i = 0; i < renderers.Length; i++)
            {
                originalSharedMaterials[i] = renderers[i] != null
                    ? renderers[i].sharedMaterials
                    : System.Array.Empty<Material>();
                runtimeMaterials[i] = System.Array.Empty<Material>();
            }
        }

        public GameObject GameObject { get; }

        public Transform Transform { get; }

        public GameplayVfxPlaybackHandle Handle => handle;

        public int PrefabInstanceId { get; private set; }

        public void Activate(
            int prefabInstanceId,
            GameplayVfxPlaybackHandle playbackHandle,
            Transform parent,
            in VfxResolvedAnchor anchor)
        {
            PrefabInstanceId = prefabInstanceId;
            handle = playbackHandle;
            Transform.SetParent(parent, worldPositionStays: false);
            Transform.localPosition = anchor.HasLocalPose ? anchor.LocalPosition : Vector3.zero;
            Transform.localRotation = anchor.HasLocalPose ? anchor.LocalRotation : Quaternion.identity;
            Transform.localScale = Vector3.one;
            GameObject.SetActive(true);
            RestartParticles();
        }

        public void ActivateFlipDestroySelfMotion(
            int prefabInstanceId,
            GameplayVfxPlaybackHandle playbackHandle,
            Transform parent,
            in FlipDestroySelfMotionVfxCommand command)
        {
            PrefabInstanceId = prefabInstanceId;
            handle = playbackHandle;
            Transform.SetParent(parent, worldPositionStays: false);
            Transform.localPosition = command.SourceLocalPosition;
            Transform.localRotation = command.SourceLocalRotation;
            Transform.localScale = Vector3.one;
            GameObject.SetActive(true);
            EnsureRuntimeMaterials();
            ApplyFlipDestroySelfMotion(command, elapsedSeconds: 0f);
            RestartParticles();
        }

        public void AdvanceFlipDestroySelfMotion(
            in FlipDestroySelfMotionVfxCommand command,
            float elapsedSeconds)
        {
            ApplyFlipDestroySelfMotion(command, elapsedSeconds);
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
            ClearRuntimeMaterials();
            GameObject.SetActive(false);
            Transform.SetParent(poolRoot, worldPositionStays: false);
            Transform.localPosition = Vector3.zero;
            Transform.localRotation = Quaternion.identity;
            Transform.localScale = Vector3.one;
            handle = null;
        }

        public void HardCleanup()
        {
            ClearRuntimeMaterials();
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

        private void EnsureRuntimeMaterials()
        {
            ClearRuntimeMaterials();

            for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                var renderer = renderers[rendererIndex];
                if (renderer == null)
                {
                    runtimeMaterials[rendererIndex] = System.Array.Empty<Material>();
                    continue;
                }

                var sharedMaterials = renderer.sharedMaterials;
                var clonedMaterials = new Material[sharedMaterials.Length];
                for (var materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
                {
                    var sharedMaterial = sharedMaterials[materialIndex];
                    if (sharedMaterial != null)
                    {
                        clonedMaterials[materialIndex] = new Material(sharedMaterial);
                    }
                }

                renderer.sharedMaterials = clonedMaterials;
                runtimeMaterials[rendererIndex] = clonedMaterials;
            }

            hasRuntimeMaterials = true;
        }

        private void ClearRuntimeMaterials()
        {
            if (!hasRuntimeMaterials)
            {
                return;
            }

            for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                var renderer = renderers[rendererIndex];
                if (renderer != null)
                {
                    renderer.sharedMaterials = originalSharedMaterials[rendererIndex] ?? System.Array.Empty<Material>();
                }

                var materials = runtimeMaterials[rendererIndex];
                if (materials == null)
                {
                    continue;
                }

                for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    var material = materials[materialIndex];
                    if (material == null)
                    {
                        continue;
                    }

                    if (Application.isPlaying)
                    {
                        Object.Destroy(material);
                    }
                    else
                    {
                        Object.DestroyImmediate(material);
                    }
                }

                runtimeMaterials[rendererIndex] = System.Array.Empty<Material>();
            }

            hasRuntimeMaterials = false;
        }

        private void ApplyFlipDestroySelfMotion(
            in FlipDestroySelfMotionVfxCommand command,
            float elapsedSeconds)
        {
            var flightNormalizedTime = Mathf.Clamp01(elapsedSeconds / command.FlightDurationSeconds);
            var sampledPose = elapsedSeconds < command.FlightDurationSeconds
                ? FlipArcSampler.Sample(command.SourcePose, command.ImpactPose, flightNormalizedTime, command.ArcHeight)
                : command.ImpactPose;
            Transform.localPosition = sampledPose.Position;
            Transform.localRotation = sampledPose.Rotation;

            if (elapsedSeconds <= command.BreakStartSeconds)
            {
                Transform.localScale = Vector3.one;
                ApplyAlpha(1f);
                return;
            }

            var breakDurationSeconds = Mathf.Max(0.0001f, command.FadeDurationSeconds);
            var breakNormalizedTime = Mathf.Clamp01((elapsedSeconds - command.BreakStartSeconds) / breakDurationSeconds);
            var easedScaleTime = Mathf.Pow(breakNormalizedTime, 2f);
            var easedAlphaTime = Mathf.Pow(breakNormalizedTime, 2.5f);
            Transform.localScale = new Vector3(
                Mathf.Lerp(1f, 1.12f, easedScaleTime),
                Mathf.Lerp(1f, 1.12f, easedScaleTime),
                Mathf.Lerp(1f, 0.18f, easedScaleTime));
            ApplyAlpha(1f - easedAlphaTime);
        }

        private void ApplyAlpha(float alpha)
        {
            if (!hasRuntimeMaterials)
            {
                return;
            }

            for (var rendererIndex = 0; rendererIndex < runtimeMaterials.Length; rendererIndex++)
            {
                var materials = runtimeMaterials[rendererIndex];
                if (materials == null)
                {
                    continue;
                }

                for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    var material = materials[materialIndex];
                    if (material == null)
                    {
                        continue;
                    }

                    ConfigureTransparentMaterial(material);
                    if (material.HasProperty("_BaseColor"))
                    {
                        var color = material.GetColor("_BaseColor");
                        color.a = alpha;
                        material.SetColor("_BaseColor", color);
                    }

                    if (material.HasProperty("_Color"))
                    {
                        var color = material.color;
                        color.a = alpha;
                        material.color = color;
                    }
                }
            }
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}
