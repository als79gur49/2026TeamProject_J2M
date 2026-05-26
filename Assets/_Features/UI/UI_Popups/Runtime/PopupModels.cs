using System;
using System.Collections.Generic;

namespace Game.Feature.UI.Popups
{
    public interface IPopupPayload
    {
    }

    public enum PopupCompletionKind
    {
        Closed = 0,
        Cancelled = 1,
        Confirmed = 2,
        Acknowledged = 3,
        Resumed = 4,
        SettingsRequested = 5,
        ObjectiveRequested = 6,
        RetryRequested = 7,
        MainMenuRequested = 8,
    }

    public enum PopupBackdropMode
    {
        None = 0,
        CloseTop = 1,
        Consume = 2,
    }

    public enum TooltipPopupAnchorPreset
    {
        Center = 0,
        UpperRight = 1,
        LowerLeft = 2,
    }

    public interface IPopupView
    {
        event Action<PopupCompletionKind> CompletionRequested;

        bool IsVisible { get; set; }

        void SetIsTopmost(bool isTopmost);
    }

    public sealed class PausePopupPayload : IPopupPayload
    {
        public static readonly PausePopupPayload Default = new(
            "Paused",
            "Pausing modal popup",
            "Resume",
            "Objective",
            "Settings",
            "Retry",
            "Main Menu");

        public PausePopupPayload(
            string titleText,
            string descriptionText,
            string resumeLabel,
            string objectiveLabel,
            string settingsLabel,
            string retryLabel = "Retry",
            string mainMenuLabel = "Main Menu")
        {
            TitleText = titleText ?? string.Empty;
            DescriptionText = descriptionText ?? string.Empty;
            ResumeLabel = resumeLabel ?? string.Empty;
            ObjectiveLabel = objectiveLabel ?? string.Empty;
            SettingsLabel = settingsLabel ?? string.Empty;
            RetryLabel = retryLabel ?? string.Empty;
            MainMenuLabel = mainMenuLabel ?? string.Empty;
        }

        public string TitleText { get; }

        public string DescriptionText { get; }

        public string ResumeLabel { get; }

        public string ObjectiveLabel { get; }

        public string SettingsLabel { get; }

        public string RetryLabel { get; }

        public string MainMenuLabel { get; }
    }

    public sealed class ObjectiveInfoPopupPayload : IPopupPayload
    {
        public ObjectiveInfoPopupPayload(string titleText, string bodyText, string closeLabel = "Close")
        {
            TitleText = titleText ?? string.Empty;
            BodyText = bodyText ?? string.Empty;
            CloseLabel = closeLabel ?? string.Empty;
        }

        public string TitleText { get; }

        public string BodyText { get; }

        public string CloseLabel { get; }
    }

    public sealed class ConfirmPopupPayload : IPopupPayload
    {
        public ConfirmPopupPayload(
            string titleText,
            string bodyText,
            string confirmLabel,
            string cancelLabel,
            bool isConfirmDestructive)
        {
            TitleText = titleText ?? string.Empty;
            BodyText = bodyText ?? string.Empty;
            ConfirmLabel = confirmLabel ?? string.Empty;
            CancelLabel = cancelLabel ?? string.Empty;
            IsConfirmDestructive = isConfirmDestructive;
        }

        public string TitleText { get; }

        public string BodyText { get; }

        public string ConfirmLabel { get; }

        public string CancelLabel { get; }

        public bool IsConfirmDestructive { get; }
    }

    public sealed class TooltipPopupPayload : IPopupPayload
    {
        public TooltipPopupPayload(
            string titleText,
            string bodyText,
            TooltipPopupAnchorPreset anchorPreset = TooltipPopupAnchorPreset.Center)
        {
            TitleText = titleText ?? string.Empty;
            BodyText = bodyText ?? string.Empty;
            AnchorPreset = anchorPreset;
        }

        public string TitleText { get; }

        public string BodyText { get; }

