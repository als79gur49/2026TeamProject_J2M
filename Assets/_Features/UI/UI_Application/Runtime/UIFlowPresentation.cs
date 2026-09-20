using System;
using Game.Feature.UI.HUD;
using Game.Feature.UI.Popups;

namespace Game.Feature.UI.Application
{
    public readonly struct UIFlowPresentationSnapshot : IEquatable<UIFlowPresentationSnapshot>
    {
        public static readonly UIFlowPresentationSnapshot GameplayDefault = new(
            isHudVisible: true,
            isUiGameplayInputBlocked: false,
            isPopupLayerVisible: false,
            showsPopupDim: false,
            blocksLowerLayerPointer: false,
            PopupBackdropMode.None);

        public UIFlowPresentationSnapshot(
            bool isHudVisible,
            bool isUiGameplayInputBlocked,
            bool isPopupLayerVisible,
            bool showsPopupDim,
            bool blocksLowerLayerPointer,
            PopupBackdropMode popupBackdropMode)
        {
            IsHudVisible = isHudVisible;
            IsUiGameplayInputBlocked = isUiGameplayInputBlocked;
            IsPopupLayerVisible = isPopupLayerVisible;
            ShowsPopupDim = showsPopupDim;
            BlocksLowerLayerPointer = blocksLowerLayerPointer;
            PopupBackdropMode = popupBackdropMode;
        }

        public bool IsHudVisible { get; }

        public bool IsUiGameplayInputBlocked { get; }

        public bool IsPopupLayerVisible { get; }

        public bool ShowsPopupDim { get; }

        public bool BlocksLowerLayerPointer { get; }

        public PopupBackdropMode PopupBackdropMode { get; }

        public bool Equals(UIFlowPresentationSnapshot other)
        {
            return IsHudVisible == other.IsHudVisible &&
                   IsUiGameplayInputBlocked == other.IsUiGameplayInputBlocked &&
                   IsPopupLayerVisible == other.IsPopupLayerVisible &&
                   ShowsPopupDim == other.ShowsPopupDim &&
                   BlocksLowerLayerPointer == other.BlocksLowerLayerPointer &&
                   PopupBackdropMode == other.PopupBackdropMode;
        }

        public override bool Equals(object obj)
        {
            return obj is UIFlowPresentationSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                IsHudVisible,
                IsUiGameplayInputBlocked,
                IsPopupLayerVisible,
                ShowsPopupDim,
                BlocksLowerLayerPointer,
                PopupBackdropMode);
        }
    }

    public interface IUIFlowPresentationSource
    {
        UIFlowPresentationSnapshot Current { get; }

        event Action<UIFlowPresentationSnapshot> Changed;
    }

    public sealed class UIFlowShellPresenter : IDisposable
    {
        private readonly HUDRootView _hudView;
        private readonly PopupLayerView _popupLayerView;
        private readonly IGameplayUiPresentationSource _gameplayPresentationSource;
        private readonly IUIFlowPresentationSource _flowPresentationSource;

        public UIFlowShellPresenter(
            IUIFlowPresentationSource flowPresentationSource,
            IGameplayUiPresentationSource gameplayPresentationSource,
            HUDRootView hudView,
            PopupLayerView popupLayerView)
        {
            _flowPresentationSource = flowPresentationSource ??
                                      throw new ArgumentNullException(nameof(flowPresentationSource));
            _gameplayPresentationSource = gameplayPresentationSource ??
                                          throw new ArgumentNullException(nameof(gameplayPresentationSource));
            _hudView = hudView ?? throw new ArgumentNullException(nameof(hudView));
            _popupLayerView = popupLayerView ?? throw new ArgumentNullException(nameof(popupLayerView));

            _flowPresentationSource.Changed += HandleFlowPresentationChanged;
            Apply(_flowPresentationSource.Current);
        }

        public void Dispose()
        {
            _flowPresentationSource.Changed -= HandleFlowPresentationChanged;
        }

        private void HandleFlowPresentationChanged(UIFlowPresentationSnapshot snapshot)
        {
            Apply(snapshot);
        }

        private void Apply(UIFlowPresentationSnapshot snapshot)
        {
            _gameplayPresentationSource.UpdateUiGameplayInputBlocked(
                snapshot.IsUiGameplayInputBlocked);
            _hudView.IsVisible = snapshot.IsHudVisible;
            _popupLayerView.SetState(
                snapshot.IsPopupLayerVisible,
                snapshot.ShowsPopupDim,
                snapshot.BlocksLowerLayerPointer,
                snapshot.PopupBackdropMode);
        }
    }
}
