using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Game.Exhibition.Integration;
using Game.Exhibition.RestartExperiment;
using Game.Platform.Runtime;
using Game.Platform.Steam;
using NUnit.Framework;
using UnityEngine;

namespace Game.Exhibition.Tests
{
    // Real MonoBehaviour, local storage, output queue, health gate and canonical ShutdownOnce.
    // Only native API / files / process / pipe / quit are harmless substitutes.
    public sealed class ObservationV3RuntimeAdapterTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        ObservationV3Runtime runtime;
        ObservationV3Session session;
        FakeIO io;
        Native native;
        SteamPlatformRuntime steam;
        PlatformStartupHandle startup;
        ValidatedObservationNativePermit permit;
        GameObject host;
        SynchronizationContext previous;
        readonly string run = Guid.NewGuid().ToString("N");
        static void Set(object value, string name, object field) => value.GetType().GetField(name, Private).SetValue(value, field);
        static T Get<T>(object value, string name) => (T)value.GetType().GetField(name, Private).GetValue(value);
        object Call(string name, params object[] args) => typeof(ObservationV3Runtime).GetMethod(name, Private).Invoke(runtime, args);
        static void ResetStatic(Type type, string method) => type.GetMethod(method, Static).Invoke(null, null);
        [SetUp] public void Setup()
        {
            previous = SynchronizationContext.Current; SynchronizationContext.SetSynchronizationContext(null);
            ResetStatic(typeof(SteamOverlayObservationAccess), "Reset");
            ResetStatic(typeof(PlatformStartupDeferral), "Reset");
            var hostType = typeof(PlatformStartupDeferral).Assembly.GetType("Game.Platform.Runtime.PlatformRuntimeApplicationHost");
            ResetStatic(hostType, "ResetStaticOwnerForSubsystemRegistration");
            ResetStatic(typeof(PlatformRuntimeRegistry), "ResetForSubsystemRegistration");
            runtime = new GameObject("runtime adapter fixture").AddComponent<ObservationV3Runtime>();
            // EditMode does not promise Awake for a newly attached MonoBehaviour.
            if (Get<ObservationV3Session>(runtime, "session") == null) Call("Awake");
            io = new FakeIO(this); runtime.IO = io; session = Get<ObservationV3Session>(runtime, "session");
            startup = PlatformStartupDeferral.Request(); Set(runtime, "startup", startup);
            Set(runtime, "options", new ObservationV3Options { Role = OverlayObservationRole.ReplacementObserver, Owner = ObservationLaunchOwner.SteamDelegated });
            Set(runtime, "self", Identity(7)); Set(runtime, "client", Identity(9)); Set(runtime, "helper", Identity(8));
            var config = Document<ObservationV3OriginConfig>("origin-config"); config.AppId = 5218360; config.ExpectedSteamId = 76561198000000000UL; config.Targets = new[] { "fake-target" };
            var baseline = Document<ObservationV3Baseline>("baseline"); baseline.Targets = new[] { new ObservationV3AchievementValue { Target = "fake-target", ReadSucceeded = true, Achieved = false } };
            var prep = Document<ObservationV3OriginPreparation>("origin-preparation"); prep.Self = Identity(7); prep.OriginalSteam = Identity(9); prep.Targets = config.Targets;
            Set(runtime, "bundle", new ObservationV3Bundle { Root = "fake-root", Config = Pin(config), Preparation = Pin(prep),
                Plan = Pin(Document<ObservationV3LaunchPlan>("launch-plan")), Baseline = Pin(baseline) });
            Set(runtime, "context", Pin(Document<ObservationV3IndependentContext>("independent-context")));
            Set(runtime, "replacementClaim", Pin(Document<ObservationV3ReplacementClaim>("replacement-claim")));
            Set(runtime, "nextIdentity", long.MaxValue); // Explicit health/report/close checks still execute.
            native = new Native();
        }
        [TearDown] public void Teardown()
        {
            io?.ReleaseAll();
            if (runtime != null) { runtime.IO = io; UnityEngine.Object.DestroyImmediate(runtime.gameObject); }
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
            steam?.Shutdown();
            ResetStatic(typeof(SteamOverlayObservationAccess), "Reset"); ResetStatic(typeof(PlatformStartupDeferral), "Reset");
            ResetStatic(typeof(PlatformRuntimeRegistry), "ResetForSubsystemRegistration");
            SynchronizationContext.SetSynchronizationContext(previous);
        }
        void Ready(bool origin = false)
        {
            SteamOverlayObservationAccess.BlockNativeStartup();
            using (var p = Process.GetCurrentProcess())
            {
                var snapshot = new SteamObservationPermitSnapshot { RunId = run, Role = "ReplacementObserver", BindingHash = new string('a', 64),
                    ClientIdentity = "fake", SelfPid = p.Id, SelfStartTicks = p.StartTime.ToUniversalTime().Ticks,
                    Generation = session.Admission.Generation, IsCurrent = () => !session.Admission.Closed };
                permit = SteamObservationPermitIssuer.Issue(snapshot); SteamOverlayObservationAccess.Admit(permit, snapshot);
            }
            steam = new SteamPlatformRuntime(native);
            var selection = typeof(PlatformRuntimeSelectionResult).GetMethod("Success", Static).Invoke(null, new object[] { SteamPlatformRuntime.ProviderId, steam });
            var hostType = typeof(PlatformStartupDeferral).Assembly.GetType("Game.Platform.Runtime.PlatformRuntimeApplicationHost");
            var owner = (Component)hostType.GetMethod("CreateOrGet", Static).Invoke(null, new[] { selection });
            host = owner.gameObject;
            if (native.Initializations == 0)
            {
                hostType.GetMethod("Awake", Private).Invoke(owner, null);
                hostType.GetMethod("ConfigureAndInitialize", Private).Invoke(owner, new[] { selection });
            }
            Set(startup, "host", owner);
            Assert.AreEqual(1, native.Initializations); session.Ready(); session.Observing();
            Set(runtime, "prepared", true); Set(runtime, "nextPins", long.MaxValue);
            if (origin)
            {
                Get<ObservationV3Options>(runtime, "options").Role = OverlayObservationRole.OriginObserver;
                Set(runtime, "visibilityRef", Reference("visibility")); Set(runtime, "displayRef", Reference("display"));
                Set(runtime, "display", new ObservationV3Display { Targets = new[] { new ObservationV3TargetDisplay { Id = "fake-target", Display = "unearned" } } });
            }
        }
        void PumpUntil(Func<bool> done, string blocked = null)
        {
            var limit = Stopwatch.StartNew();
            while (!done() && limit.ElapsedMilliseconds < 5000)
            {
                Call("Update"); io.RunOne(blocked); Thread.Sleep(1);
            }
            Assert.IsTrue(done(), "Runtime did not reach expected boundary; queued=" + string.Join(",", io.Work.Select(w => w.Item1)) + "; error=" + session.FirstError);
        }
        Task Report() => (Task)Call("SubmitReport", "visibility-report", Document<ObservationRunDocument>("visibility-report"));
        void AssertClosed(bool failed)
        {
            PumpUntil(() => io.Quits == 1);
            Assert.AreEqual(failed, session.FirstError != null);
            Assert.AreEqual(0, session.PendingCount); Assert.AreEqual(1, native.Shutdowns);
            Assert.IsTrue(Get<bool>(permit, "revoked"));
            Assert.IsTrue((bool)Call("WantsToQuit")); Assert.AreEqual(1, io.Quits);
        }
        void ReplacementWindow()
        {
            var value = Document<ObservationV3IndependentContext>("independent-context");
            value.PreparedTimestamp = 0; value.DeadlineTimestamp = 30000; value.TimestampFrequency = 1000;
            Set(runtime, "context", Pin(value));
            using (var p = Process.GetCurrentProcess())
            {
                var self = Identity(p.Id); self.StartTicks = p.StartTime.ToUniversalTime().Ticks; Set(runtime, "self", self);
            }
        }
        [Test] public void IndependentReplacementInitializesAndClosesWithoutHelperOrPipe()
        {
            ReplacementWindow(); SteamOverlayObservationAccess.BlockNativeStartup();
            var start = (Task)Call("StartReplacement");
            PumpUntil(() => startup.State == PlatformStartupState.ReleaseRequested);
            // Substitute only canonical host composition; the real Runtime issued the permit.
            steam = new SteamPlatformRuntime(native);
            var selection = typeof(PlatformRuntimeSelectionResult).GetMethod("Success", Static).Invoke(null, new object[] { SteamPlatformRuntime.ProviderId, steam });
            var hostType = typeof(PlatformStartupDeferral).Assembly.GetType("Game.Platform.Runtime.PlatformRuntimeApplicationHost");
            var owner = (Component)hostType.GetMethod("CreateOrGet", Static).Invoke(null, new[] { selection }); host = owner.gameObject;
            if (native.Initializations == 0)
            {
                hostType.GetMethod("Awake", Private).Invoke(owner, null);
                hostType.GetMethod("ConfigureAndInitialize", Private).Invoke(owner, new[] { selection });
            }
            Set(startup, "host", owner);
            typeof(PlatformStartupHandle).GetProperty("State").SetValue(startup, PlatformStartupState.Running);
            permit = (ValidatedObservationNativePermit)typeof(SteamOverlayObservationAccess).GetField("permit", Static).GetValue(null);
            PumpUntil(() => start.IsCompleted); start.GetAwaiter().GetResult();
            Assert.AreEqual(1, native.Initializations); Assert.IsTrue(io.HasFile("replacement-started.json"));
            Assert.AreEqual(0, io.Starts); Assert.AreEqual(0, io.Sent.Count);
            var close = runtime.Close(); PumpUntil(() => close.IsCompleted); AssertClosed(false);
        }
        [Test] public void ReplacementDeadlineBeforeQueuedPermitPreventsNativeInitialization()
        {
            ReplacementWindow(); var start = (Task)Call("StartReplacement");
            io.Now = 30000; session.CheckDeadlines();
            PumpUntil(() => start.IsCompleted);
            Assert.IsFalse(start.Status == TaskStatus.RanToCompletion);
            Assert.AreEqual(0, native.Initializations); Assert.IsFalse(io.HasFile("replacement-started.json"));
        }
        [Test] public void PinnedSteamChangeDuringReportValidationFailsLocalReport()
        {
            Ready(); var report = Report();
            PumpUntil(() => io.Work.Any(w => w.Item1 == "report-file"), "report-file");
            io.ObservationError = new IOException("CurrentSteamChanged");
            PumpUntil(() => report.IsCompleted); AssertClosed(true);
            Assert.AreEqual(0, session.Checkpoint.Sequence);
        }
        [Test] public void OriginAndReplacementReportsUseDistinctCreateOnlyNames()
        {
            Ready(); io.files.Add("OriginObserver-report-1.json", new byte[] { 1 });
            var report = Report(); PumpUntil(() => report.IsCompleted); report.GetAwaiter().GetResult();
            Assert.IsTrue(io.HasFile("ReplacementObserver-report-1.json"));
            CollectionAssert.AreEqual(new byte[] { 1 }, io.files["OriginObserver-report-1.json"]);
        }
        [Test] public void PreparingCloseCancelsQueuedNativeAdmissionWithoutFailingFlow()
        {
            Get<ObservationV3Options>(runtime, "options").Role = OverlayObservationRole.OriginObserver;
            var flow = new ObservationV3Flow(runtime, OverlayObservationRole.OriginObserver); Set(runtime, "flow", flow);
            var prepare = flow.PrepareMenuAsync();
            // Advance only workers: native admission is now queued on the real Runtime main queue.
            io.RunOne(); io.RunOne();
            var close = runtime.Close(); Assert.AreSame(close, runtime.Close());
            PumpUntil(() => prepare.IsCompleted && close.IsCompleted);
            Assert.IsNull(session.FirstError); Assert.IsNull(flow.Error); Assert.IsFalse(flow.CanReport);
            Assert.AreEqual(0, native.Initializations); Assert.AreEqual(0, io.Failures); Assert.AreEqual(1, io.Quits);
        }
        [TestCase("save:request.json")][TestCase("request-read")][TestCase("request-validate")]
        [TestCase("helper-create")][TestCase("helper-identity")][TestCase("bootstrap-ready")]
        [TestCase("helper-retained")][TestCase("grant-confirmation")]
        public void CloseAtActualHandoffBoundaryNeverConfirmsOrRecordsCancellationAsFailure(string boundary)
        {
            Ready(true); var handoff = runtime.FullCycle();
            PumpUntil(() => io.Work.Any(w => w.Item1 == boundary), boundary);
            var close = runtime.Close(); Assert.IsFalse((bool)Call("WantsToQuit"));
            // EOF/handle failure is produced by the fake OS object after its input was closed.
            io.Child.InputClosed = true;
            PumpUntil(() => close.IsCompleted && handoff.IsCompleted);
            Assert.IsNull(session.FirstError); Assert.IsFalse(handoff.IsFaulted);
            Assert.AreEqual(0, io.Child.Confirmations); Assert.AreEqual(0, io.Failures);
            Assert.AreEqual(boundary == "helper-create" || boundary.StartsWith("request") || boundary == "save:request.json" ? 0 : 1, io.Starts);
            AssertClosed(false);
        }
        [Test] public void SuccessfulRetainedQueryReturningAfterCloseCannotQueueNativeHealth()
        {
            Ready(true); io.Child.ReturnRetainedAfterClose = true;
            var handoff = runtime.FullCycle(); PumpUntil(() => io.Work.Any(w => w.Item1 == "helper-retained"), "helper-retained");
            var close = runtime.Close(); PumpUntil(() => close.IsCompleted && handoff.IsCompleted);
            AssertClosed(false); Assert.AreEqual(0, io.Child.Confirmations); Assert.AreEqual(0, io.Failures);
        }
        [Test] public void HandoffIoErrorRemainsFirstWhenHandleCleanupAlsoFails()
        {
            Ready(true); io.Child.InputClosed = true;
            io.Child.OnDispose = () => { throw new IOException("secondary-dispose"); };
            var handoff = runtime.FullCycle(); PumpUntil(() => handoff.IsCompleted); AssertClosed(true);
            Assert.AreEqual("ClosedInput", session.FirstError.Message); Assert.AreEqual(1, io.Failures);
        }
        [Test] public void HostOwnerUnwindsDespiteBlockedFailureStorageAndPreservesOriginalError()
        {
            using (var release = new ManualResetEventSlim())
            {
                var clock = Stopwatch.StartNew(); bool released = false;
                var original = new IOException("cycle-timeout");
                var evidence = new ObservationV3HostFailureEvidence(e => release.Wait(), () => clock.ElapsedMilliseconds, 10);
                try
                {
                    var caught = Assert.Throws<IOException>(() => evidence.RunOwned<int>(() => { throw original; }, () => released = true));
                    Assert.AreSame(original, caught); Assert.IsTrue(released);
                    Assert.Less(clock.ElapsedMilliseconds, 1000);
                }
                finally { release.Set(); }
            }
        }
        [Test] public void CompletedTerminalReadAlsoHasABoundedOwnerWait()
        {
            using (var release = new ManualResetEventSlim())
            {
                try { Assert.Throws<TimeoutException>(() => ObservationV3HostFailureEvidence.ReadCompletion(() => { release.Wait(); return true; }, 10)); }
                finally { release.Set(); }
            }
        }
        [Test] public void CloseAfterGrantCommitJoinsApprovedHandoff()
        {
            Ready(true); io.Child.OnConfirmation = () => { var first = runtime.Close(); Assert.AreSame(first, runtime.Close()); };
            var handoff = runtime.FullCycle(); PumpUntil(() => handoff.IsCompleted);
            Assert.IsFalse(handoff.IsFaulted); Assert.AreEqual(1, io.Starts); Assert.AreEqual(1, io.Child.Confirmations); AssertClosed(false);
        }
        [TestCase("callback")][TestCase("account")][TestCase("logged-off")][TestCase("getter")]
        public void NativeFaultBeforeReportReservationRunsRealHealthAndCanonicalCleanup(string fault)
        {
            Ready(); var report = Report();
            if (fault == "callback") { native.CallbackFault = true; steam.Tick(); }
            if (fault == "account") native.Account++;
            if (fault == "logged-off") native.LoggedOn = false;
            if (fault == "getter") native.GetterFault = true;
            AssertClosed(true); PumpUntil(() => report.IsCompleted);
            Assert.IsFalse(report.Status == TaskStatus.RanToCompletion);
            Assert.AreEqual(0, io.Sent.Count); Assert.AreEqual(1, io.Failures);
        }
        [TestCase(29000, true)][TestCase(30000, false)][TestCase(31000, false)]
        public void LocalReportSaveUsesSubmissionDeadline(long time, bool success)
        {
            Ready(); var report = Report(); PumpUntil(() => io.Work.Any(w => w.Item1 == "report-save"), "report-save");
            io.Now = time; session.CheckDeadlines();
            PumpUntil(() => report.IsCompleted);
            Assert.AreEqual(success, report.Status == TaskStatus.RanToCompletion);
            Assert.AreEqual(success ? 1 : 0, session.Checkpoint.Sequence);
            var close = runtime.Close(); PumpUntil(() => close.IsCompleted); AssertClosed(!success);
            Assert.AreEqual(success, io.HasFile("ReplacementObserver-native-shutdown.json"));
        }
        [Test] public void LocalFileReturningAfterDeadlineCannotResumeCheckpointOrNormalExit()
        {
            Ready(); var report = Report(); PumpUntil(() => io.Work.Any(w => w.Item1 == "report-save"), "report-save");
            io.Now = 29000; PumpUntil(() => io.Work.Any(w => w.Item1 == "report-file"), "report-file");
            io.Now = 31000; session.CheckDeadlines(); var close = runtime.Close();
            PumpUntil(() => close.IsCompleted && report.IsCompleted); AssertClosed(true);
            Assert.AreEqual(0, session.Checkpoint.Sequence); Assert.AreEqual(0, io.Count("exit-request") + io.Count("exit-completed"));
        }
        [Test] public void OutputQueueWaitConsumesReportBudget()
        {
            Ready(); var output = Get<SemaphoreSlim>(runtime, "output"); output.Wait();
            var report = Report(); PumpUntil(() => session.PendingCount == 1);
            io.Now = 30000; session.CheckDeadlines(); output.Release(); AssertClosed(true);
            Assert.IsTrue(report.IsCompleted); Assert.AreEqual(0, io.Sent.Count);
        }
        [TestCase(false)][TestCase(true)] public void ButtonAndOsQuitJoinPendingSaveAndQuitReentry(bool pending)
        {
            Ready(); Task report = null;
            if (pending) { report = Report(); PumpUntil(() => io.Work.Any(w => w.Item1 == "report-save"), "report-save"); }
            var close = runtime.Close(); Assert.AreSame(close, runtime.Close()); Assert.IsFalse((bool)Call("WantsToQuit"));
            Assert.AreEqual(0, io.Sent.Count);
            if (pending) { io.Now = 29000; }
            PumpUntil(() => close.IsCompleted); AssertClosed(false);
            Assert.IsTrue(report == null || report.Status == TaskStatus.RanToCompletion);
            Assert.IsTrue(io.HasFile("ReplacementObserver-native-shutdown.json")); Assert.AreEqual(0, io.Sent.Count);
            Assert.AreEqual(30000, session.CloseDeadline);
        }
        [TestCase(false)][TestCase(true)] public void CleanupFaultOrLateNativeReturnNeverEmitsCompleted(bool late)
        {
            Ready(); native.ShutdownAction = () => { if (late) { io.Now = 31000; session.CheckDeadlines(); } else throw new IOException("shutdown-fault"); };
            var close = runtime.Close(); PumpUntil(() => close.IsCompleted); AssertClosed(true);
            Assert.IsFalse(io.HasFile("ReplacementObserver-native-shutdown.json")); Assert.AreEqual(0, io.Sent.Count);
        }
        [Test] public void OriginReportsAndClosesWithoutConsultingUnavailableHandoffInputs()
        {
            Ready(true); io.HandoffError = new IOException("ProcessStartMonitorUnavailable");
            var report = Report(); PumpUntil(() => report.IsCompleted);
            Assert.AreEqual(TaskStatus.RanToCompletion, report.Status);
            Assert.AreEqual(0, io.HandoffChecks); Assert.IsNull(session.FirstError);
            var close = runtime.Close(); PumpUntil(() => close.IsCompleted);
            Assert.AreEqual(0, io.HandoffChecks); Assert.AreEqual(0, io.Starts); AssertClosed(false);
        }
        [Test] public void EarnedBaselineAllowsLocalReportsButCannotStartHandoff()
        {
            Ready(true);
            var bundle = Get<ObservationV3Bundle>(runtime, "bundle");
            var baseline = bundle.Baseline.Value; baseline.Targets[0].Achieved = true; bundle.Baseline = Pin(baseline);
            Assert.IsFalse(runtime.CanRestart);
            var report = Report(); PumpUntil(() => report.IsCompleted);
            Assert.AreEqual(TaskStatus.RanToCompletion, report.Status);
            Assert.Throws<IOException>(() => runtime.FullCycle());
            Assert.AreEqual(0, io.Starts); Assert.IsNull(session.FirstError); Assert.AreEqual(0, io.Quits);
            var close = runtime.Close(); PumpUntil(() => close.IsCompleted); AssertClosed(false);
        }
        [Test] public void UnitySaveRootIsCanonicalBeforeStrictJournalPreparation()
        {
            string supplied = new Game.Feature.Stages.ApplicationPersistentDataSavePathProvider().SaveRootPath;
            string canonical = new ObservationV3RuntimeIO().SaveRoot();
            TestContext.Progress.WriteLine("Unity save path: " + supplied + " | canonical: " + canonical);
            Assert.AreEqual(Path.GetFullPath(supplied), canonical);
            Assert.DoesNotThrow(() => ObservationV3RuntimeWire.Canonical(canonical, null));
            var mixed = canonical.Replace('\\', '/');
            if (mixed != canonical) Assert.Throws<IOException>(() => ObservationV3RuntimeWire.Canonical(mixed, null));
        }
        [TestCase(false)][TestCase(true)] public void OriginFailureKeepsErrorUntilExplicitClose(bool beforePreparation)
        {
            Set(runtime, "options", new ObservationV3Options { Role = OverlayObservationRole.OriginObserver, Owner = ObservationLaunchOwner.SteamDelegated });
            if (beforePreparation) Get<ObservationV3Bundle>(runtime, "bundle").Preparation = null;
            var failure = new IOException("CanonicalPathRequired"); session.TryFail(failure);
            PumpUntil(() => io.LoggedFailures == 1);
            Assert.AreEqual(0, io.Quits); Assert.IsTrue(session.Admission.Closed);
            Assert.AreSame(failure, session.FirstError); Assert.AreEqual(0, native.Initializations);
            var close = runtime.Close(); PumpUntil(() => close.IsCompleted);
            Assert.AreEqual(1, io.Quits); Assert.AreEqual(1, io.LoggedFailures);
        }
        [TestCase("LaunchInputUnverified")][TestCase("ProcessStartMonitorUnavailable")]
        public void FullCycleChecksHandoffInputsBeforeRequestOrHelperCreation(string failure)
        {
            Ready(true); io.HandoffError = new IOException(failure);
            var handoff = runtime.FullCycle(); PumpUntil(() => handoff.IsCompleted);
            Assert.AreEqual(1, io.HandoffChecks); Assert.AreEqual(0, io.Starts);
            Assert.AreEqual(failure, session.FirstError.Message);
            Assert.AreEqual(0, io.Child.Confirmations);
        }
        [Test] public void FirstFailureSurvivesCloseAndLateHandoffIoError()
        {
            Ready(true); var handoff = runtime.FullCycle(); PumpUntil(() => io.Work.Any(w => w.Item1 == "bootstrap-ready"), "bootstrap-ready");
            var first = new IOException("first"); session.TryFail(first); runtime.Close(); io.Child.InputClosed = true;
            PumpUntil(() => handoff.IsCompleted); AssertClosed(true); Assert.AreSame(first, session.FirstError);
        }
        [Test] public void HelperFailureEvidenceHasOneWriteAndAnAbsoluteJoinBudget()
        {
            using (var release = new ManualResetEventSlim()) using (var entered = new ManualResetEventSlim())
            {
                long now = 0; int writes = 0; Exception recorded = null;
                var first = new IOException("first");
                var evidence = new ObservationV3HostFailureEvidence(e => { Interlocked.Increment(ref writes); recorded = e; entered.Set(); release.Wait(); }, () => now, 10);
                try
                {
                    evidence.Start(first); Assert.IsTrue(entered.Wait(1000)); evidence.Start(new IOException("second"));
                    now = 10; Assert.IsFalse(evidence.Join()); now = 100; Assert.IsFalse(evidence.Join());
                    Assert.AreEqual(1, writes); Assert.AreSame(first, recorded);
                }
                finally { release.Set(); }
            }
        }
        [Test] public void HelperCreationReturningAfterFailureClosesUnclaimedStreamsOnce()
        {
            Ready(true); io.BackgroundOperation = "helper-create";
            using (var entered = new ManualResetEventSlim()) using (var release = new ManualResetEventSlim())
            {
                io.OnStart = () => { entered.Set(); release.Wait(); };
                var handoff = runtime.FullCycle();
                try
                {
                    PumpUntil(() => entered.IsSet);
                    io.Now = 30000; session.CheckDeadlines();
                    PumpUntil(() => handoff.IsCompleted); AssertClosed(true);
                    Assert.AreEqual(0, io.Child.Disposals); Assert.AreEqual(0, io.Child.Confirmations);
                }
                finally { release.Set(); }
                PumpUntil(() => io.Child.Disposals == 1);
                Assert.AreEqual(1, io.Starts); Assert.AreEqual(1, io.Child.InputCloses);
            }
        }
        [Test] public void GrantCommittedButBlockedSendTimesOutWithoutRetryOrCancellationSuccess()
        {
            Ready(true); io.BackgroundOperation = "grant-confirmation";
            using (var entered = new ManualResetEventSlim()) using (var release = new ManualResetEventSlim())
            {
                io.Child.OnConfirmation = () => { entered.Set(); release.Wait(); };
                var handoff = runtime.FullCycle();
                try
                {
                    PumpUntil(() => entered.IsSet); Assert.AreEqual(ObservationV3SessionStage.GrantCommitInFlight, session.Stage);
                    var close = runtime.Close(); Assert.AreSame(close, runtime.Close());
                    io.Now = 30000; session.CheckDeadlines(); PumpUntil(() => handoff.IsCompleted && close.IsCompleted);
                    AssertClosed(true); Assert.AreEqual(1, io.Starts); Assert.AreEqual(1, io.Child.Confirmations);
                    var claim = session.Admission.Facts().Single(f => f.OperationId == "grant-confirmation");
                    Assert.AreEqual("Uncertain", claim.Outcome);
                }
                finally { release.Set(); }
                PumpUntil(() => io.BackgroundTask.IsCompleted);
                Assert.IsNotNull(session.FirstError); Assert.AreEqual(ObservationV3SessionStage.QuitAllowed, session.Stage);
                Assert.AreEqual(1, io.Starts); Assert.AreEqual(1, io.Child.Confirmations); Assert.AreEqual(0, io.Count("exit-completed"));
            }
        }
        [Test] public void BlockedHelperHandleDisposalNeverBlocksMainThreadClose()
        {
            Ready(true); io.BackgroundOperation = "helper-release";
            using (var entered = new ManualResetEventSlim()) using (var release = new ManualResetEventSlim())
            {
                io.Child.OnDispose = () => { entered.Set(); release.Wait(); };
                var handoff = runtime.FullCycle();
                try
                {
                    PumpUntil(() => entered.IsSet); var close = runtime.Close();
                    io.Now = 30000; session.CheckDeadlines();
                    PumpUntil(() => close.IsCompleted && handoff.IsCompleted); AssertClosed(true);
                }
                finally { release.Set(); }
                PumpUntil(() => io.BackgroundTask.IsCompleted); Assert.AreEqual(1, io.Child.Disposals);
            }
        }
        [Test] public void CallbackFaultClosesAdmissionBeforePendingPinWorkerReturns()
        {
            Ready(); Set(runtime, "prepared", true); Set(runtime, "nextPins", 0L); Call("Update");
            Assert.IsTrue(io.Work.Any(w => w.Item1 == "observation-pins"));
            native.CallbackFault = true; steam.Tick(); Call("Update");
            Assert.IsTrue(session.Admission.Closed); Assert.IsTrue(Get<bool>(permit, "revoked"));
            Assert.AreEqual(1, native.Shutdowns); // No worker was advanced.
            Assert.AreEqual(0, io.PipeCloses);
            AssertClosed(true);
        }
        [Test] public void IdentityCadenceDetectsAccountAtOneSecondWhilePinIsPending()
        {
            Ready(); Set(runtime, "nextIdentity", 1000L); Set(runtime, "pinsPending", true);
            native.Account++; io.Now = 999; Call("Update"); Assert.IsNull(session.FirstError);
            io.Now = 1000; Call("Update"); Assert.IsNotNull(session.FirstError); AssertClosed(true);
        }
        [TestCase("save-fail")][TestCase("save-late")]
        public void FinalLifecycleStorageFailureNeverReopensSuccess(string fault)
        {
            Ready();
            if (fault == "save-fail") io.FailedSave = "ReplacementObserver-native-shutdown.json";
            var close = runtime.Close();
            if (fault == "save-late")
                PumpUntil(() => io.Work.Any(w => w.Item1 == "save:ReplacementObserver-native-shutdown.json"), "save:ReplacementObserver-native-shutdown.json");
            if (fault.EndsWith("late")) { io.Now = 30000; session.CheckDeadlines(); }
            PumpUntil(() => close.IsCompleted); AssertClosed(true);
            var first = session.FirstError; io.SendReturned.TrySetResult(true); io.ReleaseAll(); Call("Update");
            Assert.AreSame(first, session.FirstError); Assert.AreEqual(1, io.Quits);
            Assert.AreEqual(0, io.Sent.Count);
        }
        [Test] public void WatchdogClosesSessionWhileNativeShutdownIsBlockedAndRejectsLateReturn()
        {
            Ready();
            using (var entered = new ManualResetEventSlim()) using (var release = new ManualResetEventSlim())
            {
                native.ShutdownAction = () => { entered.Set(); if (!release.Wait(2000)) throw new TimeoutException("fixture-release"); };
                var watch = Task.Run(() =>
                {
                    try
                    {
                        Assert.IsTrue(entered.Wait(2000)); io.Now = 30000; session.CheckDeadlines();
                        Assert.IsTrue(session.Admission.Closed); Assert.AreEqual(0, io.Quits); Assert.AreEqual(0, io.Count("exit-completed"));
                    }
                    finally { release.Set(); }
                });
                var close = runtime.Close(); PumpUntil(() => close.IsCompleted); watch.GetAwaiter().GetResult();
                AssertClosed(true); Assert.AreEqual(0, io.Count("exit-completed"));
            }
        }
        static ProcessIdentity Identity(int pid) => new ProcessIdentity { Pid = pid, StartTicks = pid, Path = "fake.exe", Sha256 = new string('a', 64), Session = 1, UserSid = "fake", Logon = "fake" };
        static ObservationRef Reference(string path) => new ObservationRef { Path = path, Sha256 = new string('a', 64) };
        T Document<T>(string kind) where T : ObservationDocument, new()
        {
            var value = (T)Filled(typeof(T)); return ObservationV3RuntimeWire.Stamp(value, kind, "ReplacementObserver", 1, run);
        }
        static object Filled(Type type)
        {
            if (type == typeof(string)) return "fake";
            if (type.IsValueType) return Activator.CreateInstance(type);
            if (type.IsArray) return Array.CreateInstance(type.GetElementType(), 0);
            var value = Activator.CreateInstance(type);
            foreach (var field in type.GetFields()) field.SetValue(value, field.IsDefined(typeof(ObservationNullableAttribute), false) ? null : Filled(field.FieldType));
            return value;
        }
        static ObservationPinned<T> Pin<T>(T value)
        { var bytes = ObservationV3RuntimeWire.Bytes(value); return new ObservationPinned<T>(bytes, new ObservationRef { Path = "fake-" + typeof(T).Name, Sha256 = ObservationV3RuntimeWire.Hash(bytes) }); }
        sealed class Native : ISteamNativeApi, ISteamObservationIdentityApi
        {
            public int Initializations, Shutdowns; public bool CallbackFault, GetterFault, LoggedOn = true;
            public ulong Account = 76561198000000000UL; public Action ShutdownAction;
            public bool IsPacksizeCompatible() => true;
            public SteamDllCheckObservation ObserveDllCheck() => SteamDllCheckObservation.UpstreamDisabled(true);
            public bool Initialize() { Initializations++; return true; }
            public void RunCallbacks() { if (CallbackFault) throw new IOException("callback-fault"); }
            public void Shutdown() { Shutdowns++; ShutdownAction?.Invoke(); }
            public uint GetAppId() => 5218360;
            public ulong GetSteamId() { if (GetterFault) throw new IOException("identity-getter"); return Account; }
            public bool IsSteamIdValid() => true;
            public bool IsLoggedOn() => LoggedOn;
            public bool IsOverlayEnabled() => true;
            public void RegisterOverlayActivationCallback(Action<bool> observer) { }
            public void DisposeOverlayActivationCallback() { }
        }
        sealed class FakeIO : ObservationV3RuntimeIO
        {
            readonly ObservationV3RuntimeAdapterTests test;
            public readonly ConcurrentQueue<Tuple<string, Action>> Work = new ConcurrentQueue<Tuple<string, Action>>();
            public readonly List<ObservationV3Envelope> Sent = new List<ObservationV3Envelope>();
            public readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();
            readonly Queue<ObservationV3Frame> incoming = new Queue<ObservationV3Frame>();
            TaskCompletionSource<ObservationV3Frame> reader;
            public readonly ChildProcess Child;
            public long Now; public int Quits, Failures, Starts, PipeCloses; long received;
            public string FailedSave, FailedSend, HeldSend, BackgroundOperation;
            public Action OnStart;
            public Exception HandoffError;
            public int HandoffChecks;
            public int LoggedFailures;
            public Task BackgroundTask;
            public readonly TaskCompletionSource<bool> SendReturned = new TaskCompletionSource<bool>();
            public FakeIO(ObservationV3RuntimeAdapterTests test) { this.test = test; Child = new ChildProcess(this); }
            public override long Milliseconds => Now;
            public override long Timestamp => Now;
            public override long TimestampFrequency => 1000;
            public override Task<T> Worker<T>(string operation, Func<T> work, CancellationToken token)
            {
                if (operation == BackgroundOperation) { var background = Task.Run(work, token); BackgroundTask = background; return background; }
                var done = new TaskCompletionSource<T>();
                Work.Enqueue(Tuple.Create(operation, (Action)(() =>
                {
                    try
                    {
                        // Only initial file/OS discovery is stubbed. Runtime continuations remain real.
                        var value = operation == "prepare-origin" || operation == "observation-pins" || operation.StartsWith("pin:") || operation == "request-validate" ? (T)(object)true : work();
                        done.TrySetResult(value);
                    }
                    catch (Exception e) { done.TrySetException(e); }
                })));
                return done.Task;
            }
            public void RunOne(string blocked = null)
            { if (Work.TryPeek(out var next) && next.Item1 != blocked && Work.TryDequeue(out var work)) work.Item2(); }
            public void ReleaseAll() { while (Work.Count != 0) RunOne(); }
            public override void ValidatePins(ObservationV3Bundle bundle) { }
            public override void ValidateObservation(ObservationV3Bundle bundle, ObservationV3ProcessHandles handles, ProcessIdentity self, ProcessIdentity client) { if (ObservationError != null) throw ObservationError; }
            public Exception ObservationError;
            public override void ValidateOrigin(ObservationV3Bundle bundle, ObservationV3ProcessHandles handles, ProcessIdentity self) { }
            public override void ValidateHandoffInputs(ObservationV3Bundle bundle, ObservationV3ProcessHandles handles, ProcessIdentity self)
            { HandoffChecks++; if (HandoffError != null) throw HandoffError; }
            public override ProcessStartInfo HelperStart(ObservationV3Bundle bundle, ObservationRef request) => new ProcessStartInfo();
            public override ObservationV3OriginProcess StartHelper(ProcessStartInfo start) { Starts++; OnStart?.Invoke(); return Child; }
            public bool HasFile(string name) => files.ContainsKey(name);
            public override ObservationRef Save(string root, string name, byte[] bytes)
            { if (name == FailedSave) throw new IOException("save-failed:" + name); if (files.ContainsKey(name)) throw new IOException("create-only:" + name); files.Add(name, bytes); return new ObservationRef { Path = name, Sha256 = ObservationV3RuntimeWire.Hash(bytes) }; }
            public override ObservationPinned<T> Read<T>(ObservationRef reference, string root) => new ObservationPinned<T>(files[reference.Path], reference);
            public override void FilePin(ObservationRef reference, string root)
            { Assert.AreEqual(reference.Sha256, ObservationV3RuntimeWire.Hash(files[reference.Path])); }
            public override void WriteFailure(ObservationV3Bundle bundle, Exception error, string role, ObservationV3Admission admission) { Failures++; }
            public override void LogFailure(Exception error) { LoggedFailures++; }
            public override void Quit() { Assert.IsTrue((bool)test.Call("WantsToQuit")); Quits++; }
            public int Count(string kind) => Sent.Count(f => f.Kind == kind);
            public sealed class ChildProcess : ObservationV3OriginProcess
            {
                readonly FakeIO io; public volatile bool InputClosed; public bool ReturnRetainedAfterClose; public int Confirmations, Disposals, InputCloses; public Action OnConfirmation, OnDispose;
                public ChildProcess(FakeIO io) : base(null) { this.io = io; }
                public override ProcessIdentity Capture(ObservationV3ProcessHandles handles) => Identity(8);
                public override void Match(ObservationV3ProcessHandles handles, ProcessIdentity identity) { if (InputClosed && !ReturnRetainedAfterClose) throw new IOException("ExitedHandle"); }
                public override void WriteLine(string value)
                {
                    if (InputClosed) throw new IOException("ClosedInput");
                    if (value.Length == 64) { Confirmations++; OnConfirmation?.Invoke(); }
                }
                public override string ReadLine()
                {
                    if (InputClosed) return null;
                    var ready = io.test.Document<ObservationV3BootstrapReady>("bootstrap-ready"); ready.OriginHandleRetained = true;
                    ready.Origin = Identity(7); ready.Helper = Identity(8);
                    ready.RequestRef = Ref("request.json"); ready.GrantRef = Ref("bootstrap-grant.json");
                    return ObservationV3Wire.Serialize(io.Save("", "ready", ObservationV3RuntimeWire.Bytes(ready)));
                }
                ObservationRef Ref(string name) => new ObservationRef { Path = name, Sha256 = ObservationV3RuntimeWire.Hash(io.files[name]) };
                public override void CloseInput() { Interlocked.Increment(ref InputCloses); InputClosed = true; }
                public override void Dispose() { Interlocked.Increment(ref Disposals); OnDispose?.Invoke(); InputClosed = true; }
            }
        }
    }
}
