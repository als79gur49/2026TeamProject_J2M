namespace Game.Feature.Stages
{
    // Covers recovery/compensation as well as ordinary commits. Only memory is changed here.
    internal sealed class CampaignHudObservedFileStore : IAtomicTextFileStore
    {
        private readonly IAtomicTextFileStore _inner;
        private readonly CampaignHudReadStore _reads;
        internal CampaignHudObservedFileStore(IAtomicTextFileStore inner, CampaignHudReadStore reads)
        { _inner = inner; _reads = reads; }
        internal CampaignHudReadStore Reads => _reads;
        private void Changing(string name)
        {
            _reads.ObserveStorageAccess();
            var pending = name.StartsWith(CampaignSaveRecoveryService.PendingResetFileName, System.StringComparison.Ordinal);
            _reads.Invalidate(gateUnknown: pending, profileUnknown: !pending);
        }
        public bool Exists(string name) { _reads.ObserveStorageAccess(); return _inner.Exists(name); }
        public string ReadAllText(string name) { _reads.ObserveStorageAccess(); return _inner.ReadAllText(name); }
        public void WriteAllTextAtomic(string name, string text)
        { Changing(name); _inner.WriteAllTextAtomic(name, text); }
        public void WriteAllTextAtomicWithoutBackup(string name, string text)
        { Changing(name); _inner.WriteAllTextAtomicWithoutBackup(name, text); }
        public bool Delete(string name) { Changing(name); return _inner.Delete(name); }
        public void EnsureDirectory() { _reads.ObserveStorageAccess(); _inner.EnsureDirectory(); }
        public bool TryRestoreBackup(string name) { Changing(name); return _inner.TryRestoreBackup(name); }
        public bool TryQuarantine(string name, out string path)
        { Changing(name); return _inner.TryQuarantine(name, out path); }
        public bool TryQuarantine(string name, string suffix, out string path)
        { Changing(name); return _inner.TryQuarantine(name, suffix, out path); }
        public void RecoverInterruptedWrite(string name) { Changing(name); _inner.RecoverInterruptedWrite(name); }
        public void CleanupTempFiles(string name) { Changing(name); _inner.CleanupTempFiles(name); }
    }
}
