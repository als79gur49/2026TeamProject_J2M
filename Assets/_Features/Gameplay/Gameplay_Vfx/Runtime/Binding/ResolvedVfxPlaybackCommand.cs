namespace Game.Feature.Gameplay.Vfx
{
    public readonly struct ResolvedVfxPlaybackCommand
    {
        public ResolvedVfxPlaybackCommand(
            in GameplayVfxRequest request,
            VfxBindingRuntimePolicy policy,
            in VfxResolvedAnchor anchor)
        {
            Request = request;
            Policy = policy;
            Anchor = anchor;
        }

        public GameplayVfxRequest Request { get; }

        public VfxBindingRuntimePolicy Policy { get; }

        public VfxResolvedAnchor Anchor { get; }

        public GameplayVfxCueId CueId => Request.CueId;

        public bool IsPersistent => Request.IsPersistent;

        public VfxPersistentKey PersistentKey => Request.PersistentKey;
    }
}
