using System;

namespace Game.Feature.Gameplay.BoardState
{
    public static class TileFeatureActivationQueries
    {
        public static bool IsSupportedDestroyActivation(TileFeatureActivationRule activationRule)
        {
            return activationRule == TileFeatureActivationRule.BottomFaceOnly ||
                   activationRule == TileFeatureActivationRule.FrontFaceOnly ||
                   activationRule == TileFeatureActivationRule.ActiveFaceOnly ||
                   activationRule == TileFeatureActivationRule.InactiveFaceOnly;
        }

        public static bool IsActive(
            TileFeatureState state,
            TileFeatureRuntimeDefinition definition,
            CubeTopologyState topology)
        {
            if (state.TileId != definition.TileId)
            {
                return false;
            }

            switch (definition.ActivationRule)
            {
                case TileFeatureActivationRule.Always:
                    return true;
                case TileFeatureActivationRule.BottomFaceOnly:
                    return state.Cell.face == topology.BottomFace;
                case TileFeatureActivationRule.FrontFaceOnly:
                    return state.Cell.face == topology.FrontFace;
                case TileFeatureActivationRule.ActiveFaceOnly:
                    return topology.IsFaceActive(state.Cell.face);
                case TileFeatureActivationRule.InactiveFaceOnly:
                    return !topology.IsFaceActive(state.Cell.face);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(definition),
                        definition.ActivationRule,
                        "Unknown TileFeature activation rule.");
            }
        }
    }
}
