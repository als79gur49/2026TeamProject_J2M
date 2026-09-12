using System;
using System.IO;
using Game.Feature.Stages;
using Game.Product.Achievements.Infrastructure;

namespace Game.Product.Achievements.Composition
{
    public sealed class StageAtomicAchievementTextStoreAdapter : IAchievementTextStore, IDestructiveAchievementTextStore
    {
        private readonly IAtomicTextFileStore _store;
        private readonly string _rootDirectory;

        public StageAtomicAchievementTextStoreAdapter(
            IAtomicTextFileStore store,
            string rootDirectory)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                throw new ArgumentException("A save root directory is required.", nameof(rootDirectory));
            }

            _rootDirectory = rootDirectory;
        }

        public bool Exists(string fileName)
        {
            return _store.Exists(fileName);
        }

        public string ReadAllText(string fileName)
        {
            return _store.ReadAllText(fileName);
        }

        public void WriteAllTextAtomic(string fileName, string contents)
        {
            _store.WriteAllTextAtomic(fileName, contents);
        }

        public void ResetToEmpty(string fileName, string contents)
        {
            // Resolve interrupted writes before replacing both automatic recovery sources.
            // A failure leaves the caller's reset request pending for a complete retry.
            var backup = fileName + ".bak";
            _store.RecoverInterruptedWrite(backup);
            _store.RecoverInterruptedWrite(fileName);
            _store.WriteAllTextAtomicWithoutBackup(backup, contents);
            _store.WriteAllTextAtomicWithoutBackup(fileName, contents);
            _store.CleanupTempFiles(backup);
            _store.CleanupTempFiles(fileName);
        }

        public bool TryRestoreBackup(string fileName)
        {
            return _store.TryRestoreBackup(fileName);
        }

        public bool TryQuarantine(string fileName, out string quarantinePath)
        {
            return _store.TryQuarantine(fileName, out quarantinePath);
        }

        public bool HasQuarantinedCopy(string fileName)
        {
            if (!Directory.Exists(_rootDirectory))
            {
                return false;
            }

            return Directory.GetFiles(
                _rootDirectory,
                fileName + ".corrupt.*",
                SearchOption.TopDirectoryOnly).Length > 0;
        }

        public void CleanupTempFiles(string fileName)
        {
            _store.CleanupTempFiles(fileName);
        }
    }
}
