using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Gameplay.UIAccess.Queries;
using Game.Feature.UI.Application;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class HudSelectiveQueryTests
    {
        [Test]
        public void OneHundredUnchangedRefreshes_ReadOnlySessionAfterInitialFiveQueries()
        {
            using var f = new Fixture();
            for (var i = 0; i < 100; i++) f.Refresh();
            Assert.That(f.Session.Reads, Is.EqualTo(101));
            Assert.That(f.Stage.Reads, Is.EqualTo(1));
            Assert.That(f.Objective.Reads, Is.EqualTo(1));
            Assert.That(f.Player.Reads, Is.EqualTo(1));
            Assert.That(f.Surface.Reads, Is.EqualTo(1));
        }

        [Test]
        public void BarrierAndWindowRevisionsOnlyRereadTheirQueries_ExplicitInvalidationRereadsAll()
        {
            using var f = new Fixture();
            f.Objective.Revision++;
            f.Refresh();
            Assert.That(f.Objective.Reads, Is.EqualTo(2));
            Assert.That(f.Player.Reads, Is.EqualTo(1));
            f.Player.Revision++;
            f.Surface.Revision++;
            f.Refresh();
            Assert.That(f.Player.Reads, Is.EqualTo(2));
            Assert.That(f.Surface.Reads, Is.EqualTo(2));
            Assert.That(f.Stage.Reads, Is.EqualTo(1));
            f.Source.InvalidateHudQueries();
            f.Refresh();
            Assert.That(f.Stage.Reads, Is.EqualTo(2));
            Assert.That(f.Objective.Reads, Is.EqualTo(3));
            Assert.That(f.Player.Reads, Is.EqualTo(3));
            Assert.That(f.Surface.Reads, Is.EqualTo(3));
        }

        [Test]
        public void UnsupportedRevisionUsesOriginalRead_AndFailuresNeverBecomeCacheHits()
        {
            using var f = new Fixture();
            f.Stage.Supported = false;
            f.Refresh();
            f.Refresh();
            Assert.That(f.Stage.Reads, Is.EqualTo(3));
            f.Player.Revision++;
            f.Player.Throw = true;
            Assert.Throws<InvalidOperationException>(() => f.Refresh());
            Assert.Throws<InvalidOperationException>(() => f.Refresh());
            Assert.That(f.Player.Reads, Is.EqualTo(3));
            f.Player.Throw = false;
            f.Refresh();
            Assert.That(f.Player.Reads, Is.EqualTo(4));
        }

        [Test]
        public void GenerationChangedAfterProducingResult_IsNotAttachedToOlderResult()
        {
            using var f = new Fixture();
            f.Player.Revision++;
            f.Player.AfterStamp = () => { f.Player.Revision++; f.Player.AfterStamp = null; };
            f.Refresh();
            f.Refresh();
            Assert.That(f.Player.Reads, Is.EqualTo(3));
            f.Refresh();
            Assert.That(f.Player.Reads, Is.EqualTo(3));
        }

        [Test]
        public void CacheHitStillRunsProbe_WhenSessionReturnsPausedOrTerminalValues()
        {
            using var f = new Fixture();
            f.Player.FailProbe = true;
            Assert.Throws<InvalidOperationException>(() => f.Refresh());
            Assert.That(f.Player.Reads, Is.EqualTo(1));
            f.Player.FailProbe = false;
            f.Surface.FailProbe = true;
            Assert.Throws<InvalidOperationException>(() => f.Refresh());
            Assert.That(f.Surface.Reads, Is.EqualTo(1));
        }

        private class RevisionQuery<T> : IGameplayHudRevisionedQuery<T>
        {
            internal T Value;
            internal long Revision;
            internal int Reads;
            internal bool Supported = true, Throw, FailProbe;
            internal Action AfterStamp;
            public T Read()
            {
                Reads++;
                if (Throw) throw new InvalidOperationException("query unavailable");
                return Value;
            }
            public bool TryGetRevision(out GameplayHudQueryStamp stamp)
            {
                if (FailProbe) throw new InvalidOperationException("live read invariant");
                stamp = new GameplayHudQueryStamp(this, generation: Revision);
                return Supported;
            }
            public GameplayHudQueryRead<T> ReadWithRevision()
            {
                var value = Read();
                TryGetRevision(out var stamp);
                AfterStamp?.Invoke();
                return new GameplayHudQueryRead<T>(value, stamp, true);
            }
        }
        private sealed class StageQuery : RevisionQuery<GameplayStageReadModel>, IGameplayStageQuery { }
        private sealed class PlayerQuery : RevisionQuery<GameplayPlayerHudReadModel>, IGameplayPlayerHudQuery { }
        private sealed class ObjectiveQuery : RevisionQuery<GameplayObjectiveReadModel>, IGameplayObjectiveQuery { }
        private sealed class SurfaceQuery : RevisionQuery<IReadOnlyList<GameplaySurfaceButtonRemainderReadModel>>, IGameplaySurfaceButtonRemainderQuery { }
        private sealed class SessionQuery : IGameplaySessionQuery
        {
            internal int Reads;
            public GameplaySessionReadModel Read() { Reads++; return default; }
        }
        private sealed class Fixture : IDisposable
        {
            internal readonly SessionQuery Session = new();
            internal readonly StageQuery Stage = new();
            internal readonly PlayerQuery Player = new();
            internal readonly ObjectiveQuery Objective = new() { Value = GameplayObjectiveReadModel.NoObjective };
            internal readonly SurfaceQuery Surface = new();
            internal readonly GameplayUiPresentationSource Source;
            private bool _blocked;
            internal Fixture()
            {
                Source = new GameplayUiPresentationSource(new GameplayQueryFacade(Session, Stage, Player, Objective, Surface),
                    new FakeGameplayPresentationFeed(), new FakeGameplayPauseService());
            }
            internal void Refresh() { _blocked = !_blocked; Source.UpdateUiGameplayInputBlocked(_blocked); }
            public void Dispose() => Source.Dispose();
        }
    }
}
