using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct GameplayEntityPose
    {
        public GameplayEntityPose(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        public static GameplayEntityPose Lerp(GameplayEntityPose start, GameplayEntityPose end, float t)
        {
            var clampedT = Mathf.Clamp01(t);
            return new GameplayEntityPose(
                Vector3.LerpUnclamped(start.Position, end.Position, clampedT),
                Quaternion.SlerpUnclamped(start.Rotation, end.Rotation, clampedT));
        }
    }

    public readonly struct TransitionVisibilityState
    {
        public TransitionVisibilityState(
            TickTransitionVisibilityMode mode,
            GameplayEntityPose localPose)
        {
            Mode = mode;
            LocalPose = localPose;
        }

        public TickTransitionVisibilityMode Mode { get; }

        public GameplayEntityPose LocalPose { get; }
    }
}
