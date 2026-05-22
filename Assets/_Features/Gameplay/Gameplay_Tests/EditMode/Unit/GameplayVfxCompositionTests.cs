using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Authoring;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxCompositionTests
    {
        private static readonly string[] ProductionGuardPaths =
        {
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHost.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHostConfiguration.cs",
            "Assets/_Features/Stages/Runtime/Content/StagePresentationDefinition.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyDeathExitEffectPlanBuilder.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayExitPresentationController.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayFrontFaceShieldVfxPresenter.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayUtilityWindupVfxPresenter.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxFlipInteractionDriver.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/PresentationMotionTrack.cs",
        };

        [Test]
        [Category("Extended")]
        public void Composition_AllowsEmptyConfiguration()
        {
            var result = GameplayVfxBindingComposition.Compose(null, Array.Empty<VfxProfileAsset>());

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Resolver, Is.Not.Null);
            Assert.That(result.HostDefaultMap, Is.SameAs(VfxCueMap.Empty));
            Assert.That(result.FamilyProfiles, Is.Empty);
            Assert.That(result.Validation.HasErrors, Is.False);
            Assert.That(result.Validation.Messages.Select(message => message.Code), Does.Contain("VFX_COMPOSITION_EMPTY"));
            Assert.That(
                result.Resolver.TryResolve(CreateRequest(GameplayVfxCueId.From(PlayerVfxCue.Damage)), out _),
                Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Composition_BuildsHostDefaultResolver()
        {
            var prefab = new GameObject("HostDefaultVfxPrefab");
            var binding = CreateBinding(
                GameplayVfxFamily.Player,
                (int)PlayerVfxCue.Damage,
                prefab,
                maxConcurrentInstances: 3);
            var hostMap = CreateCueMap(binding);

            try
            {
                var result = GameplayVfxBindingComposition.Compose(hostMap, Array.Empty<VfxProfileAsset>());

                Assert.That(result.Succeeded, Is.True);
                Assert.That(
                    result.Resolver.TryResolve(CreateRequest(GameplayVfxCueId.From(PlayerVfxCue.Damage)), out var policy),
                    Is.True);
                Assert.That(policy.CueId, Is.EqualTo(GameplayVfxCueId.From(PlayerVfxCue.Damage)));
                Assert.That(policy.MaxConcurrentInstances, Is.EqualTo(3));
            }
            finally
            {
                Destroy(hostMap, binding, prefab);
            }
        }

        [Test]
        [Category("Extended")]
        public void Composition_BuildsFamilyProfiles()
        {
            var prefab = new GameObject("PlayerProfileVfxPrefab");
            var binding = CreateBinding(
                GameplayVfxFamily.Player,
                (int)PlayerVfxCue.Damage,
                prefab,
                maxConcurrentInstances: 5);
            var profile = CreateProfile(GameplayVfxFamily.Player, binding);

            try
            {
                var result = GameplayVfxBindingComposition.Compose(null, new[] { profile });

                Assert.That(result.Succeeded, Is.True);
                Assert.That(result.FamilyProfiles.Keys, Does.Contain(GameplayVfxFamily.Player));
                Assert.That(
                    result.Resolver.TryResolve(CreateRequest(GameplayVfxCueId.From(PlayerVfxCue.Damage)), out var policy),
                    Is.True);
                Assert.That(policy.MaxConcurrentInstances, Is.EqualTo(5));
            }
            finally
            {
                Destroy(profile, binding, prefab);
            }
        }

        [Test]
        [Category("Extended")]
        public void Composition_ProfileOverridesHostDefault()
        {
            var hostPrefab = new GameObject("HostOverrideVfxPrefab");
            var profilePrefab = new GameObject("ProfileOverrideVfxPrefab");
            var hostBinding = CreateBinding(
                GameplayVfxFamily.Player,
                (int)PlayerVfxCue.Damage,
                hostPrefab,
                maxConcurrentInstances: 2);
            var profileBinding = CreateBinding(
                GameplayVfxFamily.Player,
                (int)PlayerVfxCue.Damage,
                profilePrefab,
                maxConcurrentInstances: 9);
            var hostMap = CreateCueMap(hostBinding);
            var profile = CreateProfile(GameplayVfxFamily.Player, profileBinding);

            try
            {
                var result = GameplayVfxBindingComposition.Compose(hostMap, new[] { profile });

                Assert.That(result.Succeeded, Is.True);
                Assert.That(
                    result.Resolver.TryResolve(CreateRequest(GameplayVfxCueId.From(PlayerVfxCue.Damage)), out var policy),
                    Is.True);
                Assert.That(policy.MaxConcurrentInstances, Is.EqualTo(9));
            }
            finally
            {
                Destroy(profile, hostMap, profileBinding, hostBinding, profilePrefab, hostPrefab);
            }
        }

        [Test]
        [Category("Extended")]
        public void Composition_ProfileFallsBackToHostDefault()
        {
            var hostPrefab = new GameObject("BoxFallbackVfxPrefab");
            var profilePrefab = new GameObject("PlayerFallbackVfxPrefab");
            var hostBinding = CreateBinding(
                GameplayVfxFamily.Box,
                (int)BoxVfxCue.DestroySmoke,
                hostPrefab,
                maxConcurrentInstances: 4);
            var profileBinding = CreateBinding(
                GameplayVfxFamily.Player,
                (int)PlayerVfxCue.Damage,
                profilePrefab);
            var hostMap = CreateCueMap(hostBinding);
            var profile = CreateProfile(GameplayVfxFamily.Player, profileBinding);

            try
            {
                var result = GameplayVfxBindingComposition.Compose(hostMap, new[] { profile });

                Assert.That(result.Succeeded, Is.True);
                Assert.That(
                    result.Resolver.TryResolve(CreateRequest(GameplayVfxCueId.From(BoxVfxCue.DestroySmoke)), out var policy),
                    Is.True);
                Assert.That(policy.CueId, Is.EqualTo(GameplayVfxCueId.From(BoxVfxCue.DestroySmoke)));
                Assert.That(policy.MaxConcurrentInstances, Is.EqualTo(4));
            }
            finally
            {
                Destroy(profile, hostMap, profileBinding, hostBinding, profilePrefab, hostPrefab);
            }
        }

        [Test]
        [Category("Extended")]
        public void Composition_RejectsDuplicateProfileFamily()
        {
            var prefabA = new GameObject("DuplicateProfileVfxPrefabA");
            var prefabB = new GameObject("DuplicateProfileVfxPrefabB");
            var bindingA = CreateBinding(GameplayVfxFamily.Player, (int)PlayerVfxCue.Damage, prefabA);
            var bindingB = CreateBinding(GameplayVfxFamily.Player, (int)PlayerVfxCue.Death, prefabB);
            var profileA = CreateProfile(GameplayVfxFamily.Player, bindingA);
            var profileB = CreateProfile(GameplayVfxFamily.Player, bindingB);

            try
            {
                var result = GameplayVfxBindingComposition.Compose(null, new[] { profileA, profileB });

                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Resolver, Is.Null);
                Assert.That(
                    result.Validation.Messages.Select(message => message.Code),
                    Does.Contain("VFX_COMPOSITION_DUPLICATE_PROFILE_FAMILY"));
                Assert.Throws<InvalidOperationException>(() => result.ThrowIfErrors());
            }
            finally
            {
                Destroy(profileA, profileB, bindingA, bindingB, prefabA, prefabB);
            }
        }

        [Test]
        [Category("Extended")]
        public void Composition_RejectsNullProfileEntry()
        {
            var result = GameplayVfxBindingComposition.Compose(null, new VfxProfileAsset[] { null });

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Resolver, Is.Null);
            Assert.That(
                result.Validation.Messages.Select(message => message.Code),
                Does.Contain("VFX_COMPOSITION_NULL_PROFILE"));
        }

        [Test]
        [Category("Extended")]
        public void Composition_PropagatesInvalidHostMap()
        {
            var prefabA = new GameObject("InvalidHostVfxPrefabA");
            var prefabB = new GameObject("InvalidHostVfxPrefabB");
            var bindingA = CreateBinding(GameplayVfxFamily.Box, (int)BoxVfxCue.DestroySmoke, prefabA);
            var bindingB = CreateBinding(GameplayVfxFamily.Box, (int)BoxVfxCue.DestroySmoke, prefabB);
            var hostMap = CreateCueMap(bindingA, bindingB);

            try
            {
                var result = GameplayVfxBindingComposition.Compose(hostMap, Array.Empty<VfxProfileAsset>());

                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Resolver, Is.Null);
                Assert.That(
                    result.Validation.Messages.Select(message => message.Code),
                    Does.Contain("VFX_CUE_MAP_DUPLICATE_CUE"));
            }
            finally
            {
                Destroy(hostMap, bindingA, bindingB, prefabA, prefabB);
            }
        }

        [Test]
        [Category("Extended")]
        public void Composition_PropagatesInvalidProfile()
        {
            var prefab = new GameObject("CrossFamilyProfileVfxPrefab");
            var binding = CreateBinding(GameplayVfxFamily.Box, (int)BoxVfxCue.DestroySmoke, prefab);
            var profile = CreateProfile(GameplayVfxFamily.Player, binding);

            try
            {
                var result = GameplayVfxBindingComposition.Compose(null, new[] { profile });

                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Resolver, Is.Null);
                Assert.That(
                    result.Validation.Messages.Select(message => message.Code),
                    Does.Contain("VFX_PROFILE_CROSS_FAMILY_BINDING"));
            }
            finally
            {
                Destroy(profile, binding, prefab);
            }
        }

        [Test]
        [Category("Extended")]
        public void Composition_DoesNotConnectProduction()
        {
            var forbiddenTerms = new[]
            {
                "GameplayVfxBindingComposition",
                "VfxCueMapAsset",
                "VfxProfileAsset",
                "CompositeVfxBindingResolver",
                "GameplayVfxPresentationController",
            };

            foreach (var path in ProductionGuardPaths)
            {
                var source = ReadRepoFile(path);
                foreach (var term in forbiddenTerms)
                {
                    Assert.That(source, Does.Not.Contain(term), $"{path} must not reference {term}.");
                }
            }
        }

        [Test]
        [Category("Extended")]
        public void RuntimeCore_DoesNotReferenceAuthoring()
        {
            var asmdef = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Vfx/Gameplay.Vfx.asmdef");
            var runtimeSource = ReadDirectorySources("Assets/_Features/Gameplay/Gameplay_Vfx/Runtime");

            Assert.That(asmdef, Does.Not.Contain("Game.Feature.Gameplay.Vfx.Authoring"));
            Assert.That(runtimeSource, Does.Not.Contain("Game.Feature.Gameplay.Vfx.Authoring"));
            Assert.That(runtimeSource, Does.Not.Contain("VfxCueMapAsset"));
            Assert.That(runtimeSource, Does.Not.Contain("VfxProfileAsset"));
            Assert.That(runtimeSource, Does.Not.Contain("ScriptableObject"));
        }

        private static GameplayVfxRequest CreateRequest(GameplayVfxCueId cueId)
        {
            return new GameplayVfxRequest(
                1,
                1,
                17,
                cueId,
                VfxAnchor.ForEntity(3),
                VfxTimingKind.ImmediateOnTickPresentation);
        }

        private static VfxCueMapAsset CreateCueMap(params VfxBindingDefinitionAsset[] bindings)
        {
            var cueMap = ScriptableObject.CreateInstance<VfxCueMapAsset>();
            SetField(cueMap, "bindings", bindings);
            return cueMap;
        }

        private static VfxProfileAsset CreateProfile(
            GameplayVfxFamily family,
            params VfxBindingDefinitionAsset[] bindings)
        {
            var profile = ScriptableObject.CreateInstance<VfxProfileAsset>();
            SetField(profile, "family", family);
            SetField(profile, "bindings", bindings);
            return profile;
        }

        private static VfxBindingDefinitionAsset CreateBinding(
            GameplayVfxFamily family,
            int cueCode,
            GameObject prefab,
            VfxBindingRequirement requirement = VfxBindingRequirement.Optional,
            VfxMissingAnchorPolicy missingAnchorPolicy = VfxMissingAnchorPolicy.SkipOptional,
            VfxPlaybackMode playbackMode = VfxPlaybackMode.OneShot,
            VfxStopPolicy stopPolicy = VfxStopPolicy.AuthoredDuration,
            float defaultLifetimeSeconds = 0f,
            float tailSeconds = 0f,
            int initialPoolSize = 0,
            int maxConcurrentInstances = 0)
        {
            GameplayVfxTestPrefabFactory.EnsureModelRoot(prefab);

            var binding = ScriptableObject.CreateInstance<VfxBindingDefinitionAsset>();
            SetField(binding, "family", family);
            SetField(binding, "cueCode", cueCode);
            SetField(binding, "prefab", prefab);
            SetField(binding, "requirement", requirement);
            SetField(binding, "missingAnchorPolicy", missingAnchorPolicy);
            SetField(binding, "playbackMode", playbackMode);
            SetField(binding, "stopPolicy", stopPolicy);
            SetField(binding, "defaultLifetimeSeconds", defaultLifetimeSeconds);
            SetField(binding, "tailSeconds", tailSeconds);
            SetField(binding, "initialPoolSize", initialPoolSize);
            SetField(binding, "maxConcurrentInstances", maxConcurrentInstances);
            return binding;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static string ReadDirectorySources(string relativeDirectory)
        {
            return string.Join(
                "\n",
                Directory.GetFiles(Path.GetFullPath(relativeDirectory), "*.cs", SearchOption.AllDirectories)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(File.ReadAllText));
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(Path.GetFullPath(relativePath)).Replace("\r\n", "\n");
        }

        private static void Destroy(params UnityEngine.Object[] unityObjects)
        {
            foreach (var unityObject in unityObjects)
            {
                if (unityObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(unityObject);
                }
            }
        }
    }

    internal static class GameplayVfxTestPrefabFactory
    {
        public static void EnsureModelRoot(GameObject prefab)
        {
            if (prefab == null ||
                prefab.transform.Find(VfxPrefabValidationDiagnostics.ModelRootName) != null)
            {
                return;
            }

            new GameObject(VfxPrefabValidationDiagnostics.ModelRootName)
                .transform.SetParent(prefab.transform, worldPositionStays: false);
        }
    }
}
