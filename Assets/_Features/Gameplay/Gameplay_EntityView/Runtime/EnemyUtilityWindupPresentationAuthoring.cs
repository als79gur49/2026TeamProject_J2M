using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct EnemyUtilityWindupPresentationSnapshot
    {
        public EnemyUtilityWindupPresentationSnapshot(
            GameObject summonWindupWarningPrefab,
            float summonWindupWarningSeconds,
            bool attachSummonWarningToSourceView,
            Vector3 localOffset,
            string attachSlot)
        {
            SummonWindupWarningPrefab = summonWindupWarningPrefab;
            SummonWindupWarningSeconds = summonWindupWarningSeconds;
            AttachSummonWarningToSourceView = attachSummonWarningToSourceView;
            LocalOffset = localOffset;
            AttachSlot = attachSlot ?? string.Empty;
        }

        public GameObject SummonWindupWarningPrefab { get; }

        public float SummonWindupWarningSeconds { get; }

        public bool AttachSummonWarningToSourceView { get; }

        public Vector3 LocalOffset { get; }

        public string AttachSlot { get; }

        public bool HasSummonWindupWarningPrefab => SummonWindupWarningPrefab != null;
    }

    [DisallowMultipleComponent]
    public sealed class EnemyUtilityWindupPresentationAuthoring : MonoBehaviour
    {
        [SerializeField] private GameObject summonWindupWarningPrefab;
        [SerializeField] private float summonWindupWarningSeconds = 0.15f;
        [SerializeField] private bool attachSummonWarningToSourceView = true;
        [SerializeField] private Vector3 localOffset;
        [SerializeField] private string attachSlot = string.Empty;

        public GameObject SummonWindupWarningPrefab => summonWindupWarningPrefab;

        public float SummonWindupWarningSeconds => summonWindupWarningSeconds;

        public bool AttachSummonWarningToSourceView => attachSummonWarningToSourceView;

        public Vector3 LocalOffset => localOffset;

        public string AttachSlot => attachSlot ?? string.Empty;

        public void Validate()
        {
            if (summonWindupWarningSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(summonWindupWarningSeconds),
                    "Summon windup warning presentation timing must be zero or greater.");
            }
        }

        public EnemyUtilityWindupPresentationSnapshot CreateSnapshot()
        {
            Validate();
            return new EnemyUtilityWindupPresentationSnapshot(
                summonWindupWarningPrefab,
                summonWindupWarningSeconds,
                attachSummonWarningToSourceView,
                localOffset,
                attachSlot);
        }

        public static EnemyUtilityWindupPresentationAuthoring GetOptionalValidatedAuthoring(
            GameplayEntityView entityView)
        {
            if (entityView == null)
            {
                throw new ArgumentNullException(nameof(entityView));
            }

            if (!entityView.TryGetComponent<EnemyUtilityWindupPresentationAuthoring>(out var authoring) ||
                authoring == null)
            {
                return null;
            }

            authoring.Validate();
            return authoring;
        }
    }
}
