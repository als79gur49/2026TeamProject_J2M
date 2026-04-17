using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Shared.Audio
{
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour, IAudioService
    {
        [SerializeField] [Min(1)] private int initialPoolSize = 8;
        [SerializeField] [Min(1)] private int maxPoolSize = 24;

        private readonly List<AudioSourcePlaybackController> activeControllers = new();
        private AttachedAudioRegistry attachedRegistry;
        private AudioSource bgmSource;
        private bool runtimeInitialized;
        private AudioSourcePool sourcePool;
        private Transform runtimeRoot;

        public void InitializeRuntime(Transform ownerRoot)
        {
            if (ownerRoot == null)
            {
                throw new ArgumentNullException(nameof(ownerRoot));
            }

            runtimeRoot = ownerRoot;
            sourcePool ??= new AudioSourcePool(runtimeRoot, initialPoolSize, maxPoolSize);
            attachedRegistry ??= new AttachedAudioRegistry();
            EnsureBgmSource();
            runtimeInitialized = true;
        }

        public AudioPlaybackHandle Play2D(AudioDefinition definition, in AudioPlaybackContext context = default)
        {
            ThrowIfNotInitialized();
            return CreatePlayback(definition, context, attachedKey: null, bgm: false);
        }

        public AudioPlaybackHandle PlayAttached(
            AudioDefinition definition,
            Component owner,
            AudioAttachmentSlot slot,
            in AudioPlaybackContext context = default)
        {
            ThrowIfNotInitialized();

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

            var handle = CreatePlayback(definition, context, key, bgm: false);
            if (handle.IsValid)
            {
                attachedRegistry.Register(key, owner, definition, handle);
            }

            return handle;
        }

        public AudioPlaybackHandle PlayBgm(AudioDefinition definition)
        {
            ThrowIfNotInitialized();

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var playbackData = definition.Resolve(default);
            if (playbackData.Clip == null)
            {
                throw new InvalidOperationException($"{definition.name} resolved a null AudioClip.");
            }

            if (bgmSource == null)
            {
                EnsureBgmSource();
            }

            for (var i = activeControllers.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(activeControllers[i].Source, bgmSource))
                {
                    activeControllers[i].Stop();
                }
            }

            bgmSource.clip = playbackData.Clip;
            bgmSource.loop = true;
            bgmSource.volume = playbackData.Volume;
            bgmSource.pitch = playbackData.Pitch;
            bgmSource.Play();

            var controller = new AudioSourcePlaybackController(
                bgmSource,
                sourcePool: null,
                onStop: null,
                invalidateAttached: null,
                loop: true);
            activeControllers.Add(controller);
            return controller.Handle;
        }

        public void Stop(AudioPlaybackHandle handle)
        {
            ThrowIfNotInitialized();
            handle?.Stop();
        }

        public void StopBgm()
        {
            ThrowIfNotInitialized();

            if (bgmSource == null)
            {
                return;
            }

            bgmSource.Stop();
            for (var i = activeControllers.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(activeControllers[i].Source, bgmSource))
                {
                    activeControllers[i].Stop();
                }
            }
        }

        private void Update()
        {
            for (var i = activeControllers.Count - 1; i >= 0; i--)
            {
                if (!activeControllers[i].IsValid)
                {
                    activeControllers.RemoveAt(i);
                    continue;
                }

                activeControllers[i].Tick();
                if (!activeControllers[i].IsValid)
                {
                    activeControllers.RemoveAt(i);
                }
            }
        }

        private AudioPlaybackHandle CreatePlayback(
            AudioDefinition definition,
            in AudioPlaybackContext context,
            AttachedAudioKey? attachedKey,
            bool bgm)
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

            var source = bgm
                ? bgmSource
                : sourcePool.Acquire();
            if (source == null)
            {
                return AudioPlaybackHandle.Invalid;
            }

            ConfigureSource(source, playbackData);
            source.Play();

            Action invalidateAttached = null;
            if (attachedKey.HasValue)
            {
                var key = attachedKey.Value;
                invalidateAttached = () => attachedRegistry.Unregister(key);
            }

            var controller = new AudioSourcePlaybackController(
                source,
                bgm ? null : sourcePool,
                onStop: null,
                invalidateAttached,
                playbackData.Loop);
            activeControllers.Add(controller);
            return controller.Handle;
        }

        private void ConfigureSource(AudioSource source, AudioPlaybackData playbackData)
        {
            source.clip = playbackData.Clip;
            source.loop = playbackData.Loop;
            source.volume = playbackData.Volume;
            source.pitch = playbackData.Pitch;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.panStereo = 0f;
        }

        private void ThrowIfNotInitialized()
        {
            if (!runtimeInitialized)
            {
                throw new InvalidOperationException(
                    "AudioManager must be initialized by AudioRuntimeRoot. Add AudioRuntimeInstaller to the canonical bootstrap root.");
            }
        }

        private void EnsureBgmSource()
        {
            if (bgmSource != null)
            {
                return;
            }

            var bgmObject = new GameObject("AudioBgmChannel");
            bgmObject.transform.SetParent(runtimeRoot != null ? runtimeRoot : transform, worldPositionStays: false);
            bgmSource = bgmObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;
            bgmSource.spatialBlend = 0f;
            bgmSource.dopplerLevel = 0f;
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
                if (source == null || !leased.Remove(source))
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
            private readonly Action invalidateAttached;
            private readonly Action onStop;
            private readonly AudioSourcePool sourcePool;
            private bool stopped;

            public AudioSourcePlaybackController(
                AudioSource source,
                AudioSourcePool sourcePool,
                Action onStop,
                Action invalidateAttached,
                bool loop)
            {
                Source = source;
                this.sourcePool = sourcePool;
                this.onStop = onStop;
                this.invalidateAttached = invalidateAttached;
                Loop = loop;
                Handle = new AudioPlaybackHandle(this);
            }

            public AudioPlaybackHandle Handle { get; }

            public bool Loop { get; }

            public AudioSource Source { get; }

            public bool IsValid => !stopped && Source != null;

            public void Stop()
            {
                if (stopped)
                {
                    return;
                }

                stopped = true;
                invalidateAttached?.Invoke();

                if (Source != null)
                {
                    Source.Stop();
                    sourcePool?.Release(Source);
                }

                onStop?.Invoke();
            }

            public void Pause()
            {
                if (!IsValid)
                {
                    return;
                }

                Source.Pause();
            }

            public void Resume()
            {
                if (!IsValid)
                {
                    return;
                }

                Source.UnPause();
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
                if (!IsValid || Loop)
                {
                    return;
                }

                if (!Source.isPlaying)
                {
                    Stop();
                }
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
