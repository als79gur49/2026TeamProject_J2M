namespace Game.Feature.Gameplay.Vfx
{
    public sealed class ProfileAwareVfxBindingResolver : IVfxBindingResolver
    {
        private readonly IGameplayVfxProfileProvider profileProvider;
        private readonly IVfxBindingResolver fallbackResolver;

        public ProfileAwareVfxBindingResolver(
            IGameplayVfxProfileProvider profileProvider,
            VfxCueMap hostDefaultMap)
            : this(profileProvider, new VfxCueMapBindingResolver(hostDefaultMap))
        {
        }

        public ProfileAwareVfxBindingResolver(
            IGameplayVfxProfileProvider profileProvider,
            IVfxBindingResolver fallbackResolver)
        {
            this.profileProvider = profileProvider;
            this.fallbackResolver = fallbackResolver ?? new VfxCueMapBindingResolver(VfxCueMap.Empty);
        }

        public bool TryResolve(in GameplayVfxRequest request, out VfxBindingRuntimePolicy policy)
        {
            if (request.SourceEntityId > 0
                && profileProvider != null
                && profileProvider.TryResolveProfileForRequest(request, out var profile)
                && profile != null
                && profile.TryResolve(request.CueId, request.StyleKey, out policy))
            {
                return true;
            }

            return fallbackResolver.TryResolve(request, out policy);
        }

        private sealed class VfxCueMapBindingResolver : IVfxBindingResolver
        {
            private readonly VfxCueMap map;

            public VfxCueMapBindingResolver(VfxCueMap map)
            {
                this.map = map ?? VfxCueMap.Empty;
            }

            public bool TryResolve(in GameplayVfxRequest request, out VfxBindingRuntimePolicy policy)
            {
                return map.TryResolve(request.CueId, request.StyleKey, out policy);
            }
        }
    }
}
