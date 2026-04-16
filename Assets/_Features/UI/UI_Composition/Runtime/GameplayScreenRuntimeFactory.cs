using System;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    public sealed class GameplayScreenRuntimeFactory : IScreenRuntimeFactory
    {
        private readonly IGameplayQueryFacade _queryFacade;
        private readonly IGameplayUiPresentationSource _presentationSource;
        private readonly ScreenPrefabCatalog _screenPrefabCatalog;
        private readonly ScreenLayerView _screenLayerView;
        private readonly UiSessionSettingsStore _sessionSettingsStore;

        public GameplayScreenRuntimeFactory(
            ScreenLayerView screenLayerView,
            IGameplayQueryFacade queryFacade,
            IGameplayUiPresentationSource presentationSource,
            UiSessionSettingsStore sessionSettingsStore,
            ScreenPrefabCatalog screenPrefabCatalog)
        {
            _screenLayerView = screenLayerView ?? throw new ArgumentNullException(nameof(screenLayerView));
            _queryFacade = queryFacade ?? throw new ArgumentNullException(nameof(queryFacade));
            _presentationSource = presentationSource ?? throw new ArgumentNullException(nameof(presentationSource));
            _sessionSettingsStore = sessionSettingsStore ?? throw new ArgumentNullException(nameof(sessionSettingsStore));
            _screenPrefabCatalog = screenPrefabCatalog ?? throw new ArgumentNullException(nameof(screenPrefabCatalog));
        }

        public ScreenRuntimeFactoryResult Create(ScreenRequest request)
        {
            switch (request.ScreenId)
            {
                case ScreenId.Gameplay:
                    return CreateGameplayRuntime();

                case ScreenId.Help:
                    return CreateHelpRuntime();

                case ScreenId.ObjectiveStatus:
                    return CreateObjectiveStatusRuntime();

                case ScreenId.Inventory:
                    return CreateInventoryRuntime();

                case ScreenId.Settings:
                    return CreateSettingsRuntime();

                case ScreenId.StageResult:
                    return CreateStageResultRuntime();

                default:
                    throw new InvalidOperationException($"Unsupported screen id: {request.ScreenId}");
            }
        }

        private ScreenRuntimeFactoryResult CreateGameplayRuntime()
        {
            var presenter = new GameplayScreenPresenter();
            var view = InstantiateScreenPrefab(_screenPrefabCatalog.GameplayPrefab, ScreenId.Gameplay);
            view.Bind(presenter.ViewModel);
            view.SetIsCurrent(false);

            return new ScreenRuntimeFactoryResult(
                new ScreenPolicy(
                    ScreenPolicyClass.GameplayRoot,
                    ScreenRetentionMode.RetainMountedHistory,
                    ScreenBackAction.None,
                    HudShellMode.Visible,
                    blocksUiGameplayInput: false),
                new GameplayRuntime(view, presenter, () => DestroyObject(view.gameObject)));
        }

        private ScreenRuntimeFactoryResult CreateHelpRuntime()
        {
            var presenter = new HelpScreenPresenter();
            var view = InstantiateScreenPrefab(_screenPrefabCatalog.HelpPrefab, ScreenId.Help);
            view.Bind(presenter.ViewModel);
            view.SetIsCurrent(false);

            return new ScreenRuntimeFactoryResult(
                new ScreenPolicy(
                    ScreenPolicyClass.InformationalOverlay,
                    ScreenRetentionMode.RetainMountedHistory,
                    ScreenBackAction.Pop,
                    HudShellMode.Visible,
                    blocksUiGameplayInput: true),
                new HelpRuntime(view, presenter, () => DestroyObject(view.gameObject)));
        }

        private ScreenRuntimeFactoryResult CreateObjectiveStatusRuntime()
        {
            var objectiveStatusPresenter = new ObjectiveStatusPresenter(_queryFacade, _presentationSource);
            var presenter = new ObjectiveStatusScreenPresenter(objectiveStatusPresenter);
            var view = InstantiateScreenPrefab(_screenPrefabCatalog.ObjectiveStatusPrefab, ScreenId.ObjectiveStatus);
            view.Bind(presenter.ViewModel);
            view.SetIsCurrent(false);

            return new ScreenRuntimeFactoryResult(
                new ScreenPolicy(
                    ScreenPolicyClass.GameplayAdjacentOverlay,
                    ScreenRetentionMode.RetainMountedHistory,
                    ScreenBackAction.Pop,
                    HudShellMode.Visible,
                    blocksUiGameplayInput: true),
                new ObjectiveStatusRuntime(view, presenter, () => DestroyObject(view.gameObject)));
        }

        private ScreenRuntimeFactoryResult CreateInventoryRuntime()
        {
            var presenter = new InventoryScreenPresenter();
            var view = InstantiateScreenPrefab(_screenPrefabCatalog.InventoryPrefab, ScreenId.Inventory);
            if (view.CatalogView == null || view.DetailView == null || view.ActionView == null)
            {
                throw new InvalidOperationException(
                    "Inventory screen prefab is missing one or more required child views.");
            }

            view.Bind(presenter.ViewModel);
            view.CatalogView.Bind(presenter.CatalogPresenter.ViewModel);
            view.DetailView.Bind(presenter.DetailPresenter.ViewModel);
            view.ActionView.Bind(presenter.ActionPresenter.ViewModel);
            view.SetIsCurrent(false);

            return new ScreenRuntimeFactoryResult(
                new ScreenPolicy(
                    ScreenPolicyClass.GameplayAdjacentOverlay,
                    ScreenRetentionMode.RetainMountedHistory,
                    ScreenBackAction.Pop,
                    HudShellMode.Visible,
                    blocksUiGameplayInput: true),
                new InventoryRuntime(view, presenter, () => DestroyObject(view.gameObject)));
        }

        private ScreenRuntimeFactoryResult CreateSettingsRuntime()
        {
            var presenter = new SettingsScreenPresenter(_sessionSettingsStore);
            var view = InstantiateScreenPrefab(_screenPrefabCatalog.SettingsPrefab, ScreenId.Settings);
            view.Bind(presenter.ViewModel);
            view.SetIsCurrent(false);

            return new ScreenRuntimeFactoryResult(
                new ScreenPolicy(
                    ScreenPolicyClass.Configuration,
                    ScreenRetentionMode.RetainMountedHistory,
                    ScreenBackAction.Pop,
                    HudShellMode.Hidden,
                    blocksUiGameplayInput: true),
                new SettingsRuntime(view, presenter, () => DestroyObject(view.gameObject)));
        }

        private ScreenRuntimeFactoryResult CreateStageResultRuntime()
        {
            var presenter = new StageResultScreenPresenter();
            var view = InstantiateScreenPrefab(_screenPrefabCatalog.StageResultPrefab, ScreenId.StageResult);
            view.Bind(presenter.ViewModel);
            view.SetIsCurrent(false);

            return new ScreenRuntimeFactoryResult(
                new ScreenPolicy(
                    ScreenPolicyClass.TerminalResult,
                    ScreenRetentionMode.DisposeOnHide,
                    ScreenBackAction.Consume,
                    HudShellMode.Hidden,
                    blocksUiGameplayInput: true),
                new StageResultRuntime(view, presenter, () => DestroyObject(view.gameObject)));
        }

        private TView InstantiateScreenPrefab<TView>(TView prefab, ScreenId screenId)
            where TView : Component, IScreenView
        {
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"Screen prefab catalog is missing a canonical prefab for screen '{screenId}'.");
            }

            var instantiatedRoot = UnityEngine.Object.Instantiate(prefab.gameObject, _screenLayerView.ContentRoot, false);
            if (instantiatedRoot == null)
            {
                throw new InvalidOperationException(
                    $"Screen prefab instantiation returned null for screen '{screenId}'.");
            }

            var view = instantiatedRoot.GetComponent<TView>();
            if (view == null)
            {
                throw new InvalidOperationException(
                    $"Screen prefab for '{screenId}' is missing the expected root view component '{typeof(TView).Name}'.");
            }

            if (view.transform.parent != _screenLayerView.ContentRoot)
            {
                throw new InvalidOperationException(
                    $"Screen '{screenId}' must mount directly beneath ScreenLayer content root.");
            }

            return view;
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
