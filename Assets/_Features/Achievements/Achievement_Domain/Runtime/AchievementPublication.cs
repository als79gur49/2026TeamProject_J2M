using System;

namespace Game.Product.Achievements
{
    public enum AchievementPublicationResult
    {
        Accepted = 0,
        AlreadySatisfied = 1,
        Deferred = 2,
        Unavailable = 3,
        Rejected = 4,
        Failed = 5,
    }

    public interface IAchievementPublicationSink
    {
        void Publish(
            GameAchievementId achievementId,
            Action<AchievementPublicationResult> completed);
    }

    public sealed class UnavailableAchievementPublicationSink : IAchievementPublicationSink
    {
        public void Publish(
            GameAchievementId achievementId,
            Action<AchievementPublicationResult> completed)
        {
            if (completed == null)
            {
                throw new ArgumentNullException(nameof(completed));
            }

            completed(AchievementPublicationResult.Unavailable);
        }
    }
}
