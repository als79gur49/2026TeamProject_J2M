using System;

namespace Game.Feature.Gameplay.BoardState
{
    [Serializable]
    public struct CubeTopologyState : IEquatable<CubeTopologyState>
    {
        public FaceId bottomFace;

        public CubeTopologyState(FaceId bottomFace)
        {
            this.bottomFace = bottomFace;
        }

        public FaceId BottomFace => bottomFace;

        public FaceId FrontFace => FaceIdUtility.GetNext(bottomFace);

        public bool IsFaceActive(FaceId face)
        {
            return face == BottomFace || face == FrontFace;
        }

        public CubeTopologyState Rotate(CubeRotationKind rotationKind)
        {
            return rotationKind switch
            {
                CubeRotationKind.Forward => new CubeTopologyState(FaceIdUtility.GetNext(bottomFace)),
                CubeRotationKind.Backward => new CubeTopologyState(FaceIdUtility.GetPrevious(bottomFace)),
                _ => this,
            };
        }

        public bool Equals(CubeTopologyState other)
        {
            return bottomFace == other.bottomFace;
        }

        public override bool Equals(object obj)
        {
            return obj is CubeTopologyState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)bottomFace;
        }

        public override string ToString()
        {
            return $"Bottom={BottomFace}|Front={FrontFace}";
        }
    }
}
