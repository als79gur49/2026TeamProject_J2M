using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Host
{
    public sealed class InputRepeatCooldown
    {
        private readonly bool _directionChangeConsumesDelay;
        private readonly int _initialMoveDelayTicks;
        private readonly int _repeatedMoveIntervalTicks;

        private bool _hasActiveHold;
        private bool _hasIssuedMoveForHold;
        private Direction _heldDirection;
        private int _firstMoveAllowedTick;
        private int _nextMoveAllowedTick;

        public InputRepeatCooldown(
            int initialMoveDelayTicks,
            int repeatedMoveIntervalTicks,
            bool directionChangeConsumesDelay)
        {
            if (initialMoveDelayTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialMoveDelayTicks), "Initial move delay must be zero or greater.");
            }

            if (repeatedMoveIntervalTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(repeatedMoveIntervalTicks), "Repeated move interval must be greater than zero.");
            }

            _initialMoveDelayTicks = initialMoveDelayTicks;
            _repeatedMoveIntervalTicks = repeatedMoveIntervalTicks;
            _directionChangeConsumesDelay = directionChangeConsumesDelay;
        }

        public InputRepeatCooldown(
            GameplayTimingProfile timingProfile,
            bool directionChangeConsumesDelay)
            : this(
                (timingProfile ?? throw new ArgumentNullException(nameof(timingProfile))).InitialMoveDelayTicks,
                timingProfile.RepeatedMoveIntervalTicks,
                directionChangeConsumesDelay)
        {
        }

        public PlayerTickCommand BuildCommand(int currentTick, Direction quantizedDirection)
        {
            if (currentTick <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(currentTick), "Tick indices must be positive.");
            }

            if (quantizedDirection == Direction.None)
            {
                Reset();
                return PlayerTickCommand.None;
            }

            if (!_hasActiveHold)
            {
                BeginHold(currentTick, quantizedDirection, allowImmediateIssue: _initialMoveDelayTicks == 0);
                return TryIssueFirstMove(currentTick);
            }

            if (quantizedDirection != _heldDirection)
            {
                BeginHold(
                    currentTick,
                    quantizedDirection,
                    allowImmediateIssue: !_directionChangeConsumesDelay || _initialMoveDelayTicks == 0);
                return TryIssueFirstMove(currentTick);
            }

            if (!_hasIssuedMoveForHold)
            {
                return TryIssueFirstMove(currentTick);
            }

            if (currentTick < _nextMoveAllowedTick)
            {
                return PlayerTickCommand.None;
            }

            return Issue(currentTick);
        }

        public void Reset()
        {
            _hasActiveHold = false;
            _hasIssuedMoveForHold = false;
            _heldDirection = Direction.None;
            _firstMoveAllowedTick = 0;
            _nextMoveAllowedTick = 0;
        }

        private void BeginHold(int currentTick, Direction direction, bool allowImmediateIssue)
        {
            _hasActiveHold = true;
            _hasIssuedMoveForHold = false;
            _heldDirection = direction;
            _firstMoveAllowedTick = allowImmediateIssue
                ? currentTick
                : currentTick + _initialMoveDelayTicks;
            _nextMoveAllowedTick = 0;
        }

        private PlayerTickCommand TryIssueFirstMove(int currentTick)
        {
            if (currentTick < _firstMoveAllowedTick)
            {
                return PlayerTickCommand.None;
            }

            return Issue(currentTick);
        }

        private PlayerTickCommand Issue(int currentTick)
        {
            _hasIssuedMoveForHold = true;
            _nextMoveAllowedTick = currentTick + _repeatedMoveIntervalTicks;
            return PlayerTickCommand.Move(_heldDirection);
        }
    }
}
