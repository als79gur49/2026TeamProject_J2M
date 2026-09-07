using Game.Product.Achievements.CampaignIntegration;

namespace Game.Product.Achievements.Composition
{
    /// <summary>Optional composition control; normal application startup remains automatic.</summary>
    public static class ProductAchievementStartupControl
    {
        public static bool IsDeferred { get; private set; }
        internal static bool RequiresExplicitReconciliation { get; private set; }

        public static void DeferAutomaticStart()
        {
            IsDeferred = true;
            RequiresExplicitReconciliation = true;
        }

        public static bool StartDeferredServices()
        {
            var success = ProductAchievementRuntimeBootstrap.StartNow();
            if (success) IsDeferred = false;
            return success;
        }

        public static CampaignStageAchievementReconciliationResult ReconcileCampaign()
            => ProductAchievementRuntimeBootstrap.ReconcileNow();

        internal static void Reset()
        {
            IsDeferred = false;
            RequiresExplicitReconciliation = false;
        }
    }
}
