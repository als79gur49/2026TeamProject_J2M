using System;
using System.IO;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class PlayerFree2DLocomotionAuthoringTests
    {
        [Test]
        [Category("Extended")]
        public void PlayerFree2DLocomotionAuthoring_DefaultCollisionRadius_IsZero()
        {
            var snapshot = PlayerFree2DLocomotionAuthoring.CreateDefault()
                .Compile(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(snapshot.CollisionRadiusUnits, Is.EqualTo(0));
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2DLocomotionAuthoring_DefaultTiming_UsesPlayerOwnedValue()
        {
            var snapshot = PlayerFree2DLocomotionAuthoring.CreateDefault()
                .Compile(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(snapshot.SecondsPerCellAtFullSpeed, Is.EqualTo(0.33333334f));
            Assert.That(snapshot.TicksPerCell, Is.EqualTo(20));
            Assert.That(snapshot.SpeedUnitsPerTick, Is.EqualTo(204));
            Assert.That(snapshot.UnitsPerTickRemainder, Is.EqualTo(16));
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2DLocomotionAuthoring_DefaultActionAssistSettleWindow_Is512Units()
        {
            var snapshot = PlayerFree2DLocomotionAuthoring.CreateDefault()
                .Compile(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(snapshot.ActionAssistSettleWindowUnits, Is.EqualTo(512));
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2DLocomotionAuthoring_CollisionRadiusCells_ConvertsToFixedUnits()
        {
            var settings = PlayerFree2DLocomotionAuthoring.CreateDefault();
            settings.CollisionRadiusCells = 0.1875f;
            var snapshot = settings.Compile(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(snapshot.CollisionRadiusUnits, Is.EqualTo(768));
            Assert.That(snapshot.CollisionRadiusUnits, Is.EqualTo(KinematicFixed.UnitsPerCell * 3 / 16));
        }

        [TestCase(0f, 0)]
        [TestCase(0.0625f, 256)]
        [TestCase(0.125f, 512)]
        [TestCase(0.1875f, 768)]
        [Category("Extended")]
        public void PlayerFree2DLocomotionAuthoring_ActionAssistSettleWindowCells_ConvertsToFixedUnits(
            float actionAssistSettleWindowCells,
            int expectedUnits)
        {
            var settings = PlayerFree2DLocomotionAuthoring.CreateDefault();
            settings.ActionAssistSettleWindowCells = actionAssistSettleWindowCells;
            var snapshot = settings.Compile(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(snapshot.ActionAssistSettleWindowUnits, Is.EqualTo(expectedUnits));
        }

        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        [Category("Extended")]
        public void PlayerFree2DLocomotionAuthoring_InvalidCollisionRadius_Throws(float collisionRadiusCells)
        {
            var settings = PlayerFree2DLocomotionAuthoring.CreateDefault();
            settings.CollisionRadiusCells = collisionRadiusCells;

            Assert.Throws<ArgumentOutOfRangeException>(
                () => settings.Compile(GameplayTimingProfile.DefaultSimulationTicksPerSecond));
        }

        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        [Category("Extended")]
        public void PlayerFree2DLocomotionAuthoring_InvalidActionAssistSettleWindow_Throws(
            float actionAssistSettleWindowCells)
        {
            var settings = PlayerFree2DLocomotionAuthoring.CreateDefault();
            settings.ActionAssistSettleWindowCells = actionAssistSettleWindowCells;

            Assert.Throws<ArgumentOutOfRangeException>(
                () => settings.Compile(GameplayTimingProfile.DefaultSimulationTicksPerSecond));
        }

        [TestCase(0f)]
        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(2.01f)]
        [Category("Extended")]
        public void PlayerFree2DLocomotionAuthoring_InvalidSecondsPerCell_Throws(float secondsPerCellAtFullSpeed)
        {
            var settings = PlayerFree2DLocomotionAuthoring.CreateDefault();
            settings.SecondsPerCellAtFullSpeed = secondsPerCellAtFullSpeed;

            Assert.Throws<ArgumentOutOfRangeException>(
                () => settings.Compile(GameplayTimingProfile.DefaultSimulationTicksPerSecond));
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2DLocomotionSource_DoesNotReferenceUnitOrEnemyTimingSettings()
        {
            const string path =
                "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/PlayerFree2DLocomotionSettings.cs";

            var source = File.ReadAllText(path);

            Assert.That(source, Does.Not.Contain("UnitKinematicLocomotionTimingSettings"));
            Assert.That(source, Does.Not.Contain("EnemyLocomotionTimingSettings"));
        }
    }
}
