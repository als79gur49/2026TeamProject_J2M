using System;

namespace Game.Feature.Gameplay.BoardState
{
    public readonly struct KinematicProgressResolution
    {
        public KinematicProgressResolution(
            SurfaceCell anchorCell,
            SimulationOffset2 localOffset,
            bool isAnchorCommitTick,
            bool isSettled,
            int remainingTicks,
            int remainingDistanceUnits,
            int progressUnits)
        {
            AnchorCell = anchorCell;
            LocalOffset = localOffset;
            IsAnchorCommitTick = isAnchorCommitTick;
            IsSettled = isSettled;
            RemainingTicks = remainingTicks;
            RemainingDistanceUnits = remainingDistanceUnits;
            ProgressUnits = progressUnits;
        }

        public SurfaceCell AnchorCell { get; }

        public SimulationOffset2 LocalOffset { get; }

        public bool IsAnchorCommitTick { get; }

        public bool IsSettled { get; }

        public int RemainingTicks { get; }

        public int RemainingDistanceUnits { get; }

        public int ProgressUnits { get; }
    }

    public static class KinematicProgressResolver
    {
        public static bool TryResolveReverseFromHeld(
            SurfaceCell currentAnchor,
            UnitKinematicRuntimeState heldState,
            int reverseStepDirectionX,
            int reverseStepDirectionY,
            out SurfaceCell mirroredAnchor,
            out UnitKinematicRuntimeState mirroredState,
            out int poseDeltaRawUnits)
        {
            mirroredAnchor = default;
            mirroredState = default;
            poseDeltaRawUnits = int.MaxValue;

            var normalizedHeld = heldState.NormalizedForStorage();
            if (normalizedHeld.mode != MotionMode.Held ||
                normalizedHeld.totalTicks < 2 ||
                (normalizedHeld.totalTicks % 2) != 0 ||
                normalizedHeld.elapsedTicks <= 0 ||
                normalizedHeld.elapsedTicks >= normalizedHeld.totalTicks ||
                !IsCardinalDirection(normalizedHeld.stepDirectionX, normalizedHeld.stepDirectionY) ||
                !IsCardinalDirection(reverseStepDirectionX, reverseStepDirectionY) ||
                reverseStepDirectionX != -normalizedHeld.stepDirectionX ||
                reverseStepDirectionY != -normalizedHeld.stepDirectionY)
            {
                return false;
            }

            var totalTicks = normalizedHeld.totalTicks;
            var commitTick = totalTicks / 2;
            var mirroredElapsedTicks = totalTicks - normalizedHeld.elapsedTicks;
            var resolution = ResolvePose(
                currentAnchor,
                reverseStepDirectionX,
                reverseStepDirectionY,
                mirroredElapsedTicks,
                totalTicks);

            mirroredAnchor = resolution.AnchorCell;
            mirroredState = new UnitKinematicRuntimeState
            {
                localOffset = resolution.LocalOffset,
                velocity = CreateDebugVelocity(reverseStepDirectionX, reverseStepDirectionY),
                mode = MotionMode.Voluntary,
                forcedOp = ForcedMotionOp.None,
                remainingDistanceUnits = resolution.RemainingDistanceUnits,
                remainingTicks = resolution.RemainingTicks,
                speedScalePermille = 1000,
                sequenceId = normalizedHeld.sequenceId + 1,
                elapsedTicks = mirroredElapsedTicks,
                totalTicks = totalTicks,
                commitTick = commitTick,
                startedTick = normalizedHeld.startedTick,
                stepDirectionX = reverseStepDirectionX,
                stepDirectionY = reverseStepDirectionY,
            }.NormalizedForStorage();
            poseDeltaRawUnits = ResolvePoseDeltaRawUnits(
                currentAnchor,
                normalizedHeld.localOffset,
                mirroredAnchor,
                mirroredState.localOffset);
            return true;
        }

        public static KinematicProgressResolution ResolvePose(
            SurfaceCell currentAnchor,
            int stepDirectionX,
            int stepDirectionY,
            int elapsedTicks,
            int totalTicks,
            int unitsPerCell = SimulationFixed.UnitsPerCell)
        {
            if (unitsPerCell <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(unitsPerCell), "Units per cell must be greater than zero.");
            }

