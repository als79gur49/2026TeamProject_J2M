using System.Collections.Generic;
using Game.Feature.Gameplay.UIAccess.Models;

namespace Game.Feature.Gameplay.UIAccess.Queries
{
    public interface IGameplaySurfaceButtonRemainderQuery
    {
        IReadOnlyList<GameplaySurfaceButtonRemainderReadModel> Read();
    }
}
