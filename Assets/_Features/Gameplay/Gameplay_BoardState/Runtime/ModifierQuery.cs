using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class ModifierQuery
    {
        public static bool ShouldParticipateInGameplayQueries(in ResolvedSpatialState spatialState)
        {
            SpatialStateSemantics.EnsureProductionSupported(spatialState.Kind);
            return SpatialStateSemantics.ParticipatesInGameplayQueries(spatialState);
        }

        public static bool ShouldParticipateInTraversalBlocking(in ResolvedSpatialState spatialState)
        {
            SpatialStateSemantics.EnsureProductionSupported(spatialState.Kind);
            return SpatialStateSemantics.ParticipatesInTraversalBlocking(spatialState);
        }

        public static bool ShouldParticipateInSettlementBlocking(in ResolvedSpatialState spatialState)
        {
            SpatialStateSemantics.EnsureProductionSupported(spatialState.Kind);
            return SpatialStateSemantics.ParticipatesInSettlementBlocking(spatialState);
        }

        public static bool ShouldParticipateInTargetSelection(in ResolvedSpatialState spatialState)
        {
            SpatialStateSemantics.EnsureProductionSupported(spatialState.Kind);
            return SpatialStateSemantics.ParticipatesInTargetSelection(spatialState);
        }

        public static bool ShouldParticipateInEnemyCurrentLockRetention(
            in ResolvedSpatialState spatialState,
            CurrentEnemyLockRetentionEvidence evidence)
        {
            SpatialStateSemantics.EnsureProductionSupported(spatialState.Kind);
            return SpatialStateSemantics.ParticipatesInGameplayQueries(spatialState);
        }

        public static LegalityCapabilitySet GetTraversalCapabilities(in LegalityActorRef actor)
        {
            SpatialStateSemantics.EnsureProductionSupported(actor.SpatialState.Kind);
            return StateQuery.GetBaseCapabilities(actor.SpatialState);
        }

        public static bool IgnoresTraversalBlocker(
            in LegalityCapabilitySet capabilities,
            in LegalityBlocker blocker)
        {
            return blocker.Kind switch
            {
                LegalityBlockerKind.Unit => capabilities.Has(LegalityCapabilityId.IgnoreTraversalUnitBlocker),
                LegalityBlockerKind.Solid => capabilities.Has(LegalityCapabilityId.IgnoreTraversalSolidBlocker),
                _ => false,
            };
        }

        public static LegalityModifierSet GetJumpLandingModifiers(
            in SettlementContext context,
            JumpLandingEvidence evidence)
        {
            return context.TerminalCell == evidence.LockedTargetCell
                ? LegalityModifierSet.None.With(LegalityModifierId.ExclusiveLockedPlayerExactStackAllowance)
                : LegalityModifierSet.None;
        }

        public static LegalityModifierSet GetImpactFollowThroughModifiers(ImpactFollowThroughEvidence evidence)
        {
            return HasAcceptedImpactDestroy(
                evidence.DestroyResolutions,
                evidence.AttackSourceId,
                evidence.TargetId)
                ? LegalityModifierSet.None.With(LegalityModifierId.AcceptedDestroyVacatesTarget)
                : LegalityModifierSet.None;
        }

        private static bool HasAcceptedImpactDestroy(
            IReadOnlyList<DestroyResolutionRecord> destroyResolutions,
            int sourceEntityId,
            int targetEntityId)
        {
            for (var i = 0; i < destroyResolutions.Count; i++)
            {
                if (destroyResolutions[i].Accepted &&
                    destroyResolutions[i].SourceId == sourceEntityId &&
                    destroyResolutions[i].TargetId == targetEntityId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
