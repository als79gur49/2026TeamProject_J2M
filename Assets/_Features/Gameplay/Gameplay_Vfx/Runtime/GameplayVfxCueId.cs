using System;

namespace Game.Feature.Gameplay.Vfx
{
    public readonly struct GameplayVfxCueId : IEquatable<GameplayVfxCueId>, IComparable<GameplayVfxCueId>
    {
        public static readonly GameplayVfxCueId None = new(GameplayVfxFamily.None, 0);

        public GameplayVfxCueId(GameplayVfxFamily family, int code)
        {
            Family = family;
            Code = code;
        }

        public GameplayVfxFamily Family { get; }

        public int Code { get; }

        public bool IsNone => Family == GameplayVfxFamily.None || Code == 0;

        public static GameplayVfxCueId From(PlayerVfxCue cue)
        {
            return new GameplayVfxCueId(GameplayVfxFamily.Player, (int)cue);
        }

        public static GameplayVfxCueId From(BoxVfxCue cue)
        {
            return new GameplayVfxCueId(GameplayVfxFamily.Box, (int)cue);
        }

        public static GameplayVfxCueId From(EnemyVfxCue cue)
        {
            return new GameplayVfxCueId(GameplayVfxFamily.Enemy, (int)cue);
        }

        public static GameplayVfxCueId From(TileFeatureVfxCue cue)
        {
            return new GameplayVfxCueId(GameplayVfxFamily.TileFeature, (int)cue);
        }

        public static GameplayVfxCueId From(TerrainVfxCue cue)
        {
            return new GameplayVfxCueId(GameplayVfxFamily.Terrain, (int)cue);
        }

        public static GameplayVfxCueId From(ProjectileVfxCue cue)
        {
            return new GameplayVfxCueId(GameplayVfxFamily.Projectile, (int)cue);
        }

        public static GameplayVfxCueId From(ObjectiveStageVfxCue cue)
        {
            return new GameplayVfxCueId(GameplayVfxFamily.ObjectiveStage, (int)cue);
        }

        public int CompareTo(GameplayVfxCueId other)
        {
            var familyCompare = Family.CompareTo(other.Family);
            return familyCompare != 0 ? familyCompare : Code.CompareTo(other.Code);
        }

        public bool Equals(GameplayVfxCueId other)
        {
            return Family == other.Family && Code == other.Code;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayVfxCueId other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Family * 397) ^ Code;
            }
        }

        public override string ToString()
        {
            return IsNone ? "None" : $"{Family}:{Code}";
        }

        public static bool operator ==(GameplayVfxCueId left, GameplayVfxCueId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GameplayVfxCueId left, GameplayVfxCueId right)
        {
            return !left.Equals(right);
        }
    }
}
