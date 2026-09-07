using System;
using System.Threading.Tasks;
using Game.Feature.UI.Application;

namespace Game.Exhibition.Integration
{
    /// <summary>One session, one resume attempt. A committed request always crosses a restart boundary.</summary>
    public sealed class ParticipantResetService : IParticipantResetPort
    {
        private readonly ExhibitionResetCoordinator coordinator;
        private readonly IParticipantRestart restart;
        private readonly Func<bool> steamAvailable;
        private readonly Action stopPublication;
        private readonly Action startServices;
        private readonly Action reconcile;
        private bool deferred;
        private bool menuReady;
        private bool prepareAttempted;
        private bool requestInProgress;
        public event Action Changed;
        public bool BlocksMenu { get; private set; }
        public bool IsBusy { get; private set; }
        public bool CanRequest => menuReady && !BlocksMenu && !requestInProgress && steamAvailable();
        public bool SuppressSaveSeedImport { get; private set; }
        public string Error { get; private set; }

        public ParticipantResetService(ExhibitionResetCoordinator coordinator, IParticipantRestart restart,
            Func<bool> steamAvailable, Action stopPublication, Action startServices, Action reconcile,
            ResetRecord initialRecord, Exception startupFailure = null)
        {
            this.coordinator = coordinator;
            this.restart = restart;
            this.steamAvailable = steamAvailable;
            this.stopPublication = stopPublication;
            this.startServices = startServices;
            this.reconcile = reconcile;
            deferred = initialRecord?.State == ResetRecord.Pending;
            BlocksMenu = deferred || startupFailure != null;
            SuppressSaveSeedImport = initialRecord != null;
            Error = startupFailure?.Message;
        }

        public async Task PrepareMenuAsync()
        {
            if (prepareAttempted) return;
            prepareAttempted = true;
            if (!string.IsNullOrEmpty(Error) || !deferred) return;
            IsBusy = true;
            Changed?.Invoke();
            try
            {
                await coordinator.ResumeAsync();
                startServices();
                BlocksMenu = false;
            }
            catch (Exception exception) { Error = exception.Message; }
            finally { IsBusy = false; Changed?.Invoke(); }
        }

        public void CompleteMenuInitialization()
        {
            if (BlocksMenu) return;
            try
            {
                if (deferred) reconcile();
                menuReady = true;
            }
            catch (Exception exception)
            {
                BlocksMenu = true;
                Error = exception.Message;
                stopPublication();
            }
            Changed?.Invoke();
        }

        public void LeaveMenu() => menuReady = false;

        public void FailMenuInitialization(string reason)
        {
            BlocksMenu = true;
            menuReady = false;
            IsBusy = false;
            Error = reason;
            stopPublication();
            Changed?.Invoke();
        }

        public void RequestReset()
        {
            if (!CanRequest) return;
            requestInProgress = true;
            Error = null;
            try
            {
                restart.ValidateAvailable();
                coordinator.RequestReset();
                BlocksMenu = true;
                menuReady = false;
                SuppressSaveSeedImport = true;
                stopPublication();
                RestartCommitted();
            }
            catch (Exception exception)
            {
                // A failed write can have left Pending, a companion-only record, or unreadable storage.
                try { BlocksMenu |= coordinator.ReadRecord()?.State == ResetRecord.Pending; }
                catch { BlocksMenu = true; }
                Error = exception.Message;
                IsBusy = false;
                if (BlocksMenu) { menuReady = false; stopPublication(); }
            }
            finally { requestInProgress = false; Changed?.Invoke(); }
        }

        public void Restart()
        {
            if (!BlocksMenu || IsBusy) return;
            try { RestartCommitted(); }
            catch (Exception exception) { Error = exception.Message; IsBusy = false; Changed?.Invoke(); }
        }

        private void RestartCommitted()
        {
            restart.ValidateAvailable();
            var record = coordinator.ReadRecord();
            if (record == null) throw new InvalidOperationException("초기화 기록을 확인한 뒤 게임을 다시 실행해 주세요.");
            IsBusy = true;
            Error = null;
            Changed?.Invoke();
            var identity = record.State == ResetRecord.Pending
                ? new ResetIdentity(record.AppId, record.SteamId)
                : coordinator.GetCurrentIdentity();
            restart.Restart(identity);
        }
    }
}
