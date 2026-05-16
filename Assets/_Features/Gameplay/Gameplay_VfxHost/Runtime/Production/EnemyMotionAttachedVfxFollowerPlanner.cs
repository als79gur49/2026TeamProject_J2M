using System.Collections.Generic;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Vfx;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    internal sealed class EnemyMotionAttachedVfxFollowerPlanner
    {
        private readonly List<AttachedVfxFollowerDesiredState> desiredFollowers = new();
        private readonly HashSet<int> removedEntityIds = new();

        public IReadOnlyList<AttachedVfxFollowerDesiredState> DesiredFollowers => desiredFollowers;

        public void Build(
            int tickIndex,
            TickPresentationData presentationData,
            bool enableGlideWindTrail,
            bool enableChargeBoosterTrail,
            bool enableBoxSlideFollowLoop,
            bool enableEnemyJumpWindupLoop)
        {
            desiredFollowers.Clear();
            removedEntityIds.Clear();

            if (presentationData == null ||
                (!enableGlideWindTrail &&
                 !enableChargeBoosterTrail &&
                 !enableBoxSlideFollowLoop &&
                 !enableEnemyJumpWindupLoop))
            {
                return;
            }

            CollectRemovedEntityIds(presentationData);

            if (enableBoxSlideFollowLoop)
            {
                AddBoxSlideFollowers(tickIndex, presentationData);
            }

            if (enableEnemyJumpWindupLoop)
            {
                AddJumpWindupFollowers(presentationData);
            }

            if (enableGlideWindTrail)
            {
                AddGlideFollowers(presentationData);
            }

            if (enableChargeBoosterTrail)
            {
                AddChargeFollowers(presentationData);
            }
        }

        public void Clear()
        {
            desiredFollowers.Clear();
            removedEntityIds.Clear();
        }

        public void RemoveCue(GameplayVfxCueId cueId)
        {
            desiredFollowers.RemoveAll(state => state.CueId == cueId);
        }

        private void AddGlideFollowers(TickPresentationData presentationData)
        {
            var signals = presentationData.EnemyGlideSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.EntityId <= 0 ||
                    removedEntityIds.Contains(signal.EntityId))
                {
                    continue;
                }

                if (!TryResolveGlideFollowerCue(signal.Phase, out var cueId, out var stateKind))
                {
                    continue;
                }

                desiredFollowers.Add(
                    new AttachedVfxFollowerDesiredState(
                        cueId,
                        signal.EntityId,
                        stateKind,
                        signal.Sequence,
                        Vector3.zero,
                        Quaternion.identity));
            }
        }

        private void AddBoxSlideFollowers(
            int tickIndex,
            TickPresentationData presentationData)
        {
            var motions = presentationData.EntityMotions;
            for (var i = 0; i < motions.Count; i++)
            {
                var motion = motions[i];
                if (motion.MotionKind != TickEntityMotionKind.BoxSlide ||
                    motion.EntityId <= 0 ||
                    removedEntityIds.Contains(motion.EntityId))
                {
                    continue;
                }

                desiredFollowers.Add(
                    new AttachedVfxFollowerDesiredState(
                        GameplayVfxCueId.From(BoxVfxCue.BoxSlideFollowLoop),
                        motion.EntityId,
                        AttachedVfxFollowerStateKind.BoxSlideFollow,
                        BoxSlideTrailVfxCommandBuilder.ComputeSequenceId(tickIndex, motion),
                        Vector3.zero,
                        Quaternion.identity));
            }
        }

        private void AddJumpWindupFollowers(TickPresentationData presentationData)
        {
            var signals = presentationData.EnemyJumpSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.EntityId <= 0 ||
                    removedEntityIds.Contains(signal.EntityId) ||
                    signal.Phase != EnemyJumpPhase.Windup)
                {
                    continue;
                }

                desiredFollowers.Add(
                    new AttachedVfxFollowerDesiredState(
                        GameplayVfxCueId.From(EnemyVfxCue.JumperWindupLoop),
                        signal.EntityId,
                        AttachedVfxFollowerStateKind.EnemyJumpWindup,
                        signal.Sequence > 0 ? signal.Sequence : signal.EntityId,
                        Vector3.zero,
                        Quaternion.identity));
            }
        }

        private void AddChargeFollowers(TickPresentationData presentationData)
        {
            var signals = presentationData.EnemyChargeSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.EntityId <= 0 ||
                    removedEntityIds.Contains(signal.EntityId) ||
                    signal.Phase != EnemyChargePhase.Active)
                {
                    continue;
                }

                desiredFollowers.Add(
                    new AttachedVfxFollowerDesiredState(
                        GameplayVfxCueId.From(EnemyVfxCue.ChargeBoosterTrail),
                        signal.EntityId,
                        AttachedVfxFollowerStateKind.EnemyChargeActive,
                        signal.Sequence,
                        Vector3.zero,
                        Quaternion.identity));
            }
        }

        private static bool TryResolveGlideFollowerCue(
            EnemyGlidePhase phase,
            out GameplayVfxCueId cueId,
            out AttachedVfxFollowerStateKind stateKind)
        {
            switch (phase)
            {
                case EnemyGlidePhase.Windup:
                    cueId = GameplayVfxCueId.From(EnemyVfxCue.GlideWindupLoop);
                    stateKind = AttachedVfxFollowerStateKind.EnemyGlideWindup;
                    return true;
                case EnemyGlidePhase.Active:
                    cueId = GameplayVfxCueId.From(EnemyVfxCue.GlideWindTrail);
                    stateKind = AttachedVfxFollowerStateKind.EnemyGlideActive;
                    return true;
                case EnemyGlidePhase.Recovery:
                    cueId = GameplayVfxCueId.From(EnemyVfxCue.GlideRecoverLoop);
                    stateKind = AttachedVfxFollowerStateKind.EnemyGlideRecover;
                    return true;
                default:
                    cueId = default;
                    stateKind = AttachedVfxFollowerStateKind.None;
                    return false;
            }
        }

        private void CollectRemovedEntityIds(TickPresentationData presentationData)
        {
            var exitSignals = presentationData.EntityExitSignals;
            for (var i = 0; i < exitSignals.Count; i++)
            {
                removedEntityIds.Add(exitSignals[i].ExitedEntityId);
            }

            var visibilityChanges = presentationData.VisibilityChanges;
            for (var i = 0; i < visibilityChanges.Count; i++)
            {
                var change = visibilityChanges[i];
                if (change.ChangeKind == TickVisibilityChangeKind.Remove)
                {
                    removedEntityIds.Add(change.EntityId);
                }
            }
        }
    }
}
