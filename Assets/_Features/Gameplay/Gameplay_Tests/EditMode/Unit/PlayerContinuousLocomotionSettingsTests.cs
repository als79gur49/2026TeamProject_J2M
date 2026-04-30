using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class PlayerContinuousLocomotionSettingsTests
    {
        [Test]
        [Category("Extended")]
        public void PlayerContinuousLocomotionSettings_DefaultCollisionRadius_IsZero()
        {
            var snapshot = PlayerContinuousLocomotionSettings.CreateDefault()
                .CreateAuthoritativeSnapshot(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(snapshot.CollisionRadiusUnits, Is.EqualTo(0));
        }

        [Test]
        [Category("Extended")]
        public void PlayerContinuousLocomotionSettings_CollisionRadiusCells_ConvertsToFixedUnits()
        {
            var snapshot = new PlayerContinuousLocomotionSettings
            {
                CollisionRadiusCells = 0.1875f,
            }.CreateAuthoritativeSnapshot(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(snapshot.CollisionRadiusUnits, Is.EqualTo(768));
            Assert.That(snapshot.CollisionRadiusUnits, Is.EqualTo(KinematicFixed.UnitsPerCell * 3 / 16));
        }

        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        [Category("Extended")]
        public void PlayerContinuousLocomotionSettings_InvalidCollisionRadius_Throws(float collisionRadiusCells)
        {
            var settings = new PlayerContinuousLocomotionSettings
            {
                CollisionRadiusCells = collisionRadiusCells,
            };

            Assert.Throws<ArgumentOutOfRangeException>(
                () => settings.CreateAuthoritativeSnapshot(GameplayTimingProfile.DefaultSimulationTicksPerSecond));
        }
    }
}
