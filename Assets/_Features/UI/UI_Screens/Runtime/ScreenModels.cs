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

    public sealed class InventoryScreenItemPayload
    {
        public InventoryScreenItemPayload(string labelText, int amount)
        {
            LabelText = labelText ?? string.Empty;
            Amount = amount;
        }

        public string LabelText { get; }

        public int Amount { get; }
    }

    public sealed class InventoryScreenPayload : IScreenPayload
    {
        public static readonly InventoryScreenPayload Default = new(
            "Inventory",
            new[]
            {
                new InventoryScreenItemPayload("Crystal Shard", 3),
                new InventoryScreenItemPayload("Flip Charge", 1),
                new InventoryScreenItemPayload("Recon Map", 1),
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
            "Toggle Tooltips",
            "Toggle Large Text",
            "Back");

        public SettingsScreenPayload(
            string titleText,
            string tooltipToggleLabel,
            string largeTextToggleLabel,
            string backLabel)
        {
            TitleText = titleText ?? string.Empty;
            TooltipToggleLabel = tooltipToggleLabel ?? string.Empty;
            LargeTextToggleLabel = largeTextToggleLabel ?? string.Empty;
            BackLabel = backLabel ?? string.Empty;
        }

        public string TitleText { get; }

        public string TooltipToggleLabel { get; }

        public string LargeTextToggleLabel { get; }

        public string BackLabel { get; }
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

        public string ItemsText { get; private set; } = string.Empty;

        public string BackLabel { get; private set; } = string.Empty;

        public void SetContent(string titleText, string itemsText, string backLabel)
        {
            TitleText = titleText ?? string.Empty;
            ItemsText = itemsText ?? string.Empty;
            BackLabel = backLabel ?? string.Empty;
            Changed?.Invoke();
        }
    }

    public sealed class SettingsScreenViewModel
    {
        public event Action Changed;

        public string TitleText { get; private set; } = string.Empty;

        public string TooltipStatusText { get; private set; } = string.Empty;

        public string LargeTextStatusText { get; private set; } = string.Empty;

        public string TooltipToggleLabel { get; private set; } = string.Empty;

        public string LargeTextToggleLabel { get; private set; } = string.Empty;

        public string BackLabel { get; private set; } = string.Empty;

        public void SetContent(
            string titleText,
            string tooltipStatusText,
            string largeTextStatusText,
            string tooltipToggleLabel,
            string largeTextToggleLabel,
            string backLabel)
        {
            TitleText = titleText ?? string.Empty;
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
