namespace Game.Feature.Stages
{
    public readonly struct CampaignProfileLoadResult
    {
        public CampaignProfileLoadResult(
            CampaignProfileLoadStatus status,
            CampaignProfileDocument document,
            string message)
        {
            Status = status;
            Document = document;
            Message = message ?? string.Empty;
        }

        public CampaignProfileLoadStatus Status { get; }

        public CampaignProfileDocument Document { get; }

        public string Message { get; }

        public bool HasDocument => Document != null;
    }
}
