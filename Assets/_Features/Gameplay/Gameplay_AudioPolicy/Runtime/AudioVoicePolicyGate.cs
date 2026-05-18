using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.AudioPolicy
{
    public sealed class AudioVoicePolicyGate
    {
        private readonly Dictionary<AudioVoiceGroupId, int> _nextGlobalTickByGroup = new();
        private readonly Dictionary<OwnerGroupKey, int> _nextOwnerTickByGroup = new();
        private readonly Dictionary<AudioVoiceGroupId, TickBudget> _globalBudgetByGroup = new();
        private readonly Dictionary<OwnerGroupKey, TickBudget> _ownerBudgetByGroup = new();

        public void Reset()
        {
            _nextGlobalTickByGroup.Clear();
            _nextOwnerTickByGroup.Clear();
            _globalBudgetByGroup.Clear();
            _ownerBudgetByGroup.Clear();
        }

        public bool ShouldAccept(
            in AudioVoicePolicy policy,
            int? ownerEntityId,
            int tickIndex,
            int simulationTicksPerSecond,
            int additionalCooldownJitterTicks = 0)
        {
            if (!policy.HasGroup)
            {
                return true;
            }

            if (simulationTicksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(simulationTicksPerSecond),
                    "Simulation tick rate must be greater than zero.");
            }

            if (policy.MaxVoicesGlobal > 0 &&
                !TryConsumeGlobalBudget(policy.Group, tickIndex, policy.MaxVoicesGlobal))
            {
                return false;
            }

            var ownerKey = new OwnerGroupKey(policy.Group, ownerEntityId ?? 0);
            if (ownerEntityId.HasValue &&
                policy.MaxVoicesPerOwner > 0 &&
                !TryConsumeOwnerBudget(ownerKey, tickIndex, policy.MaxVoicesPerOwner))
            {
                RollbackGlobalBudget(policy.Group);
                return false;
            }

            if (_nextGlobalTickByGroup.TryGetValue(policy.Group, out var nextGlobalTick) &&
                tickIndex < nextGlobalTick)
            {
                RollbackGlobalBudget(policy.Group);
                RollbackOwnerBudget(ownerEntityId, ownerKey);
                return false;
            }

            if (ownerEntityId.HasValue &&
                _nextOwnerTickByGroup.TryGetValue(ownerKey, out var nextOwnerTick) &&
                tickIndex < nextOwnerTick)
            {
                RollbackGlobalBudget(policy.Group);
                RollbackOwnerBudget(ownerEntityId, ownerKey);
                return false;
            }

            var globalCooldownTicks = SecondsToCeilTicks(policy.CooldownSecondsGlobal, simulationTicksPerSecond);
            var ownerCooldownTicks = SecondsToCeilTicks(policy.CooldownSecondsPerOwner, simulationTicksPerSecond);
            var jitter = Math.Max(0, additionalCooldownJitterTicks);
            if (globalCooldownTicks > 0)
            {
                _nextGlobalTickByGroup[policy.Group] = tickIndex + globalCooldownTicks + jitter;
            }

            if (ownerEntityId.HasValue && ownerCooldownTicks > 0)
            {
                _nextOwnerTickByGroup[ownerKey] = tickIndex + ownerCooldownTicks + jitter;
            }

            return true;
        }

        private bool TryConsumeGlobalBudget(AudioVoiceGroupId group, int tickIndex, int maxVoices)
        {
            var budget = GetBudget(_globalBudgetByGroup, group, tickIndex);
            if (budget.Count >= maxVoices)
            {
                _globalBudgetByGroup[group] = budget;
                return false;
            }

            budget.Count++;
            _globalBudgetByGroup[group] = budget;
            return true;
        }

        private bool TryConsumeOwnerBudget(OwnerGroupKey key, int tickIndex, int maxVoices)
        {
            var budget = GetBudget(_ownerBudgetByGroup, key, tickIndex);
            if (budget.Count >= maxVoices)
            {
                _ownerBudgetByGroup[key] = budget;
                return false;
            }

            budget.Count++;
            _ownerBudgetByGroup[key] = budget;
            return true;
        }

        private void RollbackGlobalBudget(AudioVoiceGroupId group)
        {
            if (_globalBudgetByGroup.TryGetValue(group, out var budget) && budget.Count > 0)
            {
                budget.Count--;
                _globalBudgetByGroup[group] = budget;
            }
        }

        private void RollbackOwnerBudget(int? ownerEntityId, OwnerGroupKey key)
        {
            if (!ownerEntityId.HasValue)
            {
                return;
            }

            if (_ownerBudgetByGroup.TryGetValue(key, out var budget) && budget.Count > 0)
            {
                budget.Count--;
                _ownerBudgetByGroup[key] = budget;
            }
        }

        private static TickBudget GetBudget<TKey>(IDictionary<TKey, TickBudget> budgets, TKey key, int tickIndex)
        {
            if (!budgets.TryGetValue(key, out var budget) || budget.TickIndex != tickIndex)
            {
                return new TickBudget(tickIndex, 0);
            }

            return budget;
        }

        private static int SecondsToCeilTicks(float seconds, int simulationTicksPerSecond)
        {
            if (seconds <= 0f)
            {
                return 0;
            }

            const float floatingPointTolerance = 0.0001f;
            return Math.Max(1, (int)Math.Ceiling((seconds * simulationTicksPerSecond) - floatingPointTolerance));
        }

        private readonly struct OwnerGroupKey : IEquatable<OwnerGroupKey>
        {
            public OwnerGroupKey(AudioVoiceGroupId group, int ownerEntityId)
            {
                Group = group;
                OwnerEntityId = ownerEntityId;
            }

            private AudioVoiceGroupId Group { get; }

            private int OwnerEntityId { get; }

            public bool Equals(OwnerGroupKey other)
            {
                return Group == other.Group && OwnerEntityId == other.OwnerEntityId;
            }

            public override bool Equals(object obj)
            {
                return obj is OwnerGroupKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)Group * 397) ^ OwnerEntityId;
                }
            }
        }

        private struct TickBudget
        {
            public TickBudget(int tickIndex, int count)
            {
                TickIndex = tickIndex;
                Count = count;
            }

            public int TickIndex { get; }

            public int Count;
        }
    }
}
