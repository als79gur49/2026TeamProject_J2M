using System;
using System.Linq;
using System.Threading.Tasks;
using Game.Exhibition.RestartExperiment;
using Game.Feature.UI.Application;

namespace Game.Exhibition.Integration
{
    public sealed class OverlayHandoffObservationOptions
    {
        public const string Initial = "-j2mOverlayHandoffObservation";
        public const string Context = "-j2mOverlayHandoffContext";
        public const string Request = "-j2mOverlayHandoffRequest";
        public OverlayObservationRole Role;
        public string Path, RequestPath;
        public Exception Error;
        public ObservationArgumentCount[] ArgumentCounts;
        public sealed class ObservationArgumentCount { public string Name; public int Count; }
        public static bool Present(string[] args) => (args ?? Array.Empty<string>()).Any(a =>
            a != null && a.StartsWith("-j2mOverlayHandoff", StringComparison.OrdinalIgnoreCase));

        public static OverlayHandoffObservationOptions Parse(string[] args, bool enabled)
        {
            if (!Present(args)) return null;
            var result = new OverlayHandoffObservationOptions { ArgumentCounts = new[] { Initial, Context, Request,
                "-j2mPlatformProvider", "-j2mResetOverlayTrial", "-j2mRestartExperiment", "-j2mRestartObservation",
                "-j2mSteamSmoke", "-j2mSteamAchievementSmoke" }.Select(key => new ObservationArgumentCount {
                    Name = key, Count = args.Count(a => a == key || (a != null && a.StartsWith(key + "=", StringComparison.Ordinal))) }).ToArray() };
            try
            {
                if (!enabled) throw new InvalidOperationException("Observation is unsupported by this build.");
                string Value(string key)
                {
                    var indices = Enumerable.Range(0, args.Length).Where(i => args[i] == key).ToArray();
                    if (indices.Length == 0) return null;
                    if (indices.Length != 1 || indices[0] + 1 >= args.Length || string.IsNullOrWhiteSpace(args[indices[0] + 1]) ||
                        args[indices[0] + 1].StartsWith("-", StringComparison.Ordinal)) throw new ArgumentException("Invalid argument: " + key);
                    return args[indices[0] + 1];
                }
                foreach (var arg in args.Where(a => a != null))
                {
                    if (arg.StartsWith("-j2mResetOverlay", StringComparison.OrdinalIgnoreCase) ||
                        arg.StartsWith("-j2mRestart", StringComparison.OrdinalIgnoreCase) ||
                        arg.StartsWith("-j2mSteamSmoke", StringComparison.OrdinalIgnoreCase) ||
                        arg.StartsWith("-j2mSteamAchievementSmoke", StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException("Observation and reset/restart/smoke arguments are mutually exclusive.");
                    if (arg.StartsWith("-j2mOverlayHandoff", StringComparison.OrdinalIgnoreCase) && arg != Initial && arg != Context && arg != Request)
                        throw new ArgumentException("Unknown observation argument.");
                }
                var initial = Value(Initial); var context = Value(Context); result.RequestPath = Value(Request);
                if (initial != null && context == null && result.RequestPath == null)
                { result.Role = OverlayObservationRole.OriginObserver; result.Path = initial; }
                else if (initial == null && context != null && result.RequestPath != null)
                { result.Role = OverlayObservationRole.ReplacementObserver; result.Path = context; }
                else throw new ArgumentException("Partial or mixed observation role.");
                var providers = args.Where(a => a != null && (a == "-j2mPlatformProvider" || a.StartsWith("-j2mPlatformProvider=", StringComparison.Ordinal))).ToArray();
                if (providers.Length != 1) throw new ArgumentException("Exactly one effective Steam provider is required.");
                string provider = providers[0].Contains("=") ? providers[0].Substring(providers[0].IndexOf('=') + 1) : Value("-j2mPlatformProvider");
                if (!string.Equals(provider?.Trim(), "steam", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Steam provider required.");
            }
            catch (Exception e) { result.Error = e; }
            return result;
        }
    }

    public enum OverlayObservationStage { Preparing, Observing, Revalidating, HandoffAccepted, HandoffUncertain,
        Completed, Failed, Cancelled, StartupAlreadyStarted }
    public enum OverlayVisibility { Opened, NotVisible, Inconclusive }

    public sealed class OverlayHandoffAcceptedEvidenceException : Exception
    {
        public OverlayHandoffAcceptedEvidenceException(Exception inner)
            : base("Helper creation was accepted, but creation evidence could not be saved.", inner) { }
    }

    public interface IOverlayHandoffObservationRuntime
    {
        double Now { get; }
        bool NativeReady();
        bool ReceiptReady();
        Task PrepareAsync(Action guard);
        string ObservationFailure { get; }
        Task RevalidateAsync(Action guard);
        void Claim();
        HandoffResult StartHelper(Action finalGuard);
        void Record(string stage, object value = null);
        Task SaveObservationAsync(OverlayVisibility visibility);
        Task DelayAsync();
        void FinalSnapshot();
        void Quit();
    }

    /// <summary>User reports and a single read-only GameOnly handoff. No reset or publication capability.</summary>
    public sealed class OverlayHandoffObservation : IParticipantResetPort, IParticipantResetActionPresentation, IParticipantResetStatusPresentation
    {
        private readonly IOverlayHandoffObservationRuntime runtime;
        public OverlayObservationRole Role { get; }
        public OverlayObservationStage Stage { get; private set; } = OverlayObservationStage.Preparing;
        public string Error { get; private set; }
        public OverlayVisibility? Visibility { get; private set; }
        private bool preparing, ready, reporting, handoffAttempted, exiting, finalSnapshotTaken;
        public event Action Changed;
        public bool HasRuntime => runtime != null;
        public bool BlocksMenu => true;
        public bool SuppressSaveSeedImport => true;
        public bool HideResetAction => true;
        public bool OwnsStatusPresentation => true;
        public bool CanRequest => false;
        public bool IsBusy => Stage == OverlayObservationStage.Preparing || Stage == OverlayObservationStage.Revalidating || reporting;
        public bool CanReplace => Role == OverlayObservationRole.OriginObserver && Stage == OverlayObservationStage.Observing &&
            ready && Visibility == OverlayVisibility.Opened && !handoffAttempted && !reporting && !exiting && Error == null;
        public bool CanReport => Stage == OverlayObservationStage.Observing && ready && !Visibility.HasValue && !reporting && !exiting && Error == null;
        private bool Terminal => Stage != OverlayObservationStage.Preparing && Stage != OverlayObservationStage.Observing && Stage != OverlayObservationStage.Revalidating;

        public OverlayHandoffObservation(IOverlayHandoffObservationRuntime runtime, OverlayObservationRole role, Exception error = null)
        {
            this.runtime = runtime; Role = role;
            if (error != null) Fail(error);
        }

        public async Task PrepareMenuAsync()
        {
            if (preparing || Terminal || exiting) return;
            preparing = true;
            try
            {
                await WaitAsync(runtime.NativeReady, "SdkTimeout"); Guard();
                if (Role == OverlayObservationRole.ReplacementObserver)
                { await WaitAsync(runtime.ReceiptReady, "ReceiptTimeout"); Guard(); }
                await runtime.PrepareAsync(Guard); Guard();
                runtime.Record("ReadOnlyReady"); Guard();
                ready = true; Stage = OverlayObservationStage.Observing; Notify();
            }
            catch (Exception e) { Fail(e); }
        }

        private async Task WaitAsync(Func<bool> poll, string reason)
        {
            double deadline = runtime.Now + 30;
            while (true)
            {
                Guard();
                bool readyNow = poll(); Guard();
                if (runtime.Now >= deadline) throw new TimeoutException(reason);
                if (readyNow) return;
                await runtime.DelayAsync(); Guard();
            }
        }

        public void Tick()
        {
            if (Terminal || exiting) return;
            try { Guard(); } catch (Exception e) { Fail(e); }
        }

        public async Task ReportAsync(OverlayVisibility visibility)
        {
            Tick();
            if (!CanReport) return;
            reporting = true; Notify();
            try
            {
                Guard();
                await runtime.SaveObservationAsync(visibility);
                // Preserve a successfully stored user statement even when cancellation arrived during the write.
                Visibility = visibility;
                Guard();
                if (Role == OverlayObservationRole.ReplacementObserver || visibility != OverlayVisibility.Opened)
                    Stage = OverlayObservationStage.Completed;
            }
            catch (Exception e) { Fail(e); }
            finally { reporting = false; Notify(); }
        }

        public async Task ReplaceAsync()
        {
            Tick();
            if (!CanReplace) return;
            handoffAttempted = true;
            Stage = OverlayObservationStage.Revalidating; Notify();
            bool requested = false;
            try
            {
                Guard();
                await runtime.RevalidateAsync(Guard); Guard();
                runtime.Claim(); Guard();
                var result = runtime.StartHelper(() => { Guard(); requested = true; });
                if (result == HandoffResult.Accepted)
                {
                    Stage = OverlayObservationStage.HandoffAccepted;
                    TryRecord("HandoffAccepted"); Notify();
                    await CloseAsync();
                }
                else
                {
                    Stage = result == HandoffResult.NotStarted ? OverlayObservationStage.Failed : OverlayObservationStage.HandoffUncertain;
                    SetError(new InvalidOperationException("Helper creation was not confirmed. The claim is consumed; do not retry."));
                    TryRecord(Stage.ToString()); Notify();
                }
            }
            catch (Exception e)
            {
                if (e is OverlayHandoffAcceptedEvidenceException && requested)
                {
                    Stage = OverlayObservationStage.HandoffAccepted;
                    SetError(e.InnerException ?? e);
                    TryRecord("HandoffAcceptedEvidenceError", Error); Notify();
                    await CloseAsync();
                }
                else if (requested)
                { Stage = OverlayObservationStage.HandoffUncertain; SetError(e); TryRecord("HandoffUncertain"); Notify(); }
                else Fail(e);
            }
        }

        private void Guard()
        {
            if (Terminal || exiting) throw new OperationCanceledException();
            if (Error != null) throw new InvalidOperationException(Error);
            if (runtime.ObservationFailure != null) throw new InvalidOperationException(runtime.ObservationFailure);
        }

        public Task CloseAsync()
        {
            if (exiting) return Task.CompletedTask;
            exiting = true;
            if (!Terminal) Stage = OverlayObservationStage.Cancelled;
            Notify();
            try { FinalizeObservation(); }
            finally
            {
                try { runtime?.Quit(); }
                catch (Exception e) { SetError(e); }
                Notify();
            }
            return Task.CompletedTask;
        }

        public void FinalizeObservation()
        {
            if (finalSnapshotTaken) return;
            finalSnapshotTaken = true;
            exiting = true;
            if (!Terminal) Stage = OverlayObservationStage.Cancelled;
            try { runtime?.FinalSnapshot(); }
            catch (Exception e) { SetError(e); }
            TryRecord("ExitRequested", new { Stage = Stage.ToString(), Error, ActualExitConfirmed = false, SteamTrackingReleased = false });
        }

        private void Fail(Exception e)
        {
            if (e is OperationCanceledException && (Terminal || exiting)) return;
            SetError(e);
            if (!Terminal && !exiting)
                Stage = e.Message.StartsWith("StartupAlreadyStarted", StringComparison.Ordinal) ? OverlayObservationStage.StartupAlreadyStarted : OverlayObservationStage.Failed;
            Notify();
        }
        private void SetError(Exception e)
        {
            if (Error != null) return;
            Error = OverlayObservationEvidence.Limit(e.GetType().Name + ": " + e.Message);
            TryRecord("CoreFailure", Error);
        }
        private void TryRecord(string stage, object value = null)
        { try { runtime?.Record(stage, value); } catch (Exception e) { if (Error == null) Error = OverlayObservationEvidence.Limit(e.Message); } }
        private void Notify() => Changed?.Invoke();
        public void CompleteMenuInitialization() { }
        public void LeaveMenu() { }
        public void FailMenuInitialization(string reason) => Fail(new InvalidOperationException(reason));
        public void RequestReset() { }
        public void Restart() { }
    }
}
