using System;
using UnityEngine;

namespace Game.Feature.Stages
{
    [DisallowMultipleComponent]
    public sealed class BackgroundSpaceOrbitAuthoring : MonoBehaviour
    {
        [Header("Orbit Targets")]
        [SerializeField] private Transform celestialOrbitPivot;
        [SerializeField] private Transform debrisOrbitPivot;
        [SerializeField] private Transform saturnSelfSpinTarget;

        [Header("Base Motion (degrees / second)")]
        [SerializeField] private float celestialDegreesPerSecond = 0.35f;
        [SerializeField] private float debrisDegreesPerSecond = -0.65f;
        [SerializeField] private float saturnDegreesPerSecond = 4f;

        [Header("Topology Response")]
        [SerializeField, Min(1f)] private float maximumTopologySpeedMultiplier = 1.35f;

        private Quaternion _celestialAuthoredRotation;
        private Quaternion _debrisAuthoredRotation;
        private Quaternion _saturnAuthoredRotation;
        private float _celestialAngle;
        private float _debrisAngle;
        private float _saturnAngle;
        private bool _isInitialized;

        public void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            _celestialAuthoredRotation = ResolveLocalRotation(celestialOrbitPivot);
            _debrisAuthoredRotation = ResolveLocalRotation(debrisOrbitPivot);
            _saturnAuthoredRotation = ResolveLocalRotation(saturnSelfSpinTarget);
            _isInitialized = true;
        }

        public void Advance(float deltaTime, float topologyResponse01)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaTime),
                    deltaTime,
                    "Background orbit delta time cannot be negative.");
            }

            Initialize();

            var response = Mathf.Clamp01(topologyResponse01);
            var speedMultiplier = Mathf.Lerp(
                1f,
                Mathf.Max(1f, maximumTopologySpeedMultiplier),
                response);

            _celestialAngle = RepeatAngle(
                _celestialAngle + celestialDegreesPerSecond * speedMultiplier * deltaTime);
            _debrisAngle = RepeatAngle(
                _debrisAngle + debrisDegreesPerSecond * speedMultiplier * deltaTime);
            _saturnAngle = RepeatAngle(
                _saturnAngle + saturnDegreesPerSecond * deltaTime);

            ApplyLocalAxisRotation(celestialOrbitPivot, _celestialAuthoredRotation, _celestialAngle, Vector3.right);
            ApplyLocalAxisRotation(debrisOrbitPivot, _debrisAuthoredRotation, _debrisAngle, Vector3.right);
            ApplyLocalAxisRotation(saturnSelfSpinTarget, _saturnAuthoredRotation, _saturnAngle, Vector3.up);
        }

        public void ResetRuntimeState()
        {
            if (!_isInitialized)
            {
                return;
            }

            _celestialAngle = 0f;
            _debrisAngle = 0f;
            _saturnAngle = 0f;
            RestoreLocalRotation(celestialOrbitPivot, _celestialAuthoredRotation);
            RestoreLocalRotation(debrisOrbitPivot, _debrisAuthoredRotation);
            RestoreLocalRotation(saturnSelfSpinTarget, _saturnAuthoredRotation);
        }

        private static Quaternion ResolveLocalRotation(Transform target)
        {
            return target != null ? target.localRotation : Quaternion.identity;
        }

        private static float RepeatAngle(float angle)
        {
            return Mathf.Repeat(angle, 360f);
        }

        private static void ApplyLocalAxisRotation(
            Transform target,
            Quaternion authoredRotation,
            float angle,
            Vector3 axis)
        {
            if (target != null)
            {
                target.localRotation = authoredRotation * Quaternion.AngleAxis(angle, axis);
            }
        }

        private static void RestoreLocalRotation(Transform target, Quaternion authoredRotation)
        {
            if (target != null)
            {
                target.localRotation = authoredRotation;
            }
        }
    }
}
