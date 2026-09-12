namespace Game.Exhibition
{
    public interface IParticipantRestart
    {
        void ValidateAvailable();
        void Restart(ResetIdentity identity);
    }
}
