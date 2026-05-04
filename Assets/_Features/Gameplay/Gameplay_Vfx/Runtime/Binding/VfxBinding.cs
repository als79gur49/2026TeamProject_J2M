using System;

namespace Game.Feature.Gameplay.Vfx
{
    public sealed class VfxBinding
    {
        public VfxBinding(VfxBindingRuntimePolicy policy)
        {
            policy.ValidateOrThrow();
            Policy = policy;
        }

        public VfxBindingRuntimePolicy Policy { get; }

        public GameplayVfxCueId CueId => Policy.CueId;
    }
}
