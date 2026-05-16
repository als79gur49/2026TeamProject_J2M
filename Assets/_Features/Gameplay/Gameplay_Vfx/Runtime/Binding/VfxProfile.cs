using System;
using System.Collections.Generic;
using Game.Feature.Gameplay;

namespace Game.Feature.Gameplay.Vfx
{
    public sealed class VfxProfile
    {
        private readonly VfxCueMap cueMap;

        public VfxProfile(GameplayVfxFamily family, IEnumerable<VfxBindingRuntimePolicy> policies)
        {
            if (family == GameplayVfxFamily.None)
            {
                throw new ArgumentException("VFX profile family cannot be None.", nameof(family));
            }

            if (policies == null)
            {
                throw new ArgumentNullException(nameof(policies));
            }

            Family = family;
            var profilePolicies = new List<VfxBindingRuntimePolicy>();
            foreach (var policy in policies)
            {
                if (policy.CueId.Family != family)
                {
                    throw new InvalidOperationException(
                        $"VFX profile family '{family}' cannot contain cue '{policy.CueId}'.");
                }

                profilePolicies.Add(policy);
            }

            cueMap = new VfxCueMap(profilePolicies);
        }

        public GameplayVfxFamily Family { get; }

        public bool TryResolve(GameplayVfxCueId cueId, out VfxBindingRuntimePolicy policy)
        {
            return TryResolve(cueId, VfxStyleKey.Default, out policy);
        }

        public bool TryResolve(GameplayVfxCueId cueId, VfxStyleKey styleKey, out VfxBindingRuntimePolicy policy)
        {
            if (cueId.Family != Family)
            {
                policy = default;
                return false;
            }

            return cueMap.TryResolve(cueId, styleKey, out policy);
        }
    }
}
