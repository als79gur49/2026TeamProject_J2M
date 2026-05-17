using System;
using UnityEngine;

namespace Game.Feature.UI.HUD
{
    [CreateAssetMenu(
        fileName = "SurfaceBeltStyleProfile",
        menuName = "Game/UI/HUD/Surface Belt Style Profile")]
    public sealed class SurfaceBeltStyleProfile : ScriptableObject
    {
        [Serializable]
        public struct SlotStyle
        {
            [SerializeField] private int _slotIndex;
            [SerializeField] private Color _color;

            public SlotStyle(int slotIndex, Color color)
            {
                _slotIndex = slotIndex;
                _color = color;
            }

            public int SlotIndex => _slotIndex;

            public Color Color => _color;
        }

        [SerializeField]
        private SlotStyle[] _slotStyles =
        {
            new SlotStyle(0, new Color(0.2196079f, 0.7568628f, 0.4470589f, 1.0f)),
            new SlotStyle(1, new Color(0.2313726f, 0.509804f, 0.9647059f, 1.0f)),
            new SlotStyle(2, new Color(0.9607844f, 0.7725491f, 0.2588235f, 1.0f)),
            new SlotStyle(3, new Color(0.6941177f, 0.3607844f, 1.0f, 1.0f)),
        };

        public bool TryGetStyle(int slotIndex, out SlotStyle style)
        {
            var normalizedSlot = SurfaceBeltSlotMapping.WrapSlot(slotIndex);
            var styles = _slotStyles ?? Array.Empty<SlotStyle>();
            for (var i = 0; i < styles.Length; i++)
            {
                if (styles[i].SlotIndex == normalizedSlot)
                {
                    style = styles[i];
                    return true;
                }
            }

            style = default;
            return false;
        }

        public bool TryValidate(out string message)
        {
            var seen = new bool[SurfaceBeltSlotMapping.SurfaceCount];
            var styles = _slotStyles ?? Array.Empty<SlotStyle>();
            for (var i = 0; i < styles.Length; i++)
            {
                var slot = styles[i].SlotIndex;
                if (slot < 0 || slot >= SurfaceBeltSlotMapping.SurfaceCount)
                {
                    message = $"Surface belt style slot {slot} is outside 0..3.";
                    return false;
                }

                if (seen[slot])
                {
                    message = $"Surface belt style slot {slot} is duplicated.";
                    return false;
                }

                seen[slot] = true;
            }

            for (var slot = 0; slot < seen.Length; slot++)
            {
                if (!seen[slot])
                {
                    message = $"Surface belt style slot {slot} is missing.";
                    return false;
                }
            }

            message = string.Empty;
            return true;
        }

        private void OnValidate()
        {
            if (!TryValidate(out var message))
            {
                Debug.LogWarning(message, this);
            }
        }
    }
}
