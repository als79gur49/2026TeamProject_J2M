using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;

namespace Game.Feature.Gameplay.Loop
{
    public enum EnemyAudioSemanticCue
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

    public enum EnemyAudioSemanticOriginKind
    {
        None = 0,
        Action = 1,
        Jump = 2,
        Charge = 3,
        Death = 4,
        ForwardCellImpact = 5,
        Utility = 6,
        Glide = 7,
        Summon = 8,
        Move = 9,
        Stationary = 10,
    }

    public enum EnemyAudioSemanticPhase
    {
        None = 0,
        Windup = 1,
        Active = 2,
        Recover = 3,
        Landing = 4,
        Death = 5,
        Impact = 6,
        Move = 7,
        StationaryActive = 8,
    }

    public readonly struct EnemyAudioSemanticEvent
    {
        public EnemyAudioSemanticEvent(
            int ownerEntityId,
            EnemyAudioSemanticCue cue,
            int tickIndex,
            int sourceSequenceId,
            EnemyAudioSemanticOriginKind originKind,
            EnemyAudioSemanticPhase phase,
            int targetEntityId = 0,
            int sourceActionKind = 0,
            int sourceOutcome = 0,
            int sourceCause = 0,
            int timing = 0,
            SurfaceCell sourceCell = default,
            SurfaceCell targetCell = default,
            bool hasSourceCell = false,
            bool hasTargetCell = false,
            Direction direction = Direction.None,
            int impactTick = 0,
            int impactId = 0,
            int presentationKey = 0,
            float visualContactNormalizedTime = 0f)
        {
            OwnerEntityId = Math.Max(0, ownerEntityId);
            Cue = cue;
            TickIndex = Math.Max(0, tickIndex);
            SourceSequenceId = Math.Max(0, sourceSequenceId);
            OriginKind = originKind;
            Phase = phase;
            TargetEntityId = Math.Max(0, targetEntityId);
            SourceActionKind = Math.Max(0, sourceActionKind);
            SourceOutcome = Math.Max(0, sourceOutcome);
            SourceCause = Math.Max(0, sourceCause);
            Timing = Math.Max(0, timing);
            SourceCell = sourceCell;
            TargetCell = targetCell;
            HasSourceCell = hasSourceCell;
            HasTargetCell = hasTargetCell;
            Direction = direction;
            ImpactTick = Math.Max(0, impactTick);
            ImpactId = Math.Max(0, impactId);
            PresentationKey = Math.Max(0, presentationKey);
            VisualContactNormalizedTime = ClampNormalized(visualContactNormalizedTime);
        }

        public int OwnerEntityId { get; }

        public EnemyAudioSemanticCue Cue { get; }

        public int TickIndex { get; }

        public int SourceSequenceId { get; }

        public EnemyAudioSemanticOriginKind OriginKind { get; }

        public EnemyAudioSemanticPhase Phase { get; }

        public int TargetEntityId { get; }

        public int SourceActionKind { get; }

        public int SourceOutcome { get; }

        public int SourceCause { get; }

        public int Timing { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell TargetCell { get; }

        public bool HasSourceCell { get; }

        public bool HasTargetCell { get; }

        public Direction Direction { get; }

        public int ImpactTick { get; }

        public int ImpactId { get; }

        public int PresentationKey { get; }

        public float VisualContactNormalizedTime { get; }

        public bool IsValid =>
            OwnerEntityId > 0 &&
            Cue != EnemyAudioSemanticCue.None &&
            OriginKind != EnemyAudioSemanticOriginKind.None &&
            Phase != EnemyAudioSemanticPhase.None;

        private static float ClampNormalized(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            return value >= 1f ? 1f : value;
        }
    }

    public static class EnemyAudioSemanticProjector
    {
        private static readonly IReadOnlyList<EnemyAudioSemanticEvent> EmptyEvents =
            new ReadOnlyCollection<EnemyAudioSemanticEvent>(new List<EnemyAudioSemanticEvent>());

        public static IReadOnlyList<EnemyAudioSemanticEvent> Project(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var presentationData = result.PresentationData ?? TickPresentationData.Empty;
            var enemyEntityIds = BuildEnemyEntityIdSet(result.FinalEntities);
            var motionFactEntityIds = BuildMotionFactEntityIdSet(presentationData);
            var events = new List<EnemyAudioSemanticEvent>();
            var emitted = new HashSet<EnemyAudioSemanticIdentity>();

            BuildMoveEvents(presentationData, result.TickIndex, enemyEntityIds, events, emitted);
            BuildActionEvents(presentationData, result.TickIndex, events, emitted);
            BuildUtilityEvents(presentationData, result.TickIndex, events, emitted);
            BuildSummonActiveEvents(presentationData, result.TickIndex, events, emitted);
            BuildJumpEvents(presentationData, result.TickIndex, events, emitted);
            BuildGlideEvents(presentationData, result.TickIndex, events, emitted);
            BuildChargeEvents(presentationData, result.TickIndex, events, emitted);
            BuildForwardCellImpactEvents(presentationData, result.TickIndex, events, emitted);
            BuildDeathEvents(presentationData, result.TickIndex, events, emitted);
            BuildStationaryActiveEvents(result.FinalEntities, motionFactEntityIds, result.TickIndex, events, emitted);

            return events.Count == 0
                ? EmptyEvents
                : new ReadOnlyCollection<EnemyAudioSemanticEvent>(events);
        }

        private static HashSet<int> BuildEnemyEntityIdSet(IReadOnlyList<EntityState> finalEntities)
        {
            var entityIds = new HashSet<int>();
            if (finalEntities == null)
            {
                return entityIds;
            }

            for (var i = 0; i < finalEntities.Count; i++)
            {
                var entity = finalEntities[i];
                if (EntityRolePolicy.IsEnemyUnit(entity))
                {
                    entityIds.Add(entity.entityId);
                }
            }

            return entityIds;
        }

        private static HashSet<int> BuildMotionFactEntityIdSet(TickPresentationData presentationData)
        {
            var entityIds = new HashSet<int>();
            var motions = presentationData.EntityMotions;
            for (var i = 0; i < motions.Count; i++)
            {
                if (motions[i].EntityId > 0)
                {
                    entityIds.Add(motions[i].EntityId);
                }
            }

            var tracks = presentationData.KinematicMotionTracks;
            for (var i = 0; i < tracks.Count; i++)
            {
                if (tracks[i].EntityId > 0)
                {
                    entityIds.Add(tracks[i].EntityId);
                }
            }

            return entityIds;
        }

        private static void BuildMoveEvents(
            TickPresentationData presentationData,
            int tickIndex,
            ISet<int> enemyEntityIds,
            ICollection<EnemyAudioSemanticEvent> events,
            ISet<EnemyAudioSemanticIdentity> emitted)
        {
            var plannedMoveEntityIds = new HashSet<int>();
            var motions = presentationData.EntityMotions;
            for (var i = 0; i < motions.Count; i++)
            {
                var motion = motions[i];
                if (motion.MotionKind != TickEntityMotionKind.Move ||
                    !enemyEntityIds.Contains(motion.EntityId) ||
                    !plannedMoveEntityIds.Add(motion.EntityId))
                {
                    continue;
                }

                AddEvent(
                    events,
                    emitted,
                    new EnemyAudioSemanticEvent(
                        motion.EntityId,
                        EnemyAudioSemanticCue.Move,
                        tickIndex,
                        i + 1,
                        EnemyAudioSemanticOriginKind.Move,
                        EnemyAudioSemanticPhase.Move,
                        sourceActionKind: (int)TickEntityMotionKind.Move,
                        sourceCell: motion.SourceCell,
                        targetCell: motion.DestinationCell,
                        hasSourceCell: true,
                        hasTargetCell: true,
                        direction: motion.DestinationFacing ?? motion.SourceFacing ?? Direction.None));
            }

            var tracks = presentationData.KinematicMotionTracks;
            for (var i = 0; i < tracks.Count; i++)
            {
                var track = tracks[i];
                if (!IsEnemyLocomotionMoveTrack(track, tickIndex, enemyEntityIds) ||
                    !plannedMoveEntityIds.Add(track.EntityId))
                {
                    continue;
                }

                AddEvent(
                    events,
                    emitted,
                    new EnemyAudioSemanticEvent(
                        track.EntityId,
                        EnemyAudioSemanticCue.Move,
                        tickIndex,
                        track.StartedTick > 0 ? track.StartedTick : i + 1,
                        EnemyAudioSemanticOriginKind.Move,
                        EnemyAudioSemanticPhase.Move,
                        sourceActionKind: (int)track.MotionMode,
                        sourceCell: track.SourceAnchorCell,
                        targetCell: track.DestinationAnchorCell,
                        hasSourceCell: true,
                        hasTargetCell: true,
                        direction: track.DestinationFacing ?? track.SourceFacing ?? Direction.None));
            }
        }

        private static bool IsEnemyLocomotionMoveTrack(
            in TickKinematicMotionTrack track,
            int tickIndex,
            ISet<int> enemyEntityIds)
        {
            return track.EntityId > 0 &&
                   enemyEntityIds.Contains(track.EntityId) &&
                   track.EntityType == EntityType.Unit &&
                   track.TerminalKind == TickKinematicMotionTerminalKind.None &&
                   track.MotionMode == MotionMode.Voluntary &&
                   track.ForcedMotionOp == ForcedMotionOp.None &&
                   HasActualKinematicMovement(track) &&
                   IsKinematicMoveStartTick(track, tickIndex);
        }

        private static bool HasActualKinematicMovement(in TickKinematicMotionTrack track)
        {
            return !track.SourceAnchorCell.Equals(track.DestinationAnchorCell) ||
                   !track.SourceLocalOffset.Equals(track.DestinationLocalOffset);
        }

        private static bool IsKinematicMoveStartTick(in TickKinematicMotionTrack track, int tickIndex)
        {
            return track.StartedTick <= 0 ||
                   track.StartedTick == tickIndex;
        }

        private static void BuildActionEvents(
            TickPresentationData presentationData,
            int tickIndex,
            ICollection<EnemyAudioSemanticEvent> events,
            ISet<EnemyAudioSemanticIdentity> emitted)
        {
            var signals = presentationData.EnemyActionSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.EntityId <= 0)
                {
                    continue;
                }

                var sequenceId = signal.ActiveActionSequence > 0 ? signal.ActiveActionSequence : i + 1;
                if (signal.StartedThisTick)
                {
                    AddEvent(
                        events,
                        emitted,
                        CreateActionEvent(
                            signal,
                            tickIndex,
                            sequenceId,
                            EnemyAudioSemanticCue.Windup,
                            EnemyAudioSemanticPhase.Windup));
                }

                if (TryResolveActionExecution(signal, out var cue, out var phase))
                {
                    AddEvent(events, emitted, CreateActionEvent(signal, tickIndex, sequenceId, cue, phase));
                }

                if (signal.StartedRecoveryThisTick)
                {
                    AddEvent(
                        events,
                        emitted,
                        CreateActionEvent(
                            signal,
                            tickIndex,
                            sequenceId,
                            EnemyAudioSemanticCue.Recover,
                            EnemyAudioSemanticPhase.Recover));
                }
            }
        }

