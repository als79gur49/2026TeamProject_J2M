using System;
using System.Collections.Generic;

namespace Game.Feature.Stages
{
    public interface ICampaignStageSequenceResolverProvider
    {
        bool TryCreateCampaignStageSequenceResolver(out CampaignStageSequenceResolver resolver);
    }

    public readonly struct CampaignStageSequenceSnapshotEntry
    {
        internal CampaignStageSequenceSnapshotEntry(StageId stageId, string levelGroupId)
        {
            StageId = stageId;
            LevelGroupId = levelGroupId;
        }

        public StageId StageId { get; }

        public string LevelGroupId { get; }
    }

    public sealed class CampaignStageSequenceResolver
    {
        private readonly Dictionary<StageId, int> _indicesByStageId = new();
        private readonly Dictionary<string, StageId> _firstStageByLevelGroupId = new(StringComparer.Ordinal);
        private readonly IReadOnlyList<CampaignStageSequenceSnapshotEntry> _entries;

        public CampaignStageSequenceResolver(CampaignStageSequenceDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var sourceEntries = definition.Entries;
            if (sourceEntries == null || sourceEntries.Count == 0)
            {
                throw new ArgumentException("Campaign stage sequence requires at least one stage.", nameof(definition));
            }

            var entries = new CampaignStageSequenceSnapshotEntry[sourceEntries.Count];
            for (var i = 0; i < sourceEntries.Count; i++)
            {
                var sourceEntry = sourceEntries[i];
                if (sourceEntry == null || !sourceEntry.StageId.IsValid)
                {
                    throw new ArgumentException("Campaign stage sequence contains an invalid stage id.", nameof(definition));
                }

                if (!_indicesByStageId.TryAdd(sourceEntry.StageId, i))
                {
                    throw new ArgumentException(
                        $"Campaign stage sequence contains duplicate stage id '{sourceEntry.StageId.Value}'.",
                        nameof(definition));
                }

                var levelGroupId = sourceEntry.LevelGroupId;
                if (string.IsNullOrWhiteSpace(levelGroupId) ||
                    !StageIdNormalizer.IsCanonical(levelGroupId))
                {
                    throw new ArgumentException(
                        $"Campaign stage sequence entry '{sourceEntry.StageId.Value}' contains an invalid level group id.",
                        nameof(definition));
                }

                entries[i] = new CampaignStageSequenceSnapshotEntry(
                    sourceEntry.StageId,
                    levelGroupId);
                _firstStageByLevelGroupId.TryAdd(levelGroupId, sourceEntry.StageId);
            }

            _entries = Array.AsReadOnly(entries);
        }

        public StageId FirstStageId => _entries[0].StageId;

        public StageId FinalStageId => _entries[_entries.Count - 1].StageId;

        public IReadOnlyList<CampaignStageSequenceSnapshotEntry> Entries => _entries;

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

        public CampaignStageSequenceSnapshotEntry GetEntryOrThrow(StageId stageId)
        {
            if (TryGetEntry(stageId, out var entry))
            {
                return entry;
            }

            throw new ArgumentException($"Stage id '{stageId.Value}' is not part of the campaign sequence.", nameof(stageId));
        }

        public bool TryGetEntry(StageId stageId, out CampaignStageSequenceSnapshotEntry entry)
        {
            if (stageId.IsValid && _indicesByStageId.TryGetValue(stageId, out var index))
            {
                entry = _entries[index];
                return true;
            }

            entry = default;
            return false;
        }
    }
}
