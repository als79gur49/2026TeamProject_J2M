using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EnemyChargeStateTests
    {
        [Test]
        [Category("Core")]
        public void EnemyChargeQueries_StartCharge_InitializesCountersAndPhase()
        {
            var previousState = new EnemyChargeRuntimeState { sequence = 2 };
            var timingSettings = new EnemyChargeTimingSettings(windupTicks: 1, activeStepCooldownTicks: 2, recoverTicks: 3);

            var startedState = EnemyChargeQueries.StartCharge(
                previousState,
                Direction.Right,
                tickIndex: 5,
                timingSettings,
                reachableSteps: 4);

            Assert.That(startedState.phase, Is.EqualTo(EnemyChargePhase.Windup));
            Assert.That(startedState.sequence, Is.EqualTo(3));
            Assert.That(startedState.lockedDirection, Is.EqualTo(Direction.Right));
            Assert.That(startedState.windupEndTick, Is.EqualTo(6));
            Assert.That(startedState.remainingActiveSteps, Is.EqualTo(4));
            Assert.That(startedState.recoverRemainingTicks, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void EnemyChargeQueries_StartCharge_WithZeroWindup_StartsActiveWithoutConsumingStep()
        {
            var timingSettings = new EnemyChargeTimingSettings(windupTicks: 0, activeStepCooldownTicks: 1, recoverTicks: 2);

            var startedState = EnemyChargeQueries.StartCharge(
                previousState: default,
                lockedDirection: Direction.Up,
                tickIndex: 3,
                timingSettings,
                reachableSteps: 2);

            Assert.That(startedState.phase, Is.EqualTo(EnemyChargePhase.Active));
            Assert.That(startedState.remainingActiveSteps, Is.EqualTo(2));
            Assert.That(startedState.recoverRemainingTicks, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void EnemyChargeQueries_ConsumeActiveStep_DecrementsRemainingStepsOnly()
        {
            var activeState = new EnemyChargeRuntimeState
            {
                phase = EnemyChargePhase.Active,
                sequence = 4,
                lockedDirection = Direction.Left,
                windupEndTick = 8,
                remainingActiveSteps = 3,
                recoverRemainingTicks = 0,
            };

            var consumedState = EnemyChargeQueries.ConsumeActiveStep(activeState);

            Assert.That(consumedState.phase, Is.EqualTo(EnemyChargePhase.Active));
            Assert.That(consumedState.sequence, Is.EqualTo(4));
            Assert.That(consumedState.lockedDirection, Is.EqualTo(Direction.Left));
            Assert.That(consumedState.remainingActiveSteps, Is.EqualTo(2));
            Assert.That(consumedState.recoverRemainingTicks, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void EnemyChargeQueries_EnterRecoverAndTickRecover_UseChargeStateCountdownOnly()
        {
            var activeState = new EnemyChargeRuntimeState
            {
                phase = EnemyChargePhase.Active,
                sequence = 5,
                lockedDirection = Direction.Down,
                windupEndTick = 7,
                remainingActiveSteps = 0,
            };

            var recoverState = EnemyChargeQueries.EnterRecover(activeState, recoverTicks: 2);
            var tickedRecoverState = EnemyChargeQueries.TickRecover(recoverState);

            Assert.That(recoverState.phase, Is.EqualTo(EnemyChargePhase.Recover));
            Assert.That(recoverState.recoverRemainingTicks, Is.EqualTo(2));
            Assert.That(tickedRecoverState.phase, Is.EqualTo(EnemyChargePhase.Recover));
            Assert.That(tickedRecoverState.recoverRemainingTicks, Is.EqualTo(1));
            Assert.That(tickedRecoverState.remainingActiveSteps, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void EnemyChargeQueries_Clear_PreservesSequenceAndRemovesProgress()
        {
            var state = new EnemyChargeRuntimeState
            {
                phase = EnemyChargePhase.Recover,
                sequence = 6,
                lockedDirection = Direction.Right,
                windupEndTick = 9,
                remainingActiveSteps = 1,
                recoverRemainingTicks = 2,
            };

            var clearedState = EnemyChargeQueries.Clear(state);

            Assert.That(clearedState.phase, Is.EqualTo(EnemyChargePhase.None));
            Assert.That(clearedState.sequence, Is.EqualTo(6));
            Assert.That(clearedState.lockedDirection, Is.EqualTo(Direction.None));
            Assert.That(clearedState.windupEndTick, Is.Zero);
            Assert.That(clearedState.remainingActiveSteps, Is.Zero);
            Assert.That(clearedState.recoverRemainingTicks, Is.Zero);
        }
    }
}