        private static EnemyAudioSemanticEvent CreateActionEvent(
            in TickEnemyActionPresentationSignal signal,
            int tickIndex,
            int sequenceId,
            EnemyAudioSemanticCue cue,
            EnemyAudioSemanticPhase phase)
        {
            return new EnemyAudioSemanticEvent(
                signal.EntityId,
                cue,
                tickIndex,
                sequenceId,
                EnemyAudioSemanticOriginKind.Action,
                phase,
                sourceActionKind: (int)signal.ActiveActionKind,
                sourceOutcome: (int)signal.PresentationOutcome,
                sourceCause: (int)signal.PresentationSource);
        }

        private static bool TryResolveActionExecution(
            in TickEnemyActionPresentationSignal signal,
            out EnemyAudioSemanticCue cue,
            out EnemyAudioSemanticPhase phase)
        {
            cue = EnemyAudioSemanticCue.None;
            phase = EnemyAudioSemanticPhase.None;
            if (!signal.ExecutedThisTick ||
                signal.PresentationOutcome == EnemyActionPresentationOutcome.RejectedByReceiverCooldown ||
                signal.PresentationSource == EnemyActionPresentationSource.ForwardCellImpact)
            {
                return false;
            }

            if (signal.PresentationSource == EnemyActionPresentationSource.PassiveContact)
            {
                if (signal.PresentationOutcome == EnemyActionPresentationOutcome.RejectedByPlayerInvincible)
                {
                    return false;
                }

                cue = EnemyAudioSemanticCue.PassiveContact;
                phase = EnemyAudioSemanticPhase.Active;
                return true;
            }

            cue = EnemyAudioSemanticCue.Active;
            phase = EnemyAudioSemanticPhase.Active;
            return true;
        }

