using Game.Feature.Stages;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Application
{
    public enum SaveSlotRepositoryOperation
    {
        LoadAllWithReport = 0,
    }

    public readonly struct SaveSlotFailureDiagnostic
    {
        public SaveSlotFailureDiagnostic(
            SaveSlotFailurePresentationKind failureKind,
            CampaignSaveLoadStatus loadStatus,
            string reason,
            int slotNumber,
            SaveSlotRepositoryOperation operation)
        {
            FailureKind = failureKind;
            LoadStatus = loadStatus;
            Reason = reason ?? string.Empty;
            SlotNumber = slotNumber;
            Operation = operation;
        }

        public SaveSlotFailurePresentationKind FailureKind { get; }

        public CampaignSaveLoadStatus LoadStatus { get; }

        public string Reason { get; }

        public int SlotNumber { get; }

        public SaveSlotRepositoryOperation Operation { get; }
    }

    public interface IMainMenuSaveDiagnosticPort
    {
        void Report(SaveSlotFailureDiagnostic diagnostic);
    }

    public sealed class NoOpMainMenuSaveDiagnosticPort : IMainMenuSaveDiagnosticPort
    {
        public static readonly NoOpMainMenuSaveDiagnosticPort Instance = new();

        private NoOpMainMenuSaveDiagnosticPort()
        {
        }

        public void Report(SaveSlotFailureDiagnostic diagnostic)
        {
        }
    }

    public interface IMainMenuSettingsPort
    {
        void OpenSettings();
    }

    public sealed class NoOpMainMenuSettingsPort : IMainMenuSettingsPort
    {
        public static readonly NoOpMainMenuSettingsPort Instance = new();

        private NoOpMainMenuSettingsPort()
        {
        }

        public void OpenSettings()
        {
        }
    }

    public interface IApplicationQuitPort
    {
        void Quit();
    }
}
