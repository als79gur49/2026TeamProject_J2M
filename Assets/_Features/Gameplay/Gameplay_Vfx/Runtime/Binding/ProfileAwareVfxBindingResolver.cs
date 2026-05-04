namespace Game.Feature.Gameplay.Vfx
{
    public sealed class ProfileAwareVfxBindingResolver : IVfxBindingResolver
    {
        private readonly IGameplayVfxProfileProvider profileProvider;
        private readonly VfxCueMap hostDefaultMap;

        public ProfileAwareVfxBindingResolver(
            IGameplayVfxProfileProvider profileProvider,
            VfxCueMap hostDefaultMap)
        {
            this.profileProvider = profileProvider;
            this.hostDefaultMap = hostDefaultMap ?? VfxCueMap.Empty;
        }

        public bool TryResolve(in GameplayVfxRequest request, out VfxBindingRuntimePolicy policy)
        {
            if (request.SourceEntityId > 0
                && profileProvider != null
                && profileProvider.TryResolveProfileForRequest(request, out var profile)
                && profile != null
                && profile.TryResolve(request.CueId, out policy))
            {
                return true;
            }

            return hostDefaultMap.TryResolve(request.CueId, out policy);
        }
    }
}
