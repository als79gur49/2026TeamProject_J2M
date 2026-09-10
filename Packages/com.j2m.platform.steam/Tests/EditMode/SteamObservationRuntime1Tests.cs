using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
namespace Game.Platform.Steam.Tests.EditMode
{
    public sealed class SteamObservationRuntime1Tests
    {
        [SetUp] public void Setup() { Reset(); SteamOverlayObservationAccess.BlockNativeStartup(); }
        [TearDown] public void Cleanup() { SteamOverlayObservationAccess.Runtime?.Shutdown(); Reset(); }
        private static void Reset() => typeof(SteamOverlayObservationAccess).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        private static SteamObservationPermitSnapshot Snapshot()
        {
            using (var p = Process.GetCurrentProcess()) return new SteamObservationPermitSnapshot {
                RunId = Guid.NewGuid().ToString("N"), Role = "OriginObserver", BindingHash = new string('a', 64),
                ClientIdentity = "externally-validated-fake", SelfPid = p.Id, SelfStartTicks = p.StartTime.ToUniversalTime().Ticks,
                Generation = 1, IsCurrent = () => true };
        }
        [TestCase(true, false)][TestCase(true, true)][TestCase(false, false)]
        public void GetterSeparatesFalseSuccessFromFailureAndNeverPublishes(bool success, bool earned)
        {
            var s = Snapshot(); SteamOverlayObservationAccess.Admit(SteamObservationPermitIssuer.Issue(s), s);
            var native = new FakeSteamNativeApi();
            var achievements = new FakeSteamAchievementApi { BeforeReadResult = success, BeforeUnlocked = earned };
            var runtime = new SteamPlatformRuntime(new SteamRuntimeDependencies(native, achievements), false, false, null, null);
            Assert.IsTrue(runtime.Initialize().IsSuccess);
            var read = runtime.ReadAchievement("fake-target"); runtime.Tick(); runtime.Shutdown(); runtime.Shutdown();
            Assert.AreEqual(success, read.ReadSucceeded); Assert.AreEqual(success ? (bool?)earned : null, read.Achieved);
            Assert.AreEqual(1, native.InitializeCount); Assert.AreEqual(1, native.ShutdownCount);
            Assert.AreEqual(0, achievements.SetAchievementCount); Assert.AreEqual(0, achievements.StoreStatsCount);
            Assert.AreEqual(0, achievements.RegistrationCount); Assert.AreEqual(0, achievements.GetNumAchievementsCount);
        }
        [Test]
        public void BindingMutationWorkerAndReuseCannotAdmitAnotherOwner()
        {
            var s = Snapshot(); var permit = SteamObservationPermitIssuer.Issue(s);
            s.Generation++; Assert.Throws<InvalidOperationException>(() => SteamOverlayObservationAccess.Admit(permit, s)); s.Generation--;
            var error = Task.Run(() => { try { SteamObservationPermitIssuer.Issue(s); return null; } catch (Exception e) { return e; } }).Result;
            Assert.IsInstanceOf<InvalidOperationException>(error);
            SteamOverlayObservationAccess.Admit(permit, s); permit.Consume();
            Assert.Throws<InvalidOperationException>(() => permit.Consume());
        }
        [Test]
        public void WorkerTickAndShutdownCannotCallNativeOrConsumeCleanup()
        {
            var s = Snapshot(); SteamOverlayObservationAccess.Admit(SteamObservationPermitIssuer.Issue(s), s);
            var native = new FakeSteamNativeApi(); var runtime = new SteamPlatformRuntime(native); Assert.IsTrue(runtime.Initialize().IsSuccess);
            foreach (Action action in new Action[] { runtime.Tick, runtime.Shutdown })
            {
                var error = Task.Run(() => { try { action(); return null; } catch (Exception e) { return e; } }).Result;
                Assert.IsInstanceOf<InvalidOperationException>(error);
            }
            Assert.AreEqual(0, native.CallbackCount); Assert.AreEqual(0, native.ShutdownCount);
            runtime.Tick(); runtime.Shutdown(); Assert.AreEqual(1, native.CallbackCount); Assert.AreEqual(1, native.ShutdownCount);
        }
        [TestCase(false)][TestCase(true)]
        public void ShutdownDiagnosticsDistinguishNativeReturnFromSwallowedFailure(bool fail)
        {
            var snapshot = Snapshot(); SteamOverlayObservationAccess.Admit(SteamObservationPermitIssuer.Issue(snapshot), snapshot);
            var native = new FakeSteamNativeApi { ShutdownException = fail ? new InvalidOperationException("shutdown") : null };
            var runtime = new SteamPlatformRuntime(native); Assert.IsTrue(runtime.Initialize().IsSuccess);
            runtime.Shutdown(); runtime.Shutdown();
            Assert.AreEqual(1, native.ShutdownCount); Assert.AreEqual(!fail, runtime.Diagnostics.ShutdownReturned);
            Assert.AreEqual(fail ? SteamPlatformFailureReason.ShutdownException : SteamPlatformFailureReason.None, runtime.Diagnostics.LastFailureReason);
        }
        [Test]
        public void CallbackFailureIsVisibleToObservationHealthBeforeAnotherReport()
        {
            var snapshot = Snapshot(); SteamOverlayObservationAccess.Admit(SteamObservationPermitIssuer.Issue(snapshot), snapshot);
            var native = new FakeSteamNativeApi { CallbackException = new InvalidOperationException("callbacks") };
            var runtime = new SteamPlatformRuntime(native); Assert.IsTrue(runtime.Initialize().IsSuccess);
            runtime.Tick(); Assert.IsNotNull(SteamOverlayObservationAccess.NativeFailure);
            Assert.AreEqual(SteamPlatformFailureReason.CallbackException, runtime.Diagnostics.LastFailureReason);
            runtime.Shutdown(); runtime.Shutdown(); Assert.AreEqual(1, native.ShutdownCount);
        }
        [Test]
        public void RevokedPermitNeverStartsNative()
        {
            var s = Snapshot(); var permit = SteamObservationPermitIssuer.Issue(s); SteamOverlayObservationAccess.Admit(permit, s); permit.Revoke();
            var native = new FakeSteamNativeApi(); var runtime = new SteamPlatformRuntime(native);
            Assert.Throws<InvalidOperationException>(() => runtime.Initialize()); Assert.AreEqual(0, native.InitializeCount);
        }
    }
}
