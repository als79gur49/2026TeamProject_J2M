using System;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.BlockAudio;
using Game.Feature.Gameplay.GravityFieldAudio;
using Game.Feature.Gameplay.PlayerLocomotionAudio;
using Game.Feature.Gameplay.TileFeatureAudio;
using Game.Feature.Gameplay.TopologyAudio;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [CreateAssetMenu(
        fileName = "GameplayPresentationAudioConfig",
        menuName = "Game/Gameplay/Presentation Audio Config")]
    public sealed class GameplayPresentationAudioConfig : ScriptableObject
    {
        [SerializeField] private GameplayAudioMap gameplayAudioMap;
        [SerializeField] private BlockAudioMap blockAudioMap;
        [SerializeField] private PlayerLocomotionAudioMap playerLocomotionAudioMap;
        [SerializeField] private TopologyAudioMap topologyAudioMap;
        [SerializeField] private GravityFieldAudioMap gravityFieldAudioMap;
        [SerializeField] private TileFeatureAudioMap tileFeatureAudioMap;

        public GameplayAudioMap GameplayAudioMap => gameplayAudioMap;

        public BlockAudioMap BlockAudioMap => blockAudioMap;

        public PlayerLocomotionAudioMap PlayerLocomotionAudioMap => playerLocomotionAudioMap;

        public TopologyAudioMap TopologyAudioMap => topologyAudioMap;

        public GravityFieldAudioMap GravityFieldAudioMap => gravityFieldAudioMap;

        public TileFeatureAudioMap TileFeatureAudioMap => tileFeatureAudioMap;

        public void ValidateOrThrow()
        {
            if (gameplayAudioMap == null)
            {
                throw new InvalidOperationException($"{name}: gameplayAudioMap is required.");
            }

            if (blockAudioMap == null)
            {
                throw new InvalidOperationException($"{name}: blockAudioMap is required.");
            }

            if (playerLocomotionAudioMap == null)
            {
                throw new InvalidOperationException($"{name}: playerLocomotionAudioMap is required.");
            }

            if (topologyAudioMap == null)
            {
                throw new InvalidOperationException($"{name}: topologyAudioMap is required.");
            }

            if (gravityFieldAudioMap == null)
            {
                throw new InvalidOperationException($"{name}: gravityFieldAudioMap is required.");
            }

            if (tileFeatureAudioMap == null)
            {
                throw new InvalidOperationException($"{name}: tileFeatureAudioMap is required.");
            }

            gameplayAudioMap.ValidateRequiredSemanticsOrThrow(
                GameplayAudioSemanticCatalog.RequiredOneShotV1);
            blockAudioMap.ValidateRequiredCuesOrThrow(
                BlockAudioCueCatalog.RequiredOneShotV1);
            playerLocomotionAudioMap.ValidateRequiredCuesOrThrow(
                PlayerLocomotionAudioCueCatalog.RequiredOneShotV1);
            topologyAudioMap.ValidateRequiredCuesOrThrow(
                TopologyAudioCueCatalog.RequiredOneShotV1);
            gravityFieldAudioMap.ValidateRequiredCuesOrThrow(
                GravityFieldAudioCueCatalog.RequiredOneShotV1);
            tileFeatureAudioMap.ValidateRequiredCuesOrThrow(
                TileFeatureAudioCueCatalog.RequiredOneShotV1);
        }
    }
}
