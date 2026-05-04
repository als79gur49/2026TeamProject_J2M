using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.Vfx
{
    public sealed class CompositeVfxBindingResolver : IVfxBindingResolver
    {
        private readonly VfxCueMap hostDefaultMap;
        private readonly IReadOnlyDictionary<GameplayVfxFamily, VfxProfile> familyProfiles;

        public CompositeVfxBindingResolver(
            VfxCueMap hostDefaultMap,
            IReadOnlyDictionary<GameplayVfxFamily, VfxProfile> familyProfiles)
        {
            this.hostDefaultMap = hostDefaultMap ?? VfxCueMap.Empty;
            this.familyProfiles = familyProfiles
                ?? new Dictionary<GameplayVfxFamily, VfxProfile>();
        }

        public bool TryResolve(in GameplayVfxRequest request, out VfxBindingRuntimePolicy policy)
        {
            if (familyProfiles.TryGetValue(request.CueId.Family, out var profile)
                && profile != null
                && profile.TryResolve(request.CueId, out policy))
            {
                return true;
            }

            return hostDefaultMap.TryResolve(request.CueId, out policy);
        }
    }
}
