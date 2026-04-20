using System;
using System.Collections.Generic;

namespace Game.Feature.UI.Screens
{
    public interface IScreenPayload
    {
    }

    public interface IScreenView
    {
        void SetIsCurrent(bool isCurrent);
    }

    public sealed class GameplayScreenPayload : IScreenPayload
    {
        public static readonly GameplayScreenPayload Default = new(
            "Gameplay Screen",
            "Help",
            "Objectives",
            "Inventory",
            "Settings");

        public GameplayScreenPayload(
            string titleText,
            string helpLabel,
            string objectivesLabel,
            string inventoryLabel,
            string settingsLabel)
        {
            TitleText = titleText ?? string.Empty;
            HelpLabel = helpLabel ?? string.Empty;
            ObjectivesLabel = objectivesLabel ?? string.Empty;
            InventoryLabel = inventoryLabel ?? string.Empty;
            SettingsLabel = settingsLabel ?? string.Empty;
        }

        public string TitleText { get; }

        public string HelpLabel { get; }

        public string ObjectivesLabel { get; }

        public string InventoryLabel { get; }

        public string SettingsLabel { get; }
    }

    public sealed class HelpScreenPayload : IScreenPayload
    {
        public static readonly HelpScreenPayload Default = new(
            "Help & Controls",
            "Use Move Up to advance, Flip Right to rotate, Objectives to review stage status, Inventory to inspect the validation list, and Back to return to gameplay.",
            "Back");

        public HelpScreenPayload(
            string titleText,
            string descriptionText,
            string backLabel)
        {
            TitleText = titleText ?? string.Empty;
            DescriptionText = descriptionText ?? string.Empty;
            BackLabel = backLabel ?? string.Empty;
        }

        public string TitleText { get; }

        public string DescriptionText { get; }

        public string BackLabel { get; }
    }

    public sealed class ObjectiveStatusScreenPayload : IScreenPayload
    {
        public static readonly ObjectiveStatusScreenPayload Default = new("Objective Status");

        public ObjectiveStatusScreenPayload(string titleText)
        {
            TitleText = titleText ?? string.Empty;
        }

        public string TitleText { get; }
    }

    public enum InventoryItemCategory
    {
        Consumable = 0,
        Utility = 1,
        KeyItem = 2,
    }

    public sealed class InventoryScreenActionPayload
    {
        public InventoryScreenActionPayload(
            string actionId,
            string labelText,
            string previewFeedbackText,
            bool isEnabled = true,
            string disabledReasonText = null)
        {
            ActionId = actionId ?? string.Empty;
            LabelText = labelText ?? string.Empty;
            PreviewFeedbackText = previewFeedbackText ?? string.Empty;
            IsEnabled = isEnabled;
            DisabledReasonText = disabledReasonText ?? string.Empty;
        }

        public string ActionId { get; }

        public string LabelText { get; }

        public string PreviewFeedbackText { get; }

        public bool IsEnabled { get; }

        public string DisabledReasonText { get; }
    }

    public sealed class InventoryScreenItemPayload
    {
        public InventoryScreenItemPayload(
            string itemId,
            string labelText,
            InventoryItemCategory category,
            int amount,
            string descriptionText,
            string detailText,
            IReadOnlyList<InventoryScreenActionPayload> actions)
        {
            ItemId = itemId ?? string.Empty;
            LabelText = labelText ?? string.Empty;
            Category = category;
            Amount = amount;
            DescriptionText = descriptionText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            Actions = actions ?? Array.Empty<InventoryScreenActionPayload>();
        }

        public string ItemId { get; }

        public string LabelText { get; }

        public InventoryItemCategory Category { get; }

        public int Amount { get; }

        public string DescriptionText { get; }

        public string DetailText { get; }

        public IReadOnlyList<InventoryScreenActionPayload> Actions { get; }
    }

