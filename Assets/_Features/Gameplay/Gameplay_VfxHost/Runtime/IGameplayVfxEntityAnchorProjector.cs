namespace Game.Feature.Gameplay.Vfx.Host
{
    public interface IGameplayVfxEntityAnchorProjector
    {
        bool TryResolveEntity(
            int entityId,
            VfxAnchorSlot slot,
            out VfxResolvedAnchor resolvedAnchor);
    }
}
