using System;
using UnityEngine;

namespace Game.Feature.Stages
{
    [CreateAssetMenu(menuName = "Gameplay/Stages/Stage Catalog", fileName = "StageCatalog")]
    public sealed class StageCatalog : ScriptableObject
    {
        [SerializeField] private StageContentEntry[] entries = Array.Empty<StageContentEntry>();
        [SerializeField] private StageIdAliasTable stageIdAliasTable;

        public StageContentEntry[] Entries => entries ?? Array.Empty<StageContentEntry>();

        public StageIdAliasTable StageIdAliasTable => stageIdAliasTable;
    }
}
