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
            InspectProcessStartup(Environment.GetCommandLineArgs(), Application.isBatchMode,
                () => Compose(null, null), (completed, error) => Compose(completed, error));
        }

        // Deferral precedes parsing so a malformed return cannot start ordinary writers.
        public static void InspectProcessStartup(string[] args, bool batchMode, Action compose,
            Action<CompletedParticipantResetOptions, Exception> completedCompose = null)
        {
            if (CompletedParticipantResetOptions.Present(args))
            {
                DeferServices();
                CompletedParticipantResetOptions completed = null;
                Exception error = null;
                try
                {
                    if (batchMode || completedCompose == null)
                        throw new InvalidOperationException("Completed participant reset requires interactive startup composition.");
                    completed = CompletedParticipantResetOptions.Parse(args);
                }
                catch (Exception exception) { error = exception; }
                if (completedCompose == null)
                    throw error ?? new InvalidOperationException("Completed participant reset composition is unavailable.");
                completedCompose(completed, error);
                return;
            }
            if (batchMode) return;
            try { compose(); }
            catch
            {
                ProductAchievementStartupControl.DeferAutomaticStart();
                CampaignSaveCompositionProvider.SuspendProductionAccess();
                // Registration may not exist yet; stop alone cannot latch the startup hold.
                try { SteamAchievementMaintenanceAccess.DeferAutomaticPublication(); }
                catch (InvalidOperationException) { /* An existing runtime is stopped below. */ }
                SteamAchievementMaintenanceAccess.StopPublication();
                throw;
            }
        }

        private static void DeferServices()
        {
            bool alreadyStarted = ProductAchievementStartupControl.HasStarted;
            ProductAchievementStartupControl.DeferAutomaticStart();
            CampaignSaveCompositionProvider.SuspendProductionAccess();
            SteamAchievementMaintenanceAccess.DeferAutomaticPublication();
            if (alreadyStarted) throw new InvalidOperationException("Participant startup contract violated: product services already started.");
        }

        private static void Compose(CompletedParticipantResetOptions completedReset, Exception completedFailure)
        {
            var paths = new ApplicationPersistentDataSavePathProvider();
            ResetRecord record = null;
            var journalPath = Path.Combine(paths.SaveRootPath, "exhibition-reset.json");
            var journal = new FileExhibitionResetJournal(journalPath);
            var coordinator = new ExhibitionResetCoordinator(
                journal, new SteamExhibitionResetAdapter(), new ParticipantProgressResetAdapter(paths));
            Exception failure = completedFailure;
            try
            {
                instanceLock = AcquireSessionLock(paths);
                record = coordinator.ReadRecord();
            }
            catch (Exception exception) { failure = exception; }
            if (completedReset == null && (failure != null || record?.State == ResetRecord.Pending))
                DeferServices();
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
                startServices, reconcile, record, failure, completedRestart, completedReset, startRuntime);
            ParticipantResetMenuAccess.Register(service);
        }
    }
}
