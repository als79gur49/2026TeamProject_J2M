using System;
using System.Threading.Tasks;
using Game.Exhibition.RestartExperiment;
using Game.Feature.UI.Application;

namespace Game.Exhibition.Integration
{
    // Pin the Ready participant operation before exposing a baseline for user confirmation.
    public sealed class ResetOverlayInitialIdentityPin
    {
        private ResetRecord initial;
        public void Validate(ResetRecord record, ResetIdentity identity)
        {
            if (record == null || record.State != ResetRecord.Ready ||
                record.AppId != identity.AppId || record.SteamId != identity.SteamId ||
                record.MappingVersion != ExhibitionResetCoordinator.MappingVersion ||
                !Guid.TryParseExact(record.OperationId, "N", out _))
                throw new InvalidOperationException("Invalid Ready baseline journal/account.");
            if (initial == null) { initial = record.Copy(); return; }
            if (initial.OperationId != record.OperationId || initial.AppId != record.AppId ||
                initial.SteamId != record.SteamId || initial.MappingVersion != record.MappingVersion ||
                initial.State != record.State)
                throw new InvalidOperationException("Ready journal/account changed after baseline preparation.");
        }
    }

    // The same read/record/compare boundary is used by the native runtime and fake account tests.
    public sealed class ResetOverlayBaseline
    {
        private readonly Func<bool[]> read;
        private readonly Action<bool[]> record;
        private bool[] values;
        public ResetOverlayBaseline(Func<bool[]> read, Action<bool[]> record) { this.read = read; this.record = record; }
        public bool[] Prepare()
        {
            if (values != null) throw new InvalidOperationException("Baseline preparation already consumed.");
            var observed = read();
            if (observed == null || observed.Length != 5) throw new InvalidOperationException("Five mapped achievement observations required.");
            record(observed);
            if (!Array.Exists(observed, value => value)) throw new InvalidOperationException("No earned mapped achievement to compare; no reset was requested.");
            values = (bool[])observed.Clone();
            return (bool[])values.Clone();
        }
        public void Verify()
        {
            var observed = read();
            if (values == null || observed == null || observed.Length != values.Length)
                throw new InvalidOperationException("Achievement baseline unavailable.");
            for (int i = 0; i < values.Length; i++)
                if (values[i] != observed[i]) throw new InvalidOperationException("Achievement baseline changed; trial stopped before reset.");
        }
    }

    public static class ResetOverlayStartupWait
    {
        public static async Task Run(Func<bool> available, Func<bool> receipt, Func<long> clock, Func<int, Task> delay, Action guard = null)
        {
            long expires = clock() + 30000;
            while (true)
            {
                guard?.Invoke();
                bool ready = available(); guard?.Invoke(); bool owned = receipt(); guard?.Invoke();
                long remaining = expires - clock();
                if (remaining <= 0) throw new TimeoutException("Trial child receipt/Steam readiness exceeded 30 seconds.");
                if (ready && owned) return;
                await delay((int)Math.Min(50, remaining)); guard?.Invoke();
            }
        }
    }

    public enum ResetOverlayStage { Initial, Preparing, Revalidating, Resetting, AwaitingOverlay, Reporting,
        Launching, HandoffAccepted, HandoffUncertain, Completed, Failed, Exiting, StartupAlreadyStarted }

    public sealed class ResetOverlayAcceptedEvidenceException : Exception
    {
        public ResetOverlayAcceptedEvidenceException(Exception inner) : base("Helper accepted; creation evidence failed.", inner) { }
    }

    public interface IResetOverlayTrialRuntime
    {
        bool Available { get; }
        string CoreFailure { get; }
        string[] Targets { get; }
        string BaselineSummary { get; }
        Task WaitAvailableAsync(Action guard);
        Task PrepareAsync(ResetRecord record, Action guard);
        Task RevalidateAsync(Action guard);
        void ValidateRecord(ResetRecord record, bool pending);
        void Claim(string step);
        void ConfirmScope();
        Task BindPendingAsync(ResetRecord record, Action guard);
        Task VerifyResetAsync(ResetRecord record, Action guard);
        Task SaveReportAsync(string visibility, string[] judgments);
        HandoffResult StartHelper(Action guard, Action finalGuard);
        void Record(string stage);
        void Failure(Exception error);
        Task FinalSnapshotAsync();
        void Quit();
    }