            if (totalTicks < 2 || (totalTicks % 2) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalTicks), "Kinematic total ticks must be an even value of at least two.");
            }

            if (!IsCardinalDirection(stepDirectionX, stepDirectionY))
            {
                throw new ArgumentException("Kinematic step direction must be cardinal.");
            }

            if (elapsedTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedTicks), "Elapsed ticks must be greater than zero.");
            }

            var commitTick = totalTicks / 2;
            if (elapsedTicks >= totalTicks)
            {
                return new KinematicProgressResolution(
                    currentAnchor,
                    SimulationOffset2.Zero,
                    isAnchorCommitTick: false,
                    isSettled: true,
                    remainingTicks: 0,
                    remainingDistanceUnits: 0,
                    progressUnits: unitsPerCell);
            }

            if (elapsedTicks == commitTick)
            {
                return new KinematicProgressResolution(
                    currentAnchor + new UnityEngine.Vector2Int(stepDirectionX, stepDirectionY),
                    CreateCommitOffset(stepDirectionX, stepDirectionY),
                    isAnchorCommitTick: true,
                    isSettled: false,
                    remainingTicks: totalTicks - elapsedTicks,
                    remainingDistanceUnits: unitsPerCell / 2,
                    progressUnits: unitsPerCell / 2);
            }

            var progressUnits = ResolveProgressUnits(elapsedTicks, totalTicks, unitsPerCell);
            var localUnits = elapsedTicks < commitTick
                ? progressUnits
                : progressUnits - unitsPerCell;

            return new KinematicProgressResolution(
                currentAnchor,
                CreateAxisOffset(stepDirectionX, stepDirectionY, localUnits),
                isAnchorCommitTick: false,
                isSettled: false,
                remainingTicks: totalTicks - elapsedTicks,
                remainingDistanceUnits: Math.Max(0, unitsPerCell - progressUnits),
                progressUnits: progressUnits);
        }

        public static int ResolveProgressUnits(int elapsedTicks, int totalTicks, int unitsPerCell = SimulationFixed.UnitsPerCell)
        {
            if (elapsedTicks >= totalTicks)
            {
                return unitsPerCell;
            }

            return (int)(((long)unitsPerCell * elapsedTicks + (totalTicks / 2)) / totalTicks);
        }

        private static bool IsCardinalDirection(int x, int y)
        {
            return Math.Abs(x) + Math.Abs(y) == 1;
        }

        private static SimulationVelocity2 CreateDebugVelocity(int stepDirectionX, int stepDirectionY)
        {
            return new SimulationVelocity2(
                SimulationFixed.FromRaw(stepDirectionX * SimulationFixed.DefaultReferenceUnitsPerTick),
                SimulationFixed.FromRaw(stepDirectionY * SimulationFixed.DefaultReferenceUnitsPerTick));
        }

        private static int ResolvePoseDeltaRawUnits(
            SurfaceCell oldAnchor,
            SimulationOffset2 oldOffset,
            SurfaceCell newAnchor,
            SimulationOffset2 newOffset)
        {
            if (oldAnchor.face != newAnchor.face)
            {
                return int.MaxValue;
            }

            var oldWorldX = ((long)oldAnchor.x * SimulationFixed.UnitsPerCell) + oldOffset.X.RawValue;
            var oldWorldY = ((long)oldAnchor.y * SimulationFixed.UnitsPerCell) + oldOffset.Y.RawValue;
            var newWorldX = ((long)newAnchor.x * SimulationFixed.UnitsPerCell) + newOffset.X.RawValue;
            var newWorldY = ((long)newAnchor.y * SimulationFixed.UnitsPerCell) + newOffset.Y.RawValue;
            return (int)Math.Max(Math.Abs(oldWorldX - newWorldX), Math.Abs(oldWorldY - newWorldY));
        }

        private static SimulationOffset2 CreateAxisOffset(int stepDirectionX, int stepDirectionY, int localUnits)
        {
            return new SimulationOffset2(
                SimulationFixed.FromRaw(stepDirectionX == 0 ? 0 : localUnits * stepDirectionX),
                SimulationFixed.FromRaw(stepDirectionY == 0 ? 0 : localUnits * stepDirectionY));
        }

        private static SimulationOffset2 CreateCommitOffset(int stepDirectionX, int stepDirectionY)
        {
            return new SimulationOffset2(
                SimulationFixed.FromRaw(CreateCommitAxisOffset(stepDirectionX)),
                SimulationFixed.FromRaw(CreateCommitAxisOffset(stepDirectionY)));
        }

        private static int CreateCommitAxisOffset(int stepDirection)
        {
            if (stepDirection > 0)
            {
                return SimulationFixed.MinLocalOffset;
            }

            if (stepDirection < 0)
            {
                return SimulationFixed.MaxPositiveLocalOffset;
            }

            return 0;
        }
    }
}
