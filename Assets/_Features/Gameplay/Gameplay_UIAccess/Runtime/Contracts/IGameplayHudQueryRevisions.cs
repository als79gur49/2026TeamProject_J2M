using System;

namespace Game.Feature.Gameplay.UIAccess.Contracts
{
    public enum GameplayHudQueryKind { Stage, Objective, PlayerHud, SurfaceRemainder }

    // Identity tokens are compared by reference, never by hash. Probing must not clone a
    // snapshot, traverse content, or access storage, but must preserve read-time guards.
    public readonly struct GameplayHudQueryStamp : IEquatable<GameplayHudQueryStamp>
    {
        private readonly object _owner, _definition, _result;
        private readonly long _generation, _version;
        public long ChanceRevision { get; }
        public GameplayHudQueryStamp(object owner, object definition = null, object result = null,
            long generation = 0, long version = 0, long chanceRevision = -1)
        {
            _owner = owner; _definition = definition; _result = result;
            _generation = generation; _version = version; ChanceRevision = chanceRevision;
        }
        public bool Equals(GameplayHudQueryStamp other) => ReferenceEquals(_owner, other._owner) &&
            ReferenceEquals(_definition, other._definition) && ReferenceEquals(_result, other._result) &&
            _generation == other._generation && _version == other._version && ChanceRevision == other.ChanceRevision;
        public override bool Equals(object obj) => obj is GameplayHudQueryStamp other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(_generation, _version, ChanceRevision);
    }

    public readonly struct GameplayHudQueryRead<T>
    {
        public GameplayHudQueryRead(T value, GameplayHudQueryStamp stamp, bool canReuse)
        { Value = value; Stamp = stamp; CanReuse = canReuse; }
        public T Value { get; }
        public GameplayHudQueryStamp Stamp { get; }
        public bool CanReuse { get; }
    }

    public interface IGameplayHudRevisionProbe
    {
        bool TryGetRevision(out GameplayHudQueryStamp stamp);
    }

    public interface IGameplayHudRevisionedQuery<T> : IGameplayHudRevisionProbe
    {
        GameplayHudQueryRead<T> ReadWithRevision();
    }

    public interface IGameplayHudQueryRevisions
    {
        bool TryGetRevision(GameplayHudQueryKind kind, out GameplayHudQueryStamp stamp);
    }

    public interface IGameplayHudContentInvalidation
    {
        void InvalidateHudContent();
    }
}
