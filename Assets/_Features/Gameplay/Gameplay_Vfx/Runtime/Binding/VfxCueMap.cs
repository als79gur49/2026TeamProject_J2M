using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.Vfx
{
    public sealed class VfxCueMap
    {
        public static readonly VfxCueMap Empty = new VfxCueMap(Array.Empty<VfxBindingRuntimePolicy>());

        private readonly Dictionary<GameplayVfxCueId, VfxBindingRuntimePolicy> policies;

        public VfxCueMap(IEnumerable<VfxBindingRuntimePolicy> policies)
        {
            if (policies == null)
            {
                throw new ArgumentNullException(nameof(policies));
            }

            this.policies = new Dictionary<GameplayVfxCueId, VfxBindingRuntimePolicy>();
            foreach (var policy in policies)
            {
                policy.ValidateOrThrow();
                if (this.policies.ContainsKey(policy.CueId))
                {
                    throw new InvalidOperationException($"VFX cue map contains duplicate cue '{policy.CueId}'.");
                }

                this.policies.Add(policy.CueId, policy);
            }
        }

        public bool TryResolve(GameplayVfxCueId cueId, out VfxBindingRuntimePolicy policy)
        {
            return policies.TryGetValue(cueId, out policy);
        }

        public VfxBindingRuntimePolicy ResolveOrThrow(GameplayVfxCueId cueId)
        {
            if (TryResolve(cueId, out var policy))
            {
                return policy;
            }

            throw new InvalidOperationException($"VFX cue map is missing cue '{cueId}'.");
        }
    }
}
