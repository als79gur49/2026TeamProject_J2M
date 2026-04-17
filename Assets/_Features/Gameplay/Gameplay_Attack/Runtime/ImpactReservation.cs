using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Attack
{
    public readonly struct ImpactReservation
    {
        public ImpactReservation(
            int sourceId,
            int targetId,
            SurfaceCell impactCell,
            int damage,
            int tickGenerated)
            : this(
                sourceId,
                targetId,
                impactCell,
                damage,
                tickGenerated,
                default)
        {
        }

        internal ImpactReservation(
            int sourceId,
            int targetId,
            SurfaceCell impactCell,
            int damage,
            int tickGenerated,
            int sourceActionPlanId,
            int localActionIndex)
            : this(
                sourceId,
                targetId,
                impactCell,
                damage,
                tickGenerated,
                new InternalMetadata(sourceActionPlanId, localActionIndex))
        {
        }

        private ImpactReservation(
            int sourceId,
            int targetId,
            SurfaceCell impactCell,
            int damage,
            int tickGenerated,
            InternalMetadata metadata)
        {
            SourceId = sourceId;
            TargetId = targetId;
            ImpactCell = impactCell;
            Damage = damage;
            TickGenerated = tickGenerated;
            Metadata = metadata;
        }

        public int SourceId { get; }

        public int TargetId { get; }

        public SurfaceCell ImpactCell { get; }

        public int Damage { get; }

        public int TickGenerated { get; }

        internal int SourceActionPlanId => Metadata.SourceActionPlanId;

        internal int LocalActionIndex => Metadata.LocalActionIndex;

        internal InternalMetadata Metadata { get; }

        internal readonly struct InternalMetadata
        {
            public InternalMetadata(int sourceActionPlanId, int localActionIndex)
            {
                SourceActionPlanId = sourceActionPlanId;
                LocalActionIndex = localActionIndex;
            }

            public int SourceActionPlanId { get; }

            public int LocalActionIndex { get; }
        }
    }
}
