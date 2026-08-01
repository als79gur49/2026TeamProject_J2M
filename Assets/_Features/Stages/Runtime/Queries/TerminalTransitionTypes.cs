using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Game.Feature.Stages
{
    public enum TerminalTraceEvent
    {
        VictoryClaimAccepted = 0,
        IrisStarted = 1,
        BlackReached = 2,
        StageClearGateReleased = 3,
        StageClearedPublished = 4,
        DestinationMutationAdmitted = 5,
        StageResultCreated = 6,
        PayloadBound = 7,
        DestinationReadyEmitted = 8,
        DestinationReadyAccepted = 9,
        DestinationReadyRejected = 10,
        RevealRequested = 11,
        RevealAccepted = 12,
        RevealRejected = 13,
        RevealStarted = 14,
        RevealCompleted = 15,
        OverlayHidden = 16,
        SessionCompleted = 17,
        ResultBackdropReady = 18,
        ResultBackdropHandoff = 19,
        ResultContentEntranceStarted = 20,
        ResultInteractionReady = 21,
    }

    public readonly struct TerminalTraceRecord
    {
        public TerminalTraceRecord(
            TerminalSessionSnapshot session,
            TerminalTraceEvent traceEvent,
            bool accepted,
            string reason)
        {
            Token = session.Token;
            TransitionId = session.TransitionId;
            TerminalKind = session.TerminalKind;
            DestinationKind = session.DestinationKind;
            SourceSceneGeneration = session.SourceSceneGeneration;
            DestinationSceneGeneration = session.DestinationSceneGeneration;
            AuthorityPhase = session.Phase;
            Event = traceEvent;
            Accepted = accepted;
            Reason = reason ?? string.Empty;
            UnscaledTime = Time.unscaledTime;
        }

        public TerminalSessionToken Token { get; }

        public long TransitionId { get; }

        public TerminalTransitionKind TerminalKind { get; }

        public TerminalDestinationKind DestinationKind { get; }

        public long SourceSceneGeneration { get; }

        public long DestinationSceneGeneration { get; }

        public TerminalSessionPhase AuthorityPhase { get; }

        public TerminalTraceEvent Event { get; }

        public bool Accepted { get; }

        public string Reason { get; }

        public float UnscaledTime { get; }
    }

    public static class TerminalRuntimeTrace
    {
        private const int Capacity = 256;
        private static readonly List<TerminalTraceRecord> Records = new(Capacity);

        public static IReadOnlyList<TerminalTraceRecord> Snapshot => Records.ToArray();

        public static void Record(
            TerminalSessionSnapshot session,
            TerminalTraceEvent traceEvent,
            bool accepted = true,
            string reason = "")
        {
            if (Records.Count == Capacity)
            {
                Records.RemoveAt(0);
            }

            var record = new TerminalTraceRecord(session, traceEvent, accepted, reason);
            Records.Add(record);
            Debug.Log(
                "TerminalTrace " +
                $"token={record.Token} transitionId={record.TransitionId} " +
                $"terminalKind={record.TerminalKind} destinationKind={record.DestinationKind} " +
                $"sourceGeneration={record.SourceSceneGeneration} " +
                $"destinationGeneration={record.DestinationSceneGeneration} " +
                $"authorityPhase={record.AuthorityPhase} event={record.Event} " +
                $"accepted={record.Accepted} reason={record.Reason} " +
                $"unscaledTime={record.UnscaledTime:0.000}");
        }

        internal static void Reset()
        {
            Records.Clear();
        }
    }

    public readonly struct TerminalSessionToken : IEquatable<TerminalSessionToken>
    {
        public TerminalSessionToken(long authorityGeneration, long sequence)
        {
            if (authorityGeneration <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(authorityGeneration));
            }

            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            AuthorityGeneration = authorityGeneration;
            Sequence = sequence;
        }

        public long AuthorityGeneration { get; }

        public long Sequence { get; }

        public bool IsValid => AuthorityGeneration > 0 && Sequence > 0;

        public bool Equals(TerminalSessionToken other)
        {
            return AuthorityGeneration == other.AuthorityGeneration &&
                   Sequence == other.Sequence;
        }

        public override bool Equals(object obj)
        {
            return obj is TerminalSessionToken other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(AuthorityGeneration, Sequence);
        }

        public override string ToString()
        {
            return IsValid ? $"{AuthorityGeneration}:{Sequence}" : "none";
        }

        public static bool operator ==(TerminalSessionToken left, TerminalSessionToken right) =>
            left.Equals(right);

        public static bool operator !=(TerminalSessionToken left, TerminalSessionToken right) =>
            !left.Equals(right);
    }

    public enum TerminalClaimRejectionReason
    {
        None = 0,
        LowerPrioritySameTick = 1,
        TerminalAlreadyClaimed = 2,
        InvalidRequest = 3,
        SessionAlreadyActive = 4,
    }

    public readonly struct TerminalClaimResult
    {
        private TerminalClaimResult(
            bool accepted,
            TerminalSessionToken token,
            TerminalTransitionKind terminalKind,
            TerminalClaimRejectionReason rejectionReason)
        {
            Accepted = accepted;
            Rejected = !accepted;
            Token = token;
            TerminalKind = terminalKind;
            RejectionReason = rejectionReason;
        }

        public bool Accepted { get; }

        public bool Rejected { get; }

        public TerminalSessionToken Token { get; }

        // Compatibility-only diagnostic projection. Runtime correlation must use Token.
        public long ClaimId => Token.Sequence;

        public TerminalTransitionKind TerminalKind { get; }

        public TerminalClaimRejectionReason RejectionReason { get; }

        public static TerminalClaimResult Accept(
            TerminalSessionToken token,
            TerminalTransitionKind terminalKind)
        {
            if (!token.IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(token));
            }

            return new TerminalClaimResult(
                accepted: true,
                token,
                terminalKind,
                TerminalClaimRejectionReason.None);
        }

        public static TerminalClaimResult Accept(long claimId, TerminalTransitionKind terminalKind)
        {
            return Accept(
                new TerminalSessionToken(
                    TerminalSessionRegistry.Authority.AuthorityGeneration,
                    claimId),
                terminalKind);
        }

        public static TerminalClaimResult Reject(
            TerminalTransitionKind terminalKind,
            TerminalClaimRejectionReason rejectionReason,
            TerminalSessionToken token = default)
        {
            if (rejectionReason == TerminalClaimRejectionReason.None)
            {
                throw new ArgumentOutOfRangeException(nameof(rejectionReason));
            }

            return new TerminalClaimResult(
                accepted: false,
                token,
                terminalKind,
                rejectionReason);
        }

        public static TerminalClaimResult Reject(
            TerminalTransitionKind terminalKind,
            TerminalClaimRejectionReason rejectionReason,
            long claimId)
        {
            return Reject(
                terminalKind,
                rejectionReason,
                claimId > 0
                    ? new TerminalSessionToken(
                        TerminalSessionRegistry.Authority.AuthorityGeneration,
                        claimId)
                    : default);
        }
    }

    public enum TerminalTransitionKind
    {
        Victory = 0,
        Defeat = 1,
    }

    public enum TerminalTransitionState
    {
        Hidden = 0,
        Focusing = 1,
        Holding = 2,
        Closing = 3,
        Black = 4,
        Revealing = 5,
        Completed = 6,
        Cancelled = 7,
        Disposed = 8,
    }

    public enum TerminalTransitionDestinationMode
    {
        SameScene = 0,
        SceneHandoff = 1,
    }

    public enum TerminalSessionPhase
    {
        Inactive = 0,
        Claimed = 1,
        Iris = 2,
        Black = 3,
        OpaqueHandoff = 4,
        Presenting = 5,
        Loading = 6,
        ReadyToActivate = 7,
        Activating = 8,
        WaitingDestinationReady = 9,
        WaitingSameSceneDestination = 10,
        Revealing = 11,
        ResultBackdropHandoff = 12,
        WaitingResultInteraction = 13,
        Completed = 14,
        FailedHoldingCover = 15,
    }

    public enum TerminalDestinationKind
    {
        None = 0,
        SameSceneStageResult = 1,
        SameSceneGameClear = 2,
        SameSceneLevelFailed = 3,
        ReloadedGameplay = 4,
        MainMenu = 5,
    }

    public enum TerminalDestinationProvenance
    {
        None = 0,
        SameSceneStageResult = 1,
        SameSceneGameClear = 2,
        SameSceneLevelFailed = 3,
        ReloadedGameplayBootstrap = 4,
        MainMenuBootstrap = 5,
    }

    public enum DestinationReadinessOutcome
    {
        Ready = 0,
        Failed = 1,
        Cancelled = 2,
    }

    public readonly struct TerminalFailure
    {
        public TerminalFailure(string code, string message)
        {
            Code = string.IsNullOrWhiteSpace(code) ? "TerminalFailure" : code;
            Message = string.IsNullOrWhiteSpace(message)
                ? "Terminal transition failed while holding its persistent authored cover."
                : message;
        }

        public string Code { get; }

        public string Message { get; }
    }

    public readonly struct TerminalClaimRequest
    {
        public TerminalClaimRequest(
            TerminalTransitionKind terminalKind,
            long sourceSceneGeneration,
            TerminalDestinationKind destinationKind)
        {
            if (sourceSceneGeneration <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceSceneGeneration));
            }

            if (destinationKind == TerminalDestinationKind.None)
            {
                throw new ArgumentOutOfRangeException(nameof(destinationKind));
            }

            TerminalKind = terminalKind;
            SourceSceneGeneration = sourceSceneGeneration;
            DestinationKind = destinationKind;
        }

        public TerminalTransitionKind TerminalKind { get; }

        public long SourceSceneGeneration { get; }

        public TerminalDestinationKind DestinationKind { get; }
    }

    public readonly struct DestinationReadinessSignal
    {
        public DestinationReadinessSignal(
            TerminalSessionToken token,
            long transitionId,
            long sourceSceneGeneration,
            long destinationSceneGeneration,
            TerminalDestinationKind destinationKind,
            TerminalSessionPhase expectedPhase,
            TerminalDestinationProvenance provenance,
            DestinationReadinessOutcome outcome,
            string failureReason = null)
        {
            Token = token;
            TransitionId = transitionId;
            SourceSceneGeneration = sourceSceneGeneration;
            DestinationSceneGeneration = destinationSceneGeneration;
            DestinationKind = destinationKind;
            ExpectedPhase = expectedPhase;
            Provenance = provenance;
            Outcome = outcome;
            FailureReason = failureReason ?? string.Empty;
        }

        public TerminalSessionToken Token { get; }

        public long TransitionId { get; }

        public long SourceSceneGeneration { get; }

        public long DestinationSceneGeneration { get; }

        public TerminalDestinationKind DestinationKind { get; }

        public TerminalSessionPhase ExpectedPhase { get; }

        public TerminalDestinationProvenance Provenance { get; }

        public DestinationReadinessOutcome Outcome { get; }

        public string FailureReason { get; }
    }

    public readonly struct TerminalSessionSnapshot
    {
        public TerminalSessionSnapshot(
            bool isActive,
            TerminalSessionToken token,
            TerminalTransitionKind terminalKind,
            TerminalSessionPhase phase,
            string failureReason,
            long transitionId,
            long sourceSceneGeneration,
            long destinationSceneGeneration,
            TerminalDestinationKind destinationKind)
        {
            IsActive = isActive;
            Token = token;
            TerminalKind = terminalKind;
            Phase = phase;
            FailureReason = failureReason ?? string.Empty;
            TransitionId = transitionId;
            SourceSceneGeneration = sourceSceneGeneration;
            DestinationSceneGeneration = destinationSceneGeneration;
            DestinationKind = destinationKind;
        }

        public bool IsActive { get; }

        public TerminalSessionToken Token { get; }

        // Compatibility-only diagnostic projection. Runtime correlation must use Token.
        public long ClaimId => Token.Sequence;

        public TerminalTransitionKind TerminalKind { get; }

        public TerminalSessionPhase Phase { get; }

        public string FailureReason { get; }

        public long TransitionId { get; }

        public long SourceSceneGeneration { get; }

        public long DestinationSceneGeneration { get; }

        public TerminalDestinationKind DestinationKind { get; }
    }

    public interface ITerminalSessionReadModel
    {
        event Action<TerminalSessionSnapshot> Changed;

        bool IsActive { get; }

        TerminalSessionToken ActiveToken { get; }

        TerminalSessionPhase Phase { get; }

        TerminalSessionSnapshot Current { get; }

        bool CanAcceptDestinationEvent(DestinationReadinessSignal signal);
    }

    public interface ITerminalSessionAuthority : ITerminalSessionReadModel
    {
        TerminalClaimResult TryClaim(TerminalClaimRequest request);

        bool TryAdvancePhase(TerminalSessionToken token, TerminalSessionPhase next);

        bool TryFail(TerminalSessionToken token, TerminalFailure failure);
    }

    public interface ITerminalSessionAuthorityProvider
    {
        bool TryGetTerminalSessionAuthority(
            out ITerminalSessionReadModel readModel,
            out ITerminalSessionAuthority authority);
    }

    public sealed class PersistentTerminalSessionAuthority : ITerminalSessionAuthority
    {
        private static long _nextAuthorityGeneration;
        private long _currentSceneGeneration;
        private int _currentSceneHandle = int.MinValue;
        private long _nextSceneGeneration;
        private long _nextSequence;
        private TerminalSessionSnapshot _current;

        public PersistentTerminalSessionAuthority(long authorityGeneration = 0)
        {
            AuthorityGeneration = authorityGeneration > 0
                ? authorityGeneration
                : Interlocked.Increment(ref _nextAuthorityGeneration);
        }

        public event Action<TerminalSessionSnapshot> Changed;

        public long AuthorityGeneration { get; }

        public bool IsActive => _current.IsActive;

        public TerminalSessionToken ActiveToken => _current.Token;

        public TerminalSessionPhase Phase => _current.Phase;

        public TerminalSessionSnapshot Current => _current;

        public long CurrentSceneGeneration => _currentSceneGeneration;

        public long RegisterSceneBootstrap(int sceneHandle, string sceneName)
        {
            if (_currentSceneGeneration > 0 && _currentSceneHandle == sceneHandle)
            {
                return _currentSceneGeneration;
            }

            _currentSceneHandle = sceneHandle;
            _currentSceneGeneration = ++_nextSceneGeneration;
            if (_current.IsActive &&
                _current.SourceSceneGeneration != _currentSceneGeneration &&
                _current.DestinationSceneGeneration == 0)
            {
                Publish(
                    isActive: true,
                    _current.Token,
                    _current.TerminalKind,
                    _current.Phase,
                    _current.FailureReason,
                    _current.TransitionId,
                    _current.SourceSceneGeneration,
                    _currentSceneGeneration,
                    _current.DestinationKind);
            }

            return _currentSceneGeneration;
        }

        public TerminalClaimResult TryClaim(TerminalClaimRequest request)
        {
            if (_current.IsActive)
            {
                return TerminalClaimResult.Reject(
                    request.TerminalKind,
                    TerminalClaimRejectionReason.SessionAlreadyActive,
                    _current.Token);
            }

            if (_currentSceneGeneration <= 0 ||
                request.SourceSceneGeneration != _currentSceneGeneration)
            {
                return TerminalClaimResult.Reject(
                    request.TerminalKind,
                    TerminalClaimRejectionReason.InvalidRequest);
            }

            var token = new TerminalSessionToken(AuthorityGeneration, ++_nextSequence);
            Publish(
                isActive: true,
                token,
                request.TerminalKind,
                TerminalSessionPhase.Claimed,
                string.Empty,
                transitionId: 0,
                request.SourceSceneGeneration,
                destinationSceneGeneration: 0,
                request.DestinationKind);
            if (request.TerminalKind == TerminalTransitionKind.Victory)
            {
                TerminalRuntimeTrace.Record(_current, TerminalTraceEvent.VictoryClaimAccepted);
            }

            return TerminalClaimResult.Accept(token, request.TerminalKind);
        }

        public bool TryAdvancePhase(TerminalSessionToken token, TerminalSessionPhase next)
        {
            if (!_current.IsActive ||
                _current.Token != token ||
                next == TerminalSessionPhase.Inactive ||
                next == TerminalSessionPhase.Completed ||
                next == TerminalSessionPhase.FailedHoldingCover ||
                next <= _current.Phase)
            {
                return false;
            }

            Publish(
                isActive: true,
                token,
                _current.TerminalKind,
                next,
                string.Empty,
                _current.TransitionId,
                _current.SourceSceneGeneration,
                _current.DestinationSceneGeneration,
                _current.DestinationKind);
            if (next == TerminalSessionPhase.Iris)
            {
                TerminalRuntimeTrace.Record(_current, TerminalTraceEvent.IrisStarted);
            }
            else if (next == TerminalSessionPhase.Black)
            {
                TerminalRuntimeTrace.Record(_current, TerminalTraceEvent.BlackReached);
            }
            else if (next == TerminalSessionPhase.Revealing)
            {
                TerminalRuntimeTrace.Record(_current, TerminalTraceEvent.RevealStarted);
            }

            return true;
        }

        public bool TryBindTransition(
            TerminalSessionToken token,
            long transitionId,
            TerminalDestinationKind destinationKind)
        {
            if (!_current.IsActive ||
                _current.Token != token ||
                transitionId <= 0 ||
                _current.TransitionId != 0 ||
                destinationKind == TerminalDestinationKind.None)
            {
                return false;
            }

            Publish(
                isActive: true,
                token,
                _current.TerminalKind,
                _current.Phase,
                string.Empty,
                transitionId,
                _current.SourceSceneGeneration,
                _current.DestinationSceneGeneration,
                destinationKind);
            return true;
        }

        public bool TrySetDestinationKind(
            TerminalSessionToken token,
            TerminalDestinationKind destinationKind)
        {
            if (!_current.IsActive ||
                _current.Token != token ||
                destinationKind == TerminalDestinationKind.None ||
                _current.TransitionId != 0 ||
                _current.Phase >= TerminalSessionPhase.WaitingDestinationReady)
            {
                return false;
            }

            Publish(
                isActive: true,
                token,
                _current.TerminalKind,
                _current.Phase,
                string.Empty,
                _current.TransitionId,
                _current.SourceSceneGeneration,
                _current.DestinationSceneGeneration,
                destinationKind);
            return true;
        }

        public bool TryFail(TerminalSessionToken token, TerminalFailure failure)
        {
            if (!_current.IsActive || _current.Token != token)
            {
                return false;
            }

            Publish(
                isActive: true,
                token,
                _current.TerminalKind,
                TerminalSessionPhase.FailedHoldingCover,
                failure.Message,
                _current.TransitionId,
                _current.SourceSceneGeneration,
                _current.DestinationSceneGeneration,
                _current.DestinationKind);
            return true;
        }

        public bool TryComplete(TerminalSessionToken token)
        {
            if (!_current.IsActive ||
                _current.Token != token ||
                (_current.Phase != TerminalSessionPhase.Revealing &&
                 _current.Phase != TerminalSessionPhase.WaitingResultInteraction))
            {
                return false;
            }

            Publish(
                isActive: false,
                token,
                _current.TerminalKind,
                TerminalSessionPhase.Completed,
                string.Empty,
                _current.TransitionId,
                _current.SourceSceneGeneration,
                _current.DestinationSceneGeneration,
                _current.DestinationKind);
            TerminalRuntimeTrace.Record(_current, TerminalTraceEvent.SessionCompleted);
            return true;
        }

        public bool CanAcceptDestinationEvent(DestinationReadinessSignal signal)
        {
            if (!_current.IsActive ||
                signal.Token != _current.Token ||
                signal.ExpectedPhase != _current.Phase ||
                signal.SourceSceneGeneration != _current.SourceSceneGeneration ||
                signal.DestinationKind != _current.DestinationKind ||
                signal.Provenance == TerminalDestinationProvenance.None)
            {
                return false;
            }

            if (_current.Phase == TerminalSessionPhase.WaitingSameSceneDestination)
            {
                return signal.TransitionId == 0 &&
                       signal.DestinationSceneGeneration == _current.SourceSceneGeneration &&
                       IsSameSceneProvenance(signal.DestinationKind, signal.Provenance);
            }

            if (_current.Phase != TerminalSessionPhase.WaitingDestinationReady)
            {
                return false;
            }

            return _current.TransitionId > 0 &&
                   signal.TransitionId == _current.TransitionId &&
                   _current.DestinationSceneGeneration > 0 &&
                   signal.DestinationSceneGeneration == _current.DestinationSceneGeneration &&
                   IsSceneHandoffProvenance(signal.DestinationKind, signal.Provenance);
        }

        internal void Reset()
        {
            _current = default;
            _currentSceneGeneration = 0;
            _currentSceneHandle = int.MinValue;
            _nextSceneGeneration = 0;
            _nextSequence = 0;
            Changed = null;
        }

        private static bool IsSameSceneProvenance(
            TerminalDestinationKind destinationKind,
            TerminalDestinationProvenance provenance)
        {
            return (destinationKind == TerminalDestinationKind.SameSceneStageResult &&
                    provenance == TerminalDestinationProvenance.SameSceneStageResult) ||
                   (destinationKind == TerminalDestinationKind.SameSceneGameClear &&
                    provenance == TerminalDestinationProvenance.SameSceneGameClear) ||
                   (destinationKind == TerminalDestinationKind.SameSceneLevelFailed &&
                    provenance == TerminalDestinationProvenance.SameSceneLevelFailed);
        }

        private static bool IsSceneHandoffProvenance(
            TerminalDestinationKind destinationKind,
            TerminalDestinationProvenance provenance)
        {
            return (destinationKind == TerminalDestinationKind.ReloadedGameplay &&
                    provenance == TerminalDestinationProvenance.ReloadedGameplayBootstrap) ||
                   (destinationKind == TerminalDestinationKind.MainMenu &&
                    provenance == TerminalDestinationProvenance.MainMenuBootstrap);
        }

        private void Publish(
            bool isActive,
            TerminalSessionToken token,
            TerminalTransitionKind terminalKind,
            TerminalSessionPhase phase,
            string failureReason,
            long transitionId,
            long sourceSceneGeneration,
            long destinationSceneGeneration,
            TerminalDestinationKind destinationKind)
        {
            _current = new TerminalSessionSnapshot(
                isActive,
                token,
                terminalKind,
                phase,
                failureReason,
                transitionId,
                sourceSceneGeneration,
                destinationSceneGeneration,
                destinationKind);
            Changed?.Invoke(_current);
        }
    }

    public static class TerminalSessionRegistry
    {
        private static PersistentTerminalSessionAuthority _authority = new();

        public static event Action<TerminalSessionSnapshot> Changed
        {
            add => _authority.Changed += value;
            remove => _authority.Changed -= value;
        }

        public static PersistentTerminalSessionAuthority Authority => _authority;

        public static ITerminalSessionReadModel ReadModel => _authority;

        public static TerminalSessionSnapshot Current => _authority.Current;

        public static bool IsActive => _authority.IsActive;

        public static bool TryAdvance(
            TerminalSessionToken token,
            TerminalSessionPhase phase)
        {
            return _authority.TryAdvancePhase(token, phase);
        }

        public static bool TryFailHoldingCover(
            TerminalSessionToken token,
            string failureReason)
        {
            return _authority.TryFail(
                token,
                new TerminalFailure("TerminalTransitionFailure", failureReason));
        }

        public static bool TryComplete(TerminalSessionToken token)
        {
            return _authority.TryComplete(token);
        }

        internal static void ResetForTests()
        {
            _authority.Reset();
            TerminalRuntimeTrace.Reset();
        }
    }

    public static class TerminalDestinationReadiness
    {
        private static DestinationReadinessSignal? _readySignal;

        public static event Action<DestinationReadinessSignal> DestinationReady;

        public static bool IsReady(TerminalSessionToken token)
        {
            var authority = TerminalSessionRegistry.Authority;
            return token.IsValid &&
                   authority.IsActive &&
                   authority.ActiveToken == token &&
                   _readySignal.HasValue &&
                   _readySignal.Value.Token == token;
        }

        public static bool Signal(DestinationReadinessSignal signal)
        {
            var authority = TerminalSessionRegistry.Authority;
            TerminalRuntimeTrace.Record(
                authority.Current,
                TerminalTraceEvent.DestinationReadyEmitted);
            if (!authority.CanAcceptDestinationEvent(signal))
            {
                TerminalRuntimeTrace.Record(
                    authority.Current,
                    TerminalTraceEvent.DestinationReadyRejected,
                    accepted: false,
                    DescribeRejection(authority.Current, signal));
                return false;
            }

            if (_readySignal.HasValue && _readySignal.Value.Token == signal.Token)
            {
                TerminalRuntimeTrace.Record(
                    authority.Current,
                    TerminalTraceEvent.DestinationReadyRejected,
                    accepted: false,
                    "DESTINATION_READY_DUPLICATE");
                return false;
            }

            if (signal.Outcome != DestinationReadinessOutcome.Ready)
            {
                return authority.TryFail(
                    signal.Token,
                    new TerminalFailure(
                        signal.Outcome.ToString(),
                        signal.FailureReason));
            }

            _readySignal = signal;
            TerminalRuntimeTrace.Record(
                authority.Current,
                TerminalTraceEvent.DestinationReadyAccepted);
            DestinationReady?.Invoke(signal);
            return true;
        }

        private static string DescribeRejection(
            TerminalSessionSnapshot session,
            DestinationReadinessSignal signal)
        {
            if (!session.IsActive || signal.Token != session.Token)
            {
                return "DESTINATION_READY_REJECTED_TOKEN";
            }

            if (signal.ExpectedPhase != session.Phase)
            {
                return "DESTINATION_READY_REJECTED_PHASE";
            }

            if (signal.SourceSceneGeneration != session.SourceSceneGeneration ||
                (session.Phase == TerminalSessionPhase.WaitingSameSceneDestination &&
                 signal.DestinationSceneGeneration != session.SourceSceneGeneration) ||
                (session.Phase == TerminalSessionPhase.WaitingDestinationReady &&
                 signal.DestinationSceneGeneration != session.DestinationSceneGeneration))
            {
                return "DESTINATION_READY_REJECTED_GENERATION";
            }

            if (signal.DestinationKind != session.DestinationKind)
            {
                return "DESTINATION_READY_REJECTED_KIND";
            }

            if (signal.Provenance == TerminalDestinationProvenance.None)
            {
                return "DESTINATION_READY_REJECTED_PROVENANCE";
            }

            if (session.Phase == TerminalSessionPhase.WaitingDestinationReady &&
                signal.TransitionId != session.TransitionId)
            {
                return "DESTINATION_READY_REJECTED_TRANSITION_ID";
            }

            return "DESTINATION_READY_REJECTED_PROVENANCE";
        }

        internal static void ResetForTests()
        {
            _readySignal = null;
            DestinationReady = null;
        }
    }

    public readonly struct TerminalTransitionRequest
    {
        public TerminalTransitionRequest(
            TerminalTransitionKind kind,
            int focusEntityId,
            TerminalSessionToken token,
            TerminalTransitionDestinationMode destinationMode)
        {
            if (focusEntityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(focusEntityId));
            }

            if (!token.IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(token));
            }

            Kind = kind;
            FocusEntityId = focusEntityId;
            Token = token;
            DestinationMode = destinationMode;
        }

        public TerminalTransitionRequest(
            TerminalTransitionKind kind,
            int focusEntityId,
            long claimId,
            TerminalTransitionDestinationMode destinationMode)
            : this(
                kind,
                focusEntityId,
                new TerminalSessionToken(
                    TerminalSessionRegistry.Authority.AuthorityGeneration,
                    claimId),
                destinationMode)
        {
        }

        public TerminalTransitionKind Kind { get; }

        public int FocusEntityId { get; }

        public TerminalSessionToken Token { get; }

        public long ClaimId => Token.Sequence;

        public TerminalTransitionDestinationMode DestinationMode { get; }
    }

    public readonly struct TerminalFocusTarget
    {
        public TerminalFocusTarget(Vector2 normalizedCenter, float normalizedRadius, bool isFallback)
        {
            NormalizedCenter = new Vector2(
                Mathf.Clamp01(normalizedCenter.x),
                Mathf.Clamp01(normalizedCenter.y));
            NormalizedRadius = Mathf.Max(0f, normalizedRadius);
            IsFallback = isFallback;
        }

        public Vector2 NormalizedCenter { get; }

        public float NormalizedRadius { get; }

        public bool IsFallback { get; }
    }

    public interface ITerminalFocusTargetSource
    {
        bool TryCapture(int entityId, out TerminalFocusTarget target);
    }

    public interface ITerminalTransitionPort
    {
        bool TryBegin(
            TerminalTransitionRequest request,
            out TerminalTransitionPlayback playback);
    }

    public interface ITerminalTransitionPortProvider
    {
        bool TryGetTerminalTransitionPort(out ITerminalTransitionPort port);
    }

    public enum TerminalIrisEasing
    {
        Linear = 0,
        SmoothStep = 1,
        EaseOutSine = 2,
        EaseInCubic = 3,
    }

    public readonly struct TerminalIrisRuntimeEdgeSettings
    {
        public TerminalIrisRuntimeEdgeSettings(
            float edgeAntiAliasScale,
            float minimumAAPixels,
            float artisticFeatherHalfWidthPixels,
            float rimWidthPixels,
            float rimSoftnessPixels,
            float rimFadeOutPixels,
            Color rimColor)
        {
            RequireFinitePositive(edgeAntiAliasScale, nameof(edgeAntiAliasScale));
            RequireFinitePositive(minimumAAPixels, nameof(minimumAAPixels));
            RequireFiniteNonNegative(
                artisticFeatherHalfWidthPixels,
                nameof(artisticFeatherHalfWidthPixels));
            RequireFiniteNonNegative(rimWidthPixels, nameof(rimWidthPixels));
            RequireFiniteNonNegative(rimSoftnessPixels, nameof(rimSoftnessPixels));
            RequireFiniteNonNegative(rimFadeOutPixels, nameof(rimFadeOutPixels));
            if (float.IsNaN(rimColor.a) ||
                float.IsInfinity(rimColor.a) ||
                rimColor.a < 0f ||
                rimColor.a > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rimColor),
                    rimColor,
                    "Terminal Iris Rim alpha must be finite and in [0, 1].");
            }

            EdgeAntiAliasScale = edgeAntiAliasScale;
            MinimumAAPixels = minimumAAPixels;
            ArtisticFeatherHalfWidthPixels = artisticFeatherHalfWidthPixels;
            RimWidthPixels = rimWidthPixels;
            RimSoftnessPixels = rimSoftnessPixels;
            RimFadeOutPixels = rimFadeOutPixels;
            RimColor = rimColor;
        }

        public float EdgeAntiAliasScale { get; }

        public float MinimumAAPixels { get; }

        public float ArtisticFeatherHalfWidthPixels { get; }

        public float RimWidthPixels { get; }

        public float RimSoftnessPixels { get; }

        public float RimFadeOutPixels { get; }

        public Color RimColor { get; }

        public float RequiredClosedOvershootPixels =>
            MinimumAAPixels +
            ArtisticFeatherHalfWidthPixels +
            0.5f * RimWidthPixels +
            RimSoftnessPixels +
            1f;

        internal void ValidateOrThrow(string parameterName)
        {
            RequireFinitePositive(
                EdgeAntiAliasScale,
                $"{parameterName}.{nameof(EdgeAntiAliasScale)}");
            RequireFinitePositive(
                MinimumAAPixels,
                $"{parameterName}.{nameof(MinimumAAPixels)}");
            RequireFiniteNonNegative(
                ArtisticFeatherHalfWidthPixels,
                $"{parameterName}.{nameof(ArtisticFeatherHalfWidthPixels)}");
            RequireFiniteNonNegative(
                RimWidthPixels,
                $"{parameterName}.{nameof(RimWidthPixels)}");
            RequireFiniteNonNegative(
                RimSoftnessPixels,
                $"{parameterName}.{nameof(RimSoftnessPixels)}");
            RequireFiniteNonNegative(
                RimFadeOutPixels,
                $"{parameterName}.{nameof(RimFadeOutPixels)}");
            if (float.IsNaN(RimColor.a) ||
                float.IsInfinity(RimColor.a) ||
                RimColor.a < 0f ||
                RimColor.a > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    $"{parameterName}.{nameof(RimColor)}",
                    RimColor,
                    "Terminal Iris Rim alpha must be finite and in [0, 1].");
            }
        }

        private static void RequireFinitePositive(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Terminal Iris AA values must be finite and greater than zero.");
            }
        }

        private static void RequireFiniteNonNegative(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Terminal Iris edge values must be finite and non-negative.");
            }
        }
    }

    public readonly struct TerminalIrisRuntimeOpenPreset
    {
        public TerminalIrisRuntimeOpenPreset(
            float preOpenHoldDuration,
            float openingDuration,
            float fullOpenMargin,
            float finalClosedOvershootPixels,
            TerminalIrisRuntimeEdgeSettings edge,
            TerminalIrisEasing openingEasing)
        {
            RequireFiniteNonNegative(preOpenHoldDuration, nameof(preOpenHoldDuration));
            RequireFinitePositive(openingDuration, nameof(openingDuration));
            RequireFiniteNonNegative(fullOpenMargin, nameof(fullOpenMargin));
            RequireFinitePositive(
                finalClosedOvershootPixels,
                nameof(finalClosedOvershootPixels));
            edge.ValidateOrThrow(nameof(edge));
            if (finalClosedOvershootPixels < edge.RequiredClosedOvershootPixels)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(finalClosedOvershootPixels),
                    finalClosedOvershootPixels,
                    "Terminal Iris closed overshoot must exceed AA, Feather, Rim support, and the opaque safety pixel.");
            }

            RequireValidEasing(openingEasing, nameof(openingEasing));

            PreOpenHoldDuration = preOpenHoldDuration;
            OpeningDuration = openingDuration;
            FullOpenMargin = fullOpenMargin;
            FinalClosedOvershootPixels = finalClosedOvershootPixels;
            Edge = edge;
            OpeningEasing = openingEasing;
        }

        public float PreOpenHoldDuration { get; }

        public float OpeningDuration { get; }

        public float FullOpenMargin { get; }

        public float FinalClosedOvershootPixels { get; }

        public TerminalIrisRuntimeEdgeSettings Edge { get; }

        public TerminalIrisEasing OpeningEasing { get; }

        public float TotalDuration => PreOpenHoldDuration + OpeningDuration;

        internal void ValidateOrThrow(string parameterName)
        {
            RequireFiniteNonNegative(PreOpenHoldDuration, $"{parameterName}.{nameof(PreOpenHoldDuration)}");
            RequireFinitePositive(OpeningDuration, $"{parameterName}.{nameof(OpeningDuration)}");
            RequireFiniteNonNegative(FullOpenMargin, $"{parameterName}.{nameof(FullOpenMargin)}");
            RequireFinitePositive(
                FinalClosedOvershootPixels,
                $"{parameterName}.{nameof(FinalClosedOvershootPixels)}");
            Edge.ValidateOrThrow($"{parameterName}.{nameof(Edge)}");
            if (FinalClosedOvershootPixels < Edge.RequiredClosedOvershootPixels)
            {
                throw new ArgumentOutOfRangeException(
                    $"{parameterName}.{nameof(FinalClosedOvershootPixels)}",
                    FinalClosedOvershootPixels,
                    "Terminal Iris closed overshoot must exceed the complete edge support.");
            }

            RequireValidEasing(OpeningEasing, $"{parameterName}.{nameof(OpeningEasing)}");
        }

        private static void RequireFinitePositive(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Terminal Iris opening duration must be finite and greater than zero.");
            }
        }

        private static void RequireFiniteNonNegative(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Terminal Iris opening values must be finite and non-negative.");
            }
        }

        private static void RequireValidEasing(
            TerminalIrisEasing easing,
            string parameterName)
        {
            if (!Enum.IsDefined(typeof(TerminalIrisEasing), easing))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    easing,
                    "Terminal Iris easing must be a defined value.");
            }
        }
    }

    public readonly struct TerminalIrisRuntimePreset
    {
        public TerminalIrisRuntimePreset(
            float focusDuration,
            float holdDuration,
            float closeDuration,
            Vector2 fallbackCenter,
            float fallbackRadius,
            float minimumFocusRadius,
            float focusPadding,
            float finalClosedOvershootPixels,
            TerminalIrisRuntimeEdgeSettings edge,
            TerminalIrisEasing focusEasing,
            TerminalIrisEasing closeEasing,
            TerminalIrisRuntimeOpenPreset? revealPreset = null)
        {
            RequireFiniteNonNegative(focusDuration, nameof(focusDuration));
            RequireFiniteNonNegative(holdDuration, nameof(holdDuration));
            RequireFiniteNonNegative(closeDuration, nameof(closeDuration));
            var closeTotalDuration = focusDuration + holdDuration + closeDuration;
            if (float.IsInfinity(closeTotalDuration) || closeTotalDuration <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(closeDuration),
                    closeDuration,
                    "Terminal Iris close total duration must be greater than zero.");
            }

            RequireNormalizedCenter(fallbackCenter, nameof(fallbackCenter));
            RequireFiniteNonNegative(fallbackRadius, nameof(fallbackRadius));
            RequireFiniteNonNegative(minimumFocusRadius, nameof(minimumFocusRadius));
            RequireFiniteNonNegative(focusPadding, nameof(focusPadding));
            RequireFinitePositive(
                finalClosedOvershootPixels,
                nameof(finalClosedOvershootPixels));
            edge.ValidateOrThrow(nameof(edge));
            if (finalClosedOvershootPixels < edge.RequiredClosedOvershootPixels)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(finalClosedOvershootPixels),
                    finalClosedOvershootPixels,
                    "Terminal Iris closed overshoot must exceed AA, Feather, Rim support, and the opaque safety pixel.");
            }

            RequireValidEasing(focusEasing, nameof(focusEasing));
            RequireValidEasing(closeEasing, nameof(closeEasing));
            revealPreset?.ValidateOrThrow(nameof(revealPreset));

            FocusDuration = focusDuration;
            HoldDuration = holdDuration;
            CloseDuration = closeDuration;
            FallbackCenter = fallbackCenter;
            FallbackRadius = fallbackRadius;
            MinimumFocusRadius = minimumFocusRadius;
            FocusPadding = focusPadding;
            FinalClosedOvershootPixels = finalClosedOvershootPixels;
            Edge = edge;
            FocusEasing = focusEasing;
            CloseEasing = closeEasing;
            RevealPreset = revealPreset;
        }

        public float FocusDuration { get; }

        public float HoldDuration { get; }

        public float CloseDuration { get; }

        public Vector2 FallbackCenter { get; }

        public float FallbackRadius { get; }

        public float MinimumFocusRadius { get; }

        public float FocusPadding { get; }

        public float FinalClosedOvershootPixels { get; }

        public TerminalIrisRuntimeEdgeSettings Edge { get; }

        public TerminalIrisEasing FocusEasing { get; }

        public TerminalIrisEasing CloseEasing { get; }

        public TerminalIrisRuntimeOpenPreset? RevealPreset { get; }

        public float BlackAt => FocusDuration + HoldDuration + CloseDuration;

        internal void ValidateOrThrow(string parameterName)
        {
            RequireFiniteNonNegative(FocusDuration, $"{parameterName}.{nameof(FocusDuration)}");
            RequireFiniteNonNegative(HoldDuration, $"{parameterName}.{nameof(HoldDuration)}");
            RequireFiniteNonNegative(CloseDuration, $"{parameterName}.{nameof(CloseDuration)}");
            if (float.IsNaN(BlackAt) || float.IsInfinity(BlackAt) || BlackAt <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Terminal Iris close total duration must be greater than zero.");
            }

            RequireNormalizedCenter(FallbackCenter, $"{parameterName}.{nameof(FallbackCenter)}");
            RequireFiniteNonNegative(FallbackRadius, $"{parameterName}.{nameof(FallbackRadius)}");
            RequireFiniteNonNegative(
                MinimumFocusRadius,
                $"{parameterName}.{nameof(MinimumFocusRadius)}");
            RequireFiniteNonNegative(FocusPadding, $"{parameterName}.{nameof(FocusPadding)}");
            RequireFinitePositive(
                FinalClosedOvershootPixels,
                $"{parameterName}.{nameof(FinalClosedOvershootPixels)}");
            Edge.ValidateOrThrow($"{parameterName}.{nameof(Edge)}");
            if (FinalClosedOvershootPixels < Edge.RequiredClosedOvershootPixels)
            {
                throw new ArgumentOutOfRangeException(
                    $"{parameterName}.{nameof(FinalClosedOvershootPixels)}",
                    FinalClosedOvershootPixels,
                    "Terminal Iris closed overshoot must exceed the complete edge support.");
            }
            RequireValidEasing(FocusEasing, $"{parameterName}.{nameof(FocusEasing)}");
            RequireValidEasing(CloseEasing, $"{parameterName}.{nameof(CloseEasing)}");
            RevealPreset?.ValidateOrThrow($"{parameterName}.{nameof(RevealPreset)}");
        }

        private static void RequireNormalizedCenter(Vector2 value, string parameterName)
        {
            if (float.IsNaN(value.x) ||
                float.IsInfinity(value.x) ||
                float.IsNaN(value.y) ||
                float.IsInfinity(value.y) ||
                value.x < 0f ||
                value.x > 1f ||
                value.y < 0f ||
                value.y > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Terminal Iris fallback center coordinates must be in [0, 1].");
            }
        }

        private static void RequireFiniteNonNegative(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Terminal Iris close values must be finite and non-negative.");
            }
        }

        private static void RequireFinitePositive(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Terminal Iris closed overshoot must be finite and greater than zero.");
            }
        }

        private static void RequireValidEasing(
            TerminalIrisEasing easing,
            string parameterName)
        {
            if (!Enum.IsDefined(typeof(TerminalIrisEasing), easing))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    easing,
                    "Terminal Iris easing must be a defined value.");
            }
        }
    }

    public static class TerminalIrisEasingUtility
    {
        public static float Evaluate(TerminalIrisEasing easing, float progress)
        {
            var boundedProgress = Mathf.Clamp01(progress);
            return easing switch
            {
                TerminalIrisEasing.Linear => boundedProgress,
                TerminalIrisEasing.SmoothStep =>
                    boundedProgress * boundedProgress * (3f - 2f * boundedProgress),
                TerminalIrisEasing.EaseOutSine =>
                    Mathf.Sin(0.5f * Mathf.PI * boundedProgress),
                TerminalIrisEasing.EaseInCubic =>
                    boundedProgress * boundedProgress * boundedProgress,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(easing),
                    easing,
                    "Terminal Iris easing must be a defined value."),
            };
        }
    }

    public sealed class TerminalTransitionPlayback : IDisposable
    {
        private const float GuaranteedFullyRevealedRadius = 4f;
        private readonly TerminalIrisRuntimePreset _preset;
        private float _fullyRevealedRadius;
        private float _revealFullyOpenRadius;
        private float _configuredRevealFullyOpenRadius;
        private float _phaseElapsed;
        private bool _blackRaised;
        private bool _revealRaised;
        private bool _completedToResultBackdrop;
        private bool _disposed;

        public TerminalTransitionPlayback(TerminalIrisRuntimePreset preset)
        {
            preset.ValidateOrThrow(nameof(preset));
            _preset = preset;
            State = TerminalTransitionState.Hidden;
            CurrentCenter = preset.FallbackCenter;
            _fullyRevealedRadius = GuaranteedFullyRevealedRadius;
            _configuredRevealFullyOpenRadius = GuaranteedFullyRevealedRadius;
            CurrentRadius = _fullyRevealedRadius;
            CurrentClosedOvershootPixels = 0f;
        }

        public event Action<TerminalTransitionPlayback> BlackReached;

        public event Action<TerminalTransitionPlayback> RevealCompleted;

        public event Action<TerminalTransitionPlayback> Cancelled;

        public event Action<TerminalTransitionPlayback> StateChanged;

        public TerminalTransitionState State { get; private set; }

        public TerminalTransitionRequest Request { get; private set; }

        public TerminalFocusTarget FocusTarget { get; private set; }

        public TerminalIrisRuntimePreset Preset => _preset;

        public TerminalIrisRuntimeEdgeSettings CurrentEdge =>
            State == TerminalTransitionState.Revealing && _preset.RevealPreset.HasValue
                ? _preset.RevealPreset.Value.Edge
                : _preset.Edge;

        public Vector2 CurrentCenter { get; private set; }

        public float CurrentRadius { get; private set; }

        public float CurrentClosedOvershootPixels { get; private set; }

        public bool IsTerminal =>
            State == TerminalTransitionState.Completed ||
            State == TerminalTransitionState.Cancelled ||
            State == TerminalTransitionState.Disposed;

        public bool CompletedToResultBackdrop => _completedToResultBackdrop;

        public void ConfigureFullyRevealedRadii(
            float closeFullyRevealedRadius,
            float revealFullyRevealedRadius)
        {
            if (_disposed || State != TerminalTransitionState.Hidden)
            {
                throw new InvalidOperationException(
                    "Terminal Iris fully-revealed radii can only be configured before playback begins.");
            }

            RequireFinitePositiveRadius(
                closeFullyRevealedRadius,
                nameof(closeFullyRevealedRadius));
            RequireFinitePositiveRadius(
                revealFullyRevealedRadius,
                nameof(revealFullyRevealedRadius));
            _fullyRevealedRadius = closeFullyRevealedRadius;
            _configuredRevealFullyOpenRadius = revealFullyRevealedRadius;
            CurrentRadius = _fullyRevealedRadius;
        }

        public bool TryBegin(TerminalTransitionRequest request, TerminalFocusTarget focusTarget)
        {
            if (_disposed || State != TerminalTransitionState.Hidden)
            {
                return false;
            }

            Request = request;
            FocusTarget = focusTarget;
            _phaseElapsed = 0f;
            CurrentCenter = _preset.FallbackCenter;
            CurrentRadius = _fullyRevealedRadius;
            CurrentClosedOvershootPixels = 0f;
            SetState(TerminalTransitionState.Focusing);
            ApplyVisual();
            return true;
        }

        public void Advance(float unscaledDeltaTime)
        {
            if (_disposed || unscaledDeltaTime < 0f)
            {
                return;
            }

            var remaining = unscaledDeltaTime;
            var guard = 0;
            while (remaining >= 0f && guard++ < 8)
            {
                var duration = ResolveCurrentPhaseDuration();
                if (duration < 0f)
                {
                    break;
                }

                var available = Math.Max(0f, duration - _phaseElapsed);
                var consumed = Math.Min(remaining, available);
                _phaseElapsed += consumed;
                remaining -= consumed;
                ApplyVisual();

                if (_phaseElapsed + 0.000001f < duration)
                {
                    break;
                }

                if (!AdvanceStateAtThreshold())
                {
                    break;
                }

                if (remaining <= 0f && ResolveCurrentPhaseDuration() > 0f)
                {
                    break;
                }
            }
        }

        public bool RequestReveal(TerminalSessionToken token)
        {
            if (_disposed ||
                !_preset.RevealPreset.HasValue ||
                token != Request.Token ||
                Request.DestinationMode != TerminalTransitionDestinationMode.SameScene ||
                State != TerminalTransitionState.Black)
            {
                return false;
            }

            var revealPreset = _preset.RevealPreset.Value;
            _phaseElapsed = 0f;
            _revealFullyOpenRadius = _configuredRevealFullyOpenRadius;
            SetState(TerminalTransitionState.Revealing);
            ApplyVisual();
            return true;
        }

        public bool CompleteHandoff(TerminalSessionToken token)
        {
            if (_disposed ||
                token != Request.Token ||
                Request.DestinationMode != TerminalTransitionDestinationMode.SceneHandoff ||
                State != TerminalTransitionState.Black)
            {
                return false;
            }

            CurrentRadius = 0f;
            CurrentClosedOvershootPixels = _preset.FinalClosedOvershootPixels;
            SetState(TerminalTransitionState.Completed);
            return true;
        }

        public bool CompleteToResultBackdrop(TerminalSessionToken token)
        {
            if (_disposed ||
                token != Request.Token ||
                Request.Kind != TerminalTransitionKind.Victory ||
                Request.DestinationMode != TerminalTransitionDestinationMode.SameScene ||
                State != TerminalTransitionState.Black)
            {
                return false;
            }

            _completedToResultBackdrop = true;
            CurrentRadius = 0f;
            CurrentClosedOvershootPixels = _preset.FinalClosedOvershootPixels;
            SetState(TerminalTransitionState.Completed);
            return true;
        }

        public bool Cancel(TerminalSessionToken token)
        {
            if (_disposed || token != Request.Token || IsTerminal)
            {
                return false;
            }

            SetState(TerminalTransitionState.Cancelled);
            Cancelled?.Invoke(this);
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            BlackReached = null;
            RevealCompleted = null;
            Cancelled = null;
            StateChanged = null;
            State = TerminalTransitionState.Disposed;
        }

        private float ResolveCurrentPhaseDuration()
        {
            return State switch
            {
                TerminalTransitionState.Focusing => _preset.FocusDuration,
                TerminalTransitionState.Holding => _preset.HoldDuration,
                TerminalTransitionState.Closing => _preset.CloseDuration,
                TerminalTransitionState.Revealing =>
                    _preset.RevealPreset?.TotalDuration ?? -1f,
                _ => -1f,
            };
        }

        private bool AdvanceStateAtThreshold()
        {
            _phaseElapsed = 0f;
            switch (State)
            {
                case TerminalTransitionState.Focusing:
                    SetState(TerminalTransitionState.Holding);
                    ApplyVisual();
                    return true;
                case TerminalTransitionState.Holding:
                    SetState(TerminalTransitionState.Closing);
                    ApplyVisual();
                    return true;
                case TerminalTransitionState.Closing:
                    CurrentRadius = 0f;
                    CurrentClosedOvershootPixels = _preset.FinalClosedOvershootPixels;
                    SetState(TerminalTransitionState.Black);
                    if (!_blackRaised)
                    {
                        _blackRaised = true;
                        BlackReached?.Invoke(this);
                    }

                    return false;
                case TerminalTransitionState.Revealing:
                    CurrentRadius = _revealFullyOpenRadius;
                    CurrentClosedOvershootPixels = 0f;
                    SetState(TerminalTransitionState.Completed);
                    if (!_revealRaised)
                    {
                        _revealRaised = true;
                        RevealCompleted?.Invoke(this);
                    }

                    return false;
                default:
                    return false;
            }
        }

        private void ApplyVisual()
        {
            var duration = ResolveCurrentPhaseDuration();
            var progress = duration <= 0f ? 1f : Mathf.Clamp01(_phaseElapsed / duration);
            var focusRadius = Math.Max(
                _preset.MinimumFocusRadius,
                FocusTarget.NormalizedRadius + _preset.FocusPadding);
            switch (State)
            {
                case TerminalTransitionState.Focusing:
                    var focusProgress = TerminalIrisEasingUtility.Evaluate(
                        _preset.FocusEasing,
                        progress);
                    CurrentCenter = Vector2.Lerp(
                        _preset.FallbackCenter,
                        FocusTarget.NormalizedCenter,
                        focusProgress);
                    CurrentRadius = Mathf.Lerp(
                        _fullyRevealedRadius,
                        focusRadius,
                        focusProgress);
                    CurrentClosedOvershootPixels = 0f;
                    break;
                case TerminalTransitionState.Holding:
                    CurrentCenter = FocusTarget.NormalizedCenter;
                    CurrentRadius = focusRadius;
                    CurrentClosedOvershootPixels = 0f;
                    break;
                case TerminalTransitionState.Closing:
                    CurrentCenter = FocusTarget.NormalizedCenter;
                    var closeProgress = TerminalIrisEasingUtility.Evaluate(
                        _preset.CloseEasing,
                        progress);
                    CurrentRadius = Mathf.Lerp(
                        focusRadius,
                        0f,
                        closeProgress);
                    CurrentClosedOvershootPixels = Mathf.Lerp(
                        0f,
                        _preset.FinalClosedOvershootPixels,
                        closeProgress);
                    break;
                case TerminalTransitionState.Black:
                    CurrentCenter = FocusTarget.NormalizedCenter;
                    CurrentRadius = 0f;
                    CurrentClosedOvershootPixels = _preset.FinalClosedOvershootPixels;
                    break;
                case TerminalTransitionState.Revealing:
                    var revealPreset = _preset.RevealPreset.Value;
                    var openingElapsed = Math.Max(
                        0f,
                        _phaseElapsed - revealPreset.PreOpenHoldDuration);
                    var openingProgress = Mathf.Clamp01(
                        openingElapsed / revealPreset.OpeningDuration);
                    CurrentCenter = FocusTarget.NormalizedCenter;
                    CurrentRadius = Mathf.Lerp(
                        0f,
                        _revealFullyOpenRadius,
                        TerminalIrisEasingUtility.Evaluate(
                            revealPreset.OpeningEasing,
                            openingProgress));
                    CurrentClosedOvershootPixels = Mathf.Lerp(
                        revealPreset.FinalClosedOvershootPixels,
                        0f,
                        TerminalIrisEasingUtility.Evaluate(
                            revealPreset.OpeningEasing,
                            openingProgress));
                    break;
            }
        }

        public static float CalculateFullyRevealedRadius(
            Vector2 center,
            float aspectRatio,
            float fullOpenMargin)
        {
            if (float.IsNaN(fullOpenMargin) ||
                float.IsInfinity(fullOpenMargin) ||
                fullOpenMargin < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fullOpenMargin),
                    fullOpenMargin,
                    "Terminal Iris full-open margin must be finite and non-negative.");
            }

            var boundedAspect = Math.Max(0.0001f, aspectRatio);
            var maxDistance = 0f;
            for (var x = 0; x <= 1; x++)
            {
                for (var y = 0; y <= 1; y++)
                {
                    var delta = new Vector2(x - center.x, y - center.y);
                    delta.x *= boundedAspect;
                    maxDistance = Math.Max(maxDistance, delta.magnitude);
                }
            }

            return maxDistance + fullOpenMargin;
        }

        private static void RequireFinitePositiveRadius(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Terminal Iris fully-revealed radius must be finite and greater than zero.");
            }
        }

        private void SetState(TerminalTransitionState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            StateChanged?.Invoke(this);
        }
    }

    public enum SceneEntryPresentationPhase
    {
        Inactive = 0,
        Claimed = 1,
        PersistentCoverRequested = 2,
        PersistentCoverReady = 3,
        Loading = 4,
        WaitingRuntimeReady = 5,
        EntryIrisClosed = 6,
        Opening = 7,
        Completed = 8,
        FailedHoldingCover = 9,
    }

    public enum SceneEntryRuntimeReadyProvenance
    {
        None = 0,
        ProductionGameplayBootstrap = 1,
    }

    public readonly struct SceneEntrySessionToken : IEquatable<SceneEntrySessionToken>
    {
        public SceneEntrySessionToken(long value)
        {
            Value = value;
        }

        public long Value { get; }
        public bool IsValid => Value > 0;
        public bool Equals(SceneEntrySessionToken other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SceneEntrySessionToken other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => IsValid ? Value.ToString() : "none";
        public static bool operator ==(SceneEntrySessionToken left, SceneEntrySessionToken right) => left.Equals(right);
        public static bool operator !=(SceneEntrySessionToken left, SceneEntrySessionToken right) => !left.Equals(right);
    }

    public readonly struct SceneEntryPresentationSnapshot
    {
        public SceneEntryPresentationSnapshot(
            bool isActive,
            SceneEntrySessionToken token,
            SceneEntryPresentationPhase phase,
            SceneTransitionIntent transitionIntent,
            StageId destinationStageId,
            long transitionId,
            long sourceSceneGeneration,
            long destinationSceneGeneration,
            string launchProvenance,
            int launchSlotNumber,
            Guid launchToken,
            string failureReason)
        {
            IsActive = isActive;
            Token = token;
            Phase = phase;
            TransitionIntent = transitionIntent;
            DestinationStageId = destinationStageId;
            TransitionId = transitionId;
            SourceSceneGeneration = sourceSceneGeneration;
            DestinationSceneGeneration = destinationSceneGeneration;
            LaunchProvenance = launchProvenance ?? string.Empty;
            LaunchSlotNumber = launchSlotNumber;
            LaunchToken = launchToken;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool IsActive { get; }
        public SceneEntrySessionToken Token { get; }
        public SceneEntryPresentationPhase Phase { get; }
        public SceneTransitionIntent TransitionIntent { get; }
        public StageId DestinationStageId { get; }
        public long TransitionId { get; }
        public long SourceSceneGeneration { get; }
        public long DestinationSceneGeneration { get; }
        public string LaunchProvenance { get; }
        public int LaunchSlotNumber { get; }
        public Guid LaunchToken { get; }
        public string FailureReason { get; }
    }

    public readonly struct SceneEntryRuntimeReady
    {
        public SceneEntryRuntimeReady(
            SceneEntrySessionToken token,
            long transitionId,
            long destinationSceneGeneration,
            int playerEntityId,
            Camera outputCamera,
            SceneEntryRuntimeReadyProvenance provenance)
        {
            Token = token;
            TransitionId = transitionId;
            DestinationSceneGeneration = destinationSceneGeneration;
            PlayerEntityId = playerEntityId;
            OutputCamera = outputCamera;
            Provenance = provenance;
        }

        public SceneEntrySessionToken Token { get; }
        public long TransitionId { get; }
        public long DestinationSceneGeneration { get; }
        public int PlayerEntityId { get; }
        public Camera OutputCamera { get; }
        public SceneEntryRuntimeReadyProvenance Provenance { get; }
    }

    public interface ISceneEntryPresentationReadModel
    {
        event Action<SceneEntryPresentationSnapshot> Changed;
        bool IsActive { get; }
        SceneEntryPresentationSnapshot Current { get; }
    }

    public static class SceneEntryPresentationRegistry
    {
        private sealed class ReadModelImpl : ISceneEntryPresentationReadModel
        {
            public event Action<SceneEntryPresentationSnapshot> Changed;
            public bool IsActive => Current.IsActive;
            public SceneEntryPresentationSnapshot Current { get; private set; }

            public void Publish(SceneEntryPresentationSnapshot snapshot)
            {
                Current = snapshot;
                Changed?.Invoke(snapshot);
            }

            public void Reset()
            {
                Current = default;
                Changed = null;
            }
        }

        private static readonly ReadModelImpl Model = new();
        private static long _nextToken;

        public static ISceneEntryPresentationReadModel ReadModel => Model;
        public static SceneEntryPresentationSnapshot Current => Model.Current;
        public static bool IsActive => Model.IsActive;

        public static bool TryClaim(
            StageId destinationStageId,
            long sourceSceneGeneration,
            out SceneEntrySessionToken token)
        {
            return TryClaim(
                SceneTransitionIntent.StageAdvance,
                destinationStageId,
                sourceSceneGeneration,
                out token);
        }

        public static bool TryClaim(
            SceneTransitionIntent transitionIntent,
            StageId destinationStageId,
            long sourceSceneGeneration,
            out SceneEntrySessionToken token)
        {
            return TryClaim(
                transitionIntent,
                destinationStageId,
                sourceSceneGeneration,
                string.Empty,
                0,
                Guid.Empty,
                out token);
        }

        public static bool TryClaim(
            SceneTransitionIntent transitionIntent,
            StageId destinationStageId,
            long sourceSceneGeneration,
            string launchProvenance,
            int launchSlotNumber,
            Guid launchToken,
            out SceneEntrySessionToken token)
        {
            token = default;
            if (Model.IsActive ||
                transitionIntent == SceneTransitionIntent.Unknown ||
                !destinationStageId.IsValid ||
                sourceSceneGeneration <= 0 ||
                launchSlotNumber < 0 ||
                launchSlotNumber > SaveSlotStore.SlotCount)
            {
                return false;
            }

            token = new SceneEntrySessionToken(Interlocked.Increment(ref _nextToken));
            Model.Publish(new SceneEntryPresentationSnapshot(
                true,
                token,
                SceneEntryPresentationPhase.Claimed,
                transitionIntent,
                destinationStageId,
                0,
                sourceSceneGeneration,
                0,
                launchProvenance,
                launchSlotNumber,
                launchToken,
                string.Empty));
            return true;
        }

        public static bool TryBindTransition(SceneEntrySessionToken token, long transitionId)
        {
            var current = Model.Current;
            if (!Matches(current, token) || current.Phase != SceneEntryPresentationPhase.Claimed || transitionId <= 0)
            {
                return false;
            }

            Publish(current, SceneEntryPresentationPhase.PersistentCoverRequested, transitionId, 0, string.Empty);
            return true;
        }

        public static bool TryAdvance(SceneEntrySessionToken token, SceneEntryPresentationPhase phase)
        {
            var current = Model.Current;
            if (!Matches(current, token) ||
                !IsNextPhase(current.Phase, phase))
            {
                return false;
            }

            Publish(current, phase, current.TransitionId, current.DestinationSceneGeneration, string.Empty);
            return true;
        }

        public static bool TryRegisterDestinationScene(SceneEntrySessionToken token, long destinationSceneGeneration)
        {
            var current = Model.Current;
            if (!Matches(current, token) ||
                current.Phase != SceneEntryPresentationPhase.Loading ||
                current.TransitionId <= 0 ||
                destinationSceneGeneration <= 0 ||
                destinationSceneGeneration == current.SourceSceneGeneration)
            {
                return false;
            }

            Publish(
                current,
                SceneEntryPresentationPhase.WaitingRuntimeReady,
                current.TransitionId,
                destinationSceneGeneration,
                string.Empty);
            return true;
        }

        public static bool CanAcceptRuntimeReady(SceneEntryRuntimeReady ready)
        {
            var current = Model.Current;
            if (!Matches(current, ready.Token))
            {
                return false;
            }

            var routePolicy =
                SceneTransitionRoutePolicyCatalog.ResolveProduction(
                    current.TransitionIntent);
            return routePolicy.Status == SceneTransitionRouteStatus.Canonical &&
                   routePolicy.DestinationKind == SceneTransitionDestinationKind.Gameplay &&
                   routePolicy.ImplementsSceneTransitionSession &&
                   routePolicy.ImplementsDestinationReadiness &&
                   routePolicy.ImplementsDestinationRenderAcknowledgement &&
                   routePolicy.ImplementsInputAdmission &&
                   current.Phase == SceneEntryPresentationPhase.WaitingRuntimeReady &&
                   ready.TransitionId == current.TransitionId &&
                   ready.DestinationSceneGeneration == current.DestinationSceneGeneration &&
                   ready.PlayerEntityId > 0 &&
                   ready.OutputCamera != null &&
                   ready.Provenance == SceneEntryRuntimeReadyProvenance.ProductionGameplayBootstrap;
        }

        public static bool TryComplete(SceneEntrySessionToken token)
        {
            var current = Model.Current;
            if (!Matches(current, token) || current.Phase != SceneEntryPresentationPhase.Opening)
            {
                return false;
            }

            Model.Publish(new SceneEntryPresentationSnapshot(
                false,
                current.Token,
                SceneEntryPresentationPhase.Completed,
                current.TransitionIntent,
                current.DestinationStageId,
                current.TransitionId,
                current.SourceSceneGeneration,
                current.DestinationSceneGeneration,
                current.LaunchProvenance,
                current.LaunchSlotNumber,
                current.LaunchToken,
                string.Empty));
            return true;
        }

        public static bool TryFailHoldingCover(SceneEntrySessionToken token, string failureReason)
        {
            var current = Model.Current;
            if (!Matches(current, token))
            {
                return false;
            }

            Publish(
                current,
                SceneEntryPresentationPhase.FailedHoldingCover,
                current.TransitionId,
                current.DestinationSceneGeneration,
                failureReason);
            return true;
        }

        public static bool TryCancelClaim(SceneEntrySessionToken token)
        {
            var current = Model.Current;
            if (!Matches(current, token) ||
                current.Phase != SceneEntryPresentationPhase.Claimed)
            {
                return false;
            }

            Model.Publish(default);
            return true;
        }

        internal static void ResetForTests()
        {
            Model.Reset();
            _nextToken = 0;
        }

        private static bool Matches(SceneEntryPresentationSnapshot current, SceneEntrySessionToken token)
        {
            return current.IsActive && token.IsValid && current.Token == token;
        }

        private static bool IsNextPhase(
            SceneEntryPresentationPhase current,
            SceneEntryPresentationPhase next)
        {
            return (current == SceneEntryPresentationPhase.PersistentCoverRequested &&
                    next == SceneEntryPresentationPhase.PersistentCoverReady) ||
                   (current == SceneEntryPresentationPhase.PersistentCoverReady &&
                    next == SceneEntryPresentationPhase.Loading) ||
                   (current == SceneEntryPresentationPhase.WaitingRuntimeReady &&
                    next == SceneEntryPresentationPhase.EntryIrisClosed) ||
                   (current == SceneEntryPresentationPhase.EntryIrisClosed &&
                    next == SceneEntryPresentationPhase.Opening);
        }

        private static void Publish(
            SceneEntryPresentationSnapshot current,
            SceneEntryPresentationPhase phase,
            long transitionId,
            long destinationSceneGeneration,
            string failureReason)
        {
            Model.Publish(new SceneEntryPresentationSnapshot(
                true,
                current.Token,
                phase,
                current.TransitionIntent,
                current.DestinationStageId,
                transitionId,
                current.SourceSceneGeneration,
                destinationSceneGeneration,
                current.LaunchProvenance,
                current.LaunchSlotNumber,
                current.LaunchToken,
                failureReason));
        }
    }

    public enum MainMenuDestinationReadyProvenance
    {
        None = 0,
        ProductionMainMenuComposition = 1,
    }

    public readonly struct MainMenuEntrySessionToken : IEquatable<MainMenuEntrySessionToken>
    {
        public MainMenuEntrySessionToken(long value)
        {
            Value = value;
        }

        public long Value { get; }
        public bool IsValid => Value > 0;
        public bool Equals(MainMenuEntrySessionToken other) => Value == other.Value;
        public override bool Equals(object obj) =>
            obj is MainMenuEntrySessionToken other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => IsValid ? Value.ToString() : "none";
        public static bool operator ==(
            MainMenuEntrySessionToken left,
            MainMenuEntrySessionToken right) => left.Equals(right);
        public static bool operator !=(
            MainMenuEntrySessionToken left,
            MainMenuEntrySessionToken right) => !left.Equals(right);
    }

    public readonly struct MainMenuEntryPresentationSnapshot
    {
        public MainMenuEntryPresentationSnapshot(
            bool isActive,
            MainMenuEntrySessionToken token,
            SceneEntryPresentationPhase phase,
            SceneTransitionIntent transitionIntent,
            long transitionId,
            long sourceSceneGeneration,
            long destinationSceneGeneration,
            string sourceProvenance,
            string failureReason)
        {
            IsActive = isActive;
            Token = token;
            Phase = phase;
            TransitionIntent = transitionIntent;
            TransitionId = transitionId;
            SourceSceneGeneration = sourceSceneGeneration;
            DestinationSceneGeneration = destinationSceneGeneration;
            SourceProvenance = sourceProvenance ?? string.Empty;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool IsActive { get; }
        public MainMenuEntrySessionToken Token { get; }
        public SceneEntryPresentationPhase Phase { get; }
        public SceneTransitionIntent TransitionIntent { get; }
        public long TransitionId { get; }
        public long SourceSceneGeneration { get; }
        public long DestinationSceneGeneration { get; }
        public string SourceProvenance { get; }
        public string FailureReason { get; }
    }

    public readonly struct MainMenuDestinationReady
    {
        public MainMenuDestinationReady(
            MainMenuEntrySessionToken token,
            long transitionId,
            long destinationSceneGeneration,
            MainMenuDestinationReadyProvenance provenance)
        {
            Token = token;
            TransitionId = transitionId;
            DestinationSceneGeneration = destinationSceneGeneration;
            Provenance = provenance;
        }

        public MainMenuEntrySessionToken Token { get; }
        public long TransitionId { get; }
        public long DestinationSceneGeneration { get; }
        public MainMenuDestinationReadyProvenance Provenance { get; }
    }

    public static class MainMenuEntryPresentationRegistry
    {
        private static MainMenuEntryPresentationSnapshot _current;
        private static long _nextToken;

        public static event Action<MainMenuEntryPresentationSnapshot> Changed;

        public static MainMenuEntryPresentationSnapshot Current => _current;
        public static bool IsActive => _current.IsActive;

        public static bool TryClaim(
            SceneTransitionIntent transitionIntent,
            long sourceSceneGeneration,
            string sourceProvenance,
            out MainMenuEntrySessionToken token)
        {
            token = default;
            if (_current.IsActive ||
                sourceSceneGeneration <= 0 ||
                (transitionIntent != SceneTransitionIntent.ReturnToMainMenu &&
                 transitionIntent != SceneTransitionIntent.CinematicToMainMenu))
            {
                return false;
            }

            var routePolicy =
                SceneTransitionRoutePolicyCatalog.ResolveProduction(transitionIntent);
            if (routePolicy.DestinationKind != SceneTransitionDestinationKind.MainMenu ||
                routePolicy.Status != SceneTransitionRouteStatus.Canonical ||
                !routePolicy.ImplementsSceneTransitionSession)
            {
                return false;
            }

            token = new MainMenuEntrySessionToken(
                Interlocked.Increment(ref _nextToken));
            Publish(new MainMenuEntryPresentationSnapshot(
                true,
                token,
                SceneEntryPresentationPhase.Claimed,
                transitionIntent,
                0,
                sourceSceneGeneration,
                0,
                sourceProvenance,
                string.Empty));
            return true;
        }

        public static bool TryBindTransition(
            MainMenuEntrySessionToken token,
            long transitionId)
        {
            var current = _current;
            if (!Matches(current, token) ||
                current.Phase != SceneEntryPresentationPhase.Claimed ||
                transitionId <= 0)
            {
                return false;
            }

            Publish(
                current,
                SceneEntryPresentationPhase.PersistentCoverRequested,
                transitionId,
                0,
                string.Empty);
            return true;
        }

        public static bool TryAdvance(
            MainMenuEntrySessionToken token,
            SceneEntryPresentationPhase phase)
        {
            var current = _current;
            if (!Matches(current, token) ||
                !IsNextPhase(current.Phase, phase))
            {
                return false;
            }

            Publish(
                current,
                phase,
                current.TransitionId,
                current.DestinationSceneGeneration,
                string.Empty);
            return true;
        }

        public static bool TryRegisterDestinationScene(
            MainMenuEntrySessionToken token,
            long destinationSceneGeneration)
        {
            var current = _current;
            if (!Matches(current, token) ||
                current.Phase != SceneEntryPresentationPhase.Loading ||
                current.TransitionId <= 0 ||
                destinationSceneGeneration <= 0 ||
                destinationSceneGeneration == current.SourceSceneGeneration)
            {
                return false;
            }

            Publish(
                current,
                SceneEntryPresentationPhase.WaitingRuntimeReady,
                current.TransitionId,
                destinationSceneGeneration,
                string.Empty);
            return true;
        }

        public static bool CanAcceptRuntimeReady(MainMenuDestinationReady ready)
        {
            var current = _current;
            if (!Matches(current, ready.Token))
            {
                return false;
            }

            var routePolicy = SceneTransitionRoutePolicyCatalog.ResolveProduction(
                current.TransitionIntent);
            return routePolicy.Status == SceneTransitionRouteStatus.Canonical &&
                   routePolicy.DestinationKind ==
                   SceneTransitionDestinationKind.MainMenu &&
                   routePolicy.ImplementsSceneTransitionSession &&
                   routePolicy.ImplementsDestinationReadiness &&
                   routePolicy.ImplementsDestinationRenderAcknowledgement &&
                   routePolicy.ImplementsInputAdmission &&
                   current.Phase ==
                   SceneEntryPresentationPhase.WaitingRuntimeReady &&
                   ready.TransitionId == current.TransitionId &&
                   ready.DestinationSceneGeneration ==
                   current.DestinationSceneGeneration &&
                   ready.Provenance ==
                   MainMenuDestinationReadyProvenance
                       .ProductionMainMenuComposition;
        }

        public static bool TryComplete(MainMenuEntrySessionToken token)
        {
            var current = _current;
            if (!Matches(current, token) ||
                current.Phase != SceneEntryPresentationPhase.Opening)
            {
                return false;
            }

            Publish(new MainMenuEntryPresentationSnapshot(
                false,
                current.Token,
                SceneEntryPresentationPhase.Completed,
                current.TransitionIntent,
                current.TransitionId,
                current.SourceSceneGeneration,
                current.DestinationSceneGeneration,
                current.SourceProvenance,
                string.Empty));
            return true;
        }

        public static bool TryCancelClaim(MainMenuEntrySessionToken token)
        {
            var current = _current;
            if (!Matches(current, token) ||
                current.Phase != SceneEntryPresentationPhase.Claimed)
            {
                return false;
            }

            Publish(default);
            return true;
        }

        public static bool TryFailHoldingCover(
            MainMenuEntrySessionToken token,
            string failureReason)
        {
            var current = _current;
            if (!Matches(current, token))
            {
                return false;
            }

            Publish(
                current,
                SceneEntryPresentationPhase.FailedHoldingCover,
                current.TransitionId,
                current.DestinationSceneGeneration,
                string.IsNullOrWhiteSpace(failureReason)
                    ? "Main Menu destination failed while the opaque cover remained active."
                    : failureReason);
            return true;
        }

        internal static void ResetForTests()
        {
            _current = default;
            _nextToken = 0;
            Changed = null;
        }

        private static bool Matches(
            MainMenuEntryPresentationSnapshot current,
            MainMenuEntrySessionToken token)
        {
            return current.IsActive && token.IsValid && current.Token == token;
        }

        private static bool IsNextPhase(
            SceneEntryPresentationPhase current,
            SceneEntryPresentationPhase next)
        {
            return (current ==
                    SceneEntryPresentationPhase.PersistentCoverRequested &&
                    next == SceneEntryPresentationPhase.PersistentCoverReady) ||
                   (current ==
                    SceneEntryPresentationPhase.PersistentCoverReady &&
                    next == SceneEntryPresentationPhase.Loading) ||
                   (current ==
                    SceneEntryPresentationPhase.WaitingRuntimeReady &&
                    next == SceneEntryPresentationPhase.EntryIrisClosed) ||
                   (current == SceneEntryPresentationPhase.EntryIrisClosed &&
                    next == SceneEntryPresentationPhase.Opening);
        }

        private static void Publish(
            MainMenuEntryPresentationSnapshot current,
            SceneEntryPresentationPhase phase,
            long transitionId,
            long destinationSceneGeneration,
            string failureReason)
        {
            Publish(new MainMenuEntryPresentationSnapshot(
                true,
                current.Token,
                phase,
                current.TransitionIntent,
                transitionId,
                current.SourceSceneGeneration,
                destinationSceneGeneration,
                current.SourceProvenance,
                failureReason));
        }

        private static void Publish(MainMenuEntryPresentationSnapshot snapshot)
        {
            _current = snapshot;
            Changed?.Invoke(snapshot);
        }
    }
}
