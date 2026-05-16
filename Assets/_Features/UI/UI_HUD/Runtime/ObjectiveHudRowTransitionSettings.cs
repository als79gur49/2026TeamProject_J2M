using System;

namespace Game.Feature.UI.HUD
{
    [Serializable]
    public sealed class ObjectiveHudRowTransitionSettings
    {
        public float EnterDuration = 0.5f;
        public float CollapseDuration = 0.3f;
        public float FullHeight = 60.0f;
        public string ActiveBoolParameter = "Active";
        public string InStateName = "In";
        public string ActiveStateName = "Active";
        public string OutStateName = "Out";
        public string InactiveStateName = "Inactive";
    }
}
