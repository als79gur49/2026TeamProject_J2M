using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Steamworks;

namespace Game.Platform.Steam.SteamworksNet.Tests.EditMode
{
    public sealed class SteamworksNetAchievementAdapterTests
    {
        [Test]
        public void ProductionDependencies_ShareOneConcreteAdapterInstance()
        {
            var dependencies = SteamworksNetPlatformRegistration.CreateRuntimeDependencies();

            Assert.That(dependencies.Lifecycle, Is.SameAs(dependencies.Achievements));
            Assert.That(dependencies.Lifecycle, Is.TypeOf<SteamworksNetNativeApi>());
        }

        [Test]
        public void PartialRegistrationFailure_DisposesFirstHandleAndAllowsRetry()
        {
            var statsHandles = new List<FakeHandle>();
            var achievementFactoryCount = 0;
            var adapter = new SteamworksNetNativeApi(
                observer =>
                {
                    var handle = new FakeHandle();
                    statsHandles.Add(handle);
                    return handle;
                },
                observer =>
                {
                    achievementFactoryCount++;
                    if (achievementFactoryCount == 1)
                    {
                        throw new InvalidOperationException("second callback failed");
                    }

                    return new FakeHandle();
                });

            Assert.That(
                () => Register(adapter),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(statsHandles[0].DisposeCount, Is.EqualTo(1));

            Assert.DoesNotThrow(() => Register(adapter));
            adapter.DisposeAchievementStoreCallbacks();

            Assert.That(statsHandles[1].DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateRegistration_IsRejectedAndDisposalIsIdempotentInReverseOrder()
        {
            var order = new List<string>();
            var stats = new FakeHandle("stats", order);
            var achievement = new FakeHandle("achievement", order);
            var adapter = new SteamworksNetNativeApi(
                observer => stats,
                observer => achievement);
            Register(adapter);

            Assert.That(
                () => Register(adapter),
                Throws.TypeOf<InvalidOperationException>());

            adapter.DisposeAchievementStoreCallbacks();
            adapter.DisposeAchievementStoreCallbacks();

            Assert.That(order, Is.EqualTo(new[] { "achievement", "stats" }));
            Assert.That(stats.DisposeCount, Is.EqualTo(1));
            Assert.That(achievement.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateRegistration_PreservesExistingOwnerCallbacks()
        {
            Action<UserStatsStored_t> statsDispatch = null;
            Action<UserAchievementStored_t> achievementDispatch = null;
            var statsHandle = new FakeHandle();
            var achievementHandle = new FakeHandle();
            var ownerStatsCount = 0;
            var ownerAchievementCount = 0;
            var adapter = new SteamworksNetNativeApi(
                observer =>
                {
                    statsDispatch = observer;
                    return statsHandle;
                },
                observer =>
                {
                    achievementDispatch = observer;
                    return achievementHandle;
                });
            adapter.RegisterAchievementStoreCallbacks(
                _ => ownerStatsCount++,
                _ => ownerAchievementCount++);

            Assert.That(
                () => Register(adapter),
                Throws.TypeOf<InvalidOperationException>());

            var gameId = new CGameID(new AppId_t(480)).m_GameID;
            statsDispatch(new UserStatsStored_t
            {
                m_nGameID = gameId,
                m_eResult = EResult.k_EResultOK,
            });
            achievementDispatch(CreateAchievementStored(
                gameId,
                "ACH_WIN_ONE_GAME",
                currentProgress: 0,
                maximumProgress: 0));

            Assert.That(ownerStatsCount, Is.EqualTo(1));
            Assert.That(ownerAchievementCount, Is.EqualTo(1));
            Assert.That(statsHandle.DisposeCount, Is.Zero);
            Assert.That(achievementHandle.DisposeCount, Is.Zero);

            adapter.DisposeAchievementStoreCallbacks();
        }

        [Test]
        public void StoreCallbacks_NormalizeAppIdResultAndBooleanUnlockWithoutIdentityData()
        {
            Action<UserStatsStored_t> statsDispatch = null;
            Action<UserAchievementStored_t> achievementDispatch = null;
            var adapter = new SteamworksNetNativeApi(
                observer =>
                {
                    statsDispatch = observer;
                    return new FakeHandle();
                },
                observer =>
                {
                    achievementDispatch = observer;
                    return new FakeHandle();
                });
            SteamStatsStoredObservation stats = default;
            SteamAchievementStoredObservation achievement = default;
            adapter.RegisterAchievementStoreCallbacks(
                value => stats = value,
                value => achievement = value);

            var gameId = new CGameID(new AppId_t(480)).m_GameID;
            statsDispatch(new UserStatsStored_t
            {
                m_nGameID = gameId,
                m_eResult = EResult.k_EResultOK,
            });
            achievementDispatch(CreateAchievementStored(
                gameId,
                "ACH_WIN_ONE_GAME",
                currentProgress: 0,
                maximumProgress: 0));

            Assert.That(stats.AppId, Is.EqualTo(480));
            Assert.That(stats.Result, Is.EqualTo(SteamCallbackResult.Ok));
            Assert.That(achievement.AppId, Is.EqualTo(480));
            Assert.That(achievement.AchievementName, Is.EqualTo("ACH_WIN_ONE_GAME"));
            Assert.That(achievement.IsFullUnlock, Is.True);
            Assert.That(typeof(SteamStatsStoredObservation).GetProperties(),
                Has.None.Matches<PropertyInfo>(ContainsPrivateIdentity));
            Assert.That(typeof(SteamAchievementStoredObservation).GetProperties(),
                Has.None.Matches<PropertyInfo>(ContainsPrivateIdentity));
        }

        private static void Register(SteamworksNetNativeApi adapter)
        {
            adapter.RegisterAchievementStoreCallbacks(_ => { }, _ => { });
        }

        private static UserAchievementStored_t CreateAchievementStored(
            ulong gameId,
            string name,
            uint currentProgress,
            uint maximumProgress)
        {
            object boxed = new UserAchievementStored_t();
            var nameBytes = typeof(UserAchievementStored_t).GetField(
                "m_rgchAchievementName_",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(nameBytes, Is.Not.Null);
            var buffer = new byte[Constants.k_cchStatNameMax];
            Encoding.UTF8.GetBytes(name, 0, name.Length, buffer, 0);
            nameBytes.SetValue(boxed, buffer);
            var observation = (UserAchievementStored_t)boxed;
            observation.m_nGameID = gameId;
            observation.m_nCurProgress = currentProgress;
            observation.m_nMaxProgress = maximumProgress;
            return observation;
        }

        private static bool ContainsPrivateIdentity(PropertyInfo property)
        {
            return property.Name.IndexOf("SteamId", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   property.Name.IndexOf("Persona", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   property.Name.IndexOf("Account", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private sealed class FakeHandle : IDisposable
        {
            private readonly string name;
            private readonly List<string> order;

            internal FakeHandle(string name = null, List<string> order = null)
            {
                this.name = name;
                this.order = order;
            }

            internal int DisposeCount { get; private set; }

            public void Dispose()
            {
                DisposeCount++;
                if (name != null)
                {
                    order.Add(name);
                }
            }
        }
    }
}
