using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Stages
{
    [CreateAssetMenu(menuName = "Gameplay/Stages/Stage Authoring Definition", fileName = "stage-authoring")]
    public sealed class StageAuthoringDefinition : StageCompanionDefinitionBase
    {
        public const int CurrentSchemaVersion = 1;
        private const string DefaultPrimaryGoalDisplayText = "Reach the Exit Zone";

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private StageDefinition generatedGameplayDefinition;
        [SerializeField] private StagePresentationDefinition generatedPresentationDefinition;
        [SerializeField] private StageBoardDefinition board = new()
        {
            InitialBottomFace = Game.Feature.Gameplay.BoardState.FaceId.Floor,
        };
        [SerializeField] private List<StagePlacedEntityAuthoring> placements = new();
        [SerializeField] private List<StageTileFeatureDefinition> tileFeatures = new();
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
            tileFeatures = value != null
                ? new List<StageTileFeatureDefinition>(value)
                : new List<StageTileFeatureDefinition>();
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
                ObjectiveTitle = value.ObjectiveTitle ?? string.Empty,
                ObjectiveSummary = value.ObjectiveSummary ?? string.Empty,
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
                normalized[i].DisplayText = NormalizeConditionDisplayText(entries[i]);
            }

            return normalized;
        }

        private static string NormalizeConditionDisplayText(StageObjectiveConditionEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(entry.DisplayText))
            {
                return entry.DisplayText;
            }

            return entry.Role == Game.Feature.Gameplay.Objectives.StageObjectiveConditionRole.PrimaryGoal
                ? DefaultPrimaryGoalDisplayText
                : string.Empty;
        }
    }
}
