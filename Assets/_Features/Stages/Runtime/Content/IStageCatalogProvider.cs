using System.Collections.Generic;

namespace Game.Feature.Stages
{
    public interface IStageCatalogProvider
    {
        IReadOnlyList<StageContentEntry> LoadEntries();

        StageIdAliasTable AliasTable { get; }
    }
}
