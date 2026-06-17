using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.PresentationContracts;

namespace Game.Feature.Gameplay.PresentationPlanning
{
    public enum PresentationPlaybackPolicyHintKind
    {
        None = 0,
        OneShot = 1,
        Track = 2,
        Barrier = 3,
    }

    public enum PresentationTopologyCueKey
    {
        None = 0,
        Transition = 1,
    }

    public enum PresentationVfxCueKey
    {
        None = 0,
        DamageHit = 1,
        EnemyDeath = 2,
    }

    public enum PresentationMotionCueKey
    {
        None = 0,
        BoxSlide = 1,
        BoxFlip = 2,
        BoxFlipImpact = 3,
    }

    public enum PresentationAnimationCueKey
    {
        None = 0,
        PlayerPushWindup = 1,
        PlayerPushExecute = 2,
        PlayerPushRecovery = 3,
        PlayerPushBlocked = 4,
        PlayerPushImpactContact = 5,
        PlayerPushFailed = 6,
        PlayerFlipWindup = 7,
        PlayerFlipExecute = 8,
        PlayerFlipRecovery = 9,
        PlayerFlipBlocked = 10,
        PlayerFlipImpactContact = 11,
        PlayerFlipFailed = 12,
        EnemyJumpWindup = 13,
        EnemyJumpAirborne = 14,
        EnemyJumpLand = 15,
        EnemyChargeWindup = 16,
        EnemyChargeActive = 17,
        EnemyChargeRecover = 18,
        EnemyDeath = 19,
    }

    public enum PresentationSfxCueKey
    {
        None = 0,
        PlayerDamage = 1,
        EnemyDamage = 2,
        EntityExitItemConsume = 3,
        EntityExitBoxDestroy = 4,
        EntityExitEnemyDeath = 5,
        EntityExitOutOfBounds = 6,
    }

    public enum PresentationActionAudioCueKey
    {
        None = 0,
        PlayerPushWindup = 1,
        PlayerPushAssistOutOfRange = 2,
        PlayerPushNoTarget = 3,
        PlayerPushInvalid = 4,
        PlayerFlipWindup = 5,
        PlayerFlipAssistOutOfRange = 6,
        PlayerFlipNoTarget = 7,
        PlayerFlipInvalid = 8,
    }

    public enum PresentationEnemyAudioCueKey
    {
        None = 0,
        Move = 1,
        Death = 2,
        Windup = 3,
        Landing = 4,
        Active = 5,
        Recover = 6,
        ForwardCellImpact = 7,
        ChargeActiveLoop = 8,
        StationaryActive = 9,
        PassiveContact = 10,
    }

