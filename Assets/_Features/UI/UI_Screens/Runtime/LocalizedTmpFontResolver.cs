using System;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;

namespace Game.Feature.UI.Screens
{
    public readonly struct LocalizedTmpFontStyle
    {
        public LocalizedTmpFontStyle(
            TMP_FontAsset fontAsset,
            Material materialPreset = null)
        {
            FontAsset = fontAsset;
            MaterialPreset = materialPreset;
        }

        public TMP_FontAsset FontAsset { get; }

        public Material MaterialPreset { get; }
    }

    public interface ILocalizedTmpFontResolver
    {
        LocalizedTmpFontStyle ResolveFont(
            string localeCode,
            LocalizedTextRole role,
            LocalizedTextWeight weight);
    }

    public sealed class DefaultLocalizedTmpFontResolver : ILocalizedTmpFontResolver
    {
        private readonly TMP_FontAsset _englishFontAsset;
        private readonly Material _englishMaterialPreset;
        private readonly TMP_FontAsset _koreanFontAsset;
        private readonly Material _koreanMaterialPreset;

        public DefaultLocalizedTmpFontResolver(
            TMP_FontAsset koreanFontAsset,
            Material koreanMaterialPreset = null,
            TMP_FontAsset englishFontAsset = null,
            Material englishMaterialPreset = null)
        {
            _koreanFontAsset = koreanFontAsset;
            _koreanMaterialPreset = koreanMaterialPreset;
            _englishFontAsset = englishFontAsset;
            _englishMaterialPreset = englishMaterialPreset;
        }

        public LocalizedTmpFontStyle ResolveFont(
            string localeCode,
            LocalizedTextRole role,
            LocalizedTextWeight weight)
        {
            if (string.Equals(localeCode, "ko-KR", StringComparison.Ordinal))
            {
                return new LocalizedTmpFontStyle(_koreanFontAsset, _koreanMaterialPreset);
            }

            if (string.Equals(localeCode, "en-US", StringComparison.Ordinal))
            {
                return new LocalizedTmpFontStyle(_englishFontAsset, _englishMaterialPreset);
            }

            return default;
        }
    }
}
