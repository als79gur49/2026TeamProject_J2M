using System;
using System.Collections.Generic;
using Game.Feature.DemoStageControl;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Stages;

namespace Game.Feature.UI.Composition
{
    internal sealed class DemoStageControlPresentationSource :
        IDemoStageControlPresentationSource,
        IDisposable
    {
        private readonly IDemoStageControlCommandPort _commands;
        private readonly IDemoGameplayOverrideCommandPort _overrides;
        private readonly SceneTransitionCoordinator _sceneTransitions;
        private bool _disposed;

        public DemoStageControlPresentationSource(
            IDemoStageControlCommandPort commands,
            IDemoGameplayOverrideCommandPort overrides,
            SceneTransitionCoordinator sceneTransitions)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _overrides = overrides;
            _sceneTransitions = sceneTransitions
                ?? throw new ArgumentNullException(nameof(sceneTransitions));
            Current = ReadCurrent();
            _sceneTransitions.TransitionStateChanged += HandleTransitionStateChanged;
            TerminalSessionRegistry.Changed += HandleTerminalSessionChanged;
        }

        public DemoStageControlPresentationSnapshot Current { get; private set; }

        public event Action<DemoStageControlPresentationSnapshot> Changed;

        public void Refresh()
        {
            if (_disposed)
            {
                return;
            }

            var next = ReadCurrent();
            if (next.Equals(Current))
            {
                return;
            }

            Current = next;
            Changed?.Invoke(next);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _sceneTransitions.TransitionStateChanged -= HandleTransitionStateChanged;
            TerminalSessionRegistry.Changed -= HandleTerminalSessionChanged;
            Changed = null;
            _disposed = true;
        }

        private DemoStageControlPresentationSnapshot ReadCurrent()
        {
            return new DemoStageControlPresentationSnapshot(
                _commands.GetStages(),
                _commands.GetStatus(),
                _overrides?.GetOverrideStatus() ?? default);
        }

        private void HandleTransitionStateChanged(bool _) => Refresh();

        private void HandleTerminalSessionChanged(TerminalSessionSnapshot _) => Refresh();
    }

    internal sealed class RefreshingDemoStageControlCommandPort : IDemoStageControlCommandPort
    {
        private readonly IDemoStageControlCommandPort _inner;
        private readonly DemoStageControlPresentationSource _source;

        public RefreshingDemoStageControlCommandPort(
            IDemoStageControlCommandPort inner,
            DemoStageControlPresentationSource source)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public IReadOnlyList<DemoStageControlStageItem> GetStages() => _inner.GetStages();

        public DemoStageControlStatus GetStatus() => _inner.GetStatus();

        public DemoStageControlResult StartStage(StageId stageId)
        {
            var result = _inner.StartStage(stageId);
            _source.Refresh();
            return result;
        }

        public DemoStageControlResult ForceClearCurrentStage()
        {
            var result = _inner.ForceClearCurrentStage();
            _source.Refresh();
            return result;
        }
    }

    internal sealed class RefreshingDemoGameplayOverrideCommandPort : IDemoGameplayOverrideCommandPort
    {
        private readonly IDemoGameplayOverrideCommandPort _inner;
        private readonly DemoStageControlPresentationSource _source;

        public RefreshingDemoGameplayOverrideCommandPort(
            IDemoGameplayOverrideCommandPort inner,
            DemoStageControlPresentationSource source)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public bool PlayerInvincible => _inner.PlayerInvincible;

        public DemoStageControlResult SetPlayerInvincible(bool enabled)
        {
            var result = _inner.SetPlayerInvincible(enabled);
            _source.Refresh();
            return result;
        }

        public DemoStageControlResult TogglePlayerInvincible()
        {
            var result = _inner.TogglePlayerInvincible();
            _source.Refresh();
            return result;
        }

        public DemoGameplayOverrideSnapshot GetSnapshot() => _inner.GetSnapshot();

        public DemoGameplayOverrideStatus GetOverrideStatus() => _inner.GetOverrideStatus();
    }
}
