using System;
using Game.Feature.Stages;
using Game.Feature.UI.Composition;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.UI.Tests
{
    public sealed class GameplayTerminalTransitionPortTests
    {
        private const string MotionProfilePath =
            "Assets/_Features/UI/UI_Composition/Resources/UI/Transitions/TerminalIrisMotionProfile.asset";

        [TestCase(false)]
        [TestCase(true)]
        public void IrisNotificationFailure_DiscardsCandidateBeforeShow(bool throwFromObserver)
        {
            var authority = new PersistentTerminalSessionAuthority();
            var scene = authority.RegisterSceneBootstrap(20, "iris-notification");
            var claim = authority.TryClaim(new TerminalClaimRequest(TerminalTransitionKind.Defeat,
                scene, TerminalDestinationKind.ReloadedGameplay));
            var view = new ThrowingSetupView(SetupThrowPoint.Show, new InvalidOperationException("unused"));
            view.DisableFailure();
            var profile = AssetDatabase.LoadAssetAtPath<TerminalIrisMotionProfile>(MotionProfilePath);
            TerminalTransitionPlayback candidate = null;
            using var port = new GameplayTerminalTransitionPort(view, profile.CreateResolver(), null, authority,
                preset => candidate = new TerminalTransitionPlayback(preset));
            authority.Changed += snapshot =>
            {
                if (snapshot.Phase != TerminalSessionPhase.Iris) return;
                if (throwFromObserver) throw new InvalidOperationException("iris observer failed");
                port.TryAbortSetup(claim.Token, new TerminalFailure("Recovered", "backup recovered"));
            };
            var request = new TerminalTransitionRequest(TerminalTransitionKind.Defeat, 10,
                claim.Token, TerminalTransitionDestinationMode.SceneHandoff);
            if (throwFromObserver)
                Assert.Throws<InvalidOperationException>(() => port.TryBegin(request, out _));
            else
                Assert.That(port.TryBegin(request, out _), Is.False);
            Assert.That(candidate.State, Is.EqualTo(TerminalTransitionState.Disposed));
            Assert.That(port.CurrentPlayback, Is.Null);
            Assert.That(TerminalTransitionRegistry.Current, Is.Null);
            Assert.That(authority.IsActive, Is.False);
            Assert.That(view.ShowCalls, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LocalIrisAbort_ReleasesOnlyUnboundMatchingPlayback(bool coordinatorOwnsTransition)
        {
            var authority = new PersistentTerminalSessionAuthority();
            var scene = authority.RegisterSceneBootstrap(20, "local-iris");
            var claim = authority.TryClaim(new TerminalClaimRequest(TerminalTransitionKind.Defeat,
                scene, TerminalDestinationKind.ReloadedGameplay));
            var view = new ThrowingSetupView(SetupThrowPoint.Show, new InvalidOperationException("unused"));
            view.DisableFailure();
            var profile = AssetDatabase.LoadAssetAtPath<TerminalIrisMotionProfile>(MotionProfilePath);
            using var port = new GameplayTerminalTransitionPort(view, profile.CreateResolver(), null, authority);
            Assert.That(port.TryBegin(new TerminalTransitionRequest(TerminalTransitionKind.Defeat, 10,
                claim.Token, TerminalTransitionDestinationMode.SceneHandoff), out var playback), Is.True);
            var failure = new TerminalFailure("RouteRejected", "immediate rejection");
            Assert.That(port.TryAbortSetup(default, failure), Is.False);
            if (coordinatorOwnsTransition)
                authority.TryBindTransition(claim.Token, 42, TerminalDestinationKind.ReloadedGameplay);
            Assert.That(port.TryAbortSetup(claim.Token, failure), Is.EqualTo(!coordinatorOwnsTransition));
            Assert.That(authority.IsActive, Is.EqualTo(coordinatorOwnsTransition));
            Assert.That(view.IsVisible, Is.EqualTo(coordinatorOwnsTransition));
            if (!coordinatorOwnsTransition)
            {
                Assert.That(playback.State, Is.EqualTo(TerminalTransitionState.Disposed));
                Assert.That(port.CurrentPlayback, Is.Null);
                Assert.That(authority.Phase, Is.EqualTo(TerminalSessionPhase.FailedBeforeCover));
            }
        }

        [TestCase(SetupThrowPoint.Show)]
        [TestCase(SetupThrowPoint.Apply)]
        [TestCase(SetupThrowPoint.MaterialDiagnostics)]
        [Category("Extended")]
        public void TerminalIris_SetupThrow_AbortsPartialPlaybackAndExactSession(
            SetupThrowPoint throwPoint)
        {
            var authority = new PersistentTerminalSessionAuthority(authorityGeneration: 901);
            var sceneGeneration = authority.RegisterSceneBootstrap(17, "iris-setup-red");
            var claim = authority.TryClaim(new TerminalClaimRequest(
                TerminalTransitionKind.Defeat,
                sceneGeneration,
                TerminalDestinationKind.ReloadedGameplay));
            var setupException = new InvalidOperationException($"{throwPoint}-setup-failure");
            var view = new ThrowingSetupView(throwPoint, setupException);
            TerminalTransitionPlayback candidate = null;
            var profile = AssetDatabase.LoadAssetAtPath<TerminalIrisMotionProfile>(MotionProfilePath);
            Assert.That(profile, Is.Not.Null, MotionProfilePath);

            using var port = new GameplayTerminalTransitionPort(
                view,
                profile.CreateResolver(),
                focusTargetSource: null,
                authority,
                preset => candidate = new TerminalTransitionPlayback(preset));

            var thrown = Assert.Throws<InvalidOperationException>(() =>
                port.TryBegin(
                    new TerminalTransitionRequest(
                        TerminalTransitionKind.Defeat,
                        focusEntityId: 10,
                        claim.Token,
                        TerminalTransitionDestinationMode.SceneHandoff),
                    out _));

            Assert.That(thrown, Is.SameAs(setupException));
            Assert.That(candidate, Is.Not.Null);
            Assert.That(candidate.State, Is.EqualTo(TerminalTransitionState.Disposed));
            Assert.That(ReadEvent(candidate, "StateChanged"), Is.Null);
            Assert.That(ReadEvent(candidate, "Cancelled"), Is.Null);
            Assert.That(port.CurrentPlayback, Is.Null);
            Assert.That(TerminalTransitionRegistry.Current, Is.Null);
            Assert.That(view.HideCalls, Is.EqualTo(1));
            Assert.That(view.IsVisible, Is.False);
            Assert.That(authority.IsActive, Is.False);
            Assert.That(authority.Current.Token, Is.EqualTo(claim.Token));
            Assert.That(authority.Current.FailureReason, Does.Contain(setupException.Message));

            var next = authority.TryClaim(new TerminalClaimRequest(
                TerminalTransitionKind.Defeat,
                sceneGeneration,
                TerminalDestinationKind.ReloadedGameplay));
            Assert.That(next.Accepted, Is.True);
            view.DisableFailure();
            Assert.That(
                port.TryBegin(
                    new TerminalTransitionRequest(
                        TerminalTransitionKind.Defeat,
                        focusEntityId: 10,
                        next.Token,
                        TerminalTransitionDestinationMode.SceneHandoff),
                    out var nextPlayback),
                Is.True);
            Assert.That(nextPlayback, Is.SameAs(port.CurrentPlayback));
        }

        private static Delegate ReadEvent(TerminalTransitionPlayback playback, string eventName)
        {
            return (Delegate)typeof(TerminalTransitionPlayback)
                .GetField(eventName, System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(playback);
        }

        public enum SetupThrowPoint
        {
            Show,
            Apply,
            MaterialDiagnostics,
        }

        private sealed class ThrowingSetupView : ITerminalIrisSetupView
        {
            private SetupThrowPoint? _throwPoint;
            private readonly Exception _exception;

            internal ThrowingSetupView(SetupThrowPoint throwPoint, Exception exception)
            {
                _throwPoint = throwPoint;
                _exception = exception;
            }

            internal int HideCalls { get; private set; }
            internal int ShowCalls { get; private set; }

            internal bool IsVisible { get; private set; }

            internal void DisableFailure() => _throwPoint = null;

            public Vector2 LastAppliedCenterForDiagnostics { get; private set; }

            public int LastMaterialApplicationFrameForDiagnostics { get; private set; } = -1;

            public ResultTransitionVisualStyle RequireVisualStyle() =>
                throw new InvalidOperationException("Victory visual style is not used by this fixture.");

            public void ConfigureDimSnapshot(ResultDimVisualSnapshot snapshot)
            {
            }

            public void ConfigureTransitionColor(Color color)
            {
            }

            public float CalculateFullyRevealedRadius(Vector2 center, float fullOpenMargin) => 4f;

            public void Show()
            {
                ShowCalls++;
                IsVisible = true;
                if (_throwPoint == SetupThrowPoint.Show)
                {
                    throw _exception;
                }
            }

            public void Apply(TerminalTransitionPlayback playback)
            {
                LastAppliedCenterForDiagnostics = playback.CurrentCenter;
                LastMaterialApplicationFrameForDiagnostics = Time.frameCount;
                if (_throwPoint == SetupThrowPoint.Apply)
                {
                    throw _exception;
                }
            }

            public Vector2 ReadMaterialCenterForDiagnostics()
            {
                if (_throwPoint == SetupThrowPoint.MaterialDiagnostics)
                {
                    throw _exception;
                }

                return LastAppliedCenterForDiagnostics;
            }

            public void Hide()
            {
                HideCalls++;
                IsVisible = false;
            }
        }
    }
}
