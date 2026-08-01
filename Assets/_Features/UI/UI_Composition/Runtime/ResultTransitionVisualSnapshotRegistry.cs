using System;
using Game.Feature.Stages;
using Game.Feature.UI.ViewShared;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    internal readonly struct ResultTransitionVisualPlaybackSnapshot
    {
        internal ResultTransitionVisualPlaybackSnapshot(
            TerminalSessionToken terminalToken,
            ResultDimVisualSnapshot dim,
            ResultTransitionRuntimeStyle runtimeStyle)
        {
            if (!terminalToken.IsValid)
            {
                throw new ArgumentException(
                    "Result transition visual playback requires a valid terminal token.",
                    nameof(terminalToken));
            }

            TerminalToken = terminalToken;
            Dim = dim;
            RuntimeStyle = runtimeStyle;
        }

        internal TerminalSessionToken TerminalToken { get; }
        internal ResultDimVisualSnapshot Dim { get; }
        internal ResultTransitionRuntimeStyle RuntimeStyle { get; }
    }

    internal static class ResultTransitionVisualSnapshotRegistry
    {
        private static ResultTransitionVisualPlaybackSnapshot? _current;

        internal static ResultTransitionVisualPlaybackSnapshot Capture(
            TerminalSessionToken terminalToken,
            ResultTransitionVisualStyle style)
        {
            if (style == null)
            {
                throw new InvalidOperationException(
                    "Result transition playback requires the canonical ResultTransitionVisualStyle asset.");
            }

            var snapshot = new ResultTransitionVisualPlaybackSnapshot(
                terminalToken,
                style.CreateDimSnapshot(),
                style.CreateRuntimeSnapshot());
            _current = snapshot;
            return snapshot;
        }

        internal static bool TryGet(
            TerminalSessionToken terminalToken,
            out ResultTransitionVisualPlaybackSnapshot snapshot)
        {
            if (_current.HasValue &&
                _current.Value.TerminalToken == terminalToken)
            {
                snapshot = _current.Value;
                return true;
            }

            snapshot = default;
            return false;
        }

        internal static ResultTransitionVisualPlaybackSnapshot RequireCurrent()
        {
            return _current ??
                   throw new InvalidOperationException(
                       "No immutable Result transition visual playback snapshot is active.");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _current = null;
        }

#if UNITY_EDITOR
        internal static void ResetForTests()
        {
            _current = null;
        }
#endif
    }
}
