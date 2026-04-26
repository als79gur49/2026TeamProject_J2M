using System;
using System.Collections.Generic;

namespace Game.Feature.Stages
{
    public sealed class CampaignStageSequenceResolver
    {
        private readonly Dictionary<StageId, int> _indicesByStageId = new();
        private readonly Dictionary<string, StageId> _firstStageByLevelGroupId = new(StringComparer.Ordinal);
        private readonly IReadOnlyList<CampaignStageSequenceEntry> _entries;

        public CampaignStageSequenceResolver(CampaignStageSequenceDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            _entries = definition.Entries;
            if (_entries.Count == 0)
            {
                throw new ArgumentException("Campaign stage sequence requires at least one stage.", nameof(definition));
            }

            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry == null || !entry.StageId.IsValid)
                {
                    throw new ArgumentException("Campaign stage sequence contains an invalid stage id.", nameof(definition));
                }

                if (!_indicesByStageId.TryAdd(entry.StageId, i))
                {
                    throw new ArgumentException(
                        $"Campaign stage sequence contains duplicate stage id '{entry.StageId.Value}'.",
                        nameof(definition));
                }

                if (!string.IsNullOrWhiteSpace(entry.LevelGroupId) &&
                    !_firstStageByLevelGroupId.ContainsKey(entry.LevelGroupId))
                {
                    _firstStageByLevelGroupId.Add(entry.LevelGroupId, entry.StageId);
                }
            }
        }

        public StageId FirstStageId => _entries[0].StageId;

        public StageId FinalStageId => _entries[_entries.Count - 1].StageId;

        public IReadOnlyList<CampaignStageSequenceEntry> Entries => _entries;

        public bool Contains(StageId stageId)
        {
            return stageId.IsValid && _indicesByStageId.ContainsKey(stageId);
        }

        public bool IsFirst(StageId stageId)
        {
            return stageId.Equals(FirstStageId);
        }

        public bool IsFinal(StageId stageId)
        {
            return stageId.Equals(FinalStageId);
        }

        public StageId GetNextOrNone(StageId stageId)
        {
            return TryGetNext(stageId, out var nextStageId)
                ? nextStageId
                : StageId.None;
        }

        public bool TryGetNext(StageId stageId, out StageId nextStageId)
        {
            if (!stageId.IsValid || !_indicesByStageId.TryGetValue(stageId, out var index))
            {
                nextStageId = StageId.None;
                return false;
            }

            var nextIndex = index + 1;
            if (nextIndex >= _entries.Count)
            {
                nextStageId = StageId.None;
                return false;
            }

            nextStageId = _entries[nextIndex].StageId;
            return true;
        }

        public string GetDisplayName(StageId stageId)
        {
            return TryGetEntry(stageId, out var entry) ? entry.DisplayName : string.Empty;
        }

        public string GetLevelGroupId(StageId stageId)
        {
            return TryGetEntry(stageId, out var entry) ? entry.LevelGroupId : string.Empty;
        }

        public StageId GetFirstStageInLevelGroupOrNone(string levelGroupId)
        {
            return TryGetFirstStageInLevelGroup(levelGroupId, out var stageId)
                ? stageId
                : StageId.None;
        }

        public bool TryGetFirstStageInLevelGroup(string levelGroupId, out StageId stageId)
        {
            if (!string.IsNullOrWhiteSpace(levelGroupId) &&
                _firstStageByLevelGroupId.TryGetValue(levelGroupId, out stageId))
            {
                return true;
            }

            stageId = StageId.None;
            return false;
        }

        public CampaignStageSequenceEntry GetEntryOrThrow(StageId stageId)
        {
            if (TryGetEntry(stageId, out var entry))
            {
                return entry;
            }

            throw new ArgumentException($"Stage id '{stageId.Value}' is not part of the campaign sequence.", nameof(stageId));
        }

        public bool TryGetEntry(StageId stageId, out CampaignStageSequenceEntry entry)
        {
            if (stageId.IsValid && _indicesByStageId.TryGetValue(stageId, out var index))
            {
                entry = _entries[index];
                return true;
            }

            entry = null;
            return false;
        }
    }
}
