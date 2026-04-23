using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Shared.Audio
{
    internal readonly struct AudioLivePlaybackDebugSnapshot
    {
        public AudioLivePlaybackDebugSnapshot(
            AudioChannel leafChannel,
            float baseClipVolume,
            AudioSource source,
            bool isSourceReferenceValid,
            bool isControllerValid)
        {
            LeafChannel = leafChannel;
            BaseClipVolume = baseClipVolume;
            Source = source;
            IsSourceReferenceValid = isSourceReferenceValid;
            IsControllerValid = isControllerValid;
        }

        public AudioChannel LeafChannel { get; }

        public float BaseClipVolume { get; }

        public AudioSource Source { get; }

        public bool IsSourceReferenceValid { get; }

        public bool IsControllerValid { get; }
    }

    internal sealed class AudioPlaybackService
    {
        private readonly List<AudioSourcePlaybackController> activeControllers = new();
        private readonly Dictionary<AudioSourcePlaybackController, AudioLivePlaybackRecord> livePlaybacks = new();

        private AttachedAudioRegistry attachedRegistry;
        private AudioSource bgmSource;
        private AudioSourcePool sourcePool;
        private Transform runtimeRoot;

        public int LivePlaybackCount
        {
            get
            {
                PruneStaleLivePlaybacks();
                return livePlaybacks.Count;
            }
        }

        public void Initialize(Transform ownerRoot, int initialPoolSize, int maxPoolSize)
        {
            if (ownerRoot == null)
            {
                throw new ArgumentNullException(nameof(ownerRoot));
            }

            runtimeRoot = ownerRoot;
            sourcePool ??= new AudioSourcePool(runtimeRoot, initialPoolSize, maxPoolSize);
            attachedRegistry ??= new AttachedAudioRegistry();
            EnsureBgmSource();
        }

        public AudioPlaybackHandle Play2D(
            AudioDefinition definition,
            in AudioPlaybackContext context,
            AudioMixingService mixingService)
        {
            return CreatePlayback(definition, context, attachedKey: null, useBgmLane: false, mixingService);
        }

        public AudioPlaybackHandle PlayAttached(
            AudioDefinition definition,
            Component owner,
            AudioAttachmentSlot slot,
            in AudioPlaybackContext context,
            AudioMixingService mixingService)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (slot.IsEmpty)
            {
                throw new ArgumentException("Attached playback requires a non-empty AudioAttachmentSlot.", nameof(slot));
            }

            if (!owner.gameObject.activeInHierarchy)
            {
                return AudioPlaybackHandle.Invalid;
            }

            var key = new AttachedAudioKey(owner.GetInstanceID(), slot);
            if (attachedRegistry.TryReuseExisting(key, owner, definition, out var reusedHandle))
            {
                return reusedHandle;
            }

            var handle = CreatePlayback(definition, context, key, useBgmLane: false, mixingService);
            if (handle.IsValid)
            {
                attachedRegistry.Register(key, owner, definition, handle);
            }

            return handle;
        }

        public AudioPlaybackHandle PlayBgm(AudioDefinition definition, AudioMixingService mixingService)
        {
            EnsureBgmSource();

            for (var i = activeControllers.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(activeControllers[i].Source, bgmSource))
                {
                    activeControllers[i].Stop();
                }
            }

            return CreatePlayback(definition, default, attachedKey: null, useBgmLane: true, mixingService);
        }

        public void Stop(AudioPlaybackHandle handle)
        {
            handle?.Stop();
        }

        public void StopBgm()
        {
            if (bgmSource == null)
            {
                return;
            }

            for (var i = activeControllers.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(activeControllers[i].Source, bgmSource))
                {
                    activeControllers[i].Stop();
                }
            }
        }

        public void Tick()
        {
            for (var i = activeControllers.Count - 1; i >= 0; i--)
            {
                var controller = activeControllers[i];
                if (!controller.IsAlive)
                {
                    controller.Stop();
                    activeControllers.RemoveAt(i);
                    continue;
                }

                controller.Tick();
                if (!controller.IsValid)
                {
                    activeControllers.RemoveAt(i);
                }
            }

            PruneStaleLivePlaybacks();
        }

        public void ApplyLiveMix(AudioMixingService mixingService)
        {
            var staleControllers = new List<AudioSourcePlaybackController>();
            var controllers = new List<AudioSourcePlaybackController>(livePlaybacks.Keys);
            for (var i = 0; i < controllers.Count; i++)
            {
                var controller = controllers[i];
                controller.RefreshCompletionState();
                if (!livePlaybacks.TryGetValue(controller, out var record))
                {
                    continue;
                }

                if (!record.IsSourceReferenceValid || !record.Controller.IsAlive)
                {
                    staleControllers.Add(record.Controller);
                    continue;
                }

                record.Source.volume = mixingService.ResolvePlaybackVolume(record.LeafChannel, record.BaseClipVolume);
            }

            for (var i = 0; i < staleControllers.Count; i++)
            {
                staleControllers[i].Stop();
            }
        }

        public void Dispose()
        {
            for (var i = activeControllers.Count - 1; i >= 0; i--)
            {
                activeControllers[i].Stop();
            }

            activeControllers.Clear();
            livePlaybacks.Clear();
            attachedRegistry?.Dispose();
        }

        public AudioLivePlaybackDebugSnapshot[] CaptureLivePlaybackSnapshots()
        {
            PruneStaleLivePlaybacks();
            var snapshots = new AudioLivePlaybackDebugSnapshot[livePlaybacks.Count];
            var index = 0;
            foreach (var pair in livePlaybacks)
            {
                var record = pair.Value;
                snapshots[index++] = new AudioLivePlaybackDebugSnapshot(
                    record.LeafChannel,
                    record.BaseClipVolume,
                    record.Source,
                    record.IsSourceReferenceValid,
                    record.Controller.IsAlive);
            }

            return snapshots;
        }

        private AudioPlaybackHandle CreatePlayback(
            AudioDefinition definition,
            in AudioPlaybackContext context,
            AttachedAudioKey? attachedKey,
            bool useBgmLane,
            AudioMixingService mixingService)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var playbackData = definition.Resolve(context);
            if (playbackData.Clip == null)
            {
                throw new InvalidOperationException($"{definition.name} resolved a null AudioClip.");
            }

            var leafChannel = AudioDefinitionCategoryRules.ToLeafChannel(
                playbackData.Category,
                $"AudioDefinition '{definition.name}'");

            var source = useBgmLane
                ? bgmSource
                : sourcePool.Acquire();
            if (source == null)
            {
                return AudioPlaybackHandle.Invalid;
            }

            var finalVolume = mixingService.ResolvePlaybackVolume(leafChannel, playbackData.Volume);
            ConfigureSource(source, playbackData, finalVolume);
            source.Play();

            Action invalidateAttached = null;
            if (attachedKey.HasValue)
            {
                var key = attachedKey.Value;
                invalidateAttached = () => attachedRegistry.Unregister(key);
            }

            var controller = new AudioSourcePlaybackController(
                source,
                useBgmLane ? null : sourcePool,
                this,
                invalidateAttached,
                playbackData.Loop);
            activeControllers.Add(controller);
            RegisterLivePlayback(controller, leafChannel, playbackData.Volume);
            return controller.Handle;
        }

        private void RegisterLivePlayback(
            AudioSourcePlaybackController controller,
            AudioChannel leafChannel,
            float baseClipVolume)
        {
            livePlaybacks[controller] = new AudioLivePlaybackRecord(controller, leafChannel, baseClipVolume);
        }

        private void UnregisterLivePlayback(AudioSourcePlaybackController controller)
        {
            if (controller == null)
            {
                return;
            }

            livePlaybacks.Remove(controller);
        }

        private void PruneStaleLivePlaybacks()
        {
            var staleControllers = new List<AudioSourcePlaybackController>();
            var controllers = new List<AudioSourcePlaybackController>(livePlaybacks.Keys);
            for (var i = 0; i < controllers.Count; i++)
            {
                var controller = controllers[i];
                controller.RefreshCompletionState();
                if (!livePlaybacks.TryGetValue(controller, out var record))
                {
                    continue;
                }

                if (!record.IsSourceReferenceValid || !record.Controller.IsAlive)
                {
                    staleControllers.Add(controller);
                }
            }

            for (var i = 0; i < staleControllers.Count; i++)
            {
                staleControllers[i].Stop();
            }
        }

        private void ConfigureSource(AudioSource source, AudioPlaybackData playbackData, float finalVolume)
        {
            source.clip = playbackData.Clip;
            source.loop = playbackData.Loop;
            source.volume = finalVolume;
            source.pitch = playbackData.Pitch;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.panStereo = 0f;
        }

        private void EnsureBgmSource()
        {
            if (bgmSource != null)
            {
                return;
            }

            var bgmObject = new GameObject("AudioBgmChannel");
            bgmObject.transform.SetParent(runtimeRoot, worldPositionStays: false);
            bgmSource = bgmObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;
            bgmSource.spatialBlend = 0f;
            bgmSource.dopplerLevel = 0f;
        }

        private readonly struct AudioLivePlaybackRecord
        {
            public AudioLivePlaybackRecord(
                AudioSourcePlaybackController controller,
                AudioChannel leafChannel,
                float baseClipVolume)
            {
                Controller = controller;
                LeafChannel = leafChannel;
                BaseClipVolume = baseClipVolume;
            }

            public AudioSourcePlaybackController Controller { get; }

            public AudioChannel LeafChannel { get; }

            public float BaseClipVolume { get; }

            public AudioSource Source => Controller.Source;

            public bool IsSourceReferenceValid => Source != null;
        }

        private readonly struct AttachedAudioKey : IEquatable<AttachedAudioKey>
        {
            public AttachedAudioKey(int ownerInstanceId, AudioAttachmentSlot slot)
            {
                OwnerInstanceId = ownerInstanceId;
                Slot = slot;
            }

            public int OwnerInstanceId { get; }

            public AudioAttachmentSlot Slot { get; }

            public bool Equals(AttachedAudioKey other)
            {
                return OwnerInstanceId == other.OwnerInstanceId &&
                       Slot.Equals(other.Slot);
            }

            public override bool Equals(object obj)
            {
                return obj is AttachedAudioKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(OwnerInstanceId, Slot);
            }
        }

        private sealed class AttachedAudioRegistry
        {
            private readonly Dictionary<AttachedAudioKey, AttachedAudioEntry> entries = new();

            public bool TryReuseExisting(
                AttachedAudioKey key,
                Component owner,
                AudioDefinition definition,
                out AudioPlaybackHandle handle)
            {
                if (entries.TryGetValue(key, out var entry))
                {
                    if (!entry.Handle.IsValid)
                    {
                        entries.Remove(key);
                    }
                    else if (ReferenceEquals(entry.Definition, definition))
                    {
                        handle = entry.Handle;
                        return true;
                    }
                    else
                    {
                        entry.Handle.Stop();
                        entries.Remove(key);
                    }
                }

                handle = AudioPlaybackHandle.Invalid;
                return false;
            }

            public void Register(
                AttachedAudioKey key,
                Component owner,
                AudioDefinition definition,
                AudioPlaybackHandle handle)
            {
                if (entries.ContainsKey(key))
                {
                    entries[key].Handle.Stop();
                    entries.Remove(key);
                }

                var relay = owner.gameObject.GetComponent<AudioOwnerLifecycleRelay>();
                if (relay == null)
                {
                    relay = owner.gameObject.AddComponent<AudioOwnerLifecycleRelay>();
                }

                relay.Invalidated -= HandleOwnerInvalidated;
                relay.Invalidated += HandleOwnerInvalidated;

                entries[key] = new AttachedAudioEntry(definition, handle, relay);
            }

            public void Unregister(AttachedAudioKey key)
            {
                if (!entries.TryGetValue(key, out var entry))
                {
                    return;
                }

                entries.Remove(key);

                if (entry.Relay == null)
                {
                    return;
                }

                foreach (var remainingEntry in entries.Values)
                {
                    if (remainingEntry.Relay == entry.Relay)
                    {
                        return;
                    }
                }

                entry.Relay.Invalidated -= HandleOwnerInvalidated;
            }

            public void Dispose()
            {
                foreach (var entry in entries.Values)
                {
                    if (entry.Relay != null)
                    {
                        entry.Relay.Invalidated -= HandleOwnerInvalidated;
                    }
                }

                entries.Clear();
            }

            private void HandleOwnerInvalidated(AudioOwnerLifecycleRelay relay)
            {
                var keysToStop = new List<AttachedAudioKey>();
                foreach (var pair in entries)
                {
                    if (pair.Value.Relay == relay)
                    {
                        keysToStop.Add(pair.Key);
                    }
                }

                for (var i = 0; i < keysToStop.Count; i++)
                {
                    if (!entries.TryGetValue(keysToStop[i], out var entry))
                    {
                        continue;
                    }

                    entry.Handle.Stop();
                    Unregister(keysToStop[i]);
                }
            }

            private sealed class AttachedAudioEntry
            {
                public AttachedAudioEntry(
                    AudioDefinition definition,
                    AudioPlaybackHandle handle,
                    AudioOwnerLifecycleRelay relay)
                {
                    Definition = definition;
                    Handle = handle;
                    Relay = relay;
                }

                public AudioDefinition Definition { get; }

                public AudioPlaybackHandle Handle { get; }

                public AudioOwnerLifecycleRelay Relay { get; }
            }
        }

        private sealed class AudioSourcePool
        {
            private readonly Stack<AudioSource> available = new();
            private readonly HashSet<AudioSource> leased = new();
            private readonly int maxPoolSize;
            private readonly Transform poolRoot;

            public AudioSourcePool(Transform runtimeRoot, int initialPoolSize, int maxPoolSize)
            {
                this.maxPoolSize = Mathf.Max(initialPoolSize, maxPoolSize);
                var poolObject = new GameObject("AudioSourcePool");
                poolObject.transform.SetParent(runtimeRoot, worldPositionStays: false);
                poolRoot = poolObject.transform;

                for (var i = 0; i < initialPoolSize; i++)
                {
                    available.Push(CreateSource(i));
                }
            }

            public AudioSource Acquire()
            {
                PruneDestroyedEntries();

                AudioSource source = null;
                if (available.Count > 0)
                {
                    source = available.Pop();
                }
                else if (leased.Count < maxPoolSize)
                {
                    source = CreateSource(leased.Count + available.Count);
                }

                if (source != null)
                {
                    leased.Add(source);
                }

                return source;
            }

            public void Release(AudioSource source)
            {
                if (ReferenceEquals(source, null) || !RemoveLeasedEntry(source))
                {
                    return;
                }

                if (source == null)
                {
                    return;
                }

                source.Stop();
                source.clip = null;
                source.loop = false;
                source.volume = 1f;
                source.pitch = 1f;
                source.transform.SetParent(poolRoot, worldPositionStays: false);
                available.Push(source);
            }

            private bool RemoveLeasedEntry(AudioSource source)
            {
                if (leased.Remove(source))
                {
                    return true;
                }

                AudioSource matchedSource = null;
                foreach (var leasedSource in leased)
                {
                    if (ReferenceEquals(leasedSource, source))
                    {
                        matchedSource = leasedSource;
                        break;
                    }
                }

                if (ReferenceEquals(matchedSource, null))
                {
                    return false;
                }

                leased.Remove(matchedSource);
                return true;
            }

            private void PruneDestroyedEntries()
            {
                PruneDestroyedLeasedEntries();
                PruneDestroyedAvailableEntries();
            }

            private void PruneDestroyedLeasedEntries()
            {
                if (leased.Count == 0)
                {
                    return;
                }

                var staleSources = new List<AudioSource>();
                foreach (var leasedSource in leased)
                {
                    if (leasedSource == null)
                    {
                        staleSources.Add(leasedSource);
                    }
                }

                for (var i = 0; i < staleSources.Count; i++)
                {
                    RemoveLeasedEntry(staleSources[i]);
                }
            }

            private void PruneDestroyedAvailableEntries()
            {
                if (available.Count == 0)
                {
                    return;
                }

                var aliveSources = new List<AudioSource>(available.Count);
                foreach (var availableSource in available)
                {
                    if (availableSource != null)
                    {
                        aliveSources.Add(availableSource);
                    }
                }

                if (aliveSources.Count == available.Count)
                {
                    return;
                }

                available.Clear();
                for (var i = aliveSources.Count - 1; i >= 0; i--)
                {
                    available.Push(aliveSources[i]);
                }
            }

            private AudioSource CreateSource(int index)
            {
                var sourceObject = new GameObject($"PooledAudioSource_{index:000}");
                sourceObject.transform.SetParent(poolRoot, worldPositionStays: false);
                var source = sourceObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                return source;
            }
        }

        private sealed class AudioSourcePlaybackController : IAudioPlaybackController
        {
            private readonly double expectedDurationSeconds;
            private readonly Action invalidateAttached;
            private readonly bool loop;
            private readonly AudioPlaybackService owner;
            private readonly int startedAtFrame;
            private readonly double startedAtRealtime;
            private readonly AudioSourcePool sourcePool;
            private double accumulatedPausedDuration;
            private bool paused;
            private double pausedAtRealtime;
            private bool stopped;

            public AudioSourcePlaybackController(
                AudioSource source,
                AudioSourcePool sourcePool,
                AudioPlaybackService owner,
                Action invalidateAttached,
                bool loop)
            {
                Source = source;
                this.sourcePool = sourcePool;
                this.owner = owner;
                this.invalidateAttached = invalidateAttached;
                this.loop = loop;
                startedAtFrame = Time.frameCount;
                startedAtRealtime = Time.realtimeSinceStartupAsDouble;
                expectedDurationSeconds = ResolveExpectedDurationSeconds(source, loop);
                Handle = new AudioPlaybackHandle(this);
            }

            public AudioPlaybackHandle Handle { get; }

            public AudioSource Source { get; }

            public bool IsValid
            {
                get
                {
                    RefreshCompletionState();
                    return IsAlive;
                }
            }

            public bool IsAlive => !stopped && Source != null;

            public void Stop()
            {
                if (stopped)
                {
                    return;
                }

                stopped = true;
                owner.UnregisterLivePlayback(this);
                invalidateAttached?.Invoke();

                if (sourcePool != null)
                {
                    sourcePool.Release(Source);
                    return;
                }

                if (Source != null)
                {
                    Source.Stop();
                }
            }

            public void Pause()
            {
                if (!IsValid || paused)
                {
                    return;
                }

                Source.Pause();
                paused = true;
                pausedAtRealtime = Time.realtimeSinceStartupAsDouble;
            }

            public void Resume()
            {
                if (!IsValid || !paused)
                {
                    return;
                }

                Source.UnPause();
                accumulatedPausedDuration += Time.realtimeSinceStartupAsDouble - pausedAtRealtime;
                paused = false;
            }

            public void SetVolume(float volume)
            {
                if (!IsValid)
                {
                    return;
                }

                Source.volume = Mathf.Clamp01(volume);
            }

            public void Tick()
            {
                if (!IsAlive || loop || paused)
                {
                    return;
                }

                if (!Source.isPlaying || HasReachedNaturalCompletion())
                {
                    Stop();
                }
            }

            public void RefreshCompletionState()
            {
                if (!IsAlive || loop || paused)
                {
                    return;
                }

                if (HasReachedNaturalCompletion())
                {
                    Stop();
                }
            }

            private bool HasReachedNaturalCompletion()
            {
                if (double.IsPositiveInfinity(expectedDurationSeconds))
                {
                    return false;
                }

                var elapsedRealtime = Time.realtimeSinceStartupAsDouble - startedAtRealtime - accumulatedPausedDuration;
                if (elapsedRealtime >= expectedDurationSeconds)
                {
                    return true;
                }

                return Time.frameCount > startedAtFrame &&
                       expectedDurationSeconds <= Time.unscaledDeltaTime + 0.0001f;
            }

            private static double ResolveExpectedDurationSeconds(AudioSource source, bool loop)
            {
                if (loop || source == null || source.clip == null)
                {
                    return double.PositiveInfinity;
                }

                var pitch = Math.Max(0.01f, Math.Abs(source.pitch));
                return source.clip.samples / (double)source.clip.frequency / pitch;
            }
        }
    }

    [DisallowMultipleComponent]
    internal sealed class AudioOwnerLifecycleRelay : MonoBehaviour
    {
        public event Action<AudioOwnerLifecycleRelay> Invalidated;

        private bool destroying;

        private void OnDisable()
        {
            if (!destroying)
            {
                Invalidated?.Invoke(this);
            }
        }

        private void OnDestroy()
        {
            destroying = true;
            Invalidated?.Invoke(this);
        }
    }
}
