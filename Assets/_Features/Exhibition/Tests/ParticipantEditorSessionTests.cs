using System;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using Game.Feature.Stages;
using Game.Exhibition.Integration;
using Game.Feature.UI.Application;
using Game.Platform.Runtime;
using Game.Platform.Steam;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Exhibition.Tests
{
    public sealed class ParticipantEditorSessionTests
    {
        private bool previousOptionsEnabled;
        private EnterPlayModeOptions previousOptions;
        private SceneAsset previousStartScene;
        private SceneSetup[] previousScenes;
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            previousOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousOptions = EditorSettings.enterPlayModeOptions;
            previousStartScene = EditorSceneManager.playModeStartScene;
            previousScenes = EditorSceneManager.GetSceneManagerSetup();
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            EditorSettings.enterPlayModeOptionsEnabled = previousOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousOptions;
            EditorSceneManager.playModeStartScene = previousStartScene;
            if (previousScenes != null && previousScenes.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
        }

        [UnityTest, Category("Full")]
        public IEnumerator NoDomainReload_ReentryClearsProductionCacheAndShutsDownNativeHost()
        {
            yield return new EnterPlayMode(false);
            var previousResetAdapter = new SteamExhibitionResetAdapter();
            var cache = typeof(CampaignSaveCompositionProvider).GetField("productionComposition", StaticPrivate);
            Assert.That(cache.GetValue(null), Is.Null);
            // A sentinel detects reuse without opening or recovering the user's canonical save root.
            cache.SetValue(null, FormatterServices.GetUninitializedObject(cache.FieldType));
            CampaignSaveCompositionProvider.SuspendProductionAccess();
            var native = new CountingNative();
            var bootstrap = typeof(LocalPlatformRuntime).Assembly.GetType("Game.Platform.Runtime.PlatformRuntimeBootstrap", true);
            var hostType = typeof(LocalPlatformRuntime).Assembly.GetType("Game.Platform.Runtime.PlatformRuntimeApplicationHost", true);
            foreach (Component host in Object.FindObjectsByType(hostType, FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(host.gameObject);
            yield return null;
            bootstrap.GetMethod("ResetSubsystemStateForTests", StaticPrivate, null, Type.EmptyTypes, null).Invoke(null, null);
            var args = new object[] { false, new SteamPlatformRuntimeFactory(() => native), null };
            Assert.That(bootstrap.GetMethod("TryConfigureTestOverride", StaticPrivate).Invoke(null, args), Is.True);
            bootstrap.GetMethod("BootstrapNowForTests", StaticPrivate).Invoke(null, null);
            yield return null;
            Assert.That(native.Initializations, Is.EqualTo(1));
            Assert.That(native.Callbacks, Is.GreaterThan(0));
            yield return new ExitPlayMode();
            Assert.That(native.Shutdowns, Is.EqualTo(1));
            var callbacks = native.Callbacks;
            yield return new EnterPlayMode(false);
            Assert.That(cache.GetValue(null), Is.Null, "Previous Play session production cache must not survive.");
            Assert.That(Assert.Throws<InvalidOperationException>(() => previousResetAdapter.GetIdentity()).Message,
                Does.Contain("previous Play session"), "An old async reset must fail before accessing the next session Steam identity.");
            Assert.That(typeof(CampaignSaveCompositionProvider).GetField("productionAccessSuspended", StaticPrivate).GetValue(null), Is.False);
            Assert.That(ParticipantResetMenuAccess.Current, Is.Null, "Batch tests must not inherit a production feature registration.");
            Assert.That(native.Callbacks, Is.EqualTo(callbacks));
            yield return new ExitPlayMode();
            Assert.That(native.Shutdowns, Is.EqualTo(1));
        }

        private sealed class CountingNative : ISteamNativeApi
        {
            public int Initializations, Callbacks, Shutdowns;
            public bool IsPacksizeCompatible() => true;
            public bool Initialize() { Initializations++; return true; }
            public void RunCallbacks() { Callbacks++; }
            public void Shutdown() { Shutdowns++; }
            public uint GetAppId() => 480;
            public bool IsSteamIdValid() => true;
            public bool IsLoggedOn() => true;
        }
    }
}
