using System;
using Game.Feature.UI.HUD;

namespace Game.Feature.UI.Application
{
    public sealed class HUDRootPresenter : IDisposable
    {
        private readonly PlayerStatusPresenter _playerStatusPresenter;
        private readonly IGameplayUiPresentationSource _presentationSource;
        private readonly StageInfoPresenter _stageInfoPresenter;
        private readonly ObjectiveHudPresenter _objectiveHudPresenter;
        private readonly ChancePanelPresenter _chancePanelPresenter;
        private readonly SurfaceBeltIndicatorPresenter _surfaceBeltIndicatorPresenter;

        public HUDRootPresenter(
            IGameplayUiPresentationSource presentationSource,
            StageInfoPresenter stageInfoPresenter,
            ObjectiveHudPresenter objectiveHudPresenter,
            ChancePanelPresenter chancePanelPresenter,
            SurfaceBeltIndicatorPresenter surfaceBeltIndicatorPresenter,
            PlayerStatusPresenter playerStatusPresenter)
        {
            _presentationSource = presentationSource ?? throw new ArgumentNullException(nameof(presentationSource));
            _stageInfoPresenter = stageInfoPresenter ?? throw new ArgumentNullException(nameof(stageInfoPresenter));
            _objectiveHudPresenter = objectiveHudPresenter ?? throw new ArgumentNullException(nameof(objectiveHudPresenter));
            _chancePanelPresenter = chancePanelPresenter ?? throw new ArgumentNullException(nameof(chancePanelPresenter));
            _surfaceBeltIndicatorPresenter = surfaceBeltIndicatorPresenter ?? throw new ArgumentNullException(nameof(surfaceBeltIndicatorPresenter));
            _playerStatusPresenter = playerStatusPresenter ?? throw new ArgumentNullException(nameof(playerStatusPresenter));

            ViewModel = new HUDRootViewModel();
            _presentationSource.SnapshotChanged += HandleSnapshotChanged;

            ApplySnapshot(_presentationSource.CurrentSnapshot);
        }

        public HUDRootPresenter(
            IGameplayUiPresentationSource presentationSource,
            StageInfoPresenter stageInfoPresenter,
            ObjectiveHudPresenter objectiveHudPresenter,
            PlayerStatusPresenter playerStatusPresenter)
            : this(
                presentationSource,
                stageInfoPresenter,
                objectiveHudPresenter,
                new ChancePanelPresenter(),
                new SurfaceBeltIndicatorPresenter(),
                playerStatusPresenter)
        {
        }

        public HUDRootViewModel ViewModel { get; }

        public void Dispose()
        {
            _presentationSource.SnapshotChanged -= HandleSnapshotChanged;
        }

        private void HandleSnapshotChanged(UIPresentationSnapshot snapshot)
        {
            ApplySnapshot(snapshot);
        }

        private void ApplySnapshot(UIPresentationSnapshot snapshot)
        {
            ViewModel.SetShellState(
                isDimmed: snapshot.Interaction.IsPaused ||
                          snapshot.Interaction.IsUiGameplayInputBlocked ||
                          snapshot.Interaction.HasBlockingGameplayPresentation,
                isGameplayReadOnly: snapshot.Tick.IsStageCleared ||
                                    snapshot.Interaction.IsPaused ||
                                    snapshot.Interaction.IsUiGameplayInputBlocked ||
                                    snapshot.Interaction.HasBlockingGameplayPresentation ||
                                    !snapshot.Interaction.CanAcceptGameplayCommands,
                isPauseButtonEnabled: !snapshot.Interaction.IsPaused &&
                                      !snapshot.Interaction.IsUiGameplayInputBlocked);

            _stageInfoPresenter.Apply(snapshot.Stage);
            _objectiveHudPresenter.Apply(snapshot.Objective);
            _chancePanelPresenter.Apply(snapshot.Chance);
            _surfaceBeltIndicatorPresenter.Apply(snapshot.SurfaceBelt);
            _playerStatusPresenter.Apply(snapshot.Tick, snapshot.Interaction, snapshot.Player);
        }
    }

    public sealed class StageInfoPresenter
    {
        public StageInfoViewModel ViewModel { get; } = new();

        public void Apply(UIStageSlice stage)
        {
            ViewModel.SetStageName(stage.DisplayName);
        }
    }
}