    /// <summary>Two roles, one reset, one GameOnly handoff. No service-resume or FullCycle capability.</summary>
    public sealed class ResetOverlayTrial : IParticipantResetPort, IParticipantResetActionPresentation, IParticipantResetStatusPresentation
    {
        private readonly ExhibitionResetCoordinator coordinator;
        private readonly IResetOverlayTrialRuntime runtime;
        private bool prepared, ready, reportAttempted, reportSaved, scopeConfirmed, handoffAttempted;
        private bool working, resetInFlight, exitRequested, finalized;
        private TaskCompletionSource<bool> idle;
        private Task closeTask;
        public ResetOverlayRole Role { get; }
        public ResetOverlayStage Stage { get; private set; } = ResetOverlayStage.Initial;
        public string Error { get; private set; }
        public event Action Changed;
        public bool HasRuntime => runtime != null;
        public bool AllowApplicationQuit { get; private set; }
        public bool BlocksMenu => true;
        public bool HideResetAction => true;
        public bool OwnsStatusPresentation => true;
        public bool SuppressSaveSeedImport => true;
        public bool IsBusy => working;
        public bool ExitRequested => exitRequested;
        public bool BaselineReady => ready;
        public bool ReportSaved => reportSaved;
        public string BaselineSummary => runtime?.BaselineSummary;
        public string[] Targets => runtime?.Targets ?? Array.Empty<string>();
        public bool CanReport => ready && !working && !reportAttempted && !exitRequested && Error == null &&
            (Stage == ResetOverlayStage.Initial || Stage == ResetOverlayStage.AwaitingOverlay);
        public bool CanRequest => Role == ResetOverlayRole.Initiator && Stage == ResetOverlayStage.Initial &&
            reportSaved && scopeConfirmed && !working && !handoffAttempted && !exitRequested && Error == null && runtime.Available;
        public bool ScopeConfirmed
        {
            get => scopeConfirmed;
            set { if (Role == ResetOverlayRole.Initiator && reportSaved && Stage == ResetOverlayStage.Initial && !working && !exitRequested) { scopeConfirmed = value; Notify(); } }
        }
        private bool Terminal => Stage == ResetOverlayStage.Failed || Stage == ResetOverlayStage.StartupAlreadyStarted ||
            Stage == ResetOverlayStage.Exiting || Stage == ResetOverlayStage.Completed || Stage == ResetOverlayStage.HandoffAccepted || Stage == ResetOverlayStage.HandoffUncertain;

        public ResetOverlayTrial(ExhibitionResetCoordinator coordinator, IResetOverlayTrialRuntime runtime,
            ResetOverlayRole role, Exception startupFailure = null)
        {
            this.coordinator = coordinator; this.runtime = runtime; Role = role;
            if (startupFailure != null) Fail(startupFailure);
            else if (role != ResetOverlayRole.Initiator && role != ResetOverlayRole.ResetWorker)
                Fail(new InvalidOperationException("Unsupported reset role; this trial ends in the GameOnly worker."));
        }

        public Task PrepareMenuAsync()
        {
            if (prepared || Terminal || exitRequested) return Task.CompletedTask;
            prepared = true; BeginWork();
            return PrepareCoreAsync();
        }
        private async Task PrepareCoreAsync()
        {
            try
            {
                Set(ResetOverlayStage.Preparing); Guard();
                await runtime.WaitAvailableAsync(Guard); Guard();
                await runtime.PrepareAsync(coordinator.ReadRecord(), Guard); Guard();
                if (Role == ResetOverlayRole.Initiator)
                {
                    ready = true; Set(ResetOverlayStage.Initial); return;
                }
                runtime.Claim("reset-worker"); Guard();
                runtime.ValidateRecord(coordinator.ReadRecord(), true); Guard();
                runtime.Record("ResetStarted"); Guard();
                Set(ResetOverlayStage.Resetting); Guard();
                resetInFlight = true;
                await coordinator.ResumeAsync(() => { Guard(); runtime.ValidateRecord(coordinator.ReadRecord(), true); Guard(); });
                Guard();
                await runtime.VerifyResetAsync(coordinator.ReadRecord(), Guard); Guard();
                ready = true;
                Set(exitRequested ? ResetOverlayStage.Exiting : ResetOverlayStage.AwaitingOverlay);
            }
            catch (Exception e) { Fail(e); }
            finally { resetInFlight = false; EndWork(); }
        }

        public Task ReportAsync(string visibility, string[] judgments)
        {
            Tick();
            if (!CanReport) return Task.CompletedTask;
            reportAttempted = true; BeginWork();
            return ReportCoreAsync(visibility, judgments == null ? null : (string[])judgments.Clone());
        }
        private async Task ReportCoreAsync(string visibility, string[] judgments)
        {
            try
            {
                Guard(); Set(ResetOverlayStage.Reporting); Guard();
                if (judgments == null || judgments.Length != Targets.Length || judgments.Length == 0 ||
                    Array.Exists(judgments, v => v != "earned" && v != "unearned" && v != "inconclusive") ||
                    (visibility != "opened" && visibility != "not-visible" && visibility != "inconclusive") ||
                    (visibility != "opened" && Array.Exists(judgments, v => v != "inconclusive")))
                    throw new InvalidOperationException("Complete target judgments and an opening-attempt statement are required.");
                await runtime.SaveReportAsync(visibility, judgments);
                reportSaved = true; // Preserve a durable user statement after cancellation during storage.
                Guard();
                if (Role == ResetOverlayRole.Initiator && (visibility != "opened" || Array.Exists(judgments, v => v != "earned")))
                    throw new InvalidOperationException("Baseline screen mismatch or unidentified target; no reset requested.");
                Set(Role == ResetOverlayRole.Initiator ? ResetOverlayStage.Initial : ResetOverlayStage.Completed);
            }
            catch (Exception e) { Fail(e); }
            finally { EndWork(); }
        }

