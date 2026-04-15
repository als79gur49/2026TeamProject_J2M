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
            "Resume");

        public PausePopupPayload(string titleText, string descriptionText, string resumeLabel)
        {
            TitleText = titleText ?? string.Empty;
            DescriptionText = descriptionText ?? string.Empty;
            ResumeLabel = resumeLabel ?? string.Empty;
        }

        public string TitleText { get; }

        public string DescriptionText { get; }

        public string ResumeLabel { get; }
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

    public sealed class PausePopupViewModel
    {
        public event Action Changed;

        public string TitleText { get; private set; } = string.Empty;

        public string DescriptionText { get; private set; } = string.Empty;

        public string ResumeLabel { get; private set; } = string.Empty;

        public void SetContent(string titleText, string descriptionText, string resumeLabel)
        {
            TitleText = titleText ?? string.Empty;
            DescriptionText = descriptionText ?? string.Empty;
            ResumeLabel = resumeLabel ?? string.Empty;
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
}