    public sealed class InventoryScreenPayload : IScreenPayload
    {
        public static readonly InventoryScreenPayload Default = new(
            "Inventory",
            new[]
            {
                new InventoryScreenItemPayload(
                    "crystal-shard",
                    "Crystal Shard",
                    InventoryItemCategory.Consumable,
                    3,
                    "A condensed shard used to power nearby stage devices.",
                    "Stored in the consumable kit. Stable enough for preview socketing only.",
                    new[]
                    {
                        new InventoryScreenActionPayload("socket", "Socket", "Preview: route Crystal Shard into the next socketed device."),
                        new InventoryScreenActionPayload("inspect", "Inspect", "Preview: inspect the crystal fracture lines for charge quality."),
                    }),
                new InventoryScreenItemPayload(
                    "field-ration",
                    "Field Ration",
                    InventoryItemCategory.Consumable,
                    2,
                    "A compact ration pack staged for recovery checkpoints.",
                    "Marked consumable. Share remains disabled until a companion target exists.",
                    new[]
                    {
                        new InventoryScreenActionPayload("use", "Use", "Preview: consume one Field Ration at the next safe checkpoint."),
                        new InventoryScreenActionPayload("share", "Share", string.Empty, isEnabled: false, disabledReasonText: "Companion target required."),
                    }),
                new InventoryScreenItemPayload(
                    "recon-map",
                    "Recon Map",
                    InventoryItemCategory.Utility,
                    1,
                    "A folded route overlay that highlights nearby traversal anchors.",
                    "Pinned to the utility stack. Search and filters should preserve this selection when possible.",
                    new[]
                    {
                        new InventoryScreenActionPayload("equip", "Equip", "Preview: equip Recon Map to the utility slot."),
                        new InventoryScreenActionPayload("pin", "Pin", "Preview: pin Recon Map notes beside the current objective."),
                    }),
                new InventoryScreenItemPayload(
                    "phase-boots",
                    "Phase Boots",
                    InventoryItemCategory.Utility,
                    1,
                    "Prototype boots that stabilize short traversal bursts.",
                    "Utility gear. Tuning remains unavailable in the local presentation-only proof.",
                    new[]
                    {
                        new InventoryScreenActionPayload("equip", "Equip", "Preview: equip Phase Boots for the next traversal route."),
                        new InventoryScreenActionPayload("tune", "Tune", string.Empty, isEnabled: false, disabledReasonText: "Workbench required."),
                    }),
                new InventoryScreenItemPayload(
                    "rusted-key",
                    "Rusted Key",
                    InventoryItemCategory.KeyItem,
                    1,
                    "An old key tagged to a locked tutorial service hatch.",
                    "Tracked as a key item. No authoritative unlock command exists in Stage 8.",
                    new[]
                    {
                        new InventoryScreenActionPayload("inspect", "Inspect", "Preview: review likely locks that match the Rusted Key."),
                    }),
            },
            "Back");

        public InventoryScreenPayload(
            string titleText,
            IReadOnlyList<InventoryScreenItemPayload> items,
            string backLabel)
        {
            TitleText = titleText ?? string.Empty;
            Items = items ?? Array.Empty<InventoryScreenItemPayload>();
            BackLabel = backLabel ?? string.Empty;
        }

        public string TitleText { get; }

        public IReadOnlyList<InventoryScreenItemPayload> Items { get; }

        public string BackLabel { get; }
    }

    public sealed class SettingsScreenPayload : IScreenPayload
    {
        public static readonly SettingsScreenPayload Default = new(
            "Settings",
            "Main",
            "Background Music",
            "Effects",
            "Toggle Tooltips",
            "Toggle Large Text",
            "Back");

        public SettingsScreenPayload(
            string titleText,
            string mainAudioLabel,
            string bgmAudioLabel,
            string sfxAudioLabel,
            string tooltipToggleLabel,
            string largeTextToggleLabel,
            string backLabel)
        {
            TitleText = titleText ?? string.Empty;
            MainAudioLabel = mainAudioLabel ?? string.Empty;
            BgmAudioLabel = bgmAudioLabel ?? string.Empty;
            SfxAudioLabel = sfxAudioLabel ?? string.Empty;
            TooltipToggleLabel = tooltipToggleLabel ?? string.Empty;
            LargeTextToggleLabel = largeTextToggleLabel ?? string.Empty;
            BackLabel = backLabel ?? string.Empty;
        }

        public string TitleText { get; }

        public string MainAudioLabel { get; }

        public string BgmAudioLabel { get; }

        public string SfxAudioLabel { get; }

        public string TooltipToggleLabel { get; }

        public string LargeTextToggleLabel { get; }

        public string BackLabel { get; }
    }

    public enum AudioSettingsChannel
    {
        Main = 0,
        Bgm = 1,
        Sfx = 2,
    }

    public readonly struct AudioSettingsRowViewModel
    {
        public AudioSettingsRowViewModel(
            string labelText,
            string valueText,
            float normalizedValue,
            bool isMuted)
        {
            LabelText = labelText ?? string.Empty;
            ValueText = valueText ?? string.Empty;
            NormalizedValue = normalizedValue;
            IsMuted = isMuted;
        }

        public string LabelText { get; }

        public string ValueText { get; }

        public float NormalizedValue { get; }

        public bool IsMuted { get; }
    }

