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
                ConditionEntries = value.GetConditionEntriesOrEmpty(),
            };
        }
    }
}
