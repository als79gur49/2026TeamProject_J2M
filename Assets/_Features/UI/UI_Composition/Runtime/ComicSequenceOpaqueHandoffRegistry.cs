using System;
using System.Threading;
using Game.Feature.Stages;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    internal enum ComicSequenceOpaqueHandoffPhase
    {
        Inactive = 0,
        Claimed = 1,
        ComicSequenceOpaqueRendered = 2,
        PersistentCoverRendered = 3,
        Released = 4,
        FailedHoldingOpaque = 5,
    }

    internal readonly struct ComicSequenceOpaqueHandoffToken :
        IEquatable<ComicSequenceOpaqueHandoffToken>
    {
        internal ComicSequenceOpaqueHandoffToken(long value)
        {
            Value = value;
        }

        internal long Value { get; }
        internal bool IsValid => Value > 0;
        public bool Equals(ComicSequenceOpaqueHandoffToken other) =>
            Value == other.Value;
        public override bool Equals(object obj) =>
            obj is ComicSequenceOpaqueHandoffToken other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => IsValid ? Value.ToString() : "none";
        public static bool operator ==(
            ComicSequenceOpaqueHandoffToken left,
            ComicSequenceOpaqueHandoffToken right) => left.Equals(right);
        public static bool operator !=(
            ComicSequenceOpaqueHandoffToken left,
            ComicSequenceOpaqueHandoffToken right) => !left.Equals(right);
    }

    internal readonly struct ComicSequenceOpaqueHandoffSnapshot
    {
        internal ComicSequenceOpaqueHandoffSnapshot(
            bool isActive,
            ComicSequenceOpaqueHandoffToken token,
            SceneTransitionIntent intent,
            ComicSequenceOpaqueHandoffPhase phase,
            long sourceSceneGeneration,
            Color opaqueColor,
            string failureReason)
        {
            IsActive = isActive;
            Token = token;
            Intent = intent;
            Phase = phase;
            SourceSceneGeneration = sourceSceneGeneration;
            opaqueColor.a = 1f;
            OpaqueColor = opaqueColor;
            FailureReason = failureReason ?? string.Empty;
        }

        internal bool IsActive { get; }
        internal ComicSequenceOpaqueHandoffToken Token { get; }
        internal SceneTransitionIntent Intent { get; }
        internal ComicSequenceOpaqueHandoffPhase Phase { get; }
        internal long SourceSceneGeneration { get; }
        internal Color OpaqueColor { get; }
        internal string FailureReason { get; }
    }

    internal static class ComicSequenceOpaqueHandoffRegistry
    {
        private static ComicSequenceOpaqueHandoffSnapshot _current;
        private static Action _releaseOpaqueOwner;
        private static long _nextToken;

        internal static ComicSequenceOpaqueHandoffSnapshot Current => _current;
        internal static bool IsActive => _current.IsActive;

        internal static bool TryClaim(
            SceneTransitionIntent intent,
            long sourceSceneGeneration,
            Color opaqueColor,
            Action releaseOpaqueOwner,
            out ComicSequenceOpaqueHandoffToken token)
        {
            token = default;
            if (_current.IsActive ||
                sourceSceneGeneration <= 0 ||
                releaseOpaqueOwner == null ||
                (intent != SceneTransitionIntent.ComicIntroToGameplay &&
                 intent != SceneTransitionIntent.ComicOutroToMainMenu) ||
                !IsFinite(opaqueColor))
            {
                return false;
            }

            var routePolicy =
                SceneTransitionRoutePolicyCatalog.ResolveProduction(intent);
            if (routePolicy.Status != SceneTransitionRouteStatus.Canonical ||
                !routePolicy.TargetRequiresOpaqueOwnerTransfer ||
                !routePolicy.ImplementsOpaqueOwnerTransfer)
            {
                return false;
            }

            token = new ComicSequenceOpaqueHandoffToken(
                Interlocked.Increment(ref _nextToken));
            _releaseOpaqueOwner = releaseOpaqueOwner;
            _current = new ComicSequenceOpaqueHandoffSnapshot(
                true,
                token,
                intent,
                ComicSequenceOpaqueHandoffPhase.Claimed,
                sourceSceneGeneration,
                opaqueColor,
                string.Empty);
            return true;
        }

        internal static bool TryAcknowledgeComicSequenceOpaqueRendered(
            ComicSequenceOpaqueHandoffToken token)
        {
            if (!Matches(token) ||
                _current.Phase != ComicSequenceOpaqueHandoffPhase.Claimed)
            {
                return false;
            }

            Publish(
                ComicSequenceOpaqueHandoffPhase.ComicSequenceOpaqueRendered,
                string.Empty);
            return true;
        }

        internal static bool IsExactClaimedOwner(
            ComicSequenceOpaqueHandoffToken token,
            SceneTransitionIntent expectedIntent)
        {
            return Matches(token) &&
                   _current.Intent == expectedIntent &&
                   _current.Phase == ComicSequenceOpaqueHandoffPhase.Claimed;
        }

        internal static bool TryAbortClaimedOwnerAfterSetupFailure(
            ComicSequenceOpaqueHandoffToken token,
            SceneTransitionIntent expectedIntent)
        {
            if (!IsExactClaimedOwner(token, expectedIntent))
            {
                return false;
            }

            _releaseOpaqueOwner = null;
            _current = new ComicSequenceOpaqueHandoffSnapshot(
                false,
                _current.Token,
                _current.Intent,
                ComicSequenceOpaqueHandoffPhase.Released,
                _current.SourceSceneGeneration,
                _current.OpaqueColor,
                string.Empty);
            return true;
        }

        internal static bool TryTransferToPersistentCover(
            ComicSequenceOpaqueHandoffToken token)
        {
            if (!Matches(token) ||
                _current.Phase !=
                ComicSequenceOpaqueHandoffPhase.ComicSequenceOpaqueRendered ||
                _releaseOpaqueOwner == null)
            {
                return false;
            }

            Publish(
                ComicSequenceOpaqueHandoffPhase.PersistentCoverRendered,
                string.Empty);
            try
            {
                _releaseOpaqueOwner.Invoke();
            }
            catch (Exception exception)
            {
                Publish(
                    ComicSequenceOpaqueHandoffPhase.FailedHoldingOpaque,
                    exception.Message);
                return false;
            }

            _releaseOpaqueOwner = null;
            _current = new ComicSequenceOpaqueHandoffSnapshot(
                false,
                _current.Token,
                _current.Intent,
                ComicSequenceOpaqueHandoffPhase.Released,
                _current.SourceSceneGeneration,
                _current.OpaqueColor,
                string.Empty);
            return true;
        }

        internal static bool TryReleaseCancelledOpaqueOwner(
            ComicSequenceOpaqueHandoffToken token,
            SceneTransitionIntent expectedIntent)
        {
            if (!Matches(token) ||
                _current.Intent != expectedIntent ||
                expectedIntent != SceneTransitionIntent.ComicIntroToGameplay ||
                (_current.Phase != ComicSequenceOpaqueHandoffPhase.Claimed &&
                 _current.Phase !=
                 ComicSequenceOpaqueHandoffPhase.ComicSequenceOpaqueRendered) ||
                _releaseOpaqueOwner == null)
            {
                return false;
            }

            try
            {
                _releaseOpaqueOwner.Invoke();
            }
            catch (Exception exception)
            {
                Publish(
                    ComicSequenceOpaqueHandoffPhase.FailedHoldingOpaque,
                    exception.Message);
                return false;
            }

            _releaseOpaqueOwner = null;
            _current = new ComicSequenceOpaqueHandoffSnapshot(
                false,
                _current.Token,
                _current.Intent,
                ComicSequenceOpaqueHandoffPhase.Released,
                _current.SourceSceneGeneration,
                _current.OpaqueColor,
                string.Empty);
            return true;
        }

        internal static bool TryReleaseAbandonedOwner(
            ComicSequenceOpaqueHandoffToken token)
        {
            if (!Matches(token) ||
                (_current.Phase != ComicSequenceOpaqueHandoffPhase.Claimed &&
                 _current.Phase !=
                 ComicSequenceOpaqueHandoffPhase.ComicSequenceOpaqueRendered))
            {
                return false;
            }

            _releaseOpaqueOwner = null;
            _current = new ComicSequenceOpaqueHandoffSnapshot(
                false,
                _current.Token,
                _current.Intent,
                ComicSequenceOpaqueHandoffPhase.Released,
                _current.SourceSceneGeneration,
                _current.OpaqueColor,
                string.Empty);
            return true;
        }

        internal static bool TryFailHoldingOpaque(
            ComicSequenceOpaqueHandoffToken token,
            string failureReason)
        {
            if (!Matches(token))
            {
                return false;
            }

            Publish(
                ComicSequenceOpaqueHandoffPhase.FailedHoldingOpaque,
                string.IsNullOrWhiteSpace(failureReason)
                    ? "Comic sequence opaque ownership transfer failed."
                    : failureReason);
            return true;
        }

        internal static void ResetForTests()
        {
            _current = default;
            _releaseOpaqueOwner = null;
            _nextToken = 0;
        }

        private static bool Matches(ComicSequenceOpaqueHandoffToken token)
        {
            return _current.IsActive &&
                   token.IsValid &&
                   _current.Token == token;
        }

        private static void Publish(
            ComicSequenceOpaqueHandoffPhase phase,
            string failureReason)
        {
            _current = new ComicSequenceOpaqueHandoffSnapshot(
                true,
                _current.Token,
                _current.Intent,
                phase,
                _current.SourceSceneGeneration,
                _current.OpaqueColor,
                failureReason);
        }

        private static bool IsFinite(Color color)
        {
            return !float.IsNaN(color.r) && !float.IsInfinity(color.r) &&
                   !float.IsNaN(color.g) && !float.IsInfinity(color.g) &&
                   !float.IsNaN(color.b) && !float.IsInfinity(color.b) &&
                   !float.IsNaN(color.a) && !float.IsInfinity(color.a);
        }
    }
}
