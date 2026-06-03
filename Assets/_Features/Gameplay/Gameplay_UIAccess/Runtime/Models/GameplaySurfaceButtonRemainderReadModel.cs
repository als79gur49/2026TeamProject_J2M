namespace Game.Feature.Gameplay.UIAccess.Models
{
    public readonly struct GameplaySurfaceButtonRemainderReadModel
    {
        public GameplaySurfaceButtonRemainderReadModel(
            GameplayUiFace face,
            int normalRemaining,
            int moonBlockOnlyRemaining)
        {
            Face = face;
            NormalRemaining = normalRemaining > 0 ? normalRemaining : 0;
            MoonBlockOnlyRemaining = moonBlockOnlyRemaining > 0 ? moonBlockOnlyRemaining : 0;
        }

        public GameplayUiFace Face { get; }

        public int NormalRemaining { get; }

        public int MoonBlockOnlyRemaining { get; }

        public int TotalRemaining => NormalRemaining + MoonBlockOnlyRemaining;

        public bool HasAnyRemaining => TotalRemaining > 0;
    }
}
