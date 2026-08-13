using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.TextCore.LowLevel;

namespace Game.Feature.UI.Tests
{
    public static class NanumGothicFontValidationUtility
    {
        public const string SourceFontPath = "Assets/_Shared/UI/Fonts/NanumGothic.ttf";
        public const string FontAssetPath = "Assets/_Shared/UI/Fonts/NanumGothic SDF.asset";
        public const string UiKoreanStringTablePath = "Assets/Localization/StringTables/UI/UI_ko-KR.asset";
        public const string StageKoreanStringTablePath = "Assets/Localization/StringTables/Stage/Stage_ko-KR.asset";
        public const int AtlasSize = 2048;
        public const int SamplingPointSize = 90;
        public const int Padding = 9;

        public const string CommonUiCharacters =
            "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz%+-/():[] .,";

        public static readonly string[] SettingsKoreanLabels =
        {
            "설정",
            "오디오",
            "화면",
            "조작",
            "이동 키",
            "밀기",
            "뒤집기",
            "변경",
            "키 설정 초기화",
            "뒤로",
        };

        public static readonly string[] PauseKoreanLabels =
        {
            "일시 정지",
            "계속하기",
            "다시 시도",
            "메인 메뉴",
        };

        public static readonly string[] MainMenuKoreanLabels =
        {
            "시작",
            "설정",
            "종료",
        };

        public static readonly string[] ConfirmPopupKoreanLabels =
        {
            "입력 설정 초기화",
            "입력 설정을 기본값으로 초기화할까요?",
            "초기화",
            "취소",
            "화면 설정을 유지할까요?",
            "{0} × {1} 테두리 없는 창 모드를 적용했습니다. {2}초 안에 확인하지 않으면 이전 설정으로 돌아갑니다.",
            "{0} × {1} 창 모드를 적용했습니다. {2}초 안에 확인하지 않으면 이전 설정으로 돌아갑니다.",
            "변경 사항 유지",
            "되돌리기",
        };

        public static readonly string[] RequiredKoreanStringTablePaths =
        {
            UiKoreanStringTablePath,
            StageKoreanStringTablePath,
        };

        [MenuItem("Tools/UI/Generate NanumGothic TMP Validation Font")]
        public static void GenerateFromMenu()
        {
            GenerateOrThrow();
        }

        public static TMP_FontAsset GenerateOrThrow()
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                throw new InvalidOperationException($"Missing source font: {SourceFontPath}");
            }

            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fontAsset == null)
            {
                fontAsset = CreateFontAsset(sourceFont);
            }

            var characterSet = BuildValidationCharacterSet();
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.ClearFontAssetData();
            if (!fontAsset.TryAddCharacters(characterSet, out var missingCharacters))
            {
                throw new InvalidOperationException(
                    $"NanumGothic validation asset is missing requested glyphs: {FormatCharacters(missingCharacters)}");
            }

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            fontAsset.ReadFontAssetDefinition();
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceUpdate);

            var reloaded = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (reloaded == null)
            {
                throw new InvalidOperationException($"Failed to reload generated font asset: {FontAssetPath}");
            }

            var missingCoverage = GetMissingCharacters(
                reloaded,
                LoadRequiredKoreanFontCoverageStrings().Append(CommonUiCharacters));
            if (missingCoverage.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Generated NanumGothic asset does not cover validation strings: {FormatCharacters(missingCoverage)}");
            }

            return reloaded;
        }

        private static TMP_FontAsset CreateFontAsset(Font sourceFont)
        {
            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                SamplingPointSize,
                Padding,
                GlyphRenderMode.SDFAA,
                AtlasSize,
                AtlasSize,
                AtlasPopulationMode.Dynamic,
                false);

            if (fontAsset == null)
            {
                throw new InvalidOperationException("TMP failed to create a NanumGothic font asset.");
            }

            fontAsset.name = Path.GetFileNameWithoutExtension(FontAssetPath);
            if (fontAsset.material != null)
            {
                fontAsset.material.name = $"{fontAsset.name} Material";
            }

            if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
            {
                fontAsset.atlasTextures[0].name = $"{fontAsset.name} Atlas";
            }

            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
            {
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            }

            if (fontAsset.material != null)
            {
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            return fontAsset;
        }

        public static string BuildValidationCharacterSet()
        {
            var characters = new SortedSet<char>();
            foreach (var text in LoadRequiredKoreanFontCoverageStrings().Append(CommonUiCharacters))
            {
                foreach (var character in text)
                {
                    if (!char.IsControl(character))
                    {
                        characters.Add(character);
                    }
                }
            }

            return string.Concat(characters);
        }

        public static IReadOnlyList<string> LoadRequiredKoreanFontCoverageStrings()
        {
            var strings = new List<string>();
            foreach (var tablePath in RequiredKoreanStringTablePaths)
            {
                strings.AddRange(LoadLocalizedValues(tablePath));
            }

            return strings;
        }

        public static IReadOnlyList<string> LoadLocalizedValues(string tablePath)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>(tablePath);
            if (table == null)
            {
                throw new InvalidOperationException($"Missing String Table asset: {tablePath}");
            }

            return table.SharedData.Entries
                .Select(entry => table.GetEntry(entry.Key)?.LocalizedValue)
                .Where(value => !string.IsNullOrEmpty(value))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        public static IReadOnlyList<char> GetMissingCharacters(TMP_FontAsset fontAsset, IEnumerable<string> texts)
        {
            if (fontAsset == null)
            {
                throw new ArgumentNullException(nameof(fontAsset));
            }

            var missing = new SortedSet<char>();
            foreach (var text in texts)
            {
                foreach (var character in text ?? string.Empty)
                {
                    if (!fontAsset.HasCharacter(character, searchFallbacks: false, tryAddCharacter: false))
                    {
                        missing.Add(character);
                    }
                }
            }

            return missing.ToArray();
        }

        public static string FormatCharacters(IEnumerable<char> characters)
        {
            return string.Join(
                ", ",
                characters.Select(character => $"{character} U+{(int)character:X4}"));
        }
    }
}
