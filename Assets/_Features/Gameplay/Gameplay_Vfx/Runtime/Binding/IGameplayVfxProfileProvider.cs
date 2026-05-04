namespace Game.Feature.Gameplay.Vfx
{
    public interface IGameplayVfxProfileProvider
    {
        bool TryResolveProfileForRequest(
            in GameplayVfxRequest request,
            out VfxProfile profile);
    }
}
