namespace Game.Feature.Stages
{
    public interface ICampaignProfileRepository
    {
        CampaignProfileLoadResult Load();

        void Save(CampaignProfileDocument document);
    }
}
