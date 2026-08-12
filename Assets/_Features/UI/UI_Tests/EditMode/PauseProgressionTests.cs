using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Popups;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class PauseProgressionTests
    {
        private const string PausePrefabPath =
            "Assets/_Features/UI/UI_Popups/Prefabs/PausePopup.prefab";

        [Test]
        public void Mapper_CanonicalCampaignCreatesFiveGroupStartsAndEightStages()
        {
            var snapshot = CreateCanonicalSnapshot("stage-2-1");

            var viewModel = MapProgression(snapshot);

            Assert.That(viewModel.IsVisible, Is.True);
            Assert.That(viewModel.Markers, Has.Count.EqualTo(13));
            Assert.That(viewModel.CurrentIndex, Is.EqualTo(5));
            Assert.That(
                CountMarkers(viewModel, PauseProgressionMarkerKind.GroupStart),
                Is.EqualTo(5));
            Assert.That(
                CountMarkers(viewModel, PauseProgressionMarkerKind.Stage),
                Is.EqualTo(8));
        }

        [Test]
        public void Mapper_UnknownCurrentStageKeepsMarkersWithoutCurrentIndex()
        {
            var snapshot = CreateCanonicalSnapshot("stage-unknown");

            var viewModel = MapProgression(snapshot);

            Assert.That(viewModel.IsVisible, Is.True);
            Assert.That(viewModel.CurrentIndex, Is.EqualTo(-1));
            Assert.That(viewModel.Markers, Has.Count.EqualTo(13));
        }

        [Test]
        public void Snapshot_CopiesInputStages()
        {
            var stages = new[]
            {
                new PauseProgressionStageSnapshot("stage-a", "group-a"),
            };
            var snapshot = new PauseProgressionSnapshot(true, stages, "stage-a");

            stages[0] = new PauseProgressionStageSnapshot("mutated", "mutated");

            Assert.That(snapshot.Stages[0].StageKey, Is.EqualTo("stage-a"));
            Assert.That(snapshot.Stages[0].GroupKey, Is.EqualTo("group-a"));
        }

        [Test]
        public void ReadSource_UsesCurrentPresentationStageAndCopiesCanonicalSequence()
        {
            var definition = CampaignStageSequenceTestAsset.LoadProductionDefinition();
            var presentationSource = new ManualGameplayUiPresentationSource();
            presentationSource.PublishSnapshot(CreateSnapshotForStage(StageId.CreateOrThrow("stage-3-1")));
            var source = new CampaignPauseProgressionReadSource(
                new CampaignStageSequenceResolver(definition),
                presentationSource);

            Assert.That(source.TryRead(out var snapshot), Is.True);
            Assert.That(snapshot.IsAvailable, Is.True);
            Assert.That(snapshot.Stages, Has.Count.EqualTo(definition.Entries.Count));
            Assert.That(snapshot.CurrentStageKey, Is.EqualTo("stage-3-1"));
        }

        [Test]
        public void Prefab_BindCreatesMarkersAndKeepsCurrentColorWhileNavigating()
        {
            var prefab = UiTestPrefabAssetUtility.LoadPopupPrefab<PausePopupView>(PausePrefabPath);
            var root = Object.Instantiate(prefab.gameObject);
            try
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform);
                var view = root.GetComponent<PausePopupView>();
                var progression = GetField<PauseProgressionStripView>(view, "_progressionView");
                var viewModel = CreatePopupViewModel(CreateCanonicalSnapshot("stage-2-1"));

                view.Bind(viewModel);
                view.IsVisible = true;
                view.OnNavigationFocusGained();

                var navigation = GetField<UiSelectableButtonGroup>(view, "_navigationGroup");
                var selectedBeforeScroll = navigation.SelectedIndex;
                var markers = GetField<List<PauseProgressionMarkerView>>(progression, "_markers");
                var currentColor = GetField<Color>(progression, "_currentMarkerColor");
                var stageTemplate = GetField<PauseProgressionMarkerView>(progression, "_stageMarkerTemplate");
                var content = GetField<RectTransform>(progression, "_content");
                var scrollRect = GetField<ScrollRect>(progression, "_scrollRect");
                var viewport = scrollRect.viewport;
                var backLine = viewport.Find("BackLine") as RectTransform;
                Canvas.ForceUpdateCanvases();

                Assert.That(backLine, Is.Not.Null);
                var backLinePosition = backLine.anchoredPosition;
                var backLineSize = backLine.sizeDelta;

                Assert.That(progression.MarkerCount, Is.EqualTo(13));
                Assert.That(progression.CurrentIndex, Is.EqualTo(5));
                Assert.That(markers[5].VisualImage.color, Is.EqualTo(currentColor));
                Assert.That(markers[6].VisualImage.color, Is.EqualTo(stageTemplate.VisualImage.color));
                Assert.That(markers[5].IsViewed, Is.True);
                Assert.That(backLine.parent, Is.SameAs(viewport));
                Assert.That(backLine.GetSiblingIndex(), Is.LessThan(content.GetSiblingIndex()));
                Assert.That(backLine.rect.height, Is.GreaterThan(0f));
                Assert.That(backLine.anchorMin, Is.EqualTo(new Vector2(0f, 0f)));
                Assert.That(backLine.anchorMax, Is.EqualTo(new Vector2(1f, 0f)));
                Assert.That(backLine.pivot, Is.EqualTo(new Vector2(0.5f, 0f)));
                Assert.That(
                    backLine.rect.width,
                    Is.EqualTo(viewport.rect.width - 32f).Within(0.01f));
                Assert.That(backLine.rect.width, Is.GreaterThan(backLine.rect.height));
                Assert.That(
                    GetBottomIn(markers[0].RectTransform, viewport),
                    Is.EqualTo(GetTopIn(backLine, viewport)).Within(0.01f));
                Assert.That(
                    GetBottomIn(markers[1].RectTransform, viewport),
                    Is.EqualTo(GetTopIn(backLine, viewport)).Within(0.01f));
                Assert.That(view.HandleNavigate(UiNavigationCommand.Right), Is.True);
                Assert.That(progression.ViewedIndex, Is.EqualTo(6));
                Assert.That(progression.CurrentIndex, Is.EqualTo(5));
                Assert.That(markers[5].VisualImage.color, Is.EqualTo(currentColor));
                Assert.That(markers[5].IsViewed, Is.False);
                Assert.That(markers[6].IsViewed, Is.True);
                Assert.That(navigation.SelectedIndex, Is.EqualTo(selectedBeforeScroll));
                Assert.That(backLine.anchoredPosition, Is.EqualTo(backLinePosition));
                Assert.That(backLine.sizeDelta, Is.EqualTo(backLineSize));
                Assert.That(view.HandleNavigate(UiNavigationCommand.Left), Is.True);
                Assert.That(progression.ViewedIndex, Is.EqualTo(5));
                Assert.That(markers[6].IsViewed, Is.False);
                Assert.That(markers[5].IsViewed, Is.True);
                Assert.That(view.HandleNavigate(UiNavigationCommand.Down), Is.True);
                Assert.That(navigation.SelectedIndex, Is.EqualTo(selectedBeforeScroll + 1));
                view.OnNavigationFocusLost();
                Assert.That(markers[5].IsViewed, Is.False);
                view.OnNavigationFocusGained();
                Assert.That(markers[5].IsViewed, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Prefab_KeyboardNavigationMovesSelectionFrameWhenContentFitsViewport()
        {
            var prefab = UiTestPrefabAssetUtility.LoadPopupPrefab<PausePopupView>(PausePrefabPath);
            var root = Object.Instantiate(prefab.gameObject);
            try
            {
                var view = root.GetComponent<PausePopupView>();
                var progression = GetField<PauseProgressionStripView>(view, "_progressionView");
                var stages = new[]
                {
                    new PauseProgressionStageSnapshot("stage-a", "group-a"),
                    new PauseProgressionStageSnapshot("stage-b", "group-a"),
                };

                view.Bind(CreatePopupViewModel(new PauseProgressionSnapshot(true, stages, "stage-a")));
                view.IsVisible = true;
                view.OnNavigationFocusGained();
                Canvas.ForceUpdateCanvases();

                var markers = GetField<List<PauseProgressionMarkerView>>(progression, "_markers");
                var content = GetField<RectTransform>(progression, "_content");
                var scrollRect = GetField<ScrollRect>(progression, "_scrollRect");

                Assert.That(content.rect.width, Is.LessThanOrEqualTo(scrollRect.viewport.rect.width));
                Assert.That(markers[0].IsViewed, Is.True);
                Assert.That(view.HandleNavigate(UiNavigationCommand.Right), Is.True);
                Assert.That(progression.ViewedIndex, Is.EqualTo(1));
                Assert.That(markers[0].IsViewed, Is.False);
                Assert.That(markers[1].IsViewed, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static PausePopupViewModel CreatePopupViewModel(PauseProgressionSnapshot snapshot)
        {
            var viewModel = new PausePopupViewModel();
            viewModel.SetContent(
                "Paused",
                "Resume",
                "Settings",
                "Retry",
                "Main Menu",
                MapProgression(snapshot));
            return viewModel;
        }

        private static PauseProgressionViewModel MapProgression(PauseProgressionSnapshot snapshot)
        {
            var presenter = new PausePopupPresenter(
                PackageFreeLocalizedTextResolver.CreateSettingsDefault());
            presenter.Apply(new PausePopupPayload(snapshot));
            return presenter.ViewModel.Progression;
        }

        private static PauseProgressionSnapshot CreateCanonicalSnapshot(string currentStageKey)
        {
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            var stages = new PauseProgressionStageSnapshot[resolver.Entries.Count];
            for (var i = 0; i < stages.Length; i++)
            {
                stages[i] = new PauseProgressionStageSnapshot(
                    resolver.Entries[i].StageId.Value,
                    resolver.Entries[i].LevelGroupId);
            }

            return new PauseProgressionSnapshot(true, stages, currentStageKey);
        }

        private static int CountMarkers(
            PauseProgressionViewModel viewModel,
            PauseProgressionMarkerKind kind)
        {
            var count = 0;
            for (var i = 0; i < viewModel.Markers.Count; i++)
            {
                if (viewModel.Markers[i].Kind == kind)
                {
                    count++;
                }
            }

            return count;
        }

        private static UIPresentationSnapshot CreateSnapshotForStage(StageId stageId)
        {
            return new UIPresentationSnapshot(
                UIPresentationSnapshot.Empty.Tick,
                UIPresentationSnapshot.Empty.Interaction,
                new UIStageSlice(stageId, StageDisplayNameKeys.ForStage(stageId)),
                UIPresentationSnapshot.Empty.Player,
                UIPresentationSnapshot.Empty.Notifications);
        }

        private static float GetBottomIn(RectTransform target, RectTransform reference)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var bottom = float.PositiveInfinity;
            for (var i = 0; i < corners.Length; i++)
            {
                bottom = Mathf.Min(bottom, reference.InverseTransformPoint(corners[i]).y);
            }

            return bottom;
        }

        private static float GetTopIn(RectTransform target, RectTransform reference)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var top = float.NegativeInfinity;
            for (var i = 0; i < corners.Length; i++)
            {
                top = Mathf.Max(top, reference.InverseTransformPoint(corners[i]).y);
            }

            return top;
        }

        private static T GetField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(target);
        }
    }
}
