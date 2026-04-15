using System;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Composition
{
    public sealed class GameplayPopupRuntimeFactory : IPopupRuntimeFactory
    {
        private readonly PopupLayerView _popupLayerView;

        public GameplayPopupRuntimeFactory(PopupLayerView popupLayerView)
        {
            _popupLayerView = popupLayerView ?? throw new ArgumentNullException(nameof(popupLayerView));
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

                default:
                    throw new InvalidOperationException($"Unsupported popup id: {request.PopupId}");
            }
        }

        private PopupRuntimeFactoryResult CreatePausePopup(PausePopupPayload payload)
        {
            var presenter = new PausePopupPresenter();
            presenter.Apply(payload);

            var panel = UiCanvasElementFactory.CreatePanel(
                "PausePopup",
                _popupLayerView.ContentRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(280f, 160f),
                Vector2.zero);
            var canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
            var view = panel.gameObject.AddComponent<PausePopupView>();
            var title = UiCanvasElementFactory.CreateLabel("Title", panel, new Vector2(16f, -20f), new Vector2(248f, 24f), TextAnchor.MiddleCenter, 18);
            var description = UiCanvasElementFactory.CreateLabel("Description", panel, new Vector2(16f, -56f), new Vector2(248f, 40f), TextAnchor.MiddleCenter, 15);
            var resume = UiCanvasElementFactory.CreateButton("ResumeButton", panel, payload.ResumeLabel, new Vector2(91f, -118f), new Vector2(98f, 28f));
            view.Configure(panel.gameObject, canvasGroup, title, description, resume);
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
                    DestroyObject(panel.gameObject);
                }));
        }

        private PopupRuntimeFactoryResult CreateObjectiveInfoPopup(ObjectiveInfoPopupPayload payload)
        {
            var presenter = new ObjectiveInfoPopupPresenter();
            presenter.Apply(payload);

            var panel = UiCanvasElementFactory.CreatePanel(
                "ObjectiveInfoPopup",
                _popupLayerView.ContentRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(340f, 200f),
                new Vector2(0f, -20f));
            var canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
            var view = panel.gameObject.AddComponent<ObjectiveInfoPopupView>();
            var title = UiCanvasElementFactory.CreateLabel("Title", panel, new Vector2(16f, -16f), new Vector2(308f, 24f), TextAnchor.MiddleCenter, 18);
            var body = UiCanvasElementFactory.CreateLabel("Body", panel, new Vector2(16f, -52f), new Vector2(308f, 86f), TextAnchor.UpperLeft, 14);
            var close = UiCanvasElementFactory.CreateButton("CloseButton", panel, payload.CloseLabel, new Vector2(121f, -160f), new Vector2(98f, 28f));
            view.Configure(panel.gameObject, canvasGroup, title, body, close);
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
                    DestroyObject(panel.gameObject);
                }));
        }

        private PopupRuntimeFactoryResult CreateConfirmPopup(ConfirmPopupPayload payload)
        {
            var presenter = new ConfirmPopupPresenter();
            presenter.Apply(payload);

            var panel = UiCanvasElementFactory.CreatePanel(
                "ConfirmPopup",
                _popupLayerView.ContentRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(360f, 190f),
                new Vector2(0f, -10f));
            var canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
            var view = panel.gameObject.AddComponent<ConfirmPopupView>();
            var title = UiCanvasElementFactory.CreateLabel("Title", panel, new Vector2(16f, -20f), new Vector2(328f, 24f), TextAnchor.MiddleCenter, 18);
            var body = UiCanvasElementFactory.CreateLabel("Body", panel, new Vector2(16f, -58f), new Vector2(328f, 58f), TextAnchor.UpperLeft, 14);
            var cancel = UiCanvasElementFactory.CreateButton("CancelButton", panel, payload.CancelLabel, new Vector2(64f, -148f), new Vector2(100f, 30f));
            var confirm = UiCanvasElementFactory.CreateButton("ConfirmButton", panel, payload.ConfirmLabel, new Vector2(196f, -148f), new Vector2(100f, 30f));
            view.Configure(
                panel.gameObject,
                canvasGroup,
                title,
                body,
                confirm,
                cancel,
                UiCanvasElementFactory.GetButtonLabel(confirm),
                UiCanvasElementFactory.GetButtonLabel(cancel),
                confirm.GetComponent<Image>());
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
                    DestroyObject(panel.gameObject);
                }));
        }

        private PopupRuntimeFactoryResult CreateTooltipPopup(TooltipPopupPayload payload)
        {
            var presenter = new TooltipPopupPresenter();
            presenter.Apply(payload);

            var panel = UiCanvasElementFactory.CreatePanel(
                "TooltipPopup",
                _popupLayerView.ContentRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(260f, 132f),
                new Vector2(0f, 160f));
            panel.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.13f, 0.94f);
            var canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
            var view = panel.gameObject.AddComponent<TooltipPopupView>();
            var title = UiCanvasElementFactory.CreateLabel("Title", panel, new Vector2(14f, -14f), new Vector2(204f, 20f), TextAnchor.MiddleLeft, 15);
            var dismiss = UiCanvasElementFactory.CreateButton("DismissButton", panel, "x", new Vector2(220f, -12f), new Vector2(24f, 24f));
            var body = UiCanvasElementFactory.CreateLabel("Body", panel, new Vector2(14f, -40f), new Vector2(230f, 70f), TextAnchor.UpperLeft, 13);
            view.Configure(panel.gameObject, canvasGroup, panel, title, body, dismiss);
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
                    DestroyObject(panel.gameObject);
                }));
        }

        private PopupRuntimeFactoryResult CreateRewardPopup(RewardPopupPayload payload)
        {
            var presenter = new RewardPopupPresenter();
            presenter.Apply(payload);

            var panel = UiCanvasElementFactory.CreatePanel(
                "RewardPopup",
                _popupLayerView.ContentRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(380f, 240f),
                new Vector2(0f, -12f));
            var canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
            var view = panel.gameObject.AddComponent<RewardPopupView>();
            var title = UiCanvasElementFactory.CreateLabel("Title", panel, new Vector2(16f, -18f), new Vector2(348f, 24f), TextAnchor.MiddleCenter, 18);
            var first = UiCanvasElementFactory.CreateLabel("FirstReward", panel, new Vector2(24f, -58f), new Vector2(332f, 22f), TextAnchor.MiddleLeft, 15);
            var second = UiCanvasElementFactory.CreateLabel("SecondReward", panel, new Vector2(24f, -88f), new Vector2(332f, 22f), TextAnchor.MiddleLeft, 15);
            var third = UiCanvasElementFactory.CreateLabel("ThirdReward", panel, new Vector2(24f, -118f), new Vector2(332f, 22f), TextAnchor.MiddleLeft, 15);
            var summary = UiCanvasElementFactory.CreateLabel("Summary", panel, new Vector2(24f, -158f), new Vector2(332f, 34f), TextAnchor.UpperLeft, 14);
            var close = UiCanvasElementFactory.CreateButton("CollectButton", panel, payload.CloseLabel, new Vector2(140f, -204f), new Vector2(100f, 30f));
            view.Configure(
                panel.gameObject,
                canvasGroup,
                title,
                first,
                second,
                third,
                summary,
                close,
                UiCanvasElementFactory.GetButtonLabel(close));
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
                    DestroyObject(panel.gameObject);
                }));
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

        private sealed class PopupRuntime<TView> : IPopupRuntime where TView : Component, IPopupView
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
        }
    }
}
