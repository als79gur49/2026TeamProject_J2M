using System;
using TMPro;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    [DisallowMultipleComponent]
    public sealed class TypographyBinding : MonoBehaviour
    {
        [SerializeField] private TMP_Text target;
        [SerializeField] private TypographyStyleTag styleTag = TypographyStyleTag.Default;
        [SerializeField] private TypographySizingSource sizingSourceOverride = TypographySizingSource.Hybrid;
        [SerializeField] private TypographyApplyMask applyMaskOverride;
        [SerializeField] private bool useApplyMaskOverride;

        [NonSerialized] private bool hasAuthoredState;
        [NonSerialized] private TmpTypographyAuthoredState authoredState;

        public TMP_Text Target => target != null ? target : GetComponent<TMP_Text>();

        public TypographyStyleTag StyleTag => styleTag;

        public TypographySizingSource SizingSourceOverride => sizingSourceOverride;

        public TypographyApplyMask ApplyMaskOverride => applyMaskOverride;

        public bool UseApplyMaskOverride => useApplyMaskOverride;

        public TmpTypographyAuthoredState CaptureAuthoredState()
        {
            if (!hasAuthoredState)
            {
                authoredState = TmpTypographyAuthoredState.Capture(Target);
                hasAuthoredState = true;
            }

            return authoredState;
        }

        public static TypographyBinding FindFor(TMP_Text text)
        {
            if (text == null)
            {
                return null;
            }

            var binding = text.GetComponent<TypographyBinding>();
            if (binding != null && (binding.target == null || binding.target == text))
            {
                return binding;
            }

            var bindings = text.GetComponents<TypographyBinding>();
            for (var i = 0; i < bindings.Length; i++)
            {
                if (bindings[i] != null && bindings[i].target == text)
                {
                    return bindings[i];
                }
            }

            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            target ??= GetComponent<TMP_Text>();
        }
#endif

        private void Reset()
        {
            target = GetComponent<TMP_Text>();
        }
    }

    public readonly struct TmpTypographyAuthoredState
    {
        public readonly TMP_FontAsset OriginalFont;
        public readonly Material OriginalMaterial;
        public readonly FontStyles OriginalFontStyle;
        public readonly float FontSize;
        public readonly bool EnableAutoSizing;
        public readonly float FontSizeMin;
        public readonly float FontSizeMax;
        public readonly float LineSpacing;
        public readonly float CharacterSpacing;

        private TmpTypographyAuthoredState(
            TMP_FontAsset originalFont,
            Material originalMaterial,
            FontStyles originalFontStyle,
            float fontSize,
            bool enableAutoSizing,
            float fontSizeMin,
            float fontSizeMax,
            float lineSpacing,
            float characterSpacing)
        {
            OriginalFont = originalFont;
            OriginalMaterial = originalMaterial;
            OriginalFontStyle = originalFontStyle;
            FontSize = fontSize;
            EnableAutoSizing = enableAutoSizing;
            FontSizeMin = fontSizeMin;
            FontSizeMax = fontSizeMax;
            LineSpacing = lineSpacing;
            CharacterSpacing = characterSpacing;
        }

        public static TmpTypographyAuthoredState Capture(TMP_Text target)
        {
            return target == null
                ? default
                : new TmpTypographyAuthoredState(
                    target.font,
                    target.fontSharedMaterial,
                    target.fontStyle,
                    target.fontSize,
                    target.enableAutoSizing,
                    target.fontSizeMin,
                    target.fontSizeMax,
                    target.lineSpacing,
                    target.characterSpacing);
        }
    }
}
