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
        public void PlayerContinuousLocomotionSettings_DefaultActionAssistSettleWindow_Is512Units()
        {
            var snapshot = PlayerContinuousLocomotionSettings.CreateDefault()
                .CreateAuthoritativeSnapshot(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(snapshot.ActionAssistSettleWindowUnits, Is.EqualTo(512));
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
            Assert.That(snapshot.CollisionRadiusUnits, Is.EqualTo(SimulationFixed.UnitsPerCell * 3 / 16));
        }

        [TestCase(0f, 0)]
        [TestCase(0.0625f, 256)]
        [TestCase(0.125f, 512)]
        [TestCase(0.1875f, 768)]
        [Category("Extended")]
        public void PlayerContinuousLocomotionSettings_ActionAssistSettleWindowCells_ConvertsToFixedUnits(
            float actionAssistSettleWindowCells,
            int expectedUnits)
        {
            var snapshot = new PlayerContinuousLocomotionSettings
            {
                ActionAssistSettleWindowCells = actionAssistSettleWindowCells,
            }.CreateAuthoritativeSnapshot(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(snapshot.ActionAssistSettleWindowUnits, Is.EqualTo(expectedUnits));
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

        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        [Category("Extended")]
        public void PlayerContinuousLocomotionSettings_InvalidActionAssistSettleWindow_Throws(
            float actionAssistSettleWindowCells)
        {
            var settings = new PlayerContinuousLocomotionSettings
            {
                ActionAssistSettleWindowCells = actionAssistSettleWindowCells,
            };

            Assert.Throws<ArgumentOutOfRangeException>(
                () => settings.CreateAuthoritativeSnapshot(GameplayTimingProfile.DefaultSimulationTicksPerSecond));
        }
    }
}
