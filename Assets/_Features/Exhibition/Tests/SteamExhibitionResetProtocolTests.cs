using System;
using System.Threading.Tasks;
using Game.Exhibition.Integration;
using Game.Platform.Steam;
using NUnit.Framework;

namespace Game.Exhibition.Tests
{
    [Category("Full")]
    public sealed class SteamExhibitionResetProtocolTests
    {
        private FakeApi _api;
        private SteamExhibitionResetProtocol _protocol;
        private int _clears;
        private int _failed;
        private int _released;
        private bool _clearResult;
        private Action _validate;

        [SetUp]
        public void SetUp()
        {
            _api = new FakeApi(); _clears = 0; _failed = 0; _released = 0; _clearResult = true;
            _validate = () => { };
            _api.StoreAction = () => Observe(SteamCallbackResult.Ok);
        }

        private void Create(TimeSpan? timeout = null)
        {
            _protocol = new SteamExhibitionResetProtocol(_api,
                name => { _clears++; return _clearResult; }, () => _validate(),
                new[] { "A", "B" }, 123, () => _failed++, () => _released++,
                timeout ?? TimeSpan.FromSeconds(1));
        }
        private void Observe(SteamCallbackResult result, uint appId = 123) =>
            _protocol.ObserveStatsStored(new SteamStatsStoredObservation(appId, result));
        private void AssertFailedRelease()
        {
            Assert.That(_failed, Is.EqualTo(1)); Assert.That(_released, Is.EqualTo(1));
        }

        [Test]
        public async Task SynchronousSuccessCallbackIsObservedAndLeaseReleased()
        {
            Create(); await _protocol.RunAsync();
            Assert.That(_clears, Is.EqualTo(2)); Assert.That(_api.Stores, Is.EqualTo(1));
            Assert.That(_api.Reads, Is.EqualTo(4)); Assert.That(_released, Is.EqualTo(1));
            Assert.That(_failed, Is.Zero);
            Assert.That(_api.CallbackRegistrations, Is.Zero);
        }

        [Test]
        public void MissingLaterSchemaEntryPreventsAllClears()
        {
            _api.Available = new[] { "A" }; Create();
            Assert.ThrowsAsync<InvalidOperationException>(async () => await _protocol.RunAsync());
            Assert.That(_clears, Is.Zero); Assert.That(_api.Stores, Is.Zero); AssertFailedRelease();
        }

        [Test]
        public void UnreadableSchemaPreventsAllClears()
        {
            _api.UnreadableBeforeStore = "B"; Create();
            Assert.ThrowsAsync<InvalidOperationException>(async () => await _protocol.RunAsync());
            Assert.That(_clears, Is.Zero); Assert.That(_api.Stores, Is.Zero); AssertFailedRelease();
        }

        [Test]
        public void ClearFailureDoesNotStore()
        {
            _clearResult = false; Create();
            Assert.ThrowsAsync<InvalidOperationException>(async () => await _protocol.RunAsync());
            Assert.That(_api.Stores, Is.Zero); AssertFailedRelease();
        }

        [Test]
        public void StoreFalseFailsDespiteInlineSuccessCallback()
        {
            _api.StoreResult = false; Create();
            Assert.ThrowsAsync<InvalidOperationException>(async () => await _protocol.RunAsync());
            AssertFailedRelease();
        }

        [Test]
        public void NonOkResultFailsWithoutReadback()
        {
            _api.StoreAction = () => Observe(SteamCallbackResult.Failure); Create();
            Assert.ThrowsAsync<InvalidOperationException>(async () => await _protocol.RunAsync());
            Assert.That(_api.Reads, Is.EqualTo(2)); AssertFailedRelease();
        }

        [Test]
        public void WrongAppAndPreRequestCallbacksCannotCompleteStore()
        {
            _api.StoreAction = () => Observe(SteamCallbackResult.Ok, 456); Create(TimeSpan.Zero);
            Observe(SteamCallbackResult.Ok);
            Assert.ThrowsAsync<TimeoutException>(async () => await _protocol.RunAsync());
            AssertFailedRelease();
        }

        [Test]
        public void TimeoutReleasesAndCannotBeRetriedByLateCallback()
        {
            _api.StoreAction = () => { }; Create(TimeSpan.Zero);
            Assert.ThrowsAsync<TimeoutException>(async () => await _protocol.RunAsync());
            Observe(SteamCallbackResult.Ok);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await _protocol.RunAsync());
            Assert.That(_api.Stores, Is.EqualTo(1)); AssertFailedRelease();
        }

        [TestCase(false)] [TestCase(true)]
        public void ReadbackFailureOrStillAchievedDoesNotReportSuccess(bool achieved)
        {
            _api.ReadbackSucceeds = achieved; _api.ReadbackAchieved = achieved; Create();
            Assert.ThrowsAsync<InvalidOperationException>(async () => await _protocol.RunAsync());
            AssertFailedRelease();
        }

        [Test]
        public async Task FirstStoreCallbackWinsAndOtherAppIsIgnored()
        {
            _api.StoreAction = () =>
            {
                Observe(SteamCallbackResult.Failure, 456);
                Observe(SteamCallbackResult.Ok);
                Observe(SteamCallbackResult.Failure);
            };
            Create(); await _protocol.RunAsync();
            Assert.That(_failed, Is.Zero); Assert.That(_released, Is.EqualTo(1));
        }

        [Test]
        public void IdentityFailureBeforeClearReleasesOwnedCallbacks()
        {
            _validate = () => throw new InvalidOperationException("Wrong account"); Create();
            Assert.ThrowsAsync<InvalidOperationException>(async () => await _protocol.RunAsync());
            Assert.That(_clears, Is.Zero); AssertFailedRelease();
        }

        private sealed class FakeApi : ISteamAchievementApi
        {
            public string[] Available = { "A", "B" };
            public string UnreadableBeforeStore;
            public bool ReadbackSucceeds = true;
            public bool ReadbackAchieved;
            public bool StoreResult = true;
            public Action StoreAction;
            public int Stores;
            public int Reads;
            public int CallbackRegistrations;
            public uint GetNumAchievements() => (uint)Available.Length;
            public string GetAchievementName(uint index) => Available[index];
            public bool GetAchievement(string name, out bool achieved)
            {
                Reads++; achieved = Stores == 0 || ReadbackAchieved;
                return Stores == 0 ? name != UnreadableBeforeStore : ReadbackSucceeds;
            }
            public bool StoreStats() { Stores++; StoreAction?.Invoke(); return StoreResult; }
            public bool SetAchievement(string name) => throw new AssertionException("Reset must never unlock achievements.");
            public void RegisterAchievementStoreCallbacks(Action<SteamStatsStoredObservation> stats,
                Action<SteamAchievementStoredObservation> achievements)
            { CallbackRegistrations++; throw new AssertionException("The adapter owns callback acquisition."); }
            public void DisposeAchievementStoreCallbacks() => throw new AssertionException("Release through the owned lease.");
        }
    }
}