    public readonly struct PresentationCueKey : IEquatable<PresentationCueKey>
    {
        public PresentationCueKey(PresentationDomain domain, int localKey, int variantKey = 0)
        {
            Domain = domain;
            LocalKey = Math.Max(0, localKey);
            VariantKey = Math.Max(0, variantKey);
        }

        public PresentationDomain Domain { get; }

        public int LocalKey { get; }

        public int VariantKey { get; }

        public bool IsValid => Domain != PresentationDomain.None && LocalKey > 0;

        public static PresentationCueKey ForVfx(PresentationVfxCueKey key)
        {
            return new PresentationCueKey(PresentationDomain.Vfx, (int)key);
        }

        public static PresentationCueKey ForMotion(PresentationMotionCueKey key)
        {
            return new PresentationCueKey(PresentationDomain.Motion, (int)key);
        }

        public static PresentationCueKey ForAnimation(PresentationAnimationCueKey key)
        {
            return new PresentationCueKey(PresentationDomain.Animation, (int)key);
        }

        public static PresentationCueKey ForSfx(PresentationSfxCueKey key)
        {
            return new PresentationCueKey(PresentationDomain.Sfx, (int)key);
        }

        public static PresentationCueKey ForActionAudio(PresentationActionAudioCueKey key)
        {
            return new PresentationCueKey(PresentationDomain.ActionAudio, (int)key);
        }

        public static PresentationCueKey ForEnemyAudio(PresentationEnemyAudioCueKey key)
        {
            return new PresentationCueKey(PresentationDomain.EnemyAudio, (int)key);
        }

        public bool TryGetVfxCueKey(out PresentationVfxCueKey key)
        {
            if (Domain == PresentationDomain.Vfx &&
                Enum.IsDefined(typeof(PresentationVfxCueKey), LocalKey) &&
                LocalKey != (int)PresentationVfxCueKey.None)
            {
                key = (PresentationVfxCueKey)LocalKey;
                return true;
            }

            key = PresentationVfxCueKey.None;
            return false;
        }

        public bool TryGetMotionCueKey(out PresentationMotionCueKey key)
        {
            if (Domain == PresentationDomain.Motion &&
                Enum.IsDefined(typeof(PresentationMotionCueKey), LocalKey) &&
                LocalKey != (int)PresentationMotionCueKey.None)
            {
                key = (PresentationMotionCueKey)LocalKey;
                return true;
            }

            key = PresentationMotionCueKey.None;
            return false;
        }

        public bool TryGetAnimationCueKey(out PresentationAnimationCueKey key)
        {
            if (Domain == PresentationDomain.Animation &&
                Enum.IsDefined(typeof(PresentationAnimationCueKey), LocalKey) &&
                LocalKey != (int)PresentationAnimationCueKey.None)
            {
                key = (PresentationAnimationCueKey)LocalKey;
                return true;
            }

            key = PresentationAnimationCueKey.None;
            return false;
        }

        public bool TryGetSfxCueKey(out PresentationSfxCueKey key)
        {
            if (Domain == PresentationDomain.Sfx &&
                Enum.IsDefined(typeof(PresentationSfxCueKey), LocalKey) &&
                LocalKey != (int)PresentationSfxCueKey.None)
            {
                key = (PresentationSfxCueKey)LocalKey;
                return true;
            }

            key = PresentationSfxCueKey.None;
            return false;
        }

        public bool TryGetActionAudioCueKey(out PresentationActionAudioCueKey key)
        {
            if (Domain == PresentationDomain.ActionAudio &&
                Enum.IsDefined(typeof(PresentationActionAudioCueKey), LocalKey) &&
                LocalKey != (int)PresentationActionAudioCueKey.None)
            {
                key = (PresentationActionAudioCueKey)LocalKey;
                return true;
            }

            key = PresentationActionAudioCueKey.None;
            return false;
        }

        public bool TryGetEnemyAudioCueKey(out PresentationEnemyAudioCueKey key)
        {
            if (Domain == PresentationDomain.EnemyAudio &&
                Enum.IsDefined(typeof(PresentationEnemyAudioCueKey), LocalKey) &&
                LocalKey != (int)PresentationEnemyAudioCueKey.None)
            {
                key = (PresentationEnemyAudioCueKey)LocalKey;
                return true;
            }

            key = PresentationEnemyAudioCueKey.None;
            return false;
        }

        public bool Equals(PresentationCueKey other)
        {
            return Domain == other.Domain &&
                   LocalKey == other.LocalKey &&
                   VariantKey == other.VariantKey;
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationCueKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Domain;
                hash = (hash * 397) ^ LocalKey;
                hash = (hash * 397) ^ VariantKey;
                return hash;
            }
        }
    }

    public readonly struct PresentationPlaybackPolicyHint : IEquatable<PresentationPlaybackPolicyHint>
    {
        public PresentationPlaybackPolicyHint(
            PresentationPlaybackPolicyHintKind kind,
            bool blocking = false,
            int dedupeKey = 0,
            int cancellationKey = 0,
            int cooldownKey = 0)
        {
            Kind = kind;
            Blocking = blocking;
            DedupeKey = Math.Max(0, dedupeKey);
            CancellationKey = Math.Max(0, cancellationKey);
            CooldownKey = Math.Max(0, cooldownKey);
        }

        public PresentationPlaybackPolicyHintKind Kind { get; }

        public bool Blocking { get; }

        public int DedupeKey { get; }

        public int CancellationKey { get; }

        public int CooldownKey { get; }

        public static PresentationPlaybackPolicyHint OneShot()
        {
            return new PresentationPlaybackPolicyHint(PresentationPlaybackPolicyHintKind.OneShot);
        }

        public static PresentationPlaybackPolicyHint OneShot(int dedupeKey)
        {
            return new PresentationPlaybackPolicyHint(
                PresentationPlaybackPolicyHintKind.OneShot,
                blocking: false,
                dedupeKey: dedupeKey);
        }

        public static PresentationPlaybackPolicyHint Track(bool blocking = false)
        {
            return new PresentationPlaybackPolicyHint(PresentationPlaybackPolicyHintKind.Track, blocking);
        }

        public bool Equals(PresentationPlaybackPolicyHint other)
        {
            return Kind == other.Kind &&
                   Blocking == other.Blocking &&
                   DedupeKey == other.DedupeKey &&
                   CancellationKey == other.CancellationKey &&
                   CooldownKey == other.CooldownKey;
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationPlaybackPolicyHint other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                hash = (hash * 397) ^ Blocking.GetHashCode();
                hash = (hash * 397) ^ DedupeKey;
                hash = (hash * 397) ^ CancellationKey;
                hash = (hash * 397) ^ CooldownKey;
                return hash;
            }
        }
    }

    public readonly struct PresentationSfxPayload : IEquatable<PresentationSfxPayload>
    {
        public PresentationSfxPayload(
            int entityType,
            int exitCause = 0,
            int sourceActorEntityId = 0)
        {
            EntityType = Math.Max(0, entityType);
            ExitCause = Math.Max(0, exitCause);
            SourceActorEntityId = Math.Max(0, sourceActorEntityId);
        }

        public int EntityType { get; }

        public int ExitCause { get; }

        public int SourceActorEntityId { get; }

        public bool Equals(PresentationSfxPayload other)
        {
            return EntityType == other.EntityType &&
                   ExitCause == other.ExitCause &&
                   SourceActorEntityId == other.SourceActorEntityId;
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationSfxPayload other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = EntityType;
                hash = (hash * 397) ^ ExitCause;
                hash = (hash * 397) ^ SourceActorEntityId;
                return hash;
            }
        }
    }

    public readonly struct PresentationCue : IEquatable<PresentationCue>
    {
        public PresentationCue(
            PresentationDomain domain,
            PresentationCueKey key,
            PresentationSource source,
            PresentationTarget target,
            PresentationAnchor anchor,
            PresentationPlaybackPolicyHint policyHint = default,
            PresentationTopologyTransitionPayload topologyPayload = default,
            PresentationMotionPayload motionPayload = default,
            PresentationAnimationPayload animationPayload = default,
            PresentationEnemyPayload enemyPayload = default,
            PresentationSfxPayload sfxPayload = default,
            PresentationActionAudioPayload actionAudioPayload = default,
            PresentationEnemyAudioPayload enemyAudioPayload = default)
        {
            Domain = domain;
            Key = key;
            Source = source;
            Target = target;
            Anchor = anchor;
            PolicyHint = policyHint;
            TopologyPayload = topologyPayload;
            MotionPayload = motionPayload;
            AnimationPayload = animationPayload;
            EnemyPayload = enemyPayload;
            SfxPayload = sfxPayload;
            ActionAudioPayload = actionAudioPayload;
            EnemyAudioPayload = enemyAudioPayload;
        }

        public PresentationDomain Domain { get; }

        public PresentationCueKey Key { get; }

        public PresentationSource Source { get; }

        public PresentationTarget Target { get; }

        public PresentationAnchor Anchor { get; }

        public PresentationPlaybackPolicyHint PolicyHint { get; }

        public PresentationTopologyTransitionPayload TopologyPayload { get; }

        public PresentationMotionPayload MotionPayload { get; }

        public PresentationAnimationPayload AnimationPayload { get; }

        public PresentationEnemyPayload EnemyPayload { get; }

        public PresentationSfxPayload SfxPayload { get; }

        public PresentationActionAudioPayload ActionAudioPayload { get; }

        public PresentationEnemyAudioPayload EnemyAudioPayload { get; }

        public bool Equals(PresentationCue other)
        {
            return Domain == other.Domain &&
                   Key.Equals(other.Key) &&
                   Source.Equals(other.Source) &&
                   Target.Equals(other.Target) &&
                   Anchor.Equals(other.Anchor) &&
                   PolicyHint.Equals(other.PolicyHint) &&
                   TopologyPayload.Equals(other.TopologyPayload) &&
                   MotionPayload.Equals(other.MotionPayload) &&
                   AnimationPayload.Equals(other.AnimationPayload) &&
                   EnemyPayload.Equals(other.EnemyPayload) &&
                   SfxPayload.Equals(other.SfxPayload) &&
                   ActionAudioPayload.Equals(other.ActionAudioPayload) &&
                   EnemyAudioPayload.Equals(other.EnemyAudioPayload);
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationCue other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Domain;
                hash = (hash * 397) ^ Key.GetHashCode();
                hash = (hash * 397) ^ Source.GetHashCode();
                hash = (hash * 397) ^ Target.GetHashCode();
                hash = (hash * 397) ^ Anchor.GetHashCode();
                hash = (hash * 397) ^ PolicyHint.GetHashCode();
                hash = (hash * 397) ^ TopologyPayload.GetHashCode();
                hash = (hash * 397) ^ MotionPayload.GetHashCode();
                hash = (hash * 397) ^ AnimationPayload.GetHashCode();
                hash = (hash * 397) ^ EnemyPayload.GetHashCode();
                hash = (hash * 397) ^ SfxPayload.GetHashCode();
                hash = (hash * 397) ^ ActionAudioPayload.GetHashCode();
                hash = (hash * 397) ^ EnemyAudioPayload.GetHashCode();
                return hash;
            }
        }
    }

    public readonly struct PresentationCueFrameDiagnostics
    {
        public PresentationCueFrameDiagnostics(int sourceFactCount, int plannedCueCount, int plannerCount)
        {
            SourceFactCount = Math.Max(0, sourceFactCount);
            PlannedCueCount = Math.Max(0, plannedCueCount);
            PlannerCount = Math.Max(0, plannerCount);
        }

        public int SourceFactCount { get; }

        public int PlannedCueCount { get; }

        public int PlannerCount { get; }
    }

    public sealed class PresentationCueFrame
    {
        private static readonly IReadOnlyList<PresentationCue> EmptyCues =
            new ReadOnlyCollection<PresentationCue>(new List<PresentationCue>());

        private readonly IReadOnlyList<PresentationCue> _cues;

        public PresentationCueFrame(
            int tickIndex,
            IReadOnlyList<PresentationCue> cues,
            PresentationCueFrameDiagnostics diagnostics)
        {
            TickIndex = tickIndex;
            _cues = new ReadOnlyCollection<PresentationCue>(
                new List<PresentationCue>(cues ?? EmptyCues));
            Diagnostics = diagnostics;
        }

        public int TickIndex { get; }

        public IReadOnlyList<PresentationCue> Cues => _cues;

        public PresentationCueFrameDiagnostics Diagnostics { get; }

        public static PresentationCueFrame Empty(int tickIndex, int sourceFactCount = 0)
        {
            return new PresentationCueFrame(
                tickIndex,
                EmptyCues,
                new PresentationCueFrameDiagnostics(sourceFactCount, 0, 0));
        }
    }

    public sealed class PresentationCueFrameBuilder
    {
        private readonly List<PresentationCue> _cues = new();
        private readonly int _sourceFactCount;
        private int _plannerCount;

        public PresentationCueFrameBuilder(int tickIndex, int sourceFactCount)
        {
            TickIndex = tickIndex;
            _sourceFactCount = Math.Max(0, sourceFactCount);
        }

        public int TickIndex { get; }

        public int Count => _cues.Count;

        public void Add(PresentationCue cue)
        {
            if (cue.Domain == PresentationDomain.None || !cue.Key.IsValid)
            {
                return;
            }

            _cues.Add(cue);
        }

        public void MarkPlannerRan()
        {
            _plannerCount++;
        }

        public PresentationCueFrame Build()
        {
            return new PresentationCueFrame(
                TickIndex,
                _cues,
                new PresentationCueFrameDiagnostics(_sourceFactCount, _cues.Count, _plannerCount));
        }
    }

    public interface IPresentationCuePlanner
    {
        void Plan(in PresentationFactFrame facts, PresentationCueFrameBuilder builder);
    }

    public sealed class TopologyCuePlanner : IPresentationCuePlanner
    {
        private static readonly PresentationCueKey TransitionCueKey =
            new(PresentationDomain.Topology, (int)PresentationTopologyCueKey.Transition);

        public void Plan(in PresentationFactFrame facts, PresentationCueFrameBuilder builder)
        {
            if (facts == null)
            {
                throw new ArgumentNullException(nameof(facts));
            }

            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            for (var i = 0; i < facts.Facts.Count; i++)
            {
                var fact = facts.Facts[i];
                if (fact.Kind != PresentationFactKind.Topology)
                {
                    continue;
                }

                builder.Add(new PresentationCue(
                    PresentationDomain.Topology,
                    TransitionCueKey,
                    fact.Source,
                    PresentationTarget.Topology(),
                    PresentationAnchor.ForTopologyOrbit(),
                    PresentationPlaybackPolicyHint.Track(blocking: true),
                    fact.TopologyPayload));
            }
        }
    }

    public sealed class VfxCuePlanner : IPresentationCuePlanner
    {
        public void Plan(in PresentationFactFrame facts, PresentationCueFrameBuilder builder)
        {
            if (facts == null)
            {
                throw new ArgumentNullException(nameof(facts));
            }

            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var enemyDeathEntityIds = BuildEnemyDeathEntityIds(facts);
            for (var i = 0; i < facts.Facts.Count; i++)
            {
                var fact = facts.Facts[i];
                if (TryPlanDamageHit(fact, enemyDeathEntityIds, out var damageCue) ||
                    TryPlanEnemyDeath(fact, out damageCue))
                {
                    builder.Add(damageCue);
                }
            }
        }

        private static bool TryPlanDamageHit(
            PresentationFact fact,
            ISet<int> enemyDeathEntityIds,
            out PresentationCue cue)
        {
            if (fact.Kind != PresentationFactKind.Combat ||
                fact.Source.SemanticSource != PresentationSemanticSource.EnemyDamage ||
                fact.Target.Kind != PresentationTargetKind.Entity ||
                fact.Target.EntityId <= 0 ||
                enemyDeathEntityIds.Contains(fact.Target.EntityId))
            {
                cue = default;
                return false;
            }

            var key = PresentationCueKey.ForVfx(PresentationVfxCueKey.DamageHit);
            cue = new PresentationCue(
                PresentationDomain.Vfx,
                key,
                fact.Source,
                fact.Target,
                PresentationAnchor.ForEntityCenter(fact.Target.EntityId),
                PresentationPlaybackPolicyHint.OneShot(ComputeDedupeKey(fact, key)));
            return true;
        }

        private static bool TryPlanEnemyDeath(PresentationFact fact, out PresentationCue cue)
        {
            if (fact.Kind != PresentationFactKind.EntityLifecycle ||
                fact.Source.SemanticSource != PresentationSemanticSource.EntityExit ||
                fact.Target.Kind != PresentationTargetKind.Entity ||
                fact.Target.EntityId <= 0 ||
                fact.Payload.PrimaryValue != 3)
            {
                cue = default;
                return false;
            }

            var key = PresentationCueKey.ForVfx(PresentationVfxCueKey.EnemyDeath);
            cue = new PresentationCue(
                PresentationDomain.Vfx,
                key,
                fact.Source,
                fact.Target,
                fact.Payload.PrimaryCellCenterAnchorOrEntityCenter(fact.Target.EntityId),
                PresentationPlaybackPolicyHint.OneShot(ComputeDedupeKey(fact, key)));
            return true;
        }

        private static int ComputeDedupeKey(PresentationFact fact, PresentationCueKey key)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 397) ^ fact.Source.TickIndex;
                hash = (hash * 397) ^ (int)fact.Source.SemanticSource;
                hash = (hash * 397) ^ fact.Source.SourceEntityId;
                hash = (hash * 397) ^ fact.Target.EntityId;
                hash = (hash * 397) ^ key.GetHashCode();
                return hash & int.MaxValue;
            }
        }

        private static ISet<int> BuildEnemyDeathEntityIds(PresentationFactFrame facts)
        {
            var entityIds = new HashSet<int>();
            for (var i = 0; i < facts.Facts.Count; i++)
            {
                var fact = facts.Facts[i];
                if (fact.Kind == PresentationFactKind.EntityLifecycle &&
                    fact.Source.SemanticSource == PresentationSemanticSource.EntityExit &&
                    fact.Target.Kind == PresentationTargetKind.Entity &&
                    fact.Target.EntityId > 0 &&
                    fact.Payload.PrimaryValue == 3)
                {
                    entityIds.Add(fact.Target.EntityId);
                }
            }

            return entityIds;
        }
    }

    public sealed class MotionCuePlanner : IPresentationCuePlanner
    {
        public void Plan(in PresentationFactFrame facts, PresentationCueFrameBuilder builder)
        {
            if (facts == null)
            {
                throw new ArgumentNullException(nameof(facts));
            }

            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            for (var i = 0; i < facts.Facts.Count; i++)
            {
                var fact = facts.Facts[i];
                if (TryPlanMotion(fact, out var cue))
                {
                    builder.Add(cue);
                }
            }
        }

        private static bool TryPlanMotion(PresentationFact fact, out PresentationCue cue)
        {
            cue = default;
            if (fact.Kind != PresentationFactKind.Movement ||
                !fact.MotionPayload.IsValid ||
                fact.Target.Kind != PresentationTargetKind.Entity ||
                fact.Target.EntityId <= 0)
            {
                return false;
            }

            if (!TryResolveCueKey(fact.MotionPayload.Kind, out var motionCueKey))
            {
                return false;
            }

            var key = PresentationCueKey.ForMotion(motionCueKey);
            cue = new PresentationCue(
                PresentationDomain.Motion,
                key,
                fact.Source,
                fact.Target,
                PresentationAnchor.ForEntityVisualRoot(fact.Target.EntityId),
                new PresentationPlaybackPolicyHint(
                    PresentationPlaybackPolicyHintKind.Track,
                    blocking: false,
                    dedupeKey: ComputeDedupeKey(fact, key)),
                motionPayload: fact.MotionPayload);
            return true;
        }

        private static bool TryResolveCueKey(
            PresentationMotionFactKind factKind,
            out PresentationMotionCueKey cueKey)
        {
            switch (factKind)
            {
                case PresentationMotionFactKind.BoxSlide:
                    cueKey = PresentationMotionCueKey.BoxSlide;
                    return true;
                case PresentationMotionFactKind.BoxFlip:
                    cueKey = PresentationMotionCueKey.BoxFlip;
                    return true;
                case PresentationMotionFactKind.BoxFlipImpact:
                    cueKey = PresentationMotionCueKey.BoxFlipImpact;
                    return true;
                default:
                    cueKey = PresentationMotionCueKey.None;
                    return false;
            }
        }

        private static int ComputeDedupeKey(PresentationFact fact, PresentationCueKey key)
        {
            unchecked
            {
                var payload = fact.MotionPayload;
                var hash = 23;
                hash = (hash * 397) ^ fact.Source.TickIndex;
                hash = (hash * 397) ^ (int)fact.Source.SemanticSource;
                hash = (hash * 397) ^ payload.EntityId;
                hash = (hash * 397) ^ payload.GetHashCode();
                hash = (hash * 397) ^ key.GetHashCode();
                return hash & int.MaxValue;
            }
        }
    }

    public sealed class AnimationCuePlanner : IPresentationCuePlanner
    {
        public void Plan(in PresentationFactFrame facts, PresentationCueFrameBuilder builder)
        {
            if (facts == null)
            {
                throw new ArgumentNullException(nameof(facts));
            }

            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            for (var i = 0; i < facts.Facts.Count; i++)
            {
                var fact = facts.Facts[i];
                if (TryPlanAnimation(fact, out var cue))
                {
                    builder.Add(cue);
                }
            }
        }

        private static bool TryPlanAnimation(PresentationFact fact, out PresentationCue cue)
        {
            cue = default;
            if (fact.Kind != PresentationFactKind.Action ||
                !fact.AnimationPayload.IsValid ||
                fact.Target.Kind != PresentationTargetKind.Entity ||
                fact.Target.EntityId <= 0)
            {
                return false;
            }

            if (!TryResolveCueKey(fact.AnimationPayload, out var animationCueKey))
            {
                return false;
            }

            var key = PresentationCueKey.ForAnimation(animationCueKey);
            cue = new PresentationCue(
                PresentationDomain.Animation,
                key,
                fact.Source,
                fact.Target,
                PresentationAnchor.ForEntityVisualRoot(fact.Target.EntityId),
                PresentationPlaybackPolicyHint.OneShot(ComputeDedupeKey(fact, key)),
                animationPayload: fact.AnimationPayload);
            return true;
        }

        private static bool TryResolveCueKey(
            PresentationAnimationPayload payload,
            out PresentationAnimationCueKey cueKey)
        {
            switch (payload.ActionKind)
            {
                case PresentationAnimationActionKind.Push:
                    cueKey = ResolvePushCueKey(payload.PhaseKind, payload.OutcomeKind);
                    return cueKey != PresentationAnimationCueKey.None;
                case PresentationAnimationActionKind.Flip:
                    cueKey = ResolveFlipCueKey(payload.PhaseKind, payload.OutcomeKind);
                    return cueKey != PresentationAnimationCueKey.None;
                default:
                    cueKey = PresentationAnimationCueKey.None;
                    return false;
            }
        }

        private static PresentationAnimationCueKey ResolvePushCueKey(
            PresentationAnimationPhaseKind phaseKind,
            PresentationAnimationOutcomeKind outcomeKind)
        {
            if (outcomeKind == PresentationAnimationOutcomeKind.Blocked)
            {
                return PresentationAnimationCueKey.PlayerPushBlocked;
            }

            if (outcomeKind == PresentationAnimationOutcomeKind.Impact)
            {
                return PresentationAnimationCueKey.PlayerPushImpactContact;
            }

            return phaseKind switch
            {
                PresentationAnimationPhaseKind.Windup => PresentationAnimationCueKey.PlayerPushWindup,
                PresentationAnimationPhaseKind.Execute => PresentationAnimationCueKey.PlayerPushExecute,
                PresentationAnimationPhaseKind.Recovery => PresentationAnimationCueKey.PlayerPushRecovery,
                PresentationAnimationPhaseKind.Failed => PresentationAnimationCueKey.PlayerPushFailed,
                _ => PresentationAnimationCueKey.None,
            };
        }

        private static PresentationAnimationCueKey ResolveFlipCueKey(
            PresentationAnimationPhaseKind phaseKind,
            PresentationAnimationOutcomeKind outcomeKind)
        {
            if (outcomeKind == PresentationAnimationOutcomeKind.Blocked)
            {
                return PresentationAnimationCueKey.PlayerFlipBlocked;
            }

            if (outcomeKind == PresentationAnimationOutcomeKind.Impact)
            {
                return PresentationAnimationCueKey.PlayerFlipImpactContact;
            }

            return phaseKind switch
            {
                PresentationAnimationPhaseKind.Windup => PresentationAnimationCueKey.PlayerFlipWindup,
                PresentationAnimationPhaseKind.Execute => PresentationAnimationCueKey.PlayerFlipExecute,
                PresentationAnimationPhaseKind.Recovery => PresentationAnimationCueKey.PlayerFlipRecovery,
                PresentationAnimationPhaseKind.Failed => PresentationAnimationCueKey.PlayerFlipFailed,
                _ => PresentationAnimationCueKey.None,
            };
        }

        private static int ComputeDedupeKey(PresentationFact fact, PresentationCueKey key)
        {
            unchecked
            {
                var payload = fact.AnimationPayload;
                var hash = 29;
                hash = (hash * 397) ^ fact.Source.TickIndex;
                hash = (hash * 397) ^ (int)fact.Source.SemanticSource;
                hash = (hash * 397) ^ payload.EntityId;
                hash = (hash * 397) ^ payload.SourceSequenceId;
                hash = (hash * 397) ^ payload.SourceActionPlanId;
                hash = (hash * 397) ^ (int)payload.ActionKind;
                hash = (hash * 397) ^ (int)payload.PhaseKind;
                hash = (hash * 397) ^ (int)payload.OutcomeKind;
                hash = (hash * 397) ^ key.GetHashCode();
                return hash & int.MaxValue;
            }
        }
    }

    public sealed class EnemyPresentationCuePlanner : IPresentationCuePlanner
    {
        public void Plan(in PresentationFactFrame facts, PresentationCueFrameBuilder builder)
        {
            if (facts == null)
            {
                throw new ArgumentNullException(nameof(facts));
            }

            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            for (var i = 0; i < facts.Facts.Count; i++)
            {
                var fact = facts.Facts[i];
                if (TryPlanEnemyPresentation(fact, out var cue))
                {
                    builder.Add(cue);
                }
            }
        }

        private static bool TryPlanEnemyPresentation(PresentationFact fact, out PresentationCue cue)
        {
            cue = default;
            if (fact.Kind != PresentationFactKind.EnemyPresentation ||
                !fact.EnemyPayload.IsValid ||
                fact.Target.Kind != PresentationTargetKind.Entity ||
                fact.Target.EntityId <= 0)
            {
                return false;
            }

            if (!TryResolveCueKey(fact.EnemyPayload, out var animationCueKey))
            {
                return false;
            }

            var key = PresentationCueKey.ForAnimation(animationCueKey);
            cue = new PresentationCue(
                PresentationDomain.Animation,
                key,
                fact.Source,
                fact.Target,
                PresentationAnchor.ForEntityVisualRoot(fact.Target.EntityId),
                PresentationPlaybackPolicyHint.OneShot(ComputeDedupeKey(fact, key)),
                animationPayload: fact.AnimationPayload,
                enemyPayload: fact.EnemyPayload);
            return true;
        }

        private static bool TryResolveCueKey(
            PresentationEnemyPayload payload,
            out PresentationAnimationCueKey cueKey)
        {
            switch (payload.Kind)
            {
                case PresentationEnemyPresentationKind.Jump:
                    cueKey = ResolveJumpCueKey(payload.Phase);
                    return cueKey != PresentationAnimationCueKey.None;
                case PresentationEnemyPresentationKind.Charge:
                    cueKey = ResolveChargeCueKey(payload.Phase);
                    return cueKey != PresentationAnimationCueKey.None;
                case PresentationEnemyPresentationKind.Death:
                    cueKey = payload.Phase == PresentationEnemyPresentationPhase.Death
                        ? PresentationAnimationCueKey.EnemyDeath
                        : PresentationAnimationCueKey.None;
                    return cueKey != PresentationAnimationCueKey.None;
                default:
                    cueKey = PresentationAnimationCueKey.None;
                    return false;
            }
        }

        private static PresentationAnimationCueKey ResolveJumpCueKey(
            PresentationEnemyPresentationPhase phase)
        {
            return phase switch
            {
                PresentationEnemyPresentationPhase.Windup => PresentationAnimationCueKey.EnemyJumpWindup,
                PresentationEnemyPresentationPhase.Airborne => PresentationAnimationCueKey.EnemyJumpAirborne,
                PresentationEnemyPresentationPhase.Land => PresentationAnimationCueKey.EnemyJumpLand,
                _ => PresentationAnimationCueKey.None,
            };
        }

        private static PresentationAnimationCueKey ResolveChargeCueKey(
            PresentationEnemyPresentationPhase phase)
        {
            return phase switch
            {
                PresentationEnemyPresentationPhase.Windup => PresentationAnimationCueKey.EnemyChargeWindup,
                PresentationEnemyPresentationPhase.Active => PresentationAnimationCueKey.EnemyChargeActive,
                PresentationEnemyPresentationPhase.Recover => PresentationAnimationCueKey.EnemyChargeRecover,
                _ => PresentationAnimationCueKey.None,
            };
        }

        private static int ComputeDedupeKey(PresentationFact fact, PresentationCueKey key)
        {
            unchecked
            {
                var payload = fact.EnemyPayload;
                var hash = 31;
                hash = (hash * 397) ^ fact.Source.TickIndex;
                hash = (hash * 397) ^ (int)fact.Source.SemanticSource;
                hash = (hash * 397) ^ payload.EnemyEntityId;
                hash = (hash * 397) ^ payload.SourceSequenceId;
                hash = (hash * 397) ^ (int)payload.Kind;
                hash = (hash * 397) ^ (int)payload.Phase;
                hash = (hash * 397) ^ key.GetHashCode();
                return hash & int.MaxValue;
            }
        }
    }

    public sealed class SfxCuePlanner : IPresentationCuePlanner
    {
        private const int ExitCauseItemConsume = 1;
        private const int ExitCauseBoxDestroy = 2;
        private const int ExitCauseEnemyDeath = 3;
        private const int ExitCauseOutOfBounds = 4;

        public void Plan(in PresentationFactFrame facts, PresentationCueFrameBuilder builder)
        {
            if (facts == null)
            {
                throw new ArgumentNullException(nameof(facts));
            }

            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            for (var i = 0; i < facts.Facts.Count; i++)
            {
                var fact = facts.Facts[i];
                if (TryPlanDamageSfx(fact, out var damageCue) ||
                    TryPlanEntityExitSfx(fact, out damageCue))
                {
                    builder.Add(damageCue);
                }
            }
        }

        private static bool TryPlanDamageSfx(PresentationFact fact, out PresentationCue cue)
        {
            cue = default;
            if (fact.Kind != PresentationFactKind.Combat ||
                fact.Target.Kind != PresentationTargetKind.Entity ||
                fact.Target.EntityId <= 0)
            {
                return false;
            }

            PresentationSfxCueKey sfxCueKey;
            switch (fact.Source.SemanticSource)
            {
                case PresentationSemanticSource.PlayerDamage:
                    sfxCueKey = PresentationSfxCueKey.PlayerDamage;
                    break;
                case PresentationSemanticSource.EnemyDamage:
                    sfxCueKey = PresentationSfxCueKey.EnemyDamage;
                    break;
                default:
                    return false;
            }

            var key = PresentationCueKey.ForSfx(sfxCueKey);
            cue = new PresentationCue(
                PresentationDomain.Sfx,
                key,
                fact.Source,
                fact.Target,
                PresentationAnchor.ForEntityCenter(fact.Target.EntityId),
                PresentationPlaybackPolicyHint.OneShot(ComputeDedupeKey(fact, key)),
                sfxPayload: new PresentationSfxPayload(entityType: 0));
            return true;
        }

        private static bool TryPlanEntityExitSfx(PresentationFact fact, out PresentationCue cue)
        {
            cue = default;
            if (fact.Kind != PresentationFactKind.EntityLifecycle ||
                fact.Source.SemanticSource != PresentationSemanticSource.EntityExit ||
                fact.Target.Kind != PresentationTargetKind.Entity ||
                fact.Target.EntityId <= 0 ||
                !TryResolveExitCueKey(fact.Payload.PrimaryValue, out var sfxCueKey))
            {
                return false;
            }

            var key = PresentationCueKey.ForSfx(sfxCueKey);
            cue = new PresentationCue(
                PresentationDomain.Sfx,
                key,
                fact.Source,
                fact.Target,
                fact.Payload.PrimaryCellCenterAnchorOrEntityCenter(fact.Target.EntityId),
                PresentationPlaybackPolicyHint.OneShot(ComputeDedupeKey(fact, key)),
                sfxPayload: new PresentationSfxPayload(
                    fact.Payload.SecondaryValue,
                    fact.Payload.PrimaryValue,
                    fact.Payload.TertiaryValue));
            return true;
        }

        private static bool TryResolveExitCueKey(int exitCause, out PresentationSfxCueKey cueKey)
        {
            switch (exitCause)
            {
                case ExitCauseItemConsume:
                    cueKey = PresentationSfxCueKey.EntityExitItemConsume;
                    return true;
                case ExitCauseBoxDestroy:
                    cueKey = PresentationSfxCueKey.EntityExitBoxDestroy;
                    return true;
                case ExitCauseEnemyDeath:
                    cueKey = PresentationSfxCueKey.EntityExitEnemyDeath;
                    return true;
                case ExitCauseOutOfBounds:
                    cueKey = PresentationSfxCueKey.EntityExitOutOfBounds;
                    return true;
                default:
                    cueKey = PresentationSfxCueKey.None;
                    return false;
            }
        }

        private static int ComputeDedupeKey(PresentationFact fact, PresentationCueKey key)
        {
            unchecked
            {
                var hash = 37;
                hash = (hash * 397) ^ fact.Source.TickIndex;
                hash = (hash * 397) ^ (int)fact.Source.SemanticSource;
                hash = (hash * 397) ^ fact.Source.SourceEntityId;
                hash = (hash * 397) ^ fact.Source.SourceActionKind;
                hash = (hash * 397) ^ fact.Source.SourceSequence;
                hash = (hash * 397) ^ fact.Target.EntityId;
                hash = (hash * 397) ^ key.GetHashCode();
                return hash & int.MaxValue;
            }
        }
    }

    public sealed class ActionAudioCuePlanner : IPresentationCuePlanner
    {
        public void Plan(in PresentationFactFrame facts, PresentationCueFrameBuilder builder)
        {
            if (facts == null)
            {
                throw new ArgumentNullException(nameof(facts));
            }

            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            for (var i = 0; i < facts.Facts.Count; i++)
            {
                var fact = facts.Facts[i];
                if (TryPlanActionAudio(fact, out var cue))
                {
                    builder.Add(cue);
                }
            }
        }

        private static bool TryPlanActionAudio(PresentationFact fact, out PresentationCue cue)
        {
            cue = default;
            if (fact.Kind != PresentationFactKind.ActionAudio ||
                !fact.ActionAudioPayload.IsValid ||
                fact.Target.Kind != PresentationTargetKind.Entity ||
                fact.Target.EntityId <= 0)
            {
                return false;
            }

            if (!TryResolveCueKey(fact.ActionAudioPayload, out var actionAudioCueKey))
            {
                return false;
            }

            var key = PresentationCueKey.ForActionAudio(actionAudioCueKey);
            cue = new PresentationCue(
                PresentationDomain.ActionAudio,
                key,
                fact.Source,
                PresentationTarget.Entity(fact.ActionAudioPayload.OwnerEntityId),
                PresentationAnchor.ForEntityVisualRoot(fact.ActionAudioPayload.OwnerEntityId),
                PresentationPlaybackPolicyHint.OneShot(ComputeDedupeKey(fact, key)),
                actionAudioPayload: fact.ActionAudioPayload);
            return true;
        }

        private static bool TryResolveCueKey(
            PresentationActionAudioPayload payload,
            out PresentationActionAudioCueKey cueKey)
        {
            switch (payload.ActionKind)
            {
                case 0:
                    cueKey = ResolvePushCueKey(payload.Moment);
                    return cueKey != PresentationActionAudioCueKey.None;
                case 1:
                    cueKey = ResolveFlipCueKey(payload.Moment);
                    return cueKey != PresentationActionAudioCueKey.None;
                default:
                    cueKey = PresentationActionAudioCueKey.None;
                    return false;
            }
        }

        private static PresentationActionAudioCueKey ResolvePushCueKey(int moment)
        {
            return moment switch
            {
                0 => PresentationActionAudioCueKey.PlayerPushWindup,
                6 => PresentationActionAudioCueKey.PlayerPushAssistOutOfRange,
                7 => PresentationActionAudioCueKey.PlayerPushNoTarget,
                8 => PresentationActionAudioCueKey.PlayerPushInvalid,
                _ => PresentationActionAudioCueKey.None,
            };
        }

        private static PresentationActionAudioCueKey ResolveFlipCueKey(int moment)
        {
            return moment switch
            {
                0 => PresentationActionAudioCueKey.PlayerFlipWindup,
                6 => PresentationActionAudioCueKey.PlayerFlipAssistOutOfRange,
                7 => PresentationActionAudioCueKey.PlayerFlipNoTarget,
                8 => PresentationActionAudioCueKey.PlayerFlipInvalid,
                _ => PresentationActionAudioCueKey.None,
            };
        }

        private static int ComputeDedupeKey(PresentationFact fact, PresentationCueKey key)
        {
            unchecked
            {
                var payload = fact.ActionAudioPayload;
                var hash = 41;
                hash = (hash * 397) ^ fact.Source.TickIndex;
                hash = (hash * 397) ^ (int)fact.Source.SemanticSource;
                hash = (hash * 397) ^ payload.OwnerEntityId;
                hash = (hash * 397) ^ payload.ActionKind;
                hash = (hash * 397) ^ payload.Moment;
                hash = (hash * 397) ^ payload.SourceSequenceId;
                hash = (hash * 397) ^ payload.SourceActionPlanId;
                hash = (hash * 397) ^ payload.TargetEntityId;
                hash = (hash * 397) ^ (int)payload.OutcomeKind;
                hash = (hash * 397) ^ key.GetHashCode();
                return hash & int.MaxValue;
            }
        }
    }

    public sealed class EnemyAudioCuePlanner : IPresentationCuePlanner
    {
        public void Plan(in PresentationFactFrame facts, PresentationCueFrameBuilder builder)
        {
            if (facts == null)
            {
                throw new ArgumentNullException(nameof(facts));
            }

            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            for (var i = 0; i < facts.Facts.Count; i++)
            {
                var fact = facts.Facts[i];
                if (TryPlanEnemyAudio(fact, out var cue))
                {
                    builder.Add(cue);
                }
            }
        }

        private static bool TryPlanEnemyAudio(PresentationFact fact, out PresentationCue cue)
        {
            cue = default;
            if (fact.Kind != PresentationFactKind.EnemyAudio ||
                !fact.EnemyAudioPayload.IsValid ||
                fact.Target.Kind != PresentationTargetKind.Entity ||
                fact.Target.EntityId <= 0)
            {
                return false;
            }

            if (!TryResolveCueKey(fact.EnemyAudioPayload.CueKey, out var enemyAudioCueKey))
            {
                return false;
            }

            var key = PresentationCueKey.ForEnemyAudio(enemyAudioCueKey);
            cue = new PresentationCue(
                PresentationDomain.EnemyAudio,
                key,
                fact.Source,
                PresentationTarget.Entity(fact.EnemyAudioPayload.OwnerEntityId),
                PresentationAnchor.ForEntityVisualRoot(fact.EnemyAudioPayload.OwnerEntityId),
                PresentationPlaybackPolicyHint.OneShot(ComputeDedupeKey(fact, key)),
                enemyAudioPayload: fact.EnemyAudioPayload);
            return true;
        }

        private static bool TryResolveCueKey(int cueKey, out PresentationEnemyAudioCueKey resolved)
        {
            if (Enum.IsDefined(typeof(PresentationEnemyAudioCueKey), cueKey) &&
                cueKey != (int)PresentationEnemyAudioCueKey.None &&
                cueKey != (int)PresentationEnemyAudioCueKey.ChargeActiveLoop)
            {
                resolved = (PresentationEnemyAudioCueKey)cueKey;
                return true;
            }

            resolved = PresentationEnemyAudioCueKey.None;
            return false;
        }

        private static int ComputeDedupeKey(PresentationFact fact, PresentationCueKey key)
        {
            unchecked
            {
                var payload = fact.EnemyAudioPayload;
                var hash = 43;
                hash = (hash * 397) ^ fact.Source.TickIndex;
                hash = (hash * 397) ^ (int)fact.Source.SemanticSource;
                hash = (hash * 397) ^ payload.OwnerEntityId;
                hash = (hash * 397) ^ payload.CueKey;
                hash = (hash * 397) ^ payload.SourceSequenceId;
                hash = (hash * 397) ^ (int)payload.OriginKind;
                hash = (hash * 397) ^ (int)payload.Phase;
                hash = (hash * 397) ^ payload.TargetEntityId;
                hash = (hash * 397) ^ payload.ImpactTick;
                hash = (hash * 397) ^ payload.ImpactId;
                hash = (hash * 397) ^ payload.PresentationKey;
                hash = (hash * 397) ^ key.GetHashCode();
                return hash & int.MaxValue;
            }
        }
    }

    public sealed class PresentationCuePlannerSet
    {
        private static readonly IReadOnlyList<IPresentationCuePlanner> EmptyPlanners =
            new ReadOnlyCollection<IPresentationCuePlanner>(new List<IPresentationCuePlanner>());

        private readonly IReadOnlyList<IPresentationCuePlanner> _planners;

        public PresentationCuePlannerSet(IReadOnlyList<IPresentationCuePlanner> planners = null)
        {
            _planners = new ReadOnlyCollection<IPresentationCuePlanner>(
                new List<IPresentationCuePlanner>(planners ?? EmptyPlanners));
        }

        public IReadOnlyList<IPresentationCuePlanner> Planners => _planners;

        public PresentationCueFrame Plan(PresentationFactFrame facts)
        {
            if (facts == null)
            {
                throw new ArgumentNullException(nameof(facts));
            }

            var builder = new PresentationCueFrameBuilder(
                facts.TickIndex,
                facts.Diagnostics.ExtractedFactCount);

            for (var i = 0; i < _planners.Count; i++)
            {
                var planner = _planners[i];
                if (planner == null)
                {
                    continue;
                }

                planner.Plan(in facts, builder);
                builder.MarkPlannerRan();
            }

            return builder.Build();
        }
    }
}
