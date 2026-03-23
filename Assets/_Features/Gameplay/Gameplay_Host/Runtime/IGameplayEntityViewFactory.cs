using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Host
{
    public interface IGameplayEntityViewFactory
    {
        GameplayEntityView CreateView(in EntityState entity);
    }
}
