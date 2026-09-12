using System;
using System.IO;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Platform.Steam;
using Game.Product.Achievements.Composition;
using Game.Product.Achievements.CampaignIntegration;
using UnityEngine;

namespace Game.Exhibition.Integration
{
    /// <summary>Optional participant maintenance startup in ordinary Windows players and Editor sessions.</summary>
    public static class ExhibitionApplication
    {
        private static FileStream instanceLock;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            ReleaseSessionLock();
        }

        internal static FileStream AcquireSessionLock(ISavePathProvider paths, bool requireExisting = false)
        {
            if (!requireExisting) Directory.CreateDirectory(paths.SaveRootPath);
            return new FileStream(Path.Combine(paths.SaveRootPath, "exhibition-instance.lock"),
                requireExisting ? FileMode.Open : FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        // Player keeps the lease until process exit; Editor releases only after all Play objects shut down.
        public static void ReleaseSessionLock()
        {
            instanceLock?.Dispose();
            instanceLock = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void InspectJournal()
        {
            bool enabled = false;
#if J2M_PARTICIPANT_RESTART_EXPERIMENT && UNITY_STANDALONE_WIN && !UNITY_EDITOR
            enabled = true;
#endif
            InspectProcessStartup(Environment.GetCommandLineArgs(), enabled, Application.isBatchMode,
                options => Compose(options, null, null), (completed, error) => Compose(null, completed, error));
        }

        public static void InspectProcessStartup(string[] args, bool enabled, bool batchMode, Action<ResetOverlayTrialOptions> compose,
            Action<CompletedParticipantResetOptions, Exception> completedCompose = null)
        {
            if (CompletedParticipantResetOptions.Present(args))
            {
                DeferServices();
                CompletedParticipantResetOptions completed = null; Exception error = null;
                try
                {
                    if (batchMode || completedCompose == null || ResetOverlayTrialOptions.Present(args) ||
                        OverlayHandoffObservationOptions.Present(args) || Game.Exhibition.RestartExperiment.ObservationV3Options.Present(args))
                        throw new InvalidOperationException("Completed participant reset startup arguments cannot be combined or run in batch mode.");
                    completed = CompletedParticipantResetOptions.Parse(args);
                }
                catch (Exception exception) { error = exception; }
                if (completedCompose == null) throw error ?? new InvalidOperationException("Completed participant reset composition is unavailable.");
                completedCompose(completed, error);
                return;
            }
            if (InspectV3Startup(args, ProductAchievementStartupControl.ObservationBuild, enabled, batchMode)) return;
            // Batch automation still avoids participant composition, but observation options must fail closed.
            if (batchMode)
            {
                if (!InspectObservationStartup(args, enabled, _ => throw new InvalidOperationException("BatchObservationUnsupported")))
                    InspectResetStartup(args, enabled, _ => throw new InvalidOperationException("BatchResetTrialUnsupported"));
                return;
            }
            InspectStartup(args, enabled, compose);
        }

        // Called before any BeforeSceneLoad product or platform publisher registration.
        // The opt-in latch is applied before filesystem/diagnostic/configuration composition.
        public static void InspectStartup(string[] args, bool trialEnabled, Action<ResetOverlayTrialOptions> compose)
        {
            if (InspectV3Startup(args, ProductAchievementStartupControl.ObservationBuild, trialEnabled, false)) return;
            if (InspectObservationStartup(args, trialEnabled, ComposeObservation)) return;
            if (InspectResetStartup(args, trialEnabled, compose)) return;
            ResetOverlayTrialOptions options = null;
            try
            {
                if (options != null) DeferServices();
                compose(options);
            }
            catch (Exception error)
            {
                // Preserve deferral even if constructors or diagnostics fail before a runtime exists.
                ProductAchievementStartupControl.DeferAutomaticStart();
                CampaignSaveCompositionProvider.SuspendProductionAccess();
                SteamAchievementMaintenanceAccess.StopPublication();
                if (options == null) throw;
                var failed = new ResetOverlayTrial(null, null, options.Role, error);
                ParticipantResetMenuAccess.Register(failed);
                ResetOverlayTrialPresentation.Attach(failed, null);
            }
        }

        public static bool InspectResetStartup(string[] args, bool enabled, Action<ResetOverlayTrialOptions> compose)
        {
            if (!ResetOverlayTrialOptions.Present(args)) return false;
            bool alreadyStarted = ProductAchievementStartupControl.ResetTrial || SteamAchievementMaintenanceAccess.ResetTrial ||
                SteamOverlayObservationAccess.SessionRequested || ProductAchievementStartupControl.HasStarted || CampaignSaveCompositionProvider.HasProductionComposition ||
                Game.Platform.Runtime.PlatformRuntimeRegistry.HasSelection || SteamOverlayObservationAccess.NativeStartupAttempted;
            Exception failure = null;
            foreach (Action inhibit in new Action[] { ProductAchievementStartupControl.InhibitForResetTrial,
                CampaignSaveCompositionProvider.InhibitForResetTrial, SteamAchievementMaintenanceAccess.InhibitForResetTrial })
                try { inhibit(); } catch (Exception e) { failure = failure ?? e; }
            var options = ResetOverlayTrialOptions.Parse(args, enabled);
            try
            {
                if (alreadyStarted)
                {
                    // Preserve prior history while revoking stale participant/native writers in a rejected session.
                    CampaignSaveCompositionProvider.InhibitForObservation();
                    SteamOverlayObservationAccess.InhibitWrites();
                    throw new InvalidOperationException("StartupAlreadyStarted: prior service/native history is retained; this session is ineligible.");
                }
                if (failure != null) throw failure;
                if (options.Error != null) throw options.Error;
                compose(options);
            }
            catch (Exception error)
            {
                var failed = new ResetOverlayTrial(null, null, options.Role, error);
                ParticipantResetMenuAccess.Register(failed);
                ResetOverlayTrialPresentation.Attach(failed, null);
            }
            return true;
        }

        public static bool InspectObservationStartup(string[] args, bool enabled, Action<OverlayHandoffObservationOptions> compose)
        {
            if (!OverlayHandoffObservationOptions.Present(args)) return false;
            // Capture history before inhibition/disposal. Later byte equality cannot make this a valid sample.
            bool alreadyStarted = SteamOverlayObservationAccess.SessionRequested || ProductAchievementStartupControl.HasStarted || CampaignSaveCompositionProvider.HasProductionComposition ||
                Game.Platform.Runtime.PlatformRuntimeRegistry.HasSelection || SteamOverlayObservationAccess.NativeStartupAttempted;
            Exception failure = null;
            foreach (Action inhibit in new Action[] { ProductAchievementStartupControl.InhibitForObservation,
                CampaignSaveCompositionProvider.InhibitForObservation, () => SteamOverlayObservationAccess.InhibitWrites(),
                SteamAchievementMaintenanceAccess.DeferAutomaticPublication })
            {
                try { inhibit(); }
                catch (Exception e) { failure = failure ?? e; }
            }
            var options = OverlayHandoffObservationOptions.Parse(args, enabled);
            try
            {
                if (alreadyStarted) throw new InvalidOperationException("StartupAlreadyStarted: prior runtime/service history is preserved; this session is ineligible.");
                if (failure != null) throw failure;
                if (options.Error != null) throw options.Error;
                compose(options);
            }
            catch (Exception error)
            {
                var failed = new OverlayHandoffObservation(null, options.Role, error);
                ParticipantResetMenuAccess.Register(failed);
                OverlayHandoffObservationPresentation.Attach(failed);
            }
            return true;
        }

        public static bool InspectV3Startup(string[] args, bool observationOnlyBuild, bool enabled, bool batchMode)
        {
            if (!observationOnlyBuild && !Game.Exhibition.RestartExperiment.ObservationV3Options.Present(args)) return false;
            bool started = SteamOverlayObservationAccess.SessionRequested || SteamOverlayObservationAccess.NativeStartupAttempted ||
                ProductAchievementStartupControl.HasStarted || CampaignSaveCompositionProvider.HasProductionComposition ||
                Game.Platform.Runtime.PlatformRuntimeRegistry.HasSelection;
            Exception error = null;
            foreach (Action inhibit in new Action[] { ProductAchievementStartupControl.InhibitForObservation,
                CampaignSaveCompositionProvider.InhibitForObservation, SteamOverlayObservationAccess.BlockNativeStartup,
                SteamAchievementMaintenanceAccess.DeferAutomaticPublication })
                try { inhibit(); } catch (Exception e) { error = error ?? e; }
            try
            {
                if (started) throw new InvalidOperationException("StartupAlreadyStarted");
                if (error != null) throw error;
                var parsed = Game.Exhibition.RestartExperiment.ObservationV3Options.Parse(args);
                if (!enabled || batchMode) throw new InvalidOperationException("ObservationV3Unsupported");
                if (!observationOnlyBuild) throw new InvalidOperationException("CompiledObservationBuildRequired");
                // Synchronous deferral precedes the asynchronous input/peer validation and canonical release.
                ObservationV3Runtime.StartObservation(parsed);
                return true;
            }
            catch (Exception e) { error = e; }
            var failed = new OverlayHandoffObservation(null, Game.Exhibition.RestartExperiment.OverlayObservationRole.None,
                error ?? new InvalidOperationException("LaunchTransportUnavailable"));
            ParticipantResetMenuAccess.Register(failed);
            OverlayHandoffObservationPresentation.Attach(failed);
            return true;
        }

        private static void ComposeObservation(OverlayHandoffObservationOptions options)
        {
            var paths = new ApplicationPersistentDataSavePathProvider();
            instanceLock = AcquireSessionLock(paths);
            var runtime = new OverlayHandoffObservationRuntime(options, paths);
            var observation = new OverlayHandoffObservation(runtime, options.Role);
            ParticipantResetMenuAccess.Register(observation);
            OverlayHandoffObservationPresentation.Attach(observation);
        }

        private static void DeferServices()
        {
            bool alreadyStarted = ProductAchievementStartupControl.HasStarted;
            ProductAchievementStartupControl.DeferAutomaticStart();
            CampaignSaveCompositionProvider.SuspendProductionAccess();
            SteamAchievementMaintenanceAccess.DeferAutomaticPublication();
            if (alreadyStarted) throw new InvalidOperationException("Trial startup contract violated: product services already started.");
        }

        private static void Compose(ResetOverlayTrialOptions trialOptions, CompletedParticipantResetOptions completedReset, Exception completedFailure)
        {
            var paths = new ApplicationPersistentDataSavePathProvider();
            ResetRecord record = null;
            var journalPath = Path.Combine(paths.SaveRootPath, "exhibition-reset.json");
            var journal = new FileExhibitionResetJournal(journalPath);
            var diagnostics = trialOptions == null ? ParticipantResetDiagnostics.Create(() => record, journal.Load) : null;
            var coordinator = new ExhibitionResetCoordinator(
                journal, new SteamExhibitionResetAdapter(diagnostics), new ParticipantProgressResetAdapter(paths));
            Exception failure = completedFailure;
            try
            {
                instanceLock = AcquireSessionLock(paths, trialOptions != null);
                record = coordinator.ReadRecord();
            }
            catch (Exception exception) { failure = exception; }
            if (trialOptions?.Error != null) failure = failure ?? trialOptions.Error;
            if (trialOptions == null && completedReset == null && (failure != null || record?.State == ResetRecord.Pending))
                DeferServices();
            if (trialOptions != null)
            {
                ResetOverlayTrialRuntime runtime = null;
                try
                {
                    if (failure != null) throw failure;
                    if (trialOptions.Role == RestartExperiment.ResetOverlayRole.Initiator && record?.State != ResetRecord.Ready)
                        throw new InvalidOperationException("An already Ready journal is required; Pending is never auto-resumed by the Initiator.");
                    runtime = new ResetOverlayTrialRuntime(trialOptions.Role, trialOptions.Path, trialOptions.Observation, paths);
                }
                catch (Exception exception) { failure = exception; }
                var trial = new ResetOverlayTrial(coordinator, runtime, trialOptions.Role, failure);
                ParticipantResetMenuAccess.Register(trial);
                ResetOverlayTrialPresentation.Attach(trial, runtime?.EvidenceDirectory);
                return;
            }
#if UNITY_EDITOR
            IParticipantRestart restart = new EditorParticipantRestartAdapter();
            IParticipantRestart completedRestart = null;
#else
            IParticipantRestart restart = new ExhibitionRelaunchAdapter(30);
            IParticipantRestart completedRestart = new CompletedParticipantResetRestartAdapter(journalPath);
#endif
            Action startRuntime = () =>
            {
                if (!ProductAchievementStartupControl.StartDeferredServices())
                    throw new InvalidOperationException("업적 서비스를 시작할 수 없습니다. 다시 실행해 주세요.");
            };
            Action startServices = () =>
            {
                if (!SteamAchievementMaintenanceAccess.StartPublication())
                    throw new InvalidOperationException("업적 게시를 시작할 수 없습니다. 다시 실행해 주세요.");
                CampaignSaveCompositionProvider.ReleaseProductionAccess();
            };
            Action reconcile = () =>
            {
                var result = ProductAchievementStartupControl.ReconcileCampaign();
                if (result != CampaignStageAchievementReconciliationResult.Completed && result != CampaignStageAchievementReconciliationResult.AlreadyReconciled &&
                    result != CampaignStageAchievementReconciliationResult.NotAttempted) throw new InvalidOperationException("진행 업적 준비 실패: " + result);
            };
            IParticipantResetPort service = new ParticipantResetService(coordinator, restart,
                () => SteamAchievementMaintenanceAccess.IsAvailable, SteamAchievementMaintenanceAccess.StopPublication,
                startServices, reconcile, record, failure, diagnostics, completedRestart, completedReset, startRuntime);
#if J2M_PARTICIPANT_RESTART_EXPERIMENT && UNITY_STANDALONE_WIN && !UNITY_EDITOR
            service = ParticipantRestartExperimentHook.Wrap(service, record);
#endif
            ParticipantResetMenuAccess.Register(service);
        }
    }
}
