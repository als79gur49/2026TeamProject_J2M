namespace Game.Feature.Gameplay.Vfx.Host
{
    public interface IGameplayVfxMotionAnchorProjector
    {
        bool TryResolveMotionTrack(
            int motionTrackId,
            VfxAnchorSlot slot,
            out VfxResolvedAnchor resolvedAnchor);
    }
}
