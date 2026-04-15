using System;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Composition
{
    public sealed class GameplayScreenRuntimeFactory : IScreenRuntimeFactory
    {
        private readonly IGameplayQueryFacade _queryFacade;
        private readonly IGameplayUiPresentationSource _presentationSource;
        private readonly ScreenLayerView _screenLayerView;
        private readonly UiSessionSettingsStore _sessionSettingsStore;

        public GameplayScreenRuntimeFactory(
            ScreenLayerView screenLayerView,
            IGameplayQueryFacade queryFacade,
            IGameplayUiPresentationSource presentationSource,
            UiSessionSettingsStore sessionSettingsStore)
        {
            _screenLayerView = screenLayerView ?? throw new ArgumentNullException(nameof(screenLayerView));
            _queryFacade = queryFacade ?? throw new ArgumentNullException(nameof(queryFacade));
            _presentationSource = presentationSource ?? throw new ArgumentNullException(nameof(presentationSource));
            _sessionSettingsStore = sessionSettingsStore ?? throw new ArgumentNullException(nameof(sessionSettingsStore));
        }

        public ScreenRuntimeFactoryResult Create(ScreenRequest request)
        {
            switch (request.ScreenId)
            {
                case ScreenId.Gameplay:
                    return CreateGameplayScreen();

                case ScreenId.Help:
                    return CreateHelpScreen();

                case ScreenId.ObjectiveStatus:
                    return CreateObjectiveStatusScreen();

                case ScreenId.Inventory:
                    return CreateInventoryScreen();

                case ScreenId.Settings:
                    return CreateSettingsScreen();

                case ScreenId.StageResult:
                    return CreateStageResultScreen();

                default:
                    throw new InvalidOperationException($"Unsupported screen id: {request.ScreenId}");
            }
        }

        private ScreenRuntimeFactoryResult CreateGameplayScreen()
        {
            var presenter = new GameplayScreenPresenter();
            var panel = UiCanvasElementFactory.CreatePanel(
                "GameplayScreen",
                _screenLayerView.ContentRoot,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(360f, 118f),
                new Vector2(16f, -16f));
            var view = panel.gameObject.AddComponent<GameplayScreenView>();
            var title = UiCanvasElementFactory.CreateLabel("Title", panel, new Vector2(12f, -12f), new Vector2(336f, 24f), TextAnchor.MiddleLeft, 18);
            var help = UiCanvasElementFactory.CreateButton("HelpButton", panel, "Help", new Vector2(12f, -76f), new Vector2(80f, 28f));
            var objective = UiCanvasElementFactory.CreateButton("ObjectiveButton", panel, "Objectives", new Vector2(102f, -76f), new Vector2(96f, 28f));
            var inventory = UiCanvasElementFactory.CreateButton("InventoryButton", panel, "Inventory", new Vector2(208f, -76f), new Vector2(80f, 28f));
            var settings = UiCanvasElementFactory.CreateButton("SettingsButton", panel, "Settings", new Vector2(298f, -76f), new Vector2(80f, 28f));
            view.Configure(
                panel.gameObject,
                title,
                help,
                objective,
                inventory,
                settings,
                UiCanvasElementFactory.GetButtonLabel(help),
                UiCanvasElementFactory.GetButtonLabel(objective),
                UiCanvasElementFactory.GetButtonLabel(inventory),
                UiCanvasElementFactory.GetButtonLabel(settings));
            view.Bind(presenter.ViewModel);
            view.SetIsCurrent(false);

            return new ScreenRuntimeFactoryResult(
                new ScreenPolicy(
                    ScreenPolicyClass.GameplayRoot,
                    ScreenRetentionMode.RetainMountedHistory,
                    ScreenBackAction.None,
                    HudShellMode.Visible,
                    blocksUiGameplayInput: false),
                new GameplayRuntime(view, presenter, () => DestroyObject(panel.gameObject)));
        }

        private ScreenRuntimeFactoryResult CreateHelpScreen()
        {
            var presenter = new HelpScreenPresenter();
            var panel = UiCanvasElementFactory.CreatePanel(
                "HelpScreen",
                _screenLayerView.ContentRoot,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(360f, 170f),
                new Vector2(0f, -20f));
            var view = panel.gameObject.AddComponent<HelpScreenView>();
            var title = UiCanvasElementFactory.CreateLabel("Title", panel, new Vector2(16f, -16f), new Vector2(328f, 24f), TextAnchor.MiddleCenter, 18);
            var description = UiCanvasElementFactory.CreateLabel("Description", panel, new Vector2(16f, -54f), new Vector2(328f, 48f), TextAnchor.UpperCenter, 15);
            var back = UiCanvasElementFactory.CreateButton("BackButton", panel, "Back", new Vector2(131f, -126f), new Vector2(98f, 28f));
            view.Configure(panel.gameObject, title, description, back);
            view.Bind(presenter.ViewModel);
            view.SetIsCurrent(false);

            return new ScreenRuntimeFactoryResult(
                new ScreenPolicy(
                    ScreenPolicyClass.InformationalOverlay,
                    ScreenRetentionMode.RetainMountedHistory,
                    ScreenBackAction.Pop,
                    HudShellMode.Visible,
                    blocksUiGameplayInput: true),
                new HelpRuntime(view, presenter, () => DestroyObject(panel.gameObject)));
        }

        private ScreenRuntimeFactoryResult CreateObjectiveStatusScreen()
        {
            var objectiveStatusPresenter = new ObjectiveStatusPresenter(_queryFacade, _presentationSource);
            var presenter = new ObjectiveStatusScreenPresenter(objectiveStatusPresenter);
            var panel = UiCanvasElementFactory.CreatePanel(
                "ObjectiveStatusScreen",
                _screenLayerView.ContentRoot,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(440f, 250f),
                new Vector2(0f, -20f));
            var view = panel.gameObject.AddComponent<ObjectiveStatusScreenView>();
            var title = UiCanvasElementFactory.CreateLabel("Title", panel, new Vector2(16f, -16f), new Vector2(408f, 24f), TextAnchor.MiddleCenter, 18);
            var badge = UiCanvasElementFactory.CreateLabel("Badge", panel, new Vector2(16f, -48f), new Vector2(408f, 22f), TextAnchor.MiddleCenter, 16);
            var summary = UiCanvasElementFactory.CreateLabel("Summary", panel, new Vector2(16f, -78f), new Vector2(408f, 52f), TextAnchor.UpperLeft, 15);
            var detail = UiCanvasElementFactory.CreateLabel("Detail", panel, new Vector2(16f, -138f), new Vector2(408f, 22f), TextAnchor.MiddleLeft, 14);
            var secondary = UiCanvasElementFactory.CreateLabel("Secondary", panel, new Vector2(16f, -164f), new Vector2(408f, 36f), TextAnchor.UpperLeft, 14);
            var overview = UiCanvasElementFactory.CreateButton("OverviewButton", panel, "Overview", new Vector2(16f, -216f), new Vector2(92f, 28f));
            var session = UiCanvasElementFactory.CreateButton("SessionButton", panel, "Session", new Vector2(116f, -216f), new Vector2(92f, 28f));
            var info = UiCanvasElementFactory.CreateButton("InfoButton", panel, "Info", new Vector2(216f, -216f), new Vector2(92f, 28f));
            var back = UiCanvasElementFactory.CreateButton("BackButton", panel, "Back", new Vector2(316f, -216f), new Vector2(92f, 28f));
            view.Configure(panel.gameObject, title, badge, summary, detail, secondary, overview, session, info, back);
            view.Bind(presenter.ViewModel);
            view.SetIsCurrent(false);

            return new ScreenRuntimeFactoryResult(
                new ScreenPolicy(
                    ScreenPolicyClass.GameplayAdjacentOverlay,
                    ScreenRetentionMode.RetainMountedHistory,
                    ScreenBackAction.Pop,
                    HudShellMode.Visible,
                    blocksUiGameplayInput: true),
                new ObjectiveStatusRuntime(view, presenter, () => DestroyObject(panel.gameObject)));
        }

        private ScreenRuntimeFactoryResult CreateInventoryScreen()
        {
            var presenter = new InventoryScreenPresenter();
            var panel = UiCanvasElementFactory.CreatePanel(
                "InventoryScreen",
                _screenLayerView.ContentRoot,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(584f, 330f),
                new Vector2(0f, -20f));
            var view = panel.gameObject.AddComponent<InventoryScreenView>();
            var title = UiCanvasElementFactory.CreateLabel("Title", panel, new Vector2(16f, -16f), new Vector2(552f, 24f), TextAnchor.MiddleCenter, 18);

            var catalogPanel = UiCanvasElementFactory.CreatePanel(
                "CatalogPanel",
                panel,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(248f, 220f),
                new Vector2(16f, -52f));
            var catalogView = catalogPanel.gameObject.AddComponent<InventoryCatalogView>();
            var searchButton = UiCanvasElementFactory.CreateButton("SearchButton", catalogPanel, "Search", new Vector2(8f, -8f), new Vector2(72f, 24f));
            var filterButton = UiCanvasElementFactory.CreateButton("FilterButton", catalogPanel, "Filter", new Vector2(86f, -8f), new Vector2(74f, 24f));
            var sortButton = UiCanvasElementFactory.CreateButton("SortButton", catalogPanel, "Sort", new Vector2(166f, -8f), new Vector2(74f, 24f));
            var summaryLabel = UiCanvasElementFactory.CreateLabel("Summary", catalogPanel, new Vector2(8f, -38f), new Vector2(232f, 18f), TextAnchor.MiddleLeft, 13);
            var emptyLabel = UiCanvasElementFactory.CreateLabel("Empty", catalogPanel, new Vector2(8f, -188f), new Vector2(232f, 24f), TextAnchor.UpperLeft, 12);
            var rowButtons = new Button[5];
            var rowLabelTexts = new Text[5];
            var rowMetaTexts = new Text[5];
            for (var i = 0; i < rowButtons.Length; i++)
            {
                var rowY = -64f - (i * 26f);
                rowButtons[i] = UiCanvasElementFactory.CreateButton(
                    $"RowButton{i}",
                    catalogPanel,
                    $"Row {i + 1}",
                    new Vector2(8f, rowY),
                    new Vector2(122f, 24f));
                rowLabelTexts[i] = UiCanvasElementFactory.GetButtonLabel(rowButtons[i]);
                if (rowLabelTexts[i] != null)
                {
                    rowLabelTexts[i].alignment = TextAnchor.MiddleLeft;
                }

                rowMetaTexts[i] = UiCanvasElementFactory.CreateLabel(
                    $"RowMeta{i}",
                    catalogPanel,
                    new Vector2(136f, rowY),
                    new Vector2(104f, 24f),
                    TextAnchor.MiddleLeft,
                    12);
            }
            catalogView.Configure(
                searchButton,
                filterButton,
                sortButton,
                UiCanvasElementFactory.GetButtonLabel(searchButton),
                UiCanvasElementFactory.GetButtonLabel(filterButton),
                UiCanvasElementFactory.GetButtonLabel(sortButton),
                summaryLabel,
                emptyLabel,
                rowButtons,
                rowLabelTexts,
                rowMetaTexts);

            var detailPanel = UiCanvasElementFactory.CreatePanel(
                "DetailPanel",
                panel,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(288f, 120f),
                new Vector2(280f, -52f));
            var detailView = detailPanel.gameObject.AddComponent<InventoryDetailView>();
            var detailTitle = UiCanvasElementFactory.CreateLabel("DetailTitle", detailPanel, new Vector2(12f, -10f), new Vector2(264f, 22f), TextAnchor.MiddleLeft, 16);
            var detailBadge = UiCanvasElementFactory.CreateLabel("DetailBadge", detailPanel, new Vector2(12f, -36f), new Vector2(264f, 18f), TextAnchor.MiddleLeft, 13);
            var detailDescription = UiCanvasElementFactory.CreateLabel("DetailDescription", detailPanel, new Vector2(12f, -58f), new Vector2(264f, 34f), TextAnchor.UpperLeft, 13);
            var detailBody = UiCanvasElementFactory.CreateLabel("DetailBody", detailPanel, new Vector2(12f, -92f), new Vector2(264f, 24f), TextAnchor.UpperLeft, 12);
            detailView.Configure(detailTitle, detailBadge, detailDescription, detailBody);

            var actionPanel = UiCanvasElementFactory.CreatePanel(
                "ActionPanel",
                panel,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(288f, 112f),
                new Vector2(280f, -180f));
            var actionView = actionPanel.gameObject.AddComponent<InventoryActionView>();
            var primaryButton = UiCanvasElementFactory.CreateButton("PrimaryActionButton", actionPanel, "Primary", new Vector2(12f, -12f), new Vector2(110f, 26f));
            var secondaryButton = UiCanvasElementFactory.CreateButton("SecondaryActionButton", actionPanel, "Secondary", new Vector2(12f, -46f), new Vector2(110f, 26f));
            var primaryState = UiCanvasElementFactory.CreateLabel("PrimaryState", actionPanel, new Vector2(130f, -12f), new Vector2(146f, 24f), TextAnchor.MiddleLeft, 12);
            var secondaryState = UiCanvasElementFactory.CreateLabel("SecondaryState", actionPanel, new Vector2(130f, -46f), new Vector2(146f, 24f), TextAnchor.MiddleLeft, 12);
            var feedback = UiCanvasElementFactory.CreateLabel("ActionFeedback", actionPanel, new Vector2(12f, -78f), new Vector2(264f, 24f), TextAnchor.UpperLeft, 12);
            actionView.Configure(
                primaryButton,
                secondaryButton,
                UiCanvasElementFactory.GetButtonLabel(primaryButton),
                UiCanvasElementFactory.GetButtonLabel(secondaryButton),
                primaryState,
                secondaryState,
                feedback);

            var back = UiCanvasElementFactory.CreateButton("BackButton", panel, "Back", new Vector2(243f, -290f), new Vector2(98f, 28f));
            view.Configure(
                panel.gameObject,
                title,
                catalogView,
                detailView,
                actionView,
                back,
                UiCanvasElementFactory.GetButtonLabel(back));
            view.Bind(presenter.ViewModel);
            catalogView.Bind(presenter.CatalogPresenter.ViewModel);
            detailView.Bind(presenter.DetailPresenter.ViewModel);
            actionView.Bind(presenter.ActionPresenter.ViewModel);
            view.SetIsCurrent(false);

            return new ScreenRuntimeFactoryResult(
                new ScreenPolicy(
                    ScreenPolicyClass.GameplayAdjacentOverlay,
                    ScreenRetentionMode.RetainMountedHistory,
                    ScreenBackAction.Pop,
                    HudShellMode.Visible,
                    blocksUiGameplayInput: true),
                new InventoryRuntime(view, presenter, () => DestroyObject(panel.gameObject)));
        }

        private ScreenRuntimeFactoryResult CreateSettingsScreen()
        {
            var presenter = new SettingsScreenPresenter(_sessionSettingsStore);
            var panel = UiCanvasElementFactory.CreatePanel(
                "SettingsScreen",
                _screenLayerView.ContentRoot,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(400f, 220f),
                new Vector2(0f, -20f));
            var view = panel.gameObject.AddComponent<SettingsScreenView>();
            var title = UiCanvasElementFactory.CreateLabel("Title", panel, new Vector2(16f, -16f), new Vector2(368f, 24f), TextAnchor.MiddleCenter, 18);
            var tooltipStatus = UiCanvasElementFactory.CreateLabel("TooltipStatus", panel, new Vector2(24f, -58f), new Vector2(160f, 22f), TextAnchor.MiddleLeft, 15);
            var tooltipToggle = UiCanvasElementFactory.CreateButton("TooltipToggle", panel, "Toggle Tooltips", new Vector2(220f, -52f), new Vector2(140f, 28f));
            var largeTextStatus = UiCanvasElementFactory.CreateLabel("LargeTextStatus", panel, new Vector2(24f, -104f), new Vector2(160f, 22f), TextAnchor.MiddleLeft, 15);
            var largeTextToggle = UiCanvasElementFactory.CreateButton("LargeTextToggle", panel, "Toggle Large Text", new Vector2(220f, -98f), new Vector2(140f, 28f));
            var back = UiCanvasElementFactory.CreateButton("BackButton", panel, "Back", new Vector2(151f, -176f), new Vector2(98f, 28f));
            view.Configure(
                panel.gameObject,
                title,
                tooltipStatus,
                largeTextStatus,
                tooltipToggle,
                largeTextToggle,
                back,
                UiCanvasElementFactory.GetButtonLabel(tooltipToggle),
                UiCanvasElementFactory.GetButtonLabel(largeTextToggle),
                UiCanvasElementFactory.GetButtonLabel(back));
            view.Bind(presenter.ViewModel);
            view.SetIsCurrent(false);

            return new ScreenRuntimeFactoryResult(
                new ScreenPolicy(
                    ScreenPolicyClass.Configuration,
                    ScreenRetentionMode.RetainMountedHistory,
                    ScreenBackAction.Pop,
                    HudShellMode.Hidden,
                    blocksUiGameplayInput: true),
                new SettingsRuntime(view, presenter, () => DestroyObject(panel.gameObject)));
        }

        private ScreenRuntimeFactoryResult CreateStageResultScreen()
        {
            var presenter = new StageResultScreenPresenter();
            var panel = UiCanvasElementFactory.CreatePanel(
                "StageResultScreen",
                _screenLayerView.ContentRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(460f, 220f),
                Vector2.zero);
            var view = panel.gameObject.AddComponent<StageResultScreenView>();
            var title = UiCanvasElementFactory.CreateLabel("Title", panel, new Vector2(24f, -20f), new Vector2(412f, 30f), TextAnchor.MiddleCenter, 20);
            var summary = UiCanvasElementFactory.CreateLabel("Summary", panel, new Vector2(24f, -70f), new Vector2(412f, 30f), TextAnchor.MiddleCenter, 16);
            var detail = UiCanvasElementFactory.CreateLabel("Detail", panel, new Vector2(24f, -112f), new Vector2(412f, 44f), TextAnchor.UpperCenter, 14);
            var continueButton = UiCanvasElementFactory.CreateButton("ContinueButton", panel, "Continue", new Vector2(181f, -176f), new Vector2(98f, 30f));
            view.Configure(
                panel.gameObject,
                title,
                summary,
                detail,
                continueButton,
                UiCanvasElementFactory.GetButtonLabel(continueButton));
            view.Bind(presenter.ViewModel);
            view.SetIsCurrent(false);

            return new ScreenRuntimeFactoryResult(
                new ScreenPolicy(
                    ScreenPolicyClass.TerminalResult,
                    ScreenRetentionMode.DisposeOnHide,
                    ScreenBackAction.Consume,
                    HudShellMode.Hidden,
                    blocksUiGameplayInput: true),
                new StageResultRuntime(view, presenter, () => DestroyObject(panel.gameObject)));
        }

        private static TPayload ExpectPayload<TPayload>(IScreenPayload payload) where TPayload : class, IScreenPayload
        {
            if (payload is not TPayload typedPayload)
            {
                throw new InvalidOperationException($"Screen payload type mismatch. Expected {typeof(TPayload).Name}.");
            }

            return typedPayload;
        }

        private static void DestroyObject(UnityEngine.Object unityObject)
        {
            if (unityObject == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                UnityEngine.Object.Destroy(unityObject);
                return;
            }

            UnityEngine.Object.DestroyImmediate(unityObject);
        }

        private abstract class ScreenRuntimeBase<TView> : IScreenRuntime where TView : Component, IScreenView
        {
            private readonly Action _dispose;

            protected ScreenRuntimeBase(TView view, Action dispose)
            {
                View = view ?? throw new ArgumentNullException(nameof(view));
                _dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));
            }

            public event Action<ScreenAction> ActionRequested;

            protected TView View { get; }

            public abstract void ApplyPayload(IScreenPayload payload);

            public virtual void Dispose()
            {
                View.SetIsCurrent(false);
                _dispose();
            }

            public void SetIsCurrent(bool isCurrent)
            {
                View.SetIsCurrent(isCurrent);
            }

            protected void RaiseAction(ScreenAction action)
            {
                ActionRequested?.Invoke(action);
            }
        }

        private sealed class GameplayRuntime : ScreenRuntimeBase<GameplayScreenView>
        {
            private readonly GameplayScreenPresenter _presenter;

            public GameplayRuntime(
                GameplayScreenView view,
                GameplayScreenPresenter presenter,
                Action dispose)
                : base(view, dispose)
            {
                _presenter = presenter;
                view.HelpRequested += HandleHelpRequested;
                view.ObjectivesRequested += HandleObjectivesRequested;
                view.InventoryRequested += HandleInventoryRequested;
                view.SettingsRequested += HandleSettingsRequested;
            }

            public override void ApplyPayload(IScreenPayload payload)
            {
                _presenter.Apply(ExpectPayload<GameplayScreenPayload>(payload));
            }

            public override void Dispose()
            {
                View.HelpRequested -= HandleHelpRequested;
                View.ObjectivesRequested -= HandleObjectivesRequested;
                View.InventoryRequested -= HandleInventoryRequested;
                View.SettingsRequested -= HandleSettingsRequested;
                View.Bind(null);
                base.Dispose();
            }

            private void HandleHelpRequested()
            {
                RaiseAction(ScreenAction.Push(new ScreenRequest(ScreenId.Help, HelpScreenPayload.Default, ScreenId.Help.ToString())));
            }

            private void HandleObjectivesRequested()
            {
                RaiseAction(ScreenAction.Push(new ScreenRequest(
                    ScreenId.ObjectiveStatus,
                    ObjectiveStatusScreenPayload.Default,
                    ScreenId.ObjectiveStatus.ToString())));
            }

            private void HandleInventoryRequested()
            {
                RaiseAction(ScreenAction.Push(new ScreenRequest(ScreenId.Inventory, InventoryScreenPayload.Default, ScreenId.Inventory.ToString())));
            }

            private void HandleSettingsRequested()
            {
                RaiseAction(ScreenAction.Push(new ScreenRequest(ScreenId.Settings, SettingsScreenPayload.Default, ScreenId.Settings.ToString())));
            }
        }

        private sealed class HelpRuntime : ScreenRuntimeBase<HelpScreenView>
        {
            private readonly HelpScreenPresenter _presenter;

            public HelpRuntime(HelpScreenView view, HelpScreenPresenter presenter, Action dispose)
                : base(view, dispose)
            {
                _presenter = presenter;
                view.BackRequested += HandleBackRequested;
            }

            public override void ApplyPayload(IScreenPayload payload)
            {
                _presenter.Apply(ExpectPayload<HelpScreenPayload>(payload));
            }

            public override void Dispose()
            {
                View.BackRequested -= HandleBackRequested;
                View.Bind(null);
                base.Dispose();
            }

            private void HandleBackRequested()
            {
                RaiseAction(ScreenAction.Back());
            }
        }

        private sealed class ObjectiveStatusRuntime : ScreenRuntimeBase<ObjectiveStatusScreenView>
        {
            private readonly ObjectiveStatusScreenPresenter _presenter;

            public ObjectiveStatusRuntime(
                ObjectiveStatusScreenView view,
                ObjectiveStatusScreenPresenter presenter,
                Action dispose)
                : base(view, dispose)
            {
                _presenter = presenter;
                view.OverviewRequested += HandleOverviewRequested;
                view.SessionRequested += HandleSessionRequested;
                view.InfoRequested += HandleInfoRequested;
                view.BackRequested += HandleBackRequested;
            }

            public override void ApplyPayload(IScreenPayload payload)
            {
                _presenter.ApplyPayload(ExpectPayload<ObjectiveStatusScreenPayload>(payload));
            }

            public override void Dispose()
            {
                View.OverviewRequested -= HandleOverviewRequested;
                View.SessionRequested -= HandleSessionRequested;
                View.InfoRequested -= HandleInfoRequested;
                View.BackRequested -= HandleBackRequested;
                View.Bind(null);
                _presenter.Dispose();
                base.Dispose();
            }

            private void HandleOverviewRequested()
            {
                _presenter.ShowOverview();
            }

            private void HandleSessionRequested()
            {
                _presenter.ShowSession();
            }

            private void HandleInfoRequested()
            {
                RaiseAction(ScreenAction.Popup(new PopupRequest(PopupId.ObjectiveInfo, _presenter.BuildInfoPopupPayload())));
            }

            private void HandleBackRequested()
            {
                RaiseAction(ScreenAction.Back());
            }
        }

        private sealed class InventoryRuntime : ScreenRuntimeBase<InventoryScreenView>
        {
            private readonly InventoryScreenPresenter _presenter;

            public InventoryRuntime(
                InventoryScreenView view,
                InventoryScreenPresenter presenter,
                Action dispose)
                : base(view, dispose)
            {
                _presenter = presenter;
                view.BackRequested += HandleBackRequested;
                view.CatalogView.SearchRequested += HandleSearchRequested;
                view.CatalogView.FilterRequested += HandleFilterRequested;
                view.CatalogView.SortRequested += HandleSortRequested;
                view.CatalogView.RowRequested += HandleRowRequested;
                view.ActionView.PrimaryActionRequested += HandlePrimaryActionRequested;
                view.ActionView.SecondaryActionRequested += HandleSecondaryActionRequested;
            }

            public override void ApplyPayload(IScreenPayload payload)
            {
                _presenter.Apply(ExpectPayload<InventoryScreenPayload>(payload));
            }

            public override void Dispose()
            {
                View.BackRequested -= HandleBackRequested;
                View.CatalogView.SearchRequested -= HandleSearchRequested;
                View.CatalogView.FilterRequested -= HandleFilterRequested;
                View.CatalogView.SortRequested -= HandleSortRequested;
                View.CatalogView.RowRequested -= HandleRowRequested;
                View.ActionView.PrimaryActionRequested -= HandlePrimaryActionRequested;
                View.ActionView.SecondaryActionRequested -= HandleSecondaryActionRequested;
                View.ActionView.Bind(null);
                View.DetailView.Bind(null);
                View.CatalogView.Bind(null);
                View.Bind(null);
                _presenter.Dispose();
                base.Dispose();
            }

            private void HandleBackRequested()
            {
                RaiseAction(ScreenAction.Back());
            }

            private void HandleFilterRequested()
            {
                _presenter.CatalogPresenter.CycleFilter();
            }

            private void HandlePrimaryActionRequested()
            {
                _presenter.ActionPresenter.RequestPrimaryAction();
            }

            private void HandleRowRequested(int visibleIndex)
            {
                _presenter.CatalogPresenter.SelectVisibleRow(visibleIndex);
            }

            private void HandleSearchRequested()
            {
                _presenter.CatalogPresenter.CycleSearch();
            }

            private void HandleSecondaryActionRequested()
            {
                _presenter.ActionPresenter.RequestSecondaryAction();
            }

            private void HandleSortRequested()
            {
                _presenter.CatalogPresenter.CycleSort();
            }
        }

        private sealed class SettingsRuntime : ScreenRuntimeBase<SettingsScreenView>
        {
            private readonly SettingsScreenPresenter _presenter;

            public SettingsRuntime(SettingsScreenView view, SettingsScreenPresenter presenter, Action dispose)
                : base(view, dispose)
            {
                _presenter = presenter;
                view.TooltipToggleRequested += HandleTooltipToggleRequested;
                view.LargeTextToggleRequested += HandleLargeTextToggleRequested;
                view.BackRequested += HandleBackRequested;
            }

            public override void ApplyPayload(IScreenPayload payload)
            {
                _presenter.Apply(ExpectPayload<SettingsScreenPayload>(payload));
            }

            public override void Dispose()
            {
                View.TooltipToggleRequested -= HandleTooltipToggleRequested;
                View.LargeTextToggleRequested -= HandleLargeTextToggleRequested;
                View.BackRequested -= HandleBackRequested;
                View.Bind(null);
                base.Dispose();
            }

            private void HandleTooltipToggleRequested()
            {
                _presenter.ToggleTooltips();
            }

            private void HandleLargeTextToggleRequested()
            {
                _presenter.ToggleLargeText();
            }

            private void HandleBackRequested()
            {
                RaiseAction(ScreenAction.Back());
            }
        }

        private sealed class StageResultRuntime : ScreenRuntimeBase<StageResultScreenView>
        {
            private readonly StageResultScreenPresenter _presenter;

            public StageResultRuntime(StageResultScreenView view, StageResultScreenPresenter presenter, Action dispose)
                : base(view, dispose)
            {
                _presenter = presenter;
                view.ContinueRequested += HandleContinueRequested;
            }

            public override void ApplyPayload(IScreenPayload payload)
            {
                _presenter.Apply(ExpectPayload<StageResultScreenPayload>(payload));
            }

            public override void Dispose()
            {
                View.ContinueRequested -= HandleContinueRequested;
                View.Bind(null);
                base.Dispose();
            }

            private void HandleContinueRequested()
            {
                RaiseAction(ScreenAction.Show(new ScreenRequest(ScreenId.Gameplay, GameplayScreenPayload.Default, ScreenId.Gameplay.ToString())));
            }
        }
    }
}
