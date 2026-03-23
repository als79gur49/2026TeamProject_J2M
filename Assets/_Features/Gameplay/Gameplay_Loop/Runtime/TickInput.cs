namespace Game.Feature.Gameplay.Loop
{
    public readonly struct TickInput
    {
        public TickInput(int tickIndex, PlayerTickCommand playerCommand = default)
        {
            TickIndex = tickIndex;
            PlayerCommand = playerCommand;
        }

        public int TickIndex { get; }

        public PlayerTickCommand PlayerCommand { get; }
    }
}
