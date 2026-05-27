using System;
using System.Collections.Generic;
using Game.Feature.Stages;

namespace Game.Feature.DemoStageControl
{
    public interface IDemoStageControlCommandPort
    {
        IReadOnlyList<DemoStageControlStageItem> GetStages();

        DemoStageControlStatus GetStatus();

        DemoStageControlResult StartStage(StageId stageId);

        DemoStageControlResult ForceClearCurrentStage();
    }

    public interface IDemoStageControlCampaignBridge
    {
        StageId CurrentStageId { get; }

        bool TrySetActiveStage(StageContentEntry entry, out string message);

        bool IsUnlocked(StageContentEntry entry);
    }

    public interface IDemoStageControlLaunchBridge
    {
        bool IsSceneTransitionInProgress { get; }

        DemoStageControlResult Launch(StageId stageId);
    }

    public interface IDemoStageControlCompletionBridge
    {
        bool IsCompletionInProgress { get; }

        DemoStageControlResult ForceClearCurrentStage();
    }

    public interface IDemoStageControlGameplayContextProvider
    {
        bool TryCreateDemoStageControlContext(out DemoStageControlGameplayContext context);
    }

    public readonly struct DemoStageControlGameplayContext
    {
        public DemoStageControlGameplayContext(
            IStageCatalogProvider stageCatalogProvider,
            IDemoStageControlCampaignBridge campaignBridge)
        {
            StageCatalogProvider = stageCatalogProvider;
            CampaignBridge = campaignBridge;
        }

        public IStageCatalogProvider StageCatalogProvider { get; }

        public IDemoStageControlCampaignBridge CampaignBridge { get; }

        public bool IsValid => StageCatalogProvider != null && CampaignBridge != null;
    }

    public enum DemoStageControlOpenKey
    {
        F10 = 0,
        BackQuote = 1,
    }

    [Serializable]
    public sealed class DemoStageControlSettings
    {
        public bool Enabled = true;
        public DemoStageControlOpenKey OpenKey = DemoStageControlOpenKey.F10;
        public bool AllowLockedStageSelection = true;
        public bool ShowHotkeyHint;

        public static DemoStageControlSettings EnabledByDefault()
        {
            return new DemoStageControlSettings
            {
                Enabled = true,
                OpenKey = DemoStageControlOpenKey.F10,
                AllowLockedStageSelection = true,
                ShowHotkeyHint = false,
            };
        }
    }

    public readonly struct DemoStageControlStageItem
    {
        public DemoStageControlStageItem(
            StageId stageId,
            string displayName,
            bool isCurrent,
            bool isUnlocked)
        {
            StageId = stageId;
            DisplayName = displayName ?? string.Empty;
            IsCurrent = isCurrent;
            IsUnlocked = isUnlocked;
        }

        public StageId StageId { get; }

        public string DisplayName { get; }

        public bool IsCurrent { get; }

        public bool IsUnlocked { get; }
    }

    public readonly struct DemoStageControlStatus
    {
        public DemoStageControlStatus(
            StageId currentStageId,
            StageId campaignActiveStageId,
            bool isSceneTransitionInProgress,
            bool isCompletionInProgress,
            string lastResultMessage)
        {
            CurrentStageId = currentStageId;
            CampaignActiveStageId = campaignActiveStageId;
            IsSceneTransitionInProgress = isSceneTransitionInProgress;
            IsCompletionInProgress = isCompletionInProgress;
            LastResultMessage = lastResultMessage ?? string.Empty;
        }

        public StageId CurrentStageId { get; }

        public StageId CampaignActiveStageId { get; }

        public bool IsSceneTransitionInProgress { get; }

        public bool IsCompletionInProgress { get; }

        public string LastResultMessage { get; }
    }

    public readonly struct DemoStageControlResult
    {
        private DemoStageControlResult(bool success, string message)
        {
            Success = success;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }

        public string Message { get; }

        public static DemoStageControlResult Ok(string message)
        {
            return new DemoStageControlResult(true, message);
        }

        public static DemoStageControlResult Fail(string message)
        {
            return new DemoStageControlResult(false, message);
        }
    }
}
