using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.AudioPolicy;
using Game.Shared.Audio;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal readonly struct GameplaySfxRequest
    {
        public GameplaySfxRequest(
            AudioDefinition definition,
            AudioVoicePolicy policy,
            int? ownerEntityId,
            Component attachOwner,
            AudioAttachmentSlot attachmentSlot,
            in AudioPlaybackContext context,
            int tickIndex,
            int sequence)
        {
            Definition = definition;
            Policy = policy;
            OwnerEntityId = ownerEntityId;
            AttachOwner = attachOwner;
            AttachmentSlot = attachmentSlot;
            Context = context;
            TickIndex = tickIndex;
            Sequence = sequence;
        }

        public AudioDefinition Definition { get; }

        public AudioVoicePolicy Policy { get; }

        public int? OwnerEntityId { get; }

        public Component AttachOwner { get; }

        public AudioAttachmentSlot AttachmentSlot { get; }

        public AudioPlaybackContext Context { get; }

        public int TickIndex { get; }

        public int Sequence { get; }

        public bool IsAttached => AttachOwner != null && !AttachmentSlot.IsEmpty;

        public GameplaySfxRequest WithPolicy(AudioVoicePolicy policy)
        {
            return new GameplaySfxRequest(
                Definition,
                policy,
                OwnerEntityId,
                AttachOwner,
                AttachmentSlot,
                Context,
                TickIndex,
                Sequence);
        }
    }

    internal sealed class GameplaySfxArbiter
    {
        private readonly Dictionary<AudioVoiceGroupId, int> _nextGlobalTickByGroup = new();
        private readonly Dictionary<OwnerGroupKey, int> _nextOwnerTickByGroup = new();
        private readonly IAudioPolicyDiagnostics _diagnostics;
        private int _lastSequence;

        public GameplaySfxArbiter(IAudioPolicyDiagnostics diagnostics = null)
        {
            _diagnostics = diagnostics;
        }

        public int NextSequence()
        {
            unchecked
            {
                _lastSequence++;
                return _lastSequence == 0 ? ++_lastSequence : _lastSequence;
            }
        }

        public IReadOnlyList<GameplaySfxRequest> Filter(
            IReadOnlyList<GameplaySfxRequest> requests,
            int tickIndex,
            int simulationTicksPerSecond)
        {
            if (requests == null)
            {
                throw new ArgumentNullException(nameof(requests));
            }

            if (requests.Count == 0)
            {
                return Array.Empty<GameplaySfxRequest>();
            }

            if (simulationTicksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(simulationTicksPerSecond),
                    "Simulation tick rate must be greater than zero.");
            }

            var normalizedRequests = NormalizePolicies(requests);
            var deduped = RemoveExactDuplicates(normalizedRequests);
            deduped.Sort(CompareForAdmission);

            var accepted = new List<GameplaySfxRequest>();
            var groupCounts = new Dictionary<AudioVoiceGroupId, int>();
            var ownerCounts = new Dictionary<OwnerGroupKey, int>();
            for (var i = 0; i < deduped.Count; i++)
            {
                var request = deduped[i];
                if (ShouldAccept(
                        request,
                        tickIndex,
                        simulationTicksPerSecond,
                        groupCounts,
                        ownerCounts))
                {
                    accepted.Add(request);
                    CommitCooldowns(request.Policy, request.OwnerEntityId, tickIndex, simulationTicksPerSecond);
                }
            }

            accepted.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));
            return accepted.Count == 0 ? Array.Empty<GameplaySfxRequest>() : accepted;
        }

        private List<GameplaySfxRequest> NormalizePolicies(IReadOnlyList<GameplaySfxRequest> requests)
        {
            var output = new List<GameplaySfxRequest>(requests.Count);
            for (var i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                var policy = request.Policy;
                if (!policy.HasGroup)
                {
                    output.Add(request);
                    continue;
                }

                var normalizedOverflow = AudioVoicePolicySupport.NormalizeForGameplaySfxArbiterV1(
                    policy.OverflowMode,
                    _diagnostics,
                    ResolvePolicyContext(request));
                output.Add(normalizedOverflow == policy.OverflowMode
                    ? request
                    : request.WithPolicy(policy.WithOverflowMode(normalizedOverflow)));
            }

            return output;
        }

        private static List<GameplaySfxRequest> RemoveExactDuplicates(IReadOnlyList<GameplaySfxRequest> requests)
        {
            var output = new List<GameplaySfxRequest>();
            var seen = new HashSet<DuplicateKey>();
            for (var i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                var key = new DuplicateKey(request);
                if (!seen.Add(key))
                {
                    continue;
                }

                output.Add(request);
            }

            return output;
        }

        private static int CompareForAdmission(GameplaySfxRequest left, GameplaySfxRequest right)
        {
            var priority = right.Policy.Priority.CompareTo(left.Policy.Priority);
            return priority != 0 ? priority : left.Sequence.CompareTo(right.Sequence);
        }

        private bool ShouldAccept(
            in GameplaySfxRequest request,
            int tickIndex,
            int simulationTicksPerSecond,
            IDictionary<AudioVoiceGroupId, int> groupCounts,
            IDictionary<OwnerGroupKey, int> ownerCounts)
        {
            var policy = request.Policy;
            if (!policy.HasGroup)
            {
                return true;
            }

            if (_nextGlobalTickByGroup.TryGetValue(policy.Group, out var nextGlobalTick) &&
                tickIndex < nextGlobalTick)
            {
                return false;
            }

            var ownerKey = new OwnerGroupKey(policy.Group, request.OwnerEntityId ?? 0);
            if (request.OwnerEntityId.HasValue &&
                _nextOwnerTickByGroup.TryGetValue(ownerKey, out var nextOwnerTick) &&
                tickIndex < nextOwnerTick)
            {
                return false;
            }

            var currentGroupCount = groupCounts.TryGetValue(policy.Group, out var groupCount)
                ? groupCount
                : 0;
            if (policy.MaxVoicesGlobal > 0 && currentGroupCount >= policy.MaxVoicesGlobal)
            {
                return false;
            }

            if (request.OwnerEntityId.HasValue && policy.MaxVoicesPerOwner > 0)
            {
                var currentOwnerCount = ownerCounts.TryGetValue(ownerKey, out var ownerCount)
                    ? ownerCount
                    : 0;
                if (currentOwnerCount >= policy.MaxVoicesPerOwner)
                {
                    return false;
                }

                ownerCounts[ownerKey] = currentOwnerCount + 1;
            }

            groupCounts[policy.Group] = currentGroupCount + 1;
            return true;
        }

        private void CommitCooldowns(
            in AudioVoicePolicy policy,
            int? ownerEntityId,
            int tickIndex,
            int simulationTicksPerSecond)
        {
            if (!policy.HasGroup)
            {
                return;
            }

            var globalCooldownTicks = SecondsToCeilTicks(policy.CooldownSecondsGlobal, simulationTicksPerSecond);
            if (globalCooldownTicks > 0)
            {
                _nextGlobalTickByGroup[policy.Group] = tickIndex + globalCooldownTicks;
            }

            var ownerCooldownTicks = SecondsToCeilTicks(policy.CooldownSecondsPerOwner, simulationTicksPerSecond);
            if (ownerEntityId.HasValue && ownerCooldownTicks > 0)
            {
                _nextOwnerTickByGroup[new OwnerGroupKey(policy.Group, ownerEntityId.Value)] =
                    tickIndex + ownerCooldownTicks;
            }
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

        private static string ResolvePolicyContext(in GameplaySfxRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.Context.DebugTag))
            {
                return request.Context.DebugTag;
            }

            return request.Policy.Group.ToString();
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

        private readonly struct DuplicateKey : IEquatable<DuplicateKey>
        {
            public DuplicateKey(in GameplaySfxRequest request)
            {
                Group = request.Policy.Group;
                TickIndex = request.TickIndex;
                HasOwnerEntityId = request.OwnerEntityId.HasValue;
                OwnerEntityId = request.OwnerEntityId ?? 0;
                Definition = request.Definition;
                AttachOwner = request.AttachOwner;
                AttachmentSlot = request.AttachmentSlot;
                DebugTag = request.Context.DebugTag ?? string.Empty;
                VolumeMultiplier = request.Context.VolumeMultiplier;
                PitchMultiplier = request.Context.PitchMultiplier;
            }

            private AudioVoiceGroupId Group { get; }

            private int TickIndex { get; }

            private bool HasOwnerEntityId { get; }

            private int OwnerEntityId { get; }

            private AudioDefinition Definition { get; }

            private Component AttachOwner { get; }

            private AudioAttachmentSlot AttachmentSlot { get; }

            private string DebugTag { get; }

            private float VolumeMultiplier { get; }

            private float PitchMultiplier { get; }

            public bool Equals(DuplicateKey other)
            {
                return Group == other.Group &&
                       TickIndex == other.TickIndex &&
                       HasOwnerEntityId == other.HasOwnerEntityId &&
                       OwnerEntityId == other.OwnerEntityId &&
                       ReferenceEquals(Definition, other.Definition) &&
                       ReferenceEquals(AttachOwner, other.AttachOwner) &&
                       AttachmentSlot.Equals(other.AttachmentSlot) &&
                       string.Equals(DebugTag, other.DebugTag, StringComparison.Ordinal) &&
                       VolumeMultiplier.Equals(other.VolumeMultiplier) &&
                       PitchMultiplier.Equals(other.PitchMultiplier);
            }

            public override bool Equals(object obj)
            {
                return obj is DuplicateKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = (int)Group;
                    hash = (hash * 397) ^ TickIndex;
                    hash = (hash * 397) ^ HasOwnerEntityId.GetHashCode();
                    hash = (hash * 397) ^ OwnerEntityId;
                    hash = (hash * 397) ^ (Definition != null ? Definition.GetInstanceID() : 0);
                    hash = (hash * 397) ^ (AttachOwner != null ? AttachOwner.GetInstanceID() : 0);
                    hash = (hash * 397) ^ AttachmentSlot.GetHashCode();
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(DebugTag);
                    hash = (hash * 397) ^ VolumeMultiplier.GetHashCode();
                    hash = (hash * 397) ^ PitchMultiplier.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
