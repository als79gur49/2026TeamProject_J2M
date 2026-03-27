namespace Game.Feature.Gameplay.BoardState
{
    public enum FaceId
    {
        Floor = 0,
        Front = 1,
        Ceiling = 2,
        Back = 3,
    }

    public static class FaceIdUtility
    {
        public static FaceId GetNext(FaceId face)
        {
            return face switch
            {
                FaceId.Floor => FaceId.Front,
                FaceId.Front => FaceId.Ceiling,
                FaceId.Ceiling => FaceId.Back,
                _ => FaceId.Floor,
            };
        }

        public static FaceId GetPrevious(FaceId face)
        {
            return face switch
            {
                FaceId.Floor => FaceId.Back,
                FaceId.Front => FaceId.Floor,
                FaceId.Ceiling => FaceId.Front,
                _ => FaceId.Ceiling,
            };
        }
    }
}
