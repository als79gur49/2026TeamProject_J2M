namespace Game.Feature.Stages
{
    public interface IAtomicTextFileStore
    {
        bool Exists(string fileName);

        string ReadAllText(string fileName);

        void WriteAllTextAtomic(string fileName, string contents);
    }
}
