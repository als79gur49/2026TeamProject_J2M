namespace Game.Feature.Stages
{
    public sealed class StageClearResult
    {
        public StageClearResult(
            StageId stageId,
            int finalTickIndex)
        {
            StageId = stageId;
            FinalTickIndex = finalTickIndex;
        }

        public StageId StageId { get; }

        public int FinalTickIndex { get; }
    }

}
