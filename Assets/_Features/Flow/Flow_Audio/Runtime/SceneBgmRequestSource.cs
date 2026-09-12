using System;
using Game.Shared.Audio;
using UnityEngine;

namespace Game.Feature.Flow.Audio
{
    [DisallowMultipleComponent]
    public sealed class SceneBgmRequestSource : MonoBehaviour
    {
        [SerializeField] private GlobalAudioFlowBootstrap bootstrap;
        [SerializeField] private BgmProfile profile;

        private BgmRequestLease requestLease;

        public GlobalAudioFlowBootstrap Bootstrap => bootstrap;

        public BgmProfile Profile => profile;

        private void Start()
        {
            if (profile == null)
            {
                return;
            }

            if (bootstrap == null)
            {
                throw new InvalidOperationException(
                    "SceneBgmRequestSource requires a serialized GlobalAudioFlowBootstrap reference when a BgmProfile is assigned.");
            }

            var acquiredLease = bootstrap.GetRequestRouterOrThrow().Acquire(BgmFlowRequest.ProfileRequest(
                BgmRequestSourceKind.SceneDefault,
                BgmRequestPriority.SceneDefault,
                profile));
            var previousLease = requestLease;
            requestLease = acquiredLease;
            previousLease?.Dispose();
        }

        private void OnDestroy()
        {
            var lease = requestLease;
            requestLease = null;
            lease?.Dispose();
        }
    }
}
