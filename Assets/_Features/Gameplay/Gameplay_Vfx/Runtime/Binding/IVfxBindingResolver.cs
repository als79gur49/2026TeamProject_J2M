namespace Game.Feature.Gameplay.Vfx
{
    public interface IVfxBindingResolver
    {
        bool TryResolve(in GameplayVfxRequest request, out VfxBindingRuntimePolicy policy);
    }
}
