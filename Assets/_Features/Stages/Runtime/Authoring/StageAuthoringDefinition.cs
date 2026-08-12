using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Stages
{
    [CreateAssetMenu(menuName = "Gameplay/Stages/Stage Authoring Definition", fileName = "stage-authoring")]
    public sealed class StageAuthoringDefinition : StageCompanionDefinitionBase
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private StageDefinition generatedGameplayDefinition;
        [SerializeField] private StagePresentationDefinition generatedPresentationDefinition;
        [SerializeField] private StageBoardDefinition board = new()
        {
            InitialBottomFace = Game.Feature.Gameplay.BoardState.FaceId.Floor,
        };
        [SerializeField] private List<StagePlacedEntityAuthoring> placements = new();
        [SerializeField] private List<StageTileFeatureDefinition> tileFeatures = new();
        [SerializeField] private List<TileFeaturePresentationBinding> tileFeaturePresentationSelections = new();
        [SerializeField] private List<StageZoneDefinition> zones = new();
        [SerializeField] private StageObjectiveAuthoring objective = StageObjectiveAuthoring.CreateDefault();
        [SerializeField] private List<StageAuthoringIdMapping> entityIdMappings = new();
        [SerializeField] private bool enforceGeneratedSync;

        public int SchemaVersion => schemaVersion;

        public StageDefinition GeneratedGameplayDefinition => generatedGameplayDefinition;

        public StagePresentationDefinition GeneratedPresentationDefinition => generatedPresentationDefinition;

        public StageBoardDefinition Board => board;

        public IReadOnlyList<StagePlacedEntityAuthoring> Placements =>
            placements != null ? placements : Array.Empty<StagePlacedEntityAuthoring>();

        public IReadOnlyList<StageTileFeatureDefinition> TileFeatures =>
            tileFeatures != null ? tileFeatures : Array.Empty<StageTileFeatureDefinition>();

        public IReadOnlyList<TileFeaturePresentationBinding> TileFeaturePresentationSelections =>
            tileFeaturePresentationSelections != null
                ? tileFeaturePresentationSelections
                : Array.Empty<TileFeaturePresentationBinding>();

        public IReadOnlyList<StageZoneDefinition> Zones =>
            zones != null ? zones : Array.Empty<StageZoneDefinition>();

        public StageObjectiveAuthoring Objective => NormalizeObjective(objective);

        public IReadOnlyList<StageAuthoringIdMapping> EntityIdMappings =>
            entityIdMappings != null ? entityIdMappings : Array.Empty<StageAuthoringIdMapping>();

        public bool EnforceGeneratedSync => enforceGeneratedSync;

        public void AssignGeneratedDefinitions(
            StageDefinition gameplayDefinition,
            StagePresentationDefinition presentationDefinition)
        {
            generatedGameplayDefinition = gameplayDefinition;
            generatedPresentationDefinition = presentationDefinition;
        }

        public void SetBoard(StageBoardDefinition value)
        {
            board = value;
        }

        public void SetPlacements(IEnumerable<StagePlacedEntityAuthoring> value)
        {
            placements = value != null
                ? new List<StagePlacedEntityAuthoring>(value)
                : new List<StagePlacedEntityAuthoring>();
        }

        public void SetTileFeatures(IEnumerable<StageTileFeatureDefinition> value)
        {
            var existingSelectionsByTileId = new Dictionary<int, TileFeaturePresentationBinding>();
            var existingSelections = TileFeaturePresentationSelections;
            for (var i = 0; i < existingSelections.Count; i++)
            {
                var selection = existingSelections[i];
                if (selection != null &&
                    selection.TileId > 0 &&
                    !existingSelectionsByTileId.ContainsKey(selection.TileId))
                {
                    existingSelectionsByTileId.Add(selection.TileId, selection);
                }
            }

            tileFeatures = value != null
                ? new List<StageTileFeatureDefinition>(value)
                : new List<StageTileFeatureDefinition>();
            tileFeaturePresentationSelections = new List<TileFeaturePresentationBinding>(tileFeatures.Count);
            for (var i = 0; i < tileFeatures.Count; i++)
            {
                var tileId = tileFeatures[i].TileId;
                if (existingSelectionsByTileId.TryGetValue(tileId, out var existing))
                {
                    tileFeaturePresentationSelections.Add(existing);
                }
                else
                {
                    tileFeaturePresentationSelections.Add(new TileFeaturePresentationBinding
                    {
                        TileId = tileId,
                        PresentationKey = string.Empty,
                        VisualPrefab = null,
                    });
                }
            }
        }

        public void SetTileFeaturePresentationSelections(IEnumerable<TileFeaturePresentationBinding> value)
        {
            tileFeaturePresentationSelections = value != null
                ? new List<TileFeaturePresentationBinding>(value)
                : new List<TileFeaturePresentationBinding>();
        }

        public void SetZones(IEnumerable<StageZoneDefinition> value)
        {
            zones = value != null
                ? new List<StageZoneDefinition>(value)
                : new List<StageZoneDefinition>();
        }

        public void SetObjective(StageObjectiveAuthoring value)
        {
            objective = NormalizeObjective(value);
        }

        public void SetEntityIdMappings(IEnumerable<StageAuthoringIdMapping> value)
        {
            entityIdMappings = value != null
                ? new List<StageAuthoringIdMapping>(value)
                : new List<StageAuthoringIdMapping>();
        }

        public void SetEnforceGeneratedSync(bool value)
        {
            enforceGeneratedSync = value;
        }

        private static StageObjectiveAuthoring NormalizeObjective(StageObjectiveAuthoring value)
        {
            return new StageObjectiveAuthoring
            {
                CompletionPolicy = value.CompletionPolicy,
                ConditionEntries = NormalizeObjectiveConditionEntries(value.GetConditionEntriesOrEmpty()),
            };
        }

        private static StageObjectiveConditionEntry[] NormalizeObjectiveConditionEntries(
            StageObjectiveConditionEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
            {
                return Array.Empty<StageObjectiveConditionEntry>();
            }

            var normalized = new StageObjectiveConditionEntry[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                normalized[i] = entries[i];
                normalized[i].StableConditionId = entries[i].StableConditionId ?? string.Empty;
                normalized[i].AuthoringLabel = entries[i].AuthoringLabel ?? string.Empty;
            }

            return normalized;
        }
    }
}
