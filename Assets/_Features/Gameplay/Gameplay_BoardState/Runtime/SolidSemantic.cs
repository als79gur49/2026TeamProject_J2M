using System;

namespace Game.Feature.Gameplay.BoardState
{
    [Serializable]
    public readonly struct SolidSemantic : IEquatable<SolidSemantic>
    {
        public SolidSemantic(EntityState entity, SolidKind kind)
        {
            Entity = entity;
            Kind = kind;
        }

        public EntityState Entity { get; }

        public SolidKind Kind { get; }

        public bool Equals(SolidSemantic other)
        {
            return Entity.Equals(other.Entity) && Kind == other.Kind;
        }

        public override bool Equals(object obj)
        {
            return obj is SolidSemantic other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Entity.GetHashCode() * 397) ^ (int)Kind;
            }
        }

        public override string ToString()
        {
            return $"E={Entity.entityId}|Kind={Kind}";
        }
    }
}
