using Game.Product.Achievements.CampaignIntegration;

namespace Game.Product.Achievements.Composition
{
    /// <summary>Optional composition control; normal application startup remains automatic.</summary>
    public static class ProductAchievementStartupControl
    {
        public static bool HasStarted => ProductAchievementRuntimeBootstrap.HasStarted;
        public static bool ObservationBuild => Game.Feature.Stages.CampaignSaveCompositionProvider.ObservationBuild;
        private static bool observationOnly;
        public static bool ObservationOnly { get => observationOnly || ObservationBuild; private set => observationOnly = value; }
        public static bool ResetTrial { get; private set; }
        public static bool ServicesInhibited => ObservationOnly || ResetTrial;
        public static void InhibitForResetTrial()
        {
            ResetTrial = true;
            DeferAutomaticStart();
            ProductAchievementRuntimeBootstrap.StopForObservation();
        }
        public static void InhibitForObservation()
        {
            ObservationOnly = true;
            DeferAutomaticStart();
            ProductAchievementRuntimeBootstrap.StopForObservation();
        }

        private static bool deferred;
        public static bool IsDeferred { get => deferred || ObservationBuild; private set => deferred = value; }
        internal static bool RequiresExplicitReconciliation { get; private set; }

        public static void DeferAutomaticStart()
        {
            IsDeferred = true;
            RequiresExplicitReconciliation = true;
        }

        public static bool StartDeferredServices()
        {
            if (ServicesInhibited) return false;
            var success = ProductAchievementRuntimeBootstrap.StartNow();
            if (success) IsDeferred = false;
            return success;
        }

        public static CampaignStageAchievementReconciliationResult ReconcileCampaign()
            => ServicesInhibited ? CampaignStageAchievementReconciliationResult.NotAttempted : ProductAchievementRuntimeBootstrap.ReconcileNow();

        internal static void Reset()
        {
            ObservationOnly = false;
            ResetTrial = false;
            IsDeferred = false;
            RequiresExplicitReconciliation = false;
        }
    }
}
