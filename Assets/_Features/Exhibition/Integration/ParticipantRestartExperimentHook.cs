#if J2M_PARTICIPANT_RESTART_EXPERIMENT && UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Game.Exhibition.RestartExperiment;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Platform.Runtime;
using Game.Platform.Steam;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace Game.Exhibition.Integration
{
    /// <summary>Explicit operator experiment. Neither the define nor a Ready record starts a helper.</summary>
    public sealed class ParticipantRestartExperimentHook : MonoBehaviour, IParticipantResetPort, IParticipantResetActionPresentation, IRestartHandoff
    {
        private IParticipantResetPort inner;
        private Handoff handoff;
        private Trial trial;
        private bool ready, started, childObserved, notifying;
        private string prerequisites, requestPath, observationPath;
        private ExperimentRequest request;
        public event Action Changed;
        public bool BlocksMenu => started || inner.BlocksMenu;
        public bool IsBusy => started ? handoff.IsBusy : inner.IsBusy;
        // This deliberately armed diagnostic session cannot offer the destructive reset action.
        public bool CanRequest => observationPath != null && inner.CanRequest;
        public bool HideResetAction => observationPath == null;
        public bool SuppressSaveSeedImport => inner.SuppressSaveSeedImport;
        public string Error => started ? handoff.Error : inner.Error;

        public static IParticipantResetPort Wrap(IParticipantResetPort port, ResetRecord initial)
        {
            if (Application.isBatchMode || initial?.State != ResetRecord.Ready) return port;
            string selected = Argument("-j2mRestartExperiment");
            string observation = Argument("-j2mRestartObservation");
            if (selected == null && observation == null) return port;
            Trial mode = Trial.GameOnly;
            if (selected != null && (!Enum.TryParse(selected, false, out mode) || !Enum.IsDefined(typeof(Trial), mode)))
            { Debug.LogError("Unknown restart experiment trial; no hook installed."); return port; }
            var selection = PlatformProviderSelection.CurrentRequest;
            if (selection.Kind != PlatformProviderSelectionKind.Explicit || selection.RequestedProviderId != SteamPlatformRuntime.ProviderId) return port;
            var host = new GameObject("Participant restart experiment");
            DontDestroyOnLoad(host);
            var hook = host.AddComponent<ParticipantRestartExperimentHook>();
            hook.inner = port; hook.trial = mode; hook.observationPath = observation;
            hook.prerequisites = Argument("-j2mRestartPrerequisites");
            hook.handoff = new Handoff(hook, () => new RestartIdentity(hook.request.AppId, hook.request.SteamId), hook.RecordError);
            hook.handoff.Changed += hook.Notify;
            port.Changed += hook.Notify;
            return hook;
        }

        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }
        private void Update()
        {
            if (observationPath != null)
            {
                if (!childObserved && ready)
                {
                    childObserved = true;
                    try
                    {
                        var observed = ExperimentFiles.Read<ExperimentRequest>(observationPath);
                        using (var process = Process.GetCurrentProcess())
                        {
                            var child = WindowsIdentityCapture.Capture(process, true);
                            if (child.Path != observed.Parent.Path || child.Sha256 != observed.Parent.Sha256 ||
                                WindowsIdentityCapture.SameProcess(child, observed.Parent)) throw new IOException("Observation child identity differs.");
                            WindowsCycleEnvironment.WriteObservation(observed, "ChildMenuReady", null, -1, null, child);
                        }
                    }
                    catch (Exception e) { Debug.LogException(e); }
                }
                return; // Observation arguments can never arm another trial.
            }
            var keyboard = Keyboard.current;
            if (!ready || started || keyboard == null) return;
            if (keyboard.ctrlKey.isPressed && keyboard.shiftKey.isPressed && keyboard.f10Key.wasPressedThisFrame)
            {
                started = true; // Set before Changed, preflight, identity reads or publication suspension.
                handoff.Start(trial == Trial.GameOnly ? RestartPurpose.GameOnly : RestartPurpose.CompletedReset);
            }
        }
        private void OnGUI()
        {
            if (observationPath == null && ready && !started)
                GUI.Box(new Rect(10, 10, 610, 44), "RESTART EXPERIMENT: " + trial + " — Ctrl+Shift+F10\nCloses this game; Survival/Probe/FullCycle also close Steam.");
        }
        public Task PrepareMenuAsync() => inner.PrepareMenuAsync();
        public void CompleteMenuInitialization() { inner.CompleteMenuInitialization(); ready = inner.CanRequest; }
        public void LeaveMenu() { ready = false; inner.LeaveMenu(); }
        public void FailMenuInitialization(string reason)
        {
            ready = false;
            if (started) handoff.FailMenuInitialization(reason); else inner.FailMenuInitialization(reason);
        }
        public void RequestReset() { if (observationPath != null) inner.RequestReset(); }
        public void Restart()
        {
            if (started) handoff.Start(trial == Trial.GameOnly ? RestartPurpose.GameOnly : RestartPurpose.CompletedReset);
            else if (observationPath != null) inner.Restart();
        }

        public void ValidateAvailable(RestartPurpose purpose)
        {
            Handoff.ValidatePurpose(purpose);
            if (observationPath != null || (purpose == RestartPurpose.GameOnly) != (trial == Trial.GameOnly)) throw new InvalidOperationException("Trial/purpose mismatch.");
            if (request == null)
            {
                var paths = new ApplicationPersistentDataSavePathProvider();
                var record = new FileExhibitionResetJournal(Path.Combine(paths.SaveRootPath, "exhibition-reset.json")).Load();
                if (record?.State != ResetRecord.Ready) throw new InvalidOperationException("An already Ready session is required.");
                var identity = new SteamExhibitionResetAdapter().GetIdentity();
                using (var process = Process.GetCurrentProcess())
                {
                    var parent = WindowsIdentityCapture.Capture(process, true);
                    string nonce = Guid.NewGuid().ToString("N");
                    request = new ExperimentRequest { Nonce = nonce, Parent = parent, Steam = WindowsIdentityCapture.Steam(parent),
                        AppId = identity.AppId, SteamId = identity.SteamId, Trial = trial, PrerequisitesPath = prerequisites,
                        OperationId = record.OperationId, Build = Application.buildGUID + "/" + Application.version,
                        EvidenceDirectory = Path.Combine(@"D:\J2M\evidence\participant-restart-preflight", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + nonce),
                        ToolsDirectory = Path.Combine(Path.GetDirectoryName(parent.Path), "RestartExperiment"),
                        DllPath = Path.Combine(Path.GetDirectoryName(parent.Path), Path.GetFileNameWithoutExtension(parent.Path) + "_Data", "Plugins", "x86_64", "steam_api64.dll") };
                }
            }
            new WindowsCycleEnvironment(request, requestPath).Validate();
            if (!new WindowsCycleEnvironment(request, requestPath).OriginalSteamAlive()) throw new InvalidOperationException("Original Steam is absent.");
            Directory.CreateDirectory(request.EvidenceDirectory);
            requestPath = Path.Combine(request.EvidenceDirectory, "request.json");
            File.WriteAllText(requestPath, ExperimentFiles.Json(request));
            WindowsCycleEnvironment.WriteObservation(request, "PreflightComplete", null, 0, null, null);
            SteamAchievementMaintenanceAccess.StopPublication();
        }
        public HandoffResult StartHandoff(RestartIdentity identity, RestartPurpose purpose)
        {
            // All failures up to Process.Start are definitively before creation. Recheck the PID/path now.
            ProcessStartInfo start;
            try
            {
                if (request == null || identity.AppId != request.AppId || identity.SteamId != request.SteamId)
                    throw new InvalidOperationException("Captured identity changed.");
                ValidateAvailable(purpose);
                start = ExperimentFiles.HostStart(request.ToolsDirectory, requestPath, false);
            }
            catch (Exception e) { RecordError(e); return HandoffResult.NotStarted; }
            using (var process = Process.Start(start))
            {
                if (process == null) return HandoffResult.NotStarted;
                // No post-creation operation may turn this into NotStarted.
                try { WindowsCycleEnvironment.WriteObservation(request, "HelperAccepted", null, 0, null, WindowsIdentityCapture.Capture(process, false)); }
                catch (Exception e) { RecordError(e); }
                return HandoffResult.Accepted;
            }
        }
        public void RequestExit() { Application.Quit(); }
        private void RecordError(Exception e)
        {
            Debug.LogException(e);
            try { if (request != null && Directory.Exists(request.EvidenceDirectory)) WindowsCycleEnvironment.WriteObservation(request, "ParentError", e.ToString(), -1, null, null); }
            catch (Exception logError) { Debug.LogException(logError); }
        }
        private void Notify()
        {
            if (notifying) return;
            var handlers = Changed;
            if (handlers == null) return;
            notifying = true;
            try { foreach (Action handler in handlers.GetInvocationList()) try { handler(); } catch (Exception e) { RecordError(e); } }
            finally { notifying = false; }
        }
        private void OnDestroy() { if (inner != null) inner.Changed -= Notify; }
    }
}
#endif
