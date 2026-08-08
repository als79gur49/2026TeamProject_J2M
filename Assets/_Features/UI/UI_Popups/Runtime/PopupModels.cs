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
            PauseProgressionSnapshot progression = null,
            LocalizedTextDescriptor titleTextDescriptor = default,
            LocalizedTextDescriptor resumeLabelDescriptor = default,
            LocalizedTextDescriptor settingsLabelDescriptor = default,
            LocalizedTextDescriptor retryLabelDescriptor = default,
            LocalizedTextDescriptor mainMenuLabelDescriptor = default)
        {
            Progression = progression ?? PauseProgressionSnapshot.Unavailable;
            TitleTextDescriptor = OrDefault(titleTextDescriptor, PauseStaticTextDescriptors.Title);
            ResumeLabelDescriptor = OrDefault(resumeLabelDescriptor, PauseStaticTextDescriptors.Resume);
            SettingsLabelDescriptor = OrDefault(settingsLabelDescriptor, PauseStaticTextDescriptors.Settings);
            RetryLabelDescriptor = OrDefault(retryLabelDescriptor, PauseStaticTextDescriptors.Retry);
            MainMenuLabelDescriptor = OrDefault(mainMenuLabelDescriptor, PauseStaticTextDescriptors.MainMenu);
        }

        public PauseProgressionSnapshot Progression { get; }

        public LocalizedTextDescriptor TitleTextDescriptor { get; }

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

    public readonly struct PauseProgressionStageSnapshot
    {
        public PauseProgressionStageSnapshot(string stageKey, string groupKey)
        {
            StageKey = stageKey ?? string.Empty;
            GroupKey = groupKey ?? string.Empty;
        }

        public string StageKey { get; }

        public string GroupKey { get; }
    }

    public sealed class PauseProgressionSnapshot
    {
        public static readonly PauseProgressionSnapshot Unavailable = new(
            isAvailable: false,
            Array.Empty<PauseProgressionStageSnapshot>(),
            string.Empty);

        private readonly IReadOnlyList<PauseProgressionStageSnapshot> _stages;

        public PauseProgressionSnapshot(
            bool isAvailable,
            IReadOnlyList<PauseProgressionStageSnapshot> stages,
            string currentStageKey)
        {
            IsAvailable = isAvailable;
            CurrentStageKey = currentStageKey ?? string.Empty;
            if (stages == null || stages.Count == 0)
            {
                _stages = Array.Empty<PauseProgressionStageSnapshot>();
                return;
            }

            var copiedStages = new PauseProgressionStageSnapshot[stages.Count];
            for (var i = 0; i < stages.Count; i++)
            {
                copiedStages[i] = stages[i];
            }

            _stages = Array.AsReadOnly(copiedStages);
        }

        public bool IsAvailable { get; }

        public IReadOnlyList<PauseProgressionStageSnapshot> Stages => _stages;

        public string CurrentStageKey { get; }
    }

    public enum PauseProgressionMarkerKind
    {
        GroupStart = 0,
        Stage = 1,
    }

    public readonly struct PauseProgressionMarkerModel
    {
        public PauseProgressionMarkerModel(string stageKey, PauseProgressionMarkerKind kind)
        {
            StageKey = stageKey ?? string.Empty;
            Kind = kind;
        }

        public string StageKey { get; }

        public PauseProgressionMarkerKind Kind { get; }
    }

    public sealed class PauseProgressionViewModel
    {
        public static readonly PauseProgressionViewModel Hidden = new(
            isVisible: false,
            Array.Empty<PauseProgressionMarkerModel>(),
            currentIndex: -1);

        private readonly IReadOnlyList<PauseProgressionMarkerModel> _markers;

        public PauseProgressionViewModel(
            bool isVisible,
            IReadOnlyList<PauseProgressionMarkerModel> markers,
            int currentIndex)
        {
            IsVisible = isVisible;
            CurrentIndex = currentIndex;
            if (markers == null || markers.Count == 0)
            {
                _markers = Array.Empty<PauseProgressionMarkerModel>();
                return;
            }

            var copiedMarkers = new PauseProgressionMarkerModel[markers.Count];
            for (var i = 0; i < markers.Count; i++)
            {
                copiedMarkers[i] = markers[i];
            }

            _markers = Array.AsReadOnly(copiedMarkers);
        }

        public bool IsVisible { get; }

        public IReadOnlyList<PauseProgressionMarkerModel> Markers => _markers;

        public int CurrentIndex { get; }
    }

    public sealed class ConfirmPopupPayload : IPopupPayload
    {
        public ConfirmPopupPayload(
            string titleText,
            string bodyText,
            string confirmLabel,
            string cancelLabel,
            bool isConfirmDestructive)
            : this(
                titleText,
                bodyText,
                string.Empty,
                confirmLabel,
                cancelLabel,
                isConfirmDestructive)
        {
        }

        public ConfirmPopupPayload(
            string titleText,
            string bodyText,
            string warningText,
            string confirmLabel,
            string cancelLabel,
            bool isConfirmDestructive)
        {
            TitleText = titleText ?? string.Empty;
            BodyText = bodyText ?? string.Empty;
            WarningText = warningText ?? string.Empty;
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
            : this(
                titleTextDescriptor,
                bodyTextDescriptor,
                default,
                confirmLabelDescriptor,
                cancelLabelDescriptor,
                isConfirmDestructive)
        {
        }

        public ConfirmPopupPayload(
            LocalizedTextDescriptor titleTextDescriptor,
            LocalizedTextDescriptor bodyTextDescriptor,
            LocalizedTextDescriptor warningTextDescriptor,
            LocalizedTextDescriptor confirmLabelDescriptor,
            LocalizedTextDescriptor cancelLabelDescriptor,
            bool isConfirmDestructive)
        {
            TitleTextDescriptor = titleTextDescriptor;
            BodyTextDescriptor = bodyTextDescriptor;
            WarningTextDescriptor = warningTextDescriptor;
            ConfirmLabelDescriptor = confirmLabelDescriptor;
            CancelLabelDescriptor = cancelLabelDescriptor;
            TitleText = string.Empty;
            BodyText = string.Empty;
            WarningText = string.Empty;
            ConfirmLabel = string.Empty;
            CancelLabel = string.Empty;
            IsConfirmDestructive = isConfirmDestructive;
        }

        public string TitleText { get; }

        public string BodyText { get; }

        public string WarningText { get; }

        public string ConfirmLabel { get; }

        public string CancelLabel { get; }

        public LocalizedTextDescriptor TitleTextDescriptor { get; }

        public LocalizedTextDescriptor BodyTextDescriptor { get; }

        public LocalizedTextDescriptor WarningTextDescriptor { get; }

        public LocalizedTextDescriptor ConfirmLabelDescriptor { get; }

        public LocalizedTextDescriptor CancelLabelDescriptor { get; }

        public bool IsConfirmDestructive { get; }
    }

    public sealed class PausePopupViewModel
    {
        public event Action Changed;

        public string TitleText { get; private set; } = string.Empty;

        public string ResumeLabel { get; private set; } = string.Empty;

        public string SettingsLabel { get; private set; } = string.Empty;

        public string RetryLabel { get; private set; } = string.Empty;

        public string MainMenuLabel { get; private set; } = string.Empty;

        public PauseProgressionViewModel Progression { get; private set; } = PauseProgressionViewModel.Hidden;

        public void SetContent(
            string titleText,
            string resumeLabel,
            string settingsLabel,
            string retryLabel,
            string mainMenuLabel,
            PauseProgressionViewModel progression)
        {
            TitleText = titleText ?? string.Empty;
            ResumeLabel = resumeLabel ?? string.Empty;
            SettingsLabel = settingsLabel ?? string.Empty;
            RetryLabel = retryLabel ?? string.Empty;
            MainMenuLabel = mainMenuLabel ?? string.Empty;
            Progression = progression ?? PauseProgressionViewModel.Hidden;
            Changed?.Invoke();
        }
    }

    public sealed class ConfirmPopupViewModel
    {
        public event Action Changed;

        public string TitleText { get; private set; } = string.Empty;

        public string BodyText { get; private set; } = string.Empty;

        public string WarningText { get; private set; } = string.Empty;

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
            SetContent(
                titleText,
                bodyText,
                string.Empty,
                confirmLabel,
                cancelLabel,
                isConfirmDestructive);
        }

        public void SetContent(
            string titleText,
            string bodyText,
            string warningText,
            string confirmLabel,
            string cancelLabel,
            bool isConfirmDestructive)
        {
            TitleText = titleText ?? string.Empty;
            BodyText = bodyText ?? string.Empty;
            WarningText = warningText ?? string.Empty;
            ConfirmLabel = confirmLabel ?? string.Empty;
            CancelLabel = cancelLabel ?? string.Empty;
            IsConfirmDestructive = isConfirmDestructive;
            Changed?.Invoke();
        }
    }

}
