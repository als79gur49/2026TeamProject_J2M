using System;
using System.IO;
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
        public void ObjectiveStatusScreenPresenter_ListsObjectiveConditionDetails_FromObjectiveSnapshotOnly()
        {
            var queryFacade = new FakeGameplayQueryFacade(
                new GameplaySessionReadModel(nextTickIndex: 7, isPaused: false, canAcceptGameplayCommands: false, isStageCleared: false),
                FakeGameplayQueryFacade.CreateDefaultPlayerHud(),
                CreateObjectiveReadModel());
            using var presentationSource = UiTestPortFactory.CreatePresentationSource(queryFacade: queryFacade);
            using var statePresenter = new ObjectiveStatusPresenter(presentationSource);
            using var presenter = new ObjectiveStatusScreenPresenter(statePresenter);

            presenter.ApplyPayload(Game.Feature.UI.Screens.ObjectiveStatusScreenPayload.Default);

            Assert.That(presenter.ViewModel.BadgeText, Is.EqualTo("Goal Reached"));
            Assert.That(presenter.ViewModel.SummaryText, Does.Contain("Reach the Exit"));
            Assert.That(presenter.ViewModel.SummaryText, Does.Contain("Move to the exit zone."));
            Assert.That(presenter.ViewModel.DetailText, Does.Contain("Primary:"));
            Assert.That(presenter.ViewModel.DetailText, Does.Contain("Done: Reach the exit zone"));
            Assert.That(presenter.ViewModel.DetailText, Does.Contain("Optional:"));
            Assert.That(presenter.ViewModel.DetailText, Does.Contain("Pending: Defeat every enemy"));
            Assert.That(presenter.ViewModel.SecondaryText, Is.EqualTo("Goal: Yes | Required: No | Cleared: No"));
            Assert.That(presenter.ViewModel.DetailText, Does.Not.Contain("PlayerAtAnyZone"));
            Assert.That(presenter.ViewModel.DetailText, Does.Not.Contain("PrimaryGoal"));
            Assert.That(presenter.BuildInfoPopupPayload().BodyText, Does.Contain("Goal reached: Yes"));
            Assert.That(presenter.BuildInfoPopupPayload().BodyText, Does.Not.Contain("PlayerAtAnyZone"));
            Assert.That(presenter.BuildInfoPopupPayload().BodyText, Does.Not.Contain("PrimaryGoal"));
            Assert.That(presenter.BuildInfoPopupPayload().BodyText, Does.Not.Contain("Reach the exit zone"));
            Assert.That(presenter.BuildInfoPopupPayload().BodyText, Does.Not.Contain("Defeat every enemy"));
        }

        [Test]
        public void ObjectiveStatusPresenter_DoesNotRenderRawConditionDetails()
        {
            var queryFacade = new FakeGameplayQueryFacade(
                new GameplaySessionReadModel(nextTickIndex: 7, isPaused: false, canAcceptGameplayCommands: true, isStageCleared: false),
                FakeGameplayQueryFacade.CreateDefaultPlayerHud(),
                CreateObjectiveReadModel());
            using var presentationSource = UiTestPortFactory.CreatePresentationSource(queryFacade: queryFacade);
            using var statePresenter = new ObjectiveStatusPresenter(presentationSource);
            using var presenter = new ObjectiveStatusScreenPresenter(statePresenter);

            Assert.That(presenter.ViewModel.DetailText, Does.Not.Contain("PlayerEntityId"));
            Assert.That(presenter.ViewModel.DetailText, Does.Not.Contain("MatchedZoneId"));
            Assert.That(presenter.ViewModel.DetailText, Does.Not.Contain("ConditionType"));
        }

        private static GameplayObjectiveReadModel CreateObjectiveReadModel()
        {
            return new GameplayObjectiveReadModel(
                hasObjective: true,
                goalReached: true,
                allConditionsSatisfied: false,
                isCleared: false,
                objectiveTitle: "Reach the Exit",
                objectiveSummary: "Move to the exit zone.",
                conditions: new[]
                {
                    new GameplayObjectiveConditionReadModel(
                        stableId: "primary-goal",
                        role: GameplayObjectiveConditionRole.PrimaryGoal,
                        required: true,
                        isSatisfied: true,
                        titleText: "Reach the exit zone",
                        progressText: string.Empty,
                        sortOrder: 0),
                    new GameplayObjectiveConditionReadModel(
                        stableId: "optional-enemies",
                        role: GameplayObjectiveConditionRole.SecondaryGoal,
                        required: false,
                        isSatisfied: false,
                        titleText: "Defeat every enemy",
                        progressText: string.Empty,
                        sortOrder: 10),
                });
        }
    }

    public sealed class SettingsScreenPresenterTests
    {
        [Test]
        public void SettingsScreenPresenter_RootState_RemainsBoundedToShellOnly()
        {
            var presenter = new SettingsScreenPresenter(
                new AccessibilitySettingsStore(),
                new FakeAudioSettingsPort(),
                new FakeDisplaySettingsPort());

            presenter.Apply(SettingsScreenPayload.Default, 15d);

            Assert.That(presenter.ViewModel.TitleText, Is.EqualTo(SettingsScreenPayload.Default.TitleText));
            Assert.That(presenter.AudioPresenter.ViewModel.BgmAudio.ValueText, Is.Not.Empty);
            Assert.That(presenter.DisplayPresenter.ViewModel.CurrentDisplayValueText, Is.EqualTo("1920 x 1080"));
        }

        [Test]
        public void SettingsScreenPresenter_StaticAudioAndDisplayCopy_RemainsPrefabAuthored()
        {
            var presenter = new SettingsScreenPresenter(
                new AccessibilitySettingsStore(),
                new FakeAudioSettingsPort(),
                new FakeDisplaySettingsPort());

            presenter.Apply(SettingsScreenPayload.Default, 15d);

            Assert.That(typeof(AudioSettingsRowViewModel).GetProperty("LabelText"), Is.Null);
            Assert.That(typeof(SettingsDisplayViewModel).GetProperty("ResolutionHoverHintText"), Is.Null);
        }
    }

    public sealed class SettingsAudioPresenterTests
    {
        [Test]
        public void SettingsAudioPresenter_Rows_RefreshFromPortState_AndFlushExplicitly()
        {
            var audioPort = new FakeAudioSettingsPort();
            var presenter = new SettingsAudioPresenter(audioPort);

            presenter.Apply(default);
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

            presenter.Apply(default, 15d);
            presenter.StageResolution(2);
            presenter.StageWindowMode(DisplayWindowMode.FullScreenWindow);

            Assert.That(presenter.ViewModel.IsDisplayApplyInteractable, Is.True);
            Assert.That(presenter.ViewModel.SelectedResolutionIndex, Is.EqualTo(2));
            Assert.That(presenter.ViewModel.CurrentDisplayValueText, Is.EqualTo("1920 x 1080"));

            Assert.That(presenter.ApplyStagedSettings(15d), Is.True);

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

            presenter.Apply(default, 15d);
            presenter.StageResolution(1);
            presenter.StageWindowMode(DisplayWindowMode.FullScreenWindow);
            presenter.ApplyStagedSettings(15d);

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

            presenter.Apply(default, 15d);
            displayPort.SetRuntimeDrift(1, DisplayWindowMode.FullScreenWindow);
            presenter.ResyncState(15d);

            Assert.That(presenter.ViewModel.CurrentDisplayValueText, Is.EqualTo("1600 x 900"));
            Assert.That(
                presenter.ViewModel.DisplayStatusText,
                Is.EqualTo("Current display changed outside saved settings. Saved settings remain unchanged until you apply again."));
            Assert.That(presenter.ViewModel.SelectedResolutionIndex, Is.EqualTo(0));
            Assert.That(presenter.ViewModel.IsDisplayApplyInteractable, Is.False);
        }

        [Test]
        public void SettingsDisplayPresenterInput_AndViewModel_DoNotExposeStaticPrefabCopyOrHoverStateFlags()
        {
            var inputPropertyNames = typeof(SettingsDisplayPresenterInput)
                .GetProperties()
                .Select(property => property.Name)
                .ToArray();
            var viewModelPropertyNames = typeof(SettingsDisplayViewModel)
                .GetProperties()
                .Select(property => property.Name)
                .ToArray();

            Assert.That(inputPropertyNames, Is.Empty);
            Assert.That(viewModelPropertyNames, Has.No.Member("ResolutionHoverHintText"));
            Assert.That(viewModelPropertyNames, Has.No.Member("DisplaySectionTitle"));
            Assert.That(viewModelPropertyNames, Has.No.Member("CurrentDisplayLabel"));
            Assert.That(viewModelPropertyNames, Has.No.Member("ResolutionLabel"));
            Assert.That(viewModelPropertyNames, Has.No.Member("FullscreenLabel"));
            Assert.That(viewModelPropertyNames, Has.No.Member("DisplayApplyLabel"));
            Assert.That(viewModelPropertyNames, Has.No.Member("DisplayRevertLabel"));
            Assert.That(inputPropertyNames, Has.No.Member("AreTooltipsEnabled"));
            Assert.That(viewModelPropertyNames, Has.No.Member("AreTooltipsEnabled"));
            Assert.That(inputPropertyNames, Has.No.Member("IsResolutionHoverHintVisible"));
            Assert.That(viewModelPropertyNames, Has.No.Member("IsResolutionHoverHintVisible"));
        }

        [Test]
        public void SettingsDisplayPresenter_PreviewCountdown_ShapesWholeSecondTextAndBarFromSnapshotOnly()
        {
            var presenter = new SettingsDisplayPresenter(new FakeDisplaySettingsPort());
            presenter.Apply(default, 15d);
            presenter.StageResolution(2);
            Assert.That(presenter.ApplyStagedSettings(15d), Is.True);

            presenter.SetPreviewCountdown(DisplayPreviewCountdownSnapshot.Create(15.0, 15.0));
            Assert.That(presenter.ViewModel.PreviewCountdownText, Is.EqualTo("Reverting in 15s"));
            Assert.That(presenter.ViewModel.PreviewCountdownNormalized, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(presenter.ViewModel.IsPreviewCountdownVisible, Is.True);

            presenter.SetPreviewCountdown(DisplayPreviewCountdownSnapshot.Create(14.2, 15.0));
            Assert.That(presenter.ViewModel.PreviewCountdownText, Is.EqualTo("Reverting in 15s"));
            Assert.That(presenter.ViewModel.PreviewCountdownNormalized, Is.EqualTo(1f).Within(0.0001f));

            presenter.SetPreviewCountdown(DisplayPreviewCountdownSnapshot.Create(14.0, 15.0));
            Assert.That(presenter.ViewModel.PreviewCountdownText, Is.EqualTo("Reverting in 15s"));
            Assert.That(presenter.ViewModel.PreviewCountdownNormalized, Is.EqualTo(1f).Within(0.0001f));

            presenter.SetPreviewCountdown(DisplayPreviewCountdownSnapshot.Create(1.0, 15.0));
            Assert.That(presenter.ViewModel.PreviewCountdownText, Is.EqualTo("Reverting in 1s"));
            Assert.That(presenter.ViewModel.PreviewCountdownNormalized, Is.EqualTo(1f / 15f).Within(0.0001f));

            presenter.SetPreviewCountdown(DisplayPreviewCountdownSnapshot.Create(0.2, 15.0));
            Assert.That(presenter.ViewModel.PreviewCountdownText, Is.EqualTo("Reverting in 1s"));
            Assert.That(presenter.ViewModel.PreviewCountdownNormalized, Is.EqualTo(1f / 15f).Within(0.0001f));

            presenter.ClearPreviewCountdown();
            Assert.That(presenter.ViewModel.PreviewCountdownText, Is.EqualTo(string.Empty));
            Assert.That(presenter.ViewModel.PreviewCountdownNormalized, Is.Zero);
            Assert.That(presenter.ViewModel.IsPreviewCountdownVisible, Is.False);
        }

        [Test]
        public void SettingsDisplayPresenter_UsesSuppliedTimeoutCopy_WithoutPresenterOrRuntimeHardcoded15Seconds()
        {
            var presenter = new SettingsDisplayPresenter(new FakeDisplaySettingsPort());
            presenter.Apply(default, 21d);
            presenter.StageResolution(2);

            Assert.That(presenter.ApplyStagedSettings(21d), Is.True);
            Assert.That(
                presenter.ViewModel.DisplayStatusText,
                Is.EqualTo("Preview active. Current display is temporary and not saved. Confirm to keep it, or it will revert in 21 seconds."));

            var presenterSource = ReadRepoFile("Assets/_Features/UI/UI_Application/Runtime/ScreenPresenters.cs");
            var runtimeSource = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/GameplayScreenRuntimeFactory.cs");
            Assert.That(presenterSource, Does.Not.Contain("15 seconds"));
            Assert.That(runtimeSource, Does.Not.Contain("15 seconds"));
        }

        [Test]
        public void SettingsDisplayPresenter_DoesNotOwnIndependentTimeoutOrTimerFields()
        {
            var fieldNames = typeof(SettingsDisplayPresenter)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Select(field => field.Name)
                .ToArray();
            var doubleFields = typeof(SettingsDisplayPresenter)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(field => field.FieldType == typeof(double))
                .Select(field => field.Name)
                .ToArray();

            Assert.That(fieldNames, Does.Contain("_previewCountdown"));
            Assert.That(fieldNames, Has.No.Member("_previewTimeoutSeconds"));
            Assert.That(fieldNames, Has.No.Member("_previewDeadline"));
            Assert.That(fieldNames, Has.No.Member("_timeProvider"));
            Assert.That(doubleFields, Is.Empty);
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", relativePath)));
        }
    }
}
