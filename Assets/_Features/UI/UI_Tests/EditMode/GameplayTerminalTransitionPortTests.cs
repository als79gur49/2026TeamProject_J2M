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
