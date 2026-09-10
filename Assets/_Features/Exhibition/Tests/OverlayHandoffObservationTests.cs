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
    public sealed class OverlayHandoffObservationTests
    {
        internal sealed class Runtime : IOverlayHandoffObservationRuntime
        {
            public double Clock, NativePollCost;
            public int Helpers, Claims, Quits, FinalReads, Prepares, Reports, Validations;
            public bool Native = true, Receipt = true;
            public string Failure;
            public Func<string> ReadFailure;
            public Exception NativeError, AcceptedEvidenceError, ReportError, ValidationError, FinalError;
            public HandoffResult Result = HandoffResult.Accepted;
            public Action OnDelay, OnClaim, OnLaunchPrepared;
            public TaskCompletionSource<bool> PendingReport, PendingValidation, PendingPrepare;
            public readonly List<string> Records = new List<string>();
            public double Now => Clock;
            public bool NativeReady() { Clock += NativePollCost; if (NativeError != null) throw NativeError; return Native; }
            public bool ReceiptReady() => Receipt;
            public async Task PrepareAsync(Action guard) { Prepares++; if (PendingPrepare != null) await PendingPrepare.Task; guard(); }
            public string ObservationFailure => ReadFailure?.Invoke() ?? Failure;
            public async Task RevalidateAsync(Action guard)
            { Validations++; if (PendingValidation != null) await PendingValidation.Task; guard(); if (ValidationError != null) throw ValidationError; }
            public void Claim() { Claims++; OnClaim?.Invoke(); }
            public HandoffResult StartHelper(Action guard)
            { OnLaunchPrepared?.Invoke(); guard(); Helpers++;
                if (AcceptedEvidenceError != null) throw new OverlayHandoffAcceptedEvidenceException(AcceptedEvidenceError);
                return Result; }
            public void Record(string stage, object value = null) => Records.Add(stage);
            public async Task SaveObservationAsync(OverlayVisibility visibility)
            { Reports++; if (PendingReport != null) await PendingReport.Task; if (ReportError != null) throw ReportError; }
            public Task DelayAsync() { Clock += 1; OnDelay?.Invoke(); return Task.CompletedTask; }
            public void FinalSnapshot() { FinalReads++; if (FinalError != null) throw FinalError; }
            public void Quit() => Quits++;
        }

        private static async Task<OverlayHandoffObservation> Origin(Runtime runtime)
        {
            var flow = new OverlayHandoffObservation(runtime, OverlayObservationRole.OriginObserver);
            await flow.PrepareMenuAsync();
            await flow.ReportAsync(OverlayVisibility.Opened);
            return flow;
        }

        [Test, Category("Integration")]
        public async Task OriginOpenedCreatesExactlyOneHelperAndNormalExitWithoutResetCapability()
        {
            var runtime = new Runtime(); var flow = await Origin(runtime);
            Assert.That(flow.CanReplace, Is.True);
            await flow.ReplaceAsync(); await flow.ReplaceAsync(); flow.RequestReset(); flow.Restart();
            Assert.That(runtime.Claims, Is.EqualTo(1)); Assert.That(runtime.Helpers, Is.EqualTo(1));
            Assert.That(runtime.Quits, Is.EqualTo(1)); Assert.That(runtime.Reports, Is.EqualTo(1));
            Assert.That(flow.Stage, Is.EqualTo(OverlayObservationStage.HandoffAccepted));
            Assert.That(flow.BlocksMenu && flow.SuppressSaveSeedImport && flow.OwnsStatusPresentation, Is.True);
        }

        [TestCase(OverlayVisibility.NotVisible), TestCase(OverlayVisibility.Inconclusive), Category("Integration")]
        public async Task OriginWithoutOpenedCannotReplace(OverlayVisibility report)
        {
            var runtime = new Runtime(); var flow = new OverlayHandoffObservation(runtime, OverlayObservationRole.OriginObserver);
            await flow.PrepareMenuAsync(); await flow.ReplaceAsync(); Assert.That(runtime.Helpers, Is.Zero);
            await flow.ReportAsync(report); await flow.ReplaceAsync();
            Assert.That(runtime.Helpers + runtime.Claims, Is.Zero); Assert.That(flow.Visibility, Is.EqualTo(report));
        }

        [TestCase(false), TestCase(true), Category("Integration")]
        public async Task SlowReportBlocksDuplicatesAndHandoffAndPreservesStoredStatementAfterCancel(bool cancel)
        {
            var runtime = new Runtime { PendingReport = new TaskCompletionSource<bool>() };
            var flow = new OverlayHandoffObservation(runtime, OverlayObservationRole.OriginObserver);
            await flow.PrepareMenuAsync(); var report = flow.ReportAsync(OverlayVisibility.Opened);
            await flow.ReportAsync(OverlayVisibility.NotVisible); await flow.ReplaceAsync();
            Assert.That(runtime.Reports, Is.EqualTo(1)); Assert.That(runtime.Helpers, Is.Zero);
            if (cancel) await flow.CloseAsync();
            runtime.PendingReport.SetResult(true); await report;
            Assert.That(flow.Visibility, Is.EqualTo(OverlayVisibility.Opened));
            Assert.That(flow.CanReplace, Is.EqualTo(!cancel));
            if (cancel) Assert.That(flow.Stage, Is.EqualTo(OverlayObservationStage.Cancelled));
        }

        [Test, Category("Integration")]
        public async Task FailedCoreReportNeverEnablesReplacement()
        {
            var runtime = new Runtime { ReportError = new IOException("report disk failure") };
            var flow = await Origin(runtime); await flow.ReplaceAsync();
            Assert.That(flow.Visibility, Is.Null); Assert.That(runtime.Helpers + runtime.Claims, Is.Zero);
            Assert.That(flow.Error, Does.Contain("report disk failure"));
        }

        [TestCase(0), TestCase(1), TestCase(2), Category("Integration")]
        public async Task CancellationDuringRevalidationAfterClaimOrAtHelperBoundaryNeverLaunches(int boundary)
        {
            var runtime = new Runtime(); var flow = await Origin(runtime);
            if (boundary == 0) runtime.PendingValidation = new TaskCompletionSource<bool>();
            if (boundary == 1) runtime.OnClaim = () => _ = flow.CloseAsync();
            if (boundary == 2) runtime.OnLaunchPrepared = () => _ = flow.CloseAsync();
            var replacing = flow.ReplaceAsync();
            if (boundary == 0) { await flow.CloseAsync(); runtime.PendingValidation.SetResult(true); }
            await replacing; await flow.ReplaceAsync();
            Assert.That(runtime.Helpers, Is.Zero); Assert.That(runtime.Claims, Is.EqualTo(boundary == 0 ? 0 : 1));
            Assert.That(flow.Stage, Is.EqualTo(OverlayObservationStage.Cancelled));
        }

        [TestCase("account"), TestCase("Ready"), TestCase("save"), TestCase("payload"), TestCase("client"), Category("Integration")]
        public async Task LongHumanWaitStillRevalidatesAndRejectsChangedPins(string pin)
        {
            var runtime = new Runtime(); var flow = await Origin(runtime);
            runtime.Clock = 3600; runtime.ValidationError = new IOException(pin + " changed");
            await flow.ReplaceAsync(); Assert.That(runtime.Validations, Is.EqualTo(1));
            Assert.That(runtime.Helpers + runtime.Claims, Is.Zero); Assert.That(flow.Error, Does.Contain(pin));
        }

        [Test, Category("Integration")]
        public async Task HumanWaitBeyondFiveMinutesAllowsValidatedHandoff()
        {
            var runtime = new Runtime(); var flow = await Origin(runtime);
            runtime.Clock = 3600; flow.Tick(); await flow.ReplaceAsync();
            Assert.That(runtime.Validations, Is.EqualTo(1)); Assert.That(runtime.Helpers, Is.EqualTo(1));
        }

        [TestCase(false), TestCase(true), Category("Integration")]
        public async Task NativeOrReceiptWaitHasExactThirtySecondLimit(bool child)
        {
            var runtime = new Runtime { Native = child, Receipt = false };
            var flow = new OverlayHandoffObservation(runtime, child ? OverlayObservationRole.ReplacementObserver : OverlayObservationRole.OriginObserver);
            await flow.PrepareMenuAsync();
            Assert.That(runtime.Clock, Is.EqualTo(30)); Assert.That(flow.Error, Does.Contain(child ? "ReceiptTimeout" : "SdkTimeout"));
            Assert.That(runtime.Prepares, Is.Zero);
        }

        [Test, Category("Integration")]
        public async Task SuccessfulSlowSdkPollAtDeadlineStillTimesOut()
        {
            var runtime = new Runtime { NativePollCost = 30 };
            var flow = new OverlayHandoffObservation(runtime, OverlayObservationRole.OriginObserver);
            await flow.PrepareMenuAsync(); Assert.That(flow.Error, Does.Contain("SdkTimeout"));
        }

        [Test, Category("Integration")]
        public async Task DefiniteNativeFailureIsImmediate()
        {
            var runtime = new Runtime { NativeError = new IOException("native-init-returned-false") };
            var flow = new OverlayHandoffObservation(runtime, OverlayObservationRole.OriginObserver);
            await flow.PrepareMenuAsync();
            Assert.That(runtime.Clock, Is.Zero); Assert.That(flow.Error, Does.Contain("native-init-returned-false"));
        }

        [Test, Category("Integration")]
        public async Task QuitDuringPreparationRejectsLateContinuation()
        {
            var runtime = new Runtime { PendingPrepare = new TaskCompletionSource<bool>() };
            var flow = new OverlayHandoffObservation(runtime, OverlayObservationRole.OriginObserver);
            var preparing = flow.PrepareMenuAsync(); await flow.CloseAsync();
            runtime.PendingPrepare.SetResult(true); await preparing;
            Assert.That(flow.Stage, Is.EqualTo(OverlayObservationStage.Cancelled));
            Assert.That(runtime.Helpers, Is.Zero); Assert.That(runtime.Quits, Is.EqualTo(1)); Assert.That(flow.CanReport, Is.False);
        }

        [TestCase(false), TestCase(true), Category("Integration")]
        public async Task QuitDuringNativeOrReceiptWaitStopsPolling(bool child)
        {
            var runtime = new Runtime { Native = child, Receipt = false };
            var flow = new OverlayHandoffObservation(runtime, child ? OverlayObservationRole.ReplacementObserver : OverlayObservationRole.OriginObserver);
            runtime.OnDelay = () => _ = flow.CloseAsync();
            await flow.PrepareMenuAsync(); Assert.That(runtime.Clock, Is.EqualTo(1)); Assert.That(runtime.Prepares, Is.Zero);
        }

        [TestCase(HandoffResult.Unknown), TestCase(HandoffResult.NotStarted), Category("Integration")]
        public async Task ConsumedClaimNeverRetriesEvenWhenCreationNotConfirmed(HandoffResult result)
        {
            var runtime = new Runtime { Result = result }; var flow = await Origin(runtime);
            await flow.ReplaceAsync(); await flow.ReplaceAsync();
            Assert.That(runtime.Helpers, Is.EqualTo(1)); Assert.That(runtime.Claims, Is.EqualTo(1)); Assert.That(runtime.Quits, Is.Zero);
        }

        [Test, Category("Integration")]
        public async Task AcceptedHelperEvidenceFailureStillExitsAndKeepsFirstCoreError()
        {
            var runtime = new Runtime { AcceptedEvidenceError = new IOException("creation evidence failure"), FinalError = new IOException("final failure") };
            var flow = await Origin(runtime); await flow.ReplaceAsync(); await flow.CloseAsync(); await flow.ReplaceAsync();
            Assert.That(flow.Stage, Is.EqualTo(OverlayObservationStage.HandoffAccepted));
            Assert.That(flow.Error, Does.Contain("creation evidence failure"));
            Assert.That(runtime.Helpers, Is.EqualTo(1)); Assert.That(runtime.Quits, Is.EqualTo(1));
            Assert.That(runtime.Records, Does.Contain("ExitRequested"));
        }

        [TestCase(OverlayVisibility.Opened), TestCase(OverlayVisibility.NotVisible), TestCase(OverlayVisibility.Inconclusive), Category("Integration")]
        public async Task ChildReportRetainsUserVisibilityWithoutApiOrCapture(OverlayVisibility visibility)
        {
            var runtime = new Runtime(); var flow = new OverlayHandoffObservation(runtime, OverlayObservationRole.ReplacementObserver);
            await flow.PrepareMenuAsync(); await flow.ReportAsync(visibility); await flow.ReportAsync(OverlayVisibility.Opened);
            Assert.That(flow.Visibility, Is.EqualTo(visibility)); Assert.That(runtime.Reports, Is.EqualTo(1));
            Assert.That(flow.Stage, Is.EqualTo(OverlayObservationStage.Completed)); Assert.That(runtime.Helpers, Is.Zero);
        }

        [Test, Category("Integration")]
        public async Task RuntimeCoreFailureBlocksHandoff()
        {
            var runtime = new Runtime(); var flow = await Origin(runtime); runtime.Failure = "core record failed";
            await flow.ReplaceAsync(); Assert.That(runtime.Helpers, Is.Zero); Assert.That(flow.Error, Does.Contain(runtime.Failure));
        }

        [TestCase("-j2mResetOverlayTrial"), TestCase("-j2mRestartExperiment"), TestCase("-j2mRestartObservation"),
         TestCase("-j2mSteamSmoke"), TestCase("-j2mSteamAchievementSmoke"), Category("Integration")]
        public void MixedOptionsAreRecognizedAndRejected(string conflict)
        {
            var args = new[] { "game", OverlayHandoffObservationOptions.Initial, "config", "-j2mPlatformProvider", "steam", conflict, "value" };
            Assert.That(OverlayHandoffObservationOptions.Present(args), Is.True);
            Assert.That(OverlayHandoffObservationOptions.Parse(args, true).Error, Is.Not.Null);
        }

        [Test, Category("Integration")]
        public void OptionsRequireOneEffectiveProviderAndExactRole()
        {
            var args = new[] { "game", OverlayHandoffObservationOptions.Initial, "config", "-j2mPlatformProvider", "steam" };
            Assert.That(OverlayHandoffObservationOptions.Parse(args, true).Error, Is.Null);
            Assert.That(OverlayHandoffObservationOptions.Parse(args, false).Error, Is.Not.Null);
            Assert.That(OverlayHandoffObservationOptions.Parse(args.Concat(new[] { "-j2mPlatformProvider=steam" }).ToArray(), true).Error, Is.Not.Null);
            Assert.That(OverlayHandoffObservationOptions.Parse(args.Concat(new[] { OverlayHandoffObservationOptions.Initial, "config" }).ToArray(), true).Error, Is.Not.Null);
            Assert.That(OverlayHandoffObservationOptions.Parse(new[] { OverlayHandoffObservationOptions.Context, "child" }, true).Error, Is.Not.Null);
        }
    }
}
