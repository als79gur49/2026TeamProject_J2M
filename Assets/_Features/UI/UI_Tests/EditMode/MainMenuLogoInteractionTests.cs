using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Stages;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Screens;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class MainMenuLogoInteractionTests
    {
        private const string PrefabPath =
            "Assets/_Features/UI/UI_Screens/Prefabs/MainMenuScreen.prefab";
        private const string LogoSpritePath = "Assets/3DM/Sprite/tittl3e.png";
        private const string MaterialPath =
            "Assets/_Features/UI/UI_Screens/Materials/LogoShine_AllIn1.mat";

        [Test]
        public void Prefab_PreservesLogoAndAuthorsEffectHierarchyAndReferences()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<MainMenuScreenView>(PrefabPath);
            var contentHost = FindDescendant(prefab.transform, "ContentHost");
            var effectRoot = FindDirectChild(contentHost, "LogoEffectRoot");
            var glow = FindDirectChild(effectRoot, "ParticleFX_Glow");
            var motion = FindDirectChild(effectRoot, "LogoMotionRoot");
            var logo = FindDirectChild(motion, "Logo");
            var spark = FindDirectChild(effectRoot, "LogoSparkBurst");

            Assert.That(effectRoot.GetSiblingIndex(), Is.GreaterThanOrEqualTo(0));
            Assert.That(glow.GetSiblingIndex(), Is.LessThan(motion.GetSiblingIndex()));
            Assert.That(spark.GetSiblingIndex(), Is.GreaterThan(motion.GetSiblingIndex()));

            var logoImage = logo.GetComponent<Image>();
            Assert.That(logoImage, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(logoImage.sprite), Is.EqualTo(LogoSpritePath));
            Assert.That(logoImage.raycastTarget, Is.False);

            var effect = effectRoot.GetComponent<MainMenuLogoEffectView>();
            Assert.That(effect, Is.Not.Null);
            Assert.DoesNotThrow(effect.ValidateAuthoredStructureOrThrow);
            AssertSerializedReference(effect, "_logoImage", logoImage);
            AssertSerializedReference(effect, "_impactRoot", motion.GetComponent<RectTransform>());
            AssertSerializedReference(effect, "_glowRoot", glow.GetComponent<RectTransform>());
            AssertSerializedReference(effect, "_glowCanvasGroup", glow.GetComponent<CanvasGroup>());
            AssertSerializedReference(effect, "_acceptedBurst", spark.GetComponent<ParticleSystem>());

            Assert.That(glow.GetComponent<ParticleSystem>(), Is.Not.Null);
        }

        [Test]
        public void LogoSprite_UsesHighQualityWindowsImportSettings()
        {
            var importer = AssetImporter.GetAtPath(LogoSpritePath) as TextureImporter;

            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.maxTextureSize, Is.EqualTo(4096));
            Assert.That(importer.textureCompression,
                Is.EqualTo(TextureImporterCompression.Uncompressed));

            var standalone = importer.GetPlatformTextureSettings("Standalone");
            Assert.That(standalone.overridden, Is.True);
            Assert.That(standalone.maxTextureSize, Is.EqualTo(4096));
            Assert.That(standalone.textureCompression,
                Is.EqualTo(TextureImporterCompression.Uncompressed));
        }

        [Test]
        public void MaterialAndSparkBurst_FollowAuthoredRuntimeContracts()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("AllIn1SpriteShader/AllIn1SpriteShaderUiMask"));
            Assert.That(material.IsKeywordEnabled("SHINE_ON"), Is.True);
            Assert.That(material.GetFloat("_ShineGlow"), Is.Zero.Within(0.0001f));
            Assert.That(material.GetFloat("_ShineLocation"), Is.EqualTo(0.03f).Within(0.0001f));
            Assert.That(material.GetFloat("_ShineWidth"), Is.EqualTo(0.14f).Within(0.0001f));
            Assert.That(material.GetFloat("_ShineRotate"), Is.EqualTo(0.785f).Within(0.001f));
            Assert.That(material.GetColor("_Color"), Is.EqualTo(new Color(0.85f, 0.85f, 0.85f, 1f)));
            Assert.That(material.GetColor("_ShineColor"), Is.EqualTo(new Color(1f, 0.65f, 0f, 1f)));

            var prefab = AssetDatabase.LoadAssetAtPath<MainMenuScreenView>(PrefabPath);
            var spark = FindDescendant(prefab.transform, "LogoSparkBurst").GetComponent<ParticleSystem>();
            Assert.That(spark.main.loop, Is.False);
            Assert.That(spark.main.playOnAwake, Is.False);
            Assert.That(spark.main.useUnscaledTime, Is.True);
            Assert.That(spark.main.maxParticles, Is.EqualTo(16));
            Assert.That(spark.emission.rateOverTime.constantMax, Is.Zero);
            Assert.That(spark.emission.rateOverDistance.constantMax, Is.Zero);
            Assert.That(spark.GetComponent<ParticleSystemRenderer>().enabled, Is.False);
        }

        [Test]
        public void Relay_PointerExitRestoresNavigationFocus_AndBlockedStatePublishesNone()
        {
            var instance = InstantiatePrefab();
            var popupController = new PopupController(new FakePopupRuntimeFactory());
            MainMenuLogoFeedbackController controller = null;
            try
            {
                var screen = instance.GetComponent<MainMenuScreenView>();
                screen.ShowSection(MainMenuSectionId.None);
                controller = new MainMenuLogoFeedbackController(
                    screen,
                    screen.LogoEffectView,
                    popupController,
                    settingsOverlayController: null,
                    applicationFocused: true);
                var start = FindDescendant(instance.transform, "StartButton")
                    .GetComponent<MainMenuCommandFeedbackRelay>();
                var settings = FindDescendant(instance.transform, "SettingsButton")
                    .GetComponent<MainMenuCommandFeedbackRelay>();
                var changes = new List<MainMenuLogoFocus>();
                screen.CommandFocusChanged += changed => changes.Add(changed.Current);

                start.SetNavigationFocused(true);
                var navigationShine = GetField<object>(screen.LogoEffectView, "_shineTween");
                Assert.That(navigationShine, Is.Not.Null);
                settings.OnPointerEnter(new PointerEventData(null));
                Assert.That(GetField<object>(screen.LogoEffectView, "_shineTween"),
                    Is.Not.Null.And.Not.SameAs(navigationShine));
                settings.OnPointerExit(new PointerEventData(null));

                Assert.That(changes[^3].CommandId, Is.EqualTo(MainMenuCommandId.Start));
                Assert.That(changes[^3].Source, Is.EqualTo(MainMenuLogoFocusSource.Navigation));
                Assert.That(changes[^2].CommandId, Is.EqualTo(MainMenuCommandId.Settings));
                Assert.That(changes[^2].Source, Is.EqualTo(MainMenuLogoFocusSource.Pointer));
                Assert.That(changes[^1].CommandId, Is.EqualTo(MainMenuCommandId.Start));
                Assert.That(changes[^1].Source, Is.EqualTo(MainMenuLogoFocusSource.Navigation));

                screen.SetCommandFeedbackBlocked(true);
                Assert.That(changes[^1].HasFocus, Is.False);
                var countWhileBlocked = changes.Count;
                settings.OnPointerEnter(new PointerEventData(null));
                Assert.That(changes.Count, Is.EqualTo(countWhileBlocked));
                Assert.That(GetField<UiHoverScaleEffect>(start, "_selectionFeedback"), Is.Not.Null,
                    "The relay must retain the existing pointer/navigation/submit scale delegate.");
                Assert.DoesNotThrow(start.PlaySubmitFeedback);
            }
            finally
            {
                controller?.Dispose();
                popupController.Dispose();
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void Effect_BlockedAndDisableEnable_RestoreStateAndReuseSingleMaterialClone()
        {
            var instance = InstantiatePrefab();
            try
            {
                var effect = instance.GetComponentInChildren<MainMenuLogoEffectView>(true);
                var impactRoot = GetField<RectTransform>(effect, "_impactRoot");
                var logoImage = GetField<Image>(effect, "_logoImage");
                var basePosition = impactRoot.anchoredPosition;
                var baseScale = impactRoot.localScale;

                effect.PlayFocusShine(new MainMenuLogoFocus(
                    MainMenuCommandId.Start,
                    MainMenuLogoFocusSource.Pointer));
                var runtimeMaterial = GetField<Material>(effect, "_logoRuntimeMaterial");
                Assert.That(runtimeMaterial, Is.Not.Null);
                Assert.That(GetField<object>(effect, "_shineTween"), Is.Not.Null);

                effect.PlayAccepted(MainMenuLogoImpactKind.Start);
                Assert.That(GetField<object>(effect, "_impactTween"), Is.Not.Null);
                effect.enabled = false;

                Assert.That(impactRoot.anchoredPosition, Is.EqualTo(basePosition));
                Assert.That(impactRoot.localScale, Is.EqualTo(baseScale));
                Assert.That(runtimeMaterial.GetFloat("_ShineLocation"), Is.EqualTo(0.03f).Within(0.0001f));
                Assert.That(runtimeMaterial.GetFloat("_ShineGlow"), Is.Zero.Within(0.0001f));

                effect.enabled = true;
                effect.PlayFocusShine(new MainMenuLogoFocus(
                    MainMenuCommandId.Settings,
                    MainMenuLogoFocusSource.Navigation));
                Assert.That(GetField<Material>(effect, "_logoRuntimeMaterial"), Is.SameAs(runtimeMaterial));
                Assert.That(logoImage.material, Is.SameAs(runtimeMaterial));

                effect.SetInteractionBlocked(true);
                effect.PlayAccepted(MainMenuLogoImpactKind.Quit);
                Assert.That(GetField<object>(effect, "_impactTween"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void Controller_UsesAcceptedLifecycleOnce_AndIgnoresRawRequest()
        {
            var instance = InstantiatePrefab();
            var popupController = new PopupController(new FakePopupRuntimeFactory());
            MainMenuLogoFeedbackController controller = null;
            try
            {
                var screen = instance.GetComponent<MainMenuScreenView>();
                var effect = screen.LogoEffectView;
                screen.ShowSection(MainMenuSectionId.None);
                controller = new MainMenuLogoFeedbackController(
                    screen,
                    effect,
                    popupController,
                    settingsOverlayController: null,
                    applicationFocused: true);

                screen.ClickQuit();
                Assert.That(GetField<object>(effect, "_impactTween"), Is.Null,
                    "A raw command request is intent only and must not play accepted feedback.");

                screen.ShowSection(MainMenuSectionId.SaveSlots);
                var acceptedTween = GetField<object>(effect, "_impactTween");
                Assert.That(acceptedTween, Is.Not.Null);
                screen.ShowSection(MainMenuSectionId.SaveSlots);
                Assert.That(GetField<object>(effect, "_impactTween"), Is.SameAs(acceptedTween));

                effect.RestoreImmediate();
                controller.NotifyGameplayLaunchAccepted(new SceneEntrySessionToken(41));
                var launchTween = GetField<object>(effect, "_impactTween");
                Assert.That(launchTween, Is.Not.Null,
                    "Accepted gameplay launch remains allowed from the blocked Save Slots domain.");
                controller.NotifyGameplayLaunchAccepted(new SceneEntrySessionToken(41));
                Assert.That(GetField<object>(effect, "_impactTween"), Is.SameAs(launchTween));
            }
            finally
            {
                controller?.Dispose();
                popupController.Dispose();
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void Controller_ApplicationFocusBlockPreventsNewAcceptedImpact()
        {
            var instance = InstantiatePrefab();
            var popupController = new PopupController(new FakePopupRuntimeFactory());
            MainMenuLogoFeedbackController controller = null;
            try
            {
                var screen = instance.GetComponent<MainMenuScreenView>();
                screen.ShowSection(MainMenuSectionId.None);
                controller = new MainMenuLogoFeedbackController(
                    screen,
                    screen.LogoEffectView,
                    popupController,
                    settingsOverlayController: null,
                    applicationFocused: true);

                controller.SetApplicationFocused(false);
                screen.ShowSection(MainMenuSectionId.SaveSlots);
                Assert.That(GetField<object>(screen.LogoEffectView, "_impactTween"), Is.Null);
                controller.NotifyGameplayLaunchAccepted(new SceneEntrySessionToken(42));
                Assert.That(GetField<object>(screen.LogoEffectView, "_impactTween"), Is.Null);
            }
            finally
            {
                controller?.Dispose();
                popupController.Dispose();
                Object.DestroyImmediate(instance);
            }
        }

        private static GameObject InstantiatePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            return Object.Instantiate(prefab);
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).Single(child => child.name == name);
        }

        private static Transform FindDirectChild(Transform root, string name)
        {
            return root.Cast<Transform>().Single(child => child.name == name);
        }

        private static void AssertSerializedReference(
            Object target,
            string propertyName,
            Object expected)
        {
            var property = new SerializedObject(target).FindProperty(propertyName);
            Assert.That(property, Is.Not.Null);
            Assert.That(property.objectReferenceValue, Is.SameAs(expected));
        }

        private static T GetField<T>(object target, string fieldName)
        {
            return (T)target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);
        }
    }
}
