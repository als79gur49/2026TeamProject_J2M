using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct EnemyFrontFaceShieldPresentationSnapshot
    {
        public EnemyFrontFaceShieldPresentationSnapshot(
            GameObject telegraphPrefab,
            GameObject activeLoopPrefab,
            GameObject blockBurstPrefab,
            float telegraphSeconds,
            float activeFadeInSeconds,
            float activeFadeOutSeconds,
            float blockBurstSeconds,
            bool attachActiveLoopToSourceView,
            bool scaleByRadius,
            Vector3 localOffset,
            string attachSlot)
        {
            TelegraphPrefab = telegraphPrefab;
            ActiveLoopPrefab = activeLoopPrefab;
            BlockBurstPrefab = blockBurstPrefab;
            TelegraphSeconds = telegraphSeconds;
            ActiveFadeInSeconds = activeFadeInSeconds;
            ActiveFadeOutSeconds = activeFadeOutSeconds;
            BlockBurstSeconds = blockBurstSeconds;
            AttachActiveLoopToSourceView = attachActiveLoopToSourceView;
            ScaleByRadius = scaleByRadius;
            LocalOffset = localOffset;
            AttachSlot = attachSlot ?? string.Empty;
        }

        public GameObject TelegraphPrefab { get; }

        public GameObject ActiveLoopPrefab { get; }

        public GameObject BlockBurstPrefab { get; }

        public float TelegraphSeconds { get; }

        public float ActiveFadeInSeconds { get; }

        public float ActiveFadeOutSeconds { get; }

        public float BlockBurstSeconds { get; }

        public bool AttachActiveLoopToSourceView { get; }

        public bool ScaleByRadius { get; }

        public Vector3 LocalOffset { get; }

        public string AttachSlot { get; }

        public bool HasTelegraphPrefab => TelegraphPrefab != null;

        public bool HasActiveLoopPrefab => ActiveLoopPrefab != null;

        public bool HasBlockBurstPrefab => BlockBurstPrefab != null;
    }

    [DisallowMultipleComponent]
    public sealed class EnemyFrontFaceShieldPresentationAuthoring : MonoBehaviour
    {
        [SerializeField] private GameObject telegraphPrefab;
        [SerializeField] private GameObject activeLoopPrefab;
        [SerializeField] private GameObject blockBurstPrefab;
        [SerializeField] private float telegraphSeconds = 0.15f;
        [SerializeField] private float activeFadeInSeconds = 0.1f;
        [SerializeField] private float activeFadeOutSeconds = 0.12f;
        [SerializeField] private float blockBurstSeconds = 0.25f;
        [SerializeField] private bool attachActiveLoopToSourceView = true;
        [SerializeField] private bool scaleByRadius = true;
        [SerializeField] private Vector3 localOffset;
        [SerializeField] private string attachSlot = string.Empty;

        public GameObject TelegraphPrefab => telegraphPrefab;

        public GameObject ActiveLoopPrefab => activeLoopPrefab;

        public GameObject BlockBurstPrefab => blockBurstPrefab;

        public float TelegraphSeconds => telegraphSeconds;

        public float ActiveFadeInSeconds => activeFadeInSeconds;

        public float ActiveFadeOutSeconds => activeFadeOutSeconds;

        public float BlockBurstSeconds => blockBurstSeconds;

        public bool AttachActiveLoopToSourceView => attachActiveLoopToSourceView;

        public bool ScaleByRadius => scaleByRadius;

        public Vector3 LocalOffset => localOffset;

        public string AttachSlot => attachSlot ?? string.Empty;

        public void Validate()
        {
            ValidateNonNegative(telegraphSeconds, nameof(telegraphSeconds));
            ValidateNonNegative(activeFadeInSeconds, nameof(activeFadeInSeconds));
            ValidateNonNegative(activeFadeOutSeconds, nameof(activeFadeOutSeconds));
            ValidateNonNegative(blockBurstSeconds, nameof(blockBurstSeconds));
        }

        public EnemyFrontFaceShieldPresentationSnapshot CreateSnapshot()
        {
            Validate();
            return new EnemyFrontFaceShieldPresentationSnapshot(
                telegraphPrefab,
                activeLoopPrefab,
                blockBurstPrefab,
                telegraphSeconds,
                activeFadeInSeconds,
                activeFadeOutSeconds,
                blockBurstSeconds,
                attachActiveLoopToSourceView,
                scaleByRadius,
                localOffset,
                attachSlot);
        }

        public static EnemyFrontFaceShieldPresentationAuthoring GetOptionalValidatedAuthoring(
            GameplayEntityView entityView)
        {
            if (entityView == null)
            {
                throw new ArgumentNullException(nameof(entityView));
            }

            if (!entityView.TryGetComponent<EnemyFrontFaceShieldPresentationAuthoring>(out var authoring) ||
                authoring == null)
            {
                return null;
            }

            authoring.Validate();
            return authoring;
        }

        private static void ValidateNonNegative(float value, string parameterName)
        {
            if (value < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Shield presentation timing values must be zero or greater.");
            }
        }
    }
}
