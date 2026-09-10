using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Game.Exhibition.Integration;
using Game.Exhibition.RestartExperiment;
using NUnit.Framework;

namespace Game.Exhibition.Tests
{
    public sealed class ResetOverlayTrialTests
    {
        private sealed class Effects : IExhibitionResetJournal, IExhibitionSteamReset, IParticipantProgressReset
        {
            public ResetRecord Value = new ResetRecord { State = ResetRecord.Ready, AppId = 123, SteamId = 456,
                OperationId = Guid.NewGuid().ToString("N"), MappingVersion = ExhibitionResetCoordinator.MappingVersion };
            public int Pending, Ready, SteamResets, LocalResets;
            public string FailAt;
            public TaskCompletionSource<bool> ResetGate;
            public Action BeforeJournalRead;
            public ResetRecord Load() { BeforeJournalRead?.Invoke(); return Value.Copy(); }
            public void Save(ResetRecord row)
            {
                if (FailAt == row.State) throw new IOException(row.State);
                if (row.State == ResetRecord.Pending) Pending++; else Ready++;
                Value = row.Copy();
            }
            public ResetIdentity GetIdentity() => new ResetIdentity(123, 456);
            public async Task ResetAsync(ResetIdentity identity) { SteamResets++; if (ResetGate != null) await ResetGate.Task; if (FailAt == "steam") throw new IOException("steam"); }
            public void Reset() { LocalResets++; if (FailAt == "local") throw new IOException("local"); }
        }
        private sealed class Runtime : IResetOverlayTrialRuntime
        {
            public List<string> Calls = new List<string>();
            public HashSet<string> Claims = new HashSet<string>();
            public Action<string> Boundary;
            public string FailAt, Fault;
            public TaskCompletionSource<bool> Waiting, Reporting, Revalidating;
            public HandoffResult Result = HandoffResult.Accepted;
            public bool AcceptedEvidenceFailure;
            public int Helpers, Reports, Quits;
            public bool Available => true;
            public string CoreFailure => Fault;
            public string[] Targets => new[] { "VQ_LEVEL_0_CLEAR" };
            public string BaselineSummary => "VQ_LEVEL_0_CLEAR: earned";
            private void Call(string name) { Calls.Add(name); Boundary?.Invoke(name); if (FailAt == name) throw new IOException(name); }
            public async Task WaitAvailableAsync(Action guard) { Call("wait"); if (Waiting != null) await Waiting.Task; guard(); }
            public Task PrepareAsync(ResetRecord record, Action guard) { Call("prepare"); guard(); return Task.CompletedTask; }
            public async Task RevalidateAsync(Action guard) { Call("revalidate"); if (Revalidating != null) await Revalidating.Task; guard(); }
            public void ValidateRecord(ResetRecord record, bool pending) { Call(pending ? "validate-pending" : "validate-ready"); Assert.That(record.State, Is.EqualTo(pending ? ResetRecord.Pending : ResetRecord.Ready)); }
            public void Claim(string step) { Call("claim:" + step); if (!Claims.Add(step)) throw new IOException("Claim consumed."); }
            public void ConfirmScope() => Call("scope");
            public Task BindPendingAsync(ResetRecord record, Action guard) { Call("bind"); guard(); return Task.CompletedTask; }
            public Task VerifyResetAsync(ResetRecord record, Action guard) { Call("result"); guard(); Assert.That(record.State, Is.EqualTo(ResetRecord.Ready)); return Task.CompletedTask; }
            public async Task SaveReportAsync(string visibility, string[] judgments) { Call("report"); if (Reporting != null) await Reporting.Task; Reports++; }
            public HandoffResult StartHelper(Action guard, Action finalGuard)
            {
                Call("launch-prepare"); guard(); finalGuard(); Helpers++; Call("create");
                if (AcceptedEvidenceFailure) throw new ResetOverlayAcceptedEvidenceException(new IOException("accepted-record"));
                return Result;
            }
            public void Record(string stage) => Call(stage);
            public void Failure(Exception error) => Calls.Add("failure:" + error.Message);
            public Task FinalSnapshotAsync() { Call("final-files"); return Task.CompletedTask; }
            public void Quit() { Quits++; Call("quit"); }
        }
        private Effects effects;
        private Runtime runtime;
        [SetUp] public void Setup() { effects = new Effects(); runtime = new Runtime(); }
        private ResetOverlayTrial Flow(ResetOverlayRole role = ResetOverlayRole.Initiator) => new ResetOverlayTrial(new ExhibitionResetCoordinator(effects, effects, effects), runtime, role);
        private async Task<ResetOverlayTrial> Armed()
        {
            var flow = Flow(); await flow.PrepareMenuAsync(); await flow.ReportAsync("opened", new[] { "earned" }); flow.ScopeConfirmed = true; return flow;
        }
        [Test]
        public async Task TwoRolesOneResetOneGameOnlyAndStoredReportsUntilExit()
        {
            var origin = await Armed(); await origin.RequestResetAsync(); await origin.RequestResetAsync();
            Assert.That(effects.Pending, Is.EqualTo(1)); Assert.That(effects.SteamResets, Is.Zero); Assert.That(runtime.Helpers, Is.EqualTo(1));
            Assert.That(origin.Stage, Is.EqualTo(ResetOverlayStage.HandoffAccepted));
            var worker = Flow(ResetOverlayRole.ResetWorker); await worker.PrepareMenuAsync(); await worker.PrepareMenuAsync(); worker.CompleteMenuInitialization(); worker.Restart();
            Assert.That(worker.Stage, Is.EqualTo(ResetOverlayStage.AwaitingOverlay));
            Assert.That(effects.SteamResets, Is.EqualTo(1)); Assert.That(effects.LocalResets, Is.EqualTo(1)); Assert.That(effects.Ready, Is.EqualTo(1));
            await worker.ReportAsync("opened", new[] { "unearned" }); await worker.ReportAsync("opened", new[] { "earned" });
            Assert.That(runtime.Reports, Is.EqualTo(2)); Assert.That(worker.Stage, Is.EqualTo(ResetOverlayStage.Completed));
            await worker.CloseAsync(); Assert.That(worker.BlocksMenu && worker.HideResetAction && worker.OwnsStatusPresentation, Is.True);
            Assert.That(runtime.Helpers, Is.EqualTo(1)); Assert.That(runtime.Quits, Is.EqualTo(2));
        }
        [Test]
        public async Task ActualReportAndSeparateScopeConfirmationAreBothRequired()
        {
            var f = Flow(); await f.PrepareMenuAsync(); f.ScopeConfirmed = true; await f.RequestResetAsync();
            Assert.That(effects.Pending, Is.Zero); Assert.That(f.ScopeConfirmed, Is.False);
            await f.ReportAsync("opened", new[] { "earned" }); await f.RequestResetAsync(); Assert.That(effects.Pending, Is.Zero);
            f.ScopeConfirmed = true; Assert.That(f.CanRequest, Is.True);
        }
        [TestCase("opened", "unearned")][TestCase("opened", "inconclusive")][TestCase("not-visible", "inconclusive")][TestCase("inconclusive", "inconclusive")]
        public async Task BaselineScreenMismatchPreservesReportWithoutClaimOrPending(string visibility, string judgment)
        {
            var f = Flow(); await f.PrepareMenuAsync(); await f.ReportAsync(visibility, new[] { judgment }); f.ScopeConfirmed = true; await f.RequestResetAsync();
            Assert.That(f.ReportSaved, Is.True); Assert.That(runtime.Claims, Is.Empty); Assert.That(effects.Pending, Is.Zero); Assert.That(runtime.Helpers, Is.Zero);
        }
        [TestCase("prepare")][TestCase("report")][TestCase("scope")][TestCase("revalidate")][TestCase("claim:initial-request")][TestCase("validate-ready")]
        public async Task PrerequisiteOrRequiredRecordFailureNeverWritesPending(string step)
        {
            runtime.FailAt = step; var f = await Armed(); await f.RequestResetAsync();
            Assert.That(effects.Pending + effects.SteamResets + effects.LocalResets + runtime.Helpers, Is.Zero);
            Assert.That(f.Error, Is.Not.Null);
        }
        [TestCase("bind")][TestCase("launch-prepare")]
        public async Task FailureAfterPendingRetainsPendingWithoutHelper(string step)
        {
            var f = await Armed(); runtime.FailAt = step; await f.RequestResetAsync(); await f.RequestResetAsync(); f.Restart();
            Assert.That(effects.Pending, Is.EqualTo(1)); Assert.That(effects.Value.State, Is.EqualTo(ResetRecord.Pending)); Assert.That(runtime.Helpers, Is.Zero);
        }
        [TestCase("prepare")][TestCase("revalidate")][TestCase("claim:initial-request")][TestCase("validate-ready")][TestCase("launch-prepare")]
        public async Task CancelAfterCostClaimOrAtCreationBoundaryStopsFurtherWrites(string step)
        {
            var f = Flow(); if (step != "prepare") { await f.PrepareMenuAsync(); await f.ReportAsync("opened", new[] { "earned" }); f.ScopeConfirmed = true; }
            runtime.Boundary = name => { if (name == step) f.Cancel(); };
            if (step == "prepare") await f.PrepareMenuAsync(); else await f.RequestResetAsync();
            await f.CloseAsync();
            Assert.That(runtime.Helpers, Is.Zero);
            Assert.That(effects.Pending, Is.EqualTo(step == "launch-prepare" ? 1 : 0));
            if (step == "claim:initial-request") Assert.That(runtime.Claims, Does.Contain("initial-request"));
        }
        [Test]
        public async Task SlowReportBlocksDuplicateAndResetAndKeepsSavedStatementAfterClose()
        {
            var f = Flow(); await f.PrepareMenuAsync(); runtime.Reporting = new TaskCompletionSource<bool>();
            var report = f.ReportAsync("opened", new[] { "earned" }); await f.ReportAsync("opened", new[] { "earned" });
            f.ScopeConfirmed = true; await f.RequestResetAsync(); var close = f.CloseAsync();
            Assert.That(close.IsCompleted, Is.False); Assert.That(runtime.Quits, Is.Zero);
            runtime.Reporting.SetResult(true); await report; await close;
            Assert.That(f.ReportSaved, Is.True); Assert.That(runtime.Reports, Is.EqualTo(1)); Assert.That(effects.Pending + runtime.Helpers, Is.Zero); Assert.That(runtime.Quits, Is.EqualTo(1));
        }
        [Test]
        public async Task SlowRevalidationCloseHasNoLateClaimOrPending()
        {
            var f = await Armed(); runtime.Revalidating = new TaskCompletionSource<bool>(); var task = f.RequestResetAsync(); var close = f.CloseAsync();
            runtime.Revalidating.SetResult(true); await task; await close;
            Assert.That(runtime.Claims, Is.Empty); Assert.That(effects.Pending + runtime.Helpers, Is.Zero);
        }
        [Test]
        public async Task QuitDuringNativePreparationHasNoLatePreparation()
        {
            var f = Flow(); runtime.Waiting = new TaskCompletionSource<bool>(); var task = f.PrepareMenuAsync(); var close = f.CloseAsync();
            runtime.Waiting.SetResult(true); await task; await close;
            Assert.That(runtime.Calls, Does.Not.Contain("prepare")); Assert.That(effects.Pending, Is.Zero);
        }
        [Test]
        public async Task CancelFromResettingNotificationPreventsFirstDestructiveCall()
        {
            effects.Value.State = ResetRecord.Pending; var f = Flow(ResetOverlayRole.ResetWorker);
            f.Changed += () => { if (f.Stage == ResetOverlayStage.Resetting) f.Cancel(); };
            await f.PrepareMenuAsync(); await f.CloseAsync(); Assert.That(effects.SteamResets + effects.LocalResets, Is.Zero);
        }
        [Test]
        public async Task CloseDuringStartedWorkerFinishesOneResetAndRequiredResultBeforeQuit()
        {
            effects.Value.State = ResetRecord.Pending; effects.ResetGate = new TaskCompletionSource<bool>();
            var f = Flow(ResetOverlayRole.ResetWorker); var task = f.PrepareMenuAsync(); var close = f.CloseAsync();
            Assert.That(runtime.Quits, Is.Zero); effects.ResetGate.SetResult(true); await task; await close;
            Assert.That(effects.SteamResets, Is.EqualTo(1)); Assert.That(effects.LocalResets, Is.EqualTo(1));
            Assert.That(runtime.Calls.IndexOf("result"), Is.LessThan(runtime.Calls.IndexOf("quit"))); Assert.That(f.CanReport, Is.False);
        }
        [TestCase("steam")][TestCase("local")][TestCase("Ready")]
        public async Task PartialResetDoesNotResumeServicesOrReplay(string step)
        {
            effects.Value.State = ResetRecord.Pending; effects.FailAt = step; var f = Flow(ResetOverlayRole.ResetWorker);
            await f.PrepareMenuAsync(); await f.PrepareMenuAsync(); f.Restart(); await f.RequestResetAsync();
            Assert.That(f.Stage, Is.EqualTo(ResetOverlayStage.Failed)); Assert.That(effects.Value.State, Is.EqualTo(ResetRecord.Pending)); Assert.That(runtime.Helpers, Is.Zero);
        }
        [TestCase(HandoffResult.NotStarted)][TestCase(HandoffResult.Unknown)]
        public async Task UnconfirmedCreationConsumesClaimAndNeverRetries(HandoffResult result)
        {
            var f = await Armed(); runtime.Result = result; await f.RequestResetAsync(); await f.RequestResetAsync();
            Assert.That(runtime.Helpers, Is.EqualTo(1)); Assert.That(effects.Pending, Is.EqualTo(1)); Assert.That(f.CanRequest, Is.False);
        }
        [Test]
        public async Task AcceptedEvidenceFailureStillClosesAndPreservesFirstError()
        {
            var f = await Armed(); runtime.AcceptedEvidenceFailure = true; runtime.FailAt = "final-files"; await f.RequestResetAsync();
            Assert.That(f.Stage, Is.EqualTo(ResetOverlayStage.HandoffAccepted)); Assert.That(runtime.Quits, Is.EqualTo(1)); Assert.That(f.Error, Does.Contain("accepted-record"));
        }
        [Test]
        public async Task CallbackFailureAfterResetAwaitPreventsLocalWrite()
        {
            effects.Value.State = ResetRecord.Pending; effects.ResetGate = new TaskCompletionSource<bool>(); var f = Flow(ResetOverlayRole.ResetWorker);
            var task = f.PrepareMenuAsync(); runtime.Fault = "callback-fault"; effects.ResetGate.SetResult(true); await task;
            Assert.That(effects.LocalResets, Is.Zero); Assert.That(effects.Value.State, Is.EqualTo(ResetRecord.Pending)); Assert.That(f.Error, Does.Contain("callback-fault"));
        }
        [Test]
        public async Task FinalObserverIsRejectedAndHasNoPrepareOrResetPath()
        {
            var f = Flow(ResetOverlayRole.FinalObserver); await f.PrepareMenuAsync(); await f.RequestResetAsync(); f.CompleteMenuInitialization();
            Assert.That(f.Stage, Is.EqualTo(ResetOverlayStage.Failed)); Assert.That(runtime.Calls, Has.None.EqualTo("wait")); Assert.That(effects.Pending, Is.Zero);
        }
        [TestCase(1)][TestCase(3)][TestCase(5)]
        public void BaselineRecordsExistingValuesAndRejectsDrift(int count)
        {
            var values = Enumerable.Range(0, 5).Select(i => i < count).ToArray(); int records = 0;
            var baseline = new ResetOverlayBaseline(() => (bool[])values.Clone(), _ => records++);
            baseline.Prepare(); baseline.Verify(); values[0] = false;
            Assert.Throws<InvalidOperationException>(baseline.Verify); Assert.That(records, Is.EqualTo(1));
        }
        [TestCase("empty")][TestCase("query")][TestCase("record")]
        public void BaselineFailureCannotBeArmed(string failure)
        {
            var b = new ResetOverlayBaseline(() => failure == "query" ? throw new IOException("query") : new[] { failure != "empty", false, false, false, false },
                _ => { if (failure == "record") throw new IOException("record"); });
            Assert.That(() => b.Prepare(), Throws.Exception); Assert.That(() => b.Verify(), Throws.Exception);
        }
        [TestCase("operation")][TestCase("account")][TestCase("mapping")][TestCase("state")]
        public void ReadyIdentityRemainsPinned(string changed)
        {
            var pin = new ResetOverlayInitialIdentityPin(); pin.Validate(effects.Value, effects.GetIdentity());
            if (changed == "operation") effects.Value.OperationId = Guid.NewGuid().ToString("N");
            if (changed == "account") effects.Value.SteamId++;
            if (changed == "mapping") effects.Value.MappingVersion = "changed";
            if (changed == "state") effects.Value.State = ResetRecord.Pending;
            Assert.Throws<InvalidOperationException>(() => pin.Validate(effects.Value, effects.GetIdentity()));
        }
        [Test]
        public async Task AvailabilityWaitChecksCancellationAfterPollCostAndPreservesDeadline()
        {
            long now = 0; bool cancelled = false;
            Assert.CatchAsync<OperationCanceledException>(async () => await ResetOverlayStartupWait.Run(() => { cancelled = true; return true; }, () => true,
                () => now, ms => Task.CompletedTask, () => { if (cancelled) throw new OperationCanceledException(); }));
            await ResetOverlayStartupWait.Run(() => true, () => now >= 100, () => now, ms => { now += ms; return Task.CompletedTask; });
            Assert.That(now, Is.EqualTo(100));
            Assert.ThrowsAsync<TimeoutException>(async () => await ResetOverlayStartupWait.Run(() => { now += 30000; return true; }, () => true, () => now, ms => Task.CompletedTask));
        }
        [TestCase("none")] [TestCase("subset")] [TestCase("extra")] [TestCase("hash")]
        [TestCase("duplicate")] [TestCase("count")] [TestCase("traversal")]
        public void PayloadVerificationRequiresCompleteUnchangedInstalledSet(string damage)
        {
            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var entries = new List<TrialPayloadFile>();
                foreach (var name in new[] { "VectorQuake.exe", "Exhibition-Relaunch.ps1", "VectorQuake_Data/Managed/Game.Exhibition.Integration.dll",
                    "RestartExperiment/RestartExperiment.cs", "RestartExperiment/RestartExperimentWindows.cs", "RestartExperiment/RestartExperimentNativeProbe.cs", "RestartExperiment/Restart-Experiment.ps1" })
                {
                    var path = Path.Combine(root, name); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, "fixture");
                    entries.Add(new TrialPayloadFile { relativePath = name, sha256 = ExperimentFiles.Hash(path) });
                }
                if (damage == "subset") entries.RemoveAt(0);
                if (damage == "extra") File.WriteAllText(Path.Combine(root, "extra"), "old");
                if (damage == "hash") entries[0].sha256 = "wrong";
                if (damage == "duplicate") entries.Add(entries[0]);
                if (damage == "traversal") entries[0].relativePath = "../outside";
                var manifest = new TrialPayloadManifest { files = entries.ToArray(), fileCount = entries.Count + (damage == "count" ? 1 : 0) };
                if (damage == "none") Assert.DoesNotThrow(() => ResetOverlayTrialFiles.VerifyPayload(root, manifest));
                else Assert.Throws<IOException>(() => ResetOverlayTrialFiles.VerifyPayload(root, manifest));
            }
            finally { Directory.Delete(root, true); }
        }


        [Test]
        public void DurableClaimDoesNotRecoverPartialFile()
        {
            var path = Path.GetTempFileName();
            try { Assert.Throws<IOException>(() => ResetOverlayTrialFiles.Create(path, new { Step = "initial-request" })); }
            finally { File.Delete(path); }
        }
        [TestCase(false)][TestCase(true)]
        public void TrialArgumentPresenceNeverFallsBackWhenUnsupportedOrMalformed(bool enabled)
        {
            foreach (var args in new[] { new[] { "-j2mResetOverlayTrial" }, new[] { "-j2mResetOverlayTrial=bad" }, new[] { "-J2MRESETOVERLAYTRIAL", "config" } })
                Assert.That(ResetOverlayTrialOptions.Parse(args, enabled)?.Error, Is.Not.Null);
            Assert.That(ResetOverlayTrialOptions.Parse(new[] { "game" }, enabled), Is.Null);
        }
        [TestCase("-j2mResetOverlayTrial", "again")][TestCase("-j2mPlatformProvider", "steam")][TestCase("-j2mRestartExperiment", "FullCycle")]
        [TestCase("-j2mSteamSmoke", "true")][TestCase("-j2mResetOverlayPhase", "FinalObserver")]
        public void DuplicateOrMixedOptionsFail(string key, string value)
        {
            Assert.That(ResetOverlayTrialOptions.Parse(new[] { "-j2mPlatformProvider", "steam", "-j2mResetOverlayTrial", "config", key, value }, true).Error, Is.Not.Null);
        }
    }
}
