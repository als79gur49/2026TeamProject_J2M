using System;
using Game.Feature.UI.HUD;
using Game.Feature.UI.ViewShared;

namespace Game.Feature.UI.Application
{
    public sealed class HUDRootPresenter : IDisposable
    {
        private readonly IGameplayUiPresentationSource _presentationSource;
        private readonly StageInfoPresenter _stageInfoPresenter;
        private readonly ObjectiveHudPresenter _objectiveHudPresenter;
        private readonly ChancePanelPresenter _chancePanelPresenter;
        private readonly HealthPanelPresenter _healthPanelPresenter;
        private readonly SurfaceBeltIndicatorPresenter _surfaceBeltIndicatorPresenter;

        public HUDRootPresenter(
            IGameplayUiPresentationSource presentationSource,
            StageInfoPresenter stageInfoPresenter,
            ObjectiveHudPresenter objectiveHudPresenter,
            ChancePanelPresenter chancePanelPresenter,
            SurfaceBeltIndicatorPresenter surfaceBeltIndicatorPresenter,
            HealthPanelPresenter healthPanelPresenter = null)
        {
            _presentationSource = presentationSource ?? throw new ArgumentNullException(nameof(presentationSource));
            _stageInfoPresenter = stageInfoPresenter ?? throw new ArgumentNullException(nameof(stageInfoPresenter));
            _objectiveHudPresenter = objectiveHudPresenter ?? throw new ArgumentNullException(nameof(objectiveHudPresenter));
            _chancePanelPresenter = chancePanelPresenter ?? throw new ArgumentNullException(nameof(chancePanelPresenter));
            _surfaceBeltIndicatorPresenter = surfaceBeltIndicatorPresenter ?? throw new ArgumentNullException(nameof(surfaceBeltIndicatorPresenter));

            _healthPanelPresenter = healthPanelPresenter;
            ViewModel = new HUDRootViewModel();
            _presentationSource.SnapshotChanged += HandleSnapshotChanged;

            ApplySnapshot(_presentationSource.CurrentSnapshot);
        }

        public HUDRootViewModel ViewModel { get; }

        public void Dispose()
        {
            _presentationSource.SnapshotChanged -= HandleSnapshotChanged;
            _stageInfoPresenter.Dispose();
            _objectiveHudPresenter.Dispose();
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
                isPauseButtonEnabled: !snapshot.Interaction.IsPaused &&
                                      !snapshot.Interaction.IsUiGameplayInputBlocked);

            _stageInfoPresenter.Apply(snapshot.Stage);
            _objectiveHudPresenter.Apply(snapshot.Objective);
            _chancePanelPresenter.Apply(snapshot.Chance);
            _healthPanelPresenter?.Apply(snapshot.Health);
            _surfaceBeltIndicatorPresenter.Apply(snapshot.SurfaceBelt);
        }
    }

    public sealed class HealthPanelPresenter
    {
        public HealthPanelViewModel ViewModel { get; } = new();

        public void Apply(UIHealthSlice health)
        {
            ViewModel.SetHealth(health.HasHealth, health.Hp, health.MaxHp);
        }
    }

    public sealed class StageInfoPresenter : IDisposable
    {
        private readonly ILocalizedTextResolver _localizedTextResolver;
        private UIStageSlice _lastStage = UIStageSlice.Empty;

        public StageInfoPresenter(ILocalizedTextResolver localizedTextResolver = null)
        {
            _localizedTextResolver = localizedTextResolver;
            if (_localizedTextResolver != null)
            {
                _localizedTextResolver.LocaleChanged += HandleLocaleChanged;
            }
        }

        public StageInfoViewModel ViewModel { get; } = new();

        public void Apply(UIStageSlice stage)
        {
            _lastStage = stage;
            ViewModel.SetStageName(ResolveStageName(stage));
        }

        public void Dispose()
        {
            if (_localizedTextResolver != null)
            {
                _localizedTextResolver.LocaleChanged -= HandleLocaleChanged;
            }
        }

        private void HandleLocaleChanged()
        {
            ViewModel.SetStageName(ResolveStageName(_lastStage));
        }

        private string ResolveStageName(UIStageSlice stage)
        {
            if (_localizedTextResolver != null && !string.IsNullOrWhiteSpace(stage.DisplayNameKey))
            {
                return _localizedTextResolver.Resolve(stage.DisplayNameDescriptor);
            }

            return string.Empty;
        }
    }
}
