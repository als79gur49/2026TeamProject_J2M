using System;

namespace Game.Feature.Gameplay.Entities
{
    [Serializable]
    public struct EnemyAiProfileOverride
    {
        public int EntityId;
        public EnemyAiProfile Profile;
    }
}
