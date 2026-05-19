namespace Game.Feature.Gameplay.Vfx
{
    public interface IVfxAnchorResolver
    {
        bool TryResolve(
            in GameplayVfxRequest request,
            VfxBindingRuntimePolicy policy,
            out VfxResolvedAnchor resolvedAnchor);
    }
}
