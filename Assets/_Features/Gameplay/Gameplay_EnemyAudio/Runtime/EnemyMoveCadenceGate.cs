using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.EnemyAudio
{
    internal sealed class EnemyMoveCadenceGate
    {
        internal const float DefaultPerEntityMoveMinIntervalSeconds = 7f;
        internal const float DefaultGlobalMoveMinIntervalSeconds = 3f;
        internal const float DefaultJitterSeconds = 1f;
        internal const int DefaultMaxMoveRequestsPerTick = 1;

        private readonly Dictionary<int, int> _nextAllowedTickByEntityId = new();

        private int _perEntityMoveMinIntervalTicks;
        private int _globalMoveMinIntervalTicks;
        private int _jitterTicks;
        private int _maxMoveRequestsPerTick;
        private int _nextGlobalMoveTick;
        private int _lastBudgetTick = int.MinValue;
        private int _moveRequestsPlayedThisTick;

        public EnemyMoveCadenceGate()
        {
            Configure(simulationTicksPerSecond: 20);
        }

        internal EnemyMoveCadenceGate(
            int perEntityMoveMinIntervalTicks,
            int globalMoveMinIntervalTicks,
            int jitterTicks,
            int maxMoveRequestsPerTick)
        {
            ConfigureTicks(
                perEntityMoveMinIntervalTicks,
                globalMoveMinIntervalTicks,
                jitterTicks,
                maxMoveRequestsPerTick);
        }

        internal int PerEntityMoveMinIntervalTicks => _perEntityMoveMinIntervalTicks;

        internal int GlobalMoveMinIntervalTicks => _globalMoveMinIntervalTicks;

        internal int JitterTicks => _jitterTicks;

        internal int MaxMoveRequestsPerTick => _maxMoveRequestsPerTick;

        public void Configure(int simulationTicksPerSecond)
        {
            if (simulationTicksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(simulationTicksPerSecond),
                    "Simulation tick rate must be greater than zero.");
            }

            ConfigureTicks(
                SecondsToCeilTicks(DefaultPerEntityMoveMinIntervalSeconds, simulationTicksPerSecond),
                SecondsToCeilTicks(DefaultGlobalMoveMinIntervalSeconds, simulationTicksPerSecond),
                SecondsToCeilTicks(DefaultJitterSeconds, simulationTicksPerSecond, allowZero: true),
                DefaultMaxMoveRequestsPerTick);
        }

        internal void ConfigureTicks(
            int perEntityMoveMinIntervalTicks,
            int globalMoveMinIntervalTicks,
            int jitterTicks,
            int maxMoveRequestsPerTick)
        {
            if (perEntityMoveMinIntervalTicks < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(perEntityMoveMinIntervalTicks),
                    "Per-entity interval ticks must be zero or greater.");
            }

            if (globalMoveMinIntervalTicks < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(globalMoveMinIntervalTicks),
                    "Global interval ticks must be zero or greater.");
            }

            if (jitterTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(jitterTicks), "Jitter ticks must be zero or greater.");
            }

            if (maxMoveRequestsPerTick < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxMoveRequestsPerTick),
                    "Max move requests per tick must be zero or greater.");
            }

            _perEntityMoveMinIntervalTicks = perEntityMoveMinIntervalTicks;
            _globalMoveMinIntervalTicks = globalMoveMinIntervalTicks;
            _jitterTicks = jitterTicks;
            _maxMoveRequestsPerTick = maxMoveRequestsPerTick;
            ResetState();
        }

        public void ResetState()
        {
            _nextAllowedTickByEntityId.Clear();
            _nextGlobalMoveTick = 0;
            _lastBudgetTick = int.MinValue;
            _moveRequestsPlayedThisTick = 0;
        }

        public bool ShouldPlayMove(int ownerEntityId, int tickIndex)
        {
            if (ownerEntityId <= 0)
            {
                return false;
            }

            RefreshTickBudget(tickIndex);

            if (_maxMoveRequestsPerTick <= 0 ||
                _moveRequestsPlayedThisTick >= _maxMoveRequestsPerTick ||
                tickIndex < _nextGlobalMoveTick)
            {
                return false;
            }

            if (_nextAllowedTickByEntityId.TryGetValue(ownerEntityId, out var nextEntityMoveTick) &&
                tickIndex < nextEntityMoveTick)
            {
                return false;
            }

            var jitter = ComputeDeterministicJitterTicks(ownerEntityId, tickIndex, _jitterTicks);
            _nextAllowedTickByEntityId[ownerEntityId] = tickIndex + _perEntityMoveMinIntervalTicks + jitter;
            _nextGlobalMoveTick = tickIndex + _globalMoveMinIntervalTicks + jitter;
            _moveRequestsPlayedThisTick++;
            return true;
        }

        internal static int ComputeDeterministicJitterTicks(int ownerEntityId, int tickIndex, int jitterTicks)
        {
            if (jitterTicks <= 0)
            {
                return 0;
            }

            return (int)(ComputeHash(ownerEntityId, tickIndex) % (uint)(jitterTicks + 1));
        }

        private void RefreshTickBudget(int tickIndex)
        {
            if (_lastBudgetTick == tickIndex)
            {
                return;
            }

            _lastBudgetTick = tickIndex;
            _moveRequestsPlayedThisTick = 0;
        }

        private static int SecondsToCeilTicks(float seconds, int simulationTicksPerSecond, bool allowZero = false)
        {
            if (seconds < 0f ||
                float.IsNaN(seconds) ||
                float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), "Seconds must be zero or greater.");
            }

            if (allowZero && seconds <= 0f)
            {
                return 0;
            }

            const float floatingPointTolerance = 0.0001f;
            return Math.Max(1, (int)Math.Ceiling((seconds * simulationTicksPerSecond) - floatingPointTolerance));
        }

        private static uint ComputeHash(int ownerEntityId, int tickIndex)
        {
            unchecked
            {
                var hash = 2166136261u;
                hash = (hash ^ (uint)ownerEntityId) * 16777619u;
                hash = (hash ^ (uint)tickIndex) * 16777619u;
                hash = (hash ^ 0x9e3779b9u) * 16777619u;
                return hash;
            }
        }
    }
}
