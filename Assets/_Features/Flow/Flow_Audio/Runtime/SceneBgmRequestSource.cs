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

            bootstrap.GetRequestRouterOrThrow().Submit(BgmFlowRequest.ProfileRequest(
                BgmRequestSourceKind.SceneDefault,
                BgmRequestPriority.SceneDefault,
                profile));
        }
    }
}
