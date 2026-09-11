using System;
using System.Collections.Generic;
using System.IO;
using Game.Exhibition.RestartExperiment;
using Game.Exhibition.Editor;
using NUnit.Framework;

namespace Game.Exhibition.Tests
{
    public sealed class RestartExperimentTests
    {
        private sealed class CycleFake : ICycleEnvironment
        {
            public long Now;
            public Exception ExecutionError, CleanupError, ReleaseError;
            public int Cleanups;
            public int Shutdowns, SteamStarts, GameStarts, Probes, LockReleases;
            public bool LockHeld, LockDenied, ParentStuck, SteamStuck, OtherGame, ReplacedSteam;
            public string FailStage;
            public Func<int, bool> Probe = budget => true;
            public Action<string> Inspect = _ => { };
            public long ClockStep;
            public bool CommandStuck;
            public long Milliseconds { get { long value = Now; Now += ClockStep; return value; } }
            public void Validate() { if (FailStage == "validate") throw new IOException(); }
            public IDisposable AcquireCycleLock()
            {
                if (LockDenied || LockHeld) throw new IOException("lock denied or contended");
                LockHeld = true; return new Lease(() => { LockHeld = false; LockReleases++; if (ReleaseError != null) ThrowOrigin(ReleaseError); });
            }
            public bool ParentAlive() { Inspect("parent"); return ParentStuck || Now < 200; }
            public void EnsureNoOtherGame() { Inspect("game-check"); if (OtherGame) throw new IOException("manual game"); }
            public bool OriginalSteamAlive()
            {
                if (ExecutionError != null) ThrowOrigin(ExecutionError);
                Inspect("original"); if (ReplacedSteam) throw new IOException("replacement Steam");
                return SteamStuck || Shutdowns == 0 || Now < 400;
            }
            public void RequestSteamExit(Deadline deadline) { Inspect("shutdown-create"); deadline.Remaining(); Assert.That(LockHeld, Is.True); Shutdowns++; }
            public bool ShutdownCommandAlive() { Inspect("command"); return CommandStuck; }
            public void EnsureSteamExited() { Inspect("steam-exited"); if (ReplacedSteam) throw new IOException("replacement"); }
            public void StartSteam() { Assert.That(LockHeld, Is.True); SteamStarts++; }
            public void EnsureNewSteamUnchanged() { Inspect("new-steam"); if (ReplacedSteam) throw new IOException("replacement"); }
            public bool ProbeReady(Deadline deadline) { Inspect("probe-prepare"); int budget = (int)deadline.Remaining(); Probes++; return Probe(budget); }
            public void StartGame(Deadline deadline) { Inspect("game-prepare"); if (deadline != null) deadline.Remaining(); Assert.That(LockHeld, Is.True); GameStarts++; }
            public void Delay(int milliseconds) { Assert.That(milliseconds, Is.GreaterThan(0)); Now += milliseconds; }
            public void Cleanup() { Assert.That(LockHeld, Is.True); Cleanups++; Inspect("cleanup"); if (CleanupError != null) ThrowOrigin(CleanupError); }
            private sealed class Lease : IDisposable { private readonly Action release; public Lease(Action release) { this.release = release; } public void Dispose() => release(); }
        }




        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void ThrowOrigin(Exception error) { throw error; }

        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(false, false, true)]
        [TestCase(true, true, true)]
        public void FailurePreservesOriginAndReleasesResources(bool execute, bool cleanup, bool release)
        {
            var fake = new CycleFake {
                ExecutionError = execute ? new System.ComponentModel.Win32Exception(5) : null,
                CleanupError = cleanup ? new IOException("cleanup failure") : null,
                ReleaseError = release ? new IOException("release failure") : null
            };
            Exception result = Assert.Catch(() => Cycle.Run(fake, Trial.FullCycle));
            var failures = new List<Exception>();
            CollectFailures(result, failures);
            var expected = new List<Exception>();
            if (execute) expected.Add(fake.ExecutionError);
            if (cleanup) expected.Add(fake.CleanupError);
            if (release) expected.Add(fake.ReleaseError);
            Assert.That(failures, Is.EqualTo(expected));
            foreach (var error in failures) Assert.That(error.StackTrace, Does.Contain("ThrowOrigin"));
            if (execute) {
                Assert.That(fake.ExecutionError.Data["RestartOperation"], Is.EqualTo("Execute"));
                Assert.That(fake.SteamStarts + fake.Probes + fake.GameStarts, Is.Zero);
            }
            if (cleanup) Assert.That(fake.CleanupError.Data["RestartOperation"], Is.EqualTo("Cleanup"));
            if (release) Assert.That(fake.ReleaseError.Data["RestartOperation"], Is.EqualTo("ReleaseCycleLock"));
            Assert.That(fake.Cleanups, Is.EqualTo(1));
            Assert.That(fake.LockReleases, Is.EqualTo(1));
            Assert.That(fake.LockHeld, Is.False);
        }

        private static void CollectFailures(Exception error, List<Exception> failures)
        {
            var aggregate = error as AggregateException;
            if (aggregate == null) { failures.Add(error); return; }
            foreach (var inner in aggregate.InnerExceptions) CollectFailures(inner, failures);
        }

        [TestCase("command", 0, 0)]
        [TestCase("steam-exited", 0, 0)]
        [TestCase("game-prepare", 1, 1)]
        public void AccessDeniedAfterShutdownDoesNotSubmitAnotherLaunch(string stage, int steamStarts, int probes)
        {
            var denied = new System.ComponentModel.Win32Exception(5);
            var fake = new CycleFake();
            fake.Inspect = current => { if (current == stage) ThrowOrigin(denied); };
            Assert.That(Assert.Catch(() => Cycle.Run(fake, Trial.FullCycle)), Is.SameAs(denied));
            Assert.That(fake.Shutdowns, Is.EqualTo(1));
            Assert.That(fake.SteamStarts, Is.EqualTo(steamStarts));
            Assert.That(fake.Probes, Is.EqualTo(probes));
            Assert.That(fake.GameStarts, Is.Zero);
            Assert.That(fake.Cleanups, Is.EqualTo(1));
            Assert.That(fake.LockReleases, Is.EqualTo(1));
        }

        [Test]
        public void FailureContextKeepsInnerOperationAndNativeError()
        {
            var error = new System.ComponentModel.Win32Exception(5);
            Cycle.Note(error, "RestartOperation", "StartSteam.CaptureNewSteamIdentity");
            Cycle.Note(error, "RestartOperation", "Execute");
            Assert.That(error.Data["RestartOperation"], Is.EqualTo("StartSteam.CaptureNewSteamIdentity"));
            Assert.That(error.NativeErrorCode, Is.EqualTo(5));
        }

