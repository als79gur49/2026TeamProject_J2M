using System;
using Game.Feature.Stages;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    internal enum TerminalFocusTransportSource
    {
        ProductionPlayerProjection = 0,
        MotionProfileFallback = 1,
    }

    internal readonly struct TerminalFocusTransportDiagnostics
    {
        internal TerminalFocusTransportDiagnostics(
            TerminalTransitionRequest request,
            TerminalSessionSnapshot session,
            Vector2 requestCenter,
            bool requestFocusValid,
            TerminalFocusTransportSource focusSource,
            Vector2 playbackSnapshotCenter,
            bool playbackFocusValid,
            Vector2 overlayCenter,
            Vector2 materialCenter,
            int materialApplicationFrame)
        {
            Request = request;
            Session = session;
            RequestCenter = requestCenter;
            RequestFocusValid = requestFocusValid;
            FocusSource = focusSource;
            PlaybackSnapshotCenter = playbackSnapshotCenter;
            PlaybackFocusValid = playbackFocusValid;
            OverlayCenter = overlayCenter;
            MaterialCenter = materialCenter;
            MaterialApplicationFrame = materialApplicationFrame;
        }

        internal TerminalTransitionRequest Request { get; }

        internal TerminalSessionSnapshot Session { get; }

        internal Vector2 RequestCenter { get; }

        internal bool RequestFocusValid { get; }

        internal TerminalFocusTransportSource FocusSource { get; }

        internal Vector2 PlaybackSnapshotCenter { get; }

        internal bool PlaybackFocusValid { get; }

        internal Vector2 OverlayCenter { get; }

        internal Vector2 MaterialCenter { get; }

        internal int MaterialApplicationFrame { get; }
    }

    internal static class TerminalTransitionRegistry
    {
        internal static TerminalTransitionPlayback Current { get; private set; }

        internal static void Set(TerminalTransitionPlayback playback)
        {
            Current = playback;
        }

        internal static void Clear(TerminalTransitionPlayback playback)
        {
            if (ReferenceEquals(Current, playback))
            {
                Current = null;
            }
        }
    }

    internal sealed class GameplayTerminalTransitionPort : ITerminalTransitionPort, IDisposable
    {
        private readonly ITerminalFocusTargetSource _focusTargetSource;
        private readonly PersistentTerminalSessionAuthority _terminalAuthority;
        private readonly TerminalIrisOverlayView _view;
        private readonly TerminalIrisMotionProfileResolver _motionProfileResolver;
        private TerminalTransitionPlayback _playback;
        private bool _disposed;

        public GameplayTerminalTransitionPort(
            TerminalIrisOverlayView view,
            TerminalIrisMotionProfileResolver motionProfileResolver,
            ITerminalFocusTargetSource focusTargetSource,
            PersistentTerminalSessionAuthority terminalAuthority = null)
        {
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));
            _motionProfileResolver = motionProfileResolver ??
                throw new ArgumentNullException(nameof(motionProfileResolver));
            _focusTargetSource = focusTargetSource;
            _terminalAuthority = terminalAuthority ?? TerminalSessionRegistry.Authority;
            TerminalDestinationReadiness.DestinationReady += HandleDestinationReady;
        }

        internal TerminalTransitionPlayback CurrentPlayback => _playback;

        internal TerminalFocusCaptureDiagnostics LastFocusCaptureDiagnostics { get; private set; }

        internal TerminalFocusTransportDiagnostics LastFocusTransportDiagnostics { get; private set; }

        internal bool CompleteToResultBackdrop(TerminalSessionToken token)
        {
            if (_disposed ||
                _playback == null ||
                !_terminalAuthority.IsActive ||
                _terminalAuthority.ActiveToken != token ||
                _terminalAuthority.Current.TerminalKind != TerminalTransitionKind.Victory ||
                _terminalAuthority.Phase != TerminalSessionPhase.WaitingSameSceneDestination ||
                !TerminalDestinationReadiness.IsReady(token))
            {
                return false;
            }

            if (!_terminalAuthority.TryAdvancePhase(token, TerminalSessionPhase.ResultBackdropHandoff))
            {
                throw new InvalidOperationException(
                    $"Result backdrop handoff for token {token} could not advance terminal authority.");
            }

            TerminalRuntimeTrace.Record(
                _terminalAuthority.Current,
                TerminalTraceEvent.ResultBackdropHandoff);
            if (!_playback.CompleteToResultBackdrop(token))
            {
                throw new InvalidOperationException(
                    $"Result backdrop handoff for token {token} could not hide the completed Iris.");
            }

            return true;
        }

        public bool TryBegin(
            TerminalTransitionRequest request,
            out TerminalTransitionPlayback playback)
        {
            playback = _playback;
            if (_disposed || (_playback != null && !_playback.IsTerminal))
            {
                return false;
            }

            ReleasePlayback();
            if (request.Kind == TerminalTransitionKind.Victory)
            {
                var visualSnapshot = ResultTransitionVisualSnapshotRegistry.Capture(
                    request.Token,
                    _view.RequireVisualStyle());
                _view.ConfigureDimSnapshot(visualSnapshot.Dim);
            }
            else
            {
                var defeatVisual = _motionProfileResolver.ResolveRetryVisual(
                    SceneTransitionIntent.DeathRetry);
                _view.ConfigureTransitionColor(defeatVisual.SourceCloseColor);
            }

            var preset = _motionProfileResolver.ResolveClose(request.Kind);
            var focus = new TerminalFocusTarget(
                preset.FallbackCenter,
                preset.FallbackRadius,
                isFallback: true);
            if (_focusTargetSource != null &&
                _focusTargetSource.TryCapture(request.FocusEntityId, out var capturedFocus))
            {
                focus = capturedFocus;
            }

            if (_focusTargetSource is GameplayTerminalFocusTargetSource productionFocusSource)
            {
                LastFocusCaptureDiagnostics = productionFocusSource.LastCaptureDiagnostics;
                if (!LastFocusCaptureDiagnostics.Succeeded)
                {
                    Debug.LogWarning(
                        "TerminalFocusCaptureFailed " +
                        $"token={request.Token} " +
                        $"terminalKind={request.Kind} " +
                        $"playerEntityId={request.FocusEntityId} " +
                        $"failureReason={LastFocusCaptureDiagnostics.FailureReason} " +
                        $"outputCamera={LastFocusCaptureDiagnostics.OutputCamera?.name ?? "null"} " +
                        $"outputCameraInstanceId={LastFocusCaptureDiagnostics.OutputCamera?.GetInstanceID() ?? 0} " +
                        $"viewFound={LastFocusCaptureDiagnostics.View != null} " +
                        $"viewInstanceId={LastFocusCaptureDiagnostics.View?.GetInstanceID() ?? 0} " +
                        $"rendererCount={LastFocusCaptureDiagnostics.RendererCount}");
                }
            }

            var candidate = new TerminalTransitionPlayback(preset);
            var closeFullyRevealedRadius = Mathf.Max(
                _view.CalculateFullyRevealedRadius(preset.FallbackCenter, 0f),
                _view.CalculateFullyRevealedRadius(focus.NormalizedCenter, 0f));
            var revealFullyRevealedRadius = preset.RevealPreset.HasValue
                ? _view.CalculateFullyRevealedRadius(
                    focus.NormalizedCenter,
                    preset.RevealPreset.Value.FullOpenMargin)
                : closeFullyRevealedRadius;
            candidate.ConfigureFullyRevealedRadii(
                closeFullyRevealedRadius,
                revealFullyRevealedRadius);
            if (!candidate.TryBegin(request, focus))
            {
                candidate.Dispose();
                return false;
            }

            if (!_terminalAuthority.IsActive ||
                _terminalAuthority.ActiveToken != request.Token ||
                _terminalAuthority.Current.TerminalKind != request.Kind ||
                _terminalAuthority.Phase != TerminalSessionPhase.Claimed)
            {
                candidate.Dispose();
                return false;
            }

            if (!_terminalAuthority.TryAdvancePhase(request.Token, TerminalSessionPhase.Iris))
            {
                candidate.Dispose();
                return false;
            }
            _playback = candidate;
            _playback.StateChanged += HandleStateChanged;
            _playback.Cancelled += HandleCancelled;
            _view.Show();
            _view.Apply(_playback);
            var appliedMaterialCenter = _view.RuntimeMaterialForTests.GetVector("_Center");
            LastFocusTransportDiagnostics = new TerminalFocusTransportDiagnostics(
                request,
                _terminalAuthority.Current,
                focus.NormalizedCenter,
                !focus.IsFallback,
                focus.IsFallback
                    ? TerminalFocusTransportSource.MotionProfileFallback
                    : TerminalFocusTransportSource.ProductionPlayerProjection,
                candidate.FocusTarget.NormalizedCenter,
                !candidate.FocusTarget.IsFallback,
                _view.LastAppliedCenterForDiagnostics,
                new Vector2(appliedMaterialCenter.x, appliedMaterialCenter.y),
                _view.LastMaterialApplicationFrameForDiagnostics);
            TerminalTransitionRegistry.Set(_playback);
            playback = _playback;
            return true;
        }

        internal void Tick(float unscaledDeltaTime)
        {
            if (_disposed || _playback == null || _playback.IsTerminal)
            {
                return;
            }

            _playback.Advance(Math.Max(0f, unscaledDeltaTime));
            if (!_playback.IsTerminal)
            {
                _view.Apply(_playback);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            TerminalDestinationReadiness.DestinationReady -= HandleDestinationReady;
            if (_playback != null && !_playback.IsTerminal)
            {
                _playback.Cancel(_playback.Request.Token);
            }

            ReleasePlayback();
            if (_view != null)
            {
                _view.Hide();
            }
        }

        private void HandleStateChanged(TerminalTransitionPlayback playback)
        {
            if (!ReferenceEquals(playback, _playback))
            {
                return;
            }

            if (playback.State == TerminalTransitionState.Completed ||
                playback.State == TerminalTransitionState.Cancelled)
            {
                if (playback.State == TerminalTransitionState.Completed)
                {
                    if (!playback.CompletedToResultBackdrop)
                    {
                        TerminalRuntimeTrace.Record(
                            _terminalAuthority.Current,
                            TerminalTraceEvent.RevealCompleted);
                    }
                }

                _view.Hide();
                if (playback.State == TerminalTransitionState.Completed)
                {
                    TerminalRuntimeTrace.Record(
                        _terminalAuthority.Current,
                        TerminalTraceEvent.OverlayHidden);
                }
            }

            if (playback.State == TerminalTransitionState.Black)
            {
                _terminalAuthority.TryAdvancePhase(
                    playback.Request.Token,
                    TerminalSessionPhase.Black);
                if (playback.Request.DestinationMode == TerminalTransitionDestinationMode.SameScene)
                {
                    _terminalAuthority.TryAdvancePhase(
                        playback.Request.Token,
                        TerminalSessionPhase.WaitingSameSceneDestination);
                }
            }
            else if (playback.State == TerminalTransitionState.Revealing)
            {
                _terminalAuthority.TryAdvancePhase(
                    playback.Request.Token,
                    TerminalSessionPhase.Revealing);
            }
            else if (playback.State == TerminalTransitionState.Completed &&
                     !playback.CompletedToResultBackdrop &&
                     playback.Request.DestinationMode == TerminalTransitionDestinationMode.SameScene)
            {
                _terminalAuthority.TryComplete(playback.Request.Token);
            }
        }

        private void HandleCancelled(TerminalTransitionPlayback playback)
        {
            _terminalAuthority.TryFail(
                playback.Request.Token,
                new TerminalFailure(
                    "IrisCancelled",
                    "Terminal Iris playback was cancelled before reveal completed."));
        }

        private void HandleDestinationReady(DestinationReadinessSignal signal)
        {
            if (_disposed ||
                _playback == null ||
                _playback.Request.Token != signal.Token ||
                signal.Outcome != DestinationReadinessOutcome.Ready ||
                _playback.Request.DestinationMode != TerminalTransitionDestinationMode.SameScene)
            {
                return;
            }

            if (_playback.Request.Kind == TerminalTransitionKind.Victory)
            {
                return;
            }

            TerminalRuntimeTrace.Record(
                _terminalAuthority.Current,
                TerminalTraceEvent.RevealRequested);
            if (_playback.State == TerminalTransitionState.Black)
            {
                TerminalRuntimeTrace.Record(
                    _terminalAuthority.Current,
                    TerminalTraceEvent.RevealAccepted);
            }

            if (!_playback.RequestReveal(signal.Token))
            {
                TerminalRuntimeTrace.Record(
                    _terminalAuthority.Current,
                    TerminalTraceEvent.RevealRejected,
                    accepted: false,
                    "REQUEST_REVEAL_REJECTED");
                throw new InvalidOperationException(
                    $"Terminal destination readiness for token {signal.Token} arrived outside the matching black state.");
            }
        }

        private void ReleasePlayback()
        {
            if (_playback == null)
            {
                return;
            }

            TerminalTransitionRegistry.Clear(_playback);
            _playback.StateChanged -= HandleStateChanged;
            _playback.Cancelled -= HandleCancelled;
            _playback.Dispose();
            _playback = null;
        }
    }
}
