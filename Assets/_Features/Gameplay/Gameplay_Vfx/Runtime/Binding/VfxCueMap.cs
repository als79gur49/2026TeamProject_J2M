using System;
using System.Collections.Generic;
using Game.Feature.Gameplay;

namespace Game.Feature.Gameplay.Vfx
{
    public sealed class VfxCueMap
    {
        public static readonly VfxCueMap Empty = new VfxCueMap(Array.Empty<VfxBindingRuntimePolicy>());

        private readonly Dictionary<VfxBindingKey, VfxBindingRuntimePolicy> policies;

        public VfxCueMap(IEnumerable<VfxBindingRuntimePolicy> policies)
        {
            if (policies == null)
            {
                throw new ArgumentNullException(nameof(policies));
            }

            this.policies = new Dictionary<VfxBindingKey, VfxBindingRuntimePolicy>();
            foreach (var policy in policies)
            {
                policy.ValidateOrThrow();
                var key = new VfxBindingKey(policy.CueId, policy.StyleKey);
                if (this.policies.ContainsKey(key))
                {
                    throw new InvalidOperationException($"VFX cue map contains duplicate binding '{key}'.");
                }

                this.policies.Add(key, policy);
            }
        }

        public bool TryResolve(GameplayVfxCueId cueId, out VfxBindingRuntimePolicy policy)
        {
            return TryResolve(cueId, VfxStyleKey.Default, out policy);
        }

        public bool TryResolve(GameplayVfxCueId cueId, VfxStyleKey styleKey, out VfxBindingRuntimePolicy policy)
        {
            return policies.TryGetValue(new VfxBindingKey(cueId, styleKey), out policy);
        }

        public int Count => policies.Count;

        public VfxBindingRuntimePolicy ResolveOrThrow(GameplayVfxCueId cueId)
        {
            return ResolveOrThrow(cueId, VfxStyleKey.Default);
        }

        public VfxBindingRuntimePolicy ResolveOrThrow(GameplayVfxCueId cueId, VfxStyleKey styleKey)
        {
            if (TryResolve(cueId, styleKey, out var policy))
            {
                return policy;
            }

            throw new InvalidOperationException($"VFX cue map is missing binding '{new VfxBindingKey(cueId, styleKey)}'.");
        }
    }
}