        [TestCase(Trial.FullCycle, 1, 1, 1, 1)]
        public void TrialsHaveExactCreationCountsAndReleaseLock(Trial trial, int shutdown, int steam, int probes, int game)
        {
            var fake = new CycleFake(); Cycle.Run(fake, trial);
            Assert.That(new[] { fake.Shutdowns, fake.SteamStarts, fake.Probes, fake.GameStarts }, Is.EqualTo(new[] { shutdown, steam, probes, game }));
            Assert.That(fake.LockReleases, Is.EqualTo(1)); Assert.That(fake.LockHeld, Is.False);
        }

        [TestCase(Trial.FullCycle)]
        public void LockFailureHasZeroSideEffects(Trial trial)
        {
            var fake = new CycleFake { LockDenied = true };
            Assert.Throws<IOException>(() => Cycle.Run(fake, trial));
            Assert.That(fake.Shutdowns + fake.SteamStarts + fake.GameStarts + fake.Probes, Is.Zero);
        }

        [Test]
        public void FullCycleReentryContendsOnSameCycleLock()
        {
            var fake = new CycleFake();
            fake.Probe = budget => {
                Assert.Throws<IOException>(() => Cycle.Run(fake, Trial.FullCycle));
                return true;
            };
            Cycle.Run(fake, Trial.FullCycle);
            Assert.That(fake.GameStarts, Is.EqualTo(1)); Assert.That(fake.Shutdowns, Is.EqualTo(1));
        }

        [TestCase(true)] [TestCase(false)]
        public void ParentAndSteamTimeoutNeverRestartLoop(bool parent)
        {
            var fake = new CycleFake { ParentStuck = parent, SteamStuck = !parent };
            Assert.Throws<TimeoutException>(() => Cycle.Run(fake, Trial.FullCycle));
            Assert.That(fake.Shutdowns, Is.EqualTo(parent ? 0 : 1));
            Assert.That(fake.SteamStarts + fake.GameStarts + fake.Probes, Is.Zero);
            Assert.That(fake.Now, Is.EqualTo(parent ? 30000 : 60200));
            Assert.That(fake.LockHeld, Is.False);
        }

        [TestCase("timeout")] [TestCase("termination")] [TestCase("shutdown")] [TestCase("malformed")]
        public void FatalProbeFailureHasNoRetryOrGame(string reason)
        {
            var fake = new CycleFake { Probe = budget => throw new IOException(reason) };
            Assert.Throws<IOException>(() => Cycle.Run(fake, Trial.FullCycle));
            Assert.That(fake.Probes, Is.EqualTo(1)); Assert.That(fake.GameStarts, Is.Zero);
            Assert.That(fake.SteamStarts, Is.EqualTo(1)); Assert.That(fake.LockHeld, Is.False);
        }

        [Test]
        public void NotReadyWaitUsesOneAbsoluteDeadlineIncludingNativeTime()
        {
            var fake = new CycleFake();
            fake.Probe = budget => { Assert.That(budget, Is.InRange(1, 10000)); fake.Now += budget; return false; };
            Assert.Throws<TimeoutException>(() => Cycle.Run(fake, Trial.FullCycle));
            Assert.That(fake.Now, Is.EqualTo(120400)); Assert.That(fake.GameStarts, Is.Zero);
            Assert.That(fake.SteamStarts, Is.EqualTo(1));
        }

        [TestCase(true)] [TestCase(false)]
        public void ManualGameOrReplacementAfterProbeStopsFinalLaunch(bool manualGame)
        {
            var fake = new CycleFake();
            fake.Probe = budget => { fake.OtherGame = manualGame; fake.ReplacedSteam = !manualGame; return true; };
            Assert.Throws<IOException>(() => Cycle.Run(fake, Trial.FullCycle)); Assert.That(fake.GameStarts, Is.Zero);
        }

