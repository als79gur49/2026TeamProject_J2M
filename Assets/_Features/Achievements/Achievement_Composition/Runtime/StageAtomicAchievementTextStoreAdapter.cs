using System;
using Game.Feature.Stages;
using Game.Product.Achievements.Infrastructure;

namespace Game.Product.Achievements.Composition
{
    public sealed class StageAtomicAchievementTextStoreAdapter : IAchievementTextStore
    {
        private readonly IAtomicTextFileStore _store;

        public StageAtomicAchievementTextStoreAdapter(IAtomicTextFileStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
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

        public bool TryRestoreBackup(string fileName)
        {
            return _store.TryRestoreBackup(fileName);
        }

        public bool TryQuarantine(string fileName, out string quarantinePath)
        {
            return _store.TryQuarantine(fileName, out quarantinePath);
        }

        public void CleanupTempFiles(string fileName)
        {
            _store.CleanupTempFiles(fileName);
        }
    }
}
