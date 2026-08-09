namespace Game.Product.Achievements.Infrastructure
{
    public interface IAchievementTextStore
    {
        bool Exists(string fileName);

        string ReadAllText(string fileName);

        void WriteAllTextAtomic(string fileName, string contents);

        bool TryRestoreBackup(string fileName);

        bool TryQuarantine(string fileName, out string quarantinePath);

        void CleanupTempFiles(string fileName);
    }
}
