using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.Loop
{
    public enum TickEntityMotionKind
    {
        None = 0,
        Move = 1,
        Push = 2,
        Flip = 3,
        ProjectileMove = 4,
        BoxSlide = 5,
    }

    public enum TickVisibilityChangeKind
    {
        None = 0,
        Spawn = 1,
        Detach = 2,
        Remove = 3,
    }

    public enum TickTransitionVisibilityMode
    {
        None = 0,
        RetainUntilTransitionComplete = 1,
        ShowAtTransitionStart = 2,
    }

    public enum TilePresentationEventKind
    {
        None = 0,
        ButtonActivated = 1,
        DestroyTileTriggered = 2,
        SlideTileRedirected = 3,
        BarricadeBlocked = 4,
        BarricadeCrushed = 5,
        ExitOpened = 6,
        ExitEntered = 7,
        MoonBlockGenerated = 8,
        BarricadeActivated = 9,
        BarricadeDeactivated = 10,
        MoonBlockGeneratorBlocked = 11,
        DestroyTileActivated = 12,
        DestroyTileDeactivated = 13,
        ExitObjectiveCleared = 14,
    }

    public enum EntityPresentationKind
    {
        Unknown = 0,
        Player = 1,
        Enemy = 2,
        Box = 3,
        Projectile = 4,
    }

    public enum EntitySpawnPresentationReason
    {
        Unknown = 0,
        InitialStageStart = 1,
        PlayerRespawn = 2,
        EnemySpawn = 3,
        EnemySummon = 4,
        ProjectileSpawn = 5,
        BoxSpawn = 6,
        StageInitialPlacement = 7,
        ScriptedSpawn = 8,
    }

    public readonly struct TileFeaturePresentationSource
    {
        public TileFeaturePresentationSource(
            int tileId,
            TileFeatureKind featureKind,
            SurfaceCell cell)
        {
            TileId = tileId;
            FeatureKind = featureKind;
            Cell = cell;
        }

        public int TileId { get; }

        public TileFeatureKind FeatureKind { get; }

        public SurfaceCell Cell { get; }
    }

    public enum TickPlayerOutcomePresentationKind
    {
        None = 0,
        StageClearVictory = 1,
    }

    public readonly struct TickPlayerOutcomePresentationSignal
    {
        public TickPlayerOutcomePresentationSignal(
            int entityId,
            TickPlayerOutcomePresentationKind outcomeKind,
            int sourceTileId,
            SurfaceCell sourceCell)
        {
            EntityId = entityId;
            OutcomeKind = outcomeKind;
            SourceTileId = sourceTileId;
            SourceCell = sourceCell;
        }

        public int EntityId { get; }

        public TickPlayerOutcomePresentationKind OutcomeKind { get; }

        public int SourceTileId { get; }

        public SurfaceCell SourceCell { get; }
    }

    public readonly struct EntitySpawnPresentationSignal
    {
        public EntitySpawnPresentationSignal(
            int entityId,
            EntityPresentationKind entityKind,
            EntitySpawnPresentationReason reason,
            SurfaceCell cell,
            CubeTopologyState topology,
            Direction facing,
            TileFeaturePresentationSource? sourceTileFeature)
        {
            EntityId = entityId;
            EntityKind = entityKind;
            Reason = reason;
            Cell = cell;
            Topology = topology;
            Facing = facing;
            SourceTileFeature = sourceTileFeature;
        }

        public int EntityId { get; }

        public EntityPresentationKind EntityKind { get; }

        public EntitySpawnPresentationReason Reason { get; }

        public SurfaceCell Cell { get; }

        public CubeTopologyState Topology { get; }

        public Direction Facing { get; }

        public TileFeaturePresentationSource? SourceTileFeature { get; }
    }

    public sealed class InitialPresentationData
    {
        public static readonly InitialPresentationData Empty = new(Array.Empty<EntitySpawnPresentationSignal>());

        private readonly ReadOnlyCollection<EntitySpawnPresentationSignal> _entitySpawnSignals;

        public InitialPresentationData(
            IReadOnlyList<EntitySpawnPresentationSignal> entitySpawnSignals)
        {
            _entitySpawnSignals = new ReadOnlyCollection<EntitySpawnPresentationSignal>(
                new List<EntitySpawnPresentationSignal>(
                    entitySpawnSignals ?? Array.Empty<EntitySpawnPresentationSignal>()));
        }

        public IReadOnlyList<EntitySpawnPresentationSignal> EntitySpawnSignals => _entitySpawnSignals;
    }

    internal static class EntitySpawnPresentationSourceResolver
    {
        public static TileFeaturePresentationSource? TryResolveEntranceSource(
            SurfaceCell cell,
            IReadOnlyList<TileFeatureState> tileFeatures)
        {
            if (tileFeatures == null || tileFeatures.Count == 0)
            {
                return null;
            }

            for (var i = 0; i < tileFeatures.Count; i++)
            {
                var tileFeature = tileFeatures[i];
                if (tileFeature.Kind == TileFeatureKind.Entrance &&
                    tileFeature.Cell.Equals(cell))
                {
                    return new TileFeaturePresentationSource(
                        tileFeature.TileId,
                        tileFeature.Kind,
                        tileFeature.Cell);
                }
            }

            return null;
        }
    }

    public enum PresentationTimingKind
    {
        Immediate = 0,
        DelaySeconds = 1,
        MotionContact = 2,
        MotionEnd = 3,
        Barrier = 4,
    }

    public enum PresentationBarrierKind
    {
        None = 0,
        ButtonActivated = 1,
        EntityExit = 2,
        ObjectiveCondition = 3,
        StageClear = 4,
    }

    public readonly struct PresentationBarrierKey : IEquatable<PresentationBarrierKey>
    {
        private PresentationBarrierKey(PresentationBarrierKind kind, int primaryId)
        {
            Kind = kind;
            PrimaryId = primaryId;
        }

        public PresentationBarrierKind Kind { get; }

        public int PrimaryId { get; }

        public bool IsValid => Kind != PresentationBarrierKind.None;

        public static PresentationBarrierKey None()
        {
            return default;
        }

        public static PresentationBarrierKey ButtonActivated(int tileId)
        {
            return tileId > 0
                ? new PresentationBarrierKey(PresentationBarrierKind.ButtonActivated, tileId)
                : default;
        }

        public static PresentationBarrierKey EntityExit(int entityId)
        {
            return entityId > 0
                ? new PresentationBarrierKey(PresentationBarrierKind.EntityExit, entityId)
                : default;
        }

        public static PresentationBarrierKey ObjectiveCondition(int conditionId)
        {
            return conditionId > 0
                ? new PresentationBarrierKey(PresentationBarrierKind.ObjectiveCondition, conditionId)
                : default;
        }

        public static PresentationBarrierKey StageClear(int stageRunId)
        {
            return stageRunId > 0
                ? new PresentationBarrierKey(PresentationBarrierKind.StageClear, stageRunId)
                : default;
        }

        public bool Equals(PresentationBarrierKey other)
        {
            return Kind == other.Kind && PrimaryId == other.PrimaryId;
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationBarrierKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Kind * 397) ^ PrimaryId;
            }
        }
    }

    public readonly struct PresentationTimingAnchor : IEquatable<PresentationTimingAnchor>
    {
        private PresentationTimingAnchor(
            PresentationTimingKind kind,
            float delaySeconds,
            int sourceEntityId,
            int targetEntityId,
            int actionPlanId,
            int localActionIndex,
            MovementSemanticKind movementSemanticKind,
            float visualContactNormalizedTime,
            PresentationBarrierKey barrierKey)
        {
            Kind = kind;
            DelaySeconds = Math.Max(0f, delaySeconds);
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            ActionPlanId = actionPlanId;
            LocalActionIndex = localActionIndex;
            MovementSemanticKind = movementSemanticKind;
            VisualContactNormalizedTime = ClampNormalized(visualContactNormalizedTime);
            BarrierKey = barrierKey;
        }

        public PresentationTimingKind Kind { get; }

        public float DelaySeconds { get; }

        public int SourceEntityId { get; }

        public int TargetEntityId { get; }

        public int ActionPlanId { get; }

        public int LocalActionIndex { get; }

        public MovementSemanticKind MovementSemanticKind { get; }

        public float VisualContactNormalizedTime { get; }

        public PresentationBarrierKey BarrierKey { get; }

        public bool IsImmediate => Kind == PresentationTimingKind.Immediate;

        public static PresentationTimingAnchor Immediate()
        {
            return default;
        }

        public static PresentationTimingAnchor Delayed(
            float delaySeconds,
            PresentationBarrierKey barrierKey = default)
        {
            return delaySeconds > 0f
                ? new PresentationTimingAnchor(
                    PresentationTimingKind.DelaySeconds,
                    delaySeconds,
                    0,
                    0,
                    0,
                    0,
                    MovementSemanticKind.None,
                    0f,
                    barrierKey)
                : default;
        }

        public static PresentationTimingAnchor MotionContact(
            int sourceEntityId,
            int targetEntityId,
            int actionPlanId,
            int localActionIndex,
            MovementSemanticKind movementSemanticKind,
            float visualContactNormalizedTime,
            PresentationBarrierKey barrierKey = default)
        {
            return new PresentationTimingAnchor(
                PresentationTimingKind.MotionContact,
                0f,
                sourceEntityId,
                targetEntityId,
                actionPlanId,
                localActionIndex,
                movementSemanticKind,
                visualContactNormalizedTime,
                barrierKey);
        }

        public bool Equals(PresentationTimingAnchor other)
        {
            return Kind == other.Kind &&
                   DelaySeconds.Equals(other.DelaySeconds) &&
                   SourceEntityId == other.SourceEntityId &&
                   TargetEntityId == other.TargetEntityId &&
                   ActionPlanId == other.ActionPlanId &&
                   LocalActionIndex == other.LocalActionIndex &&
                   MovementSemanticKind == other.MovementSemanticKind &&
                   VisualContactNormalizedTime.Equals(other.VisualContactNormalizedTime) &&
                   BarrierKey.Equals(other.BarrierKey);
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationTimingAnchor other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                hash = (hash * 397) ^ DelaySeconds.GetHashCode();
                hash = (hash * 397) ^ SourceEntityId;
                hash = (hash * 397) ^ TargetEntityId;
                hash = (hash * 397) ^ ActionPlanId;
                hash = (hash * 397) ^ LocalActionIndex;
                hash = (hash * 397) ^ (int)MovementSemanticKind;
                hash = (hash * 397) ^ VisualContactNormalizedTime.GetHashCode();
                hash = (hash * 397) ^ BarrierKey.GetHashCode();
                return hash;
            }
        }

        private static float ClampNormalized(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            return value >= 1f ? 1f : value;
        }
    }

    public static class PresentationTimingResolver
    {
        public static float ResolveDelaySeconds(
            PresentationTimingAnchor anchor,
            GameplayTimingProfile timingProfile)
        {
            switch (anchor.Kind)
            {
                case PresentationTimingKind.DelaySeconds:
                    return Math.Max(0f, anchor.DelaySeconds);
                case PresentationTimingKind.MotionContact:
                    return ResolveMotionDurationSeconds(anchor.MovementSemanticKind, timingProfile) *
                           anchor.VisualContactNormalizedTime;
                case PresentationTimingKind.Immediate:
                case PresentationTimingKind.MotionEnd:
                case PresentationTimingKind.Barrier:
                default:
                    return 0f;
            }
        }

        private static float ResolveMotionDurationSeconds(
            MovementSemanticKind movementSemanticKind,
            GameplayTimingProfile timingProfile)
        {
            var profile = timingProfile ?? GameplayTimingProfile.CreateDefault();
            switch (movementSemanticKind)
            {
                case MovementSemanticKind.Flip:
                    return profile.FlipMotionDurationSeconds;
                case MovementSemanticKind.Push:
                    return profile.PushMotionDurationSeconds;
                case MovementSemanticKind.Move:
                case MovementSemanticKind.Item:
                    return profile.MoveMotionDurationSeconds;
                case MovementSemanticKind.Slide:
                    return profile.BoxSlideStepIntervalSeconds;
                case MovementSemanticKind.ProjectileMove:
                    return profile.ProjectileStepIntervalSeconds;
                default:
                    return 0f;
            }
        }
    }

    public readonly struct PresentationVisibilityGate : IEquatable<PresentationVisibilityGate>
    {
        public PresentationVisibilityGate(
            PresentationTimingAnchor timingAnchor,
            PresentationBarrierKey barrierKey = default)
        {
            TimingAnchor = timingAnchor;
            BarrierKey = barrierKey.IsValid ? barrierKey : timingAnchor.BarrierKey;
        }

        public PresentationTimingAnchor TimingAnchor { get; }

        public PresentationBarrierKey BarrierKey { get; }

        public bool HasGate => BarrierKey.IsValid && !TimingAnchor.IsImmediate;

        public static PresentationVisibilityGate Immediate()
        {
            return default;
        }

        public bool Equals(PresentationVisibilityGate other)
        {
            return TimingAnchor.Equals(other.TimingAnchor) &&
                   BarrierKey.Equals(other.BarrierKey);
        }

        public override bool Equals(object obj)
        {
            return obj is PresentationVisibilityGate other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (TimingAnchor.GetHashCode() * 397) ^ BarrierKey.GetHashCode();
            }
        }
    }

    public enum MoonBlockGeneratorBlockedReason
    {
        None = 0,
        UnitOccupant = 1,
        WallLikeSolid = 2,
        PlacementBlocked = 3,
    }

    public readonly struct MoonBlockGeneratorBlockedPayload
    {
        public MoonBlockGeneratorBlockedPayload(
            MoonBlockGeneratorBlockedReason reason,
            int blockingEntityId,
            SurfaceCell blockedCell)
        {
            Reason = reason;
            BlockingEntityId = blockingEntityId;
            BlockedCell = blockedCell;
        }

        public static MoonBlockGeneratorBlockedPayload None => default;

        public MoonBlockGeneratorBlockedReason Reason { get; }

        public int BlockingEntityId { get; }

        public SurfaceCell BlockedCell { get; }

        public bool IsValid => Reason != MoonBlockGeneratorBlockedReason.None;
    }

    public readonly struct TilePresentationEvent
    {
        public TilePresentationEvent(
            TilePresentationEventKind eventKind,
            int tileId,
            SurfaceCell cell,
            TileFeatureKind tileFeatureKind,
            int sourceEntityId,
            int ownerEntityId,
            int teamId,
            int targetEntityId = 0,
            Direction direction = Direction.None,
            MoonBlockGeneratorBlockedPayload moonBlockGeneratorBlockedPayload = default,
            int spawnTick = 0,
            int spawnInteractionLockTicks = 0,
            PresentationTimingAnchor timingAnchor = default,
            PresentationBarrierKey barrierKey = default)
        {
            EventKind = eventKind;
            TileId = tileId;
            Cell = cell;
            TileFeatureKind = tileFeatureKind;
            SourceEntityId = sourceEntityId;
            OwnerEntityId = ownerEntityId;
            TeamId = teamId;
            TargetEntityId = targetEntityId;
            Direction = direction;
            MoonBlockGeneratorBlockedPayload = moonBlockGeneratorBlockedPayload;
            SpawnTick = spawnTick;
            SpawnInteractionLockTicks = spawnInteractionLockTicks;
            TimingAnchor = timingAnchor;
            BarrierKey = barrierKey.IsValid ? barrierKey : timingAnchor.BarrierKey;
        }

        public TilePresentationEventKind EventKind { get; }

        public int TileId { get; }

        public SurfaceCell Cell { get; }

        public TileFeatureKind TileFeatureKind { get; }

        public int SourceEntityId { get; }

        public int OwnerEntityId { get; }

        public int TeamId { get; }

        public int TargetEntityId { get; }

        public Direction Direction { get; }

        public MoonBlockGeneratorBlockedPayload MoonBlockGeneratorBlockedPayload { get; }

        public int SpawnTick { get; }

        public int SpawnInteractionLockTicks { get; }

        public PresentationTimingAnchor TimingAnchor { get; }

        public PresentationBarrierKey BarrierKey { get; }
    }

    public readonly struct GravityFieldLockedBoxPayload
    {
        public GravityFieldLockedBoxPayload(
            int emitterEntityId,
            int targetEntityId,
            SurfaceCell emitterCell,
            SurfaceCell targetCell)
        {
            EmitterEntityId = emitterEntityId;
            TargetEntityId = targetEntityId;
            EmitterCell = emitterCell;
            TargetCell = targetCell;
        }

        public static GravityFieldLockedBoxPayload None => default;

        public int EmitterEntityId { get; }

        public int TargetEntityId { get; }

        public SurfaceCell EmitterCell { get; }

        public SurfaceCell TargetCell { get; }

        public bool IsValid => EmitterEntityId > 0 && TargetEntityId > 0;
    }

    public readonly struct TileFeatureVisualState
    {
        public TileFeatureVisualState(
            int tileId,
            SurfaceCell cell,
            TileFeatureKind tileFeatureKind,
            bool isActive,
            int sourceEntityId,
            int ownerEntityId,
            int teamId,
            PresentationVisibilityGate visibilityGate = default)
        {
            TileId = tileId;
            Cell = cell;
            TileFeatureKind = tileFeatureKind;
            IsActive = isActive;
            SourceEntityId = sourceEntityId;
            OwnerEntityId = ownerEntityId;
            TeamId = teamId;
            VisibilityGate = visibilityGate;
        }

        public int TileId { get; }

        public SurfaceCell Cell { get; }

        public TileFeatureKind TileFeatureKind { get; }

        public bool IsActive { get; }

        public int SourceEntityId { get; }

        public int OwnerEntityId { get; }

        public int TeamId { get; }

        public PresentationVisibilityGate VisibilityGate { get; }
    }

    public enum GravityFieldPresentationEventKind
    {
        None = 0,
        Activated = 1,
        Expired = 2,
        LockedBox = 3,
    }

    public readonly struct GravityFieldPresentationEvent
    {
        public GravityFieldPresentationEvent(
            GravityFieldPresentationEventKind eventKind,
            int emitterEntityId,
            SurfaceCell cell,
            int targetEntityId = 0,
            GravityFieldLockedBoxPayload lockedBoxPayload = default)
        {
            EventKind = eventKind;
            EmitterEntityId = emitterEntityId;
            Cell = cell;
            LockedBoxPayload = lockedBoxPayload;
            TargetEntityId = targetEntityId > 0
                ? targetEntityId
                : lockedBoxPayload.TargetEntityId;
        }

        public GravityFieldPresentationEventKind EventKind { get; }

        public int EmitterEntityId { get; }

        public SurfaceCell Cell { get; }

        public int TargetEntityId { get; }

        public GravityFieldLockedBoxPayload LockedBoxPayload { get; }
    }

    public readonly struct GravityFieldAreaFootprint
    {
        public const int SlotCount = 9;

        public static readonly GravityFieldAreaFootprint Empty = new(
            Array.Empty<SurfaceCell>(),
            slotVisibilityMask: 0);

        private static readonly IReadOnlyList<SurfaceCell> EmptyAreaCells =
            new ReadOnlyCollection<SurfaceCell>(new List<SurfaceCell>());

        private readonly IReadOnlyList<SurfaceCell> _areaCells;

        public GravityFieldAreaFootprint(
            IEnumerable<SurfaceCell> areaCells,
            int slotVisibilityMask)
        {
            _areaCells = new ReadOnlyCollection<SurfaceCell>(
                new List<SurfaceCell>(areaCells ?? Array.Empty<SurfaceCell>()));
            SlotVisibilityMask = slotVisibilityMask & 0x1FF;
        }

        public IReadOnlyList<SurfaceCell> AreaCells => _areaCells ?? EmptyAreaCells;

        public int SlotVisibilityMask { get; }

        public bool IsSlotVisible(int slotIndex)
        {
            return slotIndex >= 0 &&
                   slotIndex < SlotCount &&
                   (SlotVisibilityMask & (1 << slotIndex)) != 0;
        }
    }

    public readonly struct GravityFieldVisualState
    {
        private static readonly IReadOnlyList<int> EmptyLockedTargetEntityIds =
            new ReadOnlyCollection<int>(new List<int>());

        private readonly IReadOnlyList<int> _lockedTargetEntityIds;

        public GravityFieldVisualState(
            int emitterEntityId,
            SurfaceCell cell,
            GravityFieldPhase phase,
            int timerTicks,
            int durationTicks,
            float progress01)
            : this(
                emitterEntityId,
                cell,
                phase,
                timerTicks,
                durationTicks,
                progress01,
                GravityFieldAreaFootprint.Empty,
                Array.Empty<int>())
        {
        }

        public GravityFieldVisualState(
            int emitterEntityId,
            SurfaceCell cell,
            GravityFieldPhase phase,
            int timerTicks,
            int durationTicks,
            float progress01,
            IEnumerable<int> lockedTargetEntityIds)
            : this(
                emitterEntityId,
                cell,
                phase,
                timerTicks,
                durationTicks,
                progress01,
                GravityFieldAreaFootprint.Empty,
                lockedTargetEntityIds)
        {
        }

        public GravityFieldVisualState(
            int emitterEntityId,
            SurfaceCell cell,
            GravityFieldPhase phase,
            int timerTicks,
            int durationTicks,
            float progress01,
            GravityFieldAreaFootprint areaFootprint)
            : this(
                emitterEntityId,
                cell,
                phase,
                timerTicks,
                durationTicks,
                progress01,
                areaFootprint,
                Array.Empty<int>())
        {
        }

        public GravityFieldVisualState(
            int emitterEntityId,
            SurfaceCell cell,
            GravityFieldPhase phase,
            int timerTicks,
            int durationTicks,
            float progress01,
            GravityFieldAreaFootprint areaFootprint,
            IEnumerable<int> lockedTargetEntityIds)
        {
            EmitterEntityId = emitterEntityId;
            Cell = cell;
            Phase = phase;
            TimerTicks = timerTicks;
            DurationTicks = durationTicks;
            Progress01 = Clamp01(progress01);
            AreaFootprint = areaFootprint;
            _lockedTargetEntityIds = BuildLockedTargetEntityIds(phase, lockedTargetEntityIds);
        }

        public int EmitterEntityId { get; }

        public SurfaceCell Cell { get; }

        public GravityFieldPhase Phase { get; }

        public int TimerTicks { get; }

        public int DurationTicks { get; }

        public float Progress01 { get; }

        public GravityFieldAreaFootprint AreaFootprint { get; }

        public IReadOnlyList<SurfaceCell> AreaCells => AreaFootprint.AreaCells;

        public IReadOnlyList<int> LockedTargetEntityIds => _lockedTargetEntityIds ?? EmptyLockedTargetEntityIds;

        private static float Clamp01(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            return value >= 1f ? 1f : value;
        }

        private static IReadOnlyList<int> BuildLockedTargetEntityIds(
            GravityFieldPhase phase,
            IEnumerable<int> lockedTargetEntityIds)
        {
            if (phase != GravityFieldPhase.Active)
            {
                return EmptyLockedTargetEntityIds;
            }

            var ids = new List<int>();
            foreach (var targetEntityId in lockedTargetEntityIds ?? Array.Empty<int>())
            {
                if (targetEntityId > 0 && !ids.Contains(targetEntityId))
                {
                    ids.Add(targetEntityId);
                }
            }

            if (ids.Count == 0)
            {
                return EmptyLockedTargetEntityIds;
            }

            ids.Sort();
            return new ReadOnlyCollection<int>(ids);
        }
    }

    public readonly struct TileFeatureActiveVisualState
    {
        public TileFeatureActiveVisualState(
            int tileId,
            SurfaceCell cell,
            TileFeatureKind tileFeatureKind,
            int sourceEntityId,
            int ownerEntityId,
            int teamId)
        {
            TileId = tileId;
            Cell = cell;
            TileFeatureKind = tileFeatureKind;
            SourceEntityId = sourceEntityId;
            OwnerEntityId = ownerEntityId;
            TeamId = teamId;
        }

        public int TileId { get; }

        public SurfaceCell Cell { get; }

        public TileFeatureKind TileFeatureKind { get; }

        public int SourceEntityId { get; }

        public int OwnerEntityId { get; }

        public int TeamId { get; }
    }

    public readonly struct TickEntityMotion
    {
        // Motion records describe a render transition between already-committed logical cells.
        public TickEntityMotion(
            int entityId,
            TickEntityMotionKind motionKind,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell)
            : this(
                entityId,
                motionKind,
                sourceCell,
                destinationCell,
                sourceTopology: null,
                destinationTopology: null,
                sourceFacing: null,
                destinationFacing: null)
        {
        }

        public TickEntityMotion(
            int entityId,
            TickEntityMotionKind motionKind,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell,
            CubeTopologyState? sourceTopology,
            CubeTopologyState? destinationTopology,
            Direction? sourceFacing,
            Direction? destinationFacing)
        {
            EntityId = entityId;
            MotionKind = motionKind;
            SourceCell = sourceCell;
            DestinationCell = destinationCell;
            SourceTopology = sourceTopology;
            DestinationTopology = destinationTopology;
            SourceFacing = sourceFacing;
            DestinationFacing = destinationFacing;
        }

        public int EntityId { get; }

        public TickEntityMotionKind MotionKind { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell DestinationCell { get; }

        public CubeTopologyState? SourceTopology { get; }

        public CubeTopologyState? DestinationTopology { get; }

        public Direction? SourceFacing { get; }

        public Direction? DestinationFacing { get; }
    }

    public enum TickKinematicMotionTerminalKind
    {
        None = 0,
        Interrupted = 1,
        Removed = 2,
    }

    public readonly struct TickKinematicMotionTrack
    {
        public TickKinematicMotionTrack(
            int entityId,
            SurfaceCell sourceAnchorCell,
            KinematicOffset2 sourceLocalOffset,
            SurfaceCell destinationAnchorCell,
            KinematicOffset2 destinationLocalOffset,
            MotionMode motionMode,
            ForcedMotionOp forcedMotionOp,
            TickKinematicMotionTerminalKind terminalKind = TickKinematicMotionTerminalKind.None,
            int startedTick = 0,
            int elapsedTicks = 0,
            int totalTicks = 0)
            : this(
                entityId,
                sourceAnchorCell,
                sourceLocalOffset,
                destinationAnchorCell,
                destinationLocalOffset,
                motionMode,
                forcedMotionOp,
                EntityType.Unit,
                sourceTopology: null,
                destinationTopology: null,
                sourceFacing: null,
                destinationFacing: null,
                terminalKind: terminalKind,
                startedTick: startedTick,
                elapsedTicks: elapsedTicks,
                totalTicks: totalTicks)
        {
        }

        public TickKinematicMotionTrack(
            int entityId,
            SurfaceCell sourceAnchorCell,
            KinematicOffset2 sourceLocalOffset,
            SurfaceCell destinationAnchorCell,
            KinematicOffset2 destinationLocalOffset,
            MotionMode motionMode,
            ForcedMotionOp forcedMotionOp,
            EntityType entityType,
            CubeTopologyState? sourceTopology,
            CubeTopologyState? destinationTopology,
            Direction? sourceFacing,
            Direction? destinationFacing,
            TickKinematicMotionTerminalKind terminalKind = TickKinematicMotionTerminalKind.None,
            int startedTick = 0,
            int elapsedTicks = 0,
            int totalTicks = 0)
        {
            EntityId = entityId;
            SourceAnchorCell = sourceAnchorCell;
            SourceLocalOffset = sourceLocalOffset;
            DestinationAnchorCell = destinationAnchorCell;
            DestinationLocalOffset = destinationLocalOffset;
            MotionMode = motionMode;
            ForcedMotionOp = forcedMotionOp;
            EntityType = entityType;
            SourceTopology = sourceTopology;
            DestinationTopology = destinationTopology;
            SourceFacing = sourceFacing;
            DestinationFacing = destinationFacing;
            TerminalKind = terminalKind;
            StartedTick = Math.Max(0, startedTick);
            ElapsedTicks = Math.Max(0, elapsedTicks);
            TotalTicks = Math.Max(0, totalTicks);
        }

        public int EntityId { get; }

        public SurfaceCell SourceAnchorCell { get; }

        public KinematicOffset2 SourceLocalOffset { get; }

        public SurfaceCell DestinationAnchorCell { get; }

        public KinematicOffset2 DestinationLocalOffset { get; }

        public MotionMode MotionMode { get; }

        public ForcedMotionOp ForcedMotionOp { get; }

        public EntityType EntityType { get; }

        public CubeTopologyState? SourceTopology { get; }

        public CubeTopologyState? DestinationTopology { get; }

        public Direction? SourceFacing { get; }

        public Direction? DestinationFacing { get; }

        public TickKinematicMotionTerminalKind TerminalKind { get; }

        public int StartedTick { get; }

        public int ElapsedTicks { get; }

        public int TotalTicks { get; }
    }

    public readonly struct TickContinuousLocomotionTrack
    {
        public TickContinuousLocomotionTrack(
            int entityId,
            SurfaceCell sourceAnchorCell,
            KinematicOffset2 sourceLocalOffset,
            SurfaceCell destinationAnchorCell,
            KinematicOffset2 destinationLocalOffset,
            Direction sourceFacing,
            Direction destinationFacing,
            ContinuousLocomotionMode mode,
            TickKinematicMotionTerminalKind terminalKind = TickKinematicMotionTerminalKind.None)
            : this(
                entityId,
                sourceAnchorCell,
                sourceLocalOffset,
                destinationAnchorCell,
                destinationLocalOffset,
                sourceFacing,
                destinationFacing,
                mode,
                terminalKind,
                sourceTopology: null,
                destinationTopology: null,
                topologyRotationKind: CubeRotationKind.None,
                topologyTransitionReason: null)
        {
        }

        public TickContinuousLocomotionTrack(
            int entityId,
            SurfaceCell sourceAnchorCell,
            KinematicOffset2 sourceLocalOffset,
            SurfaceCell destinationAnchorCell,
            KinematicOffset2 destinationLocalOffset,
            Direction sourceFacing,
            Direction destinationFacing,
            ContinuousLocomotionMode mode,
            TickKinematicMotionTerminalKind terminalKind,
            CubeTopologyState? sourceTopology,
            CubeTopologyState? destinationTopology,
            CubeRotationKind topologyRotationKind,
            string topologyTransitionReason)
        {
            EntityId = entityId;
            SourceAnchorCell = sourceAnchorCell;
            SourceLocalOffset = sourceLocalOffset;
            DestinationAnchorCell = destinationAnchorCell;
            DestinationLocalOffset = destinationLocalOffset;
            SourceFacing = sourceFacing;
            DestinationFacing = destinationFacing;
            Mode = mode;
            TerminalKind = terminalKind;
            SourceTopology = sourceTopology;
            DestinationTopology = destinationTopology;
            TopologyRotationKind = topologyRotationKind;
            TopologyTransitionReason = topologyTransitionReason ?? string.Empty;
        }

        public int EntityId { get; }

        public SurfaceCell SourceAnchorCell { get; }

        public KinematicOffset2 SourceLocalOffset { get; }

        public SurfaceCell DestinationAnchorCell { get; }

        public KinematicOffset2 DestinationLocalOffset { get; }

        public Direction SourceFacing { get; }

        public Direction DestinationFacing { get; }

        public ContinuousLocomotionMode Mode { get; }

        public TickKinematicMotionTerminalKind TerminalKind { get; }

        public CubeTopologyState? SourceTopology { get; }

        public CubeTopologyState? DestinationTopology { get; }

        public CubeRotationKind TopologyRotationKind { get; }

        public string TopologyTransitionReason { get; }
    }

    public readonly struct TickTopologyMotion
    {
        public TickTopologyMotion(
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            CubeRotationKind rotationKind)
        {
            SourceTopology = sourceTopology;
            DestinationTopology = destinationTopology;
            RotationKind = rotationKind;
        }

        public CubeTopologyState SourceTopology { get; }

        public CubeTopologyState DestinationTopology { get; }

        public CubeRotationKind RotationKind { get; }
    }

    public enum TickTraversalBlockerKind
    {
        None = 0,
        BoardEdge = 1,
        Terrain = 2,
        Solid = 3,
        Unit = 4,
        Reservation = 5,
        TileFeature = 6,
    }

    public readonly struct TickPlayerTopologyTransitionBlockedSignal
    {
        public TickPlayerTopologyTransitionBlockedSignal(
            int entityId,
            Direction direction,
            SurfaceCell originCell,
            SurfaceCell candidateCell,
            CubeTopologyState sourceTopology,
            CubeTopologyState requiredTopology,
            CubeRotationKind rotationKind,
            TickTraversalBlockerKind primaryBlockerKind)
        {
            EntityId = entityId;
            Direction = direction;
            OriginCell = originCell;
            CandidateCell = candidateCell;
            SourceTopology = sourceTopology;
            RequiredTopology = requiredTopology;
            RotationKind = rotationKind;
            PrimaryBlockerKind = primaryBlockerKind;
        }

        public int EntityId { get; }

        public Direction Direction { get; }

        public SurfaceCell OriginCell { get; }

        public SurfaceCell CandidateCell { get; }

        public CubeTopologyState SourceTopology { get; }

        public CubeTopologyState RequiredTopology { get; }

        public CubeRotationKind RotationKind { get; }

        public TickTraversalBlockerKind PrimaryBlockerKind { get; }
    }

    public readonly struct TickVisibilityChange
    {
        public TickVisibilityChange(
            int entityId,
            TickVisibilityChangeKind changeKind,
            SurfaceCell cell,
            CubeTopologyState topology,
            Direction facing)
        {
            EntityId = entityId;
            ChangeKind = changeKind;
            Cell = cell;
            Topology = topology;
            Facing = facing;
        }

        public int EntityId { get; }

        public TickVisibilityChangeKind ChangeKind { get; }

        public SurfaceCell Cell { get; }

        public CubeTopologyState Topology { get; }

        public Direction Facing { get; }
    }

    public readonly struct TickTransitionVisibilityChange
    {
        public TickTransitionVisibilityChange(
            int entityId,
            TickTransitionVisibilityMode mode,
            SurfaceCell cell,
            CubeTopologyState topology,
            Direction facing)
        {
            EntityId = entityId;
            Mode = mode;
            Cell = cell;
            Topology = topology;
            Facing = facing;
        }

        public int EntityId { get; }

        public TickTransitionVisibilityMode Mode { get; }

        public SurfaceCell Cell { get; }

        public CubeTopologyState Topology { get; }

        public Direction Facing { get; }
    }

    public readonly struct TickSummonedEnemyPresentationBinding
    {
        public TickSummonedEnemyPresentationBinding(
            int entityId,
            bool hasEnemyDefinitionBinding,
            EnemyUnitArchetypeId archetypeId,
            int sourceEntityId = 0)
        {
            EntityId = entityId;
            HasEnemyDefinitionBinding = hasEnemyDefinitionBinding;
            ArchetypeId = archetypeId;
            SourceEntityId = sourceEntityId;
        }

        public int EntityId { get; }

        public bool HasEnemyDefinitionBinding { get; }

        public EnemyUnitArchetypeId ArchetypeId { get; }

        public int SourceEntityId { get; }
    }

    public readonly struct TickSummonWindupWarningSignal
    {
        public TickSummonWindupWarningSignal(
            int sourceEntityId,
            int effectIndex,
            SurfaceCell sourceCell,
            CubeTopologyState topology,
            Direction facing,
            int windupStartTick,
            int windupEndTick,
            int activationSequence,
            int tickIndex,
            int presentationSeed)
        {
            SourceEntityId = sourceEntityId;
            EffectIndex = effectIndex;
            SourceCell = sourceCell;
            Topology = topology;
            Facing = facing;
            WindupStartTick = windupStartTick;
            WindupEndTick = windupEndTick;
            ActivationSequence = activationSequence;
            TickIndex = tickIndex;
            PresentationSeed = presentationSeed;
        }

        public int SourceEntityId { get; }

        public int EffectIndex { get; }

        public SurfaceCell SourceCell { get; }

        public CubeTopologyState Topology { get; }

        public Direction Facing { get; }

        public int WindupStartTick { get; }

        public int WindupEndTick { get; }

        public int ActivationSequence { get; }

        public int TickIndex { get; }

        public int PresentationSeed { get; }
    }

    public enum FrontFaceShieldBlockMovementKind
    {
        Unknown = 0,
        PushStart = 1,
        SlidingContinuation = 2,
    }

    public readonly struct TickFrontFaceShieldSourceSignal
    {
        public TickFrontFaceShieldSourceSignal(
            int sourceEntityId,
            SurfaceCell sourceCell,
            CubeTopologyState topology,
            int radius,
            bool includeSourceCell,
            FrontFaceShieldTargetPattern targetPattern,
            int tickIndex,
            int presentationSeed)
        {
            SourceEntityId = sourceEntityId;
            SourceCell = sourceCell;
            Topology = topology;
            Radius = radius;
            IncludeSourceCell = includeSourceCell;
            TargetPattern = targetPattern;
            TickIndex = tickIndex;
            PresentationSeed = presentationSeed;
        }

        public int SourceEntityId { get; }

        public SurfaceCell SourceCell { get; }

        public CubeTopologyState Topology { get; }

        public int Radius { get; }

        public bool IncludeSourceCell { get; }

        public FrontFaceShieldTargetPattern TargetPattern { get; }

        public int TickIndex { get; }

        public int PresentationSeed { get; }
    }

    public readonly struct TickFrontFaceShieldWindupWarningSignal
    {
        public TickFrontFaceShieldWindupWarningSignal(
            int sourceEntityId,
            int effectIndex,
            SurfaceCell sourceCell,
            CubeTopologyState topology,
            int radius,
            bool includeSourceCell,
            FrontFaceShieldTargetPattern targetPattern,
            int windupStartTick,
            int windupEndTick,
            int activationSequence,
            int tickIndex,
            int presentationSeed)
        {
            SourceEntityId = sourceEntityId;
            EffectIndex = effectIndex;
            SourceCell = sourceCell;
            Topology = topology;
            Radius = radius;
            IncludeSourceCell = includeSourceCell;
            TargetPattern = targetPattern;
            WindupStartTick = windupStartTick;
            WindupEndTick = windupEndTick;
            ActivationSequence = activationSequence;
            TickIndex = tickIndex;
            PresentationSeed = presentationSeed;
        }

        public int SourceEntityId { get; }

        public int EffectIndex { get; }

        public SurfaceCell SourceCell { get; }

        public CubeTopologyState Topology { get; }

        public int Radius { get; }

        public bool IncludeSourceCell { get; }

        public FrontFaceShieldTargetPattern TargetPattern { get; }

        public int WindupStartTick { get; }

        public int WindupEndTick { get; }

        public int ActivationSequence { get; }

        public int TickIndex { get; }

        public int PresentationSeed { get; }
    }

    public readonly struct TickFrontFaceShieldBlockSignal
    {
        public TickFrontFaceShieldBlockSignal(
            int shieldSourceEntityId,
            int boxEntityId,
            int actorEntityId,
            SurfaceCell blockedCell,
            SurfaceCell shieldSourceCell,
            FrontFaceShieldBlockMovementKind movementKind,
            CubeTopologyState topology,
            int tickIndex,
            int presentationSeed)
        {
            ShieldSourceEntityId = shieldSourceEntityId;
            BoxEntityId = boxEntityId;
            ActorEntityId = actorEntityId;
            BlockedCell = blockedCell;
            ShieldSourceCell = shieldSourceCell;
            MovementKind = movementKind;
            Topology = topology;
            TickIndex = tickIndex;
            PresentationSeed = presentationSeed;
        }

        public int ShieldSourceEntityId { get; }

        public int BoxEntityId { get; }

        public int ActorEntityId { get; }

        public SurfaceCell BlockedCell { get; }

        public SurfaceCell ShieldSourceCell { get; }

        public FrontFaceShieldBlockMovementKind MovementKind { get; }

        public CubeTopologyState Topology { get; }

        public int TickIndex { get; }

        public int PresentationSeed { get; }
    }

    public readonly struct TickPlayerActionPresentationSignal
    {
        public TickPlayerActionPresentationSignal(
            int entityId,
            PlayerActionKind activeActionKind,
            int activeActionSequence,
            bool startedThisTick,
            bool completedThisTick,
            bool canceledThisTick,
            bool executedThisTick = false,
            bool isRecoveryPhase = false,
            TickPlayerActionResolutionKind resolutionKind = TickPlayerActionResolutionKind.None,
            int targetEntityId = 0,
            Direction direction = Direction.None,
            int actionPlanId = 0,
            TickPlayerFlipOutcomeKind flipOutcome = TickPlayerFlipOutcomeKind.None,
            bool hasFlipImpactContactTiming = false,
            int flipTargetBoxEntityId = 0)
        {
            EntityId = entityId;
            ActiveActionKind = activeActionKind;
            ActiveActionSequence = activeActionSequence;
            StartedThisTick = startedThisTick;
            ExecutedThisTick = executedThisTick;
            IsRecoveryPhase = isRecoveryPhase;
            ResolutionKind = resolutionKind;
            CompletedThisTick = completedThisTick;
            CanceledThisTick = canceledThisTick;
            TargetEntityId = targetEntityId;
            Direction = direction;
            ActionPlanId = actionPlanId;
            FlipOutcome = flipOutcome;
            HasFlipImpactContactTiming = hasFlipImpactContactTiming;
            FlipTargetBoxEntityId = flipTargetBoxEntityId;
        }

        public int EntityId { get; }

        public PlayerActionKind ActiveActionKind { get; }

        public int ActiveActionSequence { get; }

        public bool StartedThisTick { get; }

        public bool ExecutedThisTick { get; }

        public bool IsRecoveryPhase { get; }

        public TickPlayerActionResolutionKind ResolutionKind { get; }

        public bool CompletedThisTick { get; }

        public bool CanceledThisTick { get; }

        public int TargetEntityId { get; }

        public Direction Direction { get; }

        public int ActionPlanId { get; }

        public TickPlayerFlipOutcomeKind FlipOutcome { get; }

        public bool HasFlipImpactContactTiming { get; }

        public int FlipTargetBoxEntityId { get; }
    }

    public enum PlayerActionAttemptFeedbackKind
    {
        None = 0,
        AssistOutOfRange = 1,
        NoTarget = 2,
        Invalid = 3,
    }

    public readonly struct TickPlayerActionAttemptPresentationSignal
    {
        public TickPlayerActionAttemptPresentationSignal(
            int entityId,
            PlayerActionKind actionKind,
            Direction direction,
            PlayerActionAttemptFeedbackKind feedbackKind,
            int targetEntityId = 0,
            bool hasTarget = false,
            bool emitsVisualFeedback = true)
        {
            EntityId = entityId;
            ActionKind = actionKind;
            Direction = direction;
            FeedbackKind = feedbackKind;
            TargetEntityId = targetEntityId;
            HasTarget = hasTarget && targetEntityId > 0;
            EmitsVisualFeedback = emitsVisualFeedback;
        }

        public int EntityId { get; }

        public PlayerActionKind ActionKind { get; }

        public Direction Direction { get; }

        public PlayerActionAttemptFeedbackKind FeedbackKind { get; }

        public int TargetEntityId { get; }

        public bool HasTarget { get; }

        public bool EmitsVisualFeedback { get; }
    }

    public enum TickPlayerActionResolutionKind
    {
        None = 0,
        Success = 1,
        Impact = 2,
        Blocked = 3,
    }

    public enum TickPlayerFlipOutcomeKind
    {
        None = 0,
        FollowThrough = 1,
        DestroySelf = 2,
        Stay = 3,
        Blocked = 4,
    }

    public readonly struct TickPlayerLocomotionPresentationSignal
    {
        public TickPlayerLocomotionPresentationSignal(
            int entityId,
            bool shouldPlayWalkLoop,
            bool moveMotionGeneratedThisTick,
            bool waitingForNextMoveCadence,
            Direction inputDirection = Direction.None,
            bool inputIsBuffered = false)
        {
            EntityId = entityId;
            ShouldPlayWalkLoop = shouldPlayWalkLoop;
            MoveMotionGeneratedThisTick = moveMotionGeneratedThisTick;
            WaitingForNextMoveCadence = waitingForNextMoveCadence;
            InputDirection = inputDirection;
            InputIsBuffered = inputIsBuffered;
        }

        public int EntityId { get; }

        public bool ShouldPlayWalkLoop { get; }

        public bool MoveMotionGeneratedThisTick { get; }

        public bool WaitingForNextMoveCadence { get; }

        public Direction InputDirection { get; }

        public bool InputIsBuffered { get; }
    }

    public readonly struct TickPlayerDamagePresentationSignal
    {
        public TickPlayerDamagePresentationSignal(
            int entityId,
            bool tookDamageThisTick,
            int damageAmount)
        {
            EntityId = entityId;
            TookDamageThisTick = tookDamageThisTick;
            DamageAmount = damageAmount;
        }

        public int EntityId { get; }

        public bool TookDamageThisTick { get; }

        public int DamageAmount { get; }
    }

    public enum DeathDirectionHintKind
    {
        AttackerReverse = 1,
        FacingReverse = 2,
        Unknown = 3,
    }

    public readonly struct TickPlayerDeathPresentationSignal
    {
        public TickPlayerDeathPresentationSignal(
            int entityId,
            bool didDieThisTick,
            int sourceEntityId,
            Direction fallbackFacing,
            bool resolvedDamageSourceAvailable,
            int damageAmountAtFatalHit,
            DeathDirectionHintKind deathDirectionHintKind)
        {
            EntityId = entityId;
            DidDieThisTick = didDieThisTick;
            SourceEntityId = sourceEntityId;
            FallbackFacing = fallbackFacing;
            ResolvedDamageSourceAvailable = resolvedDamageSourceAvailable;
            DamageAmountAtFatalHit = damageAmountAtFatalHit;
            DeathDirectionHintKind = deathDirectionHintKind;
        }

        public int EntityId { get; }

        public bool DidDieThisTick { get; }

        public int SourceEntityId { get; }

        public Direction FallbackFacing { get; }

        public bool ResolvedDamageSourceAvailable { get; }

        public int DamageAmountAtFatalHit { get; }

        public DeathDirectionHintKind DeathDirectionHintKind { get; }
    }

    public readonly struct TickPlayerDeathHoldPresentationSignal
    {
        public TickPlayerDeathHoldPresentationSignal(
            int entityId,
            int startTick,
            int eligibleTick,
            int remainingTicks,
            bool startedThisTick)
        {
            EntityId = entityId;
            StartTick = startTick;
            EligibleTick = eligibleTick;
            RemainingTicks = Math.Max(0, remainingTicks);
            StartedThisTick = startedThisTick;
        }

        public int EntityId { get; }

        public int StartTick { get; }

        public int EligibleTick { get; }

        public int RemainingTicks { get; }

        public bool StartedThisTick { get; }
    }

    public readonly struct TickEnemyDamagePresentationSignal
    {
        public TickEnemyDamagePresentationSignal(
            int entityId,
            bool tookDamageThisTick,
            int damageAmount)
        {
            EntityId = entityId;
            TookDamageThisTick = tookDamageThisTick;
            DamageAmount = damageAmount;
        }

        public int EntityId { get; }

        public bool TookDamageThisTick { get; }

        public int DamageAmount { get; }
    }

    public enum EnemyActionPresentationSource
    {
        Unknown = 0,
        Combat = 1,
        PassiveContact = 2,
        ForwardCellImpact = 3,
    }

    public enum EnemyActionPresentationOutcome
    {
        None = 0,
        Executed = 1,
        RejectedByReceiverCooldown = 2,
        RejectedByPlayerInvincible = 3,
        NoEffect = 4,
    }

    public readonly struct TickEnemyActionPresentationSignal
    {
        public TickEnemyActionPresentationSignal(
            int entityId,
            EnemyActionKind activeActionKind,
            int activeActionSequence,
            bool startedThisTick,
            bool canceledThisTick,
            bool executedThisTick,
            bool startedRecoveryThisTick,
            EnemyActionPresentationSource presentationSource = EnemyActionPresentationSource.Unknown,
            EnemyActionPresentationOutcome presentationOutcome = EnemyActionPresentationOutcome.None)
        {
            EntityId = entityId;
            ActiveActionKind = activeActionKind;
            ActiveActionSequence = activeActionSequence;
            StartedThisTick = startedThisTick;
            CanceledThisTick = canceledThisTick;
            ExecutedThisTick = executedThisTick;
            StartedRecoveryThisTick = startedRecoveryThisTick;
            PresentationSource = presentationSource;
            PresentationOutcome = presentationOutcome;
        }

        public int EntityId { get; }

        public EnemyActionKind ActiveActionKind { get; }

        public int ActiveActionSequence { get; }

        public bool StartedThisTick { get; }

        public bool CanceledThisTick { get; }

        public bool ExecutedThisTick { get; }

        public bool StartedRecoveryThisTick { get; }

        public EnemyActionPresentationSource PresentationSource { get; }

        public EnemyActionPresentationOutcome PresentationOutcome { get; }
    }

    public readonly struct TickForwardCellImpactPresentationSignal
    {
        public TickForwardCellImpactPresentationSignal(
            int impactId,
            int presentationKey,
            int ownerId,
            int sourceEnemyId,
            SurfaceCell targetCell,
            Direction direction,
            bool hit,
            int targetEntityId)
        {
            ImpactId = impactId;
            PresentationKey = presentationKey != 0 ? presentationKey : impactId;
            OwnerId = ownerId;
            SourceEnemyId = sourceEnemyId;
            TargetCell = targetCell;
            Direction = direction;
            Hit = hit;
            TargetEntityId = targetEntityId;
        }

        public TickForwardCellImpactPresentationSignal(
            int impactId,
            int ownerId,
            int sourceEnemyId,
            SurfaceCell targetCell,
            Direction direction,
            bool hit,
            int targetEntityId)
            : this(
                impactId,
                impactId,
                ownerId,
                sourceEnemyId,
                targetCell,
                direction,
                hit,
                targetEntityId)
        {
        }

        public int ImpactId { get; }

        public int PresentationKey { get; }

        public int OwnerId { get; }

        public int SourceEnemyId { get; }

        public SurfaceCell TargetCell { get; }

        public Direction Direction { get; }

        public bool Hit { get; }

        public int TargetEntityId { get; }
    }

    public readonly struct TickForwardCellProjectileArrivalPresentationSignal
    {
        public TickForwardCellProjectileArrivalPresentationSignal(
            int impactId,
            int presentationKey,
            int ownerId,
            int sourceEnemyId,
            SurfaceCell targetCell,
            Direction direction,
            int impactTick,
            PendingCellImpactResolutionKind resolutionKind,
            int targetEntityId)
        {
            ImpactId = impactId;
            PresentationKey = presentationKey != 0 ? presentationKey : impactId;
            OwnerId = ownerId;
            SourceEnemyId = sourceEnemyId;
            TargetCell = targetCell;
            Direction = direction;
            ImpactTick = impactTick;
            ResolutionKind = resolutionKind;
            TargetEntityId = targetEntityId;
        }

        public int ImpactId { get; }

        public int PresentationKey { get; }

        public int OwnerId { get; }

        public int SourceEnemyId { get; }

        public SurfaceCell TargetCell { get; }

        public Direction Direction { get; }

        public int ImpactTick { get; }

        public PendingCellImpactResolutionKind ResolutionKind { get; }

        public int TargetEntityId { get; }
    }

    public readonly struct TickForwardCellProjectileWindupPresentationSignal
    {
        public TickForwardCellProjectileWindupPresentationSignal(
            int presentationKey,
            int ownerId,
            int sourceEnemyId,
            SurfaceCell targetCell,
            Direction direction,
            int startedTick,
            int expectedReleaseTick,
            int expectedImpactTick = 0)
        {
            PresentationKey = presentationKey;
            OwnerId = ownerId;
            SourceEnemyId = sourceEnemyId;
            TargetCell = targetCell;
            Direction = direction;
            StartedTick = startedTick;
            ExpectedReleaseTick = expectedReleaseTick;
            ExpectedImpactTick = expectedImpactTick;
        }

        public int PresentationKey { get; }

        public int OwnerId { get; }

        public int SourceEnemyId { get; }

        public SurfaceCell TargetCell { get; }

        public Direction Direction { get; }

        public int StartedTick { get; }

        public int ExpectedReleaseTick { get; }

        public int ExpectedImpactTick { get; }
    }

    public readonly struct TickForwardCellProjectileReleasePresentationSignal
    {
        public TickForwardCellProjectileReleasePresentationSignal(
            int presentationKey,
            int impactId,
            int ownerId,
            int sourceEnemyId,
            SurfaceCell sourceCell,
            SurfaceCell targetCell,
            Direction direction,
            int releaseTick,
            int impactTick,
            int impactDelayTicks)
        {
            PresentationKey = presentationKey != 0 ? presentationKey : impactId;
            ImpactId = impactId;
            OwnerId = ownerId;
            SourceEnemyId = sourceEnemyId;
            SourceCell = sourceCell;
            TargetCell = targetCell;
            Direction = direction;
            ReleaseTick = releaseTick;
            ImpactTick = impactTick;
            ImpactDelayTicks = Math.Max(0, impactDelayTicks);
        }

        public int PresentationKey { get; }

        public int ImpactId { get; }

        public int OwnerId { get; }

        public int SourceEnemyId { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell TargetCell { get; }

        public Direction Direction { get; }

        public int ReleaseTick { get; }

        public int ImpactTick { get; }

        public int ImpactDelayTicks { get; }
    }

    public enum ForwardCellProjectileClearReason
    {
        None = 0,
        Canceled = 1,
        SourceExited = 2,
        Cleanup = 3,
    }

    public readonly struct TickForwardCellProjectileClearPresentationSignal
    {
        public TickForwardCellProjectileClearPresentationSignal(
            int presentationKey,
            int ownerId,
            int sourceEnemyId,
            SurfaceCell targetCell,
            Direction direction,
            int startedTick,
            ForwardCellProjectileClearReason reason)
        {
            PresentationKey = presentationKey;
            OwnerId = ownerId;
            SourceEnemyId = sourceEnemyId;
            TargetCell = targetCell;
            Direction = direction;
            StartedTick = startedTick;
            Reason = reason;
        }

        public int PresentationKey { get; }

        public int OwnerId { get; }

        public int SourceEnemyId { get; }

        public SurfaceCell TargetCell { get; }

        public Direction Direction { get; }

        public int StartedTick { get; }

        public ForwardCellProjectileClearReason Reason { get; }
    }

    public enum EnemyUtilityPresentationKind
    {
        None = 0,
        LockNearbyBoxes = 1,
        GravityFieldAura = 2,
        SummonMinion = 3,
    }

    public enum EnemyUtilityPresentationPhase
    {
        None = 0,
        WindupStarted = 1,
        RecoverStarted = 2,
        ActiveStarted = 3,
        AttackStarted = 4,
        Canceled = 5,
    }

    public readonly struct TickEnemyUtilityPresentationSignal
    {
        public TickEnemyUtilityPresentationSignal(
            int entityId,
            EnemyUtilityPresentationKind kind,
            EnemyUtilityPresentationPhase phase,
            int startTick,
            int executeTick,
            int durationTicks,
            int effectIndex = 0,
            int activationSequence = 0)
        {
            EntityId = entityId;
            Kind = kind;
            Phase = phase;
            StartTick = startTick;
            ExecuteTick = executeTick;
            DurationTicks = Math.Max(0, durationTicks);
            EffectIndex = effectIndex;
            ActivationSequence = Math.Max(0, activationSequence);
        }

        public int EntityId { get; }

        public EnemyUtilityPresentationKind Kind { get; }

        public EnemyUtilityPresentationPhase Phase { get; }

        public int StartTick { get; }

        public int ExecuteTick { get; }

        public int DurationTicks { get; }

        public int EffectIndex { get; }

        public int ActivationSequence { get; }
    }

    public readonly struct TickEnemyUtilityPhasePresentationState
    {
        public TickEnemyUtilityPhasePresentationState(
            int entityId,
            EnemyUtilityPresentationKind kind,
            EnemyUtilityEffectPhase phase,
            int phaseElapsedTicks,
            int phaseDurationTicks,
            int effectIndex = 0,
            int activationSequence = 0)
        {
            EntityId = entityId;
            Kind = kind;
            Phase = phase;
            PhaseElapsedTicks = Math.Max(0, phaseElapsedTicks);
            PhaseDurationTicks = Math.Max(0, phaseDurationTicks);
            EffectIndex = Math.Max(0, effectIndex);
            ActivationSequence = Math.Max(0, activationSequence);
        }

        public int EntityId { get; }

        public EnemyUtilityPresentationKind Kind { get; }

        public EnemyUtilityEffectPhase Phase { get; }

        public int PhaseElapsedTicks { get; }

        public int PhaseDurationTicks { get; }

        public int EffectIndex { get; }

        public int ActivationSequence { get; }
    }

    public readonly struct TickEnemyUtilityCooldownPresentationSignal
    {
        public TickEnemyUtilityCooldownPresentationSignal(
            int entityId,
            EnemyUtilityPresentationKind kind,
            int cooldownTicksRemaining,
            int cooldownTicksTotal,
            int effectIndex = 0,
            int activationSequence = 0)
        {
            EntityId = entityId;
            Kind = kind;
            CooldownTicksRemaining = Math.Max(0, cooldownTicksRemaining);
            CooldownTicksTotal = Math.Max(CooldownTicksRemaining, cooldownTicksTotal);
            EffectIndex = effectIndex;
            ActivationSequence = Math.Max(0, activationSequence);
        }

        public int EntityId { get; }

        public EnemyUtilityPresentationKind Kind { get; }

        public int CooldownTicksRemaining { get; }

        public int CooldownTicksTotal { get; }

        public int EffectIndex { get; }

        public int ActivationSequence { get; }
    }

    public readonly struct TickEnemyGravityFieldAuraVisualState
    {
        public TickEnemyGravityFieldAuraVisualState(
            int entityId,
            SurfaceCell cell,
            EnemyUtilityEffectPhase phase,
            int radius,
            int timerTicks,
            int durationTicks,
            float progress01,
            int effectIndex,
            int activationSequence,
            GravityFieldAreaFootprint areaFootprint,
            bool startedThisTick)
        {
            EntityId = entityId;
            Cell = cell;
            Phase = phase;
            Radius = Math.Max(0, radius);
            TimerTicks = Math.Max(0, timerTicks);
            DurationTicks = Math.Max(TimerTicks, durationTicks);
            Progress01 = Clamp01(progress01);
            EffectIndex = Math.Max(0, effectIndex);
            ActivationSequence = Math.Max(0, activationSequence);
            AreaFootprint = areaFootprint;
            StartedThisTick = startedThisTick;
        }

        public int EntityId { get; }

        public SurfaceCell Cell { get; }

        public EnemyUtilityEffectPhase Phase { get; }

        public int Radius { get; }

        public int TimerTicks { get; }

        public int DurationTicks { get; }

        public float Progress01 { get; }

        public int EffectIndex { get; }

        public int ActivationSequence { get; }

        public GravityFieldAreaFootprint AreaFootprint { get; }

        public IReadOnlyList<SurfaceCell> AreaCells => AreaFootprint.AreaCells;

        public bool StartedThisTick { get; }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            return value >= 1f ? 1f : value;
        }
    }

    public enum TickEnemyJumpPresentationOutcome
    {
        None = 0,
        WindupStarted = 1,
        AirborneStarted = 2,
        Landed = 3,
        Retried = 4,
        CrushedBoxAndLanded = 5,
    }

    public readonly struct TickEnemyJumpPresentationSignal
    {
        public TickEnemyJumpPresentationSignal(
            int entityId,
            int sequence,
            EnemyJumpPhase phase,
            bool startedWindupThisTick,
            bool startedAirborneThisTick,
            bool landedThisTick,
            bool retryThisTick,
            SurfaceCell sourceCell = default,
            SurfaceCell lockedTargetCell = default,
            SurfaceCell presentationTargetCell = default,
            Direction facing = Direction.None,
            int windupTicks = 0,
            int landingTick = 0,
            int remainingAirborneTicks = 0,
            int retryCount = 0,
            TickEnemyJumpPresentationOutcome outcome = TickEnemyJumpPresentationOutcome.None)
        {
            var resolvedOutcome = outcome == TickEnemyJumpPresentationOutcome.None
                ? ResolveOutcome(startedWindupThisTick, startedAirborneThisTick, landedThisTick, retryThisTick)
                : outcome;
            EntityId = entityId;
            Sequence = sequence;
            Phase = phase;
            StartedWindupThisTick = startedWindupThisTick;
            StartedAirborneThisTick = startedAirborneThisTick;
            LandedThisTick = landedThisTick || resolvedOutcome == TickEnemyJumpPresentationOutcome.CrushedBoxAndLanded;
            RetryThisTick = retryThisTick || resolvedOutcome == TickEnemyJumpPresentationOutcome.Retried;
            SourceCell = sourceCell;
            LockedTargetCell = lockedTargetCell;
            PresentationTargetCell = presentationTargetCell;
            Facing = facing;
            WindupTicks = Math.Max(0, windupTicks);
            LandingTick = landingTick;
            RemainingAirborneTicks = remainingAirborneTicks;
            RetryCount = retryCount;
            Outcome = resolvedOutcome;
        }

        public int EntityId { get; }

        public int Sequence { get; }

        public EnemyJumpPhase Phase { get; }

        public bool StartedWindupThisTick { get; }

        public bool StartedAirborneThisTick { get; }

        public bool LandedThisTick { get; }

        public bool RetryThisTick { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell LockedTargetCell { get; }

        public SurfaceCell PresentationTargetCell { get; }

        public Direction Facing { get; }

        public int WindupTicks { get; }

        public int LandingTick { get; }

        public int RemainingAirborneTicks { get; }

        public int RetryCount { get; }

        public TickEnemyJumpPresentationOutcome Outcome { get; }

        private static TickEnemyJumpPresentationOutcome ResolveOutcome(
            bool startedWindupThisTick,
            bool startedAirborneThisTick,
            bool landedThisTick,
            bool retryThisTick)
        {
            if (startedWindupThisTick)
            {
                return TickEnemyJumpPresentationOutcome.WindupStarted;
            }

            if (startedAirborneThisTick)
            {
                return TickEnemyJumpPresentationOutcome.AirborneStarted;
            }

            if (landedThisTick)
            {
                return TickEnemyJumpPresentationOutcome.Landed;
            }

            return retryThisTick
                ? TickEnemyJumpPresentationOutcome.Retried
                : TickEnemyJumpPresentationOutcome.None;
        }
    }

    public readonly struct TickEnemyChargePresentationSignal
    {
        public TickEnemyChargePresentationSignal(
            int entityId,
            int sequence,
            EnemyChargePhase phase,
            bool startedWindupThisTick,
            bool startedActiveThisTick,
            bool startedRecoverThisTick,
            Direction lockedDirection = Direction.None)
        {
            EntityId = entityId;
            Sequence = sequence;
            Phase = phase;
            StartedWindupThisTick = startedWindupThisTick;
            StartedActiveThisTick = startedActiveThisTick;
            StartedRecoverThisTick = startedRecoverThisTick;
            LockedDirection = lockedDirection;
        }

        public int EntityId { get; }

        public int Sequence { get; }

        public EnemyChargePhase Phase { get; }

        public bool StartedWindupThisTick { get; }

        public bool StartedActiveThisTick { get; }

        public bool StartedRecoverThisTick { get; }

        public Direction LockedDirection { get; }
    }

    public readonly struct TickEnemyGlidePresentationSignal
    {
        public TickEnemyGlidePresentationSignal(
            int entityId,
            SurfaceCell anchorCell,
            EnemyGlidePhase phase,
            int sequence,
            int phaseElapsedTicks,
            int phaseTotalTicks,
            float normalizedPhaseProgress,
            int liftHeightUnits,
            int recoveryDipHeightUnits,
            int currentHeightUnits,
            bool isAirborneVisual,
            bool wantsRecover,
            bool isTerminalZero)
        {
            EntityId = entityId;
            AnchorCell = anchorCell;
            Phase = phase;
            Sequence = sequence;
            PhaseElapsedTicks = Math.Max(0, phaseElapsedTicks);
            PhaseTotalTicks = Math.Max(0, phaseTotalTicks);
            NormalizedPhaseProgress = Math.Max(0f, Math.Min(1f, normalizedPhaseProgress));
            LiftHeightUnits = Math.Max(0, liftHeightUnits);
            RecoveryDipHeightUnits = Math.Max(0, recoveryDipHeightUnits);
            CurrentHeightUnits = currentHeightUnits;
            IsAirborneVisual = isAirborneVisual;
            WantsRecover = wantsRecover;
            IsTerminalZero = isTerminalZero;
        }

        public int EntityId { get; }

        public SurfaceCell AnchorCell { get; }

        public EnemyGlidePhase Phase { get; }

        public int Sequence { get; }

        public int PhaseElapsedTicks { get; }

        public int PhaseTotalTicks { get; }

        public float NormalizedPhaseProgress { get; }

        public int LiftHeightUnits { get; }

        public int RecoveryDipHeightUnits { get; }

        public int CurrentHeightUnits { get; }

        public bool IsAirborneVisual { get; }

        public bool WantsRecover { get; }

        public bool IsTerminalZero { get; }
    }

    public enum TickEntityExitCause
    {
        None = 0,
        ItemConsume = 1,
        BoxDestroy = 2,
        EnemyDeath = 3,
        OutOfBounds = 4,
        DestroyedByImpact = BoxDestroy,
        Killed = EnemyDeath,
    }

    public enum EntityExitPresentationTiming
    {
        Immediate = 0,
        AfterEntityMotion = 1,
        AtContactTime = 2,
        AfterAnimationTail = 3,
    }

    internal readonly struct EntityExitPresentationFact
    {
        public EntityExitPresentationFact(
            int entityId,
            TickEntityExitCause exitCause,
            EntityType entityType,
            SurfaceCell anchorCell,
            CubeTopologyState topology,
            Direction facing,
            int sourceActorEntityId,
            EntityExitPresentationTiming timing,
            string boundaryReason,
            bool hasExplicitAnchor,
            float visualContactNormalizedTime = 0f)
        {
            EntityId = entityId;
            ExitCause = exitCause;
            EntityType = entityType;
            AnchorCell = anchorCell;
            Topology = topology;
            Facing = facing;
            SourceActorEntityId = sourceActorEntityId;
            Timing = timing;
            BoundaryReason = boundaryReason ?? string.Empty;
            HasExplicitAnchor = hasExplicitAnchor;
            VisualContactNormalizedTime = ClampNormalized(visualContactNormalizedTime);
        }

        public int EntityId { get; }

        public TickEntityExitCause ExitCause { get; }

        public EntityType EntityType { get; }

        public SurfaceCell AnchorCell { get; }

        public CubeTopologyState Topology { get; }

        public Direction Facing { get; }

        public int SourceActorEntityId { get; }

        public EntityExitPresentationTiming Timing { get; }

        public string BoundaryReason { get; }

        public bool HasExplicitAnchor { get; }

        public float VisualContactNormalizedTime { get; }

        private static float ClampNormalized(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            return value >= 1f ? 1f : value;
        }
    }

    // Exit signals transfer visual ownership away from the authoritative entity view.
    // Once an exit is committed, the original entity view must not remain visible in
    // the scene just to support a lingering effect; any echo is transient-only.
    public readonly struct TickEntityExitPresentationSignal
    {
        public TickEntityExitPresentationSignal(
            int exitedEntityId,
            TickEntityExitCause exitCause,
            SurfaceCell sourceCell,
            CubeTopologyState topology,
            Direction facing,
            EntityType entityType,
            int? sourceActorEntityId = null,
            int? anchorEntityId = null,
            int presentationSeed = 0,
            EntityExitPresentationTiming timing = EntityExitPresentationTiming.Immediate,
            float visualContactNormalizedTime = 0f)
        {
            ExitedEntityId = exitedEntityId;
            ExitCause = exitCause;
            SourceCell = sourceCell;
            Topology = topology;
            Facing = facing;
            EntityType = entityType;
            SourceActorEntityId = sourceActorEntityId;
            AnchorEntityId = anchorEntityId;
            PresentationSeed = presentationSeed;
            Timing = timing;
            VisualContactNormalizedTime = ClampNormalized(visualContactNormalizedTime);
        }

        public int ExitedEntityId { get; }

        public TickEntityExitCause ExitCause { get; }

        public SurfaceCell SourceCell { get; }

        public CubeTopologyState Topology { get; }

        public Direction Facing { get; }

        public EntityType EntityType { get; }

        public int? SourceActorEntityId { get; }

        public int? AnchorEntityId { get; }

        public int PresentationSeed { get; }

        public EntityExitPresentationTiming Timing { get; }

        public float VisualContactNormalizedTime { get; }

        private static float ClampNormalized(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            return value >= 1f ? 1f : value;
        }
    }

    // Presentation-only transient for current Flip nonlethal destroy-self impact.
    // This signal is render metadata and must not be treated as gameplay truth.
    internal readonly struct TickImpactTransientPresentationSignal
    {
        public TickImpactTransientPresentationSignal(
            int entityId,
            EntityType entityType,
            SurfaceCell sourceCell,
            SurfaceCell impactCell,
            CubeTopologyState topology,
            Direction facing,
            int presentationSeed)
        {
            EntityId = entityId;
            EntityType = entityType;
            SourceCell = sourceCell;
            ImpactCell = impactCell;
            Topology = topology;
            Facing = facing;
            PresentationSeed = presentationSeed;
        }

        public int EntityId { get; }

        public EntityType EntityType { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell ImpactCell { get; }

        public CubeTopologyState Topology { get; }

        public Direction Facing { get; }

        public int PresentationSeed { get; }
    }

    public enum FlipImpactPresentationDisposition
    {
        DestroySelf = 1,
        Stay = 2,
    }

    public enum FlipFloorImpactPresentationKind
    {
        Landing = 1,
        FollowThrough = 2,
        Stay = 3,
        DestroySelf = 4,
    }

    public readonly struct FlipFloorImpactPresentationSignal
    {
        public FlipFloorImpactPresentationSignal(
            int sourceActionPlanId,
            int boxEntityId,
            int actorEntityId,
            SurfaceCell sourceCell,
            SurfaceCell contactCell,
            CubeTopologyState topology,
            Direction sourceFacing,
            Direction contactFacing,
            FlipFloorImpactPresentationKind kind,
            float visualContactNormalizedTime = GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime)
        {
            SourceActionPlanId = sourceActionPlanId;
            BoxEntityId = boxEntityId;
            ActorEntityId = actorEntityId;
            SourceCell = sourceCell;
            ContactCell = contactCell;
            Topology = topology;
            SourceFacing = sourceFacing;
            ContactFacing = contactFacing;
            Kind = kind;
            VisualContactNormalizedTime = ClampNormalized(visualContactNormalizedTime);
        }

        public int SourceActionPlanId { get; }

        public int BoxEntityId { get; }

        public int ActorEntityId { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell ContactCell { get; }

        public CubeTopologyState Topology { get; }

        public Direction SourceFacing { get; }

        public Direction ContactFacing { get; }

        public FlipFloorImpactPresentationKind Kind { get; }

        public float VisualContactNormalizedTime { get; }

        private static float ClampNormalized(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            return value >= 1f ? 1f : value;
        }
    }

    public readonly struct FlipImpactPresentationSignal
    {
        public FlipImpactPresentationSignal(
            int sourceActionPlanId,
            int boxEntityId,
            int impactTargetEntityId,
            int actorEntityId,
            SurfaceCell sourceCell,
            SurfaceCell impactCell,
            CubeTopologyState topology,
            Direction sourceFacing,
            Direction impactFacing,
            FlipImpactPresentationDisposition disposition,
            bool hasLandingCell = false,
            SurfaceCell landingCell = default)
        {
            SourceActionPlanId = sourceActionPlanId;
            BoxEntityId = boxEntityId;
            ImpactTargetEntityId = impactTargetEntityId;
            ActorEntityId = actorEntityId;
            SourceCell = sourceCell;
            ImpactCell = impactCell;
            Topology = topology;
            SourceFacing = sourceFacing;
            ImpactFacing = impactFacing;
            HasLandingCell = hasLandingCell;
            LandingCell = landingCell;
            Disposition = disposition;
        }

        public int SourceActionPlanId { get; }

        public int BoxEntityId { get; }

        public int ImpactTargetEntityId { get; }

        public int ActorEntityId { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell ImpactCell { get; }

        public CubeTopologyState Topology { get; }

        public Direction SourceFacing { get; }

        public Direction ImpactFacing { get; }

        public bool HasLandingCell { get; }

        public SurfaceCell LandingCell { get; }

        public FlipImpactPresentationDisposition Disposition { get; }
    }

    public readonly struct BoxSlideStopPresentationSignal
    {
        public BoxSlideStopPresentationSignal(
            int boxEntityId,
            SurfaceCell sourceCell,
            SurfaceCell stopperCell,
            Direction slideDirection,
            BoxSlideStopperKind stopperKind,
            int stopperEntityId,
            SolidKind solidKind,
            CubeTopologyState topology,
            BoxSlideStopCause cause,
            int stopperTileId = 0)
        {
            BoxEntityId = boxEntityId;
            SourceCell = sourceCell;
            StopperCell = stopperCell;
            SlideDirection = slideDirection;
            StopperKind = stopperKind;
            StopperEntityId = stopperEntityId;
            StopperTileId = stopperTileId;
            SolidKind = solidKind;
            Topology = topology;
            Cause = cause;
        }

        public int BoxEntityId { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell StopperCell { get; }

        public Direction SlideDirection { get; }

        public BoxSlideStopperKind StopperKind { get; }

        public int StopperEntityId { get; }

        public int StopperTileId { get; }

        public SolidKind SolidKind { get; }

        public CubeTopologyState Topology { get; }

        public BoxSlideStopCause Cause { get; }
    }

    public readonly struct BoxSlideStartPresentationSignal
    {
        public BoxSlideStartPresentationSignal(
            int boxEntityId,
            int actorEntityId,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell,
            CubeTopologyState topology)
        {
            BoxEntityId = boxEntityId;
            ActorEntityId = actorEntityId;
            SourceCell = sourceCell;
            DestinationCell = destinationCell;
            Topology = topology;
        }

        public int BoxEntityId { get; }

        public int ActorEntityId { get; }

        public SurfaceCell SourceCell { get; }

        public SurfaceCell DestinationCell { get; }

        public CubeTopologyState Topology { get; }
    }

    public readonly struct TickPlayerFlipResultTurnSignal
    {
        public TickPlayerFlipResultTurnSignal(
            int entityId,
            int actionSequence,
            Direction actionDirection,
            Direction contactFacing,
            Direction resultFacing,
            int startTick,
            PlayerFlipResultTurnStartReason reason)
        {
            EntityId = entityId;
            ActionSequence = actionSequence;
            ActionDirection = actionDirection;
            ContactFacing = contactFacing;
            ResultFacing = resultFacing;
            StartTick = startTick;
            Reason = reason;
        }

        public int EntityId { get; }

        public int ActionSequence { get; }

        public Direction ActionDirection { get; }

        public Direction ContactFacing { get; }

        public Direction ResultFacing { get; }

        public int StartTick { get; }

        public PlayerFlipResultTurnStartReason Reason { get; }
    }

    public sealed class TickPresentationData
    {
        public static readonly TickPresentationData Empty = new(
            Array.Empty<TickEntityMotion>(),
            topologyMotion: null,
            Array.Empty<TickVisibilityChange>(),
            Array.Empty<TickTransitionVisibilityChange>(),
            Array.Empty<TickPlayerActionPresentationSignal>(),
            Array.Empty<TickPlayerLocomotionPresentationSignal>(),
            Array.Empty<TickPlayerDamagePresentationSignal>(),
            Array.Empty<TickPlayerDeathPresentationSignal>(),
            Array.Empty<TickEnemyDamagePresentationSignal>(),
            Array.Empty<TickEnemyActionPresentationSignal>(),
            Array.Empty<TickEnemyJumpPresentationSignal>(),
            Array.Empty<TickEnemyChargePresentationSignal>(),
            Array.Empty<TickEntityExitPresentationSignal>(),
            Array.Empty<FlipImpactPresentationSignal>());

        private readonly ReadOnlyCollection<TickEntityExitPresentationSignal> _entityExitSignals;
        private readonly ReadOnlyCollection<BoxSlideStopPresentationSignal> _boxSlideStopSignals;
        private readonly ReadOnlyCollection<BoxSlideStartPresentationSignal> _boxSlideStartSignals;
        private ReadOnlyCollection<TickImpactTransientPresentationSignal> _impactTransientSignals;
        private readonly ReadOnlyCollection<FlipFloorImpactPresentationSignal> _flipFloorImpactSignals;
        private readonly ReadOnlyCollection<FlipImpactPresentationSignal> _flipImpactSignals;
        private readonly ReadOnlyCollection<TickEnemyActionPresentationSignal> _enemyActionSignals;
        private readonly ReadOnlyCollection<TickEnemyDamagePresentationSignal> _enemyDamageSignals;
        private readonly ReadOnlyCollection<TickEnemyJumpPresentationSignal> _enemyJumpSignals;
        private readonly ReadOnlyCollection<TickEnemyChargePresentationSignal> _enemyChargeSignals;
        private readonly ReadOnlyCollection<TickEnemyGlidePresentationSignal> _enemyGlideSignals;
        private readonly ReadOnlyCollection<TickEnemyUtilityPresentationSignal> _enemyUtilitySignals;
        private readonly ReadOnlyCollection<TickEnemyUtilityPhasePresentationState> _enemyUtilityPhaseStates;
        private readonly ReadOnlyCollection<TickEnemyUtilityCooldownPresentationSignal> _enemyUtilityCooldownSignals;
        private readonly ReadOnlyCollection<TickEnemyGravityFieldAuraVisualState> _enemyGravityFieldAuraVisualStates;
        private readonly ReadOnlyCollection<TickForwardCellProjectileWindupPresentationSignal> _forwardCellProjectileWindupSignals;
        private readonly ReadOnlyCollection<TickForwardCellProjectileReleasePresentationSignal> _forwardCellProjectileReleaseSignals;
        private readonly ReadOnlyCollection<TickForwardCellProjectileClearPresentationSignal> _forwardCellProjectileClearSignals;
        private readonly ReadOnlyCollection<TickForwardCellImpactPresentationSignal> _forwardCellImpactSignals;
        private readonly ReadOnlyCollection<TickForwardCellProjectileArrivalPresentationSignal> _forwardCellProjectileArrivalSignals;
        private readonly ReadOnlyCollection<TickEntityMotion> _entityMotions;
        private readonly ReadOnlyCollection<TickKinematicMotionTrack> _kinematicMotionTracks;
        private readonly ReadOnlyCollection<TickContinuousLocomotionTrack> _continuousLocomotionTracks;
        private readonly ReadOnlyCollection<EntitySpawnPresentationSignal> _entitySpawnSignals;
        private readonly ReadOnlyCollection<TilePresentationEvent> _tileEvents;
        private readonly ReadOnlyCollection<TileFeatureVisualState> _tileFeatureVisualStates;
        private readonly ReadOnlyCollection<TileFeatureVisualState> _tileFeatureVisibleVisualStates;
        private readonly ReadOnlyCollection<GravityFieldPresentationEvent> _gravityFieldEvents;
        private readonly ReadOnlyCollection<GravityFieldVisualState> _gravityFieldVisualStates;
        private readonly ReadOnlyCollection<TileFeatureActiveVisualState> _tileFeatureActiveVisualStates;
        private ReadOnlyCollection<TickFrontFaceShieldBlockSignal> _frontFaceShieldBlocks;
        private ReadOnlyCollection<TickFrontFaceShieldSourceSignal> _frontFaceShieldSources;
        private ReadOnlyCollection<TickFrontFaceShieldWindupWarningSignal> _frontFaceShieldWindupWarnings;
        private readonly ReadOnlyCollection<TickPlayerActionPresentationSignal> _playerActionSignals;
        private readonly ReadOnlyCollection<TickPlayerActionAttemptPresentationSignal> _playerActionAttemptSignals;
        private readonly ReadOnlyCollection<TickPlayerFlipResultTurnSignal> _playerFlipResultTurnSignals;
        private readonly ReadOnlyCollection<TickPlayerDamagePresentationSignal> _playerDamageSignals;
        private readonly ReadOnlyCollection<TickPlayerDeathHoldPresentationSignal> _playerDeathHoldSignals;
        private readonly ReadOnlyCollection<TickPlayerDeathPresentationSignal> _playerDeathSignals;
        private readonly ReadOnlyCollection<TickPlayerLocomotionPresentationSignal> _playerLocomotionSignals;
        private readonly ReadOnlyCollection<TickPlayerOutcomePresentationSignal> _playerOutcomeSignals;
        private readonly ReadOnlyCollection<TickPlayerTopologyTransitionBlockedSignal> _playerTopologyTransitionBlockedSignals;
        private ReadOnlyCollection<TickSummonedEnemyPresentationBinding> _summonedEnemyPresentationBindings;
        private ReadOnlyCollection<TickSummonWindupWarningSignal> _summonWindupWarnings;
        private readonly TickTopologyMotion? _topologyMotion;
        private readonly ReadOnlyCollection<TickTransitionVisibilityChange> _transitionVisibilityChanges;
        private readonly ReadOnlyCollection<TickVisibilityChange> _visibilityChanges;

        // Presentation data is render-only metadata layered on top of authoritative gameplay state.
        public TickPresentationData(IEnumerable<TickEntityMotion> entityMotions)
            : this(
                entityMotions,
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>())
        {
        }

        public TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                Array.Empty<TickTransitionVisibilityChange>())
        {
        }

        public TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                Array.Empty<TickPlayerActionPresentationSignal>())
        {
        }

        public TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                Array.Empty<TickEnemyActionPresentationSignal>())
        {
        }

        public TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals,
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>())
        {
        }

        public TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals,
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                entityExitSignals,
                Array.Empty<FlipImpactPresentationSignal>())
        {
        }

        public TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEnemyJumpPresentationSignal> enemyJumpSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals,
                enemyJumpSignals,
                entityExitSignals,
                Array.Empty<FlipImpactPresentationSignal>())
        {
        }

        public TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEnemyJumpPresentationSignal> enemyJumpSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                playerLocomotionSignals,
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals,
                enemyJumpSignals,
                entityExitSignals,
                Array.Empty<FlipImpactPresentationSignal>())
        {
        }

        public TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals,
            IEnumerable<TickPlayerDamagePresentationSignal> playerDamageSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEnemyJumpPresentationSignal> enemyJumpSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                playerLocomotionSignals,
                playerDamageSignals,
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals,
                enemyJumpSignals,
                entityExitSignals,
                Array.Empty<FlipImpactPresentationSignal>())
        {
        }

        internal TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals,
            IEnumerable<TickPlayerDamagePresentationSignal> playerDamageSignals,
            IEnumerable<TickPlayerDeathPresentationSignal> playerDeathSignals,
            IEnumerable<TickEnemyDamagePresentationSignal> enemyDamageSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEnemyJumpPresentationSignal> enemyJumpSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                playerLocomotionSignals,
                playerDamageSignals,
                playerDeathSignals,
                enemyDamageSignals,
                enemyActionSignals,
                enemyJumpSignals,
                entityExitSignals,
                Array.Empty<FlipImpactPresentationSignal>())
        {
        }

        internal TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals,
            IEnumerable<TickPlayerDamagePresentationSignal> playerDamageSignals,
            IEnumerable<TickEnemyDamagePresentationSignal> enemyDamageSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEnemyJumpPresentationSignal> enemyJumpSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals,
            IEnumerable<TickImpactTransientPresentationSignal> impactTransientSignals)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                playerLocomotionSignals,
                playerDamageSignals,
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals,
                enemyActionSignals,
                enemyJumpSignals,
                Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals,
                impactTransientSignals,
                Array.Empty<FlipImpactPresentationSignal>())
        {
        }

        internal TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals,
            IEnumerable<TickPlayerDamagePresentationSignal> playerDamageSignals,
            IEnumerable<TickEnemyDamagePresentationSignal> enemyDamageSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEnemyJumpPresentationSignal> enemyJumpSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals,
            IEnumerable<TickImpactTransientPresentationSignal> impactTransientSignals,
            IEnumerable<FlipImpactPresentationSignal> flipImpactSignals)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                playerLocomotionSignals,
                playerDamageSignals,
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals,
                enemyActionSignals,
                enemyJumpSignals,
                Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals,
                impactTransientSignals,
                flipImpactSignals)
        {
        }

        public TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals,
            IEnumerable<TickPlayerDamagePresentationSignal> playerDamageSignals,
            IEnumerable<TickEnemyDamagePresentationSignal> enemyDamageSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEnemyJumpPresentationSignal> enemyJumpSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals,
            IEnumerable<TickPlayerActionAttemptPresentationSignal> playerActionAttemptSignals = null,
            IEnumerable<TickPlayerOutcomePresentationSignal> playerOutcomeSignals = null)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                playerLocomotionSignals,
                playerDamageSignals,
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals,
                enemyActionSignals,
                enemyJumpSignals,
                Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals,
                Array.Empty<FlipImpactPresentationSignal>(),
                playerActionAttemptSignals: playerActionAttemptSignals,
                playerOutcomeSignals: playerOutcomeSignals)
        {
        }

        internal TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals,
            IEnumerable<TickPlayerDamagePresentationSignal> playerDamageSignals,
            IEnumerable<TickPlayerDeathPresentationSignal> playerDeathSignals,
            IEnumerable<TickEnemyDamagePresentationSignal> enemyDamageSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEnemyJumpPresentationSignal> enemyJumpSignals,
            IEnumerable<TickEnemyChargePresentationSignal> enemyChargeSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals,
            IEnumerable<FlipImpactPresentationSignal> flipImpactSignals,
            IEnumerable<TickFrontFaceShieldSourceSignal> frontFaceShieldSources = null,
            IEnumerable<TickFrontFaceShieldBlockSignal> frontFaceShieldBlocks = null,
            IEnumerable<TickSummonWindupWarningSignal> summonWindupWarnings = null,
            IEnumerable<TickFrontFaceShieldWindupWarningSignal> frontFaceShieldWindupWarnings = null,
            IEnumerable<TickKinematicMotionTrack> kinematicMotionTracks = null,
            IEnumerable<TickPlayerDeathHoldPresentationSignal> playerDeathHoldSignals = null,
            IEnumerable<TickContinuousLocomotionTrack> continuousLocomotionTracks = null,
            IEnumerable<TickEnemyGlidePresentationSignal> enemyGlideSignals = null,
            IEnumerable<TilePresentationEvent> tileEvents = null,
            IEnumerable<GravityFieldPresentationEvent> gravityFieldEvents = null,
            IEnumerable<GravityFieldVisualState> gravityFieldVisualStates = null,
            IEnumerable<TickPlayerActionAttemptPresentationSignal> playerActionAttemptSignals = null,
            IEnumerable<BoxSlideStopPresentationSignal> boxSlideStopSignals = null,
            IEnumerable<TickEnemyUtilityPresentationSignal> enemyUtilitySignals = null,
            IEnumerable<TickEnemyUtilityCooldownPresentationSignal> enemyUtilityCooldownSignals = null,
            IEnumerable<TickEnemyGravityFieldAuraVisualState> enemyGravityFieldAuraVisualStates = null,
            IEnumerable<BoxSlideStartPresentationSignal> boxSlideStartSignals = null,
            IEnumerable<TileFeatureVisualState> tileFeatureVisualStates = null,
            IEnumerable<TileFeatureVisualState> tileFeatureVisibleVisualStates = null,
            IEnumerable<TileFeatureActiveVisualState> tileFeatureActiveVisualStates = null,
            IEnumerable<TickPlayerFlipResultTurnSignal> playerFlipResultTurnSignals = null,
            IEnumerable<FlipFloorImpactPresentationSignal> flipFloorImpactSignals = null,
            IEnumerable<TickForwardCellImpactPresentationSignal> forwardCellImpactSignals = null,
            IEnumerable<TickForwardCellProjectileArrivalPresentationSignal> forwardCellProjectileArrivalSignals = null,
            IEnumerable<TickForwardCellProjectileWindupPresentationSignal> forwardCellProjectileWindupSignals = null,
            IEnumerable<TickForwardCellProjectileReleasePresentationSignal> forwardCellProjectileReleaseSignals = null,
            IEnumerable<TickForwardCellProjectileClearPresentationSignal> forwardCellProjectileClearSignals = null,
            IEnumerable<EntitySpawnPresentationSignal> entitySpawnSignals = null,
            IEnumerable<TickPlayerOutcomePresentationSignal> playerOutcomeSignals = null,
            IEnumerable<TickEnemyUtilityPhasePresentationState> enemyUtilityPhaseStates = null,
            IEnumerable<TickPlayerTopologyTransitionBlockedSignal> playerTopologyTransitionBlockedSignals = null)
        {
            if (entityMotions == null)
            {
                throw new ArgumentNullException(nameof(entityMotions));
            }

            if (visibilityChanges == null)
            {
                throw new ArgumentNullException(nameof(visibilityChanges));
            }

            if (transitionVisibilityChanges == null)
            {
                throw new ArgumentNullException(nameof(transitionVisibilityChanges));
            }

            if (playerActionSignals == null)
            {
                throw new ArgumentNullException(nameof(playerActionSignals));
            }

            if (enemyActionSignals == null)
            {
                throw new ArgumentNullException(nameof(enemyActionSignals));
            }

            if (playerLocomotionSignals == null)
            {
                throw new ArgumentNullException(nameof(playerLocomotionSignals));
            }

            if (playerDamageSignals == null)
            {
                throw new ArgumentNullException(nameof(playerDamageSignals));
            }

            if (playerDeathSignals == null)
            {
                throw new ArgumentNullException(nameof(playerDeathSignals));
            }

            if (enemyDamageSignals == null)
            {
                throw new ArgumentNullException(nameof(enemyDamageSignals));
            }

            if (enemyJumpSignals == null)
            {
                throw new ArgumentNullException(nameof(enemyJumpSignals));
            }

            if (enemyChargeSignals == null)
            {
                throw new ArgumentNullException(nameof(enemyChargeSignals));
            }

            if (entityExitSignals == null)
            {
                throw new ArgumentNullException(nameof(entityExitSignals));
            }

            if (flipImpactSignals == null)
            {
                throw new ArgumentNullException(nameof(flipImpactSignals));
            }

            _entityMotions = new ReadOnlyCollection<TickEntityMotion>(new List<TickEntityMotion>(entityMotions));
            _kinematicMotionTracks = new ReadOnlyCollection<TickKinematicMotionTrack>(
                new List<TickKinematicMotionTrack>(
                    kinematicMotionTracks ?? Array.Empty<TickKinematicMotionTrack>()));
            _continuousLocomotionTracks = new ReadOnlyCollection<TickContinuousLocomotionTrack>(
                new List<TickContinuousLocomotionTrack>(
                    continuousLocomotionTracks ?? Array.Empty<TickContinuousLocomotionTrack>()));
            _entitySpawnSignals = new ReadOnlyCollection<EntitySpawnPresentationSignal>(
                new List<EntitySpawnPresentationSignal>(
                    entitySpawnSignals ?? Array.Empty<EntitySpawnPresentationSignal>()));
            _tileEvents = new ReadOnlyCollection<TilePresentationEvent>(
                new List<TilePresentationEvent>(
                    tileEvents ?? Array.Empty<TilePresentationEvent>()));
            _tileFeatureVisualStates = new ReadOnlyCollection<TileFeatureVisualState>(
                new List<TileFeatureVisualState>(
                    tileFeatureVisualStates ?? Array.Empty<TileFeatureVisualState>()));
            _tileFeatureVisibleVisualStates = new ReadOnlyCollection<TileFeatureVisualState>(
                new List<TileFeatureVisualState>(
                    tileFeatureVisibleVisualStates ?? Array.Empty<TileFeatureVisualState>()));
            _gravityFieldEvents = new ReadOnlyCollection<GravityFieldPresentationEvent>(
                new List<GravityFieldPresentationEvent>(
                    gravityFieldEvents ?? Array.Empty<GravityFieldPresentationEvent>()));
            _gravityFieldVisualStates = new ReadOnlyCollection<GravityFieldVisualState>(
                new List<GravityFieldVisualState>(
                    gravityFieldVisualStates ?? Array.Empty<GravityFieldVisualState>()));
            _tileFeatureActiveVisualStates = new ReadOnlyCollection<TileFeatureActiveVisualState>(
                new List<TileFeatureActiveVisualState>(
                    tileFeatureActiveVisualStates ?? Array.Empty<TileFeatureActiveVisualState>()));
            _topologyMotion = topologyMotion;
            _visibilityChanges = new ReadOnlyCollection<TickVisibilityChange>(new List<TickVisibilityChange>(visibilityChanges));
            _transitionVisibilityChanges = new ReadOnlyCollection<TickTransitionVisibilityChange>(
                new List<TickTransitionVisibilityChange>(transitionVisibilityChanges));
            _playerActionSignals = new ReadOnlyCollection<TickPlayerActionPresentationSignal>(
                new List<TickPlayerActionPresentationSignal>(playerActionSignals));
            _playerActionAttemptSignals = new ReadOnlyCollection<TickPlayerActionAttemptPresentationSignal>(
                new List<TickPlayerActionAttemptPresentationSignal>(
                    playerActionAttemptSignals ?? Array.Empty<TickPlayerActionAttemptPresentationSignal>()));
            _playerFlipResultTurnSignals = new ReadOnlyCollection<TickPlayerFlipResultTurnSignal>(
                new List<TickPlayerFlipResultTurnSignal>(
                    playerFlipResultTurnSignals ?? Array.Empty<TickPlayerFlipResultTurnSignal>()));
            _playerLocomotionSignals = new ReadOnlyCollection<TickPlayerLocomotionPresentationSignal>(
                new List<TickPlayerLocomotionPresentationSignal>(playerLocomotionSignals));
            _playerDamageSignals = new ReadOnlyCollection<TickPlayerDamagePresentationSignal>(
                new List<TickPlayerDamagePresentationSignal>(playerDamageSignals));
            _playerDeathSignals = new ReadOnlyCollection<TickPlayerDeathPresentationSignal>(
                new List<TickPlayerDeathPresentationSignal>(playerDeathSignals));
            _playerDeathHoldSignals = new ReadOnlyCollection<TickPlayerDeathHoldPresentationSignal>(
                new List<TickPlayerDeathHoldPresentationSignal>(
                    playerDeathHoldSignals ?? Array.Empty<TickPlayerDeathHoldPresentationSignal>()));
            _playerOutcomeSignals = new ReadOnlyCollection<TickPlayerOutcomePresentationSignal>(
                new List<TickPlayerOutcomePresentationSignal>(
                    playerOutcomeSignals ?? Array.Empty<TickPlayerOutcomePresentationSignal>()));
            _playerTopologyTransitionBlockedSignals =
                new ReadOnlyCollection<TickPlayerTopologyTransitionBlockedSignal>(
                    new List<TickPlayerTopologyTransitionBlockedSignal>(
                        playerTopologyTransitionBlockedSignals ??
                        Array.Empty<TickPlayerTopologyTransitionBlockedSignal>()));
            _enemyDamageSignals = new ReadOnlyCollection<TickEnemyDamagePresentationSignal>(
                new List<TickEnemyDamagePresentationSignal>(enemyDamageSignals));
            _enemyActionSignals = new ReadOnlyCollection<TickEnemyActionPresentationSignal>(
                new List<TickEnemyActionPresentationSignal>(enemyActionSignals));
            _enemyJumpSignals = new ReadOnlyCollection<TickEnemyJumpPresentationSignal>(
                new List<TickEnemyJumpPresentationSignal>(enemyJumpSignals));
            _enemyChargeSignals = new ReadOnlyCollection<TickEnemyChargePresentationSignal>(
                new List<TickEnemyChargePresentationSignal>(enemyChargeSignals));
            _enemyGlideSignals = new ReadOnlyCollection<TickEnemyGlidePresentationSignal>(
                new List<TickEnemyGlidePresentationSignal>(
                    enemyGlideSignals ?? Array.Empty<TickEnemyGlidePresentationSignal>()));
            _enemyUtilitySignals = new ReadOnlyCollection<TickEnemyUtilityPresentationSignal>(
                new List<TickEnemyUtilityPresentationSignal>(
                    enemyUtilitySignals ?? Array.Empty<TickEnemyUtilityPresentationSignal>()));
            _enemyUtilityPhaseStates = new ReadOnlyCollection<TickEnemyUtilityPhasePresentationState>(
                new List<TickEnemyUtilityPhasePresentationState>(
                    enemyUtilityPhaseStates ?? Array.Empty<TickEnemyUtilityPhasePresentationState>()));
            _enemyUtilityCooldownSignals = new ReadOnlyCollection<TickEnemyUtilityCooldownPresentationSignal>(
                new List<TickEnemyUtilityCooldownPresentationSignal>(
                    enemyUtilityCooldownSignals ?? Array.Empty<TickEnemyUtilityCooldownPresentationSignal>()));
            _enemyGravityFieldAuraVisualStates = new ReadOnlyCollection<TickEnemyGravityFieldAuraVisualState>(
                new List<TickEnemyGravityFieldAuraVisualState>(
                    enemyGravityFieldAuraVisualStates ?? Array.Empty<TickEnemyGravityFieldAuraVisualState>()));
            _forwardCellProjectileWindupSignals =
                new ReadOnlyCollection<TickForwardCellProjectileWindupPresentationSignal>(
                    new List<TickForwardCellProjectileWindupPresentationSignal>(
                        forwardCellProjectileWindupSignals ??
                        Array.Empty<TickForwardCellProjectileWindupPresentationSignal>()));
            _forwardCellProjectileReleaseSignals =
                new ReadOnlyCollection<TickForwardCellProjectileReleasePresentationSignal>(
                    new List<TickForwardCellProjectileReleasePresentationSignal>(
                        forwardCellProjectileReleaseSignals ??
                        Array.Empty<TickForwardCellProjectileReleasePresentationSignal>()));
            _forwardCellProjectileClearSignals =
                new ReadOnlyCollection<TickForwardCellProjectileClearPresentationSignal>(
                    new List<TickForwardCellProjectileClearPresentationSignal>(
                        forwardCellProjectileClearSignals ??
                        Array.Empty<TickForwardCellProjectileClearPresentationSignal>()));
            _forwardCellImpactSignals = new ReadOnlyCollection<TickForwardCellImpactPresentationSignal>(
                new List<TickForwardCellImpactPresentationSignal>(
                    forwardCellImpactSignals ?? Array.Empty<TickForwardCellImpactPresentationSignal>()));
            _forwardCellProjectileArrivalSignals =
                new ReadOnlyCollection<TickForwardCellProjectileArrivalPresentationSignal>(
                    new List<TickForwardCellProjectileArrivalPresentationSignal>(
                        forwardCellProjectileArrivalSignals ??
                        Array.Empty<TickForwardCellProjectileArrivalPresentationSignal>()));
            _entityExitSignals = new ReadOnlyCollection<TickEntityExitPresentationSignal>(
                new List<TickEntityExitPresentationSignal>(entityExitSignals));
            _boxSlideStopSignals = new ReadOnlyCollection<BoxSlideStopPresentationSignal>(
                new List<BoxSlideStopPresentationSignal>(
                    boxSlideStopSignals ?? Array.Empty<BoxSlideStopPresentationSignal>()));
            _boxSlideStartSignals = new ReadOnlyCollection<BoxSlideStartPresentationSignal>(
                new List<BoxSlideStartPresentationSignal>(
                    boxSlideStartSignals ?? Array.Empty<BoxSlideStartPresentationSignal>()));
            _flipImpactSignals = new ReadOnlyCollection<FlipImpactPresentationSignal>(
                new List<FlipImpactPresentationSignal>(flipImpactSignals));
            _flipFloorImpactSignals = new ReadOnlyCollection<FlipFloorImpactPresentationSignal>(
                new List<FlipFloorImpactPresentationSignal>(
                    flipFloorImpactSignals ?? Array.Empty<FlipFloorImpactPresentationSignal>()));
            _impactTransientSignals = new ReadOnlyCollection<TickImpactTransientPresentationSignal>(
                new List<TickImpactTransientPresentationSignal>());
            _summonedEnemyPresentationBindings = new ReadOnlyCollection<TickSummonedEnemyPresentationBinding>(
                new List<TickSummonedEnemyPresentationBinding>());
            _frontFaceShieldSources = new ReadOnlyCollection<TickFrontFaceShieldSourceSignal>(
                new List<TickFrontFaceShieldSourceSignal>(
                    frontFaceShieldSources ?? Array.Empty<TickFrontFaceShieldSourceSignal>()));
            _frontFaceShieldBlocks = new ReadOnlyCollection<TickFrontFaceShieldBlockSignal>(
                new List<TickFrontFaceShieldBlockSignal>(
                    frontFaceShieldBlocks ?? Array.Empty<TickFrontFaceShieldBlockSignal>()));
            _summonWindupWarnings = new ReadOnlyCollection<TickSummonWindupWarningSignal>(
                new List<TickSummonWindupWarningSignal>(
                    summonWindupWarnings ?? Array.Empty<TickSummonWindupWarningSignal>()));
            _frontFaceShieldWindupWarnings = new ReadOnlyCollection<TickFrontFaceShieldWindupWarningSignal>(
                new List<TickFrontFaceShieldWindupWarningSignal>(
                    frontFaceShieldWindupWarnings ?? Array.Empty<TickFrontFaceShieldWindupWarningSignal>()));
        }

        public TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals,
            IEnumerable<TickPlayerDamagePresentationSignal> playerDamageSignals,
            IEnumerable<TickPlayerDeathPresentationSignal> playerDeathSignals,
            IEnumerable<TickEnemyDamagePresentationSignal> enemyDamageSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEnemyJumpPresentationSignal> enemyJumpSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals,
            IEnumerable<FlipImpactPresentationSignal> flipImpactSignals,
            IEnumerable<TilePresentationEvent> tileEvents = null,
            IEnumerable<GravityFieldPresentationEvent> gravityFieldEvents = null,
            IEnumerable<GravityFieldVisualState> gravityFieldVisualStates = null,
            IEnumerable<TickPlayerActionAttemptPresentationSignal> playerActionAttemptSignals = null,
            IEnumerable<BoxSlideStopPresentationSignal> boxSlideStopSignals = null,
            IEnumerable<TickEnemyUtilityPresentationSignal> enemyUtilitySignals = null,
            IEnumerable<TickEnemyUtilityCooldownPresentationSignal> enemyUtilityCooldownSignals = null,
            IEnumerable<TickEnemyGravityFieldAuraVisualState> enemyGravityFieldAuraVisualStates = null,
            IEnumerable<BoxSlideStartPresentationSignal> boxSlideStartSignals = null,
            IEnumerable<TileFeatureVisualState> tileFeatureVisualStates = null,
            IEnumerable<TileFeatureVisualState> tileFeatureVisibleVisualStates = null,
            IEnumerable<TileFeatureActiveVisualState> tileFeatureActiveVisualStates = null,
            IEnumerable<TickPlayerFlipResultTurnSignal> playerFlipResultTurnSignals = null,
            IEnumerable<FlipFloorImpactPresentationSignal> flipFloorImpactSignals = null,
            IEnumerable<TickForwardCellImpactPresentationSignal> forwardCellImpactSignals = null,
            IEnumerable<TickForwardCellProjectileArrivalPresentationSignal> forwardCellProjectileArrivalSignals = null,
            IEnumerable<TickForwardCellProjectileWindupPresentationSignal> forwardCellProjectileWindupSignals = null,
            IEnumerable<TickForwardCellProjectileReleasePresentationSignal> forwardCellProjectileReleaseSignals = null,
            IEnumerable<TickForwardCellProjectileClearPresentationSignal> forwardCellProjectileClearSignals = null,
            IEnumerable<EntitySpawnPresentationSignal> entitySpawnSignals = null,
            IEnumerable<TickPlayerOutcomePresentationSignal> playerOutcomeSignals = null,
            IEnumerable<TickEnemyUtilityPhasePresentationState> enemyUtilityPhaseStates = null,
            IEnumerable<TickPlayerTopologyTransitionBlockedSignal> playerTopologyTransitionBlockedSignals = null)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                playerLocomotionSignals,
                playerDamageSignals,
                playerDeathSignals,
                enemyDamageSignals,
                enemyActionSignals,
                enemyJumpSignals,
                Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals,
                flipImpactSignals,
                tileEvents: tileEvents,
                gravityFieldEvents: gravityFieldEvents,
                gravityFieldVisualStates: gravityFieldVisualStates,
                playerActionAttemptSignals: playerActionAttemptSignals,
                boxSlideStopSignals: boxSlideStopSignals,
                enemyUtilitySignals: enemyUtilitySignals,
                enemyUtilityCooldownSignals: enemyUtilityCooldownSignals,
                enemyGravityFieldAuraVisualStates: enemyGravityFieldAuraVisualStates,
                boxSlideStartSignals: boxSlideStartSignals,
                tileFeatureVisualStates: tileFeatureVisualStates,
                tileFeatureVisibleVisualStates: tileFeatureVisibleVisualStates,
                tileFeatureActiveVisualStates: tileFeatureActiveVisualStates,
                playerFlipResultTurnSignals: playerFlipResultTurnSignals,
                flipFloorImpactSignals: flipFloorImpactSignals,
                forwardCellImpactSignals: forwardCellImpactSignals,
                forwardCellProjectileArrivalSignals: forwardCellProjectileArrivalSignals,
                forwardCellProjectileWindupSignals: forwardCellProjectileWindupSignals,
                forwardCellProjectileReleaseSignals: forwardCellProjectileReleaseSignals,
                forwardCellProjectileClearSignals: forwardCellProjectileClearSignals,
                entitySpawnSignals: entitySpawnSignals,
                playerOutcomeSignals: playerOutcomeSignals,
                enemyUtilityPhaseStates: enemyUtilityPhaseStates,
                playerTopologyTransitionBlockedSignals: playerTopologyTransitionBlockedSignals)
        {
        }

        internal TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals,
            IEnumerable<TickPlayerDamagePresentationSignal> playerDamageSignals,
            IEnumerable<TickPlayerDeathPresentationSignal> playerDeathSignals,
            IEnumerable<TickEnemyDamagePresentationSignal> enemyDamageSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEnemyJumpPresentationSignal> enemyJumpSignals,
            IEnumerable<TickEnemyChargePresentationSignal> enemyChargeSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals,
            IEnumerable<TickImpactTransientPresentationSignal> impactTransientSignals)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                playerLocomotionSignals,
                playerDamageSignals,
                playerDeathSignals,
                enemyDamageSignals,
                enemyActionSignals,
                enemyJumpSignals,
                enemyChargeSignals,
                entityExitSignals,
                impactTransientSignals,
                Array.Empty<FlipImpactPresentationSignal>())
        {
        }

        internal TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals,
            IEnumerable<TickPlayerDamagePresentationSignal> playerDamageSignals,
            IEnumerable<TickPlayerDeathPresentationSignal> playerDeathSignals,
            IEnumerable<TickEnemyDamagePresentationSignal> enemyDamageSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEnemyJumpPresentationSignal> enemyJumpSignals,
            IEnumerable<TickEnemyChargePresentationSignal> enemyChargeSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals,
            IEnumerable<TickImpactTransientPresentationSignal> impactTransientSignals,
            IEnumerable<FlipImpactPresentationSignal> flipImpactSignals,
            IEnumerable<TickKinematicMotionTrack> kinematicMotionTracks = null,
            IEnumerable<TickPlayerDeathHoldPresentationSignal> playerDeathHoldSignals = null,
            IEnumerable<TickContinuousLocomotionTrack> continuousLocomotionTracks = null,
            IEnumerable<TickEnemyGlidePresentationSignal> enemyGlideSignals = null,
            IEnumerable<TilePresentationEvent> tileEvents = null,
            IEnumerable<GravityFieldPresentationEvent> gravityFieldEvents = null,
            IEnumerable<GravityFieldVisualState> gravityFieldVisualStates = null,
            IEnumerable<TickPlayerActionAttemptPresentationSignal> playerActionAttemptSignals = null,
            IEnumerable<BoxSlideStopPresentationSignal> boxSlideStopSignals = null,
            IEnumerable<TickEnemyUtilityPresentationSignal> enemyUtilitySignals = null,
            IEnumerable<TickEnemyUtilityCooldownPresentationSignal> enemyUtilityCooldownSignals = null,
            IEnumerable<TickEnemyGravityFieldAuraVisualState> enemyGravityFieldAuraVisualStates = null,
            IEnumerable<BoxSlideStartPresentationSignal> boxSlideStartSignals = null,
            IEnumerable<TileFeatureVisualState> tileFeatureVisualStates = null,
            IEnumerable<TileFeatureVisualState> tileFeatureVisibleVisualStates = null,
            IEnumerable<TileFeatureActiveVisualState> tileFeatureActiveVisualStates = null,
            IEnumerable<TickPlayerFlipResultTurnSignal> playerFlipResultTurnSignals = null,
            IEnumerable<FlipFloorImpactPresentationSignal> flipFloorImpactSignals = null,
            IEnumerable<TickForwardCellImpactPresentationSignal> forwardCellImpactSignals = null,
            IEnumerable<TickForwardCellProjectileArrivalPresentationSignal> forwardCellProjectileArrivalSignals = null,
            IEnumerable<TickForwardCellProjectileWindupPresentationSignal> forwardCellProjectileWindupSignals = null,
            IEnumerable<TickForwardCellProjectileReleasePresentationSignal> forwardCellProjectileReleaseSignals = null,
            IEnumerable<TickForwardCellProjectileClearPresentationSignal> forwardCellProjectileClearSignals = null,
            IEnumerable<EntitySpawnPresentationSignal> entitySpawnSignals = null,
            IEnumerable<TickPlayerOutcomePresentationSignal> playerOutcomeSignals = null,
            IEnumerable<TickEnemyUtilityPhasePresentationState> enemyUtilityPhaseStates = null,
            IEnumerable<TickPlayerTopologyTransitionBlockedSignal> playerTopologyTransitionBlockedSignals = null)
            : this(
                entityMotions,
                topologyMotion,
                visibilityChanges,
                transitionVisibilityChanges,
                playerActionSignals,
                playerLocomotionSignals,
                playerDamageSignals,
                playerDeathSignals,
                enemyDamageSignals,
                enemyActionSignals,
                enemyJumpSignals,
                enemyChargeSignals,
                entityExitSignals,
                flipImpactSignals,
                kinematicMotionTracks: kinematicMotionTracks,
                playerDeathHoldSignals: playerDeathHoldSignals,
                continuousLocomotionTracks: continuousLocomotionTracks,
                enemyGlideSignals: enemyGlideSignals,
                tileEvents: tileEvents,
                gravityFieldEvents: gravityFieldEvents,
                gravityFieldVisualStates: gravityFieldVisualStates,
                playerActionAttemptSignals: playerActionAttemptSignals,
                boxSlideStopSignals: boxSlideStopSignals,
                enemyUtilitySignals: enemyUtilitySignals,
                enemyUtilityCooldownSignals: enemyUtilityCooldownSignals,
                enemyGravityFieldAuraVisualStates: enemyGravityFieldAuraVisualStates,
                boxSlideStartSignals: boxSlideStartSignals,
                tileFeatureVisualStates: tileFeatureVisualStates,
                tileFeatureVisibleVisualStates: tileFeatureVisibleVisualStates,
                tileFeatureActiveVisualStates: tileFeatureActiveVisualStates,
                playerFlipResultTurnSignals: playerFlipResultTurnSignals,
                flipFloorImpactSignals: flipFloorImpactSignals,
                forwardCellImpactSignals: forwardCellImpactSignals,
                forwardCellProjectileArrivalSignals: forwardCellProjectileArrivalSignals,
                forwardCellProjectileWindupSignals: forwardCellProjectileWindupSignals,
                forwardCellProjectileReleaseSignals: forwardCellProjectileReleaseSignals,
                forwardCellProjectileClearSignals: forwardCellProjectileClearSignals,
                entitySpawnSignals: entitySpawnSignals,
                playerOutcomeSignals: playerOutcomeSignals,
                enemyUtilityPhaseStates: enemyUtilityPhaseStates,
                playerTopologyTransitionBlockedSignals: playerTopologyTransitionBlockedSignals)
        {
            if (impactTransientSignals == null)
            {
                throw new ArgumentNullException(nameof(impactTransientSignals));
            }

            _impactTransientSignals = new ReadOnlyCollection<TickImpactTransientPresentationSignal>(
                new List<TickImpactTransientPresentationSignal>(impactTransientSignals));
        }

        internal TickPresentationData(
            IEnumerable<TickEntityMotion> entityMotions,
            TickTopologyMotion? topologyMotion,
            IEnumerable<TickVisibilityChange> visibilityChanges,
            IEnumerable<TickTransitionVisibilityChange> transitionVisibilityChanges,
            IEnumerable<TickPlayerActionPresentationSignal> playerActionSignals,
            IEnumerable<TickPlayerLocomotionPresentationSignal> playerLocomotionSignals,
            IEnumerable<TickPlayerDamagePresentationSignal> playerDamageSignals,
            IEnumerable<TickPlayerDeathPresentationSignal> playerDeathSignals,
            IEnumerable<TickEnemyDamagePresentationSignal> enemyDamageSignals,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals,
            IEnumerable<TickEnemyJumpPresentationSignal> enemyJumpSignals,
            IEnumerable<TickEnemyChargePresentationSignal> enemyChargeSignals,
            IEnumerable<TickEntityExitPresentationSignal> entityExitSignals,
            IEnumerable<TickImpactTransientPresentationSignal> impactTransientSignals,
            IEnumerable<FlipImpactPresentationSignal> flipImpactSignals,
            IEnumerable<TickSummonedEnemyPresentationBinding> summonedEnemyPresentationBindings,
            IEnumerable<TickFrontFaceShieldSourceSignal> frontFaceShieldSources = null,
            IEnumerable<TickFrontFaceShieldBlockSignal> frontFaceShieldBlocks = null,
            IEnumerable<TickSummonWindupWarningSignal> summonWindupWarnings = null,
            IEnumerable<TickFrontFaceShieldWindupWarningSignal> frontFaceShieldWindupWarnings = null,
            IEnumerable<TickKinematicMotionTrack> kinematicMotionTracks = null,
            IEnumerable<TickPlayerDeathHoldPresentationSignal> playerDeathHoldSignals = null,
            IEnumerable<TickContinuousLocomotionTrack> continuousLocomotionTracks = null,
            IEnumerable<TickEnemyGlidePresentationSignal> enemyGlideSignals = null,
            IEnumerable<TilePresentationEvent> tileEvents = null,
            IEnumerable<GravityFieldPresentationEvent> gravityFieldEvents = null,
            IEnumerable<GravityFieldVisualState> gravityFieldVisualStates = null,
            IEnumerable<TickPlayerActionAttemptPresentationSignal> playerActionAttemptSignals = null,
            IEnumerable<BoxSlideStopPresentationSignal> boxSlideStopSignals = null,
            IEnumerable<TickEnemyUtilityPresentationSignal> enemyUtilitySignals = null,
            IEnumerable<TickEnemyUtilityCooldownPresentationSignal> enemyUtilityCooldownSignals = null,
            IEnumerable<TickEnemyGravityFieldAuraVisualState> enemyGravityFieldAuraVisualStates = null,
            IEnumerable<BoxSlideStartPresentationSignal> boxSlideStartSignals = null,
            IEnumerable<TileFeatureVisualState> tileFeatureVisualStates = null,
            IEnumerable<TileFeatureVisualState> tileFeatureVisibleVisualStates = null,
            IEnumerable<TileFeatureActiveVisualState> tileFeatureActiveVisualStates = null,
            IEnumerable<TickPlayerFlipResultTurnSignal> playerFlipResultTurnSignals = null,
            IEnumerable<FlipFloorImpactPresentationSignal> flipFloorImpactSignals = null,
            IEnumerable<TickForwardCellImpactPresentationSignal> forwardCellImpactSignals = null,
            IEnumerable<TickForwardCellProjectileArrivalPresentationSignal> forwardCellProjectileArrivalSignals = null,
            IEnumerable<TickForwardCellProjectileWindupPresentationSignal> forwardCellProjectileWindupSignals = null,
            IEnumerable<TickForwardCellProjectileReleasePresentationSignal> forwardCellProjectileReleaseSignals = null,
            IEnumerable<TickForwardCellProjectileClearPresentationSignal> forwardCellProjectileClearSignals = null,
            IEnumerable<EntitySpawnPresentationSignal> entitySpawnSignals = null,
            IEnumerable<TickPlayerOutcomePresentationSignal> playerOutcomeSignals = null,
            IEnumerable<TickEnemyUtilityPhasePresentationState> enemyUtilityPhaseStates = null,
            IEnumerable<TickPlayerTopologyTransitionBlockedSignal> playerTopologyTransitionBlockedSignals = null)
            : this(
                entityMotions: entityMotions,
                topologyMotion: topologyMotion,
                visibilityChanges: visibilityChanges,
                transitionVisibilityChanges: transitionVisibilityChanges,
                playerActionSignals: playerActionSignals,
                playerLocomotionSignals: playerLocomotionSignals,
                playerDamageSignals: playerDamageSignals,
                playerDeathSignals: playerDeathSignals,
                enemyDamageSignals: enemyDamageSignals,
                enemyActionSignals: enemyActionSignals,
                enemyJumpSignals: enemyJumpSignals,
                enemyChargeSignals: enemyChargeSignals,
                entityExitSignals: entityExitSignals,
                impactTransientSignals: impactTransientSignals,
                flipImpactSignals: flipImpactSignals,
                kinematicMotionTracks: kinematicMotionTracks,
                playerDeathHoldSignals: playerDeathHoldSignals,
                continuousLocomotionTracks: continuousLocomotionTracks,
                enemyGlideSignals: enemyGlideSignals,
                tileEvents: tileEvents,
                gravityFieldEvents: gravityFieldEvents,
                gravityFieldVisualStates: gravityFieldVisualStates,
                playerActionAttemptSignals: playerActionAttemptSignals,
                boxSlideStopSignals: boxSlideStopSignals,
                enemyUtilitySignals: enemyUtilitySignals,
                enemyUtilityCooldownSignals: enemyUtilityCooldownSignals,
                enemyGravityFieldAuraVisualStates: enemyGravityFieldAuraVisualStates,
                boxSlideStartSignals: boxSlideStartSignals,
                tileFeatureVisualStates: tileFeatureVisualStates,
                tileFeatureVisibleVisualStates: tileFeatureVisibleVisualStates,
                tileFeatureActiveVisualStates: tileFeatureActiveVisualStates,
                playerFlipResultTurnSignals: playerFlipResultTurnSignals,
                flipFloorImpactSignals: flipFloorImpactSignals,
                forwardCellImpactSignals: forwardCellImpactSignals,
                forwardCellProjectileArrivalSignals: forwardCellProjectileArrivalSignals,
                forwardCellProjectileWindupSignals: forwardCellProjectileWindupSignals,
                forwardCellProjectileReleaseSignals: forwardCellProjectileReleaseSignals,
                forwardCellProjectileClearSignals: forwardCellProjectileClearSignals,
                entitySpawnSignals: entitySpawnSignals,
                playerOutcomeSignals: playerOutcomeSignals,
                enemyUtilityPhaseStates: enemyUtilityPhaseStates,
                playerTopologyTransitionBlockedSignals: playerTopologyTransitionBlockedSignals)
        {
            if (summonedEnemyPresentationBindings == null)
            {
                throw new ArgumentNullException(nameof(summonedEnemyPresentationBindings));
            }

            _summonedEnemyPresentationBindings = new ReadOnlyCollection<TickSummonedEnemyPresentationBinding>(
                new List<TickSummonedEnemyPresentationBinding>(summonedEnemyPresentationBindings));
            _frontFaceShieldSources = new ReadOnlyCollection<TickFrontFaceShieldSourceSignal>(
                new List<TickFrontFaceShieldSourceSignal>(
                    frontFaceShieldSources ?? Array.Empty<TickFrontFaceShieldSourceSignal>()));
            _frontFaceShieldBlocks = new ReadOnlyCollection<TickFrontFaceShieldBlockSignal>(
                new List<TickFrontFaceShieldBlockSignal>(
                    frontFaceShieldBlocks ?? Array.Empty<TickFrontFaceShieldBlockSignal>()));
            _summonWindupWarnings = new ReadOnlyCollection<TickSummonWindupWarningSignal>(
                new List<TickSummonWindupWarningSignal>(
                    summonWindupWarnings ?? Array.Empty<TickSummonWindupWarningSignal>()));
            _frontFaceShieldWindupWarnings = new ReadOnlyCollection<TickFrontFaceShieldWindupWarningSignal>(
                new List<TickFrontFaceShieldWindupWarningSignal>(
                    frontFaceShieldWindupWarnings ?? Array.Empty<TickFrontFaceShieldWindupWarningSignal>()));
        }

        public IReadOnlyList<TickEntityMotion> EntityMotions => _entityMotions;

        public IReadOnlyList<TickKinematicMotionTrack> KinematicMotionTracks => _kinematicMotionTracks;

        public IReadOnlyList<TickContinuousLocomotionTrack> ContinuousLocomotionTracks => _continuousLocomotionTracks;

        public IReadOnlyList<EntitySpawnPresentationSignal> EntitySpawnSignals => _entitySpawnSignals;

        public IReadOnlyList<TilePresentationEvent> TileEvents => _tileEvents;

        public IReadOnlyList<TileFeatureVisualState> TileFeatureVisualStates => _tileFeatureVisualStates;

        public IReadOnlyList<TileFeatureVisualState> TileFeatureVisibleVisualStates =>
            _tileFeatureVisibleVisualStates;

        public IReadOnlyList<GravityFieldPresentationEvent> GravityFieldEvents => _gravityFieldEvents;

        public IReadOnlyList<GravityFieldVisualState> GravityFieldVisualStates => _gravityFieldVisualStates;

        public IReadOnlyList<TileFeatureActiveVisualState> TileFeatureActiveVisualStates => _tileFeatureActiveVisualStates;

        public TickTopologyMotion? TopologyMotion => _topologyMotion;

        public IReadOnlyList<TickVisibilityChange> VisibilityChanges => _visibilityChanges;

        public IReadOnlyList<TickTransitionVisibilityChange> TransitionVisibilityChanges => _transitionVisibilityChanges;

        public IReadOnlyList<TickPlayerActionPresentationSignal> PlayerActionSignals => _playerActionSignals;

        public IReadOnlyList<TickPlayerActionAttemptPresentationSignal> PlayerActionAttemptSignals =>
            _playerActionAttemptSignals;

        public IReadOnlyList<TickPlayerFlipResultTurnSignal> PlayerFlipResultTurnSignals =>
            _playerFlipResultTurnSignals;

        public IReadOnlyList<TickPlayerLocomotionPresentationSignal> PlayerLocomotionSignals => _playerLocomotionSignals;

        public IReadOnlyList<TickPlayerDamagePresentationSignal> PlayerDamageSignals => _playerDamageSignals;

        public IReadOnlyList<TickPlayerDeathPresentationSignal> PlayerDeathSignals => _playerDeathSignals;

        public IReadOnlyList<TickPlayerDeathHoldPresentationSignal> PlayerDeathHoldSignals => _playerDeathHoldSignals;

        public IReadOnlyList<TickPlayerOutcomePresentationSignal> PlayerOutcomeSignals => _playerOutcomeSignals;

        public IReadOnlyList<TickPlayerTopologyTransitionBlockedSignal> PlayerTopologyTransitionBlockedSignals =>
            _playerTopologyTransitionBlockedSignals;

        public IReadOnlyList<TickEnemyDamagePresentationSignal> EnemyDamageSignals => _enemyDamageSignals;

        public IReadOnlyList<TickEnemyActionPresentationSignal> EnemyActionSignals => _enemyActionSignals;

        public IReadOnlyList<TickEnemyJumpPresentationSignal> EnemyJumpSignals => _enemyJumpSignals;

        public IReadOnlyList<TickEnemyChargePresentationSignal> EnemyChargeSignals => _enemyChargeSignals;

        public IReadOnlyList<TickEnemyGlidePresentationSignal> EnemyGlideSignals => _enemyGlideSignals;

        public IReadOnlyList<TickEnemyUtilityPresentationSignal> EnemyUtilitySignals => _enemyUtilitySignals;

        public IReadOnlyList<TickEnemyUtilityPhasePresentationState> EnemyUtilityPhaseStates =>
            _enemyUtilityPhaseStates;

        public IReadOnlyList<TickEnemyUtilityCooldownPresentationSignal> EnemyUtilityCooldownSignals =>
            _enemyUtilityCooldownSignals;

        public IReadOnlyList<TickEnemyGravityFieldAuraVisualState> EnemyGravityFieldAuraVisualStates =>
            _enemyGravityFieldAuraVisualStates;

        public IReadOnlyList<TickForwardCellProjectileWindupPresentationSignal> ForwardCellProjectileWindupSignals =>
            _forwardCellProjectileWindupSignals;

        public IReadOnlyList<TickForwardCellProjectileReleasePresentationSignal> ForwardCellProjectileReleaseSignals =>
            _forwardCellProjectileReleaseSignals;

        public IReadOnlyList<TickForwardCellProjectileClearPresentationSignal> ForwardCellProjectileClearSignals =>
            _forwardCellProjectileClearSignals;

        public IReadOnlyList<TickForwardCellImpactPresentationSignal> ForwardCellImpactSignals =>
            _forwardCellImpactSignals;

        public IReadOnlyList<TickForwardCellProjectileArrivalPresentationSignal> ForwardCellProjectileArrivalSignals =>
            _forwardCellProjectileArrivalSignals;

        public IReadOnlyList<TickEntityExitPresentationSignal> EntityExitSignals => _entityExitSignals;

        public IReadOnlyList<BoxSlideStopPresentationSignal> BoxSlideStopSignals => _boxSlideStopSignals;

        public IReadOnlyList<BoxSlideStartPresentationSignal> BoxSlideStartSignals => _boxSlideStartSignals;

        public IReadOnlyList<FlipImpactPresentationSignal> FlipImpactSignals => _flipImpactSignals;

        public IReadOnlyList<FlipFloorImpactPresentationSignal> FlipFloorImpactSignals => _flipFloorImpactSignals;

        internal IReadOnlyList<TickImpactTransientPresentationSignal> ImpactTransientSignals => _impactTransientSignals;

        public IReadOnlyList<TickSummonedEnemyPresentationBinding> SummonedEnemyPresentationBindings =>
            _summonedEnemyPresentationBindings;

        public IReadOnlyList<TickFrontFaceShieldSourceSignal> FrontFaceShieldSources => _frontFaceShieldSources;

        public IReadOnlyList<TickFrontFaceShieldBlockSignal> FrontFaceShieldBlocks => _frontFaceShieldBlocks;

        public IReadOnlyList<TickSummonWindupWarningSignal> SummonWindupWarnings => _summonWindupWarnings;

        public IReadOnlyList<TickFrontFaceShieldWindupWarningSignal> FrontFaceShieldWindupWarnings =>
            _frontFaceShieldWindupWarnings;
    }
}
