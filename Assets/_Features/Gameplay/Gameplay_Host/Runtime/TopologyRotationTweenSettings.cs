using System;

namespace Game.Feature.Gameplay.Host
{
    public enum TopologyRotationTweenEase
    {
        Linear = 0,
        InSine = 1,
        OutSine = 2,
        InOutSine = 3,
        InQuad = 4,
        OutQuad = 5,
        InOutQuad = 6,
        InCubic = 7,
        OutCubic = 8,
        InOutCubic = 9,
        InQuart = 10,
        OutQuart = 11,
        InOutQuart = 12,
        InQuint = 13,
        OutQuint = 14,
        InOutQuint = 15,
        InExpo = 16,
        OutExpo = 17,
        InOutExpo = 18,
        InCirc = 19,
        OutCirc = 20,
        InOutCirc = 21,
        InBack = 22,
        OutBack = 23,
        InOutBack = 24,
        InElastic = 25,
        OutElastic = 26,
        InOutElastic = 27,
        InBounce = 28,
        OutBounce = 29,
        InOutBounce = 30,
    }

    [Serializable]
    public struct TopologyRotationTweenSettings
    {
        public TopologyRotationTweenEase Ease;

        public static TopologyRotationTweenSettings CreateDefault()
        {
            return new TopologyRotationTweenSettings
            {
                Ease = TopologyRotationTweenEase.OutQuad,
            };
        }
    }
}
