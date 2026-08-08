namespace Game.Platform.Steam
{
    public enum SteamAchievementSmokePhase
    {
        OptIn = 0,
        Session = 1,
        CallbackRegistration = 2,
        Schema = 3,
        BeforeRead = 4,
        Set = 5,
        Store = 6,
        CallbackWait = 7,
        AfterRead = 8,
        Completed = 9,
    }
}
