using System;
using System.Collections.Generic;
using Game.Feature.DemoStageControl;
using Game.Feature.DemoStageControl.UI;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
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
        private readonly ILocalizedTextResolver _localizedTextResolver;
        private readonly ILocalizedTypographyResolver _localizedTypographyResolver;
        private readonly GameplayUiTypographyTheme _typographyTheme;

        public GameplayPopupRuntimeFactory(
            PopupLayerView popupLayerView,
            PopupPrefabCatalog popupPrefabCatalog,
            IDemoStageControlCommandPort demoStageControlCommandPort = null,
            IDemoGameplayOverrideCommandPort demoGameplayOverrideCommandPort = null,
            ILocalizedTextResolver localizedTextResolver = null,
            ILocalizedTypographyResolver localizedTypographyResolver = null,
            GameplayUiTypographyTheme typographyTheme = null)
        {
            _popupLayerView = popupLayerView ?? throw new ArgumentNullException(nameof(popupLayerView));
            _popupPrefabCatalog = popupPrefabCatalog ?? throw new ArgumentNullException(nameof(popupPrefabCatalog));
            _demoStageControlCommandPort = demoStageControlCommandPort;
            _demoGameplayOverrideCommandPort = demoGameplayOverrideCommandPort;
            _localizedTextResolver = localizedTextResolver
                ?? throw new InvalidOperationException(
                    "GameplayPopupRuntimeFactory requires an explicit production localized text resolver.");
            _localizedTypographyResolver = localizedTypographyResolver ?? DefaultLocalizedTypographyResolver.Instance;
            _typographyTheme = typographyTheme ?? _popupPrefabCatalog.TypographyTheme;
        }

        public PopupRuntimeFactoryResult Create(PopupRequest request)
        {
            switch (request.PopupId)
            {
                case PopupId.Pause:
                    return CreatePausePopup(ExpectPayload<PausePopupPayload>(request.Payload));

                case PopupId.Confirm:
                    return CreateConfirmPopup(ExpectPayload<ConfirmPopupPayload>(request.Payload));

                case PopupId.DemoStageControl:
                    return CreateDemoStageControlPopup(ExpectPayload<DemoStageControlPanelPayload>(request.Payload));

                default:
                    throw new InvalidOperationException($"Unsupported popup id: {request.PopupId}");
            }
        }

        private PopupRuntimeFactoryResult CreatePausePopup(PausePopupPayload payload)
        {
            var presenter = new PausePopupPresenter(_localizedTextResolver);
            presenter.Apply(payload);

            var view = InstantiatePopupPrefab(_popupPrefabCatalog.PausePrefab, PopupId.Pause);
            view.Bind(presenter.ViewModel);
            var localizedBindings = BindPauseStaticLocalization(view, payload);
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
                    DisposeBindings(localizedBindings);
                    view.UnbindStaticLocalization();
                    view.Bind(null);
                    DestroyObject(view.gameObject);
                }));
        }

        private List<LocalizedTmpTextBinding> BindPauseStaticLocalization(
            PausePopupView view,
            PausePopupPayload payload)
        {
            view.BindExternalStaticLocalization();
            var targets = view.CreateStaticLocalizationTargets(payload);
            var bindings = new List<LocalizedTmpTextBinding>(targets.Count);
            for (var i = 0; i < targets.Count; i++)
            {
                bindings.Add(new LocalizedTmpTextBinding(
                    targets[i].Target,
                    targets[i].Descriptor,
                    _localizedTextResolver,
                    _localizedTypographyResolver,
                    typographyTheme: _typographyTheme));
            }

            return bindings;
        }

        private static void DisposeBindings(List<LocalizedTmpTextBinding> bindings)
        {
            if (bindings == null)
            {
                return;
            }

            for (var i = 0; i < bindings.Count; i++)
            {
                bindings[i]?.Dispose();
            }

            bindings.Clear();
        }

        private PopupRuntimeFactoryResult CreateConfirmPopup(ConfirmPopupPayload payload)
        {
            var presenter = new ConfirmPopupPresenter(_localizedTextResolver);
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
                    presenter.Dispose();
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
