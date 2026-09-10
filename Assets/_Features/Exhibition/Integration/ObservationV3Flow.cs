using System;
using System.Linq;
using System.Threading.Tasks;
using Game.Feature.UI.Application;
using Game.Exhibition.RestartExperiment;

namespace Game.Exhibition.Integration
{
    public interface IObservationV3PlayerPorts
    {
        Task Prepare();
        string[] Targets { get; }
        bool CanRestart { get; }
        Task StoreVisibility(string value, string text, string observedUtc);
        Task StoreDisplay(ObservationV3TargetDisplay[] targets, string text, string observedUtc);
        Task FullCycle();
        Task Close();
    }
    public sealed class ObservationV3Flow : IParticipantResetPort, IParticipantResetActionPresentation, IParticipantResetStatusPresentation
    {
        private readonly IObservationV3PlayerPorts ports;
        private bool prepared, preparing, busy, closing, claimed, opened, originDisplayKnown;
        private bool sessionCanReport = true;
        private Task closeTask;
        public string CloseLabel { get; private set; } = "보고 저장 확인 후 종료";
        public readonly OverlayObservationRole Role;
        public event Action Changed;
        public string Error { get; private set; }
        public string Status { get; private set; } = "읽기 준비 중";
        public bool BlocksMenu => true;
        public bool SuppressSaveSeedImport => true;
        public bool HideResetAction => true;
        public bool OwnsStatusPresentation => true;
        public bool CanRequest => false;
        public bool IsBusy => busy || preparing;
        public bool CanReport => sessionCanReport && prepared && !busy && !closing && !claimed && Error == null;
        public bool CanDisplay => CanReport && opened;
        public bool CanFullCycle => CanReport && Role == OverlayObservationRole.OriginObserver && originDisplayKnown && ports.CanRestart;
        public string[] Targets => ports.Targets ?? Array.Empty<string>();
        public ObservationV3Flow(IObservationV3PlayerPorts ports, OverlayObservationRole role) { this.ports = ports; Role = role; }
        public async Task PrepareMenuAsync()
        {
            if (preparing || prepared || Error != null) return;
            preparing = true; Notify();
            try { await ports.Prepare(); if (!closing) { prepared = true; Status = "Overlay 관찰 입력 가능"; } }
            catch (Exception e) { Fail(e); }
            finally { preparing = false; Notify(); }
        }
        public async Task ReportVisibility(string value, string text, string observedUtc = null)
        {
            if (!CanReport) return;
            busy = true; originDisplayKnown = false; opened = false; Notify();
            try { await ports.StoreVisibility(value, text, observedUtc); opened = value == "opened"; Status = "표시 여부 저장 완료"; }
            catch (Exception e) { Fail(e); }
            finally { busy = false; Notify(); }
        }
        public async Task ReportDisplay(ObservationV3TargetDisplay[] values, string text, string observedUtc = null)
        {
            if (!CanDisplay) return;
            busy = true; originDisplayKnown = false; Notify();
            try
            {
                await ports.StoreDisplay(values, text, observedUtc);
                originDisplayKnown = values.Length == Targets.Length && values.All(v => v.Display == "earned" || v.Display == "unearned");
                Status = "대상별 표시 저장 완료";

            }
            catch (Exception e) { Fail(e); }
            finally { busy = false; Notify(); }
        }
        public async Task FullCycleAsync()
        {
            if (!CanFullCycle) return;
            claimed = true; busy = true; Status = "단회 Steam 재시작 인계 중"; Notify();
            try { await ports.FullCycle(); }
            catch (Exception e) { Fail(e); }
            finally { busy = false; Notify(); }
        }
        // Closing remains available during report I/O. The runtime drains to the submitted boundary.
        public Task CloseAsync()
        {
            if (closeTask != null) return closeTask;
            closing = true; Status = CloseLabel; Notify();
            closeTask = ports.Close(); return closeTask;
        }
        public void SessionChanged(ObservationV3SessionStage state, bool canReport, Exception error)
        {
            sessionCanReport = canReport;
            if (error != null) { Fail(error); return; }
            if (state == ObservationV3SessionStage.ClosingCancelledBeforeCommit || state == ObservationV3SessionStage.Closing || state == ObservationV3SessionStage.QuitAllowed) closing = true;
            switch (state)
            {
                case ObservationV3SessionStage.HandoffPreparing:
                case ObservationV3SessionStage.ClosingCancelledBeforeCommit: CloseLabel = "인계 취소 후 종료"; break;
                case ObservationV3SessionStage.GrantCommitInFlight: CloseLabel = "인계 전달 확인 중"; break;
                case ObservationV3SessionStage.GrantConfirmed: CloseLabel = "단회 인계 후 종료"; break;
            }
            if (closing || state == ObservationV3SessionStage.GrantCommitInFlight || state == ObservationV3SessionStage.GrantConfirmed) Status = CloseLabel;
            Notify();
        }
        public void Fail(Exception e) { if (Error == null) Error = e.Message; Status = "관찰 중단"; Notify(); }
        private void Notify() { Changed?.Invoke(); }
        public void CompleteMenuInitialization() { }
        public void LeaveMenu() { }
        public void FailMenuInitialization(string reason) { Fail(new InvalidOperationException(reason)); }
        public void RequestReset() { }
        public void Restart() { }
    }
}
