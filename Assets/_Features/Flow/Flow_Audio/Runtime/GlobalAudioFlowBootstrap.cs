using System;
using Game.Shared.Audio;
using UnityEngine;

namespace Game.Feature.Flow.Audio
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1100)]
    public sealed class GlobalAudioFlowBootstrap : MonoBehaviour
    {
        private const string MissingCoordinatorMessage =
            "GlobalAudioFlowBootstrap could not provide a persistent BGM flow coordinator. Verify the persistent audio-flow bootstrap path; scene-global lookup is not supported.";
        private const string MissingInstallerMessage =
            "GlobalAudioFlowBootstrap requires a co-located AudioRuntimeInstaller configured for PreferRegisteredPersistentRuntime.";

        [SerializeField] private AudioRuntimeInstaller audioRuntimeInstaller;
        [SerializeField] private GlobalAudioFlowRoot persistentRoot;

        public AudioRuntimeInstaller AudioRuntimeInstaller => audioRuntimeInstaller;

        public GlobalAudioFlowRoot PersistentRoot => persistentRoot;

        public IBgmFlowCoordinator Coordinator => persistentRoot?.Coordinator;

        public BgmRequestRouter RequestRouter => persistentRoot?.RequestRouter;

        private void Awake()
        {
            audioRuntimeInstaller ??= GetComponent<AudioRuntimeInstaller>();
            ValidateInstallerOrThrow();
            persistentRoot = GlobalAudioFlowRoot.GetOrCreate();
            audioRuntimeInstaller.Install();

            if (persistentRoot?.Coordinator == null)
            {
                throw new InvalidOperationException(MissingCoordinatorMessage);
            }
        }

        public IBgmFlowCoordinator GetCoordinatorOrThrow()
        {
            if (persistentRoot?.Coordinator == null)
            {
                throw new InvalidOperationException(MissingCoordinatorMessage);
            }

            return persistentRoot.Coordinator;
        }

        public BgmRequestRouter GetRequestRouterOrThrow()
        {
            if (persistentRoot?.RequestRouter == null)
            {
                throw new InvalidOperationException(MissingCoordinatorMessage);
            }

            return persistentRoot.RequestRouter;
        }

        private void ValidateInstallerOrThrow()
        {
            if (audioRuntimeInstaller == null ||
                audioRuntimeInstaller.BindingMode != AudioRuntimeInstallerBindingMode.PreferRegisteredPersistentRuntime)
            {
                throw new InvalidOperationException(MissingInstallerMessage);
            }
        }
    }
}
