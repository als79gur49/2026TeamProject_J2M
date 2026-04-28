using Game.Feature.Gameplay.UIAccess.Models;

namespace Game.Feature.Gameplay.UIAccess.Queries
{
    public interface IGameplaySessionQuery
    {
        GameplaySessionReadModel Read();
    }

    public interface IGameplayStageQuery
    {
        GameplayStageReadModel Read();
    }
}
