using System;
using Game.Feature.Gameplay.AudioPolicy;

namespace Game.Feature.Gameplay.EnemyAudio
{
    internal sealed class EnemyStationaryActiveCadenceGate
    {
        internal const float DefaultPerEntityMinIntervalSeconds = 7f;
        internal const float DefaultGlobalMinIntervalSeconds = 3f;
        internal const int DefaultMaxRequestsPerTick = 1;

        private readonly AudioVoicePolicyGate _policyGate = new();

        private int _simulationTicksPerSecond;
        private AudioVoicePolicy _policy = AudioVoicePolicy.None;

        public EnemyStationaryActiveCadenceGate()
        {
            Configure(simulationTicksPerSecond: 20);
        }

        internal AudioVoicePolicy Policy => _policy;

        public void Configure(int simulationTicksPerSecond)
        {
            if (simulationTicksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(simulationTicksPerSecond),
                    "Simulation tick rate must be greater than zero.");
            }

            _simulationTicksPerSecond = simulationTicksPerSecond;
            _policy = new AudioVoicePolicy(
                AudioVoiceGroupId.GenericGameplay,
                priority: 40,
                maxVoicesGlobal: DefaultMaxRequestsPerTick,
                maxVoicesPerOwner: 1,
                cooldownSecondsGlobal: DefaultGlobalMinIntervalSeconds,
                cooldownSecondsPerOwner: DefaultPerEntityMinIntervalSeconds,
                duplicateWindowSeconds: 0f,
                overflowMode: VoiceOverflowMode.DropNewest);
            ResetState();
        }

        public void ResetState()
        {
            _policyGate.Reset();
        }

        public bool ShouldPlayStationaryActive(int ownerEntityId, int tickIndex)
        {
            if (ownerEntityId <= 0)
            {
                return false;
            }

            return _policyGate.ShouldAccept(
                _policy,
                ownerEntityId,
                tickIndex,
                _simulationTicksPerSecond);
        }
    }
}
