using System;
using Game.Feature.Gameplay.Host;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;

namespace Game.Feature.UI.Composition
{
    internal sealed class GameplayPlayerActionCountTypographyController : IDisposable
    {
        private readonly IGameplayPlayerActionCountViewSource _viewSource;
        private readonly ILocalizedTextResolver _localeSource;
        private readonly GameplayUiTypographyTheme _theme;

        private GameplayPlayerActionCountView _currentView;
        private LocalizedTmpTypographyBinding _binding;
        private bool _isDisposed;

        public GameplayPlayerActionCountTypographyController(
            IGameplayPlayerActionCountViewSource viewSource,
            ILocalizedTextResolver localeSource,
            GameplayUiTypographyTheme theme)
        {
            _viewSource = viewSource ?? throw new ArgumentNullException(nameof(viewSource));
            _localeSource = localeSource ?? throw new ArgumentNullException(nameof(localeSource));
            _theme = theme ?? throw new ArgumentNullException(nameof(theme));

            _viewSource.CounterViewChanged += HandleCounterViewChanged;
            try
            {
                HandleCounterViewChanged(_viewSource.CurrentCounterView);
            }
            catch
            {
                _viewSource.CounterViewChanged -= HandleCounterViewChanged;
                throw;
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _viewSource.CounterViewChanged -= HandleCounterViewChanged;
            _binding?.Dispose();
            _binding = null;
            _currentView = null;
            _isDisposed = true;
        }

        private void HandleCounterViewChanged(GameplayPlayerActionCountView view)
        {
            if (_isDisposed || ReferenceEquals(_currentView, view))
            {
                return;
            }

            _binding?.Dispose();
            _binding = null;
            _currentView = null;
            if (view == null)
            {
                return;
            }

            if (view.CountLabel == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(GameplayPlayerActionCountView)} is missing its CountLabel typography target.");
            }

            _binding = new LocalizedTmpTypographyBinding(
                view.CountLabel,
                _localeSource,
                _theme,
                TypographyStyleTag.Value);
            _currentView = view;
        }
    }
}