        public TooltipPopupAnchorPreset AnchorPreset { get; }
    }

    public readonly struct RewardPopupItemPayload
    {
        public RewardPopupItemPayload(string labelText, int amount)
        {
            LabelText = labelText ?? string.Empty;
            Amount = amount;
        }

        public string LabelText { get; }

        public int Amount { get; }
    }

    public sealed class RewardPopupPayload : IPopupPayload
    {
        public RewardPopupPayload(
            string titleText,
            IEnumerable<RewardPopupItemPayload> items,
            string summaryText,
            string closeLabel)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            TitleText = titleText ?? string.Empty;
            SummaryText = summaryText ?? string.Empty;
            CloseLabel = closeLabel ?? string.Empty;
            Items = new List<RewardPopupItemPayload>(items).ToArray();
        }

        public string TitleText { get; }

        public IReadOnlyList<RewardPopupItemPayload> Items { get; }

        public string SummaryText { get; }

        public string CloseLabel { get; }
    }

    public sealed class DebugCommandsPopupPayload : IPopupPayload
    {
        public DebugCommandsPopupPayload(
            string titleText,
            string debugBuildStatusText,
            string currentStageText,
            string nextStageText,
            string lockStatusText,
            string forceClearAvailabilityText,
            string lastCommandMessage,
            bool canGoNextStage,
            bool canForceClearResultOnly)
        {
            TitleText = titleText ?? string.Empty;
            DebugBuildStatusText = debugBuildStatusText ?? string.Empty;
            CurrentStageText = currentStageText ?? string.Empty;
            NextStageText = nextStageText ?? string.Empty;
            LockStatusText = lockStatusText ?? string.Empty;
            ForceClearAvailabilityText = forceClearAvailabilityText ?? string.Empty;
            LastCommandMessage = lastCommandMessage ?? string.Empty;
            CanGoNextStage = canGoNextStage;
            CanForceClearResultOnly = canForceClearResultOnly;
        }

        public string TitleText { get; }

        public string DebugBuildStatusText { get; }

        public string CurrentStageText { get; }

        public string NextStageText { get; }

        public string LockStatusText { get; }

        public string ForceClearAvailabilityText { get; }

        public string LastCommandMessage { get; }

        public bool CanGoNextStage { get; }

        public bool CanForceClearResultOnly { get; }
    }

    public sealed class PausePopupViewModel
    {
        public event Action Changed;

        public string TitleText { get; private set; } = string.Empty;

        public string DescriptionText { get; private set; } = string.Empty;

        public string ResumeLabel { get; private set; } = string.Empty;

        public string ObjectiveLabel { get; private set; } = string.Empty;

        public string SettingsLabel { get; private set; } = string.Empty;

        public string RetryLabel { get; private set; } = string.Empty;

        public string MainMenuLabel { get; private set; } = string.Empty;

        public void SetContent(
            string titleText,
            string descriptionText,
            string resumeLabel,
            string objectiveLabel,
            string settingsLabel,
            string retryLabel,
            string mainMenuLabel)
        {
            TitleText = titleText ?? string.Empty;
            DescriptionText = descriptionText ?? string.Empty;
            ResumeLabel = resumeLabel ?? string.Empty;
            ObjectiveLabel = objectiveLabel ?? string.Empty;
            SettingsLabel = settingsLabel ?? string.Empty;
            RetryLabel = retryLabel ?? string.Empty;
            MainMenuLabel = mainMenuLabel ?? string.Empty;
            Changed?.Invoke();
        }
    }

    public sealed class ObjectiveInfoPopupViewModel
    {
        public event Action Changed;

        public string TitleText { get; private set; } = string.Empty;

        public string BodyText { get; private set; } = string.Empty;

        public string CloseLabel { get; private set; } = string.Empty;

        public void SetContent(string titleText, string bodyText, string closeLabel)
        {
            TitleText = titleText ?? string.Empty;
            BodyText = bodyText ?? string.Empty;
            CloseLabel = closeLabel ?? string.Empty;
            Changed?.Invoke();
        }
    }

    public sealed class ConfirmPopupViewModel
    {
        public event Action Changed;

        public string TitleText { get; private set; } = string.Empty;

        public string BodyText { get; private set; } = string.Empty;

        public string ConfirmLabel { get; private set; } = string.Empty;

        public string CancelLabel { get; private set; } = string.Empty;

        public bool IsConfirmDestructive { get; private set; }

        public void SetContent(
            string titleText,
            string bodyText,
            string confirmLabel,
            string cancelLabel,
            bool isConfirmDestructive)
        {
            TitleText = titleText ?? string.Empty;
            BodyText = bodyText ?? string.Empty;
            ConfirmLabel = confirmLabel ?? string.Empty;
            CancelLabel = cancelLabel ?? string.Empty;
            IsConfirmDestructive = isConfirmDestructive;
            Changed?.Invoke();
        }
    }

    public sealed class TooltipPopupViewModel
    {
        public event Action Changed;

        public string TitleText { get; private set; } = string.Empty;

        public string BodyText { get; private set; } = string.Empty;

        public TooltipPopupAnchorPreset AnchorPreset { get; private set; }

        public void SetContent(
            string titleText,
            string bodyText,
            TooltipPopupAnchorPreset anchorPreset)
        {
            TitleText = titleText ?? string.Empty;
            BodyText = bodyText ?? string.Empty;
            AnchorPreset = anchorPreset;
            Changed?.Invoke();
        }
    }

    public sealed class RewardPopupViewModel
    {
        public event Action Changed;

        public string TitleText { get; private set; } = string.Empty;

        public string SummaryText { get; private set; } = string.Empty;

        public string CloseLabel { get; private set; } = string.Empty;

        public string[] ItemLines { get; private set; } = Array.Empty<string>();

        public void SetContent(
            string titleText,
            IEnumerable<string> itemLines,
            string summaryText,
            string closeLabel)
        {
            if (itemLines == null)
            {
                throw new ArgumentNullException(nameof(itemLines));
            }

            TitleText = titleText ?? string.Empty;
            SummaryText = summaryText ?? string.Empty;
            CloseLabel = closeLabel ?? string.Empty;
            ItemLines = new List<string>(itemLines).ToArray();
            Changed?.Invoke();
        }
    }

    public sealed class DebugCommandsPopupViewModel
    {
        public event Action Changed;

        public string TitleText { get; private set; } = string.Empty;

        public string DebugBuildStatusText { get; private set; } = string.Empty;

        public string CurrentStageText { get; private set; } = string.Empty;

        public string NextStageText { get; private set; } = string.Empty;

        public string LockStatusText { get; private set; } = string.Empty;

        public string ForceClearAvailabilityText { get; private set; } = string.Empty;

        public string LastCommandMessage { get; private set; } = string.Empty;

        public string NextStageLabel { get; private set; } = "Next Stage";

        public string ForceClearResultOnlyLabel { get; private set; } = "Force Clear Result Only - NO SAVE / NO REWARD";

        public string CloseLabel { get; private set; } = "Close";

        public bool CanGoNextStage { get; private set; }

        public bool CanForceClearResultOnly { get; private set; }

        public void SetContent(DebugCommandsPopupPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            TitleText = payload.TitleText;
            DebugBuildStatusText = payload.DebugBuildStatusText;
            CurrentStageText = payload.CurrentStageText;
            NextStageText = payload.NextStageText;
            LockStatusText = payload.LockStatusText;
            ForceClearAvailabilityText = payload.ForceClearAvailabilityText;
            LastCommandMessage = payload.LastCommandMessage;
            CanGoNextStage = payload.CanGoNextStage;
            CanForceClearResultOnly = payload.CanForceClearResultOnly;
            Changed?.Invoke();
        }
    }
}
