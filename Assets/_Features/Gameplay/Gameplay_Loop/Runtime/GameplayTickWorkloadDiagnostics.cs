using System;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Loop
{
    internal static class GameplayTickWorkloadDiagnostics
    {
        [ThreadStatic]
        private static Capture _current;

        internal static bool IsEnabled => _current != null;

        internal static GameplayTickWorkloadCounts Current => _current?.Counts ?? default;

        internal static GameplayTickWorkloadScope BeginCapture()
        {
            var previous = _current;
            var capture = new Capture();
            _current = capture;
            return new GameplayTickWorkloadScope(previous, capture);
        }

        internal static void RecordFinalEntityEnumeration(int itemCount)
        {
            _current?.RecordFinalEntityEnumeration(itemCount);
        }

        internal static void RecordFinalEntityDefensiveCopy(int itemCount)
        {
            _current?.RecordFinalEntityDefensiveCopy(itemCount);
        }

        internal static void RecordOwnedFinalEntityWrapperCreated(int itemCount)
        {
            _current?.RecordOwnedFinalEntityWrapperCreated(itemCount);
        }

        internal static void RecordTickResultFinalEntitiesShared(int itemCount)
        {
            _current?.RecordTickResultFinalEntitiesShared(itemCount);
        }

        internal static void RecordEntityLogicCandidateCacheConstructed()
        {
            _current?.RecordEntityLogicCandidateCacheConstructed();
        }

        internal static void RecordEntityLogicBuild(in EntityLogicBuildMetrics metrics)
        {
            _current?.RecordEntityLogicBuild(metrics);
        }

        internal sealed class Capture
        {
            private int _entityLogicCandidateCacheConstructionCount;
            private EntityLogicBuildMetrics _entityLogicBuildMetrics;
            private int _entityLogicProviderBuildCount;
            private int _finalEntityDefensiveCopiedItemCount;
            private int _finalEntityDefensiveCopyCount;
            private int _finalEntityEnumeratedItemCount;
            private int _finalEntityEnumerationCount;
            private int _finalEntityOwnedWrapperCreationCount;
            private int _finalEntityOwnedWrapperItemCount;
            private int _tickResultFinalEntityTrustedShareCount;
            private int _tickResultFinalEntityTrustedSharedItemCount;

            internal GameplayTickWorkloadCounts Counts => new(
                _finalEntityEnumerationCount,
                _finalEntityEnumeratedItemCount,
                _finalEntityDefensiveCopyCount,
                _finalEntityDefensiveCopiedItemCount,
                _finalEntityOwnedWrapperCreationCount,
                _finalEntityOwnedWrapperItemCount,
                _tickResultFinalEntityTrustedShareCount,
                _tickResultFinalEntityTrustedSharedItemCount,
                _entityLogicCandidateCacheConstructionCount,
                _entityLogicProviderBuildCount,
                _entityLogicBuildMetrics);

            internal void RecordFinalEntityEnumeration(int itemCount)
            {
                _finalEntityEnumerationCount++;
                _finalEntityEnumeratedItemCount += itemCount;
            }

            internal void RecordFinalEntityDefensiveCopy(int itemCount)
            {
                _finalEntityDefensiveCopyCount++;
                _finalEntityDefensiveCopiedItemCount += itemCount;
            }

            internal void RecordOwnedFinalEntityWrapperCreated(int itemCount)
            {
                _finalEntityOwnedWrapperCreationCount++;
                _finalEntityOwnedWrapperItemCount += itemCount;
            }

            internal void RecordTickResultFinalEntitiesShared(int itemCount)
            {
                _tickResultFinalEntityTrustedShareCount++;
                _tickResultFinalEntityTrustedSharedItemCount += itemCount;
            }

            internal void RecordEntityLogicCandidateCacheConstructed()
            {
                _entityLogicCandidateCacheConstructionCount++;
            }

            internal void RecordEntityLogicBuild(in EntityLogicBuildMetrics metrics)
            {
                _entityLogicProviderBuildCount++;
                _entityLogicBuildMetrics = EntityLogicBuildMetrics.Add(_entityLogicBuildMetrics, metrics);
            }
        }

        internal readonly struct GameplayTickWorkloadScope : IDisposable
        {
            private readonly Capture _previous;
            private readonly Capture _capture;

            internal GameplayTickWorkloadScope(Capture previous, Capture capture)
            {
                _previous = previous;
                _capture = capture;
            }

            public GameplayTickWorkloadCounts Counts => _capture?.Counts ?? default;

            public void Dispose()
            {
                if (_current == _capture)
                {
                    _current = _previous;
                }
            }
        }
    }

    internal readonly struct GameplayTickWorkloadCounts
    {
        public GameplayTickWorkloadCounts(
            int finalEntityEnumerationCount,
            int finalEntityEnumeratedItemCount,
            int finalEntityDefensiveCopyCount,
            int finalEntityDefensiveCopiedItemCount,
            int finalEntityOwnedWrapperCreationCount,
            int finalEntityOwnedWrapperItemCount,
            int tickResultFinalEntityTrustedShareCount,
            int tickResultFinalEntityTrustedSharedItemCount,
            int entityLogicCandidateCacheConstructionCount,
            int entityLogicProviderBuildCount,
            in EntityLogicBuildMetrics entityLogicBuildMetrics)
        {
            FinalEntityEnumerationCount = finalEntityEnumerationCount;
            FinalEntityEnumeratedItemCount = finalEntityEnumeratedItemCount;
            FinalEntityDefensiveCopyCount = finalEntityDefensiveCopyCount;
            FinalEntityDefensiveCopiedItemCount = finalEntityDefensiveCopiedItemCount;
            FinalEntityOwnedWrapperCreationCount = finalEntityOwnedWrapperCreationCount;
            FinalEntityOwnedWrapperItemCount = finalEntityOwnedWrapperItemCount;
            TickResultFinalEntityTrustedShareCount = tickResultFinalEntityTrustedShareCount;
            TickResultFinalEntityTrustedSharedItemCount = tickResultFinalEntityTrustedSharedItemCount;
            EntityLogicCandidateCacheConstructionCount = entityLogicCandidateCacheConstructionCount;
            EntityLogicProviderBuildCount = entityLogicProviderBuildCount;
            EntityLogicBuildMetrics = entityLogicBuildMetrics;
        }

        public int FinalEntityEnumerationCount { get; }
        public int FinalEntityEnumeratedItemCount { get; }
        public int FinalEntityDefensiveCopyCount { get; }
        public int FinalEntityDefensiveCopiedItemCount { get; }
        public int FinalEntityOwnedWrapperCreationCount { get; }
        public int FinalEntityOwnedWrapperItemCount { get; }
        public int TickResultFinalEntityTrustedShareCount { get; }
        public int TickResultFinalEntityTrustedSharedItemCount { get; }
        public int EntityLogicCandidateCacheConstructionCount { get; }
        public int EntityLogicProviderBuildCount { get; }
        public EntityLogicBuildMetrics EntityLogicBuildMetrics { get; }
    }

    internal readonly struct EntityLogicBuildMetrics
    {
        public EntityLogicBuildMetrics(
            int entityVisitedCount,
            int registeredFactoryCount,
            int factoryOpportunityCount,
            int prefilterSkipCount,
            int canCreateProbeCount,
            int createdLogicCount,
            int acceptedLogicCount,
            int conflictRejectedLogicCount,
            in EntityLogicTypeMetrics none,
            in EntityLogicTypeMetrics unit,
            in EntityLogicTypeMetrics box,
            in EntityLogicTypeMetrics unknown)
        {
            EntityVisitedCount = entityVisitedCount;
            RegisteredFactoryCount = registeredFactoryCount;
            FactoryOpportunityCount = factoryOpportunityCount;
            PrefilterSkipCount = prefilterSkipCount;
            CanCreateProbeCount = canCreateProbeCount;
            CreatedLogicCount = createdLogicCount;
            AcceptedLogicCount = acceptedLogicCount;
            ConflictRejectedLogicCount = conflictRejectedLogicCount;
            None = none;
            Unit = unit;
            Box = box;
            Unknown = unknown;
        }

        public int EntityVisitedCount { get; }
        public int RegisteredFactoryCount { get; }
        public int FactoryOpportunityCount { get; }
        public int PrefilterSkipCount { get; }
        public int CanCreateProbeCount { get; }
        public int CreatedLogicCount { get; }
        public int AcceptedLogicCount { get; }
        public int ConflictRejectedLogicCount { get; }
        public EntityLogicTypeMetrics None { get; }
        public EntityLogicTypeMetrics Unit { get; }
        public EntityLogicTypeMetrics Box { get; }
        public EntityLogicTypeMetrics Unknown { get; }

        internal static EntityLogicBuildMetrics Add(
            in EntityLogicBuildMetrics left,
            in EntityLogicBuildMetrics right)
        {
            return new EntityLogicBuildMetrics(
                left.EntityVisitedCount + right.EntityVisitedCount,
                left.RegisteredFactoryCount + right.RegisteredFactoryCount,
                left.FactoryOpportunityCount + right.FactoryOpportunityCount,
                left.PrefilterSkipCount + right.PrefilterSkipCount,
                left.CanCreateProbeCount + right.CanCreateProbeCount,
                left.CreatedLogicCount + right.CreatedLogicCount,
                left.AcceptedLogicCount + right.AcceptedLogicCount,
                left.ConflictRejectedLogicCount + right.ConflictRejectedLogicCount,
                EntityLogicTypeMetrics.Add(left.None, right.None),
                EntityLogicTypeMetrics.Add(left.Unit, right.Unit),
                EntityLogicTypeMetrics.Add(left.Box, right.Box),
                EntityLogicTypeMetrics.Add(left.Unknown, right.Unknown));
        }
    }

    internal readonly struct EntityLogicTypeMetrics
    {
        public EntityLogicTypeMetrics(
            int entityVisitedCount,
            int factoryOpportunityCount,
            int prefilterSkipCount,
            int canCreateProbeCount,
            int createdLogicCount,
            int acceptedLogicCount,
            int conflictRejectedLogicCount)
        {
            EntityVisitedCount = entityVisitedCount;
            FactoryOpportunityCount = factoryOpportunityCount;
            PrefilterSkipCount = prefilterSkipCount;
            CanCreateProbeCount = canCreateProbeCount;
            CreatedLogicCount = createdLogicCount;
            AcceptedLogicCount = acceptedLogicCount;
            ConflictRejectedLogicCount = conflictRejectedLogicCount;
        }

        public int EntityVisitedCount { get; }
        public int FactoryOpportunityCount { get; }
        public int PrefilterSkipCount { get; }
        public int CanCreateProbeCount { get; }
        public int CreatedLogicCount { get; }
        public int AcceptedLogicCount { get; }
        public int ConflictRejectedLogicCount { get; }

        internal static EntityLogicTypeMetrics Add(
            in EntityLogicTypeMetrics left,
            in EntityLogicTypeMetrics right)
        {
            return new EntityLogicTypeMetrics(
                left.EntityVisitedCount + right.EntityVisitedCount,
                left.FactoryOpportunityCount + right.FactoryOpportunityCount,
                left.PrefilterSkipCount + right.PrefilterSkipCount,
                left.CanCreateProbeCount + right.CanCreateProbeCount,
                left.CreatedLogicCount + right.CreatedLogicCount,
                left.AcceptedLogicCount + right.AcceptedLogicCount,
                left.ConflictRejectedLogicCount + right.ConflictRejectedLogicCount);
        }
    }

    internal struct EntityLogicBuildMetricsAccumulator
    {
        private readonly int _registeredFactoryCount;
        private int _entityVisitedCount;
        private int _factoryOpportunityCount;
        private int _prefilterSkipCount;
        private int _canCreateProbeCount;
        private int _createdLogicCount;
        private int _acceptedLogicCount;
        private int _conflictRejectedLogicCount;
        private EntityLogicTypeMetricsAccumulator _none;
        private EntityLogicTypeMetricsAccumulator _unit;
        private EntityLogicTypeMetricsAccumulator _box;
        private EntityLogicTypeMetricsAccumulator _unknown;

        public EntityLogicBuildMetricsAccumulator(int registeredFactoryCount)
        {
            _registeredFactoryCount = registeredFactoryCount;
            _entityVisitedCount = 0;
            _factoryOpportunityCount = 0;
            _prefilterSkipCount = 0;
            _canCreateProbeCount = 0;
            _createdLogicCount = 0;
            _acceptedLogicCount = 0;
            _conflictRejectedLogicCount = 0;
            _none = default;
            _unit = default;
            _box = default;
            _unknown = default;
        }

        public void RecordEntityVisited(EntityType entityType)
        {
            _entityVisitedCount++;
            _factoryOpportunityCount += _registeredFactoryCount;
            Record(entityType, EntityLogicMetricEvent.EntityVisited, _registeredFactoryCount);
        }

        public void RecordPrefilterSkip(EntityType entityType)
        {
            _prefilterSkipCount++;
            Record(entityType, EntityLogicMetricEvent.PrefilterSkip);
        }

        public void RecordCanCreateProbe(EntityType entityType)
        {
            _canCreateProbeCount++;
            Record(entityType, EntityLogicMetricEvent.CanCreateProbe);
        }

        public void RecordCreated(EntityType entityType)
        {
            _createdLogicCount++;
            Record(entityType, EntityLogicMetricEvent.Created);
        }

        public void RecordAccepted(EntityType entityType)
        {
            _acceptedLogicCount++;
            Record(entityType, EntityLogicMetricEvent.Accepted);
        }

        public void RecordConflictRejected(EntityType entityType)
        {
            _conflictRejectedLogicCount++;
            Record(entityType, EntityLogicMetricEvent.ConflictRejected);
        }

        public EntityLogicBuildMetrics Build()
        {
            return new EntityLogicBuildMetrics(
                _entityVisitedCount,
                _registeredFactoryCount,
                _factoryOpportunityCount,
                _prefilterSkipCount,
                _canCreateProbeCount,
                _createdLogicCount,
                _acceptedLogicCount,
                _conflictRejectedLogicCount,
                _none.Build(),
                _unit.Build(),
                _box.Build(),
                _unknown.Build());
        }

        private void Record(
            EntityType entityType,
            EntityLogicMetricEvent metricEvent,
            int registeredFactoryCount = 0)
        {
            if (entityType == EntityType.None)
            {
                Record(ref _none, metricEvent, registeredFactoryCount);
                return;
            }

            if (entityType == EntityType.Unit)
            {
                Record(ref _unit, metricEvent, registeredFactoryCount);
                return;
            }

            if (entityType == EntityType.Box)
            {
                Record(ref _box, metricEvent, registeredFactoryCount);
                return;
            }

            Record(ref _unknown, metricEvent, registeredFactoryCount);
        }

        private static void Record(
            ref EntityLogicTypeMetricsAccumulator accumulator,
            EntityLogicMetricEvent metricEvent,
            int registeredFactoryCount)
        {
            switch (metricEvent)
            {
                case EntityLogicMetricEvent.EntityVisited:
                    accumulator.RecordEntityVisited(registeredFactoryCount);
                    break;
                case EntityLogicMetricEvent.PrefilterSkip:
                    accumulator.RecordPrefilterSkip();
                    break;
                case EntityLogicMetricEvent.CanCreateProbe:
                    accumulator.RecordCanCreateProbe();
                    break;
                case EntityLogicMetricEvent.Created:
                    accumulator.RecordCreated();
                    break;
                case EntityLogicMetricEvent.Accepted:
                    accumulator.RecordAccepted();
                    break;
                case EntityLogicMetricEvent.ConflictRejected:
                    accumulator.RecordConflictRejected();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(metricEvent), metricEvent, null);
            }
        }

        private enum EntityLogicMetricEvent
        {
            EntityVisited = 0,
            PrefilterSkip = 1,
            CanCreateProbe = 2,
            Created = 3,
            Accepted = 4,
            ConflictRejected = 5,
        }
    }

    internal struct EntityLogicTypeMetricsAccumulator
    {
        private int _entityVisitedCount;
        private int _factoryOpportunityCount;
        private int _prefilterSkipCount;
        private int _canCreateProbeCount;
        private int _createdLogicCount;
        private int _acceptedLogicCount;
        private int _conflictRejectedLogicCount;

        public void RecordEntityVisited(int registeredFactoryCount)
        {
            _entityVisitedCount++;
            _factoryOpportunityCount += registeredFactoryCount;
        }

        public void RecordPrefilterSkip() => _prefilterSkipCount++;
        public void RecordCanCreateProbe() => _canCreateProbeCount++;
        public void RecordCreated() => _createdLogicCount++;
        public void RecordAccepted() => _acceptedLogicCount++;
        public void RecordConflictRejected() => _conflictRejectedLogicCount++;

        public EntityLogicTypeMetrics Build()
        {
            return new EntityLogicTypeMetrics(
                _entityVisitedCount,
                _factoryOpportunityCount,
                _prefilterSkipCount,
                _canCreateProbeCount,
                _createdLogicCount,
                _acceptedLogicCount,
                _conflictRejectedLogicCount);
        }
    }
}