        [Test]
        public void ProcessIdentityUsesStartTicksScopeAndPath()
        {
            var a = Identity(); var b = Identity();
            Assert.That(WindowsIdentityCapture.SameProcess(a, b), Is.True);
            b.StartTicks++; Assert.That(WindowsIdentityCapture.SameProcess(a, b), Is.False);
            b = Identity(); b.Logon = "another"; Assert.That(WindowsIdentityCapture.SameProcess(a, b), Is.False);
            b = Identity(); b.Session++; Assert.That(WindowsIdentityCapture.SameProcess(a, b), Is.False);
            b = Identity(); b.UserSid = "another"; Assert.That(WindowsIdentityCapture.SameProcess(a, b), Is.False);
            b = Identity(); b.Path = @"D:\Other\steam.exe"; Assert.That(WindowsIdentityCapture.SameProcess(a, b), Is.False);
            b = Identity(); b.Pid++; b.StartTicks++;
            Assert.That(WindowsIdentityCapture.LockName(a), Is.EqualTo(WindowsIdentityCapture.LockName(b)));
            Assert.That(WindowsIdentityCapture.LockName(a), Does.Not.Contain(a.UserSid));
        }
        [Test]
        public void UnityProcessSessionMatchesFreshWindowsPowerShellObservation()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) Assert.Ignore("Windows session contract");
            using (var current = System.Diagnostics.Process.GetCurrentProcess())
            {
                var captured = WindowsIdentityCapture.Capture(current, false);
                var start = new System.Diagnostics.ProcessStartInfo {
                    FileName = ExperimentFiles.PowerShell,
                    Arguments = "-NoProfile -Command \"(Get-Process -Id " + current.Id + ").SessionId\"",
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                using (var observer = System.Diagnostics.Process.Start(start))
                {
                    try
                    {
                        var output = observer.StandardOutput.ReadToEndAsync();
                        var error = observer.StandardError.ReadToEndAsync();
                        Assert.That(observer.WaitForExit(15000), Is.True, "Read-only observer timeout");
                        Assert.That(observer.ExitCode, Is.Zero);
                        Assert.That(error.Result, Is.Empty);
                        Assert.That(captured.Session, Is.EqualTo(int.Parse(output.Result.Trim())),
                            "Unity and Windows must agree before a helper handoff");
                    }
                    finally { if (!observer.HasExited) { observer.Kill(); observer.WaitForExit(); } }
                }
            }
        }

        [Test]
        public void NewlyStartedPowerShellIdentityDoesNotRequireLoadedModuleEnumeration()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) Assert.Ignore("Windows process identity");
            var start = new System.Diagnostics.ProcessStartInfo {
                FileName = ExperimentFiles.PowerShell,
                Arguments = "-NoProfile -Command \"[Console]::In.ReadLine() | Out-Null\"",
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true
            };
            for (int i = 0; i < 5; i++)
                using (var child = System.Diagnostics.Process.Start(start))
                {
                    try
                    {
                        var identity = WindowsIdentityCapture.Capture(child, false);
                        Assert.That(identity.Path, Is.EqualTo(WindowsIdentityCapture.CanonicalPath(start.FileName)).IgnoreCase);
                        Assert.That(identity.Pid, Is.EqualTo(child.Id));
                        child.StandardInput.Close();
                        Assert.That(child.WaitForExit(15000), Is.True);
                    }
                    finally { if (!child.HasExited) { child.Kill(); child.WaitForExit(); } }
                }
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void FailedNativeInitEndsObservationWithoutQueryShutdownOrLibraryUnload(int code)
        {
            var result = new ProbeObservation(); int queries = 0, shutdowns = 0;
            bool release = NativeProbe.ObserveSession(result, () => code, () => queries++, () => shutdowns++);
            Assert.That(release, Is.False);
            Assert.That(queries + shutdowns, Is.Zero);
            Assert.That(result.ShutdownReturned, Is.False);
            if (code == 2) Assert.That(result.Error, Is.Null);
            else Assert.That(result.Error, Is.Not.Empty);
        }

        [TestCase("none")] [TestCase("query")] [TestCase("shutdown")]
        public void SuccessfulNativeInitAlwaysAttemptsShutdownEvenWhenQueryFails(string failure)
        {
            var result = new ProbeObservation(); int shutdowns = 0;
            bool release = NativeProbe.ObserveSession(result, () => 0,
                () => { if (failure == "query") throw new IOException("query"); },
                () => { shutdowns++; if (failure == "shutdown") throw new IOException("shutdown"); });
            Assert.That(shutdowns, Is.EqualTo(1));
            Assert.That(release, Is.EqualTo(failure != "shutdown"));
            Assert.That(result.ShutdownReturned, Is.EqualTo(failure != "shutdown"));
            if (failure != "none") Assert.That(result.Error, Is.Not.Empty);
        }

        private static ProcessIdentity Identity() => new ProcessIdentity { Pid = 11, StartTicks = 25, Session = 1, Path = @"D:\Steam\steam.exe", UserSid = "S-1-5-21-example", Logon = "logon" };

        [TestCase("nonce")] [TestCase("pid")] [TestCase("ticks")] [TestCase("shutdown")] [TestCase("error")]
        public void ProbeMustExitCleanlyWithMatchingIdentity(string damage)
        {
            var expected = new ExperimentRequest { Nonce = "nonce", AppId = 1, SteamId = 2 };
            var row = new ProbeObservation { InitDisposition = ProbeInitDisposition.Succeeded, InitCalled = true, InitReturned = true, Nonce = "nonce", Pid = 3, StartTicks = 4, AppId = 1, SteamId = 2, LoggedOn = true, QueryCalled = true, QueryReturned = true, ShutdownCalled = true, ShutdownReturned = true };
            if (damage == "nonce") row.Nonce = "old";
            if (damage == "pid") row.Pid++;
            if (damage == "ticks") row.StartTicks++;
            if (damage == "shutdown") row.ShutdownReturned = false;
            if (damage == "error") row.Error = "query error";
            Assert.Throws<IOException>(() => WindowsCycleEnvironment.ValidateProbe(row, expected, 3, 4));
        }

        [TestCase("login")] [TestCase("appid")] [TestCase("steamid")]
        public void ProbeIdentityMismatchAndNotReadyAreObservations(string condition)
        {
            var expected = new ExperimentRequest { Nonce = "n", AppId = 1, SteamId = 2 };
            var row = new ProbeObservation { InitDisposition = ProbeInitDisposition.Succeeded, InitCalled = true, InitReturned = true, Nonce = "n", Pid = 3, StartTicks = 4, AppId = 1, SteamId = 2, LoggedOn = true, QueryCalled = true, QueryReturned = true, ShutdownCalled = true, ShutdownReturned = true };
            Assert.That(WindowsCycleEnvironment.ValidateProbe(row, expected, 3, 4), Is.True);
            if (condition == "login") row.LoggedOn = false;
            if (condition == "appid") row.AppId++;
            if (condition == "steamid") row.SteamId++;
            Assert.That(WindowsCycleEnvironment.ValidateProbe(row, expected, 3, 4), Is.False);
        }

        [TestCase(1, "FailedGeneric")] [TestCase(3, "VersionMismatch")] [TestCase(99, "UnknownResult")]
        public void FatalInitResultsReportTheirCause(int code, string cause)
        {
            var row = new ProbeObservation { InitDisposition = ProbeInitDisposition.Fatal, InitCalled = true, InitReturned = true, Nonce = "n", Pid = 3, StartTicks = 4, InitResult = code };
            var error = Assert.Throws<IOException>(() => WindowsCycleEnvironment.ValidateProbe(row,
                new ExperimentRequest { Nonce = "n" }, 3, 4));
            Assert.That(error.Message, Does.Contain(cause));
        }

        [TestCase("shutdown")] [TestCase("appid")] [TestCase("steamid")] [TestCase("login")] [TestCase("error")] [TestCase("nonce")]
        public void NoSteamClientCannotBypassResultIntegrity(string damage)
        {
            var row = new ProbeObservation { InitDisposition = ProbeInitDisposition.NoSteamClient, InitCalled = true, InitReturned = true, Nonce = "n", Pid = 3, StartTicks = 4, InitResult = 2 };
            if (damage == "shutdown") row.ShutdownReturned = true;
            if (damage == "appid") row.AppId = 1;
            if (damage == "steamid") row.SteamId = 1;
            if (damage == "login") row.LoggedOn = true;
            if (damage == "error") row.Error = "recording failed";
            if (damage == "nonce") row.Nonce = "stale";
            Assert.Throws<IOException>(() => WindowsCycleEnvironment.ValidateProbe(row, new ExperimentRequest { Nonce = "n" }, 3, 4));
        }

        [TestCase(Trial.FullCycle, 1)]
        public void NoSteamClientThenReadyReobservesWithoutAnotherClientCycle(Trial trial, int games)
        {
            var expected = new ExperimentRequest { Nonce = "n", AppId = 1, SteamId = 2 };
            var fake = new CycleFake(); int observations = 0;
            fake.Probe = budget =>
            {
                bool first = observations++ == 0;
                var row = new ProbeObservation { InitDisposition = ProbeInitDisposition.Succeeded, InitCalled = true, InitReturned = true, Nonce = "n", Pid = 3, StartTicks = 4,
                    InitResult = first ? 2 : 0, AppId = first ? 0u : 1u, SteamId = first ? 0ul : 2ul,
                    LoggedOn = !first, QueryCalled = !first, QueryReturned = !first, ShutdownCalled = !first, ShutdownReturned = !first };
                row.InitDisposition = ProbeInitPolicy.Classify(row.InitResult, row.InitDiagnostic);
                return WindowsCycleEnvironment.ValidateProbe(row, expected, 3, 4);
            };
            Cycle.Run(fake, trial);
            Assert.That(fake.Probes, Is.EqualTo(2));
            Assert.That(fake.Shutdowns, Is.EqualTo(1));
            Assert.That(fake.SteamStarts, Is.EqualTo(1));
            Assert.That(fake.GameStarts, Is.EqualTo(games));
        }

        [TestCase(true, 0)] [TestCase(false, 0)] [TestCase(true, 1)] [TestCase(false, 2)]
        public void LateParentObservationRejectsBothAliveAndExited(bool alive, int overshoot)
        {
            var fake = new CycleFake { ParentStuck = alive };
            fake.Inspect = stage => { if (stage == "parent") fake.Now = 30000 + overshoot; };
            Assert.Throws<TimeoutException>(() => Cycle.Run(fake, Trial.FullCycle));
            Assert.That(fake.Shutdowns + fake.Probes + fake.GameStarts, Is.Zero);
            Assert.That(fake.LockHeld, Is.False);
        }

        [TestCase(true, 0)] [TestCase(false, 0)] [TestCase(true, 1)] [TestCase(false, 2)]
        public void LatePostProbeChecksRejectReadyAndNotReady(bool ready, int overshoot)
        {
            var fake = new CycleFake();
            fake.Probe = _ => ready;
            fake.Inspect = stage => { if (stage == "new-steam" && fake.Probes > 0) fake.Now = 120400 + overshoot; };
            Assert.Throws<TimeoutException>(() => Cycle.Run(fake, Trial.FullCycle));
            Assert.That(fake.Probes, Is.EqualTo(1)); Assert.That(fake.GameStarts, Is.Zero);
            Assert.That(fake.LockHeld, Is.False);
        }

        [TestCase("probe-prepare")] [TestCase("game-prepare")]
        public void ExpensiveFinalPreparationCannotLaunchAfterReadinessDeadline(string point)
        {
            var fake = new CycleFake();
            fake.Inspect = stage => { if (stage == point) fake.Now = 120400; };
            Assert.Throws<TimeoutException>(() => Cycle.Run(fake, Trial.FullCycle));
            Assert.That(fake.GameStarts, Is.Zero);
            if (point == "probe-prepare") Assert.That(fake.Probes, Is.Zero);
        }

        [Test]
        public void AdjacentClockReadsCannotProduceNonpositiveDelay()
        {
            var fake = new CycleFake { ClockStep = 2, ParentStuck = true };
            Assert.Throws<TimeoutException>(() => Cycle.Run(fake, Trial.FullCycle));
            Assert.That(fake.LockHeld, Is.False);
        }

        [TestCase(Trial.FullCycle)]
        public void OwnedCommandMustExitWithinOriginalShutdownBudget(Trial trial)
        {
            var fake = new CycleFake { CommandStuck = true };
            var error = Assert.Throws<TimeoutException>(() => Cycle.Run(fake, trial));
            Assert.That(error.Message, Is.EqualTo("ShutdownCommandExitTimeout"));
            Assert.That(fake.Now, Is.EqualTo(60200));
            Assert.That(fake.SteamStarts + fake.Probes + fake.GameStarts, Is.Zero);
            Assert.That(fake.LockHeld, Is.False);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void ShutdownCreationConsumesSameDeadline(int over)
        {
            var fake = new CycleFake();
            fake.Inspect = stage => { if (stage == "shutdown-create") fake.Now = 60200 + over; };
            Assert.Throws<TimeoutException>(() => Cycle.Run(fake, Trial.FullCycle));
            Assert.That(fake.Shutdowns, Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void CleanupIsInsideLockEvenOnFailure(bool fail)
        {
            var fake = new CycleFake(); var order = new List<string>();
            fake.Inspect = stage =>
            {
                if (stage == "cleanup")
                { Assert.That(fake.LockHeld, Is.True); order.Add(stage); }
                if (fail && stage == "parent") throw new IOException("primary");
            };
            if (fail) Assert.Throws<IOException>(() => Cycle.Run(fake, Trial.FullCycle));
            else Cycle.Run(fake, Trial.FullCycle);
            Assert.That(order, Is.EqualTo(new[] { "cleanup" }));
            Assert.That(fake.LockHeld, Is.False);
        }

        [Test]
        public void PrimaryAndCleanupFailuresSurviveLockRelease()
        {
            var fake = new CycleFake();
            fake.Inspect = stage => { if (stage == "parent" || stage == "cleanup") throw new IOException(stage); };
            var error = Assert.Throws<AggregateException>(() => Cycle.Run(fake, Trial.FullCycle));
            Assert.That(error.ToString(), Does.Contain("parent").And.Contain("cleanup"));
            Assert.That(fake.LockHeld, Is.False);
        }

        [TestCase(0)] [TestCase(-1)] [TestCase(-2)]
        public void BackendRejectsNonpositiveSleep(int delay)
        { Assert.Throws<ArgumentOutOfRangeException>(() => new WindowsCycleEnvironment(null, null).Delay(delay)); }

        [TestCase("client")] [TestCase("hash")] [TestCase("environment")]
        public void ActualGamePreparationSeamChecksDeadlineAfterLastCost(string point)
        {
            long now = 0; int starts = 0;
            Action<string> cost = stage => { if (stage == point) now = 100; };
            WithCompletedRequest((request, requestPath) => {
                Assert.Throws<TimeoutException>(() => LaunchEnvironment.Start(() => LaunchEnvironment.PrepareCompletedResetSubmission(request, requestPath,
                    () => { }, () => cost("hash"), () => cost("client"), () => cost("environment")),
                    new Deadline(() => now, 100, "late"), _ => { starts++; return null; }));
                Assert.That(starts, Is.Zero);
            });
        }

        [TestCase("client")] [TestCase("environment")]
        public void ActualProbePreparationSeamChecksDeadlineAfterLastCost(string point)
        {
            long now = 0; int starts = 0;
            Assert.Throws<TimeoutException>(() => LaunchEnvironment.Start(() => LaunchEnvironment.PrepareProbe(new ExperimentRequest { AppId = 123 },
                () => new System.Diagnostics.ProcessStartInfo(), () => { if (point == "client") now = 100; },
                () => { if (point == "environment") now = 100; }), new Deadline(() => now, 100, "late"), _ => { starts++; return null; }));
            Assert.That(starts, Is.Zero);
        }

        [Test]
        public void InitDescriptionIsInformationalButAllOtherErrorsRemainFatal()
        {
            var row = new ProbeObservation { Nonce = "n", Pid = 3, StartTicks = 4 };
            NativeProbe.ObserveSession(row, () => NativeProbe.CaptureInit(row, buffer =>
            { System.Text.Encoding.UTF8.GetBytes("No Steam client").CopyTo(buffer, 0); return 2; }),
                () => Assert.Fail("query"), () => Assert.Fail("shutdown"));
            Assert.That(row.InitDiagnostic, Is.EqualTo("No Steam client"));
            Assert.That(WindowsCycleEnvironment.ValidateProbe(row, new ExperimentRequest { Nonce = "n" }, 3, 4), Is.False);
            row.QueryError = "query failed";
            Assert.Throws<IOException>(() => WindowsCycleEnvironment.ValidateProbe(row, new ExperimentRequest { Nonce = "n" }, 3, 4));
        }

        private static ProbeObservation GlobalUserUnavailable()
        {
            var row = new ProbeObservation { Nonce = "n", Pid = 3, StartTicks = 4 };
            bool unload = NativeProbe.ObserveSession(row, () => NativeProbe.CaptureInit(row, buffer =>
                { System.Text.Encoding.UTF8.GetBytes("ConnectToGlobalUser failed.").CopyTo(buffer, 0); return 1; }),
                () => Assert.Fail("query after failed Init"), () => Assert.Fail("Shutdown after failed Init"));
            Assert.That(unload, Is.False);
            Assert.That(row.Error, Is.Null);
            return row;
        }

        [TestCase(null)] [TestCase("")] [TestCase("ConnectToGlobalUser failed. ")]
        [TestCase("connecttoglobaluser failed.")] [TestCase("prefix ConnectToGlobalUser failed.")]
        public void OtherGenericDiagnosticsRemainFatal(string diagnostic)
        {
            Assert.That(ProbeInitPolicy.Classify(1, diagnostic), Is.EqualTo(ProbeInitDisposition.Fatal));
            var row = GlobalUserUnavailable(); row.InitDiagnostic = diagnostic;
            row.InitDisposition = ProbeInitPolicy.Classify(1, diagnostic);
            Assert.Throws<IOException>(() => WindowsCycleEnvironment.ValidateProbe(row, new ExperimentRequest { Nonce = "n" }, 3, 4));
        }

        [TestCase("Error")] [TestCase("QueryError")] [TestCase("ShutdownError")]
        [TestCase("CleanupError")] [TestCase("FailureStage")]
        public void GlobalUserObservationCannotExcuseAnyOtherFailure(string field)
        {
            var row = GlobalUserUnavailable();
            Assert.That(WindowsCycleEnvironment.ValidateProbe(row, new ExperimentRequest { Nonce = "n" }, 3, 4), Is.False);
            typeof(ProbeObservation).GetField(field).SetValue(row, "failure");
            Assert.Throws<IOException>(() => WindowsCycleEnvironment.ValidateProbe(row, new ExperimentRequest { Nonce = "n" }, 3, 4));
        }

        [TestCase("wire")] [TestCase("disposition")] [TestCase("query")]
        [TestCase("shutdown")] [TestCase("identity")]
        public void GlobalUserObservationStillRequiresConsistentFailedInit(string damage)
        {
            var row = GlobalUserUnavailable();
            if (damage == "wire") row.WireVersion = 2;
            if (damage == "disposition") row.InitDisposition = ProbeInitDisposition.NoSteamClient;
            if (damage == "query") row.QueryCalled = true;
            if (damage == "shutdown") row.ShutdownCalled = true;
            if (damage == "identity") row.SteamId = 2;
            Assert.Throws<IOException>(() => WindowsCycleEnvironment.ValidateProbe(row, new ExperimentRequest { Nonce = "n" }, 3, 4));
        }

        [TestCase(Trial.FullCycle, false)]
        [TestCase(Trial.FullCycle, true)]
        public void GlobalUserObservationUsesOneClientAndOriginalDeadline(Trial trial, bool persistent)
        {
            var fake = new CycleFake(); int observed = 0;
            var expected = new ExperimentRequest { Nonce = "n", AppId = 1, SteamId = 2 };
            fake.Probe = _ =>
            {
                observed++;
                var row = GlobalUserUnavailable();
                if (observed == 1) { row.InitResult = 2; row.InitDisposition = ProbeInitDisposition.NoSteamClient; }
                if (!persistent && observed == 3)
                {
                    row.InitResult = 0; row.InitDisposition = ProbeInitDisposition.Succeeded;
                    row.QueryCalled = row.QueryReturned = row.ShutdownCalled = row.ShutdownReturned = row.LoggedOn = true;
                    row.AppId = 1; row.SteamId = 2;
                }
                return WindowsCycleEnvironment.ValidateProbe(row, expected, 3, 4);
            };
            if (persistent) Assert.Throws<TimeoutException>(() => Cycle.Run(fake, trial));
            else Cycle.Run(fake, trial);
            Assert.That(fake.SteamStarts, Is.EqualTo(1)); Assert.That(fake.Shutdowns, Is.EqualTo(1));
            Assert.That(fake.GameStarts, Is.EqualTo(!persistent && trial == Trial.FullCycle ? 1 : 0));
            Assert.That(fake.LockHeld, Is.False);
            Assert.That(observed, Is.EqualTo(persistent ? 120 : 3));
        }

        private const string ObservedSdkStderr = "Setting breakpad minidump AppID = 123\r\nSteamInternal_SetMinidumpSteamID:  Caching Steam ID:  456 [API loaded no]\r\n";

        [Test]
        public void ObservedSdkDiagnosticRequiresFullReadinessAndExactRequestIdentity()
        {
            var expected = new ExperimentRequest { AppId = 123, SteamId = 456 };
            Assert.That(ProbeStderrPolicy.Validate(ObservedSdkStderr, true, expected), Is.EqualTo("Sdk165InitDiagnostics"));
            Assert.That(ProbeStderrPolicy.Validate("", false, expected), Is.EqualTo("Empty"));
            Assert.Throws<IOException>(() => ProbeStderrPolicy.Validate(ObservedSdkStderr, false, expected));
            expected.SteamId++;
            Assert.Throws<IOException>(() => ProbeStderrPolicy.Validate(ObservedSdkStderr, true, expected));
            expected.SteamId = 456; expected.AppId++;
            Assert.Throws<IOException>(() => ProbeStderrPolicy.Validate(ObservedSdkStderr, true, expected));
        }

        [TestCase("prefix")] [TestCase("suffix")] [TestCase("duplicate")] [TestCase("case")]
        [TestCase("spaces")] [TestCase("lf")] [TestCase("partial")] [TestCase("missing-final-newline")]
        [TestCase("loaded")] [TestCase("whitespace")] [TestCase("null")]
        public void StderrPolicyNeverTrimsFiltersOrIgnoresAdditionalOutput(string damage)
        {
            string text = ObservedSdkStderr;
            if (damage == "prefix") text = "fatal\r\n" + text;
            if (damage == "suffix") text += "fatal\r\n";
            if (damage == "duplicate") text += text;
            if (damage == "case") text = text.ToLowerInvariant();
            if (damage == "spaces") text = text.Replace("  Caching", " Caching");
            if (damage == "lf") text = text.Replace("\r\n", "\n");
            if (damage == "partial") text = text.Substring(0, text.IndexOf("\r\n") + 2);
            if (damage == "missing-final-newline") text = text.TrimEnd();
            if (damage == "loaded") text = text.Replace("loaded no", "loaded yes");
            if (damage == "whitespace") text = " \r\n";
            if (damage == "null") text = null;
            Assert.Throws<IOException>(() => ProbeStderrPolicy.Validate(text, true, new ExperimentRequest { AppId = 123, SteamId = 456 }));
        }

        [Test]
        public void PopupExceptionSummaryPreservesCompleteFailureInLogs()
        {
            var attempt = new ProbeAttempt { Attempt = 2, FailureStage = "Execution", Error = new string('x', 4000), CleanupError = "secondary", ArtifactPath = @"D:\evidence\probe-attempt-002.json" };
            var error = new ProbeAttemptException(attempt);
            Assert.That(error.Message.Length, Is.LessThan(200));
            Assert.That(error.Message, Does.Contain(attempt.ArtifactPath));
            Assert.That(error.InnerException.Message, Does.StartWith(attempt.Error).And.Contain("Cleanup error: secondary"));
        }

        [Test]
        public void ProbeAttemptEvidencePreservesPrimaryAndStreamsWithoutOverwrite()
        {
            string root = Path.Combine(Path.GetTempPath(), "j2m-probe-evidence-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var attempt = new ProbeAttempt { Attempt = 1, FailureStage = "Execution",
                    Error = "Probe abnormal exit: ExitCode=1", ExitConfirmed = true, OwnedExitConfirmed = true,
                    ExitCode = 1, Stdout = "s-not-json", Stderr = "native-primary", OutputComplete = true,
                    ErrorOutputComplete = true, ParseDisposition = "SkippedBecauseProcessOrOutputFailed" };
                ProbeAttemptEvidence.Save(attempt, root);
                Assert.That(File.ReadAllText(Path.Combine(root, "probe-attempt-001.stdout.txt")), Is.EqualTo("s-not-json"));
                Assert.That(File.ReadAllText(Path.Combine(root, "probe-attempt-001.stderr.txt")), Is.EqualTo("native-primary"));
                string json = File.ReadAllText(Path.Combine(root, "probe-attempt-001.json"));
                Assert.That(json, Does.Contain("Probe abnormal exit: ExitCode=1"));
                Assert.That(json, Does.Contain("SkippedBecauseProcessOrOutputFailed"));
                Assert.That(json, Does.Contain("StdoutSha256").And.Contain("StderrSha256"));
                Assert.That(attempt.StdoutSha256, Is.EqualTo(HashText("s-not-json")));
                Assert.That(attempt.StderrSha256, Is.EqualTo(HashText("native-primary")));
                ProbeAttemptEvidence.Save(attempt, root);
                Assert.That(attempt.Attempt, Is.EqualTo(2));
                Assert.That(File.Exists(Path.Combine(root, "probe-attempt-002.json")), Is.True);
                Assert.That(File.ReadAllText(Path.Combine(root, "probe-attempt-001.stdout.txt")), Is.EqualTo("s-not-json"));
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void ConcurrentProbeEvidenceAllocatesDistinctAttempts()
        {
            string root = Path.Combine(Path.GetTempPath(), "j2m-probe-concurrent-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var first = new ProbeAttempt { Attempt = 1, Error = "first", Stdout = "one", Stderr = "first-error" };
                var second = new ProbeAttempt { Attempt = 1, Error = "second", Stdout = "two", Stderr = "second-error" };
                System.Threading.Tasks.Task.WaitAll(
                    System.Threading.Tasks.Task.Run(() => ProbeAttemptEvidence.Save(first, root)),
                    System.Threading.Tasks.Task.Run(() => ProbeAttemptEvidence.Save(second, root)));
                Assert.That(first.Attempt, Is.Not.EqualTo(second.Attempt));
                Assert.That(Directory.GetFiles(root, "probe-attempt-*.json").Length, Is.EqualTo(2));
                Assert.That(Directory.GetFiles(root, "*.reservation").Length, Is.Zero);
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void JsonCommitFailureDoesNotPublishMissingArtifactPath()
        {
            string root = Path.Combine(Path.GetTempPath(), "j2m-probe-partial-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "probe-attempt-001.json"));
            try
            {
                var attempt = new ProbeAttempt { Attempt = 1, FailureStage = "Execution", Error = "exit-primary", Stderr = "native-primary" };
                var failure = Assert.Throws<IOException>(() => ProbeAttemptEvidence.Save(attempt, root));
                attempt.ArtifactError = failure.ToString();
                Assert.That(attempt.ArtifactPath, Is.Null);
                Assert.That(File.Exists(Path.Combine(root, "probe-attempt-001.stderr.txt")), Is.True);
                var reported = new ProbeAttemptException(attempt);
                Assert.That(reported.Message, Does.Contain("partial"));
                Assert.That(reported.InnerException.Message, Does.Contain("exit-primary").And.Contain("Evidence persistence error").And.Contain("native-primary"));
                Assert.That(Directory.GetFiles(root, "*.reservation").Length, Is.Zero);
            }
            finally { Directory.Delete(root, true); }
        }

        private static string HashText(string value)
        {
            using (var hash = System.Security.Cryptography.SHA256.Create())
                return ExperimentFiles.Hex(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value)));
        }

        [Test]
        public void AbnormalChildExitSkipsJsonParsingAndPersistsBothStreams()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) Assert.Ignore("Windows process evidence contract");
            string root = Path.Combine(Path.GetTempPath(), "j2m-probe-exit-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string body = "$null=[Console]::In.ReadLine();[Console]::Out.Write('s-not-json');" +
                    "[Console]::Error.Write('native-primary');exit 1";
                var start = new System.Diagnostics.ProcessStartInfo {
                    FileName = ExperimentFiles.PowerShell,
                    Arguments = "-NoProfile -EncodedCommand " + Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(body)),
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                var expected = new ExperimentRequest { Nonce = "grant", EvidenceDirectory = root, AppId = 1, SteamId = 2 };
                var error = Assert.Throws<ProbeAttemptException>(() => WindowsCycleEnvironment.RunOwnedProbe(start, 5000, expected));
                Assert.That(error.InnerException.Message, Does.Contain("ExitCode=1"));
                string json = File.ReadAllText(Path.Combine(root, "probe-attempt-001.json"));
                Assert.That(json, Does.Contain("SkippedBecauseProcessOrOutputFailed"));
                Assert.That(json, Does.Not.Contain("SerializationException"));
                Assert.That(File.ReadAllText(Path.Combine(root, "probe-attempt-001.stdout.txt")), Is.EqualTo("s-not-json"));
                Assert.That(File.ReadAllText(Path.Combine(root, "probe-attempt-001.stderr.txt")), Is.EqualTo("native-primary"));
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void QueryAndShutdownErrorsDoNotOverwritePrimary()
        {
            var row = new ProbeObservation();
            NativeProbe.ObserveSession(row, () => 0, () => { throw new IOException("query-primary"); },
                () => { throw new IOException("shutdown-secondary"); });
            Assert.That(row.Error, Does.Contain("query-primary"));
            Assert.That(row.QueryError, Does.Contain("query-primary"));
            Assert.That(row.ShutdownError, Does.Contain("shutdown-secondary"));
        }

        [Test]
        public void InitNotReturnedHasNoInventedDescriptionOrShutdown()
        {
            var row = new ProbeObservation();
            Assert.That(NativeProbe.ObserveSession(row, () => { throw new IOException("init did not return"); },
                () => Assert.Fail("query"), () => Assert.Fail("shutdown")), Is.False);
            Assert.That(row.InitReturned, Is.False); Assert.That(row.InitDiagnostic, Is.Null);
            Assert.That(row.ShutdownCalled, Is.False);
        }

        [TestCase("QueryError")] [TestCase("ShutdownError")] [TestCase("CleanupError")] [TestCase("FailureStage")]
        public void ValidLookingReadyResultNeverBypassesSeparateErrors(string field)
        {
            var row = new ProbeObservation { InitDisposition = ProbeInitDisposition.Succeeded, InitCalled = true, InitReturned = true, QueryCalled = true, QueryReturned = true,
                ShutdownCalled = true, ShutdownReturned = true, Nonce = "n", Pid = 3, StartTicks = 4, AppId = 1, SteamId = 2, LoggedOn = true };
            typeof(ProbeObservation).GetField(field).SetValue(row, "failure");
            Assert.Throws<IOException>(() => WindowsCycleEnvironment.ValidateProbe(row, new ExperimentRequest { Nonce = "n", AppId = 1, SteamId = 2 }, 3, 4));
        }

        [TestCase(Trial.FullCycle)]
        public void EarlyAbnormalCommandExitStopsWithoutWaitingForClientTimeout(Trial trial)
        {
            var fake = new CycleFake { SteamStuck = true };
            fake.Inspect = stage => { if (stage == "command") throw new IOException("command exit 7"); };
            var error = Assert.Throws<IOException>(() => Cycle.Run(fake, trial));
            Assert.That(error.Message, Does.Contain("exit 7")); Assert.That(fake.Now, Is.LessThan(60200));
            Assert.That(fake.SteamStarts + fake.GameStarts + fake.Probes, Is.Zero);
        }

        [TestCase(Trial.FullCycle)]
        public void ReplacementWhileOwnedCommandStillAliveStopsWithoutNewCycle(Trial trial)
        {
            var fake = new CycleFake { CommandStuck = true };
            fake.Inspect = stage => { if (stage == "steam-exited") fake.ReplacedSteam = true; };
            Assert.Throws<IOException>(() => Cycle.Run(fake, trial));
            Assert.That(fake.SteamStarts + fake.GameStarts + fake.Probes, Is.Zero);
        }

        [Test]
        public void CleanupAllowanceCannotRestartAfterConsumption()
        {
            long now = 10; var output = new CleanupBudget(() => now, 1000); var kill = new CleanupBudget(() => now, 2000);
            Assert.That(output.Remaining(), Is.EqualTo(1000)); now += 750;
            Assert.That(output.Remaining(), Is.EqualTo(250)); Assert.That(kill.Remaining(), Is.EqualTo(2000));
            now += 500; Assert.That(output.Remaining(), Is.Zero); Assert.That(kill.Remaining(), Is.EqualTo(1500));
            now += 2000; Assert.That(kill.Remaining(), Is.Zero); Assert.That(output.Remaining(), Is.Zero);
        }

        [Test]
        public void ProductCycleShipsOnlyOperationalHelpersAndRejectsStaleFiles()
        {
            var root = Path.Combine(Path.GetTempPath(), "j2m-restart-experiment-" + Guid.NewGuid().ToString("N"));
            try
            {
                RestartExperimentBuildPostprocessor.CopyTools(root);
                Assert.That(File.Exists(Path.Combine(root, "RestartExperiment", "Restart-Experiment.ps1")), Is.True);
                Assert.That(File.Exists(Path.Combine(root, "RestartExperiment", "ObservationV3Wire.cs")), Is.False);
                Assert.That(Directory.GetFiles(Path.Combine(root, "RestartExperiment")).Length, Is.EqualTo(4));
                File.WriteAllText(Path.Combine(root, "RestartExperiment", "stale.json"), "{}");
                Assert.That(File.ReadAllText(Path.Combine(root, "RestartExperiment", "RestartExperiment.cs")),
                    Is.EqualTo(File.ReadAllText("Assets/_Features/Exhibition/Integration/RestartExperiment.cs")));
                Assert.Throws<UnityEditor.Build.BuildFailedException>(() => RestartExperimentBuildPostprocessor.CopyTools(root));

            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void CompletedResetMappingContractMatchesCoordinatorAndFixedV2()
        {
            const string expected = "level-and-efficient-clear-v2";
            Assert.That(ExhibitionResetCoordinator.MappingVersion, Is.EqualTo(expected));
            // Reflection keeps the pre-production RED revision compilable.
            var field = typeof(CompletedResetProductWire).GetField("SupportedMappingVersion");
            Assert.That(field, Is.Not.Null, "The standalone helper must expose its supported mapping contract.");
            Assert.That(field.GetRawConstantValue(), Is.EqualTo(expected));
        }

        [Test]
        public void CompletedResetCurrentMappingIsAccepted()
        {
            WithCompletedRequest((request, path) =>
                Assert.DoesNotThrow(() => CompletedResetProductWire.ValidateRequestPath(request, path)));
        }

        [TestCase("level-clear-v1")]
        [TestCase("unknown-mapping")]
        public void CompletedResetIncompatibleMappingIsRejectedWithoutChangingOrConsumingJournal(string mapping)
        {
            WithCompletedRequest((request, path) =>
            {
                var journal = ExperimentFiles.Read<ProductReadyJournal>(request.ReadyJournalPath);
                journal.MappingVersion = mapping;
                File.WriteAllText(request.ReadyJournalPath, ExperimentFiles.Json(journal));
                // Keep every integrity binding valid so this exercises mapping compatibility itself.
                request.ReadyJournalSha256 = ExperimentFiles.Hash(request.ReadyJournalPath);
                File.WriteAllText(path, ExperimentFiles.Json(request));
                var before = File.ReadAllBytes(request.ReadyJournalPath);
                var failure = Assert.Throws<IOException>(() => CompletedResetProductWire.ValidateRequestPath(request, path));
                Assert.That(failure.Message, Does.Contain("achievement mapping is incompatible"));
                CollectionAssert.AreEqual(before, File.ReadAllBytes(request.ReadyJournalPath));
                Assert.That(File.Exists(path + ".consumed"), Is.False);
            });
        }

        private static void WithCompletedRequest(Action<ExperimentRequest, string> action)
        {
            var root = Path.Combine(Path.GetTempPath(), "j2m-completed-reset-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var operation = Guid.NewGuid().ToString("N");
                var journalPath = Path.Combine(root, "exhibition-reset.json");
                File.WriteAllText(journalPath, ExperimentFiles.Json(new ProductReadyJournal { SchemaVersion = 1,
                    OperationId = operation, State = "Ready", MappingVersion = "level-and-efficient-clear-v2", AppId = 123, SteamId = 456 }));
                journalPath = WindowsIdentityCapture.CanonicalPath(journalPath);
                var request = new ExperimentRequest { CompletedResetProduct = true, Nonce = Guid.NewGuid().ToString("N"),
                    OperationId = operation, Trial = Trial.FullCycle, AppId = 123, SteamId = 456,
                    ReadyJournalPath = journalPath, ReadyJournalSha256 = ExperimentFiles.Hash(journalPath),
                    Steam = new ProcessIdentity { Path = @"C:\Steam\steam.exe" } };
                var requestDirectory = Path.Combine(root, "participant-reset-handoff", operation);
                Directory.CreateDirectory(requestDirectory);
                request.EvidenceDirectory = requestDirectory;
                var requestPath = Path.Combine(requestDirectory, "request.json");
                File.WriteAllText(requestPath, ExperimentFiles.Json(request));
                action(request, requestPath);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void CompletedResetSubmissionUsesSteamAndBindsReadyJournalAndReturnArguments()
        {
            var root = Path.Combine(Path.GetTempPath(), "j2m-completed-reset-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var operation = Guid.NewGuid().ToString("N");
                var journalPath = Path.Combine(root, "exhibition-reset.json");
                File.WriteAllText(journalPath, ExperimentFiles.Json(new ProductReadyJournal { SchemaVersion = 1,
                    OperationId = operation, State = "Ready", MappingVersion = "level-and-efficient-clear-v2", AppId = 123, SteamId = 456 }));
                journalPath = WindowsIdentityCapture.CanonicalPath(journalPath);
                var request = new ExperimentRequest { CompletedResetProduct = true, Nonce = Guid.NewGuid().ToString("N"),
                    OperationId = operation, Trial = Trial.FullCycle, AppId = 123, SteamId = 456,
                    ReadyJournalPath = journalPath, ReadyJournalSha256 = ExperimentFiles.Hash(journalPath),
                    Steam = new ProcessIdentity { Path = @"C:\Steam\steam.exe" } };
                var requestDirectory = Path.Combine(root, "participant-reset-handoff", operation);
                Directory.CreateDirectory(requestDirectory);
                request.EvidenceDirectory = requestDirectory;
                var requestPath = Path.Combine(requestDirectory, "request.json");
                File.WriteAllText(requestPath, ExperimentFiles.Json(request));
                var start = LaunchEnvironment.PrepareCompletedResetSubmission(request, requestPath, () => { }, () => { }, () => { }, () => { });
                Assert.That(start.FileName, Is.EqualTo(request.Steam.Path));
                Assert.That(start.Arguments, Does.StartWith("-applaunch 123 -- -j2mCompletedParticipantReset"));
                Assert.That(start.Arguments, Does.Not.Contain("-j2mPlatformProvider"));
                Assert.That(start.Arguments, Does.Contain("-j2mCompletedParticipantReset"));
                Assert.That(start.Arguments, Does.Contain(ExperimentFiles.Hash(requestPath)));
                File.WriteAllText(journalPath, ExperimentFiles.Json(new ProductReadyJournal { SchemaVersion = 1,
                    OperationId = operation, State = "Pending", MappingVersion = "level-and-efficient-clear-v2", AppId = 123, SteamId = 456 }));
                Assert.Throws<IOException>(() => CompletedResetProductWire.Validate(request));
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
