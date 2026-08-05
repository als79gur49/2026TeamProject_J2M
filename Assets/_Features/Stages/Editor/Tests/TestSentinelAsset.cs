using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    internal sealed class TestSentinelAsset : ScriptableObject
    {
        [SerializeField]
        private string value;

        public void SetValue(string nextValue)
        {
            value = nextValue;
        }
    }
}
