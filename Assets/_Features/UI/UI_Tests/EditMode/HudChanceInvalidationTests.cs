using System;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Gameplay.UIAccess.Queries;
using Game.Feature.UI.Application;
using Game.Feature.UI.HUD;
using Game.Feature.Stages;
using Game.Feature.Gameplay.UIAccess.Presentation;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class HudChanceInvalidationTests
    {
        [Test]
        public void SameDisplayRevision_IsConsumedWithoutRefreshOrSnapshotNotification()
        {
            var player = new ChanceQuery();
            using var fixture = new Fixture(player);
            player.Revision++;
            var before = fixture.Source.CurrentSnapshot;
            fixture.Source.FlushPendingChanceChanges();
            fixture.Source.FlushPendingChanceChanges();
            Assert.That(player.Reads, Is.EqualTo(2));
            Assert.That(fixture.Notifications, Is.Zero);
            Assert.That(fixture.Source.CurrentSnapshot, Is.EqualTo(before));
        }

        [Test]
        public void PausedChangedDisplayRefreshesOnce_AndReusesTheValidatedChance()
        {
            var player = new ChanceQuery();
            using var fixture = new Fixture(player);
            fixture.Pause.Pause();
            var reads = player.Reads;
            var notifications = fixture.Notifications;
            player.Value = new GameplayPlayerHudReadModel(true, 1, 3);
            player.Revision++;
            fixture.Source.FlushPendingChanceChanges();
            fixture.Source.FlushPendingChanceChanges();
            Assert.That(player.Reads, Is.EqualTo(reads + 1));
            Assert.That(fixture.Notifications, Is.EqualTo(notifications + 1));
            Assert.That(fixture.Source.CurrentSnapshot.Chance.RemainingChances, Is.EqualTo(1));
        }

        [Test]
        public void PolicyOnlyChangeIsVisible_ExistingRefreshConsumesPending()
        {
            var player = new ChanceQuery();
            using var fixture = new Fixture(player);
            player.Value = new GameplayPlayerHudReadModel(true, 2, 3, GameplayChanceAudioPolicy.SuppressChanceChangeCue);
            player.Revision++;
            fixture.Source.UpdateUiGameplayInputBlocked(true);
            var reads = player.Reads;
            fixture.Source.FlushPendingChanceChanges();
            Assert.That(player.Reads, Is.EqualTo(reads));
            Assert.That(fixture.Source.CurrentSnapshot.Chance.AudioPolicy,
                Is.EqualTo(GameplayChanceAudioPolicy.SuppressChanceChangeCue));
        }

        [Test]
        public void FailedAutomaticRevisionIsAttemptedOnce_ExplicitRefreshStillThrows()
        {
            var player = new ChanceQuery();
            using var fixture = new Fixture(player);
            player.Throw = true;
            player.Revision++;
            Assert.Throws<InvalidOperationException>(() => fixture.Source.FlushPendingChanceChanges());
            var reads = player.Reads;
            fixture.Source.FlushPendingChanceChanges();
            Assert.That(player.Reads, Is.EqualTo(reads));
            Assert.Throws<InvalidOperationException>(() => fixture.Source.UpdateUiGameplayInputBlocked(true));
            player.Revision++;
            Assert.Throws<InvalidOperationException>(() => fixture.Source.FlushPendingChanceChanges());
        }

        [Test]
        public void NewRevisionDuringReadRemainsPending_ScopeAndDisposePreventAutomaticReads()
        {
            var player = new ChanceQuery();
            using var fixture = new Fixture(player);
            player.Revision++;
            player.AfterRead = () => { player.Revision++; player.AfterRead = null; };
            fixture.Source.FlushPendingChanceChanges();
            var reads = player.Reads;
            player.IsChanceDisplayUpdating = true;
            fixture.Source.FlushPendingChanceChanges();
            Assert.That(player.Reads, Is.EqualTo(reads));
            player.IsChanceDisplayUpdating = false;
            fixture.Source.FlushPendingChanceChanges();
            Assert.That(player.Reads, Is.EqualTo(reads + 1));
            fixture.Source.Dispose();
            player.Revision++;
            fixture.Source.FlushPendingChanceChanges();
            Assert.That(player.Reads, Is.EqualTo(reads + 1));
        }

        [Test]
        public void SameChancePreservesObjectivePulseAndSurfaceSequence_ChangedChanceAllowsRefreshEffects()
        {
            var player = new ChanceQuery();
            using var f = new Fixture(player);
            var objective = new ObjectiveHudPresenter();
            using var root = new HUDRootPresenter(f.Source, new StageInfoPresenter(), objective,
                new ChancePanelPresenter(), new SurfaceBeltIndicatorPresenter());
            f.Defaults.SetStage(new GameplayStageReadModel(StageId.CreateOrThrow("stage-1-1"), "stage.stage-1-1.display_name"));
            f.Defaults.SetObjective(Objective(false));
            var floor = new GameplayUiTopology(GameplayUiFace.Floor);
            f.Feed.PublishState(new GameplayPresentationState(floor, true, false, true));
            f.Defaults.SetObjective(Objective(true));
            f.Feed.PublishFrame(new GameplayPresentationFrame(7, floor,
                new GameplayTopologyPresentationSlice(floor, new GameplayUiTopology(GameplayUiFace.Front), GameplayUiRotationKind.Forward)));
            Assert.That(objective.ViewModel.Rows[0].JustSatisfied, Is.True);
            var sequence = f.Source.CurrentSnapshot.SurfaceBelt.TransitionSequenceId;
            Assert.That(sequence, Is.GreaterThan(0));
            var notifications = f.Notifications;
            player.Revision++;
            f.Source.FlushPendingChanceChanges();
            Assert.That(f.Notifications, Is.EqualTo(notifications));
            Assert.That(objective.ViewModel.Rows[0].JustSatisfied, Is.True);
            Assert.That(f.Source.CurrentSnapshot.SurfaceBelt.TransitionSequenceId, Is.EqualTo(sequence));
            player.Value = new GameplayPlayerHudReadModel(true, 1, 3);
            player.Revision++;
            f.Source.FlushPendingChanceChanges();
            Assert.That(f.Notifications, Is.EqualTo(notifications + 1));
            Assert.That(objective.ViewModel.Rows[0].JustSatisfied, Is.False);
            Assert.That(f.Source.CurrentSnapshot.SurfaceBelt.TransitionSequenceId, Is.Zero);
            Assert.That(f.Source.CurrentSnapshot.Tick.LastReducedTickIndex, Is.EqualTo(7));
        }

        private static GameplayObjectiveReadModel Objective(bool satisfied) => new(
            true, satisfied, satisfied, false, new[]
            {
                new GameplayObjectiveConditionReadModel("primary-goal", GameplayObjectivePresentationKind.ReachExit,
                    "reach-exit|role-1", GameplayObjectiveConditionRole.PrimaryGoal, true, satisfied, satisfied ? 1 : 0, 1, 0),
            });

        private sealed class ChanceQuery : IGameplayPlayerHudQuery, IGameplayHudChanceChanges
        {
            internal long Revision;
            internal int Reads;
            internal bool Throw;
            internal Action AfterRead;
            internal GameplayPlayerHudReadModel Value = new(true, 2, 3);
            public bool IsChanceDisplayUpdating { get; set; }
            public bool TryGetChanceRevision(out long revision) { revision = Revision; return true; }
            public GameplayPlayerHudReadModel Read() => ReadChance(out _);
            public GameplayPlayerHudReadModel ReadChance(out long revision)
            {
                revision = Revision;
                Reads++;
                if (Throw) throw new InvalidOperationException("save unavailable");
                var value = Value;
                AfterRead?.Invoke();
                return value;
            }
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly FakeGameplayPauseService Pause = new();
            internal readonly GameplayUiPresentationSource Source;
            internal int Notifications;
            internal readonly FakeGameplayPresentationFeed Feed = new();
            internal readonly FakeGameplayQueryFacade Defaults = new(default, default, GameplayObjectiveReadModel.NoObjective);
            internal Fixture(ChanceQuery player)
            {
                var defaults = Defaults;
                var query = new GameplayQueryFacade(defaults.Session, defaults.Stage, player,
                    defaults.Objectives, defaults.SurfaceButtonRemainders);
                Source = new GameplayUiPresentationSource(query, Feed, Pause);
                Source.SnapshotChanged += _ => Notifications++;
            }
            public void Dispose() => Source.Dispose();
        }
    }
}
