using System;

namespace Game.Feature.Gameplay.BoardState
{
    internal readonly struct TransitionRequirement : IEquatable<TransitionRequirement>
    {
        public static readonly TransitionRequirement None = new(
            TransitionRequirementKind.None,
            CubeRotationKind.None,
            default);

        private TransitionRequirement(
            TransitionRequirementKind kind,
            CubeRotationKind rotationKind,
            CubeTopologyState updatedTopology)
        {
            Kind = kind;
            RotationKind = rotationKind;
            UpdatedTopology = updatedTopology;
        }

        public TransitionRequirementKind Kind { get; }

        public CubeRotationKind RotationKind { get; }

        public CubeTopologyState UpdatedTopology { get; }

        public static TransitionRequirement TopologyUpdate(
            CubeRotationKind rotationKind,
            CubeTopologyState updatedTopology)
        {
            if (rotationKind == CubeRotationKind.None)
            {
                throw new ArgumentOutOfRangeException(nameof(rotationKind), "Topology updates require a non-none rotation.");
            }

            return new TransitionRequirement(
                TransitionRequirementKind.TopologyUpdate,
                rotationKind,
                updatedTopology);
        }

        public bool Equals(TransitionRequirement other)
        {
            return Kind == other.Kind &&
                   RotationKind == other.RotationKind &&
                   UpdatedTopology.Equals(other.UpdatedTopology);
        }

        public override bool Equals(object obj)
        {
            return obj is TransitionRequirement other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                hash = (hash * 397) ^ (int)RotationKind;
                hash = (hash * 397) ^ UpdatedTopology.GetHashCode();
                return hash;
            }
        }
    }
}
