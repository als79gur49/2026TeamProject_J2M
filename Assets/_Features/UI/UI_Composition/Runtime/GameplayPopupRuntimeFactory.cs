using System;
using Game.Feature.DemoStageControl;
using Game.Feature.DemoStageControl.UI;
using Game.Feature.Gameplay.UIAccess.DebugCommands;
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
        private readonly DebugCommandAccess _debugCommandAccess;
        private readonly Action<DebugCommandResult> _debugStageResultRequested;
        private readonly IDemoStageControlCommandPort _demoStageControlCommandPort;
        private readonly IDemoGameplayOverrideCommandPort _demoGameplayOverrideCommandPort;
        private readonly Func<bool> _isDebugCommandsRuntimeEnabled;

        public GameplayPopupRuntimeFactory(
            PopupLayerView popupLayerView,
            PopupPrefabCatalog popupPrefabCatalog,
            DebugCommandAccess debugCommandAccess = null,
            Action<DebugCommandResult> debugStageResultRequested = null,
            IDemoStageControlCommandPort demoStageControlCommandPort = null,
            IDemoGameplayOverrideCommandPort demoGameplayOverrideCommandPort = null,
            Func<bool> isDebugCommandsRuntimeEnabled = null)
        {
            _popupLayerView = popupLayerView ?? throw new ArgumentNullException(nameof(popupLayerView));
            _popupPrefabCatalog = popupPrefabCatalog ?? throw new ArgumentNullException(nameof(popupPrefabCatalog));
            _debugCommandAccess = debugCommandAccess ?? DebugCommandAccess.Disabled;
            _debugStageResultRequested = debugStageResultRequested;
            _demoStageControlCommandPort = demoStageControlCommandPort;
            _demoGameplayOverrideCommandPort = demoGameplayOverrideCommandPort;
            _isDebugCommandsRuntimeEnabled = isDebugCommandsRuntimeEnabled ?? IsDebugCommandsRuntimeEnabled;
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

                case PopupId.Reward:
                    return CreateRewardPopup(ExpectPayload<RewardPopupPayload>(request.Payload));

                case PopupId.DemoStageControl:
                    return CreateDemoStageControlPopup(ExpectPayload<DemoStageControlPanelPayload>(request.Payload));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                case PopupId.DebugCommands:
                    return CreateDebugCommandsPopup(ExpectPayload<DebugCommandsPopupPayload>(request.Payload));
#endif

                default:
                    throw new InvalidOperationException($"Unsupported popup id: {request.PopupId}");
            }
        }

        internal static DebugCommandsPopupPayload BuildDebugCommandsPayload(
            DebugCommandAvailabilitySnapshot availability,
            string lastCommandMessage)
        {
            return new DebugCommandsPopupPayload(
                "Debug Commands",
                availability.IsDebugBuildEnabled ? "Debug build: enabled" : "Debug build: disabled",
                availability.CurrentStageId.IsValid
                    ? $"Current StageId: {availability.CurrentStageId.Value}"
                    : "Current StageId: none",
                BuildNextStageStatusText(availability),
                availability.IsPresentationLocked ? "Topology/Presentation lock: active" : "Topology/Presentation lock: clear",
                availability.CanForceClearResultOnly ? "Force Clear Result Only: available" : $"Force Clear Result Only: unavailable ({availability.ReasonText})",
                lastCommandMessage,
                availability.CanGoNextStage,
                availability.CanForceClearResultOnly);
        }

        private static string BuildNextStageStatusText(DebugCommandAvailabilitySnapshot availability)
        {
            var nextStageText = availability.NextStageId.IsValid
                ? $"Next StageId: {availability.NextStageId.Value}"
                : "Next StageId: No next stage";

            return availability.CanGoNextStage || string.IsNullOrWhiteSpace(availability.ReasonText)
                ? nextStageText
                : $"{nextStageText} (unavailable: {availability.ReasonText})";
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

        private PopupRuntimeFactoryResult CreateRewardPopup(RewardPopupPayload payload)
        {
            var presenter = new RewardPopupPresenter();
            presenter.Apply(payload);

            var view = InstantiatePopupPrefab(_popupPrefabCatalog.RewardPrefab, PopupId.Reward);
            view.Bind(presenter.ViewModel);
            view.IsVisible = true;

            return new PopupRuntimeFactoryResult(
                new PopupPolicy(
                    PopupPolicyClass.ExplicitCloseRewardResult,
                    PopupLifetimeScope.CurrentScreen,
                    PopupBackAction.Consume,
                    PopupBackdropMode.Consume,
                    showsDim: true,
                    blocksLowerLayers: true),
                new PopupRuntime<RewardPopupView>(view, () =>
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private PopupRuntimeFactoryResult CreateDebugCommandsPopup(DebugCommandsPopupPayload payload)
        {
            if (!_isDebugCommandsRuntimeEnabled() || !_debugCommandAccess.IsEnabled)
            {
                throw new InvalidOperationException("Debug commands popup is disabled for this runtime.");
            }

            var presenter = new DebugCommandsPopupPresenter();
            presenter.Apply(payload);

            var viewRoot = new GameObject(nameof(DebugCommandsPopupView), typeof(RectTransform));
            viewRoot.transform.SetParent(_popupLayerView.ContentRoot, false);
            var view = viewRoot.AddComponent<DebugCommandsPopupView>();
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
                new DebugCommandsPopupRuntime(
                    view,
                    presenter,
                    _debugCommandAccess,
                    _debugStageResultRequested,
                    () => DestroyObject(view.gameObject)));
        }
#endif

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

        private static bool IsDebugCommandsRuntimeEnabled()
        {
            return DebugCommandBuildGate.IsRuntimeEnabled(
                UnityEngine.Application.isEditor,
                UnityEngine.Debug.isDebugBuild);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private sealed class DebugCommandsPopupRuntime : IPopupRuntime, IUiNavigationTargetProvider
        {
            private readonly DebugCommandAccess _debugCommandAccess;
            private readonly Action<DebugCommandResult> _debugStageResultRequested;
            private readonly Action _dispose;
            private readonly DebugCommandsPopupPresenter _presenter;
            private readonly DebugCommandsPopupView _view;

            public DebugCommandsPopupRuntime(
                DebugCommandsPopupView view,
                DebugCommandsPopupPresenter presenter,
                DebugCommandAccess debugCommandAccess,
                Action<DebugCommandResult> debugStageResultRequested,
                Action dispose)
            {
                _view = view ?? throw new ArgumentNullException(nameof(view));
                _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
                _debugCommandAccess = debugCommandAccess ?? DebugCommandAccess.Disabled;
                _debugStageResultRequested = debugStageResultRequested;
                _dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));
                _view.NextStageRequested += HandleNextStageRequested;
                _view.ForceClearResultOnlyRequested += HandleForceClearResultOnlyRequested;
            }

            public event Action<PopupCompletionKind> CompletionRequested
            {
                add => _view.CompletionRequested += value;
                remove => _view.CompletionRequested -= value;
            }

            public void Dispose()
            {
                _view.NextStageRequested -= HandleNextStageRequested;
                _view.ForceClearResultOnlyRequested -= HandleForceClearResultOnlyRequested;
                _view.Bind(null);
                _dispose();
            }

            public void SetIsTopmost(bool isTopmost)
            {
                _view.SetIsTopmost(isTopmost);
            }

            public bool TryGetNavigationTarget(out IUiNavigationTarget target)
            {
                target = _view;
                return target != null;
            }

            private void HandleNextStageRequested()
            {
                var result = _debugCommandAccess.StageCommandPort.GoToNextStage();
                Refresh(result.Message);
            }

            private void HandleForceClearResultOnlyRequested()
            {
                var result = _debugCommandAccess.StageCommandPort.ForceClearResultOnly();
                Refresh(result.Message);
                if (result.IsSuccess && result.StageResultReadModel != null)
                {
                    _debugStageResultRequested?.Invoke(result);
                }
            }

            private void Refresh(string lastCommandMessage)
            {
                _presenter.Apply(BuildDebugCommandsPayload(
                    _debugCommandAccess.StageCommandPort.GetAvailability(),
                    lastCommandMessage));
            }
        }
#endif

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