    public readonly struct SettingsScreenState
    {
        public SettingsScreenState(bool areTooltipsEnabled, bool isLargeTextEnabled)
        {
            AreTooltipsEnabled = areTooltipsEnabled;
            IsLargeTextEnabled = isLargeTextEnabled;
        }

        public bool AreTooltipsEnabled { get; }

        public bool IsLargeTextEnabled { get; }
    }

    public sealed class StageResultScreenPayload : IScreenPayload
    {
        public StageResultScreenPayload(
            string titleText,
            string summaryText,
            string detailText,
            string continueLabel)
        {
            TitleText = titleText ?? string.Empty;
            SummaryText = summaryText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            ContinueLabel = continueLabel ?? string.Empty;
        }

        public string TitleText { get; }

        public string SummaryText { get; }

        public string DetailText { get; }

        public string ContinueLabel { get; }
    }

    public sealed class GameplayScreenViewModel
    {
        public event Action Changed;

        public string TitleText { get; private set; } = string.Empty;

        public string HelpLabel { get; private set; } = string.Empty;

        public string ObjectivesLabel { get; private set; } = string.Empty;

        public string InventoryLabel { get; private set; } = string.Empty;

        public string SettingsLabel { get; private set; } = string.Empty;

        public void SetContent(
            string titleText,
            string helpLabel,
            string objectivesLabel,
            string inventoryLabel,
            string settingsLabel)
        {
            TitleText = titleText ?? string.Empty;
            HelpLabel = helpLabel ?? string.Empty;
            ObjectivesLabel = objectivesLabel ?? string.Empty;
            InventoryLabel = inventoryLabel ?? string.Empty;
            SettingsLabel = settingsLabel ?? string.Empty;
            Changed?.Invoke();
        }
    }

    public sealed class HelpScreenViewModel
    {
        public event Action Changed;

        public string TitleText { get; private set; } = string.Empty;

        public string DescriptionText { get; private set; } = string.Empty;

        public string BackLabel { get; private set; } = string.Empty;

        public void SetContent(string titleText, string descriptionText, string backLabel)
        {
            TitleText = titleText ?? string.Empty;
            DescriptionText = descriptionText ?? string.Empty;
            BackLabel = backLabel ?? string.Empty;
            Changed?.Invoke();
        }
    }

    public sealed class InventoryScreenViewModel
    {
        public event Action Changed;

        public string TitleText { get; private set; } = string.Empty;

        public string BackLabel { get; private set; } = string.Empty;

        public void SetContent(string titleText, string backLabel)
        {
            TitleText = titleText ?? string.Empty;
            BackLabel = backLabel ?? string.Empty;
            Changed?.Invoke();
        }
    }

    public readonly struct InventoryCatalogRowViewModel
    {
        public InventoryCatalogRowViewModel(
            string labelText,
            string metaText,
            bool isSelected,
            bool isVisible)
        {
            LabelText = labelText ?? string.Empty;
            MetaText = metaText ?? string.Empty;
            IsSelected = isSelected;
            IsVisible = isVisible;
        }

        public string LabelText { get; }

        public string MetaText { get; }

        public bool IsSelected { get; }

        public bool IsVisible { get; }
    }

    public sealed class InventoryCatalogViewModel
    {
        public event Action Changed;

        public string SearchLabelText { get; private set; } = string.Empty;

        public string FilterLabelText { get; private set; } = string.Empty;

        public string SortLabelText { get; private set; } = string.Empty;

        public string SummaryText { get; private set; } = string.Empty;

        public string EmptyStateText { get; private set; } = string.Empty;

        public IReadOnlyList<InventoryCatalogRowViewModel> Rows { get; private set; } = Array.Empty<InventoryCatalogRowViewModel>();

        public void SetContent(
            string searchLabelText,
            string filterLabelText,
            string sortLabelText,
            string summaryText,
            string emptyStateText,
            IReadOnlyList<InventoryCatalogRowViewModel> rows)
        {
            SearchLabelText = searchLabelText ?? string.Empty;
            FilterLabelText = filterLabelText ?? string.Empty;
            SortLabelText = sortLabelText ?? string.Empty;
            SummaryText = summaryText ?? string.Empty;
            EmptyStateText = emptyStateText ?? string.Empty;
            Rows = rows ?? Array.Empty<InventoryCatalogRowViewModel>();
            Changed?.Invoke();
        }
    }

