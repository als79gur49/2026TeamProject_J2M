using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class LegalityContextGovernanceTests
    {
        [Test]
        [Category("Extended")]
        public void TraverseContext_CoreShape_RemainsWithinPinnedFieldBudget()
        {
            var properties = GetPublicInstanceProperties(typeof(TraverseContext));

            Assert.That(properties.Select(property => property.Name), Is.EquivalentTo(new[]
            {
                "Snapshot",
                "Actor",
                "OriginCell",
                "CandidateCell",
                "EvaluationTopology",
                "TransitionRequirement",
                "ReservationStatus",
            }));
            Assert.That(properties, Has.Length.EqualTo(7));
        }

        [Test]
        [Category("Extended")]
        public void SettlementContext_CoreShape_RemainsWithinPinnedFieldBudget()
        {
            var properties = GetPublicInstanceProperties(typeof(SettlementContext));

            Assert.That(properties.Select(property => property.Name), Is.EquivalentTo(new[]
            {
                "OccupancySnapshot",
                "Actor",
                "TerminalCell",
                "TerminalTopology",
                "RequestedTerminalState",
                "ReservationStatus",
            }));
            Assert.That(properties, Has.Length.EqualTo(6));
        }

        [Test]
        [Category("Extended")]
        public void LegalityActorRef_CarriesResolvedSpatialState_NotRawSpatialSources()
        {
            var properties = GetPublicInstanceProperties(typeof(LegalityActorRef));

            Assert.That(properties.Select(property => property.Name), Is.EquivalentTo(new[]
            {
                "EntityId",
                "EntityType",
                "SpatialState",
            }));
            Assert.That(typeof(LegalityActorRef).GetProperty("SpatialState")?.PropertyType, Is.EqualTo(typeof(ResolvedSpatialState)));
            Assert.That(properties.Any(property => property.PropertyType == typeof(EntityBoardPresence)), Is.False);
            Assert.That(properties.Any(property => property.PropertyType == typeof(EnemyJumpRuntimeState)), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void TypedEvidence_RemainsOutsideBaseContextShape()
        {
            Assert.That(
                GetPublicInstanceProperties(typeof(JumpLandingEvidence)).Select(property => property.Name),
                Is.EquivalentTo(new[]
                {
                    "DamageProjectionSnapshot",
                    "LockedTargetCell",
                }));
            Assert.That(
                GetPublicInstanceProperties(typeof(ImpactFollowThroughEvidence)).Select(property => property.Name),
                Is.EquivalentTo(new[]
                {
                    "AttackSourceId",
                    "TargetId",
                    "DestroyResolutions",
                }));
        }

        private static PropertyInfo[] GetPublicInstanceProperties(System.Type type)
        {
            return type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        }
    }
}
