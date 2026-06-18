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
        public void PlayerFree2DLocomotionAuthoringResolver_NoOverride_ReturnsBaseline()
        {
            var baseline = PlayerFree2DLocomotionAuthoring.CreateDefault();
            baseline.SecondsPerCellAtFullSpeed = 0.4f;
            baseline.CollisionRadiusCells = 0.125f;
            baseline.ActionAssistSettleWindowCells = 0.1875f;

            var resolved = PlayerFree2DLocomotionAuthoringResolver.Resolve(
                baseline,
                PlayerFree2DLocomotionOverride.None);

            Assert.That(resolved.SecondsPerCellAtFullSpeed, Is.EqualTo(0.4f));
            Assert.That(resolved.CollisionRadiusCells, Is.EqualTo(0.125f));
            Assert.That(resolved.ActionAssistSettleWindowCells, Is.EqualTo(0.1875f));
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2DLocomotionAuthoringResolver_PartialOverride_ChangesOnlyEnabledFields()
        {
            var baseline = PlayerFree2DLocomotionAuthoring.CreateDefault();
            baseline.SecondsPerCellAtFullSpeed = 0.4f;
            baseline.CollisionRadiusCells = 0.125f;
            baseline.ActionAssistSettleWindowCells = 0.1875f;
            var overrideValue = PlayerFree2DLocomotionOverride.CreateCollisionAndActionAssist(
                collisionRadiusCells: 0.25f,
                actionAssistSettleWindowCells: 0.0625f);

            var resolved = PlayerFree2DLocomotionAuthoringResolver.Resolve(baseline, overrideValue);
            var snapshot = resolved.Compile(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(snapshot.SecondsPerCellAtFullSpeed, Is.EqualTo(0.4f));
            Assert.That(snapshot.TicksPerCell, Is.EqualTo(24));
            Assert.That(snapshot.CollisionRadiusUnits, Is.EqualTo(1024));
            Assert.That(snapshot.ActionAssistSettleWindowUnits, Is.EqualTo(256));
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2DLocomotionAuthoringResolver_ExplicitDefaultValuedOverride_IsApplied()
        {
            var baseline = PlayerFree2DLocomotionAuthoring.CreateDefault();
            baseline.CollisionRadiusCells = 0.125f;
            baseline.ActionAssistSettleWindowCells = 0.1875f;
            var overrideValue = PlayerFree2DLocomotionOverride.Create(
                overrideSecondsPerCellAtFullSpeed: false,
                secondsPerCellAtFullSpeed: 0f,
                overrideCollisionRadiusCells: true,
                collisionRadiusCells: 0f,
                overrideActionAssistSettleWindowCells: true,
                actionAssistSettleWindowCells: 0.125f);

            var resolved = PlayerFree2DLocomotionAuthoringResolver.Resolve(baseline, overrideValue);
            var snapshot = resolved.Compile(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(snapshot.CollisionRadiusUnits, Is.EqualTo(0));
            Assert.That(snapshot.ActionAssistSettleWindowUnits, Is.EqualTo(512));
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2DLocomotionAuthoringResolver_InvalidOverride_FailsAtCompile()
        {
            var baseline = PlayerFree2DLocomotionAuthoring.CreateDefault();
            var overrideValue = PlayerFree2DLocomotionOverride.Create(
                overrideSecondsPerCellAtFullSpeed: false,
                secondsPerCellAtFullSpeed: 0f,
                overrideCollisionRadiusCells: true,
                collisionRadiusCells: -0.01f,
                overrideActionAssistSettleWindowCells: false,
                actionAssistSettleWindowCells: 0f);

            var resolved = PlayerFree2DLocomotionAuthoringResolver.Resolve(baseline, overrideValue);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => resolved.Compile(GameplayTimingProfile.DefaultSimulationTicksPerSecond));
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

        [Test]
        [Category("Extended")]
        public void TickPipeline_DoesNotCreateDefaultPlayerFree2DSettingsAtRuntime()
        {
            const string path =
                "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs";

            var source = File.ReadAllText(path);

            Assert.That(source, Does.Not.Contain("PlayerFree2DLocomotionAuthoring.CreateDefault"));
            Assert.That(source, Does.Not.Contain("GameplayTimingProfile.DefaultSimulationTicksPerSecond"));
        }

        [Test]
        [Category("Extended")]
        public void StageBackedInstaller_DescribesPlayerFree2DOverrideWithoutMutatingBaseline()
        {
            const string path =
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstaller.cs";

            var source = File.ReadAllText(path);

            Assert.That(source, Does.Contain("TryGetPlayerFree2DLocomotionOverride"));
            Assert.That(source, Does.Contain("PlayerFree2DLocomotionOverride.CreateCollisionAndActionAssist"));
            Assert.That(source, Does.Not.Contain("configuration.PlayerFree2DLocomotion ="));
        }
    }
}
