using UnityEngine;

namespace Game.Product.Achievements.CampaignIntegration
{
    internal static class ProductAchievementEarningSinkHandoff
    {
        private static IProductAchievementEarningSink _earningSink;

        internal static bool TryRegister(IProductAchievementEarningSink earningSink)
        {
            if (earningSink == null)
            {
                return false;
            }

            if (_earningSink == null)
            {
                _earningSink = earningSink;
                return true;
            }

            return ReferenceEquals(_earningSink, earningSink);
        }

        internal static bool TryGet(out IProductAchievementEarningSink earningSink)
        {
            earningSink = _earningSink;
            return earningSink != null;
        }

        internal static INormalCampaignCompletionAchievementIntegration
            CreateIntegrationForSceneComposition()
        {
            return _earningSink != null
                ? new NormalCampaignCompletionAchievementIntegration(_earningSink)
                : UnavailableNormalCampaignCompletionAchievementIntegration.Instance;
        }

        internal static void Clear(IProductAchievementEarningSink expected)
        {
            if (expected != null && ReferenceEquals(_earningSink, expected))
            {
                _earningSink = null;
            }
        }

        internal static void ResetForTests()
        {
            _earningSink = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForSubsystemRegistration()
        {
            _earningSink = null;
        }
    }
}
