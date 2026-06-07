using System;
using System.Reflection;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.BlockAudio;
using Game.Feature.Gameplay.GravityFieldAudio;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.PlayerLocomotionAudio;
using Game.Feature.Gameplay.TileFeatureAudio;
using Game.Feature.Gameplay.TopologyAudio;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayPresentationAudioConfigValidationTests
    {
        private const string GameplayAudioMapPath =
            "Assets/_Features/Gameplay/Gameplay_Audio/Maps/GameplayAudioMap_CampaignV1.asset";
        private const string BlockAudioMapPath =
            "Assets/_Features/Gameplay/Gameplay_BlockAudio/Maps/BlockAudioMap_PlayerSounds.asset";
        private const string PlayerLocomotionAudioMapPath =
            "Assets/_Features/Gameplay/Gameplay_PlayerLocomotionAudio/Maps/PlayerLocomotionAudioMap_PlayerSounds.asset";
        private const string TopologyAudioMapPath =
            "Assets/_Features/Gameplay/Gameplay_TopologyAudio/Maps/TopologyAudioMap_ObjectSounds.asset";
        private const string GravityFieldAudioMapPath =
            "Assets/_Features/Gameplay/Gameplay_GravityFieldAudio/Maps/GravityFieldAudioMap_ObjectSounds.asset";
        private const string TileFeatureAudioMapPath =
            "Assets/_Features/Gameplay/Gameplay_TileFeatureAudio/Maps/TileFeatureAudioMap_ObjectSounds.asset";

        [TestCase("gameplayAudioMap", "gameplayAudioMap is required.")]
        [TestCase("blockAudioMap", "blockAudioMap is required.")]
        [TestCase("playerLocomotionAudioMap", "playerLocomotionAudioMap is required.")]
        [TestCase("topologyAudioMap", "topologyAudioMap is required.")]
        [TestCase("gravityFieldAudioMap", "gravityFieldAudioMap is required.")]
        [TestCase("tileFeatureAudioMap", "tileFeatureAudioMap is required.")]
        [Category("Extended")]
        public void ValidateOrThrow_NullLaneMap_FailsFast(string fieldName, string expectedMessageFragment)
        {
            var config = CreateCanonicalConfig();
            try
            {
                SetSerializedField(typeof(GameplayPresentationAudioConfig), config, fieldName, null);

                var exception = Assert.Throws<InvalidOperationException>(() => config.ValidateOrThrow());

                Assert.That(exception.Message, Does.Contain(expectedMessageFragment));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        [Category("Extended")]
        public void ValidateOrThrow_DelegatesToRequiredLaneValidators()
        {
            AssertDelegatesToValidator(
                config => SetSerializedField(
                    typeof(GameplayPresentationAudioConfig),
                    config,
                    "gameplayAudioMap",
                    CreateEmptyAsset<GameplayAudioMap>("GameplayAudioMap_MissingRequired")),
                "missing required gameplay audio semantics");
            AssertDelegatesToValidator(
                config => SetSerializedField(
                    typeof(GameplayPresentationAudioConfig),
                    config,
                    "blockAudioMap",
                    CreateEmptyAsset<BlockAudioMap>("BlockAudioMap_MissingRequired")),
                "missing required block audio cues");
            AssertDelegatesToValidator(
                config => SetSerializedField(
                    typeof(GameplayPresentationAudioConfig),
                    config,
                    "playerLocomotionAudioMap",
                    CreateEmptyAsset<PlayerLocomotionAudioMap>("PlayerLocomotionAudioMap_MissingRequired")),
                "missing required player locomotion audio cues");
            AssertDelegatesToValidator(
                config => SetSerializedField(
                    typeof(GameplayPresentationAudioConfig),
                    config,
                    "topologyAudioMap",
                    CreateEmptyAsset<TopologyAudioMap>("TopologyAudioMap_MissingRequired")),
                "missing required topology audio cues");
            AssertDelegatesToValidator(
                config => SetSerializedField(
                    typeof(GameplayPresentationAudioConfig),
                    config,
                    "tileFeatureAudioMap",
                    CreateEmptyAsset<TileFeatureAudioMap>("TileFeatureAudioMap_MissingRequired")),
                "missing required tile feature audio cues");
        }

        [Test]
        [Category("Extended")]
        public void ValidateOrThrow_DoesNotRequireGravityOptionalCues()
        {
            var gravityMap = CreateEmptyAsset<GravityFieldAudioMap>("GravityFieldAudioMap_OptionalCuesAbsent");
            var config = CreateCanonicalConfig(gravityFieldAudioMap: gravityMap);
            try
            {
                Assert.DoesNotThrow(() => config.ValidateOrThrow());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
                UnityEngine.Object.DestroyImmediate(gravityMap);
            }
        }

        [Test]
        [Category("Extended")]
        public void ValidateOrThrow_DoesNotRequireTileFeatureOptionalOrBurstCues()
        {
            var tileMap = ScriptableObject.CreateInstance<TileFeatureAudioMap>();
            tileMap.name = "TileFeatureAudioMap_OnlyButtonRequired";
            var definition = CreateDefinition("TileFeatureButtonRequired", out var clip);
            SetTileFeatureEntries(
                tileMap,
                (TileFeatureAudioCue.ButtonActivated, CreateBinding(definition)));
            var config = CreateCanonicalConfig(tileFeatureAudioMap: tileMap);
            try
            {
                Assert.DoesNotThrow(() => config.ValidateOrThrow());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
                UnityEngine.Object.DestroyImmediate(tileMap);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        private static void AssertDelegatesToValidator(
            Action<GameplayPresentationAudioConfig> mutate,
            string expectedMessageFragment)
        {
            var config = CreateCanonicalConfig();
            try
            {
                mutate(config);

                var exception = Assert.Throws<InvalidOperationException>(() => config.ValidateOrThrow());

                Assert.That(exception.Message, Does.Contain(expectedMessageFragment));
            }
            finally
            {
                DestroyIfRuntimeAsset(config.GameplayAudioMap);
                DestroyIfRuntimeAsset(config.BlockAudioMap);
                DestroyIfRuntimeAsset(config.PlayerLocomotionAudioMap);
                DestroyIfRuntimeAsset(config.TopologyAudioMap);
                DestroyIfRuntimeAsset(config.GravityFieldAudioMap);
                DestroyIfRuntimeAsset(config.TileFeatureAudioMap);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        private static GameplayPresentationAudioConfig CreateCanonicalConfig(
            GameplayAudioMap gameplayAudioMap = null,
            BlockAudioMap blockAudioMap = null,
            PlayerLocomotionAudioMap playerLocomotionAudioMap = null,
            TopologyAudioMap topologyAudioMap = null,
            GravityFieldAudioMap gravityFieldAudioMap = null,
            TileFeatureAudioMap tileFeatureAudioMap = null)
        {
            var config = ScriptableObject.CreateInstance<GameplayPresentationAudioConfig>();
            config.name = "GameplayPresentationAudioConfig_ValidationTest";
            SetSerializedField(
                typeof(GameplayPresentationAudioConfig),
                config,
                "gameplayAudioMap",
                gameplayAudioMap != null ? gameplayAudioMap : LoadCanonical<GameplayAudioMap>(GameplayAudioMapPath));
            SetSerializedField(
                typeof(GameplayPresentationAudioConfig),
                config,
                "blockAudioMap",
                blockAudioMap != null ? blockAudioMap : LoadCanonical<BlockAudioMap>(BlockAudioMapPath));
            SetSerializedField(
                typeof(GameplayPresentationAudioConfig),
                config,
                "playerLocomotionAudioMap",
                playerLocomotionAudioMap != null
                    ? playerLocomotionAudioMap
                    : LoadCanonical<PlayerLocomotionAudioMap>(PlayerLocomotionAudioMapPath));
            SetSerializedField(
                typeof(GameplayPresentationAudioConfig),
                config,
                "topologyAudioMap",
                topologyAudioMap != null ? topologyAudioMap : LoadCanonical<TopologyAudioMap>(TopologyAudioMapPath));
            SetSerializedField(
                typeof(GameplayPresentationAudioConfig),
                config,
                "gravityFieldAudioMap",
                gravityFieldAudioMap != null
                    ? gravityFieldAudioMap
                    : LoadCanonical<GravityFieldAudioMap>(GravityFieldAudioMapPath));
            SetSerializedField(
                typeof(GameplayPresentationAudioConfig),
                config,
                "tileFeatureAudioMap",
                tileFeatureAudioMap != null
                    ? tileFeatureAudioMap
                    : LoadCanonical<TileFeatureAudioMap>(TileFeatureAudioMapPath));
            return config;
        }

        private static T CreateEmptyAsset<T>(string name) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            return asset;
        }

        private static void SetTileFeatureEntries(
            TileFeatureAudioMap map,
            params (TileFeatureAudioCue Cue, AudioBinding Binding)[] entries)
        {
            var entryType = typeof(TileFeatureAudioMap).GetNestedType("Entry", BindingFlags.NonPublic);
            Assert.That(entryType, Is.Not.Null);
            var cueField = entryType.GetField("Cue", BindingFlags.Public | BindingFlags.Instance);
            var bindingField = entryType.GetField("Binding", BindingFlags.Public | BindingFlags.Instance);
            var array = Array.CreateInstance(entryType, entries.Length);
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = Activator.CreateInstance(entryType);
                cueField.SetValue(entry, entries[i].Cue);
                bindingField.SetValue(entry, entries[i].Binding);
                array.SetValue(entry, i);
            }

            SetSerializedField(typeof(TileFeatureAudioMap), map, "entries", array);
        }

        private static AudioBinding CreateBinding(AudioDefinition definition)
        {
            var binding = new AudioBinding();
            SetSerializedField(typeof(AudioBinding), binding, "definition", definition);
            SetSerializedField(typeof(AudioBinding), binding, "attachmentSlot", default(AudioAttachmentSlot));
            SetSerializedField(typeof(AudioBinding), binding, "policy", null);
            return binding;
        }

        private static SingleAudioDefinition CreateDefinition(string name, out AudioClip clip)
        {
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            definition.name = name;
            clip = AudioClip.Create(name + "_Clip", 4410, 1, 44100, false);
            SetSerializedField(typeof(SingleAudioDefinition), definition, "clip", clip);
            SetSerializedField(typeof(AudioDefinition), definition, "category", AudioCategory.Sfx);
            SetSerializedField(typeof(AudioDefinition), definition, "defaultVolumeTrim", 1f);
            SetSerializedField(typeof(AudioDefinition), definition, "pitchRange", Vector2.one);
            SetSerializedField(typeof(AudioDefinition), definition, "loop", false);
            return definition;
        }

        private static T LoadCanonical<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }

        private static void DestroyIfRuntimeAsset(UnityEngine.Object asset)
        {
            if (asset != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset)))
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        private static void SetSerializedField(Type declaringType, object target, string fieldName, object value)
        {
            var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {declaringType.Name}.");
            field.SetValue(target, value);
        }
    }
}
