using System;
using System.Threading.Tasks;
using Game.Feature.UI.Application;

namespace Game.Exhibition.Integration
{
    /// <summary>One session, one resume attempt. A committed request always crosses a restart boundary.</summary>
    public sealed class ParticipantResetService : IParticipantResetPort, IParticipantResetLegacyRecovery, IParticipantResetRetryPolicy
    {
        private readonly ExhibitionResetCoordinator coordinator;
        private readonly IParticipantRestart restart;
        private readonly Func<bool> steamAvailable;
        private readonly Action stopPublication;
        private readonly Action startServices;
        private readonly Action reconcile;
        private readonly IParticipantRestart completedResetRestart;
        private readonly ICompletedParticipantResetReturn completedResetReturn;
        private readonly Action startRuntime;
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
        public bool RequiresLegacyRecovery { get; private set; }
        public bool CanRestartAfterFailure
        {
            get
            {
                if (!BlocksMenu || IsBusy || completedResetReturn != null || RequiresLegacyRecovery) return false;
                try { return coordinator.ReadRecord()?.MappingVersion == ExhibitionResetCoordinator.MappingVersion; }
                catch { return false; }
            }
        }

        public ParticipantResetService(ExhibitionResetCoordinator coordinator, IParticipantRestart restart,
            Func<bool> steamAvailable, Action stopPublication, Action startServices, Action reconcile,
            ResetRecord initialRecord, Exception startupFailure = null,
            IParticipantRestart completedResetRestart = null, ICompletedParticipantResetReturn completedResetReturn = null,
            Action startRuntime = null, bool suppressSaveSeedImport = false)
        {
            this.coordinator = coordinator;
            this.restart = restart;
            this.steamAvailable = steamAvailable;
            this.stopPublication = stopPublication;
            this.startServices = startServices;
            this.reconcile = reconcile;
            this.completedResetRestart = completedResetRestart;
            this.completedResetReturn = completedResetReturn;
            this.startRuntime = startRuntime;
            deferred = initialRecord?.State == ResetRecord.Pending;
            BlocksMenu = deferred || completedResetReturn != null || startupFailure != null;
            SuppressSaveSeedImport = initialRecord != null || suppressSaveSeedImport;
            RequiresLegacyRecovery = startupFailure == null && completedResetReturn == null && deferred &&
                initialRecord.MappingVersion == ExhibitionResetCoordinator.PreviousMappingVersion;
            Error = startupFailure?.Message;
        }

        public async Task PrepareMenuAsync()
        {
            if (prepareAttempted) return;
            prepareAttempted = true;
            if (RequiresLegacyRecovery || !string.IsNullOrEmpty(Error) || (!deferred && completedResetReturn == null)) return;
            IsBusy = true;
            Changed?.Invoke();
            try
            {
                if (completedResetReturn != null)
                {
                    startRuntime?.Invoke();
                    completedResetReturn.Validate(coordinator);
                    startServices();
                    BlocksMenu = false;
                    return;
                }
                await coordinator.ResumeAsync();
                if (completedResetRestart != null)
                {
                    var ready = coordinator.ReadRecord();
                    completedResetRestart.ValidateAvailable();
                    completedResetRestart.Restart(new ResetIdentity(ready.AppId, ready.SteamId));
                    return;
                }
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
                if (deferred || completedResetReturn != null) reconcile();
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
                completedResetRestart?.ValidateAvailable();
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

        public void RequestLegacyReset()
        {
            if (!RequiresLegacyRecovery || IsBusy || requestInProgress) return;
            requestInProgress = true;
            IsBusy = true;
            Error = null;
            Changed?.Invoke();
            try
            {
                restart.ValidateAvailable();
                completedResetRestart?.ValidateAvailable();
                coordinator.RequestLegacyReset();
                RequiresLegacyRecovery = false;
                SuppressSaveSeedImport = true;
                stopPublication();
                RestartCommitted();
            }
            catch (Exception exception)
            {
                // If replacement committed before a cleanup error, only the new request may restart.
                try
                {
                    var actual = coordinator.ReadRecord();
                    RequiresLegacyRecovery = actual?.State == ResetRecord.Pending &&
                        actual.MappingVersion == ExhibitionResetCoordinator.PreviousMappingVersion;
                }
                catch { RequiresLegacyRecovery = false; }
                Error = exception.Message;
                IsBusy = false;
                stopPublication();
            }
            finally
            {
                BlocksMenu = true;
                menuReady = false;
                requestInProgress = false;
                Changed?.Invoke();
            }
        }

        public void Restart()
        {
            if (!CanRestartAfterFailure) return;
            try { RestartCommitted(); }
            catch (Exception exception) { Error = exception.Message; IsBusy = false; Changed?.Invoke(); }
        }

        private void RestartCommitted()
        {
            var record = coordinator.ReadRecord();
            if (record == null) throw new InvalidOperationException("초기화 기록을 확인한 뒤 게임을 다시 실행해 주세요.");
            var selected = record.State == ResetRecord.Ready && completedResetRestart != null ? completedResetRestart : restart;
            selected.ValidateAvailable();
            IsBusy = true;
            Error = null;
            Changed?.Invoke();
            var identity = record.State == ResetRecord.Pending
                ? new ResetIdentity(record.AppId, record.SteamId)
                : coordinator.GetCurrentIdentity();
            selected.Restart(identity);
        }
    }
}
