using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Gameplay.UIAccess.Queries;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    internal sealed class GameplayHostSurfaceButtonRemainderQuery : IGameplaySurfaceButtonRemainderQuery
    {
        private static readonly GameplaySurfaceButtonRemainderReadModel[] Empty =
        {
            new(GameplayUiFace.Floor, 0, 0),
            new(GameplayUiFace.Front, 0, 0),
            new(GameplayUiFace.Ceiling, 0, 0),
            new(GameplayUiFace.Back, 0, 0),
        };

        private readonly GameplayHostCommandAdmissionPolicy _admissionPolicy;
        private readonly IReadOnlyDictionary<int, TileFeatureRuntimeDefinition> _definitionsByTileId;
        private readonly List<TileFeatureState> _tileFeatureBuffer = new();

        public GameplayHostSurfaceButtonRemainderQuery(
            GameplayHostCommandAdmissionPolicy admissionPolicy,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
        {
            _admissionPolicy = admissionPolicy;
            _definitionsByTileId = BuildDefinitionLookup(tileFeatureDefinitions);
        }

        public IReadOnlyList<GameplaySurfaceButtonRemainderReadModel> Read()
        {
            if (_admissionPolicy == null ||
                !_admissionPolicy.TryCreateSnapshot(out var snapshot))
            {
                return Empty;
            }

            var normalByFace = new int[Empty.Length];
            var moonBlockOnlyByFace = new int[Empty.Length];
            snapshot.EnumerateTileFeaturesOrdered(_tileFeatureBuffer);
            for (var i = 0; i < _tileFeatureBuffer.Count; i++)
            {
                var tileFeature = _tileFeatureBuffer[i];
                if (tileFeature.Kind != TileFeatureKind.Button ||
                    (tileFeature.Flags & TileFeatureFlags.Activated) != 0)
                {
                    continue;
                }

                if (!_definitionsByTileId.TryGetValue(tileFeature.TileId, out var definition))
                {
                    throw new InvalidOperationException(
                        $"Button TileFeature {tileFeature.TileId} is missing a {nameof(TileFeatureRuntimeDefinition)}.");
                }

                var face = GameplayUiAccessMapper.ToUiFace(tileFeature.Cell.face);
                var faceIndex = (int)face;
                if (definition.BoxSelector == TileFeatureBoxSelector.MoonBlockOnly)
                {
                    moonBlockOnlyByFace[faceIndex]++;
                }
                else
                {
                    normalByFace[faceIndex]++;
                }
            }

            return new[]
            {
                new GameplaySurfaceButtonRemainderReadModel(GameplayUiFace.Floor, normalByFace[0], moonBlockOnlyByFace[0]),
                new GameplaySurfaceButtonRemainderReadModel(GameplayUiFace.Front, normalByFace[1], moonBlockOnlyByFace[1]),
                new GameplaySurfaceButtonRemainderReadModel(GameplayUiFace.Ceiling, normalByFace[2], moonBlockOnlyByFace[2]),
                new GameplaySurfaceButtonRemainderReadModel(GameplayUiFace.Back, normalByFace[3], moonBlockOnlyByFace[3]),
            };
        }

        private static IReadOnlyDictionary<int, TileFeatureRuntimeDefinition> BuildDefinitionLookup(
            IReadOnlyList<TileFeatureRuntimeDefinition> definitions)
        {
            var lookup = new Dictionary<int, TileFeatureRuntimeDefinition>();
            if (definitions == null)
            {
                return lookup;
            }

            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition.TileId <= 0)
                {
                    continue;
                }

                if (lookup.ContainsKey(definition.TileId))
                {
                    throw new InvalidOperationException(
                        $"Duplicate {nameof(TileFeatureRuntimeDefinition)} for TileId {definition.TileId}.");
                }

                lookup.Add(definition.TileId, definition);
            }

            return lookup;
        }
    }
}
