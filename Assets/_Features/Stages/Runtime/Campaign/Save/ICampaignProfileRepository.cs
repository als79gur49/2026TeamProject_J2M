namespace Game.Feature.Stages
{
    public interface ICampaignProfileRepository
    {
        CampaignProfileLoadResult Load();

        void Save(CampaignProfileDocument document);

        void SaveDestructive(CampaignProfileDocument document);
    }
}
