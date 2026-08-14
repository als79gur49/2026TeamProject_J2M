using System.IO;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.Gameplay.Tests.Unit
{
    [Category("Extended")]
    public sealed class GameplayPlayerActionCountAssetContractTests
    {
        private const string PrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Prefabs/PlayerActionCountView.prefab";
        private const string GlowPath =
            "Assets/_Features/Gameplay/Gameplay_Host/PresentationAssets/PlayerActionCount/PlayerActionCountGlow.png";
        private const string MaterialPath =
            "Assets/_Features/Gameplay/Gameplay_Host/PresentationAssets/PlayerActionCount/PlayerActionCountGlow_AllIn1.mat";
        private const string ScenePath = "Assets/Scenes/UIAudioScene.unity";

        [Test]
        public void Prefab_IsWorldSpaceNonInteractiveAndHasMultiplicationGlyph()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);

            var view = prefab.GetComponent<GameplayPlayerActionCountView>();
            var canvas = prefab.GetComponent<Canvas>();
            var canvasGroup = prefab.GetComponent<CanvasGroup>();
            var label = prefab.GetComponentInChildren<TMP_Text>(includeInactive: true);
            var effectDriver = prefab.GetComponent<GameplayPlayerActionCountEffectDriver>();
            var motionRoot = prefab.transform.Find("MotionRoot");
            var glowTransform = motionRoot != null ? motionRoot.Find("GlowImage") : null;
            var glowImage = glowTransform != null ? glowTransform.GetComponent<Image>() : null;

            Assert.That(view, Is.Not.Null);
            Assert.That(view.IsReady, Is.True);
            Assert.That(effectDriver, Is.Not.Null);
            Assert.That(effectDriver.IsReady, Is.True);
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(canvasGroup, Is.Not.Null);
            Assert.That(canvasGroup.interactable, Is.False);
            Assert.That(canvasGroup.blocksRaycasts, Is.False);
            Assert.That(label, Is.Not.Null);
            Assert.That(label.raycastTarget, Is.False);
            Assert.That(label.font, Is.Not.Null);
            Assert.That(label.font.HasCharacter('×'), Is.True);
            Assert.That(label.transform.parent, Is.SameAs(motionRoot));
            Assert.That(label.material.shader.name, Is.Not.EqualTo(GameplayPlayerActionCountEffectDriver.AllIn1UiMaskShaderName));
            Assert.That(glowImage, Is.Not.Null);
            Assert.That(glowImage.raycastTarget, Is.False);
            Assert.That(glowImage.rectTransform.sizeDelta, Is.EqualTo(new Vector2(96f, 96f)));
            Assert.That(AssetDatabase.GetAssetPath(glowImage.sprite), Is.EqualTo(GlowPath));
            Assert.That(AssetDatabase.GetAssetPath(glowImage.material), Is.EqualTo(MaterialPath));
            Assert.That(glowImage.material.shader.name, Is.EqualTo(GameplayPlayerActionCountEffectDriver.AllIn1UiMaskShaderName));
        }

        [Test]
        public void GlowAssets_AreProjectOwnedSpriteAndConfiguredAllIn1Material()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GlowPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var importer = AssetImporter.GetAtPath(GlowPath) as TextureImporter;

            Assert.That(sprite, Is.Not.Null);
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo(GameplayPlayerActionCountEffectDriver.AllIn1UiMaskShaderName));
            Assert.That(material.IsKeywordEnabled("HITEFFECT_ON"), Is.True);
            Assert.That(material.IsKeywordEnabled("DISTORT_ON"), Is.True);
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
        }

        [Test]
        public void Assets_KeepExpectedGuidReferencesAndMetaPairs()
        {
            var prefabSource = File.ReadAllText(PrefabPath);
            Assert.That(prefabSource, Does.Contain("guid: f2e8241c705aee496f3853c05e9b8853"));
            Assert.That(prefabSource, Does.Contain("guid: 47354321cb37429685b2490e235771b1"));
            Assert.That(prefabSource, Does.Contain("guid: 82c9968f57bf4e44b83d67ef9d54ca21"));
            Assert.That(prefabSource, Does.Contain("guid: 5ae2438e560847f3a76138cbf98079cc"));
            Assert.That(prefabSource, Does.Contain("guid: ac5a07beaf8b4aac9e90035710837ba4"));

            Assert.That(File.Exists(PrefabPath + ".meta"), Is.True);
            Assert.That(File.Exists(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPlayerActionCountView.cs.meta"), Is.True);
            Assert.That(File.Exists(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPlayerActionCountAnchor.cs.meta"), Is.True);
            Assert.That(File.Exists(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPlayerActionCountPresentationRuntime.cs.meta"), Is.True);
            Assert.That(File.Exists(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPlayerActionCountEffectDriver.cs.meta"), Is.True);
            Assert.That(File.Exists(GlowPath + ".meta"), Is.True);
            Assert.That(File.Exists(MaterialPath + ".meta"), Is.True);
        }

        [Test]
        public void SharedFloatingTextBillboard_PreservesWorldUpOffsetContract()
        {
            var source = File.ReadAllText(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayFloatingTextBillboard.cs");
            Assert.That(source, Does.Contain("Vector3.up * verticalOffset"));
            Assert.That(source, Does.Not.Contain("target.forward * verticalOffset"));
        }

        [Test]
        public void CanonicalGameplayShell_InstallsRuntimeWithPrefabReference()
        {
            var sceneSource = File.ReadAllText(ScenePath);
            Assert.That(sceneSource, Does.Contain(nameof(GameplayPlayerActionCountPresentationRuntime)));
            Assert.That(sceneSource, Does.Contain("guid: ce484a30e46138ddb9aa607718d630b7"));
            Assert.That(sceneSource, Does.Contain("opaqueDurationSeconds: 1"));
            Assert.That(sceneSource, Does.Contain("fadeDurationSeconds: 0.5"));
            Assert.That(sceneSource, Does.Contain("surfaceInsetDistance: 1"));
        }
    }
}
