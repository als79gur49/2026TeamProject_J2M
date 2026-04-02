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