        public Task RequestResetAsync()
        {
            Tick();
            if (!CanRequest) return Task.CompletedTask;
            handoffAttempted = true; BeginWork();
            return RequestCoreAsync();
        }
        private async Task RequestCoreAsync()
        {
            bool requested = false, accepted = false;
            try
            {
                Set(ResetOverlayStage.Revalidating); Guard();
                runtime.ConfirmScope(); Guard();
                await runtime.RevalidateAsync(Guard); Guard();
                runtime.Claim("initial-request"); Guard();
                coordinator.RequestReset(() => { Guard(); runtime.ValidateRecord(coordinator.ReadRecord(), false); Guard(); });
                Guard();
                await runtime.BindPendingAsync(coordinator.ReadRecord(), Guard); Guard();
                Set(ResetOverlayStage.Launching); Guard();
                var result = runtime.StartHelper(Guard, () => { Guard(); requested = true; });
                if (result == HandoffResult.Accepted)
                { accepted = true; Set(ResetOverlayStage.HandoffAccepted); }
                else
                {
                    Fail(new InvalidOperationException("Helper creation not confirmed; claim and Pending are retained. Do not retry."));
                    if (result != HandoffResult.NotStarted) Set(ResetOverlayStage.HandoffUncertain);
                }
            }
            catch (Exception e)
            {
                if (requested && e is ResetOverlayAcceptedEvidenceException)
                { accepted = true; SetError(e.InnerException ?? e); Set(ResetOverlayStage.HandoffAccepted); }
                else if (requested) { SetError(e); Set(ResetOverlayStage.HandoffUncertain); }
                else Fail(e);
            }
            finally { EndWork(); }
            if (accepted) await CloseAsync();
        }

        public void Tick()
        {
            if (Terminal || (exitRequested && !resetInFlight)) return;
            try { Guard(); } catch (Exception e) { Fail(e); }
        }
        private void Guard()
        {
            if ((exitRequested && !resetInFlight) || Terminal) throw new OperationCanceledException();
            if (Error != null) throw new InvalidOperationException(Error);
            if (runtime?.CoreFailure != null) throw new InvalidOperationException(runtime.CoreFailure);
        }
        private void BeginWork() { working = true; idle = new TaskCompletionSource<bool>(); }
        private void EndWork() { working = false; idle?.TrySetResult(true); Notify(); }
        public Task CloseAsync()
        {
            if (exitRequested) return closeTask ?? Task.CompletedTask;
            exitRequested = true;
            if (!Terminal && !resetInFlight) Set(ResetOverlayStage.Exiting);
            closeTask = CloseCoreAsync();
            return closeTask;
        }
        private async Task CloseCoreAsync()
        {
            if (working) await idle.Task;
            try
            {
                if (!finalized)
                {
                    finalized = true;
                    if (runtime != null) await runtime.FinalSnapshotAsync();
                }
            }
            catch (Exception e) { SetError(e); }
            finally
            {
                try { runtime?.Record("ExitRequested:ActualExitConfirmed=false;SteamTrackingReleased=false"); }
                catch (Exception e) { SetError(e); }
                AllowApplicationQuit = true;
                try { runtime?.Quit(); } catch (Exception e) { SetError(e); }
                Notify();
            }
        }
        public void ObserveExternalQuit()
        {
            if (AllowApplicationQuit) return;
            exitRequested = true;
            try { runtime?.Record("ExternalExit:CompletionUnconfirmed;SteamTrackingReleased=false"); } catch (Exception e) { SetError(e); }
        }
        public void Cancel() { _ = CloseAsync(); }
        public void RequestReset() { _ = RequestResetAsync(); }
        public void CompleteMenuInitialization() { }
        public void Restart() { }
        public void LeaveMenu() { Cancel(); }
        public void FailMenuInitialization(string reason) { Fail(new InvalidOperationException(reason)); }
        private void Fail(Exception error)
        {
            if (error is OperationCanceledException && (exitRequested || Terminal)) return;
            SetError(error); ready = false;
            if (Stage != ResetOverlayStage.HandoffAccepted && Stage != ResetOverlayStage.HandoffUncertain)
                Set(error.Message.StartsWith("StartupAlreadyStarted", StringComparison.Ordinal) ? ResetOverlayStage.StartupAlreadyStarted : ResetOverlayStage.Failed);
        }
        private void SetError(Exception error)
        {
            if (Error != null) return;
            Error = OverlayObservationEvidence.Limit(error.GetType().Name + ": " + error.Message);
            try { runtime?.Failure(error); } catch (Exception) { }
        }
        private void Set(ResetOverlayStage stage) { Stage = stage; Notify(); }
        private void Notify()
        {
            var handlers = Changed;
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList()) try { handler(); } catch (Exception) { }
        }
    }
}
