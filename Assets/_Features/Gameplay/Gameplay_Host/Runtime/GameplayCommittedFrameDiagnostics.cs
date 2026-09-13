using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Game.Feature.Gameplay.Host
{
    internal enum CommittedFrameStoreReason
    {
        Initial = 0,
        Tick = 1,
    }

    internal enum StaticWallTargetSelectionKind
    {
        NotPresentable = 0,
        FallbackWithoutCache = 1,
        Rebuild = 2,
        Hit = 3,
    }

    internal enum StaticWallTargetRetireReason
    {
        None = 0,
        RemovedOrExit = 1,
        SignatureChanged = 2,
        SpawnOrGeneration = 3,
        UnknownGeneration = 4,
    }

    internal enum StaticWallTargetObservationKind
    {
        InputSelection = 0,
        MissingLifecycle = 1,
    }

    [Flags]
    internal enum StaticWallTargetInvalidation
    {
        None = 0,
        Session = 1 << 0,
        Generation = 1 << 1,
        Signature = 1 << 2,
        StaticRevision = 1 << 3,
        Topology = 1 << 4,
        ProjectorProfile = 1 << 5,
    }

    internal readonly struct StaticWallTargetRetireHistogram
    {
        internal StaticWallTargetRetireHistogram(
            int removedOrExit,
            int signatureChanged,
            int spawnOrGeneration,
            int unknownGeneration)
        {
            RemovedOrExit = removedOrExit;
            SignatureChanged = signatureChanged;
            SpawnOrGeneration = spawnOrGeneration;
            UnknownGeneration = unknownGeneration;
        }

        internal int RemovedOrExit { get; }
        internal int SignatureChanged { get; }
        internal int SpawnOrGeneration { get; }
        internal int UnknownGeneration { get; }
        internal int Total => RemovedOrExit + SignatureChanged + SpawnOrGeneration + UnknownGeneration;

        internal int Count(StaticWallTargetRetireReason reason)
        {
            return reason switch
            {
                StaticWallTargetRetireReason.None => 0,
                StaticWallTargetRetireReason.RemovedOrExit => RemovedOrExit,
                StaticWallTargetRetireReason.SignatureChanged => SignatureChanged,
                StaticWallTargetRetireReason.SpawnOrGeneration => SpawnOrGeneration,
                StaticWallTargetRetireReason.UnknownGeneration => UnknownGeneration,
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
            };
        }
    }

    internal readonly struct StaticWallTargetInvalidationHistogram
    {
        internal StaticWallTargetInvalidationHistogram(
            int session,
            int generation,
            int signature,
            int staticRevision,
            int topology,
            int projectorProfile)
        {
            Session = session;
            Generation = generation;
            Signature = signature;
            StaticRevision = staticRevision;
            Topology = topology;
            ProjectorProfile = projectorProfile;
        }

        internal int Session { get; }
        internal int Generation { get; }
        internal int Signature { get; }
        internal int StaticRevision { get; }
        internal int Topology { get; }
        internal int ProjectorProfile { get; }
        internal int Total => Session + Generation + Signature + StaticRevision + Topology + ProjectorProfile;

        internal int Count(StaticWallTargetInvalidation reason)
        {
            return reason switch
            {
                StaticWallTargetInvalidation.Session => Session,
                StaticWallTargetInvalidation.Generation => Generation,
                StaticWallTargetInvalidation.Signature => Signature,
                StaticWallTargetInvalidation.StaticRevision => StaticRevision,
                StaticWallTargetInvalidation.Topology => Topology,
                StaticWallTargetInvalidation.ProjectorProfile => ProjectorProfile,
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
            };
        }
    }

    internal readonly struct StaticWallTargetEntityObservation
    {
        internal StaticWallTargetEntityObservation(
            long sessionId,
            int frameOrdinal,
            int entityId,
            StaticWallTargetObservationKind observationKind,
            bool hasInputEntityState,
            string canonicalInputEntityState,
            bool isProvenanceCandidate,
            string provenanceSignature,
            int presentationGeneration,
            string staticRevision,
            int topologyRevision,
            int projectorProfileRevision,
            bool isAdmittedStaticWall,
            bool isPolicyPresentable,
            StaticWallTargetSelectionKind selectionKind,
            StaticWallTargetRetireReason retireReason,
            StaticWallTargetInvalidation invalidations,
            bool didRunProjection,
            bool didRunPose,
            bool didRunSlot,
            bool didRunViewResolve,
            bool viewResolved,
            bool didUseFallbackValuePath,
            int insertOrdinal)
        {
            SessionId = sessionId;
            FrameOrdinal = frameOrdinal;
            EntityId = entityId;
            ObservationKind = observationKind;
            HasInputEntityState = hasInputEntityState;
            CanonicalInputEntityState = canonicalInputEntityState ?? string.Empty;
            IsProvenanceCandidate = isProvenanceCandidate;
            ProvenanceSignature = provenanceSignature ?? string.Empty;
            PresentationGeneration = presentationGeneration;
            StaticRevision = staticRevision ?? string.Empty;
            TopologyRevision = topologyRevision;
            ProjectorProfileRevision = projectorProfileRevision;
            IsAdmittedStaticWall = isAdmittedStaticWall;
            IsPolicyPresentable = isPolicyPresentable;
            SelectionKind = selectionKind;
            RetireReason = retireReason;
            Invalidations = invalidations;
            DidRunProjection = didRunProjection;
            DidRunPose = didRunPose;
            DidRunSlot = didRunSlot;
            DidRunViewResolve = didRunViewResolve;
            ViewResolved = viewResolved;
            DidUseFallbackValuePath = didUseFallbackValuePath;
            InsertOrdinal = insertOrdinal;
        }

        internal long SessionId { get; }
        internal int FrameOrdinal { get; }
        internal int EntityId { get; }
        internal StaticWallTargetObservationKind ObservationKind { get; }
        internal bool HasInputEntityState { get; }
        internal string CanonicalInputEntityState { get; }
        internal bool IsProvenanceCandidate { get; }
        internal string ProvenanceSignature { get; }
        internal int PresentationGeneration { get; }
        internal string StaticRevision { get; }
        internal int TopologyRevision { get; }
        internal int ProjectorProfileRevision { get; }
        internal bool IsAdmittedStaticWall { get; }
        internal bool IsPolicyPresentable { get; }
        internal StaticWallTargetSelectionKind SelectionKind { get; }
        internal StaticWallTargetRetireReason RetireReason { get; }
        internal StaticWallTargetInvalidation Invalidations { get; }
        internal bool DidRunProjection { get; }
        internal bool DidRunPose { get; }
        internal bool DidRunSlot { get; }
        internal bool DidRunViewResolve { get; }
        internal bool ViewResolved { get; }
        internal bool DidUseFallbackValuePath { get; }
        internal int InsertOrdinal { get; }
        internal bool DidInsert => InsertOrdinal >= 0;
    }

    internal sealed class GameplayCommittedFrameObservation
    {
        internal const string SelectionSchema =
            "package4-selection-schema-v2|frame:session,ordinal,tick,reason,input,provenance,eligible,hit,miss,rebuild,retire-by-reason,invalidation-by-reason,fallback-scan,dynamic-project,project,pose,slot,view,insert,output-ids,input-fingerprint,canonical-input,cache-count,cache-high-water|row:session,ordinal,entity-id,observation-kind,has-input,canonical-entity,provenance-candidate,provenance-signature,generation,static-revision,topology-revision,projector-revision,admitted,policy-presentable,selection,retire,invalidation,projection,pose,slot,view-resolve,view-resolved,fallback,insert-ordinal";

        internal static string SelectionSchemaDigest { get; } = ComputeSha256(SelectionSchema);

        internal GameplayCommittedFrameObservation(
            long sessionId,
            int frameOrdinal,
            int tickIndex,
            CommittedFrameStoreReason reason,
            int inputCount,
            int provenanceCount,
            int eligibleCount,
            int hitCount,
            int missCount,
            int rebuildCount,
            StaticWallTargetRetireHistogram retireHistogram,
            StaticWallTargetInvalidationHistogram invalidationHistogram,
            int fallbackEntityScanCount,
            int dynamicEntityProjectCount,
            int tryProjectEntityCellCount,
            int createEntityPoseCount,
            int tryGetProjectedEntitySlotCount,
            int viewResolveCount,
            int insertCount,
            IReadOnlyList<int> outputEntityIds,
            string inputSemanticFingerprint,
            string canonicalInputDump,
            int cacheCount,
            int cacheHighWaterMark,
            IReadOnlyList<StaticWallTargetEntityObservation> entityObservations)
        {
            SessionId = sessionId;
            FrameOrdinal = frameOrdinal;
            TickIndex = tickIndex;
            Reason = reason;
            InputCount = inputCount;
            ProvenanceCount = provenanceCount;
            EligibleCount = eligibleCount;
            HitCount = hitCount;
            MissCount = missCount;
            RebuildCount = rebuildCount;
            RetireHistogram = retireHistogram;
            InvalidationHistogram = invalidationHistogram;
            FallbackEntityScanCount = fallbackEntityScanCount;
            DynamicEntityProjectCount = dynamicEntityProjectCount;
            TryProjectEntityCellCount = tryProjectEntityCellCount;
            CreateEntityPoseCount = createEntityPoseCount;
            TryGetProjectedEntitySlotCount = tryGetProjectedEntitySlotCount;
            ViewResolveCount = viewResolveCount;
            InsertCount = insertCount;
            OutputEntityIds = new ReadOnlyCollection<int>(new List<int>(outputEntityIds));
            InputSemanticFingerprint = inputSemanticFingerprint ?? string.Empty;
            CanonicalInputDump = canonicalInputDump ?? string.Empty;
            CacheCount = cacheCount;
            CacheHighWaterMark = cacheHighWaterMark;
            EntityObservations = new ReadOnlyCollection<StaticWallTargetEntityObservation>(
                new List<StaticWallTargetEntityObservation>(entityObservations));
        }

        internal long SessionId { get; }
        internal int FrameOrdinal { get; }
        internal int TickIndex { get; }
        internal CommittedFrameStoreReason Reason { get; }
        internal int InputCount { get; }
        internal int ProvenanceCount { get; }
        internal int EligibleCount { get; }
        internal int HitCount { get; }
        internal int MissCount { get; }
        internal int RebuildCount { get; }
        internal StaticWallTargetRetireHistogram RetireHistogram { get; }
        internal StaticWallTargetInvalidationHistogram InvalidationHistogram { get; }
        internal int RetireCount => RetireHistogram.Total;
        internal int InvalidationCount => InvalidationHistogram.Total;
        internal int FallbackEntityScanCount { get; }
        internal int DynamicEntityProjectCount { get; }
        internal int TryProjectEntityCellCount { get; }
        internal int CreateEntityPoseCount { get; }
        internal int TryGetProjectedEntitySlotCount { get; }
        internal int ViewResolveCount { get; }
        internal int InsertCount { get; }
        internal IReadOnlyList<int> OutputEntityIds { get; }
        internal string InputSemanticFingerprint { get; }
        internal string CanonicalInputDump { get; }
        internal int CacheCount { get; }
        internal int CacheHighWaterMark { get; }
        internal IReadOnlyList<StaticWallTargetEntityObservation> EntityObservations { get; }

        private static string ComputeSha256(string value)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
            var builder = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++)
            {
                builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }

    internal static class GameplayCommittedFrameDiagnostics
    {
        [ThreadStatic] private static Capture _current;

        internal static bool IsEnabled => _current != null;

        internal static CaptureScope BeginCapture()
        {
            var previous = _current;
            var capture = new Capture();
            _current = capture;
            return new CaptureScope(previous, capture);
        }

        internal static void Record(GameplayCommittedFrameObservation observation)
        {
            _current?.Frames.Add(observation);
        }

        internal sealed class Capture
        {
            internal List<GameplayCommittedFrameObservation> Frames { get; } = new();
        }

        internal sealed class CaptureScope : IDisposable
        {
            private readonly Capture _previous;
            private readonly Capture _capture;
            private bool _disposed;

            internal CaptureScope(Capture previous, Capture capture)
            {
                _previous = previous;
                _capture = capture;
            }

            internal IReadOnlyList<GameplayCommittedFrameObservation> Frames => _capture.Frames;

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _current = _previous;
            }
        }
    }
}
