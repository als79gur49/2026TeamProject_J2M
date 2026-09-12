using System;
using System.Threading.Tasks;
using Game.Platform.Runtime;
using NUnit.Framework;

namespace Game.Platform.Tests.EditMode
{
    public sealed class PlatformStartupDeferralTests
    {
        [SetUp] public void Reset() { PlatformRuntimeBootstrap.ResetSubsystemStateForTests(new string[0]); }
        [Test] public void CancelPreventsReleaseAndDoesNotSelectRuntime()
        {
            var handle = PlatformStartupDeferral.Request(); handle.Cancel();
            Assert.Throws<InvalidOperationException>(() => handle.Release());
            Assert.IsFalse(PlatformRuntimeRegistry.HasSelection);
        }
        [Test] public void DuplicateRequestAndReleaseAreRejected()
        {
            var handle = PlatformStartupDeferral.Request();
            Assert.Throws<InvalidOperationException>(() => PlatformStartupDeferral.Request());
            handle.Release(); Assert.Throws<InvalidOperationException>(() => handle.Release());
            Assert.AreEqual(PlatformStartupState.ReleaseRequested, handle.State);
            handle.Cancel();
        }
        [Test] public void WorkerCannotRequestOrReleaseMainThreadCapability()
        {
            Assert.IsInstanceOf<InvalidOperationException>(Task.Run(() => { try { PlatformStartupDeferral.Request(); return null; } catch (Exception e) { return e; } }).Result);
            var h = PlatformStartupDeferral.Request();
            Assert.IsInstanceOf<InvalidOperationException>(Task.Run(() => { try { h.Release(); return null; } catch (Exception e) { return e; } }).Result);
            h.Cancel();
        }
        [Test] public void ShutdownReentrantInsideInitializeCleansLateCandidateOnce()
        {
            PlatformRuntimeLifecycle lifecycle = null;
            var runtime = new ReentrantRuntime(() => lifecycle.ShutdownOnce());
            PlatformRuntimeRegistry.RegisterFactory(new FakePlatformRuntimeFactory("reentrant", () => runtime));
            PlatformRuntimeRegistry.Seal();
            lifecycle = new PlatformRuntimeLifecycle(PlatformRuntimeRegistry.Select(PlatformProviderSelectionRequest.Explicit(new PlatformProviderId("reentrant"), "test")));
            lifecycle.InitializeOnce(); lifecycle.ShutdownOnce(); lifecycle.TickOnce();
            Assert.AreEqual(1, runtime.Initializations); Assert.AreEqual(1, runtime.Shutdowns); Assert.AreEqual(0, runtime.Ticks);
        }
        private sealed class ReentrantRuntime : IPlatformRuntime
        {
            private readonly Action during;
            internal int Initializations, Shutdowns, Ticks;
            internal ReentrantRuntime(Action during) { this.during = during; }
            public PlatformProviderId ProviderId => new PlatformProviderId("reentrant");
            public PlatformAvailability Availability => PlatformAvailability.Available;
            public PlatformInitializationResult Initialize() { Initializations++; during(); return PlatformInitializationResult.Success; }
            public void Shutdown() { Shutdowns++; }
            public void Tick() { Ticks++; }
        }
    }
}
