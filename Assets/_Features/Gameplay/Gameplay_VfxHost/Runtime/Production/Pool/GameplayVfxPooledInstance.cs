using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    internal sealed class GameplayVfxPooledInstance
    {
        private readonly ParticleSystem[] particleSystems;
        private readonly Renderer[] prefabRenderers;
        private readonly bool[] prefabRendererEnabled;
        private readonly Transform sourceCloneStagingRoot;
        private readonly Transform tailRoot;
        private readonly TrailRenderer[] trailRenderers;
        private GameObject sourceCloneObject;
        private GameplayVfxPlaybackHandle handle;
        private VfxRendererMaterialInstanceSet activeMaterialInstances;
        private SuspendedParticleSnapshot[] suspendedParticleSnapshots;
        private bool[] suspendedRendererEnabled;
        private VfxPresentationSuspendReason presentationSuspendReasons;
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

            var stagingObject = new GameObject("DeathMotionCloneStagingRoot");
            stagingObject.SetActive(false);
            sourceCloneStagingRoot = stagingObject.transform;
            sourceCloneStagingRoot.SetParent(Transform, worldPositionStays: false);
            suspendedParticleSnapshots = new SuspendedParticleSnapshot[particleSystems.Length];
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
            presentationSuspendReasons = VfxPresentationSuspendReason.None;
            ClearSuspendedParticleSnapshot();
            RestorePrefabVisuals();
            Transform.SetParent(parent, worldPositionStays: false);
            Transform.localPosition = anchor.HasLocalPose ? anchor.LocalPosition : Vector3.zero;
            Transform.localRotation = anchor.HasLocalPose ? anchor.LocalRotation : Quaternion.identity;
            Transform.localScale = Vector3.one;
            GameObject.SetActive(true);
            RestartParticles();
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
            ActivateParameterizedMotion(
                prefabInstanceId,
                playbackHandle,
                parent,
                command,
                cloneSourceProvider,
                VfxRendererInactiveVisualSnapshotSet.Empty,
                VfxSourceHierarchyPoseCapture.Unattempted);
        }

        public void ActivateParameterizedMotion(
            int prefabInstanceId,
            GameplayVfxPlaybackHandle playbackHandle,
            Transform parent,
            in ParameterizedMotionVfxCommand command,
            IGameplayVfxCloneSourceProvider cloneSourceProvider,
            in VfxRendererInactiveVisualSnapshotSet sourceVisualSnapshot,
            in VfxSourceHierarchyPoseCapture sourcePoseCapture)
        {
            PrefabInstanceId = prefabInstanceId;
            handle = playbackHandle;
            presentationSuspendReasons = VfxPresentationSuspendReason.None;
            ClearSuspendedParticleSnapshot();
            ClearParameterizedVisuals();
            RestorePrefabVisuals();
            Transform.SetParent(parent, worldPositionStays: false);
            Transform.localPosition = command.SourceLocalPosition;
            Transform.localRotation = command.SourceLocalRotation;
            Transform.localScale = Vector3.one;
            GameObject.SetActive(true);
            ConfigureParameterizedVisuals(
                command,
                cloneSourceProvider,
                sourceVisualSnapshot,
                sourcePoseCapture);
            ApplyParameterizedMotion(command, elapsedSeconds: 0f);
            if (!usingSourceClone)
            {
                RestartParticles();
            }
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
            for (var i = 0; i < particleSystems.Length; i++)
            {
                var particleSystem = particleSystems[i];
                if (particleSystem != null)
                {
                    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        public void StopEmittingAndClear()
        {
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
        }

        public void DetachToTailRoot()
        {
            Transform.SetParent(tailRoot, worldPositionStays: true);
        }

        public void SuspendPresentation()
        {
            SuspendPresentation(VfxPresentationSuspendReason.Visibility);
        }

        public void SuspendPresentation(VfxPresentationSuspendReason reason)
        {
            if (reason == VfxPresentationSuspendReason.None ||
                presentationSuspendReasons.HasFlag(reason))
            {
                return;
            }

            var previousReasons = presentationSuspendReasons;
            presentationSuspendReasons |= reason;
            ApplyPresentationSuspendState(previousReasons);
        }

        public void ResumePresentation()
        {
            ResumePresentation(VfxPresentationSuspendReason.Visibility);
        }

        public void ResumePresentation(VfxPresentationSuspendReason reason)
        {
            if (reason == VfxPresentationSuspendReason.None ||
                !presentationSuspendReasons.HasFlag(reason))
            {
                return;
            }

            var previousReasons = presentationSuspendReasons;
            presentationSuspendReasons &= ~reason;
            ApplyPresentationSuspendState(previousReasons);
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
            StopEmittingAndClear();
            ClearTrails();
            ClearParameterizedVisuals();
            RestorePrefabVisuals();
            presentationSuspendReasons = VfxPresentationSuspendReason.None;
            ClearSuspendedParticleSnapshot();
            GameObject.SetActive(false);
            Transform.SetParent(poolRoot, worldPositionStays: false);
            Transform.localPosition = Vector3.zero;
            Transform.localRotation = Quaternion.identity;
            Transform.localScale = Vector3.one;
            handle = null;
        }

        public void HardCleanup()
        {
            ClearParameterizedVisuals();
            if (GameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(GameObject);
            }

            handle = null;
        }

        private void ApplyPresentationSuspendState(VfxPresentationSuspendReason previousReasons)
        {
            var wasPaused = previousReasons != VfxPresentationSuspendReason.None;
            var isPaused = presentationSuspendReasons != VfxPresentationSuspendReason.None;
            var wasHidden = ShouldHideForSuspend(previousReasons);
            var isHidden = ShouldHideForSuspend(presentationSuspendReasons);

            if (!wasHidden && isHidden)
            {
                HideRenderersForSuspend();
            }
            else if (wasHidden && !isHidden)
            {
                RestoreSuspendedRendererState();
            }

            if (!wasPaused && isPaused)
            {
                PauseParticles();
            }
            else if (wasPaused && !isPaused)
            {
                ResumeParticles();
            }
        }

        private void HideRenderersForSuspend()
        {
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
        }

        private void RestoreSuspendedRendererState()
        {
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
        }

        private void PauseParticles()
        {
            for (var i = 0; i < particleSystems.Length; i++)
            {
                var particleSystem = particleSystems[i];
                if (particleSystem == null)
                {
                    if (i < suspendedParticleSnapshots.Length)
                    {
                        suspendedParticleSnapshots[i] = default;
                    }

                    continue;
                }

                if (i < suspendedParticleSnapshots.Length)
                {
                    var wasPlaying = particleSystem.isPlaying;
                    var wasEmitting = particleSystem.isEmitting;
                    suspendedParticleSnapshots[i] = new SuspendedParticleSnapshot(
                        wasPlaying,
                        wasEmitting,
                        particleSystem.isPaused,
                        particleSystem.particleCount);
                }

                particleSystem.Pause(true);
            }
        }

        private void ResumeParticles()
        {
            for (var i = 0; i < particleSystems.Length; i++)
            {
                var particleSystem = particleSystems[i];
                if (particleSystem == null)
                {
                    continue;
                }

                var snapshot = i < suspendedParticleSnapshots.Length
                    ? suspendedParticleSnapshots[i]
                    : default;
                if (snapshot.ShouldResumePlayback)
                {
                    particleSystem.Play(true);
                }
            }

            ClearSuspendedParticleSnapshot();
        }

        private void ClearSuspendedParticleSnapshot()
        {
            if (suspendedParticleSnapshots == null ||
                suspendedParticleSnapshots.Length != particleSystems.Length)
            {
                suspendedParticleSnapshots = new SuspendedParticleSnapshot[particleSystems.Length];
                return;
            }

            Array.Clear(suspendedParticleSnapshots, 0, suspendedParticleSnapshots.Length);
        }

        private static bool ShouldHideForSuspend(VfxPresentationSuspendReason reasons)
        {
            return reasons.HasFlag(VfxPresentationSuspendReason.Visibility) ||
                   reasons.HasFlag(VfxPresentationSuspendReason.TopologyTransition);
        }

        private void ConfigureParameterizedVisuals(
            in ParameterizedMotionVfxCommand command,
            IGameplayVfxCloneSourceProvider cloneSourceProvider,
            in VfxRendererInactiveVisualSnapshotSet sourceVisualSnapshot,
            in VfxSourceHierarchyPoseCapture sourcePoseCapture)
        {
            var disableRendererShadows = ShouldDisableParameterizedMotionShadows(command);
            if (TryCreateSourceClone(
                    command,
                    cloneSourceProvider,
                    sourcePoseCapture,
                    out var cloneRenderers,
                    out var liveSourceSnapshot))
            {
                HidePrefabVisuals();
                activeMaterialInstances = VfxRendererMaterialInstanceSet.Create(
                    cloneRenderers,
                    disableRendererShadows: disableRendererShadows);
                activeMaterialInstances.ApplyInactiveVisualSnapshot(
                    sourceVisualSnapshot.HasEntries ? sourceVisualSnapshot : liveSourceSnapshot);
                usingSourceClone = true;
                return;
            }

            if (command.CloneMode == ParameterizedMotionVfxCloneMode.SourceCloneMotion)
            {
                HidePrefabVisuals();
                activeMaterialInstances = VfxRendererMaterialInstanceSet.Create(
                    Array.Empty<Renderer>(),
                    disableRendererShadows: disableRendererShadows);
                usingSourceClone = true;
                return;
            }

            activeMaterialInstances = VfxRendererMaterialInstanceSet.Create(
                prefabRenderers,
                disableRendererShadows: disableRendererShadows);
            usingSourceClone = false;
        }

        private static bool ShouldDisableParameterizedMotionShadows(in ParameterizedMotionVfxCommand command)
        {
            return command.CueId == GameplayVfxCueId.From(EnemyVfxCue.DeathMotion) &&
                   command.FadeMode == ParameterizedMotionVfxFadeMode.EnemyDeathFade;
        }

        private bool TryCreateSourceClone(
            in ParameterizedMotionVfxCommand command,
            IGameplayVfxCloneSourceProvider cloneSourceProvider,
            in VfxSourceHierarchyPoseCapture sourcePoseCapture,
            out Renderer[] cloneRenderers,
            out VfxRendererInactiveVisualSnapshotSet liveSourceSnapshot)
        {
            cloneRenderers = Array.Empty<Renderer>();
            liveSourceSnapshot = VfxRendererInactiveVisualSnapshotSet.Empty;
            if (command.CloneMode == ParameterizedMotionVfxCloneMode.PrefabOnly ||
                cloneSourceProvider == null ||
                !cloneSourceProvider.TryResolveCloneSource(
                    new GameplayVfxCloneSourceKey(command.SourceEntityId, command.SequenceId),
                    out var source) ||
                source.ModelRoot == null)
            {
                return false;
            }

            liveSourceSnapshot = source.CaptureInactiveVisualSnapshot();
            if (ShouldFreezeSourcePose(command))
            {
                return TryCreateFrozenDeathSourceClone(
                    command,
                    source,
                    sourcePoseCapture,
                    out cloneRenderers);
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
                DestroySourceClone();
                return false;
            }

            return true;
        }

        private bool TryCreateFrozenDeathSourceClone(
            in ParameterizedMotionVfxCommand command,
            in GameplayVfxCloneSource source,
            in VfxSourceHierarchyPoseCapture sourcePoseCapture,
            out Renderer[] cloneRenderers)
        {
            cloneRenderers = Array.Empty<Renderer>();
            if (TryFindUnsupportedPoseWriter(source.ModelRoot, out var unsupportedPoseWriter))
            {
                LogSourceClonePoseFailure(
                    command,
                    VfxSourceHierarchyPoseFailure.UnsupportedPoseWriter,
                    unsupportedPoseWriter != null ? unsupportedPoseWriter.GetType().Name : "Unknown");
                return false;
            }

            VfxSourceHierarchyPoseSnapshot poseSnapshot;
            if (sourcePoseCapture.WasAttempted)
            {
                if (!sourcePoseCapture.HasSnapshot)
                {
                    LogSourceClonePoseFailure(command, sourcePoseCapture.Failure);
                    return false;
                }

                poseSnapshot = sourcePoseCapture.Snapshot;
            }
            else if (!VfxSourceHierarchyPoseSnapshot.TryCapture(
                         source.ModelRoot,
                         out poseSnapshot,
                         out var captureFailure))
            {
                LogSourceClonePoseFailure(command, captureFailure);
                return false;
            }

            sourceCloneObject = UnityEngine.Object.Instantiate(
                source.ModelRoot.gameObject,
                sourceCloneStagingRoot,
                worldPositionStays: false);
            sourceCloneObject.name = "ParameterizedMotionCloneRoot";
            sourceCloneObject.SetActive(false);
            DisableCloneAnimators(sourceCloneObject);
            RemoveGameplayAffectingComponents(sourceCloneObject);
            if (!poseSnapshot.TryApply(sourceCloneObject.transform, out var applyFailure))
            {
                LogSourceClonePoseFailure(command, applyFailure);
                DestroySourceClone();
                return false;
            }

            sourceCloneObject.transform.SetParent(Transform, worldPositionStays: false);
            sourceCloneObject.SetActive(true);
            cloneRenderers = sourceCloneObject.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (cloneRenderers.Length == 0)
            {
                LogSourceClonePoseFailure(command, VfxSourceHierarchyPoseFailure.MissingRenderer);
                DestroySourceClone();
                cloneRenderers = Array.Empty<Renderer>();
                return false;
            }

            return true;
        }

        private static bool ShouldFreezeSourcePose(in ParameterizedMotionVfxCommand command)
        {
            return command.CueId == GameplayVfxCueId.From(EnemyVfxCue.DeathMotion) &&
                   command.CloneMode == ParameterizedMotionVfxCloneMode.PrefabWithSourceClone &&
                   command.FadeMode == ParameterizedMotionVfxFadeMode.EnemyDeathFade;
        }

        private static void DisableCloneAnimators(GameObject root)
        {
            var animators = root.GetComponentsInChildren<Animator>(includeInactive: true);
            for (var i = 0; i < animators.Length; i++)
            {
                var animator = animators[i];
                if (animator == null)
                {
                    continue;
                }

                animator.writeDefaultValuesOnDisable = false;
                animator.keepAnimatorStateOnDisable = true;
                animator.enabled = false;
            }
        }

        internal static bool TryFindUnsupportedPoseWriter(Transform sourceRoot, out Component unsupportedPoseWriter)
        {
            var components = sourceRoot.GetComponentsInChildren<Component>(includeInactive: true);
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null || component is Animator)
                {
                    continue;
                }

                if (component is GameplayVfxAttachPoint)
                {
                    continue;
                }

                if (component is Animation legacyAnimation && legacyAnimation.enabled)
                {
                    unsupportedPoseWriter = component;
                    return true;
                }

                if (component is MonoBehaviour behaviour && behaviour.enabled)
                {
                    unsupportedPoseWriter = component;
                    return true;
                }

                var typeName = component.GetType().Name;
                if (typeName == "Cloth" || typeName.EndsWith("Constraint", StringComparison.Ordinal))
                {
                    unsupportedPoseWriter = component;
                    return true;
                }
            }

            unsupportedPoseWriter = null;
            return false;
        }

        private static void LogSourceClonePoseFailure(
            in ParameterizedMotionVfxCommand command,
            VfxSourceHierarchyPoseFailure failure,
            string detail = null)
        {
            UnityEngine.Debug.LogWarning(
                $"{nameof(GameplayVfxPooledInstance)} failed to freeze the DeathMotion source pose; " +
                $"the authored fallback prefab will be used. reason={failure} detail={detail ?? "None"} " +
                $"sourceEntityId={command.SourceEntityId} sequenceId={command.SequenceId}");
        }

        private void ClearParameterizedVisuals()
        {
            activeMaterialInstances?.Clear();
            activeMaterialInstances = null;
            if (sourceCloneObject != null)
            {
                DestroySourceClone();
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

        private void ApplyParameterizedMotion(
            in ParameterizedMotionVfxCommand command,
            float elapsedSeconds)
        {
            var sample = ParameterizedMotionVfxSampler.Sample(command, elapsedSeconds);
            Transform.localPosition = sample.LocalPosition;
            Transform.localRotation = sample.LocalRotation;

            if (command.FadeMode == ParameterizedMotionVfxFadeMode.EnemyDeathFade)
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

            var colliders = root.GetComponentsInChildren<Collider>(includeInactive: true);
            for (var i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
                SafeDestroy(colliders[i]);
            }

            var rigidbodies = root.GetComponentsInChildren<Rigidbody>(includeInactive: true);
            for (var i = 0; i < rigidbodies.Length; i++)
            {
                rigidbodies[i].detectCollisions = false;
                rigidbodies[i].isKinematic = true;
                SafeDestroy(rigidbodies[i]);
            }

            var audioSources = root.GetComponentsInChildren<AudioSource>(includeInactive: true);
            for (var i = 0; i < audioSources.Length; i++)
            {
                audioSources[i].Stop();
                audioSources[i].enabled = false;
                SafeDestroy(audioSources[i]);
            }

            var attachPoints = root.GetComponentsInChildren<GameplayVfxAttachPoint>(includeInactive: true);
            for (var i = 0; i < attachPoints.Length; i++)
            {
                attachPoints[i].enabled = false;
                SafeDestroy(attachPoints[i]);
            }

            var components = root.GetComponentsInChildren<Component>(includeInactive: true);
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component != null && component.GetType().Name == "NavMeshAgent")
                {
                    if (component is Behaviour behaviour)
                    {
                        behaviour.enabled = false;
                    }

                    SafeDestroy(component);
                }
            }
        }

        private void DestroySourceClone()
        {
            if (sourceCloneObject == null)
            {
                return;
            }

            sourceCloneObject.SetActive(false);
            sourceCloneObject.transform.SetParent(sourceCloneStagingRoot, worldPositionStays: false);
            SafeDestroy(sourceCloneObject);
            sourceCloneObject = null;
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

        private readonly struct SuspendedParticleSnapshot
        {
            public SuspendedParticleSnapshot(
                bool wasPlaying,
                bool wasEmitting,
                bool wasPaused,
                int particleCountAtSuspend)
            {
                WasPlaying = wasPlaying;
                WasEmitting = wasEmitting;
                WasPaused = wasPaused;
                ParticleCountAtSuspend = particleCountAtSuspend;
                ShouldResumePlayback = wasPlaying || wasEmitting;
            }

            public bool WasPlaying { get; }

            public bool WasEmitting { get; }

            public bool WasPaused { get; }

            public int ParticleCountAtSuspend { get; }

            public bool ShouldResumePlayback { get; }
        }
    }

    internal enum VfxSourceHierarchyPoseFailure
    {
        None = 0,
        MissingRoot = 1,
        InactiveSource = 2,
        HierarchyMismatch = 3,
        SkinnedRendererMismatch = 4,
        BlendShapeMismatch = 5,
        UnsupportedPoseWriter = 6,
        MissingRenderer = 7,
        MissingScheduledSource = 8,
    }

    internal readonly struct VfxSourceHierarchyPoseCapture
    {
        private VfxSourceHierarchyPoseCapture(
            bool wasAttempted,
            VfxSourceHierarchyPoseSnapshot snapshot,
            VfxSourceHierarchyPoseFailure failure)
        {
            WasAttempted = wasAttempted;
            Snapshot = snapshot;
            Failure = failure;
        }

        public static VfxSourceHierarchyPoseCapture Unattempted => default;

        public bool WasAttempted { get; }

        public bool HasSnapshot => Snapshot != null;

        public VfxSourceHierarchyPoseSnapshot Snapshot { get; }

        public VfxSourceHierarchyPoseFailure Failure { get; }

        public static VfxSourceHierarchyPoseCapture Capture(Transform sourceRoot)
        {
            return VfxSourceHierarchyPoseSnapshot.TryCapture(
                sourceRoot,
                out var snapshot,
                out var failure)
                ? new VfxSourceHierarchyPoseCapture(true, snapshot, VfxSourceHierarchyPoseFailure.None)
                : Failed(failure);
        }

        public static VfxSourceHierarchyPoseCapture Failed(VfxSourceHierarchyPoseFailure failure)
        {
            if (failure == VfxSourceHierarchyPoseFailure.None)
            {
                throw new ArgumentException("A failed pose capture requires a non-None failure reason.", nameof(failure));
            }

            return new VfxSourceHierarchyPoseCapture(true, null, failure);
        }
    }

    internal sealed class VfxSourceHierarchyPoseSnapshot
    {
        private readonly NodePose[] nodes;

        private VfxSourceHierarchyPoseSnapshot(NodePose[] nodes)
        {
            this.nodes = nodes ?? Array.Empty<NodePose>();
        }

        public static bool TryCapture(
            Transform sourceRoot,
            out VfxSourceHierarchyPoseSnapshot snapshot,
            out VfxSourceHierarchyPoseFailure failure)
        {
            snapshot = null;
            if (sourceRoot == null)
            {
                failure = VfxSourceHierarchyPoseFailure.MissingRoot;
                return false;
            }

            if (!sourceRoot.gameObject.activeInHierarchy)
            {
                failure = VfxSourceHierarchyPoseFailure.InactiveSource;
                return false;
            }

            var sourceNodes = new List<Transform>();
            CollectPreOrder(sourceRoot, sourceNodes);
            var capturedNodes = new NodePose[sourceNodes.Count];
            for (var i = 0; i < sourceNodes.Count; i++)
            {
                var sourceNode = sourceNodes[i];
                var renderers = sourceNode.GetComponents<SkinnedMeshRenderer>();
                var rendererPoses = new SkinnedRendererPose[renderers.Length];
                for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    var renderer = renderers[rendererIndex];
                    var blendShapeCount = renderer.sharedMesh != null
                        ? renderer.sharedMesh.blendShapeCount
                        : 0;
                    var weights = new float[blendShapeCount];
                    for (var weightIndex = 0; weightIndex < blendShapeCount; weightIndex++)
                    {
                        weights[weightIndex] = renderer.GetBlendShapeWeight(weightIndex);
                    }

                    rendererPoses[rendererIndex] = new SkinnedRendererPose(renderer.localBounds, weights);
                }

                capturedNodes[i] = new NodePose(
                    sourceNode.childCount,
                    sourceNode.localPosition,
                    sourceNode.localRotation,
                    sourceNode.localScale,
                    rendererPoses);
            }

            snapshot = new VfxSourceHierarchyPoseSnapshot(capturedNodes);
            failure = VfxSourceHierarchyPoseFailure.None;
            return true;
        }

        public bool TryApply(Transform targetRoot, out VfxSourceHierarchyPoseFailure failure)
        {
            if (targetRoot == null)
            {
                failure = VfxSourceHierarchyPoseFailure.MissingRoot;
                return false;
            }

            var targetNodes = new List<Transform>();
            CollectPreOrder(targetRoot, targetNodes);
            if (targetNodes.Count != nodes.Length)
            {
                failure = VfxSourceHierarchyPoseFailure.HierarchyMismatch;
                return false;
            }

            for (var i = 0; i < nodes.Length; i++)
            {
                var targetNode = targetNodes[i];
                var nodePose = nodes[i];
                if (targetNode.childCount != nodePose.ChildCount)
                {
                    failure = VfxSourceHierarchyPoseFailure.HierarchyMismatch;
                    return false;
                }

                var renderers = targetNode.GetComponents<SkinnedMeshRenderer>();
                if (renderers.Length != nodePose.Renderers.Length)
                {
                    failure = VfxSourceHierarchyPoseFailure.SkinnedRendererMismatch;
                    return false;
                }

                for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    var renderer = renderers[rendererIndex];
                    var expectedBlendShapeCount = nodePose.Renderers[rendererIndex].BlendShapeWeights.Length;
                    var actualBlendShapeCount = renderer.sharedMesh != null
                        ? renderer.sharedMesh.blendShapeCount
                        : 0;
                    if (actualBlendShapeCount != expectedBlendShapeCount)
                    {
                        failure = VfxSourceHierarchyPoseFailure.BlendShapeMismatch;
                        return false;
                    }
                }
            }

            for (var i = 0; i < nodes.Length; i++)
            {
                var targetNode = targetNodes[i];
                var nodePose = nodes[i];
                targetNode.localPosition = nodePose.LocalPosition;
                targetNode.localRotation = nodePose.LocalRotation;
                targetNode.localScale = nodePose.LocalScale;

                var renderers = targetNode.GetComponents<SkinnedMeshRenderer>();
                for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    var renderer = renderers[rendererIndex];
                    var rendererPose = nodePose.Renderers[rendererIndex];
                    renderer.localBounds = rendererPose.LocalBounds;
                    for (var weightIndex = 0; weightIndex < rendererPose.BlendShapeWeights.Length; weightIndex++)
                    {
                        renderer.SetBlendShapeWeight(weightIndex, rendererPose.BlendShapeWeights[weightIndex]);
                    }
                }
            }

            failure = VfxSourceHierarchyPoseFailure.None;
            return true;
        }

        private static void CollectPreOrder(Transform node, ICollection<Transform> results)
        {
            results.Add(node);
            for (var childIndex = 0; childIndex < node.childCount; childIndex++)
            {
                CollectPreOrder(node.GetChild(childIndex), results);
            }
        }

        private readonly struct NodePose
        {
            public NodePose(
                int childCount,
                Vector3 localPosition,
                Quaternion localRotation,
                Vector3 localScale,
                SkinnedRendererPose[] renderers)
            {
                ChildCount = childCount;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
                LocalScale = localScale;
                Renderers = renderers ?? Array.Empty<SkinnedRendererPose>();
            }

            public int ChildCount { get; }
            public Vector3 LocalPosition { get; }
            public Quaternion LocalRotation { get; }
            public Vector3 LocalScale { get; }
            public SkinnedRendererPose[] Renderers { get; }
        }

        private readonly struct SkinnedRendererPose
        {
            public SkinnedRendererPose(Bounds localBounds, float[] blendShapeWeights)
            {
                LocalBounds = localBounds;
                BlendShapeWeights = blendShapeWeights ?? Array.Empty<float>();
            }

            public Bounds LocalBounds { get; }
            public float[] BlendShapeWeights { get; }
        }
    }
}