    public sealed class InventoryDetailViewModel
    {
        public event Action Changed;

        public string TitleText { get; private set; } = string.Empty;

        public string BadgeText { get; private set; } = string.Empty;

        public string DescriptionText { get; private set; } = string.Empty;

        public string DetailText { get; private set; } = string.Empty;

        public void SetContent(
            string titleText,
            string badgeText,
            string descriptionText,
            string detailText)
        {
            TitleText = titleText ?? string.Empty;
            BadgeText = badgeText ?? string.Empty;
            DescriptionText = descriptionText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            Changed?.Invoke();
        }
    }

    public sealed class InventoryActionViewModel
    {
        public event Action Changed;

        public string PrimaryLabelText { get; private set; } = string.Empty;

        public string SecondaryLabelText { get; private set; } = string.Empty;

        public string PrimaryStateText { get; private set; } = string.Empty;

        public string SecondaryStateText { get; private set; } = string.Empty;

        public string FeedbackText { get; private set; } = string.Empty;

        public bool IsPrimaryVisible { get; private set; }

        public bool IsPrimaryEnabled { get; private set; }

        public bool IsSecondaryVisible { get; private set; }

        public bool IsSecondaryEnabled { get; private set; }

        public void SetContent(
            string primaryLabelText,
            string primaryStateText,
            bool isPrimaryVisible,
            bool isPrimaryEnabled,
            string secondaryLabelText,
            string secondaryStateText,
            bool isSecondaryVisible,
            bool isSecondaryEnabled,
            string feedbackText)
        {
            PrimaryLabelText = primaryLabelText ?? string.Empty;
            PrimaryStateText = primaryStateText ?? string.Empty;
            IsPrimaryVisible = isPrimaryVisible;
            IsPrimaryEnabled = isPrimaryEnabled;
            SecondaryLabelText = secondaryLabelText ?? string.Empty;
            SecondaryStateText = secondaryStateText ?? string.Empty;
            IsSecondaryVisible = isSecondaryVisible;
            IsSecondaryEnabled = isSecondaryEnabled;
            FeedbackText = feedbackText ?? string.Empty;
            Changed?.Invoke();
        }
    }

    public sealed class SettingsScreenViewModel
    {
        public event Action Changed;

        public string TitleText { get; private set; } = string.Empty;

        public AudioSettingsRowViewModel MainAudio { get; private set; }

        public AudioSettingsRowViewModel BgmAudio { get; private set; }

        public AudioSettingsRowViewModel SfxAudio { get; private set; }

        public string TooltipStatusText { get; private set; } = string.Empty;

        public string LargeTextStatusText { get; private set; } = string.Empty;

        public string TooltipToggleLabel { get; private set; } = string.Empty;

        public string LargeTextToggleLabel { get; private set; } = string.Empty;

        public string BackLabel { get; private set; } = string.Empty;

        public void SetContent(
            string titleText,
            AudioSettingsRowViewModel mainAudio,
            AudioSettingsRowViewModel bgmAudio,
            AudioSettingsRowViewModel sfxAudio,
            string tooltipStatusText,
            string largeTextStatusText,
            string tooltipToggleLabel,
            string largeTextToggleLabel,
            string backLabel)
        {
            TitleText = titleText ?? string.Empty;
            MainAudio = mainAudio;
            BgmAudio = bgmAudio;
            SfxAudio = sfxAudio;
            TooltipStatusText = tooltipStatusText ?? string.Empty;
            LargeTextStatusText = largeTextStatusText ?? string.Empty;
            TooltipToggleLabel = tooltipToggleLabel ?? string.Empty;
            LargeTextToggleLabel = largeTextToggleLabel ?? string.Empty;
            BackLabel = backLabel ?? string.Empty;
            Changed?.Invoke();
        }
    }

    public sealed class StageResultScreenViewModel
    {
        public event Action Changed;

        public string TitleText { get; private set; } = string.Empty;

        public string SummaryText { get; private set; } = string.Empty;

        public string DetailText { get; private set; } = string.Empty;

        public string ContinueLabel { get; private set; } = string.Empty;

        public void SetContent(
            string titleText,
            string summaryText,
            string detailText,
            string continueLabel)
        {
            TitleText = titleText ?? string.Empty;
            SummaryText = summaryText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            ContinueLabel = continueLabel ?? string.Empty;
            Changed?.Invoke();
        }
    }
}
