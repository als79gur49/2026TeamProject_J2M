using System;
using Game.Feature.Gameplay.Entities;

namespace Game.Feature.Gameplay.Host
{
    [Serializable]
    public struct EnemyAiProfileOverride
    {
        public int EntityId;
        public EnemyAiProfile Profile;
    }
}
