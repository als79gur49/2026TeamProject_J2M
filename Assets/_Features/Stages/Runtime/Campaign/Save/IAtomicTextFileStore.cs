namespace Game.Feature.Stages
{
    public interface IAtomicTextFileStore
    {
        bool Exists(string fileName);

        string ReadAllText(string fileName);

        void WriteAllTextAtomic(string fileName, string contents);

        void WriteAllTextAtomicWithoutBackup(string fileName, string contents);

        bool Delete(string fileName);

        void EnsureDirectory();

        bool TryRestoreBackup(string fileName);

        bool TryQuarantine(string fileName, out string quarantinePath);

        bool TryQuarantine(string fileName, string suffix, out string quarantinePath);

        void RecoverInterruptedWrite(string fileName);

        void CleanupTempFiles(string fileName);
    }
}
