using System;
using System.Collections.Generic;
using Game.Feature.DemoStageControl;
using Game.Feature.DemoStageControl.UI;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    public static class PausePopupProductionLocalizationComposer
    {
        public static IDisposable Bind(
            PausePopupView view,
            PausePopupPayload payload,
            ILocalizedTextResolver localizedTextResolver,
            ILocalizedTypographyResolver localizedTypographyResolver,
            GameplayUiTypographyTheme typographyTheme)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (localizedTextResolver == null)
            {
                throw new ArgumentNullException(nameof(localizedTextResolver));
            }

            if (localizedTypographyResolver == null)
            {
                throw new ArgumentNullException(nameof(localizedTypographyResolver));
            }

            if (typographyTheme == null)
            {
                throw new ArgumentNullException(nameof(typographyTheme));
            }

            view.BindExternalStaticLocalization();
            var targets = view.CreateStaticLocalizationTargets(payload);
            var bindings = new List<LocalizedTmpTextBinding>(targets.Count);
            try
            {
                for (var i = 0; i < targets.Count; i++)
                {
                    bindings.Add(new LocalizedTmpTextBinding(
                        targets[i].Target,
                        targets[i].Descriptor,
                        localizedTextResolver,
                        localizedTypographyResolver,
                        typographyTheme: typographyTheme));
                }

                return new BindingScope(
                    view,
                    bindings,
                    localizedTextResolver,
                    localizedTypographyResolver,
                    typographyTheme);
            }
            catch
            {
                DisposeBindings(bindings);
                view.UnbindStaticLocalization();
                throw;
            }
        }

        private static void DisposeBindings(List<LocalizedTmpTextBinding> bindings)
        {
            for (var i = 0; i < bindings.Count; i++)
            {
                bindings[i]?.Dispose();
            }

            bindings.Clear();
        }

        private sealed class BindingScope : IDisposable
        {
            private readonly PausePopupView _view;
            private readonly List<LocalizedTmpTextBinding> _bindings;
            private readonly List<LocalizedTmpTextBinding> _stageNameBindings = new(2);
            private readonly IReadOnlyList<TMP_Text> _stageNameTargets;
            private readonly ILocalizedTextResolver _localizedTextResolver;
            private readonly ILocalizedTypographyResolver _localizedTypographyResolver;
            private readonly GameplayUiTypographyTheme _typographyTheme;
            private bool _isDisposed;

            public BindingScope(
                PausePopupView view,
                List<LocalizedTmpTextBinding> bindings,
                ILocalizedTextResolver localizedTextResolver,
                ILocalizedTypographyResolver localizedTypographyResolver,
                GameplayUiTypographyTheme typographyTheme)
            {
                _view = view;
                _bindings = bindings;
                _stageNameTargets = view.CreateStageNameLocalizationTargets();
                _localizedTextResolver = localizedTextResolver;
                _localizedTypographyResolver = localizedTypographyResolver;
                _typographyTheme = typographyTheme;

                try
                {
                    ValidateStageNameTargets();
                    _view.SelectedStageDescriptorChanged += HandleSelectedStageDescriptorChanged;
                    RefreshStageNameBindings(_view.CurrentSelectedStageDescriptor);
                }
                catch
                {
                    _view.SelectedStageDescriptorChanged -= HandleSelectedStageDescriptorChanged;
                    DisposeBindings(_stageNameBindings);
                    throw;
                }
            }

            public void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }

                _view.SelectedStageDescriptorChanged -= HandleSelectedStageDescriptorChanged;
                DisposeBindings(_stageNameBindings);
                DisposeBindings(_bindings);
                _view.UnbindStaticLocalization();
                _isDisposed = true;
            }

            private void ValidateStageNameTargets()
            {
                if (_stageNameTargets == null || _stageNameTargets.Count != 2)
                {
                    throw new InvalidOperationException(
                        "PausePopup requires exactly two authored stage-name targets.");
                }

                for (var i = 0; i < _stageNameTargets.Count; i++)
                {
                    var target = _stageNameTargets[i];
                    if (target == null)
                    {
                        throw new InvalidOperationException(
                            $"PausePopup stage-name target at index {i} is not assigned.");
                    }

                    if (TypographyBinding.FindFor(target) == null)
                    {
                        throw new InvalidOperationException(
                            $"PausePopup stage-name target '{target.name}' requires an authored TypographyBinding.");
                    }
                }
            }

            private void HandleSelectedStageDescriptorChanged(LocalizedTextDescriptor descriptor)
            {
                RefreshStageNameBindings(descriptor);
            }

            private void RefreshStageNameBindings(LocalizedTextDescriptor descriptor)
            {
                DisposeBindings(_stageNameBindings);
                if (string.IsNullOrWhiteSpace(descriptor.Table) ||
                    string.IsNullOrWhiteSpace(descriptor.Key))
                {
                    for (var i = 0; i < _stageNameTargets.Count; i++)
                    {
                        _stageNameTargets[i].text = string.Empty;
                    }

                    return;
                }

                for (var i = 0; i < _stageNameTargets.Count; i++)
                {
                    var target = _stageNameTargets[i];
                    _stageNameBindings.Add(new LocalizedTmpTextBinding(
                        target,
                        descriptor,
                        _localizedTextResolver,
                        _localizedTypographyResolver,
                        typographyTheme: _typographyTheme,
                        typographyBinding: TypographyBinding.FindFor(target)));
                }
            }
        }
    }

    public static class ConfirmPopupProductionLocalizationComposer
    {
        private const int RequiredTargetCount = 5;

        public static IDisposable Bind(
            ConfirmPopupView view,
            ILocalizedTextResolver localizedTextResolver,
            GameplayUiTypographyTheme typographyTheme)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (localizedTextResolver == null)
            {
                throw new ArgumentNullException(nameof(localizedTextResolver));
            }

            if (typographyTheme == null)
            {
                throw new ArgumentNullException(nameof(typographyTheme));
            }

            var targets = view.CreateTypographyTargets();
            if (targets == null || targets.Count != RequiredTargetCount)
            {
                throw new InvalidOperationException(
                    $"ConfirmPopup requires exactly {RequiredTargetCount} production typography targets.");
            }

            var bindings = new List<LocalizedTmpTypographyBinding>(RequiredTargetCount);
            try
            {
                for (var i = 0; i < targets.Count; i++)
                {
                    if (targets[i] == null)
                    {
                        throw new InvalidOperationException(
                            $"ConfirmPopup production typography target at index {i} is not assigned.");
                    }

                    bindings.Add(new LocalizedTmpTypographyBinding(
                        targets[i],
                        localizedTextResolver,
                        typographyTheme));
                }
            }
            catch
            {
                DisposeBindings(bindings);
                throw;
            }

            return new BindingScope(bindings);
        }

        private static void DisposeBindings(List<LocalizedTmpTypographyBinding> bindings)
        {
            for (var i = 0; i < bindings.Count; i++)
            {
                bindings[i]?.Dispose();
            }

            bindings.Clear();
        }

        private sealed class BindingScope : IDisposable
        {
            private readonly List<LocalizedTmpTypographyBinding> _bindings;
            private bool _isDisposed;

            public BindingScope(List<LocalizedTmpTypographyBinding> bindings)
            {
                _bindings = bindings;
            }

            public void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }

                DisposeBindings(_bindings);
                _isDisposed = true;
            }
        }
    }

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
            var localizedBindings = PausePopupProductionLocalizationComposer.Bind(
                view,
                payload,
                _localizedTextResolver,
                _localizedTypographyResolver,
                _typographyTheme);
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
                    localizedBindings.Dispose();
                    view.Bind(null);
                    DestroyObject(view.gameObject);
                }));
        }

        private PopupRuntimeFactoryResult CreateConfirmPopup(ConfirmPopupPayload payload)
        {
            var presenter = new ConfirmPopupPresenter(_localizedTextResolver);
            presenter.Apply(payload);

            var view = InstantiatePopupPrefab(_popupPrefabCatalog.ConfirmPrefab, PopupId.Confirm);
            view.Bind(presenter.ViewModel);
            view.ConfigureActions(payload.ConfirmEnabled, payload.CancelEnabled, payload.ConsumeBack);
            var typographyBindings = ConfirmPopupProductionLocalizationComposer.Bind(
                view,
                _localizedTextResolver,
                _typographyTheme);
            view.IsVisible = true;

            return new PopupRuntimeFactoryResult(
                new PopupPolicy(
                    PopupPolicyClass.ModalBlocking,
                    PopupLifetimeScope.CurrentScreen,
                    payload.ConsumeBack ? PopupBackAction.Consume : PopupBackAction.Cancel,
                    PopupBackdropMode.Consume,
                    showsDim: true,
                    blocksLowerLayers: true),
                new PopupRuntime<ConfirmPopupView>(view, () =>
                {
                    typographyBindings.Dispose();
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
                    _localizedTextResolver,
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
