using System;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Stages
{
    public sealed class BackgroundWallSurfaceTintNoiseController
    {
        private readonly BackgroundWallSurfaceTintAuthoring _authoring;
        private FaceId _lastAppliedFace;
        private bool _hasAppliedFace;
        private FaceId _activeSourceFace;
        private FaceId _activeFace;
        private float _elapsedSeconds;
        private float _durationSeconds;
        private int _restartSequence;

        public BackgroundWallSurfaceTintNoiseController(BackgroundWallSurfaceTintAuthoring authoring)
        {
            _authoring = authoring ?? throw new ArgumentNullException(nameof(authoring));
        }

        public bool IsOverlayActive { get; private set; }

        internal FaceId DebugLastAppliedFace => _lastAppliedFace;

        internal FaceId DebugActiveSourceFace => _activeSourceFace;

        internal FaceId DebugActiveFace => _activeFace;

        internal float DebugElapsedSeconds => _elapsedSeconds;

        internal int DebugRestartSequence => _restartSequence;

        public void ApplyInitialStableFace(FaceId face)
        {
            StopOverlay();
            ApplyBaseFace(face);
        }

        public void ObservePresentationFace(
            FaceId face,
            bool isTopologyTransitionActive,
            float topologyTransitionDurationSeconds)
        {
            if (!isTopologyTransitionActive)
            {
                if (!_hasAppliedFace || !_lastAppliedFace.Equals(face))
                {
                    StopOverlay();
                    ApplyBaseFace(face);
                }

                return;
            }

            if (!_hasAppliedFace)
            {
                ApplyBaseFace(face);
                return;
            }

            if (_lastAppliedFace.Equals(face) && (!IsOverlayActive || _activeFace.Equals(face)))
            {
                return;
            }

            StartOverlay(_lastAppliedFace, face, ResolveDurationSeconds(topologyTransitionDurationSeconds));
        }

        public void Advance(float deltaTime)
        {
            if (!IsOverlayActive)
            {
                return;
            }

            _elapsedSeconds = Mathf.Min(_durationSeconds, _elapsedSeconds + Mathf.Max(0f, deltaTime));
            if (_elapsedSeconds >= _durationSeconds)
            {
                StopOverlay();
                ApplyBaseFace(_activeFace);
                return;
            }

            ApplyOverlay();
        }

        public void Reset()
        {
            var shouldReapply = _hasAppliedFace;
            var face = _lastAppliedFace;
            StopOverlay();
            if (shouldReapply)
            {
                ApplyBaseFace(face);
            }
        }

        private void StartOverlay(FaceId sourceFace, FaceId destinationFace, float durationSeconds)
        {
            var profile = _authoring.NoiseProfile;
            if (profile == null || !profile.Enabled)
            {
                StopOverlay();
                ApplyBaseFace(destinationFace);
                return;
            }

            _activeSourceFace = sourceFace;
            _activeFace = destinationFace;
            _lastAppliedFace = destinationFace;
            _hasAppliedFace = true;
            _elapsedSeconds = 0f;
            _durationSeconds = Mathf.Max(0.0001f, durationSeconds);
            _restartSequence++;

            IsOverlayActive = true;
            ApplyOverlay();
        }

        private void ApplyOverlay()
        {
            var profile = _authoring.NoiseProfile;
            if (profile == null || !profile.Enabled)
            {
                StopOverlay();
                ApplyBaseFace(_activeFace);
                return;
            }

            _authoring.ApplyFaceTint(
                _activeFace,
                BackgroundWallSurfaceNoiseOverlay.Active(
                    profile,
                    _activeSourceFace,
                    _elapsedSeconds,
                    _durationSeconds,
                    _restartSequence));
        }

        private void ApplyBaseFace(FaceId face)
        {
            _authoring.ApplyFaceTint(face);
            _lastAppliedFace = face;
            _hasAppliedFace = true;
        }

        private void StopOverlay()
        {
            IsOverlayActive = false;
            _activeSourceFace = default;
            _elapsedSeconds = 0f;
            _durationSeconds = 0f;
        }

        private float ResolveDurationSeconds(float topologyTransitionDurationSeconds)
        {
            var profile = _authoring.NoiseProfile;
            if (profile != null &&
                profile.UseTopologyTransitionDuration &&
                topologyTransitionDurationSeconds > 0f)
            {
                return topologyTransitionDurationSeconds;
            }

            return profile != null ? profile.DurationSeconds : 0f;
        }
    }

    public readonly struct BackgroundWallSurfaceNoiseOverlay
    {
        private readonly BackgroundWallSurfaceNoiseProfile _profile;
        private readonly FaceId _sourceFace;
        private readonly float _elapsedSeconds;
        private readonly float _durationSeconds;
        private readonly int _sequence;

        private BackgroundWallSurfaceNoiseOverlay(
            BackgroundWallSurfaceNoiseProfile profile,
            FaceId sourceFace,
            float elapsedSeconds,
            float durationSeconds,
            int sequence)
        {
            _profile = profile;
            _sourceFace = sourceFace;
            _elapsedSeconds = elapsedSeconds;
            _durationSeconds = Mathf.Max(0.0001f, durationSeconds);
            _sequence = sequence;
            IsActive = profile != null && profile.Enabled;
        }

        public bool IsActive { get; }

        public FaceId SourceFace => _sourceFace;

        public static BackgroundWallSurfaceNoiseOverlay Inactive => default;

        public static BackgroundWallSurfaceNoiseOverlay Active(
            BackgroundWallSurfaceNoiseProfile profile,
            FaceId sourceFace,
            float elapsedSeconds,
            float durationSeconds,
            int sequence)
        {
            return new BackgroundWallSurfaceNoiseOverlay(profile, sourceFace, elapsedSeconds, durationSeconds, sequence);
        }

        public Color ApplyBaseColor(
            Color sourceColor,
            Color destinationColor,
            FaceId destinationFace,
            BackgroundWallSurfaceTintTarget target,
            int targetIndex)
        {
            if (!ShouldApply(target) || !target.NoiseAffectsBaseColor)
            {
                return destinationColor;
            }

            var amount = ResolveAmount(
                _profile.BaseColorNoiseAmount,
                target.HasBaseNoiseStrengthOverride,
                target.BaseNoiseStrengthOverride);
            if (amount <= 0f)
            {
                return destinationColor;
            }

            return ApplyRgbOverlay(
                sourceColor,
                destinationColor,
                destinationFace,
                target,
                targetIndex,
                amount,
                channelSalt: 17);
        }

        public Color ApplyEmissionColor(
            Color sourceColor,
            Color destinationColor,
            FaceId destinationFace,
            BackgroundWallSurfaceTintTarget target,
            int targetIndex)
        {
            if (!ShouldApply(target) || !target.NoiseAffectsEmission)
            {
                return destinationColor;
            }

            var amount = ResolveAmount(
                _profile.EmissionNoiseAmount,
                target.HasEmissionNoiseStrengthOverride,
                target.EmissionNoiseStrengthOverride);
            if (amount <= 0f)
            {
                return destinationColor;
            }

            return ApplyRgbOverlay(
                sourceColor,
                destinationColor,
                destinationFace,
                target,
                targetIndex,
                amount,
                channelSalt: 53);
        }

        private bool ShouldApply(BackgroundWallSurfaceTintTarget target)
        {
            return IsActive &&
                   _profile != null &&
                   target != null &&
                   target.ApplyNoise;
        }

        private Color ApplyRgbOverlay(
            Color sourceColor,
            Color destinationColor,
            FaceId destinationFace,
            BackgroundWallSurfaceTintTarget target,
            int targetIndex,
            float amount,
            int channelSalt)
        {
            var normalizedTime = Mathf.Clamp01(_elapsedSeconds / _durationSeconds);
            var progress = _profile.EvaluateProgress(normalizedTime);
            var endpointFade = 1f - Mathf.Abs((normalizedTime * 2f) - 1f);
            var strength = amount * _profile.NoiseStrength * endpointFade;
            var signedNoise = SampleSignedNoise(destinationFace, target, targetIndex, channelSalt);
            var blend = Mathf.Clamp01(progress + (signedNoise * strength));
            var color = Color.Lerp(sourceColor, destinationColor, blend);
            color.a = destinationColor.a;
            return color;
        }

        private float SampleSignedNoise(
            FaceId destinationFace,
            BackgroundWallSurfaceTintTarget target,
            int targetIndex,
            int channelSalt)
        {
            var seed = _profile.Seed + channelSalt + (_sequence * 13);
            switch (_profile.SeedMode)
            {
                case BackgroundWallSurfaceNoiseSeedMode.FaceOnly:
                    seed += (int)destinationFace * 101;
                    break;
                case BackgroundWallSurfaceNoiseSeedMode.FaceAndTarget:
                    seed += (int)destinationFace * 101;
                    seed += targetIndex * 37;
                    seed += target.MaterialIndex * 19;
                    break;
            }

            var x = (seed % 997) * 0.0137f;
            var y = (_elapsedSeconds * _profile.NoiseFrequency) + ((seed % 577) * 0.0191f);
            return (Mathf.PerlinNoise(x, y) * 2f) - 1f;
        }

        private static float ResolveAmount(float profileAmount, bool hasOverride, float overrideAmount)
        {
            return Mathf.Max(0f, hasOverride ? overrideAmount : profileAmount);
        }
    }
}
