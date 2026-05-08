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
            TickPresentationData presentationData,
            bool enableGlideWindTrail,
            bool enableChargeBoosterTrail)
        {
            desiredFollowers.Clear();
            removedEntityIds.Clear();

            if (presentationData == null ||
                (!enableGlideWindTrail && !enableChargeBoosterTrail))
            {
                return;
            }

            CollectRemovedEntityIds(presentationData);

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
                    removedEntityIds.Contains(signal.EntityId) ||
                    signal.Phase != EnemyGlidePhase.Active)
                {
                    continue;
                }

                desiredFollowers.Add(
                    new AttachedVfxFollowerDesiredState(
                        GameplayVfxCueId.From(EnemyVfxCue.GlideWindTrail),
                        signal.EntityId,
                        AttachedVfxFollowerStateKind.EnemyGlideActive,
                        signal.Sequence,
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
