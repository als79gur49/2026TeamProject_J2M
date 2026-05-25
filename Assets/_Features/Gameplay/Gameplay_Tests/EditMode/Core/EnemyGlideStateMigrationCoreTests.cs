using System;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Tests.Support.Pure;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class EnemyGlideStateMigrationCoreTests
    {
        private const int LegacyLandingPendingPhaseValue = 3;

        [Test]
        [Category("Core")]
        public void EnemyGlideState_LegacyLandingPendingPhase_NormalizesToActiveWantsRecover()
        {
            var state = SerializedFieldTestUtility.WithSerializedField(
                default(EnemyGlideRuntimeState),
                "phase",
                (EnemyGlidePhase)LegacyLandingPendingPhaseValue);

            AssertLegacyLandingPendingNormalizesToActiveWantsRecover(state);
            AssertNoPublicLandingPendingApi();
        }

        [Test]
        [Category("Core")]
        public void EnemyGlideState_LegacyIsLandingPendingFlag_NormalizesToActiveWantsRecover()
        {
            var state = SerializedFieldTestUtility.WithSerializedField(
                default(EnemyGlideRuntimeState),
                "phase",
                EnemyGlidePhase.Cooldown);
            state = SerializedFieldTestUtility.WithSerializedField(state, "isLandingPending", true);

            AssertLegacyLandingPendingNormalizesToActiveWantsRecover(state);
            AssertNoPublicLandingPendingApi();
        }

        [Test]
        [Category("Core")]
        public void EnemyGlidePhase_SerializedValues_AreStableAfterLandingPendingRemoval()
        {
            Assert.That((int)EnemyGlidePhase.Active, Is.EqualTo(2));
            Assert.That(
                (int)EnemyGlidePhase.Recovery,
                Is.EqualTo(4),
                "Recovery must remain 4 because serialized value 3 was previously LandingPending and is reserved for legacy normalization.");
            Assert.That(
                (int)EnemyGlidePhase.Cooldown,
                Is.EqualTo(5),
                "Cooldown must remain 5 to avoid migrating old serialized enum data.");
            Assert.That(
                Enum.GetNames(typeof(EnemyGlidePhase)),
                Has.No.Member("LandingPending"),
                "Serialized value 3 is reserved for removed LandingPending legacy data and must not be reintroduced as a public enum value.");
        }

        [Test]
        [Category("Core")]
        public void EnemyGlideState_LegacyLandingPendingCell_IsPrivateCompatibilityOnly()
        {
            var legacyCell = new SurfaceCell(FaceId.Back, 7, 9);
            var state = SerializedFieldTestUtility.WithSerializedField(
                default(EnemyGlideRuntimeState),
                "phase",
                (EnemyGlidePhase)LegacyLandingPendingPhaseValue);
            state = SerializedFieldTestUtility.WithSerializedField(state, "landingPendingCell", legacyCell);

            AssertLegacyLandingPendingNormalizesToActiveWantsRecover(state);
            AssertNoPublicLandingPendingApi();
        }

        private static void AssertLegacyLandingPendingNormalizesToActiveWantsRecover(EnemyGlideRuntimeState state)
        {
            Assert.That(state.Phase, Is.EqualTo(EnemyGlidePhase.Active));
            Assert.That(state.WantsRecover, Is.True);
            Assert.That(state.IsActive, Is.True);
            Assert.That(state.Phase, Is.Not.EqualTo(EnemyGlidePhase.Recovery));
            Assert.That(state.Phase, Is.Not.EqualTo(EnemyGlidePhase.Cooldown));
        }

        private static void AssertNoPublicLandingPendingApi()
        {
            var publicRuntimeMembers = typeof(EnemyGlideRuntimeState)
                .GetMembers()
                .Select(member => member.Name);
            var publicPhaseNames = Enum.GetNames(typeof(EnemyGlidePhase));

            Assert.That(
                publicRuntimeMembers.Any(name => name.Contains("LandingPending", StringComparison.Ordinal)),
                Is.False);
            Assert.That(publicPhaseNames, Has.No.Member("LandingPending"));
        }
    }
}
