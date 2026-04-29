using System;

namespace Game.Feature.Stages
{
    public sealed class StageAuthoringNormalizedPresentationSnapshot
    {
        public StageAuthoringNormalizedPresentationSnapshot(
            StageAuthoringNormalizedPresentationBinding[] enemyBindings,
            StageAuthoringNormalizedPresentationBinding[] staticBindings)
        {
            EnemyBindings = enemyBindings ?? Array.Empty<StageAuthoringNormalizedPresentationBinding>();
            StaticBindings = staticBindings ?? Array.Empty<StageAuthoringNormalizedPresentationBinding>();
        }

        public StageAuthoringNormalizedPresentationBinding[] EnemyBindings { get; }

        public StageAuthoringNormalizedPresentationBinding[] StaticBindings { get; }
    }

    public readonly struct StageAuthoringNormalizedPresentationBinding
    {
        public StageAuthoringNormalizedPresentationBinding(int entityId, string presentationId)
        {
            EntityId = entityId;
            PresentationId = presentationId ?? string.Empty;
        }

        public int EntityId { get; }

        public string PresentationId { get; }
    }
}
