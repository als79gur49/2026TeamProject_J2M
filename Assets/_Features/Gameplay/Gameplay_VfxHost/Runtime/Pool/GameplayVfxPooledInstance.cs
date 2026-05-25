using System;
using Game.Feature.Gameplay.Host;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    internal sealed class GameplayVfxPooledInstance
    {
        private readonly ParticleSystem[] particleSystems;
        private readonly Renderer[] prefabRenderers;
        private readonly bool[] prefabRendererEnabled;
        private readonly Transform tailRoot;
        private readonly TrailRenderer[] trailRenderers;
        private GameObject sourceCloneObject;
        private GameplayVfxPlaybackHandle handle;
        private VfxRendererMaterialInstanceSet activeMaterialInstances;
        private bool[] suspendedRendererEnabled;
        private bool isPresentationSuspended;
        private bool usingSourceClone;

        public GameplayVfxPooledInstance(GameObject gameObject, Transform tailRoot)
        {
            GameObject = gameObject;
            Transform = gameObject.transform;
            this.tailRoot = tailRoot;
            particleSystems = gameObject.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            trailRenderers = gameObject.GetComponentsInChildren<TrailRenderer>(includeInactive: true);
            prefabRenderers = gameObject.GetComponentsInChildren<Renderer>(includeInactive: true);
            prefabRendererEnabled = new bool[prefabRenderers.Length];
            for (var i = 0; i < prefabRenderers.Length; i++)
            {
                prefabRendererEnabled[i] = prefabRenderers[i] != null && prefabRenderers[i].enabled;
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
            isPresentationSuspended = false;
            RestorePrefabVisuals();
            Transform.SetParent(parent, worldPositionStays: false);
            Transform.localPosition = anchor.HasLocalPose ? anchor.LocalPosition : Vector3.zero;
            Transform.localRotation = anchor.HasLocalPose ? anchor.LocalRotation : Quaternion.identity;
            Transform.localScale = Vector3.one;
            TraceIfEntrance(nameof(Activate), "BeforeSetActiveTrue", includeStackTrace: false);
            GameObject.SetActive(true);
            RestartParticles();
            TraceIfEntrance(nameof(Activate), "AfterRestartParticles", includeStackTrace: false);
        }

        public void Reanchor(in VfxResolvedAnchor anchor)
        {
            Transform.localPosition = anchor.HasLocalPose ? anchor.LocalPosition : Vector3.zero;
            Transform.localRotation = anchor.HasLocalPose ? anchor.LocalRotation : Quaternion.identity;
        }

        public void ActivateParameterizedMotion(
            int prefabInstanceId,
            GameplayVfxPlaybackHandle playbackHandle,
            Transform parent,
            in ParameterizedMotionVfxCommand command,
            IGameplayVfxCloneSourceProvider cloneSourceProvider)
        {
            PrefabInstanceId = prefabInstanceId;
            handle = playbackHandle;
            isPresentationSuspended = false;
            ClearParameterizedVisuals();
            RestorePrefabVisuals();
            Transform.SetParent(parent, worldPositionStays: false);
            Transform.localPosition = command.SourceLocalPosition;
            Transform.localRotation = command.SourceLocalRotation;
            Transform.localScale = Vector3.one;
            TraceIfEntrance(nameof(ActivateParameterizedMotion), "BeforeSetActiveTrue", includeStackTrace: false);
            GameObject.SetActive(true);
            ConfigureParameterizedVisuals(command, cloneSourceProvider);
            ApplyParameterizedMotion(command, elapsedSeconds: 0f);
            if (!usingSourceClone)
            {
                RestartParticles();
            }
            TraceIfEntrance(nameof(ActivateParameterizedMotion), "AfterRestartParticles", includeStackTrace: false);
        }

        public void ActivateFlipDestroySelfMotion(
            int prefabInstanceId,
            GameplayVfxPlaybackHandle playbackHandle,
            Transform parent,
            in FlipDestroySelfMotionVfxCommand command)
        {
            ActivateParameterizedMotion(
                prefabInstanceId,
                playbackHandle,
                parent,
                command.ToParameterizedMotionVfxCommand(),
                cloneSourceProvider: null);
        }

        public void AdvanceParameterizedMotion(
            in ParameterizedMotionVfxCommand command,
            float elapsedSeconds)
        {
            ApplyParameterizedMotion(command, elapsedSeconds);
        }

        public void AdvanceFlipDestroySelfMotion(
            in FlipDestroySelfMotionVfxCommand command,
            float elapsedSeconds)
        {
            ApplyParameterizedMotion(command.ToParameterizedMotionVfxCommand(), elapsedSeconds);
        }

        public void StopEmitting()
        {
            TraceIfEntrance(nameof(StopEmitting), "BeforeStopEmitting", includeStackTrace: true);
            for (var i = 0; i < particleSystems.Length; i++)
            {
                var particleSystem = particleSystems[i];
                if (particleSystem != null)
                {
                    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
            TraceIfEntrance(nameof(StopEmitting), "AfterStopEmitting", includeStackTrace: false);
        }

        public void StopEmittingAndClear()
        {
            TraceIfEntrance(nameof(StopEmittingAndClear), "BeforeStopEmittingAndClear", includeStackTrace: true);
            for (var i = 0; i < particleSystems.Length; i++)
            {
                var particleSystem = particleSystems[i];
                if (particleSystem != null)
                {
                    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    particleSystem.Clear(true);
                }
            }

            ClearTrails();
            TraceIfEntrance(nameof(StopEmittingAndClear), "AfterStopEmittingAndClear", includeStackTrace: false);
        }

        public void DetachToTailRoot()
        {
            Transform.SetParent(tailRoot, worldPositionStays: true);
        }

        public void SuspendPresentation()
        {
            if (isPresentationSuspended)
            {
                return;
            }

            if (suspendedRendererEnabled == null ||
                suspendedRendererEnabled.Length != prefabRenderers.Length)
            {
                suspendedRendererEnabled = new bool[prefabRenderers.Length];
            }

            for (var i = 0; i < prefabRenderers.Length; i++)
            {
                var renderer = prefabRenderers[i];
                suspendedRendererEnabled[i] = renderer != null && renderer.enabled;
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }

            for (var i = 0; i < particleSystems.Length; i++)
            {
                particleSystems[i]?.Pause(true);
            }

            isPresentationSuspended = true;
        }

        public void ResumePresentation()
        {
            if (!isPresentationSuspended)
            {
                return;
            }

            for (var i = 0; i < prefabRenderers.Length; i++)
            {
                var renderer = prefabRenderers[i];
                if (renderer != null)
                {
                    renderer.enabled = suspendedRendererEnabled != null &&
                                       i < suspendedRendererEnabled.Length &&
                                       suspendedRendererEnabled[i];
                }
            }

            for (var i = 0; i < particleSystems.Length; i++)
            {
                particleSystems[i]?.Play(true);
            }

            isPresentationSuspended = false;
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
            TraceIfEntrance(nameof(DeactivateForPool), "Entry", includeStackTrace: true);
            StopEmittingAndClear();
            ClearTrails();
            ClearParameterizedVisuals();
            RestorePrefabVisuals();
            isPresentationSuspended = false;
            TraceIfEntrance(nameof(DeactivateForPool), "BeforeSetActiveFalse", includeStackTrace: true);
            GameObject.SetActive(false);
            Transform.SetParent(poolRoot, worldPositionStays: false);
            Transform.localPosition = Vector3.zero;
            Transform.localRotation = Quaternion.identity;
            Transform.localScale = Vector3.one;
            TraceIfEntrance(nameof(DeactivateForPool), "AfterSetActiveFalse", includeStackTrace: false);
            handle = null;
        }

        public void HardCleanup()
        {
            TraceIfEntrance(nameof(HardCleanup), "Entry", includeStackTrace: true);
            ClearParameterizedVisuals();
            if (GameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(GameObject);
            }

            handle = null;
        }

        private void ConfigureParameterizedVisuals(
            in ParameterizedMotionVfxCommand command,
            IGameplayVfxCloneSourceProvider cloneSourceProvider)
        {
            if (TryCreateSourceClone(command, cloneSourceProvider, out var cloneRenderers))
            {
                HidePrefabVisuals();
                activeMaterialInstances = VfxRendererMaterialInstanceSet.Create(cloneRenderers);
                usingSourceClone = true;
                return;
            }

            if (command.CloneMode == ParameterizedMotionVfxCloneMode.SourceCloneMotion)
            {
                HidePrefabVisuals();
                activeMaterialInstances = VfxRendererMaterialInstanceSet.Create(Array.Empty<Renderer>());
                usingSourceClone = true;
                return;
            }

            activeMaterialInstances = VfxRendererMaterialInstanceSet.Create(prefabRenderers);
            usingSourceClone = false;
        }

        private bool TryCreateSourceClone(
            in ParameterizedMotionVfxCommand command,
            IGameplayVfxCloneSourceProvider cloneSourceProvider,
            out Renderer[] cloneRenderers)
        {
            cloneRenderers = Array.Empty<Renderer>();
            if (command.CloneMode == ParameterizedMotionVfxCloneMode.PrefabOnly ||
                cloneSourceProvider == null ||
                !cloneSourceProvider.TryResolveCloneSource(command.SourceEntityId, out var source) ||
                source.ModelRoot == null)
            {
                return false;
            }

            sourceCloneObject = UnityEngine.Object.Instantiate(source.ModelRoot.gameObject, Transform, worldPositionStays: false);
            sourceCloneObject.name = "ParameterizedMotionCloneRoot";
            sourceCloneObject.transform.localPosition = source.ModelRoot.localPosition;
            sourceCloneObject.transform.localRotation = source.ModelRoot.localRotation;
            sourceCloneObject.transform.localScale = source.LocalScale;
            RemoveGameplayAffectingComponents(sourceCloneObject);
            sourceCloneObject.SetActive(true);
            cloneRenderers = sourceCloneObject.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (cloneRenderers.Length == 0)
            {
                SafeDestroy(sourceCloneObject);
                sourceCloneObject = null;
                return false;
            }

            return true;
        }

        private void ClearParameterizedVisuals()
        {
            activeMaterialInstances?.Clear();
            activeMaterialInstances = null;
            if (sourceCloneObject != null)
            {
                SafeDestroy(sourceCloneObject);
                sourceCloneObject = null;
            }

            usingSourceClone = false;
        }

        private void HidePrefabVisuals()
        {
            for (var i = 0; i < prefabRenderers.Length; i++)
            {
                if (prefabRenderers[i] != null)
                {
                    prefabRenderers[i].enabled = false;
                }
            }

            StopEmitting();
        }

        private void RestorePrefabVisuals()
        {
            for (var i = 0; i < prefabRenderers.Length; i++)
            {
                if (prefabRenderers[i] != null)
                {
                    prefabRenderers[i].enabled = prefabRendererEnabled[i];
                }
            }
        }

        private void RestartParticles()
        {
            TraceIfEntrance(nameof(RestartParticles), "BeforeRestartParticles", includeStackTrace: false);
            for (var i = 0; i < particleSystems.Length; i++)
            {
                var particleSystem = particleSystems[i];
                if (particleSystem != null)
                {
                    particleSystem.Clear(true);
                    particleSystem.Play(true);
                }
            }
            TraceIfEntrance(nameof(RestartParticles), "AfterRestartParticles", includeStackTrace: false);
        }

        private void TraceIfEntrance(string method, string reason, bool includeStackTrace)
        {
            if (handle == null || !GameplayVfxLifetimeUnityTrace.IsEntranceSpawn(handle.CueId))
            {
                return;
            }

            var age = Time.time - handle.StartedAtSeconds;
            GameplayVfxLifetimeUnityTrace.Log(
                method,
                reason,
                $"handleId={handle.HandleId} cueFamily={handle.CueId.Family} cueCode={handle.CueId.Code} cueName={GameplayVfxLifetimeUnityTrace.DescribeCueName(handle.CueId)} state={handle.State} createdAt={handle.StartedAtSeconds:F3} age={age:F3} stopPolicy={handle.Policy.StopPolicy} defaultLifetimeSeconds={handle.Policy.DefaultLifetimeSeconds:F3} tailSeconds={handle.Policy.TailSeconds:F3} effectiveLifetimeSeconds={(handle.Policy.DefaultLifetimeSeconds + handle.Policy.TailSeconds):F3} {GameplayVfxLifetimeUnityTrace.DescribeGameObject(GameObject)} {GameplayVfxLifetimeUnityTrace.DescribeParticles(GameObject)}",
                GameObject,
                includeStackTrace);
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

        private void ApplyParameterizedMotion(
            in ParameterizedMotionVfxCommand command,
            float elapsedSeconds)
        {
            var sample = ParameterizedMotionVfxSampler.Sample(command, elapsedSeconds);
            Transform.localPosition = sample.LocalPosition;
            Transform.localRotation = sample.LocalRotation;

            if (command.FadeMode == ParameterizedMotionVfxFadeMode.LegacyEnemyDeath)
            {
                Transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.88f, sample.NormalizedTime);
                activeMaterialInstances?.ApplyAlpha(1f - (sample.FadeProgress * sample.FadeProgress));
                return;
            }

            if (command.FadeMode == ParameterizedMotionVfxFadeMode.DestroyShrinkEase)
            {
                ApplyDestroyShrinkFadeState(sample.NormalizedTime, sample.FadeProgress);
                return;
            }

            if (sample.FadeProgress <= 0f)
            {
                ApplyFadeState(command, scaleProgress: 0f, alpha: 1f);
                return;
            }

            ApplyFadeState(
                command,
                Mathf.Pow(sample.FadeProgress, 2f),
                1f - Mathf.Pow(sample.FadeProgress, 2.5f));
        }

        private void ApplyFadeState(
            in ParameterizedMotionVfxCommand command,
            float scaleProgress,
            float alpha)
        {
            if (command.FadeMode == ParameterizedMotionVfxFadeMode.ScaleAndAlpha ||
                command.FadeMode == ParameterizedMotionVfxFadeMode.ScaleOnly)
            {
                Transform.localScale = new Vector3(
                    Mathf.Lerp(1f, 1.12f, scaleProgress),
                    Mathf.Lerp(1f, 1.12f, scaleProgress),
                    Mathf.Lerp(1f, 0.18f, scaleProgress));
            }
            else
            {
                Transform.localScale = Vector3.one;
            }

            if (command.FadeMode == ParameterizedMotionVfxFadeMode.ScaleAndAlpha ||
                command.FadeMode == ParameterizedMotionVfxFadeMode.AlphaOnly)
            {
                activeMaterialInstances?.ApplyAlpha(alpha);
            }
        }

        private void ApplyDestroyShrinkFadeState(float normalizedTime, float fadeProgress)
        {
            const float popEnd = 0.16f;
            const float popScale = 1.10f;
            const float finalScale = 0.06f;
            var time = Mathf.Clamp01(normalizedTime);
            float scale;
            if (time <= popEnd)
            {
                var popT = Mathf.Clamp01(time / popEnd);
                var easedPop = 1f - Mathf.Pow(1f - popT, 3f);
                scale = Mathf.Lerp(1f, popScale, easedPop);
            }
            else
            {
                var shrinkT = Mathf.Clamp01((time - popEnd) / (1f - popEnd));
                var easedShrink = 1f - Mathf.Pow(1f - shrinkT, 3f);
                scale = Mathf.Lerp(popScale, finalScale, easedShrink);
            }

            Transform.localScale = Vector3.one * Mathf.Max(0f, scale);

            var alphaT = Mathf.Clamp01((fadeProgress - 0.18f) / 0.82f);
            var alphaEase = alphaT * alphaT * (3f - (2f * alphaT));
            activeMaterialInstances?.ApplyAlpha(1f - alphaEase);
        }

        private static void RemoveGameplayAffectingComponents(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            DestroyComponents(root.GetComponentsInChildren<Collider>(includeInactive: true));
            DestroyComponents(root.GetComponentsInChildren<Rigidbody>(includeInactive: true));
            DestroyComponents(root.GetComponentsInChildren<AudioSource>(includeInactive: true));
            var components = root.GetComponentsInChildren<Component>(includeInactive: true);
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component != null && component.GetType().Name == "NavMeshAgent")
                {
                    SafeDestroy(component);
                }
            }
        }

        private static void DestroyComponents(Component[] components)
        {
            for (var i = 0; i < components.Length; i++)
            {
                SafeDestroy(components[i]);
            }
        }

        private static void SafeDestroy(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
