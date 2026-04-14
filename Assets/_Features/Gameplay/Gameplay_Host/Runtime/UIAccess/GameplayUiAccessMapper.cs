using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.UIAccess.Models;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    internal static class GameplayUiAccessMapper
    {
        public static Direction ToGameplayDirection(GameplayUiDirection direction)
        {
            return direction switch
            {
                GameplayUiDirection.Up => Direction.Up,
                GameplayUiDirection.Right => Direction.Right,
                GameplayUiDirection.Down => Direction.Down,
                GameplayUiDirection.Left => Direction.Left,
                _ => Direction.None,
            };
        }

        public static GameplayUiDirection ToUiDirection(Direction direction)
        {
            return direction switch
            {
                Direction.Up => GameplayUiDirection.Up,
                Direction.Right => GameplayUiDirection.Right,
                Direction.Down => GameplayUiDirection.Down,
                Direction.Left => GameplayUiDirection.Left,
                _ => GameplayUiDirection.None,
            };
        }

        public static GameplayUiActionKind ToUiActionKind(PlayerActionKind actionKind)
        {
            return actionKind switch
            {
                PlayerActionKind.Push => GameplayUiActionKind.Push,
                PlayerActionKind.Flip => GameplayUiActionKind.Flip,
                _ => GameplayUiActionKind.None,
            };
        }

        public static GameplayUiFace ToUiFace(FaceId face)
        {
            return face switch
            {
                FaceId.Front => GameplayUiFace.Front,
                FaceId.Ceiling => GameplayUiFace.Ceiling,
                FaceId.Back => GameplayUiFace.Back,
                _ => GameplayUiFace.Floor,
            };
        }

        public static GameplayUiRotationKind ToUiRotationKind(CubeRotationKind rotationKind)
        {
            return rotationKind switch
            {
                CubeRotationKind.Forward => GameplayUiRotationKind.Forward,
                CubeRotationKind.Backward => GameplayUiRotationKind.Backward,
                _ => GameplayUiRotationKind.None,
            };
        }

        public static GameplayUiTopology ToUiTopology(CubeTopologyState topology)
        {
            return new GameplayUiTopology(ToUiFace(topology.BottomFace));
        }
    }
}
