using System;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Stages
{
    public enum StageWorldGuideFacingMode
    {
        SurfaceAligned = 0,
        BillboardToCamera = 1,
        YawOnlyBillboard = 2,
    }

    [Serializable]
    public sealed class StageWorldGuideInstruction
    {
        [SerializeField] private bool enabled = true;
        [SerializeField] private string guideKey = string.Empty;
        [SerializeField] private SurfaceCell cell;
        [SerializeField] private Vector3 localOffset;
        [SerializeField] private float heightOffset = 0.25f;
        [SerializeField] private StageWorldGuideFacingMode facingMode = StageWorldGuideFacingMode.BillboardToCamera;
        [SerializeField] private bool hideWhenFaceInactive = true;

        public StageWorldGuideInstruction()
        {
        }

        public StageWorldGuideInstruction(
            bool enabled,
            string guideKey,
            SurfaceCell cell,
            Vector3 localOffset,
            float heightOffset,
            StageWorldGuideFacingMode facingMode,
            bool hideWhenFaceInactive)
        {
            this.enabled = enabled;
            this.guideKey = guideKey ?? string.Empty;
            this.cell = cell;
            this.localOffset = localOffset;
            this.heightOffset = heightOffset;
            this.facingMode = facingMode;
            this.hideWhenFaceInactive = hideWhenFaceInactive;
        }

        public bool Enabled => enabled;

        public string GuideKey => NormalizeGuideKey(guideKey);

        public SurfaceCell Cell => cell;

        public Vector3 LocalOffset => localOffset;

        public float HeightOffset => heightOffset;

        public StageWorldGuideFacingMode FacingMode => facingMode;

        public bool HideWhenFaceInactive => hideWhenFaceInactive;

        public static string NormalizeGuideKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    public readonly struct StageWorldGuideInstructionResolved
    {
        public StageWorldGuideInstructionResolved(
            string guideKey,
            SurfaceCell cell,
            Vector3 localOffset,
            float heightOffset,
            StageWorldGuideFacingMode facingMode,
            bool hideWhenFaceInactive)
        {
            GuideKey = StageWorldGuideInstruction.NormalizeGuideKey(guideKey);
            Cell = cell;
            LocalOffset = localOffset;
            HeightOffset = heightOffset;
            FacingMode = facingMode;
            HideWhenFaceInactive = hideWhenFaceInactive;
        }

        public string GuideKey { get; }

        public SurfaceCell Cell { get; }

        public Vector3 LocalOffset { get; }

        public float HeightOffset { get; }

        public StageWorldGuideFacingMode FacingMode { get; }

        public bool HideWhenFaceInactive { get; }
    }
}
