using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Core
{
    [Category("Core")]
    public sealed class PlayerActionUseCounterStateCoreTests
    {
        private const int PlayerEntityId = 10;

        [TestCase(PlayerActionKind.Push, TickPlayerActionResolutionKind.Success)]
        [TestCase(PlayerActionKind.Push, TickPlayerActionResolutionKind.Impact)]
        [TestCase(PlayerActionKind.Flip, TickPlayerActionResolutionKind.Success)]
        [TestCase(PlayerActionKind.Flip, TickPlayerActionResolutionKind.Impact)]
        public void CanonicalPlayerSuccessfulPushFlip_IncrementsCombinedCount(
            PlayerActionKind actionKind,
            TickPlayerActionResolutionKind resolutionKind)
        {
            var state = CreateState();

            Assert.That(state.TryConsume(CreateActionSignal(PlayerEntityId, actionKind, 1, resolutionKind)), Is.True);
            Assert.That(state.Count, Is.EqualTo(1));
            Assert.That(state.VisibleCount, Is.Zero);
            Assert.That(state.IsVisible, Is.False);
            Assert.That(state.RevealCount(1), Is.True);
            Assert.That(state.VisibleCount, Is.EqualTo(1));
            Assert.That(state.IsVisible, Is.True);
            Assert.That(state.Alpha, Is.EqualTo(1f));
        }

        [Test]
        public void NonCountableAndOtherEntitySignals_AreIgnored()
        {
            var state = CreateState();
            var rejected = new[]
            {
                CreateActionSignal(PlayerEntityId, PlayerActionKind.Push, 1, TickPlayerActionResolutionKind.Blocked),
                CreateActionSignal(PlayerEntityId, PlayerActionKind.Flip, 2, TickPlayerActionResolutionKind.None),
                CreateActionSignal(PlayerEntityId, PlayerActionKind.Push, 3, TickPlayerActionResolutionKind.Success, executed: false),
                CreateActionSignal(PlayerEntityId, PlayerActionKind.Flip, 4, TickPlayerActionResolutionKind.Impact, canceled: true),
                CreateActionSignal(PlayerEntityId, PlayerActionKind.None, 5, TickPlayerActionResolutionKind.Success),
                CreateActionSignal(PlayerEntityId, PlayerActionKind.Push, 0, TickPlayerActionResolutionKind.Success),
                CreateActionSignal(99, PlayerActionKind.Push, 6, TickPlayerActionResolutionKind.Success),
            };

            for (var i = 0; i < rejected.Length; i++)
            {
                Assert.That(state.TryConsume(rejected[i]), Is.False, $"Rejected signal index {i}");
            }

            Assert.That(state.Count, Is.Zero);
            Assert.That(state.VisibleCount, Is.Zero);
            Assert.That(state.IsVisible, Is.False);
        }

        [Test]
        public void EntityAndActionSequence_DeduplicatesRepeatedPresentation()
        {
            var state = CreateState();
            var push = CreateActionSignal(
                PlayerEntityId,
                PlayerActionKind.Push,
                1,
                TickPlayerActionResolutionKind.Success);

            Assert.That(state.TryConsume(push), Is.True);
            Assert.That(state.TryConsume(push), Is.False);
            Assert.That(
                state.TryConsume(CreateActionSignal(
                    PlayerEntityId,
                    PlayerActionKind.Flip,
                    2,
                    TickPlayerActionResolutionKind.Impact)),
                Is.True);
            Assert.That(state.Count, Is.EqualTo(2));
            Assert.That(state.VisibleCount, Is.Zero);
        }

        [Test]
        public void AuthoredTiming_HoldsFadesAndRetriggerRestarts()
        {
            var state = CreateState();
            Assert.That(
                state.TryConsume(CreateActionSignal(
                    PlayerEntityId,
                    PlayerActionKind.Push,
                    1,
                    TickPlayerActionResolutionKind.Success)),
                Is.True);

            Assert.That(state.RevealCount(1), Is.True);

            state.Advance(1f, opaqueDurationSeconds: 1f, fadeDurationSeconds: 0.5f);
            Assert.That(state.Alpha, Is.EqualTo(1f));
            state.Advance(0.25f, opaqueDurationSeconds: 1f, fadeDurationSeconds: 0.5f);
            Assert.That(state.Alpha, Is.EqualTo(0.5f).Within(0.0001f));

            Assert.That(
                state.TryConsume(CreateActionSignal(
                    PlayerEntityId,
                    PlayerActionKind.Flip,
                    2,
                    TickPlayerActionResolutionKind.Impact)),
                Is.True);
            Assert.That(state.RevealCount(2), Is.True);
            Assert.That(state.Alpha, Is.EqualTo(1f));

            state.Advance(1.5f, opaqueDurationSeconds: 1f, fadeDurationSeconds: 0.5f);
            Assert.That(state.IsVisible, Is.False);
            Assert.That(state.Alpha, Is.Zero);
        }

        [Test]
        public void CanonicalDeath_Reset_OtherEntitySignalsDoNot()
        {
            var state = CreateState();
            var action = CreateActionSignal(
                PlayerEntityId,
                PlayerActionKind.Push,
                1,
                TickPlayerActionResolutionKind.Success);
            Assert.That(state.TryConsume(action), Is.True);

            Assert.That(state.TryReset(CreateDeathSignal(99)), Is.False);
            Assert.That(state.Count, Is.EqualTo(1));

            Assert.That(state.TryReset(CreateDeathSignal(PlayerEntityId)), Is.True);
            AssertReset(state);
            Assert.That(state.TryConsume(action), Is.True, "A reset run may reuse an action sequence.");

        }

        [Test]
        public void StageAttemptTracker_DeduplicatesAndResetsAcrossDeath()
        {
            var tracker = new StageAttemptPushFlipTracker();
            var push = CreateActionSignal(
                PlayerEntityId,
                PlayerActionKind.Push,
                1,
                TickPlayerActionResolutionKind.Success);

            tracker.Observe(CreateTick(push), PlayerEntityId);
            tracker.Observe(CreateTick(push), PlayerEntityId);
            Assert.That(tracker.CombinedPushFlipUses, Is.EqualTo(1));

            tracker.Observe(CreateTick(death: CreateDeathSignal(PlayerEntityId)), PlayerEntityId);
            Assert.That(tracker.CombinedPushFlipUses, Is.Zero);

            tracker.Observe(CreateTick(push), PlayerEntityId);
            Assert.That(
                tracker.Snapshot.CombinedPushFlipUses,
                Is.EqualTo(1),
                "A new attempt may reuse an action sequence after death.");
        }

        private static PlayerActionUseCounterState CreateState()
        {
            var state = new PlayerActionUseCounterState();
            state.ConfigurePlayerEntityId(PlayerEntityId);
            return state;
        }

        private static TickPlayerActionPresentationSignal CreateActionSignal(
            int entityId,
            PlayerActionKind actionKind,
            int actionSequence,
            TickPlayerActionResolutionKind resolutionKind,
            bool executed = true,
            bool canceled = false)
        {
            return new TickPlayerActionPresentationSignal(
                entityId,
                actionKind,
                actionSequence,
                startedThisTick: false,
                completedThisTick: true,
                canceledThisTick: canceled,
                executedThisTick: executed,
                isRecoveryPhase: false,
                resolutionKind,
                targetEntityId: 20,
                direction: Direction.Right,
                actionPlanId: actionSequence);
        }

        private static TickPlayerDeathPresentationSignal CreateDeathSignal(int entityId)
        {
            return new TickPlayerDeathPresentationSignal(
                entityId,
                didDieThisTick: true,
                sourceEntityId: 20,
                fallbackFacing: Direction.Right,
                resolvedDamageSourceAvailable: true,
                damageAmountAtFatalHit: 1,
                deathDirectionHintKind: DeathDirectionHintKind.AttackerReverse);
        }

        private static TickResult CreateTick(
            TickPlayerActionPresentationSignal? action = null,
            TickPlayerDeathPresentationSignal? death = null)
        {
            return new TickResult(
                1,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                Array.Empty<EntityState>(),
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                new TickPresentationData(
                    Array.Empty<TickEntityMotion>(),
                    topologyMotion: null,
                    Array.Empty<TickVisibilityChange>(),
                    Array.Empty<TickTransitionVisibilityChange>(),
                    action.HasValue
                        ? new[] { action.Value }
                        : Array.Empty<TickPlayerActionPresentationSignal>(),
                    Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                    Array.Empty<TickPlayerDamagePresentationSignal>(),
                    death.HasValue
                        ? new[] { death.Value }
                        : Array.Empty<TickPlayerDeathPresentationSignal>(),
                    Array.Empty<TickEnemyDamagePresentationSignal>(),
                    Array.Empty<TickEnemyActionPresentationSignal>(),
                    Array.Empty<TickEnemyJumpPresentationSignal>(),
                    Array.Empty<TickEntityExitPresentationSignal>(),
                    Array.Empty<FlipImpactPresentationSignal>()),
                string.Empty,
                TickTrace.Empty);
        }

        private static void AssertReset(PlayerActionUseCounterState state)
        {
            Assert.That(state.Count, Is.Zero);
            Assert.That(state.VisibleCount, Is.Zero);
            Assert.That(state.IsVisible, Is.False);
            Assert.That(state.Alpha, Is.Zero);
        }
    }
}
