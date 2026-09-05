using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Popups;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class PauseProgressionTests
    {
        private const string PausePrefabPath =
            "Assets/_Features/UI/UI_Popups/Prefabs/PausePopup.prefab";
        private const string PausePreviewCatalogPath =
            "Assets/_Features/UI/UI_Popups/Prefabs/PauseStagePreviewCatalog.asset";

        [Test]
        public void Mapper_CanonicalCampaignCreatesStageImagesInSequenceOrder()
        {
            var snapshot = CreateCanonicalSnapshot("stage-2-1");

            var viewModel = MapProgression(snapshot);

            Assert.That(viewModel.IsVisible, Is.True);
            Assert.That(viewModel.Markers, Has.Count.EqualTo(13));
            Assert.That(viewModel.CurrentIndex, Is.EqualTo(5));
            Assert.That(viewModel.Markers[5].StageKey, Is.EqualTo(snapshot.Stages[5].StageKey));
            Assert.That(
                viewModel.Markers[5].DisplayNameDescriptor,
                Is.EqualTo(snapshot.Stages[5].DisplayNameDescriptor));
        }

        [Test]
        public void Mapper_UnknownCurrentStageKeepsMarkersWithoutCurrentIndex()
        {
            var snapshot = CreateCanonicalSnapshot("stage-unknown");

            var viewModel = MapProgression(snapshot);

            Assert.That(viewModel.IsVisible, Is.True);
            Assert.That(viewModel.CurrentIndex, Is.EqualTo(-1));
            Assert.That(viewModel.Markers, Has.Count.EqualTo(13));
            Assert.That(viewModel.Markers[0].StageKey, Is.EqualTo(snapshot.Stages[0].StageKey));
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
            Assert.That(snapshot.Stages[0].DisplayNameDescriptor.Table, Is.EqualTo(StageDisplayNameKeys.Table));
            Assert.That(snapshot.Stages[0].DisplayNameDescriptor.Key, Is.Not.Empty);
        }

        [Test]
        public void Prefab_AuthorsShellPanelOverlayAndMarkerDependencies()
        {
            var prefab = UiTestPrefabAssetUtility.LoadPopupPrefab<PausePopupView>(PausePrefabPath);
            var shell = (RectTransform)prefab.transform;
            var panel = shell.Find("PausePanel") as RectTransform;
            var overlay = shell.Find("StagePreviewOverlay") as RectTransform;
            var progression = GetField<PauseProgressionStripView>(prefab, "_progressionView");
            var scrollRect = GetField<ScrollRect>(progression, "_scrollRect");
            var previewCatalog = GetField<PauseStagePreviewCatalog>(progression, "_previewCatalog");
            var stageTemplate = GetField<PauseProgressionMarkerView>(progression, "_stageMarkerTemplate");

            Assert.That(shell.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(shell.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(shell.sizeDelta, Is.EqualTo(Vector2.zero));
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.sizeDelta, Is.EqualTo(new Vector2(700f, 500f)));
            Assert.That(overlay, Is.Not.Null);
            Assert.That(overlay.parent, Is.SameAs(shell));
            Assert.That(overlay.GetSiblingIndex(), Is.EqualTo(shell.childCount - 1));
            Assert.That(overlay.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(overlay.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(overlay.gameObject.activeSelf, Is.False);
            Assert.That(GetField<RectTransform>(prefab, "_enterMotionRoot"), Is.SameAs(panel));
            Assert.That(GetField<CanvasGroup>(prefab, "_enterCanvasGroup"), Is.SameAs(panel.GetComponent<CanvasGroup>()));
            Assert.That(GetField<PauseStagePreviewOverlayView>(prefab, "_previewOverlay"), Is.SameAs(overlay.GetComponent<PauseStagePreviewOverlayView>()));
            Assert.That(scrollRect.movementType, Is.EqualTo(ScrollRect.MovementType.Clamped));
            Assert.That(previewCatalog, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(previewCatalog), Is.EqualTo(PausePreviewCatalogPath));
            Assert.That(previewCatalog.PlaceholderSprite, Is.Not.Null);
            Assert.That(
                previewCatalog.ResolveOrPlaceholder(string.Empty),
                Is.SameAs(previewCatalog.PlaceholderSprite));

            AssertMarkerTemplateIsAuthored(stageTemplate);
            Assert.That(scrollRect.viewport.Find("BackLine"), Is.Null);
        }

        [Test]
        public void PreviewCatalog_CoversCampaignWithDistinctMatchingStageScreenshots()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PauseStagePreviewCatalog>(PausePreviewCatalogPath);
            var sequence = CampaignStageSequenceTestAsset.LoadProductionDefinition();
            Assert.That(catalog.Entries, Has.Length.EqualTo(sequence.Entries.Count));
            var keys = new HashSet<string>();
            var sprites = new HashSet<Sprite>();
            foreach (var entry in catalog.Entries)
            {
                Assert.That(keys.Add(entry.StageKey), Is.True, "Duplicate stage mapping");
                Assert.That(entry.PreviewSprite, Is.Not.Null, entry.StageKey);
                Assert.That(sprites.Add(entry.PreviewSprite), Is.True, "Stages must use distinct screenshots");
                Assert.That(AssetDatabase.GetAssetPath(entry.PreviewSprite), Is.EqualTo(
                    $"Assets/_Features/UI/UI_Popups/StagePreviews/{entry.StageKey}.png"));
                Assert.That(entry.PreviewSprite.rect.width / entry.PreviewSprite.rect.height,
                    Is.EqualTo(16f / 9f).Within(0.001f));
                Assert.That(entry.PreviewSprite.rect.size, Is.EqualTo(new Vector2(1280f, 720f)));
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(entry.PreviewSprite));
                var desktop = importer.GetPlatformTextureSettings("Standalone");
                Assert.That(desktop.overridden, Is.True);
                Assert.That(desktop.format, Is.EqualTo(TextureImporterFormat.BC7));
                Assert.That(importer.isReadable, Is.False);
                Assert.That(importer.mipmapEnabled, Is.False);
                if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64)
                {
                    Assert.That(entry.PreviewSprite.texture.format, Is.EqualTo(TextureFormat.BC7));
                }
            }

            foreach (var entry in sequence.Entries)
            {
                Assert.That(keys, Does.Contain(entry.StageId.Value));
                Assert.That(catalog.ResolveOrPlaceholder(entry.StageId.Value),
                    Is.Not.SameAs(catalog.PlaceholderSprite));
            }
            Assert.That(catalog.ResolveOrPlaceholder("legacy-stage-5-1"), Is.SameAs(catalog.PlaceholderSprite));
            Assert.That(catalog.ResolveOrPlaceholder("stage-unknown"), Is.SameAs(catalog.PlaceholderSprite));
        }

        [Test]
        public void Prefab_BindCreatesImageOnlyStripAndSelectedImagePushesItsNeighbors()
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
                var markers = GetField<List<PauseProgressionMarkerView>>(progression, "_markers");
                var content = GetField<RectTransform>(progression, "_content");
                var scrollRect = GetField<ScrollRect>(progression, "_scrollRect");
                var viewport = scrollRect.viewport;
                Canvas.ForceUpdateCanvases();

                Assert.That(progression.MarkerCount, Is.EqualTo(13));
                var catalog = AssetDatabase.LoadAssetAtPath<PauseStagePreviewCatalog>(PausePreviewCatalogPath);
                for (var i = 0; i < markers.Count; i++)
                {
                    Assert.That(markers[i].VisualImage.sprite,
                        Is.SameAs(catalog.ResolveOrPlaceholder(viewModel.Progression.Markers[i].StageKey)));
                }
                Assert.That(progression.CurrentIndex, Is.EqualTo(5));
                Assert.That(markers[4].VisualImage.sprite, Is.Not.Null);
                Assert.That(markers[5].VisualImage.sprite, Is.Not.Null);
                Assert.That(markers[6].VisualImage.sprite, Is.Not.Null);
                Assert.That(markers[4].VisualImage.color, Is.EqualTo(Color.white));
                Assert.That(markers[5].VisualImage.color, Is.EqualTo(Color.white));
                Assert.That(markers[6].VisualImage.color, Is.EqualTo(Color.white));
                Assert.That(progression.SelectedIndex, Is.EqualTo(5));
                Assert.That(progression.SelectedStageName, Is.Empty);
                Assert.That(progression.SelectedStageDescriptor.Key, Is.Not.Empty);
                Assert.That(markers[5].IsSelected, Is.True);
                Assert.That(markers[5].LayoutElement.preferredWidth, Is.EqualTo(144f));
                Assert.That(markers[4].LayoutElement.preferredWidth, Is.EqualTo(72f));
                Assert.That(markers[6].LayoutElement.preferredWidth, Is.EqualTo(72f));
                Assert.That(
                    GetCenterYIn(markers[0].VisualImage.rectTransform, viewport),
                    Is.EqualTo(GetCenterYIn(markers[1].VisualImage.rectTransform, viewport)).Within(0.01f));
                Assert.That(content.rect.width, Is.GreaterThan(viewport.rect.width));
                var selectedCenterBeforeMove = GetCenterXIn(markers[6].RectTransform, content);
                Assert.That(view.HandleNavigate(UiNavigationCommand.Right), Is.True);
                Assert.That(progression.SelectedIndex, Is.EqualTo(6));
                Assert.That(markers[5].LayoutElement.preferredWidth, Is.EqualTo(72f));
                Assert.That(markers[6].LayoutElement.preferredWidth, Is.EqualTo(144f));
                Assert.That(
                    GetCenterXIn(markers[6].RectTransform, content),
                    Is.Not.EqualTo(selectedCenterBeforeMove).Within(0.01f));
                AssertMarkersDoNotOverlap(markers[5].RectTransform, markers[6].RectTransform, content);
                AssertMarkersDoNotOverlap(markers[6].RectTransform, markers[7].RectTransform, content);
                Assert.That(view.HandleNavigate(UiNavigationCommand.Left), Is.True);
                Assert.That(progression.SelectedIndex, Is.EqualTo(5));

                Assert.That(progression.SelectIndex(0), Is.True);
                Assert.That(progression.TryMoveSelectedIndex(-1), Is.False);
                Assert.That(content.anchoredPosition.x, Is.EqualTo(0f).Within(0.01f));
                Assert.That(progression.SelectIndex(markers.Count - 1), Is.True);
                Assert.That(progression.TryMoveSelectedIndex(1), Is.False);
                Assert.That(
                    content.anchoredPosition.x,
                    Is.EqualTo(viewport.rect.width - content.rect.width).Within(0.01f));
                Assert.That(progression.SelectIndex(5), Is.True);

                Assert.That(view.HandleNavigate(UiNavigationCommand.Down), Is.True);
                Assert.That(navigation.SelectedIndex, Is.EqualTo(0));
                Assert.That(markers[5].LayoutElement.preferredWidth, Is.EqualTo(144f));
                view.OnNavigationFocusLost();
                view.OnNavigationFocusGained();
                Assert.That(markers[5].LayoutElement.preferredWidth, Is.EqualTo(144f));

                var overlay = GetField<PauseStagePreviewOverlayView>(view, "_previewOverlay");
                var completionCount = 0;
                view.CompletionRequested += _ => completionCount++;
                SetField(overlay, "_closeDurationSeconds", 0f);
                Assert.That(view.HandleSubmit(), Is.True);
                Assert.That(overlay.IsOpen, Is.True);
                view.ClickResume();
                Assert.That(completionCount, Is.Zero);
                Assert.That(view.HandleCancel(), Is.True);
                Assert.That(overlay.IsOpen, Is.False);
                view.ClickResume();
                Assert.That(completionCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Prefab_ClickSelectsThenSecondClickOpensPreview_AndCancelClosesIt()
        {
            var prefab = UiTestPrefabAssetUtility.LoadPopupPrefab<PausePopupView>(PausePrefabPath);
            var root = Object.Instantiate(prefab.gameObject);
            try
            {
                var view = root.GetComponent<PausePopupView>();
                var progression = GetField<PauseProgressionStripView>(view, "_progressionView");
                var stages = new[]
                {
                    new PauseProgressionStageSnapshot(
                        "stage-0-1",
                        "group-a",
                        StageDisplayNameTextDescriptors.ForStage(StageId.CreateOrThrow("stage-0-1"))),
                    new PauseProgressionStageSnapshot(
                        "stage-0-2",
                        "group-a",
                        StageDisplayNameTextDescriptors.ForStage(StageId.CreateOrThrow("stage-0-2"))),
                };

                view.Bind(CreatePopupViewModel(new PauseProgressionSnapshot(true, stages, "stage-0-1")));
                view.IsVisible = true;
                Canvas.ForceUpdateCanvases();

                var stageNameTargets = view.CreateStageNameLocalizationTargets();
                var markers = GetField<List<PauseProgressionMarkerView>>(progression, "_markers");
                var overlay = GetField<PauseStagePreviewOverlayView>(view, "_previewOverlay");
                SetField(overlay, "_closeDurationSeconds", 0f);

                Assert.That(stageNameTargets.Count, Is.EqualTo(2));
                Assert.That(stageNameTargets[0].text, Is.Empty);
                Assert.That(stageNameTargets[1].text, Is.Empty);
                Assert.That(progression.SelectedIndex, Is.EqualTo(0));

                markers[1].GetComponent<Button>().onClick.Invoke();

                Assert.That(progression.SelectedIndex, Is.EqualTo(1));
                Assert.That(markers[0].IsSelected, Is.False);
                Assert.That(markers[1].IsSelected, Is.True);
                Assert.That(overlay.IsOpen, Is.False);

                markers[1].GetComponent<Button>().onClick.Invoke();

                Assert.That(overlay.IsOpen, Is.True);
                Assert.That(stageNameTargets[0].text, Is.Empty);
                Assert.That(stageNameTargets[1].text, Is.Empty);
                Assert.That(view.HandleNavigate(UiNavigationCommand.Left), Is.False);
                Assert.That(progression.SelectedIndex, Is.EqualTo(1));

                var closeButton = GetField<Button>(overlay, "_closeButton");
                closeButton.onClick.Invoke();

                Assert.That(overlay.IsOpen, Is.False);

                markers[1].GetComponent<Button>().onClick.Invoke();
                Assert.That(overlay.IsOpen, Is.True);
                view.OnNavigationFocusGained();
                Assert.That(view.HandleSubmit(), Is.True);
                Assert.That(overlay.IsOpen, Is.False);
                Assert.That(progression.SelectedIndex, Is.EqualTo(1));

                Assert.That(view.HandleSubmit(), Is.True);
                Assert.That(overlay.IsOpen, Is.True);
                closeButton.onClick.Invoke();
                Assert.That(overlay.IsOpen, Is.False);
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
                    resolver.Entries[i].LevelGroupId,
                    StageDisplayNameTextDescriptors.ForStage(resolver.Entries[i].StageId));
            }

            return new PauseProgressionSnapshot(true, stages, currentStageKey);
        }

        private static void AssertMarkerTemplateIsAuthored(PauseProgressionMarkerView marker)
        {
            var button = marker.GetComponent<Button>();
            Assert.That(button, Is.Not.Null);
            Assert.That(button.transition, Is.EqualTo(Selectable.Transition.None));
            Assert.That(marker.LayoutElement, Is.Not.Null);
            Assert.That(marker.VisualImage, Is.Not.Null);
            Assert.That(button.targetGraphic, Is.SameAs(marker.VisualImage));
            Assert.That(marker.GetComponent<Graphic>(), Is.Null);
            Assert.That(marker.VisualImage.GetComponentsInChildren<Graphic>(true), Has.Length.EqualTo(1));
            Assert.That(marker.VisualImage.transform.childCount, Is.Zero);
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

        private static float GetCenterYIn(RectTransform target, RectTransform reference)
        {
            return reference.InverseTransformPoint(target.TransformPoint(target.rect.center)).y;
        }

        private static float GetCenterXIn(RectTransform target, RectTransform reference)
        {
            return reference.InverseTransformPoint(target.TransformPoint(target.rect.center)).x;
        }

        private static void AssertMarkersDoNotOverlap(
            RectTransform left,
            RectTransform right,
            RectTransform reference)
        {
            var leftBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(reference, left);
            var rightBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(reference, right);
            Assert.That(leftBounds.max.x, Is.LessThanOrEqualTo(rightBounds.min.x));
        }

        private static T GetField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(target);
        }

        private static void SetField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
