using System;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.Application;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class ObjectiveStatusScreenControllerTests
    {
        [Test]
        public void ObjectiveStatusScreenPresenter_DerivesOverviewAndSessionContent_FromExistingQueries()
        {
            var queryFacade = new FakeGameplayQueryFacade(
                new GameplaySessionReadModel(nextTickIndex: 7, isPaused: false, canAcceptGameplayCommands: false, isStageCleared: false),
                FakeGameplayQueryFacade.CreateDefaultPlayerHud(),
                new GameplayObjectiveReadModel(hasObjective: true, goalReached: true, allConditionsSatisfied: false, isCleared: false));
            using var presentationSource = UiTestPortFactory.CreatePresentationSource(queryFacade: queryFacade);
            using var statePresenter = new ObjectiveStatusPresenter(queryFacade, presentationSource);
            using var presenter = new ObjectiveStatusScreenPresenter(statePresenter);

            presenter.ApplyPayload(Game.Feature.UI.Screens.ObjectiveStatusScreenPayload.Default);

            Assert.That(presenter.ViewModel.BadgeText, Is.EqualTo("Goal Reached"));
            Assert.That(presenter.ViewModel.SummaryText, Does.Contain("Primary goal reached"));

            presenter.ShowSession();

            Assert.That(presenter.ViewModel.BadgeText, Is.EqualTo("Session"));
            Assert.That(presenter.ViewModel.SummaryText, Does.Contain("7"));
            Assert.That(presenter.BuildInfoPopupPayload().TitleText, Is.EqualTo("Session Info"));
        }
    }

    public sealed class SettingsScreenPresenterTests
    {
        [Test]
        public void SettingsScreenPresenter_BuildTooltipInfoPayload_RemainsBoundedAndStateAware()
        {
            var presenter = new SettingsScreenPresenter(
                new AccessibilitySettingsStore(),
                new FakeAudioSettingsPort(),
                new FakeDisplaySettingsPort());

            presenter.Apply(SettingsScreenPayload.Default);

            var enabledPayload = presenter.BuildTooltipInfoPayload();
            Assert.That(enabledPayload.TitleText, Is.EqualTo("Tooltips"));
            Assert.That(enabledPayload.BodyText, Does.Contain("short contextual hints"));
            Assert.That(enabledPayload.BodyText, Does.Contain("Enabled"));
            Assert.That(enabledPayload.BodyText, Does.Contain(SettingsScreenPayload.Default.TooltipToggleLabel));
            Assert.That(enabledPayload.BodyText, Does.Not.Contain("\n"));
            Assert.That(
                enabledPayload.BodyText.Split('.').Count(segment => !string.IsNullOrWhiteSpace(segment)),
                Is.LessThanOrEqualTo(2));
            Assert.That(enabledPayload.AnchorPreset, Is.EqualTo(TooltipPopupAnchorPreset.Center));

            presenter.ToggleTooltips();

            var disabledPayload = presenter.BuildTooltipInfoPayload();
            Assert.That(disabledPayload.BodyText, Does.Contain("Disabled"));
            Assert.That(disabledPayload.AnchorPreset, Is.EqualTo(TooltipPopupAnchorPreset.Center));
        }

        [Test]
        public void SettingsScreenPresenter_RootState_RemainsBoundedToShellAndAccessibilityOnly()
        {
            var presenter = new SettingsScreenPresenter(
                new AccessibilitySettingsStore(),
                new FakeAudioSettingsPort(),
                new FakeDisplaySettingsPort());

            presenter.Apply(SettingsScreenPayload.Default);
            presenter.ToggleLargeText();

            Assert.That(presenter.ViewModel.TitleText, Is.EqualTo(SettingsScreenPayload.Default.TitleText));
            Assert.That(presenter.ViewModel.TooltipStatusText, Is.EqualTo("Enabled"));
            Assert.That(presenter.ViewModel.LargeTextStatusText, Is.EqualTo("Enabled"));
            Assert.That(presenter.AudioPresenter.ViewModel.BgmAudio.LabelText, Is.EqualTo(SettingsScreenPayload.Default.BgmAudioLabel));
            Assert.That(presenter.DisplayPresenter.ViewModel.DisplaySectionTitle, Is.EqualTo(SettingsScreenPayload.Default.DisplaySectionTitle));
        }
    }

    public sealed class SettingsAudioPresenterTests
    {
        [Test]
        public void SettingsAudioPresenter_Rows_RefreshFromPortState_AndFlushExplicitly()
        {
            var audioPort = new FakeAudioSettingsPort();
            var presenter = new SettingsAudioPresenter(audioPort);

            presenter.Apply(new SettingsAudioPresenterInput(
                SettingsScreenPayload.Default.MainAudioLabel,
                SettingsScreenPayload.Default.BgmAudioLabel,
                SettingsScreenPayload.Default.SfxAudioLabel));
            presenter.SetVolume(AudioSettingsChannel.Bgm, 0.42f);
            presenter.SetMuted(AudioSettingsChannel.Sfx, true);
            presenter.Flush();

            Assert.That(presenter.ViewModel.BgmAudio.ValueText, Does.Contain("42"));
            Assert.That(presenter.ViewModel.SfxAudio.IsMuted, Is.True);
            Assert.That(audioPort.FlushCallCount, Is.EqualTo(1));
        }
    }

    public sealed class SettingsDisplayPresenterTests
    {
        [Test]
        public void SettingsDisplayPresenter_DisplayPreviewState_UsesCommittedVsStagedAndDisablesApplyDuringPreview()
        {
            var displayPort = new FakeDisplaySettingsPort();
            var presenter = new SettingsDisplayPresenter(displayPort);

            presenter.Apply(new SettingsDisplayPresenterInput(
                SettingsScreenPayload.Default.DisplaySectionTitle,
                SettingsScreenPayload.Default.CurrentDisplayLabel,
                SettingsScreenPayload.Default.ResolutionLabel,
                SettingsScreenPayload.Default.FullscreenLabel,
                SettingsScreenPayload.Default.DisplayApplyLabel,
                SettingsScreenPayload.Default.DisplayRevertLabel));
            presenter.StageResolution(2);
            presenter.StageWindowMode(DisplayWindowMode.FullScreenWindow);

            Assert.That(presenter.ViewModel.IsDisplayApplyInteractable, Is.True);
            Assert.That(presenter.ViewModel.SelectedResolutionIndex, Is.EqualTo(2));
            Assert.That(presenter.ViewModel.CurrentDisplayValueText, Is.EqualTo("1920 x 1080"));

            Assert.That(presenter.ApplyStagedSettings(), Is.True);

            Assert.That(displayPort.BeginPreviewCallCount, Is.EqualTo(1));
            Assert.That(presenter.ViewModel.IsDisplayPreviewActive, Is.True);
            Assert.That(presenter.ViewModel.IsDisplayApplyInteractable, Is.False);
            Assert.That(
                presenter.ViewModel.DisplayStatusText,
                Is.EqualTo("Preview active. Current display is temporary and not saved. Confirm to keep it, or it will revert in 15 seconds."));
            Assert.That(presenter.ViewModel.CurrentDisplayValueText, Is.EqualTo("1280 x 720"));

            Assert.That(presenter.ConfirmPreview(), Is.True);

            Assert.That(displayPort.CommitPreviewCallCount, Is.EqualTo(1));
            Assert.That(presenter.ViewModel.IsDisplayPreviewActive, Is.False);
            Assert.That(presenter.ViewModel.IsDisplayApplyInteractable, Is.False);
            Assert.That(presenter.ViewModel.SelectedResolutionIndex, Is.EqualTo(2));
            Assert.That(presenter.ViewModel.IsFullscreenEnabled, Is.True);
            Assert.That(presenter.ViewModel.DisplayStatusText, Is.EqualTo("Display settings saved."));
        }

        [Test]
        public void SettingsDisplayPresenter_CancelPreview_ResetsStagedStateBackToCommitted()
        {
            var displayPort = new FakeDisplaySettingsPort();
            var presenter = new SettingsDisplayPresenter(displayPort);

            presenter.Apply(new SettingsDisplayPresenterInput(
                SettingsScreenPayload.Default.DisplaySectionTitle,
                SettingsScreenPayload.Default.CurrentDisplayLabel,
                SettingsScreenPayload.Default.ResolutionLabel,
                SettingsScreenPayload.Default.FullscreenLabel,
                SettingsScreenPayload.Default.DisplayApplyLabel,
                SettingsScreenPayload.Default.DisplayRevertLabel));
            presenter.StageResolution(1);
            presenter.StageWindowMode(DisplayWindowMode.FullScreenWindow);
            presenter.ApplyStagedSettings();

            Assert.That(presenter.CancelPreview(), Is.True);

            Assert.That(displayPort.RevertPreviewCallCount, Is.EqualTo(1));
            Assert.That(presenter.ViewModel.IsDisplayPreviewActive, Is.False);
            Assert.That(presenter.ViewModel.SelectedResolutionIndex, Is.EqualTo(displayPort.CommittedModeIndex));
            Assert.That(presenter.ViewModel.IsFullscreenEnabled, Is.False);
            Assert.That(presenter.ViewModel.DisplayStatusText, Is.EqualTo("Preview reverted to the previous saved display settings."));
            Assert.That(presenter.ViewModel.IsDisplayApplyInteractable, Is.False);
        }

        [Test]
        public void SettingsDisplayPresenter_ExternalRuntimeDrift_UpdatesCurrentRuntimeLabel_WithoutChangingDirtyBaseline()
        {
            var displayPort = new FakeDisplaySettingsPort();
            var presenter = new SettingsDisplayPresenter(displayPort);

            presenter.Apply(new SettingsDisplayPresenterInput(
                SettingsScreenPayload.Default.DisplaySectionTitle,
                SettingsScreenPayload.Default.CurrentDisplayLabel,
                SettingsScreenPayload.Default.ResolutionLabel,
                SettingsScreenPayload.Default.FullscreenLabel,
                SettingsScreenPayload.Default.DisplayApplyLabel,
                SettingsScreenPayload.Default.DisplayRevertLabel));
            displayPort.SetRuntimeDrift(1, DisplayWindowMode.FullScreenWindow);
            presenter.ResyncState();

            Assert.That(presenter.ViewModel.CurrentDisplayValueText, Is.EqualTo("1600 x 900"));
            Assert.That(
                presenter.ViewModel.DisplayStatusText,
                Is.EqualTo("Current display changed outside saved settings. Saved settings remain unchanged until you apply again."));
            Assert.That(presenter.ViewModel.SelectedResolutionIndex, Is.EqualTo(0));
            Assert.That(presenter.ViewModel.IsDisplayApplyInteractable, Is.False);
        }
    }

    public sealed class InventoryScreenPresenterTests
    {
        [Test]
        public void InventoryScreenPresenter_RootState_RemainsBoundedToSourceItemsAndCanonicalSelection()
        {
            using var presenter = new InventoryScreenPresenter();

            presenter.Apply(InventoryScreenPayload.Default);

            Assert.That(presenter.ViewModel.TitleText, Is.EqualTo("Inventory"));
            Assert.That(presenter.DetailPresenter.ViewModel.TitleText, Is.EqualTo("Crystal Shard"));
            Assert.That(presenter.ActionPresenter.ViewModel.PrimaryLabelText, Is.EqualTo("Socket"));

            var fieldNames = typeof(InventoryScreenPresenter)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Select(field => field.Name)
                .ToArray();

            Assert.That(fieldNames.Any(name => name.IndexOf("search", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
            Assert.That(fieldNames.Any(name => name.IndexOf("filter", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
            Assert.That(fieldNames.Any(name => name.IndexOf("sort", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
            Assert.That(fieldNames.Any(name => name.IndexOf("visible", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
            Assert.That(fieldNames.Any(name => name.IndexOf("summary", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);

            presenter.CatalogPresenter.SelectVisibleRow(3);

            Assert.That(presenter.DetailPresenter.ViewModel.TitleText, Is.EqualTo("Recon Map"));
            Assert.That(presenter.ActionPresenter.ViewModel.PrimaryLabelText, Is.EqualTo("Equip"));
        }

        [Test]
        public void InventoryCatalogPresenter_OwnsQueryStateAndOnlyEscalatesWhenSelectionActuallyChanges()
        {
            var presenter = new InventoryCatalogPresenter();
            var selectionChangeCount = 0;
            string lastSelectionId = null;
            presenter.SelectionChanged += selectionId =>
            {
                selectionChangeCount++;
                lastSelectionId = selectionId;
            };

            var selectedItemId = presenter.Apply(CreateCatalogInput(InventoryScreenPayload.Default, "recon-map"));

            Assert.That(selectedItemId, Is.EqualTo("recon-map"));
            Assert.That(presenter.ViewModel.Rows[3].LabelText, Is.EqualTo("Recon Map"));

            presenter.CycleSort();

            Assert.That(selectionChangeCount, Is.EqualTo(0));
            Assert.That(presenter.ViewModel.SortLabelText, Does.Contain("Quantity"));

            presenter.CycleFilter();

            Assert.That(selectionChangeCount, Is.EqualTo(1));
            Assert.That(lastSelectionId, Is.EqualTo("crystal-shard"));
            Assert.That(presenter.ViewModel.FilterLabelText, Does.Contain("Consumable"));
            Assert.That(presenter.ViewModel.Rows[0].LabelText, Is.EqualTo("Crystal Shard"));
        }

        [Test]
        public void InventoryActionPresenter_KeepsPreviewFeedbackLocalAndResetsOnSelectionBoundary()
        {
            var presenter = new InventoryActionPresenter();

            presenter.Apply(new InventoryActionPresenterInput(
                new InventoryActionSelectionInput(
                    "phase-boots",
                    "Phase Boots",
                    new[]
                    {
                        new InventoryActionChoiceInput("equip", "Equip", "Preview: equip Phase Boots for the next traversal route.", true, string.Empty),
                        new InventoryActionChoiceInput("tune", "Tune", string.Empty, false, "Workbench required."),
                    }),
                resetVersion: 1));

            presenter.RequestPrimaryAction();
            Assert.That(presenter.ViewModel.FeedbackText, Does.Contain("Phase Boots"));

            presenter.RequestSecondaryAction();
            Assert.That(presenter.ViewModel.FeedbackText, Is.EqualTo("Workbench required."));

            presenter.Apply(new InventoryActionPresenterInput(null, resetVersion: 2));

            Assert.That(presenter.ViewModel.FeedbackText, Does.Contain("Preview-only actions"));
            Assert.That(presenter.ViewModel.IsPrimaryVisible, Is.False);
            Assert.That(presenter.ViewModel.IsSecondaryVisible, Is.False);
        }

        [Test]
        public void InventoryPresenterInputContracts_RemainNarrowAndResponsibilitySpecific()
        {
            Assert.That(
                typeof(InventoryCatalogPresenterInput)
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Select(property => property.Name)
                    .OrderBy(name => name)
                    .ToArray(),
                Is.EqualTo(new[] { "SelectedItemId", "SourceItems" }));

            Assert.That(
                typeof(InventoryDetailPresenterInput)
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Select(property => property.Name)
                    .OrderBy(name => name)
                    .ToArray(),
                Is.EqualTo(new[] { "SelectedItem" }));

            Assert.That(
                typeof(InventoryActionPresenterInput)
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Select(property => property.Name)
                    .OrderBy(name => name)
                    .ToArray(),
                Is.EqualTo(new[] { "ResetVersion", "SelectedItem" }));
        }

        private static InventoryCatalogPresenterInput CreateCatalogInput(InventoryScreenPayload payload, string selectedItemId)
        {
            var sourceItems = payload.Items
                .Select(item => new InventoryCatalogItemInput(
                    item.ItemId,
                    item.LabelText,
                    item.Category,
                    item.Amount,
                    item.DescriptionText))
                .ToArray();

            return new InventoryCatalogPresenterInput(sourceItems, selectedItemId);
        }
    }
}
