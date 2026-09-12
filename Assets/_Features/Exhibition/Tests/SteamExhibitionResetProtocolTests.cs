using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Exhibition.Integration;
using Game.Platform.Steam;
using Game.Platform.Steam.ProductAchievements;
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

        // Explicit release contract: do not derive the expected set from Production.
        private static readonly string[] ProductionNames =
        {
            "VQ_LEVEL_0_CLEAR", "VQ_LEVEL_1_CLEAR", "VQ_LEVEL_2_CLEAR",
            "VQ_LEVEL_3_CLEAR", "VQ_LEVEL_4_CLEAR",
            "VQ_STAGE_0_1_EFFICIENT_CLEAR", "VQ_STAGE_0_2_EFFICIENT_CLEAR", "VQ_STAGE_0_3_EFFICIENT_CLEAR",
            "VQ_STAGE_1_1_EFFICIENT_CLEAR", "VQ_STAGE_1_2_EFFICIENT_CLEAR",
            "VQ_STAGE_2_1_EFFICIENT_CLEAR", "VQ_STAGE_2_2_EFFICIENT_CLEAR",
            "VQ_STAGE_3_1_EFFICIENT_CLEAR", "VQ_STAGE_3_2_EFFICIENT_CLEAR", "VQ_STAGE_3_3_EFFICIENT_CLEAR",
            "VQ_STAGE_4_1_EFFICIENT_CLEAR", "VQ_STAGE_4_2_EFFICIENT_CLEAR", "VQ_STAGE_4_3_EFFICIENT_CLEAR",
        };
        // Former production API names from commit 848dd652a; schema presence is not reset authorization.
        private static readonly string[] RetiredNames =
        {
            "VQ_CAMPAIGN_COMPLETE", "VQ_STAGE_1_2_CLEAR", "VQ_STAGE_1_2_PUSH_FLIP_LE_25",
        };

        private List<string> CreateProduction(string failedClear = null)
        {
            var names = SteamAchievementMapping.Production.Entries
                .Select(entry => entry.ExpectedSteamApiName.Value).ToArray();
            Assert.That(names, Is.EquivalentTo(ProductionNames));
            var cleared = new List<string>();
            _protocol = new SteamExhibitionResetProtocol(_api,
                name => { cleared.Add(name); return name != failedClear; }, () => _validate(),
                names, 123, () => _failed++, () => _released++, TimeSpan.FromSeconds(1));
            return cleared;
        }

        [Test]
        public async Task ProductionResetClearsExactlyEighteenMappedAchievementsAndExcludesRetiredSchemaEntries()
        {
            _api.Available = ProductionNames.Concat(RetiredNames).ToArray();
            var cleared = CreateProduction();
            await _protocol.RunAsync();
            Assert.That(cleared, Is.EquivalentTo(ProductionNames));
            Assert.That(cleared.Distinct().Count(), Is.EqualTo(18));
            Assert.That(cleared.Intersect(RetiredNames), Is.Empty);
            Assert.That(_api.Stores, Is.EqualTo(1));
            Assert.That(_api.ReadNames, Is.EquivalentTo(ProductionNames.Concat(ProductionNames)));
            Assert.That(_api.CallbackRegistrations, Is.Zero);
            Assert.That(_failed, Is.Zero); Assert.That(_released, Is.EqualTo(1));
        }

        [Test]
        public void MissingLastProductionSchemaEntryPreventsEveryDestructiveCall()
        {
            _api.Available = ProductionNames.Take(17).Concat(RetiredNames).ToArray();
            var cleared = CreateProduction();
            Assert.ThrowsAsync<InvalidOperationException>(async () => await _protocol.RunAsync());
            Assert.That(cleared, Is.Empty); Assert.That(_api.Stores, Is.Zero);
            AssertFailedRelease();
        }

        [Test]
        public void LastProductionClearFailureDoesNotStorePartialReset()
        {
            _api.Available = ProductionNames;
            var cleared = CreateProduction(ProductionNames[17]);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await _protocol.RunAsync());
            Assert.That(cleared, Is.EqualTo(ProductionNames));
            Assert.That(_api.Stores, Is.Zero);
            Assert.That(_api.ReadNames, Is.EquivalentTo(ProductionNames));
            AssertFailedRelease();
        }

        [Test]
        public void LastProductionReadbackStillAchievedFailsAfterSuccessfulStore()
        {
            _api.Available = ProductionNames;
            _api.ReadbackAchievedName = ProductionNames[17];
            var cleared = CreateProduction();
            Assert.ThrowsAsync<InvalidOperationException>(async () => await _protocol.RunAsync());
            Assert.That(cleared, Is.EquivalentTo(ProductionNames));
            Assert.That(_api.Stores, Is.EqualTo(1));
            Assert.That(_api.ReadNames, Is.EquivalentTo(ProductionNames.Concat(ProductionNames)));
            AssertFailedRelease();
        }

        private sealed class FakeApi : ISteamAchievementApi
        {
            public string[] Available = { "A", "B" };
            public string UnreadableBeforeStore;
            public bool ReadbackSucceeds = true;
            public bool ReadbackAchieved;
            public string ReadbackAchievedName;
            public readonly List<string> ReadNames = new List<string>();
            public bool StoreResult = true;
            public Action StoreAction;
            public int Stores;
            public int Reads;
            public int CallbackRegistrations;
            public uint GetNumAchievements() => (uint)Available.Length;
            public string GetAchievementName(uint index) => Available[index];
            public bool GetAchievement(string name, out bool achieved)
            {
                Reads++; ReadNames.Add(name);
                achieved = Stores == 0 || ReadbackAchieved || name == ReadbackAchievedName;
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
