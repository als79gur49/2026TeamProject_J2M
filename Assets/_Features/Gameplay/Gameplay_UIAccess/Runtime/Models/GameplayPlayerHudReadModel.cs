namespace Game.Feature.Gameplay.UIAccess.Models
{
    public enum GameplayChanceAudioPolicy
    {
        Default = 0,
        SuppressChanceChangeCue = 1,
    }

    public readonly struct GameplayPlayerHudReadModel
    {
        public GameplayPlayerHudReadModel(
            bool hasRemainingChances = false,
            int remainingChances = 0,
            int maxChances = 0,
            GameplayChanceAudioPolicy chanceAudioPolicy = GameplayChanceAudioPolicy.Default,
            bool hasHealth = false, int hp = 0, int maxHp = 0)
        {
            HasHealth = hasHealth;
            Hp = hp;
            MaxHp = maxHp;
            HasRemainingChances = hasRemainingChances;
            RemainingChances = remainingChances;
            MaxChances = maxChances > 0
                ? maxChances
                : (hasRemainingChances ? remainingChances : 0);
            ChanceAudioPolicy = hasRemainingChances
                ? chanceAudioPolicy
                : GameplayChanceAudioPolicy.Default;
        }

        public bool HasHealth { get; }
        public int Hp { get; }
        public int MaxHp { get; }

        public bool HasRemainingChances { get; }

        public int RemainingChances { get; }

        public int MaxChances { get; }

        public GameplayChanceAudioPolicy ChanceAudioPolicy { get; }
    }
}
