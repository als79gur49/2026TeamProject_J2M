using UnityEngine;
using Game.Feature.Gameplay;

namespace Game.Feature.Gameplay.Vfx.Authoring
{
    [CreateAssetMenu(menuName = "Game/VFX/VFX Binding Definition")]
    public sealed class VfxBindingDefinitionAsset : ScriptableObject
    {
#pragma warning disable 0649
        [SerializeField] private GameplayVfxFamily family;
        [SerializeField] private int cueCode;
        [SerializeField] private VfxStyleKey styleKey;
        [SerializeField] private GameObject prefab;
        [SerializeField] private VfxBindingRequirement requirement;
        [SerializeField] private VfxMissingAnchorPolicy missingAnchorPolicy;
        [SerializeField] private VfxPlaybackMode playbackMode;
        [SerializeField] private VfxStopPolicy stopPolicy;
        [SerializeField] private float defaultLifetimeSeconds;
        [SerializeField] private float tailSeconds;
        [SerializeField] private int initialPoolSize;
        [SerializeField] private int maxConcurrentInstances;
#pragma warning restore 0649

        public GameplayVfxCueId CueId => new GameplayVfxCueId(family, cueCode);

        public VfxStyleKey StyleKey => styleKey;

        public VfxBindingKey BindingKey => new(CueId, styleKey);

        public GameObject Prefab => prefab;

        public VfxBindingRequirement Requirement => requirement;

        public VfxMissingAnchorPolicy MissingAnchorPolicy => missingAnchorPolicy;

        public VfxPlaybackMode PlaybackMode => playbackMode;

        public VfxStopPolicy StopPolicy => stopPolicy;

        public float DefaultLifetimeSeconds => defaultLifetimeSeconds;

        public float TailSeconds => tailSeconds;

        public int InitialPoolSize => initialPoolSize;

        public int MaxConcurrentInstances => maxConcurrentInstances;

        private void OnValidate()
        {
            LogValidationMessages(ValidateAuthoring());
        }

        public VfxBindingRuntimePolicy BuildRuntimePolicy()
        {
            ValidateAuthoring().ThrowIfErrors();
            return CreateRuntimePolicy();
        }

        public VfxAuthoringValidationResult ValidateAuthoring()
        {
            return VfxBindingDiagnostics.ValidateBinding(this);
        }

        internal VfxBindingRuntimePolicy CreateRuntimePolicy()
        {
            return new VfxBindingRuntimePolicy(
                CueId,
                requirement,
                missingAnchorPolicy,
                playbackMode,
                stopPolicy,
                defaultLifetimeSeconds,
                tailSeconds,
                maxConcurrentInstances,
                styleKey);
        }

        private void LogValidationMessages(VfxAuthoringValidationResult result)
        {
            for (var i = 0; i < result.Messages.Count; i++)
            {
                var message = result.Messages[i];
                if (message.Severity == VfxAuthoringValidationSeverity.Error)
                {
                    UnityEngine.Debug.LogError(message.ToString(), message.Context != null ? message.Context : this);
                }
                else if (message.Severity == VfxAuthoringValidationSeverity.Warning)
                {
                    UnityEngine.Debug.LogWarning(message.ToString(), message.Context != null ? message.Context : this);
                }
            }
        }
    }
}
