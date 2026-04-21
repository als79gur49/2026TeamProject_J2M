using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Stages
{
    [Serializable]
    public struct StageIdAliasEntry
    {
        public string DeprecatedStageId;
        public StageId CurrentStageId;
    }

    [CreateAssetMenu(menuName = "Gameplay/Stages/Stage Id Alias Table", fileName = "StageIdAliasTable")]
    public sealed class StageIdAliasTable : ScriptableObject
    {
        [SerializeField] private StageIdAliasEntry[] entries = Array.Empty<StageIdAliasEntry>();

        public IReadOnlyList<StageIdAliasEntry> Entries => entries ?? Array.Empty<StageIdAliasEntry>();

        public void SetEntries(StageIdAliasEntry[] value)
        {
            entries = value ?? Array.Empty<StageIdAliasEntry>();
        }

        public StageId Resolve(string rawStageId)
        {
            if (!StageIdNormalizer.TryNormalize(rawStageId, out var normalizedRawId, out _))
            {
                return StageId.None;
            }

            for (var i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];
                if (!StageIdNormalizer.TryNormalize(entry.DeprecatedStageId, out var normalizedAlias, out _))
                {
                    continue;
                }

                if (string.Equals(normalizedAlias, normalizedRawId, StringComparison.Ordinal))
                {
                    return entry.CurrentStageId;
                }
            }

            return StageId.None;
        }
    }
}
