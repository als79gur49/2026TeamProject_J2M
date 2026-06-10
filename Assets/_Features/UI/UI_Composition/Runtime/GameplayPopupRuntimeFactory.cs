using System;
using Game.Feature.DemoStageControl;
using Game.Feature.DemoStageControl.UI;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.ViewShared;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    public sealed class GameplayPopupRuntimeFactory : IPopupRuntimeFactory
    {
        private readonly PopupPrefabCatalog _popupPrefabCatalog;
        private readonly PopupLayerView _popupLayerView;
        private readonly IDemoStageControlCommandPort _demoStageControlCommandPort;
        private readonly IDemoGameplayOverrideCommandPort _demoGameplayOverrideCommandPort;

        public GameplayPopupRuntimeFactory(
            PopupLayerView popupLayerView,
            PopupPrefabCatalog popupPrefabCatalog,
            IDemoStageControlCommandPort demoStageControlCommandPort = null,
            IDemoGameplayOverrideCommandPort demoGameplayOverrideCommandPort = null)
        {
            _popupLayerView = popupLayerView ?? throw new ArgumentNullException(nameof(popupLayerView));
            _popupPrefabCatalog = popupPrefabCatalog ?? throw new ArgumentNullException(nameof(popupPrefabCatalog));
            _demoStageControlCommandPort = demoStageControlCommandPort;
            _demoGameplayOverrideCommandPort = demoGameplayOverrideCommandPort;
        }

        public PopupRuntimeFactoryResult Create(PopupRequest request)
        {
            switch (request.PopupId)
            {
                case PopupId.Pause:
                    return CreatePausePopup(ExpectPayload<PausePopupPayload>(request.Payload));

                case PopupId.ObjectiveInfo:
                    return CreateObjectiveInfoPopup(ExpectPayload<ObjectiveInfoPopupPayload>(request.Payload));

                case PopupId.Confirm:
                    return CreateConfirmPopup(ExpectPayload<ConfirmPopupPayload>(request.Payload));

                case PopupId.Tooltip:
                    return CreateTooltipPopup(ExpectPayload<TooltipPopupPayload>(request.Payload));

                case PopupId.DemoStageControl:
                    return CreateDemoStageControlPopup(ExpectPayload<DemoStageControlPanelPayload>(request.Payload));

                default:
                    throw new InvalidOperationException($"Unsupported popup id: {request.PopupId}");
            }
        }

        private PopupRuntimeFactoryResult CreatePausePopup(PausePopupPayload payload)
        {
            var presenter = new PausePopupPresenter();
            presenter.Apply(payload);

            var view = InstantiatePopupPrefab(_popupPrefabCatalog.PausePrefab, PopupId.Pause);
            view.Bind(presenter.ViewModel);
            view.IsVisible = true;

            return new PopupRuntimeFactoryResult(
                new PopupPolicy(
                    PopupPolicyClass.ModalBlocking,
                    PopupLifetimeScope.CurrentScreen,
                    PopupBackAction.Close,
                    PopupBackdropMode.Consume,
                    showsDim: true,
                    blocksLowerLayers: true),
                new PopupRuntime<PausePopupView>(view, () =>
                {
                    view.Bind(null);
                    DestroyObject(view.gameObject);
                }));
        }

        private PopupRuntimeFactoryResult CreateObjectiveInfoPopup(ObjectiveInfoPopupPayload payload)
        {
            var presenter = new ObjectiveInfoPopupPresenter();
            presenter.Apply(payload);

            var view = InstantiatePopupPrefab(_popupPrefabCatalog.ObjectiveInfoPrefab, PopupId.ObjectiveInfo);
            view.Bind(presenter.ViewModel);
            view.IsVisible = true;

            return new PopupRuntimeFactoryResult(
                new PopupPolicy(
                    PopupPolicyClass.NonModalInformational,
                    PopupLifetimeScope.CurrentScreen,
                    PopupBackAction.Close,
                    PopupBackdropMode.None,
                    showsDim: false,
                    blocksLowerLayers: false),
                new PopupRuntime<ObjectiveInfoPopupView>(view, () =>
                {
                    view.Bind(null);
                    DestroyObject(view.gameObject);
                }));
        }

        private PopupRuntimeFactoryResult CreateConfirmPopup(ConfirmPopupPayload payload)
        {
            var presenter = new ConfirmPopupPresenter();
            presenter.Apply(payload);

            var view = InstantiatePopupPrefab(_popupPrefabCatalog.ConfirmPrefab, PopupId.Confirm);
            view.Bind(presenter.ViewModel);
            view.IsVisible = true;

            return new PopupRuntimeFactoryResult(
                new PopupPolicy(
                    PopupPolicyClass.ModalBlocking,
                    PopupLifetimeScope.CurrentScreen,
                    PopupBackAction.Cancel,
                    PopupBackdropMode.Consume,
                    showsDim: true,
                    blocksLowerLayers: true),
                new PopupRuntime<ConfirmPopupView>(view, () =>
                {
                    view.Bind(null);
                    DestroyObject(view.gameObject);
                }));
        }

        private PopupRuntimeFactoryResult CreateTooltipPopup(TooltipPopupPayload payload)
        {
            var presenter = new TooltipPopupPresenter();
            presenter.Apply(payload);

            var view = InstantiatePopupPrefab(_popupPrefabCatalog.TooltipPrefab, PopupId.Tooltip);
            view.Bind(presenter.ViewModel);
            view.IsVisible = true;

            return new PopupRuntimeFactoryResult(
                new PopupPolicy(
                    PopupPolicyClass.AnchoredEphemeral,
                    PopupLifetimeScope.CurrentScreen,
                    PopupBackAction.Close,
                    PopupBackdropMode.None,
                    showsDim: false,
                    blocksLowerLayers: false),
                new PopupRuntime<TooltipPopupView>(view, () =>
                {
                    view.Bind(null);
                    DestroyObject(view.gameObject);
                }));
        }

        private PopupRuntimeFactoryResult CreateDemoStageControlPopup(DemoStageControlPanelPayload payload)
        {
            if (_demoStageControlCommandPort == null)
            {
                throw new InvalidOperationException("Demo Stage Control command port is not available.");
            }

            var view = DemoStageControlPanelView.CreateRuntime(_popupLayerView.ContentRoot);
            view.IsVisible = true;

            return new PopupRuntimeFactoryResult(
                new PopupPolicy(
                    PopupPolicyClass.ModalBlocking,
                    PopupLifetimeScope.CurrentScreen,
                    PopupBackAction.Close,
                    PopupBackdropMode.Consume,
                    showsDim: true,
                    blocksLowerLayers: true),
                new DemoStageControlPanelRuntime(
                    view,
                    _demoStageControlCommandPort,
                    _demoGameplayOverrideCommandPort,
                    payload,
                    () => DestroyObject(view.gameObject)));
        }

        private TView InstantiatePopupPrefab<TView>(TView prefab, PopupId popupId)
            where TView : Component, IPopupView
        {
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"Popup prefab catalog is missing a canonical prefab for popup '{popupId}'.");
            }

            var prefabRoot = prefab.gameObject;
            var instantiatedRoot = UnityEngine.Object.Instantiate(prefabRoot, _popupLayerView.ContentRoot, false);
            if (instantiatedRoot == null)
            {
                throw new InvalidOperationException(
                    $"Popup prefab instantiation returned null for popup '{popupId}'.");
            }

            var view = instantiatedRoot.GetComponent<TView>();
            if (view == null)
            {
                throw new InvalidOperationException(
                    $"Popup prefab for '{popupId}' is missing the expected root view component '{typeof(TView).Name}'.");
            }

            if (view.transform.parent != _popupLayerView.ContentRoot)
            {
                throw new InvalidOperationException(
                    $"Popup '{popupId}' must mount directly beneath PopupLayer content root.");
            }

            return view;
        }

        private static TPayload ExpectPayload<TPayload>(IPopupPayload payload) where TPayload : class, IPopupPayload
        {
            if (payload is not TPayload typedPayload)
            {
                throw new InvalidOperationException($"Popup payload type mismatch. Expected {typeof(TPayload).Name}.");
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

        private sealed class PopupRuntime<TView> : IPopupRuntime, IUiNavigationTargetProvider where TView : Component, IPopupView
        {
            private readonly Action _dispose;
            private readonly TView _view;

            public PopupRuntime(TView view, Action dispose)
            {
                _view = view ?? throw new ArgumentNullException(nameof(view));
                _dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));
            }

            public event Action<PopupCompletionKind> CompletionRequested
            {
                add => _view.CompletionRequested += value;
                remove => _view.CompletionRequested -= value;
            }

            public void Dispose()
            {
                _view.IsVisible = false;
                _dispose();
            }

            public void SetIsTopmost(bool isTopmost)
            {
                _view.IsVisible = true;
                _view.SetIsTopmost(isTopmost);
            }

            public bool TryGetNavigationTarget(out IUiNavigationTarget target)
            {
                target = _view as IUiNavigationTarget;
                return target != null;
            }
        }
    }
}