        private static void BuildUtilityEvents(
            TickPresentationData presentationData,
            int tickIndex,
            ICollection<EnemyAudioSemanticEvent> events,
            ISet<EnemyAudioSemanticIdentity> emitted)
        {
            var signals = presentationData.EnemyUtilitySignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.EntityId <= 0 ||
                    signal.Kind != EnemyUtilityPresentationKind.GravityFieldAura ||
                    !TryResolveUtilityCue(signal.Phase, out var cue, out var phase))
                {
                    continue;
                }

                var sequenceId = ResolveUtilitySequence(signal, i);
                AddEvent(
                    events,
                    emitted,
                    new EnemyAudioSemanticEvent(
                        signal.EntityId,
                        cue,
                        tickIndex,
                        sequenceId,
                        EnemyAudioSemanticOriginKind.Utility,
                        phase,
                        sourceActionKind: (int)signal.Kind,
                        sourceOutcome: (int)signal.Phase,
                        sourceCause: (int)signal.Kind));
            }
        }

        private static bool TryResolveUtilityCue(
            EnemyUtilityPresentationPhase utilityPhase,
            out EnemyAudioSemanticCue cue,
            out EnemyAudioSemanticPhase phase)
        {
            switch (utilityPhase)
            {
                case EnemyUtilityPresentationPhase.WindupStarted:
                    cue = EnemyAudioSemanticCue.Windup;
                    phase = EnemyAudioSemanticPhase.Windup;
                    return true;
                case EnemyUtilityPresentationPhase.ActiveStarted:
                case EnemyUtilityPresentationPhase.AttackStarted:
                    cue = EnemyAudioSemanticCue.Active;
                    phase = EnemyAudioSemanticPhase.Active;
                    return true;
                case EnemyUtilityPresentationPhase.RecoverStarted:
                    cue = EnemyAudioSemanticCue.Recover;
                    phase = EnemyAudioSemanticPhase.Recover;
                    return true;
                default:
                    cue = EnemyAudioSemanticCue.None;
                    phase = EnemyAudioSemanticPhase.None;
                    return false;
            }
        }

        private static int ResolveUtilitySequence(in TickEnemyUtilityPresentationSignal signal, int index)
        {
            if (signal.ActivationSequence > 0)
            {
                return signal.ActivationSequence;
            }

            if (signal.EffectIndex > 0)
            {
                return signal.EffectIndex;
            }

            if (signal.StartTick > 0 || signal.ExecuteTick > 0)
            {
                unchecked
                {
                    return (((Math.Max(0, signal.StartTick) + 1) * 397) ^
                            (Math.Max(0, signal.ExecuteTick) + 1)) & int.MaxValue;
                }
            }

            return index + 1;
        }

        private static void BuildSummonActiveEvents(
            TickPresentationData presentationData,
            int tickIndex,
            ICollection<EnemyAudioSemanticEvent> events,
            ISet<EnemyAudioSemanticIdentity> emitted)
        {
            var visibilityChanges = presentationData.VisibilityChanges;
            for (var i = 0; i < visibilityChanges.Count; i++)
            {
                var change = visibilityChanges[i];
                if (change.ChangeKind != TickVisibilityChangeKind.Spawn ||
                    !TryResolveSummonSourceEntityId(presentationData, change.EntityId, out var sourceEntityId))
                {
                    continue;
                }

                AddEvent(
                    events,
                    emitted,
                    new EnemyAudioSemanticEvent(
                        sourceEntityId,
                        EnemyAudioSemanticCue.Active,
                        tickIndex,
                        change.EntityId > 0 ? change.EntityId : i + 1,
                        EnemyAudioSemanticOriginKind.Summon,
                        EnemyAudioSemanticPhase.Active,
                        targetEntityId: change.EntityId,
                        sourceActionKind: (int)EnemyAudioSemanticOriginKind.Summon,
                        sourceOutcome: (int)TickVisibilityChangeKind.Spawn,
                        sourceCause: (int)TickVisibilityChangeKind.Spawn,
                        targetCell: change.Cell,
                        hasTargetCell: true,
                        direction: change.Facing,
                        presentationKey: change.EntityId));
            }
        }

        private static bool TryResolveSummonSourceEntityId(
            TickPresentationData presentationData,
            int summonedEntityId,
            out int sourceEntityId)
        {
            var bindings = presentationData.SummonedEnemyPresentationBindings;
            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding.EntityId == summonedEntityId &&
                    binding.HasEnemyDefinitionBinding &&
                    binding.SourceEntityId > 0)
                {
                    sourceEntityId = binding.SourceEntityId;
                    return true;
                }
            }

            sourceEntityId = 0;
            return false;
        }

        private static void BuildJumpEvents(
            TickPresentationData presentationData,
            int tickIndex,
            ICollection<EnemyAudioSemanticEvent> events,
            ISet<EnemyAudioSemanticIdentity> emitted)
        {
            var signals = presentationData.EnemyJumpSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.EntityId <= 0 || !signal.LandedThisTick)
                {
                    continue;
                }

                AddEvent(
                    events,
                    emitted,
                    new EnemyAudioSemanticEvent(
                        signal.EntityId,
                        EnemyAudioSemanticCue.Landing,
                        tickIndex,
                        signal.Sequence > 0 ? signal.Sequence : i + 1,
                        EnemyAudioSemanticOriginKind.Jump,
                        EnemyAudioSemanticPhase.Landing,
                        sourceActionKind: (int)EnemyAudioSemanticOriginKind.Jump,
                        sourceOutcome: (int)signal.Outcome,
                        sourceCell: signal.SourceCell,
                        targetCell: signal.PresentationTargetCell,
                        hasSourceCell: true,
                        hasTargetCell: true,
                        direction: signal.Facing));
            }
        }

        private static void BuildGlideEvents(
            TickPresentationData presentationData,
            int tickIndex,
            ICollection<EnemyAudioSemanticEvent> events,
            ISet<EnemyAudioSemanticIdentity> emitted)
        {
            var signals = presentationData.EnemyGlideSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.EntityId <= 0 ||
                    signal.PhaseElapsedTicks != 0 ||
                    !TryResolveGlideCue(signal.Phase, out var cue, out var phase))
                {
                    continue;
                }

                AddEvent(
                    events,
                    emitted,
                    new EnemyAudioSemanticEvent(
                        signal.EntityId,
                        cue,
                        tickIndex,
                        signal.Sequence > 0 ? signal.Sequence : i + 1,
                        EnemyAudioSemanticOriginKind.Glide,
                        phase,
                        sourceActionKind: (int)signal.Phase,
                        sourceOutcome: (int)signal.Phase,
                        sourceCell: signal.AnchorCell,
                        hasSourceCell: true));
            }
        }

        private static bool TryResolveGlideCue(
            EnemyGlidePhase glidePhase,
            out EnemyAudioSemanticCue cue,
            out EnemyAudioSemanticPhase phase)
        {
            switch (glidePhase)
            {
                case EnemyGlidePhase.Windup:
                    cue = EnemyAudioSemanticCue.Windup;
                    phase = EnemyAudioSemanticPhase.Windup;
                    return true;
                case EnemyGlidePhase.Active:
                    cue = EnemyAudioSemanticCue.Active;
                    phase = EnemyAudioSemanticPhase.Active;
                    return true;
                case EnemyGlidePhase.Recovery:
                    cue = EnemyAudioSemanticCue.Recover;
                    phase = EnemyAudioSemanticPhase.Recover;
                    return true;
                default:
                    cue = EnemyAudioSemanticCue.None;
                    phase = EnemyAudioSemanticPhase.None;
                    return false;
            }
        }

        private static void BuildChargeEvents(
            TickPresentationData presentationData,
            int tickIndex,
            ICollection<EnemyAudioSemanticEvent> events,
            ISet<EnemyAudioSemanticIdentity> emitted)
        {
            var signals = presentationData.EnemyChargeSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.EntityId <= 0 || !signal.StartedActiveThisTick)
                {
                    continue;
                }

                AddEvent(
                    events,
                    emitted,
                    new EnemyAudioSemanticEvent(
                        signal.EntityId,
                        EnemyAudioSemanticCue.Active,
                        tickIndex,
                        signal.Sequence > 0 ? signal.Sequence : i + 1,
                        EnemyAudioSemanticOriginKind.Charge,
                        EnemyAudioSemanticPhase.Active,
                        sourceActionKind: (int)EnemyAudioSemanticOriginKind.Charge,
                        sourceOutcome: (int)EnemyChargePhase.Active,
                        direction: signal.LockedDirection));
            }
        }

        private static void BuildForwardCellImpactEvents(
            TickPresentationData presentationData,
            int tickIndex,
            ICollection<EnemyAudioSemanticEvent> events,
            ISet<EnemyAudioSemanticIdentity> emitted)
        {
            var signals = presentationData.ForwardCellProjectileArrivalSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.SourceEnemyId <= 0 ||
                    !signal.ResolutionKind.IsValidArrival())
                {
                    continue;
                }

                AddEvent(
                    events,
                    emitted,
                    new EnemyAudioSemanticEvent(
                        signal.SourceEnemyId,
                        EnemyAudioSemanticCue.ForwardCellImpact,
                        tickIndex,
                        signal.PresentationKey > 0 ? signal.PresentationKey : i + 1,
                        EnemyAudioSemanticOriginKind.ForwardCellImpact,
                        EnemyAudioSemanticPhase.Impact,
                        targetEntityId: signal.TargetEntityId,
                        sourceActionKind: (int)EnemyAudioSemanticOriginKind.ForwardCellImpact,
                        sourceOutcome: (int)signal.ResolutionKind,
                        sourceCause: (int)signal.ResolutionKind,
                        targetCell: signal.TargetCell,
                        hasTargetCell: true,
                        direction: signal.Direction,
                        impactTick: signal.ImpactTick,
                        impactId: signal.ImpactId,
                        presentationKey: signal.PresentationKey));
            }
        }

        private static void BuildDeathEvents(
            TickPresentationData presentationData,
            int tickIndex,
            ICollection<EnemyAudioSemanticEvent> events,
            ISet<EnemyAudioSemanticIdentity> emitted)
        {
            var signals = presentationData.EntityExitSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.ExitedEntityId <= 0 ||
                    (signal.ExitCause != TickEntityExitCause.EnemyDeath &&
                     signal.ExitCause != TickEntityExitCause.Killed))
                {
                    continue;
                }

                AddEvent(
                    events,
                    emitted,
                    new EnemyAudioSemanticEvent(
                        signal.ExitedEntityId,
                        EnemyAudioSemanticCue.Death,
                        tickIndex,
                        signal.PresentationSeed > 0 ? signal.PresentationSeed : i + 1,
                        EnemyAudioSemanticOriginKind.Death,
                        EnemyAudioSemanticPhase.Death,
                        sourceActionKind: (int)EnemyAudioSemanticOriginKind.Death,
                        sourceOutcome: (int)signal.ExitCause,
                        sourceCause: (int)signal.ExitCause,
                        timing: (int)signal.Timing,
                        sourceCell: signal.SourceCell,
                        targetCell: signal.PresentationTargetCell,
                        hasSourceCell: true,
                        hasTargetCell: signal.HasPresentationTargetCell,
                        direction: signal.Facing,
                        visualContactNormalizedTime: signal.VisualContactNormalizedTime));
            }
        }

        private static void BuildStationaryActiveEvents(
            IReadOnlyList<EntityState> finalEntities,
            ISet<int> motionFactEntityIds,
            int tickIndex,
            ICollection<EnemyAudioSemanticEvent> events,
            ISet<EnemyAudioSemanticIdentity> emitted)
        {
            if (finalEntities == null)
            {
                return;
            }

            for (var i = 0; i < finalEntities.Count; i++)
            {
                var entity = finalEntities[i];
                if (!EntityRolePolicy.IsEnemyUnit(entity) ||
                    entity.entityId <= 0 ||
                    motionFactEntityIds.Contains(entity.entityId) ||
                    HasEventForOwner(events, entity.entityId))
                {
                    continue;
                }

                AddEvent(
                    events,
                    emitted,
                    new EnemyAudioSemanticEvent(
                        entity.entityId,
                        EnemyAudioSemanticCue.StationaryActive,
                        tickIndex,
                        i + 1,
                        EnemyAudioSemanticOriginKind.Stationary,
                        EnemyAudioSemanticPhase.StationaryActive));
            }
        }

        private static bool HasEventForOwner(IEnumerable<EnemyAudioSemanticEvent> events, int ownerEntityId)
        {
            foreach (var semanticEvent in events)
            {
                if (semanticEvent.OwnerEntityId == ownerEntityId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddEvent(
            ICollection<EnemyAudioSemanticEvent> events,
            ISet<EnemyAudioSemanticIdentity> emitted,
            in EnemyAudioSemanticEvent semanticEvent)
        {
            if (!semanticEvent.IsValid)
            {
                return;
            }

            var identity = EnemyAudioSemanticIdentity.Create(semanticEvent);
            if (!emitted.Add(identity))
            {
                return;
            }

            events.Add(semanticEvent);
        }

        private readonly struct EnemyAudioSemanticIdentity : IEquatable<EnemyAudioSemanticIdentity>
        {
            private EnemyAudioSemanticIdentity(
                int tickIndex,
                int ownerEntityId,
                EnemyAudioSemanticCue cue,
                EnemyAudioSemanticOriginKind originKind,
                EnemyAudioSemanticPhase phase,
                int sourceSequenceId,
                int targetEntityId,
                int impactTick,
                int impactId,
                int presentationKey)
            {
                TickIndex = tickIndex;
                OwnerEntityId = ownerEntityId;
                Cue = cue;
                OriginKind = originKind;
                Phase = phase;
                SourceSequenceId = sourceSequenceId;
                TargetEntityId = targetEntityId;
                ImpactTick = impactTick;
                ImpactId = impactId;
                PresentationKey = presentationKey;
            }

            private int TickIndex { get; }

            private int OwnerEntityId { get; }

            private EnemyAudioSemanticCue Cue { get; }

            private EnemyAudioSemanticOriginKind OriginKind { get; }

            private EnemyAudioSemanticPhase Phase { get; }

            private int SourceSequenceId { get; }

            private int TargetEntityId { get; }

            private int ImpactTick { get; }

            private int ImpactId { get; }

            private int PresentationKey { get; }

            public static EnemyAudioSemanticIdentity Create(in EnemyAudioSemanticEvent semanticEvent)
            {
                return new EnemyAudioSemanticIdentity(
                    semanticEvent.TickIndex,
                    semanticEvent.OwnerEntityId,
                    semanticEvent.Cue,
                    semanticEvent.OriginKind,
                    semanticEvent.Phase,
                    semanticEvent.SourceSequenceId,
                    semanticEvent.TargetEntityId,
                    semanticEvent.ImpactTick,
                    semanticEvent.ImpactId,
                    semanticEvent.PresentationKey);
            }

            public bool Equals(EnemyAudioSemanticIdentity other)
            {
                return TickIndex == other.TickIndex &&
                       OwnerEntityId == other.OwnerEntityId &&
                       Cue == other.Cue &&
                       OriginKind == other.OriginKind &&
                       Phase == other.Phase &&
                       SourceSequenceId == other.SourceSequenceId &&
                       TargetEntityId == other.TargetEntityId &&
                       ImpactTick == other.ImpactTick &&
                       ImpactId == other.ImpactId &&
                       PresentationKey == other.PresentationKey;
            }

            public override bool Equals(object obj)
            {
                return obj is EnemyAudioSemanticIdentity other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = TickIndex;
                    hash = (hash * 397) ^ OwnerEntityId;
                    hash = (hash * 397) ^ (int)Cue;
                    hash = (hash * 397) ^ (int)OriginKind;
                    hash = (hash * 397) ^ (int)Phase;
                    hash = (hash * 397) ^ SourceSequenceId;
                    hash = (hash * 397) ^ TargetEntityId;
                    hash = (hash * 397) ^ ImpactTick;
                    hash = (hash * 397) ^ ImpactId;
                    hash = (hash * 397) ^ PresentationKey;
                    return hash;
                }
            }
        }
    }
}
