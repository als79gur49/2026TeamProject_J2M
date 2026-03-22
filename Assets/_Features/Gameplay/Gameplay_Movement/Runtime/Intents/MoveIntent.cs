using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Movement.Intents
{
    public sealed class MoveIntent : Intent
    {
        public MoveIntent(int sourceId, int priority)
            : base(sourceId, priority, TickPhase.Movement)
        {
        }
    }
}
