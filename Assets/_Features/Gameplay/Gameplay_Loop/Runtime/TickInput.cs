namespace Game.Feature.Gameplay.Loop
{
    public readonly struct TickInput
    {
        public TickInput(int tickIndex)
        {
            TickIndex = tickIndex;
        }

        public int TickIndex { get; }
    }
}
