using System;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Exhibition
{
    [Serializable]
    public sealed class ResetRecord
    {
        public const string Pending = "Pending";
        public const string Ready = "Ready";
        public int SchemaVersion = 1;
        public string OperationId;
        public string State;
        public uint AppId;
        public ulong SteamId;
        public string MappingVersion;

        public ResetRecord Copy() => (ResetRecord)MemberwiseClone();
    }

    public readonly struct ResetIdentity
    {
        public ResetIdentity(uint appId, ulong steamId)
        {
            AppId = appId;
            SteamId = steamId;
        }
        public uint AppId { get; }
        public ulong SteamId { get; }
    }

    public interface IExhibitionResetJournal
    {
        ResetRecord Load();
        void Save(ResetRecord record);
    }

    public interface IExhibitionSteamReset
    {
        ResetIdentity GetIdentity();
        Task ResetAsync(ResetIdentity expectedIdentity);
    }

    public interface IParticipantProgressReset
    {
        void Reset();
    }

    public sealed class ExhibitionResetCoordinator
    {
        public const string MappingVersion = "level-and-efficient-clear-v2";
        public const string IncompatibleMappingMessage = "이전 또는 지원하지 않는 테스트 버전의 초기화 기록입니다. 테스트 데이터 정리가 필요합니다. 재시작만으로 해결되지 않습니다.";
        private readonly IExhibitionResetJournal _journal;
        private readonly IExhibitionSteamReset _steam;
        private readonly IParticipantProgressReset _progress;
        private int _busy;
        private bool _resumeAttempted;

        public ExhibitionResetCoordinator(IExhibitionResetJournal journal,
            IExhibitionSteamReset steam, IParticipantProgressReset progress)
        {
            _journal = journal ?? throw new ArgumentNullException(nameof(journal));
            _steam = steam ?? throw new ArgumentNullException(nameof(steam));
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));

        }

        public void RequestReset() => RequestReset(null);

        public void RequestReset(Action beforeWrite)
        {
            Enter();
            try
            {
                var identity = _steam.GetIdentity();
                if (identity.AppId == 0 || identity.SteamId == 0)
                    throw new InvalidOperationException("A connected Steam account and AppID are required.");
                var existing = _journal.Load();
                if (existing != null)
                {
                    ValidateRecord(existing);
                    if (existing.State == ResetRecord.Pending)
                        throw new InvalidOperationException("A participant reset is already pending. Restart to resume it.");
                }
                beforeWrite?.Invoke();
                SaveCommitted(new ResetRecord
                {
                    OperationId = Guid.NewGuid().ToString("N"),
                    State = ResetRecord.Pending,
                    AppId = identity.AppId,
                    SteamId = identity.SteamId,
                    MappingVersion = MappingVersion,
                });
            }
            finally { Volatile.Write(ref _busy, 0); }
        }

        public Task<bool> ResumeAsync() => ResumeAsync(null);

        public async Task<bool> ResumeAsync(Action guard)
        {
            Enter();
            try
            {
                if (_resumeAttempted)
                    throw new InvalidOperationException("Restart the application before retrying a reset.");
                _resumeAttempted = true;
                var record = _journal.Load();
                if (record == null) return true;
                ValidateRecord(record);
                if (record.State == ResetRecord.Ready) return true;
                ValidatePending(record);
                guard?.Invoke();
                await _steam.ResetAsync(new ResetIdentity(record.AppId, record.SteamId));
                guard?.Invoke();
                ValidatePending(record);
                _progress.Reset();
                guard?.Invoke();
                ValidatePending(record);
                var ready = record.Copy();
                ready.State = ResetRecord.Ready;
                SaveCommitted(ready);
                return true;
            }
            finally { Volatile.Write(ref _busy, 0); }
        }

        private void Enter()
        {
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
                throw new InvalidOperationException("Participant reset is already in progress.");
        }

        public ResetIdentity GetCurrentIdentity() => _steam.GetIdentity();

        public ResetRecord ReadRecord()
        {
            var record = _journal.Load();
            if (record != null) ValidateRecord(record);
            return record;
        }

        private void SaveCommitted(ResetRecord desired)
        {
            try { _journal.Save(desired); }
            catch
            {
                // Save can throw after the canonical atomic replacement has committed.
                var actual = ReadRecord();
                if (actual == null || actual.OperationId != desired.OperationId ||
                    actual.State != desired.State || actual.AppId != desired.AppId ||
                    actual.SteamId != desired.SteamId || actual.MappingVersion != desired.MappingVersion)
                    throw;
            }
        }

        private void ValidatePending(ResetRecord record)
        {
            var actual = _steam.GetIdentity();
            if (record.MappingVersion != MappingVersion)
                throw new InvalidOperationException(IncompatibleMappingMessage);
            if (actual.AppId != record.AppId || actual.SteamId != record.SteamId)
                throw new InvalidOperationException("Sign in to the Steam account and AppID recorded in the pending reset, before resuming the reset.");
        }

        private static void ValidateRecord(ResetRecord record)
        {
            if (record.SchemaVersion != 1 || !Guid.TryParseExact(record.OperationId, "N", out _) ||
                (record.State != ResetRecord.Pending && record.State != ResetRecord.Ready) ||
                record.AppId == 0 || record.SteamId == 0 || string.IsNullOrWhiteSpace(record.MappingVersion))
                throw new InvalidOperationException("The participant reset record is corrupt or unsupported.");
            if (record.MappingVersion != MappingVersion)
                throw new InvalidOperationException(IncompatibleMappingMessage);
        }
    }
}
