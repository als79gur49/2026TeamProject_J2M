using System;
using Game.Shared.Audio;
using UnityEngine;

namespace Game.Feature.Flow.Audio
{
    [DisallowMultipleComponent]
    public sealed class GlobalAudioFlowRoot : MonoBehaviour
    {
        private const string DuplicateRootMessage =
            "GlobalAudioFlowRoot cannot exist more than once. Reuse the existing persistent audio-flow root instead of creating another.";
        private const string RootObjectName = "GlobalAudioFlowRoot";

        private static GlobalAudioFlowRoot current;

        [SerializeField] private AudioRuntimeInstaller audioRuntimeInstaller;

        private IBgmFlowCoordinator coordinator;
        private bool isInitialized;

        public static GlobalAudioFlowRoot Current => current;

        public AudioRuntimeInstaller AudioRuntimeInstaller => audioRuntimeInstaller;

        public AudioRuntimeRoot RuntimeRoot => audioRuntimeInstaller?.RuntimeRoot;

        public IBgmFlowCoordinator Coordinator => coordinator;

        public static GlobalAudioFlowRoot GetOrCreate()
        {
            if (current != null)
            {
                current.InitializePersistentRoot();
                return current;
            }

            var rootObject = new GameObject(RootObjectName);
            var root = rootObject.AddComponent<GlobalAudioFlowRoot>();
            root.InitializePersistentRoot();
            return root;
        }

        private void Awake()
        {
            if (current != null && current != this)
            {
                throw new InvalidOperationException(DuplicateRootMessage);
            }

            current = this;
        }

        public void InitializePersistentRoot()
        {
            if (current != null && current != this)
            {
                throw new InvalidOperationException(DuplicateRootMessage);
            }

            current = this;
            gameObject.name = RootObjectName;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (isInitialized)
            {
                return;
            }

            audioRuntimeInstaller ??= GetComponent<AudioRuntimeInstaller>() ?? gameObject.AddComponent<AudioRuntimeInstaller>();
            audioRuntimeInstaller.Install();
            var runtimeRoot = audioRuntimeInstaller.RuntimeRoot ?? throw new InvalidOperationException(
                "GlobalAudioFlowRoot failed to initialize its AudioRuntimeInstaller.");
            AudioRuntimeExternalRootRegistry.RegisterPersistentRuntime(runtimeRoot, this);
            coordinator = new BgmFlowCoordinator(new BgmPlaybackPortAdapter(audioRuntimeInstaller.AudioService));
            isInitialized = true;
        }

        private void OnDestroy()
        {
            if (RuntimeRoot != null)
            {
                AudioRuntimeExternalRootRegistry.UnregisterPersistentRuntime(RuntimeRoot, this);
            }

            if (current == this)
            {
                current = null;
            }

            coordinator = null;
            isInitialized = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            current = null;
        }
    }
}
