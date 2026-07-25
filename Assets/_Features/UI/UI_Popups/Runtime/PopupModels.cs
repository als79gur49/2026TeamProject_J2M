using System;
using System.Collections.Generic;
using Game.Feature.UI.ViewShared;

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
        Resumed = 4,
        SettingsRequested = 5,
        RetryRequested = 7,
        MainMenuRequested = 8,
    }

    public enum PopupBackdropMode
    {
        None = 0,
        CloseTop = 1,
        Consume = 2,
    }

    public interface IPopupView
    {
        event Action<PopupCompletionKind> CompletionRequested;

        bool IsVisible { get; set; }

        void SetIsTopmost(bool isTopmost);
    }

    public sealed class PausePopupPayload : IPopupPayload
    {
        public static readonly PausePopupPayload Default = new();

        public PausePopupPayload(
            LocalizedTextDescriptor titleTextDescriptor = default,
            LocalizedTextDescriptor descriptionTextDescriptor = default,
            LocalizedTextDescriptor resumeLabelDescriptor = default,
            LocalizedTextDescriptor settingsLabelDescriptor = default,
            LocalizedTextDescriptor retryLabelDescriptor = default,
            LocalizedTextDescriptor mainMenuLabelDescriptor = default)
        {
            TitleTextDescriptor = OrDefault(titleTextDescriptor, PauseStaticTextDescriptors.Title);
            DescriptionTextDescriptor = OrDefault(descriptionTextDescriptor, PauseStaticTextDescriptors.Description);
            ResumeLabelDescriptor = OrDefault(resumeLabelDescriptor, PauseStaticTextDescriptors.Resume);
            SettingsLabelDescriptor = OrDefault(settingsLabelDescriptor, PauseStaticTextDescriptors.Settings);
            RetryLabelDescriptor = OrDefault(retryLabelDescriptor, PauseStaticTextDescriptors.Retry);
            MainMenuLabelDescriptor = OrDefault(mainMenuLabelDescriptor, PauseStaticTextDescriptors.MainMenu);
        }

        public LocalizedTextDescriptor TitleTextDescriptor { get; }

        public LocalizedTextDescriptor DescriptionTextDescriptor { get; }

        public LocalizedTextDescriptor ResumeLabelDescriptor { get; }

        public LocalizedTextDescriptor SettingsLabelDescriptor { get; }

        public LocalizedTextDescriptor RetryLabelDescriptor { get; }

        public LocalizedTextDescriptor MainMenuLabelDescriptor { get; }

        private static LocalizedTextDescriptor OrDefault(
            LocalizedTextDescriptor descriptor,
            LocalizedTextDescriptor fallback)
        {
            return string.IsNullOrEmpty(descriptor.Table) && string.IsNullOrEmpty(descriptor.Key)
                ? fallback
                : descriptor;
        }
    }

    public static class PauseStaticTextDescriptors
    {
        public const string Table = "UI";

        public static readonly LocalizedTextDescriptor Title = new(
            Table,
            "ui.pause.title",
            LocalizedTextRole.Title,
            LocalizedTextWeight.Bold);

        public static readonly LocalizedTextDescriptor Description = new(
            Table,
            "ui.pause.description",
            LocalizedTextRole.Body,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor Resume = new(
            Table,
            "ui.pause.resume",
            LocalizedTextRole.Button,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor Settings = new(
            Table,
            "ui.common.settings",
            LocalizedTextRole.Button,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor Retry = new(
            Table,
            "ui.pause.retry",
            LocalizedTextRole.Button,
            LocalizedTextWeight.Regular);

        public static readonly LocalizedTextDescriptor MainMenu = new(
            Table,
            "ui.pause.main_menu",
            LocalizedTextRole.Button,
            LocalizedTextWeight.Regular);
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

        public ConfirmPopupPayload(
            LocalizedTextDescriptor titleTextDescriptor,
            LocalizedTextDescriptor bodyTextDescriptor,
            LocalizedTextDescriptor confirmLabelDescriptor,
            LocalizedTextDescriptor cancelLabelDescriptor,
            bool isConfirmDestructive)
        {
            TitleTextDescriptor = titleTextDescriptor;
            BodyTextDescriptor = bodyTextDescriptor;
            ConfirmLabelDescriptor = confirmLabelDescriptor;
            CancelLabelDescriptor = cancelLabelDescriptor;
            TitleText = string.Empty;
            BodyText = string.Empty;
            ConfirmLabel = string.Empty;
            CancelLabel = string.Empty;
            IsConfirmDestructive = isConfirmDestructive;
        }

        public string TitleText { get; }

        public string BodyText { get; }

        public string ConfirmLabel { get; }

        public string CancelLabel { get; }

        public LocalizedTextDescriptor TitleTextDescriptor { get; }

        public LocalizedTextDescriptor BodyTextDescriptor { get; }

        public LocalizedTextDescriptor ConfirmLabelDescriptor { get; }

        public LocalizedTextDescriptor CancelLabelDescriptor { get; }

        public bool IsConfirmDestructive { get; }
    }

    public sealed class PausePopupViewModel
    {
        public event Action Changed;

        public string TitleText { get; private set; } = string.Empty;

        public string DescriptionText { get; private set; } = string.Empty;

        public string ResumeLabel { get; private set; } = string.Empty;

        public string SettingsLabel { get; private set; } = string.Empty;

        public string RetryLabel { get; private set; } = string.Empty;

        public string MainMenuLabel { get; private set; } = string.Empty;

        public void SetContent(
            string titleText,
            string descriptionText,
            string resumeLabel,
            string settingsLabel,
            string retryLabel,
            string mainMenuLabel)
        {
            TitleText = titleText ?? string.Empty;
            DescriptionText = descriptionText ?? string.Empty;
            ResumeLabel = resumeLabel ?? string.Empty;
            SettingsLabel = settingsLabel ?? string.Empty;
            RetryLabel = retryLabel ?? string.Empty;
            MainMenuLabel = mainMenuLabel ?? string.Empty;
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

}
