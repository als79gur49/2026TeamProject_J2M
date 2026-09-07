namespace Game.Product.Achievements.Infrastructure
{
    /// <summary>Replaces the canonical document and every automatic recovery source.</summary>
    public interface IDestructiveAchievementTextStore
    {
        void ResetToEmpty(string fileName, string contents);
    }
}
