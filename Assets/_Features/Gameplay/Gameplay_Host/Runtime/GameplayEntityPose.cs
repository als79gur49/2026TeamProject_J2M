using Game.Feature.Gameplay;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Entities;
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
            GameplayEntityPose localPose,
            GameplayProjectedFaceSlot? projectedSlot)
        {
            Mode = mode;
            LocalPose = localPose;
            ProjectedSlot = projectedSlot;
        }

        public TickTransitionVisibilityMode Mode { get; }

        public GameplayEntityPose LocalPose { get; }

        public GameplayProjectedFaceSlot? ProjectedSlot { get; }
    }

    public readonly struct JumpDetachedVisibilityState
    {
        public JumpDetachedVisibilityState(
            EnemyJumpPhase jumpPhase,
            GameplayEntityPose localPose)
        {
            JumpPhase = jumpPhase;
            LocalPose = localPose;
        }

        public EnemyJumpPhase JumpPhase { get; }

        public GameplayEntityPose LocalPose { get; }
    }

    public sealed class JumpTrack
    {
        private JumpClip _clip;

        public bool HasClip => _clip != null;

        public GameplayEntityPose CurrentPose => _clip != null
            ? _clip.Sample()
            : default;

        public GameplayEntityPose EndPose => _clip != null
            ? _clip.EndPose
            : default;

        public float RemainingSeconds => _clip != null
            ? _clip.RemainingSeconds
            : 0f;

        public void Clear()
        {
            _clip = null;
        }

        public void Replace(JumpClip clip)
        {
            _clip = clip ?? throw new System.ArgumentNullException(nameof(clip));
        }

        public GameplayEntityPose SampleAndAdvance(float deltaTime, GameplayEntityPose fallbackPose)
        {
            if (_clip == null)
            {
                return fallbackPose;
            }

            _clip.Advance(deltaTime);
            var pose = _clip.IsComplete
                ? _clip.EndPose
                : _clip.Sample();
            if (_clip.IsComplete)
            {
                _clip = null;
            }

            return pose;
        }
    }

    public sealed class JumpClip
    {
        private readonly float _arcHeightWorld;

        private JumpClip(
            GameplayEntityPose startPose,
            GameplayEntityPose endPose,
            float durationSeconds,
            float arcHeightWorld)
        {
            StartPose = startPose;
            EndPose = endPose;
            DurationSeconds = Mathf.Max(durationSeconds, 0.0001f);
            ElapsedSeconds = 0f;
            _arcHeightWorld = Mathf.Max(0f, arcHeightWorld);
        }

        public float DurationSeconds { get; }

        public float ElapsedSeconds { get; private set; }

        public GameplayEntityPose EndPose { get; }

        public bool IsComplete => RemainingSeconds <= 0.0001f;

        public float RemainingSeconds => Mathf.Max(0f, DurationSeconds - ElapsedSeconds);

        public GameplayEntityPose StartPose { get; }

        public static JumpClip Create(
            GameplayEntityPose startPose,
            GameplayEntityPose endPose,
            float durationSeconds,
            float arcHeightWorld)
        {
            return new JumpClip(startPose, endPose, durationSeconds, arcHeightWorld);
        }

        public void Advance(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            ElapsedSeconds = Mathf.Min(DurationSeconds, ElapsedSeconds + deltaTime);
        }

        public GameplayEntityPose Sample()
        {
            var t = DurationSeconds <= 0f
                ? 1f
                : Mathf.Clamp01(ElapsedSeconds / DurationSeconds);
            var liftAxis = ResolveLiftAxis();
            var position = Vector3.LerpUnclamped(StartPose.Position, EndPose.Position, t);
            if (_arcHeightWorld > 0f)
            {
                position += liftAxis * (4f * t * (1f - t) * _arcHeightWorld);
            }

            return new GameplayEntityPose(
                position,
                Quaternion.SlerpUnclamped(StartPose.Rotation, EndPose.Rotation, t));
        }

        private Vector3 ResolveLiftAxis()
        {
            // Entity poses sit offset against the projected face normal, so the visual
            // jump lift has to travel opposite the pose forward/surface-normal axis.
            var startNormal = -(StartPose.Rotation * Vector3.forward);
            var endNormal = -(EndPose.Rotation * Vector3.forward);
            var combined = startNormal + endNormal;
            if (combined.sqrMagnitude > 0.000001f)
            {
                return combined.normalized;
            }

            if (startNormal.sqrMagnitude > 0.000001f)
            {
                return startNormal.normalized;
            }

            return Vector3.back;
        }
    }
}
