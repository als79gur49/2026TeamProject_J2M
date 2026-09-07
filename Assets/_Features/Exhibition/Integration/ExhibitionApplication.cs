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

        internal static FileStream AcquireSessionLock(ISavePathProvider paths)
        {
            Directory.CreateDirectory(paths.SaveRootPath);
            return new FileStream(Path.Combine(paths.SaveRootPath, "exhibition-instance.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
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
            // Repository automation composes fakes explicitly; never open a user's save root in batch tests.
            if (Application.isBatchMode) return;
            var paths = new ApplicationPersistentDataSavePathProvider();
            var coordinator = new ExhibitionResetCoordinator(
                new FileExhibitionResetJournal(Path.Combine(paths.SaveRootPath, "exhibition-reset.json")),
                new SteamExhibitionResetAdapter(), new ParticipantProgressResetAdapter(paths));
            ResetRecord record = null;
            Exception failure = null;
            try
            {
                instanceLock = AcquireSessionLock(paths);
                record = coordinator.ReadRecord();
            }
            catch (Exception exception) { failure = exception; }
            if (failure != null || record?.State == ResetRecord.Pending)
            {
                ProductAchievementStartupControl.DeferAutomaticStart();
                SteamAchievementMaintenanceAccess.DeferAutomaticPublication();
                CampaignSaveCompositionProvider.SuspendProductionAccess();
            }
#if UNITY_EDITOR
            IParticipantRestart restart = new EditorParticipantRestartAdapter();
#else
            IParticipantRestart restart = new ExhibitionRelaunchAdapter(30);
#endif
            ParticipantResetMenuAccess.Register(new ParticipantResetService(coordinator, restart,
                () => SteamAchievementMaintenanceAccess.IsAvailable,
                SteamAchievementMaintenanceAccess.StopPublication,
                () =>
                {
                    if (!ProductAchievementStartupControl.StartDeferredServices() ||
                        !SteamAchievementMaintenanceAccess.StartPublication())
                        throw new InvalidOperationException("업적 서비스를 시작할 수 없습니다. 다시 실행해 주세요.");
                    CampaignSaveCompositionProvider.ReleaseProductionAccess();
                },
                () =>
                {
                    var result = ProductAchievementStartupControl.ReconcileCampaign();
                    if (result != CampaignStageAchievementReconciliationResult.Completed &&
                        result != CampaignStageAchievementReconciliationResult.AlreadyReconciled &&
                        result != CampaignStageAchievementReconciliationResult.NotAttempted)
                        throw new InvalidOperationException("진행 업적 준비 실패: " + result);
                }, record, failure));
        }
    }
}
