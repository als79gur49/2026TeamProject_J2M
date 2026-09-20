using System;
using System.Collections.Generic;
using Game.Feature.DemoStageControl;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Stages;
using Game.Feature.UI.Composition;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.UI.Tests
{
    public sealed class DemoStageControlPresentationSourceTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            TerminalSessionRegistry.ResetForTests();
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void Source_PublishesCompletedChangedSnapshotOnce_AndDeduplicates()
        {
            var commands = new MutableCommands();
            using var source = CreateSource(commands);
            var published = new List<DemoStageControlPresentationSnapshot>();
            source.Changed += published.Add;

            source.Refresh();
            commands.Transitioning = true;
            source.Refresh();
            source.Refresh();

            Assert.That(published, Has.Count.EqualTo(1));
            Assert.That(published[0].Status.IsSceneTransitionInProgress, Is.True);
            Assert.That(source.Current, Is.EqualTo(published[0]));
        }

        [Test]
        public void CommandDecorators_PublishResultAndOverrideState()
        {
            var commands = new MutableCommands();
            using var source = CreateSource(commands);
            var stagePort = new RefreshingDemoStageControlCommandPort(commands, source);
            var overridePort = new RefreshingDemoGameplayOverrideCommandPort(commands, source);
            var publications = 0;
            source.Changed += _ => publications++;

            stagePort.ForceClearCurrentStage();
            overridePort.SetPlayerInvincible(true);

            Assert.That(publications, Is.EqualTo(2));
            Assert.That(source.Current.Status.LastResultMessage, Is.EqualTo("forced clear"));
            Assert.That(source.Current.OverrideStatus.PlayerInvincible, Is.True);
        }

        [Test]
        public void Source_Dispose_RemovesTerminalSubscription()
        {
            var commands = new MutableCommands();
            var source = CreateSource(commands);
            var publications = 0;
            source.Changed += _ => publications++;
            source.Dispose();

            var authority = TerminalSessionRegistry.Authority;
            var generation = authority.RegisterSceneBootstrap(7201, "demo-source-disposed");
            authority.TryClaim(new TerminalClaimRequest(
                TerminalTransitionKind.Defeat,
                generation,
                TerminalDestinationKind.ReloadedGameplay));

            Assert.That(publications, Is.EqualTo(0));
        }

        private DemoStageControlPresentationSource CreateSource(MutableCommands commands)
        {
            _root = new GameObject(nameof(DemoStageControlPresentationSourceTests));
            var transitions = _root.AddComponent<SceneTransitionCoordinator>();
            return new DemoStageControlPresentationSource(commands, commands, transitions);
        }

        private sealed class MutableCommands :
            IDemoStageControlCommandPort,
            IDemoGameplayOverrideCommandPort
        {
            private readonly StageId _stageId = StageId.CreateOrThrow("stage-0-1");
            private string _lastResult = string.Empty;
            private string _lastOverride = string.Empty;

            public bool Transitioning { get; set; }

            public bool PlayerInvincible { get; private set; }

            public IReadOnlyList<DemoStageControlStageItem> GetStages() => new[]
            {
                new DemoStageControlStageItem(_stageId, string.Empty, true, true),
            };

            public DemoStageControlStatus GetStatus() => new(
                _stageId, _stageId, Transitioning, false, _lastResult);

            public DemoStageControlResult StartStage(StageId stageId)
            {
                _lastResult = $"started {stageId.Value}";
                return DemoStageControlResult.Ok(_lastResult);
            }

            public DemoStageControlResult ForceClearCurrentStage()
            {
                _lastResult = "forced clear";
                return DemoStageControlResult.Ok(_lastResult);
            }

            public DemoStageControlResult SetPlayerInvincible(bool enabled)
            {
                PlayerInvincible = enabled;
                _lastOverride = enabled ? "on" : "off";
                return DemoStageControlResult.Ok(_lastOverride);
            }

            public DemoStageControlResult TogglePlayerInvincible() =>
                SetPlayerInvincible(!PlayerInvincible);

            public DemoGameplayOverrideSnapshot GetSnapshot() => new(PlayerInvincible);

            public DemoGameplayOverrideStatus GetOverrideStatus() =>
                new(PlayerInvincible, _lastOverride);
        }
    }
}
